using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The controller's vibration, as CGamePad keeps it (instance 0x21CBC540, SetVibration 0x12B940, stepped by
    /// CGamePad::Step 0x12B140). Pure data: the step sends whatever these hold to scePadSetActDirect every frame and
    /// counts the timers down, turning a motor off at zero — so writing them IS a rumble, no call needed.
    /// The engine's own hits use motor 1 at 0xE6 for 22 frames (a knockdown) and 0xDC for 12 (a lighter hit).
    /// </summary>
    internal static class GamePad
    {
        internal const long Base       = 0x21CBC540;
        internal const int  MotorSmall = 0x2C;   // byte, on/off
        internal const int  MotorLarge = 0x2D;   // byte, strength 0-255
        internal const int  TimerSmall = 0x38;   // int, frames left
        internal const int  TimerLarge = 0x3C;
        internal const int  Enabled    = 0x464;  // the player's own vibration option — 0 = off, and SetVibration obeys it

        /// <summary>A shove of rumble on the big motor, if the player has vibration on.</summary>
        internal static void Rumble(int strength, int frames)
        {
            if (Memory.ReadInt(Base + Enabled) == 0) return;
            Memory.WriteInt(Base + TimerLarge, frames);
            Memory.WriteByte(Base + MotorLarge, (byte)Math.Min(255, Math.Max(0, strength)));
        }
    }
}
