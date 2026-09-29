using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Babel's Spear — "Curse of Babel": a five-second guard charge raises a giant copy of the spear out of the ground
    /// under the locked-on enemy, point up, most of it still buried. The enemy above it is struck and thrown off it, and it
    /// and every enemy within <see cref="ConfusionRadius"/> of the spear are CONFUSED for as long as the spear stands
    /// (<see cref="SpearSeconds"/> plus its fade: the confusion ends when the spear has fully faded), as is any enemy that comes within that radius while it stands (Ungaga locks on from twice as
    /// far while the spear is his, <see cref="ReachFactor"/>): tinted a light blue, each
    /// goes after the NEAREST thing inside the area — another enemy, or the player while the player is in it — and its swings
    /// hurt other enemies; with nothing in the area to go after it wanders. Once risen the spear turns slowly on the spot, and
    /// fades out over <see cref="FadeSeconds"/> when its time is up.
    ///
    /// The pieces, all data:
    ///  · the spear is <see cref="BladeProp"/>'s copy of the equipped weapon (the same engine-drawn copy Big Bang hangs over
    ///    its target), baked to point up and placed at the target's feet with the root sunk so only <see cref="Exposed"/>
    ///    units of the tip show (the c10w10 mesh runs −9.6 … +16.0 along its axis, the tip at 16). Its RISE and its SPIN are
    ///    the engine's own frames, not this thread's ticks (which stepped it at 20 Hz): the blade-fall cave (Big Bang's) integrates
    ///    the slot's height with an upward velocity and a deceleration that reach zero together at the top — fast off the mark,
    ///    slow to settle — between effect frames <see cref="EmergeStartFrame"/> and <see cref="EmergeEndFrame"/>; the blade-spin
    ///    cave then adds <see cref="SpinDegPerSec"/> a second to its yaw. Faded at the end (the copy's opacity — taking the slot
    ///    down while it draws left a frame of it stretched);
    ///  · the strike is one player-hit sphere (CollisionPool.PlayerHitEntry) at the target's body, the weapon's attack, its
    ///    kick words pointing away from the spear;
    ///  · confusion rides the Mirage's per-slot target-pointer table (CodeCaves.PtrTable, read by the cold-hosted
    ///    _GET_POSITION / _GET_DISTANCE): a confused slot's entry points at the LIVE position of its target, chosen every tick
    ///    as the nearest candidate in the confusion area — another live enemy's CCharacter position, or the player global while
    ///    the player is inside the area — or, with no candidate, at the slot's own wander quadword (CodeCaves.BabelWander), a
    ///    random spot near it renewed every <see cref="WanderSeconds"/> or once reached. The table is owned here while any slot
    ///    is confused (Mirage's loop stands down, <see cref="OwnsTable"/>);
    ///  · friendly fire: each tick, every OPEN attack entry a confused enemy has planted (pool owner = slot·5 + 200, gate words
    ///    equal) is tested against every other live enemy's body spheres; on contact a small hit entry is planted on the
    ///    victim's body — the attack's damage, reaction and kick, owner −1 (the engine then takes damage − defence, no weapon
    ///    stats, no WHP drain, no kill credit) — one per attacker-victim pair per <see cref="ContactCooldown"/>. The attacker's
    ///    own entry is never widened: its victim mask stays 1, so its own body (which its swing sphere overlaps) is never
    ///    struck, and the planted hit sits on the victim, out of the attacker's reach;
    ///  · the tint is the unit's ambient add (CCharacter +0xCE0), re-asserted each tick, cleared at the end;
    ///  · the confusion AREA is drawn by the Dark Genie's small beam (c17_beem_s), recoloured cyan/blue by the ISO patch onto the
    ///    dead dun/effect/zibaku_f name (the genie keeps its pink), borrowed the way Big Bang
    ///    borrows explosion.chr (<see cref="WantedShot"/>) — but into the SECOND main-character effect instance (Ruby's and
    ///    Osmond's, idle for Ungaga — and stepped and drawn beside the live one only while CodeCaves.SecondEffectLive is set, by
    ///    the ISO's second-effect caves: the dungeon loop itself steps and draws the LIVE instance alone), so Ungaga's own
    ///    charge-attack effect (c10a_ex, the first instance) is left in place — and played as a
    ///    sub-shot the spear drives: its KEY 0 (rise, frames 10–28) as the spear emerges — fully up as the clip reaches frame
    ///    <see cref="EmergeAtFrame"/> —, KEY 1 (loop, 28–48) while it stands, KEY 2 (vanish, 48–70) while it fades — all at
    ///    <see cref="ShockRate"/> frames a tick, which is what the spear's own timing is derived from. Drawn at its authored size (<see cref="ShockScale"/>): it marks the spot, not the
    ///    area's edge.</summary>
    internal static class BabelsSpear
    {
        private const string Tag = "[Babel] ";
        private const int    TickMs           = 50;
        private const int    GuardChargeMs    = 1000;   // hold the guard this long to summon; not again until the spear has fully faded
        private const int    GuardLoopMotion  = 9, GuardMoveMotion = 33;   // Ungaga's guard-hold poses (the Mirage's)
        private const float  SpearSeconds     = 20f;
        // The beam's KEY windows (c17_beem_s info.cfg) at ShockRate: rise 10–28, loop 28–48, vanish 48–70.
        private const float  ShockRate        = 0.1f;
        private const int    ShockRise = 0, ShockLoop = 1, ShockVanish = 2;
        private const float  ShockRiseStart = 10f, ShockRiseEnd = 28f, ShockLoopStart = 28f, ShockLoopEnd = 48f, ShockVanishStart = 48f, ShockVanishEnd = 70f;
        private const float  EmergeStartFrame = 10f, EmergeEndFrame = 13f;                             // the rise clip's frames (from 10) the spear starts, and finishes, emerging at
        private const float  EmergeStartSeconds = (EmergeStartFrame - ShockRiseStart) / ShockRate / 60f;   // 0 s: with the effect
        private const float  EmergeSeconds      = (EmergeEndFrame - EmergeStartFrame) / ShockRate / 60f;   // 0.5 s (30 frames)
        // Its opacity is the ISO's: the zibaku_f copy's palette alphas are halved in the bake (BorrowedShotBakes) — the sub-shot's
        // opacity word did not reach a shot effect's draw.
        private const float  BlockRadius      = 8f;      // the risen spear as a solid column for enemies (the shaft is ~2 wide at 4×): the spear-block cave
        private const float  VanishSeconds    = (ShockVanishEnd - ShockVanishStart) / ShockRate / 60f;  // 3.67 s: the vanish clip, after the spear's time
        private const float  FadeStartFrame   = 60f;                                                    // the vanish clip's frame the spear starts fading at
        private const float  FadeStartSeconds = (FadeStartFrame - ShockVanishStart) / ShockRate / 60f;  // 2.0 s into the vanish
        private const float  FadeSeconds      = (ShockVanishEnd - FadeStartFrame) / ShockRate / 60f;    // 1.67 s: gone as the vanish clip ends
        private const float  ShockScale       = 1f;      // as authored (a sideways scale to the area's edge read as size, not range)
        // Lock-on: Ungaga's entry in the lock-on factor table (the same data the Cross Hinder and the Flamingo drive), ×2 while the spear is his.
        private const float ReachFactor = 2.0f;
        private static readonly long  ReachEntry = CodeCaves.LockOnFactorTable + Player.UngagaId * 4;
        private static readonly float Reach      = CodeCaves.LockOnFactorVanilla[Player.UngagaId] * ReachFactor;
        private static bool _reachHeld;
        private const int    ShockTemplate    = 5;       // a stock config's shape; the name and motions are replaced (Big Bang's choice)
        private const string ShockName        = "zibaku_f", ShockDir = BorrowedShots.EffectDir;   // the beam's cyan/blue copy on the dead zibaku_f name (BorrowedShotBakes)
        private const float  SpinDegPerSec    = 240f;
        private const float  Scale            = 5f;     // the copy's size; the spear is 25.6 long unscaled
        private const float  TipZ             = 16.0f;  // the mesh's tip along its axis (c10w10: −9.6 … 16.0)
        private const float  ExposedModelZ    = 10f;    // the length of the tip that stands above the ground once risen, in the MODEL's units (z 6 … 16)
        private const float  Exposed          = ExposedModelZ * Scale;   // …and in the world's, at the copy's size
        private const float  Buried           = 4f;     // how far below the ground the tip starts
        private const float  ConfusionRadius  = 300f;
        private const float  WanderSeconds    = 4f;     // a wandering enemy's spot renewed this often…
        private const float  WanderRange      = 40f;    // …within this of where it stands…
        private const float  WanderReached    = 8f;     // …or once it has got this close to it
        private const float  ContactCooldown  = 0.5f;   // one friendly-fire hit per attacker-victim pair this often
        private const float  ContactRadius    = 1f;     // the planted hit's own radius: the victim's sphere does the reaching
        private const int    ShellLifeTicks   = 3;      // ticks a planted hit stays before it is withdrawn
        private const int    ShellPoolReserve = 16;     // free pool entries always left to the engine
        private const float  StrikeRadius     = 6f;
        private const float  KickStrength     = 2.475f, KickDecay = 0.12f;   // ≈ 25 units, away from the spear
        private const float  NoLockReach      = 60f;    // no lock: the nearest enemy within this, else the spear rises ahead of Ungaga
        private const float  AheadDistance    = 15f;
        // Light blue, as the unit's ambient add (scene ambient ≈ 128 is neutral): kept dim.
        private const float  TintR = 12f, TintG = 50f, TintB = 84f;
        private const float  SpearTint = 50f;           // the copy's own ambient add, neutral grey (brighter, not coloured)

        /// <summary>True while any enemy is confused: the per-slot target table is this ability's (Mirage's loop leaves it).</summary>
        internal static bool OwnsTable { get; private set; }

        private static BorrowedEffect _shock;
        private static int _shockSlot = -1;   // the sub-shot playing the shockwave (−1 = none)
        private static int _shockKey = -1;    // the KEY it is on
        private static int _shockRearms;      // DIAGNOSTIC: times the engine retired the sub-shot and it was re-armed
        private static DateTime _shockReport;

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the Dark Genie's shockwave, whenever
        /// Babel's Spear is in Ungaga's hands. Handed to BorrowedShots.Start as a provider. Every phase radius is zeroed: the effect is
        /// the visual only. The config's MUZZLE motion is the VANISH clip: Step__12CSHOT_EFFECT retires a phase-0 sub-shot
        /// the frame its cursor sits within one frame below the muzzle motion's END, whichever clip is playing — with KEY 0
        /// (end 40) there, the loop clip's own first frame killed it. With the vanish (end 70) declared, the rise and the loop
        /// (frames 10–50) never reach the window, and the vanish ends the sub-shot by itself when it gets there.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.UngagaId) return null;
            if (Player.Weapon.GetCurrentWeaponId() != Items.babelsspear) return null;
            if (_shock == null)
            {
                _shock = BorrowedShots.CustomConfig(ShockTemplate, ShockName, muzzleMotion: ShockVanish, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: ShockDir,
                                                    instance: ShotEffectPack.CharaMainEffectCrash);
                if (_shock == null) return null;
                for (int phase = 0; phase < 4; phase++) BorrowedShots.SetPhaseRadius(_shock, phase, 0f);
            }
            return _shock;
        }

        private static readonly Random _rng = new();
        private static bool     _up;               // the spear stands
        private static bool     _riseStarted, _risen, _struck;
        private static int      _target = -1;      // the enemy the spear rose under (−1 = none): struck when the tip reaches its hit sphere
        private static DateTime _summoned, _confusionEnd;
        private static float    _sx, _sy, _ground;
        private static readonly DateTime[] _confusedUntil = new DateTime[EnemyAddresses.FloorSlots.Count];
        private static readonly int[]      _victim        = new int[EnemyAddresses.FloorSlots.Count];   // −1 = the player, −2 = wandering
        private static readonly DateTime[] _wanderSet     = new DateTime[EnemyAddresses.FloorSlots.Count];
        private static readonly DateTime[,] _lastContact  = new DateTime[EnemyAddresses.FloorSlots.Count, EnemyAddresses.FloorSlots.Count];
        private static readonly List<(int idx, int ticks)> _shells = new();
        private static bool _guardLatched; private static DateTime _guardSince;
        private static DateTime _lastReport;   // DIAGNOSTIC: the confused enemies' state, once a second

        public static void CurseOfBabelEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"curse of Babel: hold the guard {GuardChargeMs / 1000} s → the spear rises under the target for {SpearSeconds:F0} s; confusion within {ConfusionRadius:F0}");
            for (int s = 0; s < _victim.Length; s++) _victim[s] = -3;
            try
            {
                while (Player.CurrentCharacterNum() == Player.UngagaId && Player.Weapon.GetCurrentWeaponId() == Items.babelsspear && Player.InDungeonFloor())
                {
                    HoldReach();
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Charge();
                        if (_up) DriveSpear();
                        DriveConfusion();
                    }
                    Thread.Sleep(TickMs);
                }
            }
            finally { End(); }
        }

        // ── the guard charge ──
        private static void Charge()
        {
            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int  mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool inPose = guarding && (mid == GuardLoopMotion || mid == GuardMoveMotion);
            if (!guarding) { _guardLatched = false; _guardSince = default; return; }   // released: a new hold can charge again
            if (!inPose || _guardLatched) return;
            if (_up) { _guardSince = default; return; }                                    // cooling down: the spear still stands or fades
            if (_guardSince == default) { _guardSince = GameClock.Now; return; }
            if ((GameClock.Now - _guardSince).TotalMilliseconds < GuardChargeMs) return;
            _guardLatched = true;
            Player.FlashChargeComplete();
            Summon();
        }

        // ── the spear ──
        private static void Summon()
        {
            if (_up) TakeDown();
            int target = Target(out float x, out float y, out float ground);
            _sx = x; _sy = y; _ground = ground; _riseStarted = _risen = _struck = false; _target = target; _fadeK = 1f; _spinTick = -1;
            if (!BladeProp.Spawn(Scale, 0, pointDown: false, pointUp: true)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no spear copy (slot or cave busy)"); return; }
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            BladeProp.Place(_sx, RootHeight(0f), _sy, 0f);                                    // buried, still: the caves move it from here
            BladeProp.Tint(SpearTint, SpearTint, SpearTint);
            BladeProp.Alpha(1f);
            _up = true; _summoned = GameClock.Now; _confusionEnd = _summoned.AddSeconds(SpearSeconds + VanishSeconds);   // the confusion outlasts the spear by the vanish: gone when it has fully faded
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the spear rises at ({_sx:F0},{_sy:F0}) ground {_ground:F0}" + (target >= 0 ? $" under enemy slot {target}" : " ahead of Ungaga") + $"; target redirect {(Mirage.Armed ? "armed" : "NOT ARMED — confusion cannot steer")}");
            if (target >= 0) Confuse(target);                                                  // the strike waits for the tip to reach it (TipStrike)
            ConfuseWithinRadius();
            ShockStart();
        }

        // ── the shockwave: the confusion area drawn ──
        private static void ShockStart()
        {
            _shockSlot = -1; _shockKey = -1; _shockRearms = 0;
            if (_shock == null || !BorrowedShots.Entered(_shock)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shockwave not entered on this floor — the area goes undrawn"); return; }
            if (!BorrowedShots.Burst(_shock, _sx, _ground, _sy, 0, 1f)) return;
            _shockSlot = Memory.ReadInt(_shock.Instance + ShotEffectPack.OffLastIdx);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                                    // the second instance stepped and drawn beside the live one (the ISO's second-effect caves)
            ShockDrive(0);
        }

        /// <summary>The sub-shot each tick: alive, at the spear, scaled to the area, on the KEY the spear calls for at ShockRate.
        /// The rise hands over to the loop and the loop rewinds to its first frame <see cref="ClipLead"/> frames before the
        /// clip's end (the cursor parks a fraction short of it and reads finished); the vanish starts when the spear's time is
        /// up and, being the config's muzzle motion, ends the sub-shot through the engine at its last frame. A retire before
        /// that (a swap, a reload) is re-armed.</summary>
        private const float ClipLead = 0.5f;   // frames before a KEY's end to move on: a tick's advance (0.3 at ShockRate) plus the step the cursor parks short by
        private static void ShockDrive(double age)
        {
            if (_shockSlot < 0 || _shock == null || !Player.CheckDunIsWalkingMode()) return;
            long inst = _shock.Instance, obj = inst + ShotEffectPack.OffObj + _shockSlot * ShotEffectPack.ObjStride;
            bool alive = Memory.ReadUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2) != 0;
            float frame = Memory.ReadFloat(obj + ShotEffectPack.ObjFrame);
            int status = Memory.ReadInt(obj + CharacterMotion.MotionStatusOffset);
            bool riseDone = _shockKey == ShockRise && (status == 3 || frame >= ShockRiseEnd - ClipLead);
            int key = age >= SpearSeconds ? ShockVanish : (_shockKey == ShockRise && !riseDone) || _shockKey < 0 ? ShockRise : ShockLoop;
            float start = key == ShockRise ? ShockRiseStart : key == ShockLoop ? ShockLoopStart : ShockVanishStart;
            float end   = key == ShockRise ? ShockRiseEnd   : key == ShockLoop ? ShockLoopEnd   : ShockVanishEnd;
            bool restart = key != _shockKey || (key == ShockLoop && (status == 3 || frame >= end - ClipLead || frame < start - 1));
            if ((GameClock.Now - _shockReport).TotalMilliseconds >= 250)
            {   // DIAGNOSTIC
                _shockReport = GameClock.Now;
                uint root = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                string rootName = Memory.IsValidGuest(root) ? System.Text.Encoding.ASCII.GetString(Memory.ReadBytesBatch(Memory.ToMmu(root) + CFrameVu1.Name, 12) ?? new byte[0]).TrimEnd('\0') : "none";
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shock #{_shockSlot} age {age:F1}: alive {alive}, phase {Memory.ReadUShort(inst + ShotEffectPack.OffPhase + _shockSlot * 2)}, key {key} (held {_shockKey}), frame {frame:F1}, motId {Memory.ReadInt(obj + ShotEffectPack.ObjMotId)}, flags {Memory.ReadInt(obj + ShotEffectPack.ObjMotFlag)}, status {Memory.ReadInt(obj + CharacterMotion.MotionStatusOffset)}, spd {Memory.ReadFloat(obj + ShotEffectPack.ObjMotSpd):F2}, scale ({Memory.ReadFloat(obj + CCharacter.CharScale):F1},{Memory.ReadFloat(obj + CCharacter.CharScale + 4):F1},{Memory.ReadFloat(obj + CCharacter.CharScale + 8):F1}), pos ({Memory.ReadFloat(obj + ShotEffectPack.ObjPos):F0},{Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4):F0},{Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8):F0}), root `{rootName}`, re-arms {_shockRearms}");
            }
            if (age >= SpearSeconds + VanishSeconds || (!alive && key == ShockVanish)) { if (alive) Memory.WriteUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2, 0); _shockSlot = -1; Memory.WriteInt(CodeCaves.SecondEffectLive, 0); return; }   // the vanish ran out: the engine ended it
            if (!alive) { Memory.WriteUShort(inst + ShotEffectPack.OffPhase + _shockSlot * 2, 0); Memory.WriteUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2, 1); restart = true; _shockRearms++; }
            if (restart)
            {
                Memory.WriteInt  (obj + ShotEffectPack.ObjMotId, key);
                Memory.WriteInt  (obj + ShotEffectPack.ObjMotFlag, 6);
                Memory.WriteFloat(obj + ShotEffectPack.ObjFrame, start);
                _shockKey = key;
            }
            Memory.WriteFloat(obj + ShotEffectPack.ObjMotSpd, ShockRate);                              // an absolute rate: 0.2 frames a tick
            Memory.WriteVec3 (obj + ShotEffectPack.ObjPos, _sx, _ground, _sy);
            Memory.WriteVec3 (obj + CCharacter.CharScale, ShockScale, ShockScale, ShockScale);
        }

        private static void ShockStop()
        {
            if (_shockSlot < 0 || _shock == null) return;
            Memory.WriteUShort(_shock.Instance + ShotEffectPack.OffActive + _shockSlot * 2, 0);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _shockSlot = -1; _shockKey = -1;
        }

        /// <summary>Every live enemy within ConfusionRadius of the standing spear not yet confused: confused until the spear goes.</summary>
        private static void ConfuseWithinRadius()
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (_confusedUntil[s] != default || !Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - _sx, dy = Memory.ReadFloat(p + 8) - _sy;
                if (dx * dx + dy * dy > ConfusionRadius * ConfusionRadius) continue;
                Confuse(s);
            }
        }

        /// <summary>The locked-on enemy, else the nearest live one within reach; its position and ground height out. −1 with
        /// a spot ahead of Ungaga when there is none.</summary>
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
        private static float RootHeight(float t) => _ground - TipZ * Scale + (-Buried + (Exposed + Buried) * t);

        private static void DriveSpear()
        {
            if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); _up = false; return; }
            double age = (GameClock.Now - _summoned).TotalSeconds;
            if (age >= SpearSeconds + VanishSeconds) { TakeDown(); return; }
            ShockDrive(age);
            if (!_riseStarted && age >= EmergeStartSeconds)
            {   // the rise, on the engine's frames: the blade-fall cave steps y −= vy, vy += g each frame. An upward velocity v0
                // (negative in its sign) and a deceleration g with v0 = g·T reach zero together after T frames, having covered
                // D = v0·T/2 — the ease the brief asks for. Its stop test (y ≤ stop) is disarmed with a stop far below.
                float frames = EmergeSeconds * 60f, d = RootHeight(1f) - RootHeight(0f), v0 = 2f * d / frames, g = v0 / frames;
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, RootHeight(0f));
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, -v0);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, g);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, -1e9f);
                Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFalling);
                _riseStarted = true;
            }
            if (_riseStarted && !_struck) TipStrike();
            if (!_risen && age >= EmergeStartSeconds + EmergeSeconds)
            {   // risen: the integrator off (its velocity is at zero), the exact top written once, the spin begins, and it is solid
                Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
                BladeProp.SetHeight(RootHeight(1f));
                Memory.WriteFloat(CodeCaves.BladeSpin, (float)(SpinDegPerSec * Math.PI / 180.0 / 60.0));
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, _sx);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, _ground);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, _sy);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, BlockRadius);
                Memory.WriteInt  (CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 1);
                _risen = true;
            }
            double fadeAt = SpearSeconds + FadeStartSeconds;                                                  // its time up, the vanish plays; from its frame 60 the spear fades out with it
            _fadeK = age < fadeAt ? 1f : (float)Math.Max(0.0, 1.0 - (age - fadeAt) / FadeSeconds);
            BladeProp.Alpha(_fadeK);                                                                          // the confused enemies' tint fades with it (DriveConfusion)
            if (_risen) SpinContacts(age);
            ConfuseWithinRadius();                                                                           // through the fade too: the confusion lasts until the spear has fully faded
        }

        private static void TakeDown()
        {
            if (!_up) return;
            ShockStop();
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);                     // passable again
            BladeProp.Despawn();
            _up = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the spear sinks away");
        }

        /// <summary>The strike, the moment the rising tip reaches the target's hit sphere: the tip's height (the blade-fall cave's
        /// running Y plus the tip's reach) against the lowest active body sphere's underside, with the sphere still over the
        /// spear (its edge within StrikeRadius of the axis). A target that has left, or is gone, is never struck.</summary>
        private static void TipStrike()
        {
            if (_target < 0) { _struck = true; return; }
            if (!Enemies.IsLive(_target)) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {_target} gone before the tip reached it — no strike"); return; }
            float tip = (_risen ? RootHeight(1f) : Memory.ReadFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY)) + TipZ * Scale;
            long a = EnemyAddresses.FloorSlots.SlotAddr(_target, 0), b = BodyCollision.SlotBase(_target), up = EnemyAddresses.CharObjects.PosAddr(_target);
            float ux = Memory.ReadFloat(up), uh = Memory.ReadFloat(up + 4), uy = Memory.ReadFloat(up + 8);
            float lowest = float.MaxValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;                 // not this frame's placement
                lowest = Math.Min(lowest, ch - Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride));
            }
            if (lowest == float.MaxValue) lowest = uh;                                            // no live sphere: its feet
            if (tip < lowest) { if (_risen) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the tip ({tip:F1}) stopped under enemy slot {_target}'s hit sphere ({lowest:F1}) — no strike"); } return; }
            if (BigBang.NearestHitSphereEdge(_target, a, _sx, _sy) > StrikeRadius) { if (_risen) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {_target} moved off the spear — no strike"); } return; }
            _struck = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the tip ({tip:F1}) reached enemy slot {_target}'s hit sphere ({lowest:F1})");
            Strike(_target);
        }

        /// <summary>One player-hit sphere on the target's body at the weapon's attack, thrown away from the spear.</summary>
        private static void Strike(int slot) => Hit(slot, 1f, KickStrength, "struck", drains: true);

        /// <summary>One player-hit sphere on an enemy's body: <paramref name="share"/> of the weapon's attack, thrown away from the
        /// spear at <paramref name="kick"/> (0 = no throw). Withdrawn after ShellLifeTicks if the engine did not take it.</summary>
        private static void Hit(int slot, float share, float kick, string what, bool drains)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Math.Max(1, (int)Math.Round(Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset) * share));
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(StrikeRadius, cr), attack, 0);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            if (kick > 0f)
            {
                F(0x80, _sx); F(0x84, _ground); F(0x88, _sy);             // the kick comes from the spear
                F(0x90, kick); F(0x94, KickDecay);
                BitConverter.GetBytes(2).CopyTo(e, 0x98);                // kick type 2: thrown away from it
            }
            if (!drains) BitConverter.GetBytes(CodeCaves.NoDrainMark).CopyTo(e, CodeCaves.NoDrainMarkOff);   // the no-drain cave bills nothing for it
            CollisionPool.Plant(pool, idx, e);
            _shells.Add((idx, ShellLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what} enemy slot {slot} for {attack} (entry {idx})");
        }

        // ── the turning spikes ──
        private const float SpikeShare   = 0.1f;   // a spike's hit: a tenth of the spear's
        private const float SpikeStepDeg = 60f;    // the six spikes: every 60° of turn another passes any given point
        private const float SpikeReach   = 2f;     // a body sphere this far past the spear's solid column counts as touching
        private static int   _spinTick = -1;
        private static float _fadeK = 1f;          // the spear's visibility 0..1, which the confused enemies' tint follows

        /// <summary>Every 60° of the risen spear's turn (the spin rate, counted from the moment it stopped rising), each live enemy
        /// whose hit sphere touches the solid column takes a spike's hit.</summary>
        private static void SpinContacts(double age)
        {
            double turning = age - (EmergeStartSeconds + EmergeSeconds);
            if (turning < 0) return;
            int tick = (int)(turning * SpinDegPerSec / SpikeStepDeg);
            if (tick == _spinTick) return;
            _spinTick = tick;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                if (BigBang.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), _sx, _sy) > BlockRadius + SpikeReach) continue;
                Hit(s, SpikeShare, 0f, "spike caught", drains: false);
            }
        }

        // ── confusion ──
        private static void Confuse(int slot)
        {
            if (GameClock.Now >= _confusionEnd) return;
            _confusedUntil[slot] = _confusionEnd;
            _victim[slot] = -3;                                          // unset: the first tick chooses and logs
            _wanderSet[slot] = default;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {slot} confused");
        }

        private static string Whom(int victim) => victim >= 0 ? $"enemy slot {victim}" : victim == -1 ? "the player" : "nothing — wandering";

        private static bool Confused(int slot) => _confusedUntil[slot] != default && GameClock.Now < _confusedUntil[slot] && Enemies.IsLive(slot);

        private static bool InArea(float x, float y) { float dx = x - _sx, dy = y - _sy; return dx * dx + dy * dy <= ConfusionRadius * ConfusionRadius; }

        /// <summary>The nearest thing in the confusion area for a confused slot to go after: another live enemy in the area, or the
        /// player while the player is in it; −2 when there is nothing.</summary>
        private static int Nearest(int slot)
        {
            long me = EnemyAddresses.CharObjects.PosAddr(slot);
            float mx = Memory.ReadFloat(me), my = Memory.ReadFloat(me + 8);
            int best = -2; float bestD = float.MaxValue;
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            if (InArea(px, py)) { bestD = (px - mx) * (px - mx) + (py - my) * (py - my); best = -1; }
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (s == slot || !Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float x = Memory.ReadFloat(p), y = Memory.ReadFloat(p + 8);
                if (!InArea(x, y)) continue;
                float d = (x - mx) * (x - mx) + (y - my) * (y - my);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>A wandering slot's spot: a random point within WanderRange of where it stands, at its height, renewed every
        /// WanderSeconds or once it has come within WanderReached of it. Returns the spot's guest address.</summary>
        private static uint Wander(int slot)
        {
            long q = CodeCaves.BabelWander + (long)slot * CodeCaves.BabelWanderStride;
            long me = EnemyAddresses.CharObjects.PosAddr(slot);
            float mx = Memory.ReadFloat(me), mh = Memory.ReadFloat(me + 4), my = Memory.ReadFloat(me + 8);
            float wx = Memory.ReadFloat(q), wy = Memory.ReadFloat(q + 8);
            bool reached = (wx - mx) * (wx - mx) + (wy - my) * (wy - my) <= WanderReached * WanderReached;
            if (_wanderSet[slot] == default || reached || (GameClock.Now - _wanderSet[slot]).TotalSeconds >= WanderSeconds)
            {
                double ang = _rng.NextDouble() * 2 * Math.PI, r = WanderRange * (0.5 + 0.5 * _rng.NextDouble());
                var b = new byte[16];
                BitConverter.GetBytes(mx + (float)(Math.Sin(ang) * r)).CopyTo(b, 0);
                BitConverter.GetBytes(mh).CopyTo(b, 4);
                BitConverter.GetBytes(my + (float)(Math.Cos(ang) * r)).CopyTo(b, 8);
                BitConverter.GetBytes(1f).CopyTo(b, 12);
                Memory.WriteBytesBatch(q, b);
                _wanderSet[slot] = GameClock.Now;
            }
            return (uint)(q - Memory.Pcsx2Base);
        }

        private static void DriveConfusion()
        {
            bool any = false;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (_confusedUntil[s] == default) continue;
                if (!Confused(s)) { Release(s); continue; }
                any = true;
                int victim = Nearest(s);
                if (victim != _victim[s]) { _victim[s] = victim; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {s} → after {Whom(victim)}"); }
                if (Mirage.Armed)
                {
                    uint ptr = victim >= 0 ? (uint)(EnemyAddresses.CharObjects.PosAddr(victim) - Memory.Pcsx2Base)
                             : victim == -1 ? StbExternCmd.PlayerPosGuest : Wander(s);
                    if (Memory.ReadUInt(CodeCaves.PtrAddr(s)) != ptr) Memory.WriteUInt(CodeCaves.PtrAddr(s), ptr);
                }
                Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharaTint, TintR * _fadeK, TintG * _fadeK, TintB * _fadeK);
            }
            RetireShells();
            if (any) ContactHits();
            OwnsTable = any && Mirage.Armed;
            if (any && (GameClock.Now - _lastReport).TotalSeconds >= 1) { _lastReport = GameClock.Now; Report(); }
        }

        /// <summary>DIAGNOSTIC: each confused enemy — where it is, whom it is after and how far, its motion, and what its
        /// pointer-table entry holds.</summary>
        private static void Report()
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Confused(s)) continue;
                long me = EnemyAddresses.CharObjects.PosAddr(s);
                float mx = Memory.ReadFloat(me), my = Memory.ReadFloat(me + 8);
                float vx = px, vy = py;
                if (_victim[s] >= 0) { long v = EnemyAddresses.CharObjects.PosAddr(_victim[s]); vx = Memory.ReadFloat(v); vy = Memory.ReadFloat(v + 8); }
                else if (_victim[s] == -2) { long q = CodeCaves.BabelWander + (long)s * CodeCaves.BabelWanderStride; vx = Memory.ReadFloat(q); vy = Memory.ReadFloat(q + 8); }
                float dist = (float)Math.Sqrt((vx - mx) * (vx - mx) + (vy - my) * (vy - my));
                int motion = Memory.ReadInt(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.MotionId);
                uint ptr = Mirage.Armed ? Memory.ReadUInt(CodeCaves.PtrAddr(s)) : 0;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot {s} at ({mx:F0},{my:F0}) after {Whom(_victim[s])} at ({vx:F0},{vy:F0}) dist {dist:F0}, motion {motion}, table → 0x{ptr:X8}");
            }
        }

        /// <summary>A confused slot back to normal: its pointer on the player, its tint off.</summary>
        private static void Release(int slot)
        {
            _confusedUntil[slot] = default; _victim[slot] = -3; _wanderSet[slot] = default;
            if (Mirage.Armed) Memory.WriteUInt(CodeCaves.PtrAddr(slot), StbExternCmd.PlayerPosGuest);
            Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharaTint, 0f, 0f, 0f);
        }

        /// <summary>A confused enemy's open attack sphere touching another enemy's body: a hit planted on that body.</summary>
        private static void ContactHits()
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            byte[] active = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (active == null) return;
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (BitConverter.ToInt32(active, i * 4) == 0) continue;
                long e = pool + i * CollisionPool.Stride;
                int owner = Memory.ReadInt(e + CollisionPool.Owner);
                if (owner < 200 || (owner - 200) % 5 != 0) continue;
                int attacker = (owner - 200) / 5;
                if (attacker >= EnemyAddresses.FloorSlots.Count || !Confused(attacker)) continue;
                if (Memory.ReadInt(e + CollisionPool.GateA) != Memory.ReadInt(e + CollisionPool.GateB)) continue;   // not open this frame
                byte[] atk = Memory.ReadBytesBatch(e, CollisionPool.Stride);
                if (atk == null) continue;
                float ax = BitConverter.ToSingle(atk, 0x00), ay = BitConverter.ToSingle(atk, 0x08), ar = BitConverter.ToSingle(atk, CollisionPool.Radius);
                int damage = BitConverter.ToInt32(atk, 0x34);
                if (damage <= 0 || ar <= 0f) continue;
                for (int v = 0; v < EnemyAddresses.FloorSlots.Count; v++)
                {
                    if (v == attacker || !Enemies.IsLive(v)) continue;
                    if ((GameClock.Now - _lastContact[attacker, v]).TotalSeconds < ContactCooldown) continue;
                    if (BigBang.NearestHitSphereEdge(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), ax, ay) > ar) continue;   // no contact
                    if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) return;
                    int idx = CollisionPool.TakeFreeSlot(pool);
                    if (idx < 0) return;
                    BigBang.BodyCentre(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), out float cx, out float ch, out float cy, out _);
                    byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, damage, 0);
                    void I(int o, int val) => BitConverter.GetBytes(val).CopyTo(hit, o);
                    I(CollisionPool.Owner, -1); I(0x60, -1); I(0x64, 0); I(0x68, -1); I(0x6C, 0);        // nobody's: damage − defence, no weapon, no drain, no credit
                    Array.Copy(atk, 0x4C, hit, 0x4C, 4);                                                // the attack's reaction…
                    Array.Copy(atk, 0x80, hit, 0x80, 0x1C);                                             // …and its kick (+0x80..+0x98)
                    CollisionPool.Plant(pool, idx, hit);
                    _shells.Add((idx, ShellLifeTicks));
                    _lastContact[attacker, v] = GameClock.Now;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {attacker}'s swing lands on enemy slot {v} for {damage} before defence (entry {idx})");
                }
            }
        }

        /// <summary>Planted hits the engine has not consumed within their life are withdrawn.</summary>
        private static void RetireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var (idx, ticks) = _shells[i];
                if (--ticks > 0) { _shells[i] = (idx, ticks); continue; }
                if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); }   // the mark goes with it (Set never writes +0x9C)
                _shells.RemoveAt(i);
            }
        }

        /// <summary>Ungaga's lock-on reach ×2 (Babel's Spear, and Hercules' Wrath beside it — HerculesWrath's thread holds it too).</summary>
        internal static void HoldReach()
        {
            if ((uint)Memory.ReadInt(DunPatches.LockOnTableHookAddrMmu) != DunPatches.LockOnTableWord0) return;   // table patch not in this ISO
            if (Memory.ReadFloat(ReachEntry) == Reach) return;
            Memory.WriteInt(CodeCaves.LockOnFactorTable + CodeCaves.LockOnFactorOwner, 1);   // ours: the PNACH stops re-seeding
            Memory.WriteFloat(ReachEntry, Reach);
            if (!_reachHeld) { _reachHeld = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lock-on reach ×{ReachFactor:F1}"); }
        }
        internal static void ReleaseReach()
        {
            if (!_reachHeld) return;
            _reachHeld = false;
            if (Memory.ReadFloat(ReachEntry) == Reach) Memory.WriteFloat(ReachEntry, CodeCaves.LockOnFactorVanilla[Player.UngagaId]);
        }

        private static void End()
        {
            ReleaseReach();
            TakeDown();
            { long pool = CollisionPool.Resolve(); foreach (var (idx, _) in _shells) if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); } _shells.Clear(); }
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++) if (_confusedUntil[s] != default) Release(s);
            OwnsTable = false;
            _guardLatched = false; _guardSince = default;
        }
    }
}
