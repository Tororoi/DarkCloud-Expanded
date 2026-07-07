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

        // ── Macho Sword "Overtraining" (ABS rollover) ──────────────────────────────────────
        private static DateTime _machoNextTick = DateTime.MinValue;
        private static DateTime _machoNextOwnershipCheck = DateTime.MinValue;
        private static DateTime _machoNextCarryPass = DateTime.MinValue;
        private static bool _machoOwned;
        // per-enemy-slot HP latch for kill detection
        private static readonly int[] _machoPrevHp = new int[EnemyAddresses.FloorSlots.Count];
        // shadow accounting for the ACTIVE character's equipped weapon record
        private static long _machoShadowRecord;   // record the shadow belongs to (0 = unsynced)
        private static int _machoShadowId;
        private static int _machoTrueAbs;
        private static DateTime _machoLastGrantAt = DateTime.MinValue;
        // level-up carry: last-seen state + pending carries, one per bag record (6 chars × 10 slots)
        private struct MachoSnap { public ushort Id; public short Level; public short Abs; public short RolledAbs; public DateTime RolledAt; }
        private struct MachoCarry { public bool Active; public ushort Id; public int Carry; public int GuardPasses; }
        private static readonly MachoSnap[,] _machoSnaps = new MachoSnap[6, 10];
        private static readonly MachoCarry[,] _machoCarries = new MachoCarry[6, 10];
        private static bool _machoSnapsPrimed;
        // fast carry path: armed at the menu level-up CONFIRM (flow kind 0), fires the instant
        // the engine's post-level abs reset lands — during the ~2s animation, so the gauge
        // never redraws with 0
        private static long _machoFlowRec;        // record being leveled by the menu flow (0 = disarmed)
        private static ushort _machoFlowId;
        private static short _machoFlowPrevLevel;
        private static int _machoFlowCarry;
        private static DateTime _machoFlowArmedAt;
        private static Thread _machoStrikeThread; // tight-poll payout thread, alive only while armed
        private static bool _machoBankMigrated;
        private static string MachoBankFile => System.IO.Path.Combine(AppContext.BaseDirectory, "MachoRolloverBank.txt");

        /// <summary>
        /// Ability Name: Overtraining (Macho Sword) — replaces the old Shadow Boxing effect.
        /// PASSIVE: as long as a Macho Sword is OWNED (equipped, in Toan's bag, or in storage —
        /// it does not need to be in hand), every weapon's ABS keeps absorbing past its max, up
        /// to <see cref="WeaponCollision.AbsRollover.RolloverFactor"/>× (98/100 + a 6-ABS kill →
        /// 104/100, cap 200/100), and the rolled-over surplus carries into the next level
        /// (level up at 116/100 → the new level starts at 16 ABS).
        ///
        /// The rolled value lives IN the record's abs field — the menu gauge number reads
        /// "116/100" for real — backed by two tiny EE-code display patches (applied from
        /// ApplyNewChanges at in-game entry, while the menu code is still cold — see
        /// <see cref="ApplyAbsCodePatches"/>): the weapon menu's number clamp is split off its
        /// gauge math (number raw, bar still pinned at full), and the battle HUD's abs bar gets
        /// a min(abs, max) folded into its width computation so it no longer overdraws. On menu
        /// level-up the engine zeroes abs on the message-dismiss press (SetLevelUpWeaponData —
        /// same input that redraws the gauge), so the level-up carry is delivered by a strike
        /// thread armed at the confirm (<see cref="MachoFastCarryTick"/>): the 0 is on screen
        /// for at most a frame before the carried surplus replaces it.
        ///
        /// Kill accounting: the engine's grant (CMonstorUnit::Step death block) is SKIPPED
        /// entirely once abs ≥ max, so above max this watcher replicates it — per-kill detection
        /// via the enemy slots' HP&gt;0→≤0 latch (which fires ~a death animation BEFORE the
        /// engine's own grant on the slot-release frame), the engine's own eligibility (killer ==
        /// active character, no default weapons, no monster-transform drain mode) and modifiers
        /// (back-floor ×2, slot-flag-0x2000 ×1.2). Writing the rolled value ≥ max immediately
        /// also neutralizes the engine's pending grant for the same kill (its abs &lt; max gate
        /// fails), so nothing double-counts. Save-safe: abs is an ordinary save field and a save
        /// carrying 116/100 is benign even without the mod — the engine treats abs ≥ max as
        /// "level-up ready" and simply stops absorbing. Known cosmetics: the item-menu panel
        /// clamps its abs number to max, and the vanilla "ABS MAX" popup doesn't fire on the
        /// crossing kill (this watcher preempts the engine's clamp path).
        /// </summary>
        public static void MachoSwordTick()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _machoNextTick) return;
            _machoNextTick = now.AddMilliseconds(100);

            if (!_machoBankMigrated) MachoMigrateBankFile();

            // Level-up carry watchers. They run in every mode (weapons level up from menus
            // anywhere) and regardless of Macho ownership, so leftover rolled weapons still
            // carry after the sword is sold. The fast path arms a strike thread at the menu
            // confirm; the 500ms pass is the backstop for level-ups the fast path missed.
            MachoFastCarryTick(now);
            if (now >= _machoNextCarryPass)
            {
                _machoNextCarryPass = now.AddMilliseconds(500);
                MachoCarryPass(now);
            }

            if (now >= _machoNextOwnershipCheck)
            {
                _machoNextOwnershipCheck = now.AddSeconds(2);
                _machoOwned = PlayerOwnsMachoSword();
            }
            // Rollover accumulation (kill ABS past max) is the Macho-owned part of the ability,
            // and only matters in live dungeon play.
            if (!_machoOwned || !Player.InDungeonFloor())
            {
                _machoShadowRecord = 0;
                return;
            }
            MachoKillRolloverTick();
        }

        private static void MachoKillRolloverTick()
        {
            // Resolve the ACTIVE character's equipped inventory record — kill ABS lands there.
            int ch = Player.CurrentCharacterNum();
            if ((uint)ch > 5) { _machoShadowRecord = 0; return; }
            int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                            WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
            if ((uint)equipSlot > 9) { _machoShadowRecord = 0; return; }
            long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
            int id = Memory.ReadUShort(rec);
            int max = MachoMaxExp(id, Memory.ReadShort(rec + WeaponCollision.InventoryWeaponLevelOffset));
            int cur = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponAbsOffset);

            if (rec != _machoShadowRecord || id != _machoShadowId)
            {
                // Equipped weapon changed — adopt its stored abs as the truth and re-latch the
                // HP array so pre-existing corpses can't read as fresh kills.
                _machoShadowRecord = rec;
                _machoShadowId = id;
                _machoTrueAbs = cur;
                for (int s = 0; s < _machoPrevHp.Length; s++) _machoPrevHp[s] = 0;
                return;
            }

            bool drainMode = Memory.ReadInt(WeaponCollision.AbsRollover.UserStatusBase +
                                            WeaponCollision.AbsRollover.TransformStateOffset) == 10;
            bool eligible = max > 0 && !drainMode && !MachoIsAbslessWeapon(id);

            // 1) Kills first — so a fast engine grant that already landed isn't counted twice.
            int grants = MachoCollectKillGrants(ch, eligible);
            if (grants > 0)
            {
                int cap = WeaponCollision.AbsRollover.RolloverFactor * max;
                _machoTrueAbs = Math.Min(_machoTrueAbs + grants, cap);
                _machoLastGrantAt = DateTime.UtcNow;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: +{grants} ABS (kill) — weapon {id} at {_machoTrueAbs}/{max}");
            }
            // 2) Sync up to engine activity (its own below-max grants).
            if (cur > _machoTrueAbs) _machoTrueAbs = cur;
            // 3) Adopt engine reductions (level-up reset, transform-drain kills) — but NOT
            //    while a detected kill's engine grant may still be in flight (grants land on
            //    the slot-release frame, up to a few seconds after our HP-latch detection;
            //    adopting down in that window loses the in-flight amount if the NEXT kill
            //    crosses max). The carry watcher re-applies level-up carries on its own.
            else if (cur < _machoTrueAbs && grants == 0 &&
                     DateTime.UtcNow - _machoLastGrantAt > TimeSpan.FromSeconds(4))
                _machoTrueAbs = cur;

            // 4) Materialize the rollover. Only ever needed above the vanilla max; a value ≥ max
            //    also makes the engine's pending grant for these kills skip itself natively.
            if (_machoTrueAbs > max && _machoTrueAbs > cur)
                Memory.WriteUShort(rec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)_machoTrueAbs);
        }

        // One pass over the 16 enemy slots: HP > 0 → ≤ 0 crossings are deaths. Returns the summed
        // engine-equivalent ABS grant of this tick's eligible kills.
        private static int MachoCollectKillGrants(int activeChar, bool eligible)
        {
            int total = 0;
            bool backFloor = Memory.ReadInt(WeaponCollision.AbsRollover.BackFloorDoubleAddr) != 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                int prev = _machoPrevHp[s];
                _machoPrevHp[s] = hp;
                if (prev <= 0 || hp > 0) continue;   // no death crossing on this slot
                // Still in its death animation (RenderStatus ≥ 1): the engine's own grant (on the
                // -1 release frame) hasn't run yet, so our write below always lands first.
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) < 1) continue;
                if (!eligible) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.KillerCharId)) != activeChar) continue;
                int give = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs));
                if (give <= 0) continue;
                if (backFloor) give <<= 1;
                if ((Memory.ReadInt(WeaponCollision.AbsRollover.SlotStatusFlagsAddr(s)) &
                     WeaponCollision.AbsRollover.AbsBonusFlag) != 0)
                    give = (int)(give * WeaponCollision.AbsRollover.AbsBonusMult);
                total += give;
            }
            return total;
        }

        // The fast carry path. The engine commits level++/abs=0 on the message-DISMISS press —
        // the same input that redraws the gauge (pinned by the LvUpDiag trace 2026-07-06:
        // SetLevelUpWeaponData zeroes abs, then WeaponMenuSelect's phase-7 handler bumps the
        // level, all in one frame). A reactive write can therefore never fully win; this path
        // arms at the CONFIRM (flow kind 0, set by SetLevelUpValue ~2s before the commit, while
        // the record still holds the rolled abs) and hands the payout to a dedicated STRIKE
        // THREAD polling the record every few ms — the carry lands within a frame of the
        // commit, keeping the 0 on screen for at most a single frame. This tick remains the
        // arm/disarm bookkeeper and the payout backstop should the thread die.
        private static void MachoFastCarryTick(DateTime now)
        {
            if (_machoFlowRec == 0)
            {
                if (Memory.ReadShort(WeaponCollision.LevelUpFlowKind) != WeaponCollision.FlowKindLevelUp) return;
                int ch = Memory.ReadByte(WeaponCollision.MenuSelectedCharacter);
                int slot = Memory.ReadByte(WeaponCollision.MenuSelectedWeaponSlot);
                if ((uint)ch > 5 || (uint)slot > 9) return;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, slot);
                ushort id = Memory.ReadUShort(rec);
                short level = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponLevelOffset);
                int max = MachoMaxExp(id, level);
                if (max <= 0) return;                 // not a real weapon record
                int abs = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponAbsOffset);
                if (abs <= max) return;               // no rollover on the leveling weapon — vanilla handles it
                _machoFlowId = id;
                _machoFlowPrevLevel = level;
                _machoFlowCarry = Math.Min(abs, WeaponCollision.AbsRollover.RolloverFactor * max) - max;
                _machoFlowArmedAt = now;
                _machoFlowRec = rec;                  // set last — arms the strike thread's loop
                if (_machoStrikeThread == null || !_machoStrikeThread.IsAlive)
                {
                    _machoStrikeThread = new Thread(MachoCarryStrikeLoop) { IsBackground = true };
                    _machoStrikeThread.Start();
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: level-up confirmed on weapon {id} with {abs}/{max} ABS — carry {_machoFlowCarry} armed (strike thread up)");
            }
            else
            {
                ushort id = Memory.ReadUShort(_machoFlowRec);
                short level = Memory.ReadShort(_machoFlowRec + WeaponCollision.InventoryWeaponLevelOffset);
                short abs = Memory.ReadShort(_machoFlowRec + WeaponCollision.InventoryWeaponAbsOffset);
                if (id != _machoFlowId || now - _machoFlowArmedAt > TimeSpan.FromSeconds(15))
                {
                    _machoFlowRec = 0;   // weapon changed under us, or the flow never committed
                }
                else if (level > _machoFlowPrevLevel && abs < _machoFlowCarry)
                {
                    // Backstop payout (the strike thread normally beats this by ~100ms).
                    Memory.WriteUShort(_machoFlowRec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)_machoFlowCarry);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Macho Sword: fast carry (backstop) — weapon {id} starts its new level at {_machoFlowCarry} ABS");
                    _machoFlowRec = 0;
                }
                else if (level > _machoFlowPrevLevel && abs >= _machoFlowCarry)
                {
                    _machoFlowRec = 0;   // strike thread already paid out (or engine kept enough)
                }
            }
        }

        // Strike thread: lives only while a rolled level-up is in flight. Polls the armed record
        // every few ms so the carry write lands within a frame of the engine's dismissal commit —
        // the tightest reaction PINE allows without patching the commit itself (the abs reset is
        // a hardcoded `sh $zero` with no data operand, and the user prefers the flash-then-carry
        // presentation over NOPing it).
        private static void MachoCarryStrikeLoop()
        {
            try
            {
                while (true)
                {
                    long rec = _machoFlowRec;
                    if (rec == 0) return;                              // disarmed (paid out / timeout / changed)
                    if (Memory.ReadUShort(rec) != _machoFlowId) { Thread.Sleep(4); continue; } // let the tick disarm
                    short level = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponLevelOffset);
                    if (level > _machoFlowPrevLevel)
                    {
                        short abs = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponAbsOffset);
                        if (abs < _machoFlowCarry)
                            Memory.WriteUShort(rec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)_machoFlowCarry);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: strike carry — weapon {_machoFlowId} starts its new level at {_machoFlowCarry} ABS");
                        _machoFlowRec = 0;
                        return;
                    }
                    Thread.Sleep(4);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Macho Sword: strike thread error — " + ex.Message);
            }
        }

        // Watches all 60 bag records for level-ups and carries the rolled-over surplus into the
        // new level: level up at 116/100 → the new level's gauge starts at 16. In practice the
        // engine has already committed level++ AND its abs reset by the time the level bump is
        // visible here (play-tested: the menu showed 0/newMax while the old stability-wait
        // version held back), so the carry is written IMMEDIATELY on detection — no visible
        // "0 then suddenly 16" gap. Because the engine's post-level abs write location is
        // still unknown (NOT in SetLevelUpValue or the CWeaponLevelUp flow states), a short
        // guard window follows: if something re-zeroes the field below the carry within ~5s,
        // it is re-applied. A path that natively leaves MORE than the carry (e.g. Powerup
        // Powder keeping the rolled value) is never overwritten — the write only ever raises.
        private static void MachoCarryPass(DateTime now)
        {
            for (int ch = 0; ch < 6; ch++)
            {
                for (int slot = 0; slot < 10; slot++)
                {
                    long rec = WeaponCollision.AbsRollover.RecordAddr(ch, slot);
                    ushort id = Memory.ReadUShort(rec);
                    bool validWeapon = id >= 257 && id <= 376;
                    short level = validWeapon ? Memory.ReadShort(rec + WeaponCollision.InventoryWeaponLevelOffset) : (short)0;
                    short abs = validWeapon ? Memory.ReadShort(rec + WeaponCollision.InventoryWeaponAbsOffset) : (short)0;

                    MachoSnap prev = _machoSnaps[ch, slot];
                    ref MachoCarry pend = ref _machoCarries[ch, slot];

                    if (pend.Active)
                    {
                        if (!validWeapon || id != pend.Id)
                            pend.Active = false;                       // weapon gone/changed — drop the guard
                        else
                        {
                            if (abs < pend.Carry)                      // a late engine reset undid the carry — re-apply
                            {
                                Memory.WriteUShort(rec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)pend.Carry);
                                abs = (short)pend.Carry;
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                    $"Macho Sword: level-up carry re-applied — weapon {id} at {pend.Carry} ABS");
                            }
                            if (++pend.GuardPasses >= 10) pend.Active = false;   // ~5s guard window
                        }
                    }
                    else if (_machoSnapsPrimed && validWeapon && id == prev.Id && level > prev.Level)
                    {
                        int prevMax = MachoMaxExp(id, prev.Level);
                        int rolled = prev.Abs;
                        if (prev.RolledAbs > rolled && (now - prev.RolledAt) < TimeSpan.FromSeconds(8))
                            rolled = prev.RolledAbs;
                        if (prevMax > 0)
                        {
                            int expected = Math.Max(0, Math.Min(rolled, WeaponCollision.AbsRollover.RolloverFactor * prevMax) - prevMax);
                            // Settle only the cases that are OURS to settle: a commit that
                            // zeroed a rollover below its carry (the normal case), or one that
                            // left the rolled value fully in place. A path that natively kept a
                            // below-max abs (e.g. an item-menu Powerup Powder) is left alone.
                            bool resetSkipped = abs == rolled && rolled >= prevMax;
                            if (abs != expected && (resetSkipped || abs < expected))
                            {
                                Memory.WriteUShort(rec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)expected);
                                abs = (short)expected;
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                    $"Macho Sword: weapon {id} leveled with {rolled}/{prevMax} ABS — new level starts at {expected}");
                            }
                            if (expected > 0)
                                pend = new MachoCarry { Active = true, Id = id, Carry = expected, GuardPasses = 0 };
                        }
                    }

                    MachoSnap snap = new MachoSnap { Id = id, Level = level, Abs = abs, RolledAbs = prev.RolledAbs, RolledAt = prev.RolledAt };
                    if (validWeapon && id == prev.Id && abs > MachoMaxExp(id, level)) { snap.RolledAbs = abs; snap.RolledAt = now; }
                    else if (id != prev.Id) { snap.RolledAbs = 0; snap.RolledAt = DateTime.MinValue; }
                    _machoSnaps[ch, slot] = snap;
                }
            }
            _machoSnapsPrimed = true;
        }

        // One-time migration from the short-lived "banked surplus" build: its sidecar file held
        // rollover OUTSIDE the records — fold any surviving surplus back into the matching
        // record's abs field, then delete the file.
        private static void MachoMigrateBankFile()
        {
            _machoBankMigrated = true;
            try
            {
                if (!System.IO.File.Exists(MachoBankFile)) return;
                foreach (string line in System.IO.File.ReadAllLines(MachoBankFile))
                {
                    string[] p = line.Split(',');
                    if (p.Length != 5) continue;
                    int ch = int.Parse(p[0]); int slot = int.Parse(p[1]);
                    int id = int.Parse(p[2]); int surplus = int.Parse(p[4]);
                    if ((uint)ch > 5 || (uint)slot > 9 || surplus <= 0) continue;
                    long rec = WeaponCollision.AbsRollover.RecordAddr(ch, slot);
                    if (Memory.ReadUShort(rec) != id) continue;
                    int max = MachoMaxExp(id, Memory.ReadShort(rec + WeaponCollision.InventoryWeaponLevelOffset));
                    if (max <= 0) continue;
                    int abs = Memory.ReadShort(rec + WeaponCollision.InventoryWeaponAbsOffset);
                    int merged = Math.Min(abs + surplus, WeaponCollision.AbsRollover.RolloverFactor * max);
                    if (merged > abs)
                    {
                        Memory.WriteUShort(rec + WeaponCollision.InventoryWeaponAbsOffset, (ushort)merged);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: migrated {surplus} banked ABS back into weapon {id} ({merged}/{max})");
                    }
                }
                System.IO.File.Delete(MachoBankFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Macho Sword: bank migration failed — " + ex.Message);
            }
        }

        // ── ABS rollover EE-code patches ─────────────────────────────────────────────────────
        // Surgical patches (display + the level-up commit) for rolled records (abs > max); all verified
        // against the SCUS_971.11 disassembly and rewritten IN PLACE (no code cave):
        //  • DrawWepDamageDraw (0x1F8D30) — the weapon-menu WHP/ABS panel. Vanilla clamps abs
        //    into $s2 once and feeds BOTH the gauge width and the number. Patch: NOP the clamp
        //    (por $s2,$s1 @+0x98) so the final DrawMenuNumber($s2) draws the RAW abs ("116/100"),
        //    and rebuild the width block (@+0xB4, 10 words) to fold min($s2,$s1) into its own
        //    99*abs/max computation — the div-by-zero guard it replaces is redundant (max ≥ 1 is
        //    guaranteed by the preceding bgtz fallback). The full-gauge blink compare (bne
        //    $s1,$s2 @+0x174) becomes abs >= max (slt+bnez) so rolled weapons blink too.
        //  • topStatusInfo (0x1B04F0) — the battle-HUD abs bar. Vanilla computes fill =
        //    abs*(len/max) with NO clamp (the overdraw seen in play-testing). Patch: min(abs,max)
        //    before the mtc1, using the blez delay slot + two FPU-hazard nops as free slots (an
        //    integer op fills a COP1 scheduling gap exactly as well as a nop).
        // ⚠ PINE writes to EE CODE pages crash PCSX2 when the target code is HOT (recompiled
        // blocks live). The floor-load window did NOT save these two functions (crashed on
        // floor load, 2026-07-06 — by then the menu/HUD draw code had already run, unlike the
        // ToanKey case that window was verified on), so this is applied from
        // MainMenuThread.ApplyNewChanges at in-game entry instead, while the menu/HUD draw
        // code is still cold for the session. Original words are read-verified first: a
        // mismatch (different game build / emulator state) logs once and never writes.
        private struct AbsCodePatch { public long Addr; public uint[] Orig; public uint[] New; }
        private static readonly AbsCodePatch[] _absDisplayPatches =
        {
            // DrawWepDamageDraw: NOP the abs clamp (por $s2, $s1 — taken when abs > max)
            new AbsCodePatch { Addr = 0x201F8DC8, Orig = new uint[] { 0x72209628 }, New = new uint[] { 0x00000000 } },
            // DrawWepDamageDraw: gauge width = 99*min(abs,max)/max (replaces 99*s2/s1 + the
            // redundant div-by-zero guard: slt/move/movn prepend, bnez/nop/break dropped)
            new AbsCodePatch
            {
                Addr = 0x201F8DE4,
                Orig = new uint[] { 0x00121140, 0x00521821, 0x00031080, 0x00431023, 0x0051001A,
                                    0x16200002, 0x00000000, 0x000001CD, 0x00001012, 0xAFA200A0 },
                New  = new uint[] { 0x0232082A,   // slt  $at, $s1, $s2      (at = max < abs)
                                    0x02401821,   // move $v1, $s2
                                    0x0221180B,   // movn $v1, $s1, $at      (v1 = min(abs, max))
                                    0x00031140,   // sll  $v0, $v1, 5
                                    0x00431821,   // addu $v1, $v0, $v1      (33×)
                                    0x00031080,   // sll  $v0, $v1, 2
                                    0x00431023,   // subu $v0, $v0, $v1      (99×)
                                    0x0051001A,   // div  $zero, $v0, $s1
                                    0x00001012,   // mflo $v0                (LO access interlocks)
                                    0xAFA200A0 }, // sw   $v0, 0xa0($sp)
            },
            // DrawWepDamageDraw: full-gauge blink at abs >= max instead of abs == max
            new AbsCodePatch
            {
                Addr = 0x201F8EA4,
                Orig = new uint[] { 0x1632001C, 0x00000000 },  // bne $s1, $s2, +0x1C; nop
                New  = new uint[] { 0x0251082A, 0x1420001B },  // slt $at, $s2, $s1; bnez $at, +0x1B
            },
            // topStatusInfo: clamp the HUD abs-bar fill to the bar length (min(abs,max) before mtc1)
            new AbsCodePatch { Addr = 0x201B0BC8, Orig = new uint[] { 0x00000000 }, New = new uint[] { 0x8FA200B8 } }, // lw   $v0, 0xb8($sp)  (max; blez delay slot)
            new AbsCodePatch { Addr = 0x201B0BD0, Orig = new uint[] { 0x00000000 }, New = new uint[] { 0x0043082A } }, // slt  $at, $v0, $v1   (fills the mtc1→cvt gap)
            new AbsCodePatch { Addr = 0x201B0BD8, Orig = new uint[] { 0x8FA200B8 }, New = new uint[] { 0x0041180B } }, // movn $v1, $v0, $at   (replaces the lw moved up)
        };
        private static bool _absPatchMismatchLogged;

        /// <summary>Applies the ABS display patches. Called from ApplyNewChanges (in-game
        /// entry), while the target menu/HUD draw code is still cold for the session — writing
        /// HOT EE code crashes PCSX2 (floor-load application crashed in testing). Idempotent:
        /// each patch is skipped when already applied and refused (log once) when the original
        /// words don't match this build.</summary>
        public static void ApplyAbsCodePatches()
        {
            foreach (AbsCodePatch p in _absDisplayPatches)
            {
                bool applied = true, pristine = true;
                for (int i = 0; i < p.Orig.Length; i++)
                {
                    uint w = (uint)Memory.ReadInt(p.Addr + i * 4L);
                    if (w != p.New[i]) applied = false;
                    if (w != p.Orig[i]) pristine = false;
                }
                if (applied) continue;                 // this emulator session already has it
                if (!pristine)
                {
                    if (!_absPatchMismatchLogged)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: ABS display patch @0x{p.Addr:X8} found unexpected code — not patching");
                    _absPatchMismatchLogged = true;
                    continue;
                }
                for (int i = 0; i < p.New.Length; i++)
                    Memory.WriteUInt(p.Addr + i * 4L, p.New[i]);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: ABS display patch applied @0x{p.Addr:X8} ({p.New.Length} words)");
            }
        }

        // C# replica of GetWeaponMaxExp (ELF 0x232080): live static-table base (+0x30, SIGNED
        // char) + level × step (+0x32, short), clamped to 1..999 with the engine's own ≤0 → 99
        // fallback. Reads the live table so any modded base values are honored. Returns 0 for
        // ids outside the weapon table.
        private static int MachoMaxExp(int weaponId, int level)
        {
            int row = weaponId - Weapons.daggerid;
            if (row < 0 || row > 118) return 0;
            int baseAbs = (sbyte)Memory.ReadByte(Weapons.abs + row * Weapons.weaponoffset);
            int step = Memory.ReadShort(Weapons.absadd + row * Weapons.weaponoffset);
            long max = baseAbs + (long)level * step;
            if (max > 999) max = 999;
            if (max < 1) max = 99;
            return (int)max;
        }

        // The engine grants no ABS to the six default weapons (CheckDefaultWeapon) — nor to
        // their broken forms (id − 1). The Serpent Sword's flag-gated no-ABS state (game flag
        // 0x30 unset) is NOT replicated: without native grants its abs never approaches max, so
        // the rollover path self-limits there anyway.
        private static bool MachoIsAbslessWeapon(int id)
        {
            return id == Weapons.daggerid     || id == Weapons.daggerid - 1
                || id == Weapons.woodenid     || id == Weapons.woodenid - 1
                || id == Weapons.malletid     || id == Weapons.malletid - 1
                || id == Weapons.goldringid   || id == Weapons.goldringid - 1
                || id == Weapons.stickid      || id == Weapons.stickid - 1
                || id == Weapons.machinegunid || id == Weapons.machinegunid - 1;
        }

        private static bool PlayerOwnsMachoSword()
        {
            if (Player.Weapon.GetCurrentWeaponId() == Items.machosword)
                return true;

            for (int i = 0; i < 10; i++)
                if (Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * i)) == Items.machosword)
                    return true;

            for (int i = 0; i < 30; i++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * i)) == Items.machosword)
                    return true;

            return false;
        }

        // ── Dark Cloud "Guard Crush" ───────────────────────────────────────────────────────
        // Per-slot snapshot of each enemy's original guard-window flags (captured on first sight) so they can
        // be restored when Dark Cloud is put away — the flag is armed once at spawn, so if we didn't restore,
        // enemies would stay unguarded (until they respawn) even after switching weapons. Keyed by species id
        // to catch slot reuse.
        private static readonly ushort[,] _dcGuardSnap = new ushort[EnemyAddresses.FloorSlots.Count, EnemyAddresses.GuardWindows.WindowCount];
        private static readonly ushort[] _dcGuardSpecies = new ushort[EnemyAddresses.FloorSlots.Count];
        private static readonly bool[] _dcGuardCaptured = new bool[EnemyAddresses.FloorSlots.Count];
        private static byte _dcLastFloor = 0xFF;

        /// <summary>
        /// Ability Name: Guard Crush (Dark Cloud)
        /// While Dark Cloud is wielded, Toan cuts through any enemy's guard — every hit lands even when the
        /// enemy is in its guard-motion frames. The enemy still animates its block; the hit simply connects and
        /// the damage flinch shatters it.
        ///
        /// Mechanism (see EnemyAddresses.GuardWindows): a guarding enemy blocks a hit when its current motion
        /// frame is inside a registered guard window and that window's flag (MMU+slot*0x20+0x60550) is set —
        /// CheckDmg (0x1D9F10) then negates the hit. The flag is written ONCE at spawn, so this watcher zeroes it
        /// data-side for every active enemy while Dark Cloud is in Toan's hands, and restores the captured
        /// original when it isn't (sidekick out, paused, unequipped, or dungeon exit) so enemies block normally
        /// with other weapons. No code patch; the frame values are left untouched.
        /// </summary>
        public static void DarkCloud()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                ushort dcEquipped = Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride);
                if (dcEquipped != Items.darkcloud && dcEquipped != Items.seventhheaven)  // 7th Heaven inherits Guard Crush
                    break;

                byte floor = Memory.ReadByte(Addresses.checkFloor);
                if (floor != _dcLastFloor)
                {
                    _dcLastFloor = floor;
                    for (int s = 0; s < _dcGuardCaptured.Length; s++) _dcGuardCaptured[s] = false;
                }

                // No enemy guards Toan's hits while Dark Cloud is out; hand off (restore guards) for sidekicks/pause.
                bool active = Player.CurrentCharacterNum() == Player.ToanId && !Player.CheckDunIsPaused();
                DarkCloudDriveGuards(active);
                Thread.Sleep(16);
            }

            // Restore every captured guard on unequip / dungeon exit so enemies block normally again.
            DarkCloudDriveGuards(false);
        }

        private static void DarkCloudDriveGuards(bool breakGuard)
        {
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) < 1)
                {
                    _dcGuardCaptured[slot] = false;   // slot went inactive — re-capture whoever spawns next
                    continue;
                }
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                if (!_dcGuardCaptured[slot] || _dcGuardSpecies[slot] != species)
                {
                    // Capture the ORIGINAL flags before we ever zero them (guard armed at spawn, so first sight = original).
                    for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                        _dcGuardSnap[slot, w] = Memory.ReadUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w));
                    _dcGuardSpecies[slot] = species;
                    _dcGuardCaptured[slot] = true;
                }
                for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                {
                    ushort want = breakGuard ? (ushort)0 : _dcGuardSnap[slot, w];
                    long addr = EnemyAddresses.GuardWindows.FlagAddr(slot, w);
                    if (Memory.ReadUShort(addr) != want) Memory.WriteUShort(addr, want);
                }
            }
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

            byte[] bag = Memory.ReadBytesBatch(WeaponCollision.InventoryWeaponSlot0Id,
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

        // ── 7 Branch Sword "Sevenfold Rite" ────────────────────────────────────────────────
        private static DateTime _sevenBranchNextTick = DateTime.MinValue;
        private static bool _sbFactorBoosted = false;
        private static readonly byte[][] _sbSnapshots = new byte[10][]; // last-seen 7BS bag records
        private static readonly bool[] _sbSphereSeen = new bool[WeaponCollision.AttachBoard.ScanCount];
        private static readonly DateTime[] _sbPendingSince = new DateTime[WeaponCollision.AttachBoard.ScanCount];
        private static bool _sbInitialized = false;

        // HandleSevenBranchBreak outcomes
        private const int SbPending = 0;   // sphere seen but its source sword hasn't vanished yet
        private const int SbKept    = 1;   // resolved: +7 or higher — sphere kept as-is (already 77%)
        private const int SbUndone  = 2;   // resolved: below +7 — break undone, sphere cleared

        /// <summary>
        /// Ability Name: Sevenfold Rite (7 Branch Sword)
        /// The 7 Branch Sword refuses Status Break below +7, and breaking it at +7 or higher
        /// transfers 77% of its stats into the SynthSphere instead of the normal 60%. Data-only
        /// (EE code pokes crash PCSX2 — see the warning in WeaponAddresses); no persistent stat
        /// is ever spoofed.
        ///  • 77% transfer: while the weapon menu is OPEN with a 7BS selected, the shared
        ///    stat-transfer factor float (0x2A1890) is set to 0.77 and restored to 0.6 otherwise.
        ///    SetStatusBreak reads it live at confirm, so the engine itself builds the
        ///    boosted sphere. The float is shared with cape/fishing-line physics (which
        ///    only run unpaused), so it's only ever 1.0 while a menu is up.
        ///  • Below-+7 refusal: the engine still lets the player pick Status Break below +7, so
        ///    it's refused after the fact. The break writes the sphere immediately but only
        ///    empties the bag slot 1-2s later (CWeaponLevelUp::Step case 5, in place, id -> 0xFFFF).
        ///    A new 7BS-sourced sphere is held PENDING until its source sword vanishes; then if
        ///    the source was below +7 the break is undone (restore the snapshot into the emptied
        ///    slot, clear the sphere, message). At +7+ nothing is needed — the float already made
        ///    the sphere 77%. The break animation still plays on a refused break (cosmetic).
        /// The Status Break hover hint explains the rule whenever a 7BS is selected (mes-data).
        /// </summary>
        public static void SevenBranchTick()
        {
            // The selection / factor / cursor-pin logic below runs EVERY call (main loop ~50ms)
            // so the confirm-cursor pin reacts fast enough to beat a Right+X input. Only the
            // heavier bag/board undo scan at the end is throttled to ~250ms.
            byte gameMode = Memory.ReadByte(Addresses.mode);
            bool menuOpen =
                (gameMode == 2 && Memory.ReadByte(Addresses.townMode) == 8) ||
                (gameMode == 3 && Memory.ReadByte(Addresses.dungeonMode) == 2) ||
                Player.CheckIsWorldMapMenu();

            bool selected = false;
            int selLevel = -1;
            if (Memory.ReadByte(WeaponCollision.MenuSelectedCharacter) == Player.ToanId)
            {
                byte sel = Memory.ReadByte(WeaponCollision.MenuSelectedWeaponSlot);
                if (sel < 10)
                {
                    long rec = WeaponCollision.InventoryWeaponSlot0Id +
                        sel * WeaponCollision.InventoryWeaponSlotStride;
                    if (Memory.ReadUShort(rec) == Items.sevenbranchsword)
                    {
                        selected = true;
                        selLevel = Memory.ReadUShort(rec + WeaponCollision.InventoryWeaponLevelOffset);
                    }
                }
            }
            WeaponDescriptions.SetStatusBreakHint(selected);
            // Pre-swap the SynthSphere "acquired" message line to the "resists" text whenever a
            // below-+7 7BS is selected, so the popup after an interrupted break IS the refusal
            // message (proactive: applied long before a break can be confirmed; no timing race).
            WeaponDescriptions.SetBreakResultResists(selected && selLevel >= 0 && selLevel < 7);

            // Below +7: interrupt the break flow mid-animation, before it "completes". The
            // confirm and the break animation start normally, but while the CWeaponLevelUp flow
            // sits in its animation state (state 5 — lasts 1-2s, trivially caught at this poll
            // rate) we advance it straight to the wind-down state (6). That skips case 5
            // entirely: the sword is never deleted and the "SynthSphere created!" presentation
            // never fires — the flow just winds down and exits through its own native path.
            // The sphere (written to the board at confirm) is cleared here as well. At +7 and
            // above the flow is untouched.
            if (menuOpen && selected && selLevel >= 0 && selLevel < 7 &&
                Memory.ReadUShort(WeaponCollision.LevelUpFlowKind) == WeaponCollision.FlowKindStatusBreak &&
                Memory.ReadUShort(WeaponCollision.LevelUpFlowState) == WeaponCollision.BreakStateAnimation)
            {
                Memory.WriteUShort(WeaponCollision.LevelUpFlowState, WeaponCollision.BreakStateWindDown);

                // Erase the just-created 7BS sphere from the attachment board
                for (int i = 0; i < WeaponCollision.AttachBoard.ScanCount; i++)
                {
                    long entry = WeaponCollision.AttachBoard.Base + (long)i * WeaponCollision.AttachBoard.Stride;
                    if (Memory.ReadUShort(entry + WeaponCollision.AttachBoard.EntryItemId) ==
                            WeaponCollision.AttachBoard.SynthSphereId &&
                        Memory.ReadUShort(entry + WeaponCollision.AttachBoard.EntrySourceId) ==
                            Items.sevenbranchsword &&
                        !_sbSphereSeen[i])
                    {
                        Memory.WriteByteArray(entry, new byte[WeaponCollision.AttachBoard.Stride]);
                        break;
                    }
                }

                // Feedback comes from the flow's own message window: the "acquired" line was
                // pre-swapped to the "resists" text above, so the popup that follows explains
                // the refusal in-menu.
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"7 Branch Sword: status break at +{selLevel} interrupted mid-animation (needs +7)");
            }

            // 77% transfer factor (shared float — only ever boosted while a menu is open)
            bool boost = menuOpen && selected;
            if (boost != _sbFactorBoosted)
            {
                _sbFactorBoosted = boost;
                Memory.WriteFloat(WeaponCollision.StatusBreakFactorFloat,
                    boost ? WeaponCollision.StatusBreakFactorSeven
                          : WeaponCollision.StatusBreakFactorDefault);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    (boost ? "7 Branch Sword: status-break factor 0.6 -> 0.77"
                           : "7 Branch Sword: status-break factor restored to 0.6"));
            }

            // Undo watcher (backstop): throttle the heavy bag/board reads to ~250ms.
            if (DateTime.UtcNow < _sevenBranchNextTick) return;
            _sevenBranchNextTick = DateTime.UtcNow.AddMilliseconds(250);

            byte[] bag = Memory.ReadBytesBatch(WeaponCollision.InventoryWeaponSlot0Id,
                10 * WeaponCollision.InventoryWeaponSlotStride);
            byte[] board = Memory.ReadBytesBatch(WeaponCollision.AttachBoard.Base,
                WeaponCollision.AttachBoard.ScanCount * WeaponCollision.AttachBoard.Stride);
            if (bag == null || board == null ||
                bag.Length < 10 * WeaponCollision.InventoryWeaponSlotStride ||
                board.Length < WeaponCollision.AttachBoard.ScanCount * WeaponCollision.AttachBoard.Stride)
                return;

            for (int idx = 0; idx < WeaponCollision.AttachBoard.ScanCount; idx++)
            {
                int off = idx * WeaponCollision.AttachBoard.Stride;
                bool isSevenSphere =
                    BitConverter.ToUInt16(board, off + WeaponCollision.AttachBoard.EntryItemId) ==
                        WeaponCollision.AttachBoard.SynthSphereId &&
                    BitConverter.ToUInt16(board, off + WeaponCollision.AttachBoard.EntrySourceId) ==
                        Items.sevenbranchsword;

                bool markSeen = isSevenSphere;
                if (isSevenSphere && !_sbSphereSeen[idx] && _sbInitialized)
                {
                    if (_sbPendingSince[idx] == DateTime.MinValue)
                        _sbPendingSince[idx] = DateTime.UtcNow;

                    int outcome = HandleSevenBranchBreak(idx, bag);
                    if (outcome == SbPending)
                    {
                        if (DateTime.UtcNow - _sbPendingSince[idx] < TimeSpan.FromSeconds(8))
                            markSeen = false;                       // not resolved — retry next tick
                        else
                        {
                            _sbPendingSince[idx] = DateTime.MinValue; // pre-existing sphere; stop trying
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                "7 Branch Sword: sphere never matched a vanished sword — left untouched");
                        }
                    }
                    else
                    {
                        _sbPendingSince[idx] = DateTime.MinValue;
                        if (outcome == SbUndone) markSeen = false;    // sphere was cleared
                    }
                }
                else if (!isSevenSphere)
                {
                    _sbPendingSince[idx] = DateTime.MinValue;
                }
                _sbSphereSeen[idx] = markSeen;
            }
            _sbInitialized = true;

            for (int s = 0; s < 10; s++)
            {
                int off = s * WeaponCollision.InventoryWeaponSlotStride;
                if (BitConverter.ToUInt16(bag, off) == Items.sevenbranchsword)
                {
                    byte[] snap = new byte[WeaponCollision.InventoryWeaponSlotStride];
                    Array.Copy(bag, off, snap, 0, snap.Length);
                    _sbSnapshots[s] = snap;
                }
                else
                {
                    _sbSnapshots[s] = null;
                }
            }
        }

        /// <summary>
        /// Resolves a newly seen 7BS-sourced SynthSphere at board index <paramref name="idx"/>
        /// once its source sword has vanished from the bag: below +7 -> undo the break; +7 or
        /// higher -> keep it (the factor float already made it 77%). Returns
        /// <see cref="SbPending"/> while the source sword is still present.
        /// </summary>
        private static int HandleSevenBranchBreak(int idx, byte[] bag)
        {
            int donor = -1;
            for (int s = 0; s < 10; s++)
            {
                if (_sbSnapshots[s] != null &&
                    BitConverter.ToUInt16(bag, s * WeaponCollision.InventoryWeaponSlotStride) !=
                        Items.sevenbranchsword)
                {
                    donor = s;
                    break;
                }
            }
            if (donor < 0) return SbPending;   // source sword still in the bag (animation running)

            byte[] snap = _sbSnapshots[donor];
            int level = BitConverter.ToUInt16(snap, WeaponCollision.InventoryWeaponLevelOffset);
            if (level >= 7)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"7 Branch Sword: status break at +{level} — 77% sphere (factor float)");
                return SbKept;
            }

            long sphereAddr = WeaponCollision.AttachBoard.Base + (long)idx * WeaponCollision.AttachBoard.Stride;
            long slotAddr = WeaponCollision.InventoryWeaponSlot0Id +
                            donor * WeaponCollision.InventoryWeaponSlotStride;
            Memory.WriteByteArray(slotAddr, snap);
            Memory.WriteByteArray(sphereAddr, new byte[WeaponCollision.AttachBoard.Stride]);
            Array.Copy(snap, 0, bag, donor * WeaponCollision.InventoryWeaponSlotStride, snap.Length);
            Dayuppy.DisplayMessage("The 7 Branch Sword resists!\nIt only breaks at +7 or above.", 2, 34, 5000);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"7 Branch Sword: status break at +{level} undone (needs +7)");
            return SbUndone;
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
        /// Ability Name: Quick Draw (Small Sword, Tsukikage, Heaven's Cloud)
        /// While a Quick Draw sword is wielded, Toan's opening swing comes out almost instantly:
        /// once the first combo attack (action state 0x24) starts, the animation frame cursor
        /// (<see cref="WeaponCollision.AnimFrameCursor"/>) is snapped forward past the wind-up
        /// to just before the hit window. ToanKey_Play gates the forward step-in (frames
        /// 820–820.5), weapon trail (824–825), swing sound (825) and hit window (825–828) off
        /// that same cursor, so skipping 820.5 → 824 keeps all of them — only the wind-up
        /// disappears (~14 game ticks → ~3). Follow-up combo hits (actions 0x25–0x28) and the
        /// charge attacks are untouched. Tsukikage and Heaven's Cloud inherit the effect for
        /// lineage reasons (the Small Sword builds up into them), on top of their own effects.
        /// </summary>
        public static void SmallSword()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride);
                if (equippedId != Items.smallsword && equippedId != Items.tsukikage &&
                    equippedId != Items.heavenscloud)
                    break;

                if (Player.CurrentCharacterNum() == Player.ToanId &&
                    !Player.CheckDunIsPaused() &&
                    Memory.ReadInt(WeaponCollision.ChargeActionState) == WeaponCollision.ActionComboFirst)
                {
                    float frame = Memory.ReadFloat(WeaponCollision.AnimFrameCursor);
                    // Wait until the engine's one-shot step-in write (820–820.5) has run, then
                    // fast-forward. The cursor advances 0.3/tick, so at least one poll always
                    // lands inside the window; a late catch just skips a little less.
                    if (frame >= WeaponCollision.Combo1WindupSettled &&
                        frame < WeaponCollision.Combo1QuickDrawTarget)
                        Memory.WriteFloat(WeaponCollision.AnimFrameCursor,
                            WeaponCollision.Combo1QuickDrawTarget);
                }

                // Tighter poll than the other effect loops: latency here directly delays the swing.
                Thread.Sleep(8);
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

        // ── 7th Heaven "Divine Guard" ──────────────────────────────────────────────────────
        // Which BST shot types (34) we've downgraded 3→2 (global table). Which per-enemy melee dmg-cols we've
        // downgraded (per floor). Captured/species keyed like the Dark Cloud guard-crush to catch slot reuse.
        private static readonly bool[] _shBstDown = new bool[BehaviorScriptTable.Count];
        private static readonly bool[,] _shMeleeDown = new bool[EnemyAddresses.FloorSlots.Count, EnemyAddresses.EnemyAttackParams.MaxCols];
        private static readonly bool[] _shMeleeCaptured = new bool[EnemyAddresses.FloorSlots.Count];
        private static readonly ushort[] _shMeleeSpecies = new ushort[EnemyAddresses.FloorSlots.Count];
        private static byte _shLastFloor = 0xFF;

        /// <summary>
        /// Ability Name: Divine Guard (7th Heaven)
        /// While 7th Heaven is wielded, Toan's guard is perfect — it blocks EVERY enemy attack and projectile,
        /// including the heavy "type 3" hits that normally break guard. Mechanism: BtCheckDamageProc only lets a
        /// guard block reaction-type 2/4 attacks; type-3 ones bypass it. This watcher downgrades every incoming
        /// attack's reaction type from 3 to 2 while the sword is equipped — melee via each enemy's dmg-col param
        /// (EnemyAttackParams.ReactionAddr) and projectiles via the shot-effect table (BehaviorScriptTable +0x44)
        /// — so nothing is unguardable. Side effect (accepted): unguarded, those attacks now knockback instead of
        /// knocking you down (softer). The reaction types are set once at spawn, so this holds with no race; the
        /// originals are restored (3) on unequip / dungeon exit. 7th Heaven also inherits Dark Cloud's Guard Crush
        /// (its guard-break vs enemies) for lineage reasons — see CustomEffects.DarkCloud + the Dungeon dispatch.
        /// </summary>
        public static void SeventhHeaven()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponCollision.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        toanSlot * WeaponCollision.InventoryWeaponSlotStride) != Items.seventhheaven)
                    break;

                SeventhHeavenSoftenAttacks(true);
                Thread.Sleep(100);
            }
            SeventhHeavenSoftenAttacks(false);   // restore original reaction types on unequip / exit
        }

        private static void SeventhHeavenSoftenAttacks(bool active)
        {
            // Projectiles: downgrade every type-3 shot type in the shot-effect table (BST +0x44) to type-2.
            for (int i = 0; i < BehaviorScriptTable.Count; i++)
            {
                long addr = 0x20000000 | (uint)BehaviorScriptTable.FieldAddress(i, BehaviorScriptTable.HitReactionType);
                if (active && !_shBstDown[i])
                {
                    if (Memory.ReadInt(addr) == 3)
                    { Memory.WriteInt(addr, 2); _shBstDown[i] = true; }
                }
                else if (!active && _shBstDown[i])
                {
                    if (Memory.ReadInt(addr) == 2) Memory.WriteInt(addr, 3);
                    _shBstDown[i] = false;
                }
            }

            // Melee: downgrade each enemy's type-3 dmg-col reaction to type-2 (captured once per enemy per floor).
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _shLastFloor)
            {
                _shLastFloor = floor;
                for (int s = 0; s < _shMeleeCaptured.Length; s++)
                {
                    _shMeleeCaptured[s] = false;
                    for (int c = 0; c < EnemyAddresses.EnemyAttackParams.MaxCols; c++) _shMeleeDown[s, c] = false;
                }
            }
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                bool live = active &&
                    Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) >= 1;
                if (live)
                {
                    ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                    if (_shMeleeCaptured[slot] && _shMeleeSpecies[slot] == species) continue; // already handled this enemy
                    _shMeleeSpecies[slot] = species;
                    _shMeleeCaptured[slot] = true;
                    uint[] rx = Memory.ReadUIntBatch(EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, 0),
                                                     EnemyAddresses.EnemyAttackParams.MaxCols);
                    for (int col = 0; col < EnemyAddresses.EnemyAttackParams.MaxCols; col++)
                        if (rx[col] == 3)
                        {
                            Memory.WriteInt(EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, col), 2);
                            _shMeleeDown[slot, col] = true;
                        }
                }
                else if (_shMeleeCaptured[slot])
                {
                    // Slot went inactive (enemy gone) or we're restoring on unequip: put type-3s back only when
                    // restoring (!active) — a dead enemy's stale param is overwritten on the next spawn anyway.
                    for (int col = 0; col < EnemyAddresses.EnemyAttackParams.MaxCols; col++)
                        if (_shMeleeDown[slot, col])
                        {
                            if (!active)
                            {
                                long a = EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, col);
                                if (Memory.ReadInt(a) == 2) Memory.WriteInt(a, 3);
                            }
                            _shMeleeDown[slot, col] = false;
                        }
                    _shMeleeCaptured[slot] = false;
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
                //Initialize the damage
                int damage = Player.Weapon.GetCurrentWeaponAttack() + Player.Weapon.GetCurrentWeaponMagic();

                //The energy ball only starts growing once the charge is fully built (first flash); from
                //there its size tracks the Mobius damage multiplier (M = damage / baseDamage). baseDamage
                //is the un-boosted attack+magic, floored at 1 to avoid divide-by-zero.
                int baseDamage = System.Math.Max(1, damage);
                bool fullyCharged = false;
                float ballScale = 1.0f;

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
                        //The flash marks the charge as fully built — from here the ball may grow.
                        fullyCharged = true;

                        //Display current damage
                        Dayuppy.DisplayMessage(message, height, width, sleep + 500);

                        Thread.Sleep(sleep);

                        //Reset Flash
                        Memory.WriteUShort(chargeGlowTimer, 0);
                    }

                    //Once fully charged, grow the energy ball in step with the damage multiplier the Mobius
                    //ramp has reached (scale = 1 + (M-1)*perMultiple, clamped). Re-applied every tick so the
                    //effect stays sized as the charge is held. See Weapons.SetRubyBallScale.
                    if (fullyCharged)
                    {
                        float m = (float)damage / baseDamage;
                        ballScale = 1.0f + (m - 1.0f) * WeaponCollision.RubyBallGrowthPerMultiple;
                        if (ballScale > WeaponCollision.RubyBallMaxScale) ballScale = WeaponCollision.RubyBallMaxScale;
                        if (ballScale < 1.0f) ballScale = 1.0f;
                        Weapons.SetRubyBallScale(ballScale);
                    }

                    Thread.Sleep(100);
                }

                //Charge released. Freeze the final size and re-apply it so the fired orbs (same effect pool
                //as the ball) fly at the grown size, and inflate enemy body radii so the orbs' COLLISION
                //grows to match (equivalent to a bigger damage sphere; see Weapons.MaintainRubyOrbHitbox).
                float finalBallScale = ballScale;
                if (fullyCharged)
                {
                    Weapons.SetRubyBallScale(finalBallScale);
                    Weapons.MaintainRubyOrbHitbox(finalBallScale);
                }

                //Wait for the fired orbs to actually spawn before tracking them. The release animation takes
                //a moment (the held ball is killed, then the shots grab pool slots), so the old approach of
                //using the slot list captured at CHARGE START raced it — when the list was empty the reset
                //below ran instantly and snapped the just-fired orbs back to 1× (and skipped their damage).
                //Poll the live flags instead; time out in case the charge was interrupted without firing.
                List<int> liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                for (int wait = 0; liveOrbs.Count == 0 && wait < 60; wait++)   // up to ~3s (fire lands ~1.5s in)
                {
                    Thread.Sleep(50);
                    if (Player.Ruby.IsChargingAttack()) break;                 // interrupted → recharging already
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //Drive the boosted damage into every live orb until they all expire (slots re-read each tick
                //so late-spawning second orbs are covered too). Keep the enemy hitbox inflation fresh while
                //the orbs fly (covers enemies that spawn mid-flight).
                //Tick period: the engine inits an orb's damage at spawn and never rewrites it, so the only
                //race is spawn→our-next-tick; a point-blank orb can hit within a frame, so the period must
                //stay UNDER one frame (16.7ms @60fps). 10ms ≈ 0.6 frames of worst-case stale damage. PINE
                //writes aren't frame-synced, so exactly matching 16.7ms wouldn't align to anything anyway.
                int hitboxTick = 0;
                while (liveOrbs.Count > 0)
                {
                    //A NEW charge starting is the hand-off signal: its held ball is an active pool slot, so
                    //without this check the loop would never exit (blocking Dungeon from spawning a fresh
                    //MobiusRing), the new ball would inherit this charge's scale, and this loop would write
                    //THIS charge's damage into the new shot — a full size+power carry-over exploit. Break,
                    //reset below, and let the dispatcher start a clean ramp for the new charge. Any old orbs
                    //still flying keep their (already latched) boosted damage but snap to 1× visuals — brief
                    //and acceptable.
                    if (Player.Ruby.IsChargingAttack()) break;

                    foreach (int id in liveOrbs)
                        Memory.WriteInt(RubyOrbs.Orb0.damage + 4 * id, damage);
                    if (fullyCharged && ++hitboxTick % 20 == 0)
                        Weapons.MaintainRubyOrbHitbox(finalBallScale);
                    Thread.Sleep(10);
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //All orbs expired (or a new charge took over) — snap the effect pool back to its original
                //size so the next charge starts from a clean 1× template, and restore enemy hitboxes.
                Weapons.SetRubyBallScale(1.0f);
                Weapons.RestoreRubyOrbHitbox();
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
