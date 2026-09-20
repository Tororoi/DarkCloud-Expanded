using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Brave Ark — resists Freeze, Poison, Curse and Goo.</summary>
    internal static class BraveArk
    {
        // ── Brave Ark "Hero's Courage" ─────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Hero's Courage (Brave Ark)
        /// Brave Ark: resists Freeze, Poison, Curse, and Goo status effects while equipped.
        /// Clears any of those statuses within the polling interval.
        /// </summary>
        public static void BraveArkEffect()
        {
            const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                      ToanState.StatusCurse  | ToanState.StatusGoo;

            while (Player.Weapon.GetCurrentWeaponId() == Items.braveark && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                ushort status = Memory.ReadUShort(ToanState.Status);
                if ((status & resistMask) != 0)
                    Memory.WriteUShort(ToanState.Status, (ushort)(status & ~resistMask));
            }
        }
    }
}
