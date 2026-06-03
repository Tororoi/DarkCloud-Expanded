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
        internal const int AiState             = 0x050; // fish AI behavior state (see FishingState.FishAiState_*)
        internal const int AiStateTimer        = 0x058; // countdown timer for current AI state; underflows to FFFFFFFF to trigger state transition
        internal const int Size                = 0x060;
        internal const int ScaleX              = 0x064;
        internal const int ScaleY              = 0x068;
        internal const int Heading             = 0x074;
        internal const int LivePosY            = 0x0B0;
        internal const int LivePosZ            = 0x0B4;
        internal const int LivePosX            = 0x0B8;
    }

    /// <summary>Addresses and state values for the fishing-related state machines.</summary>
    internal static class FishingState
    {
        // Addresses
        internal const int FishingStateAddr  = 0x21D19714; // 0 = not fishing, 1 = session active
        internal const int TriggerIndexAddr  = 0x202A1F64; // game writes active fishing trigger here; not reliable for area identification
        internal const int Addr708           = 0x21D19708; // overworld / dialog state machine
        internal const int PhaseAddr         = 0x21D33E28; // fishing phase state machine

        // Addr708 state values
        internal const int State708_QuitDialog  = 0x00000085; // "Continue fishing" / "Quit fishing" dialog
        internal const int State708_BaitScreen  = 0x00000086; // bait selection screen open
        internal const int State708_Overworld   = 0x0000000C; // back in overworld

        // FishSlotOffsets.AiState values — confirmed via phase logger datamining
        internal const int FishAiState_Dormant     = unchecked((int)0xFFFFFFFF); // not participating this cast; fish far from hook
        internal const int FishAiState_Approaching = 6;  // swimming toward the hook at slow speed (~0.1)
        internal const int FishAiState_Nibbling    = 7;  // arrived at hook, stationary, nibbling bait
        internal const int FishAiState_Unk08       = 8;  // observed briefly during approach before going dormant; purpose unknown
        internal const int FishAiState_Patrolling  = 9;  // fast patrol movement (>0.3 spd), farther from hook
        internal const int FishAiState_Hooked      = 11; // fish is on the hook, being reeled in

        // PhaseAddr state values
        internal const int Phase_Idle          = 0x00000000; // default/idle phase; bait screen is Addr708=State708_BaitScreen
        internal const int Phase_Walking       = 0x00000002;
        internal const int Phase_Casting       = 0x00000004;
        internal const int Phase_HookInWater   = 0x00000005;
        internal const int Phase_NibblePull    = 0x00000006; // player pulled rod while fish was nibbling
        internal const int Phase_Uncasting     = 0x00000007; // X cancel while rod out
        internal const int Phase_HoldingFish   = 0x00000008; // measurements shown
        internal const int Phase_ThrowingBack  = 0x00000009; // landing animation
        internal const int Phase_PullingOut    = 0x0000000A; // fish leaving water
        internal const int Phase_ReelingIn     = 0x0000000C;
        internal const int Phase_DraggingHook  = 0x0000000D; // moving Toan to drag hook
        internal const int Phase_IdleTapping   = 0x0000000E; // Toan tapping foot idle animation
        internal const int Phase_IdleCrouching = 0x0000000F; // Toan crouching idle animation
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
            Unk004 = 17.0f, Unk008 = 10.0f,
            EstimatedMinSize = 7.2945f, MaxSize = 20.0f,
            FpMin = 20, FpMax = 50,
            BaitAffEvy             = 1.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
            Unk004 = 19.5f, Unk008 = 8.0f,
            EstimatedMinSize = 5.0f, MaxSize = 16.0f,
            FpMin = 10, FpMax = 30,
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
            EstimatedMinSize = 4.0000f, MaxSize = 19.0f,
            FpMin = 8, FpMax = 25,
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
        internal static readonly FishData Kaiji = new FishData
        {
            Id = 3, Name = "Kaiji",
            Unk004 = 35.4f, Unk008 = 12.0f,
            EstimatedMinSize = 9.0039f, MaxSize = 24.0f,
            FpMin = 30, FpMax = 60,
            BaitAffEvy             = 0.5f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
            Unk004 = 28.0f, Unk008 = 8.0f,
            EstimatedMinSize = 5.0f, MaxSize = 16.0f,
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
            EstimatedMinSize = 3.5784f, MaxSize = 12.0f,
            FpMin = 15, FpMax = 40,
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
            EstimatedMinSize = 4.2954f, MaxSize = 10.0f,
            FpMin = 20, FpMax = 50,
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
        internal static readonly FishData Umadakara = new FishData
        {
            Id = 9, Name = "Umadakara",
            Unk004 = 21.0f, Unk008 = 8.0f,
            EstimatedMinSize = 5.5702f, MaxSize = 16.0f,
            FpMin = 100, FpMax = 200,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.0f,
            BaitAffGooeypeach      = 0.0f,
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
            Unk004 = 20.0f, Unk008 = 8.0f,
            EstimatedMinSize = 4.5092f, MaxSize = 16.0f,
            FpMin = 40, FpMax = 80,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.5f,
            BaitAffPrickly         = 0.5f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
            Unk004 = 20.0f, Unk008 = 6.0f,
            EstimatedMinSize = 4.5388f, MaxSize = 12.0f,
            FpMin = 20, FpMax = 40,
            BaitAffEvy             = 1.0f,
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
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Bon = new FishData
        {
            Id = 12, Name = "Bon",
            Unk004 = 15.0f, Unk008 = 6.0f,
            EstimatedMinSize = 4.0435f, MaxSize = 12.0f,
            FpMin = 10, FpMax = 30,
            BaitAffEvy             = 1.0f,
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
            BaitAffPetitefish      = 0.0f,
        };
        internal static readonly FishData Hamahama = new FishData
        {
            Id = 13, Name = "Hamahama",
            Unk004 = 21.0f, Unk008 = 10.0f,
            EstimatedMinSize = 7.3150f, MaxSize = 20.0f,
            FpMin = 35, FpMax = 70,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
            Unk004 = 21.0f, Unk008 = 8.0f,
            EstimatedMinSize = 4.6f, MaxSize = 16.0f,
            FpMin = 15, FpMax = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
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
        internal static readonly FishData Den = new FishData
        {
            Id = 15, Name = "Den",
            Unk004 = 20.0f, Unk008 = 8.0f,
            EstimatedMinSize = 4.0f, MaxSize = 20.0f,
            FpMin = 20, FpMax = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
            Unk004 = 20.0f, Unk008 = 8.0f,
            EstimatedMinSize = 5.9053f, MaxSize = 14.0f,
            FpMin = 20, FpMax = 40,
            BaitAffEvy             = 0.0f,
            BaitAffMimi            = 0.0f,
            BaitAffPrickly         = 0.0f,
            BaitAffThrobbingCherry = 0.2f,
            BaitAffGooeypeach      = 0.2f,
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
        // Per-fish spawn weights for this area. fishId → float[4] = [Morning, Afternoon, Dusk, Night].
        // 0.0 = never; values represent relative probability within this area and time period.
        internal Dictionary<byte, float[]> SpawnWeights;
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
            SlotBase = Addresses.fishSlotBase_Norune, SlotCount = 4, QuestBase = 0x21CE4416, GiverName = "Pike",
            FishIds = new byte[]
            {
                FishDatabase.Gobbler.Id, FishDatabase.Nonky.Id, FishDatabase.Gummy.Id, FishDatabase.Niler.Id,
            },
            SpawnWeights = new Dictionary<byte, float[]>
            {
                [FishDatabase.Gobbler.Id] = new float[]
                {
                    0.26f,  // M=157sess(H1=70,H2=87,pk=11)  160slots(H1=74,H2=86,pk=7) [703 total]
                    0.25f,  // A=158sess(H1=72,H2=86,pk=12)  155slots(H1=59,H2=96,pk=12) [703 total]
                    0.25f,  // D=208sess(H1=93,H2=115,pk=15) 210slots(H1=94,H2=116,pk=17) [703 total]
                    0.22f,  // N=180sess(H1=93,H2=87,pk=11)  156slots(H1=84,H2=72,pk=11) [703 total]
                },
                [FishDatabase.Nonky.Id] = new float[]
                {
                    0.25f,  // M=157sess(H1=70,H2=87,pk=11)  158slots(H1=82,H2=76,pk=16) [703 total]
                    0.25f,  // A=158sess(H1=72,H2=86,pk=12)  160slots(H1=82,H2=78,pk=14) [703 total]
                    0.23f,  // D=208sess(H1=93,H2=115,pk=15) 188slots(H1=82,H2=106,pk=9) [703 total]
                    0.27f,  // N=180sess(H1=93,H2=87,pk=11)  196slots(H1=95,H2=101,pk=16) [703 total]
                },
                [FishDatabase.Gummy.Id] = new float[]
                {
                    0.26f,  // M=157sess(H1=70,H2=87,pk=11)  162slots(H1=68,H2=94,pk=10) [703 total]
                    0.25f,  // A=158sess(H1=72,H2=86,pk=12)  158slots(H1=82,H2=76,pk=8) [703 total]
                    0.28f,  // D=208sess(H1=93,H2=115,pk=15) 235slots(H1=110,H2=125,pk=22) [703 total]
                    0.27f,  // N=180sess(H1=93,H2=87,pk=11)  191slots(H1=107,H2=84,pk=12) [703 total]
                },
                [FishDatabase.Niler.Id] = new float[]
                {
                    0.24f,  // M=157sess(H1=70,H2=87,pk=11)  148slots(H1=56,H2=92,pk=11) [703 total]
                    0.25f,  // A=158sess(H1=72,H2=86,pk=12)  159slots(H1=65,H2=94,pk=14) [703 total]
                    0.24f,  // D=208sess(H1=93,H2=115,pk=15) 199slots(H1=86,H2=113,pk=12) [703 total]
                    0.25f,  // N=180sess(H1=93,H2=87,pk=11)  177slots(H1=86,H2=91,pk=5) [703 total]
                },
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
            SpawnWeights = new Dictionary<byte, float[]>
            {
                [FishDatabase.Nonky.Id] = new float[]
                {
                    0.36f,   // M=53sess(H1=24,H2=29,pk=4)  95slots(H1=52,H2=43,pk=7) [174 total]
                    0.30f,   // A=21sess(H1=21,H2=0,pk=0)   31slots(H1=31,H2=0,pk=0) [174 total]
                    0.28f,   // D=48sess(H1=19,H2=29,pk=4)  66slots(H1=24,H2=42,pk=10) [174 total]
                    0.32f,   // N=52sess(H1=23,H2=29,pk=3)  82slots(H1=36,H2=46,pk=4) [174 total]
                },
                [FishDatabase.BakuBaku.Id] = new float[]
                {
                    0.30f,   // M=53sess(H1=24,H2=29,pk=4)  79slots(H1=30,H2=49,pk=6) [174 total]
                    0.29f,   // A=21sess(H1=21,H2=0,pk=0)   30slots(H1=30,H2=0,pk=0) [174 total]
                    0.38f,   // D=48sess(H1=19,H2=29,pk=4)  92slots(H1=36,H2=56,pk=3) [174 total]
                    0.33f,   // N=52sess(H1=23,H2=29,pk=3)  85slots(H1=33,H2=52,pk=6) [174 total]
                },
                [FishDatabase.Gummy.Id] = new float[]
                {
                    0.29f,   // M=53sess(H1=24,H2=29,pk=4)  78slots(H1=34,H2=44,pk=7) [174 total]
                    0.42f,   // A=21sess(H1=21,H2=0,pk=0)   44slots(H1=44,H2=0,pk=0) [174 total]
                    0.28f,   // D=48sess(H1=19,H2=29,pk=4)  68slots(H1=27,H2=41,pk=6) [174 total]
                    0.32f,   // N=52sess(H1=23,H2=29,pk=3)  83slots(H1=40,H2=43,pk=4) [174 total]
                },
                [FishDatabase.MardanGarayan.Id] = new float[]
                {
                    0.05f,   // M=53sess(H1=24,H2=29,pk=4)  12slots(H1=4,H2=8,pk=0) [174 total]
                    0.00f,   // A=21sess(H1=21,H2=0,pk=0)   0slots [174 total]
                    0.04f,   // D=48sess(H1=19,H2=29,pk=4)  10slots(H1=6,H2=4,pk=0) [174 total]
                    0.04f,   // N=52sess(H1=23,H2=29,pk=3)  9slots(H1=5,H2=4,pk=1) [174 total]
                },
                [FishDatabase.BaronGarayan.Id] = new float[]
                {
                    0.004f,  // M=53sess(H1=24,H2=29,pk=4)  1slots(H1=0,H2=1,pk=0) [174 total]
                    0.000f,  // A=21sess(H1=21,H2=0,pk=0)   0slots [174 total]
                    0.017f,  // D=48sess(H1=19,H2=29,pk=4)  4slots(H1=2,H2=2,pk=1) [174 total]
                    0.004f,  // N=52sess(H1=23,H2=29,pk=3)  1slots(H1=1,H2=0,pk=0) [174 total]
                },
            },
        };
        // Synthetic ID 100 — shares slot block and quest with MatatakiWaterfall.
        // Resolved from area ID 1 via player Y position: Peanut Pond ≈ Y=-1103, Waterfall ≈ Y=720. Split at Y=0.
        internal static readonly AreaFishData PeanutPond = new AreaFishData
        {
            Id = 100, Name = "Peanut Pond",
            SlotBase = Addresses.fishSlotBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
            FishIds = new byte[]
            {
                FishDatabase.Tarton.Id, FishDatabase.Gobbler.Id, FishDatabase.BakuBaku.Id,
                FishDatabase.Umadakara.Id,
            },
            SpawnWeights = new Dictionary<byte, float[]>
            {
                [FishDatabase.Gobbler.Id] = new float[]
                {
                    0.32f,  // M=53sess(H1=24,H2=29,pk=4)  85slots(H1=36,H2=49,pk=6) [244 total]
                    0.40f,  // A=52sess(H1=23,H2=29,pk=3)  103slots(H1=42,H2=61,pk=5) [244 total]
                    0.39f,  // D=86sess(H1=46,H2=40,pk=8)  168slots(H1=95,H2=73,pk=20) [244 total]
                    0.30f,  // N=53sess(H1=24,H2=29,pk=4)  79slots(H1=33,H2=46,pk=8) [244 total]
                },
                [FishDatabase.BakuBaku.Id] = new float[]
                {
                    0.38f,  // M=53sess(H1=24,H2=29,pk=4)  101slots(H1=47,H2=54,pk=8) [244 total]
                    0.33f,  // A=52sess(H1=23,H2=29,pk=3)  85slots(H1=33,H2=52,pk=4) [244 total]
                    0.34f,  // D=86sess(H1=46,H2=40,pk=8)  147slots(H1=78,H2=69,pk=11) [244 total]
                    0.38f,  // N=53sess(H1=24,H2=29,pk=4)  101slots(H1=44,H2=57,pk=8) [244 total]
                },
                [FishDatabase.Umadakara.Id] = new float[]
                {
                    0.10f,  // M=53sess(H1=24,H2=29,pk=4)  27slots(H1=13,H2=14,pk=1) [244 total]
                    0.10f,  // A=52sess(H1=23,H2=29,pk=3)  26slots(H1=10,H2=16,pk=4) [244 total]
                    0.10f,  // D=86sess(H1=46,H2=40,pk=8)  45slots(H1=23,H2=22,pk=3) [244 total]
                    0.08f,  // N=53sess(H1=24,H2=29,pk=4)  21slots(H1=12,H2=9,pk=1) [244 total]
                },
                [FishDatabase.Tarton.Id] = new float[]
                {
                    0.20f,  // M=53sess(H1=24,H2=29,pk=4)  52slots(H1=24,H2=28,pk=5) [244 total]
                    0.18f,  // A=52sess(H1=23,H2=29,pk=3)  46slots(H1=30,H2=16,pk=2) [244 total]
                    0.16f,  // D=86sess(H1=46,H2=40,pk=8)  70slots(H1=34,H2=36,pk=6) [244 total]
                    0.24f,  // N=53sess(H1=24,H2=29,pk=4)  64slots(H1=31,H2=33,pk=3) [244 total]
                },
            },
        };
        internal static readonly AreaFishData QueensHarbor = new AreaFishData
        {
            Id = 19, Name = "Queens Harbor",
            SlotBase = Addresses.fishSlotBase_Queens, SlotCount = 5, QuestBase = 0x21CE4427, GiverName = "Sam",
            QuestsDoneAddr = 0x21CE442F, PostLoopSrc = 0x21CE4430, PostLoopDst = 0x202A1FA0,
            FishIds = new byte[]
            {
                FishDatabase.Bobo.Id, FishDatabase.Kaiji.Id, FishDatabase.Piccoly.Id,
                FishDatabase.Bon.Id, FishDatabase.Hamahama.Id,
            },
            SpawnWeights = new Dictionary<byte, float[]>
            {
                [FishDatabase.Bobo.Id] = new float[]
                {
                    0.24f,  // M=58sess(H1=29,H2=29,pk=4)  71slots(H1=35,H2=36,pk=5) [160 total]
                    0.23f,  // A=16sess(H1=16,H2=0,pk=0)   18slots(H1=18,H2=0,pk=0) [160 total]
                    0.20f,  // D=27sess(H1=0,H2=27,pk=0)   27slots(H1=0,H2=27,pk=0) [160 total]
                    0.21f,  // N=59sess(H1=29,H2=30,pk=4)  63slots(H1=32,H2=31,pk=6) [160 total]
                },
                [FishDatabase.Kaiji.Id] = new float[]
                {
                    0.17f,  // M=58sess(H1=29,H2=29,pk=4)  48slots(H1=23,H2=25,pk=4) [160 total]
                    0.19f,  // A=16sess(H1=16,H2=0,pk=0)   15slots(H1=15,H2=0,pk=0) [160 total]
                    0.22f,  // D=27sess(H1=0,H2=27,pk=0)   29slots(H1=0,H2=29,pk=0) [160 total]
                    0.18f,  // N=59sess(H1=29,H2=30,pk=4)  53slots(H1=28,H2=25,pk=1) [160 total]
                },
                [FishDatabase.Piccoly.Id] = new float[]
                {
                    0.21f,  // M=58sess(H1=29,H2=29,pk=4)  60slots(H1=28,H2=32,pk=2) [160 total]
                    0.19f,  // A=16sess(H1=16,H2=0,pk=0)   15slots(H1=15,H2=0,pk=0) [160 total]
                    0.23f,  // D=27sess(H1=0,H2=27,pk=0)   31slots(H1=0,H2=31,pk=0) [160 total]
                    0.21f,  // N=59sess(H1=29,H2=30,pk=4)  63slots(H1=35,H2=28,pk=6) [160 total]
                },
                [FishDatabase.Bon.Id] = new float[]
                {
                    0.16f,  // M=58sess(H1=29,H2=29,pk=4)  47slots(H1=26,H2=21,pk=4) [160 total]
                    0.29f,  // A=16sess(H1=16,H2=0,pk=0)   23slots(H1=23,H2=0,pk=0) [160 total]
                    0.19f,  // D=27sess(H1=0,H2=27,pk=0)   25slots(H1=0,H2=25,pk=0) [160 total]
                    0.17f,  // N=59sess(H1=29,H2=30,pk=4)  49slots(H1=16,H2=33,pk=3) [160 total]
                },
                [FishDatabase.Hamahama.Id] = new float[]
                {
                    0.22f,  // M=58sess(H1=29,H2=29,pk=4)  64slots(H1=33,H2=31,pk=5) [160 total]
                    0.11f,  // A=16sess(H1=16,H2=0,pk=0)   9slots(H1=9,H2=0,pk=0) [160 total]
                    0.17f,  // D=27sess(H1=0,H2=27,pk=0)   23slots(H1=0,H2=23,pk=0) [160 total]
                    0.23f,  // N=59sess(H1=29,H2=30,pk=4)  67slots(H1=34,H2=33,pk=4) [160 total]
                },
            },
        };
        internal static readonly AreaFishData MuskaLackaOasis = new AreaFishData
        {
            Id = 3, Name = "Muska Lacka Oasis",
            SlotBase = Addresses.fishSlotBase_MuskaLacka, SlotCount = 4, QuestBase = 0x21CE4431, GiverName = "Devia",
            FishIds = new byte[]
            {
                FishDatabase.Negie.Id, FishDatabase.Den.Id, FishDatabase.Heela.Id,
                FishDatabase.MardanGarayan.Id, FishDatabase.BaronGarayan.Id,
            },
            SpawnWeights = new Dictionary<byte, float[]>
            {
                [FishDatabase.Negie.Id] = new float[]
                {
                    0.34f,  // M=67sess(H1=38,H2=29,pk=1)  90slots(H1=54,H2=36,pk=2) [436 total]
                    0.39f,  // A=53sess(H1=24,H2=29,pk=4)  83slots(H1=42,H2=41,pk=7) [436 total]
                    0.39f,  // D=157sess(H1=69,H2=88,pk=11) 243slots(H1=105,H2=138,pk=13) [436 total]
                    0.41f,  // N=159sess(H1=72,H2=87,pk=12) 258slots(H1=111,H2=147,pk=20) [436 total]
                },
                [FishDatabase.Den.Id] = new float[]
                {
                    0.34f,  // M=67sess(H1=38,H2=29,pk=1)  90slots(H1=45,H2=45,pk=1) [436 total]
                    0.26f,  // A=53sess(H1=24,H2=29,pk=4)  55slots(H1=24,H2=31,pk=3) [436 total]
                    0.26f,  // D=157sess(H1=69,H2=88,pk=11) 161slots(H1=72,H2=89,pk=18) [436 total]
                    0.27f,  // N=159sess(H1=72,H2=87,pk=12) 170slots(H1=80,H2=90,pk=9) [436 total]
                },
                [FishDatabase.Heela.Id] = new float[]
                {
                    0.29f,  // M=67sess(H1=38,H2=29,pk=1)  77slots(H1=46,H2=31,pk=1) [436 total]
                    0.32f,  // A=53sess(H1=24,H2=29,pk=4)  67slots(H1=30,H2=37,pk=6) [436 total]
                    0.30f,  // D=157sess(H1=69,H2=88,pk=11) 189slots(H1=83,H2=106,pk=9) [436 total]
                    0.31f,  // N=159sess(H1=72,H2=87,pk=12) 195slots(H1=89,H2=106,pk=18) [436 total]
                },
                [FishDatabase.MardanGarayan.Id] = new float[]
                {
                    0.030f,  // M=67sess(H1=38,H2=29,pk=1)  8slots(H1=5,H2=3,pk=0) [436 total]
                    0.028f,  // A=53sess(H1=24,H2=29,pk=4)  6slots(H1=0,H2=6,pk=0) [436 total]
                    0.045f,  // D=157sess(H1=69,H2=88,pk=11) 28slots(H1=15,H2=13,pk=3) [436 total]
                    0.016f,  // N=159sess(H1=72,H2=87,pk=12) 10slots(H1=6,H2=4,pk=1) [436 total]
                },
                [FishDatabase.BaronGarayan.Id] = new float[]
                {
                    0.011f,  // M=67sess(H1=38,H2=29,pk=1)  3slots(H1=2,H2=1,pk=0) [436 total]
                    0.005f,  // A=53sess(H1=24,H2=29,pk=4)  1slots(H1=0,H2=1,pk=0) [436 total]
                    0.011f,  // D=157sess(H1=69,H2=88,pk=11) 7slots(H1=1,H2=6,pk=1) [436 total]
                    0.005f,  // N=159sess(H1=72,H2=87,pk=12) 3slots(H1=2,H2=1,pk=0) [436 total]
                },
            },
        };

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
