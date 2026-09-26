using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Gladius — "Jacket Hunter": a Master Jacket killed while the Gladius is in hand gives four times its ABS.</summary>
    internal static class Gladius
    {
        private const int AbsMult = 4;      // the Master Jacket's kill-ABS multiplier (applied once per slot)
        private const int TickMs  = 250;    // nothing here is latency-critical: one write per slot, asserted

        /// <summary>Every Master Jacket slot's kill-ABS value (slot +0xB0, what the engine grants on the kill) is
        /// multiplied ONCE as the slot appears on the floor — both the stock and the Enhanced Master Jacket, which
        /// share species id 1 — and put back for the slots still live on unequip. Write-once, so a miniboss's own
        /// multiplier (applied at spawn) is simply built on.</summary>
        public static void JacketHunterEffect()
        {
            int n = EnemyAddresses.FloorSlots.Count;
            var absOriginal = new int[n];
            var boosted = new bool[n];
            byte floor = 0xFF;
            ushort jacket = EnemySpecies.MasterJacket.Id;

            while (Player.Weapon.GetCurrentWeaponId() == Items.gladius && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                byte f = Memory.ReadByte(Addresses.checkFloor);
                if (f != floor) { floor = f; Array.Clear(boosted, 0, n); }
                if (Player.CheckDunIsPaused()) continue;

                for (int h = 0; h < n; h++)
                {
                    int render = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus));
                    if (render <= 0) { boosted[h] = false; continue; }          // empty/released slot → rearm
                    if (boosted[h]) continue;
                    if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.EnemySpeciesId)) != jacket) continue;

                    long absAddr = EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs);
                    absOriginal[h] = Memory.ReadInt(absAddr);
                    Memory.WriteInt(absAddr, absOriginal[h] * AbsMult);
                    boosted[h] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Gladius] Master Jacket in slot {h}: ABS {absOriginal[h]} → {absOriginal[h] * AbsMult}");
                }
            }

            // Unequip / character switch / dungeon exit: the live slots' own values back.
            if (Memory.ReadByte(Addresses.checkFloor) == floor)
                for (int h = 0; h < n; h++)
                    if (boosted[h] && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus)) > 0)
                        Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs), absOriginal[h]);
        }
    }
}
