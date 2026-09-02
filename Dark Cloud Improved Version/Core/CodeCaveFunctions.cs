using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// HOSTING ENGINE CODE IN A CAVE — the mechanism, and the rules it must obey.
    ///
    /// Cave ADDRESSES live in <see cref="CodeCaves"/>; this file is how you safely put CODE in one and get
    /// the game to run it. The rules below were each paid for in crashes — read them before adding a caller.
    ///
    /// ── RULE 1: reach a cave by DATA, never by a patched jump ────────────────────────────────────────
    /// The ONLY proven-safe way in is to repoint a FUNCTION-POINTER the game already dereferences (e.g. the
    /// STB external-command dispatch table). That's a pure data write: the game loads our pointer and jalr's
    /// to it, and PCSX2's recompiler compiles the cave as a normal target.
    ///
    /// Patching a direct `j`/`jal` INTO a cave does NOT work. It may appear to run, then crash the moment
    /// the containing block is RE-COMPILED (e.g. some other patch toggles control flow through it). This
    /// killed the first heat-haze attempt: a `jal` at 0x1DAEBCC into a cave ran fine until casting toggled a
    /// nearby gate, forcing a re-compile — and it crashed even with a stub that did nothing but `jr ra`.
    /// If a function has no pointer table to repoint, you cannot host it. Find a data path or give up.
    ///
    /// ── RULE 2: arm only in the COLD window ──────────────────────────────────────────────────────────
    /// Writing code that is currently executing/compiled crashes the recompiler. Arm from the cold window
    /// (in-game entry, before the target has run) — the same window the ABS patches use.
    ///
    /// ── RULE 3: verify pristine vanilla first ────────────────────────────────────────────────────────
    /// If the target function is not byte-for-byte vanilla, a stale in-place patch is still live from an
    /// earlier session and copying it would host corrupt code. Abort and tell the user to restart the game.
    ///
    /// ── RULE 4: only self-contained functions ────────────────────────────────────────────────────────
    /// The copy is relocated to a new address, so the body must survive that: absolute `jal`s are fine (they
    /// encode a target, not an offset) and PC-relative branches are fine (they move with the code). What is
    /// NOT fine is anything PC-relative to its ORIGINAL address. Check the disassembly before copying.
    ///
    /// See docs/cave-code-execution.md.
    /// </summary>
    internal static class CodeCaveFunctions
    {
        internal const uint Nop = 0x00000000;

        /// <summary>MIPS absolute jump: `j target` (target is a GUEST address).</summary>
        internal static uint J(uint targetGuest) => 0x08000000u | ((targetGuest >> 2) & 0x03FFFFFF);

        /// <summary>MIPS absolute call: `jal target` (target is a GUEST address).</summary>
        internal static uint Jal(uint targetGuest) => 0x0C000000u | ((targetGuest >> 2) & 0x03FFFFFF);

        /// <summary>Pack MIPS words into the little-endian byte image PINE writes.</summary>
        internal static byte[] Assemble(params uint[] words)
        {
            var b = new byte[words.Length * 4];
            for (int i = 0; i < words.Length; i++) BitConverter.GetBytes(words[i]).CopyTo(b, i * 4);
            return b;
        }

        /// <summary>
        /// Host a CLEAN copy of a vanilla function in a cave, patch detours into the copy, append a helper
        /// after it, and repoint a dispatch slot at the copy (RULE 1 — the game's own indirect call takes us
        /// there; no in-place surgery on the original).
        ///
        /// Idempotent: if the dispatch slot already points at the cave, this is a no-op returning true — so a
        /// caller can retry every tick until the cold window arrives.
        /// </summary>
        /// <param name="name">For logging.</param>
        /// <param name="vanillaFn">MMU address of the engine function to copy.</param>
        /// <param name="fnSize">Bytes to copy — must cover the whole function.</param>
        /// <param name="cave">MMU address to host it at.</param>
        /// <param name="caveGuest">Same cave as a GUEST address (what we write into the pointer table).</param>
        /// <param name="dispatchSlot">MMU address of the function-POINTER the game dereferences.</param>
        /// <param name="pristine">(offset, expectedWord) pairs asserted against the ORIGINAL before copying (RULE 3).</param>
        /// <param name="detours">(offset, code) written INTO the copy — e.g. `j helper / nop` over a hardcoded load.</param>
        /// <param name="helperOff">Offset within the cave for <paramref name="helper"/> — past the copied body.</param>
        /// <param name="helper">Caller-supplied code (the payload); it must jump back into the copy itself.</param>
        internal static bool ArmDispatchCave(
            string name,
            long vanillaFn, int fnSize,
            long cave, uint caveGuest,
            long dispatchSlot,
            (int Off, uint Word)[] pristine,
            (int Off, uint[] Code)[] detours,
            int helperOff, uint[] helper)
        {
            uint vanillaGuest = (uint)vanillaFn & Memory.PhysAddrMask;
            uint slot = (uint)Memory.ReadInt(dispatchSlot);
            if (slot == caveGuest) return true;                       // already armed (safe to retry every tick)
            if (slot != vanillaGuest)
            {
                Console.WriteLine($"[cave] {name}: dispatch = 0x{slot:X} (expected vanilla 0x{vanillaGuest:X}) — abort");
                return false;
            }

            // RULE 3 — refuse to clone a function someone already patched in place.
            foreach (var (off, word) in pristine)
                if ((uint)Memory.ReadInt(vanillaFn + off) != word)
                {
                    Console.WriteLine($"[cave] {name}: +0x{off:X} is 0x{(uint)Memory.ReadInt(vanillaFn + off):X}, expected 0x{word:X} — " +
                                       "not pristine vanilla (stale in-place patch? restart the game) — abort");
                    return false;
                }

            byte[] fn = Memory.ReadBytesBatch(vanillaFn, fnSize);
            if (fn == null) { Console.WriteLine($"[cave] {name}: could not read the function — abort"); return false; }
            if (helperOff < fnSize) { Console.WriteLine($"[cave] {name}: helperOff 0x{helperOff:X} overlaps the {fnSize:X}-byte body — abort"); return false; }

            foreach (var (off, code) in detours)
                Assemble(code).CopyTo(fn, off);

            Memory.WriteBytesBatch(cave, fn);                        // the relocated body (RULE 4)
            if (helper is { Length: > 0 })
                Memory.WriteBytesBatch(cave + helperOff, Assemble(helper));
            Memory.WriteUInt(dispatchSlot, caveGuest);               // RULE 1 — the DATA hand-off

            uint back = (uint)Memory.ReadInt(dispatchSlot);
            Console.WriteLine($"[cave] {name} → clean cave @0x{caveGuest:X} (dispatch readback 0x{back:X})");
            return back == caveGuest;
        }
    }
}
