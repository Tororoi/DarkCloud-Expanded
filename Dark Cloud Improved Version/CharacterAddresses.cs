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

    /// <summary>
    /// The engine's whole-character ambient "flash" — an animated ambient tint applied to the ACTIVE unit
    /// (RE'd from <c>setUnitAmbientAnime__Ffffff</c> dun 0x1DC1000 + <c>unitAmbientAnime__FPf</c> 0x1DC1050).
    /// Setting <see cref="Enable"/> makes the per-frame updater drive the unit's ambient colour =
    /// (colour × pulse) + 64 until the <see cref="Count"/> repeats run out. It is character-agnostic — the
    /// game uses it for drink/face-change and Ruby's Mobius charge flash — so the same data writes flash any
    /// active character. Trigger: write colour + speed + count, reset <see cref="Phase"/>, then set Enable.
    /// gp = 0x2A97F0. See <c>CustomXiaoEffects.TriggerCharacterFlash</c> / the Ruby Mobius flash.
    /// </summary>
    internal static class CharacterFlash
    {
        internal const long Speed  = 0x202A36F4; // fGpffff9f04 — pulse speed (Ruby's charge flash uses 15.0)
        internal const long Phase  = 0x202A36F8; // fGpffff9f08 — animation phase; reset to 0 to (re)start
        internal const long Count  = 0x202A36FC; // iGpffff9f0c — remaining flash repeats
        internal const long Enable = 0x202A3700; // iGpffff9f10 — 1 = on; the updater clears it when Count runs out
        internal const long ColorR = 0x21F068D0; // fRam01f068d0 — flash colour RGB (float, 0-255)
        internal const long ColorG = 0x21F068D4; // fRam01f068d4
        internal const long ColorB = 0x21F068D8; // fRam01f068d8
    }

    /// <summary>
    /// Toan's live status/HP fields (same block Player.Toan wraps with getters; exposed here as
    /// addresses for effects that need raw bit-level status access, e.g. the Evilcise/Maneater
    /// curse loops). The status word and its single shared countdown timer cover ALL status
    /// ailments at once — refreshing the timer extends every active status bit.
    /// </summary>
    internal static class ToanState
    {
        internal const int Hp          = 0x21CD955E; // ushort — current HP
        internal const int Status      = 0x21CDD814; // ushort — status bit field (see Status* bits)
        internal const int StatusTimer = 0x21CDD824; // ushort — shared countdown (frames) for all status bits

        // Status bit masks
        internal const ushort StatusNearDeath = 0x02;
        internal const ushort StatusFreeze    = 0x04;
        internal const ushort StatusStamina   = 0x08;
        internal const ushort StatusPoison    = 0x10;
        internal const ushort StatusCurse     = 0x20;
        internal const ushort StatusGoo       = 0x40;

        /// <summary>Timer value the curse effects use when (re)applying a status: 3600 frames = 60s.</summary>
        internal const ushort StatusDurationFrames = 3600;
    }
}
