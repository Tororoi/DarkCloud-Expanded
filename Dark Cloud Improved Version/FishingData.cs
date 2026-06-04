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
        internal float? MinSize;            // +0x000; 0.5 × BaseSize — the size floor enforced by ELF slot init (0x00240D60)
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

    /// <summary>
    /// Field offsets within a single fish slot, relative to the slot's base address.
    /// Slot stride is 0x2410; per-area base addresses are in <see cref="Addresses"/> (fishSlotBase_*).
    /// All fields confirmed via slot dump analysis unless noted otherwise.
    /// </summary>
    internal static class FishSlotOffsets
    {
        internal const int Stride = 0x2410;

        // ---- Species identity + static data ----
        // Written by the game from FishSpeciesTable each time a fish spawns.
        // Slot writes are effective for the current session; modify FishSpeciesTable to persist across spawns.
        internal const int SpeciesId           = 0x000;  // byte; fish species ID (indexes FishDatabase)
        // ELF 0x00240D60 (slot init, runs once): Calls RNG → Size = BaseSize, then ±= RNG*(MaxSize-BaseSize)/4 or /8,
        // clamped to [0.5*BaseSize, MaxSize]. ScaleModel = Size/ScaleDivisor; slot+0x68 = Size/25.
        // ELF 0x00240E80 (FP reward): if Size < BaseSize → FP = BaseFp*Size/BaseSize;
        //                             if Size ≥ BaseSize → FP = BaseFp + (MaxFp-BaseFp)*(Size-BaseSize)/(MaxSize-BaseSize).
        // Empirical check: 92cm Mardan (Size=9.2, BaseSize=10.0, BaseFp=200) → 200*9.2/10 = 184 FP ✓
        internal const int ScaleDivisor           = 0x004;  // float; visual scale divisor — ScaleModel = Size / ScaleDivisor (ELF 0x00240D60); not used in FP reward
        internal const int BaseSize              = 0x008;  // float; base/center size for RNG distribution and FP threshold (ELF 0x00240D60, 0x00240E80)
        internal const int MaxSize             = 0x00C;  // float; ×10 = display cm; maximum size cap and FP upper bound (ELF 0x00240D60, 0x00240E80)
        internal const int BaseFp               = 0x010;  // int; FP reward when Size = BaseSize (base size); scales down proportionally below BaseSize (ELF 0x00240E80)
        internal const int MaxFp               = 0x014;  // int; FP reward when Size = MaxSize (ELF 0x00240E80)
        // Bait affinity floats — 13 entries matching BaitNoticeRadiusTable order
        // 0.0 = never bites; 1.0 = normal; values above 1.0 may be clamped by game code
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

        // ---- AI behavior state ----
        internal const int AiState             = 0x050;  // int; current behavior (see FishingState.FishAiState_*)
        internal const int Unk054              = 0x054;  // unknown
        internal const int AiStateTimer        = 0x058;  // uint; read-only — countdown managed by game state machine; underflows to 0xFFFFFFFF to trigger transition

        // ---- Size and render scale ----
        internal const int Size                = 0x060;  // float; ×10 = display cm
        internal const int ScaleModel              = 0x064;  // float; Size / ScaleDivisor (ELF 0x00240D60)
        internal const int ScaleFixed              = 0x068;  // float; Size / 25.0 (ELF 0x00240D60; constant divisor)

        // ---- Movement ----
        internal const int Heading             = 0x074;  // float; current facing angle (radians)
        internal const int Speed               = 0x080;  // float; current movement speed
        internal const int Velocity            = 0x084;  // float; ramps up as fish accelerates
        internal const int Unk088              = 0x088;  // int; small value (3–10); consistently 4 during Approaching/Nibbling — purpose unknown
        internal const int NoticeRadius        = 0x08C;  // float; read-only — overwritten every frame from BaitNoticeRadiusTable; 3D radius within which fish transitions Dormant→Approaching

        // ---- AI target position ----
        internal const int AiTargetY           = 0x090;  // float; destination Y; converges to hook Y while Approaching
        internal const int AiTargetZ           = 0x094;  // float; destination Z (depth); converges to hook depth while Approaching
        internal const int AiTargetX           = 0x098;  // float; destination X; converges to hook X while Approaching

        // ---- Live world position ----
        internal const int LivePosY            = 0x0B0;  // float
        internal const int LivePosZ            = 0x0B4;  // float
        internal const int LivePosX            = 0x0B8;  // float

        // ---- Unknown ----
        internal const int Unk130              = 0x130;  // float; purpose unknown
        internal const int Unk134              = 0x134;  // float; purpose unknown
        internal const int Unk138              = 0x138;  // float; purpose unknown
        internal const int Unk150              = 0x150;  // float; purpose unknown
        internal const int Unk154              = 0x154;  // float; purpose unknown
        internal const int Unk158              = 0x158;  // float; purpose unknown
    }

    /// <summary>One entry in <see cref="BaitNoticeRadiusTable"/>. Fields are EE RAM addresses.</summary>
    internal readonly struct BaitTableEntry
    {
        /// <summary>EE RAM address of the float notice-radius field.</summary>
        internal readonly int   Radius;
        /// <summary>EE RAM address of the uint32 item-ID field (always Radius + 4).</summary>
        internal readonly int   Id;
        /// <summary>Original game value. Stored here because writes to EE RAM persist and the
        /// table is not restored on re-entry, so we can't read back the default.</summary>
        internal readonly float DefaultRadius;
        internal BaitTableEntry(int radiusAddr, float defaultRadius) { Radius = radiusAddr; Id = radiusAddr + 4; DefaultRadius = defaultRadius; }
    }

    /// <summary>
    /// Bait notice-radius table in EE RAM. Each entry is (float radius, uint32 itemId) at stride 8,
    /// in the same order as the bait affinity fields in <see cref="FishSlotOffsets"/>.
    /// The game copies entry.Radius into each fish slot's <see cref="FishSlotOffsets.NoticeRadius"/>
    /// every frame, keyed by the equipped bait's item ID.
    /// Confirmed via ScanFor25f cluster dump (2026-06-03). Bait validity is enforced elsewhere —
    /// writing a non-bait item ID into an entry's Id address has no effect on the bait screen.
    /// </summary>
    internal static class BaitNoticeRadiusTable
    {
        internal const int TableBase = 0x2026AE8C;
        internal const int Stride    = 8;

        internal static readonly BaitTableEntry Evy             = new BaitTableEntry(0x2026AE8C, 128.0f); // id=193
        internal static readonly BaitTableEntry Mimi            = new BaitTableEntry(0x2026AE94,  50.0f); // id=197
        internal static readonly BaitTableEntry Prickly         = new BaitTableEntry(0x2026AE9C,  25.0f); // id=199
        internal static readonly BaitTableEntry ThrobbingCherry = new BaitTableEntry(0x2026AEA4,  25.0f); // id=166
        internal static readonly BaitTableEntry Gooeypeach      = new BaitTableEntry(0x2026AEAC,  25.0f); // id=167
        internal static readonly BaitTableEntry Bombnuts        = new BaitTableEntry(0x2026AEB4,  25.0f); // id=168
        internal static readonly BaitTableEntry PoisonousApple  = new BaitTableEntry(0x2026AEBC,  25.0f); // id=169
        internal static readonly BaitTableEntry MellowBanana    = new BaitTableEntry(0x2026AEC4,  25.0f); // id=170
        internal static readonly BaitTableEntry Carrot          = new BaitTableEntry(0x2026AECC,  25.0f); // id=186
        internal static readonly BaitTableEntry PotatoCake      = new BaitTableEntry(0x2026AED4,  25.0f); // id=187
        internal static readonly BaitTableEntry Minon           = new BaitTableEntry(0x2026AEDC,  25.0f); // id=188
        internal static readonly BaitTableEntry Battan          = new BaitTableEntry(0x2026AEE4,  25.0f); // id=189
        internal static readonly BaitTableEntry Petitefish      = new BaitTableEntry(0x2026AEEC,  25.0f); // id=190
        internal static readonly BaitTableEntry Unknown14       = new BaitTableEntry(0x2026AEF4,  40.0f); // id=0 — purpose unknown
    }

    /// <summary>
    /// Per-species static data table in EE RAM. 18 entries (IDs 0–17) at stride 72 (0x48) bytes.
    /// Unlike a fish slot, entries have no leading fish-ID field — the array index is the species ID.
    /// Field offsets within each entry are therefore 4 less than their counterparts in <see cref="FishSlotOffsets"/>.
    /// Base address and stride confirmed 2026-06-03 via forced slot-write experiment; ID 0 (Bobo) verified
    /// (ScaleDivisor=17.0, BaseSize=10.0, MaxSize=20.0, BaseFp=20, MaxFp=50).
    /// </summary>
    internal static class FishSpeciesTable
    {
        internal const int Base   = 0x20296050;
        internal const int Stride = 72;   // 0x48 bytes per entry
        internal const int Count  = 18;

        // Field offsets within each entry. Names match FishData / FishSlotOffsets for the same field;
        // values are the species-table offsets (slot offset − 4).
        internal const int ScaleDivisor           = 0x00;  // float; visual scale divisor — ScaleModel = Size / ScaleDivisor (ELF 0x00240D60)
        internal const int BaseSize              = 0x04;  // float; base/center size for RNG distribution and FP threshold (ELF 0x00240D60, 0x00240E80)
        internal const int MaxSize             = 0x08;  // float; ×10 = display cm
        internal const int BaseFp               = 0x0C;  // int; base FP value — modifier role unconfirmed
        internal const int MaxFp               = 0x10;  // int; base FP value — modifier role unconfirmed
        // Bait affinity floats — same order as BaitNoticeRadiusTable and FishSlotOffsets.BaitAff*
        // 0.0 = never bites; 1.0 = normal; values above 1.0 may be clamped by game code
        internal const int BaitAffEvy             = 0x14;
        internal const int BaitAffMimi            = 0x18;
        internal const int BaitAffPrickly         = 0x1C;
        internal const int BaitAffThrobbingCherry = 0x20;
        internal const int BaitAffGooeypeach      = 0x24;
        internal const int BaitAffBombnuts        = 0x28;
        internal const int BaitAffPoisonousApple  = 0x2C;
        internal const int BaitAffMellowBanana    = 0x30;
        internal const int BaitAffCarrot          = 0x34;
        internal const int BaitAffPotatoCake      = 0x38;
        internal const int BaitAffMinon           = 0x3C;
        internal const int BaitAffBattan          = 0x40;
        internal const int BaitAffPetitefish      = 0x44;

        /// <summary>Returns the EE RAM base address of the entry for <paramref name="fishId"/>,
        /// or -1 if out of range.</summary>
        internal static int AddrForId(int fishId) =>
            (uint)fishId < Count ? Base + fishId * Stride : -1;
    }

    /// <summary>
    /// Fish model filename pointer array in EE RAM. Discovered 2026-06-03 via ScanForFishTable.
    /// 18 uint32 entries at stride 4 (one per fish ID 0–17). Each raw pointer P gives the EE
    /// string address as (0x20000000 + P). The string is 16 bytes, null-padded ASCII,
    /// formatted "chara/f{id+1:D2}a.chr" (ID 0 → "chara/f01a.chr", ID 17 → "chara/f18a.chr").
    /// Fish ID 8 has no FishDatabase entry but does have a model file ("chara/f09a.chr"),
    /// suggesting a cut or unused species.
    /// </summary>
    internal static class FishModelTable
    {
        /// <summary>EE RAM address of the first pointer entry (fish ID 0).</summary>
        internal const int PointerArrayBase = 0x20296570;
        internal const int Stride           = 4;
        internal const int Count            = 18;
        /// <summary>Value subtracted from each raw uint32 pointer to get the EE string address.</summary>
        internal const long PtrBias         = 0x20000000L;

        /// <summary>Returns the EE RAM address of the pointer for <paramref name="fishId"/>,
        /// or -1 if out of range.</summary>
        internal static int PointerAddrForId(int fishId) =>
            (uint)fishId < Count ? PointerArrayBase + fishId * Stride : -1;
    }

    /// <summary>
    /// Shared shadow/silhouette model used by all fish species. Not in the FishModelTable
    /// pointer array (which covers only f01a–f18a).
    ///
    /// ELF slot init (0x002411E0) hardcodes the string address via LUI+ADDIU $a0, 0x0029FC98
    /// and calls ChrLoader unconditionally — no species branch. f00s is globally loaded once
    /// for the whole fishing scene, not per-slot. The shadow geometry is baked into f00s.mds;
    /// there is no per-species shadow configuration.
    ///
    /// The same loader also loads info.cfg (EE RAM 0x2029FCA8) immediately after f00s.chr
    /// as a sub-object at slot+0xA0.
    /// </summary>
    internal static class FishShadowModel
    {
        internal const string Filename        = "chara/f00s.chr";
        /// <summary>EE RAM address of the "chara/f00s.chr" string (hardcoded in ELF slot init).</summary>
        internal const int    EeRamStringAddr = 0x2029FC98;
        /// <summary>Byte offset of f00s.chr inside DATA.DAT (Dark Cloud USA disc).</summary>
        internal const int    DataDatOffset   = 0x0031D800;
        internal const int    DataDatSize     = 109_296;
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
            ScaleDivisor = 17.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 20.0f,
            BaseFp = 20, MaxFp = 50,
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
            ScaleDivisor = 19.5f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 10, MaxFp = 30,
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
            ScaleDivisor = 25.5f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 19.0f,
            BaseFp = 8, MaxFp = 25,
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
            ScaleDivisor = 35.4f, BaseSize = 12.0f,
            MinSize = 6.0f, MaxSize = 24.0f,
            BaseFp = 30, MaxFp = 60,
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
            ScaleDivisor = 28.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 10, MaxFp = 40,
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
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 16.0f,
            BaseFp = 200, MaxFp = 400,
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
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 15, MaxFp = 40,
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
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 10.0f,
            BaseFp = 20, MaxFp = 50,
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
            BaitAffGooeypeach      = 0.0f,
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
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 40, MaxFp = 80,
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
            ScaleDivisor = 20.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 20, MaxFp = 40,
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
            ScaleDivisor = 15.0f, BaseSize = 6.0f,
            MinSize = 3.0f, MaxSize = 12.0f,
            BaseFp = 10, MaxFp = 30,
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
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 20.0f,
            BaseFp = 35, MaxFp = 70,
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
            ScaleDivisor = 21.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 16.0f,
            BaseFp = 15, MaxFp = 40,
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
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 20.0f,
            BaseFp = 20, MaxFp = 40,
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
            ScaleDivisor = 20.0f, BaseSize = 8.0f,
            MinSize = 4.0f, MaxSize = 14.0f,
            BaseFp = 20, MaxFp = 40,
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
            ScaleDivisor = 21.0f, BaseSize = 10.0f,
            MinSize = 5.0f, MaxSize = 30.0f,
            BaseFp = 600, MaxFp = 1000,
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
                Bobo, Gobbler, Nonky, Kaiji, BakuBaku, Gummy, Niler, MissingFish,
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
            SlotBase = Addresses.fishSlotBase_Norune, SlotCount = 4, QuestBase = 0x21CE4416, GiverName = "Pike",
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
            SlotBase = Addresses.fishSlotBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
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
            SlotBase = Addresses.fishSlotBase_Matataki, SlotCount = 5, QuestBase = 0x21CE441E, GiverName = "Pao",
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
            SlotBase = Addresses.fishSlotBase_Queens, SlotCount = 5, QuestBase = 0x21CE4427, GiverName = "Sam",
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
            SlotBase = Addresses.fishSlotBase_MuskaLacka, SlotCount = 4, QuestBase = 0x21CE4431, GiverName = "Devia",
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
            byte last = 0;
            foreach (byte id in weights.Keys) { if (i++ == pick) return id; last = id; }
            return last;
        }

        // VA 0x001A8BD4: random % 3; outcomes 0/1/2 each map to one fish.
        private static byte SpawnThreeWayEqualMod3(Dictionary<byte, float> weights)
        {
            int pick = Fishing.Rng.Next(3);
            int i = 0;
            byte last = 0;
            foreach (byte id in weights.Keys) { if (i++ == pick) return id; last = id; }
            return last;
        }

        // VA 0x001A8B00+: random % 100 checked against sequential cumulative thresholds.
        private static byte SpawnRandomMod100(Dictionary<byte, float> weights)
        {
            int roll = Fishing.Rng.Next(100);
            float cumulative = 0f;
            byte last = 0;
            foreach (var kvp in weights)
            {
                cumulative += kvp.Value * 100f;
                if (roll < cumulative) return kvp.Key;
                last = kvp.Key;
            }
            return last;
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
