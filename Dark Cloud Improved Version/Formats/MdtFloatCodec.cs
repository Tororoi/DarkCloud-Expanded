using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A visual MDT mesh in f32 (<see cref="Mdt"/>), parsed and re-emitted in the CANONICAL block order POS / DL / UV / NORM /
    /// COL / MAT, each block and the whole 16-aligned. The companion of <see cref="MdtMesh"/>, which keeps the source's block order and
    /// padding for byte-exact re-injection and holds doubles; this codec is for meshes BUILT or RESHAPED in float arithmetic — the canal
    /// ladder's edge interpolation (CanalLadderCarve) and the runtime's rebuilt meshes (CashModel, GroundShadow) — whose bytes depend on
    /// the f32 maths and the fixed layout. Header = 16 words: +0x08 total, +0x0C vertex count, +0x10 POSITIONS, +0x18 UVs, +0x20
    /// COLOURS (none when 0 or ≥ 0x80000000; presence = 4-int records), +0x24 display-list size, +0x28 display-list offset, +0x30
    /// NORMALS, +0x38 MATERIALS (0x60 B each). Display list: 0x10 preamble (word 2 = submesh count) then per submesh [primType,
    /// vertCount, matIdx] + records [pos, uv, norm(, col)]; prim 3 = list, 4 = strip.</summary>
    internal static class MdtFloatCodec
    {
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

        internal static Mdt Parse(byte[] s, int fo)
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

        /// <summary>Component-wise a + (b − a)·t in f32.</summary>
        internal static float[] Lerp(float[] a, float[] b, float t)
        { var o = new float[4]; for (int i = 0; i < 4; i++) o[i] = a[i] + (b[i] - a[i]) * t; return o; }

        /// <summary>A submesh's triangles as record triples: lists in threes, strips with alternating winding.</summary>
        internal static IEnumerable<int[][]> TrisOf(int prim, List<int[]> recs)
        {
            if (prim == 3) for (int i = 0; i + 2 < recs.Count; i += 3) yield return new[] { recs[i], recs[i + 1], recs[i + 2] };
            else if (prim == 4) for (int i = 0; i + 2 < recs.Count; i++)
                yield return (i & 1) == 1 ? new[] { recs[i], recs[i + 2], recs[i + 1] } : new[] { recs[i], recs[i + 1], recs[i + 2] };
        }

        /// <summary>Drop every entry of <paramref name="stream"/> no record's slot <paramref name="slot"/> references, renumbering the
        /// records (sorted by old index).</summary>
        internal static void CompactStream(Mdt m, int slot, List<float[]> stream)
        {
            var used = new SortedSet<int>();
            foreach (var sub in m.subs) foreach (var r in sub.recs) used.Add(r[slot]);
            var remap = new Dictionary<int, int>(); var ns = new List<float[]>();
            foreach (int o in used) { remap[o] = ns.Count; ns.Add(stream[o]); }
            stream.Clear(); stream.AddRange(ns);
            foreach (var sub in m.subs) foreach (var r in sub.recs) r[slot] = remap[r[slot]];
        }

        internal static byte[] Build(Mdt m)
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
    }
}
