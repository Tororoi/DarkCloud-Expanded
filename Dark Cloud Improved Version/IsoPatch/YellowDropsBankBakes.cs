using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Yellow Drops WEST BANK: the bulge (edge columns move −x by WEST_BULGE·sin(πs) along the section, paired crown
    /// columns follow, section ends stay welded) baked at 2× station density into a rebuilt s1301 sub — the grid10/grid11
    /// visual MDTs (every edge spanning two adjacent stations split at the attribute-lerped midpoint, then all station
    /// columns shifted west), the s1301_a crown wall + floor and the s1301_c camera wall the same way, the doumu_c factory
    /// camera ring pulled in, and the torii-gate pillar camera hulls added as new `_c` nodes. Also the fish walls along the
    /// bulged waterline for the fishing DCFC bin.</summary>
    internal static class YellowDropsBankBakes
    {
        private const double WestBulge = 70.0;
        private static readonly double[] WbSpan = { -145.0, 274.0 };
        private const int WbSubdiv = 1;                      // extra stations per segment (2× density)
        private const double FishWallBottom = -24.0;         // fish swim at WaterLevel − 8; walls run bank top down to here
        private const double YLocal = 210.0;                 // s1301 node frames sit at world y + 210
        private const double Tol = 0.35;
        internal const int VanillaS1301Size = 0x4ca50;       // the block offsets below are the vanilla sub's

        // vanilla west-bank rows (world coords), NW → SE
        private static readonly Dictionary<string, double[][]> WbRows = new()
        {
            ["edge_top"] = new[] { new[] { -426.0, 23.0, -142.0 }, new[] { -424.0, 23.0, -23.0 }, new[] { -413.0, 23.0, 102.0 }, new[] { -388.0, 23.0, 206.0 }, new[] { -382.0, 23.0, 275.0 } },
            ["edge_bot"] = new[] { new[] { -427.0, -10.0, -142.0 }, new[] { -424.0, -10.0, -23.0 }, new[] { -413.0, -10.0, 102.0 }, new[] { -388.0, -10.0, 206.0 }, new[] { -382.0, -10.0, 275.0 } },
            ["crown"]    = new[] { new[] { -420.0, 30.0, -148.0 }, new[] { -417.0, 30.0, -24.0 }, new[] { -404.0, 30.0, 103.0 }, new[] { -378.0, 30.0, 206.0 }, new[] { -369.0, 30.0, 273.0 } },
            ["cam"]      = new[] { new[] { -441.0, 30.0, -134.0 }, new[] { -441.0, 30.0, -22.0 }, new[] { -430.0, 30.0, 103.0 }, new[] { -405.0, 30.0, 208.0 }, new[] { -398.0, 30.0, 275.0 } },
        };

        private static double WbProfile(double z)
        {
            double z0 = WbSpan[0], z1 = WbSpan[1];
            if (!(z0 < z && z < z1)) return 0.0;
            return -WestBulge * Math.Sin(Math.PI * (z - z0) / (z1 - z0));
        }

        private static List<double[]> Chain(string row, double loZ, double hiZ)
            => WbRows[row].Where(p => loZ <= p[2] && p[2] <= hiZ).Select(p => new[] { p[0], p[1] + YLocal, p[2] }).ToList();

        private static Dictionary<string, List<List<double[]>>> Chains() => new()
        {
            ["grid10"] = new() { Chain("edge_top", -150, 0), Chain("edge_bot", -150, 0), Chain("crown", -150, 0) },
            ["grid11"] = new() { Chain("edge_top", -30, 280), Chain("edge_bot", -30, 280), Chain("crown", -30, 280) },
            ["_a"] = new() { WbRows["crown"].Select(p => new[] { p[0], 240.0, p[2] }).ToList(), WbRows["crown"].Select(p => new[] { p[0], 336.0, p[2] }).ToList() },
            ["_c"] = new() { WbRows["cam"].Select(p => new[] { p[0], 240.0, p[2] }).ToList(), WbRows["cam"].Select(p => new[] { p[0], 200.0, p[2] }).ToList() },
        };

        private static double[] Lerp3(double[] a, double[] b, int k) { double t = k / (WbSubdiv + 1.0); return new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t }; }

        /// <summary>(A, B) for every station pair of the chains, in order.</summary>
        private static List<(double[] a, double[] b)> Segments(List<List<double[]>> chains)
        {
            var segs = new List<(double[], double[])>();
            foreach (var ch in chains) for (int i = 0; i + 1 < ch.Count; i++) segs.Add((ch[i], ch[i + 1]));
            return segs;
        }

        private static bool ColMatch(double[] p, double[] s) => Math.Abs(p[0] - s[0]) <= Tol && Math.Abs(p[1] - s[1]) <= 1.0 && Math.Abs(p[2] - s[2]) <= Tol;

        private static List<double[]> AllColumns(List<List<double[]>> chains)
        {
            var cols = new List<double[]>();
            foreach (var ch in chains)
            {
                cols.AddRange(ch);
                for (int i = 0; i + 1 < ch.Count; i++) for (int k = 1; k <= WbSubdiv; k++) cols.Add(Lerp3(ch[i], ch[i + 1], k));
            }
            return cols;
        }

        // ───────────────────────── visual MDT edit ─────────────────────────
        private static (byte[] mdt, int moved) EditVisualMdt(byte[] scn, int fo, List<List<double[]>> chains)
        {
            var m = MdtMesh.Parse(scn, fo);
            var tris = m.Triangles(true).Select(t => (mat: t.mat, recs: new[] { t.a, t.b, t.c })).ToList();
            var segs = Segments(chains);
            var midpos = new Dictionary<(int, int, double), int>();
            var midattr = new Dictionary<(int, int, double, string), int>();
            List<double[]> Block(string b) => b == "uv" ? m.Uv : b == "norm" ? m.Norm : m.Col;
            bool SegOfEdge(int ia, int ib)
            {
                double[] pa = m.Pos[ia], pb = m.Pos[ib];
                foreach (var (a, b) in segs) if ((ColMatch(pa, a) && ColMatch(pb, b)) || (ColMatch(pa, b) && ColMatch(pb, a))) return true;
                return false;
            }
            int LerpEntry(string block, int ia, int ib, double t)
            {
                var key = (Math.Min(ia, ib), Math.Max(ia, ib), PyMath.Round(t, 4), block);
                if (midattr.TryGetValue(key, out int have)) return have;
                var lst = Block(block); double[] va = lst[ia], vb = lst[ib];
                double t2 = ia > ib ? 1.0 - t : t;
                var nv = new double[va.Length]; for (int j = 0; j < va.Length; j++) nv[j] = va[j] + (vb[j] - va[j]) * t2;
                lst.Add(nv);
                midattr[key] = lst.Count - 1;
                return midattr[key];
            }
            int[] MidRec(int[] ra, int[] rb, double t)
            {
                var key = (Math.Min(ra[0], rb[0]), Math.Max(ra[0], rb[0]), PyMath.Round(ra[0] < rb[0] ? t : 1 - t, 4));
                if (!midpos.ContainsKey(key))
                {
                    double[] pa = m.Pos[ra[0]], pb = m.Pos[rb[0]];
                    var np = new double[pa.Length]; for (int j = 0; j < pa.Length; j++) np[j] = pa[j] + (pb[j] - pa[j]) * t;
                    m.Pos.Add(np); midpos[key] = m.Pos.Count - 1;
                }
                var rec = new List<int> { midpos[key], LerpEntry("uv", ra[1], rb[1], t) };
                rec.Add(m.Norm.Count > 0 ? LerpEntry("norm", ra[2], rb[2], t) : 0);
                if (m.HasCol) rec.Add(LerpEntry("col", ra[3], rb[3], t));
                return rec.ToArray();
            }
            bool changed = true; int guard = 0;
            while (changed && guard < 30)
            {
                changed = false; guard++;
                var outp = new List<(int mat, int[][] recs)>();
                foreach (var (mat, recs) in tris)
                {
                    bool done = false;
                    for (int e = 0; e < 3; e++)
                    {
                        int[] ra = recs[e], rb = recs[(e + 1) % 3], rc = recs[(e + 2) % 3];
                        if (SegOfEdge(ra[0], rb[0]))
                        {
                            var mrec = MidRec(ra, rb, 0.5);
                            outp.Add((mat, new[] { ra, mrec, rc })); outp.Add((mat, new[] { mrec, rb, rc }));
                            changed = true; done = true; break;
                        }
                    }
                    if (!done) outp.Add((mat, recs));
                }
                tris = outp;
            }
            var cols = AllColumns(chains); int nmoved = 0;
            foreach (var p in m.Pos) foreach (var c in cols) if (ColMatch(p, c)) { p[0] += WbProfile(p[2]); nmoved++; break; }
            var bymat = new List<(int mat, List<int[]> recs)>();
            foreach (var (mat, recs) in tris)
            {
                int k = bymat.FindIndex(x => x.mat == mat);
                if (k < 0) { bymat.Add((mat, new List<int[]>())); k = bymat.Count - 1; }
                bymat[k].recs.AddRange(recs);
            }
            m.Submeshes = bymat.Select(x => (3, x.mat, x.recs)).ToList();
            m.Hdr[5] = (uint)m.Uv.Count; m.Hdr[11] = (uint)m.Norm.Count; if (m.HasCol) m.Hdr[7] = (uint)m.Col.Count;
            int stride = m.HasCol ? 4 : 3;
            int dlLen = 16 + m.Submeshes.Sum(s => 12 + s.recs.Count * stride * 4);
            var blocklens = new Dictionary<string, int> { ["POS"] = 16 * m.Pos.Count, ["UV"] = 16 * m.Uv.Count, ["NORM"] = 16 * m.Norm.Count, ["COL"] = m.HasCol ? 16 * m.Col.Count : 0, ["DL"] = dlLen, ["MAT"] = m.Materials.Sum(x => x.Length) };
            foreach (string nm in m.Order) m.Pads[nm] = new byte[(-blocklens[nm]) & 15];
            byte[] outb = m.Build();
            foreach (int w in new[] { 4, 6, 10, 12, 14 }) if (IsoBytes.U32(outb, w * 4) % 16 != 0) throw new IOException($"block offset 0x{IsoBytes.U32(outb, w * 4):x} misaligned");
            return (outb, nmoved);
        }

        // ───────────────────────── collision MDT edit ─────────────────────────
        private static (byte[] mdt, int moved) EditCollMdt(byte[] scn, int fo, List<List<double[]>> chains)
        {
            int POS = (int)IsoBytes.U32(scn, fo + 0x10), DL = (int)IsoBytes.U32(scn, fo + 0x28), total = (int)IsoBytes.U32(scn, fo + 8);
            int tc = (int)IsoBytes.U32(scn, fo + DL + 0x14);
            var recs = new List<int[]>();
            for (int t = 0; t < tc; t++) { var r = new int[5]; for (int k = 0; k < 5; k++) r[k] = BitConverter.ToInt32(scn, fo + DL + 0x18 + t * 0x14 + k * 4); recs.Add(r); }
            int nv = recs.SelectMany(r => r.Take(3)).Max() + 1;
            var pos = new List<double[]>();
            for (int i = 0; i < nv; i++) pos.Add(new double[] { IsoBytes.F32(scn, fo + POS + i * 0x10), IsoBytes.F32(scn, fo + POS + i * 0x10 + 4), IsoBytes.F32(scn, fo + POS + i * 0x10 + 8), IsoBytes.F32(scn, fo + POS + i * 0x10 + 12) });
            var segs = Segments(chains);
            var midcache = new Dictionary<(int, int), int>();
            bool SegHit(int ia, int ib)
            {
                double[] pa = pos[ia], pb = pos[ib];
                foreach (var (a, b) in segs) if ((ColMatch(pa, a) && ColMatch(pb, b)) || (ColMatch(pa, b) && ColMatch(pb, a))) return true;
                return false;
            }
            int Midpoint(int ia, int ib)
            {
                var key = (Math.Min(ia, ib), Math.Max(ia, ib));
                if (!midcache.ContainsKey(key)) { pos.Add(new[] { (pos[ia][0] + pos[ib][0]) / 2, (pos[ia][1] + pos[ib][1]) / 2, (pos[ia][2] + pos[ib][2]) / 2, (pos[ia][3] + pos[ib][3]) / 2 }); midcache[key] = pos.Count - 1; }
                return midcache[key];
            }
            bool changed = true; int guard = 0;
            while (changed && guard < 30)
            {
                changed = false; guard++;
                var outp = new List<int[]>();
                foreach (var r in recs)
                {
                    bool done = false;
                    for (int e = 0; e < 3; e++)
                    {
                        int ia = r[e], ib = r[(e + 1) % 3];
                        if (SegHit(ia, ib))
                        {
                            int mi = Midpoint(ia, ib);
                            var r1 = (int[])r.Clone(); var r2 = (int[])r.Clone();
                            r1[(e + 1) % 3] = mi; r2[e] = mi;
                            outp.Add(r1); outp.Add(r2); changed = true; done = true; break;
                        }
                    }
                    if (!done) outp.Add(r);
                }
                recs = outp;
            }
            var cols = AllColumns(chains); int nmoved = 0;
            foreach (var p in pos) foreach (var c in cols) if (ColMatch(p, c)) { p[0] += WbProfile(p[2]); nmoved++; break; }
            var o = new List<byte>(scn.AsSpan(fo, total).ToArray());
            while (o.Count % 16 != 0) o.Add(0);
            int newPos = o.Count;
            foreach (var p in pos) for (int k = 0; k < 4; k++) o.AddRange(BitConverter.GetBytes((float)p[k]));
            int newDl = o.Count;
            o.AddRange(scn.AsSpan(fo + DL, 0x18).ToArray());
            foreach (var r in recs) for (int k = 0; k < 5; k++) o.AddRange(BitConverter.GetBytes(r[k]));
            while (o.Count % 16 != 0) o.Add(0);
            var ob = o.ToArray();
            IsoBytes.U32(ob, 0x08, (uint)ob.Length); IsoBytes.U32(ob, 0x0c, (uint)pos.Count); IsoBytes.U32(ob, 0x10, (uint)newPos); IsoBytes.U32(ob, 0x28, (uint)newDl);
            IsoBytes.U32(ob, newDl + 0x14, (uint)recs.Count);
            return (ob, nmoved);
        }

        // ───────────────────────── MDS container re-lay ─────────────────────────
        /// <summary>The nested MDS at mdsOff rebuilt with per-node-name replacement MDT bytes, optionally appending new nodes
        /// (table entries cloned from templateNode with their own name, index and mesh offset).</summary>
        private static byte[] RelayMds(byte[] sub, int mdsOff, int mdsSize, Dictionary<string, byte[]> edits, List<(string name, byte[] mdt)> addNodes = null, int templateNode = 1)
        {
            int cnt = (int)IsoBytes.U32(sub, mdsOff + 8), tbl = (int)IsoBytes.U32(sub, mdsOff + 12);
            var nodes = new List<(int i, string nm, int mo)>();
            for (int i = 0; i < cnt; i++) { int b = mdsOff + tbl + i * 0x70; nodes.Add((i, IsoBytes.NameAt(sub, b + 8, 16), BitConverter.ToInt32(sub, b + 0x28))); }
            var order = nodes.Where(n => n.mo != 0).OrderBy(n => n.mo).ToList();
            var blocks = new List<(int i, string nm, byte[] raw)>();
            for (int k = 0; k < order.Count; k++)
            {
                int end = k + 1 < order.Count ? order[k + 1].mo : mdsSize;
                blocks.Add((order[k].i, order[k].nm, sub.AsSpan(mdsOff + order[k].mo, end - order[k].mo).ToArray()));
            }
            addNodes ??= new List<(string, byte[])>();
            var outp = new List<byte>(sub.AsSpan(mdsOff, tbl + cnt * 0x70).ToArray());
            for (int k = 0; k < addNodes.Count; k++)
            {
                var ent = sub.AsSpan(mdsOff + tbl + templateNode * 0x70, 0x70).ToArray();
                IsoBytes.U32(ent, 0, (uint)(cnt + k));
                byte[] nmb = Encoding.Latin1.GetBytes(addNodes[k].name); for (int c = 0; c < 16; c++) ent[8 + c] = c < nmb.Length ? nmb[c] : (byte)0;
                outp.AddRange(ent);
            }
            var arr = outp.ToArray(); IsoBytes.U32(arr, 8, (uint)(cnt + addNodes.Count)); outp = new List<byte>(arr);
            var patches = new List<(int at, int val)>();
            foreach (var (i, nm, raw) in blocks)
            {
                int newMo = outp.Count;
                outp.AddRange(edits.TryGetValue(nm, out var ed) ? ed : raw);
                while (outp.Count % 16 != 0) outp.Add(0);
                patches.Add((tbl + i * 0x70 + 0x28, newMo));
            }
            for (int k = 0; k < addNodes.Count; k++)
            {
                int newMo = outp.Count;
                outp.AddRange(addNodes[k].mdt);
                while (outp.Count % 16 != 0) outp.Add(0);
                patches.Add((tbl + (cnt + k) * 0x70 + 0x28, newMo));
            }
            var res = outp.ToArray();
            foreach (var (at, val) in patches) Array.Copy(BitConverter.GetBytes(val), 0, res, at, 4);
            return res;
        }

        // ───────────────────────── pillar camera hulls + doumu hug ─────────────────────────
        private const double PillarPad = 8.0; private const int HullN = 8; private const double YLo = -10.0, YHi = 130.0;
        private static readonly (string label, string sub, int inst)[] PillarTargets = { ("extru_inner_S", "s1303", 0), ("extru_inner_N", "s1304", 0) };
        private const double BandLo = 20.0, BandHi = 45.0;
        private const double DoumuPull = 20.0, DoumuClear = 4.0; private static readonly double[] DoumuC = { -2.0, -6.0 };

        private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        private static List<List<double[]>> Cluster2(List<double[]> pts)
        {
            int n = pts.Count;
            double cx = 0, cz = 0; foreach (var p in pts) cx += p[0]; cx /= n; foreach (var p in pts) cz += p[1]; cz /= n;
            double sxx = 0, szz = 0, sxz = 0;
            foreach (var p in pts) sxx += (p[0] - cx) * (p[0] - cx); foreach (var p in pts) szz += (p[1] - cz) * (p[1] - cz); foreach (var p in pts) sxz += (p[0] - cx) * (p[1] - cz);
            double ang = 0.5 * Math.Atan2(2 * sxz, sxx - szz), ax = Math.Cos(ang), az = Math.Sin(ang);
            var proj = pts.Select(p => (v: p[0] * ax + p[1] * az, p)).OrderBy(x => x.v).ThenBy(x => x.p[0]).ThenBy(x => x.p[1]).ToList();
            int gapI = 0; double gap = -1.0;
            for (int i = n / 4; i < 3 * n / 4; i++) { double d = proj[i + 1].v - proj[i].v; if (d > gap) { gap = d; gapI = i; } }
            return new List<List<double[]>> { proj.Take(gapI + 1).Select(x => x.p).ToList(), proj.Skip(gapI + 1).Select(x => x.p).ToList() };
        }

        private static List<double[]> Hull(List<double[]> pts0)
        {
            var pts = pts0.Select(p => (p[0], p[1])).Distinct().OrderBy(p => p.Item1).ThenBy(p => p.Item2).Select(p => new[] { p.Item1, p.Item2 }).ToList();
            if (pts.Count < 3) return pts;
            List<double[]> Half(IEnumerable<double[]> seq)
            {
                var h = new List<double[]>();
                foreach (var p in seq)
                {
                    while (h.Count >= 2 && (h[^1][0] - h[^2][0]) * (p[1] - h[^2][1]) - (h[^1][1] - h[^2][1]) * (p[0] - h[^2][0]) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                return h;
            }
            var lo = Half(pts); var hi = Half(Enumerable.Reverse(pts));
            return lo.Take(lo.Count - 1).Concat(hi.Take(hi.Count - 1)).ToList();
        }

        private static List<double[]> Simplify(List<double[]> hull, int n)
        {
            if (hull.Count <= n) return hull;
            double cx = 0, cz = 0; foreach (var p in hull) cx += p[0]; cx /= hull.Count; foreach (var p in hull) cz += p[1]; cz /= hull.Count;
            var bins = new SortedDictionary<int, (double r, double[] p)>();
            foreach (var p in hull)
            {
                int b = PyMath.Mod((int)((Math.Atan2(p[1] - cz, p[0] - cx) + Math.PI) / (2 * Math.PI) * n), n);
                double r = Hypot(p[0] - cx, p[1] - cz);
                if (!bins.ContainsKey(b) || r > bins[b].r) bins[b] = (r, p);
            }
            return bins.Values.Select(x => x.p).ToList();
        }

        private static List<double[][]> Walls(List<List<double[]>> feet)
        {
            var tris = new List<double[][]>();
            foreach (var foot in feet)
            {
                int n = foot.Count; double area2 = 0;
                for (int i = 0; i < n; i++) area2 += foot[i][0] * foot[(i + 1) % n][1] - foot[(i + 1) % n][0] * foot[i][1];
                var loop = area2 < 0 ? foot : Enumerable.Reverse(foot).ToList();
                for (int i = 0; i < n; i++)
                {
                    double[] a = loop[i], b = loop[(i + 1) % n];
                    tris.Add(new[] { new[] { a[0], YLo, a[1] }, new[] { b[0], YLo, b[1] }, new[] { b[0], YHi, b[1] } });
                    tris.Add(new[] { new[] { a[0], YLo, a[1] }, new[] { b[0], YHi, b[1] }, new[] { a[0], YHi, a[1] } });
                }
            }
            return tris;
        }

        private static List<(string label, List<double[][]> tris)> PillarHulls(List<SceneScn.PlacedMesh> placed)
        {
            var outp = new List<(string, List<double[][]>)>();
            foreach (var (label, subn, inst) in PillarTargets)
            {
                var verts = new List<double[]>();
                foreach (var pm in placed)
                    if (pm.Sub == subn && pm.Inst == inst && pm.Name.StartsWith("extru", StringComparison.Ordinal))
                        verts.AddRange(pm.Verts.Where(v => BandLo <= v[1] && v[1] <= BandHi).Select(v => new[] { PyMath.Round(v[0], 2), PyMath.Round(v[2], 2) }));
                var feet = new List<List<double[]>>();
                foreach (var leg in Cluster2(verts))
                {
                    var hull = Simplify(Hull(leg), HullN);
                    double cx = 0, cz = 0; foreach (var p in hull) cx += p[0]; cx /= hull.Count; foreach (var p in hull) cz += p[1]; cz /= hull.Count;
                    var padded = new List<double[]>();
                    foreach (var p in hull) { double d = Hypot(p[0] - cx, p[1] - cz); if (d == 0) d = 1.0; padded.Add(new[] { p[0] + (p[0] - cx) / d * PillarPad, p[1] + (p[1] - cz) / d * PillarPad }); }
                    feet.Add(CollisionMdt.Chaikin(padded));
                }
                outp.Add((label, Walls(feet)));
            }
            return outp;
        }

        private static double[] DoumuSectors(List<SceneScn.PlacedMesh> placed)
        {
            var sec = new double[36];
            foreach (var pm in placed)
            {
                if (pm.Name != "sphere27") continue;
                foreach (var v in pm.Verts)
                {
                    if (!(20.0 <= v[1] && v[1] <= 140.0)) continue;
                    double r = Hypot(v[0] - DoumuC[0], v[2] - DoumuC[1]);
                    int i = PyMath.Mod((int)PyMath.FloorDiv(Math.Atan2(v[2] - DoumuC[1], v[0] - DoumuC[0]) * (180.0 / Math.PI), 10), 36);
                    foreach (int j in new[] { i - 1, i, i + 1 }) sec[PyMath.Mod(j, 36)] = Math.Max(sec[PyMath.Mod(j, 36)], r);
                }
            }
            return sec;
        }

        private static (double x, double z) DoumuHugXz(double[] sec, double x, double z)
        {
            double dx = x - DoumuC[0], dz = z - DoumuC[1], r = Hypot(dx, dz);
            if (r <= 1.0) return (x, z);
            double floorR = sec[PyMath.Mod((int)PyMath.FloorDiv(Math.Atan2(dz, dx) * (180.0 / Math.PI), 10), 36)] + DoumuClear;
            double r2 = Math.Max(r - DoumuPull, Math.Min(floorR, r));
            return (DoumuC[0] + dx * r2 / r, DoumuC[1] + dz * r2 / r);
        }

        // ───────────────────────── the rebuilt s1301 ─────────────────────────
        internal static byte[] RebuildS1301(byte[] scn, byte[] mapinfo, Action<string> log)
        {
            var (soff, ssize) = SceneScn.DirectoryMap(scn)["s1301"];
            if (ssize != VanillaS1301Size) throw new IOException($"s1301 size 0x{ssize:x} != vanilla 0x{VanillaS1301Size:x} — not the vanilla sub");
            byte[] sub = scn.AsSpan(soff, ssize).ToArray();
            const int VisOff = 0xb20, VisSize = 0x39af0, AOff = 0x3a610, ASize = 0x9bd0, COff = 0x441e0, CSize = 0x8870;
            var chains = Chains();
            foreach (var (off, size, tag) in new[] { (VisOff, VisSize, "visual"), (AOff, ASize, "_a"), (COff, CSize, "_c") })
                if (!RelayMds(sub, off, size, new Dictionary<string, byte[]>()).AsSpan().SequenceEqual(sub.AsSpan(off, size))) throw new IOException($"{tag} relay drift");
            // stage 2: visual grid10/grid11
            var editsVis = new Dictionary<string, byte[]>();
            {
                int cnt = (int)IsoBytes.U32(sub, VisOff + 8), tbl = (int)IsoBytes.U32(sub, VisOff + 12);
                for (int i = 0; i < cnt; i++)
                {
                    int b = VisOff + tbl + i * 0x70; string nm = IsoBytes.NameAt(sub, b + 8, 16);
                    if (nm != "grid10" && nm != "grid11") continue;
                    int mo = BitConverter.ToInt32(sub, b + 0x28);
                    var (fresh, moved) = EditVisualMdt(sub, VisOff + mo, chains[nm]);
                    editsVis[nm] = fresh;
                    log($"  {nm} rebuilt ({fresh.Length} bytes, {moved} column verts shifted)");
                }
            }
            // stage 3: collision walls (the nodes holding the station columns)
            Dictionary<string, byte[]> CollEdit(int mdsOff, List<List<double[]>> ch, string tag)
            {
                var edits = new Dictionary<string, byte[]>();
                int cnt2 = (int)IsoBytes.U32(sub, mdsOff + 8), tbl2 = (int)IsoBytes.U32(sub, mdsOff + 12);
                var stations = ch.SelectMany(c => c).ToList();
                for (int i = 0; i < cnt2; i++)
                {
                    int b = mdsOff + tbl2 + i * 0x70; string nm = IsoBytes.NameAt(sub, b + 8, 16);
                    int mo = BitConverter.ToInt32(sub, b + 0x28);
                    if (mo == 0) continue;
                    int fo = mdsOff + mo;
                    if (!(sub[fo] == 'M' && sub[fo + 1] == 'D' && sub[fo + 2] == 'T')) continue;
                    int POS = (int)IsoBytes.U32(sub, fo + 0x10), DL = (int)IsoBytes.U32(sub, fo + 0x28);
                    int tc = (int)IsoBytes.U32(sub, fo + DL + 0x14), nv = 0;
                    for (int t = 0; t < tc; t++) for (int k = 0; k < 3; k++) nv = Math.Max(nv, BitConverter.ToInt32(sub, fo + DL + 0x18 + t * 0x14 + k * 4) + 1);
                    int hitn = 0;
                    for (int vi = 0; vi < nv; vi++)
                    {
                        double[] p = { IsoBytes.F32(sub, fo + POS + vi * 0x10), IsoBytes.F32(sub, fo + POS + vi * 0x10 + 4), IsoBytes.F32(sub, fo + POS + vi * 0x10 + 8) };
                        if (stations.Any(s => ColMatch(p, s))) hitn++;
                    }
                    if (hitn == 0) continue;
                    var (fresh, moved) = EditCollMdt(sub, fo, ch);
                    edits[nm] = fresh;
                    log($"  {tag}/{nm} rebuilt ({hitn} station verts found, {moved} shifted)");
                }
                return edits;
            }
            var editsA = CollEdit(AOff, chains["_a"], "s1301_a");
            var editsC = CollEdit(COff, chains["_c"], "s1301_c");
            if (editsA.Count == 0 || editsC.Count == 0) throw new IOException("collision wall nodes not found");
            // stage 3a2: doumu_c hug — the node's frame carries a scale, so local → world, hug, back through the inverted 3×3
            var placed = SceneScn.PlacedMeshes(scn, mapinfo);
            var sectors = DoumuSectors(placed);
            {
                var (nodes3, wm3) = SceneScn.Accum(sub, COff);
                for (int i = 0; i < nodes3.Count; i++)
                {
                    var nd = nodes3[i];
                    if (nd.Name != "doumu_c" || nd.MeshOff == 0) continue;
                    var M = wm3(i);
                    double a = M[0], b2 = M[4], c2 = M[8], d2 = M[1], e2 = M[5], f2 = M[9], g2 = M[2], h2 = M[6], i2 = M[10];
                    double det = a * (e2 * i2 - f2 * h2) - b2 * (d2 * i2 - f2 * g2) + c2 * (d2 * h2 - e2 * g2);
                    double[] inv = { (e2 * i2 - f2 * h2) / det, (c2 * h2 - b2 * i2) / det, (b2 * f2 - c2 * e2) / det,
                                     (f2 * g2 - d2 * i2) / det, (a * i2 - c2 * g2) / det, (c2 * d2 - a * f2) / det,
                                     (d2 * h2 - e2 * g2) / det, (b2 * g2 - a * h2) / det, (a * e2 - b2 * d2) / det };
                    int fo = COff + nd.MeshOff;
                    int w2 = (int)IsoBytes.U32(sub, fo + 8), w3 = (int)IsoBytes.U32(sub, fo + 12), w4 = (int)IsoBytes.U32(sub, fo + 16);
                    var blk = sub.AsSpan(fo, w2).ToArray(); int nmv = 0;
                    for (int vi = 0; vi < w3; vi++)
                    {
                        int o = w4 + vi * 0x10;
                        double vx = IsoBytes.F32(blk, o), vy = IsoBytes.F32(blk, o + 4), vz = IsoBytes.F32(blk, o + 8); float vw = IsoBytes.F32(blk, o + 12);
                        var w = SceneScn.Xform(M, vx, vy, vz);
                        var (nx, nz) = DoumuHugXz(sectors, w[0], w[2]);
                        if (Math.Abs(nx - w[0]) + Math.Abs(nz - w[2]) > 1e-6)
                        {
                            double rx = nx - M[12], ry2 = w[1] - M[13], rz = nz - M[14];
                            double lx = inv[0] * rx + inv[1] * ry2 + inv[2] * rz, lz = inv[6] * rx + inv[7] * ry2 + inv[8] * rz;
                            IsoBytes.WrF(blk, o, (float)lx); IsoBytes.WrF(blk, o + 4, (float)vy); IsoBytes.WrF(blk, o + 8, (float)lz); IsoBytes.WrF(blk, o + 12, vw);
                            nmv++;
                        }
                    }
                    editsC["doumu_c"] = blk;
                    log($"  doumu_c hugged ({nmv}/{w3} verts pulled in)");
                }
            }
            // stage 3b: pillar camera hulls as new _c nodes
            var nameMap = new Dictionary<string, string> { ["extru_inner_S"] = "pcam_xs", ["extru_inner_N"] = "pcam_xn" };
            var addC = PillarHulls(placed).Where(h => nameMap.ContainsKey(h.label)).Select(h => (nameMap[h.label], CollisionMdt.Build(h.tris, yShift: 210.0))).ToList();
            log($"  {addC.Count} pillar-hull camera nodes");
            // stage 4: assemble the new sub
            byte[] vis = RelayMds(sub, VisOff, VisSize, editsVis), amds = RelayMds(sub, AOff, ASize, editsA), cmds = RelayMds(sub, COff, CSize, editsC, addC);
            var fresh2 = new List<byte>(sub.Take(VisOff)); fresh2.AddRange(vis); fresh2.AddRange(amds); fresh2.AddRange(cmds);
            var outp = fresh2.ToArray();
            int aOff = VisOff + vis.Length, cOff = VisOff + vis.Length + amds.Length;
            foreach (int o in new[] { 0x4c, 0x50, 0x54, 0x78 }) IsoBytes.U32(outp, o, (uint)aOff);
            IsoBytes.U32(outp, 0x58, (uint)vis.Length); IsoBytes.U32(outp, 0x7c, (uint)amds.Length);
            foreach (int o in new[] { 0x90, 0xa8, 0xc0 }) IsoBytes.U32(outp, o, (uint)cOff);
            IsoBytes.U32(outp, 0xc4, (uint)cmds.Length);
            log($"  new s1301 = {outp.Length} bytes (was {ssize}, +{outp.Length - ssize})");
            return outp;
        }

        /// <summary>Player-collision wall band along the bulged west-bank waterline, bank top down to the fish depth, plus the
        /// pillar-base tris — the Yellow Drops fishing DCFC bin (map 23).</summary>
        internal static List<double[][]> WestbankFishWalls()
        {
            var chain = new List<double[]> { new[] { -485.0, 16.0, -197.0 } };   // the existing shoreline vert NW of the section
            var et = WbRows["edge_top"];
            for (int i = 0; i + 1 < et.Length; i++)
            {
                chain.Add((double[])et[i].Clone());
                for (int k = 1; k <= WbSubdiv; k++) chain.Add(Lerp3(et[i], et[i + 1], k));
            }
            chain.Add((double[])et[^1].Clone());
            for (int i = 0; i < chain.Count; i++) chain[i] = new[] { chain[i][0] + (i > 0 ? WbProfile(chain[i][2]) : 0.0), chain[i][1], chain[i][2] };
            var outp = new List<double[][]>();
            for (int i = 0; i + 1 < chain.Count; i++)
            {
                double[] a = chain[i], b = chain[i + 1];
                double[] ab = { a[0], FishWallBottom, a[2] }, bb = { b[0], FishWallBottom, b[2] };
                outp.Add(new[] { new[] { a[0], a[1], a[2] }, new[] { b[0], b[1], b[2] }, bb });
                outp.Add(new[] { new[] { a[0], a[1], a[2] }, (double[])bb.Clone(), ab });
            }
            outp.AddRange(CollisionMdt.TrisFrom(SceneCollisionData.YellowDropsPillarBaseTris));
            return outp;
        }
    }
}
