namespace Dark_Cloud_Improved_Version
{
    /// <summary>Facts about Super Steve's dungeon rig that the sphere abilities share: the model whitened or tinted as "the
    /// weapon", the one glow disc resident while Xiao is the active character, and how it is painted and sized.</summary>
    internal static class SuperSteveRig
    {
        internal const string GlowDisc    = "catglowp";   // the cat's disc: the one glow disc resident while Xiao is the active character
        internal const int    GlowGoldRow = 9;            // the glow cave's ONE-based palette row: 1–5 the elements, 6 none, 7 the Divine Beast blue, 8 the Angel Shooter white, 9 the Angel Gear gold — the Sun Sword's colour
        internal const string WeaponModel = "c04w13";     // Super Steve's dungeon rig (item 312 = c04w13.chr): what SolarBlade whitens
        internal const float  GlowSize = 0.75f;           // the disc wider than the ×10 stone (45 units at 1.0 — 0.4 sat behind the 20-unit sprite): on the pouch while primed at the same size, so it looks the same when it rides the pellet
    }
}
