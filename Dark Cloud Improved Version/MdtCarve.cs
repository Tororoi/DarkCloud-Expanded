using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Visual-MDT parse / carve / rebuild toolkit (C# port of tools/carve_ladder.py) and the canal-ladder
    /// carve that uses it: de-yaw, clip, snap, compact, world-place, re-emit. LAD_X is the ladder's world X
    /// (IsoPatcher.LAD_BOTTOM/LAD_TOP derive their climb points from it).
    /// </summary>
    internal static class MdtCarve
    {
        // ── canal ladder: carve the Factory metal ladder (e05a01/hasigo1) from the user's ISO and reshape it
        //    for the Queens canal wall. Faithful C# port of tools/carve_ladder.py (the reference the viewer
        //    renders): de-yaw ~9.5° so the rails run parallel to X, clip the bottom off at the mid-rung gap
        //    (y=22) with edge interpolation so the rails stay watertight, snap the cut ring to the floor and
        //    shift so the donor's ground mount lands on the walkway (y=70), compact, then translate to the
        //    world placement (centred x=700, feet on the walkway). Emitted as a kanban-style 1-node MDS with
        //    world-baked verts (mapinfo GROUND "hasigo" places it at the origin). ──
        internal const float LAD_CUT_Y = 22f, LAD_SNAP_Y = 20f, LAD_SHIFT = 20f, LAD_X = 706f, LAD_FEET_Z = 52f;

        internal sealed class Mdt
        {
            public uint[] hw; public int[] preamble; public bool hasCol;
            public List<float[]> pos, uv, norm, col;                      // col null when absent
            public List<(int prim, int mat, List<int[]> recs)> subs;
            public List<byte[]> mats;
        }

        internal static List<float[]> ReadVecs(byte[] s, int b, int n)
        {
            var v = new List<float[]>(n);
            for (int i = 0; i < n; i++)
                v.Add(new[] { BitConverter.ToSingle(s, b + i * 16), BitConverter.ToSingle(s, b + i * 16 + 4),
                              BitConverter.ToSingle(s, b + i * 16 + 8), BitConverter.ToSingle(s, b + i * 16 + 12) });
            return v;
        }

        internal static Mdt MdtParse(byte[] s, int fo)
        {
            var m = new Mdt { hw = new uint[16] };
            for (int i = 0; i < 16; i++) m.hw[i] = U32(s, fo + i * 4);
            int total = (int)m.hw[2], nPos = (int)m.hw[3], POS = (int)m.hw[4], UV = (int)m.hw[6];
            uint COL = m.hw[8]; int DL = (int)m.hw[10], NORM = (int)m.hw[12], MAT = (int)m.hw[14];
            m.hasCol = COL > 0 && COL < 0x80000000; int stride = m.hasCol ? 4 : 3;
            m.preamble = new int[4]; for (int i = 0; i < 4; i++) m.preamble[i] = (int)U32(s, fo + DL + i * 4);
            int numsub = m.preamble[2], o = DL + 0x10;
            m.subs = new();
            for (int si = 0; si < numsub; si++)
            {
                int prim = (int)U32(s, fo + o), vcnt = (int)U32(s, fo + o + 4), midx = (int)U32(s, fo + o + 8); o += 0xC;
                var recs = new List<int[]>(vcnt);
                for (int r = 0; r < vcnt; r++)
                {
                    var rec = new int[stride];
                    for (int k = 0; k < stride; k++) rec[k] = (int)U32(s, fo + o + (r * stride + k) * 4);
                    recs.Add(rec);
                }
                o += vcnt * stride * 4;
                m.subs.Add((prim, midx, recs));
            }
            int nUV = 0, nNorm = 0, nCol = 0;
            foreach (var sub in m.subs) foreach (var r in sub.recs)
            { nUV = Math.Max(nUV, r[1] + 1); nNorm = Math.Max(nNorm, r[2] + 1); if (m.hasCol) nCol = Math.Max(nCol, r[3] + 1); }
            m.pos = ReadVecs(s, fo + POS, nPos);
            m.uv = ReadVecs(s, fo + UV, nUV);
            m.norm = NORM > 0 ? ReadVecs(s, fo + NORM, nNorm) : new();
            m.col = m.hasCol ? ReadVecs(s, fo + (int)COL, nCol) : null;
            int nmat = (total - MAT) / 0x60;
            m.mats = new();
            for (int i = 0; i < nmat; i++) { var mb = new byte[0x60]; Array.Copy(s, fo + MAT + i * 0x60, mb, 0, 0x60); m.mats.Add(mb); }
            return m;
        }

        internal static float[] Lerp(float[] a, float[] b, float t)
        { var o = new float[4]; for (int i = 0; i < 4; i++) o[i] = a[i] + (b[i] - a[i]) * t; return o; }

        internal static IEnumerable<int[][]> TrisOf(int prim, List<int[]> recs)
        {
            if (prim == 3) for (int i = 0; i + 2 < recs.Count; i += 3) yield return new[] { recs[i], recs[i + 1], recs[i + 2] };
            else if (prim == 4) for (int i = 0; i + 2 < recs.Count; i++)
                yield return (i & 1) == 1 ? new[] { recs[i], recs[i + 2], recs[i + 1] } : new[] { recs[i], recs[i + 1], recs[i + 2] };
        }

        internal static void CarveMesh(Mdt m)
        {
            // 1) de-yaw: measure dz/dx of the rail-plane verts (y<85, z<-40), rotate pos + norm by -that about Y
            double mx = 0, mz = 0; int cnt = 0;
            foreach (var v in m.pos) if (v[1] < 85 && v[2] < -40) { mx += v[0]; mz += v[2]; cnt++; }
            mx /= cnt; mz /= cnt;
            double num = 0, den = 0;
            foreach (var v in m.pos) if (v[1] < 85 && v[2] < -40) { num += (v[0] - mx) * (v[2] - mz); den += (v[0] - mx) * (v[0] - mx); }
            double th = Math.Atan2(num, den); float c = (float)Math.Cos(th), s = (float)Math.Sin(th);
            void RotY(List<float[]> vs) { foreach (var v in vs) { float x = v[0], z = v[2]; v[0] = x * c + z * s; v[2] = -x * s + z * c; } }
            // ⚠ For this mesh the block roles are the reverse of their header labels: hw[6] (m.uv) holds the
            // per-vertex NORMALS (unit 3-vectors) and hw[12] (m.norm) holds the TRUE flat texture coords
            // (V tracks height; maps 100% onto e05t06's gray metal region). Rotate positions + real normals;
            // the texture coords are rotation-invariant and MUST stay untouched, or the ladder samples random
            // atlas cells in-game (the gray/gold/brown garble). Only spatial data (pos, normals) de-yaws.
            RotY(m.pos); if (m.uv.Count > 0) RotY(m.uv);

            // 2) clip everything below LAD_CUT_Y, interpolating a new vert on each crossing edge
            int firstNew = m.pos.Count, stride = m.hasCol ? 4 : 3;
            var cache = new Dictionary<string, int[]>();
            int[] CutVert(int[] rA, int[] rB)
            {
                bool aFirst = string.CompareOrdinal(string.Join(",", rA), string.Join(",", rB)) <= 0;
                int[] a = aFirst ? rA : rB, b = aFirst ? rB : rA;
                string key = string.Join(",", a) + "|" + string.Join(",", b);
                if (cache.TryGetValue(key, out var got)) return got;
                float[] pa = m.pos[a[0]], pb = m.pos[b[0]];
                float t = (LAD_CUT_Y - pa[1]) / (pb[1] - pa[1]);
                m.pos.Add(Lerp(pa, pb, t)); m.uv.Add(Lerp(m.uv[a[1]], m.uv[b[1]], t));
                var rec = new int[stride]; rec[0] = m.pos.Count - 1; rec[1] = m.uv.Count - 1;
                if (m.norm.Count > 0) { m.norm.Add(Lerp(m.norm[a[2]], m.norm[b[2]], t)); rec[2] = m.norm.Count - 1; } else rec[2] = 0;
                if (m.hasCol) { m.col.Add(Lerp(m.col[a[3]], m.col[b[3]], t)); rec[3] = m.col.Count - 1; }
                cache[key] = rec; return rec;
            }
            var newSubs = new List<(int, int, List<int[]>)>();
            foreach (var (prim, midx, recs) in m.subs)
            {
                var outRecs = new List<int[]>();
                foreach (var tri in TrisOf(prim, recs))
                {
                    var poly = new List<int[]>();
                    for (int i = 0; i < 3; i++)
                    {
                        int[] A = tri[i], B = tri[(i + 1) % 3];
                        bool inA = m.pos[A[0]][1] >= LAD_CUT_Y, inB = m.pos[B[0]][1] >= LAD_CUT_Y;
                        if (inA) poly.Add(A);
                        if (inA != inB) poly.Add(CutVert(A, B));
                    }
                    // clone each emitted record: strip sources share a record across triangles, and the
                    // per-slot in-place compaction below must see every list position as a distinct object
                    for (int k = 1; k + 1 < poly.Count; k++)
                    { outRecs.Add((int[])poly[0].Clone()); outRecs.Add((int[])poly[k].Clone()); outRecs.Add((int[])poly[k + 1].Clone()); }
                }
                if (outRecs.Count > 0) newSubs.Add((3, midx, outRecs));
            }
            m.subs = newSubs.ConvertAll(x => (x.Item1, x.Item2, x.Item3));

            // 3) snap the cut ring to the floor + shift so the ground mount lands on the walkway
            for (int i = 0; i < m.pos.Count; i++)
                m.pos[i][1] = (i >= firstNew ? LAD_SNAP_Y : m.pos[i][1]) - LAD_SHIFT;

            // 4) compact: drop the now-unreferenced (clipped-away) verts from every stream
            CompactStream(m, 0, m.pos); CompactStream(m, 1, m.uv);
            if (m.norm.Count > 0) CompactStream(m, 2, m.norm);
            if (m.hasCol) CompactStream(m, 3, m.col);
        }

        internal static void CompactStream(Mdt m, int slot, List<float[]> stream)
        {
            var used = new SortedSet<int>();
            foreach (var sub in m.subs) foreach (var r in sub.recs) used.Add(r[slot]);
            var remap = new Dictionary<int, int>(); var ns = new List<float[]>();
            foreach (int o in used) { remap[o] = ns.Count; ns.Add(stream[o]); }
            stream.Clear(); stream.AddRange(ns);
            foreach (var sub in m.subs) foreach (var r in sub.recs) r[slot] = remap[r[slot]];
        }

        internal static void WorldPlace(Mdt m)
        {
            float minx = float.MaxValue, maxx = float.MinValue, feet = float.MinValue;
            foreach (var v in m.pos) { minx = Math.Min(minx, v[0]); maxx = Math.Max(maxx, v[0]); if (v[1] > 69) feet = Math.Max(feet, v[2]); }
            float dx = LAD_X - (minx + maxx) / 2, dz = LAD_FEET_Z - feet;
            foreach (var v in m.pos) { v[0] += dx; v[2] += dz; }
        }

        internal static byte[] MdtBuild(Mdt m)
        {
            int stride = m.hasCol ? 4 : 3;
            var dl = new List<byte>();
            void PutI(List<byte> b, int v) => b.AddRange(BitConverter.GetBytes(v));
            PutI(dl, m.preamble[0]); PutI(dl, m.preamble[1]); PutI(dl, m.subs.Count); PutI(dl, m.preamble[3]);
            foreach (var (prim, midx, recs) in m.subs)
            { PutI(dl, prim); PutI(dl, recs.Count); PutI(dl, midx); foreach (var r in recs) for (int k = 0; k < stride; k++) PutI(dl, r[k]); }
            byte[] VecBytes(List<float[]> vs)
            { var b = new byte[vs.Count * 16]; for (int i = 0; i < vs.Count; i++) for (int k = 0; k < 4; k++) Array.Copy(BitConverter.GetBytes(vs[i][k]), 0, b, i * 16 + k * 4, 4); return b; }
            byte[] matBytes = new byte[m.mats.Count * 0x60];
            for (int i = 0; i < m.mats.Count; i++) Array.Copy(m.mats[i], 0, matBytes, i * 0x60, 0x60);

            var outb = new List<byte>(new byte[0x40]);
            int Emit(byte[] blk) { while ((outb.Count & 0xF) != 0) outb.Add(0); int off = outb.Count; outb.AddRange(blk); return off; }
            int posOff = Emit(VecBytes(m.pos)), dlOff = Emit(dl.ToArray()), uvOff = Emit(VecBytes(m.uv));
            int normOff = m.norm.Count > 0 ? Emit(VecBytes(m.norm)) : 0;
            int colOff = m.hasCol ? Emit(VecBytes(m.col)) : 0;
            int matOff = Emit(matBytes);
            while ((outb.Count & 0xF) != 0) outb.Add(0);

            byte[] o = outb.ToArray();
            var hw = (uint[])m.hw.Clone();
            hw[2] = (uint)o.Length; hw[3] = (uint)m.pos.Count; hw[4] = (uint)posOff; hw[6] = (uint)uvOff;
            hw[8] = m.hasCol ? (uint)colOff : m.hw[8]; hw[9] = (uint)dl.Count; hw[10] = (uint)dlOff;
            hw[12] = m.norm.Count > 0 ? (uint)normOff : 0; hw[14] = (uint)matOff;
            for (int i = 0; i < 16; i++) U32(o, i * 4, hw[i]);
            return o;
        }

        internal static byte[] CarveLadder(byte[] scene)
        {
            // Scope to the e05a01 PART (the node name also appears in a name table before the geometry, so a
            // bare string search grabs the wrong one): part-table entry -> its MDS -> node-table scan.
            int nParts = (int)U32(scene, 4), poff = -1;
            for (int i = 0; i < nParts; i++) { int e = 0x10 + i * 0x30; if (NameAt(scene, e, 0x10) == LADDER_PART) { poff = (int)U32(scene, e + 0x10); break; } }
            if (poff < 0) throw new IOException($"Ladder part {LADDER_PART} not found in the ISO.");
            int mds = FindFrom(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, poff);
            if (mds < 0) throw new IOException("Ladder part MDS not found.");
            int tbl = mds + (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8), no = -1;
            for (int i = 0; i < count; i++) { int c = tbl + i * 0x70; if (NameAt(scene, c + 8, 0x20) == LADDER_NODE) { no = c; break; } }
            if (no < 0) throw new IOException($"{LADDER_NODE} node index not found.");
            int meshOff = (int)U32(scene, no + 0x28);
            int mdt = (scene[mds + meshOff] == 'M') ? mds + meshOff : meshOff;   // meshOff is block-relative
            if (!(scene[mdt] == 'M' && scene[mdt + 1] == 'D' && scene[mdt + 2] == 'T')) throw new IOException("ladder MDT not resolved.");

            var m = MdtParse(scene, mdt);
            CarveMesh(m); WorldPlace(m);
            byte[] mdtBytes = MdtBuild(m);

            // wrap in a 1-node MDS (identity 4x4 — mapinfo places the world-baked verts at the origin)
            var outb = new byte[0x10 + 0x70 + mdtBytes.Length];
            outb[0] = (byte)'M'; outb[1] = (byte)'D'; outb[2] = (byte)'S'; outb[3] = 0;
            U32(outb, 4, 1); U32(outb, 8, 1); U32(outb, 0xC, 0x10);
            const int nOff = 0x10;
            U32(outb, nOff + 4, 0x70);
            byte[] nn = Encoding.Latin1.GetBytes("hasigo"); Array.Copy(nn, 0, outb, nOff + 8, nn.Length);
            U32(outb, nOff + 0x28, 0x80); U32(outb, nOff + 0x2C, 0xFFFFFFFF);
            for (int i = 0; i < 4; i++) Array.Copy(BitConverter.GetBytes(1.0f), 0, outb, nOff + 0x30 + i * 0x14, 4);
            Array.Copy(mdtBytes, 0, outb, nOff + 0x70, mdtBytes.Length);
            return outb;
        }
    }
}
