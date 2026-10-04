using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Dark Cloud — Toan's hits cut straight through enemy guards.</summary>
    internal static class DarkCloud
    {
        /// <summary>
        /// Ability Name: Guard Crush (Dark Cloud)
        /// While Dark Cloud is wielded, Toan cuts through any enemy's guard — every hit lands even when the
        /// enemy is in its guard-motion frames. The enemy still animates its block; the hit simply connects and
        /// the damage flinch shatters it.
        ///
        /// Mechanism: a guarding enemy blocks a hit when its current motion frame is inside a registered guard window
        /// (EnemyAddresses.GuardWindows) — CheckDmg (0x1D9F10) then negates the hit. While Dark Cloud is in Toan's hands
        /// the ISO's guard gate is told that no window blocks (GuardGate.NobodyBlocks); with a sidekick out, paused,
        /// unequipped or off the floor, enemies block as their scripts set them.
        /// </summary>
        public static void GuardCrushEffect()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort dcEquipped = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (dcEquipped != Items.darkcloud && dcEquipped != Items.seventhheaven)  // 7th Heaven inherits Guard Crush
                    break;

                // No enemy guards Toan's hits while Dark Cloud is out; enemies guard again for sidekicks/pause.
                bool active = Player.CurrentCharacterNum() == Player.ToanId && !Player.CheckDunIsPaused();
                GuardGate.NobodyBlocks(active);
                Thread.Sleep(16);
            }

            // Unequipped / off the floor: enemies block normally again.
            GuardGate.NobodyBlocks(false);
        }

    }
}
