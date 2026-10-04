using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The element glow disc the character bakes share: the Gallery of Time's torch glow (`lightling`, a 64×64 RGBA32 TIM2 in
    /// d06main_a.mpd's fire.img) re-coloured as a radial gradient and rebuilt as an 8-bit disc whose CLUT a cave repaints. The GS reads
    /// a PSMT8 palette in CSM1 order (bits 3 and 4 of the index exchanged) and nothing de-swizzles it, so the disc only uses the 128
    /// indices the permutation leaves alone (bits 3 and 4 equal) and the cave copies its table straight down. CatPackBakes bakes the
    /// cat's disc (resting on the "None" ramp) and refuses a stale palette blob; ToanGlowBakes bakes Toan's three discs on the gold,
    /// blue and Zeus ramps; ElfCatPatches.PatchCatGlowPalettes writes the nine palettes into the ELF.</summary>
    internal static class GlowDisc
    {
        internal const string SourcePack = @"dun\mpd_pack\d06main_a.mpd";       // the Gallery of Time map pack: its fire.img holds the purple torch glow disc
        internal const string SourcePicture = "lightling";
        private static readonly (int, int, int) CatCore = (15, 219, 255), CatOuter = (60, 67, 255);
        private const double Cross = 0.125;                                      // radius fraction where the mix is halfway (smaller = the outer colour reaches further in)
        // Per-weapon looks are palette ROWS of the same 8-bit disc: rows 6-8 after the six elements.
        private static readonly ((int, int, int) core, (int, int, int) outer)[] Looks =
        {
            (CatCore, CatOuter),                            // 6 Divine Beast Title — the authored blue
            ((215, 215, 215), (180, 190, 215)),             // 7 Angel Shooter — white, a cool edge
            ((255, 238, 180), (255, 176, 40)),              // 8 Angel Gear — gold
        };
        internal static readonly int[] ClutFixed = Enumerable.Range(0, 256).Where(i => (i & 0x18) == 0x00 || (i & 0x18) == 0x18).ToArray();
        // Per element (00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy, 05 None): the radial gradient's centre and edge. The OUTER colour is what
        // mostly shows: Cross 0.125 puts the mix halfway at an eighth of the radius. "None" is the dimmed white the Angel Shooter wears.
        internal static readonly ((int, int, int) core, (int, int, int) outer)[] Elements =
        {
            ((128, 118, 52), (200, 5, 0)),                  // Fire     orange
            ((0, 200, 215), (0, 4, 183)),                   // Ice      blue
            ((255, 248, 190), (156, 131, 43)),              // Thunder  yellow
            ((128, 255, 113), (0, 86, 126)),                // Wind     green
            ((211, 73, 236), (33, 0, 175)),                 // Holy     purple
            ((50, 50, 50), (160, 160, 160)),                // None     the dimmed white
        };
        /// <summary>Row 8's ramp — the Angel Gear cat's GOLD: a pale gold core out to a deeper gold edge. Toan's own glow
        /// disc rests on it (ToanGlowBakes).</summary>
        internal static ((int, int, int) core, (int, int, int) outer) Gold => Looks[2];
        /// <summary>The Sword of Zeus's ring: a BLACK core (nothing, drawn additively) out to a deep red edge, so the disc reads
        /// as a ring around the judgement blade. Not a cave row — Toan's disc alone rests on it (ToanGlowBakes).</summary>
        internal static readonly ((int, int, int) core, (int, int, int) outer) Zeus = ((0, 0, 0), (200, 4, 0));
        /// <summary>Row 6's ramp — the Divine Beast Title cat's BLUE: a pale cyan core out to a deep blue edge. Big Bang's
        /// glow disc rests on it (ToanGlowBakes).</summary>
        internal static ((int, int, int) core, (int, int, int) outer) Blue => Looks[0];
        private static ((int, int, int) core, (int, int, int) outer)[] Rows => Elements.Concat(Looks).ToArray();   // the cave's table, in row order

        /// <summary>The torch glow disc (a 64×64 RGBA32 TIM2, no CLUT) re-coloured as a radial gradient: every pixel keeps its alpha and its
        /// share of the disc's peak luminance, and its hue runs from `core` at the centre to `outer` at the disc's visible edge.</summary>
        private static byte[] Tim2(byte[] lightling, (int, int, int) core, (int, int, int) outer)
        {
            uint cs = IsoBytes.U32(lightling, 0x14), isz = IsoBytes.U32(lightling, 0x18); int hs = IsoBytes.U16(lightling, 0x1C);
            int it = lightling[0x23], w = IsoBytes.U16(lightling, 0x24), h = IsoBytes.U16(lightling, 0x26);
            if (it != 3 || cs != 0) throw new IOException($"glow source is not a 32-bit TIM2 (type {it}, clut {cs})");
            var px = lightling.AsSpan(0x10 + hs, (int)isz).ToArray();
            var lums = new double[px.Length / 4];
            for (int k = 0; k < px.Length; k += 4) lums[k / 4] = 0.30 * px[k] + 0.59 * px[k + 1] + 0.11 * px[k + 2];
            double peak = lums.Max(); if (peak == 0) peak = 1.0;
            double cx = (w - 1) / 2.0, cy = (h - 1) / 2.0;
            double edge = 0; bool any = false;
            for (int i = 0; i < lums.Length; i++)
                if (lums[i] > peak * 0.02) { double d = Math.Pow(Math.Pow((i % w) - cx, 2) + Math.Pow((i / w) - cy, 2), 0.5); if (!any || d > edge) { edge = d; any = true; } }
            if (!any || edge == 0) edge = 1.0;
            double expo = Math.Log(0.5) / Math.Log(Cross);
            var (c0, c1, c2) = core; var (o0, o1, o2) = outer; int[] cc = { c0, c1, c2 }, oo = { o0, o1, o2 };
            for (int i = 0; i < w * h; i++)
            {
                int k = i * 4; double l = lums[i] / peak;
                double t = Math.Min(1.0, Math.Pow(Math.Pow((i % w) - cx, 2) + Math.Pow((i / w) - cy, 2), 0.5) / edge);
                t = Math.Pow(t, expo);
                for (int c = 0; c < 3; c++) px[k + c] = (byte)Math.Min(255, (int)((cc[c] * (1.0 - t) + oo[c] * t) * l));
            }
            var outp = (byte[])lightling.Clone();
            Array.Copy(px, 0, outp, 0x10 + hs, px.Length);
            return outp;
        }

        /// <summary>The source disc's luminance per texel, and its sorted distinct levels: luminance falls monotonically from the centre, so it
        /// stands in for radius and ONE baked index map serves every colour.</summary>
        private static (int[] key, List<int> levels) Levels(byte[] lightling)
        {
            int hs = IsoBytes.U16(lightling, 0x1C); int isz = (int)IsoBytes.U32(lightling, 0x18);
            var key = new int[isz / 4];
            for (int i = 0; i < isz; i += 4) key[i / 4] = (lightling[0x10 + hs + i] * 299 + lightling[0x10 + hs + i + 1] * 587 + lightling[0x10 + hs + i + 2] * 114) / 1000;
            return (key, key.Distinct().OrderBy(v => v).ToList());
        }

        /// <summary>One byte per texel: the pixel's luminance RANK, mapped onto a permutation-safe CLUT index.</summary>
        private static byte[] Indices(byte[] lightling)
        {
            var (key, levels) = Levels(lightling);
            if (levels.Count > ClutFixed.Length) throw new IOException($"glow disc needs {levels.Count} palette entries; only {ClutFixed.Length} are permutation-safe");
            var slot = new Dictionary<int, int>(); for (int i = 0; i < levels.Count; i++) slot[levels[i]] = ClutFixed[i];
            return key.Select(k => (byte)slot[k]).ToArray();
        }

        /// <summary>The 128 permutation-safe CLUT words for ONE colour, in ascending index order — the 512 B the cave copies. Each word is the
        /// mean of what the 32-bit disc would hold for the texels at that luminance.</summary>
        private static byte[] Palette(byte[] lightling, (int, int, int) core, (int, int, int) outer)
        {
            var (key, levels) = Levels(lightling);
            int hs = IsoBytes.U16(lightling, 0x1C); int isz = (int)IsoBytes.U32(lightling, 0x18);
            var px = Tim2(lightling, core, outer).AsSpan(0x10 + hs, isz).ToArray();
            var acc = new Dictionary<int, long[]>();
            for (int i = 0; i < key.Length; i++)
            {
                if (!acc.TryGetValue(key[i], out var a)) acc[key[i]] = a = new long[5];
                for (int c = 0; c < 4; c++) a[c] += px[i * 4 + c];
                a[4]++;
            }
            var outp = new byte[ClutFixed.Length * 4];
            for (int r = 0; r < levels.Count; r++) { var a = acc[levels[r]]; for (int c = 0; c < 4; c++) outp[r * 4 + c] = (byte)(a[c] / a[4]); }
            return outp;
        }

        /// <summary>All nine palettes back to back — the blob the mod embeds (Resources/isoPatch/catGlowPalettes.bin) and writes into the ELF's
        /// table cave. The cave tells "already painted" from ONE word, the brightest level (table slot 114, CLUT index 226 — offsets baked into
        /// the palette stub), so both facts are asserted here.</summary>
        internal static byte[] Palettes(byte[] lightling)
        {
            var (_, levels) = Levels(lightling);
            int last = levels.Count - 1;
            if (last != 114 || ClutFixed[last] != 226)
                throw new IOException($"the glow disc now has {levels.Count} levels (brightest at CLUT index {ClutFixed[last]}) — update the state-check offsets in the palette stub");
            var tabs = Rows.Select(r => Palette(lightling, r.core, r.outer)).ToList();
            if (tabs.Select(t => BitConverter.ToUInt32(t, last * 4)).Distinct().Count() != tabs.Count)
                throw new IOException("two glow ramps end on the same brightest colour — the cave could not tell those looks apart; tune a core");
            return tabs.SelectMany(t => t).ToArray();
        }

        /// <summary>The glow disc as an 8-bit TIM2 (64×64 indices + a 256-entry CLUT) built off a vanilla 8-bit picture's headers
        /// (<paramref name="template"/>, through <see cref="Tim8.Build8"/>). The CLUT baked here is only the resting look on
        /// <paramref name="core"/> → <paramref name="outer"/>; the cave repaints it per element.</summary>
        internal static byte[] BuildT8(byte[] template, byte[] lightling, (int, int, int) core, (int, int, int) outer)
        {
            int w = IsoBytes.U16(lightling, 0x24), h = IsoBytes.U16(lightling, 0x26);
            var idx = Indices(lightling);
            if (w != 64 || h != 64 || idx.Length != w * h) throw new IOException($"glow_t8_tim2: source disc is {w}x{h} — DrawFire's texel rect is hardcoded to 64x64");
            var pal = new byte[256 * 4]; var tab = Palette(lightling, core, outer);
            for (int r = 0; r < ClutFixed.Length; r++) Array.Copy(tab, r * 4, pal, ClutFixed[r] * 4, 4);
            return Tim8.Build8(template, w, h, idx, pal, "glow_t8_tim2");
        }
    }
}
