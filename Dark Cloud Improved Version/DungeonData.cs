using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    // Enemy spawn pool data extracted from ELF /tmp/SCUS_971.11, file offset 0x1861D0.
    //
    // BINARY FORMAT (spawn pool table)
    // ---------------------------------
    // The table is a packed stream of 28-word (112-byte) fixed-stride pool records.
    // Exception: the very first record in the entire table has no [0, 1] header and is
    // 26 words; all subsequent records are 28 words.
    //
    // Standard record layout (28 words, little-endian 32-bit each):
    //   word 0 : 0
    //   word 1 : 1
    //   word 2 : tier (0 = unset / back-floor default)
    //   words 3+ : (TableIndex, probability%, 1) triplets, one per entry
    //   0xFFFF  : sentinel ending entry list
    //   0,1,0xFFFF : padding triplets filling remainder to 28 words
    //
    // TableIndex is the physical 0-based row index in the enemy species table
    // (ELF offset 0x17FC54, stride 0x9C).  It is NOT the same as EnemyDefaults.Id.
    //
    // RAM ADDRESSES
    // -------------
    // ELF file offset 0x1861D0 maps to RAM via: RAM = 0x00100000 + (fileOffset − 0x100)
    //   Pool table base: 0x002860D0
    //   Slot stride:     112 bytes  (0x70)
    //   SlotAddress(n) = 0x002860D0 + n × 0x70
    //
    //   Array              Slots      RAM range
    //   DBC_Front          0 –  14   0x002860D0 – 0x002866F0
    //   DBC_Back          15 –  28   0x00286760 – 0x00286D10
    //   WO_Front          29 –  46   0x00286D80 – 0x002874F0
    //   WO_Back           47 –  62   0x00287560 – 0x00287BF0
    //   SW_Front          63 –  81   0x00287C60 – 0x00288440
    //   SW_Back           82 –  98   0x002884B0 – 0x00288BB0
    //   SMT_Front         99 – 117   0x00288C20 – 0x00289400
    //   SMT_Back         118 – 134   0x00289470 – 0x00289B70
    //   MS_Front         135 – 150   0x00289BE0 – 0x0028A270
    //   MS_Back          151 – 164   0x0028A2E0 – 0x0028A890
    //   GoT_Front        165 – 189   0x0028A900 – 0x0028B380
    //   DHC_DarkGenie        190      0x0028B3F0            ← anomaly: stored before GoT_Back
    //   [GoT boss back]      191      0x0028B460            ← empty companion slot
    //   GoT_Back         192 – 215   0x0028B4D0 – 0x0028BEE0
    //   DHC_Front        216 – 316   0x0028BF50 – 0x0028EB10
    //   DHC_Back         317 – 415   0x0028EB80 – 0x00291660
    //
    // DUNGEON STRUCTURE (slot indices, stride 28 words)
    // --------------------------------------------------
    // Each dungeon occupies a contiguous block:
    //   [descriptor slot][front floor slots 1..N][back floor slots for floors 1..N-1]
    // The boss floor (last front slot) has no back floor.
    // DBC is unique: its descriptor (slot 0) is non-empty and has an associated back-floor slot.
    //
    // FLOOR COUNT CORRECTIONS vs. Dungeon.cs GetDungeonEventFloors()
    // ----------------------------------------------------------------
    //   WO  : code says 16  -> actual boss slot = floor 17  (off by 1)
    //   SW  : code says 17  -> actual boss slot = floor 18  (off by 1)
    //   SMT : code says 17  -> actual boss slot = floor 18  (off by 1)
    //   MS  : code says 14  -> actual boss slot = floor 15  (off by 1)
    //   DHC : code says 99  -> actual boss slot = floor 100 (off by 1)
    //
    // TABLEINDEX RESOLUTION (EnemySpeciesTable scan 2026-06-05)
    // ----------------------------------------------------------
    // Valid table range confirmed: tbl_0–165.  tbl_166+ is garbage (hp=0, empty code).
    // All previously unknown indices are now named in EnemyDatabase:
    //   SW boss companions     : tbl_92  → SWComp92; tbl_101–104 → IQComp101–104
    //   DarkGenie companions   : tbl_88–90 → DGComp88–90; tbl_93 → DGComp93
    //   DHC GemronFire group   : tbl_113–120 → WhiteFangEnhanced–DiamondEnhanced
    //   DHC GemronIce group    : tbl_123–129 → AuntieMeduEnhanced–CorceaEnhanced
    //   DHC GemronThunder group: tbl_134–142 → CaveBatEnhanced–KingMimicDSEnhanced
    //   DHC GemronWind group   : tbl_145–153 → AlexanderEnhanced–KingMimicDSEnhancedTwice
    //   DHC GemronHoly group   : tbl_155–165 → GaciousEnhanced–DHC165 (tbl_165 = BlackKnight)
    //   DHC floor 100 padding  : tbl_166     → DHC166 (garbage row, present in binary)
    //
    // WHY PATCHING 0x002860D0 DOES NOT AFFECT IN-GAME SPAWNS
    // --------------------------------------------------------
    // 0x002860D0 is a dual-use global (GP-relative: lw/sw $rX, -0x6358($gp), GP=0x28C428):
    //   1. At ELF load time it holds the static pool table (readable and patchable).
    //   2. During dungeon init the game reads the static pool data, copies it to a heap
    //      object at 0x01DC48E0 (via fns 0x124670/0x1246A0), then writes the heap
    //      pointer back into [0x002860D0], clobbering the static table base.
    //      Area-transition functions: 0x1BB310, 0x1BB5E0, 0x1D1810, 0x1D2460, 0x1D2C70, 0x1D36A0
    //   3. All subsequent spawn reads go through the heap object — not the static table.
    //   4. The game pre-computes floor enemy placements at dungeon-zone init time
    //      (before PINE connects), so patching the static table has no observable effect.
    //      Use ScanEnemySpeciesTable() (formerly in SpawnPoolVerifier) to identify
    //      TableIndex values without needing to spawn them.
    //   5. To modify live spawns: patch the heap object at 0x01DC48E0 after dungeon init
    //      completes but before the player enters a floor.
    //   Live spawn selection buffer (hardcoded): 0x01EB60D0 — confirmed to be a metadata
    //   header (6 non-zero words), not spawn-pool data.

    internal struct SpawnEntry
    {
        // Physical row index in the enemy species table (ELF 0x17FC54, stride 0x9C).
        internal int TableIndex;
        // Spawn weight / probability; values in a pool sum to 100.
        internal int Probability;
        internal SpawnEntry(int idx, int prob) { TableIndex = idx; Probability = prob; }
    }

    internal struct FloorSpawnPool
    {
        internal int Tier;
        internal SpawnEntry[] Entries;
        internal FloorSpawnPool(int tier, SpawnEntry[] entries) { Tier = tier; Entries = entries; }
    }

    // Helper for terse entry construction
    internal static class SP
    {
        internal static SpawnEntry E(int? idx, int prob) => new SpawnEntry(idx!.Value, prob);
        internal static FloorSpawnPool Pool(int tier, params SpawnEntry[] entries) =>
            new FloorSpawnPool(tier, entries);
        internal static FloorSpawnPool Empty(int tier) =>
            new FloorSpawnPool(tier, new SpawnEntry[0]);
    }

    /// <summary>
    /// Binary-extracted spawn pools for all 7 dungeons.
    /// Front[0] is the dungeon descriptor pool (not a numbered floor; used by the engine
    /// before floor 1 loads).  Front[N] is floor N's enemy pool.  Back[N] is the back-side
    /// pool for floor N (floors that have no back — boss and GoT — have no back entry).
    /// </summary>
    internal static class SpawnPoolData
    {
        // ── Dungeon 0 : Divine Beast Cave (DBC) ────────────────────────────────────────
        // 14 floors.  Boss: Dran (floor 14).  Event floors: 3, 7, 14.
        // Descriptor pool (slot 0) is non-empty — the only dungeon where this is the case.
        // Front slots  0– 14  RAM 0x002860D0–0x002866F0
        // Back  slots 15– 28  RAM 0x00286760–0x00286D10

        internal static readonly FloorSpawnPool[] DBC_Front = new FloorSpawnPool[]
        {
            // [0] descriptor
            SP.Pool(3, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,50), SP.E(EnemyDatabase.Dasher.TableIndex,30)),
            // [1] floor 1
            SP.Pool(3, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,20), SP.E(EnemyDatabase.Dasher.TableIndex,40), SP.E(EnemyDatabase.Yammich.TableIndex,20)),
            // [2] floor 2
            SP.Pool(3, SP.E(EnemyDatabase.CaveBat.TableIndex,25), SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,25), SP.E(EnemyDatabase.Dasher.TableIndex,10), SP.E(EnemyDatabase.Statue.TableIndex,20), SP.E(EnemyDatabase.StatueDog.TableIndex,20)),
            // [3] floor 3 — EVENT: skeleton encounter (SkeletonSoldier:100%)
            SP.Pool(3, SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,100)),
            // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Dasher.TableIndex,20), SP.E(EnemyDatabase.Statue.TableIndex,20), SP.E(EnemyDatabase.StatueDog.TableIndex,20), SP.E(EnemyDatabase.Yammich.TableIndex,20)),
            // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.CaveBat.TableIndex,25), SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,25), SP.E(EnemyDatabase.Statue.TableIndex,25), SP.E(EnemyDatabase.MimicDBC.TableIndex,25)),
            // [6] floor 6
            SP.Pool(5, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,20), SP.E(EnemyDatabase.Ghost.TableIndex,20), SP.E(EnemyDatabase.Opar.TableIndex,20), SP.E(EnemyDatabase.MasterJacket.TableIndex,20)),
            // [7] floor 7 — EVENT: story (no enemy spawns)
            SP.Empty(5),
            // [8] floor 8
            SP.Pool(5, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Ghost.TableIndex,20), SP.E(EnemyDatabase.Statue.TableIndex,20), SP.E(EnemyDatabase.StatueDog.TableIndex,20), SP.E(EnemyDatabase.MimicDBC.TableIndex,20)),
            // [9] floor 9
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,25), SP.E(EnemyDatabase.Dasher.TableIndex,10), SP.E(EnemyDatabase.Ghost.TableIndex,10), SP.E(EnemyDatabase.MasterJacket.TableIndex,30), SP.E(EnemyDatabase.Rockanoff.TableIndex,25)),
            // [10] floor 10
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Dasher.TableIndex,25), SP.E(EnemyDatabase.Opar.TableIndex,20), SP.E(EnemyDatabase.MasterJacket.TableIndex,20), SP.E(EnemyDatabase.Dragon.TableIndex,15)),
            // [11] floor 11
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Statue.TableIndex,20), SP.E(EnemyDatabase.MasterJacket.TableIndex,20), SP.E(EnemyDatabase.KingMimicDBC.TableIndex,20), SP.E(EnemyDatabase.Rockanoff.TableIndex,20)),
            // [12] floor 12
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Opar.TableIndex,20), SP.E(EnemyDatabase.MasterJacket.TableIndex,20), SP.E(EnemyDatabase.Rockanoff.TableIndex,20), SP.E(EnemyDatabase.Dragon.TableIndex,20)),
            // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,20), SP.E(EnemyDatabase.Ghost.TableIndex,20), SP.E(EnemyDatabase.KingMimicDBC.TableIndex,20), SP.E(EnemyDatabase.Rockanoff.TableIndex,20), SP.E(EnemyDatabase.Dragon.TableIndex,20)),
            // [14] floor 14 — BOSS: Dran
            SP.Pool(7, SP.E(EnemyDatabase.Dran.TableIndex,100)),
        };

        // Back-floor pools for DBC slots 0-13 (index N here = back for Front[N]).
        internal static readonly FloorSpawnPool[] DBC_Back = new FloorSpawnPool[]
        {
            SP.Pool(3, SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,60), SP.E(EnemyDatabase.Ghost.TableIndex,20), SP.E(EnemyDatabase.Yammich.TableIndex,20)),        // back for descriptor
            SP.Pool(3, SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,80), SP.E(EnemyDatabase.Ghost.TableIndex,20)),                      // back for floor 1
            SP.Pool(3, SP.E(EnemyDatabase.SkeletonSoldier.TableIndex,80), SP.E(EnemyDatabase.Ghost.TableIndex,20)),                      // back for floor 2
            SP.Empty(3),                                               // back for floor 3 (event)
            SP.Pool(4, SP.E(EnemyDatabase.MimicDBC.TableIndex,50), SP.E(EnemyDatabase.KingMimicDBC.TableIndex,50)),                     // back for floor 4
            SP.Pool(4, SP.E(EnemyDatabase.Statue.TableIndex,50), SP.E(EnemyDatabase.StatueDog.TableIndex,50)),                      // back for floor 5
            SP.Pool(5, SP.E(EnemyDatabase.Dasher.TableIndex,50), SP.E(EnemyDatabase.Rockanoff.TableIndex,50)),                      // back for floor 6
            SP.Empty(5),                                               // back for floor 7 (event)
            SP.Pool(5, SP.E(EnemyDatabase.CaveBat.TableIndex,100)),                                  // back for floor 8
            SP.Pool(7, SP.E(EnemyDatabase.MimicDBC.TableIndex,30), SP.E(EnemyDatabase.KingMimicDBC.TableIndex,30), SP.E(EnemyDatabase.Rockanoff.TableIndex,40)),        // back for floor 9
            SP.Pool(7, SP.E(EnemyDatabase.Ghost.TableIndex,25), SP.E(EnemyDatabase.MimicDBC.TableIndex,50), SP.E(EnemyDatabase.MasterJacket.TableIndex,25)),         // back for floor 10
            SP.Pool(7, SP.E(EnemyDatabase.CaveBat.TableIndex,50), SP.E(EnemyDatabase.Dragon.TableIndex,50)),                     // back for floor 11
            SP.Pool(7, SP.E(EnemyDatabase.MimicDBC.TableIndex,50), SP.E(EnemyDatabase.KingMimicDBC.TableIndex,50)),                     // back for floor 12
            SP.Pool(7, SP.E(EnemyDatabase.Rockanoff.TableIndex,100)),                                  // back for floor 13
        };

        // ── Dungeon 1 : Wise Owl Forest (WO) ─────────────────────────────────────────────
        // 17 floors.  Boss: MasterUtan (floor 17).  Event floors: 8, 17.
        // NOTE: GetDungeonEventFloors currently returns 16 for the boss floor — should be 17.
        // Front slots 29– 46  RAM 0x00286D80–0x002874F0
        // Back  slots 47– 62  RAM 0x00287560–0x00287BF0

        internal static readonly FloorSpawnPool[] WO_Front = new FloorSpawnPool[]
        {
            // [0] descriptor (empty)
            SP.Empty(0),
            // [1] floor 1
            SP.Pool(4, SP.E(EnemyDatabase.CannibalPlant.TableIndex,20), SP.E(EnemyDatabase.Saturday.TableIndex,20), SP.E(EnemyDatabase.Friday.TableIndex,20), SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.Wednesday.TableIndex,20)),
            // [2] floor 2
            SP.Pool(4, SP.E(EnemyDatabase.CannibalPlant.TableIndex,25), SP.E(EnemyDatabase.Tuesday.TableIndex,25), SP.E(EnemyDatabase.Monday.TableIndex,25), SP.E(EnemyDatabase.FliFli.TableIndex,25)),
            // [3] floor 3
            SP.Pool(4, SP.E(EnemyDatabase.CannibalPlant.TableIndex,20), SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.Sunday.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.KingPrickly.TableIndex,20)),
            // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.Sunday.TableIndex,20), SP.E(EnemyDatabase.FliFli.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.KingPrickly.TableIndex,20)),
            // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.CannibalPlant.TableIndex,20), SP.E(EnemyDatabase.Friday.TableIndex,20), SP.E(EnemyDatabase.Wednesday.TableIndex,20), SP.E(EnemyDatabase.MimicWOF.TableIndex,20), SP.E(EnemyDatabase.HaleyHoley.TableIndex,20)),
            // [6] floor 6
            SP.Pool(4, SP.E(EnemyDatabase.Saturday.TableIndex,20), SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.FliFli.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.WitchIllza.TableIndex,20)),
            // [7] floor 7
            SP.Pool(5, SP.E(EnemyDatabase.Wednesday.TableIndex,20), SP.E(EnemyDatabase.Tuesday.TableIndex,20), SP.E(EnemyDatabase.Monday.TableIndex,20), SP.E(EnemyDatabase.Sunday.TableIndex,20), SP.E(EnemyDatabase.WitchIllza.TableIndex,20)),
            // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(EnemyDatabase.FliFli.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.MimicWOF.TableIndex,20), SP.E(EnemyDatabase.WitchIllza.TableIndex,20), SP.E(EnemyDatabase.KingPrickly.TableIndex,20)),
            // [9] floor 9
            SP.Empty(5),
            // [10] floor 10
            SP.Pool(6, SP.E(EnemyDatabase.CannibalPlant.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.MimicWOF.TableIndex,20), SP.E(EnemyDatabase.EarthDigger.TableIndex,20), SP.E(EnemyDatabase.HaleyHoley.TableIndex,20)),
            // [11] floor 11
            SP.Pool(6, SP.E(EnemyDatabase.FliFli.TableIndex,20), SP.E(EnemyDatabase.WitchIllza.TableIndex,20), SP.E(EnemyDatabase.EarthDigger.TableIndex,20), SP.E(EnemyDatabase.Halloween.TableIndex,20), SP.E(EnemyDatabase.HaleyHoley.TableIndex,20)),
            // [12] floor 12
            SP.Pool(6, SP.E(EnemyDatabase.Hornet.TableIndex,25), SP.E(EnemyDatabase.WitchIllza.TableIndex,25), SP.E(EnemyDatabase.EarthDigger.TableIndex,25), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,25)),
            // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.CannibalPlant.TableIndex,30), SP.E(EnemyDatabase.FliFli.TableIndex,40), SP.E(EnemyDatabase.EarthDigger.TableIndex,30)),
            // [14] floor 14
            SP.Pool(7, SP.E(EnemyDatabase.Sunday.TableIndex,25), SP.E(EnemyDatabase.WitchIllza.TableIndex,25), SP.E(EnemyDatabase.Halloween.TableIndex,25), SP.E(EnemyDatabase.Werewolf.TableIndex,25)),
            // [15] floor 15
            SP.Pool(7, SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.Hornet.TableIndex,20), SP.E(EnemyDatabase.Halloween.TableIndex,20), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,20), SP.E(EnemyDatabase.KingPrickly.TableIndex,20)),
            // [16] floor 16
            SP.Pool(8, SP.E(EnemyDatabase.WitchIllza.TableIndex,20), SP.E(EnemyDatabase.EarthDigger.TableIndex,20), SP.E(EnemyDatabase.Halloween.TableIndex,20), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,20), SP.E(EnemyDatabase.Werewolf.TableIndex,20)),
            // [17] floor 17 — BOSS: MasterUtan
            SP.Pool(1, SP.E(EnemyDatabase.MasterUtan.TableIndex,100)),
        };

        // Back-floor pools for WO floors 1-16 (index N here = back for floor N).
        internal static readonly FloorSpawnPool[] WO_Back = new FloorSpawnPool[]
        {
            SP.Pool(0, SP.E(EnemyDatabase.CannibalPlant.TableIndex,50), SP.E(EnemyDatabase.Hornet.TableIndex,50)),                                // back for floor 1
            SP.Pool(0, SP.E(EnemyDatabase.EarthDigger.TableIndex,100)),                                            // back for floor 2
            SP.Pool(0, SP.E(EnemyDatabase.FliFli.TableIndex,50), SP.E(EnemyDatabase.WitchIllza.TableIndex,50)),                               // back for floor 3
            SP.Pool(0, SP.E(EnemyDatabase.MimicWOF.TableIndex,50), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,50)),                              // back for floor 4
            SP.Pool(0, SP.E(EnemyDatabase.FliFli.TableIndex,30), SP.E(EnemyDatabase.WitchIllza.TableIndex,30), SP.E(EnemyDatabase.Halloween.TableIndex,40)),                  // back for floor 5
            SP.Pool(0, SP.E(EnemyDatabase.Thursday.TableIndex,25), SP.E(EnemyDatabase.Tuesday.TableIndex,25), SP.E(EnemyDatabase.Monday.TableIndex,25), SP.E(EnemyDatabase.Sunday.TableIndex,25)),   // back for floor 6
            SP.Pool(0, SP.E(EnemyDatabase.EarthDigger.TableIndex,50), SP.E(EnemyDatabase.KingPrickly.TableIndex,50)),                              // back for floor 7
            SP.Pool(0, SP.E(EnemyDatabase.MimicWOF.TableIndex,50), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,50)),                              // back for floor 8
            SP.Empty(0),                                                        // back for floor 9
            SP.Pool(0, SP.E(EnemyDatabase.Werewolf.TableIndex,100)),                                            // back for floor 10
            SP.Pool(0, SP.E(EnemyDatabase.Halloween.TableIndex,100)),                                            // back for floor 11
            SP.Pool(0, SP.E(EnemyDatabase.CannibalPlant.TableIndex,50), SP.E(EnemyDatabase.FliFli.TableIndex,50)),                               // back for floor 12
            SP.Pool(0, SP.E(EnemyDatabase.Thursday.TableIndex,20), SP.E(EnemyDatabase.Tuesday.TableIndex,20), SP.E(EnemyDatabase.Monday.TableIndex,20), SP.E(EnemyDatabase.Sunday.TableIndex,20), SP.E(EnemyDatabase.Halloween.TableIndex,20)), // back for floor 13
            SP.Pool(0, SP.E(EnemyDatabase.EarthDigger.TableIndex,100)),                                            // back for floor 14
            SP.Pool(0, SP.E(EnemyDatabase.MimicWOF.TableIndex,50), SP.E(EnemyDatabase.KingMimicWOF.TableIndex,50)),                              // back for floor 15
            SP.Pool(0, SP.E(EnemyDatabase.Saturday.TableIndex,25), SP.E(EnemyDatabase.Friday.TableIndex,25), SP.E(EnemyDatabase.Thursday.TableIndex,25), SP.E(EnemyDatabase.Wednesday.TableIndex,25)),   // back for floor 16
        };

        // ── Dungeon 2 : Shipwreck (SW) ────────────────────────────────────────────────────
        // 18 floors.  Boss: IceQueen (floor 18).  Event floors: 8, 18.
        // NOTE: GetDungeonEventFloors currently returns 17 for the boss floor — should be 18.
        // tbl_92, tbl_101–104 appear only in the boss pool; species names unknown.
        // Front slots 63– 81  RAM 0x00287C60–0x00288440
        // Back  slots 82– 98  RAM 0x002884B0–0x00288BB0

        internal static readonly FloorSpawnPool[] SW_Front = new FloorSpawnPool[]
        {
            // [0] descriptor (empty)
            SP.Empty(0),
            // [1] floor 1
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,30), SP.E(EnemyDatabase.Gunny.TableIndex,70)),
            // [2] floor 2
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,30), SP.E(EnemyDatabase.Gunny.TableIndex,50), SP.E(EnemyDatabase.CursedRose.TableIndex,20)),
            // [3] floor 3
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,40), SP.E(EnemyDatabase.Gunny.TableIndex,50), SP.E(EnemyDatabase.CursedRose.TableIndex,10)),
            // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,20), SP.E(EnemyDatabase.Gunny.TableIndex,35), SP.E(EnemyDatabase.CursedRose.TableIndex,20), SP.E(EnemyDatabase.MimicSW.TableIndex,25)),
            // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,40), SP.E(EnemyDatabase.Gunny.TableIndex,40), SP.E(EnemyDatabase.MimicSW.TableIndex,20)),
            // [6] floor 6
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,5), SP.E(EnemyDatabase.CursedRose.TableIndex,5), SP.E(EnemyDatabase.MimicSW.TableIndex,25), SP.E(EnemyDatabase.Gyon.TableIndex,65)),
            // [7] floor 7
            SP.Pool(5, SP.E(EnemyDatabase.Gunny.TableIndex,30), SP.E(EnemyDatabase.CursedRose.TableIndex,10), SP.E(EnemyDatabase.Gyon.TableIndex,40), SP.E(EnemyDatabase.Sam.TableIndex,20)),
            // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(EnemyDatabase.MimicSW.TableIndex,25), SP.E(EnemyDatabase.Gyon.TableIndex,45), SP.E(EnemyDatabase.Sam.TableIndex,5), SP.E(EnemyDatabase.Captain.TableIndex,25)),
            // [9] floor 9
            SP.Pool(5, SP.E(EnemyDatabase.Corcea.TableIndex,50), SP.E(EnemyDatabase.Captain.TableIndex,25), SP.E(EnemyDatabase.PiratesChariot.TableIndex,25)),
            // [10] floor 10
            SP.Pool(6, SP.E(EnemyDatabase.Gunny.TableIndex,30), SP.E(EnemyDatabase.CursedRose.TableIndex,20), SP.E(EnemyDatabase.Captain.TableIndex,25), SP.E(EnemyDatabase.PiratesChariot.TableIndex,25)),
            // [11] floor 11
            SP.Pool(6, SP.E(EnemyDatabase.Corcea.TableIndex,20), SP.E(EnemyDatabase.Gyon.TableIndex,35), SP.E(EnemyDatabase.PiratesChariot.TableIndex,20), SP.E(EnemyDatabase.KingMimicSW.TableIndex,25)),
            // [12] floor 12
            SP.Pool(6, SP.E(EnemyDatabase.CursedRose.TableIndex,25), SP.E(EnemyDatabase.MimicSW.TableIndex,25), SP.E(EnemyDatabase.Captain.TableIndex,25), SP.E(EnemyDatabase.KingMimicSW.TableIndex,25)),
            // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.MimicSW.TableIndex,25), SP.E(EnemyDatabase.Captain.TableIndex,25), SP.E(EnemyDatabase.PiratesChariot.TableIndex,25), SP.E(EnemyDatabase.AuntieMedu.TableIndex,25)),
            // [14] floor 14
            SP.Pool(7, SP.E(EnemyDatabase.Sam.TableIndex,25), SP.E(EnemyDatabase.KingMimicSW.TableIndex,25), SP.E(EnemyDatabase.AuntieMedu.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,25)),
            // [15] floor 15
            SP.Pool(7, SP.E(EnemyDatabase.Gyon.TableIndex,40), SP.E(EnemyDatabase.Captain.TableIndex,10), SP.E(EnemyDatabase.AuntieMedu.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,25)),
            // [16] floor 16
            SP.Pool(8, SP.E(EnemyDatabase.CursedRose.TableIndex,25), SP.E(EnemyDatabase.Captain.TableIndex,25), SP.E(EnemyDatabase.PiratesChariot.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,25)),
            // [17] floor 17
            SP.Pool(8, SP.E(EnemyDatabase.PiratesChariot.TableIndex,25), SP.E(EnemyDatabase.KingMimicSW.TableIndex,25), SP.E(EnemyDatabase.AuntieMedu.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,25)),
            // [18] floor 18 — BOSS: IceQueen + companions (kori, i_me, i_ta, c17_, e124)
            SP.Pool(1, SP.E(EnemyDatabase.IceQueen.TableIndex,40), SP.E(EnemyDatabase.IQComp101.TableIndex,10), SP.E(EnemyDatabase.IceArrow.TableIndex,10), SP.E(EnemyDatabase.IQComp102.TableIndex,10), SP.E(EnemyDatabase.IQComp103.TableIndex,10), SP.E(EnemyDatabase.SWComp92.TableIndex,10), SP.E(EnemyDatabase.IQComp104.TableIndex,10)),
        };

        internal static readonly FloorSpawnPool[] SW_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(EnemyDatabase.Gunny.TableIndex,50), SP.E(EnemyDatabase.Gyon.TableIndex,50)),                             // back for floor 1
            SP.Pool(4, SP.E(EnemyDatabase.Corcea.TableIndex,50), SP.E(EnemyDatabase.Captain.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(EnemyDatabase.CursedRose.TableIndex,50), SP.E(EnemyDatabase.Captain.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(EnemyDatabase.Sam.TableIndex,30), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,70)),                             // back for floor 4
            SP.Pool(4, SP.E(EnemyDatabase.PiratesChariot.TableIndex,50), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,50)),                             // back for floor 5
            SP.Pool(4, SP.E(EnemyDatabase.MimicSW.TableIndex,50), SP.E(EnemyDatabase.KingMimicSW.TableIndex,50)),                             // back for floor 6
            SP.Pool(4, SP.E(EnemyDatabase.CursedRose.TableIndex,50), SP.E(EnemyDatabase.AuntieMedu.TableIndex,50)),                             // back for floor 7
            SP.Pool(4, SP.E(EnemyDatabase.Gyon.TableIndex,50), SP.E(EnemyDatabase.AuntieMedu.TableIndex,50)),                             // back for floor 8
            SP.Empty(5),                                                       // back for floor 9
            SP.Pool(5, SP.E(EnemyDatabase.MimicSW.TableIndex,50), SP.E(EnemyDatabase.KingMimicSW.TableIndex,50)),                             // back for floor 10
            SP.Pool(5, SP.E(EnemyDatabase.Sam.TableIndex,30), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,70)),                             // back for floor 11
            SP.Pool(6, SP.E(EnemyDatabase.PiratesChariot.TableIndex,50), SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,50)),                             // back for floor 12
            SP.Pool(6, SP.E(EnemyDatabase.Corcea.TableIndex,50), SP.E(EnemyDatabase.CursedRose.TableIndex,50)),                             // back for floor 13
            SP.Pool(5, SP.E(EnemyDatabase.MaskOfPrajna.TableIndex,100)),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(7, SP.E(EnemyDatabase.Gyon.TableIndex,50), SP.E(EnemyDatabase.AuntieMedu.TableIndex,50)),                             // back for floor 15
            SP.Pool(7, SP.E(EnemyDatabase.MimicSW.TableIndex,50), SP.E(EnemyDatabase.KingMimicSW.TableIndex,50)),                             // back for floor 16
            SP.Pool(7, SP.E(EnemyDatabase.Corcea.TableIndex,30), SP.E(EnemyDatabase.Captain.TableIndex,30), SP.E(EnemyDatabase.PiratesChariot.TableIndex,40)),               // back for floor 17
        };

        // ── Dungeon 3 : Sun & Moon Temple (SMT) ──────────────────────────────────────────
        // Front slots  99–117  RAM 0x00288C20–0x00289400
        // Back  slots 118–134  RAM 0x00289470–0x00289B70
        // 18 floors.  Boss: KingsCurse + PhaseEntity100 (floor 18).  Event floors: 8, 18.
        // NOTE: GetDungeonEventFloors currently returns 17 for the boss floor — should be 18.

        internal static readonly FloorSpawnPool[] SMT_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                                   // [0] descriptor
            SP.Pool(4, SP.E(EnemyDatabase.Mummy.TableIndex,50), SP.E(EnemyDatabase.Phantom.TableIndex,50)),                                                         // [1] floor 1
            SP.Pool(4, SP.E(EnemyDatabase.Mummy.TableIndex,40), SP.E(EnemyDatabase.Phantom.TableIndex,30), SP.E(EnemyDatabase.BomberHead.TableIndex,30)),                                            // [2] floor 2
            SP.Pool(4, SP.E(EnemyDatabase.Mummy.TableIndex,25), SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.MimicSMT.TableIndex,25)),                               // [3] floor 3
            SP.Pool(4, SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.MimicSMT.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,25)),                               // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.Mummy.TableIndex,20), SP.E(EnemyDatabase.MimicSMT.TableIndex,35), SP.E(EnemyDatabase.Golem.TableIndex,45)),                                            // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.Mummy.TableIndex,25), SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,25), SP.E(EnemyDatabase.MrBlare.TableIndex,25)),                               // [6] floor 6
            SP.Pool(5, SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.MimicSMT.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermit.TableIndex,25)),                               // [7] floor 7
            SP.Pool(5, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.MrBlare.TableIndex,50)),                                                         // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(EnemyDatabase.Gol.TableIndex,50), SP.E(EnemyDatabase.Sil.TableIndex,50)),                                                         // [9] floor 9
            SP.Pool(6, SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,25), SP.E(EnemyDatabase.Dune.TableIndex,25)),                               // [10] floor 10
            SP.Pool(6, SP.E(EnemyDatabase.MimicSMT.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermit.TableIndex,25), SP.E(EnemyDatabase.Dune.TableIndex,50)),                                            // [11] floor 11
            SP.Pool(6, SP.E(EnemyDatabase.Mummy.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,25), SP.E(EnemyDatabase.Dune.TableIndex,25), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,25)),                               // [12] floor 12
            SP.Pool(7, SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.MrBlare.TableIndex,25), SP.E(EnemyDatabase.Dune.TableIndex,25)),                               // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.MimicSMT.TableIndex,25), SP.E(EnemyDatabase.Golem.TableIndex,20), SP.E(EnemyDatabase.CrabbyHermit.TableIndex,20), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,25), SP.E(EnemyDatabase.BlueDragon.TableIndex,10)),                  // [14] floor 14
            SP.Pool(7, SP.E(EnemyDatabase.Phantom.TableIndex,25), SP.E(EnemyDatabase.BomberHead.TableIndex,25), SP.E(EnemyDatabase.Dune.TableIndex,25), SP.E(EnemyDatabase.SteelGiant.TableIndex,25)),                               // [15] floor 15
            SP.Pool(8, SP.E(EnemyDatabase.MimicSMT.TableIndex,50), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,50)),                                                         // [16] floor 16
            SP.Pool(8, SP.E(EnemyDatabase.Dune.TableIndex,25), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,25), SP.E(EnemyDatabase.BlueDragon.TableIndex,25), SP.E(EnemyDatabase.SteelGiant.TableIndex,25)),                               // [17] floor 17
            SP.Pool(1, SP.E(EnemyDatabase.KingsCurse.TableIndex,50), SP.E(EnemyDatabase.UnknownPhase100.TableIndex,50)),                                                         // [18] floor 18 — BOSS: KingsCurse + PhaseEntity100
        };

        internal static readonly FloorSpawnPool[] SMT_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(EnemyDatabase.Golem.TableIndex,100)),                                          // back for floor 1
            SP.Pool(4, SP.E(EnemyDatabase.Phantom.TableIndex,40), SP.E(EnemyDatabase.CrabbyHermit.TableIndex,20), SP.E(EnemyDatabase.Dune.TableIndex,40)),                // back for floor 2
            SP.Pool(4, SP.E(EnemyDatabase.MimicSMT.TableIndex,50), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.MrBlare.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(EnemyDatabase.Golem.TableIndex,30), SP.E(EnemyDatabase.Dune.TableIndex,30), SP.E(EnemyDatabase.SteelGiant.TableIndex,40)),               // back for floor 5
            SP.Pool(4, SP.E(EnemyDatabase.CrabbyHermit.TableIndex,30), SP.E(EnemyDatabase.Dune.TableIndex,70)),                             // back for floor 6
            SP.Pool(4, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.MrBlare.TableIndex,50)),                             // back for floor 7
            SP.Pool(4, SP.E(EnemyDatabase.MimicSMT.TableIndex,50), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,50)),                             // back for floor 8
            SP.Empty(5),                                                       // back for floor 9
            SP.Pool(5, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.SteelGiant.TableIndex,50)),                             // back for floor 10
            SP.Pool(5, SP.E(EnemyDatabase.BomberHead.TableIndex,100)),                                          // back for floor 11
            SP.Pool(6, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.SteelGiant.TableIndex,50)),                             // back for floor 12
            SP.Pool(6, SP.E(EnemyDatabase.BomberHead.TableIndex,50), SP.E(EnemyDatabase.MrBlare.TableIndex,50)),                             // back for floor 13
            SP.Pool(5, SP.E(EnemyDatabase.Mummy.TableIndex,100)),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(7, SP.E(EnemyDatabase.Phantom.TableIndex,100)),                                          // back for floor 15
            SP.Pool(7, SP.E(EnemyDatabase.MimicSMT.TableIndex,50), SP.E(EnemyDatabase.KingMimicSMT.TableIndex,50)),                             // back for floor 16
            SP.Pool(7, SP.E(EnemyDatabase.BlueDragon.TableIndex,100)),                                          // back for floor 17
        };

        // ── Dungeon 4 : Moon Sea (MS) ─────────────────────────────────────────────────────
        // Front slots 135–150  RAM 0x00289BE0–0x0028A270
        // Back  slots 151–164  RAM 0x0028A2E0–0x0028A890
        // 15 floors.  Boss: MinotaurJoe + WineKeg (floor 15).  Event floors: 7, 15.
        // NOTE: GetDungeonEventFloors currently returns 14 for the boss floor — should be 15.

        internal static readonly FloorSpawnPool[] MS_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                      // [0] descriptor
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,40), SP.E(EnemyDatabase.MoonBug.TableIndex,30), SP.E(EnemyDatabase.WitchHellza.TableIndex,30)),                               // [1] floor 1
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,25), SP.E(EnemyDatabase.MoonBug.TableIndex,25), SP.E(EnemyDatabase.WitchHellza.TableIndex,25), SP.E(EnemyDatabase.SpaceGyon.TableIndex,25)),                  // [2] floor 2
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,25), SP.E(EnemyDatabase.MoonBug.TableIndex,25), SP.E(EnemyDatabase.SpaceGyon.TableIndex,25), SP.E(EnemyDatabase.MimicMS.TableIndex,25)),                  // [3] floor 3
            SP.Pool(4, SP.E(EnemyDatabase.MoonBug.TableIndex,25), SP.E(EnemyDatabase.WitchHellza.TableIndex,25), SP.E(EnemyDatabase.SpaceGyon.TableIndex,25), SP.E(EnemyDatabase.MimicMS.TableIndex,25)),                  // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,50), SP.E(EnemyDatabase.MoonDigger.TableIndex,50)),                                             // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.WitchHellza.TableIndex,25), SP.E(EnemyDatabase.SpaceGyon.TableIndex,25), SP.E(EnemyDatabase.MimicMS.TableIndex,25), SP.E(EnemyDatabase.WhiteFang.TableIndex,25)),                  // [6] floor 6
            SP.Pool(5, SP.E(EnemyDatabase.MoonBug.TableIndex,25), SP.E(EnemyDatabase.MoonDigger.TableIndex,25), SP.E(EnemyDatabase.WhiteFang.TableIndex,25), SP.E(EnemyDatabase.Vulcan.TableIndex,25)),                  // [7] floor 7 — EVENT
            SP.Empty(5),                                                                      // [8] floor 8
            SP.Pool(5, SP.E(EnemyDatabase.SpaceGyon.TableIndex,25), SP.E(EnemyDatabase.MoonDigger.TableIndex,25), SP.E(EnemyDatabase.Vulcan.TableIndex,25), SP.E(EnemyDatabase.KingMimicMS.TableIndex,25)),                  // [9] floor 9
            SP.Pool(6, SP.E(EnemyDatabase.MoonDigger.TableIndex,25), SP.E(EnemyDatabase.WhiteFang.TableIndex,25), SP.E(EnemyDatabase.Vulcan.TableIndex,25), SP.E(EnemyDatabase.Titan.TableIndex,25)),                  // [10] floor 10
            SP.Pool(6, SP.E(EnemyDatabase.MoonBug.TableIndex,50), SP.E(EnemyDatabase.Titan.TableIndex,50)),                                             // [11] floor 11
            SP.Pool(6, SP.E(EnemyDatabase.WitchHellza.TableIndex,25), SP.E(EnemyDatabase.Vulcan.TableIndex,25), SP.E(EnemyDatabase.Titan.TableIndex,25), SP.E(EnemyDatabase.CrescentBaron.TableIndex,25)),                  // [12] floor 12
            SP.Pool(7, SP.E(EnemyDatabase.MoonDigger.TableIndex,25), SP.E(EnemyDatabase.Vulcan.TableIndex,25), SP.E(EnemyDatabase.CrescentBaron.TableIndex,25), SP.E(EnemyDatabase.Arthur.TableIndex,25)),                  // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.KingMimicMS.TableIndex,25), SP.E(EnemyDatabase.Titan.TableIndex,25), SP.E(EnemyDatabase.CrescentBaron.TableIndex,25), SP.E(EnemyDatabase.Arthur.TableIndex,25)),                  // [14] floor 14
            SP.Pool(7, SP.E(EnemyDatabase.MinotaurJoe.TableIndex,50), SP.E(EnemyDatabase.WineKeg.TableIndex,50)),                                             // [15] floor 15 — BOSS: MinotaurJoe + WineKeg
        };

        internal static readonly FloorSpawnPool[] MS_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(EnemyDatabase.MoonDigger.TableIndex,100)),                                          // back for floor 1
            SP.Pool(4, SP.E(EnemyDatabase.Titan.TableIndex,50), SP.E(EnemyDatabase.Arthur.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,50), SP.E(EnemyDatabase.CrescentBaron.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(EnemyDatabase.WitchHellza.TableIndex,50), SP.E(EnemyDatabase.Titan.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(EnemyDatabase.MoonBug.TableIndex,100)),                                          // back for floor 5
            SP.Pool(4, SP.E(EnemyDatabase.HellPockle.TableIndex,33), SP.E(EnemyDatabase.MimicMS.TableIndex,33), SP.E(EnemyDatabase.KingMimicMS.TableIndex,34)),               // back for floor 6
            SP.Pool(4, SP.E(EnemyDatabase.MoonBug.TableIndex,50), SP.E(EnemyDatabase.CrescentBaron.TableIndex,50)),                             // back for floor 7
            SP.Empty(4),                                                       // back for floor 8
            SP.Pool(5, SP.E(EnemyDatabase.HellPockle.TableIndex,50), SP.E(EnemyDatabase.MoonDigger.TableIndex,50)),                             // back for floor 9
            SP.Pool(5, SP.E(EnemyDatabase.Vulcan.TableIndex,100)),                                          // back for floor 10
            SP.Pool(5, SP.E(EnemyDatabase.Arthur.TableIndex,100)),                                          // back for floor 11
            SP.Pool(6, SP.E(EnemyDatabase.SpaceGyon.TableIndex,100)),                                          // back for floor 12
            SP.Pool(6, SP.E(EnemyDatabase.HellPockle.TableIndex,33), SP.E(EnemyDatabase.MimicMS.TableIndex,33), SP.E(EnemyDatabase.KingMimicMS.TableIndex,34)),               // back for floor 13
            SP.Pool(5, SP.E(EnemyDatabase.WitchHellza.TableIndex,34), SP.E(EnemyDatabase.WhiteFang.TableIndex,33), SP.E(EnemyDatabase.CrescentBaron.TableIndex,33)),               // back for floor 14  [tier 5, not 7]
        };

        // ── Dungeon 5 : Gallery of Time (GoT) ────────────────────────────────────────────
        // 24 floors, no boss.  No event floors recorded in code.
        // The DarkGenie boss pool (see DHC section) is stored in the binary immediately after
        // GoT's last front floor, before GoT's back floors.
        // Front slots 165–189  RAM 0x0028A900–0x0028B380
        // Back  slots 192–215  RAM 0x0028B4D0–0x0028BEE0  (slots 190–191 = DarkGenie anomaly)

        internal static readonly FloorSpawnPool[] GoT_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                                    // [0] descriptor
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,40), SP.E(EnemyDatabase.CurseDancer.TableIndex,30), SP.E(EnemyDatabase.DarkFlower.TableIndex,30)),                                             // [1] floor 1
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.CurseDancer.TableIndex,25), SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25)),                                // [2] floor 2
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25), SP.E(EnemyDatabase.LivingArmor.TableIndex,25)),                                // [3] floor 3
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.CurseDancer.TableIndex,25), SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25)),                                // [4] floor 4
            SP.Pool(4, SP.E(EnemyDatabase.CurseDancer.TableIndex,25), SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.RashDasher.TableIndex,25)),                                // [5] floor 5
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.Club.TableIndex,25)),                                // [6] floor 6
            SP.Pool(5, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Heart.TableIndex,25)),                                // [7] floor 7
            SP.Pool(5, SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25), SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.Diamond.TableIndex,25)),                                // [8] floor 8
            SP.Pool(5, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Spade.TableIndex,25)),                                // [9] floor 9
            SP.Pool(6, SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Joker.TableIndex,25)),                                // [10] floor 10
            SP.Pool(6, SP.E(EnemyDatabase.Club.TableIndex,20), SP.E(EnemyDatabase.Heart.TableIndex,20), SP.E(EnemyDatabase.Diamond.TableIndex,20), SP.E(EnemyDatabase.Spade.TableIndex,20), SP.E(EnemyDatabase.Joker.TableIndex,20)),                   // [11] floor 11
            SP.Pool(6, SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Heart.TableIndex,25), SP.E(EnemyDatabase.Joker.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25)),                                // [12] floor 12
            SP.Pool(7, SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Club.TableIndex,25), SP.E(EnemyDatabase.Diamond.TableIndex,25), SP.E(EnemyDatabase.Lich.TableIndex,25)),                                // [13] floor 13
            SP.Pool(7, SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Lich.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25)),                                // [14] floor 14
            SP.Pool(7, SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.Lich.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25)),                                // [15] floor 15
            SP.Pool(8, SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Lich.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),                                // [16] floor 16
            SP.Pool(8, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),                                // [17] floor 17
            SP.Pool(8, SP.E(EnemyDatabase.LivingArmor.TableIndex,40), SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),                                             // [18] floor 18
            SP.Pool(8, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.CurseDancer.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25), SP.E(EnemyDatabase.Lich.TableIndex,25)),                                // [19] floor 19
            SP.Pool(8, SP.E(EnemyDatabase.DarkFlower.TableIndex,25), SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),                                // [20] floor 20
            SP.Pool(8, SP.E(EnemyDatabase.EvilBat.TableIndex,25), SP.E(EnemyDatabase.Billy.TableIndex,25), SP.E(EnemyDatabase.Heart.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25)),                                // [21] floor 21
            SP.Pool(8, SP.E(EnemyDatabase.Joker.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),                                // [22] floor 22
            SP.Pool(8, SP.E(EnemyDatabase.Club.TableIndex,20), SP.E(EnemyDatabase.Heart.TableIndex,20), SP.E(EnemyDatabase.Diamond.TableIndex,20), SP.E(EnemyDatabase.Spade.TableIndex,20), SP.E(EnemyDatabase.Joker.TableIndex,20)),                   // [23] floor 23
            SP.Pool(8, SP.E(EnemyDatabase.Lich.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),                                // [24] floor 24
        };

        // Back-floor pools for GoT floors 1-24.
        internal static readonly FloorSpawnPool[] GoT_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(EnemyDatabase.Heart.TableIndex,50), SP.E(EnemyDatabase.Lich.TableIndex,50)),                             // back for floor 1
            SP.Pool(4, SP.E(EnemyDatabase.LivingArmor.TableIndex,50), SP.E(EnemyDatabase.Alexander.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.BlackDragon.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.DarkFlower.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25)),  // back for floor 5
            SP.Pool(4, SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),  // back for floor 6
            SP.Pool(4, SP.E(EnemyDatabase.LivingArmor.TableIndex,50), SP.E(EnemyDatabase.Alexander.TableIndex,50)),                             // back for floor 7
            SP.Pool(5, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.BlackDragon.TableIndex,50)),                             // back for floor 8
            SP.Pool(5, SP.E(EnemyDatabase.LivingArmor.TableIndex,50), SP.E(EnemyDatabase.Alexander.TableIndex,50)),                             // back for floor 9
            SP.Pool(5, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.DarkFlower.TableIndex,50)),                             // back for floor 10
            SP.Pool(6, SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25)),  // back for floor 11
            SP.Pool(6, SP.E(EnemyDatabase.RashDasher.TableIndex,25), SP.E(EnemyDatabase.Blizzard.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25), SP.E(EnemyDatabase.BlackDragon.TableIndex,25)),  // back for floor 12
            SP.Pool(5, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.BlackDragon.TableIndex,50)),                             // back for floor 13  [tier 5, not 7]
            SP.Pool(7, SP.E(EnemyDatabase.CurseDancer.TableIndex,50), SP.E(EnemyDatabase.Billy.TableIndex,50)),                             // back for floor 14
            SP.Pool(7, SP.E(EnemyDatabase.LivingArmor.TableIndex,25), SP.E(EnemyDatabase.MimicGoT.TableIndex,25), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,25), SP.E(EnemyDatabase.Alexander.TableIndex,25)),  // back for floor 15
            SP.Pool(7, SP.E(EnemyDatabase.EvilBat.TableIndex,100)),                                          // back for floor 16
            SP.Pool(7, SP.E(EnemyDatabase.EvilBat.TableIndex,50), SP.E(EnemyDatabase.DarkFlower.TableIndex,50)),                             // back for floor 17
            SP.Pool(7, SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.Diamond.TableIndex,40), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),               // back for floor 18
            SP.Pool(7, SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.Club.TableIndex,40), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),               // back for floor 19
            SP.Pool(8, SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.Heart.TableIndex,40), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),               // back for floor 20
            SP.Pool(8, SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.Spade.TableIndex,40), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),               // back for floor 21
            SP.Pool(8, SP.E(EnemyDatabase.MimicGoT.TableIndex,30), SP.E(EnemyDatabase.Joker.TableIndex,40), SP.E(EnemyDatabase.KingMimicGoT.TableIndex,30)),               // back for floor 22
            SP.Pool(8, SP.E(EnemyDatabase.Club.TableIndex,20), SP.E(EnemyDatabase.Heart.TableIndex,20), SP.E(EnemyDatabase.Diamond.TableIndex,20), SP.E(EnemyDatabase.Spade.TableIndex,20), SP.E(EnemyDatabase.Joker.TableIndex,20)), // back for floor 23
            SP.Pool(8, SP.E(EnemyDatabase.EvilBat.TableIndex,100)),                                          // back for floor 24
        };

        // DarkGenie boss pool — belongs to DHC but stored in the binary after GoT's last
        // front floor (slot 190) and before GoT's back floors.  The companion empty slot
        // (slot 191) occupies the back-floor position for this boss encounter.
        internal static readonly FloorSpawnPool DHC_DarkGenieBoss =
            SP.Pool(1, SP.E(EnemyDatabase.DarkGenie.TableIndex,40), SP.E(EnemyDatabase.DarkGenieForm2.TableIndex,10), SP.E(EnemyDatabase.RightHand.TableIndex,10), SP.E(EnemyDatabase.LeftHand.TableIndex,10),
                       SP.E(EnemyDatabase.DGComp88.TableIndex,10), SP.E(EnemyDatabase.DGComp89.TableIndex,10), SP.E(EnemyDatabase.DGComp90.TableIndex,5), SP.E(EnemyDatabase.DGComp93.TableIndex,5));

        // ── Dungeon 6 : Demon Shaft (DHC) ────────────────────────────────────────────────
        // 100 floors.  Boss: BlackKnight + tbl_165 (floor 100); DarkGenie is the true final
        // boss (see DHC_DarkGenieBoss above, stored separately in the binary).
        // NOTE: GetDungeonEventFloors currently returns 99 — boss floor is 100 (off by 1).
        // Front slots 216–316  RAM 0x0028BF50–0x0028EB10
        // Back  slots 317–415  RAM 0x0028EB80–0x00291660
        //
        // Enemy groups by floor range:
        //   Floors   1-20 : GemronFire group  (GemronFire, Nikapous, tbl_113–120)
        //   Floors  21-40 : GemronIce group   (GemronIce, HornHead, KingMimicDHC, MimicDHC, tbl_123–129)
        //   Floors  41-60 : GemronThunder group (GemronThunder, BishopQ, tbl_134–142)
        //   Floors  61-80 : GemronWind group  (GemronWind, SilverGear, tbl_145–153)
        //   Floors  81-99 : GemronHoly group  (GemronHoly, tbl_155–165)
        //   Floor  100    : BlackKnight boss (t1)
        //
        // All tbl_NNN entries in DHC have unknown species names.

        internal static readonly FloorSpawnPool[] DHC_Front = BuildDHCFront();
        internal static readonly FloorSpawnPool[] DHC_Back  = BuildDHCBack();

        private static FloorSpawnPool[] BuildDHCFront()
        {
            // 101 entries: [0] descriptor + floors 1-100
            var p = new FloorSpawnPool[101];
            p[0] = SP.Empty(0); // descriptor

            // Floors 1-20: GemronFire group
            p[1]  = SP.Pool(4, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[2]  = SP.Pool(4, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            p[3]  = SP.Pool(4, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[4]  = SP.Pool(4, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[5]  = SP.Pool(4, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            p[6]  = SP.Pool(4, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[7]  = SP.Pool(5, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[8]  = SP.Pool(5, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            p[9]  = SP.Pool(5, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[10] = SP.Pool(6, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[11] = SP.Pool(6, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[12] = SP.Pool(6, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[13] = SP.Pool(7, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[14] = SP.Pool(7, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            p[15] = SP.Pool(7, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            p[16] = SP.Pool(8, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[17] = SP.Pool(8, SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            p[18] = SP.Pool(8, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25));
            p[19] = SP.Pool(8, SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            p[20] = SP.Pool(8, SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));

            // Floors 21-40: GemronIce group
            p[21] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25));
            p[22] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            p[23] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            p[24] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[25] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25));
            p[26] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            p[27] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            p[28] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[29] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            p[30] = SP.Pool(8, SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            p[31] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[32] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[33] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            p[34] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[35] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[36] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            p[37] = SP.Pool(8, SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[38] = SP.Pool(8, SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            p[39] = SP.Pool(8, SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            p[40] = SP.Pool(8, SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25)); // same as floor 37

            // Floors 41-60: GemronThunder group
            p[41] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25));
            p[42] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            p[43] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            p[44] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[45] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25));
            p[46] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            p[47] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            p[48] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[49] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            p[50] = SP.Pool(8, SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            p[51] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[52] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[53] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            p[54] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[55] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[56] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            p[57] = SP.Pool(8, SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[58] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            p[59] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            p[60] = SP.Pool(8, SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));

            // Floors 61-80: GemronWind group
            p[61] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25));
            p[62] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            p[63] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            p[64] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[65] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25));
            p[66] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            p[67] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            p[68] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[69] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            p[70] = SP.Pool(8, SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            p[71] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[72] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[73] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            p[74] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[75] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[76] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            p[77] = SP.Pool(8, SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[78] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            p[79] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            p[80] = SP.Pool(8, SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));

            // Floors 81-99: GemronHoly group
            p[81] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25));
            p[82] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            p[83] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            p[84] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[85] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25));
            p[86] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            p[87] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            p[88] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[89] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            p[90] = SP.Pool(8, SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            p[91] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[92] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[93] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            p[94] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[95] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[96] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            p[97] = SP.Pool(8, SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            p[98] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            p[99] = SP.Pool(8, SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));

            // Floor 100 — BOSS: BlackKnight (tbl_165) + garbage padding (tbl_166, as extracted from binary)
            p[100] = SP.Pool(1, SP.E(EnemyDatabase.DHC166.TableIndex,50), SP.E(EnemyDatabase.BlackKnight.TableIndex,50));

            return p;
        }

        private static FloorSpawnPool[] BuildDHCBack()
        {
            // 99 back floors for DHC floors 1-99 (boss at 100 has no back floor).
            var b = new FloorSpawnPool[99];

            // Back floors 1-20: GemronFire group variants
            b[0]  = SP.Pool(4, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[1]  = SP.Pool(4, SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            b[2]  = SP.Pool(4, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25));
            b[3]  = SP.Pool(4, SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[4]  = SP.Pool(4, SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[5]  = SP.Pool(4, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            b[6]  = SP.Pool(4, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            b[7]  = SP.Pool(4, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[8]  = SP.Pool(5, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            b[9]  = SP.Pool(5, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            b[10] = SP.Pool(5, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            b[11] = SP.Pool(6, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[12] = SP.Pool(6, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            b[13] = SP.Pool(5, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            b[14] = SP.Pool(7, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[15] = SP.Pool(7, SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[16] = SP.Pool(7, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronFire.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));
            b[17] = SP.Pool(7, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.MasterJacketEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.ArthurEnhanced.TableIndex,25));
            b[18] = SP.Pool(7, SP.E(EnemyDatabase.MummyEnhanced.TableIndex,25), SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.WhiteFangEnhanced.TableIndex,25), SP.E(EnemyDatabase.VulcanEnhanced.TableIndex,25));
            b[19] = SP.Pool(7, SP.E(EnemyDatabase.HalloweenEnhanced.TableIndex,25), SP.E(EnemyDatabase.DiamondEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilEnhanced.TableIndex,25), SP.E(EnemyDatabase.Nikapous.TableIndex,25));

            // Back floors 21-40: GemronIce group variants
            b[20] = SP.Pool(8, SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[21] = SP.Pool(8, SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[22] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[23] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[24] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25));
            b[25] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[26] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            b[27] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[28] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25));
            b[29] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[30] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            b[31] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[32] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[33] = SP.Pool(8, SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));
            b[34] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDS.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[35] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.GemronIce.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[36] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.WitchHellzaEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25));
            b[37] = SP.Pool(8, SP.E(EnemyDatabase.CorceaEnhanced.TableIndex,25), SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.RockanoffEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));

            // Back floors 39-40 bridge gap (the two remaining GemronIce back slots)
            b[38] = SP.Pool(8, SP.E(EnemyDatabase.HornHead.TableIndex,25), SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.SteelGiantEnhanced.TableIndex,25));
            b[39] = SP.Pool(8, SP.E(EnemyDatabase.YammichEnhanced.TableIndex,25), SP.E(EnemyDatabase.ClubEnhanced.TableIndex,25), SP.E(EnemyDatabase.AuntieMeduEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDS.TableIndex,25));

            // Back floors 41-60: GemronThunder group variants
            b[40] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            b[41] = SP.Pool(8, SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[42] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            b[43] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[44] = SP.Pool(8, SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[45] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25));
            b[46] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            b[47] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            b[48] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[49] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25));
            b[50] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            b[51] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            b[52] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[53] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            b[54] = SP.Pool(8, SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25));
            b[55] = SP.Pool(8, SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[56] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronThunder.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[57] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpadeEnhanced.TableIndex,25), SP.E(EnemyDatabase.RashDasherEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25));
            b[58] = SP.Pool(8, SP.E(EnemyDatabase.CaptainEnhanced.TableIndex,25), SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GolEnhanced.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));
            b[59] = SP.Pool(8, SP.E(EnemyDatabase.CaveBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.BishopQ.TableIndex,25), SP.E(EnemyDatabase.MaskOfPrajnaEnhanced.TableIndex,25));

            // Back floors 61-80: GemronWind group variants
            b[60] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            b[61] = SP.Pool(8, SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[62] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            b[63] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[64] = SP.Pool(8, SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[65] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25));
            b[66] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            b[67] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            b[68] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[69] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25));
            b[70] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            b[71] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            b[72] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[73] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            b[74] = SP.Pool(8, SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25));
            b[75] = SP.Pool(8, SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[76] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronWind.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[77] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.SilverGear.TableIndex,25), SP.E(EnemyDatabase.HeartEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25));
            b[78] = SP.Pool(8, SP.E(EnemyDatabase.BomberHeadEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.PiratesChariotEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));
            b[79] = SP.Pool(8, SP.E(EnemyDatabase.CrabbyHermitEnhanced.TableIndex,25), SP.E(EnemyDatabase.CursedRoseEnhanced.TableIndex,25), SP.E(EnemyDatabase.SpaceGyonEnhanced.TableIndex,25), SP.E(EnemyDatabase.AlexanderEnhanced.TableIndex,25));

            // Back floors 81-99: GemronHoly group variants
            b[80] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[81] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            b[82] = SP.Pool(8, SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[83] = SP.Pool(8, SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            b[84] = SP.Pool(8, SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[85] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25));
            b[86] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            b[87] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            b[88] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[89] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25));
            b[90] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            b[91] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            b[92] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[93] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            b[94] = SP.Pool(8, SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25));
            b[95] = SP.Pool(8, SP.E(EnemyDatabase.LichEnhanced.TableIndex,25), SP.E(EnemyDatabase.MimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[96] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.GemronHoly.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));
            b[97] = SP.Pool(8, SP.E(EnemyDatabase.LivingArmorEnhanced.TableIndex,25), SP.E(EnemyDatabase.StatueDogEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.GaciousEnhanced.TableIndex,25));
            b[98] = SP.Pool(8, SP.E(EnemyDatabase.EvilBatEnhanced.TableIndex,25), SP.E(EnemyDatabase.TitanEnhanced.TableIndex,25), SP.E(EnemyDatabase.JokerEnhanced.TableIndex,25), SP.E(EnemyDatabase.CrescentBaronEnhanced.TableIndex,25));

            return b;
        }
    }
}
