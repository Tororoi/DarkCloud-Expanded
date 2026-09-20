using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Aga's Sword — +15 defense while equipped.</summary>
    internal static class AgasSword
    {
        // ── Aga's Sword "Defensive Legacy" ─────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Defensive Legacy (Aga's Sword)
        /// Aga's Sword: +15 defense to Toan while equipped.
        /// </summary>
        public static void AgasSwordEffect()
        {
            const int boost = 15;
            int baseDefense = Player.Toan.GetDefense();
            Player.Toan.SetDefense(baseDefense + boost);

            while (Player.Weapon.GetCurrentWeaponId() == Items.agassword && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                int current = Player.Toan.GetDefense();
                if (current != baseDefense + boost)
                {
                    baseDefense = current;
                    Player.Toan.SetDefense(baseDefense + boost);
                }
            }

            Player.Toan.SetDefense(Player.Toan.GetDefense() - boost);
        }
    }
}
