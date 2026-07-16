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
    ///   -> EdGetEvent: matchedParam = 3, matchedPoint
    ///   -> EdMoveChara: label = matchedPoint[+0x1C]      // an event point's +0x1C is a SCRIPT LABEL for type 3
    ///   -> the VM runs that label
    /// </code>
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
            internal readonly float FishDepth;
            internal bool HasFishDepth => !float.IsNaN(FishDepth);

            /// <summary>
            /// FABRICATED COLLISION. The town's native cpoly is all ABOVE the fish (it's the land), so fish
            /// and hook fall through it. When set, the mod REPLACES cpoly with vertical walls built from these
            /// outlines — the water Perimeter (a closed loop, fish kept in) and any Obstacles (rocks/pillars,
            /// fish kept out). Each is a flat float array of x,z pairs. Walls span the fish's depth band, so
            /// a fish swimming at WaterLevel-FishDepth physically cannot cross them.
            /// </summary>
            internal readonly float[] Perimeter;
            internal readonly float[][] Obstacles;
            internal bool HasCollision => Perimeter != null;

            /// <summary>DIAGNOSTIC: skip the turi model swap. Proven the model load (not the rect, not pool
            /// memory) is what crashes Brownboo — with the swap skipped it reaches fishing mode.</summary>
            internal readonly bool DiagSkipModel;

            /// <summary>DIAGNOSTIC: do NOT clear/reload the town's villagers. <c>_CLEAR_VILLAGER_BUFF</c> only
            /// rewinds the villager allocator, it does not delete the objects — so the model then loads over
            /// memory the town still references. Harmless in Yellow Drops; a candidate cause in Brownboo.
            /// With this set, the entry skips the clear and the exit skips the reload (nothing to restore).</summary>
            internal readonly bool DiagNoVillagerClear;

            internal Spot(int mapNo, string name, int areaId,
                          float x1, float z1, float x2, float z2, float water, float ground,
                          float tx, float ty, float tz, float radius,
                          float sx = float.NaN, float sy = float.NaN, float sz = float.NaN,
                          float facing = float.NaN, bool diagSkipModel = false,
                          bool diagNoVillagerClear = false,
                          float fx1 = float.NaN, float fz1 = float.NaN, float fx2 = float.NaN,
                          float fz2 = float.NaN, float fishDepth = float.NaN,
                          float[] perimeter = null, float[][] obstacles = null)
            {
                MapNo = mapNo; Name = name; AreaId = areaId;
                X1 = x1; Z1 = z1; X2 = x2; Z2 = z2; Water = water; Ground = ground;
                TrigX = tx; TrigY = ty; TrigZ = tz; Radius = radius;
                StandX = sx; StandY = sy; StandZ = sz; Facing = facing;
                DiagSkipModel = diagSkipModel;
                DiagNoVillagerClear = diagNoVillagerClear;
                FishX1 = fx1; FishZ1 = fz1; FishX2 = fx2; FishZ2 = fz2; FishDepth = fishDepth;
                Perimeter = perimeter; Obstacles = obstacles;
            }
        }

        // Water planes are the HEIGHT values the probe read out of each town's WATER_SURFACE table.
        //
        // Rectangles are kept near 200x200 on purpose: PickUpPoly HANGS THE GAME if it gathers more than
        // 0x400 polys (a dev assert that shipped). Norune's real 200x200 spot gathers 197, so that is the
        // proven-safe scale. Do not widen these without watching cpoly.
        //
        // The trigger is a RADIUS around a world point, not a box — EdGetEvent tests
        // `DistVector(pos, player) < point[0x60]`. Keep it modest: EdGetEvent matches ONE point, so an
        // over-large radius wins over every door and their "!" markers vanish (which is what happened at
        // 2000 units).
        private static readonly Spot[] Spots =
        {
            // Queens: the canal (static WATER e03c01/c02/c08), surface at Y=31.
            new Spot(2, "Queens canal", 0,
                     -100f, -40f, 100f, 40f, water: 31f, ground: 10f,
                     tx: 0f, ty: 31f, tz: 0f, radius: 200f),

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
            // diagNoVillagerClear skips the clear, so the spot should now work.
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
            new Spot(14, "Brownboo lake", 0,
                     -320f, -260f, 310f, 300f, water: 0f, ground: -15f,
                     tx: 74f, ty: 10f, tz: -20f, radius: InteractRadius,
                     sx: 74f, sy: 10f, sz: -20f, facing: -1.639f,
                     diagNoVillagerClear: true,   // the crash fix: don't clear villagers in Brownboo
                     fishDepth: 6f,
                     // Fabricated collision: the lake shore (fish kept in) + the two large rocks (fish kept
                     // out), traced with the overhead cursor and decimated. Walls span the fish's depth band.
                     perimeter: new[]
                     {
                         -243f,-72f, -147f,-250f, -115f,-271f, -91f,-281f, -37f,-294f, 55f,-296f, 71f,-292f,
                         164f,-239f, 218f,-176f, 266f,-68f, 287f,10f, 295f,24f, 291f,95f, 285f,108f, 285f,131f,
                         249f,169f, 205f,204f, 179f,214f, 98f,232f, 76f,232f, 10f,245f, -67f,230f, -192f,160f,
                         -248f,59f, -258f,25f, -251f,-31f, -235f,-69f,
                     },
                     // Obstacle footprints extracted from Brownboo's own mesh (gedit/s04/scene.scn), decoded
                     // offline: each is the underwater XZ hull of an "iwa" (rock) node, in world coords (pond
                     // centre = 0,0), expanded 3u outward for fish clearance. Precise, not hand-traced.
                     obstacles: new[]
                     {
                         new[] { -207f,-34f, -203f,-58f, -166f,-86f, -135f,-88f, -123f,-84f, -88f,-50f, -91f,-9f, -100f,6f, -151f,29f, -186f,10f },   // iwa01 (west)
                         new[] { -49f,150f, -48f,145f, -38f,136f, -28f,134f, 50f,136f, 62f,147f, 64f,153f, 62f,168f, 49f,175f, -38f,168f },           // iwa02 (north)
                         new[] { 182f,-48f, 186f,-66f, 203f,-81f, 212f,-84f, 231f,-76f, 245f,-51f, 247f,-33f, 229f,-18f, 219f,-15f, 197f,-25f },       // iwa03 (east)
                     }),

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
            new Spot(23, "Yellow Drops liquid", 0,
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
        private static bool IsSystemLabel(int id) => id < 300;

        private static int _installedMap = -1;
        private static int _lastSeenMap = int.MinValue;
        private static int _settleTicks;

        private static int _slot = -1;
        private static long _slotAddr;
        private static int _labelId;
        private static Spot _spot;
        private static int _lastParam = int.MinValue;
        private static int _lastMode = int.MinValue;
        private static int _lastGameMode = int.MinValue;
        private static int _watchdog;

        // Byte offsets, within the exit script, of the first float operand of _SET_NPC_POS / _SET_NPC_ROT,
        // and their live addresses once written. See PatchExitPosition.
        private static int _exitPosOperand = -1, _exitRotOperand = -1;
        private static long _exitPosAddr, _exitRotAddr;

        /// <summary>Armed when a fishing session starts, so <see cref="SnapCameraBehindPlayer"/> fires once
        /// on entry — and NOT every time the bait menu bounces us through event mode and back.</summary>
        private static bool _snapCamera;

        private static bool _fishDepthPatched;

        /// <summary>Patch the inline fish-depth constant for this spot (WaterLevel - depth), if it sets one.
        /// In-place, one instruction, at install time; undone on town change by <see cref="RestoreFishDepth"/>.</summary>
        private static void PatchFishDepth(Spot spot)
        {
            if (!spot.HasFishDepth) return;
            if (Memory.ReadUInt(FishDepthPatch.Instr) != FishDepthPatch.Original) return;  // already patched / moved
            Memory.WriteInt(FishDepthPatch.Instr, unchecked((int)FishDepthPatch.For(spot.FishDepth)));
            _fishDepthPatched = true;
            Log($"   fish depth patched to WaterLevel-{spot.FishDepth} (was -12)");
        }

        private static void RestoreFishDepth()
        {
            if (!_fishDepthPatched) return;
            Memory.WriteInt(FishDepthPatch.Instr, unchecked((int)FishDepthPatch.Original));
            _fishDepthPatched = false;
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

            int map = Memory.ReadInt(EditLoop.MapNo);

            // Leaving a town and coming back RELOADS it — the script buffer is re-read and the event array
            // rebuilt, so our install is gone. Remembering "already installed for map 23" would then skip a
            // town whose spot no longer exists. Reset the moment the map changes at all.
            if (map != _lastSeenMap)
            {
                _lastSeenMap = map;
                _installedMap = -1;
                _slot = -1;
                _fishingWasLive = false;
                _settleTicks = 0;
                _lastParam = int.MinValue;
                _lastMode = int.MinValue;
                RestoreFishDepth();     // undo any per-town fish-depth patch before the next town
            }

            if (map == _installedMap) { WatchMatches(); return; }

            if (!TryGetSpot(map, out Spot spot)) return;

            // The script buffer and the event array are both populated LATE, and the event array is built up
            // progressively. Installing into a half-built town is how you get silent nonsense, so wait.
            if (!ScriptReady() || EventPoints.Base() == 0 || Memory.ReadInt(EventPoints.Count) <= 0)
            { _settleTicks = 0; return; }
            if (++_settleTicks < 20) return;              // ~1 s of stability

            _installedMap = map;
            _settleTicks = 0;
            Install(spot);
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
            BuildArena(stb, labelCount, tbl);
            if (_arena.Count == 0) { Log("   no spare labels — skipping"); return; }

            // Our entry script is ~1.5 KB and no single spare label is that big (the 300-block runs 650-800 B
            // each). But their code regions TILE the buffer, so a run of adjacent ones can be treated as one
            // arena and written straight through.
            Lab lab = Allocate(stb, Need(BuildFishingBytecode(spot)), out int end);
            if (lab == null)
            {
                Log("   the spare labels cannot hold the fishing script — skipping");
                return;
            }

            // Give the script an id of our own rather than inheriting the label's. Otherwise the town keeps a
            // live entry pointing at our code, and any event of ITS OWN that happens to use that id would run
            // the fishing script — drawing a "!" and offering to fish wherever it fired. Only OUR event point
            // names this id, so nothing else can reach it.
            int codeOff = lab.Off;
            Memory.WriteInt(stb + lab.Entry, FishingLabelId);
            int labelId = FishingLabelId;

            Log($"   script @0x{stb:X}  labels={labelCount}  label {lab.Id} -> {labelId} " +
                $"(code @+0x{codeOff:X}, arena {end - codeOff}B across {SpanCount(codeOff, end)} label(s))");

            WriteScript(stb, codeOff, end, BuildFishingBytecode(spot),
                        $"_LOAD_MAIN_CHARA({FishingModel}) + _LOAD_FISHING_DATA(area={spot.AreaId}, " +
                        $"water={spot.Water}) + stance + bait + fishing");

            _exitPosOperand = _exitRotOperand = -1;
            _exitPosAddr = _exitRotAddr = 0;
            InstallEngineLabel(stb, EventPoints.FishingExitLabel, BuildExitBytecode(spot),
                               $"restore {NormalModel} + re-place player + _EXIT_FISHING   [Circle = leave]");
            InstallEngineLabel(stb, EventPoints.FishingBaitLabel, BuildBaitBytecode(),
                               $"_GOTO_CHANGE_ESA + load the chosen bait   [Square = bait menu]");

            if (!TryCreateEventPoint(spot, labelId, out int slot))
            {
                Log("   NO FREE EVENT POINT SLOT — the trigger could not be created");
                return;
            }
            _slot = slot;
            _slotAddr = EventPoints.Slot(EventPoints.Base(), slot);
            _labelId = labelId;
            _spot = spot;

            PatchFishDepth(spot);

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
            var all = new System.Collections.Generic.List<(int id, int off, int slot)>();
            for (int i = 0; i < labelCount; i++)
            {
                long e = stb + tbl + i * TownScript.LabelStride;
                all.Add((Memory.ReadInt(e), Memory.ReadInt(e + 4), i));
            }
            all.Sort((a, b) => a.off.CompareTo(b.off));

            var sizes = new System.Text.StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                int size = i + 1 < all.Count ? all[i + 1].off - all[i].off : 0;   // 0 = last, unknown end
                bool sys = IsSystemLabel(all[i].id);
                sizes.Append($"{all[i].id}:{(size > 0 ? size.ToString() : "end")}{(sys ? "*" : "")} ");
                if (sys || size <= 0) continue;
                _arena.Add(new Lab
                {
                    Slot = all[i].slot, Id = all[i].id, Off = all[i].off, Size = size,
                    Entry = (int)(tbl + all[i].slot * TownScript.LabelStride),
                });
            }
            Log($"   label regions (* = protected, never hijacked): {sizes}");
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

        private static int SpanCount(int off, int end)
        {
            int n = 0;
            foreach (var l in _arena) if (l.Off >= off && l.Off < end) n++;
            return n;
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

            // Declare our locals. A label's header (the 4 slots after the 8-byte gap) starts with the LOCAL
            // VARIABLE COUNT in its op field — Norune's label 256 says 27, label 134 says 10, label 133 says
            // 1. The labels we hijack declare 0, so a script that touches var0 without raising this would be
            // reaching outside its frame.
            if (w.Locals > 0) Memory.WriteInt(stb + codeOff + 8, w.Locals);

            Memory.WriteBytesBatch(stb + codeOff + TownScript.LabelCodeSkip, bc);
            if (blob.Length > 0) Memory.WriteBytesBatch(stb + blobOff, blob);
            Log($"   wrote {bc.Length}B code + {blob.Length}B strings @+0x{blobOff:X}" +
                (w.Locals > 0 ? $", {w.Locals} local(s)" : "") + $": {what}");
        }

        /// <summary>
        /// Give the town a label the ENGINE asks for by number (133 = quit, 134 = bait). The id is not
        /// negotiable, so if the town has no such label we claim a spare and REWRITE ITS ID.
        /// </summary>
        private static void InstallEngineLabel(long stb, int targetId, StbWriter w, string what)
        {
            Lab lab = Allocate(stb, Need(w), out int end);
            if (lab == null)
            {
                Log($"   NO room for label {targetId} — that fishing button will do nothing");
                return;
            }

            Memory.WriteInt(stb + lab.Entry, targetId);
            Log($"   label {lab.Id} re-numbered to {targetId} (the engine requests it by number)");
            WriteScript(stb, lab.Off, end, w, what);

            // Remember where the exit script's position/rotation operands landed, so they can be kept in step
            // with the player while they fish.
            if (targetId == EventPoints.FishingExitLabel && _exitPosOperand >= 0)
            {
                long code = stb + lab.Off + TownScript.LabelCodeSkip;
                _exitPosAddr = code + _exitPosOperand + 8;   // +8 = the a2 field of the first PushFloat
                _exitRotAddr = code + _exitRotOperand + 8;
                Log($"   exit position operands @0x{_exitPosAddr:X} / rot @0x{_exitRotAddr:X} " +
                    $"— patched live so quitting does not move you");
            }
        }

        /// <summary>
        /// Keep the exit script's <c>_SET_NPC_POS</c> / <c>_SET_NPC_ROT</c> operands equal to where the player
        /// actually is.
        ///
        /// The exit script HAS to re-place the player, because <c>_LOAD_MAIN_CHARA</c> resets the model's
        /// position and you would otherwise come out of fishing falling through the map. But vanilla does not
        /// visibly move you on quit — and you can walk around while fishing, so restoring the ENTRY stance
        /// yanks you back. Rewriting the literals each frame makes the re-place a no-op you cannot see.
        ///
        /// Instructions are 12 bytes {op, a1, a2}; the operand of a PushFloat is its a2, hence the +8 above
        /// and the 12-byte strides here.
        /// </summary>
        /// <summary>
        /// Swing the follow camera around behind the player as fishing begins, so you start out looking at
        /// the water rather than at whatever you happened to walk in facing.
        ///
        /// NOT vanilla behaviour — the stock game leaves the camera alone. This is an added convenience, so
        /// it gets its own switch.
        ///
        /// The camera's yaw is the direction it looks FROM, not the direction it looks toward: writing the
        /// player's yaw straight in put the camera in front of them, staring back. Behind-the-shoulder is
        /// therefore yaw + PI. (<c>_RESET_CAMERA_ANGLE</c> is no help here: it only sets the two globals that
        /// <c>EventMode</c>'s <c>default:</c> branch reads, and fishing exits through <c>case 0xb</c>, which
        /// never reaches them.)
        ///
        /// Writing BOTH the target and current yaw is what <c>SetAngleSoon</c> does, and it snaps instantly;
        /// writing only <see cref="EditLoop.CameraAngle"/> would have it swing round instead.
        /// </summary>
        internal static bool SnapCameraOnStart = true;

        private static void SnapCameraBehindPlayer()
        {
            if (!SnapCameraOnStart) return;

            float behind = GeoramaProbe.ReadYaw() + (float)Math.PI;
            Memory.WriteFloat(EditLoop.MainCamera + EditLoop.CameraAngle, behind);
            Memory.WriteFloat(EditLoop.MainCamera + EditLoop.CameraAngleNow, behind);
            Log($"camera snapped behind the player (yaw {behind:0.###})");
        }

        private static void PatchExitPosition()
        {
            if (_exitPosAddr == 0) return;
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeFishing) return;
            if (!ReadPos(out float x, out float y, out float z)) return;

            Memory.WriteFloat(_exitPosAddr, x);
            Memory.WriteFloat(_exitPosAddr + 12, y);
            Memory.WriteFloat(_exitPosAddr + 24, z);
            Memory.WriteFloat(_exitRotAddr + 12, GeoramaProbe.ReadYaw());   // the middle float is the yaw
        }


        /// <summary>
        /// The script local that <c>_LOAD_SYNC</c> reports into, so the load loop waits exactly as long as the
        /// disc takes — no more, and crucially no less. Index 1, because the bait menu uses var0 for its
        /// result.
        /// </summary>
        private const int GateVar = 1;


        /// <summary>
        /// Label 134 — STOPGAP, not the real bait menu.
        ///
        /// Norune's 134 is 1,656 bytes: it builds an item-select menu from the baits you actually own (item
        /// ids 166-170, 193, 197 appear in its bytecode) and feeds the choice to <c>_SET_FISHING_ESA</c>.
        /// Porting it means relocating its jump targets AND accepting that its message ids would resolve
        /// against a different town's .mes, so the prompts would be wrong text.
        ///
        /// <c>_SET_FISHING_ESA</c> itself takes a single int — the bait item id — so until the real menu is
        /// ported, Square just equips one. <c>FishingLoadEsa</c> returns whatever bait was equipped to your
        /// inventory first, so repeatedly pressing it does not lose items. It does NOT check that you own the
        /// bait, so this hands you one; that is a cheat, and it is why this is a stopgap and not the answer.
        ///
        /// The real menu route is <c>_GOTO_CHANGE_ESA</c> (command 25), which drives the generic use-item
        /// menu — but its first argument must be a STRING (the handler bails unless the stack type is 3), so
        /// it needs a string offset in the town's own .stb.
        /// </summary>
        /// <summary>
        /// Build the bait's model and hang it on the hook.
        ///
        /// <c>_SET_FISHING_ESA</c> loads NOTHING — it only points the hook at item frame 0
        /// (<c>FishingLoadEsa: EsaFrame = itemFrames[0]</c>). The frame has to be built first:
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
        /// This has to be emitted into EVERY script that wants bait, not done once at startup:
        /// <c>EdInitEventParamSimple</c> ZEROES the item-frame table at the start of every event, so by the
        /// time label 134 runs, whatever the entry script loaded is already gone. That is exactly why
        /// pressing Square removed the bait instead of adding it.
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
        /// <c>_GOTO_CHANGE_ESA</c> (command 25) drives the game's own use-item menu: it copies a STATIC bait
        /// list (the template at <c>_820</c> — so we do not have to supply one), calls <c>EdSetUseItem</c>
        /// and sets <c>menu_mode = 9</c>. Its one meaningful argument is a POINTER to a script local, which
        /// the menu writes the chosen item id into. Hence <see cref="StbWriter.PushVarRef"/>.
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

            // The load now waits on the disc rather than on a frame count, so it is as short as it can safely
            // be — vanilla returns you to fishing the instant you pick a bait, and this is the closest we get
            // without the crash risk of guessing.
            EmitBaitLoad(w);

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
        /// Modelled directly on Norune's real call (label 256, +0x0E8E0), which pushes 998 then the area and
        /// six floats and does EXT argc=8. We push negative floats as literals rather than using the negate
        /// op the original happens to use.
        /// </summary>
        private static StbWriter BuildFishingBytecode(Spot s)
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
            if (!s.DiagNoVillagerClear)
            {
                w.PushInt(StbCommands.ClearVillagerBuff); // 38 — paired with _LOAD_VILLAGER on exit
                w.Ext(1);
            }

            // RESET THE FISHING POOL TO ITS BASE *BEFORE* THE MODEL LOAD. This is the Brownboo fix.
            //
            // _LOAD_MAIN_CHARA(turi, flag=1) loads the 1.8 MB model into allocator 0x1d1b360 — the same pool
            // _LOAD_FISHING_DATA uses. Norune loads the model FIRST and clears the event buffer AFTER, which
            // works only because its pool pointer already sits low. Brownboo has more resident event data, so
            // the pointer is high, and model-start + 1.8 MB runs off the end of the pool -> overflow -> crash
            // (confirmed: skipping the model reaches fishing fine, cpoly=4). Resetting the pool to base first
            // makes the model load from the bottom, so everything packs tight from the base and fits.
            w.PushInt(StbCommands.ClearEventBuff);    // 39 — moved up from after the model swap
            w.Ext(1);

            // We do NOT mirror Norune's _NPC_DRAW(0,-1) here (nor _NPC_DRAW(1,-1) on exit), and that is not a
            // shortcut — for the player it is a NO-OP. _NPC_DRAW's per-character draw flags are an array
            // indexed 0..15 for VILLAGERS; the player is id -1, which GetChara resolves to the main-character
            // pointer directly, bypassing that array. The only other thing it writes is DAT_01d3d230, which
            // has ZERO readers in the whole binary (verified by xref). So Norune's hide/show around the model
            // swap is vestigial — it changes nothing the renderer looks at.

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
                w.PushInt(1);                             // flag 1 = load into the fishing pool 0x1d1b360
                w.Ext(4);
                w.Yield();
            }

            w.PushInt(StbCommands.LoadFishingData);   // 998 — NOT 999; the dispatch table is {handler, id}
            w.PushInt(s.AreaId);
            w.PushFloat(s.X1);
            w.PushFloat(s.Z1);
            w.PushFloat(s.X2);
            w.PushFloat(s.Z2);
            w.PushFloat(s.Water);
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
            // _SET_NPC_POS(-1, 40, 0, 96) and _SET_NPC_ROT(-1, 0, 3.14, 0) — where (40, 0, 96) is the
            // part-local position of its own fishing event point. Without it the player keeps whatever
            // position and facing they walked in with, so the cast is aimed at dry land and the engine
            // rejects it. This is the rod "bug".
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
            // Loading it here corrupts the heap. _CLEAR_EVENT_BUFF (which the item load needs) is a bump
            // allocator RESET:
            //
            //     EdEventBuffer = EdVillagerBuffer + n * 0x10;      // pointer back to base
            //
            // and the fields it rewinds (0x1d1b368/36c) belong to the allocator at 0x1d1b360 — the SAME one
            // _LOAD_FISHING_DATA just allocated fishing.pak and the fish out of. So doing the item load after
            // the fishing load rewinds the arena and drops the bait on top of the fishing data. The session
            // still runs, because that memory is already in hand; but the arena is wrecked, and the next thing
            // to allocate from it — area streaming, once you walk far enough — lands in the wreckage.
            //
            // That was the crash-after-fishing-when-you-walk-away. Vanilla starts a session with no bait
            // (FishingInit sets esa_type = -1) and you pick one with Square, so this is also the faithful
            // behaviour, not just the safe one.

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
        /// In fishing mode <c>EdMoveChara</c> runs a <c>chara_fishing</c> state machine that asks for script
        /// labels BY NUMBER when you press a button:
        ///
        /// <code>
        ///   EdPadDown(0x40) -> chara_fishing = 2      // Cross  = cast
        ///   EdPadDown(0x20) -> ScriptLabelRequest = 0x85   // 133 = quit
        ///   EdPadDown(0x80) -> ScriptLabelRequest = 0x86   // 134 = bait menu
        /// </code>
        ///
        /// Norune's script HAS labels 133 and 134. A town that never had fishing does not — so the button
        /// asks for a label that does not exist and nothing happens, which is exactly why the session could
        /// not be exited. We synthesise 133 ourselves; it is tiny.
        ///
        /// The RET matters: we set no return code, so <c>EventMode</c> takes its <c>default:</c> branch and
        /// puts <c>GameMode</c> back to 1 (walking). That is how Norune's exit path ends too.
        /// </summary>
        private static StbWriter BuildExitBytecode(Spot s)
        {
            var w = new StbWriter();
            w.Yield();                                // same rule as the main script — see BuildFishingBytecode
            w.Yield();                                // Norune yields twice here before touching the model

            // Put the ORDINARY Toan back. If we do not, the player walks around town holding a fishing rod
            // with the fishing motion table — every normal animation would then be wrong in the same way the
            // cast was. (Skipped when the entry skipped the swap — there is nothing to undo.)
            if (!s.DiagSkipModel)
            {
                w.PushInt(StbCommands.LoadMainChara);     // 999
                w.PushString(NormalModel);
                w.PushString(NormalModelCfg);
                w.PushInt(0);
                w.Ext(4);
            }

            // RE-PLACE THE PLAYER — but back where they ACTUALLY ARE, not at the entry stance.
            //
            // The re-placement itself is not optional: _LOAD_MAIN_CHARA resets the model's position, and
            // without this you come out of fishing elsewhere on the map, falling. But vanilla does not visibly
            // move you on exit, and snapping to the entry stance does — you can walk around while fishing, so
            // by the time you quit you are rarely where you started.
            //
            // So we emit the position as literals and then REWRITE THOSE OPERANDS every frame while fishing
            // (see PatchExitPosition) with the player's live position and yaw. The script restores exactly
            // where you were standing, and the re-place is invisible.
            if (s.HasStance)
            {
                EmitWorldCoordReset(w);

                w.PushInt(StbCommands.SetNpcPos);
                w.PushInt(-1);
                _exitPosOperand = w.Offset;           // the three floats that follow get patched live
                w.PushFloat(s.StandX); w.PushFloat(s.StandY); w.PushFloat(s.StandZ);
                w.Ext(5);

                w.PushInt(StbCommands.SetNpcRot);
                w.PushInt(-1);
                _exitRotOperand = w.Offset;
                w.PushFloat(0f); w.PushFloat(s.Facing); w.PushFloat(0f);
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
            if (!s.DiagNoVillagerClear)               // nothing was cleared, so nothing to reload
            {
                w.PushInt(StbCommands.LoadVillager);      // 57
                w.Ext(1);
                EmitWaitLoop(w, StbCommands.LoadSync, exitOnNonZero: false);
            }

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
        /// <c>CheckEventPoint</c> opens with <c>if (*piVar5 == 0) return 0;</c> — <b>+0x00 must be non-zero</b>
        /// (every real point in the dumps reads <c>00:1</c>). That is why nothing happened in Queens or
        /// Brownboo even though the install logged success.
        ///
        /// Rather than reverse-engineer the remaining fields (there is a time window at +0x40/+0x44 that
        /// <c>EdCheckTime</c> reads, and more besides), CLONE a working point and override only what we mean
        /// to change. Copying a known-good record is more robust than reconstructing one from a partial map.
        /// </summary>
        private static bool TryCreateEventPoint(Spot s, int labelId, out int slot)
        {
            long arr = EventPoints.Base();
            int n = Memory.ReadInt(EventPoints.Count);

            // A live point to copy the unknown fields from — any occupied slot will do.
            long donor = 0;
            for (int i = 0; i < n; i++)
            {
                long e = EventPoints.Slot(arr, i);
                if (Memory.ReadInt(e + EventPoints.Type) != EventPoints.TypeFree) { donor = e; break; }
            }
            if (donor == 0) { slot = -1; return false; }

            for (int i = 1; i < n; i++)
            {
                long e = EventPoints.Slot(arr, i);
                if (Memory.ReadInt(e + EventPoints.Type) != EventPoints.TypeFree) continue;

                byte[] tmpl = Memory.ReadBytesBatch(donor, EventPoints.Stride);
                Memory.WriteBytesBatch(e, tmpl);          // inherit every field we have not mapped

                Memory.WriteInt(e + EventPoints.Enabled, 1);            // CheckEventPoint bails if this is 0
                Memory.WriteInt(e + EventPoints.MapFlag, 0);            // no "already done" gate

                // THE ONE THAT SILENTLY SWALLOWED THE LAST ATTEMPT. The donor is a door, whose PartIndex is
                // >= 0 — so EdGetEvent tried to resolve a Georama part and either skipped the point or made
                // our world coordinates part-relative. -1 means "free-standing, Position is world space".
                Memory.WriteInt(e + EventPoints.PartIndex, -1);
                Memory.WriteInt(e + EventPoints.ObjectPtr, 0);          // no CMapObject to inherit a position from
                Memory.WriteInt(e + EventPoints.FramePtr, 0);           // no visibility gate

                Memory.WriteInt(e + EventPoints.ItemOrLabel, labelId);  // type 3 -> the SCRIPT LABEL

                // The donor is a door, so we inherited its name — the probe read ours back as 's1901', which
                // is a MAP DESTINATION. Type 3 never jumps, so it is almost certainly inert, but leaving a
                // live map name on a point we are about to fire is asking for the kind of bug that takes a
                // day to find. Blank it.
                Memory.WriteBytesBatch(e + EventPoints.Name, new byte[16]);

                Memory.WriteFloat(e + EventPoints.Position, s.TrigX);
                Memory.WriteFloat(e + EventPoints.Position + 4, s.TrigY);
                Memory.WriteFloat(e + EventPoints.Position + 8, s.TrigZ);
                Memory.WriteFloat(e + EventPoints.Radius, s.Radius);    // a scalar radius, NOT a box

                // Type LAST: it is what marks the slot live. Writing it first would expose a half-built
                // point to a frame of the engine's attention.
                Memory.WriteInt(e + EventPoints.Type, EventPoints.TypeScript);

                slot = i;
                return true;
            }
            slot = -1;
            return false;
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
            // rejected the state — a very different bug. Watch it instead of inferring it.
            int mode = Memory.ReadInt(EditLoop.EventReturnCode);
            if (mode != _lastMode)
            {
                Log($"EventReturnCode {_lastMode} -> {mode}" +
                    (mode == EditLoop.ReturnCodeFishing ? "   (script asked for fishing)" : ""));
                _lastMode = mode;
            }

            // The chain is: script sets ReturnCode=0xB -> EdEventMode returns it -> EventMode does
            // `case 0xb: GameMode = 0x10`. EventMode ONLY runs while GameMode == 0xE (event mode). So the
            // whole question is whether our event ever entered 0xE. Sampling GameMode only when the return
            // code changed was looking at the one instant that cannot answer that — watch every transition.
            int gm = Memory.ReadInt(EditLoop.GameMode);
            if (gm != _lastGameMode)
            {
                Log($"GameMode {_lastGameMode} -> {gm}   {GameModeName(gm)}");
                _lastGameMode = gm;

                if (gm == EditLoop.GameModeFishing && _snapCamera)
                {
                    _snapCamera = false;
                    SnapCameraBehindPlayer();
                }
            }

            PatchExitPosition();
            WatchFishingStart();

            // Did our slot survive? The array is built progressively during load, and something later in the
            // sequence could reclaim it.
            if (_slot < 0 || ++_watchdog < 100) return;   // every ~5 s
            _watchdog = 0;

            int type = Memory.ReadInt(_slotAddr + EventPoints.Type);
            if (type != EventPoints.TypeScript)
                Log($"our event point [{_slot}] is GONE (type is now {type}) — something reclaimed the slot");
        }

        /// <summary>
        /// Notice when a session actually begins, so the camera can be swung behind the player.
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

            if (live && !_fishingWasLive)
            {
                _snapCamera = true;
                // EXPERIMENT: drop every vertical wall from the native cpoly, keeping only the floors/slopes
                // the hook/bobber raycast honours. Tests whether player movement (its own collision system)
                // still keeps you on the boardwalk and whether the bobber/hook behave with walls gone.
                if (FloorsOnlyExperiment) ReplaceWithFloorsOnly();
                // Keep the town's native terrain collision (hook/bobber vs sand/rocks/houses) and APPEND our
                // fish-containment walls on top of it. See InjectCollision.
                else if (InjectFakeCollision && _spot.HasCollision) InjectCollision(_spot);
            }
            _fishingWasLive = live;
        }

        /// <summary>
        /// EXPERIMENT: rewrite the native cpoly to keep ONLY near-horizontal polys (floors + slopes), dropping
        /// every vertical wall. The hypothesis being tested: (a) the hook/bobber only ever land on floor-ish
        /// polys (FishLineStep honours |normal.Y| &gt; 0.2), so walls never mattered to them, and (b) the
        /// player's OWN movement collision — a separate system from cpoly — is what keeps them on the boardwalk
        /// during a session. If both hold, we can throw away ~460 wall polys and free the whole budget.
        ///
        /// Pure runtime memory op: forward-compact the buffer in place (write index never outruns read index)
        /// and lower CPolyNum. Runs once, AFTER PickUpPoly has gathered — so no bearing on the 1024 gather cap.
        /// Reloading the town restores the full native set.
        /// </summary>
        internal static bool FloorsOnlyExperiment = true;

        /// <summary>The floor-ness cutoff: the engine's own raycast (FishLineStep, DAT_2a1a64) counts a poly as
        /// ground when |normal.Y| &gt; 0.2 on the normalised normal. Keeping exactly that set preserves every
        /// poly the hook/bobber can land on and discards only true walls.</summary>
        private const float FloorNormalYMin = 0.2f;

        private static void ReplaceWithFloorsOnly()
        {
            uint p = Memory.ReadUInt(FishingSpot.CPoly) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) { Log("   floors-only: cpoly ptr invalid — skipping"); return; }
            long buf = Memory.ToMmu(p);

            int nativeCount = Memory.ReadInt(FishingSpot.CPolyNum);
            if (nativeCount <= 0 || nativeCount > FishingSpot.CPolyMax)
            { Log($"   floors-only: native count {nativeCount} unusable — skipping"); return; }

            // Capture the FULL gather (floors + walls) at the current cast rect, BEFORE we compact — this is
            // the ground-truth geometry the viewer splits into floor/slope/wall, so widening the rect can be
            // verified. Runs here (not in the probe) because CustomFishingSpot.Tick fires before the probe,
            // so by the time the probe dumps, the walls are already gone.
            DumpFullGather(buf, nativeCount);

            int keep = 0, walls = 0, ladtops = 0;
            for (int i = 0; i < nativeCount; i++)
            {
                long poly = buf + (long)i * 0x50;
                float nx = Memory.ReadFloat(poly + 0x30);
                float ny = Memory.ReadFloat(poly + 0x30 + 4);
                float nz = Memory.ReadFloat(poly + 0x30 + 8);
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl <= 0f || Math.Abs(ny) / nl <= FloorNormalYMin) { walls++; continue; }   // a wall — drop it

                // Also drop floor polys sitting on TOP of the in-water ladders (platforms above the water,
                // not pond floor) — the bobber/hook have no business catching on them. Gated on the poly's
                // LOWEST vertex, so pond floor near a ladder base (low Y) is kept; only the high tops go.
                if (IsLadderTopFloor(poly)) { ladtops++; continue; }

                if (keep != i)
                    Memory.WriteBytesBatch(buf + (long)keep * 0x50, Memory.ReadBytesBatch(poly, 0x50));
                keep++;
            }

            Memory.WriteInt(FishingSpot.CPolyNum, keep);
            Log($"   floors-only: kept {keep} floor/slope polys (dropped {walls} walls + {ladtops} " +
                $"ladder-top floors) — cpoly {nativeCount} → {keep}");
        }

        /// <summary>Brownboo's in-water ladder (s04r*) XZ positions, from gedit/s04/mapinfo.cfg. Used to
        /// reclaim the FLOOR platforms on top of each ladder. Radius/height must match the viewer's van_cut
        /// (tools/brownboo_viewer.py: LAD_POS / LAD_R / LAD_Y).</summary>
        private static readonly (float x, float z)[] BrownbooLadders =
        {
            (0f, 74f), (-57f, 48f), (32f, -67f), (82f, 109f), (62f, -127f), (-55f, -116f), (-91f, 76f),
        };
        private const float LadderRadius  = 45f;   // top platforms lean out up to ~42u from the base position
        private const float LadderTopMinY = 25f;   // a floor poly at/above this height near a ladder is a top

        /// <summary>True if a cpoly triangle is a floor platform on top of one of Brownboo's ladders: its
        /// lowest vertex is above <see cref="LadderTopMinY"/> AND its centre lies within
        /// <see cref="LadderRadius"/> of a ladder. Brownboo-only (the positions are its own).</summary>
        private static bool IsLadderTopFloor(long poly)
        {
            if (_spot.MapNo != 14) return false;

            float y0 = Memory.ReadFloat(poly + 4);
            float y1 = Memory.ReadFloat(poly + 0x10 + 4);
            float y2 = Memory.ReadFloat(poly + 0x20 + 4);
            if (Math.Min(y0, Math.Min(y1, y2)) < LadderTopMinY) return false;

            float cx = (Memory.ReadFloat(poly) + Memory.ReadFloat(poly + 0x10) + Memory.ReadFloat(poly + 0x20)) / 3f;
            float cz = (Memory.ReadFloat(poly + 8) + Memory.ReadFloat(poly + 0x10 + 8) + Memory.ReadFloat(poly + 0x20 + 8)) / 3f;
            foreach (var (lx, lz) in BrownbooLadders)
            {
                float dx = cx - lx, dz = cz - lz;
                if (dx * dx + dz * dz < LadderRadius * LadderRadius) return true;
            }
            return false;
        }

        /// <summary>Where the FULL native gather (floors + walls, pre-removal) is written at the CURRENT cast
        /// rect, for the viewer (tools/brownboo_viewer.py) to split into floor/slope/wall. Overwrites the
        /// stale reference each capture, which is correct — the rect it reflects is whatever is live now.</summary>
        private const string FullGatherCsv = "/Users/thomascantwell/DarkCloud-Enhanced/tools/vanilla_cpoly.csv";

        /// <summary>Dump every cpoly triangle (3 verts + normal) to a CSV. One line per triangle:
        /// v0x,v0y,v0z,v1x,v1y,v1z,v2x,v2y,v2z,nx,ny,nz.</summary>
        private static void DumpFullGather(long buf, int count)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("v0x,v0y,v0z,v1x,v1y,v1z,v2x,v2y,v2z,nx,ny,nz");
            for (int i = 0; i < count; i++)
            {
                long poly = buf + (long)i * 0x50;
                for (int v = 0; v < 3; v++)
                {
                    sb.Append(Memory.ReadFloat(poly + v * 0x10).ToString("0.###")).Append(',');
                    sb.Append(Memory.ReadFloat(poly + v * 0x10 + 4).ToString("0.###")).Append(',');
                    sb.Append(Memory.ReadFloat(poly + v * 0x10 + 8).ToString("0.###")).Append(',');
                }
                sb.Append(Memory.ReadFloat(poly + 0x30).ToString("0.###")).Append(',');
                sb.Append(Memory.ReadFloat(poly + 0x30 + 4).ToString("0.###")).Append(',');
                sb.Append(Memory.ReadFloat(poly + 0x30 + 8).ToString("0.###"));
                sb.AppendLine();
            }
            try
            {
                System.IO.File.WriteAllText(FullGatherCsv, sb.ToString());
                Log($"   full-gather: wrote {count} polys (floors+walls) -> {FullGatherCsv}");
            }
            catch (Exception e)
            {
                Log($"   full-gather: write FAILED: {e.Message}");
            }
        }

        /// <summary>Turn OFF to leave the native cpoly untouched (no fish walls). See InjectCollision.</summary>
        internal static bool InjectFakeCollision = false;   // OFF: leave native cpoly untouched to read the vanilla count

        private static bool _fishingWasLive;

        /// <summary>
        /// APPEND fabricated fish-containment walls to the town's native collision, without disturbing it.
        ///
        /// The native cpoly (built by CEditGround::PickUpPoly at spot load) is the town's REAL terrain — the
        /// sloped sand bottom, the shore banks, the rocks and the houses — world-placed with true surface
        /// normals. FishLineStep raycasts the hook and bobber straight DOWN against it and only honours
        /// floor-ish polys (|normal.Y| > 0.2), so that native mesh is exactly what stops the hook/bobber
        /// passing through terrain and what makes a cast onto a rock/land rest above WaterLevel+5 (which
        /// FishingCheckUkiHook then rejects). We must NOT overwrite it — an earlier version did, which is
        /// why the hook/bobber clipped through everything and casts onto rocks succeeded.
        ///
        /// Our walls are purely for the FISH (a separate collision path): vertical quads (normal.Y = 0), so
        /// they are invisible to the hook/bobber raycast and only fence the fish. We write them into the
        /// slots ABOVE the native count and bump CPolyNum. PickUpPoly hangs the game above 0x400, so we cap.
        ///
        /// A CCPoly is 0x50 bytes: three verts (+0x00/+0x10/+0x20, x/y/z) and a normal (+0x30). We copy an
        /// existing poly as a template so its flag/padding bytes stay valid, and rewrite only verts + normal.
        /// </summary>
        private static void InjectCollision(Spot s)
        {
            uint p = Memory.ReadUInt(FishingSpot.CPoly) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long buf = Memory.ToMmu(p);

            int nativeCount = Memory.ReadInt(FishingSpot.CPolyNum);
            if (nativeCount <= 0 || nativeCount > FishingSpot.CPolyMax) { Log($"   collision: native count {nativeCount} unusable — skipping"); return; }

            byte[] template = Memory.ReadBytesBatch(buf, 0x50);   // a real poly, for its non-vertex fields
            float depth = s.HasFishDepth ? s.FishDepth : 12f;
            float yLow  = s.Water - depth - 4f;                   // a bit below the fish
            float yHigh = s.Water + 3f;                           // a bit above the surface

            var polys = new System.Collections.Generic.List<byte[]>();

            // (1) Fish-containment walls along the shore perimeter. The native sand shore is a sloped FLOOR
            //     (no vertical barrier), and fish swim at a fixed depth, so they need a real wall to fence
            //     them in. Vertical (normal.Y = 0) → invisible to the hook/bobber, which is what we want.
            AddWallLoop(polys, template, s.Perimeter, yLow, yHigh, inward: true);

            // (2) The rocks' EXACT triangles, decoded from the town's visual mesh (they have no native
            //     collision — they're decorative). Real surface normals: top faces (normal.Y > 0.2) stop
            //     the hook/bobber and reject casts onto the rock; side faces fence the fish. This replaces
            //     the crude convex-hull walls with the true geometry.
            int meshTris = AddMeshTriangles(polys, template, s.MapNo);

            int total = nativeCount + polys.Count;
            if (total > FishingSpot.CPolyMax)
            {
                Log($"   collision: native {nativeCount} + {polys.Count} added = {total} > {FishingSpot.CPolyMax} cap — skipping");
                return;
            }

            for (int i = 0; i < polys.Count; i++)
                Memory.WriteBytesBatch(buf + (long)(nativeCount + i) * 0x50, polys[i]);
            Memory.WriteInt(FishingSpot.CPolyNum, total);
            Log($"   collision: {nativeCount} native + {polys.Count - meshTris} shore walls + {meshTris} exact rock tris → {total}");
        }

        /// <summary>Emit two triangles per edge of a closed x,z loop — a vertical wall from yLow to yHigh with
        /// a horizontal normal facing into (inward) or away from (obstacle) the loop's centre.</summary>
        private static void AddWallLoop(System.Collections.Generic.List<byte[]> outp, byte[] template,
                                        float[] loop, float yLow, float yHigh, bool inward)
        {
            int n = loop.Length / 2;
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++) { cx += loop[i * 2]; cz += loop[i * 2 + 1]; }
            cx /= n; cz /= n;

            for (int i = 0; i < n; i++)
            {
                float ax = loop[i * 2], az = loop[i * 2 + 1];
                int j = (i + 1) % n;
                float bx = loop[j * 2], bz = loop[j * 2 + 1];

                // normal perpendicular to the edge, in XZ; flip so it points toward (inward) / away (obstacle)
                float nx = -(bz - az), nz = bx - ax;
                float len = (float)Math.Sqrt(nx * nx + nz * nz); if (len < 1e-3f) continue;
                nx /= len; nz /= len;
                float mx = (ax + bx) * 0.5f, mz = (az + bz) * 0.5f;
                bool pointsToCentre = (nx * (cx - mx) + nz * (cz - mz)) > 0;
                if (pointsToCentre != inward) { nx = -nx; nz = -nz; }

                outp.Add(MakeTri(template, ax, yLow, az, bx, yLow, bz, bx, yHigh, bz, nx, nz));
                outp.Add(MakeTri(template, ax, yLow, az, bx, yHigh, bz, ax, yHigh, az, nx, nz));
            }
        }

        private static byte[] MakeTri(byte[] template,
                                      float x0, float y0, float z0, float x1, float y1, float z1,
                                      float x2, float y2, float z2, float nx, float nz)
        {
            byte[] q = (byte[])template.Clone();
            PutVec(q, 0x00, x0, y0, z0);
            PutVec(q, 0x10, x1, y1, z1);
            PutVec(q, 0x20, x2, y2, z2);
            PutVec(q, 0x30, nx, 0f, nz);   // horizontal normal
            return q;
        }

        private static string MeshCollisionFile(int mapNo) =>
            Path.Combine(AppContext.BaseDirectory, "Resources", "FishingCollision", $"brownboo_{mapNo}.bin");

        /// <summary>Append the spot's EXACT mesh triangles (decoded offline from the town's visual mesh) to
        /// the poly list, each with a real plane normal so the hook/bobber rest on up-facing faces and the
        /// fish are stopped by side faces. Returns the number of triangles added (0 if no data file).</summary>
        private static int AddMeshTriangles(System.Collections.Generic.List<byte[]> outp, byte[] template, int mapNo)
        {
            string path = MeshCollisionFile(mapNo);
            if (!File.Exists(path)) return 0;

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception e) { Log($"   mesh collision: read failed ({e.Message})"); return 0; }

            // Header: 'DCFC', uint version, uint mapNo, uint triCount; then triCount * 9 floats (3 verts).
            if (data.Length < 16 || data[0] != (byte)'D' || data[1] != (byte)'C' || data[2] != (byte)'F' || data[3] != (byte)'C')
            { Log("   mesh collision: bad magic"); return 0; }
            int triCount = BitConverter.ToInt32(data, 12);
            int need = 16 + triCount * 9 * 4;
            if (triCount < 0 || data.Length < need) { Log($"   mesh collision: truncated ({data.Length} < {need})"); return 0; }

            float F(int i) => BitConverter.ToSingle(data, i);
            int p = 16, added = 0;
            for (int t = 0; t < triCount; t++, p += 36)
            {
                float ax = F(p),      ay = F(p + 4),  az = F(p + 8);
                float bx = F(p + 12), by = F(p + 16), bz = F(p + 20);
                float cx = F(p + 24), cy = F(p + 28), cz = F(p + 32);

                // plane normal = (b-a) x (c-a), normalized
                float ux = bx - ax, uy = by - ay, uz = bz - az;
                float vx = cx - ax, vy = cy - ay, vz = cz - az;
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6f) continue;
                nx /= len; ny /= len; nz /= len;

                byte[] q = (byte[])template.Clone();
                PutVec(q, 0x00, ax, ay, az);
                PutVec(q, 0x10, bx, by, bz);
                PutVec(q, 0x20, cx, cy, cz);
                PutVec(q, 0x30, nx, ny, nz);
                outp.Add(q);
                added++;
            }
            return added;
        }

        private static void PutVec(byte[] b, int off, float x, float y, float z)
        {
            Array.Copy(BitConverter.GetBytes(x), 0, b, off + 0, 4);
            Array.Copy(BitConverter.GetBytes(y), 0, b, off + 4, 4);
            Array.Copy(BitConverter.GetBytes(z), 0, b, off + 8, 4);
        }

        private static bool ReadPos(out float x, out float y, out float z)
        {
            x = y = z = 0f;
            uint p = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return false;

            long c = Memory.ToMmu(p) + EditLoop.CharaPosition;
            x = Memory.ReadFloat(c);
            y = Memory.ReadFloat(c + 4);
            z = Memory.ReadFloat(c + 8);
            return true;
        }

        private static void DumpSlot(string tag, long e)
        {
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

    /// <summary>The STB command ids we use. Confirmed from the dispatch table — whose 8-byte entries are
    /// <c>{handler, id}</c>, NOT <c>{id, handler}</c>. Reading them the other way round shifts every command
    /// by one and turns <c>_LOAD_FISHING_DATA</c> into <c>_LOAD_MAIN_CHARA</c>.</summary>
    internal static class StbCommands
    {
        internal const int LoadFishingData = 998;   // (area, x1, z1, x2, z2, water, ground)
        internal const int GotoFishing     = 997;   // ()
        internal const int InitFish        = 996;   // (x1, z1, x2, z2)
        internal const int ExitFishing     = 995;   // ()
        internal const int SetFishingEsa   = 994;   // ()

        internal const int LoadMainChara  = 999;    // (chrPath, cfgName, flag) — swaps the player's model
        internal const int FadeIn        = 500;     // (frames) — 500 is FADE_IN, not FADE_OUT
        internal const int SetWorldCoord = 7;       // (x, y, z, rx, ry, rz)
        internal const int SetNpcPos     = 137;     // (charaId, x, y, z)   charaId -1 = the player
        internal const int SetNpcRot     = 138;     // (charaId, rx, ry, rz)
        internal const int NpcDraw       = 140;     // (flag, charaId)

        // The bait model pipeline. _SET_FISHING_ESA only points the hook at ITEM FRAME 0 — it does not load
        // anything. The frame has to be built first, and _LOAD_ITEM_FILE is a BACKGROUND (async) read.
        internal const int LoadItemFile     = 49;   // (itemId) — starts an async load of the item's chr + img
        internal const int LoadItem         = 50;   // (0) — builds item frame 0 from the loaded files
        internal const int ClearEventBuff   = 39;   // ()
        internal const int ActiveFileBuffer = 44;   // (a, b)

        /// <summary>
        /// (&amp;out) — out = non-zero while ANY background disc read is still in flight.
        ///
        /// This is the load-complete poll, and it existed all along: <c>ReadBGSync</c> pumps the reader and
        /// scans <c>bg_read_info</c> for a slot that is queued but not yet complete. Non-blocking, so a script
        /// loops on it. Norune's mystery <c>call_func 400</c> is nothing more than
        /// <c>while (_LOAD_SYNC(&amp;v)) YIELD;</c>
        ///
        /// I previously concluded no such command existed, having grepped the command names for CHECK / READ /
        /// BG / WAIT / FILE — none of which match "_LOAD_SYNC".
        /// </summary>
        internal const int LoadSync = 34;

        internal const int FadeOut            = 501; // (frames) — 501 is FADE_OUT; 500 is FADE_IN
        internal const int ClearVillagerBuff  = 38;  // ()

        /// <summary>() — rewinds the villager buffer and reloads every NPC (and its textures) for the current
        /// map from disc. Reads its list from globals, no args. Used on fishing exit to un-garble whatever
        /// villager texture block the session's model/bait loads overwrote.</summary>
        internal const int LoadVillager = 57;

        /// <summary>(&amp;out) — out = non-zero while a fade is still in progress. Same shape as
        /// <see cref="LoadSync"/>: poll it in a YIELD loop instead of counting frames.</summary>
        internal const int CheckFade = 502;

        /// <summary>() — raises the "!" prompt for this frame. It is a PER-FRAME flag (EdEventInit clears it,
        /// the ladder code sets it the same way), so it has to be re-asserted every frame it should show.</summary>
        internal const int DrawExclamationMark = 10;

        /// <summary>(&amp;out) — out = the buttons pressed this frame (after exch_ok_cancel).</summary>
        internal const int GetPadDown = 1;

        /// <summary>
        /// X (Cross) AS A SCRIPT SEES IT — 0x20, not 0x40.
        ///
        /// <c>EdMoveChara</c> tests the raw pad with <c>PadDown(0x40)</c> for confirm, so 0x40 is Cross in
        /// engine code. But <c>_GET_PADDOWN</c> pipes the pad through <c>exch_ok_cancel</c> first, which
        /// SWAPS bits 0x20 and 0x40:
        ///
        /// <code>
        ///   v = pad &amp; ~0x60;
        ///   if (pad &amp; 0x20) v |= 0x40;
        ///   if (pad &amp; 0x40) v |= 0x20;
        /// </code>
        ///
        /// So a script testing 0x40 is testing CIRCLE. That is why the fishing prompt answered to Circle.
        /// </summary>
        internal const int PadCross = 0x20;

        /// <summary>(&amp;outVar) — opens the game's native bait menu (menu_mode 9) over a static bait list,
        /// and writes the chosen item id back through the pointer. The handler REFUSES unless arg1's stack
        /// type is 3 (a pointer), so it must be pushed with PushVarRef.</summary>
        internal const int GotoChangeEsa = 25;
    }

    /// <summary>Emit STB VM bytecode: 12-byte instructions <c>{u32 op, u32 a1, u32 a2}</c>.</summary>
    internal sealed class StbWriter
    {
        private const int OpPush  = 3;
        private const int OpExt   = 21;
        private const int OpRet   = 15;
        private const int OpYield = 23;

        private const int TypeInt    = 1;
        private const int TypeFloat  = 2;
        private const int TypeString = 3;

        private readonly System.Collections.Generic.List<byte> _b = new System.Collections.Generic.List<byte>();
        private readonly System.Collections.Generic.List<(string Text, int PatchAt)> _strs =
            new System.Collections.Generic.List<(string, int)>();

        private const int OpVarValue = 1;   // push the VALUE of local var a1
        private const int OpVarRef   = 2;   // push a POINTER to local var a1 (stack type 3)

        /// <summary>
        /// Variable ADDRESSING MODE, and it lives in <c>a2</c> — not <c>a1</c>, which is the variable index.
        ///
        /// <c>exe()</c> case 1/2 switch on <c>a2</c>: 1 = direct (<c>vars[a1]</c>), and 2/4/8/0x10/0x20 are
        /// indirect/array forms that pop an index first. Emitting <c>a2 = 0</c> matches NOTHING, so the
        /// instruction pushes nothing at all — the stack then runs short, EXT reads garbage as the command
        /// id, and the VM derails. That is exactly what froze the game on the bait menu.
        /// </summary>
        private const int VarModeDirect = 1;

        internal void PushInt(int v)     => Emit(OpPush, TypeInt, unchecked((uint)v));
        internal void PushFloat(float v) => Emit(OpPush, TypeFloat, BitConverter.ToUInt32(BitConverter.GetBytes(v), 0));

        /// <summary>Push local variable <paramref name="idx"/>'s value.</summary>
        internal void PushVar(int idx) => Emit(OpVarValue, (uint)idx, VarModeDirect);

        /// <summary>
        /// Push a POINTER to local variable <paramref name="idx"/> — an OUT parameter.
        ///
        /// This is how <c>_GOTO_CHANGE_ESA</c> hands back the bait you picked: its handler takes
        /// <c>p_use_item = arg1.value</c> (and refuses unless <c>arg1.type == 3</c>), opens the menu, and the
        /// menu writes the chosen item id through that pointer. So stack type 3 is "pointer", not "string" —
        /// a string push is just a pointer into the .stb, which is why <see cref="PushString"/> shares it.
        /// </summary>
        internal void PushVarRef(int idx) => Emit(OpVarRef, (uint)idx, VarModeDirect);

        /// <summary>Highest local variable index used, or -1. A label's header declares how many locals it
        /// has (header slot 0's op field), and the VM reserves that many.</summary>
        internal int Locals { get; private set; }

        internal void UseLocals(int n) { if (n > Locals) Locals = n; }

        /// <summary>Byte offset of the next instruction — used to find an operand again so the mod can patch
        /// it live (see the exit script's position, which is rewritten every frame while fishing).</summary>
        internal int Offset => _b.Count;

        /// <summary>
        /// Push a string. The operand is NOT a file offset — it is an offset relative to the script's CODE
        /// BASE (the u32 at header +0x08). Norune's model swap reads `a2 = 0xED18` and the string really
        /// lives at file 0xEE00, and 0xEE00 - 0xED18 = 0xE8, which is exactly its codeBase. That also matches
        /// <c>load__10CRunScript</c>, which caches <c>base + *(base + 8)</c>.
        ///
        /// The offset cannot be known until the string is placed, so this emits a placeholder and remembers
        /// where to patch it. Call <see cref="EmitStrings"/> once the layout is decided.
        /// </summary>
        internal void PushString(string text)
        {
            _strs.Add((text, _b.Count + 8));   // the a2 field of the instruction we are about to emit
            Emit(OpPush, TypeString, 0);
        }

        /// <summary>
        /// Lay the pushed strings out at <paramref name="blobOffset"/> (an offset within the .stb buffer),
        /// patch every placeholder, and return the bytes to write there. Call AFTER the bytecode is complete;
        /// <see cref="ToArray"/> then returns the patched code.
        /// </summary>
        internal byte[] EmitStrings(int blobOffset, int codeBase)
        {
            var blob = new System.Collections.Generic.List<byte>();
            foreach (var (text, patchAt) in _strs)
            {
                byte[] a2 = BitConverter.GetBytes(blobOffset + blob.Count - codeBase);
                for (int i = 0; i < 4; i++) _b[patchAt + i] = a2[i];
                blob.AddRange(System.Text.Encoding.ASCII.GetBytes(text));
                blob.Add(0);
            }
            return blob.ToArray();
        }

        internal bool HasStrings => _strs.Count > 0;

        /// <summary>Bytes the string blob will occupy (each string is NUL-terminated). Needed to size an
        /// allocation BEFORE the layout is decided.</summary>
        internal int StringBytes
        {
            get
            {
                int n = 0;
                foreach (var (text, _) in _strs) n += text.Length + 1;
                return n;
            }
        }

        /// <summary>Call. <paramref name="stackEntries"/> counts the command id as well as the arguments.</summary>
        internal void Ext(int stackEntries) => Emit(OpExt, (uint)stackEntries, 0);

        internal void Ret() => Emit(OpRet, 0, 0);

        /// <summary>Suspend until the next frame. A script that never yields is run to completion inside
        /// <c>EdEventInit</c> and demoted to a "simple event" — it never becomes a real event, so its return
        /// code is never acted on. See <c>BuildFishingBytecode</c>.</summary>
        internal void Yield() => Emit(OpYield, 0, 0);

        private const int OpJmp     = 16;   // pc = codeBase + a1
        private const int OpBrFalse = 17;   // pops; branches if false
        private const int OpBrTrue  = 18;   // pops; branches if true

        private readonly System.Collections.Generic.List<int> _marks = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<(int PatchAt, int Mark)> _jumps =
            new System.Collections.Generic.List<(int, int)>();

        /// <summary>Remember this spot as a jump target. Like strings, a jump's operand is an offset from the
        /// script's CODE BASE, so it cannot be resolved until we know where the script will be written.</summary>
        internal int Mark()
        {
            _marks.Add(_b.Count);
            return _marks.Count - 1;
        }

        /// <summary>Reserve a mark for a spot not emitted yet (a forward branch); fix it with
        /// <see cref="PlaceMark"/>.</summary>
        internal int MarkForward()
        {
            _marks.Add(-1);
            return _marks.Count - 1;
        }

        internal void PlaceMark(int mark) => _marks[mark] = _b.Count;

        private const int OpAnd = 24;

        /// <summary>Pop two, push (a &amp; b). Used to test a single button out of the pad bitmask.</summary>
        internal void And() => Emit(OpAnd, 0, 0);

        internal void Jmp(int mark)     => EmitJump(OpJmp, mark);
        internal void BrTrue(int mark)  => EmitJump(OpBrTrue, mark);
        internal void BrFalse(int mark) => EmitJump(OpBrFalse, mark);

        private void EmitJump(int op, int mark)
        {
            _jumps.Add((_b.Count + 4, mark));      // a1 is the operand for jumps, at instruction + 4
            Emit(op, 0, 0);
        }

        /// <summary>Resolve jump targets once the script's position is known.</summary>
        internal void EmitJumps(int scriptOffset, int codeBase)
        {
            foreach (var (patchAt, mark) in _jumps)
            {
                byte[] a1 = BitConverter.GetBytes(scriptOffset + _marks[mark] - codeBase);
                for (int i = 0; i < 4; i++) _b[patchAt + i] = a1[i];
            }
        }

        private void Emit(int op, uint a1, uint a2)
        {
            _b.AddRange(BitConverter.GetBytes(op));
            _b.AddRange(BitConverter.GetBytes(a1));
            _b.AddRange(BitConverter.GetBytes(a2));
        }

        internal byte[] ToArray() => _b.ToArray();
    }
}
