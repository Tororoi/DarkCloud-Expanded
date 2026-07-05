using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomEffects
    {
        static int currentAddress;
        public static bool evilciseNewFloor = false;
        public static bool chronicleNewFloor = false;
        static float chronicleCurrentWHP = 0;
        static float chronicleFormerWHP = 0;
        static int[] chronicleCurrentEnemyHP;
        static int[] chronicleFormerEnemyHP;
        public const int mode = Addresses.mode;

        private static Random random = new Random();
        public static Thread damageFadeoutThread = new Thread(new ThreadStart(DamageFadeout));

        private static readonly HashSet<int> FrozenTunaIceEnemies = new()
        {
            EnemySpecies.Blizzard.Id,   // 65
            EnemySpecies.Sam.Id,        // 85
            EnemySpecies.GemronIce.Id,  // 312
        };

        private static readonly HashSet<int> CactusImmuneNameTags = new()
        {
            EnemySpecies.MasterJacket.Id,    // 1
            EnemySpecies.SkeletonSoldier.Id, // 3
            EnemySpecies.Statue.Id,          // 5
            EnemySpecies.PiratesChariot.Id,  // 25
            EnemySpecies.Golem.Id,           // 30
            EnemySpecies.MrBlare.Id,         // 31
            EnemySpecies.Dune.Id,            // 32
            EnemySpecies.Titan.Id,           // 33
            EnemySpecies.Arthur.Id,          // 40
            EnemySpecies.LivingArmor.Id,     // 55
            EnemySpecies.SteelGiant.Id,      // 64
            EnemySpecies.Billy.Id,           // 69
            EnemySpecies.Vulcan.Id,          // 70
            EnemySpecies.Rockanoff.Id,       // 77
            EnemySpecies.Gol.Id,             // 90
            EnemySpecies.Sil.Id,             // 91
            EnemySpecies.StatueDog.Id,       // 303
            EnemySpecies.Gacious.Id,         // 317
            EnemySpecies.SilverGear.Id,      // 318
            EnemySpecies.HornHead.Id,        // 319
        };

        /// <summary>
        /// Toggles the effect of the bone rapier
        /// </summary>
        /// <param name="isActive">True if to active the effect</param>
        public static void BoneRapierEffect(bool isActive)
        {
            if (isActive)
            {
                //Set BypassBoneDoor
                if (!Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(true);
            }
            //Otherwise reset BypassBoneDoor
            else if (Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(false);
        }

        /// <summary>
        /// Trigger to open a bone door
        /// </summary>
        public static void BoneDoorTrigger()
        {
            while (!Dungeon.doorIsOpen &&
                    Player.InDungeonFloor() &&
                    Player.Weapon.GetCurrentWeaponId() == 290)
            {
                //Bone door opened through Bone Rapier
                if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                    Dungeon.IsBypassBoneDoor() &&
                    Memory.ReadInt(0x21D56800) == 15903712) //Aux address to help determine if the bone door specifically was opened)
                {
                    int ms = 0;

                    while (Memory.ReadInt(Addresses.hideHud) == 1 && ms < 2000)
                    {
                        Thread.Sleep(100);
                        ms += 100;
                        continue;
                    }

                    //Display our custom message
                    Dayuppy.DisplayMessage("You can hear an ominous voice\nlaughing 'Rattle me bones!'", 2, 29, 4000);
                    Dungeon.doorIsOpen = true;
                }
                //Bone door opened normally without Bone Rapier
                else if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                        !Dungeon.IsBypassBoneDoor() &&
                        Memory.ReadInt(0x21D56800) == 15903712 //Aux address to help determine if the bone door specifically was opened
                        )
                {
                    Dungeon.doorIsOpen = true;
                }

                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Checks if the player has Chronicle 2 equipped or in inventory and sets acquired to true if so.
        /// When possessed, Chronicle 2 affects dungeon loot in two ways:
        ///   1. The clown vendor always rolls from the weapon table instead of having a 50% chance of non-weapon items.
        ///   2. Powerup Powder is allowed to appear in chests normally; without Chronicle 2 it has an 80% chance to be re-rolled.
        /// It also adjusts the big-chest spawn threshold based on the current dungeon.
        /// Checks weapon slots 0–9 first, then scans up to 30 storage slots starting at 0x21CE22D8.
        /// </summary>
        /// <param name="acquired"></param>
        /// <returns></returns>
        public static bool CheckChronicle2(bool acquired)
        {
            acquired = false;

            if (Memory.ReadUShort(Player.Toan.WeaponSlot0.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot1.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot2.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot3.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot4.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot5.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot6.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot7.id) == 298 || Memory.ReadUShort(Player.Toan.WeaponSlot8.id) == 298
                || Memory.ReadUShort(Player.Toan.WeaponSlot9.id) == 298)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Chronicle 2");
                acquired = true;
            }
            else
            {
                currentAddress = 0x21CE22D8;
                for (int i = 0; i < 30; i++)
                {
                    if (Memory.ReadUShort(currentAddress) == 298)
                    {
                        acquired = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Chronicle 2 in storage");
                    }
                    currentAddress += 0x000000F8;
                }

                if (acquired != true)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player does not have Chronicle 2");
                    acquired = false;
                }
            }
            return acquired;
        }

        /// <summary>
        /// Ability Name: Wise Owl Always Knows (Wise Owl Sword)
        /// Wise Owl Sword passive: displays a message when an enemy holding a WOF key is nearby,
        /// provided the player owns a Wise Owl Sword anywhere (bag, storage, or equipped).
        /// </summary>
        public static void WiseOwlSword()
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
                    Dayuppy.DisplayMessage(hint, 1, 40, 3000);
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

        /// <summary>
        /// Ability Name: Jealous Soul (Evilcise)
        /// Evilcise effect: Toan is cursed while equipped and immune to all other status effects.
        /// Breaking the curse with holy water applies poison and sets HP to 1.
        /// The curse is reapplied on each new floor.
        /// </summary>
        public static void Evilcise()
        {
            bool penalized    = false;
            bool wasNearDeath = false;
            byte lastFloor    = Memory.ReadByte(Addresses.checkFloor);

            // Apply curse immediately on equip, but not while in NearDeath
            ushort cur = Memory.ReadUShort(ToanState.Status);
            wasNearDeath = (cur & 0x02) != 0;
            if (!wasNearDeath)
            {
                Memory.WriteUShort(ToanState.Status,      (ushort)(cur | 0x20));
                Memory.WriteUShort(ToanState.StatusTimer, 3600);
            }

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id + toanSlot * WeaponCollision.InventoryWeaponSlotStride) != Items.evilcise)
                    break;
                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    // New floor: clear penalty/NearDeath tracking and reapply curse
                    penalized    = false;
                    wasNearDeath = false;
                    lastFloor    = currentFloor;
                    cur = Memory.ReadUShort(ToanState.Status);
                    Memory.WriteUShort(ToanState.Status,      (ushort)((cur & 0x02) | 0x20));
                    Memory.WriteUShort(ToanState.StatusTimer, 3600);
                }

                cur = Memory.ReadUShort(ToanState.Status);
                bool nearDeath = (cur & 0x02) != 0;

                if (nearDeath)
                {
                    // NearDeath state: suspend curse effect, don't penalize
                    wasNearDeath = true;
                }
                else if (wasNearDeath)
                {
                    // Recovered from NearDeath: reapply curse only if holy water wasn't used this floor
                    wasNearDeath = false;
                    if (!penalized)
                    {
                        Memory.WriteUShort(ToanState.Status,      (ushort)(cur | 0x20));
                        Memory.WriteUShort(ToanState.StatusTimer, 3600);
                    }
                }
                else if (!penalized)
                {
                    if ((cur & 0x20) == 0)
                    {
                        // Curse was removed externally (holy water) — penalize
                        penalized = true;
                        Memory.WriteUShort(ToanState.Status,      (ushort)(cur | 0x10)); // Poison
                        Memory.WriteUShort(ToanState.StatusTimer, 3600);
                        Memory.WriteUShort(ToanState.Hp, 1);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Evilcise: curse broken — poison and HP=1 applied");
                    }
                    else
                    {
                        // Curse active: strip other statuses and refresh timer
                        Memory.WriteUShort(ToanState.Status,      (ushort)((cur & 0x02) | 0x20));
                        Memory.WriteUShort(ToanState.StatusTimer, 3600);
                    }
                }

                Thread.Sleep(100);
            }

            // Strip curse on unequip or dungeon exit
            ushort final = Memory.ReadUShort(ToanState.Status);
            Memory.WriteUShort(ToanState.Status, (ushort)(final & ~0x20));
        }

        /// <summary>
        /// Ability Name: Blood Price (Maneater)
        /// Maneater effect: Toan is cursed while equipped (reapplied each floor), but unlike
        /// Evilcise, breaking the curse with holy water carries no penalty — the curse simply
        /// stays off until the next floor. While the weapon is in the game's low-durability
        /// warning state (WHP &lt;= 10% of max — the exact check that makes the HUD gauge blink,
        /// <c>DrawWepDamageDraw</c> ELF 0x1F8D30, threshold constant 0x2A1870), the sword feeds
        /// on Toan: each second it drains 1 HP to restore 1 WHP. It never kills — the drain
        /// stops at 1 HP and pauses during NearDeath.
        /// </summary>
        public static void Maneater()
        {
            bool cured        = false;
            bool wasNearDeath = false;
            byte lastFloor    = Memory.ReadByte(Addresses.checkFloor);
            DateTime lastDrainCheck = DateTime.UtcNow;

            // Apply curse immediately on equip, but not while in NearDeath
            ushort cur = Memory.ReadUShort(ToanState.Status);
            wasNearDeath = (cur & ToanState.StatusNearDeath) != 0;
            if (!wasNearDeath)
            {
                Memory.WriteUShort(ToanState.Status,      (ushort)(cur | ToanState.StatusCurse));
                Memory.WriteUShort(ToanState.StatusTimer, ToanState.StatusDurationFrames);
            }

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                long weaponRecord = WeaponCollision.InventoryWeaponSlot0Id + toanSlot * WeaponCollision.InventoryWeaponSlotStride;
                if (Memory.ReadUShort(weaponRecord) != Items.maneater)
                    break;
                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    // New floor: clear cure/NearDeath tracking and reapply curse
                    cured        = false;
                    wasNearDeath = false;
                    lastFloor    = currentFloor;
                    cur = Memory.ReadUShort(ToanState.Status);
                    Memory.WriteUShort(ToanState.Status,      (ushort)(cur | ToanState.StatusCurse));
                    Memory.WriteUShort(ToanState.StatusTimer, ToanState.StatusDurationFrames);
                }

                cur = Memory.ReadUShort(ToanState.Status);
                bool nearDeath = (cur & ToanState.StatusNearDeath) != 0;

                if (nearDeath)
                {
                    // NearDeath state: suspend curse maintenance
                    wasNearDeath = true;
                }
                else if (wasNearDeath)
                {
                    // Recovered from NearDeath: reapply curse only if holy water wasn't used this floor
                    wasNearDeath = false;
                    if (!cured)
                    {
                        Memory.WriteUShort(ToanState.Status,      (ushort)(cur | ToanState.StatusCurse));
                        Memory.WriteUShort(ToanState.StatusTimer, ToanState.StatusDurationFrames);
                    }
                }
                else if (!cured)
                {
                    if ((cur & ToanState.StatusCurse) == 0)
                    {
                        // Curse removed externally (holy water) — no penalty, stays off this floor
                        cured = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            "Maneater: curse cleansed — no penalty");
                    }
                    else if ((cur & unchecked((ushort)~(ToanState.StatusNearDeath | ToanState.StatusCurse))) == 0)
                    {
                        // Keep the curse alive by refreshing the (shared) status timer — but only
                        // while curse is the sole timed status, so a poison/freeze/goo the player
                        // is waiting out is never extended. If the timer expires alongside such a
                        // status the curse lapses with it and reads as cured for this floor.
                        Memory.WriteUShort(ToanState.StatusTimer, ToanState.StatusDurationFrames);
                    }
                }

                // Blood price: 1 HP -> 1 WHP per second while in the native low-WHP warning state
                if (DateTime.UtcNow - lastDrainCheck >= TimeSpan.FromSeconds(1))
                {
                    lastDrainCheck = DateTime.UtcNow;
                    float  whp    = Memory.ReadFloat(weaponRecord + WeaponCollision.InventoryWeaponWhpOffset);
                    short  maxWhp = Memory.ReadShort(weaponRecord + WeaponCollision.InventoryWeaponMaxWhpOffset);
                    ushort hp     = Memory.ReadUShort(ToanState.Hp);
                    if (!nearDeath && hp > 1 && maxWhp > 0 &&
                        whp > 0f && whp <= WeaponCollision.LowWhpWarningFraction * maxWhp)
                    {
                        Memory.WriteUShort(ToanState.Hp, (ushort)(hp - 1));
                        Memory.WriteFloat(weaponRecord + WeaponCollision.InventoryWeaponWhpOffset, Math.Min(whp + 1f, maxWhp));
                    }
                }

                Thread.Sleep(100);
            }

            // Strip curse on unequip or dungeon exit
            ushort maneaterFinal = Memory.ReadUShort(ToanState.Status);
            Memory.WriteUShort(ToanState.Status, (ushort)(maneaterFinal & ~ToanState.StatusCurse));
        }

        /// <summary>
        /// Ability Name: Shadow Boxing (Macho Sword)
        /// Every combo swing that hits nothing but air builds +1 attack, up to +30, for the
        /// current dungeon floor (stacks reset on floor change). The bonus is written only to the
        /// in-battle weapon record (<see cref="Player.Weapon.SetCurrentWeaponAttack"/>) — the copy
        /// the swing code latches damage from — never the inventory record, so menus and build-up
        /// requirements keep seeing the unboosted stat (the same battle-copy/inventory split the
        /// Lamb's Sword low-WHP rate uses natively). The boost can exceed the weapon's max attack.
        ///
        /// Swing detection: <see cref="WeaponCollision.ChargeActionState"/> combo states 0x24-0x28
        /// (one per combo hit). A swing counts as a miss when <see cref="WeaponCollision.HitSparkCounter"/>
        /// hasn't advanced by the time the swing ends. Consequences of that signal: guarded
        /// (blocked) hits count as misses, and charge attacks (lunge/whirlwind) build no stacks.
        /// </summary>
        public static void MachoSword()
        {
            const int maxBonus = 30;
            const ushort rebaseSentinel = 0xFFFF;

            int bonus = 0;
            byte lastFloor = Memory.ReadByte(Addresses.checkFloor);
            ushort baseAttack = Player.Weapon.GetCurrentWeaponAttack();
            ushort lastWritten = baseAttack;
            bool inSwing = false;
            int prevState = -1;
            int swingStartHits = 0;

            void SettleMissedSwing(int hitsNow)
            {
                if (hitsNow != swingStartHits || bonus >= maxBonus) return;
                bonus++;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: whiffed swing — attack bonus {bonus}/{maxBonus}");
            }

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride) != Items.machosword)
                    break;

                // The battle record and action state belong to the ACTIVE character — while a
                // sidekick is out, hands off entirely and re-baseline when Toan returns.
                if (Player.CurrentCharacterNum() != Player.ToanId)
                {
                    inSwing = false;
                    lastWritten = rebaseSentinel;
                    Thread.Sleep(100);
                    continue;
                }

                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    lastFloor = currentFloor;
                    bonus = 0;      // fresh floor, warm-up starts over
                    inSwing = false;
                }

                // If the engine rewrote the battle record (menu close, re-equip, build-up,
                // character switch), adopt the rewritten value as the new unboosted baseline.
                ushort curAttack = Player.Weapon.GetCurrentWeaponAttack();
                if (curAttack != lastWritten)
                    baseAttack = curAttack;

                int state = Memory.ReadInt(WeaponCollision.ChargeActionState);
                int hits  = Memory.ReadInt(WeaponCollision.HitSparkCounter);
                bool isComboSwing = state >= WeaponCollision.ActionComboFirst &&
                                    state <= WeaponCollision.ActionComboLast;
                if (isComboSwing && (!inSwing || state != prevState))
                {
                    // A new swing began (possibly chained straight from the previous combo hit):
                    // settle the previous swing first, then start tracking this one.
                    if (inSwing) SettleMissedSwing(hits);
                    inSwing = true;
                    swingStartHits = hits;
                }
                else if (!isComboSwing && inSwing)
                {
                    inSwing = false;
                    SettleMissedSwing(hits);
                }
                prevState = state;

                ushort desired = (ushort)(baseAttack + bonus);
                if (desired != curAttack)
                    Player.Weapon.SetCurrentWeaponAttack(desired);
                lastWritten = desired;

                Thread.Sleep(33);
            }

            // Drop the boost on unequip/exit — but only while the battle record still belongs to
            // the Macho Sword (after a menu unequip it already holds the NEW weapon's stats).
            if (Player.Weapon.GetCurrentWeaponId() == Items.machosword)
                Player.Weapon.SetCurrentWeaponAttack(baseAttack);
        }

        // ── Buster Sword "True Buster" ─────────────────────────────────────────────────────
        private static DateTime _busterNextTick = DateTime.MinValue;

        /// <summary>
        /// Ability Name: True Buster (Buster Sword)
        /// Anti-category attachments (Dinoslayer..Mage Slayer, items 111-120) are worth +4
        /// instead of +3 while attached to a Buster Sword. Implemented by editing the attachment
        /// ENTRY inside the weapon record (its own anti byte, template base 3 → 4): the entry's
        /// values — not the item template — are what the engine reads everywhere, so the +1 is
        /// automatically part of the attachment's temporary stats: it counts in menus, in
        /// build-up (evolution) eligibility (CompareBuildUpModelData2 recomputes from the record
        /// + entries), in combat once the battle copy rebuilds, it disappears when the
        /// attachment is removed (detaching discards the entry), and the level-up absorb bakes
        /// the full +4 into base stats natively (AttachMentValuePlus consumes entry values).
        ///
        /// Stateless and restart-safe by design: an entry at the base value (3) gets bumped
        /// once; an already-bumped entry (4) is left alone. A bumped entry can never migrate to
        /// another weapon: detaching discards it, and the engine refuses Build Up while any
        /// attachment is on the weapon (NowWeaponStatusValue gates the option on
        /// GetNowWeaponAttachNum == 0). Runs from the mod main loop in ALL modes (self-gated to
        /// ~2.5 Hz) because attaching can happen from any menu, sword equipped or not.
        /// </summary>
        public static void BusterSwordTick()
        {
            if (DateTime.UtcNow < _busterNextTick) return;
            _busterNextTick = DateTime.UtcNow.AddMilliseconds(400);

            byte[] bag = Memory.ReadByteArray(WeaponCollision.InventoryWeaponSlot0Id,
                10 * WeaponCollision.InventoryWeaponSlotStride);
            if (bag == null || bag.Length < 10 * WeaponCollision.InventoryWeaponSlotStride) return;

            for (int slot = 0; slot < 10; slot++)
            {
                int off = slot * WeaponCollision.InventoryWeaponSlotStride;
                if (BitConverter.ToUInt16(bag, off) != Items.bustersword) continue;

                for (int j = 0; j < WeaponCollision.WeaponAttachSlotCount; j++)
                {
                    int entryOff = off + WeaponCollision.WeaponAttachSlot0Offset +
                                   j * WeaponCollision.WeaponAttachSlotStride;
                    ushort attachId = BitConverter.ToUInt16(bag, entryOff);
                    if (attachId < Items.dragonslayer || attachId > Items.mageslayer) continue;

                    int cat = attachId - Items.dragonslayer;   // ids 111-120 are in anti-byte order
                    int valOff = entryOff + WeaponCollision.AttachEntryAntiOffset + cat;
                    if (bag[valOff] != WeaponCollision.AttachAntiBaseValue) continue;

                    Memory.WriteByte(WeaponCollision.InventoryWeaponSlot0Id + valOff,
                        (byte)(WeaponCollision.AttachAntiBaseValue + 1));
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Buster Sword: attachment {attachId} boosted to +{WeaponCollision.AttachAntiBaseValue + 1} (bag slot {slot})");
                }
            }
        }

        /// <summary>
        /// Ability Name: Moonlit Focus (Tsukikage, Heaven's Cloud)
        /// Holding X charges the charge attack twice as fast as normal weapons. The engine adds
        /// 1/60 to the charge meter (<see cref="WeaponCollision.ChargeMeter"/>) per wind-up frame
        /// (= 1.0/second); this effect feeds in a second 1.0/second of wall time while the
        /// wind-up state is active, so lunge readies in ~0.25s and whirlwind in ~0.75s. The
        /// charge level is re-derived from the meter every wind-up frame by the engine, so the
        /// flash/tier progression follows automatically. Heaven's Cloud inherits the effect for
        /// lineage reasons (Tsukikage builds up into it), on top of its own charge-scaling
        /// effect — firing the whirlwind sooner simply means less time for the blade to grow.
        /// </summary>
        public static void Tsukikage()
        {
            DateTime lastTick = DateTime.UtcNow;

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride);
                if (equippedId != Items.tsukikage && equippedId != Items.heavenscloud)
                    break;

                DateTime now = DateTime.UtcNow;
                // Clamp so time spent outside the windup (or in a long poll hiccup) never dumps
                // a big lump into the meter the moment charging starts.
                float elapsed = Math.Min((float)(now - lastTick).TotalSeconds, 0.1f);
                lastTick = now;

                if (Player.CurrentCharacterNum() == Player.ToanId &&
                    !Player.CheckDunIsPaused() &&
                    Memory.ReadInt(WeaponCollision.ChargeActionState) == WeaponCollision.ActionWindup)
                {
                    float meter = Memory.ReadFloat(WeaponCollision.ChargeMeter);
                    if (meter > 0f && meter < WeaponCollision.ChargeMeterCap)
                    {
                        float boosted = Math.Min(WeaponCollision.ChargeMeterCap,
                            meter + elapsed * WeaponCollision.ChargeMeterPerSecond);
                        Memory.WriteFloat(WeaponCollision.ChargeMeter, boosted);
                    }
                }

                Thread.Sleep(16);
            }
        }

        /// <summary>
        /// Ability Name: Solar Harvest (Sun Sword, Big Bang)
        /// While the Sun Sword — or its evolution, Big Bang — is wielded, each enemy on the floor
        /// has a 1% chance (rolled once per slot per floor) to drop a Sun attachment instead of
        /// its regular drop when killed. (Big Bang shares this effect for lineage/visual-design
        /// reasons; it will additionally get its own unique effect later.)
        /// Implemented by pre-staging the engine's guaranteed-drop field (<see
        /// cref="EnemySlotOffsets.ForceItemDrop"/> — the same mechanism dungeon keys and miniboss
        /// loot use, consumed by the death-drop block in CMonstorUnit::Step, ELF 0x1DF4C0) while
        /// the sword is in hand, and un-staging when it isn't (unequip, character switch), so
        /// there is no race against the killing blow. Slots already carrying a forced drop
        /// (dungeon key, miniboss loot, mimic key) are never touched. Engine caveat: the drop
        /// path runs items through a small de-dupe set, so a second Sun proc on the same floor
        /// may be swallowed.
        /// </summary>
        public static void SunSword()
        {
            const int procPercent = 1;

            byte lastFloor = Memory.ReadByte(Addresses.checkFloor);
            int slotCount = EnemyAddresses.FloorSlots.Count;
            bool[]   rolled       = new bool[slotCount];   // 1% chance consumed for this slot
            bool[]   winner       = new bool[slotCount];   // slot won the roll
            bool[]   staged       = new bool[slotCount];   // our Sun id is currently written
            ushort[] originalDrop = new ushort[slotCount]; // pre-stage value to restore (0 or 65535)

            void UnstageAll()
            {
                for (int i = 0; i < slotCount; i++)
                {
                    if (!staged[i]) continue;
                    staged[i] = false;
                    int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                    if (Memory.ReadUShort(dropAddr) == Items.sun)   // still ours → restore
                        Memory.WriteUShort(dropAddr, originalDrop[i]);
                }
            }

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride);
                if (equippedId != Items.sunsword && equippedId != Items.bigbang)
                    break;

                byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
                if (currentFloor != lastFloor)
                {
                    // New floor: the slot array was reinitialized, so forget everything WITHOUT
                    // restoring (writing stale values into fresh slots would corrupt them).
                    lastFloor = currentFloor;
                    Array.Clear(rolled, 0, slotCount);
                    Array.Clear(winner, 0, slotCount);
                    Array.Clear(staged, 0, slotCount);
                }

                // Kills while a sidekick is out aren't Sun Sword kills — keep nothing staged.
                // Winners keep their win and are re-staged when Toan takes over again.
                if (Player.CurrentCharacterNum() != Player.ToanId)
                {
                    UnstageAll();
                    Thread.Sleep(250);
                    continue;
                }

                for (int i = 0; i < slotCount; i++)
                {
                    if (Enemies.GetFloorEnemyId(i) == 0) continue;
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) <= 0) continue;

                    int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                    ushort dropVal = Memory.ReadUShort(dropAddr);

                    if (!rolled[i])
                    {
                        rolled[i] = true;
                        // Slots that already carry a forced drop (key/miniboss/mimic) are off-limits
                        if (dropVal != 0 && dropVal != 65535) continue;
                        winner[i] = random.Next(100) < procPercent;
                        if (winner[i]) originalDrop[i] = dropVal;
                    }
                    if (!winner[i] || staged[i]) continue;

                    // Re-check occupancy — a key could have been assigned here after our roll
                    if (dropVal != 0 && dropVal != 65535 && dropVal != Items.sun)
                    {
                        winner[i] = false;
                        continue;
                    }
                    Memory.WriteUShort(dropAddr, (ushort)Items.sun);
                    staged[i] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Sun Sword: slot {i} ({Enemies.GetFloorEnemyId(i)}) will drop a Sun attachment");
                }

                Thread.Sleep(250);
            }

            // Unequipped or left the floor: revert anything still staged (guarded by a
            // still-ours check, so stale slots after a floor unload are left alone).
            UnstageAll();
        }

        /// <summary>
        /// Heaven's Cloud effect (charge scaling). While the whirlwind is being charged the blade grows over
        /// real time (up to 3x after <c>growSeconds</c>), independent of the polling rate; the whirl visual and
        /// enemy hitboxes are kept matched to the current size the whole time it's charging AND executing, so
        /// they're ready the instant the whirlwind fires. When the charge attack finishes the blade snaps back
        /// to its original size. Scaling reuses the helpers in <see cref="Weapons"/>.
        /// </summary>
        public static void HeavensCloudEffect()
        {
            const float maxScale = 3.0f;       // blade grows up to 3x
            const double growSeconds = 4.0;    // wall-clock time to grow from 1x to maxScale
            var growTimer = new System.Diagnostics.Stopwatch();
            float factor = 1.0f;
            bool active = false;               // a non-base scale is applied and still needs resetting

            while (Player.Weapon.GetCurrentWeaponId() == Items.heavenscloud &&
                   Player.InDungeonFloor())
            {
                if (Weapons.IsChargingWhirlwind())      // whirlwind charge specifically → grow the blade over time
                {
                    if (!growTimer.IsRunning) growTimer.Restart();   // whirlwind charge just began
                    float t = (float)Math.Min(1.0, growTimer.Elapsed.TotalSeconds / growSeconds);
                    factor = 1.0f + t * (maxScale - 1.0f);
                    active = true;
                }
                else if (Weapons.IsWhirlwindActive())   // whirlwind executing → hold the size reached during the charge
                {
                    growTimer.Stop();
                    active = true;
                }
                else if (active)                        // charge finished → snap the blade back to its original size
                {
                    growTimer.Reset();
                    factor = 1.0f;
                    Weapons.ResetHeavensCloudReach();
                    active = false;
                }

                // Keep blade + whirl + enemy hitboxes matched to the current size for the whole charge→whirlwind
                // window: pre-scaled while charging (enemies are already inflated and the fuusya pool is already
                // sized when the whirlwind lands) and maintained while it executes (the effect re-poses its root
                // on each cast, so the scale must be re-applied while it's live).
                if (active)
                {
                    Weapons.ScaleHeavensCloudBlade(factor);
                    Weapons.ScaleHeavensCloudWhirl(factor);
                    Weapons.ScaleHeavensCloudHitbox(factor + 2.0f);
                }

                Thread.Sleep(30);
            }

            Weapons.ResetHeavensCloudReach();   // unequipped → make sure everything is back to normal
        }

        /// <summary>
        /// Ability Name: Defensive Legacy (Aga's Sword)
        /// Aga's Sword: +15 defense to Toan while equipped.
        /// </summary>
        public static void AgasSword()
        {
            const int boost = 15;
            int baseDefense = Player.Toan.GetDefense();
            Player.Toan.SetDefense(baseDefense + boost);

            while (Player.Weapon.GetCurrentWeaponId() == Items.agassword && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                int current = Player.Toan.GetDefense();
                if (current != baseDefense + boost)
                {
                    baseDefense = current;
                    Player.Toan.SetDefense(baseDefense + boost);
                }
            }

            Player.Toan.SetDefense(Player.Toan.GetDefense() - boost);
        }

        /// <summary>
        /// Ability Name: Hero's Courage (Brave Ark)
        /// Brave Ark: resists Freeze, Poison, Curse, and Goo status effects while equipped.
        /// Clears any of those statuses within the polling interval.
        /// </summary>
        public static void BraveArk()
        {
            const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                      ToanState.StatusCurse  | ToanState.StatusGoo;

            while (Player.Weapon.GetCurrentWeaponId() == Items.braveark && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                ushort status = Memory.ReadUShort(ToanState.Status);
                if ((status & resistMask) != 0)
                    Memory.WriteUShort(ToanState.Status, (ushort)(status & ~resistMask));
            }
        }

        /// <summary>
        /// Triggers SeventhHeaven effect: Whenever an attachment is acquired, another one is given.
        /// </summary>
        public static void SeventhHeaven()
        {
            while ( Player.Weapon.GetCurrentWeaponId() == Items.seventhheaven &&
                    Memory.ReadByte(Addresses.mode) == 3 &&
                    Player.CheckDunIsWalkingMode() == true)
            {
                //Store the first empty slot
                int slot = Player.Inventory.GetBagAttachmentsFirstAvailableSlot();

                //Store the item in that slot (by default should always be empty)
                int oldItem = Player.Inventory.GetBagAttachments()[slot];

                Thread.Sleep(250);

                //Re-check the item again in the same slot to see if a new item has been acquired
                int newItem = Player.Inventory.GetBagAttachments()[slot];

                //Check if a non gem attachment was obtained and proceed to make a copy of it
                if (newItem != oldItem && newItem >= Items.fire && newItem <= Items.mageslayer)
                {
                    const int attachmentOffset = 0x20;
                    const int attachmentValuesRange = 0x1F;

                    //Store the newly obtained attachment values
                    byte[] attachmentValues = Memory.ReadByteArray(Addresses.firstBagAttachment + (attachmentOffset * slot), attachmentValuesRange);

                    //If the item is a gem, roll for 50% chance to duplicate
                    if (newItem >= Items.garnet && newItem <= Items.turquoise)
                    {
                        int roll = random.Next(100);

                        if (roll < 50)
                        {
                            //Put a copy of the same attachment on the next available slot
                            if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() != -1) Memory.WriteByteArray(Addresses.firstBagAttachment + (attachmentOffset * Player.Inventory.GetBagAttachmentsFirstAvailableSlot()), attachmentValues);

                            Dayuppy.DisplayMessage("The 7th Heaven has blessed\nyou with a gift!", 2, 27, 3500);
                        }
                        return;
                    }

                    //Put a copy of the same attachment on the next available slot
                    if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() != -1) Memory.WriteByteArray(Addresses.firstBagAttachment + (attachmentOffset * Player.Inventory.GetBagAttachmentsFirstAvailableSlot()), attachmentValues);

                    Dayuppy.DisplayMessage("The 7th Heaven has blessed\nyou with a gift!", 2, 27, 3500);
                }
            }
        }

        /// <summary>
        /// Triggers Chronicle effect: Attacks now hit all nearby targets for a percentage of the damage.
        /// </summary>
        public static void ChronicleSword()
        {
            if (chronicleNewFloor == true)
            {
                for (int i = 0; i < 7; i++ )
                {
                    Memory.WriteInt(0x21EC828C + (0x60 * i), 0);
                    Memory.WriteInt(0x21EC8290 + (0x60 * i), 158);
                    Memory.WriteInt(0x21EC8294 + (0x60 * i), 12);
                    Memory.WriteInt(0x21EC8298 + (0x60 * i), 18);
                }
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                    Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.DistanceToPlayer), 0);
                chronicleNewFloor = false;
            }

            Thread.Sleep(50);

            //Save weapon Whp
            chronicleCurrentWHP = ReusableFunctions.GetCurrentEquippedWhp(Player.CurrentCharacterNum(), Player.Toan.GetWeaponSlot());

            //Save every enemy's HP on the current floor
            chronicleCurrentEnemyHP = ReusableFunctions.GetEnemiesHp();

            int damagedEnemyNum = 0;
            if (chronicleCurrentWHP < chronicleFormerWHP && ReusableFunctions.GetRecentDamageDealtByPlayer() > 0)
            {
                float flashRGB_R = 0;
                float flashRGB_G = 0;
                float flashRGB_B = 0;
                float damageDealt = 0;
                for (int i = 0; i < 15; i++)
                {
                    if (chronicleCurrentEnemyHP[i] < chronicleFormerEnemyHP[i])
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Damaged enemy number: " + i);
                        damagedEnemyNum = i;
                        flashRGB_R = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorRed));
                        flashRGB_G = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorGreen));
                        flashRGB_B = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FlashColorBlue));
                        damageDealt = Memory.ReadInt(0x21DC452C);
                        break;
                    }
                }

                float[] enemiesDistance = ReusableFunctions.GetEnemiesDistance();
                List<int> enemiesinRange = new List<int>();
                float[] enemiescoordinateX = new float[15];
                float[] enemiescoordinateY = new float[15];
                float[] enemiescoordinateZ = new float[15];
                float[] effectDamage = new float[15];
                int[] effectDamageDigit1 = new int[15];
                int[] effectDamageDigit2 = new int[15];
                int[] effectDamageDigit3 = new int[15];
                int[] effectDamageDigit4 = new int[15];
                int[] effectDamageDigit5 = new int[15];

                for (int i= 0; i < 15; i++)
                {
                    if (i != damagedEnemyNum)
                    {
                        if (chronicleCurrentEnemyHP[i] > 0 && enemiesDistance[i] < 300)
                        {
                            if (enemiesDistance[i] > 0)
                            {
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + i + " is in range");
                                enemiesinRange.Add(i);
                            }
                        }
                    }
                }

                if (enemiesinRange.Count > 0)
                {
                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorRed), flashRGB_R);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorGreen), flashRGB_G);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashColorBlue), flashRGB_B);
                        Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashDecayRate), (float)(0.1));


                        enemiescoordinateX[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationX));
                        enemiescoordinateZ[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationZ));
                        enemiescoordinateY[i] = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.LocationY));

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + enemiesinRange[i] + " XZY coordinates: " + enemiescoordinateX[i] + " " + enemiescoordinateZ[i] + " " + enemiescoordinateY[i]);

                        if (enemiesDistance[enemiesinRange[i]] < 50)
                        {
                            effectDamage[i] = (float)System.Math.Floor(damageDealt / 2);
                        }
                        else
                        {
                            float effectDamagePercent = ((300 - enemiesDistance[enemiesinRange[i]]) / 5);
                            if (effectDamagePercent < 1)
                            {
                                effectDamagePercent = 1;
                            }
                            effectDamage[i] = (float)System.Math.Floor(damageDealt * (effectDamagePercent / 100));
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Enemy " + enemiesinRange[i] + " effect dmg: " + effectDamage[i]);

                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        int[] digitArray = GetIntArray((int)effectDamage[i]);

                        effectDamageDigit2[i] = -1;
                        effectDamageDigit3[i] = -1;
                        effectDamageDigit4[i] = -1;
                        effectDamageDigit5[i] = -1;
                        if (digitArray.Length > 0)
                        {
                            effectDamageDigit1[i] = digitArray[0];
                        }
                        if (digitArray.Length > 1)
                        {
                            effectDamageDigit2[i] = digitArray[1];
                        }
                        if (digitArray.Length > 2)
                        {
                            effectDamageDigit3[i] = digitArray[2];
                        }
                        if (digitArray.Length > 3)
                        {
                            effectDamageDigit4[i] = digitArray[3];
                        }
                        if (digitArray.Length > 4)
                        {
                            effectDamageDigit5[i] = digitArray[4];
                        }
                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteFloat(0x21EC8240 + (0x60 * i), enemiescoordinateX[i]);
                        Memory.WriteFloat(0x21EC8244 + (0x60 * i), enemiescoordinateZ[i] - 3);
                        Memory.WriteFloat(0x21EC8248 + (0x60 * i), enemiescoordinateY[i]);
                        Memory.WriteFloat(0x21EC824C + (0x60 * i), 1);

                        Memory.WriteFloat(0x21EC8254 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8258 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC825C + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8260 + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8264 + (0x60 * i), 0);

                        Memory.WriteInt(0x21EC8268 + (0x60 * i), effectDamageDigit1[i]);
                        Memory.WriteInt(0x21EC826C + (0x60 * i), effectDamageDigit2[i]);
                        Memory.WriteInt(0x21EC8270 + (0x60 * i), effectDamageDigit3[i]);
                        Memory.WriteInt(0x21EC8274 + (0x60 * i), effectDamageDigit4[i]);
                        Memory.WriteInt(0x21EC8278 + (0x60 * i), effectDamageDigit5[i]);

                        Memory.WriteFloat(0x21EC827C + (0x60 * i), 0);
                        Memory.WriteFloat(0x21EC8280 + (0x60 * i), 3);
                        Memory.WriteInt(0x21EC8284 + (0x60 * i), -1);

                        if (i == 7)
                        {
                            break;
                        }
                    }

                    for (int i = 0; i < enemiesinRange.Count; i++)
                    {
                        Memory.WriteInt(0x21EC829C + (0x60 * i), 1);
                        Memory.WriteByte(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.FlashActivation), 1);

                        int enemyHP = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.Hp));
                        int newEnemyHP = (int)(enemyHP - effectDamage[i]);
                        if (newEnemyHP < 1)
                        {
                            newEnemyHP = 1;
                            Memory.WriteByte(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.PoisonPeriod), 1);
                        }
                        Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(enemiesinRange[i], EnemySlotOffsets.Hp), newEnemyHP);
                    }

                    if (!damageFadeoutThread.IsAlive)
                    {
                        damageFadeoutThread = new Thread(new ThreadStart(DamageFadeout));
                        damageFadeoutThread.Start();
                    }
                }
            }
            ReusableFunctions.ClearRecentDamageAndDamageSource();
            chronicleFormerWHP = chronicleCurrentWHP;
            chronicleFormerEnemyHP = chronicleCurrentEnemyHP;
        }

        public static void DamageFadeout()
        {
            Thread.Sleep(500);
            for (int i = 0; i < 7; i++)
            {
                Memory.WriteInt(0x21EC8284 + (0x60 * i), 1);
            }
            Thread.Sleep(200);
            for (int i = 0; i < 7; i++)
            {
                Memory.WriteInt(0x21EC829C + (0x60 * i), 0);
            }
        }

        public static int[] GetIntArray(int num)
        {
            List<int> listOfInts = new List<int>();
            while (num > 0)
            {
                listOfInts.Add(num % 10);
                num = num / 10;
            }
            //listOfInts.Reverse();
            return listOfInts.ToArray();
        }

        /// <summary>
        /// Triggers Angel Gear effect: Applies the Heal regeneration effect to all allies
        /// </summary>
        public static void AngelGear()
        {
            //Initialize variables
            ushort HpValueAdd = 1;
            ushort Delay = 5000;
            ushort XiaoHp = 0;
            ushort XiaoMaxHp = 0;
            bool isHealXiao = false;

            //Run while Angel Gear is equipped and Player is in valid state
            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                //Fetch HP values for characters
                ushort ToanHp = Player.Toan.GetHp();
                ushort ToanMaxHp = Player.Toan.GetMaxHp();
                ushort GoroHp = Player.Goro.GetHp();
                ushort GoroMaxHp = Player.Goro.GetMaxHp();
                ushort RubyHp = Player.Ruby.GetHp();
                ushort RubyMaxHp = Player.Ruby.GetMaxHp();
                ushort UngagaHp = Player.Ungaga.GetHp();
                ushort UngagaMaxHp = Player.Ungaga.GetMaxHp();
                ushort OsmondHp = Player.Osmond.GetHp();
                ushort OsmondMaxHp = Player.Osmond.GetMaxHp();

                //Check for the Heal special attribute on the weapon
                if (Player.Weapon.GetCurrentWeaponSpecial2() % 16 < 8 ||
                    Player.Weapon.GetCurrentWeaponSpecial2() % 16 > 11)
                {
                    isHealXiao = true;
                    XiaoHp = Player.Xiao.GetHp();
                    XiaoMaxHp = Player.Xiao.GetMaxHp();
                }

                //Add the HP value to the characters current HP
                if (ToanHp < ToanMaxHp && ToanHp > 0) Player.Toan.SetHp((ushort)(ToanHp + HpValueAdd));
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Toan HP add: " + (ToanHp + HpValueAdd));
                if (GoroHp < GoroMaxHp && GoroHp > 0) Player.Goro.SetHp((ushort)(GoroHp + HpValueAdd));
                if (RubyHp < RubyMaxHp && RubyHp > 0) Player.Ruby.SetHp((ushort)(RubyHp + HpValueAdd));
                if (UngagaHp < UngagaMaxHp && UngagaHp > 0) Player.Ungaga.SetHp((ushort)(UngagaHp + HpValueAdd));
                if (OsmondHp < OsmondMaxHp && OsmondHp > 0) Player.Osmond.SetHp((ushort)(OsmondHp + HpValueAdd));

                //Only affect Xiao if Angel Gear does not have the Heal attribute already
                if (isHealXiao && XiaoHp < XiaoMaxHp && XiaoHp > 0) Player.Xiao.SetHp((ushort)(XiaoHp + HpValueAdd));

                //Wait in between additions
                Thread.Sleep(Delay);
            }
        }

        /// <summary>
        /// Triggers Tall Hammer effect: Reduces enemies size on hit
        /// </summary>
        public static void TallHammer()
        {
            //Offset between the enemy's dimension addresses
            int scaleOffset = MiniBoss.scaleOffset;

            //Save every enemy's HP on the current floor
            int[] formerEnemyHpList = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            //Re-save every enemy's HP on the current floor
            int[] currentEnemyHpList = ReusableFunctions.GetEnemiesHp();

            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId;

            if (hasHit)
            {
                //Store the damaged enemies ID onto a list
                List<int> enemyIds = ReusableFunctions.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

                //Run through the enemies hit
                foreach (int id in enemyIds)
                {
                    //Declare the enemy dimensions based on the enemy that got hit
                    float enemyZeroWidth = Memory.ReadFloat(0x21E18530 + (scaleOffset * id));
                    float enemyZeroHeight = Memory.ReadFloat(0x21E18534 + (scaleOffset * id));
                    float enemyZeroDepth = Memory.ReadFloat(0x21E18538 + (scaleOffset * id));

                    //Set an initial acceleration value
                    float i = 0.15f;

                    //Set a counter for how many times to change the enemy's dimensions (this acts as a duration variable)
                    int counter = 0;

                    //Instructions will run for 1000 times (arbitrary number) and only while the enemy's dimensions are between 30% - 100% of their original size
                    while (counter < 1000 && ((enemyZeroWidth >= 0.3f && enemyZeroWidth <= 1f) || (enemyZeroHeight >= 0.3f && enemyZeroHeight <= 1f) || (enemyZeroDepth >= 0.3f && enemyZeroDepth <= 1f)))
                    {
                        //Change each of the enemy axis dimensions (X,Y and Z) based on the offset from the original Enemy 0 address
                        Memory.WriteFloat(MiniBoss.enemyZeroWidth + (scaleOffset * id), enemyZeroWidth - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroHeight + (scaleOffset * id), enemyZeroHeight - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroDepth + (scaleOffset * id), enemyZeroDepth - (i * 0.0001f));
                        i++;
                        counter++;
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Inferno effect: Increase attack power depending on health and thirst
        /// </summary>
        public static void Inferno()
        {
            float goroMaxHP = Player.Goro.GetMaxHp();
            float goroCurrentHP = Player.Goro.GetHp();

            float hpPercentage = 100 - (goroCurrentHP / goroMaxHP * 100);

            float goroMaxThirst = Player.Goro.GetMaxThirst();
            float goroCurrentThirst = Player.Goro.GetThirst();

            float thirstPercentage = 100 - (goroCurrentThirst / goroMaxThirst * 100);

            ushort currentBaseAttack = Player.Weapon.GetCurrentWeaponAttack();

            ushort attachmentsAttack = 0;

            for (int i = 0; i < 4; i++)
            {
                attachmentsAttack += Memory.ReadUShort(0x21EA75C0 + (i * 0x20));
            }

            ushort currentTotalAttack = (ushort)(currentBaseAttack + attachmentsAttack);

            if (currentTotalAttack > 350)
            {
                currentTotalAttack = 350;
            }

            ushort hpAttackBoost = (ushort)((currentTotalAttack / 100) * hpPercentage);

            ushort thirstAttackBoost = (ushort)((currentTotalAttack / 100) * (thirstPercentage / 2));

            Memory.WriteUShort(0x21EA7594, (ushort)(currentTotalAttack + hpAttackBoost + thirstAttackBoost));
        }

        /// <summary>
        /// Ability Name: Cold Storage (Frozen Tuna)
        /// Frozen Tuna: WHP lost builds a healing pool. When Goro takes damage the pool
        /// drains at 1 HP per 0.5 seconds. Healing pauses if HP reaches max; the pool is
        /// preserved until the next hit. Pool resets on weapon repair or weapon switch.
        /// On hit, 5% chance to stop all non-ice enemies and freeze Goro for 3 seconds.
        /// Ice enemies (Blizzard, Sam, Ice Gemron) are immune to the stop proc.
        /// </summary>
        public static void FrozenTuna()
        {
            float storedHealing = 0f; // HP banked from WHP losses
            float healFraction  = 0f; // sub-integer carry for 1 HP/500ms drain
            bool  healActive    = false;

            while (Player.Weapon.GetCurrentWeaponId() == Items.frozentuna && Player.InDungeonFloor())
            {
                int[] formerHp      = ReusableFunctions.GetEnemiesHp();
                float whpBefore     = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());
                ushort goroHpBefore = Player.Goro.GetHp();

                Thread.Sleep(50);

                int[] currentHp  = ReusableFunctions.GetEnemiesHp();
                float whpAfter   = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());
                ushort goroHp    = Player.Goro.GetHp();
                ushort goroMaxHp = Player.Goro.GetMaxHp();

                // WHP lost → bank into healing pool (2 HP per 1 WHP lost)
                if (whpAfter < whpBefore)
                    storedHealing += (whpBefore - whpAfter) * 2f;

                // WHP repaired → reset everything
                if (whpAfter > whpBefore)
                {
                    storedHealing = 0f;
                    healFraction  = 0f;
                    healActive    = false;
                }

                // Goro took damage → activate pool drain if pool has anything
                if (goroHp < goroHpBefore && storedHealing > 0f)
                    healActive = true;

                // Drain pool at 1 HP per 500ms (0.1 HP per 50ms tick) while below max
                if (healActive && storedHealing > 0f && goroHp < goroMaxHp)
                {
                    float drain    = Math.Min(0.1f, storedHealing);
                    storedHealing -= drain;
                    healFraction  += drain;

                    int intHeal = (int)healFraction;
                    if (intHeal > 0)
                    {
                        Player.Goro.SetHp((ushort)Math.Min(goroHp + intHeal, goroMaxHp));
                        healFraction -= intHeal;
                    }
                }

                if (storedHealing <= 0f)
                    healActive = false;

                // On-hit 5% stop proc: all non-ice enemies stopped; Goro frozen too
                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId)
                {
                    bool hitDetected = false;
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            hitDetected = true;
                            break;
                        }
                    }

                    if (hitDetected && random.Next(100) < 5)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            if (Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) == 2 &&
                                !FrozenTunaIceEnemies.Contains(Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId))))
                            {
                                Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FreezeTimer), 300);
                            }
                        }
                        Player.Goro.SetStatus("freeze", 180); // 3 seconds at 60fps
                        int freezeStartTick = Memory.ReadInt(Addresses.ingameTimer);
                        Task.Run(() =>
                        {
                            while (Memory.ReadInt(Addresses.ingameTimer) - freezeStartTick < 180)
                                Thread.Sleep(50);
                            if (Player.Goro.GetStatus() == 4)
                            {
                                Memory.WriteUShort(Player.Goro.status, 0);
                                Memory.WriteUShort(Player.Goro.statusTimer, 0);
                            }
                        });
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Mobius Ring effect: Increases damage output the longer you charge an attack
        /// </summary>
        public static void MobiusRing()
        {
            //Declare inputs
            string message;
            int height;
            int width;
            ushort sleep = 1500;
            int chargeGlowTimer = 0x21DC449E;
            ushort chargeTimer = 0;

            //Check these addresses which tells us if Ruby is charging an attack
            if (Player.Ruby.IsChargingAttack())
            {
                //Fetch the active orbs
                List<int> OrbIds = RubyOrbs.GetRubyActiveOrbs();

                //Initialize the damage
                int damage = Player.Weapon.GetCurrentWeaponAttack() + Player.Weapon.GetCurrentWeaponMagic();

                while (Player.Ruby.IsChargingAttack())
                {
                    //Check if the game is paused during the charge
                    if (Player.CheckDunIsPaused())
                    {
                        ReusableFunctions.AwaitUnpause(1);
                    }
                    //If the damage increase reaches the set max value, stop increasing it further
                    if (damage >= ushort.MaxValue)
                    {
                        damage = ushort.MaxValue;
                    }
                    else damage += damage / 2;

                    //Set messages to display onscreen
                    if (damage > 9000)
                    {
                        message = "Total damage is over 9000";
                        height = 1;
                        width = message.Length;
                    }
                    else
                    {
                        message = "Total damage " + damage;
                        height = 1;
                        width = message.Length;
                    }

                    //Keep looping until chargeGlowTimer reaches the value 17008 or the player stops charging
                    while (Memory.ReadUShort(chargeGlowTimer) < 17008 && Player.Ruby.IsChargingAttack())
                    {
                        if (Player.CheckDunIsPaused())
                        {
                            ReusableFunctions.AwaitUnpause(1);
                        }
                        Thread.Sleep(100);
                        continue;
                    }

                    //Save the value of the timer
                    chargeTimer = Memory.ReadUShort(chargeGlowTimer);

                    //Check if the timer hit the value we are looking for. This value makes Ruby flash
                    if (chargeTimer == 17008)
                    {
                        //Display current damage
                        Dayuppy.DisplayMessage(message, height, width, sleep + 500);

                        Thread.Sleep(sleep);

                        //Reset Flash
                        Memory.WriteUShort(chargeGlowTimer, 0);
                    }

                    Thread.Sleep(100);
                }

                //Go through the different slots that Ruby stores her attacks in memory
                foreach (int id in OrbIds)
                {
                    switch (id)
                    {
                        case 0:
                            //Check if the orb is still alive
                            while (Memory.ReadByte(RubyOrbs.Orb0.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb0.damage, damage); //Set the damage
                            }
                            break;
                        case 1:
                            while (Memory.ReadByte(RubyOrbs.Orb1.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb1.damage, damage);
                            }
                            break;
                        case 2:
                            while (Memory.ReadByte(RubyOrbs.Orb2.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb2.damage, damage);
                            }
                            break;
                        case 3:
                            while (Memory.ReadByte(RubyOrbs.Orb3.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb3.damage, damage);
                            }
                            break;
                        case 4:
                            while (Memory.ReadByte(RubyOrbs.Orb4.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb4.damage, damage);
                            }
                            break;
                        case 5:
                            while (Memory.ReadByte(RubyOrbs.Orb5.id) == 1)
                            {
                                Memory.WriteInt(RubyOrbs.Orb5.damage, damage);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Enables the Secret Armlet special effect:
        /// <br></br>
        /// <br>Makes all magic circle effects turn into positive ones.</br>
        /// </summary>
        /// <param name="isNewFloor">Determines if we have entered a new floor to know if we should run this code again.</param>
        public static bool SecretArmletEnable()
        {
            bool changed = false;

            //Check if any of the spawned circles have a negative effect and change into positive ones
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0 && Memory.ReadByte(Addresses.circleEffect1) > 4)
                { int effectPositive1 = random.Next(5); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0 && Memory.ReadByte(Addresses.circleEffect2) > 4)
                { int effectPositive2 = random.Next(5); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0 && Memory.ReadByte(Addresses.circleEffect3) > 4)
                { int effectPositive3 = random.Next(5); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect1) > 4)
                { int effectPositive4 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect2) > 4)
                { int effectPositive5 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect3) > 4)
                { int effectPositive6 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

        /// <summary>
        /// Disables the Secret Armlet special effect and re-rolls any present magic circle outcome
        /// </summary>
        public static bool SecretArmletDisable()
        {
            bool changed = false;

            //Re-roll the existing circles
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0)
                { int effectPositive1 = random.Next(10); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0)
                { int effectPositive2 = random.Next(10); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0)
                { int effectPositive3 = random.Next(10); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0)
                { int effectPositive4 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0)
                { int effectPositive5 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0)
                { int effectPositive6 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

        /// <summary>
        /// Triggers Hercules Wrath effect: Chance on getting hit to gain Stamina.
        /// </summary>
        public static void HerculesWrath()
        {
            //Check Ungaga's HP
            ushort formerHP = Memory.ReadUShort(Player.Ungaga.hp);

            Thread.Sleep(100);

            //Re-check Ungaga's HP
            ushort currentHP = Memory.ReadUShort(Player.Ungaga.hp);

            if (currentHP < formerHP)
            {
                //Declare the scale for the chance to base on (0 - 100)
                int procChance = random.Next(100);

                //Check for the chance to take effect (30 = 30%)
                if (procChance < 30)
                {
                    //Give the Stamina effect for 30 seconds (1800 = 30 sec)
                    Player.Ungaga.SetStatus("stamina", 1800);
                }
            }
        }

        /// <summary>
        /// Triggers Babel Spear effect: Chance on hit to apply stop to all enemies.
        /// </summary>
        public static void BabelSpear()
        {
            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.UngagaId;

            if (hasHit)
            {
                int procChance = random.Next(100);

                //Chance to apply stop (6%)
                if (procChance < 6)
                {
                    for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                        if (Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) == 2)
                            Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.FreezeTimer), 300); //Stop duration (300 = 5 seconds)
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

        /// <summary>
        /// Ability Name: Absorb (Cactus)
        /// Cactus: on hit, restore Ungaga's thirst scaled by damage dealt.
        /// 100 damage = 10.0 thirst units = 1 visible water drop.
        /// Rock, metal, and undead types are immune.
        /// </summary>
        public static void Cactus()
        {
            while (Player.Weapon.GetCurrentWeaponId() == Items.cactus ||
                   Player.InDungeonFloor())
            {
                int[] former = ReusableFunctions.GetEnemiesHp();
                Thread.Sleep(50);
                int[] current = ReusableFunctions.GetEnemiesHp();

                if (ReusableFunctions.GetDamageSourceCharacterID() != Player.UngagaId)
                {
                    ReusableFunctions.ClearRecentDamageAndDamageSource();
                    continue;
                }

                for (int i = 0; i < 15; i++)
                {
                    if (former[i] <= 0 || current[i] >= former[i])
                        continue;

                    int enemySpeciesId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    if (CactusImmuneNameTags.Contains(enemySpeciesId))
                        continue;

                    float curThirst = Player.Ungaga.GetThirst();
                    float maxThirst = Player.Ungaga.GetMaxThirst();
                    if (maxThirst > 0 && curThirst >= maxThirst)
                        break;

                    float gain = (former[i] - current[i]) / 10.0f;
                    float newThirst = (maxThirst > 0)
                        ? Math.Min(curThirst + gain, maxThirst)
                        : curThirst + gain;
                    Player.Ungaga.SetThirst(newThirst);
                    break;
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

        /// <summary>
        /// Triggers Supernova effect: Chance on hit to apply a random status
        /// </summary>
        public static void Supernova()
        {
            //Get a read on all the enemies hp on the current floor
            int[] formerEnemyHpList = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.OsmondId;

            if (hasHit)
            {
                //Get a second read on all the enemies hp on the current floor
                int[] currentEnemyHpList = ReusableFunctions.GetEnemiesHp();

                //Store the damaged enemies ID onto a list
                List<int> enemyIds = ReusableFunctions.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

                //Go through the enemies IDs
                foreach (int id in enemyIds)
                {
                    int procChance = random.Next(100);    //Roll for chance to proc effect (10% chance)
                    int effect = random.Next(4);        //Roll for which effect to apply (Equal chance)

                    if (procChance <= 10)
                    {
                        int slotBase = EnemyAddresses.FloorSlots.SlotAddr(id, 0);
                        switch (effect)
                        {
                            case 0: Memory.WriteUShort(slotBase + EnemySlotOffsets.FreezeTimer,  300); break;
                            case 1: Memory.WriteUShort(slotBase + EnemySlotOffsets.PoisonPeriod, 1);   break;
                            case 2: Memory.WriteUShort(slotBase + EnemySlotOffsets.StaminaTimer, 300); break;
                            case 3: Memory.WriteUShort(slotBase + EnemySlotOffsets.GooeyState,   1);   break;
                        }
                    }
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

        /// <summary>
        /// Triggers StarBreaker effect: Chance on kill to get an empty synthsphere (Breaks down any weapon)
        /// </summary>
        public static void StarBreaker()
        {
            //Save every enemy's HP on the current floor
            int[] formerEnemiesHP = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            //Re-save every enemy's HP on the current floor
            int[] currentEnemiesHP = ReusableFunctions.GetEnemiesHp();

            List<int> enemiesKilled = ReusableFunctions.GetEnemiesKilledIds(formerEnemiesHP, currentEnemiesHP);

            int roll = random.Next(100);

            //Check if an enemy was killed and if the roll chance was met (2%)
            if(enemiesKilled.Count > 0 && roll < 2)
            {
                //Check if the player has free inventory slots
                if (Player.Inventory.GetBagAttachmentsFirstAvailableSlot() >= 0)
                {
                    //Place the synth sphere in inventory
                    Player.Inventory.SetBagAttachments(Items.synthsphere);

                    //Display the effect message
                    Dayuppy.DisplayMessage("The Star Breaker sent\nyou a shooting star!", 2, 21);
                }
            }
        }

        /// <summary>
        /// Snail effect: 5% chance on hit to inflict gooey on the struck enemy
        /// </summary>
        public static void SnailEffect()
        {
            while (Player.Weapon.GetCurrentWeaponId() == Items.snail &&
                   Player.InDungeonFloor())
            {
                int[] formerHp = ReusableFunctions.GetEnemiesHp();
                Thread.Sleep(50);
                int[] currentHp = ReusableFunctions.GetEnemiesHp();

                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.OsmondId)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            if (random.Next(100) < 5)
                            {
                                Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.GooeyState), 1);
                            }
                        }
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }
    }
}
