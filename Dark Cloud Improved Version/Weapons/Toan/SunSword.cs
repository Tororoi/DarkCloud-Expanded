using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sun Sword — every enemy killed has a chance to drop a Sun attachment (Big Bang inherits).</summary>
    internal static class SunSword
    {
        private static readonly Random random = new Random();

        // ── Sun Sword "Solar Harvest" ──────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Solar Harvest (Sun Sword, Big Bang)
        /// While the Sun Sword — or its evolution, Big Bang — is wielded, each enemy on the floor
        /// has a 1% chance (rolled once per slot per floor) to drop a Sun attachment instead of
        /// its regular drop when killed. (Big Bang shares this effect for lineage/visual-design
        /// reasons; it will additionally get its own unique effect later.)
        /// Implemented by pre-staging the engine's guaranteed-drop field (<see
        /// cref="EnemySlotOffsets.ForceItemDrop"/> — the same mechanism dungeon keys and miniboss
        /// loot use, consumed by the death-drop block in CMonstorUnit::Step, ELF 0x1DF4C0) while
        /// the sword is in hand, and un-staging when it isn't (unequip, character switch), so
        /// there is no race against the killing blow. Slots already carrying a forced drop
        /// (dungeon key, miniboss loot, mimic key) are never touched. Engine caveat: the drop
        /// path runs items through a small de-dupe set, so a second Sun proc on the same floor
        /// may be swallowed.
        /// </summary>
        public static void SunSwordEffect()
        {
            var st = new SunHarvestState(EnemyAddresses.FloorSlots.Count);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (equippedId != Items.sunsword && equippedId != Items.bigbang)
                    break;

                // Kills while a sidekick is out aren't Sun Sword kills — winners keep their win
                // (state is preserved) and are re-staged when Toan takes over again.
                SunHarvestDrive(Player.CurrentCharacterNum() == Player.ToanId, st);
                Thread.Sleep(250);
            }
            SunHarvestDrive(false, st);   // unequipped / left the floor: revert anything still staged
        }

        // Solar Harvest core — shared with Super Steve (an attached Sun Sword / Big Bang sphere).
        // Per-caller state (no shared statics) so the Toan thread and the Xiao thread never fight
        // over the staged slots. `active` = the Sun wielder is currently the acting character.
        internal sealed class SunHarvestState
        {
            public byte LastFloor = 0xFF;
            public readonly bool[] Rolled, Winner, Staged;
            public readonly ushort[] OriginalDrop;
            public SunHarvestState(int n)
            { Rolled = new bool[n]; Winner = new bool[n]; Staged = new bool[n]; OriginalDrop = new ushort[n]; }
        }

        internal static void SunHarvestDrive(bool active, SunHarvestState st)
        {
            const int procPercent = 1;
            int slotCount = EnemyAddresses.FloorSlots.Count;

            byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
            if (currentFloor != st.LastFloor)
            {
                // New floor: the slot array was reinitialized, so forget everything WITHOUT
                // restoring (writing stale values into fresh slots would corrupt them).
                st.LastFloor = currentFloor;
                Array.Clear(st.Rolled, 0, slotCount);
                Array.Clear(st.Winner, 0, slotCount);
                Array.Clear(st.Staged, 0, slotCount);
            }

            if (!active) { SunUnstageAll(st); return; }

            for (int i = 0; i < slotCount; i++)
            {
                if (Enemies.GetFloorEnemyId(i) == 0) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) <= 0) continue;

                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                ushort dropVal = Memory.ReadUShort(dropAddr);

                if (!st.Rolled[i])
                {
                    st.Rolled[i] = true;
                    // Slots that already carry a forced drop (key/miniboss/mimic) are off-limits
                    if (dropVal != 0 && dropVal != 65535) continue;
                    st.Winner[i] = random.Next(100) < procPercent;
                    if (st.Winner[i]) st.OriginalDrop[i] = dropVal;
                }
                if (!st.Winner[i] || st.Staged[i]) continue;

                // Re-check occupancy — a key could have been assigned here after our roll
                if (dropVal != 0 && dropVal != 65535 && dropVal != Items.sun)
                { st.Winner[i] = false; continue; }
                Memory.WriteUShort(dropAddr, (ushort)Items.sun);
                st.Staged[i] = true;
            }
        }

        private static void SunUnstageAll(SunHarvestState st)
        {
            for (int i = 0; i < st.Staged.Length; i++)
            {
                if (!st.Staged[i]) continue;
                st.Staged[i] = false;
                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                if (Memory.ReadUShort(dropAddr) == Items.sun)   // still ours → restore
                    Memory.WriteUShort(dropAddr, st.OriginalDrop[i]);
            }
        }
    }
}
