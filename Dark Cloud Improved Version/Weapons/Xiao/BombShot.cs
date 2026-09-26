using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Big Bang sphere — "Detonate" from Xiao's slingshot, as close to Big Bang's as her weapon
    /// allows. A guard charge (<see cref="ChargeSeconds"/>) whitens the slingshot, darkens the room to Big Bang's dim and
    /// runs the cyan build-up on her; primed, she holds the white. LOCKED ON, a ten-times Bomb — the item's own model,
    /// tinted grey with the Matador's red-orange disc on it — hangs over the target where Big Bang hangs its judgement
    /// blade, and her next shot lets it fall: it lands with the bomb's own blast at ten times its size and exactly Big
    /// Bang's blast — the falloff damage, the kick, every enemy turned to it — and Big Bang's flash. NOT locked on, the
    /// next pellet IS a bomb: the model at five times, the disc on it, flying the pellet's own line; landing on an enemy
    /// it bursts at five times with half the blast's damage and kick, and the flash. Her white bleeds out over
    /// <see cref="TintFadeSeconds"/> once the shot leaves. Weapon HP is the shot's, taken as the pellet leaves: 20 for the
    /// drop, 10 for the bomb shot. Explosions cannot hurt her while the sphere is on. Driven from Super Steve's sphere
    /// dispatch; Solar Harvest is inherited alongside.</summary>
    internal static class BombShot
    {
        private const string Tag = "[BombShot] ";
        private const double ChargeSeconds = 5.0;
        private const float  HoverScale = 10f, ShotScale = 5f;          // the bomb model over a target, and as the pellet
        private const float  HoverFxScale = 10f, ShotFxScale = 5f;      // the bomb's blast visual, likewise
        private const float  BombLength = 4f;                           // the bomb model's reach below its root at 1× (it sits on the ground by it)
        private const float  ShotDamage = 0.5f, ShotKick = 0.5f;        // the bomb shot's blast against the drop's
        private const float  DropWhp = 20f, ShotWhp = 10f, SwingBase = 1.5f;
        private const double TintFadeSeconds = 0.25, MissSeconds = 3.0;
        private const float  HitProximity = 40f;
        private const string GlowDisc = "catglowp";
        private const int    GlowFireRow = 1;                           // the Matador's red-orange
        private const float  GlowSize = 0.6f;
        private static readonly float[] BombTint = { 150f, 150f, 150f };

        private static readonly BigBang.JudgementOwner Owner = new BigBang.JudgementOwner
        {
            WeaponId = Items.supersteve, Glow = GlowDisc, Profile = null, Redirect = true, RampWholeFall = true,
            IsPrimed = () => _phase == Phase.Primed || _phase == Phase.Dropping,
            Scale = HoverScale, Length = () => BombLength, SpawnRoot = BombModel.Root, Upright = true,
            Tint = BombTint, GlowRow = GlowFireRow, GlowScale = GlowSize, GlowLift = 0f,
            Land = LandDrop,
        };

        private enum Phase { Idle, Charging, Primed, Dropping, Flying, HitPending }
        private static Phase _phase;
        private static DateTime _holdStart, _firedAt, _hitAt;
        private static int   _slot = -1;
        private static float _lastX, _lastH, _lastY;
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static readonly List<(int slot, int ticks)> _planted = new List<(int, int)>();
        private static bool _nativeWarned;
        private static byte _floor = 0xFF;

        private static bool Native => (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew;

        internal static void Drive(bool active)
        {
            Owner.Profile ??= SunSword.BombShotFlash;
            SunSword.BlindTick();
            SunSword.ExpireHits(_planted);
            if (!active) return;
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _floor) { if (_floor != 0xFF) { BombModel.Forget(); if (_phase != Phase.Idle) Dissipate(); } _floor = floor; }
            BigBang.DriveImmunity(true);
            var p = SunSword.BombShotFlash;
            SolarLighting.ToanTintOwned = _phase == Phase.Charging || _phase == Phase.Primed;
            switch (_phase)
            {
                case Phase.Idle:
                    if (SunSword.BlindRunning) break;
                    if (GuardWatch.IsGuarding()) { _phase = Phase.Charging; _holdStart = GameClock.Now; }
                    break;

                case Phase.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding()) { Dissipate(); break; }
                    double held = (GameClock.Now - _holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / ChargeSeconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / ChargeSeconds));
                    ChargeTint.Ramp(ChargeSeconds - held);
                    if (held >= ChargeSeconds)
                    {
                        _phase = Phase.Primed; ChargeTint.Clear();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — locked on, the bomb hangs; the next shot drops it or IS one");
                    }
                    break;
                }

                case Phase.Primed:
                {
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim);
                    SunSword.HoldPrimedTint(p, 1f);
                    BigBang.JudgementTick(Owner);                                     // the bomb over a locked target, following it
                    bool hanging = BladeProp.Active && PlayerAction.LockHeld(out _);
                    ChargedShotWhp.Arm((hanging ? DropWhp : ShotWhp) / SwingBase);   // the shot's bill, taken by the engine as the pellet leaves
                    int slot = NewPellet();
                    if (slot < 0) break;
                    if (hanging && BigBang.BeginDrop())
                    {   // the shot lets the bomb fall (the pellet itself flies plain)
                        _phase = Phase.Dropping; _firedAt = GameClock.Now;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the hanging bomb is let go");
                    }
                    else Fire(slot);
                    break;
                }

                case Phase.Dropping:
                {
                    double since = (GameClock.Now - _firedAt).TotalSeconds;
                    float k = (float)Math.Max(0.0, 1.0 - since / TintFadeSeconds);
                    SunSword.HoldPrimedTint(p, k); SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    BigBang.JudgementTick(Owner);                                     // the fall, and the landing (LandDrop)
                    if (BigBang.TakeDropLanded()) { _phase = Phase.Idle; break; }
                    if (!BigBang.Dropping && !BigBang.LandingPending) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the drop was abandoned — the charge is spent"); Dissipate(); }
                    break;
                }

                case Phase.Flying:
                {
                    double since = (GameClock.Now - _firedAt).TotalSeconds;
                    float k = (float)Math.Max(0.0, 1.0 - since / TintFadeSeconds);
                    SunSword.HoldPrimedTint(p, k); SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarGlow.Tick(); BladeProp.Maintain();
                    long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                    bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, _slot)) != 0;
                    if (live)
                    {
                        long pa = PlayerShotPool.PosAddr(pool, _slot);
                        _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8);
                        if (since < MissSeconds) break;
                    }
                    if (!live && EnemyNear(_lastX, _lastY, HitProximity))
                    { _phase = Phase.HitPending; _hitAt = GameClock.Now; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bomb landed at ({_lastX:F0},{_lastH:F0},{_lastY:F0})"); break; }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (live ? "bomb still out after 3 s" : "bomb landed on nothing") + " — the charge is spent");
                    Dissipate();
                    break;
                }

                case Phase.HitPending:
                {
                    double frames = (GameClock.Now - _hitAt).TotalSeconds * 60.0;
                    SolarLighting.DimRamp(p.PrimeDim, (float)(frames / SolarLighting.RampFrames));
                    if (frames < SolarLighting.RampFrames) break;
                    SunSword.HoldPrimedTint(p, 0f); SolarBlade.Clear(); SolarGlow.Hide(); EndPellet();
                    Blast(_lastX, _lastH, _lastY, ShotFxScale, ShotDamage, ShotKick);
                    _phase = Phase.Idle;
                    break;
                }
            }
        }

        /// <summary>The bomb's blast at (x, h, y): the visual at <paramref name="fxScale"/>, Big Bang's falloff blast at
        /// <paramref name="damage"/>/<paramref name="kick"/> of its own, every enemy turned to it, and the flash from it.</summary>
        private static void Blast(float x, float h, float y, float fxScale, float damage, float kick)
        {
            BigBang.LastBlast = (x, h, y);
            BombFx.Spawn(x, h, y, fxScale);
            BigBang.PlantFalloff(x, h, y, damageScale: damage, kickScale: kick);
            BigBang.TurnEnemiesToward(x, y);
            SunSword.FlashAt(SunSword.BombShotFlash, x, h, y, _planted);
        }
        /// <summary>The hanging bomb's landing (BigBang's owner callback): the full blast.</summary>
        private static void LandDrop(int slot, float x, float h, float y) => Blast(x, h, y, HoverFxScale, 1f, 1f);

        private static int NewPellet()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return -1;
            int found = -1;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i]) { _seen[i] = true; if (found < 0) found = i; }
                else if (!live) _seen[i] = false;
            }
            return found;
        }

        /// <summary>The bomb shot: the pellet's sprite hidden, the bomb model (five times) placed on it each frame by the
        /// pellet-follow cave, the disc on the model.</summary>
        private static void Fire(int slot)
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8);
            _slot = slot; _firedAt = GameClock.Now; _phase = Phase.Flying;
            ChargeTint.Clear();
            BigBang.ReleaseJudgement();                                              // a hover fading off a lost lock: gone, the copy is the shot's now
            uint root = BombModel.Root();
            if (root == 0 || !Native)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (root == 0 ? "no bomb model — the pellet flies plain" : "the pellet-follow cave is not in this ISO — the pellet flies plain (re-patch the ISO)")); }
                return;
            }
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), 0.001f);         // the pellet's own sprite, hidden
            if (!BladeProp.Spawn(ShotScale, root, pointDown: false)) return;
            BladeProp.Tint(BombTint[0], BombTint[1], BombTint[2]);
            float yaw = (float)Math.Atan2(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
            BladeProp.Place(_lastX, _lastH, _lastY, yaw);
            BladeProp.Alpha(1f);
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowLift, 0f);
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowSpin, 0f);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowEnded, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowSlot, slot + 1);           // LAST: the cave places the copy from this frame
            SolarGlow.Show(GlowDisc, anchor: BladeProp.RootGuest, lift: 0f, palRow: GlowFireRow, scale: GlowSize);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bomb shot: pellet slot {slot}, the bomb ×{ShotScale:0} on it");
        }

        private static void EndPellet()
        {
            if (_slot < 0) return;
            Memory.WriteInt(CodeCaves.Mailbox.PropFollowSlot, 0);
            if (BladeProp.Active) BladeProp.Despawn();
            _slot = -1;
        }

        private static void Dissipate()
        {
            SunSword.HoldPrimedTint(SunSword.BombShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Fade(); SolarLighting.EndDim(); EndPellet();
            BigBang.ReleaseJudgement();
            _phase = Phase.Idle;
        }

        private static bool EnemyNear(float x, float y, float range)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long pos = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX);
                float dx = Memory.ReadFloat(pos) - x, dy = Memory.ReadFloat(pos + 8) - y;
                if (dx * dx + dy * dy <= range * range) return true;
            }
            return false;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended, explosions dangerous again.</summary>
        internal static void Stop()
        {
            if (_phase != Phase.Idle) SunSword.HoldPrimedTint(SunSword.BombShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.Restore(); EndPellet();
            BigBang.ReleaseJudgement(); BigBang.DriveImmunity(false);
            SunSword.EndBlinding();
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in _planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            _planted.Clear();
            SolarLighting.ToanTintOwned = false;
            Array.Clear(_seen, 0, _seen.Length);
            _phase = Phase.Idle; _slot = -1;
        }
    }
}
