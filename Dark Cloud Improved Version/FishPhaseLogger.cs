using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Datamining and diagnostic tools for fishing: session logging, slot polling, and memory scanning.
    /// Also drives the per-frame phase/slot monitor when <see cref="Enabled"/> is true.
    ///
    /// Enable by setting <see cref="Enabled"/> = true before a session begins.
    /// To disable without removing the call, set Enabled = false or comment the
    /// FishPhaseLogger.OnSessionStart call in Fishing.cs.
    ///
    /// Known dynamic field map (from PollSlotDynamics + FishSlotOffsets):
    ///   +0x054  Unk054          (int; waypoint refresh timer)
    ///   +0x058  AiStateTimer    (int; game-controlled live state; read-only)
    ///   +0x060  Size            (float)
    ///   +0x064  ScaleX          (float)
    ///   +0x068  ScaleY          (float)
    ///   +0x074  Heading         (float, radians)
    ///   +0x080  Speed           (float)
    ///   +0x084  Velocity        (float)
    ///   +0x090  AiTargetY       (float, AI destination / hook pos)
    ///   +0x094  AiTargetZ       (float)
    ///   +0x098  AiTargetX       (float)
    ///   +0x0B0  LivePosY        (float, per-fish rendered position)
    ///   +0x0B4  LivePosZ        (float)
    ///   +0x0B8  LivePosX        (float)
    ///   +0x130  Unk130          (float; mirrors ScaleY — likely shadow/collision scale)
    ///   +0x134  Unk134          (float; mirrors ScaleY)
    ///   +0x138  Unk138          (float; mirrors ScaleY)
    ///   +0x150  Unk150          (float; default 7.0; purpose unknown)
    ///   +0x154  Unk154          (float; default 18.0; untested)
    ///   +0x158  Unk158          (float; default 60.0; purpose unknown)
    /// Unexplored gaps logged as hex — likely candidates for AI state / bait timer:
    ///   g04C  +0x04C–+0x053  (2 ints, between BaitAffPetitefish and Unk054)
    ///   g05C  +0x05C         (1 int,  between AiStateTimer and Size)
    ///   g06C  +0x06C–+0x073 (2 ints, between ScaleY and Heading)
    ///   g078  +0x078–+0x07F (2 ints, between Heading and Speed)
    ///   g088  +0x088–+0x08F (2 ints, between Velocity and AiTarget)
    ///   g09C  +0x09C–+0x0AF (5 ints, between AiTarget and LivePos)
    ///   g0BC  +0x0BC–+0x0DB (8 ints, after LivePos)
    /// </summary>
    internal static class FishPhaseLogger
    {
        internal static bool Enabled = true;

        private static Thread _thread;
        private static volatile bool _running;
        private static int _slotBase;
        private static int _slotCount;
        private static DateTime _lastSlotPollTime = DateTime.MinValue;

        /// <summary>Called from Fishing.InitFishingSession on the first tick of a session.</summary>
        internal static void OnSessionStart(int slotBase, int slotCount)
        {
            if (!Enabled) return;
            Stop();
            _slotBase  = slotBase;
            _slotCount = slotCount;

            // To run datamining scans (run once — comment back in before a session to trigger):
            // new Thread(ScanForFishTable) { IsBackground = true, Name = "FishTableScan" }.Start();

            _running = true;
            _thread  = new Thread(Run) { IsBackground = true, Name = "FishPhaseLogger" };
            _thread.Start();
        }

        /// <summary>Called from TownCharacter when FishingState drops to 0.</summary>
        internal static void OnSessionEnd()
        {
            _running = false;
        }

        private static void Stop()
        {
            _running = false;
            _thread?.Join(200);
        }

        private static void Run()
        {
            int frame       = 0;
            int lastPhase   = -1;
            var lastAiState = new int[_slotCount];
            for (int i = 0; i < _slotCount; i++) lastAiState[i] = -2;

            // Hook triangulation: track heading stability per slot.
            // Once a slot's heading changes by < 0.02 rad for 10 consecutive frames
            // while Approaching, its vector is considered stable for intersection math.
            var stableFrames = new int[_slotCount];
            var prevHdg      = new float[_slotCount];
            var stableHdg    = new float[_slotCount];
            var stableLpX    = new float[_slotCount];
            var stableLpY    = new float[_slotCount];
            var stableLpZ    = new float[_slotCount];
            var stablePosZ   = new float[_slotCount]; // hook Z depth (from AI destination, not fish live pos)
            int lastStableCount = 0;
            for (int i = 0; i < _slotCount; i++) prevHdg[i] = float.NaN;

            // Per-slot AI target pos; used by NoticeRange to reference established hook position
            var slotAimX = new float[_slotCount];
            var slotAimY = new float[_slotCount];
            var slotAimZ = new float[_slotCount];

            while (_running)
            {
                int phase = Memory.ReadInt(FishingAddresses.Phase);

                if (phase != lastPhase)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishPhase] phase={phase:X2} ({PhaseLabel(phase)})");

                    if (phase == FishingState.Phase_HookInWater)
                    {
                        for (int i = 0; i < _slotCount; i++) { stableFrames[i] = 0; prevHdg[i] = float.NaN; lastAiState[i] = -2; }
                        lastStableCount = 0;
                    }

                    lastPhase = phase;
                }

                if (phase == FishingState.Phase_HookInWater
                 || phase == FishingState.Phase_NibblePull
                 || phase == FishingState.Phase_ReelingIn)
                {
                    for (int i = 0; i < _slotCount; i++)
                    {
                        int slotAddr = _slotBase + i * FishSlotOffsets.Stride;

                        byte  fishId       = Memory.ReadByte(slotAddr);
                        int   aiState      = Memory.ReadInt(slotAddr   + FishSlotOffsets.AiState);
                        float heading      = Memory.ReadFloat(slotAddr + FishSlotOffsets.Heading);
                        float speed        = Memory.ReadFloat(slotAddr + FishSlotOffsets.Speed);
                        float velocity     = Memory.ReadFloat(slotAddr + FishSlotOffsets.Velocity);
                        float aiTargetX    = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetX);
                        float aiTargetY    = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetY);
                        float aiTargetZ    = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetZ);
                        slotAimX[i] = aiTargetX;
                        slotAimY[i] = aiTargetY;
                        slotAimZ[i] = aiTargetZ;
                        float livePosX     = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosX);
                        float livePosY     = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosY);
                        float livePosZ     = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosZ);
                        int   unk054       = Memory.ReadInt(slotAddr   + FishSlotOffsets.Unk054);
                        int   aiStateTimer = Memory.ReadInt(slotAddr   + FishSlotOffsets.AiStateTimer);
                        float unk150       = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk150);
                        float unk154       = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk154);
                        float unk158       = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk158);

                        string g04C  = ReadHex(slotAddr, 0x04C, 1);
                        string g05C  = ReadHex(slotAddr, 0x05C, 1);
                        string g06C  = ReadHex(slotAddr, 0x06C, 2);
                        string g078  = ReadHex(slotAddr, 0x078, 2);
                        int   unk088 = Memory.ReadInt(slotAddr   + FishSlotOffsets.Unk088);
                        float detectionRadius = Memory.ReadFloat(slotAddr + FishSlotOffsets.NoticeRadius);
                        string g09C  = ReadHex(slotAddr, 0x09C, 5);
                        string g0BC  = ReadHex(slotAddr, 0x0BC, 8);

                        // Heading stability tracking
                        if (aiState == FishSlotState.AiState_Approaching && !float.IsNaN(prevHdg[i]))
                        {
                            if (Math.Abs(heading - prevHdg[i]) < 0.02f)
                            {
                                stableFrames[i]++;
                                if (stableFrames[i] >= 10)
                                {
                                    stableHdg[i]  = heading;
                                    stableLpX[i]  = livePosX;
                                    stableLpY[i]  = livePosY;
                                    stableLpZ[i]  = livePosZ;
                                    stablePosZ[i] = aiTargetZ; // hook depth
                                    if (stableFrames[i] == 10)
                                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                            $"[HookTriang] s={i} {FishDatabase.GetName(fishId)} heading stable: live=({livePosX:F2},{livePosY:F2},{livePosZ:F2}) hookZ={aiTargetZ:F2} hdg={heading:F4}");
                                }
                            }
                            else
                            {
                                stableFrames[i] = 0;
                            }
                        }
                        else if (aiState != FishSlotState.AiState_Approaching)
                        {
                            stableFrames[i] = 0;
                        }
                        prevHdg[i] = aiState == FishSlotState.AiState_Approaching ? heading : float.NaN;

                        if (aiState != lastAiState[i])
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[FishPhase] f={frame:D5} s={i} {FishDatabase.GetName(fishId)} aiState {lastAiState[i]:X8} -> {aiState:X8}");

                            // Log notice-range distance when a fish naturally transitions to Approaching.
                            // Requires another slot to already be approaching/nibbling so the hook
                            // position is established — avoids reading AiTarget at the same frame
                            // the game writes it (which may not yet be stable).
                            if (aiState == FishSlotState.AiState_Approaching
                             && lastAiState[i] != FishSlotState.AiState_Approaching
                             && lastAiState[i] != -2)
                            {
                                int refSlot = -1;
                                for (int j = 0; j < _slotCount; j++)
                                {
                                    if (j != i && (lastAiState[j] == FishSlotState.AiState_Approaching
                                                || lastAiState[j] == FishSlotState.AiState_Nibbling))
                                    { refSlot = j; break; }
                                }
                                if (refSlot >= 0)
                                {
                                    float hookX = slotAimX[refSlot], hookY = slotAimY[refSlot], hookZ = slotAimZ[refSlot];
                                    float deltaX = livePosX - hookX, deltaY = livePosY - hookY, deltaZ = livePosZ - hookZ;
                                    float dist3d = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
                                    float dist2d = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                        $"[NoticeRange] s={i} {FishDatabase.GetName(fishId)} " +
                                        $"dist3d={dist3d:F2} dist2d={dist2d:F2} " +
                                        $"fish=({livePosX:F2},{livePosY:F2},{livePosZ:F2}) hook=({hookX:F2},{hookY:F2},{hookZ:F2}) [ref=s{refSlot}]");
                                }
                            }

                            lastAiState[i] = aiState;
                        }

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishPhase] f={frame:D5} ph={phase:X2} s={i} {FishDatabase.GetName(fishId)} " +
                            $"ai={aiState:X8} hdg={heading:F3} spd={speed:F3} vel={velocity:F3} " +
                            $"aim=({aiTargetX:F1},{aiTargetY:F1},{aiTargetZ:F1}) live=({livePosX:F1},{livePosY:F1},{livePosZ:F1}) " +
                            $"u054={unk054:X8} u058={aiStateTimer:X8} " +
                            $"b150={unk150:F3} b154={unk154:F3} b158={unk158:F3} " +
                            $"g04C=[{g04C}] g05C=[{g05C}] g06C=[{g06C}] " +
                            $"g078=[{g078}] u088={unk088:X8} dr={detectionRadius:F1} g09C=[{g09C}] g0BC=[{g0BC}]");
                    }

                    // Triangulate hook position when 2+ slots have stable headings
                    int stableCount = 0;
                    for (int i = 0; i < _slotCount; i++)
                        if (stableFrames[i] >= 10) stableCount++;

                    if (stableCount >= 2 && stableCount > lastStableCount)
                    {
                        float sumX = 0, sumY = 0;
                        int pairs = 0;
                        for (int a = 0; a < _slotCount - 1; a++)
                        {
                            if (stableFrames[a] < 10) continue;
                            for (int b = a + 1; b < _slotCount; b++)
                            {
                                if (stableFrames[b] < 10) continue;
                                float cosA = (float)Math.Cos(stableHdg[a]), sinA = (float)Math.Sin(stableHdg[a]);
                                float cosB = (float)Math.Cos(stableHdg[b]), sinB = (float)Math.Sin(stableHdg[b]);
                                float denom = cosA * sinB - sinA * cosB;
                                if (Math.Abs(denom) < 0.01f) continue; // parallel vectors
                                float intersectionParam = ((stableLpX[b] - stableLpX[a]) * sinB - (stableLpY[b] - stableLpY[a]) * cosB) / denom;
                                if (intersectionParam < 0) continue; // intersection behind fish
                                sumX += stableLpX[a] + intersectionParam * cosA;
                                sumY += stableLpY[a] + intersectionParam * sinA;
                                pairs++;
                            }
                        }
                        if (pairs > 0)
                        {
                            float estimatedX = sumX / pairs, estimatedY = sumY / pairs;
                            float estimatedZ = 0; int zCount = 0;
                            for (int i = 0; i < _slotCount; i++)
                                if (stableFrames[i] >= 10) { estimatedZ += stablePosZ[i]; zCount++; }
                            estimatedZ /= zCount;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[HookTriang] estimated hook=({estimatedX:F2},{estimatedY:F2},{estimatedZ:F2}) from {pairs} vector pair(s) ({stableCount} stable slots)");
                        }
                        lastStableCount = stableCount;
                    }

                    frame++;
                }

                Thread.Sleep(16);
            }
        }

        // ── Diagnostic functions ────────────────────────────────────────────────────

        /// <summary>
        /// Adds one of every bait type to the bag so all 13 baits are available for testing.
        /// Bait list (item ID — best fish):
        ///   throbbingcherry(166)  Heela, Niler, Den
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
            float todFloat = Memory.ReadFloat(Addresses.timeofDayWrite);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSession] area={areaId} slots={slotCount} tod={todFloat:F2}");
            FishDataFarmer.RecordSession(todFloat);
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart  = slotBase + slotIndex * FishSlotOffsets.Stride;
                byte fishId    = Memory.ReadByte(slotStart);
                float scaleDivisor = Memory.ReadFloat(slotStart + FishSlotOffsets.ScaleDivisor);
                float baseSize     = Memory.ReadFloat(slotStart + FishSlotOffsets.BaseSize);
                float maxSize      = Memory.ReadFloat(slotStart + FishSlotOffsets.MaxSize);
                float size         = Memory.ReadFloat(slotStart + FishSlotOffsets.Size);
                int   fpMin        = Memory.ReadInt(slotStart   + FishSlotOffsets.BaseFp);
                int   fpMax        = Memory.ReadInt(slotStart   + FishSlotOffsets.MaxFp);
                FishDataFarmer.RecordSlot(fishId, todFloat);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishInfo] area={areaId} slot={slotIndex} {FishDatabase.GetName(fishId)} (id={fishId}) " +
                    $"scaleDivisor={scaleDivisor:F1} baseSize={baseSize:F1} max={maxSize:F1}({(int)(maxSize*10)}cm) " +
                    $"size={size:F4} ({(int)(size*10)}cm) fp={fpMin}-{fpMax}");
                // bait affinity table — values are bite-likelihood weights (0.0=never, 1.0=normal)
                float affEvy      = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffEvy);
                float affMimi     = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffMimi);
                float affPrickly  = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffPrickly);
                float affCherry   = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffThrobbingCherry);
                float affPeach    = Memory.ReadFloat(slotStart + FishSlotOffsets.BaitAffGooeyPeach);
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
        /// Writes a test integer value to the given slot field offset across all slots in the area.
        /// </summary>
        internal static void TestSlotField(int slotBase, int slotCount, int offset, int value)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                Memory.WriteInt(slotBase + slotIndex * FishSlotOffsets.Stride + offset, value);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FieldTest] offset=0x{offset:X3} int={value} written to {slotCount} slots");
        }

        /// <summary>
        /// Writes a test float value to the given slot field offset across all slots in the area.
        /// </summary>
        internal static void TestSlotField(int slotBase, int slotCount, int offset, float value)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                Memory.WriteFloat(slotBase + slotIndex * FishSlotOffsets.Stride + offset, value);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FieldTest] offset=0x{offset:X3} float={value} written to {slotCount} slots");
        }

        /// <summary>
        /// Polls and logs live movement/behavior fields for all slots once per second.
        /// Reads heading, speed, velocity, world position, waypoint, behavior floats, and scale mirrors.
        /// </summary>
        internal static void PollSlotDynamics(AreaFishData areaData)
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
                int slotAddr     = areaData.SlotBase + slotIndex * FishSlotOffsets.Stride;
                byte fishId      = Memory.ReadByte(slotAddr);
                float heading    = Memory.ReadFloat(slotAddr + FishSlotOffsets.Heading);
                float speed      = Memory.ReadFloat(slotAddr + FishSlotOffsets.Speed);
                float velocity   = Memory.ReadFloat(slotAddr + FishSlotOffsets.Velocity);
                float aiTargetX  = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetX);
                float aiTargetY  = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetY);
                float aiTargetZ  = Memory.ReadFloat(slotAddr + FishSlotOffsets.AiTargetZ);
                float fishPosX   = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosX);
                float fishPosY   = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosY);
                float fishPosZ   = Memory.ReadFloat(slotAddr + FishSlotOffsets.LivePosZ);
                float unk150     = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk150);
                float unk154     = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk154);
                float unk158     = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk158);
                float scale130   = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk130);
                float scale134   = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk134);
                float scale138   = Memory.ReadFloat(slotAddr + FishSlotOffsets.Unk138);
                int unk054       = Memory.ReadInt(slotAddr  + FishSlotOffsets.Unk054);
                int unk058       = Memory.ReadInt(slotAddr  + FishSlotOffsets.AiStateTimer);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SlotWatch] slot={slotIndex} {FishDatabase.GetName(fishId)} " +
                    $"hdg={heading:F3} spd={speed:F3} curVel={velocity:F3} " +
                    $"pos=({aiTargetX:F1},{aiTargetY:F2},{aiTargetZ:F1}) fishPos=({fishPosX:F1},{fishPosY:F1},{fishPosZ:F1}) " +
                    $"b150={unk150:F2} b154={unk154:F2} b158={unk158:F2} " +
                    $"unk054={unk054} unk058={unk058} scl=({scale130:F4},{scale134:F4},{scale138:F4})");
            }
        }

        /// <summary>
        /// Dumps every 4-byte word in the first 0x200 bytes of each slot as both raw hex and
        /// interpreted float. Used to discover new slot field mappings.
        /// </summary>
        internal static void DumpFishSlot(int slotBase, int slotCount)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int slotStart = slotBase + slotIndex * FishSlotOffsets.Stride;
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

        // ── Delta-scan methodology ────────────────────────────────────────────────
        // Goal: locate an unknown PS2 EE RAM address that stores a known float triplet
        //       (e.g. hook landing position) without any prior knowledge of the address.
        //
        // How it works (two-cast approach):
        //   Cast 1 — hook lands at position P1. Scan all 32 MB of EE RAM
        //            (0x20000000–0x21FFFFFF via PINE). Record every address where
        //            3 consecutive floats match P1 in any of the 6 XYZ orderings
        //            within ±tol. These are "candidates."
        //
        //   Cast 2 — hook lands at a DIFFERENT position P2 (move ≥3 units away so
        //            values that changed are meaningful). Re-read only the candidate
        //            addresses. Any address whose 3 floats now match P2 is CONFIRMED.
        //            Addresses that held P1 by coincidence won't also hold P2.
        //
        // Efficiency: ReadFloatBatch packs 1024 Read32 PINE commands into one socket
        //   round-trip. PCSX2 v2.7.x responds with [1 status byte][N × 4 data bytes]
        //   (fmtB). At ~0.12 ms per round-trip, 8192 batches scan all 32 MB in ~1 s.
        //   ReadByteArray (1 byte per round-trip) would take ~74 hours for the same range.
        //
        // Findings for Dark Cloud fishing (Muska Lacka, 2026-03-06):
        //   The hook landing position is stored ONLY in each active fish slot's AiTarget
        //   field (YZX ordering, stride 0x2410 apart). There is no separate hook entity
        //   position anywhere in EE RAM. AiTarget is the authoritative source.
        // ─────────────────────────────────────────────────────────────────────────────
        private static string ReadHex(int slotStart, int offset, int count)
        {
            var builder = new System.Text.StringBuilder(count * 9);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) builder.Append(' ');
                builder.Append(Memory.ReadInt(slotStart + offset + i * 4).ToString("X8"));
            }
            return builder.ToString();
        }

        private static void ScanForFishTable()
        {
            // Baron Garayan has the only MaxSize=30.0f among all 18 species.
            // Validate each hit using adjacent known values from FishDatabase:
            //   ScaleDivisor=21.0f at MaxSize-8, BaseSize=10.0f at MaxSize-4
            //   BaseFp=600 at MaxSize+4,    MaxFp=1000 at MaxSize+8
            // (offsets mirror the fish slot layout: ScaleDivisor@0x004, BaseSize@0x008,
            //  MaxSize@0x00C, BaseFp@0x010, MaxFp@0x014)
            const long  scanStart  = 0x20000000L;
            const long  scanEnd    = 0x22000000L;
            const int   batchSize  = 1024;
            const float target     = 30.0f;
            const float tol        = 0.01f;
            const float unk004Val  = 21.0f;
            const float unk008Val  = 10.0f;
            const uint  fpMinVal   = 600;
            const uint  fpMaxVal   = 1000;

            long totalBatches = ((scanEnd - scanStart) / 4 + batchSize - 1) / batchSize;
            var hits = new System.Collections.Generic.List<int>();

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "[FishTableScan] scanning EE RAM for Baron Garayan MaxSize=30.0f...");

            for (long b = 0; b < totalBatches && _running; b++)
            {
                long  addr  = scanStart + b * batchSize * 4;
                int   count = (int)Math.Min(batchSize, (scanEnd - addr) / 4);
                float[] floatBatch = Memory.ReadFloatBatch(addr, count);
                for (int i = 0; i < count; i++)
                    if (Math.Abs(floatBatch[i] - target) < tol)
                        hits.Add((int)(addr + i * 4));
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishTableScan] {hits.Count} hit(s) — validating against Baron Garayan signature...");

            foreach (int hit in hits)
            {
                const int halfWindowSize = 8;
                float[] windowWords = Memory.ReadFloatBatch(hit - halfWindowSize * 4, halfWindowSize * 2);

                bool hasScaleDivisor = Math.Abs(windowWords[halfWindowSize - 2] - unk004Val) < tol;
                bool hasBaseSize = Math.Abs(windowWords[halfWindowSize - 1] - unk008Val) < tol;
                uint rawBaseFp  = BitConverter.ToUInt32(BitConverter.GetBytes(windowWords[halfWindowSize + 1]), 0);
                uint rawMaxFp  = BitConverter.ToUInt32(BitConverter.GetBytes(windowWords[halfWindowSize + 2]), 0);
                bool hasBaseFp  = rawBaseFp == fpMinVal;
                bool hasMaxFp  = rawMaxFp == fpMaxVal;
                int  score     = (hasScaleDivisor ? 1 : 0) + (hasBaseSize ? 1 : 0) + (hasBaseFp ? 1 : 0) + (hasMaxFp ? 1 : 0);

                if (score < 2) continue;

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishTableScan] candidate 0x{(uint)hit:X8} score={score}/4 " +
                    $"(unk004={hasScaleDivisor} unk008={hasBaseSize} fpMin={hasBaseFp} fpMax={hasMaxFp})");

                // Dump 16 words before through 32 words after the MaxSize hit,
                // 8 words per line, hit marked with [].
                const int pre  = 16;
                const int post = 32;
                float[] dumpWords = Memory.ReadFloatBatch(hit - pre * 4, pre + post);
                for (int row = 0; row < (pre + post); row += 8)
                {
                    var rowBuilder = new System.Text.StringBuilder();
                    rowBuilder.Append($"[FishTableScan]   0x{(uint)(hit - pre * 4 + row * 4):X8}:");
                    for (int k = row; k < row + 8 && k < dumpWords.Length; k++)
                    {
                        uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(dumpWords[k]), 0);
                        rowBuilder.Append(k == pre ? $" [{raw:X8}]" : $"  {raw:X8}");
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + rowBuilder);
                }

                // Search ±512 bytes around MaxSize for Baron Garayan's known MinSize=5.0f (observed in slot).
                // Two plausible encodings: raw float (0x40A00000) or integer 5 (0x00000005).
                const int searchWords = 128; // 512 bytes each direction
                float[] region = Memory.ReadFloatBatch(hit - searchWords * 4, searchWords * 2);
                bool foundMinSize = false;
                for (int k = 0; k < region.Length; k++)
                {
                    uint   raw        = BitConverter.ToUInt32(BitConverter.GetBytes(region[k]), 0);
                    int    byteOffset = (k - searchWords) * 4;
                    string which      = null;
                    if      (Math.Abs(region[k] - 5.0f)  < tol) which = "5.0f";
                    else if (Math.Abs(region[k] - 50.0f) < tol) which = "50.0f";
                    else if (raw == 5u)                           which = "int5";
                    else if (raw == 50u)                          which = "int50";
                    if (which != null)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishTableScan] {which} at MaxSize{(byteOffset >= 0 ? "+" : "")}{byteOffset} " +
                            $"(0x{(uint)(hit + byteOffset):X8})");
                        foundMinSize = true;
                    }
                }
                if (!foundMinSize)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        "[FishTableScan] MinSize not found within ±512 bytes (checked 5.0f/50.0f/int5/int50)");

                // Follow the pointer array that sits immediately after the species table.
                // Table base = Baron Garayan entry base − 17×stride; entry base = MaxSize − 8.
                // Pointer array = table_end + 0x10 bytes padding = hit + 0x50.
                int ptrBase = hit + 0x50;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishTableScan] following pointer array at 0x{(uint)ptrBase:X8}...");
                float[] ptrWords = Memory.ReadFloatBatch(ptrBase, 32);
                for (int p = 0; p < 32; p++)
                {
                    uint ptr = BitConverter.ToUInt32(BitConverter.GetBytes(ptrWords[p]), 0);
                    if (ptr == 0) continue;
                    long targetAddr = 0x20000000L + ptr;
                    float[] targetWords = Memory.ReadFloatBatch(targetAddr, 8);
                    var rowBuilder = new System.Text.StringBuilder();
                    for (int t = 0; t < 8; t++)
                    {
                        uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(targetWords[t]), 0);
                        rowBuilder.Append($"  {raw:X8}");
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishTableScan]   ptr[{p}] →0x{ptr:X8}:{rowBuilder}");
                }

                // Scan 1024 bytes immediately before the species table base (entry 0 start).
                // The ±512-byte MinSize search above is centered on Baron Garayan's entry (ID 17),
                // so it reaches back only to ~entry 9 — the pre-table region is uncharted.
                int tableBase   = hit - 0x008 - 17 * 0x48; // = species table entry 0 start
                const int adjWords = 256;                   // 256 words = 1024 bytes per region
                int preStart    = tableBase - adjWords * 4;
                int ptrArrayEnd = ptrBase   + FishModelTable.Count * FishModelTable.Stride;

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishTableScan] pre-table  0x{(uint)preStart:X8}–0x{(uint)tableBase:X8}:");
                float[] preReg = Memory.ReadFloatBatch(preStart, adjWords);
                for (int row = 0; row < adjWords; row += 8)
                {
                    var rowBuilder = new System.Text.StringBuilder();
                    rowBuilder.Append($"[FishTableScan]   0x{(uint)(preStart + row * 4):X8}:");
                    for (int k = row; k < row + 8 && k < preReg.Length; k++)
                    {
                        uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(preReg[k]), 0);
                        rowBuilder.Append($"  {raw:X8}");
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + rowBuilder);
                }

                // Print the 192-byte packed-byte section (tableBase-0x160) as individual bytes,
                // 12 per row → 16 rows. Hypothesis: spawn weights per fish per area×time.
                // (16 fish × 12 bytes = 192 if IDs 8 and 17 are excluded)
                const int byteSecLen = 192;
                int byteSecBase = tableBase - 0x160;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishTableScan] byte-section 0x{(uint)byteSecBase:X8} ({byteSecLen}b, 12/row):");
                float[] bsWords = Memory.ReadFloatBatch(byteSecBase, byteSecLen / 4);
                byte[] bsBytes = new byte[byteSecLen];
                for (int i = 0; i < byteSecLen / 4; i++)
                {
                    byte[] wb = BitConverter.GetBytes(bsWords[i]);
                    bsBytes[i*4+0] = wb[0]; bsBytes[i*4+1] = wb[1];
                    bsBytes[i*4+2] = wb[2]; bsBytes[i*4+3] = wb[3];
                }
                for (int row = 0; row < byteSecLen; row += 12)
                {
                    var rowBuilder = new System.Text.StringBuilder();
                    rowBuilder.Append($"[FishTableScan]   row{row/12:D2}:");
                    for (int k = row; k < row + 12 && k < bsBytes.Length; k++)
                        rowBuilder.Append($" {bsBytes[k]:X2}");
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + rowBuilder);
                }

                // Follow the pointer stored at tableBase - 0x90 (observed: 0x0029F6E0).
                int ptrFieldAddr = tableBase - 0x90;
                float[] ptrFieldWords = Memory.ReadFloatBatch(ptrFieldAddr, 1);
                uint rawFollowPtr = BitConverter.ToUInt32(BitConverter.GetBytes(ptrFieldWords[0]), 0);
                if (rawFollowPtr != 0 && rawFollowPtr < 0x02200000u)
                {
                    long followTarget = 0x20000000L + rawFollowPtr;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishTableScan] ptr@0x{(uint)ptrFieldAddr:X8}=0x{rawFollowPtr:X8} → 0x{(uint)followTarget:X8}:");
                    float[] followData = Memory.ReadFloatBatch(followTarget, 64);
                    for (int row = 0; row < 64; row += 8)
                    {
                        var rowBuilder = new System.Text.StringBuilder();
                        rowBuilder.Append($"[FishTableScan]   0x{(uint)(followTarget + row * 4):X8}:");
                        for (int k = row; k < row + 8 && k < followData.Length; k++)
                        {
                            uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(followData[k]), 0);
                            rowBuilder.Append($"  {raw:X8}");
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + rowBuilder);
                    }
                }

                // Scan 512 bytes immediately after the pointer array (first uncharted region post-table).
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishTableScan] post-ptr   0x{(uint)ptrArrayEnd:X8}–0x{(uint)(ptrArrayEnd + adjWords * 4):X8}:");
                float[] postReg = Memory.ReadFloatBatch(ptrArrayEnd, adjWords);
                for (int row = 0; row < adjWords; row += 8)
                {
                    var rowBuilder = new System.Text.StringBuilder();
                    rowBuilder.Append($"[FishTableScan]   0x{(uint)(ptrArrayEnd + row * 4):X8}:");
                    for (int k = row; k < row + 8 && k < postReg.Length; k++)
                    {
                        uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(postReg[k]), 0);
                        rowBuilder.Append($"  {raw:X8}");
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + rowBuilder);
                }

                // Search both adjacent regions for Baron Garayan's known MinSize.
                bool foundAdj = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    float[] adjacentRegion = pass == 0 ? preReg : postReg;
                    int     regBase        = pass == 0 ? preStart : ptrArrayEnd;
                    string  label          = pass == 0 ? "pre-table" : "post-ptr";
                    for (int k = 0; k < adjacentRegion.Length; k++)
                    {
                        uint   raw      = BitConverter.ToUInt32(BitConverter.GetBytes(adjacentRegion[k]), 0);
                        int    wordAddr = regBase + k * 4;
                        string which = null;
                        if      (Math.Abs(adjacentRegion[k] - 5.0f)  < tol) which = "5.0f";
                        else if (Math.Abs(adjacentRegion[k] - 50.0f) < tol) which = "50.0f";
                        else if (raw == 5u)                                   which = "int5";
                        else if (raw == 50u)                                  which = "int50";
                        if (which != null)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[FishTableScan] MinSize candidate {which} at 0x{(uint)wordAddr:X8} ({label})");
                            foundAdj = true;
                        }
                    }
                }
                if (!foundAdj)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        "[FishTableScan] MinSize not found in adjacent regions");
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishTableScan] done");
        }

        private static string PhaseLabel(int phase) => phase switch
        {
            FishingState.Phase_Idle          => "Idle",
            FishingState.Phase_Walking       => "Walking",
            FishingState.Phase_Casting       => "Casting",
            FishingState.Phase_HookInWater   => "HookInWater",
            FishingState.Phase_NibblePull    => "NibblePull",
            FishingState.Phase_Uncasting     => "Uncasting",
            FishingState.Phase_HoldingFish   => "HoldingFish",
            FishingState.Phase_ThrowingBack  => "ThrowingBack",
            FishingState.Phase_PullingOut    => "PullingOut",
            FishingState.Phase_ReelingIn     => "ReelingIn",
            FishingState.Phase_DraggingHook  => "DraggingHook",
            FishingState.Phase_IdleTapping   => "IdleTapping",
            FishingState.Phase_IdleCrouching => "IdleCrouching",
            _                                => $"Unknown({phase:X2})",
        };
    }
}
