using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.Confusion;
using static Dark_Cloud_Improved_Version.ConfusionFriendlyFire;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Pocklekul's pockle in the friendly fire (it is a monster on a private manager, tools/stubs/ally_brain.s; nothing native
    /// tests a monster against monsters). It drops nothing when it falls but the gold its stealing swings took (<see cref="Summoned"/>,
    /// <see cref="Steal"/>). Its unit 0 plants its swings as floor slot 0 does (owner 200) but with
    /// mask 0 (tools/stubs/ally_hooks.s: they never hit the player), which tells them apart; its shots are stamped slot 0's and told
    /// apart by the unit they are first seen nearer (<see cref="Its"/>). Each tick while it lives: every attack sphere placed since the last
    /// tick, frame by frame (the swing ring, tools/stubs/ally_record.s — <see cref="Ring"/>), and its flying shots are tested against every
    /// live enemy, and a hit is planted on the one they reach as one of the SUMMONER'S (<see cref="CollisionPool.PlayerHitEntry"/>:
    /// their kill, their ABS), its damage its own over their ranged distance factor (a shot then taken through the engine's own contact). An
    /// enemy whose HP that hit lowers — or that its body stops (<see cref="Blocked"/>) — is PROVOKED and goes after it until it falls or goes. Every enemy's swing (from the ring) and every enemy shot
    /// that reaches its body plant a hit on it, owner −1 (damage − defence). One per pair per ContactCooldown, withdrawn after
    /// ShellLifeSeconds like the confused enemies' (<see cref="ConfusionFriendlyFire"/>). <see cref="Confusion.Tick"/> runs it; the
    /// Pocklekul's thread ticks while a pockle stands.</summary>
    internal static class ConfusionAlly
    {
        private const string Tag = "[Confusion/ConfusionAlly] ";
        private const int    AllyHitMask = 0x100;                  // what the hits planted on it carry: the ally's unit takes it, no floor unit does
        private const int    SwingOwner = 200;                     // its unit 0's attack spheres: slot 0 × 5 + 200, as floor slot 0's (theirs mask 1, its 0)
        private const long   Unit = 0x1E3D0, UnitState = 0x00, UnitHp = 0x24;   // its unit 0 (MONSTOR) in the manager
        private const double ProvokeWindowSeconds = 0.3;          // a landed hit's HP loss looked for this long
        private const int    StealGoldFlag = 0x40000, StealPercent = 20;   // a swing's +0x50 bit, and the engine's own chance (BtCheckDamageProc)
        private static readonly Random _rng = new();
        private static readonly DateTime[] _lastOn = new DateTime[Slots];   // it → enemy
        private static readonly DateTime[] _lastBy = new DateTime[Slots];   // enemy → it
        private static readonly bool[]     _provoked = new bool[Slots];
        private static readonly List<(int slot, int hp, DateTime until)> _landing = new();   // every planted hit watched for the HP it takes (slot −1: the ally)
        private static readonly ShotSweep _shots = new();                   // every floor slot's shots, its own (slot 0's) among them
        private static readonly HashSet<int> _ownShots = new();             // the keys of its own, told at first sight

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
            Blocked(m);
            Ring(m, pool);
            Shots(m, pool);
            Landings(m);
            for (int s = 0; s < Slots; s++)
                if (_provoked[s] && (!Enemies.IsLive(s) || IsConfused(s))) Release(s);
        }

        /// <summary>The enemies it provoked back on the player, its hits and shots forgotten.</summary>
        internal static void End()
        {
            for (int s = 0; s < Slots; s++) if (_provoked[s]) Release(s);
            _landing.Clear(); _shots.Clear(); _ownShots.Clear();
        }

        private static void Release(int s)
        {
            _provoked[s] = false;
            if (!IsConfused(s)) AggroTable.PointConfused(s, StbExternCmd.PlayerPosGuest);
            if (Enemies.IsLive(s)) Log($"enemy slot {s} back on the player");
        }

        /// <summary>Whether something of slot 0's at (x, y) is its: nearer its unit than floor slot 0's (or floor slot 0 not live).</summary>
        private static bool Its(long m, float x, float y)
        {
            long me = EnemyBody.Body.Ally(m).Pos;
            float dx = Memory.ReadFloat(me) - x, dy = Memory.ReadFloat(me + 8) - y;
            if (!Enemies.IsLive(0)) return true;
            long f = EnemyAddresses.CharObjects.PosAddr(0);
            float fx = Memory.ReadFloat(f) - x, fy = Memory.ReadFloat(f + 8) - y;
            return dx * dx + dy * dy <= fx * fx + fy * fy;
        }

        // ── the swing ring (tools/stubs/ally_record.s): every attack sphere CheckDmg placed while the ally stands, one record a frame ──
        private const int RingOff = 0xAB00, RingRecords = 256, RecordSize = 32, WhoAlly = 16;
        private static uint _ringRead;                      // the records already tested (the summon zeroes the ring's count)

        /// <summary>Every attack sphere recorded since the last pass, each frame's: the ally's on the live enemies it reaches (a hit planted
        /// as one of the summoner's), each enemy's on the ally's body (a hit planted on it). One per pair per ContactCooldown. The kick
        /// words come from the pool's open entries (CheckDmg writes them after the sphere): each attacker's latest.</summary>
        private static void Ring(long m, long pool)
        {
            long ring = m + RingOff;
            uint count = Memory.ReadUInt(ring);
            if (count == _ringRead) { CloseSwingWindow(); return; }
            if (count - _ringRead > RingRecords) _ringRead = count - RingRecords;   // fallen behind: the oldest were written over
            byte[] recs = Memory.ReadBytesBatch(ring + 0x10, RingRecords * RecordSize);
            if (recs == null) return;
            byte[] allyKick = null;
            var enemyKick = new byte[Slots][];
            foreach (var (_, e) in AttackScan.OpenEntries(pool))
            {
                if (BitConverter.ToInt32(e, CollisionPool.Owner) == SwingOwner && BitConverter.ToInt32(e, CollisionPool.Mask) == 0) allyKick = e;
                else if (AttackScan.EnemyOwner(e) is int a and >= 0) enemyKick[a] = e;
            }
            var snaps = new EnemyBody.Snapshot[Slots];
            EnemyBody.Snapshot me = null;
            for (uint i = _ringRead; i != count; i++)
            {
                int o = (int)(i % RingRecords) * RecordSize;
                float x = BitConverter.ToSingle(recs, o), h = BitConverter.ToSingle(recs, o + 4), y = BitConverter.ToSingle(recs, o + 8), r = BitConverter.ToSingle(recs, o + 12);
                int damage = BitConverter.ToInt32(recs, o + 16), reaction = BitConverter.ToInt32(recs, o + 20), flags = BitConverter.ToInt32(recs, o + 24);
                uint tag = BitConverter.ToUInt32(recs, o + 28);
                int who = (int)(tag & 0xFF), mask = (int)((tag >> 8) & 0xFF), frame = (int)(tag >> 16);
                if (damage <= 0 || r <= 0f) continue;
                if (who == WhoAlly)
                {
                    OpenSwingWindow(frame, r);
                    for (int v = 0; v < Slots; v++)
                    {
                        if (!Enemies.IsLive(v)) continue;
                        if ((snaps[v] ??= EnemyBody.Snapshot.Read(EnemyBody.Body.Of(v))) is not { } body) continue;
                        float edge = body.Edge2D(x, y);
                        NoteSwing(v, edge);
                        if (edge > r || (GameClock.Now - _lastOn[v]).TotalSeconds < ContactCooldown) continue;
                        if (!HitEnemy(pool, v, damage, reaction, allyKick)) { _ringRead = count; return; }
                        _swingPlanted = true;
                        Log($"its swing lands on enemy slot {v} for {damage} before defence");
                        if ((flags & StealGoldFlag) != 0 && _rng.Next(100) < StealPercent) Steal(m, v);
                    }
                }
                else if (who < Slots && (mask & (int)CollisionPool.HurtsPlayerMask) != 0)
                {
                    if (!Enemies.IsLive(who) || (GameClock.Now - _lastBy[who]).TotalSeconds < ContactCooldown) continue;
                    if ((me ??= EnemyBody.Snapshot.Read(EnemyBody.Body.Ally(m))) is null || me.Edge3D(x, h, y) > r) continue;
                    if (!PlantOnIt(m, pool, damage, reaction, 0, enemyKick[who])) { _ringRead = count; return; }
                    _lastBy[who] = GameClock.Now;
                    Log($"enemy slot {who}'s swing lands on the pockle for {damage} before defence");
                }
            }
            _ringRead = count;
        }

        // DIAGNOSTIC (behind DebugDiagnostics): one line per swing window of the ally's — the frames it was recorded in, how near its
        // sphere came to an enemy's body, and whether it planted
        private const int SwingGapFrames = 6;
        private static int _swingFrames, _swingFirst, _swingLast, _swingSlot = -1;
        private static float _swingEdge = float.MaxValue, _swingRadius;
        private static bool _swingPlanted;

        private static void OpenSwingWindow(int frame, float radius)
        {
            if (!DebugDiagnostics.Enabled) return;
            if (_swingFrames > 0 && ((frame - _swingLast) & 0xFFFF) > SwingGapFrames) CloseSwingWindow();
            if (_swingFrames == 0) { _swingFirst = frame; _swingPlanted = false; }
            if (_swingFrames == 0 || frame != _swingLast) _swingFrames++;
            _swingLast = frame; _swingRadius = radius;
        }

        private static void NoteSwing(int v, float edge)
        {
            if (!DebugDiagnostics.Enabled) return;
            if (edge < _swingEdge) { _swingEdge = edge; _swingSlot = v; }
        }

        private static void CloseSwingWindow()
        {
            if (_swingFrames == 0) return;
            Log($"its swing window closed: radius {_swingRadius:F1}, {_swingFrames} frames recorded over {((_swingLast - _swingFirst) & 0xFFFF) + 1}, "
                + $"nearest enemy slot {_swingSlot} body edge {_swingEdge:F1} away — " + (_swingPlanted ? "planted" : "no contact"));
            _swingFrames = 0; _swingEdge = float.MaxValue; _swingSlot = -1;
        }

        /// <summary>A hit planted on enemy <paramref name="v"/> as one of the summoner's (Ruby or Xiao, both ranged) — the ranged distance
        /// factor undone (CheckDmg scales a ranged character's hit by their distance to it: ≤ 20 × 1.5, from 50 down to × 0.5) — watched for
        /// the HP it takes.</summary>
        private static bool HitEnemy(long pool, int v, int damage, int reaction, byte[] kickFrom)
        {
            EnemyBody.BodyCentre(EnemyBody.Body.Of(v), out float cx, out float ch, out float cy, out _);
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            float d = (float)Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py));
            float f = d <= 20f ? 1.5f : d >= 50f ? Math.Max(0.5f, (100f - (d - 50f)) / 100f) : 1f;
            byte[] hit = CollisionPool.PlayerHitEntry(cx, ch, cy, ContactRadius, Math.Max(1, (int)Math.Round(damage / f)), 0);
            BitConverter.GetBytes(reaction).CopyTo(hit, 0x4C);
            if (kickFrom != null) AttackScan.CopyKick(kickFrom, hit);
            int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.Hp));
            if (PlantShell(pool, hit) < 0) return false;
            _lastOn[v] = GameClock.Now;
            _landing.Add((v, hp, GameClock.Now.AddSeconds(ProvokeWindowSeconds)));
            return true;
        }

        /// <summary>A stealing swing (the pockles' melee, flagged 0x40000 as against the player) that landed and won the engine's 20%:
        /// the enemy's gold — what it would drop — goes into the pockle's stolen gold, which the death block drops when it falls; the
        /// enemy has none left to drop.</summary>
        private static void Steal(long m, int v)
        {
            long gold = EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.MinGoldDrop);
            int amount = Memory.ReadInt(gold);
            if (amount <= 0) return;
            long stolen = m + Unit + EnemySlotOffsets.StolenGold;
            Memory.WriteInt(stolen, Memory.ReadInt(stolen) + amount);
            Memory.WriteInt(gold, 0);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.DropChance), 0);
            Log($"its swing steals {amount} gold from enemy slot {v} (it carries {Memory.ReadInt(stolen)})");
        }

        /// <summary>A pockle just summoned: no drops of its own and no kill ABS — only gold it steals falls when it does.</summary>
        internal static void Summoned()
        {
            long m = Manager();
            if (m == 0) return;
            Memory.WriteUShort(m + Unit + EnemySlotOffsets.DropsItems, 0);
            Memory.WriteInt(m + Unit + EnemySlotOffsets.Abs, 0);
            Memory.WriteInt(m + Unit + EnemySlotOffsets.StolenGold, 0);
            _ringRead = 0;                                                    // the summon zeroed its swing ring
            _swingFrames = 0; _swingEdge = float.MaxValue; _swingSlot = -1;
        }

        /// <summary>An enemy its body stopped (the native block rule notes it in CodeCaves.AllyBlocked) goes after it, as one it hit does.</summary>
        private static void Blocked(long m)
        {
            uint bits = Memory.ReadUInt(CodeCaves.AllyBlocked);
            if (bits == 0) return;
            Memory.WriteUInt(CodeCaves.AllyBlocked, 0);
            for (int v = 0; v < Slots; v++)
                if ((bits & (1u << v)) != 0) Provoke(m, v, "blocked by");
        }

        /// <summary>Enemy <paramref name="v"/> sent after it (its pointer-table entry on the pockle's position), unless confused or already.</summary>
        private static void Provoke(long m, int v, string why)
        {
            if (_provoked[v] || !Enemies.IsLive(v) || IsConfused(v)) return;
            if (!AggroTable.PointConfused(v, (uint)(EnemyBody.Body.Ally(m).Pos - Memory.Pcsx2Base))) return;
            _provoked[v] = true;
            Log($"enemy slot {v} provoked ({why} the pockle): after it");
        }

        /// <summary>An enemy its hit took HP from goes after it.</summary>
        /// <summary>Each planted hit's outcome within <see cref="ProvokeWindowSeconds"/>: the HP it took, logged (or that it took none);
        /// an enemy whose HP it lowered is provoked.</summary>
        private static void Landings(long m)
        {
            for (int k = _landing.Count - 1; k >= 0; k--)
            {
                var (v, hp, until) = _landing[k];
                bool ally = v < 0;
                if (!ally && !Enemies.IsLive(v)) { _landing.RemoveAt(k); continue; }
                int now = ally ? Memory.ReadInt(m + Unit + UnitHp) : Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(v, EnemySlotOffsets.Hp));
                string who = ally ? "the pockle" : $"enemy slot {v}";
                if (now < hp)
                {
                    _landing.RemoveAt(k);
                    Log($"  → {who} lost {hp - now} HP");
                    if (!ally && !_provoked[v]) Provoke(m, v, "hit by");
                }
                else if (GameClock.Now >= until) { _landing.RemoveAt(k); Log($"  → {who} lost no HP"); }
            }
        }


        /// <summary>Every live flying shot, swept along its path since the last tick: its own (a slot-0 shot first seen nearer it than
        /// floor slot 0) against the live enemies, an enemy's against its body. A hit is planted and the shot taken through the engine's
        /// own contact where its path met the body.</summary>
        private static void Shots(long m, long pool)
        {
            var me = EnemyBody.Body.Ally(m);
            foreach (var shot in _shots.Live(_ => true))
            {
                if (shot.First)
                {
                    if (shot.Owner == 0 && Its(m, shot.Now.x, shot.Now.y)) _ownShots.Add(shot.Key); else _ownShots.Remove(shot.Key);
                }
                float reach = shot.Reach;
                int damage = shot.Damage;
                (float x, float h, float y) at = shot.Now;
                if (_ownShots.Contains(shot.Key))
                {
                    int victim = -1;
                    for (int v = 0; v < Slots && victim < 0; v++)
                        if (Enemies.IsLive(v) && EnemyBody.SegmentEdge(EnemyBody.Body.Of(v), shot.From, shot.Now, out at) <= reach) victim = v;
                    if (victim < 0) continue;
                    if (!HitEnemy(pool, victim, damage, shot.Reaction, null)) return;
                    Log($"its shot hits enemy slot {victim} for {damage} before defence");
                }
                else
                {
                    if (EnemyBody.SegmentEdge(me, shot.From, shot.Now, out at) > reach) continue;
                    uint flags = shot.CfgFlags;
                    uint elem = flags & EnemyStatus.ShotElementMask;
                    if (!PlantOnIt(m, pool, damage, shot.Reaction, elem != 0 && (flags & 0xFF00) == 0 ? elem : 0u, null)) return;
                    Log($"enemy slot {shot.Owner}'s shot hits the pockle for {damage} before defence");
                }
                Memory.WriteVec3(shot.Obj + ShotEffectPack.ObjPos, at.x, at.h, at.y);   // the burst where the path met the body
                ShotContact(shot.Fx, shot.Cfg, shot.Sub, shot.Obj);
                _shots.Forget(shot.Key); _ownShots.Remove(shot.Key);
            }
        }

        /// <summary>A hit planted on its body, nobody's (<see cref="NobodysHit"/>), with the ally-only mask: the reaction, the element,
        /// and (a swing's) kick words.</summary>
        private static bool PlantOnIt(long m, long pool, int damage, int reaction, uint element, byte[] kickFrom)
        {
            byte[] hit = NobodysHit(EnemyBody.Body.Ally(m), damage);
            void I(int o, int val) => BitConverter.GetBytes(val).CopyTo(hit, o);
            I(CollisionPool.Mask, AllyHitMask);                                                       // its CheckDmg alone takes it (ally_hooks.s mask)
            I(0x4C, reaction); I(CollisionPool.Element, (int)element);
            if (kickFrom != null) AttackScan.CopyKick(kickFrom, hit);
            int hp = Memory.ReadInt(m + Unit + UnitHp);
            if (PlantShell(pool, hit) < 0) return false;
            _landing.Add((-1, hp, GameClock.Now.AddSeconds(ProvokeWindowSeconds)));
            return true;
        }

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
