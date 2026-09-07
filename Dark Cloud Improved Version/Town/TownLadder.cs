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
        private static int _rubyDbgTick;   // TEMP: Ruby run-bug diagnostic cadence
        private static int _rubyDbgTable;  // TEMP: full motion-table dump cadence
        private static uint _rubyTableAddr; // TEMP: cached motion_info guest addr (from the periodic sample)
        private static bool _rubyEntryBad;  // TEMP: last watch verdict (edge-triggered logging)

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
        private const int   ReadyIndex     = 11;   // choreography slot: s86 #3 crouch/ready (10 = double-door now)
        private const int   FloatUpIndex   = 12;   // choreography slot: e04c04cat #5 float/hop-up, fast (ascent)
        private const int   WalkIndex      = 2;    // town walk — the align walk-in
        private const int   WalkBackIndex  = 13;   // choreography slot: the walk clip baked REVERSED — backwards steps

        // ── Osmond (ally 5) — catalog: down = jump-down dive → fall-loop → native land; up = HELICOPTER
        // backpack: propeller-out → start-fly → fly-loop ascent → reversed start-fly (descend) → reversed
        // propeller (stow) standing ON the ledge. Slots baked by assemble_town_model.py (11-16).
        private const int OsmondAlly        = 5;
        private const int OzJumpDownIndex   = 11;   // e403 #9 dive (launch; play-once holds the dive pose)
        private const int OzFallLoopIndex   = 8;    // town fall slot = e403 #10 fall-loop (LOOPS in flight)
        private const int OzPropellerIndex  = 12;   // e402 #16 propeller-out (deploy)
        private const int OzStartFlyIndex   = 13;   // e402 #17 start-fly
        private const int OzFlyLoopIndex    = 14;   // e402 #18 fly-loop (LOOPS during ascent)
        private const int OzRevStartFlyIndex = 15;  // reversed #17 — the descend-to-land
        private const int OzStowIndex       = 16;   // reversed #16 — stow the backpack
        private const float HeliRiseSpeed    = 0.5f;   // units/frame ascent (a LIFT, not a jump)
        private const float HeliDescendSpeed = 0.125f; // units/frame settle — MUST make the descend phase (Hover/this)
                                                       // outlast rev-start-fly (32 engine frames @0.5x) so the spin ramp-down completes
        private const float HeliForwardSpeed = 0.30f;  // units/frame drift over the ledge at hover height
        private const float OzLandSpeed      = 0.30f;  // e403 land clip at its authored pace (overlaps the descent via LandLead)
        private const int   OzLandFrames     = 50;     // ≈ 15 anim frames / OzLandSpeed — the sequence ends WITH the land clip
        private const float HeliHover        = 4f;     // rise this far above the ledge before settling
        private const int   HeliDeployFrames = 112;    // propeller-out at 0.5x ≈ 1.9 s
        private const float HeliFlightSpeed  = 0.5f;   // playback override for start-fly/fly-loop/rev-start-fly — SAME for all
                                                       // three so the hub spin's engine-frame rate is continuous across switches
        private const int   HeliStartFrames  = 30;     // start-fly to COMPLETION and not a frame more: window 240-255 spans
                                                       // 15 clip frames @0.5x = 30 engine frames. Slack here FREEZES the hub
                                                       // at the ramp's end pose (play-once hold) = visible still-blade frames
                                                       // before the loop takes over; cutting early snaps mid-ramp instead.
        private const int   HeliStowFrames   = 116;    // stow = 56 anim frames @0.5x = 112 engine frames + slack — the event must NOT end before the put-away finishes
        private const int   HeliMaxRise      = 400;    // ascent frame cap (Brownboo stilts ≈ 190 units)

        // ── Goro (ally 2) — the VANILLA treehouse climb, decoded from gedit/s01/event.stb: mount hop (e102 #3)
        // then ALTERNATING jump clips #4/#5 chained while a slow LINEAR rise runs (~0.117 u/f in the cutscene).
        // Down = jump off (#4) → fall-loop (#6) → scripted land (#3). Slots baked by assemble_town_model.py.
        private const int GoroAlly          = 2;
        private const int GrItemFall        = 8;    // town fall slot = e101 #3 falling (loops)
        private const int GrLandHop         = 9;    // town land slot = e101 #4 — a CROUCH: standing → bent over
        private const int GrHopA            = 11;   // e102 #4 — climb jump A
        private const int GrHopB            = 12;   // e102 #5 — climb jump B
        private const int GrRevLand         = 13;   // e101 #4 baked REVERSED: bent over → standing
        // VANILLA CLIMB MODEL (event-102 treehouse climb, fully RE'd from s01 event.stb + the ASQ engine):
        // the climb is BALLISTIC LEAPS — per-frame scripted arcs at the UNTOUCHED vanilla gravity (0.15),
        // alternating jump-kick clips RESTARTED each hop (one-shot + id change), constant facing, hop SE 361
        // per launch. Vanilla's 31-tick hop fits its 23.75u tree leaps; on a short ladder hop that duration
        // leaves a long post-crest fall (~13u back down) before each relaunch — jerky. Fix: each hop's TICK
        // COUNT is solved (not gravity) so the arc CRESTS LATE (~80% in) and settles under a unit.
        private const int   GoroClimbHops   = 4;      // the cutscene takes exactly 4 jumps up — always 4 quick hops
        private const int   GoroHopMaxT     = 31;     // vanilla hop duration (ticks incl. launch) = the cap
        private const int   GoroHopMinT     = 12;     // floor so short climbs don't become instant
        private const float GoroHopEndFall  = 0.5f;   // fall-speed at hop end the tick-solve targets (≈0.8u settle)
        private const float GoroHopG        = 0.15f;  // per-tick² hop gravity — vanilla, both directions
        private const int   GoroHopSe       = 361;    // the vanilla hop sound
        private const int   GoroKickFrames  = 20;     // the kick windows are 20 anim frames → speed = frames/ticks
        // The FINAL hop is the dramatic one: a taller arc overshooting the ledge, the kick paced to fill the
        // WHOLE arc (completing right where the land crouch takes over — at 1.2x it read as FLAILING; the
        // authored clip rate is 0.3), then the land crouch leading the touchdown.
        private const float GoroFinalOver      = 15f;   // final apex ≈ this far ABOVE the ledge
        private const float GoroFinalKickMax   = 1.0f;  // playback cap for tiny arcs
        private const int   GoroFinalLandLead  = 8;     // land crouch starts this many ticks before touchdown
        // Down-jump. The land clip is a one-way crouch (standing → bent over), so it can't loop as a pump —
        // the windup is REVERSED once (rise) then FORWARD once (sink into the crouch, continuous at the
        // standing seam), launch straight into the falling loop, a HIGH hop, and the crouch ONCE on impact.
        private const float GoroDownHop     = 2.1f;   // up-kick velocity: apex = v²/2g ≈ 15u above the ledge
        private const int   GoroLandCycleF  = 40;     // one land-clip play = 10 anim frames @0.25 KEY speed
        // Windup playback: faster than the impact crouch so it reads as a JUMP gather — the rise is brisk and
        // the forward sink is snappier still (plus a 2-frame beat at full crouch before the launch).
        private const int   GoroLandClipF     = 10;     // the crouch clip's anim-frame length
        private const float GoroWindRevSpeed  = 0.40f;  // reversed rise → 25 engine frames
        private const float GoroWindFwdSpeed  = 0.80f;  // forward sink → ~13 engine frames
        // ── Ruby (ally 3) — she FLOATS (catalog). UP: the mid-air float loop rising straight up the ladder
        // line, a drift over the ledge at hover height, a slow settle, idle. DOWN: her e228 jump clip (the
        // bake FREEZES its root travel — the cutscene's 43u leap) into the mid-air hold loop, on a floaty
        // low-gravity arc, ending LandMargin above the base so the engine lands her natively (its airborne
        // branch = fall slot 8 the float, land slot 9 the idle settle — the catalog's "settle to idle").
        private const int RubyAlly          = 3;
        private const int RbFloatIndex      = 8;      // town fall slot = e228 #0 mid-air float (LOOPS)
        private const int RbJumpIndex       = 11;     // e228 #14 jump (root frozen at bake)
        private const int RbJumpLoopIndex   = 12;     // e228 #15 mid-air hold (LOOPS, pinned at float rest)
        private const float RbRiseSpeed     = 0.35f;  // units/frame float ascent — SAME as the descent
        private const float RbForwardSpeed  = 0.30f;  // drift over the ledge at hover height
        private const float RbSettleSpeed   = 0.15f;  // gentle drop onto the ledge
        private const float RbDescendSpeed  = 0.35f;  // down-ladder main descent — a controlled float, no gravity
        private const float RbHover         = 3f;     // rise this far above the ledge before the drift
        private const int   RbMaxRise       = 500;    // ascent frame cap
        // Idle→float transition softener: enter the float loop at a crawl while lifting gently off the
        // ground, then let it play at its authored pace — reads as her gathering into the hover.
        private const float RbEntrySpeed    = 0.03f;  // float-loop playback during the entry beat
        private const int   RbEntryF        = 24;     // entry beat length (frames)
        private const float RbEntryLift     = 0.08f;  // units/frame gentle lift during the beat (~2u total)
        private const int   RbRampF         = 8;      // rise-speed ramp: 8f @0.25 then 8f @0.35 before cruise
        private const int   RubyRefuseTicks = 36;     // slot-7 dmg-out: 20f @0.2 ≈ 100 engine frames ≈ 1.8 s

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

            // TEMP DIAGNOSTIC (Ruby run bug): per-tick WATCH on motion_info[1] — log the exact moment it
            // flips away from the parsed {60,80,0.40} (and back), with game state for correlation.
            if (AllySwapPrototype.CurrentAlly == RubyAlly && _rubyTableAddr != 0)
            {
                long e1 = Memory.ToMmu(_rubyTableAddr) + 0x10;
                int s0 = Memory.ReadInt(e1), s1 = Memory.ReadInt(e1 + 4);
                float s2 = Memory.ReadFloat(e1 + 8);
                bool bad = s1 != 80;                      // parsed value is end=80; anything else = corrupted
                if (bad != _rubyEntryBad)
                {
                    _rubyEntryBad = bad;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[RubyDbg] ENTRY1 {(bad ? "CORRUPTED" : "restored")} -> [{s0}..{s1}]@{s2:F2} " +
                        $"gameMode={Memory.ReadInt(EditLoop.GameMode)} mapEvt={Memory.ReadInt(EditLoop.StartEventNo)}");
                    if (bad)
                    {
                        // The engine's cfg-parse globals persist after a parse: config_file @0x2A24DC is
                        // the LAST cfg text ReadInfo consumed — the misparse writes through motion_info
                        // @0x2A24B0, so dumping both names the culprit file directly.
                        uint cf = Memory.ReadUInt(0x202A24DC) & Memory.PhysAddrMask;
                        int cfSize = Memory.ReadInt(0x202A24E0);
                        uint miG = Memory.ReadUInt(0x202A24B0);
                        int keyNo = Memory.ReadInt(0x202A24AC);
                        string text = "?";
                        if (Memory.IsValidGuest(cf) && cfSize > 0)
                        {
                            var bytes = new byte[Math.Min(cfSize, 640)];
                            for (int k = 0; k < bytes.Length; k++) bytes[k] = Memory.ReadByte(Memory.ToMmu(cf) + k);
                            var sb2 = new System.Text.StringBuilder();
                            for (int row = 0x0C0; row < Math.Min(bytes.Length, 0x180); row += 16)
                            {
                                sb2.Append($"  +{row:X3}: ");
                                for (int k = row; k < row + 16 && k < bytes.Length; k++) sb2.Append($"{bytes[k]:X2} ");
                                sb2.Append("  ");
                                for (int k = row; k < row + 16 && k < bytes.Length; k++)
                                    sb2.Append(bytes[k] >= 0x20 && bytes[k] < 0x7F ? (char)bytes[k] : '.');
                                sb2.Append('\n');
                            }
                            text = sb2.ToString();
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[RubyDbg] last-parsed cfg @0x{cf:X8} size={cfSize} motion_info=0x{miG:X8} key_no={keyNo}\n{text}");
                    }
                }
            }
            if (AllySwapPrototype.CurrentAlly == RubyAlly && ++_rubyDbgTick >= 30)
            {
                _rubyDbgTick = 0;
                uint ch = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(ch))
                {
                    long cc = Memory.ToMmu(ch);
                    int id = Memory.ReadInt(cc + 0xc68);
                    int flags = Memory.ReadInt(cc + 0xc64);
                    float spd = Memory.ReadFloat(cc + 0xc60);
                    int mode = Memory.ReadInt(cc + 0xc70);
                    uint st = Memory.ReadUInt(cc + 0xc20) & Memory.PhysAddrMask;
                    if (Memory.IsValidGuest(st))
                    {
                        long s = Memory.ToMmu(st);
                        float frame = Memory.ReadFloat(s + 0x10);
                        int cur = Memory.ReadInt(s + 0x24);
                        int prev = Memory.ReadInt(s + 0x28);
                        uint mi = Memory.ReadUInt(s + 0x64) & Memory.PhysAddrMask;
                        if (Memory.IsValidGuest(mi)) _rubyTableAddr = mi;   // arm the per-tick entry1 watch
                        string entry = "?";
                        if (Memory.IsValidGuest(mi) && cur >= 0 && cur < 32)
                        {
                            long e = Memory.ToMmu(mi) + cur * 0x10;
                            entry = $"[{Memory.ReadInt(e)}..{Memory.ReadInt(e + 4)}]@{Memory.ReadFloat(e + 8):F2}";
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[RubyDbg] id={id} flags={flags:X} spdOvr={spd:F2} mode={mode} frame={frame:F1} idx={cur} prev={prev} win={entry}");
                        if (++_rubyDbgTable >= 4 && Memory.IsValidGuest(mi))
                        {
                            _rubyDbgTable = 0;
                            var sb = new System.Text.StringBuilder($"[RubyDbg] TABLE@0x{mi:X8} ");
                            long tb = Memory.ToMmu(mi);
                            for (int k = 0; k < 14; k++)
                                sb.Append($"{k}:[{Memory.ReadInt(tb + k * 0x10)}..{Memory.ReadInt(tb + k * 0x10 + 4)}]@{Memory.ReadFloat(tb + k * 0x10 + 8):F2} ");
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + sb.ToString());
                        }
                    }
                }
            }

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
                int ally = AllySwapPrototype.CurrentAlly;
                if (_jumpPhase == 0 && (ally == XiaoAlly || ally == OsmondAlly || ally == GoroAlly || ally == RubyAlly))
                {
                    bool fired = ally switch
                    {
                        XiaoAlly => TryFireJump(),
                        OsmondAlly => TryFireOsmond(),
                        RubyAlly => TryFireRuby(),
                        _ => TryFireGoro(),
                    };
                    if (fired) return;
                    if (ally == XiaoAlly || ally == GoroAlly || ally == RubyAlly)    // refusal clip in slot 7
                    {
                        _refusalLeft = ally == XiaoAlly ? RefusalTicks
                                     : ally == RubyAlly ? RubyRefuseTicks
                                     : 28;                                 // Goro's "no" = 30f @0.5 ≈ 60 engine frames
                        Memory.WriteInt(IdleMotionFlags, PlayOnceFlag);    // flags BEFORE index (the cave reads both per frame)
                        Memory.WriteInt(IdleMotionMailbox, RefusalIndex);
                    }
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
            else if (walking || _jumpTicks > 300)                 // event done (or 15 s safety cap — heli ascents run long)
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

            // Alignment plan: walk (turning) to the mount point. UP-jumps then take BackDist backwards steps
            // along -facing (cat lining up a leap at the BOTTOM); DOWN-jumps launch straight off the mount
            // point — backing away from a ledge edge reads wrong, so no back-up at the TOP.
            float ax = mount[0], az = mount[2];                                  // align point (keep her ground y)
            float fsin = (float)Math.Sin(face), fcos = (float)Math.Cos(face);
            float back = h > 0 ? BackDist : 0f;
            float sx = ax - fsin * back, sz = az - fcos * back;                  // arc start (== mount for down)
            float alignDist = (float)Math.Sqrt(Dist2(mount, px, pz));
            int Ta = Math.Min(Math.Max((int)(alignDist / WalkSpeed), AlignMin), AlignMax);
            int Tb = back > 0f ? (int)Math.Ceiling(back / BackSpeed) : 0;

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
            if (!WriteLadderScript(stb, lab, BuildJumpBytecode(plan),
                        $"ladder jump ({(plan.Up ? "up" : "down")}, align {Ta}f + back {Tb}f + arc {T}f)")) return false;
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
            // clip parametrization (Xiao defaults; Osmond's down-jump overrides)
            internal int ReadyHoldF = ReadyHold;        // 0 = skip the pre-launch windup entirely
            internal int ReadyIdx   = ReadyIndex;       // windup clip (Xiao: looping crouch)
            internal float ReadySpeed = -1f;
            internal int ReadyFlags = 0;                // 0=loop; Goro's one-way crouch plays ONCE (PlayOnceFlag)
            internal int Ready2Idx  = 0, Ready2F = 0;   // optional PRE-windup clip (played once, before ReadyIdx)
            internal float Ready2Speed = -1f;
            internal int LaunchIdx  = LeapIndex;        // down-launch pose
            internal int LaunchFlags = PlayOnceFlag;    // Goro launches straight into his LOOPING fall clip (0)
            internal float LaunchSpeed = -1f;           // launch clip playback (-1 = KEY speed)
            internal int LaunchF    = 0;                // >0: switch to AirIdx after this many ARC frames (lets a
                                                        // launch clip play out) instead of at the zenith (vy<0)
            internal int AirIdx     = LeapIndex;        // pose from the zenith on
            internal int AirFlags   = PlayOnceFlag;     // Osmond's fall-loop wants 0 (looping)
            // SCRIPTED landing (0 = hand back airborne and let the engine land natively, Xiao's way): the arc
            // is solved to GROUND level, then snap to the target, play LandIdx once, hold LandFrames, Ret
            // grounded. Used by Osmond — the native land won't trigger off a ~1-unit engine drop.
            internal int LandIdx = 0, LandFrames = 0, LandLead = 0;   // LandLead: start the land clip this many frames BEFORE touchdown
            internal int LandFlags = PlayOnceFlag;      // 0 = the land clip LOOPS through LandFrames (Goro's 2 cycles)
            internal float G = Gravity;                 // arc gravity (Goro's vanilla descent uses 0.15)
            internal float LandSpeed = -1f;
            internal float TgtX, TgtY, TgtZ;
        }

        private static float WrapAngle(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        /// <summary>Write a ladder script into label 405 ONLY if it fits. WriteScript refuses oversized
        /// scripts with just a log — and firing the label then runs its STALE content (the previous script's
        /// baked absolute positions → teleports). Returns false so the caller can fall back to the refusal.</summary>
        private static bool WriteLadderScript(long stb, ScriptLabel lab, StbWriter w, string what)
        {
            if (ScriptByteSize(w) > lab.Size)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                    $"script '{what}' needs {ScriptByteSize(w)}B > label {lab.Size}B — NOT firing (refusal instead)");
                return false;
            }
            WriteScript(stb, lab.Off, lab.Off + lab.Size, w, what);
            return true;
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

            EmitAlignWalk(w, p.Ta, p.DxA, p.DzA, p.DYaw, p.Face);

            // ── BACK-UP (up-jumps only): backwards walk-steps off the mount point (facing held, REVERSED
            // walk clip). Down-jumps skip this — the cat lines up her leap at the BOTTOM, not on a ledge edge.
            if (p.Tb > 0)
            {
                SetMotion(w, WalkBackIndex, -1f, 0);          // looping
                SetLocalInt(w, 0, p.Tb);
                int backLoop = w.Mark();
                AddToLocal(w, 1, () => w.PushFloat(p.DxB));
                AddToLocal(w, 3, () => w.PushFloat(p.DzB));
                EmitNpcPosFromLocals(w);
                w.Yield();
                EmitDecAndLoop(w, backLoop);
            }

            // ── READY: optional pre-windup clip once, then the windup clip (skipped when the launch clip has
            // its own wind-up) ──
            if (p.Ready2F > 0)
            {
                SetMotion(w, p.Ready2Idx, p.Ready2Speed);     // play-once, holds its end pose into ReadyIdx
                EmitYieldLoop(w, p.Ready2F);
            }
            if (p.ReadyHoldF > 0)
            {
                SetMotion(w, p.ReadyIdx, p.ReadySpeed, p.ReadyFlags);
                EmitYieldLoop(w, p.ReadyHoldF);
            }

            // ── LAUNCH + ARC ──
            SetMotion(w, p.Up ? FloatUpIndex : p.LaunchIdx, p.Up ? -1f : p.LaunchSpeed,
                      p.Up ? PlayOnceFlag : p.LaunchFlags);
            SetLocalInt(w, 0, p.T);
            SetLocalFloat(w, 4, p.Vy0);

            int loop = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(p.Vx));         // x += vx
            AddToLocal(w, 2, () => w.PushVarFloat(4));         // y += vy
            AddToLocal(w, 3, () => w.PushFloat(p.Vz));         // z += vz
            w.PushVarRefFloat(4); w.PushVarFloat(4); w.PushFloat(p.G); w.Sub(); w.Store(); w.Pop();   // vy -= g

            // The pose switches to the AIR clip past the zenith (vy < 0) — or, when LaunchF is set, only once
            // the launch clip has had its frames (Goro's reversed-land crouch-off needs to finish playing;
            // the zenith is ~10 ticks in). Same-id re-sets are no-ops.
            if (p.LaunchF > 0) { w.PushVar(0); w.PushInt(p.T - p.LaunchF); w.Cmp(StbWriter.CmpLe); }
            else               { w.PushVarFloat(4); w.PushFloat(0f); w.Cmp(StbWriter.CmpLt); }
            int noSwitch = w.MarkForward();
            w.BrFalse(noSwitch);
            SetMotion(w, p.AirIdx, -1f, p.AirFlags);
            w.PlaceMark(noSwitch);

            if (p.LandIdx > 0 && p.LandLead > 0)
            {
                // Start the land clip just BEFORE touchdown so its contact frames line up with the arrival
                // instead of playing after he has already stopped.
                w.PushVar(0); w.PushInt(p.LandLead); w.Cmp(StbWriter.CmpLe);
                int noLand = w.MarkForward();
                w.BrFalse(noLand);
                SetMotion(w, p.LandIdx, p.LandSpeed, p.LandFlags);
                w.PlaceMark(noLand);
            }

            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, loop);

            if (p.LandIdx > 0)
            {
                w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);   // snap exactly onto the ground
                w.PushFloat(p.TgtX); w.PushFloat(p.TgtY); w.PushFloat(p.TgtZ); w.Ext(5);
                SetMotion(w, p.LandIdx, p.LandSpeed, p.LandFlags);  // land clip (same-id re-set = no-op if LandLead already started it)
                EmitYieldLoop(w, p.LandFrames);
            }

            // Release relative to HER facing so the follow cam takes over at the ladder-cam angle (zero swing).
            w.PushInt(StbCommands.ResetCameraAngle); w.PushFloat(p.Release); w.Ext(2);
            w.Ret();
            return w;
        }

        /// <summary>The Toan-style align walk: walk (looping) to the mount point over Ta frames while turning
        /// to the ladder facing, then snap the exact facing. Locals 1/3/5 = x/z/yaw (already initialized).</summary>
        private static void EmitAlignWalk(StbWriter w, int ta, float dxA, float dzA, float dYaw, float face)
        {
            SetMotion(w, WalkIndex, -1f, 0);                  // walk, LOOPING
            SetLocalInt(w, 0, ta);
            int alignLoop = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(dxA));
            AddToLocal(w, 3, () => w.PushFloat(dzA));
            AddToLocal(w, 5, () => w.PushFloat(dYaw));
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushFloat(0f); w.PushVarFloat(5); w.PushFloat(0f); w.Ext(5);
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, alignLoop);

            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);  // snap the exact final facing
            w.PushFloat(0f); w.PushFloat(face); w.PushFloat(0f); w.Ext(5);
        }

        /// <summary>Osmond's ladder. DOWN reuses the ballistic builder with his clips (dive launch → looping
        /// fall → native land). UP is the HELICOPTER: align → deploy propeller → start-fly → fly-loop ascent
        /// (straight-line rise + horizontal drift to the top point) → hover, reversed start-fly descend →
        /// land ON the ledge exactly → stow the backpack → hand back grounded (no native fall).</summary>
        private static bool TryFireOsmond()
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
            float[] tgt   = da >= db ? a : b;
            float[] mount = da >= db ? b : a;
            float h = tgt[1] + LandMargin - py;
            if (Math.Abs(h) < 2f || Math.Abs(h) > 500f) return false;

            float ladderYaw = Memory.ReadFloat(LadderParam + 0x34);
            float yaw0 = Memory.ReadFloat(c + EditLoop.CharaRotation + 4);
            float face = h < 0
                ? (float)Math.Atan2(tgt[0] - mount[0], tgt[2] - mount[2])
                : ladderYaw;
            float ax = mount[0], az = mount[2];
            float alignDist = (float)Math.Sqrt(Dist2(mount, px, pz));
            int Ta = Math.Min(Math.Max((int)(alignDist / WalkSpeed), AlignMin), AlignMax);
            float release = WrapAngle(ladderYaw + (float)Math.PI - face);

            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null || lab.Size <= 0) return false;
            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);

            if (h < 0)
            {
                float hd = tgt[1] - py;                 // solve to GROUND level — the landing is scripted
                float vy0 = DownHop;
                int T = (int)((vy0 + Math.Sqrt(vy0 * vy0 + 2f * Gravity * -hd)) / Gravity);
                T = Math.Min(Math.Max(T, MinFrames), MaxFrames);
                var plan = new JumpPlan
                {
                    Up = false,
                    Px = px, Py = py, Pz = pz, Yaw0 = yaw0, Face = face,
                    Ax = ax, Az = az, Ta = Ta, Tb = 0,
                    DxA = (ax - px) / Ta, DzA = (az - pz) / Ta, DYaw = WrapAngle(face - yaw0) / Ta,
                    Vx = (tgt[0] - ax) / T, Vy0 = vy0, Vz = (tgt[2] - az) / T, T = T,
                    LadderYaw = ladderYaw, Release = release,
                    ReadyHoldF = 0,                     // the dive clip carries its own wind-up
                    LaunchIdx = OzJumpDownIndex,
                    AirIdx = OzFallLoopIndex, AirFlags = 0,   // fall-loop LOOPS through the descent
                    LandIdx = 9, LandSpeed = OzLandSpeed, LandLead = Math.Min(12, T / 3),
                    LandFrames = Math.Max(OzLandFrames - Math.Min(12, T / 3), 8),   // land starts pre-touchdown; hold = the clip's remainder, then control returns
                    TgtX = tgt[0], TgtY = tgt[1], TgtZ = tgt[2],
                };
                if (!WriteLadderScript(stb, lab, BuildJumpBytecode(plan),
                            $"osmond dive (down, align {Ta}f + arc {T}f + land)")) return false;
            }
            else
            {
                // Straight UP from the base point to hover height, THEN forward over the ledge, THEN settle.
                int Tup = Math.Min(Math.Max((int)((h - LandMargin + HeliHover) / HeliRiseSpeed), MinFrames), HeliMaxRise);
                float fwdDist = (float)Math.Sqrt((tgt[0] - ax) * (tgt[0] - ax) + (tgt[2] - az) * (tgt[2] - az));
                int Tf = Math.Max((int)(fwdDist / HeliForwardSpeed), 8);
                // PHASE LOCK: the descend hand-off is only seamless if the fly-loop's spin phase is deterministic
                // when we switch — pad the flight to a whole number of loop periods (10 engine frames each at the
                // 0.5x override of the 10-clip-frame loop). The pad rides the forward phase (a beat of hover).
                Tf += (10 - (Tup + Tf) % 10) % 10;
                int Tdown = Math.Max((int)(HeliHover / HeliDescendSpeed), 8);
                if (!WriteLadderScript(stb, lab,
                            BuildOsmondHeliBytecode(px, py, pz, yaw0, face, ax, az, Ta,
                                                    (ax - px) / Ta, (az - pz) / Ta, WrapAngle(face - yaw0) / Ta,
                                                    Tup, (tgt[0] - ax) / Tf, (tgt[2] - az) / Tf, Tf, Tdown,
                                                    tgt[0], tgt[1], tgt[2], ladderYaw, release),
                            $"osmond heli (up, align {Ta}f + rise {Tup}f + fwd {Tf}f)")) return false;
            }
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            _jumpPhase = 1; _jumpTicks = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"osmond {(h < 0 ? "DIVE down" : "HELI up")} type={type} Ta={Ta} h={h:F1} " +
                $"ladderYaw={ladderYaw:F2} face={face:F2} release={release:F2} target=({tgt[0]:F1},{tgt[1]:F1},{tgt[2]:F1})");
            return true;
        }

        /// <summary>Goro's ladder. DOWN = the ballistic builder with his clips (jump-off launch → looping fall
        /// → scripted land hop). UP = the VANILLA climb: align → mount hop → slow LINEAR rise while the two jump
        /// clips ALTERNATE (the cutscene's _ASQ chain) → a small ballistic hop onto the ledge → land hop.</summary>
        private static bool TryFireGoro()
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
            float[] tgt   = da >= db ? a : b;
            float[] mount = da >= db ? b : a;
            float h = tgt[1] + LandMargin - py;
            if (Math.Abs(h) < 2f || Math.Abs(h) > 500f) return false;

            float ladderYaw = Memory.ReadFloat(LadderParam + 0x34);
            float yaw0 = Memory.ReadFloat(c + EditLoop.CharaRotation + 4);
            float face = h < 0
                ? (float)Math.Atan2(tgt[0] - mount[0], tgt[2] - mount[2])
                : ladderYaw;
            float ax = mount[0], az = mount[2];
            float alignDist = (float)Math.Sqrt(Dist2(mount, px, pz));
            int Ta = Math.Min(Math.Max((int)(alignDist / WalkSpeed), AlignMin), AlignMax);
            float release = WrapAngle(ladderYaw + (float)Math.PI - face);

            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null || lab.Size <= 0) return false;
            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);

            if (h < 0)
            {
                float hd = tgt[1] - py;
                float vy0 = GoroDownHop;
                int T = (int)((vy0 + Math.Sqrt(vy0 * vy0 + 2f * GoroHopG * -hd)) / GoroHopG);   // vanilla descent g=0.15
                T = Math.Min(Math.Max(T, MinFrames), MaxFrames);
                int lead = Math.Min(12, T / 3);
                var plan = new JumpPlan
                {
                    Up = false,
                    Px = px, Py = py, Pz = pz, Yaw0 = yaw0, Face = face,
                    Ax = ax, Az = az, Ta = Ta, Tb = 0,
                    DxA = (ax - px) / Ta, DzA = (az - pz) / Ta, DYaw = WrapAngle(face - yaw0) / Ta,
                    Vx = (tgt[0] - ax) / T, Vy0 = vy0, Vz = (tgt[2] - az) / T, T = T, G = GoroHopG,
                    LadderYaw = ladderYaw, Release = release,
                    Ready2Idx = GrRevLand, Ready2Speed = GoroWindRevSpeed,           // windup 1: brisk rise once
                    Ready2F = (int)(GoroLandClipF / GoroWindRevSpeed),
                    ReadyIdx = GrLandHop, ReadySpeed = GoroWindFwdSpeed,             // windup 2: snappy sink once
                    ReadyHoldF = (int)(GoroLandClipF / GoroWindFwdSpeed) + 2,        // +2f beat at full crouch
                    ReadyFlags = PlayOnceFlag,
                    LaunchIdx = GrItemFall, LaunchFlags = 0,          // straight into the LOOPING fall as he leaves
                    AirIdx = GrItemFall, AirFlags = 0,                // (same clip past the zenith — a no-op re-set)
                    LandIdx = GrLandHop,                              // impact: the crouch ONCE (absorb the hit)
                    LandLead = lead, LandFrames = Math.Max(GoroLandCycleF - lead, 8),
                    TgtX = tgt[0], TgtY = tgt[1], TgtZ = tgt[2],
                };
                if (!WriteLadderScript(stb, lab, BuildJumpBytecode(plan),
                            $"goro jump-down (windup {2 * GoroLandCycleF}f + arc {T}f + land)")) return false;
            }
            else
            {
                // Vanilla hop model, per the cutscene rewatch: exactly 4 quick ballistic hops STRAIGHT UP at
                // the ladder plane (no lateral, no drift — drifting toward the ledge point clipped the wall);
                // the LAST hop carries onto the ledge. Vanilla gravity throughout; the hop TICK COUNT N is
                // solved per climb so each arc CRESTS LATE — smallest N whose end fall-speed
                // g(N+1)/2 − dy/N reaches GoroHopEndFall (monotonic in N) — then v0 is the EXACT discrete
                // solution of the script's integrator (y+=vy then vy-=g over N ticks): gain = N·v0 − g·N(N−1)/2 = dy.
                float climbH = h - LandMargin;
                float dy = climbH / GoroClimbHops;
                int N = GoroHopMinT;
                while (N < GoroHopMaxT && GoroHopG * (N + 1) / 2f - dy / N < GoroHopEndFall) N++;
                float v0y = dy / N + GoroHopG * (N - 1) / 2f;
                // Each hop occupies the full VANILLA 31-tick slot: the arc runs its N ticks, then he DWELLS on
                // the rung for the remainder — same overall cadence as the cutscene (launch speed is already
                // gentler than the down-jump's fall; it was the back-to-back chaining that read as too fast).
                // The kick clip paces across the whole slot so the pose flows through the dwell.
                float kickSpeed = (float)GoroKickFrames / GoroHopMaxT;       // ≈0.65 — the vanilla playback rate
                if (!WriteLadderScript(stb, lab,
                            BuildGoroClimbBytecode(px, py, pz, yaw0, face, ax, az, Ta,
                                                   (ax - px) / Ta, (az - pz) / Ta, WrapAngle(face - yaw0) / Ta,
                                                   GoroClimbHops, N, v0y, kickSpeed,
                                                   tgt[0], tgt[1], tgt[2], ladderYaw, release),
                            $"goro climb (align {Ta}f + {GoroClimbHops} hops of {dy:F1}u @{N}t)")) return false;
            }
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            _jumpPhase = 1; _jumpTicks = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"goro {(h < 0 ? "JUMP down" : "CLIMB up")} type={type} Ta={Ta} h={h:F1} face={face:F2} release={release:F2}");
            return true;
        }

        /// <summary>Ruby's ladder. DOWN = a floaty low-gravity arc: the jump clip launches, the mid-air hold
        /// loop takes the tail, and the arc ends LandMargin above the base for the engine's native landing
        /// (float → idle settle). UP = the FLOAT: rise straight up the ladder line in the float loop, drift
        /// over the ledge at hover height, settle down onto it, idle.</summary>
        private static bool TryFireRuby()
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
            float[] tgt   = da >= db ? a : b;
            float[] mount = da >= db ? b : a;
            float h = tgt[1] + LandMargin - py;
            if (Math.Abs(h) < 2f || Math.Abs(h) > 500f) return false;

            float ladderYaw = Memory.ReadFloat(LadderParam + 0x34);
            float yaw0 = Memory.ReadFloat(c + EditLoop.CharaRotation + 4);
            float face = h < 0
                ? (float)Math.Atan2(tgt[0] - mount[0], tgt[2] - mount[2])
                : ladderYaw;
            float ax = mount[0], az = mount[2];
            float alignDist = (float)Math.Sqrt(Dist2(mount, px, pz));
            int Ta = Math.Min(Math.Max((int)(alignDist / WalkSpeed), AlignMin), AlignMax);
            float release = WrapAngle(ladderYaw + (float)Math.PI - face);

            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null || lab.Size <= 0) return false;
            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);

            float fwdDist = (float)Math.Sqrt((tgt[0] - ax) * (tgt[0] - ax) + (tgt[2] - az) * (tgt[2] - az));
            int Tf = Math.Max((int)(fwdDist / RbForwardSpeed), 8);
            if (h < 0)
            {
                // Pure controlled FLOAT down — no ballistics: lift-off beat, drift out over the edge, glide
                // straight down to just above the base, settle the last bit, idle. Mirrors the ascent.
                float drop = py + RbEntryF * RbEntryLift - (tgt[1] + RbHover);
                int Tv = Math.Max((int)(drop / RbDescendSpeed), 4);   // constant speed, no duration clamp
                int Tdown = Math.Max((int)(RbHover / RbSettleSpeed), 8);
                if (!WriteLadderScript(stb, lab,
                            BuildRubyFloatBytecode(px, py, pz, yaw0, face, ax, az, Ta,
                                                   (ax - px) / Ta, (az - pz) / Ta, WrapAngle(face - yaw0) / Ta,
                                                   false, Tv, (tgt[0] - ax) / Tf, (tgt[2] - az) / Tf, Tf, Tdown,
                                                   tgt[0], tgt[1], tgt[2], ladderYaw, release),
                            $"ruby float-down (align {Ta}f + fwd {Tf}f + glide {Tv}f)")) return false;
            }
            else
            {
                float preRise = RbEntryF * RbSettleSpeed + RbRampF * 0.25f;   // beat + ramp climb
                // NO duration clamp: constant float speed — longer ladders simply take longer.
                int Tup = Math.Max((int)((h - LandMargin + RbHover - preRise) / RbRiseSpeed), 4);
                int Tdown = Math.Max((int)(RbHover / RbSettleSpeed), 8);
                if (!WriteLadderScript(stb, lab,
                            BuildRubyFloatBytecode(px, py, pz, yaw0, face, ax, az, Ta,
                                                   (ax - px) / Ta, (az - pz) / Ta, WrapAngle(face - yaw0) / Ta,
                                                   true, Tup, (tgt[0] - ax) / Tf, (tgt[2] - az) / Tf, Tf, Tdown,
                                                   tgt[0], tgt[1], tgt[2], ladderYaw, release),
                            $"ruby float-up (align {Ta}f + rise {Tup}f + fwd {Tf}f)")) return false;
            }
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            _jumpPhase = 1; _jumpTicks = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"ruby {(h < 0 ? "FLOAT down" : "FLOAT up")} type={type} Ta={Ta} h={h:F1} face={face:F2} release={release:F2}");
            return true;
        }

        /// <summary>Ruby's float flight, both directions: align walk → ENTRY BEAT (float loop at a crawl,
        /// gentle lift off the ground — the softened idle→float transition) → UP: rise, drift over the
        /// ledge, settle DOWN onto it / DOWN: drift out over the edge, glide straight down, settle → snap,
        /// idle. Same shape as Osmond's heli minus the deploy/stow phases.</summary>
        private static StbWriter BuildRubyFloatBytecode(float px, float py, float pz, float yaw0, float face,
                                                        float ax, float az, int ta,
                                                        float dxA, float dzA, float dYaw,
                                                        bool up, int tvert, float dxF, float dzF, int tf, int tdown,
                                                        float tx, float ty, float tz,
                                                        float ladderYaw, float release)
        {
            var w = new StbWriter();
            w.UseLocals(6);
            w.Yield(); w.Yield();
            EmitWorldCoordReset(w);

            w.PushInt(StbCommands.SetFollowCamera); w.PushInt(-1);
            w.PushFloat(CamDist); w.PushFloat(CamHeight);
            w.PushFloat(ladderYaw - (float)Math.PI); w.PushFloat(CamEase); w.Ext(6);

            SetLocalFloat(w, 1, px); SetLocalFloat(w, 2, py); SetLocalFloat(w, 3, pz);
            SetLocalFloat(w, 5, yaw0);
            EmitAlignWalk(w, ta, dxA, dzA, dYaw, face);

            void Drift(int ticks, float dx, float dy, float dz)
            {
                if (ticks <= 0) return;
                SetLocalInt(w, 0, ticks);
                int m = w.Mark();
                if (dx != 0f) AddToLocal(w, 1, () => w.PushFloat(dx));
                if (dy != 0f) AddToLocal(w, 2, () => w.PushFloat(dy));
                if (dz != 0f) AddToLocal(w, 3, () => w.PushFloat(dz));
                EmitNpcPosFromLocals(w);
                w.Yield();
                EmitDecAndLoop(w, m);
            }

            SetMotion(w, RbFloatIndex, RbEntrySpeed, 0);      // ENTRY BEAT: float loop at a crawl...
            if (up)
            {
                // ...already RISING gently through the transition (no dead hover before the climb —
                // mirrors how the down sequence blends motion through its settle), then RAMP the climb
                // speed instead of stepping to it (0.15 → 0.25 → 0.35 → 0.45 reads as her accelerating).
                Drift(RbEntryF, 0f, RbSettleSpeed, 0f);
                SetMotion(w, RbFloatIndex, -1f, 0);           // same id — only the speed changes (no restart)
                Drift(RbRampF, 0f, 0.25f, 0f);
                Drift(tvert, 0f, RbRiseSpeed, 0f);            // cruise up the ladder line
                Drift(tf, dxF, 0f, dzF);                      // drift forward over the ledge at hover height
                Drift(tdown, 0f, -RbSettleSpeed, 0f);         // settle gently onto the ledge
            }
            else
            {
                Drift(RbEntryF, 0f, RbEntryLift, 0f);         // ...with a gentle lift off the edge
                SetMotion(w, RbFloatIndex, -1f, 0);
                Drift(tf, dxF, 0f, dzF);                      // drift out over the edge
                Drift(tvert, 0f, -RbDescendSpeed, 0f);        // glide straight down to just above the base
                Drift(tdown, 0f, -RbSettleSpeed, 0f);         // settle the last stretch
            }

            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);  // snap exactly onto the target point (grounded)
            w.PushFloat(tx); w.PushFloat(ty); w.PushFloat(tz); w.Ext(5);
            SetMotion(w, 0, -1f, 0);                          // idle — the settle
            EmitYieldLoop(w, 20);

            w.PushInt(StbCommands.ResetCameraAngle); w.PushFloat(release); w.Ext(2);
            w.Ret();
            return w;
        }

        private static StbWriter BuildGoroClimbBytecode(float px, float py, float pz, float yaw0, float face,
                                                        float ax, float az, int ta,
                                                        float dxA, float dzA, float dYaw,
                                                        int nHops, int nTicks, float v0y, float kickSpeed,
                                                        float tx, float ty, float tz,
                                                        float ladderYaw, float release)
        {
            // Locals: 0=tick counter, 1=x, 2=y, 3=z, 4=vy, 5=yaw (align only). Hops are UNROLLED in C#
            // (alternation + last-hop carry decided at build time); each hop is an nTicks per-frame arc loop —
            // the vanilla 0x5bc4 hop helper's shape.
            var w = new StbWriter();
            w.UseLocals(7);
            w.Yield(); w.Yield();
            EmitWorldCoordReset(w);

            w.PushInt(StbCommands.SetFollowCamera); w.PushInt(-1);
            w.PushFloat(CamDist); w.PushFloat(CamHeight);
            w.PushFloat(ladderYaw - (float)Math.PI); w.PushFloat(CamEase); w.Ext(6);

            SetLocalFloat(w, 1, px); SetLocalFloat(w, 2, py); SetLocalFloat(w, 3, pz);
            SetLocalFloat(w, 5, yaw0);
            EmitAlignWalk(w, ta, dxA, dzA, dYaw, face);       // (vanilla runs in at 0.5 — our align walk suffices)

            // Arc segment emitter: `ticks` per-frame steps of y+=vy; vy-=g (vy carries across segments in
            // local 4), plus optional constant horizontal drift.
            void Arc(int ticks, float hx, float hz)
            {
                if (ticks <= 0) return;
                SetLocalInt(w, 0, ticks);
                int m = w.Mark();
                AddToLocal(w, 2, () => w.PushVarFloat(4));    // y += vy
                w.PushVarRefFloat(4); w.PushVarFloat(4); w.PushFloat(GoroHopG); w.Sub(); w.Store(); w.Pop();   // vy -= g
                if (hx != 0f) AddToLocal(w, 1, () => w.PushFloat(hx));
                if (hz != 0f) AddToLocal(w, 3, () => w.PushFloat(hz));
                EmitNpcPosFromLocals(w);
                w.Yield();
                EmitDecAndLoop(w, m);
            }

            // Hops UNROLLED in C# so the kick clips ALTERNATE with certainty. Hops 1..n−1 rise straight up
            // one rung, cresting late, each filling the vanilla 31-tick slot (arc + dwell on the rung).
            for (int i = 0; i < nHops - 1; i++)
            {
                int clip = (i % 2 == 0) ? GrHopA : GrHopB;    // A,B,A,B — alternating id CHANGE restarts each (vanilla)
                w.PushInt(StbCommands.PlaySe); w.PushInt(GoroHopSe); w.Ext(2);   // hop SE per launch
                SetMotion(w, clip, kickSpeed);                // one-shot, paced across the full hop slot
                SetLocalFloat(w, 4, v0y);                     // vy = launch velocity
                Arc(nTicks, 0f, 0f);
                if (nTicks < GoroHopMaxT)                     // dwell on the rung — fills the vanilla hop slot
                    EmitYieldLoop(w, GoroHopMaxT - nTicks);
            }

            // FINAL hop — the dramatic one: a taller arc (apex ≈ GoroFinalOver above the ledge), the kick
            // played to COMPLETION (its end pose then HOLDS through the descent — no fall clip, it looked
            // wrong here), the land crouch leading the touchdown, horizontal carry onto the ledge point.
            float dyF = (ty - py) / nHops;                    // this hop's net rise (same rung spacing)
            float v0L = (float)Math.Sqrt(2f * GoroHopG * (dyF + GoroFinalOver));
            int NL = 0;                                       // discrete tick count until the arc falls back to +dyF
            for (float yy = 0f, vv = v0L; (vv > 0f || yy > dyF) && NL < 90; NL++) { yy += vv; vv -= GoroHopG; }
            int segC = Math.Min(GoroFinalLandLead, NL);       // tail: land crouch already playing
            // Kick paced to complete EXACTLY at the land handoff — spans the arc, no whip, no frozen gap.
            float finKick = Math.Min(GoroFinalKickMax, (float)GoroKickFrames / Math.Max(NL - segC, 1));
            float fx = (tx - ax) / NL, fz = (tz - az) / NL;
            int finClip = ((nHops - 1) % 2 == 0) ? GrHopA : GrHopB;
            w.PushInt(StbCommands.PlaySe); w.PushInt(GoroHopSe); w.Ext(2);
            SetMotion(w, finClip, finKick);
            SetLocalFloat(w, 4, v0L);
            Arc(NL - segC, fx, fz);
            if (segC > 0) { SetMotion(w, GrLandHop); Arc(segC, fx, fz); }

            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);  // snap exactly onto the ledge point
            w.PushFloat(tx); w.PushFloat(ty); w.PushFloat(tz); w.Ext(5);
            SetMotion(w, GrLandHop);                          // no-op if the lead already started it
            EmitYieldLoop(w, 50);

            w.PushInt(StbCommands.ResetCameraAngle); w.PushFloat(release); w.Ext(2);
            w.Ret();
            return w;
        }

        private static StbWriter BuildOsmondHeliBytecode(float px, float py, float pz, float yaw0, float face,
                                                         float ax, float az, int ta,
                                                         float dxA, float dzA, float dYaw,
                                                         int tup, float dxF, float dzF, int tf, int tdown,
                                                         float tx, float ty, float tz,
                                                         float ladderYaw, float release)
        {
            var w = new StbWriter();
            w.UseLocals(6);
            w.Yield(); w.Yield();
            EmitWorldCoordReset(w);

            w.PushInt(StbCommands.SetFollowCamera); w.PushInt(-1);
            w.PushFloat(CamDist); w.PushFloat(CamHeight);
            w.PushFloat(ladderYaw - (float)Math.PI); w.PushFloat(CamEase); w.Ext(6);

            SetLocalFloat(w, 1, px); SetLocalFloat(w, 2, py); SetLocalFloat(w, 3, pz);
            SetLocalFloat(w, 5, yaw0);
            EmitAlignWalk(w, ta, dxA, dzA, dYaw, face);

            SetMotion(w, OzPropellerIndex, 0.5f);             // deploy the backpack (once, sped)
            EmitYieldLoop(w, HeliDeployFrames);
            SetMotion(w, OzStartFlyIndex, HeliFlightSpeed);   // spin-up — plays FULLY (ramp ends at full rate, aligned)
            EmitYieldLoop(w, HeliStartFrames);

            SetMotion(w, OzFlyLoopIndex, HeliFlightSpeed, 0); // fly-loop, LOOPING through the whole flight
            SetLocalInt(w, 0, tup);                           // ── phase 1: STRAIGHT UP at the base point ──
            int rise = w.Mark();
            AddToLocal(w, 2, () => w.PushFloat(HeliRiseSpeed));
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, rise);

            SetLocalInt(w, 0, tf);                            // ── phase 2: FORWARD over the ledge, holding hover ──
            int fwd = w.Mark();
            AddToLocal(w, 1, () => w.PushFloat(dxF));
            AddToLocal(w, 3, () => w.PushFloat(dzF));
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, fwd);

            SetMotion(w, OzRevStartFlyIndex, HeliFlightSpeed);   // descend — plays FULLY (spin ramps down inside it)
            SetLocalInt(w, 0, tdown);
            int fall = w.Mark();
            AddToLocal(w, 2, () => w.PushFloat(-HeliDescendSpeed));
            EmitNpcPosFromLocals(w);
            w.Yield();
            EmitDecAndLoop(w, fall);

            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);  // snap exactly onto the ledge (grounded — no native fall)
            w.PushFloat(tx); w.PushFloat(ty); w.PushFloat(tz); w.Ext(5);
            SetMotion(w, OzStowIndex, 0.5f);                  // stow the backpack
            EmitYieldLoop(w, HeliStowFrames);

            w.PushInt(StbCommands.ResetCameraAngle); w.PushFloat(release); w.Ext(2);
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
