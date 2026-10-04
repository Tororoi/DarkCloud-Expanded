// Dungeon address bank: the floor / map system (CDungeonMap, MAPPARTS grid, placement routines, lighting, collision
// mesh), the healing spring and HEAL-ability words, the CDataAlloc2 pools, the 20x20 tile grid, the script-event flag,
// the 6-slot chara draw and the two camera pointers (DungeonCamera / FollowCamera).
// Effect pools (fire raster, bomb, maseki) are in EffectAddresses.cs; the script global ints in StbAddresses.cs.
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
        /// Scene/dungeon lighting — STUB for a later pass (not yet fully decoded). The ambient color and directional
        /// lights are global renderer state, set per-draw (MGSetAmbient runs before nearly every Draw), so there is NO
        /// stable "write once" ambient field — the per-enemy AmbientBase* slot fields are just a per-frame copy of the
        /// global ambient. The STB script command _LOAD_LIGHT(idx) loads a LIGHTING PRESET — it copies two directional-
        /// light matrices (@LightBuffers +0x00/+0x40) and one ambient (R,G,B,A) vector (+0x80) from a preset table into
        /// the active light buffers. This is the lead for changing dungeon lighting at its source. (Addresses: routines
        /// are ELF vaddrs; data are EE addresses.)
        /// </summary>
        internal static class Lighting
        {
            internal const long LoadLight     = 0x1938B0;   // _LOAD_LIGHT__FP12RS_STACKDATAi (STB cmd) — load lighting preset[idx]
            internal const long MGGetAmbient  = 0x12DD30;   // MGGetAmbient(float*) — read global ambient (RGBA) into a buffer
            internal const long MGSetAmbient  = 0x12DD00;   // MGSetAmbient(float*) — set global ambient; called before nearly every draw (volatile)
            internal const long GlobalAmbient = 0x21C756B0; // EE — 4-float (R,G,B,A) renderer ambient register; rewritten per draw
            internal const long LightBuffers  = 0x21D3D500; // EE — active lights: dir-light matrices @ +0x00/+0x40, ambient vec @ +0x80
        }

        /// <summary>
        /// The dungeon's ATMOSPHERE globals — pure data the dungeon overlay's MainDraw (dun 0x1DAE2A0) hands the renderer
        /// EVERY frame, so a write here lasts until the zone script rewrites it: the front-floor set when <see cref="Mode"/>
        /// is 0, the back-floor (ura) set otherwise. Per frame: MGSetLight(<c>*Dirs</c>, <c>*Colors</c>) → MGSetAmbient
        /// (<c>*Ambient</c>) → background colour → fog (<c>*FogRate</c>, <c>*FogColor</c>). Colours are floats on a 0-255 scale
        /// (an ambient of 255,255,255 lights every surface to full texture brightness and over); fog runs from
        /// <c>FogRate[0]</c> (start distance) to <c>FogRate[1]</c> (fully the fog colour) — vanilla Demon Shaft ura is 140/190.
        /// The zone cfg's AMBIENT / LIGHT_C / FOG lines are what fill these at floor load.
        /// </summary>
        internal static class DungeonLighting
        {
            internal const long Mode        = 0x202A34CC; // int — 0 = the main set is drawn, else the sub (ura) set (lightingMode)
            internal const long MainDirs    = 0x21DF86B0; // 4 × float4 — directional light matrices (untouched by the mod)
            internal const long MainColors  = 0x21DF86F0; // 4 × float4 — directional light colours, RGB(A) per row
            internal const long MainAmbient = 0x21DF8730; // float4 — ambient RGBA
            internal const long MainFogRate = 0x21DC24A0; // 4 floats — fog start, fog end, and two the mod leaves alone
            internal const long MainFogColor= 0x202A22E4; // 3 bytes — fog RGB
            internal const long SubDirs     = 0x21DF8740;
            internal const long SubColors   = 0x21DF8780;
            internal const long SubAmbient  = 0x21DF87C0;
            internal const long SubFogRate  = 0x21DC24B0;
            internal const long SubFogColor = 0x202A22E8;
            internal const int  ColorRows   = 4;          // float4 rows in a Colors block
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

    /// <summary>Set to 1 while the player stands inside a healing spring's zone, and cleared at the top of
    /// every CheckHealZone (0x1AF6E0) call — a live "in the spring right now" flag, rewritten each frame.
    /// Native 0x1DC4514. The spring heals HP/thirst via HealingWater (0x1AF980), which is what drives the
    /// check; HealingWater has no ELF callers because it is called from the DUN OVERLAY.
    ///
    /// ⚠ It is a 16-BIT field — the engine writes it with `sh` (0x1AF5C8 / 0x1AF710 / 0x1AF930). READ IT WITH
    /// <c>ReadUShort</c>. A 32-bit read pulls in the adjacent halfword, so a `== 1` test silently fails
    /// whenever that neighbour is non-zero — which is exactly what made the Kitchen Knife blessing never fire.</summary>
    internal static class HealingSpring
    {
        internal const long InZoneFlag = 0x21DC4514;   // short (16-bit)
    }

    /// <summary>The passive HEAL ability (weapon flag 0x800): the dun overlay's per-frame loop (0x1DB8240) counts frames
    /// here and, when the count reaches its period, grants +1 HP through AddNowLife and starts over. The period is 240
    /// frames as shipped and 180 with DunPatches' cadence patch. Frame-driven, so it stands still whenever the floor's
    /// loop does — a hold, the menu — and a wrap is the one moment the native heal has just fired.</summary>
    internal static class HealAbility
    {
        internal const long TickCounter = 0x202A3684;   // int, gp-0x616C
    }

    /// <summary>The dungeon's CDataAlloc2 bump pools — each {base +0, used +8, cap +0xC}, the last two in 16-byte units —
    /// carved in <see cref="InCarveOrder"/> by GameInit (dun 0x1DAC1C0) from the 27 MB global buffer (GlobalDataBuffer
    /// @0x2AB080, whose bump counter sits at the array's end). Chara, weapons and effects share ONE pool: the chara cap is
    /// the pool size and LoadChara2's other two counters take what is left. An overflow is a silent spin
    /// (Alloc__14CDataAlloc2: printf + while(true)). Raising the character heap (DunPatches) moves every pool carved after
    /// it — DungeonPools measures the shift.</summary>
    internal static class DataPools
    {
        internal const int  Used = 0x8, Cap = 0xC;
        internal const long Common = 0x21F06640, Motion = 0x202AB020, Chara = 0x21F06660, ShotFx = 0x21F06690;
        internal const long Texture = 0x202AB030, P870 = 0x21F06870, P6A0 = 0x21F066A0, P6B0 = 0x21F066B0;
        internal const long P6C0 = 0x21F066C0, P840 = 0x21F06840, Monstor = 0x21F066D0, Map = 0x21F06650;
        internal const long Weapon = 0x21F06670, Effect = 0x21F06680;   // LoadChara2's counters inside the chara pool
        /// <summary>BtCashBuffer: the floor system script's work allocator (BtSystemScriptRun). On a dungeon's first floor it
        /// is aimed at BtScriptWorkBuffer (P840, 100,000 units — whose own counter therefore never moves); on every later
        /// floor at the monster pool's region with its capacity + 35,000. Its used counter is the only record of that demand.</summary>
        internal const long Cash = 0x21F06850;
        internal const long GlobalUsed = 0x21C74980;                    // the global buffer's bump counter (units)
        internal const int  GlobalCap  = 0x19C98F;
        internal static readonly (long addr, string name)[] InCarveOrder =
        {
            (Common, "common"), (Motion, "motion"), (Chara, "chara"), (ShotFx, "shotfx"), (Texture, "texture"), (P870, "p870"),
            (P6A0, "p6a0"), (P6B0, "p6b0"), (P6C0, "p6c0"), (P840, "p840"), (Monstor, "monstor"), (Map, "map"),
        };
    }

    /// <summary>
    /// The active floor's 20×20 minimap tile grid, RE'd from CDungeonMap (checkMask 0x1C39C0 gives the
    /// world→tile transform; DrawMiniMap 0x1C3180 gives the per-tile struct). World→tile:
    /// tx = (worldX + 80) / 160, ty = (worldY + 80) / 160 (tile size 160, tile 0 spans −80..+80).
    /// Per-tile 0x10-byte entries at instance+0x9C50: int at +0 = map-parts index, −1 = VOID (no floor
    /// geometry — a wall for line-of-sight purposes). Structural proof: 400 entries × 0x10 ends at
    /// +0xB550, exactly where the room-rect list ({x0,y0,w,h} × count@+0xB650) begins. The revealed
    /// minimap mask (ints) sits at +0x8710. Instance pointer = global NowDngMap.
    /// </summary>
    internal static class DungeonTileGrid
    {
        internal const long NowDngMapPtr   = DungeonAddresses.Map.NowDngMapPtr; // same global — declared once, in Map
        internal const int  TilePartsOffset = 0x9C50;    // + (tx + ty*20)*0x10 → int parts index, −1 = void
        internal const int  TileRotOffset   = 0x9C54;    //   +4 → int rotation variant (×−90°, see setCollisionData)
        internal const int  TileStride      = 0x10;
        internal const int  GridSize        = 20;
        internal const float TileWorldSize  = 160f;
        internal const float TileWorldBias  = 80f;       // tile = (world + bias) / size

        // ── per-part collision geometry (RE'd from setCollisionData 0x1C0FC0 + PickUpNearPoly
        //    CFrame 0x12A390 + CCollisionMDT 0x1258B0) ─────────────────────────────────────────
        // Parts table @instance+0x490, stride 0x1D0: +0xC = collision CFrame ptr (0 = none),
        // +0x10 = short rotation base (added to the tile's rotation variant; combined value r is
        // wrapped `if (r > 3) r -= 3; if (r == 3) r = -1` then angle = r × −90°, position = tile×160).
        // CFrame node: +0x0 flags (bit0 = has collision, bit1 = stop-descend, bit2 = skip; ==4 → dead),
        // +0x4 = collision object ptr, +0x138 = first child, +0x13C = next sibling.
        // Collision object +0x30 → MDT blob: vertex pool at blob+*(blob+0x10) (float4, stride 0x10);
        // poly section at blob+*(blob+0x28): triangle count at +0x14, index records at +0x18
        // (3 vertex indices per record; stride 0x14 per the decompile's next-record precompute).
        internal const int  PartsTableOffset  = 0x490;
        internal const int  PartsStride       = 0x1D0;
        internal const int  PartColFrame      = 0xC;
        internal const int  PartRotBase       = 0x10;
        internal const int  FrameFlags        = 0x0;
        internal const int  FrameColObj       = 0x4;
        internal const int  FrameFirstChild   = 0x138;
        internal const int  FrameNextSibling  = 0x13C;
        internal const int  ColObjBlobPtr     = 0x30;
        internal const int  BlobVertsOffset   = 0x10;
        internal const int  BlobPolysOffset   = 0x28;
        internal const int  PolySecCount      = 0x14;
        internal const int  PolySecRecords    = 0x18;
        internal const int  PolyRecordStride  = 0x14;

        // setCollisionData's gather mode (+0xBDEC): 1 = the tile grid above; otherwise a list of placed parts — each parts-table
        // entry whose +0x1B0 (+0x640 from the instance) is non-zero, placed at its own +0x110 vector (+0x5A0) and rotated by its
        // (int) float +0x170 (+0x600) plus the rotation base, wrapped as above.
        internal const int  GatherModeOffset  = 0xBDEC;
        internal const int  PlacedActive      = 0x640;
        internal const int  PlacedPos         = 0x5A0;
        internal const int  PlacedRot         = 0x600;
        // The collision object's ready-built CCPolys (PickUpNearPoly__13CCollisionMDT 0x1256C0): array ptr +0x34, count +0x38,
        // stride 0x70 — v0/v1/v2 float4 (x, height, y) at +0/+0x10/+0x20, in the frame's local space.
        internal const int  ColObjPolys       = 0x34;
        internal const int  ColObjPolyCount   = 0x38;
        internal const int  ColPolyStride     = 0x70;
        // A CFrame's local matrix (GetLWMatrix__6CFrame 0x1281B0): the base 4×4 at +0x1D0 (row-vector, rows 0–2 the basis, row 3
        // the translation) — as is while +0x24C is 0; once SetRotation/SetPosition have run, the basis rows scaled by +0x210,
        // turned by the X/Y/Z angles at +0x230 (when +0x248 bit 0), and the position +0x220 added to the translation (bit 1:
        // the base translation is kept out of the turn). The world matrix is local × the parent's (+0x110).
        internal const int  FrameMatrix       = 0x1D0;
        internal const int  FrameScale        = 0x210;
        internal const int  FramePos          = 0x220;
        internal const int  FrameRot          = 0x230;
        internal const int  FrameRotFlags     = 0x248;
        internal const int  FrameComposed     = 0x24C;

        // Dynamic door objects (setCollisionData's second gather loop): 0x18 slots @instance+0xB660,
        // stride 0x40 — int active flag at +0, float3 position at +0x10. All doors share ONE collision
        // CFrame (ptr @instance+0xBC6C) that the engine only TRANSLATES per door (no rotation is set).
        internal const int  DoorSlotsOffset   = 0xB660;
        internal const int  DoorStride        = 0x40;
        internal const int  DoorCount         = 0x18;
        internal const int  DoorPosOffset     = 0x10;
        internal const int  DoorFrameOffset   = 0xBC6C;
    }

    /// <summary>Dungeon SCRIPT EVENT state (the battle-system script runner). Non-zero while an in-floor scripted
    /// event runs (the jump-across, chest and story beats that load event models such as c04bjump.chr);
    /// BtSystemScriptAfter (0x1BB5E0) zeroes it at the event's end — right after EdEventAllClear, which deletes the
    /// player's extend motions (MOTION 1+), and alongside a reset of her motion-speed/flags/id words. Anything that
    /// depends on the player's extra motion channels or the texture manager must stand down while it is set.</summary>
    internal static class DungeonScriptEvent
    {
        internal const long BtEventMode = 0x202A3608;
    }

    /// <summary>
    /// The DUNGEON's 6-character draw — the vanilla facility for rendering an extra character in a dungeon.
    /// Draw__11CSeireiKing (the dungeon scene draw) loops i in 0..5: if registry[i] != 0 it reloads texture
    /// group <see cref="CharaTexBase"/>+i, then calls Draw__12CNPCharacter on chara[i] = CharaArray + i*Stride.
    /// That draw renders iff active(+0x146C) != 0 AND model(+0xBC) != 0 AND opacity(+0xCEC) > 0.
    ///
    /// So: fill a slot's CCharacter fields (see <see cref="CCharacter"/>), set it active, and register it —
    /// and the game draws your character every frame. All DATA writes, no injected code. A separate step loop
    /// walks the same slots; <see cref="StepSkipTable"/> lets you have a slot DRAWN but not STEPPED.
    /// (Slot gates below are CNPCharacter fields, i.e. past the embedded CCharacter.)
    /// </summary>
    internal static class DungeonCharaDraw
    {
        internal const long CharaArray    = 0x21EA8460;  // guest 0x01EA8460 (hardcoded in the dungeon draw)
        internal const int  CharaStride   = 0x14A0;
        internal const long CharaRegistry = 0x21D3D284;  // entry i (int) != 0 → slot i is drawn
        internal const long StepSkipTable = 0x21D3D344;  // entry i (int) != 0 → slot i is NOT stepped
        internal const int  CharaTexBase  = 0x20;        // slot i's texture group = 0x20 + i

        internal const int  CharaActive   = 0x146C;      // int — Draw__12CNPCharacter requires != 0
        internal const int  CharaMotionA  = 0x1474;      // Step__12CNPCharacter runs the motion step only if
        internal const int  CharaMotionB  = 0x1478;      //   (+0x1474 || +0x1478) != 0 — zero both to freeze
        internal const int  CharaRampA    = 0x1484;      // opacity-ramp amount (int); 0 = no ramp
        internal const int  CharaRampB    = 0x1488;      //   ramp B (engine sets = -1 each step → falls back to A)
    }

    /// <summary>The dungeon's camera pointer (ELF <c>NowCamera</c>, gp-0x6358): the CCameraFollow the dungeon driver
    /// (MoveChara, OpC_MotionProcess, CameraAutoMove) moves — a dungeon-side object (0x21DC45E0 in play), not the town's
    /// MainCamera. 0 in town.</summary>
    internal static class DungeonCamera
    {
        internal const long NowCamera = 0x202A3498;
    }

    /// <summary>
    /// The live town follow-camera (CCameraFollow), reached through the ELF's camera POINTER —
    /// resolves to <see cref="EditLoop.MainCamera"/> while walking/fishing. One home for the
    /// pointer and the Step-consumed field offsets (previously re-declared per feature file).
    /// </summary>
    internal static class FollowCamera
    {
        /// <summary>Pointer to the active CCameraFollow.</summary>
        internal const long Ptr = 0x21D19678;

        internal const int RefX = 0x2C0;       // ref (look-at) xyz used by Step
        internal const int RefY = 0x2C4;
        internal const int RefZ = 0x2C8;
        internal const int Dist = 0x2D0;
        internal const int Height = 0x2D4;
        internal const int Angle = 0x2D8;      // target yaw (== EditLoop.CameraAngle)
        internal const int AngleNow = 0x2DC;   // smoothed yaw (== EditLoop.CameraAngleNow)

        /// <summary>The per-frame camera-collision gather arena struct pointer
        /// (WorkBuffer: {+0 data, +8 used, +C cap}) — see TownCameraPolyBuffer.</summary>
        internal const long WorkBufferPtr = 0x202A2388;

        /// <summary>The camera object as an MMU address, or 0 if the pointer isn't live.</summary>
        internal static long Base()
        {
            uint p = Memory.ReadGuestPtr(Ptr);
            return Memory.IsValidGuest(p) ? Memory.ToMmu(p) : 0;
        }
    }
}
