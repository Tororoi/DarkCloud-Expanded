using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;
using static Dark_Cloud_Improved_Version.ElfCameraPatches;
using static Dark_Cloud_Improved_Version.ElfWaterPatches;
using static Dark_Cloud_Improved_Version.ElfFishingPatches;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// ELF (SCUS_971.11) patching: the boot cave that registers fishsign.img, ElfPatchAndCrc (program-header
    /// resolve + the ordered Patch* dispatch + new PCSX2 CRC), and the small cave-stub hooks (tide-evict
    /// fade, Queens spray, spray bias). The fishing ELF patches live in ElfFishingPatches; camera and
    /// water-visual patches in ElfCameraPatches / ElfWaterPatches.
    /// </summary>
    internal static class ElfPatches
    {
        internal static byte[] BuildCave()
        {
            uint[] w = {
                Addiu(sp, sp, -0x20), Sw(a0, 0x14, sp), Sw(a1, 0x18, sp),
                Move(a0, a1), Lui(a1, BootCaveStringAddr >> 16), Ori(a1, a1, BootCaveStringAddr & 0xFFFF), Addiu(a2, zero, 0),
                Jal(GetPackFile), 0,
                Lui(t0, BootCaveDiagAddr >> 16), Sw(v0, (int)(BootCaveDiagAddr & 0xFFFF), t0),
                Move(a1, v0), Lui(a0, SysTexMgr >> 16), Ori(a0, a0, SysTexMgr & 0xFFFF),
                Addiu(a2, zero, -1), Addiu(a3, zero, 0), Addiu(t0, zero, 0),
                Jal(EnterIMGFile), 0,
                Lw(a0, 0x14, sp), Lw(a1, 0x18, sp), Addiu(a2, zero, 0),
                Jal(LoadFile), 0,
                Addiu(sp, sp, 0x20), J(REJOIN_VA), 0,
            };
            var b = new byte[w.Length * 4];
            for (int i = 0; i < w.Length; i++) Array.Copy(BitConverter.GetBytes(w[i]), 0, b, i * 4, 4);
            if (b.Length > BootCaveMaxBytes) throw new InvalidOperationException($"cave {b.Length}B > {BootCaveMaxBytes}B");
            return b;
        }

        internal static uint ElfPatchAndCrc(FileStream fs, Rec elf)
        {
            long elfIso = (long)elf.Ext * SectorBytes;
            byte[] eh = Rd(fs, elfIso, 0x34);
            uint phoff = U32(eh, 0x1c); ushort phent = BitConverter.ToUInt16(eh, 0x2a), phnum = BitConverter.ToUInt16(eh, 0x2c);
            long pOff = -1, pVa = -1;
            for (int i = 0; i < phnum; i++)
            {
                byte[] ph = Rd(fs, elfIso + phoff + i * phent, 24);
                uint typ = U32(ph, 0), off = U32(ph, 4), va = U32(ph, 8), fsz = U32(ph, 16);
                if (typ == 1 && fsz > 0 && va <= DETOUR_VA && DETOUR_VA < va + fsz) { pOff = off; pVa = va; break; }
            }
            if (pOff < 0) throw new IOException("No PT_LOAD covers the patch site — wrong ISO/version.");
            long ElfOff(uint va) => elfIso + pOff + (va - pVa);

            byte[] cave = BuildCave();
            if (RdU32(fs, ElfOff(DETOUR_VA)) != Jal(LoadFile) || RdU32(fs, ElfOff(DETOUR_VA + 4)) != 0)
                throw new IOException("Boot-loader patch site is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            byte[] caveWas = Rd(fs, ElfOff(BootCaveAddr), cave.Length);
            foreach (byte x in caveWas) if (x != 0) throw new IOException("Boot-cave region not empty — unexpected ISO.");

            Wr(fs, ElfOff(BootCaveStringAddr), Encoding.ASCII.GetBytes("fishsign.img\0"));
            Wr(fs, ElfOff(BootCaveAddr), cave);
            WrU32(fs, ElfOff(DETOUR_VA), J(BootCaveAddr));

            PatchFishingLoadFish(fs, ElfOff);
            PatchFishBox(fs, ElfOff);

            PatchNativeCameraPostPass(fs, ElfOff);       // native occlusion camera (collision, pull-in, height — all ELF-baked)
            PatchFishingCameraTarget(fs, ElfOff);        // center the fishing shot on the bobber (kept)
            PatchFishingCameraHeight(fs, ElfOff);        // fishing camera height 40 -> per-spot data word (canal wades at 5)
            PatchFishingCameraGather(fs, ElfOff);        // fishing camera-collision gather: mask 1 -> 0xffff (see ALL camera walls while fishing)
            PatchFishingUncastGate(fs, ElfOff);          // invalid-cast auto-uncast: 31-frame delay -> 4, height check gated on a SETTLED bobber
            PatchDrawWaterCompaction(fs, ElfOff);        // frees the cave the water-redraw hook (below) lives in
            PatchWaterRedraw(fs, ElfOff);                 // moves the water draw after the character ONLY while the wading mailbox is armed (order-gate cave; unarmed = vanilla order, fixes the Matataki-falls DOF artifact)
            PatchCapeEarlyDraw(fs, ElfOff);               // AFTER PatchWaterRedraw: EARLY_STUB also draws the cape early (survives falls)
            PatchCanalEvictFadeHook(fs, ElfOff);          // fully-black fade frame → canal tide-evict map-jump (native, flag-gated)
            PatchQueensSprayHook(fs, ElfOff);             // MainDraw effect step → spray emitters at the Queens canal waterfalls (table-driven)
            PatchSprayBiasShim(fs, ElfOff);               // EffectWaterSpray → add a per-emitter velocity bias (mist facing + height)
            PatchFishLineSplit(fs, ElfOff);               // fishing rope: per-segment rest length (distpAbove/distpBelow) split at anchor 18
            PatchStiltsHeal(fs, ElfOff);                  // Brownboo stilts: re-upload scene bank 1 after FishLineDraw, before the waterside redraw (v4; chains the water-redraw jal)
            PatchAllyTextureBudget(fs, ElfOff);           // enlarge the town character buffers so an ally swap's model/textures fit (Ungaga); moves the geometry buffer → ClothUnstick heals the first-Queens-load cloth blowup

            byte[] pelf = Rd(fs, elfIso, (int)elf.Size);
            uint crc = 0;
            for (int i = 0; i < pelf.Length / 4; i++) crc ^= U32(pelf, i * 4);
            return crc;
        }

        // ── Canal tide-evict: hook the fully-black fade frame natively ───────────────────────────────
        // EdFadeInOut sets fade_end=1 (`sw $v1,-0x6df4($gp)` @0x189970) the instant a fade-OUT reaches full
        // black. Retarget that store to our stub in the dead CharaChange region (reclaimable ELF code — a jal
        // there is legal; heap caves crash the recompiler): the stub does the store, then if CanalTide raised
        // the evict flag (mailbox 0x01F10040) it requests the _MAP_JUMP to the East Harbor dock (NextMapNo=19,
        // arrival StartEventNo=404, return code 8) and clears the flag. Frame-perfect — the mod no longer polls
        // the fade; it only sets the flag when the player is caught in the draining canal.
        // (Stub = tools/stubs/canal_evict_fade_hook.s → Resources/isoPatch/canalEvictFadeHook.bin.)
        internal static void PatchCanalEvictFadeHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228BB0;   // dead CharaChangeLoop (reclaimable; jal-legal ELF code)
            const uint HookAddr = 0x00189970;   // EdFadeInOut fade-out `fade_end = 1` store
            if (RdU32(fs, ElfOff(HookAddr)) != 0xAF83920C)
                throw new IOException($"Canal-evict hook site 0x{HookAddr:X} is not vanilla `sw $v1,-0x6df4($gp)` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.canalEvictFadeHook.bin")
                ?? throw new IOException("Embedded EE function missing: canalEvictFadeHook.bin (reassemble tools/stubs/canal_evict_fade_hook.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0xAF83920C)
                throw new IOException($"canalEvictFadeHook.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // store → jal stub; delay slot `clear $s4` runs first (harmless loop init)
        }

        // ── Ally in-place-swap: enlarge the town character buffers ───────────────────────────────────
        // In-place ally swapping (AllySwapPrototype) reloads the town character via _LOAD_MAIN_CHARA. Two of
        // the town's fixed CDataAlloc2 arenas are sized for ONE resident town character and overrun when a
        // LARGER ally is loaded in place — the overrun is `Alloc__14CDataAlloc2` (0x1278a0) spinning forever
        // on `if (cap < used+n) { printf; do{}while(true); }` (THE freeze). EditInit builds the whole town
        // fresh for its target character so it never hits this; our in-place reload can't. The two ceilings,
        // found by live probe (grow one and the freeze moves to the other):
        //   • 0x1d3a080 TEXTURE buffer — SetDataBuffer 40000 blk (625K); ~470K resident (scene+villager+Toan
        //     textures) → ~155K free. A swap's new textures overran it (froze even loading small Ruby).
        //   • 0x1d3a060 main-char GEOMETRY arena — SetDataBuffer 0x1c138=115000 blk (1796K). Ungaga's parsed
        //     model reached ~1525K and its shadow MDS (also loaded here via mds_buffer) pushed past 1796K.
        //
        // Fix: grow the texture buffer 40000 → 100000 blk (625K→1562K, ~1090K free) and the geometry arena
        // 115000 → 160000 blk (1796K→2500K, ~975K headroom over Ungaga), and SHRINK the over-provisioned
        // 0x1d3a050 arena (1216000 blk/19000K, only ~66% used in town) by the COMBINED 105000 blk so the
        // master-pool total carve is UNCHANGED — nothing carved after 0x1d3a050 (the villager buffer, at a
        // fixed DataBuffer offset) moves; the arenas between just follow their allocator base pointers. All
        // three are SetDataBuffer size immediates in EditInit — cold town-init code, safe in-place edits.
        //
        // The 0x1d3a080 size was a single `ori a1,zero,0x9c40`; 100000 (0x186A0) needs 17 bits, so the ori
        // becomes `lui a1,0x1` and the jal's delay-slot nop becomes `ori a1,a1,0x86A0` (the delay slot runs
        // before SetDataBuffer, so a1 is correct at entry). 0x1d3a060 and 0x1d3a050 already use lui+ori pairs
        // (immediates edited in place). Sizes: geometry 0x27100=160000; scene 0x10F2B8=1111000 (1216000−105000).
        internal static void PatchAllyTextureBudget(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint TexSizeVa  = 0x00178534;  // ori a1,zero,0x9c40  (SetDataBuffer(0x1d3a080, 40000) size)
            const uint TexDelayVa = 0x0017853C;  // nop  (that SetDataBuffer jal's delay slot)
            const uint GeoLuiVa   = 0x00178570;  // lui v0,0x1          (SetDataBuffer(0x1d3a060, 0x1c138) size hi)
            const uint GeoOriVa   = 0x00178574;  // ori a1,v0,0xc138    (                                    size lo)
            const uint SceneLuiVa = 0x001785C4;  // lui v0,0x12         (SetDataBuffer(0x1d3a050, 0x128e00) size hi)
            const uint SceneOriVa = 0x001785C8;  // ori a1,v0,0x8e00    (                                    size lo)

            // Validate vanilla — refuse on an already-patched or wrong ISO rather than corrupt the layout.
            if (RdU32(fs, ElfOff(TexSizeVa))  != Ori(a1, zero, 0x9c40) ||
                RdU32(fs, ElfOff(TexDelayVa)) != 0 ||
                RdU32(fs, ElfOff(GeoLuiVa))   != Lui(v0, 0x1) ||
                RdU32(fs, ElfOff(GeoOriVa))   != Ori(a1, v0, 0xc138) ||
                RdU32(fs, ElfOff(SceneLuiVa)) != Lui(v0, 0x12) ||
                RdU32(fs, ElfOff(SceneOriVa)) != Ori(a1, v0, 0x8e00))
                throw new IOException("Ally character-buffer patch sites are not vanilla — unmodified Dark Cloud (USA) ISO expected.");

            // Buffers carve SEQUENTIALLY from the master pool: texture (0x1d3a080) → geometry (0x1d3a060) →
            // scene (0x1d3a050). Growing texture therefore pushes the GEOMETRY buffer's base later — and the
            // town player's CLOTH lives in geometry, so on the FIRST cold load into a far-spawn town (Queens)
            // it lands on uninitialised boot memory and its Verlet solver blows up (the "missing front cape";
            // ClothUnstick heals it via the engine's own _INIT_NPC_CLOTH reset). This grow is REQUIRED for
            // Ungaga's textures to fit, so it stays and the heal covers the cloth (confirmed by a 3-way bisect
            // of these writes, 2026-09-04).
            // 0x1d3a080: 40000 → 100000 (0x186A0), as lui a1,0x1 ; <jal> ; ori a1,a1,0x86A0 (delay slot).
            WrU32(fs, ElfOff(TexSizeVa),  Lui(a1, 0x1));
            WrU32(fs, ElfOff(TexDelayVa), Ori(a1, a1, 0x86A0));
            // 0x1d3a060: 115000 → 160000 (0x27100).
            WrU32(fs, ElfOff(GeoLuiVa),   Lui(v0, 0x2));
            WrU32(fs, ElfOff(GeoOriVa),   Ori(a1, v0, 0x7100));
            // 0x1d3a050: 1216000 → 1111000 (0x10F2B8) — reclaim the combined 105000 blk from its unused slack.
            WrU32(fs, ElfOff(SceneLuiVa), Lui(v0, 0x10));
            WrU32(fs, ElfOff(SceneOriVa), Ori(a1, v0, 0xF2B8));
        }

        // ── Queens waterfall spray hook ──────────────────────────────────────────────────────────────
        // MainDraw @0x17c5a0 is `jal EditEffectStep2` (0x166de0) — the point where the Matataki-spray branch and
        // the non-Matataki path converge, right before DrawEffect. Redirect it to the queensSprayCave (in the dead
        // CharaChange region, after the fade hook), which spawns EffectWaterSpray emitters from CanalTide's table
        // then tail-calls EditEffectStep2. Its delay slot is a nop (nothing displaced), so the redirect is a clean
        // one-word swap. (Stub = tools/stubs/queens_spray_cave.s → Resources/isoPatch/queensSprayCave.bin.)
        internal static void PatchQueensSprayHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228C00;   // dead CharaChange space, past the fade hook (0x228BB0, ~64 B)
            const uint HookAddr = 0x0017C5A0;   // MainDraw `jal EditEffectStep2` (convergence point before DrawEffect)
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059B78)   // = jal 0x00166de0
                throw new IOException($"Queens-spray hook site 0x{HookAddr:X} is not vanilla `jal EditEffectStep2` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.queensSprayCave.bin")
                ?? throw new IOException("Embedded EE function missing: queensSprayCave.bin (reassemble tools/stubs/queens_spray_cave.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x27BDFFE0)   // first insn = addiu $sp,$sp,-0x20
                throw new IOException($"queensSprayCave.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal EditEffectStep2 → jal queensSprayCave (which re-does that call)
        }

        // ── Spray velocity-bias shim ─────────────────────────────────────────────────────────────────
        // EffectWaterSpray @0x165184 ends with `jal EnterEffect` (spawn the just-built particle). Redirect it to
        // the sprayBiasShim, which adds the global bias vec (0x01F18300, set per-emitter by the spray cave) to the
        // particle's initial velocity, then tail-jumps to EnterEffect. The bias is 0 for Matataki's own spray, so
        // this is transparent there. (Stub = tools/stubs/spray_bias_shim.s → Resources/isoPatch/sprayBiasShim.bin.)
        internal static void PatchSprayBiasShim(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228D00;   // dead CharaChange space, past the spray cave (0x228C00, 180 B)
            const uint HookAddr = 0x00165184;   // EffectWaterSpray `jal EnterEffect`
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059260)   // = jal 0x00164980 (EnterEffect)
                throw new IOException($"Spray-bias hook site 0x{HookAddr:X} is not vanilla `jal EnterEffect` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.sprayBiasShim.bin")
                ?? throw new IOException("Embedded EE function missing: sprayBiasShim.bin (reassemble tools/stubs/spray_bias_shim.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x3C0801F2)   // first insn = lui $t0,0x1f2
                throw new IOException($"sprayBiasShim.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal EnterEffect → jal sprayBiasShim (which re-does that call)
        }

    }
}
