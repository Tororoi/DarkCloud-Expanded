using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;
using static Dark_Cloud_Improved_Version.FishingLabelAllocator;
using static Dark_Cloud_Improved_Version.FishingScriptBuilder;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ladder handling for swapped-in town allies. Toan's climb overlay (chara\c01dhashigo.chr) is Toan-rigged,
    /// so mounting a ladder as any other model loads that overlay onto the wrong skeleton → crash/corruption.
    /// <see cref="ElfPatches"/>' ladder-refusal cave hooks the mount inside EdMoveChara (@0x16C0FC): when the
    /// mailbox <see cref="BlockLadder"/> is nonzero it skips both EdInitHashigo and the climbing flag (no mount,
    /// no crash) and sets <see cref="RefusalRequested"/>=1 on that blocked Cross press (one-shot per press).
    ///
    /// This ticker sets BlockLadder for any non-Toan ally and consumes RefusalRequested. Per-ally response:
    /// • XIAO — a ballistic JUMP replaces the climb: read the ladder's endpoint vectors from the live event-param
    ///   buffer (0x21D196A0, the same struct EdInitHashigo consumes), compute the arc in C#, bake it into a small
    ///   STB VM loop (per-frame x/y/z integration + _SET_NPC_POS + YIELD — 60 fps smooth) written into the spare
    ///   label 405 (the ally-swap's proven vehicle) and fired via start_event_no. She keeps her facing (no
    ///   turn-around), holds the leap pose (char+0xc64 play-once flag, asserted per tick during the event), and
    ///   the arc ends ~1 unit above the target so the engine's own airborne branch plays the natural landing.
    ///   Down = small hop + gravity to the base point; up = a springy jump to the top point, same gravity.
    /// • Others — the shake-head refusal (or block+log until their models are built). Also the fallback for Xiao
    ///   if the ladder data reads invalid.
    /// </summary>
    internal static class TownLadder
    {
        internal static bool Enabled = true;

        private const string Tag = "[Ladder] ";

        private const long BlockLadder      = CodeCaves.Mailbox.BlockLadder;       // EE 0x21F10074 — mod writes (0=vanilla mount / Toan)
        private const long RefusalRequested = CodeCaves.Mailbox.RefusalRequested;  // EE 0x21F10078 — cave sets on a blocked press, mod clears
        private const long IdleMotionMailbox = CodeCaves.Mailbox.IdleMotionOverride; // EE 0x21F10070 — plays a motion in place of idle
        private const long IdleMotionFlags   = CodeCaves.Mailbox.IdleMotionFlags;    // EE 0x21F10080 — +0xc64 playback flags for the override

        // Xiao's refusal = town slot 7 (assemble_town_model.py grafts e613c04cat's no/hold/return there,
        // frames 228-278 @ speed 0.30 ≈ 170 engine frames ≈ 2.8 s). Played ONCE via the engine's own one-shot:
        // flags bit1 (2) makes CCharacter::Step freeze on the clip's LAST frame instead of looping (same flag
        // the land animation uses), so the release timer below needs no precision — the cat holds the final
        // neutral pose until we let go, and the snap back to idle is invisible.
        private const int PlayOnceFlag   = 2;
        private const int XiaoAlly       = 1;
        private const int RefusalIndex   = 7;
        private const int RefusalTicks   = 62;   // ≈3.1 s — covers the 2.8 s clip; the hold hides the slack

        private static int _refusalLeft;   // >0 = refusal playing (holds the idle-motion mailbox)

        // ── Xiao's ladder JUMP ──────────────────────────────────────────────────────────────────────
        // Live event-param buffer EdGetEvent refreshes while the player stands on an event point (and
        // EdInitHashigo consumes for the vanilla mount): +0x00 type (4/5 = the ladder's two ends),
        // +0x10 / +0x20 the two endpoint vectors (x,y,z,w floats), +0x30 a third (dismount) vector.
        private const long LadderParam   = 0x21D196A0;
        private const int  LeapIndex     = 8;      // town slot 8 = fall(leap), s86 c04cat 205-214
        private const float Gravity      = 0.10f;  // per-frame² — jump feel knob (bigger = snappier)
        private const float DownHop      = 1.5f;   // small up-kick starting a down-jump
        private const float LandMargin   = 1.0f;   // end the arc this far ABOVE the target → engine lands her natively
        private const int   MinFrames = 20, MaxFrames = 90;
        private const int   ReadyIndex     = 10;   // choreography slot: s86 #3 crouch/ready
        private const int   FloatUpIndex   = 11;   // choreography slot: e04c04cat #5 float/hop-up, fast (ascent)
        private const int   WalkIndex      = 2;    // town walk — the align walk-in
        private const int   WalkBackIndex  = 12;   // choreography slot: the walk clip baked REVERSED — backwards steps
        // Pre-jump alignment (both directions): walk to the mount point turning to FACE the ladder, then a few
        // backwards walk-steps to line up, then the ready crouch LOOPING for ~0.25 s, then launch.
        private const float WalkSpeed  = 0.30f;   // align walk, units/frame
        private const float BackSpeed  = 0.20f;   // backwards steps, units/frame
        private const float BackDist   = 6.0f;    // how far she backs off the mount point
        private const int   ReadyHold  = 30;      // 0.5 s of looping ready crouch
        private const int   AlignMin = 8, AlignMax = 60;
        private const float ApexOver     = 8f;     // up-jumps peak this far ABOVE the target ledge → a clear rise-over-and-drop
        // Vanilla ladder camera (RE'd from gedit/system/event.stb label 1 — Toan's climb):
        // _SET_FOLLOW_CAMERA(chara, dist=30·h/50, height=h(=50 default), angle=ladderYaw−π, ease=8), released
        // with _RESET_CAMERA_ANGLE(π). The ladder's yaw is the Y of the param buffer's THIRD vector (+0x30).
        private const float CamDist   = 30f;
        private const float CamHeight = 50f;
        private const float CamEase   = 8f;

        private static int _jumpPhase;     // 0=idle, 1=fired (waiting for the event to start), 2=event running
        private static int _jumpTicks;     // safety timeout inside the current phase

        /// <summary>True while a refusal animation is playing — <see cref="TownIdleSit"/> must stay off the
        /// idle-motion mailbox for the duration (it would overwrite the shake with the sit).</summary>
        internal static bool RefusalPlaying => _refusalLeft > 0;

        internal static void Tick()
        {
            if (!Enabled) return;
            try { TickCore(); }
            catch (Exception e)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
            }
        }

        private static void TickCore()
        {
            if (Memory.ReadByte(Addresses.mode) != 2) { CancelRefusal(); return; }   // town only (the cave is inside the town mover)

            // Non-Toan cannot mount ladders (Toan's climb overlay would crash on their rig). Written every tick so
            // it stays correct across swaps and emulator resets; the cave is town-only so its value is inert elsewhere.
            Memory.WriteInt(BlockLadder, AllySwapPrototype.CurrentAlly != 0 ? 1 : 0);

            // Refusal playback: the engine plays the shake once and HOLDS its last frame (PlayOnceFlag). Release
            // when the countdown runs out, the player moves (motion left idle/override — don't let a re-idle
            // replay the shake), or an event takes over walking.
            if (_refusalLeft > 0)
            {
                bool moved = false;
                uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(chara))
                {
                    int m = Memory.ReadInt(Memory.ToMmu(chara) + 0xc68);
                    moved = m != RefusalIndex && m != 0;   // run/walk/fall/land = the player broke the pose
                }
                if (--_refusalLeft == 0 || moved ||
                    Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking)
                    CancelRefusal();
            }

            TickJump();

            // Consume the cave's one-shot refusal request (raised only on a blocked Cross press at a ladder).
            if (Memory.ReadInt(RefusalRequested) != 0)
            {
                Memory.WriteInt(RefusalRequested, 0);
                if (AllySwapPrototype.CurrentAlly == XiaoAlly && _jumpPhase == 0)
                {
                    if (TryFireJump()) return;                         // Xiao JUMPS the ladder
                    _refusalLeft = RefusalTicks;                       // bad ladder data → shake-head fallback
                    Memory.WriteInt(IdleMotionFlags, PlayOnceFlag);    // flags BEFORE index (the cave reads both per frame)
                    Memory.WriteInt(IdleMotionMailbox, RefusalIndex);
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "ladder press blocked (non-Toan)");
            }
        }

        /// <summary>Jump lifecycle bookkeeping. The play-once flags ride INSIDE the script now
        /// (_SET_NPC_MOTION's 4-arg form writes char+0xc64 directly), so this only tracks the phase — gating
        /// new ladder presses while a jump runs and expiring stale state if an event never starts/ends.</summary>
        private static void TickJump()
        {
            if (_jumpPhase == 0) return;
            bool walking = Memory.ReadInt(EditLoop.GameMode) == EditLoop.GameModeWalking;
            _jumpTicks++;

            if (_jumpPhase == 1)
            {
                if (!walking) { _jumpPhase = 2; _jumpTicks = 0; }
                else if (_jumpTicks > 20) { _jumpPhase = 0; }    // event never started (~1 s) — give up
            }
            else if (walking || _jumpTicks > 160)                 // event done (or 8 s safety cap — align+back+arc)
            {
                _jumpPhase = 0;
            }
        }

        /// <summary>Release a refusal-in-progress: zero BOTH mailboxes (index AND flags — a stranded flags=2
        /// would freeze the next idle/sit on its last frame; a stranded index would loop the shake forever).</summary>
        private static void CancelRefusal()
        {
            if (_refusalLeft <= 0) return;
            _refusalLeft = 0;
            Memory.WriteInt(IdleMotionMailbox, 0);
            Memory.WriteInt(IdleMotionFlags, 0);
        }

        /// <summary>Read the ladder the player just pressed at, pick the FAR endpoint as the jump target,
        /// solve the arc, bake it into label 405 and fire it. False = data didn't look like a ladder.</summary>
        private static bool TryFireJump()
        {
            int type = Memory.ReadInt(LadderParam);
            if (type != 4 && type != 5) return false;

            uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) return false;
            long c = Memory.ToMmu(chara);
            float px = Memory.ReadFloat(c + EditLoop.CharaPosition);
            float py = Memory.ReadFloat(c + EditLoop.CharaPosition + 4);
            float pz = Memory.ReadFloat(c + EditLoop.CharaPosition + 8);

            float[] a = ReadVec(LadderParam + 0x10), b = ReadVec(LadderParam + 0x20);
            float da = Dist2(a, px, pz), db = Dist2(b, px, pz);
            float[] tgt   = da >= db ? a : b;                     // the end we are NOT standing at (jump target)
            float[] mount = da >= db ? b : a;                     // the end we ARE at (alignment anchor)
            float h = tgt[1] + LandMargin - py;
            if (Math.Abs(h) < 2f || Math.Abs(h) > 500f) return false;   // not a real ladder drop/climb

            // FACING: she turns to face the ladder for the jump. DOWN = toward the drop (the target's horizontal
            // direction — well-defined). UP = the ladder's own yaw (param vec 3's Y — the horizontal delta to
            // the top is too small to aim by).
            float ladderYaw = Memory.ReadFloat(LadderParam + 0x34);
            float yaw0 = Memory.ReadFloat(c + EditLoop.CharaRotation + 4);
            float face = h < 0
                ? (float)Math.Atan2(tgt[0] - mount[0], tgt[2] - mount[2])
                : ladderYaw;

            // Alignment plan: walk (turning) to the mount point, then BackDist backwards along -facing → the
            // arc launches from that backed-up start S.
            float ax = mount[0], az = mount[2];                                  // align point (keep her ground y)
            float fsin = (float)Math.Sin(face), fcos = (float)Math.Cos(face);
            float sx = ax - fsin * BackDist, sz = az - fcos * BackDist;          // arc start after the back-up
            float alignDist = (float)Math.Sqrt(Dist2(mount, px, pz));
            int Ta = Math.Min(Math.Max((int)(alignDist / WalkSpeed), AlignMin), AlignMax);
            int Tb = (int)Math.Ceiling(BackDist / BackSpeed);

            // Ballistic solve (per-frame units, 60 fps): shared gravity; DOWN = small hop then fall to the base.
            // UP = peak ApexOver ABOVE the ledge, then drop onto it, so the leap→land transition reads clearly.
            int T; float vy0;
            if (h < 0) { vy0 = DownHop; T = (int)((vy0 + Math.Sqrt(vy0 * vy0 + 2f * Gravity * -h)) / Gravity); }
            else
            {
                vy0 = (float)Math.Sqrt(2f * Gravity * (h + ApexOver));                       // reaches peak = h+ApexOver
                T = (int)Math.Ceiling(vy0 / Gravity + Math.Sqrt(2f * ApexOver / Gravity));   // rise + fall-back onto the ledge
            }
            T = Math.Min(Math.Max(T, MinFrames), MaxFrames);
            float vx = (tgt[0] - sx) / T, vz = (tgt[2] - sz) / T;

            // Camera: vanilla ladder takeover (SET_FOLLOW_CAMERA at the ladder yaw). Release is relative to the
            // player's facing (EventMode does SetAngleSoon(facing + a) on event END); she ends the jump at the
            // ALIGNED facing, so solve for zero swing: facing + a == the ladder-cam angle. (Toan's facing at a
            // climb's end makes this the vanilla π.)
            float release = WrapAngle(ladderYaw + (float)Math.PI - face);

            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null || lab.Size <= 0) return false;

            var plan = new JumpPlan
            {
                Up = h > 0,
                Px = px, Py = py, Pz = pz, Yaw0 = yaw0, Face = face,
                Ax = ax, Az = az, Ta = Ta, Tb = Tb,
                DxA = (ax - px) / Ta, DzA = (az - pz) / Ta, DYaw = WrapAngle(face - yaw0) / Ta,
                DxB = -fsin * BackSpeed, DzB = -fcos * BackSpeed,
                Vx = vx, Vy0 = vy0, Vz = vz, T = T,
                LadderYaw = ladderYaw, Release = release,
            };

            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);
            WriteScript(stb, lab.Off, lab.Off + lab.Size, BuildJumpBytecode(plan),
                        $"ladder jump ({(plan.Up ? "up" : "down")}, align {Ta}f + back {Tb}f + arc {T}f)");
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            _jumpPhase = 1; _jumpTicks = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"jump {(plan.Up ? "UP" : "DOWN")} type={type} Ta={Ta} Tb={Tb} T={T} vy0={vy0:F2} " +
                $"ladderYaw={ladderYaw:F2} yaw0={yaw0:F2} face={face:F2} release={release:F2} " +
                $"mount=({ax:F1},{az:F1}) target=({tgt[0]:F1},{tgt[1]:F1},{tgt[2]:F1})");
            return true;
        }

        /// <summary>Everything the jump script needs, solved in C# and baked as float constants.</summary>
        private sealed class JumpPlan
        {
            internal bool Up;
            internal float Px, Py, Pz, Yaw0, Face;      // start pos + initial/aligned facing
            internal float Ax, Az;                      // mount (alignment) point
            internal int Ta, Tb, T;                     // align / back-up / arc frame counts
            internal float DxA, DzA, DYaw;              // per-frame align deltas
            internal float DxB, DzB;                    // per-frame back-up deltas
            internal float Vx, Vy0, Vz;                 // arc velocities
            internal float LadderYaw, Release;
        }

        private static float WrapAngle(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        private static float[] ReadVec(long addr) => new[]
            { Memory.ReadFloat(addr), Memory.ReadFloat(addr + 4), Memory.ReadFloat(addr + 8) };
        private static float Dist2(float[] v, float x, float z)
            { float dx = v[0] - x, dz = v[2] - z; return dx * dx + dz * dz; }

        /// <summary>The jump event (docs/town-swap-animation-map.md choreography). Locals: 0=frames left (int),
        /// 1=x, 2=y, 3=z, 4=vy, 5=yaw (floats).
        /// BOTH directions: walk to the mount point while turning to FACE the ladder (like Toan's align walk) →
        /// a few BACKWARDS walk-steps off it → ready crouch LOOPING ~0.25 s → launch. UP launches in the
        /// float/hop-up pose and switches to the LEAP at the arc's ZENITH (the loop switches the moment vy goes
        /// negative; same-id re-sets never restart). DOWN launches straight into the leap (vy starts positive,
        /// so the zenith switch fires immediately after the hop — a no-op for the pose).
        /// Camera: the VANILLA ladder takeover — _SET_FOLLOW_CAMERA at the ladder's yaw, released with
        /// _RESET_CAMERA_ANGLE at the zero-swing angle for HER facing. No landing code: the arc ends
        /// <see cref="LandMargin"/> above the target, so the engine's airborne branch lands her natively.</summary>
        private static StbWriter BuildJumpBytecode(JumpPlan p)
        {
            var w = new StbWriter();
            w.UseLocals(6);
            w.Yield(); w.Yield();
            EmitWorldCoordReset(w);

            // Toan's ladder camera, verbatim: follow the player at the ladder's facing angle (yaw − π).
            w.PushInt(StbCommands.SetFollowCamera); w.PushInt(-1);
            w.PushFloat(CamDist); w.PushFloat(CamHeight);
            w.PushFloat(p.LadderYaw - (float)Math.PI); w.PushFloat(CamEase); w.Ext(6);

            SetLocalFloat(w, 1, p.Px); SetLocalFloat(w, 2, p.Py); SetLocalFloat(w, 3, p.Pz);
            SetLocalFloat(w, 5, p.Yaw0);

            // ── ALIGN: walk to the mount point, turning to the ladder facing ──
            SetMotion(w, WalkIndex, -1f, 0);                  // walk, LOOPING
            SetLocalInt(w, 0, p.Ta);
            int alignLoop = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(p.DxA));
            AddToLocal(w, 3, () => w.PushFloat(p.DzA));
            AddToLocal(w, 5, () => w.PushFloat(p.DYaw));
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushFloat(0f); w.PushVarFloat(5); w.PushFloat(0f); w.Ext(5);
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, alignLoop);

            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);  // snap the exact final facing
            w.PushFloat(0f); w.PushFloat(p.Face); w.PushFloat(0f); w.Ext(5);

            // ── BACK-UP: a few backwards walk-steps off the mount point (facing held, REVERSED walk clip) ──
            SetMotion(w, WalkBackIndex, -1f, 0);              // looping
            SetLocalInt(w, 0, p.Tb);
            int backLoop = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(p.DxB));
            AddToLocal(w, 3, () => w.PushFloat(p.DzB));
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, backLoop);

            // ── READY: crouch looping for the hold ──
            SetMotion(w, ReadyIndex, -1f, 0);                 // LOOPING (not play-once)
            EmitYieldLoop(w, ReadyHold);

            // ── LAUNCH + ARC ──
            SetMotion(w, p.Up ? FloatUpIndex : LeapIndex);
            SetLocalInt(w, 0, p.T);
            SetLocalFloat(w, 4, p.Vy0);

            int loop = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(p.Vx));         // x += vx
            AddToLocal(w, 2, () => w.PushVarFloat(4));         // y += vy
            AddToLocal(w, 3, () => w.PushFloat(p.Vz));         // z += vz
            w.PushVarRefFloat(4); w.PushVarFloat(4); w.PushFloat(Gravity); w.Sub(); w.Store(); w.Pop();   // vy -= g

            // Past the zenith (vy < 0) the pose is the LEAP — same-id re-sets are no-ops, so this is one switch.
            w.PushVarFloat(4); w.PushFloat(0f); w.Cmp(StbWriter.CmpLt);
            int noSwitch = w.MarkForward();
            w.BrFalse(noSwitch);
            SetMotion(w, LeapIndex);
            w.PlaceMark(noSwitch);

            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, loop);

            // Release relative to HER facing so the follow cam takes over at the ladder-cam angle (zero swing).
            w.PushInt(StbCommands.ResetCameraAngle); w.PushFloat(p.Release); w.Ext(2);
            w.Ret();
            return w;
        }

        private static void EmitNpcPosFromLocals(StbWriter w)
        {
            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);
            w.PushVarFloat(1); w.PushVarFloat(2); w.PushVarFloat(3); w.Ext(5);
        }

        private static void EmitDecAndLoop(StbWriter w, int mark)
        {
            w.PushVarRef(0); w.PushVar(0); w.PushInt(1); w.Sub(); w.Store(); w.Pop();   // frames left -= 1
            w.PushVar(0); w.BrTrue(mark);                                               // loop while non-zero
        }

        /// <summary>_SET_NPC_MOTION's 4-arg form: (charaId, idx, speed, flags) — speed -1 = the KEY speed,
        /// flags land in char+0xc64 (bit1 = play once + hold last frame; 0 = loop). Same-id re-calls never
        /// restart.</summary>
        private static void SetMotion(StbWriter w, int idx, float speed = -1f, int flags = PlayOnceFlag)
        {
            w.PushInt(StbCommands.SetNpcMotion); w.PushInt(-1); w.PushInt(idx);
            w.PushFloat(speed); w.PushInt(flags); w.Ext(5);
        }

        /// <summary>Yield <paramref name="n"/> frames via a compact VM loop (local 0 as the counter — emitted
        /// only BEFORE the arc initializes it).</summary>
        private static void EmitYieldLoop(StbWriter w, int n)
        {
            SetLocalInt(w, 0, n);
            int loop = w.Mark();
            w.Yield();
            w.PushVarRef(0); w.PushVar(0); w.PushInt(1); w.Sub(); w.Store(); w.Pop();
            w.PushVar(0); w.BrTrue(loop);
        }

        private static void SetLocalInt(StbWriter w, int idx, int v)
            { w.PushVarRef(idx); w.PushInt(v); w.Store(); w.Pop(); }
        private static void SetLocalFloat(StbWriter w, int idx, float v)
            { w.PushVarRefFloat(idx); w.PushFloat(v); w.Store(); w.Pop(); }
        private static void AddToLocal(StbWriter w, int idx, Action pushDelta)
            { w.PushVarRefFloat(idx); w.PushVarFloat(idx); pushDelta(); w.Add(); w.Store(); w.Pop(); }
    }
}
