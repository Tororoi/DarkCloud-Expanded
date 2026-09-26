using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Crysknife and Magical Hammer — "Circle Amplifier", an ownership passive the two share. While either is owned
    /// (in any hand, bag or storage) every magic circle's effect is doubled, the good and the bad alike; while BOTH are owned
    /// it is tripled. Any number of one weapon is still one weapon: two Crysknives double, a Crysknife and a Magical Hammer
    /// triple. The attack-doubling lasts 60 / 90 s, the max-WHP rolls run 6–10 / 9–15 each way, the stat and element
    /// losses double / triple, enemies rage (or, favoured, slow) for 10 / 15 s, the gilda loss is two / three fifths, the
    /// WHP-quartering leaves 1, and the two "to max" circles drop one / two powders into the bag — Powerup for ABS-full,
    /// Auto Repair for WHP-cure — while there is room. The figures are MagicCircles.Boosted's; the ISO's circle cave
    /// applies them.</summary>
    internal static class CircleBoost
    {
        private static DateTime _next;
        private static int _level = -1;

        /// <summary>From the global tick, self-gated to 1 Hz (ownership changes in menus, so it runs in every mode).</summary>
        public static void CircleAmplifierEffect()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _next) return;
            _next = now.AddSeconds(1);
            int level = (WeaponOwnership.Owned(Items.crystalknife) ? 1 : 0) + (WeaponOwnership.Owned(Items.magicalhammer) ? 1 : 0);
            if (level == 0) MagicCircles.Vanilla(); else MagicCircles.Set(MagicCircles.Boosted(level + 1));
            if (level != _level)
            {
                _level = level;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + (level == 0 ? "[CircleBoost] neither owned: the magic circles are the game's own"
                    : $"[CircleBoost] {(level == 1 ? "one" : "both")} of Crysknife / Magical Hammer owned: the magic circles are ×{level + 1}"));
            }
        }
    }
}
