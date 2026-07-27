using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Put a working fishing spot in a town that never had one — with no ISO change and no injected code.
    ///
    /// The engine cannot be talked into this with plain writes: <c>_LOAD_FISHING_DATA</c> loads
    /// <c>chara/fishing.pak</c>, allocates six CFish, and gathers collision polys. So the game has to RUN the
    /// command. It does that through a script, and a script is reached through an event point:
    ///
    /// <code>
    /// walk into a type-3 event point
    ///   -> the engine matches the point and reads its script label
    ///   -> the VM runs that label
    /// </code>
    /// (engine functions / record offsets: game_data/docs/fishing-engine-re.md §event-dispatch)
    ///
    /// So this needs exactly two data writes, both of which the mod can do:
    ///
    ///   1. **Overwrite a script label's bytecode** in the town's loaded <c>event.stb</c> with a
    ///      <c>_LOAD_FISHING_DATA</c> call carrying our rectangle and water level.
    ///   2. **Create a type-3 event point** naming that label, with a trigger box over the water.
    ///
    /// Walk in; the engine sets the spot up; its own fishing state machine (<c>EdMoveChara</c>, gated on
    /// <see cref="FishingAddresses.Active"/>) takes it from there.
    ///
    /// FIRST-CUT SCOPE: we inject only <c>_LOAD_FISHING_DATA</c>, not the rest of Norune's ~28 KB label-256
    /// state machine (which also handles <c>_INIT_FISH</c>, <c>_EXIT_FISHING</c> and the bait menu). The bet
    /// is that setup is all the script has to do and the engine drives the session. If exiting turns out to
    /// be broken, that is the thing to fix — and we will know precisely, rather than having pre-emptively
    /// relocated 28 KB of branches and string offsets on a guess.
    ///
    /// Everything here is RAM-only and per-load: reloading the town restores the stock script.
    /// </summary>
    internal static class CustomFishingSpot
    {
        internal static bool Enabled = true;

        /// <summary>Verbose fishing instrumentation: per-tick MATCH / GameMode / EventReturnCode transition
        /// logging, the event-slot readback dump, and the collision poly-gather dump. OFF by default (quiet);
        /// flip on while debugging fishing. Purely observational — no game state depends on it.</summary>
        internal static bool Diagnostics = false;

        // BISECT (2026-07-24): the three Brownboo fishing-code instruction patches, toggled individually to find
        // which one(s) cause the Brownboo-specific crash. Add back one at a time; restore all to true when done.

        /// <summary>A spot to install. Rect corners and the water plane come from the town's own
        /// <c>WATER_SURFACE</c> (see GeoramaProbe's dump); the trigger box just has to be somewhere the
        /// player will walk.</summary>
        private readonly struct Spot
        {
            internal readonly int MapNo;
            internal readonly string Name;
            internal readonly int AreaId;                       // which of the five stock fish tables (0-4)
            internal readonly float X1, Z1, X2, Z2;             // the castable rectangle
            internal readonly float Water, Ground;
            internal readonly float TrigX, TrigY, TrigZ;        // trigger point (WORLD — PartIndex is -1)
            internal readonly float Radius;                     // EdGetEvent tests DISTANCE < this

            /// <summary>Where to stand and which way to face while fishing, exactly as Norune's script does
            /// with <c>_SET_NPC_POS</c> / <c>_SET_NPC_ROT</c>. Leave <see cref="Facing"/> NaN to skip the
            /// snap — but then the cast goes wherever the player happened to be looking, which does not
            /// work.</summary>
            internal readonly float StandX, StandY, StandZ, Facing;

            internal bool HasStance => !float.IsNaN(Facing);

            /// <summary>
            /// The FISH rectangle — where fish spawn and wander. SEPARATE from the cast rect (X1..Z2):
            /// <c>_LOAD_FISHING_DATA</c> sets the cast bounds, <c>_INIT_FISH</c> sets the fish bounds, and
            /// they are different globals. So the cast rect can cover the whole lake while fish stay in a
            /// smaller box inside the actual water (away from the shallow shore where they would clip through
            /// the banks). Leave NaN to reuse the cast rect.
            /// </summary>
            internal readonly float FishX1, FishZ1, FishX2, FishZ2;
            internal bool HasFishRect => !float.IsNaN(FishX1);

            /// <summary>Fish depth below the water surface. Vanilla is 12 (FishingInitFish places fish at
            /// WaterLevel-12); shallow ponds want less. Patched per-town into the inline constant. NaN = 12.</summary>
            /// <summary>Shallow fishing: fish sit at WaterLevel-FishDepth (vanilla 12) via a data write, and the
            /// bobber anchor is repointed (data toggle over the cold FishLineStep patch) so the hook rises to
            /// reach them. NaN = vanilla depth. See game_data/docs/fishing-engine-re.md §fishing-line.</summary>
            internal readonly float FishDepth;
            internal bool HasFishDepth => !float.IsNaN(FishDepth);

            /// <summary>DIAGNOSTIC: skip the turi model swap. Proven the model load (not the rect, not pool
            /// memory) is what crashes Brownboo — with the swap skipped it reaches fishing mode.</summary>
            internal readonly bool DiagSkipModel;

            internal Spot(int mapNo, string name, int areaId,
                          float x1, float z1, float x2, float z2, float water, float ground,
                          float tx, float ty, float tz, float radius,
                          float sx = float.NaN, float sy = float.NaN, float sz = float.NaN,
                          float facing = float.NaN, bool diagSkipModel = false,
                          float fx1 = float.NaN, float fz1 = float.NaN, float fx2 = float.NaN,
                          float fz2 = float.NaN, float fishDepth = float.NaN)
            {
                MapNo = mapNo; Name = name; AreaId = areaId;
                X1 = x1; Z1 = z1; X2 = x2; Z2 = z2; Water = water; Ground = ground;
                TrigX = tx; TrigY = ty; TrigZ = tz; Radius = radius;
                StandX = sx; StandY = sy; StandZ = sz; Facing = facing;
                DiagSkipModel = diagSkipModel;
                FishX1 = fx1; FishZ1 = fz1; FishX2 = fx2; FishZ2 = fz2; FishDepth = fishDepth;
            }
        }

        // Water planes are the HEIGHT values the probe read out of each town's water-surface table.
        //
        // Rectangles are kept near 200x200 on purpose: the engine's collision-poly gather HANGS THE GAME
        // above a fixed cap, and a 200x200 spot stays well under. Do not widen these without watching cpoly.
        //
        // The trigger is a RADIUS around a world point, not a box, and the engine matches only ONE point —
        // so an over-large radius wins over every door and their "!" markers vanish (what happened at 2000
        // units). Keep it modest.
        // (poly cap + match test: game_data/docs/fishing-engine-re.md §fishing-load, §event-dispatch)
        private static readonly Spot[] Spots =
        {
            // Queens: the canal (static WATER e03c01/c02/c08), surface at Y=31. Trigger + fishing sign on the
            // north bank at (250,70,-70) — bank collision Y=70 confirmed (georama_collision), water is just
            // south (canal Z[-50,52]); stance faces +Z (south) across the canal. Radius 10 = tight "!" bubble.
            // ⚠ RECT is 1140x250 (viewer-matched, spans both bridges). This is FAR over the ~200x200 the
            // poly-gather (PickUpPoly, hard 1024 cap, no bounds check -> stack smash) is safe at. Watch the
            // "FISHING SPOT LOADED" cpoly count (flip Diagnostics on); if it nears 1024, decouple — keep a
            // small cast/gather rect here and move the big roam box to the FishX1..FishZ2 params.
            new Spot(2, "Queens canal", 6,      // area 6 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — 100% Bobo
                     -240f, -100f, 900f, 150f, water: 31f, ground: 10f,
                     tx: 250f, ty: 70f, tz: -70f, radius: 10f,
                     sx: 250f, sy: 70f, sz: -70f, facing: 0f),           // stance: face +Z (south) toward the water

            // Brownboo: the pond (static WATER s04w01). WATER_SURFACE centred on the origin, ±120, HEIGHT 0.
            // Stance at the +X edge facing the water: (74, 10, -20), yaw -1.639 — forward (-1.00, -0.07).
            //
            // Brownboo's central pond is unfishable: it has a BOARDWALK over it, and _LOAD_FISHING_DATA's
            // poly gather (PickUpPoly, fixed 1024-poly buffer, NO bounds check, box spans the full ±1000 Y)
            // scoops up the entire boardwalk mesh for any rect touching its footprint — >1024 polys smashes
            // the stack and crashes on entry (the player position reads (0,0,0) right after, i.e. corrupted).
            // 180x180, 70x70 near the bank, AND 40x40 over the pond centre all crashed.
            //
            // Brownboo pond, at the FIRST spot tried — stance (74, 10, -20), yaw -1.639 -> forward
            // (-1.00, -0.07), toward the pond centre. This crashed repeatedly before, but that was
            // _CLEAR_VILLAGER_BUFF (it rewinds the villager allocator without deleting the objects, and the
            // model loads over memory Brownboo still references), NOT the location or the boardwalk.
            // The vanilla villager clear (cmd 38/57, behind the fade) handles this; the spot works.
            //
            // WATER LEVEL = 0: confirmed by eye (bobber sits right on the surface at 0), and the near-water
            // heights are ~1 and below — the ~7 readings were the raised banks, not the waterline. So the
            // rejected casts are NOT the height check; they are casts landing outside the RECT.
            //
            // Brownboo is almost all water, so the rect covers the whole MAP (extent from the overhead
            // edges: x ~-347..320, z ~-289..307), not just the +/-120 central pond. WATCH cpoly on the
            // FISHING SPOT LOADED line: this is a large rect and the poly gather has a hard 1024 cap
            // (overflow crashes). Brownboo's water is sparse so it should stay well under, but confirm.
            // Cast rect (X1,Z1,X2,Z2 = W,N,E,S edges): W=-320, N=-260, E=310, S=300. Corners over land are
            // rejected by the native terrain (bobber rests above water+5) — this still works with the
            // floors-only experiment, since those rejections come from the floor polys we KEEP, not the
            // walls we drop. STILL WATCH cpoly on the FISHING SPOT LOADED line: the poly GATHER (PickUpPoly)
            // runs before our wall-removal and has a hard 1024 cap, so widening the rect can only be checked
            // by watching the count — if it approaches 1024 we must decouple the fish rect (roam bounds) from
            // this cast/gather rect. Mirrored in tools/brownboo_viewer.py (RECT_*).
            new Spot(14, "Brownboo lake", 5,    // area 5 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — Piccoly/Negie/Gummy + Garayan
                     -250f, -240f, 250f, 240f, water: 0f, ground: -15f,   // ±250 W/E, ±240 N/S, centre (0,0)
                     // trigger + stance just south of the sign (212,-53); face NORTH (yaw pi = -Z) toward the sign (212,-61)
                     tx: 212f, ty: 12f, tz: -53f, radius: InteractRadius,
                     sx: 212f, sy: 10f, sz: -53f, facing: 3.14159f,
                     fishDepth: 7.6f      // shallow pond: fish at WaterLevel-7.6 (write the CFrame translation Y @ fish+0x1264, the authoritative depth). And the bobber anchor
                                          // is toggled to point 21 (data write over the cold FishLineStep patch) so
                                          // the hook rises to ~-3 / bait ~-6 to reach them. All data-only —
                                          // patching hot fishing code crashes PCSX2 (recompiler). Line length
                                          // (cast reach) unchanged.
                     // BISECT RESULT (2026-07-23): stilts garbage is NOT the model load and NOT the buffer
                     // clears (both skipped -> still garbage). Clobber is intrinsic to entering fishing mode
                     // (fishing-init / HUD texture setup evicting the stilts' GS block).
                     // Shallow fishing: the RESTING hook follows the fishing-line physics, NOT the rod animation
                     // (in the waiting state it is not pinned to the rod bone). Moving the bobber's anchor toward
                     // the hook shortens the below-water run so it rises; done via the cold-patch data toggle
                     // (InstallShallowLinePatch / SetShallowLine). See game_data/docs/fishing-engine-re.md §fishing-line.
                     ),

            // Yellow Drops: the yellow liquid.
            //
            // The trigger must be somewhere the player can actually STAND. An earlier attempt put it at the
            // centre of the WATER_SURFACE record (0, 1, 0) — the middle of the pool, i.e. exactly the place
            // nobody can walk. It could never fire.
            //
            // STANCE, captured live at the water's edge facing the liquid: (-582.9, 9.6, -276.8), yaw 2.31.
            // The script snaps the player to it, as Norune's does.
            //
            // The RECT IS IN FRONT OF THE PLAYER, not around them. An earlier version centred it on the
            // trigger — which is where the player STANDS, i.e. dry land — so the cast had nowhere to land.
            // Forward is (sin yaw, cos yaw): confirmed against Norune, whose _SET_NPC_ROT ry = pi puts the
            // water at -Z, and whose rect does extend toward -Z from where the player stands. Pushing the
            // 200x200 rect 100 units along forward puts the player just inside the near edge — again exactly
            // Norune's geometry.
            //
            // Water level is still the town's declared WATER_SURFACE height (1). Note the trigger is OUTSIDE
            // that surface's square (+/-320 about the origin), so this liquid is probably NOT that surface —
            // if the bobber floats above or sinks below the visible liquid, this is the number to move.
            new Spot(23, "Yellow Drops liquid", 7,  // area 7 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — Tarton/Nonky/Negie/Bon
                     -609f, -444f, -409f, -244f, water: 1f, ground: -15f,
                     tx: -575f, ty: 9f, tz: -286f, radius: InteractRadius,
                     sx: -582.9f, sy: 9.6f, sz: -276.8f, facing: 2.31f),
        };

        /// <summary>
        /// How close you must stand for the "!" to show and X to work.
        ///
        /// Read off the game rather than guessed. Across every town dumped, the two kinds of point you walk up
        /// to and press X on are DOORS (type 1, radius 10) and ITEM pickups (type 2, radius 15) — 302 and 406
        /// of them respectively, with no variation. 80 was fine while the point fired on contact; as a prompt
        /// it lights up from halfway across the town.
        /// </summary>
        private const float InteractRadius = 10f;

        /// <summary>
        /// Labels that must NOT be hijacked.
        ///
        /// The cutoff was 200, and that let label **256** through — which in Yellow Drops is the TOWN'S OWN
        /// script (3196 bytes, by far the biggest). Overwriting it left the screen black on load. 256 is
        /// only the fishing script in NORUNE; elsewhere it is the town's main event, and its size is exactly
        /// what made "pick the biggest region" choose it.
        ///
        /// The 300+ block is per-event scripting and is what we have been safely overwriting all along
        /// (310, 305, 304). Everything below it is either an engine handler or the town itself.
        /// </summary>
        /// <summary>
        /// Label ids we may hijack for the fishing scripts, in every town. Derived by an OFFLINE scan of all
        /// 33 town event.stb files in the (immutable) vanilla ISO: these are the ONLY 300-block labels never
        /// dispatched by any town's script (via _NEXT_EVENT/_FADEOUT_TO_EVENT) — i.e. dead placeholder slots
        /// everywhere. Notably 300 is EXCLUDED: it is a real, dispatched event in Queens (e03) and Yellow
        /// Drops (s13), so hijacking it — as the old size-first picker did — silently broke a town event.
        /// A fixed whitelist beats a runtime scan here: the data can't change, a scan that found everything
        /// used would leave the spot uninstallable, and one list keeps every area consistent. (BuildArena
        /// still drops any of these a live event POINT references — cheap insurance for a future area — but
        /// no town's event points touch the 300-block, verified live for the three fishing towns.)
        /// </summary>
        // 301-310: the towns' own native spare labels (offline-verified never dispatched). This pool is the
        // FALLBACK path only — a spot on an unpatched disc, or a town without baked labels. The three custom
        // fishing towns are ISO-patched with labels 9600/400/133/134 already numbered (IsoPatcher.ExtendStb),
        // which the installer claims directly by id (ClaimLabel/FindLabelById), bypassing this pool entirely.
        private static readonly System.Collections.Generic.HashSet<int> SafeHijackLabels =
            new System.Collections.Generic.HashSet<int>
            { 301, 302, 303, 304, 305, 306, 307, 310 };

        private static int _installedMap = -1;

        // Location of the installed fishing bytecode, so CanalTide can re-bake just its water arg on a tide
        // change (see RebuildFishingScript). 0 = not installed.
        private static long _fishStb;
        private static int _fishCodeOff, _fishEnd, _fishMenuCbRel;

        /// <summary>Re-write ONLY the fishing bytecode in place so its baked water level picks up the current
        /// tide (BuildFishingBytecode re-reads <see cref="CanalTide.QueensWaterLevel"/>). Skips itself during a
        /// live session (never rewrite a running script) and if the town's stb has moved (a rebuild — the
        /// install path handles that). Queens only; no labels or event points are touched.</summary>
        internal static void RebuildFishingScript()
        {
            if (_installedMap != CanalTide.QueensMapNo || _fishStb == 0 || InFishingWindow) return;
            long stb = TownScript.Base();
            if (stb == 0 || stb != _fishStb) return;
            WriteScript(stb, _fishCodeOff, _fishEnd, BuildFishingBytecode(_spot, _fishMenuCbRel),
                        "re-bake fishing water level for the current tide");
        }
        private static int _lastSeenMap = int.MinValue;
        private static int _settleTicks;

        private static int _slot = -1;
        private static long _slotAddr;
        private static Spot _spot;
        private static int _lastParam = int.MinValue;
        private static int _lastMode = int.MinValue;
        private static int _lastGameMode = int.MinValue;
        private static int _watchdog;

        // ── Shallow-hook (Brownboo) — DATA-ONLY, recompiler-safe. See TownAddresses.FishLineShallow. ─────────
        private static bool _shallowLineInstalled;

        /// <summary>Install the cold-window FishLineStep rewrite ONCE (from ApplyNewChanges, before any fishing
        /// has JIT-compiled the function). It re-points the bobber's six anchor loads at a mod data global, so
        /// from then on the anchor is chosen by a pure data write — no more code writes, no recompiler hazard.
        /// Verify-before-write: if the sites aren't vanilla (someone fished before the mod started), it skips
        /// rather than corrupt hot code.</summary>
        private static bool _shallowLineGaveUp;

        internal static void InstallShallowLinePatch()
        {
            if (_shallowLineInstalled || _shallowLineGaveUp) return;

            // Safety: if a fishing session has already happened this boot, FishLineStep is JIT'd (hot) and writing
            // it would crash. That can only occur if fishing beat this poll (loaded onto a spot, fished in ~1s).
            // In that rare case, give up permanently — leave vanilla depth, never patch hot code.
            if (_anyFishingSeen)
            {
                _shallowLineGaveUp = true;
                Log("shallow-line: a fishing session preceded the cold patch — SKIPPING permanently (vanilla depth, no crash)");
                return;
            }

            // Poll: only patch once the game's FishLineStep code is actually present (all six sites read vanilla).
            // Before the game finishes loading, or mid-transition, the read is garbage — so we RETRY (called from
            // ApplyNewChanges AND every Tick) rather than give up. This still lands well before any fishing JITs
            // the function. (Fishing hot-JITs it in the recompiler but leaves the EE memory bytes vanilla, so the
            // read can't tell "hot" from "cold" — the early timing is what keeps the write safe.)
            bool allVanilla = true, allPatched = true;
            foreach (var (lui, ld, reg) in FishLineShallow.Sites)
            {
                uint gotLui = Memory.ReadUInt(lui);
                if (gotLui != FishLineShallow.OrigLui)     allVanilla = false;
                if (gotLui != FishLineShallow.NewLui(reg)) allPatched = false;
            }
            if (allPatched) { _shallowLineInstalled = true; return; }   // already patched this boot (mod relaunch)
            if (!allVanilla) return;                                    // not loaded yet — retry next tick

            Memory.WriteUInt(FishLineShallow.BobberPtr, FishLineShallow.PointVanilla);   // default anchor = point[18]
            foreach (var (lui, ld, reg) in FishLineShallow.Sites)
            {
                Memory.WriteUInt(lui, FishLineShallow.NewLui(reg));   // lui $reg, 0x01FB
                Memory.WriteUInt(ld,  FishLineShallow.NewLw(reg));    // lw  $reg, 0x4000($reg)
            }
            _shallowLineInstalled = true;
            Log("shallow-line: FishLineStep bobber anchor now reads the data global (cold patch installed)");
        }

        /// <summary>Per-town data toggle: bobber at point 21 (shallow) or point 18 (vanilla). Safe any time —
        /// the cold patch already made FishLineStep read this global every frame.</summary>
        private static void SetShallowLine(bool shallow)
        {
            if (!_shallowLineInstalled) return;
            Memory.WriteUInt(FishLineShallow.BobberPtr,
                             shallow ? FishLineShallow.PointShallow : FishLineShallow.PointVanilla);
        }

        /// <summary>Move the spawned fish to WaterLevel-FishDepth by writing their depth directly (data only) —
        /// replaces the crash-prone FishingInitFish code patch.
        ///
        /// Depth is the CFrame translation Y at <c>fish+0x1264</c> (Fish::Get/SetPosition route through the frame
        /// at fish+0x1250; the +0xB0..0xB8 "LivePos" fields are only a readout cache). FishingStepFish moves the
        /// fish with a depth delta hardcoded to 0, so the depth is a fixed point of its per-frame read/add/write
        /// loop — one write to the real translation sticks (we also mirror the cache for the same-frame visual).</summary>
        private static void ApplyShallowFishDepth(Spot spot)
        {
            if (!spot.HasFishDepth) return;
            uint p = Memory.ReadUInt(FishingSpot.Fish) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long baseAddr = Memory.ToMmu(p);
            float depth = Memory.ReadFloat(FishingSpot.WaterLevel) - spot.FishDepth;
            int num = Memory.ReadInt(FishingSpot.FishNum);

            const int FrameDepthY = 0x1264;   // CFrame translation Y (depth) — the authoritative field
            for (int i = 0; i < num && i < 6; i++)
            {
                long fish = baseAddr + (long)i * FishSlotOffsets.Stride;
                Memory.WriteFloat(fish + FrameDepthY, depth);                 // authoritative
                Memory.WriteFloat(fish + FishSlotOffsets.LivePosZ, depth);    // readout cache (immediate visual)
            }
            Log($"   fish moved to WaterLevel-{spot.FishDepth} ({num} fish)");
        }

        private static string GameModeName(int gm) => gm switch
        {
            EditLoop.GameModeWalking  => "walking",
            EditLoop.GameModeOverhead => "overhead camera",
            EditLoop.GameModeEvent    => "EVENT MODE — EventMode() runs, the return code gets consumed here",
            EditLoop.GameModeFishing  => "*** FISHING ***",
            _ => "",
        };

        internal static void Tick()
        {
            if (!Enabled) return;

            InstallShallowLinePatch();   // idempotent retry: lands the cold FishLineStep patch once the game's
                                         // code is present (ApplyNewChanges may fire before it is), before fishing

            int map = Memory.ReadInt(EditLoop.MapNo);
            CameraPassThrough.Apply(map);   // Brownboo: let the follow-camera pass through buildings (MapNo-gated inside)

            // Leaving a town and coming back RELOADS it — the script buffer is re-read and the event array
            // rebuilt, so our install is gone. Remembering "already installed for map 23" would then skip a
            // town whose spot no longer exists. Reset the moment the map changes at all.
            if (map != _lastSeenMap)
            {
                _lastSeenMap = map;
                ResetInstallState();
            }

            if (map == _installedMap)
            {
                // A town can be rebuilt WITHOUT the map number changing — the initial save-load finishing its
                // build after we installed, or stepping out of a building in the same area reloads the event
                // script and wipes our label + event point. Detect that our install is actually still present;
                // if it's gone, drop back to the install path (never mid-session). This is why the trigger
                // vanished on first load and after leaving a building.
                if (!InFishingWindow && ++_verifyTicks >= 20)
                {
                    _verifyTicks = 0;
                    if (!FishingInstallPresent())
                    {
                        int ty = _slotAddr == 0 ? -99 : Memory.ReadInt(_slotAddr + EventPoints.Type);
                        Log($"install incomplete (slot={_slot} type={ty}) — re-installing");
                        ResetInstallState();
                    }
                }
                if (map == _installedMap)
                {
                    WatchMatches(); UpdateFishingWindow(); UpdateVillagerHide(); PriscleenFish.Tick();
                    if (_spot.MapNo == 14) VillagerPlacement.PinMango();   // Brownboo: nudge Mango out from under the (baked) sign
                    return;
                }
            }

            if (!TryGetSpot(map, out Spot spot)) return;

            // The script buffer and the event array are both populated LATE, and the event array is built up
            // progressively. Installing into a half-built town is how you get silent nonsense, so wait.
            bool ready = ScriptReady();
            int epBase = (int)EventPoints.Base();
            int epCount = epBase == 0 ? 0 : Memory.ReadInt(EventPoints.Count);
            if (!ready || epBase == 0 || epCount <= 0)
            {
                _settleTicks = 0;
                if (++_notReadyTicks % 60 == 0)          // ~3s: surface WHY the install can't fire yet
                    Log($"waiting to install (map {map}): scriptReady={ready} eventBase={epBase != 0} count={epCount}");
                return;
            }
            _notReadyTicks = 0;
            if (++_settleTicks < 20) return;              // ~1 s of stability

            _installedMap = map;
            _settleTicks = 0;
            Install(spot);
        }

        private static int _verifyTicks;
        private static int _notReadyTicks;

        /// <summary>Clear all per-install state so the install path re-runs from scratch — used both on a map
        /// change and when a same-map town rebuild has wiped our install.</summary>
        private static void ResetInstallState()
        {
            _installedMap = -1;
            _fishStb = 0;
            _slot = -1;
            _slotAddr = 0;
            _fishingWasLive = false;
            InFishingWindow = false;
            _savedVillagerCount = -1;  // count belongs to the old town; don't restore it into the new one
            _drawFlagsSaved = false;
            _settleTicks = 0;
            _verifyTicks = 0;
            _lastParam = int.MinValue;
            _lastMode = int.MinValue;
            SetShallowLine(false);  // data-only: bobber anchor back to vanilla point[18] for the next town
            PriscleenFish.Uninstall();
            VillagerPlacement.Uninstall();
        }

        /// <summary>True only if BOTH halves of our install are still live: the renumbered fishing label in
        /// the stb, AND our event point. A cold save-load can leave a PARTIAL install — the label gets
        /// written but TryCreateEventPoint fails (no donor/free slot yet) or the point is wiped as the load
        /// finishes — and a label-only check would wrongly report "installed" forever, so the "!" never
        /// appears (the bug on first load into Yellow Drops). Checking the event point too forces a retry.</summary>
        private static bool FishingInstallPresent()
        {
            // (1) our event point must exist and still be a live script trigger
            if (_slot < 0 || _slotAddr == 0) return false;
            if (Memory.ReadInt(_slotAddr + EventPoints.Type) != EventPoints.TypeScript) return false;
            if (Memory.ReadInt(_slotAddr + EventPoints.ItemOrLabel) != FishingLabelId) return false;

            // (2) our fishing label must still be in the stb label table
            long stb = TownScript.Base();
            if (stb == 0) return true;                       // can't tell yet — don't trigger a reinstall
            int n = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            if (n <= 0 || n >= 4000 || tbl <= 0) return true;   // mid-rebuild / not ready — wait, don't thrash
            for (int i = 0; i < n; i++)
                if (Memory.ReadInt(stb + tbl + (long)i * TownScript.LabelStride) == FishingLabelId) return true;
            return false;
        }

        private static bool TryGetSpot(int map, out Spot spot)
        {
            foreach (var s in Spots)
                if (s.MapNo == map) { spot = s; return true; }
            spot = default;
            return false;
        }

        private static bool ScriptReady()
        {
            long stb = TownScript.Base();
            if (stb == 0) return false;
            int n = Memory.ReadInt(stb + TownScript.LabelCount);
            int t = Memory.ReadInt(stb + TownScript.LabelTable);
            return n > 0 && n < 4000 && t > 0;
        }

        private static void Install(Spot spot)
        {
            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);

            Log($"installing '{spot.Name}' (MapNo {spot.MapNo})");

            Log($"   pool at install: used {Memory.ReadInt(FishingPool.Used) * FishingPool.BlockSize / 1024} KB " +
                $"of {Memory.ReadInt(FishingPool.Capacity) * FishingPool.BlockSize / 1024} KB");

            BuildArena(stb, labelCount, tbl);   // native-orphan pool, used only as the unbaked-town fallback

            // Each script goes into the ISO-baked label that already carries its final id (9600/400/133/134),
            // sized to fit it in one label — so nothing is renumbered or split. ClaimLabel falls back to a
            // renamed native orphan on an unpatched disc. The MENU is claimed FIRST so its offset is known
            // before the entry/quit scripts CALL_FUNC it; on the fallback path that also marks its orphan used
            // before the entry script's arena is carved out. If it can't be placed, menuCbRel stays -1 and
            // both menus fall back to inline copies.
            int codeBaseVal = Memory.ReadInt(stb + TownScript.CodeBase);
            int menuCbRel = -1;
            Lab menuLab = ClaimLabel(stb, labelCount, tbl, MenuSubLabelId, Need(BuildMenuSubroutine()), out int menuEnd);
            if (menuLab != null)
            {
                Memory.WriteInt(stb + menuLab.Entry, MenuSubLabelId);   // no-op for a baked label; renames a fallback orphan
                WriteScript(stb, menuLab.Off, menuEnd, BuildMenuSubroutine(),
                            "shared menu-select subroutine (CALL_FUNC target for entry + quit menus)");
                menuCbRel = menuLab.Off - codeBaseVal;
                Log($"   menu subroutine: label {MenuSubLabelId} (code @+0x{menuLab.Off:X}, CALL_FUNC cb-rel 0x{menuCbRel:X})");
            }
            else Log("   no spare label for the shared menu subroutine — entry/quit menus fall back to inline");

            Lab lab = ClaimLabel(stb, labelCount, tbl, FishingLabelId, Need(BuildFishingBytecode(spot, menuCbRel)), out int end);
            if (lab == null)
            {
                Log("   the spare labels cannot hold the fishing script — skipping");
                return;
            }

            // The entry label answers to an id nothing else dispatches (400): only OUR event point names it,
            // so no town event of its own can reach the fishing script.
            int codeOff = lab.Off;
            Memory.WriteInt(stb + lab.Entry, FishingLabelId);   // no-op for a baked label; renames a fallback orphan
            int labelId = FishingLabelId;

            Log($"   entry script @0x{stb:X}  labels={labelCount}  label {labelId} " +
                $"(code @+0x{codeOff:X}, {end - codeOff}B region)");

            WriteScript(stb, codeOff, end, BuildFishingBytecode(spot, menuCbRel),
                        $"_LOAD_MAIN_CHARA({FishingModel}) + _LOAD_FISHING_DATA(area={spot.AreaId}, " +
                        $"water={spot.Water}) + stance + bait + fishing");
            // remember exactly where the fishing bytecode lives so the CanalTide tide change can re-bake just
            // its water arg (BuildFishingBytecode re-reads CanalTide.QueensWaterLevel) without touching the
            // labels or the event point.
            _fishStb = stb; _fishCodeOff = codeOff; _fishEnd = end; _fishMenuCbRel = menuCbRel;

            InstallEngineLabel(stb, labelCount, tbl, EventPoints.FishingExitLabel, BuildExitBytecode(spot, menuCbRel),
                               $"restore {NormalModel} + re-place player + _EXIT_FISHING   [Circle = leave]");
            InstallEngineLabel(stb, labelCount, tbl, EventPoints.FishingBaitLabel, BuildBaitBytecode(),
                               $"_GOTO_CHANGE_ESA + load the chosen bait   [Square = bait menu]");

            if (!TryCreateEventPoint(spot, labelId, out int slot))
            {
                Log("   NO FREE EVENT POINT SLOT — the trigger could not be created");
                return;
            }
            _slot = slot;
            _slotAddr = EventPoints.Slot(EventPoints.Base(), slot);
            _spot = spot;

            // Shallow hook (data-only): point the cold-patched bobber anchor at point 21 for spots that want it.
            // The fish are moved to match on the fishing-window open (ApplyShallowFishDepth), once they've spawned.
            SetShallowLine(spot.HasFishDepth);

            if (spot.MapNo == 14) PriscleenFish.Install();   // Priscleen (DC2 fish) into species 8, Brownboo only

            Log($"   event point [{slot}] type=3 label={labelId} " +
                $"pos=({spot.TrigX},{spot.TrigY},{spot.TrigZ}) radius={spot.Radius} partIndex=-1 (world)");
            if (spot.HasFishRect)
                Log($"   fish rect ({spot.FishX1},{spot.FishZ1})-({spot.FishX2},{spot.FishZ2}) " +
                    $"(cast rect is separate)");

            // Read it back. Three attempts have now "succeeded" and done nothing, so verify what the engine
            // will actually see rather than trusting that the writes landed as intended.
            DumpSlot("   readback:", _slotAddr);
            Log("   walk toward the point; the watcher below reports every event match the engine makes");
        }

        // Toan has TWO models. The ordinary one has no fishing rod and no fishing motions — which is why the
        // "cast" plays whatever animation happens to sit at the fishing motion's index (the atla-opening one).
        // c01d_turi ("turi" = 釣り, fishing) is the fishing Toan: it carries the rod and the right motion table.
        //
        // This is not optional dressing. _GOTO_FISHING does
        //     SearchFrame(chara->model, "sao")        // 竿 = fishing rod
        // and hands that frame to FishLineInit. On a model with no `sao` frame there is no rod to hang the
        // line from, so the line, float and bait have nowhere to be.
        //
        // Norune swaps the model on the way in and swaps it back on the way out.
        private const string FishingModel    = "chara/c01d_turi.chr";
        private const string FishingModelCfg = "c01d_turi.cfg";
        private const string NormalModel     = "chara/c01d.chr";
        private const string NormalModelCfg  = "info.cfg";

        /// <summary>One hijackable label: its table slot, its id, and the code region it owns.</summary>
        private sealed class Lab
        {
            internal int Slot, Id, Off, Size, Entry;
            internal bool Used;
        }

        private static readonly System.Collections.Generic.List<Lab> _arena =
            new System.Collections.Generic.List<Lab>();

        /// <summary>
        /// Collect the hijackable labels, in CODE ORDER.
        ///
        /// Label code regions tile the buffer end to end — each label's code runs until the next label's
        /// <c>codeOffset</c>. So a run of ADJACENT spare labels is one contiguous span we can write straight
        /// through, which is the only way the ~2 KB entry script fits: the spare labels in Yellow Drops are
        /// 650-800 B apiece.
        /// </summary>
        private static void BuildArena(long stb, int labelCount, int tbl)
        {
            _arena.Clear();

            // PROTECT LABELS A LIVE EVENT POINT DISPATCHES. We only ever hijack labels the town isn't using —
            // system labels (<300) are protected by id, but a town CAN put a real story trigger on a >=300
            // label. So also protect any label an active type-3 event point references (ItemOrLabel). This is
            // the guarantee that installing a fishing spot never silently breaks a story/quest trigger; without
            // it, Allocate could retire a label something in the world still fires.
            var referenced = new System.Collections.Generic.HashSet<int>();
            long arr = EventPoints.Base();
            int epn = arr == 0 ? 0 : Memory.ReadInt(EventPoints.Count);
            for (int i = 0; i < epn && i <= MaxEventPoints; i++)
            {
                long e = EventPoints.Slot(arr, i);
                if (Memory.ReadInt(e + EventPoints.Type) == EventPoints.TypeScript)
                    referenced.Add(Memory.ReadInt(e + EventPoints.ItemOrLabel));
            }
            referenced.Remove(FishingLabelId);     // our own point, from a prior install — not a town event

            var all = new System.Collections.Generic.List<(int id, int off, int slot)>();
            for (int i = 0; i < labelCount; i++)
            {
                long e = stb + tbl + i * TownScript.LabelStride;
                all.Add((Memory.ReadInt(e), Memory.ReadInt(e + 4), i));
            }
            all.Sort((a, b) => a.off.CompareTo(b.off));

            // Candidates come from the fixed SafeHijackLabels whitelist (offline-verified never dispatched in
            // ANY town). No runtime bytecode scan: the vanilla data can't change, and a scan that found
            // everything used would leave the spot uninstallable. The event-point set above is still consulted
            // as cheap insurance (a future area could put a point on one), though none does today.
            var sizes = new System.Text.StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                int size = i + 1 < all.Count ? all[i + 1].off - all[i].off : 0;   // 0 = last, unknown end
                bool safe = SafeHijackLabels.Contains(all[i].id);
                bool epRef = referenced.Contains(all[i].id);
                sizes.Append($"{all[i].id}:{(size > 0 ? size.ToString() : "end")}" +
                             $"{(safe ? "+" : "")}{(epRef ? "@" : "")} ");
                if (!safe || epRef || size <= 0) continue;   // + = safe hijack pool, @ = event-point (skip)
                _arena.Add(new Lab
                {
                    Slot = all[i].slot, Id = all[i].id, Off = all[i].off, Size = size,
                    Entry = (int)(tbl + all[i].slot * TownScript.LabelStride),
                });
            }
            Log($"   label regions (+ = safe hijack pool, @ = event-point protected): {sizes}");
        }

        /// <summary>Bytes a script needs: header skip + code + string blob + alignment slack.</summary>
        private static int Need(StbWriter w) => TownScript.LabelCodeSkip + w.ToArray().Length + w.StringBytes + 8;

        /// <summary>An id nothing will ever ask for, given to labels whose code we have overwritten.</summary>
        private const int RetiredLabelId = 9000;

        /// <summary>
        /// The id our fishing script answers to. Deliberately outside the range any town uses (the highest
        /// real label seen anywhere is 310), so the ONLY thing that can dispatch it is our own event point.
        /// </summary>
        private const int FishingLabelId = 400;

        /// <summary>Id for the shared menu-select subroutine's label. Nothing dispatches it as an event — it is
        /// only ever reached by CALL_FUNC (vanilla parks the same routine as an anonymous funcdata) — so this
        /// just needs to be an id no town uses and clear of the <see cref="RetiredLabelId"/> range.</summary>
        private const int MenuSubLabelId = 9600;

        /// <summary>
        /// Claim a run of adjacent unused labels totalling at least <paramref name="need"/> bytes, and return
        /// the FIRST one — its id is what the script will answer to.
        ///
        /// FEWEST LABELS FIRST. Every extra label a run swallows is a town event we destroy, so try to fit in
        /// one label before considering two, and so on. Taking the first run that merely fits would grab a
        /// 644+644 pair when a single 804 was sitting right there — and would then retire a label for nothing.
        ///
        /// Every label a run does swallow is marked used (so a later allocation cannot hand out the same
        /// bytes) and RETIRED (so the engine cannot dispatch into the middle of the script we write over it).
        /// </summary>
        private static Lab Allocate(long stb, int need, out int end)
        {
            for (int len = 1; len <= _arena.Count; len++)
            for (int i = 0; i + len <= _arena.Count; i++)
            {
                int total = 0;
                bool usable = true;
                for (int j = i; j < i + len; j++)
                {
                    if (_arena[j].Used ||
                        (j > i && _arena[j].Off != _arena[j - 1].Off + _arena[j - 1].Size))   // not adjacent
                    { usable = false; break; }
                    total += _arena[j].Size;
                }
                if (!usable || total < need) continue;

                {
                    int j = i + len - 1;
                    for (int k = i; k <= j; k++) _arena[k].Used = true;

                    // RETIRE THE SWALLOWED LABELS. A run's later labels keep their table entries, but we are
                    // about to write straight THROUGH their code — so their codeOffset would then point into
                    // the middle of our bytecode. If the town ever asks for one (an event that fires when you
                    // reach some part of the map, say), the VM reads our data as a funcdata, takes a garbage
                    // code offset from it, and jumps into nowhere. That is the crash-on-walking-away.
                    //
                    // Give them an id nothing will ever request. The engine then simply fails to find the
                    // label and treats it as a no-op event, which loses whatever that event did — but a lost
                    // town event beats a hard crash, and there is nowhere else to put a 1.5 KB script.
                    for (int k = i + 1; k <= j; k++)
                    {
                        Memory.WriteInt(stb + _arena[k].Entry, RetiredLabelId + k);
                        Log($"   label {_arena[k].Id} RETIRED (its code is inside our script now) — " +
                            $"the town can no longer dispatch into it");
                    }

                    end = _arena[i].Off + total;
                    return _arena[i];
                }
            }
            end = 0;
            return null;
        }

        /// <summary>
        /// The label to write <paramref name="targetId"/>'s script into. PREFERS the ISO-baked label that
        /// already carries this id (<see cref="IsoPatcher.ExtendStb"/> stamps 9600/400/133/134 straight into
        /// the three custom fishing towns): it is correctly numbered and sized to hold its one script, so we
        /// write into it directly — no renumber, no arena run, no spanning. FALLS BACK to renaming a native
        /// orphan for a town/ISO without the baked labels (an unpatched disc, or a spot added to a new town).
        /// </summary>
        private static Lab ClaimLabel(long stb, int labelCount, int tbl, int targetId, int need, out int end)
        {
            Lab baked = FindLabelById(stb, labelCount, tbl, targetId);
            if (baked != null) { end = baked.Off + baked.Size; return baked; }
            return Allocate(stb, need, out end);   // native-orphan fallback; caller renames it to targetId
        }

        /// <summary>Find the label whose id is <paramref name="id"/> and return its code region (its size is
        /// the gap to the next label by offset). Null if absent. Used to claim a pre-baked, pre-numbered
        /// fishing label directly, without the fit-allocator's size search.</summary>
        private static Lab FindLabelById(long stb, int labelCount, int tbl, int id)
        {
            int myOff = -1, mySlot = -1;
            var offs = new int[labelCount];
            for (int i = 0; i < labelCount; i++)
            {
                long e = stb + tbl + i * TownScript.LabelStride;
                offs[i] = Memory.ReadInt(e + 4);
                if (Memory.ReadInt(e) == id) { myOff = offs[i]; mySlot = i; }
            }
            if (mySlot < 0) return null;
            int next = int.MaxValue;
            for (int i = 0; i < labelCount; i++) if (offs[i] > myOff && offs[i] < next) next = offs[i];
            return new Lab
            {
                Id = id, Slot = mySlot, Off = myOff,
                Size = next == int.MaxValue ? 0 : next - myOff,
                Entry = tbl + mySlot * TownScript.LabelStride,
            };
        }

        /// <summary>
        /// Serialize a script at <paramref name="codeOff"/>, placing any strings it pushed just past its code.
        /// String operands are offsets from the script's CODE BASE, so the blob must live inside the buffer.
        /// </summary>
        private static void WriteScript(long stb, int codeOff, int end, StbWriter w, string what)
        {
            int codeBase = Memory.ReadInt(stb + TownScript.CodeBase);
            int scriptOff = codeOff + TownScript.LabelCodeSkip;

            byte[] bc = w.ToArray();
            int blobOff = (scriptOff + bc.Length + 3) & ~3;
            byte[] blob = w.EmitStrings(blobOff, codeBase);
            w.EmitJumps(scriptOff, codeBase);       // jump targets are codeBase-relative, like strings
            bc = w.ToArray();                       // re-read: both passes patched the operands in place

            int last = blobOff + blob.Length;
            if (last > end)
            {
                Log($"   REFUSING to write: needs +0x{codeOff:X}..+0x{last:X}, arena ends at +0x{end:X}");
                return;
            }

            // Declare our locals. A label's header starts with the LOCAL VARIABLE COUNT. The labels we hijack
            // declare 0, so a script that touches var0 without raising this would be reaching outside its
            // frame. (header layout + Norune's per-label counts: memory stb-label-header-format.md,
            // game_data/docs/fishing-engine-re.md §stb-label-header)
            if (w.Locals > 0) Memory.WriteInt(stb + codeOff + 8, w.Locals);
            // fd[3] (funcOff+0xC) = argument count. Native/baked spares carry 0 here, so only a genuine
            // subroutine (the shared menu) needs it — but a wrong non-zero value would misframe the callee.
            if (w.ArgCount > 0) Memory.WriteInt(stb + codeOff + 0xC, w.ArgCount);

            Memory.WriteBytesBatch(stb + codeOff + TownScript.LabelCodeSkip, bc);
            if (blob.Length > 0) Memory.WriteBytesBatch(stb + blobOff, blob);
            Log($"   wrote {bc.Length}B code + {blob.Length}B strings @+0x{blobOff:X}" +
                (w.Locals > 0 ? $", {w.Locals} local(s)" : "") + $": {what}");
        }

        /// <summary>
        /// Give the town a label the ENGINE asks for by number (133 = quit, 134 = bait). The id is not
        /// negotiable, so if the town has no such label we claim a spare and REWRITE ITS ID.
        /// </summary>
        private static void InstallEngineLabel(long stb, int labelCount, int tbl, int targetId, StbWriter w, string what)
        {
            Lab lab = ClaimLabel(stb, labelCount, tbl, targetId, Need(w), out int end);
            if (lab == null)
            {
                Log($"   NO room for label {targetId} — that fishing button will do nothing");
                return;
            }

            Memory.WriteInt(stb + lab.Entry, targetId);   // no-op for a baked label; renames a fallback orphan
            Log($"   label {targetId} (the engine requests it by number, code @+0x{lab.Off:X})");
            WriteScript(stb, lab.Off, end, w, what);
        }



        /// <summary>
        /// The script local that <c>_LOAD_SYNC</c> reports into, so the load loop waits exactly as long as the
        /// disc takes — no more, and crucially no less. Index 1, because the bait menu uses var0 for its
        /// result.
        /// </summary>
        private const int GateVar = 1;


        /// <summary>
        /// Build the bait's model and hang it on the hook.
        ///
        /// <c>_SET_FISHING_ESA</c> loads NOTHING — it only points the hook at item frame 0. The frame has to
        /// be built first:
        ///
        /// <code>
        ///   _LOAD_ITEM_FILE(id)        // async background read (LoadFileBG)
        ///   &lt;wait&gt;
        ///   _CLEAR_EVENT_BUFF()
        ///   _ACTIVE_FILE_BUFFER(0, 0)
        ///   _LOAD_ITEM(0)              // builds item frame 0; returns 0 if the read has not landed
        ///   YIELD
        ///   _SET_FISHING_ESA(id)
        /// </code>
        ///
        /// This has to be emitted into EVERY script that wants bait, not done once at startup: the engine
        /// zeroes the item-frame table at the start of every event, so by the time label 134 runs, whatever
        /// the entry script loaded is already gone. That is exactly why pressing Square removed the bait
        /// instead of adding it. (game_data/docs/fishing-engine-re.md §fishing-esa)
        /// </summary>
        /// <summary>
        /// <c>while (poll(&amp;v)) YIELD;</c> — wait on something the engine will finish in its own time.
        ///
        /// Both of the game's "are you done yet" commands have the same shape, reporting through a pointer
        /// argument because that is the ONLY way an EXT command can return anything (EXT pushes no result;
        /// <c>SetStack</c> demands a type-3 pointer arg):
        ///
        ///   <see cref="StbCommands.LoadSync"/>  (34)  — a background disc read is still in flight
        ///   <see cref="StbCommands.CheckFade"/> (502) — a fade is still running
        ///
        /// This is what Norune's opaque <c>call_func 400</c> was all along, once the funcdata format fell out:
        /// a four-instruction loop. Counting frames instead is a race — and losing the load one does not look
        /// wrong, it CRASHES (an item frame built from an empty buffer, then a call through a garbage
        /// pointer). See docs/stb-script-format.md.
        /// </summary>
        /// <param name="exitOnNonZero">
        /// WATCH THE POLARITY — the two poll commands report OPPOSITE senses:
        ///
        ///   _LOAD_SYNC  -> ReadBGSync()    : non-zero while a read is STILL PENDING  (exit on zero)
        ///   _CHECK_FADE -> fade_end        : non-zero once the fade has FINISHED     (exit on non-zero)
        ///
        /// They look identical at the call site, which is exactly how the fade wait ended up exiting on its
        /// first iteration: fade_end is 0 the moment EdFadeOut starts, so "loop while non-zero" waited zero
        /// frames and the model swap happened in plain view, before the screen had faded.
        /// </param>
        /// <summary>
        /// Reset the world coordinate to identity, so <c>_SET_NPC_POS</c> / <c>_SET_NPC_ROT</c> take plain
        /// WORLD coordinates. (Norune passes the pond part's transform instead, because its numbers are
        /// part-local; ours come out of the probe in world space.)
        ///
        /// Call it with NO ARGUMENTS. <c>_SET_WORLD_COORD</c>'s handler branches on the argument count, and
        /// the zero-arg path is exactly this reset — <c>sceVu0UnitMatrix</c> on both matrices. Pushing six
        /// zero floats does the same thing the long way round, for 6 extra instructions.
        /// </summary>
        private static void EmitWorldCoordReset(StbWriter w)
        {
            w.PushInt(StbCommands.SetWorldCoord);     // 7, with no args = "identity"
            w.Ext(1);
        }

        private static void EmitWaitLoop(StbWriter w, int pollCommand, bool exitOnNonZero)
        {
            w.UseLocals(GateVar + 1);

            int retry = w.Mark();
            w.PushInt(pollCommand);
            w.PushVarRef(GateVar);
            w.Ext(2);

            w.PushVar(GateVar);
            int done = w.MarkForward();
            if (exitOnNonZero) w.BrTrue(done);
            else w.BrFalse(done);
            w.Yield();
            w.Jmp(retry);
            w.PlaceMark(done);
        }

        /// <summary>
        /// Load the model for the bait in var0 and hang it on the hook. ONLY valid from label 134.
        ///
        /// This must not be emitted into the entry script. It calls <c>_CLEAR_EVENT_BUFF</c>, which rewinds
        /// the bump allocator that <c>_LOAD_FISHING_DATA</c> allocates from — running it after the fishing
        /// load drops the bait on top of fishing.pak and corrupts the arena. See BuildFishingBytecode.
        /// </summary>
        private static void EmitBaitLoad(StbWriter w)
        {
            w.UseLocals(GateVar + 1);

            void PushItem() => w.PushVar(0);        // var0 = whatever the menu chose

            w.PushInt(StbCommands.LoadItemFile);    // 49 — issues a BACKGROUND disc read and returns at once
            PushItem();
            w.Ext(2);

            // WAIT FOR THE READ, rather than betting on a frame count.
            //
            // _LOAD_ITEM builds an item frame from the read buffer, and if the data has not landed it builds
            // one out of nothing — the game then calls through a garbage pointer and dies ("Jump to unaligned
            // address"). A fixed YIELD spin is a race: 5 frames lost it, 10 might, and a slower disc surely
            // would. It was never a cosmetic knob.
            //
            //     while (_LOAD_SYNC(&v)) YIELD;
            //
            // _LOAD_SYNC (34) is the load poll — it pumps the reader and reports whether anything is still in
            // flight. This is EXACTLY what Norune's opaque `call_func 400` turned out to be, once the funcdata
            // format was cracked (see docs/stb-script-format.md).
            EmitWaitLoop(w, StbCommands.LoadSync, exitOnNonZero: false);   // busy while non-zero

            w.PushInt(StbCommands.ClearEventBuff);  // 39
            w.Ext(1);

            w.PushInt(StbCommands.ActiveFileBuffer);// 44
            w.PushInt(0);
            w.PushInt(0);
            w.Ext(3);

            w.PushInt(StbCommands.LoadItem);        // 50 — builds item frame 0
            w.PushInt(0);
            w.Ext(2);
            w.Yield();

            w.PushInt(StbCommands.SetFishingEsa);   // 994
            PushItem();
            w.Ext(2);
        }

        /// <summary>
        /// Label 134 — the REAL bait menu.
        ///
        /// <c>_GOTO_CHANGE_ESA</c> (command 25) drives the game's own use-item menu: it copies a built-in bait
        /// list (so we do not have to supply one) and opens the menu. Its one meaningful argument is a POINTER
        /// to a script local, which the menu writes the chosen item id into. Hence
        /// <see cref="StbWriter.PushVarRef"/>. (game_data/docs/fishing-engine-re.md §fishing-esa)
        ///
        /// The single YIELD after it is enough: while <c>menu_mode != 0</c>, <c>EdEventMode</c> runs the menu
        /// instead of stepping the script, so we resume only once the player has chosen.
        /// </summary>
        private static StbWriter BuildBaitBytecode()
        {
            var w = new StbWriter();
            w.UseLocals(1);                         // var0 = the chosen bait, written by the menu
            w.Yield();

            w.PushInt(StbCommands.GotoChangeEsa);   // 25
            w.PushVarRef(0);                        // out: var0 <- the item the player picked
            w.Ext(2);
            w.Yield();                              // the menu owns the frames; we wake when it closes

            // ONLY load a bait if the player actually PICKED one. The menu leaves var0 <= 0 on cancel (var0 is
            // a zeroed local, and the engine writes an id only on a real selection). Vanilla's label 134 guards
            // the load with exactly this `var0 > 0` test; without it, cancelling still ran the load and pointed
            // _SET_FISHING_ESA at item 0 — which reads back as the first esa-table entry (Evy) with no model on
            // the hook. The load itself waits on the disc rather than a frame count (crash-safe).
            w.PushVar(0); w.PushInt(0); w.Cmp(StbWriter.CmpGt);
            int cancelled = w.MarkForward();
            w.BrFalse(cancelled);                   // var0 <= 0 -> menu was cancelled, leave the esa untouched
                EmitBaitLoad(w);
            w.PlaceMark(cancelled);

            // GO BACK TO FISHING. Every fishing sub-script runs as a normal event, and when an event RETs,
            // EventMode switches on its return code — whose `default:` branch is `GameMode = 1`, i.e. WALKING.
            // So a script that ends without asking for something specific silently ENDS THE SESSION. That is
            // exactly how label 133 (quit) works, and pressing Square used to quit for the same reason.
            //
            // Asking for fishing again puts EventMode back through `case 0xb: GameMode = 0x10`.
            w.PushInt(StbCommands.GotoFishing);     // 997
            w.Ext(1);

            // NO _FADE_IN. Norune's label 134 has no fade of any kind — picking a bait returns you to fishing
            // instantly. The fade here was mine, copied from the entry script where it belongs.

            w.Ret();
            return w;
        }

        /// <summary>
        /// The STB VM is 12-byte instructions {op, a1, a2}. Push type 1 = int, 2 = float (IEEE bits).
        /// EXT (op 21) takes the STACK ENTRY COUNT in a1, including the command id, which is the first entry.
        ///
        /// Modelled directly on Norune's real _LOAD_FISHING_DATA call, which pushes 998 then the area and six
        /// floats and does EXT argc=8. We push negative floats as literals rather than using the negate op the
        /// original happens to use. (exact script offset: game_data/docs/fishing-engine-re.md §norune-script)
        /// </summary>
        /// <summary>Emit <c>var[idx] = &lt;value pushed by <paramref name="push"/>&gt;</c> as a statement
        /// (STORE re-pushes the stored value, so this drops it with POP).</summary>
        private static void SetVar(StbWriter w, int idx, System.Action push)
        {
            w.PushVarRef(idx); push(); w.Store(); w.Pop();
        }

        /// <summary>
        /// Draw event-mes <paramref name="msgId"/> (window 1 — our injected menu text) as an
        /// <paramref name="count"/>-line cursor menu and block until the player presses X, leaving the chosen
        /// 0-based line in local <paramref name="vSel"/>. The caller then branches on <paramref name="vSel"/>.
        ///
        /// This reproduces Norune's shared fishing-select cursor with the documented VM primitives: the LEFT
        /// ANALOG STICK (LY, ±0.5 threshold, with a "held" edge flag so one push moves one line) AND the d-pad,
        /// X to confirm. No CALL_FUNC and none of the vanilla subroutine's analog-acceleration bulk.
        /// Scratch locals <paramref name="vPad"/>/<paramref name="vLy"/>/<paramref name="vHeld"/>/
        /// <paramref name="vScratch"/> are clobbered.
        /// </summary>
        /// <summary>Inline form: emit the menu straight into the caller's frame, leaving the choice in
        /// <paramref name="vSel"/>. Used only as a fallback when no shared subroutine could be allocated.</summary>
        private static void EmitSelectMenu(StbWriter w, int msgId, int count,
                                           int vSel, int vPad, int vLy, int vHeld, int vScratch)
            => EmitMenuBody(w, () => w.PushInt(msgId), () => w.PushInt(count - 1),
                            vSel, vPad, vLy, vHeld, vScratch);

        /// <summary>
        /// The menu cursor loop, shared by the inline form (<see cref="EmitSelectMenu"/>) and the CALL_FUNC
        /// subroutine (<see cref="BuildMenuSubroutine"/>). msgId and (count-1) are supplied via delegates so
        /// the SAME bytecode serves both a literal-const inline copy and a subroutine reading them from its
        /// arg locals — one implementation to maintain, exactly as vanilla shares its 0x4bd4/0x4264 pair.
        /// </summary>
        private static void EmitMenuBody(StbWriter w, System.Action pushMsgId, System.Action pushCountMinus1,
                                         int vSel, int vPad, int vLy, int vHeld, int vScratch)
        {
            const int Win = 1;
            // Locals is a COUNT (indices 0..count-1), so reserve highest-index + 1 — see WriteScript.
            int maxIdx = System.Math.Max(System.Math.Max(vSel, vPad), System.Math.Max(System.Math.Max(vLy, vHeld), vScratch));
            w.UseLocals(maxIdx + 1);

            // Matches Norune's menu-show + its caller (label 11): tail off, draw the message, position it, and
            // AUTO-PLACE the bubble (this is what was missing — without _SET_MES_AUTOSET the bubble sat top-left).
            w.PushInt(StbCommands.SetMesShippo);    w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.SetMesDrawSpeed); w.PushInt(Win); w.PushFloat(0.0f); w.Ext(3); // 0 = instant (per-char delay); vanilla's select-loop value
            w.PushInt(StbCommands.MesMake);       w.PushInt(Win); pushMsgId(); w.Ext(3);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(9); w.Ext(3);
            w.PushInt(StbCommands.SetMesAutoset); w.PushInt(Win); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.Ext(6);
            SetVar(w, vSel,  () => w.PushInt(0));
            SetVar(w, vHeld, () => w.PushInt(0));

            int loop        = w.Mark();
            int afterAnalog = w.MarkForward();

            w.PushInt(StbCommands.SetMesCursor); w.PushInt(Win); w.PushVar(vSel); w.Ext(3);
            w.Yield();
            w.PushInt(StbCommands.GetApad);   w.PushVarRef(vScratch); w.PushVarRef(vLy); w.Ext(3); // vLy = LY
            w.PushInt(StbCommands.GetPadDown); w.PushVarRef(vPad); w.Ext(2);

            // Analog, edge-detected: LY > 0.5 = down, LY < -0.5 = up; move only on the neutral->pushed edge.
            // ±0.5 is vanilla's exact threshold (dir() subroutine, symmetric, no hysteresis). A wider deadzone
            // would make double-taps re-arm HARDER, not easier — the dip between taps must reach |LY| <= 0.5.
            int notADown = w.MarkForward();
            w.PushVar(vLy); w.PushFloat(0.5f); w.Cmp(StbWriter.CmpGt); w.BrFalse(notADown);
                int adHeld = w.MarkForward();
                w.PushVar(vHeld); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(adHeld);   // held -> only set flag
                    int adSkip = w.MarkForward();
                    w.PushVar(vSel); pushCountMinus1(); w.Cmp(StbWriter.CmpLt); w.BrFalse(adSkip);
                        SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Add(); });
                    w.PlaceMark(adSkip);
                w.PlaceMark(adHeld);
                SetVar(w, vHeld, () => w.PushInt(1));
                w.Jmp(afterAnalog);
            w.PlaceMark(notADown);
            int notAUp = w.MarkForward();
            w.PushVar(vLy); w.PushFloat(-0.5f); w.Cmp(StbWriter.CmpLt); w.BrFalse(notAUp);
                int auHeld = w.MarkForward();
                w.PushVar(vHeld); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(auHeld);
                    int auSkip = w.MarkForward();
                    w.PushVar(vSel); w.PushInt(0); w.Cmp(StbWriter.CmpGt); w.BrFalse(auSkip);
                        SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Sub(); });
                    w.PlaceMark(auSkip);
                w.PlaceMark(auHeld);
                SetVar(w, vHeld, () => w.PushInt(1));
                w.Jmp(afterAnalog);
            w.PlaceMark(notAUp);
            SetVar(w, vHeld, () => w.PushInt(0));   // stick neutral -> re-arm the edge
            w.PlaceMark(afterAnalog);

            // D-pad (already edge events from _GET_PADDOWN): DOWN then UP.
            int notDDown = w.MarkForward();
            w.PushVar(vPad); w.PushInt(StbCommands.PadDown); w.And(); w.BrFalse(notDDown);
                int ddSkip = w.MarkForward();
                w.PushVar(vSel); pushCountMinus1(); w.Cmp(StbWriter.CmpLt); w.BrFalse(ddSkip);
                    SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Add(); });
                w.PlaceMark(ddSkip);
            w.PlaceMark(notDDown);
            int notDUp = w.MarkForward();
            w.PushVar(vPad); w.PushInt(StbCommands.PadUp); w.And(); w.BrFalse(notDUp);
                int duSkip = w.MarkForward();
                w.PushVar(vSel); w.PushInt(0); w.Cmp(StbWriter.CmpGt); w.BrFalse(duSkip);
                    SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Sub(); });
                w.PlaceMark(duSkip);
            w.PlaceMark(notDUp);

            // X confirms the highlighted line; otherwise loop for another frame.
            w.PushVar(vPad); w.PushInt(StbCommands.PadCross); w.And(); w.BrFalse(loop);
            // Turn the selection cursor OFF: line -1 makes DrawMesWin draw no pointer and no highlight (it gates
            // the cursor on line >= 0). Vanilla's select-loop does exactly this on exit — it's why the no-pole
            // line that follows renders as a plain dialog bubble instead of a menu.
            w.PushInt(StbCommands.SetMesCursor); w.PushInt(Win); w.PushInt(-1); w.Ext(3);
            // Restore pos + tail as vanilla's menu-show/label 11 do. Do NOT _MES_CLOSE here: vanilla leaves the
            // window open so the dispatch path reuses it — the mode change (fishing/quit) clears it, and the
            // no-pole path's _MES_MAKE(21) replaces it in-place. Closing here left msg 21 built-but-not-shown.
            w.PushInt(StbCommands.SetMesPos);    w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.SetMesShippo); w.PushInt(Win); w.PushInt(1); w.Ext(3);
        }

        // The shared menu subroutine's frame: locals 0,1 are the arguments (msgId, count); 2..6 are its
        // scratch. Matches vanilla 0x4bd4 (args in the low locals, cursor state above).
        private const int MenuMsgArg = 0, MenuCountArg = 1;
        private const int MenuSel = 2, MenuPad = 3, MenuLy = 4, MenuHeld = 5, MenuScratch = 6;

        /// <summary>
        /// Build the shared menu-select subroutine — the CALL_FUNC target that both the entry menu and the quit
        /// confirm invoke, exactly as Norune's label 11 and 133 both call its 0x4bd4. Takes (msgId, count) as
        /// stack args and returns the chosen 0-based line ON THE STACK (push then RET), which the caller STOREs.
        /// </summary>
        private static StbWriter BuildMenuSubroutine()
        {
            var w = new StbWriter();
            w.SetArgs(2);                                        // var0 = msgId, var1 = count
            EmitMenuBody(w, () => w.PushVar(MenuMsgArg),
                            () => { w.PushVar(MenuCountArg); w.PushInt(1); w.Sub(); },
                            MenuSel, MenuPad, MenuLy, MenuHeld, MenuScratch);
            w.PushVar(MenuSel);                                  // vanilla returns v6 the same way: push, then RET
            w.Ret();
            return w;
        }

        /// <summary>
        /// Invoke the shared menu subroutine and leave the choice in <paramref name="destVar"/>. This is
        /// vanilla's call shape verbatim (label 11 / 133): push the destination REF first so it sits below the
        /// args, push the args, CALL_FUNC, then STORE (which writes *destVar and re-pushes the value) and POP.
        /// </summary>
        private static void EmitMenuCall(StbWriter w, int msgId, int count, int destVar, int menuCbRel)
        {
            w.UseLocals(destVar + 1);
            w.PushVarRef(destVar);        // ref stays at the bottom of the stack for the trailing STORE
            w.PushInt(msgId);             // arg0 -> callee var0
            w.PushInt(count);             // arg1 -> callee var1
            w.CallFunc(menuCbRel);        // returns the chosen line on the stack
            w.Store();                    // *destVar = choice
            w.Pop();
        }

        /// <summary>Emit the entry/quit menu: the shared CALL_FUNC subroutine when one was allocated
        /// (<paramref name="menuCbRel"/> &gt;= 0), else an inline copy as a fallback. Either way the choice
        /// lands in <paramref name="destVar"/> for the caller to branch on.</summary>
        private static void EmitMenu(StbWriter w, int msgId, int count, int destVar, int menuCbRel,
                                     int vPad, int vLy, int vHeld, int vScratch)
        {
            if (menuCbRel >= 0) EmitMenuCall(w, msgId, count, destVar, menuCbRel);
            else EmitSelectMenu(w, msgId, count, destVar, vPad, vLy, vHeld, vScratch);
        }

        /// <summary>Show event-mes <paramref name="msgId"/> in window 1 (no cursor), wait for X, then close.
        /// This is Norune's no-pole line: window 1, pos 8 (anchors it talk-box style). Two things are essential
        /// here. (1) The caller must NOT have closed the menu window first — the show-flag (ClsMes+0x94) is set
        /// to 1 only at event start, and _MES_MAKE's rebuild path never re-raises it, so a preceding _MES_CLOSE
        /// would leave this built-but-invisible. (2) We WAIT for X: window 1 is only drawn in event mode, so
        /// without staying in the event the line renders for a single frame and vanishes as the event ends.</summary>
        private static void EmitShowMessage(StbWriter w, int msgId, int padVar)
        {
            const int Win = 1;
            w.UseLocals(padVar + 1);    // self-declare: the inline menu no longer reserves this var for us
            // Tail OFF (flag 0) for the whole display: DrawMesWin_sub draws the shippo tail whenever
            // ClsMes+0x58 != 0, and its anchor sits mid-screen — turning it on here is what put the stray
            // triangle above the player. Vanilla also shows this line with the tail off.
            w.PushInt(StbCommands.SetMesShippo);  w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.MesMake);       w.PushInt(Win); w.PushInt(msgId); w.Ext(3);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(8); w.Ext(3);
            w.PushInt(StbCommands.SetMesAutoset); w.PushInt(Win); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.Ext(6);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(0); w.Ext(3);
            int loop = w.Mark();
            w.Yield();
            w.PushInt(StbCommands.GetPadDown); w.PushVarRef(padVar); w.Ext(2);
            w.PushVar(padVar); w.PushInt(StbCommands.PadCross); w.And(); w.BrFalse(loop);
            EmitCloseMenu(w);
        }

        /// <summary>Hide window 1. The menu leaves the bubble SHOWN after a selection (so the no-pole path can
        /// reuse it); every other dispatch path calls this before its transition so the bubble doesn't linger
        /// and render its bare frame+tail during the fade. Safe across opens: the show-flag is re-raised at the
        /// next event start.</summary>
        private static void EmitCloseMenu(StbWriter w)
        {
            w.PushInt(StbCommands.MesClose); w.PushInt(1); w.Ext(2);
        }

        private static StbWriter BuildFishingBytecode(Spot s, int menuCbRel)
        {
            var w = new StbWriter();

            // ── PROMPT, DON'T POUNCE ────────────────────────────────────────────────────────────────────
            //
            // A type-3 event point fires its label the moment you are in range — EdMoveChara has no button
            // check for it (only item and ladder points test PadDown). So the "!" prompt and the X press have
            // to come from the SCRIPT, which is exactly what Norune's enormous label 256 is doing.
            //
            // The mechanism is the same rule that cost us three test cycles at the start, used deliberately
            // this time: a script that RETURNS WITHOUT YIELDING is a "simple event". EdEventInit runs it,
            // sees it finish, and never enters event mode — so the player keeps walking around. That means
            // this script can run EVERY FRAME while you stand near the spot, cheaply, drawing the prompt and
            // watching the pad, and only commit — i.e. yield — once you actually press X.
            //
            // It also makes the whole thing repeatable for free: no disarm/re-arm bookkeeping, no leaked
            // fish. Walk away, come back, press X again.
            w.UseLocals(2);                           // var0 = pad bits, var1 = the wait-loop gate

            w.PushInt(StbCommands.DrawExclamationMark);   // 10 — per-frame; re-asserted on every pass
            w.Ext(1);

            w.PushInt(StbCommands.GetPadDown);        // 1
            w.PushVarRef(0);                          // out: var0 = buttons pressed this frame
            w.Ext(2);

            w.PushVar(0);
            w.PushInt(StbCommands.PadCross);
            w.And();
            int idle = w.MarkForward();
            w.BrFalse(idle);                          // no X -> fall out WITHOUT yielding: a simple event

            // Everything past here yields, so pressing X promotes this into a real event — and only then.

            // Snap the player out of whatever animation they were in (mid-walk, most often) to idle the
            // instant the menu commits. Without this the last motion FREEZES on screen for the whole menu —
            // you press X while walking and stand there mid-stride. Norune's label 11 does exactly this here:
            // _SET_NPC_MOTION(charaId -1 = player, motion 0 = idle), before it opens the menu.
            w.PushInt(StbCommands.SetNpcMotion);      // 133
            w.PushInt(-1);                            // charaId -1 = the player
            w.PushInt(0);                             // motion 0 = idle/stand
            w.Ext(3);

            // ── VANILLA ENTRY MENU (Fish / Exchange FP / Fishing log / Quit) ────────────────────────────
            // Norune shows this before fishing starts (its label 11). We reproduce it. But first WAIT for the
            // mod to swap window 1 to our injected menu text: the swap happens on CustomFishingSpot.Tick, which
            // runs on the mod's 50 ms loop (Thread.Sleep(50) ≈ 3 game frames) — NOT per frame. If the menu's
            // _MES_MAKE runs before that tick lands, it builds a garbage-sized texture off the town's own event
            // mes (which lacks msg 20) — the "heavily distorted" bubble. 8 yields (~133 ms) clears a full tick
            // interval with margin, so the swap is reliably done first.
            for (int i = 0; i < 8; i++) w.Yield();
            EmitMenu(w, 20, 4, /*sel*/2, menuCbRel, /*pad*/3, /*ly*/4, /*held*/5, /*scratch*/6);

            // Exchange FP (1) / Fishing log (2) each open their own engine menu then return; Quit (3) returns.
            // Only "Fish" (0) falls through to the session setup below. (Norune's label 11 returns after FP/log
            // exactly like this — the engine sub-menu runs on menu_mode after the event ends.)
            int doFish = w.MarkForward();
            w.PushVar(2); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrTrue(doFish);
                int notFp = w.MarkForward();
                w.PushVar(2); w.PushInt(1); w.Cmp(StbWriter.CmpEq); w.BrFalse(notFp);
                    EmitCloseMenu(w);
                    w.PushInt(StbCommands.GotoFpChange); w.Ext(1); w.Yield();
                    w.Ret();
                w.PlaceMark(notFp);
                int notLog = w.MarkForward();
                w.PushVar(2); w.PushInt(2); w.Cmp(StbWriter.CmpEq); w.BrFalse(notLog);
                    EmitCloseMenu(w);
                    w.PushInt(StbCommands.GotoFishRanking); w.Ext(1); w.Yield();
                    w.Ret();
                w.PlaceMark(notLog);
                EmitCloseMenu(w);
                w.Ret();                              // Quit (3) or anything unexpected: back to walking
            w.PlaceMark(doFish);

            // "Fish": require the fishing rod (item 185). EdCheckItem returns -1 when it isn't owned; Norune's
            // label 11 shows the no-pole line (msg 21) and returns in that case, else starts fishing.
            w.PushInt(StbCommands.SItemCheck); w.PushInt(StbCommands.FishingRodItem); w.PushVarRef(2); w.Ext(3);
            int haveRod = w.MarkForward();
            w.PushVar(2); w.PushInt(0); w.Cmp(StbWriter.CmpLt); w.BrFalse(haveRod);   // v2 >= 0 -> owned -> fish
                EmitShowMessage(w, 21, /*padVar*/3);   // no-pole line, wait for X, then close (window still open here)
                w.Ret();
            w.PlaceMark(haveRod);
            EmitCloseMenu(w);   // rod in hand: hide the menu bubble before the fade so it doesn't garble

            // Rod in hand: fall through to the fade + model swap + fishing-data load.

            // FADE TO BLACK BEFORE TOUCHING THE MODEL, and hide the player while we do it. Norune:
            //
            //     _FADE_OUT(30) ; <wait> ; _CLEAR_VILLAGER_BUFF() ; _NPC_DRAW(0, -1) ; _LOAD_MAIN_CHARA(...)
            //
            // We were swapping the model in plain sight, which is why the player visibly vanished and then
            // faded back in wearing the fishing model. The swap is supposed to happen behind black.
            w.PushInt(StbCommands.FadeOut);           // 501 — FADE_OUT (500 is FADE_IN)
            w.PushInt(30);
            w.Ext(2);
            EmitWaitLoop(w, StbCommands.CheckFade, exitOnNonZero: true);   // done once fade_end is set

            // CLEAR THE TOWN'S NPCs for the session. This is HALF of a pair, and it only works with the other
            // half — _LOAD_VILLAGER on exit (see BuildExitBytecode).
            //
            // It rewinds the villager buffer, so the townspeople vanish while you fish (which is what vanilla
            // does — the town is empty during a session) and their memory is free for the 1.8 MB fishing
            // model. On its own it CRASHES: an earlier build called this and never reloaded, so after the
            // session the engine kept iterating villager slots whose memory had become part of a fishing rod,
            // and walking to where one stood killed the game. Reloading them on exit is what makes it safe.
            //
            // Clearing here rather than not-clearing also fixes the OTHER symptom: with villagers still loaded
            // through a session, an open town (Brownboo) shows them — and one renders garbled, because the
            // texture manager reuses a block the model/bait overwrote. Gone for the session, gone the glitch.
            w.PushInt(StbCommands.ClearVillagerBuff); // 38 — paired with _LOAD_VILLAGER on exit
            w.Ext(1);

            // RESET THE FISHING POOL TO ITS BASE *BEFORE* THE MODEL LOAD. This is the Brownboo fix.
            //
            // _LOAD_MAIN_CHARA(turi, flag=1) loads the 1.8 MB model into the same pool _LOAD_FISHING_DATA
            // uses (see §fishing-load). Norune loads the model FIRST and clears the event buffer AFTER, which
            // works only because its pool pointer already sits low. Brownboo has more resident event data, so
            // the pointer is high, and model-start + 1.8 MB runs off the end of the pool -> overflow -> crash
            // (confirmed: skipping the model reaches fishing fine, cpoly=4). Resetting the pool to base first
            // makes the model load from the bottom, so everything packs tight from the base and fits.
            w.PushInt(StbCommands.ClearEventBuff);    // 39 — moved up from after the model swap
            w.Ext(1);

            // We do NOT mirror Norune's _NPC_DRAW(0,-1) here (nor _NPC_DRAW(1,-1) on exit), and that is not a
            // shortcut — for the player it is a NO-OP. Its per-character draw flags are a villager-only array;
            // the player (id -1) bypasses it, and its only other write has zero readers in the binary. So
            // Norune's hide/show around the model swap is vestigial. (game_data/docs/fishing-engine-re.md §npc-draw)

            // SWAP TOAN FOR THE FISHING TOAN.
            //     _LOAD_MAIN_CHARA("chara/c01d_turi.chr", "c01d_turi.cfg", 1)
            // The ordinary c01d has no `sao` (rod) frame for _GOTO_FISHING to hang the line from, and none of
            // the fishing motions — so the cast animation index lands on whatever else is at that slot in
            // c01d's motion table, which is why it played the atla-opening motion.
            if (!s.DiagSkipModel)
            {
                w.PushInt(StbCommands.LoadMainChara);     // 999 — into the pool we just reset to base
                w.PushString(FishingModel);
                w.PushString(FishingModelCfg);
                w.PushInt(1);                             // flag 1 = load into the fishing pool
                w.Ext(4);
                w.Yield();
            }

            w.PushInt(StbCommands.LoadFishingData);   // 998 — NOT 999; the dispatch table is {handler, id}
            w.PushInt(s.AreaId);
            w.PushFloat(s.X1);
            w.PushFloat(s.Z1);
            w.PushFloat(s.X2);
            w.PushFloat(s.Z2);
            // Queens: the canal water level follows the day/night clock (CanalTide); everywhere else it is the
            // spot's fixed height. Fish seed at WaterLevel-depth and the bobber rides it, so this shifts the
            // whole session up/down with the tide.
            float water = s.MapNo == CanalTide.QueensMapNo ? CanalTide.QueensWaterLevel() : s.Water;
            w.PushFloat(water);
            w.PushFloat(s.Ground);
            w.Ext(8);                                 // 1 command id + 7 arguments

            // _INIT_FISH places the fish, at the centre of the rect it is GIVEN, at WaterLevel-12 (see the
            // fish-depth patch, which changes the 12). This rect is the FISH bounds (fish_rect), distinct
            // from the cast bounds (_LOAD_FISHING_DATA's rect). Give it the smaller water rect when the spot
            // has one, so fish stay in the water instead of wandering the whole cast area / through banks.
            float fx1 = s.HasFishRect ? s.FishX1 : s.X1;
            float fz1 = s.HasFishRect ? s.FishZ1 : s.Z1;
            float fx2 = s.HasFishRect ? s.FishX2 : s.X2;
            float fz2 = s.HasFishRect ? s.FishZ2 : s.Z2;
            w.PushInt(StbCommands.InitFish);          // 996
            w.PushFloat(fx1);
            w.PushFloat(fz1);
            w.PushFloat(fx2);
            w.PushFloat(fz2);
            w.Ext(5);                                 // 1 command id + 4 arguments

            // Snap the player into the fishing stance. Norune does exactly this — _SET_WORLD_COORD, then
            // _SET_NPC_POS / _SET_NPC_ROT at its own fishing point. Without it the player keeps whatever
            // position and facing they walked in with, so the cast is aimed at dry land and the engine
            // rejects it. This is the rod "bug". (Norune's exact coords: game_data/docs/fishing-engine-re.md §norune-script)
            //
            // _SET_WORLD_COORD is set to IDENTITY so that the position and rotation below are plain world
            // coordinates. Norune passes the pond part's transform instead, because its numbers are
            // part-local; ours come straight out of the probe in world space.
            if (s.HasStance)
            {
                EmitWorldCoordReset(w);

                w.PushInt(StbCommands.SetNpcPos);
                w.PushInt(-1);                        // -1 = the player
                w.PushFloat(s.StandX); w.PushFloat(s.StandY); w.PushFloat(s.StandZ);
                w.Ext(5);

                w.PushInt(StbCommands.SetNpcRot);
                w.PushInt(-1);
                w.PushFloat(0f); w.PushFloat(s.Facing); w.PushFloat(0f);
                w.Ext(5);
            }

            // NO BAIT LOAD HERE — and that is deliberate. Norune loads bait ONLY from label 134.
            //
            // Loading it here corrupts the heap. _CLEAR_EVENT_BUFF (which the item load needs) RESETS the
            // bump allocator that _LOAD_FISHING_DATA just allocated fishing.pak and the fish out of. So doing
            // the item load after the fishing load rewinds the arena and drops the bait on top of the fishing
            // data. The session still runs, because that memory is already in hand; but the arena is wrecked,
            // and the next thing to allocate from it — area streaming, once you walk far enough — lands in the
            // wreckage.
            //
            // That was the crash-after-fishing-when-you-walk-away. Vanilla starts a session with no bait and
            // you pick one with Square, so this is also the faithful behaviour, not just the safe one.
            // (allocator + fields: game_data/docs/fishing-engine-re.md §fishing-load)

            w.PushInt(StbCommands.GotoFishing);       // 997 — sets the event return code to 0xB
            w.Ext(1);                                 // command id only; matches Norune's `push 997; EXT argc=1`

            // Norune ends with _FADE_IN(60) — command 500 is FADE_*IN*, not fade-out (the mod's old command
            // table had the ids shifted by one and said otherwise). Without it the screen never fades back,
            // which is the missing transition.
            w.PushInt(StbCommands.FadeIn);
            w.PushInt(60);
            w.Ext(2);

            // The no-X path lands here too, having yielded nothing — so on an ordinary frame the whole script
            // is: draw the "!", read the pad, return. Cheap enough to run every frame you stand there.
            w.PlaceMark(idle);
            w.Ret();
            return w;
        }

        /// <summary>
        /// Label 133 — the engine's hardcoded "leave fishing" script.
        ///
        /// In fishing mode the engine asks for script labels BY NUMBER when you press a button — Cross casts,
        /// Circle requests label 133 (quit), Square requests 134 (bait menu).
        ///
        /// Norune's script HAS labels 133 and 134. A town that never had fishing does not — so the button
        /// asks for a label that does not exist and nothing happens, which is exactly why the session could
        /// not be exited. We synthesise 133 ourselves; it is tiny.
        /// (button -> label map: game_data/docs/fishing-engine-re.md §fishing-buttons)
        ///
        /// The RET matters: we set no return code, so <c>EventMode</c> takes its <c>default:</c> branch and
        /// puts <c>GameMode</c> back to 1 (walking). That is how Norune's exit path ends too.
        /// </summary>
        private static StbWriter BuildExitBytecode(Spot s, int menuCbRel)
        {
            var w = new StbWriter();
            w.Yield();                                // same rule as the main script — see BuildFishingBytecode
            w.Yield();                                // Norune yields twice here before touching the model

            // Snap the player to idle before the confirm menu, exactly as Norune's 133 opens with
            // _SET_NPC_MOTION(-1, 0) (and as our entry script does). Without it the player holds the fishing
            // pose frozen behind the "Quit fishing?" menu; on Continue the session resumes the fishing motion.
            w.PushInt(StbCommands.SetNpcMotion);      // 133
            w.PushInt(-1);                            // charaId -1 = the player
            w.PushInt(0);                             // motion 0 = idle/stand
            w.Ext(3);

            // ── VANILLA QUIT MENU (Continue fishing / Quit fishing) ─────────────────────────────────────
            // Circle during fishing asks the engine for label 133 (this script). Norune's 133 shows a 2-option
            // confirm before actually leaving. The session is already open, so window 1 already holds our menu
            // text (msg 22). Choice lands in var8 (high range, clear of the position locals below).
            EmitMenu(w, 22, 2, /*sel*/8, menuCbRel, /*pad*/9, /*ly*/10, /*held*/11, /*scratch*/12);
            int doQuit = w.MarkForward();
            w.PushVar(8); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(doQuit);   // sel != 0 (Quit) -> leave
                // Continue (0): keep the session running. _SET_RETURN_CODE(11) is exactly what Norune's 133
                // does to fall back into fishing instead of exiting.
                EmitCloseMenu(w);
                w.PushInt(StbCommands.SetReturnCode); w.PushInt(11); w.Ext(2);
                w.Yield();
                w.Ret();
            w.PlaceMark(doQuit);
            EmitCloseMenu(w);   // Quit: hide the menu bubble before the fade-out below
            // Quit: fall through to the fade-out + model restore + _EXIT_FISHING below.

            // FADE OUT OURSELVES before the model swap, don't rely on the fishing quit having done it. When
            // you quit while standing IN the trigger radius, the enter event point (in range) re-fires and
            // pre-empts the quit's fade-out — so the screen was still showing when the exit's fade-in ran,
            // i.e. no visible fade at all. Doing our own fade-out makes it consistent regardless: from an
            // already-black screen this is a harmless no-op (stays black), from a visible one it fades out
            // properly, and the model swap + villager reload then happen behind black as intended.
            w.PushInt(StbCommands.FadeOut);           // 501
            w.PushInt(30);
            w.Ext(2);
            EmitWaitLoop(w, StbCommands.CheckFade, exitOnNonZero: true);

            // RESTORE THE ORDINARY TOAN AND PUT THE PLAYER BACK WHERE THEY ARE — exactly as Norune's exit does.
            //
            // _LOAD_MAIN_CHARA resets the model's position, so the exit MUST re-place the player or you come out
            // of fishing falling through the map. And you can walk around while fishing (until you cast), so the
            // re-place has to land wherever you actually are, not at the entry stance.
            //
            // Norune's 256 exit block does this without any per-frame help: _GET_NPC_POS / _GET_NPC_ROT read the
            // live position into locals BEFORE the model swap, then _SET_NPC_POS / _SET_NPC_ROT write them back
            // AFTER. (Skipped when the entry skipped the swap — there is nothing to undo, and nothing reset the
            // position.)
            //
            // The position/rotation locals MUST be pushed float-safe (PushVarFloat / PushVarRefFloat, a2 = 8):
            // that stamps each stack entry's type tag so _SET_NPC_POS's GetStackFloat REINTERPRETS the float
            // bits instead of doing (float)(int)bits. With the plain int-mode push, a coord like 0x4309A6C0 is
            // read as (float)1124760000 ≈ 1.12e9 and the player (and camera) is flung off the map — a black
            // screen on quit. See PushVarFloat.
            //
            // BOTH GET and SET convert through the GLOBAL world-coord matrix (GetLocalPos in, GetWorldPos out).
            // The reset forces it to identity so the round-trip is exact in raw world space regardless of the
            // fishing setup or the player's per-CFrame flag; re-asserted after the swap so the villager reload
            // downstream also runs against a clean matrix.
            if (!s.DiagSkipModel)
            {
                // v2..v4 = position, v5..v7 = rotation. Deliberately NOT v0..v5: the wait loops below use
                // GateVar (var1) and the confirm menu uses var8, and the float-mode push STAMPS a var's type
                // tag — stamping the wait-loop's gate corrupts its truthiness check and hangs the exit after
                // _EXIT_FISHING. Keep our floats clear of both reserved locals.
                w.UseLocals(8);
                EmitWorldCoordReset(w);                   // identity: GET/SET both operate in world space

                w.PushInt(StbCommands.GetNpcPos);         // 131
                w.PushInt(-1);
                w.PushVarRefFloat(2); w.PushVarRefFloat(3); w.PushVarRefFloat(4);
                w.Ext(5);

                w.PushInt(StbCommands.GetNpcRot);         // 139
                w.PushInt(-1);
                w.PushVarRefFloat(5); w.PushVarRefFloat(6); w.PushVarRefFloat(7);
                w.Ext(5);

                w.PushInt(StbCommands.LoadMainChara);     // 999
                w.PushString(NormalModel);
                w.PushString(NormalModelCfg);
                w.PushInt(0);
                w.Ext(4);

                EmitWorldCoordReset(w);                   // re-assert identity AFTER the swap: the model load can
                                                          // leave a non-identity matrix, which would both mis-place
                                                          // the SET below and corrupt the villager reload downstream

                w.PushInt(StbCommands.SetNpcPos);         // 137
                w.PushInt(-1);
                w.PushVarFloat(2); w.PushVarFloat(3); w.PushVarFloat(4);
                w.Ext(5);

                w.PushInt(StbCommands.SetNpcRot);         // 138
                w.PushInt(-1);
                w.PushVarFloat(5); w.PushVarFloat(6); w.PushVarFloat(7);
                w.Ext(5);
            }

            // AFTER the model swap, not before — Norune's order.
            w.PushInt(StbCommands.ExitFishing);       // 995
            w.Ext(1);

            // RELOAD THE TOWN'S NPCs. This is the fix for the one garbled villager (e.g. Limbo).
            //
            // Fishing loads the 1.8 MB fishing Toan and the bait, and the texture manager reuses blocks — so
            // one villager's texture block gets overwritten during a session. We restore the player model on
            // the way out but never the villagers, so that block stays wrong until the area reloads (which is
            // why walking into a building "fixes" it). _LOAD_VILLAGER rewinds the villager buffer and reloads
            // every NPC and its textures from disc — exactly what Norune's exit does through its own script
            // functions (which we never ported). It takes no arguments; it reads the current map's villager
            // list from globals.
            //
            // After _EXIT_FISHING, so the fishing data is torn down first; behind the fade, so the reload is
            // invisible; and followed by a load-wait, so the town is whole before the screen comes back.
            w.PushInt(StbCommands.LoadVillager);      // 57
            w.Ext(1);
            EmitWaitLoop(w, StbCommands.LoadSync, exitOnNonZero: false);

            w.PushInt(StbCommands.FadeIn);            // 500(60), as Norune's own exit block does
            w.PushInt(60);
            w.Ext(2);

            w.Ret();
            return w;
        }

        /// <summary>
        /// Claim a free slot and make it a type-3 trigger for <paramref name="labelId"/>.
        ///
        /// The first attempt wrote only type / label / position / box, and the point was SILENTLY REJECTED:
        /// the engine's point check bails unless the Enabled field is non-zero. That is why nothing happened
        /// in Queens or Brownboo even though the install logged success.
        ///
        /// Rather than reverse-engineer the remaining fields (there is a time window and more besides), CLONE
        /// a working point and override only what we mean to change. Copying a known-good record is more
        /// robust than reconstructing one from a partial map.
        /// </summary>
        // The event array physically holds 256 points; the live count is mirrored to EventPoints.Count each
        // frame. When the town's own points fill the front of the array with no gap (a cold save-load — Yellow
        // Drops packs 12 with none free), we APPEND at index=count and bump the count, exactly as the engine's
        // own loader does. (array base / count field offsets: game_data/docs/fishing-engine-re.md §event-point-record)
        private const int MaxEventPoints = 0x100;
        private const long EventCountFromBase = -0x9010;   // count field, relative to the event-array base

        private static bool TryCreateEventPoint(Spot s, int labelId, out int slot)
        {
            long arr = EventPoints.Base();
            int n = Memory.ReadInt(EventPoints.Count);
            if (arr == 0 || n <= 0 || n > MaxEventPoints) { slot = -1; return false; }

            // A live point to copy the unknown fields from + the first reusable free slot (index 0 reserved).
            long donor = 0;
            int freeIdx = -1;
            for (int i = 0; i < n; i++)
            {
                int ty = Memory.ReadInt(EventPoints.Slot(arr, i) + EventPoints.Type);
                if (ty != EventPoints.TypeFree) { if (donor == 0) donor = EventPoints.Slot(arr, i); }
                else if (i >= 1 && freeIdx < 0) freeIdx = i;
            }
            if (donor == 0) { slot = -1; return false; }   // no template to clone — genuinely can't build one

            // Reuse a free slot if one exists; otherwise append at index=count (physical room up to 256).
            bool append = freeIdx < 0;
            int target = append ? n : freeIdx;
            if (target >= MaxEventPoints) { slot = -1; return false; }
            long e = EventPoints.Slot(arr, target);

            byte[] tmpl = Memory.ReadBytesBatch(donor, EventPoints.Stride);
            Memory.WriteBytesBatch(e, tmpl);          // inherit every field we have not mapped

            Memory.WriteInt(e + EventPoints.Enabled, 1);            // the engine's point check bails if this is 0
            Memory.WriteInt(e + EventPoints.MapFlag, 0);            // no "already done" gate

            // THE ONE THAT SILENTLY SWALLOWED THE LAST ATTEMPT. The donor is a door, whose PartIndex is
            // >= 0 — so EdGetEvent tried to resolve a Georama part and either skipped the point or made
            // our world coordinates part-relative. -1 means "free-standing, Position is world space".
            Memory.WriteInt(e + EventPoints.PartIndex, -1);
            Memory.WriteInt(e + EventPoints.ObjectPtr, 0);          // no CMapObject to inherit a position from
            Memory.WriteInt(e + EventPoints.FramePtr, 0);           // no visibility gate

            Memory.WriteInt(e + EventPoints.ItemOrLabel, labelId);  // type 3 -> the SCRIPT LABEL

            // Suppress the sparkle. The engine draws an animated 3D "shiny marker" at a type-3 point when the
            // field below is > 0. We inherit it from the donor, so a re-install that clones a marked point
            // shows a sparkle at the trigger. Our "!" prompt IS the indicator, so force it off.
            // (draw routine + field: game_data/docs/fishing-engine-re.md §event-point-record)
            Memory.WriteInt(e + 0x20, 0);

            // The donor is a door, so we inherited its name — a live MAP DESTINATION. Type 3 never jumps, but
            // leaving a live map name on a point we are about to fire is asking for a day-long bug. Blank it.
            Memory.WriteBytesBatch(e + EventPoints.Name, new byte[16]);

            Memory.WriteFloat(e + EventPoints.Position, s.TrigX);
            Memory.WriteFloat(e + EventPoints.Position + 4, s.TrigY);
            Memory.WriteFloat(e + EventPoints.Position + 8, s.TrigZ);
            Memory.WriteFloat(e + EventPoints.Radius, s.Radius);    // a scalar radius, NOT a box

            // Type LAST: it is what marks the slot live. Writing it first would expose a half-built point.
            Memory.WriteInt(e + EventPoints.Type, EventPoints.TypeScript);

            // If we appended, raise the live count at its SOURCE so the engine iterates far enough to see our
            // new slot — it copies that into EventPoints.Count every frame. (§event-point-record)
            if (append)
            {
                Memory.WriteInt(arr + EventCountFromBase, n + 1);
                Log($"   appended event point at index {target} (count {n} -> {n + 1}); array was full");
            }

            slot = target;
            return true;
        }

        /// <summary>
        /// Report every event match the engine makes, and keep an eye on our own slot.
        ///
        /// Three "successful" installs have now produced nothing, each time because of a field I had inferred
        /// rather than read. So stop inferring: watch what the engine ACTUALLY matches as the player walks.
        /// Walking past a door should log a match — that proves the mechanism and the array we are writing
        /// into. If doors match and ours never does, the fault is in our record. If NOTHING matches, the
        /// fault is in the array or the count.
        /// </summary>
        private static void WatchMatches()
        {
            // ── FUNCTIONAL (always) ── drive the per-session collision setup off the GameMode/water transition.
            int gm = Memory.ReadInt(EditLoop.GameMode);
            if (gm != _lastGameMode)
            {
                if (Diagnostics) Log($"GameMode {_lastGameMode} -> {gm}   {GameModeName(gm)}");
                _lastGameMode = gm;
            }

            WatchFishingStart();

            // ── OBSERVATIONAL (Diagnostics only) ── transition watches for debugging the trigger/mode chain.
            if (!Diagnostics) return;

            int param = Memory.ReadInt(EventPoints.MatchedParam);
            if (param != _lastParam)
            {
                _lastParam = param;
                uint pt = Memory.ReadUInt(EventPoints.MatchedPoint) & Memory.PhysAddrMask;
                long e = Memory.IsValidGuest(pt) ? Memory.ToMmu(pt) : 0;
                bool ours = e != 0 && e == _slotAddr;

                if (param > 0 || e != 0)
                    Log($"MATCH param={param} point=0x{pt:X8}{(ours ? "  <<< OURS" : "")}  " +
                        $"labelRequest={Memory.ReadInt(EventPoints.ScriptLabelRequest)}" +
                        (e != 0 ? $"  type={Memory.ReadInt(e + EventPoints.Type)} " +
                                  $"label/item={Memory.ReadInt(e + EventPoints.ItemOrLabel)}" : ""));
            }

            // _GOTO_FISHING's whole job is `TownMode = 0xB`. If the mode never reaches 0xB, the command did
            // not run (or bailed on GetChara). If it reaches 0xB and then reverts, the mode ran and something
            // rejected the state — watch it instead of inferring it.
            int mode = Memory.ReadInt(EditLoop.EventReturnCode);
            if (mode != _lastMode)
            {
                Log($"EventReturnCode {_lastMode} -> {mode}" +
                    (mode == EditLoop.ReturnCodeFishing ? "   (script asked for fishing)" : ""));
                _lastMode = mode;
            }

            // Did our slot survive? The array is built progressively during load, and something later in the
            // sequence could reclaim it.
            if (_slot < 0 || ++_watchdog < 100) return;   // every ~5 s
            _watchdog = 0;

            int type = Memory.ReadInt(_slotAddr + EventPoints.Type);
            if (type != EventPoints.TypeScript)
                Log($"our event point [{_slot}] is GONE (type is now {type}) — something reclaimed the slot");
        }

        /// <summary>
        /// Notice when a session actually begins, so the per-session collision setup runs once.
        ///
        /// This used to also DISARM the event point and re-arm it on walk-away, because the old script fired
        /// on contact and returned immediately — so it re-ran every frame, re-loading fishing.pak and leaking
        /// a fresh set of CFish each pass. None of that bookkeeping is needed now: the script draws the "!"
        /// and waits for X, and only yields once you press it. The point can simply stay live, which is also
        /// what makes re-entry work.
        /// </summary>
        private static void WatchFishingStart()
        {
            bool live = Memory.ReadInt(FishingSpot.CPolyNum) > 0
                        || Memory.ReadFloat(FishingSpot.WaterLevel) != 0f;

            if (live) _anyFishingSeen = true;   // FishLineStep is JIT'd from here on — gates the cold patch

            if (live && !_fishingWasLive)
            {
                // Drop every vertical wall from the native cpoly, keeping only the floors/slopes the hook/bobber
                // raycast honours: player movement (its own collision system) still keeps you on the boardwalk,
                // and dropping the walls frees the poly budget for the rocks below.
                FishingCollision.ReplaceWithFloorsOnly(_spot.MapNo);
                // APPEND the simplified rock collision (decoded offline, tools/export_rock_collision.py) so the
                // bobber can't cast onto/through the rocks and fish can't swim through them. Runs after the
                // floors-only compaction, so it fills the slots freed by the dropped walls.
                FishingCollision.AppendRockCollision(_spot.MapNo);

                // TEST AID for the Priscleen port: stamp the loaded fish as species 8 (no-op unless the
                // PriscleenFish.ForceAllSpecies8 switch is on).
                PriscleenFish.ForceSpecies8OnFish();
            }

            // Move the fish to the shallow depth once they spawn. The custom-town SPECIES now come straight
            // from the loader (IsoPatcher.PatchFishingLoadFish bakes dedicated areas 5/6/7 into FishingLoadFish),
            // so there's no mod-side re-species and no race — nothing to do here for species. Depth still waits
            // for the fish: the window goes live (cpoly/water) a few frames before _INIT_FISH places them.
            if (!live) { _shallowFishApplied = false; _fishCPolySynced = false; }
            else
            {
                uint fp = Memory.ReadUInt(FishingSpot.Fish) & Memory.PhysAddrMask;
                bool fishPlaced = Memory.IsValidGuest(fp) && Memory.ReadInt(FishingSpot.FishNum) > 0;

                if (fishPlaced && !_shallowFishApplied && _spot.HasFishDepth)
                { ApplyShallowFishDepth(_spot); _shallowFishApplied = true; }

                // Fish freeze their cpoly COUNT at _INIT_FISH — BEFORE our one-shot append grew the buffer, so
                // the appended containment walls fall past that count and fish swim through them. The moment the
                // fish exist (count is set), re-point it at the live cpoly_num ONCE; the append has already run
                // this same Tick (block above), and nothing rewrites the count afterwards, so no per-frame pin.
                if (fishPlaced && !_fishCPolySynced)
                { FishingCollision.SyncFishCPolyCount(); _fishCPolySynced = true; }
            }
            _fishingWasLive = live;
        }

        private static bool _fishingWasLive;
        private static bool _shallowFishApplied;   // one-shot per session: fish moved to WaterLevel-FishDepth
        private static bool _fishCPolySynced;      // one-shot per session: fish cpoly count re-pointed at live cpoly_num
        private static bool _anyFishingSeen;       // set once any fishing session opens (FishLineStep is now JIT'd/hot)

        /// <summary>
        /// TRUE while a fishing session (or its enter/exit script) owns the game — the window where the
        /// custom-dialogue machinery (TownCharacter's NPC scan + the mailbox flags its PNACH patches key
        /// on) must stand down. BISECT for the Brownboo crash-under-cmd-38: if the crash stops with the
        /// dialogue system quiet, our own machinery was the thing referencing freed villager data.
        ///
        /// Deliberately NOT true during the walk-mode prompt (label runs every frame near the spot as a
        /// simple event) — dialogue near the spot stays functional. Event mode (14) counts only when the
        /// running script is one of OURS, so normal talk dialogues are unaffected.
        /// </summary>
        internal static bool InFishingWindow { get; private set; }

        // The engine's event-mode NPC stepper loops `for i < Villagers.Count` and calls VIRTUAL functions on
        // each villager object. A fishing session frees the villager buffer (cmd 38) and loads the 1.8 MB
        // model over those objects, so their vtable pointers become garbage — and the stepper jumps through
        // one to an unmapped page (the recLUT crash). Brownboo is populated so the count is large and it
        // steps the freed villagers; Yellow Drops is nearly empty so the loop body never runs — which is why
        // only Brownboo crashed. Vanilla's villager clear must pair with suspending this count; we do the
        // same: zero the count for the whole fishing window and restore it on the way out. One write covers
        // every event-mode villager iterator, not just the one we traced.
        // (iterators + object layout: game_data/docs/fishing-engine-re.md §villager-suspend)
        private static int _savedVillagerCount = -1;

        // The engine's villager DRAW walks a HARDCODED 10 slots (so the count knob above does NOT cover it),
        // each gated by a per-object draw flag. The objects live at a FIXED base that the model load does NOT
        // overwrite — only the VISUAL sub-object they point to is freed, and dispatching its vtable through
        // the garbage pointer is the recLUT crash. Zero the fixed draw flags and the gate returns 0 first, so
        // the freed visual is never touched. Restore on exit.
        // (draw routine + object/flag offsets: game_data/docs/fishing-engine-re.md §villager-suspend)
        private static readonly int[] _savedDrawFlags = new int[Villagers.DrawSlots];
        private static bool _drawFlagsSaved;
        private static bool _villagersHidden;   // one-shot latch for UpdateVillagerHide


        private static void UpdateFishingWindow()
        {
            bool was = InFishingWindow;
            InFishingWindow = ComputeFishingWindow(out string why);
            if (InFishingWindow == was) return;
            Log($"fishing-window {(InFishingWindow ? "OPEN" : "closed")} ({why})");

            // The catch bubble (talk msg 2000) and entry/quit menu text (event 20/21/22) are BAKED into each
            // custom town's mes by IsoPatcher, so the engine draws them natively — no buffer swap here.
            // Villagers are hidden LAZILY by UpdateVillagerHide once the screen is fully black (they must stay
            // on screen while the entry menu is up), so there is nothing to do on OPEN; on CLOSE, put them back.
            if (InFishingWindow) return;

            // Restore AFTER the exit script's _LOAD_VILLAGER (57) has rebuilt the villagers.
            if (_savedVillagerCount > 0) Memory.WriteInt(Villagers.Count, _savedVillagerCount);
            if (_drawFlagsSaved)
            {
                for (int i = 0; i < Villagers.DrawSlots; i++)
                    Memory.WriteInt(Villagers.ObjBase + (long)i * Villagers.ObjStride + Villagers.DrawFlag,
                                    _savedDrawFlags[i]);
                _drawFlagsSaved = false;
                Log($"   villagers restored (count -> {_savedVillagerCount}, draw flags restored)");
            }
            _savedVillagerCount = -1;
            _villagersHidden = false;   // re-arm the fade-gated hide for the next session
        }

        /// <summary>
        /// Hides the town's villagers for the session — but only once the screen has fully faded to black.
        ///
        /// This is split out of <see cref="UpdateFishingWindow"/> on purpose. That method's OPEN transition
        /// fires at entry-menu time, and the townspeople must stay on screen while the menu is up (the player
        /// can still back out with FP / log / quit). The enter script only commits to fishing AFTER the menu:
        /// it fades to black (_FADE_OUT), waits for the fade to finish, and THEN frees the villager buffer
        /// (cmd 38) and loads the 1.8 MB fishing model over it. Hiding the villagers exactly at "fully black"
        /// means the pop-out is invisible (the user sees them right up to the fade) yet the count is zeroed
        /// and the draw flags cleared before the engine can step/draw a villager whose memory is being reused.
        ///
        /// Fade state: a fade-out holds <c>fade_in_out == -1</c> and flips <c>fade_end</c> to 1 only when the
        /// black alpha reaches full, staying there until the fade-in. So "fully black on a fade-out" ==
        /// <c>fade_in_out == -1 &amp;&amp; fade_end != 0</c>. We also hide on a live session (cpoly / fishing mode)
        /// as a safety net for attaching mid-session, where the buffer is already freed and leaving villagers
        /// stepping would crash. (fade global: game_data/docs/fishing-engine-re.md §fade-state)
        /// </summary>
        private static void UpdateVillagerHide()
        {
            if (_villagersHidden || !InFishingWindow) return;

            bool fullyBlack = Memory.ReadInt(FishingSpot.FadeInOut) == -1 &&
                              Memory.ReadInt(FishingSpot.FadeEnd) != 0;
            bool sessionLive = Memory.ReadInt(FishingSpot.CPolyNum) > 0 ||
                               Memory.ReadFloat(FishingSpot.WaterLevel) != 0f ||
                               Memory.ReadInt(EditLoop.GameMode) == EditLoop.GameModeFishing;
            if (!fullyBlack && !sessionLive) return;   // entry menu still up (or fade still ramping) — leave them

            // (1) STEP — the event-mode NPC stepper loops `i < count`, so zero the count.
            _savedVillagerCount = Memory.ReadInt(Villagers.Count);
            if (_savedVillagerCount > 0) Memory.WriteInt(Villagers.Count, 0);
            // (2) DRAW — the villager draw walks a fixed 10 slots gated by a per-object flag; zero the 10
            //     fixed-address flags so the gate returns 0 and skips the freed visual. (§villager-suspend)
            for (int i = 0; i < Villagers.DrawSlots; i++)
            {
                long f = Villagers.ObjBase + (long)i * Villagers.ObjStride + Villagers.DrawFlag;
                _savedDrawFlags[i] = Memory.ReadInt(f);
                Memory.WriteInt(f, 0);
            }
            _drawFlagsSaved = true;
            _villagersHidden = true;
            Log($"   villagers hidden at fade-to-black: count {_savedVillagerCount} -> 0, " +
                $"{Villagers.DrawSlots} draw flags -> 0 " +
                $"({(fullyBlack ? "fade complete" : "session live")})");
        }

        private static bool ComputeFishingWindow(out string why)
        {
            // Only our own fishing towns, and only once a spot is installed here.
            if (_installedMap < 0 || _spot.MapNo != _installedMap) { why = "no spot"; return false; }

            // Loaded session / active fishing — unambiguous, always suspend.
            if (Memory.ReadInt(FishingSpot.CPolyNum) > 0 ||
                Memory.ReadFloat(FishingSpot.WaterLevel) != 0f) { why = "cpoly/water live"; return true; }
            int gm = Memory.ReadInt(EditLoop.GameMode);
            if (gm == EditLoop.GameModeFishing) { why = "fishing mode"; return true; }

            // The ENTER/EXIT window is event mode — but so is every town dialogue/cutscene, and hiding the
            // villagers for those is what made them vanish during normal play. The engine stamps the RUNNING
            // event's id into EditEvent.Info early enough (before the enter script's fade + loads), and it is
            // EXACTLY our fishing label — verified live: dialogue events read other ids, our fishing enter
            // reads FishingLabelId (400), and exit/bait run the engine's own labels 133/134. So the
            // running-event id is the clean, position-independent discriminator.
            // (discriminator source: game_data/docs/fishing-engine-re.md §running-event)
            if (gm == EditLoop.GameModeEvent)
            {
                int ev = Memory.ReadInt(EditEvent.Info);
                if (ev == FishingLabelId || ev == EventPoints.FishingExitLabel || ev == EventPoints.FishingBaitLabel)
                { why = $"our fishing event ({ev})"; return true; }
                // Latch through any mid-session event blip (e.g. a frame between fishing and the exit label
                // where EditEvent.Info hasn't updated yet) so a freed villager can't flicker back for one frame.
                if (InFishingWindow) { why = "event mode (latched)"; return true; }
            }
            why = "walking/other";
            return false;
        }
        private static void DumpSlot(string tag, long e)
        {
            if (!Diagnostics) return;
            Log($"{tag} enabled={Memory.ReadInt(e + EventPoints.Enabled)} " +
                $"mapFlag={Memory.ReadInt(e + EventPoints.MapFlag)} " +
                $"partIndex={Memory.ReadInt(e + EventPoints.PartIndex)} " +
                $"type={Memory.ReadInt(e + EventPoints.Type)} " +
                $"objPtr=0x{Memory.ReadUInt(e + EventPoints.ObjectPtr):X} " +
                $"framePtr=0x{Memory.ReadUInt(e + EventPoints.FramePtr):X} " +
                $"label={Memory.ReadInt(e + EventPoints.ItemOrLabel)}");
            Log($"{tag} pos=({Memory.ReadFloat(e + EventPoints.Position):0.#}, " +
                $"{Memory.ReadFloat(e + EventPoints.Position + 4):0.#}, " +
                $"{Memory.ReadFloat(e + EventPoints.Position + 8):0.#})  " +
                $"radius={Memory.ReadFloat(e + EventPoints.Radius):0.#}  " +
                $"time=({Memory.ReadFloat(e + 0x40):0.##}, {Memory.ReadFloat(e + 0x44):0.##})");
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[CustomFishingSpot] " + s);
    }

}
