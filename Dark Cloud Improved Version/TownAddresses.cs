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

        /// <summary>ELF <c>GameMode</c> — the town's top-level state machine.</summary>
        internal const long GameMode = 0x202A1F50;

        internal const int GameModeWalking  = 1;
        internal const int GameModeOverhead = 4;

        /// <summary>Event mode. <c>EventMode__Fv</c> (0x17E970) runs ONLY in this mode, and it is the sole
        /// consumer of <see cref="EventReturnCode"/> — so an event script that never reaches 0xE can set any
        /// return code it likes and nothing will ever act on it.</summary>
        internal const int GameModeEvent = 0xE;

        /// <summary>
        /// FISHING is <c>GameMode 0x10</c> — NOT 0xB, which is what the mod used to say here.
        ///
        /// 0xB is the event RETURN CODE that asks for fishing; <c>EventMode</c> turns it into the mode with
        /// <c>case 0xb: GameMode = 0x10;</c>. Independently confirmed by <c>MoveChara</c>, which computes
        /// <c>DAT_01d19714 = (GameMode == 0x10)</c> — and 0x1D19714 is the flag the mod has long called
        /// <c>FishingAddresses.Active</c>. So "Active" is precisely "GameMode is 0x10".
        /// </summary>
        internal const int GameModeFishing = 0x10;

        /// <summary>
        /// ELF <c>DAT_01d3d618</c> — an event script's RETURN CODE, not a mode. <c>EdInitEventParamSimple</c>
        /// zeroes it when an event starts; the script sets it (<c>_GOTO_FISHING</c> = 0xB, <c>_MAP_JUMP</c>,
        /// <c>_GOTO_INTERIOR</c>, <c>_GOTO_OUTSIDE</c> all write it); <c>EdEventMode</c> sees
        /// <c>if (0 &lt; code) EdEventFinish()</c> and RETURNS it; and <c>EventMode</c> switches on it:
        /// <c>4</c> = interior, <c>5</c> = dungeon, <c>9</c> = menu, <c>0xB</c> = fishing, default = walking.
        ///
        /// The catch, and the thing that cost a day: that whole chain hangs off <c>EventMode</c>, which only
        /// runs in <see cref="GameModeEvent"/>. Setting this code from a script that is NOT running as a
        /// real event leaves the value sitting there, read by nobody.
        /// </summary>
        internal const long EventReturnCode = 0x21D3D618;

        internal const int ReturnCodeFishing = 0xB;

        internal const long ECursorFrame      = 0x202A1F60; // CFrame* — the edit cursor
        internal const long EditCamera        = 0x21D34840; // CCameraFollow, 752 bytes

        /// <summary>
        /// The MAIN follow camera (ELF <c>MainCamera</c>) — the one walking and fishing use, and the one
        /// <c>EventMode</c> drives with <c>FollowOn</c> / <c>SetFollow</c> / <c>SetAngleSoon</c>. Not to be
        /// confused with <see cref="EditCamera"/>, nor with the event camera at 0x21D35140.
        /// </summary>
        internal const long MainCamera = 0x21D34540;

        /// <summary>CCameraFollow's yaw. <c>SetAngle</c> (0x124B20) writes only <see cref="CameraAngle"/>
        /// (so the camera swings around to it); <c>SetAngleSoon</c> (0x124B30) writes BOTH, which snaps it
        /// there instantly. Behind-the-player is simply angle = the player's yaw — that is precisely what
        /// EventMode does when it returns you to walking: <c>SetAngleSoon(charaYaw + offset)</c>.</summary>
        internal const int CameraAngle    = 0x2D8;
        internal const int CameraAngleNow = 0x2DC;
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

        /// <summary>
        /// Offset of the (x, y, z) rotation, so <c>+0x64</c> is the YAW. From
        /// <c>GetRotation__7CObject</c> / <c>SetRotation__7CObject</c> (0x156EF0 / 0x156DE0), which read and
        /// write <c>this + 0x60/0x64/0x68</c>. Same object as <see cref="CharaPosition"/>.
        ///
        /// NOT 0x230. That is <c>CFrame</c>'s euler cache, and <c>GetRotation__6CFrame</c> only trusts it when
        /// the flag at <c>+0x244</c> is clear — for the player it is not, so 0x230 reads a constant zero. It
        /// read exactly 0.000 through a whole session of the player visibly turning.
        /// </summary>
        internal const int CharaRotation = 0x60;
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

        /// <summary><b>Must be non-zero or the point is ignored.</b> <c>CheckEventPoint</c> opens with
        /// <c>if (*point == 0) return 0;</c>. Every live point reads 1 here. Forgetting it makes an
        /// otherwise-correct event point silently do nothing.</summary>
        internal const int Enabled = 0x00;

        /// <summary>If &gt; 0, <c>CheckEventPoint</c> gates the point on <c>EdGetMapFlag</c> — i.e. "already
        /// done". Zero it for a point that should always fire.</summary>
        internal const int MapFlag = 0x08;

        /// <summary>Event type. <b>0 means the slot is FREE</b> — <c>GetNewEventPoint</c> scans for the first
        /// entry with 0 here, starting at index 1 (index 0 is reserved).</summary>
        internal const int Type = 0x10;

        /// <summary>
        /// <b>Georama PART INDEX — and the field that silently swallows a hand-made event point.</b>
        ///
        /// <c>EdGetEvent</c>:
        /// <code>
        /// if (point[0x0C] >= 0) {
        ///     parts = GetPartsObject(EditGround, ...);
        ///     if (parts == 0) goto skip;          // part not placed -> the point is SKIPPED
        ///     GetPosRot(parts, pos, rot);         // Position becomes PART-RELATIVE
        /// }
        /// </code>
        ///
        /// So a point with a part index is anchored to that part (which is why Norune's fishing trigger
        /// reads <c>(40, 0, 96)</c> — part-local, attached to the lake). <b>Set this to -1</b> for a
        /// free-standing point whose <see cref="Position"/> is plain world space.
        /// </summary>
        internal const int PartIndex = 0x0C;

        /// <summary>Optional <c>CMapObject*</c>. If <see cref="PartIndex"/> is negative and this is set, the
        /// position is taken from this object instead. 0 for a free-standing point.</summary>
        internal const int ObjectPtr = 0x14;

        /// <summary>Optional CFrame*. If set, <c>CheckEventPoint</c> gates the point on that frame's draw
        /// flag (<c>frame[0xB0] &amp; 1</c>) — so a hidden object's event does not fire. 0 = no gate.</summary>
        internal const int FramePtr = 0x18;

        /// <summary>
        /// Overloaded by <see cref="Type"/>, and this is the crux of the whole fishing trigger:
        ///
        ///   type 2 — an ITEM id (the searchable barrels and pots).
        ///   type 3 — a <b>SCRIPT LABEL id</b>. <c>EdMoveChara</c> does
        ///            <c>if (matchedParam == 3 &amp;&amp; point[0x1C] &gt; 0) label = point[0x1C];</c>
        ///            and then <c>ScriptLabelRequest = label</c>. Walking into the point runs that label.
        ///
        /// Norune's fishing sign is a type-3 point whose label is <b>256</b> — exactly the label that holds
        /// <c>_LOAD_FISHING_DATA</c> / <c>_GOTO_FISHING</c> in <c>gedit\e01\event.stb</c>. That is the whole
        /// mechanism, and it means a custom fishing spot needs only two data writes: a type-3 event point at
        /// the water, and the fishing bytecode at whatever label it names.
        /// </summary>
        internal const int ItemOrLabel = 0x1C;

        // Decoded from a live dump, then confirmed by catching a real fishing spot loading in Norune.
        // NOTE: type-3 points are NOT in the door list — the dump at town load shows only type-1 interior
        // doors. The fishing point's position (40, 0, 96) is PART-LOCAL: it is attached to the lake part.
        internal const int Name     = 0x30;   // char[] — for doors, the target map ("i01h06", "dungeon")
        internal const int Position = 0x50;   // float x, y, z  (world if PartIndex < 0, else part-local)

        /// <summary>A scalar <b>RADIUS</b>, not a box: <c>EdGetEvent</c> does
        /// <c>if (DistVector(pos, player) &lt; *(float*)(point + 0x60))</c>.</summary>
        internal const int Radius = 0x60;

        internal const int Rotation = 0x70;   // float x, y, z

        /// <summary>Event types seen live.</summary>
        internal const int TypeFree   = 0;   // slot is unused
        internal const int TypeDoor   = 1;   // map jump; fires on walk-in
        internal const int TypeItem   = 2;   // press the action button; +0x1C is an item id
        internal const int TypeScript = 3;   // +0x1C is a SCRIPT LABEL — this is what a fishing sign is
        internal const int TypeLadder4 = 4;  // EdInitHashigo (ladders)
        internal const int TypeLadder5 = 5;

        /// <summary>
        /// ELF <c>0x21D19708</c> (the mod already calls this <c>FishingAddresses.OverworldState</c>) — the
        /// script LABEL the town is being asked to run, or -1 for none. <c>EdMoveChara</c> writes it every
        /// frame from the matched event point, so writing it directly is a race; create a type-3 event point
        /// instead and let the engine do it.
        /// </summary>
        internal const long ScriptLabelRequest = 0x21D19708;

        /// <summary>The label the engine falls back to (0x100). Norune's fishing script lives here.</summary>
        internal const int DefaultFishingLabel = 256;

        /// <summary>
        /// Labels the ENGINE requests by number while fishing. <c>EdMoveChara</c>'s <c>chara_fishing</c>
        /// state machine writes them straight into <see cref="ScriptLabelRequest"/> on a button press:
        /// Circle (pad 0x20) asks for 133, Square (pad 0x80) asks for 134. Cross (0x40) casts and needs no
        /// script.
        ///
        /// These ids are NOT negotiable — a town that lacks them has no way to quit fishing or change bait,
        /// because the request names a label that does not exist and the event simply never starts.
        /// </summary>
        internal const int FishingExitLabel = 133;   // "quit fishing?"
        internal const int FishingBaitLabel = 134;   // the bait menu

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
    /// <summary>
    /// The background (disc) file reader. <c>_LOAD_ITEM_FILE</c> issues <c>LoadFileBG</c> reads and returns
    /// immediately; <c>_LOAD_ITEM</c> then builds an item frame from the buffer — and if the read has not
    /// landed, it builds one out of nothing and the game dies calling through a garbage pointer
    /// ("Jump to unaligned address").
    ///
    /// The trap is that <c>GetReadBGFile</c> tests <c>entry[0]</c>, which <c>LoadFileBG</c> sets at QUEUE
    /// time — so it reports a slot as available while the read is still in flight. <c>ReadBG</c> shows the
    /// real state machine:
    ///
    /// <code>
    ///   entry[0] = 1                       // queued        (LoadFileBG)
    ///   entry[1] = sceCdRead handle        // read issued   (ReadBG)
    ///   entry[2] = 1                       // COMPLETE      (ReadBG, after sceCdSync with no error)
    /// </code>
    ///
    /// <c>entry[2]</c> is the only honest completion flag, and it is what the mod polls to release a script
    /// waiting on a load — instead of guessing at a frame count, which is a race that crashes when lost.
    /// </summary>
    /// <summary>
    /// The villager/fishing memory pool — a <c>CDataAlloc2</c> bump allocator (base, used, capacity), all
    /// counts in 0x10-byte blocks. <c>Alloc</c> hangs (<c>while(true)</c>) if <c>used + size &gt; capacity</c>.
    ///
    /// This is the pool <c>_LOAD_MAIN_CHARA(turi, flag=1)</c> AND <c>_LOAD_FISHING_DATA</c> allocate from, so
    /// the 1.73 MB fishing model has to fit here. <c>_CLEAR_VILLAGER_BUFF</c> recomputes
    /// <c>capacity = BaseBuffer.capacity - BaseBuffer.used</c> — i.e. whatever the parent buffer has free —
    /// so a town with more resident data gets a SMALLER fishing pool. Reading this tells us whether the model
    /// fits, instead of finding out by crashing.
    /// </summary>
    /// <summary>
    /// FishingInitFish places fish at <c>WaterLevel - 12</c>, where the 12.0 is built INLINE as
    /// <c>lui r2, 0x4140</c> (0x41400000 = 12.0) at 0x001A94D4 — not a data constant. Patching that one
    /// instruction's immediate changes the fish depth: e.g. 0x40C0 = 6.0, 0x4040 = 3.0. In-place, one
    /// instruction, restored on town change — same class as the other cold-window code patches we ship.
    /// </summary>
    internal static class FishDepthPatch
    {
        internal const long Instr = 0x201A94D4;
        internal const uint Original = 0x3C024140;   // lui r2, 0x4140  (= 12.0)
        internal static uint For(float depth)        // lui r2, hi16(depth as float)
        {
            uint bits = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(depth), 0);
            return 0x3C020000u | (bits >> 16);
        }
    }

    internal static class FishingPool
    {
        internal const long Base     = 0x21D1B360;   // guest pointer to the pool memory
        internal const long Used     = 0x21D1B368;   // blocks in use (x0x10 = bytes)
        internal const long Capacity = 0x21D1B36C;   // block capacity (x0x10 = bytes)
        internal const int  BlockSize = 0x10;

        internal const int TuriModelBytes = 1814240; // chara/c01d_turi.chr — must fit
    }

    internal static class BgRead
    {
        internal const long Table  = 0x21CBB0C0;   // bg_read_info
        internal const int  Stride = 0x9C;         // 0x27 ints
        internal const int  Slots  = 32;

        internal const int InUse    = 0x00;        // set when QUEUED — NOT a completion signal
        internal const int Complete = 0x08;        // entry[2] — set when the read has actually landed
    }

    /// <summary>
    /// The event script's VM object (ELF <c>CRunScript</c>) — <c>EdEventInit</c> calls
    /// <c>reload__10CRunScript(0x1d4a430, ...)</c>. <c>exe()</c> reads the running frame's LOCAL VARIABLE
    /// array from <c>this + 0x28</c>, so that is how the mod reaches a script's locals: each is an
    /// 8-byte RS_STACKDATA of {type, value}, and type 1 = int.
    /// </summary>
    internal static class RunScript
    {
        internal const long Object   = 0x21D4A430;
        internal const int  VarsBase = 0x28;

        internal const int VarStride = 8;
        internal const int VarType   = 0;
        internal const int VarValue  = 4;
        internal const int TypeInt   = 1;
    }

    internal static class TownScript
    {
        /// <summary>ELF <c>EdEventData</c> — native ptr to the loaded event.stb image.</summary>
        internal const long EdEventData = 0x202A2854;

        internal const int CodeBase    = 0x08;
        internal const int LabelTable  = 0x0C;
        internal const int LabelCount  = 0x10;
        internal const int LabelStride = 0x08;   // { u32 id, u32 codeOffset }

        /// <summary>
        /// Where a label's EXECUTABLE code starts, relative to its <c>codeOffset</c>.
        ///
        /// A label does not begin with an instruction. It begins with an 8-byte gap and then a FOUR-SLOT
        /// HEADER (4 x 12 bytes): one slot whose first word varies per label — 27, 10, 4, 1, 3 — almost
        /// certainly a local-variable count, followed by three zeroed slots. Every real label in the game
        /// has this shape, and the first genuine instruction sits after it.
        ///
        /// Writing over that header is silent death: the VM reads your first PUSH as the frame setup and
        /// everything after it as rubbish. The event point fires, the label is requested, and nothing runs.
        /// </summary>
        internal const int LabelCodeSkip = 0x08 + 4 * 12;   // 8-byte gap + 4-slot header = 56

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
