using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Gear "Guardian Reflector" (roadmap PR 7; pool RE in game_data/docs/angelgear-reflector-re.md).
    /// While Xiao guards with the Angel Gear, a GIANT COPY of her slingshot stands IN FRONT of her
    /// for the whole guard hold (transparent → solid over 0.25 s, folding away over 0.5 s on
    /// release). It ORBITS her to face, in priority: the nearest enemy shot closing on her (even
    /// when an enemy is nearer), else the nearest enemy, else straight ahead — and, while it fires,
    /// the enemy it is about to shoot.
    ///
    /// INTERCEPT &amp; RE-FIRE (user design 2026-09-08): every closing shot is claimed (latch held —
    /// it cannot hurt anyone). The one the slingshot faces is homed gently into the POUCH and
    /// caught there (quiet vanish); any other reaches her body and is absorbed there instead (dull
    /// white flash). Each caught shot is then RE-CREATED from the pouch — the weapon copy plays its
    /// own draw/hold/shoot keys — at the NEAREST living enemy (the arrival line reversed when none),
    /// via the same pure-data spawn the engine's own Set uses. Fresh shots are latched harmless for
    /// now — real reflected DAMAGE (pellet-style CollisionData with the shot's element) is a later
    /// stage, as are the melee shield (aggro like the Mirage decoy, one hit = 4 s cooldown).
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
        private const int  OffA060    = 0xA060;    // + i*2, short — Set writes 0xFFFF; SetUserID2 (0x1AE400) then stamps the FIRING ENEMY SLOT
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
        private const float PouchHeight  = 7.5f;   // copy root height above her feet (8.5 read slightly high at 2x)
        private const float PropAhead    = 12f;    // copy root this far out from her, along the orbit bearing
        private const float CaptureRadius = 7f;    // a faced shot this close to the pouch is caught (+ 2 ticks of travel)
        private const float HomingRange  = 40f;    // faced shot is homed into the pouch from this range
        private const float HomingGain   = 0.35f;  // per-tick blend of its direction toward the pouch
        private const int   FireWaitMax  = 20;     // ticks a caught shot waits for the sky to clear before firing anyway
        // SHIELD RING (user design 2026-09-09): enemies keep Xiao's CENTER as their target, but each one
        // "sees" her at the point on a ring of the slingshot's radius along its own approach line — so it
        // walks straight at her and stops/attacks on reaching the slingshot. Pure data: the Mirage redirect
        // caves make _GET_POSITION/_GET_DISTANCE read a per-slot POINTER (CodeCaves.PtrTable); the ring
        // points each live enemy's pointer at its own ring position. Enemies already inside the ring see her
        // real position (they are past the shield). Released → every pointer back to the live player.
        private const float RingRadius   = PropAhead + 2f;   // the slingshot's reach plus its own thickness
        private const int   RingSlots    = 20;               // per-slot entries managed (Mirage manages the same 20)
        // MELEE HIT ON THE SLINGSHOT (user 2026-09-09): enemy swings are CCollisionData spheres in the
        // NowColData pool, planted by CMonstorUnit::CheckDmg (0x1D9F10) only during an attack's damage
        // window (mask +0x48 bit 1 = hurts the player, owner +0x58 = slot*5+200). The player is hit
        // when CheckHitUser (0x1B5920) finds an open entry (+0x70 == +0x74) whose horizontal distance
        // ≤ its radius and whose vertical band overlaps hers. A guarded hit (BtCheckDamageProc, dun
        // 0x1DBAFD0) then CONSUMES the entry, plants a hit-mark (MyHitPointMark @0x1EC4940, 16 × 0x20:
        // pos vec4, +0x10 life 0x10, +0x14 timer 0, +0x18 active 1 — entry 0 for guards) and plays
        // SndSePlay(0xA2), the guard clink. The slingshot does the same at frame rate against its own
        // volume, then dispels and cools down.
        private const long   NowColDataPtr = 0x202A35E0;
        private const int    ColEntries = 96, ColStride = 0xA0, ColActiveOff = 0x3C00;
        private const int    ColRadius = 0x3C, ColMask = 0x48, ColOwner = 0x58, ColGateA = 0x70, ColGateB = 0x74;
        // Set's 2nd arg → entry +0x38: 0 for MELEE/contact planters (CheckDmg, player swings, bombs, status
        // powders) and 1.0f for PROJECTILE/effect planters (CSHOT_EFFECT impacts, MACHINGUN, FIREBAR, thrown
        // items) — the discriminator that keeps a shot detonating near the slingshot from reading as a swing.
        private const int    ColClass = 0x38;
        private const int    ColColIdx = 0x60;
        // RULE (user 2026-09-09): melee is melee — any melee-class player-hurting sphere on the slingshot
        // dispels it. Shots are shots — a pool shot whose BODY reaches the slingshot is caught there and
        // re-fired (see CatchAtProp), and a shot's impact sphere landing on it is swallowed, never a hit.
        private const uint   HurtsPlayerMask = 1;
        private const long   HitMarkPool = 0x21EC4940;
        private const int    HitMarkLife = 0x10;
        private const ushort GuardClinkSe = 0xA2;
        private const float  PropHitRadius = 6f, PropHitHeight = 14f, PropHitBelow = 2f;   // the copy's volume about its root
        private const int    HitFadeTicks = 3;             // dispel: solid → gone in 0.15 s
        private const double CooldownSeconds = 4.0;
        private const int    HitWatchMs = 16;
        // PHYSICAL BLOCK (user 2026-09-09: a collision circle, not a per-frame clamp). CMonstorUnit::MoveCheck2
        // (0x1DCDD0, called from Step for every enemy) zeroes an enemy's scripted movement (dir +0x1E430 /
        // speed +0x1E450) when its next position is within (its move radius +0x1E414 + 6.0) of the player
        // and it is heading toward her — the ONLY enemy-vs-player body block in the engine (MoveChecMonster
        // is enemy-vs-enemy; MoveCheck is the player-vs-enemy side). The 6.0 is a per-site immediate:
        //   0x1DCFD0  lui  $v1,0x40c0     →  lui  $v1,HI(Mailbox.ShieldBlockAddend)
        //   0x1DCFD4  mtc1 $v1,$f1        →  lwc1 $f1,LO(Mailbox.ShieldBlockAddend)($v1)
        // Two words, applied ONCE while cold (menu/town — writing hot EE code crashes the recompiler);
        // from then on the block distance is a DATA word: 6.0 vanilla, RingRadius while the shield is up.
        private const long   BlockPatchAddr = 0x201DCFD0;
        private static readonly uint[] BlockPristine = { 0x3C0340C0u, 0x44830800u };
        private static readonly uint[] BlockPatched  = { 0x3C030000u | (uint)((CodeCaves.Mailbox.ShieldBlockAddend - 0x20000000) >> 16),
                                                          0xC4610000u | (uint)((CodeCaves.Mailbox.ShieldBlockAddend - 0x20000000) & 0xFFFF) };
        private const float  VanillaBlockAddend = 6f;
        // ENGINE-SIDE CATCH (user 2026-09-09: no per-tick collision checks). checkCollision (0x1AB740) is every
        // shot's hit-the-player test; its player-position load (`lui $v0,0x1ea; addiu $a1,$v0,0x1d30` @0x1AB828,
        // $v0 dead after) becomes a POINTER read of Mailbox.ShotHitTarget. Solid shield → pointer = the copy's
        // shot-target node (pouch lowered by her 14-unit body lift, engine-refreshed every draw) → shots hit the POUCH natively at frame
        // rate; claimed (latched) shots plant nothing there and simply end — their death near the pouch is
        // the catch. Shield down → pointer = the player global (vanilla).
        private const long   ShotPatchAddr = 0x201AB828;
        private static readonly uint[] ShotPristine = { 0x3C0201EAu, 0x24451D30u };
        private static readonly uint[] ShotPatched  = { 0x3C050000u | (uint)((CodeCaves.Mailbox.ShotHitTarget - 0x20000000) >> 16),
                                                         0x8CA50000u | (uint)((CodeCaves.Mailbox.ShotHitTarget - 0x20000000) & 0xFFFF) };
        private const float  EngineCatchNear = 20f;      // a claimed shot that died within this of the pouch was caught there
        private const float PullLength   = 2.5f;   // pouch draw travel, weapon units at x1 (authored 4.2)
        private const float PropScale    = 2f;     // giant factor for the slingshot copy (4 read too big)
        private const int   FadeInTicks  = 5;      // transparency → solid over 0.25 s (guard begins)
        private const int   FadeOutTicks = 10;     // solid → transparent over 0.5 s (guard released)
        private const float ReturnBoost  = 1.6f;   // fresh shot speed = absorbed arrival speed × this
        private const int   PendingMax   = 6;
        private const byte  LatchHold    = 0x7F;
        private const int   FreshTimers  = 240;    // wait/life given to a fresh shot (4 s of flight)
        private const float AbsorbNear   = 8f;     // quiet-kill a claimed shot inside this range of her body
        private const float ClearMin     = 4f;     // fresh shots leave at least this far down the aim from the pouch
        private const float PastHer      = 10f;    // ...and, when the aim runs back through her, this far past her body
        // Fire-cycle timing (50 ms ticks): the authored draw is 10 frames (~3 ticks) — 4 keeps it
        // readable; hold at full draw; a short gap after the snap before the next volley.
        private const int   DrawTicks    = 5;      // 10 frames at KEY rate 0.7 ≈ 4.8 ticks: let the draw complete
        private const int   HoldTicks    = 3;
        private const int   ShootTicks   = 4;

        // Dull white absorb flash on Xiao (half-sine, 0.25 s — see unitAmbientAnime notes).
        private const float AbsorbWhite = 90f, AbsorbFlashFrames = 15f;

        // Targeting: nearest living enemy (lock-on selection deliberately ignored), aimed EXACTLY where
        // Xiao's own pellets go — the enemy's lock-on frame world position (FloorSlots LockOnPoint,
        // engine-refreshed every draw; setTargetCursor copies it to the aim global 0x1DC4500), or its
        // origin raised 8 when the species set no lock-on frame.

        private const int  FastTickMs = 50, IdleTickMs = 250;

        private sealed class Claim
        {
            public int    Slot, Idx;
            public float  Speed;                   // arrival speed (units/frame)
            public float  RetX, RetH, RetY;        // unit return line (back where it came from)
            public int    Damage;
            public ushort Owner, Attr2;
            public byte   SndFlag, Reload;
            public float  LastX, LastH, LastY;         // where it was last seen (to judge an engine-side death)
        }

        private static Thread _thread;
        private static readonly List<Claim> _claimed = new();   // in flight toward Xiao (latched)
        private static readonly List<Claim> _pending = new();   // absorbed, awaiting re-fire
        private static float _alpha;                            // prop opacity 0..1
        private static bool  _jingled;                          // once per appearance
        private static int   _pullTick = -1;                    // -1 idle; else ticks into the fire cycle
        private static int   _pendingWait;                      // ticks the head of the queue has waited to fire
        private static bool  _ringWarned, _blockArmed, _blockWarned, _shotArmed, _shotWarned, _shotRedirected;
        private static Thread _hitThread;
        private static volatile bool _hitFlag;                  // set by the hit watch, consumed by the loop
        private static bool  _dispelling;                       // hit → fast fade-out in progress
        private static DateTime _cooldownUntil;                 // no respawn before this
        /// <summary>The shield ring owns the AI redirect pointer table (Mirage's table writer stands down).</summary>
        internal static bool RingActive { get; private set; }
        private static bool  _comboLatch;

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "GuardianReflector" };
            _thread.Start();
            if (_hitThread == null || !_hitThread.IsAlive)
            { _hitThread = new Thread(HitWatch) { IsBackground = true, Name = "SlingshotHitWatch" }; _hitThread.Start(); }
        }

        private static void Loop()
        {
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool inDun = Enabled && Player.InDungeonFloor() && Player.CurrentCharacterNum() == XiaoId;
                    if (!_blockArmed && !Player.InDungeonFloor()) ArmBlockPatch();
                    if (!_shotArmed && !Player.InDungeonFloor()) ArmShotPatch();
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

                    // A MELEE HIT dispels the shield: fast fade-out, then a cooldown before it can return.
                    if (_hitFlag)
                    {
                        _hitFlag = false;
                        if (SlingshotProp.Active && !_dispelling)
                        {
                            _dispelling = true;
                            _cooldownUntil = DateTime.UtcNow.AddSeconds(CooldownSeconds);
                            SeSeq.Play(SeSeq.WeaponBreak, 60);                 // the game's own weapon-break sound (WHP → 0)
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slingshot struck — dispelled (weapon-break SE), back in {CooldownSeconds:0.#} s");
                        }
                    }

                    // THE SHIELD FOLLOWS THE GUARD: up for the whole hold, folding away on release.
                    if (_dispelling)
                    {
                        _alpha = Math.Max(0f, _alpha - 1f / HitFadeTicks);
                        if (_alpha <= 0f || !SlingshotProp.Active) { SlingshotProp.Despawn(); _dispelling = false; }
                    }
                    else if (armed)
                    {
                        if (!SlingshotProp.Active && DateTime.UtcNow >= _cooldownUntil
                            && SlingshotProp.Spawn(PropScale, PouchHeight, PropAhead, PullLength))
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
                    // Shots are only intercepted while the SHIELD is up (not while it is broken / cooling
                    // down / folding): with no slingshot they reach her exactly as vanilla.
                    bool shieldUp = armed && SlingshotProp.Active && !_dispelling;
                    if (shieldUp) ClaimClosingShots(pack, xx, xh, xy);

                    // ORBIT: the copy circles her to face the nearest closing shot, else the nearest
                    // enemy, else straight ahead — and its fire target during a cycle. This tick only
                    // sets the wanted bearing; the prop's own frame-rate thread swings to it smoothly.
                    Claim faced = null;
                    if (SlingshotProp.Active)
                    {
                        if (ChooseBearing(pack, xx, xh, xy, yaw, out float want, out faced))
                            SlingshotProp.OrbitTarget = Wrap(want - yaw);
                        SlingshotProp.Maintain(_alpha);
                    }
                    GetPouch(xx, xh, xy, yaw, out float px, out float ph, out float py);
                    RedirectShots(shieldUp && _alpha >= 1f);
                    if (SlingshotProp.Active) UpdateRing(xx, xh, xy); else ReleaseRing();

                    SustainClaims(pack, shieldUp, xx, xh, xy, px, ph, py, faced);

                    // Fire cycle = the weapon's OWN keys (c04w##.cfg): 11 draw → 12 hold → 13 shoot
                    // (the fresh projectile leaves on the shoot key) → back to the copy's idle hold (KEY 14).
                    // It begins once nothing else is inbound (or the queue has waited FireWaitMax
                    // ticks), giving the copy time to turn onto its target first.
                    if (armed && SlingshotProp.Active && _alpha >= 1f && (_pending.Count > 0 || _pullTick >= 0))
                    {
                        if (_pullTick < 0)
                        {
                            _pendingWait++;
                            if (_claimed.Count == 0 || _pendingWait > FireWaitMax)
                            { SlingshotProp.SetMotion(SlingshotProp.KeyDraw); _pullTick = 0; _pendingWait = 0; }
                        }
                        if (_pullTick >= 0)
                        {
                            _pullTick++;
                            if (_pullTick == DrawTicks) SlingshotProp.SetMotion(SlingshotProp.KeyHold);
                            if (_pullTick == DrawTicks + HoldTicks)
                            {
                                SlingshotProp.SetMotion(SlingshotProp.KeyShoot);
                                FirePending(pack, xx, xh, xy, px, ph, py);
                            }
                            if (_pullTick >= DrawTicks + HoldTicks + ShootTicks)
                            {
                                SlingshotProp.SetMotion(SlingshotProp.KeyIdle);
                                _pullTick = -1;
                            }
                        }
                    }
                    else if (_pullTick >= 0)
                    {
                        SlingshotProp.SetMotion(SlingshotProp.KeyIdle);
                        _pullTick = -1;
                    }
                    if (_pending.Count == 0) _pendingWait = 0;
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

        /// <summary>Keep every claimed shot harmless and end each one QUIETLY ourselves (active
        /// flag cleared — no impact animation): the shot the copy is FACING is homed gently into the
        /// pouch and caught there; any other vanishes into her body with the dull white flash. Either
        /// way it queues for re-fire. (Two/three ticks of travel pad the catch/kill ranges so a fast
        /// shot can't slip through between 50 ms ticks and detonate on her.)</summary>
        private static void SustainClaims(long pack, bool shieldUp, float xx, float xh, float xy,
                                          float px, float ph, float py, Claim faced)
        {
            if (!shieldUp)
            {
                // Shield down (broken, cooling, folding, or guard released): every claimed shot goes
                // LIVE again at once (latch → 0; left alone it would stay harmless for its 127-frame
                // countdown) and nothing waits to be fired.
                if (_claimed.Count > 0 || _pending.Count > 0)
                {
                    foreach (var c in _claimed)
                        Memory.WriteByte(pack + c.Slot * SlotStride + OffLatch + c.Idx, 0);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"shield down — released {_claimed.Count} claimed, dropped {_pending.Count} pending");
                    _claimed.Clear(); _pending.Clear();
                }
                return;
            }
            bool armed = true, solid = SlingshotProp.Active && _alpha >= 1f;
            for (int q = _claimed.Count - 1; q >= 0; q--)
            {
                var c = _claimed[q];
                long inst = pack + c.Slot * SlotStride;
                bool gone = Memory.ReadUShort(inst + OffActive + c.Idx * 2) == 0, caught = false, byEngine = gone;
                if (!gone)
                {
                    long obj = inst + OffObj + c.Idx * ObjStride;
                    float sx = Memory.ReadFloat(obj + ObjPos), sh = Memory.ReadFloat(obj + ObjPos + 4), sy = Memory.ReadFloat(obj + ObjPos + 8);
                    c.LastX = sx; c.LastH = sh; c.LastY = sy;
                    float bx = xx - sx, bh = xh - sh, by = xy - sy;          // shot → her body
                    float qx = px - sx, qh = ph - sh, qy = py - sy;          // shot → the pouch
                    float dq = qx * qx + qh * qh + qy * qy;
                    float catchR = CaptureRadius + c.Speed * 2f, kill = AbsorbNear + c.Speed * 3f;
                    if (solid && dq < catchR * catchR)
                    {
                        Memory.WriteUShort(inst + OffActive + c.Idx * 2, 0);   // into the pouch
                        gone = true; caught = true;
                    }
                    else if (bx * bx + bh * bh + by * by < kill * kill
                             || Memory.ReadUShort(inst + OffPhase + c.Idx * 2) > 1)
                    {
                        Memory.WriteUShort(inst + OffActive + c.Idx * 2, 0);   // quiet vanish into her
                        gone = true;
                    }
                    else if (solid && c == faced && dq < HomingRange * HomingRange)
                    {
                        long dirA = inst + OffDir + c.Idx * 0x10;
                        float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                        float vl = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy), ql = (float)Math.Sqrt(dq);
                        if (vl > 1e-3f && ql > 1e-3f)
                        {
                            float nx = vx / vl * (1f - HomingGain) + qx / ql * HomingGain;
                            float nh = vh / vl * (1f - HomingGain) + qh / ql * HomingGain;
                            float ny = vy / vl * (1f - HomingGain) + qy / ql * HomingGain;
                            float nl = (float)Math.Sqrt(nx * nx + nh * nh + ny * ny);
                            if (nl > 1e-3f)
                            {
                                WriteVec(dirA, nx / nl * vl, nh / nl * vl, ny / nl * vl);   // speed kept
                                FaceAlong(obj, nx, nh, ny);
                            }
                        }
                    }
                }
                if (gone)
                {
                    _claimed.RemoveAt(q);
                    string how = caught ? "caught in the pouch" : "absorbed into her";
                    if (byEngine)
                    {
                        // The engine ended it (it hit "the player" — i.e. the POUCH while redirected — a wall,
                        // or expired): judge by where we last saw it.
                        float qx = px - c.LastX, qh = ph - c.LastH, qy = py - c.LastY;
                        float ex = xx - c.LastX, eh = xh - c.LastH, ey = xy - c.LastY;
                        if (qx * qx + qh * qh + qy * qy < EngineCatchNear * EngineCatchNear) { caught = true; how = "caught at the pouch (engine)"; }
                        else if (ex * ex + eh * eh + ey * ey < (AbsorbNear + c.Speed * 3f) * (AbsorbNear + c.Speed * 3f)) how = "ended on her";
                        else
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lost slot{c.Slot}#{c.Idx} (wall/expired) — not re-fired");
                            continue;
                        }
                    }
                    if (armed && _pending.Count < PendingMax)
                    {
                        _pending.Add(c);
                        if (!caught) Player.FlashActiveCharacter(AbsorbWhite, AbsorbWhite, AbsorbWhite, AbsorbFlashFrames, 1);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{how}: slot{c.Slot}#{c.Idx} (pending {_pending.Count})");
                    }
                    continue;
                }
                Memory.WriteByte(inst + OffLatch + c.Idx, LatchHold);   // harmless to the end
            }
        }

        /// <summary>Where the copy should face — a world bearing in her convention (atan2(dx, dy),
        /// forward = (sin, cos)). Fire target while a cycle runs or a caught shot waits with a clear
        /// sky; else the nearest closing shot (also returned, for homing); else the nearest enemy;
        /// else her own facing. False = nothing to face, hold the current bearing.</summary>
        private static bool ChooseBearing(long pack, float xx, float xh, float xy, float yaw, out float want, out Claim faced)
        {
            want = yaw; faced = null;
            if (_pullTick >= 0 || (_pending.Count > 0 && _claimed.Count == 0))
            {
                if (PickTarget(xx, xh, xy, out float ex, out _, out float ey))
                { want = (float)Math.Atan2(ex - xx, ey - xy); return true; }
                return false;
            }
            float best = float.MaxValue;
            foreach (var c in _claimed)
            {
                long obj = pack + c.Slot * SlotStride + OffObj + c.Idx * ObjStride;
                float sx = Memory.ReadFloat(obj + ObjPos), sy = Memory.ReadFloat(obj + ObjPos + 8);
                float d = (sx - xx) * (sx - xx) + (sy - xy) * (sy - xy);
                if (d < best) { best = d; faced = c; want = (float)Math.Atan2(sx - xx, sy - xy); }
            }
            if (faced != null) return true;
            if (PickTarget(xx, xh, xy, out float nx, out _, out float ny))
            { want = (float)Math.Atan2(nx - xx, ny - xy); return true; }
            return true;                                                // nothing around: straight ahead
        }

        private static float Wrap(float a)
        {
            const float twoPi = 2f * (float)Math.PI;
            while (a >  (float)Math.PI) a -= twoPi;
            while (a <= -(float)Math.PI) a += twoPi;
            return a;
        }

        /// <summary>The pouch in world space: the copy's pouch bone once it has been drawn, else the
        /// analytic copy root (her position + the orbit offset).</summary>
        private static void GetPouch(float xx, float xh, float xy, float yaw, out float px, out float ph, out float py)
        {
            if (SlingshotProp.Active && SlingshotProp.PouchWorld(out px, out ph, out py)) return;
            float b = yaw + SlingshotProp.Orbit;
            px = xx + (float)Math.Sin(b) * PropAhead; ph = xh + PouchHeight; py = xy + (float)Math.Cos(b) * PropAhead;
        }

        // ─────────────────────────────── re-fire from the pouch ────────────────────────────────

        /// <summary>Spawn a FRESH shot of the absorbed species from the pouch at the targeted enemy —
        /// the same pure-data spawn Set performs for muzzle-less shots (phase 1 direct), reusing the
        /// free sub-shot's own frame objects. Latched harmless until the damage stage lands.</summary>
        private static void FirePending(long pack, float xx, float xh, float xy, float px, float ph, float py)
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

            // Leave from the fork's MUZZLE (the copy's eff30 — where her own pellets spawn), else the pouch.
            float poX = px, poH = ph, poY = py;
            if (SlingshotProp.MuzzleWorld(out float mx, out float mh, out float my)) { poX = mx; poH = mh; poY = my; }

            // Aim: the nearest living enemy (lock-on ignored) —
            // at pellet height above its feet (her vanilla shots fly flat at body height, not into
            // the ground) — else the absorbed arrival line reversed.
            float ax = c.RetX, ah = c.RetH, ay = c.RetY;
            if (PickTarget(poX, poH, poY, out float ex, out float eh, out float ey))
            {
                ax = ex - poX; ah = eh - poH; ay = ey - poY;
                float al = (float)Math.Sqrt(ax * ax + ah * ah + ay * ay);
                if (al < 1f) { ax = c.RetX; ah = c.RetH; ay = c.RetY; }
                else { ax /= al; ah /= al; ay /= al; }
            }
            float v = c.Speed * ReturnBoost;

            // Spawn CLEAR of her contact zone — a fresh shot born inside it dies on her at once (the
            // same contact-kill the absorb uses). The pouch is out in front, so a short lead is
            // normally enough; when the aim runs back through her (target behind), the shot leaves
            // from just past her body instead.
            float tx = xx - poX, th = xh - poH, ty = xy - poY;
            float along = tx * ax + th * ah + ty * ay;
            float perp2 = tx * tx + th * th + ty * ty - along * along;
            float lead = along > 0f && perp2 < (AbsorbNear + 4f) * (AbsorbNear + 4f) ? along + PastHer : ClearMin;
            float spX = poX + ax * lead, spH = poH + ah * lead, spY = poY + ay * lead;

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

        /// <summary>Nearest living enemy's AIM POINT: its lock-on frame's world position when the
        /// species set one (what Xiao's own shots fly at), else its origin raised by 8.</summary>
        private static bool PickTarget(float px, float ph, float py, out float ex, out float eh, out float ey)
        {
            ex = eh = ey = 0f;
            float best = float.MaxValue;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float cx = Memory.ReadFloat(p), ch = Memory.ReadFloat(p + 4), cy = Memory.ReadFloat(p + 8);
                if (cx == 0f && ch == 0f && cy == 0f) continue;
                float d = (cx - px) * (cx - px) + (cy - py) * (cy - py);
                if (d >= best) continue;
                best = d;
                ex = cx; eh = ch + EnemySlotOffsets.LockOnFallbackLift; ey = cy;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LockOnFrame)) != 0)
                {
                    long lp = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LockOnPoint);
                    float lx = Memory.ReadFloat(lp), lh = Memory.ReadFloat(lp + 4), ly = Memory.ReadFloat(lp + 8);
                    if (!(lx == 0f && lh == 0f && ly == 0f) && Math.Abs(lh - ch) < 200f) { ex = lx; eh = lh; ey = ly; }
                }
            }
            return best < float.MaxValue;
        }

        // ───────────────────────────────────── shield ring ─────────────────────────────────────

        /// <summary>Per tick while the slingshot is up: every live enemy farther than RingRadius gets a
        /// pointer to its own ring position (her center + RingRadius along the line to it); nearer ones,
        /// and empty slots, read the live player. Positions are written BEFORE the pointers.</summary>
        private static void UpdateRing(float xx, float xh, float xy)
        {
            if (!Mirage.Armed)
            {
                if (!_ringWarned) { _ringWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "redirect caves not armed — shield ring inactive (arms at the next town visit)"); }
                return;
            }
            var ring = new byte[RingSlots * 16];
            var ptrs = new byte[RingSlots * CodeCaves.PtrStride];
            int ringed = 0;
            for (int s = 0; s < RingSlots; s++)
            {
                uint ptr = StbExternCmd.PlayerPosGuest;
                if (s < EnemyAddresses.FloorSlots.Count && IsLiveEnemy(s))
                {
                    long p = EnemyAddresses.CharObjects.PosAddr(s);
                    float dx = Memory.ReadFloat(p) - xx, dy = Memory.ReadFloat(p + 8) - xy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > RingRadius)
                    {
                        BitConverter.GetBytes(xx + dx / d * RingRadius).CopyTo(ring, s * 16);
                        BitConverter.GetBytes(xh).CopyTo(ring, s * 16 + 4);
                        BitConverter.GetBytes(xy + dy / d * RingRadius).CopyTo(ring, s * 16 + 8);
                        BitConverter.GetBytes(1f).CopyTo(ring, s * 16 + 12);
                        ptr = SlingshotProp.RingTableGuest + (uint)(s * 16);
                        ringed++;
                    }
                }
                BitConverter.GetBytes(ptr).CopyTo(ptrs, s * CodeCaves.PtrStride);
            }
            bool first = !RingActive;
            RingActive = true;                                   // Mirage's writer stands down from here
            if (first && _blockArmed) Memory.WriteFloat(CodeCaves.Mailbox.ShieldBlockAddend, RingRadius);   // bodies stop at the ring
            Memory.WriteBytesBatch(SlingshotProp.RingTable, ring);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs);
            if (first) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shield ring up (R={RingRadius:F1}, {ringed} enemies ringed)");
        }

        /// <summary>Every managed pointer back to the live player; Mirage's writer resumes.</summary>
        private static void ReleaseRing()
        {
            if (!RingActive) return;
            var ptrs = new byte[RingSlots * CodeCaves.PtrStride];
            for (int s = 0; s < RingSlots; s++) BitConverter.GetBytes(StbExternCmd.PlayerPosGuest).CopyTo(ptrs, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs);
            if (_blockArmed) Memory.WriteFloat(CodeCaves.Mailbox.ShieldBlockAddend, VanillaBlockAddend);
            RingActive = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shield ring released");
        }

        private static bool IsLiveEnemy(int s)
        {
            int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
            if (id == 0 || id == 0xFFFF) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }

        // ───────────────────────────────── physical block patch ─────────────────────────────────

        /// <summary>Make MoveCheck2's enemy-block addend a data word (see the constants above). Cold only:
        /// called from the main-menu entry and retried from the loop while not on a dungeon floor.
        /// Idempotent; refuses (log once) if the words are neither vanilla nor ours.</summary>
        internal static void ArmBlockPatch()
        {
            if (_blockArmed) return;
            try
            {
                uint w0 = (uint)Memory.ReadInt(BlockPatchAddr), w1 = (uint)Memory.ReadInt(BlockPatchAddr + 4);
                if (w0 == BlockPatched[0] && w1 == BlockPatched[1])
                {
                    Memory.WriteFloat(CodeCaves.Mailbox.ShieldBlockAddend, VanillaBlockAddend);   // stale value from a dead session
                    _blockArmed = true;
                    return;
                }
                if (w0 != BlockPristine[0] || w1 != BlockPristine[1])
                {
                    if (!_blockWarned) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"MoveCheck2 @0x{BlockPatchAddr:X8} is not vanilla ({w0:X8} {w1:X8}) — shield block not armed");
                    _blockWarned = true;
                    return;
                }
                Memory.WriteFloat(CodeCaves.Mailbox.ShieldBlockAddend, VanillaBlockAddend);   // data first...
                Memory.WriteUInt(BlockPatchAddr,     BlockPatched[0]);                           // ...then lui (a half-applied pair is harmless this way round)
                Memory.WriteUInt(BlockPatchAddr + 4, BlockPatched[1]);
                _blockArmed = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shield block armed: MoveCheck2 addend → data word (6.0 vanilla)");
            }
            catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "block patch failed: " + e.Message); }
        }

        /// <summary>Make checkCollision's player-position load a pointer read (see the constants). Cold only.</summary>
        internal static void ArmShotPatch()
        {
            if (_shotArmed) return;
            try
            {
                uint w0 = (uint)Memory.ReadInt(ShotPatchAddr), w1 = (uint)Memory.ReadInt(ShotPatchAddr + 4);
                if (w0 == ShotPatched[0] && w1 == ShotPatched[1])
                {
                    Memory.WriteUInt(CodeCaves.Mailbox.ShotHitTarget, StbExternCmd.PlayerPosGuest);
                    _shotArmed = true;
                    return;
                }
                if (w0 != ShotPristine[0] || w1 != ShotPristine[1])
                {
                    if (!_shotWarned) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"checkCollision @0x{ShotPatchAddr:X8} is not vanilla ({w0:X8} {w1:X8}) — engine-side catch not armed");
                    _shotWarned = true;
                    return;
                }
                Memory.WriteUInt(CodeCaves.Mailbox.ShotHitTarget, StbExternCmd.PlayerPosGuest);   // pointer first...
                Memory.WriteUInt(ShotPatchAddr,     ShotPatched[0]);                               // ...then lui $a1
                Memory.WriteUInt(ShotPatchAddr + 4, ShotPatched[1]);                               // ...then lw $a1
                _shotArmed = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "engine-side catch armed: checkCollision reads the shot target through a pointer");
            }
            catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shot patch failed: " + e.Message); }
        }

        /// <summary>Aim every enemy shot's hit-the-player test at the pouch while the shield is solid, back
        /// at her when it is not. Two writes per transition, nothing per frame.</summary>
        private static void RedirectShots(bool solid)
        {
            if (!_shotArmed) return;
            uint target = solid ? SlingshotProp.ShotTargetGuest : 0u;
            bool want = target != 0;
            if (want == _shotRedirected) return;
            Memory.WriteUInt(CodeCaves.Mailbox.ShotHitTarget, want ? target : StbExternCmd.PlayerPosGuest);
            _shotRedirected = want;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (want ? $"shots now collide with the pouch (0x{target:X})" : "shots collide with her again"));
        }

        // ─────────────────────────────────── melee hit watch ───────────────────────────────────

        /// <summary>Frame-rate scan of the melee pool while the shield is solid: an open, player-hurting
        /// sphere overlapping the slingshot's volume is a HIT — consume the entry (it has spent itself
        /// on the shield, exactly as a guarded hit does), plant the engine's own hit-mark at the sphere,
        /// play the guard clink, and flag the loop to dispel.</summary>
        private static void HitWatch()
        {
            var hm = new byte[0x20];
            while (true)
            {
                int sleep = HitWatchMs;
                try
                {
                    bool quiet = _dispelling;                       // fading out: still eat the swing that broke it, silently
                    if (!SlingshotProp.Active || (_alpha < 1f && !quiet)) { Thread.Sleep(50); continue; }
                    long pool = Memory.ReadInt(NowColDataPtr);
                    if (pool <= 0) { Thread.Sleep(50); continue; }
                    pool += 0x20000000;
                    if (!SlingshotProp.RootWorld(out float rx, out float rh, out float ry)) { Thread.Sleep(50); continue; }
                    byte[] flags = Memory.ReadBytesBatch(pool + ColActiveOff, ColEntries * 4);
                    if (flags == null) { Thread.Sleep(50); continue; }
                    for (int i = 0; i < ColEntries; i++)
                    {
                        if (BitConverter.ToInt32(flags, i * 4) == 0) continue;
                        byte[] e = Memory.ReadBytesBatch(pool + i * ColStride, ColStride);
                        if (e == null) continue;
                        if ((BitConverter.ToUInt32(e, ColMask) & HurtsPlayerMask) == 0) continue;
                        if (BitConverter.ToInt32(e, ColGateA) != BitConverter.ToInt32(e, ColGateB)) continue;
                        float ex = BitConverter.ToSingle(e, 0), eh = BitConverter.ToSingle(e, 4), ey = BitConverter.ToSingle(e, 8);
                        float r  = BitConverter.ToSingle(e, ColRadius);
                        float dx = ex - rx, dy = ey - ry;
                        if (dx * dx + dy * dy > (r + PropHitRadius) * (r + PropHitRadius)) continue;
                        if (eh + r < rh - PropHitBelow || eh - r > rh + PropHitHeight) continue;

                        Memory.WriteInt(pool + ColActiveOff + i * 4, 0);            // spent itself on the shield
                        if (BitConverter.ToSingle(e, ColClass) != 0f)
                        {
                            // A SHOT's impact sphere landing on the slingshot: swallowed, never a hit.
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"swallowed a shot impact on the slingshot (entry {i}, r={r:F1})");
                            continue;
                        }
                        if (quiet) continue;                                        // no second spark/clink while dispelling
                        Array.Clear(hm, 0, hm.Length);
                        Array.Copy(e, 0, hm, 0, 16);                                // hit-mark at the sphere's center
                        BitConverter.GetBytes(HitMarkLife).CopyTo(hm, 0x10);
                        BitConverter.GetBytes(0).CopyTo(hm, 0x14);
                        BitConverter.GetBytes(1).CopyTo(hm, 0x18);                  // active last
                        Memory.WriteBytesBatch(HitMarkPool, hm);
                        SeSeq.Play(GuardClinkSe, 30);
                        int owner = BitConverter.ToInt32(e, ColOwner), col = BitConverter.ToInt32(e, ColColIdx);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"melee hit on the slingshot: entry {i} col {col}, r={r:F1}, owner {(owner >= 200 ? "slot " + (owner - 200) / 5 : owner.ToString())}");
                        _hitFlag = true;
                        sleep = 200;                                                // debounce: one hit is enough
                        break;
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "hit watch failed: " + e.Message); sleep = 500; }
                Thread.Sleep(sleep);
            }
        }

        private static void HardReset()
        {
            ReleaseRing();
            _dispelling = false;
            _hitFlag = false;
            RedirectShots(false);
            if (SlingshotProp.Active) SlingshotProp.Despawn();
            _claimed.Clear();
            _pending.Clear();
            _alpha = 0f;
            _pullTick = -1;
            _pendingWait = 0;
            SlingshotProp.OrbitTarget = 0f;
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
