using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What Toan's lock-on gains from the Sun Sword line's later blades — Big Bang and the Sword of Zeus: the
    /// REACH (he locks on from <see cref="ReachFactor"/>× as far). The SPEED half (<see cref="SpeedRate"/>× while locked
    /// on) is kept for a blade that wants it; none drives it now. Both are the game's own data, held while the blade
    /// is out and put back when it goes; each blade's tick drives them and its exit releases them.</summary>
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
        // attack stances (right / left / forward / back) — held through the motion-speed override (−1 = the KEY's
        // own rate), which the game writes back to −1 on every motion change, so it is re-asserted each tick. The
        // guard walk (33) is NOT played faster: its stride is scaled instead (StrideExtra, below) so the animation
        // keeps its own rate. Toan's held lock is PlayerAction.LockHeld (LockOnActive reads 0 for him).
        private const float SpeedRate = 1.3f;
        private static readonly int[] LockOnMoves = { 19, 20, 21, 22 };
        // The guard walk's STRIDE while locked on: CodeCaves.StrideScale, read every frame by the stride cave at the
        // key handler's move-vector build (dun 0x1DB0F68) and applied only while motion 33 plays — the engine does
        // the per-frame work; this only says how much, when the lock comes and goes.
        private const float StrideExtra = SpeedRate - 1f;
        // ⚠ BISECT (temporary): the stride is never armed — the cave stays in the ISO reading 0, i.e. vanilla — to test
        // whether the game resets seen with the Sword of Zeus follow it. Set true to restore the stride.
        private const bool  StrideEnabled = false;
        private static bool _strideHeld;
        internal static void DriveStride(bool active, string tag)
        {
            bool want = StrideEnabled && active && PlayerAction.LockHeld(out _);
            if (want == _strideHeld) return;
            _strideHeld = want;
            Memory.WriteFloat(CodeCaves.StrideScale, want ? StrideExtra : 0f);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + (want ? $"guard-walk stride ×{SpeedRate}" : "guard-walk stride vanilla"));
        }
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
