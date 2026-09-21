using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Chronicle Sword and Chronicle 2 — attacks hit all nearby targets for a percentage of damage.</summary>
    internal static class ChronicleSword
    {
        /// <summary>
        /// Checks if the player has Chronicle 2 equipped or in inventory and sets acquired to true if so.
        /// When possessed, Chronicle 2 affects dungeon loot in two ways:
        ///   1. The clown vendor always rolls from the weapon table instead of having a 50% chance of non-weapon items.
        ///   2. Powerup Powder is allowed to appear in chests normally; without Chronicle 2 it has an 80% chance to be re-rolled.
        /// It also adjusts the big-chest spawn threshold based on the current dungeon.
        /// Checks weapon slots 0–9 first, then scans up to 30 storage slots starting at 0x21CE22D8.
        /// </summary>
        /// <param name="acquired"></param>
        /// <returns></returns>
        public static bool CheckChronicle2(bool acquired)
        {
            acquired = false;

            if (Memory.ReadUShort(Player.Toan.WeaponSlot0.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot1.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot2.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot3.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot4.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot5.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot6.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot7.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot8.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot9.id) == 298)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Chronicle 2");
                acquired = true;
            }
            else
            {
                currentAddress = 0x21CE22D8;
                for (int i = 0; i < 30; i++)
                {
                    if (Memory.ReadUShort(currentAddress) == 298)
                    {
                        acquired = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Chronicle 2 in storage");
                    }
                    currentAddress += 0x000000F8;
                }

                if (acquired != true)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player does not have Chronicle 2");
                    acquired = false;
                }
            }
            return acquired;
        }

        // ── Chronicle Sword ────────────────────────────────────────────────────────────────
        private static int currentAddress;   // storage-slot scan cursor for CheckChronicle2
        private static float chronicleCurrentWHP = 0;
        private static float chronicleFormerWHP = 0;
        private static int[] chronicleCurrentEnemyHP;
        private static int[] chronicleFormerEnemyHP;

        public static bool chronicleNewFloor = false;

        public static Thread damageFadeoutThread = new Thread(new ThreadStart(DamageFadeout));

        /// <summary>
        /// Triggers Chronicle effect: Attacks now hit all nearby targets for a percentage of the damage.
        /// </summary>
        public static void ChronicleSwordEffect()
        {
            if (chronicleNewFloor == true)
            {
                for (int i = 0; i < 7; i++ )
                {
                    Memory.WriteInt(0x21EC828C + (0x60 * i), 0);
                    Memory.WriteInt(0x21EC8290 + (0x60 * i), 158);
                    Memory.WriteInt(0x21EC8294 + (0x60 * i), 12);
                    Memory.WriteInt(0x21EC8298 + (0x60 * i), 18);
                }
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                    Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DistanceToPlayer), 0);
                chronicleNewFloor = false;
            }

            Thread.Sleep(50);

            //Save weapon Whp
            chronicleCurrentWHP = ReusableFunctions.GetCurrentEquippedWhp(Player.CurrentCharacterNum(), Player.Toan.GetWeaponSlot());

            //Save every enemy's HP on the current floor
            chronicleCurrentEnemyHP = ReusableFunctions.GetEnemiesHp();

            int damagedEnemyNum = 0;
            if (chronicleCurrentWHP < chronicleFormerWHP && ReusableFunctions.GetRecentDamageDealtByPlayer() > 0)
            {
                float flashRGB_R = 0;
                float flashRGB_G = 0;
                float flashRGB_B = 0;
                float damageDealt = 0;
                for (int i = 0; i < 15; i++)
                {
                    if (chronicleCurrentEnemyHP[i] < chronicleFormerEnemyHP[i])
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Damaged enemy number: " + i);
                        damagedEnemyNum = i;
                        flashRGB_R = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorRed));
                        flashRGB_G = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorGreen));
                        flashRGB_B = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorBlue));
                        damageDealt = Memory.ReadInt(0x21DC452C);
                        break;
                    }
                }

                float[] enemiesDistance = ReusableFunctions.GetEnemiesDistance();
                List<int> enemiesinRange = new List<int>();
                float[] enemiescoordinateX = new float[15];
                float[] enemiescoordinateY = new float[15];
                float[] enemiescoordinateZ = new float[15];
                float[] effectDamage = new float[15];
                int[] effectDamageDigit1 = new int[15];
                int[] effectDamageDigit2 = new int[15];
                int[] effectDamageDigit3 = new int[15];
                int[] effectDamageDigit4 = new int[15];
                int[] effectDamageDigit5 = new int[15];

                for (int i= 0; i < 15; i++)
                {
                    if (i != damagedEnemyNum)
                    {
                        if (chronicleCurrentEnemyHP[i] > 0 && enemiesDistance[i] < 300)
                        {
                            if (enemiesDistance[i] > 0)
                            {
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + i + " is in range");
                                enemiesinRange.Add(i);
                            }
                        }
                    }
                }

                if (enemiesinRange.Count > 0)
                {
                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorRed), flashRGB_R);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorGreen), flashRGB_G);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorBlue), flashRGB_B);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashDecayRate), (float)(0.1));


                        enemiescoordinateX[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationX));
                        enemiescoordinateZ[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationZ));
                        enemiescoordinateY[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationY));

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + enemiesinRange[i] + " XZY coordinates: " + enemiescoordinateX[i] + " " + enemiescoordinateZ[i] + " " + enemiescoordinateY[i]);

                        if (enemiesDistance[enemiesinRange[i]] < 50)
                        {
                            effectDamage[i] = (float)System.Math.Floor(damageDealt / 2);
                        }
                        else
                        {
                            float effectDamagePercent = ((300 - enemiesDistance[enemiesinRange[i]]) / 5);
                            if (effectDamagePercent < 1)
                            {
                                effectDamagePercent = 1;
                            }
                            effectDamage[i] = (float)System.Math.Floor(damageDealt * (effectDamagePercent / 100));
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + enemiesinRange[i] + " effect dmg: " + effectDamage[i]);

                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        int[] digitArray = GetIntArray((int)effectDamage[i]);

                        effectDamageDigit2[i] = -1;
                        effectDamageDigit3[i] = -1;
                        effectDamageDigit4[i] = -1;
                        effectDamageDigit5[i] = -1;
                        if (digitArray.Length > 0)
                        {
                            effectDamageDigit1[i] = digitArray[0];
                        }
                        if (digitArray.Length > 1)
                        {
                            effectDamageDigit2[i] = digitArray[1];
                        }
                        if (digitArray.Length > 2)
                        {
                            effectDamageDigit3[i] = digitArray[2];
                        }
                        if (digitArray.Length > 3)
                        {
                            effectDamageDigit4[i] = digitArray[3];
                        }
                        if (digitArray.Length > 4)
                        {
                            effectDamageDigit5[i] = digitArray[4];
                        }
                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteFloat(0x21EC8240 + (0x60 * i), enemiescoordinateX[i]);
                        Memory.WriteFloat(0x21EC8244 + (0x60 * i), enemiescoordinateZ[i] - 3);
                        Memory.WriteFloat(0x21EC8248 + (0x60 * i), enemiescoordinateY[i]);
                        Memory.WriteFloat(0x21EC824C + (0x60 * i), 1);

                        Memory.WriteFloat(0x21EC8254 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8258 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC825C + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8260 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8264 + (0x60 * i), 0);

                        Memory.WriteInt(0x21EC8268 + (0x60 * i), effectDamageDigit1[i]);
                        Memory.WriteInt(0x21EC826C + (0x60 * i), effectDamageDigit2[i]);
                        Memory.WriteInt(0x21EC8270 + (0x60 * i), effectDamageDigit3[i]);
                        Memory.WriteInt(0x21EC8274 + (0x60 * i), effectDamageDigit4[i]);
                        Memory.WriteInt(0x21EC8278 + (0x60 * i), effectDamageDigit5[i]);

                        Memory.WriteFloat(0x21EC827C + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8280 + (0x60 * i), 3);
                        Memory.WriteInt(0x21EC8284 + (0x60 * i), -1);

                        if (i == 7)
                        {
                            break;
                        }
                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteInt(0x21EC829C + (0x60 * i), 1);
                        Memory.WriteByte(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashActivation), 1);

                        int enemyHP = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.Hp));
                        int newEnemyHP = (int)(enemyHP - effectDamage[i]);
                        if (newEnemyHP < 1)
                        {
                            newEnemyHP = 1;
                            Memory.WriteByte(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.PoisonPeriod), 1);
                        }
                        Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.Hp), newEnemyHP);
                    }

                    if (!damageFadeoutThread.IsAlive)
                    {
                        damageFadeoutThread = new Thread(new ThreadStart(DamageFadeout));
                        damageFadeoutThread.Start();
                    }
                }
            }
            ReusableFunctions.ClearRecentDamageAndDamageSource();
            chronicleFormerWHP = chronicleCurrentWHP;
            chronicleFormerEnemyHP = chronicleCurrentEnemyHP;
        }

        public static void DamageFadeout()
        {
            Thread.Sleep(500);
            for (int i = 0; i < 7; i++)
            {
                Memory.WriteInt(0x21EC8284 + (0x60 * i), 1);
            }
            Thread.Sleep(200);
            for (int i = 0; i < 7; i++)
            {
                Memory.WriteInt(0x21EC829C + (0x60 * i), 0);
            }
        }

        public static int[] GetIntArray(int num)
        {
            List<int> listOfInts = new List<int>();
            while (num > 0)
            {
                listOfInts.Add(num % 10);
                num = num / 10;
            }
            //listOfInts.Reverse();
            return listOfInts.ToArray();
        }
    }
}
