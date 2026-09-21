using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Divine Beast cat: the ELF patches behind the cat shot — its pellet follower, glow, sphere percentage, guard bypass, cape and mask tints, copy queue and palettes (docs/divine-beast-title.md). Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfCatPatches
    {
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
            // Shape check: opens a stack frame (`addiu sp,sp,-N`) and performs the displaced `jal step__5CSHOT` within
            // its first eight words (after the register saves) — the frame size and save count vary by stub version.
            bool opensFrame = b.Length >= 32 && (b.Length & 3) == 0 && (U32(b, 0) & 0xFFFF8000) == 0x27BD8000;
            bool callsStep = false;
            for (int i = 4; i < 32 && i < b.Length; i += 4) if (U32(b, i) == Jal(0x001ABD10)) callsStep = true;
            if (!opensFrame || !callsStep)
                throw new IOException($"catPelletFollow.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.XiaoMeleeFlinch)   // the flinch stub sits right after it
                throw new IOException("catPelletFollow.bin overruns its cave — move ElfCave.XiaoMeleeFlinch/NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        // ── Xiao melee-type flinch ───────────────────────────────────────────────────────────────────────
        // CheckDmg__12CMonstorUnit hard-codes "Xiao's hits never stagger" (the monster step starts an enemy's damage
        // reaction, script label 110, only when CheckDmg returns 1). The Divine Beast cat's hit entry carries a
        // melee-type kick (+0x98 == 2; pellets carry 0), so DunPatches re-routes the start of that rule (dun 0x1DB410)
        // to this stub, which applies it only to entries WITHOUT that kick. Stub: tools/stubs/xiao_melee_flinch.s.
        internal static void PatchXiaoMeleeFlinch(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.XiaoMeleeFlinch;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.xiaoMeleeFlinch.bin")
                ?? throw new IOException("Embedded EE function missing: xiaoMeleeFlinch.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: 10 words, opens with `lw v0,-0x6210(gp)` (NowColData) and returns with `j 0x1DB420` + nop.
            if (b.Length != 40 || U32(b, 0) != 0x8F829DF0u || U32(b, 32) != 0x08076D08u || U32(b, 36) != 0)
                throw new IOException($"xiaoMeleeFlinch.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("xiaoMeleeFlinch.bin overruns its cave — move ElfCave.NextFree.");
            // Hook site (main ELF, CheckDmg__12CMonstorUnit 0x1D9F10): `li v0,1; bne s3,v0,+2; nop; clear s0` = the
            // "owner == Xiao → no flinch" rule; the stub replaces it and returns to the untouched tail at 0x1DB420.
            const uint HookAddr = 0x001DB410;
            const uint FlinchPreviousCave = 0x01FB1FA0;      // where the stub sat before it moved — an ISO patched then is re-hooked
            uint jump = 0x08000000u | (CaveAddr >> 2);
            uint cur0 = RdU32(fs, ElfOff(HookAddr)), cur1 = RdU32(fs, ElfOff(HookAddr + 4));
            bool vanilla = cur0 == 0x24020001u && cur1 == 0x16620002u, ours = (cur0 == jump || cur0 == J(FlinchPreviousCave)) && cur1 == 0;
            if (!(vanilla || ours) || RdU32(fs, ElfOff(HookAddr + 0x10)) != 0x8F829DF0u || RdU32(fs, ElfOff(HookAddr + 0x14)) != 0x0056A021u)
                throw new IOException($"Xiao-flinch hook site 0x{HookAddr:X} is not vanilla `li v0,1; bne s3,v0` (tail `lw v0,NowColData; addu s4,v0,s6`) — unmodified Dark Cloud (USA) ISO expected.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), jump);          // j cave
            WrU32(fs, ElfOff(HookAddr + 4), 0);         // delay slot nop (was the bne)
        }

        // ── Divine Beast cat glow ────────────────────────────────────────────────────────────────────────
        // The dungeon draw loop's two torch passes (dun 0x1DAEBF8 / 0x1DAEC10, hooked by DunPatches) come here;
        // the cave performs them and then draws the cat's `catglow` disc at its torso with CFireOmni::DrawFire.
        // Stub: tools/stubs/cat_glow_draw.s (name string at +0, entries at +0x08 / +0x20).
        internal static void PatchCatGlowDraw(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatGlowDraw;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catGlowDraw.bin")
                ?? throw new IOException("Embedded EE function missing: catGlowDraw.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: "catglow\0" then two entries that each open a 0x40 frame and jal the pass they replace.
            if (b.Length < 0x40 || (b.Length & 3) != 0 || U32(b, 0) != 0x67746163u || U32(b, 4) != 0x00776F6Cu
                || U32(b, 0x08) != 0x27BDFFC0u || U32(b, 0x10) != Jal(0x001C40C0) || U32(b, 0x20) != 0x27BDFFC0u || U32(b, 0x28) != Jal(0x001C3CC0))
                throw new IOException($"catGlowDraw.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.CatSpherePercent)   // the sphere-percent cave sits right after it
                throw new IOException("catGlowDraw.bin overruns its cave — move ElfCave.CatSpherePercent/NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        // ── Divine Beast cat: per-sphere cat percentage ─────────────────────────────────────────────────
        // CheckDmg scales a hit by the hurt sphere's per-attacker % (`_SET_BODY_COL_PARA(10+char, %)`); Minotaur Joe's
        // face is 0 % for Xiao. The cave (tools/stubs/cat_sphere_percent.s) re-forms that load's address: a Xiao-owned
        // hit whose kick type (+0x98) equals the sphere's spare[1] (`_SET_BODY_COL_PARA(1, kick)`, +0x55490 table — no
        // vanilla reader or writer, reset to 100 by every _SET_BODY_COL) reads spare[0] instead. The disc side
        // (tools/iso_patch/patch_monster_scripts.py, run by IsoPatcher.BakeMonsterSpheres) arms Joe's face with (100, 2).
        internal static void PatchCatSpherePercent(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatSpherePercent;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catSpherePercent.bin")
                ?? throw new IOException("Embedded EE function missing: catSpherePercent.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: 28 words, opens `li v1,1; bne s3,v1`, both exits `j 0x1DC08C`; the last word is the vanilla `addu at,v1,at`.
            const uint Return = 0x001DC08C;
            if (b.Length != 112 || U32(b, 0) != 0x24030001u || (U32(b, 4) >> 16) != 0x1663u || U32(b, 64) != J(Return) || U32(b, 104) != J(Return) || U32(b, 108) != 0x00610821u)
                throw new IOException($"catSpherePercent.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catSpherePercent.bin overruns its cave — move ElfCave.NextFree.");
            // Hook site (main ELF, CheckDmg__12CMonstorUnit 0x1D9F10): `lui at,5; addu at,v1,at; lw a2,0x55d0(at); lui v1,0x42c8`
            // — the per-attacker % load; the first two words become the jump, the lw stays and the cave returns onto it.
            const uint HookAddr = 0x001DC084;
            uint jump = J(CaveAddr);
            uint cur0 = RdU32(fs, ElfOff(HookAddr)), cur1 = RdU32(fs, ElfOff(HookAddr + 4));
            bool vanilla = cur0 == 0x3C010005u && cur1 == 0x00610821u, ours = cur0 == jump && cur1 == 0;
            if (!(vanilla || ours) || RdU32(fs, ElfOff(HookAddr + 8)) != 0x8C2655D0u || RdU32(fs, ElfOff(HookAddr + 12)) != 0x3C0342C8u)
                throw new IOException($"Sphere-percent hook site 0x{HookAddr:X} is not vanilla `lui at,5; addu at,v1,at; lw a2,0x55d0(at); lui v1,0x42c8` — unmodified Dark Cloud (USA) ISO expected.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), jump);          // j cave
            WrU32(fs, ElfOff(HookAddr + 4), 0);         // delay slot nop (was the addu)
        }

        // Divine Beast cat: its hits pass an enemy's GUARD WINDOW (tools/stubs/cat_guard_bypass.s). CheckDmg decides a guard
        // purely from the defender — a window flag (slot*0x20 + 0x60550) and the enemy's live motion frame inside that window's
        // [start, end] — and consults nothing on the attacking entry, so the mod could only zero the windows, which a script
        // re-registers with `_SET_GUARD_FRAME` whenever its label runs. Chest mimics do exactly that (their wake IS a guard, from
        // our own disc patch), and the 20 Hz crush kept losing the race. The cave takes over the flag load and
        // reports "no window" when the entry is Xiao's with the cat's kick type (+0x58 == 1, +0x98 == 2).
        internal const uint GuardBypassHookAddr = 0x001DAC78;                              // CheckDmg's guard-window load site
        internal const long GuardBypassHookAddrMmu = 0x20000000L + GuardBypassHookAddr;
        private static readonly uint[] GuardBypassPreviousCaves = { 0x01FB2250, 0x01FB3F40, 0x01FB1ED0, 0x01FB40C0, 0x01FB4120 }; // where the cave sat before it moved — an ISO patched then is re-hooked
        internal static void PatchCatGuardBypass(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.DunCave.CatGuardBypass;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catGuardBypass.bin")
                ?? throw new IOException("Embedded EE function missing: catGuardBypass.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            const uint Return = 0x001DAC80;                    // the `beq v0,zero` right after the hooked load
            // Shape: opens `lw at,-0x6210(gp)`, both exits `j 0x1DAC80`, and carries the vanilla `lh v0,0x550(at)`.
            int exits = 0; bool vanillaLoad = false;
            for (int i = 0; i + 4 <= b.Length; i += 4) { uint w = U32(b, i); if (w == J(Return)) exits++; if (w == 0x84220550u) vanillaLoad = true; }
            if (b.Length % 4 != 0 || U32(b, 0) != 0x8F819DF0u || exits != 2 || !vanillaLoad)
                throw new IOException($"catGuardBypass.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (b.Length > CodeCaves.DunCave.CatGuardBypassSpan)
                throw new IOException("catGuardBypass.bin overruns MemoryMapDump's span in dun.bin.");
            // Hook the `addu`, NOT the `lh` after it: the `lh`'s own delay slot would be the `beq` at 0x1DAC80, and a branch in a
            // branch's delay slot is undefined on the R5900. Taking the `addu` leaves the `lh` as the delay slot, which is then
            // nop'd — the cave re-forms the address from a2/v1 itself and does the load, so neither word is needed.
            const uint HookAddr = GuardBypassHookAddr;         // `addu at,v0,at`, feeding `lh v0,0x550(at)`
            uint jump = J(CaveAddr);
            uint cur0 = RdU32(fs, ElfOff(HookAddr)), cur1 = RdU32(fs, ElfOff(HookAddr + 4));
            bool vanilla = cur0 == 0x00410821u && cur1 == 0x84220550u, ours = (cur0 == jump || Array.Exists(GuardBypassPreviousCaves, c => cur0 == J(c))) && cur1 == 0;
            if (!(vanilla || ours) || RdU32(fs, ElfOff(HookAddr - 4)) != 0x3C010006u || RdU32(fs, ElfOff(HookAddr + 8)) != 0x104000D1u)
                throw new IOException($"Guard-window hook site 0x{HookAddr:X} is not vanilla `lui at,0x6; addu at,v0,at; lh v0,0x550(at); beq v0,zero` — unmodified Dark Cloud (USA) ISO expected.");
            // the cave bytes themselves go into dun.bin (DunPatches.Caves); only the hook is main-ELF
            WrU32(fs, ElfOff(HookAddr), jump);                 // j cave
            WrU32(fs, ElfOff(HookAddr + 4), 0);                // delay slot nop (was the lh the cave now performs)
        }

        // Divine Beast cat: the Super Steve cape draws under its own ambient (tools/stubs/cat_cape_tint.s). Draw__10CCharacter
        // saves the global ambient, adds the CHARACTER's tint, then draws its meshes AND its cloth list inside that window — so a
        // cloth is lit by the character's colour and has none of its own (writing its material's colour rows does nothing). The
        // cave wraps the cloth-draw call and, for the one cloth the mod names in Mailbox.CatCapeCloth, adds Mailbox.CatCapeTint
        // to the ambient for that draw alone; every other cloth in the game is untouched.
        internal static void PatchCatCapeTint(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatCapeTint;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catCapeTint.bin")
                ?? throw new IOException("Embedded EE function missing: catCapeTint.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: 44 words, opens `addiu sp,sp,-0x50`, and calls Draw__6CCloth (0x13B640) on both paths.
            if (b.Length != 176 || U32(b, 0) != 0x27BDFFB0u || U32(b, 124) != Jal(0x0013B640u) || U32(b, 156) != Jal(0x0013B640u))
                throw new IOException($"catCapeTint.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catCapeTint.bin overruns its cave — move ElfCave.NextFree.");
            const uint HookAddr = 0x00139694;              // `jal Draw__6CCloth` in Draw__10CCharacter's cloth-list loop
            uint jal = Jal(CaveAddr), vanilla = Jal(0x0013B640u);
            uint cur = RdU32(fs, ElfOff(HookAddr));
            if (!(cur == vanilla || cur == jal) || RdU32(fs, ElfOff(HookAddr - 8)) != 0x10800003u)
                throw new IOException($"Cloth-draw hook site 0x{HookAddr:X} is not vanilla `beq a0,zero,+3; nop; jal Draw__6CCloth` — unmodified Dark Cloud (USA) ISO expected.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), jal);              // the cave calls Draw__6CCloth itself, on both paths
        }

        /// <summary>The Super Steve cat's MASK under the cape's ambient. Unlike every other cave here this one patches NO hook
        /// site: a mesh draws through a C++ virtual call, so there is no `jal` to take. The bytes just have to exist, and
        /// CatCopy.MaskTint reaches them at runtime by giving the mask's own copied CVisualMDT a private vtable whose
        /// two DrawVu1 slots point in here. Nothing else in the game can arrive at it.</summary>
        internal static void PatchCatMaskTint(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatMaskTint;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catMaskTint.bin")
                ?? throw new IOException("Embedded EE function missing: catMaskTint.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: 57 words, two entries that load the real DrawVu1 overloads (0x1360E0 / 0x136200) and fall into one body.
            if (b.Length != 228 || U32(b, 0) != 0x3C190013u || U32(b, 8) != 0x373960E0u || U32(b, 20) != 0x37396200u)
                throw new IOException($"catMaskTint.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catMaskTint.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        /// <summary>The cat's mesh-copy queue. Like the mask's cave this patches NO hook site of its own: DunPatches already
        /// aims the dungeon step loop's once-per-frame call at the cat, and that aim now lands here instead of straight on
        /// CatPelletFollow — this cave services the queue when there is one and jumps on to the follower either way.</summary>
        internal static void PatchCatCopyQueue(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatCopyQueue;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catCopyQueue.bin")
                ?? throw new IOException("Embedded EE function missing: catCopyQueue.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: opens by materialising the queue address and ends `j CatPelletFollow` (its tail calls CatPalette and
            // CatGlowPalette first — the cape's colour, then the glow's).
            if (b.Length != 592 || U32(b, 0) != 0x3C0801FAu || U32(b, b.Length - 8) != (0x08000000u | (CodeCaves.ElfCave.CatPelletFollow >> 2)))
                throw new IOException($"catCopyQueue.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catCopyQueue.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        /// <summary>The cape/mask element colour, repainted in the machine (tools/stubs/cat_palette.s). Called once per
        /// dungeon frame from the copy-queue cave's tail; the first six words are the colour table, so the code — and the
        /// call target — start at +0x18.</summary>
        internal static void PatchCatPalette(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatPalette;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catPalette.bin")
                ?? throw new IOException("Embedded EE function missing: catPalette.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: six colour words, then `lui t0,0x01CD` (Xiao's equipped-slot byte) at the entry point.
            if (b.Length < 0x18 + 8 || U32(b, 0x18) != 0x3C0801CDu)
                throw new IOException($"catPalette.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catPalette.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        /// <summary>The GLOW disc's six per-element palettes: pure DATA, 512 B each in element order (00 Fire … 05 None).
        /// Baked by `build_cat_pack.py --palettes` off the SAME luminance index map as the disc it colours, so the two are
        /// regenerated together — a table built against a different map paints the right colours onto the wrong levels.</summary>
        internal static void PatchCatGlowPalettes(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatGlowPalTables;
            const int Expected = 9 * 128 * 4;         // nine rows (6 elements + 3 weapon looks) x the 128 permutation-safe CLUT words
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catGlowPalettes.bin")
                ?? throw new IOException("Embedded data missing: catGlowPalettes.bin (run build_cat_pack.py --palettes and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length != Expected)
                throw new IOException($"catGlowPalettes.bin is {b.Length} B, expected {Expected} — re-run build_cat_pack.py --palettes.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.CatGlowPalette)
                throw new IOException("catGlowPalettes.bin overruns its table cave — the stub starts right after it.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        /// <summary>The element GLOW's palette, repainted in the machine (tools/stubs/cat_glow_palette.s). Rides the same
        /// once-per-frame call from the copy-queue cave's tail that the cape's palette does.</summary>
        internal static void PatchCatGlowPalette(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.CatGlowPalette;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catGlowPalette.bin")
                ?? throw new IOException("Embedded EE function missing: catGlowPalette.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: all code, opening `lui t5,0x01FB` (the mailbox page, for the row the mod asks for) — no leading
            // data table, unlike CatPalette.
            if (b.Length < 8 || U32(b, 0) != 0x3C0D01FBu)
                throw new IOException($"catGlowPalette.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            // The by-name scan is the one part of this cave that fails INVISIBLY: a mistyped name word matches nothing, the
            // cave returns, and the glow silently keeps its baked colour (0x706F776C spells "lwop", which
            // spells "lwop"). So require the compare's own immediate to be present, twice: cached-entry check and scan loop.
            int nameWords = 0;
            for (int i = 0; i + 4 <= b.Length; i += 4) if (U32(b, i) == 0x37186F6Cu) nameWords++;
            if (nameWords < 2)
                throw new IOException("catGlowPalette.bin does not spell \"lowp\" (0x70776F6C) — its by-name scan would match nothing.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("catGlowPalette.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }
    }
}
