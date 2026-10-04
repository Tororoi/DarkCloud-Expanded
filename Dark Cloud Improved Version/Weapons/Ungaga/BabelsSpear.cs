using System;
using System.Collections.Generic;
using System.Threading;
using static Dark_Cloud_Improved_Version.BabelCopy;
using static Dark_Cloud_Improved_Version.BabelBeam;
using static Dark_Cloud_Improved_Version.BabelSpikes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Babel's Spear — "Curse of Babel": a five-second guard charge raises a giant copy of the spear (Super Steve's sphere: a
    /// statue of Super Steve; <see cref="BabelCopy"/>) point-up out of the ground under the locked-on enemy, following it while it rises
    /// until the tip strikes it: the enemy above it is struck and thrown off, and every enemy on the floor is confused (the shared
    /// <see cref="Confusion"/>) until the copy has fully faded after <see cref="SpearSeconds"/>. Once risen it turns on the spot and
    /// its spikes catch what touches it (<see cref="BabelSpikes"/>); the Dark Genie's beam marks the spot (<see cref="BabelBeam"/>);
    /// the wielder's weapon wears the copy's tint through the charge, the stand and the fade. The rise and the spin are the engine's
    /// own frames (the blade-fall and blade-spin caves). The helpers share this class's members through using static.</summary>
    internal static class BabelsSpear
    {
        private const string Tag = "[Babel] ";
        private const int    TickMs           = 50;
        private const int    GuardChargeMs    = 5000;   // hold the guard pose (GuardWatch.HoldPose: the Mirage's; Ungaga's and Xiao's alike) this long to summon (restarting whenever the pose is left); not again until the spear has fully faded
        internal const float SpearSeconds     = 20f;
        // The beam's KEY windows (c17_beem_s info.cfg) at ShockRate: rise 10–28, loop 28–48, vanish 48–70.
        internal const float ShockRate        = 0.1f;
        internal const int   ShockRise = 0, ShockLoop = 1, ShockVanish = 2;
        internal const float ShockRiseStart = 10f, ShockRiseEnd = 28f, ShockLoopStart = 28f, ShockLoopEnd = 48f, ShockVanishStart = 48f, ShockVanishEnd = 70f;
        private const float  EmergeStartFrame = 10f, EmergeEndFrame = 13f;                             // the rise clip's frames (from 10) the spear starts, and finishes, emerging at
        internal const float EmergeStartSeconds = (EmergeStartFrame - ShockRiseStart) / ShockRate / 60f;   // 0 s: with the effect
        internal const float EmergeSeconds      = (EmergeEndFrame - EmergeStartFrame) / ShockRate / 60f;   // 0.5 s (30 frames)
        // Its opacity is the ISO's: the zibaku_f copy's palette alphas are halved in the bake (BorrowedShotBakes).
        internal static float BlockRadius => F.BlockRadius;   // the risen copy as a solid column (the spear-block caves: enemies, the player, enemy shots)
        internal const float VanishSeconds    = (ShockVanishEnd - ShockVanishStart) / ShockRate / 60f;  // 3.67 s: the vanish clip, after the spear's time
        private const float  FadeStartFrame   = 60f;                                                    // the vanish clip's frame the spear starts fading at
        private const float  FadeStartSeconds = (FadeStartFrame - ShockVanishStart) / ShockRate / 60f;  // 2.0 s into the vanish
        private const float  FadeSeconds      = (ShockVanishEnd - FadeStartFrame) / ShockRate / 60f;    // 1.67 s: gone as the vanish clip ends
        internal static float ShockScale => F.ShockScale;   // the beam's size: the spear's as authored (a sideways scale to the area's edge read as size, not range)
        private const int    ShockTemplate    = 5;       // a stock config's shape; the name and motions are replaced (Big Bang's choice)
        private const string ShockName        = "zibaku_f", ShockDir = BorrowedShots.EffectDir;   // the beam's cyan/blue copy on the dead zibaku_f name (BorrowedShotBakes)
        internal const float SpinDegPerSec    = 240f;
        internal static bool _xiao;                           // Super Steve's sphere is the wielder (latched per thread)
        private const float  Buried           = 4f;     // how far below the ground the tip starts
        internal const int   ShellLifeTicks   = 3;      // ticks a planted hit stays before it is withdrawn (EnemyHit leaves the engine its pool reserve)
        internal const float StrikeRadius     = 6f;
        internal const float KickDecay        = 0.12f;  // a kick's fade when none is named (the strike's is the Baselard's)
        private const float  NoLockReach      = 60f;    // no lock: the nearest enemy within this, else the copy rises ahead of the wielder
        private const float  AheadDistance    = 15f;
        // Light blue, as the unit's ambient add (scene ambient ≈ 128 is neutral): kept dim.
        private const float  TintR = 12f, TintG = 50f, TintB = 84f;
        internal const float SpearTint = 50f;           // the copy's own ambient add, neutral grey (brighter, not coloured)
        private static readonly float[] WeaponTint = { SpearTint, SpearTint, SpearTint };   // the wielder's weapon wears the copy's tint
        private const string ModelCode = "c10w10";      // Babel's Spear
        private const uint   BladeFrame = 0x00303177;   // 'w','1','0',NUL — its mesh frame, w10 (the spears' frames are not named alike: c10w08__m, w09__m, w10)

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the Dark Genie's shockwave, whenever
        /// Curse of Babel is wielded (<see cref="Wielded"/>). Handed to BorrowedShots.Start as a provider. Every phase radius is zeroed: the effect is
        /// the visual only. The config's MUZZLE motion is the VANISH clip: Step__12CSHOT_EFFECT retires a phase-0 sub-shot
        /// the frame its cursor sits within one frame below the muzzle motion's END, whichever clip is playing; with the vanish
        /// (end 70) declared, the rise and the loop (frames 10–50) never reach the window, and the vanish ends the sub-shot by
        /// itself when it gets there.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded()) return null;
            _shock ??= BorrowedShots.VisualOnly(ShockTemplate, ShockName, muzzleMotion: ShockVanish, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: ShockDir,
                                                instance: ShotEffectPack.CharaMainEffectCrash);
            return _shock;
        }

        private static readonly Random _rng = new();
        private static bool     _up;               // the spear stands
        internal static bool    _riseStarted, _risen, _struck;
        internal static int     _target = -1;      // the enemy the spear rose under (−1 = none): struck when the tip reaches its hit sphere
        private static DateTime _summoned, _confusionEnd;
        internal static float   _sx, _sy, _ground;
        private static bool _guardLatched; private static DateTime _guardSince;

        /// <summary>Curse of Babel's wielder: Ungaga with Babel's Spear, or Xiao with Super Steve and a Babel's Spear sphere.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.babelsspear);

        public static void CurseOfBabelEffect()
        {
            int ch = Player.CurrentCharacterNum();
            _xiao = ch == Player.XiaoId;
            F = _xiao ? StatueForm : SpearForm;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"curse of Babel: hold the guard {GuardChargeMs / 1000} s → the copy rises under the target for {SpearSeconds:F0} s; every enemy on the floor confused");
            try
            {
                // Ends the moment the active character changes, a menu open or not (the copy's slot was cloned from this character's
                // objects, which an ally switch reloads under it; a sphere keeps Wielded() true across it).
                while (Wielded() && Player.InDungeonFloor() && Player.CurrentCharacterNum() == ch)
                {
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Charge();
                        if (_up) DriveSpear();
                        DriveConfusion();
                    }
                    for (int t = 0; t < TickMs && Player.CurrentCharacterNum() == ch; t += 10) Thread.Sleep(10);   // the tick, cut short by a switch
                }
            }
            finally { End(); }
        }

        // ── the guard charge ──
        private static void Charge()
        {
            var (guarding, inPose) = GuardWatch.HoldPose();
            if (!guarding) { _guardLatched = false; ChargeOff(); return; }                 // released: a new hold can charge again
            if (_up || _guardLatched) { _guardSince = default; return; }                   // cooling down (the spear still stands or fades), or this hold already summoned
            if (!inPose) { ChargeOff(); return; }                                          // out of the guard pose (a swing, a hit, the raise): the charge starts again
            if (_guardSince == default) _guardSince = GameClock.Now;
            double held = (GameClock.Now - _guardSince).TotalMilliseconds;
            SetTint((float)Math.Min(1.0, held / GuardChargeMs));                         // the weapon takes the copy's tint as it charges
            if (held < GuardChargeMs) return;
            _guardLatched = true;
            Player.FlashChargeComplete();
            Summon();
        }

        /// <summary>A charge dropped: the timer cleared and the weapon's tint off (left alone while the spear is up — it is the spear's).</summary>
        private static void ChargeOff()
        {
            if (_guardSince != default && !_up) BladeTint.Clear();
            _guardSince = default;
        }

        /// <summary>The wielder's weapon tinted toward the copy's own tint: Ungaga's spear, or Super Steve's whole slingshot.</summary>
        private static void SetTint(float k) => WielderTint.Set(k, ModelCode, BladeFrame, WeaponTint, _xiao);

        // ── the spear ──
        private static void Summon()
        {
            if (_up) TakeDown();
            int target = Target(out float x, out float y, out float ground);
            _sx = x; _sy = y; _ground = ground; _riseStarted = _risen = _struck = false; _target = target; _fadeK = 1f; _spinTick = -1;
            if (!CopySpawn()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no {F.Name} copy (slot or cave busy)"); return; }
            Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            CopyPlace(_sx, RootHeight(0f), _sy);                                               // buried, still: the caves move it from here
            CopyAlpha(1f);
            _up = true; _summoned = GameClock.Now; _confusionEnd = _summoned.AddSeconds(SpearSeconds + VanishSeconds);   // the confusion outlasts the spear by the vanish: gone when it has fully faded
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {F.Name} rises at ({_sx:F0},{_sy:F0}) ground {_ground:F0}" + (target >= 0 ? $" under enemy slot {target}" : " ahead of the wielder") + $"; target redirect {(TargetRedirectCaves.Armed ? "armed" : "NOT ARMED — confusion cannot steer")}");
            Confusion.Configure(null, new[] { TintR, TintG, TintB }, provokes: true, Tag);   // the whole floor: no area; the hit turn on their attacker
            if (target >= 0) Confuse(target);                                                  // the strike waits for the tip to reach it (TipStrike)
            ConfuseAll();
            ShockStart();
        }

        /// <summary>Every live enemy on the floor not yet confused (one that spawns while the spear stands too): confused until the spear goes.</summary>
        private static void ConfuseAll()
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                if (!Confusion.IsConfused(s) && Enemies.IsLive(s)) Confuse(s);
        }

        /// <summary>The locked-on enemy, else the nearest live one within reach; its root's position and ground height out. −1
        /// with a spot ahead of the wielder when there is none.</summary>
        private static int Target(out float x, out float y, out float ground)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            int slot = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (slot < 0 || slot >= EnemyAddresses.FloorSlots.Count || !Enemies.IsLive(slot))
            {
                slot = -1; float best = NoLockReach * NoLockReach;
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                {
                    if (!Enemies.IsLive(s)) continue;
                    long p = EnemyAddresses.CharObjects.PosAddr(s);
                    float dx = Memory.ReadFloat(p) - px, dy = Memory.ReadFloat(p + 8) - py, d = dx * dx + dy * dy;
                    if (d < best) { best = d; slot = s; }
                }
            }
            if (slot >= 0)
            {
                long p = EnemyAddresses.CharObjects.PosAddr(slot);
                x = Memory.ReadFloat(p); ground = Memory.ReadFloat(p + 4); y = Memory.ReadFloat(p + 8);
                return slot;
            }
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            x = px + (float)Math.Sin(yaw) * AheadDistance; y = py + (float)Math.Cos(yaw) * AheadDistance; ground = ph;
            return -1;
        }

        /// <summary>The copy's root height for an emergence fraction <paramref name="t"/> (0 buried, 1 risen): the tip sits
        /// TipZ·Scale above the root.</summary>
        internal static float RootHeight(float t) => _ground - TipZ * Scale + (-Buried + (Exposed + Buried) * t);

        private static void DriveSpear()
        {
            if (!CopyMaintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); _up = false; return; }
            double age = (GameClock.Now - _summoned).TotalSeconds;
            if (age >= SpearSeconds + VanishSeconds) { TakeDown(); return; }
            ShockDrive(age);
            if (!_riseStarted && age >= EmergeStartSeconds)
            {   // the rise, on the engine's frames: the blade-fall cave's ease from buried to risen over the emergence
                VerticalDrive.StartEase(RootHeight(0f), RootHeight(1f), EmergeSeconds * 60f);
                _riseStarted = true;
            }
            if (_riseStarted && !_struck && !_risen) Follow();
            if (_riseStarted && !_struck) TipStrike();
            if (!_risen && age >= EmergeStartSeconds + EmergeSeconds)
            {   // risen: the integrator off (its velocity is at zero), the exact top written once, the spin begins, and it is solid
                Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
                CopySetHeight(RootHeight(1f));
                Memory.WriteFloat(CodeCaves.BladeSpin, (float)(SpinDegPerSec * Math.PI / 180.0 / 60.0));
                SpearBlock.Arm(_sx, _ground, _sy, BlockRadius, _ground + Exposed);                   // enemy shots stop below the tip
                _risen = true;
            }
            double fadeAt = SpearSeconds + FadeStartSeconds;                                                  // its time up, the vanish plays; from its frame 60 the spear fades out with it
            _fadeK = age < fadeAt ? 1f : (float)Math.Max(0.0, 1.0 - (age - fadeAt) / FadeSeconds);
            CopyAlpha(_fadeK);                                                                                // the confused enemies' tint fades with it (DriveConfusion)
            if (_risen) SpinContacts(age);
            ConfuseAll();                                                                                    // through the fade too: the confusion lasts until the spear has fully faded
        }

        private static void TakeDown()
        {
            if (!_up) return;
            ShockStop();
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
            SpearBlock.Disarm();                                                                    // passable again
            CopyDespawn();
            ConfusionStars.Fade = 1f;
            BladeTint.Clear();                                                                       // the weapon's tint goes with the copy
            _up = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the spear sinks away");
        }

        /// <summary>While it rises and has not struck, the copy keeps under its target: the slot's x/y (and the beam's spot) set to
        /// the target's root each tick, its height the rise cave's. Gone or dead, it stays where it is.</summary>
        private static void Follow()
        {
            if (_target < 0 || !Enemies.IsLive(_target)) return;
            long p = EnemyAddresses.CharObjects.PosAddr(_target);
            _sx = Memory.ReadFloat(p); _sy = Memory.ReadFloat(p + 8);
            CopySetXY(_sx, _sy);
        }

        private static float _fadeK = 1f;          // the spear's visibility 0..1, which the confused enemies' tint follows

        // ── confusion (the shared Confusion) ──
        private static void Confuse(int slot)
        {   // a dormant enemy (a shut chest mimic, one not yet activated) joins when it wakes (ConfuseAll then); bosses never, as the ability's roll
            if (Confusion.IsActive(slot) && !Enemies.IsBoss(slot)) Confusion.Confuse(slot, _confusionEnd);
        }

        private static void DriveConfusion()
        {
            Confusion.MoveArea(_sx, _sy);
            Confusion.TintScale = _fadeK;                                                    // the confused enemies' tint fades with the spear
            if (_up) SetTint(_fadeK);                                                        // …and the weapon's with the copy
            ConfusionStars.Fade = _up ? _fadeK : 1f;                                         // the stars (ConfuseAbility draws them) fade with the spear
            Confusion.Tick();
            RetireShells();
        }

        private static void End()
        {
            TakeDown();
            _shells.WithdrawAll();
            Confusion.End();
            ConfusionStars.Fade = 1f;
            BladeTint.Clear();
            _guardLatched = false; _guardSince = default;
        }
    }
}
