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
    /// Known dynamic field map (from the retired PollSlotDynamics scans + FishSlotOffsets):
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
        internal static bool Enabled = false;   // dev diagnostic — spawns a polling thread per fishing session when on

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
                                            $"[HookTriang] s={i} {Fish.GetName(fishId)} heading stable: live=({livePosX:F2},{livePosY:F2},{livePosZ:F2}) hookZ={aiTargetZ:F2} hdg={heading:F4}");
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
                                $"[FishPhase] f={frame:D5} s={i} {Fish.GetName(fishId)} aiState {lastAiState[i]:X8} -> {aiState:X8}");

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
                                        $"[NoticeRange] s={i} {Fish.GetName(fishId)} " +
                                        $"dist3d={dist3d:F2} dist2d={dist2d:F2} " +
                                        $"fish=({livePosX:F2},{livePosY:F2},{livePosZ:F2}) hook=({hookX:F2},{hookY:F2},{hookZ:F2}) [ref=s{refSlot}]");
                                }
                            }

                            lastAiState[i] = aiState;
                        }

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishPhase] f={frame:D5} ph={phase:X2} s={i} {Fish.GetName(fishId)} " +
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
                    $"[FishInfo] area={areaId} slot={slotIndex} {Fish.GetName(fishId)} (id={fishId}) " +
                    $"scaleDivisor={scaleDivisor:F1} baseSize={baseSize:F1} max={maxSize:F1}({(int)(maxSize*10)}cm) " +
                    $"size={size:F4} ({(int)(size*10)}cm) fp={fpMin}-{fpMax}");
            }
        }

        // ── Delta-scan methodology (the ScanForFishTable implementation was removed 2026-09; git) ──
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
