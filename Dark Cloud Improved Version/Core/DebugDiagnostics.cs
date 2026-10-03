namespace Dark_Cloud_Improved_Version
{
    /// <summary>The one switch for the mod's diagnostic logging: the per-tick traces, probes and state dumps that explain a
    /// feature's engine interaction while it is being worked on (each marked DIAGNOSTIC where it runs), and the test aids
    /// (TestWeaponGrant). Off in play; set true in a dev build.</summary>
    internal static class DebugDiagnostics
    {
        internal static bool Enabled = false;
    }
}
