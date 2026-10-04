using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Tall Hammer — its curse shrinks the enemies it hits: every freshly hit enemy loses <see cref="TallHammerShrinkStep"/>
    /// of its scale on each axis per hit, down to <see cref="TallHammerMinScale"/> of its size. The driver (<see cref="TallHammerDrive"/>)
    /// is character-agnostic apart from whose hits count, so Goro's own weapon and Super Steve's inherited copy both reuse it.</summary>
    internal static class TallHammer
    {
        private const float TallHammerShrinkStep = 0.1f;   // scale lost per hit (matches the old net -0.1)
        private const float TallHammerMinScale   = 0.3f;   // don't shrink past 30% of original

        /// <summary>Per-tick enemy-HP snapshot for detecting fresh hits across driver calls.</summary>
        internal sealed class TallHammerState { public int[] PrevHp; }

        /// <summary>Compares the enemy-HP snapshot to last tick's (<paramref name="st"/>) and, when the damage came from
        /// <paramref name="wielderId"/>, shrinks each freshly-hit enemy by one step (clamped to <see cref="TallHammerMinScale"/>).</summary>
        internal static void TallHammerDrive(bool active, int wielderId, TallHammerState st)
        {
            int[] cur = EnemyQueries.GetEnemiesHp();
            if (st.PrevHp != null && active && EnemyQueries.GetDamageSourceCharacterID() == wielderId)
            {
                foreach (int id in EnemyQueries.GetEnemiesHitIds(st.PrevHp, cur))
                    ShrinkEnemy(id);
            }
            st.PrevHp = cur;
        }

        /// <summary>Goro's own Tall Hammer: the driver every 50 ms while it is equipped on a floor.</summary>
        public static void TallHammerEffect()
        {
            var st = new TallHammerState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.tallhammer && Player.InDungeonFloor())
            {
                TallHammerDrive(true, Player.GoroId, st);
                Thread.Sleep(50);
            }
        }

        /// <summary>Shrink one enemy's X/Y/Z scale by <see cref="TallHammerShrinkStep"/>, but only while at
        /// least one axis is still within [<see cref="TallHammerMinScale"/>, 1] of its original size.</summary>
        private static void ShrinkEnemy(int id)
        {
            int off = MiniBoss.scaleOffset * id;
            float w = Memory.ReadFloat(MiniBoss.enemyZeroWidth  + off);
            float h = Memory.ReadFloat(MiniBoss.enemyZeroHeight + off);
            float d = Memory.ReadFloat(MiniBoss.enemyZeroDepth  + off);
            if ((w >= TallHammerMinScale && w <= 1f) ||
                (h >= TallHammerMinScale && h <= 1f) ||
                (d >= TallHammerMinScale && d <= 1f))
            {
                Memory.WriteFloat(MiniBoss.enemyZeroWidth  + off, w - TallHammerShrinkStep);
                Memory.WriteFloat(MiniBoss.enemyZeroHeight + off, h - TallHammerShrinkStep);
                Memory.WriteFloat(MiniBoss.enemyZeroDepth  + off, d - TallHammerShrinkStep);
            }
        }
    }
}
