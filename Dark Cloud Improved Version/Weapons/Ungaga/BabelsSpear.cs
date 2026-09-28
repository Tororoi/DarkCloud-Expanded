using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Babel's Spear — "Curse of Babel": a five-second guard charge raises a giant copy of the spear out of the ground
    /// under the locked-on enemy, point up, most of it still buried. The enemy above it is struck and thrown off it, and it
    /// and every enemy within <see cref="ConfusionRadius"/> of the spear are CONFUSED for as long as the spear stands
    /// (<see cref="SpearSeconds"/>), as is any enemy that comes within that radius while it stands: tinted a pale purple, each
    /// goes after the NEAREST thing inside the area — another enemy, or the player while the player is in it — and its swings
    /// hurt other enemies; with nothing in the area to go after it wanders. Once risen the spear turns slowly on the spot, and
    /// fades out over <see cref="FadeSeconds"/> when its time is up.
    ///
    /// The pieces, all data:
    ///  · the spear is <see cref="BladeProp"/>'s copy of the equipped weapon (the same engine-drawn copy Big Bang hangs over
    ///    its target), baked to point up and placed at the target's feet with the root sunk so only <see cref="Exposed"/>
    ///    units of the tip show (the c10w10 mesh runs −9.6 … +16.0 along its axis, the tip at 16), risen over
    ///    <see cref="EmergeSeconds"/>, then turned <see cref="SpinDegPerSec"/> a second, faded at the end (the copy's opacity —
    ///    taking the slot down while it draws left a frame of it stretched);
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
    ///  · the tint is the unit's ambient add (CCharacter +0xCE0), re-asserted each tick, cleared at the end.</summary>
    internal static class BabelsSpear
    {
        private const string Tag = "[Babel] ";
        private const int    TickMs           = 50;
        private const int    GuardChargeMs    = 5000;   // hold the guard this long to summon
        private const int    GuardLoopMotion  = 9, GuardMoveMotion = 33;   // Ungaga's guard-hold poses (the Mirage's)
        private const float  SpearSeconds     = 20f;
        private const float  EmergeSeconds    = 0.6f;
        private const float  FadeSeconds      = 1f;
        private const float  SpinDegPerSec    = 120f;
        private const float  Scale            = 4f;     // the copy's size; the spear is 25.6 long unscaled
        private const float  TipZ             = 16.0f;  // the mesh's tip along its axis (c10w10: −9.6 … 16.0)
        private const float  Exposed          = 40f;    // how much of the tip stands above the ground once risen
        private const float  Buried           = 4f;     // how far below the ground the tip starts
        private const float  ConfusionRadius  = 150f;
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
        // Pale purple, as the unit's ambient add (scene ambient ≈ 128 is neutral): not too bright.
        private const float  TintR = 40f, TintG = 16f, TintB = 56f;

        /// <summary>True while any enemy is confused: the per-slot target table is this ability's (Mirage's loop leaves it).</summary>
        internal static bool OwnsTable { get; private set; }

        private static readonly Random _rng = new();
        private static bool     _up;               // the spear stands
        private static DateTime _summoned, _confusionEnd;
        private static float    _sx, _sy, _ground, _yaw;
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
            _sx = x; _sy = y; _ground = ground; _yaw = 0f;
            if (!BladeProp.Spawn(Scale, 0, pointDown: false, pointUp: true)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no spear copy (slot or cave busy)"); return; }
            BladeProp.Place(_sx, RootHeight(0f), _sy, _yaw);
            BladeProp.Alpha(1f);
            _up = true; _summoned = GameClock.Now; _confusionEnd = _summoned.AddSeconds(SpearSeconds);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the spear rises at ({_sx:F0},{_sy:F0}) ground {_ground:F0}" + (target >= 0 ? $" under enemy slot {target}" : " ahead of Ungaga") + $"; target redirect {(Mirage.Armed ? "armed" : "NOT ARMED — confusion cannot steer")}");
            if (target >= 0) { Strike(target); Confuse(target); }
            ConfuseWithinRadius();
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
            if (age >= SpearSeconds + FadeSeconds) { TakeDown(); return; }
            float t = (float)Math.Min(1.0, age / EmergeSeconds);
            // The slot's Euler yaw, kept within ±π: past that the engine's angle-to-matrix path diverges (the copy stretched
            // sideways without bound and swung back and forth once the angle had wound past a turn).
            if (t >= 1f) _yaw = Wrap(_yaw + (float)(SpinDegPerSec * Math.PI / 180.0 * TickMs / 1000.0));
            BladeProp.Place(_sx, RootHeight(t), _sy, _yaw);
            BladeProp.Alpha(age < SpearSeconds ? 1f : (float)(1.0 - (age - SpearSeconds) / FadeSeconds));   // its time up: faded out, then down
            if (age < SpearSeconds) ConfuseWithinRadius();                                                   // not while it fades: the confusion has run out
        }

        private static float Wrap(float a)
        {
            while (a > Math.PI)  a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        private static void TakeDown()
        {
            if (!_up) return;
            BladeProp.Despawn();
            _up = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the spear sinks away");
        }

        /// <summary>One player-hit sphere on the target's body at the weapon's attack, thrown away from the spear.</summary>
        private static void Strike(int slot)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset);
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(StrikeRadius, cr), Math.Max(1, attack), 0);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x80, _sx); F(0x84, _ground); F(0x88, _sy);                 // the kick comes from the spear
            F(0x90, KickStrength); F(0x94, KickDecay);
            BitConverter.GetBytes(2).CopyTo(e, 0x98);                    // kick type 2: thrown away from it
            CollisionPool.Plant(pool, idx, e);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"struck enemy slot {slot} for {attack} (entry {idx})");
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
                Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharaTint, TintR, TintG, TintB);
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
                if (pool != 0) CollisionPool.Deactivate(pool, idx);
                _shells.RemoveAt(i);
            }
        }

        private static void End()
        {
            TakeDown();
            { long pool = CollisionPool.Resolve(); foreach (var (idx, _) in _shells) if (pool != 0) CollisionPool.Deactivate(pool, idx); _shells.Clear(); }
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++) if (_confusedUntil[s] != default) Release(s);
            OwnsTable = false;
            _guardLatched = false; _guardSince = default;
        }
    }
}
