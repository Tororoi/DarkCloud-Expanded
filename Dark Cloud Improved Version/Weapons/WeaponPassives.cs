namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The weapon OWNERSHIP passives: effects a weapon grants while it is owned (hand, bag or storage —
    /// <see cref="WeaponOwnership"/>), whether or not it is equipped. They run on every game-loop tick in
    /// every mode (town, dungeon, menus) because ownership and ABS level-ups change from any menu; each one
    /// self-gates its own cadence.
    /// </summary>
    internal static class WeaponPassives
    {
        private const string Tag = "[WeaponPassives] ";

        /// <summary>One game-loop tick (50 ms). The six passives, in this order.</summary>
        internal static void Tick()
        {
            // Buster Sword "Buster Boost" absorb watcher — runs in every mode (self-gated to
            // ~2.5 Hz) because ABS level-ups can happen from any menu, sword equipped or not.
            BusterSword.BusterBoostEffect();
            // 7 Branch Sword "Sevenfold Rite" — status-break patch keyed on the weapon-menu
            // selection; runs in every mode because the menu exists everywhere (self-gated ~10 Hz).
            SevenBranchSword.SevenfoldRiteEffect();
            // Macho Sword "Overtraining" — ownership passive: ABS rollover past max on every
            // weapon + level-up carry; runs in every mode (level-ups happen from any menu,
            // self-gated ~10 Hz, kill tracking only while in a dungeon floor).
            MachoSword.OvertrainingEffect();
            // Atlamillia Sword "Atlamillia Insurance" — break watcher + dynamic atla + collected-slot
            // erasure; runs in every mode (self-gated ~2 Hz) so floor-select stays clean too.
            AtlamilliaSword.AtlamilliaInsuranceEffect();
            // Crysknife / Magical Hammer "Circle Amplifier" — ownership passive: the magic circle table ×2 while one is
            // owned, ×3 while both are (self-gated 1 Hz; ownership changes in menus, so it runs in every mode).
            CrysKnife.CircleAmplifierEffect();
            // Secret Armlet "Favoured Circles" — ownership passive: the circle table's favour word while the armlet is owned (self-gated 1 Hz).
            SecretArmlet.FavouredCirclesEffect();
        }
    }
}
