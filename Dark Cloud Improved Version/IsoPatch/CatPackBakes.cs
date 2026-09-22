using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Divine Beast Title "cat shot": Xiao's cat rig baked INTO her dungeon character pack (dun\mainchara\c04b.chr, the
    /// pack every dungeon loads while she is active). The cat rides along as extra nodes in her frame ARRAY that are never
    /// linked into her tree (parent −1: no draw, skinning or DMA while hidden), plus a SECOND motion channel (MOTION 1,
    /// KEY_START 64). Her own motion ids stop at 45, so the cat channel never poses her, and its tracks use bone indices
    /// RELATIVE to the cat root: the runtime copies just the cat subtree and plays the channel on the copy, where the cat root
    /// IS node 0. Not the weapon pack: the weapon menu rebuilds every carried weapon into a 944 KB arena, and a bigger weapon
    /// pack overflowed it. On top go Dran's wings, the Super Steve cape and the mask (<see cref="WingBake"/>), a flat white
    /// wing texture, the flat cape texture, and the 8-bit element glow disc. Everything is read from the player's own ISO.</summary>
    internal static class CatPackBakes
    {
        internal const string HostChr = @"dun\mainchara\c04b.chr";
        internal const string HostCfg = "base.cfg", HostMds = "c04b.mds", HostBbp = "c04b.bbp", HostImg = "c04b01.img";
        internal const string CatChr = @"gedit\s86\chara\c04cat.chr";
        internal const string FloatChr = @"gedit\e01\chara\e04c04cat.chr";        // the town cat's vertical float/hop-up (its #5 clip, frames 160..169)
        private static readonly (uint lo, uint hi) FloatSrc = (160, 169), FloatDst = (285, 294);   // grafted into cat.mot where s86 has no keys
        internal const string GlowSrc = @"dun\mpd_pack\d06main_a.mpd";            // the Gallery of Time map pack: its fire.img holds the purple torch glow disc
        private static readonly (int, int, int) GlowCore = (15, 219, 255), GlowOuter = (60, 67, 255);
        private const double GlowCross = 0.125;                                    // radius fraction where the mix is halfway (smaller = the outer colour reaches further in)
        // Per-weapon looks are palette ROWS of the same 8-bit disc: rows 6-8 after the six elements.
        private static readonly ((int, int, int) core, (int, int, int) outer)[] GlowLooks =
        {
            (GlowCore, GlowOuter),                          // 6 Divine Beast Title — the authored blue
            ((215, 215, 215), (180, 190, 215)),             // 7 Angel Shooter — white, a cool edge
            ((255, 238, 180), (255, 176, 40)),              // 8 Angel Gear — gold
        };
        /// <summary>The per-ELEMENT glow: ONE 8-bit disc (5,184 B) whose CLUT the cave repaints. The GS reads a PSMT8 palette in CSM1
        /// order (bits 3 and 4 of the index exchanged) and nothing de-swizzles it, so the disc only uses the 128 indices the
        /// permutation leaves alone (bits 3 and 4 equal) and the cave copies its table straight down.</summary>
        internal const string GlowT8Name = "catglowp";
        internal static readonly int[] ClutFixed = Enumerable.Range(0, 256).Where(i => (i & 0x18) == 0x00 || (i & 0x18) == 0x18).ToArray();
        // Per element (00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy, 05 None): the radial gradient's centre and edge. The OUTER colour is what
        // mostly shows: GlowCross 0.125 puts the mix halfway at an eighth of the radius. "None" is the dimmed white the Angel Shooter wears.
        internal static readonly ((int, int, int) core, (int, int, int) outer)[] GlowElements =
        {
            ((128, 118, 52), (200, 5, 0)),                  // Fire     orange
            ((0, 200, 215), (0, 4, 183)),                   // Ice      blue
            ((255, 248, 190), (156, 131, 43)),              // Thunder  yellow
            ((128, 255, 113), (0, 86, 126)),                // Wind     green
            ((211, 73, 236), (33, 0, 175)),                 // Holy     purple
            ((50, 50, 50), (160, 160, 160)),                // None     the dimmed white
        };
        /// <summary>Row 7's ramp — the Angel Shooter's white. Toan's own glow disc rests on it (ToanGlowBakes).</summary>
        internal static ((int, int, int) core, (int, int, int) outer) GlowWhite => GlowLooks[1];
        private static ((int, int, int) core, (int, int, int) outer)[] GlowRows => GlowElements.Concat(GlowLooks).ToArray();   // the cave's table, in row order
        internal const string CapeCloName = "catcape.clo";
        internal const string DranChr = @"dun\monstor\c12a.chr";                   // the wing donor
        private static readonly byte[] WingRgba = { 255, 255, 255, 0x80 };          // the wings' flat texture: solid white, GS alpha 0x80 = opaque
        private static readonly byte[] CapeRgba = { 128, 28, 0, 0x80 };             // the cape's flat texture; the colour on screen comes from the ambient the runtime gives the cloth
        internal const string NodePrefix = "cat_";                                  // every cat bone (her rig already carries `kao`, `skin`, …)
        internal const string CatRootName = "catroot";                              // what the runtime looks for in her tree
        internal const string CatSkinName = NodePrefix + "skin";
        internal const double HideScale = 0.001;                                    // bind 3×3 of the cat root on HER (the copy restores 1.0)
        private const int CatParent = -1;                                           // UNPARENTED: LoadMDSFile calls SetParent(frame, NULL) for parent < 0
        private const string VersionMark = "//catpack v18 wings+cape";
        internal const int KeyStart = 64;                                           // cat channel key ids 64.. (her own ids end at 45)
        internal static readonly (int s, int e, double sp, string cm)[] CatKeys =    // s86 c04cat windows; ids = KeyStart + index
        {
            (10, 20, 0.1, "cat stand"), (95, 105, 0.4, "cat ready"), (120, 136, 0.5, "cat run"), (190, 204, 0.5, "cat take-off"),
            (205, 214, 0.5, "cat leap"), (215, 227, 0.36, "cat land"), (60, 80, 1.0, "cat walk (s86 KEY 2, brisk)"),
            (285, 294, 0.6, "cat float-up (e04c04cat #5 160..169)"),               // 71: the vertical leap, as the town ladder jump
            (30, 40, 0.1, "cat sit (s86 KEY 1)"),                                    // 72: in place when there is no enemy to go for
        };
        // The vanilla DATA.HD2 records (USA disc): an ISO patched by the earlier weapon-pack version is put back before the bake.
        private static readonly (uint, uint, uint, uint) HostVanilla = (0x195D5800, 0x218220, 0x32BAB, 0x431);
        private static readonly (string name, (uint, uint, uint, uint) rec)[] WeaponRevert =
        {
            (@"commenu\weapon\c04w09.chr", (0x17275000, 0x90E0, 0x2E4EA, 0x13)),
            (@"commenu\c04wtes.chr", (0x168A800, 0x8A950, 0x2D15, 0x116)),
            (@"dun\mainchara\c04w.pac", (0x1985B800, 0x641A0, 0x330B7, 0xC9)),
        };

        // ───────────────────────────── pack helpers ─────────────────────────────
        private static List<(string name, int meshOff, int parent)> MdsNodes(byte[] pl)
        {
            int cnt = (int)IsoBytes.U32(pl, 8);
            var outp = new List<(string, int, int)>();
            for (int i = 0; i < cnt; i++) { int p = 0x18 + i * 0x70; outp.Add((IsoBytes.NameAt(pl, p, 0x20), (int)IsoBytes.U32(pl, p + 0x20), BitConverter.ToInt32(pl, p + 0x24))); }
            return outp;
        }

        /// <summary>A texture bank with the IM2 table layout under either magic (`IMG\0` here): 0x10 header, 0x30 entries (name, offset).</summary>
        internal sealed class Bank
        {
            private const int Hdr = 0x10, Ent = 0x30;
            internal readonly byte[] Magic, Data; internal readonly List<(string name, int off)> Entries = new();
            internal Bank(byte[] data)
            {
                string m = Encoding.Latin1.GetString(data, 0, 4);
                if (m != "IMG\0" && m != "IM2\0") throw new IOException("not an IMG/IM2 bank");
                Magic = data.AsSpan(0, 4).ToArray(); Data = data;
                int count = (int)IsoBytes.U32(data, 4);
                for (int i = 0; i < count; i++) { int e = Hdr + i * Ent; Entries.Add((IsoBytes.NameAt(data, e, 0x20), (int)IsoBytes.U32(data, e + 0x20))); }
            }
            internal byte[] Block(string name)
            {
                var offs = Entries.Select(x => x.off).OrderBy(o => o).ToList();
                foreach (var (n, o) in Entries)
                    if (n == name) { int nxt = offs.FirstOrDefault(x => x > o, Data.Length); return Data.AsSpan(o, nxt - o).ToArray(); }
                throw new KeyNotFoundException(name);
            }
            internal static byte[] Build(byte[] magic, List<(string name, byte[] blob)> items)
            {
                var outp = new List<byte>();
                outp.AddRange(magic); outp.AddRange(BitConverter.GetBytes(items.Count)); outp.AddRange(new byte[8]); outp.AddRange(new byte[items.Count * Ent]);
                for (int i = 0; i < items.Count; i++)
                {
                    int e = Hdr + i * Ent;
                    var nb = Encoding.Latin1.GetBytes(items[i].name); int len = Math.Min(nb.Length, 0x1F);
                    for (int k = 0; k < len; k++) outp[e + k] = nb[k];
                    var ob = BitConverter.GetBytes(outp.Count); for (int k = 0; k < 4; k++) outp[e + 0x20 + k] = ob[k];
                    outp.AddRange(items[i].blob);
                    outp.AddRange(new byte[PyMath.Mod(-outp.Count, 16)]);
                }
                return outp.ToArray();
            }
        }

        private static (int bpp, int hdr, int w, int h) Tim2Info(byte[] data)
        {
            if (Encoding.ASCII.GetString(data, 0, 4) != "TIM2") throw new IOException("not a TIM2 block");
            const int pic = 0x10;
            return (data[pic + 0x13], IsoBytes.U16(data, pic + 0x0C), IsoBytes.U16(data, pic + 0x14), IsoBytes.U16(data, pic + 0x16));
        }

        /// <summary>A size×size 8-bit TIM2 whose 256-colour CLUT is all `rgba`, built from a vanilla 8-bit picture's headers. GsTex0 /
        /// GsTex1 / GsRegs / GsTexClut stay ZERO like every vanilla picture: the engine derives the GS register values itself at load.</summary>
        private static byte[] FlatTim2(byte[] template, byte[] rgba, int size = 32)
        {
            var info = Tim2Info(template);
            if (info.bpp != 5 || info.hdr != 0x30) throw new IOException("flat_tim2: template is not an 8-bit TIM2 with a 0x30 picture header");
            const int pic = 0x10;
            var hdr = template.AsSpan(0, pic + 0x30).ToArray();
            int imgSz = size * size, clutSz = 256 * 4;
            IsoBytes.U32(hdr, pic, (uint)(0x30 + imgSz + clutSz)); IsoBytes.U32(hdr, pic + 4, (uint)clutSz); IsoBytes.U32(hdr, pic + 8, (uint)imgSz);
            IsoBytes.U16(hdr, pic + 0x14, (ushort)size); IsoBytes.U16(hdr, pic + 0x16, (ushort)size);
            Array.Clear(hdr, pic + 0x18, 0x18);
            var outp = new byte[hdr.Length + imgSz + clutSz];
            Array.Copy(hdr, outp, hdr.Length);
            for (int i = 0; i < 256; i++) Array.Copy(rgba, 0, outp, hdr.Length + imgSz + i * 4, 4);
            return outp;
        }

        // ───────────────────────────── the glow disc ─────────────────────────────
        /// <summary>The torch glow disc (a 64×64 RGBA32 TIM2, no CLUT) re-coloured as a radial gradient: every pixel keeps its alpha and its
        /// share of the disc's peak luminance, and its hue runs from `core` at the centre to `outer` at the disc's visible edge.</summary>
        private static byte[] GlowTim2(byte[] lightling, (int, int, int) core, (int, int, int) outer)
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
            double expo = Math.Log(0.5) / Math.Log(GlowCross);
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
        private static (int[] key, List<int> levels) GlowLevels(byte[] lightling)
        {
            int hs = IsoBytes.U16(lightling, 0x1C); int isz = (int)IsoBytes.U32(lightling, 0x18);
            var key = new int[isz / 4];
            for (int i = 0; i < isz; i += 4) key[i / 4] = (lightling[0x10 + hs + i] * 299 + lightling[0x10 + hs + i + 1] * 587 + lightling[0x10 + hs + i + 2] * 114) / 1000;
            return (key, key.Distinct().OrderBy(v => v).ToList());
        }

        /// <summary>One byte per texel: the pixel's luminance RANK, mapped onto a permutation-safe CLUT index.</summary>
        private static byte[] GlowIndices(byte[] lightling)
        {
            var (key, levels) = GlowLevels(lightling);
            if (levels.Count > ClutFixed.Length) throw new IOException($"glow disc needs {levels.Count} palette entries; only {ClutFixed.Length} are permutation-safe");
            var slot = new Dictionary<int, int>(); for (int i = 0; i < levels.Count; i++) slot[levels[i]] = ClutFixed[i];
            return key.Select(k => (byte)slot[k]).ToArray();
        }

        /// <summary>The 128 permutation-safe CLUT words for ONE colour, in ascending index order — the 512 B the cave copies. Each word is the
        /// mean of what the 32-bit disc would hold for the texels at that luminance.</summary>
        private static byte[] GlowPalette(byte[] lightling, (int, int, int) core, (int, int, int) outer)
        {
            var (key, levels) = GlowLevels(lightling);
            int hs = IsoBytes.U16(lightling, 0x1C); int isz = (int)IsoBytes.U32(lightling, 0x18);
            var px = GlowTim2(lightling, core, outer).AsSpan(0x10 + hs, isz).ToArray();
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
        internal static byte[] GlowPalettes(byte[] lightling)
        {
            var (_, levels) = GlowLevels(lightling);
            int last = levels.Count - 1;
            if (last != 114 || ClutFixed[last] != 226)
                throw new IOException($"the glow disc now has {levels.Count} levels (brightest at CLUT index {ClutFixed[last]}) — update the state-check offsets in the palette stub");
            var tabs = GlowRows.Select(r => GlowPalette(lightling, r.core, r.outer)).ToList();
            if (tabs.Select(t => BitConverter.ToUInt32(t, last * 4)).Distinct().Count() != tabs.Count)
                throw new IOException("two glow ramps end on the same brightest colour — the cave could not tell those looks apart; tune a core");
            return tabs.SelectMany(t => t).ToArray();
        }

        /// <summary>The per-element glow disc as an 8-bit TIM2 (64×64 indices + a 256-entry CLUT) built off a vanilla 8-bit picture's headers. The
        /// CLUT baked here is only the resting look; the cave repaints it per element.</summary>
        internal static byte[] GlowT8Tim2(byte[] template, byte[] lightling, (int, int, int) core, (int, int, int) outer)
        {
            var info = Tim2Info(template);
            if (info.bpp != 5 || info.hdr != 0x30) throw new IOException("glow_t8_tim2: template is not an 8-bit TIM2 with a 0x30 picture header");
            int w = IsoBytes.U16(lightling, 0x24), h = IsoBytes.U16(lightling, 0x26);
            var idx = GlowIndices(lightling);
            if (w != 64 || h != 64 || idx.Length != w * h) throw new IOException($"glow_t8_tim2: source disc is {w}x{h} — DrawFire's texel rect is hardcoded to 64x64");
            var pal = new byte[256 * 4]; var tab = GlowPalette(lightling, core, outer);
            for (int r = 0; r < ClutFixed.Length; r++) Array.Copy(tab, r * 4, pal, ClutFixed[r] * 4, 4);
            const int pic = 0x10;
            var hdr = template.AsSpan(0, pic + 0x30).ToArray();
            IsoBytes.U32(hdr, pic, (uint)(0x30 + idx.Length + pal.Length)); IsoBytes.U32(hdr, pic + 4, (uint)pal.Length); IsoBytes.U32(hdr, pic + 8, (uint)idx.Length);
            IsoBytes.U16(hdr, pic + 0x14, (ushort)w); IsoBytes.U16(hdr, pic + 0x16, (ushort)h);
            Array.Clear(hdr, pic + 0x18, 0x18);
            return hdr.Concat(idx).Concat(pal).ToArray();
        }

        // ───────────────────────────── the graft ─────────────────────────────
        private static string CatName(string orig, int index) => index == 0 ? CatRootName : NodePrefix + orig;

        /// <summary>Every node of the cat rig appended to the host .mds: the cat root renamed and unparented with its bind 3×3 at HideScale, the
        /// rest prefixed with their parents re-based; the cat's MDT chunks land after the host's mesh block. Returns (payload, nb, K).</summary>
        private static (byte[] payload, int nb, int K) GraftMds(byte[] bp, byte[] spl)
        {
            var bnodes = MdsNodes(bp); var snodes = MdsNodes(spl);
            int nb = bnodes.Count, K = snodes.Count;
            var bnames = bnodes.Select(b => b.name).ToHashSet();
            if (bnames.Contains(CatRootName)) throw new IOException("cat rig already grafted into this pack (patched ISO used as the base?)");
            var clash = snodes.Select((n, k) => CatName(n.name, k)).Where(bnames.Contains).ToList();
            if (clash.Count > 0) throw new IOException("node name clash between host and cat rig: " + string.Join(", ", clash));
            if (IsoBytes.U32(bp, 0x14) != 0x70 || IsoBytes.U32(spl, 0x14) != 0x70) throw new IOException("unexpected .mds record stride");
            int mesh0 = 0x18 + nb * 0x70 - 8;
            int firstMesh = bnodes.Where(b => b.meshOff != 0).Min(b => b.meshOff);
            if (firstMesh != mesh0 || Encoding.ASCII.GetString(bp, mesh0, 4) != "MDT\0") throw new IOException("host .mds table/mesh overlap layout not as expected");
            int delta = K * 0x70;
            var table = bp.AsSpan(0, mesh0).ToArray().Concat(BitConverter.GetBytes(nb)).Concat(BitConverter.GetBytes(0x70)).ToArray();
            IsoBytes.U32(table, 8, (uint)(nb + K));
            for (int i = 0; i < nb; i++) { uint off = IsoBytes.U32(table, 0x18 + i * 0x70 + 0x20); if (off != 0) IsoBytes.U32(table, 0x18 + i * 0x70 + 0x20, off + (uint)delta); }
            var mblock = bp.AsSpan(mesh0).ToArray();
            int pStart = 0x18 + (nb + K) * 0x70 - 8 + mblock.Length;
            var recs = new List<byte>(); var chunks = new List<byte>();
            for (int k = 0; k < K; k++)
            {
                var (nm, smo, spar) = snodes[k];
                var rec = spl.AsSpan(0x18 + k * 0x70, 0x68).ToArray();
                Array.Clear(rec, 0, 0x20); var nb2 = Encoding.Latin1.GetBytes(CatName(nm, k)); Array.Copy(nb2, rec, Math.Min(nb2.Length, 0x20));
                if (spar < 0)
                {
                    if (k != 0) throw new IOException("cat rig has more than one root");
                    Array.Copy(BitConverter.GetBytes(CatParent), 0, rec, 0x24, 4);
                    for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) { int o = 0x28 + (r * 4 + c) * 4; IsoBytes.WrF(rec, o, (float)(IsoBytes.F32(rec, o) * HideScale)); }
                }
                else Array.Copy(BitConverter.GetBytes(nb + spar), 0, rec, 0x24, 4);
                if (smo != 0)
                {
                    if (Encoding.ASCII.GetString(spl, smo, 4) != "MDT\0") throw new IOException($"cat node {nm} mesh @0x{smo:X} is not an MDT chunk");
                    int csz = (int)IsoBytes.U32(spl, smo + 8);
                    IsoBytes.U32(rec, 0x20, (uint)(pStart + chunks.Count));
                    chunks.AddRange(spl.AsSpan(smo, csz).ToArray());
                    chunks.AddRange(new byte[PyMath.Mod(-chunks.Count, 16)]);
                }
                recs.AddRange(rec); recs.AddRange(BitConverter.GetBytes(nb + k + 1)); recs.AddRange(BitConverter.GetBytes(0x70));
            }
            recs.RemoveRange(recs.Count - 8, 8);
            return (table.Concat(recs).Concat(mblock).Concat(chunks).ToArray(), nb, K);
        }

        /// <summary>`extra` = (name, parent index ABSOLUTE in the payload's table, local16, mdt or null) appended to an .mds payload: the records go
        /// after the last node, every existing mesh offset shifts by the inserted records, the new MDTs land after the existing mesh block.</summary>
        private static byte[] GraftExtraNodes(byte[] payload, List<(string name, int par, double[] local16, byte[] mdt)> extra)
        {
            int nOld = (int)IsoBytes.U32(payload, 8);
            if (IsoBytes.U32(payload, 0x14) != 0x70) throw new IOException("mds stride");
            int mesh0 = 0x18 + nOld * 0x70 - 8, delta = extra.Count * 0x70;
            var head = payload.AsSpan(0, mesh0).ToArray().Concat(BitConverter.GetBytes(nOld)).Concat(BitConverter.GetBytes(0x70)).ToArray();
            IsoBytes.U32(head, 8, (uint)(nOld + extra.Count));
            for (int i = 0; i < nOld; i++) { uint mo = IsoBytes.U32(head, 0x18 + i * 0x70 + 0x20); if (mo != 0) IsoBytes.U32(head, 0x18 + i * 0x70 + 0x20, mo + (uint)delta); }
            var mblock = payload.AsSpan(mesh0).ToArray();
            int pStart = 0x18 + (nOld + extra.Count) * 0x70 - 8 + mblock.Length;
            var chunks = new List<byte>(); var recs = new List<byte>();
            for (int k = 0; k < extra.Count; k++)
            {
                var (name, par, local16, mdt) = extra[k];
                var nm = Encoding.ASCII.GetBytes(name);
                if (nm.Length > 0x1F) throw new IOException("node name too long: " + name);
                var rec = new byte[0x68];
                Array.Copy(nm, rec, nm.Length);
                IsoBytes.U32(rec, 0x20, (uint)(mdt != null ? pStart + chunks.Count : 0));
                Array.Copy(BitConverter.GetBytes(par), 0, rec, 0x24, 4);
                for (int i = 0; i < 16; i++) IsoBytes.WrF(rec, 0x28 + i * 4, (float)local16[i]);
                if (mdt != null) { chunks.AddRange(mdt); chunks.AddRange(new byte[PyMath.Mod(-mdt.Length, 16)]); }
                recs.AddRange(rec); recs.AddRange(BitConverter.GetBytes(nOld + k + 1)); recs.AddRange(BitConverter.GetBytes(0x70));
            }
            var outp = head.Concat(recs).ToList();
            outp.RemoveRange(outp.Count - 8, 8);                                  // the last record's tail merges into the first MDT again
            return outp.Concat(mblock).Concat(chunks).ToArray();
        }

        /// <summary>Only the keyframes inside the clip windows (plus the bracketing key on each side) are kept. Bone ids stay RELATIVE to the cat root.</summary>
        private static void TrimTracks(MotFile mot, IEnumerable<(int lo, int hi)> windows)
        {
            foreach (var t in mot.Tracks)
            {
                var keys = t.Keyframes.OrderBy(k => k.Frame).ToList();
                var keep = new HashSet<int>();
                foreach (var (lo, hi) in windows)
                {
                    int before = -1, after = -1;
                    for (int i = 0; i < keys.Count; i++) { if (keys[i].Frame < lo) before = i; if (after < 0 && keys[i].Frame > hi) after = i; }
                    if (before >= 0) keep.Add(before);
                    if (after >= 0) keep.Add(after);
                    for (int i = 0; i < keys.Count; i++) if (lo <= keys[i].Frame && keys[i].Frame <= hi) keep.Add(i);
                }
                t.Keyframes = keep.Count > 0 ? keep.OrderBy(i => i).Select(i => keys[i]).ToList() : keys.Take(1).ToList();
            }
        }

        private static string Strip(string s) => s.Trim(' ', '\t', '\n', '\r', '\v', '\f');

        /// <summary>The host cfg with `ALLOC_DBUFF "cat_skin"` after its last ALLOC_DBUFF and the MOTION 1 block LAST (CommandFOOT / CommandEVENT bind
        /// to the channel of the most recent MOTION line — the host's FOOT/EVENT lines must stay on channel 0).</summary>
        private static string BuildCfg(string text, string nl)
        {
            var lines = text.Split(nl).ToList();
            int lastAlloc = -1; for (int i = 0; i < lines.Count; i++) if (Strip(lines[i]).StartsWith("ALLOC_DBUFF", StringComparison.Ordinal)) lastAlloc = i;
            if (lastAlloc < 0) throw new IOException("host cfg has no ALLOC_DBUFF line");
            if (!lines.Any(ln => Strip(ln).StartsWith("MOTION_END", StringComparison.Ordinal))) throw new IOException("host cfg has no MOTION_END");
            var block = new List<string> { "MOTION 1, \"cat.mot\", \"cat.bbp\", \"cat.wgt\"", "SHADOW_MOTION \"\", \"\", \"\"", $"KEY_START {KeyStart}" };
            for (int i = 0; i < CatKeys.Length; i++) { var (s, e, sp, cm) = CatKeys[i]; block.Add($"\tKEY\t{s},\t{e},\t{PyMath.Repr(sp)},\t\t//{KeyStart + i} {cm}"); }
            block.Add("MOTION_END"); block.Add(VersionMark); block.Add("");
            var outp = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                outp.Add(lines[i]);
                if (i == lastAlloc) outp.Add($"ALLOC_DBUFF \"{CatSkinName}\"");
            }
            while (outp.Count > 0 && Strip(outp[^1]) == "") outp.RemoveAt(outp.Count - 1);
            outp.Add(""); outp.AddRange(block);
            return string.Join(nl, outp);
        }

        internal sealed class Report { internal int Nb, K, Textures, MotBytes, MotKeys, SizeIn, SizeOut; }

        /// <summary>The cat bake on the host pack bytes: the s86 cat grafted in (nodes, bbp rows, textures, trimmed cat.mot with the float-up
        /// spliced in, cat.wgt, cat.bbp, the cfg block), then the wing graft, the cape and the mask on top.</summary>
        internal static (byte[] chr, Report rep) Assemble(byte[] baseBytes, byte[] catBytes, byte[] floatBytes, byte[] glowBytes, byte[] dranBytes, Action<string> log)
        {
            var bas = ChrPack.Parse(baseBytes); var cat = ChrPack.Parse(catBytes); var flt = ChrPack.Parse(floatBytes); var glowPack = ChrPack.Parse(glowBytes);
            var glowImg = glowPack.Find("fire.img") ?? throw new IOException("glow source pack lacks fire.img");
            foreach (string n in new[] { "e04c04cat.mds", "e04c04cat.mot" }) if (flt.Find(n) == null) throw new IOException($"float-up pack lacks {n}");
            foreach (string n in new[] { HostCfg, HostMds, HostBbp, HostImg }) if (bas.Find(n) == null) throw new IOException($"host pack lacks {n}");
            foreach (string n in new[] { "c04cat.mds", "c04cat.bbp", "c04cat.img", "c04cat.mot", "c04cat.wgt" }) if (cat.Find(n) == null) throw new IOException($"cat pack lacks {n}");
            var rep = new Report { SizeIn = baseBytes.Length };
            var (newMds, nb, K) = GraftMds(bas.Require(HostMds).Payload, cat.Require("c04cat.mds").Payload);
            bas.Require(HostMds).ReplacePayload(newMds);
            byte[] bb = bas.Require(HostBbp).Payload, cb = cat.Require("c04cat.bbp").Payload;
            if (bb.Length != nb * 64 || cb.Length != K * 64) throw new IOException(".bbp is not count*64 bytes");
            bas.Require(HostBbp).ReplacePayload(bb.Concat(cb).ToArray());
            var himg = new Bank(bas.Require(HostImg).Payload); var cimg = new Bank(cat.Require("c04cat.img").Payload);
            var items = himg.Entries.Select(e => (e.name, himg.Block(e.name))).ToList();
            items.AddRange(cimg.Entries.Select(e => (e.name, cimg.Block(e.name))));                 // the real cat fur (no flat retexture)
            if (items.Select(x => x.Item1).Distinct().Count() != items.Count) throw new IOException("texture entry name clash");
            bas.Require(HostImg).ReplacePayload(Bank.Build(himg.Magic, items));
            var mot = MotFile.FromPack(cat, "c04cat.mot"); TrimTracks(mot, CatKeys.Select(k => (k.s, k.e)));
            // The vertical leap: e04c04cat's float/hop-up window grafted by joint NAME into frames FloatDst — what the town ladder jump plays.
            var fmot = MotFile.FromPack(flt, "e04c04cat.mot");
            var frep = MotSplice.SpliceByJoint(mot, fmot, MotSplice.ReadMdsFrames(flt.Require("e04c04cat.mds").Payload), MotSplice.ReadMdsFrames(cat.Require("c04cat.mds").Payload),
                                               FloatSrc.lo, FloatSrc.hi, FloatDst.lo, FloatDst.hi);
            if (frep.Written == 0) throw new IOException("float-up graft wrote no tracks");
            var wgt = MotFile.FromPack(cat, "c04cat.wgt");
            var motRec = ChrRecord.Create("cat.mot", mot.BuildPayload()); var wgtRec = ChrRecord.Create("cat.wgt", wgt.BuildPayload()); var bbpRec = ChrRecord.Create("cat.bbp", cb);
            var cfg = bas.Require(HostCfg);
            string text = Encoding.Latin1.GetString(cfg.Payload);                                   // Shift-JIS bytes, carried through untouched
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            cfg.ReplacePayload(Encoding.Latin1.GetBytes(BuildCfg(text, nl)));
            bas.Records.Add(motRec); bas.Records.Add(wgtRec); bas.Records.Add(bbpRec);
            byte[] wingless = bas.Rebuild();
            // ── the wings, baked from the graft on the wingless pack just built ──
            var wd = WingBake.Build(dranBytes, wingless, nb, K, catBytes, log);
            var extra = wd.Nodes.Select(n => (n.Name, nb + n.Parent, n.Local16, n.Mdt)).ToList();      // parents → absolute host indices
            bas.Require(HostMds).ReplacePayload(GraftExtraNodes(bas.Require(HostMds).Payload, extra));
            bas.Require(HostBbp).ReplacePayload(bas.Require(HostBbp).Payload.Concat(wd.Bbp).ToArray());
            bas.Require("cat.bbp").ReplacePayload(bas.Require("cat.bbp").Payload.Concat(wd.Bbp).ToArray());
            var bank = new Bank(bas.Require(HostImg).Payload);
            var items1 = bank.Entries.Select(e => (e.name, bank.Block(e.name))).ToList();
            items1.Add((wd.Texture, FlatTim2(cimg.Block("c04cat01"), WingRgba)));
            byte[] light = new Bank(glowImg.Payload).Block("lightling");
            // The element glow: one 8-bit disc for all six colours. Its resting CLUT is "None", so it looks right even if the palette cave never runs.
            items1.Add((GlowT8Name, GlowT8Tim2(cimg.Block("c04cat01"), light, GlowElements[5].core, GlowElements[5].outer)));
            if (items1.Select(x => x.Item1).Distinct().Count() != items1.Count) throw new IOException("texture entry name clash (wings)");
            bas.Require(HostImg).ReplacePayload(Bank.Build(bank.Magic, items1));
            var wgt2 = MotFile.FromRecord(bas.Require("cat.wgt")); wgt2.Tracks.AddRange(wd.WgtTracks);
            var mot2 = MotFile.FromRecord(bas.Require("cat.mot")); mot2.Tracks.AddRange(wd.MotTracks);
            bas.Require("cat.wgt").ReplacePayload(wgt2.BuildPayload()); bas.Require("cat.mot").ReplacePayload(mot2.BuildPayload());
            string text2 = Encoding.Latin1.GetString(bas.Require(HostCfg).Payload);
            string anchor = $"ALLOC_DBUFF \"{CatSkinName}\"";
            int at = text2.IndexOf(anchor, StringComparison.Ordinal);
            if (at >= 0) text2 = text2.Substring(0, at) + anchor + nl + string.Join(nl, wd.AllocDbuff.Select(nm => $"ALLOC_DBUFF \"{nm}\"")) + text2.Substring(at + anchor.Length);
            // ── the Super Steve cape: a FRAME node reachable from HER root, its MDT the rest lattice, a CLOTH line + the .clo record, ALLOC_MDT
            //    for the node (every shipped cloth has it), a flat texture. The runtime clones her CCloth onto the cat copy and re-anchors it. ──
            var cape = wd.Cape;
            bas.Require(HostMds).ReplacePayload(GraftExtraNodes(bas.Require(HostMds).Payload, new List<(string, int, double[], byte[])> { (cape.Name, cape.ParentAbs, cape.Local16, cape.Mdt) }));
            var crow = new byte[64]; for (int i = 0; i < 16; i++) IsoBytes.WrF(crow, i * 4, (float)cape.Local16[i]);
            bas.Require(HostBbp).ReplacePayload(bas.Require(HostBbp).Payload.Concat(crow).ToArray());
            bas.Require("cat.bbp").ReplacePayload(bas.Require("cat.bbp").Payload.Concat(crow).ToArray());
            var bank2 = new Bank(bas.Require(HostImg).Payload);
            var items2 = bank2.Entries.Select(e => (e.name, bank2.Block(e.name))).ToList();
            items2.Add((cape.Texture, FlatTim2(cimg.Block("c04cat01"), CapeRgba)));
            if (items2.Select(x => x.Item1).Distinct().Count() != items2.Count) throw new IOException("texture entry name clash (cape)");
            bas.Require(HostImg).ReplacePayload(Bank.Build(bank2.Magic, items2));
            bas.Records.Add(ChrRecord.Create(CapeCloName, cape.Clo));
            var lines2 = text2.Split(nl).ToList();
            int firstDbuff = lines2.FindIndex(ln => Strip(ln).StartsWith("ALLOC_DBUFF", StringComparison.Ordinal));
            lines2.Insert(firstDbuff, $"ALLOC_MDT \"{cape.Name}\"");
            int shadow = lines2.FindIndex(ln => Strip(ln).StartsWith("SHADOW_MODEL", StringComparison.Ordinal));
            lines2.Insert(shadow + 1, $"CLOTH \"{CapeCloName}\"");
            bas.Require(HostCfg).ReplacePayload(Encoding.Latin1.GetBytes(string.Join(nl, lines2)));
            byte[] outp = bas.Rebuild();
            rep.Nb = nb; rep.K = K + extra.Count; rep.Textures = items2.Count; rep.MotBytes = bas.Require("cat.mot").Size; rep.MotKeys = mot2.Tracks.Sum(t => t.Keyframes.Count); rep.SizeOut = outp.Length;
            Verify(baseBytes, outp, catBytes, wd);
            return (outp, rep);
        }

        /// <summary>Structural checks on the finished pack (the same ones the bake has always made).</summary>
        private static void Verify(byte[] baseBytes, byte[] newBytes, byte[] catBytes, WingBake.Result wings)
        {
            void Check(bool ok, string what) { if (!ok) throw new IOException("cat pack verify: " + what); }
            var old = ChrPack.Parse(baseBytes); var nw = ChrPack.Parse(newBytes); var cat = ChrPack.Parse(catBytes);
            Check(ChrPack.Parse(nw.Rebuild()).Rebuild().AsSpan().SequenceEqual(newBytes), "pack round-trip");
            var on = MdsNodes(old.Require(HostMds).Payload); var nn = MdsNodes(nw.Require(HostMds).Payload); var cn = MdsNodes(cat.Require("c04cat.mds").Payload);
            int nb = on.Count, K = cn.Count; var X = wings.Nodes; const int C = 1;
            byte[] npl = nw.Require(HostMds).Payload;
            Check(nn.Count == nb + K + X.Count + C && IsoBytes.U32(npl, 8) == nb + K + X.Count + C, "node count");
            var cp = wings.Cape; int ci = nb + K + X.Count;
            Check(nn[ci].name == cp.Name && nn[ci].parent == 0 && nn[ci].meshOff != 0, "cape node");
            Check(IsoBytes.U32(npl, nn[ci].meshOff + 12) == cp.Rows * cp.Cols, "cape lattice vertex count");
            Check(nw.Find(CapeCloName) != null && nw.Find(CapeCloName).Payload.AsSpan().SequenceEqual(cp.Clo), "cape .clo record");
            Check(Encoding.ASCII.GetString(cp.Clo).Split("BOUND").Length - 1 == CatWings.CapeBounds.Length, "cape BOUND count");
            for (int k = 0; k < X.Count; k++)
            {
                Check(nn[nb + K + k].name == X[k].Name && nn[nb + K + k].parent == nb + X[k].Parent && (nn[nb + K + k].meshOff != 0) == (X[k].Mdt != null), $"wing node {X[k].Name}");
                for (int i = 0; i < 16; i++) Check(IsoBytes.F32(npl, 0x18 + (nb + K + k) * 0x70 + 0x28 + i * 4) == (float)X[k].Local16[i], $"wing node {X[k].Name} bind");
            }
            Check(nn.Take(nb).Select(n => n.name).SequenceEqual(on.Select(n => n.name)), "host nodes renamed");
            Check(nn[nb].name == CatRootName && nn[nb].parent == CatParent, "cat root parent");
            for (int k = 1; k < K; k++) Check(nn[nb + k].name == NodePrefix + cn[k].name && nn[nb + k].parent == nb + cn[k].parent, $"cat node {k}");
            int meshes = 0;
            foreach (var (nm, mo, _) in nn) if (mo != 0) { Check(Encoding.ASCII.GetString(npl, mo, 4) == "MDT\0", $"{nm} mesh magic"); meshes++; }
            Check(meshes == on.Count(n => n.meshOff != 0) + cn.Count(n => n.meshOff != 0) + X.Count(x => x.Mdt != null) + C, "mesh count");
            byte[] opl = old.Require(HostMds).Payload;
            for (int i = 0; i < nb; i++)
            {
                var o = opl.AsSpan(0x18 + i * 0x70, 0x68).ToArray(); var n = npl.AsSpan(0x18 + i * 0x70, 0x68).ToArray();
                if (on[i].meshOff != 0) IsoBytes.U32(n, 0x20, (uint)on[i].meshOff);
                Check(n.AsSpan().SequenceEqual(o), $"host node {i} changed");
            }
            Check(nw.Require(HostBbp).Size == (nb + K + X.Count + C) * 64, "bbp rows");
            Check(nw.Require("cat.bbp").Size == (K + X.Count + C) * 64, "cat.bbp rows");
            foreach (string name in new[] { "cat.mot", "cat.wgt" }) Check(MotFile.FromRecord(nw.Require(name)).Tracks.All(t => t.W0 < K + X.Count), $"{name} track ids must be cat-relative");
            var wg = MotFile.FromRecord(nw.Require("cat.wgt"));
            foreach (var x in X)
            {
                if (x.Mdt == null) continue;
                int M = K + X.IndexOf(x);
                var run = wg.Tracks.Where(t => t.W0 == M).ToList();
                Check(run.Count > 0 && run[0].W1 == x.Parent && run[0].Keyframes.Count == 0, $"{x.Name} wgt reset entry");
                var bones = run.Skip(1).Select(t => t.W1).ToList();
                Check(bones.SequenceEqual(bones.OrderBy(b => b)) && bones.All(b => b < K + X.Count), $"{x.Name} wgt bone order");
                var cover = new Dictionary<uint, double>();
                foreach (var t in run.Skip(1)) foreach (var kf in t.Keyframes) cover[kf.Frame] = (cover.TryGetValue(kf.Frame, out double v) ? v : 0) + kf.Value[0];
                uint nv = IsoBytes.U32(npl, nn[nb + M].meshOff + 12);
                Check(cover.Count == nv && cover.Keys.All(f => f < nv) && cover.Values.All(v => Math.Abs(v - 100) < 0.01), $"{x.Name} wgt coverage ({cover.Count}/{nv})");
            }
            string text = Encoding.Latin1.GetString(nw.Require(HostCfg).Payload);
            Check(text.Contains($"ALLOC_DBUFF \"{CatSkinName}\"") && text.Contains("MOTION 1, \"cat.mot\"") && text.Contains($"KEY_START {KeyStart}"), "cfg");
            Check(wings.AllocDbuff.All(nm => text.Contains($"ALLOC_DBUFF \"{nm}\"")), "wing / mask ALLOC_DBUFF");
            Check(text.Contains($"ALLOC_MDT \"{cp.Name}\"") && text.Contains($"CLOTH \"{CapeCloName}\""), "cape cfg lines");
            Check(text.IndexOf("CLOTH \"", StringComparison.Ordinal) < text.IndexOf("MOTION 0", StringComparison.Ordinal), "CLOTH must precede the motion blocks");
            Check(text.Split("MOTION_END").Length - 1 == 2, "cfg blocks");
            int lastFoot = Math.Max(text.LastIndexOf("FOOT", StringComparison.Ordinal), text.LastIndexOf("EVENT", StringComparison.Ordinal));
            Check(lastFoot < text.IndexOf("MOTION 1, \"cat.mot\"", StringComparison.Ordinal), "MOTION 1 must follow every FOOT/EVENT line");
            if (old.Find("hand_up.cfg") != null) Check(nw.Require("hand_up.cfg").Payload.AsSpan().SequenceEqual(old.Find("hand_up.cfg").Payload), "hand_up.cfg changed");
            var bank = new Bank(nw.Require(HostImg).Payload);
            var names = bank.Entries.Select(e => e.name).ToHashSet();
            Check(new[] { "c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05" }.All(names.Contains), "cat textures");
            Check(names.Contains(wings.Texture) && names.Contains(GlowT8Name) && names.Contains(cp.Texture), "wing / glow / cape textures");
            Check(wings.MaskTexture == cp.Texture, "the mask shares the cape's texture");
            var inf = Tim2Info(bank.Block(wings.Texture));
            Check(inf.w == 32 && inf.h == 32, "flat wing texture");
            Check(new Bank(old.Require(HostImg).Payload).Entries.All(e => names.Contains(e.name)), "host textures kept");
            foreach (var (n, _) in bank.Entries) Check(Encoding.ASCII.GetString(bank.Block(n), 0, 4) == "TIM2", $"texture {n} block");
            Check(text.Contains(VersionMark), "version mark");
        }

        private static bool HasCat(byte[] chr, string mdsName = HostMds)
        {
            try { return MdsNodes(ChrPack.Parse(chr).Require(mdsName).Payload).Any(n => n.name == CatRootName); }
            catch (Exception) { return false; }
        }

        /// <summary>This tool version's bake (older cat bakes of the host get reverted and redone).</summary>
        private static bool IsCurrentBake(byte[] chr)
        {
            try { return HasCat(chr) && Encoding.Latin1.GetString(ChrPack.Parse(chr).Require(HostCfg).Payload).Contains(VersionMark); }
            catch (Exception) { return false; }
        }

        /// <summary>The six element ramps reach the game as an EMBEDDED resource (Resources/isoPatch/catGlowPalettes.bin, written into the ELF by
        /// ElfCatPatches.PatchCatGlowPalettes). The pack is re-baked on every patch, but that .bin is regenerated only by the build
        /// (tools/build_resources.py), so a bake whose ramps no longer match the embedded blob is refused: the glow would keep its OLD colours
        /// while every diagnostic reported success.</summary>
        private static void RefuseIfPaletteBlobStale(byte[] glowBytes)
        {
            using var st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.catGlowPalettes.bin");
            if (st == null) return;
            using var ms = new MemoryStream(); st.CopyTo(ms);
            var img = ChrPack.Parse(glowBytes).Find("fire.img");
            if (img == null) return;
            if (!ms.ToArray().AsSpan().SequenceEqual(GlowPalettes(new Bank(img.Payload).Block("lightling"))))
                throw new IOException("GlowElements has changed but Resources/isoPatch/catGlowPalettes.bin has not. The patch writes that .bin, so the glow would keep its OLD colours. "
                                      + "Rebuild the mod with the extracted disc available (tools/build_resources.py regenerates the .bin), then patch again.");
        }

        /// <summary>The post-step: the earlier weapon-pack bake reverted if the ISO carries it, an older host bake reverted to vanilla, the pack
        /// assembled from the ISO's own files and redirected into the DATA.DAT tail. Idempotent.</summary>
        internal static void Run(IsoArchive arc, Action<string> log)
        {
            foreach (var (name, vanilla) in WeaponRevert)
            {
                var cur = arc.Slot(name);
                if (cur == vanilla) continue;
                bool isChr = name.EndsWith(".chr", StringComparison.Ordinal) && !name.Contains("wtes");
                byte[] van = arc.ReadAt(vanilla.Item1, (int)vanilla.Item2);
                bool ok;
                try
                {
                    var pk = ChrPack.Parse(van);
                    byte[] inner = isChr ? van : pk.Require("c04w09.chr").Payload;
                    ok = ChrPack.Parse(inner).Find("c04w09.mds") != null && !HasCat(inner, "c04w09.mds");
                }
                catch (Exception) { ok = false; }
                if (!ok) throw new IOException($"{name}: record differs from vanilla and the vanilla bytes are not where expected — refusing to revert");
                arc.SetSlot(name, vanilla);
                log($"reverted {name} to its vanilla record (earlier weapon-pack bake removed)");
            }
            RefuseIfPaletteBlobStale(arc.Read(GlowSrc));
            byte[] host = arc.Read(HostChr);
            if (IsCurrentBake(host)) { log("cat already in dun\\mainchara\\c04b.chr — skipped"); return; }
            if (HasCat(host))                                                  // an older bake of this tool: back to vanilla first
            {
                byte[] van = arc.ReadAt(HostVanilla.Item1, (int)HostVanilla.Item2);
                if (HasCat(van) || ChrPack.Parse(van).Find(HostMds) == null) throw new IOException("c04b.chr carries an older cat bake and the vanilla bytes are not where expected — refusing");
                arc.SetSlot(HostChr, HostVanilla);
                log("reverted dun\\mainchara\\c04b.chr to its vanilla record (older cat bake removed)");
                host = van;
            }
            var (newChr, rep) = Assemble(host, arc.Read(CatChr), arc.Read(FloatChr), arc.Read(GlowSrc), arc.Read(DranChr), log);
            log($"Divine Beast cat (wings) assembled into c04b.chr — {rep.K} cat nodes, {rep.Textures} textures, cat.mot {rep.MotBytes:N0} B ({rep.MotKeys} keys), {rep.SizeIn:N0}->{rep.SizeOut:N0} B");
            arc.Redirect(HostChr, newChr);
            log("DONE (Divine Beast Title cat pack)");
        }
    }
}
