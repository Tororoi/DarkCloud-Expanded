using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// "Harder enemy AI" gameplay toggle. First behavior: EVERY enemy with a legitimate get-up motion can
    /// REVIVE after death, and the five natively-reviving undead revive far more often.
    ///
    /// How (all data writes, per floor):
    ///  • Native revivers (Master Jacket / Mummy / Gacious / Horn Head + Enhanced — seven total): their
    ///    death label already rolls `_GET_RAND(100) &lt; threshold → revive`; the threshold literal is
    ///    simply raised (10-18 → <see cref="NativeReviverChance"/>).
    ///  • Everyone else with a LEGITIMATE get-up motion: label 120's real entry is funcdata[0] (the label
    ///    table entry points at a 16-byte funcdata {codeOff,?,locals,args}, execution starts at
    ///    codeBase+codeOff — NOT at labelOff+8, which is inside the header!). Every target's intro runs
    ///    `cmd 34` then pushes cmd 101 (invulnerability) — we overwrite that one push cell with an op16
    ///    unconditional JMP into CODE-CAVE bytecode that rolls exactly where the native revivers roll
    ///    (before the label's own fade/invincibility). Revive path clones the mummy's native recipe using
    ///    only THIS script's parts: its own collapse SET_MOTION run + its own wait-CALL cluster (op19
    ///    targets are codeBase-relative → script-local library calls work verbatim from the cave), then
    ///    SET_LIFE(float FRACTION of max HP — native semantic), SET_MOTION(get-up), the wait cluster again
    ///    retargeted to motion 10 and its end frame, SET_MOTION(idle), push 0, RET. Death path replays the
    ///    displaced push and jumps back. The label frame has locals=0 in every target — the roll needs
    ///    var0, so the funcdata locals field is raised to 1 (restored on unpatch).
    ///  • Loaded STBs are located by 96-byte file signatures (loaded scripts are byte-identical to the
    ///    files) via ONE multi-needle RAM sweep per floor; STBs reload per floor, so patches re-apply per
    ///    floor and stale addresses are never touched.
    /// Cave region: 0x1F10100 (CodeCaveScanner-proven heap tail; see cave layout comment below).
    /// </summary>
    internal static class HarderEnemyAI
    {
        internal static bool Enabled = false;
        internal static bool DebugAlwaysRevive = true;   // TEMP diagnostic: roll threshold 100 → every kill revives

        private const int SplicedReviveChance = 30;   // % revive for species that never natively revived
        private const int NativeReviverChance = 45;   // % revive for the seven native revivers (10-18% natively)
        private const float ReviveHpFraction  = 0.4f; // revive at this fraction of max HP (_STATUS_SET_LIFE takes a float fraction; mummy natively uses rand(0.3)+0.3)
        private const int GetUpMotionIdx      = 10;   // universal: every legit get-up is motion 10
        private const int  SigBytes  = 96;

        // ── cave layout (authoritative map: CodeCaveAddresses.cs) ──
        // Per-species STB stubs starting just past the PNACH mailbox, in the heap-tail region CodeCaveScanner
        // has verified clean across every session/sweep on record. Branch (op16/17/18) and CALL (op19) targets
        // all resolve per-script (pc = executing script's stbBase+codeBase + operand), and every stub calls its
        // own script's wait subroutine — so nothing is shareable across species; each species gets one
        // self-contained stub per loaded copy.
        //
        // The region grows UPWARD into Mirage's caves, so it is hard-bounded at MaxStubs (see CodeCaves):
        // overrunning would silently scribble over the decoy aggro table (0x1F30000) and the clone frame tree.
        //
        // DO NOT use the BSS-slack gaps around 0x2A4000: 0x2A4000 itself is INSIDE tocbf (the CD
        // table-of-contents buffer — the game rewrites it during disc streaming, which corrupted the v4
        // stub mid-floor and crashed the VM to the BIOS browser), and the real gaps there are <0x140 bytes.
        private const long StubBase   = CodeCaves.AiStubBase;
        private const int  StubStride = CodeCaves.AiStubStride;   // up to 85 cells (roll + MUTEKI + motion run + 2 wait clusters)
        private const int  MaxStubs   = CodeCaves.AiStubMaxSlots; // 32 — one stub per SPLICED SPECIES on a floor,
                                                                  // not per enemy; a floor has a handful. Capped so
                                                                  // this can't grow into the clone caves that follow.

        /// <summary>Every enemy model code with a LEGITIMATE get-up (起き上がり) motion, all at index 10.
        /// Generated from docs/enemy-motion-table.md and filtered: 39 of the 70 "get up" entries are
        /// VESTIGIAL placeholders whose frame range strictly overlaps another motion (e.g. Dasher's get-up
        /// f10-20 == idle, the dragons' f30-50 == walk) — playing those would show a fragment of the wrong
        /// animation, so they are NOT revive-eligible. Validation: all seven native revivers pass the
        /// overlap test (their get-ups must be real — the game plays them).</summary>
        private static readonly HashSet<string> GetUpCodes = new HashSet<string>
        {
            "e01a","e03a","e14a","e15a","e16a","e17a","e18a","e19a","e20a","e44a","e45a","e46a","e47a",
            "e48a","e50a","e52a","e62a","e77a","e106","e124","e129","e131","e132","e119","e134","e138",
            "e144","e118","e150","e117","e161",
        };

        /// <summary>The eight native revivers: file offset of the revive-roll threshold literal (see
        /// CustomToanEffects.UndeadRevivers — same cells; buffed here instead of spliced). Complete per
        /// TWO rigid pattern sweeps of all 172 monster STBs: the mummy shape (roll after cmd 34) and the
        /// Silver Gear shape (rand BEFORE cmd 34, compare after — e118 is its only member).</summary>
        private static readonly Dictionary<string, (int ThrCell, int OrigThr)> NativeRevivers =
            new Dictionary<string, (int, int)>
        {
            { "e01a",  (0x715C, 10) },   // Master Jacket
            { "e50a",  (0x4A08, 18) },   // Mummy
            { "e117",  (0x7550, 16) },   // Gacious (Enhanced) (file e117a.stb)
            { "e119",  (0x54DC, 14) },   // Horn Head (file e119a.stb)
            { "e124",  (0x7494, 16) },   // Gacious (file e124a.stb)
            { "e129",  (0x7174, 10) },   // Master Jacket (Enhanced)
            { "e131",  (0x4A08, 18) },   // Mummy (Enhanced)
            { "e118",  (0x7F38, 15) },   // Silver Gear (roll interleaved with the cmd-34 intro; revive = CALL its own sub)
        };

        private sealed class Patch
        {
            public long Addr; public byte[] Original; public byte[] Written;
        }

        private static Thread _thread;
        private static readonly List<Patch> _patches = new List<Patch>();
        private static byte _floor = 0xFF;
        private static bool _appliedThisFloor;
        private static int _applyAttempts;
        private const int MaxApplyAttempts = 10;   // × 2s — covers slow floor loads
        private static bool _wasActive;

        internal static void StartThread()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
            Console.WriteLine("[HarderAI] watcher thread started");
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    if (!Memory.IsConnected) continue;

                    bool active = Enabled && Player.InDungeonFloor();
                    if (active && !_wasActive)
                    {
                        Console.WriteLine("[HarderAI] active (toggle on + in dungeon)");
                        _applyAttempts = 0;   // fresh activation (incl. mid-floor re-toggle) → full retry budget
                    }
                    _wasActive = active;
                    if (!active)
                    {
                        if (_patches.Count > 0 && !Enabled) RestoreAll();   // toggled off mid-floor → undo
                        if (!Player.InDungeonFloor()) { _patches.Clear(); _floor = 0xFF; _appliedThisFloor = false; }
                        continue;
                    }

                    byte f = Memory.ReadByte(Addresses.checkFloor);
                    if (f != _floor)
                    {
                        _floor = f;
                        _patches.Clear();          // previous floor's STBs are gone — never touch stale addresses
                        _appliedThisFloor = false;
                        _applyAttempts = 0;
                    }
                    // The floor number updates BEFORE the slots/scripts finish loading (observed live:
                    // checkFloor flips while the model buffers are still streaming), so apply is retried
                    // until it actually lands — never one-shot on the floor-change edge.
                    if (!_appliedThisFloor && _applyAttempts < MaxApplyAttempts)
                    {
                        _applyAttempts++;
                        if (ApplyFloor())
                            _appliedThisFloor = true;
                        else
                            Thread.Sleep(1500);   // floor still loading — try again shortly
                    }
                }
                catch (Exception e) { Console.WriteLine($"[HarderAI] tick failed: {e.Message}"); }
            }
        }

        // ── per-floor application ────────────────────────────────────────────────────────────

        /// <summary>One application attempt. Returns true when finished for this floor (patched, or
        /// legitimately nothing to patch) — false means "floor not ready yet, retry".</summary>
        private static bool ApplyFloor()
        {
            if (!GameDataFiles.Available) { Console.WriteLine("[HarderAI] game data files unavailable — skipping"); return true; }

            // Which species are on this floor?
            var ids = new HashSet<int>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                if (id > 0 && id < 0xFFFF) ids.Add(id);
            }
            if (ids.Count == 0) return false;   // slots not populated yet — floor still loading

            // Candidate model codes (base + Enhanced share ids; scan for all, patch what's found loaded).
            var codes = new HashSet<string>();
            foreach (EnemyDefaults d in EnemySpecies.All.Values)
                if (d.ModelCode != null && ids.Contains(d.Id) && GetUpCodes.Contains(d.ModelCode))
                    codes.Add(d.ModelCode);
            if (codes.Count == 0) { Console.WriteLine($"[HarderAI] ids {string.Join(",", ids)} → no get-up-eligible species on this floor"); return true; }

            // Build one needle per candidate from its stb file on disk.
            var needles = new List<(byte[] Sig, int SigFileOff, string Code, byte[] File, bool IsNative)>();
            foreach (string code in codes)
            {
                string stb = "monstor\\" + code + (code.Length == 4 && char.IsDigit(code[3]) ? "a" : "") + ".stb";
                byte[] file = GameDataFiles.TryReadEntry(stb);
                if (file == null) continue;

                bool native = NativeRevivers.ContainsKey(code);
                int anchor = native ? NativeRevivers[code].ThrCell
                                    : (FindLabel120(file, out _, out int execOff) ? execOff : -1);
                if (anchor < SigBytes) continue;

                // Widen the window until the signature is unique within the file.
                int w = SigBytes;
                while (w < anchor && CountOccurrences(file, anchor - w, w) > 1) w += 48;
                var sig = new byte[w];
                Array.Copy(file, anchor - w, sig, 0, w);
                needles.Add((sig, anchor - w, code, file, native));
            }
            if (needles.Count == 0) { Console.WriteLine($"[HarderAI] codes {string.Join(",", codes)} → no needles built (stb read/parse failed)"); return true; }
            Console.WriteLine($"[HarderAI] sweeping for {needles.Count} script(s): {string.Join(",", codes)}");

            // One RAM sweep matching every needle.
            int caveSlot = 0, buffed = 0, spliced = 0;
            const int Block = 0x40000;
            int maxNeedle = 0; foreach (var nd in needles) maxNeedle = Math.Max(maxNeedle, nd.Sig.Length);
            for (long off = 0; off < Memory.EeRamSize; off += Block - (maxNeedle - 1))
            {
                int size = (int)Math.Min(Block, Memory.EeRamSize - off);
                if (size <= maxNeedle) break;
                byte[] buf;
                try { buf = Memory.ReadBytesBatch(Memory.Pcsx2Base + off, size); }
                catch { continue; }
                if (buf == null) continue;

                for (int i = 0; i < buf.Length; i++)
                {
                    foreach (var nd in needles)
                    {
                        if (i + nd.Sig.Length > buf.Length || buf[i] != nd.Sig[0]) continue;
                        bool match = true;
                        for (int j = 1; j < nd.Sig.Length && match; j++) match = buf[i + j] == nd.Sig[j];
                        if (!match) continue;

                        long stbBase = Memory.Pcsx2Base + off + i - nd.SigFileOff;
                        if (nd.IsNative)
                        {
                            if (BuffNativeReviver(stbBase, nd.Code)) buffed++;
                        }
                        else
                        {
                            if (SpliceReviveRoll(stbBase, nd.File, nd.Code, caveSlot)) { spliced++; caveSlot++; }
                        }
                    }
                }
            }
            if (buffed + spliced == 0)
            {
                Console.WriteLine($"[HarderAI] sweep found no loaded scripts yet (floor {_floor}, attempt {_applyAttempts}) — will retry");
                return false;   // scripts not in RAM yet — retry
            }
            Console.WriteLine($"[HarderAI] result: {spliced} spliced, {buffed} native buffed (floor {_floor})");
            return true;
        }

        /// <summary>Raise a native reviver's threshold literal to <see cref="NativeReviverChance"/>.
        /// Skips if Cross Hinder has sanctified it (threshold 0 — the holy weapon wins).</summary>
        private static bool BuffNativeReviver(long stbBase, string code)
        {
            (int thrCell, int origThr) = NativeRevivers[code];
            long cell = stbBase + thrCell;
            if (Memory.ReadInt(cell) != 3 || Memory.ReadInt(cell + 4) != 1) return false;   // not a push-t1 → wrong hit
            int cur = Memory.ReadInt(cell + 8);
            if (cur == 0 || cur > 100) return false;   // 0 = Cross Hinder's mark; >100 = not a sane threshold
            if (cur == NativeReviverChance) return true;

            var p = new Patch { Addr = cell + 8, Original = BitConverter.GetBytes(cur), Written = BitConverter.GetBytes(NativeReviverChance) };
            Memory.WriteInt(cell + 8, NativeReviverChance);
            _patches.Add(p);
            return true;
        }

        /// <summary>Splice a revive roll into a plain death, cloning the NATIVE reviver recipe (mummy
        /// e50a label 120) out of the target script's own parts. Patch point: the single `push cmd 101`
        /// cell right after the intro's `cmd 34` EXT — exactly where mummy rolls, and BEFORE the label's
        /// own invulnerability+fade (which made v2's revives invisible). One op16 JMP → cave stub:
        ///   roll _GET_RAND(100) &lt; chance ?
        ///   revive → [own collapse SET_MOTION run, verbatim] [own wait-CALL cluster, verbatim — op19
        ///            resolves script-locally even from the cave] SET_LIFE(0.4f of max — float-fraction
        ///            semantic, verified native) SET_MOTION(10) [wait cluster retargeted to motion 10 and
        ///            its end frame] SET_MOTION(0 idle) push 0 RET — the engine sees HP &gt; 0 and the enemy
        ///            simply resumes its AI, exactly like a native reviver;
        ///   death  → replay the displaced push-101 cell, JMP back into the untouched label.
        /// The label's funcdata locals field (labelOff+8) is raised 0→1 for the roll's var0.</summary>
        private static bool SpliceReviveRoll(long stbBase, byte[] file, string code, int caveSlot)
        {
            uint U32(int o) => BitConverter.ToUInt32(file, o);
            if (!FindLabel120(file, out int labelOff, out int execOff)) return false;

            // Patch point: the `push cmd 101` immediately after the intro's `cmd 34; EXT(1)` pair.
            int p = -1;
            for (int k = 0; k < 10; k++)
            {
                int c = execOff + k * 12;
                if (c + 36 > file.Length) break;
                if (U32(c) == 3 && U32(c + 4) == 1 && U32(c + 8) == 34 &&
                    U32(c + 12) == 21 && U32(c + 16) == 1 && U32(c + 20) == 0) { p = c + 24; break; }
            }
            if (p < 0 || U32(p) != 3 || U32(p + 4) != 1 || U32(p + 8) != 101)
            { Console.WriteLine($"[HarderAI] {code}: label-120 intro shape unrecognized — skipped"); return true; }

            long patchAddr = stbBase + p;
            if (Memory.ReadInt(patchAddr) == 16) return true;   // already spliced (op16 JMP)
            byte[] live = Memory.ReadBytesBatch(patchAddr, 12);
            if (live == null) return false;
            for (int i = 0; i < 12; i++)
                if (live[i] != file[p + i]) return false;       // not the loaded copy (or mid-load) — retry

            // The target's OWN collapse + wait, verbatim. Two label layouts exist:
            //  inline (most): [SET_MOTION run][wait-CALL cluster] directly in label 120;
            //  sub-call (e52a): the label CALLs an argless subroutine whose body IS that same unit —
            //  then [CALL][drop] verbatim covers collapse AND wait, and the get-up wait template is
            //  extracted from inside the sub body with the same helpers.
            int codeBaseEarly = (int)U32(8);
            uint[][] motion = ExtractDeathMotionRun(file, execOff, out int afterRun);
            uint[][] wait = null, getUpWaitTemplate = null;
            if (motion != null)
            {
                wait = ExtractWaitCluster(file, afterRun);
                if (wait == null) { Console.WriteLine($"[HarderAI] {code}: no wait-CALL cluster after the collapse — skipped"); return true; }
                getUpWaitTemplate = wait;
            }
            else
            {
                for (int c = p; c + 24 <= file.Length && c < p + 30 * 12 && motion == null; c += 12)
                {
                    if (U32(c) == 15) break;                                             // label RET — nothing found
                    if (U32(c) != 19 || U32(c + 12) != 4) continue;                      // want [CALL][drop]
                    int fd = codeBaseEarly + (int)U32(c + 8);                            // funcdata of the callee
                    if (fd + 16 > file.Length || U32(fd + 12) != 0) continue;            // must be ARGLESS
                    uint[][] subRun = ExtractDeathMotionRun(file, codeBaseEarly + (int)U32(fd), out int subAfter);
                    if (subRun == null) continue;
                    uint[][] subWait = ExtractWaitCluster(file, subAfter);
                    if (subWait == null) continue;
                    if (ReviveCollapseMotion.ContainsKey(code))
                    {
                        // Fake-death species: the knockdown retarget can't reach inside the CALLed sub,
                        // so inline the sub's own run + wait cluster instead (position-independent cells;
                        // its wait CALL still resolves script-locally from the cave).
                        motion = subRun;
                        wait = subWait;
                    }
                    else
                    {
                        motion = new[] { new[] { U32(c), U32(c + 4), U32(c + 8) },       // CALL collapse-sub
                                         new[] { 4u, 0u, 0u } };                         // drop result
                        wait = new uint[0][];                                            // sub already waited
                    }
                    getUpWaitTemplate = subWait;
                }
                if (motion == null) { Console.WriteLine($"[HarderAI] {code}: no standard death-motion run — skipped"); return true; }
            }

            // Get-up variant of the cluster: the motion-index arg (the int push DIRECTLY BEFORE the op6
            // ADD — keyed by position, not value: King Prickly's cluster says 11 while its SET_MOTION uses
            // 9) → the get-up index; the end-frame float → get-up end − 5. (Argless clusters — e44a
            // family, mummy-style — have neither: their sub reads the current motion itself.) The frame
            // margin mirrors the compiler's own (e03a waits to 214 of 225).
            int getUpIdx = GetUpMotionIdx;
            var waitGetUp = new uint[getUpWaitTemplate.Length][];
            bool needFrame = false;
            for (int i = 0; i < getUpWaitTemplate.Length; i++)
            {
                waitGetUp[i] = new[] { getUpWaitTemplate[i][0], getUpWaitTemplate[i][1], getUpWaitTemplate[i][2] };
                if (getUpWaitTemplate[i][0] == 3 && getUpWaitTemplate[i][1] == 1 &&
                    i + 1 < getUpWaitTemplate.Length && getUpWaitTemplate[i + 1][0] == 6) waitGetUp[i][2] = (uint)getUpIdx;
                if (getUpWaitTemplate[i][0] == 3 && getUpWaitTemplate[i][1] == 2) needFrame = true;
            }
            if (needFrame)
            {
                if (!GetUpEndFrame.TryGetValue(code, out int endF))
                { Console.WriteLine($"[HarderAI] {code}: get-up end frame unknown — skipped"); return true; }
                uint bits = BitConverter.ToUInt32(BitConverter.GetBytes((float)(endF - 5)), 0);
                for (int i = 0; i < waitGetUp.Length; i++)
                    if (waitGetUp[i][0] == 3 && waitGetUp[i][1] == 2) waitGetUp[i][2] = bits;
            }

            long progOrigin = stbBase + codeBaseEarly;   // op16/17/19 operands are relative to this
            // The stub region grows upward into Mirage's caves — refuse to splice past the wall rather than
            // silently overwrite the decoy aggro table / clone frame tree (see CodeCaveAddresses.cs).
            if (caveSlot >= MaxStubs)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[HarderAI] cave full: slot {caveSlot} >= {MaxStubs} (would run into Mirage's caves @0x{CodeCaves.PtrTable:X}) — skipping {code}");
                return false;
            }
            long stub = StubBase + (long)caveSlot * StubStride;
            uint hpBits = BitConverter.ToUInt32(BitConverter.GetBytes(ReviveHpFraction), 0);

            var cells = new List<uint[]>
            {
                new uint[] {  3, 1, 180 },                     // push cmd _GET_RAND
                new uint[] {  3, 1, 100 },                     // push 100
                new uint[] {  2, 0, 1 },                       // out-ref var0 (int)
                new uint[] { 21, 3, 0 },                       // EXT → rand(100) → var0
                new uint[] {  1, 0, 1 },                       // push var0
                new uint[] {  3, 1, (uint)(DebugAlwaysRevive ? 100 : SplicedReviveChance) },
                new uint[] { 14, 0x2A, 0 },                    // CMP: rand < chance
                new uint[] { 17, 0, 0 },                       // BR_FALSE → death (retargeted below)
            };
            int brDeath = cells.Count - 1;
            // Fake-death species: retarget the collapse to the knockdown motion (copies stay pristine —
            // getUpWaitTemplate aliases the extracted cluster).
            bool fakeDeath = ReviveCollapseMotion.TryGetValue(code, out (int Motion, int EndFrame) fake);
            if (fakeDeath)
            {
                motion[1] = new[] { 3u, 1u, (uint)fake.Motion };
                uint fBits = BitConverter.ToUInt32(BitConverter.GetBytes((float)(fake.EndFrame - 5)), 0);
                var retgt = new uint[wait.Length][];
                for (int i = 0; i < wait.Length; i++)
                {
                    retgt[i] = new[] { wait[i][0], wait[i][1], wait[i][2] };
                    if (wait[i][0] == 3 && wait[i][1] == 1 &&
                        i + 1 < wait.Length && wait[i + 1][0] == 6) retgt[i][2] = (uint)fake.Motion;
                    if (wait[i][0] == 3 && wait[i][1] == 2) retgt[i][2] = fBits;
                }
                wait = retgt;
            }
            cells.Add(new uint[] {  3, 1, 101 });              // _STATUS_SET_MUTEKI(120) — Silver Gear's
            cells.Add(new uint[] {  3, 1, 120 });              // native revive guards the knockdown briefly
            cells.Add(new uint[] { 21, 2, 0 });
            cells.AddRange(motion);                            // collapse (verbatim, or knockdown-retargeted)
            cells.AddRange(wait);                              // wait for it to finish
            {
                // Silver Gear-style lie-still pause for EVERY spliced reviver: its native revive counts
                // down rand(40)+60 ticks between the collapse and the get-up (ours: rand(40)+30, a touch
                // snappier per user taste). Cloned cell-for-cell
                // (op22/op23 exactly as native — never synthesized beyond this proven shape); the loop is
                // self-contained, so the same cells work in any species' stub. Targets are stub-absolute.
                cells.Add(new uint[] {  3, 1, 180 });          // _GET_RAND
                cells.Add(new uint[] {  3, 1, 40 });
                cells.Add(new uint[] {  2, 0, 1 });            //   → var0
                cells.Add(new uint[] { 21, 3, 0 });
                cells.Add(new uint[] {  2, 0, 1 });            // var0 += 30
                cells.Add(new uint[] {  1, 0, 1 });
                cells.Add(new uint[] {  3, 1, 30 });
                cells.Add(new uint[] {  6, 0, 0 });
                cells.Add(new uint[] {  5, 0, 0 });
                cells.Add(new uint[] {  4, 0, 0 });
                int loopTop = cells.Count;                     // exit = loopTop + 13
                cells.Add(new uint[] { 22, 0, 0 });
                cells.Add(new uint[] {  1, 0, 1 });            // var0 != 0 ?
                cells.Add(new uint[] {  3, 1, 0 });
                cells.Add(new uint[] { 14, 0x2C, 0 });
                cells.Add(new uint[] { 17, (uint)(stub + (loopTop + 13) * 12 - progOrigin), 0 });   // == 0 → exit
                cells.Add(new uint[] {  2, 0, 1 });            // var0 -= 1
                cells.Add(new uint[] {  1, 0, 1 });
                cells.Add(new uint[] {  3, 1, 1 });
                cells.Add(new uint[] {  7, 0, 0 });
                cells.Add(new uint[] {  5, 0, 0 });
                cells.Add(new uint[] {  4, 0, 0 });
                cells.Add(new uint[] { 23, 0, 0 });
                cells.Add(new uint[] { 16, (uint)(stub + loopTop * 12 - progOrigin), 0 });          // JMP loop
                cells.Add(new uint[] { 22, 1, 0 });            // exit (native pairs the op22s this way)
            }
            cells.Add(new uint[] {  3, 1, 228 });              // _STATUS_SET_LIFE
            cells.Add(new uint[] {  3, 2, hpBits });           //   float fraction of max HP
            cells.Add(new uint[] { 21, 2, 0 });
            cells.Add(new uint[] {  3, 1, 200 });              // _SET_MOTION
            cells.Add(new uint[] {  3, 1, (uint)getUpIdx });   //   get-up (universal index 10)
            cells.Add(new uint[] { 21, 2, 0 });
            cells.AddRange(waitGetUp);                         // let the get-up play out
            cells.Add(new uint[] {  3, 1, 200 });              // _SET_MOTION
            cells.Add(new uint[] {  3, 1, 0 });                //   idle (native revivers end this way)
            cells.Add(new uint[] { 21, 2, 0 });
            cells.Add(new uint[] {  3, 1, 0 });                // push return value
            cells.Add(new uint[] { 15, 0, 0 });                // RET — engine resumes the live enemy's AI
            int deathIdx = cells.Count;
            cells.Add(new uint[] { U32(p), U32(p + 4), U32(p + 8) });                    // replay push-101
            cells.Add(new uint[] { 16, (uint)(patchAddr + 12 - progOrigin), 0 });        // JMP back into label
            cells[brDeath] = new uint[] { 17, (uint)(stub + deathIdx * 12 - progOrigin), 0 };

            if (cells.Count * 12 > StubStride)
            { Console.WriteLine($"[HarderAI] {code}: stub too large ({cells.Count} cells) — skipped"); return true; }

            var caveBytes = new byte[cells.Count * 12];
            for (int c = 0; c < cells.Count; c++)
            {
                BitConverter.GetBytes(cells[c][0]).CopyTo(caveBytes, c * 12);
                BitConverter.GetBytes(cells[c][1]).CopyTo(caveBytes, c * 12 + 4);
                BitConverter.GetBytes(cells[c][2]).CopyTo(caveBytes, c * 12 + 8);
            }
            Memory.WriteBytesBatch(stub, caveBytes);

            // The roll needs var0, but every splice target's label 120 has funcdata locals = 0.
            long localsAddr = stbBase + labelOff + 8;
            int curLocals = Memory.ReadInt(localsAddr);
            if (curLocals < 1)
            {
                Memory.WriteInt(localsAddr, 1);
                _patches.Add(new Patch { Addr = localsAddr, Original = BitConverter.GetBytes(curLocals), Written = BitConverter.GetBytes(1) });
            }

            // Splice: the push-101 cell becomes an op16 unconditional JMP → stub.
            var jump = new byte[12];
            BitConverter.GetBytes(16).CopyTo(jump, 0);
            BitConverter.GetBytes((uint)(stub - progOrigin)).CopyTo(jump, 4);
            Memory.WriteBytesBatch(patchAddr, jump);
            _patches.Add(new Patch { Addr = patchAddr, Original = live, Written = jump });
            Console.WriteLine($"[HarderAI] {code}: label@0x{patchAddr:X} → stub@0x{stub:X} ({cells.Count} cells, chance {(DebugAlwaysRevive ? 100 : SplicedReviveChance)}%)");
            return true;
        }

        /// <summary>Label 120's table entry (→ its 16-byte funcdata {codeOff,?,locals,args}) and the file
        /// offset of its first EXECUTED cell (codeBase + codeOff). NOTE: labelOff+8 is the locals field of
        /// the header, NOT code — patching there corrupts the frame setup (the v3/v4 bug).</summary>
        private static bool FindLabel120(byte[] file, out int labelOff, out int execOff)
        {
            uint U32(int o) => BitConverter.ToUInt32(file, o);
            int tbl = (int)U32(0xC), cnt = (int)U32(0x10), cb = (int)U32(8);
            labelOff = -1; execOff = -1;
            for (int i = 0; i < cnt; i++)
                if ((int)U32(tbl + i * 8) == 120)
                {
                    labelOff = (int)U32(tbl + i * 8 + 4);
                    execOff = cb + (int)U32(labelOff);
                    return execOff > 0 && execOff + 12 <= file.Length;
                }
            return false;
        }

        /// <summary>The target's death-motion call in label 120, copied verbatim: the cell run from
        /// `push[t1] 200` (_SET_MOTION) through its closing EXT. Cells must be position-independent
        /// (pushes/arithmetic only — no branches/calls); <paramref name="afterRun"/> = file offset just
        /// past the EXT. Returns null if absent or unclean.</summary>
        private static uint[][] ExtractDeathMotionRun(byte[] file, int execOff, out int afterRun)
        {
            uint U32(int o) => BitConverter.ToUInt32(file, o);
            afterRun = -1;
            var allow = new HashSet<uint> { 1, 2, 3, 5, 6, 7, 11, 24, 25 };
            for (int c = execOff; c + 12 <= file.Length && c < execOff + 80 * 12; c += 12)
            {
                if (U32(c) == 15) return null;   // label's RET reached — no SET_MOTION run
                if (U32(c) != 3 || U32(c + 4) != 1 || U32(c + 8) != 200) continue;
                var run = new List<uint[]>();
                for (int c2 = c; c2 + 12 <= file.Length && run.Count < 12; c2 += 12)
                {
                    uint op = U32(c2);
                    run.Add(new[] { op, U32(c2 + 4), U32(c2 + 8) });
                    if (op == 21) { afterRun = c2 + 12; return run.ToArray(); }
                    if (!allow.Contains(op)) return null;
                }
                return null;
            }
            return null;
        }

        /// <summary>The wait-for-motion cluster following the collapse call: arg pushes + the op19 CALL to
        /// this script's own wait subroutine + the op4 result drop. Two native shapes exist — argful
        /// (push soundBase+motionIdx; ADD; push endFrame float; CALL; drop — e03a family) and argless
        /// (CALL; drop — e44a family, mummy-style). Only ops {3,6,19,4} may appear. Null = unrecognized.</summary>
        private static uint[][] ExtractWaitCluster(byte[] file, int off)
        {
            uint U32(int o) => BitConverter.ToUInt32(file, o);
            var cluster = new List<uint[]>();
            bool sawCall = false;
            for (int c = off; c + 12 <= file.Length && cluster.Count < 10; c += 12)
            {
                uint op = U32(c);
                if (op != 3 && op != 6 && op != 19 && op != 4) return null;
                cluster.Add(new[] { op, U32(c + 4), U32(c + 8) });
                if (op == 19) sawCall = true;
                if (op == 4 && sawCall) return cluster.ToArray();
            }
            return null;
        }

        /// <summary>Get-up (motion 10) END frame per model code, from docs/enemy-motion-table.md — used to
        /// retarget argful wait clusters at the get-up animation.</summary>
        private static readonly Dictionary<string, int> GetUpEndFrame = new Dictionary<string, int>
        {
            ["e03a"] = 185, ["e14a"] = 220, ["e15a"] = 220, ["e16a"] = 220, ["e17a"] = 220,
            ["e18a"] = 220, ["e19a"] = 220, ["e20a"] = 220, ["e44a"] = 225, ["e45a"] = 225,
            ["e46a"] = 225, ["e47a"] = 225, ["e48a"] = 225, ["e52a"] = 340, ["e62a"] = 220,
            ["e77a"] = 160, ["e106"] = 190, ["e132"] = 225, ["e134"] = 160,
            ["e138"] = 225, ["e144"] = 225, ["e150"] = 225, ["e161"] = 225,
        };

        /// <summary>Backflip-death species: their real death motion (11) throws the model's root backward
        /// and the get-up slides it forward again — unfixable by repositioning (the slide is in the track).
        /// Instead, when the roll says REVIVE these play their KNOCKDOWN (damage2, motion 9) as the fake
        /// death — exactly what Silver Gear's native revive sub does (SET_MOTION(9), verified) — which ends
        /// lying down in place and pairs with the get-up by design. Value = (motionIdx, endFrame) for the
        /// collapse retarget; damage2 = f170-190 for every member.</summary>
        private static readonly Dictionary<string, (int Motion, int EndFrame)> ReviveCollapseMotion =
            new Dictionary<string, (int, int)>
        {
            ["e14a"] = (9, 190), ["e15a"] = (9, 190), ["e16a"] = (9, 190), ["e17a"] = (9, 190),
            ["e18a"] = (9, 190), ["e19a"] = (9, 190), ["e20a"] = (9, 190),   // Sunday-Saturday
            ["e62a"] = (9, 190),                                             // Hell Pockle
            ["e52a"] = (9, 310),                                             // Curse Dancer (dmg2 f300-310, get-up f320-340)
            ["e44a"] = (9, 200), ["e45a"] = (9, 200), ["e46a"] = (9, 200),   // Heart, Club, Diamond
            ["e47a"] = (9, 200), ["e48a"] = (9, 200),                        // Spade, Joker
            ["e132"] = (9, 200), ["e138"] = (9, 200), ["e144"] = (9, 200),   // card suits (Enhanced)
            ["e150"] = (9, 200), ["e161"] = (9, 200),
        };

        private static int CountOccurrences(byte[] hay, int sigOff, int sigLen)
        {
            int count = 0;
            for (int i = 0; i + sigLen <= hay.Length; i++)
            {
                bool m = true;
                for (int j = 0; j < sigLen && m; j++) m = hay[i + j] == hay[sigOff + j];
                if (m) count++;
            }
            return count;
        }

        /// <summary>Undo every patch (toggle turned off mid-floor). Each write is verified against what we
        /// wrote before restoring, so a reloaded script is never scribbled on.</summary>
        private static void RestoreAll()
        {
            foreach (Patch p in _patches)
            {
                byte[] cur = Memory.ReadBytesBatch(p.Addr, p.Written.Length);
                bool ours = cur != null;
                for (int i = 0; ours && i < p.Written.Length; i++) ours = cur[i] == p.Written[i];
                if (ours) Memory.WriteBytesBatch(p.Addr, p.Original);
            }
            _patches.Clear();
            _appliedThisFloor = false;   // re-apply if toggled back on
        }
    }
}
