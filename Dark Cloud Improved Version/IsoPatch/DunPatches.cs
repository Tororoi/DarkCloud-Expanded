using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Word patches to <c>dun.bin</c> — the dungeon overlay (disc root <c>DUN.BIN</c>, loaded at
    /// <see cref="LoadBase"/> every dungeon). Why the ISO and not runtime writes or the pnach: overlay code is
    /// HOT every frame (a PINE write to it crashes the recompiler — the ABS floor-load lesson) and reloads with
    /// every dungeon, and a pnach per-frame write must be gated because the town overlay shares these addresses.
    /// A baked word changes only this file and needs no gating. Data a patched load points at lives in the
    /// MAILBOX page (0x01F10000): a PINE write into a page holding executed code SIGBUSes PCSX2, and the ELF
    /// cave segment is such a page (2026-09-09 crash). The pnach seeds the defaults whenever the app is idle.
    /// File offset of a word = record start + (RAM address − LoadBase): the overlay is a flat image.
    /// </summary>
    internal static class DunPatches
    {
        internal const uint LoadBase = 0x01DABD00;

        private sealed record Word(uint Addr, uint Orig, uint New, string What);

        private static readonly Word[] Words =
        {
            // Passive HEAL ability (weapon flag 0x800) cadence: heal tick compares its frame counter with
            // `slti v0,v0,0xF0` (240 f = 4 s, dun 0x1DB8234); 0xB4 = 180 f = 3 s for every HEAL weapon
            // (user 2026-09-09). Guardian Grace reads the threshold from this word and floors the counter while Xiao guards.
            new(HealCadenceAddr, HealCadenceOrig, HealCadenceNew, "heal-ability cadence 4 s → 3 s"),
            // Xiao's attack-gauge refill multiplier (motionDrive: `lui v0,0x3fc0; mtc1 v0,f0`, v0 dead after)
            // → `lui v0,HI; lwc1 f0,LO(v0)` of Mailbox.ShieldGaugeRate (pnach-seeded 1.5 = vanilla while idle).
            new(0x01DB8090, 0x3C023FC0, 0x3C020000u | (uint)((CodeCaves.Mailbox.ShieldGaugeRate - 0x20000000) >> 16),    "gauge refill multiplier → mailbox word (lui)"),
            new(0x01DB8094, 0x44820000, 0xC4400000u | (uint)((CodeCaves.Mailbox.ShieldGaugeRate - 0x20000000) & 0xFFFF), "gauge refill multiplier → mailbox word (lwc1 f0)"),
            // Xiao's per-shot WHP factor: BattleActionPlay_Jinn passes SwordDmgCheck1 an immediate 1.0 (`lui v0,0x3f80; mtc1 v0,f12`)
            // at each of its two fire paths → `lui v0,HI; lwc1 f12,LO(v0)` of Mailbox.XiaoShotWhpFactor (pnach-seeded 1.0 = vanilla;
            // ChargedShotWhp writes the charge's factor while the shot is held).
            new(XiaoShotWhpSiteA,     0x3C023F80, XiaoShotWhpPatchedWord0, "Xiao shot WHP factor → mailbox word (lui, path A)"),
            new(XiaoShotWhpSiteA + 4, 0x44826000, XiaoShotWhpPatchedWord1, "Xiao shot WHP factor → mailbox word (lwc1 f12, path A)"),
            new(XiaoShotWhpSiteB,     0x3C023F80, XiaoShotWhpPatchedWord0, "Xiao shot WHP factor → mailbox word (lui, path B)"),
            new(XiaoShotWhpSiteB + 4, 0x44826000, XiaoShotWhpPatchedWord1, "Xiao shot WHP factor → mailbox word (lwc1 f12, path B)"),
            // CHARACTER HEAP: the dungeon's character + weapons + shot-effect data share ONE CDataAlloc2 pool that
            // GameInit carves from the 27 MB global buffer as 210000 × 16 B = 3.36 MB, and an overflow is a silent
            // spin (Alloc__14CDataAlloc2: printf + while(true)). Vanilla Xiao already sits within ~150 KB of that
            // ceiling; the cat baked into her pack (build_cat_pack.py) needs ~300 KB more. The global buffer is FULL
            // by design (reset base 50,010 + GameInit's carves 1,256,006 + read buffer 280,000 + 2 × 37,700 packet
            // buffers + 25,000 ≈ 1,688,000 of 1,690,000 units — raising the heap alone black-screened the dungeon),
            // so the 30,000 units come out of the dungeon READ buffer (SetPacketReadBuffer(0x9344, 280000) at the
            // end of GameInit): 4.48 → 4.00 MB. Its real demand is bounded: the largest single dungeon file is a
            // 3.86 MB map pack, and the longest staged menu chain (Xiao's party switch: dunmenu5 + portraits + her
            // ~2.5 MB pack + weapons + effect) reaches ~3.9 MB. Measured 2026-09-10 (flat textures): Xiao+cat chara
            // 3,216,400 + weapons 227,168 + effects 70,128 = 3,513,696; the real cat textures add ~127,000.
            // 2026-09-13, the WINGS: the cat's wing bones/meshes/keys/textures cost another ~220 KB in this pool (chara
            // 3,400,336 → 3,620,496 measured), and the pool is shared with the weapons (~216 KB) and the floor's shot effects
            // (70-190 KB): 240000 units overflowed by 67-190 KB → a silent spin on the switch to Xiao. The allocator's own
            // counter (DivineBeastCat.HeapWatch, GlobalPoolUsed 0x21C74980) shows the global buffer at 26,616,000 of
            // 27,039,984 B in every log — 26,499 units unused (which is exactly why +30,000 alone black-screened and the
            // read-buffer cut was needed) — so the heap takes 20,000 more of them: 260000 units = 4.16 MB, leaving 6,499
            // units (104 KB) of global slack. The literal 210000 is `lui r,3; ori r,r,0x3450` at four sites (carve, the two
            // remaining-room computations, a memory-map printf): 260000 = 0x3F7A0 = ori 0xF7A0 (lui 3 unchanged). 280000 is
            // `lui v0,4; ori a1,v0,0x45C0` → 250000 = `lui v0,3; ori a1,v0,0xD090`.
            // 2026-09-14, the MASK: 260000 units was not enough for it. HeapWatch on the freeze read chara 3,905,424 of
            // 4,160,000 with weapons 225,232 and effects 29,296 — total 4,159,952, i.e. 48 BYTES free, and the effects pool
            // squeezed to a 29,344 cap against a real demand of 70-190 KB. So the heap takes 5,000 more units (80,000 B) out
            // of the global buffer, which the same log measured at 103,984 B free; 23,984 B of it is left. 265000 = 0x40B28
            // no longer fits the `ori` alone (260000 was 0x3F7A0, still lui 3), so each site's `lui r2,3` goes to 4 as well.
            new(0x01DAC0C0, 0x3C020003, 0x3C020004, "character heap → 265000 units (MemoryMapDump printf, lui)"),
            new(0x01DAC334, 0x3C020003, 0x3C020004, "character heap → 265000 units (GameInit carve, lui)"),
            new(0x01DB9A84, 0x3C020003, 0x3C020004, "character heap → 265000 units (LoadWeapon2 remaining, lui)"),
            new(0x01DBA8AC, 0x3C020003, 0x3C020004, "character heap → 265000 units (LoadChara2 remaining, lui)"),
            new(0x01DAC0C4, 0x34463450, 0x34460B28, "character heap 210000 → 265000 units (MemoryMapDump printf)"),
            new(0x01DAC338, 0x34453450, 0x34450B28, "character heap 210000 → 265000 units (GameInit carve)"),
            new(0x01DB9A88, 0x34433450, 0x34430B28, "character heap 210000 → 265000 units (LoadWeapon2 remaining)"),
            new(0x01DBA8B0, 0x34423450, 0x34420B28, "character heap 210000 → 265000 units (LoadChara2 remaining)"),
            new(0x01DAC460, 0x3C020004, 0x3C020003, "dungeon read buffer 280000 → 250000 units (lui)"),
            new(0x01DAC464, 0x344545C0, 0x3445D090, "dungeon read buffer 280000 → 250000 units (ori)"),
            // Divine Beast cat: the dungeon step loop's once-per-frame `jal step__5CSHOT` (a0 = player shot pool)
            // → the native pellet-follower cave, which performs that call and then pins the cat's chara slot to the
            // pellet the Mailbox names (ElfPatches.PatchCatPelletFollow writes the cave).
            new(CatFollowHookAddr, CatFollowHookOrig, CatFollowHookNew, "cat pellet follower hook (jal step__5CSHOT → cave)"),
            // Divine Beast cat glow: the draw loop's two torch passes → the glow cave's entries, which perform the pass
            // and then draw the cat's glow disc with the same routine (ElfPatches.PatchCatGlowDraw writes the cave).
            new(0x01DAEBF8, 0x0C071030, 0x0C000000u | (CodeCaves.ElfCave.CatGlowDrawEntryA >> 2), "cat glow hook A (jal DrawFire__11CDungeonMap → cave)"),
            new(0x01DAEC10, 0x0C070F30, 0x0C000000u | (CodeCaves.ElfCave.CatGlowDrawEntryB >> 2), "cat glow hook B (jal DrawFireFreeStyle → cave)"),
            // Mirage haze: the draw loop's raster pass → the haze cave, which performs the pass and then draws one raster at
            // the clone's root (ElfPatches.PatchMirageHazeDraw writes the cave).
            new(MirageHazeHookAddr, MirageHazeHookOrig, MirageHazeHookNew, "mirage haze hook (jal DrawRaster__11CDungeonMap → cave)"),
            // Super Steve's sphere icon: the HUD's status pass → the icon cave, which performs it and then draws the sphere
            // weapon's icon over Steve (ElfPatches.PatchSuperSteveIconDraw writes the cave).
            new(SsIconHookAddr, SsIconHookOrig, SsIconHookNew, "super steve icon hook (jal topStatusInfo → cave)"),
            // The loader's `jal MemoryMapDump` (dun 0x1DB9568) was the Gemron cave's first hook; the cave now sits at the head of
            // the per-frame chain instead, and an ISO patched with that first hook gets the vanilla word back.
            new(GemronLoadHookAddr, GemronLoadHookOld, GemronLoadHookVanilla, "gemron shots: the retired loader hook back to vanilla"),
            new(SsIconCopyHookAddr, SsIconCopyHookOrig, SsIconCopyHookNew, "super steve icon copy hook (jal DngActiveWeaponTextureCopy → cave)"),
            new(SsIconCopyHookAddr2, SsIconCopyHookOrig, SsIconCopyHookNew, "super steve icon copy hook 2 (the step path's jal DngActiveWeaponTextureCopy → cave)"),
        };

        /// <summary>The patched form of the first gauge word — what the runtime checks to know the patch is live.</summary>
        internal static uint GaugePatchedWord0 => Words[1].New;
        internal const long GaugePatchAddrMmu = 0x201DB8090;

        /// <summary>The cat follower hook site (dun step loop `jal step__5CSHOT`) — the runtime checks the word to
        /// know the native follower is live (DivineBeastCat falls back to its thread follower when it is not).</summary>
        internal const uint CatFollowHookAddr = 0x01DB874C;
        internal const uint CatFollowHookOrig = 0x0C06AF44;                                   // jal 0x1ABD10
        // …and it lands on GemronShotsEnter (Dragon's Y: keeps the Gemron shot config entered in the floor's pack), which
        // calls PropPelletFollow (the Matador's charged shot), which calls the COPY-QUEUE cave — that services the cat's mesh
        // copy when one is pending and jumps on to CatPelletFollow, where the displaced step__5CSHOT runs — then each places
        // its own thing. Every frame with nothing to do the chain reads a few zero words and falls straight through.
        internal const uint CatFollowHookNew  = 0x0C000000u | (CodeCaves.ElfCave.GemronShotsEnter >> 2);
        internal const long CatFollowHookAddrMmu = 0x20000000L + CatFollowHookAddr;

        internal const uint MirageHazeHookAddr = 0x01DAEBCC;
        internal const uint MirageHazeHookOrig = 0x0C071184;                                   // jal 0x1C4610 DrawRaster__11CDungeonMap
        internal const uint MirageHazeHookNew  = 0x0C000000u | (CodeCaves.ElfCave.MirageHazeDraw >> 2);
        internal const long MirageHazeHookAddrMmu = 0x20000000L + MirageHazeHookAddr;

        internal const uint XiaoShotWhpSiteA = 0x01DBCC58, XiaoShotWhpSiteB = 0x01DBCDD0;   // the two `lui v0,0x3f80` feeding SwordDmgCheck1 in BattleActionPlay_Jinn
        internal const uint XiaoShotWhpPatchedWord0 = 0x3C020000u | (uint)((CodeCaves.Mailbox.XiaoShotWhpFactor - 0x20000000) >> 16);
        internal const uint XiaoShotWhpPatchedWord1 = 0xC44C0000u | (uint)((CodeCaves.Mailbox.XiaoShotWhpFactor - 0x20000000) & 0xFFFF);
        internal const long XiaoShotWhpPatchAddrMmu = 0x20000000L + XiaoShotWhpSiteA;
        internal const uint GemronLoadHookAddr    = 0x01DB9568;                          // OpB_InitProcess: jal MemoryMapDump after the species loop
        internal const uint GemronLoadHookVanilla = 0x0C76B01C;                          // jal 0x1DAC070
        internal const uint GemronLoadHookOld     = 0x0C000000u | (CodeCaves.ElfCave.GemronShotsEnter >> 2);
        internal const uint HealCadenceAddr = 0x01DB8234;                                 // the heal tick's `slti v0,v0,THRESHOLD`: low half = the period in frames
        internal const uint HealCadenceOrig = 0x284200F0;
        internal const uint HealCadenceNew  = 0x284200B4;
        internal const long HealCadenceAddrMmu = 0x20000000L + HealCadenceAddr;
        internal const uint SsIconHookAddr = 0x01DB0364;
        internal const uint SsIconHookOrig = 0x0C06C13C;                                   // jal 0x1B04F0 topStatusInfo
        internal const uint SsIconHookNew  = 0x0C000000u | (CodeCaves.ElfCave.SuperSteveIconDraw >> 2);
        internal const long SsIconHookAddrMmu = 0x20000000L + SsIconHookAddr;
        internal const uint SsIconCopyHookAddr = 0x01DAE608;                               // the overlay's two jal DngActiveWeaponTextureCopy sites
        internal const uint SsIconCopyHookAddr2 = 0x01DAE36C;                              //   (the second sits beside the item copy in the step path)
        internal const uint SsIconCopyHookOrig = 0x0C08A9AC;                               // jal 0x22A6B0
        internal const uint SsIconCopyHookNew  = 0x0C000000u | (CodeCaves.ElfCave.SuperSteveIconCopy >> 2);

        internal static void Apply(FileStream fs, Rec dun, Action<string> progress)
        {
            long baseOff = (long)dun.Ext * SectorBytes;
            int applied = 0;
            foreach (var w in Words)
            {
                uint rel = w.Addr - LoadBase;
                if (rel + 4 > dun.Size) throw new IOException($"dun.bin patch 0x{w.Addr:X} lies past the file ({w.What}).");
                long off = baseOff + rel;
                uint cur = RdU32(fs, off);
                if (cur == w.New) continue;                                   // already ours (re-patching a patched ISO)
                if (cur != w.Orig)
                    throw new IOException($"dun.bin @0x{w.Addr:X} is not vanilla ({cur:X8}, expected {w.Orig:X8}) — {w.What}. Unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, off, w.New);
                applied++;
            }
            progress($"Patched dun.bin ({applied} word(s): heal cadence 3 s, gauge multiplier → cave word, character heap 3.36 → 4.16 MB (read buffer + global slack), cat pellet-follower hook, cat glow hooks) …");
        }
    }
}
