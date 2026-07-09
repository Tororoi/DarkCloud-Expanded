using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomOsmondEffects
    {

        private static Random random = new Random();

        // ── Snail ──────────────────────────────────────────────────────────────────────────
        private const int SnailGooeyPercent = 5;   // on-hit chance to goo the struck enemy

        /// <summary>Per-caller Snail state: last tick's enemy-HP snapshot for fresh-hit detection.</summary>
        internal sealed class SnailState { public int[] PrevHp; }

        /// <summary>Snail — per-tick driver: <see cref="SnailGooeyPercent"/>% chance on hit to inflict gooey
        /// on the struck enemy. Wielder-agnostic apart from whose hits count, so Osmond's own Snail and Super
        /// Steve's inherited copy both reuse it.</summary>
        internal static void SnailDrive(bool active, int wielderId, SnailState st)
        {
            int[] cur = ReusableFunctions.GetEnemiesHp();
            if (st.PrevHp != null && active && ReusableFunctions.GetDamageSourceCharacterID() == wielderId)
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
                if (hit) ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
            st.PrevHp = cur;
        }

        /// <summary>Osmond's own Snail weapon: loops the shared driver while it's equipped.</summary>
        public static void SnailEffect()
        {
            var st = new SnailState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.snail && Player.InDungeonFloor())
            {
                SnailDrive(true, Player.OsmondId, st);
                Thread.Sleep(50);
            }
        }

        // ── Star Breaker ───────────────────────────────────────────────────────────────────
        private const int StarBreakerProcPercent = 2;   // on-kill chance to receive a synthsphere

        /// <summary>Per-caller Star Breaker state: last tick's enemy-HP snapshot for kill detection.</summary>
        internal sealed class StarBreakerState { public int[] PrevHp; }

        /// <summary>Star Breaker — per-tick driver: <see cref="StarBreakerProcPercent"/>% chance on an enemy
        /// kill to receive an empty SynthSphere (breaks down any weapon). Faithful to the original: ANY kill
        /// on the floor procs (no damage-source check), which only matters while the wielder is active anyway.</summary>
        internal static void StarBreakerDrive(bool active, StarBreakerState st)
        {
            int[] cur = ReusableFunctions.GetEnemiesHp();
            if (st.PrevHp != null && active)
            {
                List<int> killed = ReusableFunctions.GetEnemiesKilledIds(st.PrevHp, cur);
                if (killed.Count > 0 && random.Next(100) < StarBreakerProcPercent &&
                    Player.Inventory.GetBagAttachmentsFirstAvailableSlot() >= 0)
                {
                    Player.Inventory.SetBagAttachments(Items.synthsphere);
                    Dayuppy.DisplayMessage("The Star Breaker sent\nyou a shooting star!", 2, 21);
                }
            }
            st.PrevHp = cur;
        }

        /// <summary>Osmond's own Star Breaker weapon: loops the shared driver while it's equipped.</summary>
        public static void StarBreakerEffect()
        {
            var st = new StarBreakerState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.starbreaker && Player.InDungeonFloor())
            {
                StarBreakerDrive(true, st);
                Thread.Sleep(50);
            }
        }

        // ── Supernova ──────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Supernova effect: Chance on hit to apply a random status
        /// </summary>
        public static void SupernovaEffect()
        {
            //Get a read on all the enemies hp on the current floor
            int[] formerEnemyHpList = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.OsmondId;

            if (hasHit)
            {
                //Get a second read on all the enemies hp on the current floor
                int[] currentEnemyHpList = ReusableFunctions.GetEnemiesHp();

                //Store the damaged enemies ID onto a list
                List<int> enemyIds = ReusableFunctions.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

                //Go through the enemies IDs
                foreach (int id in enemyIds)
                {
                    int procChance = random.Next(100);    //Roll for chance to proc effect (10% chance)
                    int effect = random.Next(4);        //Roll for which effect to apply (Equal chance)

                    if (procChance <= 10)
                    {
                        int slotBase = EnemyAddresses.FloorSlots.SlotAddr(id, 0);
                        switch (effect)
                        {
                            case 0: Memory.WriteUShort(slotBase + EnemySlotOffsets.FreezeTimer,  300); break;
                            case 1: Memory.WriteUShort(slotBase + EnemySlotOffsets.PoisonPeriod, 1);   break;
                            case 2: Memory.WriteUShort(slotBase + EnemySlotOffsets.StaminaTimer, 300); break;
                            case 3: Memory.WriteUShort(slotBase + EnemySlotOffsets.GooeyState,   1);   break;
                        }
                    }
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

    }
}


