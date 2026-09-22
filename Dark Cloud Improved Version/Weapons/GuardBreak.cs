using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// SHARED guard breaking: while it is driven on, no enemy blocks anything.
    ///
    /// Guarding is pure data — each slot has three guard windows whose active flags CheckDmg consults — so breaking it is
    /// simply zeroing those flags and putting them back afterwards. Nothing native is patched. The original flags are
    /// captured the first time a slot is seen (guards are armed at spawn, so first sight IS the original), re-captured when
    /// a slot is reused by another species, and forgotten on a floor change.
    ///
    /// It lives here rather than inside one weapon because several abilities want it: Dark Cloud and 7th Heaven for as long
    /// as they are drawn, Super Steve through their spheres, and Solar Flash only while its flash is blinding the floor.
    /// Toan carries one sword at a time and Super Steve is Xiao's, so no two of them are ever driving it at once; the last
    /// caller to pass false restores what was captured.
    /// </summary>
    internal static class GuardBreak
    {
        private static readonly ushort[,] _snap    = new ushort[EnemyAddresses.FloorSlots.Count, EnemyAddresses.GuardWindows.WindowCount];
        private static readonly ushort[]  _species = new ushort[EnemyAddresses.FloorSlots.Count];
        private static readonly bool[]    _captured = new bool[EnemyAddresses.FloorSlots.Count];
        private static byte _lastFloor = 0xFF;

        /// <summary>On: every live enemy's guard windows are held at zero. Off: each slot's captured flags are put back.</summary>
        internal static void Drive(bool breakGuard)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _lastFloor) { _lastFloor = floor; Array.Clear(_captured, 0, _captured.Length); }

            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) < 1)
                {
                    _captured[slot] = false;   // slot went inactive — re-capture whoever spawns next
                    continue;
                }
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                if (!_captured[slot] || _species[slot] != species)
                {
                    for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                        _snap[slot, w] = Memory.ReadUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w));
                    _species[slot] = species;
                    _captured[slot] = true;
                }
                for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                {
                    ushort want = breakGuard ? (ushort)0 : _snap[slot, w];
                    long addr = EnemyAddresses.GuardWindows.FlagAddr(slot, w);
                    if (Memory.ReadUShort(addr) != want) Memory.WriteUShort(addr, want);
                }
            }
        }
    }
}
