namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The ACTIVE player character's <c>CCharacter</c> object and its motion-control fields (RE'd from
    /// SCUS_971.11: <c>motionDrive__Fv</c> dun 0x1DB7450, <c>Step__10CCharacterFv</c> main 0x138530,
    /// <c>MotionProc__FP6CFrameP12MOTION_STATEP8Mot_List</c> main 0x147D20). See docs/character-motion-table.md.
    ///
    /// The controlled character (Toan/Xiao/Goro/Ruby/Ungaga/Osmond — only one is active at a time) lives at a
    /// FIXED global: <see cref="Base"/> = native 0x1EA1D20. It is the same object <c>WeaponCollision.
    /// EquippedModelPtr</c> (0x21EA1DDC) points into: EquippedModelPtr == Base + <see cref="ModelRootOffset"/>.
    /// </summary>
    internal static class CharacterMotion
    {
        internal const long NativeBase = 0x001EA1D20;
        internal const long Base       = 0x21EA1D20; // MMU (native | 0x20000000) — active-character CCharacter

        // ── Field offsets within the CCharacter object ──
        internal const int ModelRootOffset   = 0xBC;  // → native ptr to the model root CFrame (== EquippedModelPtr)
        internal const int MotionSpeedOffset  = 0xC60; // float — motion play-rate override (see below)
        internal const int MotionFlagsOffset  = 0xC64; // uint  — motion flags: bit0x1=stop, 0x2=?, 0x4=restart
        internal const int MotionIdOffset     = 0xC68; // int   — current motion id (-1 = none); set by motionDrive
        internal const int MotionStatusOffset = 0xC70; // int   — motion-phase status out (0/1/2/3)

        // ── Absolute MMU addresses (Base + offset) ──
        // ★ MOTION PLAY-RATE OVERRIDE — the animation-speed knob. Default -1.0. Step__CCharacter fetches the
        // current motion track's baked KEY step, then: `if (0.0 < *MotionSpeedOverride) trackStep = *MotionSpeedOverride`
        // — i.e. a POSITIVE value REPLACES the per-motion step (an ABSOLUTE rate, NOT a multiplier); -1.0 = use the
        // motion's own KEY speed. Drives WHATEVER motion is playing (attacks AND movement). motionDrive rewrites
        // it to -1.0 only on a motion CHANGE (not every frame — the player has no STB script), so a re-apply loop
        // can hold a value between changes. Directly PINE-writable (data write, no code patch).
        internal const long MotionSpeedOverride = Base + MotionSpeedOffset; // 0x21EA2980
        internal const long MotionFlags         = Base + MotionFlagsOffset; // 0x21EA2984
        internal const long MotionId            = Base + MotionIdOffset;    // 0x21EA2988
        internal const long MotionStatus        = Base + MotionStatusOffset;// 0x21EA2990

        internal const float MotionSpeedUseKey = -1.0f; // sentinel = "use the motion's own KEY speed" (default)
    }
}
