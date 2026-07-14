namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// EE RAM addresses for the fishing-related state machines.
    /// </summary>
    internal static class FishingAddresses
    {
        internal const int Active            = 0x21D19714; // 0 = not fishing, 1 = session active
        /// <summary>
        /// Index of the fishing trigger NPC/object that activated the current fishing session,
        /// within the area's object table. Set when townMode transitions to 16 (fishing).
        /// Used to distinguish sub-spots that share the same area ID.
        /// Confirmed values: Norune=4, PeanutPond=11, MatatakiWaterfall=13, Queens=-1 (unset), Muska Lacka=5.
        /// </summary>
        internal const int TriggerIndex      = 0x202A1F64;
        internal const int FishCatchConfirm  = 0x202A26E8; // byte; checked after slot clears (0xFF) to confirm fish was actually landed
        internal const int OverworldState    = 0x21D19708; // overworld / dialog state machine
        internal const int WalkSpeed         = 0x21D33E20; // Toan walking speed while fishing
        internal const int CastAnimGate      = 0x21D33E24; // 2 during cast/uncast animations; may gate player inputs
        internal const int Phase             = 0x21D33E28; // fishing phase state machine
        internal const int AcquiredFlagsBase = 0x21CE4439; // 18 bytes, one per fish species ID (0–17)
    }

    /// <summary>
    /// State values for the fishing-related state machines.
    /// </summary>
    internal static class FishingState
    {
        // FishingAddresses.FishCatchConfirm values — read by game after rod pull to confirm catch and trigger success dialog; not a general-purpose state machine
        internal const int FishCatchConfirm_Active = 12;

        // FishingAddresses.OverworldState values
        internal const int OverworldState_Fishing     = unchecked((int)0xFFFFFFFF); // default while fishing (cast out, no dialog)
        internal const int OverworldState_QuitDialog  = 0x00000085; // "Continue fishing" / "Quit fishing" dialog
        internal const int OverworldState_BaitScreen  = 0x00000086; // bait selection screen open
        internal const int OverworldState_Overworld   = 0x0000000C; // back in overworld

        // FishingAddresses.Phase values
        internal const int Phase_Idle          = 0x00000000; // default/idle phase; bait screen is FishingAddresses.OverworldState = OverworldState_BaitScreen
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
    /// One entry in <see cref="BaitDetectionRadiusTable"/>. Fields are EE RAM addresses.
    /// </summary>
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
    internal static class BaitDetectionRadiusTable
    {
        internal const int TableBase = 0x2026AE8C;
        internal const int Stride    = 8;

        internal static readonly BaitTableEntry Evy             = new BaitTableEntry(0x2026AE8C, 128.0f); // id=193
        internal static readonly BaitTableEntry Mimi            = new BaitTableEntry(0x2026AE94,  50.0f); // id=197
        internal static readonly BaitTableEntry Prickly         = new BaitTableEntry(0x2026AE9C,  25.0f); // id=199
        internal static readonly BaitTableEntry ThrobbingCherry = new BaitTableEntry(0x2026AEA4,  25.0f); // id=166
        internal static readonly BaitTableEntry GooeyPeach      = new BaitTableEntry(0x2026AEAC,  25.0f); // id=167
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
    /// Fish model filename pointer array in EE RAM. Discovered 2026-06-03 via ScanForFishTable.
    /// 18 uint32 entries at stride 4 (one per fish ID 0–17). Each raw pointer P gives the EE
    /// string address as (0x20000000 + P). The string is 16 bytes, null-padded ASCII,
    /// formatted "chara/f{id+1:D2}a.chr" (ID 0 → "chara/f01a.chr", ID 17 → "chara/f18a.chr").
    /// Fish ID 8 has no Fish entry but does have a model file ("chara/f09a.chr"),
    /// suggesting a cut or unused species.
    /// </summary>
    internal static class FishModelTable
    {
        /// <summary>EE RAM address of the first pointer entry (fish ID 0).</summary>
        internal const int PointerArrayBase = 0x20296570;
        internal const int Stride           = 4;
        internal const int Count            = 18;
        /// <summary>Value subtracted from each raw uint32 pointer to get the EE string address (= <see cref="Memory.Pcsx2Base"/>).</summary>
        internal static long PtrBias => Memory.Pcsx2Base;

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
        // Bait affinity floats — same order as BaitDetectionRadiusTable and FishSlotOffsets.BaitAff*
        // 0.0 = never bites; 1.0 = normal; values above 1.0 may be clamped by game code
        internal const int BaitAffEvy             = 0x14;
        internal const int BaitAffMimi            = 0x18;
        internal const int BaitAffPrickly         = 0x1C;
        internal const int BaitAffThrobbingCherry = 0x20;
        internal const int BaitAffGooeyPeach      = 0x24;
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
    /// Field offsets within a single fish slot, relative to the slot's base address.
    /// Per-area base addresses and slot stride are also in this class (AreaBase_*, Stride).
    /// All fields confirmed via slot dump analysis unless noted otherwise.
    /// </summary>
    internal static class FishSlotOffsets
    {
        internal const int Stride = 0x2410;

        // Per-area base addresses of the first fish slot
        internal const int AreaBase_Norune     = 0x214798D0; // Area 0 — Norune Village, 4 slots
        internal const int AreaBase_Matataki   = 0x214D9910; // Area 1 — Matataki Waterfall + Peanut Pond, 5 slots (shared)
        internal const int AreaBase_MuskaLacka = 0x213C3150; // Area 3 — 4 slots
        internal const int AreaBase_Queens     = 0x20DE0710; // Area 19 — 5 slots

        // ---- Species identity + static data ----
        // Written by the game from FishSpeciesTable each time a fish spawns.
        // Slot writes are effective for the current session; modify FishSpeciesTable to persist across spawns.
        internal const int SpeciesId           = 0x000;  // byte; fish species ID (indexes Fish)
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
        // Bait affinity floats — 13 entries matching BaitDetectionRadiusTable order
        // 0.0 = never bites; 1.0 = normal; values above 1.0 may be clamped by game code
        internal const int BaitAffEvy             = 0x018;
        internal const int BaitAffMimi            = 0x01C;
        internal const int BaitAffPrickly         = 0x020;
        internal const int BaitAffThrobbingCherry = 0x024;
        internal const int BaitAffGooeyPeach      = 0x028;
        internal const int BaitAffBombnuts        = 0x02C;
        internal const int BaitAffPoisonousApple  = 0x030;
        internal const int BaitAffMellowBanana    = 0x034;
        internal const int BaitAffCarrot          = 0x038;
        internal const int BaitAffPotatoCake      = 0x03C;
        internal const int BaitAffMinon           = 0x040;
        internal const int BaitAffBattan          = 0x044;
        internal const int BaitAffPetitefish      = 0x048;

        // ---- AI behavior state ----
        internal const int AiState             = 0x050;  // int; current behavior (see FishSlotState.AiState_*)
        internal const int Unk054              = 0x054;  // unknown
        internal const int AiStateTimer        = 0x058;  // uint; read-only — countdown managed by game state machine; underflows to 0xFFFFFFFF to trigger transition

        // ---- Size and render scale ----
        internal const int Size                = 0x060;  // float; ×10 = display cm
        internal const int ScaleModel          = 0x064;  // float; Size / ScaleDivisor (ELF 0x00240D60)
        internal const int ScaleFixed          = 0x068;  // float; Size / 25.0 (ELF 0x00240D60; constant divisor)

        // ---- Movement ----
        internal const int Heading             = 0x074;  // float; current facing angle (radians)
        internal const int Speed               = 0x080;  // float; current movement speed
        internal const int Velocity            = 0x084;  // float; ramps up as fish accelerates
        internal const int Unk088              = 0x088;  // int; small value (3–10); consistently 4 during Approaching/Nibbling — purpose unknown
        internal const int NoticeRadius        = 0x08C;  // float; read-only — overwritten every frame from BaitDetectionRadiusTable; 3D radius within which fish transitions Dormant→Approaching

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

    /// <summary>
    /// State values for <see cref="FishSlotOffsets.AiState"/>. Confirmed via phase logger datamining.
    /// </summary>
    internal static class FishSlotState
    {
        internal const int AiState_Dormant     = unchecked((int)0xFFFFFFFF); // not participating this cast; fish far from hook
        internal const int AiState_Approaching = 6;  // swimming toward the hook at slow speed (~0.1)
        internal const int AiState_Nibbling    = 7;  // arrived at hook, stationary, nibbling bait
        internal const int AiState_Unk08       = 8;  // observed briefly during approach before going dormant; purpose unknown
        internal const int AiState_Patrolling  = 9;  // fast patrol movement (>0.3 spd), farther from hook
        internal const int AiState_Hooked      = 11; // fish is on the hook, being reeled in
    }

    /// <summary>
    /// The native fishing-records ("fish ranking") list inside the <c>CSaveData</c> object.
    /// RE'd from <c>SetFishingRank__9CSaveDataFif</c> (ELF 0x157DC0) and
    /// <c>GetFishingRank__9CSaveDataFi</c> (ELF 0x157F40): 64 entries of 16 bytes at
    /// SaveData+0x1E0, kept sorted by size descending. The native insert fills the first empty
    /// slot (size &lt;= 0) or evicts the smallest entry when full, then re-sorts. The records
    /// screen (<c>FishRecordView*</c>) reads entries back via GetFishingRank, which treats
    /// <c>size &lt;= 0 || fishId &lt; 0</c> as empty — so rewriting this list changes both the
    /// display and what persists in the save file.
    /// </summary>
    internal static class FishingRankList
    {
        /// <summary>ELF global <c>CSaveData* SaveData</c> (native 0x2A250C). Holds a PS2-native
        /// pointer; convert with <c>Memory.ToMmu()</c> before dereferencing. 0 before a save
        /// is loaded.</summary>
        internal const int SaveDataPtr = 0x202A250C;
        internal const int Offset      = 0x1E0; // rank array within CSaveData
        internal const int Stride      = 0x10;  // 16 bytes per entry
        internal const int Count       = 64;    // native capacity (0x40 entries)
        internal const int EntryFishId = 0x0;   // int; -1 = empty
        internal const int EntrySize   = 0x4;   // float; <= 0 = empty. Display cm = floor(size * 10)
    }

    /// <summary>
    /// A fishing SPOT — where you may cast, how high the water is, and what collision the hook and
    /// the fish live against. All of it is set by the single STB command <c>_LOAD_FISHING_DATA</c>
    /// (cmd 999, handler 0x1969A0) and then left in these globals, which means the mod can move or
    /// resize a spot with plain writes. See docs/custom-fishing-spot.md.
    ///
    /// The three ingredients are INDEPENDENT: the rectangle and the water height are these globals;
    /// the collision comes from <c>CEditGround::PickUpPoly</c>, which is not water-aware and happily
    /// gathers static terrain; and the visible liquid is whatever the map already draws. So a spot
    /// does not require a Georama water part — only a rectangle over something with ground under it.
    /// </summary>
    internal static class FishingSpot
    {
        /// <summary>ELF <c>fishing_rect</c> — the castable rectangle, a 32-byte CBoxVu0.
        /// The handler hardcodes the box's Y extents to +/-1000, so it is effectively 2D in XZ.</summary>
        internal const long FishingRect = 0x21D549D0;

        /// <summary>ELF <c>fish_rect</c> — the box the fish are kept inside (copied from the above by
        /// <c>FishingInitFish</c>). Fish are seeded at its centre, <c>WaterLevel - 12</c> deep.</summary>
        internal const long FishRect = 0x21D549F0;

        // CBoxVu0: two corners, each (x, y, z, w).
        internal const int BoxCornerA = 0x00;   // x @ +0x00, y @ +0x04 (= +1000), z @ +0x08
        internal const int BoxCornerB = 0x10;   // x @ +0x10, y @ +0x14 (= -1000), z @ +0x18
        internal const int BoxSize    = 0x20;

        internal const long WaterLevel     = 0x202A2B28; // float — the surface
        internal const long GroundLevel    = 0x202A2B2C; // float — the bed
        internal const long UkiGroundLevel = 0x202A2B30; // float — where the bobber rests
        internal const long HookGroundLevel = 0x202A2B34;
        internal const long LineGroundLevel = 0x202A2B38;

        internal const long CPoly     = 0x202A2B68; // CCPoly* — the collision PickUpPoly gathered
        internal const long CPolyNum  = 0x202A2B6C; // int. PickUpPoly HANGS the game above 0x400.
        internal const int  CPolyMax  = 0x400;

        internal const long Fish       = 0x202A2B58; // CFish* — array base, stride 0x2410
        internal const long FishNum    = 0x202A2B64; // int — live fish (4 or 5; six are allocated)
        internal const long AngleFish  = 0x202A2B5C;
        internal const long BattleFish = 0x202A2B60;

        /// <summary>int. 0 disables the underwater view. Area 3 (Queens Harbor) ships with it off —
        /// worth copying for any spot whose "water" is not water (e.g. Yellow Drops' liquid).</summary>
        internal const long DrawUnderWater = 0x202A1FA0;
    }
}
