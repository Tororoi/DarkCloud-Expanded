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

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// ELF (SCUS_971.11) patching: the boot cave that registers fishsign.img, ElfPatchAndCrc (program-header
    /// resolve + the ordered Patch* dispatch + new PCSX2 CRC), the small cave-stub hooks (tide-evict fade,
    /// Queens spray, spray bias, fish-line split, uncast gate) and the fishing data rewrites (LoadFish pools,
    /// fish collision box). Camera and water-visual patches live in ElfCameraPatches / ElfWaterPatches.
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
            PatchWaterRedraw(fs, ElfOff);                 // moves (not duplicates) the water draw to after the character
            PatchCapeEarlyDraw(fs, ElfOff);               // AFTER PatchWaterRedraw: EARLY_STUB also draws the cape early (survives falls)
            PatchCanalEvictFadeHook(fs, ElfOff);          // fully-black fade frame → canal tide-evict map-jump (native, flag-gated)
            PatchQueensSprayHook(fs, ElfOff);             // MainDraw effect step → spray emitters at the Queens canal waterfalls (table-driven)
            PatchSprayBiasShim(fs, ElfOff);               // EffectWaterSpray → add a per-emitter velocity bias (mist facing + height)
            PatchFishLineSplit(fs, ElfOff);               // fishing rope: per-segment rest length (distpAbove/distpBelow) split at anchor 18

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
        // (Stub = tools/canal_evict_fade_hook.s → Resources/isoPatch/canalEvictFadeHook.bin.)
        internal static void PatchCanalEvictFadeHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228BB0;   // dead CharaChangeLoop (reclaimable; jal-legal ELF code)
            const uint HookAddr = 0x00189970;   // EdFadeInOut fade-out `fade_end = 1` store
            if (RdU32(fs, ElfOff(HookAddr)) != 0xAF83920C)
                throw new IOException($"Canal-evict hook site 0x{HookAddr:X} is not vanilla `sw $v1,-0x6df4($gp)` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.canalEvictFadeHook.bin")
                ?? throw new IOException("Embedded EE function missing: canalEvictFadeHook.bin (reassemble tools/canal_evict_fade_hook.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0xAF83920C)
                throw new IOException($"canalEvictFadeHook.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // store → jal stub; delay slot `clear $s4` runs first (harmless loop init)
        }

        // ── Queens waterfall spray hook ──────────────────────────────────────────────────────────────
        // MainDraw @0x17c5a0 is `jal EditEffectStep2` (0x166de0) — the point where the Matataki-spray branch and
        // the non-Matataki path converge, right before DrawEffect. Redirect it to the queensSprayCave (in the dead
        // CharaChange region, after the fade hook), which spawns EffectWaterSpray emitters from CanalTide's table
        // then tail-calls EditEffectStep2. Its delay slot is a nop (nothing displaced), so the redirect is a clean
        // one-word swap. (Stub = tools/queens_spray_cave.s → Resources/isoPatch/queensSprayCave.bin.)
        internal static void PatchQueensSprayHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228C00;   // dead CharaChange space, past the fade hook (0x228BB0, ~64 B)
            const uint HookAddr = 0x0017C5A0;   // MainDraw `jal EditEffectStep2` (convergence point before DrawEffect)
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059B78)   // = jal 0x00166de0
                throw new IOException($"Queens-spray hook site 0x{HookAddr:X} is not vanilla `jal EditEffectStep2` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.queensSprayCave.bin")
                ?? throw new IOException("Embedded EE function missing: queensSprayCave.bin (reassemble tools/queens_spray_cave.s and rebuild)");
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
        // this is transparent there. (Stub = tools/spray_bias_shim.s → Resources/isoPatch/sprayBiasShim.bin.)
        internal static void PatchSprayBiasShim(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228D00;   // dead CharaChange space, past the spray cave (0x228C00, 180 B)
            const uint HookAddr = 0x00165184;   // EffectWaterSpray `jal EnterEffect`
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C059260)   // = jal 0x00164980 (EnterEffect)
                throw new IOException($"Spray-bias hook site 0x{HookAddr:X} is not vanilla `jal EnterEffect` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.sprayBiasShim.bin")
                ?? throw new IOException("Embedded EE function missing: sprayBiasShim.bin (reassemble tools/spray_bias_shim.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x3C0801F2)   // first insn = lui $t0,0x1f2
                throw new IOException($"sprayBiasShim.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal EnterEffect → jal sprayBiasShim (which re-does that call)
        }

        // ── FishingLoadFish species-selection rewrite (baked, race-free) ─────────────────────────────
        // Densely rewrites the per-slot species selector [0x1a8a48,0x1a8d44) so the LOADER itself hands
        // back the right fish for every area — including the mod's custom towns (dedicated areas 5/6/7 =
        // Brownboo/Queens/Yellow Drops) — with no runtime re-species and thus no race. Native areas 0-4
        // keep their exact distributions, with two requested vanilla edits folded in: area 2 (Matataki)
        // Gummy->Niler and area 3 (East Harbor) Piccoly->Gobbler. Equal-weight pools are `rand%N -> byte
        // table` lookups, so adding a fish later is one table byte + bumping N (212 bytes of nop headroom
        // remain in-region). Assembled by tools/iso_patch/assemble_fish_pools.py; the full original and new
        // listings live in game_data/docs/fishing-loadfish-re.md.
        internal static void PatchFishingLoadFish(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SpeciesRegionAddr = 0x001A8A48;   // start of the per-slot species-selection region
            // vanilla anchors spread across the region — reject a non-vanilla / already-patched ISO
            foreach (var (va, word) in new (uint va, uint word)[]
            {
                (0x001A8A48, 0x2413FFFF),   // addiu $s3,$zero,-1   (default species; becomes `jal rand`)
                (0x001A8A8C, 0x100000AD),   // b 0x1a8d44           (old area-dispatch fall-through)
                (0x001A8D40, 0x24130010),   // addiu $s3,$zero,0x10 (last native leaf — Heela)
            })
                if (RdU32(fs, ElfOff(va)) != word)
                    throw new IOException($"FishingLoadFish region 0x{va:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");

            uint[] region =
            {
                0x0C0411BE, 0x2413FFFF, 0x24030005, 0x12C3005C, 0x24030006, 0x12C3006B,
                0x24030007, 0x12C3006C, 0x24030001, 0x12C30011, 0x24030002, 0x12C30023,
                0x24030003, 0x12C30031, 0x24030004, 0x12C3003A, 0x00000000, 0x24030004,
                0x0043001A, 0x3C01001A, 0x34218AB0, 0x00001010, 0x00220821, 0x90330000,
                0x100000A6, 0x00000000, 0x07060201, 0x24030064, 0x0043001A, 0x00000000,
                0x00000000, 0x00001010, 0x24130001, 0x28410023, 0x14200065, 0x00000000,
                0x24130004, 0x28410046, 0x14200061, 0x00000000, 0x24130009, 0x28410050,
                0x1420005D, 0x00000000, 0x2413000A, 0x10000091, 0x00000000, 0x0050001A,
                0x00000000, 0x00000000, 0x00001810, 0x1060004A, 0x00000000, 0x24030003,
                0x0043001A, 0x3C01001A, 0x34218B40, 0x00001010, 0x00220821, 0x90330000,
                0x10000082, 0x00000000, 0x00070402, 0x24030005, 0x0043001A, 0x3C01001A,
                0x34218B68, 0x00001010, 0x00220821, 0x90330000, 0x10000078, 0x00000000,
                0x0C010300, 0x0000000D, 0x0050001A, 0x00000000, 0x00000000, 0x00001810,
                0x1060002F, 0x00000000, 0x24030064, 0x0043001A, 0x00000000, 0x00000000,
                0x00001010, 0x2413000E, 0x28410028, 0x14200030, 0x00000000, 0x2413000F,
                0x28410046, 0x1420002C, 0x00000000, 0x24130010, 0x10000060, 0x00000000,
                0x24030032, 0x0043001A, 0x00000000, 0x00000000, 0x00001810, 0x10600018,
                0x00000000, 0x24030004, 0x0043001A, 0x3C01001A, 0x34218C08, 0x00001010,
                0x00220821, 0x90330000, 0x10000050, 0x00000000, 0x060E0B0B, 0x24130000,
                0x1000004C, 0x00000000, 0x24030004, 0x0043001A, 0x3C01001A, 0x34218C3C,
                0x00001010, 0x00220821, 0x90330000, 0x10000043, 0x00000000, 0x0C0E020A,
                0x0C0411BE, 0x24030005, 0x0043001A, 0x00000000, 0x00001010, 0x24130005,
                0x24030011, 0x0062980A, 0x10000038, 0x00000000, 0x10000036, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
            };
            for (int i = 0; i < region.Length; i++)
                WrU32(fs, ElfOff(SpeciesRegionAddr + (uint)i * 4), region[i]);

            // FishNum per area: default 6; area 0 -> 4 (native override kept); area 4 -> 5. The area-4
            // compare instruction @0x1a8998 doubles as the stored value, so the compare const stays 4 and
            // the `5` is written into that branch's delay slot @0x1a89a0 (a nop in vanilla).
            if (RdU32(fs, ElfOff(0x001A8980)) != 0x24020005)
                throw new IOException("FishNum default site 0x1A8980 is not vanilla.");
            WrU32(fs, ElfOff(0x001A8980), 0x24020006);          // FishNum default 5 -> 6
            if (RdU32(fs, ElfOff(0x001A89A0)) != 0x00000000)
                throw new IOException("FishNum area-4 delay slot 0x1A89A0 is not a nop.");
            WrU32(fs, ElfOff(0x001A89A0), 0x24020005);          // area 4 (Muska Lacka) -> 5 (bne delay slot)
        }

        // ── Fish collision-gather box: symmetrise +Z so thin axis-aligned +Z walls contain fish ─────────
        // Step__5CFish (0x240480) builds the AABB it hands PickUpNearPoly as max=(x+10,y+10,z),
        // min=(x-10,y,z-10) — it reaches 10u toward -Z but 0 toward +Z. So a razor-thin, axis-aligned +Z
        // wall (the Queens south canal wall) is never gathered until the fish is already through it and the
        // fish swim straight past it (north/-Z walls, and all of vanilla's thick/angled terrain, are caught
        // fine). Fix: make max.z = z+10. The build loads its 10.0 with a 2-op lui/mtc1; swapping that for a
        // 1-op $gp load of the rodata 10.0 (@0x2A22F8 = _gp(0x2A97F0)-0x74F8) frees exactly the one slot
        // needed to add +10 to max.z — a same-length in-place rewrite, no code cave. Register use is
        // otherwise identical to vanilla ($f1=10, $f2/$f3/$f4=x/y/z, $f0 scratch; $v0 no longer needed).
        internal static void PatchFishBox(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SITE = 0x00240B24;   // start of the box-build in Step__5CFish
            uint[] vanilla =
            {
                0x3C024120, 0x44820800, 0xC7A20040, 0x46020800, 0xE7A05090,
                0xC7A30044, 0x46030800, 0xE7A05094, 0xC7A40048, 0xE7A45098,
                0x46011001, 0xE7A050A0, 0xE7A350A4, 0x46012001, 0xE7A050A8,
            };
            uint[] patched =
            {
                0xC7818B08,   // lwc1  $f1, -0x74f8($gp)   ; f1 = 10.0 (rodata) — was lui $v0,0x4120
                0xC7A20040,   // lwc1  $f2, 0x40($sp)      ; x        (freed slot)
                0x46020800,   // add.s $f0, $f1, $f2       ; x+10
                0xE7A05090,   // swc1  $f0, 0x5090($sp)    ; max.x
                0xC7A30044,   // lwc1  $f3, 0x44($sp)      ; y
                0x46030800,   // add.s $f0, $f1, $f3       ; y+10
                0xE7A05094,   // swc1  $f0, 0x5094($sp)    ; max.y
                0xC7A40048,   // lwc1  $f4, 0x48($sp)      ; z
                0x46040800,   // add.s $f0, $f1, $f4       ; z+10   ← the fix
                0xE7A05098,   // swc1  $f0, 0x5098($sp)    ; max.z = z+10
                0x46011001,   // sub.s $f0, $f2, $f1       ; x-10
                0xE7A050A0,   // swc1  $f0, 0x50a0($sp)    ; min.x
                0xE7A350A4,   // swc1  $f3, 0x50a4($sp)    ; min.y = y
                0x46012001,   // sub.s $f0, $f4, $f1       ; z-10
                0xE7A050A8,   // swc1  $f0, 0x50a8($sp)    ; min.z = z-10
            };
            for (int i = 0; i < vanilla.Length; i++)
                if (RdU32(fs, ElfOff(SITE + (uint)i * 4)) != vanilla[i])
                    throw new IOException($"Fish collision-box site 0x{SITE + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < patched.Length; i++)
                WrU32(fs, ElfOff(SITE + (uint)i * 4), patched[i]);
        }

        // ── Fishing invalid-cast auto-uncast: fire fast, but only on a SETTLED bobber ────────────────
        // The engine already rejects bad casts: in the waiting state EdMoveChara calls FishingCheckUkiHook
        // (bobber/hook outside the fishing rect, or RESTING above water+5 — e.g. deposited on the canal rim
        // by the vertical probe's ground-lift) and a nonzero verdict auto-uncasts (chara_fishing=5). But it
        // waits 31 frames (`slti at,st_cnt,0x1f` @0x16C6D0) before consulting it — the bobber sits on land
        // for a beat. Two-part fix:
        //   (1) gate 0x1F -> 4: the check runs ~4 frames into the waiting state;
        //   (2) cave over the function's height-check tail (fishlineUncastGate.bin @0x228E20, entered by a
        //       `j` over the `lui v0,0x40a0` 5.0-load @0x1AA2D4): the height violation only counts when the
        //       bobber's Verlet velocity is ~0 (settled). Without this, the early check would uncast LEGIT
        //       long casts still airborne above water+5 when the waiting state begins.
        // (Cave = tools/fishline_uncast_gate.s. ISO-baked, so patching hot fishing code is safe.)
        internal static void PatchFishingUncastGate(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint UncastGateCaveAddr = 0x00228E20;                       // dead CharaChange region (ex cast-scale slot)
            const uint GateAddr = 0x0016C6D0;                       // EdMoveChara: slti at,st_cnt,0x1f (check delay)
            const uint LuiAddr = 0x001AA2D4, MtcAddr = 0x001AA2D8;   // CheckUkiHook tail: lui v0,0x40a0 ; mtc1 v0,f1
            uint gotG = RdU32(fs, ElfOff(GateAddr)), gotL = RdU32(fs, ElfOff(LuiAddr)), gotM = RdU32(fs, ElfOff(MtcAddr));
            if (gotG != 0x2841001F || gotL != 0x3C0240A0 || gotM != 0x44820800)
                throw new IOException($"Fishing uncast-gate sites are not vanilla (got 0x{gotG:X8}/0x{gotL:X8}/0x{gotM:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.fishlineUncastGate.bin")
                ?? throw new IOException("Embedded EE function missing: fishlineUncastGate.bin (reassemble tools/fishline_uncast_gate.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x3C0840A0)   // first insn = lui $t0,0x40a0
                throw new IOException($"fishlineUncastGate.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(UncastGateCaveAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(GateAddr), 0x28410004);   // slti at,st_cnt,4 — consult the check almost immediately
            // Route the tail through QueensDragCheck @0x229360 (camera_norm_side.s bank) FIRST: in Queens,
            // waiting-state only, a float dragged past the canal wall (|z|>49.5) or inside a bridge-pillar
            // box returns invalid -> native auto-uncast; otherwise it falls through (j) into the
            // settled-height cave below, unmodified. (Wall-stopped rest positions 48 / arch face 25 stay
            // fishable — the drag thresholds sit deliberately beyond them.)
            WrU32(fs, ElfOff(LuiAddr), J(0x002294C0)); // height tail -> drag check -> settled-gated cave (v10 addr)
            WrU32(fs, ElfOff(MtcAddr), 0);             // displaced mtc1 -> nop (the cave rebuilds f1 itself)
            // ── QUEENS BOBBER GROUND-LIFT GATE (QueensUkiGroundGate @0x229440, camera_norm_side.s) ──
            // FishLineStep's uki ground probe lifts the bobber onto ANY floor poly at its (x,z) — bridge
            // decks and pipe tops included (they're walkable, so they're in the fishing cpoly gather).
            // Probe-proven teleports: y 24.5 -> 70.2 onto a bridge deck (while the pillar box held x),
            // y 8.7 -> 77.8 onto the pipes; also "cast under the bridge -> bobber on top". The gate skips
            // the lift in Queens when the floor sits above water+5 (deck/pipe top — the flight clamp keeps
            // Queens casts inside the canal, so real banks are unreachable; other towns stay vanilla).
            const uint UkiGroundLuiAddr = 0x001AA538, UkiGroundMtcAddr = 0x001AA53C;   // lui v0,0x3f80 ; mtc1 v0,f1
            uint gotUG = RdU32(fs, ElfOff(UkiGroundLuiAddr)), gotUGd = RdU32(fs, ElfOff(UkiGroundMtcAddr));
            if (gotUG != 0x3C023F80 || gotUGd != 0x44820800)
                throw new IOException($"Uki ground-lift site not vanilla (got 0x{gotUG:X8}/0x{gotUGd:X8}).");
            WrU32(fs, ElfOff(UkiGroundLuiAddr), J(0x00229690));  // ground store head -> overhead-floor-gated bank sub (v10 addr)
            WrU32(fs, ElfOff(UkiGroundMtcAddr), 0);               // displaced mtc1 -> nop (sub redoes the store)
        }

        // ── Fishing rope split rest length ───────────────────────────────────────────────────────────
        // The Verlet rope uses ONE rest length `distp` @0x202A1FA4 for all 23 segments (loaded `lwc1 f,-0x784c(gp)`
        // in both FishLineInit's layout loop and FishLineStep's constraint solve). Split it at the bobber anchor
        // (index 18) into distpAbove (= the existing distp; rod→bobber = cast reach, LineScale still tunes it) and
        // distpBelow (mailbox @0x01F10048; bobber→hook = hook depth, mod-tuned). Each `lwc1` → `j <cave>` and its
        // following `sub.S` → nop; the cave (fishlineSplitCaves.bin, init@0x228DC0 / step@0x228DEC) selects the
        // rest length on the loop index s0 (<=18 above, else below), does the displaced sub.S, and jumps back.
        // Baked into the ELF (safe: on disc before FishLineStep is ever JIT'd, unlike the runtime FishLineShallow
        // cold-patch which touches the DIFFERENT anchor-load instructions). See the feasibility doc.
        internal static void PatchFishLineSplit(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228DC0, StepCaveAddr = 0x00228DEC;   // init_cave / step_cave (one bin)
            const uint InitLwc1Addr = 0x001A9CAC, InitSubAddr = 0x001A9CB0;  // FishLineInit: lwc1 f0,distp ; sub.S f0,f1,f0
            const uint StepLwc1Addr = 0x001AA7C8, StepSubAddr = 0x001AA7CC;  // FishLineStep: lwc1 f1,distp ; sub.S f2,f0,f1
            if (RdU32(fs, ElfOff(InitLwc1Addr)) != 0xC78087B4 || RdU32(fs, ElfOff(InitSubAddr)) != 0x46000801 ||
                RdU32(fs, ElfOff(StepLwc1Addr)) != 0xC78187B4 || RdU32(fs, ElfOff(StepSubAddr)) != 0x46010081)
                throw new IOException("FishLine-split sites are not vanilla `lwc1 distp`/`sub.S` — unmodified Dark Cloud (USA) ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.fishlineSplitCaves.bin")
                ?? throw new IOException("Embedded EE function missing: fishlineSplitCaves.bin (reassemble tools/fishline_split_caves.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x2A080013)   // first insn = slti $t0,$s0,0x13
                throw new IOException($"fishlineSplitCaves.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(InitLwc1Addr), J(StubAddr));    WrU32(fs, ElfOff(InitSubAddr), 0);   // j init_cave ; nop
            WrU32(fs, ElfOff(StepLwc1Addr), J(StepCaveAddr));  WrU32(fs, ElfOff(StepSubAddr), 0);   // j step_cave ; nop
        }

        // (A "cast-trajectory scale" cave hooked into the FishLineSetUki/SetHook tails was tried here and
        // REMOVED 2026-08: the throw state (chara_fishing==3) passes the -1 sentinel weight, so the bobber is
        // NOT bone-pinned during the cast — the vanilla throw is ROPE TRANSMISSION (the short taut line slings
        // the bobber; cast reach ≈ line length), and a pin-target scale never executes. The cast boost is the
        // C#-side LINE PAY-OUT in CustomFishingSpot instead: sling at vanilla length, then ramp distpAbove out
        // during the flight — see game_data/docs/fishing-line-split-and-cast-feasibility.md.)
    }
}
