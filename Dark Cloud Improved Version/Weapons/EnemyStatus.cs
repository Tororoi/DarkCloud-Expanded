using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A shot's status effects applied to an enemy as DATA, with CMonstorUnit::CheckDmg's own rules — what a hit planted by
    /// the mod uses when its element word must stay a pure element bit (a status bit in +0x50 sends CheckDmg's element branch through
    /// the wrong column). The Angel Gear's reflected shots and the Confuse ability's enemy-on-enemy shots both land their statuses here.</summary>
    internal static class EnemyStatus
    {
        /// <summary>A shot config's flag word (BT_SHOT_EFFECT +0x40): the five element bits, and the enemy status bits a hit can carry.</summary>
        internal const uint ShotElementMask = 0x1F, ShotEnemyStatusMask = 0x100 | 0x200 | 0x800;

        /// <summary>The shot's statuses (<paramref name="stat"/>: the config flag word's enemy-valid bits — 0x100 freeze, 0x200 poison,
        /// 0x800 gooey), applied exactly as CheckDmg applies +0x50 bits: gated only by the species susceptibility (0 = immune); poison
        /// 180 f unless frozen/raging (clears gooey); freeze 300 f (toggles OFF if already frozen; clears the others and the move
        /// blend); gooey 180 f only when no other status is up. Curse (0x400) and 0x1000 do nothing to enemies, as in vanilla.
        /// Returns a note for the caller's log.</summary>
        internal static string ApplyShotStatus(int slot, uint stat)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
            if (Memory.ReadShort(a + EnemySlotOffsets.StatusSusceptibility) == 0) return " status: immune";
            int freeze = Memory.ReadInt(a + EnemySlotOffsets.FreezeTimer), poison = Memory.ReadInt(a + EnemySlotOffsets.PoisonPeriod);
            int rage = Memory.ReadInt(a + EnemySlotOffsets.StaminaTimer);
            var applied = new List<string>();
            if ((stat & 0x200) != 0 && freeze == 0 && rage == 0)
            {
                Memory.WriteInt(a + EnemySlotOffsets.PoisonPeriod, 0xB4); Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0); poison = 0xB4; applied.Add("poison");
            }
            if ((stat & 0x100) != 0)
            {
                if (freeze < 1)
                {
                    Memory.WriteInt(a + EnemySlotOffsets.FreezeTimer, 300); Memory.WriteInt(a + EnemySlotOffsets.PoisonPeriod, 0);
                    Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0); Memory.WriteInt(a + EnemySlotOffsets.StaminaTimer, 0);
                    Memory.WriteFloat(a + EnemySlotOffsets.MovementBlend, 0f); freeze = 300; poison = 0; applied.Add("freeze");
                }
                else { Memory.WriteInt(a + EnemySlotOffsets.FreezeTimer, 0); freeze = 0; applied.Add("freeze off"); }
            }
            if ((stat & 0x800) != 0 && freeze == 0 && poison == 0 && rage == 0)
            {
                Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0xB4); applied.Add("gooey");
            }
            return applied.Count > 0 ? " status: " + string.Join("+", applied) : " status: blocked by an active status";
        }
    }
}
