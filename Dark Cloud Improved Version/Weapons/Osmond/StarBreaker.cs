using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Star Breaker — kills may break off an empty SynthSphere: a tick in which an enemy died rolls
    /// <see cref="StarBreakerProcPercent"/>% for a SynthSphere into the first free bag attachment slot, with the "shooting star"
    /// message. ANY kill on the floor procs (no damage-source check), which only matters while the wielder is active anyway; the
    /// driver (<see cref="StarBreakerDrive"/>) is shared with Super Steve's inherited copy.</summary>
    internal static class StarBreaker
    {
        private static Random random = new Random();
        private const int StarBreakerProcPercent = 2;   // on-kill chance to receive a synthsphere

        /// <summary>Per-caller Star Breaker state: last tick's enemy-HP snapshot for kill detection.</summary>
        internal sealed class StarBreakerState { public int[] PrevHp; }

        /// <summary>Per-tick driver: a tick with at least one kill rolls once; a win needs a free bag attachment slot.</summary>
        internal static void StarBreakerDrive(bool active, StarBreakerState st)
        {
            int[] cur = EnemyQueries.GetEnemiesHp();
            if (st.PrevHp != null && active)
            {
                List<int> killed = EnemyQueries.GetEnemiesKilledIds(st.PrevHp, cur);
                if (killed.Count > 0 && random.Next(100) < StarBreakerProcPercent &&
                    Inventory.GetBagAttachmentsFirstAvailableSlot() >= 0)
                {
                    Inventory.SetBagAttachments(Items.synthsphere);
                    DungeonMessages.DisplayMessage("The Star Breaker sent\nyou a shooting star!", 2, 21);
                }
            }
            st.PrevHp = cur;
        }

        /// <summary>Osmond's own Star Breaker: the driver every 50 ms while it is equipped on a floor.</summary>
        public static void ShootingStarsEffect()
        {
            var st = new StarBreakerState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.starbreaker && Player.InDungeonFloor())
            {
                StarBreakerDrive(true, st);
                Thread.Sleep(50);
            }
        }
    }
}
