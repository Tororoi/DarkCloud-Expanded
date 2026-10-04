using System;
using System.Collections.Generic;
using System.Threading;
using static Dark_Cloud_Improved_Version.RockFall;
using static Dark_Cloud_Improved_Version.RockImpact;
using static Dark_Cloud_Improved_Version.NutBonk;
using static Dark_Cloud_Improved_Version.TargetDimming;
using static Dark_Cloud_Improved_Version.Shockwave;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Terra Sword — a five-second guard charge turns the weapon green and drops something on the locked-on enemy the moment
    /// it is full (primed and waiting while there is no lock): Ungaga's is Master Utan's boulder (<see cref="IwaModel"/>), which blasts
    /// what it lands on, rests solid for <see cref="RestSeconds"/> pinning what is under it, then fades; Super Steve's Terra Sword
    /// sphere drops a nut (<see cref="KinomiModel"/>) onto the target's head, bonking and confusing it. The fall, the growth, the shadow
    /// and the target's darkening are the engine's own frames (the ISO's fall-drive cave, <see cref="RockFall"/>); this thread sets a
    /// drop up and watches for its landing: the impact is <see cref="RockImpact"/>, the bonk <see cref="NutBonk"/>, the shockwave
    /// <see cref="Shockwave"/>, the darkening <see cref="TargetDimming"/>, all sharing this class's members through using static
    /// (docs/terra-sword.md).</summary>
    internal static class TerraSword
    {
        private const string Tag = "[TerraSword] ";
        private const int    TickMs          = 16;
        private const double ChargeSeconds   = 5.0;
        private static readonly float[] Tint = { 80f, 150f, 20f };    // the weapon's charged ambient add
        private const double TintFadeSeconds = 0.5;
        private const string ModelCode       = "c10w08";
        private const uint   BladeFrame      = 0x77303163;          // 'c','1','0','w' — the Terra Sword's mesh frame, c10w08__m
        // The fall.
        private const float  DropHeight      = 500f;                // the dropped thing's bottom this far above the ground under the target
        // units/s²: the impact speed of a 300 u drop under 0.6 of the judgement blade's 500 (√(2·300·300) ≈ 424 u/s), from DropHeight: v²/(2·h)
        private const double Gravity         = 2.0 * 300.0 * 300.0 / (2.0 * DropHeight);   // 180
        private const float  Sink            = 4f;                  // how far the resting rock sits into the ground
        private const float  GrowDrop        = 150f;                // it grows from nothing to full size over this much of the drop
        private const float  ShadowGrowDrop  = 300f;                // …its shadow over this much
        // The target darkens as the drop comes down — its own lighting (CCharacter DimOn / DimFloor: the step eases DimFactor toward the
        // floor 0.08 a frame), the floor lowered with the fall to TargetDim, and released where the fall ends.
        private const float  TargetDim       = 0.55f;               // the rock's…
        private const float  KinomiDim       = 0.8f;                // …the nut's, lighter
        private static float DimDepth => _xiao ? KinomiDim : TargetDim;
        private const float  MinScale        = 0.01f;               // "nothing": a zero scale is a singular matrix
        // The rock's impact.
        private const float  ReferenceRadius = 21f;                 // the rock's
        internal const float BlastRadius     = ReferenceRadius + 5f;
        internal const float BlastShare      = 4f;
        internal static readonly float KickStrength = (float)(Math.Sqrt(2.0) * BlastFalloff.KickStrength);   // twice Big Bang's throw distance (distance ≈ force²/(2·decay)): 4.95
        internal const float KickDecay       = BlastFalloff.KickDecay;
        private const float  ImpactWhp       = 10f;
        // syougekiha's one KEY (its cfg): frames 2–31 at 0.5.
        private const string ShockName       = "syougekiha";
        private const int    ShockTemplate   = 5;                   // a stock config's shape; the name and motions are replaced
        internal const float ShockStart      = 2f, ShockRate = 0.5f;      // its own KEY's rate
        internal const float ShockScale      = ReferenceRadius / 3f;   // its reference radius 3 → the rock's 21
        // The resting rock.
        private const double RestSeconds     = 20.0;
        private const double FadeSeconds     = 0.5;
        internal const float StuckShare      = 1f;
        private const double StuckSeconds    = 1.0;
        internal const float HitRadius       = 6f;                  // a planted hit's least radius (it sits on the enemy's body)
        internal const float KickLead        = 1f;                  // a victim right under the spot is thrown from this far on its far side from the player
        internal const int   ShellLifeTicks  = 10;                  // ticks a planted hit stays before it is withdrawn (EnemyHit leaves the engine its pool reserve)
        // Super Steve's nut.
        private const float  KinomiScale     = 2f;                  // ±1.9 at 1× → ±3.8
        private const float  KinomiSink     = 0.5f;
        internal const float BonkShare       = 0.5f;
        internal const float BonkUp          = 80f, BonkSide = 45f, BonkGravity = 400f;   // units/s, units/s, units/s²: a short hop off the head
        private const double HopAimSeconds   = 0.25;                // the armed hop's drift and landing floor re-worked this often while the nut falls
        internal const double ConfusionHold  = 3600.0;              // seconds: the confusion is ended with the nut (TakeDown), not by time

        private enum Phase { Idle, Charging, Primed }
        private static Phase    _phase = Phase.Idle;
        private static readonly GuardHold _guard = new();             // the guard pose timed (its clock started, read and cleared by the charge)
        internal static DateTime _tintFadeFrom;
        internal static volatile bool _rockUp, _falling, _bouncing;
        private static bool     _xiao;                              // Super Steve's sphere is the wielder (latched per thread)
        internal static int     _target = -1;
        internal static float   _x, _y, _ground, _yaw, _start;
        private static DateTime _landed, _lastStuck;
        internal static int     _bonked = -1;                       // the enemy the nut confused (its confusion alone ends with the nut)
        internal static bool    _headArmed;                         // the nut's fall stops on the target's head (the cave's armed hop: a landing there is a bonk)

        /// <summary>Ungaga with the Terra Sword, or Xiao with Super Steve and a Terra Sword sphere.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.terrasword);

        /// <summary>The effect this weapon wants in the SECOND main-character instance — Ungaga's: the shockwave (its one clip the muzzle
        /// motion, so the engine retires it at its end: it plays once), every phase radius zeroed. Super Steve's nut wants none: the
        /// bonked enemy's stars are the resident ones every confused enemy wears (ConfusionStars).</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded() || Player.CurrentCharacterNum() == Player.XiaoId) return null;
            _shock ??= BorrowedShots.VisualOnly(ShockTemplate, ShockName, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: BorrowedShots.WepEffDir, instance: ShotEffectPack.CharaMainEffectCrash);
            return _shock;
        }

        // ── the two forms ──
        private static uint  ModelRoot()      => _xiao ? KinomiModel.Root() : IwaModel.Root();
        private static void  KeepTextures()   { if (_xiao) KinomiModel.KeepTextures(); else IwaModel.KeepTextures(); }
        private static void  ReleaseTextures() { if (_xiao) KinomiModel.ReleaseTextures(); else IwaModel.ReleaseTextures(); }
        internal static float FullScale       => _xiao ? KinomiScale : 1f;
        internal static float Radius          => _xiao ? KinomiModel.Radius * KinomiScale : IwaModel.Radius;
        internal static float SinkNow         => _xiao ? KinomiSink : Sink;
        private static string What            => _xiao ? "nut" : "boulder";
        private static void  SetTint(float k)  => WielderTint.Set(k, ModelCode, BladeFrame, Tint, _xiao);   // the spear's frame, or the whole slingshot

        public static void RockfallEffect()
        {
            int ch = Player.CurrentCharacterNum();
            _xiao = ch == Player.XiaoId;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{(_xiao ? "nut" : "rock")}fall: guard {ChargeSeconds:F0} s to prime; locked on, it drops");
            if (_xiao) Confusion.Configure(null, null, provokes: true, Tag);
            try
            {
                // Ends the moment the active character changes, a menu open or not (the copy's slot was cloned from this character's
                // objects, which an ally switch reloads; a sphere keeps Wielded() true across it).
                while (Wielded() && Player.InDungeonFloor() && Player.CurrentCharacterNum() == ch)
                {
                    GroundShadow.Guard();                                                   // its shadow off outside play, the pause screen and the item menu
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Step();
                        if (_rockUp) DriveRock();
                        ShockDrive();
                        if (_xiao) Confusion.Tick();
                        RetireShells();
                    }
                    Thread.Sleep(TickMs);
                }
            }
            finally { End(); }
        }

        // ── the charge and the trigger ──
        private static void Step()
        {
            switch (_phase)
            {
                case Phase.Idle:
                    FadeTint();
                    // The charge counts from here, even for a guard held while the drop was still up.
                    if (_guard.Held() > 0 && !_rockUp) { _phase = Phase.Charging; _tintFadeFrom = default; _guard.Since = GameClock.Now; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charging"); }
                    break;
                case Phase.Charging:
                {
                    double held = _guard.Held();
                    if (held <= 0) { _phase = Phase.Idle; BladeTint.Clear(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "guard released before the charge was full"); break; }
                    float f = (float)Math.Min(1.0, held / ChargeSeconds);
                    SetTint(f);
                    if (f >= 1f) { _phase = Phase.Primed; Player.FlashChargeComplete(); ModelRoot(); GroundShadow.Root(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed"); }   // the model and its shadow loaded into the cash now
                    break;
                }
                case Phase.Primed:
                    SetTint(1f);
                    if (PlayerAction.LockHeld(out int slot) && slot < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(slot))
                    {
                        _phase = Phase.Idle; _guard.Since = default;
                        Drop(slot);                                                            // the green holds until the fall ends
                    }
                    break;
            }
        }

        /// <summary>From where the fall ends: the green out over TintFadeSeconds, then the weapon's own colour; held full while it falls.</summary>
        private static void FadeTint()
        {
            if (_falling) { SetTint(1f); return; }
            if (_tintFadeFrom == default) return;
            double t = (GameClock.Now - _tintFadeFrom).TotalSeconds / TintFadeSeconds;
            if (t >= 1.0) { BladeTint.Clear(); _tintFadeFrom = default; return; }
            SetTint((float)(1.0 - t));
        }

        // ── the drop ──
        private static void Drop(int slot)
        {
            if (_rockUp) TakeDown();
            if (!CaveLive) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the fall-drive cave is not in this ISO — repatch to drop"); _tintFadeFrom = GameClock.Now; return; }
            uint root = ModelRoot();
            if (root == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no {What} model in the item cash — nothing falls"); _tintFadeFrom = GameClock.Now; return; }
            _target = slot;
            TargetGround();
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            if (!BladeProp.Spawn(MinScale, root, pointDown: false)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no {What} copy (slot or cave busy)"); _tintFadeFrom = GameClock.Now; return; }
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            float start = _start = RestHeight() + DropHeight + SinkNow;                         // its bottom DropHeight above the ground
            BladeProp.Place(_x, start, _y, _yaw);
            BladeProp.Alpha(1f);
            DimStart(slot);
            // Mode 4: it follows the target's root. The nut's stop follows the root's height, raised by the species' authored height
            // (scaled with the unit) and its own radius — it lands ON the head, the frame it reaches it — and the hop off the head
            // is ARMED in the cave, so that landing turns into the bounce on the same frame.
            uint follow = (uint)(EnemyAddresses.CharObjects.PosAddr(slot) - 0x20000000L), stopSrc = 0; float stopOff = 0f;
            _headArmed = _xiao;
            if (_headArmed) { stopSrc = follow + 4; stopOff = EnemyBody.HeadHeight(slot) + Radius; }
            float span = Math.Max(1f, start - RestHeight());
            uint frame = GroundShadow.Root();
            FallRow(0, (uint)SlotScaleGuest, 3, start * FullScale / GrowDrop, -FullScale / GrowDrop, MinScale * FullScale, FullScale);   // the drop's scale
            FallRow(1, frame != 0 ? frame + (uint)CFrameVu1.TrsScaleX : 0, 3, start * Radius / ShadowGrowDrop, -Radius / ShadowGrowDrop, MinScale * Radius, Radius);   // its shadow's
            FallRow(2, frame != 0 ? frame + (uint)CFrameVu1.DirtyTrs : 0, 1, 1f, 0f, 1f, 1f);        // …rebuilt from its TRS every frame (1.0f: non-zero)
            FallRow(3, frame != 0 ? frame + (uint)CFrameVu1.WorldCacheA : 0, 1, 0f, 0f, 0f, 0f);
            float dim = 1f - DimDepth;
            FallRow(4, (uint)(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.DimFloor - 0x20000000L), 1, 1f - dim * start / span, dim / span, DimDepth, 1f);   // the target's darkness
            Memory.WriteInt(CodeCaves.FallDrive + CodeCaves.FallDriveHopped, 0);
            if (_headArmed) { _hopSide = new Random().Next(2) == 0 ? -1f : 1f; AimHop(); } else Memory.WriteInt(CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 0);
            StartFall(start, 0f, (float)(Gravity / 3600.0), RestHeight(), follow, 0f, 0f, stopSrc, stopOff);
            _rockUp = true; _falling = true; _bouncing = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {What} drops on enemy slot {slot} at ({_x:F0},{_y:F0}) ground {_ground:F1}" + (_headArmed ? $", onto its head ({stopOff - Radius:F1} over its root)" : ""));
        }

        private static long SlotScaleGuest => DungeonCharaDraw.CharaArray - 0x20000000L + (long)BladeProp.Slot * DungeonCharaDraw.CharaStride + CCharacter.CharScale;

        /// <summary>The target's root across the ground, and the floor under it (its root's height where no floor is found).</summary>
        private static void TargetGround()
        {
            long p = EnemyAddresses.CharObjects.PosAddr(_target);
            _x = Memory.ReadFloat(p); float rootH = Memory.ReadFloat(p + 4); _y = Memory.ReadFloat(p + 8);
            _ground = DungeonFloor.HeightAt(_x, _y, rootH + 20f, out float fh) ? fh : rootH;
        }

        /// <summary>The dropped thing's centre height at rest: its radius above the ground, less its sink.</summary>
        private static float RestHeight() => _ground + Radius - SinkNow;

        private static void DriveRock()
        {
            if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); TakeDown(); return; }
            KeepTextures();                                                                    // its texture re-sent through the copy's pass
            bool landed = Memory.ReadInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag) == CodeCaves.DriveLanded;
            if (_falling)
            {
                if (_headArmed && Memory.ReadInt(CodeCaves.FallDrive + CodeCaves.FallDriveHopped) != 0) { Bonk(); return; }
                if (landed) { Impact(); return; }
                if (_target >= 0 && Enemies.IsLive(_target))
                {   // the cave keeps it over the target; the floor under the target is the rock's stop (the nut's is the head)
                    TargetGround();
                    if (!_headArmed) Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveStop, RestHeight());
                    else if ((GameClock.Now - _hopAimed).TotalSeconds >= HopAimSeconds) AimHop();
                }
                else if (Memory.ReadUInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveUnit) != 0)
                {   // gone or dead: it falls where it is, onto the floor
                    _headArmed = false;
                    Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 0);
                    Memory.WriteUInt (CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, 0);
                    Memory.WriteUInt (CodeCaves.VerticalDrive + CodeCaves.VerticalDriveUnit, 0);
                    Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveStop, RestHeight());
                }
                Shadow(true, null);                                                            // its scale is the cave's while it falls
                return;
            }
            if (_bouncing)
            {   // the cave hops it: its x/z drift, its height falls; the shadow follows it to where it comes down
                long sp = DungeonCharaDraw.CharaArray + (long)BladeProp.Slot * DungeonCharaDraw.CharaStride + CCharacter.CharPos;
                _x = Memory.ReadFloat(sp); _y = Memory.ReadFloat(sp + 8);
                if (landed)
                {
                    _bouncing = false;
                    Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
                    Rest();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the nut comes to rest at ({_x:F0},{_ground:F0},{_y:F0})");
                }
                else Shadow(true, 1f);
                return;
            }
            double age = (GameClock.Now - _landed).TotalSeconds;
            if (age >= RestSeconds + FadeSeconds) { TakeDown(); return; }
            BladeProp.Alpha(age < RestSeconds ? 1f : (float)Math.Max(0.0, 1.0 - (age - RestSeconds) / FadeSeconds));
            Shadow(age < RestSeconds, 1f);                                                     // gone as it starts to fade
            if (!_xiao && (GameClock.Now - _lastStuck).TotalSeconds >= StuckSeconds) { _lastStuck = GameClock.Now; StuckHits(); }
        }

        /// <summary>The fall ended on the floor: the rock's impact, or (the nut missed its target) the nut simply comes to rest.</summary>
        private static void Impact()
        {
            _falling = false;
            Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
            FallRowsOff();
            BladeProp.SetHeight(RestHeight());
            BladeProp.SetScale(FullScale);
            DimEnd();                                                                          // the target's light back
            _tintFadeFrom = GameClock.Now;                                                     // the green fades from here
            if (_xiao) { Rest(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the nut lands at ({_x:F0},{_ground:F0},{_y:F0}) — no head under it"); return; }
            _landed = GameClock.Now; _lastStuck = GameClock.Now;
            Solid();
            Blast();
            ShockPlay();
            GamePad.Knockdown();
            WeaponWhp.Drain((ushort)Items.terrasword, ImpactWhp, Tag + "impact ");
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"impact at ({_x:F0},{_ground:F0},{_y:F0})");
        }

        /// <summary>At rest where it is: solid, its rest clock started.</summary>
        private static void Rest()
        {
            _landed = GameClock.Now; _lastStuck = GameClock.Now;
            Solid();
        }

        /// <summary>The resting drop as a solid column: enemies, the player and enemy shots (the spear-block caves), its top the
        /// resting thing's.</summary>
        private static void Solid() => SpearBlock.Arm(_x, _ground, _y, Radius, RestHeight() + Radius);

        private static void TakeDown()
        {
            if (!_rockUp) return;
            _rockUp = _falling = _bouncing = false; _headArmed = false;
            Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
            Memory.WriteUInt(CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, 0);
            Memory.WriteInt (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 0);
            FallRowsOff();
            SpearBlock.Disarm();                                                                     // passable again
            Shadow(false, 0f);
            DimEnd();
            if (_xiao && _bonked >= 0) { Confusion.Unconfuse(_bonked); _bonked = -1; }               // the bonked enemy's confusion ends with the nut (others keep theirs)
            BladeProp.Despawn();
            ReleaseTextures();
            _target = -1;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {What} is gone");
        }

        /// <summary>The drop's round shadow on the floor under it (GroundShadow), <paramref name="scale"/> of its radius (null leaves it
        /// to the fall-drive cave's rows while it falls). Re-asserted every tick while on.</summary>
        private static void Shadow(bool on, float? scale)
        {
            if (on && BladeProp.Active) GroundShadow.Show(_x, _ground, _y, scale is float sc ? sc * Radius : null);
            else GroundShadow.Hide();
        }

        private static void End()
        {
            ShockStop();
            TakeDown();
            if (_xiao && _bonked >= 0) { Confusion.Unconfuse(_bonked); _bonked = -1; }
            long pool = CollisionPool.Resolve();
            foreach (var sh in _shells) CollisionPool.Withdraw(pool, sh.Idx, clearMark: true);
            _shells.Clear();
            BladeTint.Clear();
            _phase = Phase.Idle; _guard.Since = default; _tintFadeFrom = default;
            if (_xiao) KinomiModel.Forget(); else IwaModel.Forget();
            GroundShadow.Forget();                                                        // a floor change empties the cash: loaded again when next wanted
        }
    }
}
