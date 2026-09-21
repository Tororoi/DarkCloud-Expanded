using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Evilcise — Jealous Soul: Toan is cursed while it is equipped and immune to every other status; breaking the
    /// curse with holy water poisons him and sets HP to 1; reapplied each floor. Super Steve drives the same for Xiao.</summary>
    internal static class Evilcise
    {
        /// <summary>
        /// Ability Name: Jealous Soul (Evilcise)
        /// Evilcise effect: Toan is cursed while equipped and immune to all other status effects.
        /// Breaking the curse with holy water applies poison and sets HP to 1.
        /// The curse is reapplied on each new floor.
        /// </summary>
        public static void JealousSoulEffect()
        {
            var st = new CurseState();
            var target = new CurseAddrs(ToanState.Status, ToanState.StatusTimer, ToanState.Hp);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id + toanSlot * WeaponHave.InventoryWeaponSlotStride) != Items.evilcise)
                    break;
                Drive(true, target, st);
                Thread.Sleep(100);
            }
            Drive(false, target, st);   // strip curse on unequip / dungeon exit
        }

        // Jealous Soul (Evilcise): curse the wielder (immune to other statuses); breaking the curse
        // with holy water applies Poison + sets HP to 1; reapplied each floor. `active` = the wielder
        // is the acting character with the weapon in hand; pass false to strip on unequip / switch.
        internal static void Drive(bool active, CurseAddrs a, CurseState st)
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
    }
}
