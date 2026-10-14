using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Crystal Ring — a cursed ring. Wearing it curses Ruby (on equip and on every floor), and while the curse holds her element
    /// is Ice and every orb she releases (her quick shot, a partial or a full charge — the ball she holds while charging stays hers)
    /// flies as the Crystal Gemron's ice arrow instead, homing on the locked-on enemy, else the nearest one, else the nearest of her
    /// door crystals ("Prism": an arrow opens a crystal of any colour). Curing the curse costs nothing: her shots and her element choice
    /// are her own again until the next floor or the ring is put on again.
    ///
    /// The arrow is config 34 (ElfSpeciesPatches: the korinoya as a shot) borrowed into the SECOND main-character instance
    /// (<see cref="WantedShot"/>; BorrowedShots keeps it entered), stepped and drawn beside her own while CodeCaves.SecondEffectLive is
    /// set. Each orb is put out the tick it appears and an arrow fired in its place at its damage (<see cref="ReplaceOrbs"/>). The
    /// homing is the Gemron's own steering (crystal_home.s) run by ruby_arrows.s on the second instance before it steps, at the target
    /// the mod names in CodeCaves.CrystalArrows (<see cref="Aim"/>). Each arrow carries the floor's door key as its hit's user id, which
    /// is what a door crystal compares (DungeonEvents).
    ///
    /// The ring is held to Ice wherever its curse would hold (<see cref="MenuTick"/>): CodeCaves.ElementLock makes the D-pad picker offer
    /// Ice alone and the weapon menu refuse any other element with the curse's warning, and the ring's record is kept at Ice, so the
    /// game's own loads of Ruby's per-element shot (on equipping it, on every floor) load the Ice one and her charge ball matches.</summary>
    internal static class CrystalRing
    {
        private const string Tag = "[CrystalRing] ";
        private const int   IceArrowConfig = 34;          // the korinoya shot (ElfSpeciesPatches.PatchIceArrowConfig)
        private const byte  IceElement = 1;               // 0 Fire, 1 Ice, 2 Thunder, 3 Wind, 4 Holy
        private const float ArrowSpeed = 2.5f;            // Ruby's own ball's flight speed (her c05_*03 configs)
        private const int   ArrowLife  = 180;             // frames of flight, her ball's
        private const int   HeldBall   = 1;               // SetLoop(1) (+0xA0B0): the charge ball she holds, never replaced
        private const float EnemyRange = 200f;            // the nearest enemy within this of her is the target
        private const float CrystalRange = 250f;          // …else the nearest of her door crystals within this
        private const float RootLift   = 8f;              // above an enemy's feet when its species names no lock-on frame
        private const int   TickMs = 16;

        private static readonly bool[] _seen = new bool[ShotEffectPack.SubShots];
        private static readonly CurseState _curse = new CurseState();
        private static bool _live, _armed;
        private static volatile bool _running, _cured;   // the ring's thread is up / its curse was cured (until the next floor or wearing)
        private static int _ringSlot = -1;                // Ruby's bag slot holding the worn ring
        private static int _menuTicks;
        private static uint _target;                      // the guest position the arrows home on (0 = none)
        private static BorrowedEffect _arrow;

        /// <summary>Ruby with the Crystal Ring in hand.</summary>
        internal static bool Wielded() => Player.CurrentCharacterNum() == Player.RubyId && Player.Weapon.GetCurrentWeaponId() == Items.crystalring;

        /// <summary>The ring's curse holds (put on by the ring, not cured since).</summary>
        internal static bool Cursed => _curse.Applied && !_curse.Resolved;

        /// <summary>The effect the ring wants in Ruby's second main-character instance: the ice arrow, while the curse holds.</summary>
        internal static BorrowedEffect WantedShot() => Wielded() && Cursed ? Arrow() : null;

        private static BorrowedEffect Arrow()
        {
            if (_arrow != null) return _arrow;
            _arrow = BorrowedShots.TableConfig(IceArrowConfig, keepFlags: false, instance: ShotEffectPack.CharaMainEffectCrash);
            if (_arrow != null) BitConverter.GetBytes(ArrowSpeed).CopyTo(_arrow.Cfg, ShotEffectPack.CfgSpeed);   // the steering keeps it at her ball's pace
            return _arrow;
        }

        /// <summary>The ring's thread: every tick while it is in Ruby's hand on a floor; the curse and everything else taken back once
        /// it goes.</summary>
        public static void CursedPrismEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "on Ruby's hand");
            _running = true;
            _ringSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr + Player.RubyId);
            while (Player.InDungeonFloor() && Wielded())
            {
                Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(TickMs);
            }
            Stop();
        }

        private static void Drive(bool active)
        {
            Curse();
            if (!active) return;
            BorrowedEffect fx = Cursed ? Arrow() : null;
            if (fx == null || !BorrowedShots.Entered(fx)) { Disarm(); return; }
            if (Memory.ReadUInt(ShotEffectPack.MainEffectLivePtr) != ShotEffectPack.CharaMainEffect - 0x20000000L) { Disarm(); return; }   // a broken weapon fires from the second instance itself
            KeepLive();
            ReplaceOrbs(fx);
            Aim();
        }

        // ── the curse: on equip and on every floor; a cure holds until the next floor (Maneater's rules, without its drain) ──
        private static void Curse()
        {
            int status = Player.Ruby.status;
            ushort cur = Memory.ReadUShort(status);
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (!_curse.Applied || floor != _curse.LastFloor)
            {
                bool first = !_curse.Applied;
                _curse.Applied = true; _curse.Resolved = false; _curse.LastFloor = floor;
                _curse.WasNearDeath = (cur & ToanState.StatusNearDeath) != 0;
                if (!_curse.WasNearDeath) Memory.WriteUShort(status, (ushort)(cur | ToanState.StatusCurse));
                ElementMenu.Apply(IceElement);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (first ? "Ruby is cursed: ice arrows, Ice locked" : "a new floor: the curse is back"));
                return;
            }
            bool nearDeath = (cur & ToanState.StatusNearDeath) != 0;
            if (nearDeath) _curse.WasNearDeath = true;
            else if (_curse.WasNearDeath)
            {
                _curse.WasNearDeath = false;
                if (!_curse.Resolved) Memory.WriteUShort(status, (ushort)(cur | ToanState.StatusCurse));
            }
            else if (!_curse.Resolved && (cur & ToanState.StatusCurse) == 0)
            {
                _curse.Resolved = true; _cured = true;
                Disarm();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the curse is cured: her own shots and elements until the next floor");
            }
        }

        // ── the arrows ──
        /// <summary>Each orb the tick it appears in her live instance — the held charge ball excepted — put out, and an ice arrow fired
        /// from the second instance at its position and direction, at its damage, with her owner id and the floor's door key.</summary>
        private static void ReplaceOrbs(BorrowedEffect fx)
        {
            long inst = ShotEffectPack.CharaMainEffect;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return;
            int doorKey = Memory.ReadInt(DungeonEvents.RubyDoorKey);
            for (int i = 0; i < count; i++)
            {
                bool live = Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) != 0;
                if (!live) { _seen[i] = false; continue; }
                if (_seen[i]) continue;
                _seen[i] = true;
                if (Memory.ReadInt(inst + ShotEffectPack.OffA0B0 + i * 4) == HeldBall) continue;          // the ball she is charging
                long obj = inst + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride, dir = inst + ShotEffectPack.OffDir + i * 0x10;
                float x = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), h = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), y = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                float vx = Memory.ReadFloat(dir), vh = Memory.ReadFloat(dir + 4), vy = Memory.ReadFloat(dir + 8);
                int damage = Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4);
                if (BorrowedShots.Fire(fx, x, h, y, vx, vh, vy, damage, ArrowLife, Player.RubyId, userCol: doorKey))
                    Memory.WriteUShort(inst + ShotEffectPack.OffActive + i * 2, 0);   // her orb gives way
            }
        }

        /// <summary>The target the arrows home on (CodeCaves.CrystalArrows): the locked-on enemy, else the nearest within
        /// <see cref="EnemyRange"/>, else the nearest of her door crystals within <see cref="CrystalRange"/> — its lock-on point (a live
        /// world position the game refreshes every drawn frame) or its feet + <see cref="RootLift"/>, a crystal's own point.</summary>
        private static void Aim()
        {
            float rx = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos), ry = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            uint target = 0; float lift = 0f;
            int slot = PlayerAction.LockHeld(out int locked) && locked < EnemyAddresses.FloorSlots.Count ? locked : -1;
            if (slot < 0 || !Enemies.IsLive(slot))
            {
                slot = -1; float best = EnemyRange * EnemyRange;
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                {
                    if (!Enemies.IsLive(s)) continue;
                    long p = EnemyAddresses.CharObjects.CharAddr(s) + EnemyAddresses.CharObjects.PosOffset;
                    float dx = Memory.ReadFloat(p) - rx, dy = Memory.ReadFloat(p + 8) - ry, d = dx * dx + dy * dy;
                    if (d < best) { best = d; slot = s; }
                }
            }
            if (slot >= 0)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.LockOnFrame)) != 0)
                    target = (uint)(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.LockOnPoint) - 0x20000000L);
                else { target = (uint)(EnemyAddresses.CharObjects.CharAddr(slot) + EnemyAddresses.CharObjects.PosOffset - 0x20000000L); lift = RootLift; }
            }
            else target = NearestCrystal(rx, ry);
            if (target != _target || !_armed)
            {
                Memory.WriteUInt(CodeCaves.CrystalArrows + CodeCaves.CrystalArrowsTarget, target);
                Memory.WriteFloat(CodeCaves.CrystalArrows + CodeCaves.CrystalArrowsLift, lift);
                _target = target;
            }
            if (!_armed) { Memory.WriteInt(CodeCaves.CrystalArrows + CodeCaves.CrystalArrowsArm, 1); _armed = true; }
        }

        /// <summary>The guest address of the nearest of her door crystals still shut (a hit marker answering to Ruby, enabled and switched on)
        /// within <see cref="CrystalRange"/> of her, or 0.</summary>
        private static uint NearestCrystal(float rx, float ry)
        {
            uint man = Memory.ReadUInt(DungeonEvents.NowEventManPtr);
            if (!Memory.IsValidGuest(man)) return 0;
            long recs = Memory.ToMmu(man) + DungeonEvents.Records;
            byte[] all = Memory.ReadBytesBatch(recs, DungeonEvents.RecordCount * DungeonEvents.RecordStride);
            if (all == null) return 0;
            uint best = 0; float bestD = CrystalRange * CrystalRange;
            for (int i = 0; i < DungeonEvents.RecordCount; i++)
            {
                int o = i * DungeonEvents.RecordStride;
                uint def = BitConverter.ToUInt32(all, o + DungeonEvents.RecDef) & Memory.PhysAddrMask;
                if (def == 0 || BitConverter.ToInt32(all, o + DungeonEvents.RecEnabled) == 0 || BitConverter.ToInt32(all, o + DungeonEvents.RecSwitch) == 0) continue;
                if (!Memory.IsValidGuest(def) || Memory.ReadInt(Memory.ToMmu(def) + DungeonEvents.DefChara) != Player.RubyId) continue;
                float dx = BitConverter.ToSingle(all, o + DungeonEvents.RecPos) - rx, dy = BitConverter.ToSingle(all, o + DungeonEvents.RecPos + 8) - ry, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = (uint)(man + DungeonEvents.Records + o + DungeonEvents.RecPos); }
            }
            return best;
        }

        /// <summary>The second instance stepped and drawn while the arrows fly (re-asserted: another ability may clear it).</summary>
        private static void KeepLive()
        {
            if (Memory.ReadInt(CodeCaves.SecondEffectLive) != 1) Memory.WriteInt(CodeCaves.SecondEffectLive, 1);
            _live = true;
        }

        /// <summary>Homing off and the second instance handed back.</summary>
        private static void Disarm()
        {
            if (_armed) { Memory.WriteInt(CodeCaves.CrystalArrows + CodeCaves.CrystalArrowsArm, 0); _armed = false; _target = 0; }
            if (_live) { Memory.WriteInt(CodeCaves.SecondEffectLive, 0); _live = false; }
        }

        /// <summary>The ring left her hand or the floor: homing off, the curse it put on taken back, the ring back to Ice before the next
        /// load of her shot, and the next wearing starts afresh.</summary>
        private static void Stop()
        {
            Disarm();
            if (_curse.Applied)
            {
                int status = Player.Ruby.status;
                Memory.WriteUShort(status, (ushort)(Memory.ReadUShort(status) & ~ToanState.StatusCurse));
            }
            if (_ringSlot >= 0 && _ringSlot < WeaponSlots && Memory.ReadUShort(WeaponRecord.Address(Player.RubyId, _ringSlot, WeaponRecord.Id)) == Items.crystalring)
                Memory.WriteByte(WeaponRecord.Address(Player.RubyId, _ringSlot, WeaponRecord.ElementHud), IceElement);
            _curse.Applied = false; _curse.Resolved = false; _curse.LastFloor = 0xFF;
            _cured = false; _running = false; _ringSlot = -1;
            Array.Clear(_seen, 0, _seen.Length);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "off Ruby's hand");
        }

        // ── the ring held to Ice, in every mode ──
        private const int WeaponSlots = 10;
        private const int RecordCheckTicks = 20;          // the bag's rings checked once a second (the menu tick is 50 ms)

        /// <summary>One game-loop tick (50 ms, every mode — WeaponPassives): unless the curse was cured, the ring is held to Ice
        /// (CodeCaves.ElementLock), every Crystal Ring in Ruby's bag is kept at Ice while it is not being worn on a floor, and the weapon
        /// menu's element warning reads as the curse's while the ring is its selection.</summary>
        internal static void MenuTick()
        {
            bool held = !_cured;
            uint lockWord = held ? (uint)Items.crystalring | (uint)IceElement << 16 : 0u;
            if (Memory.ReadUInt(CodeCaves.ElementLock) != lockWord) Memory.WriteUInt(CodeCaves.ElementLock, lockWord);
            if (held && !_running && ++_menuTicks >= RecordCheckTicks)
            {
                _menuTicks = 0;
                for (int slot = 0; slot < WeaponSlots; slot++)
                    if (Memory.ReadUShort(WeaponRecord.Address(Player.RubyId, slot, WeaponRecord.Id)) == Items.crystalring
                        && Memory.ReadByte(WeaponRecord.Address(Player.RubyId, slot, WeaponRecord.ElementHud)) != IceElement)
                        Memory.WriteByte(WeaponRecord.Address(Player.RubyId, slot, WeaponRecord.ElementHud), IceElement);
            }
            WeaponDescriptions.SetElementWarningCurse(held && MenuSelectsRing());
        }

        private static bool MenuSelectsRing()
        {
            if (Memory.ReadByte(WeaponMenu.MenuSelectedCharacter) != Player.RubyId) return false;
            byte slot = Memory.ReadByte(WeaponMenu.MenuSelectedWeaponSlot);
            return slot < WeaponSlots && Memory.ReadUShort(WeaponRecord.Address(Player.RubyId, slot, WeaponRecord.Id)) == Items.crystalring;
        }
    }
}
