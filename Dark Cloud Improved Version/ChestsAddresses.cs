namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Floor chest slot array — live chest instances laid out contiguously in RAM.
    /// Count varies per floor (observed 5–6 slots); read <see cref="ChestSlots.CountAddr"/>
    /// at runtime to get the exact count for the current floor.
    /// Use <see cref="ChestSlots.SlotAddr"/> to compute the address of any field in any slot.
    ///
    /// Confirmed by memory dump analysis 2026-06-06.  Base address validated against the
    /// existing <see cref="Addresses.firstChest"/> / <see cref="Addresses.firstChestSize"/>
    /// constants (slot 0 EntityId and ChestSize respectively).
    /// See DungeonData.cs — "CHEST SPAWN TABLE" section for full investigation notes.
    /// </summary>
    internal static class ChestAddresses
    {
        internal static class ChestSlots
        {
            // Slot 0 EntityId field — the value Addresses.firstChest already points to.
            internal const int SlotBase = 0x21DD0260;
            internal const int Stride   = 0x40; // bytes between consecutive chest slots
            // Address of the chest count int for the current floor (firstChest − 0x30).
            internal const int CountAddr = 0x21DD0230;

            /// <summary>RAM address of <paramref name="fieldOffset"/> within slot <paramref name="slot"/>.</summary>
            internal static int SlotAddr(int slot, int fieldOffset) => SlotBase + slot * Stride + fieldOffset;
        }
    }

    /// <summary>Chest slot field offsets relative to each slot's base address (EntityId field).</summary>
    internal static class ChestSlotOffsets
    {
        internal const int ActiveFlag = -0x20; // int   — 1 = alive (unopened), 0 = empty/looted
        internal const int WorldX     = -0x10; // float — world X position; confirmed range 500–2000 (matches enemy coord scale)
        internal const int WorldY     = -0x08; // float — world Y position; confirmed range 500–2000
        internal const int EntityId   =  0x00; // int   — entity/slot identifier; slot 0 = Addresses.firstChest (0x21DD0260)
        internal const int ChestSize  =  0x08; // int   — 0 = small chest (item), 1 = big chest (weapon/rare); slot 0 = Addresses.firstChestSize (0x21DD0268)
    }
}
