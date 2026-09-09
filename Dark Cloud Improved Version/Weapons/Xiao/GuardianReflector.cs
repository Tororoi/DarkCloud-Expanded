using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Gear "Guardian Reflector" (roadmap PR 7; pool RE in game_data/docs/angelgear-reflector-re.md).
    /// While Xiao guards with the Angel Gear, a GIANT SLINGSHOT — Angel Gear's silhouette rising
    /// UPRIGHT at her back like wings — materializes for the whole guard hold (transparent → solid
    /// over 0.25 s, folding away over 0.5 s on release).
    ///
    /// ABSORB &amp; RE-FIRE (v4, user design 2026-09-08): a claimed enemy projectile is DISARMED
    /// (latch held — it cannot hurt anyone) but otherwise LEFT ALONE: it flies into Xiao and the
    /// engine ends it on contact, exactly as built — that contact is the ABSORB, marked by a dull
    /// white flash on her. The wings then AUTO-FIRE A FRESH SHOT of the same species from the pouch
    /// at the targeted enemy (nearest living enemy; the shot's arrival line reversed when none).
    /// Absorbed shots queue invisibly and fire one after another. This ends the losing fight
    /// against the engine's contact-kill (steering shots around her never reliably saved them) —
    /// nothing has to survive; everything is re-created at the wings via the same pure-data spawn
    /// the engine's own Set uses. Fresh shots are latched harmless for now — real reflected DAMAGE
    /// (pellet-style CollisionData with the shot's element) is the next stage.
    ///
    /// LEVERS (see the RE doc): enemy shots are CSHOT_EFFECT sub-shots in the pack at
    /// *NowShotEffect; a sub-shot spawn is pure data (Set 0x1ADD60 decompiled — phase 1 skips the
    /// muzzle exactly as Set does for muzzle-less configs; the fresh shot reuses the free sub-shot's
    /// OWN frame objects, so nothing is deep-copied). The wings are <see cref="SlingshotProp"/>.
    /// </summary>
    internal static class GuardianReflector
    {
        internal static bool Enabled = true;

        private const string Tag = "[GuardianReflector] ";

        private const int  XiaoId = 1;

        // ── the monster shot-effect pack (RE doc table) ──
        private const long NowShotEffectPtr = 0x202A35D8;
        private const int  PackSlots  = 5;
        private const int  SlotStride = 0xA160;
        private const int  SubShots   = 8;
        private const int  OffCfg     = 0x000;     // BT_SHOT_EFFECT cfg ptr (EE): +0x38 wait, +0x3C life, +0x4E fly motion
        private const int  OffDir     = 0x9F40;    // + i*0x10, vec3 — per-frame position delta (velocity)
        private const int  OffAttr2   = 0x9FC0;    // + i*2, short — Set param_6
        private const int  OffWait    = 0x9FD0;    // + i*4 — phase-1 countdown
        private const int  OffPhase   = 0x9FF0;    // + i*2 — 0 muzzle, 1 flying, 3+ impact chain
        private const int  OffActive  = 0xA000;    // + i*2 (Set writes it LAST)
        private const int  OffLife    = 0xA010;    // + i*4
        private const int  OffOwner   = 0xA050;    // + i*2, short — owner attr → CollisionData +0x58
        private const int  OffA060    = 0xA060;    // + i*2, short — Set writes 0xFFFF
        private const int  OffDamage  = 0xA070;    // + i*4
        private const int  OffA0B0    = 0xA0B0;    // + i*4 — Set: -1
        private const int  OffA0D0    = 0xA0D0;    // + i*4 — Set: -1.0f
        private const int  OffA0F0    = 0xA0F0;    // + i*4 — Set: -1
        private const int  OffA110    = 0xA110;    // + i*4 — Set: -1
        private const int  OffSndFlag = 0xA130;    // + i, byte
        private const int  OffReload  = 0xA138;    // + i, byte — latch reload value
        private const int  OffLatch   = 0xA140;    // + i, byte — held ≥1 = plants no damage
        private const int  OffLastIdx = 0xA150;    // int — Set records the spawned index
        private const int  OffCount   = 0xA14C;
        private const int  OffObj     = 0x11C0;    // + i*0x11B0 — the sub-shot's effect-CCharacter
        private const int  ObjStride  = 0x11B0;
        private const int  ObjPos     = 0x10;      // vec: [+0] x, [+4] height, [+8] y
        private const int  ObjFrame   = 0x2F0;     // motion frame (float)
        private const int  ObjFrameTb = 0x344;     // → per-motion frame table (int per 0x10)
        private const int  ObjMotSpd  = 0xC60;     // -1.0f = keyframe rate
        private const int  ObjMotFlag = 0xC64;     // Set: 4 on the flying phase
        private const int  ObjMotId   = 0xC68;     // motion id

        // ── tuning ──
        private const float ClaimRadius  = 100f;   // claim a closing shot inside this range of Xiao
        private const float PouchHeight  = 8.5f;   // pouch = above Xiao...
        private const float PouchBehind  = 8f;     // ...and slightly behind her
        private const float PropScale    = 2f;     // giant factor for the slingshot copy (4 read too big)
        private const int   FadeInTicks  = 5;      // transparency → solid over 0.25 s (guard begins)
        private const int   FadeOutTicks = 10;     // solid → transparent over 0.5 s (guard released)
        private const float ReturnBoost  = 1.6f;   // fresh shot speed = absorbed arrival speed × this
        private const int   PendingMax   = 6;
        private const byte  LatchHold    = 0x7F;
        private const int   FreshTimers  = 240;    // wait/life given to a fresh shot (4 s of flight)
        private const float AbsorbNear   = 8f;     // quiet-kill a claimed shot inside this range of her body
        private const float ClearAhead   = 18f;    // fresh shots spawn this far along the aim (outside her contact zone)
        // Fire-cycle timing (50 ms ticks): the authored draw is 10 frames (~3 ticks) — 4 keeps it
        // readable; hold at full draw; a short gap after the snap before the next volley.
        private const int   DrawTicks    = 4;
        private const int   HoldTicks    = 3;
        private const int   ShootTicks   = 4;

        // Dull white absorb flash on Xiao (half-sine, 0.25 s — see unitAmbientAnime notes).
        private const float AbsorbWhite = 90f, AbsorbFlashFrames = 15f;

        // Targeting: lock-on globals (main BSS lockOnTargetFlag/No — slot index into the enemy
        // arrays) and the aim lift — vanilla pellets fly flat at body height, not at the feet.
        private const long  LockOnFlag     = 0x202A3588;
        private const long  LockOnTargetNo = 0x202A3584;
        private const float AimLift        = 7f;

        private const int  FastTickMs = 50, IdleTickMs = 250;

        private sealed class Claim
        {
            public int    Slot, Idx;
            public float  Speed;                   // arrival speed (units/frame)
            public float  RetX, RetH, RetY;        // unit return line (back where it came from)
            public int    Damage;
            public ushort Owner, Attr2;
            public byte   SndFlag, Reload;
        }

        private static Thread _thread;
        private static readonly List<Claim> _claimed = new();   // in flight toward Xiao (latched)
        private static readonly List<Claim> _pending = new();   // absorbed, awaiting re-fire
        private static float _alpha;                            // prop opacity 0..1
        private static bool  _jingled;                          // once per appearance
        private static int   _pullTick = -1;
        private static bool  _comboLatch;                    // -1 idle; 0..PullTicks = elastic draw

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "GuardianReflector" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool inDun = Enabled && Player.InDungeonFloor() && Player.CurrentCharacterNum() == XiaoId;
                    if (inDun) sleep = FastTickMs;
                    bool armed = inDun
                              && Memory.ReadUShort(WeaponHave.BattleWeaponRecord) == Items.angelgear
                              && !Player.CheckDunIsPausedOrMenu()
                              && GuardWatch.IsGuarding();

                    long pack = inDun ? Memory.ReadInt(NowShotEffectPtr) : 0;
                    if (pack <= 0)
                    {
                        HardReset();
                        Thread.Sleep(sleep);
                        continue;
                    }
                    pack += 0x20000000;

                    float xx = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos);
                    float xh = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
                    float xy = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
                    float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);

                    // ORIENTATION TUNING: L3+R3 (edge) cycles the wings' orientation preset and
                    // respawns them — cycle in game until they stand right, then read the preset
                    // from the log so it can be pinned.
                    bool combo = (Memory.ReadUShort(Addresses.buttonInputs) & ((ushort)Button.L3 | (ushort)Button.R3))
                                 == ((ushort)Button.L3 | (ushort)Button.R3);
                    if (inDun && combo && !_comboLatch)
                    {
                        SlingshotProp.OrientPreset = (SlingshotProp.OrientPreset + 1) & 15;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"orientation preset -> {SlingshotProp.OrientPreset}");
                        if (SlingshotProp.Active) SlingshotProp.Despawn();   // respawns next armed tick
                    }
                    _comboLatch = combo;

                    // THE WINGS FOLLOW THE GUARD: up for the whole hold, folding away on release.
                    if (armed)
                    {
                        if (!SlingshotProp.Active
                            && SlingshotProp.Spawn(PropScale, PouchHeight, PouchBehind))
                        { _alpha = 0f; _jingled = false; }
                        if (SlingshotProp.Active)
                        {
                            _alpha = Math.Min(1f, _alpha + 1f / FadeInTicks);
                            if (!_jingled && _alpha >= 1f)
                            { _jingled = true; SeSeq.Play(SeSeq.ChangeJingle, 90); }
                        }
                    }
                    else if (SlingshotProp.Active)
                    {
                        _alpha = Math.Max(0f, _alpha - 1f / FadeOutTicks);
                        if (_alpha <= 0f) SlingshotProp.Despawn();
                    }
                    if (SlingshotProp.Active) SlingshotProp.Maintain(_alpha);

                    if (armed) ClaimClosingShots(pack, xx, xh, xy);
                    SustainClaims(pack, armed, xx, xh, xy);

                    // Fire cycle = the weapon's OWN keys (c04w##.cfg): 11 draw → 12 hold → 13 shoot
                    // (the fresh projectile leaves on the shoot key) → back to its guard-loop key.
                    if (armed && SlingshotProp.Active && _alpha >= 1f && (_pending.Count > 0 || _pullTick >= 0))
                    {
                        if (_pullTick < 0 && _pending.Count > 0) { SlingshotProp.SetMotion(SlingshotProp.KeyDraw); _pullTick = 0; }
                        if (_pullTick >= 0)
                        {
                            _pullTick++;
                            if (_pullTick == DrawTicks) SlingshotProp.SetMotion(SlingshotProp.KeyHold);
                            if (_pullTick == DrawTicks + HoldTicks)
                            {
                                SlingshotProp.SetMotion(SlingshotProp.KeyShoot);
                                FirePending(pack, xx, xh, xy, yaw);
                            }
                            if (_pullTick >= DrawTicks + HoldTicks + ShootTicks)
                            {
                                SlingshotProp.SetMotion(SlingshotProp.KeyGuardLoop);
                                _pullTick = -1;
                            }
                        }
                    }
                    else if (_pullTick >= 0)
                    {
                        SlingshotProp.SetMotion(SlingshotProp.KeyGuardLoop);
                        _pullTick = -1;
                    }
                    if (!armed) _pending.Clear();
                }
                catch (Exception e)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
                    HardReset();
                    sleep = 1000;
                }
                Thread.Sleep(sleep);
            }
        }

        // ───────────────────────────── absorb (claim + let it land) ─────────────────────────────

        /// <summary>Every live in-flight shot inside the guard radius and closing on Xiao is claimed:
        /// latched harmless and SNAPSHOTTED (species slot, speed, damage, owner fields, arrival
        /// line), then left to fly — the engine's contact-kill on her IS the absorb.</summary>
        private static void ClaimClosingShots(long pack, float xx, float xh, float xy)
        {
            for (int s = 0; s < PackSlots; s++)
            {
                long inst = pack + s * SlotStride;
                int count = Memory.ReadInt(inst + OffCount);
                if (count < 1 || count > SubShots) continue;
                for (int i = 0; i < count; i++)
                {
                    if (IsClaimed(s, i)) continue;
                    if (Memory.ReadUShort(inst + OffActive + i * 2) == 0) continue;
                    if (Memory.ReadUShort(inst + OffPhase + i * 2) != 1) continue;
                    long obj = inst + OffObj + i * ObjStride;
                    float dx = xx - Memory.ReadFloat(obj + ObjPos);
                    float dh = xh - Memory.ReadFloat(obj + ObjPos + 4);
                    float dy = xy - Memory.ReadFloat(obj + ObjPos + 8);
                    if (dx * dx + dh * dh + dy * dy > ClaimRadius * ClaimRadius) continue;
                    long dirA = inst + OffDir + i * 0x10;
                    float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                    if (vx * dx + vh * dh + vy * dy <= 0f) continue;

                    float speed = Math.Max(1f, (float)Math.Sqrt(vx * vx + vh * vh + vy * vy));
                    float inv = -1f / speed;
                    Memory.WriteByte(inst + OffLatch + i, LatchHold);
                    _claimed.Add(new Claim
                    {
                        Slot = s, Idx = i, Speed = speed,
                        RetX = vx * inv, RetH = vh * inv, RetY = vy * inv,
                        Damage  = Memory.ReadInt(inst + OffDamage + i * 4),
                        Owner   = Memory.ReadUShort(inst + OffOwner + i * 2),
                        Attr2   = Memory.ReadUShort(inst + OffAttr2 + i * 2),
                        SndFlag = Memory.ReadByte(inst + OffSndFlag + i),
                        Reload  = Memory.ReadByte(inst + OffReload + i),
                    });
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"claimed shot slot{s}#{i} speed={speed:F2}/frame dmg={Memory.ReadInt(inst + OffDamage + i * 4)}");
                }
            }
        }

        private static bool IsClaimed(int s, int i)
        {
            foreach (var c in _claimed) if (c.Slot == s && c.Idx == i) return true;
            return false;
        }

        /// <summary>Keep every claimed shot harmless, and make the absorb QUIET: the instant one is
        /// about to touch her (or its impact chain starts), clear its active flag ourselves — the
        /// shot simply vanishes into her, no explosion animation — then flash Xiao dull white and
        /// queue its re-fire. (One tick of travel is added to the kill range so a fast shot can't
        /// slip past between our 50 ms ticks and detonate on her.)</summary>
        private static void SustainClaims(long pack, bool armed, float xx, float xh, float xy)
        {
            for (int q = _claimed.Count - 1; q >= 0; q--)
            {
                var c = _claimed[q];
                long inst = pack + c.Slot * SlotStride;
                bool gone = Memory.ReadUShort(inst + OffActive + c.Idx * 2) == 0;
                if (!gone)
                {
                    long obj = inst + OffObj + c.Idx * ObjStride;
                    float dx = xx - Memory.ReadFloat(obj + ObjPos);
                    float dh = xh - Memory.ReadFloat(obj + ObjPos + 4);
                    float dy = xy - Memory.ReadFloat(obj + ObjPos + 8);
                    float kill = AbsorbNear + c.Speed * 3f;
                    if (dx * dx + dh * dh + dy * dy < kill * kill
                        || Memory.ReadUShort(inst + OffPhase + c.Idx * 2) > 1)
                    {
                        Memory.WriteUShort(inst + OffActive + c.Idx * 2, 0);   // quiet vanish
                        gone = true;
                    }
                }
                if (gone)
                {
                    _claimed.RemoveAt(q);
                    if (armed && _pending.Count < PendingMax)
                    {
                        _pending.Add(c);
                        Player.FlashActiveCharacter(AbsorbWhite, AbsorbWhite, AbsorbWhite, AbsorbFlashFrames, 1);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"absorbed slot{c.Slot}#{c.Idx} (pending {_pending.Count})");
                    }
                    continue;
                }
                Memory.WriteByte(inst + OffLatch + c.Idx, LatchHold);   // harmless to the end
            }
        }

        // ──────────────────────────────── re-fire from the wings ────────────────────────────────

        /// <summary>Spawn a FRESH shot of the absorbed species from the pouch at the targeted enemy —
        /// the same pure-data spawn Set performs for muzzle-less shots (phase 1 direct), reusing the
        /// free sub-shot's own frame objects. Latched harmless until the damage stage lands.</summary>
        private static void FirePending(long pack, float xx, float xh, float xy, float yaw)
        {
            if (_pending.Count == 0) return;
            var c = _pending[0];
            long inst = pack + c.Slot * SlotStride;
            int count = Memory.ReadInt(inst + OffCount);
            if (count < 1 || count > SubShots) { _pending.RemoveAt(0); return; }

            int j = -1;                                             // a free sub-shot to inhabit
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return;                                      // all busy — retry next tick

            long cfg = Memory.ReadInt(inst + OffCfg);
            if (cfg <= 0) { _pending.RemoveAt(0); return; }
            cfg += 0x20000000;
            long obj  = inst + OffObj + j * ObjStride;
            long dirA = inst + OffDir + j * 0x10;

            // The pouch, in world space (rides her position + facing like the prop does).
            float fx = (float)Math.Sin(yaw), fy = (float)Math.Cos(yaw);
            float poX = xx - fx * PouchBehind, poH = xh + PouchHeight, poY = xy - fy * PouchBehind;

            // Aim: the enemy Xiao is LOCKED ONTO when there is one, else the nearest living enemy —
            // at pellet height above its feet (her vanilla shots fly flat at body height, not into
            // the ground) — else the absorbed arrival line reversed.
            float ax = c.RetX, ah = c.RetH, ay = c.RetY;
            if (PickTarget(poX, poH, poY, out float ex, out float eh, out float ey))
            {
                ax = ex - poX; ah = (eh + AimLift) - poH; ay = ey - poY;
                float al = (float)Math.Sqrt(ax * ax + ah * ah + ay * ay);
                if (al < 1f) { ax = c.RetX; ah = c.RetH; ay = c.RetY; }
                else { ax /= al; ah /= al; ay /= al; }
            }
            float v = c.Speed * ReturnBoost;

            // Spawn CLEAR of her contact zone — a fresh shot born at the pouch dies on her
            // immediately (the same contact-kill the absorb uses), so it leaves from a point
            // down the aim line instead, past her body.
            float spX = poX + ax * ClearAhead, spH = poH + ah * ClearAhead, spY = poY + ay * ClearAhead;

            // ── the Set replica (fields in Set's own order; active LAST) ──
            int flyMot = (short)Memory.ReadUShort(cfg + 0x4E);
            long ftab = Memory.ReadInt(obj + ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + flyMot * 0x10) : 1;
            Memory.WriteUShort(inst + OffPhase + j * 2, 1);          // flying, no muzzle
            Memory.WriteFloat (obj + ObjPos,     spX);
            Memory.WriteFloat (obj + ObjPos + 4, spH);
            Memory.WriteFloat (obj + ObjPos + 8, spY);
            Memory.WriteFloat (obj + ObjPos + 12, 1f);
            Memory.WriteInt   (obj + ObjMotId,  flyMot);
            Memory.WriteInt   (obj + ObjMotFlag, 4);
            Memory.WriteFloat (obj + ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ObjFrame,  startFrame);
            WriteVec(dirA, ax * v, ah * v, ay * v);
            Memory.WriteInt   (inst + OffWait + j * 4, FreshTimers);
            Memory.WriteInt   (inst + OffLife + j * 4, FreshTimers);
            Memory.WriteInt   (inst + OffDamage + j * 4, c.Damage);
            Memory.WriteUShort(inst + OffOwner + j * 2, c.Owner);
            Memory.WriteUShort(inst + OffAttr2 + j * 2, c.Attr2);
            Memory.WriteUShort(inst + OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + OffA110 + j * 4, -1);
            Memory.WriteByte  (inst + OffSndFlag + j, c.SndFlag);
            Memory.WriteByte  (inst + OffReload + j, c.Reload);
            Memory.WriteByte  (inst + OffLatch + j, LatchHold);      // harmless until the damage stage
            Memory.WriteInt   (inst + OffLastIdx, j);
            FaceAlong(obj, ax, ah, ay);
            Memory.WriteUShort(inst + OffActive + j * 2, 1);         // live — engine steps it from here

            _pending.RemoveAt(0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"re-fired slot{c.Slot}#{j} at " + (ah == c.RetH && ax == c.RetX ? "return line" : "nearest enemy"));
        }

        /// <summary>Nearest living enemy's live position (the STB-visible CCharacter array).</summary>
        private static bool PickTarget(float px, float ph, float py, out float ex, out float eh, out float ey)
        {
            ex = eh = ey = 0f;
            // The lock-on target first (same slot index space as the enemy arrays).
            int locked = Memory.ReadInt(LockOnFlag) != 0 ? Memory.ReadInt(LockOnTargetNo) : -1;
            if (locked >= 0 && locked < EnemyAddresses.FloorSlots.Count
                && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(locked, EnemySlotOffsets.Hp)) > 0)
            {
                long lp = EnemyAddresses.CharObjects.PosAddr(locked);
                ex = Memory.ReadFloat(lp); eh = Memory.ReadFloat(lp + 4); ey = Memory.ReadFloat(lp + 8);
                if (ex != 0f || eh != 0f || ey != 0f) return true;
            }
            float best = float.MaxValue;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float cx = Memory.ReadFloat(p), ch = Memory.ReadFloat(p + 4), cy = Memory.ReadFloat(p + 8);
                if (cx == 0f && ch == 0f && cy == 0f) continue;
                float d = (cx - px) * (cx - px) + (ch - ph) * (ch - ph) + (cy - py) * (cy - py);
                if (d < best) { best = d; ex = cx; eh = ch; ey = cy; }
            }
            return best < float.MaxValue;
        }

        private static void HardReset()
        {
            if (SlingshotProp.Active) SlingshotProp.Despawn();
            _claimed.Clear();
            _pending.Clear();
            _alpha = 0f;
            _pullTick = -1;
        }

        private static void WriteVec(long addr, float a, float b, float c)
        {
            Memory.WriteFloat(addr, a);
            Memory.WriteFloat(addr + 4, b);
            Memory.WriteFloat(addr + 8, c);
        }

        /// <summary>Point the sub-shot's model along its motion (the engine orients only at spawn).
        /// SetTransMatrix (0x128560) is pure data — copy the matrix to frame+0x1D0, zero the world
        /// cache +0x240 — so we rebuild the same look-at basis (row Z = flight direction, Y kept
        /// upright) and write it ourselves. Translation row is left alone.</summary>
        private static void FaceAlong(long obj, float dx, float dh, float dy)
        {
            float len = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy);
            if (len < 1e-4f) return;
            uint frame = (uint)Memory.ReadInt(obj + CCharacter.CharModel) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(frame)) return;
            float zx = dx / len, zh = dh / len, zy = dy / len;          // Z = direction of travel
            float xx, xh, xy;                                            // X = up × Z (up = +height)
            if (Math.Abs(zh) > 0.99f) { xx = 1f; xh = 0f; xy = 0f; }     // near-vertical: any horizontal X
            else
            {
                xx = zy; xh = 0f; xy = -zx;                              // (0,1,0) × (zx,zh,zy)
                float xl = (float)Math.Sqrt(xx * xx + xy * xy);
                xx /= xl; xy /= xl;
            }
            float yx = zh * xy - zy * xh, yh = zy * xx - zx * xy, yy = zx * xh - zh * xx;   // Y = Z × X
            long f = Memory.ToMmu(frame);
            var m = new byte[0x30];
            BitConverter.GetBytes(xx).CopyTo(m, 0x00); BitConverter.GetBytes(xh).CopyTo(m, 0x04); BitConverter.GetBytes(xy).CopyTo(m, 0x08);
            BitConverter.GetBytes(yx).CopyTo(m, 0x10); BitConverter.GetBytes(yh).CopyTo(m, 0x14); BitConverter.GetBytes(yy).CopyTo(m, 0x18);
            BitConverter.GetBytes(zx).CopyTo(m, 0x20); BitConverter.GetBytes(zh).CopyTo(m, 0x24); BitConverter.GetBytes(zy).CopyTo(m, 0x28);
            Memory.WriteBytesBatch(f + CFrameVu1.LocalMatrix, m);
            Memory.WriteInt(f + CFrameVu1.WorldCacheA, 0);
        }
    }
}
