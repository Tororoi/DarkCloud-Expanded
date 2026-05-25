using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public partial class MiniBoss
    {
        static Random rnd = new Random();

        public const int enemyZeroWidth = 0x21E18530;  //Enemy Width multiplier
        public const int enemyZeroHeight = 0x21E18534; //Enemy Height multiplier
        public const int enemyZeroDepth = 0x21E18538;  //Enemy Depth multiplier
        public const int scaleOffset = 0x3510;         //Offset for size
        public static int enemyNumber = 0;
        public static bool miniBossRolled = false;
        const int varOffset = 0x190;            //Offset for attributes
        const float scaleSize = 1.5F;           //Sets the total size of the miniboss
        const int enemyHPMult = 5;              //Miniboss HP multiplier
        const int enemyABSMult = 5;             //Miniboss ABS multiplier
        const int enemyItemResistMulti = 10;    //Miniboss Item Resistance multiplier %
        const int enemyGoldMult = 5;            //Miniboss Gilda Drop multiplier
        const int enemyDropChance = 100;        //Miniboss Drop chance % (0 - 100)
        const byte staminaTimer = 79;           //Miniboss Stamina Timer (Currently 79 on the 3rd byte is roughly 1 day)

        static Dictionary<ushort, string> nonKeyEnemies = Enemies.GetFlyingEnemies();

        /// <summary>
        /// Picks and transforms an enemy on the current floor to become a Champion (Miniboss).
        /// </summary>
        /// <param name="skipFirstRoll">To skip the spawning chance roll.</param>
        /// <param name="dungeon">The number of the current dungeon.</param>
        /// <param name="floor">The number of the current floor.</param>
        /// <returns></returns>
        public static bool MiniBossSpawn(bool skipFirstRoll = false, byte dungeon = 255, byte floor = 255)
        {
            //Rolls for a 30% chance to spawn the miniboss
            if (rnd.Next(100) <= 30 || skipFirstRoll)
            {

                if (skipFirstRoll == false)
                {
                    Thread.Sleep(200);
                }

                //Choose the enemy to convert into mini boss
                enemyNumber = rnd.Next(Enemies.GetFloorEnemiesIds().Count);

                //Check if the chosen enemy has an ID
                if (Enemies.GetFloorEnemyId(enemyNumber) > 0)
                {
                    //Check if chosen enemy is flying type
                    if (!nonKeyEnemies.ContainsKey(Enemies.GetFloorEnemyId(enemyNumber)))
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +   "\nEnemyNumber rolled after flying check: " + Enemies.GetFloorEnemyId(enemyNumber) + "" +
                                                                                    "\nIs flying enemy: " + nonKeyEnemies.ContainsKey(Enemies.GetFloorEnemyId(enemyNumber)) +
                                                                                    "\nChosen miniboss ID: " + Enemies.GetFloorEnemyId(enemyNumber) + "\n");

                        //Check if chosen enemy has the key
                        if (Enemies.EnemyHasKey(enemyNumber, dungeon))
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "The Key has landed on the mini boss!");

                            int newEnemyNumber;

                            //Get the enemy key ID
                            ushort KeyId = Memory.ReadUShort(Enemies.Enemy0.forceItemDrop + (varOffset * enemyNumber));

                            //Re-roll for a different enemy that does not hold the key (due to Wise Owl) and is non flying
                            do { newEnemyNumber = rnd.Next(Enemies.GetFloorEnemiesIds().Count); } while (newEnemyNumber == enemyNumber &&
                                                                                                                Enemies.EnemyHasKey(newEnemyNumber, dungeon) &&
                                                                                                                nonKeyEnemies.ContainsKey(Enemies.GetFloorEnemyId(newEnemyNumber)));

                            //Remove the key from the original enemy
                            Memory.WriteUShort(Enemies.Enemy0.forceItemDrop + (varOffset * enemyNumber), 0);

                            //Set the key onto a new enemy
                            Memory.WriteUShort(Enemies.Enemy0.forceItemDrop + (varOffset * newEnemyNumber), KeyId);
                        }

                        //  == Get base values from the chosen enemy ==
                        int startBossHP = Memory.ReadInt(Enemies.Enemy0.hp + (varOffset * enemyNumber));
                        int startAbs = Memory.ReadInt(Enemies.Enemy0.abs + (varOffset * enemyNumber));
                        int startGold = Memory.ReadInt(Enemies.Enemy0.minGoldDrop + (varOffset * enemyNumber));

                        // === Set mini boss new stats ===
                        Memory.WriteFloat(enemyZeroWidth + (scaleOffset * enemyNumber), scaleSize);                         //Scales Width
                        Memory.WriteFloat(enemyZeroHeight + (scaleOffset * enemyNumber), scaleSize);                        //Scales Height
                        Memory.WriteFloat(enemyZeroDepth + (scaleOffset * enemyNumber), scaleSize);                         //Scales Depth
                        Memory.WriteInt(Enemies.Enemy0.hp + (varOffset * enemyNumber), (startBossHP * enemyHPMult));        //Changes Enemy HP
                        Memory.WriteInt(Enemies.Enemy0.maxHp + (varOffset * enemyNumber), (startBossHP * enemyHPMult));     //Changes MaxHP
                        Memory.WriteInt(Enemies.Enemy0.abs + (varOffset * enemyNumber), (startAbs * enemyABSMult));         //Changes ABS reward
                        Memory.WriteInt(Enemies.Enemy0.itemResistance + (varOffset * enemyNumber), enemyItemResistMulti);   //Changes the enemies item resistance
                        Memory.WriteInt(Enemies.Enemy0.minGoldDrop + (varOffset * enemyNumber), startGold * enemyGoldMult); //Changes the enemies gilda drop amount
                        Memory.WriteInt(Enemies.Enemy0.dropChance + (varOffset * enemyNumber), enemyDropChance);            //Changes the enemies drop chance
                        Memory.WriteByte(Enemies.Enemy0.staminaTimer + (varOffset * enemyNumber) + 0x2, staminaTimer);      //Changes the enemies stamina timer


                        // === Set mini boss new item ===

                        int[] weaponTable = CustomChests.GetDungeonWeaponsTable(dungeon, floor);
                        ushort enemyTypeId = Enemies.GetFloorEnemyId(enemyNumber);

                        //Check for enemy-specific flavor drops before standard rolls
                        if (!TryApplyFlavorDrop(enemyTypeId, dungeon, enemyNumber))
                        {
                            //Roll first for the backfloor key
                            if (rnd.Next(100) < 35)
                            {
                                WriteDropItem(Dungeon.GetDungeonBackFloorKey(dungeon), enemyNumber);
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with backfloor key!");
                            }
                            //If backfloor key roll fails, roll for weapon
                            else if (rnd.Next(100) < 15)
                            {
                                WriteDropItem(weaponTable[rnd.Next(weaponTable.Length)], enemyNumber);
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with weapon!");
                            }
                            //If weapon roll fails, roll for attachments
                            else if (rnd.Next(100) < 80)
                            {
                                if (rnd.Next(100) < 60) WriteDropItem(attachmentsTableLucky[rnd.Next(attachmentsTableLucky.Length)], enemyNumber);
                                else WriteDropItem(attachmentsTableUnlucky[rnd.Next(attachmentsTableUnlucky.Length)], enemyNumber);
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with attachment!");
                            }
                            else //If previous rolls fail, default to items
                            {
                                if (rnd.Next(100) < 60) WriteDropItem(itemTableLucky[rnd.Next(itemTableLucky.Length)], enemyNumber);
                                else WriteDropItem(itemTableUnlucky[rnd.Next(itemTableUnlucky.Length)], enemyNumber);
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with item!");
                            }
                        }

                        miniBossRolled = true;
                        return true;
                    }
                    //Retry if landing on a flying enemy
                    else { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " Miniboss landed on flying enemy!"); MiniBossSpawn(true, dungeon, floor); return true; }
                }
                //Retry if landing on a enemy with ID 0
                else { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Chosen enemy ID must not be 0!"); MiniBossSpawn(true, dungeon, floor); return true; }
            }
            else
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Failed to roll for Mini Boss!");
                miniBossRolled = false;
            }

            return false;
        }

        /// <summary>
        /// Rolls for and applies a dungeon-specific flavor drop to the miniboss.
        /// Checks flavorRare (5%) first, then flavorDrop (30%).
        /// Returns true if a special drop was applied.
        /// </summary>
        private static bool TryApplyFlavorDrop(ushort enemyTypeId, byte dungeon, int enemyNum)
        {
            Dictionary<ushort, int[]> rareDrops;
            Dictionary<ushort, int[]> flavorDrops;
            Dictionary<int, WeaponBoostData> rareBoosts;
            Dictionary<int, WeaponBoostData> flavorBoosts;

            switch (dungeon)
            {
                case 0: rareDrops = dbcFlavorRareDrops;  flavorDrops = dbcFlavorDrops;  rareBoosts = dbcWeaponBoosts;  flavorBoosts = emptyWeaponBoosts;        break;
                case 1: rareDrops = wofFlavorRareDrops;  flavorDrops = wofFlavorDrops;  rareBoosts = wofWeaponBoosts;  flavorBoosts = emptyWeaponBoosts;        break;
                case 2: rareDrops = shipFlavorRareDrops; flavorDrops = shipFlavorDrops; rareBoosts = shipWeaponBoosts; flavorBoosts = emptyWeaponBoosts;        break;
                case 3: rareDrops = sunFlavorRareDrops;  flavorDrops = sunFlavorDrops;  rareBoosts = sunWeaponBoosts;  flavorBoosts = emptyWeaponBoosts;        break;
                case 4: rareDrops = moonFlavorRareDrops; flavorDrops = moonFlavorDrops; rareBoosts = moonWeaponBoosts; flavorBoosts = moonFlavorWeaponBoosts;   break;
                case 5: rareDrops = galFlavorRareDrops;  flavorDrops = galFlavorDrops;  rareBoosts = galWeaponBoosts;  flavorBoosts = galFlavorWeaponBoosts;    break;
                case 6: rareDrops = dsFlavorRareDrops;   flavorDrops = dsFlavorDrops;   rareBoosts = dsWeaponBoosts;   flavorBoosts = dsFlavorWeaponBoosts;      break;
                default: return false;
            }

            if (rareDrops.TryGetValue(enemyTypeId, out int[] rarePool) && rnd.Next(100) < 5)
            {
                int rareItem = rarePool[rnd.Next(rarePool.Length)];
                WriteDropItem(rareItem, enemyNum);
                if (rareBoosts.TryGetValue(rareItem, out WeaponBoostData boost))
                    StartInventoryBoostMonitor(boost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor rare drop!");
                return true;
            }

            if (flavorDrops.TryGetValue(enemyTypeId, out int[] flavorPool) && rnd.Next(100) < 30)
            {
                int flavorItem = flavorPool[rnd.Next(flavorPool.Length)];
                WriteDropItem(flavorItem, enemyNum);
                if (flavorBoosts.TryGetValue(flavorItem, out WeaponBoostData flavorBoost))
                    StartInventoryBoostMonitor(flavorBoost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor drop!");
                return true;
            }

            return false;
        }

        // Weapons start at ID 257 in Items.cs; they require a 32-bit write.
        private static void WriteDropItem(int itemId, int enemyNum)
        {
            int dropAddress = Enemies.Enemy0.forceItemDrop + (varOffset * enemyNum);
            if (itemId >= 257)
                Memory.WriteInt(dropAddress, itemId);
            else
                Memory.WriteUShort(dropAddress, (ushort)itemId);
        }

        private static void StartInventoryBoostMonitor(WeaponBoostData boost, int enemyNum)
        {
            pendingBoost = boost;
            pendingBoostActive = true;
            boostMonitorThread = new Thread(() => InventoryBoostMonitorLoop(enemyNum)) { IsBackground = true };
            boostMonitorThread.Start();
        }

        private static void InventoryBoostMonitorLoop(int enemyNum)
        {
            const int weaponOffset     = 0xF8;
            const int attackOffset     = 0x04;
            const int enduranceOffset  = 0x06;
            const int magicOffset      = 0x0A;
            const int whpMaxOffset     = 0x0C;
            const int whpOffset        = 0x10;
            const int fireOffset       = 0x17;
            const int iceOffset        = 0x18;
            const int thunderOffset    = 0x19;
            const int windOffset       = 0x1A;
            const int holyOffset       = 0x1B;
            const int antiDragonOffset = 0x1C;
            const int antiUndeadOffset = 0x1D;
            const int antiMarineOffset = 0x1E;
            const int antiRockOffset   = 0x1F;
            const int antiPlantOffset  = 0x20;
            const int antiBeastOffset  = 0x21;
            const int antiSkyOffset    = 0x22;
            const int antiMimicOffset  = 0x24;
            const int antiMageOffset   = 0x25;
            const int special1Offset   = 0xEE;
            const int special2Offset   = 0xEF;
            const int toanSlots = 10;

            // Phase 1: wait for the miniboss to die so any weapon already in
            // inventory (e.g. from a chest) is present in the snapshot we take next.
            int hpAddress = Enemies.Enemy0.hp + (varOffset * enemyNum);
            int elapsed = 0;
            while (pendingBoostActive && Memory.ReadInt(hpAddress) > 0 && elapsed < 300000)
            {
                Thread.Sleep(250);
                elapsed += 250;
            }

            if (!pendingBoostActive) return;

            if (elapsed >= 300000)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Weapon boost monitor timed out waiting for miniboss death.");
                pendingBoostActive = false;
                return;
            }

            // Phase 2: snapshot inventory now (post-kill), then watch until the
            // dropped weapon is picked up or the floor changes (pendingBoostActive → false).
            ushort[] snapshot = new ushort[toanSlots];
            for (int i = 0; i < toanSlots; i++)
                snapshot[i] = Memory.ReadUShort(Addresses.firstBagWeapon + (weaponOffset * i));

            while (pendingBoostActive)
            {
                Thread.Sleep(250);

                for (int i = 0; i < toanSlots; i++)
                {
                    int slotBase = Addresses.firstBagWeapon + (weaponOffset * i);
                    ushort current = Memory.ReadUShort(slotBase);
                    if (current == pendingBoost.WeaponId && snapshot[i] != pendingBoost.WeaponId)
                    {
                        if (pendingBoost.Attack > 0)     Memory.WriteUShort(slotBase + attackOffset,    pendingBoost.Attack);
                        if (pendingBoost.Endurance > 0)  Memory.WriteUShort(slotBase + enduranceOffset, pendingBoost.Endurance);
                        if (pendingBoost.Magic > 0)      Memory.WriteUShort(slotBase + magicOffset,     pendingBoost.Magic);
                        if (pendingBoost.Whp > 0)      { Memory.WriteUShort(slotBase + whpMaxOffset,    pendingBoost.Whp); Memory.WriteUShort(slotBase + whpOffset, pendingBoost.Whp); }
                        if (pendingBoost.Fire > 0)       Memory.WriteByte(slotBase + fireOffset,        pendingBoost.Fire);
                        if (pendingBoost.Ice > 0)        Memory.WriteByte(slotBase + iceOffset,         pendingBoost.Ice);
                        if (pendingBoost.Thunder > 0)    Memory.WriteByte(slotBase + thunderOffset,     pendingBoost.Thunder);
                        if (pendingBoost.Wind > 0)       Memory.WriteByte(slotBase + windOffset,        pendingBoost.Wind);
                        if (pendingBoost.Holy > 0)       Memory.WriteByte(slotBase + holyOffset,        pendingBoost.Holy);
                        if (pendingBoost.AntiDragon > 0) Memory.WriteByte(slotBase + antiDragonOffset,  pendingBoost.AntiDragon);
                        if (pendingBoost.AntiUndead > 0) Memory.WriteByte(slotBase + antiUndeadOffset,  pendingBoost.AntiUndead);
                        if (pendingBoost.AntiMarine > 0) Memory.WriteByte(slotBase + antiMarineOffset,  pendingBoost.AntiMarine);
                        if (pendingBoost.AntiRock > 0)   Memory.WriteByte(slotBase + antiRockOffset,    pendingBoost.AntiRock);
                        if (pendingBoost.AntiPlant > 0)  Memory.WriteByte(slotBase + antiPlantOffset,   pendingBoost.AntiPlant);
                        if (pendingBoost.AntiBeast > 0)  Memory.WriteByte(slotBase + antiBeastOffset,   pendingBoost.AntiBeast);
                        if (pendingBoost.AntiSky > 0)    Memory.WriteByte(slotBase + antiSkyOffset,     pendingBoost.AntiSky);
                        if (pendingBoost.AntiMimic > 0)  Memory.WriteByte(slotBase + antiMimicOffset,   pendingBoost.AntiMimic);
                        if (pendingBoost.AntiMage > 0)   Memory.WriteByte(slotBase + antiMageOffset,    pendingBoost.AntiMage);
                        if (pendingBoost.Special1 > 0)   Memory.WriteByte(slotBase + special1Offset,    pendingBoost.Special1);
                        if (pendingBoost.Special2 > 0)   Memory.WriteByte(slotBase + special2Offset,    pendingBoost.Special2);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Weapon boost applied to bag slot {i}:" +
                            (pendingBoost.Attack > 0     ? $" atk={pendingBoost.Attack}"              : "") +
                            (pendingBoost.Endurance > 0  ? $" end={pendingBoost.Endurance}"           : "") +
                            (pendingBoost.Magic > 0      ? $" mp={pendingBoost.Magic}"                : "") +
                            (pendingBoost.Whp > 0        ? $" whp={pendingBoost.Whp}"                 : "") +
                            (pendingBoost.Fire > 0       ? $" fire={pendingBoost.Fire}"               : "") +
                            (pendingBoost.Ice > 0        ? $" ice={pendingBoost.Ice}"                 : "") +
                            (pendingBoost.Thunder > 0    ? $" thunder={pendingBoost.Thunder}"         : "") +
                            (pendingBoost.Wind > 0       ? $" wind={pendingBoost.Wind}"               : "") +
                            (pendingBoost.Holy > 0       ? $" holy={pendingBoost.Holy}"               : "") +
                            (pendingBoost.AntiDragon > 0 ? $" aDragon={pendingBoost.AntiDragon}"      : "") +
                            (pendingBoost.AntiUndead > 0 ? $" aUndead={pendingBoost.AntiUndead}"      : "") +
                            (pendingBoost.AntiMarine > 0 ? $" aMarine={pendingBoost.AntiMarine}"      : "") +
                            (pendingBoost.AntiRock > 0   ? $" aRock={pendingBoost.AntiRock}"          : "") +
                            (pendingBoost.AntiPlant > 0  ? $" aPlant={pendingBoost.AntiPlant}"        : "") +
                            (pendingBoost.AntiBeast > 0  ? $" aBeast={pendingBoost.AntiBeast}"        : "") +
                            (pendingBoost.AntiSky > 0    ? $" aSky={pendingBoost.AntiSky}"            : "") +
                            (pendingBoost.AntiMimic > 0  ? $" aMimic={pendingBoost.AntiMimic}"        : "") +
                            (pendingBoost.AntiMage > 0   ? $" aMage={pendingBoost.AntiMage}"          : "") +
                            (pendingBoost.Special1 > 0   ? $" sp1=0x{pendingBoost.Special1:X2}"       : "") +
                            (pendingBoost.Special2 > 0   ? $" sp2=0x{pendingBoost.Special2:X2}"       : ""));
                        pendingBoostActive = false;
                        return;
                    }
                }
            }
        }
    }
}
