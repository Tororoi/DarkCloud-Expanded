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
        public static List<int> miniBossEnemyNumbers = new List<int>();
        public static bool miniBossRolled = false;

        const float scaleSize = 1.5F;           //Sets the total size of the miniboss
        const int enemyHPMult = 4;              //Miniboss HP multiplier
        const int enemyABSMult = 4;             //Miniboss ABS multiplier
        const int enemyItemResistMulti = 10;    //Miniboss Item Resistance multiplier %
        const int enemyGoldMult = 4;            //Miniboss Gilda Loot multiplier
        const int enemyLootChance = 100;        //Miniboss Loot chance % (0 - 100)
        const byte staminaTimer = 79;           //Miniboss Stamina Timer (Currently 79 on the 3rd byte is roughly 1 day)

        static Dictionary<ushort, string> nonKeyEnemies = EnemySlots.GetFlyingEnemies();

        public class MiniBossSnapshot
        {
            public int Slot;
            public ushort TypeId;
        }

        /// <summary>
        /// Picks and transforms enemies on the current floor into Champions (Minibosses).
        /// Each eligible enemy (non-flying, non-zero ID, no forced item drop) has a
        /// 1-in-(15 minus ineligible count) chance of being chosen. All winners are transformed.
        /// </summary>
        public static bool MiniBossSpawn(byte dungeon = 255, byte floor = 255)
        {
            Thread.Sleep(200);

            miniBossEnemyNumbers.Clear();

            // Count all enemies ineligible to become a miniboss (ID 0, flying, or has a forced item drop)
            int ineligibleCount = 0;
            List<ushort> allIds = EnemySlots.GetFloorEnemiesIds();
            for (int i = 0; i < allIds.Count; i++)
            {
                ushort id = allIds[i];
                ushort dropVal = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop));
                if (id == 0 || nonKeyEnemies.ContainsKey(id) || Enemies.BossEnemies.ContainsKey(id) || (dropVal != 0 && dropVal != 65535))
                    ineligibleCount++;
            }

            int denominator = Math.Max(1, 15 - ineligibleCount);

            // Roll 1-in-denominator for each eligible enemy; all winners become minibosses
            List<int> winners = new List<int>();
            for (int i = 0; i < allIds.Count; i++)
            {
                ushort id = allIds[i];
                if (id == 0) continue;
                if (nonKeyEnemies.ContainsKey(id)) continue;
                if (Enemies.BossEnemies.ContainsKey(id)) continue;   // never promote a boss/boss-companion (e.g. Ice Queen 113, IceArrow 84) to a miniboss
                ushort dropVal = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop));
                if (dropVal != 0 && dropVal != 65535) continue;
                if (rnd.Next(denominator) == 0)
                    winners.Add(i);
            }

            if (winners.Count == 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Failed to roll for Mini Boss!");
                miniBossRolled = false;
                return false;
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "\nIneligible count: " + ineligibleCount +
                "\nDenominator (1-in-N): " + denominator +
                "\nWinners: " + winners.Count + "\n");

            foreach (int slot in winners)
                ApplyMiniBossToSlot(slot, dungeon, floor);

            miniBossRolled = true;
            return true;
        }

        /// <summary>
        /// Applies miniboss stat multipliers and rolls loot for a single enemy slot.
        /// Also registers the slot in miniBossEnemyNumbers.
        /// </summary>
        private static void ApplyMiniBossToSlot(int slot, byte dungeon, byte floor)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "\nApplying miniboss to slot: " + slot +
                "\nEnemy ID: " + EnemySlots.GetFloorEnemyId(slot) + "\n");

            // Eligibility filtering should prevent a key-holder from ever being chosen.
            // This block should be unreachable — log a warning if it fires.
            if (EnemySlots.EnemyHasKey(slot, dungeon))
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss ApplyMiniBossToSlot: slot " + slot + " holds a key — this should not happen with the current eligibility filter!");

            int startBossHP = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MaxHp));
            int startAbs    = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Abs));
            int startGold   = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MinGoldDrop));

            Memory.WriteFloat(enemyZeroWidth  + (scaleOffset * slot), scaleSize);
            Memory.WriteFloat(enemyZeroHeight + (scaleOffset * slot), scaleSize);
            Memory.WriteFloat(enemyZeroDepth  + (scaleOffset * slot), scaleSize);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp),           startBossHP * enemyHPMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MaxHp),        startBossHP * enemyHPMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Abs),          startAbs * enemyABSMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ItemResistance), enemyItemResistMulti);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MinGoldDrop),  startGold * enemyGoldMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.DropChance),   enemyLootChance);
            Memory.WriteByte(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.StaminaTimer) + 0x2, staminaTimer);

            int[] weaponTable  = CustomChests.GetDungeonWeaponsTable(dungeon, floor);
            ushort enemySpeciesId = EnemySlots.GetFloorEnemyId(slot);

            if (!TryApplyFlavorLoot(enemySpeciesId, dungeon, slot))
            {
                if (rnd.Next(100) < 35)
                {
                    WriteLootItem(Dungeon.GetDungeonBackFloorKey(dungeon), slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with backfloor key!");
                }
                else if (rnd.Next(100) < 15)
                {
                    WriteLootItem(weaponTable[rnd.Next(weaponTable.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with weapon!");
                }
                else if (rnd.Next(100) < 80)
                {
                    if (rnd.Next(100) < 60) WriteLootItem(MiniBossLootTables.attachmentsTableLucky[rnd.Next(MiniBossLootTables.attachmentsTableLucky.Length)], slot);
                    else                    WriteLootItem(MiniBossLootTables.attachmentsTableUnlucky[rnd.Next(MiniBossLootTables.attachmentsTableUnlucky.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with attachment!");
                }
                else
                {
                    if (rnd.Next(100) < 60) WriteLootItem(MiniBossLootTables.itemTableLucky[rnd.Next(MiniBossLootTables.itemTableLucky.Length)], slot);
                    else                    WriteLootItem(MiniBossLootTables.itemTableUnlucky[rnd.Next(MiniBossLootTables.itemTableUnlucky.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with item!");
                }
            }

            miniBossEnemyNumbers.Add(slot);
        }

        /// <summary>
        /// Snapshots all currently alive minibosses, clears miniboss state, and returns the snapshot.
        /// Returns an empty list (never null) if no minibosses are alive.
        /// </summary>
        public static List<MiniBossSnapshot> TakeSnapshot()
        {
            var snap = new List<MiniBossSnapshot>();
            foreach (int slot in miniBossEnemyNumbers)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) > 0)
                    snap.Add(new MiniBossSnapshot { Slot = slot, TypeId = EnemySlots.GetFloorEnemyId(slot) });
            }
            miniBossRolled = false;
            miniBossEnemyNumbers.Clear();
            return snap;
        }

        /// <summary>
        /// Restores minibosses from a previous snapshot, re-applying stats to each alive entry.
        /// If a restored enemy was assigned the key during respawn, the key is transferred to
        /// another non-miniboss, non-key enemy before applying stats.
        /// </summary>
        public static void RestoreFromSnapshot(List<MiniBossSnapshot> snapshot, byte dungeon, byte floor)
        {
            if (snapshot == null || snapshot.Count == 0) return;

            // Build the set of slots being restored so key transfer avoids them
            var restoredSlots = new HashSet<int>();
            foreach (var entry in snapshot)
            {
                if (EnemySlots.GetFloorEnemyId(entry.Slot) == entry.TypeId)
                    restoredSlots.Add(entry.Slot);
            }

            foreach (var entry in snapshot)
            {
                if (EnemySlots.GetFloorEnemyId(entry.Slot) != entry.TypeId)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss restore: type mismatch at slot " + entry.Slot + ", skipping.");
                    continue;
                }

                // If the respawned enemy was assigned the key, transfer it away before applying stats
                if (EnemySlots.EnemyHasKey(entry.Slot, dungeon))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss restore: key conflict at slot " + entry.Slot + ", transferring key.");
                    ushort keyId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(entry.Slot, EnemySlotOffsets.ForceItemDrop));
                    int newSlot = -1;
                    for (int i = 0; i < 15; i++)
                    {
                        if (restoredSlots.Contains(i) || i == entry.Slot) continue;
                        if (EnemySlots.GetFloorEnemyId(i) == 0) continue;
                        if (EnemySlots.EnemyHasKey(i, dungeon)) continue;
                        newSlot = i;
                        break;
                    }
                    if (newSlot >= 0)
                    {
                        Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(entry.Slot, EnemySlotOffsets.ForceItemDrop), 0);
                        Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(newSlot, EnemySlotOffsets.ForceItemDrop), keyId);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Key transferred to slot " + newSlot);
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss restore: no valid slot found for key transfer!");
                    }
                }

                ApplyMiniBossToSlot(entry.Slot, dungeon, floor);
            }

            if (miniBossEnemyNumbers.Count > 0)
                miniBossRolled = true;
        }

        /// <summary>
        /// Rolls for and applies a dungeon-specific flavor loot to the miniboss.
        /// Checks flavorRare (5%) first, then flavorLoot (30%).
        /// Returns true if special loot was applied.
        /// </summary>
        private static bool TryApplyFlavorLoot(ushort enemySpeciesId, byte dungeon, int enemyNum)
        {
            Dictionary<ushort, int[]> rareLoot;
            Dictionary<ushort, int[]> flavorLoot;
            Dictionary<int, MiniBossLootTables.WeaponBoostData> rareBoosts;
            Dictionary<int, MiniBossLootTables.WeaponBoostData> flavorBoosts;

            switch (dungeon)
            {
                case 0: rareLoot = MiniBossLootTables.dbcFlavorRareLoot;  flavorLoot = MiniBossLootTables.dbcFlavorLoot;  rareBoosts = MiniBossLootTables.dbcWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 1: rareLoot = MiniBossLootTables.wofFlavorRareLoot;  flavorLoot = MiniBossLootTables.wofFlavorLoot;  rareBoosts = MiniBossLootTables.wofWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 2: rareLoot = MiniBossLootTables.shipFlavorRareLoot; flavorLoot = MiniBossLootTables.shipFlavorLoot; rareBoosts = MiniBossLootTables.shipWeaponBoosts; flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 3: rareLoot = MiniBossLootTables.sunFlavorRareLoot;  flavorLoot = MiniBossLootTables.sunFlavorLoot;  rareBoosts = MiniBossLootTables.sunWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 4: rareLoot = MiniBossLootTables.moonFlavorRareLoot; flavorLoot = MiniBossLootTables.moonFlavorLoot; rareBoosts = MiniBossLootTables.moonWeaponBoosts; flavorBoosts = MiniBossLootTables.moonFlavorWeaponBoosts;   break;
                case 5: rareLoot = MiniBossLootTables.galFlavorRareLoot;  flavorLoot = MiniBossLootTables.galFlavorLoot;  rareBoosts = MiniBossLootTables.galWeaponBoosts;  flavorBoosts = MiniBossLootTables.galFlavorWeaponBoosts;    break;
                case 6: rareLoot = MiniBossLootTables.dsFlavorRareLoot;   flavorLoot = MiniBossLootTables.dsFlavorLoot;   rareBoosts = MiniBossLootTables.dsWeaponBoosts;   flavorBoosts = MiniBossLootTables.dsFlavorWeaponBoosts;      break;
                default: return false;
            }

            if (rareLoot.TryGetValue(enemySpeciesId, out int[] rarePool) && rnd.Next(100) < 5)
            {
                int rareItem = rarePool[rnd.Next(rarePool.Length)];
                WriteLootItem(rareItem, enemyNum);
                if (rareBoosts.TryGetValue(rareItem, out MiniBossLootTables.WeaponBoostData boost))
                    StartInventoryBoostMonitor(boost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor rare loot!");
                return true;
            }

            if (flavorLoot.TryGetValue(enemySpeciesId, out int[] flavorPool) && rnd.Next(100) < 30)
            {
                int flavorItem = flavorPool[rnd.Next(flavorPool.Length)];
                WriteLootItem(flavorItem, enemyNum);
                if (flavorBoosts.TryGetValue(flavorItem, out MiniBossLootTables.WeaponBoostData flavorBoost))
                    StartInventoryBoostMonitor(flavorBoost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor loot!");
                return true;
            }

            return false;
        }

        // Weapons start at ID 257 in Items.cs; they require a 32-bit write.
        private static void WriteLootItem(int itemId, int enemyNum)
        {
            int lootAddress = EnemyAddresses.FloorSlots.SlotAddr(enemyNum, EnemySlotOffsets.ForceItemDrop);
            if (itemId >= 257)
                Memory.WriteInt(lootAddress, itemId);
            else
                Memory.WriteUShort(lootAddress, (ushort)itemId);
        }

        private static void StartInventoryBoostMonitor(MiniBossLootTables.WeaponBoostData boost, int enemyNum)
        {
            MiniBossLootTables.pendingBoost = boost;
            MiniBossLootTables.pendingBoostActive = true;
            MiniBossLootTables.boostMonitorThread = new Thread(() => InventoryBoostMonitorLoop(enemyNum)) { IsBackground = true };
            MiniBossLootTables.boostMonitorThread.Start();
        }

        private static void InventoryBoostMonitorLoop(int enemyNum)
        {
            const int weaponOffset     = 0xF8;
            const int attackOffset     = 0x04;
            const int enduranceOffset  = 0x06;
            const int speedOffset      = 0x08;
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
            int hpAddress = EnemyAddresses.FloorSlots.SlotAddr(enemyNum, EnemySlotOffsets.Hp);
            int elapsed = 0;
            while (MiniBossLootTables.pendingBoostActive && Memory.ReadInt(hpAddress) > 0 && elapsed < 300000)
            {
                Thread.Sleep(250);
                elapsed += 250;
            }

            if (!MiniBossLootTables.pendingBoostActive) return;

            if (elapsed >= 300000)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Weapon boost monitor timed out waiting for miniboss death.");
                MiniBossLootTables.pendingBoostActive = false;
                return;
            }

            // Phase 2: snapshot inventory now (post-kill), then watch until the
            // dropped weapon is picked up or the floor changes (MiniBossLootTables.pendingBoostActive → false).
            ushort[] snapshot = new ushort[toanSlots];
            for (int i = 0; i < toanSlots; i++)
                snapshot[i] = Memory.ReadUShort(Addresses.firstBagWeapon + (weaponOffset * i));

            while (MiniBossLootTables.pendingBoostActive)
            {
                Thread.Sleep(250);

                for (int i = 0; i < toanSlots; i++)
                {
                    int slotBase = Addresses.firstBagWeapon + (weaponOffset * i);
                    ushort current = Memory.ReadUShort(slotBase);
                    if (current == MiniBossLootTables.pendingBoost.WeaponId && snapshot[i] != MiniBossLootTables.pendingBoost.WeaponId)
                    {
                        if (MiniBossLootTables.pendingBoost.Attack > 0)     Memory.WriteUShort(slotBase + attackOffset,    MiniBossLootTables.pendingBoost.Attack);
                        if (MiniBossLootTables.pendingBoost.Endurance > 0)  Memory.WriteUShort(slotBase + enduranceOffset, MiniBossLootTables.pendingBoost.Endurance);
                        if (MiniBossLootTables.pendingBoost.Speed > 0)      Memory.WriteUShort(slotBase + speedOffset,     MiniBossLootTables.pendingBoost.Speed);
                        if (MiniBossLootTables.pendingBoost.Magic > 0)      Memory.WriteUShort(slotBase + magicOffset,     MiniBossLootTables.pendingBoost.Magic);
                        if (MiniBossLootTables.pendingBoost.Whp > 0)      { Memory.WriteUShort(slotBase + whpMaxOffset,    MiniBossLootTables.pendingBoost.Whp); Memory.WriteUShort(slotBase + whpOffset, MiniBossLootTables.pendingBoost.Whp); }
                        if (MiniBossLootTables.pendingBoost.Fire > 0)       Memory.WriteByte(slotBase + fireOffset,        MiniBossLootTables.pendingBoost.Fire);
                        if (MiniBossLootTables.pendingBoost.Ice > 0)        Memory.WriteByte(slotBase + iceOffset,         MiniBossLootTables.pendingBoost.Ice);
                        if (MiniBossLootTables.pendingBoost.Thunder > 0)    Memory.WriteByte(slotBase + thunderOffset,     MiniBossLootTables.pendingBoost.Thunder);
                        if (MiniBossLootTables.pendingBoost.Wind > 0)       Memory.WriteByte(slotBase + windOffset,        MiniBossLootTables.pendingBoost.Wind);
                        if (MiniBossLootTables.pendingBoost.Holy > 0)       Memory.WriteByte(slotBase + holyOffset,        MiniBossLootTables.pendingBoost.Holy);
                        if (MiniBossLootTables.pendingBoost.AntiDragon > 0) Memory.WriteByte(slotBase + antiDragonOffset,  MiniBossLootTables.pendingBoost.AntiDragon);
                        if (MiniBossLootTables.pendingBoost.AntiUndead > 0) Memory.WriteByte(slotBase + antiUndeadOffset,  MiniBossLootTables.pendingBoost.AntiUndead);
                        if (MiniBossLootTables.pendingBoost.AntiMarine > 0) Memory.WriteByte(slotBase + antiMarineOffset,  MiniBossLootTables.pendingBoost.AntiMarine);
                        if (MiniBossLootTables.pendingBoost.AntiRock > 0)   Memory.WriteByte(slotBase + antiRockOffset,    MiniBossLootTables.pendingBoost.AntiRock);
                        if (MiniBossLootTables.pendingBoost.AntiPlant > 0)  Memory.WriteByte(slotBase + antiPlantOffset,   MiniBossLootTables.pendingBoost.AntiPlant);
                        if (MiniBossLootTables.pendingBoost.AntiBeast > 0)  Memory.WriteByte(slotBase + antiBeastOffset,   MiniBossLootTables.pendingBoost.AntiBeast);
                        if (MiniBossLootTables.pendingBoost.AntiSky > 0)    Memory.WriteByte(slotBase + antiSkyOffset,     MiniBossLootTables.pendingBoost.AntiSky);
                        if (MiniBossLootTables.pendingBoost.AntiMimic > 0)  Memory.WriteByte(slotBase + antiMimicOffset,   MiniBossLootTables.pendingBoost.AntiMimic);
                        if (MiniBossLootTables.pendingBoost.AntiMage > 0)   Memory.WriteByte(slotBase + antiMageOffset,    MiniBossLootTables.pendingBoost.AntiMage);
                        if (MiniBossLootTables.pendingBoost.Special1 > 0)   Memory.WriteByte(slotBase + special1Offset,    MiniBossLootTables.pendingBoost.Special1);
                        if (MiniBossLootTables.pendingBoost.Special2 > 0)   Memory.WriteByte(slotBase + special2Offset,    MiniBossLootTables.pendingBoost.Special2);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Weapon boost applied to bag slot {i}:" +
                            (MiniBossLootTables.pendingBoost.Attack > 0     ? $" atk={MiniBossLootTables.pendingBoost.Attack}"              : "") +
                            (MiniBossLootTables.pendingBoost.Endurance > 0  ? $" end={MiniBossLootTables.pendingBoost.Endurance}"           : "") +
                            (MiniBossLootTables.pendingBoost.Speed > 0      ? $" spd={MiniBossLootTables.pendingBoost.Speed}"               : "") +
                            (MiniBossLootTables.pendingBoost.Magic > 0      ? $" mp={MiniBossLootTables.pendingBoost.Magic}"                : "") +
                            (MiniBossLootTables.pendingBoost.Whp > 0        ? $" whp={MiniBossLootTables.pendingBoost.Whp}"                 : "") +
                            (MiniBossLootTables.pendingBoost.Fire > 0       ? $" fire={MiniBossLootTables.pendingBoost.Fire}"               : "") +
                            (MiniBossLootTables.pendingBoost.Ice > 0        ? $" ice={MiniBossLootTables.pendingBoost.Ice}"                 : "") +
                            (MiniBossLootTables.pendingBoost.Thunder > 0    ? $" thunder={MiniBossLootTables.pendingBoost.Thunder}"         : "") +
                            (MiniBossLootTables.pendingBoost.Wind > 0       ? $" wind={MiniBossLootTables.pendingBoost.Wind}"               : "") +
                            (MiniBossLootTables.pendingBoost.Holy > 0       ? $" holy={MiniBossLootTables.pendingBoost.Holy}"               : "") +
                            (MiniBossLootTables.pendingBoost.AntiDragon > 0 ? $" aDragon={MiniBossLootTables.pendingBoost.AntiDragon}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiUndead > 0 ? $" aUndead={MiniBossLootTables.pendingBoost.AntiUndead}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiMarine > 0 ? $" aMarine={MiniBossLootTables.pendingBoost.AntiMarine}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiRock > 0   ? $" aRock={MiniBossLootTables.pendingBoost.AntiRock}"          : "") +
                            (MiniBossLootTables.pendingBoost.AntiPlant > 0  ? $" aPlant={MiniBossLootTables.pendingBoost.AntiPlant}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiBeast > 0  ? $" aBeast={MiniBossLootTables.pendingBoost.AntiBeast}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiSky > 0    ? $" aSky={MiniBossLootTables.pendingBoost.AntiSky}"            : "") +
                            (MiniBossLootTables.pendingBoost.AntiMimic > 0  ? $" aMimic={MiniBossLootTables.pendingBoost.AntiMimic}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiMage > 0   ? $" aMage={MiniBossLootTables.pendingBoost.AntiMage}"          : "") +
                            (MiniBossLootTables.pendingBoost.Special1 > 0   ? $" sp1=0x{MiniBossLootTables.pendingBoost.Special1:X2}"       : "") +
                            (MiniBossLootTables.pendingBoost.Special2 > 0   ? $" sp2=0x{MiniBossLootTables.pendingBoost.Special2:X2}"       : ""));
                        MiniBossLootTables.pendingBoostActive = false;
                        return;
                    }
                }
            }
        }
    }
}
