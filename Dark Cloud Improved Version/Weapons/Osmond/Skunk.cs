using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Skunk — the flamethrower reaches twice as far. Osmond's flame gun mode (gun type 2 — the Blessing Gun and the Skunk;
    /// BattleActionPlay_Ozumond_F) is a CSHOT_FIREBAR of 24 flame particles laid along the aim, each 2 units past the last
    /// (Init places them, Set re-aims them every frame), so the flame reaches 46 units. The ISO's ELF patch makes both read the
    /// spacing from <see cref="Mailbox.FlameSpacing"/> (pnach-seeded 2.0 while nobody owns it); the driver owns the word and
    /// holds it at <see cref="Spacing"/> while the Skunk is equipped, 2.0 back when it goes. The particles' own collision
    /// spheres (radius 4, one plant each per 30 frames) and life are the game's, so the longer flame stays continuous.
    /// </summary>
    internal static class Skunk
    {
        private const string Tag = "[Skunk] ";
        private const float VanillaSpacing = 2f, Spacing = 4f;
        private static bool _held, _nativeWarned;

        private static bool Native
        {
            get
            {
                uint hi = 0x3C020000u | (uint)((Mailbox.FlameSpacing - 0x20000000) >> 16);
                return (uint)Memory.ReadInt(0x20000000L + 0x001AED88) == hi;
            }
        }

        /// <summary>Drive every tick while the Skunk is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!Native)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the flamethrower-spacing patch is not in this ISO — vanilla reach (re-patch the ISO)"); }
                return;
            }
            if (Memory.ReadFloat(Mailbox.FlameSpacing) == Spacing) return;
            Memory.WriteInt(Mailbox.FlameSpacingOwner, 1);   // ours: the PNACH stops re-seeding
            Memory.WriteFloat(Mailbox.FlameSpacing, Spacing);
            if (!_held) { _held = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"flamethrower reach ×{Spacing / VanillaSpacing:F0}"); }
        }

        /// <summary>The weapon or the floor went: the vanilla spacing back.</summary>
        internal static void Stop()
        {
            if (!_held) return;
            _held = false;
            if (Memory.ReadFloat(Mailbox.FlameSpacing) == Spacing) Memory.WriteFloat(Mailbox.FlameSpacing, VanillaSpacing);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "flamethrower reach released");
        }

        /// <summary>Osmond's Skunk thread: hands every tick to <see cref="Drive"/> while the weapon is equipped, and
        /// stands it down once when it goes.</summary>
        public static void LongerFlameEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.skunk)
            {
                Drive(!Player.CheckDunIsPaused());
                Thread.Sleep(16);
            }
            Stop();
        }
    }
}
