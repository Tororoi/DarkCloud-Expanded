using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with an Aga's Sword sphere — "Defensive Legacy" on Xiao: +<see cref="DefenseBoost"/> to her defense
    /// while the sphere is attached, taken back off when it goes (a balanced ± write, so her base defense is never lost).
    /// Mirrors <see cref="AgasSword.DefensiveLegacyEffect"/>, whose loop is written against Player.Toan; a wielder parameter
    /// there (the character's defense accessors) would let it serve both. Driven from Super Steve's sphere dispatch.</summary>
    internal static class AgasSwordSphere
    {
        private const string Tag = "[AgasSwordSphere] ";
        private const int DefenseBoost = 15;   // mirrors AgasSword.DefensiveLegacyEffect
        private static bool _applied;          // whether the +15 defense is currently on Xiao

        /// <summary>Defensive Legacy (Aga's Sword): balanced ±<see cref="DefenseBoost"/> on Xiao's defense.</summary>
        internal static void Drive(bool want)
        {
            if (want && !_applied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() + DefenseBoost); _applied = true; }
            else if (!want && _applied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() - DefenseBoost); _applied = false; }
        }
    }
}
