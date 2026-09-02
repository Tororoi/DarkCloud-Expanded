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

        // Dynamic door objects (setCollisionData's second gather loop): 0x18 slots @instance+0xB660,
        // stride 0x40 — int active flag at +0, float3 position at +0x10. All doors share ONE collision
        // CFrame (ptr @instance+0xBC6C) that the engine only TRANSLATES per door (no rotation is set).
        internal const int  DoorSlotsOffset   = 0xB660;
        internal const int  DoorStride        = 0x40;
        internal const int  DoorCount         = 0x18;
        internal const int  DoorPosOffset     = 0x10;
        internal const int  DoorFrameOffset   = 0xBC6C;
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

    /// <summary>
    /// The dungeon FIRE / HEAT-HAZE system — the only framebuffer distortion in the game, and the vehicle
    /// for the Mirage clone's shimmer (Mirage hijacks a torch's raster emitter; see [[mirage-decoy-aggro]]).
    ///
    /// DrawRaster__11CDungeonMap (0x1C4610) walks a 20x20 per-tile fire array, but ONLY tiles within +/-4 of
    /// the CAMERA and with dist &lt;= 240 (both camera-relative — DrawMap__11CDungeonMap @0x1C286C RECOMPUTES
    /// that dist every frame, so forcing it from the mod is futile; the PNACH relaxes the 240 gate instead).
    /// Each enabled tile draws the raster emitters of its fire STRUCT at dngMap + fireIdx*Stride, placing
    /// emitter i at world ((localX + col*16)*10, localY*10, (localZ + row*16)*10) AFTER rotating (localX,localZ)
    /// by theta = (4 - tileRot) * 90deg.
    ///
    /// DrawFire__11CDungeonMap walks the SAME emitter list and draws each as a flame+light, so an injected
    /// emitter must zero its <see cref="EmitFlags"/> byte (bit0 = light, bit1 = flame) to be haze-only.
    /// </summary>
    internal static class FireRaster
    {
        // Per-tile fire array (relative to the CDungeonMap instance, i.e. NowDngMapPtr).
        internal const int  TileArray   = 0x9C50;   // 20x20 entries, 0x10 each
        internal const int  TileStride  = 0x10;
        internal const int  TileCount   = 400;
        internal const int  TileFireIdx = 0x00;     // int; -1 = no fire
        internal const int  TileRot     = 0x04;     // int; emitter offsets are rotated by (4-rot)*90deg
        internal const int  TileDist    = 0x08;     // float camera distance; <=240 to draw (game recomputes each frame)
        internal const int  TileEnabled = 0x0C;     // int; ==1 to draw

        // Fire struct: dngMap + fireIdx * Stride.
        internal const int  Stride      = 0x1D0;
        internal const int  EmitCount   = 0x4A2;    // short — RASTER emitter count (lh v1,0x4A2(v1) @0x1C48B8)
        internal const int  EmitPos     = 0x4B0;    // emitter[i] local pos (x,y,z floats), stride 0x10
        internal const int  EmitPosStride = 0x10;
        internal const int  EmitFlags   = 0x510;    // emitter[i] flag BYTE (stride 1): bit0 = light/glow, bit1 = flame.
                                                    // Zero it => heat-haze ONLY (DrawRaster never reads this byte).
        internal const int  MaxEmitSlot = 5;        // EmitPos[6] would land on EmitFlags[0] — never append past 5

        /// <summary>DAT_002a1da8 (RODATA): blendTextuerTest's multiplicative displacement gain. The distortion is
        /// (size/10000) * gain * wave * AMP, so 0 = no distortion and it scales linearly to full (~1.3 vanilla).
        /// Being plain DATA, the mod can RAMP it per tick — patching the AMP code constant every frame would be
        /// the recompiler-crashing hot-code surgery we avoid everywhere.</summary>
        internal const long DistortionGain = 0x202A1DA8;
    }

    /// <summary>
    /// The item-Bomb explosion effect (the visual thrown Bomb items make — resident on EVERY floor) plus
    /// the ground shockwave ring. RE'd from <c>SetBombEffect</c> @0x1D5940 / <c>SetBomb__15CItemBombEffect</c>
    /// @0x1D60A0 / <c>CheckBomb</c> @0x1D6190: spawning is PURE DATA WRITES into a 3-slot pool, so the mod
    /// can fabricate an explosion at any position (the native path's extra steps — SndSePlay(0x6C) and the
    /// CCollisionData damage — are function calls, replaced mod-side). A slot is FREE when all five
    /// <see cref="ActiveOffset"/> flags are 0 (CheckBomb's test). Each explosion = five staggered sub-puffs.
    /// Native SetBomb writes, per sub-puff i (0..4): pos float4 @ +i*0x10 (all five = the blast point),
    /// state 0 @ +0x50+i*4, timer i*−3 @ +0x64+i*4, 20.0f @ +0x78+i*4, 128.0f @ +0x8C+i*4, active 1
    /// @ +0xA0+i*4; then scale @ +0xB4, state[0]=2, state[1]=1. The native caller also fires the shockwave
    /// when scale &gt; 1: pos float4 @ +0, 1.0f @ +0xC, scale×30 @ +0x10/+0x14, 0 @ +0x18, scale×15 @ +0x1C,
    /// 0 @ +0x20/+0x24, 1 @ +0x28.
    /// </summary>
    internal static class BombEffect
    {
        internal const long PoolPtr      = 0x202A35E4; // NowBombEffect (gp-0x620C) → 3 slots × 0xC0
        internal const long ShockWavePtr = 0x202A35E8; // NowShockWave (gp-0x6208) → single struct
        internal const int  SlotCount    = 3;
        internal const int  SlotStride   = 0xC0;
        internal const int  SubPuffs     = 5;
        internal const int  PosOffset    = 0x00;  // + i*0x10, float4 per sub-puff
        internal const int  StateOffset  = 0x50;  // + i*4
        internal const int  TimerOffset  = 0x64;  // + i*4 (int, i*−3 = staggered starts)
        internal const int  SizeAOffset  = 0x78;  // + i*4 (float, 20.0)
        internal const int  SizeBOffset  = 0x8C;  // + i*4 (float, 128.0)
        internal const int  ActiveOffset = 0xA0;  // + i*4 (int; any nonzero = slot busy)
        internal const int  ScaleOffset  = 0xB4;  // float — whole-explosion scale
    }

    /// <summary>
    /// The THROWN-GEM elemental burst (Fire/Ice/Thunder/Wind/Holy Gem, items 161-165) — ELF global
    /// <c>MasekiEffect</c> (魔石 = "magic stone"), a <c>CSHOT_EFFECT[5]</c>: one slot per element.
    ///
    /// Like the item-Bomb pool this is a PURE-DATA effect, and for the same three reasons:
    ///   • RESIDENT — dungeon load unconditionally does LoadFile("dun/effect/maseki_ex.chr") and Entry2/ReEntry's
    ///     all five slots into a dedicated 65KB arena (MasekiModelBuffer). NOT gated on owning/throwing a gem.
    ///     All five descriptors name the SAME .chr — they differ only in element bits and MOTION INDEX
    ///     (<see cref="MotionIdxOffset"/>). One model, five animations.
    ///   • STEPPED + DRAWN unconditionally every frame by the dun overlay's per-frame loops (step dun 0x1DB8550,
    ///     draw dun 0x1DAE590) — the loop ADJACENT to the CBomb pool <see cref="BombEffect"/> already drives,
    ///     under the same draw gate. If Big Bang renders, this renders.
    ///   • SPAWNING IS FIELD WRITES. Vanilla spawns from Step__14CMainItemModel (main 0x1D4E20) via
    ///     Set__12CSHOT_EFFECT (0x1ADD60) + SetDmg/SetLifeTime/SetWait — every one of which is a plain store.
    ///     Nothing is allocated, loaded or registered at spawn time, so the mod replicates the stores itself.
    ///
    /// Each element slot holds <see cref="SubSlots"/> = 3 INITIALISED sub-slots, each a real CCharacter
    /// (position/rotation/SCALE/motion), so bursts get an independent size — that is the "big for the main
    /// pellet, normal for shrapnel" knob. Sub-slots 3..7 physically exist but Entry2 never gave them model or
    /// motion data: activating them is garbage or a crash. Three per element is the hard cap.
    ///
    /// Bursts are fire-and-forget: Step clears the active flag when the lifetime expires.
    /// See also the memory note "maseki-gem-effect-pool". Do NOT confuse with CWeaponElement (CWeaponElFx) —
    /// that is the ON-HIT elemental spark an elemental WEAPON makes, spawned from CheckDmg.
    /// </summary>
    internal static class MasekiEffect
    {
        internal const long SlotBase     = 0x21E5B380; // MasekiEffect[0]; + element*SlotStride
        internal const int  SlotStride   = 0xA160;     // sizeof(CSHOT_EFFECT)
        internal const long DescBase     = 0x21DC2230; // MyEntryEffect_Maseki00..04; + element*DescStride
        internal const int  DescStride   = 0x70;
        internal const int  SubSlots     = 3;          // INITIALISED sub-slots per element (Entry2's count)

        // Element index — the slot AND the descriptor's motion index select the animation.
        internal const int  Fire = 0, Ice = 1, Thunder = 2, Wind = 3, Holy = 4;

        // ── the sub-slot CCharacter: SubCharBase + i*SubCharStride, relative to the element slot ──
        internal const int  SubCharBase   = 0x11C0;
        internal const int  SubCharStride = 0x11B0;    // sizeof(CCharacter)

        // ── per-sub-slot arrays, relative to the element slot ──
        internal const int  VelOffset       = 0x9F40;  // + i*VelStride (float4; gems are stationary → 0,0,0,1)
        internal const int  VelStride       = 0x10;    // one float4 per sub-slot
        // The burst's little STATE MACHINE. All three MUST be re-seeded on every spawn: a sub-slot is reused, and
        // if the phase is left at the value the PREVIOUS burst died in, Step never reaches its expire branch — the
        // active flag is never cleared, the slot LEAKS, and once all three leak nothing renders ever again.
        internal const int  PhaseCountOff   = 0x9FC0;  // + i*2  (u16) — reset to 0
        internal const int  PhaseTimerOff   = 0x9FD0;  // + i*4  (int, seeded from descriptor +0x38)
        internal const int  PhaseOffset     = 0x9FF0;  // + i*2  (u16) — reset to 0
        internal const int  ActiveOffset    = 0xA000;  // + i*2  (u16) — THE GATE. Write LAST. Step clears it on expiry.
        internal const int  DamageOffset    = 0xA010;  // + i*4  (int) — 0 = visual-only burst
        internal const int  WepStatusOffset = 0xA030;  // + i*4
        internal const int  UserIdOffset    = 0xA050;  // + i*2  (u16)
        internal const int  UserId2Offset   = 0xA060;  // + i*2  (u16)
        internal const int  Unk070Offset    = 0xA070;  // + i*4
        internal const int  LoopOffset      = 0xA0B0;  // + i*4  (int, -1 = no loop)
        internal const int  RandomRateOff   = 0xA0D0;  // + i*4  (float; >=0 makes Draw jitter the position — a shake knob)
        internal const int  LifeTimeOffset  = 0xA0F0;  // + i*4  (int; vanilla 10)
        internal const int  EnemyAttrOffset = 0xA110;  // + i*4  (int, -1 = normal)
        internal const int  NoSoundOffset   = 0xA130;  // + i    (byte)
        internal const int  WaitOffset      = 0xA138;  // + i    (byte; vanilla 5 = re-hit wait)

        /// <summary>+ i (byte) — the collision RE-HIT COOLDOWN, and the switch that makes a burst visual-only.
        /// Step creates the burst's damage sphere only when this is &lt; 1 (then re-arms it to
        /// <see cref="WaitOffset"/>), so seeding it ABOVE the burst's lifetime means the sphere is NEVER created.
        /// That matters for more than the damage: a burst that collides DEALS a hit, which advances the engine's
        /// hitCnt — and anything driving effects off that ring then spawns another burst, and cascades.
        /// Zeroing <see cref="DamageOffset"/> alone is NOT enough (the damage formula floors at 1 HP).</summary>
        internal const int  HitCooldownOff  = 0xA140;
        internal const byte NoCollide       = 0x7F;   // >> any burst lifetime → the cooldown never reaches 0
        internal const int  Unk148Offset    = 0xA148;  // int
        internal const int  LastEnteredOff  = 0xA150;  // int — index of the most recently entered sub-slot

        // ── descriptor (BT_SHOT_EFFECT) fields we read ──
        internal const int  PhaseSeedOffset = 0x38;    // int  — seeds PhaseTimerOff
        internal const int  MotionIdxOffset = 0x4C;    // s16  — the element's animation in maseki_ex.chr
        /// <summary>float — the RADIUS of the damage sphere Step spawns for a burst (vanilla 10.0). Widening it is
        /// how a burst damages an AREA, and it makes the engine resolve that damage itself: guards, elements,
        /// death, drops all behave, unlike a direct HP write which produces an unkillable walking corpse.
        /// It lives on the SHARED descriptor, so a change is also seen by real thrown gems of that element —
        /// snapshot and restore it (GemBurst does).</summary>
        internal const int  ColRadiusOffset = 0x28;
        /// <summary>int — the ELEMENT BITS a burst's damage carries (1=Fire, 2=Ice, 4=Thunder, 8=Wind, 0x10=Holy;
        /// 0 = none). Step passes it into CCollisionData::Set, and CheckDmg reads it back off the collision to pick
        /// the element. Like the radius it is on the SHARED descriptor — snapshot and restore it (GemBurst does),
        /// or real thrown gems of that element change too.</summary>
        internal const int  ElementBitsOffset = 0x40;

        internal static long Slot(int element)   => SlotBase + (long)element * SlotStride;
        internal static long Desc(int element)   => DescBase + (long)element * DescStride;
        internal static long SubChar(long slot, int i) => slot + SubCharBase + (long)i * SubCharStride;

        /// <summary>The pool is live only once the dungeon has entered the models: slot[0] holds the pointer to
        /// its own descriptor. If it doesn't, Draw early-outs and writing fields would do nothing (or worse).
        /// NOTE the pointer in RAM is a GUEST address (0x01DC2230) — it must be mapped before comparing with our
        /// MMU-space <see cref="Desc"/> (0x21DC2230), or this never matches and every burst silently no-ops.</summary>
        internal static bool Loaded(int element)
        {
            int p = Memory.ReadInt(Slot(element));
            return Memory.IsValidGuest(p) && Memory.ToMmu(p) == Desc(element);
        }
    }
}
