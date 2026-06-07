using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    // DUNGEON RUNTIME MEMORY MAP (PCSX2 addresses, PS2 = addr − 0x20000000)
    // Investigation via LogFloorDataForTileMapSearch() — 2026-06-06 pass 1 (13 regions).
    // Goal: locate the walkable tile map for valid enemy spawn-position selection.
    // ──────────────────────────────────────────────────────────────────────────────────────────
    //
    // CONFIRMED: MIPS/VU EXECUTABLE CODE (never changes, eliminated as data candidates)
    //   0x201C7000 — dun.bin code segment 1 (MIPS VU microcode)
    //   0x201DC000 — dun.bin code segment 2 (MIPS VU microcode)
    //
    // CONFIRMED: UNALLOCATED / ZERO PADDING (no data loaded here)
    //   0x20980000, 0x20990000, 0x21CD0000
    //
    // CONFIRMED: ASSET STRING TABLE (changes per dungeon, not per floor)
    //   0x20930000 — room type / room shape asset strings
    //   0x20940000 — actor model paths, animation names (in_door, cdoor, ndoor_on/off,
    //                urac01.chr … urac18.chr, c04bjump.chr, chr1_3, chr1_4, u03_top, etc.)
    //
    // CONFIRMED: SHIFT-JIS DIALOGUE / ITEM NAMES
    //   0x21CC0000 — encoded text used by UI (FD-prefixed character codes + FF 01/02 delimiters)
    //
    // CONFIRMED: AI / SCRIPTED EVENT PARAMETER TABLES (changes per floor)
    //   0x20920000 — AI script parameter stream: type-tagged 4-byte values (tag 02=float,
    //                tag 03=int) alternating with float parameters for enemy behavior scripts.
    //                Contains world-space radii and trigger distances (e.g. 22.24, 3.58, 17.09,
    //                111.3 world units).  Distinct count 45 (DBC) vs 12 (WOF) — content varies.
    //   0x20928000 — Continuation / second half of AI script parameters.  Floats here are in
    //                range 107–304 world units (DBC F1), consistent with encounter radii.
    //
    // CONFIRMED: TELEPORT COST TABLE (19 entries, static per dungeon)
    //   0x21DD0000–0x21DD011F — 19 records × 16 bytes:
    //     [0] int  : item_id (-1 for most floors, 44 = escape item for boss floor)
    //     [1] int  : 0
    //     [2] float: gald cost to warp to that floor (DBC range: ~2481–4857, increasing)
    //     [3] int  : 0
    //   All 18 first entries have item_id = -1.  Entry 18 has item_id = 44 (escape item).
    //
    // *** KEY FIND *** ROOM LAYOUT GRID (changes per floor)
    //   0x21DD0130–0x21DD018F — 24 consecutive 4-byte integers, values 0–11, then zeros.
    //   These are room type codes for a 4×6 (or 6×4) procedural layout grid.
    //   Room type values 0–11 correspond to room shapes (dead end, straight, L-turn,
    //   T-junction, 4-way, etc.).  The grid differs between dungeons and between floors
    //   of the same dungeon.  DBC F1 example: [9,10,3,4,3,7,3,3,8,2,4,4,1,11,3,4,2,0,4,3].
    //   WOF F0 example: [11,7,4,3,5,5,4,4,7,0,3,3,11,1,4,3,3,11,3,3,0,3,3,3].
    //   This is the room-level connectivity map, NOT per-tile walkability.
    //
    // CONFIRMED: PER-ROOM CHEST / ITEM SPAWN TABLE (changes per floor and per visit state)
    //   0x21DE0000 — 32+ entries × 16 bytes:
    //     [0] int  : item_id (room/chest item ID; -1 = empty or padding slot; 44 = special)
    //     [1] int  : secondary flag (0 or small int; role unclear)
    //     [2] float: -1.0 on freshly-entered floors (DBC F0, WOF F0); populated float on
    //                visited floors (DBC F1: values 545–2155, then a second decreasing set).
    //                Float likely encodes a chest's Y or path-distance world coordinate.
    //     [3] int  : 0 or 1 (rare flag; possibly "visited" or "required")
    //   WOF F0 has non-(-1) item_ids but still -1.0 floats — suggests the float is assigned
    //   lazily by a pathfinding pass that runs after initial spawn logging.
    //
    // CONFIRMED: ENEMY INSTANCE DATA (fills in after enemies actually spawn)
    //   0x21E16800 — base of the live enemy instance block; begins with model name string
    //                (e.g. "e101a", "e17a"), then per-instance stat words.  All zeros on
    //                DBC F0 (captured pre-spawn); populated by DBC F1 and WOF F0.
    //
    // NOT FOUND: WALKABLE TILE MAP
    //   None of the 13 scanned regions contains a 2D byte grid with walkable/wall values.
    //   The collision tile map is computed at runtime from the loaded .mds room meshes and
    //   lives in a dynamically-allocated buffer whose address has not been identified.
    //   The 'map' / 'miniMap' constants at 0x202A359C / 0x202A35B0 are display flags,
    //   not pointers to tile data.
    //
    // PASS 2 RESULTS (2026-06-06)
    //   Gap regions (0x20948000–0x20978000): all zeros — unallocated.  Tile map is not here.
    //   Pointer sniff (0x202A355C–0x202A35FC): 9 pointers found, all constant across floors
    //   and dungeons.  Targets fall in two groups:
    //     0x202A3574–357C → 0x2036E650/0x2036EF10/0x20373090 — renderer/scripting tables
    //     0x202A35D0–35E8 → 0x21EC7940–0x21F00110 — audio/physics engine tables (upper RAM)
    //   None point to a tile map.
    //
    //   CONCLUSION: the walkable tile map is computed at runtime from the loaded .mds meshes
    //   and lives in a dynamically-allocated buffer at an address that varies each run.
    //   Tile-map scanning is not feasible without hooking the mesh-to-nav-grid conversion.
    //
    // *** KEY FIND *** CHEST SPAWN TABLE (changes per floor; confirmed 2026-06-06)
    //   Base: firstChest = 0x21DD0260 (entity ID field of slot 0).
    //   Count address: firstChest − 0x30 = 0x21DD0230 (int; observed 5–6 chests per floor).
    //   Stride: 0x40 bytes between consecutive chest slots.
    //   SlotBase(i) = firstChest + i × 0x40
    //
    //   Field offsets relative to SlotBase(i):
    //     −0x20 : active flag (int)  — 1 = alive (unopened), 0 = empty/looted
    //     −0x10 : world X (float32) — confirmed world-scale (500–2000 range, matches enemy coords)
    //     −0x08 : world Y (float32) — confirmed world-scale
    //     +0x00 : entity ID (int)   — firstChest (0x21DD0260) is this field for slot 0
    //     +0x08 : chest size (int)  — firstChestSize (0x21DD0268) is this field for slot 0
    //
    //   The engine always places chests on walkable tiles.  Active chest world positions are
    //   therefore a reliable source of valid floor coordinates without needing the tile map —
    //   usable as teleport targets for FixModelRedirectSpawnPositions().
    //   See ChestsAddresses.cs for the typed address constants.
    // ══════════════════════════════════════════════════════════════════════════════════════════

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
    //   WOF_Front          29 –  46   0x00286D80 – 0x002874F0
    //   WOF_Back           47 –  62   0x00287560 – 0x00287BF0
    //   SW_Front          63 –  81   0x00287C60 – 0x00288440
    //   SW_Back           82 –  98   0x002884B0 – 0x00288BB0
    //   SMT_Front         99 – 117   0x00288C20 – 0x00289400
    //   SMT_Back         118 – 134   0x00289470 – 0x00289B70
    //   MS_Front         135 – 150   0x00289BE0 – 0x0028A270
    //   MS_Back          151 – 164   0x0028A2E0 – 0x0028A890
    //   GoT_Front        165 – 189   0x0028A900 – 0x0028B380
    //   GoT_DarkGenie        190      0x0028B3F0            ← anomaly: stored before GoT_Back
    //   [GoT boss back]      191      0x0028B460            ← empty companion slot
    //   GoT_Back         192 – 215   0x0028B4D0 – 0x0028BEE0
    //   DS_Front        216 – 316   0x0028BF50 – 0x0028EB10
    //   DS_Back         317 – 415   0x0028EB80 – 0x00291660
    //
    // DUNGEON STRUCTURE (slot indices, stride 28 words)
    // --------------------------------------------------
    // Each dungeon occupies a contiguous block:
    //   [descriptor slot][front floor slots 1..N][back floor slots for floors 1..N-1]
    // The boss floor (last front slot) has no back floor.
    // DBC is unique: its descriptor (slot 0) is non-empty and has an associated back-floor slot.
    //
    // TABLEINDEX RESOLUTION (EnemySpeciesTable scan 2026-06-05)
    // ----------------------------------------------------------
    // Valid table range confirmed: tbl_0–165.  tbl_166+ is garbage (hp=0, empty code).
    // All previously unknown indices are now named in Enemies:
    //   SW boss companions     : tbl_92  → SWComp92; tbl_101–104 → IQComp101–104
    //   DarkGenie companions   : tbl_88–90 → DGComp88–90; tbl_93 → DGComp93
    //   DS GemronFire group   : tbl_113–120 → WhiteFangEnhanced–DiamondEnhanced
    //   DS GemronIce group    : tbl_123–129 → AuntieMeduEnhanced–CorceaEnhanced
    //   DS GemronThunder group: tbl_134–142 → CaveBatEnhanced–KingMimicDSEnhanced
    //   DS GemronWind group   : tbl_145–153 → AlexanderEnhanced–KingMimicDSEnhancedTwice
    //   DS GemronHoly group   : tbl_155–165 → GaciousEnhanced–DS165 (tbl_165 = BlackKnight)
    //   DS floor 100 padding  : tbl_166     → DS166 (garbage row, present in binary)
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
            SP.Pool(3, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.SkeletonSoldier.TableIndex,50), SP.E(Enemies.Dasher.TableIndex,30)),
            // [1] floor 1
            SP.Pool(3, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.SkeletonSoldier.TableIndex,20), SP.E(Enemies.Dasher.TableIndex,40), SP.E(Enemies.Yammich.TableIndex,20)),
            // [2] floor 2
            SP.Pool(3, SP.E(Enemies.CaveBat.TableIndex,25), SP.E(Enemies.SkeletonSoldier.TableIndex,25), SP.E(Enemies.Dasher.TableIndex,10), SP.E(Enemies.Statue.TableIndex,20), SP.E(Enemies.StatueDog.TableIndex,20)),
            // [3] floor 3 — EVENT: skeleton encounter (SkeletonSoldier:100%)
            SP.Pool(3, SP.E(Enemies.SkeletonSoldier.TableIndex,100)),
            // [4] floor 4
            SP.Pool(4, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Dasher.TableIndex,20), SP.E(Enemies.Statue.TableIndex,20), SP.E(Enemies.StatueDog.TableIndex,20), SP.E(Enemies.Yammich.TableIndex,20)),
            // [5] floor 5
            SP.Pool(4, SP.E(Enemies.CaveBat.TableIndex,25), SP.E(Enemies.SkeletonSoldier.TableIndex,25), SP.E(Enemies.Statue.TableIndex,25), SP.E(Enemies.MimicDBC.TableIndex,25)),
            // [6] floor 6
            SP.Pool(5, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.SkeletonSoldier.TableIndex,20), SP.E(Enemies.Ghost.TableIndex,20), SP.E(Enemies.Opar.TableIndex,20), SP.E(Enemies.MasterJacket.TableIndex,20)),
            // [7] floor 7 — EVENT: story (no enemy spawns)
            SP.Empty(5),
            // [8] floor 8
            SP.Pool(5, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Ghost.TableIndex,20), SP.E(Enemies.Statue.TableIndex,20), SP.E(Enemies.StatueDog.TableIndex,20), SP.E(Enemies.MimicDBC.TableIndex,20)),
            // [9] floor 9
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,25), SP.E(Enemies.Dasher.TableIndex,10), SP.E(Enemies.Ghost.TableIndex,10), SP.E(Enemies.MasterJacket.TableIndex,30), SP.E(Enemies.Rockanoff.TableIndex,25)),
            // [10] floor 10
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Dasher.TableIndex,25), SP.E(Enemies.Opar.TableIndex,20), SP.E(Enemies.MasterJacket.TableIndex,20), SP.E(Enemies.Dragon.TableIndex,15)),
            // [11] floor 11
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Statue.TableIndex,20), SP.E(Enemies.MasterJacket.TableIndex,20), SP.E(Enemies.KingMimicDBC.TableIndex,20), SP.E(Enemies.Rockanoff.TableIndex,20)),
            // [12] floor 12
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Opar.TableIndex,20), SP.E(Enemies.MasterJacket.TableIndex,20), SP.E(Enemies.Rockanoff.TableIndex,20), SP.E(Enemies.Dragon.TableIndex,20)),
            // [13] floor 13
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,20), SP.E(Enemies.Ghost.TableIndex,20), SP.E(Enemies.KingMimicDBC.TableIndex,20), SP.E(Enemies.Rockanoff.TableIndex,20), SP.E(Enemies.Dragon.TableIndex,20)),
            // [14] floor 14 — BOSS: Dran
            SP.Pool(7, SP.E(Enemies.Dran.TableIndex,100)),
        };

        // Back-floor pools for DBC slots 0-13 (index N here = back for Front[N]).
        internal static readonly FloorSpawnPool[] DBC_Back = new FloorSpawnPool[]
        {
            SP.Pool(3, SP.E(Enemies.SkeletonSoldier.TableIndex,60), SP.E(Enemies.Ghost.TableIndex,20), SP.E(Enemies.Yammich.TableIndex,20)),        // back for descriptor
            SP.Pool(3, SP.E(Enemies.SkeletonSoldier.TableIndex,80), SP.E(Enemies.Ghost.TableIndex,20)),                      // back for floor 1
            SP.Pool(3, SP.E(Enemies.SkeletonSoldier.TableIndex,80), SP.E(Enemies.Ghost.TableIndex,20)),                      // back for floor 2
            SP.Empty(3),                                               // back for floor 3 (event)
            SP.Pool(4, SP.E(Enemies.MimicDBC.TableIndex,50), SP.E(Enemies.KingMimicDBC.TableIndex,50)),                     // back for floor 4
            SP.Pool(4, SP.E(Enemies.Statue.TableIndex,50), SP.E(Enemies.StatueDog.TableIndex,50)),                      // back for floor 5
            SP.Pool(5, SP.E(Enemies.Dasher.TableIndex,50), SP.E(Enemies.Rockanoff.TableIndex,50)),                      // back for floor 6
            SP.Empty(5),                                               // back for floor 7 (event)
            SP.Pool(5, SP.E(Enemies.CaveBat.TableIndex,100)),                                  // back for floor 8
            SP.Pool(7, SP.E(Enemies.MimicDBC.TableIndex,30), SP.E(Enemies.KingMimicDBC.TableIndex,30), SP.E(Enemies.Rockanoff.TableIndex,40)),        // back for floor 9
            SP.Pool(7, SP.E(Enemies.Ghost.TableIndex,25), SP.E(Enemies.MimicDBC.TableIndex,50), SP.E(Enemies.MasterJacket.TableIndex,25)),         // back for floor 10
            SP.Pool(7, SP.E(Enemies.CaveBat.TableIndex,50), SP.E(Enemies.Dragon.TableIndex,50)),                     // back for floor 11
            SP.Pool(7, SP.E(Enemies.MimicDBC.TableIndex,50), SP.E(Enemies.KingMimicDBC.TableIndex,50)),                     // back for floor 12
            SP.Pool(7, SP.E(Enemies.Rockanoff.TableIndex,100)),                                  // back for floor 13
        };

        // ── Dungeon 1 : Wise Owl Forest (WOF) ─────────────────────────────────────────────
        // 17 floors.  Boss: MasterUtan (floor 17).  Event floors: 8, 17.
        // NOTE: GetDungeonEventFloors currently returns 16 for the boss floor — should be 17.
        // Front slots 29– 46  RAM 0x00286D80–0x002874F0
        // Back  slots 47– 62  RAM 0x00287560–0x00287BF0

        internal static readonly FloorSpawnPool[] WOF_Front = new FloorSpawnPool[]
        {
            // [0] descriptor (empty)
            SP.Empty(0),
            // [1] floor 1
            SP.Pool(4, SP.E(Enemies.CannibalPlant.TableIndex,20), SP.E(Enemies.Saturday.TableIndex,20), SP.E(Enemies.Friday.TableIndex,20), SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.Wednesday.TableIndex,20)),
            // [2] floor 2
            SP.Pool(4, SP.E(Enemies.CannibalPlant.TableIndex,25), SP.E(Enemies.Tuesday.TableIndex,25), SP.E(Enemies.Monday.TableIndex,25), SP.E(Enemies.FliFli.TableIndex,25)),
            // [3] floor 3
            SP.Pool(4, SP.E(Enemies.CannibalPlant.TableIndex,20), SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.Sunday.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.KingPrickly.TableIndex,20)),
            // [4] floor 4
            SP.Pool(4, SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.Sunday.TableIndex,20), SP.E(Enemies.FliFli.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.KingPrickly.TableIndex,20)),
            // [5] floor 5
            SP.Pool(4, SP.E(Enemies.CannibalPlant.TableIndex,20), SP.E(Enemies.Friday.TableIndex,20), SP.E(Enemies.Wednesday.TableIndex,20), SP.E(Enemies.MimicWOF.TableIndex,20), SP.E(Enemies.HaleyHoley.TableIndex,20)),
            // [6] floor 6
            SP.Pool(4, SP.E(Enemies.Saturday.TableIndex,20), SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.FliFli.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.WitchIllza.TableIndex,20)),
            // [7] floor 7
            SP.Pool(5, SP.E(Enemies.Wednesday.TableIndex,20), SP.E(Enemies.Tuesday.TableIndex,20), SP.E(Enemies.Monday.TableIndex,20), SP.E(Enemies.Sunday.TableIndex,20), SP.E(Enemies.WitchIllza.TableIndex,20)),
            // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(Enemies.FliFli.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.MimicWOF.TableIndex,20), SP.E(Enemies.WitchIllza.TableIndex,20), SP.E(Enemies.KingPrickly.TableIndex,20)),
            // [9] floor 9
            SP.Empty(5),
            // [10] floor 10
            SP.Pool(6, SP.E(Enemies.CannibalPlant.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.MimicWOF.TableIndex,20), SP.E(Enemies.EarthDigger.TableIndex,20), SP.E(Enemies.HaleyHoley.TableIndex,20)),
            // [11] floor 11
            SP.Pool(6, SP.E(Enemies.FliFli.TableIndex,20), SP.E(Enemies.WitchIllza.TableIndex,20), SP.E(Enemies.EarthDigger.TableIndex,20), SP.E(Enemies.Halloween.TableIndex,20), SP.E(Enemies.HaleyHoley.TableIndex,20)),
            // [12] floor 12
            SP.Pool(6, SP.E(Enemies.Hornet.TableIndex,25), SP.E(Enemies.WitchIllza.TableIndex,25), SP.E(Enemies.EarthDigger.TableIndex,25), SP.E(Enemies.KingMimicWOF.TableIndex,25)),
            // [13] floor 13
            SP.Pool(7, SP.E(Enemies.CannibalPlant.TableIndex,30), SP.E(Enemies.FliFli.TableIndex,40), SP.E(Enemies.EarthDigger.TableIndex,30)),
            // [14] floor 14
            SP.Pool(7, SP.E(Enemies.Sunday.TableIndex,25), SP.E(Enemies.WitchIllza.TableIndex,25), SP.E(Enemies.Halloween.TableIndex,25), SP.E(Enemies.Werewolf.TableIndex,25)),
            // [15] floor 15
            SP.Pool(7, SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.Hornet.TableIndex,20), SP.E(Enemies.Halloween.TableIndex,20), SP.E(Enemies.KingMimicWOF.TableIndex,20), SP.E(Enemies.KingPrickly.TableIndex,20)),
            // [16] floor 16
            SP.Pool(8, SP.E(Enemies.WitchIllza.TableIndex,20), SP.E(Enemies.EarthDigger.TableIndex,20), SP.E(Enemies.Halloween.TableIndex,20), SP.E(Enemies.KingMimicWOF.TableIndex,20), SP.E(Enemies.Werewolf.TableIndex,20)),
            // [17] floor 17 — BOSS: MasterUtan
            SP.Pool(1, SP.E(Enemies.MasterUtan.TableIndex,100)),
        };

        // Back-floor pools for WOF floors 1-16 (index N here = back for floor N).
        internal static readonly FloorSpawnPool[] WOF_Back = new FloorSpawnPool[]
        {
            SP.Pool(0, SP.E(Enemies.CannibalPlant.TableIndex,50), SP.E(Enemies.Hornet.TableIndex,50)),                                // back for floor 1
            SP.Pool(0, SP.E(Enemies.EarthDigger.TableIndex,100)),                                            // back for floor 2
            SP.Pool(0, SP.E(Enemies.FliFli.TableIndex,50), SP.E(Enemies.WitchIllza.TableIndex,50)),                               // back for floor 3
            SP.Pool(0, SP.E(Enemies.MimicWOF.TableIndex,50), SP.E(Enemies.KingMimicWOF.TableIndex,50)),                              // back for floor 4
            SP.Pool(0, SP.E(Enemies.FliFli.TableIndex,30), SP.E(Enemies.WitchIllza.TableIndex,30), SP.E(Enemies.Halloween.TableIndex,40)),                  // back for floor 5
            SP.Pool(0, SP.E(Enemies.Thursday.TableIndex,25), SP.E(Enemies.Tuesday.TableIndex,25), SP.E(Enemies.Monday.TableIndex,25), SP.E(Enemies.Sunday.TableIndex,25)),   // back for floor 6
            SP.Pool(0, SP.E(Enemies.EarthDigger.TableIndex,50), SP.E(Enemies.KingPrickly.TableIndex,50)),                              // back for floor 7
            SP.Pool(0, SP.E(Enemies.MimicWOF.TableIndex,50), SP.E(Enemies.KingMimicWOF.TableIndex,50)),                              // back for floor 8
            SP.Empty(0),                                                        // back for floor 9
            SP.Pool(0, SP.E(Enemies.Werewolf.TableIndex,100)),                                            // back for floor 10
            SP.Pool(0, SP.E(Enemies.Halloween.TableIndex,100)),                                            // back for floor 11
            SP.Pool(0, SP.E(Enemies.CannibalPlant.TableIndex,50), SP.E(Enemies.FliFli.TableIndex,50)),                               // back for floor 12
            SP.Pool(0, SP.E(Enemies.Thursday.TableIndex,20), SP.E(Enemies.Tuesday.TableIndex,20), SP.E(Enemies.Monday.TableIndex,20), SP.E(Enemies.Sunday.TableIndex,20), SP.E(Enemies.Halloween.TableIndex,20)), // back for floor 13
            SP.Pool(0, SP.E(Enemies.EarthDigger.TableIndex,100)),                                            // back for floor 14
            SP.Pool(0, SP.E(Enemies.MimicWOF.TableIndex,50), SP.E(Enemies.KingMimicWOF.TableIndex,50)),                              // back for floor 15
            SP.Pool(0, SP.E(Enemies.Saturday.TableIndex,25), SP.E(Enemies.Friday.TableIndex,25), SP.E(Enemies.Thursday.TableIndex,25), SP.E(Enemies.Wednesday.TableIndex,25)),   // back for floor 16
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
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,30), SP.E(Enemies.Gunny.TableIndex,70)),
            // [2] floor 2
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,30), SP.E(Enemies.Gunny.TableIndex,50), SP.E(Enemies.CursedRose.TableIndex,20)),
            // [3] floor 3
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,40), SP.E(Enemies.Gunny.TableIndex,50), SP.E(Enemies.CursedRose.TableIndex,10)),
            // [4] floor 4
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,20), SP.E(Enemies.Gunny.TableIndex,35), SP.E(Enemies.CursedRose.TableIndex,20), SP.E(Enemies.MimicSW.TableIndex,25)),
            // [5] floor 5
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,40), SP.E(Enemies.Gunny.TableIndex,40), SP.E(Enemies.MimicSW.TableIndex,20)),
            // [6] floor 6
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,5), SP.E(Enemies.CursedRose.TableIndex,5), SP.E(Enemies.MimicSW.TableIndex,25), SP.E(Enemies.Gyon.TableIndex,65)),
            // [7] floor 7
            SP.Pool(5, SP.E(Enemies.Gunny.TableIndex,30), SP.E(Enemies.CursedRose.TableIndex,10), SP.E(Enemies.Gyon.TableIndex,40), SP.E(Enemies.Sam.TableIndex,20)),
            // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(Enemies.MimicSW.TableIndex,25), SP.E(Enemies.Gyon.TableIndex,45), SP.E(Enemies.Sam.TableIndex,5), SP.E(Enemies.Captain.TableIndex,25)),
            // [9] floor 9
            SP.Pool(5, SP.E(Enemies.Corcea.TableIndex,50), SP.E(Enemies.Captain.TableIndex,25), SP.E(Enemies.PiratesChariot.TableIndex,25)),
            // [10] floor 10
            SP.Pool(6, SP.E(Enemies.Gunny.TableIndex,30), SP.E(Enemies.CursedRose.TableIndex,20), SP.E(Enemies.Captain.TableIndex,25), SP.E(Enemies.PiratesChariot.TableIndex,25)),
            // [11] floor 11
            SP.Pool(6, SP.E(Enemies.Corcea.TableIndex,20), SP.E(Enemies.Gyon.TableIndex,35), SP.E(Enemies.PiratesChariot.TableIndex,20), SP.E(Enemies.KingMimicSW.TableIndex,25)),
            // [12] floor 12
            SP.Pool(6, SP.E(Enemies.CursedRose.TableIndex,25), SP.E(Enemies.MimicSW.TableIndex,25), SP.E(Enemies.Captain.TableIndex,25), SP.E(Enemies.KingMimicSW.TableIndex,25)),
            // [13] floor 13
            SP.Pool(7, SP.E(Enemies.MimicSW.TableIndex,25), SP.E(Enemies.Captain.TableIndex,25), SP.E(Enemies.PiratesChariot.TableIndex,25), SP.E(Enemies.AuntieMedu.TableIndex,25)),
            // [14] floor 14
            SP.Pool(7, SP.E(Enemies.Sam.TableIndex,25), SP.E(Enemies.KingMimicSW.TableIndex,25), SP.E(Enemies.AuntieMedu.TableIndex,25), SP.E(Enemies.MaskOfPrajna.TableIndex,25)),
            // [15] floor 15
            SP.Pool(7, SP.E(Enemies.Gyon.TableIndex,40), SP.E(Enemies.Captain.TableIndex,10), SP.E(Enemies.AuntieMedu.TableIndex,25), SP.E(Enemies.MaskOfPrajna.TableIndex,25)),
            // [16] floor 16
            SP.Pool(8, SP.E(Enemies.CursedRose.TableIndex,25), SP.E(Enemies.Captain.TableIndex,25), SP.E(Enemies.PiratesChariot.TableIndex,25), SP.E(Enemies.MaskOfPrajna.TableIndex,25)),
            // [17] floor 17
            SP.Pool(8, SP.E(Enemies.PiratesChariot.TableIndex,25), SP.E(Enemies.KingMimicSW.TableIndex,25), SP.E(Enemies.AuntieMedu.TableIndex,25), SP.E(Enemies.MaskOfPrajna.TableIndex,25)),
            // [18] floor 18 — BOSS: IceQueen + companions (kori, i_me, i_ta, c17_, e124)
            SP.Pool(1, SP.E(Enemies.IceQueen.TableIndex,40), SP.E(Enemies.IQComp101.TableIndex,10), SP.E(Enemies.IceArrow.TableIndex,10), SP.E(Enemies.IQComp102.TableIndex,10), SP.E(Enemies.IQComp103.TableIndex,10), SP.E(Enemies.SWComp92.TableIndex,10), SP.E(Enemies.IQComp104.TableIndex,10)),
        };

        internal static readonly FloorSpawnPool[] SW_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(Enemies.Gunny.TableIndex,50), SP.E(Enemies.Gyon.TableIndex,50)),                             // back for floor 1
            SP.Pool(4, SP.E(Enemies.Corcea.TableIndex,50), SP.E(Enemies.Captain.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(Enemies.CursedRose.TableIndex,50), SP.E(Enemies.Captain.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(Enemies.Sam.TableIndex,30), SP.E(Enemies.MaskOfPrajna.TableIndex,70)),                             // back for floor 4
            SP.Pool(4, SP.E(Enemies.PiratesChariot.TableIndex,50), SP.E(Enemies.MaskOfPrajna.TableIndex,50)),                             // back for floor 5
            SP.Pool(4, SP.E(Enemies.MimicSW.TableIndex,50), SP.E(Enemies.KingMimicSW.TableIndex,50)),                             // back for floor 6
            SP.Pool(4, SP.E(Enemies.CursedRose.TableIndex,50), SP.E(Enemies.AuntieMedu.TableIndex,50)),                             // back for floor 7
            SP.Pool(4, SP.E(Enemies.Gyon.TableIndex,50), SP.E(Enemies.AuntieMedu.TableIndex,50)),                             // back for floor 8
            SP.Empty(5),                                                       // back for floor 9
            SP.Pool(5, SP.E(Enemies.MimicSW.TableIndex,50), SP.E(Enemies.KingMimicSW.TableIndex,50)),                             // back for floor 10
            SP.Pool(5, SP.E(Enemies.Sam.TableIndex,30), SP.E(Enemies.MaskOfPrajna.TableIndex,70)),                             // back for floor 11
            SP.Pool(6, SP.E(Enemies.PiratesChariot.TableIndex,50), SP.E(Enemies.MaskOfPrajna.TableIndex,50)),                             // back for floor 12
            SP.Pool(6, SP.E(Enemies.Corcea.TableIndex,50), SP.E(Enemies.CursedRose.TableIndex,50)),                             // back for floor 13
            SP.Pool(5, SP.E(Enemies.MaskOfPrajna.TableIndex,100)),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(7, SP.E(Enemies.Gyon.TableIndex,50), SP.E(Enemies.AuntieMedu.TableIndex,50)),                             // back for floor 15
            SP.Pool(7, SP.E(Enemies.MimicSW.TableIndex,50), SP.E(Enemies.KingMimicSW.TableIndex,50)),                             // back for floor 16
            SP.Pool(7, SP.E(Enemies.Corcea.TableIndex,30), SP.E(Enemies.Captain.TableIndex,30), SP.E(Enemies.PiratesChariot.TableIndex,40)),               // back for floor 17
        };

        // ── Dungeon 3 : Sun & Moon Temple (SMT) ──────────────────────────────────────────
        // Front slots  99–117  RAM 0x00288C20–0x00289400
        // Back  slots 118–134  RAM 0x00289470–0x00289B70
        // 18 floors.  Boss: KingsCurse + PhaseEntity100 (floor 18).  Event floors: 8, 18.
        // NOTE: GetDungeonEventFloors currently returns 17 for the boss floor — should be 18.

        internal static readonly FloorSpawnPool[] SMT_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                                   // [0] descriptor
            SP.Pool(4, SP.E(Enemies.Mummy.TableIndex,50), SP.E(Enemies.Phantom.TableIndex,50)),                                                         // [1] floor 1
            SP.Pool(4, SP.E(Enemies.Mummy.TableIndex,40), SP.E(Enemies.Phantom.TableIndex,30), SP.E(Enemies.BomberHead.TableIndex,30)),                                            // [2] floor 2
            SP.Pool(4, SP.E(Enemies.Mummy.TableIndex,25), SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.MimicSMT.TableIndex,25)),                               // [3] floor 3
            SP.Pool(4, SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.MimicSMT.TableIndex,25), SP.E(Enemies.Golem.TableIndex,25)),                               // [4] floor 4
            SP.Pool(4, SP.E(Enemies.Mummy.TableIndex,20), SP.E(Enemies.MimicSMT.TableIndex,35), SP.E(Enemies.Golem.TableIndex,45)),                                            // [5] floor 5
            SP.Pool(4, SP.E(Enemies.Mummy.TableIndex,25), SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.Golem.TableIndex,25), SP.E(Enemies.MrBlare.TableIndex,25)),                               // [6] floor 6
            SP.Pool(5, SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.MimicSMT.TableIndex,25), SP.E(Enemies.Golem.TableIndex,25), SP.E(Enemies.CrabbyHermit.TableIndex,25)),                               // [7] floor 7
            SP.Pool(5, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.MrBlare.TableIndex,50)),                                                         // [8] floor 8 — EVENT
            SP.Pool(5, SP.E(Enemies.Gol.TableIndex,50), SP.E(Enemies.Sil.TableIndex,50)),                                                         // [9] floor 9
            SP.Pool(6, SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.Golem.TableIndex,25), SP.E(Enemies.Dune.TableIndex,25)),                               // [10] floor 10
            SP.Pool(6, SP.E(Enemies.MimicSMT.TableIndex,25), SP.E(Enemies.CrabbyHermit.TableIndex,25), SP.E(Enemies.Dune.TableIndex,50)),                                            // [11] floor 11
            SP.Pool(6, SP.E(Enemies.Mummy.TableIndex,25), SP.E(Enemies.Golem.TableIndex,25), SP.E(Enemies.Dune.TableIndex,25), SP.E(Enemies.KingMimicSMT.TableIndex,25)),                               // [12] floor 12
            SP.Pool(7, SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.MrBlare.TableIndex,25), SP.E(Enemies.Dune.TableIndex,25)),                               // [13] floor 13
            SP.Pool(7, SP.E(Enemies.MimicSMT.TableIndex,25), SP.E(Enemies.Golem.TableIndex,20), SP.E(Enemies.CrabbyHermit.TableIndex,20), SP.E(Enemies.KingMimicSMT.TableIndex,25), SP.E(Enemies.BlueDragon.TableIndex,10)),                  // [14] floor 14
            SP.Pool(7, SP.E(Enemies.Phantom.TableIndex,25), SP.E(Enemies.BomberHead.TableIndex,25), SP.E(Enemies.Dune.TableIndex,25), SP.E(Enemies.SteelGiant.TableIndex,25)),                               // [15] floor 15
            SP.Pool(8, SP.E(Enemies.MimicSMT.TableIndex,50), SP.E(Enemies.KingMimicSMT.TableIndex,50)),                                                         // [16] floor 16
            SP.Pool(8, SP.E(Enemies.Dune.TableIndex,25), SP.E(Enemies.KingMimicSMT.TableIndex,25), SP.E(Enemies.BlueDragon.TableIndex,25), SP.E(Enemies.SteelGiant.TableIndex,25)),                               // [17] floor 17
            SP.Pool(1, SP.E(Enemies.KingsCurse.TableIndex,50), SP.E(Enemies.UnknownPhase100.TableIndex,50)),                                                         // [18] floor 18 — BOSS: KingsCurse + PhaseEntity100
        };

        internal static readonly FloorSpawnPool[] SMT_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(Enemies.Golem.TableIndex,100)),                                          // back for floor 1
            SP.Pool(4, SP.E(Enemies.Phantom.TableIndex,40), SP.E(Enemies.CrabbyHermit.TableIndex,20), SP.E(Enemies.Dune.TableIndex,40)),                // back for floor 2
            SP.Pool(4, SP.E(Enemies.MimicSMT.TableIndex,50), SP.E(Enemies.KingMimicSMT.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.MrBlare.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(Enemies.Golem.TableIndex,30), SP.E(Enemies.Dune.TableIndex,30), SP.E(Enemies.SteelGiant.TableIndex,40)),               // back for floor 5
            SP.Pool(4, SP.E(Enemies.CrabbyHermit.TableIndex,30), SP.E(Enemies.Dune.TableIndex,70)),                             // back for floor 6
            SP.Pool(4, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.MrBlare.TableIndex,50)),                             // back for floor 7
            SP.Pool(4, SP.E(Enemies.MimicSMT.TableIndex,50), SP.E(Enemies.KingMimicSMT.TableIndex,50)),                             // back for floor 8
            SP.Empty(5),                                                       // back for floor 9
            SP.Pool(5, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.SteelGiant.TableIndex,50)),                             // back for floor 10
            SP.Pool(5, SP.E(Enemies.BomberHead.TableIndex,100)),                                          // back for floor 11
            SP.Pool(6, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.SteelGiant.TableIndex,50)),                             // back for floor 12
            SP.Pool(6, SP.E(Enemies.BomberHead.TableIndex,50), SP.E(Enemies.MrBlare.TableIndex,50)),                             // back for floor 13
            SP.Pool(5, SP.E(Enemies.Mummy.TableIndex,100)),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(7, SP.E(Enemies.Phantom.TableIndex,100)),                                          // back for floor 15
            SP.Pool(7, SP.E(Enemies.MimicSMT.TableIndex,50), SP.E(Enemies.KingMimicSMT.TableIndex,50)),                             // back for floor 16
            SP.Pool(7, SP.E(Enemies.BlueDragon.TableIndex,100)),                                          // back for floor 17
        };

        // ── Dungeon 4 : Moon Sea (MS) ─────────────────────────────────────────────────────
        // Front slots 135–150  RAM 0x00289BE0–0x0028A270
        // Back  slots 151–164  RAM 0x0028A2E0–0x0028A890
        // 15 floors.  Boss: MinotaurJoe + WineKeg (floor 15).  Event floors: 7, 15.
        // NOTE: GetDungeonEventFloors currently returns 14 for the boss floor — should be 15.

        internal static readonly FloorSpawnPool[] MS_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                      // [0] descriptor
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,40), SP.E(Enemies.MoonBug.TableIndex,30), SP.E(Enemies.WitchHellza.TableIndex,30)),                               // [1] floor 1
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,25), SP.E(Enemies.MoonBug.TableIndex,25), SP.E(Enemies.WitchHellza.TableIndex,25), SP.E(Enemies.SpaceGyon.TableIndex,25)),                  // [2] floor 2
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,25), SP.E(Enemies.MoonBug.TableIndex,25), SP.E(Enemies.SpaceGyon.TableIndex,25), SP.E(Enemies.MimicMS.TableIndex,25)),                  // [3] floor 3
            SP.Pool(4, SP.E(Enemies.MoonBug.TableIndex,25), SP.E(Enemies.WitchHellza.TableIndex,25), SP.E(Enemies.SpaceGyon.TableIndex,25), SP.E(Enemies.MimicMS.TableIndex,25)),                  // [4] floor 4
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,50), SP.E(Enemies.MoonDigger.TableIndex,50)),                                             // [5] floor 5
            SP.Pool(4, SP.E(Enemies.WitchHellza.TableIndex,25), SP.E(Enemies.SpaceGyon.TableIndex,25), SP.E(Enemies.MimicMS.TableIndex,25), SP.E(Enemies.WhiteFang.TableIndex,25)),                  // [6] floor 6
            SP.Pool(5, SP.E(Enemies.MoonBug.TableIndex,25), SP.E(Enemies.MoonDigger.TableIndex,25), SP.E(Enemies.WhiteFang.TableIndex,25), SP.E(Enemies.Vulcan.TableIndex,25)),                  // [7] floor 7 — EVENT
            SP.Empty(5),                                                                      // [8] floor 8
            SP.Pool(5, SP.E(Enemies.SpaceGyon.TableIndex,25), SP.E(Enemies.MoonDigger.TableIndex,25), SP.E(Enemies.Vulcan.TableIndex,25), SP.E(Enemies.KingMimicMS.TableIndex,25)),                  // [9] floor 9
            SP.Pool(6, SP.E(Enemies.MoonDigger.TableIndex,25), SP.E(Enemies.WhiteFang.TableIndex,25), SP.E(Enemies.Vulcan.TableIndex,25), SP.E(Enemies.Titan.TableIndex,25)),                  // [10] floor 10
            SP.Pool(6, SP.E(Enemies.MoonBug.TableIndex,50), SP.E(Enemies.Titan.TableIndex,50)),                                             // [11] floor 11
            SP.Pool(6, SP.E(Enemies.WitchHellza.TableIndex,25), SP.E(Enemies.Vulcan.TableIndex,25), SP.E(Enemies.Titan.TableIndex,25), SP.E(Enemies.CrescentBaron.TableIndex,25)),                  // [12] floor 12
            SP.Pool(7, SP.E(Enemies.MoonDigger.TableIndex,25), SP.E(Enemies.Vulcan.TableIndex,25), SP.E(Enemies.CrescentBaron.TableIndex,25), SP.E(Enemies.Arthur.TableIndex,25)),                  // [13] floor 13
            SP.Pool(7, SP.E(Enemies.KingMimicMS.TableIndex,25), SP.E(Enemies.Titan.TableIndex,25), SP.E(Enemies.CrescentBaron.TableIndex,25), SP.E(Enemies.Arthur.TableIndex,25)),                  // [14] floor 14
            SP.Pool(7, SP.E(Enemies.MinotaurJoe.TableIndex,50), SP.E(Enemies.WineKeg.TableIndex,50)),                                             // [15] floor 15 — BOSS: MinotaurJoe + WineKeg
        };

        internal static readonly FloorSpawnPool[] MS_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(Enemies.MoonDigger.TableIndex,100)),                                          // back for floor 1
            SP.Pool(4, SP.E(Enemies.Titan.TableIndex,50), SP.E(Enemies.Arthur.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,50), SP.E(Enemies.CrescentBaron.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(Enemies.WitchHellza.TableIndex,50), SP.E(Enemies.Titan.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(Enemies.MoonBug.TableIndex,100)),                                          // back for floor 5
            SP.Pool(4, SP.E(Enemies.HellPockle.TableIndex,33), SP.E(Enemies.MimicMS.TableIndex,33), SP.E(Enemies.KingMimicMS.TableIndex,34)),               // back for floor 6
            SP.Pool(4, SP.E(Enemies.MoonBug.TableIndex,50), SP.E(Enemies.CrescentBaron.TableIndex,50)),                             // back for floor 7
            SP.Empty(4),                                                       // back for floor 8
            SP.Pool(5, SP.E(Enemies.HellPockle.TableIndex,50), SP.E(Enemies.MoonDigger.TableIndex,50)),                             // back for floor 9
            SP.Pool(5, SP.E(Enemies.Vulcan.TableIndex,100)),                                          // back for floor 10
            SP.Pool(5, SP.E(Enemies.Arthur.TableIndex,100)),                                          // back for floor 11
            SP.Pool(6, SP.E(Enemies.SpaceGyon.TableIndex,100)),                                          // back for floor 12
            SP.Pool(6, SP.E(Enemies.HellPockle.TableIndex,33), SP.E(Enemies.MimicMS.TableIndex,33), SP.E(Enemies.KingMimicMS.TableIndex,34)),               // back for floor 13
            SP.Pool(5, SP.E(Enemies.WitchHellza.TableIndex,34), SP.E(Enemies.WhiteFang.TableIndex,33), SP.E(Enemies.CrescentBaron.TableIndex,33)),               // back for floor 14  [tier 5, not 7]
        };

        // ── Dungeon 5 : Gallery of Time (GoT) ────────────────────────────────────────────
        // 24 floors, no boss.  No event floors recorded in code.
        // The DarkGenie boss pool (see DS section) is stored in the binary immediately after
        // GoT's last front floor, before GoT's back floors.
        // Front slots 165–189  RAM 0x0028A900–0x0028B380
        // Back  slots 192–215  RAM 0x0028B4D0–0x0028BEE0  (slots 190–191 = DarkGenie anomaly)

        internal static readonly FloorSpawnPool[] GoT_Front = new FloorSpawnPool[]
        {
            SP.Empty(0),                                                                                    // [0] descriptor
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,40), SP.E(Enemies.CurseDancer.TableIndex,30), SP.E(Enemies.DarkFlower.TableIndex,30)),                                             // [1] floor 1
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.CurseDancer.TableIndex,25), SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25)),                                // [2] floor 2
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25), SP.E(Enemies.LivingArmor.TableIndex,25)),                                // [3] floor 3
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.CurseDancer.TableIndex,25), SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25)),                                // [4] floor 4
            SP.Pool(4, SP.E(Enemies.CurseDancer.TableIndex,25), SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.RashDasher.TableIndex,25)),                                // [5] floor 5
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.Club.TableIndex,25)),                                // [6] floor 6
            SP.Pool(5, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Heart.TableIndex,25)),                                // [7] floor 7
            SP.Pool(5, SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25), SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.Diamond.TableIndex,25)),                                // [8] floor 8
            SP.Pool(5, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Spade.TableIndex,25)),                                // [9] floor 9
            SP.Pool(6, SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Joker.TableIndex,25)),                                // [10] floor 10
            SP.Pool(6, SP.E(Enemies.Club.TableIndex,20), SP.E(Enemies.Heart.TableIndex,20), SP.E(Enemies.Diamond.TableIndex,20), SP.E(Enemies.Spade.TableIndex,20), SP.E(Enemies.Joker.TableIndex,20)),                   // [11] floor 11
            SP.Pool(6, SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Heart.TableIndex,25), SP.E(Enemies.Joker.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25)),                                // [12] floor 12
            SP.Pool(7, SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Club.TableIndex,25), SP.E(Enemies.Diamond.TableIndex,25), SP.E(Enemies.Lich.TableIndex,25)),                                // [13] floor 13
            SP.Pool(7, SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Lich.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25)),                                // [14] floor 14
            SP.Pool(7, SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.Lich.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25)),                                // [15] floor 15
            SP.Pool(8, SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Lich.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),                                // [16] floor 16
            SP.Pool(8, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),                                // [17] floor 17
            SP.Pool(8, SP.E(Enemies.LivingArmor.TableIndex,40), SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.KingMimicGoT.TableIndex,30)),                                             // [18] floor 18
            SP.Pool(8, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.CurseDancer.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25), SP.E(Enemies.Lich.TableIndex,25)),                                // [19] floor 19
            SP.Pool(8, SP.E(Enemies.DarkFlower.TableIndex,25), SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),                                // [20] floor 20
            SP.Pool(8, SP.E(Enemies.EvilBat.TableIndex,25), SP.E(Enemies.Billy.TableIndex,25), SP.E(Enemies.Heart.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25)),                                // [21] floor 21
            SP.Pool(8, SP.E(Enemies.Joker.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),                                // [22] floor 22
            SP.Pool(8, SP.E(Enemies.Club.TableIndex,20), SP.E(Enemies.Heart.TableIndex,20), SP.E(Enemies.Diamond.TableIndex,20), SP.E(Enemies.Spade.TableIndex,20), SP.E(Enemies.Joker.TableIndex,20)),                   // [23] floor 23
            SP.Pool(8, SP.E(Enemies.Lich.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),                                // [24] floor 24
        };

        // Back-floor pools for GoT floors 1-24.
        internal static readonly FloorSpawnPool[] GoT_Back = new FloorSpawnPool[]
        {
            SP.Pool(4, SP.E(Enemies.Heart.TableIndex,50), SP.E(Enemies.Lich.TableIndex,50)),                             // back for floor 1
            SP.Pool(4, SP.E(Enemies.LivingArmor.TableIndex,50), SP.E(Enemies.Alexander.TableIndex,50)),                             // back for floor 2
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.BlackDragon.TableIndex,50)),                             // back for floor 3
            SP.Pool(4, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.DarkFlower.TableIndex,50)),                             // back for floor 4
            SP.Pool(4, SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25)),  // back for floor 5
            SP.Pool(4, SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),  // back for floor 6
            SP.Pool(4, SP.E(Enemies.LivingArmor.TableIndex,50), SP.E(Enemies.Alexander.TableIndex,50)),                             // back for floor 7
            SP.Pool(5, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.BlackDragon.TableIndex,50)),                             // back for floor 8
            SP.Pool(5, SP.E(Enemies.LivingArmor.TableIndex,50), SP.E(Enemies.Alexander.TableIndex,50)),                             // back for floor 9
            SP.Pool(5, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.DarkFlower.TableIndex,50)),                             // back for floor 10
            SP.Pool(6, SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25)),  // back for floor 11
            SP.Pool(6, SP.E(Enemies.RashDasher.TableIndex,25), SP.E(Enemies.Blizzard.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25), SP.E(Enemies.BlackDragon.TableIndex,25)),  // back for floor 12
            SP.Pool(5, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.BlackDragon.TableIndex,50)),                             // back for floor 13  [tier 5, not 7]
            SP.Pool(7, SP.E(Enemies.CurseDancer.TableIndex,50), SP.E(Enemies.Billy.TableIndex,50)),                             // back for floor 14
            SP.Pool(7, SP.E(Enemies.LivingArmor.TableIndex,25), SP.E(Enemies.MimicGoT.TableIndex,25), SP.E(Enemies.KingMimicGoT.TableIndex,25), SP.E(Enemies.Alexander.TableIndex,25)),  // back for floor 15
            SP.Pool(7, SP.E(Enemies.EvilBat.TableIndex,100)),                                          // back for floor 16
            SP.Pool(7, SP.E(Enemies.EvilBat.TableIndex,50), SP.E(Enemies.DarkFlower.TableIndex,50)),                             // back for floor 17
            SP.Pool(7, SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.Diamond.TableIndex,40), SP.E(Enemies.KingMimicGoT.TableIndex,30)),               // back for floor 18
            SP.Pool(7, SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.Club.TableIndex,40), SP.E(Enemies.KingMimicGoT.TableIndex,30)),               // back for floor 19
            SP.Pool(8, SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.Heart.TableIndex,40), SP.E(Enemies.KingMimicGoT.TableIndex,30)),               // back for floor 20
            SP.Pool(8, SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.Spade.TableIndex,40), SP.E(Enemies.KingMimicGoT.TableIndex,30)),               // back for floor 21
            SP.Pool(8, SP.E(Enemies.MimicGoT.TableIndex,30), SP.E(Enemies.Joker.TableIndex,40), SP.E(Enemies.KingMimicGoT.TableIndex,30)),               // back for floor 22
            SP.Pool(8, SP.E(Enemies.Club.TableIndex,20), SP.E(Enemies.Heart.TableIndex,20), SP.E(Enemies.Diamond.TableIndex,20), SP.E(Enemies.Spade.TableIndex,20), SP.E(Enemies.Joker.TableIndex,20)), // back for floor 23
            SP.Pool(8, SP.E(Enemies.EvilBat.TableIndex,100)),                                          // back for floor 24
        };

        // DarkGenie boss pool — belongs to DS but stored in the binary after GoT's last
        // front floor (slot 190) and before GoT's back floors.  The companion empty slot
        // (slot 191) occupies the back-floor position for this boss encounter.
        internal static readonly FloorSpawnPool GoT_DarkGenieBoss =
            SP.Pool(1, SP.E(Enemies.DarkGenie.TableIndex,40), SP.E(Enemies.DarkGenieForm2.TableIndex,10), SP.E(Enemies.RightHand.TableIndex,10), SP.E(Enemies.LeftHand.TableIndex,10),
                       SP.E(Enemies.DGComp88.TableIndex,10), SP.E(Enemies.DGComp89.TableIndex,10), SP.E(Enemies.DGComp90.TableIndex,5), SP.E(Enemies.DGComp93.TableIndex,5));

        // ── Dungeon 6 : Demon Shaft (DS) ────────────────────────────────────────────────
        // 100 floors.  Boss: BlackKnight + tbl_165 (floor 100); DarkGenie is the true final
        // boss (see DS_DarkGenieBoss above, stored separately in the binary).
        // NOTE: GetDungeonEventFloors currently returns 99 — boss floor is 100 (off by 1).
        // Front slots 216–316  RAM 0x0028BF50–0x0028EB10
        // Back  slots 317–415  RAM 0x0028EB80–0x00291660
        //
        // Enemy groups by floor range:
        //   Floors   1-20 : GemronFire group  (GemronFire, Nikapous, tbl_113–120)
        //   Floors  21-40 : GemronIce group   (GemronIce, HornHead, KingMimicDS, MimicDS, tbl_123–129)
        //   Floors  41-60 : GemronThunder group (GemronThunder, BishopQ, tbl_134–142)
        //   Floors  61-80 : GemronWind group  (GemronWind, SilverGear, tbl_145–153)
        //   Floors  81-99 : GemronHoly group  (GemronHoly, tbl_155–165)
        //   Floor  100    : BlackKnight boss (t1)
        //
        // All tbl_NNN entries in DS have unknown species names.

        internal static readonly FloorSpawnPool[] DS_Front = BuildDSFront();
        internal static readonly FloorSpawnPool[] DS_Back  = BuildDSBack();

        private static FloorSpawnPool[] BuildDSFront()
        {
            // 101 entries: [0] descriptor + floors 1-100
            var p = new FloorSpawnPool[101];
            p[0] = SP.Empty(0); // descriptor

            // Floors 1-20: GemronFire group
            p[1]  = SP.Pool(4, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[2]  = SP.Pool(4, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            p[3]  = SP.Pool(4, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[4]  = SP.Pool(4, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[5]  = SP.Pool(4, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            p[6]  = SP.Pool(4, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[7]  = SP.Pool(5, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[8]  = SP.Pool(5, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            p[9]  = SP.Pool(5, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[10] = SP.Pool(6, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[11] = SP.Pool(6, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[12] = SP.Pool(6, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[13] = SP.Pool(7, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[14] = SP.Pool(7, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            p[15] = SP.Pool(7, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            p[16] = SP.Pool(8, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[17] = SP.Pool(8, SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            p[18] = SP.Pool(8, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25));
            p[19] = SP.Pool(8, SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            p[20] = SP.Pool(8, SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));

            // Floors 21-40: GemronIce group
            p[21] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25));
            p[22] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            p[23] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            p[24] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[25] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25));
            p[26] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            p[27] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            p[28] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[29] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            p[30] = SP.Pool(8, SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            p[31] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[32] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[33] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            p[34] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[35] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[36] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            p[37] = SP.Pool(8, SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[38] = SP.Pool(8, SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            p[39] = SP.Pool(8, SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            p[40] = SP.Pool(8, SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25)); // same as floor 37

            // Floors 41-60: GemronThunder group
            p[41] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25));
            p[42] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            p[43] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            p[44] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[45] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25));
            p[46] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            p[47] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            p[48] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[49] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            p[50] = SP.Pool(8, SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            p[51] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[52] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[53] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            p[54] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[55] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[56] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            p[57] = SP.Pool(8, SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[58] = SP.Pool(8, SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            p[59] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            p[60] = SP.Pool(8, SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));

            // Floors 61-80: GemronWind group
            p[61] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25));
            p[62] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            p[63] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            p[64] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[65] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25));
            p[66] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            p[67] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            p[68] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[69] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            p[70] = SP.Pool(8, SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            p[71] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[72] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[73] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            p[74] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[75] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[76] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            p[77] = SP.Pool(8, SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[78] = SP.Pool(8, SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            p[79] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            p[80] = SP.Pool(8, SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));

            // Floors 81-99: GemronHoly group
            p[81] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25));
            p[82] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            p[83] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            p[84] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[85] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25));
            p[86] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            p[87] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            p[88] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[89] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            p[90] = SP.Pool(8, SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            p[91] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[92] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[93] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            p[94] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[95] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[96] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            p[97] = SP.Pool(8, SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            p[98] = SP.Pool(8, SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            p[99] = SP.Pool(8, SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));

            // Floor 100 — BOSS: BlackKnight (tbl_165) + garbage padding (tbl_166, as extracted from binary)
            p[100] = SP.Pool(1, SP.E(Enemies.DS166.TableIndex,50), SP.E(Enemies.BlackKnight.TableIndex,50));

            return p;
        }

        private static FloorSpawnPool[] BuildDSBack()
        {
            // 99 back floors for DS floors 1-99 (boss at 100 has no back floor).
            var b = new FloorSpawnPool[99];

            // Back floors 1-20: GemronFire group variants
            b[0]  = SP.Pool(4, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[1]  = SP.Pool(4, SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            b[2]  = SP.Pool(4, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25));
            b[3]  = SP.Pool(4, SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[4]  = SP.Pool(4, SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[5]  = SP.Pool(4, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            b[6]  = SP.Pool(4, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            b[7]  = SP.Pool(4, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[8]  = SP.Pool(5, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            b[9]  = SP.Pool(5, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            b[10] = SP.Pool(5, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            b[11] = SP.Pool(6, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[12] = SP.Pool(6, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            b[13] = SP.Pool(5, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            b[14] = SP.Pool(7, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[15] = SP.Pool(7, SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[16] = SP.Pool(7, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.GemronFire.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));
            b[17] = SP.Pool(7, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.MasterJacketEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.ArthurEnhanced.TableIndex,25));
            b[18] = SP.Pool(7, SP.E(Enemies.MummyEnhanced.TableIndex,25), SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.WhiteFangEnhanced.TableIndex,25), SP.E(Enemies.VulcanEnhanced.TableIndex,25));
            b[19] = SP.Pool(7, SP.E(Enemies.HalloweenEnhanced.TableIndex,25), SP.E(Enemies.DiamondEnhanced.TableIndex,25), SP.E(Enemies.SilEnhanced.TableIndex,25), SP.E(Enemies.Nikapous.TableIndex,25));

            // Back floors 21-40: GemronIce group variants
            b[20] = SP.Pool(8, SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[21] = SP.Pool(8, SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[22] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[23] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[24] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25));
            b[25] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[26] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            b[27] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[28] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25));
            b[29] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[30] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            b[31] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[32] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[33] = SP.Pool(8, SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));
            b[34] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.MimicDS.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[35] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.GemronIce.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[36] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.WitchHellzaEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25));
            b[37] = SP.Pool(8, SP.E(Enemies.CorceaEnhanced.TableIndex,25), SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.RockanoffEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));

            // Back floors 39-40 bridge gap (the two remaining GemronIce back slots)
            b[38] = SP.Pool(8, SP.E(Enemies.HornHead.TableIndex,25), SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.SteelGiantEnhanced.TableIndex,25));
            b[39] = SP.Pool(8, SP.E(Enemies.YammichEnhanced.TableIndex,25), SP.E(Enemies.ClubEnhanced.TableIndex,25), SP.E(Enemies.AuntieMeduEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDS.TableIndex,25));

            // Back floors 41-60: GemronThunder group variants
            b[40] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            b[41] = SP.Pool(8, SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[42] = SP.Pool(8, SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            b[43] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[44] = SP.Pool(8, SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[45] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25));
            b[46] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            b[47] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            b[48] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[49] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25));
            b[50] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            b[51] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            b[52] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[53] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            b[54] = SP.Pool(8, SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25));
            b[55] = SP.Pool(8, SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[56] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GemronThunder.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[57] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.SpadeEnhanced.TableIndex,25), SP.E(Enemies.RashDasherEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25));
            b[58] = SP.Pool(8, SP.E(Enemies.CaptainEnhanced.TableIndex,25), SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GolEnhanced.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));
            b[59] = SP.Pool(8, SP.E(Enemies.CaveBatEnhanced.TableIndex,25), SP.E(Enemies.GyonEnhanced.TableIndex,25), SP.E(Enemies.BishopQ.TableIndex,25), SP.E(Enemies.MaskOfPrajnaEnhanced.TableIndex,25));

            // Back floors 61-80: GemronWind group variants
            b[60] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            b[61] = SP.Pool(8, SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[62] = SP.Pool(8, SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            b[63] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[64] = SP.Pool(8, SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[65] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25));
            b[66] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            b[67] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            b[68] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[69] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25));
            b[70] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            b[71] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            b[72] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[73] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            b[74] = SP.Pool(8, SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25));
            b[75] = SP.Pool(8, SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedTwice.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[76] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.GemronWind.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[77] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.SilverGear.TableIndex,25), SP.E(Enemies.HeartEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25));
            b[78] = SP.Pool(8, SP.E(Enemies.BomberHeadEnhanced.TableIndex,25), SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.PiratesChariotEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));
            b[79] = SP.Pool(8, SP.E(Enemies.CrabbyHermitEnhanced.TableIndex,25), SP.E(Enemies.CursedRoseEnhanced.TableIndex,25), SP.E(Enemies.SpaceGyonEnhanced.TableIndex,25), SP.E(Enemies.AlexanderEnhanced.TableIndex,25));

            // Back floors 81-99: GemronHoly group variants
            b[80] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[81] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            b[82] = SP.Pool(8, SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[83] = SP.Pool(8, SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            b[84] = SP.Pool(8, SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[85] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25));
            b[86] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            b[87] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            b[88] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[89] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25));
            b[90] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            b[91] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            b[92] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[93] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            b[94] = SP.Pool(8, SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25));
            b[95] = SP.Pool(8, SP.E(Enemies.LichEnhanced.TableIndex,25), SP.E(Enemies.MimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.KingMimicDSEnhancedThrice.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[96] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.GemronHoly.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));
            b[97] = SP.Pool(8, SP.E(Enemies.LivingArmorEnhanced.TableIndex,25), SP.E(Enemies.StatueDogEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.GaciousEnhanced.TableIndex,25));
            b[98] = SP.Pool(8, SP.E(Enemies.EvilBatEnhanced.TableIndex,25), SP.E(Enemies.TitanEnhanced.TableIndex,25), SP.E(Enemies.JokerEnhanced.TableIndex,25), SP.E(Enemies.CrescentBaronEnhanced.TableIndex,25));

            return b;
        }
    }

    /// <summary>
    /// Dungeon metadata backed by binary-extracted spawn pools.
    /// Front[0] is the dungeon descriptor pool (used before floor 1 loads).
    /// Front[N] is floor N's front-side pool.  Back[N] is floor N's back-side pool.
    /// </summary>
    internal struct DungeonData
    {
        internal byte             Id;
        internal string           Name;
        internal FloorSpawnPool[] Front;  // Front[0]=descriptor, Front[N]=floor N
        internal FloorSpawnPool[] Back;   // Back[N]=back for floor N (shorter than Front)
    }

    /// <summary>
    /// Per-dungeon spawn pool lookup, keyed by dungeon ID byte.
    /// Dungeon IDs: 0=Divine Beast Cave, 1=Wise Owl, 2=Shipwreck,
    ///              3=Sun and Moon, 4=Moon Sea, 5=Gallery of Time, 6=Demon Shaft.
    /// </summary>
    internal static class Dungeons
    {
        internal static readonly DungeonData DivineBeastCave = new DungeonData
        {
            Id = 0, Name = "Divine Beast Cave",
            Front = SpawnPoolData.DBC_Front,
            Back  = SpawnPoolData.DBC_Back,
        };

        internal static readonly DungeonData WiseOwlForest = new DungeonData
        {
            Id = 1, Name = "Wise Owl Forest",
            Front = SpawnPoolData.WOF_Front,
            Back  = SpawnPoolData.WOF_Back,
        };

        internal static readonly DungeonData Shipwreck = new DungeonData
        {
            Id = 2, Name = "Shipwreck",
            Front = SpawnPoolData.SW_Front,
            Back  = SpawnPoolData.SW_Back,
        };

        internal static readonly DungeonData SunAndMoonTemple = new DungeonData
        {
            Id = 3, Name = "Sun and Moon Temple",
            Front = SpawnPoolData.SMT_Front,
            Back  = SpawnPoolData.SMT_Back,
        };

        internal static readonly DungeonData MoonSea = new DungeonData
        {
            Id = 4, Name = "Moon Sea",
            Front = SpawnPoolData.MS_Front,
            Back  = SpawnPoolData.MS_Back,
        };

        internal static readonly DungeonData GalleryOfTime = new DungeonData
        {
            Id = 5, Name = "Gallery of Time",
            Front = SpawnPoolData.GoT_Front,
            Back  = SpawnPoolData.GoT_Back,
        };

        internal static readonly DungeonData DemonShaft = new DungeonData
        {
            Id = 6, Name = "Demon Shaft",
            Front = SpawnPoolData.DS_Front,
            Back  = SpawnPoolData.DS_Back,
        };

        private static readonly Dictionary<byte, DungeonData> ById;
        static Dungeons()
        {
            DungeonData[] all = { DivineBeastCave, WiseOwlForest, Shipwreck, SunAndMoonTemple, MoonSea, GalleryOfTime, DemonShaft };
            ById = new Dictionary<byte, DungeonData>(all.Length);
            foreach (DungeonData d in all) ById[d.Id] = d;
        }

        internal static bool TryGetValue(byte dungeonId, out DungeonData data) => ById.TryGetValue(dungeonId, out data);
    }
}
