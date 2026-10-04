using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Small Sword, Tsukikage or Heaven's Cloud sphere — the Small Sword's "Quick Draw" from Xiao's
    /// slingshot. Hold X to nock and aim as normal; the shot fires the instant X is released (the draw/hold state jumps straight
    /// to the shoot motion at its pellet-release frame), and Super Steve's BATTLE-copy speed is written past the 99 cap so the
    /// between-shots gauge refills near-instantly. Menu/inventory speed is untouched. Driven from Super Steve's sphere dispatch.</summary>
    internal static class SmallSwordSphere
    {
        private const string Tag = "[SmallSwordSphere] ";
        private const float ShotFireFrame = 251.0f;  // inside the (251,252) pellet-release window (shoot motion idx 13)
        private static bool _releaseArmed;           // edge latch so one X-release = one instant shot
        private const ushort BattleSpeed = 350;      // effective Speed in Super Steve's BATTLE copy (past 99) → keeps the rate-of-fire gauge from being the bottleneck

        /// <summary>
        /// Quick Draw inheritance for Xiao, driven each tick from <see cref="SuperSteve.SphereInheritanceEffect"/>.
        /// Xiao (controlled) shares Toan's shot plumbing — motion-frame cursor = AnimFrameCursor (0x21EA2010),
        /// shot action-state = ChargeActionState (0x21DC4494). Her shot is three c04b motions: draw (0xB, frames
        /// 240→251), a zero-speed "nocked" HOLD parked on 250 (0xC) that lasts until the fire input releases,
        /// then shoot (0xD) — the pellet leaves at frame 251.
        ///
        /// Vanilla feel is preserved: hold X to nock/aim, the shot fires on RELEASE. Quick Draw makes that
        /// release instant. Two levers:
        ///   1. Rate-of-fire: the between-shots gauge (0x21DC44C8) fills at effectiveSpeed/30 and gates the next
        ///      shot at 100; we write Super Steve's BATTLE-copy speed (+8) past the game's 99 cap so it refills
        ///      near-instantly. Menu/inventory speed is untouched (still reads 99).
        ///   2. Instant release: the moment the engine's release flag (0x21DC4498, the real X-release — never
        ///      forced) is set during the draw/hold, we jump the state straight to shoot (0xD) and drop the
        ///      cursor onto the shoot-motion start (251.0) so the pellet crosses the (251,252) fire window
        ///      exactly once. Edge-latched (<see cref="_releaseArmed"/>) so one release = one pellet. (251.5
        ///      sat inside the window and double-fired; 251.0 = the clean boundary.)
        /// The draw motion plays at a fixed KEY rate that speed never scales — only the gauge does (motionDrive,
        /// dun 0x1DB7450) — which is why the fix is gauge-speed + release-jump, not a motion-rate override.
        /// </summary>
        internal static void Drive(bool active)
        {
            // Only touch the battle-speed when the BATTLE weapon really is Super Steve (it can lag the equipped
            // record during swaps). Writing past the 99 cap (BATTLE copy only) removes the between-shots wait.
            bool boosted = active && Memory.ReadUShort(WeaponHave.BattleWeaponRecord) == Items.supersteve;
            long battleSpeedAddr = WeaponHave.BattleWeaponRecord + WeaponHave.EffSpeedOffset;
            if (boosted && Memory.ReadUShort(battleSpeedAddr) != BattleSpeed)
                Memory.WriteUShort(battleSpeedAddr, BattleSpeed);

            // Instant shot on X RELEASE: while drawing (0xB) or holding (0xC), the moment the fire input is
            // released (engine sets XiaoShotReleaseFlag), jump to the shoot state (0xD) and drop the cursor into
            // the fire window (251). Holding still aims; a tap fires almost instantly. Edge-triggered.
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool inDrawOrHold = shotState == PlayerAction.XiaoShotDraw ||
                                shotState == PlayerAction.XiaoShotHold;
            bool released = inDrawOrHold && Memory.ReadInt(PlayerAction.XiaoShotReleaseFlag) != 0;
            if (boosted && released && !_releaseArmed)
            {
                Memory.WriteInt(PlayerAction.XiaoShotReleaseFlag, 1);
                Memory.WriteInt(PlayerAction.ChargeActionState, PlayerAction.XiaoShotShoot);
                Memory.WriteFloat(PlayerAction.AnimFrameCursor, ShotFireFrame);
            }
            _releaseArmed = released;
        }
    }
}
