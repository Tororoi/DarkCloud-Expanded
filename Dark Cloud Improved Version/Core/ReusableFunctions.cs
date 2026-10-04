using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class ReusableFunctions
    {

        /// <summary>First occurrence of <paramref name="needle"/> in <paramref name="hay"/> at or after
        /// <paramref name="start"/>, else -1. The one byte-search for the ISO/scene patchers (was
        /// duplicated as IsoPatcher.Find/FindFrom/IndexOf and CanalTide.IndexOf).</summary>
        internal static int IndexOfBytes(byte[] hay, byte[] needle, int start = 0)
        {
            for (int i = System.Math.Max(0, start); i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /// <summary>Last occurrence of <paramref name="needle"/> starting at or before
        /// <paramref name="before"/>, else -1.</summary>
        internal static int LastIndexOfBytes(byte[] hay, byte[] needle, int before)
        {
            for (int i = System.Math.Min(before, hay.Length - needle.Length); i >= 0; i--)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

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
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.ToanId, 9, WeaponRecord.Whp)); break;
                    }
                    break;

                case 1:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.XiaoId, 9, WeaponRecord.Whp)); break;
                    }
                    break;

                case 2:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.GoroId, 9, WeaponRecord.Whp)); break;
                    }
                    break;

                case 3:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.RubyId, 9, WeaponRecord.Whp)); break;
                    }
                    break;

                case 4:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.UngagaId, 9, WeaponRecord.Whp)); break;
                    }
                    break;

                case 5:
                    //Check on which slot is the weapon equipped on and save its Whp
                    switch (weaponslotid)
                    {
                        case 0: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 0, WeaponRecord.Whp)); break;
                        case 1: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 1, WeaponRecord.Whp)); break;
                        case 2: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 2, WeaponRecord.Whp)); break;
                        case 3: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 3, WeaponRecord.Whp)); break;
                        case 4: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 4, WeaponRecord.Whp)); break;
                        case 5: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 5, WeaponRecord.Whp)); break;
                        case 6: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 6, WeaponRecord.Whp)); break;
                        case 7: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 7, WeaponRecord.Whp)); break;
                        case 8: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 8, WeaponRecord.Whp)); break;
                        case 9: whp = Memory.ReadFloat(WeaponRecord.Address(Player.OsmondId, 9, WeaponRecord.Whp)); break;
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
            int damage = Memory.ReadInt(PlayerAddresses.MostRecentDamage);
            return damage;
        }

        /// <summary>
        /// Returns the source of the last damage caused
        /// </summary>
        /// <returns>PlayerId, if source is a character's weapon. -1 if source is a throwable.</returns>
        public static int GetDamageSourceCharacterID()
        {
            int character = Memory.ReadInt(PlayerAddresses.DamageSource);
            return character;
        }

        /// <summary>
        /// Clears the last damage and damage source values in memory.
        /// </summary>
        public static void ClearRecentDamageAndDamageSource()
        {
            Memory.WriteInt(PlayerAddresses.MostRecentDamage, -1);
            Memory.WriteInt(PlayerAddresses.DamageSource, -1);
        }
    }
}
