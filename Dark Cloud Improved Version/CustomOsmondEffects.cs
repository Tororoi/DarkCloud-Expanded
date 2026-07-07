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
        /// <summary>
        /// Snail effect: 5% chance on hit to inflict gooey on the struck enemy
        /// </summary>
        public static void SnailEffect()
        {
            while (Player.Weapon.GetCurrentWeaponId() == Items.snail &&
                   Player.InDungeonFloor())
            {
                int[] formerHp = ReusableFunctions.GetEnemiesHp();
                Thread.Sleep(50);
                int[] currentHp = ReusableFunctions.GetEnemiesHp();

                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.OsmondId)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            if (random.Next(100) < 5)
                            {
                                Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.GooeyState), 1);
                            }
                        }
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        // ── Star Breaker ───────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers StarBreaker effect: Chance on kill to get an empty synthsphere (Breaks down any weapon)
        /// </summary>
        public static void StarBreakerEffect()
        {
            //Save every enemy's HP on the current floor
            int[] formerEnemiesHP = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            //Re-save every enemy's HP on the current floor
            int[] currentEnemiesHP = ReusableFunctions.GetEnemiesHp();

            List<int> enemiesKilled = ReusableFunctions.GetEnemiesKilledIds(formerEnemiesHP, currentEnemiesHP);

            int roll = random.Next(100);

            //Check if an enemy was killed and if the roll chance was met (2%)
            if(enemiesKilled.Count > 0 && roll < 2)
            {
                //Check if the player has free inventory slots
                if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() >= 0)
                {
                    //Place the synth sphere in inventory
                    Player.Inventory.SetBagAttachments(Items.synthsphere);

                    //Display the effect message
                    Dayuppy.DisplayMessage("The Star Breaker sent\nyou a shooting star!", 2, 21);
                }
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


