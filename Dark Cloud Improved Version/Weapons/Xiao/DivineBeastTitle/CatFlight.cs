using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.DivineBeastTitle;
using static Dark_Cloud_Improved_Version.CatCape;
using static Dark_Cloud_Improved_Version.CatCopy;
using static Dark_Cloud_Improved_Version.CatTextures;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The cat in flight: the cave handshake (arm, poll, bind, hide, disarm), aim and targeting, the hit's element and the guard crush, and the flight step. One of the <see cref="DivineBeastTitle"/> classes, which share their members through using static.</summary>
    internal static class CatFlight
    {
        // ─────────────────────────────────────── the cave handshake ────────────────────────────────────────

        /// <summary>At the charge threshold: give the cave the growth reciprocal and the head rest offset (cat space ×
        /// scale), zero its counters, hide the copy (scale 0 — the cave owns position/scale/opacity from here) and set
        /// state 3: the next NEW pellet binds on its birth frame. Arming while a previous cat still rides its pellet
        /// clears it, so a new charged shot always starts clean.</summary>
        internal static void ArmCave()
        {
            Memory.WriteInt  (CodeCaves.Mailbox.CatPelletSlot, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatState, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGrowInv, 1f / GrowFrames);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadX, CatScale * _headX);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadH, CatScale * _headH);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadZ, CatScale * _headZ);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGrowFrames, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGrowN, GrowFrames);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGravity, FallGravity);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloorH, Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4));
            Memory.WriteInt  (CodeCaves.Mailbox.CatTargetPtr, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandStopFrame, LandStopFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandEndFrame, LandEndFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatTrackHalf, _look.Track ? 1f : 0f);   // the winged cat re-aims until halfway down from the apex
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandLead, LandLeadFrames);
            Memory.WriteInt  (CodeCaves.Mailbox.CatMoveKey, KeyWalk);                // a brisk walk reads better than the run
            Memory.WriteFloat(CodeCaves.Mailbox.CatMoveFrac, MoveFrac);
            Memory.WriteFloat(CodeCaves.Mailbox.CatMoveAbs, MoveSpeedAbs);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatLaunch, FloatLaunchFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatStart, FloatStartFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatRate, CharacterMotion.MotionSpeedUseKey);   // KEY rate — FloatRate is baked into the KEY entry at spawn (see RegisterSlot)
            Memory.WriteFloat(CodeCaves.Mailbox.CatFallBlend, 1f / FallBlendSteps);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFallBlendFrames, FallBlendSteps);   // the cave switches float → fall this many frames before the land clip starts
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, GlowScale);
            Memory.WriteFloat(CodeCaves.Mailbox.CatScaleMul, CatScale);          // the size the cave grows the cat to on the pellet
            _glowFade = -1;
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowFlags, GlowFlags);
            WriteGlowName();                                                      // (clears CatGlowReady: the copy's texture entries are remade per spawn — rebind)
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowPull, GlowPull);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowLift, GlowLift);
            Memory.WriteFloat(CodeCaves.Mailbox.CatBlendDefault, BlendDefault);
            Memory.WriteFloat(CodeCaves.MotionCave + MotionType.StateSpeed, BlendDefault);   // the copy's channel: a hit mid-fall can leave the slow fade armed
            Memory.WriteInt  (CodeCaves.Mailbox.CatSitKey, KeySit);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeUp, ProbeUp);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeDown, ProbeDown);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeFront, ProbeFront);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeBack, ProbeBack);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRateBase, RateBase);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRatePerSpeed, RatePerSpeed);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRateMax, RateMax);
            Memory.WriteInt  (CodeCaves.Mailbox.CatIdleKey, KeyStand);
            Memory.WriteInt  (CodeCaves.Mailbox.CatBlocked, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceRange, RangeFor(_target));
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceFrames, PounceFrames);   // per target: ApplyFlightTime
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitEntry, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitLatch, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitDamage, 0);                 // 0 = not stamped yet: the cave tests no touch until WriteHitStamps at bind. A stale non-zero value lets the hidden copy deal a 1-damage hit on its first frame
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitAttr, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatKickStrength, KickStrength);
            Memory.WriteFloat(CodeCaves.Mailbox.CatKickDecay, KickDecay);
            _hitFade = false;
            Memory.WriteFloat(CodeCaves.Mailbox.CatReadyEnd, ReadyEndFrame);
            Memory.WriteInt  (CodeCaves.Mailbox.CatPounceFly, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatFloatKey, KeyFloat);
            Memory.WriteFloat(CodeCaves.Mailbox.CatReadyStart, ReadyStartFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceMaxDist, RangeFor(_target) * 2f);
            long sl = SlotAddr();
            Memory.WriteVec3(sl + CCharacter.CharScale, 0f, 0f, 0f);
            Memory.WriteFloat(sl + CCharacter.NpcOpacity, 0f);
            _scale = 0f; _alpha = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = true; _disarmTicks = 0;
            _phase = Phase.Resident; _phaseStart = GameClock.Now;
            Memory.WriteInt  (CodeCaves.Mailbox.CatState, 3);                   // waiting — armed
            Log("shot released — the cave binds the next pellet on its birth frame");
        }

        internal static void DisarmCave()
        {
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }   // CatGlowOn is PollCave's (the glow may linger past the cat)
            _caveOwns = false; _disarmTicks = 0;
        }

        private static bool _pounceLogged, _sitLogged;
        private static int _retargetTick;
        private static int _pounceKind;               // 1 ground, 2 flying (log only)
        /// <summary>Follow the cave's state: 1 = it bound a pellet (note it, face along the pellet, log), 2 = that
        /// pellet ended (hold the last placed spot, fade, then hide again). A shot-less release disarms it.</summary>
        internal static void PollCave()
        {
            if (!Active) return;
            int state = Memory.ReadInt(CodeCaves.Mailbox.CatState);
            if (_disarmTicks > 0 && --_disarmTicks == 0 && state == 3) { DisarmCave(); Hide(); Log("charge released without a shot — cat stays hidden"); return; }
            if (_target >= 0 && state >= 4) WriteTargetAim();                                                  // the aim point follows the target's body every tick
            // A lock-on made after the cat picked its target wins, or it keeps a far one: checked every ~0.5 s
            // while walking or crouched. A closed mimic keeps the cat crouched (the cave holds the ready clip on its last frame on CatHoldReady).
            if (_target >= 0 && (state == 6 || state == 10) && ++_retargetTick >= 30) { _retargetTick = 0; RetargetToLockOn(state); }
            // Hold the crouch while the target cannot be hit: a chest-mimic still shut, or any enemy inside its invincibility
            // frames (`_STATUS_SET_MUTEKI`: 9 after a hit, 100 when a mimic wakes, 1000 dying) — CheckDmg skips every hit then.
            if (state == 1 || state == 4 || state == 5 || state == 8 || state == 11) CrushGuardsNearCat();
            bool hold = _target >= 0 && state >= 4 && (IsUnopenedMimic(_target) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.HitStunTimer)) > 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatHoldReady, hold ? 1 : 0);
            if (hold != _holdLogged)
            {
                _holdLogged = hold;
                Log((hold ? $"target slot {_target} cannot be hit yet (shut mimic / invincibility frames) — crouching until it can" : "target hittable — leaping"));
                // Released: a chest-mimic's init label ran when it woke (its guard windows — the disc bake makes the wake a
                // guard — are registered only now); crush them for this flight so the leap lands through the guard.
                if (!hold && _target >= 0) CrushGuard(_target, again: true);
            }
            // The glow follows the cat's visibility. While the cat fades out (hit or 20 s expiry: opacity over FadeTicks) the glow
            // SHRINKS on its own, longer clock (GlowFadeTicks), so it lingers a beat where the cat vanished — size is the
            // fade, since the torch tint global does not take. Its clock starts with the fade and keeps running past Hide();
            // a new bind resets it. Once the shrink has run out the glow stays OFF until the next bind: PollCave runs before
            // Step in the tick, so snapping back to "follow the cat" here would show one full-size frame before Step hid it.
            bool fading = _hitFade || _phase == Phase.Fading;
            if (fading && _glowFade < 0) _glowFade = 0;
            if (_glowFade >= 0 && _glowFade < GlowFadeTicks) _glowFade++;
            bool shrinking = _glowFade >= 0 && _glowFade < GlowFadeTicks, done = _glowFade >= GlowFadeTicks;
            bool glow = !done && ((state != 0 && state != 3) || shrinking);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, glow ? 1 : 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, GlowScale * (shrinking ? 1f - _glowFade / (float)GlowFadeTicks : done ? 0f : 1f));
            int ent = Memory.ReadInt(CodeCaves.Mailbox.CatHitEntry);
            if (ent != 0)                                                        // the cave planted a damage entry at a contact (the pellet's own recipe)
            {
                Memory.WriteInt(CodeCaves.Mailbox.CatHitEntry, 0);
                lock (_planted) _planted.Add((ent - 1, PlantedLifeTicks, true));
                int hitAttr = Memory.ReadInt(CodeCaves.Mailbox.CatHitAttr);
                Log($"cat contact — damage entry {ent - 1} planted natively (base {Memory.ReadInt(CodeCaves.Mailbox.CatHitDamage)}, attr 0x{hitAttr:X} = {ElementNameOf(hitAttr)})");
            }
            if (state >= 4 && state <= 11 && state != 9)                        // every cave-owned state: face the cave's live direction (it re-aims in the ready crouch, the float wind-up and the take-off)
            {
                float ddx = Memory.ReadFloat(CodeCaves.Mailbox.CatDirX), ddz = Memory.ReadFloat(CodeCaves.Mailbox.CatDirZ);
                if (ddx * ddx + ddz * ddz > 1e-6f) { _dirX = ddx; _dirY = ddz; _yaw = (float)Math.Atan2(ddx, ddz); }
            }
            if (state >= 4 && state <= 11 && LifetimeOver()) { FadeKeepingPose(); return; }   // the lifetime is absolute from the bind: on the ground, crouched at a shut mimic or mid-leap alike, it shrinks and fades as it stands
            switch (state)
            {
                case 1:
                    if (_phase != Phase.Flying)
                    {
                        long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                        int slot = Memory.ReadInt(CodeCaves.Mailbox.CatPelletSlot) - 1;
                        if (!Memory.IsValidGuest(pool) || slot < 0) break;
                        _pool = pool; _pelletSlot = slot; _alpha = 1f; _fade = 0; _glowFade = -1; _caveOwns = true; _disarmTicks = 0;
                        ReseedCape();
                        long va = PlayerShotPool.VelAddr(pool, slot);
                        FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
                        _target = PickTarget();                                   // locked-on first, else the nearest to Xiao
                        _floor = _target >= 0 ? Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(_target) + 4)
                                              : Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
                        _pelletDamage = Memory.ReadInt(PlayerShotPool.DamageAddr(pool, slot));
                        WriteHitStamps();                                         // the entry the cave will plant: pellet + attack, the weapon's element
                        CrushGuard(_target);                                      // Guard Crush: the target's guard windows are dropped for this flight
                        ApplyFlightTime();
                        _phase = Phase.Flying; _phaseStart = GameClock.Now; _hitDone = false; _boundAt = GameClock.Now; _gaitLogged = false; _blockedLogged = false; _pounceLogged = false; _pounceKind = 0; _sitLogged = false; _retargetTick = 0;
                        _flightFrame0 = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame);
                        Memory.WriteFloat(CodeCaves.Mailbox.CatFloorH, _floor);
                        WriteTargetAim();
                        Log(
                            $"cat bound to pellet slot {slot} on its birth frame" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + $" (motion frame {_flightFrame0:F1})");
                    }
                    break;
                case 4:                                                          // falling: off the pellet's line, or a flying pounce's arc
                    if (_phase != Phase.Falling)
                    {
                        _phase = Phase.Falling; _phaseStart = GameClock.Now;
                        if (Memory.ReadInt(CodeCaves.Mailbox.CatPounceFly) != 0)
                        {
                            Log($"vertical leap at flying enemy slot {_target}, v=({Memory.ReadFloat(CodeCaves.Mailbox.CatVx):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVh):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVz):F2})/frame (decided at distance {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgDist):F1})");
                            break;
                        }
                        Log(
                            $"full size after {Memory.ReadInt(CodeCaves.Mailbox.CatGrowFrames)} frames — off the pellet's line, v=({Memory.ReadFloat(CodeCaves.Mailbox.CatVx):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVh):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVz):F2})/frame, run speed {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F2}");
                    }
                    break;
                case 5:                                                          // land clip started (ahead of touchdown); the cave stops momentum at paw contact and runs at clip end
                    if (_phase != Phase.Landing)
                    {
                        _phase = Phase.Landing; _phaseStart = GameClock.Now;
                        long lp = SlotAddr() + CCharacter.CharPos;
                        Log($"land clip started at ({Memory.ReadFloat(lp):F1},{Memory.ReadFloat(lp + 4):F1},{Memory.ReadFloat(lp + 8):F1}), {Memory.ReadFloat(lp + 4) - Memory.ReadFloat(CodeCaves.Mailbox.CatFloorH):F2} above the floor, motion frame {Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame):F1}");
                    }
                    break;
                case 6:                                                          // running (cave moves it); face along its direction, decide the end
                {
                    if (_phase != Phase.Running)
                    {
                        _phase = Phase.Running; _phaseStart = GameClock.Now; _pounceLogged = false; _pounceKind = 0;
                        Log($"land clip done (frame {Memory.ReadFloat(CodeCaves.Mailbox.CatPrevFrame):F1}) — moving off, floor {Memory.ReadFloat(CodeCaves.Mailbox.CatFloorH):F2}");
                    }
                    long rp = SlotAddr() + CCharacter.CharPos;
                    _x = Memory.ReadFloat(rp); _h = Memory.ReadFloat(rp + 4); _y = Memory.ReadFloat(rp + 8);
                    double rt = (GameClock.Now - _phaseStart).TotalSeconds;
                    float dist = float.MaxValue;
                    if (_target >= 0 && !Enemies.IsLive(_target))
                    {
                        int was = _target;
                        _target = PickTarget();                                   // the next nearest, if any
                        WriteTargetAim();
                        CrushGuard(_target); ApplyFlightTime();
                        Log($"target slot {was} is gone — now {(_target >= 0 ? $"slot {_target}" : "none (walking straight)")}");
                    }
                    if (_target >= 0)
                    {
                        long tp = EnemyAddresses.CharObjects.PosAddr(_target);
                        float ex = Memory.ReadFloat(tp) - _x, ey = Memory.ReadFloat(tp + 8) - _y; dist = (float)Math.Sqrt(ex * ex + ey * ey);
                    }
                    if (_target < 0)
                    {
                        if (!_sitLogged) { _sitLogged = true; Log($"no enemy within {MaxTargetDistance:F0} — sitting"); }
                        if (++_retargetTick >= 30)                                 // look again every ~0.5 s
                        {
                            _retargetTick = 0;
                            _target = PickTarget();
                            if (_target >= 0)
                            {
                                WriteTargetAim();
                                CrushGuard(_target); ApplyFlightTime();
                                _sitLogged = false; _gaitLogged = false;
                                Log($"enemy slot {_target} came within range — up and after it");
                            }
                        }
                    }
                    else if (!_gaitLogged && Memory.ReadInt(CodeCaves.Mailbox.CatBlocked) == 0)
                    {
                        _gaitLogged = true;
                        Log($"walking at {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F3}/frame, clip rate {Memory.ReadFloat(SlotAddr() + CharacterMotion.MotionSpeedOffset):F2} (town mapping)");
                    }
                    bool blocked = Memory.ReadInt(CodeCaves.Mailbox.CatBlocked) != 0;
                    if (blocked && !_blockedLogged) { _blockedLogged = true; Log("a wall stops the cat — waiting"); }
                    if (!blocked) _blockedLogged = false;
                    break;
                }
                case 11:                                                         // float-up wind-up: in place, turning, until the feet-off frame launches the leap
                    if (_pounceKind != 3) { _pounceKind = 3; _phase = Phase.TakeOff; Log($"float wind-up at enemy slot {_target} — jump at frame {FloatLaunchFrame:F0}"); }
                    break;
                case 10:                                                         // ready: in place before the jump, or held there while the target cannot be hit
                    if (!_pounceLogged) { _pounceLogged = true; _phase = Phase.TakeOff; _phaseStart = GameClock.Now; Log($"readying a pounce at enemy slot {_target} (cave compared distance {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgDist):F1} vs range {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgRange):F1})"); }
                    break;
                case 2:
                {
                    long sp = SlotAddr() + CCharacter.CharPos;                       // hold the last placed spot through the fade
                    _x = Memory.ReadFloat(sp); _h = Memory.ReadFloat(sp + 4); _y = Memory.ReadFloat(sp + 8);
                    int frames = Memory.ReadInt(CodeCaves.Mailbox.CatGrowFrames);
                    float mf = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame);
                    _scale = 1f; _pelletSlot = -1;
                    Memory.WriteInt(CodeCaves.Mailbox.CatState, 0);
                    _caveOwns = false;
                    Log($"pellet ended after {frames} frames; motion frame {_flightFrame0:F1} → {mf:F1}");
                    Enter(Phase.Fading, KeyLeap);
                    break;
                }
            }
        }

        /// <summary>Thread fallback (unpatched ISO): pin the resident copy to the new charged pellet from here on.</summary>
        internal static void Bind(long pool, int slot)
        {
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            _px = Memory.ReadFloat(pa); _ph = Memory.ReadFloat(pa + 4); _py = Memory.ReadFloat(pa + 8);
            FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
            _target = LockedTarget();
            _floor = _target >= 0 ? Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(_target) + 4)
                                  : Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
            _pool = pool; _pelletSlot = slot; _scale = 0f; _alpha = 1f; _fade = 0; _glowFade = -1;
            PlaceRootUnderHead();
            _phase = Phase.Flying; _phaseStart = GameClock.Now; _hitDone = false;
            Maintain();
            Log(
                $"cat pinned to pellet slot {slot} from ({_px:F1},{_ph:F1},{_py:F1})" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + " [thread follower]");
        }

        /// <summary>Back to resident: invisible, scale 0, fall pose looping, ready for the next charge.</summary>
        private static void Hide()
        {
            _alpha = 0f; _scale = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = false; _hitFade = false;
            RestoreGuardsNow();
            _phase = Phase.Resident; _phaseStart = GameClock.Now;
            SetKey(KeyLeap);
            Maintain();
        }

        /// <summary>True once the cat has been out for <see cref="LifetimeSeconds"/> since its bind, releasing the cave
        /// (state 0) so the caller can fade it from wherever it stands. Not applied while it still rides the pellet
        /// (states 1–3): the pellet's own life ends that flight far sooner.</summary>
        private static bool LifetimeOver()
        {
            if ((GameClock.Now - _boundAt).TotalSeconds < LifetimeSeconds) return false;
            Memory.WriteInt(CodeCaves.Mailbox.CatState, 0);
            _scale = 1f; _caveOwns = false;
            Log("20 s lifetime over — shrinking away");
            return true;
        }

        /// <summary>Fade out from the current pose without restarting the clip (the cave left the key as it stood).</summary>
        private static void FadeKeepingPose()
        {
            _key = Memory.ReadInt(SlotAddr() + CCharacter.MotionId);
            _phase = Phase.Fading; _phaseStart = GameClock.Now; _fade = 0;
        }

        // ───────────────────────────────────────── aim + targeting ─────────────────────────────────────────

        /// <summary>Yaw the cat along a horizontal direction; a pellet going straight up or down keeps her facing.</summary>
        private static void FaceAlong(float vx, float vy)
        {
            float hl = (float)Math.Sqrt(vx * vx + vy * vy);
            if (hl < 1e-3f)
            {
                float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
                vx = (float)Math.Sin(yaw); vy = (float)Math.Cos(yaw); hl = 1f;
            }
            _dirX = vx / hl; _dirY = vy / hl;
            _yaw = (float)Math.Atan2(_dirX, _dirY);
        }

        /// <summary>The leap's flight time to the target (the cave reads it at launch): the tall/large/flying set in
        /// <see cref="EnemySpecies.VerticalLeapTargets"/> and minibosses get the higher 40-frame arc, everything else
        /// the quick 27-frame one.</summary>
        private static void ApplyFlightTime()
        {
            float frames = PounceFrames; string why = "";
            if (_target >= 0)
            {
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.EnemySpeciesId));
                float scale = Memory.ReadFloat(ModelScaleOffsets.ModelBase + (long)_target * ModelScaleOffsets.ModelStride + ModelScaleOffsets.ScaleX);
                if (EnemySpecies.VerticalLeapTargets.TryGetValue(species, out string name)) { frames = PounceFramesTall; why = name; }
                else if (MiniBoss.miniBossEnemyNumbers.Contains(_target) || scale >= 1.25f) { frames = PounceFramesTall; why = $"miniboss (model scale {scale:F2})"; }
            }
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceFrames, frames);
            if (why.Length > 0) Log($"target slot {_target}: {frames:F0}-frame leap ({why})");
        }

        /// <summary>The pounce range for this target: the look's (50 for the winged cat) — but a dormant chest-mimic is
        /// always approached to 30.</summary>
        private static float RangeFor(int target)
            => target >= 0 && _look.Range > PounceRange && IsUnopenedMimic(target) ? PounceRange : _look.Range;

        /// <summary>The point the cave walks to and jumps at: the centre of the target's BIGGEST active body sphere (the
        /// Dragon's torso, a bat's body, a Titan's chest) rather than its root at the feet. Written into the mailbox every
        /// tick; CatTargetPtr points at that vector. No target → pointer 0 (the cat sits).</summary>
        private static void WriteTargetAim()
        {
            if (_target < 0) { Memory.WriteInt(CodeCaves.Mailbox.CatTargetPtr, 0); return; }
            float range = RangeFor(_target);                                     // per target, every tick (a mimic may open mid-approach)
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceRange, range);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceMaxDist, range * 2f);
            long root = EnemyAddresses.CharObjects.PosAddr(_target);
            float x = Memory.ReadFloat(root), h = Memory.ReadFloat(root + 4), y = Memory.ReadFloat(root + 8);
            if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.RenderStatus)) != 2)
            {
                // Dormant (a chest-mimic that has not opened): its script has not declared any spheres, so the sphere table
                // is whatever the slot's previous occupant left — aiming at that sent the cat wandering off.
                // The root is the chest's spot (SetMimicEvent places the box at the enemy's spawn position).
                Memory.WriteVec3(CodeCaves.Mailbox.CatAimPos, x, h, y);
                Memory.WriteInt(CodeCaves.Mailbox.CatTargetPtr, (int)Memory.ToGuest(CodeCaves.Mailbox.CatAimPos));
                if (_target != _aimLoggedFor) { _aimLoggedFor = _target; Log($"aim at enemy slot {_target}: dormant (chest) — its root at ({x:F1},{h:F1},{y:F1})"); }
                return;
            }
            long tbl = BodyCollision.SlotBase(_target);
            const int n = BodyCollision.MaxBodyParts;
            byte[] active = Memory.ReadBytesBatch(tbl + BodyCollision.ActiveArray, n * 4), radii = Memory.ReadBytesBatch(tbl + BodyCollision.RadiusArray, n * BodyCollision.BodyPartStride), centres = Memory.ReadBytesBatch(tbl + BodyCollision.CentreArray, n * BodyCollision.CentreStride);
            byte[] pct = Memory.ReadBytesBatch(tbl + BodyCollision.DamagePctArray, n * BodyCollision.DamagePctStride);           // per sphere: damage % by attacker character (_SET_BODY_COL_PARA 10+char); Xiao = +4
            byte[] spare = Memory.ReadBytesBatch(tbl + BodyCollision.SpareArray, n * BodyCollision.SpareStride);         // per sphere: the spare 5-int table — [1] = kick type admitted for Xiao at [0] % (disc-baked on Joe's face; ELF PatchCatSpherePercent)
            if (active != null && radii != null && centres != null)
            {
                // The biggest hurt sphere that can actually damage (Xiao % > 0 — Master Utan's neck/face/hands are 0 for her,
                // only the toes count); among equals, the one furthest FORWARD along the enemy's facing (Statue Dog's front
                // sphere, the Black Knight Mount's fore-body), then the root as a last resort.
                float yaw = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(_target) + CCharacter.CharRotY);
                float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
                float best = -1f, bestFwd = float.MinValue; bool anyDamaging = false;
                for (int pass = 0; pass < 2 && best < 0; pass++)                     // pass 0: damaging spheres only; pass 1: any
                {
                    for (int j = 0; j < 16; j++)
                    {
                        if (BitConverter.ToInt32(active, j * 4) == 0) continue;
                        int p = pct == null ? 100 : BitConverter.ToInt32(pct, j * 0x18 + 4);
                        if (spare != null && BitConverter.ToInt32(spare, j * 0x14 + 4) == CatKickType) p = BitConverter.ToInt32(spare, j * 0x14);   // the cat's own % on this sphere
                        if (pass == 0 && p <= 0) continue;
                        float r = BitConverter.ToSingle(radii, j * 4);
                        float cx = BitConverter.ToSingle(centres, j * 0x10), ch = BitConverter.ToSingle(centres, j * 0x10 + 4), cy = BitConverter.ToSingle(centres, j * 0x10 + 8);
                        float fwd = (cx - Memory.ReadFloat(root)) * fx + (cy - Memory.ReadFloat(root + 8)) * fz;
                        if (r > best + 0.01f || (Math.Abs(r - best) <= 0.01f && fwd > bestFwd)) { best = r; bestFwd = fwd; x = cx; h = ch; y = cy; anyDamaging = pass == 0; }
                    }
                }
                if (_target != _aimLoggedFor) { _aimLoggedFor = _target; Log($"aim at enemy slot {_target}: sphere r={best:F1} at ({x:F1},{h:F1},{y:F1}){(anyDamaging ? "" : " — NO sphere can take Xiao's damage; aiming at the biggest anyway")}"); }
            }
            Memory.WriteVec3(CodeCaves.Mailbox.CatAimPos, x, h, y);
            Memory.WriteInt  (CodeCaves.Mailbox.CatTargetPtr, (int)Memory.ToGuest(CodeCaves.Mailbox.CatAimPos));
        }

        // A chest-mimic waits as a DORMANT slot; while the cat's target is one, the cat crouches and waits.
        private static readonly HashSet<ushort> KingMimics = new() { (ushort)EnemySpecies.KingMimicDBC.Id, (ushort)EnemySpecies.KingMimicSMT.Id, (ushort)EnemySpecies.KingMimicMS.Id, (ushort)EnemySpecies.KingMimicWOF.Id, (ushort)EnemySpecies.KingMimicSW.Id, (ushort)EnemySpecies.KingMimicGoT.Id, (ushort)EnemySpecies.KingMimicDS.Id };
        private static readonly HashSet<ushort> Mimics     = new() { (ushort)EnemySpecies.MimicDBC.Id, (ushort)EnemySpecies.MimicSMT.Id, (ushort)EnemySpecies.MimicMS.Id, (ushort)EnemySpecies.MimicWOF.Id, (ushort)EnemySpecies.MimicSW.Id, (ushort)EnemySpecies.MimicGoT.Id, (ushort)EnemySpecies.MimicDS.Id };
        private static bool _holdLogged;

        /// <summary>A native chest-mimic is a DORMANT enemy slot (RenderStatus 1: its view gate is 0, so it is never promoted
        /// to 2 and never drawn — the treasure box drawn at its position is the disguise, see ChestAddresses). Opening the
        /// box sets the gate and the slot goes to 2: its script declares the hurt spheres right then, so the cat leaps at
        /// that moment and not after the "appear" clip.</summary>
        private static bool IsUnopenedMimic(int slot)
        {
            ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!KingMimics.Contains(species) && !Mimics.Contains(species)) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) != 2;   // dormant: the chest
        }

        /// <summary>A lock-on onto a different live enemy retargets the cat. Crouched (state 10) it is sent back to walking
        /// (state 6, the crouch's play-once cleared so the walk loops) so the cave closes the distance before leaping again.</summary>
        private static void RetargetToLockOn(int state)
        {
            int locked = LockedTarget();
            if (locked < 0 || locked == _target || !Enemies.IsLive(locked)) return;
            int was = _target; _target = locked;
            WriteTargetAim(); CrushGuard(_target); ApplyFlightTime();
            _gaitLogged = false; _pounceLogged = false; _sitLogged = false;
            if (state == 10)
            {
                SetMotionFlags(clear: CCharacter.MotionPlayOnce);
                Memory.WriteInt(CodeCaves.Mailbox.CatState, 6);
            }
            Log($"lock-on moved: target slot {was} → {locked}{(state == 10 ? " (leaving the crouch, walking)" : "")}");
        }

        /// <summary>Locked-on enemy first; otherwise the live enemy nearest to Xiao; −1 when none.</summary>
        private static int PickTarget()
        {
            int locked = LockedTarget();
            if (locked >= 0) return locked;
            float px = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos), py = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            int best = -1; float bestD = float.MaxValue;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - px, dy = Memory.ReadFloat(p + 8) - py, d = dx * dx + dy * dy;
                if (d < bestD && d <= MaxTargetDistance * MaxTargetDistance) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>The locked-on enemy slot, if it is still alive.</summary>
        private static int LockedTarget()
        {
            int s = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (s < 0 || s >= EnemyAddresses.FloorSlots.Count) return -1;
            return Enemies.IsLive(s) ? s : -1;
        }

        // ───────────────────────────── the hit's element, and the guard crush ──────────────────────────────

        /// <summary>The element a stamped attr bit stands for. Named in the contact log so "did the DAMAGE follow the
        /// element, or only the hit visual?" is answerable from the log alone, without decoding a hex bit.</summary>
        private static string ElementNameOf(int attr) => attr switch
        {
            0x01 => "Fire", 0x02 => "Ice", 0x04 => "Thunder", 0x08 => "Wind", 0x10 => "Holy",
            0 => "none (no element, or the selected one has no build-up level)",
            _ => "unexpected — more than one bit"
        };

        private static int _pelletDamage;
        /// <summary>The damage entry the cave plants at a contact carries the pellet's damage plus the weapon's attack
        /// (the "attack doubled" rule) and the weapon's element. Written at bind, and re-written by <see cref="Maintain"/>
        /// whenever the live element stops matching the stamped one while a cat is already flying — the attack and the
        /// element are read LIVE here, so re-calling is all that is needed; only _pelletDamage is captured, at bind.</summary>
        internal static void WriteHitStamps()
        {
            int attack = Memory.ReadShort(BattleWeaponAttack);
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;      // one pure element bit or none
            Memory.WriteInt(CodeCaves.Mailbox.CatHitDamage, Math.Max(1, _pelletDamage + attack));
            Memory.WriteInt(CodeCaves.Mailbox.CatHitAttr, (int)attr);
        }

        internal static readonly List<(int slot, int ticks, ushort[] flags)> _guardRestore = new();
        /// <summary>Guard Crush for the cat: the target's guard-frame windows are zeroed for the flight (a guarding enemy
        /// would otherwise take the hit on its guard); restored by <see cref="RetirePlanted"/> after the cat's lifetime
        /// or at once by <see cref="RestoreGuardsNow"/>.</summary>
        private static void CrushGuard(int enemy, bool again = false)
        {
            if (enemy < 0 || enemy >= EnemyAddresses.FloorSlots.Count) return;
            lock (_planted)
            {
                int have = _guardRestore.FindIndex(g => g.slot == enemy);
                if (have >= 0 && !again) return;                                     // already crushed this flight
                if (have >= 0)
                {   // again: windows registered AFTER the first crush (a chest-mimic's init label runs when it wakes) — zero
                    // them too; the snapshot keeps the first non-zero flag per window so the restore puts everything back
                    var (slot, ticks, snap) = _guardRestore[have];
                    bool more = false;
                    for (int w = 0; w < snap.Length; w++)
                    {
                        long a = EnemyAddresses.GuardWindows.FlagAddr(enemy, w);
                        ushort cur = Memory.ReadUShort(a);
                        if (cur != 0) { Memory.WriteUShort(a, 0); if (snap[w] == 0) snap[w] = cur; more = true; }
                    }
                    if (more) Log($"guard windows of enemy slot {enemy} zeroed again (registered since the first crush)");
                    return;
                }
            }
            var snap0 = new ushort[EnemyAddresses.GuardWindows.WindowCount];
            bool any = false;
            for (int w = 0; w < snap0.Length; w++)
            {
                long a = EnemyAddresses.GuardWindows.FlagAddr(enemy, w);
                snap0[w] = Memory.ReadUShort(a);
                if (snap0[w] != 0) { Memory.WriteUShort(a, 0); any = true; }
            }
            if (!any) return;
            lock (_planted) _guardRestore.Add((enemy, (int)(LifetimeSeconds * 60) + 120, snap0));
            Log($"guard windows of enemy slot {enemy} zeroed for this flight (Guard Crush)");
        }

        private const float GuardCrushRadius = 30f;      // the cat's reach while it is airborne
        private const int   GuardSweepTicks  = 45;       // how long a swept crush holds after the last sweep (~1.5 s)
        /// <summary>Guard Crush, every tick the cat can deal damage. <see cref="CrushGuard"/> covers only the chosen target and
        /// only when called, but an enemy re-registers its guard windows whenever its script runs <c>_SET_GUARD_FRAME</c>, and
        /// the cat's contact test hits whatever body sphere it touches — which need not be the target. CheckDmg (0x1D9F10)
        /// offers the attacker no "unguardable" flag: the window IS the guard, so it has to be down at the instant of contact.
        /// Reads every slot's window flags in one block and crushes those live and within <see cref="GuardCrushRadius"/> (the
        /// target always counts); <see cref="RetirePlanted"/> restores them.</summary>
        private static void CrushGuardsNearCat()
        {
            int n = EnemyAddresses.FloorSlots.Count, wc = EnemyAddresses.GuardWindows.WindowCount;
            byte[] blk = Memory.ReadBytesBatch(EnemyAddresses.GuardWindows.FlagAddr(0, 0), n * EnemyAddresses.GuardWindows.Stride);
            if (blk == null) return;
            long sl = SlotAddr();
            float cx = Memory.ReadFloat(sl + CCharacter.CharPos), cy = Memory.ReadFloat(sl + CCharacter.CharPos + 8);
            for (int slot = 0; slot < n; slot++)
            {
                var cur = new ushort[wc];
                bool any = false;
                for (int w = 0; w < wc; w++)
                {
                    cur[w] = BitConverter.ToUInt16(blk, slot * EnemyAddresses.GuardWindows.Stride + w * 2);
                    if (cur[w] != 0) any = true;
                }
                if (!any) continue;                                                  // nothing registered: nothing to crush
                if (slot != _target)
                {
                    long p = EnemyAddresses.CharObjects.PosAddr(slot);
                    float dx = Memory.ReadFloat(p) - cx, dy = Memory.ReadFloat(p + 8) - cy;
                    if (dx * dx + dy * dy > GuardCrushRadius * GuardCrushRadius) continue;
                }
                for (int w = 0; w < wc; w++) if (cur[w] != 0) Memory.WriteUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w), 0);
                lock (_planted)
                {
                    int i = _guardRestore.FindIndex(g => g.slot == slot);
                    if (i < 0)
                    {
                        _guardRestore.Add((slot, GuardSweepTicks, cur));
                        Log($"guard windows of enemy slot {slot} zeroed (in the cat's reach)");
                    }
                    else
                    {
                        var (s2, ticks, snap) = _guardRestore[i];
                        for (int w = 0; w < wc && w < snap.Length; w++) if (snap[w] == 0 && cur[w] != 0) snap[w] = cur[w];
                        _guardRestore[i] = (s2, Math.Max(ticks, GuardSweepTicks), snap);
                    }
                }
            }
        }

        private static void RestoreGuardsNow()
        {
            lock (_planted)
            {
                foreach (var (slot, _, flags) in _guardRestore)
                    for (int w = 0; w < flags.Length; w++) if (flags[w] != 0) Memory.WriteUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w), flags[w]);
                _guardRestore.Clear();
            }
        }

        // ───────────────────────────────────────────── flight ──────────────────────────────────────────────

        /// <summary>The thread-side flight state machine, used when the ISO has no pellet-catcher cave. With the cave present
        /// nearly every phase returns immediately (<c>if (_native) break;</c>) and this only mirrors what the cave drives,
        /// ending in <see cref="Maintain"/> either way.</summary>
        internal static void Step()
        {
            double t = (GameClock.Now - _phaseStart).TotalSeconds;
            if (_hitFade)                                                        // after a landed hit: keep flying/landing under the cave, fade out meanwhile
            {
                _fade++; _alpha = Math.Max(0f, 1f - _fade / (float)FadeTicks);
                if (_fade >= FadeTicks) { DisarmCave(); Hide(); return; }      // the cave stops driving the (now invisible) cat
            }
            if (_target >= 0 && !Enemies.IsLive(_target)) _target = -1;
            float tx = 0, th = 0, ty = 0;
            if (_target >= 0)
            {
                long p = EnemyAddresses.CharObjects.PosAddr(_target);
                tx = Memory.ReadFloat(p); th = Memory.ReadFloat(p + 4); ty = Memory.ReadFloat(p + 8);
            }
            switch (_phase)
            {
                case Phase.Resident:
                    break;                                                       // hidden; the cave (or Bind) wakes it
                case Phase.Flying:
                {
                    if (_native) break;                                          // the cave owns position/scale; PollCave handles the end
                    // Thread follower (unpatched ISO): the head sits on the pellet's point, the body grows in behind it.
                    if (Memory.ReadInt(PlayerShotPool.FlagAddr(_pool, _pelletSlot)) == 0)
                    {
                        _pelletSlot = -1;                                        // the pellet ended (hit, wall or lifetime)
                        Enter(Phase.Fading, KeyLeap);                            // landing / run / pounce parked while the flight is tuned
                        break;
                    }
                    long pa = PlayerShotPool.PosAddr(_pool, _pelletSlot), va = PlayerShotPool.VelAddr(_pool, _pelletSlot);
                    _px = Memory.ReadFloat(pa); _ph = Memory.ReadFloat(pa + 4); _py = Memory.ReadFloat(pa + 8);
                    FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
                    _scale = (float)Math.Min(1.0, t / GrowSeconds);
                    Memory.WriteFloat(PlayerShotPool.ScaleAddr(_pool, _pelletSlot), 1f - _scale);   // sprite only; the hitbox is untouched
                    PlaceRootUnderHead();
                    break;
                }
                case Phase.Falling:
                    break;                                                       // the cave flies it; PollCave sees the landing
                case Phase.Landing:
                    if (_native) break;                                          // the cave runs the clip and hands over to the run at its end
                    if (t >= LandSeconds) Enter(Phase.Running, KeyWalk);
                    break;
                case Phase.Running:
                {
                    if (_native) break;                                          // the cave runs it; PollCave ends the run
                    float dx = _dirX, dy = _dirY, dist = float.MaxValue;
                    if (_target >= 0)
                    {
                        dx = tx - _x; dy = ty - _y; dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist > 1e-3f) { dx /= dist; dy /= dist; _dirX = dx; _dirY = dy; }
                        _floor = th;
                    }
                    _yaw = (float)Math.Atan2(dx, dy);
                    _x += dx * RunSpeed; _y += dy * RunSpeed; _h = _floor;
                    bool go = _target >= 0 ? dist <= PounceRange : t >= StraightRunSeconds;
                    if (go) Enter(Phase.TakeOff, KeyTakeOff);
                    else if (t >= RunTimeoutSeconds) Enter(Phase.Fading, KeyStand);
                    break;
                }
                case Phase.TakeOff:
                    if (_native) break;                                          // the cave runs the take-off, the leap and the landing; PollCave only mirrors them
                    if (_target >= 0) { float dx = tx - _x, dy = ty - _y; if (dx * dx + dy * dy > 1e-3f) _yaw = (float)Math.Atan2(dx, dy); }
                    if (t >= TakeOffSeconds)
                    {
                        // Leap: reach the target (or PounceRange straight ahead) in PounceFrames with a small arc.
                        float ex = _target >= 0 ? tx : _x + _dirX * PounceRange, ey = _target >= 0 ? ty : _y + _dirY * PounceRange;
                        float dx = ex - _x, dy = ey - _y;
                        _vx = dx / PounceFrames; _vy = dy / PounceFrames; _vh = Gravity * PounceFrames * 0.5f;
                        _dirX = dx; _dirY = dy;
                        float l = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (l > 1e-3f) { _dirX /= l; _dirY /= l; _yaw = (float)Math.Atan2(_dirX, _dirY); }
                        Enter(Phase.Leaping, KeyLeap);
                    }
                    break;
                case Phase.Leaping:
                {
                    if (_native) break;                                          // cave-owned (see TakeOff)
                    _x += _vx; _y += _vy; _h += _vh; _vh -= Gravity;
                    bool near = _target >= 0 && (tx - _x) * (tx - _x) + (ty - _y) * (ty - _y) <= HitRadius * HitRadius * 0.5f;
                    if (!_hitDone && (near || (_vh < 0 && _h <= _floor + 1f))) { _hitDone = true; PlantHit(_x, _h + 3f, _y, HitRadius, Math.Max(1, _pelletDamage + Memory.ReadShort(BattleWeaponAttack)), _x, _h, _y); }
                    if (_vh < 0 && _h <= _floor) { _h = _floor; Enter(Phase.LandEnd, KeyLand); }
                    break;
                }
                case Phase.LandEnd:
                    if (_native) break;                                          // thread follower only
                    if (t >= LandSeconds) Enter(Phase.Fading, KeyStand);
                    break;
                case Phase.Fading:
                    _fade++;
                    _alpha = Math.Max(0f, 1f - _fade / (float)FadeTicks);
                    if (_fade >= FadeTicks) { Hide(); return; }
                    break;
            }
            Maintain();
        }

        /// <summary>Root = flight point − the head's rest offset (at the current growth scale) turned by the yaw
        /// (model +Z → world (sin yaw, cos yaw)), so the head stays on the pellet while the body grows behind it.</summary>
        private static void PlaceRootUnderHead()
        {
            float cy = (float)Math.Cos(_yaw), sy = (float)Math.Sin(_yaw), k = _scale * CatScale;
            _x = _px - k * (_headX * cy + _headZ * sy);
            _y = _py - k * (-_headX * sy + _headZ * cy);
            _h = _ph - k * _headH;
        }

        /// <summary>Enter a flight phase: stamp the phase clock and play its clip.</summary>
        private static void Enter(Phase p, int key)
        {
            _phase = p; _phaseStart = GameClock.Now;
            SetKey(key);
        }
    }
}
