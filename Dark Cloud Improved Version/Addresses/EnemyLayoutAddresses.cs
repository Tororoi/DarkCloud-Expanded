namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Per-dungeon / per-floor enemy spawn layout tables — the data that decides WHICH enemy
    /// species (and therefore which model + AI) spawn on each floor of each dungeon.
    ///
    /// Confirmed from ELF binary analysis 2026-06-07 by disassembling BtLoadMonstor__Fi (dun.bin).
    ///
    /// Reference chain (how a dungeon floor populates its enemy slots):
    ///   BtEnemyLayoutList    @ 0x002917B0  — 7 dungeon pointers (NORMAL floors)
    ///   BtUraEnemyLayoutList @ 0x002917D0  — 7 dungeon pointers (URA / back floors; 裏 = "back")
    ///       │ index by dungeon number ([gp − 0x625C])
    ///       ▼
    ///   BtEnemyLayout0N / BtUraEnemyLayout0N  — per-dungeon array, one 0x70-byte block per floor
    ///       │ + floor * 0x70
    ///       ▼
    ///   Floor block = 9 entries × 0x0C bytes (see EnemyLayoutEntry below)
    ///       │ for each entry whose Id (+0x4) != -1:
    ///       ▼
    ///   CMonstorUnit::SetupBaseModel(this, _, enemyId, 0x26, MonstorModelBuffer)  (ELF 0x001DFE90)
    ///       │ enemyId → packed species record (EnemySpeciesTable @ 0x0027FB00) → ModelCode "eNNa"/"cNNx"
    ///       ▼
    ///   loads mesh into MonstorModelBuffer (PS2 0x01F066D0) and behavior/AI script into
    ///   MonstorScriptBuffer (PS2 0x01F066E0). Both the live enemy slot array (0x21E16BA0) and the
    ///   ModelScale table (0x21E18530) are sub-arrays of one global, MainMonstorUnit (PS2 0x01DF87D0,
    ///   size 0x60750). BtArrengeMonstor__Fv wires each of the 16 slots to a 0x10-byte script entry.
    ///
    /// IMPORTANT — CORRECTED 2026-06-19: the value at entry +0x4 is the physical species-table ROW INDEX
    /// (EnemyDefaults.TableIndex), NOT the internal enemy id. Confirmed two ways: (1) BtLoadMonstor passes
    /// +0x4 straight to SetupBaseModel's tableIndex arg; (2) a raw ELF dump of DBC floor 0 has +0x4 = 52/1/3
    /// = the TableIndex of CaveBat(Id 60)/SkeletonSoldier(Id 3)/Dasher(Id 6), i.e. TableIndex not Id. This is
    /// why SetSpawnRosterMix correctly writes EnemyDefaults.TableIndex here. (The prior note claiming this was
    /// EnemyDefaults.Id was wrong.) Always resolve via EnemySpeciesTable / EnemyData.cs.
    ///
    /// Full decoded rosters for all 7 dungeons (normal + Ura) are in /enemy-spawn-layout.md at repo root.
    /// </summary>
    internal static class BtEnemyLayout
    {
        // ── Master pointer tables (PS2-native = file-resident in the main ELF segment) ──
        // PCSX2 address = native + 0x20000000.  ELF file offset = native − 0x100000 + 0x100.
        internal const int EnemyLayoutListBase    = 0x002917B0; // 7 × 4-byte ptrs → BtEnemyLayout00..06   (normal floors)
        internal const int UraEnemyLayoutListBase = 0x002917D0; // 7 × 4-byte ptrs → BtUraEnemyLayout00..06 (back floors)
        internal const int DungeonCount           = 7;

        // ── Floor block geometry ──
        internal const int FloorStride = 0x70; // bytes per floor block
        internal const int EntryStride = 0x0C; // bytes per enemy entry
        internal const int EntriesPerFloor = 9; // max distinct species entries per floor (block tail at +0x6C, =1)

        // Per-dungeon layout symbol addresses (PS2-native) and floor counts, from ELF symbol sizes.
        // Index = dungeon number. Names are best-effort (dun\dNN*.cfg ordering); floor counts are authoritative.
        //                                     normal       ura          floors  dungeon
        // [0] BtEnemyLayout00 / Ura00         0x002860D0   0x00286760    15      Divine Beast Cave (d01)
        // [1] BtEnemyLayout01 / Ura01         0x00286DF0   0x00287560    17      Wise Owl Forest (d02)
        // [2] BtEnemyLayout02 / Ura02         0x00287CD0   0x002884B0    18      Lake Gilna / Coastal (d03)
        // [3] BtEnemyLayout03 / Ura03         0x00288C90   0x00289470    18      Queens (d04)
        // [4] BtEnemyLayout04 / Ura04         0x00289C50   0x0028A2E0    15      Shipwreck (d05)
        // [5] BtEnemyLayout05 / Ura05         0x0028A970   0x0028B4D0    26      Muska Lacka / Sun & Moon (d06)
        // [6] BtEnemyLayout06 / Ura06         0x0028C030   0x0028EBF0   100      Moon Sea + Demon Shaft (d07)
        internal static readonly int[] LayoutBase    = { 0x002860D0, 0x00286DF0, 0x00287CD0, 0x00288C90, 0x00289C50, 0x0028A970, 0x0028C030 };
        internal static readonly int[] UraLayoutBase = { 0x00286760, 0x00287560, 0x002884B0, 0x00289470, 0x0028A2E0, 0x0028B4D0, 0x0028EBF0 };
        internal static readonly int[] FloorCount    = { 15, 17, 18, 18, 15, 26, 100 };

        /// <summary>PCSX2 address of a floor block's first entry. Pass a base from LayoutBase/UraLayoutBase.</summary>
        internal static int FloorAddress(int layoutBaseNative, int floor) =>
            (int)(layoutBaseNative + Memory.Pcsx2Base) + floor * FloorStride;

        /// <summary>PCSX2 address of entry <paramref name="entry"/> (0–8) within a floor block.</summary>
        internal static int EntryAddress(int layoutBaseNative, int floor, int entry) =>
            FloorAddress(layoutBaseNative, floor) + entry * EntryStride;

        // ── Entry field offsets (0x0C bytes per entry) ──
        // CONFIRMED 2026-06-19 by disassembling the ONLY pool reader, BtLoadMonstor__Fi (dun.bin overlay
        // @ 0x01DB9330): it loads BtEnemyLayoutList[dungeon] (0x2917B0) / Ura (0x2917D0), adds floor*0x70,
        // then walks entries 0..8 at stride 0xC reading ONLY +0x4 (Id), calling SetupBaseModel for each,
        // and BREAKING when +0x4 == -1. It never reads +0x0 or +0x8. A whole-binary scan (main ELF + dun
        // overlay) found no other reader of the 0x2860D0..0x291660 pool. So ONLY Id (+0x4) drives spawns.
        // +0x0: entry 0 holds a per-floor value (3/4/5/7/8, rising with depth — this is DungeonData's
        // "tier"); entries 1..8 are always 1. NOT read by any code → vestigial authoring metadata (matches
        // the 2026-06-09 test where overwriting it changed nothing). Floor population is fixed by spawn-point
        // generation at floor assembly, not by this table.
        internal const int Count    = 0x0; // int   — entry-0 = DungeonData "tier"; UNUSED at runtime (see note)
        // +0x4: species-table row index (= EnemyDefaults.TableIndex; NOT EnemyDefaults.Id — see the note
        // above). -1 = terminator/empty. This is the tableIndex SetupBaseModel loads. (Const kept named
        // `Id` to match the field's conventional name, but it holds a TableIndex.)
        internal const int Id       = 0x4; // int   — species TableIndex; -1 = unused/terminator
        // +0x8: spawn weight (percent); source data sums to ~100 per floor, but NOT read by the spawn path —
        // species selection is uniform rand%(distinct loaded species). No reader of +0x8 exists in the binary.
        internal const int Weight   = 0x8; // int   — spawn weight %; UNUSED at runtime (uniform pick)
    }

    /// <summary>
    /// Global state variables written by the boss-defeat function at ELF 0x0F5DF0.
    ///
    /// The game uses r28 (gp = PS2-native 0x01E00000) as the global data pointer.
    /// All addresses below are PCSX2 (= PS2-native + 0x20000000).
    ///
    /// Write order observed in the function (abbreviated):
    ///   1. 0x21DF94D8 — receives arg1 (boss slot pointer); first write in function
    ///   2. 0x21DF94E0 / 0x21DF9500 / 0x21DF94F8 / 0x21DF9510 / 0x21DF94EC / 0x21DF94F4 — dungeon exit state struct
    ///   3. jal BGM transition (PS2=0x0022BA00) — audio state changes before any C# poll can react
    ///   4. 0x21DF881C — dungeonClear flag (Dungeon.cs already resets this, but too late)
    ///   5. 0x21DF94E4 — written four separate times by the function
    ///   6. 0x21DF94FC — written to 1; likely the "boss defeated / trigger exit" flag
    ///   7. 0x21DF94E8 — cleared to 0
    ///   8. 0x21DF9504 — cleared to 0
    ///   9. 0x21D90408 — secondary write (r1 = 0x01D90000; base is separate from gp block)
    ///
    /// Key insight: all nine writes above happen inside a single MIPS frame.  No C# poller
    /// (even at 50 ms / ~3 frames) can observe and undo them before the engine processes the
    /// exit sequence.  The only reliable prevention is to null the on-death callback pointer
    /// inside the SpeciesDataPtr behavior block BEFORE the boss dies.
    ///
    /// The "boss defeated" exit trigger is believed to be <see cref="BossDefeatedFlag"/> (0x21DF94FC),
    /// since it is the only field explicitly set to 1 rather than copied from a computed value.
    /// Resetting DungeonClear alone is insufficient.
    /// </summary>
    internal static class BossDefeatState
    {
        // All addresses are PCSX2 (PS2-native + 0x20000000).

        // ── Boss slot state cluster (gp − 0x6B28 .. gp − 0x6AEC) ────────────────
        // This block is a contiguous struct. The full span is 0x21DF94D8–0x21DF9510.
        internal const int BossSlotPtr      = 0x21DF94D8; // arg1 passed in; first value written
        internal const int ExitState0       = 0x21DF94E0; // dungeon exit state field
        internal const int ExitStateE4      = 0x21DF94E4; // written four times with computed values
        internal const int ExitStateEC      = 0x21DF94EC; // dungeon exit state field
        internal const int ExitStateF4      = 0x21DF94F4; // dungeon exit state field
        internal const int ExitStateF8      = 0x21DF94F8; // dungeon exit state field
        internal const int ExitState00      = 0x21DF9500; // dungeon exit state field
        internal const int ExitState04      = 0x21DF9504; // cleared to 0
        internal const int ExitState10      = 0x21DF9510; // dungeon exit state field

        // ── "Boss defeated" / exit trigger ──────────────────────────────────────
        // Written to 1 (literal addiu r2,r0,1) late in the function; believed to be the
        // primary flag the engine polls to start the exit/ending sequence.
        // Resetting only DungeonClear (0x21DF881C) is NOT sufficient — this field must also
        // be kept at 0 to prevent the exit from triggering on non-boss floors.
        internal const int BossDefeatedFlag = 0x21DF94FC; // int — 0=normal, 1=boss defeated (triggers exit)

        // ── DungeonClear (separate gp block) ────────────────────────────────────
        // Written by the same defeat function.  Dungeon.cs already polls and resets this,
        // but the reset races against the engine reading it in the same or next frame.
        internal const int DungeonClear     = 0x21DF881C; // int — 0=in progress, nonzero=cleared (already handled by Dungeon.cs)

        // ── Secondary write (different base: r1 = 0x01D90000) ───────────────────
        internal const int Secondary0408    = 0x21D90408; // int — written near function end; purpose unknown
    }
}
