using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Terra Sword — a five-second guard charge (<see cref="ChargeSeconds"/>), the sword turning green (<see cref="Tint"/>)
    /// as it charges, drops a boulder on the LOCKED-ON enemy the moment it is full; with no lock it stays primed (still green) and
    /// drops as soon as one is held. The green holds through the fall and fades over <see cref="TintFadeSeconds"/> from the impact.
    ///
    /// The boulder is Master Utan's (<see cref="IwaModel"/>, in the item-model cash) drawn by <see cref="BladeProp"/> in chara slot 3.
    /// It appears <see cref="DropHeight"/> above the ground under the target — above any ceiling — and falls from rest under
    /// <see cref="Gravity"/> (~2.4 s for the full drop, ~424 u/s at the impact) on the engine's frames (the blade-fall cave: vy
    /// += g, y −= vy, landed at its stop height). Until it lands, every tick puts it over the target's root and sets the stop
    /// height from the floor under it (DungeonFloor), so it tracks the target across the ground and down steps and ramps. It grows
    /// from nothing to full size over the first <see cref="GrowDrop"/> units of the drop, and its round shadow on the floor under
    /// the target grows over the first <see cref="ShadowGrowDrop"/> — the warning of where it lands. Both grow from a 2 ms loop of
    /// their own (<see cref="GrowLoop"/>) that follows the cave's fall height, so the steps keep pace with the engine's frames.
    ///
    /// Where it lands:
    ///  · one blast — every live enemy whose hit spheres come within <see cref="BlastRadius"/> (the rock's reference radius 21
    ///    + 5) of the spot takes <see cref="BlastShare"/>× the weapon's attack, thrown away from the spot at
    ///    <see cref="KickStrength"/> (twice as far as Big Bang's inner-ring kick throws);
    ///  · the shockwave syougekiha (dun/mainchara/wep_eff), played once in the second main-character
    ///    effect instance at <see cref="ShockScale"/>× (its reference radius 3 matched to the rock's 21) and an absolute
    ///    <see cref="ShockRate"/>;
    ///  · the controller rumbles as the engine's own knockdown does (<see cref="RumbleStrength"/>, <see cref="RumbleFrames"/>);
    ///  · <see cref="ImpactWhp"/> base weapon HP is billed once (WeaponWhp; Endurance scales it). The blast's and the stuck
    ///    damage's hits are marked so the engine bills nothing for them.
    /// The boulder then rests on the ground, sunk <see cref="Sink"/>, solid to the player, enemies and enemy shots (the spear-block
    /// caves, <see cref="BlockRadius"/>), for <see cref="RestSeconds"/>, then fades over <see cref="FadeSeconds"/>. An enemy whose
    /// root is inside it takes <see cref="StuckShare"/>× the attack every <see cref="StuckSeconds"/> while it stands. A new drop
    /// while one rests takes the resting one away first.</summary>
    internal static class TerraSword
    {
        private const string Tag = "[TerraSword] ";
        private const int    TickMs          = 16;
        private const double ChargeSeconds   = 5.0;
        private static readonly float[] Tint = { 80f, 150f, 20f };    // the sword's charged ambient add
        private const double TintFadeSeconds = 0.5;
        private const string ModelCode       = "c10w08";
        private const uint   BladeFrame      = 0x77303163;          // 'c','1','0','w' — the Terra Sword's mesh frame, c10w08__m
        // The fall.
        private const float  DropHeight      = 500f;                // the rock's bottom this far above the ground under the target
        // units/s²: the impact speed of a 300 u drop under 0.6 of the judgement blade's 500 (√(2·300·300) ≈ 424 u/s), from DropHeight: v²/(2·h)
        private const double Gravity         = 2.0 * 300.0 * 300.0 / (2.0 * DropHeight);   // 180
        private const float  Sink            = 4f;                  // how far the resting rock sits into the ground
        private const float  GrowDrop        = 150f;                // the rock grows from nothing to full size over this much of the drop
        private const float  ShadowGrowDrop  = 300f;                // …its shadow over this much
        private const int    GrowTickMs      = 2;                   // the growth's own cadence: well inside a frame, so each frame's height is caught as it lands
        private const float  MinScale        = 0.01f;               // "nothing": a zero scale is a singular matrix
        private const float  ShadowLift      = 0.3f;                // the shadow frame this far above the floor (off its surface)
        private const float  ShadowDrop      = 12.8f;               // the point handed to the draw sits this far below the frame — the player's own (DrawShadow__10CCharacter, 0x2A1888)
        // The impact.
        private const float  ReferenceRadius = 21f;                 // the rock's
        private const float  BlastRadius     = ReferenceRadius + 5f;
        private const float  BlastShare      = 4f;
        private static readonly float KickStrength = (float)(Math.Sqrt(2.0) * BigBang.KickStrength);   // twice Big Bang's throw distance (distance ≈ force²/(2·decay)): 4.95
        private const float  KickDecay       = BigBang.KickDecay;
        private const float  ImpactWhp       = 10f;
        private const int    RumbleStrength  = 0xE6, RumbleFrames = 22;   // the engine's knockdown rumble (OpB_DrawProcess)
        // syougekiha's one KEY (its cfg): frames 2–31 at 0.5.
        private const string ShockName       = "syougekiha";
        private const int    ShockTemplate   = 5;                   // a stock config's shape; the name and motions are replaced
        private const float  ShockStart      = 2f, ShockRate = 0.5f;      // its own KEY's rate
        private const float  ShockScale      = ReferenceRadius / 3f;   // its reference radius 3 → the rock's 21
        // The resting rock.
        private const double RestSeconds     = 10.0;
        private const double FadeSeconds     = 0.5;
        private const float  BlockRadius     = 20f;                 // the solid column (the rock is ±20.4 about its centre)
        private const float  StuckShare      = 1f;
        private const double StuckSeconds    = 1.0;
        private const float  HitRadius       = 6f;                  // a planted hit's least radius (it sits on the enemy's body)
        private const int    ShellLifeTicks  = 10;                  // ticks a planted hit stays before it is withdrawn
        private const int    ShellPoolReserve = 16;                 // free pool entries always left to the engine

        private enum Phase { Idle, Charging, Primed }
        private static Phase    _phase = Phase.Idle;
        private static DateTime _guardSince, _tintFadeFrom;
        private static volatile bool _rockUp, _falling;
        private static int      _target = -1;
        private static float    _x, _y, _ground, _yaw, _start;
        private static DateTime _landed, _lastStuck;
        private static readonly List<(int idx, int ticks)> _shells = new();
        private static BorrowedEffect _shock;
        private static int _sub = -1;                               // the sub-shot playing the shockwave (−1 = none)

        /// <summary>Ungaga with the Terra Sword.</summary>
        internal static bool Wielded()
            => Player.CurrentCharacterNum() == Player.UngagaId && Player.Weapon.GetCurrentWeaponId() == Items.terrasword;

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the shockwave, whenever the Terra Sword is
        /// wielded. Every phase radius zeroed: it is the visual only (the blast is the hit). Its one clip is the muzzle motion, so
        /// the engine retires the sub-shot at the clip's end: it plays once.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded()) return null;
            if (_shock == null)
            {
                _shock = BorrowedShots.CustomConfig(ShockTemplate, ShockName, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                                    dir: BorrowedShots.WepEffDir, instance: ShotEffectPack.CharaMainEffectCrash);
                if (_shock == null) return null;
                for (int ph = 0; ph < 4; ph++) BorrowedShots.SetPhaseRadius(_shock, ph, 0f);
            }
            return _shock;
        }

        public static void RockfallEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"rockfall: guard {ChargeSeconds:F0} s to prime; a swing while locked on drops the boulder");
            try
            {
                while (Wielded() && Player.InDungeonFloor())
                {
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Step();
                        if (_rockUp) DriveRock();
                        ShockDrive();
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
                    if (GuardHeld() > 0 && !_falling) { _phase = Phase.Charging; _tintFadeFrom = default; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charging"); }
                    break;
                case Phase.Charging:
                {
                    double held = GuardHeld();
                    if (held <= 0) { _phase = Phase.Idle; SolarBlade.Clear(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "guard released before the charge was full"); break; }
                    float f = (float)Math.Min(1.0, held / ChargeSeconds);
                    SolarBlade.Set(f, ModelCode, BladeFrame, 0, Tint);
                    if (f >= 1f) { _phase = Phase.Primed; Player.FlashChargeComplete(); IwaModel.Root(); IwaModel.ShadowRoot(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed"); }   // the rock and its shadow loaded into the cash now
                    break;
                }
                case Phase.Primed:
                    SolarBlade.Set(1f, ModelCode, BladeFrame, 0, Tint);
                    if (PlayerAction.LockHeld(out int slot) && slot < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(slot))
                    {
                        _phase = Phase.Idle; _guardSince = default;
                        Drop(slot);                                                            // the green holds until the impact
                    }
                    break;
            }
        }

        /// <summary>The guard pose held, as the Mirage reads it: R1 down and Ungaga in the guard loop or guard walk. The seconds it
        /// has been held (0 = not guarding).</summary>
        private static double GuardHeld()
        {
            bool r1 = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            if (!r1) { _guardSince = default; return 0; }
            if (mid != Mirage.GuardLoopMotion && mid != Mirage.GuardMoveMotion) return _guardSince == default ? 0 : (GameClock.Now - _guardSince).TotalSeconds;
            if (_guardSince == default) _guardSince = GameClock.Now;
            return (GameClock.Now - _guardSince).TotalSeconds;
        }

        /// <summary>From the impact: the green out over TintFadeSeconds, then the blade's own colour; held full while the rock falls.</summary>
        private static void FadeTint()
        {
            if (_falling) { SolarBlade.Set(1f, ModelCode, BladeFrame, 0, Tint); return; }
            if (_tintFadeFrom == default) return;
            double t = (GameClock.Now - _tintFadeFrom).TotalSeconds / TintFadeSeconds;
            if (t >= 1.0) { SolarBlade.Clear(); _tintFadeFrom = default; return; }
            SolarBlade.Set((float)(1.0 - t), ModelCode, BladeFrame, 0, Tint);
        }

        // ── the boulder ──
        private static void Drop(int slot)
        {
            if (_rockUp) TakeDown();
            uint root = IwaModel.Root();
            if (root == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no rock model in the item cash — nothing falls"); _tintFadeFrom = GameClock.Now; return; }
            _target = slot;
            TargetGround();
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            if (!BladeProp.Spawn(MinScale, root, pointDown: false)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no rock copy (slot or cave busy)"); _tintFadeFrom = GameClock.Now; return; }
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);
            float start = _start = RestHeight() + DropHeight + Sink;                            // its bottom DropHeight above the ground
            BladeProp.Place(_x, start, _y, _yaw);
            BladeProp.Alpha(1f);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, start);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, 0f);                             // from rest
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, (float)(Gravity / (60.0 * 60.0))); // per frame²
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, RestHeight());
            Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFalling);
            _rockUp = true; _falling = true;
            _lastFallY = float.NaN;
            new Thread(GrowLoop) { IsBackground = true, Name = "TerraSwordGrow" }.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the boulder drops on enemy slot {slot} at ({_x:F0},{_y:F0}) ground {_ground:F1}");
        }

        /// <summary>The target's root across the ground, and the floor under it (its root's height where no floor is found).</summary>
        private static void TargetGround()
        {
            long p = EnemyAddresses.CharObjects.PosAddr(_target);
            _x = Memory.ReadFloat(p); float rootH = Memory.ReadFloat(p + 4); _y = Memory.ReadFloat(p + 8);
            _ground = DungeonFloor.HeightAt(_x, _y, rootH + 20f, out float fh) ? fh : rootH;
        }

        /// <summary>The rock's centre height at rest: its radius above the ground, less the Sink.</summary>
        private static float RestHeight() => _ground + IwaModel.Radius - Sink;

        private static void DriveRock()
        {
            if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); TakeDown(); return; }
            IwaModel.KeepTextures();                                                           // its texture re-sent through the copy's pass
            if (_falling)
            {
                if (Memory.ReadInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag) == CodeCaves.BladeLanded) { Impact(); return; }
                if (_target >= 0 && Enemies.IsLive(_target))
                {   // over the target, its stop height the floor under it (gone or dead: it falls where it is)
                    TargetGround();
                    BladeProp.SetXY(_x, _y);
                    Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, RestHeight());
                }
                Shadow(true, null);                                                            // its scale is GrowLoop's while it falls
                return;
            }
            double age = (GameClock.Now - _landed).TotalSeconds;
            if (age >= RestSeconds + FadeSeconds) { TakeDown(); return; }
            BladeProp.Alpha(age < RestSeconds ? 1f : (float)Math.Max(0.0, 1.0 - (age - RestSeconds) / FadeSeconds));
            Shadow(age < RestSeconds, 1f);                                                     // gone as the rock starts to fade
            if ((GameClock.Now - _lastStuck).TotalSeconds >= StuckSeconds) { _lastStuck = GameClock.Now; StuckHits(); }
        }

        private static void Impact()
        {
            _falling = false; _landed = GameClock.Now; _lastStuck = GameClock.Now;
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            BladeProp.SetHeight(RestHeight());
            BladeProp.SetScale(1f);
            _tintFadeFrom = GameClock.Now;                                                     // the sword's green fades from here
            Solid();
            Blast();
            ShockPlay();
            GamePad.Rumble(RumbleStrength, RumbleFrames);
            WeaponWhp.Drain((ushort)Items.terrasword, ImpactWhp, Tag + "impact ");
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"impact at ({_x:F0},{_ground:F0},{_y:F0})");
        }

        /// <summary>The resting rock as a solid column: enemies, the player and enemy shots (the spear-block caves).</summary>
        private static void Solid()
        {
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, _x);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, _ground);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, _y);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, BlockRadius);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockTop, RestHeight() + IwaModel.Radius);   // enemy shots stop below its top
            Memory.WriteInt  (CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 1);
        }

        /// <summary>Every live enemy whose hit spheres come within BlastRadius of the spot: BlastShare× the attack, thrown away from it.</summary>
        private static void Blast()
        {
            int planted = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                if (BigBang.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), _x, _y) > BlastRadius) continue;
                if (Hit(s, BlastShare, KickStrength, "crushed")) planted++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"blast: {planted} enem{(planted == 1 ? "y" : "ies")} within {BlastRadius:F0}");
        }

        /// <summary>Every live enemy whose root is inside the resting rock: StuckShare× the attack, no throw.</summary>
        private static void StuckHits()
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - _x, dy = Memory.ReadFloat(p + 8) - _y;
                if (dx * dx + dy * dy > IwaModel.Radius * IwaModel.Radius) continue;
                Hit(s, StuckShare, 0f, "pinned under the rock");
            }
        }

        /// <summary>One player-hit sphere on an enemy's body: <paramref name="share"/> of the weapon's attack, thrown away from the
        /// rock at <paramref name="kick"/> (0 = no throw), marked so the ISO's no-drain caves bill no weapon HP for it.</summary>
        private static bool Hit(int slot, float share, float kick, string what)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return false;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return false;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * share));
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(HitRadius, cr), attack, 0);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            if (kick > 0f)
            {
                F(0x80, _x); F(0x84, _ground); F(0x88, _y);              // the kick comes from the rock
                F(0x90, kick); F(0x94, KickDecay);
                BitConverter.GetBytes(2).CopyTo(e, 0x98);                // kick type 2: thrown away from it
            }
            BitConverter.GetBytes(CodeCaves.NoDrainMark).CopyTo(e, CodeCaves.NoDrainMarkOff);
            CollisionPool.Plant(pool, idx, e);
            _shells.Add((idx, ShellLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}: enemy slot {slot} for {attack} (entry {idx})");
            return true;
        }

        private static void RetireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var (idx, ticks) = _shells[i];
                if (--ticks > 0) { _shells[i] = (idx, ticks); continue; }
                if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); }   // the mark goes with it
                _shells.RemoveAt(i);
            }
        }

        private static void TakeDown()
        {
            if (!_rockUp) return;
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);                      // passable again
            Shadow(false, 0f);
            BladeProp.Despawn();
            IwaModel.ReleaseTextures();
            _rockUp = _falling = false; _target = -1;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the boulder is gone");
        }

        /// <summary>The rock's shadow (the ISO's rock-shadow cave, in the dungeon's shadow pass): the rock's SHADOW model
        /// (IwaModel.ShadowRoot — a lit mesh drawn in shadow mode comes out garbled) placed over the rock and drawn by
        /// MGDrawShadowFast, projected straight down onto the floor under it. Re-asserted every tick while on; one write off.</summary>
        private static void Shadow(bool on, float? scale)
        {
            uint root = on && BladeProp.Active ? IwaModel.ShadowRoot() : 0u;
            if (root == 0)
            {
                if (_shadowOn) { Memory.WriteInt(CodeCaves.RockShadow + CodeCaves.RockShadowFlag, 0); _shadowOn = false; }
                return;
            }
            // The shadow frame over the rock (nothing else places it), written as CFrame's SetScale / SetRotation / SetPosition do
            // (what DrawShadowMonstor calls before its draw): the TRS fields, the TRS-dirty flag, the world cache dropped.
            long f = Memory.ToMmu(root);
            if (scale is float sc) Memory.WriteVec3(f + CFrameVu1.TrsScaleX, sc, sc, sc);
            Memory.WriteVec3(f + CFrameVu1.EulerX, 0f, 0f, 0f);
            Memory.WriteVec3(f + CFrameVu1.TrsPosX, _x, _ground + ShadowLift, _y);                 // on the floor, as a character's shadow frame sits at its feet
            Memory.WriteInt (f + 0x23C, 0);                                                   // SetRotation's own clears
            Memory.WriteInt (f + CFrameVu1.WorldCacheB, 0);
            Memory.WriteInt (f + 0x248, Memory.ReadInt(f + 0x248) | 1);
            Memory.WriteInt (f + CFrameVu1.DirtyTrs, 1);
            Memory.WriteInt (f + CFrameVu1.WorldCacheA, 0);
            var b = new byte[0x30];
            BitConverter.GetBytes(1).CopyTo(b, CodeCaves.RockShadowFlag);
            BitConverter.GetBytes(root).CopyTo(b, CodeCaves.RockShadowFrame);
            BitConverter.GetBytes(_x).CopyTo(b, CodeCaves.RockShadowPlane);
            BitConverter.GetBytes(_ground + ShadowLift - ShadowDrop).CopyTo(b, CodeCaves.RockShadowPlane + 4);
            BitConverter.GetBytes(_y).CopyTo(b, CodeCaves.RockShadowPlane + 8);
            BitConverter.GetBytes(1f).CopyTo(b, CodeCaves.RockShadowPlane + 12);
            BitConverter.GetBytes(1f).CopyTo(b, CodeCaves.RockShadowDir + 4);                 // (0, 1, 0, 0): straight down, the engine's own
            Memory.WriteBytesBatch(CodeCaves.RockShadow + 4, b.AsSpan(4).ToArray());          // frame and vectors first…
            Memory.WriteInt(CodeCaves.RockShadow + CodeCaves.RockShadowFlag, 1);              // …the flag last
            _shadowOn = true;
        }
        private static bool _shadowOn;

        /// <summary>While the rock falls: every GrowTickMs the cave's fall height is read and, when a new frame has moved it, the rock's
        /// scale (nothing → full over GrowDrop) and its shadow's (nothing → full over ShadowGrowDrop) written from it — the regular tick
        /// leaves both alone meanwhile. Ends with the fall.</summary>
        private static void GrowLoop()
        {
            while (_falling && _rockUp)
            {
                float y = Memory.ReadFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY);
                if (y != _lastFallY)
                {
                    _lastFallY = y;
                    float fallen = _start - y;
                    BladeProp.SetScale(Math.Clamp(fallen / GrowDrop, MinScale, 1f));
                    uint root = IwaModel.ShadowRoot();
                    if (root != 0)
                    {
                        float sc = Math.Clamp(fallen / ShadowGrowDrop, MinScale, 1f);
                        long f = Memory.ToMmu(root);
                        Memory.WriteVec3(f + CFrameVu1.TrsScaleX, sc, sc, sc);
                        Memory.WriteInt (f + CFrameVu1.DirtyTrs, 1);
                        Memory.WriteInt (f + CFrameVu1.WorldCacheA, 0);
                    }
                }
                Thread.Sleep(GrowTickMs);
            }
        }
        private static float _lastFallY = float.NaN;

        // ── the shockwave ──
        private static long ShockObj => _shock.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        private static void ShockPlay()
        {
            ShockStop();
            if (_shock == null || !BorrowedShots.Entered(_shock)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shockwave not entered on this floor — the rock lands without it"); return; }
            if (!BorrowedShots.Burst(_shock, _x, _ground, _y, 0, ShockScale)) return;
            _sub = Memory.ReadInt(_shock.Instance + ShotEffectPack.OffLastIdx);
            long o = ShockObj;
            Memory.WriteInt  (o + ShotEffectPack.ObjMotId, 0);
            Memory.WriteInt  (o + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat(o + ShotEffectPack.ObjFrame, ShockStart);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, ShockRate);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                           // the second instance stepped and drawn (the second-effect caves)
        }

        /// <summary>The shockwave each tick: held at the spot, its size and rate; the engine retires it at its clip's end.</summary>
        private static void ShockDrive()
        {
            if (_sub < 0 || !Player.CheckDunIsWalkingMode()) return;
            if (Memory.ReadUShort(_shock.Instance + ShotEffectPack.OffActive + _sub * 2) == 0) { _sub = -1; Memory.WriteInt(CodeCaves.SecondEffectLive, 0); return; }
            long o = ShockObj;
            Memory.WriteVec3 (o + ShotEffectPack.ObjPos, _x, _ground, _y);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, ShockRate);
            Memory.WriteVec3 (o + CCharacter.CharScale, ShockScale, ShockScale, ShockScale);
        }

        private static void ShockStop()
        {
            if (_sub < 0 || _shock == null) return;
            Memory.WriteUShort(_shock.Instance + ShotEffectPack.OffActive + _sub * 2, 0);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _sub = -1;
        }

        private static void End()
        {
            ShockStop();
            TakeDown();
            long pool = CollisionPool.Resolve();
            foreach (var (idx, _) in _shells) if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); }
            _shells.Clear();
            SolarBlade.Clear();
            _phase = Phase.Idle; _guardSince = default; _tintFadeFrom = default;
            IwaModel.Forget();                                                        // a floor change empties the cash: loaded again when next wanted
        }
    }
}
