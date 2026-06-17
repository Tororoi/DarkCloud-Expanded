namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// EE RAM addresses for the dungeon floor / map system.
    ///
    /// Reverse-engineered from SCUS_971.11 (full .symtab). Convention: an ELF vaddr V maps to the
    /// PCSX2-readable EE address V + 0x20000000 (e.g. the global-int array at ELF 0x1D8FC80 is read
    /// at 0x21D8FC80). Globals below are given as EE addresses with their ELF symbol noted.
    ///
    /// Key engine routines (call sites / behavior decoded, addresses are ELF vaddrs):
    ///   ArrangementPos__12CMonstorUnitFP11CDungeonMapiii @0x1D7FC0
    ///       — places an enemy: picks a candidate cell via SearchiDoPutArea, then rejects it if it
    ///         lands on a treasure box / atra / trap circle (CheckTreasureBox/CheckAtra/CheckTrapCircle).
    ///   SearchiDoPutArea__FP8MAPPARTSiiiiPf @0x1C03C0
    ///       — the engine's "find a valid placement coordinate from the tiles" routine: indexes the
    ///         MAPPARTS grid as cell = base + row*320 + col*16, then converts the chosen cell to a world
    ///         position (float* out-param) via a 24-byte per-corner record (floats at +4..+24).
    ///   buildRandomMap__11CDungeonMap @0x1CB670 — random floor generation.
    ///   CCollisionMDT (GetPolygon/Intersection/PickUpNearPoly/GetMaxY @0x124EC0..) — walkable mesh.
    /// </summary>
    internal static class DungeonAddresses
    {
        /// <summary>Dungeon-map global pointers and instances (EE addresses).</summary>
        internal static class Map
        {
            /// <summary>int — currently loaded map/floor number. ELF map_no$895 @0x251F80.</summary>
            internal const long MapNo          = 0x20251F80;
            /// <summary>Pointer to the active MAPPARTS tile grid. ELF mapparts @0x2A27F0.
            /// Stores a PS2-native pointer; add 0x20000000 to read the target.</summary>
            internal const long MapPartsPtr    = 0x202A27F0;
            /// <summary>Pointer to the active CDungeonMap (Main or Ura). ELF NowDngMap @0x2A34B8.
            /// Stores a PS2-native pointer; add 0x20000000 to read the target.</summary>
            internal const long NowDngMapPtr   = 0x202A34B8;
            /// <summary>CDungeonMap instance for the front dungeon (size 0x10B10). ELF MainDungeonMap @0x1DC4BE0.</summary>
            internal const long MainDungeonMap = 0x21DC4BE0;
            /// <summary>CDungeonMap instance for the back/Ura dungeon (size 0x10B10). ELF UraDungeonMap @0x1DD56F0.</summary>
            internal const long UraDungeonMap  = 0x21DD56F0;

            /// <summary>Resolve a stored PS2-native pointer to a PCSX2-readable EE address (0 stays 0).</summary>
            internal static long Deref(uint storedPtr) => storedPtr == 0 ? 0 : (storedPtr < 0x20000000 ? storedPtr + 0x20000000 : storedPtr);
        }

        /// <summary>
        /// MAPPARTS — the floor's 2-D tile grid, as read by SearchiDoPutArea. Base address is the
        /// dereferenced <see cref="Map.MapPartsPtr"/>. Layout (confirmed from SearchiDoPutArea @0x1C03C0):
        /// each cell is 16 bytes; rows are 320 bytes apart, i.e. 20 columns per row
        /// (cell = base + row*RowStride + col*CellStride). The (row, col) indices are the int args
        /// passed to SearchiDoPutArea; the engine converts a chosen cell to world coords through a
        /// separate 24-byte float record (see class summary).
        /// </summary>
        internal static class MapPartsGrid
        {
            internal const int Columns    = 20;    // RowStride / CellStride
            internal const int RowStride  = 320;   // 0x140 — bytes between rows (= 20 cells * 16)
            internal const int CellStride = 16;    // 0x10  — bytes per tile cell

            /// <summary>Address of tile cell (row, col) given the MAPPARTS grid <paramref name="gridBase"/>.</summary>
            internal static long CellAddr(long gridBase, int row, int col) => gridBase + (long)row * RowStride + (long)col * CellStride;
        }

        /// <summary>
        /// Field offsets within a 16-byte MAPPARTS tile cell. SearchiDoPutArea reads +0 and +4 to
        /// decide whether a cell is a valid placement target; the remaining 8 bytes are unconfirmed.
        /// </summary>
        internal static class MapPartsCellOffsets
        {
            internal const int Field0 = 0x00; // int — tile descriptor word (gates placement validity)
            internal const int Field4 = 0x04; // int — secondary descriptor (combined with a per-map base index)
        }
    }
}
