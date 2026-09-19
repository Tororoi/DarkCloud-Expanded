namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// SHARED guard detection for the weapon-ability family (roadmap Phase 1: Guardian Grace, Solar
    /// Flash, Babel Curse, Storm Flash, Guardian Reflector all key off the guard channel). One source
    /// of truth so every ability agrees on what "guarding" means.
    ///
    /// The held-guard pose oscillates between motion 9 (guard loop) and 33 (guard-while-moving) under
    /// R1 — proven by Mirage's watcher for BOTH its wielders (Ungaga and Xiao share the ids; see
    /// Mirage.cs). Callers that need an edge ("guard just started") latch on their side: level-truth
    /// lives here, edge semantics differ per ability (Mirage plants once per hold, Guardian Grace
    /// heals for the whole hold, Solar Flash charges toward a threshold).
    /// </summary>
    internal static class GuardWatch
    {
        internal const int GuardLoopMotion = 9;    // guard-hold loop
        internal const int GuardMoveMotion = 33;   // guard-while-moving (oscillates with 9 under R1)

        /// <summary>True while the ACTIVE character is holding a guard: R1 down AND the current motion
        /// is one of the guard-pose ids. Motion alone is not enough (the pose lingers a frame or two
        /// after release); the button alone is not enough (R1 during a swing is not a guard).</summary>
        internal static bool IsGuarding()
        {
            if ((Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) == 0) return false;
            int mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            return mid == GuardLoopMotion || mid == GuardMoveMotion;
        }
    }
}
