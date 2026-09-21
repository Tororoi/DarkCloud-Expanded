using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A town scene.scn as the bakes read it: the sub-file directory, mapinfo GROUND/WATER placements, MDS node blocks
    /// with parent-chain accumulated transforms, and the placed visual meshes (every mesh node of every placed sub-file, in
    /// world space, decoded with the strict MDT codec). Matrices are column-major 16-doubles as stored.</summary>
    internal static class SceneScn
    {
        internal sealed class DirEntry { internal string Name; internal int Off, Size, EntryOff; }

        /// <summary>The directory (0x30-stride entries from 0x10) in order; EntryOff is where the entry sits, for repointing.</summary>
        internal static List<DirEntry> DirectoryList(byte[] scn)
        {
            var outp = new List<DirEntry>();
            for (int o = 0x10; o + 0x30 <= scn.Length; o += 0x30)
            {
                string nm = IsoBytes.NameAt(scn, o, 16);
                if (nm.Length == 0 || !char.IsLetterOrDigit(nm[0])) break;
                outp.Add(new DirEntry { Name = nm, Off = (int)IsoBytes.U32(scn, o + 0x10), Size = (int)IsoBytes.U32(scn, o + 0x14), EntryOff = o });
            }
            return outp;
        }

        /// <summary>{name: (off, size)}, first entry wins.</summary>
        internal static Dictionary<string, (int off, int size)> DirectoryMap(byte[] scn)
        {
            var d = new Dictionary<string, (int, int)>();
            foreach (var e in DirectoryList(scn)) if (!d.ContainsKey(e.Name)) d[e.Name] = (e.Off, e.Size);
            return d;
        }

        /// <summary>[(sub-file, pos, rot)] for each GROUND or WATER entry of a mapinfo.cfg (the position row then the rotation
        /// row after the LOD-name lines).</summary>
        internal static List<(string name, double[] pos, double[] rot)> GroundPlacements(string cfg)
        {
            var lines = PyMath.SplitLines(cfg);
            var outp = new List<(string, double[], double[])>();
            var head = new Regex("^\\s*(?:GROUND|WATER)\\s+\"([^\"]+)\"");
            double[] NumRow(string s)
            {
                s = s.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (!Regex.IsMatch(s, "^-?\\d")) return null;
                var parts = Regex.Split(s, "[,\\t ]+").Where(p => p.Length > 0).ToList();
                var vals = new List<double>();
                foreach (string p in parts)
                {
                    if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)) return null;
                    vals.Add(v);
                }
                return vals.ToArray();
            }
            int i = 0;
            while (i < lines.Count)
            {
                var m = head.Match(lines[i]);
                if (m.Success)
                {
                    string name = m.Groups[1].Value; var nums = new List<double[]>(); int j = i + 1;
                    while (j < lines.Count && !head.IsMatch(lines[j]) && nums.Count < 2)
                    {
                        var r = NumRow(lines[j]);
                        if (r != null && r.Length >= 3) nums.Add(new[] { r[0], r[1], r[2] });
                        j++;
                    }
                    if (nums.Count >= 2) outp.Add((name, nums[0], nums[1]));
                    i = j;
                }
                else i++;
            }
            return outp;
        }

        internal static double[] Compose(double[] a, double[] b)
        {
            var r = new double[16];
            for (int c = 0; c < 4; c++) for (int row = 0; row < 4; row++)
            {
                double s = 0; for (int k = 0; k < 4; k++) s += a[k * 4 + row] * b[c * 4 + k];
                r[c * 4 + row] = s;
            }
            return r;
        }

        internal static double[] Xform(double[] m, double x, double y, double z) => new[]
        {
            m[0] * x + m[4] * y + m[8] * z + m[12],
            m[1] * x + m[5] * y + m[9] * z + m[13],
            m[2] * x + m[6] * y + m[10] * z + m[14],
        };

        /// <summary>The mapinfo placement: rotate about Y by <paramref name="ry"/> degrees, then translate.</summary>
        internal static double[] PlaceY(double[] v, double[] pos, double ry)
        {
            double th = ry * (Math.PI / 180.0), c = Math.Cos(th), s = Math.Sin(th);
            return new[] { v[0] * c + v[2] * s + pos[0], v[1] + pos[1], -v[0] * s + v[2] * c + pos[2] };
        }

        internal sealed class Node { internal string Name; internal int MeshOff, Parent; internal double[] Mat; }

        /// <summary>The nodes of the MDS block at <paramref name="mds"/> and their parent-accumulated world matrices.</summary>
        internal static (List<Node> nodes, Func<int, double[]> world) Accum(byte[] scn, int mds)
        {
            int cnt = (int)IsoBytes.U32(scn, mds + 8), tbl = (int)IsoBytes.U32(scn, mds + 12);
            var nodes = new List<Node>(cnt);
            for (int i = 0; i < cnt; i++)
            {
                int b = mds + tbl + i * 0x70;
                var mat = new double[16]; for (int k = 0; k < 16; k++) mat[k] = IsoBytes.F32(scn, b + 0x30 + k * 4);
                nodes.Add(new Node { Name = IsoBytes.NameAt(scn, b + 8, 16), MeshOff = BitConverter.ToInt32(scn, b + 0x28), Parent = BitConverter.ToInt32(scn, b + 0x2C), Mat = mat });
            }
            var world = new double[cnt][];
            double[] Wm(int i)
            {
                if (world[i] == null)
                {
                    var n = nodes[i];
                    world[i] = (n.Parent < 0 || n.Parent >= cnt) ? n.Mat : Compose(Wm(n.Parent), n.Mat);
                }
                return world[i];
            }
            return (nodes, Wm);
        }

        /// <summary>The MDT offset a node's mesh word resolves to (absolute, or relative to the block), or −1.</summary>
        internal static int ResolveMdt(byte[] scn, int mds, int mo)
        {
            foreach (int c in new[] { mo, mds + mo })
                if (0 < c && c < scn.Length - 3 && scn[c] == 'M' && scn[c + 1] == 'D' && scn[c + 2] == 'T') return c;
            return -1;
        }

        internal static List<int[]> Flatten(MdtMesh m) => m.Triangles(false).Select(t => new[] { t.a[0], t.b[0], t.c[0] }).ToList();

        internal sealed class PlacedMesh { internal string Name, Sub; internal int Inst; internal List<double[]> Verts; internal List<int[]> Tris; }

        /// <summary>Every placed mesh node instance: a GROUND/WATER entry places a whole sub-file, each mesh node in it is one
        /// entry, the same sub-file placed N times yields N sets.</summary>
        internal static List<PlacedMesh> PlacedMeshes(byte[] scn, byte[] mapinfo)
        {
            var dir = DirectoryMap(scn);
            var outp = new List<PlacedMesh>();
            var instCounter = new Dictionary<string, int>();
            foreach (var (name, pos, rot) in GroundPlacements(Encoding.Latin1.GetString(mapinfo)))
            {
                if (!dir.TryGetValue(name, out var e)) continue;
                int mrel = IsoBytes.FindFrom(scn, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, e.off);
                if (mrel < 0 || mrel >= e.off + e.size) continue;
                int mds = mrel;
                var (nodes, wm) = Accum(scn, mds);
                int inst = instCounter.TryGetValue(name, out int ic) ? ic : 0;
                instCounter[name] = inst + 1;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n.MeshOff == 0) continue;
                    int fo = ResolveMdt(scn, mds, n.MeshOff);
                    if (fo < 0) continue;
                    MdtMesh m;
                    try { m = MdtMesh.Parse(scn, fo); } catch (Exception) { continue; }
                    var M = wm(i);
                    var wv = m.Pos.Select(p => PlaceY(Xform(M, p[0], p[1], p[2]), pos, rot[1])).ToList();
                    var tris = Flatten(m);
                    if (tris.Count == 0) continue;
                    outp.Add(new PlacedMesh { Name = n.Name, Inst = inst, Verts = wv, Tris = tris, Sub = name });
                }
            }
            return outp;
        }

        /// <summary>The scene with sub <paramref name="name"/> swapped for <paramref name="newSub"/>: appended 16-aligned, the
        /// directory entry repointed (the old bytes become dead space).</summary>
        internal static byte[] ReplaceSub(byte[] scene, string name, byte[] newSub)
        {
            int n = (int)IsoBytes.U32(scene, 4), ent = -1;
            for (int i = 0; i < n; i++)
            {
                int e = 0x10 + i * 0x30;
                if (IsoBytes.NameAt(scene, e, name.Length + 1) == name && scene[e + name.Length] == 0) { ent = e; break; }
            }
            if (ent < 0) throw new System.IO.IOException($"{name} not in the scene directory");
            int blob = (scene.Length + 15) & ~15;
            var outp = new byte[blob + newSub.Length];
            Array.Copy(scene, outp, scene.Length); Array.Copy(newSub, 0, outp, blob, newSub.Length);
            IsoBytes.U32(outp, ent + 0x10, (uint)blob); IsoBytes.U32(outp, ent + 0x14, (uint)newSub.Length);
            return outp;
        }
    }
}
