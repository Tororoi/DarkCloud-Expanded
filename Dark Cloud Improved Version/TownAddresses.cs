namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The town / Georama editor's runtime state: the part definitions parsed out of
    /// <c>gedit\&lt;town&gt;\mapinfo.cfg</c>, the placed parts, the animated water surfaces, and the
    /// texture banks the town loaded.
    ///
    /// All vanilla. See docs/custom-fishing-spot.md for how these fit together.
    /// </summary>
    internal static class EditInfo
    {
        /// <summary>ELF <c>edit_info</c> — a POINTER to the big EDIT_MAP_INFO buffer that
        /// <c>mapinfo.cfg</c> is parsed into. Everything else in this class is an offset from the
        /// buffer it points at, so always <see cref="Base"/> through this.</summary>
        internal const long EditInfoPtr = 0x202A27B0;

        /// <summary>ELF <c>texture_list</c> — how many texture banks the town declared
        /// (GRD_IMG / BLD_IMG / WATER_IMG / ...). Entries at <see cref="TextureBase"/>.</summary>
        internal const long TextureCount = 0x202A27B4;

        /// <summary>ELF <c>water_list</c> — how many animated water surfaces the town declared.
        /// Hard cap of <see cref="MaxWaterSurfaces"/>; several towns use ZERO of them.</summary>
        internal const long WaterCount = 0x202A27D8;

        /// <summary>ELF <c>mapobj_list</c> — how many static objects (the cfg's WATER / GROUND
        /// entries) the town declared.</summary>
        internal const long MapObjCount = 0x202A27BC;

        /// <summary>ELF <c>now_parts_no</c> — the part id the cfg parser was last working on.</summary>
        internal const long NowPartsNo = 0x202A27FC;

        /// <summary>Resolve the EDIT_MAP_INFO buffer, or 0 if no town is loaded.</summary>
        internal static long Base()
        {
            uint p = Memory.ReadUInt(EditInfoPtr) & Memory.PhysAddrMask;
            return Memory.IsValidGuest(p) ? Memory.ToMmu(p) : 0;
        }

        // ---- texture banks (CommandIMGSub 0x175060) -----------------------------------------
        internal const long TextureBase   = 0x9E0;   // from Base()
        internal const int  TextureStride = 0x48;
        internal const int  TextureName   = 0x00;    // char[0x40] — a full path, e.g. "gedit/e03/e03b01.img"
        internal const int  TextureKind   = 0x40;    // 1 = GRD_IMG, 2 = BLD_IMG, ...
        internal const int  TextureFlag   = 0x44;

        // ---- animated water surfaces (CommandWATER_SURFACE 0x1757E0) -------------------------
        // The cfg line maps one-for-one onto this record. `+WaterHeight` is the surface's Y — the
        // single most useful number when siting a fishing spot.
        internal const long WaterBase        = 0x17CD0;  // from Base()
        internal const int  WaterStride      = 0xC0;
        internal const int  MaxWaterSurfaces = 8;
        internal const int  WaterFrameName   = 0x00;   // char[16]; "" = not bound to a named frame
        internal const int  WaterGridW       = 0x10;
        internal const int  WaterGridH       = 0x14;
        internal const int  WaterPartNo      = 0x18;
        internal const int  WaterCornerA     = 0x20;   // float x,y,z (+0x2C = 1.0)
        internal const int  WaterCornerB     = 0x30;   // float x,y,z (+0x3C = 1.0)
        internal const int  WaterOffset      = 0x40;   // float x,y,z (+0x4C = 1.0)
        internal const int  WaterHeight      = 0x44;   // == WaterOffset + 4 — the surface's Y
        internal const int  WaterColorR      = 0x50;   // R, G, B at +0x50/+0x54/+0x58 — the water is TINTED
        internal const int  WaterWaveAmp     = 0x60;   // amp, freq, flow speed, scale
        internal const int  WaterFlags       = 0x70;

        // ---- part definitions (Command*_PARTS -> LoadObjectParts 0x182390) -------------------
        internal const long PartBase   = 0xDD10;   // from Base()
        internal const int  PartStride = 0x2D8;
        internal const int  MaxParts   = 46;       // LoadObjectParts walks 0..0x2D; only 0..23 become templates
        internal const int  PartName   = 0x00;     // the model name, resolved in scene.scn by SearchPTS
        internal const int  PartType   = 0x244;    // -> CMapParts.Type. See PartType constants below.

        /// <summary>What the cfg directive maps to. Only 2/3/4/5 are rendered as water by
        /// <c>CEditGround::DrawWater</c>.</summary>
        internal static class PartTypes
        {
            internal const int Building = 0;   // BLD_PARTS / GRD_PARTS
            internal const int Road     = 1;   // ROAD_PARTS
            internal const int River    = 2;   // RIVER_PARTS      — water
            internal const int Bridge   = 3;   // BRIDGE_PARTS     — water
            internal const int Lake     = 4;   // LAKE_PARTS       — water (a 2x2 patch); the fishing ponds
            internal const int OnRiver  = 5;   // ON_RIVER_PARTS   — water

            internal static bool IsWater(int t) => t >= River && t <= OnRiver;

            internal static string Name(int t) => t switch
            {
                Building => "BLD/GRD",
                Road     => "ROAD",
                River    => "RIVER",
                Bridge   => "BRIDGE",
                Lake     => "LAKE",
                OnRiver  => "ON_RIVER",
                _        => $"?({t})",
            };
        }
    }

    /// <summary>
    /// <c>CEditGround</c> — the Georama ground. Owns the placed parts, the static scenery objects and
    /// the base terrain, and it is what <c>PickUpPoly</c> gathers a fishing rectangle's collision from.
    /// </summary>
    internal static class EditGround
    {
        /// <summary>ELF <c>pEditGround</c> — pointer to the live CEditGround.</summary>
        internal const long EditGroundPtr = 0x202A28D8;

        internal static long Base()
        {
            uint p = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            return Memory.IsValidGuest(p) ? Memory.ToMmu(p) : 0;
        }

        internal const long PlacedBase    = 0x30;      // CMapParts[128]
        internal const int  PlacedCount   = 128;
        internal const long TemplateBase  = 0x15F30;   // CMapParts[24] — one per part type
        internal const int  TemplateCount = 24;
        internal const long ExtraBase     = 0x15F40;   // CMapParts[64] — the cfg's static WATER/GROUND objects
        internal const int  ExtraCount    = 64;
    }

    /// <summary>
    /// <c>CMapParts</c> — one placed Georama piece. 0x2A0 bytes. Placement is literally
    /// <c>memcpy(slot, template, 0x2A0)</c> plus a position and a rotation, which is why the mod can
    /// place one with plain field writes.
    /// </summary>
    internal static class MapParts
    {
        internal const int Stride = 0x2A0;

        internal const int Position = 0x10;    // float x, y, z — what CMapParts::SetPosition writes
        internal const int VTable   = 0xA0;
        internal const int Occupied = 0xE8;    // < 0 means the slot is FREE
        internal const int PartId   = 0xF0;
        internal const int AreaCode = 0xF4;
        internal const int ModelPtr = 0x108;   // must be non-zero for the part to place
        internal const int Type     = 0x118;   // see EditInfo.PartTypes
        internal const int State    = 0x148;
        internal const int UnitSize = 0x1D0;

        internal static long Slot(long groundBase, int i) => groundBase + EditGround.PlacedBase + i * Stride;
        internal static bool IsFree(long slotAddr) => Memory.ReadInt(slotAddr + Occupied) < 0;
    }

    /// <summary>
    /// The town main loop (<c>EditLoop</c> @ 0x17A000-ish) and the gate on the Select-button Georama
    /// bird's-eye camera.
    ///
    /// The camera is not a Georama feature as such — it is a camera. But the engine only offers it on
    /// the first five maps:
    ///
    /// <code>
    /// if (((0 &lt; EdDebugMoveFlag) || (MapNo &lt; 5)) &amp;&amp;
    ///      EdPadDown(0x100, 2) &amp;&amp; !EdCheckViewMode() &amp;&amp; !change_time_event)
    /// {
    ///     if (GameMode == 1) { GameMode = 4; /* copy follow-cam into EditCamera */ }
    /// }
    /// </code>
    ///
    /// <see cref="EdDebugMoveFlag"/> is a leftover developer flag that bypasses the map check outright,
    /// so a single positive write offers the overhead view on ANY map.
    ///
    /// CAUTION: <c>GameMode = 4</c> runs <c>MainEditMode</c>, which calls <c>EffectTask(pEditGround)</c>,
    /// <c>MakePartsBox(pEditGround)</c> and <c>MoveEditCursor()</c>. On a map with no <c>EDITAREA</c>
    /// (Yellow Drops s13, Brownboo s04) the CEditGround's four CEditArea slots are NULL, and the cursor
    /// code walks that grid — so a null dereference is plausible. Untested; try it and see.
    /// </summary>
    internal static class EditLoop
    {
        /// <summary>
        /// ELF <c>EdDebugMoveFlag</c> — a leftover developer flag, and a GRADED one:
        ///
        /// <list type="table">
        /// <item><term>0</term><description>stock behaviour</description></item>
        /// <item><term>1</term><description>debug movement: a run button doubles speed
        ///   (<c>EdMoveChara</c>), the Select-button overhead camera is offered on ANY map
        ///   (<c>EditLoop</c>), and <c>CheckEditToWalk</c> stops validating where you exit</description></item>
        /// <item><term>2+</term><description>ALSO bypasses <c>MoveCheck</c> — i.e. noclip. Do not set this
        ///   unless you mean it.</description></item>
        /// </list>
        ///
        /// One global, read by three systems (<c>EditLoop</c>, <c>EdMoveChara</c> ×6,
        /// <c>CheckEditToWalk</c>), which is why turning it on for the camera also changes movement and
        /// exit placement. Use 1; see <see cref="CheckEditToWalkFlagRead"/> to keep the good parts
        /// without the exit bug.
        /// </summary>
        internal const long EdDebugMoveFlag = 0x202A273C;

        /// <summary>
        /// The single instruction in <c>CheckEditToWalk</c> (0x17FF3C) that loads
        /// <see cref="EdDebugMoveFlag"/>: <c>lw v0, -28852(gp)</c>, immediately followed by
        /// <c>bgtz v0, +63</c> — "if the flag is on, skip the whole validation block".
        ///
        /// That block is what keeps exiting the overhead camera sane: it checks the exit point is inside a
        /// real edit area and close to its ground, and — the part that actually bites — it SNAPS the player
        /// onto the surface it hit. With the flag on, none of it runs, so you get dropped at the cursor's raw
        /// height in corners and on rooftops.
        ///
        /// Forcing <c>v0 = 0</c> here makes the flag read as OFF **in this function only**, restoring the
        /// ground-snap and the area check while leaving the fast run and the overhead camera intact. One
        /// instruction, in place, no branch retargeting — the same discipline as the Macho ABS display patches.
        /// </summary>
        internal const long CheckEditToWalkFlagRead = 0x2017FF3C;
        internal const uint CheckEditToWalkOrig     = 0x8F828F4C;  // lw    v0, -28852(gp)
        internal const uint CheckEditToWalkPatch    = 0x24020000;  // addiu v0, zero, 0

        /// <summary>ELF <c>MapNo</c>. The camera is offered natively only for 0..4 — note that is FIVE,
        /// even though Georama part data exists for six maps (see <see cref="GeoramaTables"/>).</summary>
        internal const long MapNo = 0x202A2518;

        /// <summary>ELF <c>GameMode</c>. 1 = walking, 4 = the Georama overhead camera, 0xB = fishing.</summary>
        internal const long GameMode = 0x202A1F50;

        internal const int GameModeWalking  = 1;
        internal const int GameModeOverhead = 4;

        internal const long ECursorFrame      = 0x202A1F60; // CFrame* — the edit cursor
        internal const long EditCamera        = 0x21D34840; // CCameraFollow, 752 bytes
        internal const long ChangeTimeEvent   = 0x202A28A0;
        internal const long EditArea          = 0x202A28DC;

        /// <summary>ELF <c>EditDataDir</c>, 256 bytes — the CURRENT town's data folder, e.g.
        /// <c>"gedit/e03/"</c>. This is the honest "a new town has finished loading" signal:
        /// <see cref="MapNo"/> flips as soon as the transition starts, while <c>edit_info</c> still
        /// holds the PREVIOUS town's parsed cfg. Trigger off this, not off MapNo, or you will dump
        /// the old town's data under the new town's name.</summary>
        internal const long EditDataDir = 0x21D1B3E0;

        /// <summary>ELF <c>EditMapName</c>, 32 bytes.</summary>
        internal const long EditMapName = 0x21D1B4E0;

        /// <summary>ELF <c>Chara</c> — pointer to the player's town CCharacter. Its CFrame position is
        /// at <c>+0x10</c> as (x, y, z) — the same layout <c>CFrame::SetPosition</c> writes. This is
        /// unambiguous, unlike the loose position globals, whose naming does not survive inspection.</summary>
        internal const long CharaPtr = 0x202A1F54;

        /// <summary>Offset of the (x, y, z) position within a CCharacter/CFrame.</summary>
        internal const int CharaPosition = 0x10;
    }

    /// <summary>
    /// The static Georama part / Atla tables. Both are hard-capped at SIX maps by explicit bounds
    /// checks that return NULL — <c>GetEditAtraPartsData</c> (0x158E00) and <c>GetEditAtraData</c>
    /// (0x158D80) both start with <c>if (map &lt; 0 || 5 &lt; map) return NULL;</c>. All six slots are
    /// populated in the retail build, so there is no free slot for a seventh Georama town.
    /// </summary>
    internal static class GeoramaTables
    {
        internal const long EditPartsData   = 0x202540D0; // 6 maps x 25 parts x 188 B = 28,200
        internal const int  PartsMapStride  = 0x125C;     // 25 * 0xBC
        internal const int  PartsRecord     = 0xBC;
        internal const int  MaxPartsPerMap  = 24;         // bounds check is `0x17 < part`

        internal const long EditElementData = 0x2025AF00; // 6 maps x 100 elements x 12 B = 7,200
        internal const int  ElemMapStride   = 0x4B0;
        internal const int  ElemRecord      = 12;
        internal const int  MaxElemsPerMap  = 100;

        internal const int  MaxGeoramaMaps  = 6;
    }

    /// <summary>
    /// The town's EVENT POINTS — the things you walk up to and press a button on. A real fishing spot is one
    /// of these (not an NPC: talking to an NPC goes through <c>EdTalkModeInit</c>, which is the dialogue/.mes
    /// path and never runs a script).
    ///
    /// <c>EdMoveChara</c> drives them:
    /// <code>EdGetEvent(NowTime, EventArray, EventCount, EventParam, ...)</code>
    /// and an event whose param is 2 fires when the action button is pressed.
    ///
    /// This is the hook a custom fishing spot needs, because the fishing setup CANNOT be faked with writes —
    /// <c>_LOAD_FISHING_DATA</c> loads <c>chara/fishing.pak</c>, allocates six CFish, and gathers collision
    /// polys. The game has to execute STB commands 999 and 998, so something must run a script, and an event
    /// point is the thing that does.
    /// </summary>
    internal static class EventPoints
    {
        /// <summary>Pointer to the <c>ED_EVENT_POINT</c> array (native ptr).</summary>
        internal const long ArrayPtr = 0x21D19700;

        /// <summary>How many entries the array holds.</summary>
        internal const long Count = 0x21D19704;

        internal const int Stride = 0x90;

        /// <summary>Event type. <b>0 means the slot is FREE</b> — <c>GetNewEventPoint</c> scans for the first
        /// entry with 0 here, starting at index 1 (index 0 is reserved).</summary>
        internal const int Type = 0x10;

        /// <summary>Item id, for the event types that hand something over.</summary>
        internal const int ItemId = 0x1C;

        /// <summary>The matched event's params, filled in by <c>EdGetEvent</c>. When this reads 2 and the
        /// action button is down, the event fires.</summary>
        internal const long MatchedParam = 0x21D196A0;

        /// <summary>The <c>ED_EVENT_POINT</c> that <c>EdGetEvent</c> matched (native ptr).</summary>
        internal const long MatchedPoint = 0x21D196F0;

        internal static long Slot(long baseAddr, int i) => baseAddr + i * Stride;

        /// <summary>Resolve the array, or 0.</summary>
        internal static long Base()
        {
            uint p = Memory.ReadUInt(ArrayPtr) & Memory.PhysAddrMask;
            return Memory.IsValidGuest(p) ? Memory.ToMmu(p) : 0;
        }
    }

    /// <summary>
    /// The town's loaded <c>event.stb</c> — the script the event points run.
    ///
    /// <c>LoadScript</c> does <c>EdEventData = EdScriptBuffer; LoadFile2(dir + "event.stb", EdEventData, ...)</c>,
    /// so <see cref="EdEventData"/> is a pointer straight at the STB image in RAM. The mod already patches STB
    /// bytecode elsewhere (HarderEnemyAI), and the VM is fully reverse-engineered, so injecting a fishing
    /// sequence here is tractable — see docs/custom-fishing-spot.md.
    ///
    /// Header (12-byte vmcodes; label table of {id, codeOffset} pairs):
    /// <code>
    /// +0x08  u32  codeBase
    /// +0x0C  u32  labelTableOffset
    /// +0x10  u32  labelCount
    /// label[i] = { u32 id, u32 codeOffset }   // the code itself starts at codeOffset + 8
    /// </code>
    /// </summary>
    internal static class TownScript
    {
        /// <summary>ELF <c>EdEventData</c> — native ptr to the loaded event.stb image.</summary>
        internal const long EdEventData = 0x202A2854;

        internal const int CodeBase    = 0x08;
        internal const int LabelTable  = 0x0C;
        internal const int LabelCount  = 0x10;
        internal const int LabelStride = 0x08;   // { u32 id, u32 codeOffset }
        internal const int LabelCodeSkip = 0x08; // code begins at codeOffset + 8

        internal static long Base()
        {
            uint p = Memory.ReadUInt(EdEventData) & Memory.PhysAddrMask;
            return Memory.IsValidGuest(p) ? Memory.ToMmu(p) : 0;
        }
    }

    /// <summary>
    /// <c>CFrame</c> — one node of a loaded model's hierarchy. The mod already drives these for weapon
    /// models; the same handles let it show, hide, move and scale ANY named frame in ANY loaded model.
    /// </summary>
    internal static class MapFrame
    {
        /// <summary>u16 visibility flag. <c>CMapObject::FrameObjectOnOff</c> (0x157590) resolves a frame
        /// by name and writes exactly this — so hiding a frame is a single 2-byte write.</summary>
        internal const int DrawFlag = 0xB0;

        /// <summary>The frame's name, as <c>LoadMDSFile</c> strcpy'd it out of the .mds node.</summary>
        internal const int Name = 0x118;
    }
}
