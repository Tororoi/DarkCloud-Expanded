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
        // EE encoders / register numbers this file's caves need that MipsAsm doesn't expose (kept local to
        // honour the patch's file scope; MipsAsm supplies Lui/Ori/Lw/Sw/Addiu/Move/Jal/J and zero/v0/a0..a3/t0/sp).
        private const int at = 1, s0 = 16, s2 = 18, s8 = 30, ra = 31;
        private static uint Bne(int rs, int rt, int off) => 0x14000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(off & 0xFFFF);
        private static uint Jr(int rs) => ((uint)rs << 21) | 0x08u;

        // FPU (cop1) encoders + FPR numbers for the exclamation-height cave (MipsAsm stays integer-only, so these
        // are kept local like Bne/Jr above). Verified against EdDrawSysCursor's own listing: lwc1 f1,0(v0)=0xC4410000,
        // swc1 f0,0x94(sp)=0xE7A00094, add.S f0,f0,f1=0x46010000.
        private const int f0 = 0, f2 = 2;
        private static uint Lwc1(int ft, int off, int b) => 0xC4000000u | ((uint)b << 21) | ((uint)ft << 16) | (uint)(off & 0xFFFF);
        private static uint Swc1(int ft, int off, int b) => 0xE4000000u | ((uint)b << 21) | ((uint)ft << 16) | (uint)(off & 0xFFFF);
        private static uint AddS(int fd, int fs, int ft) => 0x46000000u | ((uint)ft << 16) | ((uint)fs << 11) | ((uint)fd << 6);

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
            // va → ISO file offset. Two segments: the mod's own cave segment (the hijacked phdr3, guest
            // [ElfCave.RegionStart, ElfCave.RegionEnd) ↔ file [SegmentFileOff, +size)), else the main
            // phdr0 linear map. HijackPhdr3CaveSegment (below) creates the former BEFORE any cave write.
            long ElfOff(uint va) =>
                (va >= CodeCaves.ElfCave.RegionStart && va < CodeCaves.ElfCave.RegionEnd)
                    ? elfIso + CodeCaves.ElfCave.SegmentFileOff + (va - CodeCaves.ElfCave.RegionStart)
                    : elfIso + pOff + (va - pVa);

            // Create the cave segment FIRST — every ElfCave-targeted write below lands in its file span.
            HijackPhdr3CaveSegment(fs, elfIso, phoff, phent, phnum, elf.Size);

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
            PatchCatPelletFollow(fs, ElfOff);             // Divine Beast cat: native pellet follower cave (the dun.bin hook is in DunPatches)
            PatchIdleMotionOverride(fs, ElfOff);          // town idle motion (char+0xc68): idle(0)+mailbox → override index (idle→sit for the swapped-in cat); run/walk untouched
            PatchLadderRefusal(fs, ElfOff);               // town ladder-mount gate: BlockLadder mailbox → skip EdInitHashigo + climbing flag (non-Toan ally can't climb) and raise RefusalRequested
            PatchExclamationHeight(fs, ElfOff);           // player "!" mark Y store: add ExclamationYBoost mailbox (0 = vanilla) → lift the mark off a shorter swapped-in ally's mesh (the cat)
            // (ally-swap buffer grow removed — every ally now fits the vanilla arenas; see the note below)

            byte[] pelf = Rd(fs, elfIso, (int)elf.Size);
            uint crc = 0;
            for (int i = 0; i < pelf.Length / 4; i++) crc ^= U32(pelf, i * 4);
            return crc;
        }

        // ── The ELF cave SEGMENT: hijack the degenerate phdr3 into a real PT_LOAD ────────────────────
        // SCUS_971.11 ships 4 program headers; phdr3 is a DEGENERATE placeholder (PT_LOAD, filesz=0,
        // MEMSZ=0 — it loads and reserves nothing). Rewrite it to load file span
        // [ElfCave.SegmentFileOff, +0x2000) at guest [ElfCave.RegionStart, RegionEnd): that file span is
        // dead .reldun debug data BEYOND every phdr's file extent (phdr0 loads only 0x100..0x1a2480;
        // phdr1-3 have filesz=0), so PCSX2 never reads it — and RE tooling uses the PRISTINE extracted
        // ELF, so clobbering it in the PATCHED ISO loses nothing. The guest band is inside the mod's
        // scanner-proven clean heap tail (see CodeCaveAddresses ElfCave doc). The loader writes the bytes
        // at BOOT — cold, before any recompilation — so direct j/jal into the caves is safe.
        // The whole span is ZERO-FILLED here (deterministic content between the caves; the debug garbage
        // would otherwise persist), so this MUST run before any ElfCave-targeted cave write.
        internal static void HijackPhdr3CaveSegment(FileStream fs, long elfIso, uint phoff, ushort phent, ushort phnum, uint elfSize)
        {
            const uint SegVa   = CodeCaves.ElfCave.RegionStart;
            const uint SegOff  = CodeCaves.ElfCave.SegmentFileOff;
            const uint SegSize = CodeCaves.ElfCave.RegionEnd - CodeCaves.ElfCave.RegionStart;   // 0x2000

            if (phnum != 4)
                throw new IOException($"Expected 4 ELF program headers, got {phnum} — wrong ISO/version.");
            if (elfSize < SegOff + SegSize)
                throw new IOException($"ELF too small (0x{elfSize:X}) for the cave segment span 0x{SegOff:X}..0x{SegOff + SegSize:X}.");
            // The span must lie beyond every phdr's FILE extent (it holds dead debug data, loaded by nobody).
            for (int i = 0; i < phnum; i++)
            {
                byte[] p = Rd(fs, elfIso + phoff + i * phent, 32);
                uint off = U32(p, 4), fsz = U32(p, 16);
                if (i != 3 && off + fsz > SegOff)
                    throw new IOException($"phdr{i} file extent 0x{off:X}+0x{fsz:X} reaches the cave segment span @0x{SegOff:X} — unexpected ELF layout.");
            }

            long p3 = elfIso + phoff + 3 * phent;
            byte[] ph3 = Rd(fs, p3, 32);
            // vanilla phdr3, all 32 bytes exactly: type=1 off=0x1a2480 vaddr=paddr=0x1f06b00 filesz=0 memsz=0 flags=6 align=0x10
            byte[] vanilla =
            {
                0x01,0x00,0x00,0x00, 0x80,0x24,0x1A,0x00, 0x00,0x6B,0xF0,0x01, 0x00,0x6B,0xF0,0x01,
                0x00,0x00,0x00,0x00, 0x00,0x00,0x00,0x00, 0x06,0x00,0x00,0x00, 0x10,0x00,0x00,0x00,
            };
            for (int i = 0; i < 32; i++)
                if (ph3[i] != vanilla[i])
                    throw new IOException($"phdr3 is not the vanilla degenerate placeholder (byte {i}: got 0x{ph3[i]:X2}) — unmodified Dark Cloud (USA) ISO expected.");

            // (SegOff % 0x80 == 0 and SegVa % 0x80 == 0, satisfying p_align.)
            WrU32(fs, p3 + 0,  1);         // p_type   = PT_LOAD
            WrU32(fs, p3 + 4,  SegOff);    // p_offset
            WrU32(fs, p3 + 8,  SegVa);     // p_vaddr
            WrU32(fs, p3 + 12, SegVa);     // p_paddr
            WrU32(fs, p3 + 16, SegSize);   // p_filesz
            WrU32(fs, p3 + 20, SegSize);   // p_memsz
            WrU32(fs, p3 + 24, 7);         // p_flags  = RWX
            WrU32(fs, p3 + 28, 0x80);      // p_align

            Wr(fs, elfIso + SegOff, new byte[SegSize]);   // zero-fill the whole span
        }

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
            const uint StubAddr = CodeCaves.ElfCave.CanalEvictFadeHook;   // registry: CodeCaveAddresses.ElfCave
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

        // ── Ally in-place-swap buffer grow: REMOVED 2026-09-04 ───────────────────────────────────────
        // PatchAllyTextureBudget grew the town TEXTURE arena (0x1d3a080) + GEOMETRY arena (0x1d3a060) and
        // shrank the scene arena (0x1d3a050) so Ungaga's oversized cloth-less NPC model (c10a, 1.05MB) would
        // fit an in-place _LOAD_MAIN_CHARA. It had a nasty side effect: growing the TEXTURE arena pushed the
        // GEOMETRY arena's base later, and the town player's CLOTH lives in geometry — so on the first cold
        // Queens load it INTERMITTENTLY landed on uninitialised boot memory and its Verlet solver blew up
        // (Toan's "missing front cape"). Switching Ungaga to his 479KB PLAYER model c10p (which also gives him
        // real cloth) dropped every ally under Toan's 850KB, so all fit the VANILLA arenas — the grow became
        // unnecessary and removing it returned the cloth buffer to its benign vanilla home, fixing Toan's cape
        // (confirmed clean across cold boots 2026-09-04). Recover from git if a future ally ever exceeds vanilla.
        // See [[ccloth-particle-layout]], [[town-ally-switch-reload]].

        // ── Town idle-motion override (idle → sit for the swapped-in cat) ────────────────────────────
        // EdMoveChara @0x16a160 drives the town character's idle/run/walk animation with ONE grounded write:
        //   0x16a6a8  sw s0,0xc68(s2)   ; *(char+0xc68) = motion   (s0 = 0 idle / 1 run / 2 walk, s2 = char)
        // guarded by `if ((chara_mode & 6)==0 && chara_fishing < 2)` — the plain locomotion store, NOT the
        // airborne fall/land writes (which store the constants 8 and 9) nor the fishing-state writes. Redirect
        // it to a tiny cave in the mod's ELF cave segment (loader-loaded at boot — a jal there is legal; runtime-
        // written heap caves crash the recompiler): the cave keeps run/walk as-is, and when the motion is idle (0) it stores
        // the IdleMotionOverride mailbox (guest 0x01F10070) instead — so a non-zero mailbox (the mod's sit index)
        // makes an idle town character sit, while a zero mailbox leaves vanilla idle untouched. The jal's delay
        // slot is the following `sw zero,0xc64(s2)` (kept — order-independent), and the cave returns via `jr $ra`
        // to 0x16a6b0 (the c60 stores). Scratch = $v0 (reloaded by `lui v0` at the return) and $at (dead after
        // the guard branch), both dead across the hook; $ra is stack-saved at function entry (`sq ra,0xc0(sp)`),
        // so the jal's $ra clobber is safe. $s0/$s2 are read-only. (Cave hand-built via the MipsAsm encoders.)
        // ── Divine Beast cat: native pellet follower ─────────────────────────────────────────────────────
        // The charged shot's cat rides the live pellet (head on the pellet's point) and grows in over a few
        // frames — a mod-thread follower trails and jitters, so a cave does it: DunPatches redirects the dungeon
        // step loop's `jal step__5CSHOT` (dun 0x1DB874C, once per frame, a0 = the player shot pool) to this cave,
        // which performs that call and then places chara slot 1 from the pellet the Mailbox names (see
        // Mailbox.CatPelletSlot). Stub: tools/stubs/cat_pellet_follow.s → catPelletFollow.bin.
        internal static void PatchCatPelletFollow(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatPelletFollow;   // registry: CodeCaveAddresses.ElfCave
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catPelletFollow.bin")
                ?? throw new IOException("Embedded EE function missing: catPelletFollow.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x27BDFFF0 || U32(b, 8) != Jal(0x001ABD10))   // addiu sp,-0x10 … jal step__5CSHOT
                throw new IOException($"catPelletFollow.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catPelletFollow.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        internal static void PatchIdleMotionOverride(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x0016A6A8;   // EdMoveChara grounded locomotion store `sw s0,0xc68(s2)`
            // NOT a hand-picked literal — a first placement sat inside the fishline-split bin and clobbered the
            // rope step-cave's tail → every Queens fishing session hung on entry. The ELF cave-segment map
            // lives in CodeCaveAddresses.ElfCave — place new caves from THERE, never from a patch-local literal.
            const uint CaveAddr = CodeCaves.ElfCave.IdleMotionOverride;
            uint mbGuest = (uint)(CodeCaves.Mailbox.IdleMotionOverride - 0x20000000);   // 0x01F10070 (guest form the cave reads)

            if (RdU32(fs, ElfOff(HookAddr)) != Sw(s0, 0xc68, s2))   // 0xAE500C68
                throw new IOException($"Idle-motion hook site 0x{HookAddr:X} is not vanilla `sw s0,0xc68(s2)` — unmodified Dark Cloud (USA) ISO expected.");

            uint mbFlagsGuest = (uint)(CodeCaves.Mailbox.IdleMotionFlags - 0x20000000);   // 0x01F10080 — same upper half as the index mailbox
            uint[] cave = {
                Move(v0, s0),                                          // v0 = motion (default: as the engine computed)
                Bne(s0, zero, 5), 0,                                   // motion != 0 (run/walk) → keep it; branch to the store. delay = nop
                Lui(at, mbGuest >> 16),
                Lw(v0, (int)(mbGuest & 0xFFFF), at),                   // idle: v0 = *IdleMotionOverride (0 → still idle)
                Lw(at, (int)(mbFlagsGuest & 0xFFFF), at),              // at = *IdleMotionFlags (bit1 = play once + hold last frame)
                Sw(at, 0xc64, s2),                                     // re-write char+0xc64 (the hook's delay slot zeroed it) — 0 = vanilla loop
                Jr(ra), Sw(v0, 0xc68, s2),                             // return to 0x16a6b0; delay slot stores the motion id to char+0xc68
            };
            for (int i = 0; i < cave.Length; i++)
                WrU32(fs, ElfOff(CaveAddr + (uint)(i * 4)), cave[i]);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // store → jal cave; delay slot `sw zero,0xc64(s2)` runs first (harmless — c64 is zeroed either way)
        }

        // ── Town ladder-mount refusal (a swapped-in non-Toan ally must never climb) ──────────────────
        // The town ladder MOUNT loads a Toan-rigged climb overlay onto the active model; with a non-Toan ally
        // swapped in that rig mismatch crashes. EdMoveChara @0x16a160 mounts a ladder (event-point type 4/5) with
        // exactly two instructions, both under the SAME `PadDown(Cross) && (iVar9!=0 || viewMode==0)` press gate:
        //   0x16c0fc  jal EdInitHashigo(0x16d720)   ; a0=0x1d3d1d0, a1=s5 — THE MOUNT (loads the climb overlay)
        //   0x16c104  li  s8,0x1                     ; climbing flag → stored to DAT_01d1970c @0x16c268
        // (s8's not-climbing default is -1, set by `moveq s8,s6` @0x16bf18 with s6=-1; the vanilla no-press path
        // simply skips 0x16c104, leaving s8=-1.) Both must be skipped TOGETHER or the game thinks it is climbing
        // with no setup and hangs. Redirect the `jal EdInitHashigo` to a cave in the mod's ELF cave segment
        // (loader-loaded at boot — a jal there is legal; runtime-written heap caves crash the recompiler): it reads BlockLadder,
        // and when it is 0 it replicates vanilla exactly (calls EdInitHashigo with a0/a1 still set, then sets the
        // climbing flag s8=1), returning past the `li s8,1` to 0x16c108. When BlockLadder != 0 it raises the
        // RefusalRequested mailbox and returns to 0x16c108 WITHOUT the mount and WITHOUT touching s8 (so the
        // not-climbing -1 flows straight through) — the refusal is armed only here, inside the mount press gate,
        // so it fires once per Cross press like the vanilla one-shot mount, not every frame near the ladder. Both
        // paths return via `j 0x16c108`, so the inner EdInitHashigo call clobbering $ra is irrelevant (vanilla
        // clobbers it at the same site anyway, and the function reloads $ra from the stack at its epilogue).
        // Scratch = $at + $v0 (both dead across the site); $s5/$a0/$a1 are read-only until the preserved call.
        // (Cave hand-built via the MipsAsm encoders, like PatchIdleMotionOverride.)
        internal static void PatchLadderRefusal(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr      = 0x0016C0FC;   // EdMoveChara ladder mount `jal EdInitHashigo`
            const uint EdInitHashigo = 0x0016D720;   // the mount (loads the climb overlay)
            const uint AfterFlag     = 0x0016C108;   // return target: `li s4,1`, one past the `li s8,1` climbing-flag set
            const uint CaveAddr      = CodeCaves.ElfCave.LadderRefusal;   // registry: CodeCaveAddresses.ElfCave
            uint blockGuest   = (uint)(CodeCaves.Mailbox.BlockLadder      - 0x20000000);   // 0x01F10074 (guest form the cave reads)
            uint refusalGuest = (uint)(CodeCaves.Mailbox.RefusalRequested - 0x20000000);   // 0x01F10078 (guest form the cave sets)

            if (RdU32(fs, ElfOff(HookAddr)) != Jal(EdInitHashigo))   // 0x0C05B5C8
                throw new IOException($"Ladder-mount hook site 0x{HookAddr:X} is not vanilla `jal EdInitHashigo` — unmodified Dark Cloud (USA) ISO expected.");

            uint[] cave = {
                Lui(at, blockGuest >> 16),                                   // at = mailbox page (0x01F10000)
                Lw(v0, (int)(blockGuest & 0xFFFF), at),                      // v0 = *BlockLadder
                Bne(v0, zero, 6), 0,                                         // BlockLadder != 0 → BLOCK path (word 9); delay nop
                Jal(EdInitHashigo), 0,                                       // vanilla: EdInitHashigo(a0=0x1d3d1d0, a1=s5); delay nop
                Addiu(s8, zero, 1),                                          // climbing flag s8 = 1
                J(AfterFlag), 0,                                             // return past the `li s8,1`; delay nop
                Addiu(v0, zero, 1),                                          // BLOCK: v0 = 1
                Sw(v0, (int)(refusalGuest & 0xFFFF), at),                    // *RefusalRequested = 1 (at still = mailbox page)
                J(AfterFlag), 0,                                             // return WITHOUT mount, s8 untouched (stays -1); delay nop
            };
            for (int i = 0; i < cave.Length; i++)
                WrU32(fs, ElfOff(CaveAddr + (uint)(i * 4)), cave[i]);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // jal EdInitHashigo → jal cave; delay slot @0x16c100 (nop) runs first
        }

        // ── Player exclamation-mark height boost (lift the "!" off a shorter ally's mesh) ─────────────
        // EdDrawSysCursor @0x17cbf0 draws the PLAYER's floating "!" event-trigger mark inside its
        // `if (DAT_01d3d4a8 != 0)` block. The mark's world Y is accumulated in fStack_c (= auStack_10+4, sp+0x94):
        //   0x17cf58  add.S f0,f0,f1     ; f0 = fStack_c + (3.0 + *(Chara+0xb4) + sinf(a)*0.5)   = vanilla Y
        //   0x17cf5c  swc1  f0,0x94(sp)  ; STORE Y → auStack_10+4   ← THE HOOK
        //   0x17cf60  lwc1  f1,-0x6e54(gp); f1 = a_1906   (bob-angle reload — the jal's delay slot)
        //   0x17cf68  add.S f1,f1,f0     ; a_1906 += delta  ← still needs f1 = a_1906
        // A swapped-in ally with different proportions (the cat) sits lower, so the mark pokes through its mesh.
        // Redirect the store to a cave in the mod's ELF cave segment (loader-loaded at boot — a jal there is legal;
        // runtime-written heap caves crash the recompiler): it adds *ExclamationYBoost (guest 0x01F1007C, EE-writable) to the Y and
        // then performs the displaced store, so the mark rides `vanilla Y + boost`. A 0.0 boost reproduces vanilla
        // bit-exactly for any real position (x + 0.0 == x). This is the PLAYER mark ONLY — the NPC-cursor loop
        // earlier in the function (the `DAT_01d25c44 + 2.0 / offset_1876` store `swc1 f0,0x0(s3)` @0x17cd28) is
        // untouched. Register contract at the hook: the `jal cave` return address is 0x17cf64 (jal+8) and its delay
        // slot @0x17cf60 runs FIRST, so on cave entry f1 already = a_1906 (must be PRESERVED for 0x17cf68) and f0
        // still = the vanilla Y (0x17cf60 doesn't touch f0). The cave therefore scratches f2 (dead — last held
        // 0.5*sin, consumed at 0x17cf50) and $at (dead here; also clobbered by the later SetPosition call), never
        // f0/f1. $ra is stack-saved at entry (`sq ra,0x60(sp)`), so the jal's $ra clobber is safe.
        // (Cave hand-built via the MipsAsm encoders + the local Lwc1/Swc1/AddS, like PatchIdleMotionOverride.)
        internal static void PatchExclamationHeight(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x0017CF5C;   // EdDrawSysCursor PLAYER-mark final Y store `swc1 f0,0x94(sp)`
            const uint CaveAddr = CodeCaves.ElfCave.ExclamationHeight;   // registry: CodeCaveAddresses.ElfCave
            uint mbGuest = (uint)(CodeCaves.Mailbox.ExclamationYBoost - 0x20000000);   // 0x01F1007C (guest form the cave reads)

            if (RdU32(fs, ElfOff(HookAddr)) != Swc1(f0, 0x94, sp))   // 0xE7A00094
                throw new IOException($"Exclamation-height hook site 0x{HookAddr:X} is not vanilla `swc1 f0,0x94(sp)` — unmodified Dark Cloud (USA) ISO expected.");

            uint[] cave = {
                Lui(at, mbGuest >> 16),                     // at = mailbox page (0x01F10000)
                Lwc1(f2, (int)(mbGuest & 0xFFFF), at),      // f2 = *ExclamationYBoost   (0.0 → identity)
                AddS(f0, f0, f2),                           // f0 = vanilla Y + boost
                Swc1(f0, 0x94, sp),                         // displaced original store → auStack_10+4
                Jr(ra), 0,                                  // return to 0x17cf64 (jal+8); delay slot nop
            };
            for (int i = 0; i < cave.Length; i++)
                WrU32(fs, ElfOff(CaveAddr + (uint)(i * 4)), cave[i]);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // store → jal cave; delay slot @0x17cf60 (lwc1 f1,a_1906) runs first, unchanged
        }

        // ── Queens waterfall spray hook ──────────────────────────────────────────────────────────────
        // MainDraw @0x17c5a0 is `jal EditEffectStep2` (0x166de0) — the point where the Matataki-spray branch and
        // the non-Matataki path converge, right before DrawEffect. Redirect it to the queensSprayCave (in the mod's
        // ELF cave segment, after the fade hook), which spawns EffectWaterSpray emitters from CanalTide's table
        // then tail-calls EditEffectStep2. Its delay slot is a nop (nothing displaced), so the redirect is a clean
        // one-word swap. (Stub = tools/stubs/queens_spray_cave.s → Resources/isoPatch/queensSprayCave.bin.)
        internal static void PatchQueensSprayHook(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = CodeCaves.ElfCave.QueensSpray;   // registry: CodeCaveAddresses.ElfCave
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
            const uint StubAddr = CodeCaves.ElfCave.SprayBiasShim;   // registry: CodeCaveAddresses.ElfCave
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
