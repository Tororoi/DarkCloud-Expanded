using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Tsukikage or Heaven's Cloud sphere — the Tsukikage's "Moonlit Focus" from Xiao's slingshot:
    /// every pellet she fires leaves at twice its speed. Each newly-live shot-pool slot has its velocity doubled exactly once
    /// (a per-slot latch that clears when the slot frees). Nothing to restore when the sphere goes: a pellet already in flight
    /// keeps its speed. Driven from Super Steve's sphere dispatch (Quick Draw is inherited alongside it there).</summary>
    internal static class TsukikageSphere
    {
        private const string Tag = "[TsukikageSphere] ";
        private static readonly bool[] _handled = new bool[PlayerShotPool.SlotCount];   // per-slot "already doubled" latch

        /// <summary>Moonlit Focus (Tsukikage / Heaven's Cloud): doubles each newly-fired pellet's +0x1C0
        /// velocity (2× shot speed), once per pellet. Per-slot latched; the latch clears when the slot frees.</summary>
        internal static void Drive(bool active)
        {
            long poolBase = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) return;   // pool not allocated / bad pointer
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if (active && live && !_handled[i])
                {
                    long vel = PlayerShotPool.VelAddr(poolBase, i);
                    for (int c = 0; c < 3; c++)
                        Memory.WriteFloat(vel + c * 4, Memory.ReadFloat(vel + c * 4) * 2f);
                    _handled[i] = true;
                }
                else if (!live) _handled[i] = false;
            }
        }
    }
}
