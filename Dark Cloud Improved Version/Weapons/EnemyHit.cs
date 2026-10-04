using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>One player-attack sphere planted ON AN ENEMY'S BODY (<see cref="CollisionPool"/>: the entries CheckDmg tests the
    /// player's swings against) — what Babel's spear, the Cactus and the Terra Sword's drops land their hits with. The sphere sits on
    /// the enemy's largest posed body sphere (<see cref="EnemyBody.BodyCentre"/>), at least <c>minRadius</c> wide, so it can be consumed
    /// by that enemy alone; <see cref="PoolReserve"/> free entries are always left to the engine.</summary>
    internal static class EnemyHit
    {
        internal const int PoolReserve = 16;   // free pool entries a plant never takes from the engine

        /// <summary>A hit's kick: the victim is thrown away from (X, H, Y) at Strength, fading by Decay, with reaction Type
        /// (<see cref="CollisionPool.KickTypeAway"/>). Null on a plant = no kick words at all: no reaction.</summary>
        internal readonly record struct Kick(float X, float H, float Y, float Strength, float Decay, int Type = CollisionPool.KickTypeAway);

        /// <summary>Where a hit was planted and how wide.</summary>
        internal readonly record struct Sphere(float X, float H, float Y, float Radius);

        /// <summary>The plant: the entry's index, or −1 when the pool is unallocated, down to its reserve or full. <paramref name="baseDmg"/>
        /// is the damage before the enemy's defence; <paramref name="mark"/> (0 = none) goes in the mark word (+0x9C: CodeCaves.NoDrainMark
        /// bills no weapon HP, CodeCaves.CrushMark also passes every guard); <paramref name="weaponAbilities"/> false zeroes the entry's
        /// ability word (no poison, stop, critical, steal or drain rides on it).</summary>
        internal static int TryPlant(int enemySlot, int baseDmg, float minRadius, Kick? kick, uint mark = 0, bool weaponAbilities = true)
            => TryPlant(enemySlot, baseDmg, minRadius, kick, out _, mark, weaponAbilities);

        /// <summary><see cref="TryPlant(int, int, float, Kick?, uint, bool)"/>, with the planted sphere out for the caller's log.</summary>
        internal static int TryPlant(int enemySlot, int baseDmg, float minRadius, Kick? kick, out Sphere at, uint mark = 0, bool weaponAbilities = true)
        {
            at = default;
            long pool = CollisionPool.Resolve();
            if (pool == 0 || CollisionPool.FreeCount(pool) <= PoolReserve) return -1;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return -1;
            EnemyBody.BodyCentre(enemySlot, EnemyAddresses.FloorSlots.SlotAddr(enemySlot, 0), out float cx, out float ch, out float cy, out float cr);
            float radius = Math.Max(minRadius, cr);
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, radius, baseDmg, 0);
            if (!weaponAbilities) BitConverter.GetBytes(0).CopyTo(e, CollisionPool.AbilityFlags);
            if (kick is Kick k) CollisionPool.SetKick(e, k.X, k.H, k.Y, k.Strength, k.Decay, k.Type);
            if (mark != 0) BitConverter.GetBytes(mark).CopyTo(e, CodeCaves.NoDrainMarkOff);
            CollisionPool.Plant(pool, idx, e);
            at = new Sphere(cx, ch, cy, radius);
            return idx;
        }
    }
}
