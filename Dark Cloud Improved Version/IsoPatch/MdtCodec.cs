using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A visual MDT mesh parsed to a semantic structure and rebuilt byte for byte (blocks re-laid in their recorded
    /// source order with the captured inter-block padding), so triangles can be added, removed or moved and the mesh
    /// re-injected. Header = 16 words: +0x08 total size, +0x0C vertex count, +0x10 POSITIONS offset, +0x18 UVs, +0x20
    /// COLOURS (none when 0 or ≥ 0x80000000; presence = 4-int records), +0x24 display-list size, +0x28 display-list offset,
    /// +0x30 NORMALS, +0x38 MATERIALS (0x60 B each). Display list: 0x10 preamble (word 2 = submesh count) then per submesh
    /// [primType, vertCount, matIdx] + records [pos, uv, norm(, col)]; prim 3 = list, 4 = strip. Vectors are kept as doubles
    /// (exact for the f32 values read) so edits compute the way the Python did.</summary>
    internal sealed class MdtMesh
    {
        internal uint[] Hdr;
        internal bool HasCol;
        internal List<double[]> Pos, Uv, Norm, Col;
        internal List<(int prim, int mat, List<int[]> recs)> Submeshes;
        internal int[] Preamble;
        internal List<byte[]> Materials;
        internal List<string> Order;
        internal Dictionary<string, byte[]> Pads;

        private static uint U(byte[] b, int o) => IsoBytes.U32(b, o);
        private static List<double[]> ReadVec16(byte[] s, int b, int n)
        {
            var v = new List<double[]>(n);
            for (int i = 0; i < n; i++) v.Add(new double[] { IsoBytes.F32(s, b + i * 16), IsoBytes.F32(s, b + i * 16 + 4), IsoBytes.F32(s, b + i * 16 + 8), IsoBytes.F32(s, b + i * 16 + 12) });
            return v;
        }

        internal static MdtMesh Parse(byte[] scn, int fo)
        {
            if (!(scn[fo] == 'M' && scn[fo + 1] == 'D' && scn[fo + 2] == 'T')) throw new InvalidDataException("not an MDT");
            var hw = new uint[16]; for (int i = 0; i < 16; i++) hw[i] = U(scn, fo + i * 4);
            long total = hw[2];
            int POS = (int)hw[4], UV = (int)hw[6], DLSZ = (int)hw[9], DL = (int)hw[10], NORM = (int)hw[12], MAT = (int)hw[14];
            uint COL = hw[8];
            bool hasCol = COL > 0 && COL < 0x80000000;
            int stride = hasCol ? 4 : 3;
            if (!(0x40 <= total && total <= scn.Length - fo)) throw new InvalidDataException($"implausible total size 0x{total:x}");
            foreach (var (nm, off) in new[] { ("POS", POS), ("UV", UV), ("DL", DL), ("NORM", NORM), ("MAT", MAT) })
                if (off != 0 && !(0x40 <= off && off < total)) throw new InvalidDataException($"{nm} offset 0x{off:x} outside [0x40,0x{total:x})");
            var preamble = new int[4]; for (int i = 0; i < 4; i++) preamble[i] = BitConverter.ToInt32(scn, fo + DL + i * 4);
            int numsub = preamble[2];
            if (!(0 < numsub && numsub <= hw[3] + 1)) throw new InvalidDataException($"implausible submesh count {numsub}");
            int o = DL + 0x10;
            long dlEnd = (0 < DLSZ && DLSZ <= total) ? DL + DLSZ : total;
            var subs = new List<(int, int, List<int[]>)>();
            for (int k = 0; k < numsub; k++)
            {
                if (fo + o + 0xC > fo + total) throw new InvalidDataException("submesh header past MDT end");
                int prim = BitConverter.ToInt32(scn, fo + o), vcnt = BitConverter.ToInt32(scn, fo + o + 4), midx = BitConverter.ToInt32(scn, fo + o + 8);
                o += 0xC;
                if (vcnt < 0 || o + (long)vcnt * stride * 4 > dlEnd + 0x10) throw new InvalidDataException($"submesh vcnt {vcnt} overruns display list");
                var recs = new List<int[]>(vcnt);
                for (int r = 0; r < vcnt; r++) { var rec = new int[stride]; for (int c = 0; c < stride; c++) rec[c] = BitConverter.ToInt32(scn, fo + o + (r * stride + c) * 4); recs.Add(rec); }
                o += vcnt * stride * 4;
                subs.Add((prim, midx, recs));
            }
            int npos = (int)hw[3];
            int nuv = subs.SelectMany(s => s.Item3).Select(r => r[1]).DefaultIfEmpty(-1).Max() + 1;
            int nnorm = subs.SelectMany(s => s.Item3).Select(r => r[2]).DefaultIfEmpty(-1).Max() + 1;
            int ncol = hasCol ? subs.SelectMany(s => s.Item3).Select(r => r[3]).DefaultIfEmpty(-1).Max() + 1 : 0;
            var m = new MdtMesh { Hdr = hw, HasCol = hasCol, Submeshes = subs, Preamble = preamble };
            m.Pos = ReadVec16(scn, fo + POS, npos);
            m.Uv = ReadVec16(scn, fo + UV, nuv);
            m.Norm = NORM > 0 ? ReadVec16(scn, fo + NORM, nnorm) : new List<double[]>();
            m.Col = hasCol ? ReadVec16(scn, fo + (int)COL, ncol) : null;
            int nmat = (int)((total - MAT) / 0x60);
            m.Materials = new List<byte[]>();
            for (int i = 0; i < nmat; i++) m.Materials.Add(scn.AsSpan(fo + MAT + i * 0x60, 0x60).ToArray());
            var named = new List<(string, int)> { ("POS", POS), ("DL", DL), ("UV", UV) };
            if (NORM > 0) named.Add(("NORM", NORM));
            named.Add(("MAT", MAT));
            if (hasCol) named.Add(("COL", (int)COL));
            named = named.OrderBy(x => x.Item2).ToList();       // stable, like Python's sort
            m.Order = named.Select(x => x.Item1).ToList();
            var sizes = new Dictionary<string, int> { ["POS"] = npos * 16, ["DL"] = DLSZ, ["UV"] = nuv * 16, ["NORM"] = m.Norm.Count * 16, ["COL"] = hasCol ? ncol * 16 : 0, ["MAT"] = nmat * 0x60 };
            m.Pads = new Dictionary<string, byte[]>();
            for (int i = 0; i < named.Count; i++)
            {
                var (nm, off) = named[i];
                long nxt = i + 1 < named.Count ? named[i + 1].Item2 : total;
                int a = fo + off + sizes[nm], b = fo + (int)nxt;
                m.Pads[nm] = b > a ? scn.AsSpan(a, b - a).ToArray() : Array.Empty<byte>();
            }
            return m;
        }

        internal byte[] DlBytes()
        {
            int stride = HasCol ? 4 : 3;
            var b = new List<byte>();
            b.AddRange(BitConverter.GetBytes(Preamble[0])); b.AddRange(BitConverter.GetBytes(Preamble[1]));
            b.AddRange(BitConverter.GetBytes(Submeshes.Count)); b.AddRange(BitConverter.GetBytes(Preamble[3]));
            foreach (var (prim, midx, recs) in Submeshes)
            {
                b.AddRange(BitConverter.GetBytes(prim)); b.AddRange(BitConverter.GetBytes(recs.Count)); b.AddRange(BitConverter.GetBytes(midx));
                foreach (var r in recs) for (int c = 0; c < stride; c++) b.AddRange(BitConverter.GetBytes(r[c]));
            }
            return b.ToArray();
        }

        internal static byte[] VecBytes(List<double[]> vecs)
        {
            var b = new byte[vecs.Count * 16];
            for (int i = 0; i < vecs.Count; i++) for (int k = 0; k < 4; k++) IsoBytes.WrF(b, i * 16 + k * 4, (float)vecs[i][k]);
            return b;
        }

        internal byte[] Build()
        {
            var block = new Dictionary<string, byte[]>
            {
                ["POS"] = VecBytes(Pos), ["DL"] = DlBytes(), ["UV"] = VecBytes(Uv), ["NORM"] = VecBytes(Norm),
                ["COL"] = HasCol ? VecBytes(Col) : Array.Empty<byte>(), ["MAT"] = Materials.SelectMany(x => x).ToArray(),
            };
            var outp = new List<byte>(new byte[0x40]);
            var offs = new Dictionary<string, int>();
            foreach (string nm in Order)
            {
                offs[nm] = outp.Count;
                outp.AddRange(block[nm]);
                if (Pads.TryGetValue(nm, out var pad)) outp.AddRange(pad);
            }
            var hw = (uint[])Hdr.Clone();
            hw[2] = (uint)outp.Count; hw[3] = (uint)Pos.Count; hw[4] = (uint)offs["POS"]; hw[6] = (uint)offs["UV"];
            hw[8] = HasCol ? (uint)offs["COL"] : Hdr[8]; hw[9] = (uint)block["DL"].Length; hw[10] = (uint)offs["DL"];
            hw[12] = Norm.Count > 0 ? (uint)offs["NORM"] : 0; hw[14] = (uint)offs["MAT"];
            var o = outp.ToArray();
            for (int i = 0; i < 16; i++) IsoBytes.U32(o, i * 4, hw[i]);
            return o;
        }

        /// <summary>Triangles (record triples) of every submesh: lists in threes, strips with alternating winding.</summary>
        internal IEnumerable<(int mat, int[] a, int[] b, int[] c)> Triangles(bool dropDegenerateStrips)
        {
            foreach (var (prim, midx, recs) in Submeshes)
            {
                if (prim == 3) for (int k = 0; k + 2 < recs.Count; k += 3) yield return (midx, recs[k], recs[k + 1], recs[k + 2]);
                else if (prim == 4)
                    for (int i = 0; i + 2 < recs.Count; i++)
                    {
                        int[] a, b, c;
                        if (i % 2 == 0) { a = recs[i]; b = recs[i + 1]; c = recs[i + 2]; } else { a = recs[i + 1]; b = recs[i]; c = recs[i + 2]; }
                        if (dropDegenerateStrips && (a[0] == b[0] || b[0] == c[0] || a[0] == c[0])) continue;
                        yield return (midx, a, b, c);
                    }
                else throw new InvalidDataException($"prim {prim}");
            }
        }
    }
}
