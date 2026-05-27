using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Core fishing mod logic: slot initialization, quest tracking, Mardan sword bonuses,
    /// bait detection, range boost, and diagnostic utilities.
    /// </summary>
    internal static class Fishing
    {
        /// <summary>
        /// Returns the display name for a bait item ID, or <c>"Unknown(N)"</c> if the ID is unrecognised.
        /// </summary>
        internal static string GetBaitName(int id) => id switch
        {
            Items.throbbingcherry => "ThrobbingCherry",
            Items.gooeypeach      => "GooeyPeach",
            Items.bombnuts        => "BombNuts",
            Items.poisonousapple  => "PoisonousApple",
            Items.mellowbanana    => "MellowBanana",
            Items.carrot          => "Carrot",
            Items.potatocake      => "PotatoCake",
            Items.minon           => "Minon",
            Items.battan          => "Battan",
            Items.petitefish      => "PetiteFish",
            Items.evy             => "Evy",
            Items.mimi            => "Mimi",
            Items.prickly         => "Prickly",
            _                     => $"Unknown({id})",
        };

        internal static readonly Random Rng = new Random();
        internal static byte[] fishArray = new byte[5];
        private static ushort[] _bagSnapshot = null;
        private static ushort _lastBaitSlot = 0xFFFF;
        private static int _cachedBaitId = 0;

        // Mardan sword state — set by CheckMardanSword at each session start
        internal static bool hasMardanSword = false;
        internal static int mardanSwordId = 0;  // 278=Eins, 279=Twei, 280=Arise
        private static float mardanMultiplier = 1f;

        // Per-session quest state
        internal static bool fishingQuestCheck = false;
        internal static bool[] fishCaught = new bool[6];
        // Keyed by QuestBase address so multiple quest givers can be active simultaneously.
        private static readonly Dictionary<int, bool> questActive = new Dictionary<int, bool>();
        private static int minFishSize = 0;
        private static DateTime _lastSlotPollTime = DateTime.MinValue;
        private static readonly DateTime[] _lastSteerTime        = new DateTime[5];
        private static readonly DateTime[] _lastMatatakiSteerTime = new DateTime[5];
        private static int maxFishSize = 0;

        /// <summary>
        /// Called when the player enters a fishing area. Resets quest state and snapshots the bag inventory.
        /// </summary>
        internal static void OnSessionStart()
        {
            fishingQuestCheck = false;
            questActive.Clear();
            TakeBagSnapshot();
            CheckMardanSword();
        }

        /// <summary>
        /// Resets per-session bait detection state and restores the native detection radius patch if it was boosted.
        /// </summary>
        internal static void ResetSession()
        {
            _bagSnapshot = null;
            _lastBaitSlot = 0xFFFF;
            _cachedBaitId = 0;
        }

        /// <summary>
        /// Called from TownCharacter every fishing tick. On the first call, reads slot data, activates the active
        /// quest, rolls slots, applies Mardan bonuses, and scales fish sizes. Every tick delegates to
        /// <see cref="CheckFishingQuest"/>.
        /// </summary>
        /// <param name="area">Area ID used to look up the <see cref="AreaFishData"/> configuration.</param>
        public static void InitFishingSession(int areaId)
        {
            if (!FishingAreaDatabase.TryGetValue(areaId, out AreaFishData areaData)) return;
            if (!fishingQuestCheck)
            {
                int slotAddr = areaData.SlotBase;
                for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
                {
                    fishArray[slotIndex] = Memory.ReadByte(slotAddr);
                    slotAddr += Addresses.fishSlotStride;
                    fishCaught[slotIndex] = false;
                }
                if (Memory.ReadByte(areaData.QuestBase) == 1)
                {
                    questActive[areaData.QuestBase] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Currently on {areaData.GiverName}s quest");
                    minFishSize = Memory.ReadByte(areaData.QuestBase + 5);
                    maxFishSize = Memory.ReadByte(areaData.QuestBase + 6);
                }
                // Mardan Twei ability: reroll slots to spawn additional Mardan or Baron Garayan with the native chances.
                if (mardanSwordId == Items.mardantwei || mardanSwordId == Items.arisemardan)
                    RollFishSlots(areaData.SlotBase, areaData.SlotCount, FishDatabase.MardanGarayan.Id, 0.2f);
                // Apply the Mardan sword FP boost to all non-Garayan fish in the area. Must be done after rolling so bonus applies to new spawns.
                ApplyMardanBonus(areaData.SlotBase, areaData.SlotCount);
                // GiveBaitForTesting();
                LogFishSession(areaId, areaData.SlotBase, areaData.SlotCount);
                // Arise Mardan ability: scale fish sizes up to set scale factor, with curve based on the fish's original size. Must be done after all rolling and bonuses so the boost is applied to final sizes.
                if (mardanSwordId == Items.arisemardan)
                    ScaleFishSizes(areaData.SlotBase, areaData.SlotCount);
                fishingQuestCheck = true;
            }
            CheckFishingQuest(areaData);
            PollSlotDynamics(areaData);
            SteerFishToPlayer(areaData);
        }

        /// <summary>
        /// Multiplies the FP reward range (FpMin/FpMax) of every non-Garayan slot by <c>mardanMultiplier</c>.
        /// No-ops if the player does not own a Mardan sword.
        /// </summary>
        private static void ApplyMardanBonus(int slotBase, int slotCount)
        {
            if (!hasMardanSword) return;
            Thread.Sleep(300);
            int addr = slotBase + FishSlotOffsets.FpMin;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == FishDatabase.MardanGarayan.Id ||
                    fishArray[slotIndex] == FishDatabase.BaronGarayan.Id)
                {
                    addr += Addresses.fishSlotStride;
                    continue;
                }
                Memory.WriteInt(addr, (int)(Memory.ReadInt(addr) * mardanMultiplier));
                addr += 4;
                Memory.WriteInt(addr, (int)(Memory.ReadInt(addr) * mardanMultiplier));
                addr += Addresses.fishSlotStride - 4;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Mardan did its thing!");
            }
        }

        /// <summary>
        /// Writes all known <see cref="FishData"/> fields into the slot at <paramref name="slotStart"/>.
        /// Null fields are skipped so the game's native value is preserved.
        /// Size is randomized in <c>[EstimatedMinSize, MaxSize]</c> and scale floats are updated to match.
        /// </summary>
        /// <param name="slotStart">Absolute memory address of the slot's base.</param>
        /// <param name="fishId">Fish ID byte to write at offset 0x000.</param>
        /// <param name="fishData">Species data to write into the slot.</param>
        private static void WriteSlotData(int slotStart, byte fishId, FishData fishData)
        {
            float minSize = fishData.EstimatedMinSize ?? 5.0f;
            float maxSize = fishData.MaxSize ?? minSize;
            float size = minSize + (float)(Rng.NextDouble() * Math.Max(0, maxSize - minSize));
            Memory.WriteByte(slotStart, fishId);
            if (fishData.Unk004.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.Unk004, fishData.Unk004.Value);
            if (fishData.Unk008.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.Unk008, fishData.Unk008.Value);
            if (fishData.MaxSize.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.MaxSize, fishData.MaxSize.Value);
            if (fishData.FpMin.HasValue)
                Memory.WriteInt(slotStart + FishSlotOffsets.FpMin, fishData.FpMin.Value);
            if (fishData.FpMax.HasValue)
                Memory.WriteInt(slotStart + FishSlotOffsets.FpMax, fishData.FpMax.Value);
            if (fishData.BaitAffEvy.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffEvy, fishData.BaitAffEvy.Value);
            if (fishData.BaitAffMimi.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffMimi, fishData.BaitAffMimi.Value);
            if (fishData.BaitAffPrickly.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffPrickly, fishData.BaitAffPrickly.Value);
            if (fishData.BaitAffThrobbingCherry.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffThrobbingCherry,
                    fishData.BaitAffThrobbingCherry.Value);
            if (fishData.BaitAffGooeypeach.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffGooeypeach, fishData.BaitAffGooeypeach.Value);
            if (fishData.BaitAffBombnuts.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffBombnuts, fishData.BaitAffBombnuts.Value);
            if (fishData.BaitAffPoisonousApple.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffPoisonousApple,
                    fishData.BaitAffPoisonousApple.Value);
            if (fishData.BaitAffMellowBanana.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffMellowBanana, fishData.BaitAffMellowBanana.Value);
            if (fishData.BaitAffCarrot.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffCarrot, fishData.BaitAffCarrot.Value);
            if (fishData.BaitAffPotatoCake.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffPotatoCake, fishData.BaitAffPotatoCake.Value);
            if (fishData.BaitAffMinon.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffMinon, fishData.BaitAffMinon.Value);
            if (fishData.BaitAffBattan.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffBattan, fishData.BaitAffBattan.Value);
            if (fishData.BaitAffPetitefish.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffPetitefish, fishData.BaitAffPetitefish.Value);
            Memory.WriteFloat(slotStart + FishSlotOffsets.Size,   size);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleX, size * 0.04f);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleY, size * 0.04f);
        }

        /// <summary>
        /// Rerolls each non-Garayan slot to <paramref name="targetFishId"/>, writing full slot data via
        /// <see cref="WriteSlotData"/>. Garayan slots are left untouched.
        /// </summary>
        /// <param name="areaBase">Base address of the area's first fish slot.</param>
        /// <param name="slotCount">Number of slots to iterate.</param>
        /// <param name="targetFishId">Fish ID to write into each eligible slot.</param>
        /// <param name="baronChance">
        /// Probability [0, 1] that a rerolled slot becomes Baron Garayan instead of <paramref name="targetFishId"/>.
        /// Use 0.2f for the Mardan Twei feature to match native Garayan spawn ratios.
        /// </param>
        internal static void RollFishSlots(int areaBase, int slotCount, byte targetFishId, float baronChance = 0f)
        {
            float timeOfDay = Memory.ReadFloat(Addresses.timeofDayWrite);
            int pct;
            if      (timeOfDay >= 2.5f && timeOfDay < 5.5f)  pct = 5; // Dusk
            else if (timeOfDay >= 5.5f && timeOfDay < 8.5f)  pct = 3; // Night
            else if (timeOfDay >= 8.5f && timeOfDay < 11.5f) pct = 4; // Morning
            else                                              pct = 2; // Afternoon

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == FishDatabase.MardanGarayan.Id ||
                    fishArray[slotIndex] == FishDatabase.BaronGarayan.Id) continue;
                // TEST: force reroll on every non-Garayan slot
                if (Rng.Next(100) < pct)
                {
                    int slotStart = areaBase + (Addresses.fishSlotStride * slotIndex);
                    byte originalId = Memory.ReadByte(slotStart);
                    byte newId = (baronChance > 0f && Rng.NextDouble() < baronChance)
                        ? FishDatabase.BaronGarayan.Id : targetFishId;
                    if (!FishDatabase.TryGetValue(newId, out FishData fishData))
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[RollFishSlots] No FishData for id={newId}, skipping slot {slotIndex}");
                        continue;
                    }
                    fishArray[slotIndex] = newId;
                    WriteSlotData(slotStart, newId, fishData);
                    float size = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[RollFishSlots] slot={slotIndex} {FishDatabase.GetName(originalId)} (id={originalId}) " +
                        $"→ {FishDatabase.GetName(newId)} (id={newId}) " +
                        $"size={size:F4} ({(int)(size*10)}cm) tod={timeOfDay:F2}");
                }
            }
        }

        /// <summary>
        /// Doubles the size and scale of every fish slot in the area. Logs original vs scaled values per slot.
        /// </summary>
        private const float SizeScaleFactor       = 2f;
        private const float SizeScaleFloorDefault = 5.0f;

        internal static void ScaleFishSizes(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId = Memory.ReadByte(slotStart);
                float originalSize = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                if (originalSize <= 0f) continue;

                float floor = FishDatabase.TryGetValue(fishId, out FishData fishData) &&
                    fishData.EstimatedMinSize.HasValue
                    ? fishData.EstimatedMinSize.Value
                    : SizeScaleFloorDefault;
                float maxSize = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float range   = maxSize - floor;

                // Multiplier scales from 1× at the floor up to SizeScaleFactor× at max size.
                // Fish that rolled small get almost no boost; only near-max rolls approach the full factor.
                float t          = range > 0f ? Math.Max(0f, originalSize - floor) / range : 0f;
                float multiplier = 1f + (SizeScaleFactor - 1f) * t * t;
                float scaledSize = originalSize * multiplier;

                float scaleRatio = scaledSize / originalSize;
                Memory.WriteFloat(slotStart + FishSlotOffsets.Size,   scaledSize);
                Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleX,
                    Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleX) * scaleRatio);
                Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleY,
                    Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleY) * scaleRatio);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SizeScale] slot={slotIndex} {FishDatabase.GetName(fishId)} " +
                    $"floor={floor:F1} max={maxSize:F1} orig={originalSize:F4} scaled={scaledSize:F4} " +
                    $"({(int)(originalSize*10)}→{(int)(scaledSize*10)}cm)");
            }
        }

        /// <summary>
        /// Snapshots the bag inventory item IDs before any bait is cast so that
        /// <see cref="DetectBaitFromInventory"/> can identify which bait was consumed by slot disappearance.
        /// Must be called at session start.
        /// </summary>
        private static void TakeBagSnapshot()
        {
            int slotCount = (Addresses.firstBagWeapon - Addresses.firstBagItem) / 2;
            _bagSnapshot = new ushort[slotCount];
            for (int bagSlot = 0; bagSlot < slotCount; bagSlot++)
                _bagSnapshot[bagSlot] = Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BagSnapshot] taken ({slotCount} slots)");
        }

        /// <summary>
        /// Compares the current bag against the snapshot taken by <see cref="TakeBagSnapshot"/> and returns
        /// the item ID of the first slot that transitioned from non-zero to zero (i.e., the bait that was cast).
        /// Returns 0 if no slot changed or no snapshot exists.
        /// </summary>
        private static int DetectBaitFromInventory()
        {
            if (_bagSnapshot == null) return 0;
            for (int bagSlot = 0; bagSlot < _bagSnapshot.Length; bagSlot++)
            {
                if (_bagSnapshot[bagSlot] != 0 && Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2) == 0)
                    return _bagSnapshot[bagSlot];
            }
            return 0;
        }

        /// <summary>
        /// Returns the item ID of the bait currently selected, caching the result until the bait slot register changes.
        /// Returns 0 when no bait is equipped (bait slot register is 0xFFFF).
        /// </summary>
        internal static int GetCurrentBaitId()
        {
            ushort baitSlotRaw = Memory.ReadUShort(0x21D19708);
            if (baitSlotRaw == _lastBaitSlot) return _cachedBaitId;
            _lastBaitSlot = baitSlotRaw;
            // 0xFFFF means the cast-event register cleared — bait may still be in water, so keep the
            // last known ID rather than resetting to 0. Only a new cast (non-0xFFFF) can update the ID.
            if (baitSlotRaw != 0xFFFF)
            {
                int detected = DetectBaitFromInventory();
                if (detected != 0) _cachedBaitId = detected;
            }
            return _cachedBaitId;
        }

        /// <summary>
        /// Reads the in-game time float and maps it to the corresponding <see cref="TimeOfDay"/> period.
        /// </summary>
        private static TimeOfDay GetCurrentTimeOfDay()
        {
            float t = Memory.ReadFloat(Addresses.timeofDayWrite);
            if      (t >= 2.5f && t < 5.5f)  return TimeOfDay.Dusk;
            else if (t >= 5.5f && t < 8.5f)  return TimeOfDay.Night;
            else if (t >= 8.5f && t < 11.5f) return TimeOfDay.Morning;
            else                              return TimeOfDay.Afternoon;
        }

        /// <summary>
        /// [TEST] Adds one of every bait type to the bag at session start so all 13 baits are available.
        /// Bait list from wiki — 13 total:
        /// throbbingcherry(166)  Heela, Niler, Den
        ///   gooeypeach(167)       Gummy, Nonky, Niler
        ///   bombnuts(168)         Gobbler, Niler
        ///   poisonousapple(169)   Mardan Garayan
        ///   mellowbanana(170)     Baku Baku
        ///   evy(193)              Bobo, Bon, Kaiji, Piccoly
        ///   mimi(197)             Baku Baku, Gobbler
        ///   minon(188)            Majority
        ///   petitefish(190)       Majority
        ///   prickly(199)          Majority
        ///   battan(189)           Baku Baku, Den, Gobbler, Gummy, Heela, Negie, Niler, Nonky, Tarton
        ///   carrot(186)           Umadakara
        ///   potatocake(187)       Baron Garayan, Den, Niler, Nonky, Tarton
        /// </summary>
        internal static void GiveBaitForTesting()
        {
            int[] baits = {
                Items.throbbingcherry, Items.gooeypeach,   Items.bombnuts,
                Items.poisonousapple,  Items.mellowbanana, Items.evy,
                Items.mimi,            Items.minon,         Items.petitefish,
                Items.prickly,         Items.battan,        Items.carrot,
                Items.potatocake,
            };
            int bagSlotCount = (Addresses.firstBagWeapon - Addresses.firstBagItem) / 2;
            foreach (int bait in baits)
            {
                bool found = false;
                for (int bagSlot = 0; bagSlot < bagSlotCount && !found; bagSlot++)
                    if (Memory.ReadUShort(Addresses.firstBagItem + bagSlot * 2) == (ushort)bait)
                        found = true;
                if (!found)
                {
                    int availableSlot = Player.Inventory.GetBagItemsFirstAvailableSlot();
                    if (availableSlot >= 0)
                        Memory.WriteUShort(Addresses.firstBagItem + (0x2 * availableSlot), (ushort)bait);
                }
            }
        }

        /// <summary>
        /// Logs all slot fields (species, size, FP range, bait affinities) for every slot in the area.
        /// Called once at session initialization before any mod writes are applied.
        /// </summary>
        internal static void LogFishSession(int areaId, int slotBase, int slotCount)
        {
            TimeOfDay currentTod = GetCurrentTimeOfDay();
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart  = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId    = Memory.ReadByte(slotStart);
                float unk004   = Memory.ReadFloat(slotStart + FishSlotOffsets.Unk004);
                float unk008   = Memory.ReadFloat(slotStart + FishSlotOffsets.Unk008);
                float maxSize  = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float size     = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                int   fpMin    = Memory.ReadInt(slotStart + FishSlotOffsets.FpMin);
                int   fpMax    = Memory.ReadInt(slotStart + FishSlotOffsets.FpMax);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishInfo] area={areaId} slot={slotIndex} {FishDatabase.GetName(fishId)} (id={fishId}) " +
                    $"unk004={unk004:F1} unk008={unk008:F1} max={maxSize:F1}({(int)(maxSize*10)}cm) " +
                    $"size={size:F4} ({(int)(size*10)}cm) fp={fpMin}-{fpMax} tod={currentTod}");
                // bait affinity table — values are bite-likelihood weights (0.0=never, 1.0=normal)
                float affEvy      = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffEvy);
                float affMimi     = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffMimi);
                float affPrickly  = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffPrickly);
                float affCherry   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffThrobbingCherry);
                float affPeach    = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffGooeypeach);
                float affBombnuts = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffBombnuts);
                float affPoison   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffPoisonousApple);
                float affBanana   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffMellowBanana);
                float affCarrot   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffCarrot);
                float affPotato   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffPotatoCake);
                float affMinon    = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffMinon);
                float affBattan   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffBattan);
                float affPetite   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffPetitefish);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BaitAff]  area={areaId} slot={slotIndex} {FishDatabase.GetName(fishId)} " +
                    $"Evy={affEvy:F2} Mimi={affMimi:F2} Prickly={affPrickly:F2} " +
                    $"Cherry={affCherry:F2} Peach={affPeach:F2} " +
                    $"Bomb={affBombnuts:F2} Poison={affPoison:F2} Banana={affBanana:F2} Carrot={affCarrot:F2} " +
                    $"Potato={affPotato:F2} Minon={affMinon:F2} Battan={affBattan:F2} Petite={affPetite:F2}");
            }
            // DumpFishSlot(slotBase, slotCount);
        }

        /// <summary>
        /// Writes <c>0xFF</c> to the fish ID byte of each selected slot, marking it as caught/empty.
        /// </summary>
        /// <param name="slotBase">Base address of the area's first slot.</param>
        /// <param name="slotCount">Total number of slots in the area.</param>
        /// <param name="slotMask">Bitmask of slot indices to clear, e.g. <c>0b0110</c> clears slots 1 and 2.</param>
        internal static void DespawnFishSlots(int slotBase, int slotCount, int slotMask)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if ((slotMask & (1 << slotIndex)) == 0) continue;
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                byte originalId = Memory.ReadByte(slotStart);
                Memory.WriteByte(slotStart, 0xFF);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Despawn] slot={slotIndex} {FishDatabase.GetName(originalId)} (id={originalId}) cleared");
            }
        }

        // ── Slot structure notes from Norune pond dump (2026-05-26) ──────────────────────────────────────────────────
        // Known mapped offsets: 0x000 fishId, 0x004 Unk004, 0x008 Unk008, 0x00C MaxSize,
        //   0x010 FpMin, 0x014 FpMax, 0x018–0x048 bait affinities (13×float),
        //   0x060 Size, 0x064 ScaleX, 0x068 ScaleY,
        //   0x074 Heading, 0x0B0 LivePosY, 0x0B4 LivePosZ, 0x0B8 LivePosX.
        //
        // Unmapped region 0x04C–0x05C:
        //   +0x04C  int 1     — constant across all slots; unknown (flag? type byte?)
        //   +0x050  0xFFFFFFFF — NaN float; constant; possibly uninitialized padding or sentinel
        //   +0x054  int varies — CONFIRMED waypoint refresh timer; counts down to 0 to trigger new patrol
        //              target selection; normal range 0–~150; holding at 9999 prevents re-evaluation
        //              (fish complete initial waypoint but never pick a new one);
        //              initial waypoints are assigned separately at session start
        //   +0x058  int varies — game-controlled live state; normal range ~20–50, fluctuates
        //              non-monotonically each frame (game resets our writes within 1 second);
        //              holding above ~9999 via per-second enforcement prevents biting, suggesting
        //              bite fires when value falls below a threshold; purpose/units unclear
        //   +0x05C  int 0     — constant
        //
        // Movement data (confirmed via live polling — values change each second while fish swim):
        //   +0x074  Heading  — heading angle (radians, ~[-π, π]); drives both facing and movement direction;
        //              game updates it each frame toward active waypoint; writes accepted but game re-steers
        //              between our writes — per-second enforcement creates a directional bias, not a hard lock;
        //              bait attraction overrides normally (its AI also writes through this field)
        //   +0x080  float    — target speed; nonzero while moving toward waypoint, drops to 0 on arrival
        //   +0x084  float    — current velocity; smoothly decelerates toward target speed (visibly coasts to 0)
        //   +0x090  float    — Y world position (live) — matches Addresses.positionY axis
        //   +0x094  float    — Z world position (live) — matches Addresses.positionZ axis (vertical)
        //   +0x098  float    — X world position (live) — matches Addresses.positionX axis
        //   NOTE: all slots read the same pos value each tick — this offset aliases the player position
        //         rather than storing an individual fish live position; actual per-fish XYZ is unknown.
        //   +0x0B0  LivePosY — Y live position (per-fish); (0,0,0) when slot empty;
        //              matches Addresses.positionY axis
        //   +0x0B4  LivePosZ — Z live position (per-fish); observed -25.0 in Norune/Matataki (pond floor depth);
        //              matches Addresses.positionZ axis (vertical)
        //   +0x0B8  LivePosX — X live position (per-fish) — matches Addresses.positionX axis
        //
        // Behavior floats (same across all slots/species in Norune; writes persist — game does NOT restore them):
        //   +0x150  float  7.0  — unknown; tested with 99.0, no visible effect observed yet
        //   +0x154  float 18.0  — unknown; untested
        //   +0x158  float 60.0  — unknown; tested with 240.0, no visible effect observed yet
        //
        // Scale mirrors:
        //   +0x130/0x134/0x138 — three identical floats matching ScaleY (0x068); likely shadow/collision scale
        //   +0x0F0/0x0F4/0x0F8 — constant 1.0 across all slots; possible base-scale identity
        //
        // Spawn chance: NOT stored per slot. Time-of-day spawn weights are computed by game
        //   spawn code from external tables, not accessible via slot dump.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [DIAG] Writes a test integer value to the given slot field offset across all slots in the area.
        /// Use for integer fields (e.g. <c>0x04C</c>); see the float overload for float fields.
        /// </summary>
        /// <param name="offset">Byte offset within each slot to write.</param>
        /// <param name="value">Integer value to write.</param>
        internal static void TestSlotField(int slotBase, int slotCount, int offset, int value)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                Memory.WriteInt(slotBase + slotIndex * Addresses.fishSlotStride + offset, value);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FieldTest] offset=0x{offset:X3} int={value} written to {slotCount} slots");
        }

        /// <summary>
        /// [DIAG] Writes a test float value to the given slot field offset across all slots in the area.
        /// Use for float fields (e.g. <c>0x150–0x158</c>); see the int overload for integer fields.
        /// </summary>
        /// <param name="offset">Byte offset within each slot to write.</param>
        /// <param name="value">Float value to write.</param>
        internal static void TestSlotField(int slotBase, int slotCount, int offset, float value)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                Memory.WriteFloat(slotBase + slotIndex * Addresses.fishSlotStride + offset, value);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FieldTest] offset=0x{offset:X3} float={value} written to {slotCount} slots");
        }

        /// <summary>
        /// [DIAG] Polls and logs live movement/behavior fields for all slots once per second.
        /// Reads heading, speed, velocity, world position, waypoint, behavior floats, and scale mirrors.
        /// </summary>
        private static void PollSlotDynamics(AreaFishData areaData)
        {
            if ((DateTime.UtcNow - _lastSlotPollTime).TotalSeconds < 1.0) return;
            _lastSlotPollTime = DateTime.UtcNow;
            float playerX = Memory.ReadFloat(Addresses.positionX);
            float playerY = Memory.ReadFloat(Addresses.positionY);
            float playerZ = Memory.ReadFloat(Addresses.positionZ);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[Player] pos=({playerX:F1},{playerY:F2},{playerZ:F1})");
            for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
            {
                int s       = areaData.SlotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId = Memory.ReadByte(s);
                float hdg   = Memory.ReadFloat(s + FishSlotOffsets.Heading);
                float spd    = Memory.ReadFloat(s + 0x080);
                float curVel = Memory.ReadFloat(s + 0x084);
                float posX  = Memory.ReadFloat(s + 0x098); // positionX axis
                float posY  = Memory.ReadFloat(s + 0x090); // positionY axis
                float posZ  = Memory.ReadFloat(s + 0x094); // positionZ axis (vertical)
                float fishPosX  = Memory.ReadFloat(s + FishSlotOffsets.LivePosX);
                float fishPosY  = Memory.ReadFloat(s + FishSlotOffsets.LivePosY);
                float fishPosZ  = Memory.ReadFloat(s + FishSlotOffsets.LivePosZ);
                float b150  = Memory.ReadFloat(s + 0x150);
                float b154  = Memory.ReadFloat(s + 0x154);
                float b158  = Memory.ReadFloat(s + 0x158);
                float scl0  = Memory.ReadFloat(s + 0x130);
                float scl1  = Memory.ReadFloat(s + 0x134);
                float scl2  = Memory.ReadFloat(s + 0x138);
                int unk054  = Memory.ReadInt(s + 0x054);
                int unk058  = Memory.ReadInt(s + 0x058);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SlotWatch] slot={slotIndex} {FishDatabase.GetName(fishId)} " +
                    $"hdg={hdg:F3} spd={spd:F3} curVel={curVel:F3} " +
                    $"pos=({posX:F1},{posY:F2},{posZ:F1}) fishPos=({fishPosX:F1},{fishPosY:F1},{fishPosZ:F1}) " +
                    $"b150={b150:F2} b154={b154:F2} b158={b158:F2} " +
                    $"unk054={unk054} unk058={unk058} scl=({scl0:F4},{scl1:F4},{scl2:F4})");
            }
        }

        /// <summary>
        /// Writes the player's world position into every fish slot's patrol waypoint once per second,
        /// directing fish toward the player. Resets the waypoint refresh timer to prevent the game
        /// from overwriting the target before the fish arrives.
        /// </summary>
        private static void SteerFishToPlayer(AreaFishData areaData)
        {
            bool isMatataki = areaData.Id == FishingAreaDatabase.MatatakiWaterfall.Id;
            if (!hasMardanSword && !isMatataki) return;

            float playerX = Memory.ReadFloat(Addresses.positionX);
            float playerY = Memory.ReadFloat(Addresses.positionY);

            // Rebalance Matataki Waterfall fishing so fish are nudged toward the player without being perfectly locked on, which would feel unnatural and reduce challenge.
            if (isMatataki)
            {
                for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
                {
                    if ((DateTime.UtcNow - _lastMatatakiSteerTime[slotIndex]).TotalSeconds < 11.0) continue;
                    _lastMatatakiSteerTime[slotIndex] = DateTime.UtcNow;

                    int s = areaData.SlotBase + slotIndex * Addresses.fishSlotStride;
                    float fishX = Memory.ReadFloat(s + FishSlotOffsets.LivePosX);
                    float fishY = Memory.ReadFloat(s + FishSlotOffsets.LivePosY);
                    float dx = playerX - fishX;
                    float dy = playerY - fishY;
                    if (dx == 0f && dy == 0f) continue;

                    float angle = (float)Math.Atan2(dy, dx);
                    Memory.WriteFloat(s + FishSlotOffsets.Heading, angle);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[Steer/Matataki] slot={slotIndex} fish={fishArray[slotIndex]} angle={angle:F2}");
                }
            }

            // Mardan Eins ability: steer certain fish toward the player with a frequency based on bait affinity.
            if (!hasMardanSword) return;

            double baseSecs = mardanSwordId switch
            {
                Items.arisemardan => 2.0,
                Items.mardantwei  => 3.0,
                _                 => 4.0,
            };

            int baitId     = GetCurrentBaitId();
            int baitOffset = BaitIdToOffset(baitId);

            for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
            {
                byte fishId = fishArray[slotIndex];
                if (fishId != FishDatabase.MardanGarayan.Id &&
                    fishId != FishDatabase.BaronGarayan.Id  &&
                    fishId != FishDatabase.Umadakara.Id) continue;

                int s = areaData.SlotBase + slotIndex * Addresses.fishSlotStride;

                float affinity = baitOffset >= 0 ? Memory.ReadFloat(s + baitOffset) : 0f;
                if (affinity <= 0f) continue;

                double interval = baseSecs / affinity;
                if ((DateTime.UtcNow - _lastSteerTime[slotIndex]).TotalSeconds < interval) continue;
                _lastSteerTime[slotIndex] = DateTime.UtcNow;

                float fishX = Memory.ReadFloat(s + FishSlotOffsets.LivePosX);
                float fishY = Memory.ReadFloat(s + FishSlotOffsets.LivePosY);
                float dx = playerX - fishX;
                float dy = playerY - fishY;
                if (dx == 0f && dy == 0f) continue;

                float angle = (float)Math.Atan2(dy, dx);
                Memory.WriteFloat(s + FishSlotOffsets.Heading, angle);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Steer/Mardan] slot={slotIndex} fish={fishId} " +
                    $"aff={affinity:F2} interval={interval:F1}s angle={angle:F2}");
            }
        }

        /// <summary>
        /// Maps a bait item ID to its <see cref="FishSlotOffsets"/> bait-affinity field offset.
        /// Returns -1 for unknown bait IDs.
        /// </summary>
        private static int BaitIdToOffset(int baitId) => baitId switch
        {
            Items.evy             => FishSlotOffsets.BaitAffEvy,
            Items.mimi            => FishSlotOffsets.BaitAffMimi,
            Items.prickly         => FishSlotOffsets.BaitAffPrickly,
            Items.throbbingcherry => FishSlotOffsets.BaitAffThrobbingCherry,
            Items.gooeypeach      => FishSlotOffsets.BaitAffGooeypeach,
            Items.bombnuts        => FishSlotOffsets.BaitAffBombnuts,
            Items.poisonousapple  => FishSlotOffsets.BaitAffPoisonousApple,
            Items.mellowbanana    => FishSlotOffsets.BaitAffMellowBanana,
            Items.carrot          => FishSlotOffsets.BaitAffCarrot,
            Items.potatocake      => FishSlotOffsets.BaitAffPotatoCake,
            Items.minon           => FishSlotOffsets.BaitAffMinon,
            Items.battan          => FishSlotOffsets.BaitAffBattan,
            Items.petitefish      => FishSlotOffsets.BaitAffPetitefish,
            _                     => -1,
        };

        /// <summary>
        /// [DIAG] Dumps every 4-byte word in the first <c>0x200</c> bytes of each slot as both raw hex and
        /// interpreted float. Used to discover new slot field mappings.
        /// </summary>
        internal static void DumpFishSlot(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId = Memory.ReadByte(slotStart);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SlotDump] slot={slotIndex} {FishDatabase.GetName(fishId)} (id={fishId}) base=0x{slotStart:X8}");
                for (int offset = 0; offset < 0x200; offset += 4)
                {
                    int rawValue   = Memory.ReadInt(slotStart + offset);
                    float floatVal = Memory.ReadFloat(slotStart + offset);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[SlotDump] slot={slotIndex} +0x{offset:X3}  raw=0x{rawValue:X8}  float={floatVal:F4}");
                }
            }
        }

        /// <summary>
        /// Sets the fish-acquired flag byte for <paramref name="caughtFishId"/> in the mod memory region.
        /// </summary>
        /// <param name="caughtFishId">Fish ID of the species that was just caught.</param>
        public static void FishAcquiredFlag(byte caughtFishId)
        {
            Memory.WriteByte(0x21CE4439 + caughtFishId, 1);
        }

        /// <summary>
        /// Scans the bag (10 slots) and storage (30 slots) for any Mardan-series fishing rod.
        /// Sets <see cref="hasMardanSword"/>, <see cref="mardanSwordId"/>, and the internal
        /// <c>mardanMultiplier</c> used by <see cref="ApplyMardanBonus"/>.
        /// Arise Mardan takes priority over Twei, which takes priority over Eins.
        /// </summary>
        public static void CheckMardanSword()
        {
            int foundWeaponId = 0;

            for (int bagSlot = 0; bagSlot < 10 && foundWeaponId == 0; bagSlot++)
            {
                int weaponId = Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * bagSlot));
                if (weaponId == Items.mardaneins || weaponId == Items.mardantwei || weaponId == Items.arisemardan)
                    foundWeaponId = weaponId;
            }

            for (int storageSlot = 0; storageSlot < 30 && foundWeaponId == 0; storageSlot++)
            {
                int weaponId = Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * storageSlot));
                if (weaponId == Items.mardaneins || weaponId == Items.mardantwei || weaponId == Items.arisemardan)
                    foundWeaponId = weaponId;
            }

            if (foundWeaponId == Items.arisemardan)
            {
                hasMardanSword = true;
                mardanSwordId = Items.arisemardan;
                mardanMultiplier = 2f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Arise Mardan");
            }
            else if (foundWeaponId == Items.mardantwei)
            {
                hasMardanSword = true;
                mardanSwordId = Items.mardantwei;
                mardanMultiplier = 1.5f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Mardan Twei");
            }
            else if (foundWeaponId == Items.mardaneins)
            {
                hasMardanSword = true;
                mardanSwordId = Items.mardaneins;
                mardanMultiplier = 1.2f;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Player has Mardan Eins");
            }
            else
            {
                hasMardanSword = false;
                mardanSwordId = 0;
            }
        }

        /// <summary>
        /// Marks the quest complete (state byte → 2) in game memory and increments the Sam
        /// multi-quest counter if the area tracks one.
        /// </summary>
        private static void CompleteQuest(AreaFishData areaData)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
            Memory.WriteByte(areaData.QuestBase, 2);
            if (areaData.QuestsDoneAddr != 0)
            {
                byte questsDone = Memory.ReadByte(areaData.QuestsDoneAddr);
                if (questsDone < 4) Memory.WriteByte(areaData.QuestsDoneAddr, ++questsDone);
            }
        }

        /// <summary>
        /// Catch scan that runs every tick after the session is initialized by <see cref="InitFishingSession"/>.
        /// Detects newly caught fish (slot ID → 0xFF), fires <see cref="FishAcquiredFlag"/>, and evaluates
        /// count-fish or size-range quest completion. Also mirrors the Sam post-loop trigger when active.
        /// </summary>
        private static void CheckFishingQuest(AreaFishData areaData)
        {
            questActive.TryGetValue(areaData.QuestBase, out bool isQuestActive);
            int slotAddr = areaData.SlotBase;
            for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
            {
                if (Memory.ReadByte(slotAddr) == 255 && !fishCaught[slotIndex] && Memory.ReadByte(0x202A26E8) == 12)
                {
                    fishCaught[slotIndex] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Fish caught -> slot={slotIndex} ID: {fishArray[slotIndex]} " +
                        $"({FishDatabase.GetName(fishArray[slotIndex])})");
                    FishAcquiredFlag(fishArray[slotIndex]);
                    if (isQuestActive)
                    {
                        if (Memory.ReadByte(areaData.QuestBase + 1) == 0) // count quest
                        {
                            if (fishArray[slotIndex] == Memory.ReadByte(areaData.QuestBase + 3))
                            {
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                                byte fishRemaining = Memory.ReadByte(areaData.QuestBase + 4);
                                if (--fishRemaining == 0)
                                {
                                    CompleteQuest(areaData);
                                    questActive[areaData.QuestBase] = false;
                                    isQuestActive = false;
                                }
                                else
                                {
                                    Memory.WriteByte(areaData.QuestBase + 4, fishRemaining);
                                }
                            }
                        }
                        else // size quest
                        {
                            int catchSize = (int)Math.Floor(Memory.ReadFloat(slotAddr + FishSlotOffsets.Size) * 10);
                            if (minFishSize <= catchSize && maxFishSize >= catchSize)
                            {
                                CompleteQuest(areaData);
                                questActive[areaData.QuestBase] = false;
                                isQuestActive = false;
                            }
                        }
                    }
                }
                slotAddr += Addresses.fishSlotStride;
            }
            if (areaData.PostLoopSrc != 0 && Memory.ReadByte(areaData.PostLoopSrc) == 1)
                Memory.WriteByte(areaData.PostLoopDst, 1);
        }
    }
}
