using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMP experiment: fix sped-up Skeleton attacks whiffing by making the HIT-WINDOW frames DWELL at natural speed
    /// while the rest of the swing stays sped up. Diagnosis (from the pause-makes-it-hit clue): the player-side damage
    /// check reads the collision one step behind CheckDmg building it, so when the motion frame moves fast the checked
    /// sphere is always stale and never coincides with contact; pausing freezes the frame so the lagged check lands.
    /// Native attack speed makes the contact dwell long enough to register. So: while the enemy's PlayingMotionFrame is
    /// inside an attack hit window, overwrite PlayingMotionSpeed with the clip's NATURAL rate (un-doing the difficulty
    /// scaler for those few frames only). Runs AFTER HarderEnemies in the tick, so this write wins for those frames.
    /// Skeleton Soldier: attack1 = motion 13, hit 62–64, natural 0.32 · attack2 = motion 16, hit 83–86, natural 0.30.
    /// </summary>
    internal static class HitWindowTest
    {
        internal static bool Enabled = true;

        internal static void Tick()
        {
            if (!Enabled) return;
            if (!Player.InDungeonFloor()) return;

            int targetId = Enemies.SkeletonSoldier.Id;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (EnemySlots.GetFloorEnemyId(s) != targetId) continue;

                long mb = (long)ModelScaleOffsets.ModelBase + (long)s * ModelScaleOffsets.ModelStride;
                int   motion = Memory.ReadInt  (mb + ModelScaleOffsets.PlayingMotionId);
                float frame  = Memory.ReadFloat(mb + ModelScaleOffsets.PlayingMotionFrame);

                float natural = 0f;
                if      (motion == 13 && frame >= 62f && frame <= 64f) natural = 0.32f;   // attack1 hit window
                else if (motion == 16 && frame >= 83f && frame <= 86f) natural = 0.30f;   // attack2 hit window

                if (natural > 0f)
                    Memory.WriteFloat(mb + ModelScaleOffsets.PlayingMotionSpeed, natural);
            }
        }
    }
}
