using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Passive code-cave finder. Slowly sweeps all 32MB of EE RAM in the background,
    /// tracking which 256-byte chunks stay all-zero across sweeps and sessions.
    /// Results accumulate in one file (CodeCaveFindings.txt next to the exe):
    /// regions confirmed clean gain sweep/session counters, regions ever observed
    /// changing are demoted to REJECTED but kept in the file with the dirty offset,
    /// so no finding is ever lost between sessions.
    ///
    /// Seed the file with static ELF candidates (annotated with neighbouring symbols):
    ///   python3 tools/find_code_caves.py --seed "bin/Debug/net8.0/CodeCaveFindings.txt"
    ///
    /// Caveat: a sampling scanner proves a region is never WRITTEN, not never read or
    /// executed. Regions with symbol context from the static seed are the safest bets.
    /// </summary>
    static class CodeCaveScanner
    {
        public static bool Enabled = true;
        private const int ChunkSize = 256;               // tracking granularity
        private const int ChunkCount = (int)(Memory.EeRamSize / ChunkSize);
        private const int BlockSize = 8192;              // one PINE round-trip
        private const int ChunksPerBlock = BlockSize / ChunkSize;
        private const int MinNewRegion = 0x1000;         // only auto-report caves >= 4KB
        private const int SweepPauseMs = 45000;          // idle between sweeps
        private const int MinSweepsBeforeNewEntries = 3; // skip boot-transient zeros

        // Per-chunk lifecycle: Unknown -> Clean (zero every time seen) or Const
        // (nonzero but stable); anything that changes is Dirty forever.
        private const byte StUnknown = 0, StClean = 1, StConst = 2, StDirty = 3;
        private static readonly byte[] chunkState = new byte[ChunkCount];
        private static readonly ulong[] chunkHash = new ulong[ChunkCount];

        private class Entry
        {
            public long Start;          // PS2 guest address (matches ELF/Ghidra)
            public long Size;
            public string Status;       // CANDIDATE | CLEAN | REJECTED
            public long Sweeps;
            public int Sessions;
            public string FirstSeen = "";
            public string Dirtied = ""; // "yyyy-MM-dd+0xOFFSET" once rejected
            public string Note = "";
            public bool SessionCounted; // in-memory: sessions++ at most once per run
            public byte LiveState;      // in-memory byte-precise state for small entries
            public ulong LiveHash;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static int fileSessions, sweepsTotal, sweepsMenu, sweepsTown, sweepsDungeon;
        private static int sweepsThisSession;
        private static bool sessionCountedInHeader;

        private static string FindingsPath =>
            Path.Combine(AppContext.BaseDirectory, "CodeCaveFindings.txt");

        internal static void Run()
        {
            try
            {
                LoadFindings();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[CodeCave] scanner up: {entries.Count} known regions, findings -> {FindingsPath}");

                while (true)
                {
                    if (!Enabled) { Thread.Sleep(5000); continue; }

                    int mode = Memory.ReadByte(Addresses.mode);
                    if (Sweep())
                    {
                        sweepsTotal++;
                        sweepsThisSession++;
                        if (mode == 2) sweepsTown++;
                        else if (mode == 3) sweepsDungeon++;
                        else sweepsMenu++;
                        UpdateEntries();
                        SaveFindings();
                        if (sweepsThisSession == 1 || sweepsThisSession % 10 == 0)
                            LogSummary();
                    }
                    Thread.Sleep(SweepPauseMs);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[CodeCave] scanner died: " + e);
            }
        }

        /// <summary>One pass over all of EE RAM. Returns false if too many reads failed.</summary>
        private static bool Sweep()
        {
            int failedBlocks = 0;
            for (long off = 0; off < Memory.EeRamSize; off += BlockSize)
            {
                byte[] block = Memory.ReadBytesBatch(Memory.Pcsx2Base + off, BlockSize);
                if (block == null)
                {
                    if (++failedBlocks > 16) return false; // emulator gone/paused hard
                    continue;
                }
                int firstChunk = (int)(off / ChunkSize);
                for (int c = 0; c < ChunksPerBlock; c++)
                    Classify(firstChunk + c, block, c * ChunkSize);
                Thread.Sleep(1); // stay passive: ~4096 round-trips per sweep
            }
            return true;
        }

        /// <summary>Ranges the MOD writes into (in-use code caves, PNACH mailbox). Our own writes must not
        /// count as the game dirtying a region — chunks here are pinned clean and excluded from tracking.
        /// (This is also why findings entries overlapping these ranges stay CLEAN in the file: the region
        /// IS clean as far as the game is concerned; the mod is the only writer.)</summary>
        private static readonly (long Start, long Size)[] ModReserved =
        {
            // One contiguous claim: PNACH mailbox + AI stubs + the decoy tables + the entire CharacterClone
            // band (node pool / cloth / per-bone buffers / weapon / mesh). Authoritative layout lives in
            // CodeCaveAddresses.cs — keep this in sync, or the sweeper starts reporting our OWN writes as the
            // game dirtying the region and quietly rejects caves that are actually clean.
            (0x1F10000, 0xA4300), // 0x1F10000 .. 0x1FB4300 (the whole proven-clean heap tail)
        };

        private static bool IsReserved(int chunk)
        {
            long a = (long)chunk * ChunkSize;
            foreach (var r in ModReserved)
                if (a < r.Start + r.Size && a + ChunkSize > r.Start) return true;
            return false;
        }

        private static void Classify(int chunk, byte[] buf, int offset)
        {
            byte state = chunkState[chunk];
            if (state == StDirty) return;
            if (IsReserved(chunk)) { chunkState[chunk] = StClean; return; }

            bool zero = true;
            ulong hash = 14695981039346656037UL; // FNV-1a 64
            for (int i = 0; i < ChunkSize; i++)
            {
                byte b = buf[offset + i];
                if (b != 0) zero = false;
                hash = (hash ^ b) * 1099511628211UL;
            }

            switch (state)
            {
                case StUnknown:
                    chunkState[chunk] = zero ? StClean : StConst;
                    chunkHash[chunk] = hash;
                    break;
                case StClean:
                    if (!zero) chunkState[chunk] = StDirty;
                    break;
                case StConst:
                    if (chunkHash[chunk] != hash) chunkState[chunk] = StDirty;
                    break;
            }
        }

        private const int PreciseCheckLimit = 0x2000; // <=8KB entries: exact byte range, one round-trip

        /// <summary>Byte-exact version of the chunk state machine for one small entry.
        /// Returns false if the read failed (no verdict this sweep).</summary>
        private static bool PreciseCheck(Entry e, out bool allClean, out long badOff, out string badKind)
        {
            allClean = false;
            badOff = -1;
            badKind = "";
            byte[] buf = Memory.ReadBytesBatch(Memory.Pcsx2Base + e.Start, (int)e.Size);
            if (buf == null) return false;

            bool zero = true;
            long firstNonZero = -1;
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] != 0 && zero) { zero = false; firstNonZero = i; }
                hash = (hash ^ buf[i]) * 1099511628211UL;
            }

            switch (e.LiveState)
            {
                case StUnknown:
                    e.LiveState = zero ? StClean : StConst;
                    e.LiveHash = hash;
                    break;
                case StClean:
                    if (!zero) e.LiveState = StDirty;
                    break;
                case StConst:
                    if (e.LiveHash != hash) e.LiveState = StDirty;
                    break;
            }

            if (e.LiveState == StClean) allClean = true;
            else
            {
                badOff = Math.Max(0, firstNonZero);
                badKind = e.LiveState == StDirty ? "written" : "nonzero";
            }
            return true;
        }

        /// <summary>Summarize the chunk states covering [start, start+size).</summary>
        private static void RangeStates(long start, long size,
            out bool allClean, out long firstBadOffset, out string badKind)
        {
            int c0 = (int)(start / ChunkSize);
            int c1 = (int)((start + size + ChunkSize - 1) / ChunkSize);
            allClean = true;
            firstBadOffset = -1;
            badKind = "";
            for (int c = c0; c < c1 && c < ChunkCount; c++)
            {
                byte s = chunkState[c];
                if (s == StClean) continue;
                allClean = false;
                if (s == StUnknown) continue; // read failure: neither confirms nor rejects
                if (firstBadOffset < 0)
                {
                    firstBadOffset = Math.Max(0, (long)c * ChunkSize - start);
                    // Dirty = game observed writing; Const = nonzero content at first
                    // sight (for a previously-CLEAN entry that means it was written
                    // between sessions). Both disqualify a cave.
                    badKind = s == StDirty ? "written" : "nonzero";
                }
            }
        }

        private static void UpdateEntries()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (!sessionCountedInHeader) { fileSessions++; sessionCountedInHeader = true; }

            foreach (var e in entries)
            {
                if (e.Status == "REJECTED") continue;

                bool allClean;
                long badOff;
                string badKind;
                // Small entries (static seeds) often share 256-byte chunks with live
                // neighbouring buffers - verify those byte-precisely instead.
                if (e.Size <= PreciseCheckLimit)
                {
                    if (!PreciseCheck(e, out allClean, out badOff, out badKind)) continue;
                }
                else
                {
                    RangeStates(e.Start, e.Size, out allClean, out badOff, out badKind);
                }
                if (allClean)
                {
                    if (e.Status == "CANDIDATE")
                    {
                        e.Status = "CLEAN";
                        e.FirstSeen = today;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[CodeCave] CONFIRMED clean: 0x{e.Start:X8} +0x{e.Size:X} ({e.Note})");
                    }
                    e.Sweeps++;
                    if (!e.SessionCounted) { e.Sessions++; e.SessionCounted = true; }
                }
                else if (badOff >= 0)
                {
                    e.Status = "REJECTED";
                    e.Dirtied = $"{today}+0x{badOff:X}({badKind})";
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[CodeCave] rejected 0x{e.Start:X8} +0x{e.Size:X} ({badKind} at +0x{badOff:X})");
                }
            }

            if (sweepsThisSession >= MinSweepsBeforeNewEntries)
                DiscoverNewRegions(today);
        }

        /// <summary>Turn runs of clean chunks not already covered by a live entry into new CLEAN entries.</summary>
        private static void DiscoverNewRegions(string today)
        {
            var live = entries.Where(e => e.Status != "REJECTED")
                              .OrderBy(e => e.Start).ToList();
            int c = 0;
            while (c < ChunkCount)
            {
                if (chunkState[c] != StClean) { c++; continue; }
                int runStart = c;
                while (c < ChunkCount && chunkState[c] == StClean) c++;
                long start = (long)runStart * ChunkSize;
                long end = (long)c * ChunkSize;

                // carve out parts already tracked by live entries
                foreach (var piece in Subtract(start, end, live))
                {
                    if (piece.Item2 - piece.Item1 < MinNewRegion) continue;
                    entries.Add(new Entry
                    {
                        Start = piece.Item1,
                        Size = piece.Item2 - piece.Item1,
                        Status = "CLEAN",
                        Sweeps = sweepsThisSession,
                        Sessions = 1,
                        SessionCounted = true,
                        FirstSeen = today,
                        Note = "runtime-discovered (annotate: tools/find_code_caves.py --annotate 0x" +
                               piece.Item1.ToString("X") + ")"
                    });
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[CodeCave] new clean region 0x{piece.Item1:X8} +0x{piece.Item2 - piece.Item1:X}");
                }
            }
        }

        private static IEnumerable<Tuple<long, long>> Subtract(long start, long end, List<Entry> live)
        {
            long cur = start;
            foreach (var e in live)
            {
                long eEnd = e.Start + e.Size;
                if (eEnd <= cur || e.Start >= end) continue;
                if (e.Start > cur) yield return Tuple.Create(cur, e.Start);
                cur = Math.Max(cur, eEnd);
                if (cur >= end) yield break;
            }
            if (cur < end) yield return Tuple.Create(cur, end);
        }

        private static void LogSummary()
        {
            long cleanBytes = entries.Where(e => e.Status == "CLEAN").Sum(e => e.Size);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[CodeCave] sweep {sweepsThisSession} this session ({sweepsTotal} total): " +
                $"{entries.Count(e => e.Status == "CLEAN")} clean regions " +
                $"({cleanBytes / 1024}KB), {entries.Count(e => e.Status == "CANDIDATE")} unconfirmed, " +
                $"{entries.Count(e => e.Status == "REJECTED")} rejected");
        }

        // ---------- persistence ----------

        private static void LoadFindings()
        {
            if (!File.Exists(FindingsPath)) return;
            foreach (string raw in File.ReadAllLines(FindingsPath))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#"))
                {
                    if (line.Contains("sessions="))
                    {
                        fileSessions = (int)HeaderNum(line, "sessions");
                        sweepsTotal = (int)HeaderNum(line, "sweeps_total");
                        sweepsMenu = (int)HeaderNum(line, "sweeps_menu");
                        sweepsTown = (int)HeaderNum(line, "sweeps_town");
                        sweepsDungeon = (int)HeaderNum(line, "sweeps_dungeon");
                    }
                    continue;
                }
                var e = ParseEntry(line);
                if (e != null) entries.Add(e);
            }
        }

        private static long HeaderNum(string line, string key)
        {
            int i = line.IndexOf(key + "=", StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length + 1;
            int j = i;
            while (j < line.Length && char.IsDigit(line[j])) j++;
            return j > i ? long.Parse(line.Substring(i, j - i)) : 0;
        }

        private static Entry ParseEntry(string line)
        {
            try
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return null;
                var e = new Entry
                {
                    Start = Convert.ToInt64(parts[0], 16),
                    Size = Convert.ToInt64(parts[1], 16),
                    Status = parts[2]
                };
                for (int i = 3; i < parts.Length; i++)
                {
                    string p = parts[i];
                    if (p.StartsWith("note="))
                    {
                        e.Note = line.Substring(line.IndexOf("note=", StringComparison.Ordinal) + 5);
                        break;
                    }
                    if (p.StartsWith("sweeps=")) e.Sweeps = long.Parse(p.Substring(7));
                    else if (p.StartsWith("sessions=")) e.Sessions = int.Parse(p.Substring(9));
                    else if (p.StartsWith("first=")) e.FirstSeen = p.Substring(6);
                    else if (p.StartsWith("dirtied=")) e.Dirtied = p.Substring(8);
                }
                return e;
            }
            catch { return null; }
        }

        private static void SaveFindings()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Dark Cloud code-cave findings - cumulative, managed by CodeCaveScanner.cs");
            sb.AppendLine("# Addresses are PS2 EE guest addresses (add 0x20000000 for PINE/mod access).");
            sb.AppendLine("# CLEAN = all-zero and unwritten in every sweep so far (never-READ not provable);");
            sb.AppendLine("# CANDIDATE = static-analysis seed not yet confirmed; REJECTED = seen changing, kept for history.");
            sb.AppendLine("# Seed static candidates: python3 tools/find_code_caves.py --seed <this file>");
            sb.AppendLine(
                $"# counters sessions={fileSessions} sweeps_total={sweepsTotal} sweeps_menu={sweepsMenu} " +
                $"sweeps_town={sweepsTown} sweeps_dungeon={sweepsDungeon} " +
                $"last_update={DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");

            foreach (var e in entries.OrderBy(x => x.Status == "REJECTED" ? 1 : 0).ThenByDescending(x => x.Size))
            {
                sb.Append(
                    $"0x{e.Start:X8} 0x{e.Size:X6} {e.Status,-9} sweeps={e.Sweeps} sessions={e.Sessions}");
                if (e.FirstSeen != "") sb.Append(" first=" + e.FirstSeen);
                if (e.Dirtied != "") sb.Append(" dirtied=" + e.Dirtied);
                if (e.Note != "") sb.Append(" note=" + e.Note);
                sb.AppendLine();
            }

            string tmp = FindingsPath + ".tmp";
            File.WriteAllText(tmp, sb.ToString());
            File.Copy(tmp, FindingsPath, true);
            File.Delete(tmp);
        }
    }
}
