using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Secret Armlet — "Favoured Circles", an ownership passive: while the armlet is owned (in any hand, bag or
    /// storage) every magic circle turns out well. The ISO's circle
    /// cave (tools/stubs/circle_effects.s) deals the bad circles as good ones when the circle table's favour word is set:
    /// the gilda loss pays out as the gilda gain, the max-WHP loss as the max-WHP gain, the WHP loss as the WHP cure, the
    /// stat and element losses land as gains of the same roll, and the enemy-rage circle slows every enemy instead. The
    /// magnitudes stay whatever the table holds (MagicCircles), so the Crysknife / Magical Hammer boost applies to these too.</summary>
    internal static class SecretArmlet
    {
        private static DateTime _next;
        private static bool _on;

        /// <summary>From the global tick, self-gated to 1 Hz: the favour word follows the armlet's ownership.</summary>
        public static void FavouredCirclesEffect()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _next) return;
            _next = now.AddSeconds(1);
            bool on = WeaponOwnership.Owned(Items.secretarmlet);
            MagicCircles.Favour(on);
            if (on != _on)
            {
                _on = on;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + (on ? "[SecretArmlet] owned: the magic circles are favoured" : "[SecretArmlet] gone: the magic circles are the game's own"));
            }
        }
    }
}
