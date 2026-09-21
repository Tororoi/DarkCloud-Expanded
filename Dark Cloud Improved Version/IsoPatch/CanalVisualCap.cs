using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Queens canal west-end visual cap: the missing horizontal underside at y=50 over x[−600,−200] × z[−50,50] (seen
    /// through from the low-tide canal floor), added to `grid1__n` (sub e03g05, two-sided) as its own submesh cloning the
    /// y=50 walkway ledge's render data — four 100-unit quads with ping-pong v so every texcoord stays in the GS-clamped
    /// [0,1] range. The MDT grows, so the node table, the PTS TOC and the SCN directory are all repointed.</summary>
    internal static class CanalVisualCap
    {
        private const string SubName = "e03g05";
        private const int TocSize = 0xE0;
        private static readonly double[] CapXs = { -600.0, -500.0, -400.0, -300.0, -200.0 };
        private static readonly double[] CapV = { 0.0, 0.5, 0.0, 0.5, 0.0 };
        private const double CapY = 50.0, CapZn = -50.0, CapZs = 50.0;

        internal static (byte[] scn, int delta) AddCanalCap(byte[] scn, Action<string> log)
        {
            var dir = SceneScn.DirectoryList(scn);
            var entry = dir.FirstOrDefault(e => e.Name == SubName) ?? throw new IOException($"{SubName} not in scene directory");
            int subOff = entry.Off, subSize = entry.Size;
            byte[] sub = scn.AsSpan(subOff, subSize).ToArray();
            int at = IsoBytes.Find(sub, Encoding.ASCII.GetBytes("grid1__n\0"));
            if (at < 0) throw new IOException("grid1__n not found in e03g05");
            int rec = at - 8;
            int meshOff = BitConverter.ToInt32(sub, rec + 0x28);
            int mdsBase = IsoBytes.FindLast(sub, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, at);
            int mdtOff = mdsBase + meshOff;
            if (!(sub[mdtOff] == 'M' && sub[mdtOff + 1] == 'D' && sub[mdtOff + 2] == 'T')) throw new IOException("grid1__n meshOff does not resolve to an MDT");
            var m = MdtMesh.Parse(sub, mdtOff);
            int oldSize = (int)m.Hdr[2];
            // the cap clones the LEDGE's appearance: the y=50 walkway-top quad at x[−700,−600]
            var ledge = new Dictionary<(double, double), int[]>(); int ledgeSi = -1;
            for (int si = 0; si < m.Submeshes.Count; si++)
            {
                var (prim, midx, recs) = m.Submeshes[si];
                if (prim != 3) continue;
                for (int k = 0; k + 2 < recs.Count; k += 3)
                {
                    int[][] tri = { recs[k], recs[k + 1], recs[k + 2] };
                    var pts = tri.Select(r => m.Pos[r[0]]).ToArray();
                    if (pts.All(pt => Math.Abs(pt[1] - 50.0) < 0.5) && pts.All(pt => -701 <= pt[0] && pt[0] <= -599) && pts.All(pt => -51 <= pt[2] && pt[2] <= 51))
                    {
                        for (int i = 0; i < 3; i++) ledge[(PyMath.Round(pts[i][0], 0), PyMath.Round(pts[i][2], 0))] = (int[])tri[i].Clone();
                        ledgeSi = si;
                    }
                }
            }
            var need = new[] { (-600.0, -50.0), (-700.0, -50.0), (-700.0, 50.0), (-600.0, 50.0) };
            if (ledgeSi < 0 || need.Any(kk => !ledge.ContainsKey(kk))) throw new IOException("ledge quad corners not resolved");
            int upNormalIdx = ledge[(-600.0, -50.0)][1];                 // rec[1] indexes hw[6] = the per-vertex NORMAL
            var tcs = new Dictionary<(double, double), int>();
            foreach (var key in need) { var r = ledge[key]; var tc = m.Norm[r[2]]; tcs[(PyMath.Round(tc[0], 2), PyMath.Round(tc[1], 2))] = r[2]; }   // rec[2] indexes hw[12] = the TEXCOORDS
            foreach (var want in new[] { (0.5, 0.0), (0.0, 0.0), (0.5, 0.5), (0.0, 0.5) })
                if (!tcs.ContainsKey(want)) throw new IOException($"expected texcoord {want} not present on the ledge quad");
            var ledgeE = new Dictionary<(double, double), int> { [(-600.0, -50.0)] = ledge[(-600.0, -50.0)][0], [(-600.0, 50.0)] = ledge[(-600.0, 50.0)][0] };
            double posW = m.Pos[ledgeE[(-600.0, -50.0)]][3];
            int PosIdx(double x, double z)
            {
                var k = (PyMath.Round(x, 0), PyMath.Round(z, 0));
                if (ledgeE.TryGetValue(k, out int have)) return have;
                m.Pos.Add(new[] { x, CapY, z, posW });
                return m.Pos.Count - 1;
            }
            int[] Rec(double x, double z, double v) { double u = z < 0 ? 0.5 : 0.0; return new[] { PosIdx(x, z), upNormalIdx, tcs[(u, PyMath.Round(v, 2))] }; }
            var capRecs = new List<int[]>();
            for (int i = 0; i < 4; i++)
            {
                double x0 = CapXs[i], x1 = CapXs[i + 1], v0 = CapV[i], v1 = CapV[i + 1];
                int[] a = Rec(x0, CapZn, v0), b = Rec(x1, CapZn, v1), c = Rec(x1, CapZs, v1), d = Rec(x0, CapZs, v0);
                capRecs.AddRange(new[] { a, b, c, a, c, d });
            }
            int ledgeMidx = m.Submeshes[ledgeSi].mat;
            m.Submeshes.Add((3, ledgeMidx, capRecs));
            int stride = m.HasCol ? 4 : 3;
            var sizes = new Dictionary<string, int>
            {
                ["POS"] = m.Pos.Count * 16, ["DL"] = 16 + m.Submeshes.Sum(s => 12 + s.recs.Count * stride * 4), ["UV"] = m.Uv.Count * 16,
                ["NORM"] = m.Norm.Count * 16, ["COL"] = m.HasCol ? m.Col.Count * 16 : 0, ["MAT"] = m.Materials.Sum(x => x.Length),
            };
            foreach (string nm in m.Order) m.Pads[nm] = new byte[(16 - sizes[nm] % 16) % 16];
            var newMdt = new List<byte>(m.Build()); while (newMdt.Count % 0x10 != 0) newMdt.Add(0);
            var nb = newMdt.ToArray();
            foreach (var (idx, what) in new[] { (4, "POS"), (6, "UV"), (10, "DL"), (12, "NORM"), (14, "MAT") })
                if (IsoBytes.U32(nb, idx * 4) % 16 != 0) throw new IOException($"{what} block misaligned at {IsoBytes.U32(nb, idx * 4):x}");
            int delta = nb.Length - oldSize;
            if (delta < 0) throw new IOException("cap edit shrank the MDT?!");
            var newSub = sub.Take(mdtOff).Concat(nb).Concat(sub.Skip(mdtOff + oldSize)).ToArray();
            int cnt = (int)IsoBytes.U32(newSub, mdsBase + 8), tbl = (int)IsoBytes.U32(newSub, mdsBase + 12), fixedN = 0;
            for (int i = 0; i < cnt; i++)
            {
                int ro = mdsBase + tbl + i * 0x70; int mo = BitConverter.ToInt32(newSub, ro + 0x28);
                if (mo > meshOff) { Array.Copy(BitConverter.GetBytes(mo + delta), 0, newSub, ro + 0x28, 4); fixedN++; }
            }
            int tocFixed = 0;
            for (int i = 8; i < TocSize; i += 4)
            {
                uint v = IsoBytes.U32(newSub, i);
                if (mdtOff + oldSize <= v && v < subSize) { IsoBytes.U32(newSub, i, v + (uint)delta); tocFixed++; }
            }
            var outp = scn.Take(subOff).Concat(newSub).Concat(scn.Skip(subOff + subSize)).ToArray();
            foreach (var e in dir)
            {
                if (e.Off == subOff) IsoBytes.U32(outp, e.EntryOff + 0x14, (uint)(e.Size + delta));
                else if (e.Off > subOff) IsoBytes.U32(outp, e.EntryOff + 0x10, (uint)(e.Off + delta));
            }
            log($"canal cap: +8 tris in grid1__n (MDT 0x{oldSize:x} -> 0x{nb.Length:x}, delta +0x{delta:x}); {fixedN} meshOffs, {tocFixed} TOC offsets");
            return (outp, delta);
        }
    }
}
