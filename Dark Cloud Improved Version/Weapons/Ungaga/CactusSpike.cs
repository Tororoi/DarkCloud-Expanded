using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Cactus — "Desert Bloom": its guard raises a giant copy of the cactus out of the ground <see cref="AheadDistance"/> in front of Ungaga,
    /// point up with only its top showing, as Babel's Spear raises its spear (<see cref="BabelsSpear"/>, whose pieces this reuses):
    /// the guard pose held as long as the Mirage's clone takes to appear (Mirage.GuardChargeMs) summons it, one per hold; a new hold
    /// while one stands takes that one down at once and raises it anew at the new spot, as a new Mirage cast replaces the
    /// decoy. It rises in a poof of smoke — e03's cutscene effect e228ex (baked onto the dead dun/effect/zibaku_t
    /// name and borrowed into the second main-character instance, which the second-effect caves step and draw), its one clip
    /// (frames 1–50) at 0.6 (its own is 0.2), half size, turned to Ungaga's facing, faded out by the mod from frame <see cref="FxFadeFrom"/> to <see cref="FxEnd"/> (SubShotFade).
    /// In it the cactus comes out at <see cref="SpawnScale"/>× by the clip's frame <see cref="FxEmergedFrame"/> (the blade-fall
    /// cave's ease on the engine's frames), then grows linearly to <see cref="PeakScale"/>× by frame <see cref="FxPeakFrame"/> and
    /// settles back to <see cref="Scale"/>× by frame <see cref="FxRisenFrame"/> (a touch of squash and stretch), its root moved with the scale so the same length of it stays out and its base on the spot. It stands without turning, is
    /// solid to enemies, the player and enemy shots (the spear-block caves, <see cref="BlockRadius"/>), and every enemy touching it takes
    /// Babel's spike rate — a sixth of the weapon's attack every <see cref="HitSeconds"/>, no throw, no weapon HP. It stands
    /// <see cref="StandSeconds"/> from the summon, then fades out over <see cref="FadeSeconds"/> (solid and hurting until gone).
    ///
    /// The copy is <see cref="BladeProp"/>'s (untinted: it draws in the room's own light), in one of two <see cref="Form"/>s:
    ///  · UNGAGA's (<see cref="CactusForm"/>): the equipped Cactus, baked point-up — c10w13 runs −9.7 … +11.7 along its axis (the
    ///    tip, dcol0, at 11.68) — at 3×, 6.8 model units of its top above the floor;
    ///  · SUPER STEVE's with a Cactus sphere (<see cref="TreesForm"/>): Queens' two trees and their grass, whole (<see cref="QueensTrees"/>,
    ///    in the item-model cash — not Ungaga's weapon, and none of Xiao's own memory), authored upright, all of it out (74.3 tall),
    ///    at 0.7×, turned +90° from Xiao's facing, the grass square's centre (model −6, −1) on the spot — the part's root is at the
    ///    model's origin and a copy's root translation is the chara slot's position, so the offset is the root's, turned and scaled.
    ///    It hurts nothing and casts no shadow: solid to enemies, the player and enemy shots, it is a wall for a ranged fighter to
    ///    shoot from behind.
    /// The scales run in the same proportions (a thirtieth of full size out, 3.4 / 3 at the peak).</summary>
    internal static class CactusSpike
    {
        private const string Tag = "[CactusSpike] ";
        private const int    TickMs         = 16;
        private const float  AheadDistance  = 10f;
        private const float  SpawnRatio     = 0.1f / 3f;   // it emerges at this share of its full size…
        private const float  PeakRatio      = 3.4f / 3f;   // …overshooting to this share before it settles (a touch of squash and stretch)
        private const float  Buried         = 4f;      // how far below the floor the tip starts

        /// <summary>What rises: its model, full size, the model's top along the rise axis, how much of that stands above the floor,
        /// where its base sits along the model's own z (set on the spot), the solid column's radius at full size, its turn
        /// about the vertical from the wielder's facing, and its round shadow's radius and centre along the model's z (model units).</summary>
        private sealed record Form(string Name, float Scale, float Top, float ExposedModel, float BaseX, float BaseZ, float BlockRadius, bool Trees, float Turn,
                                   float ShadowRadius, float ShadowZ);
        private static readonly Form CactusForm = new("cactus", 3f,   11.7f, 6.8f,  0f, 0f, 6f, false, 0f, 1.8f, 0f);         // ±2.5 across at 1× with its arms (±7.5 at 3×); the shadow its body's 1.8 (spikes 2.25–2.75 left out)
        // Queens' two trees and their grass, whole: centred on the spot by the grass square's centre (model −6, −1); the column covers
        // both trunks (their bases 21.5 model units apart, ~15 at 0.7×, about the spot); turned a quarter from the facing; no shadow.
        private static readonly Form TreesForm  = new("Queens trees", 0.7f, 74.3f, 74.3f, -6f, -1f, 9.5f, true, (float)(Math.PI / 2), 0f, 0f);
        private static Form F = CactusForm;
        private static bool _xiao;
        private static float SpawnScale => F.Scale * SpawnRatio;
        private static float PeakScale  => F.Scale * PeakRatio;
        private static float Scale      => F.Scale;
        private static float Exposed    => F.ExposedModel * F.Scale;
        // e228ex's one KEY (its cfg): frames 1–50 at 0.2.
        private const float  FxStart = 1f, FxEnd = 50f, FxRate = 0.6f, FxFadeFrom = 32f;   // played at 0.6, three times its own speed
        private const float  FxScale = 0.5f;
        private const float  FxEmergedFrame = 6.5f;                                 // the clip's frame the small cactus is fully out at…
        private const float  FxPeakFrame    = 10.6f;                                // …at PeakScale at (game frame 16 at 0.6: the settle then spans ~2⅓ game frames)…
        private const float  FxRisenFrame   = 12f;                                  // …and settled to Scale at
        private const float  EmergeSeconds  = (FxEmergedFrame - FxStart) / FxRate / 60f;   // 0.153 s (9.2 engine frames)
        private const float  PeakSeconds    = (FxPeakFrame - FxStart) / FxRate / 60f;      // 0.267 s (16 engine frames)
        private const float  RisenSeconds   = (FxRisenFrame - FxStart) / FxRate / 60f;     // 0.306 s (18.3 engine frames)
        private const int    FxTemplate     = 5;       // a stock config's shape; the name and motions are replaced
        private const string FxName         = "zibaku_t";
        private const float  StandSeconds   = 10f;
        private const float  FadeSeconds    = 0.5f;
        private static float BlockRadius => F.BlockRadius;
        private const float  HitReach       = 2f;      // a body sphere this far past the column counts as touching (Babel's)
        private const float  HitShare       = 1f / 6f; // Babel's spike: a sixth of the weapon's attack…
        private const double HitSeconds     = 0.25;    // …every 60° of its 240°/s turn
        private const float  HitRadius      = 6f;
        private const int    ShellLifeTicks = 10;      // ticks (16 ms) a planted hit stays before it is withdrawn
        private const int    ShellPoolReserve = 16;    // free pool entries always left to the engine

        private static bool     _up, _emerged, _peaked, _risen;
        private static float    _curScale;                     // the copy's scale now (its shadow follows it)
        private static DateTime _summoned, _lastHit;
        private static float    _sx, _sy, _ground, _yaw;
        private static bool     _guardLatched; private static DateTime _guardSince;
        private static readonly List<(int idx, int ticks)> _shells = new();
        private static BorrowedEffect _fx;
        private static int _sub = -1;                  // the sub-shot playing the smoke (−1 = none)

        /// <summary>Desert Bloom's wielder: Ungaga with the Cactus, or Xiao with Super Steve and a Cactus sphere.</summary>
        internal static bool Wielded()
        {
            int ch = Player.CurrentCharacterNum();
            if (ch == Player.UngagaId) return Player.Weapon.GetCurrentWeaponId() == Items.cactus;
            if (ch == Player.XiaoId) return Player.Weapon.GetCurrentWeaponId() == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.cactus;
            return false;
        }

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the smoke, whenever Desert Bloom is
        /// wielded (<see cref="Wielded"/>). Every phase radius zeroed: it is the visual only. Its one clip is the muzzle motion: the engine
        /// retires the sub-shot at the clip's last frame (49–50), where the mod's fade has already reached nothing.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded()) return null;
            if (_fx == null)
            {
                _fx = BorrowedShots.CustomConfig(FxTemplate, FxName, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                                 dir: BorrowedShots.EffectDir, instance: ShotEffectPack.CharaMainEffectCrash);
                if (_fx == null) return null;
                for (int ph = 0; ph < 4; ph++) BorrowedShots.SetPhaseRadius(_fx, ph, 0f);
            }
            return _fx;
        }

        public static void SpikeEffect()
        {
            int ch = Player.CurrentCharacterNum();
            _xiao = ch == Player.XiaoId;
            F = _xiao ? TreesForm : CactusForm;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"guard {Mirage.GuardChargeMs} ms → a {F.Name} rises {AheadDistance:F0} ahead for {StandSeconds:F0} s");
            try
            {
                // Ends the moment the active character changes — checked every tick, a menu open or not: the copy's slot was
                // cloned from this character's objects, and an ally switch reloads them under it (drawn behind the menu, it read
                // freed memory and reset the game). A Cactus sphere on Super Steve keeps Wielded() true across the switch, so the
                // character is what is watched; the thread starts again for the new one, with its own form.
                while (Wielded() && Player.InDungeonFloor() && Player.CurrentCharacterNum() == ch)
                {
                    GroundShadow.Guard();                                                   // its shadow off the moment play stops (menus load over its memory)
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Charge();
                        if (_up) { Drive(); if (_xiao && _up) QueensTrees.KeepTextures(); }   // the trees' textures re-sent through the copy's pass
                        RetireShells();
                    }
                    Thread.Sleep(TickMs);
                }
            }
            finally { End(); }
        }

        private static void Charge()
        {
            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int  mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool inPose = guarding && (mid == Mirage.GuardLoopMotion || mid == Mirage.GuardMoveMotion);
            if (!guarding) { _guardLatched = false; _guardSince = default; return; }   // released: a new hold can summon again
            if (!inPose || _guardLatched) return;
            if (_guardSince == default) { _guardSince = GameClock.Now; return; }
            if ((GameClock.Now - _guardSince).TotalMilliseconds < Mirage.GuardChargeMs) return;
            _guardLatched = true;
            Summon();
        }

        private static void Summon()
        {
            if (_up) { TakeDown(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"re-summoned: the standing {F.Name} moves to the new spot"); }
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            _sx = px + (float)Math.Sin(_yaw) * AheadDistance; _sy = py + (float)Math.Cos(_yaw) * AheadDistance;
            _ground = DungeonFloor.HeightAt(_sx, _sy, ph + 20f, out float fh) ? fh : ph;   // the floor there (a step or a ramp ahead), else his
            _emerged = _peaked = _risen = false;
            uint root = 0;
            if (F.Trees && (root = QueensTrees.Root()) == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no Queens trees model in the item cash — nothing rises"); return; }
            if (!BladeProp.Spawn(SpawnScale, root, pointDown: false, pointUp: !F.Trees)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no {F.Name} copy (slot or cave busy)"); return; }
            Memory.WriteFloat(CodeCaves.BladeSpin, 0f);                                     // it never turns
            var (rx, ry) = RootXY(SpawnScale);
            _curScale = SpawnScale;
            BladeProp.Place(rx, RootHeight(0f, SpawnScale), ry, CopyYaw);
            BladeProp.Alpha(1f);
            // The rise, on the engine's frames (Babel's): the blade-fall cave steps y −= vy, vy += g each frame; v0 = 2D/T and
            // g = v0/T reach zero together at the top. Its stop test is disarmed with a stop far below.
            float frames = EmergeSeconds * 60f, d = RootHeight(1f, SpawnScale) - RootHeight(0f, SpawnScale), v0 = 2f * d / frames, g = v0 / frames;
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, RootHeight(0f, SpawnScale));
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, -v0);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, g);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, -1e9f);
            Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFalling);
            _up = true; _summoned = GameClock.Now; _lastHit = default;
            FxStartPlay();
            Player.FlashChargeComplete();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"a {F.Name} rises at ({_sx:F0},{_sy:F0}) floor {_ground:F1} (the wielder at {ph:F1})");
        }

        /// <summary>The copy's root height at scale <paramref name="s"/> for an emergence fraction <paramref name="t"/> (0: the tip
        /// Buried below the floor; 1: the form's ExposedModel of it above) — at t = 1 the same model length stands out at any size,
        /// so the growth keeps the top where the floor cuts it.</summary>
        private static float RootHeight(float t, float s) => _ground - F.Top * s + (-Buried + (F.ExposedModel * s + Buried) * t);

        /// <summary>The copy's root across the ground at scale <paramref name="s"/>: the model point (BaseX, BaseZ) — the form's centre —
        /// turned with the facing the copy is placed at (CopyYaw; the engine's RotMatrixY: local +x → (cos, −sin), local +z → (sin, cos)
        /// across the ground) and set on the spot.</summary>
        private static (float x, float y) RootXY(float s)
        {
            float c = (float)Math.Cos(CopyYaw), sn = (float)Math.Sin(CopyYaw);
            return (_sx - (c * F.BaseX + sn * F.BaseZ) * s, _sy - (-sn * F.BaseX + c * F.BaseZ) * s);
        }

        /// <summary>The copy's yaw: the wielder's facing at the summon plus the form's turn, wrapped to ±π — the engine's
        /// angle-to-matrix diverges past that (the blade-spin cave wraps for the same reason), and a facing past π/2 plus the trees'
        /// quarter turn landed there, turning about a quarter of the summons wrong.</summary>
        private static float CopyYaw
        {
            get
            {
                float y = _yaw + F.Turn;
                while (y >  MathF.PI) y -= 2 * MathF.PI;
                while (y < -MathF.PI) y += 2 * MathF.PI;
                return y;
            }
        }

        private static void Drive()
        {
            if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); TakeDown(); return; }
            double age = (GameClock.Now - _summoned).TotalSeconds;
            if (age >= StandSeconds + FadeSeconds) { TakeDown(); return; }
            FxDrive();
            if (!_emerged && age >= EmergeSeconds)
            {   // out at its small size: the integrator off, the height the mod's from here
                Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
                _emerged = true;
            }
            if (_emerged && !_risen)
            {   // the growth, linear in time: SpawnScale → PeakScale, then back to Scale; the root moved with it so the form's
                // ExposedModel stays out and its base on the spot. The peak is always shown for at least one tick, however the ticks fall.
                float sc;
                if (age < PeakSeconds || !_peaked)
                {
                    float u = (float)Math.Clamp((age - EmergeSeconds) / (PeakSeconds - EmergeSeconds), 0.0, 1.0);
                    sc = SpawnScale + (PeakScale - SpawnScale) * u;
                    if (u >= 1f) _peaked = true;
                }
                else
                {
                    float u = (float)Math.Clamp((age - PeakSeconds) / (RisenSeconds - PeakSeconds), 0.0, 1.0);
                    sc = PeakScale + (Scale - PeakScale) * u;
                    if (u >= 1f) _risen = true;
                }
                BladeProp.SetScale(sc);
                _curScale = sc;
                var (rx, ry) = RootXY(sc);
                BladeProp.Place(rx, RootHeight(1f, sc), ry, CopyYaw);
                if (_risen) Solid();
            }
            BladeProp.Alpha(age < StandSeconds ? 1f : (float)Math.Max(0.0, 1.0 - (age - StandSeconds) / FadeSeconds));
            if (age < StandSeconds) Shadow(); else GroundShadow.Hide();                          // gone as it starts to fade
            if (_risen && !F.Trees && (GameClock.Now - _lastHit).TotalSeconds >= HitSeconds)   // the trees only block: Xiao fights from range
            {
                _lastHit = GameClock.Now;
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                {
                    if (!Enemies.IsLive(s)) continue;
                    if (BigBang.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), _sx, _sy) > BlockRadius + HitReach) continue;
                    Hit(s);
                }
            }
        }

        /// <summary>Its round shadow on the floor (GroundShadow): the form's radius at the copy's scale now, centred at the form's shadow
        /// point along its model z, turned and scaled with the copy; none for a form without one.</summary>
        private static void Shadow()
        {
            if (F.ShadowRadius <= 0f) return;                                                    // none (Queens' trees)
            float d = (F.ShadowZ - F.BaseZ) * _curScale;
            GroundShadow.Show(_sx + (float)Math.Sin(CopyYaw) * d, _ground, _sy + (float)Math.Cos(CopyYaw) * d, F.ShadowRadius * _curScale);
        }

        /// <summary>Grown and settled: the column armed (enemies, the player and enemy shots) at the full size.</summary>
        private static void Solid()
        {
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, _sx);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, _ground);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, _sy);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, BlockRadius);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockTop, _ground + Exposed);   // enemy shots stop below its top
            Memory.WriteInt  (CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 1);
        }

        /// <summary>One player-hit sphere on a touching enemy's body at a sixth of the weapon's attack, no throw, marked so the
        /// ISO's no-drain caves bill no weapon HP for it (Babel's spike).</summary>
        private static void Hit(int slot)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Math.Max(1, (int)Math.Round(Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset) * HitShare));
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(HitRadius, cr), attack, 0);
            BitConverter.GetBytes(CodeCaves.NoDrainMark).CopyTo(e, CodeCaves.NoDrainMarkOff);
            CollisionPool.Plant(pool, idx, e);
            _shells.Add((idx, ShellLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pricked enemy slot {slot} for {attack} (entry {idx})");
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

        // ── the smoke ──
        private static long FxObj => _fx.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        private static void FxStartPlay()
        {
            _sub = -1;
            if (_fx == null || !BorrowedShots.Entered(_fx)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "smoke not entered on this floor — the cactus rises without it"); return; }
            if (!BorrowedShots.Burst(_fx, _sx, _ground, _sy, 0, FxScale)) return;
            _sub = Memory.ReadInt(_fx.Instance + ShotEffectPack.OffLastIdx);
            long o = FxObj;
            SubShotFade.Set(o, 1f);                                                   // a sub-shot a previous smoke faded, back to full
            Memory.WriteInt  (o + ShotEffectPack.ObjMotId, 0);
            Memory.WriteInt  (o + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat(o + ShotEffectPack.ObjFrame, FxStart);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, FxRate);                  // absolute (its own KEY is 0.2)
            TurnRoot(o);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                           // the second instance stepped and drawn (the second-effect caves)
        }

        /// <summary>The smoke turned to Ungaga's facing about the vertical through its model's ROOT frame (null7: no motion track
        /// touches it), its local 3×3 set to the engine's RotMatrixY — row 0 = (cos, 0, −sin), row 2 = (sin, 0, cos), so its local +Z faces
        /// (sin, cos), the forward the summon steps along — and its world
        /// cache dropped, as BladeProp bakes its copies upright. An absolute write: the sub-shot's model is reused burst to burst.
        /// (The sub-shot object's own Euler angles, +0x60, do not reach a shot effect's draw.)</summary>
        private static void TurnRoot(long obj)
        {
            uint root = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
            if (!Memory.IsValidGuest(root)) return;
            long r = Memory.ToMmu(root);
            float c = (float)Math.Cos(_yaw), sn = (float)Math.Sin(_yaw);                  // Ungaga's facing, the one the cactus was summoned along
            float[] m = { c, 0f, -sn,  0f, 1f, 0f,  sn, 0f, c };
            for (int i = 0; i < 9; i++) Memory.WriteFloat(r + CFrameVu1.LocalMatrix + (i / 3) * 0x10 + (i % 3) * 4, m[i]);
            Memory.WriteInt(r + CFrameVu1.WorldCacheA, 0);
        }

        /// <summary>The smoke each tick: held at the cactus, faded from frame FxFadeFrom to nothing at FxEnd; the engine retires it.</summary>
        private static void FxDrive()
        {
            if (_sub < 0 || !Player.CheckDunIsWalkingMode()) return;
            long o = FxObj;
            if (Memory.ReadUShort(_fx.Instance + ShotEffectPack.OffActive + _sub * 2) == 0) { FxEnd_(); return; }
            Memory.WriteVec3(o + ShotEffectPack.ObjPos, _sx, _ground, _sy);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, FxRate);                   // held: nothing resets it mid-clip, but a phase change would
            float frame = Memory.ReadFloat(o + ShotEffectPack.ObjFrame);
            if (frame >= FxFadeFrom) SubShotFade.Set(o, 1f - Math.Clamp((frame - FxFadeFrom) / (FxEnd - FxFadeFrom), 0f, 1f));
        }

        private static void FxEnd_()
        {
            if (_sub >= 0 && _fx != null)
            {
                Memory.WriteUShort(_fx.Instance + ShotEffectPack.OffActive + _sub * 2, 0);
                SubShotFade.Set(FxObj, 1f);                                           // handed back at full
            }
            _sub = -1;
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
        }

        private static void TakeDown()
        {
            FxEnd_();
            if (!_up) return;
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
            Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);             // passable again
            GroundShadow.Hide();
            BladeProp.Despawn();
            if (_xiao) QueensTrees.ReleaseTextures();                                        // the trees' textures back in the cash's own block
            _up = false; _emerged = _peaked = _risen = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {F.Name} is gone");
        }

        private static void End()
        {
            TakeDown();
            long pool = CollisionPool.Resolve();
            foreach (var (idx, _) in _shells) if (pool != 0) { Memory.WriteInt(pool + idx * CollisionPool.Stride + CodeCaves.NoDrainMarkOff, 0); CollisionPool.Deactivate(pool, idx); }
            _shells.Clear();
            _guardLatched = false; _guardSince = default;
            if (_xiao) QueensTrees.Forget();
            GroundShadow.Forget();                                                   // a floor change empties the cash: loaded again when next wanted
        }
    }
}
