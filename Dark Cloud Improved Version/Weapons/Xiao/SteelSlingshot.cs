using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Steel Slingshot — while its weapon HP is low (the game's own warning state: WHP ≤ 10 % of the maximum, the HUD gauge
    /// blinking) each shot costs half the WHP. Xiao's drain is per shot: BattleActionPlay_Jinn passes
    /// <see cref="Mailbox.XiaoShotWhpFactor"/> (the ISO's dun.bin patch) to SwordDmgCheck1, whose BattleSubWeaponDmg
    /// applies Durable (× 0.5) and Fragile (× 2) on top, so those stack with the half by themselves. The driver keeps
    /// <see cref="ChargedShotWhp.Base"/> at <see cref="LowFactor"/> while the WHP is low and at 1.0 otherwise. Super Steve
    /// carrying a Steel Slingshot SynthSphere has this half (its own WHP being the low one), not the level-up bonus.
    /// </summary>
    internal static class SteelSlingshot
    {
        private const string Tag = "[SteelSlingshot] ";
        private const float LowFactor = 0.5f;
        private static bool _low;

        /// <summary>Drive every tick while the Steel Slingshot is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            int slot = Memory.ReadByte(DngStatusData.Base + DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
            if ((uint)slot > 9) return;
            long rec = DngStatusData.WeaponRecord(Player.XiaoId, slot);
            float whp = Memory.ReadFloat(rec + WeaponHave.InventoryWeaponWhpOffset);
            int max = Memory.ReadShort(rec + WeaponHave.InventoryWeaponMaxWhpOffset);
            bool low = max > 0 && whp <= max * WeaponMenu.LowWhpWarningFraction;
            if (low != _low)
            {
                _low = low;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (low ? $"WHP low ({whp:F0} of {max}): shots cost ×{LowFactor} WHP" : "WHP no longer low: shots cost their vanilla WHP"));
            }
            ChargedShotWhp.SetBase(low ? LowFactor : 1f);
            ChargedShotWhp.Tick();
        }

        /// <summary>The weapon or the floor went: the vanilla factor back.</summary>
        internal static void Stop() { _low = false; ChargedShotWhp.SetBase(1f); }
    }
}
