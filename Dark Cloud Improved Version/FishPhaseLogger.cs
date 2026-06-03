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
    ///   +0x090  aimY            (float, AI/patrol position)
    ///   +0x094  aimZ            (float)
    ///   +0x098  aimX            (float)
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
        internal static bool ForceApproach = false;
        internal static bool WriteNoticeRadius = false;
        internal const  float NoticeRadiusOverride = 100.0f;
        internal static bool ScanSpeciesTable = true;

        private static Thread _thread;
        private static volatile bool _running;
        private static volatile bool _speciesScanDone = false; // reset to false to re-run scan
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
                int phase = Memory.ReadInt(FishingState.PhaseAddr);

                if (phase != lastPhase)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishPhase] phase={phase:X2} ({PhaseLabel(phase)})");

                    if (ForceApproach && phase == FishingState.Phase_HookInWater)
                    {
                        for (int i = 0; i < _slotCount; i++)
                        {
                            int s = _slotBase + i * Addresses.fishSlotStride;
                            Memory.WriteInt(s + FishSlotOffsets.AiState, FishingState.FishAiState_Approaching);
                            Memory.WriteInt(s + FishSlotOffsets.AiStateTimer, 0xFF);
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[ForceApproach] hook in water -> wrote Approaching + timer=0xFF to s={i}");
                        }
                    }


                    if (phase == FishingState.Phase_HookInWater)
                    {
                        for (int i = 0; i < _slotCount; i++) { stableFrames[i] = 0; prevHdg[i] = float.NaN; lastAiState[i] = -2; }
                        lastStableCount = 0;

                        if (ScanSpeciesTable && !_speciesScanDone)
                        {
                            _speciesScanDone = true;
                            new Thread(ScanFor25f) { IsBackground = true, Name = "SpeciesTableScan" }.Start();
                        }
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
                        float spd     = Memory.ReadFloat(s + FishSlotOffsets.Speed);
                        float vel     = Memory.ReadFloat(s + FishSlotOffsets.Velocity);
                        float aimX    = Memory.ReadFloat(s + FishSlotOffsets.AiTargetX);
                        float aimY    = Memory.ReadFloat(s + FishSlotOffsets.AiTargetY);
                        float aimZ    = Memory.ReadFloat(s + FishSlotOffsets.AiTargetZ);
                        slotAimX[i] = aimX;
                        slotAimY[i] = aimY;
                        slotAimZ[i] = aimZ;
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
                        int   unk088 = Memory.ReadInt(s + FishSlotOffsets.Unk088);
                        float nr     = Memory.ReadFloat(s + FishSlotOffsets.NoticeRadius);
                        if (WriteNoticeRadius)
                            Memory.WriteFloat(s + FishSlotOffsets.NoticeRadius, NoticeRadiusOverride);
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
                                    stableHdg[i]  = hdg;
                                    stableLpX[i]  = lpX;
                                    stableLpY[i]  = lpY;
                                    stableLpZ[i]  = lpZ;
                                    stablePosZ[i] = aimZ; // hook depth
                                    if (stableFrames[i] == 10)
                                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                            $"[HookTriang] s={i} {FishDatabase.GetName(id)} heading stable: live=({lpX:F2},{lpY:F2},{lpZ:F2}) hookZ={aimZ:F2} hdg={hdg:F4}");
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

                            // Log notice-range distance when a fish naturally transitions to Approaching.
                            // Requires another slot to already be approaching/nibbling so the hook
                            // position is established — avoids reading AiTarget at the same frame
                            // the game writes it (which may not yet be stable).
                            if (aiState == FishingState.FishAiState_Approaching
                             && lastAiState[i] != FishingState.FishAiState_Approaching
                             && lastAiState[i] != -2)
                            {
                                int refSlot = -1;
                                for (int j = 0; j < _slotCount; j++)
                                {
                                    if (j != i && (lastAiState[j] == FishingState.FishAiState_Approaching
                                                || lastAiState[j] == FishingState.FishAiState_Nibbling))
                                    { refSlot = j; break; }
                                }
                                if (refSlot >= 0)
                                {
                                    float hkX = slotAimX[refSlot], hkY = slotAimY[refSlot], hkZ = slotAimZ[refSlot];
                                    float dx = lpX - hkX, dy = lpY - hkY, dz = lpZ - hkZ;
                                    float dist3d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                    float dist2d = (float)Math.Sqrt(dx * dx + dy * dy);
                                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                        $"[NoticeRange] s={i} {FishDatabase.GetName(id)} " +
                                        $"dist3d={dist3d:F2} dist2d={dist2d:F2} " +
                                        $"fish=({lpX:F2},{lpY:F2},{lpZ:F2}) hook=({hkX:F2},{hkY:F2},{hkZ:F2}) [ref=s{refSlot}]");
                                }
                            }

                            lastAiState[i] = aiState;
                        }

                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishPhase] f={frame:D5} ph={phase:X2} s={i} {FishDatabase.GetName(id)} " +
                            $"ai={aiState:X8} hdg={hdg:F3} spd={spd:F3} vel={vel:F3} " +
                            $"aim=({aimX:F1},{aimY:F1},{aimZ:F1}) live=({lpX:F1},{lpY:F1},{lpZ:F1}) " +
                            $"u054={u054:X8} u058={u058:X8} " +
                            $"b150={b150:F3} b154={b154:F3} b158={b158:F3} " +
                            $"g04C=[{g04C}] g05C=[{g05C}] g06C=[{g06C}] " +
                            $"g078=[{g078}] u088={unk088:X8} nr={nr:F1} g09C=[{g09C}] g0BC=[{g0BC}]");
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
                            float estX = sumX / pairs, estY = sumY / pairs;
                            // Use AI destination Z (hook depth) not fish live Z
                            float estZ = 0; int zc = 0;
                            for (int i = 0; i < _slotCount; i++)
                                if (stableFrames[i] >= 10) { estZ += stablePosZ[i]; zc++; }
                            estZ /= zc;

                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[HookTriang] estimated hook=({estX:F2},{estY:F2},{estZ:F2}) from {pairs} vector pair(s) ({stableCount} stable slots)");

                        }
                        lastStableCount = stableCount;
                    }

                    frame++;
                }

                Thread.Sleep(16);
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
            var sb = new System.Text.StringBuilder(count * 9);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Memory.ReadInt(slotStart + offset + i * 4).ToString("X8"));
            }
            return sb.ToString();
        }

        private static void ScanFor25f()
        {
            const long  scanStart = 0x20000000L;
            const long  scanEnd   = 0x22000000L;
            const int   batchSize = 1024;
            const float target    = 25.0f;
            const float tol       = 0.01f;

            long totalBatches = ((scanEnd - scanStart) / 4 + batchSize - 1) / batchSize;
            var hits = new System.Collections.Generic.List<int>();

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SpeciesScan] scanning EE RAM for {target}f (0x{BitConverter.ToUInt32(BitConverter.GetBytes(target), 0):X8})...");

            for (long b = 0; b < totalBatches && _running; b++)
            {
                long  addr  = scanStart + b * batchSize * 4;
                int   count = (int)Math.Min(batchSize, (scanEnd - addr) / 4);
                float[] fs  = Memory.ReadFloatBatch(addr, count);
                for (int i = 0; i < count; i++)
                    if (Math.Abs(fs[i] - target) < tol)
                        hits.Add((int)(addr + i * 4));
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SpeciesScan] {hits.Count} hit(s) total");

            // Cluster analysis: find groups of 3+ addresses with a consistent stride.
            // A species table would appear as N entries equally spaced in memory.
            hits.Sort();
            for (int i = 0; i < hits.Count - 2; i++)
            {
                int stride = hits[i + 1] - hits[i];
                if (stride < 4) continue;
                int len = 2;
                while (i + len < hits.Count && hits[i + len] - hits[i + len - 1] == stride)
                    len++;
                if (len >= 3)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[SpeciesScan] cluster base=0x{(uint)hits[i]:X8} stride=0x{stride:X4} ({stride}) count={len}");

                    // Dump surrounding context for each entry: 16 bytes before the hit
                    // (2 extra entries worth) through the end of the stride.
                    int dumpWords = Math.Max(stride / 4, 4); // at least 4 DWORDs
                    for (int e = 0; e < len; e++)
                    {
                        long entryBase = hits[i + e] - 16; // 16 bytes of pre-context (2 entries)
                        int  words     = dumpWords + 4;    // +4 for the pre-context
                        float[] data = Memory.ReadFloatBatch(entryBase, words);
                        var sb = new System.Text.StringBuilder();
                        for (int w = 0; w < words; w++)
                        {
                            uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(data[w]), 0);
                            if (w == 4) { sb.Append($"[{raw:X8}={data[w]:F1}f]"); }      // hit (shifted by 2 extra pre-words)
                            else if (w == 5) { sb.Append($" id={raw}({BaitName(raw)})"); }
                            else { sb.Append($" {raw:X8}"); }
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[SpeciesScan]   e{e} 0x{(uint)hits[i+e]:X8}: {sb}");
                    }

                    // Write distinct values: 2 entries before cluster, all cluster entries, 1 after.
                    // evy (193) is likely the entry 2 strides before cluster (0x2026AE8C); mimi is 1 stride before.
                    // Values: e=-2→98, e=-1→99(mimi), e=0→100, ..., e=10→110, e=11→111(post).
                    for (int e = -2; e <= len; e++)
                    {
                        int  addr     = hits[i] + e * stride;
                        float writeVal = 98.0f + (e + 2); // -2→98, -1→99, 0→100, ..., 11→111
                        Memory.WriteFloat(addr, writeVal);
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[SpeciesScan] wrote 98..{97 + len + 3}f to {len + 3} entries (cluster + 3 border entries)");

                    // Patch the trailing id=0 (bare-hook) entry's item ID to cheese (155)
                    // to test whether the game will treat a non-bait item as valid bait.
                    Memory.WriteInt(BaitNoticeRadiusTable.Unknown14.Id, 155);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        "[SpeciesScan] patched no-bait entry item id → 155 (cheese)");

                    i += len - 1;
                }
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SpeciesScan] done");
        }

        private static string BaitName(uint id) => id switch
        {
            166 => "throbbingcherry",
            167 => "gooeypeach",
            168 => "bombnuts",
            169 => "poisonousapple",
            170 => "mellowbanana",
            186 => "carrot",
            187 => "potatocake",
            188 => "minon",
            189 => "battan",
            190 => "petitefish",
            193 => "evy",
            197 => "mimi",
            199 => "prickly",
            _   => $"?{id}",
        };

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
