using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Evilcise and Maneater — the two cursing swords and the curse machinery they share (also driven by Super Steve for Xiao).</summary>
    internal static class ToanCurses
    {
        // ── Evilcise "Jealous Soul" ────────────────────────────────────────────────────────
        public static bool evilciseNewFloor = false;

        /// <summary>
        /// Ability Name: Jealous Soul (Evilcise)
        /// Evilcise effect: Toan is cursed while equipped and immune to all other status effects.
        /// Breaking the curse with holy water applies poison and sets HP to 1.
        /// The curse is reapplied on each new floor.
        /// </summary>
        public static void EvilciseEffect()
        {
            var st = new CurseState();
            var target = new CurseAddrs(ToanState.Status, ToanState.StatusTimer, ToanState.Hp);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id + toanSlot * WeaponHave.InventoryWeaponSlotStride) != Items.evilcise)
                    break;
                EvilciseDrive(true, target, st);
                Thread.Sleep(100);
            }
            EvilciseDrive(false, target, st);   // strip curse on unequip / dungeon exit
        }

        // ── Shared curse machinery (Evilcise / Maneater — also driven by Super Steve for Xiao) ──
        /// <summary>The three per-character status/HP addresses a curse effect writes. Status BITS
        /// and the 3600-frame duration are character-independent (see ToanState); only these
        /// addresses differ between Toan and, e.g., Xiao.</summary>
        internal readonly struct CurseAddrs
        {
            public readonly int Status, StatusTimer, Hp;
            public CurseAddrs(int status, int statusTimer, int hp) { Status = status; StatusTimer = statusTimer; Hp = hp; }
        }

        /// <summary>Per-wielder curse state (no shared statics), so the Toan thread and the Super
        /// Steve thread never trample each other's tracking.</summary>
        internal sealed class CurseState
        {
            public bool Resolved;       // Evilcise: penalized · Maneater: cleansed (no penalty)
            public bool WasNearDeath;
            public byte LastFloor = 0xFF;
            public bool Applied;        // curse currently owned by this driver (for a clean strip)
            public DateTime LastDrain = DateTime.MinValue;
        }

        // Jealous Soul (Evilcise): curse the wielder (immune to other statuses); breaking the curse
        // with holy water applies Poison + sets HP to 1; reapplied each floor. `active` = the wielder
        // is the acting character with the weapon in hand; pass false to strip on unequip / switch.
        internal static void EvilciseDrive(bool active, CurseAddrs a, CurseState st)
        {
            if (!active)
            {
                if (st.Applied)
                {
                    ushort final = Memory.ReadUShort(a.Status);
                    Memory.WriteUShort(a.Status, (ushort)(final & ~ToanState.StatusCurse));
                    st.Applied = false;
                }
                return;
            }

            ushort cur = Memory.ReadUShort(a.Status);
            if (!st.Applied)
            {
                // Equip: apply curse immediately, but not while in NearDeath.
                st.Applied = true; st.Resolved = false;
                st.LastFloor = Memory.ReadByte(Addresses.checkFloor);
                st.WasNearDeath = (cur & ToanState.StatusNearDeath) != 0;
                if (!st.WasNearDeath)
                {
                    Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusCurse));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
                return;
            }

            byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
            if (currentFloor != st.LastFloor)
            {
                // New floor: clear penalty/NearDeath tracking and reapply curse.
                st.Resolved = false; st.WasNearDeath = false; st.LastFloor = currentFloor;
                cur = Memory.ReadUShort(a.Status);
                Memory.WriteUShort(a.Status, (ushort)((cur & ToanState.StatusNearDeath) | ToanState.StatusCurse));
                Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
            }

            cur = Memory.ReadUShort(a.Status);
            bool nearDeath = (cur & ToanState.StatusNearDeath) != 0;
            if (nearDeath) { st.WasNearDeath = true; }
            else if (st.WasNearDeath)
            {
                st.WasNearDeath = false;
                if (!st.Resolved)
                {
                    Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusCurse));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
            }
            else if (!st.Resolved)
            {
                if ((cur & ToanState.StatusCurse) == 0)
                {
                    // Curse removed externally (holy water) — penalize.
                    st.Resolved = true;
                    Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusPoison));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                    Memory.WriteUShort(a.Hp, 1);
                }
                else
                {
                    // Curse active: strip other statuses and refresh timer.
                    Memory.WriteUShort(a.Status, (ushort)((cur & ToanState.StatusNearDeath) | ToanState.StatusCurse));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
            }
        }

        // ── Maneater "Blood Price" ─────────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Blood Price (Maneater)
        /// Maneater effect: Toan is cursed while equipped (reapplied each floor), but unlike
        /// Evilcise, breaking the curse with holy water carries no penalty — the curse simply
        /// stays off until the next floor. While the weapon is in the game's low-durability
        /// warning state (WHP &lt;= 10% of max — the exact check that makes the HUD gauge blink,
        /// <c>DrawWepDamageDraw</c> ELF 0x1F8D30, threshold constant 0x2A1870), the sword feeds
        /// on Toan: each second it drains 1 HP to restore 1 WHP. It never kills — the drain
        /// stops at 1 HP and pauses during NearDeath.
        /// </summary>
        public static void ManeaterEffect()
        {
            var st = new CurseState();
            var target = new CurseAddrs(ToanState.Status, ToanState.StatusTimer, ToanState.Hp);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                long weaponRecord = WeaponHave.InventoryWeaponSlot0Id + toanSlot * WeaponHave.InventoryWeaponSlotStride;
                if (Memory.ReadUShort(weaponRecord) != Items.maneater)
                    break;
                ManeaterDrive(true, target, weaponRecord, st);
                Thread.Sleep(100);
            }
            ManeaterDrive(false, target, 0, st);   // strip curse on unequip / dungeon exit
        }

        // Blood Price (Maneater): curse the wielder (cleansing with holy water carries NO penalty,
        // stays off for the floor); while the weapon is in its native low-WHP warning state, drain
        // 1 HP/sec to restore 1 WHP (never kills). `weaponRecord` is the wielded weapon's record
        // (for the WHP drain); `active` false strips the curse. Shares CurseAddrs/CurseState with Evilcise.
        internal static void ManeaterDrive(bool active, CurseAddrs a, long weaponRecord, CurseState st)
        {
            if (!active)
            {
                if (st.Applied)
                {
                    ushort final = Memory.ReadUShort(a.Status);
                    Memory.WriteUShort(a.Status, (ushort)(final & ~ToanState.StatusCurse));
                    st.Applied = false;
                }
                return;
            }

            ushort cur = Memory.ReadUShort(a.Status);
            if (!st.Applied)
            {
                // Equip: apply curse immediately, but not while in NearDeath.
                st.Applied = true; st.Resolved = false; st.LastDrain = GameClock.Now;
                st.LastFloor = Memory.ReadByte(Addresses.checkFloor);
                st.WasNearDeath = (cur & ToanState.StatusNearDeath) != 0;
                if (!st.WasNearDeath)
                {
                    Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusCurse));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
                return;
            }

            byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
            if (currentFloor != st.LastFloor)
            {
                // New floor: clear cure/NearDeath tracking and reapply curse.
                st.Resolved = false; st.WasNearDeath = false; st.LastFloor = currentFloor;
                cur = Memory.ReadUShort(a.Status);
                Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusCurse));
                Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
            }

            cur = Memory.ReadUShort(a.Status);
            bool nearDeath = (cur & ToanState.StatusNearDeath) != 0;
            if (nearDeath) { st.WasNearDeath = true; }
            else if (st.WasNearDeath)
            {
                st.WasNearDeath = false;
                if (!st.Resolved)
                {
                    Memory.WriteUShort(a.Status, (ushort)(cur | ToanState.StatusCurse));
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
            }
            else if (!st.Resolved)
            {
                if ((cur & ToanState.StatusCurse) == 0)
                {
                    st.Resolved = true;   // cleansed — no penalty, stays off this floor
                }
                else if ((cur & unchecked((ushort)~(ToanState.StatusNearDeath | ToanState.StatusCurse))) == 0)
                {
                    // Keep the curse alive by refreshing the (shared) status timer — but only while
                    // curse is the sole timed status, so a poison/freeze/goo being waited out is
                    // never extended. If the timer expires alongside such a status the curse lapses.
                    Memory.WriteUShort(a.StatusTimer, ToanState.StatusDurationFrames);
                }
            }

            // Blood price: 1 HP -> 1 WHP per second while in the native low-WHP warning state.
            if (GameClock.Now - st.LastDrain >= TimeSpan.FromSeconds(1))
            {
                st.LastDrain = GameClock.Now;
                float  whp    = Memory.ReadFloat(weaponRecord + WeaponHave.InventoryWeaponWhpOffset);
                short  maxWhp = Memory.ReadShort(weaponRecord + WeaponHave.InventoryWeaponMaxWhpOffset);
                ushort hp     = Memory.ReadUShort(a.Hp);
                if (!nearDeath && hp > 1 && maxWhp > 0 &&
                    whp > 0f && whp <= WeaponMenu.LowWhpWarningFraction * maxWhp)
                {
                    Memory.WriteUShort(a.Hp, (ushort)(hp - 1));
                    Memory.WriteFloat(weaponRecord + WeaponHave.InventoryWeaponWhpOffset, Math.Min(whp + 1f, maxWhp));
                }
            }
        }
    }
}
