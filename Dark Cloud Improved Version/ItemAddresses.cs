namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// EE RAM / ELF addresses for item-related static data. Companion of <see cref="ItemData"/>,
    /// mirroring the FishingAddresses / EnemyAddresses convention.
    ///
    /// Addresses are given as ELF/native VAs (the value in the SCUS_971.11 symbol table). The same
    /// data is reachable from the running game (PCSX2) at <c>native + 0x20000000</c>
    /// (= <see cref="Memory.Pcsx2Base"/>); helpers below return the PCSX2 EE RAM address.
    ///
    /// Confirmed during the item research session by reading the ELF .symtab + table bytes:
    ///   PriceList @0x00291B80, ComItemInfo @0x0027DE50, ITEM_NAME_TBL_NEW @0x0027E810,
    ///   ItemSetRateList0..6 @0x0026F6A0+, ItemPutListTbl0..13 (== Addresses.ItemTbl*).
    /// </summary>
    internal static class ItemAddresses
    {
        /// <summary>Native(ELF) VA → PCSX2 EE RAM address.</summary>
        internal static long ToEe(long nativeVa) => nativeVa + Memory.Pcsx2Base; // +0x20000000

        /// <summary>
        /// PriceList — default item prices. CONFIRMED (ELF symbol <c>PriceList</c>).
        /// 296 entries (one per item ID 81-376), stride 4 bytes: buy price (ushort) @+0x0,
        /// sell price (ushort) @+0x2. Entry index = (itemId - 81). True size is 0x4A0 = 1184 bytes.
        /// NOTE: Items.cs reads 1504 bytes here, over-reading 320 bytes past the table.
        /// </summary>
        internal static class ItemPriceTable
        {
            internal const int NativeBase  = 0x00291B80;
            internal const int Base        = 0x20291B80; // == Addresses.ItemPriceTable (PCSX2 EE)
            internal const int Stride      = 4;          // bytes per item entry
            internal const int BuyOffset   = 0x0;        // ushort — purchase price
            internal const int SellOffset  = 0x2;        // ushort — sell price
            internal const int FirstItemId = 81;         // entry 0 corresponds to item ID 81
            internal const int Count       = 296;        // 0x4A0 / 4 — items 81-376
            internal const int ByteSize    = Count * Stride; // 1184

            /// <summary>Entry index for the item, or -1 if out of table range.</summary>
            internal static int IndexForId(int itemId) =>
                itemId >= FirstItemId && itemId < FirstItemId + Count ? itemId - FirstItemId : -1;

            /// <summary>EE address of the item's buy-price ushort, or -1 if out of range.</summary>
            internal static int BuyAddr(int itemId)
            {
                int i = IndexForId(itemId);
                return i < 0 ? -1 : Base + i * Stride + BuyOffset;
            }

            /// <summary>EE address of the item's sell ushort, or -1 if out of range.</summary>
            internal static int SellAddr(int itemId)
            {
                int i = IndexForId(itemId);
                return i < 0 ? -1 : Base + i * Stride + SellOffset;
            }
        }

        /// <summary>
        /// ComItemInfo — per-item cross-reference / class table. CONFIRMED (ELF symbol).
        /// 296 records (item IDs 81-376), stride 8 bytes, four ushorts:
        ///   +0x0 <see cref="ClassOffset"/>       : engine class — 0=Attach, 1=Item, 2=Weapon
        ///                                          (see <see cref="ItemClass"/>)
        ///   +0x2 <see cref="ClassIndexOffset"/>  : index within the class grouping
        ///   +0x4 <see cref="SubIndexOffset"/>    : icon/message group sub-index
        ///   +0x6 <see cref="CanonicalIdOffset"/> : canonical item ID (non-weapons) or internal
        ///                                          weapon-number (weapons); 0xFFFF = none.
        /// The +0x0 class is mirrored into <see cref="ItemData.Class"/>.
        /// </summary>
        internal static class ComItemInfo
        {
            internal const int NativeBase  = 0x0027DE50;
            internal const int Stride      = 8;
            internal const int FirstItemId = 81;
            internal const int Count       = 296;

            internal const int ClassOffset       = 0x0; // ushort — ItemClass
            internal const int ClassIndexOffset  = 0x2; // ushort
            internal const int SubIndexOffset    = 0x4; // ushort
            internal const int CanonicalIdOffset = 0x6; // ushort (0xFFFF = none)

            /// <summary>EE address of the item's record, or -1 if out of range.</summary>
            internal static long RecordAddr(int itemId) =>
                itemId >= FirstItemId && itemId < FirstItemId + Count
                    ? ToEe(NativeBase) + (long)(itemId - FirstItemId) * Stride : -1;
        }

        /// <summary>
        /// ITEM_NAME_TBL_NEW — pointer array to each item's internal asset/model code string
        /// (e.g. "atfire", "juelgnet", "taiyou"). CONFIRMED (ELF symbol). 175 pointers (item IDs
        /// 81-255); stride 4. Each entry is a native string pointer; empty string for dummy items.
        /// Weapons (256+) are not covered here (separate weapon-name system). Mirrored into
        /// <see cref="ItemData.Code"/>.
        /// </summary>
        internal static class ItemCodeTable
        {
            internal const int NativeBase  = 0x0027E810;
            internal const int Stride      = 4;
            internal const int FirstItemId = 81;
            internal const int Count       = 175; // IDs 81-255
        }

        /// <summary>
        /// ItemSetRateList0..6 — per-dungeon item drop-rate lists. CONFIRMED + DECODED.
        /// The pointer array <c>ItemSetRateTbl</c> @native 0x00279D30 holds these 7 list pointers,
        /// one per dungeon (index = dungeon 0..6, see <see cref="Dungeon"/> order). Each list is
        /// 0x302 bytes = 385 ushorts; index = (itemId - 1); value = rarity 0-100 where HIGHER = RARER
        /// (picker accepts when rate &lt; random 0-99 roll, so P(picked) ~= (100-rate)/100; 50 = baseline).
        /// This is the real source behind the placeholder <c>Items.ItemRateTbl</c>.
        ///
        /// Reader: <c>PresetSmallItemNo_Get__Fiiii</c> (ELF 0x1BFEF0) — the floor small-item drop
        /// picker. It loads the candidate pool from <c>ItemPutListTbl</c>
        /// (ItemPutListPtr[dungeon + 7*backfloor], int32 item-ID array == Addresses.ItemTbl*) and
        /// weights each candidate by this list's rate, then SUBTRACTS 10 if the player has one of
        /// that item and 20 if they hold two or more.
        ///
        /// Values vary per dungeon (Garnet id95: DBC 90 -> Demon Shaft 70; weapons 50 -> 80).
        /// A readable view of the vanilla pools + rates is in docs/chest-loot-tables.md.
        /// </summary>
        internal static class ItemDropRateLists
        {
            /// <summary>Dungeon index order of the 7 lists.</summary>
            internal enum Dungeon
            {
                DBC = 0, WiseOwl = 1, Shipwreck = 2, SunAndMoon = 3,
                MoonSea = 4, GalleryOfTime = 5, DemonShaft = 6,
            }

            internal const int TablePtrNativeBase = 0x00279D30; // ItemSetRateTbl (7 list pointers)
            internal static readonly int[] NativeBase =
            {
                0x0026F6A0, 0x0026F9B0, 0x0026FCC0, 0x0026FFD0, 0x002702E0, 0x002705F0, 0x00270900,
            };
            internal const int Stride   = 2;     // ushort per item
            internal const int Count    = 385;   // 0x302 / 2 — index = itemId - 1
            internal const int ByteSize = 0x302;

            /// <summary>PCSX2 EE RAM address of the drop-rate ushort for the item in the given
            /// dungeon (0..6), or -1 if out of range.</summary>
            internal static long RateAddr(int dungeon, int itemId)
            {
                if ((uint)dungeon >= NativeBase.Length || itemId < 1 || itemId - 1 >= Count)
                    return -1;
                return ToEe(NativeBase[dungeon]) + (long)(itemId - 1) * Stride;
            }
        }

        /// <summary>
        /// ItemPutListTbl0..13 — the floor item-drop POOLS (which items can drop). CONFIRMED + DECODED.
        /// The pointer array <c>ItemPutListPtr</c> @native 0x002763D0 holds 14 table pointers, selected by
        /// <b>index = dungeon + 7*backfloor</b> (0-6 = dungeon fronts, 7-13 = dungeon backs). == the
        /// Addresses.ItemTbl* used by Dayuppy. Read by the floor drop picker PresetSmallItemNo_Get (ELF
        /// 0x1BFEF0): it picks <c>itemId = group[rand % count]</c> then gates it by the item's
        /// <see cref="ItemDropRateLists"/> weight.
        ///
        /// Each table is a sequence of floor-RANGE GROUPS, stride <see cref="GroupStride"/> (0x208):
        ///   +0x0 word0 : floor selector (see below); a group with word0 == -1 terminates the table
        ///   +0x4 word1 : <see cref="CountOffset"/> item count for this group
        ///   +0x8       : word1 x int32 item IDs (DUPLICATES allowed = higher selection weight)
        /// Item IDs are the normal 81-376 space.
        ///
        /// FLOOR -> GROUP selection (decoded from PresetSmallItemNo_Get), floor is 0-indexed:
        ///   1. Exact-floor override: if a group has word0 == floor+1, use it (only DBC uses this: word0=11
        ///      => 1-indexed floor 11).
        ///   2. Otherwise threshold T = int at native 0x00279D70 + dungeon*4 (T = {8,9,9,9,8,12,50} for
        ///      DBC,WOF,SW,SMT,MS,GoT,DS): floor < T  -> the word0==256 group ("early"),
        ///      floor >= T -> the word0==255 group ("late").
        /// To modify a pool: overwrite the int32 IDs in place (adjust word1 to match), keeping a -1
        /// terminator group. A readable per-dungeon/side view (floor range, item, rate, count) is in
        /// docs/chest-loot-tables.md. NOTE: the Chest Randomizer overwrites live chest SLOTS
        /// (ChestAddresses), not these pools, so these tables are vanilla reference.
        /// </summary>
        internal static class ItemPutLists
        {
            internal const int PtrArrayNativeBase = 0x002763D0; // ItemPutListPtr (14 table pointers)
            internal const int PtrCount     = 14;
            internal const int GroupStride  = 0x208; // bytes per floor-range group (= 130 int32)
            internal const int Word0Offset  = 0x0;   // int  - floor selector / terminator (-1)
            internal const int CountOffset  = 0x4;   // int  - number of item IDs in the group
            internal const int ItemsOffset  = 0x8;   // int32[count] - item IDs (duplicates = weight)

            /// <summary>Pointer-array index for (dungeon 0-6, back-floor?) → table index 0-13.</summary>
            internal static int TableIndex(int dungeon, bool backFloor) => dungeon + (backFloor ? 7 : 0);

            /// <summary>EE address of the ItemPutListPtr slot for the table (deref for the table base).</summary>
            internal static long TablePtrAddr(int dungeon, bool backFloor) =>
                ToEe(PtrArrayNativeBase) + (long)TableIndex(dungeon, backFloor) * 4;
        }

        // ---- Related tables (already referenced elsewhere), recorded here for completeness ----
        // PieroItemList0..13  @0x00276410..  (Pierre/shop daily lists)
        // ItemShopList2       @0x00292020
        // ItemTemplete        @0x002943C0    (8 model-template string pointers)
        // ITEM_LIST           @0x0027D0A0    (zeroed BSS runtime work buffer — NOT static data)

        /// <summary>
        /// Static per-item attribute table in the ELF — NOT FOUND. The research session confirmed
        /// there is no per-item max-stack or flags table: stacking is gated by a global per-floor
        /// cap (EdAddMaxItem clamps the placed-item count to 100 in save data); behaviour is
        /// selected by ID-range checks in code (e.g. ItemGetMes at IDs 81/145/...). The only
        /// per-item static data are PriceList, ComItemInfo, ITEM_NAME_TBL_NEW, and the rate lists.
        /// </summary>
        internal static class ItemStaticTable { /* intentionally empty — see summary */ }
    }
}
