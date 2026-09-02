using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// THE Brownboo stilts fix (GS-dump session, 2026-09-01 — memory fishing-stilts-texture-block).
    ///
    /// Root cause (proven from PCSX2 GS dumps): the fishing textures (CTextureManager blocks 0x36/0x37)
    /// upload to GS base 0x1A40 several times per frame, overwriting the head of the town's big 256x256
    /// RGBA scene texture that ALSO lives at 0x1A40 — and whose own re-upload happens once, EARLY in the
    /// frame, before the fish uploads land. Its draws (the boardwalk stilts) then sample fish texels.
    /// The game's dirty-tracking re-uploads only the T8 atlases after a clobber, never this texture.
    ///
    /// Why every ELF/cave fix failed: the fish blocks get 0x1A40 from BeginEnterTextureBlock (0x133580),
    /// which assigns the block's GS base at +0x38 ONLY IF THE FIELD IS ZERO (`li v1,0x1A40` @0x1335BC).
    /// A pre-set non-zero base is KEPT — so the fix is one data write per block, made before the fishing
    /// load registers them: park the fish blocks in the GS region the dumps proved free (0x3BC0..0x3FE0;
    /// nothing in walking/fishing/menu frames uploads or samples there). Uploads AND texture binds both
    /// derive from the same field, so everything follows. (The July 2026 "+0x38 override did nothing"
    /// attempt wrote mgr+0x18+N*0x3C+0x38 — the block array actually starts at mgr+0, so that write hit
    /// the RAM-end field at +0x50. Off by the 0x18 header.)
    ///
    /// The write is normalizing and race-safe: only values 0 (cleared) or 0x1A40 (the colliding default)
    /// are replaced, every tick while in Brownboo — so a session that already registered at the parked
    /// address is never disturbed, and a town-load clear re-arms us before the next fishing load.
    /// </summary>
    internal static class BrownbooTextureRelocation
    {
        internal static bool Enabled = true;

        private const long Manager   = 0x21C75870;             // CTextureManager instance
        private const int  BlockSize = 0x3C;                   // per-block stride (fields: base +0x38, end +0x3C)
        // ROOT CAUSE (settled 2026-09-01 by full GS-dump timelines + DATA.DAT byte-matching): the town scene
        // atlas (block 0x01, drawn from GS 0x1A40..0x1E40; its per-frame upload is byte-identical to the
        // s04b02.img TIM2 @+0x50E30 — the CONTENT was always correct) is uploaded at frame HEAD and sampled
        // by scene draws all frame long. During fishing, per-use SCRATCH blocks still based at the 0x1A40
        // default write 64x64 tiles + CLUTs into that span MID-frame (fish texture from chara\f00s.chr, rod,
        // and fishing sub-blocks 0x38..0x3B, log-proven live at 0x1A40..0x1C80) — every scene draw after
        // those writes samples fish/rod texels: the yellow striped posts. Walking is clean only because the
        // fishing scratch blocks are silent then.
        //
        // RETIRED (2026-09-02): every park layout is withdrawn. The dump-pair analysis proved the
        // clobbering uploads (fishing player model c01d_turi + rod/bobber) are PACKET-BAKED into model
        // VIF data — they never route through block descriptors, which is why no park ever changed the
        // garble ("no change ever"). The real fix is the ISO-baked stilts-heal cave
        // (ElfFishingPatches.PatchStiltsHeal): re-upload scene bank 1 after FishLineDraw, before the
        // waterside redraw that paints the posts. This class survives only as LEGACY CLEANUP — it clears
        // any parked base left behind by an older build so the texture system runs fully vanilla.
        private static readonly (int block, uint gsBase)[] Parks = { };

        /// <summary>Every base any retired park layout ever wrote. A game that ran an old build still
        /// holds them; clearing to 0 makes the engine re-default to 0x1A40 (vanilla) on the next
        /// enter.</summary>
        private static readonly uint[] LegacyBases =
            { 0x2C00, 0x3000, 0x3100, 0x3200, 0x3300, 0x3400, 0x3BC0, 0x3E00, 0x3F00 };

        /// <summary>Blocks any retired layout ever parked.</summary>
        private static readonly int[] LegacyBlocks =
            { 0x02, 0x03, 0x07, 0x08, 0x14, 0x1B, 0x1C, 0x1D, 0x1E, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C };

        internal static void Tick()
        {
            if (!Enabled) return;
            // Legacy cleanup only, every town: clear any parked base an older build left behind so the
            // engine's BeginEnterTextureBlock re-defaults everything to the vanilla 0x1A40 scratch on
            // the next enter.
            foreach (int block in LegacyBlocks) CleanupLegacy(block);
        }

        private static void CleanupLegacy(int block)
        {
            long b = Manager + block * BlockSize;
            uint cur = Memory.ReadUInt(b + 0x38);
            if (Array.IndexOf(LegacyBases, cur) < 0) return;
            Memory.WriteUInt(b + 0x38, 0);
            Memory.WriteUInt(b + 0x3C, 0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[StiltsFix] cleared legacy park 0x{cur:X} on block 0x{block:X2}");
        }

    }
}
