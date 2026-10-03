using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Scorpion — every time its poison takes hold of an enemy (the weapon's own Poison ability, rolled by the engine in
    /// CheckDmg), two things follow at once: the wielder's own poison is drawn out (the Poison bit cleared from their status
    /// word, as Brave Ark clears it), and the weapon soaks up HALF the enemy's ABS worth (its slot's ABS, +0xB0), added to the
    /// equipped weapon's ABS up to that weapon's maximum (MachoSword.MachoMaxExp, the engine's GetWeaponMaxExp). Super Steve
    /// carrying a Scorpion SynthSphere has both (<see cref="Wielded"/>).
    ///
    /// The proc is seen as an enemy slot's poison timer (+0x0C, 0 at rest) going from zero to non-zero while the weapon is
    /// out — the engine sets it the frame its poison roll succeeds; the slot is watched again once the timer has run back to 0.</summary>
    internal static class ScorpionVenom
    {
        private const string Tag = "[Scorpion] ";
        private const int TickMs = 16;
        private static readonly bool[] _poisoned = new bool[EnemyAddresses.FloorSlots.Count];

        /// <summary>Ungaga with the Scorpion, or Xiao with Super Steve and a Scorpion sphere.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.scorpion);

        public static void VenomEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "venom: its poison landing cures the wielder's and feeds the weapon half the enemy's ABS");
            for (int s = 0; s < _poisoned.Length; s++) _poisoned[s] = Poisoned(s);   // already poisoned when it came out: not this weapon's
            while (Wielded() && Player.InDungeonFloor())
            {
                if (!Player.CheckDunIsPausedOrMenu()) Tick();
                Thread.Sleep(TickMs);
            }
        }

        private static bool Poisoned(int slot) => Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.PoisonPeriod)) != 0;

        private static void Tick()
        {
            for (int s = 0; s < _poisoned.Length; s++)
            {
                bool now = Enemies.IsLive(s) && Poisoned(s);
                if (now && !_poisoned[s]) Landed(s);
                _poisoned[s] = now;
            }
        }

        /// <summary>The poison took hold of enemy <paramref name="slot"/>: the wielder cured, the weapon fed.</summary>
        private static void Landed(int slot)
        {
            int ch = Player.CurrentCharacterNum();
            int statusAddr = ch == Player.XiaoId ? Player.Xiao.status : Player.Ungaga.status;
            ushort status = Memory.ReadUShort(statusAddr);
            bool cured = (status & ToanState.StatusPoison) != 0;
            if (cured) Memory.WriteUShort(statusAddr, (ushort)(status & ~ToanState.StatusPoison));

            int worth = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Abs));
            int gain = worth / 2, before = -1, after = -1;
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(ch));
            if (gain > 0 && bag >= 0 && bag < DngStatusData.MaxWeaponSlots)
            {
                long rec = DngStatusData.WeaponRecord(ch, bag);
                int id = Memory.ReadUShort(rec);
                if (!MachoSword.MachoIsAbslessWeapon(id))
                {
                    int max = MachoSword.MachoMaxExp(id, Memory.ReadShort(rec + 0x02));
                    before = Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset);
                    after = Math.Max(before, Math.Min(max, before + gain));               // never past the weapon's maximum (nor back from above it)
                    if (after != before) Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)after);
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"poison took hold of enemy slot {slot}" + (cured ? ": the wielder's poison drawn out" : "")
                              + (after >= 0 ? $"; ABS {before} → {after} (+{after - before} of half its {worth})" : ""));
        }
    }
}
