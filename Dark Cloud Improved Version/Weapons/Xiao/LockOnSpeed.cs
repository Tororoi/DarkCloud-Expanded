using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Dragon's Y's movement: while Xiao is locked on to an enemy she moves at <see cref="LockOnRate"/>× speed. The buff is
    /// Dragon's Y's, inherited by its line — Divine Beast Title, Angel Shooter and Angel Gear — and by Super Steve carrying
    /// any of the four's SynthSphere (<see cref="Grants"/>). Driven each tick by <see cref="CustomXiaoEffects.LockOnSpeedEffect"/>
    /// for the four weapons and by <see cref="CustomXiaoEffects.SuperSteveEffect"/> for the sphere.
    ///
    /// The dungeon walk is ROOT MOTION: motionDrive (dun 0x1DB7xxx) copies her position from her root frame's accumulated
    /// translation every frame, and the stick only steers (the camera-relative stick vector at 0x1DC4540), so there is no
    /// ground-speed constant — her speed is the moving clip at its play rate. Locked on she strafes with the attack-stance
    /// clips (c04b KEYs 19–22: 攻撃態勢 right / left / forward / back, frames 180–230 and 120–170; 18 = the stance idle), so
    /// the motion-speed override (<see cref="CharacterMotion.MotionSpeedOverride"/>, −1 = the KEY's rate) is held at
    /// <see cref="LockOnRate"/> while a lock is on and one of those — or a guard clip (8–10, 33: the guard walk moves) —
    /// plays, and put back to the KEY rate otherwise. The game itself writes −1 on every motion change, so the hold is
    /// re-asserted each tick.
    /// </summary>
    internal static class LockOnSpeed
    {
        private const string Tag = "[LockOnSpeed] ";
        private const float  LockOnRate = 1.3f;          // the strafes' play rate — and so the ground speed — while locked on (2.0 and 1.5 read too fast)
        // c04b KEYs: the four attack-stance strafes 19–22 (18 is the stance idle) and the guard — 8 enter, 9 loop, 10 exit,
        // 33 the guard walk (530–540, the one that moves).
        private static readonly int[] LockOnMoves = { 19, 20, 21, 22, 8, 9, 10, 33 };
        private static bool _held;

        /// <summary>Whether a weapon carries the buff: Dragon's Y and the three that inherit it. Also the test for a
        /// SynthSphere's source weapon on Super Steve.</summary>
        internal static bool Grants(int weaponId)
            => weaponId == Items.dragonsy || weaponId == Items.divinebeasttitle || weaponId == Items.angelshooter || weaponId == Items.angelgear;

        /// <summary>Drive every tick while a granting weapon (or sphere) is equipped; <paramref name="active"/> false releases.</summary>
        internal static void Drive(bool active)
        {
            if (!active) { if (_held) Release(); return; }
            bool locked = Memory.ReadInt(PlayerAction.LockOnActive) != 0 && Memory.ReadInt(PlayerAction.LockOnTargetSlot) >= 0;
            int motion = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool want = locked && Array.IndexOf(LockOnMoves, motion) >= 0;
            if (want)
            {
                if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) != LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, LockOnRate);
                if (!_held) { _held = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"locked on — moving at ×{LockOnRate}"); }
            }
            else if (_held) Release();
        }

        /// <summary>The weapon or the floor went: the KEY rate again.</summary>
        internal static void Stop() { if (_held) Release(); }

        private static void Release()
        {
            _held = false;
            if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) == LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "lock-on speed released");
        }
    }
}
