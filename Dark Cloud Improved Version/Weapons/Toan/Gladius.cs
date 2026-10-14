using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Gladius — "Jacket Hunter": a Master Jacket killed while the Gladius is in hand gives four times its ABS. Super
    /// Steve carrying its SynthSphere has it too (<see cref="Wielded"/>).</summary>
    internal static class Gladius
    {
        /// <summary>Whether the equipped weapon is the Gladius, or Super Steve carrying its SynthSphere.</summary>
        internal static bool Wielded() => Player.Weapon.GetCurrentWeaponId() == Items.gladius || SuperSteve.Wields(Items.gladius);

        private const int AbsMult = 4;      // the Master Jacket's kill-ABS multiplier (applied once per slot)
        private const int TickMs  = 250;    // nothing here is latency-critical: one write per slot, asserted

        private static readonly int N = EnemyAddresses.FloorSlots.Count;
        private static readonly int[] _absOriginal = new int[N];
        private static readonly bool[] _boosted = new bool[N];
        private static byte _floor = 0xFF;
        private static bool _on;
        private static long _due;            // the next pass (Environment.TickCount64): a pass every TickMs, however often it is called

        /// <summary>A pass every <see cref="TickMs"/>: every Master Jacket slot's kill-ABS value (slot +0xB0, what the engine grants on the
        /// kill) is multiplied ONCE as the slot appears on the floor — both the stock and the Enhanced Master Jacket, which share species
        /// id 1. Write-once, so a miniboss's own multiplier (applied at spawn) is simply built on. <paramref name="active"/> false holds.</summary>
        internal static void Drive(bool active)
        {
            _on = true;
            long now = Environment.TickCount64;
            if (now < _due) return;
            _due = now + TickMs;
            byte f = Memory.ReadByte(Addresses.checkFloor);
            if (f != _floor) { _floor = f; Array.Clear(_boosted, 0, N); }
            if (!active) return;
            ushort jacket = EnemySpecies.MasterJacket.Id;
            for (int h = 0; h < N; h++)
            {
                int render = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus));
                if (render <= 0) { _boosted[h] = false; continue; }          // empty/released slot → rearm
                if (_boosted[h]) continue;
                if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.EnemySpeciesId)) != jacket) continue;

                long absAddr = EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs);
                _absOriginal[h] = Memory.ReadInt(absAddr);
                Memory.WriteInt(absAddr, _absOriginal[h] * AbsMult);
                _boosted[h] = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Gladius] Master Jacket in slot {h}: ABS {_absOriginal[h]} → {_absOriginal[h] * AbsMult}");
            }
        }

        /// <summary>Unequip, sphere change, character switch or leaving the floor: the live slots' own values back.</summary>
        internal static void Stop()
        {
            if (!_on) return;
            _on = false; _due = 0;
            if (Memory.ReadByte(Addresses.checkFloor) == _floor)
                for (int h = 0; h < N; h++)
                    if (_boosted[h] && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus)) > 0)
                        Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs), _absOriginal[h]);
            Array.Clear(_boosted, 0, N);
            _floor = 0xFF;
        }

        /// <summary>Toan's Gladius thread: hands every tick to <see cref="Drive"/> while it is in hand (Super Steve's loop drives its sphere's).</summary>
        public static void JacketHunterEffect()
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
