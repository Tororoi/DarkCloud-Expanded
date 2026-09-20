using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Queens h06 (snake statue) collision: the `_c` camera hull replaced by an open cylinder (a 16-gon at twice the
    /// vanilla mean ring radius, centred on the snake-head summit, doubled height) and the `_a` player collision by the
    /// FULL visual mesh with the authored surgery applied (SceneCollisionData), each a fresh MDS appended at the sub's end
    /// with the header words repointed.</summary>
    internal static class SnakeStatueBakes
    {
        private const int MaxPlayerCollisionTris = 1000;      // one node = the whole model: fewer, larger nodes snag less
        private const double CameraHullScaleY = 2.0, CameraHullRadiusMul = 2.0;
        private const int CameraHullSegments = 16;
        private static readonly double[] CameraHullAxisXz = { -13.80, 49.65 };   // centroid of the two snake-head summit tris

        private static bool TriMatches(double[][] t, double[][] r, double tol = 0.05)
        {
            for (int rot = 0; rot < 3; rot++)
            {
                bool ok = true;
                for (int i = 0; i < 3 && ok; i++) for (int j = 0; j < 3; j++) if (Math.Abs(t[i][j] - r[(i + rot) % 3][j]) > tol) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }

        private static List<double[][]> ApplySurgery(List<double[][]> tris, Action<string> log)
        {
            var remove = CollisionMdt.TrisFrom(SceneCollisionData.SnakeStatueRemoveTris);
            var kept = new List<double[][]>(); int removed = 0;
            foreach (var t in tris)
            {
                if (remove.Any(r => TriMatches(t, r))) { removed++; continue; }
                kept.Add(t);
            }
            if (remove.Count > 0 && removed != remove.Count) log($"  WARNING: {removed} tris removed but {remove.Count} listed — check coords");
            kept.AddRange(CollisionMdt.TrisFrom(SceneCollisionData.SnakeStatueAddTris));
            return kept;
        }

        /// <summary>Every triangle of the part's visual mesh (all mesh nodes, parent-accumulated) in part-local space.</summary>
        private static List<double[][]> FullVisualTris(byte[] sub)
        {
            int mds0 = (int)IsoBytes.U32(sub, 0x48);
            var (nodes, wm) = SceneScn.Accum(sub, mds0);
            var outp = new List<double[][]>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.MeshOff == 0) continue;
                int fo = SceneScn.ResolveMdt(sub, mds0, n.MeshOff);
                if (fo < 0) continue;
                MdtMesh m;
                try { m = MdtMesh.Parse(sub, fo); } catch (Exception) { continue; }
                var M = wm(i);
                var wv = m.Pos.Select(p => SceneScn.Xform(M, p[0], p[1], p[2])).ToList();
                foreach (var tr in SceneScn.Flatten(m)) outp.Add(new[] { (double[])wv[tr[0]].Clone(), (double[])wv[tr[1]].Clone(), (double[])wv[tr[2]].Clone() });
            }
            return outp;
        }

        private static List<double[][]> CameraHullCylinderTris(byte[] sub)
        {
            int cOff = (int)IsoBytes.U32(sub, 0xc0);
            int cnt = (int)IsoBytes.U32(sub, cOff + 8), tbl = (int)IsoBytes.U32(sub, cOff + 12);
            var vs = new List<double[]>();
            for (int i = 0; i < cnt; i++)
            {
                int mo = BitConverter.ToInt32(sub, cOff + tbl + i * 0x70 + 0x28);
                if (mo == 0) continue;
                int fo = cOff + mo; int w4 = (int)IsoBytes.U32(sub, fo + 0x10), w3 = (int)IsoBytes.U32(sub, fo + 0x0C);
                for (int vi = 0; vi < w3; vi++) vs.Add(new double[] { IsoBytes.F32(sub, fo + w4 + vi * 0x10), IsoBytes.F32(sub, fo + w4 + vi * 0x10 + 4), IsoBytes.F32(sub, fo + w4 + vi * 0x10 + 8) });
            }
            double ymax = vs.Max(v => v[1]);
            var bas = vs.Where(v => v[1] < ymax * 0.5).ToList();
            double cx = 0, cz = 0; foreach (var v in bas) cx += v[0]; cx /= bas.Count; foreach (var v in bas) cz += v[2]; cz /= bas.Count;
            double rad = 0; foreach (var v in bas) rad += Math.Sqrt((v[0] - cx) * (v[0] - cx) + (v[2] - cz) * (v[2] - cz)); rad /= bas.Count;
            double hx = CameraHullAxisXz[0], hz = CameraHullAxisXz[1];
            double topY = ymax * CameraHullScaleY, r = rad * CameraHullRadiusMul;
            var ringB = new List<double[]>(); var ringT = new List<double[]>();
            for (int i = 0; i < CameraHullSegments; i++)
            {
                double a = 2 * Math.PI * i / CameraHullSegments;
                ringB.Add(new[] { hx + r * Math.Cos(a), 0.0, hz + r * Math.Sin(a) });
                ringT.Add(new[] { hx + r * Math.Cos(a), topY, hz + r * Math.Sin(a) });
            }
            var tris = new List<double[][]>();
            for (int i = 0; i < CameraHullSegments; i++)
            {
                int j = (i + 1) % CameraHullSegments;
                foreach (var tr0 in new[] { new[] { ringB[i], ringB[j], ringT[j] }, new[] { ringB[i], ringT[j], ringT[i] } })
                {
                    var tr = tr0;
                    double[] ab = { tr[1][0] - tr[0][0], tr[1][1] - tr[0][1], tr[1][2] - tr[0][2] }, ac = { tr[2][0] - tr[0][0], tr[2][1] - tr[0][1], tr[2][2] - tr[0][2] };
                    double[] n = { ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0] };
                    double gx = (tr[0][0] + tr[1][0] + tr[2][0]) / 3 - hx, gz = (tr[0][2] + tr[1][2] + tr[2][2]) / 3 - hz;
                    if (n[0] * gx + n[2] * gz < 0) tr = new[] { tr[0], tr[2], tr[1] };
                    tris.Add(new[] { (double[])tr[0].Clone(), (double[])tr[1].Clone(), (double[])tr[2].Clone() });
                }
            }
            return tris;
        }

        /// <summary>A fresh collision MDS: header cloned from the old one, a root null node and one identity-frame node per chunk
        /// (entries cloned from the old block's first mesh node).</summary>
        private static byte[] BuildCollMds(byte[] oldMds, List<List<double[][]>> chunks, string namePrefix)
        {
            int cnt0 = (int)IsoBytes.U32(oldMds, 8), tbl0 = (int)IsoBytes.U32(oldMds, 12);
            byte[] template = null;
            for (int i = 0; i < cnt0; i++) { int b = tbl0 + i * 0x70; if (BitConverter.ToInt32(oldMds, b + 0x28) != 0) { template = oldMds.AsSpan(b, 0x70).ToArray(); break; } }
            if (template == null) throw new IOException("collision MDS: no mesh node to clone");
            byte[] Entry(int idx, string nm, int mo, int par)
            {
                var e = (byte[])template.Clone();
                Array.Copy(BitConverter.GetBytes(idx), 0, e, 0, 4);
                byte[] nb = Encoding.Latin1.GetBytes(nm); for (int k = 0; k < 16; k++) e[8 + k] = k < nb.Length ? nb[k] : (byte)0;
                Array.Copy(BitConverter.GetBytes(mo), 0, e, 0x28, 4); Array.Copy(BitConverter.GetBytes(par), 0, e, 0x2C, 4);
                float[] ident = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                for (int k = 0; k < 16; k++) IsoBytes.WrF(e, 0x30 + k * 4, ident[k]);
                return e;
            }
            int n = chunks.Count;
            var newC = new List<byte>(oldMds.Take(0x10));
            var hdr = newC.ToArray(); IsoBytes.U32(hdr, 8, (uint)(n + 1)); newC = new List<byte>(hdr);
            newC.AddRange(Entry(0, "null1", 0, -1));
            var mdts = chunks.Select(ch => CollisionMdt.Build(ch, yShift: 0.0)).ToList();
            int pos = 0x10 + (n + 1) * 0x70;
            var offs = new List<int>();
            foreach (var m in mdts) { offs.Add(pos); pos += m.Length + ((-m.Length) & 15); }
            for (int k = 0; k < n; k++) newC.AddRange(Entry(k + 1, $"{namePrefix}{k:D2}", offs[k], 0));
            foreach (var m in mdts) { newC.AddRange(m); newC.AddRange(new byte[(-m.Length) & 15]); }
            return newC.ToArray();
        }

        /// <summary>(the rebuilt e03h06 sub, its original size, player-collision node count).</summary>
        internal static (byte[] sub, int origSize, int nodes) RebuildH06(byte[] scn, Action<string> log)
        {
            var (off, size) = SceneScn.DirectoryMap(scn)["e03h06"];
            var sub = new List<byte>(scn.AsSpan(off, size).ToArray());
            byte[] subBytes = sub.ToArray();
            var vis = FullVisualTris(subBytes);
            int cOff = (int)IsoBytes.U32(subBytes, 0xc0), cSize = (int)IsoBytes.U32(subBytes, 0xc4);
            byte[] newC = BuildCollMds(subBytes.AsSpan(cOff, cSize).ToArray(), new List<List<double[][]>> { CameraHullCylinderTris(subBytes) }, "hc");
            int aOff = (int)IsoBytes.U32(subBytes, 0x78), aSize = (int)IsoBytes.U32(subBytes, 0x7c);
            var chunks = CollisionMdt.KdSplit(ApplySurgery(vis, log), MaxPlayerCollisionTris, new[] { 0, 2 });
            byte[] newA = BuildCollMds(subBytes.AsSpan(aOff, aSize).ToArray(), chunks, "ha");
            while (sub.Count % 16 != 0) sub.Add(0);
            int newAOff = sub.Count; sub.AddRange(newA);
            while (sub.Count % 16 != 0) sub.Add(0);
            int newCOff = sub.Count; sub.AddRange(newC);
            var outp = sub.ToArray();
            IsoBytes.U32(outp, 0x78, (uint)newAOff); IsoBytes.U32(outp, 0x7c, (uint)newA.Length);
            IsoBytes.U32(outp, 0xc0, (uint)newCOff); IsoBytes.U32(outp, 0xc4, (uint)newC.Length);
            return (outp, size, chunks.Count);
        }
    }
}
