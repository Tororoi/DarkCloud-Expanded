using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The target darkened under the drop — its own lighting, not the scene's: CCharacter DimOn set and DimFloor at 1 as the
    /// drop starts (<see cref="DimStart"/>; the fall-drive cave then lowers the floor with the fall, and the step eases DimFactor
    /// toward it 0.08 a frame), and its light back where the fall ends or the drop is taken down (<see cref="DimEnd"/>). One slot at a
    /// time. One of the <see cref="TerraSword"/> classes, which share their members through using static.</summary>
    internal static class TargetDimming
    {
        private static volatile int _dimSlot = -1;                   // the enemy darkened (−1 = none)

        internal static void DimStart(int slot)
        {
            DimEnd();
            if (slot < 0 || !Enemies.IsLive(slot)) return;
            _dimSlot = slot;
            long c = EnemyAddresses.CharObjects.CharAddr(slot);
            Memory.WriteFloat(c + CCharacter.DimFloor, 1f);
            Memory.WriteInt  (c + CCharacter.DimOn, 1);
        }

        /// <summary>Its own lighting back: dimming off (the step eases DimFactor back to 1.0), the floor at 1.</summary>
        internal static void DimEnd()
        {
            int slot = _dimSlot;
            if (slot < 0) return;
            _dimSlot = -1;
            long c = EnemyAddresses.CharObjects.CharAddr(slot);
            Memory.WriteInt  (c + CCharacter.DimOn, 0);
            Memory.WriteFloat(c + CCharacter.DimFloor, 1f);
        }
    }
}
