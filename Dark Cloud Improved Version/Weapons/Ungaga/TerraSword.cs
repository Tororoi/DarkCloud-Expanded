using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Terra Sword — a five-second guard charge (<see cref="ChargeSeconds"/>), the weapon turning green (<see cref="Tint"/>)
    /// as it charges, drops something on the LOCKED-ON enemy the moment it is full; with no lock it stays primed (still green) and
    /// drops as soon as one is held. The green holds through the fall and fades over <see cref="TintFadeSeconds"/> from where it ends.
    /// No new charge starts while the dropped thing falls, rests or fades.
    ///
    /// What falls is drawn by <see cref="BladeProp"/> in chara slot 3 from a model in the item-model cash, and it appears
    /// <see cref="DropHeight"/> above the ground under the target — above any ceiling — and falls from rest under
    /// <see cref="Gravity"/> (~2.4 s for the full drop, ~424 u/s at the end) on the engine's frames (the blade-fall cave: vy += g,
    /// y −= vy, landed at its stop height). Until it lands, every tick puts it over the target's root and sets the stop height from
    /// the floor under it (DungeonFloor). It grows from nothing to full size over the first <see cref="GrowDrop"/> units of the drop,
    /// its round shadow on the floor under the target (GroundShadow) over the first <see cref="ShadowGrowDrop"/> — the warning of
    /// where it lands — and the target darkens with the fall, to <see cref="TargetDim"/> of its light under the rock,
    /// <see cref="KinomiDim"/> under the nut (its own lighting: CCharacter DimOn / DimFloor), released where the fall ends.
    /// All of it is the ENGINE's, frame by frame: the blade fall's MODE 4 (the ISO's fall-drive cave) falls it, copies the followed
    /// point's x/z into its slot, can take its landing height from a followed float, and writes the drive rows — the drop's scale,
    /// the shadow's scale and refresh words, the target's darkness — each clamp(a + b·y) of the fall height. The mod sets it up
    /// once and only watches for the landing. Without that cave in the ISO nothing drops (a repatch is logged as needed).
    ///
    /// UNGAGA's (the Terra Sword) — Master Utan's boulder (<see cref="IwaModel"/>). Where it lands:
    ///  · one blast — every live enemy whose hit spheres come within <see cref="BlastRadius"/> (the rock's reference radius 21
    ///    + 5) of the spot takes <see cref="BlastShare"/>× the weapon's attack, thrown away from the spot at
    ///    <see cref="KickStrength"/> (twice as far as Big Bang's inner-ring kick throws);
    ///  · the shockwave syougekiha (dun/mainchara/wep_eff), played once in the second main-character effect instance at
    ///    <see cref="ShockScale"/>× (its reference radius 3 matched to the rock's 21) and its own <see cref="ShockRate"/>;
    ///  · the controller rumbles as the engine's own knockdown does (<see cref="RumbleStrength"/>, <see cref="RumbleFrames"/>);
    ///  · no guard stops it: its hits carry CodeCaves.CrushMark, which the ISO's guard-crush cave passes through every guard window
    ///    inside CheckDmg (a dormant guard too), and which — sharing the no-drain mark's high half — bills no weapon HP per hit;
    ///  · no weapon ability rides on a drop's hits (poison, stop, critical, steal, drain: the entry's ability word is zeroed);
    ///    an enemy right under the spot is thrown from <see cref="KickLead"/> past it on the far side (toward the player);
    ///  · <see cref="ImpactWhp"/> base weapon HP is billed once (WeaponWhp; Endurance scales it). The blast's and the stuck
    ///    damage's hits are marked so the engine bills nothing for them.
    /// It then rests, sunk <see cref="Sink"/>, solid to the player, enemies and enemy shots (the spear-block caves), for
    /// <see cref="RestSeconds"/>, then fades over <see cref="FadeSeconds"/>; an enemy whose root is inside it takes
    /// <see cref="StuckShare"/>× the attack every <see cref="StuckSeconds"/>.
    ///
    /// SUPER STEVE's (a Terra Sword sphere) — a nut (<see cref="KinomiModel"/>) at <see cref="KinomiScale"/>×, its shadow its own
    /// size. It follows the target's root and lands on its HEAD — the species' authored height over the root (the fall's stop
    /// following the root's height) — the frame it reaches it, and so BONKS it: <see cref="BonkShare"/>× the attack, a
    /// melee-style hit (the entry's own reaction, the vanilla melee kick from the player), no blast; it bounces up and to one side
    /// (<see cref="BonkUp"/>, <see cref="BonkSide"/>, across the player's line to the target, a random side, under
    /// <see cref="BonkGravity"/> — armed in the cave before the fall, so the landing on the head becomes the hop that very frame:
    /// mode 4 drifting with no unit to follow), lands and rests <see cref="RestSeconds"/> — solid, harmless — then fades. The bonked enemy wears
    /// the spinning stars every confused enemy wears (ConfusionStars, the resident stars instance) and is CONFUSED as long
    /// (Confusion: it goes after the nearest enemy, or the player when the player is nearest; an enemy it hits goes after it).</summary>
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
        private const double RestSeconds     = 20.0;
        private const double FadeSeconds     = 0.5;
        private const float  StuckShare      = 1f;
        private const double StuckSeconds    = 1.0;
        private const float  HitRadius       = 6f;                  // a planted hit's least radius (it sits on the enemy's body)
        private const float  KickLead        = 1f;                  // a victim right under the spot is thrown from this far on its far side from the player
        private const int    ShellLifeTicks  = 10;                  // ticks a planted hit stays before it is withdrawn
        private const int    ShellPoolReserve = 16;                 // free pool entries always left to the engine
        // Super Steve's nut.
        private const float  KinomiScale     = 2f;                  // ±1.9 at 1× → ±3.8
        private const float  KinomiSink     = 0.5f;
        private const float  BonkShare       = 0.5f;
        private const float  BonkUp          = 80f, BonkSide = 45f, BonkGravity = 400f;   // units/s, units/s, units/s²: a short hop off the head
        private const double HopAimSeconds   = 0.25;                // the armed hop's drift and landing floor re-worked this often while the nut falls
        private const double ConfusionHold   = 3600.0;              // seconds: the confusion is ended with the nut (TakeDown), not by time

        private enum Phase { Idle, Charging, Primed }
        private static Phase    _phase = Phase.Idle;
        private static DateTime _guardSince, _tintFadeFrom;
        private static volatile bool _rockUp, _falling, _bouncing;
        private static bool     _xiao;                              // Super Steve's sphere is the wielder (latched per thread)
        private static int      _target = -1;
        private static float    _x, _y, _ground, _yaw, _start;
        private static DateTime _landed, _lastStuck;
        private static volatile int _dimSlot = -1;                   // the enemy darkened (−1 = none); the growth loop and the tick both use it
        private static int      _bonked = -1;                       // the enemy the nut confused (its confusion alone ends with the nut)
        private static bool     _headArmed;                         // the nut's fall stops on the target's head (the cave's armed hop: a landing there is a bonk)
        private static float    _hopGround;                         // the floor where the armed hop comes down
        private static DateTime _hopAimed;                          // when the armed hop's drift and floor were last worked out
        private static float    _hopSide;                           // which side of the player's line it hops to (±1, picked at the drop)
        /// <summary>A planted hit entry. A WATCHED one (Slot ≥ 0: the impact's and the bonk's) that leaves its enemy's HP as it was —
        /// consumed for nothing or never taken — is planted again (Replant), up to <see cref="HitTries"/> times, its first miss logged
        /// with what CheckDmg would have seen.</summary>
        private sealed class Shell { public int Idx, Ticks, Slot = -1, Hp, Tries; public Func<Shell, bool> Replant; public string What; }
        private static readonly List<Shell> _shells = new();
        private const int    HitTries        = 4;                   // plants of a watched hit before it is given up
        private static BorrowedEffect _shock;
        private static int _sub = -1;                               // the sub-shot playing the shockwave (−1 = none)

        /// <summary>Ungaga with the Terra Sword, or Xiao with Super Steve and a Terra Sword sphere.</summary>
        internal static bool Wielded()
        {
            int ch = Player.CurrentCharacterNum();
            if (ch == Player.UngagaId) return Player.Weapon.GetCurrentWeaponId() == Items.terrasword;
            if (ch == Player.XiaoId) return Player.Weapon.GetCurrentWeaponId() == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.terrasword;
            return false;
        }

        /// <summary>The effect this weapon wants in the SECOND main-character instance — Ungaga's: the shockwave (its one clip the muzzle
        /// motion, so the engine retires it at its end: it plays once), every phase radius zeroed. Super Steve's nut wants none: the
        /// bonked enemy's stars are the resident ones every confused enemy wears (ConfusionStars).</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded() || Player.CurrentCharacterNum() == Player.XiaoId) return null;
            if (_shock == null)
            {
                _shock = BorrowedShots.CustomConfig(ShockTemplate, ShockName, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: BorrowedShots.WepEffDir, instance: ShotEffectPack.CharaMainEffectCrash);
                if (_shock == null) return null;
                for (int ph = 0; ph < 4; ph++) BorrowedShots.SetPhaseRadius(_shock, ph, 0f);
            }
            return _shock;
        }

        // ── the two forms ──
        private static uint  ModelRoot()      => _xiao ? KinomiModel.Root() : IwaModel.Root();
        private static void  KeepTextures()   { if (_xiao) KinomiModel.KeepTextures(); else IwaModel.KeepTextures(); }
        private static void  ReleaseTextures() { if (_xiao) KinomiModel.ReleaseTextures(); else IwaModel.ReleaseTextures(); }
        private static float FullScale        => _xiao ? KinomiScale : 1f;
        private static float Radius           => _xiao ? KinomiModel.Radius * KinomiScale : IwaModel.Radius;
        private static float SinkNow          => _xiao ? KinomiSink : Sink;
        private static string What            => _xiao ? "nut" : "boulder";
        private static void  SetTint(float k)
        {
            if (_xiao) SolarBlade.Set(k, SolarShot.WeaponModel, 0, 0, Tint);                     // the whole slingshot, as the Solar Shot whitens it
            else SolarBlade.Set(k, ModelCode, BladeFrame, 0, Tint);
        }

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
                    if (GuardHeld() > 0 && !_rockUp) { _phase = Phase.Charging; _tintFadeFrom = default; _guardSince = GameClock.Now; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charging"); }
                    break;
                case Phase.Charging:
                {
                    double held = GuardHeld();
                    if (held <= 0) { _phase = Phase.Idle; SolarBlade.Clear(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "guard released before the charge was full"); break; }
                    float f = (float)Math.Min(1.0, held / ChargeSeconds);
                    SetTint(f);
                    if (f >= 1f) { _phase = Phase.Primed; Player.FlashChargeComplete(); ModelRoot(); GroundShadow.Root(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed"); }   // the model and its shadow loaded into the cash now
                    break;
                }
                case Phase.Primed:
                    SetTint(1f);
                    if (PlayerAction.LockHeld(out int slot) && slot < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(slot))
                    {
                        _phase = Phase.Idle; _guardSince = default;
                        Drop(slot);                                                            // the green holds until the fall ends
                    }
                    break;
            }
        }

        /// <summary>The guard pose held, as the Mirage reads it: R1 down and the wielder in the guard loop or guard walk (Ungaga's and
        /// Xiao's alike). The seconds it has been held (0 = not guarding).</summary>
        private static double GuardHeld()
        {
            bool r1 = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            if (!r1) { _guardSince = default; return 0; }
            if (mid != Mirage.GuardLoopMotion && mid != Mirage.GuardMoveMotion) return _guardSince == default ? 0 : (GameClock.Now - _guardSince).TotalSeconds;
            if (_guardSince == default) _guardSince = GameClock.Now;
            return (GameClock.Now - _guardSince).TotalSeconds;
        }

        /// <summary>From where the fall ends: the green out over TintFadeSeconds, then the weapon's own colour; held full while it falls.</summary>
        private static void FadeTint()
        {
            if (_falling) { SetTint(1f); return; }
            if (_tintFadeFrom == default) return;
            double t = (GameClock.Now - _tintFadeFrom).TotalSeconds / TintFadeSeconds;
            if (t >= 1.0) { SolarBlade.Clear(); _tintFadeFrom = default; return; }
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
            if (_headArmed) { stopSrc = follow + 4; stopOff = HeadHeight(slot) + Radius; }
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

        /// <summary>The ISO's fall-drive cave is in place (its first word, `lui t0,0x01FB`).</summary>
        private static bool CaveLive => Memory.ReadUInt(0x20000000L + CodeCaves.DebugIfCave.FallDrive) == 0x3C0801FBu;

        private static long SlotScaleGuest => DungeonCharaDraw.CharaArray - 0x20000000L + (long)BladeProp.Slot * DungeonCharaDraw.CharaStride + CCharacter.CharScale;

        /// <summary>One drive row of the fall-drive cave: clamp(a + b·y, lo, hi) to <paramref name="dst"/> (0 = off), <paramref name="count"/> words.</summary>
        private static void FallRow(int row, uint dst, int count, float a, float b, float lo, float hi)
        {
            var e = new byte[CodeCaves.FallDriveRowStride];
            BitConverter.GetBytes(dst).CopyTo(e, CodeCaves.FallRowDst);
            BitConverter.GetBytes(Math.Max(1, count)).CopyTo(e, CodeCaves.FallRowCount);
            BitConverter.GetBytes(a).CopyTo(e, CodeCaves.FallRowA);
            BitConverter.GetBytes(b).CopyTo(e, CodeCaves.FallRowB);
            BitConverter.GetBytes(lo).CopyTo(e, CodeCaves.FallRowLo);
            BitConverter.GetBytes(hi).CopyTo(e, CodeCaves.FallRowHi);
            Memory.WriteBytesBatch(CodeCaves.FallDrive + CodeCaves.FallDriveRows + row * CodeCaves.FallDriveRowStride, e);
        }

        private static void FallRowsOff() { for (int r = 0; r < CodeCaves.FallDriveRowCount; r++) Memory.WriteUInt(CodeCaves.FallDrive + CodeCaves.FallDriveRows + r * CodeCaves.FallDriveRowStride, 0); }

        /// <summary>A mode-4 fall: its fields first, the flag last.</summary>
        private static void StartFall(float y, float vy, float g, float stop, uint follow, float offX, float offZ, uint stopSrc, float stopOff)
        {
            Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, y);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, vy);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, g);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, stop);
            Memory.WriteUInt (CodeCaves.BladeFall + CodeCaves.BladeFallUnit, follow);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffX, offX);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffZ, offZ);
            Memory.WriteUInt (CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, stopSrc);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveStopOff, stopOff);
            Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallFollowing);
        }

        /// <summary>The species' authored height over its root (EnemySpecies HeightFromRoot, 15 where none), scaled with the unit — the
        /// top of its head.</summary>
        internal static float HeadHeight(int slot)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            float height = EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.HeightFromRoot.HasValue ? def.HeightFromRoot.Value : 15f;
            float scale = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharScale + 4);
            if (!(scale > 0.05f) || scale > 20f) scale = 1f;                                       // a grown miniboss
            return height * scale;
        }

        /// <summary>The nut's ARMED HOP (the cave's, taken the frame it lands on the head): up BonkUp, drifting BonkSide square to the
        /// player's line to the target (the side picked at the drop), down onto the floor where that comes out (DungeonFloor there,
        /// reckoned under BonkGravity, which Bonk sets as the fall's g once it sees the hop).</summary>
        private static void AimHop()
        {
            _hopAimed = GameClock.Now;
            long up = EnemyAddresses.CharObjects.PosAddr(_target);
            float x0 = Memory.ReadFloat(up), z0 = Memory.ReadFloat(up + 8), h0 = Memory.ReadFloat(up + 4) + HeadHeight(_target) + Radius;
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            float lx = x0 - px, ly = z0 - py, ll = (float)Math.Sqrt(lx * lx + ly * ly);
            if (ll < 1e-3f) { lx = 1f; ly = 0f; ll = 1f; }
            float vx = -ly / ll * BonkSide * _hopSide, vy = lx / ll * BonkSide * _hopSide;                // units/s, square to the line
            float ground = _ground + Radius - SinkNow;
            float t1 = (BonkUp + (float)Math.Sqrt(Math.Max(0f, BonkUp * BonkUp + 2f * BonkGravity * (h0 - ground)))) / BonkGravity;
            _hopGround = DungeonFloor.HeightAt(x0 + vx * t1, z0 + vy * t1, h0 + 20f, out float fh) ? fh : _ground;
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopVy, -BonkUp / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopDx, vx / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopDz, vy / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopStop, _hopGround + Radius - SinkNow);
            Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 1);
        }

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
            bool landed = Memory.ReadInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag) == CodeCaves.BladeLanded;
            if (_falling)
            {
                if (_headArmed && Memory.ReadInt(CodeCaves.FallDrive + CodeCaves.FallDriveHopped) != 0) { Bonk(); return; }
                if (landed) { Impact(); return; }
                if (_target >= 0 && Enemies.IsLive(_target))
                {   // the cave keeps it over the target; the floor under the target is the rock's stop (the nut's is the head)
                    TargetGround();
                    if (!_headArmed) Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, RestHeight());
                    else if ((GameClock.Now - _hopAimed).TotalSeconds >= HopAimSeconds) AimHop();
                }
                else if (Memory.ReadUInt(CodeCaves.BladeFall + CodeCaves.BladeFallUnit) != 0)
                {   // gone or dead: it falls where it is, onto the floor
                    _headArmed = false;
                    Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 0);
                    Memory.WriteUInt (CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, 0);
                    Memory.WriteUInt (CodeCaves.BladeFall + CodeCaves.BladeFallUnit, 0);
                    Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, RestHeight());
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
                    Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
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
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
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
            GamePad.Rumble(RumbleStrength, RumbleFrames);
            WeaponWhp.Drain((ushort)Items.terrasword, ImpactWhp, Tag + "impact ");
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"impact at ({_x:F0},{_ground:F0},{_y:F0})");
        }

        /// <summary>At rest where it is: solid, its rest clock started.</summary>
        private static void Rest()
        {
            _landed = GameClock.Now; _lastStuck = GameClock.Now;
            Solid();
        }

        /// <summary>The resting drop as a solid column: enemies, the player and enemy shots (the spear-block caves).</summary>
        private static void Solid()
        {
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, _x);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, _ground);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, _y);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, Radius);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockTop, RestHeight() + Radius);   // enemy shots stop below its top
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
        /// rock at <paramref name="kick"/> (0 = no throw), crush-marked: it passes any guard and bills no weapon HP.</summary>
        private static bool Hit(int slot, float share, float kick, string what)
        {
            var sh = new Shell { What = what };
            if (kick > 0f) { sh.Slot = slot; sh.Hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)); }   // the impact's hits are watched
            sh.Replant = x => PlantHit(x, slot, share, kick);
            return sh.Replant(sh);
        }

        private static bool PlantHit(Shell sh, int slot, float share, float kick)
        {
            string what = sh.What;
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return false;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return false;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * share));
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(HitRadius, cr), attack, 0);
            BitConverter.GetBytes(0).CopyTo(e, CollisionPool.AbilityFlags);   // no poison, stop, critical, steal or drain from a drop
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            if (kick > 0f)
            {   // the kick comes from the rock — for the enemy under it (no way across the ground away from the spot), from just past
                // it on the side away from the player, so it is thrown toward the player
                long vp = EnemyAddresses.CharObjects.PosAddr(slot);
                float ox = _x, oy = _y, vx = Memory.ReadFloat(vp), vy = Memory.ReadFloat(vp + 8);
                if ((vx - _x) * (vx - _x) + (vy - _y) * (vy - _y) < 4f)
                {
                    float px = Memory.ReadFloat(Addresses.dunPositionX) - vx, py = Memory.ReadFloat(Addresses.dunPositionY) - vy, pl = (float)Math.Sqrt(px * px + py * py);
                    if (pl < 1e-3f) { px = 1f; py = 0f; pl = 1f; }
                    ox = vx - px / pl * KickLead; oy = vy - py / pl * KickLead;
                }
                F(0x80, ox); F(0x84, _ground); F(0x88, oy);
                F(0x90, kick); F(0x94, KickDecay);
                BitConverter.GetBytes(2).CopyTo(e, 0x98);                // kick type 2: thrown away from it
            }
            BitConverter.GetBytes(CodeCaves.CrushMark).CopyTo(e, CodeCaves.NoDrainMarkOff);   // through any guard (the guard-crush cave), no weapon HP per hit
            CollisionPool.Plant(pool, idx, e);
            sh.Idx = idx; sh.Ticks = ShellLifeTicks; sh.Tries++;
            _shells.Add(sh);
            long up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uh = Memory.ReadFloat(up + 4), uy = Memory.ReadFloat(up + 8);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}{(sh.Tries > 1 ? $" (again, {sh.Tries})" : "")}: enemy slot {slot} for {attack} (entry {idx}) — planted at ({cx:F0},{ch:F0},{cy:F0}) r {Math.Max(HitRadius, cr):F1}, the unit at ({ux:F0},{uh:F0},{uy:F0}), HP {Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp))}");
            return true;
        }

        private static void RetireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var sh = _shells[i];
                bool spent = pool != 0 && !CollisionPool.IsActive(pool, sh.Idx);                 // the engine is done with it: its mark must not ride on into the entry's next use
                if (--sh.Ticks > 0 && !spent) continue;
                if (sh.Slot >= 0 && pool != 0 && !spent) Diagnose(sh, pool);                    // still there: nothing took it — what CheckDmg saw, before it goes
                if (pool != 0) { Memory.WriteInt(pool + sh.Idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, sh.Idx); }   // the mark goes with it
                _shells.RemoveAt(i);
                if (sh.Slot < 0 || !Enemies.IsLive(sh.Slot)) continue;
                int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(sh.Slot, EnemySlotOffsets.Hp));
                if (hp != sh.Hp) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{sh.What}: enemy slot {sh.Slot} HP {sh.Hp} → {hp}"); continue; }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{sh.What}: enemy slot {sh.Slot} took nothing (entry {sh.Idx} {(spent ? "consumed" : "never taken")}, try {sh.Tries}/{HitTries})");
                if (spent && sh.Tries == 1) Diagnose(sh, pool);
                if (sh.Tries < HitTries) sh.Replant(sh);
            }
        }

        /// <summary>What CheckDmg tests a watched hit against, logged: the enemy's invincibility (+0x1E468), motion frame, guard windows,
        /// and each active hurt sphere — its distance from the entry against the two radii, its frame window, its damage % for
        /// the attacker.</summary>
        private static void Diagnose(Shell sh, long pool)
        {
            int slot = sh.Slot;
            long e = pool + sh.Idx * CollisionPool.Stride, mu = EnemyAddresses.MainMonstorUnit.Base;
            float ex = Memory.ReadFloat(e), eh = Memory.ReadFloat(e + 4), ey = Memory.ReadFloat(e + 8), er = Memory.ReadFloat(e + CollisionPool.Radius);
            int owner = Memory.ReadInt(e + CollisionPool.Owner);
            var sb = new System.Text.StringBuilder();
            sb.Append($"diagnose slot {slot}: muteki {Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, 0x98))}, frame {Memory.ReadFloat(mu + slot * 0x3510L + 0x1FFC0):F1}, entry mark 0x{Memory.ReadUInt(e + CodeCaves.NoDrainMarkOff):X8} owner {owner}, guard");
            for (int w = 0; w < 3; w++)
            {
                long g = mu + slot * 0x20L + EnemyAddresses.GuardWindows.FlagOffset;
                if (Memory.ReadShort(g + w * 2) != 0) sb.Append($" [{Memory.ReadFloat(g + 8 + w * 4):F0}–{Memory.ReadFloat(g + 0x14 + w * 4):F0}]");
            }
            long b = BodyCollision.SlotBase(slot);
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c) - ex, ch = Memory.ReadFloat(c + 4) - eh, cy = Memory.ReadFloat(c + 8) - ey;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                float lo = Memory.ReadFloat(b + BodyCollision.Param1Array + part * BodyCollision.BodyPartStride), hi = Memory.ReadFloat(b + BodyCollision.Param2Array + part * BodyCollision.BodyPartStride);
                int pct = owner >= 0 && owner < 6 ? Memory.ReadInt(b + BodyCollision.DamagePctArray + part * BodyCollision.DamagePctStride + owner * 4) : -1;
                sb.Append($"; sphere {part} d {(float)Math.Sqrt(cx * cx + ch * ch + cy * cy):F1} ≤ {r + er:F1}? win {lo:F0}–{hi:F0} pct {pct}");
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + sb);
        }

        private static void TakeDown()
        {
            if (!_rockUp) return;
            _rockUp = _falling = _bouncing = false; _headArmed = false;
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteUInt(CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, 0);
            Memory.WriteInt (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 0);
            FallRowsOff();
            Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);                      // passable again
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

        /// <summary>The nut landed on the head and the cave turned it into the armed hop that frame: the hop's own gravity, then the
        /// hit — BonkShare× the attack as a melee-style hit (the entry's own reaction and the vanilla melee kick from the player; the
        /// engine bills its weapon HP as for any hit) — the stars on the enemy, the enemy confused, the darkening released, the green
        /// fading.</summary>
        private static void Bonk()
        {
            int slot = _target;
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, BonkGravity / 3600f);
            Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopped, 0);
            _falling = false; _bouncing = true; _headArmed = false;
            _ground = _hopGround;
            FallRowsOff();
            BladeProp.SetScale(FullScale);
            DimEnd();
            _tintFadeFrom = GameClock.Now;
            if (slot < 0 || !Enemies.IsLive(slot)) return;
            var sh = new Shell { What = "bonk", Slot = slot, Hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) };
            sh.Replant = PlantBonk;
            PlantBonk(sh);
            Confusion.Confuse(slot, GameClock.Now.AddSeconds(ConfusionHold));
            _bonked = slot;
        }

        /// <summary>The bonk's hit on <paramref name="sh"/>'s enemy: BonkShare× the attack, the vanilla melee kick from the player.</summary>
        private static bool PlantBonk(Shell sh)
        {
            int slot = sh.Slot;
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return false;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return false;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * BonkShare));
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(HitRadius, cr), attack, 0);
            BitConverter.GetBytes(0).CopyTo(e, CollisionPool.AbilityFlags);   // no poison, stop, critical, steal or drain from a drop
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x80, Memory.ReadFloat(Addresses.dunPositionX)); F(0x84, Memory.ReadFloat(Addresses.dunPositionZ)); F(0x88, Memory.ReadFloat(Addresses.dunPositionY));
            F(0x90, MeleeKickWords.VanillaStrength12); F(0x94, MeleeKickWords.VanillaDecay12);   // a plain melee hit's kick, from the player
            BitConverter.GetBytes(2).CopyTo(e, 0x98);
            BitConverter.GetBytes(CodeCaves.CrushMark).CopyTo(e, CodeCaves.NoDrainMarkOff);   // through any guard (a Xiao hit is still billed: the no-drain caves are Ungaga's)
            CollisionPool.Plant(pool, idx, e);
            sh.Idx = idx; sh.Ticks = ShellLifeTicks; sh.Tries++;
            _shells.Add(sh);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bonk{(sh.Tries > 1 ? $" (again, {sh.Tries})" : "")}: enemy slot {slot} for {attack} (entry {idx}) — planted at ({cx:F0},{ch:F0},{cy:F0}) r {Math.Max(HitRadius, cr):F1}");
            return true;
        }

        // ── the target's darkening ──
        private static void DimStart(int slot)
        {
            DimEnd();
            if (slot < 0 || !Enemies.IsLive(slot)) return;
            _dimSlot = slot;
            long c = EnemyAddresses.CharObjects.CharAddr(slot);
            Memory.WriteFloat(c + CCharacter.DimFloor, 1f);
            Memory.WriteInt  (c + CCharacter.DimOn, 1);
        }

        /// <summary>The darkened enemy's floor (the step eases its DimFactor there); released at once if it has died.</summary>
        private static void DimTo(float floor)
        {
            int slot = _dimSlot;
            if (slot < 0) return;
            if (!Enemies.IsLive(slot)) { DimEnd(); return; }
            Memory.WriteFloat(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.DimFloor, floor);
        }

        /// <summary>Its own lighting back: dimming off (the step eases DimFactor back to 1.0), the floor at 1.</summary>
        private static void DimEnd()
        {
            int slot = _dimSlot;
            if (slot < 0) return;
            _dimSlot = -1;
            long c = EnemyAddresses.CharObjects.CharAddr(slot);
            Memory.WriteInt  (c + CCharacter.DimOn, 0);
            Memory.WriteFloat(c + CCharacter.DimFloor, 1f);
        }

        // ── the shockwave (the rock's) ──
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
            if (_xiao && _bonked >= 0) { Confusion.Unconfuse(_bonked); _bonked = -1; }
            long pool = CollisionPool.Resolve();
            foreach (var sh in _shells) if (pool != 0) { Memory.WriteInt(pool + sh.Idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, sh.Idx); }
            _shells.Clear();
            SolarBlade.Clear();
            _phase = Phase.Idle; _guardSince = default; _tintFadeFrom = default;
            if (_xiao) KinomiModel.Forget(); else IwaModel.Forget();
            GroundShadow.Forget();                                                        // a floor change empties the cash: loaded again when next wanted
        }
    }
}
