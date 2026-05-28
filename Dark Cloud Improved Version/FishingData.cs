using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>In-game time periods that govern fish spawn weights.</summary>
    internal enum TimeOfDay { Morning, Afternoon, Dusk, Night }

    /// <summary>
    /// Known data for a single fish species, mirroring the in-game slot layout.
    /// Null fields have not yet been confirmed from a slot dump or observed in gameplay.
    /// </summary>
    internal struct FishData
    {
        internal byte   Id;
        internal string Name;
        internal float? Unk004;              // +0x004; Unknown purpose, but different per species of fish.
        internal float? Unk008;              // +0x008; Same as above. Both this and Unk004 are strongly correlated with size and FP, but not perfectly, so they may be modifiers or base values for those systems rather than direct size/FP values.
        internal float? MaxSize;             // +0x00C (float; ×10 = display cm);
        internal int?   FpMin;              // +0x010; Doesn't exactly correspond to FP gain, but is strongly correlated and may be a base FP value before modifiers.
        internal int?   FpMax;              // +0x014; Same as above.
        internal float? EstimatedMinSize;   // observed gameplay minimum (×10 = cm); null = not yet observed
        // Relative spawn weight per time period. 0.0 = never; equal values = uniform chance.
        internal float? SpawnWeightMorning;
        internal float? SpawnWeightAfternoon;
        internal float? SpawnWeightDusk;
        internal float? SpawnWeightNight;
        // Bait affinity table (+0x018–+0x048): 13 floats, 4 bytes each.
        // 0.0 = never bites; 1.0 = normal; No observed values above 1.0 and setting a high value does not appear to increase catch rate, so values above 1.0 may be clamped in the game code.
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

    /// <summary>Fish slot field offsets relative to slot base address. All confirmed via slot dump analysis.</summary>
    internal static class FishSlotOffsets
    {
        internal const int Unk004              = 0x004;
        internal const int Unk008              = 0x008;
        internal const int MaxSize             = 0x00C;
        internal const int FpMin               = 0x010;
        internal const int FpMax               = 0x014;
        internal const int BaitAffEvy             = 0x018;
        internal const int BaitAffMimi            = 0x01C;
        internal const int BaitAffPrickly         = 0x020;
        internal const int BaitAffThrobbingCherry = 0x024;
        internal const int BaitAffGooeypeach      = 0x028;
        internal const int BaitAffBombnuts        = 0x02C;
        internal const int BaitAffPoisonousApple  = 0x030;
        internal const int BaitAffMellowBanana    = 0x034;
        internal const int BaitAffCarrot          = 0x038;
        internal const int BaitAffPotatoCake      = 0x03C;
        internal const int BaitAffMinon           = 0x040;
        internal const int BaitAffBattan          = 0x044;
        internal const int BaitAffPetitefish      = 0x048;
        internal const int Size                = 0x060;
        internal const int ScaleX              = 0x064;
        internal const int ScaleY              = 0x068;
        internal const int Heading             = 0x074;
        internal const int LivePosY            = 0x0B0;
        internal const int LivePosZ            = 0x0B4;
        internal const int LivePosX            = 0x0B8;
    }

    /// <summary>
    /// Static repository of per-species <see cref="FishData"/> records, keyed by fish ID.
    /// All confirmed entries are sourced from natural slot dumps with no mod writes active.
    /// </summary>
    internal static class FishDatabase
    {
        internal static readonly FishData Bobo      = new FishData { Id =  0, Name = "Bobo" };
        internal static readonly FishData Gobbler   = new FishData
        {
            Id = 1, Name = "Gobbler",
            Unk004 = 19.5f, Unk008 = 8.0f,
            EstimatedMinSize = 6.6f, MaxSize = 16.0f,
            FpMin = 10, FpMax = 30,
            SpawnWeightMorning = 0.25f, SpawnWeightAfternoon = 0.25f,
            SpawnWeightDusk = 0.25f, SpawnWeightNight = 0.25f,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 1.0f,
        };
        internal static readonly FishData Nonky = new FishData
        {
            Id = 2, Name = "Nonky",
            Unk004 = 25.5f, Unk008 = 8.0f,
            EstimatedMinSize = 4.6f, MaxSize = 19.0f,
            FpMin = 8, FpMax = 25,
            SpawnWeightMorning = 0.25f, SpawnWeightAfternoon = 0.25f,
            SpawnWeightDusk = 0.25f, SpawnWeightNight = 0.25f,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 1.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Kaiji     = new FishData { Id =  3, Name = "Kaiji" };
        internal static readonly FishData BakuBaku  = new FishData
        {
            Id = 4, Name = "Baku Baku",
            Unk004 = 28.0f, Unk008 = 8.0f,
            EstimatedMinSize = 5.7f, MaxSize = 16.0f,
            FpMin = 10, FpMax = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 1.0f,
        };
        internal static readonly FishData MardanGarayan = new FishData
        {
            Id = 5, Name = "Mardan Garayan",
            Unk004 = 21.0f, Unk008 = 10.0f,
            EstimatedMinSize = 5.0f, MaxSize = 16.0f,
            FpMin = 200, FpMax = 400,
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
        internal static readonly FishData Gummy = new FishData
        {
            Id = 6, Name = "Gummy",
            Unk004 = 20.0f, Unk008 = 6.0f,
            EstimatedMinSize = 4.9f, MaxSize = 12.0f,
            FpMin = 15, FpMax = 40,
            SpawnWeightMorning = 0.25f, SpawnWeightAfternoon = 0.25f,
            SpawnWeightDusk = 0.25f, SpawnWeightNight = 0.25f,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 1.0f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Niler = new FishData
        {
            Id = 7, Name = "Niler",
            Unk004 = 20.0f, Unk008 = 6.0f,
            EstimatedMinSize = 5.3f, MaxSize = 10.0f,
            FpMin = 20, FpMax = 50,
            SpawnWeightMorning = 0.25f, SpawnWeightAfternoon = 0.25f,
            SpawnWeightDusk = 0.25f, SpawnWeightNight = 0.25f,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 1.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Umadakara = new FishData { Id =  9, Name = "Umadakara" };
        internal static readonly FishData Tarton    = new FishData { Id = 10, Name = "Tarton" };
        internal static readonly FishData Piccoly   = new FishData { Id = 11, Name = "Piccoly" };
        internal static readonly FishData Bon       = new FishData { Id = 12, Name = "Bon" };
        internal static readonly FishData Hamahama  = new FishData { Id = 13, Name = "Hamahama" };
        internal static readonly FishData Negie     = new FishData { Id = 14, Name = "Negie" };
        internal static readonly FishData Den       = new FishData { Id = 15, Name = "Den" };
        internal static readonly FishData Heela     = new FishData { Id = 16, Name = "Heela" };
        internal static readonly FishData BaronGarayan = new FishData
        {
            Id = 17, Name = "Baron Garayan",
            Unk004 = 21.0f, Unk008 = 10.0f,
            EstimatedMinSize = 5.0f, MaxSize = 30.0f,
            FpMin = 600, FpMax = 1000,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeypeach      = 0.0f,
            BaitAffBombnuts        = 0.0f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.0f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
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

        /// <summary>
        /// Looks up a fish by its numeric ID. Returns false if the species has no confirmed data entry.
        /// </summary>
        internal static bool TryGetValue(byte id, out FishData data) => ById.TryGetValue(id, out data);

        /// <summary>
        /// Returns the display name for a fish ID, or <c>"Unknown(N)"</c> if the ID has no entry.
        /// </summary>
        internal static string GetName(byte id) =>
            TryGetValue(id, out FishData fish) ? fish.Name : $"Unknown({id})";
    }

    /// <summary>
    /// Per-area fishing configuration. <c>QuestBase</c> byte layout:
    /// +0 state (0=none, 1=active, 2=complete), +1 type (0=count fish, 1=size range),
    /// +3 target fish ID, +4 count remaining, +5 min size, +6 max size.
    /// </summary>
    internal struct AreaFishData
    {
        internal int      Id;
        internal string   Name;
        internal int      SlotBase;
        internal int      SlotCount;
        internal int      QuestBase;       // base of this NPC's quest block in mod memory
        internal string   GiverName;
        internal int      QuestsDoneAddr;  // Sam only: multi-quest counter (0 = not present)
        internal int      PostLoopSrc;     // Sam only: queens-quest trigger src (0 = not present)
        internal int      PostLoopDst;     // Sam only: queens-quest trigger dst
        internal byte[]   FishIds;         // naturally-spawning fish IDs; null = not yet catalogued
    }

    /// <summary>
    /// Static repository of per-area <see cref="AreaFishData"/> records, keyed by area ID.
    /// Add a named entry here to support a new fishing area.
    /// </summary>
    internal static class FishingAreaDatabase
    {
        internal static readonly AreaFishData Norune = new AreaFishData
        {
            Id = 0, Name = "Norune",
            SlotBase = Addresses.fishSlotBase_Norune, SlotCount = 4, QuestBase = 0x21CE4416, GiverName = "Pike",
            FishIds = new byte[]
            {
                FishDatabase.Gobbler.Id, FishDatabase.Nonky.Id, FishDatabase.Gummy.Id, FishDatabase.Niler.Id,
            },
        };
        internal static readonly AreaFishData MatatakiWaterfall = new AreaFishData
        {
            Id = 1, Name = "Matataki Waterfall",
            SlotBase = Addresses.fishSlotBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
            FishIds = new byte[]
            {
                FishDatabase.Gummy.Id, FishDatabase.Nonky.Id, FishDatabase.BakuBaku.Id,
                FishDatabase.MardanGarayan.Id, FishDatabase.BaronGarayan.Id,
            },
        };
        internal static readonly AreaFishData Area19 = new AreaFishData
        {
            Id = 19, Name = "Area19",
            SlotBase = Addresses.fishSlotBase_Area19, SlotCount = 5, QuestBase = 0x21CE4427, GiverName = "Sam",
            QuestsDoneAddr = 0x21CE442F, PostLoopSrc = 0x21CE4430, PostLoopDst = 0x202A1FA0,
        };
        internal static readonly AreaFishData Area3 = new AreaFishData
        {
            Id = 3, Name = "Area3",
            SlotBase = Addresses.fishSlotBase_Area3, SlotCount = 4, QuestBase = 0x21CE4431, GiverName = "Devia",
        };

        private static readonly Dictionary<int, AreaFishData> ById;
        static FishingAreaDatabase()
        {
            AreaFishData[] allAreas = { Norune, MatatakiWaterfall, Area19, Area3 };
            ById = new Dictionary<int, AreaFishData>(allAreas.Length);
            foreach (AreaFishData area in allAreas) ById[area.Id] = area;
        }

        internal static bool TryGetValue(int areaId, out AreaFishData data) => ById.TryGetValue(areaId, out data);
    }
}
