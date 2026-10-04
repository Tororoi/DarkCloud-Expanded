using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.AngelGear;
using static Dark_Cloud_Improved_Version.ShotReflect;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A reflected shot's damage. The shot itself stays a latched visual (its own mask is the enemy-shot one, so the
    /// engine never collides it with monsters); OUR in-flight shots are followed (<see cref="TrackReflected"/>) and, on contact
    /// with a live enemy, the flight is ended (wait = 0: the engine plays the shot's impact motion, planting nothing because
    /// latched) and ONE pellet-style CollisionData entry is planted (<see cref="PlantReflectedHit"/>) for CMonstorUnit::CheckDmg
    /// to do defence, anti-category, resistance, No Effect, statuses and numbers. One of the <see cref="AngelGear"/> classes,
    /// which share their members through using static.</summary>
    internal static class ReflectedHits
    {
        private const string Tag = "[AngelGear/ReflectedHits] ";

        // The planted entry:
        //   +0x34 base = weapon ATTACK × (dungeon+1)/14  (0 = Divine Beast Cave … 6 = Demon Shaft = half attack;
        //                /7 read too strong for a defensive ability)
        //   +0x50 = the SHOT's element bit (pure) or its enemy-valid status bits (0x100/0x200/0x800)
        //   +0x58 = 1 (Xiao: ranged falloff + kill credit), +0x64 = her stats block, +0x6C = her ability flags
        private const long   BattleWeaponAttack  = WeaponHave.BattleWeaponRecord + 0x04;   // short (BattleActionPlay_Jinn's pellet damage)
        private const float  TierDivisor = 14f;
        private const float  HitMargin = 4f;                                              // contact slack + planted-entry reach
        private const int    PlantedLifeTicks = 2;                                       // retire an unconsumed entry
        private const int    ReflectMaxTicks = 260;                                      // give up tracking (FreshTimers + slack)

        /// <summary>One of OUR reflected shots in flight: its pack slot and sub-shot, ticks followed, the config's flag word
        /// (element and statuses) and flying radius.</summary>
        internal sealed class Fired { public int Slot, Idx, Ticks; public uint Flags; public float Radius; }
        internal static readonly List<Fired> _flying  = new();   // OUR reflected shots in flight
        internal static readonly List<(int idx, int ticks)> _planted = new();   // entries we planted, to retire

        /// <summary>Follow OUR reflected shots: keep each latched, and when one reaches a live enemy end
        /// its flight (wait = 0 → the engine's impact motion) and plant the damage entry. Also retire
        /// planted entries the engine did not consume.</summary>
        internal static void TrackReflected(long pack, float xx, float xh, float xy)
        {
            for (int q = _planted.Count - 1; q >= 0; q--)
            {
                var (idx, ticks) = _planted[q];
                if (ticks <= 0)
                {
                    long pool = Memory.ReadInt(CollisionPool.Pointer);
                    if (pool > 0) CollisionPool.Deactivate(pool + 0x20000000, idx);
                    _planted.RemoveAt(q);
                }
                else _planted[q] = (idx, ticks - 1);
            }
            if (_flying.Count == 0) return;
            for (int q = _flying.Count - 1; q >= 0; q--)
            {
                var f = _flying[q];
                long inst = pack + f.Slot * ShotEffectPack.SlotStride;
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + f.Idx * 2) == 0 || ++f.Ticks > ReflectMaxTicks) { _flying.RemoveAt(q); continue; }
                Memory.WriteByte(inst + ShotEffectPack.OffLatch + f.Idx, LatchHold);                      // never plants on its own
                long obj = inst + ShotEffectPack.OffObj + f.Idx * ShotEffectPack.ObjStride, dirA = inst + ShotEffectPack.OffDir + f.Idx * 0x10;
                float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sh = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                float speed = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy);
                for (int e = 0; e < EnemyAddresses.FloorSlots.Count; e++)
                {
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.Hp)) <= 0) continue;
                    long p = EnemyAddresses.CharObjects.PosAddr(e);
                    float ex = Memory.ReadFloat(p), eh = Memory.ReadFloat(p + 4), ey = Memory.ReadFloat(p + 8);
                    if (ex == 0f && eh == 0f && ey == 0f) continue;
                    float body = Math.Max(2f, Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.EntityScale)));
                    // judge height against the lock-on point when the species has one (chest), else origin + 8
                    float th = eh + EnemySlotOffsets.LockOnFallbackLift;
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.LockOnFrame)) != 0)
                    {
                        float lh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.LockOnPoint) + 4);
                        if (Math.Abs(lh - eh) < 200f) th = lh;
                    }
                    float reach = body + f.Radius + speed * 2f + HitMargin;
                    float dx = sx - ex, dh = sh - th, dy = sy - ey;
                    if (dx * dx + dy * dy > reach * reach) continue;
                    if (Math.Abs(dh) > body + HitMargin + 12f) continue;

                    Memory.WriteInt(inst + ShotEffectPack.OffWait + f.Idx * 4, 0);                        // flight ends next frame: impact motion, no entry (latched)
                    PlantReflectedHit(sx, sh, sy, body + f.Radius + HitMargin, f.Flags, e);
                    _flying.RemoveAt(q);
                    break;
                }
            }
        }

        /// <summary>One pellet-style CollisionData entry at the impact, fields per the RE doc §C.</summary>
        private static void PlantReflectedHit(float x, float h, float y, float radius, uint shotFlags, int enemySlot)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int slot = CollisionPool.TakeFreeSlot(pool);
            if (slot < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no free collision entry — reflected hit lost"); return; }

            int dungeon = Math.Max(0, Math.Min(6, (int)Memory.ReadByte(Addresses.checkDungeon)));
            float attack = Memory.ReadShort(BattleWeaponAttack);
            int baseDmg = Math.Max(1, (int)Math.Round(attack * (dungeon + 1) / TierDivisor));
            uint elem = shotFlags & EnemyStatus.ShotElementMask, stat = shotFlags & EnemyStatus.ShotEnemyStatusMask;
            // +0x50 must be a PURE element bit (or 0): any status bit there sends CheckDmg's element branch
            // through index 5 = MinGoldDrop as the percent (vanilla quirk — the rose's gooey shot did ~35 of
            // 156). Statuses are applied by data instead, with CheckDmg's own rules (EnemyStatus.ApplyShotStatus).
            uint attr = (elem != 0 && (shotFlags & 0xFF00) == 0) ? elem : 0u;
            string statusNote = stat != 0 ? EnemyStatus.ApplyShotStatus(enemySlot, stat) : "";

            CollisionPool.Plant(pool, slot, CollisionPool.PlayerHitEntry(x, h, y, radius, baseDmg, attr));
            _planted.Add((slot, PlantedLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"reflected hit on slot {enemySlot}: base {baseDmg} (atk {attack:F0} × {dungeon + 1}/{TierDivisor:F0}), attr 0x{attr:X} ({(attr != 0 ? "element" : "none")}){statusNote}, r={radius:F1} → entry {slot}");
        }
    }
}
