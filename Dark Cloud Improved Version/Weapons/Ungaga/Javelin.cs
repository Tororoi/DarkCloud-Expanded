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

        private static readonly int N = EnemyAddresses.FloorSlots.Count;
        private static readonly uint[] _defOriginal = new uint[N];
        private static readonly int[] _absOriginal = new int[N], _liveTicks = new int[N];
        private static readonly bool[] _done = new bool[N];
        private static byte _floor = 0xFF;
        private static bool _on;
        private static long _due;            // the next pass (Environment.TickCount64): a pass every TickMs, however often it is called

        /// <summary>A pass every <see cref="TickMs"/>: each marine slot live for <see cref="SettleTicks"/> passes loses its defense and doubles
        /// its ABS, once. <paramref name="active"/> false holds (the floor change is still noticed).</summary>
        internal static void Drive(bool active)
        {
            if (!_on)
            {
                _on = true; _floor = 0xFF;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "marine enemies lose their defense and are worth double ABS while the Javelin is out");
            }
            long now = Environment.TickCount64;
            if (now < _due) return;
            _due = now + TickMs;
            byte f = Memory.ReadByte(Addresses.checkFloor);
            if (f != _floor) { _floor = f; Array.Clear(_done, 0, N); Array.Clear(_liveTicks, 0, N); }
            if (!active) return;
            for (int s = 0; s < N; s++)
            {
                int render = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus));
                if (render <= 0) { _done[s] = false; _liveTicks[s] = 0; continue; }   // empty/released slot → rearm
                if (_done[s] || ++_liveTicks[s] < SettleTicks) continue;
                _done[s] = true;
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.ResistancePack1)) != MarineCategory) continue;

                long defAddr = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DefenseStats);
                long absAddr = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs);
                _defOriginal[s] = Memory.ReadUInt(defAddr);
                _absOriginal[s] = Memory.ReadInt(absAddr);
                Memory.WriteUInt(defAddr, 0);
                Memory.WriteInt(absAddr, _absOriginal[s] * AbsMult);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"marine enemy in slot {s}: defense {_defOriginal[s] & 0xFFFF}/{_defOriginal[s] >> 16} → 0/0, ABS {_absOriginal[s]} → {_absOriginal[s] * AbsMult}");
            }
        }

        /// <summary>Unequip, sphere change, character switch or leaving the floor: the live slots' own values back.</summary>
        internal static void Stop()
        {
            if (!_on) return;
            _on = false; _due = 0;
            if (Memory.ReadByte(Addresses.checkFloor) != _floor) return;
            for (int s = 0; s < N; s++)
            {
                if (!_done[s] || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) <= 0) continue;
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.ResistancePack1)) != MarineCategory) continue;
                Memory.WriteUInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DefenseStats), _defOriginal[s]);
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs), _absOriginal[s]);
            }
            Array.Clear(_done, 0, N);
        }

        /// <summary>Ungaga's Javelin thread: hands every tick to <see cref="Drive"/> while it is in hand (Super Steve's loop drives its sphere's).</summary>
        public static void MarineEffect()
        {
            while (Wielded() && Player.InDungeonFloor())
            {
                Drive(!Player.CheckDunIsPaused());
                Thread.Sleep(TickMs);
            }
            Stop();
        }
    }
}
