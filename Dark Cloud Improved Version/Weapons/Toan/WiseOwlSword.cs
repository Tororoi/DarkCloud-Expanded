using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Wise Owl Sword — while owned, a message tells the floor's secrets.</summary>
    internal static class WiseOwlSword
    {
        // ── Wise Owl Sword "Wise Owl Always Knows" ─────────────────────────────────────────
        /// <summary>
        /// Ability Name: Wise Owl Always Knows (Wise Owl Sword)
        /// Wise Owl Sword passive: displays a message when an enemy holding a WOF key is nearby,
        /// provided the player owns a Wise Owl Sword anywhere (bag, storage, or equipped).
        /// </summary>
        public static void WiseOwlSwordEffect()
        {
            const float maxKeyDetectionRange = 500f;

            byte lastFloor = 0xFF;
            bool floorMessageSent = false;
            int lastNearestSlot = -1;
            bool wasOutOfRange = true;

            while (Memory.ReadByte(Addresses.checkDungeon) == 1 && Player.InDungeonFloor())
            {
                Thread.Sleep(200);

                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    lastFloor = currentFloor;
                    floorMessageSent = false;
                    lastNearestSlot = -1;
                    wasOutOfRange = true;
                }

                // --- Floor entry: log key guardians for debugging (no in-game message) ---
                if (!floorMessageSent)
                {
                    var keyEnemies = new List<(int slot, byte key)>();
                    for (int e = 0; e < 15; e++)
                    {
                        byte drop = Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.ForceItemDrop));
                        if (drop == Items.shinystone || drop == Items.redberry || drop == Items.pointychestnut)
                            keyEnemies.Add((e, drop));
                    }

                    if (keyEnemies.Count > 0)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[WiseOwlSword] Floor {currentFloor} key guardians:");
                        foreach (var (slot, key) in keyEnemies)
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"  Enemy {slot} ({Enemies.GetEnemyName(Enemies.GetFloorEnemyId(slot))}): forceItemDrop = {key}");

                        floorMessageSent = true;
                    }
                }

                // --- Proximity detection: alert when nearest key-carrying enemy enters range ---
                if (!PlayerHasWiseOwlSword()) continue;

                int nearestKeySlot = -1;
                float nearestKeyDist = maxKeyDetectionRange;

                for (int e = 0; e < 15; e++)
                {
                    if (Enemies.GetFloorEnemyId(e) == 0) continue;
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.Hp)) <= 0) continue;

                    byte drop = Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.ForceItemDrop));
                    if (drop != Items.shinystone && drop != Items.redberry && drop != Items.pointychestnut) continue;

                    float dist = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(e, EnemySlotOffsets.DistanceToPlayer));
                    if (dist > 0f && dist < nearestKeyDist)
                    {
                        nearestKeyDist = dist;
                        nearestKeySlot = e;
                    }
                }

                if (nearestKeySlot == -1)
                {
                    // No key enemy within detection range — reset so the alert re-fires when one approaches
                    if (!wasOutOfRange) { wasOutOfRange = true; lastNearestSlot = -1; }
                    continue;
                }

                // Re-trigger only if the nearest key enemy changed or player was previously out of range
                bool shouldCheck = nearestKeySlot != lastNearestSlot || wasOutOfRange;
                lastNearestSlot = nearestKeySlot;
                wasOutOfRange = false;

                if (!shouldCheck) continue;

                byte nearestDrop = Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(nearestKeySlot, EnemySlotOffsets.ForceItemDrop));
                string hint = nearestDrop switch
                {
                    Items.shinystone      => "Wise Owl senses a shiny stone nearby...",
                    Items.redberry        => "Wise Owl senses a red berry nearby...",
                    Items.pointychestnut  => "Wise Owl senses a pointy chestnut nearby...",
                    _                     => null
                };

                if (hint != null)
                {
                    DungeonMessages.DisplayMessage(hint, 1, 40, 3000);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[WiseOwlSword] Key guardian nearby: {Enemies.GetEnemyName(Enemies.GetFloorEnemyId(nearestKeySlot))} (slot {nearestKeySlot}, dist {nearestKeyDist:F1}, key {nearestDrop})");
                }
            }
        }

        private static bool PlayerHasWiseOwlSword()
        {
            if (Player.Weapon.GetCurrentWeaponId() == Items.wiseowlsword)
                return true;

            for (int i = 0; i < 10; i++)
                if (Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * i)) == Items.wiseowlsword)
                    return true;

            for (int i = 0; i < 30; i++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * i)) == Items.wiseowlsword)
                    return true;

            return false;
        }
    }
}
