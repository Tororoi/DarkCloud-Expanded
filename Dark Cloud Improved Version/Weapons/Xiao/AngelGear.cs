using System;
using System.Threading;
using static Dark_Cloud_Improved_Version.ShotReflect;
using static Dark_Cloud_Improved_Version.ShieldRing;
using static Dark_Cloud_Improved_Version.ShieldPatches;
using static Dark_Cloud_Improved_Version.ReflectedHits;
using static Dark_Cloud_Improved_Version.MeleeHitWatch;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Gear "Guardian Reflector".
    /// While Xiao guards with the Angel Gear — or with Super Steve carrying an Angel Gear SynthSphere
    /// (<see cref="SuperSteve.AttachedSphere"/>), the copy then being Super Steve itself: every slingshot
    /// shares the rig, KEY table and pouch track the prop reads by name — a GIANT COPY of her slingshot stands IN
    /// FRONT of her for the whole guard hold (transparent → solid over 0.25 s, folding away over 0.5 s on
    /// release). It ORBITS her to face, in priority: the nearest enemy shot closing on her (even
    /// when an enemy is nearer), else the nearest enemy, else straight ahead — and, while it fires,
    /// the enemy it is about to shoot.
    ///
    /// INTERCEPT &amp; RE-FIRE (<see cref="ShotReflect"/>): every closing shot is claimed (latch held —
    /// it cannot hurt anyone). The one the slingshot faces is homed gently into the POUCH and
    /// caught there (quiet vanish); any other reaches her body and is absorbed there instead (dull
    /// white flash). Each caught shot is then RE-CREATED from the pouch — the weapon copy plays its
    /// own draw/hold/shoot keys — at the NEAREST living enemy (the arrival line reversed when none),
    /// via the same pure-data spawn the engine's own Set uses. A reflected shot stays latched (its own mask is the
    /// enemy-shot one); its DAMAGE is a pellet-style CollisionData entry planted the tick it reaches a live enemy
    /// (<see cref="ReflectedHits"/>: the weapon's attack scaled by dungeon tier, the shot's element, its statuses applied
    /// through <see cref="EnemyStatus"/>).
    ///
    /// THE SHIELD: enemies walk at and swing at the slingshot, not her (<see cref="ShieldRing"/>: each one's AI target
    /// pointer set to its own point on a ring of the slingshot's radius, MoveCheck2's body block widened to the ring), and
    /// their swings land on it (<see cref="MeleeHitWatch"/>): <see cref="ShieldHits"/> hits break it, the attack gauge shows
    /// its HP and refills over <see cref="CooldownSeconds"/> before it may return. The two cold EE patches that make the block
    /// distance a data word and aim every shot's player test at the pouch are <see cref="ShieldPatches"/>, armed at the main menu.
    ///
    /// LEVERS (see the RE doc): enemy shots are CSHOT_EFFECT sub-shots in the pack at
    /// *NowShotEffect; a sub-shot spawn is pure data (Set 0x1ADD60 decompiled — phase 1 skips the
    /// muzzle exactly as Set does for muzzle-less configs; the fresh shot reuses the free sub-shot's
    /// OWN frame objects, so nothing is deep-copied). The wings are <see cref="SlingshotProp"/>.
    /// This class keeps the loop (guard → shield, hits, the fire cycle), the attack gauge, the thread and the party regen
    /// (<see cref="DriveAngelGear"/>); the helper classes share its members through using static.
    /// </summary>
    internal static class AngelGear
    {
        internal static bool Enabled = true;

        private const string Tag = "[AngelGear] ";

        private const int  XiaoId = 1;

        // The monster shot-effect pack's layout: WeaponAddresses.ShotEffectPack.

        // ── tuning ──
        internal const float PouchHeight  = 7.5f;   // copy root height above her feet (8.5 read slightly high at 2x)
        internal const float PropAhead    = 12f;    // copy root this far out from her, along the orbit bearing
        private const int   FireWaitMax  = 20;     // ticks a caught shot waits for the sky to clear before firing anyway
        private const int    HitFadeTicks = 3;             // dispel: solid → gone in 0.15 s
        private const double CooldownSeconds = 4.0;       // the bar refills from 0 to full over this after a break
        // SHIELD HP ON THE ATTACK GAUGE: the bottom-left speed bar (float 0x1DC44C8, 0..100;
        // Xiao's shot needs 100 and zeroes it; a hit on her resets it to 100; MainDraw fills 128 px from it and
        // flashes at 100) shows the slingshot's HP: 5 hits, −20 each. The ISO's dun.bin patch (DunPatches)
        // makes Xiao's refill multiplier the MAILBOX word ShieldGaugeRate (pnach-seeded 1.5 while nobody owns
        // it): the app takes ownership (ShieldGaugeOwner = 1) only while the shield is up or broken, writing 0
        // to hold the bar and a small value so the ENGINE refills it over CooldownSeconds (no per-tick
        // writes); a full bar = the slingshot may respawn. Ownership is released otherwise (self-healing).
        private const long   GaugeRateWord = Mailbox.ShieldGaugeRate;
        private const long   GaugeAddr = 0x21DC44C8;
        private const int    ShieldHits = 5;
        private const float  GaugePerHit = 100f / ShieldHits;
        private const float  VanillaXiaoRefillMul = 1.5f;
        // A shot config's flag word (ShotEffectPack.CfgFlags): the low bits its element, the high byte its statuses — the
        // bits an enemy can take are these (0x100 freeze, 0x200 poison, 0x800 gooey); see EnemyStatus.ApplyShotStatus.
        private const float PullLength   = 2.5f;   // pouch draw travel, weapon units at x1 (authored 4.2)
        private const float PropScale    = 2f;     // giant factor for the slingshot copy (4 read too big)
        private const int   FadeInTicks  = 5;      // transparency → solid over 0.25 s (guard begins)
        private const int   FadeOutTicks = 10;     // solid → transparent over 0.5 s (guard released)
        // Fire-cycle timing (50 ms ticks): the authored draw is 10 frames (~3 ticks) — 4 keeps it
        // readable; hold at full draw; a short gap after the snap before the next volley.
        private const int   DrawTicks    = 5;      // 10 frames at KEY rate 0.7 ≈ 4.8 ticks: let the draw complete
        private const int   HoldTicks    = 3;
        private const int   ShootTicks   = 4;

        private const int  FastTickMs = 50, IdleTickMs = 250;

        private static Thread _thread;
        internal static float _alpha;                            // prop opacity 0..1
        private static bool  _jingled;                          // once per appearance
        internal static int   _pullTick = -1;                    // -1 idle; else ticks into the fire cycle
        private static int   _pendingWait;                      // ticks the head of the queue has waited to fire
        internal static bool  _dispelling;                       // hit → fast fade-out in progress
        private static DateTime _cooldownUntil;                 // no-PNACH fallback: no respawn before this
        private static int   _shieldHp;                         // hits left on the standing shield
        private static bool  _cooling;                          // broken: waiting for the bar to refill
        private static bool  _gaugeLive;                        // PNACH present → the gauge is ours to drive
        private static float _rateWritten = float.NaN;
        private static int   _ownerWritten = -1;
        private static bool  _live;                             // the loop's state is up (weapon live in a dungeon); one HardReset when it stops being

        /// <summary>Started once from the main menu (<see cref="MainMenuThread.ApplyNewChanges"/>): seeds the gauge multiplier
        /// to vanilla, then runs the reflector loop and the melee hit watch for the life of the app. Both idle until the Angel
        /// Gear (or Super Steve with its sphere) is live on a dungeon floor; the loop also retries the cold patches while not on one.</summary>
        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            try { Memory.WriteFloat(GaugeRateWord, VanillaXiaoRefillMul); Memory.WriteInt(Mailbox.ShieldGaugeOwner, 0); _rateWritten = VanillaXiaoRefillMul; _ownerWritten = 0; }   // vanilla until a shield stands
            catch (Exception e) { Console.WriteLine(Tag + "gauge seed failed: " + e.Message); }
            _thread = new Thread(Loop) { IsBackground = true, Name = "AngelGear" };
            _thread.Start();
            StartWatch();
        }

        /// <summary>The cold patches' arming entry points, kept on this class for the main-menu caller: the patches themselves
        /// are <see cref="ShieldPatches"/>.</summary>

        private static void Loop()
        {
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    if (!_blockArmed && !Player.InDungeonFloor()) ShieldPatches.ArmBlockPatch();
                    if (!_shotArmed && !Player.InDungeonFloor()) ShieldPatches.ArmShotPatch();
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

        private static float Wrap(float a)
        {
            const float twoPi = 2f * (float)Math.PI;
            while (a >  (float)Math.PI) a -= twoPi;
            while (a <= -(float)Math.PI) a += twoPi;
            return a;
        }

        /// <summary>Everything back to vanilla in one go: the ring, the gauge, the shot redirect, the prop, every queue and
        /// timer. Run once when the weapon stops being live and after a failed tick.</summary>
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

        // ─────────────────────────────────── attack gauge ───────────────────────────────────────

        /// <summary>Own the multiplier word while driving it (0 / slow refill); hand it back to the pnach's
        /// per-frame 1.5 seed otherwise, so the game is vanilla whenever the app is not actively shielding.</summary>
        private static void SetGaugeRate(float k)
        {
            int owner = k == VanillaXiaoRefillMul ? 0 : 1;
            if (owner == 1 && _ownerWritten != 1) { Memory.WriteInt(Mailbox.ShieldGaugeOwner, 1); _ownerWritten = 1; }
            if (k != _rateWritten) { Memory.WriteFloat(GaugeRateWord, k); _rateWritten = k; }
            if (owner == 0 && _ownerWritten != 0) { Memory.WriteInt(Mailbox.ShieldGaugeOwner, 0); _ownerWritten = 0; }
        }

        /// <summary>Multiplier that makes the engine's own refill — max(1, speed/30) per frame — fill the bar
        /// from 0 to 100 over CooldownSeconds (status halving/doubling ignored).</summary>
        private static float RefillMultiplier()
        {
            float speed = Memory.ReadShort(WeaponHave.BattleWeaponRecord + 8);
            float perFrame = Math.Max(1f, speed / 30f);
            return (float)(100.0 / (CooldownSeconds * 60.0 * perFrame));
        }

        // ─────────────────────────────── weapon thread and regen ────────────────────────────────

        /// <summary>Xiao's Angel Gear weapon thread (<see cref="WeaponThreads"/>): the party REGEN only — it runs while the
        /// weapon is equipped and hands every tick to <see cref="DriveAngelGear"/>, which owns the cadence, so a pause, a menu,
        /// a chest or a conversation only holds the interval, never restarts it. The reflector itself runs on the loop
        /// <see cref="Start"/> began at the main menu.</summary>
        public static void GuardianReflectorEffect()
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
