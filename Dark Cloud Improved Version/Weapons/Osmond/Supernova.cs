using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Supernova — each hit may unleash a random affliction: the enemy HP list is sampled, 250 ms later every enemy Osmond
    /// damaged in between rolls 11-in-100 for one of four ailments at equal odds — freeze (300 frames), poison, stamina drain (300
    /// frames) or gooey — and the recent-damage record is cleared. One pass per launch from the dungeon tick (WeaponThreads restarts
    /// it each walking-mode tick it is not running).</summary>
    internal static class Supernova
    {
        private static Random random = new Random();

        public static void SupernovaEffect()
        {
            //Get a read on all the enemies hp on the current floor
            int[] formerEnemyHpList = EnemyQueries.GetEnemiesHp();

            Thread.Sleep(250);

            int hit = EnemyQueries.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && EnemyQueries.GetDamageSourceCharacterID() == Player.OsmondId;

            if (hasHit)
            {
                //Get a second read on all the enemies hp on the current floor
                int[] currentEnemyHpList = EnemyQueries.GetEnemiesHp();

                //Store the damaged enemies ID onto a list
                List<int> enemyIds = EnemyQueries.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

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
            EnemyQueries.ClearRecentDamageAndDamageSource();
        }
    }
}
