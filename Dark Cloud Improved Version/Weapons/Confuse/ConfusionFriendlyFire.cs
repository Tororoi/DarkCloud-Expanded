using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.Confusion;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Friendly fire among confused and provoked enemies. Each tick, every OPEN attack entry such an enemy has planted (pool
    /// owner slot·5 + 200) is tested against every enemy it may hit (<see cref="ContactHits"/>), and every live flying shot it fired
    /// is swept along its path since the last tick (<see cref="ShotHits"/>); on contact a small hit entry is planted on the victim's
    /// body — the attack's damage, reaction and kick, or the shot's damage, reaction, element and statuses — owner −1 (damage − defence,
    /// no weapon stats, no WHP drain, no kill credit), one per attacker-victim pair per ContactCooldown, withdrawn after ShellLifeSeconds
    /// (<see cref="RetireShells"/>); a shot is taken through the engine's own contact (<see cref="ShotContact"/>). One of the
    /// <see cref="Confusion"/> classes, which share their members through using static.</summary>
    internal static class ConfusionFriendlyFire
    {
        private const string Tag = "[Confusion/ConfusionFriendlyFire] ";
        private static readonly DateTime[,] _lastContact = new DateTime[Slots, Slots];
        internal static readonly List<(int idx, DateTime until)> _shells = new();
        internal static readonly ShotSweep _shotSweep = new();   // the confused enemies' shots, each with its path since the last tick

        /// <summary>An attacking enemy's open attack sphere touching an enemy it may hit: a hit planted on that body.</summary>
        internal static void ContactHits()
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            foreach (var (_, atk) in AttackScan.OpenEntries(pool))
            {
                int attacker = AttackScan.EnemyOwner(atk);
                if (attacker < 0 || !Attacks(attacker)) continue;
                var (ax, _, ay) = AttackScan.Centre(atk);
                float ar = AttackScan.Radius(atk);
                int damage = AttackScan.Damage(atk);
                if (damage <= 0 || ar <= 0f) continue;
                for (int v = 0; v < Slots; v++)
                {
                    if (!Enemies.IsLive(v) || !MayHit(attacker, v)) continue;
                    if ((GameClock.Now - _lastContact[attacker, v]).TotalSeconds < ContactCooldown) continue;
                    if (EnemyBody.NearestHitSphereEdge(EnemyBody.Body.Of(v), ax, ay) > ar) continue;   // no contact
                    byte[] hit = NobodysHit(EnemyBody.Body.Of(v), damage);
                    BitConverter.GetBytes(AttackScan.Reaction(atk)).CopyTo(hit, 0x4C);              // the attack's reaction…
                    AttackScan.CopyKick(atk, hit);                                                   // …and its kick words
                    int idx = PlantShell(pool, hit);
                    if (idx < 0) return;
                    _lastContact[attacker, v] = GameClock.Now;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {attacker}'s swing lands on enemy slot {v} for {damage} before defence (entry {idx})");
                    Landed(attacker, v);
                }
            }
        }

        /// <summary>An attacking enemy's SHOT reaching an enemy it may hit (the engine tests the monster shot pack's sub-shots against
        /// the player alone — the config's victim mask): each tick, every live flying sub-shot fired by such a slot is swept along the path
        /// it moved since the last tick (<see cref="ShotSweep"/>) against the other live enemies' hit spheres, widened by the shot's flying
        /// radius. On contact a hit is planted on the victim — the shot's damage, the config's reaction and element, its statuses applied as
        /// data (owner −1; the element and statuses as the Angel Gear's reflected shots carry them) — and the shot put where its path met
        /// the enemy and taken through the engine's own CONTACT (<see cref="ShotContact"/>: its impact motion, or a bomb's detonation).
        /// Its burst plants its own damage as it would anywhere: the player is not sheltered.</summary>
        internal static void ShotHits()
        {
            long pool = CollisionPool.Resolve();
            foreach (var shot in _shotSweep.Live(Attacks))
            {
                for (int v = 0; v < Slots; v++)
                {
                    if (!Enemies.IsLive(v) || !MayHit(shot.Owner, v)) continue;
                    if (EnemyBody.SegmentEdge(EnemyBody.Body.Of(v), shot.From, shot.Now, out var at) > shot.Reach) continue;
                    int damage = shot.Damage;
                    byte[] hit = NobodysHit(EnemyBody.Body.Of(v), damage);
                    BitConverter.GetBytes(shot.Reaction).CopyTo(hit, 0x4C);                         // the shot's reaction
                    // Its element as the Angel Gear's reflected shots carry it: +0x50 a PURE element bit (a status bit there sends CheckDmg's
                    // element branch through the wrong column), the statuses applied as data with CheckDmg's own rules.
                    uint flags = shot.CfgFlags;
                    uint elem = flags & EnemyStatus.ShotElementMask, stat = flags & EnemyStatus.ShotEnemyStatusMask;
                    BitConverter.GetBytes((int)(elem != 0 && (flags & 0xFF00) == 0 ? elem : 0u)).CopyTo(hit, CollisionPool.Element);
                    int idx = PlantShell(pool, hit);
                    if (idx < 0) return;
                    string statusNote = stat != 0 ? EnemyStatus.ApplyShotStatus(v, stat) : "";
                    Memory.WriteVec3(shot.Obj + ShotEffectPack.ObjPos, at.x, at.h, at.y);              // the burst where the path met the enemy
                    ShotContact(shot.Fx, shot.Cfg, shot.Sub, shot.Obj);
                    _shotSweep.Forget(shot.Key);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _owner + $"enemy slot {shot.Owner}'s shot hits enemy slot {v} for {damage} before defence (entry {idx}){statusNote}");
                    Landed(shot.Owner, v);
                    break;
                }
            }
        }

        /// <summary>A hit on <paramref name="body"/>'s largest posed sphere that is nobody's — damage − defence, no weapon stats, no WHP
        /// drain, no kill credit (owner −1) — for the caller to finish (reaction, element, kick) and <see cref="PlantShell"/>.</summary>
        internal static byte[] NobodysHit(EnemyBody.Body body, int damage)
        {
            EnemyBody.BodyCentre(body, out float cx, out float ch, out float cy, out _);
            byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, damage, 0);
            void I(int o, int val) => BitConverter.GetBytes(val).CopyTo(hit, o);
            I(CollisionPool.Owner, -1); I(0x60, -1); I(0x64, 0); I(0x68, -1); I(0x6C, 0);
            return hit;
        }

        /// <summary>A friendly-fire hit planted (ShellPoolReserve entries always left to the engine) and withdrawn after ShellLifeSeconds
        /// (<see cref="RetireShells"/>): its entry, or −1 with no room.</summary>
        internal static int PlantShell(long pool, byte[] hit)
        {
            if (pool == 0 || CollisionPool.FreeCount(pool) <= ShellPoolReserve) return -1;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return -1;
            CollisionPool.Plant(pool, idx, hit);
            _shells.Add((idx, GameClock.Now.AddSeconds(ShellLifeSeconds)));
            return idx;
        }

        /// <summary>The engine's own contact, as Step__12CSHOT_EFFECT (0x1AC180) does it when checkCollision meets something in flight:
        /// phase 2 and its KEY from the config's phase table (+0x4C). No impact KEY: the shot is put out — and a bomb-type config
        /// (+0x54 == 100) detonates, SetBombEffect(size +0x58, the position, the config's victim mask +0x48, +0x5C) — as it would
        /// have anywhere. An impact KEY: the shot's object restarts on it (its first frame from the object's frame table, motion flags
        /// 6, the KEY's own rate) and its velocity becomes the phase's speed (+0x18 + phase × 4). The phase is written last.</summary>
        internal static void ShotContact(long fx, long cfg, int i, long obj)
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

        /// <summary>Planted hits the engine has not consumed within their life are withdrawn.</summary>
        internal static void RetireShells()
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
