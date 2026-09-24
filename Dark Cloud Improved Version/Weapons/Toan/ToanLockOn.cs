using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What Toan's lock-on gains from the Sun Sword line's later blades — Big Bang and the Sword of Zeus: the
    /// REACH (he locks on from <see cref="ReachFactor"/>× as far) and, with the Sword of Zeus, the SPEED (he moves at
    /// <see cref="SpeedRate"/>× while locked on). Both are the game's own data, held while the blade is out and put
    /// back when it goes; each blade's tick drives them and its exit releases them.</summary>
    internal static class ToanLockOn
    {
        // ── reach ─────────────────────────────────────────────────────────────────────────
        // Toan's entry in the lock-on factor table (CodeCaves.LockOnFactorTable, the same data the Flamingo drives for
        // Xiao): SetNearLockOnTarget and setTargetCursor multiply every enemy's own lock-on distance by it.
        private const float ReachFactor = 2.0f;
        private static readonly long  ReachEntry = CodeCaves.LockOnFactorTable + Player.ToanId * 4;
        private static readonly float Reach      = CodeCaves.LockOnFactorVanilla[Player.ToanId] * ReachFactor;
        private static bool _reachHeld;

        internal static void HoldReach(string tag)
        {
            if ((uint)Memory.ReadInt(DunPatches.LockOnTableHookAddrMmu) != DunPatches.LockOnTableWord0) return;   // table patch not in this ISO
            if (Memory.ReadFloat(ReachEntry) == Reach) return;
            Memory.WriteInt(CodeCaves.LockOnFactorTable + CodeCaves.LockOnFactorOwner, 1);   // ours: the PNACH stops re-seeding
            Memory.WriteFloat(ReachEntry, Reach);
            if (!_reachHeld) { _reachHeld = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + $"lock-on reach ×{ReachFactor:F1}"); }
        }
        internal static void ReleaseReach()
        {
            if (!_reachHeld) return;
            _reachHeld = false;
            if (Memory.ReadFloat(ReachEntry) == Reach) Memory.WriteFloat(ReachEntry, CodeCaves.LockOnFactorVanilla[Player.ToanId]);
        }

        // ── speed ─────────────────────────────────────────────────────────────────────────
        // Dragon's Y's buff, Toan's way (DragonsY.LockOnSpeedDrive has the mechanism): the dungeon walk is root motion,
        // so his ground speed while locked on is the play rate of the clip he strafes with — c01d KEYs 19-22, the
        // attack stances (right / left / forward / back), and the guard 8-10 with its walk 34 — held through the
        // motion-speed override (−1 = the KEY's own rate), which the game writes back to −1 on every motion change,
        // so it is re-asserted each tick. Toan's held lock is PlayerAction.LockHeld (LockOnActive reads 0 for him).
        private const float SpeedRate = 1.3f;
        private static readonly int[] LockOnMoves = { 19, 20, 21, 22, 8, 9, 10, 34 };
        private static bool _speedHeld;

        internal static void DriveSpeed(bool active, string tag)
        {
            if (!active) { ReleaseSpeed(tag); return; }
            bool want = PlayerAction.LockHeld(out _)
                        && Array.IndexOf(LockOnMoves, Memory.ReadInt(CCharacter.Base + CCharacter.MotionId)) >= 0;
            if (want)
            {
                if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) != SpeedRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, SpeedRate);
                if (!_speedHeld) { _speedHeld = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + $"locked on — moving at ×{SpeedRate}"); }
            }
            else ReleaseSpeed(tag);
        }
        internal static void ReleaseSpeed(string tag)
        {
            if (!_speedHeld) return;
            _speedHeld = false;
            if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) == SpeedRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + "lock-on speed released");
        }
    }
}
