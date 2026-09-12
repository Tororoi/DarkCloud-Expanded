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
            // (user 2026-09-09). Guardian Grace watches the counter wrap, so its sparkles/chime follow.
            new(0x01DB8234, 0x284200F0, 0x284200B4, "heal-ability cadence 4 s → 3 s"),
            // Xiao's attack-gauge refill multiplier (motionDrive: `lui v0,0x3fc0; mtc1 v0,f0`, v0 dead after)
            // → `lui v0,HI; lwc1 f0,LO(v0)` of Mailbox.ShieldGaugeRate (pnach-seeded 1.5 = vanilla while idle).
            new(0x01DB8090, 0x3C023FC0, 0x3C020000u | (uint)((CodeCaves.Mailbox.ShieldGaugeRate - 0x20000000) >> 16),    "gauge refill multiplier → mailbox word (lui)"),
            new(0x01DB8094, 0x44820000, 0xC4400000u | (uint)((CodeCaves.Mailbox.ShieldGaugeRate - 0x20000000) & 0xFFFF), "gauge refill multiplier → mailbox word (lwc1 f0)"),
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
            // The literal 210000 is `lui r,3; ori r,r,0x3450` at four sites (carve, the two remaining-room
            // computations, a memory-map printf): 240000 = ori 0xA980. 280000 is `lui v0,4; ori a1,v0,0x45C0` →
            // 250000 = `lui v0,3; ori a1,v0,0xD090`.
            new(0x01DAC0C4, 0x34463450, 0x3446A980, "character heap 210000 → 240000 units (MemoryMapDump printf)"),
            new(0x01DAC338, 0x34453450, 0x3445A980, "character heap 210000 → 240000 units (GameInit carve)"),
            new(0x01DB9A88, 0x34433450, 0x3443A980, "character heap 210000 → 240000 units (LoadWeapon2 remaining)"),
            new(0x01DBA8B0, 0x34423450, 0x3442A980, "character heap 210000 → 240000 units (LoadChara2 remaining)"),
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
        };

        /// <summary>The patched form of the first gauge word — what the runtime checks to know the patch is live.</summary>
        internal static uint GaugePatchedWord0 => Words[1].New;
        internal const long GaugePatchAddrMmu = 0x201DB8090;

        /// <summary>The cat follower hook site (dun step loop `jal step__5CSHOT`) — the runtime checks the word to
        /// know the native follower is live (DivineBeastCat falls back to its thread follower when it is not).</summary>
        internal const uint CatFollowHookAddr = 0x01DB874C;
        internal const uint CatFollowHookOrig = 0x0C06AF44;                                   // jal 0x1ABD10
        internal const uint CatFollowHookNew  = 0x0C000000u | (CodeCaves.ElfCave.CatPelletFollow >> 2);
        internal const long CatFollowHookAddrMmu = 0x20000000L + CatFollowHookAddr;

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
            progress($"Patched dun.bin ({applied} word(s): heal cadence 3 s, gauge multiplier → cave word, character heap 3.36 → 3.84 MB from the read buffer, cat pellet-follower hook, cat glow hooks) …");
        }
    }
}
