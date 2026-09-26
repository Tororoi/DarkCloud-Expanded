using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Weapon HP spent by an ability, the way the engine spends it per swing: a base cost scaled by the
    /// weapon's Endurance — <c>(1.5 − 0.01 × endurance)</c> per swing-equivalent — halved for a Durable weapon and
    /// doubled for a Fragile one, down to 0 — where the engine breaks it on the next swing.</summary>
    internal static class WeaponWhp
    {
        private const float SwingBase = 1.5f;   // what one swing costs at zero Endurance

        /// <summary>Spend <paramref name="baseWhp"/> (the cost before Endurance) from Toan's equipped weapon, which must
        /// be <paramref name="weaponId"/>. How much was taken.</summary>
        internal static float Drain(ushort weaponId, float baseWhp, string tag)
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.ToanId));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return 0f;
            long rec = DngStatusData.WeaponRecord(Player.ToanId, bag);
            if (Memory.ReadUShort(rec) != weaponId) return 0f;              // not the blade that was spent

            float factor = baseWhp / SwingBase;
            int flags = Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset);
            if ((flags & WeaponHave.DurableFlag) != 0) factor *= 0.5f;
            if ((flags & WeaponHave.FragileFlag) != 0) factor *= 2f;
            int endurance = Memory.ReadShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffEnduranceOffset);
            float drain = (SwingBase - 0.01f * endurance) * factor;
            if (drain <= 0f) return 0f;

            long whpAddr = rec + WeaponHave.InventoryWeaponWhpOffset;
            float whp = Memory.ReadFloat(whpAddr);
            // Down to 0, not 1: the engine's own drain (BattleSubWeaponDmg) breaks a weapon it takes to 0 or below — with an
            // Auto Repair Powder used first if one is carried — so a blade this leaves at 0 breaks on its next swing, the
            // way an exhausted one does. A floor of 1 had the blast unable to break it at all.
            float left = Math.Max(0f, whp - drain);
            Memory.WriteFloat(whpAddr, left);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"{tag}cost {whp - left:F1} WHP ({whp:F0} → {left:F0})" + (left <= 0f ? " — spent: it breaks on the next swing" : ""));
            return whp - left;
        }
    }
}
