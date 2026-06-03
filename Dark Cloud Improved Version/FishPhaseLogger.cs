using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Datamining tool: samples fish slot data at ~60 fps while the hook is in the water
    /// or the line is being reeled in. Phase transitions are always logged regardless of enable state.
    /// Goal: identify offsets for fish-interest, nibble, bite, and bait-exhausted states.
    ///
    /// Enable by setting <see cref="Enabled"/> = true before a session begins.
    /// Known dynamic field map (from PollSlotDynamics + FishSlotOffsets):
    ///   +0x054  unk054          (int)
    ///   +0x058  unk058          (int)
    ///   +0x060  Size            (float)
    ///   +0x064  ScaleX          (float)
    ///   +0x068  ScaleY          (float)
    ///   +0x074  Heading         (float, radians)
    ///   +0x080  speed           (float)
    ///   +0x084  curVel          (float)
    ///   +0x090  posY            (float, AI/patrol position)
    ///   +0x094  posZ            (float)
    ///   +0x098  posX            (float)
    ///   +0x0B0  LivePosY        (float, rendered position)
    ///   +0x0B4  LivePosZ        (float)
    ///   +0x0B8  LivePosX        (float)
    ///   +0x130  scl0            (float)
    ///   +0x134  scl1            (float)
    ///   +0x138  scl2            (float)
    ///   +0x150  b150            (float, purpose unknown)
    ///   +0x154  b154            (float)
    ///   +0x158  b158            (float)
    /// Unexplored gaps logged as hex — likely candidates for AI state / bait timer:
    ///   g04C  +0x04C–+0x053  (2 ints, between BaitAffPetitefish and unk054)
    ///   g05C  +0x05C         (1 int,  between unk058 and Size)
    ///   g06C  +0x06C–+0x073 (2 ints, between ScaleY and Heading)
    ///   g078  +0x078–+0x07F (2 ints, between Heading and speed)
    ///   g088  +0x088–+0x08F (2 ints, between curVel and patrol pos)
    ///   g09C  +0x09C–+0x0AF (5 ints, between patrol pos and LivePos)
    ///   g0BC  +0x0BC–+0x0DB (8 ints, after LivePos)
    /// </summary>
    internal static class FishPhaseLogger
    {
        internal static bool Enabled = true;
        internal static bool AutoCatch = false;

        private static Thread _thread;
        private static volatile bool _running;
        private static int _slotBase;
        private static int _slotCount;

        /// <summary>Called from Fishing.InitFishingSession on the first tick of a session.</summary>
        internal static void OnSessionStart(int slotBase, int slotCount)
        {
            if (!Enabled) return;
            Stop();
            _slotBase  = slotBase;
            _slotCount = slotCount;
            _running   = true;
            _thread    = new Thread(Run) { IsBackground = true, Name = "FishPhaseLogger" };
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
            int frame     = 0;
            int lastPhase = -1;
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
            int lastStableCount = 0;
            for (int i = 0; i < _slotCount; i++) prevHdg[i] = float.NaN;

            while (_running)
            {
                int phase = Memory.ReadInt(FishingState.PhaseAddr);

                if (phase != lastPhase)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishPhase] phase={phase:X2} ({PhaseLabel(phase)})");

                    if (AutoCatch && phase == FishingState.Phase_HookInWater)
                    {
                        for (int i = 0; i < _slotCount; i++)
                        {
                            int s = _slotBase + i * Addresses.fishSlotStride;
                            Memory.WriteInt(s + FishSlotOffsets.AiState, FishingState.FishAiState_Approaching);
                            Memory.WriteInt(s + FishSlotOffsets.AiStateTimer, 0xFF);
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[AutoCatch] hook in water -> wrote Approaching + timer=0xFF to s={i}");
                        }
                    }

                    if (phase == FishingState.Phase_HookInWater)
                    {
                        for (int i = 0; i < _slotCount; i++) { stableFrames[i] = 0; prevHdg[i] = float.NaN; }
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
                        int s = _slotBase + i * Addresses.fishSlotStride;

                        byte  id      = Memory.ReadByte(s);
                        int   aiState = Memory.ReadInt(s + FishSlotOffsets.AiState);
                        float hdg     = Memory.ReadFloat(s + FishSlotOffsets.Heading);
                        float spd     = Memory.ReadFloat(s + 0x080);
                        float vel     = Memory.ReadFloat(s + 0x084);
                        float posX    = Memory.ReadFloat(s + 0x098);
                        float posY    = Memory.ReadFloat(s + 0x090);
                        float posZ    = Memory.ReadFloat(s + 0x094);
                        float lpX     = Memory.ReadFloat(s + FishSlotOffsets.LivePosX);
                        float lpY     = Memory.ReadFloat(s + FishSlotOffsets.LivePosY);
                        float lpZ     = Memory.ReadFloat(s + FishSlotOffsets.LivePosZ);
                        int   u054    = Memory.ReadInt(s + 0x054);
                        int   u058    = Memory.ReadInt(s + 0x058);
                        float b150    = Memory.ReadFloat(s + 0x150);
                        float b154    = Memory.ReadFloat(s + 0x154);
                        float b158    = Memory.ReadFloat(s + 0x158);

                        string g04C = ReadHex(s, 0x04C, 1);
                        string g05C = ReadHex(s, 0x05C, 1);
                        string g06C = ReadHex(s, 0x06C, 2);
                        string g078 = ReadHex(s, 0x078, 2);
                        string g088 = ReadHex(s, 0x088, 2);
                        string g09C = ReadHex(s, 0x09C, 5);
                        string g0BC = ReadHex(s, 0x0BC, 8);

                        // Heading stability tracking
                        if (aiState == FishingState.FishAiState_Approaching && !float.IsNaN(prevHdg[i]))
                        {
                            if (Math.Abs(hdg - prevHdg[i]) < 0.02f)
                            {
                                stableFrames[i]++;
                                if (stableFrames[i] >= 10)
                                {
                                    stableHdg[i] = hdg;
                                    stableLpX[i] = lpX;
                                    stableLpY[i] = lpY;
                                    if (stableFrames[i] == 10)
                                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                            $"[HookTriang] s={i} {FishDatabase.GetName(id)} heading stable: live=({lpX:F2},{lpY:F2}) hdg={hdg:F4}");
                                }
                            }
                            else
                            {
                                stableFrames[i] = 0;
                            }
                        }
                        else if (aiState != FishingState.FishAiState_Approaching)
                        {
                            stableFrames[i] = 0;
                        }
                        prevHdg[i] = aiState == FishingState.FishAiState_Approaching ? hdg : float.NaN;

                        if (aiState != lastAiState[i])
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[FishPhase] f={frame:D5} s={i} {FishDatabase.GetName(id)} aiState {lastAiState[i]:X8} -> {aiState:X8}");
                            lastAiState[i] = aiState;
                        }

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishPhase] f={frame:D5} ph={phase:X2} s={i} {FishDatabase.GetName(id)} " +
                            $"ai={aiState:X8} hdg={hdg:F3} spd={spd:F3} vel={vel:F3} " +
                            $"pos=({posX:F1},{posY:F1},{posZ:F1}) live=({lpX:F1},{lpY:F1},{lpZ:F1}) " +
                            $"u054={u054:X8} u058={u058:X8} " +
                            $"b150={b150:F3} b154={b154:F3} b158={b158:F3} " +
                            $"g04C=[{g04C}] g05C=[{g05C}] g06C=[{g06C}] " +
                            $"g078=[{g078}] g088=[{g088}] g09C=[{g09C}] g0BC=[{g0BC}]");
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
                                float cos1 = (float)Math.Cos(stableHdg[a]), sin1 = (float)Math.Sin(stableHdg[a]);
                                float cos2 = (float)Math.Cos(stableHdg[b]), sin2 = (float)Math.Sin(stableHdg[b]);
                                float denom = cos1 * sin2 - sin1 * cos2;
                                if (Math.Abs(denom) < 0.01f) continue; // parallel vectors
                                float t1 = ((stableLpX[b] - stableLpX[a]) * sin2 - (stableLpY[b] - stableLpY[a]) * cos2) / denom;
                                if (t1 < 0) continue; // intersection behind fish
                                sumX += stableLpX[a] + t1 * cos1;
                                sumY += stableLpY[a] + t1 * sin1;
                                pairs++;
                            }
                        }
                        if (pairs > 0)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[HookTriang] estimated hook=({sumX / pairs:F2},{sumY / pairs:F2}) from {pairs} vector pair(s) ({stableCount} stable slots)");
                        }
                        lastStableCount = stableCount;
                    }

                    frame++;
                }

                Thread.Sleep(16);
            }
        }

        private static string ReadHex(int slotStart, int offset, int count)
        {
            var sb = new System.Text.StringBuilder(count * 9);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Memory.ReadInt(slotStart + offset + i * 4).ToString("X8"));
            }
            return sb.ToString();
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
