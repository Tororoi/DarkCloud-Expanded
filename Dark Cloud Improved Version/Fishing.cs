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
        internal static readonly Random Rng = new Random();
        internal static byte[] fishArray = new byte[5];

        // Bait detection
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
        private static int maxFishSize = 0;

        // Fish steering — per-slot timestamps for heading writes
        private static readonly DateTime[] _lastSteerTime         = new DateTime[5];
        private static readonly DateTime[] _lastMatatakiSteerTime = new DateTime[5];

        // Fish model pointer cache — snapshotted lazily on first redirect
        private static readonly int[]  _originalModelPointers = new int[FishModelTable.Count];
        private static readonly bool[] _modelPointerCached    = new bool[FishModelTable.Count];

        // ---- Session lifecycle ----

        /// <summary>
        /// Called when the player enters a fishing area. Resets quest state and snapshots the bag inventory.
        /// </summary>
        internal static void OnSessionStart()
        {
            fishingQuestCheck = false;
            questActive.Clear();
            TakeBagSnapshot();
            CheckMardanSword();
            // fishingTriggerIndex is not a reliable area identifier — observed values are inconsistent.
            // Area disambiguation (e.g. Peanut Pond vs Matataki Waterfall) uses player position instead.
            float posX = Memory.ReadFloat(Addresses.positionX);
            float posY = Memory.ReadFloat(Addresses.positionY);
            float posZ = Memory.ReadFloat(Addresses.positionZ);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSession] playerPos=({posX:F2},{posY:F2},{posZ:F2})");
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

        // ---- Per-tick entry point ----

        /// <summary>
        /// Called from TownCharacter every fishing tick. On the first call, reads slot data, activates the active
        /// quest, rolls slots, applies Mardan bonuses, and scales fish sizes. Every tick delegates to
        /// <see cref="CheckFishingQuest"/> and <see cref="SteerFishToPlayer"/>.
        /// </summary>
        /// <param name="areaId">Area ID used to look up the <see cref="AreaFishData"/> configuration.</param>
        internal static void InitFishingSession(int areaId)
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
                // Apply the Mardan sword FP boost to all non-Garayan fish. Must be done after rolling so bonus applies to new spawns.
                ApplyMardanBonus(areaData.SlotBase, areaData.SlotCount);
                // FishPhaseLogger.GiveBaitForTesting();
                FishPhaseLogger.LogFishSession(areaId, areaData.SlotBase, areaData.SlotCount);
                // Arise Mardan ability: scale fish sizes up. Must be done after rolling and bonuses so the boost applies to final sizes.
                if (mardanSwordId == Items.arisemardan)
                    ScaleFishSizes(areaData.SlotBase, areaData.SlotCount, 2f);
                FishPhaseLogger.OnSessionStart(areaData.SlotBase, areaData.SlotCount);
                fishingQuestCheck = true;
            }
            CheckFishingQuest(areaData);
            // FishPhaseLogger.PollSlotDynamics(areaData);
            SteerFishToPlayer(areaData);
        }

        // ---- Quest ----

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
                if (Memory.ReadByte(slotAddr) == 255 && !fishCaught[slotIndex] &&
                    Memory.ReadByte(FishingState.FishCatchConfirmAddr) == FishingState.FishCatchConfirm_Active)
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
        /// Sets the fish-acquired flag byte for <paramref name="caughtFishId"/> in the mod memory region.
        /// </summary>
        /// <param name="caughtFishId">Fish ID of the species that was just caught.</param>
        internal static void FishAcquiredFlag(byte caughtFishId)
        {
            Memory.WriteByte(Addresses.fishAcquiredFlagsBase + caughtFishId, 1);
        }

        // ---- Slot initialization ----

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
        /// Writes all known <see cref="FishData"/> fields into the slot at <paramref name="slotStart"/>.
        /// Null fields are skipped so the game's native value is preserved.
        /// Size is randomized in <c>[MinSize, MaxSize]</c> and scale floats are updated to match.
        /// </summary>
        /// <param name="slotStart">Absolute memory address of the slot's base.</param>
        /// <param name="fishId">Fish ID byte to write at offset 0x000.</param>
        /// <param name="fishData">Species data to write into the slot.</param>
        private static void WriteSlotData(int slotStart, byte fishId, FishData fishData)
        {
            float minSize = fishData.MinSize ?? 5.0f;
            float maxSize = fishData.MaxSize ?? minSize;
            float size = minSize + (float)(Rng.NextDouble() * Math.Max(0, maxSize - minSize));
            Memory.WriteByte(slotStart, fishId);
            if (fishData.ScaleDivisor.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleDivisor, fishData.ScaleDivisor.Value);
            if (fishData.BaseSize.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaseSize, fishData.BaseSize.Value);
            if (fishData.MaxSize.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.MaxSize, fishData.MaxSize.Value);
            if (fishData.BaseFp.HasValue)
                Memory.WriteInt(slotStart + FishSlotOffsets.BaseFp, fishData.BaseFp.Value);
            if (fishData.MaxFp.HasValue)
                Memory.WriteInt(slotStart + FishSlotOffsets.MaxFp, fishData.MaxFp.Value);
            if (fishData.BaitAffEvy.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffEvy, fishData.BaitAffEvy.Value);
            if (fishData.BaitAffMimi.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffMimi, fishData.BaitAffMimi.Value);
            if (fishData.BaitAffPrickly.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffPrickly, fishData.BaitAffPrickly.Value);
            if (fishData.BaitAffThrobbingCherry.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffThrobbingCherry,
                    fishData.BaitAffThrobbingCherry.Value);
            if (fishData.BaitAffGooeyPeach.HasValue)
                Memory.WriteFloat(slotStart + FishSlotOffsets.BaitAffGooeyPeach, fishData.BaitAffGooeyPeach.Value);
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
            float scaleDivisor = fishData.ScaleDivisor ?? 0f;
            Memory.WriteFloat(slotStart + FishSlotOffsets.Size,      size);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleModel, scaleDivisor > 0f ? size / scaleDivisor : 0f);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleFixed, size / 25f);
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

        // ---- Aesthetics ----

        /// <summary>
        /// Scales each fish slot's size by a curve-based multiplier: 1× at the floor rising to
        /// <paramref name="scaleFactor"/>× at max size (quadratic in normalized position).
        /// Fish near their minimum size get almost no boost; only near-max rolls approach the full factor.
        /// Logs original vs scaled values per slot.
        /// </summary>
        internal static void ScaleFishSizes(int slotBase, int slotCount, float scaleFactor)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * Addresses.fishSlotStride;
                byte fishId = Memory.ReadByte(slotStart);
                float originalSize = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                if (originalSize <= 0f) continue;

                float floor = FishDatabase.TryGetValue(fishId, out FishData fishData) && fishData.MinSize.HasValue
                    ? fishData.MinSize.Value : 0f;
                float maxSize = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float range   = maxSize - floor;

                float t          = range > 0f ? Math.Max(0f, originalSize - floor) / range : 0f;
                float multiplier = 1f + (scaleFactor - 1f) * t * t;
                float scaledSize = originalSize * multiplier;

                float scaleRatio = scaledSize / originalSize;
                Memory.WriteFloat(slotStart + FishSlotOffsets.Size,      scaledSize);
                Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleModel,
                    Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleModel) * scaleRatio);
                Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleFixed,
                    Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleFixed) * scaleRatio);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SizeScale] slot={slotIndex} {FishDatabase.GetName(fishId)} " +
                    $"floor={floor:F1} max={maxSize:F1} orig={originalSize:F4} scaled={scaledSize:F4} " +
                    $"({(int)(originalSize*10)}→{(int)(scaledSize*10)}cm)");
            }
        }

        /// <summary>
        /// Redirects the model table pointer for <paramref name="fishId"/> to the shared shadow model.
        /// Snapshots the original pointer the first time it is called for a given ID so
        /// <see cref="RestoreFishModel"/> can recover it without a live read.
        /// </summary>
        internal static void SetFishModelToShadow(byte fishId)
        {
            int ptrAddr = FishModelTable.PointerAddrForId(fishId);
            if (ptrAddr == -1) return;
            if (!_modelPointerCached[fishId])
            {
                _originalModelPointers[fishId] = Memory.ReadInt(ptrAddr);
                _modelPointerCached[fishId]    = true;
            }
            Memory.WriteInt(ptrAddr, (int)(FishShadowModel.EeRamStringAddr - FishModelTable.PtrBias));
        }

        /// <summary>
        /// Restores the model table pointer for <paramref name="fishId"/> to the value snapshotted
        /// by the first call to <see cref="SetFishModelToShadow"/> for that ID.
        /// No-ops if the pointer was never redirected in this session.
        /// </summary>
        internal static void RestoreFishModel(byte fishId)
        {
            int ptrAddr = FishModelTable.PointerAddrForId(fishId);
            if (ptrAddr == -1 || !_modelPointerCached[fishId]) return;
            Memory.WriteInt(ptrAddr, _originalModelPointers[fishId]);
        }

        // ---- Fish behavior ----

        /// <summary>
        /// Writes the player's world position into every fish slot's patrol waypoint once per second,
        /// directing fish toward the player. Resets the waypoint refresh timer to prevent the game
        /// from overwriting the target before the fish arrives.
        /// </summary>
        private static void SteerFishToPlayer(AreaFishData areaData)
        {
            bool hasPassiveSteering = areaData.Id == FishingAreaDatabase.MatatakiWaterfall.Id ||
                                      areaData.Id == FishingAreaDatabase.QueensHarbor.Id;
            if (!hasMardanSword && !hasPassiveSteering) return;

            float playerX = Memory.ReadFloat(Addresses.positionX);
            float playerY = Memory.ReadFloat(Addresses.positionY);

            // Nudge all fish toward the player every 10 seconds to keep them within reach without locking them on perfectly.
            if (hasPassiveSteering)
            {
                for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
                {
                    if ((DateTime.UtcNow - _lastMatatakiSteerTime[slotIndex]).TotalSeconds < 10.0) continue;
                    _lastMatatakiSteerTime[slotIndex] = DateTime.UtcNow;

                    int s = areaData.SlotBase + slotIndex * Addresses.fishSlotStride;
                    float fishX = Memory.ReadFloat(s + FishSlotOffsets.LivePosX);
                    float fishY = Memory.ReadFloat(s + FishSlotOffsets.LivePosY);
                    float dx = playerX - fishX;
                    float dy = playerY - fishY;
                    if (dx == 0f && dy == 0f) continue;

                    float angle = (float)Math.Atan2(dy, dx);
                    Memory.WriteFloat(s + FishSlotOffsets.Heading, angle);
                    // Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    //     $"[Steer/Passive] slot={slotIndex} fish={fishArray[slotIndex]} angle={angle:F2}");
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
                // Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                //     $"[Steer/Mardan] slot={slotIndex} fish={fishId} " +
                //     $"aff={affinity:F2} interval={interval:F1}s angle={angle:F2}");
            }
        }

        /// <summary>
        /// Forces all fish slots into the Approaching AI state, directing them toward the hook.
        /// Useful for testing bite behaviour without waiting for natural AI transitions.
        /// The game will override the state on its next AI tick, so call repeatedly if needed.
        /// </summary>
        internal static void ForceApproach(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                Memory.WriteInt(slotBase + slotIndex * Addresses.fishSlotStride + FishSlotOffsets.AiState,
                    FishingState.FishAiState_Approaching);
        }

        // ---- Bait ----

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
        /// Returns the item ID of the bait currently selected, caching the result until the bait slot register changes.
        /// Returns 0 when no bait is equipped (bait slot register is 0xFFFF).
        /// </summary>
        internal static int GetCurrentBaitId()
        {
            // Read as ushort: this address doubles as a cast-event register (0xFFFF = no active cast).
            ushort baitSlotRaw = Memory.ReadUShort(FishingState.OverworldStateAddr);
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
        /// Maps a bait item ID to its <see cref="FishSlotOffsets"/> bait-affinity field offset.
        /// Returns -1 for unknown bait IDs.
        /// </summary>
        private static int BaitIdToOffset(int baitId) => baitId switch
        {
            Items.evy             => FishSlotOffsets.BaitAffEvy,
            Items.mimi            => FishSlotOffsets.BaitAffMimi,
            Items.prickly         => FishSlotOffsets.BaitAffPrickly,
            Items.throbbingcherry => FishSlotOffsets.BaitAffThrobbingCherry,
            Items.gooeypeach      => FishSlotOffsets.BaitAffGooeyPeach,
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
        /// Writes a scaled notice radius for <paramref name="bait"/> into the EE RAM table.
        /// Multiplies <see cref="BaitTableEntry.DefaultRadius"/> rather than reading the current
        /// in-RAM value, because previous writes persist across re-entries and the game never
        /// restores the table from ROM at runtime.
        /// </summary>
        internal static void SetBaitDetectionRadius(BaitTableEntry bait, float multiplier)
        {
            Memory.WriteFloat(bait.Radius, bait.DefaultRadius * multiplier);
        }

        // ---- Mardan ----

        /// <summary>
        /// Scans the bag (10 slots) and storage (30 slots) for any Mardan-series fishing rod.
        /// Sets <see cref="hasMardanSword"/>, <see cref="mardanSwordId"/>, and the internal
        /// <c>mardanMultiplier</c> used by <see cref="ApplyMardanBonus"/>.
        /// Arise Mardan takes priority over Twei, which takes priority over Eins.
        /// </summary>
        internal static void CheckMardanSword()
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
        /// Multiplies the FP reward range (BaseFp/MaxFp) of every non-Garayan slot by <c>mardanMultiplier</c>.
        /// No-ops if the player does not own a Mardan sword.
        /// </summary>
        private static void ApplyMardanBonus(int slotBase, int slotCount)
        {
            if (!hasMardanSword) return;
            // Brief delay to let the game finish writing slot data before we overwrite FP values.
            Thread.Sleep(300);
            int addr = slotBase + FishSlotOffsets.BaseFp;
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

        // ---- Utilities ----

        /// <summary>
        /// Reads the in-game time float and maps it to the corresponding <see cref="TimeOfDay"/> period.
        /// </summary>
        internal static TimeOfDay GetCurrentTimeOfDay()
        {
            float t = Memory.ReadFloat(Addresses.timeofDayWrite);
            if      (t >= 2.5f && t < 5.5f)  return TimeOfDay.Dusk;
            else if (t >= 5.5f && t < 8.5f)  return TimeOfDay.Night;
            else if (t >= 8.5f && t < 11.5f) return TimeOfDay.Morning;
            else                              return TimeOfDay.Afternoon;
        }

        // ---- Game formula simulations ----
        // Pure reimplementations of confirmed ELF logic, used for pre-write calculation and testing.
        // No memory I/O. See ELF VAs in each summary for the authoritative source.

        /// <summary>
        /// Simulates the game's slot-init size roll (ELF 0x00240D60).
        /// Starts at <see cref="FishData.BaseSize"/>, applies an asymmetric RNG offset
        /// (÷4 upward, ÷8 downward), clamps to [0.5×BaseSize, MaxSize], then derives both scale values.
        /// The game RNG source is unknown; this uses <see cref="Rng"/> with a uniform [-1, 1] range
        /// as a close approximation of the observed size distribution.
        /// </summary>
        /// <param name="fish">Species data supplying BaseSize, ScaleDivisor, and MaxSize.</param>
        /// <param name="size">Resulting size (×10 = display cm).</param>
        /// <param name="scaleModel">slot+0x064: Size / ScaleDivisor.</param>
        /// <param name="scaleFixed">slot+0x068: Size / 25.0.</param>
        internal static void SimulateSlotInit(FishData fish, out float size, out float scaleModel, out float scaleFixed)
        {
            float baseSize     = fish.BaseSize      ?? 0f;
            float maxSize      = fish.MaxSize       ?? 0f;
            float scaleDivisor = fish.ScaleDivisor  ?? 0f;

            float rng = (float)(Rng.NextDouble() * 2.0 - 1.0);

            size = baseSize;
            if (rng >= 0f)
                size += rng * (maxSize - baseSize) / 4f;
            else
                size += rng * (maxSize - baseSize) / 8f;

            float floor = 0.5f * baseSize;
            if (size < floor)   size = floor;
            if (size > maxSize) size = maxSize;

            scaleModel = scaleDivisor > 0f ? size / scaleDivisor : 0f;
            scaleFixed = size / 25f;
        }

        /// <summary>
        /// Simulates the game's FP reward formula (ELF 0x00240E80).
        /// Two-segment piecewise linear: below BaseSize, FP scales from 0 up to BaseFp;
        /// above BaseSize, FP scales from BaseFp up to MaxFp at MaxSize.
        /// Truncates to integer, matching the game's float→int conversion.
        /// </summary>
        /// <param name="size">Fish size as stored in the slot (×10 = display cm).</param>
        /// <param name="baseSize">BaseSize field: base size and lower FP anchor.</param>
        /// <param name="maxSize">MaxSize field: maximum size and upper FP anchor.</param>
        /// <param name="fpMin">BaseFp: FP reward at exactly BaseSize.</param>
        /// <param name="fpMax">MaxFp: FP reward at exactly MaxSize.</param>
        internal static int SimulateFpReward(float size, float baseSize, float maxSize, int fpMin, int fpMax)
        {
            float reward;
            if (size < baseSize)
                reward = baseSize > 0f ? fpMin * size / baseSize : 0f;
            else
                reward = (maxSize - baseSize) > 0f
                    ? fpMin + (fpMax - fpMin) * (size - baseSize) / (maxSize - baseSize)
                    : fpMin;
            return (int)reward;
        }

        /// <summary>
        /// Convenience overload of <see cref="SimulateFpReward(float,float,float,int,int)"/>
        /// that reads all parameters from a <see cref="FishData"/> record.
        /// </summary>
        internal static int SimulateFpReward(FishData fish, float size) =>
            SimulateFpReward(size, fish.BaseSize ?? 0f, fish.MaxSize ?? 0f,
                             fish.BaseFp ?? 0, fish.MaxFp ?? 0);
    }
}
