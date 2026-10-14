using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The monsters' attacks in flight as the friendly fire reads them (<see cref="ConfusionFriendlyFire"/>, <see cref="ConfusionAlly"/>;
    /// nothing native tests a monster against monsters): the collision pool's open entries (<see cref="OpenEntries"/>, read whole) and the
    /// fields the friendly fire takes from one; the shot pack's flying sub-shots are <see cref="ShotSweep"/>'s.</summary>
    internal static class AttackScan
    {
        /// <summary>Each active pool entry open this frame (its gates equal), read in ONE batch: entries are re-made every frame, so the
        /// owner, the mask and the sphere must come from the same read.</summary>
        internal static IEnumerable<(int idx, byte[] e)> OpenEntries(long pool)
        {
            byte[] active = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (active == null) yield break;
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (BitConverter.ToInt32(active, i * 4) == 0) continue;
                byte[] e = Memory.ReadBytesBatch(pool + i * CollisionPool.Stride, CollisionPool.Stride);
                if (e == null || BitConverter.ToInt32(e, CollisionPool.GateA) != BitConverter.ToInt32(e, CollisionPool.GateB)) continue;
                yield return (i, e);
            }
        }

        /// <summary>The floor slot whose attack sphere <paramref name="e"/> is (owner slot × 5 + 200, mask bit 0 — the summoned ally's carry
        /// mask 0, tools/stubs/ally_hooks.s), else −1.</summary>
        internal static int EnemyOwner(byte[] e)
        {
            int owner = BitConverter.ToInt32(e, CollisionPool.Owner);
            if (owner < 200 || (owner - 200) % 5 != 0 || (BitConverter.ToInt32(e, CollisionPool.Mask) & (int)CollisionPool.HurtsPlayerMask) == 0) return -1;
            int slot = (owner - 200) / 5;
            return slot < EnemyAddresses.FloorSlots.Count ? slot : -1;
        }

        internal static (float x, float h, float y) Centre(byte[] e) => (BitConverter.ToSingle(e, 0x00), BitConverter.ToSingle(e, 0x04), BitConverter.ToSingle(e, 0x08));
        internal static float Radius(byte[] e) => BitConverter.ToSingle(e, CollisionPool.Radius);
        internal static int Damage(byte[] e) => BitConverter.ToInt32(e, 0x34);
        internal static int Reaction(byte[] e) => BitConverter.ToInt32(e, 0x4C);
        internal static int Flags(byte[] e) => BitConverter.ToInt32(e, 0x50);

        /// <summary>The kick words (+0x80..+0x98) of attack <paramref name="from"/> into hit <paramref name="to"/>, as they stand.</summary>
        internal static void CopyKick(byte[] from, byte[] to) => Array.Copy(from, CollisionPool.KickOriginOff, to, CollisionPool.KickOriginOff, CollisionPool.KickWordsSize);
    }

    /// <summary>The shot pack's live flying sub-shots fired by floor slots (OffA060 0..15, stamped by SetUserID2; phase ≤ 1), each with
    /// the position it held at the user's last tick — the path it moved since, swept against the bodies. One per user: each keeps its own
    /// last positions, on its own tick.</summary>
    internal sealed class ShotSweep
    {
        /// <summary>One sub-shot: its pack slot (<paramref name="Fx"/>), config, index, key (slot × SubShots + index), firing slot, object,
        /// the path <paramref name="From"/> → <paramref name="Now"/>, and whether this is the first tick it is seen.</summary>
        internal readonly record struct Shot(long Fx, long Cfg, int Sub, int Key, int Owner, long Obj, (float x, float h, float y) From, (float x, float h, float y) Now, bool First)
        {
            internal float Reach => Math.Max(1f, Memory.ReadFloat(Cfg + ShotEffectPack.CfgRadiusFlying));   // its flying radius
            internal int Damage => Math.Max(1, Memory.ReadInt(Fx + ShotEffectPack.OffDamage + Sub * 4));
            internal int Reaction => Memory.ReadInt(Cfg + ShotEffectPack.CfgReaction);
            internal uint CfgFlags => Memory.ReadUInt(Cfg + ShotEffectPack.CfgFlags);
        }

        private readonly Dictionary<int, (float x, float h, float y)> _last = new();

        /// <summary>Each live sub-shot whose firing slot <paramref name="wanted"/> accepts; every other key forgotten.</summary>
        internal IEnumerable<Shot> Live(Func<int, bool> wanted)
        {
            uint packG = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(packG)) yield break;
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
                    if (!live || owner < 0 || owner >= EnemyAddresses.FloorSlots.Count || !wanted(owner)) { _last.Remove(key); continue; }
                    long obj = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride, o = obj + ShotEffectPack.ObjPos;
                    var now = (Memory.ReadFloat(o), Memory.ReadFloat(o + 4), Memory.ReadFloat(o + 8));
                    bool first = !_last.TryGetValue(key, out var from);
                    if (first) from = now;
                    _last[key] = now;
                    yield return new Shot(fx, cfg, i, key, owner, obj, from, now, first);
                }
            }
        }

        /// <summary>A shot taken through its contact: tracked afresh should its sub-shot fly again.</summary>
        internal void Forget(int key) => _last.Remove(key);
        internal void Clear() => _last.Clear();
    }
}
