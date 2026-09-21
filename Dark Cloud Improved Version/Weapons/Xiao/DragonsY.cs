using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Dragon's Y — a shot held for <see cref="ChargeSeconds"/> fires the Gemron's ball of the selected element: the native
    /// pellet is taken the tick it appears and <see cref="BorrowedShots.Fire"/> launches the ball from its position with its
    /// velocity (the pellet's own speed) at <see cref="DamageMult"/>× its damage — with no element selected, the Black
    /// Dragon's shot instead; only when this floor has no slot for it does the pellet itself fly on at <see cref="PelletScale"/>×
    /// its sprite size and the same damage. The shot carries the Matador's kick strength: DunCave.CatGuardBypass stamps it
    /// on every entry whose base damage is <see cref="Mailbox.PelletKickDamage"/>, with the ORIGIN at that entry's own sphere
    /// centre — the burst — so each enemy it catches is shoved straight out of the burst; the guard window stands (no crush
    /// here). A charged shot costs ChargedShotWhp's weapon HP. Super Steve carrying a Dragon's Y SynthSphere has the same
    /// shot, of its own selected element (<see cref="SuperSteve.SphereInheritanceEffect"/> drives it). The lock-on movement
    /// buff is <see cref="LockOnSpeedDrive"/>, inherited further.
    /// </summary>
    internal static class DragonsY
    {
        private const string Tag = "[DragonsY] ";
        private const double ChargeSeconds = 1.0;
        private const double ArmSeconds    = 0.5;    // a charged release must produce its pellet within this
        private const float  DamageMult    = 1.5f;
        private const float  PelletScale   = 5.0f;   // no slot on this floor: the pellet itself, grown
        private const int    ElementOffset = 0x16;   // the weapon record's selected element: 00 Fire … 04 Holy, 05 None
        private const float  KickStrength  = 2.5f, KickDecay = 0.1f;   // the Matador's kick (Goro's hammer swing), out of the burst
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static bool     _holding, _charged, _nativeWarned;
        private static DateTime _holdStart, _armedUntil = DateTime.MinValue;

        /// <summary>Drive every tick while Dragon's Y (or Super Steve with its sphere) is equipped; <paramref name="active"/>
        /// false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!_nativeWarned && (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) != DunPatches.CatFollowHookNew)
            { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the Gemron-shots cave is not in this ISO — charged shots fall back to the grown pellet (re-patch the ISO)"); }
            DriveCharge();
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
            if (BorrowedShots.Fire(Shot(element), x, h, y, vx, vh, vy, damage, Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot))))
            {
                Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);                    // the pellet gives way to the shot
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: {(element == 5 ? "black dragon shot" : $"element {element} ball")} in the pellet's place, damage {damage}");
                return;
            }
            Memory.WriteInt  (dmgA, damage);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), PelletScale);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: pellet ×{PelletScale}, damage {damage} (element {element}'s shot is not entered on this floor)");
        }

        /// <summary>The shot config Dragon's Y wants entered on every floor — its selected element's, read off Xiao's inventory
        /// record (valid in town and dungeon alike); Super Steve with a Dragon's Y sphere wants its own element's — or null
        /// while she has another weapon or is not the active character (the main-character instance is every character's;
        /// Ruby's and Osmond's own shots live there when they are out). BorrowedShots asks every tick.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.XiaoId) return null;
            int slot = Memory.ReadByte(DngStatusData.Base + DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
            if ((uint)slot > 9) return null;
            long rec = DngStatusData.WeaponRecord(Player.XiaoId, slot);
            if (!Wields(rec)) return null;
            return Shot(Memory.ReadByte(rec + ElementOffset));
        }

        /// <summary>Whether the weapon record is Dragon's Y, or Super Steve carrying its SynthSphere.</summary>
        internal static bool Wields(long rec)
        {
            int id = Memory.ReadUShort(rec);
            return id == Items.dragonsy || (id == Items.supersteve && SuperSteve.AttachedSphere(rec) == Items.dragonsy);
        }

        /// <summary>The config for a selected element (00 Fire … 04 Holy, 05 none), or null for anything else.</summary>
        private static BorrowedEffect Shot(int element)
            => element >= 0 && element < ShotEffectPack.DragonsYCfg.Length ? BorrowedShots.TableConfig(ShotEffectPack.DragonsYCfg[element]) : null;

        /// <summary>The weapon or the floor went: no charge held, no kick mark.</summary>
        internal static void Stop() { _holding = false; _armedUntil = DateTime.MinValue; ChargeTint.Clear(); Memory.WriteInt(CodeCaves.Mailbox.PelletKickDamage, 0); }

        // ── Dragon's Y ─────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Dragon's Y thread: hands every tick to <see cref="DragonsY.Drive"/> (the charged shot) while the
        /// weapon is equipped, and stands it down once when it goes. Its movement buff has its own thread,
        /// <see cref="LockOnSpeedEffect"/>, shared with the weapons that inherit it.</summary>
        public static void DragonsBreathEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.dragonsy)
            {
                DragonsY.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            DragonsY.Stop();
        }

        // ── the lock-on movement buff (Dragon's Y, inherited by Divine Beast Title, Angel Shooter, Angel Gear) ──
        /// <summary>
        /// Dragon's Y's movement: while Xiao is locked on to an enemy she moves at <see cref="LockOnRate"/>× speed. The buff is
        /// Dragon's Y's, inherited by its line — Divine Beast Title, Angel Shooter and Angel Gear — and by Super Steve carrying
        /// any of the four's SynthSphere (<see cref="LockOnSpeedGrants"/>). Driven each tick by <see cref="LockOnSpeedEffect"/>
        /// for the four weapons and by <see cref="SuperSteve.SphereInheritanceEffect"/> for the sphere.
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
        private const float  LockOnRate = 1.3f;          // the strafes' play rate — and so the ground speed — while locked on (2.0 and 1.5 read too fast)
        // c04b KEYs: the four attack-stance strafes 19–22 (18 is the stance idle) and the guard — 8 enter, 9 loop, 10 exit,
        // 33 the guard walk (530–540, the one that moves).
        private static readonly int[] LockOnMoves = { 19, 20, 21, 22, 8, 9, 10, 33 };
        private static bool _lockOnHeld;

        /// <summary>Whether a weapon carries the buff: Dragon's Y and the three that inherit it. Also the test for a
        /// SynthSphere's source weapon on Super Steve.</summary>
        internal static bool LockOnSpeedGrants(int weaponId)
            => weaponId == Items.dragonsy || weaponId == Items.divinebeasttitle || weaponId == Items.angelshooter || weaponId == Items.angelgear;

        /// <summary>Drive every tick while a granting weapon (or sphere) is equipped; <paramref name="active"/> false releases.</summary>
        internal static void LockOnSpeedDrive(bool active)
        {
            if (!active) { if (_lockOnHeld) LockOnSpeedRelease(); return; }
            bool locked = Memory.ReadInt(PlayerAction.LockOnActive) != 0 && Memory.ReadInt(PlayerAction.LockOnTargetSlot) >= 0;
            int motion = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool want = locked && Array.IndexOf(LockOnMoves, motion) >= 0;
            if (want)
            {
                if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) != LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, LockOnRate);
                if (!_lockOnHeld) { _lockOnHeld = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"locked on — moving at ×{LockOnRate}"); }
            }
            else if (_lockOnHeld) LockOnSpeedRelease();
        }

        /// <summary>The weapon or the floor went: the KEY rate again.</summary>
        internal static void LockOnSpeedStop() { if (_lockOnHeld) LockOnSpeedRelease(); }

        private static void LockOnSpeedRelease()
        {
            _lockOnHeld = false;
            if (Memory.ReadFloat(CharacterMotion.MotionSpeedOverride) == LockOnRate) Memory.WriteFloat(CharacterMotion.MotionSpeedOverride, CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "lock-on speed released");
        }

        /// <summary>The lock-on movement buff's thread: hands every tick to <see cref="DragonsY.LockOnSpeedDrive"/> while one of the
        /// weapons that carry it is equipped (<see cref="DragonsY.LockOnSpeedGrants"/>), and releases it once when it goes. Super Steve
        /// drives the same buff from <see cref="SphereInheritanceEffect"/> when its sphere is one of theirs.</summary>
        public static void LockOnSpeedEffect()
        {
            while (Player.InDungeonFloor() && DragonsY.LockOnSpeedGrants(Player.Weapon.GetCurrentWeaponId()))
            {
                DragonsY.LockOnSpeedDrive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            DragonsY.LockOnSpeedStop();
        }
    }
}
