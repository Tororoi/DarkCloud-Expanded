namespace Dark_Cloud_Improved_Version
{
    /// <summary>The baked town placements: each custom fishing town's sign (scene / mapinfo file, anchor part, world position and
    /// yaw) and its trigger's sign-local offset, the canal ladder (donor, climb points, rungs) and the tide-evict dock spawn.
    /// Read by IsoPatcher.ApplySignPatch, SceneBaker (func-data entries), StbLabelBaker (the dock-spawn script) and CanalLadderCarve.</summary>
    internal static class SignPlacements
    {
        internal const string BrownbooScene   = "gedit/s04/scene.scn";
        internal const string BrownbooMapinfo = "gedit/s04/mapinfo.cfg";
        internal const int    BrownbooSignX = 212, BrownbooSignY = 9, BrownbooSignZ = -61, BrownbooSignRotY = 0;

        // Queens (e03): the sign 3 units SOUTH (+Z) of the fishing trigger (250,70,-70), facing NORTH (-Z, so
        // ry 180 — opposite Brownboo's +Z-facing ry 0). e03 has no kanban part natively, so we clone the SAME
        // s04a01 PTS header (self-contained; the e01b24 texture is already registered globally by the boot-cave)
        // and inject the kanban mesh + placement into e03's own scene.scn / mapinfo.cfg.
        internal const string QueensScene   = "gedit/e03/scene.scn";
        internal const string QueensMapinfo = "gedit/e03/mapinfo.cfg";
        internal const string QueensAnchorPart  = "e03g04";   // an existing GROUND block to insert the kanban placement after
        internal const int    QueensSignX = 250, QueensSignY = 70, QueensSignZ = -64, QueensSignRotY = 180;   // 6 units south (+Z) of the trigger

        // Low-tide canal fishing (canal-lowtide-fishing-plan.md): the canal-FLOOR sign under the eastern
        // bridge (x≈800), on the authored floor Y=0, facing WEST. CONFIRMED in-game: ry −90. sceVu0RotMatrixY
        // folds the angle (|sin|) so +90 and +270 both face EAST and −X is unreachable by any POSITIVE ry;
        // the function branches on the angle sign, so a NEGATIVE angle (−90) reaches west. (0=south, 180=north
        // work either way since those are Z-facing.)
        internal const int    CanalSignX = 800, CanalSignY = 0, CanalSignZ = 0, CanalSignRotY = -90;
        // The ladder donor is carved from the user's OWN ISO (Factory scene, node e05a01/hasigo1) at patch
        // time — same principle as the sign (CarveKanban); nothing is extracted into the codebase.
        internal const string MoonFactoryScene = "gedit/e05/scene.scn";
        internal const string LadderDonorPart = "e05a01", LadderDonorNode = "hasigo1";

        // ── NATIVE EVENT-POINT (trigger) BAKING ──────────────────────────────────────────────────────────
        // Triggers are baked as EPARTS_FUNC_DATA entries (0xC0 each) inside a part's PTS blob; at town load
        // EdInitEventPoint (0x183D50) turns each into a live ED_EVENT_POINT — no runtime creation needed.
        // Layout + field map: memory town-event-points.md. Func type: 0x12 -> type-3 SCRIPT, 0x13/0x14 ->
        // type-4/5 ladder BOTTOM/TOP. Time [0,24] -> ConvertTime start==end==7 == always-on.
        internal const int EventFuncEntryStride = 0xC0;
        // Fishing/canal label ids (400/401/402/403/404 + the mes id) live in FishingLabelIds —
        // the single home for both the bake below and the runtime installer.
        // The Shipwreck (Sunken Ship, s25) exit spot in East Harbor — captured live from a CameraDiag ref after
        // leaving the ship: world (−1311, ~7, 875.7). (Event 128's (1311,7,875.7) was PART-LOCAL — X mirrored →
        // +1311 was off-map. NOT func_mapj00 (−1088,20,1001) = Rando's shop.) Y=7 = feet; the ref's 21 is the
        // camera look-at ~14 above.
        internal static readonly float[] DockSpawnPosition = { -1311f, 7f, 875.7f };
        internal const float DockSpawnFacing = 0f;                                // ry — tune in-game if he faces wrong here

        // Per-town fishing-trigger position, PART-LOCAL to the sign (the mapinfo placement rotates+translates
        // it to world). Chosen so the native trigger lands exactly where the runtime one did (spot tx,ty,tz).
        internal static readonly float[] BrownbooTriggerOffset = { 0f, 3f, 8f };   // sign(212,9,-61) ry0  -> world (212,12,-53)
        internal static readonly float[] QueensTriggerOffset   = { 0f, 0f, 6f };   // sign(250,70,-64) ry180 -> (250,70,-70); canal placement -> (794,0,0)
        internal static readonly float[] YellowDropsTriggerOffset   = { 0f, 0f, 0f };   // new sign placed AT the spot

        // Yellow Drops (s13): no injected sign yet — inject one at the fishing spot like the other towns.
        internal const string YellowDropsScene = "gedit/s13/scene.scn", YellowDropsMapinfo = "gedit/s13/mapinfo.cfg";
        internal const string YellowDropsAnchorPart = "s1301";                                   // an existing s13 GROUND block
        // Moved 2026-08-30 to the WEST BANK bulge edge (needs the west-bank ground bake; the old spot
        // was (-575,9,-286)). ry 90 = face EAST toward the player walking up (sceVu0RotMatrixY fold).
        internal const int YellowDropsSignX = -465, YellowDropsSignY = 30, YellowDropsSignZ = 40, YellowDropsSignRotY = 90;  // at the spot (tx,ty,tz), on the y30 plateau

        // Carved ladder climb points, WORLD space (the ladder verts are world-baked so its part sits at origin
        // identity). Derived by running the vanilla Moon-Factory hasigo1 climb points — bottom (9.9,0,-48.4),
        // top (7.6,90,-34.6) — through the SAME de-yaw + placement transform as the mesh (CanalLadderCarve),
        // so the climb-path geometry (stand-off from the rail + lean) matches the Factory exactly. Bottom sits
        // ~6.5u out in front of the ladder's canal edge (z≈47.4); top is on the walkway side.
        internal static readonly float[] LadderClimbBottom = { CanalLadderCarve.LadderWorldX, 0f, 40.9f };
        internal static readonly float[] LadderClimbTop    = { CanalLadderCarve.LadderWorldX, 70f, 54.9f };
        internal const int LadderRungsBottom = 12, LadderRungsTop = 2, LadderLinkId = 0;   // mirror native hasigo1 (+0x74)
        internal static readonly float[] LadderRotation = { 0f, 0f, 0f };               // rot written to the rec; tune the Y gate in-game
    }
}
