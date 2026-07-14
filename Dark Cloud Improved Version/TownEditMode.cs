using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Offer the Georama bird's-eye camera (Select button) — and the developer's faster run — in EVERY town,
    /// not just the first five maps.
    ///
    /// The engine already has all of this; it is simply gated. <c>EditLoop</c> reads:
    ///
    /// <code>
    /// if (((0 &lt; EdDebugMoveFlag) || (MapNo &lt; 5)) &amp;&amp; SELECT &amp;&amp; !viewMode &amp;&amp; !changing)
    ///     if (GameMode == 1) GameMode = 4;    // -> overhead camera
    /// </code>
    ///
    /// so a leftover developer flag sits right next to the map check and overrides it. Setting it to 1 is the
    /// whole feature. Verified working in Queens (native), Brownboo and Yellow Drops (both normally excluded).
    ///
    /// THE CATCH: <see cref="EditLoop.EdDebugMoveFlag"/> is ONE global read by THREE systems —
    /// <c>EditLoop</c> (the camera), <c>EdMoveChara</c> (movement), and <c>CheckEditToWalk</c> (where you are
    /// allowed to drop back to walking). So switching it on for the camera also:
    ///
    ///   • doubles run speed while the run button is held — a bonus, and kept on purpose; and
    ///   • stops <c>CheckEditToWalk</c> validating the exit point — which is NOT a bonus. That block checks the
    ///     exit is inside a real edit area and near its ground, and, crucially, SNAPS the player onto the
    ///     surface below. Skipping it drops the player at the cursor's raw height, so exiting in a corner or
    ///     above a building lands them somewhere absurd.
    ///
    /// <see cref="RestoreSafeExit"/> fixes exactly that, and nothing else: it forces the single instruction in
    /// <c>CheckEditToWalk</c> that reads the flag to load 0, so that function alone sees the flag as off. You
    /// keep the camera and the fast run; you lose the broken placement.
    ///
    /// Do NOT raise the flag to 2 or higher — at 2 it also bypasses <c>MoveCheck</c>, i.e. noclip.
    /// </summary>
    internal static class TownEditMode
    {
        /// <summary>Offer the overhead camera on every map. Re-asserted each tick: a map load clears it.</summary>
        internal static bool OverheadCameraEverywhere = true;

        /// <summary>Keep exit-from-overhead placement sane (see the class remarks). Leave this on unless you
        /// are deliberately testing the unpatched behaviour.</summary>
        internal static bool SafeExit = true;

        private static bool _loggedPatch;

        /// <summary>Drive from the town loop. Cheap: one read, and a write only when something has reset.</summary>
        internal static void Tick()
        {
            if (!OverheadCameraEverywhere) return;

            if (Memory.ReadInt(EditLoop.EdDebugMoveFlag) <= 0)
                Memory.WriteInt(EditLoop.EdDebugMoveFlag, 1);   // 1, never 2 — 2 is noclip

            if (SafeExit) RestoreSafeExit();
        }

        /// <summary>
        /// Make <c>CheckEditToWalk</c>'s read of <see cref="EditLoop.EdDebugMoveFlag"/> return 0, so its
        /// ground-snap and area validation run even while the flag is on for the camera.
        ///
        /// In-place, single instruction, no branch retargeting. Idempotent — it only writes while the stock
        /// instruction is still there, so it will not fight anything else patching the same site.
        /// </summary>
        internal static void RestoreSafeExit()
        {
            if (Memory.ReadUInt(EditLoop.CheckEditToWalkFlagRead) != EditLoop.CheckEditToWalkOrig) return;

            Memory.WriteInt(EditLoop.CheckEditToWalkFlagRead, unchecked((int)EditLoop.CheckEditToWalkPatch));
            if (_loggedPatch) return;
            _loggedPatch = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "[TownEditMode] patched CheckEditToWalk — exiting the overhead camera now snaps to the " +
                "ground and validates the area again");
        }

        /// <summary>Put everything back: the stock instruction, and the flag off.</summary>
        internal static void Restore()
        {
            Memory.WriteInt(EditLoop.CheckEditToWalkFlagRead, unchecked((int)EditLoop.CheckEditToWalkOrig));
            Memory.WriteInt(EditLoop.EdDebugMoveFlag, 0);
            _loggedPatch = false;
        }
    }
}
