using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Crysknife — "Crystal Affinity": while it is in hand on a dungeon floor, every thrown gem's burst does twice its damage.
    /// The item-throw step computes a gem's damage (30 × the dungeon number + 1) and hands it to the burst; the gem-damage cave
    /// (DebugIfCave.GemDamage) multiplies it by CodeCaves.GemDamageFactor on the way — 2 here, 0 (vanilla) once the knife is put
    /// away. Super Steve carrying its SynthSphere has it too (<see cref="Wielded"/>); the knife's ownership passive on the magic
    /// circles (CircleBoost) is the owned weapon's alone and does not come with the sphere.</summary>
    internal static class CrysKnife
    {
        /// <summary>Whether the equipped weapon is the Crysknife, or Super Steve carrying its SynthSphere.</summary>
        internal static bool Wielded()
        {
            int id = Player.Weapon.GetCurrentWeaponId();
            if (id == Items.crystalknife) return true;
            return id == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.crystalknife;
        }

        private const int GemFactor = 2;
        private const int TickMs = 250;

        public static void CrystalAffinityEffect()
        {
            bool native = (uint)Memory.ReadInt(0x20000000L + 0x001D52D4) == (0x0C000000u | (CodeCaves.DebugIfCave.GemDamage >> 2));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + (native ? $"[Crysknife] crystal affinity: thrown gems do ×{GemFactor} damage" : "[Crysknife] crystal affinity: the gem-damage cave is not in this ISO — gems do vanilla damage (re-patch the ISO)"));
            while (Wielded() && Player.InDungeonFloor())
            {
                if (Memory.ReadInt(CodeCaves.GemDamageFactor) != GemFactor) Memory.WriteInt(CodeCaves.GemDamageFactor, GemFactor);   // re-asserted: fresh memory reads 0
                Thread.Sleep(TickMs);
            }
            Memory.WriteInt(CodeCaves.GemDamageFactor, 0);                            // put away, or off the floor: vanilla
        }
    }
}
