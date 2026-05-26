using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    internal struct FishData
    {
        internal byte   Id;
        internal float? Unk004;           // +0x004; null = not yet dumped
        internal float? Unk008;           // +0x008; null = not yet dumped
        internal float? MaxSize;          // +0x00C (float; ×10 = display cm); null = unknown
        internal int?   FpMin;            // +0x010; null = unknown
        internal int?   FpMax;            // +0x014; null = unknown
        internal float? EstimatedMinSize; // observed gameplay minimum (×10 = cm); null = not yet observed
        // Bait affinity table (+0x018–+0x048): 13 floats, 4 bytes each.
        // null = unknown; 0.0 = never bites; 1.0 = normal
        internal float? BaitAffEvy;
        internal float? BaitAffMimi;
        internal float? BaitAffPrickly;
        internal float? BaitAffThrobbingCherry;
        internal float? BaitAffGooeypeach;
        internal float? BaitAffBombnuts;
        internal float? BaitAffPoisonousApple;
        internal float? BaitAffMellowBanana;
        internal float? BaitAffCarrot;
        internal float? BaitAffPotatoCake;
        internal float? BaitAffMinon;
        internal float? BaitAffBattan;
        internal float? BaitAffPetitefish;
    }

    internal static class Fishing
    {
        // Fish slot field offsets (relative to slot base address). All confirmed via slot dump analysis.
        internal const int OffsetUnk004              = 0x004;
        internal const int OffsetUnk008              = 0x008;
        internal const int OffsetMaxSize             = 0x00C;
        internal const int OffsetFpMin               = 0x010;
        internal const int OffsetFpMax               = 0x014;
        internal const int OffsetBaitEvy             = 0x018;
        internal const int OffsetBaitMimi            = 0x01C;
        internal const int OffsetBaitPrickly         = 0x020;
        internal const int OffsetBaitThrobbingCherry = 0x024;
        internal const int OffsetBaitGooeypeach      = 0x028;
        internal const int OffsetBaitBombnuts        = 0x02C;
        internal const int OffsetBaitPoisonousApple  = 0x030;
        internal const int OffsetBaitMellowBanana    = 0x034;
        internal const int OffsetBaitCarrot          = 0x038;
        internal const int OffsetBaitPotatoCake      = 0x03C;
        internal const int OffsetBaitMinon           = 0x040;
        internal const int OffsetBaitBattan          = 0x044;
        internal const int OffsetBaitPetitefish      = 0x048;
        internal const int OffsetSize                = 0x060;
        internal const int OffsetScaleX              = 0x064;
        internal const int OffsetScaleY              = 0x068;

        // Named fish data. null fields have not yet been confirmed from a slot dump or observed in gameplay.
        // Mardan Garayan: all values confirmed from natural spawn dump (Matataki area, slot 0).
        // Baron Garayan: MaxSize confirmed (300cm); Unk/bait affinities assumed same as Mardan; FP range unknown.
        internal static class FishDatabase
        {
            // ── Partial data (observed minimums only; full slot dump pending) ─────────────────
            internal static readonly FishData Bobo      = new FishData { Id =  0 };
            internal static readonly FishData Gobbler   = new FishData { Id =  1, EstimatedMinSize = 11.1f };
            internal static readonly FishData Nonky     = new FishData { Id =  2, EstimatedMinSize =  7.4f };
            internal static readonly FishData Kaiji     = new FishData { Id =  3 };
            internal static readonly FishData BakuBaku  = new FishData { Id =  4, EstimatedMinSize =  6.8f };
            internal static readonly FishData Gummy     = new FishData { Id =  6, EstimatedMinSize =  4.9f };
            internal static readonly FishData Niler     = new FishData { Id =  7, EstimatedMinSize =  5.3f };
            internal static readonly FishData Umadakara = new FishData { Id =  9 };
            internal static readonly FishData Tarton    = new FishData { Id = 10 };
            internal static readonly FishData Piccoly   = new FishData { Id = 11 };
            internal static readonly FishData Bon       = new FishData { Id = 12 };
            internal static readonly FishData Hamahama  = new FishData { Id = 13 };
            internal static readonly FishData Negie     = new FishData { Id = 14 };
            internal static readonly FishData Den       = new FishData { Id = 15 };
            internal static readonly FishData Heela     = new FishData { Id = 16 };

            // ── Fully confirmed ──────────────────────────────────────────────────────────────
            internal static readonly FishData MardanGarayan = new FishData
            {
                Id = 5,
                Unk004 = 21.0f, Unk008 = 10.0f, MaxSize = 16.0f, FpMin = 200, FpMax = 400,
                EstimatedMinSize = 5.0f,
                BaitAffEvy             = 0.0f,
                BaitAffMimi            = 0.0f,
                BaitAffPrickly         = 0.0f,
                BaitAffThrobbingCherry = 0.0f,
                BaitAffGooeypeach      = 0.0f,
                BaitAffBombnuts        = 0.0f,
                BaitAffPoisonousApple  = 1.0f,
                BaitAffMellowBanana    = 0.0f,
                BaitAffCarrot          = 0.0f,
                BaitAffPotatoCake      = 0.0f,
                BaitAffMinon           = 0.0f,
                BaitAffBattan          = 0.0f,
                BaitAffPetitefish      = 0.0f,
            };
            internal static readonly FishData BaronGarayan = new FishData
            {
                Id = 17,
                Unk004 = 21.0f, Unk008 = 10.0f, MaxSize = 30.0f,
                FpMin = null, FpMax = null, // TODO: confirm from natural Baron slot dump
                EstimatedMinSize = 5.0f,
                BaitAffEvy             = 0.0f,
                BaitAffMimi            = 0.0f,
                BaitAffPrickly         = 0.0f,
                BaitAffThrobbingCherry = 0.0f,
                BaitAffGooeypeach      = 0.0f,
                BaitAffBombnuts        = 0.0f,
                BaitAffPoisonousApple  = 0.0f,
                BaitAffMellowBanana    = 0.0f,
                BaitAffCarrot          = 0.0f,
                BaitAffPotatoCake      = 1.0f,
                BaitAffMinon           = 0.0f,
                BaitAffBattan          = 0.0f,
                BaitAffPetitefish      = 0.0f,
            };

            // Dynamic lookup by fish ID — built from each entry's Id field so no hardcoded keys.
            private static readonly Dictionary<byte, FishData> ById;
            static FishDatabase()
            {
                FishData[] allFish = {
                    Bobo, Gobbler, Nonky, Kaiji, BakuBaku, Gummy, Niler,
                    Umadakara, Tarton, Piccoly, Bon, Hamahama, Negie, Den, Heela,
                    MardanGarayan, BaronGarayan,
                };
                ById = new Dictionary<byte, FishData>(allFish.Length);
                foreach (FishData fish in allFish) ById[fish.Id] = fish;
            }

            internal static bool TryGetValue(byte id, out FishData data) => ById.TryGetValue(id, out data);
        }

        private static readonly string[] FishNames =
        {
            "Bobo", "Gobbler", "Nonky", "Kaiji", "Baku Baku",
            "Mardan Garayan", "Gummy", "Niler", "NULL", "Umadakara",
            "Tarton", "Piccoly", "Bon", "Hamahama", "Negie", "Den", "Heela", "Baron Garayan"
        };
        internal static string GetFishName(byte id) =>
            id < FishNames.Length ? FishNames[id] : $"Unknown({id})";

        // Controls whether RollFishSlots is called during area init.
        private enum RollMode { Never, Always, IfTwei }

        // Per-area configuration. QuestBase layout (all byte offsets):
        //   +0 state (0=none, 1=active, 2=complete)  +1 type (0=count fish, 1=size range)
        //   +3 target fish ID  +4 count remaining  +5 min size  +6 max size
        private struct AreaFishData
        {
            internal int      SlotBase;
            internal int      SlotCount;
            internal int      QuestBase;       // base of this NPC's quest block in mod memory
            internal string   GiverName;
            internal RollMode Roll;
            internal int      QuestsDoneAddr;  // Sam only: multi-quest counter (0 = not present)
            internal int      PostLoopSrc;     // Sam only: queens-quest trigger src (0 = not present)
            internal int      PostLoopDst;     // Sam only: queens-quest trigger dst
            internal byte[]   FishIds;         // fish IDs that can naturally appear in this area; null = not yet catalogued
        }

        // Keyed by area ID. Add an entry here to support a new fishing area.
        private static readonly Dictionary<int, AreaFishData> AreaData = new Dictionary<int, AreaFishData>
        {
            [0]  = new AreaFishData { SlotBase = Addresses.fishSlotBase_Norune,   SlotCount = 4, QuestBase = 0x21CE4416, GiverName = "Pike",  Roll = RollMode.Always  },
            [1]  = new AreaFishData { SlotBase = Addresses.fishSlotBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",   Roll = RollMode.Always  },
            [19] = new AreaFishData { SlotBase = Addresses.fishSlotBase_Area19,   SlotCount = 5, QuestBase = 0x21CE4427, GiverName = "Sam",   Roll = RollMode.Never,
                                      QuestsDoneAddr = 0x21CE442F, PostLoopSrc = 0x21CE4430, PostLoopDst = 0x202A1FA0 },
            [3]  = new AreaFishData { SlotBase = Addresses.fishSlotBase_Area3,    SlotCount = 4, QuestBase = 0x21CE4431, GiverName = "Devia", Roll = RollMode.IfTwei  },
        };

        internal static readonly Random Rng = new Random();
        internal static byte[] fishArray = new byte[5];
        private static ushort[] _bagSnapshot = null;
        private static ushort _lastBaitSlot = 0xFFFF;
        private static int _cachedBaitId = 0;
        internal static bool RangeBoosted = false;

        // Mardan sword state — set by CheckMardanSword at each session start
        internal static bool hasMardanSword = false;
        internal static int mardanSwordId = 0;  // 278=Eins, 279=Twei, 280=Arise
        private static float mardanMultiplier = 1f;

        // Per-session quest state
        internal static bool fishingQuestCheck = false;
        internal static bool[] fishCaught = new bool[6];
        // Keyed by QuestBase address so multiple quest givers can be active simultaneously.
        private static readonly Dictionary<int, bool> questActive = new Dictionary<int, bool>();
        private static int minFishSize = 0;
        private static int maxFishSize = 0;

        // Called when the player enters a fishing area. Resets quest state and snapshots the inventory.
        internal static void OnSessionStart()
        {
            fishingQuestCheck = false;
            questActive.Clear();
            TakeBagSnapshot();
            CheckMardanSword();
        }

        // Resets per-session bait detection state and restores the detection radius patch if boosted.
        internal static void ResetSession()
        {
            _bagSnapshot = null;
            _lastBaitSlot = 0xFFFF;
            _cachedBaitId = 0;
            if (RangeBoosted)
            {
                Memory.WriteInt(Addresses.fishDetectionRadiusPatch, 0x3C024000);
                RangeBoosted = false;
            }
        }

        // Called from TownCharacter every fishing tick. On the first call it initializes the session
        // (reads slots, activates quest, applies area mods), then delegates to CheckFishingQuest every tick.
        public static void InitFishingSession(int area)
        {
            if (!AreaData.TryGetValue(area, out AreaFishData areaData)) return;
            if (!fishingQuestCheck)
            {
                int slotAddr = areaData.SlotBase;
                for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
                {
                    fishArray[slotIndex] = Memory.ReadByte(slotAddr);
                    slotAddr += Addresses.fishSlotStride;
                    fishCaught[slotIndex] = false;
                }
                if (Memory.ReadByte(areaData.QuestBase) == 1)
                {
                    questActive[areaData.QuestBase] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Currently on {areaData.GiverName}s quest");
                    minFishSize = Memory.ReadByte(areaData.QuestBase + 5);
                    maxFishSize = Memory.ReadByte(areaData.QuestBase + 6);
                }
                // if (areaData.Roll == RollMode.Always ||
                //     (areaData.Roll == RollMode.IfTwei && (mardanSwordId == Items.mardantwei || mardanSwordId == Items.arisemardan)))
                //     RollFishSlots(areaData.SlotBase, areaData.SlotCount, FishDatabase.MardanGarayan.Id, 0.2f);
                // ApplyMardanBonus(areaData.SlotBase, areaData.SlotCount);
                GiveBaitForTesting();
                LogFishSession(area, areaData.SlotBase, areaData.SlotCount);
                if (mardanSwordId == Items.arisemardan)
                    ScaleFishSizes(areaData.SlotBase, areaData.SlotCount);
                fishingQuestCheck = true;
            }
            CheckFishingQuest(areaData);
            UpdateFishingRangeBoost();
        }

        // Scales the FP reward range of each non-Garayan slot by mardanMultiplier.
        private static void ApplyMardanBonus(int slotBase, int slotCount)
        {
            if (!hasMardanSword) return;
            Thread.Sleep(300);
            int addr = slotBase + OffsetFpMin;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == FishDatabase.MardanGarayan.Id || fishArray[slotIndex] == FishDatabase.BaronGarayan.Id)
                {
                    addr += Addresses.fishSlotStride;
                    continue;
                }
                Memory.WriteInt(addr, (int)(Memory.ReadInt(addr) * mardanMultiplier));
                addr += 4;
                Memory.WriteInt(addr, (int)(Memory.ReadInt(addr) * mardanMultiplier));
                addr += Addresses.fishSlotStride - 4;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Mardan did its thing!");
            }
        }

        // Writes all known FishDatabase fields for fishId into the slot at slotStart. Null fields are skipped
        // (the game's native value is preserved). Size is randomized within [EstimatedMinSize, MaxSize].
        private static void WriteSlotData(int slotStart, byte fishId, FishData fishData)
        {
            float minSize = fishData.EstimatedMinSize ?? 5.0f;
            float maxSize = fishData.MaxSize ?? minSize;
            float size = minSize + (float)(Rng.NextDouble() * Math.Max(0, maxSize - minSize));
            Memory.WriteByte(slotStart, fishId);
            if (fishData.Unk004.HasValue)                 Memory.WriteFloat(slotStart + OffsetUnk004,              fishData.Unk004.Value);
            if (fishData.Unk008.HasValue)                 Memory.WriteFloat(slotStart + OffsetUnk008,              fishData.Unk008.Value);
            if (fishData.MaxSize.HasValue)                Memory.WriteFloat(slotStart + OffsetMaxSize,             fishData.MaxSize.Value);
            if (fishData.FpMin.HasValue)                  Memory.WriteInt(slotStart + OffsetFpMin,                 fishData.FpMin.Value);
            if (fishData.FpMax.HasValue)                  Memory.WriteInt(slotStart + OffsetFpMax,                 fishData.FpMax.Value);
            if (fishData.BaitAffEvy.HasValue)             Memory.WriteFloat(slotStart + OffsetBaitEvy,             fishData.BaitAffEvy.Value);
            if (fishData.BaitAffMimi.HasValue)            Memory.WriteFloat(slotStart + OffsetBaitMimi,            fishData.BaitAffMimi.Value);
            if (fishData.BaitAffPrickly.HasValue)         Memory.WriteFloat(slotStart + OffsetBaitPrickly,         fishData.BaitAffPrickly.Value);
            if (fishData.BaitAffThrobbingCherry.HasValue) Memory.WriteFloat(slotStart + OffsetBaitThrobbingCherry, fishData.BaitAffThrobbingCherry.Value);
            if (fishData.BaitAffGooeypeach.HasValue)      Memory.WriteFloat(slotStart + OffsetBaitGooeypeach,      fishData.BaitAffGooeypeach.Value);
            if (fishData.BaitAffBombnuts.HasValue)        Memory.WriteFloat(slotStart + OffsetBaitBombnuts,        fishData.BaitAffBombnuts.Value);
            if (fishData.BaitAffPoisonousApple.HasValue)  Memory.WriteFloat(slotStart + OffsetBaitPoisonousApple,  fishData.BaitAffPoisonousApple.Value);
            if (fishData.BaitAffMellowBanana.HasValue)    Memory.WriteFloat(slotStart + OffsetBaitMellowBanana,    fishData.BaitAffMellowBanana.Value);
            if (fishData.BaitAffCarrot.HasValue)          Memory.WriteFloat(slotStart + OffsetBaitCarrot,          fishData.BaitAffCarrot.Value);
            if (fishData.BaitAffPotatoCake.HasValue)      Memory.WriteFloat(slotStart + OffsetBaitPotatoCake,      fishData.BaitAffPotatoCake.Value);
            if (fishData.BaitAffMinon.HasValue)           Memory.WriteFloat(slotStart + OffsetBaitMinon,           fishData.BaitAffMinon.Value);
            if (fishData.BaitAffBattan.HasValue)          Memory.WriteFloat(slotStart + OffsetBaitBattan,          fishData.BaitAffBattan.Value);
            if (fishData.BaitAffPetitefish.HasValue)      Memory.WriteFloat(slotStart + OffsetBaitPetitefish,      fishData.BaitAffPetitefish.Value);
            Memory.WriteFloat(slotStart + OffsetSize,   size);
            Memory.WriteFloat(slotStart + OffsetScaleX, size * 0.04f);
            Memory.WriteFloat(slotStart + OffsetScaleY, size * 0.04f);
        }

        // Rerolls each non-Garayan slot to targetFishId.
        // baronChance: probability [0, 1] that a rerolled slot becomes Baron Garayan instead of targetFishId.
        //   0f (default) — all rerolled slots become targetFishId.
        //   0.2f         — used for Mardan Twei feature to match native Garayan spawn ratios.
        internal static void RollFishSlots(int areaBase, int slotCount, byte targetFishId, float baronChance = 0f)
        {
            float timeOfDay = Memory.ReadFloat(Addresses.timeofDayWrite);
            // int pct;
            // if      (timeOfDay >= 2.5f && timeOfDay < 5.5f)  pct = 5; // Dusk
            // else if (timeOfDay >= 5.5f && timeOfDay < 8.5f)  pct = 3; // Night
            // else if (timeOfDay >= 8.5f && timeOfDay < 11.5f) pct = 4; // Morning
            // else                                              pct = 2; // Afternoon

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == FishDatabase.MardanGarayan.Id || fishArray[slotIndex] == FishDatabase.BaronGarayan.Id) continue;
                // TEST: force reroll on every non-Garayan slot
                // if (Rng.Next(100) < pct)
                {
                    int slotStart = areaBase + (Addresses.fishSlotStride * slotIndex);
                    byte originalId = Memory.ReadByte(slotStart);
                    byte newId = (baronChance > 0f && Rng.NextDouble() < baronChance) ? FishDatabase.BaronGarayan.Id : targetFishId;
                    if (!FishDatabase.TryGetValue(newId, out FishData fishData))
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[RollFishSlots] No FishData for id={newId}, skipping slot {slotIndex}");
                        continue;
                    }
                    fishArray[slotIndex] = newId;
                    WriteSlotData(slotStart, newId, fishData);
                    float size = Memory.ReadFloat(slotStart + OffsetSize);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[RollFishSlots] slot={slotIndex} {GetFishName(originalId)} (id={originalId}) → {GetFishName(newId)} (id={newId}) size={size:F4} ({(int)(size*10)}cm) tod={timeOfDay:F2}");
                }
            }
        }

        internal static void ScaleFishSizes(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                float originalSize = Memory.ReadFloat(slotStart + OffsetSize);
                float scaledSize   = originalSize * 2.0f;
                Memory.WriteFloat(slotStart + OffsetSize,   scaledSize);
                float scaleRatio = scaledSize / originalSize;
                Memory.WriteFloat(slotStart + OffsetScaleX, Memory.ReadFloat(slotStart + OffsetScaleX) * scaleRatio);
                Memory.WriteFloat(slotStart + OffsetScaleY, Memory.ReadFloat(slotStart + OffsetScaleY) * scaleRatio);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SizeScale] slot={slotIndex} {GetFishName(Memory.ReadByte(slotStart))} " +
                    $"orig={originalSize:F4} scaled={scaledSize:F4} ({(int)(originalSize*10)}→{(int)(scaledSize*10)}cm)");
            }
        }

        // [DIAG] Snapshots the bag inventory so bait consumption can be detected by DetectBaitFromInventory.
        // Call at session start; the snapshot captures item IDs before any bait is cast.
        private static void TakeBagSnapshot()
        {
            int slotCount = (Addresses.firstBagWeapon - Addresses.firstBagItem) / 2;
            _bagSnapshot = new ushort[slotCount];
            for (int bagSlot = 0; bagSlot < slotCount; bagSlot++)
                _bagSnapshot[bagSlot] = Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BagSnapshot] taken ({slotCount} slots)");
        }

        private static int DetectBaitFromInventory()
        {
            if (_bagSnapshot == null) return 0;
            for (int bagSlot = 0; bagSlot < _bagSnapshot.Length; bagSlot++)
            {
                if (_bagSnapshot[bagSlot] != 0 && Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2) == 0)
                    return _bagSnapshot[bagSlot];
            }
            return 0;
        }

        internal static int GetCurrentBaitId()
        {
            ushort baitSlotRaw = Memory.ReadUShort(0x21D19708);
            if (baitSlotRaw == _lastBaitSlot) return _cachedBaitId;
            _lastBaitSlot = baitSlotRaw;
            _cachedBaitId = baitSlotRaw != 0xFFFF ? DetectBaitFromInventory() : 0;
            return _cachedBaitId;
        }

        // Patches the lui instruction at 0x20240364 that loads the fish detection radius float.
        // Native: 0x3C024000 → 2.0f. Boosted: 0x3C024080 → 4.0f (double range).
        private static void UpdateFishingRangeBoost()
        {
            int baitId = GetCurrentBaitId();
            bool shouldBoost = (mardanSwordId == Items.mardaneins ||
                                mardanSwordId == Items.mardantwei  ||
                                mardanSwordId == Items.arisemardan) &&
                               (baitId == Items.poisonousapple || baitId == Items.potatocake);

            if (shouldBoost && !RangeBoosted)
            {
                Memory.WriteInt(Addresses.fishDetectionRadiusPatch, 0x3C024080);
                RangeBoosted = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[RangeBoost] Fishing range doubled (bait={baitId})");
            }
            else if (!shouldBoost && RangeBoosted)
            {
                Memory.WriteInt(Addresses.fishDetectionRadiusPatch, 0x3C024000);
                RangeBoosted = false;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[RangeBoost] Fishing range restored");
            }
        }

        // TEST: gives one of every bait type at session start so all baits are available for testing.
        // Bait list from wiki — 13 total:
        //   throbbingcherry(166)  Heela, Niler, Den
        //   gooeypeach(167)       Gummy, Nonky, Niler
        //   bombnuts(168)         Gobbler, Niler
        //   poisonousapple(169)   Mardan Garayan
        //   mellowbanana(170)     Baku Baku
        //   evy(193)              Bobo, Bon, Kaiji, Piccoly
        //   mimi(197)             Baku Baku, Gobbler
        //   minon(188)            Majority
        //   petitefish(190)       Majority
        //   prickly(199)          Majority
        //   battan(189)           Baku Baku, Den, Gobbler, Gummy, Heela, Negie, Niler, Nonky, Tarton
        //   carrot(186)           Umadakara
        //   potatocake(187)       Baron Garayan, Den, Niler, Nonky, Tarton
        internal static void GiveBaitForTesting()
        {
            int[] baits = {
                Items.throbbingcherry, Items.gooeypeach,   Items.bombnuts,
                Items.poisonousapple,  Items.mellowbanana, Items.evy,
                Items.mimi,            Items.minon,         Items.petitefish,
                Items.prickly,         Items.battan,        Items.carrot,
                Items.potatocake,
            };
            int bagSlotCount = (Addresses.firstBagWeapon - Addresses.firstBagItem) / 2;
            foreach (int bait in baits)
            {
                bool found = false;
                for (int bagSlot = 0; bagSlot < bagSlotCount && !found; bagSlot++)
                    if (Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2) == (ushort)bait)
                        found = true;
                if (!found)
                {
                    int availableSlot = Player.Inventory.GetBagItemsFirstAvailableSlot();
                    if (availableSlot >= 0)
                        Memory.WriteUShort(Addresses.firstBagItem + (0x2 * availableSlot), (ushort)bait);
                }
            }
        }

        internal static void LogFishSession(int areaId, int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart  = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId    = Memory.ReadByte(slotStart);
                float maxSize  = Memory.ReadFloat(slotStart + OffsetMaxSize);
                float size     = Memory.ReadFloat(slotStart + OffsetSize);
                float fpMin    = Memory.ReadInt(slotStart + OffsetFpMin);
                float fpMax    = Memory.ReadInt(slotStart + OffsetFpMax);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishInfo] area={areaId} slot={slotIndex} {GetFishName(fishId)} (id={fishId}) " +
                    $"max={maxSize:F1}({(int)(maxSize*10)}cm) size={size:F4} ({(int)(size*10)}cm) fp={fpMin}-{fpMax}");
                // bait affinity table — values are bite-likelihood weights (0.0=never, 1.0=normal)
                float affEvy      = Memory.ReadFloat(slotStart + OffsetBaitEvy);
                float affMimi     = Memory.ReadFloat(slotStart + OffsetBaitMimi);
                float affPrickly  = Memory.ReadFloat(slotStart + OffsetBaitPrickly);
                float affCherry   = Memory.ReadFloat(slotStart + OffsetBaitThrobbingCherry);
                float affPeach    = Memory.ReadFloat(slotStart + OffsetBaitGooeypeach);
                float affBombnuts = Memory.ReadFloat(slotStart + OffsetBaitBombnuts);
                float affPoison   = Memory.ReadFloat(slotStart + OffsetBaitPoisonousApple);
                float affBanana   = Memory.ReadFloat(slotStart + OffsetBaitMellowBanana);
                float affCarrot   = Memory.ReadFloat(slotStart + OffsetBaitCarrot);
                float affPotato   = Memory.ReadFloat(slotStart + OffsetBaitPotatoCake);
                float affMinon    = Memory.ReadFloat(slotStart + OffsetBaitMinon);
                float affBattan   = Memory.ReadFloat(slotStart + OffsetBaitBattan);
                float affPetite   = Memory.ReadFloat(slotStart + OffsetBaitPetitefish);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BaitAff]  area={areaId} slot={slotIndex} {GetFishName(fishId)} " +
                    $"Evy={affEvy:F2} Mimi={affMimi:F2} Prickly={affPrickly:F2} Cherry={affCherry:F2} Peach={affPeach:F2} " +
                    $"Bomb={affBombnuts:F2} Poison={affPoison:F2} Banana={affBanana:F2} Carrot={affCarrot:F2} " +
                    $"Potato={affPotato:F2} Minon={affMinon:F2} Battan={affBattan:F2} Petite={affPetite:F2}");
            }
            DumpFishSlot(slotBase, slotCount);
        }

        internal static void DumpFishSlot(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId = Memory.ReadByte(slotStart);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SlotDump] slot={slotIndex} {GetFishName(fishId)} (id={fishId}) base=0x{slotStart:X8}");
                for (int offset = 0; offset < 0x200; offset += 4)
                {
                    int rawValue   = Memory.ReadInt(slotStart + offset);
                    float floatVal = Memory.ReadFloat(slotStart + offset);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[SlotDump] slot={slotIndex} +0x{offset:X3}  raw=0x{rawValue:X8}  float={floatVal:F4}");
                }
            }
        }

        public static void FishAcquiredFlag(byte caughtFishId)
        {
            Memory.WriteByte(0x21CE4439 + caughtFishId, 1);
        }

        public static void CheckMardanSword()
        {
            int foundWeaponId = 0;

            for (int bagSlot = 0; bagSlot < 10 && foundWeaponId == 0; bagSlot++)
            {
                int weaponId = Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * bagSlot));
                if (weaponId == Items.mardaneins || weaponId == Items.mardantwei || weaponId == Items.arisemardan)
                    foundWeaponId = weaponId;
            }

            for (int storageSlot = 0; storageSlot < 30 && foundWeaponId == 0; storageSlot++)
            {
                int weaponId = Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * storageSlot));
                if (weaponId == Items.mardaneins || weaponId == Items.mardantwei || weaponId == Items.arisemardan)
                    foundWeaponId = weaponId;
            }

            if (foundWeaponId == Items.arisemardan)
            {
                hasMardanSword = true;
                mardanSwordId = Items.arisemardan;
                mardanMultiplier = 2f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Arise Mardan");
            }
            else if (foundWeaponId == Items.mardantwei)
            {
                hasMardanSword = true;
                mardanSwordId = Items.mardantwei;
                mardanMultiplier = 1.5f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Mardan Twei");
            }
            else if (foundWeaponId == Items.mardaneins)
            {
                hasMardanSword = true;
                mardanSwordId = Items.mardaneins;
                mardanMultiplier = 1.2f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Mardan Eins");
            }
            else
            {
                hasMardanSword = false;
                mardanSwordId = 0;
            }
        }

        // Marks the quest complete in game memory and handles any area-specific side effects.
        private static void CompleteQuest(AreaFishData areaData)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
            Memory.WriteByte(areaData.QuestBase, 2);
            if (areaData.QuestsDoneAddr != 0)
            {
                byte questsDone = Memory.ReadByte(areaData.QuestsDoneAddr);
                if (questsDone < 4) Memory.WriteByte(areaData.QuestsDoneAddr, ++questsDone);
            }
        }

        // Catch scan — runs every tick after the session is initialized by InitFishingSession.
        private static void CheckFishingQuest(AreaFishData areaData)
        {
            questActive.TryGetValue(areaData.QuestBase, out bool isQuestActive);
            int slotAddr = areaData.SlotBase;
            for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
            {
                if (Memory.ReadByte(slotAddr) == 255 && !fishCaught[slotIndex] && Memory.ReadByte(0x202A26E8) == 12)
                {
                    fishCaught[slotIndex] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Fish caught -> slot={slotIndex} ID: {fishArray[slotIndex]} ({GetFishName(fishArray[slotIndex])})");
                    FishAcquiredFlag(fishArray[slotIndex]);
                    if (isQuestActive)
                    {
                        if (Memory.ReadByte(areaData.QuestBase + 1) == 0) // count quest
                        {
                            if (fishArray[slotIndex] == Memory.ReadByte(areaData.QuestBase + 3))
                            {
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                                byte fishRemaining = Memory.ReadByte(areaData.QuestBase + 4);
                                if (--fishRemaining == 0)
                                {
                                    CompleteQuest(areaData);
                                    questActive[areaData.QuestBase] = false;
                                    isQuestActive = false;
                                }
                                else
                                {
                                    Memory.WriteByte(areaData.QuestBase + 4, fishRemaining);
                                }
                            }
                        }
                        else // size quest
                        {
                            int catchSize = (int)Math.Floor(Memory.ReadFloat(slotAddr + OffsetSize) * 10);
                            if (minFishSize <= catchSize && maxFishSize >= catchSize)
                            {
                                CompleteQuest(areaData);
                                questActive[areaData.QuestBase] = false;
                                isQuestActive = false;
                            }
                        }
                    }
                }
                slotAddr += Addresses.fishSlotStride;
            }
            if (areaData.PostLoopSrc != 0 && Memory.ReadByte(areaData.PostLoopSrc) == 1)
                Memory.WriteByte(areaData.PostLoopDst, 1);
        }
    }
}
