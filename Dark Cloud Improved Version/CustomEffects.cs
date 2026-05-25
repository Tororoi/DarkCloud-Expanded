using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class CustomEffects
    {
        static int currentAddress;
        public static bool evilciseNewFloor = false;
        public static bool chronicleNewFloor = false;
        static float chronicleCurrentWHP = 0;
        static float chronicleFormerWHP = 0;
        static int[] chronicleCurrentEnemyHP;
        static int[] chronicleFormerEnemyHP;
        public const int mode = Addresses.mode;

        private static Random random = new Random();
        public static Thread damageFadeoutThread = new Thread(new ThreadStart(DamageFadeout));

        private static readonly HashSet<int> FrozenTunaIceEnemies = new()
        {
            Enemies.blizzard,   // 65
            Enemies.sam,        // 85
            Enemies.gemronice,  // 312
        };

        private static readonly HashSet<int> CactusImmuneNameTags = new()
        {
            Enemies.masterjacket,    // 1
            Enemies.skeletonsoldier, // 3
            Enemies.statue,          // 5
            Enemies.pirateschariot,  // 25
            Enemies.golem,           // 30
            Enemies.mrblare,         // 31
            Enemies.dune,            // 32
            Enemies.titan,           // 33
            Enemies.arthur,          // 40
            Enemies.livingarmor,     // 55
            Enemies.steelgiant,      // 64
            Enemies.billy,           // 69
            Enemies.vulcan,          // 70
            Enemies.rockanoff,       // 77
            Enemies.gol,             // 90
            Enemies.sil,             // 91
            Enemies.statuedog,       // 303
            Enemies.gacious,         // 317
            Enemies.silvergear,      // 318
            Enemies.hornhead,        // 319
        };

        /// <summary>
        /// Toggles the effect of the bone rapier
        /// </summary>
        /// <param name="isActive">True if to active the effect</param>
        public static void BoneRapierEffect(bool isActive)
        {
            if (isActive)
            {
                //Set BypassBoneDoor
                if (!Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(true);
            }
            //Otherwise reset BypassBoneDoor
            else if (Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(false);
        }

        /// <summary>
        /// Trigger to open a bone door
        /// </summary>
        public static void BoneDoorTrigger()
        {
            while (!Dungeon.doorIsOpen &&
                    Player.InDungeonFloor() &&
                    Player.Weapon.GetCurrentWeaponId() == 290)
            {
                //Bone door opened through Bone Rapier
                if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                    Dungeon.IsBypassBoneDoor() &&
                    Memory.ReadInt(0x21D56800) == 15903712) //Aux address to help determine if the bone door specifically was opened)
                {
                    int ms = 0;

                    while (Memory.ReadInt(Addresses.hideHud) == 1 && ms < 2000)
                    {
                        Thread.Sleep(100);
                        ms += 100;
                        continue;
                    }

                    //Display our custom message
                    Dayuppy.DisplayMessage("You can hear an ominous voice\nlaughing 'Rattle me bones!'", 2, 29, 4000);
                    Dungeon.doorIsOpen = true;
                }
                //Bone door opened normally without Bone Rapier
                else if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                        !Dungeon.IsBypassBoneDoor() &&
                        Memory.ReadInt(0x21D56800) == 15903712 //Aux address to help determine if the bone door specifically was opened
                        )
                {
                    Dungeon.doorIsOpen = true;
                }

                Thread.Sleep(500);
            }
        }

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

        /// <summary>
        /// Wise Owl Sword passive: displays a message when the correct WOF floor key is picked up,
        /// provided the player owns a Wise Owl Sword anywhere (bag, storage, or equipped).
        /// </summary>
        public static void WiseOwlSword()
        {
            const int bagItemSlots = 60;
            ushort[] snapshot = new ushort[bagItemSlots];

            for (int i = 0; i < bagItemSlots; i++)
                snapshot[i] = Memory.ReadUShort(Addresses.firstBagItem + (2 * i));

            while (Memory.ReadByte(Addresses.checkDungeon) == 1 && Player.InDungeonFloor())
            {
                Thread.Sleep(200);

                for (int i = 0; i < bagItemSlots; i++)
                {
                    ushort current = Memory.ReadUShort(Addresses.firstBagItem + (2 * i));
                    if (current != snapshot[i] &&
                        (current == Items.shinystone || current == Items.redberry || current == Items.pointychestnut))
                    {
                        if (PlayerHasWiseOwlSword())
                            Dayuppy.DisplayMessage("You found Wise Owl's favorite!", 1, 30, 3000);
                        break;
                    }
                    snapshot[i] = current;
                }
            }
        }

        private static bool PlayerHasWiseOwlSword()
        {
            if (Player.Weapon.GetCurrentWeaponId() == Items.wiseowlsword)
                return true;

            for (int i = 0; i < 10; i++)
                if (Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * i)) == Items.wiseowlsword)
                    return true;

            for (int i = 0; i < 30; i++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * i)) == Items.wiseowlsword)
                    return true;

            return false;
        }

        /// <summary>
        /// Evilcise effect: Toan is cursed while equipped and immune to all other status effects.
        /// Breaking the curse with holy water applies poison and sets HP to 1.
        /// The curse is reapplied on each new floor.
        /// </summary>
        public static void Evilcise()
        {
            // Toan status bits: 0x02=NearDeath 0x04=Freeze 0x08=Stamina 0x10=Poison 0x20=Curse 0x40=Goo
            const int toanStatus      = 0x21CDD814;
            const int toanStatusTimer = 0x21CDD824;
            const int toanHp          = 0x21CD955E;

            bool penalized = false;
            byte lastFloor = Memory.ReadByte(Addresses.checkFloor);

            // Apply curse immediately on equip
            ushort cur = Memory.ReadUShort(toanStatus);
            Memory.WriteUShort(toanStatus,      (ushort)((cur & 0x02) | 0x20));
            Memory.WriteUShort(toanStatusTimer, 3600);

            while (Player.Weapon.GetCurrentWeaponId() == Items.evilcise &&
                   Player.InDungeonFloor())
            {
                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    // New floor: clear penalty and reapply curse
                    penalized = false;
                    lastFloor = currentFloor;
                    cur = Memory.ReadUShort(toanStatus);
                    Memory.WriteUShort(toanStatus,      (ushort)((cur & 0x02) | 0x20));
                    Memory.WriteUShort(toanStatusTimer, 3600);
                }

                if (!penalized)
                {
                    cur = Memory.ReadUShort(toanStatus);
                    if ((cur & 0x20) == 0)
                    {
                        // Curse was removed externally (holy water) — penalize
                        penalized = true;
                        Memory.WriteUShort(toanStatus,      (ushort)((cur & 0x02) | 0x10)); // Poison
                        Memory.WriteUShort(toanStatusTimer, 3600);
                        Memory.WriteUShort(toanHp, 1);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Evilcise: curse broken — poison and HP=1 applied");
                    }
                    else
                    {
                        // Curse active: strip other statuses and refresh timer
                        Memory.WriteUShort(toanStatus,      (ushort)((cur & 0x02) | 0x20));
                        Memory.WriteUShort(toanStatusTimer, 3600);
                    }
                }

                Thread.Sleep(100);
            }

            // Strip curse on unequip or dungeon exit
            ushort final = Memory.ReadUShort(toanStatus);
            Memory.WriteUShort(toanStatus, (ushort)(final & ~0x20));
        }

        /// <summary>
        /// Heaven's Cloud effect: 50% chance on hit to inflict gooey on the struck enemy.
        /// </summary>
        public static void HeavensCloud()
        {
            while (Player.Weapon.GetCurrentWeaponId() == Items.heavenscloud &&
                   Player.InDungeonFloor())
            {
                int[] formerHp = ReusableFunctions.GetEnemiesHp();
                Thread.Sleep(50);
                int[] currentHp = ReusableFunctions.GetEnemiesHp();

                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.ToanId)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            if (random.Next(100) < 50)
                            {
                                Memory.WriteUShort(Enemies.Enemy0.gooeyState + (Enemies.offset * i), 1);
                            }
                        }
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Aga's Sword: +15 defense to Toan while equipped.
        /// </summary>
        public static void AgasSword()
        {
            const int boost = 15;
            int baseDefense = Player.Toan.GetDefense();
            Player.Toan.SetDefense(baseDefense + boost);

            while (Player.Weapon.GetCurrentWeaponId() == Items.agassword && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                int current = Player.Toan.GetDefense();
                if (current != baseDefense + boost)
                {
                    baseDefense = current;
                    Player.Toan.SetDefense(baseDefense + boost);
                }
            }

            Player.Toan.SetDefense(Player.Toan.GetDefense() - boost);
        }

        /// <summary>
        /// Triggers SeventhHeaven effect: Whenever an attachment is acquired, another one is given.
        /// </summary>
        public static void SeventhHeaven()
        {
            while ( Player.Weapon.GetCurrentWeaponId() == Items.seventhheaven &&
                    Memory.ReadByte(Addresses.mode) == 3 &&
                    Player.CheckDunIsWalkingMode() == true)
            {
                //Store the first empty slot
                int slot = Player.Inventory.GetBagAttachmentsFirstAvailableSlot();

                //Store the item in that slot (by default should always be empty)
                int oldItem = Player.Inventory.GetBagAttachments()[slot];

                Thread.Sleep(250);

                //Re-check the item again in the same slot to see if a new item has been acquired
                int newItem = Player.Inventory.GetBagAttachments()[slot];

                //Check if a non gem attachment was obtained and proceed to make a copy of it
                if (newItem != oldItem && newItem >= Items.fire && newItem <= Items.mageslayer)
                {
                    const int attachmentOffset = 0x20;
                    const int attachmentValuesRange = 0x1F;

                    //Store the newly obtained attachment values
                    byte[] attachmentValues = Memory.ReadByteArray(Addresses.firstBagAttachment + (attachmentOffset * slot), attachmentValuesRange);

                    //If the item is a gem, roll for 50% chance to duplicate
                    if (newItem >= Items.garnet && newItem <= Items.turquoise)
                    {
                        int roll = random.Next(100);

                        if (roll < 50)
                        {
                            //Put a copy of the same attachment on the next available slot
                            if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() != -1) Memory.WriteByteArray(Addresses.firstBagAttachment + (attachmentOffset * Player.Inventory.GetBagAttachmentsFirstAvailableSlot()), attachmentValues);

                            Dayuppy.DisplayMessage("The 7th Heaven has blessed\nyou with a gift!", 2, 27, 3500);
                        }
                        return;
                    }

                    //Put a copy of the same attachment on the next available slot
                    if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() != -1) Memory.WriteByteArray(Addresses.firstBagAttachment + (attachmentOffset * Player.Inventory.GetBagAttachmentsFirstAvailableSlot()), attachmentValues);

                    Dayuppy.DisplayMessage("The 7th Heaven has blessed\nyou with a gift!", 2, 27, 3500);
                }
            }
        }

        /// <summary>
        /// Triggers Chronicle effect: Attacks now hit all nearby targets for a percentage of the damage.
        /// </summary>
        public static void ChronicleSword()
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
                Memory.WriteFloat(Enemies.Enemy0.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy1.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy2.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy3.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy4.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy5.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy6.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy7.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy8.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy9.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy10.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy11.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy12.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy13.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy14.distanceToPlayer, 0);
                Memory.WriteFloat(Enemies.Enemy15.distanceToPlayer, 0);
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
                        flashRGB_R = Memory.ReadFloat(Enemies.Enemy0.flashColorRed + (i * 0x190));
                        flashRGB_G = Memory.ReadFloat(Enemies.Enemy0.flashColorGreen + (i * 0x190));
                        flashRGB_B = Memory.ReadFloat(Enemies.Enemy0.flashColorBlue + (i * 0x190));
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
                        Memory.WriteFloat(Enemies.Enemy0.flashColorRed + (0x190 * enemiesinRange[i]), flashRGB_R);
                        Memory.WriteFloat(Enemies.Enemy0.flashColorGreen + (0x190 * enemiesinRange[i]), flashRGB_G);
                        Memory.WriteFloat(Enemies.Enemy0.flashColorBlue + (0x190 * enemiesinRange[i]), flashRGB_B);
                        Memory.WriteFloat(Enemies.Enemy0.flashDuration + (0x190 * enemiesinRange[i]), (float)(0.1));


                        enemiescoordinateX[i] = Memory.ReadFloat(Enemies.Enemy0.locationCoordinateX + (0x190 * enemiesinRange[i]));
                        enemiescoordinateZ[i] = Memory.ReadFloat(Enemies.Enemy0.locationCoordinateZ + (0x190 * enemiesinRange[i]));
                        enemiescoordinateY[i] = Memory.ReadFloat(Enemies.Enemy0.locationCoordinateY + (0x190 * enemiesinRange[i]));

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
                        Memory.WriteByte(Enemies.Enemy0.flashActivation + (0x190 * enemiesinRange[i]), 1);

                        int enemyHP = Memory.ReadInt(Enemies.Enemy0.hp + (0x190 * enemiesinRange[i]));
                        int newEnemyHP = (int)(enemyHP - effectDamage[i]);
                        if (newEnemyHP < 1)
                        {
                            newEnemyHP = 1;
                            Memory.WriteByte(Enemies.Enemy0.poisonPeriod + (0x190 * enemiesinRange[i]), 1);
                        }
                        Memory.WriteInt(Enemies.Enemy0.hp + (0x190 * enemiesinRange[i]), newEnemyHP);
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

        /// <summary>
        /// Triggers Angel Gear effect: Applies the Heal regeneration effect to all allies
        /// </summary>
        public static void AngelGear()
        {
            //Initialize variables
            ushort HpValueAdd = 1;
            ushort Delay = 5000;
            ushort XiaoHp = 0;
            ushort XiaoMaxHp = 0;
            bool isHealXiao = false;

            //Run while Angel Gear is equipped and Player is in valid state
            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                //Fetch HP values for characters
                ushort ToanHp = Player.Toan.GetHp();
                ushort ToanMaxHp = Player.Toan.GetMaxHp();
                ushort GoroHp = Player.Goro.GetHp();
                ushort GoroMaxHp = Player.Goro.GetMaxHp();
                ushort RubyHp = Player.Ruby.GetHp();
                ushort RubyMaxHp = Player.Ruby.GetMaxHp();
                ushort UngagaHp = Player.Ungaga.GetHp();
                ushort UngagaMaxHp = Player.Ungaga.GetMaxHp();
                ushort OsmondHp = Player.Osmond.GetHp();
                ushort OsmondMaxHp = Player.Osmond.GetMaxHp();

                //Check for the Heal special attribute on the weapon
                if (Player.Weapon.GetCurrentWeaponSpecial2() % 16 < 8 ||
                    Player.Weapon.GetCurrentWeaponSpecial2() % 16 > 11)
                {
                    isHealXiao = true;
                    XiaoHp = Player.Xiao.GetHp();
                    XiaoMaxHp = Player.Xiao.GetMaxHp();
                }

                //Add the HP value to the characters current HP
                if (ToanHp < ToanMaxHp && ToanHp > 0) Player.Toan.SetHp((ushort)(ToanHp + HpValueAdd));
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Toan HP add: " + (ToanHp + HpValueAdd));
                if (GoroHp < GoroMaxHp && GoroHp > 0) Player.Goro.SetHp((ushort)(GoroHp + HpValueAdd));
                if (RubyHp < RubyMaxHp && RubyHp > 0) Player.Ruby.SetHp((ushort)(RubyHp + HpValueAdd));
                if (UngagaHp < UngagaMaxHp && UngagaHp > 0) Player.Ungaga.SetHp((ushort)(UngagaHp + HpValueAdd));
                if (OsmondHp < OsmondMaxHp && OsmondHp > 0) Player.Osmond.SetHp((ushort)(OsmondHp + HpValueAdd));

                //Only affect Xiao if Angel Gear does not have the Heal attribute already
                if (isHealXiao && XiaoHp < XiaoMaxHp && XiaoHp > 0) Player.Xiao.SetHp((ushort)(XiaoHp + HpValueAdd));

                //Wait in between additions
                Thread.Sleep(Delay);
            }
        }

        /// <summary>
        /// Triggers Tall Hammer effect: Reduces enemies size on hit
        /// </summary>
        public static void TallHammer()
        {
            //Offset between the enemy's dimension addresses
            int scaleOffset = MiniBoss.scaleOffset;

            //Save every enemy's HP on the current floor
            int[] formerEnemyHpList = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            //Re-save every enemy's HP on the current floor
            int[] currentEnemyHpList = ReusableFunctions.GetEnemiesHp();

            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId;

            if (hasHit)
            {
                //Store the damaged enemies ID onto a list
                List<int> enemyIds = ReusableFunctions.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

                //Run through the enemies hit
                foreach (int id in enemyIds)
                {
                    //Declare the enemy dimensions based on the enemy that got hit
                    float enemyZeroWidth = Memory.ReadFloat(0x21E18530 + (scaleOffset * id));
                    float enemyZeroHeight = Memory.ReadFloat(0x21E18534 + (scaleOffset * id));
                    float enemyZeroDepth = Memory.ReadFloat(0x21E18538 + (scaleOffset * id));

                    //Set an initial acceleration value
                    float i = 0.15f;

                    //Set a counter for how many times to change the enemy's dimensions (this acts as a duration variable)
                    int counter = 0;

                    //Instructions will run for 1000 times (arbitrary number) and only while the enemy's dimensions are between 30% - 100% of their original size
                    while (counter < 1000 && ((enemyZeroWidth >= 0.3f && enemyZeroWidth <= 1f) || (enemyZeroHeight >= 0.3f && enemyZeroHeight <= 1f) || (enemyZeroDepth >= 0.3f && enemyZeroDepth <= 1f)))
                    {
                        //Change each of the enemy axis dimensions (X,Y and Z) based on the offset from the original Enemy 0 address
                        Memory.WriteFloat(MiniBoss.enemyZeroWidth + (scaleOffset * id), enemyZeroWidth - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroHeight + (scaleOffset * id), enemyZeroHeight - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroDepth + (scaleOffset * id), enemyZeroDepth - (i * 0.0001f));
                        i++;
                        counter++;
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Inferno effect: Increase attack power depending on health and thirst
        /// </summary>
        public static void Inferno()
        {
            float goroMaxHP = Player.Goro.GetMaxHp();
            float goroCurrentHP = Player.Goro.GetHp();

            float hpPercentage = 100 - (goroCurrentHP / goroMaxHP * 100);

            float goroMaxThirst = Player.Goro.GetMaxThirst();
            float goroCurrentThirst = Player.Goro.GetThirst();

            float thirstPercentage = 100 - (goroCurrentThirst / goroMaxThirst * 100);

            ushort currentBaseAttack = Player.Weapon.GetCurrentWeaponAttack();

            ushort attachmentsAttack = 0;

            for (int i = 0; i < 4; i++)
            {
                attachmentsAttack += Memory.ReadUShort(0x21EA75C0 + (i * 0x20));
            }

            ushort currentTotalAttack = (ushort)(currentBaseAttack + attachmentsAttack);

            if (currentTotalAttack > 350)
            {
                currentTotalAttack = 350;
            }

            ushort hpAttackBoost = (ushort)((currentTotalAttack / 100) * hpPercentage);

            ushort thirstAttackBoost = (ushort)((currentTotalAttack / 100) * (thirstPercentage / 2));

            Memory.WriteUShort(0x21EA7594, (ushort)(currentTotalAttack + hpAttackBoost + thirstAttackBoost));
        }

        /// <summary>
        /// Frozen Tuna: WHP lost heals Goro's HP (fractional losses accumulate).
        /// On hit, 20% chance to stop all non-ice enemies and freeze Goro.
        /// Ice enemies (Blizzard, Sam, Ice Gemron) are immune to the stop proc.
        /// </summary>
        public static void FrozenTuna()
        {
            float hpAccumulator = 0f;

            while (Player.Weapon.GetCurrentWeaponId() == Items.frozentuna && Player.InDungeonFloor())
            {
                int[] formerHp = ReusableFunctions.GetEnemiesHp();
                float whpBefore = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());

                Thread.Sleep(50);

                int[] currentHp  = ReusableFunctions.GetEnemiesHp();
                float whpAfter   = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());

                // WHP lost → heal Goro's HP
                if (whpAfter < whpBefore)
                {
                    hpAccumulator += whpBefore - whpAfter;
                    if (hpAccumulator >= 1f)
                    {
                        int heal = (int)hpAccumulator;
                        ushort goroHp    = Player.Goro.GetHp();
                        ushort goroMaxHp = Player.Goro.GetMaxHp();
                        Player.Goro.SetHp((ushort)Math.Min(goroHp + heal, goroMaxHp));
                        hpAccumulator -= heal;
                    }
                }

                // On-hit 20% stop proc: all non-ice enemies stopped; Goro frozen too
                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId)
                {
                    bool hitDetected = false;
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            hitDetected = true;
                            break;
                        }
                    }

                    if (hitDetected && random.Next(100) < 20)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            if (Memory.ReadByte(Enemies.Enemy0.renderStatus + (Enemies.offset * i)) == 2 &&
                                !FrozenTunaIceEnemies.Contains(Memory.ReadUShort(Enemies.Enemy0.nameTag + (Enemies.offset * i))))
                            {
                                Memory.WriteUShort(Enemies.Enemy0.freezeTimer + (Enemies.offset * i), 300);
                            }
                        }
                        Player.Goro.SetStatus("freeze", 300);
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Mobius Ring effect: Increases damage output the longer you charge an attack
        /// </summary>
        public static void MobiusRing()
        {
            //Declare inputs
            string message;
            int height;
            int width;
            ushort sleep = 1500;
            int chargeGlowTimer = 0x21DC449E;
            ushort chargeTimer = 0;

            //Check these addresses which tells us if Ruby is charging an attack
            if (Player.Ruby.IsChargingAttack())
            {
                //Fetch the active orbs
                List<int> OrbIds = RubyOrbs.GetRubyActiveOrbs();

                //Initialize the damage
                int damage = Player.Weapon.GetCurrentWeaponAttack() + Player.Weapon.GetCurrentWeaponMagic();

                while (Player.Ruby.IsChargingAttack())
                {
                    //Check if the game is paused during the charge
                    if (Player.CheckDunIsPaused())
                    {
                        ReusableFunctions.AwaitUnpause(1);
                    }
                    //If the damage increase reaches the set max value, stop increasing it further
                    if (damage >= ushort.MaxValue)
                    {
                        damage = ushort.MaxValue;
                    }
                    else damage += damage / 2;

                    //Set messages to display onscreen
                    if (damage > 9000)
                    {
                        message = "Total damage is over 9000";
                        height = 1;
                        width = message.Length;
                    }
                    else
                    {
                        message = "Total damage " + damage;
                        height = 1;
                        width = message.Length;
                    }

                    //Keep looping until chargeGlowTimer reaches the value 17008 or the player stops charging
                    while (Memory.ReadUShort(chargeGlowTimer) < 17008 && Player.Ruby.IsChargingAttack())
                    {
                        if (Player.CheckDunIsPaused())
                        {
                            ReusableFunctions.AwaitUnpause(1);
                        }
                        Thread.Sleep(100);
                        continue;
                    }

                    //Save the value of the timer
                    chargeTimer = Memory.ReadUShort(chargeGlowTimer);

                    //Check if the timer hit the value we are looking for. This value makes Ruby flash
                    if (chargeTimer == 17008)
                    {
                        //Display current damage
                        Dayuppy.DisplayMessage(message, height, width, sleep + 500);

                        Thread.Sleep(sleep);

                        //Reset Flash
                        Memory.WriteUShort(chargeGlowTimer, 0);
                    }

                    Thread.Sleep(100);
                }

                //Go through the different slots that Ruby stores her attacks in memory
                foreach (int id in OrbIds)
                {
                    switch (id)
                    {
                        case 0:
                            //Check if the orb is still alive
                            while (Memory.ReadByte(RubyOrbs.Orb0.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb0.damage, damage); //Set the damage
                            }
                            break;
                        case 1:
                            while (Memory.ReadByte(RubyOrbs.Orb1.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb1.damage, damage);
                            }
                            break;
                        case 2:
                            while (Memory.ReadByte(RubyOrbs.Orb2.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb2.damage, damage);
                            }
                            break;
                        case 3:
                            while (Memory.ReadByte(RubyOrbs.Orb3.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb3.damage, damage);
                            }
                            break;
                        case 4:
                            while (Memory.ReadByte(RubyOrbs.Orb4.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb4.damage, damage);
                            }
                            break;
                        case 5:
                            while (Memory.ReadByte(RubyOrbs.Orb5.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb5.damage, damage);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Enables the Secret Armlet special effect:
        /// <br></br>
        /// <br>Makes all magic circle effects turn into positive ones.</br>
        /// </summary>
        /// <param name="isNewFloor">Determines if we have entered a new floor to know if we should run this code again.</param>
        public static bool SecretArmletEnable()
        {
            bool changed = false;

            //Check if any of the spawned circles have a negative effect and change into positive ones
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0 && Memory.ReadByte(Addresses.circleEffect1) > 4)
                { int effectPositive1 = random.Next(5); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0 && Memory.ReadByte(Addresses.circleEffect2) > 4)
                { int effectPositive2 = random.Next(5); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0 && Memory.ReadByte(Addresses.circleEffect3) > 4)
                { int effectPositive3 = random.Next(5); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect1) > 4)
                { int effectPositive4 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect2) > 4)
                { int effectPositive5 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect3) > 4)
                { int effectPositive6 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

        /// <summary>
        /// Disables the Secret Armlet special effect and re-rolls any present magic circle outcome
        /// </summary>
        public static bool SecretArmletDisable()
        {
            bool changed = false;

            //Re-roll the existing circles
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0)
                { int effectPositive1 = random.Next(10); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0)
                { int effectPositive2 = random.Next(10); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0)
                { int effectPositive3 = random.Next(10); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0)
                { int effectPositive4 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0)
                { int effectPositive5 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0)
                { int effectPositive6 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

        /// <summary>
        /// Triggers Hercules Wrath effect: Chance on getting hit to gain Stamina.
        /// </summary>
        public static void HerculesWrath()
        {
            //Check Ungaga's HP
            ushort formerHP = Memory.ReadUShort(Player.Ungaga.hp);

            Thread.Sleep(100);

            //Re-check Ungaga's HP
            ushort currentHP = Memory.ReadUShort(Player.Ungaga.hp);

            if (currentHP < formerHP)
            {
                //Declare the scale for the chance to base on (0 - 100)
                int procChance = random.Next(100);

                //Check for the chance to take effect (30 = 30%)
                if (procChance < 30)
                {
                    //Give the Stamina effect for 30 seconds (1800 = 30 sec)
                    Player.Ungaga.SetStatus("stamina", 1800);
                }
            }
        }

        /// <summary>
        /// Triggers Babel Spear effect: Chance on hit to apply stop to all enemies.
        /// </summary>
        public static void BabelSpear()
        {
            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.UngagaId;

            if (hasHit)
            {
                int procChance = random.Next(100);

                //Chance to apply stop (6%)
                if (procChance < 6)
                {
                    if(Memory.ReadByte(Enemies.Enemy0.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy0.freezeTimer, 300); //Stop duration (300 = 5 seconds)
                    if (Memory.ReadByte(Enemies.Enemy1.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy1.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy2.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy2.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy3.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy3.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy4.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy4.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy5.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy5.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy6.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy6.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy7.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy7.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy8.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy8.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy9.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy9.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy10.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy10.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy11.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy11.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy12.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy12.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy13.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy13.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy14.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy14.freezeTimer, 300);
                    if (Memory.ReadByte(Enemies.Enemy15.renderStatus) == 2) Memory.WriteUShort(Enemies.Enemy15.freezeTimer, 300);
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

        /// <summary>
        /// Cactus: on hit, restore Ungaga's thirst scaled by damage dealt.
        /// 100 damage = 10.0 thirst units = 1 visible water drop.
        /// Rock, metal, and undead types are immune.
        /// </summary>
        public static void Cactus()
        {
            while ((Player.Weapon.GetCurrentWeaponId() == Items.cactus ||
                    Player.Weapon.GetCurrentWeaponId() == Items.boneslingshot) && // TEMP: test
                   Player.InDungeonFloor())
            {
                int[] former = ReusableFunctions.GetEnemiesHp();
                Thread.Sleep(50);
                int[] current = ReusableFunctions.GetEnemiesHp();

                bool isBoneSlingshot = Player.Weapon.GetCurrentWeaponId() == Items.boneslingshot;
                int expectedSource = isBoneSlingshot ? Player.XiaoId : Player.UngagaId;
                if (ReusableFunctions.GetDamageSourceCharacterID() != expectedSource)
                {
                    ReusableFunctions.ClearRecentDamageAndDamageSource();
                    continue;
                }

                for (int i = 0; i < 15; i++)
                {
                    if (former[i] <= 0 || current[i] >= former[i])
                        continue;

                    int nameTag = Memory.ReadUShort(Enemies.Enemy0.nameTag + (Enemies.offset * i));
                    if (CactusImmuneNameTags.Contains(nameTag))
                        continue;

                    float curThirst = isBoneSlingshot ? Player.Xiao.GetThirst() : Player.Ungaga.GetThirst();
                    float maxThirst = isBoneSlingshot ? Player.Xiao.GetMaxThirst() : Player.Ungaga.GetMaxThirst();
                    if (maxThirst > 0 && curThirst >= maxThirst)
                        break;

                    float gain = (former[i] - current[i]) / 10.0f;
                    float newThirst = (maxThirst > 0)
                        ? Math.Min(curThirst + gain, maxThirst)
                        : curThirst + gain;
                    if (isBoneSlingshot) Player.Xiao.SetThirst(newThirst);
                    else Player.Ungaga.SetThirst(newThirst);
                    break;
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Supernova effect: Chance on hit to apply a random status
        /// </summary>
        public static void Supernova()
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
                        switch (id)
                        {
                            case 0:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy0.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy0.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy0.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy0.gooeyState, 1); break;
                                }
                                break;
                            case 1:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy1.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy1.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy1.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy1.gooeyState, 1); break;
                                }
                                break;
                            case 2:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy2.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy2.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy2.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy2.gooeyState, 1); break;
                                }
                                break;
                            case 3:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy3.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy3.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy3.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy3.gooeyState, 1); break;
                                }
                                break;
                            case 4:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy4.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy4.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy4.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy4.gooeyState, 1); break;
                                }
                                break;
                            case 5:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy5.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy5.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy5.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy5.gooeyState, 1); break;
                                }
                                break;
                            case 6:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy6.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy6.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy6.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy6.gooeyState, 1); break;
                                }
                                break;
                            case 7:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy7.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy7.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy7.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy7.gooeyState, 1); break;
                                }
                                break;
                            case 8:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy8.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy8.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy8.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy8.gooeyState, 1); break;
                                }
                                break;
                            case 9:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy9.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy9.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy9.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy9.gooeyState, 1); break;
                                }
                                break;
                            case 10:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy10.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy10.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy10.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy10.gooeyState, 1); break;
                                }
                                break;
                            case 11:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy11.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy11.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy11.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy11.gooeyState, 1); break;
                                }
                                break;
                            case 12:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy12.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy12.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy12.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy12.gooeyState, 1); break;
                                }
                                break;
                            case 13:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy13.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy13.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy13.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy13.gooeyState, 1); break;
                                }
                                break;
                            case 14:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy14.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy14.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy14.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy14.gooeyState, 1); break;
                                }
                                break;
                            case 15:
                                switch (effect)
                                {
                                    case 0: Memory.WriteUShort(Enemies.Enemy15.freezeTimer, 300); break;
                                    case 1: Memory.WriteUShort(Enemies.Enemy15.poisonPeriod, 1); break;
                                    case 2: Memory.WriteUShort(Enemies.Enemy15.staminaTimer, 300); break;
                                    case 3: Memory.WriteUShort(Enemies.Enemy15.gooeyState, 1); break;
                                }
                                break;
                        }
                    }
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

        /// <summary>
        /// Triggers StarBreaker effect: Chance on kill to get an empty synthsphere (Breaks down any weapon)
        /// </summary>
        public static void StarBreaker()
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

    }
}
