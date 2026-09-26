using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Baselard — "Heavy Hand": every hit from the sword throws its enemy far. Each melee hit, the lunge and
    /// the whirlwind kick at half the distance Big Bang's blast does (MeleeKick, the engine's own knockback words),
    /// for as long as the Baselard is in Toan's hand on a dungeon floor.</summary>
    internal static class Baselard
    {
        // Big Bang's blast kicks at 3.5 fading 0.12 a frame: ≈ 3.5² / (2·0.12) ≈ 51 units. Half that distance at the
        // same fade is a strength of 3.5 / √2 (the distance goes with the square of it).
        private const float KickStrength = 2.475f;    // ≈ 25 units
        private const float KickDecay    = 0.12f;     // the blast's own fade: the throw reads the same, only shorter
        private const int   TickMs       = 100;

        public static void HeavyHandEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Baselard] heavy hand: every hit kicks at {KickStrength:F2} fading {KickDecay:F2} (≈ {KickStrength * KickStrength / (2 * KickDecay):F0} units)");
            while (Player.Weapon.GetCurrentWeaponId() == Items.baselard && Player.InDungeonFloor())
            {
                if (Player.CurrentCharacterNum() == Player.ToanId) MeleeKick.Set(KickStrength, KickDecay);   // re-asserted: a word found vanilla is written again
                else MeleeKick.Restore();                                                                    // an ally swinging keeps the game's own kick
                Thread.Sleep(TickMs);
            }
            MeleeKick.Restore();                                                        // swapped, or off the floor: the game's own kick back
        }
    }
}
