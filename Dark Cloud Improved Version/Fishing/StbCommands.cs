using System;

namespace Dark_Cloud_Improved_Version
{
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
        // _MAP_JUMP(mapNo[, eventNo]) — VM cmd 15 (verified: handler 0x18bfe0 in the dispatch table).
        // mapNo is 1-BASED (the handler does MapJump(mapNo-1, eventNo)); eventNo defaults -1 = default entry.
        // Sets the transition flag the event-mode state machine consumes when the script ends → full map load.
        internal const int MapJump       = 15;      // (mapNo1Based[, eventNo])
        internal const int FadeIn        = 500;     // (frames) — 500 is FADE_IN, not FADE_OUT
        internal const int SetWorldCoord = 7;       // (x, y, z, rx, ry, rz)
        internal const int InitNpcCloth  = 143;     // (charaId) — charaId -1 = the player (DAT_01d3d21c); resets its cloth to rest pose (engine Clear__6CCloth)
        internal const int SetNpcMotion  = 133;     // (charaId, motionIdx) — charaId -1 = the player; motion 0 = idle
        internal const int SetNpcPos     = 137;     // (charaId, x, y, z)   charaId -1 = the player
        internal const int SetNpcRot     = 138;     // (charaId, rx, ry, rz)
        internal const int GetNpcPos     = 131;     // (charaId, &x, &y, &z) — reads position into locals (out-pointers)
        internal const int GetNpcRot     = 139;     // (charaId, &rx, &ry, &rz) — reads rotation into locals
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

        // ── Menu / select-cursor commands (entry & quit dialogs) ───────────────────────────────────────
        internal const int MesMake        = 192;  // (window, msgId) — draw a message; window 1 = event mes (our menu text)
        internal const int MesClose       = 193;  // (window)
        internal const int SetMesShippo   = 196;  // (window, flag) — speech-bubble tail
        internal const int SetMesDrawSpeed= 198;  // (window, speedFloat) — Norune's fishing menu sets 1.0
        internal const int SetMesPos      = 197;  // (window, posMode)  — Norune's fishing menu uses 9, then 0 to reset
        internal const int SetMesAutoset  = 195;  // (window, x1,y1,x2,y2) — auto-place the bubble to avoid the rect (Norune: 0,0,0,0)
        internal const int SetMesCursor   = 199;  // (window, line) — draw the selection cursor at 0-based line
        internal const int GetApad        = 903;  // (&lx, &ly[, &rx, &ry]) — analog stick floats; LY<-0.5 up, >0.5 down
        internal const int GotoFpChange   = 24;   // () — Exchange FP (menu_mode 8, engine-drawn)
        internal const int GotoFishRanking= 26;   // () — Fishing log (menu_mode 10, engine-drawn)
        internal const int SetReturnCode  = 3;    // (code) — 11 keeps the fishing session running
        internal const int SItemCheck     = 707;  // (itemId, &out) — out = inventory index (>=0) if owned, -1 if not (fishing rod = 185)
        internal const int FishingRodItem = 185;  // the fishing pole checked by the "Fish" option

        // _GET_PADDOWN result masks (post exch_ok_cancel). D-pad bits are unswapped; X arrives as 0x20.
        internal const int PadUp   = 0x1000;
        internal const int PadDown = 0x4000;
    }
}
