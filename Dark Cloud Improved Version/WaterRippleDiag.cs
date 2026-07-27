using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC for the Queens canal RIPPLE (the animated CWater plane, separate from the
    /// mizu__a01 mesh that CanalTide already raises). <c>DrawWaterSurface(CCamera)</c> @0x19D980 draws
    /// ONLY <c>Water[0]</c> (the loop is <c>for i&lt;1</c>), gated on the active flag <c>+0x20</c>, and takes
    /// its surface Y from <c>Water[0]+0x44</c> — the X/Z follow the camera (flags +0x24/+0x2c) but Y is fixed
    /// unless the +0x28 flag is set. So if Water[0] is the canal plane and is active, writing +0x44 moves the
    /// ripple. A prior CanalTide pin of +0x44 "fought the engine"; this logs the truth:
    ///   • WHICH CWater slot (if any) is the canal (pos.y ~= the tide) and whether it is slot 0 (the only one
    ///     drawn) — if the canal is slot 1/2 it never renders through DrawWaterSurface.
    ///   • the active flag (+0x20) and the X/Y/Z follow flags (+0x24/+0x28/+0x2c) while WALKING vs FISHING.
    ///   • the live pos.y (+0x44) each time it changes — does it hold, or get re-copied back to 31 each frame?
    /// Walk around the Queens canal at different tides (afternoon 31 / morning 40 / night 52) and fish, then
    /// read the log. CWater base = guest 0x01D536F0 / mmu 0x21D536F0, stride 0x3B0.
    /// </summary>
    internal static class WaterRippleDiag
    {
        internal static bool Enabled = false;   // confirmed: both global Water[] and CEditGround water[] are inactive in Queens — ripple is the georama tile-water, not a CWater plane

        // The GEORAMA grid renders its OWN water via DrawWaterSurface__11CEditGround (0x1a3360): 4 CWater
        // bodies at CEditGround + 0x15040 (stride 0x3B0), Y @ +0x44 — separate from the global Water[] (which
        // is inactive in Queens). CEditGround = *(edit_info @ 0x202A27B0).
        private const long EditInfoPtr = 0x202A27B0;
        private const long WaterArrOff = 0x15040;
        private const long Stride      = 0x3B0;
        private const int  Slots       = 4;          // DrawWaterSurface__CEditGround loops i<4
        private static readonly string[] _last = new string[Slots];

        internal static void Tick()
        {
            if (!Enabled) return;
            if (Memory.ReadInt(EditLoop.MapNo) != CanalTide.QueensMapNo) return;
            uint eiGuest = Memory.ReadUInt(EditInfoPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(eiGuest)) return;
            long editGround = Memory.ToMmu(eiGuest);

            int gm = Memory.ReadInt(EditLoop.GameMode);
            for (int i = 0; i < Slots; i++)
            {
                long b = editGround + WaterArrOff + i * Stride;
                int active = Memory.ReadInt(b + 0x20);
                int fx = Memory.ReadInt(b + 0x24), fy = Memory.ReadInt(b + 0x28), fz = Memory.ReadInt(b + 0x2C);
                float px = Memory.ReadFloat(b + 0x40), py = Memory.ReadFloat(b + 0x44), pz = Memory.ReadFloat(b + 0x48);
                // round Y to 0.1 so tiny ripple jitter doesn't spam, but real tide moves (31->40->52) show.
                string state = $"a{active} fxyz{fx},{fy},{fz} pos({px:0.0},{py:0.0},{pz:0.0}) gm{gm}";
                if (state == _last[i]) continue;
                _last[i] = state;
                Log($"EG.Water[{i}] active={active} followX/Y/Z={fx}/{fy}/{fz} pos=({px:0.0},{py:0.0},{pz:0.0})  [gameMode {gm}]");
            }
        }

        private static void Log(string m) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[RippleDiag] " + m);
    }
}
