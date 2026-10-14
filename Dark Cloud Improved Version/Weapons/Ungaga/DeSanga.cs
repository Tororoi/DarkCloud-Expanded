using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>DeSanga: every enemy the active character kills while it is the equipped weapon heals it <see cref="HealPerKill"/>
    /// WHP — the weapon record's current WHP (+0x10, float) raised up to its maximum (+0x0C). A kill is an enemy slot's HP crossing
    /// to 0 while it is still in its death animation, credited to the active character (the slot's killer id) — Macho Sword's
    /// kill test. Super Steve carrying a DeSanga SynthSphere has it too, healing Super Steve (<see cref="Wielded"/>).</summary>
    internal static class DeSanga
    {
        private const string Tag = "[DeSanga] ";
        private const float  HealPerKill = 5f;
        private const int    TickMs = 16;

        /// <summary>Ungaga with DeSanga, or Xiao with Super Steve and a DeSanga sphere.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.desanga);

        private static int[] _prevHp;   // each floor slot's HP last tick (null = not running)

        /// <summary>One tick: every death crossing since the last one that is the wielder's kill heals the weapon. The first tick only
        /// takes each slot's HP, so an enemy already dying when it came out is not counted. <paramref name="active"/> false holds.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            int n = EnemyAddresses.FloorSlots.Count;
            if (_prevHp == null)
            {
                _prevHp = new int[n];
                for (int s = 0; s < n; s++) _prevHp[s] = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"every kill heals the weapon {HealPerKill:F0} WHP");
                return;
            }
            int ch = Player.CurrentCharacterNum(), kills = 0;
            for (int s = 0; s < n; s++)
            {
                int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                int prev = _prevHp[s]; _prevHp[s] = hp;
                if (prev <= 0 || hp > 0) continue;                                                            // no death crossing
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) < 1) continue;   // not a death on the floor
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.KillerCharId)) != ch) continue; // not the wielder's kill
                kills++;
            }
            if (kills > 0) Heal(ch, kills * HealPerKill);
        }

        /// <summary>The weapon, the sphere or the floor went.</summary>
        internal static void Stop() => _prevHp = null;

        /// <summary>Ungaga's DeSanga thread: hands every tick to <see cref="Drive"/> while it is in hand (Super Steve's loop drives its sphere's).</summary>
        public static void VampireEffect()
        {
            while (Wielded() && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                Drive(!Player.CheckDunIsPausedOrMenu());
            }
            Stop();
        }

        private static void Heal(int ch, float amount)
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(ch));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(ch, bag);
            float max = Memory.ReadShort(rec + WeaponHave.InventoryWeaponMaxWhpOffset);
            float whp = Memory.ReadFloat(rec + WeaponHave.InventoryWeaponWhpOffset);
            float healed = Math.Max(whp, Math.Min(max, whp + amount));
            if (healed == whp) return;
            Memory.WriteFloat(rec + WeaponHave.InventoryWeaponWhpOffset, healed);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"kill heal: WHP {whp:F1} → {healed:F1} of {max:F0}");
        }
    }
}
