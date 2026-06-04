using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// In-game time periods. Common spawn weights are time-invariant; only the rare-fish
    /// trigger rate varies per time period (see <see cref="AreaFishData.RareDivisors"/>).
    /// </summary>
    internal enum TimeOfDay { Morning = 0, Afternoon = 1, Dusk = 2, Night = 3 }

    /// <summary>
    /// Known data for a single fish species, mirroring the in-game slot layout.
    /// Null fields have not yet been confirmed from a slot dump or observed in gameplay.
    /// </summary>
    internal struct FishData
    {
        internal byte   Id;
        internal string Name;
        internal float? ScaleDivisor;        // +0x004; Visual scale divisor: ScaleModel = Size / ScaleDivisor at slot init. Not used in FP reward.
        internal float? BaseSize;            // +0x008; Base/center size for RNG distribution. Also the FP threshold: FP = BaseFp * Size / BaseSize for undersized fish.
        internal float? MaxSize;             // +0x00C; ×10 = display cm. Max size cap and FP upper-bound anchor.
        internal int?   BaseFp;              // +0x010; FP reward at exactly base size (Size = BaseSize); scales proportionally for smaller fish.
        internal int?   MaxFp;              // +0x014; FP reward at maximum size (Size = MaxSize).
        internal float? MinSize;            // 0.5 × BaseSize — the size floor enforced by ELF slot init (0x00240D60); not stored in slot
        // Bait affinity table (+0x018–+0x048): 13 floats, 4 bytes each.
        // 0.0 = never bites; 1.0 = normal; No observed values above 1.0 and setting a high value does not appear to increase catch rate, so values above 1.0 may be clamped in the game code.
        internal float? BaitAffEvy;
        internal float? BaitAffMimi;
        internal float? BaitAffPrickly;
        internal float? BaitAffThrobbingCherry;
        internal float? BaitAffGooeyPeach;
        internal float? BaitAffBombnuts;
        internal float? BaitAffPoisonousApple;
        internal float? BaitAffMellowBanana;
        internal float? BaitAffCarrot;
        internal float? BaitAffPotatoCake;
        internal float? BaitAffMinon;
        internal float? BaitAffBattan;
        internal float? BaitAffPetitefish;
    }

    /// <summary>
    /// Static repository of per-species <see cref="FishData"/> records, keyed by fish ID.
    /// All confirmed entries are sourced from natural slot dumps with no mod writes active.
    /// </summary>
    internal static class FishDatabase
    {
        internal static readonly FishData Bobo = new FishData
        {
            Id = 0, Name = "Bobo",
            ScaleDivisor = 17.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 20.0f,
            BaseFp = 20, MaxFp = 50,
            BaitAffEvy             = 1.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.0f,
            BaitAffBattan          = 0.0f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Gobbler   = new FishData
        {
            Id = 1, Name = "Gobbler",
            ScaleDivisor = 19.5f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 10, MaxFp = 30,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
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
            ScaleDivisor = 25.5f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 19.0f,
            BaseFp = 8, MaxFp = 25,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 1.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Kaiji = new FishData
        {
            Id = 3, Name = "Kaiji",
            ScaleDivisor = 35.4f, BaseSize = 12.0f,
            MinSize = 6.0f, MaxSize = 24.0f,
            BaseFp = 30, MaxFp = 60,
            BaitAffEvy             = 0.5f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.0f,
            BaitAffBattan          = 0.0f,
            BaitAffPetitefish      = 1.0f,
        };
        internal static readonly FishData BakuBaku  = new FishData
        {
            Id = 4, Name = "Baku Baku",
            ScaleDivisor = 28.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 10, MaxFp = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
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
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 16.0f,
            BaseFp = 200, MaxFp = 400,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeyPeach      = 0.0f,
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
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 15, MaxFp = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 1.0f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
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
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 10.0f,
            BaseFp = 20, MaxFp = 50,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 1.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData MissingFish = new FishData
        {
            Id = 8, Name = "Missing Fish",
            ScaleDivisor = 0.0f, BaseSize = 0.0f,
            MinSize = 0.0f, MaxSize = 0.0f,
            BaseFp = 0, MaxFp = 0,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeyPeach      = 0.0f,
            BaitAffBombnuts        = 0.0f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.0f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.0f,
            BaitAffBattan          = 0.0f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Umadakara = new FishData
        {
            Id = 9, Name = "Umadakara",
            ScaleDivisor = 21.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 100, MaxFp = 200,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeyPeach      = 0.0f,
            BaitAffBombnuts        = 0.0f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.0f,
            BaitAffCarrot          = 1.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.0f,
            BaitAffBattan          = 0.0f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Tarton = new FishData
        {
            Id = 10, Name = "Tarton",
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 40, MaxFp = 80,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 1.0f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.5f,
        };
        internal static readonly FishData Piccoly = new FishData
        {
            Id = 11, Name = "Piccoly",
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 20, MaxFp = 40,
            BaitAffEvy             = 1.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Bon = new FishData
        {
            Id = 12, Name = "Bon",
            ScaleDivisor = 15.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 10, MaxFp = 30,
            BaitAffEvy             = 1.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Hamahama = new FishData
        {
            Id = 13, Name = "Hamahama",
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 20.0f,
            BaseFp = 35, MaxFp = 70,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.0f,
            BaitAffMinon           = 0.0f,
            BaitAffBattan          = 0.0f,
            BaitAffPetitefish      = 1.0f,
        };
        internal static readonly FishData Negie = new FishData
        {
            Id = 14, Name = "Negie",
            ScaleDivisor = 21.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 15, MaxFp = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 1.0f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Den = new FishData
        {
            Id = 15, Name = "Den",
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 20.0f,
            BaseFp = 20, MaxFp = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 1.0f,
            BaitAffBattan          = 0.5f,
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Heela = new FishData
        {
            Id = 16, Name = "Heela",
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 14.0f,
            BaseFp = 20, MaxFp = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeyPeach      = 0.2f,
            BaitAffBombnuts        = 0.2f,
            BaitAffPoisonousApple  = 0.0f,
            BaitAffMellowBanana    = 0.2f,
            BaitAffCarrot          = 0.0f,
            BaitAffPotatoCake      = 0.5f,
            BaitAffMinon           = 0.5f,
            BaitAffBattan          = 1.0f,
            BaitAffPetitefish      = 1.0f,
        };
        internal static readonly FishData BaronGarayan = new FishData
        {
            Id = 17, Name = "Baron Garayan",
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 30.0f,
            BaseFp = 600, MaxFp = 1000,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeyPeach      = 0.0f,
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
                Bobo, Gobbler, Nonky, Kaiji, BakuBaku, MardanGarayan, Gummy, Niler,
                MissingFish, Umadakara, Tarton, Piccoly, Bon, Hamahama, Negie, Den,
                Heela, BaronGarayan,
            };
            ById = new Dictionary<byte, FishData>(allFish.Length);
            foreach (FishData fish in allFish) ById[fish.Id] = fish;
        }

        /// <summary>
        /// Looks up a fish by its numeric ID. Returns false if the species has no confirmed data entry.
        /// </summary>
        internal static bool TryGetValue(byte fishId, out FishData data) => ById.TryGetValue(fishId, out data);

        /// <summary>
        /// Returns the display name for a fish ID, or <c>"Unknown(N)"</c> if the ID has no entry.
        /// </summary>
        internal static string GetName(byte fishId) =>
            TryGetValue(fishId, out FishData fish) ? fish.Name : $"Unknown({fishId})";
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

        // ---- Spawn weight data (sourced from ELF VA 0x001A8960–0x001A8D44) ----

        /// <summary>
        /// Per-fish probability among common-fish outcomes. Time-invariant; values sum to 1.0.
        /// For areas without rare fish (<see cref="RareDivisors"/> == null) these are the direct
        /// per-slot probabilities. For areas with rare fish, multiply by (1 − 1.0/RareDivisors[t])
        /// to get the actual per-slot probability at time period t.
        /// </summary>
        internal Dictionary<byte, float> CommonWeights;

        /// <summary>
        /// $s0 rarity divisors indexed by <see cref="TimeOfDay"/> (Morning=0, Afternoon=1,
        /// Dusk=2, Night=3). The rare-fish pre-check fires when (random % $s0 == 0),
        /// giving a per-slot trigger probability of approximately 1/$s0. When triggered the
        /// common-fish selection is skipped and <see cref="RareSplits"/> governs instead.
        /// Null for areas with no rare fish (Norune, Peanut Pond, Queens Harbor).
        /// The time→divisor mapping is inferred from empirical rates; the game function at
        /// VA 0x00187E60 that returns the time index has not been fully decoded.
        /// </summary>
        internal int[]    RareDivisors;   // [Morning, Afternoon, Dusk, Night]

        /// <summary>
        /// Per-fish fraction of rare-trigger outcomes. Values sum to 1.0.
        /// Actual per-slot probability = RareSplits[id] × (1.0 / RareDivisors[t]).
        /// Null for areas with no rare fish.
        /// </summary>
        internal Dictionary<byte, float> RareSplits;

        /// <summary>
        /// Selects a fish species ID for one slot, replicating the spawner at ELF VA 0x001A8960.
        /// Assigned per area in <see cref="FishingAreaDatabase"/>; each implementation calls the
        /// appropriate mechanism function (and rare pre-check wrapper where applicable).
        /// Call once per slot; consumes one RNG value normally, two if the rare pre-check fires.
        /// </summary>
        internal Func<TimeOfDay, byte> SpawnFish;
    }

    /// <summary>
    /// Static repository of per-area <see cref="AreaFishData"/> records, keyed by area ID.
    /// Add a named entry here to support a new fishing area.
    /// </summary>
    internal static class FishingAreaDatabase
    {
        internal static readonly AreaFishData NorunePond = new AreaFishData
        {
            Id = 0, Name = "Norune Pond",
            SlotBase = FishSlotOffsets.AreaBase_Norune, SlotCount = 4, QuestBase = 0x21CE4416, GiverName = "Pike",
            FishIds = new byte[]
            {
                FishDatabase.Gobbler.Id, FishDatabase.Nonky.Id, FishDatabase.Gummy.Id, FishDatabase.Niler.Id,
            },
            CommonWeights = new Dictionary<byte, float>
            {
                [FishDatabase.Gobbler.Id] = 0.25f,
                [FishDatabase.Nonky.Id]   = 0.25f,
                [FishDatabase.Gummy.Id]   = 0.25f,
                [FishDatabase.Niler.Id]   = 0.25f,
            },
            RareDivisors = null,
            RareSplits   = null,
            SpawnFish    = _ => SpawnFourWayEqual(NorunePond.CommonWeights),         // FourWayEqual (VA 0x001A8A94)
        };
        internal static readonly AreaFishData MatatakiWaterfall = new AreaFishData
        {
            Id = 1, Name = "Matataki Waterfall",
            SlotBase = FishSlotOffsets.AreaBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
            FishIds = new byte[]
            {
                FishDatabase.Gummy.Id, FishDatabase.Nonky.Id, FishDatabase.BakuBaku.Id,
                FishDatabase.MardanGarayan.Id, FishDatabase.BaronGarayan.Id,
            },
            CommonWeights = new Dictionary<byte, float>
            {
                [FishDatabase.Nonky.Id]    = 1f / 3f,  // random%3 == 0
                [FishDatabase.BakuBaku.Id] = 1f / 3f,  // random%3 == 1
                [FishDatabase.Gummy.Id]    = 1f / 3f,  // random%3 == 2
            },
            // Rare-fish trigger: random % $s0 == 0. $s0 values inferred from empirical rates.
            RareDivisors = new int[] { 25, 35, 20, 50 },   // [Morning, Afternoon, Dusk, Night]
            RareSplits = new Dictionary<byte, float>
            {
                [FishDatabase.MardanGarayan.Id] = 4f / 5f,  // new_random % 5 != 0
                [FishDatabase.BaronGarayan.Id]  = 1f / 5f,  // new_random % 5 == 0
            },
            SpawnFish = time => SpawnWithRareCheck(MatatakiWaterfall.RareDivisors, time,  // ThreeWayEqualMod3 (VA 0x001A8BD4)
                () => SpawnThreeWayEqualMod3(MatatakiWaterfall.CommonWeights)),
        };
        // Synthetic ID 100 — shares slot block and quest with MatatakiWaterfall.
        // Resolved from area ID 1 via player Y position: Peanut Pond ≈ Y=-1103, Waterfall ≈ Y=720. Split at Y=0.
        internal static readonly AreaFishData PeanutPond = new AreaFishData
        {
            Id = 100, Name = "Peanut Pond",
            SlotBase = FishSlotOffsets.AreaBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
            FishIds = new byte[]
            {
                FishDatabase.Tarton.Id, FishDatabase.Gobbler.Id, FishDatabase.BakuBaku.Id,
                FishDatabase.Umadakara.Id,
            },
            CommonWeights = new Dictionary<byte, float>
            {
                [FishDatabase.Gobbler.Id]   = 0.35f,  // random%100 < 35
                [FishDatabase.BakuBaku.Id]  = 0.35f,  // 35 ≤ random%100 < 70
                [FishDatabase.Umadakara.Id] = 0.10f,  // 70 ≤ random%100 < 80
                [FishDatabase.Tarton.Id]    = 0.20f,  // 80 ≤ random%100 < 100
            },
            RareDivisors = null,
            RareSplits   = null,
            SpawnFish    = _ => SpawnRandomMod100(PeanutPond.CommonWeights),              // RandomMod100 (VA 0x001A8B00)
        };
        internal static readonly AreaFishData QueensHarbor = new AreaFishData
        {
            Id = 19, Name = "Queens Harbor",
            SlotBase = FishSlotOffsets.AreaBase_Queens, SlotCount = 5, QuestBase = 0x21CE4427, GiverName = "Sam",
            QuestsDoneAddr = 0x21CE442F, PostLoopSrc = 0x21CE4430, PostLoopDst = 0x202A1FA0,
            FishIds = new byte[]
            {
                FishDatabase.Bobo.Id, FishDatabase.Kaiji.Id, FishDatabase.Piccoly.Id,
                FishDatabase.Bon.Id, FishDatabase.Hamahama.Id,
            },
            CommonWeights = new Dictionary<byte, float>
            {
                [FishDatabase.Bobo.Id]     = 0.20f,  // random%100 < 20
                [FishDatabase.Kaiji.Id]    = 0.20f,  // 20 ≤ random%100 < 40
                [FishDatabase.Piccoly.Id]  = 0.20f,  // 40 ≤ random%100 < 60
                [FishDatabase.Bon.Id]      = 0.20f,  // 60 ≤ random%100 < 80
                [FishDatabase.Hamahama.Id] = 0.20f,  // 80 ≤ random%100 < 100
            },
            RareDivisors = null,
            RareSplits   = null,
            SpawnFish    = _ => SpawnRandomMod100(QueensHarbor.CommonWeights),             // RandomMod100 (VA 0x001A8C1C)
        };
        internal static readonly AreaFishData MuskaLackaOasis = new AreaFishData
        {
            Id = 3, Name = "Muska Lacka Oasis",
            SlotBase = FishSlotOffsets.AreaBase_MuskaLacka, SlotCount = 4, QuestBase = 0x21CE4431, GiverName = "Devia",
            FishIds = new byte[]
            {
                FishDatabase.Negie.Id, FishDatabase.Den.Id, FishDatabase.Heela.Id,
                FishDatabase.MardanGarayan.Id, FishDatabase.BaronGarayan.Id,
            },
            CommonWeights = new Dictionary<byte, float>
            {
                [FishDatabase.Negie.Id] = 0.40f,  // random%100 < 40
                [FishDatabase.Den.Id]   = 0.30f,  // 40 ≤ random%100 < 70
                [FishDatabase.Heela.Id] = 0.30f,  // 70 ≤ random%100 < 100
            },
            // Rare-fish trigger: random % $s0 == 0. $s0 values inferred from empirical rates.
            RareDivisors = new int[] { 25, 35, 20, 50 },   // [Morning, Afternoon, Dusk, Night]
            RareSplits = new Dictionary<byte, float>
            {
                [FishDatabase.MardanGarayan.Id] = 4f / 5f,  // new_random % 5 != 0
                [FishDatabase.BaronGarayan.Id]  = 1f / 5f,  // new_random % 5 == 0
            },
            SpawnFish = time => SpawnWithRareCheck(MuskaLackaOasis.RareDivisors, time,    // RandomMod100 (VA 0x001A8CF0)
                () => SpawnRandomMod100(MuskaLackaOasis.CommonWeights)),
        };

        // ---- Spawn mechanism functions ----
        // Called by each area's SpawnFish delegate. Match the logic in ELF VA 0x001A8960–0x001A8D44.

        // VA 0x001A8A94: RNG output masked to [0,3] selects one of four equal-probability fish.
        private static byte SpawnFourWayEqual(Dictionary<byte, float> weights)
        {
            int pick = Fishing.Rng.Next(4);
            int i = 0;
            byte lastFishId = 0;
            foreach (byte fishId in weights.Keys) { if (i++ == pick) return fishId; lastFishId = fishId; }
            return lastFishId;
        }

        // VA 0x001A8BD4: random % 3; outcomes 0/1/2 each map to one fish.
        private static byte SpawnThreeWayEqualMod3(Dictionary<byte, float> weights)
        {
            int pick = Fishing.Rng.Next(3);
            int i = 0;
            byte lastFishId = 0;
            foreach (byte fishId in weights.Keys) { if (i++ == pick) return fishId; lastFishId = fishId; }
            return lastFishId;
        }

        // VA 0x001A8B00+: random % 100 checked against sequential cumulative thresholds.
        private static byte SpawnRandomMod100(Dictionary<byte, float> weights)
        {
            int roll = Fishing.Rng.Next(100);
            float cumulative = 0f;
            byte lastFishId = 0;
            foreach (var weightEntry in weights)
            {
                cumulative += weightEntry.Value * 100f;
                if (roll < cumulative) return weightEntry.Key;
                lastFishId = weightEntry.Key;
            }
            return lastFishId;
        }

        // Rare-fish pre-check wrapper (Matataki Waterfall and Muska Lacka Oasis).
        // Mirrors: if (random % $s0 == 0) → rare branch; $s0 = rareDivisors[(int)time].
        // When triggered: new_random % 5 == 0 → Baron Garayan, else Mardan Garayan.
        private static byte SpawnWithRareCheck(int[] rareDivisors, TimeOfDay time, Func<byte> commonSpawn)
        {
            if (Fishing.Rng.Next(rareDivisors[(int)time]) == 0)
                return Fishing.Rng.Next(5) == 0
                    ? FishDatabase.BaronGarayan.Id
                    : FishDatabase.MardanGarayan.Id;
            return commonSpawn();
        }

        private static readonly Dictionary<int, AreaFishData> ById;
        static FishingAreaDatabase()
        {
            AreaFishData[] allAreas = { NorunePond, MatatakiWaterfall, PeanutPond, QueensHarbor, MuskaLackaOasis };
            ById = new Dictionary<int, AreaFishData>(allAreas.Length);
            foreach (AreaFishData area in allAreas) ById[area.Id] = area;
        }

        internal static bool TryGetValue(int areaId, out AreaFishData data) => ById.TryGetValue(areaId, out data);
    }
}
