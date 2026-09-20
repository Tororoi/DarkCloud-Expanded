using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Dark Cloud — Toan's hits cut straight through enemy guards.</summary>
    internal static class DarkCloud
    {
        // ── Dark Cloud "Guard Crush" ───────────────────────────────────────────────────────
        // Per-slot snapshot of each enemy's original guard-window flags (captured on first sight) so they can
        // be restored when Dark Cloud is put away — the flag is armed once at spawn, so if we didn't restore,
        // enemies would stay unguarded (until they respawn) even after switching weapons. Keyed by species id
        // to catch slot reuse.
        private static readonly ushort[,] _dcGuardSnap = new ushort[EnemyAddresses.FloorSlots.Count, EnemyAddresses.GuardWindows.WindowCount];

        private static readonly ushort[] _dcGuardSpecies = new ushort[EnemyAddresses.FloorSlots.Count];

        private static readonly bool[] _dcGuardCaptured = new bool[EnemyAddresses.FloorSlots.Count];

        private static byte _dcLastFloor = 0xFF;

        /// <summary>
        /// Ability Name: Guard Crush (Dark Cloud)
        /// While Dark Cloud is wielded, Toan cuts through any enemy's guard — every hit lands even when the
        /// enemy is in its guard-motion frames. The enemy still animates its block; the hit simply connects and
        /// the damage flinch shatters it.
        ///
        /// Mechanism (see EnemyAddresses.GuardWindows): a guarding enemy blocks a hit when its current motion
        /// frame is inside a registered guard window and that window's flag (MMU+slot*0x20+0x60550) is set —
        /// CheckDmg (0x1D9F10) then negates the hit. The flag is written ONCE at spawn, so this watcher zeroes it
        /// data-side for every active enemy while Dark Cloud is in Toan's hands, and restores the captured
        /// original when it isn't (sidekick out, paused, unequipped, or dungeon exit) so enemies block normally
        /// with other weapons. No code patch; the frame values are left untouched.
        /// </summary>
        public static void DarkCloudEffect()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort dcEquipped = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (dcEquipped != Items.darkcloud && dcEquipped != Items.seventhheaven)  // 7th Heaven inherits Guard Crush
                    break;

                byte floor = Memory.ReadByte(Addresses.checkFloor);
                if (floor != _dcLastFloor)
                {
                    _dcLastFloor = floor;
                    for (int s = 0; s < _dcGuardCaptured.Length; s++) _dcGuardCaptured[s] = false;
                }

                // No enemy guards Toan's hits while Dark Cloud is out; hand off (restore guards) for sidekicks/pause.
                bool active = Player.CurrentCharacterNum() == Player.ToanId && !Player.CheckDunIsPaused();
                DarkCloudDriveGuards(active);
                Thread.Sleep(16);
            }

            // Restore every captured guard on unequip / dungeon exit so enemies block normally again.
            DarkCloudDriveGuards(false);
        }

        internal static void DarkCloudDriveGuards(bool breakGuard)
        {
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) < 1)
                {
                    _dcGuardCaptured[slot] = false;   // slot went inactive — re-capture whoever spawns next
                    continue;
                }
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                if (!_dcGuardCaptured[slot] || _dcGuardSpecies[slot] != species)
                {
                    // Capture the ORIGINAL flags before we ever zero them (guard armed at spawn, so first sight = original).
                    for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                        _dcGuardSnap[slot, w] = Memory.ReadUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w));
                    _dcGuardSpecies[slot] = species;
                    _dcGuardCaptured[slot] = true;
                }
                for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                {
                    ushort want = breakGuard ? (ushort)0 : _dcGuardSnap[slot, w];
                    long addr = EnemyAddresses.GuardWindows.FlagAddr(slot, w);
                    if (Memory.ReadUShort(addr) != want) Memory.WriteUShort(addr, want);
                }
            }
        }
    }
}
