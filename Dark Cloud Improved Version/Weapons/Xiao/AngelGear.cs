using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Gear "Guardian Reflector" (roadmap PR 7).
    /// While Xiao guards with the Angel Gear — or with Super Steve carrying an Angel Gear SynthSphere
    /// (<see cref="SuperSteve.AttachedSphere"/>), the copy then being Super Steve itself: every slingshot
    /// shares the rig, KEY table and pouch track the prop reads by name — a GIANT COPY of her slingshot stands IN
    /// FRONT of her for the whole guard hold (transparent → solid over 0.25 s, folding away over 0.5 s on
    /// release). It ORBITS her to face, in priority: the nearest enemy shot closing on her (even
    /// when an enemy is nearer), else the nearest enemy, else straight ahead — and, while it fires,
    /// the enemy it is about to shoot.
    ///
    /// INTERCEPT &amp; RE-FIRE : every closing shot is claimed (latch held —
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
    internal static class AngelGear
    {
        internal static bool Enabled = true;

        private const string Tag = "[AngelGear] ";

        private const int  XiaoId = 1;

        // The monster shot-effect pack's layout: WeaponAddresses.ShotEffectPack.

        // ── tuning ──
        private const float ClaimRadius  = 100f;   // claim a closing shot inside this range of Xiao
        private const float PouchHeight  = 7.5f;   // copy root height above her feet (8.5 read slightly high at 2x)
        private const float PropAhead    = 12f;    // copy root this far out from her, along the orbit bearing
        private const float CaptureRadius = 7f;    // a faced shot this close to the pouch is caught (+ 2 ticks of travel)
        private const float HomingRange  = 40f;    // faced shot is homed into the pouch from this range
        private const float HomingGain   = 0.35f;  // per-tick blend of its direction toward the pouch
        private const int   FireWaitMax  = 20;     // ticks a caught shot waits for the sky to clear before firing anyway
        // SHIELD RING : enemies keep Xiao's CENTER as their target, but each one
        // "sees" her at the point on a ring of the slingshot's radius along its own approach line — so it
        // walks straight at her and stops/attacks on reaching the slingshot. Pure data: the Mirage redirect
        // caves make _GET_POSITION/_GET_DISTANCE read a per-slot POINTER (CodeCaves.PtrTable); the ring
        // points each live enemy's pointer at its own ring position. Enemies already inside the ring see her
        // real position (they are past the shield). Released → every pointer back to the live player.
        private const float RingRadius   = PropAhead + PropHitRadius;   // the slingshot's far face: bodies stop at the volume a swing is tested against
        private const int   RingSlots    = 20;               // per-slot entries managed (Mirage manages the same 20)
        // MELEE HIT ON THE SLINGSHOT: enemy swings are CCollisionData spheres in the
        // NowColData pool, planted by CMonstorUnit::CheckDmg (0x1D9F10) only during an attack's damage
        // window (mask +0x48 bit 1 = hurts the player, owner +0x58 = slot*5+200). The player is hit
        // when CheckHitUser (0x1B5920) finds an open entry (+0x70 == +0x74) whose horizontal distance
        // ≤ its radius and whose vertical band overlaps hers. A guarded hit (BtCheckDamageProc, dun
        // 0x1DBAFD0) then CONSUMES the entry, plants a hit-mark (MyHitPointMark @0x1EC4940, 16 × 0x20:
        // pos vec4, +0x10 life 0x10, +0x14 timer 0, +0x18 active 1 — entry 0 for guards) and plays
        // SndSePlay(0xA2), the guard clink. The slingshot does the same at frame rate against its own
        // volume, then dispels and cools down.
        // Set's 2nd arg → entry +0x38: 0 for MELEE/contact planters (CheckDmg, player swings, bombs, status
        // powders) and 1.0f for PROJECTILE/effect planters (CSHOT_EFFECT impacts, MACHINGUN, FIREBAR, thrown
        // items) — the discriminator that keeps a shot detonating near the slingshot from reading as a swing.
        private const int    ColColIdx = 0x60;
        // ── REFLECTED DAMAGE (Stage C) ──
        // A reflected shot stays a latched visual (its own mask is the enemy-shot one, so the engine never
        // collides it with monsters). We track OUR in-flight shots only; on contact with a live enemy the
        // shot's wait is zeroed (the engine ends the flight next frame with the shot's own impact motion,
        // planting nothing because latched) and ONE pellet-style CollisionData entry is planted:
        //   +0x34 base = weapon ATTACK × (dungeon+1)/14  (0 = Divine Beast Cave … 6 = Demon Shaft = half attack;
        //                /7 read too strong for a defensive ability)
        //   +0x50 = the SHOT's element bit (pure) or its enemy-valid status bits (0x100/0x200/0x800)
        //   +0x58 = 1 (Xiao: ranged falloff + kill credit), +0x64 = her stats block, +0x6C = her ability flags
        // and CMonstorUnit::CheckDmg does defense, anti-category, resistance, No Effect, statuses, numbers.
        private const long   BattleWeaponAttack  = WeaponHave.BattleWeaponRecord + 0x04;   // short (BattleActionPlay_Jinn's pellet damage)
        private const float  TierDivisor = 14f;
        private const uint   ShotElementMask = 0x1F, ShotEnemyStatusMask = 0x100 | 0x200 | 0x800;
        private const float  HitMargin = 4f;                                              // contact slack + planted-entry reach
        private const int    PlantedLifeTicks = 2;                                       // retire an unconsumed entry
        private const int    ReflectMaxTicks = 260;                                      // give up tracking (FreshTimers + slack)
        // RULE: melee is melee — any melee-class player-hurting sphere on the slingshot
        // dispels it. Shots are shots — a pool shot whose BODY reaches the slingshot is caught there and
        // re-fired (see CatchAtProp), and a shot's impact sphere landing on it is swallowed, never a hit.
        private const long   HitMarkPool = 0x21EC4940;
        private const int    HitMarkLife = 0x10;
        private const ushort GuardClinkSe = 0xA2;
        private const float  PropHitRadius = 6f, PropHitHeight = 14f, PropHitBelow = 2f;   // the copy's volume about its root
        private const int    HitFadeTicks = 3;             // dispel: solid → gone in 0.15 s
        private const double CooldownSeconds = 4.0;       // the bar refills from 0 to full over this after a break
        // SHIELD HP ON THE ATTACK GAUGE: the bottom-left speed bar (float 0x1DC44C8, 0..100;
        // Xiao's shot needs 100 and zeroes it; a hit on her resets it to 100; MainDraw fills 128 px from it and
        // flashes at 100) shows the slingshot's HP: 5 hits, −20 each. The ISO's dun.bin patch (DunPatches)
        // makes Xiao's refill multiplier the MAILBOX word ShieldGaugeRate (pnach-seeded 1.5 while nobody owns
        // it): the app takes ownership (ShieldGaugeOwner = 1) only while the shield is up or broken, writing 0
        // to hold the bar and a small value so the ENGINE refills it over CooldownSeconds (no per-tick
        // writes); a full bar = the slingshot may respawn. Ownership is released otherwise (self-healing).
        private const long   GaugeRateWord = CodeCaves.Mailbox.ShieldGaugeRate;
        private const long   GaugeAddr = 0x21DC44C8;
        private const int    ShieldHits = 5;
        private const float  GaugePerHit = 100f / ShieldHits;
        private const float  VanillaXiaoRefillMul = 1.5f;
        private const int    HitWatchMs = 16;
        // PHYSICAL BLOCK (a collision circle, not a per-frame clamp). CMonstorUnit::MoveCheck2
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
        // ENGINE-SIDE CATCH (no per-tick collision checks). checkCollision (0x1AB740) is every
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
            public int    LastWait;                    // its flight countdown when last seen: 0 = it ran out, not a contact
        }

        private static Thread _thread;
        private static readonly List<Claim> _claimed = new();   // in flight toward Xiao (latched)
        private static readonly List<Claim> _pending = new();   // absorbed, awaiting re-fire
        private sealed class Fired { public int Slot, Idx, Ticks; public uint Flags; public float Radius; }
        private static readonly List<Fired> _flying  = new();   // OUR reflected shots in flight
        private static readonly List<(int idx, int ticks)> _planted = new();   // entries we planted, to retire
        private static float _alpha;                            // prop opacity 0..1
        private static bool  _jingled;                          // once per appearance
        private static int   _pullTick = -1;                    // -1 idle; else ticks into the fire cycle
        private static int   _pendingWait;                      // ticks the head of the queue has waited to fire
        private static bool  _ringWarned, _blockArmed, _blockWarned, _shotArmed, _shotWarned, _shotRedirected;
        private static Thread _hitThread;
        private static volatile bool _hitFlag;                  // set by the hit watch, consumed by the loop
        private static bool  _dispelling;                       // hit → fast fade-out in progress
        private static DateTime _cooldownUntil;                 // no-PNACH fallback: no respawn before this
        private static int   _shieldHp;                         // hits left on the standing shield
        private static bool  _cooling;                          // broken: waiting for the bar to refill
        private static bool  _gaugeLive;                        // PNACH present → the gauge is ours to drive
        private static float _rateWritten = float.NaN;
        private static int   _ownerWritten = -1;
        /// <summary>The shield ring owns the AI redirect pointer table (Mirage's table writer stands down).</summary>
        internal static bool RingActive { get; private set; }

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            try { Memory.WriteFloat(GaugeRateWord, VanillaXiaoRefillMul); Memory.WriteInt(CodeCaves.Mailbox.ShieldGaugeOwner, 0); _rateWritten = VanillaXiaoRefillMul; _ownerWritten = 0; }   // vanilla until a shield stands
            catch (Exception e) { Console.WriteLine(Tag + "gauge seed failed: " + e.Message); }
            _thread = new Thread(Loop) { IsBackground = true, Name = "AngelGear" };
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
                    if (!_blockArmed && !Player.InDungeonFloor()) ArmBlockPatch();
                    if (!_shotArmed && !Player.InDungeonFloor()) ArmShotPatch();
                    // Live only with the Angel Gear, or Super Steve carrying its sphere: everything below — the gauge, the
                    // ring, shot tracking — is that weapon's. Anything else, and one reset puts it all back to vanilla.
                    bool inDun = Enabled && Player.InDungeonFloor() && Player.CurrentCharacterNum() == XiaoId;
                    int  weaponId = inDun ? Memory.ReadUShort(WeaponHave.BattleWeaponRecord) : 0;
                    bool gear  = weaponId == Items.angelgear
                              || (weaponId == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.angelgear);
                    bool live  = inDun && gear;
                    if (live) sleep = FastTickMs;
                    long pack = live ? Memory.ReadInt(ShotEffectPack.NowShotEffectPtr) : 0;
                    if (pack <= 0)
                    {
                        if (_live) { HardReset(); _live = false; }
                        Thread.Sleep(sleep);
                        continue;
                    }
                    _live = true;
                    bool held  = Player.CheckDunIsPausedOrMenu();   // PAUSE screen or menu: everything stands still
                    bool armed = GuardWatch.IsGuarding();
                    pack += 0x20000000;
                    if (held) { Thread.Sleep(sleep); continue; }   // the prop holds its own slot; nothing here may advance

                    float xx = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos);
                    float xh = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
                    float xy = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
                    float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);

                    // The attack gauge is ours while the ISO's dun.bin refill patch is in this overlay.
                    _gaugeLive = (uint)Memory.ReadInt(DunPatches.GaugePatchAddrMmu) == DunPatches.GaugePatchedWord0;

                    // A MELEE HIT takes one of the shield's ShieldHits; the last one dispels it: fast fade-out,
                    // then the bar refills before it can return.
                    if (_hitFlag)
                    {
                        _hitFlag = false;
                        if (SlingshotProp.Shield && !_dispelling)
                        {
                            _shieldHp = Math.Max(0, _shieldHp - 1);
                            if (_gaugeLive) Memory.WriteFloat(GaugeAddr, _shieldHp * GaugePerHit);
                            if (_shieldHp > 0)
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slingshot struck — {_shieldHp}/{ShieldHits} left");
                            else
                            {
                                _dispelling = true; _cooling = true;
                                _cooldownUntil = GameClock.Now.AddSeconds(CooldownSeconds);
                                SeSeq.Play(SeSeq.WeaponBreak, 60);                 // the game's own weapon-break sound (WHP → 0)
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slingshot broken (weapon-break SE) — bar refills over {CooldownSeconds:0.#} s");
                            }
                        }
                    }
                    // Bar ownership: hold while the shield stands (and re-assert it if a hit on her reset the bar
                    // to 100), slow refill while broken, vanilla otherwise. Ready = the bar is full again.
                    if (SlingshotProp.Shield && !_dispelling)
                    {
                        SetGaugeRate(0f);
                        if (_gaugeLive && Math.Abs(Memory.ReadFloat(GaugeAddr) - _shieldHp * GaugePerHit) > 0.5f)
                            Memory.WriteFloat(GaugeAddr, _shieldHp * GaugePerHit);
                    }
                    else if (_dispelling) SetGaugeRate(0f);
                    else if (_cooling)
                    {
                        if (_gaugeLive)
                        {
                            SetGaugeRate(RefillMultiplier());
                            if (Memory.ReadFloat(GaugeAddr) >= 99.5f) { _cooling = false; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "bar full — slingshot ready"); }
                        }
                        else if (GameClock.Now >= _cooldownUntil) _cooling = false;
                    }
                    else SetGaugeRate(VanillaXiaoRefillMul);

                    // THE SHIELD FOLLOWS THE GUARD: up for the whole hold, folding away on release.
                    if (_dispelling)
                    {
                        _alpha = Math.Max(0f, _alpha - 1f / HitFadeTicks);
                        if (_alpha <= 0f || !SlingshotProp.Shield) { SlingshotProp.Despawn(); _dispelling = false; }
                    }
                    else if (armed)
                    {
                        bool ready = !_cooling && (!_gaugeLive || Memory.ReadFloat(GaugeAddr) >= 99.5f);   // full bar, as her own shot needs
                        if (!SlingshotProp.Shield && ready
                            && SlingshotProp.Spawn(PropScale, PouchHeight, PropAhead, PullLength))
                        { _alpha = 0f; _jingled = false; _shieldHp = ShieldHits; if (_gaugeLive) Memory.WriteFloat(GaugeAddr, 100f); }
                        if (SlingshotProp.Shield)
                        {
                            _alpha = Math.Min(1f, _alpha + 1f / FadeInTicks);
                            if (!_jingled && _alpha >= 1f)
                            { _jingled = true; SeSeq.Play(SeSeq.ChangeJingle, 90); }
                        }
                    }
                    else if (SlingshotProp.Shield)
                    {
                        _alpha = Math.Max(0f, _alpha - 1f / FadeOutTicks);
                        if (_alpha <= 0f) SlingshotProp.Despawn();
                    }
                    TrackReflected(pack, xx, xh, xy);

                    // Shots are only intercepted while the SHIELD is up (not while it is broken / cooling
                    // down / folding): with no slingshot they reach her exactly as vanilla.
                    bool shieldUp = armed && SlingshotProp.Shield && !_dispelling;
                    if (shieldUp) ClaimClosingShots(pack, xx, xh, xy);

                    // ORBIT: the copy circles her to face the nearest closing shot, else the nearest
                    // enemy, else straight ahead — and its fire target during a cycle. This tick only
                    // sets the wanted bearing; the prop's own frame-rate thread swings to it smoothly.
                    Claim faced = null;
                    if (SlingshotProp.Shield)
                    {
                        if (ChooseBearing(pack, xx, xh, xy, yaw, out float want, out faced))
                            SlingshotProp.OrbitTarget = Wrap(want - yaw);
                        SlingshotProp.Maintain(_alpha);
                    }
                    GetPouch(xx, xh, xy, yaw, out float px, out float ph, out float py);
                    RedirectShots(shieldUp && _alpha >= 1f);
                    if (SlingshotProp.Shield) UpdateRing(xx, xh, xy); else ReleaseRing();

                    SustainClaims(pack, shieldUp, xx, xh, xy, px, ph, py, faced);

                    // Fire cycle = the weapon's OWN keys (c04w##.cfg): 11 draw → 12 hold → 13 shoot
                    // (the fresh projectile leaves on the shoot key) → back to the copy's idle hold (KEY 14).
                    // It begins once nothing else is inbound (or the queue has waited FireWaitMax
                    // ticks), giving the copy time to turn onto its target first.
                    if (armed && SlingshotProp.Shield && _alpha >= 1f && (_pending.Count > 0 || _pullTick >= 0))
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

        /// <summary>Every live in-flight shot inside the guard radius, closing on Xiao and with the flight left to
        /// reach the pouch (its countdown × speed against the distance, the pouch standing PropAhead out toward it) is
        /// claimed: latched harmless and SNAPSHOTTED (species slot, speed, damage, owner fields, arrival line), then
        /// left to fly — the engine's contact-kill on her IS the absorb. A shot that would expire short is left alone.</summary>
        private static void ClaimClosingShots(long pack, float xx, float xh, float xy)
        {
            for (int s = 0; s < ShotEffectPack.PackSlots; s++)
            {
                long inst = pack + s * ShotEffectPack.SlotStride;
                int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
                if (count < 1 || count > ShotEffectPack.SubShots) continue;
                for (int i = 0; i < count; i++)
                {
                    if (IsClaimed(s, i)) continue;
                    if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) continue;
                    if (Memory.ReadUShort(inst + ShotEffectPack.OffPhase + i * 2) != 1) continue;
                    long obj = inst + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride;
                    float dx = xx - Memory.ReadFloat(obj + ShotEffectPack.ObjPos);
                    float dh = xh - Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4);
                    float dy = xy - Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                    if (dx * dx + dh * dh + dy * dy > ClaimRadius * ClaimRadius) continue;
                    long dirA = inst + ShotEffectPack.OffDir + i * 0x10;
                    float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                    if (vx * dx + vh * dh + vy * dy <= 0f) continue;

                    float speed = Math.Max(1f, (float)Math.Sqrt(vx * vx + vh * vh + vy * vy));
                    int wait = Memory.ReadInt(inst + ShotEffectPack.OffWait + i * 4);
                    float need = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy) - PropAhead - CaptureRadius;
                    if (wait * speed < need) continue;                                         // out of range: it dies before the pouch
                    float inv = -1f / speed;
                    Memory.WriteByte(inst + ShotEffectPack.OffLatch + i, LatchHold);
                    _claimed.Add(new Claim
                    {
                        Slot = s, Idx = i, Speed = speed,
                        RetX = vx * inv, RetH = vh * inv, RetY = vy * inv,
                        Damage  = Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4),
                        Owner   = Memory.ReadUShort(inst + ShotEffectPack.OffOwner + i * 2),
                        Attr2   = Memory.ReadUShort(inst + ShotEffectPack.OffAttr2 + i * 2),
                        SndFlag = Memory.ReadByte(inst + ShotEffectPack.OffSndFlag + i),
                        Reload  = Memory.ReadByte(inst + ShotEffectPack.OffReload + i),
                    });
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"claimed shot slot{s}#{i} speed={speed:F2}/frame dmg={Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4)}");
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
                        Memory.WriteByte(pack + c.Slot * ShotEffectPack.SlotStride + ShotEffectPack.OffLatch + c.Idx, 0);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"shield down — released {_claimed.Count} claimed, dropped {_pending.Count} pending");
                    _claimed.Clear(); _pending.Clear();
                }
                return;
            }
            bool armed = true, solid = SlingshotProp.Shield && _alpha >= 1f;
            for (int q = _claimed.Count - 1; q >= 0; q--)
            {
                var c = _claimed[q];
                long inst = pack + c.Slot * ShotEffectPack.SlotStride;
                bool gone = Memory.ReadUShort(inst + ShotEffectPack.OffActive + c.Idx * 2) == 0, caught = false, byEngine = gone;
                if (!gone)
                {
                    long obj = inst + ShotEffectPack.OffObj + c.Idx * ShotEffectPack.ObjStride;
                    float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sh = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                    c.LastX = sx; c.LastH = sh; c.LastY = sy; c.LastWait = Memory.ReadInt(inst + ShotEffectPack.OffWait + c.Idx * 4);
                    float bx = xx - sx, bh = xh - sh, by = xy - sy;          // shot → her body
                    float qx = px - sx, qh = ph - sh, qy = py - sy;          // shot → the pouch
                    float dq = qx * qx + qh * qh + qy * qy;
                    float catchR = CaptureRadius + c.Speed * 2f, kill = AbsorbNear + c.Speed * 3f;
                    if (solid && dq < catchR * catchR)
                    {
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + c.Idx * 2, 0);   // into the pouch
                        gone = true; caught = true;
                    }
                    else if (bx * bx + bh * bh + by * by < kill * kill
                             || Memory.ReadUShort(inst + ShotEffectPack.OffPhase + c.Idx * 2) > 1)
                    {
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + c.Idx * 2, 0);   // quiet vanish into her
                        gone = true;
                    }
                    else if (solid && c == faced && dq < HomingRange * HomingRange)
                    {
                        long dirA = inst + ShotEffectPack.OffDir + c.Idx * 0x10;
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
                                ShotEffects.FaceAlong(obj, nx, nh, ny);
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
                        if (c.LastWait <= 1)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot{c.Slot}#{c.Idx} ran out of flight short of the pouch — not re-fired");
                            continue;
                        }
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
                Memory.WriteByte(inst + ShotEffectPack.OffLatch + c.Idx, LatchHold);   // harmless to the end
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
                long obj = pack + c.Slot * ShotEffectPack.SlotStride + ShotEffectPack.OffObj + c.Idx * ShotEffectPack.ObjStride;
                float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
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
            if (SlingshotProp.Shield && SlingshotProp.PouchWorld(out px, out ph, out py)) return;
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
            long inst = pack + c.Slot * ShotEffectPack.SlotStride;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) { _pending.RemoveAt(0); return; }

            int j = -1;                                             // a free sub-shot to inhabit
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return;                                      // all busy — retry next tick

            long cfg = Memory.ReadInt(inst + ShotEffectPack.OffCfg);
            if (cfg <= 0) { _pending.RemoveAt(0); return; }
            cfg += 0x20000000;
            long obj  = inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride;
            long dirA = inst + ShotEffectPack.OffDir + j * 0x10;

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
            long ftab = Memory.ReadInt(obj + ShotEffectPack.ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + flyMot * 0x10) : 1;
            Memory.WriteUShort(inst + ShotEffectPack.OffPhase + j * 2, 1);          // flying, no muzzle
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos,     spX);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 4, spH);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 8, spY);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 12, 1f);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotId,  flyMot);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotFlag, 4);
            Memory.WriteFloat (obj + ShotEffectPack.ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ShotEffectPack.ObjFrame,  startFrame);
            WriteVec(dirA, ax * v, ah * v, ay * v);
            Memory.WriteInt   (inst + ShotEffectPack.OffWait + j * 4, FreshTimers);
            Memory.WriteInt   (inst + ShotEffectPack.OffDamage + j * 4, c.Damage);
            Memory.WriteInt   (inst + ShotEffectPack.OffUserCol + j * 4, -1);
            Memory.WriteUShort(inst + ShotEffectPack.OffOwner + j * 2, c.Owner);
            Memory.WriteUShort(inst + ShotEffectPack.OffAttr2 + j * 2, c.Attr2);
            Memory.WriteUShort(inst + ShotEffectPack.OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + ShotEffectPack.OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffA110 + j * 4, -1);
            Memory.WriteByte  (inst + ShotEffectPack.OffSndFlag + j, c.SndFlag);
            Memory.WriteByte  (inst + ShotEffectPack.OffReload + j, c.Reload);
            Memory.WriteByte  (inst + ShotEffectPack.OffLatch + j, LatchHold);      // harmless until the damage stage
            Memory.WriteInt   (inst + ShotEffectPack.OffLastIdx, j);
            ShotEffects.FaceAlong(obj, ax, ah, ay);
            Memory.WriteUShort(inst + ShotEffectPack.OffActive + j * 2, 1);         // live — engine steps it from here
            _flying.Add(new Fired { Slot = c.Slot, Idx = j, Flags = Memory.ReadUInt(cfg + ShotEffectPack.CfgFlags), Radius = Math.Max(0.5f, Memory.ReadFloat(cfg + ShotEffectPack.CfgRadiusFlying)) });

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
                if (s < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(s))
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

        // ─────────────────────────────────── attack gauge ───────────────────────────────────────

        /// <summary>Own the multiplier word while driving it (0 / slow refill); hand it back to the pnach's
        /// per-frame 1.5 seed otherwise, so the game is vanilla whenever the app is not actively shielding.</summary>
        private static void SetGaugeRate(float k)
        {
            int owner = k == VanillaXiaoRefillMul ? 0 : 1;
            if (owner == 1 && _ownerWritten != 1) { Memory.WriteInt(CodeCaves.Mailbox.ShieldGaugeOwner, 1); _ownerWritten = 1; }
            if (k != _rateWritten) { Memory.WriteFloat(GaugeRateWord, k); _rateWritten = k; }
            if (owner == 0 && _ownerWritten != 0) { Memory.WriteInt(CodeCaves.Mailbox.ShieldGaugeOwner, 0); _ownerWritten = 0; }
        }

        /// <summary>Multiplier that makes the engine's own refill — max(1, speed/30) per frame — fill the bar
        /// from 0 to 100 over CooldownSeconds (status halving/doubling ignored).</summary>
        private static float RefillMultiplier()
        {
            float speed = Memory.ReadShort(WeaponHave.BattleWeaponRecord + 8);
            float perFrame = Math.Max(1f, speed / 30f);
            return (float)(100.0 / (CooldownSeconds * 60.0 * perFrame));
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

        // ───────────────────────────────── reflected shot impact ────────────────────────────────

        /// <summary>Follow OUR reflected shots: keep each latched, and when one reaches a live enemy end
        /// its flight (wait = 0 → the engine's impact motion) and plant the damage entry. Also retire
        /// planted entries the engine did not consume.</summary>
        private static void TrackReflected(long pack, float xx, float xh, float xy)
        {
            for (int q = _planted.Count - 1; q >= 0; q--)
            {
                var (idx, ticks) = _planted[q];
                if (ticks <= 0)
                {
                    long pool = Memory.ReadInt(CollisionPool.Pointer);
                    if (pool > 0) CollisionPool.Deactivate(pool + 0x20000000, idx);
                    _planted.RemoveAt(q);
                }
                else _planted[q] = (idx, ticks - 1);
            }
            if (_flying.Count == 0) return;
            for (int q = _flying.Count - 1; q >= 0; q--)
            {
                var f = _flying[q];
                long inst = pack + f.Slot * ShotEffectPack.SlotStride;
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + f.Idx * 2) == 0 || ++f.Ticks > ReflectMaxTicks) { _flying.RemoveAt(q); continue; }
                Memory.WriteByte(inst + ShotEffectPack.OffLatch + f.Idx, LatchHold);                      // never plants on its own
                long obj = inst + ShotEffectPack.OffObj + f.Idx * ShotEffectPack.ObjStride, dirA = inst + ShotEffectPack.OffDir + f.Idx * 0x10;
                float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sh = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                float speed = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy);
                for (int e = 0; e < EnemyAddresses.FloorSlots.Count; e++)
                {
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.Hp)) <= 0) continue;
                    long p = EnemyAddresses.CharObjects.PosAddr(e);
                    float ex = Memory.ReadFloat(p), eh = Memory.ReadFloat(p + 4), ey = Memory.ReadFloat(p + 8);
                    if (ex == 0f && eh == 0f && ey == 0f) continue;
                    float body = Math.Max(2f, Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.EntityScale)));
                    // judge height against the lock-on point when the species has one (chest), else origin + 8
                    float th = eh + EnemySlotOffsets.LockOnFallbackLift;
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.LockOnFrame)) != 0)
                    {
                        float lh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.LockOnPoint) + 4);
                        if (Math.Abs(lh - eh) < 200f) th = lh;
                    }
                    float reach = body + f.Radius + speed * 2f + HitMargin;
                    float dx = sx - ex, dh = sh - th, dy = sy - ey;
                    if (dx * dx + dy * dy > reach * reach) continue;
                    if (Math.Abs(dh) > body + HitMargin + 12f) continue;

                    Memory.WriteInt(inst + ShotEffectPack.OffWait + f.Idx * 4, 0);                        // flight ends next frame: impact motion, no entry (latched)
                    PlantReflectedHit(sx, sh, sy, body + f.Radius + HitMargin, f.Flags, e);
                    _flying.RemoveAt(q);
                    break;
                }
            }
        }

        /// <summary>One pellet-style CollisionData entry at the impact, fields per the RE doc §C.</summary>
        private static void PlantReflectedHit(float x, float h, float y, float radius, uint shotFlags, int enemySlot)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int slot = CollisionPool.TakeFreeSlot(pool);
            if (slot < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no free collision entry — reflected hit lost"); return; }

            int dungeon = Math.Max(0, Math.Min(6, (int)Memory.ReadByte(Addresses.checkDungeon)));
            float attack = Memory.ReadShort(BattleWeaponAttack);
            int baseDmg = Math.Max(1, (int)Math.Round(attack * (dungeon + 1) / TierDivisor));
            uint elem = shotFlags & ShotElementMask, stat = shotFlags & ShotEnemyStatusMask;
            // +0x50 must be a PURE element bit (or 0): any status bit there sends CheckDmg's element branch
            // through index 5 = MinGoldDrop as the percent (vanilla quirk — the rose's gooey shot did ~35 of
            // 156). Statuses are applied by data instead, with CheckDmg's own rules (ApplyReflectedStatus).
            uint attr = (elem != 0 && (shotFlags & 0xFF00) == 0) ? elem : 0u;
            string statusNote = stat != 0 ? ApplyReflectedStatus(enemySlot, stat) : "";

            CollisionPool.Plant(pool, slot, CollisionPool.PlayerHitEntry(x, h, y, radius, baseDmg, attr));
            _planted.Add((slot, PlantedLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"reflected hit on slot {enemySlot}: base {baseDmg} (atk {attack:F0} × {dungeon + 1}/{TierDivisor:F0}), attr 0x{attr:X} ({(attr != 0 ? "element" : "none")}){statusNote}, r={radius:F1} → entry {slot}");
        }

        /// <summary>The shot's statuses, applied exactly as CheckDmg applies +0x50 bits: gated only by the
        /// species susceptibility (0 = immune); poison 180 f unless frozen/raging (clears gooey); freeze 300 f
        /// (toggles OFF if already frozen; clears the others and the move blend); gooey 180 f only when no
        /// other status is up. Curse (0x400) and 0x1000 do nothing to enemies, as in vanilla.</summary>
        private static string ApplyReflectedStatus(int slot, uint stat)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
            if (Memory.ReadShort(a + EnemySlotOffsets.StatusSusceptibility) == 0) return " status: immune";
            int freeze = Memory.ReadInt(a + EnemySlotOffsets.FreezeTimer), poison = Memory.ReadInt(a + EnemySlotOffsets.PoisonPeriod);
            int rage = Memory.ReadInt(a + EnemySlotOffsets.StaminaTimer);
            var applied = new List<string>();
            if ((stat & 0x200) != 0 && freeze == 0 && rage == 0)
            {
                Memory.WriteInt(a + EnemySlotOffsets.PoisonPeriod, 0xB4); Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0); poison = 0xB4; applied.Add("poison");
            }
            if ((stat & 0x100) != 0)
            {
                if (freeze < 1)
                {
                    Memory.WriteInt(a + EnemySlotOffsets.FreezeTimer, 300); Memory.WriteInt(a + EnemySlotOffsets.PoisonPeriod, 0);
                    Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0); Memory.WriteInt(a + EnemySlotOffsets.StaminaTimer, 0);
                    Memory.WriteFloat(a + EnemySlotOffsets.MovementBlend, 0f); freeze = 300; poison = 0; applied.Add("freeze");
                }
                else { Memory.WriteInt(a + EnemySlotOffsets.FreezeTimer, 0); freeze = 0; applied.Add("freeze off"); }
            }
            if ((stat & 0x800) != 0 && freeze == 0 && poison == 0 && rage == 0)
            {
                Memory.WriteInt(a + EnemySlotOffsets.GooeyState, 0xB4); applied.Add("gooey");
            }
            return applied.Count > 0 ? " status: " + string.Join("+", applied) : " status: blocked by an active status";
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
                    if (!SlingshotProp.Shield || (_alpha < 1f && !quiet)) { Thread.Sleep(50); continue; }
                    long pool = Memory.ReadInt(CollisionPool.Pointer);
                    if (pool <= 0) { Thread.Sleep(50); continue; }
                    pool += 0x20000000;
                    if (!SlingshotProp.RootWorld(out float rx, out float rh, out float ry)) { Thread.Sleep(50); continue; }
                    byte[] flags = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
                    if (flags == null) { Thread.Sleep(50); continue; }
                    for (int i = 0; i < CollisionPool.Entries; i++)
                    {
                        if (BitConverter.ToInt32(flags, i * 4) == 0) continue;
                        byte[] e = Memory.ReadBytesBatch(pool + i * CollisionPool.Stride, CollisionPool.Stride);
                        if (e == null) continue;
                        if ((BitConverter.ToUInt32(e, CollisionPool.Mask) & CollisionPool.HurtsPlayerMask) == 0) continue;
                        if (BitConverter.ToInt32(e, CollisionPool.GateA) != BitConverter.ToInt32(e, CollisionPool.GateB)) continue;
                        float ex = BitConverter.ToSingle(e, 0), eh = BitConverter.ToSingle(e, 4), ey = BitConverter.ToSingle(e, 8);
                        float r  = BitConverter.ToSingle(e, CollisionPool.Radius);
                        float dx = ex - rx, dy = ey - ry;
                        if (dx * dx + dy * dy > (r + PropHitRadius) * (r + PropHitRadius)) continue;
                        if (eh + r < rh - PropHitBelow || eh - r > rh + PropHitHeight) continue;

                        Memory.WriteInt(pool + CollisionPool.ActiveOff + i * 4, 0);            // spent itself on the shield
                        if (BitConverter.ToSingle(e, CollisionPool.EntryClass) != 0f)
                        {
                            // A SHOT's impact sphere landing on the slingshot: swallowed, never a hit.
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"swallowed a shot impact on the slingshot (entry {i}, r={r:F1})");
                            continue;
                        }
                        if (quiet) continue;                                        // no second spark/clink while dispelling
                        Array.Clear(hm, 0, hm.Length);
                        Array.Copy(e, 0, hm, 0, 16);                                // the sphere's centre …
                        float cdx = rx - ex, cdh = rh - eh, cdy = ry - ey, cl = (float)Math.Sqrt(cdx * cdx + cdh * cdh + cdy * cdy);
                        if (cl > 1e-3f)                                             // … moved to its surface toward the prop: the contact point
                        {
                            BitConverter.GetBytes(ex + cdx / cl * r).CopyTo(hm, 0);
                            BitConverter.GetBytes(eh + cdh / cl * r).CopyTo(hm, 4);
                            BitConverter.GetBytes(ey + cdy / cl * r).CopyTo(hm, 8);
                        }
                        BitConverter.GetBytes(HitMarkLife).CopyTo(hm, 0x10);
                        BitConverter.GetBytes(0).CopyTo(hm, 0x14);
                        BitConverter.GetBytes(1).CopyTo(hm, 0x18);                  // active last
                        Memory.WriteBytesBatch(HitMarkPool, hm);
                        SeSeq.Play(GuardClinkSe, 30);
                        int owner = BitConverter.ToInt32(e, CollisionPool.Owner), col = BitConverter.ToInt32(e, ColColIdx);
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

        private static bool _live;   // the loop's state is up (weapon live in a dungeon); one HardReset when it stops being

        private static void HardReset()
        {
            ReleaseRing();
            _dispelling = false;
            _hitFlag = false;
            _flying.Clear();
            _planted.Clear();
            _cooling = false;
            _shieldHp = 0;
            SetGaugeRate(VanillaXiaoRefillMul);
            RedirectShots(false);
            if (SlingshotProp.Shield) SlingshotProp.Despawn();
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

        /// <summary>Xiao's Angel Gear thread: runs while the weapon is equipped and hands every tick to
        /// <see cref="DriveAngelGear"/>, which owns the cadence — so a pause, a menu, a chest or a conversation only
        /// holds the interval, never restarts it.</summary>
        public static void AngelGearEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.angelgear)
            {
                DriveAngelGear(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
        }

        private const ushort AngelGearHealAmount = 1;
        private static int _healTickPrev = -1;   // the counter last seen; -1 = not watching, re-seed on the next tick

        /// <summary>Angel Gear's regen, driven every tick by Xiao's own thread and by Super Steve's when it inherits the
        /// weapon. It rides the native HEAL ability's own cadence: each wrap of <see cref="HealAbility.TickCounter"/> —
        /// the frame the game grants its +1 — heals each ally by <see cref="AngelGearHealAmount"/> (skipping the dead and
        /// the already-full). Xiao is healed too UNLESS the equipped weapon carries the native Heal build-up attribute
        /// (Special2 % 16 in 8..11), which already regenerates her. Opening mid-cycle never procs retroactively. While Xiao
        /// guards, <see cref="AngelShooter"/> floors the counter so the native tick fires every second, and the party heal
        /// follows — the counter only ever climbs, is set upward by that floor, or resets to 0 on a proc, so any decrease
        /// is a proc.</summary>
        internal static void DriveAngelGear(bool active)
        {
            if (!active || Player.CheckDunIsPausedOrMenu() || !Player.CheckDunIsWalkingMode()) { _healTickPrev = -1; return; }
            int c = Memory.ReadInt(HealAbility.TickCounter);
            bool wrapped = _healTickPrev >= 0 && c < _healTickPrev;   // the native +1 just fired
            _healTickPrev = c;
            if (!wrapped) return;

            HealAlly(Player.Toan.GetHp(),   Player.Toan.GetMaxHp(),   Player.Toan.SetHp);
            HealAlly(Player.Goro.GetHp(),   Player.Goro.GetMaxHp(),   Player.Goro.SetHp);
            HealAlly(Player.Ruby.GetHp(),   Player.Ruby.GetMaxHp(),   Player.Ruby.SetHp);
            HealAlly(Player.Ungaga.GetHp(), Player.Ungaga.GetMaxHp(), Player.Ungaga.SetHp);
            HealAlly(Player.Osmond.GetHp(), Player.Osmond.GetMaxHp(), Player.Osmond.SetHp);

            // Xiao only if the equipped weapon lacks the native Heal attribute (else the game already regens her).
            int special2 = Player.Weapon.GetCurrentWeaponSpecial2() % 16;
            if (special2 < 8 || special2 > 11)
                HealAlly(Player.Xiao.GetHp(), Player.Xiao.GetMaxHp(), Player.Xiao.SetHp);
        }

        private static void HealAlly(ushort hp, int maxHp, Action<ushort> setHp)
        {
            if (hp > 0 && hp < maxHp) setHp((ushort)(hp + AngelGearHealAmount));
        }

    }
}
