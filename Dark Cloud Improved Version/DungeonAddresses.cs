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
    /// <summary>Set to 1 each frame by CheckHealZone (0x1AF6E0) while the player stands inside a
    /// healing spring's zone (cleared at the top of every check — a live "in spring right now" flag).
    /// Native 0x1DC4514; the spring itself heals HP/thirst via HealingWater (0x1AF980).</summary>
    internal static class HealingSpring
    {
        internal const long InZoneFlag = 0x21DC4514;
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
        internal const long NowDngMapPtr   = 0x202A34B8; // global: native ptr to the CDungeonMap instance
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
    /// Mirage "decoy aggro" system (CAVE-FREE — executing native code from a PINE-written heap cave
    /// crashes PCSX2, so we do it entirely with an in-place patch + a DATA table). Enemy AI reads the
    /// player's world position from ONE global (0x1EA1D30) via _GET_POSITION(-2) (ELF 0x1E1DF0). Its
    /// player branch loads that pointer into a1 with `addiu a0,sp,0x40 / lui v0,0x1ea / addiu a1,v0,0x1d30
    /// / jal sceVu0CopyVector`. We rewrite those 5 words IN PLACE (the jal is left untouched, and the
    /// a0-setup moves into the jal's delay slot) so a1 instead points at TABLE + $s2*0x20 — where $s2 is
    /// the current enemy's slot index. So aggro is redirected PER ENEMY: the mod fills each slot's table
    /// entry with the real player position for normal enemies and the decoy position for fooled ones;
    /// the slot the player hits is refilled with the real player and re-targets you. The patch is applied
    /// only WHILE a decoy is active (reverted after) so idle play is untouched. Table entry = 4 floats
    /// (x, z/height, y, w) at offset 0; stride 0x20; sized large so any in-range $s2 and the brief torn
    /// state during apply/revert land on filled memory (no crash).
    /// </summary>
    internal static class MirageDecoy
    {
        // PER-ENEMY DECOY REDIRECT (pointer indirection). Enemy AI reads its target's position via _GET_POSITION
        // (ELF 0x1E1DF0) and its attack range via _GET_DISTANCE (0x1E1D00); both read the PLAYER global 0x1EA1D30
        // directly. We redirect BOTH to a per-slot POINTER table: PtrTable[slot] holds the ADDRESS to read the
        // 16-byte position from. An un-fooled slot's pointer IS the live player global, so the enemy reads the
        // engine-live player — bit-identical vanilla, zero staleness, mod NOT in the loop. A fooled slot's pointer
        // is DecoyPos (a stationary decoy the mod writes once). So no-decoy play is untouched; only fooled enemies
        // chase/attack the decoy, and a mod stall can't affect un-fooled reads (their pointer is static).
        //
        // Both _GET_POSITION and _GET_DISTANCE now read this table from CLEAN cold-PINE CAVE copies reached via the
        // STB external-command dispatch (Mirage.ArmFuncCave; see docs/cave-code-execution.md) — no in-place surgery.
        // Each cave's helper computes a1 = *(PtrTable + slot*4) with the current enemy slot from NowMonstorUnit+0x90.
        internal const long PtrTable       = 0x21F30000; // guest 0x01F30000; entry slot = a 4-byte position POINTER
        internal const int  PtrStride      = 4;
        internal const int  TableSlots     = 256;
        internal const int  MaxSlots       = 20;          // slots the mod actively manages (FloorSlots is 16)
        internal const long DecoyPos       = 0x21F30400;  // guest 0x01F30400; 16 bytes (x,z,y,w) — the decoy position
        internal const uint DecoyPosGuest  = 0x01F30400;  // written into fooled slots' pointer entries
        internal const uint PlayerPosGuest = 0x01EA1D30;  // the live player global — un-fooled slots point here
        internal static long PtrAddr(int slot) => PtrTable + (long)slot * PtrStride;

        // STB external-command dispatch slots (8-byte {funcPtr,id} entries) repointed at the cold caves.
        internal const long PosDispatch  = 0x202918A8;   // _GET_POSITION funcPtr slot
        internal const long DistDispatch = 0x202918A0;   // _GET_DISTANCE funcPtr slot
    }

    /// <summary>
    /// Mirage clone VISUAL — done the "trick the game" way (no injected code). The dungeon's NPC draw
    /// system is a registry the game already renders every frame: DrawMapFreeStyle (0x1C23C0) auto-reserves
    /// every LOADED NPC slot whose part index (+0xCFC0) matches a loaded map part, and DrawNPCDraw
    /// (0x1C1F80, called from the overlay render @0x1DAE988) draws each reserved slot's CCharacter (@+0xBDF0)
    /// via its vtable (SetPosition +0x14, SetRotation +0x30, Draw +0xAC — all verified compatible with the
    /// player's CMainChara vtable). So we copy the live player CCharacter into a FREE NPC slot (it shares
    /// Ungaga's model @+0xBC, and +0xBEAC = that same model root is the "loaded" gate) and set the slot's
    /// gate fields — then the game draws the clone for us. All DATA writes. Base = the CDungeonMap instance
    /// (NowDngMap); per-type stride 0x1330, 4 types.
    /// </summary>
    internal static class MirageClone
    {
        internal const long NowDngMapPtr = 0x202A34B8;   // → CDungeonMap instance (same as the tile grid)
        internal const int  NpcStride    = 0x1330;
        internal const int  NpcTypes     = 4;
        internal const long PlayerChar   = 0x21EA1D20;   // active CCharacter (the source to copy)
        // Copy ONLY the draw-relevant fields (through the light block @0xD60). The slot's CCharacter ends
        // at +0xCFC0 (0x11D0 in) where the NPC gate/reservation fields begin — copying the player's full
        // 0x12A0 clobbers them and corrupts the instance (teleported the player off-map). 0xD60 is safe.
        internal const int  CharCopySize = 0xD60;

        // NPC-slot field offsets — relative to (CDungeonMap instance + type*0x1330). RE'd from DrawNPCDraw
        // (0x1c1f80): for each of 4 types it draws ONE CCharacter (@Char, root @ModelRoot) at CountField
        // positions (PosList[i]) + PosField, reloading texture group (type+0x40) per draw. The gate that
        // makes it draw is CountField>0 AND ModelRoot!=0 (NOT the old Occupied/PartIdx reservation fields,
        // which belong to a different NPC path and never triggered DrawNPCDraw — why the clone never showed).
        internal const int  Char        = 0xBDF0;   // the CCharacter DrawNPCDraw renders (vtable @+0xa0 = base+0xBE90)
        internal const int  ModelRoot   = 0xBEAC;   // = Char + 0xBC; must be nonzero to reserve+draw
        internal const int  PosField    = 0xCFA0;   // float3 ADDED to PosList[i] by DrawNPCDraw (world offset lever)
        internal const int  RotBase     = 0xCFB4;   // float; added into the draw yaw
        // The RESERVATION fields — the GAME rebuilds the draw list every frame: ClearNPC_Cash zeroes
        // CountField, then DrawMap/DrawMapFreeStyle, while drawing each loaded map part, calls
        // ReservNPC_Draw for any NPC type whose PartIdx == that part index. ReservNPC_Draw increments
        // CountField and writes PosList[i] = the part origin — but ONLY if Occupied && Gate2 && ModelRoot
        // are all nonzero. So to be drawn persistently we set PartIdx/Occupied/Gate2/ModelRoot and let the
        // game own CountField (writing it ourselves loses the race with ClearNPC_Cash — the "flash" bug).
        internal const int  PartIdx     = 0xCFC0;   // must equal a LOADED map-part index → reserved when that part draws
        internal const int  Occupied    = 0xCFC4;   // reserve gate 1 (nonzero)
        internal const int  Gate2       = 0xCFC8;   // reserve gate 2 (nonzero)
        internal const int  StepCtrl    = 0xCFCC;   // -1 → StepNPC skips the motion step (we don't want it; reserve is separate)
        internal const int  PosList     = 0xCFD0;   // per-instance world position (part origin), stride 0x10 — game-written
        internal const int  RotList     = 0xD0D0;   // per-instance rotation index, stride 4
        internal const int  CountField  = 0xD110;   // instances DrawNPCDraw draws — GAME-OWNED (read-only for us)

        // CCharacter fields we tweak in the copied clone.
        internal const int  CharPos     = 0x10;     // x,z,y (DrawNPCDraw overrides via SetPosition anyway)
        internal const int  CharModel   = 0xBC;     // shared player model root
        internal const int  ClothList   = 0xC74;    // → 4 cloth ptrs; a zero stub skips cloth, a cave list adds it
        internal const int  DimFactor   = 0xCF0;    // <1.0 dims the model (fade lever)
        internal const int  LightFrom   = 0xD00;    // zero the point-light slots so the light loop skips
        internal const int  LightTo     = 0xD60;
        internal const int  TexAnimeOff = 0xDC;     // CTextureAnime — zero its active-ptr array so TexAnime
        internal const int  TexAnimeLen = 0x60;     // (called by DrawNPCDraw) no-ops instead of animating
                                                    // the SHARED textures the player also uses

        // ── Chara host — the DUNGEON's individual-character draw (the RIGHT one) ──────────────────────
        // Draw__11CSeireiKing (dun overlay, the dungeon scene draw) draws 6 charas: for i in 0..5, if
        // registry DAT_01d3d284[i] != 0 → ReloadTexture(group 0x20+i); TextureAnime; Draw__12CNPCharacter
        // (0x156540) on chara[i] = 0x1EA8460 + i*0x14A0. Draw__12CNPCharacter renders iff +0x146c!=0 (active)
        // && +0xbc!=0 (model) && +0xcec>0 (opacity), applying ambient tint +0xce0/4/8 + alpha +0xcec. So a
        // single clone: fill chara[0] (model=clone tree, pos +0x10, active +0x146c=1, opacity +0xcec), and
        // register it (DAT_01d3d284[0]=nonzero). Single instance, own texture group 0x20, no crowd/enemy AI.
        internal const long CharaArray    = 0x21EA8460;  // guest 0x01EA8460 (hardcoded in the dungeon draw)
        internal const int  CharaStride   = 0x14A0;
        internal const int  CharaSlots    = 6;
        internal const long CharaRegistry = 0x21D3D284;  // guest 0x01D3D284; entry i (int) at +i*4, !=0 → drawn
        internal const long CharaLoopGate = 0x202A3608;  // iGpffff9e18 (=_gp 0x2A97F0 -0x61E8, guest 0x2A3608): outer gate for the 6-chara draw loop; 0 in combat → set it
        internal const int  CharaTexBase  = 0x20;        // chara i's texture group = 0x20 + i
        internal const int  PlayerTexGroup = 0x1D;       // the dungeon reloads this group for the active player (Draw__11CSeireiKing) — source for the clone's textures
        internal const int  CharaActive   = 0x146C;      // int — Draw__12CNPCharacter requires !=0
        internal const int  CharaMotionA  = 0x1474;      // Step__12CNPCharacter runs the motion step only if
        internal const int  CharaMotionB  = 0x1478;      //   (+0x1474 || +0x1478) != 0 — zero both to freeze
        internal const int  CharaRampA    = 0x1484;      // Step__12CNPCharacter opacity-ramp amount (int); 0 = no ramp
        internal const int  CharaRampB    = 0x1488;      //   ramp amount B (engine sets =-1 each step → falls back to A)
        internal const int  CharaTint     = 0xCE0;       // float3 ambient ADD (tint); alpha/opacity = +0xCEC (NpcOpacity)

        // Motion-speed override (CCharacter+0xC60): Step__10CCharacter REPLACES the current motion's per-frame
        // advance with this when >0 (see [[player-motion-speed]]). The clone SHARES the player's motion structs
        // (+0xC20[i], copied), so the animation frame counter is shared: setting the clone's step to a near-zero
        // advance lets its SetMotionEX POSE the clone tree at the player's live frame without double-advancing it.
        internal const int  MotionSpeed   = 0xC60;

        // ── MotionParts host — the REAL character-draw pass ──────────────────────────────────────────
        // MainDraw draws 4 CMainChara slots (MotionParts, stride 0x11B0) every frame, each via vtable+0xac
        // (Draw__10CCharacter) with ITS OWN texture group (0x1b+slot) reloaded first. All 4 are empty
        // (modelRoot=0) during play — the active player is a separate object (0x21EA1D20). Filling one slot
        // gives a SINGLE clone instance, drawn like a real character (correct-texture pass), no crowd, no
        // reservation racing, no enemy AI. Position is direct: CCharacter +0x10 (Draw reads it). Empty a
        // slot by zeroing its model root (+0xBC) → Draw no-ops.
        internal const long MotionPartsBase   = 0x21D4F030;  // guest 0x01D4F030
        internal const int  MotionPartsStride = 0x11B0;
        internal const int  MotionPartsSlots  = 4;
        internal const int  MotionPartsTexBase = 0x1B;       // slot i's texture group = 0x1b + i

        internal const long ClothStub      = 0x21F33000;  // 16 zero bytes (clear of the 8KB aggro table @0x1F30000)
        internal const long ClothStubGuest = 0x01F33000;

        // FROZEN-CLOTH cave. Draw__10CCharacter (0x139310) auto-draws every non-null CCloth in the clone's +0xC74
        // list; nothing in the dungeon chara-loop STEPS them (ClothStep is town/menu/edit only), so a copied cloth
        // renders as a STATIC snapshot of the player's cloth at spawn. Each CCloth is a fixed 0x8550 object with no
        // internal cross-refs — the only pointers to fix are its three draw-packet fields (+0x18 active, +0x24/+0x28
        // double-buffer), which we point at a clone-owned buffer. The packet is WORLD-space (Draw__6CCloth draws at
        // origin), so the frozen drape lands where the player stood at cast = the decoy plant spot. A frozen cloth's
        // verts never change, so we SINGLE-buffer it (both +0x24/+0x28 → one buffer) — safe even mid-DMA (same bytes).
        // Lives in the node-pool tail reclaimed by MaxNodes 320→128 (0x21F54000, before MotionCave @0x21F78000).
        internal const long ClothListCave  = 0x21F54000;  // the 4-entry cloth-ptr array the clone's +0xC74 points at
        internal const uint ClothListGuest = 0x01F54000;
        internal const long ClothObjCave   = 0x21F54100;  // 3 object slots × 0x8550 (0x18FF0 → ends 0x21F6D0F0)
        internal const uint ClothObjGuest  = 0x01F54100;
        internal const long ClothBufCave   = 0x21F6E000;  // clone draw buffers, packed (before MotionCave @0x21F78000)
        internal const uint ClothBufGuest  = 0x01F6E000;
        internal const int  ClothObjSize   = 0x8550;      // fixed CCloth allocation (__nw 0x8550 in InitCloth)
        internal const int  ClothObjSlots  = 3;           // Ungaga has exactly 3 cloth pieces (dump: [3] null)
        internal const int  ClothMaxPieces = 4;           // +0xC74 list length
        internal const int  ClothActive    = 0x18;        // CCloth+0x18 = active draw packet ptr (set each frame)
        internal const int  ClothBuf0      = 0x24;        // CCloth+0x24 = DBuffID0 packet; +0x28 = DBuffID1
        internal const int  ClothAttach    = 0x3C;        // CCloth+0x3c = anchor CFrame (read when STEPPED — physics)

        // PHYSICS anchor cave: the cloth's attach CFrame (+0x3c) drives the sim (Step→GetLWMatrix walks +0x110
        // parents, recomputing world matrices on-demand). To make a STEPPED clone cloth follow the CLONE skeleton
        // instead of the player's, resolve each attach frame to clone-space: in-tree frames map by the same offset
        // the clone tree used (NodePool + (frame − modelRoot)); frames allocated PAST the contiguous tree are copied
        // here and re-parented to the clone's copy of their first in-tree ancestor. Sits after the cloth draw
        // buffers, before MotionCave (0x21F78000). Each CFrame is 0x270.
        internal const long ClothAnchorCave  = 0x21F73000;
        internal const uint ClothAnchorGuest = 0x01F73000;
        internal const long ClothAnchorEnd   = 0x21F75000;   // 0x2000 = ~12 CFrames (attach + bound out-of-tree frames)

        // PHYSICS collision: the cloth's CBound list (+0x44, linked via +0x00, each 0x130 bytes) is the body
        // collision capsules. Each capsule is positioned from two body bones at CBound +0xe4/+0xe8 (via GetLWMatrix
        // in UpDateDirPos__6CBound). For the clone we copy the list here and re-anchor each bound's +0xe4/+0xe8 to
        // the clone skeleton (same resolver as +0x3c), so the cloth collides against the CLONE's legs/hips, not the
        // player's. Shared across a character's cloth pieces (deduped by list head).
        internal const long ClothBoundCave   = 0x21F75000;
        internal const uint ClothBoundGuest  = 0x01F75000;
        internal const long ClothBoundEnd    = 0x21F78000;   // 0x3000 = ~37 CBounds
        internal const int  BoundSize        = 0x130;        // Sizeof__6CBound
        internal const int  BoundNext        = 0x00;         // linked-list next
        internal const int  BoundFrameA      = 0xE4;         // capsule endpoint bone A (CFrame*)
        internal const int  BoundFrameB      = 0xE8;         // capsule endpoint bone B (CFrame*)

        // Separate ROOT frame for the clone. Posing the shared root (via DrawNPCDraw's SetPosition) drags
        // the player (they share +0xBC). So we copy the player's root CFrame node into the cave and point
        // the clone at THE COPY, whose child ptr (+0x138) still targets the shared animated bone subtree —
        // the frame draw (DrawVu1__9CFrameVu1) is top-down (child world = accumulated matrix ∘ child local,
        // matrix passed down via RenderInfo; no parent pointer), so the shared bones render at the copy's
        // position while the player's own root stays put. Data only (MGDraw reads the node; the vtable it
        // calls @+0x250 points at real game code).
        internal const long RootBuf        = 0x21F33200;  // guest 0x01F33200; the clone's own root CFrame
        internal const long RootBufGuest   = 0x01F33200;
        internal const int  RootCopySize   = 0x800;       // full CFrameVu1 node (was 0x260 — truncated → didn't render)
        internal const int  RootChild      = 0x138;       // first child
        internal const int  RootSibling    = 0x13C;       // next sibling

        // ── Full independent frame-tree DEEP COPY (the real clone) ─────────────────────────────────
        // Root-only copies fail: a shared child's world matrix is built by GetLWMatrix (0x1281b0) walking
        // its PARENT ptr @+0x110 up the chain, and the shared children's parents still point at the
        // ORIGINAL player root → they render on the player, not the clone. The game's own CopyFrameVu1
        // (0x127610) deep-copies the whole tree: per node new CFrameVu1(0x270), operator= memcpy's
        // [0..0x260) + shares geometry @+0x260, zeroes links+cache, then SetParent relinks. We replicate
        // with PINE: DFS via child/sibling, copy 0x270 bytes/node into a cave pool, fix up
        // parent/child/sibling from an old→new map, share the mesh ptr @+0x260.
        internal const long NodePool      = 0x21F40000;   // clone node pool (proven-clean cave, clear of RootBuf/tables)
        internal const long NodePoolGuest = 0x01F40000;
        internal const int  NodeStride    = 0x270;        // MUST equal the real node size: MotionProc indexes
        internal const int  NodeSize      = 0x270;        //   bones as root+index*0x270, so the copy is contiguous
        internal const int  MaxNodes      = 128;          // pool cap (128*0x270 = 0x13800 → ends 0x21F53800). The clone
                                                          // is Ungaga-only (Mirage) = 67 nodes, so 128 is ~2× headroom;
                                                          // the reclaimed tail (0x21F54000+) now hosts the cloth cave.

        // Independent motion-struct cave. The CCharacter holds up to 8 motion "channels" as pointers at
        // +0xC20+i*4 (see GetMotionParam 0x1383b0); each points to a MOTION_TYPE whose embedded MOTION_STATE
        // (+0x10) is the animation frame counter Step advances. The clone copies the player's CCharacter, so it
        // SHARES these structs → both steps advance the same frame → 2× speed. Copy each active channel here and
        // repoint so the clone advances its OWN frame. Internal ptrs (+0x04/+0x08/+0x60/+0x64 → shared keyframe
        // data) stay as-is. Sits in the pool's clean tail (node pool ends 0x21F70C00, this < 0x21F7C000).
        internal const long MotionCave      = 0x21F78000;
        internal const long MotionCaveGuest = 0x01F78000;
        internal const int  MotionSlots     = 8;
        internal const int  MotionSlotBase  = 0xC20;      // CCharacter+0xC20+i*4 = channel[i] MOTION_TYPE ptr
        internal const int  MotionStructSize = 0xC0;      // > highest field used (+0x64); margin for safety
        internal const int  MotionSkinList  = 0x08;       // MOTION_TYPE+0x08 = MotionProc2 (skinning) list; +0x04 = rigid

        // Independent mesh cave. Only ~3 bones are CVisualMDTVu1 (software-skinned via MotionProc2); the rest
        // skin on VU1 at draw time (already per-instance). For each MDT bone, copy the visual (0x30) + VU data
        // (visual+0x1c bytes) + MDT block (MDT+0x08 bytes) here, rebase cross-refs, repoint node+0x260. Then the
        // clone skins its OWN buffers → fully independent body. ~75KB; sits in the proven-clean cave tail.
        internal const long MeshCave       = 0x21F80000;
        internal const long MeshCaveGuest  = 0x01F80000;
        internal const int  MeshCaveSize   = 0x34000;     // ~213KB (n66 VU alone is ~127KB); fits the clean tail 0x1F80000..0x1FB4300
        internal const int  VisualSize     = 0x30;        // CVisualMDTVu1
        internal const int  VisVU          = 0x18;        // visual+0x18 = VU data ptr, +0x1c = size
        internal const int  VisMDT         = 0x20;        // visual+0x20 = MDT ptr
        internal const uint MdtMagic       = 0x0054444D;  // "MDT\0" at MDT+0x00 (LE: 4D 44 54 00)
        internal const int  MdtSizeField   = 0x08;        // MDT+0x08 = total MDT block size

        // FRAME_INF = the per-bone skinning-matrix buffer (channel MOTION_TYPE +0x60), indexed bone*0xD0.
        // MotionProc2 READS and WRITES it, so the clone must own its own or it fights the player's bone matrices
        // (body "in two places at once"). One buffer per character, shared across channels → copy once, dedup.
        // Sits in the clean gap between the motion cave (0x1F78600) and the mesh cave (0x1F80000).
        internal const long FrameInfCave      = 0x21F79000;
        internal const long FrameInfCaveGuest = 0x01F79000;
        internal const int  FrameInfPtr       = 0x60;     // channel MOTION_TYPE +0x60 = FRAME_INF ptr
        internal const int  FrameInfEntry     = 0xD0;     // per-bone stride

        internal const int  MotionStateOff    = 0x10;     // MOTION_TYPE embedded MOTION_STATE (frame counter + interp)
        internal const int  MotionStateLen    = 0x40;     // bytes SetMotionEX reads/advances

        // Lever #1 test (dead): clone-private copy of the packet's +0x20 scratch. Region reused below.
        internal const long MeshScratchCave = 0x21F7D000;   // uncached MMU form (guest 0x01F7D000)
        internal const int  MeshScratchSlot = 0x1000;       // per-mesh slot

        // Per-bone ANIMATION-matrix buffer, referenced by the channel at +0x00 (MotionProc2 reads *(channel)+bone*0x40).
        // The clone copies the channel (the pointer) but shared the buffer → clone & player fight over the bone
        // matrices (the flicker + animation clamp). Copy it (once, dedup) and repoint channel+0x00. Reuses the dead
        // lever-#1 region (0x21F7D000, 0x3000 free).
        internal const long BoneMtxCave   = 0x21F7D000;
        internal const int  BoneMtxPtr    = 0x00;         // channel MOTION_TYPE +0x00 = per-bone matrix buffer ptr
        internal const int  BoneMtxEntry  = 0x40;         // per-bone stride

        // Equipped-weapon graft. The weapon is a separate object (iGpffff9d00 @ gp-0x6300 = MMU 0x202A34F0) whose
        // model root (+0xBC) is PARENTED to the player's hand bone (index 39) but drawn separately (not in the
        // player's +0xBC tree). To put it on the clone: deep-copy the weapon's small CFrame tree into WeaponCave,
        // then splice the copy's root into the CLONE's bone-39 child list so the clone's MGDraw draws it at the
        // clone's hand. Cave sits in the clean gap between BoneMtxCave (ends ~0x21F7E100) and MeshCave (0x21F80000).
        internal const long WeaponObjGlobal = 0x202A34F0;
        internal const long WeaponCave      = 0x21F7E200;
        internal const long WeaponCaveGuest = 0x01F7E200;
        internal const int  WeaponCaveSize  = 0x1D00;     // weapon trees are small (< ~12 nodes)
        internal const int  WeaponHandBone  = 39;         // player/clone frame-tree bone the weapon parents to
        // The weapon draws in its OWN chara slot (its own texgroup 0x1d pass, like the player's weapon), NOT
        // grafted into the body tree (which would share the body's 0x11 pass → wrong texture). Slot 3 because the
        // per-chara group formula is patched to (i*4 + 0x11): chara[0]=0x11 (body), chara[3]=0x1d (weapon). The
        // slot is drawn (registry @0x1D3D284) but NOT stepped (step-skip table @0x1D3D344, entry !=0 → skip).
        internal const int  WeaponCharaSlot = 3;
        internal const long StepSkipTable   = 0x21D3D344;  // ShisaiShadow step loop: entry i (int) !=0 → skip step i
        internal const int  TexIndex      = 0x100;        // CFrame node: texture index (short); <0 → DrawVu1 skips texture binding (flat/untextured)
        internal const int  NpcOpacity    = 0xCEC;        // CCharacter: model opacity 0..128 (Step__12CNPCharacter ramps it; Draw folds it into ambient alpha)
        internal const int  Parent        = 0x110;        // CFrame parent ptr (world-matrix chain walks this)
        internal const int  WorldCacheA   = 0x240;        // world-matrix-valid flag (reset to force recompute)
        internal const int  WorldCacheB   = 0x244;
        internal const int  GeomPtr       = 0x260;        // mesh/geometry object (SHARED, not copied)

        // Local (parent-relative) transform block: base matrix +0x1d0, scale +0x210, translation +0x220,
        // rotation +0x230 — a contiguous 0x70 run [0x1d0..0x240). GetLWMatrix composes this up the parent
        // chain into +0x140/+0x150. Copying this block from the live player node into the copied clone node
        // (and zeroing the world cache) re-poses the clone's rigid parts (head/hands) to the player each tick.
        internal const int  LocalTRS      = 0x1d0;
        internal const int  LocalTRSLen   = 0x70;

        // CCharacter current-motion id. Step__10CCharacter (0x138530) early-outs when this is < 0 → no
        // SetMotionEX, so the model keeps its current bone pose = FROZEN. Setting the host slot's motion id
        // to -1 stops the enemy re-posing our foreign clone tree (the likely delayed-crash cause) and holds
        // the snapshot stance.
        internal const int  MotionId      = 0xC68;
        internal const int  MotionFlags   = 0xC64;   // Step__10CCharacter per-step motion flags; bit2 (0x4) = clean
        internal const int  MotionRestart = 0x4;     //   restart (reset to frame 0, no blend), consumed once by the step
    }

    /// <summary>
    /// Mirage clone HEAT-HAZE. DrawRaster__9CFireOmni (main segment, 0x162310) is the game's torch heat
    /// shimmer: it projects the fire's world position (+0x20/24/28) to screen and re-blends the framebuffer
    /// rectangle there, wobbled by an animated phase (+0x04). We give it a cave-allocated CFireOmni parked
    /// at the clone, and hook the TAIL of DrawFire__11CDungeonMap (its jr ra @0x1C4600) to call DrawRaster
    /// once more when a mod flag is set. Render order (dun overlay): map → player+clone → enemies → RASTER
    /// → DrawFire — so the clone is already in the framebuffer when the haze pass runs. Main-segment, once.
    /// Note: DrawRaster's projection scale is a hardcoded torch size, so the shimmer is a fixed-size patch.
    /// </summary>
    internal static class MirageHaze
    {
        internal const long HookPatchAddr = 0x201C4600;   // MMU addr of DrawFire's `jr ra`
        internal const uint  HookOrig     = 0x03E00008;    // jr ra
        internal const uint  HookNew      = 0x087CC700;    // j 0x1F31C00
        internal const long HookStubAddr  = 0x21F31C00;    // guest 0x01F31C00
        internal static readonly uint[] HookStub =
        {
            0x27BDFFF0, 0xAFBF0000, 0x3C0201F3, 0x8C421BC0, 0x10400005, 0x00000000, 0x3C0401F3,
            0x34841B80, 0x0C0588C4, 0x00000000, 0x8FBF0000, 0x27BD0010, 0x03E00008, 0x00000000,
        };

        internal const long Instance   = 0x21F31B80;   // guest 0x01F31B80; the CFireOmni (0x40 bytes)
        internal const long Flag        = 0x21F31BC0;  // int: 1 = draw the haze
        internal const int  PhaseOff   = 0x04;         // raster wobble phase (0..8)
        internal const int  PosOff     = 0x20;         // x,y,z
        internal const int  RadiusOff  = 0x0C;         // 15.0f from the ctor
    }

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
}
