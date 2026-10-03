using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Javelin: while it is the active character's equipped weapon, every MARINE enemy on the floor (slot
    /// category 2) has no defense — both stats of its packed defense pair (slot +0x90: damage reduction, weapon defense) at 0 —
    /// and is worth twice its ABS (slot +0xB0, what the engine grants on the kill). Each slot is changed once, after it has
    /// been live two ticks (so the spawn-time stat normalizer and scaler have had their say), and the slots still live are put
    /// back on unequip, character switch or leaving the floor (Gladius's pattern). Super Steve carrying a Javelin SynthSphere has it
    /// too (<see cref="Wielded"/>).</summary>
    internal static class Javelin
    {
        private const string Tag = "[Javelin] ";
        private const ushort MarineCategory = 2;   // slot category (0 dragon, 1 undead, 2 marine, 3 rock, 4 plant, 5 beast, 6 sky, 7 metal, 8 mimic, 9 mage)
        private const int    AbsMult = 2;
        private const int    TickMs  = 250;
        private const int    SettleTicks = 2;      // live this many ticks before it is changed

        /// <summary>Ungaga with the Javelin, or Xiao with Super Steve and a Javelin sphere.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.javelin);

        public static void MarineEffect()
        {
            int n = EnemyAddresses.FloorSlots.Count;
            var defOriginal = new uint[n];
            var absOriginal = new int[n];
            var done = new bool[n];
            var liveTicks = new int[n];
            byte floor = 0xFF;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "marine enemies lose their defense and are worth double ABS while the Javelin is out");

            while (Wielded() && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                byte f = Memory.ReadByte(Addresses.checkFloor);
                if (f != floor) { floor = f; Array.Clear(done, 0, n); Array.Clear(liveTicks, 0, n); }
                if (Player.CheckDunIsPaused()) continue;

                for (int s = 0; s < n; s++)
                {
                    int render = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus));
                    if (render <= 0) { done[s] = false; liveTicks[s] = 0; continue; }   // empty/released slot → rearm
                    if (done[s] || ++liveTicks[s] < SettleTicks) continue;
                    done[s] = true;
                    if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.ResistancePack1)) != MarineCategory) continue;

                    long defAddr = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DefenseStats);
                    long absAddr = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs);
                    defOriginal[s] = Memory.ReadUInt(defAddr);
                    absOriginal[s] = Memory.ReadInt(absAddr);
                    Memory.WriteUInt(defAddr, 0);
                    Memory.WriteInt(absAddr, absOriginal[s] * AbsMult);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"marine enemy in slot {s}: defense {defOriginal[s] & 0xFFFF}/{defOriginal[s] >> 16} → 0/0, ABS {absOriginal[s]} → {absOriginal[s] * AbsMult}");
                }
            }

            // Unequip / character switch / dungeon exit: the live slots' own values back.
            if (Memory.ReadByte(Addresses.checkFloor) != floor) return;
            for (int s = 0; s < n; s++)
            {
                if (!done[s] || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) <= 0) continue;
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.ResistancePack1)) != MarineCategory) continue;
                Memory.WriteUInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DefenseStats), defOriginal[s]);
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs), absOriginal[s]);
            }
        }
    }
}
