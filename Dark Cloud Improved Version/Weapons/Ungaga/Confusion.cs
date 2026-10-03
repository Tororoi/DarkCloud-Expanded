using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Confused enemies — Babel's Spear's Curse of Babel, and Super Steve's Terra Sword sphere (the kinomi's spinning
    /// stars). One user at a time (they are different weapons): it <see cref="Configure"/>s the confusion, <see cref="Confuse"/>s
    /// slots, and calls <see cref="Tick"/> every tick of its loop and <see cref="End"/> when it ends.
    ///
    ///  · TARGETS ride the Mirage's per-slot target-pointer table (CodeCaves.PtrTable, read by the cold-hosted _GET_POSITION /
    ///    _GET_DISTANCE): a confused slot's entry points at the LIVE position of its target, chosen every tick as the nearest
    ///    candidate — another live enemy's CCharacter position, or the player global — inside the configured area (Babel's: both
    ///    it and the player inside the spear's radius) or anywhere (no area). With no candidate it points at the slot's own wander
    ///    quadword (CodeCaves.BabelWander), a random spot near it renewed every <see cref="WanderSeconds"/> or once reached. The
    ///    table is owned here while any slot is confused or provoked (Mirage's loop stands down, <see cref="OwnsTable"/>);
    ///  · FRIENDLY FIRE: each tick, every OPEN attack entry a confused enemy has planted (pool owner = slot·5 + 200, gate words
    ///    equal) is tested against every other live enemy's body spheres, and every live flying shot it fired (the monster shot
    ///    pack's sub-shots, the firing slot at OffA060) is swept along the path it moved since the last tick; on contact a small hit
    ///    entry is planted on the victim — the attack's damage, reaction and kick, or the shot's damage, reaction, element and
    ///    statuses (as the Angel Gear's reflected shots carry them), owner −1 (damage − defence, no weapon stats, no WHP drain, no
    ///    kill credit) — one per attacker-victim pair per <see cref="ContactCooldown"/>; a shot is taken through the engine's own
    ///    contact (<see cref="ShotContact"/>);
    ///  · PROVOKED (when configured — the default): every enemy a confused one hits remembers it (most recent first). One that is
    ///    not confused itself goes after its most recent attacker still confused, the player nearer or not, and its swings and
    ///    shots hurt that attacker alone; when that attacker's confusion ends (or it dies) it turns on the next one still
    ///    confused, else back to the player — and so does a confused enemy whose own confusion ends while others are hitting it
    ///    ("coming to its senses");
    ///  · the TINT (when configured) is the unit's ambient add (CCharacter +0xCE0), × <see cref="TintScale"/>, re-asserted each
    ///    tick, cleared on release.</summary>
    internal static class Confusion
    {
        private const string Tag = "[Confusion] ";
        private const float  WanderSeconds    = 4f;     // a wandering enemy's spot renewed this often…
        private const float  WanderRange      = 40f;    // …within this of where it stands…
        private const float  WanderReached    = 8f;     // …or once it has got this close to it
        private const float  ContactCooldown  = 0.5f;   // one friendly-fire hit per attacker-victim pair this often
        private const float  ContactRadius    = 1f;     // the planted hit's own radius: the victim's sphere does the reaching
        private const double ShellLifeSeconds = 0.15;   // a planted hit stays this long before it is withdrawn
        private const int    ShellPoolReserve = 16;     // free pool entries always left to the engine
        private static readonly int Slots = EnemyAddresses.FloorSlots.Count;

        /// <summary>True while any enemy is confused or provoked: the per-slot target table is this class's (Mirage's loop leaves it).</summary>
        internal static bool OwnsTable { get; private set; }
        /// <summary>The configured tint's strength 0..1 (Babel's follows its spear's fade).</summary>
        internal static float TintScale = 1f;

        private static (float x, float y, float r)? _area;
        private static float[] _tint;
        private static bool _provokes;
        private static string _owner = "";

        private static readonly Random     _rng          = new();
        private static readonly DateTime[] _until        = new DateTime[Slots];
        private static readonly int[]      _victim       = new int[Slots];   // −1 = the player, −2 = wandering, −3 = unset
        private static readonly DateTime[] _wanderSet    = new DateTime[Slots];
        private static readonly List<int>[] _hitBy       = new List<int>[Slots];   // the confused slots that have hit it, most recent first
        private static readonly bool[]     _provoked     = new bool[Slots];        // its pointer is on a provoker (put back when none is left)
        private static readonly object     _lock         = new();
        private static DateTime _lastTick;
        private const double MinTickSeconds = 0.012;    // several loops may tick (ConfuseAbility's, a weapon's): one pass a frame
        private static readonly DateTime[,] _lastContact = new DateTime[Slots, Slots];
        private static readonly List<(int idx, DateTime until)> _shells = new();
        private static readonly Dictionary<int, (float x, float h, float y)> _shotLast = new();   // each tracked sub-shot's position last tick
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
        private static bool IsProvoked(int slot) => Provoker(slot) >= 0;

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
            OwnsTable = any && Mirage.Armed;
            if (any && (GameClock.Now - _lastReport).TotalSeconds >= 1) { _lastReport = GameClock.Now; Report(); }
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
            OwnsTable = false; TintScale = 1f;
            _area = null; _tint = null; _provokes = true; _owner = "";   // the next user configures afresh; procs confuse plainly
        }

        private static void Point(int slot, uint ptr)
        {
            if (!Mirage.Armed) return;
            if (Memory.ReadUInt(CodeCaves.PtrAddr(slot)) != ptr) Memory.WriteUInt(CodeCaves.PtrAddr(slot), ptr);
        }

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
            if (Mirage.Armed) Memory.WriteUInt(CodeCaves.PtrAddr(slot), StbExternCmd.PlayerPosGuest);
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
            if (Mirage.Armed && _until[slot] == default) Memory.WriteUInt(CodeCaves.PtrAddr(slot), StbExternCmd.PlayerPosGuest);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {slot} comes to its senses");
        }

        /// <summary>Whether <paramref name="attacker"/>'s attack may land on <paramref name="victim"/>: a confused attacker's on any
        /// other enemy, a provoked one's on its provoker alone.</summary>
        private static bool MayHit(int attacker, int victim)
            => attacker != victim && (IsConfused(attacker) || Provoker(attacker) == victim);

        private static bool Attacks(int slot) => IsConfused(slot) || IsProvoked(slot);

        /// <summary>A landed friendly hit: the victim provoked by a confused attacker.</summary>
        private static void Landed(int attacker, int victim) { if (IsConfused(attacker)) Provoke(victim, attacker); }

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

        // ── friendly fire ──
        /// <summary>An attacking enemy's open attack sphere touching an enemy it may hit: a hit planted on that body.</summary>
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
                if (attacker >= Slots || !Attacks(attacker)) continue;
                if (Memory.ReadInt(e + CollisionPool.GateA) != Memory.ReadInt(e + CollisionPool.GateB)) continue;   // not open this frame
                byte[] atk = Memory.ReadBytesBatch(e, CollisionPool.Stride);
                if (atk == null) continue;
                float ax = BitConverter.ToSingle(atk, 0x00), ay = BitConverter.ToSingle(atk, 0x08), ar = BitConverter.ToSingle(atk, CollisionPool.Radius);
                int damage = BitConverter.ToInt32(atk, 0x34);
                if (damage <= 0 || ar <= 0f) continue;
                for (int v = 0; v < Slots; v++)
                {
                    if (!Enemies.IsLive(v) || !MayHit(attacker, v)) continue;
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
                    _shells.Add((idx, GameClock.Now.AddSeconds(ShellLifeSeconds)));
                    _lastContact[attacker, v] = GameClock.Now;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {attacker}'s swing lands on enemy slot {v} for {damage} before defence (entry {idx})");
                    Landed(attacker, v);
                }
            }
        }

        /// <summary>An attacking enemy's SHOT reaching an enemy it may hit (the engine tests the monster shot pack's sub-shots against
        /// the player alone — the config's victim mask): each tick, every live flying sub-shot fired by such a slot (OffA060, stamped
        /// by SetUserID2) is swept along the path it moved since the last tick against the other live enemies' hit spheres, widened by
        /// the shot's flying radius. On contact a hit is planted on the victim — the shot's damage, the config's reaction and element,
        /// its statuses applied as data (owner −1; the element and statuses as the Angel Gear's reflected shots carry them) — and the
        /// shot put where its path met the enemy and taken through the engine's own CONTACT (<see cref="ShotContact"/>: its impact
        /// motion, or a bomb's detonation). Its burst plants its own damage as it would anywhere: the player is not sheltered.</summary>
        private static void ShotHits()
        {
            uint packG = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(packG)) return;
            long pack = Memory.ToMmu(packG);
            long pool = CollisionPool.Resolve();
            for (int p = 0; p < ShotEffectPack.PackSlots; p++)
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
                    if (!live || owner < 0 || owner >= Slots || !Attacks(owner)) { _shotLast.Remove(key); continue; }
                    long o = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride + ShotEffectPack.ObjPos;
                    var now = (Memory.ReadFloat(o), Memory.ReadFloat(o + 4), Memory.ReadFloat(o + 8));
                    var from = _shotLast.TryGetValue(key, out var was) ? was : now;
                    _shotLast[key] = now;
                    float reach = Math.Max(1f, Memory.ReadFloat(cfg + ShotEffectPack.CfgRadiusFlying));
                    for (int v = 0; v < Slots; v++)
                    {
                        if (!Enemies.IsLive(v) || !MayHit(owner, v)) continue;
                        if (SegmentEdge3D(v, from, now, out var at) > reach) continue;
                        if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return;
                        int idx = CollisionPool.TakeFreeSlot(pool);
                        if (idx < 0) return;
                        int damage = Math.Max(1, Memory.ReadInt(fx + ShotEffectPack.OffDamage + i * 4));
                        BigBang.BodyCentre(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), out float cx, out float ch, out float cy, out _);
                        byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, damage, 0);
                        void I(int off, int val) => BitConverter.GetBytes(val).CopyTo(hit, off);
                        I(CollisionPool.Owner, -1); I(0x60, -1); I(0x64, 0); I(0x68, -1); I(0x6C, 0);    // nobody's: damage − defence, no weapon, no drain, no credit
                        I(0x4C, Memory.ReadInt(cfg + ShotEffectPack.CfgReaction));                        // the shot's reaction
                        // Its element as the Angel Gear's reflected shots carry it: +0x50 a PURE element bit (a status bit there sends CheckDmg's
                        // element branch through the wrong column), the statuses applied as data with CheckDmg's own rules.
                        uint flags = Memory.ReadUInt(cfg + ShotEffectPack.CfgFlags);
                        uint elem = flags & AngelGear.ShotElementMask, stat = flags & AngelGear.ShotEnemyStatusMask;
                        I(CollisionPool.Element, (int)(elem != 0 && (flags & 0xFF00) == 0 ? elem : 0u));
                        string statusNote = stat != 0 ? AngelGear.ApplyReflectedStatus(v, stat) : "";
                        CollisionPool.Plant(pool, idx, hit);
                        _shells.Add((idx, GameClock.Now.AddSeconds(ShellLifeSeconds)));
                        long obj = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride;
                        Memory.WriteVec3(obj + ShotEffectPack.ObjPos, at.x, at.h, at.y);                    // the burst where the path met the enemy
                        ShotContact(fx, cfg, i, obj);
                        _shotLast.Remove(key);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {owner}'s shot hits enemy slot {v} for {damage} before defence (entry {idx}){statusNote}");
                        Landed(owner, v);
                        break;
                    }
                }
            }
        }

        /// <summary>The engine's own contact, as Step__12CSHOT_EFFECT (0x1AC180) does it when checkCollision meets something in flight:
        /// phase 2 and its KEY from the config's phase table (+0x4C). No impact KEY: the shot is put out — and a bomb-type config
        /// (+0x54 == 100) detonates, SetBombEffect(size +0x58, the position, the config's victim mask +0x48, +0x5C) — as it would
        /// have anywhere. An impact KEY: the shot's object restarts on it (its first frame from the object's frame table, motion flags
        /// 6, the KEY's own rate) and its velocity becomes the phase's speed (+0x18 + phase × 4). The phase is written last.</summary>
        private static void ShotContact(long fx, long cfg, int i, long obj)
        {
            const int Phase = 2, CfgPhaseKeys = 0x4C, CfgPhaseSpeeds = 0x18, CfgBombKind = 0x54, CfgBombSize = 0x58, CfgBombArg = 0x5C, BombKind = 100;
            uint SetBombEffect = 0x001D5940;
            short key = Memory.ReadShort(cfg + CfgPhaseKeys + Phase * 2);
            if (key == -1)
            {
                Memory.WriteUShort(fx + ShotEffectPack.OffPhase + i * 2, Phase);
                Memory.WriteUShort(fx + ShotEffectPack.OffActive + i * 2, 0);
                if (Memory.ReadShort(cfg + CfgBombKind) == BombKind)
                    NativeCall.Invoke(SetBombEffect, out _, (uint)(obj + ShotEffectPack.ObjPos - 0x20000000L), (uint)Memory.ReadInt(cfg + ShotEffectPack.CfgVictimMask), (uint)Memory.ReadInt(cfg + CfgBombArg),
                                      f12: Memory.ReadFloat(cfg + CfgBombSize));
                return;
            }
            uint table = Memory.ReadGuestPtr(obj + ShotEffectPack.ObjFrameTb);
            if (Memory.IsValidGuest(table)) Memory.WriteFloat(obj + ShotEffectPack.ObjFrame, Memory.ReadInt(Memory.ToMmu(table) + key * 0x10));
            Memory.WriteInt  (obj + ShotEffectPack.ObjMotId, key);
            Memory.WriteInt  (obj + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat(obj + ShotEffectPack.ObjMotSpd, -1f);
            long d = fx + ShotEffectPack.OffDir + i * 0x10;
            float dx = Memory.ReadFloat(d), dh = Memory.ReadFloat(d + 4), dy = Memory.ReadFloat(d + 8), len = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy);
            float speed = Memory.ReadFloat(cfg + CfgPhaseSpeeds + Phase * 4);
            if (len > 1e-6f) Memory.WriteVec3(d, dx / len * speed, dh / len * speed, dy / len * speed);
            Memory.WriteUShort(fx + ShotEffectPack.OffPhase + i * 2, Phase);
        }

        /// <summary>How near the segment <paramref name="a"/>→<paramref name="b"/> comes to an enemy's body: the least distance from it
        /// to the surface of any of the enemy's active hit spheres placed this frame (within 80 of its unit across the ground), and the
        /// segment's point there (<paramref name="at"/>). MaxValue with none.</summary>
        internal static float SegmentEdge3D(int slot, (float x, float h, float y) a, (float x, float h, float y) b, out (float x, float h, float y) at)
        {
            at = b;
            long bc = BodyCollision.SlotBase(slot), up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uy = Memory.ReadFloat(up + 8), best = float.MaxValue;
            float dx = b.x - a.x, dh = b.h - a.h, dy = b.y - a.y, len2 = dx * dx + dh * dh + dy * dy;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(bc + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = bc + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;
                float t = len2 > 1e-6f ? Math.Clamp(((cx - a.x) * dx + (ch - a.h) * dh + (cy - a.y) * dy) / len2, 0f, 1f) : 0f;
                float qx = a.x + dx * t, qh = a.h + dh * t, qy = a.y + dy * t, px = qx - cx, ph = qh - ch, py = qy - cy;
                float edge = (float)Math.Sqrt(px * px + ph * ph + py * py) - Memory.ReadFloat(bc + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                if (edge < best) { best = edge; at = (qx, qh, qy); }
            }
            return best;
        }

        /// <summary>Planted hits the engine has not consumed within their life are withdrawn.</summary>
        private static void RetireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                if (GameClock.Now < _shells[i].until) continue;
                if (pool != 0) CollisionPool.Deactivate(pool, _shells[i].idx);
                _shells.RemoveAt(i);
            }
        }
    }
}
