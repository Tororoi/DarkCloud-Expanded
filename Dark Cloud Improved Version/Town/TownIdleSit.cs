using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Idle→sit for the swapped-in town cat (Xiao). The town mover (<c>EdMoveChara</c>) rewrites the character's
    /// motion id (<c>+0xc68</c>) to 0 (idle) every frame while standing, so a plain mod-side motion write would be
    /// clobbered the next frame. Instead <see cref="ElfPatches"/>' idle-motion-override cave hooks that store:
    /// when the value about to be written is 0 (idle) AND the mailbox <see cref="IdleMotionMailbox"/> is nonzero,
    /// it stores the mailbox value (a motion index) instead. That makes the sit come from EdMoveChara itself — no
    /// flicker — and any run/walk/fall/land value (1/2/8/9) passes through unchanged, so movement breaks the sit
    /// instantly at the engine level.
    ///
    /// This ticker is only the POLICY layer: while Xiao is the loaded town character and has stood idle past a
    /// threshold, it arms the mailbox with the sit index (5 = item/sit "sit-down", then 6 = the settled hold loop),
    /// and it disarms (mailbox → 0) the moment the character moves or the town/char context goes away. For Toan
    /// and every other ally the mailbox stays 0, so the cave is a no-op for them.
    /// </summary>
    internal static class TownIdleSit
    {
        internal static bool Enabled = true;

        private const string Tag = "[IdleSit] ";

        // ElfPatches.PatchIdleMotionOverride mailbox (EE 0x21F10070): write the sit motion index here to make
        // EdMoveChara's grounded locomotion store play it in place of idle; 0 = no override (vanilla idle).
        private const long IdleMotionMailbox = CodeCaves.Mailbox.IdleMotionOverride;

        private const int MotionId = 0xc68;   // CCharacter motion-id field the town mover writes each frame

        // Xiao (menu cursor 1): town idx 5/6 both grafted to s86/c04cat's looping sit (frames 30-40). Arm idx 6
        // (the item-LOOP slot) — the town engine loops the armed KEY window, so the full sit cycles cleanly.
        private const int XiaoAlly      = 1;
        private const int SitIndex      = 6;    // looping sit (also idx 5 = same clip; both flag a real item-get)
        private const int IdleThreshTicks = 30; // ~1.5s at the 20Hz town tick standing still before the cat sits

        private static int  _idleTicks;
        private static bool _armed;   // mailbox currently armed (cat sitting)

        internal static void Tick()
        {
            if (!Enabled) return;
            try { TickCore(); }
            catch (Exception e)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
            }
        }

        private static void TickCore()
        {
            // Only while walking a town as Xiao; anything else must leave the override disarmed.
            if (Memory.ReadByte(Addresses.mode) != 2 || AllySwapPrototype.CurrentAlly != XiaoAlly) { Disarm(); return; }
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking) { Disarm(); return; }

            uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) { Disarm(); return; }
            int m = Memory.ReadInt(Memory.ToMmu(chara) + MotionId);

            if (m == 1 || m == 2 || m == 8 || m == 9) { Disarm(); return; }   // run/walk/fall/land → not idle

            if (!_armed)
            {
                // m==5/6 here (mailbox still 0) is a real item-get(sit), not idle — don't count it toward the timer.
                if (m == SitIndex || m == 5) { _idleTicks = 0; return; }
                if (++_idleTicks >= IdleThreshTicks)
                {
                    _armed = true;
                    Memory.WriteInt(IdleMotionMailbox, SitIndex);   // cave loops this while she stays idle
                }
            }
            // armed → keep looping the sit; movement / context-loss disarms via the guards above.
        }

        private static void Disarm()
        {
            _idleTicks = 0;
            if (_armed) { _armed = false; Memory.WriteInt(IdleMotionMailbox, 0); }
        }
    }
}
