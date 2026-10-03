using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.ConfusionFriendlyFire;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Confused enemies, shared by Babel's Spear, Super Steve's Terra Sword sphere and the Confuse ability's procs: each user
    /// <see cref="Configure"/>s it (an area, a tint, whether a hit provokes), <see cref="Confuse"/>s slots, <see cref="Tick"/>s it
    /// from its loop and <see cref="End"/>s it. A confused slot's entry in the Mirage's target-pointer table follows the nearest live
    /// enemy or the player, else its own wander spot; an enemy a confused one hits is PROVOKED and goes after it; the tint is the
    /// unit's ambient add. Its swings' and shots' friendly fire is <see cref="ConfusionFriendlyFire"/>, which shares this class's
    /// members through using static (docs/confuse-ability.md; the friendly fire in docs/babels-spear.md).</summary>
    internal static class Confusion
    {
        private const string Tag = "[Confusion] ";
        private const float  WanderSeconds    = 4f;     // a wandering enemy's spot renewed this often…
        private const float  WanderRange      = 40f;    // …within this of where it stands…
        private const float  WanderReached    = 8f;     // …or once it has got this close to it
        internal const float ContactCooldown  = 0.5f;   // one friendly-fire hit per attacker-victim pair this often
        internal const float ContactRadius    = 1f;     // the planted hit's own radius: the victim's sphere does the reaching
        internal const double ShellLifeSeconds = 0.15;   // a planted hit stays this long before it is withdrawn
        internal const int   ShellPoolReserve = 16;     // free pool entries always left to the engine
        internal static readonly int Slots = EnemyAddresses.FloorSlots.Count;

        /// <summary>True while any enemy is confused or provoked: the per-slot target table is this class's (Mirage's loop leaves it).</summary>
        internal static bool OwnsTable => AggroTable.ConfusionActive;
        /// <summary>The configured tint's strength 0..1 (Babel's follows its spear's fade).</summary>
        internal static float TintScale = 1f;

        private static (float x, float y, float r)? _area;
        private static float[] _tint;
        private static bool _provokes;
        internal static string _owner = "";

        private static readonly Random     _rng          = new();
        private static readonly DateTime[] _until        = new DateTime[Slots];
        private static readonly int[]      _victim       = new int[Slots];   // −1 = the player, −2 = wandering, −3 = unset
        private static readonly DateTime[] _wanderSet    = new DateTime[Slots];
        private static readonly List<int>[] _hitBy       = new List<int>[Slots];   // the confused slots that have hit it, most recent first
        private static readonly bool[]     _provoked     = new bool[Slots];        // its pointer is on a provoker (put back when none is left)
        private static readonly object     _lock         = new();
        private static DateTime _lastTick;
        private const double MinTickSeconds = 0.012;    // several loops may tick (ConfuseAbility's, a weapon's): one pass a frame
        private static DateTime _lastReport;

        static Confusion() { for (int s = 0; s < Slots; s++) { _victim[s] = -3; _hitBy[s] = new List<int>(); } _provokes = true; }

        /// <summary>How the current user confuses: the area its targets are chosen in (null = anywhere), the tint (null = none), and
        /// whether a confused enemy's hit provokes its victim. <paramref name="owner"/> tags the log.</summary>
        internal static void Configure((float x, float y, float r)? area, float[] tint, bool provokes, string owner)
        {
            _area = area; _tint = tint; _provokes = provokes; _owner = owner;
        }

        /// <summary>The configured area's centre moved (Babel's spear follows its target while it rises).</summary>
        internal static void MoveArea(float x, float y) { if (_area is var (_, _, r)) _area = (x, y, r); }

        internal static bool IsConfused(int slot) => _until[slot] != default && GameClock.Now < _until[slot] && Enemies.IsLive(slot);
        /// <summary>The confused slot a non-confused one goes after: its most recent attacker still confused and alive (−1 = none).</summary>
        private static int Provoker(int slot)
        {
            if (!_provokes || IsConfused(slot) || !Enemies.IsLive(slot)) return -1;
            var list = _hitBy[slot];
            for (int i = 0; i < list.Count; i++) if (IsConfused(list[i])) return list[i];
            return -1;
        }

        /// <summary>Whether any enemy is confused right now.</summary>
        internal static bool AnyConfused() { for (int s = 0; s < Slots; s++) if (IsConfused(s)) return true; return false; }

        /// <summary>Slot confused until <paramref name="until"/> (a later time extends it).</summary>
        internal static void Confuse(int slot, DateTime until)
        {
            if (slot < 0 || slot >= Slots || GameClock.Now >= until || !Enemies.IsLive(slot)) return;
            bool fresh = !IsConfused(slot);
            if (until > _until[slot]) _until[slot] = until;
            if (!fresh) return;
            _victim[slot] = -3; _wanderSet[slot] = default;                   // unset: the first tick chooses and logs
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {slot} confused");
        }

        /// <summary>One slot's confusion ended now, the others' untouched (the Terra nut's bonked enemy as the nut fades); whoever it
        /// provoked turns on the next attacker still confused, else the player, on the next tick.</summary>
        internal static void Unconfuse(int slot)
        {
            if (slot < 0 || slot >= Slots) return;
            lock (_lock) { if (_until[slot] != default) Release(slot); }
        }

        /// <summary>Whether a slot is in play (RenderStatus 2). Until then it is dormant: a chest mimic still shut in its box, or an
        /// enemy too far off for the game to have activated it yet.</summary>
        internal static bool IsActive(int slot) => Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) == 2;

        /// <summary>Every tick of the user's loop: targets, tints, provocations, friendly fire, planted hits withdrawn.</summary>
        internal static void Tick()
        {
            lock (_lock)
            {
                if ((GameClock.Now - _lastTick).TotalSeconds < MinTickSeconds) return;
                _lastTick = GameClock.Now;
                TickLocked();
            }
        }

        private static void TickLocked()
        {
            bool any = false;
            for (int s = 0; s < Slots; s++)
            {
                _hitBy[s].RemoveAll(a => !IsConfused(a));                       // an attacker that came to its senses (or died) is forgotten
                if (_until[s] != default)
                {
                    if (!IsConfused(s)) { Release(s); s--; continue; }          // released: provoked (below) by whoever is still hitting it, else the player
                    any = true;
                    int victim = Nearest(s);
                    if (victim != _victim[s]) { _victim[s] = victim; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {s} → after {Whom(victim)}"); }
                    Point(s, victim >= 0 ? (uint)(EnemyAddresses.CharObjects.PosAddr(victim) - Memory.Pcsx2Base) : victim == -1 ? StbExternCmd.PlayerPosGuest : Wander(s));
                    if (_tint != null) Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharaTint, _tint[0] * TintScale, _tint[1] * TintScale, _tint[2] * TintScale);
                }
                else
                {
                    int by = Provoker(s);
                    if (by < 0) { if (_provoked[s]) EndProvocation(s); continue; }
                    if (!_provoked[s]) { _provoked[s] = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {s} provoked: after enemy slot {by}"); }
                    any = true;
                    Point(s, (uint)(EnemyAddresses.CharObjects.PosAddr(by) - Memory.Pcsx2Base));
                }
            }
            RetireShells();
            if (any) { ContactHits(); ShotHits(); } else _shotLast.Clear();
            AggroTable.ConfusionActive = any && Mirage.Armed;
            if (DebugDiagnostics.Enabled && any && (GameClock.Now - _lastReport).TotalSeconds >= 1) { _lastReport = GameClock.Now; Report(); }
        }

        /// <summary>Everything back: every confused or provoked slot released, every planted hit withdrawn, the table handed back.</summary>
        internal static void End()
        {
            lock (_lock) EndLocked();
        }

        private static void EndLocked()
        {
            long pool = CollisionPool.Resolve();
            foreach (var (idx, _) in _shells) if (pool != 0) CollisionPool.Deactivate(pool, idx);
            _shells.Clear(); _shotLast.Clear();
            for (int s = 0; s < Slots; s++)
            {
                if (_until[s] != default) Release(s);
                if (_provoked[s]) EndProvocation(s);
                _hitBy[s].Clear();
            }
            AggroTable.ConfusionActive = false; TintScale = 1f;
            _area = null; _tint = null; _provokes = true; _owner = "";   // the next user configures afresh; procs confuse plainly
        }

        private static void Point(int slot, uint ptr) => AggroTable.PointConfused(slot, ptr);   // nothing while a weapon effect holds the table; re-pointed when it lets go

        private static string Whom(int victim) => victim >= 0 ? $"enemy slot {victim}" : victim == -1 ? "the player" : "nothing — wandering";

        private static bool InArea(float x, float y)
        {
            if (_area is not var (ax, ay, ar)) return true;
            float dx = x - ax, dy = y - ay; return dx * dx + dy * dy <= ar * ar;
        }

        /// <summary>The nearest thing for a confused slot to go after: another live enemy, or the player — each only inside the area
        /// when there is one; −2 when there is nothing.</summary>
        private static int Nearest(int slot)
        {
            long me = EnemyAddresses.CharObjects.PosAddr(slot);
            float mx = Memory.ReadFloat(me), my = Memory.ReadFloat(me + 8);
            int best = -2; float bestD = float.MaxValue;
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            if (InArea(px, py)) { bestD = (px - mx) * (px - mx) + (py - my) * (py - my); best = -1; }
            for (int s = 0; s < Slots; s++)
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

        /// <summary>A confused slot back to normal: its pointer on the player, its tint off.</summary>
        private static void Release(int slot)
        {
            _until[slot] = default; _victim[slot] = -3; _wanderSet[slot] = default;
            AggroTable.PointConfused(slot, StbExternCmd.PlayerPosGuest);
            if (Enemies.IsLive(slot)) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {slot}'s confusion ends");
            if (_tint != null) Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharaTint, 0f, 0f, 0f);
        }

        /// <summary>A slot <paramref name="victim"/> hit by confused <paramref name="attacker"/> remembers it, most recent first — a
        /// confused victim too, for when its own confusion ends.</summary>
        private static void Provoke(int victim, int attacker)
        {
            var list = _hitBy[victim];
            if (list.Count > 0 && list[0] == attacker) return;
            list.Remove(attacker); list.Insert(0, attacker);
        }

        /// <summary>No provoker left: back on the player ("come to its senses").</summary>
        private static void EndProvocation(int slot)
        {
            _provoked[slot] = false;
            if (_until[slot] == default) AggroTable.PointConfused(slot, StbExternCmd.PlayerPosGuest);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {slot} comes to its senses");
        }

        /// <summary>Whether <paramref name="attacker"/>'s attack may land on <paramref name="victim"/>: a confused attacker's on any
        /// other enemy, a provoked one's on its provoker alone.</summary>
        internal static bool MayHit(int attacker, int victim)
            => attacker != victim && (IsConfused(attacker) || Provoker(attacker) == victim);

        internal static bool Attacks(int slot) => IsConfused(slot) || Provoker(slot) >= 0;

        /// <summary>A landed friendly hit: the victim provoked by a confused attacker.</summary>
        internal static void Landed(int attacker, int victim) { if (IsConfused(attacker)) Provoke(victim, attacker); }

        /// <summary>DIAGNOSTIC: each confused enemy — where it is, whom it is after and how far, its motion, and what its
        /// pointer-table entry holds.</summary>
        private static void Report()
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            for (int s = 0; s < Slots; s++)
            {
                if (!IsConfused(s)) continue;
                long me = EnemyAddresses.CharObjects.PosAddr(s);
                float mx = Memory.ReadFloat(me), my = Memory.ReadFloat(me + 8);
                float vx = px, vy = py;
                if (_victim[s] >= 0) { long v = EnemyAddresses.CharObjects.PosAddr(_victim[s]); vx = Memory.ReadFloat(v); vy = Memory.ReadFloat(v + 8); }
                else if (_victim[s] == -2) { long q = CodeCaves.BabelWander + (long)s * CodeCaves.BabelWanderStride; vx = Memory.ReadFloat(q); vy = Memory.ReadFloat(q + 8); }
                float dist = (float)Math.Sqrt((vx - mx) * (vx - mx) + (vy - my) * (vy - my));
                int motion = Memory.ReadInt(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.MotionId);
                uint ptr = Mirage.Armed ? Memory.ReadUInt(CodeCaves.PtrAddr(s)) : 0;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"slot {s} at ({mx:F0},{my:F0}) after {Whom(_victim[s])} at ({vx:F0},{vy:F0}) dist {dist:F0}, motion {motion}, table → 0x{ptr:X8}");
            }
        }
    }
}
