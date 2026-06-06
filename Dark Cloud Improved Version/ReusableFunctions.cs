using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class ReusableFunctions
    {
        /// <summary>
        /// Returns a timestamp to use in the console logs
        /// </summary>
        /// <returns>The timestamp</returns>
        public static string GetDateTimeForLog()
        {
            return "[" + DateTime.Parse(DateTime.UtcNow.ToString()).ToString("HH:mm:ss") + "] ";
        }

        /// <summary>
        /// Puts the current thread to sleep while the game is paused
        /// <br></br>
        /// 0 = Town <br></br>
        /// 1 = Dungeon
        /// </summary>
        /// <param name="mode">0 = Town<br></br>1 = Dungeon</param>
        /// <returns>Returns true when the game is no longer paused</returns>
        public static bool AwaitUnpause(byte mode) {


            while ((mode == 0) ? Player.CheckTownIsPaused() : Player.CheckDunIsPaused())
            {
                Thread.Sleep(100);
                continue;
            }

            return true;
        }

        public static float GetCurrentEquippedWhp(int characterId, int weaponslotid)
        {
            float whp = 0;

            switch (characterId)
            {
                case 0:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Toan.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Toan.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Toan.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Toan.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Toan.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Toan.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Toan.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Toan.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Toan.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Toan.WeaponSlot9.whp); break;
                    }
                    break;

                case 1:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Xiao.WeaponSlot9.whp); break;
                    }
                    break;

                case 2:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Goro.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Goro.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Goro.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Goro.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Goro.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Goro.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Goro.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Goro.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Goro.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Goro.WeaponSlot9.whp); break;
                    }
                    break;

                case 3:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Ruby.WeaponSlot9.whp); break;
                    }
                    break;

                case 4:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Ungaga.WeaponSlot9.whp); break;
                    }
                    break;

                case 5:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot0.whp); break;
                        case 1: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot1.whp); break;
                        case 2: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot2.whp); break;
                        case 3: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot3.whp); break;
                        case 4: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot4.whp); break;
                        case 5: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot5.whp); break;
                        case 6: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot6.whp); break;
                        case 7: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot7.whp); break;
                        case 8: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot8.whp); break;
                        case 9: whp = Memory.ReadFloat(Player.Osmond.WeaponSlot9.whp); break;
                    }
                    break;
            }
            return whp;
        }

        public static int[] GetEnemiesHp()
        {
            int[] EnemiesHP = new int[EnemyAddresses.FloorSlots.Count];
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                EnemiesHP[i] = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp));
            return EnemiesHP;
        }

        public static float[] GetEnemiesDistance()
        {
            float[] distance = new float[EnemyAddresses.FloorSlots.Count];
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                distance[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.DistanceToPlayer));
            return distance;
        }

        public static List<int> GetEnemiesHitIds(int[] formerEnemiesHp, int[] currentEnemiesHp)
        {
            //Create a list to store the IDs
            List<int> enemyIds = new List<int>();

            //Cycle through enemies HP array
            for (int i = 0; i < formerEnemiesHp.Length; i++)
            {
                //Check for which enemies were damaged
                if (currentEnemiesHp[i] < formerEnemiesHp[i])
                {
                    //Add the iterator to the list we created early as an ID for the damaged enemy
                    enemyIds.Add(i);
                }
            }

            return enemyIds;
        }

        public static List<int> GetEnemiesKilledIds(int[] formerEnemiesHp, int[] currentEnemiesHp)
        {
            //Create a list to store the IDs
            List<int> enemyKilled = new List<int>();

            //Fetch the enemies hit to check if they were killed
            List<int> enemiesHit = GetEnemiesHitIds(formerEnemiesHp, currentEnemiesHp);

            //Go through the enemies hit list and store the ones who died
            foreach (int enemy in enemiesHit)
            {
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(enemy, EnemySlotOffsets.Hp)) == 0)
                    enemyKilled.Add(enemy);
            }

            return enemyKilled;
        }

        public static bool CheckIfAllEnemiesKilled()
        {
            int count = 0;

            for(int i = 0; i < 15; i++)
            {
                if(Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) == 0 &&
                    Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) == 255) {

                    count++;
                }
            }

            if (count == 15) return true;
            else return false;
        }

        /// <summary>
        /// Returns the last damage value the player has dealt
        /// </summary>
        /// <returns></returns>
        public static int GetRecentDamageDealtByPlayer()
        {
            int damage = Memory.ReadInt(Player.mostRecentDamage);
            return damage;
        }

        /// <summary>
        /// Returns the source of the last damage caused
        /// </summary>
        /// <returns>PlayerId, if source is a character's weapon. -1 if source is a throwable.</returns>
        public static int GetDamageSourceCharacterID()
        {
            int character = Memory.ReadInt(Player.damageSource);
            return character;
        }

        /// <summary>
        /// Clears the last damage and damage source values in memory.
        /// </summary>
        public static void ClearRecentDamageAndDamageSource()
        {
            Memory.WriteInt(Player.mostRecentDamage, -1);
            Memory.WriteInt(Player.damageSource, -1);
        }
    }
}
