using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Dragon's Y — while Xiao is locked on to an enemy she moves at double speed, and a shot held for
    /// <see cref="ChargeSeconds"/> fires the Gemron's ball of the selected element: the native pellet is taken the tick it
    /// appears and <see cref="BorrowedShots.Fire"/> launches the ball from its position with its velocity (the pellet's own
    /// speed) at <see cref="DamageMult"/>× its damage — with no element selected, the Black Dragon's shot instead;
    /// only when this floor has no slot for it does the pellet itself fly on at <see cref="PelletScale"/>× its sprite size
    /// and the same damage. The shot carries the Matador's kick strength: ElfCave.CatGuardBypass stamps it on every entry
    /// whose base damage is <see cref="Mailbox.PelletKickDamage"/>, with the ORIGIN at that entry's own sphere centre —
    /// the burst — so each enemy it catches is shoved straight out of the burst; the guard window stands (no crush here).
    /// A charged shot costs ChargedShotWhp's weapon HP.
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
        private const float  DamageMult    = 1.5f;
        private const float  PelletScale   = 5.0f;   // no slot on this floor: the pellet itself, grown
        private const int    ElementOffset = 0x16;   // the weapon record's selected element: 00 Fire … 04 Holy, 05 None
        private const float  KickStrength  = 2.5f, KickDecay = 0.1f;   // the Matador's kick (Goro's hammer swing), out of the burst
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
            int element = Player.Weapon.GetCurrentWeaponElement();                          // 5 = none → the Black Dragon's shot
            float x = Memory.ReadFloat(pa), h = Memory.ReadFloat(pa + 4), y = Memory.ReadFloat(pa + 8);
            float vx = Memory.ReadFloat(va), vh = Memory.ReadFloat(va + 4), vy = Memory.ReadFloat(va + 8);
            // The kick, as the Matador's: the bypass cave stamps it on this damage's entries, out of each one's own sphere.
            Memory.WriteFloat(CodeCaves.Mailbox.PelletKickStrength, KickStrength);
            Memory.WriteFloat(CodeCaves.Mailbox.PelletKickDecay, KickDecay);
            Memory.WriteInt  (CodeCaves.Mailbox.PelletKickDamage, damage);
            if (BorrowedShots.Fire(ShotEffectPack.DragonsYCfg[element], x, h, y, vx, vh, vy, damage, Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot))))
            {
                Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);                    // the pellet gives way to the shot
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: {(element == 5 ? "black dragon shot" : $"element {element} ball")} in the pellet's place, damage {damage}");
                return;
            }
            Memory.WriteInt  (dmgA, damage);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), PelletScale);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: pellet ×{PelletScale}, damage {damage} (no slot for element {element} on this floor)");
        }

        /// <summary>The weapon or the floor went (or the lock/motion ended): the KEY rate again.</summary>
        /// <summary>The shot config Dragon's Y wants entered on every floor — its selected element's, read off Xiao's inventory
        /// record (valid in town and dungeon alike) — or −1 while she has another weapon. BorrowedShots asks every tick.</summary>
        internal static int WantedShot()
        {
            int slot = Memory.ReadByte(DngStatusData.Base + DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
            if ((uint)slot > 9) return -1;
            long rec = DngStatusData.WeaponRecord(Player.XiaoId, slot);
            if (Memory.ReadUShort(rec) != Items.dragonsy) return -1;
            int element = Memory.ReadByte(rec + ElementOffset);
            return element >= 0 && element < ShotEffectPack.DragonsYCfg.Length ? ShotEffectPack.DragonsYCfg[element] : -1;
        }

        internal static void Stop() { if (_held) Release(); _holding = false; _armedUntil = DateTime.MinValue; Memory.WriteInt(CodeCaves.Mailbox.PelletKickDamage, 0); }

        private static void Release()
        {
            _held = false;
            if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) == LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "lock-on speed released");
        }
    }
}
