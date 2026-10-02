using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Babel's Spear — "Curse of Babel": a five-second guard charge raises a giant copy of the spear out of the ground
    /// under the locked-on enemy — following it across the ground while it rises, until the tip strikes it — point up, most of it
    /// still buried. The enemy above it is struck and thrown off it, and it
    /// and every enemy on the floor are CONFUSED for as long as the spear stands
    /// (<see cref="SpearSeconds"/> plus its fade: the confusion ends when the spear has fully faded), as is any enemy that appears while it stands (Ungaga locks on from twice as
    /// far while the spear is his — the Mirage's reach, <see cref="Mirage.HoldReach"/>): tinted a light blue, each
    /// goes after the NEAREST thing — another enemy, or the player — and its swings hurt other enemies; with nothing to go after it
    /// wanders. The wielder's weapon (Ungaga's spear, Super Steve's slingshot) wears the copy's own tint: ramping in over the
    /// charge, held while the spear stands, fading with it. Once risen the spear turns slowly on the spot, and
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
    ///    as the nearest candidate anywhere on the floor — another live enemy's CCharacter position, or the player global — or,
    ///    with no candidate, at the slot's own wander quadword (CodeCaves.BabelWander), a
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
    ///    <see cref="ShockRate"/> frames a tick, which is what the spear's own timing is derived from. Drawn at its authored size for the spear, 0.7 for Super Steve (<see cref="ShockScale"/>): it marks the spot, not the
    ///    area's edge.
    /// SUPER STEVE with a Babel's Spear sphere has all of it (<see cref="Wielded"/>; Xiao's lock-on entry takes the reach): the
    /// copy is Super Steve itself (<see cref="SlingshotProp.SpawnStatue"/> — her slingshot is a skinned model, which BladeProp
    /// cannot copy — in the same chara slot 3 the rise and spin caves drive), <see cref="StatueForm"/>: stood upright at 4×, its beam at 0.7 of the spear's,
    /// and out of the ground once risen but for Steve's black feet, placed along Xiao's facing; what catches enemies as it turns is Steve's two HANDS (the fork's ends).</summary>
    internal static class BabelsSpear
    {
        private const string Tag = "[Babel] ";
        private const int    TickMs           = 50;
        private const int    GuardChargeMs    = 5000;   // hold the guard pose this long to summon (restarting whenever the pose is left); not again until the spear has fully faded
        private const int    GuardLoopMotion  = 9, GuardMoveMotion = 33;   // the guard-hold poses (the Mirage's; Ungaga's and Xiao's alike)
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
        private static float BlockRadius => F.BlockRadius;   // the risen copy as a solid column (the spear-block caves: enemies, the player, enemy shots)
        private const float  VanishSeconds    = (ShockVanishEnd - ShockVanishStart) / ShockRate / 60f;  // 3.67 s: the vanish clip, after the spear's time
        private const float  FadeStartFrame   = 60f;                                                    // the vanish clip's frame the spear starts fading at
        private const float  FadeStartSeconds = (FadeStartFrame - ShockVanishStart) / ShockRate / 60f;  // 2.0 s into the vanish
        private const float  FadeSeconds      = (ShockVanishEnd - FadeStartFrame) / ShockRate / 60f;    // 1.67 s: gone as the vanish clip ends
        private static float ShockScale => F.ShockScale;   // the beam's size: the spear's as authored (a sideways scale to the area's edge read as size, not range)
        private const int    ShockTemplate    = 5;       // a stock config's shape; the name and motions are replaced (Big Bang's choice)
        private const string ShockName        = "zibaku_f", ShockDir = BorrowedShots.EffectDir;   // the beam's cyan/blue copy on the dead zibaku_f name (BorrowedShotBakes)
        private const float  SpinDegPerSec    = 240f;
        /// <summary>What rises: its size, the model's top above its root along the rise axis, how much of the model stands
        /// above the ground once risen (model units), its turn from the wielder's facing as it is placed, the confusion-area
        /// beam's size, how its spikes catch, and the solid column's radius (the spear's shaft ~2 wide at 5× plus room: 8; Super
        /// Steve's base, its feet 0.46 from the centre: 2 at 4×).</summary>
        private sealed record Form(string Name, float Scale, float TipZ, float ExposedModelZ, float Yaw, float ShockScale,
                                   float SpikeStepDeg, float SpikeShare, bool HandSpikes, float BlockRadius);
        private static readonly Form SpearForm  = new("spear", 5f, 16.0f, 10f, 0f, 1f, 60f, 1f / 6f, false, 8f);    // six spikes: one passes a point every 60°, a sixth of the attack      // c10w10: −9.6 … 16.0 along its axis; z 6 … 16 out (25.6 long unscaled)
        // c04w13 upright: handle's end −1.85, fork tips +2.49 (4.34 long); all of it out but Steve's black feet, sunk 0.4 (1.6 units at
        // 4×) into the ground; placed along the wielder's facing.
        private static readonly Form StatueForm = new("Super Steve", 4f, 2.49f, 3.94f, 0f, 0.7f, 180f, 0.5f, true, 2f);   // two hands: one passes a point every 180°, half the attack; column = its feet (0.46 × 4)
        private static Form F = SpearForm;
        private static bool _xiao;                            // Super Steve's sphere is the wielder (latched per thread)
        private static float Scale         => F.Scale;
        private static float TipZ          => F.TipZ;
        private static float ExposedModelZ => F.ExposedModelZ;
        private static float Exposed       => ExposedModelZ * Scale;   // …in the world's units, at the copy's size
        private const float  Buried           = 4f;     // how far below the ground the tip starts
        private const int    ShellLifeTicks   = 3;      // ticks a planted hit stays before it is withdrawn
        private const int    ShellPoolReserve = 16;     // free pool entries always left to the engine
        private const float  StrikeRadius     = 6f;
        private const float  KickDecay        = 0.12f;  // a kick's fade when none is named (the strike's is the Baselard's)
        private const float  NoLockReach      = 60f;    // no lock: the nearest enemy within this, else the copy rises ahead of the wielder
        private const float  AheadDistance    = 15f;
        // Light blue, as the unit's ambient add (scene ambient ≈ 128 is neutral): kept dim.
        private const float  TintR = 12f, TintG = 50f, TintB = 84f;
        private const float  SpearTint = 50f;           // the copy's own ambient add, neutral grey (brighter, not coloured)
        private static readonly float[] WeaponTint = { SpearTint, SpearTint, SpearTint };   // the wielder's weapon wears the copy's tint
        private const string ModelCode = "c10w10";      // Babel's Spear
        private const uint   BladeFrame = 0x77303163;   // 'c','1','0','w' — its mesh frame

        private static BorrowedEffect _shock;
        private static int _shockSlot = -1;   // the sub-shot playing the shockwave (−1 = none)
        private static int _shockKey = -1;    // the KEY it is on
        private static int _shockRearms;      // DIAGNOSTIC: times the engine retired the sub-shot and it was re-armed
        private static DateTime _shockReport;

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the Dark Genie's shockwave, whenever
        /// Curse of Babel is wielded (<see cref="Wielded"/>). Handed to BorrowedShots.Start as a provider. Every phase radius is zeroed: the effect is
        /// the visual only. The config's MUZZLE motion is the VANISH clip: Step__12CSHOT_EFFECT retires a phase-0 sub-shot
        /// the frame its cursor sits within one frame below the muzzle motion's END, whichever clip is playing — with KEY 0
        /// (end 40) there, the loop clip's own first frame killed it. With the vanish (end 70) declared, the rise and the loop
        /// (frames 10–50) never reach the window, and the vanish ends the sub-shot by itself when it gets there.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded()) return null;
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
        private static readonly List<(int idx, int ticks)> _shells = new();
        private static bool _guardLatched; private static DateTime _guardSince;
        private static DateTime _lastReport;   // DIAGNOSTIC: the confused enemies' state, once a second

        /// <summary>Curse of Babel's wielder: Ungaga with Babel's Spear, or Xiao with Super Steve and a Babel's Spear sphere.</summary>
        internal static bool Wielded()
        {
            int ch = Player.CurrentCharacterNum();
            if (ch == Player.UngagaId) return Player.Weapon.GetCurrentWeaponId() == Items.babelsspear;
            if (ch == Player.XiaoId) return Player.Weapon.GetCurrentWeaponId() == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.babelsspear;
            return false;
        }

        // ── the copy: Babel's spear (BladeProp) or Super Steve (SlingshotProp's statue) ──
        private static float _copyAlpha = 1f;
        private static bool CopySpawn()
            => _xiao ? SlingshotProp.SpawnStatue(Scale, new[] { SpearTint, SpearTint, SpearTint }, 1f)
                     : BladeProp.Spawn(Scale, 0, pointDown: false, pointUp: true);
        private static void CopyPlace(float x, float h, float y)
        {   // turned relative to the wielder: their facing at the summon plus the form's turn
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY) + F.Yaw;
            if (_xiao) SlingshotProp.PlaceProjectile(x, h, y, yaw); else { BladeProp.Place(x, h, y, yaw); BladeProp.Tint(SpearTint, SpearTint, SpearTint); }
        }
        private static void CopySetHeight(float h) { if (_xiao) SlingshotProp.SetHeight(h); else BladeProp.SetHeight(h); }
        private static void CopySetXY(float x, float y) { if (_xiao) SlingshotProp.SetXY(x, y); else BladeProp.SetXY(x, y); }
        private static void CopyAlpha(float a) { _copyAlpha = a; if (!_xiao) BladeProp.Alpha(a); }
        private static bool CopyMaintain()
        {
            if (!_xiao) return BladeProp.Maintain();
            SlingshotProp.Maintain(_copyAlpha);                                      // re-asserts the slot every tick, the fade with it
            return SlingshotProp.Active;
        }
        private static void CopyDespawn() { if (_xiao) SlingshotProp.Despawn(); else BladeProp.Despawn(); }

        public static void CurseOfBabelEffect()
        {
            int ch = Player.CurrentCharacterNum();
            _xiao = ch == Player.XiaoId;
            F = _xiao ? StatueForm : SpearForm;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"curse of Babel: hold the guard {GuardChargeMs / 1000} s → the copy rises under the target for {SpearSeconds:F0} s; every enemy on the floor confused");
            try
            {
                // Ends the moment the active character changes, a menu open or not (Desert Bloom's reason: the copy's slot was cloned
                // from this character's objects, which an ally switch reloads under it; a sphere keeps Wielded() true across it).
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
            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int  mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool inPose = guarding && (mid == GuardLoopMotion || mid == GuardMoveMotion);
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
            if (_guardSince != default && !_up) SolarBlade.Clear();
            _guardSince = default;
        }

        /// <summary>The wielder's weapon tinted toward the copy's own tint: Ungaga's spear, or Super Steve's whole slingshot.</summary>
        private static void SetTint(float k)
        {
            if (_xiao) SolarBlade.Set(k, SolarShot.WeaponModel, 0, 0, WeaponTint);
            else SolarBlade.Set(k, ModelCode, BladeFrame, 0, WeaponTint);
        }

        // ── the spear ──
        private static void Summon()
        {
            if (_up) TakeDown();
            int target = Target(out float x, out float y, out float ground);
            _sx = x; _sy = y; _ground = ground; _riseStarted = _risen = _struck = false; _target = target; _fadeK = 1f; _spinTick = -1;
            if (!CopySpawn()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no {F.Name} copy (slot or cave busy)"); return; }
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            CopyPlace(_sx, RootHeight(0f), _sy);                                               // buried, still: the caves move it from here
            CopyAlpha(1f);
            _up = true; _summoned = GameClock.Now; _confusionEnd = _summoned.AddSeconds(SpearSeconds + VanishSeconds);   // the confusion outlasts the spear by the vanish: gone when it has fully faded
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {F.Name} rises at ({_sx:F0},{_sy:F0}) ground {_ground:F0}" + (target >= 0 ? $" under enemy slot {target}" : " ahead of the wielder") + $"; target redirect {(Mirage.Armed ? "armed" : "NOT ARMED — confusion cannot steer")}");
            Confusion.Configure(null, new[] { TintR, TintG, TintB }, provokes: false, Tag);  // the whole floor: no area
            if (target >= 0) Confuse(target);                                                  // the strike waits for the tip to reach it (TipStrike)
            ConfuseAll();
            ShockStart();
        }

        // ── the shockwave: the spear's spot marked ──
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
        private static float RootHeight(float t) => _ground - TipZ * Scale + (-Buried + (Exposed + Buried) * t);

        private static void DriveSpear()
        {
            if (!CopyMaintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); _up = false; return; }
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
            if (_riseStarted && !_struck && !_risen) Follow();
            if (_riseStarted && !_struck) TipStrike();
            if (!_risen && age >= EmergeStartSeconds + EmergeSeconds)
            {   // risen: the integrator off (its velocity is at zero), the exact top written once, the spin begins, and it is solid
                Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
                CopySetHeight(RootHeight(1f));
                Memory.WriteFloat(CodeCaves.BladeSpin, (float)(SpinDegPerSec * Math.PI / 180.0 / 60.0));
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, _sx);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, _ground);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, _sy);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, BlockRadius);
                Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockTop, _ground + Exposed);   // enemy shots stop below the tip
                Memory.WriteInt  (CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 1);
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
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);                     // passable again
            CopyDespawn();
            SolarBlade.Clear();                                                                       // the weapon's tint goes with the copy
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

        /// <summary>One player-hit sphere on the target's body at the weapon's attack, thrown away from the WIELDER as hard as the
        /// Baselard throws (its kick strength and fade).</summary>
        private static void Strike(int slot)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            Hit(slot, 1f, Baselard.KickStrength, "struck", drains: true, kickFrom: (px, ph, py), kickDecay: Baselard.KickDecay);
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

        /// <summary>One player-hit sphere on an enemy's body: <paramref name="share"/> of the weapon's attack, thrown away from the
        /// spear at <paramref name="kick"/> (0 = no throw). Withdrawn after ShellLifeTicks if the engine did not take it.</summary>
        private static void Hit(int slot, float share, float kick, string what, bool drains, (float x, float h, float y)? kickFrom = null, float kickDecay = KickDecay)
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
                var (kx, kh, ky) = kickFrom ?? (_sx, _ground, _sy);      // the kick comes from there (the spear, unless named)
                F(0x80, kx); F(0x84, kh); F(0x88, ky);
                F(0x90, kick); F(0x94, kickDecay);
                BitConverter.GetBytes(2).CopyTo(e, 0x98);                // kick type 2: thrown away from it
            }
            if (!drains) BitConverter.GetBytes(CodeCaves.NoDrainMark).CopyTo(e, CodeCaves.NoDrainMarkOff);   // the no-drain cave bills nothing for it
            CollisionPool.Plant(pool, idx, e);
            _shells.Add((idx, ShellLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what} enemy slot {slot} for {attack} (entry {idx})");
        }

        // ── the turning spikes ──
        private static float SpikeShare   => F.SpikeShare;     // a spike's hit: a share of the weapon's attack (the spear ⅙, Steve's hands ½)
        private static float SpikeStepDeg => F.SpikeStepDeg;   // the turn between passes at any point (the spear's six spikes 60°, Steve's two hands 180°)
        private const float SpikeReach   = 2f;     // a body sphere this far past the spear's solid column counts as touching
        // Steve's hands: the ends of the fork, in the frame of the fork's centre bone eff30 (c04w13: the arms end ±0.96 either
        // side of it along its own z, at the length it sits at), and how near a hit sphere's surface must be to one (3D).
        private static readonly (float x, float y, float z)[] Hands = { (-0.04f, 0.03f, -0.96f), (-0.04f, 0.03f, 0.96f) };
        private const float HandReach = 6f;
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
            var hands = new System.Collections.Generic.List<(float x, float h, float y)>();
            if (F.HandSpikes)
            {
                foreach (var hp in Hands)
                    if (SlingshotProp.MuzzlePointWorld(hp.x, hp.y, hp.z, out float hx, out float hh, out float hy)) hands.Add((hx, hh, hy));
                if (hands.Count == 0) return;                                  // not drawn yet: no hands to catch with
            }
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (F.HandSpikes)
                {
                    bool caught = false;
                    foreach (var hp in hands) if (SphereEdge3D(s, hp) <= HandReach) { caught = true; break; }
                    if (!caught) continue;
                    Hit(s, SpikeShare, Baselard.HalfKickStrength, "caught by Steve's hand", drains: false, kickDecay: Baselard.KickDecay);   // thrown off, half the Baselard's distance
                    continue;
                }
                if (BigBang.NearestHitSphereEdge(s, a, _sx, _sy) > BlockRadius + SpikeReach) continue;
                Hit(s, SpikeShare, Baselard.HalfKickStrength, "spike caught", drains: false, kickDecay: Baselard.KickDecay);
            }
        }

        /// <summary>How near <paramref name="p"/> is to an enemy's body in 3D: the least distance from it to the surface of any of
        /// the enemy's active hit spheres (centre distance − radius; negative inside one) — the spheres this frame placed (within 80
        /// of the unit across the ground; stale ones are skipped, as the strike does). MaxValue with none.</summary>
        private static float SphereEdge3D(int slot, (float x, float h, float y) p)
        {
            long b = BodyCollision.SlotBase(slot), up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uy = Memory.ReadFloat(up + 8), best = float.MaxValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                float dx = cx - p.x, dh = ch - p.h, dy = cy - p.y;
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dh * dh + dy * dy) - r);
            }
            return best;
        }

        // ── confusion (the shared Confusion) ──
        private static void Confuse(int slot) => Confusion.Confuse(slot, _confusionEnd);

        private static void DriveConfusion()
        {
            Confusion.MoveArea(_sx, _sy);
            Confusion.TintScale = _fadeK;                                                    // the confused enemies' tint fades with the spear
            if (_up) SetTint(_fadeK);                                                        // …and the weapon's with the copy
            Confusion.Tick();
            RetireShells();
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

        private static void End()
        {
            TakeDown();
            { long pool = CollisionPool.Resolve(); foreach (var (idx, _) in _shells) if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); } _shells.Clear(); }
            Confusion.End();
            SolarBlade.Clear();
            _guardLatched = false; _guardSince = default;
        }
    }
}
