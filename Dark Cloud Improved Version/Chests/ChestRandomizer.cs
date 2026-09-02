using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Pool-based chest randomizer. On floor entry it overwrites each live chest slot with a random
    /// item drawn from <see cref="ChestPools"/>, weighted by rarity (higher rarity = rarer)
    /// and scaled by dungeon depth. Small chests get non-weapon items (size byte 1); big chests get
    /// weapons (size byte 0). Keeps the safety / quest features of the old table-based randomizer
    /// (now CustomChests.BasicChestRandomizer): mimic skip, door-key reservation, Map / Magical Crystal
    /// sidequest items, per-dungeon fetch-quest items, and Chronicle 2's effect on the big-chest rate.
    ///
    /// Not yet wired into Dungeon.cs (Basic is still the active one) — swap the call there to enable.
    /// </summary>
    public class ChestRandomizer
    {
        // ── Live chest slot layout (ChestAddresses.ChestSlots) ──────────────────────────────
        const int Stride         = 0x40; // bytes between chest slots
        const int SizeByteOffset = 0x08; // byte: 1 = small (item), 0 = big (weapon)
        const int TrapOffset     = 0x10; // big chests: trap / clown roll (0..5)
        const int MimicThreshold = 40;   // item IDs <= 40 are mimic spawns — never overwrite
        const int NormalChests   = 8;
        const int BackfloorChests = 7;

        // ── Selection / depth tuning (the main knobs) ───────────────────────────────────────
        // Each item's pick weight = band(rarity, depth) * inverseRarity(rarity):
        //   • band   — a Gaussian centred on a "target rarity" that rises with depth, so shallow
        //              floors favour low-rarity (weak/common) items and deep floors favour high-rarity
        //              (strong) ones. Width = RaritySigma.
        //   • inverse — keeps rarer items rarer even when in-band (so e.g. Chronicle 2 stays scarce).
        const double RaritySigma     = 18.0; // band width
        const double RarityTargetMin = 40.0; // target rarity at depth 0 (shallowest)
        const double RarityTargetMax = 98.0; // target rarity at depth 1 (deepest)

        // % of chests (out of 1000) that roll as ITEMS (small); the rest are WEAPONS (big).
        // Base rises slightly per dungeon; Chronicle 2 shifts the odds toward weapons.
        const int ItemChanceBase     = 880;
        const int ItemChancePerDng   = 15;

        // Approx highest regular floor per dungeon (0..6), used only for the depth ramp.
        static readonly int[] MaxFloor = { 14, 17, 18, 18, 15, 13, 100 };

        // Per-dungeon fetch-quest: unlock-flag address → reward item ID (chance default 66, DS 85).
        static readonly Dictionary<int, (int FlagAddr, byte ItemId, byte Chance)> SideQuests = new()
        {
            [0] = (0x21CE4451, 171, 66), // Divine Beast Cave
            [1] = (0x21CE4452, 173, 66), // Wise Owl Forest
            [2] = (0x21CE4453, 243, 66), // Shipwreck
            [3] = (0x21CE4454, 172, 66), // Sun & Moon Temple
            [6] = (0x21CE4455, 241, 85), // Demon Shaft
        };

        static readonly Random rnd = new Random();

        // Per-run sidequest state.
        static bool _questPending;
        static byte _questItemId;
        static bool _questPlaced;

        /// <summary>
        /// Randomizes every chest on the current floor (normal + backfloor) from the pools.
        /// </summary>
        public static void Randomize(int currentDungeon, int currentFloor, bool chronicle2)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Pool chest randomizer activated");

            _questPending = false;
            _questPlaced = false;
            RollSideQuest(currentDungeon);

            double depth = Depth01(currentDungeon, currentFloor);

            // Item (small) chance out of 1000; Chronicle 2 shifts toward weapons (big).
            int itemChance = ItemChanceBase + ItemChancePerDng * currentDungeon;
            if (chronicle2)
                itemChance -= (1000 - itemChance);

            bool hasMap = SideQuestManager.CheckItemQuestReward(233, true, false);
            bool hasMC  = SideQuestManager.CheckItemQuestReward(234, true, false);

            // ── Normal-floor chests ──────────────────────────────────────────────────────
            // The game places the door-map (233) in the first chest on locked-door floors; the start
            // offset skips it so the key is never overwritten. Slots 0/1 are reserved for an active
            // Map / Magical Crystal sidequest reward.
            int firstItem = Memory.ReadByte(Addresses.firstChest);
            int offset = firstItem == 233
                ? (hasMap || hasMC ? 0x00 : 0x80)
                : (hasMap || hasMC ? 0x40 : 0xC0);
            int addr = Addresses.firstChest + offset;

            for (int i = 0; i < NormalChests; i++)
            {
                bool spawn = true;
                if ((i == 0 || i == 1) && (hasMap || hasMC))
                    spawn = (i == 0 && hasMap) || (i == 1 && hasMC);

                if (spawn && Memory.ReadShort(addr) > MimicThreshold)
                    addr = WriteChest(addr, depth, itemChance, currentDungeon, back: false, allowQuest: false);
                else
                    addr += Stride;
            }

            // ── Backfloor chests ─────────────────────────────────────────────────────────
            addr = Addresses.backfloorFirstChest;
            for (int i = 0; i < BackfloorChests; i++)
            {
                if (Memory.ReadShort(addr) > MimicThreshold)
                    addr = WriteChest(addr, depth, itemChance, currentDungeon, back: true, allowQuest: true);
                else
                    addr += Stride;
            }

            if (hasMap) Memory.WriteByte(Addresses.map, 1);
            if (hasMC)  Memory.WriteByte(Addresses.magicCrystal, 1);
        }

        /// <summary>Writes one chest (item or weapon) at <paramref name="addr"/>; returns the next slot address.</summary>
        static int WriteChest(int addr, double depth, int itemChance, int dungeon, bool back, bool allowQuest)
        {
            bool isItem = rnd.Next(1000) < itemChance;

            if (isItem)
            {
                int itemId = Pick(back ? ChestPools.ChestSmallBack : ChestPools.ChestSmallFront, depth);

                // Guarantee the fetch-quest item in the first eligible backfloor item chest.
                if (allowQuest && _questPending && !_questPlaced)
                {
                    itemId = _questItemId;
                    _questPlaced = true;
                }

                Memory.Write(addr, BitConverter.GetBytes(itemId));
                Memory.WriteByte(addr + SizeByteOffset, 1); // small chest
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Chest item: " + Item.GetName((ushort)itemId));
            }
            else
            {
                int weaponId = Pick(back ? ChestPools.ChestBigBack : ChestPools.ChestBigFront, depth);

                Memory.Write(addr, BitConverter.GetBytes(weaponId));
                Memory.WriteByte(addr + SizeByteOffset, 0);                       // big chest
                Memory.Write(addr + TrapOffset, BitConverter.GetBytes(rnd.Next(6))); // trap / clown roll
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Chest weapon: " + Item.GetName((ushort)weaponId));
            }

            return addr + Stride;
        }

        /// <summary>Weighted random pick from a pool (id → rarity, higher = rarer), depth-scaled.</summary>
        static int Pick(Dictionary<ushort, int> pool, double depth)
        {
            double total = 0;
            int fallback = -1, fallbackRarity = int.MaxValue;
            foreach (KeyValuePair<ushort, int> e in pool)
            {
                total += SelectionWeight(e.Value, depth);
                if (e.Value < fallbackRarity) { fallbackRarity = e.Value; fallback = e.Key; }
            }
            if (total <= 0) return fallback; // no in-band item — fall back to the commonest

            double r = rnd.NextDouble() * total;
            foreach (KeyValuePair<ushort, int> e in pool)
            {
                r -= SelectionWeight(e.Value, depth);
                if (r <= 0) return e.Key;
            }
            return fallback;
        }

        /// <summary>Pick weight for a rarity at a given depth (see the tuning block above).</summary>
        static double SelectionWeight(int rarity, double depth)
        {
            double target = RarityTargetMin + (RarityTargetMax - RarityTargetMin) * depth;
            double d = rarity - target;
            double band = Math.Exp(-(d * d) / (2 * RaritySigma * RaritySigma));
            double inverse = (101.0 - rarity) / 61.0; // higher rarity → lower weight
            return band * inverse;
        }

        /// <summary>Progress through the game, 0 (DBC floor 1) .. 1 (Demon Shaft deepest).</summary>
        static double Depth01(int dungeon, int floor)
        {
            if (dungeon < 0) dungeon = 0;
            if (dungeon > 6) dungeon = 6;
            double ff = Math.Min(1.0, Math.Max(0, floor) / (double)MaxFloor[dungeon]);
            double v = (dungeon + ff) / 7.0;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        /// <summary>Rolls whether this floor's fetch-quest item is guaranteed in a backfloor chest.</summary>
        static void RollSideQuest(int dungeon)
        {
            if (!SideQuests.TryGetValue(dungeon, out var q)) return;
            if (Memory.ReadByte(q.FlagAddr) != 1) return;               // quest not unlocked
            if (rnd.Next(0, 100) <= q.Chance) return;                   // not rolled this floor
            if (SideQuestManager.CheckItemQuestReward(q.ItemId)) return; // already owned
            _questPending = true;
            _questItemId = q.ItemId;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Rolled sidequest item for this floor");
        }
    }
}
