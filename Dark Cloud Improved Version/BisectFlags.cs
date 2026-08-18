namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY bisection switches for isolating the fishing-camera pull-in regression. Each flag gates a
    /// feature ADDED THIS SESSION. All default to <c>false</c> (feature OFF) so a single rebuild disables the
    /// lot: if the camera still sticks with everything here off, the regression is PRE-EXISTING (the town-camera
    /// pull-in, IsoPatcher.PatchTownCamera — an older ISO patch — interacting with fishing). If it goes away,
    /// flip these back on one at a time to find the culprit. Delete this file once the cause is found.
    ///
    /// NOT toggled here (kept ON): the mailbox relocation of CanalEvict off 0x40 (a bug FIX — 0x40 is the
    /// camera's stick scratch; the old collision false-warped the player and stomped the camera stick). That's
    /// an ISO change and strictly correct, so it stays.
    /// </summary>
    internal static class BisectFlags
    {
        internal static bool CastBoost     = false;  // C# whip-detector impulse — SUPERSEDED by the native cast-trajectory
                                                     // scale (IsoPatcher.PatchFishLineCastScale + Mailbox.CastScale); keep
                                                     // off. Delete the C# path once the native boost is confirmed in-game.
        // (LineSplit3x removed — the ×3 test graduated into the per-spot LineConfigSplit aerial scale.)
        internal static bool EvictFlagPin  = true;   // per-tick canal-evict flag clear (CanalTide)
        internal static bool CapeEarlyDraw = true;   // low-tide cape/body early-draw + stability gate (CanalTide)
    }
}
