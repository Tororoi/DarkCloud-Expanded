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
            internal static long Deref(uint storedPtr) => storedPtr == 0 ? 0 : (storedPtr < Memory.Pcsx2Base ? storedPtr + Memory.Pcsx2Base : storedPtr);
        }

        /// <summary>
        /// MAPPARTS — the floor's 2-D tile grid used by random-map generation / enemy arrangement.
        /// REAL base: it is EMBEDDED in the CDungeonMap at +0x9C50 (from ArrangementPos @0x1D7FC0:
        /// a0 = CDungeonMap + 0x1c58 + 0x7ff8 = CDungeonMap + 0x9C50), so grid = <see cref="Map.NowDngMapPtr"/>
        /// deref + 0x9C50. The standalone `mapparts` global (<see cref="Map.MapPartsPtr"/>) is NULL in the
        /// Ice Queen fight. Layout (SearchiDoPutArea @0x1C03C0): 16-byte cells, 20 columns, rows 320 apart
        /// (cell = base + row*320 + col*16); cell→world is via a separate 24-byte float record (world = 10.0 × record[+4..+18]).
        ///
        /// IMPORTANT: when Ice Queen is spawned the floor uses her PREBUILT ARENA (map_no=800) and this tile
        /// grid is wiped to all -1 (empty) — there are no tiles to scan during her fight. The live walkable
        /// surface is the collision mesh instead: see <see cref="CollisionMesh"/>.
        /// </summary>
        internal static class MapPartsGrid
        {
            internal const int Columns    = 20;    // RowStride / CellStride
            internal const int RowStride  = 320;   // 0x140 — bytes between rows (= 20 cells * 16)
            internal const int CellStride = 16;    // 0x10  — bytes per tile cell
            internal const int EmbeddedOffset = 0x9C50;   // MAPPARTS grid offset within the CDungeonMap
            internal const float CellWorld = 160f; // world units per cell (SearchiDoPutArea uses 160.0 @0x1c068c)

            // GRID -> WORLD (calibrated against live player+enemy positions, both landed on their '#' cell):
            //   worldX = col * 160,  worldZ = row * 160  (origin 0, no rotation; X<->col, Z<->row).
            // A cell with f0 != 0 and f0 != -1 is a placed tile (walkable). The grid is POPULATED at floor-entry
            // (buildRandomMap memcpys it, ArrangementPos reads it to place enemies); during the first load frames
            // and once Ice Queen's arena flag (map_no=800) is active it can read 0/-1, so capture it early.

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

        /// <summary>
        /// Engine routines for floor generation / enemy placement (ELF vaddrs — code, NOT data: never PINE-write these).
        /// Pipeline: <see cref="BuildRandomMap"/> carves rooms (buildRoom/joinRoom) and fills the <see cref="MapPartsGrid"/>
        /// at CDungeonMap+0x9C50, then <see cref="ArrangementPos"/> places each enemy by calling <see cref="SearchiDoPutArea"/>
        /// (picks a valid walkable cell, converts cell→world via cell size 160, rejects cells near a chest/atra/trap —
        /// see <see cref="DungeonObjects"/>). SearchiDoPutArea also indexes a per-area room-template table at
        /// <see cref="RoomTemplateTable"/> (7 areas; static room SHAPES, not floor world positions).
        /// </summary>
        internal static class Routines
        {
            internal const long BuildRandomMap    = 0x1CB670; // buildRandomMap__11CDungeonMapFii
            internal const long ArrangementPos    = 0x1D7FC0; // ArrangementPos__12CMonstorUnitFP11CDungeonMapiii
            internal const long SearchiDoPutArea  = 0x1C03C0; // SearchiDoPutArea__FP8MAPPARTSiiiiPf
            internal const long CheckTreasureBox  = 0x1C7EE0; // CheckTreasureBox__11CDungeonMapFPff
            internal const long CheckAtra         = 0x1C7FA0; // CheckAtra__11CDungeonMapFPff
            internal const long CheckTrapCircle   = 0x1C79F0; // CheckTrapCircle__11CDungeonMapFPff
            internal const long DistVector        = 0x123590; // DistVector__FPfPf (distance between two world points)
            internal const long RoomTemplateTable = 0x279D50; // FILE table[area]=ptr to that area's static room-shape array
        }

        /// <summary>
        /// Placement-exclusion objects embedded in the CDungeonMap. ArrangementPos rejects any enemy spawn cell that
        /// lands near one of these (CheckTreasureBox/CheckAtra/CheckTrapCircle, each via DistVector). We mirror that
        /// when relocating Ice Queen so neither she nor a companion spawns on a chest/atra/trap. Each entry's world
        /// position is 3 floats (x@+0, height@+4, z@+8); grid cell = (col = x/160, row = z/160). Offsets are relative
        /// to the CDungeonMap instance (<see cref="Map.NowDngMapPtr"/> deref). Decoded from the Check* routines.
        /// </summary>
        internal static class DungeonObjects
        {
            // Treasure boxes (CheckTreasureBox @0x1C7EE0): up to 24, stride 0x40.
            internal const int ChestArray  = 0xB660;  // first chest record
            internal const int ChestStride = 0x40;
            internal const int ChestActive = 0x00;    // int — nonzero = present
            internal const int ChestPos    = 0x10;    // float x/height/z
            internal const int ChestCount  = 0xBC60;  // int — live chest count
            internal const int ChestMax    = 24;

            // Atra / sealed townsfolk (CheckAtra @0x1C7FA0; only checked when map-type index < 6): up to 8, stride 0x20.
            internal const int AtraArray   = 0xBC80;
            internal const int AtraStride  = 0x20;
            internal const int AtraPos     = 0x00;    // float x/height/z
            internal const int AtraActive  = 0x14;    // int — nonzero = present
            internal const int AtraCount   = 0xBD80;  // int
            internal const int AtraMax     = 8;

            // Trap circles (CheckTrapCircle @0x1C79F0): up to 3, stride 0x20.
            internal const int TrapArray   = 0x10AB0;
            internal const int TrapStride  = 0x20;
            internal const int TrapPos     = 0x00;    // float x/height/z
            internal const int TrapActive  = 0x10;    // int — nonzero = present
            internal const int TrapMax     = 3;
        }

        /// <summary>
        /// CCollisionMDT — the per-floor walkable collision mesh (a triangle soup). This is the ACTUAL surface
        /// the player and enemies move on, and it stays loaded during the Ice Queen arena (map_no=800), unlike
        /// the <see cref="MapPartsGrid"/> tile grid (wiped to -1 once her fight is active). So for reliable
        /// on-floor placement we read THIS, not the tiles. RE'd from SCUS_971.11 (full .symtab).
        ///
        /// Engine routines (ELF vaddrs; add 0x20000000 only for data, not these code addresses):
        ///   GetMaxY__13CCollisionMDTFPf          @0x124F10 — floor height at (x,z); the walkability test (iterates polys)
        ///   GetVertexAddress__13CCollisionMDTFPi @0x1254F0 — returns vertex array ptr + writes count
        ///   GetPolygon__13CCollisionMDTFi...     @0x124EC0 — a polygon's 3 vertices
        ///   PickUpNearPoly__13CCollisionMDT...   @0x125540 — nearest polygon to a point
        ///   CreateCollisionMDT__FPUiP14CDataAlloc2 @0x127250 — builds the mesh (sets vtable@+0x20, MDT@+0x30)
        ///   LoadCollisionFile__FPUi              @0x127800
        ///
        /// Finding the live instance: scan EE RAM for the vtable pointer <see cref="Vtable"/> (or <see cref="VtableAlt"/>)
        /// stored at instance+<see cref="InstVtable"/>; the instance starts 0x20 before it. Validate via the MDT.
        /// </summary>
        internal static class CollisionMesh
        {
            // ── CCollisionMDT instance fields ──
            internal const int  InstVtable = 0x20;        // vtable pointer (== Vtable / VtableAlt) — used to locate the instance
            internal const int  InstMdt    = 0x30;        // pointer to the MDT data block (0 = no mesh loaded)
            internal const uint Vtable     = 0x2A10D0;    // CCollisionMDT vtable (PS2 vaddr, stored raw in instances)
            internal const uint VtableAlt  = 0x2A1100;    // alternate vtable seen in CreateCollisionMDT

            // ── MDT data block (at *(instance + InstMdt)) ──
            internal const int MdtVertCount  = 0x0C;      // int   — vertex count
            internal const int MdtVertArrOff = 0x10;      // int   — byte offset from MDT to the vertex array: verts = mdt + *(mdt+0x10)
            internal const int MdtPolyArrOff = 0x28;      // int   — byte offset from MDT to the polygon array: polyBase = mdt + *(mdt+0x28)

            // ── Vertex (16 bytes; X/Z are the floor plane, Y is height) ──
            internal const int VertStride = 16;
            internal const int VertX = 0x00, VertY = 0x04, VertZ = 0x08;   // floats

            // ── Polygon array (starts at polyBase + PolyArrHeader; each record PolyStride bytes) ──
            // From GetMaxY: s3 = polyBase + 0x10, indices read at s3+8/+0xC/+0x10, record advances 0x14.
            internal const int PolyArrHeader = 0x10;      // header before the first poly record (poly count TBD within it)
            internal const int PolyStride    = 0x14;      // 20 bytes per polygon record
            internal const int PolyVertIdx0  = 0x08;      // 3 vertex indices (ints) at record +0x08/+0x0C/+0x10
            // world vertex of a poly index = vertArray + idx * VertStride
        }
    }

    /// <summary>
    /// The engine's _SET/_GET_GLOBAL_INT scratch array (handler @ ELF 0x1E5190): global[i] = Base + i*4.
    /// ELF global-int array @0x1D8FC80, read at PCSX2 0x21D8FC80. Used by the Ice Queen fight handshake.
    /// </summary>
    internal static class GlobalInt
    {
        internal const long Base = 0x21D8FC80;
    }
}
