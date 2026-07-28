using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// DIAGNOSTIC for the Queens canal RIPPLE — now on the CORRECT object. Offline RE established the ripple is
    /// the mapinfo <c>WATER "e03c08"</c> (mizu) declaration, rendered by <c>DrawWaterSurface__11CEditGround</c>
    /// (0x1a3360, from MainDraw) as a CWater body in the CEditGround array at <c>base+0x15040</c> (4 slots,
    /// stride 0x3B0), animated by <c>WATER_SHAKE</c>. The Y comes from the body's pos <c>+0x44</c> (X/Z follow
    /// the camera via +0x24/+0x2c; Y is fixed unless +0x28 is set). Earlier passes read the WRONG base —
    /// <c>edit_info</c> @0x2A27B0 — but MainDraw passes <c>*(gp-0x6f18)</c> = guest <c>*(0x2A28D8)</c>, a
    /// different global. This logs that array so we can see the active canal body + its Y and confirm +0x44 is
    /// the pinnable lever to drive from the tide (like the mizu mesh).
    /// </summary>
    internal static class WaterRippleDiag
    {
        internal static bool Enabled = false;   // ripple lever found: CEditGround CWater[0] @ base+0x15040, Y +0x44 — now pinned by CanalTide.PinRipple

        private const long EditGroundPtr = 0x202A28D8;   // *(gp-0x6f18): the CEditGround MainDraw actually draws
        private const long WaterArrOff   = 0x15040;
        private const long Stride        = 0x3B0;
        private const int  Slots         = 4;            // DrawWaterSurface__CEditGround loops i<4
        private static readonly string[] _last = new string[Slots];

        internal static void Tick()
        {
            if (!Enabled) return;
            if (Memory.ReadInt(EditLoop.MapNo) != CanalTide.QueensMapNo) return;
            uint egGuest = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egGuest)) return;
            long arr = Memory.ToMmu(egGuest) + WaterArrOff;

            for (int i = 0; i < Slots; i++)
            {
                long b = arr + i * Stride;
                int active = Memory.ReadInt(b + 0x20);
                int fx = Memory.ReadInt(b + 0x24), fy = Memory.ReadInt(b + 0x28), fz = Memory.ReadInt(b + 0x2C);
                float px = Memory.ReadFloat(b + 0x40), py = Memory.ReadFloat(b + 0x44), pz = Memory.ReadFloat(b + 0x48);
                string state = $"a{active} f{fx}{fy}{fz} y{py:0.0}";
                if (state == _last[i]) continue;
                _last[i] = state;
                Log($"EG.CWater[{i}] @0x{b:X} active={active} followX/Y/Z={fx}/{fy}/{fz} pos=({px:0.0},{py:0.0},{pz:0.0})");
            }
        }

        private static void Log(string m) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[RippleDiag] " + m);
    }
}
