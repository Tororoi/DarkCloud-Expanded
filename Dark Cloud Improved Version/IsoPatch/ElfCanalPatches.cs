using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Queens canal's ELF hooks, each an embedded stub in the mod's cave segment taking over one call: the tide-evict
    /// map-jump on the fully-black fade frame (CanalEvict), the waterfall spray emitters (QueensCanalMist) and their per-emitter
    /// velocity bias. Independent of the order-coupled water redraw (ElfWaterPatches). Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfCanalPatches
    {
        // ── Canal tide-evict: hook the fully-black fade frame natively ───────────────────────────────
        // EdFadeInOut sets fade_end=1 (`sw $v1,-0x6df4($gp)` @0x189970) the instant a fade-OUT reaches full
        // black. Retarget that store to our stub in the mod's ELF cave segment (loader-loaded at boot, so a jal
        // there is legal; runtime-written heap caves crash the recompiler): the stub does the store, then if CanalTide raised
        // the evict flag (mailbox 0x01F10040) it requests the _MAP_JUMP to the East Harbor dock (NextMapNo=19,
        // arrival StartEventNo=404, return code 8) and clears the flag. Frame-perfect — the mod no longer polls
        // the fade; it only sets the flag when the player is caught in the draining canal.
        // (Stub = tools/stubs/canal_evict_fade_hook.s → Resources/isoPatch/canalEvictFadeHook.bin.)
        internal static void PatchCanalEvictFadeHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = ElfCave.CanalEvictFadeHook;   // registry: CodeCaveAddresses.ElfCave
            const uint HookAddr = 0x00189970;   // EdFadeInOut fade-out `fade_end = 1` store
            if (RdU32(fs, ElfOff(HookAddr)) != 0xAF83920C)
                throw new IOException($"Canal-evict hook site 0x{HookAddr:X} is not vanilla `sw $v1,-0x6df4($gp)` — unmodified Dark Cloud (USA) ISO expected.");
            byte[] b = Embedded("canalEvictFadeHook.bin", "Embedded EE function missing: canalEvictFadeHook.bin (reassemble tools/stubs/canal_evict_fade_hook.s and rebuild)");
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0xAF83920C)
                throw new IOException($"canalEvictFadeHook.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, StubAddr, b);
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // store → jal stub; delay slot `clear $s4` runs first (harmless loop init)
        }

        // ── Queens waterfall spray hook ──────────────────────────────────────────────────────────────
        // MainDraw @0x17c5a0 is `jal EditEffectStep2` (0x166de0) — the point where the Matataki-spray branch and
        // the non-Matataki path converge, right before DrawEffect. Redirect it to the queensSprayCave (in the mod's
        // ELF cave segment, after the fade hook), which spawns EffectWaterSpray emitters from CanalTide's table
        // then tail-calls EditEffectStep2. Its delay slot is a nop (nothing displaced), so the redirect is a clean
        // one-word swap. (Stub = tools/stubs/queens_spray_cave.s → Resources/isoPatch/queensSprayCave.bin.)
        internal static void PatchQueensSprayHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = ElfCave.QueensSpray;   // registry: CodeCaveAddresses.ElfCave
            const uint HookAddr = 0x0017C5A0;   // MainDraw `jal EditEffectStep2` (convergence point before DrawEffect)
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059B78)   // = jal 0x00166de0
                throw new IOException($"Queens-spray hook site 0x{HookAddr:X} is not vanilla `jal EditEffectStep2` — unmodified Dark Cloud (USA) ISO expected.");
            byte[] b = Embedded("queensSprayCave.bin", "Embedded EE function missing: queensSprayCave.bin (reassemble tools/stubs/queens_spray_cave.s and rebuild)");
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x27BDFFE0)   // first insn = addiu $sp,$sp,-0x20
                throw new IOException($"queensSprayCave.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, StubAddr, b);
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal EditEffectStep2 → jal queensSprayCave (which re-does that call)
        }

        // ── Spray velocity-bias shim ─────────────────────────────────────────────────────────────────
        // EffectWaterSpray @0x165184 ends with `jal EnterEffect` (spawn the just-built particle). Redirect it to
        // the sprayBiasShim, which adds the global bias vec (0x01F18300, set per-emitter by the spray cave) to the
        // particle's initial velocity, then tail-jumps to EnterEffect. The bias is 0 for Matataki's own spray, so
        // this is transparent there. (Stub = tools/stubs/spray_bias_shim.s → Resources/isoPatch/sprayBiasShim.bin.)
        internal static void PatchSprayBiasShim(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = ElfCave.SprayBiasShim;   // registry: CodeCaveAddresses.ElfCave
            const uint HookAddr = 0x00165184;   // EffectWaterSpray `jal EnterEffect`
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059260)   // = jal 0x00164980 (EnterEffect)
                throw new IOException($"Spray-bias hook site 0x{HookAddr:X} is not vanilla `jal EnterEffect` — unmodified Dark Cloud (USA) ISO expected.");
            byte[] b = Embedded("sprayBiasShim.bin", "Embedded EE function missing: sprayBiasShim.bin (reassemble tools/stubs/spray_bias_shim.s and rebuild)");
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x3C0801F2)   // first insn = lui $t0,0x1f2
                throw new IOException($"sprayBiasShim.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, StubAddr, b);
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal EnterEffect → jal sprayBiasShim (which re-does that call)
        }
    }
}
