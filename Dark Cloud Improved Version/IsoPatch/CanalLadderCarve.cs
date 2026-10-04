using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MdtFloatCodec;
using static Dark_Cloud_Improved_Version.SignPlacements;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The canal ladder: the Factory metal ladder (e05a01/hasigo1) carved from the user's ISO and reshaped for the Queens canal
    /// wall — de-yaw, clip, snap, compact, world-place, re-emit as a kanban-style 1-node MDS with world-baked verts (mapinfo GROUND
    /// "hasigo" places it at the origin). LadderWorldX is the ladder's world X (SignPlacements.LadderClimbBottom/LadderClimbTop
    /// derive their climb points from it).
    /// </summary>
    internal static class CanalLadderCarve
    {
        // ── de-yaw ~9.5° so the rails run parallel to X, clip the bottom off at the mid-rung gap (y=22) with edge
        //    interpolation so the rails stay watertight, snap the cut ring to the floor and shift so the donor's ground
        //    mount lands on the walkway (y=70), compact, then translate to the world placement (centred x=700, feet on
        //    the walkway). ──
        internal const float LadderCutY = 22f, LadderSnapY = 20f, LadderShiftY = 20f, LadderWorldX = 706f, LadderFeetZ = 52f;

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

            // 2) clip everything below LadderCutY, interpolating a new vert on each crossing edge
            int firstNew = m.pos.Count, stride = m.hasCol ? 4 : 3;
            var cache = new Dictionary<string, int[]>();
            int[] CutVert(int[] rA, int[] rB)
            {
                bool aFirst = string.CompareOrdinal(string.Join(",", rA), string.Join(",", rB)) <= 0;
                int[] a = aFirst ? rA : rB, b = aFirst ? rB : rA;
                string key = string.Join(",", a) + "|" + string.Join(",", b);
                if (cache.TryGetValue(key, out var got)) return got;
                float[] pa = m.pos[a[0]], pb = m.pos[b[0]];
                float t = (LadderCutY - pa[1]) / (pb[1] - pa[1]);
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
                        bool inA = m.pos[A[0]][1] >= LadderCutY, inB = m.pos[B[0]][1] >= LadderCutY;
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
                m.pos[i][1] = (i >= firstNew ? LadderSnapY : m.pos[i][1]) - LadderShiftY;

            // 4) compact: drop the now-unreferenced (clipped-away) verts from every stream
            CompactStream(m, 0, m.pos); CompactStream(m, 1, m.uv);
            if (m.norm.Count > 0) CompactStream(m, 2, m.norm);
            if (m.hasCol) CompactStream(m, 3, m.col);
        }

        internal static void WorldPlace(Mdt m)
        {
            float minx = float.MaxValue, maxx = float.MinValue, feet = float.MinValue;
            foreach (var v in m.pos) { minx = Math.Min(minx, v[0]); maxx = Math.Max(maxx, v[0]); if (v[1] > 69) feet = Math.Max(feet, v[2]); }
            float dx = LadderWorldX - (minx + maxx) / 2, dz = LadderFeetZ - feet;
            foreach (var v in m.pos) { v[0] += dx; v[2] += dz; }
        }

        internal static byte[] CarveLadder(byte[] scene)
        {
            // Scope to the e05a01 PART (the node name also appears in a name table before the geometry, so a
            // bare string search grabs the wrong one): part-table entry -> its MDS -> node-table scan.
            int nParts = (int)U32(scene, 4), poff = -1;
            for (int i = 0; i < nParts; i++) { int e = 0x10 + i * 0x30; if (NameAt(scene, e, 0x10) == LadderDonorPart) { poff = (int)U32(scene, e + 0x10); break; } }
            if (poff < 0) throw new IOException($"Ladder part {LadderDonorPart} not found in the ISO.");
            int mds = FindFrom(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, poff);
            if (mds < 0) throw new IOException("Ladder part MDS not found.");
            int tbl = mds + (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8), no = -1;
            for (int i = 0; i < count; i++) { int c = tbl + i * 0x70; if (NameAt(scene, c + 8, 0x20) == LadderDonorNode) { no = c; break; } }
            if (no < 0) throw new IOException($"{LadderDonorNode} node index not found.");
            int meshOff = (int)U32(scene, no + 0x28);
            int mdt = (scene[mds + meshOff] == 'M') ? mds + meshOff : meshOff;   // meshOff is block-relative
            if (!(scene[mdt] == 'M' && scene[mdt + 1] == 'D' && scene[mdt + 2] == 'T')) throw new IOException("ladder MDT not resolved.");

            var m = Parse(scene, mdt);
            CarveMesh(m); WorldPlace(m);
            byte[] mdtBytes = Build(m);

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
