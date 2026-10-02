using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The GUARD GATE's mod-side input: which enemy guard windows block nothing. CheckDmg's guard-window load goes through the
    /// ISO's gate (tools/stubs/guard_crush.s → guard_mask.s → cat_guard_bypass.s), which passes a window when
    ///  · the hit carries CodeCaves.CrushMark (set per hit by whoever plants it: the Divine Beast cat, the Terra Sword's drops,
    ///    Big Bang's guard-breaking falloff), or
    ///  · that enemy's byte in CodeCaves.GuardMask has the window's bit — written here, from two inputs:
    ///    <see cref="NobodyBlocks"/> (every window of every enemy: Dark Cloud and 7th Heaven while drawn or carried as Super
    ///    Steve's sphere, a Solar Shot's blinding, Big Bang's whirl) and <see cref="IgnoreWindows"/> (chosen windows of one enemy:
    ///    the Dusack's mimic wake window).
    /// The enemies' own windows are never written, so a script re-registering them (`_SET_GUARD_FRAME`) changes nothing, and
    /// nothing has to be put back. Toan carries one sword at a time and Super Steve is Xiao's, so no two callers drive
    /// <see cref="NobodyBlocks"/> at once; the last call wins.
    /// </summary>
    internal static class GuardGate
    {
        private const string Tag = "[GuardGate] ";
        private const byte   AllWindows = (1 << EnemyAddresses.GuardWindows.WindowCount) - 1;
        private const uint   CaveWord0  = 0x3C0101DFu;   // guard_mask.s opens `lui at,0x01DF`
        private static readonly object _lock = new();
        private static bool _all, _warned;
        private static readonly byte[] _windows = new byte[EnemyAddresses.FloorSlots.Count];

        /// <summary>On: no enemy's guard window blocks anything. Off: they block as their scripts set them (less what
        /// <see cref="IgnoreWindows"/> asks).</summary>
        internal static void NobodyBlocks(bool on)
        {
            lock (_lock) { _all = on; Flush(); }
        }

        /// <summary>The windows of <paramref name="slot"/> that block nothing (bit w = window w; 0 = none).</summary>
        internal static void IgnoreWindows(int slot, byte bits)
        {
            if (slot < 0 || slot >= _windows.Length) return;
            lock (_lock) { _windows[slot] = (byte)(bits & AllWindows); Flush(); }
        }

        /// <summary>The mailbox brought in line with the inputs (read back first: a reset clears it under us).</summary>
        private static void Flush()
        {
            var want = new byte[_windows.Length];
            bool any = false;
            for (int s = 0; s < want.Length; s++) { want[s] = _all ? AllWindows : _windows[s]; any |= want[s] != 0; }
            byte[] cur = Memory.ReadBytesBatch(CodeCaves.GuardMask, want.Length);
            if (cur == null || !want.AsSpan().SequenceEqual(cur)) Memory.WriteBytesBatch(CodeCaves.GuardMask, want);
            if (any && !_warned && Memory.ReadUInt(0x20000000L + CodeCaves.DebugInfoCave.GuardMask) != CaveWord0)
            {
                _warned = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the guard-mask cave is not in this ISO — guards are not broken until it is repatched");
            }
        }
    }
}
