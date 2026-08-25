using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Lightweight Brownboo (MapNo 14) camera probe for the winding-agnostic collision experiment: logs the
    /// live camera-gather load and the follow-camera state every couple of seconds, so "camera passed through
    /// a rock" moments can be read against hard numbers:
    ///
    ///   [BrownbooCam] polys=178/400 dist=61.3 h=5.0 ref=(-231.4,14.0,-260.1) eye=(-160.2,19.0,-238.7)
    ///
    ///   • polys — the TRUE per-frame camera-gather CCPoly count, exported by the cameraNormSide stub
    ///     ($s8 → Mailbox.CamGatherCount) vs the hard ~400 cap (PickUpCameraPoly has NO bounds check; at
    ///     the cap later parts are truncated → walls silently missing → clipping). ⚠ NOT the WorkBuffer
    ///     `used` field — that's the per-frame 2000-unit Alloc reservation and always reads "full".
    ///   • dist/h — the follow camera's boom length (+0x2D0) and height (+0x2D4): dist well under BASE(80)
    ///     near a wall = the swept-slide IS constraining; dist pinned at 80 while visibly inside geometry =
    ///     the slide never engaged (gather truncation or side/normal issue).
    ///
    /// Pure reads, throttled, Brownboo-only — safe to leave enabled.
    /// </summary>
    internal static class BrownbooCamProbe
    {
        internal static bool Enabled = true;

        private const int   BrownbooMapNo = 14;
        private const long  CamPtrVar     = 0x21D19678;   // MainCamera/CCameraFollow ptr
        private const int   ThrottleTicks = 40;           // town loop ~20 Hz -> a line every ~2 s

        private static int _tick;

        internal static void Tick()
        {
            if (!Enabled) return;
            if (Memory.ReadInt(EditLoop.MapNo) != BrownbooMapNo) { _tick = 0; return; }
            if (++_tick % ThrottleTicks != 0) return;

            int polys = Memory.ReadInt(CodeCaves.Mailbox.CamGatherCount);   // exact, exported by the norm-side stub

            uint camRaw = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(camRaw)) return;
            long cam = Memory.ToMmu(camRaw);
            float rx = Memory.ReadFloat(cam + 0x270), ry = Memory.ReadFloat(cam + 0x274), rz = Memory.ReadFloat(cam + 0x278);
            float dist = Memory.ReadFloat(cam + 0x2D0), h = Memory.ReadFloat(cam + 0x2D4), ang = Memory.ReadFloat(cam + 0x2DC);
            float ex = rx + dist * (float)Math.Sin(ang), ey = ry + h, ez = rz + dist * (float)Math.Cos(ang);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BrownbooCam] polys={polys}/400 dist={dist:0.0} h={h:0.0} " +
                $"ref=({rx:0.0},{ry:0.0},{rz:0.0}) eye=({ex:0.0},{ey:0.0},{ez:0.0})");
        }
    }
}
