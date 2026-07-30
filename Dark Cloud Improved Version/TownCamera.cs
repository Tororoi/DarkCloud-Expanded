using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// From-scratch town/fishing camera (2026-07). We DROVE the vanilla EdMoveChara camera to a dead end, so
    /// instead we decouple the engine (IsoPatcher.PatchDecoupleCamera NOPs the lone FollowOn → MainCamera stays
    /// follow-OFF) and drive MainCamera ourselves from here every tick.
    ///
    /// With follow OFF, Step__CCameraFollow ignores the follow FIELDS (dist/angle/height that vanilla keeps
    /// writing, now inert) and instead interpolates the camera's CURRENT pos/ref (+0x260/+0x270) toward the NEXT
    /// targets (+0x280/+0x290). So we own the view by writing those targets; Step (runs every frame while
    /// StopCamera==0) smooths our 20 Hz writes to 60 Hz for free — no frame-sync needed.
    ///
    /// PHASE 0 (this file): prove we own the camera. Orbit the player at OUR fixed distance/height, using the
    /// bearing vanilla still computes (+0x2d8). If the camera holds a constant distance and never does vanilla's
    /// pull-in, control is proven. It WILL clip walls — collision is Phase 2. Phase 1 = read the pad for our own
    /// yaw/pitch/zoom; Phase 2 = cache the player-collision tris per area and slide the camera along them.
    /// See memory: town-camera-rewrite.
    /// </summary>
    internal static class TownCamera
    {
        internal static bool Enabled = true;

        // Tunables (Phase 0)
        internal static float Distance = 80f;   // orbit radius around the player
        internal static float Height   = 20f;   // camera height above the follow target

        // MainCamera is reached via the pointer EdMoveChara uses (DAT_01d19678 = &MainCamera).
        private const long CamPtrVar = 0x21D19678;
        private const long NowCamPtr = 0x202A3498;   // the ACTIVE render camera — only drive MainCamera when it IS this

        // CCameraFollow field offsets (from the object base):
        private const int OffCurPos = 0x260, OffCurRef = 0x270;   // CCamera current pos / ref (what Step interpolates)
        private const int OffNextPos = 0x280, OffNextRef = 0x290; // CCamera next-targets (what we set)
        private const int OffFollowRef = 0x2C0;                   // follow look-at — vanilla still writes it = ~player pos
        private const int OffFollowAngle = 0x2D8;                // follow bearing — vanilla still tracks the stick here
        private const int OffFollowOn = 0x2E0;                   // follow-enable — must stay 0 for our targets to win

        // Interpolation speed written to the camera's +0x2a8 (the Step easing divisor): current += (next-current)
        // /speed per 60 Hz frame. 1 = snap (choppy), higher = smoother/floatier. This is what turns our 20 Hz
        // target writes into 60 Hz-smooth motion.
        internal static float InterpSpeed = 8f;   // ease strength; higher = smoother/floatier, lower = tighter
        private const int OffClamp = 0x2A4, OffSpeed = 0x2A8, OffSnap = 0x2B4;

        // Phase 1 — pad-driven yaw. GamePad global @ 0x21cbc540: held buttons u16 @+0x04, right stick X @+0x14
        // (0..255, neutral 128). Town camera = right-stick-X (proportional) + L1/R1 (fixed). We now tick at ~60 Hz
        // (see Loop), so rates match the game's per-frame values (stick 0.03, L1/R1 1° = 0.0174533).
        private const long BtnHeld = 0x21CBC544, RStickX = 0x21CBC554;
        private const int BtnL1 = 0x0004, BtnR1 = 0x0008;
        internal static float YawRate  = 0.03f;      // right stick: radians/frame at full deflection
        internal static float L1R1Rate = 0.0174533f; // L1/R1: radians/frame fixed (1°)
        internal static float Deadzone = 0.18f;      // right-stick X deadzone (game's own is ~0.39; ours finer)
        internal static bool  InvertYaw = false;     // flip if rotation feels backwards

        // Right stick Y (@0x21cbc550, 0..255, neutral 128) adjusts camera HEIGHT, like vanilla's vertical control.
        private const long RStickY = 0x21CBC550;
        internal static float VertRate = 1.2f;                 // height units/frame at full RY deflection
        internal static float VertMin = -10f, VertMax = 40f;  // clamp on the accumulated height offset
        internal static bool  InvertPitch = true;             // flip if up/down feels backwards

        // AUTOROTATE: when the straight-back view is blocked (a corner/wall), swing yaw toward whichever side has a
        // clearer line of sight — this carries the camera around corners instead of only pulling in.
        internal static bool  AutoRotate = true;
        internal static float AutoRotateRate = 0.1f;   // rad/frame swing toward the clearer side (faster = snappier)
        internal static float AutoProbe     = 0.35f;    // rad offset probed left/right to decide WHICH way to swing
        // "Clearance" is measured as a small FAN (center + two side rays AutoFan apart) so it captures lateral room,
        // not just the exact centre line — this is the padding that keeps the camera off walls. Autorotate keeps
        // swinging until the fan clearance is within AutoClearPad of the full distance (overcorrect past a bare peek).
        internal static float AutoFan      = 0.13f;     // rad half-angle of the clearance fan (~7.5°)
        internal static float AutoClearPad = 20f;       // keep rotating until this much clearance margin is restored

        // COLLISION-AWARE SPRING ARM: the arm length eases, and can EXTEND past the resting Distance so the camera
        // swings on a WIDER radius around a corner (clearing it) instead of forcing through. A hard cap at the wall
        // guarantees no clipping; everything else eases so entering/leaving obscured is smooth.
        internal static float MaxDistance = 200f;   // arm may extend to here while obscured (wider arc around corners)
        internal static float DistEase    = 0.15f;  // how fast the arm length eases toward its target
        internal static float BlendEase   = 0.12f;  // how fast the obscured blend (autorotate + extend) eases in/out

        private static bool _seeded;
        private static float _yaw;   // our own camera bearing (radians), maintained from the pad
        private static float _heightOffset;   // stick-driven vertical offset added to Height
        private static float _dist;       // smoothed arm length (horizontal orbit radius)
        private static float _rotBlend;   // eased 0..1 autorotate strength (obscured AND rotation can help)
        private static float _extBlend;   // eased 0..1 arm-extension amount (obscured AND rotation can't help)
        private static int _logCounter;   // throttle for the pull-in diagnostic

        // ── Run the controller on its OWN ~60 Hz thread, not the shared ~20 Hz mod loop. The camera must sample
        //    the target (which moves at 60 Hz) each frame, or lateral/strafe motion judders in 20 Hz steps. PINE
        //    access is lock-serialized (MemoryFunctions.SendBatch), so a second thread is safe. Start() is called
        //    once from the town loop; the thread self-gates (only drives MainCamera when it's the active camera).
        private static Thread _thread;
        // vcount @ 0x202A2400 is incremented once per vblank by VSyncCallBack — the game's frame counter. We poll
        // it fast and update the camera exactly once per frame it changes, so our sampling is PHASE-LOCKED to the
        // game (no async beat = the residual strafe chop). Polling is a single cheap read; the write happens ~60x/s.
        private const long VCount = 0x202A2400;
        private static int _lastVCount = int.MinValue;
        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "TownCamera" };
            _thread.Start();
        }
        private static void Loop()
        {
            while (true)
            {
                try
                {
                    int vc = Memory.ReadInt(VCount);
                    if (vc != _lastVCount) { _lastVCount = vc; Tick(); }   // once per game frame, frame-synced
                }
                catch { }
                Thread.Sleep(1);
            }
        }

        internal static void Tick()
        {
            if (!Enabled) return;

            uint p = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) { _seeded = false; return; }
            long cam = Memory.ToMmu(p);
            // (The NowCamera==MainCamera gate was removed — it was blocking our writes and freezing the camera.
            //  Writing MainCamera when it isn't the render camera is harmless. Re-add a correct gate later if a
            //  non-town context misbehaves.)

            // Keep follow OFF + configure Step's easing: disable the ±2/frame delta clamp (+0x2a4), set our ease
            // speed (+0x2a8), and disable the snap-to-target threshold (+0x2b4) — Step snaps current=next whenever
            // |current-next| < +0x2b4, so a large threshold makes our small 20 Hz steps snap = the choppiness.
            Memory.WriteInt(cam + OffFollowOn, 0);
            Memory.WriteInt(cam + OffClamp, 0);
            Memory.WriteFloat(cam + OffSpeed, InterpSpeed);
            Memory.WriteFloat(cam + OffSnap, 0f);

            // Orbit target = the follow look-at vanilla still computes (≈ the player). Read all 3 in ONE batched
            // round-trip so the game can't update the position between reads (3 separate reads = torn value = the
            // residual jitter).
            float[] tgt = Memory.ReadFloatBatch(cam + OffFollowRef, 3);
            float tx = tgt[0], ty = tgt[1], tz = tgt[2];

            // Seed our yaw from the current bearing on first frame / re-acquire (no jump on entry).
            if (!_seeded) _yaw = Memory.ReadFloat(cam + OffFollowAngle);

            // Pad-driven yaw: right stick X (proportional), else L1/R1 (fixed). Sign matches the game's +0x2d8
            // accumulator (stick-right → yaw--); InvertYaw flips it.
            ushort btn = Memory.ReadUShort(BtnHeld);
            float rxf = ((int)Memory.ReadByte(RStickX) - 128) / 128f;
            if (Math.Abs(rxf) < Deadzone) rxf = 0f;
            float dYaw;
            if (rxf != 0f)                    dYaw = -YawRate * rxf;
            else if ((btn & BtnR1) != 0)      dYaw = -L1R1Rate;
            else if ((btn & BtnL1) != 0)      dYaw = +L1R1Rate;
            else                              dYaw = 0f;
            _yaw += InvertYaw ? -dYaw : dYaw;

            // Pad-driven HEIGHT: right stick Y adjusts the camera's vertical offset (vanilla lets the stick raise/
            // lower the view). Neutral 128; stick-up (ryf<0) raises by default. Accumulated + clamped.
            float ryf = ((int)Memory.ReadByte(RStickY) - 128) / 128f;
            if (Math.Abs(ryf) < Deadzone) ryf = 0f;
            _heightOffset += (InvertPitch ? ryf : -ryf) * VertRate;
            if (_heightOffset < VertMin) _heightOffset = VertMin;
            if (_heightOffset > VertMax) _heightOffset = VertMax;

            float camY = ty + Height + _heightOffset;   // desired camera height this frame

            // ── COLLISION-AWARE SPRING ARM ─────────────────────────────────────────────────────────────────────
            // Priority when the view is obscured: (1) AUTOROTATE first — swing around the obstacle at rest length;
            // (2) only if rotation CAN'T help (no clearer side, i.e. the camera is being forced straight into
            // something) does the arm EXTEND toward MaxDistance as a last resort, backing off on a wider radius. A
            // hard cap at the wall distance guarantees no clipping; both responses ease in/out for smoothness.
            bool haveColl = TownCameraCollision.Enabled && TownCameraCollision.EnsureCache();
            if (!_seeded) _dist = (float)Math.Sqrt(Distance * Distance + Height * Height);   // rest 3D length

            // How obscured is the straight-back view? (fan clearance vs the full resting arm)
            float restArm = (float)Math.Sqrt(Distance * Distance + (camY - ty) * (camY - ty));
            float baseC = haveColl ? ClearanceFan(tx, ty, tz, camY, _yaw) : restArm;
            bool obscured = baseC < restArm - AutoClearPad;

            // Can rotation improve it? Probe both sides. This decides autorotate-vs-extend.
            bool canRotate = false;
            float lc = 0f, rc = 0f;
            if (obscured && haveColl)
            {
                lc = ClearanceFan(tx, ty, tz, camY, _yaw + AutoProbe);
                rc = ClearanceFan(tx, ty, tz, camY, _yaw - AutoProbe);
                canRotate = lc > baseC + 1f || rc > baseC + 1f;
            }

            // AUTOROTATE (primary): swing toward the clearer side when rotation helps and the player isn't manually
            // turning. Eased in via _rotBlend.
            _rotBlend += (((obscured && canRotate) ? 1f : 0f) - _rotBlend) * BlendEase;
            if (AutoRotate && dYaw == 0f && obscured && canRotate)
                _yaw += ((lc >= rc) ? AutoRotateRate : -AutoRotateRate) * _rotBlend;

            // EXTEND (last resort): only when obscured AND rotation can't help — the camera is being forced into an
            // object, so back the arm out toward MaxDistance. Eased in via _extBlend; stays at rest otherwise.
            bool stuck = obscured && !canRotate;
            _extBlend += ((stuck ? 1f : 0f) - _extBlend) * BlendEase;

            // Everything below is in TRUE 3D. The arm has a resting horizontal reach (Distance) and vertical rise
            // (H = Height + stick). Its rest 3D length = hypot(Distance, H); we extend that 3D length toward
            // MaxDistance only when stuck, and ease it. Then resolve the horizontal reach r and vertical rise v.
            float H = Height + _heightOffset;
            float restD3 = (float)Math.Sqrt(Distance * Distance + H * H);
            float distTarget = restD3 + (MaxDistance - restD3) * _extBlend;
            _dist += (distTarget - _dist) * DistEase;
            if (_dist < TownCameraCollision.MinDistance) _dist = TownCameraCollision.MinDistance;

            float s = _dist / restD3;    // uniform scale of the rest arm (keeps the rest elevation angle)
            float r = Distance * s;      // desired horizontal reach
            float v = H * s;             // desired vertical rise

            // Wall as a HORIZONTAL cap (measured along the rest elevation, so distant terrain below doesn't false-
            // trigger; vertical walls give the same horizontal distance at any elevation).
            float capH = haveColl ? ArmHit(tx, ty, tz, _yaw, Distance, H, MaxDistance) - TownCameraCollision.Margin : r;
            if (capH < 0f) capH = 0f;
            if (r > capH)   // a wall is inside our horizontal reach
            {
                float tanE = H / Distance;   // rest elevation (vertical per horizontal)
                float pulled3d = capH * (float)Math.Sqrt(1f + tanE * tanE);   // 3D length if we pull straight in
                if (pulled3d >= TownCameraCollision.MinDistance)
                {
                    r = capH; v = capH * tanE;   // pull in along the rest elevation (still ≥ MinDistance in 3D)
                }
                else
                {
                    // Even MinDistance won't fit horizontally → HUG the wall at capH and RAISE the camera so the 3D
                    // distance stays MinDistance (goes overhead instead of clipping). e.g. wall 2 away, min 10 → the
                    // camera sits 2 out and ~9.8 up.
                    r = Math.Min(capH, TownCameraCollision.MinDistance);
                    v = (float)Math.Sqrt(Math.Max(0f,
                            TownCameraCollision.MinDistance * TownCameraCollision.MinDistance - r * r));
                }
            }

            float px = (float)(tx + r * Math.Sin(_yaw));
            float py = ty + v;
            float pz = (float)(tz + r * Math.Cos(_yaw));

            // DIAGNOSTIC (throttled): the arm state while it's working, for tuning.
            if (haveColl && (obscured || r < Distance - 1f) && (++_logCounter % 45) == 0)
                Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[TownCamera] arm: d3={_dist:0} r={r:0} v={v:0} " +
                    $"capH={capH:0} rot={_rotBlend:0.00} ext={_extBlend:0.00} canRot={canRotate}");

            // Write the NEXT targets and let Step ease current→next at the engine's SYNCED 60 Hz — that filters
            // our async sampling jitter, so lateral/strafe motion is smooth (a direct current-write shows the
            // phase drift as chop). Safe now that FollowOff deterministically decouples vanilla: nothing else
            // writes current/next, so the ease can't drag the camera toward a vanilla value.
            Memory.WriteFloat(cam + OffNextPos,     px);
            Memory.WriteFloat(cam + OffNextPos + 4, py);
            Memory.WriteFloat(cam + OffNextPos + 8, pz);
            Memory.WriteFloat(cam + OffNextRef,     tx);
            Memory.WriteFloat(cam + OffNextRef + 4, ty);
            Memory.WriteFloat(cam + OffNextRef + 8, tz);
            if (!_seeded)   // seed current once so it doesn't ease in from wherever the camera was
            {
                Memory.WriteFloat(cam + OffCurPos,     px);
                Memory.WriteFloat(cam + OffCurPos + 4, py);
                Memory.WriteFloat(cam + OffCurPos + 8, pz);
                Memory.WriteFloat(cam + OffCurRef,     tx);
                Memory.WriteFloat(cam + OffCurRef + 4, ty);
                Memory.WriteFloat(cam + OffCurRef + 8, tz);
                _seeded = true;
            }
        }

        // Line-of-sight clearance from the look-at toward the camera position at a given yaw: the ray-hit distance
        // (== the full target→camera length if nothing blocks). Bigger = clearer. Used by AUTOROTATE to compare
        // sides without committing the camera there.
        private static float Clearance(float tx, float ty, float tz, float camY, float yaw)
        {
            float cx = (float)(tx + Distance * Math.Sin(yaw));
            float cz = (float)(tz + Distance * Math.Cos(yaw));
            float dx = cx - tx, dy = camY - ty, dz = cz - tz;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-3f) return 0f;
            return TownCameraCollision.NearestHit(tx, ty, tz, dx / len, dy / len, dz / len, len);
        }

        // Padded clearance: the TIGHTEST of the centre line and two rays AutoFan to each side. A camera merely
        // grazing a wall (bare peek) has one side ray hitting close, so this reads low and autorotate keeps swinging
        // until there's real lateral room — the "padding" that keeps the camera off walls.
        private static float ClearanceFan(float tx, float ty, float tz, float camY, float yaw)
        {
            float c = Clearance(tx, ty, tz, camY, yaw);
            float l = Clearance(tx, ty, tz, camY, yaw + AutoFan);
            float r = Clearance(tx, ty, tz, camY, yaw - AutoFan);
            return Math.Min(c, Math.Min(l, r));
        }

        // HORIZONTAL distance from the player to the nearest wall along the arm bearing `yaw`, cast at the arm's
        // rest elevation (defined by horizRef/vertRef) out to `max3d` (a 3D length). Returns ~horizontal(max3d) if
        // clear. Casting at the true elevation (not flat) keeps distant terrain below from false-triggering; for a
        // vertical wall the horizontal distance is the same at any elevation anyway. Used as the no-clip cap on the
        // arm's horizontal reach r.
        private static float ArmHit(float tx, float ty, float tz, float yaw, float horizRef, float vertRef, float max3d)
        {
            float dl = (float)Math.Sqrt(horizRef * horizRef + vertRef * vertRef);
            if (dl < 1e-3f) return max3d;
            float ch = horizRef / dl;   // cos(elevation) = horizontal fraction of the arm direction
            float ux = ch * (float)Math.Sin(yaw), uy = vertRef / dl, uz = ch * (float)Math.Cos(yaw);
            float hit = TownCameraCollision.NearestHit(tx, ty, tz, ux, uy, uz, max3d);
            return hit * ch;   // horizontal component of the hit distance
        }
    }
}
