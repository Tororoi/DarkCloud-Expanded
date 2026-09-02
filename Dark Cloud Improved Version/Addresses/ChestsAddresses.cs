namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Dungeon treasure-box + mimic subsystem, mapped from the ELF (SCUS_971.11) 2026-06-19.
    ///
    /// Everything lives INLINE inside the CDungeonMap object. Its pointer is stored at gp-0x6338
    /// (gp native 0x01E00000) ⇒ read CDungeonMap* from PCSX2 0x21DF9CC8. In practice CDungeonMap is
    /// allocated at a stable address, so the absolute addresses below (this* = 0x21DC4BE0, native
    /// 0x01DC4BE0) hold across floors — confirmed: the disassembly-derived EntityId/ChestSize/position
    /// land EXACTLY on the previously dump-verified <see cref="ChestSlotOffsets"/> addresses.
    ///
    /// Two correlated tables + a count, all relative to this* (= 0x21DC4BE0):
    ///   • BOX-OBJECT table   this*+0xB660 : 24 entries × 0x40  (rendered chests; <see cref="ChestSlots"/>)
    ///   • STATE/META table   this*+0x8D58 : 48 entries × 0x50  (per-box type/item/pos; <see cref="ChestStateTable"/>)
    ///   • BOX loop count     this*+0xBC60 : int, # of placed boxes (incremented by SetTreasureBox)
    ///
    /// Engine functions (all PURE DATA writes — no malloc/new, so replicable from C#):
    ///   SetTreasureBox  (ELF 0x1C8240): finds a free box slot (ActiveFlag==0) + free state slot
    ///     (type==-1), writes both, increments the count. Normal box ⇒ STATE.Type = 2.
    ///   SetMimicEvent   (ELF 0x1C8BC0): same shape, STATE.Type = 8, links BOX.EntityId = enemy slot idx.
    ///     Called from SetupViewMonstor (enemy spawn) ONLY when a category-8 mimic's spawn "type" field
    ///     is 3 or 4 — which roster-injected mimics lack, so they get no chest (see mimic-view-gate notes).
    ///   DrawItemBox     (ELF 0x1C4DE0): iterates box slots [0..count), draws ActiveFlag!=0 entries using
    ///     position + a render branch on ChestSize/Type (0 vs 1).
    ///   CheckTreasureBox(ELF 0x1C7EE0): proximity test over box slots [0..count).
    ///   Box-open anim + item grab: BtGetTreasurebox{Big,Small}_Loop (0x1D1810 / 0x1D2460) — these do
    ///     NOT wake the mimic.
    ///   MIMIC TRANSFORM HANDLER = OPAnalyz (dun overlay 0x01DB1400): on interacting with an active mimic
    ///     event it reads STATE.BoxLink → box idx, reads BOX.EntityId (-0x4980) → linked enemy slot idx,
    ///     and if valid (0..16) does `sh 1, enemySlot+0xD4` — i.e. sets the linked enemy's view gate to 1,
    ///     which un-skips it in CheckViewLevel so the dormant mimic wakes/becomes visible. (Same slot+0xD4
    ///     gate as SpawnRoster.ActivateMimicSlots; native writes 1, any nonzero works.)
    ///
    /// FULL CHAIN (all data-driven): SetMimicEvent populates BOX+STATE(Type=8) + count++ → DrawItemBox
    /// renders the chest → proximity (CheckTreasureBox / GetActiveIvent) → OPAnalyz sets enemy slot+0xD4=1
    /// on open → CheckViewLevel activates the mimic. So authentic roster chests = replicate SetMimicEvent's
    /// table writes post-spawn; the engine does the render + wake for free.
    /// </summary>
    internal static class ChestAddresses
    {
        /// <summary>
        /// The LIVE CDungeonMap the tables below hang off. There are TWO instances — MainDungeonMap and
        /// UraDungeonMap (the back floors), exactly 0x10B10 apart (= sizeof(CDungeonMap)) — and the engine
        /// iterates whichever <c>NowDngMap</c> points at.
        ///
        /// These tables used to be hardcoded absolutes into the MAIN map. That is a BUG on a back floor: the
        /// engine is walking the Ura map while our writes (mimic box + state slots, chest avoidance) land in the
        /// main map's object, where nothing reads them. Every address below is therefore an OFFSET, resolved
        /// against the live map at call time. Falls back to the main map if the pointer isn't up yet.
        /// </summary>
        internal static long MapBase()
        {
            long m = DungeonAddresses.Map.Deref((uint)Memory.ReadInt(DungeonAddresses.Map.NowDngMapPtr));
            return Memory.IsValidGuest(m) ? m : DungeonAddresses.Map.MainDungeonMap;
        }

        /// <summary>
        /// BOX-OBJECT table — live chest instances, one per rendered chest. 24 slots × 0x40, at map+0xB680.
        /// </summary>
        internal static class ChestSlots
        {
            internal const int TableOffset = 0xB680;  // map + this = slot 0 EntityId
            internal const int Stride      = 0x40;    // bytes between slots
            internal const int Capacity    = 24;      // 0x18 — table size SetTreasureBox/SetMimicEvent scan for a free slot

            // Number-of-chests count. NOTE: the engine's box LOOP bound (DrawItemBox/CheckTreasureBox) is
            // BoxLoopCountOffset (map+0xBC60). CountOffset (map+0xB650, a header just before the array) is what
            // existing mod code reads and has matched the live chest count — keep both; verify the relationship
            // live if it ever matters.
            internal const int CountOffset        = 0xB650; // header count (existing callers)
            internal const int BoxLoopCountOffset = 0xBC60; // incremented by SetTreasureBox; the real loop bound

            // Draw-enable / state flags trailing the array (read by DrawItemBox head gate).
            internal const int DrawFlag0Offset = 0xBC64;
            internal const int DrawFlag1Offset = 0xBC68;
            internal const int DrawFlag2Offset = 0xBC70;
            internal const int DrawFlag3Offset = 0xBC74;

            internal static long CountAddr()    => MapBase() + CountOffset;
            internal static long BoxLoopCount() => MapBase() + BoxLoopCountOffset;
            internal static long DrawFlag(int i) =>
                MapBase() + (i == 0 ? DrawFlag0Offset : i == 1 ? DrawFlag1Offset
                                    : i == 2 ? DrawFlag2Offset : DrawFlag3Offset);

            /// <summary>RAM address of <paramref name="fieldOffset"/> within box slot <paramref name="slot"/>,
            /// on the LIVE dungeon map.</summary>
            internal static long SlotAddr(int slot, int fieldOffset)
                => MapBase() + TableOffset + (long)slot * Stride + fieldOffset;
        }

        /// <summary>
        /// STATE/META table — per-box metadata (type, item contents, position, box link). 48 slots × 0x50, at
        /// map+0x8D58. Free slot marker = Type == -1.
        /// </summary>
        internal static class ChestStateTable
        {
            internal const int TableOffset = 0x8D58;  // map + this = slot 0 Type field
            internal const int Stride      = 0x50;
            internal const int Capacity    = 48;      // 0x30

            internal static long SlotAddr(int slot, int fieldOffset)
                => MapBase() + TableOffset + (long)slot * Stride + fieldOffset;
        }
    }

    /// <summary>Box-object slot field offsets, relative to the slot's EntityId field (0x00).</summary>
    internal static class ChestSlotOffsets
    {
        internal const int ActiveFlag = -0x20; // int   — occupied: 1 = alive (placed/unopened), 0 = free  [SetTreasureBox=-0x49a0]
        internal const int WorldX     = -0x10; // float — position vector start (X). Full vec is -0x10..-0x04 (X,?,Z/W).  [+0xB670]
        internal const int WorldY     = -0x08; // float — position +0x08 (height/Z per existing label)
        internal const int EntityId   =  0x00; // int   — slot/entity id; for MIMICS this is the LINKED ENEMY SLOT INDEX  [-0x4980]
        internal const int ConstOne   =  0x04; // int   — always 1  [-0x497c]
        internal const int ChestSize  =  0x08; // int   — DrawItemBox render branch [-0x4978]: 1 = small/regular chest
                                               //          (models -0x438c/-0x4390), 0 = BIG chest (models -0x4398/-0x439c).
                                               //          (Corrected from the old "0=small" note — verified in DrawItemBox.)
        internal const int OpenState  =  0x0C; // int   — 0 at create; box-open loops write here  [-0x4974]
        internal const int Extra      =  0x10; // int   — SetTreasureBox stores s2 (item-related)  [-0x4970]
    }

    /// <summary>State/meta table field offsets, relative to the slot's Type field (0x00 = this*+0x8D58).</summary>
    internal static class ChestStateOffsets
    {
        internal const int ItemA   = -0x08; // int   — item id A (SetTreasureBox: fptosi)  [-0x72b0]
        internal const int ItemB   = -0x04; // int   — item id B  [-0x72ac]
        internal const int Type    =  0x00; // int   — 2 = normal box, 8 = mimic, -1 = free  [-0x72a8]
        internal const int Zero    =  0x04; // int   — 0  [-0x72a4]
        internal const int PosX    =  0x08; // float — [-0x72a0]
        internal const int PosY    =  0x0C; // float — [-0x729c]
        internal const int PosZ    =  0x10; // float — z + height offset (10.0 or 8.0)  [-0x7298]
        internal const int Height  =  0x1C; // float — 10.0 (0x41200000) or 8.0 (0x41000000)  [-0x728c]
        internal const int BoxLink =  0x20; // int   — link to box-object slot (SetMimicEvent=box idx; SetTreasureBox=count idx)  [-0x7288]
    }
}
