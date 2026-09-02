using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The custom fishing spot DEFINITIONS: the Spot record and the three placed spots (Queens bank + canal
    /// floor, Brownboo, Yellow Drops) with their rects, water levels, stances, depths and camera heights.
    /// Pure data — CustomFishingSpot installs and drives them.
    /// </summary>
    internal static class FishingSpots
    {
        /// <summary>A spot to install. Rect corners and the water plane come from the town's own
        /// <c>WATER_SURFACE</c> (see the retired GeoramaProbe's dump — git); the trigger box just has to be somewhere the
        /// player will walk.</summary>
        internal readonly struct Spot
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

            /// <summary>Stretch the fishing line while fishing here: <c>distp</c> (the Verlet rope's per-segment
            /// rest length) is multiplied by this, so the whole line reaches farther down. For a spot whose
            /// water sits low under the rod (Queens canal, tide 31). NaN/0 = vanilla length.</summary>
            internal readonly float LineScale;
            internal bool HasLineScale => LineScale > 0f;

            /// <summary>DIAGNOSTIC: skip the turi model swap. Proven the model load (not the rect, not pool
            /// memory) is what crashes Brownboo — with the swap skipped it reaches fishing mode.</summary>
            internal readonly bool DiagSkipModel;

            /// <summary>The stb script label this spot's BAKED trigger names (IsoPatcher.BuildFishingFunc). A
            /// town can have several spots, each on its own label + baked sign part — e.g. Queens has the
            /// north-bank sign (400) and the canal-floor sign (401), which fish the same area from different
            /// stances. Defaults to the primary fishing label 400.</summary>
            internal readonly int LabelId;

            /// <summary>Camera height while fishing here. Vanilla hard-codes 40 — a raised, look-down-into-the-
            /// water angle that suits casting from a bank. A spot where the player stands IN the water (the
            /// Queens canal floor at low tide) wants the ordinary town height (5) instead, because the downward
            /// view is counterproductive there. Fed to the patched SetHeight site via
            /// <see cref="CodeCaves.Mailbox.FishCamHeight"/> (IsoPatcher.PatchFishingCameraHeight).</summary>
            internal readonly float CameraHeight;

            internal Spot(int mapNo, string name, int areaId,
                          float x1, float z1, float x2, float z2, float water, float ground,
                          float tx, float ty, float tz, float radius,
                          float sx = float.NaN, float sy = float.NaN, float sz = float.NaN,
                          float facing = float.NaN, bool diagSkipModel = false,
                          float fx1 = float.NaN, float fz1 = float.NaN, float fx2 = float.NaN,
                          float fz2 = float.NaN, float fishDepth = float.NaN,
                          float lineScale = float.NaN, int labelId = FishingLabelId,
                          float cameraHeight = VanillaFishCamHeight)
            {
                CameraHeight = cameraHeight;
                MapNo = mapNo; Name = name; AreaId = areaId;
                X1 = x1; Z1 = z1; X2 = x2; Z2 = z2; Water = water; Ground = ground;
                TrigX = tx; TrigY = ty; TrigZ = tz; Radius = radius;
                StandX = sx; StandY = sy; StandZ = sz; Facing = facing;
                DiagSkipModel = diagSkipModel; LabelId = labelId;
                FishX1 = fx1; FishZ1 = fz1; FishX2 = fx2; FishZ2 = fz2; FishDepth = fishDepth;
                LineScale = lineScale;
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
        internal static readonly Spot[] Spots =
        {
            // Queens: the canal (static WATER e03c01/c02/c08), surface at Y=31. Trigger + fishing sign on the
            // north bank at (250,70,-70) — bank collision Y=70 confirmed (georama_collision), water is just
            // south (canal Z[-50,52]); stance faces +Z (south) across the canal. Radius 10 = tight "!" bubble.
            // ⚠ RECT is 1140x250 (viewer-matched, spans both bridges). This is FAR over the ~200x200 the
            // poly-gather (PickUpPoly, hard 1024 cap, no bounds check -> stack smash) is safe at. Watch the
            // "FISHING SPOT LOADED" cpoly count (flip Diagnostics on); if it nears 1024, decouple — keep a
            // small cast/gather rect here and move the big roam box to the FishX1..FishZ2 params.
            new Spot(2, "Queens canal", 6,      // area 6 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — 100% Bobo
                     // ⚠ This rect is ALSO the fishing-session poly GATHER (PickUpPoly) — the player walks the
                     // canal/bridges on that gathered cpoly during a Queens session, so it must span the bank
                     // and bridge approaches (z -100..150), NOT just the water. A cast that slips past a 90°
                     // wall is retracted by the native height auto-uncast (PatchFishingUncastGate), not by
                     // shrinking this rect — an earlier z±49 tightening deleted the bridge floors and blocked
                     // crossing them in fishing mode.
                     -240f, -100f, 900f, 150f, water: 31f, ground: 10f,
                     tx: 250f, ty: 70f, tz: -70f, radius: 10f,
                     sx: 250f, sy: 70f, sz: -70f, facing: 0f,            // stance: face +Z (south) toward the water
                     // ⚠ lineScale here is SUPERSEDED for Queens — LineConfigSplit resolves the SPLIT line from
                     // the TIDE: medium/high = above x1.35 aerial + distpBelow 1.35 (the hang the retired
                     // point-20 anchor @1.35x used to produce); low tide = the canal-floor spot below. The
                     // bobber anchor is FIXED at point[18] (A=18 baked into the ISO split caves). Historical
                     // note: 1.25x whole-line with anchor 18 gave hook ~WaterLevel-10.4, bait -13.4, and
                     // fishDepth 14.08 held the vanilla fish-to-bait gap (~0.67) → bites. fishDepth remains
                     // PENDING a logged hook-depth capture at this bank spot (fishDepth ≈ hang + HookBodyDrop).
                     lineScale: 1.25f, fishDepth: 14.08f),

            // Queens canal FLOOR (low tide) — the canal-floor sign (its own baked part `kanbanc` + label 401).
            // Its OWN per-sign script from the shared BuildFishingBytecode builder: same fish area (6) and same
            // tide-driven water as the north-bank spot, but with the canal-floor STANCE baked in, so triggering
            // it fishes from the floor instead of teleporting to the bank. Both re-bake together on a tide change.
            // ⚠ stance/depth/facing are initial guesses — tune in-game against the exposed low-tide floor.
            new Spot(2, "Queens canal floor", 6,
                     -240f, -100f, 900f, 150f, water: 8f, ground: 0f,   // full gather rect (footing) — see the bank spot's rect note
                     tx: 794f, ty: 0f, tz: 0f, radius: 10f,
                     // stand on the canal floor and FACE EAST (yaw pi/2 -> forward (sin,cos)=(1,0)=+X) toward
                     // the canal sign at x=800. Water tracks the tide like the north spot (both in sync), and at
                     // low tide it is low (8), so fish sit just above the floor, not up
                     // at the flooded-tide surface.
                     sx: 794f, sy: 0f, sz: 0f, facing: 1.5708f,
                     // This spot is only reachable at LOW tide, where the hook rests at QueensFishDepthLow below
                     // the surface. fishDepth matches it so the fish sit exactly at the hook (this spot's
                     // empirically confirmed bite geometry; keep the two in lockstep when retuning).
                     fishDepth: QueensFishDepthLow, labelId: 401,  // its own label -> deterministic canal stance
                     // standing IN the canal: drop the raised fishing angle (40) — full town height (5) felt
                     // too cramped by eye, 20 is the tuned balance.
                     cameraHeight: CanalWadingCamHeight),

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
            // this cast/gather rect. Mirrored in tools/brownboo/brownboo_viewer.py (RECT_*).
            new Spot(14, "Brownboo lake", 5,    // area 5 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — Piccoly/Negie/Gummy + Garayan
                     -250f, -240f, 250f, 240f, water: 0f, ground: -15f,   // ±250 W/E, ±240 N/S, centre (0,0)
                     // trigger + stance just south of the sign (212,-53); face NORTH (yaw pi = -Z) toward the sign (212,-61)
                     tx: 212f, ty: 12f, tz: -53f, radius: InteractRadius,
                     sx: 212f, sy: 10f, sz: -53f, facing: 3.14159f,
                     // The bobber anchor is FIXED at point[18] (A=18 baked into the ISO split caves) —
                     // hook depth is pure distpBelow data, no anchor moves.
                     fishDepth: 6f        // ONE KNOB: fish at WaterLevel-6 (CFrame translation Y @ fish+0x1264, the
                                          // authoritative depth) AND the hook rests there too — LineConfigSplit
                                          // derives distpBelow = (6−HookBodyDrop)/5 (fish-at-hook bite geometry).
                                          // All data-only — patching hot fishing code crashes PCSX2 (recompiler).
                                          // Aerial line (cast reach) = distpAbove, independent since the split.
                     // BISECT RESULT (2026-07-23): stilts garbage is NOT the model load and NOT the buffer
                     // clears (both skipped -> still garbage). Clobber is intrinsic to entering fishing mode
                     // (fishing-init / HUD texture setup evicting the stilts' GS block).
                     // Shallow fishing: the RESTING hook follows the fishing-line physics, NOT the rod animation
                     // (in the waiting state it is not pinned to the rod bone). Its depth = the below-bobber rest
                     // length distpBelow (mailbox @0x01F10048, read by the ISO split caves at fixed anchor A=18;
                     // IsoPatcher.PatchFishLineSplit). See game_data/docs/fishing-line-split-and-cast-feasibility.md.
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
            // MOVED 2026-08-30 to the WEST BANK bulge edge (bank top y23, bulge peak z~102; assumes the
            // west-bank ground bake — tools/yellowdrops/yellowdrops_westbank_data.py WEST_BULGE). Player stands at the edge
            // facing WEST (forward = (sin yaw, cos yaw) = (-1,0) -> yaw -pi/2); the rect is the open water
            // pocket west of the bulged edge (one mid pillar at x -590..-550, z 50..177 — casts just land
            // around it). Old spot: rect (-609,-444,-409,-244), trig (-575,9,-286), stance (-582.9,9.6,-276.8) yaw 2.31.
            new Spot(23, "Yellow Drops liquid", 7,  // area 7 = DEDICATED custom area, baked into FishingLoadFish (IsoPatcher) — Tarton/Nonky/Negie/Bon
                     -692f, -156f, -378f, 270f, water: 5.25f, ground: -15f,   // user-drawn rect; water = raised surface 4.25 + the spot's usual +1
                     tx: -465f, ty: 30f, tz: 40f, radius: InteractRadius,
                     sx: -468f, sy: 30f, sz: 40f, facing: -1.5708f,
                     fishDepth: 8f,
                     cameraHeight: 30f),   // lower than the vanilla 40: the bank sits high (y30) over
                                           // the water (y1), so the stock look-down angle felt steep   // ONE KNOB: fish at WaterLevel-8 (was vanilla 12) AND the hook rests there —
                                       // LineConfigSplit derives distpBelow = (8−HookBodyDrop)/5 ≈ 0.906 (fish-at-hook)
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

        /// <summary>The vanilla fishing camera height (the literal <c>SetHeight(40.0)</c> in EdMoveChara that
        /// IsoPatcher redirects to a data word). Default for every spot that casts from a bank.</summary>
        internal const float VanillaFishCamHeight = 40f;
        /// <summary>Canal-floor fishing camera height. The full town walking height (5) was tried and felt
        /// too low/cramped while wading; 20 struck the right balance — still much lower than the vanilla
        /// look-down angle (40), but with enough height to see around while standing in the water.</summary>
        internal const float CanalWadingCamHeight = 20f;

        internal const float QueensFishDepthLow  = 7.0f;   // low tide: FISH depth below the surface (user-tuned); hook rests there too (fish-at-hook bite geometry)

        internal static bool TryGetSpot(int map, out Spot spot)
        {
            foreach (var s in Spots)
                if (s.MapNo == map) { spot = s; return true; }
            spot = default;
            return false;
        }
    }
}
