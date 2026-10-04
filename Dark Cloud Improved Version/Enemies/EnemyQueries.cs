using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Snapshot reads over the floor's enemy slots (<see cref="EnemyAddresses.FloorSlots"/>) and the engine's
    /// "last damage dealt" words, for the weapon effects that poll HP to see whom the player just hit.</summary>
    internal static class EnemyQueries
    {
        /// <summary>Every slot's HP, indexed by slot.</summary>
        internal static int[] GetEnemiesHp()
        {
            int[] enemiesHp = new int[EnemyAddresses.FloorSlots.Count];
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                enemiesHp[i] = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp));
            return enemiesHp;
        }

        /// <summary>Every slot's distance to the player, indexed by slot.</summary>
        internal static float[] GetEnemiesDistance()
        {
            float[] distance = new float[EnemyAddresses.FloorSlots.Count];
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                distance[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.DistanceToPlayer));
            return distance;
        }

        /// <summary>The slots whose HP fell between two <see cref="GetEnemiesHp"/> snapshots.</summary>
        internal static List<int> GetEnemiesHitIds(int[] formerEnemiesHp, int[] currentEnemiesHp)
        {
            List<int> enemyIds = new List<int>();
            for (int i = 0; i < formerEnemiesHp.Length; i++)
                if (currentEnemiesHp[i] < formerEnemiesHp[i]) enemyIds.Add(i);
            return enemyIds;
        }

        /// <summary>The hit slots (<see cref="GetEnemiesHitIds"/>) whose HP now reads 0.</summary>
        internal static List<int> GetEnemiesKilledIds(int[] formerEnemiesHp, int[] currentEnemiesHp)
        {
            List<int> enemyKilled = new List<int>();
            foreach (int enemy in GetEnemiesHitIds(formerEnemiesHp, currentEnemiesHp))
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(enemy, EnemySlotOffsets.Hp)) == 0)
                    enemyKilled.Add(enemy);
            return enemyKilled;
        }

        /// <summary>True when all 15 slots are at 0 HP and no longer rendered.</summary>
        internal static bool CheckIfAllEnemiesKilled()
        {
            int count = 0;
            for (int i = 0; i < 15; i++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) == 0 &&
                    Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) == 255)
                    count++;
            }
            return count == 15;
        }

        /// <summary>The last damage value the player dealt (<see cref="PlayerAddresses.MostRecentDamage"/>).</summary>
        internal static int GetRecentDamageDealtByPlayer() => Memory.ReadInt(PlayerAddresses.MostRecentDamage);

        /// <summary>The source of the last damage: the character id when it was a weapon, -1 for a throwable.</summary>
        internal static int GetDamageSourceCharacterID() => Memory.ReadInt(PlayerAddresses.DamageSource);

        /// <summary>Resets both last-damage words to -1 so the next hit is seen once.</summary>
        internal static void ClearRecentDamageAndDamageSource()
        {
            Memory.WriteInt(PlayerAddresses.MostRecentDamage, -1);
            Memory.WriteInt(PlayerAddresses.DamageSource, -1);
        }
    }
}
