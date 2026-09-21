using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Maneater — Blood Price: Toan is cursed while it is equipped (reapplied each floor), cleansing it carries no penalty,
    /// and while the sword is in its low-WHP warning state it feeds on him, 1 HP for 1 WHP a second, never below 1 HP. Super Steve
    /// drives the same for Xiao.</summary>
    internal static class Maneater
    {
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
        public static void BloodPriceEffect()
        {
            var st = new CurseState();
            var target = new CurseAddrs(ToanState.Status, ToanState.StatusTimer, ToanState.Hp);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                long weaponRecord = WeaponHave.InventoryWeaponSlot0Id + toanSlot * WeaponHave.InventoryWeaponSlotStride;
                if (Memory.ReadUShort(weaponRecord) != Items.maneater)
                    break;
                Drive(true, target, weaponRecord, st);
                Thread.Sleep(100);
            }
            Drive(false, target, 0, st);   // strip curse on unequip / dungeon exit
        }

        // Blood Price (Maneater): curse the wielder (cleansing with holy water carries NO penalty,
        // stays off for the floor); while the weapon is in its native low-WHP warning state, drain
        // 1 HP/sec to restore 1 WHP (never kills). `weaponRecord` is the wielded weapon's record
        // (for the WHP drain); `active` false strips the curse. Shares CurseAddrs/CurseState with Evilcise (Toan/Shared/Curse.cs).
        internal static void Drive(bool active, CurseAddrs a, long weaponRecord, CurseState st)
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

            // Blood Price: 1 HP -> 1 WHP per second while in the native low-WHP warning state.
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
