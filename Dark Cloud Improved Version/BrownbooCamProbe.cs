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

        // ── arrival camera set (vanilla starts AT rest — EdInitCameraParam = SetDistance(near) +
        //    SetHeight(5); the mod's height cave regressed this: at the spawn the boom hangs the eye
        //    over the crater RIM, the world-space ground-floor guard hoists it (~152) and the descent
        //    rate-cap grinds it down over ~6 s). One-shot DockCamera-style fix on the first non-event
        //    tick after entering: rest height + orbit angle aimed so the EYE sits on the TOWN side of
        //    the player (toward the crater centre) — town floor under the boom, nothing to hoist onto.
        //    A plain height snap does NOT work here: the floor guard re-raises it every frame while
        //    the eye hangs over the rim.
        //    ⚠ fires on EVERY large ref jump inside the arrival window, not once: the load stages the
        //    player at a holding position first (observed ref (301,39,9)) and TELEPORTS to the real
        //    spawn (-312,-71) a beat later — a one-shot fired at the staging point and the spawn then
        //    re-hoisted (inherited Queens angle pointed the boom over the WEST rim -> floor guard).
        private const int   ArrivalWindowTicks = 240;     // ~12 s of teleport-watching after a map change
        private const float ArrivalJump        = 60f;     // ref jump that counts as the spawn teleport
        private static int  _arrivalLeft;
        private static float _lastRx = float.NaN, _lastRz;
        private static int  _prevMap = -1;

        private static int _tick;

        internal static void Tick()
        {
            if (!Enabled) return;
            int map = Memory.ReadInt(EditLoop.MapNo);
            if (map != _prevMap) { _prevMap = map; _arrivalLeft = ArrivalWindowTicks; _lastRx = float.NaN; }
            if (map != BrownbooMapNo) { _tick = 0; return; }

            if (_arrivalLeft > 0 && Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeEvent)
            {
                _arrivalLeft--;
                uint cr = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(cr))
                {
                    long c = Memory.ToMmu(cr);
                    float curRx = Memory.ReadFloat(c + 0x270), curRz = Memory.ReadFloat(c + 0x278);
                    bool jumped = float.IsNaN(_lastRx) ||
                                  Math.Abs(curRx - _lastRx) > ArrivalJump || Math.Abs(curRz - _lastRz) > ArrivalJump;
                    _lastRx = curRx; _lastRz = curRz;
                    if (jumped)
                    {
                        float rest = Memory.ReadFloat(CodeCaves.Mailbox.CameraRestH);
                        if (float.IsNaN(rest) || rest <= 0f || rest > 60f) rest = 5f;
                        // eye = ref + dist·(sin a, cos a): aim the eye from the ref TOWARD the crater centre
                        float a = (float)Math.Atan2(-curRx, -curRz);
                        float oldH = Memory.ReadFloat(c + 0x2D4);
                        // v3: ALSO snap the boom SHORT — vanilla EdInitCameraParam is SetDistance(near) +
                        // SetHeight(5). Height+angle alone kept failing: at BASE(80) the eye still hangs
                        // over the crater rim at some spawns and the world-floor guard re-hoists it every
                        // frame. Short boom = eye at the player's own ground; it then extends outward over
                        // the LOW crater-side terrain along the snapped angle, exactly like a vanilla load.
                        Memory.WriteFloat(c + 0x2D0, 12f);    // distance = near (extends back out naturally)
                        Memory.WriteFloat(c + 0x2D4, rest);   // height = rest (vanilla EdInitCameraParam value)
                        Memory.WriteFloat(c + 0x2D8, a);      // orbit angle TARGET
                        Memory.WriteFloat(c + 0x2DC, a);      // smoothed angle (snap — no swing)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[BrownbooCam] arrival set @({curRx:0.0},{curRz:0.0}): h {oldH:0.0} -> {rest:0.0}, dist -> 12, angle -> {a:0.00}");
                    }
                }
            }

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
