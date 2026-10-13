using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The pockle's mind: its day's own script (dun\monstor\e1Na.stb, label 100 and its hit reaction, label 110) re-run as a
    /// coroutine one step per floor frame — each `yield` is the script's YIELD — while the game carries out its moves: a _SET_MOVE is a
    /// per-frame step and a frame count for tools/stubs/pockle_move.s (it keeps gliding after the script moves on, as the native move
    /// does), clips play natively. "The player" of the script is its target: the locked-on enemy, else the live enemy nearest Ruby; with
    /// none the script stops and it walks at Ruby's right. Its attack spheres (label 1's, in their clips' frame windows) touching the
    /// target land the species' damage as Ruby's hit (<see cref="EnemyHit.TryPlant"/>: her kill and ABS) and provoke the enemy, whose
    /// target pointer (<see cref="AggroTable"/>, the pockle's holder) goes to the pockle. Monsters' melee and shots that reach it cost it
    /// HP and start its hit reaction (Friday's and Saturday's super armour mid-attack kept); at 0 it falls. The scripts' player-only
    /// guards have no ally form and are left out; Tuesday's dart and Thursday's throw land as timed hits (no projectile drawn yet).</summary>
    internal static class PockleBrain
    {
        private const string Tag = "[Pocklekul/PockleBrain] ";

        private const float TargetRange = 150f, LeashRange = 200f;         // enemies this near Ruby are its; farther, let go
        private const float SideOffset = 14f, SideArrive = 3f;             // its place at Ruby's right, and how near counts as there
        private const float FollowSpeedMin = 0.5f, FollowSpeedMax = 2.4f, FollowGain = 0.08f, FollowRatePerSpeed = 0.68f;
        private const int   IdleClip = 0, WalkClip = 1, BackstepClip = 2, HurtClip = 8, DeathClip = 11, AttackClip = 13, KickClip = 14, LandClip = 15, ChargeClip = 16;
        private const float ReachAhead = 6f;                               // its attack spheres sit this far in front of its root
        private const float HitRadius = 7f, HitHeight = 14f, HitBelow = 2f;   // its body about its root
        private const int   PlantLifeFrames = 12, HurtDebounceFrames = 30, EntryDamage = 0x34;
        private const float ShotSpeed = 3f;                                // a dart's or a throw's flight, units a frame

        /// <summary>One of label 1's attack spheres: live on clip frames [From, To], Radius wide, Damage.</summary>
        private readonly record struct Sphere(float From, float To, float Radius, int Damage);

        private static readonly Random _rng = new();
        private static int _day, _hp, _maxHp, _defense;
        private static int _target = -1, _plant = -1, _plantLife;
        private static IEnumerator<int> _script;                           // label 100 (or 110 after a hit), null = none
        private static bool _reacting, _dying, _following;
        private static float _yawGoal, _turnRate;                          // the native turn: toward _yawGoal at _turnRate a frame (0 = none)
        private static bool _connected;                                    // HITDMG_VOL ≥ 0: a sphere landed since the last SetMotion
        private static float _prevFrame, _clipEnd;
        private static int[] _sinceHitBy = new int[0];
        private static readonly HashSet<int> _provoked = new();
        private static readonly Dictionary<int, (float x, float h, float y)> _shotLast = new();
        private static readonly List<(int slot, int frames, int damage)> _inFlight = new();

        internal static bool Dying => _dying;

        /// <summary>A pockle has just been stood: its day's script from the top, its species' HP and defence.</summary>
        internal static void Start(int day, EnemyDefaults species)
        {
            _day = Math.Clamp(day, 0, 6);
            _maxHp = _hp = species.MaxHp ?? 60;
            _defense = species.DamageReduction ?? 0;
            _target = -1; _plant = -1; _script = null; _reacting = _dying = _following = false; _turnRate = 0f; _connected = false;
            _sinceHitBy = new int[EnemyAddresses.FloorSlots.Count];
            for (int i = 0; i < _sinceHitBy.Length; i++) _sinceHitBy[i] = HurtDebounceFrames;
            _provoked.Clear(); _shotLast.Clear(); _inFlight.Clear();
            Log($"{species.Name}: {_hp} HP, {_defense} defence");
        }

        /// <summary>The pockle gone (fallen, dismissed, forgotten): still, and the enemies it provoked back on Ruby.</summary>
        internal static void Stop()
        {
            if (_provoked.Count > 0 || AggroTable.Held == AggroTable.Holder.Pockle) AggroTable.Release(AggroTable.Holder.Pockle);
            _provoked.Clear(); _target = -1; _plant = -1; _script = null; _inFlight.Clear();
        }

        /// <summary>One tick: <paramref name="frames"/> floor frames since the last, each one script step.</summary>
        internal static void Tick(int frames)
        {
            if (!PockleModel.Up) { Stop(); return; }
            for (int i = 0; i < _sinceHitBy.Length; i++) _sinceHitBy[i] += frames;
            WatchPlant();
            ReleaseTheDead();
            FlyShots(frames);

            if (_dying)
            {
                PockleModel.Step(0f, 0f, 0);
                if (Frame >= _clipEnd - 0.5f) { PockleModel.Despawn("fallen"); Stop(); }
                return;
            }
            TakeMeleeHits();
            if (!_dying) TakeShotHits();
            if (_dying) return;

            float rx = Memory.ReadFloat(Addresses.dunPositionX), rh = Memory.ReadFloat(Addresses.dunPositionZ), ry = Memory.ReadFloat(Addresses.dunPositionY);
            PockleModel.SetHeight(rh);

            // the target: the locked-on enemy, else the live one nearest Ruby; the script runs while it has one
            if (_target >= 0 && (!Enemies.IsLive(_target) || Dist(_target, rx, ry) > LeashRange)) _target = -1;
            if (!_reacting) _target = PickTarget(rx, ry, _target);
            if (_target < 0 && !_reacting)
            {
                if (!_following) { _script = null; _following = true; PockleModel.Step(0f, 0f, 0); _turnRate = 0f; }
                Follow(rx, ry, frames);
                return;
            }
            if (_following) { _following = false; PockleModel.Step(0f, 0f, 0); }
            _script ??= Label100();
            for (int f = 0; f < Math.Max(1, frames) && _script != null && !_dying; f++)
            {
                ApplyTurn();
                if (!_script.MoveNext()) { _script = null; _reacting = false; }
            }
        }

        // ───────────────────────────── no enemy: at Ruby's right ─────────────────────────────
        private static void Follow(float rx, float ry, int frames)
        {
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            float gx = rx + MathF.Cos(yaw) * SideOffset, gy = ry - MathF.Sin(yaw) * SideOffset;   // her right
            var (px, _, py) = PockleModel.Position;
            float dx = gx - px, dy = gy - py, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d <= SideArrive)
            {
                PockleModel.Step(0f, 0f, 0);
                PockleModel.SetMotion(IdleClip); PockleModel.SetRate(-1f);
                PockleModel.SetYaw(Blend(PockleModel.Yaw, yaw, MathF.Min(1f, 0.15f * frames)));
                return;
            }
            float speed = Math.Clamp(d * FollowGain, FollowSpeedMin, FollowSpeedMax);
            PockleModel.SetMotion(WalkClip); PockleModel.SetRate(speed * FollowRatePerSpeed);
            PockleModel.SetYaw(Blend(PockleModel.Yaw, MathF.Atan2(dx, dy), MathF.Min(1f, 0.15f * frames)));
            MoveTo(gx, gy, speed);
        }

        // ───────────────────────────── label 100: the seven loops ─────────────────────────────
        private static IEnumerator<int> Label100() => _day switch
        {
            0 => Sunday(), 1 => Monday(), 2 => Tuesday(), 3 => Wednesday(), 4 => Thursday(), 5 => Friday(), _ => Saturday(),
        };

        private static IEnumerator<int> Sunday()
        {
            int cd = 0;
            while (true)
            {
                float d = D();
                if (d > 70) foreach (var y in Approach(0.5f, 0.32f, 30, 60, true)) yield return y;
                yield return 0; d = D();
                if (d > 20 && d <= 70)
                {
                    if (R(100) < 70) foreach (var y in Approach(0.5f, 0.34f, 30, 20, true)) yield return y;
                    else { bool ok = PathClear(); yield return 0; if (ok) foreach (var y in DirectStep(0.5f, 0.42f)) yield return y; }
                    yield return 0;
                }
                if (d <= 40 && cd == 0) { if (R(100) <= 30) { foreach (var y in Reposition(0.45f, 40, 5)) yield return y; cd = 64; } yield return 0; }
                if (cd > 0) cd--;
                if (D() <= 22)
                {
                    if (R(1000) > 10) foreach (var y in Knife(0.56f)) yield return y;
                    else foreach (var y in Charge(12 + R(10), 0.3f, 1.1f, 0.2f, ramp: true, sat: false)) yield return y;
                }
                yield return 0;
            }
        }

        private static IEnumerator<int> Monday()
        {
            int cd = 0;
            while (true)
            {
                float d = D();
                if (d > 70) foreach (var y in Approach(0.34f, 0.30f, 30, 40, true)) yield return y;
                d = D();
                if (d >= 20 && d <= 70)
                {
                    if (R(100) < 70) foreach (var y in Approach(0.5f, 0.34f, 30, 20, true)) yield return y;
                    else { bool ok = PathClear(); yield return 0; if (ok) foreach (var y in DirectStep(0.54f, 0.44f)) yield return y; }
                }
                if (d <= 43 && cd == 0 && R(100) <= 30) { yield return 0; foreach (var y in Reposition(0.5f, 40, 20)) yield return y; cd = 60; }
                if (cd > 0) cd--;
                if (D() <= 21) foreach (var y in Spear(0)) yield return y;
                yield return 0;
            }
        }

        private static IEnumerator<int> Tuesday()
        {
            int cd = 0, t = R(20);
            while (true)
            {
                float d = D();
                if (d >= 100)
                {
                    foreach (var y in Approach(0.33f, 0.25f, 40, 80, true)) yield return y;
                    if ((t -= 4) <= 0) { foreach (var y in Shot(0.2f, 0.3f, 266f, 31, 8.4f, 1.5f)) yield return y; t = R(50) + 50; }
                }
                d = D();
                if (d > 42 && d <= 100)
                {
                    foreach (var y in Approach(0.35f, 0.26f, 30, 24, true)) yield return y;
                    bool ok = PathClear(); yield return 0; t -= ok ? 3 : 2;
                    if (t <= 0) { foreach (var y in Shot(0.2f, 0.3f, 266f, 31, 8.4f, 1.5f)) yield return y; t = R(60) + 50; }
                }
                if (d <= 56 && cd == 0) { int r = R(100); yield return 0; if (r <= 25) { foreach (var y in Reposition(0.4f, 50, 12)) yield return y; cd = 40; } }
                if (cd > 0) cd--;
                if (D() <= 45)
                {
                    t -= 6;
                    if (t <= 0) { foreach (var y in Shot(0.2f, 0.3f, 266f, 31, 8.4f, 1.5f)) yield return y; t = 120; }
                    else { int r = R(100); if (r < 15) foreach (var y in Stare(r + 90)) yield return y; }
                }
                t -= 1; yield return 0;
            }
        }

        private static IEnumerator<int> Wednesday()
        {
            int cd = 0;
            while (true)
            {
                float d = D();
                if (d > 90) foreach (var y in Approach(0.38f, 0.32f, 30, 60, true)) yield return y;
                d = D();
                if (d > 23 && d <= 90)
                {
                    if (R(100) < 30)
                    {
                        bool ok = PathClear(); yield return 0;
                        if (ok) foreach (var y in DirectStep(0.55f, 0.44f)) yield return y;
                        else foreach (var y in Approach(0.46f, 0.35f, 17, 24, true)) yield return y;
                    }
                    else foreach (var y in Approach(0.48f, 0.38f, 22, 24, true)) yield return y;
                }
                if (d <= 50 && cd == 0) { yield return 0; if (R(100) <= 30) { foreach (var y in Reposition(0.45f, 40, 12)) yield return y; cd = 90; } }
                if (cd > 0) cd--;
                if (D() <= 24)
                {
                    if (R(100) < 80) foreach (var y in Axe((R(2) + 3) * 0.1f)) yield return y;
                    else foreach (var y in Retreat(0.55f)) yield return y;
                }
                yield return 0;
            }
        }

        private static IEnumerator<int> Thursday()
        {
            int cd = 0, t = 0;
            while (true)
            {
                float d = D();
                if (d >= 90)
                {
                    foreach (var y in Approach(0.5f, 0.3f, 50, 80, true)) yield return y;
                    if ((t -= 1) <= 0) { foreach (var y in Throw()) yield return y; t = R(70) + 80; }
                }
                d = D();
                if (d > 24 && d < 90)
                {
                    foreach (var y in Approach(0.42f, 0.3f, 120, 26, true)) yield return y;
                    if (R(100) < 30)
                    {
                        if (InCone(40f, 90f)) { t -= 2; if (t < 0) { foreach (var y in Throw()) yield return y; t = R(50) + 60; } }
                        else t -= 1;
                    }
                    else t -= 2;
                    yield return 0;
                }
                if (d <= 58 && cd == 0 && R(100) <= 30) { yield return 0; foreach (var y in Reposition(0.42f, 50, 10)) yield return y; cd = 60; t += 1; }
                if (cd > 0) cd--;
                yield return 0;
                if (D() <= 30)
                {
                    if (R(100) < 75) { foreach (var y in Retreat(0.56f)) yield return y; t += 1; }
                    else
                    {
                        t -= 3;
                        if (t <= 0) { foreach (var y in Throw()) yield return y; t = R(36) + 60; }
                        else foreach (var y in Charge(10 + R(10), 1.0f, 1.0f, 0.3f, ramp: false, sat: false)) yield return y;
                    }
                }
                yield return 0; t -= 1;
            }
        }

        private static IEnumerator<int> Friday()
        {
            int cd = 0;
            while (true)
            {
                float d = D();
                if (d >= 90) foreach (var y in Approach(1.0f, 0.35f, 42, 50, true)) yield return y;
                d = D();
                if (d > 24 && d < 90)
                {
                    if (R(100) < 70) foreach (var y in Approach(0.58f, 0.40f, 25, 24, true)) yield return y;
                    else
                    {
                        bool ok = PathClear(); yield return 0;
                        if (ok)
                        {
                            foreach (var y in DirectStep(0.45f, 0.42f)) yield return y;
                            int r = R(1000);
                            if (r < 9) foreach (var y in Charge(R(10) + r, 0.85f, 0.85f, 0.3f, ramp: false, sat: false)) yield return y;
                        }
                        else foreach (var y in Approach(0.5f, 0.40f, 60, 24, false)) yield return y;
                    }
                }
                if (d <= 60 && cd == 0 && R(100) <= 30) { foreach (var y in Reposition(0.5f, 40, 5)) yield return y; cd = 60; }
                if (cd > 0) cd--;
                if (D() <= 28)
                {
                    if (R(100) < 95) foreach (var y in JumpKick(0.2f, 1.2f, turnInCrouch: true)) yield return y;
                    else foreach (var y in Retreat(0.45f)) yield return y;
                }
                yield return 0;
            }
        }

        private static IEnumerator<int> Saturday()
        {
            int cd = 0;
            while (true)
            {
                float d = D();
                if (d >= 90) foreach (var y in Approach(0.4f, 0.3f, 35, 60, true)) yield return y;
                yield return 0; d = D();
                if (d > 24 && d < 90)
                {
                    if (R(50) < 15) foreach (var y in Approach(0.54f, 0.37f, 25, 26, true)) yield return y;
                    else if (PathClear()) { foreach (var y in DirectStep(0.45f, 0.42f)) yield return y; }
                    else yield return 0;
                    int r = R(1000);
                    if (r < 6) foreach (var y in Charge(R(10) + r, 0f, 0.8f, 0f, ramp: true, sat: true)) yield return y;
                }
                yield return 0;
                if (d <= 60 && cd == 0 && R(100) <= 30) { foreach (var y in Reposition(0.5f, 40, 5)) yield return y; cd = 60; }
                if (cd > 0) cd--;
                if (D() <= 30) foreach (var y in Punch()) yield return y;
                yield return 0;
            }
        }

        // ───────────────────────────── label 110: hit reactions ─────────────────────────────
        /// <summary>Whether the day ignores a hit right now (Friday mid-kick, Saturday mid-punch): HP is still lost.</summary>
        private static bool SuperArmour()
        {
            int m = PockleModel.Motion; float f = Frame;
            if (_day == 5) return m == AttackClip || m == KickClip;
            if (_day == 6) return m == AttackClip && f >= 265f && f <= 280f;
            return false;
        }

        private static IEnumerator<int> Label110()
        {
            Cancel();
            switch (_day)
            {
                case 0:
                    foreach (var y in PlayOnce(HurtClip, 0.25f)) yield return y;
                    if (R(100) > 50 && D() < 26) foreach (var y in Knife(0.44f)) yield return y;
                    else foreach (var y in Retreat(0.5f)) yield return y;
                    break;
                case 1:
                    foreach (var y in PlayOnce(HurtClip, 0.4f)) yield return y;
                    if (R(100) < 80 || D() > 30) foreach (var y in Retreat(0.54f)) yield return y;
                    else foreach (var y in Spear(0)) yield return y;
                    break;
                case 2:
                    foreach (var y in PlayOnce(HurtClip, 0.25f)) yield return y;
                    if (D() <= 24 && R(100) < 30) foreach (var y in Charge(R(8) + 10, 0.3f, (R(5) + 9) * 0.1f, 0.2f, ramp: true, sat: false)) yield return y;
                    foreach (var y in Retreat(0.45f)) yield return y;
                    break;
                case 3:
                    foreach (var y in PlayOnce(HurtClip, -1f)) yield return y;
                    foreach (var y in Axe(R(6) == 0 ? 0.52f : 0.42f)) yield return y;
                    break;
                case 4:
                {
                    foreach (var y in PlayOnce(HurtClip, -1f)) yield return y;
                    int r = R(100);
                    if (r < 50 || D() <= 28) { foreach (var y in Charge(R(10) + r + 5, 0.9f, 0.9f, 0.3f, ramp: false, sat: false)) yield return y; break; }
                    int r2 = R(100); bool came = false;
                    if (r2 > 60) { foreach (var y in Approach(0.8f, 0.38f, 30, 60, true)) yield return y; came = true; }
                    if (r2 < 30) foreach (var y in Retreat(0.56f)) yield return y;
                    if (came)
                    {
                        float d = D();
                        if (d > 60) foreach (var y in Throw()) yield return y;
                        if (d < 36) foreach (var y in Charge(10 + R(10), 1.1f, 1.1f, 0.3f, ramp: false, sat: false)) yield return y;
                    }
                    break;
                }
                case 5:
                    foreach (var y in PlayOnce(HurtClip, -1f)) yield return y;
                    if (D() <= 28) foreach (var y in JumpKick(0.4f, 1.1f, turnInCrouch: false)) yield return y;
                    else if (R(100) < 70) foreach (var y in DirectStep(0.45f, 0.42f)) yield return y;
                    else foreach (var y in Retreat(0.45f)) yield return y;
                    break;
                default:
                    foreach (var y in PlayOnce(HurtClip, -1f)) yield return y;
                    if (D() <= 20) { foreach (var y in TurnUntil(TargetYaw(), 0.12f)) yield return y; foreach (var y in Punch(0.35f, 0.6f)) yield return y; }
                    else if (R(100) < 80) foreach (var y in DirectStep(0.45f, 0.42f)) yield return y;
                    else foreach (var y in Retreat(0.45f)) yield return y;
                    break;
            }
            SetMotion(IdleClip, -1f, false);
        }

        // ───────────────────────────── the shared primitives ─────────────────────────────
        /// <summary>APPROACH(speed, rate, spread, stop, retarget): legs of 30 units toward a point within ±spread° of the target's direction,
        /// a walk cycle each, re-aimed every second frame when <paramref name="retarget"/>; until nearer than <paramref name="stop"/>.</summary>
        private static IEnumerable<int> Approach(float speed, float rate, float spread, float stop, bool retarget)
        {
            int legs = 0;
            while (true)
            {
                if (stop > 0 && D() < stop) break;
                yield return 0;
                int tries = 0; float len = 30f, a = 0f; bool found = false;
                while (tries <= 10)
                {
                    if (spread >= 180) { spread = 180; if (legs > 10) break; legs += 3; }
                    yield return 0;
                    a = MathF.Floor((float)_rng.NextDouble() * spread); if (R(50) < 25) a = 360 - a;
                    var (gx, gy) = OffTarget(a, len);
                    bool path = PathClear(gx, gy);
                    TurnToward(gx, gy, 0.14f);
                    yield return 0;
                    if (path) { found = true; break; }
                    len -= 5; spread += 10; tries++;
                }
                if (!found || legs > 14) { yield return 0; break; }
                legs++;
                SetMotion(WalkClip, rate, once: true);
                var (px0, py0) = OffTarget(a, len); MoveTo(px0, py0, speed);
                int frame = 0;
                while (!ClipDone())
                {
                    yield return 0;
                    if (retarget && (++frame & 1) == 0) { var (rx, ry) = OffTarget(a, len); MoveTo(rx, ry, speed); TurnToward(rx, ry, 0.15f); }
                    if (stop > 0 && D() < stop) break;
                }
                spread -= MathF.Floor((float)_rng.NextDouble() * 5); if (spread <= 0) spread = 5;
                if (_day != 2) Cancel();
                yield return 0;
            }
            Cancel();
            yield return 0;
        }

        /// <summary>DIRECT STEP: a straight move 30 units toward the target, walking; it returns at once and the move glides on.</summary>
        private static IEnumerable<int> DirectStep(float speed, float rate)
        {
            var (px, _, py) = PockleModel.Position; var (tx, ty) = TargetPos();
            float dx = tx - px, dy = ty - py, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
            yield return 0;
            float gx = px + dx / l * 30f, gy = py + dy / l * 30f;
            MoveTo(gx, gy, speed); TurnToward(gx, gy, 0.12f); SetMotion(WalkClip, rate, once: false);
            yield return 0;
        }

        /// <summary>REPOSITION: a clear spot 13 units off at k·30° from its own facing; turn to it, walk there, and walk on until the target
        /// is <paramref name="far"/> away, it has moved that far, or <paramref name="cycles"/> walk cycles.</summary>
        private static IEnumerable<int> Reposition(float rate, float far, int cycles)
        {
            Cancel();
            float gx = 0, gy = 0;
            var (px, _, py) = PockleModel.Position;
            for (int i = 0; i < 4; i++)
            {
                float ang = PockleModel.Yaw + R(12) * MathF.PI / 6f;
                gx = px + MathF.Sin(ang) * 13f; gy = py + MathF.Cos(ang) * 13f;
                if (PathClear(gx, gy)) break;
            }
            SetMotion(WalkClip, rate, once: false);
            foreach (var y in TurnUntil(MathF.Atan2(gx - px, gy - py), 0.3f)) yield return y;
            MoveTo(gx, gy, 0.7f);
            int n = 0;
            while (true)
            {
                foreach (var y in WalkCycle()) yield return y;
                var (cx, _, cy) = PockleModel.Position;
                if (D() >= far || MathF.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py)) >= far || ++n >= cycles) break;
            }
            Cancel();
        }

        /// <summary>RETREAT: up to four backsteps of 20 units away from the target (or ±45° off that when blocked), facing it, until 40
        /// from where it started or from the target.</summary>
        private static IEnumerable<int> Retreat(float speed)
        {
            TurnToward(TargetPos(), 0.4f);
            yield return 0;
            var (sx, _, sy) = PockleModel.Position;
            for (int i = 0; i < 4; i++)
            {
                yield return 0;
                var (px, _, py) = PockleModel.Position;
                if (MathF.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy)) >= 40 || D() >= 40) break;
                foreach (float a in new[] { 180f, 225f, 135f })
                {
                    var (gx, gy) = OffTarget(a, 20f);
                    bool ok = PathClear(gx, gy); yield return 0;
                    if (!ok) continue;
                    MoveTo(gx, gy, speed); SetMotion(BackstepClip, 0.23f, once: true);
                    yield return 0;
                    while (!ClipDone()) { TurnToward(TargetPos(), 0.08f); yield return 0; }
                    break;
                }
                Cancel();
            }
            SetMotion(IdleClip, -1f, false);
        }

        /// <summary>CHARGE: the tackle clip cycle after cycle while it closes on the target (ramped: speed from <paramref name="speedFrom"/> to
        /// <paramref name="speedTo"/> at +0.01 a frame, rate 0.2 → 0.3; constant: <paramref name="speedTo"/> from the first frame; Saturday's:
        /// from 0, its first cycle at the clip's own rate); a landed hit ends it in the hop back — one tackle cycle away from the target.</summary>
        private static IEnumerable<int> Charge(int cycles, float speedFrom, float speedTo, float rateFrom, bool ramp, bool sat)
        {
            Log($"charges slot {_target} ({cycles} cycles)");
            float spd = ramp ? speedFrom : speedTo, rate = sat ? -1f : (ramp ? rateFrom : 0.3f);
            bool hit = false;
            for (int c = 0; c < cycles && !hit; c++)
            {
                SetMotion(ChargeClip, rate, once: true);
                while (!ClipDone())
                {
                    TurnToward(TargetPos(), 0.1f);
                    var (tx, ty) = TargetPos(); MoveTo(tx, ty, spd);
                    if (ramp) { spd = MathF.Min(speedTo, spd + 0.01f); rate = MathF.Min(0.3f, (rate < 0 ? 0f : rate) + 0.05f); }
                    yield return 0;
                    if (Strike(new Sphere(0f, 9999f, ChargeRadius(), ChargeDamage()))) { hit = true; break; }
                }
            }
            Cancel();
            if (!hit) yield break;
            var (px, _, py) = PockleModel.Position;
            float back = PockleModel.Yaw + MathF.PI;
            float hx = px + MathF.Sin(back) * 25f, hy = py + MathF.Cos(back) * 25f;
            foreach (var y in TurnUntil(back, 0.2f)) yield return y;
            float hopSpeed = sat ? 0.03f : (speedTo <= 0.7f ? 1.0f : speedTo);
            MoveTo(hx, hy, hopSpeed); SetMotion(ChargeClip, sat ? 0.15f : 0.25f, once: true);
            while (!ClipDone()) yield return 0;
            Cancel();
        }

        /// <summary>Sunday's knife: turn to it, swing at 0.33/0.40/0.46, track it until frame 264, lunge at the last place seen on 264–271.</summary>
        private static IEnumerable<int> Knife(float lunge)
        {
            Cancel(); SetMotion(WalkClip, -1f, false);
            foreach (var y in TurnUntil(TargetYaw(), 0.12f)) yield return y;
            int r = R(100); float rate = r < 60 ? 0.33f : r < 90 ? 0.40f : 0.46f;
            foreach (var y in Swing(rate, 264f, 0.09f, 264f, 271f, lunge, false, new Sphere(262f, 270f, 9.8f, 36))) yield return y;
        }

        /// <summary>Monday's spear: turn to it first, then the thrust where it stands (no tracking, no lunge).</summary>
        private static IEnumerable<int> Spear(int unused)
        {
            SetMotion(WalkClip, -1f, false);
            foreach (var y in TurnUntil(TargetYaw(), 0.12f)) yield return y;
            Cancel();
            int r = R(100); float rate = r < 60 ? 0.34f : r < 90 ? 0.40f : 0.46f;
            foreach (var y in Swing(rate, 0f, 0f, 0f, 0f, 0f, false, new Sphere(259f, 266f, 9.5f, 32))) yield return y;
        }

        /// <summary>Wednesday's axe: turn to it, track it until frame 263, leap at the last place seen on 263–270 (0.4; 0.54 at a fast swing).</summary>
        private static IEnumerable<int> Axe(float rate)
        {
            Cancel(); TurnToward(TargetPos(), 0.1f); SetMotion(WalkClip, -1f, false);
            foreach (var y in TurnUntil(TargetYaw(), 0.1f)) yield return y;
            foreach (var y in Swing(rate, 263f, 0.08f, 263f, 270f, rate < 0.5f ? 0.4f : 0.54f, false, new Sphere(267f, 270f, 9.4f, 30))) yield return y;
        }

        /// <summary>Saturday's punch: both fists, a lunge at 0.65 toward the place fixed at its start on frames 265.3–277.2.</summary>
        private static IEnumerable<int> Punch(float fixedRate = 0f, float lunge = 0.65f)
        {
            SetMotion(WalkClip, 0.2f, false);
            Cancel();
            int r = R(100); float rate = fixedRate > 0 ? fixedRate : r < 51 ? 0.35f : r < 91 ? 0.40f : 0.50f;
            TurnToward(TargetPos(), 0.1f);
            foreach (var y in Swing(rate, 0f, 0.1f, 265.3f, 277.2f, lunge, true, new Sphere(265f, 271f, 10.3f, 29))) yield return y;
        }

        /// <summary>A clip-13 swing: turning toward the target at <paramref name="trackRate"/> until frame <paramref name="trackUntil"/>, a lunge
        /// toward the target's place (frozen where tracking ended, or at the start) on [<paramref name="lungeFrom"/>, <paramref name="lungeTo"/>),
        /// its sphere landing once in its window.</summary>
        private static IEnumerable<int> Swing(float rate, float trackUntil, float trackRate, float lungeFrom, float lungeTo, float lunge, bool freezeAtStart, Sphere s)
        {
            var (lx, ly) = TargetPos();
            SetMotion(AttackClip, rate, once: true);
            yield return 0;
            while (!ClipDone())
            {
                float f = Frame;
                if (f < trackUntil && trackRate > 0) { TurnToward(TargetPos(), trackRate); if (!freezeAtStart) (lx, ly) = TargetPos(); }
                if (lunge > 0 && f >= lungeFrom && f < lungeTo) { _turnRate = 0f; MoveTo(lx, ly, lunge); }
                else if (f >= lungeTo && lungeTo > 0) Cancel();
                Strike(s);
                yield return 0;
            }
            Cancel();
            SetMotion(IdleClip, -1f, false);
        }

        /// <summary>Tuesday's dart: clip 13 at 0.2, turning toward the target until it fires at frame 266 toward the target's place led by its
        /// facing; the dart lands as a timed hit.</summary>
        private static IEnumerable<int> Shot(float rate, float turn, float fireAt, int damage, float up, float lead)
        {
            Cancel(); SetMotion(AttackClip, rate, once: true);
            bool fired = false;
            while (!ClipDone())
            {
                yield return 0;
                if (!fired) TurnToward(TargetPos(), turn);
                if (!fired && Frame >= fireAt) { fired = true; Fire(damage); }
            }
            SetMotion(IdleClip, -1f, false);
        }

        /// <summary>Thursday's throw: turn to it, clip 13 at 0.2/0.3/0.35 by distance, released at frame 266.5 (the apple's 30).</summary>
        private static IEnumerable<int> Throw()
        {
            Cancel(); SetMotion(WalkClip, 0.3f, false);
            foreach (var y in TurnUntil(TargetYaw(), 0.12f)) yield return y;
            float d = D(), rate = d > 35 ? 0.2f : d > 25 ? 0.3f : 0.35f;
            yield return 0;
            SetMotion(AttackClip, rate, once: true);
            bool fired = false;
            while (!ClipDone())
            {
                if (!fired) TurnToward(TargetPos(), 0.12f);
                if (!fired && Frame >= 266.5f) { fired = true; _turnRate = 0f; Fire(30); }
                yield return 0;
            }
            SetMotion(IdleClip, -1f, false);
        }

        /// <summary>Tuesday's stare: standing, turning toward the target, for <paramref name="frames"/> frames.</summary>
        private static IEnumerable<int> Stare(int frames)
        {
            Cancel(); SetMotion(IdleClip, -1f, false);
            for (int i = 0; i < frames; i++) { TurnToward(TargetPos(), 0.3f); yield return 0; }
        }

        /// <summary>Friday's jump kick: the target's place taken first; turn to it, crouch (clip 13), the airborne kick (clip 14) gliding at
        /// <paramref name="jump"/> toward that place with the foot sphere live, the landing (clip 15).</summary>
        private static IEnumerable<int> JumpKick(float rate, float jump, bool turnInCrouch)
        {
            Cancel();
            var (tx, ty) = TargetPos();
            if (turnInCrouch) foreach (var y in TurnUntil(TargetYaw(), 0.3f)) yield return y;
            SetMotion(AttackClip, rate, once: true);
            while (!ClipDone()) { if (turnInCrouch) TurnToward(TargetPos(), 0.08f); yield return 0; }
            MoveTo(tx, ty, jump); SetMotion(KickClip, rate, once: true);
            while (!ClipDone()) { Strike(new Sphere(262f, 271f, 10.5f, 29)); yield return 0; }
            Cancel();
            SetMotion(LandClip, rate, once: true);
            while (!ClipDone()) yield return 0;
            SetMotion(IdleClip, -1f, false);
        }

        private static IEnumerable<int> PlayOnce(int clip, float rate)
        {
            SetMotion(clip, rate, once: true);
            yield return 0;
            while (!ClipDone()) yield return 0;
        }

        private static IEnumerable<int> TurnUntil(float yaw, float rate)
        {
            _yawGoal = yaw; _turnRate = rate;
            for (int i = 0; i < 90 && _turnRate > 0f; i++) yield return 0;
        }

        /// <summary>One cycle of a looping clip: until its frame wraps or it reaches its end.</summary>
        private static IEnumerable<int> WalkCycle()
        {
            float prev = Frame;
            for (int i = 0; i < 200; i++)
            {
                yield return 0;
                float f = Frame;
                if (f < prev - 0.5f || f >= _clipEnd - 0.05f) break;
                prev = f;
            }
        }

        // ───────────────────────────── the script's world ─────────────────────────────
        private static float Frame => PockleModel.MotionFrame;
        private static int R(int n) => _rng.Next(n);
        private static float D()
        {
            if (_target < 0) return 9999f;
            var (px, _, py) = PockleModel.Position; var (tx, ty) = TargetPos();
            return MathF.Sqrt((tx - px) * (tx - px) + (ty - py) * (ty - py));
        }

        private static (float x, float y) TargetPos()
        {
            if (_target < 0) { var (px, _, py) = PockleModel.Position; return (px, py); }
            long tp = EnemyAddresses.CharObjects.PosAddr(_target);
            return (Memory.ReadFloat(tp), Memory.ReadFloat(tp + 8));
        }

        private static float TargetYaw()
        {
            var (px, _, py) = PockleModel.Position; var (tx, ty) = TargetPos();
            return MathF.Atan2(tx - px, ty - py);
        }

        /// <summary>A point <paramref name="len"/> from it at <paramref name="deg"/>° off the direction to the target (_GET_VECTOR).</summary>
        private static (float x, float y) OffTarget(float deg, float len)
        {
            var (px, _, py) = PockleModel.Position;
            float a = TargetYaw() + deg * MathF.PI / 180f;
            return (px + MathF.Sin(a) * len, py + MathF.Cos(a) * len);
        }

        /// <summary>SET_MOTION: clip, rate (≤ 0 = the clip's own), restarted; once = play once and hold. Resets "landed" (HITDMG_VOL).</summary>
        private static void SetMotion(int clip, float rate, bool once)
        {
            PockleModel.RestartClip(clip);
            if (!once) Memory.WriteUInt(PockleModel.Slot + CCharacter.MotionFlags, (uint)CCharacter.MotionRestart);
            PockleModel.SetRate(rate > 0f ? rate : -1f);
            _clipEnd = PockleModel.ClipRange(clip).end; _prevFrame = PockleModel.ClipRange(clip).start;
            _connected = false;
        }

        private static bool ClipDone() => Frame >= _clipEnd - 0.5f;

        /// <summary>SET_MOVE: a straight move to (x, y) at <paramref name="speed"/> a frame, carried out by the mover until it is there.</summary>
        private static void MoveTo(float x, float y, float speed)
        {
            var (px, _, py) = PockleModel.Position;
            float dx = x - px, dy = y - py, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 0.05f || speed <= 0f) { PockleModel.Step(0f, 0f, 0); return; }
            int n = Math.Max(1, (int)MathF.Ceiling(d / speed));
            if (TileVoid(px + dx * 0.5f, py + dy * 0.5f) || TileVoid(x, y)) { PockleModel.Step(0f, 0f, 0); return; }
            PockleModel.Step(dx / n, dy / n, n);
        }

        private static void Cancel() { PockleModel.Step(0f, 0f, 0); _turnRate = 0f; }

        private static void TurnToward((float x, float y) p, float rate) => TurnToward(p.x, p.y, rate);
        private static void TurnToward(float x, float y, float rate)
        {
            var (px, _, py) = PockleModel.Position;
            if ((x - px) * (x - px) + (y - py) * (y - py) < 1e-4f) return;
            _yawGoal = MathF.Atan2(x - px, y - py); _turnRate = rate;
        }

        /// <summary>The native turn's frame: facing blended toward its goal at its rate; done (rate 0) once it is there.</summary>
        private static void ApplyTurn()
        {
            if (_turnRate <= 0f) return;
            float yaw = Blend(PockleModel.Yaw, _yawGoal, _turnRate);
            PockleModel.SetYaw(yaw);
            float diff = _yawGoal - yaw;
            while (diff > MathF.PI) diff -= 2 * MathF.PI;
            while (diff < -MathF.PI) diff += 2 * MathF.PI;
            if (MathF.Abs(diff) < 0.05f) _turnRate = 0f;
        }

        private static float Blend(float yaw, float goal, float t)
        {
            float diff = goal - yaw;
            while (diff > MathF.PI) diff -= 2 * MathF.PI;
            while (diff < -MathF.PI) diff += 2 * MathF.PI;
            return yaw + diff * Math.Clamp(t, 0f, 1f);
        }

        /// <summary>CHK_MOVE_INFO: the floor under the way to a point (the tile grid: a void tile is a wall).</summary>
        private static bool PathClear() { var (tx, ty) = TargetPos(); return PathClear(tx, ty); }
        private static bool PathClear(float x, float y)
        {
            var (px, _, py) = PockleModel.Position;
            return !TileVoid(x, y) && !TileVoid((px + x) * 0.5f, (py + y) * 0.5f);
        }

        /// <summary>Thursday's cone test (CHK_USER_INNER_PRODUCT 40°, 90): the target within <paramref name="dist"/> and inside the cone.</summary>
        private static bool InCone(float deg, float dist)
        {
            var (px, _, py) = PockleModel.Position; var (tx, ty) = TargetPos();
            float dx = tx - px, dy = ty - py, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d >= dist || d < 1e-3f) return false;
            float yaw = PockleModel.Yaw, dot = (MathF.Sin(yaw) * dx + MathF.Cos(yaw) * dy) / d;
            return dot >= 1f - deg / 90f;
        }

        private static float ChargeRadius() => _day switch { 1 => 9f, 3 => 9f, 4 => 12f, 5 => 9f, _ => 10f };
        private static int ChargeDamage() => _day switch { 0 => 26, 1 => 12, 2 => 31, 3 => 28, 4 => 29, 5 => 29, _ => 25 };

        private static float Dist(int slot, float x, float y)
        {
            long p = EnemyAddresses.CharObjects.PosAddr(slot);
            float dx = Memory.ReadFloat(p) - x, dy = Memory.ReadFloat(p + 8) - y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The locked-on enemy if it is near Ruby, else the current target while it lives near her, else the live enemy nearest
        /// her within <see cref="TargetRange"/>, else −1.</summary>
        private static int PickTarget(float rx, float ry, int current)
        {
            int locked = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (locked >= 0 && locked < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(locked) && Dist(locked, rx, ry) <= LeashRange) return locked;
            if (current >= 0) return current;
            int best = -1; float bd = TargetRange;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float d = Dist(s, rx, ry);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Whether the floor's tile grid has no floor at (x, y).</summary>
        private static bool TileVoid(float x, float y)
        {
            uint map = Memory.ReadGuestPtr(DungeonTileGrid.NowDngMapPtr);
            if (!Memory.IsValidGuest(map)) return false;
            int tx = (int)MathF.Floor((x + DungeonTileGrid.TileWorldBias) / DungeonTileGrid.TileWorldSize);
            int ty = (int)MathF.Floor((y + DungeonTileGrid.TileWorldBias) / DungeonTileGrid.TileWorldSize);
            if (tx < 0 || ty < 0 || tx >= DungeonTileGrid.GridSize || ty >= DungeonTileGrid.GridSize) return true;
            return Memory.ReadInt(Memory.ToMmu(map) + DungeonTileGrid.TilePartsOffset + (long)(tx + ty * DungeonTileGrid.GridSize) * DungeonTileGrid.TileStride) < 0;
        }

        // ───────────────────────────── landing hits ─────────────────────────────
        /// <summary>The sphere, live in its frame window and not yet landed this clip, touching the target: the hit lands. True when it did.</summary>
        private static bool Strike(Sphere s)
        {
            if (_connected) return false;
            float f = Frame;
            if (f < s.From || f > s.To) return false;
            if (!Contact(s.Radius)) return false;
            Land(s.Damage);
            _connected = true;
            return true;
        }

        private static bool Contact(float radius)
        {
            if (_target < 0 || !Enemies.IsLive(_target)) return false;
            var (px, _, py) = PockleModel.Position;
            float yaw = PockleModel.Yaw;
            float sx = px + MathF.Sin(yaw) * ReachAhead, sy = py + MathF.Cos(yaw) * ReachAhead;
            EnemyBody.BodyCentre(_target, EnemyAddresses.FloorSlots.SlotAddr(_target, 0), out float cx, out _, out float cy, out float cr);
            float dx = cx - sx, dy = cy - sy;
            return dx * dx + dy * dy <= (radius + cr) * (radius + cr);
        }

        /// <summary>A projectile toward the target: it lands after its flight if the target is still near where it was aimed.</summary>
        private static void Fire(int damage)
        {
            if (_target < 0) return;
            _inFlight.Add((_target, Math.Max(1, (int)(D() / ShotSpeed)), damage));
            Log($"fires at slot {_target}");
        }

        private static void FlyShots(int frames)
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var (slot, left, dmg) = _inFlight[i];
                left -= frames;
                if (left > 0) { _inFlight[i] = (slot, left, dmg); continue; }
                _inFlight.RemoveAt(i);
                if (!Enemies.IsLive(slot)) continue;
                int keep = _target; _target = slot; Land(dmg); _target = keep;
            }
        }

        /// <summary>The damage on the target as Ruby's hit (her kill and ABS), thrown away from the pockle; landing provokes it.</summary>
        private static void Land(int damage)
        {
            if (_target < 0 || !Enemies.IsLive(_target)) return;
            var (px, ph, py) = PockleModel.Position;
            _plant = EnemyHit.TryPlant(_target, damage, 7f, new EnemyHit.Kick(px, ph, py, Baselard.HalfKickStrength, Baselard.KickDecay),
                                       mark: CodeCaves.NoDrainMark, weaponAbilities: false);
            _plantLife = PlantLifeFrames;
            Log(_plant >= 0 ? $"hits slot {_target} for {damage}" : $"no pool entry for its hit on slot {_target}");
        }

        private static void WatchPlant()
        {
            if (_plant < 0) return;
            long pool = CollisionPool.Resolve();
            if (pool == 0) { _plant = -1; return; }
            if (!CollisionPool.IsActive(pool, _plant)) { Provoke(_target); _plant = -1; return; }
            if (--_plantLife <= 0) { CollisionPool.Withdraw(pool, _plant, clearMark: true); _plant = -1; }
        }

        // ───────────────────────────── provoking ─────────────────────────────
        private static void Provoke(int slot)
        {
            if (slot < 0 || !_provoked.Add(slot)) return;
            Log($"slot {slot} provoked ({_provoked.Count} on the pockle)");
            WriteTable();
        }

        private static void ReleaseTheDead()
        {
            if (_provoked.RemoveWhere(s => !Enemies.IsLive(s)) > 0)
            {
                if (_provoked.Count == 0) { AggroTable.Release(AggroTable.Holder.Pockle); Log("no provoked enemy left — the table released"); }
                else WriteTable();
            }
        }

        private static void WriteTable()
        {
            AggroTable.Claim(AggroTable.Holder.Pockle);
            var buf = new byte[CodeCaves.TableSlots * CodeCaves.PtrStride];
            for (int s = 0; s < CodeCaves.TableSlots; s++)
                BitConverter.GetBytes(_provoked.Contains(s) ? PockleModel.PosGuest : StbExternCmd.PlayerPosGuest).CopyTo(buf, s * CodeCaves.PtrStride);
            AggroTable.Write(AggroTable.Holder.Pockle, buf);
        }

        // ───────────────────────────── being hit ─────────────────────────────
        private static void TakeMeleeHits()
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            var (px, ph, py) = PockleModel.Position;
            byte[] flags = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (flags == null) return;
            for (int i = 0; i < CollisionPool.Entries && !_dying; i++)
            {
                if (BitConverter.ToInt32(flags, i * 4) == 0) continue;
                byte[] e = Memory.ReadBytesBatch(pool + i * CollisionPool.Stride, CollisionPool.Stride);
                if (e == null) continue;
                if ((BitConverter.ToUInt32(e, CollisionPool.Mask) & CollisionPool.HurtsPlayerMask) == 0) continue;
                if (BitConverter.ToInt32(e, CollisionPool.GateA) != BitConverter.ToInt32(e, CollisionPool.GateB)) continue;
                if (BitConverter.ToSingle(e, CollisionPool.EntryClass) != 0f) continue;
                int owner = BitConverter.ToInt32(e, CollisionPool.Owner);
                if (owner < 200) continue;
                int slot = (owner - 200) / 5;
                if (slot < 0 || slot >= _sinceHitBy.Length || _sinceHitBy[slot] < HurtDebounceFrames) continue;
                float ex = BitConverter.ToSingle(e, 0), eh = BitConverter.ToSingle(e, 4), ey = BitConverter.ToSingle(e, 8), r = BitConverter.ToSingle(e, CollisionPool.Radius);
                float dx = ex - px, dy = ey - py;
                if (dx * dx + dy * dy > (r + HitRadius) * (r + HitRadius)) continue;
                if (eh + r < ph - HitBelow || eh - r > ph + HitHeight) continue;
                _sinceHitBy[slot] = 0;
                Hurt(Math.Max(0, BitConverter.ToInt32(e, EntryDamage) - _defense), $"slot {slot}'s swing", slot);
            }
        }

        private static void TakeShotHits()
        {
            uint packG = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(packG)) return;
            long pack = Memory.ToMmu(packG);
            var (px, ph, py) = PockleModel.Position;
            for (int p = 0; p < ShotEffectPack.PackSlots && !_dying; p++)
            {
                long fx = pack + (long)p * ShotEffectPack.SlotStride;
                uint cfgG = Memory.ReadUInt(fx + ShotEffectPack.OffCfg);
                if (!Memory.IsValidGuest(cfgG)) continue;
                long cfg = Memory.ToMmu(cfgG);
                for (int i = 0; i < ShotEffectPack.SubShots; i++)
                {
                    int key = p * ShotEffectPack.SubShots + i;
                    bool live = Memory.ReadUShort(fx + ShotEffectPack.OffActive + i * 2) != 0 && Memory.ReadUShort(fx + ShotEffectPack.OffPhase + i * 2) <= 1;
                    int owner = Memory.ReadShort(fx + ShotEffectPack.OffA060 + i * 2);
                    if (!live || owner < 0) { _shotLast.Remove(key); continue; }
                    long o = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride + ShotEffectPack.ObjPos;
                    var now = (x: Memory.ReadFloat(o), h: Memory.ReadFloat(o + 4), y: Memory.ReadFloat(o + 8));
                    var from = _shotLast.TryGetValue(key, out var was) ? was : now;
                    _shotLast[key] = now;
                    float reach = Math.Max(1f, Memory.ReadFloat(cfg + ShotEffectPack.CfgRadiusFlying)) + HitRadius;
                    if (!SegmentMeetsBody(from, now, px, ph, py, reach, out var at)) continue;
                    long obj = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride;
                    Memory.WriteVec3(obj + ShotEffectPack.ObjPos, at.x, at.h, at.y);
                    ConfusionFriendlyFire.ShotContact(fx, cfg, i, obj);
                    _shotLast.Remove(key);
                    Hurt(Math.Max(0, Memory.ReadInt(fx + ShotEffectPack.OffDamage + i * 4) - _defense), $"slot {owner}'s shot", owner);
                    if (_dying) return;
                }
            }
        }

        private static bool SegmentMeetsBody((float x, float h, float y) a, (float x, float h, float y) b, float px, float ph, float py, float reach,
                                             out (float x, float h, float y) at)
        {
            float dx = b.x - a.x, dy = b.y - a.y, len2 = dx * dx + dy * dy;
            float t = len2 < 1e-6f ? 0f : Math.Clamp(((px - a.x) * dx + (py - a.y) * dy) / len2, 0f, 1f);
            at = (a.x + dx * t, a.h + (b.h - a.h) * t, a.y + dy * t);
            float ex = at.x - px, ey = at.y - py;
            return ex * ex + ey * ey <= reach * reach && at.h >= ph - HitBelow - reach && at.h <= ph + HitHeight + reach;
        }

        /// <summary>A hit taken: HP down; the fall at 0, else its day's hit reaction (label 110) unless its super armour holds. The enemy that
        /// hit it becomes its target, as a script's "player" would be.</summary>
        private static void Hurt(int damage, string what, int by)
        {
            _hp -= damage;
            Log($"hit by {what} for {damage} ({Math.Max(_hp, 0)}/{_maxHp} HP left)");
            if (_hp <= 0)
            {
                PockleModel.Step(0f, 0f, 0); _turnRate = 0f;
                _clipEnd = PockleModel.ClipRange(DeathClip).end;
                PockleModel.RestartClip(DeathClip); PockleModel.SetRate(-1f);
                _dying = true; _script = null;
                if (_plant >= 0) { long pool = CollisionPool.Resolve(); if (pool != 0) CollisionPool.Withdraw(pool, _plant, clearMark: true); _plant = -1; }
                Log("falls");
                return;
            }
            if (SuperArmour()) return;
            if (by >= 0 && by < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(by) && Memory.ReadInt(PlayerAction.LockOnTargetSlot) < 0) _target = by;
            if (_target < 0) return;
            _following = false;
            _script = Label110(); _reacting = true;
        }

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
