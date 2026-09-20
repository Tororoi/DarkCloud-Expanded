using System;

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
    /// shot, of its own selected element (<see cref="CustomXiaoEffects.SuperSteveEffect"/> drives it). The lock-on movement
    /// buff is <see cref="LockOnSpeed"/>, inherited further.
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
            return id == Items.dragonsy || (id == Items.supersteve && SuperSteveAbilities.AttachedSphere(rec) == Items.dragonsy);
        }

        /// <summary>The config for a selected element (00 Fire … 04 Holy, 05 none), or null for anything else.</summary>
        private static BorrowedEffect Shot(int element)
            => element >= 0 && element < ShotEffectPack.DragonsYCfg.Length ? BorrowedShots.TableConfig(ShotEffectPack.DragonsYCfg[element]) : null;

        /// <summary>The weapon or the floor went: no charge held, no kick mark.</summary>
        internal static void Stop() { _holding = false; _armedUntil = DateTime.MinValue; ChargeTint.Clear(); Memory.WriteInt(CodeCaves.Mailbox.PelletKickDamage, 0); }
    }
}
