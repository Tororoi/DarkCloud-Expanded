using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Snail — leaves foes stuck in its trail: each hit has a <see cref="SnailGooeyPercent"/>% chance to inflict gooey on the
    /// struck enemy. The driver (<see cref="SnailDrive"/>) is wielder-agnostic apart from whose hits count, so Osmond's own Snail and
    /// Super Steve's inherited copy both reuse it.</summary>
    internal static class Snail
    {
        private static Random random = new Random();
        private const int SnailGooeyPercent = 5;   // on-hit chance to goo the struck enemy

        /// <summary>Per-caller Snail state: last tick's enemy-HP snapshot for fresh-hit detection.</summary>
        internal sealed class SnailState { public int[] PrevHp; }

        /// <summary>Per-tick driver: every freshly hit enemy rolls <see cref="SnailGooeyPercent"/>% for gooey when the damage came from
        /// <paramref name="wielderId"/>; the recent-damage source is cleared after a hit.</summary>
        internal static void SnailDrive(bool active, int wielderId, SnailState st)
        {
            int[] cur = EnemyQueries.GetEnemiesHp();
            if (st.PrevHp != null && active && EnemyQueries.GetDamageSourceCharacterID() == wielderId)
            {
                bool hit = false;
                for (int i = 0; i < EnemyAddresses.FloorSlots.Count && i < st.PrevHp.Length && i < cur.Length; i++)
                {
                    if (st.PrevHp[i] > 0 && cur[i] < st.PrevHp[i])
                    {
                        hit = true;
                        if (random.Next(100) < SnailGooeyPercent)
                            Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.GooeyState), 1);
                    }
                }
                if (hit) EnemyQueries.ClearRecentDamageAndDamageSource();
            }
            st.PrevHp = cur;
        }

        /// <summary>Osmond's own Snail: the driver every 50 ms while it is equipped on a floor.</summary>
        public static void SlimeTrailEffect()
        {
            var st = new SnailState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.snail && Player.InDungeonFloor())
            {
                SnailDrive(true, Player.OsmondId, st);
                Thread.Sleep(50);
            }
        }
    }
}
