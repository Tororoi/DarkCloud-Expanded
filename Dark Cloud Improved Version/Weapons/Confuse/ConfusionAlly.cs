using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.Confusion;
using static Dark_Cloud_Improved_Version.ConfusionFriendlyFire;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Pocklekul's pockle in the friendly fire (it is a monster on a private manager, tools/stubs/ally_brain.s; nothing native
    /// tests a monster against monsters). Its unit 0 plants its swings as floor slot 0 does (owner 200) and stamps its shots as slot 0's, so
    /// each is told apart by the unit it is nearer (<see cref="Its"/>). Each tick while it lives: its open swings and its flying shots are
    /// tested against every live enemy, and a hit is planted on the one they reach as one of RUBY'S (<see cref="CollisionPool.PlayerHitEntry"/>:
    /// her kill, her ABS), its damage its own over her ranged distance factor (a shot then taken through the engine's own contact). An
    /// enemy whose HP that hit lowers is PROVOKED and goes after it until it falls or goes. Every enemy's open swing and every enemy shot
    /// that reaches its body plant a hit on it, owner −1 (damage − defence). One per pair per ContactCooldown, withdrawn after
    /// ShellLifeSeconds like the confused enemies' (<see cref="ConfusionFriendlyFire"/>). <see cref="Confusion.Tick"/> runs it; the
    /// Pocklekul's thread ticks while a pockle stands.</summary>
    internal static class ConfusionAlly
    {
        private const string Tag = "[Confusion/ConfusionAlly] ";
        private const int    SwingOwner = 200;                     // its unit 0's attack spheres: slot 0 × 5 + 200, as floor slot 0's
        private const long   Unit = 0x1E3D0, UnitState = 0x00, UnitHp = 0x24;   // its unit 0 (MONSTOR) in the manager
        private const long   Chara = 0x1FCD0, CharaPos = 0x10;    // its unit 0's CCharacter, and its position
        private const double ProvokeWindowSeconds = 0.3;          // a landed hit's HP loss looked for this long
        private static readonly DateTime[] _lastOn = new DateTime[Slots];   // it → enemy
        private static readonly DateTime[] _lastBy = new DateTime[Slots];   // enemy → it
        private static readonly bool[]     _provoked = new bool[Slots];
        private static readonly List<(int slot, int hp, DateTime until)> _landing = new();
        private static readonly Dictionary<int, (float x, float h, float y)> _shotSeen = new();
        private static readonly HashSet<int> _ownShots = new();

        /// <summary>The ally's manager (MMU), or 0 while none stands.</summary>
        private static long Manager()
        {
            uint g = Memory.ReadUInt(CodeCaves.AllyManager);
            return g != 0 && Memory.IsValidGuest(g) ? Memory.ToMmu(g) : 0;
        }

        /// <summary>Whether a pockle stands (its friendly fire wants ticking).</summary>
        internal static bool Up => Manager() != 0;

        /// <summary>One pass (from Confusion's tick, under its lock).</summary>
        internal static void Tick()
        {
            long m = Manager();
            if (m == 0 || Memory.ReadInt(m + Unit + UnitHp) <= 0 || Memory.ReadInt(m + Unit + UnitState) < 0) { End(); return; }   // gone, or falling
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            Swings(m, pool);
            HitsOnIt(m, pool);
            Shots(m, pool);
            Landings(m);
            for (int s = 0; s < Slots; s++)
                if (_provoked[s] && (!Enemies.IsLive(s) || IsConfused(s))) Release(s);
        }

        /// <summary>The enemies it provoked back on the player, its hits and shots forgotten.</summary>
        internal static void End()
        {
            for (int s = 0; s < Slots; s++) if (_provoked[s]) Release(s);
            _landing.Clear(); _shotSeen.Clear(); _ownShots.Clear();
        }

        private static void Release(int s)
        {
            _provoked[s] = false;
            if (!IsConfused(s)) AggroTable.PointConfused(s, StbExternCmd.PlayerPosGuest);
            if (Enemies.IsLive(s)) Log($"enemy slot {s} back on Ruby");
        }

        /// <summary>Whether something of slot 0's at (x, y) is its: nearer its unit than floor slot 0's (or floor slot 0 not live).</summary>
        private static bool Its(long m, float x, float y)
        {
            long me = m + Chara + CharaPos;
            float dx = Memory.ReadFloat(me) - x, dy = Memory.ReadFloat(me + 8) - y;
            if (!Enemies.IsLive(0)) return true;
            long f = EnemyAddresses.CharObjects.PosAddr(0);
            float fx = Memory.ReadFloat(f) - x, fy = Memory.ReadFloat(f + 8) - y;
            return dx * dx + dy * dy <= fx * fx + fy * fy;
        }

        /// <summary>Its open swings on the enemies they touch.</summary>
        private static void Swings(long m, long pool)
        {
            byte[] active = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (active == null) return;
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (BitConverter.ToInt32(active, i * 4) == 0) continue;
                long e = pool + i * CollisionPool.Stride;
                if (Memory.ReadInt(e + CollisionPool.Owner) != SwingOwner) continue;
                byte[] atk = Memory.ReadBytesBatch(e, CollisionPool.Stride);
                if (atk == null || BitConverter.ToInt32(atk, CollisionPool.GateA) != BitConverter.ToInt32(atk, CollisionPool.GateB)) continue;
                float ax = BitConverter.ToSingle(atk, 0x00), ay = BitConverter.ToSingle(atk, 0x08), ar = BitConverter.ToSingle(atk, CollisionPool.Radius);
                int damage = BitConverter.ToInt32(atk, 0x34);
                if (damage <= 0 || ar <= 0f || !Its(m, ax, ay)) continue;
                for (int v = 0; v < Slots; v++)
                {
                    if (!Enemies.IsLive(v) || (GameClock.Now - _lastOn[v]).TotalSeconds < ContactCooldown) continue;
                    if (EnemyBody.NearestHitSphereEdge(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), ax, ay) > ar) continue;
                    if (!HitEnemy(pool, v, damage, BitConverter.ToInt32(atk, 0x4C), atk)) return;
                    Log($"its swing lands on enemy slot {v} for {damage} before defence");
                }
            }
        }

        /// <summary>A hit planted on enemy <paramref name="v"/> as one of Ruby's — her ranged distance factor undone (CheckDmg scales a
        /// ranged character's hit by her distance to it: ≤ 20 × 1.5, from 50 down to × 0.5) — watched for the HP it takes.</summary>
        private static bool HitEnemy(long pool, int v, int damage, int reaction, byte[] kickFrom)
        {
            if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) return false;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return false;
            EnemyBody.BodyCentre(v, EnemyAddresses.FloorSlots.SlotAddr(v, 0), out float cx, out float ch, out float cy, out _);
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            float d = (float)Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py));
            float f = d <= 20f ? 1.5f : d >= 50f ? Math.Max(0.5f, (100f - (d - 50f)) / 100f) : 1f;
            byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, Math.Max(1, (int)Math.Round(damage / f)), 0);
            BitConverter.GetBytes(reaction).CopyTo(hit, 0x4C);
            if (kickFrom != null) Array.Copy(kickFrom, CollisionPool.KickOriginOff, hit, CollisionPool.KickOriginOff, CollisionPool.KickWordsSize);
            int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.Hp));
            CollisionPool.Plant(pool, idx, hit);
            _shells.Add((idx, GameClock.Now.AddSeconds(ShellLifeSeconds)));
            _lastOn[v] = GameClock.Now;
            if (!_provoked[v]) _landing.Add((v, hp, GameClock.Now.AddSeconds(ProvokeWindowSeconds)));
            return true;
        }

        /// <summary>An enemy its hit took HP from goes after it.</summary>
        private static void Landings(long m)
        {
            for (int k = _landing.Count - 1; k >= 0; k--)
            {
                var (v, hp, until) = _landing[k];
                if (_provoked[v] || !Enemies.IsLive(v)) { _landing.RemoveAt(k); continue; }
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.Hp)) < hp)
                {
                    _landing.RemoveAt(k);
                    if (!IsConfused(v) && AggroTable.PointConfused(v, (uint)(m + Chara + CharaPos - Memory.Pcsx2Base)))
                    {
                        _provoked[v] = true;
                        Log($"enemy slot {v} provoked: after the pockle");
                    }
                }
                else if (GameClock.Now >= until) _landing.RemoveAt(k);
            }
        }

        /// <summary>Every enemy's open swing that reaches its body (its own excepted): a hit on it.</summary>
        private static void HitsOnIt(long m, long pool)
        {
            byte[] active = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (active == null) return;
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (BitConverter.ToInt32(active, i * 4) == 0) continue;
                long e = pool + i * CollisionPool.Stride;
                int owner = Memory.ReadInt(e + CollisionPool.Owner);
                if (owner < 200 || (owner - 200) % 5 != 0) continue;
                int attacker = (owner - 200) / 5;
                if (attacker >= Slots || !Enemies.IsLive(attacker) || (GameClock.Now - _lastBy[attacker]).TotalSeconds < ContactCooldown) continue;
                byte[] atk = Memory.ReadBytesBatch(e, CollisionPool.Stride);
                if (atk == null || BitConverter.ToInt32(atk, CollisionPool.GateA) != BitConverter.ToInt32(atk, CollisionPool.GateB)) continue;
                float ax = BitConverter.ToSingle(atk, 0x00), ah = BitConverter.ToSingle(atk, 0x04), ay = BitConverter.ToSingle(atk, 0x08), ar = BitConverter.ToSingle(atk, CollisionPool.Radius);
                int damage = BitConverter.ToInt32(atk, 0x34);
                if (damage <= 0 || ar <= 0f || (attacker == 0 && Its(m, ax, ay)) || Edge(m, (ax, ah, ay), (ax, ah, ay), out _) > ar) continue;
                if (!PlantOnIt(m, pool, damage, BitConverter.ToInt32(atk, 0x4C), 0, atk)) return;
                _lastBy[attacker] = GameClock.Now;
                Log($"enemy slot {attacker}'s swing lands on the pockle for {damage} before defence");
            }
        }

        /// <summary>Every live flying shot, swept along its path since the last tick: its own (a slot-0 shot first seen nearer it than
        /// floor slot 0) against the live enemies, an enemy's against its body. A hit is planted and the shot taken through the engine's
        /// own contact where its path met the body.</summary>
        private static void Shots(long m, long pool)
        {
            uint packG = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(packG)) return;
            long pack = Memory.ToMmu(packG);
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
                    if (!live || owner < 0 || owner >= Slots) { _shotSeen.Remove(key); _ownShots.Remove(key); continue; }
                    long obj = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride;
                    long o = obj + ShotEffectPack.ObjPos;
                    var now = (x: Memory.ReadFloat(o), h: Memory.ReadFloat(o + 4), y: Memory.ReadFloat(o + 8));
                    if (!_shotSeen.TryGetValue(key, out var from))
                    {
                        from = now;
                        if (owner == 0 && Its(m, now.x, now.y)) _ownShots.Add(key);
                    }
                    _shotSeen[key] = now;
                    float reach = Math.Max(1f, Memory.ReadFloat(cfg + ShotEffectPack.CfgRadiusFlying));
                    int damage = Math.Max(1, Memory.ReadInt(fx + ShotEffectPack.OffDamage + i * 4));
                    int reaction = Memory.ReadInt(cfg + ShotEffectPack.CfgReaction);
                    (float x, float h, float y) at;
                    if (_ownShots.Contains(key))
                    {
                        int victim = -1; at = now;
                        for (int v = 0; v < Slots && victim < 0; v++)
                            if (Enemies.IsLive(v) && SegmentEdge3D(v, from, now, out at) <= reach) victim = v;
                        if (victim < 0) continue;
                        if (!HitEnemy(pool, victim, damage, reaction, null)) return;
                        Log($"its shot hits enemy slot {victim} for {damage} before defence");
                    }
                    else
                    {
                        if (Edge(m, from, now, out at) > reach) continue;
                        uint flags = Memory.ReadUInt(cfg + ShotEffectPack.CfgFlags);
                        uint elem = flags & EnemyStatus.ShotElementMask;
                        if (!PlantOnIt(m, pool, damage, reaction, elem != 0 && (flags & 0xFF00) == 0 ? elem : 0u, null)) return;
                        Log($"enemy slot {owner}'s shot hits the pockle for {damage} before defence");
                    }
                    Memory.WriteVec3(o, at.x, at.h, at.y);                    // the burst where the path met the body
                    ShotContact(fx, cfg, i, obj);
                    _shotSeen.Remove(key); _ownShots.Remove(key);
                }
            }
        }

        /// <summary>A hit planted on its body: owner −1 (damage − defence, nobody's), the reaction, the element, and (a swing's) kick words.</summary>
        private static bool PlantOnIt(long m, long pool, int damage, int reaction, uint element, byte[] kickFrom)
        {
            if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) return false;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return false;
            Centre(m, out float cx, out float ch, out float cy);
            byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, damage, 0);
            void I(int o, int val) => BitConverter.GetBytes(val).CopyTo(hit, o);
            I(CollisionPool.Owner, -1); I(0x60, -1); I(0x64, 0); I(0x68, -1); I(0x6C, 0);            // nobody's: damage − defence, no weapon, no drain, no credit
            I(0x4C, reaction); I(CollisionPool.Element, (int)element);
            if (kickFrom != null) Array.Copy(kickFrom, CollisionPool.KickOriginOff, hit, CollisionPool.KickOriginOff, CollisionPool.KickWordsSize);
            CollisionPool.Plant(pool, idx, hit);
            _shells.Add((idx, GameClock.Now.AddSeconds(ShellLifeSeconds)));
            return true;
        }

        /// <summary>The centre of its largest posed hit sphere (its unit's position, 10 up, with none).</summary>
        private static void Centre(long m, out float cx, out float ch, out float cy)
        {
            long up = m + Chara + CharaPos;
            float ux = Memory.ReadFloat(up), uy = Memory.ReadFloat(up + 8), best = 0f;
            cx = ux; cy = uy; ch = Memory.ReadFloat(up + 4) + 10f;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(m + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                float r = Memory.ReadFloat(m + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                long c = m + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float sx = Memory.ReadFloat(c), sy = Memory.ReadFloat(c + 8);
                if (r <= best || Math.Abs(sx - ux) > 80f || Math.Abs(sy - uy) > 80f) continue;
                best = r; cx = sx; ch = Memory.ReadFloat(c + 4); cy = sy;
            }
        }

        /// <summary>How near the segment a→b comes to its body: the least distance to the surface of its posed hit spheres (from its
        /// position less 10 with none), and the segment's point there.</summary>
        private static float Edge(long m, (float x, float h, float y) a, (float x, float h, float y) b, out (float x, float h, float y) at)
        {
            at = b;
            long up = m + Chara + CharaPos;
            float ux = Memory.ReadFloat(up), uh = Memory.ReadFloat(up + 4), uy = Memory.ReadFloat(up + 8), best = float.MaxValue;
            float dx = b.x - a.x, dh = b.h - a.h, dy = b.y - a.y, len2 = dx * dx + dh * dh + dy * dy;
            float Near(float cx, float ch, float cy, float r, out (float, float, float) q)
            {
                float t = len2 > 1e-6f ? Math.Clamp(((cx - a.x) * dx + (ch - a.h) * dh + (cy - a.y) * dy) / len2, 0f, 1f) : 0f;
                float qx = a.x + dx * t, qh = a.h + dh * t, qy = a.y + dy * t;
                q = (qx, qh, qy);
                return (float)Math.Sqrt((qx - cx) * (qx - cx) + (qh - ch) * (qh - ch) + (qy - cy) * (qy - cy)) - r;
            }
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(m + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = m + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;
                float edge = Near(cx, ch, cy, Memory.ReadFloat(m + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride), out var q);
                if (edge < best) { best = edge; at = q; }
            }
            if (best == float.MaxValue) best = Near(ux, uh + 10f, uy, 10f, out at);
            return best;
        }

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
