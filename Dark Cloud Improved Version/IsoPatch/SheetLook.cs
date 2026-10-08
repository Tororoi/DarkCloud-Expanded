using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A look made on the Bomb Gemron preview page (its "This look" export) applied to the pack's sheets exactly as the page
    /// draws them. The body sheet is divided into sections by a map (one byte per texel); each section has its own sliders (an HSL hue
    /// turn, then saturation, contrast, brightness, lightness), palette entries it leaves as they are and entries set by hand. Every
    /// (entry, section) pair a texel uses becomes a colour; when there are more than 256 the most similar are merged, near-black first,
    /// until 256 are left; the sheet is then re-indexed onto the colours it ends with. The second sheet, which the page draws through
    /// "everything else"'s sliders, has those applied to every palette entry. The maths, the texel-order walks and the rounding
    /// (JavaScript's round-half-up) are the page's own, so the bake is what the page showed.</summary>
    internal sealed class SheetLook
    {
        private const int Rows = 64;                        // the page's section limit (its palette rows)
        private const byte Opaque = 0x80;                   // the sheets' CLUT alpha
        internal const int Budget = 256;

        private sealed class Section
        {
            internal double Hue, Sat = 1, Bri = 1, Con = 1, Light;
            internal HashSet<int> Keep = new HashSet<int>();
            internal Dictionary<int, int[]> Set = new Dictionary<int, int[]>();
        }

        private readonly List<Section> _sections = new List<Section>();
        private int _mapW, _mapH;
        private byte[] _map;

        internal static SheetLook Parse(byte[] json)
        {
            var look = new SheetLook();
            using var doc = JsonDocument.Parse(json);
            foreach (var s in doc.RootElement.GetProperty("sections").EnumerateArray())
            {
                var sec = new Section
                {
                    Hue = s.TryGetProperty("hue", out var h) ? h.GetDouble() : 0, Sat = s.TryGetProperty("sat", out var sa) ? sa.GetDouble() : 1,
                    Bri = s.TryGetProperty("bri", out var b) ? b.GetDouble() : 1, Con = s.TryGetProperty("con", out var c) ? c.GetDouble() : 1,
                    Light = s.TryGetProperty("light", out var l) ? l.GetDouble() : 0,
                };
                if (s.TryGetProperty("keep", out var keep)) foreach (var k in keep.EnumerateArray()) sec.Keep.Add(k.GetInt32());
                if (s.TryGetProperty("set", out var set))
                    foreach (var p in set.EnumerateObject())
                    {
                        string hex = p.Value.GetString().TrimStart('#');
                        sec.Set[int.Parse(p.Name)] = new[] { Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16) };
                    }
                look._sections.Add(sec);
            }
            if (look._sections.Count == 0 || look._sections.Count > Rows) throw new IOException($"look: {look._sections.Count} sections (1–{Rows})");
            if (doc.RootElement.TryGetProperty("map", out var map))
            {
                look._mapW = map.GetProperty("w").GetInt32(); look._mapH = map.GetProperty("h").GetInt32();
                look._map = UnRle(Convert.FromBase64String(map.GetProperty("rle").GetString()), look._mapW * look._mapH);
            }
            return look;
        }

        private static byte[] UnRle(byte[] runs, int len)
        {
            var a = new byte[len]; int o = 0;
            for (int i = 0; i + 1 < runs.Length; i += 2) { int n = runs[i]; byte v = runs[i + 1]; for (int j = 0; j < n && o < len; j++) a[o++] = v; }
            return a;
        }

        // ---------------------------------------------------------------------------- the page's colour maths
        private static double JsRound(double x) => Math.Floor(x + 0.5);

        private static (double h, double s, double l) Rgb2Hsl(double r, double g, double b)
        {
            r /= 255; g /= 255; b /= 255;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), l = (mx + mn) / 2, h = 0, s = 0;
            if (mx != mn)
            {
                double d = mx - mn; s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
                h = mx == r ? (g - b) / d + (g < b ? 6 : 0) : mx == g ? (b - r) / d + 2 : (r - g) / d + 4; h *= 60;
            }
            return (h, s * 100, l * 100);
        }

        private static int[] Hsl2Rgb(double h, double s, double l)
        {
            h = ((h % 360) + 360) % 360; s = Math.Max(0, Math.Min(100, s)) / 100; l = Math.Max(0, Math.Min(100, l)) / 100;
            double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = l - c / 2;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; } else if (h < 120) { r = x; g = c; b = 0; } else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; } else if (h < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
            return new[] { (int)JsRound((r + m) * 255), (int)JsRound((g + m) * 255), (int)JsRound((b + m) * 255) };
        }

        /// <summary>A colour through a section's sliders: the hue turn (HSL), then saturation, contrast, brightness, lightness.</summary>
        private static int[] Slide(int r0, int g0, int b0, Section A)
        {
            double r = r0, g = g0, b = b0;
            if (A.Hue != 0) { var (h, sa, l) = Rgb2Hsl(r, g, b); var t = Hsl2Rgb(h + A.Hue, sa, l); r = t[0]; g = t[1]; b = t[2]; }
            double lu = 0.299 * r / 255 + 0.587 * g / 255 + 0.114 * b / 255;
            int F(double c) { c = lu + (c / 255 - lu) * A.Sat; c = (c - 0.5) * A.Con + 0.5; c = c * A.Bri + A.Light; return (int)JsRound(Math.Max(0, Math.Min(1, c)) * 255); }
            return new[] { F(r), F(g), F(b) };
        }

        /// <summary>The colour entry k draws with in section s: set by hand, else left as it is, else through the section's sliders.</summary>
        private int[] Colour(int s, int k, int[][] pal)
        {
            var st = _sections[s];
            if (st.Set.TryGetValue(k, out var set)) return set;
            if (st.Keep.Contains(k)) return new[] { pal[k][0], pal[k][1], pal[k][2] };
            return Slide(pal[k][0], pal[k][1], pal[k][2], st);
        }

        private static int Key(int[] c) => (c[0] << 16) | (c[1] << 8) | c[2];

        // ---------------------------------------------------------------------------- sheets
        private static (int hdr, int w, int h, int clutAt, int pixAt) Picture(byte[] tim)
        {
            const int pic = Tim8.Pic;
            int clutSz = (int)IsoBytes.U32(tim, pic + 4), imgSz = (int)IsoBytes.U32(tim, pic + 8), hdr = IsoBytes.U16(tim, pic + 0x0C);
            int w = IsoBytes.U16(tim, pic + 0x14), h = IsoBytes.U16(tim, pic + 0x16);
            if (tim[pic + 0x13] != 5 || imgSz != w * h || clutSz != 256 * 4) throw new IOException($"look: not an 8-bit 256-colour picture ({w}×{h})");
            return (hdr, w, h, pic + hdr + imgSz, pic + hdr);
        }

        private static int Csm1(int i) => (i & ~0x18) | ((i & 0x08) << 1) | ((i & 0x10) >> 1);

        /// <summary>The body sheet (an IM2 bank's picture: pixels in the GS's block order) in the look, re-indexed onto the colours it ends
        /// with; <paramref name="colours"/> reports how many the look used before merging.</summary>
        internal byte[] ApplyToSheet(byte[] tim, out int colours, out double worstShift)
        {
            var (_, w, h, clutAt, pixAt) = Picture(tim);
            byte[] idx = Tim8.Unswizzle8(tim.AsSpan(pixAt, w * h).ToArray(), w, h);
            var pal = new int[256][];
            for (int k = 0; k < 256; k++) { int j = Csm1(k) * 4; pal[k] = new int[] { tim[clutAt + j], tim[clutAt + j + 1], tim[clutAt + j + 2] }; }
            byte[] map = _map != null && _mapW == w && _mapH == h ? _map : null;
            int n = _sections.Count;
            // every (entry, section) pair a texel uses, with its texel count, in first-seen order
            var pairCount = new Dictionary<int, int>(); var pairOrder = new List<int>();
            for (int i = 0; i < idx.Length; i++)
            {
                int key = idx[i] * Rows + (map != null ? map[i] : 0);
                if (pairCount.TryGetValue(key, out int c)) pairCount[key] = c + 1; else { pairCount[key] = 1; pairOrder.Add(key); }
            }
            // their colours, weighted by texels, in first-seen order
            var colCount = new Dictionary<int, double>(); var colOrder = new List<int>();
            foreach (int key in pairOrder)
            {
                int k = key / Rows, s = Math.Min(key % Rows, n - 1); int v = Key(Colour(s, k, pal));
                if (colCount.TryGetValue(v, out double c)) colCount[v] = c + pairCount[key]; else { colCount[v] = pairCount[key]; colOrder.Add(v); }
            }
            colours = colOrder.Count;
            var merged = Merge(colOrder, colCount, out worstShift);
            // each texel's final colour, the palette in first-seen order
            var outIdx = new byte[w * h]; var slot = new Dictionary<int, int>(); var outPal = new List<int>();
            for (int i = 0; i < idx.Length; i++)
            {
                int s = map != null ? map[i] : 0; int row = s < n ? s : 0;
                int v = Key(Colour(row, idx[i], pal)); if (merged.TryGetValue(v, out int mv)) v = mv;
                if (!slot.TryGetValue(v, out int q)) { q = outPal.Count; if (q >= Budget) throw new IOException("look: more than 256 colours after merging"); slot[v] = q; outPal.Add(v); }
                outIdx[i] = (byte)q;
            }
            var o = (byte[])tim.Clone();
            Tim8.Swizzle8(outIdx, w, h).CopyTo(o, pixAt);
            for (int k = 0; k < 256; k++)
            {
                int j = clutAt + Csm1(k) * 4, v = k < outPal.Count ? outPal[k] : 0;
                o[j] = (byte)(v >> 16); o[j + 1] = (byte)(v >> 8); o[j + 2] = (byte)v; o[j + 3] = Opaque;
            }
            return o;
        }

        /// <summary>The second sheet: "everything else"'s sliders on every palette entry (the page's shader path keeps none).</summary>
        internal byte[] ApplyBaseToSheet(byte[] tim)
        {
            var (_, _, _, clutAt, _) = Picture(tim);
            var o = (byte[])tim.Clone();
            for (int j = 0; j < 256; j++)
            {
                int p = clutAt + j * 4; var c = Slide(o[p], o[p + 1], o[p + 2], _sections[0]);
                o[p] = (byte)c[0]; o[p + 1] = (byte)c[1]; o[p + 2] = (byte)c[2];
            }
            return o;
        }

        /// <summary>The page's merge: each colour (weighted by its texels) a cluster; the cheapest pair merged into its weighted centroid
        /// until 256 are left. Cost = perceptual distance × x·√x, x = 0.12 + the pair's mean luminance, so near-black colours go first.
        /// Returns each merged colour's centroid (rounded).</summary>
        private static Dictionary<int, int> Merge(List<int> keys, Dictionary<int, double> weight, out double worst)
        {
            var map = new Dictionary<int, int>(); worst = 0;
            int n = keys.Count; if (n <= Budget) return map;
            var R = new double[n]; var G = new double[n]; var B = new double[n]; var W = new double[n]; var alive = new bool[n]; var mem = new List<int>[n];
            for (int i = 0; i < n; i++) { int v = keys[i]; R[i] = v >> 16; G[i] = (v >> 8) & 255; B[i] = v & 255; W[i] = weight[v]; alive[i] = true; mem[i] = new List<int> { v }; }
            double Cost(int i, int j)
            {
                double dr = R[i] - R[j], dg = G[i] - G[j], db = B[i] - B[j];
                double lum = (0.299 * (R[i] + R[j]) + 0.587 * (G[i] + G[j]) + 0.114 * (B[i] + B[j])) / 510, x = 0.12 + lum;
                return Math.Sqrt(2 * dr * dr + 4 * dg * dg + 3 * db * db) * (x * Math.Sqrt(x));
            }
            var nn = new int[n]; var nd = new double[n];
            void Near(int i) { int b = -1; double bd = double.PositiveInfinity; for (int j = 0; j < n; j++) { if (j == i || !alive[j]) continue; double d = Cost(i, j); if (d < bd) { bd = d; b = j; } } nn[i] = b; nd[i] = bd; }
            for (int i = 0; i < n; i++) Near(i);
            int left = n;
            while (left > Budget)
            {
                int i = -1; double bd = double.PositiveInfinity;
                for (int k = 0; k < n; k++) if (alive[k] && nd[k] < bd) { bd = nd[k]; i = k; }
                int j = nn[i]; double w = W[i] + W[j];
                R[i] = (R[i] * W[i] + R[j] * W[j]) / w; G[i] = (G[i] * W[i] + G[j] * W[j]) / w; B[i] = (B[i] * W[i] + B[j] * W[j]) / w; W[i] = w;
                mem[i].AddRange(mem[j]); alive[j] = false; left--;
                for (int k = 0; k < n; k++)
                {
                    if (!alive[k]) continue;
                    if (k == i || nn[k] == i || nn[k] == j) Near(k);
                    else { double d = Cost(k, i); if (d < nd[k]) { nd[k] = d; nn[k] = i; } }
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (!alive[i] || mem[i].Count < 2) continue;
                int[] c = { (int)JsRound(R[i]), (int)JsRound(G[i]), (int)JsRound(B[i]) }; int cv = Key(c);
                foreach (int v in mem[i]) { map[v] = cv; worst = Math.Max(worst, Math.Sqrt(Math.Pow((v >> 16) - c[0], 2) + Math.Pow(((v >> 8) & 255) - c[1], 2) + Math.Pow((v & 255) - c[2], 2))); }
            }
            return map;
        }
    }
}
