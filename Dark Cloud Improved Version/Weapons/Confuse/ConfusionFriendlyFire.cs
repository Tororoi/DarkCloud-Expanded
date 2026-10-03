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
        internal static readonly Dictionary<int, (float x, float h, float y)> _shotLast = new();   // each tracked sub-shot's position last tick

        /// <summary>An attacking enemy's open attack sphere touching an enemy it may hit: a hit planted on that body.</summary>
        internal static void ContactHits()
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
                    if (EnemyBody.NearestHitSphereEdge(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), ax, ay) > ar) continue;   // no contact
                    if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) return;
                    int idx = CollisionPool.TakeFreeSlot(pool);
                    if (idx < 0) return;
                    EnemyBody.BodyCentre(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), out float cx, out float ch, out float cy, out _);
                    byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, damage, 0);
                    void I(int o, int val) => BitConverter.GetBytes(val).CopyTo(hit, o);
                    I(CollisionPool.Owner, -1); I(0x60, -1); I(0x64, 0); I(0x68, -1); I(0x6C, 0);        // nobody's: damage − defence, no weapon, no drain, no credit
                    Array.Copy(atk, 0x4C, hit, 0x4C, 4);                                                // the attack's reaction…
                    Array.Copy(atk, CollisionPool.KickOriginOff, hit, CollisionPool.KickOriginOff, CollisionPool.KickWordsSize);   // …and its kick words (+0x80..+0x98), as they stand
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
        internal static void ShotHits()
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
                        EnemyBody.BodyCentre(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), out float cx, out float ch, out float cy, out _);
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
