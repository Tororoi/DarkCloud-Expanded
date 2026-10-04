namespace Dark_Cloud_Improved_Version
{
    /// <summary>Toan's two charge-attack hit radii as data words. ToanKey_Play baked them into instructions; the ISO patch
    /// (ElfToanMeleePatches.PatchChargeHitRadius) turned the two immediates into reads of CodeCaves.ChargeHitRadius, so the words
    /// ARE the radii: seeded to the game's own 6 / 12 at startup (<see cref="Seed"/>, MainMenuThread.ApplyNewChanges — a 0 there is a
    /// charge attack that hits nothing) and resized by an ability (<see cref="Set"/>: Big Bang puts the whirl's out of reach while
    /// its blast is the whirl's damage), which puts them back with Seed. The engine plants the damage from these spheres — its
    /// victims, its knockback, one plant per frame — so an ability sized by them needs no hit detection of its own. ⚠ The words
    /// are global: every weapon's charge reads them.</summary>
    internal static class ChargeHitRadii
    {
        /// <summary>Both words at vanilla: lunge 6, whirlwind 12.</summary>
        internal static void Seed() => Set(CodeCaves.LungeRadiusVanilla, CodeCaves.WhirlRadiusVanilla);

        /// <summary>The lunge's and the whirlwind's hit radius. ⚠ Always paired with <see cref="Seed"/>.</summary>
        internal static void Set(float lunge, float whirl)
        {
            Memory.WriteFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusLunge, lunge);
            Memory.WriteFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl, whirl);
        }
    }
}
