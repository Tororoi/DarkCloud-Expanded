using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Engine-level MDS splice + flat collision-MDS serialiser (town-agnostic): locate / replace a sub-file's `_a`
    /// (or `_c`, `_v`) variant block in scene.scn, build a flat collision MDS from named triangle lists, kd-split a pooled
    /// soup into nodes with unique 15-char names, and append nodes to an existing variant without touching its vanilla nodes.</summary>
    internal static class CollisionMdsWriter
    {
        private static string L1(byte[] b) => Encoding.Latin1.GetString(b);
        private static readonly Regex AnyVariant = new Regex("[A-Za-z0-9_]+\\.mds\\x00");

        /// <summary>Offset (within the sub) of the `&lt;name&gt;&lt;suffix&gt;.mds` variant's MDS block, or −1.</summary>
        internal static int VariantOff(byte[] sub, string name, string suffix = "_a")
        {
            var m = new Regex(Regex.Escape(name) + suffix + "\\.mds\\x00").Match(L1(sub));
            if (!m.Success) return -1;
            int off = (int)IsoBytes.U32(sub, m.Index + m.Length + 3);   // +3 skips the baked fixup-ptr tail
            return 0 < off && off < sub.Length && sub[off] == 'M' && sub[off + 1] == 'D' && sub[off + 2] == 'S' ? off : -1;
        }

        private static List<(int pos, int toff)> Variants(byte[] sub, int subOff)
        {
            var outp = new List<(int, int)>();
            foreach (Match m in AnyVariant.Matches(L1(sub)))
            {
                int fpos = m.Index + m.Length + 3;
                if (fpos + 4 > sub.Length) continue;
                int toff = (int)IsoBytes.U32(sub, fpos);
                if (0 < toff && toff < sub.Length && sub[toff] == 'M' && sub[toff + 1] == 'D' && sub[toff + 2] == 'S') outp.Add((subOff + fpos, toff));
            }
            return outp;
        }

        /// <summary>Replace a sub-file's entire `&lt;name&gt;&lt;suffix&gt;` MDS block; trailing variant offsets and the SCN directory follow.</summary>
        internal static (byte[] scn, int delta) ReplaceABlock(byte[] scn, string subName, byte[] newMds, string suffix = "_a")
        {
            var dir = SceneScn.DirectoryList(scn);
            var entry = dir.FirstOrDefault(e => e.Name == subName) ?? throw new IOException($"{subName} not in the scene directory");
            int subOff = entry.Off, subSize = entry.Size;
            byte[] sub = scn.AsSpan(subOff, subSize).ToArray();
            int vo = VariantOff(sub, subName, suffix);
            if (vo < 0) throw new IOException($"{subName}: no {suffix}");
            var variants = Variants(sub, subOff);
            var after = variants.Where(v => v.toff > vo).Select(v => v.toff).ToList();
            int oldSize = (after.Count > 0 ? after.Min() : subSize) - vo;
            var mds = new List<byte>(newMds); while (mds.Count % 0x10 != 0) mds.Add(0);
            int delta = mds.Count - oldSize;
            var outp = new List<byte>(scn.Take(subOff + vo)); outp.AddRange(mds); outp.AddRange(scn.Skip(subOff + vo + oldSize));
            var o = outp.ToArray();
            foreach (var (pos, toff) in variants) if (toff > vo) IsoBytes.U32(o, pos, (uint)(toff + delta));
            foreach (var e in dir)
            {
                if (e.Off == subOff) IsoBytes.U32(o, e.EntryOff + 0x14, (uint)(e.Size + delta));
                else if (e.Off > subOff) IsoBytes.U32(o, e.EntryOff + 0x10, (uint)(e.Off + delta));
            }
            return (o, delta);
        }

        internal static string FitNodeName(string name, HashSet<string> used, int maxlen = 15)
        {
            string cand = name.Length > maxlen ? name.Substring(0, maxlen) : name; int k = 0;
            while (used.Contains(cand)) { k++; string suf = "~" + k; cand = name.Substring(0, Math.Min(name.Length, maxlen - suf.Length)) + suf; }
            used.Add(cand);
            return cand;
        }

        /// <summary>A flat `_a` MDS: node 0 the root, the rest its children with identity frames; a node's optional per-triangle
        /// colour entries (the event-trigger tags) go through the collision MDT's colour block.</summary>
        internal static byte[] BuildFlatMds(List<(string name, List<double[][]> tris, List<byte[]> attrs)> named)
        {
            int n = named.Count;
            var header = new byte[0x10]; header[0] = (byte)'M'; header[1] = (byte)'D'; header[2] = (byte)'S';
            IsoBytes.U32(header, 4, 1); IsoBytes.U32(header, 8, (uint)n); IsoBytes.U32(header, 12, 0x10);
            var table = new List<byte>(); var blob = new List<byte>();
            int cur = 0x10 + n * 0x70;
            for (int i = 0; i < n; i++)
            {
                var (nm, t, attrs) = named[i];
                var node = new byte[0x70];
                IsoBytes.U32(node, 4, 0x70);
                byte[] b = Encoding.Latin1.GetBytes(nm); Array.Copy(b, 0, node, 8, Math.Min(15, b.Length));
                byte[] mdt = CollisionMdt.Build(t, attrs: attrs);
                Array.Copy(BitConverter.GetBytes(cur), 0, node, 0x28, 4);
                blob.AddRange(mdt); cur += mdt.Length;
                Array.Copy(BitConverter.GetBytes(i == 0 ? -1 : 0), 0, node, 0x2c, 4);
                float[] ident = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                for (int k = 0; k < 16; k++) IsoBytes.WrF(node, 0x30 + k * 4, ident[k]);
                table.AddRange(node);
            }
            return header.Concat(table).Concat(blob).ToArray();
        }

        /// <summary>kd-split a pooled soup into ≤ maxTris spatially compact nodes with unique names.</summary>
        internal static List<(string name, List<double[][]> tris)> PoolSplit(List<double[][]> pool, string prefix, HashSet<string> used, int maxTris = 100)
            => CollisionMdt.KdSplit(pool, maxTris).Select((bk, bi) => (FitNodeName($"{prefix}{bi}", used, 15), bk)).ToList();

        /// <summary>Append collision nodes to a sub-file's existing variant MDS without touching its vanilla nodes: the table
        /// re-laid with entries cloned from <paramref name="templateNode"/>, every vanilla MDT copied byte for byte, the new
        /// MDTs after them, spliced back through <see cref="ReplaceABlock"/>.</summary>
        internal static (byte[] scn, int delta) AppendVariantNodes(byte[] scn, string subName, List<(string name, byte[] mdt)> addNodes, string suffix = "_a", int templateNode = 1)
        {
            var entry = SceneScn.DirectoryList(scn).FirstOrDefault(e => e.Name == subName) ?? throw new IOException($"{subName} not in the scene directory");
            int subOff = entry.Off, subSize = entry.Size;
            byte[] sub = scn.AsSpan(subOff, subSize).ToArray();
            int vo = VariantOff(sub, subName, suffix);
            if (vo < 0) throw new IOException($"{subName}: no {suffix}");
            var after = Variants(sub, subOff).Where(v => vo < v.toff && v.toff < subSize).Select(v => v.toff).ToList();
            int mdsSize = (after.Count > 0 ? after.Min() : subSize) - vo;
            int cnt = (int)IsoBytes.U32(sub, vo + 8), tbl = (int)IsoBytes.U32(sub, vo + 12);
            var nodes = new List<(int i, int mo)>();
            for (int i = 0; i < cnt; i++) nodes.Add((i, BitConverter.ToInt32(sub, vo + tbl + i * 0x70 + 0x28)));
            var order = nodes.Where(n => n.mo != 0).OrderBy(n => n.mo).ToList();
            var blocks = new List<(int i, byte[] raw)>();
            for (int k = 0; k < order.Count; k++)
            {
                int end = k + 1 < order.Count ? order[k + 1].mo : mdsSize;
                blocks.Add((order[k].i, sub.AsSpan(vo + order[k].mo, end - order[k].mo).ToArray()));
            }
            var outp = new List<byte>(sub.AsSpan(vo, tbl + cnt * 0x70).ToArray());
            for (int k = 0; k < addNodes.Count; k++)
            {
                var ent = sub.AsSpan(vo + tbl + templateNode * 0x70, 0x70).ToArray();
                IsoBytes.U32(ent, 0, (uint)(cnt + k));
                byte[] nmb = Encoding.Latin1.GetBytes(addNodes[k].name); for (int c = 0; c < 16; c++) ent[8 + c] = c < nmb.Length && c < 15 ? nmb[c] : (byte)0;
                outp.AddRange(ent);
            }
            var patches = new List<(int at, int val)>();
            foreach (var (i, raw) in blocks) { int newMo = outp.Count; outp.AddRange(raw); while (outp.Count % 16 != 0) outp.Add(0); patches.Add((tbl + i * 0x70 + 0x28, newMo)); }
            for (int k = 0; k < addNodes.Count; k++) { int newMo = outp.Count; outp.AddRange(addNodes[k].mdt); while (outp.Count % 16 != 0) outp.Add(0); patches.Add((tbl + (cnt + k) * 0x70 + 0x28, newMo)); }
            var o = outp.ToArray();
            IsoBytes.U32(o, 8, (uint)(cnt + addNodes.Count));
            foreach (var (at, val) in patches) Array.Copy(BitConverter.GetBytes(val), 0, o, at, 4);
            return ReplaceABlock(scn, subName, o, suffix);
        }
    }
}
