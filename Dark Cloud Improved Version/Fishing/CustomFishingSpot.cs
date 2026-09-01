using System;
using System.IO;
using static Dark_Cloud_Improved_Version.FishingSpots;
using static Dark_Cloud_Improved_Version.FishingLabelIds;
using static Dark_Cloud_Improved_Version.FishingLabelAllocator;
using static Dark_Cloud_Improved_Version.FishingScriptBuilder;
using static Dark_Cloud_Improved_Version.FishingScripts;

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
    ///
    /// Split 2026-09: this class is the runtime ORCHESTRATOR (install, tick, session window, camera/level
    /// pins). The data and machinery live in FishingSpots (the spot table), FishingLabelIds (ids),
    /// FishingLabelAllocator (where scripts go), FishingScriptBuilder + FishingScripts (the bytecode), FishingCastPayout
    /// (the cast pay-out), with the villager hide in FishingVillagers and fish depth in FishingCollision.
    /// </summary>
    internal static class CustomFishingSpot
    {
        internal static bool Enabled = true;

        /// <summary>Verbose fishing instrumentation: per-tick MATCH / GameMode / EventReturnCode transition
        /// logging, the event-slot readback dump, and the collision poly-gather dump. OFF by default (quiet);
        /// flip on while debugging fishing. Purely observational — no game state depends on it.</summary>
        internal static bool Diagnostics = false;

        /// <summary>Queens fishing-camera wall clamp (see PinFishCamHeight): the canal banks/walls top out at
        /// y70, and the fishing eye must stay BELOW that or the camera skims over the walls with nothing to
        /// collide with. Eye target = WallTop − Clear; Min floors the height when a high tide (dusk, 52)
        /// squeezes the clamp, so the shot never goes flat/cramped.</summary>
        private const float QueensCamWallTopY = 70f;
        private const float QueensCamWallClear = 4f;
        private const float QueensFishCamMinH = 12f;

        private static int _installedMap = -1;

        // Location of the installed fishing bytecode, so it can be re-baked in place on a tide change. 0 = not
        // installed. _fishingStbBase also guards the re-bake (stb-moved check).
        private static long _fishingStbBase;
        private static int _fishingMenuCodeBaseOffset;
        // EVERY installed fishing script for the current town — Queens has TWO (north-bank label 400 +
        // canal-floor label 401), each its OWN per-sign script from the shared BuildFishingBytecode builder,
        // differing only in stance. Re-baked together on a tide change so both track the same tide.
        private static readonly System.Collections.Generic.List<(Spot spot, int codeOff, int end)> _tideScripts = new();

        /// <summary>Re-write the fishing bytecode of EVERY installed spot in place so its baked water level picks
        /// up the current tide (BuildFishingBytecode re-reads <see cref="CanalTide.QueensWaterLevel"/>). Skips
        /// itself during a live session (never rewrite a running script) and if the town's stb has moved (a
        /// rebuild — the install path handles that). Queens only; no labels or event points are touched.</summary>
        internal static void RebuildFishingScript()
        {
            if (_installedMap != TownMapNo.Queens || _fishingStbBase == 0 || InFishingWindow) return;
            long stb = TownScript.Base();
            if (stb == 0 || stb != _fishingStbBase) return;
            foreach (var (spot, codeOff, end) in _tideScripts)
                WriteScript(stb, codeOff, end, BuildFishingBytecode(spot, _fishingMenuCodeBaseOffset),
                            $"re-bake '{spot.Name}' water level for the current tide");
        }

        private static int _lastSeenMap = int.MinValue;
        private static int _settleTicks;

        private static Spot _spot;
        private static int _lastParam = int.MinValue;
        private static int _lastMode = int.MinValue;
        private static int _lastGameMode = int.MinValue;

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

            // RETIRED (2026-08): the anchor toggle is gone — the ISO split caves bake the above/below cutover
            // at A=18, hook depth is distpBelow, and the VANILLA anchor instructions already load point[18].
            // So this no longer patches anything. The one case left to handle: a MOD RELAUNCH against a game
            // this boot ALREADY cold-patched (the six loads read BobberPtr @0x01FB4000, and un-patching code
            // that fishing may have JIT'd is exactly the hot-write crash) — detect it and pin the DATA global
            // to point[18], which makes the patched code behave identically to vanilla.
            bool anyPatched = false;
            foreach (var (lui, ld, reg) in FishLineShallow.Sites)
                if (Memory.ReadUInt(lui) == FishLineShallow.NewLui(reg)) { anyPatched = true; break; }
            if (anyPatched)
            {
                Memory.WriteUInt(FishLineShallow.BobberPtr, FishLineShallow.PointVanilla);   // A=18, data-side
                Log("shallow-line: leftover cold patch from a previous mod run — BobberPtr pinned to point[18] (anchor toggle is retired)");
            }
            _shallowLineInstalled = true;   // nothing (more) to install this boot
        }

        // ── Queens: line length + bobber anchor vary by TIDE ─────────────────────────────────────────────
        // The canal is ONE body of water fished two ways, and the geometry differs hugely between tides: at
        // medium/high you cast DOWN from the north bank onto a deep column; at low tide you stand ON the
        // exposed floor in ~8 units of water. So both line levers are resolved from the TIDE, not the spot.
        //
        // Resting hook depth = how much line hangs below the bobber — set by the anchor point (fewer points
        // between anchor and hook 23 = shallower) and scaled by the per-segment rest length (lineScale).
        // ⚠ CAPTURE THESE: log the resting hook depth in each tide, then set the matching fish depths via
        // fishDepth = hangDepth + 3.68. Fish depths are deliberately LEFT ALONE until that capture.
        private const int   QueensAnchorNormal  = 20;     // legacy anchor — feeds the Below() mapping only
        private const float QueensLineNormal    = 1.35f;  // legacy hang-mapping lineScale for the bank spot (1.5x measured too long in-game)
        private const float ExtendedCastAbove   = 2.0f;   // EXTENDED aerial cast TARGET (pays out on the throw) — Queens
                                                          // non-low (casting DOWN from the north bank) AND Yellow Drops.
        private const float ShortLineStart      = 0.9f;   // reeled/pre-cast distpAbove scale (Queens + Yellow Drops): a slightly
                                                          // tauter line at the whip → cleaner sling; the payout ramps from here.
        private const float HookBodyDrop       = 3.47f;  // measured: realized hook depth − 5·distpBelow (hook sub-body/rig
                                                         // geometry below point[23]; from the logged hang 3.33 ↔ depth 6.8 pair)

        /// <summary>Fishing-line config for the SPLIT rope (ISO caves bake the above/below cutover at A=18,
        /// so the bobber ALWAYS anchors at the vanilla point[18] — the 18↔20/21 anchor toggle is RETIRED).
        /// <c>aboveStart</c> scales the reeled/pre-cast aerial rest length (the whip slings at this);
        /// <c>above</c> is the aerial TARGET the cast pay-out ramps to;
        /// <c>below</c> is the absolute below-bobber rest length (bobber→hook = hook depth).
        /// Below() maps each OLD anchor/lineScale combo to the distpBelow that yields the IDENTICAL realized
        /// hang — (23−anchor) old hang segments at (vanilla×ls) spread over the 5 fixed segments — so the
        /// tuned fish depths and the bite chain are unchanged by the retirement.</summary>
        private static (float aboveStart, float above, float below) LineConfigSplit(Spot s)
        {
            // Depth-driven hang: distpBelow such that the hook RESTS at the given depth (hook rest =
            // 5·distpBelow + HookBodyDrop). Floored so distpBelow can never collapse (0 kills the hang).
            static float BelowForDepth(float depth) => Math.Max((depth - HookBodyDrop) / 5f, 0.2f);
            static float Below(int oldAnchor, float ls) => (23 - oldAnchor) * FishLineShallow.VanillaDistp * ls / 5f;
            if (s.MapNo != TownMapNo.Queens)
            {
                float ls = s.HasLineScale ? s.LineScale : 1f;
                // GENERAL RULE: a spot that pins its fish depth gets the hook resting AT the fish (the bite
                // geometry confirmed at Queens low tide) — one knob (fishDepth) drives both. Spots without
                // a fish depth keep the legacy vanilla anchor-equivalence mapping.
                float below = s.HasFishDepth ? BelowForDepth(s.FishDepth)
                                             : Below(18, ls);
                if (s.MapNo == TownMapNo.YellowDrops)
                    return (ShortLineStart, ExtendedCastAbove, below);   // Yellow Drops: same extended pay-out cast as Queens
                return (1f, ls, below);
            }
            // Low tide: you stand ON the canal floor at the water's edge — a near-VANILLA cast reaches fine.
            // Every other tide: you cast DOWN from the north bank onto the deep column — that's where the
            // extended pay-out cast is needed.
            return CanalTide.QueensLowTide()
                ? (ShortLineStart, 1f,                BelowForDepth(QueensFishDepthLow))
                : (ShortLineStart, ExtendedCastAbove, Below(QueensAnchorNormal, QueensLineNormal));
        }
        private static float _lineAboveStart = 1f;                             // session-resolved reeled/pre-cast aerial scale
        private static float _lineAbove = 1f;                                  // session-resolved aerial TARGET scale
        private static float _lineBelow = FishLineShallow.VanillaDistp;        // session-resolved hang rest length

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

            PinFishCamHeight();   // keep the patched SetHeight site fed (per-spot fishing camera height)
            PinYdWaterLevel();    // Yellow Drops: hold the live water level against the camera-window re-derive

            InstallShallowLinePatch();   // idempotent retry: lands the cold FishLineStep patch once the game's
                                         // code is present (ApplyNewChanges may fire before it is), before fishing

            int map = Memory.ReadInt(EditLoop.MapNo);

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
                        Log("install incomplete — re-installing");
                        ResetInstallState();
                    }
                }
                if (map == _installedMap)
                {
                    WatchMatches(); UpdateFishingWindow(); FishingVillagers.HideForSession(InFishingWindow); PriscleenFish.Tick();
                    if (_spot.MapNo == TownMapNo.Brownboo) FishingVillagers.PinMango();   // Brownboo: nudge Mango out from under the (baked) sign
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
            _fishingStbBase = 0;
            _fishingWasLive = false;
            InFishingWindow = false;
            _settleTicks = 0;
            _verifyTicks = 0;
            _lastParam = int.MinValue;
            _lastMode = int.MinValue;
            // (anchor toggle retired — the bobber is always at point[18]; nothing to reset there)
            FishingCastPayout.Reset();
            Memory.WriteFloat(CodeCaves.Mailbox.LineDistpBelow, FishLineShallow.VanillaDistp);   // hang back to vanilla for the next town
            _lineAboveStart = 1f; _lineAbove = 1f; _lineBelow = FishLineShallow.VanillaDistp;
            PriscleenFish.Uninstall();
            FishingVillagers.Uninstall();
            FishingVillagers.ResetSession();
        }

        /// <summary>True only if BOTH halves of our install are still live: the renumbered fishing label in
        /// the stb, AND our event point. A cold save-load can leave a PARTIAL install — the label gets
        /// written but event-point creation fails (no donor/free slot yet) or the point is wiped as the load
        /// finishes — and a label-only check would wrongly report "installed" forever, so the "!" never
        /// appears (the bug on first load into Yellow Drops). Checking the event point too forces a retry.</summary>
        private static bool FishingInstallPresent()
        {
            // The trigger is baked (native ISO event point) — it can't be wiped at runtime, so we no longer
            // check for it. Only our runtime SCRIPT can be lost: a same-map town rebuild re-reads the stb from
            // disc, restoring label 400 to its EMPTY baked spare. Detect that by checking label 400's code
            // still holds our fishing bytecode (compare the first word to a freshly-built script). Anything we
            // can't yet read (mid-rebuild) returns true so we don't thrash a needless reinstall.
            long stb = TownScript.Base();
            if (stb == 0) return true;
            int n = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            if (n <= 0 || n >= 4000 || tbl <= 0) return true;
            ScriptLabel lab = FindLabelById(stb, n, tbl, FishingLabelId);
            if (lab == null) return true;                        // label table mid-rebuild — wait
            byte[] want = BuildFishingBytecode(_spot, _fishingMenuCodeBaseOffset).ToArray();
            if (want.Length < 4) return true;
            int firstWord = Memory.ReadInt((int)stb + lab.Off + TownScript.LabelCodeSkip);
            return firstWord == BitConverter.ToInt32(want, 0);   // matches -> our script is live; differs -> wiped
        }

        // The spot the player is actually FISHING. A town can have several baked signs (Queens: north-bank +
        // canal-floor), each with its own stance — the fishing script snapped the player to whichever they
        // triggered, so the nearest StandX identifies it. Drives the per-spot depth / line / bobber below so
        // the canal fishes shallow while the north bank keeps its stretched line. Single-spot towns → _spot.
        private static Spot _active;
        private static float _lastCamH = float.NaN;

        /// <summary>Feed the patched fishing-camera SetHeight site (IsoPatcher.PatchFishingCameraHeight turned
        /// its hard-coded 40 into a read of <see cref="CodeCaves.Mailbox.FishCamHeight"/>).
        ///
        /// The patched instruction runs every frame of every fishing session in EVERY town — including the
        /// vanilla ones this class never installs into — so this must ALWAYS hold a sane value, never 0. Away
        /// from a custom spot it is the vanilla 40; at a custom spot it is that spot's height, chosen by
        /// proximity BEFORE the session starts (so there is no first-frame pop) and pinned to the spot actually
        /// being fished once one is live. Only written on change.</summary>
        /// <summary>Seed the fishing camera-height word to the vanilla 40 at mod start. The ISO patch makes the
        /// engine READ this word every frame of every fishing session — including vanilla towns and even when
        /// <see cref="Enabled"/> is off — so it must be valid before anything can fish, not just once a custom
        /// spot installs.</summary>
        internal static void SeedFishCamHeight()
        {
            Memory.WriteFloat(CodeCaves.Mailbox.FishCamHeight, VanillaFishCamHeight);
            _lastCamH = VanillaFishCamHeight;
            Memory.WriteFloat(CodeCaves.Mailbox.CameraRestH, TownRestH);   // town-camera REST_H default (must be valid before any camera frame)
            _lastRestH = TownRestH;
            // Seed the split's BELOW-bobber rest length (the ISO caves read it @0x01F10048 every frame) so it's
            // never 0 (0 collapses the hang) before a session resolves the real per-spot value.
            Memory.WriteFloat(CodeCaves.Mailbox.LineDistpBelow, FishLineShallow.VanillaDistp);
        }

        /// <summary>Town-camera resting eye height (== IsoPatcher's REST_H, still baked into the climb-curve base
        /// at word 471). The height-TARGET REST_H (word 147) now reads <see cref="CodeCaves.Mailbox.CameraRestH"/>
        /// instead, which we drive below.</summary>
        internal const float TownRestH = 5f;

        /// <summary>Yellow Drops: pin the live water level (<see cref="FishingSpot.WaterLevel"/>) to the
        /// spot's configured water while a session is live. The west-pocket spot sits OUTSIDE the town's
        /// +-320 WATER_SURFACE square, and the engine re-derives the level from the camera-following
        /// surface window each frame — so once the surface was raised (0 -> 4.25), the level (and with it
        /// the bobber float height / line pay-out) visibly flickered with CAMERA ANGLE. Fishing-window
        /// only: outside fishing, WaterLevel==0 is what the install/teardown liveness checks expect.</summary>
        private static void PinYdWaterLevel()
        {
            if (!InFishingWindow || _installedMap != TownMapNo.YellowDrops || _active.MapNo != TownMapNo.YellowDrops) return;
            if (Math.Abs(Memory.ReadFloat(FishingSpot.WaterLevel) - _active.Water) > 0.001f)
                Memory.WriteFloat(FishingSpot.WaterLevel, _active.Water);
        }

        private static void PinFishCamHeight()
        {
            float h = VanillaFishCamHeight;
            if (_installedMap >= 0 && _spot.MapNo == _installedMap)
                h = (InFishingWindow ? _active : ActiveSpot()).CameraHeight;
            if (float.IsNaN(h) || h <= 0f) h = VanillaFishCamHeight;   // never let a bad spot value blank the camera

            // Queens: keep the fishing eye BELOW the canal-wall tops (y70). The camera orbits the BOBBER
            // (ref.y ≈ the fishing water level), so eye.y ≈ water + height: the vanilla 40 put the eye at
            // ~71 at medium tide (31) — skimming just OVER the y70 banks, where there is no wall poly left
            // to collide with (the "y≈70.5" pass-through). Clamp so eye.y ≤ WallTop − Clear; tide-aware via
            // the same per-time level the bobber rides, so every tide gets the max height that still
            // engages the walls (medium: 40→35; dusk high 52: →14; low 6: 40 unchanged). No climb-disable
            // is needed on top: the patched SetHeight site (0x16C2DC) re-pins the height to this word every
            // fishing frame AFTER the collision cave runs, so nothing can raise the camera during fishing.
            if (_installedMap == TownMapNo.Queens && _spot.MapNo == TownMapNo.Queens)
            {
                float water = CanalTide.QueensWaterLevel();
                h = Math.Max(QueensFishCamMinH, Math.Min(h, QueensCamWallTopY - QueensCamWallClear - water));
            }

            // Drive the camera's data-driven REST_H: the SPOT's fishing height while a session is live (so the
            // camera EASES to it as its rest), the town rest otherwise. This replaces the fight between our
            // swept-slide (easing toward REST_H) and EdMoveChara's per-frame SetHeight clamp — now the rest
            // TARGET equals the clamp value, so there's no desync and the distance recovers like it does in town.
            float restH = InFishingWindow ? h : TownRestH;
            if (float.IsNaN(_lastRestH) || Math.Abs(_lastRestH - restH) >= 0.01f)
            { Memory.WriteFloat(CodeCaves.Mailbox.CameraRestH, restH); _lastRestH = restH; }

            if (!float.IsNaN(_lastCamH) && Math.Abs(_lastCamH - h) < 0.01f) return;
            Memory.WriteFloat(CodeCaves.Mailbox.FishCamHeight, h);
            _lastCamH = h;
        }
        private static float _lastRestH = float.NaN;

        private static Spot ActiveSpot()
        {
            // ⚠ FIXED 2026-08: this used to read guest 0x21EA1D30 — Addresses.dunPositionX, the DUNGEON
            // player-position global (its own name says so). In town that address is unrelated data, so
            // every proximity pick here silently always won by whichever spot happened to sort first —
            // which is exactly why the canal sign kept resolving to the north-bank spot's name/stance no
            // matter where the player actually stood. EditLoop.TryReadPlayerPos is the TOWN-correct read
            // (GeoramaProbe-verified, via the CCharacter CFrame pointer, not a loose position global).
            if (!EditLoop.TryReadPlayerPos(out float px, out _, out _)) return _spot;
            Spot best = _spot; float bestD = float.MaxValue; int hits = 0;
            foreach (var s in Spots)
            {
                if (s.MapNo != _installedMap) continue;
                hits++;
                float d = Math.Abs(s.StandX - px);
                if (d < bestD) { bestD = d; best = s; }
            }
            return hits > 1 ? best : _spot;
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

            BuildHijackPool(stb, labelCount, tbl);   // native-orphan pool, used only as the unbaked-town fallback

            // Each script goes into the ISO-baked label that already carries its final id (9600/400/133/134),
            // sized to fit it in one label — so nothing is renumbered or split. ClaimLabel falls back to a
            // renamed native orphan on an unpatched disc. The MENU is claimed FIRST so its offset is known
            // before the entry/quit scripts CALL_FUNC it; on the fallback path that also marks its orphan used
            // before the entry script's arena is carved out. If it can't be placed, menuCodeBaseOffset stays -1 and
            // both menus fall back to inline copies.
            int codeBaseVal = Memory.ReadInt(stb + TownScript.CodeBase);
            int menuCodeBaseOffset = -1;
            ScriptLabel menuLab = ClaimLabel(stb, labelCount, tbl, MenuSubLabelId, ScriptByteSize(BuildMenuSubroutine()), out int menuEnd);
            if (menuLab != null)
            {
                Memory.WriteInt(stb + menuLab.Entry, MenuSubLabelId);   // no-op for a baked label; renames a fallback orphan
                WriteScript(stb, menuLab.Off, menuEnd, BuildMenuSubroutine(),
                            "shared menu-select subroutine (CALL_FUNC target for entry + quit menus)");
                menuCodeBaseOffset = menuLab.Off - codeBaseVal;
                Log($"   menu subroutine: label {MenuSubLabelId} (code @+0x{menuLab.Off:X}, CALL_FUNC cb-rel 0x{menuCodeBaseOffset:X})");
            }
            else Log("   no spare label for the shared menu subroutine — entry/quit menus fall back to inline");

            ScriptLabel lab = ClaimLabel(stb, labelCount, tbl, spot.LabelId, ScriptByteSize(BuildFishingBytecode(spot, menuCodeBaseOffset)), out int end);
            if (lab == null)
            {
                Log("   the spare labels cannot hold the fishing script — skipping");
                return;
            }

            // The entry label answers to an id nothing else dispatches (400/401): only OUR baked event point
            // names it, so no town event of its own can reach the fishing script.
            int codeOff = lab.Off;
            Memory.WriteInt(stb + lab.Entry, spot.LabelId);   // no-op for a baked label; renames a fallback orphan
            int labelId = spot.LabelId;

            Log($"   entry script @0x{stb:X}  labels={labelCount}  label {labelId} " +
                $"(code @+0x{codeOff:X}, {end - codeOff}B region)");

            WriteScript(stb, codeOff, end, BuildFishingBytecode(spot, menuCodeBaseOffset),
                        $"_LOAD_MAIN_CHARA({FishingModel}) + _LOAD_FISHING_DATA(area={spot.AreaId}, " +
                        $"water={spot.Water}) + stance + bait + fishing");
            // remember exactly where the fishing bytecode lives so the CanalTide tide change can re-bake just
            // its water arg (BuildFishingBytecode re-reads CanalTide.QueensWaterLevel) without touching the
            // labels or the event point.
            _fishingStbBase = stb; _fishingMenuCodeBaseOffset = menuCodeBaseOffset;
            _tideScripts.Clear();
            _tideScripts.Add((spot, codeOff, end));   // primary; secondaries (Queens canal) added in the loop below

            InstallEngineLabel(stb, labelCount, tbl, EventPoints.FishingExitLabel, BuildExitBytecode(spot, menuCodeBaseOffset),
                               $"restore {NormalModel} + re-place player + _EXIT_FISHING   [Circle = leave]");
            InstallEngineLabel(stb, labelCount, tbl, EventPoints.FishingBaitLabel, BuildBaitBytecode(),
                               $"_GOTO_CHANGE_ESA + load the chosen bait   [Square = bait menu]");

            // SECONDARY spots on the same map (Queens canal floor, label 401): each is its OWN per-sign script
            // from the SAME BuildFishingBytecode builder — same fishing logic, just its own stance/depth. They
            // share this town's menu/exit/bait (just installed) and go into their own baked spare label, so
            // triggering either sign runs a script with the correct stance baked in — no runtime re-position.
            foreach (var extra in Spots)
            {
                if (extra.MapNo != spot.MapNo || extra.LabelId == spot.LabelId) continue;
                ScriptLabel el = ClaimLabel(stb, labelCount, tbl, extra.LabelId, ScriptByteSize(BuildFishingBytecode(extra, menuCodeBaseOffset)), out int eEnd);
                if (el == null) { Log($"   secondary spot '{extra.Name}': no spare label {extra.LabelId} — skipped"); continue; }
                Memory.WriteInt(stb + el.Entry, extra.LabelId);
                WriteScript(stb, el.Off, eEnd, BuildFishingBytecode(extra, menuCodeBaseOffset),
                            $"secondary fishing spot '{extra.Name}' (area={extra.AreaId}, stance {extra.StandX},{extra.StandY},{extra.StandZ})");
                _tideScripts.Add((extra, el.Off, eEnd));   // re-baked with the primary on every tide change
                Log($"   secondary spot '{extra.Name}' installed at label {extra.LabelId} (code @+0x{el.Off:X})");
            }

            // Queens canal ladder "tide too high" message (label 402, Queens only). CanalTide enables this
            // baked type-3 point (IsoPatcher) instead of the native climb-down at HIGH tide; the script is the
            // same prompt-don't-pounce shape as the fishing entry — draws "!", and only on X-press shows the
            // line. Rides the fishing install/self-heal (same stb), so no separate presence check.
            if (spot.MapNo == TownMapNo.Queens)
            {
                ScriptLabel ml = ClaimLabel(stb, labelCount, tbl, LadderMsgLabelId, ScriptByteSize(BuildLadderMessageBytecode()), out int mlEnd);
                if (ml == null) Log($"   ladder message: no spare label {LadderMsgLabelId} — skipped");
                else
                {
                    Memory.WriteInt(stb + ml.Entry, LadderMsgLabelId);
                    WriteScript(stb, ml.Off, mlEnd, BuildLadderMessageBytecode(),
                                $"canal ladder 'tide too high' message (event-mes {LadderMsgId}, prompt-don't-pounce)");
                    Log($"   ladder message installed at label {LadderMsgLabelId} (code @+0x{ml.Off:X})");
                }

                // Canal tide-evict warp (label 403): CanalTide fires it as an event to warp a player caught in
                // the drained canal to the East Harbor dock when the tide rises. Just the _MAP_JUMP script.
                ScriptLabel wl = ClaimLabel(stb, labelCount, tbl, CanalWarpLabelId, ScriptByteSize(BuildCanalWarpBytecode()), out int wlEnd);
                if (wl == null) Log($"   canal tide-evict: no spare label {CanalWarpLabelId} — skipped");
                else
                {
                    Memory.WriteInt(stb + wl.Entry, CanalWarpLabelId);
                    WriteScript(stb, wl.Off, wlEnd, BuildCanalWarpBytecode(),
                                $"canal tide-evict _MAP_JUMP(mapArg {EastHarborMapArg} → East Harbor)");
                    Log($"   canal tide-evict installed at label {CanalWarpLabelId} (code @+0x{wl.Off:X})");
                }
            }

            // The trigger is now BAKED into the ISO — a native type-3 event point in the town's own scene.scn
            // (IsoPatcher.BuildFishingFunc), created by the engine at town load. So we no longer create a
            // runtime event point; we only install the fishing SCRIPT here, and the baked point names label
            // 400. The baked point survives day/night AND town rebuilds; only the runtime script can be lost
            // (a same-map rebuild re-reads the stb), which FishingInstallPresent now detects by script content.
            _ = labelId;
            _spot = spot;
            _active = spot;   // default until a session pins the actually-fished spot (ActiveSpot)

            // Line config (data-only): resolve up front from the same resolver the session start uses, so the
            // split's distpBelow is already right if a session begins before the next tick. The bobber anchor
            // is FIXED at the vanilla point[18] now (the ISO split caves bake A=18; the 18↔20/21 anchor
            // toggle is retired — hook depth comes from distpBelow instead). Fish are moved to match on the
            // fishing-window open (ApplyShallowFishDepth) once they've spawned.
            (_lineAboveStart, _lineAbove, _lineBelow) = LineConfigSplit(spot);
            Memory.WriteFloat(CodeCaves.Mailbox.LineDistpBelow, _lineBelow);

            if (spot.MapNo == TownMapNo.Brownboo) PriscleenFish.Install();   // Priscleen (DC2 fish) into species 8, Brownboo only

            Log($"   fishing script installed for label {FishingLabelId} " +
                $"(trigger is BAKED into the ISO at ~({spot.TrigX},{spot.TrigY},{spot.TrigZ}))");
            if (spot.HasFishRect)
                Log($"   fish rect ({spot.FishX1},{spot.FishZ1})-({spot.FishX2},{spot.FishZ2}) " +
                    $"(cast rect is separate)");
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

                if (param > 0 || e != 0)
                    Log($"MATCH param={param} point=0x{pt:X8}  " +
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
                // Which sign did the player trigger? Pin it for this session, then resolve the SPLIT line
                // config (Queens by tide, others per-spot). Anchor is FIXED at point[18] (A=18 baked in the
                // ISO caves); hook depth = distpBelow, aerial reach = distpAbove (paid out on the cast). The
                // tide cannot change mid-session, so this one resolution holds for the whole session.
                _active = ActiveSpot();
                (_lineAboveStart, _lineAbove, _lineBelow) = LineConfigSplit(_active);
                Log($"line config for '{_active.Name}': above x{_lineAboveStart:0.##}→x{_lineAbove:0.##}, below {_lineBelow:0.##} " +
                    $"(tide level {(_active.MapNo == TownMapNo.Queens ? CanalTide.QueensWaterLevel() : _active.Water):0.#})");

                // Brownboo: drop the ladder-top platforms from the cpoly (bobber ground-lift guard). Native walls
                // are KEPT — they contain the fish; the bobber probe never treats a wall as ground.
                FishingCollision.DropLadderTopFloors(_spot.MapNo);
                // APPEND the town's fishing collision (DCFC bin, tools/build_fishing_collision.py) so the
                // fish are boxed in where the native geometry is open (Queens / Yellow Drops fish walls).
                FishingCollision.AppendCustomCollision(_spot.MapNo);

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

                if (fishPlaced && !_shallowFishApplied && _active.HasFishDepth)
                { FishingCollision.ApplyFishDepth(_active.FishDepth); _shallowFishApplied = true; }

                // Fish freeze their cpoly COUNT at _INIT_FISH — BEFORE our one-shot append grew the buffer, so
                // the appended containment walls fall past that count and fish swim through them. The moment the
                // fish exist (count is set), re-point it at the live cpoly_num ONCE; the append has already run
                // this same Tick (block above), and nothing rewrites the count afterwards, so no per-frame pin.
                if (fishPlaced && !_fishCPolySynced)
                { FishingCollision.SyncFishCPolyCount(); _fishCPolySynced = true; }
            }

            // Line LENGTH (Queens): stretch the shared Verlet rest-length while this spot is live so the line
            // reaches the low canal, and restore vanilla the moment the session ends. distp is read every frame
            // (data-only, recompiler-safe), so pin it here rather than as a one-shot.
            // Length comes from the session-resolved SPLIT config (LineConfigSplit: Queens by tide, others
            // per-spot): distpBELOW = the spot's hook depth (bobber→hook hang), distpABOVE = aerial reach,
            // ramped out by the cast pay-out below. Anchor is FIXED at point[18] (A=18 baked in the ISO caves).
            if (live) Memory.WriteFloat(CodeCaves.Mailbox.LineDistpBelow, _lineBelow);   // hang = the spot's hook depth
            // Pay-out projects the rod tip along the player's LIVE facing: they can turn (and walk) before casting,
            // so the session-start stance yaw is only the fallback when the character can't be read.
            float facing = EditLoop.TryReadPlayerYaw(out float liveYaw) ? liveYaw : _active.Facing;
            FishingCastPayout.Tick(live, _lineAboveStart, _lineAbove, facing);               // line LENGTH + cast pay-out

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
            FishingVillagers.RestoreAfterSession();
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

        internal static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[CustomFishingSpot] " + s);
    }
}
