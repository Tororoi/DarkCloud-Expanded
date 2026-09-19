using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Dragon's Y — while Xiao is locked on to an enemy she moves at double speed, and a shot held for
    /// <see cref="ChargeSeconds"/> fires the Gemron's ball of the selected element: the native pellet is taken the tick it
    /// appears and <see cref="GemronShots.Fire"/> launches the ball from its position with its velocity (the pellet's own
    /// speed) at <see cref="DamageMult"/>× its damage; with no element selected — or no slot for it on this floor — the pellet
    /// itself flies on at <see cref="PelletScale"/>× its sprite size and the same damage. A charged shot costs
    /// ChargedShotWhp's weapon HP.
    ///
    /// The dungeon walk is ROOT MOTION: motionDrive (dun 0x1DB7xxx) copies her position from her root frame's accumulated
    /// translation every frame, and the stick only steers (the camera-relative stick vector at 0x1DC4540), so there is no
    /// ground-speed constant — her speed is the moving clip at its play rate. Locked on she strafes with the attack-stance
    /// clips (c04b KEYs 19–22: 攻撃態勢 right / left / forward / back, frames 180–230 and 120–170; 18 = the stance idle), so
    /// the motion-speed override (<see cref="CharacterMotion.MotionSpeedOverride"/>, −1 = the KEY's rate) is held at
    /// <see cref="LockOnRate"/> while a lock is on and one of those — or a guard clip (8–10, 33: the guard walk moves) —
    /// plays, and put back to the KEY rate otherwise. The game
    /// itself writes −1 on every motion change, so the hold is re-asserted each tick.
    /// </summary>
    internal static class DragonsY
    {
        private const string Tag = "[DragonsY] ";
        private const float  LockOnRate = 1.3f;          // the strafes' play rate — and so the ground speed — while locked on (2.0 and 1.5 read too fast)
        // c04b KEYs: the four attack-stance strafes 19–22 (18 is the stance idle) and the guard — 8 enter, 9 loop, 10 exit,
        // 33 the guard walk (530–540, the one that moves).
        private static readonly int[] LockOnMoves = { 19, 20, 21, 22, 8, 9, 10, 33 };

        private static bool _held;

        private const double ChargeSeconds = 1.0;
        private const double ArmSeconds    = 0.5;    // a charged release must produce its pellet within this
        private const float  DamageMult    = 2.0f;
        private const float  PelletScale   = 5.0f;   // no element: the pellet itself, grown
        private const int    NoElement     = 5;      // elementHUD: 00 Fire … 04 Holy, 05 None
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static bool     _holding, _charged, _nativeWarned;
        private static DateTime _holdStart, _armedUntil = DateTime.MinValue;

        /// <summary>Drive every tick while Dragon's Y is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!_nativeWarned && (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) != DunPatches.CatFollowHookNew)
            { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the Gemron-shots cave is not in this ISO — charged shots fall back to the grown pellet (re-patch the ISO)"); }
            DriveCharge();
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

        private static void DriveCharge()
        {
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;
            ChargedShotWhp.Tick();
            if (holding)
            {
                if (!_holding) { _holding = true; _charged = false; _holdStart = GameClock.Now; }
                double held = (GameClock.Now - _holdStart).TotalSeconds;
                if (!_charged && held >= ChargeSeconds) { _charged = true; Player.FlashChargeComplete(); }
                ChargedShotWhp.Arm(_charged ? ChargedShotWhp.ChargedFactor : 1f);
                ChargeTint.Ramp(_charged ? 0 : ChargeSeconds - held);
            }
            else
            {
                ChargeTint.Clear();
                if (_holding)
                {
                    _holding = false;
                    if (_charged) _armedUntil = GameClock.Now.AddSeconds(ArmSeconds);
                    _charged = false;
                }
            }
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i])
                {
                    _seen[i] = true;
                    if (GameClock.Now < _armedUntil) { _armedUntil = DateTime.MinValue; ChargedPellet(pool, i); }
                }
                else if (!live) _seen[i] = false;
            }
        }

        /// <summary>The charged pellet: the element's Gemron ball in its place, else the pellet grown.</summary>
        private static void ChargedPellet(long pool, int slot)
        {
            long dmgA = PlayerShotPool.DamageAddr(pool, slot), pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            int damage = (int)(Memory.ReadInt(dmgA) * DamageMult);
            int element = Player.Weapon.GetCurrentWeaponElement();
            if (element != NoElement
                && GemronShots.Fire(element, Memory.ReadFloat(pa), Memory.ReadFloat(pa + 4), Memory.ReadFloat(pa + 8),
                                    Memory.ReadFloat(va), Memory.ReadFloat(va + 4), Memory.ReadFloat(va + 8),
                                    damage, Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot))))
            {
                Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);                    // the pellet gives way to the ball
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: element {element} ball in the pellet's place, damage {damage}");
                return;
            }
            Memory.WriteInt  (dmgA, damage);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), PelletScale);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: pellet ×{PelletScale}, damage {damage}" + (element == NoElement ? " (no element)" : $" (no slot for element {element} on this floor)"));
        }

        /// <summary>The weapon or the floor went (or the lock/motion ended): the KEY rate again.</summary>
        internal static void Stop() { if (_held) Release(); _holding = false; _armedUntil = DateTime.MinValue; }

        private static void Release()
        {
            _held = false;
            if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) == LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "lock-on speed released");
        }
    }
}
