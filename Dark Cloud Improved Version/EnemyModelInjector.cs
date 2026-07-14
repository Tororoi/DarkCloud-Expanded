using System;
using System.Globalization;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Runtime enemy model + AI re-skin via an injected MIPS code cave.
    ///
    /// Lets you load ANY species into ANY of the 16 live enemy slots after a floor has loaded,
    /// by driving the engine's own routines:
    ///   PRELOAD (mode 1): CMonstorUnit::SetupBaseModel(this, N, tableIndex, 0x26, MonstorModelBuffer)
    ///                     @ ELF 0x001DFE90 — loads the species mesh + 0x9C species record into
    ///                     model block N. Does blocking disc I/O, so run it at a safe moment.
    ///   INSTANTIATE (mode 2): free slot N (RenderStatus = -1), then
    ///                     CMonstorUnit::SetupViewMonstor(this, N, &pos, 0) @ ELF 0x001E02B0 —
    ///                     builds the live monster from model block N into the freed slot. No disc I/O.
    ///
    /// The cave polls a PARAM block each frame from a 1-instruction detour at OpA_MotionProcess
    /// (native 0x01DB6C04). See tools/build_cave.py for the assembler source / regeneration, and
    /// the BtEnemyLayout / EnemySpeciesTable docs in EnemyAddresses.cs for the data model.
    ///
    /// IMPORTANT
    ///  • <see cref="CaveBase"/> must point at a debugger-verified FREE EE RAM region (>= 0x200 bytes).
    ///    The default 0x01F70000 is a placeholder — verify it, then update and call <see cref="Install"/>.
    ///  • The hook lives in the dun overlay, which reloads per floor — call <see cref="Install"/> on
    ///    each floor load.
    ///  • SetupViewMonstor instantiates into the FIRST slot whose RenderStatus(+0)==-1, building it
    ///    from THAT slot's model block. Mode 2 frees only slot N, so it lands in N only if no
    ///    lower-numbered slot is also free. On a fully-spawned floor that is automatic; otherwise
    ///    occupy the lower free slots first, and always preload (mode 1) the SAME N you instantiate.
    ///  • Single-unit enemies only; multi-part/bosses need extra model blocks + a behavior script.
    /// </summary>
    internal static class EnemyModelInjector
    {
        // ── Configuration ────────────────────────────────────────────────────
        /// <summary>
        /// Master switch. Stays FALSE until <see cref="CaveBase"/> has been verified against a known-free
        /// RAM region with the PCSX2 debugger. While false, <see cref="Install"/> is a no-op, so the
        /// (placeholder) cave is never written and game code is never patched. Flip to true only after
        /// verifying the base — otherwise you risk corrupting RAM / crashing during normal play.
        /// </summary>
        internal static bool Enabled = false;

        /// <summary>
        /// Native PS2 base of the cave region (PARAM block is the first 0x20 bytes; code at +0x20).
        ///
        /// CURRENTLY 0 — NO CAVE IS ALLOCATED, and <see cref="Install"/> refuses to run while it is, so a
        /// forgotten <see cref="Enabled"/> = true cannot write to a null base. The old claim (0x01400000)
        /// PREDATED the code-cave scanner and was never swept by it, so it was removed from CodeCaves rather
        /// than rubber-stamped into CodeCaveScanner's reserved list — which would only have made the sweeper
        /// stop telling us the truth about that region. To revive this feature, allocate a SCANNER-VERIFIED
        /// cave in CodeCaveAddresses.cs and point this at it.
        ///
        /// WHY A SCANNER AND NOT AN EYEBALL — the history of bad picks, all of which LOOKED free:
        ///   • 0x01F70000 — inside the active dungeon heap. Garbage, then a crash.
        ///   • 0x0027D090 — looked like static padding across two dumps; the use-item / back-floor menu
        ///     writes there.
        ///   • 0x01400000 — buried in a 3.4 MB contiguous zero block in main BSS (0x01340E20..0x01698CC0),
        ///     zero across three states including the back-floor menu, nothing nonzero within 8 KB before or
        ///     16 KB after. Still never proven over a real session, which is exactly the point: "I found a big
        ///     block of zeroes" is not evidence that the game never writes there.
        /// A region is only free if it stays clean across a long sweep (see CodeCaveScanner) or a write
        /// breakpoint held across every scenario.
        ///
        /// The embedded template is assembled for 0x01F70000 and relocated to this base at runtime.
        /// </summary>
        internal const uint CaveBase = 0;
        // Runtime hook address, in the OpA_MotionProcess EPILOGUE (the single per-frame `jr ra`
        // return path; the top of the function is first-frame init the common path skips). The dun
        // overlay loads WITH its 0x80 file header, so symbol 0x01DB73A0 is at +0x80 = 0x01DB7420
        // (verified against live dungeon eeMemory dumps). Main-segment call targets are unshifted.
        internal const uint HookAddr = 0x01DB7420;   // OpA_MotionProcess epilogue; word reproduced by the cave
        internal const uint OriginalHookWord = 0xC7BA0018; // `lwc1 $f26,0x18($sp)` — restored by Uninstall()

        // PARAM block field offsets (relative to CaveBase)
        private const int P_TRIGGER = 0x00; // write !=0 to fire; cave clears it when done
        private const int P_MODE    = 0x04; // 1 = preload, 2 = instantiate
        private const int P_N       = 0x08; // model-block index / live slot to free
        private const int P_T       = 0x0C; // tableIndex (mode 1)
        private const int P_POS     = 0x10; // float[3] position (mode 2)
        private const int P_HEARTBEAT = 0x1C; // int — incremented by the cave every frame (diagnostic)
        private const int MODE_PRELOAD = 1;
        private const int MODE_INSTANTIATE = 2;

        // ── Cave template (generated by tools/build_cave.py for CaveBase = 0x01F70000) ──
        // Relocated to the configured CaveBase by BuildCave(); regenerate with the script only if
        // you change the cave logic, hook site, or engine addresses.
        private const string CaveTemplateHex =
            "90ffbd270000bfaf0400a1af0800a2af0c00a3af1000a4af1400a5af1800a6af1c00a7af2000a8af2400a9af2800aaaf2c00adaf3000b0af3400b1af3800b2af104800003c00a9af124800004000a9aff701023c000042341c00498c010029251c0049ac" +
            "0000438c300060100000000000000000000040ac04004d8c0800518c0c00528cdf01103cd0871036010001340900a1110000000000000000020001341100a11100000000000000001f000010000000000000000021200002212820022130400226000724" +
            "f001083cd0660835a47f070c00000000000000001300001000000000000000009001033418002302124800000100013cd0e321342148210121480902ffff0a2400002aad2120000221282002f701063c1000c63400000724ac80070c0000000000000000" +
            "4000a98f130020013c00a98f110020010000bf8f0400a18f0800a28f0c00a38f1000a48f1400a58f1800a68f1c00a78f2000a88f2400a98f2800aa8f2c00ad8f3000b08f3400b18f3800b28f7000bd271800bac70add76080000000000000000";

        // Byte offsets within the cave code of the address immediates to relocate.
        // (lui/ori pairs that load the PARAM base and the &pos pointer.)
        private const int OFF_PARAM_HI = 0x50; // lui $v0, hi(PARAM)
        private const int OFF_PARAM_LO = 0x54; // ori $v0, lo(PARAM)
        private const int OFF_POS_HI   = 0x114; // lui $a2, hi(PARAM+0x10)
        private const int OFF_POS_LO   = 0x118; // ori $a2, lo(PARAM+0x10)
        private const uint TEMPLATE_BASE = 0x01F70000; // base the template was assembled for

        // ── Install ──────────────────────────────────────────────────────────
        /// <summary>
        /// Writes the relocated cave and the hook detour. Call once per floor load.
        /// No-op unless <see cref="Enabled"/> is true (so an unverified <see cref="CaveBase"/>
        /// never patches game memory).
        /// </summary>
        internal static void Install()
        {
            if (!Enabled) return;
            // Order matters: write the cave, fully clear PARAM, THEN arm the hook last. Otherwise the
            // hook goes live while PARAM still holds stale/garbage trigger+mode and the cave fires
            // SetupBaseModel/SetupViewMonstor with bad args on the next frame (instant crash).
            // Write the cave with read-back verification + retry FIRST and only arm the hook if the
            // cave fully landed — otherwise the hook would jump into a half-written / zeroed region
            // (a nop-sled through BSS) and crash.
            if (CaveBase == 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[ModelInjector] no cave allocated — see CodeCaveAddresses.cs. Install aborted.");
                return;
            }
            bool caveOk = WriteVerified(CaveBase + 0x20, BuildCave(), "cave");
            Memory.WriteInt(CaveBase + P_TRIGGER, 0);
            Memory.WriteInt(CaveBase + P_MODE, 0);
            Memory.WriteInt(CaveBase + P_HEARTBEAT, 0);
            if (!caveOk)
            {
                Console.WriteLine("[EnemyInjector] cave did not verify. Aborting install.");
                return;
            }
            if (!EnableCodeHook)
            {
                // CONFIRMED: writing the `j cave` patch into the live recompiled code page at HookAddr
                // crashes PCSX2 (PINE cannot safely modify executing EE code — the emulator disconnects).
                // The cave is harmless on its own; we just never arm the code hook. A working trigger
                // needs a DATA-based hook (a function pointer in writable RAM that the engine calls each
                // frame) or driving the game's own script/spawn data — see notes in this file's header.
                Console.WriteLine("[EnemyInjector] cave written; code hook DISABLED (PINE code-patching "
                    + "crashes PCSX2). Injector is inert until a data-based trigger is implemented.");
                return;
            }
            bool hookOk = WriteVerified(HookAddr, BuildHook(), "hook");
            Console.WriteLine($"[EnemyInjector] install {(hookOk ? "OK" : "FAILED")}; "
                + $"cave@0x{CaveBase + 0x20:X8}=0x{Memory.ReadUInt(CaveBase + 0x20):X8} "
                + $"hook@0x{HookAddr:X8}=0x{Memory.ReadUInt(HookAddr):X8}");
        }

        /// <summary>
        /// Arming the cave requires patching live EE code at <see cref="HookAddr"/>, which CRASHES PCSX2
        /// (PINE writes to a recompiled code page take down the emulator — confirmed). Leave false until a
        /// data-based trigger (writable per-frame function pointer / script injection) replaces the code hook.
        /// </summary>
        internal static bool EnableCodeHook = false;

        /// <summary>Writes <paramref name="data"/> and verifies it stuck, retrying mismatched bytes. Logs the outcome.</summary>
        private static bool WriteVerified(long addr, byte[] data, string label, int passes = 4)
        {
            for (int attempt = 0; attempt < passes; attempt++)
            {
                Memory.WriteByteArray(addr, data);
                byte[] back = Memory.ReadByteArray(addr, data.Length);
                int firstBad = -1, badCount = 0;
                for (int i = 0; i < data.Length; i++)
                    if (back[i] != data[i]) { badCount++; if (firstBad < 0) firstBad = i; }
                if (badCount == 0)
                {
                    if (attempt > 0) Console.WriteLine($"[EnemyInjector] {label} verified after {attempt + 1} passes ({data.Length} B).");
                    return true;
                }
                Console.WriteLine($"[EnemyInjector] {label} pass {attempt + 1}: {badCount}/{data.Length} bytes wrong "
                    + $"(first at +0x{firstBad:X}: got 0x{back[firstBad]:X2} want 0x{data[firstBad]:X2}).");
            }
            return false;
        }

        /// <summary>Reads the cave's per-frame heartbeat counter. If it advances, the hook+cave are running.</summary>
        internal static int Heartbeat() => Memory.ReadInt(CaveBase + P_HEARTBEAT);

        /// <summary>Returns the cave code relocated to <see cref="CaveBase"/>.</summary>
        internal static byte[] BuildCave()
        {
            byte[] code = FromHex(CaveTemplateHex);
            uint native = (uint)(CaveBase & Memory.PhysAddrMask); // EE physical address the cave loads internally
            uint paramAddr = native;
            uint posAddr   = native + P_POS;

            // Sanity-check we are patching the right instructions (catches a wrong offset / stale template).
            ExpectImm(code, OFF_PARAM_HI, (ushort)(TEMPLATE_BASE >> 16), "lui $v0 (PARAM hi)");
            ExpectImm(code, OFF_PARAM_LO, (ushort)(TEMPLATE_BASE & 0xFFFF), "ori $v0 (PARAM lo)");
            ExpectImm(code, OFF_POS_HI,   (ushort)((TEMPLATE_BASE + P_POS) >> 16), "lui $a2 (pos hi)");
            ExpectImm(code, OFF_POS_LO,   (ushort)((TEMPLATE_BASE + P_POS) & 0xFFFF), "ori $a2 (pos lo)");

            PatchImm(code, OFF_PARAM_HI, (ushort)(paramAddr >> 16));
            PatchImm(code, OFF_PARAM_LO, (ushort)(paramAddr & 0xFFFF));
            PatchImm(code, OFF_POS_HI,   (ushort)(posAddr >> 16));
            PatchImm(code, OFF_POS_LO,   (ushort)(posAddr & 0xFFFF));
            return code;
        }

        /// <summary>
        /// Restores the original instruction at the hook site, removing the detour. Call when disabling
        /// mid-dungeon (only meaningful while the dun overlay is loaded). Safe to call repeatedly.
        /// </summary>
        internal static void Uninstall()
        {
            Memory.WriteInt(HookAddr, unchecked((int)OriginalHookWord));
        }

        /// <summary>The 4-byte `j cave` detour written at <see cref="HookAddr"/> (delay slot = original next instr).</summary>
        internal static byte[] BuildHook()
        {
            uint codeStart = (uint)(CaveBase & Memory.PhysAddrMask) + 0x20;
            uint jWord = (0x02u << 26) | ((codeStart >> 2) & 0x03FFFFFF);
            return BitConverter.GetBytes(jWord);
        }

        // ── High-level operations ────────────────────────────────────────────
        /// <summary>
        /// PRELOAD: load <paramref name="tableIndex"/>'s mesh + species record into model block
        /// <paramref name="blockIndex"/>. Blocking disc I/O — call at a safe moment (e.g. floor entry).
        /// </summary>
        internal static void PreloadSpecies(int blockIndex, int tableIndex)
        {
            Memory.WriteInt(CaveBase + P_N, blockIndex);
            Memory.WriteInt(CaveBase + P_T, tableIndex);
            Memory.WriteInt(CaveBase + P_MODE, MODE_PRELOAD);
            Memory.WriteInt(CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// INSTANTIATE: free slot <paramref name="slot"/> and build the live monster from model
        /// block <paramref name="slot"/> at the given position. No disc I/O. Preload the same index first.
        /// Position floats are written in the engine's slot Location order (X, Z, Y).
        /// </summary>
        internal static void SpawnIntoSlot(int slot, float x, float z, float y)
        {
            Memory.WriteInt  (CaveBase + P_N, slot);
            Memory.WriteFloat(CaveBase + P_POS + 0, x);
            Memory.WriteFloat(CaveBase + P_POS + 4, z);
            Memory.WriteFloat(CaveBase + P_POS + 8, y);
            Memory.WriteInt  (CaveBase + P_MODE, MODE_INSTANTIATE);
            Memory.WriteInt  (CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// INSTANTIATE using the slot's CURRENT world position (copies the 12-byte Location triple
        /// verbatim, sidestepping any float-order ambiguity). Use after the slot already held a live enemy.
        /// </summary>
        internal static void SpawnIntoSlotAtCurrentPos(int slot)
        {
            long locAddr = EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.LocationX);
            byte[] pos = Memory.ReadByteArray(locAddr, 12); // X(0x100), Z(0x104), Y(0x108)
            Memory.WriteInt(CaveBase + P_N, slot);
            Memory.WriteByteArray(CaveBase + P_POS, pos);
            Memory.WriteInt(CaveBase + P_MODE, MODE_INSTANTIATE);
            Memory.WriteInt(CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// Convenience: full re-skin of an already-spawned slot. Preloads, waits for the load to
        /// complete, then instantiates at the slot's current position. Returns false on timeout.
        /// </summary>
        internal static bool ReskinSlot(int slot, int tableIndex, int loadTimeoutMs = 2000)
        {
            PreloadSpecies(slot, tableIndex);
            if (!WaitTriggerClear(loadTimeoutMs)) return false;
            SpawnIntoSlotAtCurrentPos(slot);
            return WaitTriggerClear(loadTimeoutMs);
        }

        /// <summary>Polls until the cave clears the trigger (i.e. it fired) or the timeout elapses.</summary>
        internal static bool WaitTriggerClear(int timeoutMs)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (Memory.ReadInt(CaveBase + P_TRIGGER) == 0) return true;
                Thread.Sleep(8);
            }
            return false;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static void PatchImm(byte[] code, int off, ushort imm)
        {
            code[off]     = (byte)(imm & 0xFF);        // little-endian low 16 bits of the instruction word
            code[off + 1] = (byte)((imm >> 8) & 0xFF);
        }

        private static void ExpectImm(byte[] code, int off, ushort imm, string what)
        {
            ushort actual = (ushort)(code[off] | (code[off + 1] << 8));
            if (actual != imm)
                throw new InvalidOperationException(
                    $"EnemyModelInjector cave template mismatch at 0x{off:X} ({what}): " +
                    $"expected 0x{imm:X4}, found 0x{actual:X4}. Regenerate via tools/build_cave.py.");
        }

        private static byte[] FromHex(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            return b;
        }
    }
}
