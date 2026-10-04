using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A native routine called for the mod by the game itself: the arguments go into CodeCaves.CallRequest, the
    /// magic last, and the call-request cave (the tail of the camera-pin chain, once a dungeon frame) makes the call from
    /// the camera pass's epilogue and raises Done. Dungeon only, and only for routines the engine itself calls from its
    /// per-frame update. One request at a time.</summary>
    internal static class NativeCall
    {
        private static readonly object _lock = new object();

        /// <summary>Call <paramref name="func"/>(a0..a5, f12) on the next frame; false if the frame never came
        /// (<paramref name="timeoutMs"/>). <paramref name="v0"/> is the routine's return.</summary>
        internal static bool Invoke(uint func, out uint v0, uint a0 = 0, uint a1 = 0, uint a2 = 0, uint a3 = 0, uint a4 = 0, uint a5 = 0, float f12 = 0f, int timeoutMs = 250)
        {
            lock (_lock)
            {
                long t = CodeCaves.CallRequest;
                Memory.WriteInt (t + CodeCaves.CallMagic, 0);
                Memory.WriteInt (t + CodeCaves.CallDone, 0);
                Memory.WriteUInt(t + CodeCaves.CallFunc, func);
                Memory.WriteUInt(t + CodeCaves.CallA0, a0); Memory.WriteUInt(t + CodeCaves.CallA1, a1);
                Memory.WriteUInt(t + CodeCaves.CallA2, a2); Memory.WriteUInt(t + CodeCaves.CallA3, a3);
                Memory.WriteUInt(t + CodeCaves.CallA4, a4); Memory.WriteUInt(t + CodeCaves.CallA5, a5);
                Memory.WriteFloat(t + CodeCaves.CallF12, f12);
                Memory.WriteUInt(t + CodeCaves.CallMagic, CodeCaves.CallMagicValue);   // LAST: the cave acts on it
                DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < until)
                {
                    if (Memory.ReadInt(t + CodeCaves.CallDone) != 0) { v0 = Memory.ReadUInt(t + CodeCaves.CallV0); return true; }
                    Thread.Sleep(4);
                }
                Memory.WriteInt(t + CodeCaves.CallMagic, 0);                             // withdrawn: no frame took it
                v0 = 0;
                return false;
            }
        }
    }
}
