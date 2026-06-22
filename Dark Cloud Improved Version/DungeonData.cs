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

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // BOSS DEFEAT CUTSCENE MECHANISM (ELF analysis — 2026-06-07)
    // Goal: understand what triggers the boss defeat cutscene so we can suppress it when a
    //       redirected boss (RedirectEnemyModel) dies on a regular non-boss floor.
    // ──────────────────────────────────────────────────────────────────────────────────────────
    //
    // GP REGISTER VALUE
    //   The ELF's $gp register is 0x21E00000 (PCSX2).  Confirmed by back-solving from the known
    //   dungeonClear address: sw v0, -30692(gp) stores to 0x21DF881C → gp = 0x21E00000.
    //
    // KEY ADDRESS: dungeonClear = 0x21DF881C  (gp − 30692, i.e. gp − 0x77E4)
    //   Already declared in Addresses.cs.  Holds 0xFFFFFFF1 (= −15) when the dungeon is
    //   considered cleared.  Normal in-dungeon value is something else (sample on floor entry).
    //
    // KEY ADDRESS: dungeonClearSource = 0x21DF8698  (gp − 31080, i.e. gp − 0x7968)
    //   The word that is READ and then written to dungeonClear when the boss defeat fires.
    //   Contains the "cleared" sentinel value (0xFFFFFFF1) once dungeon state is initialised.
    //
    // THE ONE WRITE (ELF offset 0x0F5E38, kseg0 0x801F5D38, PCSX2 0x201F5D38)
    //   There is exactly ONE sw instruction that writes to dungeonClear in the entire ELF:
    //     sw v0, −30692(gp)          ; stores dungeonClearSource value → dungeonClear
    //   It sits inside a "begin dungeon end sequence" function at ELF 0x0F5DF0 that also:
    //     • copies dungeon exit struct fields to GP-relative globals
    //     • calls audio fade/stop functions (jal 0x012B560, jal 0x012B920)
    //   Original instruction bytes (little-endian): 1C 88 82 AF
    //   NOP bytes: 00 00 00 00
    //
    // STATE MACHINE (ELF 0x072DD0 – 0x073028 function; dispatch table at kseg0 ~0x802A*)
    //   The dungeon combat loop is a switch on *[gp − 28820] (PCSX2 0x21DF8F6C), which we
    //   call battleState.  One specific case of that switch (reached when the game's combat
    //   manager detects the boss enemy HP reached 0) calls the boss defeat function:
    //     ELF 0x072F10 – 0x072F20 (the "boss defeated" case):
    //       addiu a1, zero, 1
    //       jal   0x01F5CF0          ; = ELF 0x0F5DF0, the dungeon-end function above
    //       beq   zero, zero, …      ; unconditional jump to function epilogue
    //   The jal has only ONE call site — this case in the switch.
    //
    // HOW THE BUG MANIFESTS
    //   RedirectEnemyModel() writes MinotaurJoe.Id (or another boss ID) into Dasher's
    //   EnemySpeciesId species-table field so that the enemy's in-game name is correct.
    //   When the redirected enemy dies, the combat manager's boss-HP check reads that species
    //   ID, recognises a boss, transitions battleState to the defeat case, and the cutscene
    //   fires regardless of which floor the player is on.
    //
    // FIX OPTIONS
    //
    //   Option A — C# monitor (no MIPS patching; simplest):
    //     On floor entry, snapshot dungeonClear (0x21DF881C).
    //     When a boss is redirected to a regular floor, start a tight-polling thread (~50 ms).
    //     If dungeonClear changes away from the snapshot value AND checkFloor (0x21CD954E)
    //     does not equal the current dungeon's canonical boss floor → write the snapshot back.
    //     Boss floors: DBC=14, WOF=17, SW=18, SMT=18, MS=15, GoT=special, DS=100.
    //     Risk: ~50 ms race window before the state machine advances past the write.
    //
    //   Option B — Runtime MIPS NOP patch via PINE:
    //     Write 0x00000000 to PCSX2 address 0x201F5D38 to silence the dungeonClear store.
    //     Apply the patch when a boss redirect is active on a non-boss floor; restore the
    //     original word (0xAF829D1C... verify: sw v0, −30692(gp) = 0xAF82_881C → bytes
    //     1C 88 82 AF) when back on a normal floor or redirect is cleared.
    //     This is race-free and surgical — only the clear is suppressed, not the whole
    //     cutscene sequence — so audio/rendering calls still run but the flag is never set.
    //     Note: 0xAF82881C in little-endian = bytes 1C 88 82 AF.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    // Enemy spawn pool data extracted from ELF /tmp/SCUS_971.11, file offset 0x1861D0.
    //
    // BINARY FORMAT (spawn pool table)
    // ---------------------------------
    // The table is a packed stream of 28-word (112-byte) fixed-stride pool records.
    // Exception: the very first record in the entire table has no [0, 1] header and is
    // 26 words; all subsequent records are 28 words.
    //
    // Standard record layout (28 words, little-endian 32-bit each) — corrected 2026-06-19 from a raw
    // ELF .data dump + BtLoadMonstor disassembly (the earlier "word0=0/word1=1/word2=tier" framing was a
    // misread):
    //   9 entries × 3 words (stride 0xC):  Count @+0x0,  Id @+0x4,  Weight @+0x8
    //   word 27 (tail) : 1
    //   The engine (BtLoadMonstor) walks entries 0..8 reading ONLY Id (+0x4) and stops at Id == -1.
    //   Count and Weight are never read:
    //     • Count (+0x0): entry 0 = the per-floor "tier" (3/4/5/7/8, rising with depth); entries 1..8 = 1.
    //     • Weight (+0x8): authored to sum to ~100/floor, but selection is uniform rand%(loaded species).
    //   Both are VESTIGIAL — only Id (+0x4) matters. (FloorSpawnPool now models just the Id list.)
    //
    // Id is the physical 0-based row index in the enemy species table (ELF offset 0x17FC54, stride 0x9C),
    // i.e. EnemyDefaults.TableIndex.  It is NOT the same as EnemyDefaults.Id.
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
    //   DS floor 100 padding  : tbl_166     → BlackKnightMount (garbage row, present in binary)
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

    // A floor's spawn pool, modeled as just the species that can spawn there (by TableIndex).
    //
    // The live BtEnemyLayout entry is 0xC bytes: Count @+0x0, Id @+0x4, Weight @+0x8. Disassembly of the
    // only pool reader (BtLoadMonstor, dun.bin 0x01DB9330) shows the engine reads ONLY Id (+0x4); it never
    // reads Count or Weight. So both of those are VESTIGIAL and dropped from this model:
    //   • Count (+0x0): entry 0 held the per-floor "tier" (3/4/5/7/8, rising with depth); entries 1..8 = 1.
    //     Unused at runtime — a leftover difficulty tag.
    //   • Weight (+0x8): source data summed to ~100/floor, but selection is uniform rand%(distinct loaded
    //     species), so weights never mattered.
    // TableIndex (= the Id field, +0x4) is the only thing that matters. (Formerly a Tier + SpawnEntry[]
    // of {TableIndex, Weight, Count}; see EnemyAddresses.BtEnemyLayout and the enemy-spawn-pool-reader notes.)
    internal struct FloorSpawnPool
    {
        internal int[] TableIndices;
        internal FloorSpawnPool(int[] tableIndices) { TableIndices = tableIndices; }
    }

    // Helper for terse pool construction: SP.Pool(id, id, ...) / SP.Empty().
    internal static class SP
    {
        internal static FloorSpawnPool Pool(params int?[] ids) =>
            new FloorSpawnPool(System.Array.ConvertAll(ids, x => x!.Value));
        internal static FloorSpawnPool Empty() =>
            new FloorSpawnPool(System.Array.Empty<int>());
    }

    /// <summary>
    /// Binary-extracted spawn pools for all 7 dungeons.
    ///
    /// INDEXING IS NOT UNIFORM ACROSS DUNGEONS — verify per dungeon against the live BtEnemyLayout
    /// (EnemyModelInjector RosterDump) before trusting the floor labels:
    ///   • DBC (VERIFIED 0-indexed): Front[N] = floor N, NO descriptor. Front length (15) ==
    ///     BtEnemyLayout.FloorCount[0] (15) and Front[0] holds real floor-0 spawn data.
    ///   • WOF/SW/SMT/MS/GoT/DS (UNVERIFIED): historically modeled as Front[0]=descriptor,
    ///     Front[N]=floor N. Their Front lengths do NOT equal the dungeon floor counts (WOF/SW/SMT
    ///     are +1, MS is short), and DungeonData's dungeon order does not line up 1:1 with
    ///     BtEnemyLayout's FloorCount indices, so whether [0] is a real empty floor-0 or a genuine
    ///     descriptor is still open. Dump each before relabeling.
    /// Back[N] is the back-side pool for floor N (floors that have no back — boss and GoT — have no
    /// back entry). This table is reference data only; nothing reads it at runtime today.
    /// </summary>
    internal static class SpawnPoolData
    {
        // ── Dungeon 0 : Divine Beast Cave (DBC) ────────────────────────────────────────
        // 15 floors, 0-indexed (0–14).  Boss: Dran (floor 14).  Event floors: 3, 7, 14.
        // Index [N] is floor N's pool — there is NO descriptor slot. Verified against the live
        // BtEnemyLayout RosterDump: array length (15) == BtEnemyLayout.FloorCount[0] (15), and the
        // old "descriptor" [0] holds real floor-0 spawn data (CaveBat/SkeletonSoldier/Dasher).
        // Front slots  0– 14  RAM 0x002860D0–0x002866F0
        // Back  slots 15– 28  RAM 0x00286760–0x00286D10

        internal static readonly FloorSpawnPool[] DBC_Front = new FloorSpawnPool[]
        {
            // [0] floor 0
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Dasher.TableIndex),
            // [1] floor 1
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Dasher.TableIndex, EnemySpecies.Yammich.TableIndex),
            // [2] floor 2
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Dasher.TableIndex, EnemySpecies.Statue.TableIndex, EnemySpecies.StatueDog.TableIndex),
            // [3] floor 3 — EVENT: skeleton encounter (SkeletonSoldier:100%)
            SP.Pool(EnemySpecies.SkeletonSoldier.TableIndex),
            // [4] floor 4
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Dasher.TableIndex, EnemySpecies.Statue.TableIndex, EnemySpecies.StatueDog.TableIndex, EnemySpecies.Yammich.TableIndex),
            // [5] floor 5
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Statue.TableIndex, EnemySpecies.MimicDBC.TableIndex),
            // [6] floor 6
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Ghost.TableIndex, EnemySpecies.Opar.TableIndex, EnemySpecies.MasterJacket.TableIndex),
            // [7] floor 7 — EVENT: story (no enemy spawns)
            SP.Empty(),
            // [8] floor 8
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Ghost.TableIndex, EnemySpecies.Statue.TableIndex, EnemySpecies.StatueDog.TableIndex, EnemySpecies.MimicDBC.TableIndex),
            // [9] floor 9
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Dasher.TableIndex, EnemySpecies.Ghost.TableIndex, EnemySpecies.MasterJacket.TableIndex, EnemySpecies.Rockanoff.TableIndex),
            // [10] floor 10
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Dasher.TableIndex, EnemySpecies.Opar.TableIndex, EnemySpecies.MasterJacket.TableIndex, EnemySpecies.Dragon.TableIndex),
            // [11] floor 11
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Statue.TableIndex, EnemySpecies.MasterJacket.TableIndex, EnemySpecies.KingMimicDBC.TableIndex, EnemySpecies.Rockanoff.TableIndex),
            // [12] floor 12
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Opar.TableIndex, EnemySpecies.MasterJacket.TableIndex, EnemySpecies.Rockanoff.TableIndex, EnemySpecies.Dragon.TableIndex),
            // [13] floor 13
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Ghost.TableIndex, EnemySpecies.KingMimicDBC.TableIndex, EnemySpecies.Rockanoff.TableIndex, EnemySpecies.Dragon.TableIndex),
            // [14] floor 14 — BOSS: Dran
            SP.Pool(EnemySpecies.Dran.TableIndex),
        };

        // Back-floor pools for DBC slots 0-13 (index N here = back for Front[N]).
        internal static readonly FloorSpawnPool[] DBC_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Ghost.TableIndex, EnemySpecies.Yammich.TableIndex),        // back for floor 0
            SP.Pool(EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Ghost.TableIndex),                      // back for floor 1
            SP.Pool(EnemySpecies.SkeletonSoldier.TableIndex, EnemySpecies.Ghost.TableIndex),                      // back for floor 2
            SP.Empty(),                                               // back for floor 3 (event)
            SP.Pool(EnemySpecies.MimicDBC.TableIndex, EnemySpecies.KingMimicDBC.TableIndex),                     // back for floor 4
            SP.Pool(EnemySpecies.Statue.TableIndex, EnemySpecies.StatueDog.TableIndex),                      // back for floor 5
            SP.Pool(EnemySpecies.Dasher.TableIndex, EnemySpecies.Rockanoff.TableIndex),                      // back for floor 6
            SP.Empty(),                                               // back for floor 7 (event)
            SP.Pool(EnemySpecies.CaveBat.TableIndex),                                  // back for floor 8
            SP.Pool(EnemySpecies.MimicDBC.TableIndex, EnemySpecies.KingMimicDBC.TableIndex, EnemySpecies.Rockanoff.TableIndex),        // back for floor 9
            SP.Pool(EnemySpecies.Ghost.TableIndex, EnemySpecies.MimicDBC.TableIndex, EnemySpecies.MasterJacket.TableIndex),         // back for floor 10
            SP.Pool(EnemySpecies.CaveBat.TableIndex, EnemySpecies.Dragon.TableIndex),                     // back for floor 11
            SP.Pool(EnemySpecies.MimicDBC.TableIndex, EnemySpecies.KingMimicDBC.TableIndex),                     // back for floor 12
            SP.Pool(EnemySpecies.Rockanoff.TableIndex),                                  // back for floor 13
        };

        // ── Dungeon 1 : Wise Owl Forest (WOF) ─────────────────────────────────────────────
        // 17 floors.  Boss: MasterUtan (floor 17).  Event floors: 8, 17.
        // NOTE: GetDungeonEventFloors currently returns 16 for the boss floor — should be 17.
        // Front slots 29– 46  RAM 0x00286D80–0x002874F0
        // Back  slots 47– 62  RAM 0x00287560–0x00287BF0

        internal static readonly FloorSpawnPool[] WOF_Front = new FloorSpawnPool[]
        {
            // [0] descriptor (empty)
            SP.Empty(),
            // [1] floor 1
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Saturday.TableIndex, EnemySpecies.Friday.TableIndex, EnemySpecies.Thursday.TableIndex, EnemySpecies.Wednesday.TableIndex),
            // [2] floor 2
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Tuesday.TableIndex, EnemySpecies.Monday.TableIndex, EnemySpecies.FliFli.TableIndex),
            // [3] floor 3
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Thursday.TableIndex, EnemySpecies.Sunday.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.KingPrickly.TableIndex),
            // [4] floor 4
            SP.Pool(EnemySpecies.Thursday.TableIndex, EnemySpecies.Sunday.TableIndex, EnemySpecies.FliFli.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.KingPrickly.TableIndex),
            // [5] floor 5
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Friday.TableIndex, EnemySpecies.Wednesday.TableIndex, EnemySpecies.MimicWOF.TableIndex, EnemySpecies.HaleyHoley.TableIndex),
            // [6] floor 6
            SP.Pool(EnemySpecies.Saturday.TableIndex, EnemySpecies.Thursday.TableIndex, EnemySpecies.FliFli.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.WitchIllza.TableIndex),
            // [7] floor 7
            SP.Pool(EnemySpecies.Wednesday.TableIndex, EnemySpecies.Tuesday.TableIndex, EnemySpecies.Monday.TableIndex, EnemySpecies.Sunday.TableIndex, EnemySpecies.WitchIllza.TableIndex),
            // [8] floor 8 — EVENT
            SP.Pool(EnemySpecies.FliFli.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.MimicWOF.TableIndex, EnemySpecies.WitchIllza.TableIndex, EnemySpecies.KingPrickly.TableIndex),
            // [9] floor 9
            SP.Empty(),
            // [10] floor 10
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.MimicWOF.TableIndex, EnemySpecies.EarthDigger.TableIndex, EnemySpecies.HaleyHoley.TableIndex),
            // [11] floor 11
            SP.Pool(EnemySpecies.FliFli.TableIndex, EnemySpecies.WitchIllza.TableIndex, EnemySpecies.EarthDigger.TableIndex, EnemySpecies.Halloween.TableIndex, EnemySpecies.HaleyHoley.TableIndex),
            // [12] floor 12
            SP.Pool(EnemySpecies.Hornet.TableIndex, EnemySpecies.WitchIllza.TableIndex, EnemySpecies.EarthDigger.TableIndex, EnemySpecies.KingMimicWOF.TableIndex),
            // [13] floor 13
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.FliFli.TableIndex, EnemySpecies.EarthDigger.TableIndex),
            // [14] floor 14
            SP.Pool(EnemySpecies.Sunday.TableIndex, EnemySpecies.WitchIllza.TableIndex, EnemySpecies.Halloween.TableIndex, EnemySpecies.Werewolf.TableIndex),
            // [15] floor 15
            SP.Pool(EnemySpecies.Thursday.TableIndex, EnemySpecies.Hornet.TableIndex, EnemySpecies.Halloween.TableIndex, EnemySpecies.KingMimicWOF.TableIndex, EnemySpecies.KingPrickly.TableIndex),
            // [16] floor 16
            SP.Pool(EnemySpecies.WitchIllza.TableIndex, EnemySpecies.EarthDigger.TableIndex, EnemySpecies.Halloween.TableIndex, EnemySpecies.KingMimicWOF.TableIndex, EnemySpecies.Werewolf.TableIndex),
            // [17] floor 17 — BOSS: MasterUtan
            SP.Pool(EnemySpecies.MasterUtan.TableIndex),
        };

        // Back-floor pools for WOF floors 1-16 (index N here = back for floor N).
        internal static readonly FloorSpawnPool[] WOF_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.Hornet.TableIndex),                                // back for floor 1
            SP.Pool(EnemySpecies.EarthDigger.TableIndex),                                            // back for floor 2
            SP.Pool(EnemySpecies.FliFli.TableIndex, EnemySpecies.WitchIllza.TableIndex),                               // back for floor 3
            SP.Pool(EnemySpecies.MimicWOF.TableIndex, EnemySpecies.KingMimicWOF.TableIndex),                              // back for floor 4
            SP.Pool(EnemySpecies.FliFli.TableIndex, EnemySpecies.WitchIllza.TableIndex, EnemySpecies.Halloween.TableIndex),                  // back for floor 5
            SP.Pool(EnemySpecies.Thursday.TableIndex, EnemySpecies.Tuesday.TableIndex, EnemySpecies.Monday.TableIndex, EnemySpecies.Sunday.TableIndex),   // back for floor 6
            SP.Pool(EnemySpecies.EarthDigger.TableIndex, EnemySpecies.KingPrickly.TableIndex),                              // back for floor 7
            SP.Pool(EnemySpecies.MimicWOF.TableIndex, EnemySpecies.KingMimicWOF.TableIndex),                              // back for floor 8
            SP.Empty(),                                                        // back for floor 9
            SP.Pool(EnemySpecies.Werewolf.TableIndex),                                            // back for floor 10
            SP.Pool(EnemySpecies.Halloween.TableIndex),                                            // back for floor 11
            SP.Pool(EnemySpecies.CannibalPlant.TableIndex, EnemySpecies.FliFli.TableIndex),                               // back for floor 12
            SP.Pool(EnemySpecies.Thursday.TableIndex, EnemySpecies.Tuesday.TableIndex, EnemySpecies.Monday.TableIndex, EnemySpecies.Sunday.TableIndex, EnemySpecies.Halloween.TableIndex), // back for floor 13
            SP.Pool(EnemySpecies.EarthDigger.TableIndex),                                            // back for floor 14
            SP.Pool(EnemySpecies.MimicWOF.TableIndex, EnemySpecies.KingMimicWOF.TableIndex),                              // back for floor 15
            SP.Pool(EnemySpecies.Saturday.TableIndex, EnemySpecies.Friday.TableIndex, EnemySpecies.Thursday.TableIndex, EnemySpecies.Wednesday.TableIndex),   // back for floor 16
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
            SP.Empty(),
            // [1] floor 1
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gunny.TableIndex),
            // [2] floor 2
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gunny.TableIndex, EnemySpecies.CursedRose.TableIndex),
            // [3] floor 3
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gunny.TableIndex, EnemySpecies.CursedRose.TableIndex),
            // [4] floor 4
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gunny.TableIndex, EnemySpecies.CursedRose.TableIndex, EnemySpecies.MimicSW.TableIndex),
            // [5] floor 5
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gunny.TableIndex, EnemySpecies.MimicSW.TableIndex),
            // [6] floor 6
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.CursedRose.TableIndex, EnemySpecies.MimicSW.TableIndex, EnemySpecies.Gyon.TableIndex),
            // [7] floor 7
            SP.Pool(EnemySpecies.Gunny.TableIndex, EnemySpecies.CursedRose.TableIndex, EnemySpecies.Gyon.TableIndex, EnemySpecies.Sam.TableIndex),
            // [8] floor 8 — EVENT
            SP.Pool(EnemySpecies.MimicSW.TableIndex, EnemySpecies.Gyon.TableIndex, EnemySpecies.Sam.TableIndex, EnemySpecies.Captain.TableIndex),
            // [9] floor 9
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.PiratesChariot.TableIndex),
            // [10] floor 10
            SP.Pool(EnemySpecies.Gunny.TableIndex, EnemySpecies.CursedRose.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.PiratesChariot.TableIndex),
            // [11] floor 11
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Gyon.TableIndex, EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.KingMimicSW.TableIndex),
            // [12] floor 12
            SP.Pool(EnemySpecies.CursedRose.TableIndex, EnemySpecies.MimicSW.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.KingMimicSW.TableIndex),
            // [13] floor 13
            SP.Pool(EnemySpecies.MimicSW.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.AuntieMedu.TableIndex),
            // [14] floor 14
            SP.Pool(EnemySpecies.Sam.TableIndex, EnemySpecies.KingMimicSW.TableIndex, EnemySpecies.AuntieMedu.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),
            // [15] floor 15
            SP.Pool(EnemySpecies.Gyon.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.AuntieMedu.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),
            // [16] floor 16
            SP.Pool(EnemySpecies.CursedRose.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),
            // [17] floor 17
            SP.Pool(EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.KingMimicSW.TableIndex, EnemySpecies.AuntieMedu.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),
            // [18] floor 18 — BOSS: IceQueen + companions (kori, i_me, i_ta, c17_, e124)
            SP.Pool(EnemySpecies.IceQueen.TableIndex, EnemySpecies.IQComp101.TableIndex, EnemySpecies.IceArrow.TableIndex, EnemySpecies.IQComp102.TableIndex, EnemySpecies.IQComp103.TableIndex, EnemySpecies.SWComp92.TableIndex, EnemySpecies.IQComp104.TableIndex),
        };

        internal static readonly FloorSpawnPool[] SW_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.Gunny.TableIndex, EnemySpecies.Gyon.TableIndex),                             // back for floor 1
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Captain.TableIndex),                             // back for floor 2
            SP.Pool(EnemySpecies.CursedRose.TableIndex, EnemySpecies.Captain.TableIndex),                             // back for floor 3
            SP.Pool(EnemySpecies.Sam.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),                             // back for floor 4
            SP.Pool(EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),                             // back for floor 5
            SP.Pool(EnemySpecies.MimicSW.TableIndex, EnemySpecies.KingMimicSW.TableIndex),                             // back for floor 6
            SP.Pool(EnemySpecies.CursedRose.TableIndex, EnemySpecies.AuntieMedu.TableIndex),                             // back for floor 7
            SP.Pool(EnemySpecies.Gyon.TableIndex, EnemySpecies.AuntieMedu.TableIndex),                             // back for floor 8
            SP.Empty(),                                                       // back for floor 9
            SP.Pool(EnemySpecies.MimicSW.TableIndex, EnemySpecies.KingMimicSW.TableIndex),                             // back for floor 10
            SP.Pool(EnemySpecies.Sam.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),                             // back for floor 11
            SP.Pool(EnemySpecies.PiratesChariot.TableIndex, EnemySpecies.MaskOfPrajna.TableIndex),                             // back for floor 12
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.CursedRose.TableIndex),                             // back for floor 13
            SP.Pool(EnemySpecies.MaskOfPrajna.TableIndex),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(EnemySpecies.Gyon.TableIndex, EnemySpecies.AuntieMedu.TableIndex),                             // back for floor 15
            SP.Pool(EnemySpecies.MimicSW.TableIndex, EnemySpecies.KingMimicSW.TableIndex),                             // back for floor 16
            SP.Pool(EnemySpecies.Corcea.TableIndex, EnemySpecies.Captain.TableIndex, EnemySpecies.PiratesChariot.TableIndex),               // back for floor 17
        };

        // ── Dungeon 3 : Sun & Moon Temple (SMT) ──────────────────────────────────────────
        // Front slots  99–117  RAM 0x00288C20–0x00289400
        // Back  slots 118–134  RAM 0x00289470–0x00289B70
        // 18 floors.  Boss: KingsCurseCoffin + PhaseEntity100 (floor 18).  Event floors: 8, 18.
        // NOTE: GetDungeonEventFloors currently returns 17 for the boss floor — should be 18.

        internal static readonly FloorSpawnPool[] SMT_Front = new FloorSpawnPool[]
        {
            SP.Empty(),                                                                                   // [0] descriptor
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.Phantom.TableIndex),                                                         // [1] floor 1
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex),                                            // [2] floor 2
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex, EnemySpecies.MimicSMT.TableIndex),                               // [3] floor 3
            SP.Pool(EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex, EnemySpecies.MimicSMT.TableIndex, EnemySpecies.Golem.TableIndex),                               // [4] floor 4
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.MimicSMT.TableIndex, EnemySpecies.Golem.TableIndex),                                            // [5] floor 5
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.Phantom.TableIndex, EnemySpecies.Golem.TableIndex, EnemySpecies.MrBlare.TableIndex),                               // [6] floor 6
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.MimicSMT.TableIndex, EnemySpecies.Golem.TableIndex, EnemySpecies.CrabbyHermit.TableIndex),                               // [7] floor 7
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.MrBlare.TableIndex),                                                         // [8] floor 8 — EVENT
            SP.Pool(EnemySpecies.Gol.TableIndex, EnemySpecies.Sil.TableIndex),                                                         // [9] floor 9
            SP.Pool(EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex, EnemySpecies.Golem.TableIndex, EnemySpecies.Dune.TableIndex),                               // [10] floor 10
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.CrabbyHermit.TableIndex, EnemySpecies.Dune.TableIndex),                                            // [11] floor 11
            SP.Pool(EnemySpecies.Mummy.TableIndex, EnemySpecies.Golem.TableIndex, EnemySpecies.Dune.TableIndex, EnemySpecies.KingMimicSMT.TableIndex),                               // [12] floor 12
            SP.Pool(EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex, EnemySpecies.MrBlare.TableIndex, EnemySpecies.Dune.TableIndex),                               // [13] floor 13
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.Golem.TableIndex, EnemySpecies.CrabbyHermit.TableIndex, EnemySpecies.KingMimicSMT.TableIndex, EnemySpecies.BlueDragon.TableIndex),                  // [14] floor 14
            SP.Pool(EnemySpecies.Phantom.TableIndex, EnemySpecies.BomberHead.TableIndex, EnemySpecies.Dune.TableIndex, EnemySpecies.SteelGiant.TableIndex),                               // [15] floor 15
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.KingMimicSMT.TableIndex),                                                         // [16] floor 16
            SP.Pool(EnemySpecies.Dune.TableIndex, EnemySpecies.KingMimicSMT.TableIndex, EnemySpecies.BlueDragon.TableIndex, EnemySpecies.SteelGiant.TableIndex),                               // [17] floor 17
            SP.Pool(EnemySpecies.KingsCurseCoffin.TableIndex, EnemySpecies.KingsCurse.TableIndex),                                                         // [18] floor 18 — BOSS: KingsCurseCoffin + PhaseEntity100
        };

        internal static readonly FloorSpawnPool[] SMT_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.Golem.TableIndex),                                          // back for floor 1
            SP.Pool(EnemySpecies.Phantom.TableIndex, EnemySpecies.CrabbyHermit.TableIndex, EnemySpecies.Dune.TableIndex),                // back for floor 2
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.KingMimicSMT.TableIndex),                             // back for floor 3
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.MrBlare.TableIndex),                             // back for floor 4
            SP.Pool(EnemySpecies.Golem.TableIndex, EnemySpecies.Dune.TableIndex, EnemySpecies.SteelGiant.TableIndex),               // back for floor 5
            SP.Pool(EnemySpecies.CrabbyHermit.TableIndex, EnemySpecies.Dune.TableIndex),                             // back for floor 6
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.MrBlare.TableIndex),                             // back for floor 7
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.KingMimicSMT.TableIndex),                             // back for floor 8
            SP.Empty(),                                                       // back for floor 9
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.SteelGiant.TableIndex),                             // back for floor 10
            SP.Pool(EnemySpecies.BomberHead.TableIndex),                                          // back for floor 11
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.SteelGiant.TableIndex),                             // back for floor 12
            SP.Pool(EnemySpecies.BomberHead.TableIndex, EnemySpecies.MrBlare.TableIndex),                             // back for floor 13
            SP.Pool(EnemySpecies.Mummy.TableIndex),                                          // back for floor 14  [tier 5, not 7]
            SP.Pool(EnemySpecies.Phantom.TableIndex),                                          // back for floor 15
            SP.Pool(EnemySpecies.MimicSMT.TableIndex, EnemySpecies.KingMimicSMT.TableIndex),                             // back for floor 16
            SP.Pool(EnemySpecies.BlueDragon.TableIndex),                                          // back for floor 17
        };

        // ── Dungeon 4 : Moon Sea (MS) ─────────────────────────────────────────────────────
        // Front slots 135–150  RAM 0x00289BE0–0x0028A270
        // Back  slots 151–164  RAM 0x0028A2E0–0x0028A890
        // 15 floors.  Boss: MinotaurJoe + WineKeg (floor 15).  Event floors: 7, 15.
        // NOTE: GetDungeonEventFloors currently returns 14 for the boss floor — should be 15.

        internal static readonly FloorSpawnPool[] MS_Front = new FloorSpawnPool[]
        {
            SP.Empty(),                                                                      // [0] descriptor
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MoonBug.TableIndex, EnemySpecies.WitchHellza.TableIndex),                               // [1] floor 1
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MoonBug.TableIndex, EnemySpecies.WitchHellza.TableIndex, EnemySpecies.SpaceGyon.TableIndex),                  // [2] floor 2
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MoonBug.TableIndex, EnemySpecies.SpaceGyon.TableIndex, EnemySpecies.MimicMS.TableIndex),                  // [3] floor 3
            SP.Pool(EnemySpecies.MoonBug.TableIndex, EnemySpecies.WitchHellza.TableIndex, EnemySpecies.SpaceGyon.TableIndex, EnemySpecies.MimicMS.TableIndex),                  // [4] floor 4
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MoonDigger.TableIndex),                                             // [5] floor 5
            SP.Pool(EnemySpecies.WitchHellza.TableIndex, EnemySpecies.SpaceGyon.TableIndex, EnemySpecies.MimicMS.TableIndex, EnemySpecies.WhiteFang.TableIndex),                  // [6] floor 6
            SP.Pool(EnemySpecies.MoonBug.TableIndex, EnemySpecies.MoonDigger.TableIndex, EnemySpecies.WhiteFang.TableIndex, EnemySpecies.Vulcan.TableIndex),                  // [7] floor 7 — EVENT
            SP.Empty(),                                                                      // [8] floor 8
            SP.Pool(EnemySpecies.SpaceGyon.TableIndex, EnemySpecies.MoonDigger.TableIndex, EnemySpecies.Vulcan.TableIndex, EnemySpecies.KingMimicMS.TableIndex),                  // [9] floor 9
            SP.Pool(EnemySpecies.MoonDigger.TableIndex, EnemySpecies.WhiteFang.TableIndex, EnemySpecies.Vulcan.TableIndex, EnemySpecies.Titan.TableIndex),                  // [10] floor 10
            SP.Pool(EnemySpecies.MoonBug.TableIndex, EnemySpecies.Titan.TableIndex),                                             // [11] floor 11
            SP.Pool(EnemySpecies.WitchHellza.TableIndex, EnemySpecies.Vulcan.TableIndex, EnemySpecies.Titan.TableIndex, EnemySpecies.CrescentBaron.TableIndex),                  // [12] floor 12
            SP.Pool(EnemySpecies.MoonDigger.TableIndex, EnemySpecies.Vulcan.TableIndex, EnemySpecies.CrescentBaron.TableIndex, EnemySpecies.Arthur.TableIndex),                  // [13] floor 13
            SP.Pool(EnemySpecies.KingMimicMS.TableIndex, EnemySpecies.Titan.TableIndex, EnemySpecies.CrescentBaron.TableIndex, EnemySpecies.Arthur.TableIndex),                  // [14] floor 14
            SP.Pool(EnemySpecies.MinotaurJoe.TableIndex, EnemySpecies.WineKeg.TableIndex),                                             // [15] floor 15 — BOSS: MinotaurJoe + WineKeg
        };

        internal static readonly FloorSpawnPool[] MS_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.MoonDigger.TableIndex),                                          // back for floor 1
            SP.Pool(EnemySpecies.Titan.TableIndex, EnemySpecies.Arthur.TableIndex),                             // back for floor 2
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.CrescentBaron.TableIndex),                             // back for floor 3
            SP.Pool(EnemySpecies.WitchHellza.TableIndex, EnemySpecies.Titan.TableIndex),                             // back for floor 4
            SP.Pool(EnemySpecies.MoonBug.TableIndex),                                          // back for floor 5
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MimicMS.TableIndex, EnemySpecies.KingMimicMS.TableIndex),               // back for floor 6
            SP.Pool(EnemySpecies.MoonBug.TableIndex, EnemySpecies.CrescentBaron.TableIndex),                             // back for floor 7
            SP.Empty(),                                                       // back for floor 8
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MoonDigger.TableIndex),                             // back for floor 9
            SP.Pool(EnemySpecies.Vulcan.TableIndex),                                          // back for floor 10
            SP.Pool(EnemySpecies.Arthur.TableIndex),                                          // back for floor 11
            SP.Pool(EnemySpecies.SpaceGyon.TableIndex),                                          // back for floor 12
            SP.Pool(EnemySpecies.HellPockle.TableIndex, EnemySpecies.MimicMS.TableIndex, EnemySpecies.KingMimicMS.TableIndex),               // back for floor 13
            SP.Pool(EnemySpecies.WitchHellza.TableIndex, EnemySpecies.WhiteFang.TableIndex, EnemySpecies.CrescentBaron.TableIndex),               // back for floor 14  [tier 5, not 7]
        };

        // ── Dungeon 5 : Gallery of Time (GoT) ────────────────────────────────────────────
        // 24 floors, no boss.  No event floors recorded in code.
        // The DarkGenie boss pool (see DS section) is stored in the binary immediately after
        // GoT's last front floor, before GoT's back floors.
        // Front slots 165–189  RAM 0x0028A900–0x0028B380
        // Back  slots 192–215  RAM 0x0028B4D0–0x0028BEE0  (slots 190–191 = DarkGenie anomaly)

        internal static readonly FloorSpawnPool[] GoT_Front = new FloorSpawnPool[]
        {
            SP.Empty(),                                                                                    // [0] descriptor
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.CurseDancer.TableIndex, EnemySpecies.DarkFlower.TableIndex),                                             // [1] floor 1
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.CurseDancer.TableIndex, EnemySpecies.DarkFlower.TableIndex, EnemySpecies.Billy.TableIndex),                                // [2] floor 2
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.DarkFlower.TableIndex, EnemySpecies.Billy.TableIndex, EnemySpecies.LivingArmor.TableIndex),                                // [3] floor 3
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.CurseDancer.TableIndex, EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex),                                // [4] floor 4
            SP.Pool(EnemySpecies.CurseDancer.TableIndex, EnemySpecies.DarkFlower.TableIndex, EnemySpecies.LivingArmor.TableIndex, EnemySpecies.RashDasher.TableIndex),                                // [5] floor 5
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.Billy.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Club.TableIndex),                                // [6] floor 6
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.RashDasher.TableIndex, EnemySpecies.Heart.TableIndex),                                // [7] floor 7
            SP.Pool(EnemySpecies.DarkFlower.TableIndex, EnemySpecies.Billy.TableIndex, EnemySpecies.LivingArmor.TableIndex, EnemySpecies.Diamond.TableIndex),                                // [8] floor 8
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.DarkFlower.TableIndex, EnemySpecies.RashDasher.TableIndex, EnemySpecies.Spade.TableIndex),                                // [9] floor 9
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.RashDasher.TableIndex, EnemySpecies.Joker.TableIndex),                                // [10] floor 10
            SP.Pool(EnemySpecies.Club.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.Diamond.TableIndex, EnemySpecies.Spade.TableIndex, EnemySpecies.Joker.TableIndex),                   // [11] floor 11
            SP.Pool(EnemySpecies.RashDasher.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.Joker.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),                                // [12] floor 12
            SP.Pool(EnemySpecies.RashDasher.TableIndex, EnemySpecies.Club.TableIndex, EnemySpecies.Diamond.TableIndex, EnemySpecies.Lich.TableIndex),                                // [13] floor 13
            SP.Pool(EnemySpecies.DarkFlower.TableIndex, EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Lich.TableIndex, EnemySpecies.Blizzard.TableIndex),                                // [14] floor 14
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.Lich.TableIndex, EnemySpecies.Blizzard.TableIndex, EnemySpecies.Alexander.TableIndex),                                // [15] floor 15
            SP.Pool(EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Lich.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),                                // [16] floor 16
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.Blizzard.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),                                // [17] floor 17
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),                                             // [18] floor 18
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.CurseDancer.TableIndex, EnemySpecies.Billy.TableIndex, EnemySpecies.Lich.TableIndex),                                // [19] floor 19
            SP.Pool(EnemySpecies.DarkFlower.TableIndex, EnemySpecies.LivingArmor.TableIndex, EnemySpecies.RashDasher.TableIndex, EnemySpecies.BlackDragon.TableIndex),                                // [20] floor 20
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.Billy.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.Blizzard.TableIndex),                                // [21] floor 21
            SP.Pool(EnemySpecies.Joker.TableIndex, EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),                                // [22] floor 22
            SP.Pool(EnemySpecies.Club.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.Diamond.TableIndex, EnemySpecies.Spade.TableIndex, EnemySpecies.Joker.TableIndex),                   // [23] floor 23
            SP.Pool(EnemySpecies.Lich.TableIndex, EnemySpecies.Blizzard.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),                                // [24] floor 24
        };

        // Back-floor pools for GoT floors 1-24.
        internal static readonly FloorSpawnPool[] GoT_Back = new FloorSpawnPool[]
        {
            SP.Pool(EnemySpecies.Heart.TableIndex, EnemySpecies.Lich.TableIndex),                             // back for floor 1
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.Alexander.TableIndex),                             // back for floor 2
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.BlackDragon.TableIndex),                             // back for floor 3
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.DarkFlower.TableIndex),                             // back for floor 4
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Alexander.TableIndex),  // back for floor 5
            SP.Pool(EnemySpecies.RashDasher.TableIndex, EnemySpecies.Blizzard.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),  // back for floor 6
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.Alexander.TableIndex),                             // back for floor 7
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.BlackDragon.TableIndex),                             // back for floor 8
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.Alexander.TableIndex),                             // back for floor 9
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.DarkFlower.TableIndex),                             // back for floor 10
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Alexander.TableIndex),  // back for floor 11
            SP.Pool(EnemySpecies.RashDasher.TableIndex, EnemySpecies.Blizzard.TableIndex, EnemySpecies.Alexander.TableIndex, EnemySpecies.BlackDragon.TableIndex),  // back for floor 12
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.BlackDragon.TableIndex),                             // back for floor 13  [tier 5, not 7]
            SP.Pool(EnemySpecies.CurseDancer.TableIndex, EnemySpecies.Billy.TableIndex),                             // back for floor 14
            SP.Pool(EnemySpecies.LivingArmor.TableIndex, EnemySpecies.MimicGoT.TableIndex, EnemySpecies.KingMimicGoT.TableIndex, EnemySpecies.Alexander.TableIndex),  // back for floor 15
            SP.Pool(EnemySpecies.EvilBat.TableIndex),                                          // back for floor 16
            SP.Pool(EnemySpecies.EvilBat.TableIndex, EnemySpecies.DarkFlower.TableIndex),                             // back for floor 17
            SP.Pool(EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Diamond.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),               // back for floor 18
            SP.Pool(EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Club.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),               // back for floor 19
            SP.Pool(EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),               // back for floor 20
            SP.Pool(EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Spade.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),               // back for floor 21
            SP.Pool(EnemySpecies.MimicGoT.TableIndex, EnemySpecies.Joker.TableIndex, EnemySpecies.KingMimicGoT.TableIndex),               // back for floor 22
            SP.Pool(EnemySpecies.Club.TableIndex, EnemySpecies.Heart.TableIndex, EnemySpecies.Diamond.TableIndex, EnemySpecies.Spade.TableIndex, EnemySpecies.Joker.TableIndex), // back for floor 23
            SP.Pool(EnemySpecies.EvilBat.TableIndex),                                          // back for floor 24
        };

        // DarkGenie boss pool — belongs to DS but stored in the binary after GoT's last
        // front floor (slot 190) and before GoT's back floors.  The companion empty slot
        // (slot 191) occupies the back-floor position for this boss encounter.
        internal static readonly FloorSpawnPool GoT_DarkGenieBoss =
            SP.Pool(EnemySpecies.DarkGenie.TableIndex, EnemySpecies.DarkGenieForm2.TableIndex, EnemySpecies.RightHand.TableIndex, EnemySpecies.LeftHand.TableIndex,
                       EnemySpecies.DGComp88.TableIndex, EnemySpecies.DGComp89.TableIndex, EnemySpecies.DGComp90.TableIndex, EnemySpecies.DGComp93.TableIndex);

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
            p[0] = SP.Empty(); // descriptor

            // Floors 1-20: GemronFire group
            p[1]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[2]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            p[3]  = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[4]  = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[5]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            p[6]  = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[7]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[8]  = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            p[9]  = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[10] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[11] = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[12] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[13] = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[14] = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            p[15] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            p[16] = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[17] = SP.Pool(EnemySpecies.GemronFire.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            p[18] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex);
            p[19] = SP.Pool(EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            p[20] = SP.Pool(EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);

            // Floors 21-40: GemronIce group
            p[21] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex);
            p[22] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.HornHead.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            p[23] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            p[24] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[25] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex);
            p[26] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            p[27] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            p[28] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[29] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            p[30] = SP.Pool(EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            p[31] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.KingMimicDS.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[32] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[33] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            p[34] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.HornHead.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[35] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[36] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            p[37] = SP.Pool(EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[38] = SP.Pool(EnemySpecies.MimicDS.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            p[39] = SP.Pool(EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            p[40] = SP.Pool(EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex); // same as floor 37

            // Floors 41-60: GemronThunder group
            p[41] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex);
            p[42] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            p[43] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            p[44] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[45] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex);
            p[46] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.BishopQ.TableIndex);
            p[47] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            p[48] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[49] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            p[50] = SP.Pool(EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            p[51] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[52] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[53] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            p[54] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[55] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[56] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            p[57] = SP.Pool(EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[58] = SP.Pool(EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            p[59] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            p[60] = SP.Pool(EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);

            // Floors 61-80: GemronWind group
            p[61] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex);
            p[62] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            p[63] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            p[64] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[65] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex);
            p[66] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            p[67] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            p[68] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[69] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            p[70] = SP.Pool(EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            p[71] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[72] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[73] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            p[74] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[75] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[76] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            p[77] = SP.Pool(EnemySpecies.SilverGear.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[78] = SP.Pool(EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            p[79] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            p[80] = SP.Pool(EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);

            // Floors 81-99: GemronHoly group
            p[81] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex);
            p[82] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            p[83] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            p[84] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[85] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.JokerEnhanced.TableIndex);
            p[86] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            p[87] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            p[88] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[89] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            p[90] = SP.Pool(EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            p[91] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[92] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[93] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            p[94] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[95] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[96] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            p[97] = SP.Pool(EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            p[98] = SP.Pool(EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            p[99] = SP.Pool(EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);

            // Floor 100 — BOSS: BlackKnight (tbl_165) + garbage padding (tbl_166, as extracted from binary)
            p[100] = SP.Pool(EnemySpecies.BlackKnightMount.TableIndex, EnemySpecies.BlackKnight.TableIndex);

            return p;
        }

        private static FloorSpawnPool[] BuildDSBack()
        {
            // 99 back floors for DS floors 1-99 (boss at 100 has no back floor).
            var b = new FloorSpawnPool[99];

            // Back floors 1-20: GemronFire group variants
            b[0]  = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[1]  = SP.Pool(EnemySpecies.GemronFire.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            b[2]  = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex);
            b[3]  = SP.Pool(EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[4]  = SP.Pool(EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[5]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            b[6]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            b[7]  = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[8]  = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            b[9]  = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            b[10] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            b[11] = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[12] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            b[13] = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            b[14] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.Nikapous.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[15] = SP.Pool(EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[16] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.GemronFire.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);
            b[17] = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.MasterJacketEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.ArthurEnhanced.TableIndex);
            b[18] = SP.Pool(EnemySpecies.MummyEnhanced.TableIndex, EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.WhiteFangEnhanced.TableIndex, EnemySpecies.VulcanEnhanced.TableIndex);
            b[19] = SP.Pool(EnemySpecies.HalloweenEnhanced.TableIndex, EnemySpecies.DiamondEnhanced.TableIndex, EnemySpecies.SilEnhanced.TableIndex, EnemySpecies.Nikapous.TableIndex);

            // Back floors 21-40: GemronIce group variants
            b[20] = SP.Pool(EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[21] = SP.Pool(EnemySpecies.MimicDS.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[22] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[23] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[24] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex);
            b[25] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.HornHead.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[26] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            b[27] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[28] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex);
            b[29] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[30] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            b[31] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[32] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[33] = SP.Pool(EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);
            b[34] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.MimicDS.TableIndex, EnemySpecies.KingMimicDS.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[35] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.GemronIce.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[36] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.WitchHellzaEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex);
            b[37] = SP.Pool(EnemySpecies.CorceaEnhanced.TableIndex, EnemySpecies.HornHead.TableIndex, EnemySpecies.RockanoffEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);

            // Back floors 39-40 bridge gap (the two remaining GemronIce back slots)
            b[38] = SP.Pool(EnemySpecies.HornHead.TableIndex, EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.SteelGiantEnhanced.TableIndex);
            b[39] = SP.Pool(EnemySpecies.YammichEnhanced.TableIndex, EnemySpecies.ClubEnhanced.TableIndex, EnemySpecies.AuntieMeduEnhanced.TableIndex, EnemySpecies.KingMimicDS.TableIndex);

            // Back floors 41-60: GemronThunder group variants
            b[40] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            b[41] = SP.Pool(EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[42] = SP.Pool(EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            b[43] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[44] = SP.Pool(EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[45] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex);
            b[46] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            b[47] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            b[48] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[49] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex);
            b[50] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.BishopQ.TableIndex);
            b[51] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            b[52] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[53] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            b[54] = SP.Pool(EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex);
            b[55] = SP.Pool(EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.MimicDSEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[56] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GemronThunder.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[57] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.SpadeEnhanced.TableIndex, EnemySpecies.RashDasherEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex);
            b[58] = SP.Pool(EnemySpecies.CaptainEnhanced.TableIndex, EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GolEnhanced.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);
            b[59] = SP.Pool(EnemySpecies.CaveBatEnhanced.TableIndex, EnemySpecies.GyonEnhanced.TableIndex, EnemySpecies.BishopQ.TableIndex, EnemySpecies.MaskOfPrajnaEnhanced.TableIndex);

            // Back floors 61-80: GemronWind group variants
            b[60] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            b[61] = SP.Pool(EnemySpecies.SilverGear.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[62] = SP.Pool(EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            b[63] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[64] = SP.Pool(EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[65] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex);
            b[66] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            b[67] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            b[68] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[69] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex);
            b[70] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            b[71] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            b[72] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[73] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            b[74] = SP.Pool(EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex);
            b[75] = SP.Pool(EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedTwice.TableIndex, EnemySpecies.KingMimicDSEnhancedTwice.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[76] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.GemronWind.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[77] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.SilverGear.TableIndex, EnemySpecies.HeartEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex);
            b[78] = SP.Pool(EnemySpecies.BomberHeadEnhanced.TableIndex, EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.PiratesChariotEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);
            b[79] = SP.Pool(EnemySpecies.CrabbyHermitEnhanced.TableIndex, EnemySpecies.CursedRoseEnhanced.TableIndex, EnemySpecies.SpaceGyonEnhanced.TableIndex, EnemySpecies.AlexanderEnhanced.TableIndex);

            // Back floors 81-99: GemronHoly group variants
            b[80] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[81] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            b[82] = SP.Pool(EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[83] = SP.Pool(EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            b[84] = SP.Pool(EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[85] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex);
            b[86] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            b[87] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            b[88] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[89] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.JokerEnhanced.TableIndex);
            b[90] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            b[91] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            b[92] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[93] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            b[94] = SP.Pool(EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex);
            b[95] = SP.Pool(EnemySpecies.LichEnhanced.TableIndex, EnemySpecies.MimicDSEnhancedThrice.TableIndex, EnemySpecies.KingMimicDSEnhancedThrice.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[96] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.GemronHoly.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);
            b[97] = SP.Pool(EnemySpecies.LivingArmorEnhanced.TableIndex, EnemySpecies.StatueDogEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.GaciousEnhanced.TableIndex);
            b[98] = SP.Pool(EnemySpecies.EvilBatEnhanced.TableIndex, EnemySpecies.TitanEnhanced.TableIndex, EnemySpecies.JokerEnhanced.TableIndex, EnemySpecies.CrescentBaronEnhanced.TableIndex);

            return b;
        }
    }

    /// <summary>
    /// Dungeon metadata backed by binary-extracted spawn pools.
    /// Front[N]/Back[N] index a floor's spawn pools, but the floor↔index mapping is NOT uniform across
    /// dungeons — DBC is verified 0-indexed (Front[N]=floor N, no descriptor); the others are unverified
    /// (historically Front[0]=descriptor). See the SpawnPoolData summary before trusting floor labels.
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
