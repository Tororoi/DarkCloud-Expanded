using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Bandit's Ring — Steal Shot, inherited from Xiao's Bandit Slingshot: a steal that lands on an enemy with a projectile
    /// takes the projectile, and every shot Ruby fires until the next steal or the floor's end is that enemy's. The mechanism is the
    /// slingshot's (<see cref="BanditSlingshot.Drive"/>); this thread only runs it while the ring is in Ruby's hand.</summary>
    internal static class BanditsRing
    {
        public static void StealShotEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.banditsring)
            {
                BanditSlingshot.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            BanditSlingshot.Stop();
        }
    }
}
