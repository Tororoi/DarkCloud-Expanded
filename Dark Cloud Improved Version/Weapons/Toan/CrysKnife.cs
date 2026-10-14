using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Crysknife — "Crystal Affinity": while it is in hand on a dungeon floor, every thrown gem's burst does twice its damage.
    /// The item-throw step computes a gem's damage (30 × the dungeon number + 1) and hands it to the burst; the gem-damage cave
    /// (DebugIfCave.GemDamage) multiplies it by CodeCaves.GemDamageFactor on the way — 2 here, 0 (vanilla) once the knife is put
    /// away. Super Steve carrying its SynthSphere has it too (<see cref="Wielded"/>).
    ///
    /// And "Circle Amplifier" (<see cref="CircleAmplifierEffect"/>), an ownership passive the knife shares with Goro's Magical
    /// Hammer: while either is owned (in any hand, bag or storage) every magic circle's effect is doubled, the good and the bad
    /// alike; while BOTH are owned it is tripled. Any number of one weapon is still one weapon: two Crysknives double, a Crysknife
    /// and a Magical Hammer triple. The attack-doubling lasts 60 / 90 s, the max-WHP rolls run 6–10 / 9–15 each way, the stat and
    /// element losses double / triple, enemies rage (or, favoured, slow) for 10 / 15 s, the gilda loss is two / three fifths, the
    /// WHP-quartering leaves 1, and the two "to max" circles drop one / two powders into the bag — Powerup for ABS-full, Auto
    /// Repair for WHP-cure — while there is room. The figures are MagicCircles.Boosted's; the ISO's circle cave applies them. It is
    /// the owned weapon's alone and does not come with the sphere.</summary>
    internal static class CrysKnife
    {
        private const string Tag = "[Crysknife] ";

        /// <summary>Whether the equipped weapon is the Crysknife, or Super Steve carrying its SynthSphere.</summary>
        internal static bool Wielded() => Player.Weapon.GetCurrentWeaponId() == Items.crystalknife || SuperSteve.Wields(Items.crystalknife);

        private const int GemFactor = 2;
        private const int TickMs = 250;

        private static bool _on;
        private static long _due;            // the next assert (Environment.TickCount64): one every TickMs, however often it is called

        /// <summary>Keeps CodeCaves.GemDamageFactor at <see cref="GemFactor"/>, re-asserted every <see cref="TickMs"/> (fresh memory reads 0).
        /// A hold changes nothing: the factor only matters when a gem is thrown.</summary>
        internal static void Drive()
        {
            if (!_on)
            {
                _on = true;
                bool native = (uint)Memory.ReadInt(0x20000000L + 0x001D52D4) == (0x0C000000u | (DebugIfCave.GemDamage >> 2));
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (native ? $"crystal affinity: thrown gems do ×{GemFactor} damage" : "crystal affinity: the gem-damage cave is not in this ISO — gems do vanilla damage (re-patch the ISO)"));
            }
            long now = Environment.TickCount64;
            if (now < _due) return;
            _due = now + TickMs;
            if (Memory.ReadInt(CodeCaves.GemDamageFactor) != GemFactor) Memory.WriteInt(CodeCaves.GemDamageFactor, GemFactor);
        }

        /// <summary>Put away, the sphere changed, or off the floor: vanilla gem damage.</summary>
        internal static void Stop()
        {
            if (!_on) return;
            _on = false; _due = 0;
            Memory.WriteInt(CodeCaves.GemDamageFactor, 0);
        }

        /// <summary>Toan's Crysknife thread: hands every tick to <see cref="Drive"/> while it is in hand (Super Steve's loop drives its sphere's).</summary>
        public static void CrystalAffinityEffect()
        {
            while (Wielded() && Player.InDungeonFloor())
            {
                Drive();
                Thread.Sleep(TickMs);
            }
            Stop();
        }

        // ── Circle Amplifier (the Crysknife's and the Magical Hammer's ownership passive) ──
        private static DateTime _circleNext;
        private static int _circleLevel = -1;

        /// <summary>From the global tick (WeaponPassives), self-gated to 1 Hz (ownership changes in menus, so it runs in every mode):
        /// the magic circle table ×2 while one of the Crysknife / Magical Hammer is owned, ×3 while both are, vanilla while neither.</summary>
        public static void CircleAmplifierEffect()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _circleNext) return;
            _circleNext = now.AddSeconds(1);
            int level = (WeaponOwnership.Owned(Items.crystalknife) ? 1 : 0) + (WeaponOwnership.Owned(Items.magicalhammer) ? 1 : 0);
            if (level == 0) MagicCircles.Vanilla(); else MagicCircles.Set(MagicCircles.Boosted(level + 1));
            if (level != _circleLevel)
            {
                _circleLevel = level;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (level == 0 ? "circle amplifier: neither Crysknife nor Magical Hammer owned — the magic circles are the game's own"
                    : $"circle amplifier: {(level == 1 ? "one" : "both")} of Crysknife / Magical Hammer owned — the magic circles are ×{level + 1}"));
            }
        }
    }
}
