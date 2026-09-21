using System;
using System.Threading;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Hardshooter's ricochet: a pellet that lands on an enemy spawns a second pellet at the exact impact point that flies at
    /// the next nearest enemy (the living one nearest the enemy just hit, within <see cref="NextEnemyRange"/>), or in a
    /// random direction when none is near. A ricochet never ricochets again; two pellets landing together (Double Impact's
    /// pair) take different targets. The second pellet is a real one in the
    /// player's shot pool, so it flies, collides and plants its own damage entry with the weapon's flags; it carries the
    /// first pellet's damage word and ground speed.
    ///
    /// THE HIT. step__5CSHOT tests a live pellet BEFORE moving it: checkCollision(2.0, …) walks every living enemy's active
    /// hurt spheres (<see cref="BodyCollision"/>: centres rebuilt each frame, radii) and the pellet dies where it stands
    /// when its position lies within 2 + radius of a centre — walls are tested after the enemies. A dead slot keeps its
    /// position, velocity and damage, so the driver reads the death point exactly and applies the game's own rule to the
    /// live spheres: the enemy whose sphere the point is inside is the one hit; none means a wall or the end of its flight.
    ///
    /// THE DEPARTURE. The ricochet is written at the death point with its collision OFF (+0x280), which also stops the
    /// game moving it; the driver steps it along its new line each tick until it stands clear of every sphere of the enemy
    /// it came from (2 + radius + one frame of travel, so the game's next test passes), then turns collision on and the
    /// game flies it from there — it cannot strike the enemy it came from. Should it reach another enemy's sphere while
    /// still on the driver's steps, collision is turned on at once and the game resolves that hit. Double Impact drives this
    /// for its pair, and Super Steve carrying a Hardshooter SynthSphere has it too (<see cref="SuperSteve.SphereInheritanceEffect"/>).
    /// </summary>
    internal static class Hardshooter
    {
        private const string Tag = "[Hardshooter] ";
        private const float PelletRadius   = 2f;     // checkCollision's radius for a pellet
        private const float NextEnemyRange = 120f;   // the next target must be within this of the enemy hit
        private const float DefaultSpeed   = 4f;     // when the first pellet's ground speed cannot be read
        private const int   Lifetime       = 0x78;   // frames of flight, a pellet's
        private const int   MaxDrivenTicks = 30;     // a departure never stays on the driver's steps longer than this
        private static readonly Random _rng = new Random();

        private static readonly bool[] _live = new bool[PlayerShotPool.SlotCount];
        private static readonly bool[] _bounced = new bool[PlayerShotPool.SlotCount];   // a ricochet: never ricochets again
        private sealed class Departure { public int Slot, From, Ticks; public float Vx, Vy, Speed; }
        private static readonly List<Departure> _departing = new List<Departure>();
        private static readonly TimeSpan ClaimWindow = TimeSpan.FromMilliseconds(250);   // two pellets landing together take different targets
        private static readonly List<(int enemy, DateTime at)> _claims = new List<(int, DateTime)>();

        /// <summary>Whether the pellet in <paramref name="slot"/> is a ricochet (Double Impact leaves those untwinned).</summary>
        internal static bool IsBounced(int slot) => _bounced[slot];

        /// <summary>Drive every tick while a weapon carrying the ricochet is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            StepDepartures(pool);
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live) { _live[i] = true; continue; }
                if (!_live[i]) continue;
                _live[i] = false;
                bool bounced = _bounced[i]; _bounced[i] = false;
                if (!bounced) Bounce(pool, i);
            }
        }

        private static void Bounce(long pool, int slot)
        {
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            float x = Memory.ReadFloat(pa), h = Memory.ReadFloat(pa + 4), y = Memory.ReadFloat(pa + 8);   // the death point: the slot keeps it
            int hit = EnemyAt(x, h, y, PelletRadius);
            if (hit < 0) return;                                                    // a wall, or the end of its flight
            _claims.RemoveAll(c => GameClock.Now - c.at > ClaimWindow);
            int target = NearestTo(hit, NextEnemyRange, out float tx, out float ty, x, h, y);
            if (target >= 0) _claims.Add((target, GameClock.Now));
            float dx, dy;
            if (target >= 0) { dx = tx - x; dy = ty - y; }
            else { double a = _rng.NextDouble() * 2 * Math.PI; dx = (float)Math.Cos(a); dy = (float)Math.Sin(a); }
            float dl = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dl < 1e-3f) { dx = 1f; dy = 0f; dl = 1f; }
            dx /= dl; dy /= dl;
            float ovx = Memory.ReadFloat(va), ovy = Memory.ReadFloat(va + 8);
            float speed = (float)Math.Sqrt(ovx * ovx + ovy * ovy);
            if (speed < 0.5f) speed = DefaultSpeed;
            int j = -1;
            for (int k = 0; k < PlayerShotPool.SlotCount; k++)
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(pool, k)) == 0) { j = k; break; }
            if (j < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no free pellet slot for the ricochet"); return; }
            long pos = PlayerShotPool.PosAddr(pool, j), vel = PlayerShotPool.VelAddr(pool, j);
            Memory.WriteFloat(pos, x); Memory.WriteFloat(pos + 4, h); Memory.WriteFloat(pos + 8, y);
            Memory.WriteFloat(vel, dx * speed); Memory.WriteFloat(vel + 4, 0f); Memory.WriteFloat(vel + 8, dy * speed);
            Memory.WriteInt  (PlayerShotPool.NoCollideAddr(pool, j), 1);          // the driver steps it clear first
            Memory.WriteInt  (PlayerShotPool.LifetimeAddr(pool, j), Lifetime);
            Memory.WriteInt  (PlayerShotPool.DamageAddr(pool, j), Memory.ReadInt(PlayerShotPool.DamageAddr(pool, slot)));
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, j), 1f);
            Memory.WriteInt  (PlayerShotPool.FlagAddr(pool, j), 1);               // live LAST
            _bounced[j] = true; _live[j] = true;
            _departing.RemoveAll(d => d.Slot == j);
            _departing.Add(new Departure { Slot = j, From = hit, Vx = dx * speed, Vy = dy * speed, Speed = speed });
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (target >= 0
                ? $"ricochet off enemy {hit} toward enemy {target} (slot {j})"
                : $"ricochet off enemy {hit} in a random direction (slot {j})"));
        }

        /// <summary>The departures: each stepped along its line until clear of the enemy it came from, then the game's.</summary>
        private static void StepDepartures(long pool)
        {
            for (int n = _departing.Count - 1; n >= 0; n--)
            {
                var d = _departing[n];
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(pool, d.Slot)) == 0) { _departing.RemoveAt(n); continue; }   // its life ran out
                long pa = PlayerShotPool.PosAddr(pool, d.Slot);
                float x = Memory.ReadFloat(pa) + d.Vx, h = Memory.ReadFloat(pa + 4), y = Memory.ReadFloat(pa + 8) + d.Vy;
                Memory.WriteFloat(pa, x); Memory.WriteFloat(pa + 8, y);
                bool clear = !InsideEnemy(d.From, x, h, y, PelletRadius + d.Speed) || ++d.Ticks >= MaxDrivenTicks;
                bool other = EnemyAt(x, h, y, PelletRadius, d.From) >= 0;             // reached someone else: the game takes the hit
                if (clear || other)
                {
                    Memory.WriteInt(PlayerShotPool.NoCollideAddr(pool, d.Slot), 0);
                    _departing.RemoveAt(n);
                }
            }
        }

        /// <summary>The living enemy one of whose active hurt spheres holds (x, h, y) within <paramref name="reach"/> of its
        /// radius — checkCollision's own rule; <paramref name="except"/> excluded. −1 = none.</summary>
        private static int EnemyAt(float x, float h, float y, float reach, int except = -1)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                if (s != except && Alive(s) && InsideEnemy(s, x, h, y, reach)) return s;
            return -1;
        }

        private static bool InsideEnemy(int s, float x, float h, float y, float reach)
        {
            long g = EnemyAddresses.MainMonstorUnit.Base + (long)s * BodyCollision.SlotStride;
            for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
            {
                if (Memory.ReadInt(g + BodyCollision.ActiveArray + p * BodyCollision.BodyPartStride) == 0) continue;
                long c = g + BodyCollision.CentreArray + p * BodyCollision.CentreStride;
                float dx = Memory.ReadFloat(c) - x, dh = Memory.ReadFloat(c + 4) - h, dy = Memory.ReadFloat(c + 8) - y;
                float r = Memory.ReadFloat(g + BodyCollision.RadiusArray + p * BodyCollision.BodyPartStride) + reach;
                if (dx * dx + dh * dh + dy * dy <= r * r) return true;
            }
            return false;
        }

        private static bool Alive(int s) =>
            Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) > 0
            && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;

        /// <summary>The living enemy nearest enemy <paramref name="from"/> within <paramref name="range"/> (itself and the targets
        /// claimed within <see cref="ClaimWindow"/> excluded) and the aim point on it: its active sphere centre nearest the death
        /// point, else its position. −1 = none.</summary>
        private static int NearestTo(int from, float range, out float ax, out float ay, float x, float h, float y)
        {
            ax = ay = 0f;
            long fp = EnemyAddresses.MainMonstorUnit.Base + EnemyAddresses.CharObjects.ArrayOffset + from * EnemyAddresses.CharObjects.Stride + EnemyAddresses.CharObjects.PosOffset;
            float fx = Memory.ReadFloat(fp), fy = Memory.ReadFloat(fp + 8);
            int best = -1; float bestD = range * range;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (s == from || !Alive(s) || _claims.Exists(c => c.enemy == s)) continue;   // a target another pellet just took is left to it
                long pos = EnemyAddresses.MainMonstorUnit.Base + EnemyAddresses.CharObjects.ArrayOffset + s * EnemyAddresses.CharObjects.Stride + EnemyAddresses.CharObjects.PosOffset;
                float px = Memory.ReadFloat(pos), py = Memory.ReadFloat(pos + 8);
                float dx = px - fx, dy = py - fy, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; ax = px; ay = py; }
            }
            if (best < 0) return -1;
            long g = EnemyAddresses.MainMonstorUnit.Base + (long)best * BodyCollision.SlotStride;
            float near = float.MaxValue;
            for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
            {
                if (Memory.ReadInt(g + BodyCollision.ActiveArray + p * BodyCollision.BodyPartStride) == 0) continue;
                long c = g + BodyCollision.CentreArray + p * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), cy = Memory.ReadFloat(c + 8), dx = cx - x, dy = cy - y, d = dx * dx + dy * dy;
                if (d < near) { near = d; ax = cx; ay = cy; }
            }
            return best;
        }

        /// <summary>The weapon or the floor went: departures handed to the game, the latches start over.</summary>
        internal static void Stop()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (Memory.IsValidGuest(pool))
                foreach (var d in _departing) Memory.WriteInt(PlayerShotPool.NoCollideAddr(pool, d.Slot), 0);
            _departing.Clear(); _claims.Clear();
            Array.Clear(_live, 0, _live.Length); Array.Clear(_bounced, 0, _bounced.Length);
        }

        // ── Hardshooter ────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Hardshooter thread: hands every tick to <see cref="Hardshooter.Drive"/> while the weapon is equipped,
        /// and stands it down once when it goes.</summary>
        public static void RicochetEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.hardshooter)
            {
                Hardshooter.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            Hardshooter.Stop();
        }

    }
}
