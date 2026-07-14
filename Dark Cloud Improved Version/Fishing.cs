using System;
using System.Collections.Generic;
using System.Linq;
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
        internal static bool[] fishCaught = new bool[6];
        // Keyed by QuestBase address so multiple quest givers can be active simultaneously.
        private static readonly Dictionary<int, bool> questActive = new Dictionary<int, bool>();
        private static int minFishSize = 0;
        private static int maxFishSize = 0;

        // Fish steering — per-slot timestamps for heading writes
        private static readonly DateTime[] _lastSteerTime         = new DateTime[5];
        private static readonly DateTime[] _lastMatatakiSteerTime = new DateTime[5];

        // Cached fishing area ID for the current session. Set on first InitFishingSession call,
        // cleared in ResetSession. Matataki area ID 1 is ambiguous (Waterfall vs Peanut Pond)
        // and is resolved via player Y position on first entry.
        private static int _fishingAreaId = -1;

        // Fish model pointer cache — snapshotted lazily on first redirect
        private static readonly int[]  _originalModelPointers = new int[FishModelTable.Count];
        private static readonly bool[] _modelPointerCached    = new bool[FishModelTable.Count];

        // Arise Mardan fish-record max-magic bonus — see UpdateFishRecordsAndAriseBonus.
        // _pendingRecordUpdate defers the post-catch update until after the native
        // SetFishingRank insert has certainly run (it fires during catch resolution).
        private static DateTime _pendingRecordUpdate  = DateTime.MinValue;
        private static bool _ariseBonusInitialized = false;

        // ---- Session lifecycle ----

        /// <summary>
        /// Called when the player enters a fishing area. Resolves and caches the area, sets up all
        /// per-session state, and starts sub-system listeners.
        /// </summary>
        internal static void OnSessionStart(int currentAreaId)
        {
            _fishingAreaId = ResolveFishingSpot(currentAreaId);
            questActive.Clear();
            TakeBagSnapshot();
            CheckMardanSword();
            if (FishingAreas.TryGetValue(_fishingAreaId, out AreaFishData areaData))
            {
                InitSlots(areaData);
                InitQuestState(areaData);
                FishPhaseLogger.LogFishSession(_fishingAreaId, areaData.SlotBase, areaData.SlotCount);
                ActivateMardanSwordAbilities(areaData);
                // Native fish-size smoothing applies to every non-Arise session. Arise sessions already
                // smooth internally via AriseScaleFishSizes, so don't double-apply here.
                if (mardanSwordId != Items.arisemardan)
                    SmoothFishSizes(areaData.SlotBase, areaData.SlotCount);
                FishPhaseLogger.OnSessionStart(areaData.SlotBase, areaData.SlotCount);
            }
            UpdateFishRecordsAndAriseBonus();
            FishDataFarmer.OnSessionDetected();
        }

        /// <summary>
        /// Resets per-session bait detection state and clears all sub-system listeners.
        /// </summary>
        internal static void ResetSession()
        {
            _bagSnapshot = null;
            _lastBaitSlot = 0xFFFF;
            _cachedBaitId = 0;
            _fishingAreaId = -1;
            // Flush any deferred record update from a catch just before quitting the session.
            _pendingRecordUpdate = DateTime.MinValue;
            UpdateFishRecordsAndAriseBonus();
            FishDataFarmer.OnSessionEnded();
            FishPhaseLogger.OnSessionEnd();
        }

        // ---- Per-tick entry point ----

        /// <summary>
        /// Called from TownCharacter on every fishing tick. Delegates to per-tick catch scanning
        /// and fish steering. All session initialization is done in <see cref="OnSessionStart"/>.
        /// </summary>
        internal static void OnFishingTick()
        {
            ProcessPendingRecordUpdate();
            if (_fishingAreaId == -1) return;
            if (!FishingAreas.TryGetValue(_fishingAreaId, out AreaFishData areaData)) return;
            CheckFishingQuest(areaData);
            // FishPhaseLogger.PollSlotDynamics(areaData);
            SteerFishToPlayer(areaData);
        }

        // ---- Quest ----

        /// <summary>
        /// Activates the area quest if its state byte is 1, storing the target size range.
        /// Called once per session from <see cref="OnSessionStart"/> after <see cref="InitSlots"/>.
        /// </summary>
        private static void InitQuestState(AreaFishData areaData)
        {
            if (Memory.ReadByte(areaData.QuestBase) != 1) return;
            questActive[areaData.QuestBase] = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"Currently on {areaData.GiverName}s quest");
            minFishSize = Memory.ReadByte(areaData.QuestBase + 5);
            maxFishSize = Memory.ReadByte(areaData.QuestBase + 6);
        }

        /// <summary>
        /// Catch scan that runs every tick after the session is initialized by <see cref="OnSessionStart"/>.
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
                    Memory.ReadByte(FishingAddresses.FishCatchConfirm) == FishingState.FishCatchConfirm_Active)
                {
                    fishCaught[slotIndex] = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Fish caught -> slot={slotIndex} ID: {fishArray[slotIndex]} " +
                        $"({Fish.GetName(fishArray[slotIndex])})");
                    FishAcquiredFlag(fishArray[slotIndex]);
                    NoteFishRecordDirty();
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
                slotAddr += FishSlotOffsets.Stride;
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
            Memory.WriteByte(FishingAddresses.AcquiredFlagsBase + caughtFishId, 1);
        }

        // ---- Arise Mardan fish-record max-magic bonus ----
        // Each species' best recorded catch grants Arise Mardan bonus MaxMagic, on a curve fit
        // through +1 at the vanilla max size, +12 at the "580cm" equivalent, and the full cap at
        // the Arise 2x size cap: bonus = max(1, round(Cap / (1 + 116 * d^0.954))) where
        // d = (capCm - recordCm) / vanillaMaxCm. Below vanilla max: 0. All sizes floored to
        // whole display cm. Full completion: 15*43 + 2*117 = 879 -> MaxMagic 120 + 879 = 999.

        private const double RecordCurveGamma  = 0.954;
        private const double RecordCurveCoeff  = 116.0;
        private const int    GarayanRecordCap  = 117;  // Mardan Garayan & Baron Garayan
        private const int    NormalRecordCap   = 43;   // the other 15 species
        private const int    AriseBaseMaxMagic = 120;  // vanilla WeaponList[280].MaxMagic

        /// <summary>
        /// Schedules a records/bonus update a few seconds from now. Called on each detected catch;
        /// the delay guarantees the native <c>SetFishingRank</c> insert for that catch has landed
        /// before we dedupe the list.
        /// </summary>
        internal static void NoteFishRecordDirty()
        {
            _pendingRecordUpdate = DateTime.UtcNow.AddSeconds(3);
        }

        /// <summary>Runs a scheduled update once its delay has elapsed. Called every fishing tick.</summary>
        internal static void ProcessPendingRecordUpdate()
        {
            if (_pendingRecordUpdate == DateTime.MinValue || DateTime.UtcNow < _pendingRecordUpdate) return;
            _pendingRecordUpdate = DateTime.MinValue;
            UpdateFishRecordsAndAriseBonus();
        }

        /// <summary>
        /// One-time catch-up so the Arise Mardan MaxMagic bonus (and the deduped records list)
        /// is in place as soon as a save is loaded, without requiring a fishing session first.
        /// Cheap no-op after the first successful run; safe to call every main-loop tick.
        /// </summary>
        internal static void EnsureAriseBonusInitialized()
        {
            if (_ariseBonusInitialized) return;
            if (Memory.ReadInt(FishingRankList.SaveDataPtr) == 0) return; // no save loaded yet
            _ariseBonusInitialized = true;
            UpdateFishRecordsAndAriseBonus();
        }

        /// <summary>
        /// Rewrites the native fishing-records list to one best-catch entry per species (still
        /// sorted largest to smallest — the records screen then shows exactly the per-species
        /// maxes), and applies the resulting max-magic bonus to Arise Mardan's WeaponList entry.
        /// Keeping the list deduped also means the native insert always finds a free slot, so
        /// every new catch enters the list before we fold it in.
        /// </summary>
        internal static void UpdateFishRecordsAndAriseBonus()
        {
            int saveDataNative = Memory.ReadInt(FishingRankList.SaveDataPtr);
            if (saveDataNative == 0) return;
            long listBase = Memory.ToMmu(saveDataNative) + FishingRankList.Offset;

            byte[] raw = Memory.ReadBytesBatch(listBase, FishingRankList.Count * FishingRankList.Stride);
            if (raw == null || raw.Length < FishingRankList.Count * FishingRankList.Stride) return;

            // Index of each species' largest entry. Entries carry 8 trailing bytes we don't
            // understand; tracking indexes lets us preserve them verbatim when compacting.
            var bestEntry = new Dictionary<int, int>();
            for (int i = 0; i < FishingRankList.Count; i++)
            {
                int off = i * FishingRankList.Stride;
                int fishId = BitConverter.ToInt32(raw, off + FishingRankList.EntryFishId);
                float size = BitConverter.ToSingle(raw, off + FishingRankList.EntrySize);
                if (size <= 0f || fishId < 0 || fishId >= FishModelTable.Count) continue;
                if (!bestEntry.TryGetValue(fishId, out int bestIdx) ||
                    size > BitConverter.ToSingle(raw, bestIdx * FishingRankList.Stride + FishingRankList.EntrySize))
                {
                    bestEntry[fishId] = i;
                }
            }

            var ordered = bestEntry.Values
                .OrderByDescending(i => BitConverter.ToSingle(raw, i * FishingRankList.Stride + FishingRankList.EntrySize))
                .ToList();

            byte[] rewritten = new byte[raw.Length];
            for (int i = 0; i < FishingRankList.Count; i++)
            {
                int off = i * FishingRankList.Stride;
                if (i < ordered.Count)
                {
                    Array.Copy(raw, ordered[i] * FishingRankList.Stride, rewritten, off, FishingRankList.Stride);
                }
                else
                {
                    BitConverter.GetBytes(-1).CopyTo(rewritten, off + FishingRankList.EntryFishId);
                    // size and trailing bytes stay 0 — GetFishingRank treats the slot as empty
                }
            }
            if (!rewritten.SequenceEqual(raw))
            {
                Memory.WriteByteArray(listBase, rewritten);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishRecords] deduped rank list to {ordered.Count} per-species entries");
            }

            int totalBonus = 0;
            foreach (KeyValuePair<int, int> kv in bestEntry)
            {
                float size = BitConverter.ToSingle(raw, kv.Value * FishingRankList.Stride + FishingRankList.EntrySize);
                totalBonus += RecordMagicBonus((byte)kv.Key, size);
            }
            int newMaxMagic = Math.Min(999, AriseBaseMaxMagic + totalBonus);
            int fieldAddr = WeaponList.FieldAddr(Items.arisemardan, WeaponList.MaxMagic);
            int oldMaxMagic = Memory.ReadShort(fieldAddr);
            if (oldMaxMagic != newMaxMagic)
            {
                Memory.WriteUShort(fieldAddr, (ushort)newMaxMagic);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[AriseMagic] fish-record bonus +{totalBonus} -> Arise Mardan MaxMagic {oldMaxMagic} -> {newMaxMagic}");
            }
        }

        /// <summary>
        /// Max-magic bonus for one species record. 0 below the vanilla max size, +1 at it, the
        /// full species cap at the Arise 2x cap, hyperbolic in between (see curve constants).
        /// </summary>
        internal static int RecordMagicBonus(byte fishId, float recordSize)
        {
            if (!Fish.TryGetValue(fishId, out FishData fishData) || !fishData.MaxSize.HasValue) return 0;
            int vanillaMaxCm = (int)Math.Floor(fishData.MaxSize.Value * 10f);
            if (vanillaMaxCm <= 0) return 0;   // MissingFish has MaxSize 0
            int cap = fishId == Fish.MardanGarayan.Id || fishId == Fish.BaronGarayan.Id
                ? GarayanRecordCap : NormalRecordCap;
            int capCm = vanillaMaxCm * 2;
            int recordCm = (int)Math.Floor(recordSize * 10f);
            if (recordCm < vanillaMaxCm) return 0;
            if (recordCm >= capCm) return cap;
            double d = (capCm - recordCm) / (double)vanillaMaxCm;
            double f = 1.0 / (1.0 + RecordCurveCoeff * Math.Pow(d, RecordCurveGamma));
            return Math.Max(1, (int)Math.Round(cap * f, MidpointRounding.AwayFromZero));
        }

        // ---- Slot initialization ----

        /// <summary>
        /// Snapshots initial slot fish IDs into <see cref="fishArray"/> and resets <see cref="fishCaught"/>.
        /// <see cref="fishArray"/> is read by both <see cref="CheckFishingQuest"/> (catch detection) and
        /// <see cref="RollFishSlots"/> (Garayan slot filtering), so this must run before either.
        /// </summary>
        private static void InitSlots(AreaFishData areaData)
        {
            int slotAddr = areaData.SlotBase;
            for (int slotIndex = 0; slotIndex < areaData.SlotCount; slotIndex++)
            {
                fishArray[slotIndex] = Memory.ReadByte(slotAddr);
                slotAddr += FishSlotOffsets.Stride;
                fishCaught[slotIndex] = false;
            }
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
            int spawnPercent;
            if      (timeOfDay >= 2.5f && timeOfDay < 5.5f)  spawnPercent = 5; // Dusk
            else if (timeOfDay >= 5.5f && timeOfDay < 8.5f)  spawnPercent = 3; // Night
            else if (timeOfDay >= 8.5f && timeOfDay < 11.5f) spawnPercent = 4; // Morning
            else                                              spawnPercent = 2; // Afternoon

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == Fish.MardanGarayan.Id ||
                    fishArray[slotIndex] == Fish.BaronGarayan.Id) continue;
                if (Rng.Next(100) < spawnPercent)
                {
                    int slotStart = areaBase + (FishSlotOffsets.Stride * slotIndex);
                    byte originalId = Memory.ReadByte(slotStart);
                    byte newId = (baronChance > 0f && Rng.NextDouble() < baronChance)
                        ? Fish.BaronGarayan.Id : targetFishId;
                    if (!Fish.TryGetValue(newId, out FishData fishData))
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[RollFishSlots] No FishData for id={newId}, skipping slot {slotIndex}");
                        continue;
                    }
                    fishArray[slotIndex] = newId;
                    WriteSlotData(slotStart, newId, fishData);
                    float size = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[RollFishSlots] slot={slotIndex} {Fish.GetName(originalId)} (id={originalId}) " +
                        $"→ {Fish.GetName(newId)} (id={newId}) " +
                        $"size={size:F4} ({(int)(size*10)}cm) tod={timeOfDay:F2}");
                }
            }
        }

        /// <summary>
        /// Writes all known <see cref="FishData"/> fields into the slot at <paramref name="slotStart"/>.
        /// Null fields are skipped so the game's native value is preserved.
        /// Size is rolled via the native slot-init formula (<see cref="SimulateSlotInit"/>) so rerolled
        /// slots match the game's own distribution; the smoothing/Arise size effects are applied afterward
        /// over all slots by <see cref="SmoothFishSizes"/> / <see cref="AriseScaleFishSizes"/>.
        /// </summary>
        /// <param name="slotStart">Absolute memory address of the slot's base.</param>
        /// <param name="fishId">Fish ID byte to write at offset 0x000.</param>
        /// <param name="fishData">Species data to write into the slot.</param>
        private static void WriteSlotData(int slotStart, byte fishId, FishData fishData)
        {
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
            SimulateSlotInit(fishData, out float size, out float scaleModel, out float scaleFixed);
            Memory.WriteFloat(slotStart + FishSlotOffsets.Size,       size);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleModel, scaleModel);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleFixed, scaleFixed);
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
                int slotStart = slotBase + slotIndex * FishSlotOffsets.Stride;
                byte originalId = Memory.ReadByte(slotStart);
                Memory.WriteByte(slotStart, 0xFF);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Despawn] slot={slotIndex} {Fish.GetName(originalId)} (id={originalId}) cleared");
            }
        }

        // ---- Aesthetics ----

        // ---- Size adjustment math ----
        // Both effects operate on the size the game has already rolled and clamped to [MinSize, MaxSize].
        // Sizes are in the game's native size units (display cm = floor(size * 10)). Fish only get bigger.

        /// <summary>
        /// Smoothing buff over an arbitrary <c>[base, max]</c> range. Zero at base and at max,
        /// peaks in the upper range; keeps <c>f(base)=base</c>, <c>f(max)=max</c>, <c>f(s)&gt;=s</c>.
        /// Fills the sparse region just below the cap so the distribution ramps into it smoothly.
        /// </summary>
        static float SmoothCore(float size, float baseSize, float max, float strength, float exponent)
        {
            if (strength <= 0f || max <= baseSize || size <= baseSize) return size;
            float t = Math.Min(1f, (size - baseSize) / (max - baseSize));
            return size + strength * (max - baseSize) * (float)Math.Pow(t, exponent) * (1f - t);
        }

        /// <summary>
        /// Linear scaling: multiplier ramps from 1× at <paramref name="min"/> to
        /// <paramref name="factor"/>× at <paramref name="max"/>. <paramref name="size"/> is assumed
        /// already within <c>[min, max]</c>.
        /// </summary>
        static float ScaleCore(float size, float min, float max, float factor)
        {
            if (factor <= 1f || max <= min) return size;
            float t = (size - min) / (max - min);
            return size * (1f + (factor - 1f) * t);
        }

        /// <summary>Native smoothing only (strength 0.93, exponent 1.2).</summary>
        static float SmoothNative(float size, float baseSize, float max)
            => SmoothCore(size, baseSize, max, 0.93f, 1.2f);

        /// <summary>
        /// Full Arise transform: native smooth → ×2 scale → smooth over the scaled range.
        /// Raises the effective cap to <c>2 × max</c>.
        /// </summary>
        static float AriseTransform(float size, float baseSize, float min, float max)
        {
            const float FACTOR = 2f;
            float s = Math.Min(size, max);                          // already clamped, just in case
            s = SmoothCore(s, baseSize, max, 0.93f, 1.2f);          // 1. native smooth
            s = ScaleCore(s, min, max, FACTOR);                     // 2. ×2 scale
            float scaledBase = ScaleCore(baseSize, min, max, FACTOR);
            float scaledMax  = max * FACTOR;
            return SmoothCore(s, scaledBase, scaledMax, 0.72f, 2f); // 3. scaled smooth
        }

        /// <summary>
        /// Applies the native smoothing buff ("Smooth Native Fish Size Distribution") to every slot,
        /// filling the sparse region just below MaxSize so the distribution ramps smoothly into the cap
        /// instead of the vanilla clamp spike. Does not change the max. Logs original vs new per fish.
        /// </summary>
        internal static void SmoothFishSizes(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * FishSlotOffsets.Stride;
                byte fishId = Memory.ReadByte(slotStart);
                float originalSize = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                if (originalSize <= 0f) continue;
                if (!Fish.TryGetValue(fishId, out FishData fishData) ||
                    !fishData.BaseSize.HasValue || !fishData.MinSize.HasValue) continue;

                float baseSize = fishData.BaseSize.Value;
                float maxSize  = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float newSize  = SmoothNative(originalSize, baseSize, maxSize);

                WriteScaledSize(slotStart, originalSize, newSize, fishId);
            }
        }

        /// <summary>
        /// Applies the full "Arise Mardan Scaling" effect to every slot: native smooth, then linear
        /// scale to 2× max, then a second smooth over the scaled range. Always includes the native
        /// smoothing internally (independent of <see cref="SmoothFishSizes"/>). Raises the cap to
        /// 2 × MaxSize. Logs original vs new per fish.
        /// </summary>
        internal static void AriseScaleFishSizes(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * FishSlotOffsets.Stride;
                byte fishId = Memory.ReadByte(slotStart);
                float originalSize = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                if (originalSize <= 0f) continue;
                if (!Fish.TryGetValue(fishId, out FishData fishData) ||
                    !fishData.BaseSize.HasValue || !fishData.MinSize.HasValue) continue;

                float baseSize = fishData.BaseSize.Value;
                float minSize  = fishData.MinSize.Value;
                float maxSize  = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float newSize  = AriseTransform(originalSize, baseSize, minSize, maxSize);

                WriteScaledSize(slotStart, originalSize, newSize, fishId);
            }
        }

        /// <summary>
        /// Shared writeback for the size effects: writes the new Size and multiplies both render-scale
        /// floats (ScaleModel, ScaleFixed) by <c>newSize / originalSize</c> so the model grows to match.
        /// Logs the original vs new size in both native units and display cm.
        /// </summary>
        static void WriteScaledSize(int slotStart, float originalSize, float newSize, byte fishId)
        {
            float scaleRatio = newSize / originalSize;
            Memory.WriteFloat(slotStart + FishSlotOffsets.Size, newSize);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleModel,
                Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleModel) * scaleRatio);
            Memory.WriteFloat(slotStart + FishSlotOffsets.ScaleFixed,
                Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleFixed) * scaleRatio);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SizeAdjust] {Fish.GetName(fishId)} orig={originalSize:F4} new={newSize:F4} " +
                $"({(int)(originalSize*10)}->{(int)(newSize*10)}cm)");
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
            bool hasPassiveSteering = areaData.Id == FishingAreas.MatatakiWaterfall.Id ||
                                      areaData.Id == FishingAreas.QueensHarbor.Id;
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

                    int slotAddr = areaData.SlotBase + slotIndex * FishSlotOffsets.Stride;
                    float fishX = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosX);
                    float fishY = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosY);
                    float deltaX = playerX - fishX;
                    float deltaY = playerY - fishY;
                    if (deltaX == 0f && deltaY == 0f) continue;

                    float angle = (float)Math.Atan2(deltaY, deltaX);
                    Memory.WriteFloat(slotAddr + FishSlotOffsets.Heading, angle);
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
                if (fishId != Fish.MardanGarayan.Id &&
                    fishId != Fish.BaronGarayan.Id  &&
                    fishId != Fish.Umadakara.Id) continue;

                int slotAddr = areaData.SlotBase + slotIndex * FishSlotOffsets.Stride;

                float affinity = baitOffset >= 0 ? Memory.ReadFloat(slotAddr + baitOffset) : 0f;
                if (affinity <= 0f) continue;

                double interval = baseSecs / affinity;
                if ((DateTime.UtcNow - _lastSteerTime[slotIndex]).TotalSeconds < interval) continue;
                _lastSteerTime[slotIndex] = DateTime.UtcNow;

                float fishX = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosX);
                float fishY = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosY);
                float deltaX = playerX - fishX;
                float deltaY = playerY - fishY;
                if (deltaX == 0f && deltaY == 0f) continue;

                float angle = (float)Math.Atan2(deltaY, deltaX);
                Memory.WriteFloat(slotAddr + FishSlotOffsets.Heading, angle);
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
                Memory.WriteInt(slotBase + slotIndex * FishSlotOffsets.Stride + FishSlotOffsets.AiState,
                    FishSlotState.AiState_Approaching);
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
            ushort baitSlotRaw = Memory.ReadUShort(FishingAddresses.OverworldState);
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
        /// Applies all Mardan sword session abilities in order: Twei slot reroll, FP boost, Arise size scaling.
        /// Must be called after <see cref="InitQuestState"/> so <see cref="fishArray"/> reflects any rerolled slots.
        /// No-ops when <see cref="hasMardanSword"/> is false.
        /// </summary>
        private static void ActivateMardanSwordAbilities(AreaFishData areaData)
        {
            if (!hasMardanSword) return;
            // Mardan Twei ability: reroll slots to spawn additional Mardan or Baron Garayan with the native chances.
            if (mardanSwordId == Items.mardantwei || mardanSwordId == Items.arisemardan)
                RollFishSlots(areaData.SlotBase, areaData.SlotCount, Fish.MardanGarayan.Id, 0.2f);
            // Apply the Mardan sword FP boost to all non-Garayan fish. Must be done after rolling so bonus applies to new spawns.
            ApplyMardanBonus(areaData.SlotBase, areaData.SlotCount);
            // Arise Mardan ability: smooth + scale fish sizes up. Must be done after rolling and bonuses so the boost applies to final sizes.
            // AriseScaleFishSizes already includes the native smoothing, so SmoothFishSizes is not also called here.
            if (mardanSwordId == Items.arisemardan)
                AriseScaleFishSizes(areaData.SlotBase, areaData.SlotCount);
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
            int fpAddr = slotBase + FishSlotOffsets.BaseFp;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (fishArray[slotIndex] == Fish.MardanGarayan.Id ||
                    fishArray[slotIndex] == Fish.BaronGarayan.Id)
                {
                    fpAddr += FishSlotOffsets.Stride;
                    continue;
                }
                Memory.WriteInt(fpAddr, (int)(Memory.ReadInt(fpAddr) * mardanMultiplier));
                fpAddr += 4;
                Memory.WriteInt(fpAddr, (int)(Memory.ReadInt(fpAddr) * mardanMultiplier));
                fpAddr += FishSlotOffsets.Stride - 4;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Mardan did its thing!");
            }
        }

        // ---- Utilities ----

        // Matataki Waterfall and Peanut Pond both have game area ID 1.
        // Resolved via player Y position: Waterfall ≈ Y=720, Peanut Pond ≈ Y=-1103. Split at Y=0.
        private static int ResolveFishingSpot(int areaId)
        {
            float playerX = Memory.ReadFloat(Addresses.positionX);
            float playerY = Memory.ReadFloat(Addresses.positionY);
            float playerZ = Memory.ReadFloat(Addresses.positionZ);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSession] playerPos=({playerX:F2},{playerY:F2},{playerZ:F2})");
            if (areaId != 1) return areaId;
            return playerY >= 0f
                ? FishingAreas.MatatakiWaterfall.Id
                : FishingAreas.PeanutPond.Id;
        }

        /// <summary>
        /// Maps the in-game time float to the corresponding <see cref="TimeOfDay"/> period.
        /// </summary>
        internal static TimeOfDay GetCurrentTimeOfDay(float timeValue)
        {
            if      (timeValue >= 2.5f && timeValue < 5.5f)  return TimeOfDay.Dusk;
            else if (timeValue >= 5.5f && timeValue < 8.5f)  return TimeOfDay.Night;
            else if (timeValue >= 8.5f && timeValue < 11.5f) return TimeOfDay.Morning;
            else                                              return TimeOfDay.Afternoon;
        }

        internal static TimeOfDay GetCurrentTimeOfDay() =>
            GetCurrentTimeOfDay(Memory.ReadFloat(Addresses.timeofDayWrite));

        // ---- Game formula simulations ----
        // Pure reimplementations of confirmed ELF logic for pre-write calculation and testing.
        // No memory I/O. See ELF VAs in each method summary for the authoritative source.
        //
        // Size RNG call chain (ELF load segment, base VA 0x00100000):
        //
        //   0x001046F8  lcgRand       — glibc-compatible LCG integer PRNG (standard rand() constants)
        //                               state  = (state × 1_103_515_245 + 12_345) & 0xFFFF_FFFF
        //                               return   state & 0x7FFF_FFFF   // 31-bit output, range [0, 2_147_483_647]
        //                               Seed stored at *(0x0024FDEC) + 88  (game-state struct, field offset 0x58).
        //                               Key instructions: MULT $v1,$v1,$a0 (R5900 writes lower 32b to rd);
        //                               ADDIU $v1,12345; AND $v0,$v1,0x7FFFFFFF; SW in delay slot saves full state.
        //
        //   0x00123CB0  baseRng       — converts one lcgRand result to float [0, 1)
        //                               r = lcgRand()                  // always non-negative (31-bit mask)
        //                               return CVT.S.W(r) / 2_147_483_648.0f
        //                               Division constant: LUI 0x4F00 → 0x4F000000 = 2^31 as float.
        //
        //   0x00123CF0  fishSizeRng   — Irwin-Hall(12) centred at zero; called once per slot init
        //                               float sum = 0;
        //                               for (int i = 0; i < 12; i++) sum += baseRng();  // 12 sequential JALs
        //                               return sum - 6.0f   // 0x40C00000 = 6.0f subtracted at end
        //                               Distribution: range (−6, 6), mean = 0, std ≈ 1, closely approximates N(0,1).
        //
        //   0x00240D60  slot init     — applies the rngRoll from fishSizeRng to produce the final size:
        //     0x00240D70 JAL 0x00123CF0        call fishSizeRng → $f0 = rngRoll
        //     0x00240D80 MTC1 $zero, $f1       $f1 = 0.0f
        //     0x00240D88 C.OLT.S $f0, $f1      condition = (rngRoll < 0.0f)
        //     0x00240D90 BC1T  0x00240DCC      if < 0 → divisor = 8.0f (0x41000000); else → divisor = 4.0f (0x40800000)
        //                               size = baseSize + rngRoll × (maxSize − baseSize) / divisor
        //     0x00240DFC/0x00240E0C C.OLT.S    clamp: floor = 0.5 × baseSize (0x3F000000), ceiling = maxSize
        //                               Asymmetric slope: positive rolls push size up at 2× the rate negative rolls push it down.

        /// <summary>
        /// Simulates the game's slot-init size roll (ELF 0x00240D60).
        /// Reproduces the full confirmed RNG chain: 12 × LCG-normalised uniform draws summed and
        /// centred (Irwin-Hall(12) − 6), then applied asymmetrically: positive rolls use divisor 4,
        /// negative rolls use divisor 8 (BC1T branch at 0x00240D90). Result clamped to
        /// [0.5×BaseSize, MaxSize], then both scale floats are derived.
        /// See the "Size RNG call chain" comment above for full ELF addresses and instruction traces.
        /// </summary>
        /// <param name="fish">Species data supplying BaseSize, ScaleDivisor, and MaxSize.</param>
        /// <param name="size">Resulting size (×10 = display cm).</param>
        /// <param name="scaleModel">slot+0x064: Size / ScaleDivisor.</param>
        /// <param name="scaleFixed">slot+0x068: Size / 25.0.</param>
        internal static void SimulateSlotInit(FishData fish, out float size, out float scaleModel, out float scaleFixed)
        {
            // Display cm = floor(size × 10), NOT rounded. The game's only float→int path is the
            // soft-cast helper at ELF 0x001110B0 (902 call sites): it right-shifts the mantissa by
            // (23 − exponent), discarding fractional bits — i.e. truncation toward zero (C-style (int)).
            // No ×10 site in the ELF adds 0.5 before casting, so there is no rounding. Since the clamp
            // below keeps size ≥ 0.5×BaseSize > 0, floor == truncation here. e.g. 8.7653 → 87cm, not 88.
            float baseSize     = fish.BaseSize      ?? 0f;
            float maxSize      = fish.MaxSize       ?? 0f;
            float scaleDivisor = fish.ScaleDivisor  ?? 0f;

            // fishSizeRng (0x00123CF0): 12 draws from baseRng (0x00123CB0), each Uniform[0,1),
            // summed and shifted → Irwin-Hall(12)−6, range (−6, 6), mean=0, std≈1.
            float rngRoll = 0f;
            for (int i = 0; i < 12; i++) rngRoll += (float)Rng.NextDouble();
            rngRoll -= 6f;

            // Asymmetric application (0x00240D88 C.OLT.S / 0x00240D90 BC1T):
            // positive roll → ÷4 (steeper slope toward maxSize)
            // negative roll → ÷8 (shallower slope toward floor)
            size = baseSize;
            if (rngRoll >= 0f)
                size += rngRoll * (maxSize - baseSize) / 4f;
            else
                size += rngRoll * (maxSize - baseSize) / 8f;

            // Clamp: floor = 0.5×baseSize (0x3F000000), ceiling = maxSize
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
