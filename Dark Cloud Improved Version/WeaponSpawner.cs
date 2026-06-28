using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY reach test: extends <b>Heaven's Cloud</b> (item 271) to ~3x reach by scaling its
    /// loaded weapon model's <c>dcol*</c> damage-collision frame translations in RAM.
    ///
    /// The equipped-model pointer (<see cref="WeaponCollision.EquippedModelPtr"/>) is the CHARACTER
    /// model root; the <c>dcol*</c> frames live in the weapon subtree attached under a hand bone, so
    /// we WALK the CFrame tree (child <see cref="WeaponCollision.CFrameChild"/> / next
    /// <see cref="WeaponCollision.CFrameNext"/>, name <see cref="WeaponCollision.CFrameName"/>) to
    /// find them, then multiply each frame's local-matrix translation (+0x180/+0x184/+0x188) by
    /// <see cref="Multiplier"/>. Scaled once per loaded model instance;
    /// <see cref="OnFloorEntered"/> re-arms it per floor. Remove when reach work is finalized.
    /// </summary>
    public static class WeaponSpawner
    {
        const int   HeavensCloudId = 271;
        const int   MaxNodes       = 8192; // tree-walk safety cap

        static bool   _started;
        static int    _scaledModel; // native ptr of the model we last scaled (0 = re-arm)
        static string _lastState = "";

        public static void StartReachExtender()
        {
            if (_started) return;
            _started = true;
            new Thread(Loop) { IsBackground = true }.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "WeaponSpawner: Heaven's Cloud reach extender started (tree walk)");
        }

        /// <summary>Floor-entry hook. No longer forces a rescan: the commenu weapon template is
        /// session-persistent, so the cached bones stay valid; ReverifyHcDcol self-heals if not.</summary>
        public static void OnFloorEntered() { _scaledModel = 0; }

        /// <summary>Live world positions of the cached HC dcol frames for the Sandbox reach debug canvas.
        /// Reads each frame's WORLD-matrix translation (frame+0x180 = name+0x68; convention X, height, Y).
        /// Empty until armed (HC equipped in a dungeon).</summary>
        public static System.Collections.Generic.List<(float x, float y, float height, float localZ)> GetDcolWorldPoints()
        {
            var list = new System.Collections.Generic.List<(float, float, float, float)>();
            foreach (var kv in _hcBones)
            {
                if ((uint)Memory.ReadInt(kv.Key) != 0x6C6F6364) continue; // still a "dcol" frame
                float x = Memory.ReadFloat(kv.Key + 0x68);   // world X  (frame+0x180)
                float h = Memory.ReadFloat(kv.Key + 0x6C);   // world height (frame+0x184)
                float y = Memory.ReadFloat(kv.Key + 0x70);   // world Y  (frame+0x188)
                list.Add((x, y, h, kv.Value));
            }
            return list;
        }

        /// <summary>Current (possibly boosted) primary swing hit radius (gp-0x7b88) for display sizing.</summary>
        public static float CurrentSwingRadius() => Memory.ReadFloat(RadiusAddrs[0]);

        static void Loop()
        {
            while (true)
            {
                try { Tick(); }
                catch (Exception ex)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "WeaponSpawner ERROR: " + ex.Message);
                }
                Thread.Sleep(400);
            }
        }

        static void Tick()
        {
            int chr = Player.CurrentCharacterNum();
            ushort wid = Player.Weapon.GetCurrentWeaponId();
            int rootPtr = Memory.ReadInt(WeaponCollision.EquippedModelPtr);

            string state = $"char={chr} wid={wid}({Item.GetName(wid)}) rootCFrame=0x{rootPtr:X8}";
            if (state != _lastState)
            {
                _lastState = state;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[REACH] " + state);
            }

            // RUNTIME reach: reach = the loaded model's dcol0..3 BONES (matched by their known base Z),
            // NOT the hand-attached "dcol" CFrame frame (that one stays at base even when reach changes).
            // We patch those bones' Z ×FACTOR in RAM, mirroring the proven offline .chr/.mds edit.
            bool hc = chr == Player.ToanId && wid == HeavensCloudId;

            // Radius: boost while HC equipped (fills the gaps from position-scaling), restore otherwise.
            ApplyReachRadius(hc);

            if (hc && rootPtr != 0)
            {
                // Scan ONCE per session (the commenu weapon template is boot-persistent), then just
                // re-verify the cached addresses cheaply each poll. No per-floor rescan => no lag.
                if (_armed) ReverifyHcDcol();
                else if (_scanBackoff <= 0) ScanAndPatchHcDcol();
                else _scanBackoff--;
            }
        }

        // Boost/restore the three (HC-gated) swing hit-sphere radii. Stock is cached once (sanity-checked
        // against ~2.8 so we never cache an already-boosted value or a wrong $gp).
        static void ApplyReachRadius(bool hc)
        {
            if (!_radiusStockRead)
            {
                for (int i = 0; i < 3; i++) _radiusStock[i] = Memory.ReadFloat(RadiusAddrs[i]);
                if (Math.Abs(_radiusStock[0] - 2.8f) > 0.3f) return; // not ready / wrong addr → retry next poll
                _radiusStockRead = true;
            }
            if (hc && !_radiusBoosted)
            {
                for (int i = 0; i < 3; i++) Memory.WriteFloat(RadiusAddrs[i], _radiusStock[i] * RadiusFactor);
                _radiusBoosted = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[REACH] swing radius ×{RadiusFactor} (HC equipped)");
            }
            else if (!hc && _radiusBoosted)
            {
                for (int i = 0; i < 3; i++) Memory.WriteFloat(RadiusAddrs[i], _radiusStock[i]);
                _radiusBoosted = false;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[REACH] swing radius restored (HC unequipped)");
            }
        }

        const float ReachFactor = 2.5f;   // dcol bone position scale (forward reach / tip = maxBase×this)
        const float NearAnchor  = 1.0f;   // where the nearest dcol bone goes (pulled in from stock 1.49
                                          // so the first combo hit's small 1.2 sphere reaches point-blank)
        const byte  OnlyDcolDigit = 0x00; // patch all dcol0..3 (0), or one digit e.g. 0x31 for dcol1

        // Hit-sphere radius constants used by Toan's swing handler (ToanKey_Play), all EXCLUSIVE to it
        // (verified: no other readers) → safe to bump while HC is equipped to fill the gaps that
        // position-scaling opens. MMU = native|0x20000000; native = main $gp(0x002A97F0) − offset.
        static readonly long[]  RadiusAddrs = { 0x202A1C68, 0x202A1C6C, 0x202A1C70 }; // gp-0x7b88/-7b84/-7b80
        static readonly float[] _radiusStock = new float[3];                          // stock 2.8 / 5.3 / 6.2
        const float RadiusFactor = 1.0f; // ISOLATION TEST: stock radius (no bump) + scaled positions —
                                         // if the FIRST hit now connects at point-blank, the big radius was the cause.
        static bool _radiusStockRead, _radiusBoosted;
        // HC dcol bone base Z values from data.dat (both the commenu .chr and the dun .mds sets).
        static readonly float[] HcBaseZ = { 1.4916f, 3.6456f, 5.9312f, 9.2053f, 12.5114f, 10.6053f };
        static bool _armed;
        static int  _scanBackoff;   // polls to wait before retrying a scan that found nothing
        // Located HC dcol frames: name MMU address (holds "dcolN") -> the base Z we scaled from.
        static readonly Dictionary<long, float> _hcBones = new();

        static bool IsHcBaseZ(float z)
        {
            foreach (float b in HcBaseZ) if (Math.Abs(z - b) < 0.01f) return true;
            return false;
        }

        // dcol CFrameVu1 frame: name at base; translation row at name+0xE8/+0xEC/+0xF0 (x/y/z).
        const int DcolNameToTransX = 0xE8;
        const int DcolNameToTransZ = 0xF0;

        /// <summary>Scan RAM for HC's dcol0..3 frames (translation (0,0,Z) at name+0xE8, Z∈HC base set),
        /// then ANCHORED-STRETCH their Z: the nearest bone stays put (so the slash trail still starts at
        /// the hilt) and the rest stretch outward so the farthest reaches maxBase×ReachFactor.
        /// target = min + (baseZ − min) × spread, spread = (max×ReachFactor − min)/(max − min).</summary>
        static void ScanAndPatchHcDcol()
        {
            _hcBones.Clear();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[REACH] scanning for HC dcol frames (×{ReachFactor}, near anchored)...");
            var addrs = new List<long>();
            var bases = new List<float>();
            float min = float.MaxValue, max = float.MinValue;
            for (long a = ScanLoMmu; a < ScanHiMmu; a += (long)ScanChunkWords * 4)
            {
                int n = ScanChunkWords + 2;
                if (a + (long)n * 4 > ScanHiMmu) n = (int)((ScanHiMmu - a) / 4);
                if (n <= 2) break;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 1 < n; i++)
                {
                    if (w[i] != 0x6C6F6364) continue;             // "dcol"
                    byte d = (byte)(w[i + 1] & 0xFF), nul = (byte)((w[i + 1] >> 8) & 0xFF);
                    if (d < 0x30 || d > 0x33 || nul != 0) continue; // dcol0..3, NUL-terminated
                    if (OnlyDcolDigit != 0 && d != OnlyDcolDigit) continue; // test: restrict to one dcol
                    long nameAddr = a + (long)i * 4;
                    float x = Memory.ReadFloat(nameAddr + DcolNameToTransX);
                    float y = Memory.ReadFloat(nameAddr + DcolNameToTransX + 4);
                    float z = Memory.ReadFloat(nameAddr + DcolNameToTransZ);
                    if (Math.Abs(x) > 0.01f || Math.Abs(y) > 0.01f) continue; // weapon dcol frames are (0,0,Z)
                    if (!IsHcBaseZ(z)) continue;                  // only HC's bones
                    addrs.Add(nameAddr); bases.Add(z);
                    if (z < min) min = z;
                    if (z > max) max = z;
                }
            }
            if (addrs.Count == 0) { _scanBackoff = 8; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[REACH] no HC dcol frames yet; will retry."); return; }

            // Map [min..max] -> [NearAnchor..tip]: the nearest bone goes to NearAnchor (pulled slightly
            // inward from stock 1.49 so the first combo hit's small 1.2 sphere reaches point-blank),
            // the rest stretch out so the farthest reaches max×ReachFactor.
            float tip = max * ReachFactor;
            for (int k = 0; k < addrs.Count; k++)
            {
                float target = (max > min) ? NearAnchor + (bases[k] - min) / (max - min) * (tip - NearAnchor) : NearAnchor;
                _hcBones[addrs[k]] = target;
                Memory.WriteFloat(addrs[k] + DcolNameToTransZ, target);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[REACH]   dcol @native 0x{addrs[k] & 0x1FFFFFFF:X8} z {bases[k]:0.###} -> {target:0.###}");
            }
            _armed = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[REACH] patched {addrs.Count} HC dcol frames (near {min:0.##}->{NearAnchor:0.##}, tip {max:0.##}->{tip:0.##}).");
        }

        // Cheap per-poll upkeep: rewrite any reverted bone. If a cached frame's "dcol" name is gone,
        // the model relocated => drop the cache and rescan (self-healing, so no per-floor full scan).
        static void ReverifyHcDcol()
        {
            foreach (var kv in _hcBones)
            {
                if ((uint)Memory.ReadInt(kv.Key) != 0x6C6F6364) { _armed = false; _hcBones.Clear(); return; }
                long zAddr = kv.Key + DcolNameToTransZ;
                if (Math.Abs(Memory.ReadFloat(zAddr) - kv.Value) > 0.05f) // kv.Value is the final target Z
                    Memory.WriteFloat(zAddr, kv.Value);
            }
        }

        /// <summary>Walks the equipped-model CFrame tree and logs every dcol* frame's world position
        /// (+0x180/4/8) and local translation (+0x200/4/8). Read-only.</summary>
        static void DumpAllDcols(int rootNative)
        {
            long root = Memory.ToMmu(rootNative);
            var stack = new Stack<int>();
            var seen = new HashSet<int>();
            int fc = Memory.ReadInt(root + WeaponCollision.CFrameChild);
            if (fc != 0) stack.Push(fc);
            int visited = 0, found = 0;
            while (stack.Count > 0 && visited < MaxNodes)
            {
                int node = stack.Pop();
                if (node == 0 || !seen.Add(node)) continue;
                visited++;
                long b = Memory.ToMmu(node);
                string name = Memory.ReadString(b + WeaponCollision.CFrameName, 16);
                int t = name.IndexOf('\0'); if (t >= 0) name = name.Substring(0, t);
                if (name.StartsWith("dcol", StringComparison.Ordinal))
                {
                    found++;
                    float wx = Memory.ReadFloat(b + 0x180), wy = Memory.ReadFloat(b + 0x184), wz = Memory.ReadFloat(b + 0x188);
                    float lx = Memory.ReadFloat(b + 0x200), ly = Memory.ReadFloat(b + 0x204), lz = Memory.ReadFloat(b + 0x208);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[REACH] {name} @0x{b:X8} local=({lx:0.###},{ly:0.###},{lz:0.###}) world=({wx:0.##},{wy:0.##},{wz:0.##})");
                }
                int c = Memory.ReadInt(b + WeaponCollision.CFrameChild);
                int n = Memory.ReadInt(b + WeaponCollision.CFrameNext);
                if (c != 0) stack.Push(c);
                if (n != 0) stack.Push(n);
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[REACH] dumped {found} dcol frames from {visited} nodes. local Z ~12+ => commenu .chr ×3 loaded; " +
                "~30+ => dcol1; base (~4) => edit NOT loaded / wrong file.");
        }

        static readonly List<long> _candidates = new(); // MMU addrs of source transRows (x at +0)

        // x tolerant to a few mantissa ULP (runtime picks up FP error vs the source); z must be exact.
        static bool XMatches(uint x) => (x >> 4) == (DcolTransX >> 4);

        static void MaintainAndProbe()
        {
            foreach (long t in _candidates)
            {
                float z = Memory.ReadFloat(t + 8);
                if (z < 8.0f) // reverted to base ~4 (or never scaled) -> (re)scale x and z ×3
                {
                    Memory.WriteFloat(t,     Memory.ReadFloat(t)     * 3.0f);
                    Memory.WriteFloat(t + 8, Memory.ReadFloat(t + 8) * 3.0f);
                }
            }
            if (_dcolMmu != 0)
            {
                float rz = Memory.ReadFloat(_dcolMmu + WeaponCollision.CFrameLocalTransZ);
                string s = $"[REACH] PROBE runtime dcol +0x208 z={rz:0.###} (base 4.0 => no propagation; ~12 => SOURCE FEEDS POSE)";
                if (s != _lastProbe) { _lastProbe = s; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + s); }
            }
        }
        static string _lastProbe = "";

        static bool _loggedNoDcol, _dumpedFields, _scanned;
        static long _dcolMmu; // live MMU address of the dcol CFrame, captured during tree walk

        // dcol local-matrix translation, observed identical across dumps (the base the pose restores):
        //   x = +0x200 = 2.6789 = 0x402B7379 ;  z = +0x208 = 4.0047 = 0x408026AE  (y ~ 0)
        const uint DcolTransX = 0x402B7379;
        const uint DcolTransZ = 0x408026AE;

        // EE main RAM, MMU-mapped (native | 0x20000000). Skip the first 1 MB (kernel/scratch).
        const long ScanLoMmu = 0x20100000;
        const long ScanHiMmu = 0x22000000;
        const int  ScanChunkWords = 2048; // 8 KB per batched round-trip (conservative vs PINE limits)

        /// <summary>One-shot RAM scan for the matrix-translation pattern (x at +0, z at +8) that holds
        /// the dcol base offset. Logs every match (native addr) so we can tell whether a static source
        /// copy exists (geometry path is viable) or only the runtime CFrame holds it (pose recomputes).</summary>
        static void ScanForSourceMatrix()
        {
            // ── SELF-TEST: prove the batched reader returns the SAME bytes as single reads at the
            //    known dcol address. If these disagree, the scan tooling is broken (not the theory). ──
            if (_dcolMmu != 0)
            {
                long xAddr = _dcolMmu + WeaponCollision.CFrameLocalTransX; // +0x200
                uint sX = (uint)Memory.ReadInt(xAddr);
                uint sY = (uint)Memory.ReadInt(xAddr + 4);
                uint sZ = (uint)Memory.ReadInt(xAddr + 8);
                uint[] bw = Memory.ReadUIntBatch(xAddr, 3);
                bool ok = bw.Length == 3 && bw[0] == sX && bw[1] == sY && bw[2] == sZ;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[REACH] SELFTEST dcol@0x{_dcolMmu:X8} +0x200 single=({sX:X8},{sY:X8},{sZ:X8}) " +
                    $"batch=({bw[0]:X8},{bw[1]:X8},{bw[2]:X8}) => {(ok ? "MATCH (reader OK)" : "MISMATCH (batch reader BUG)")}");
            }

            long knownTransMmu = _dcolMmu != 0 ? _dcolMmu + WeaponCollision.CFrameLocalTransX : 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[REACH] SCAN start: hunting x=0x{DcolTransX:X8} & +8 z=0x{DcolTransZ:X8} in 0x{ScanLoMmu:X8}..0x{ScanHiMmu:X8} (chunk {ScanChunkWords}w)");
            int hits = 0; bool foundKnown = false;
            for (long a = ScanLoMmu; a < ScanHiMmu; a += (long)ScanChunkWords * 4)
            {
                // +2 overlap so an x@end / z@end+8 pattern isn't split across chunk boundaries.
                int n = ScanChunkWords + 2;
                if (a + (long)n * 4 > ScanHiMmu) n = (int)((ScanHiMmu - a) / 4);
                if (n <= 2) break;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 2 < n; i++)
                {
                    // z exact + x tolerant (runtime/source differ by a couple mantissa ULP).
                    if (w[i + 2] == DcolTransZ && XMatches(w[i]))
                    {
                        long mmu = a + (long)i * 4;
                        hits++;
                        if (mmu == knownTransMmu) { foundKnown = true; continue; } // skip runtime CFrame
                        IdentifyCandidate(mmu);
                    }
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[REACH] SCAN done: {hits} matches ({(foundKnown ? "incl." : "MISSING")} known runtime CFrame), " +
                $"{_candidates.Count} confirmed dcol source(s) registered for scaling. " +
                (!foundKnown ? "Known dcol NOT found => scan broken, ignore."
                 : _candidates.Count == 0 ? "No confirmed source => pose recomputes; fall back to radius."
                                          : "Scaling source(s); watch PROBE for propagation."));
        }

        // Confirms whether the structure around a candidate transRow is a dcol node (looks for the
        // ASCII name "dcol" in the 0x220 bytes before it — covers MDS node name@-0x58 and CFrame-like
        // name@-0xE8). Registers it for scaling only if confirmed, so we never blind-write garbage.
        static void IdentifyCandidate(long transMmu)
        {
            long winStart = transMmu - 0x220;
            int words = (0x220 + 0x10) / 4;
            uint[] w = Memory.ReadUIntBatch(winStart, words);
            int nameRel = -1;
            for (int i = 0; i < w.Length; i++)
                if (w[i] == 0x6C6F6364) { nameRel = i * 4 - 0x220; break; } // "dcol" little-endian
            float x = Memory.ReadFloat(transMmu), y = Memory.ReadFloat(transMmu + 4), z = Memory.ReadFloat(transMmu + 8);
            bool confirmed = nameRel != -1;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[REACH] CANDIDATE transRow native=0x{transMmu & 0x1FFFFFFF:X8} trans=({x:0.###},{y:0.###},{z:0.###}) " +
                (confirmed ? $"'dcol' name @trans{(nameRel >= 0 ? "+" : "")}0x{nameRel:X} => CONFIRMED"
                           : "no 'dcol' name nearby (likely flat matrix-palette entry)"));

            // Dump the 4x3 matrix (rows at trans-0x30/-0x20/-0x10/trans) to help identify the struct.
            for (int row = 0; row < 4; row++)
            {
                long r = transMmu - 0x30 + row * 0x10;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[REACH]   mat row{row} @0x{r:X8}: ({Memory.ReadFloat(r):0.###}, {Memory.ReadFloat(r + 4):0.###}, " +
                    $"{Memory.ReadFloat(r + 8):0.###}, {Memory.ReadFloat(r + 12):0.###})");
            }

            // Scale it regardless of name (decisive test): the PROBE tells us if it feeds the pose.
            _candidates.Add(transMmu);
        }

        // DIAGNOSTIC: dump a CFrame's fields +0x150..+0x250 as float+hex to locate the bind/base
        // matrix the per-frame pose copies into +0x1d0 (look for a 2nd copy of (2.68,0,4) / 4.0).
        static void DumpFields(long b, string name)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"  FIELD DUMP {name} @0x{b:X8}:");
            for (int off = 0x00; off < 0x250; off += 4)
            {
                uint raw = (uint)Memory.ReadInt(b + off);
                float f = Memory.ReadFloat(b + off);
                // Flag words that look like a pointer into PS2 main RAM (native or already-MMU) — a
                // back-pointer to the source MDS node would land here.
                bool ptrish = (raw >= 0x00100000 && raw < 0x02000000) ||
                              (raw >= 0x20100000 && raw < 0x22000000);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"    +0x{off:X3}  0x{raw:X8}  {f:0.####}" + (ptrish ? "  <ptr?>" : ""));
            }
        }

        /// <summary>Walks the CFrame tree from the character root, scaling every dcol* frame found.</summary>
        static int ScaleDcolFrames(int rootNative)
        {
            long rootBase = Memory.ToMmu(rootNative);
            int firstChild = Memory.ReadInt(rootBase + WeaponCollision.CFrameChild);

            var stack = new Stack<int>();
            var seen  = new HashSet<int>();
            if (firstChild != 0) stack.Push(firstChild);

            int scaled = 0, visited = 0;
            var names = new List<string>();

            while (stack.Count > 0 && visited < MaxNodes)
            {
                int node = stack.Pop();
                if (node == 0 || !seen.Add(node)) continue;
                visited++;

                long b = Memory.ToMmu(node);
                string name = Memory.ReadString(b + WeaponCollision.CFrameName, 16);
                int term = name.IndexOf('\0');
                if (term >= 0) name = name.Substring(0, term);
                if (names.Count < 60 && name.Length > 0) names.Add(name);

                if (name.StartsWith("dcol", StringComparison.Ordinal))
                {
                    _dcolMmu = b; // live address of the dcol CFrame (for the scanner self-test)
                    // Runtime CFrame edit is CONFIRMED to revert every frame (live write test
                    // 2026-06-28: rescale counter climbed without bound). Source is upstream (MDS).
                    // One-shot: dump the FULL object to hunt for a back-pointer to the source node.
                    if (!_dumpedFields) { _dumpedFields = true; DumpFields(b, name); }
                    scaled++;
                }

                int child = Memory.ReadInt(b + WeaponCollision.CFrameChild);
                int next  = Memory.ReadInt(b + WeaponCollision.CFrameNext);
                if (child != 0) stack.Push(child);
                if (next  != 0) stack.Push(next);
            }

            if (scaled == 0 && !_loggedNoDcol)
            {
                _loggedNoDcol = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"WeaponSpawner: no dcol in {visited} frames. names: " + string.Join(",", names));
            }
            return scaled;
        }

        // DIAGNOSTIC: dumps a frame and its whole subtree (child/next), logging each frame's
        // local-matrix scale diagonal + translation and world translation, so we can identify the
        // weapon's blade / mesh frames to scale for "C".
        static void DumpSubtree(int rootNative)
        {
            DumpFrame(rootNative, 0);
            var st = new Stack<(int node, int depth)>();
            var seen = new HashSet<int>();
            int firstChild = Memory.ReadInt(Memory.ToMmu(rootNative) + WeaponCollision.CFrameChild);
            if (firstChild != 0) st.Push((firstChild, 1));

            int n = 0;
            while (st.Count > 0 && n < 150)
            {
                var (node, depth) = st.Pop();
                if (node == 0 || !seen.Add(node)) continue;
                n++;
                DumpFrame(node, depth);
                long b = Memory.ToMmu(node);
                int child = Memory.ReadInt(b + WeaponCollision.CFrameChild);
                int next  = Memory.ReadInt(b + WeaponCollision.CFrameNext);
                if (child != 0) st.Push((child, depth + 1));
                if (next  != 0) st.Push((next, depth));
            }
        }

        static void DumpFrame(int node, int depth)
        {
            long b = Memory.ToMmu(node);
            string nm = Memory.ReadString(b + WeaponCollision.CFrameName, 16);
            int t = nm.IndexOf('\0'); if (t >= 0) nm = nm.Substring(0, t);

            long lm = b + WeaponCollision.CFrameLocalMatrix; // +0x1d0
            float lx = Memory.ReadFloat(lm + 0x30), ly = Memory.ReadFloat(lm + 0x34), lz = Memory.ReadFloat(lm + 0x38);
            float s0 = Memory.ReadFloat(lm + 0x00), s1 = Memory.ReadFloat(lm + 0x14), s2 = Memory.ReadFloat(lm + 0x28);
            long wm = b + WeaponCollision.CFrameWorldMatrix; // +0x150
            float wx = Memory.ReadFloat(wm + 0x30), wy = Memory.ReadFloat(wm + 0x34), wz = Memory.ReadFloat(wm + 0x38);

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"    {new string(' ', depth * 2)}{nm} @0x{b:X8} " +
                $"scaleDiag=({s0:0.##},{s1:0.##},{s2:0.##}) localT=({lx:0.##},{ly:0.##},{lz:0.##}) " +
                $"worldT=({wx:0.##},{wy:0.##},{wz:0.##})");
        }
    }
}
