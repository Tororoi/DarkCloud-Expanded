using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Collision MDTs as CreateCollisionMDT reads them: total @w2, verts @w4 (stride 0x10 XYZW), display list @w10
    /// with the triangle count @DL+0x14 and records @DL+0x18 of five int32 (three POS indices, a colour index, pad), colour
    /// block @w14 (0 = none). Plus the triangle-soup helpers the bakes share (kd-split, Chaikin).</summary>
    internal static class CollisionMdt
    {
        /// <summary>Triangles (vertex triples) of a collision MDT in POS-block space; empty when not an MDT.</summary>
        internal static List<double[][]> Parse(byte[] scn, int mdt)
        {
            var outp = new List<double[][]>();
            if (IsoBytes.U32(scn, mdt) != 0x54444d) return outp;
            int POS = (int)IsoBytes.U32(scn, mdt + 0x10), DL = (int)IsoBytes.U32(scn, mdt + 0x28);
            int tc = (int)IsoBytes.U32(scn, mdt + DL + 0x14), rb = mdt + DL + 0x18;
            double[] V(int i) => new double[] { IsoBytes.F32(scn, mdt + POS + i * 0x10), IsoBytes.F32(scn, mdt + POS + i * 0x10 + 4), IsoBytes.F32(scn, mdt + POS + i * 0x10 + 8) };
            for (int t = 0; t < tc; t++)
                outp.Add(new[] { V(BitConverter.ToInt32(scn, rb + t * 0x14)), V(BitConverter.ToInt32(scn, rb + t * 0x14 + 4)), V(BitConverter.ToInt32(scn, rb + t * 0x14 + 8)) });
            return outp;
        }

        /// <summary>A collision MDT from triangles: vertices deduped on their 3-decimal rounding, one DL record per triangle.
        /// <paramref name="attrs"/> = optional per-triangle 16-byte colour entries (event-trigger tags), <paramref name="yShift"/>
        /// for node frames that sit at T = (0, −yShift, 0).</summary>
        internal static byte[] Build(List<double[][]> tris, bool colour = false, List<byte[]> attrs = null, double yShift = 0.0)
        {
            var index = new Dictionary<(double, double, double), int>();
            var verts = new List<double[]>();
            var idx = new List<int[]>();
            foreach (var t in tris)
            {
                var tri = new int[3];
                for (int k = 0; k < 3; k++)
                {
                    var p = t[k];
                    var key = (PyMath.Round(p[0], 3), PyMath.Round(p[1] + yShift, 3), PyMath.Round(p[2], 3));
                    if (!index.TryGetValue(key, out int i)) { i = verts.Count; index[key] = i; verts.Add(new[] { p[0], p[1] + yShift, p[2] }); }
                    tri[k] = i;
                }
                idx.Add(tri);
            }
            int vc = verts.Count, tc = idx.Count, POS = 0x40;
            var posBytes = new byte[vc * 16];
            for (int i = 0; i < vc; i++) { IsoBytes.WrF(posBytes, i * 16, (float)verts[i][0]); IsoBytes.WrF(posBytes, i * 16 + 4, (float)verts[i][1]); IsoBytes.WrF(posBytes, i * 16 + 8, (float)verts[i][2]); IsoBytes.WrF(posBytes, i * 16 + 12, 1.0f); }
            var entries = new List<byte[]>(); var triCidx = new List<int>();
            if (attrs != null)
            {
                if (attrs.Count != tc) throw new ArgumentException($"attrs len {attrs.Count} != tri count {tc}");
                var seen = new Dictionary<string, int>();
                foreach (var e in attrs)
                {
                    if (e.Length != 0x10) throw new ArgumentException("each attr must be a 16-byte colour entry");
                    string k = Convert.ToBase64String(e);
                    if (!seen.TryGetValue(k, out int j)) { j = entries.Count; seen[k] = j; entries.Add(e); }
                    triCidx.Add(j);
                }
            }
            int DL = (POS + posBytes.Length + 0xF) & ~0xF;
            var dl = new List<byte>();
            foreach (uint w in new uint[] { 0x003400b8, 0x10, (uint)tc, 0x20202020, 3, (uint)tc }) dl.AddRange(BitConverter.GetBytes(w));
            for (int ti = 0; ti < tc; ti++)
            {
                foreach (int v in idx[ti]) dl.AddRange(BitConverter.GetBytes(v));
                dl.AddRange(BitConverter.GetBytes(attrs != null ? triCidx[ti] : 0)); dl.AddRange(BitConverter.GetBytes(0));
            }
            int bodyEnd = DL + dl.Count, colourOff = 0;
            if (attrs != null) { colourOff = (bodyEnd + 0xF) & ~0xF; bodyEnd = colourOff + 0x10 * entries.Count; }
            else if (colour) { colourOff = (bodyEnd + 0xF) & ~0xF; bodyEnd = colourOff + 0x10; }
            int total = (bodyEnd + 0xF) & ~0xF;
            var mdt = new byte[total];
            var hdr = new uint[16];
            hdr[0] = 0x0054444d; hdr[1] = 0x40; hdr[2] = (uint)total; hdr[3] = (uint)vc; hdr[4] = (uint)POS; hdr[5] = (uint)(2 * tc);
            hdr[10] = (uint)DL; hdr[13] = 1; hdr[14] = (uint)colourOff;
            for (int i = 0; i < 16; i++) IsoBytes.U32(mdt, i * 4, hdr[i]);
            Array.Copy(posBytes, 0, mdt, POS, posBytes.Length);
            Array.Copy(dl.ToArray(), 0, mdt, DL, dl.Count);
            if (attrs != null) { int o = colourOff; foreach (var e in entries) { Array.Copy(e, 0, mdt, o, 0x10); o += 0x10; } }
            else if (colour) { mdt[colourOff] = 0x80; mdt[colourOff + 1] = 0x80; mdt[colourOff + 2] = 0x80; mdt[colourOff + 3] = 0x80; }
            return mdt;
        }

        /// <summary>Split the triangle soup along its longest centroid axis (of <paramref name="axes"/>) until each leaf holds at
        /// most <paramref name="maxTris"/>: a median split, or with <paramref name="proportional"/> each side gets its
        /// ceil(n / max) leaf share.</summary>
        internal static List<List<double[][]>> KdSplit(List<double[][]> tris, int maxTris, int[] axes = null, bool proportional = false)
        {
            axes ??= new[] { 0, 1, 2 };
            List<List<double[][]>> Rec(List<double[][]> ts)
            {
                int k = 0;
                if (proportional) { k = (int)Math.Ceiling(ts.Count / (double)maxTris); if (k <= 1) return new List<List<double[][]>> { ts }; }
                else if (ts.Count <= maxTris) return new List<List<double[][]>> { ts };
                var cs = ts.Select(t => new[] { (t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3, (t[0][2] + t[1][2] + t[2][2]) / 3 }).ToList();
                int axis = axes[0]; double best = double.NegativeInfinity;
                foreach (int a in axes) { double ext = cs.Max(c => c[a]) - cs.Min(c => c[a]); if (ext > best) { best = ext; axis = a; } }
                var order = Enumerable.Range(0, ts.Count).OrderBy(i => cs[i][axis]).ToList();
                int mid = proportional ? (int)Math.Round(ts.Count * (double)(k / 2) / k, MidpointRounding.ToEven) : ts.Count / 2;
                var left = order.Take(mid).Select(i => ts[i]).ToList(); var right = order.Skip(mid).Select(i => ts[i]).ToList();
                var r = Rec(left); r.AddRange(Rec(right)); return r;
            }
            return Rec(new List<double[][]>(tris));
        }

        /// <summary>One Chaikin corner-cutting pass on a closed xz polygon: twice the points, rounded corners.</summary>
        internal static List<double[]> Chaikin(List<double[]> poly)
        {
            var outp = new List<double[]>(); int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                outp.Add(new[] { 0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1] });
                outp.Add(new[] { 0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1] });
            }
            return outp;
        }

        internal static double[][] Tri(double[] a, double[] b, double[] c) => new[] { a, b, c };
        internal static double[][] TriFromRow(double[,] rows, int r) => new[] { new[] { rows[r, 0], rows[r, 1], rows[r, 2] }, new[] { rows[r, 3], rows[r, 4], rows[r, 5] }, new[] { rows[r, 6], rows[r, 7], rows[r, 8] } };
        internal static List<double[][]> TrisFrom(double[,] rows) => Enumerable.Range(0, rows.GetLength(0)).Select(r => TriFromRow(rows, r)).ToList();
    }
}
