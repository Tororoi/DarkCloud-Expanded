using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.TownCollisionData;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Brownboo (s04) collision: `s04g01_v` (the town's CAMERA collision, the slot every other town names `_c`) rebuilt as
    /// the vanilla terrain hull `v` (byte-identical tris) plus obj56 with ONLY the iwa01 tunnel rock replaced by its CSG hull,
    /// kd-split into ≤ 100-tri nodes; and the three hand-simplified fishing rocks appended to `s04g01_a` (every rock face a
    /// slope, so the fishing floors-only compaction keeps them).</summary>
    internal static class BrownbooCollisionBakes
    {
        private static string TriKeyTenth(double[][] t) => CollisionGeom.TriKeyRounded(t, 1);

        /// <summary>[(node, tris)] of s04g01_v (world == local: s04g01's placement is the origin).</summary>
        private static List<(string name, List<double[][]> tris)> VanillaVNodes(byte[] scn)
        {
            var (off, size) = SceneScn.DirectoryMap(scn)["s04g01"];
            byte[] sub = scn.AsSpan(off, size).ToArray();
            var m = new Regex("s04g01_v\\.mds\\x00").Match(System.Text.Encoding.Latin1.GetString(sub));
            if (!m.Success) return new List<(string, List<double[][]>)>();
            int vo = (int)IsoBytes.U32(sub, m.Index + m.Length + 3);
            int mds = off + vo;
            var (nodes, wm) = SceneScn.Accum(scn, mds);
            var outp = new List<(string, List<double[][]>)>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.MeshOff == 0) continue;
                int fo = SceneScn.ResolveMdt(scn, mds, n.MeshOff);
                if (fo < 0) continue;
                var M = wm(i);
                var tris = CollisionMdt.Parse(scn, fo).Select(t => new[] { SceneScn.Xform(M, t[0][0], t[0][1], t[0][2]), SceneScn.Xform(M, t[1][0], t[1][1], t[1][2]), SceneScn.Xform(M, t[2][0], t[2][1], t[2][2]) }).ToList();
                if (tris.Count > 0) outp.Add((n.Name, tris));
            }
            return outp;
        }

        /// <summary>obj56 tris fully inside the rock's padded bbox, excluding r ≤ 83 of the origin (the central-cylinder guard).</summary>
        private static List<double[][]> RockSelTris(List<double[][]> obj56, double[] box)
            => obj56.Where(t => t.All(p => box[0] <= p[0] && p[0] <= box[1] && box[2] <= p[2] && p[2] <= box[3]) && t.All(p => Math.Sqrt(p[0] * p[0] + p[2] * p[2]) > 83)).ToList();

        private static List<double[][]> Iwa01KeepMatch(List<double[][]> tris)
        {
            double[] Cen(double[][] t) => new[] { (t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3, (t[0][2] + t[1][2] + t[2][2]) / 3 };
            var kc = CollisionGeom.TrisFrom(Iwa01KeepTris).Select(Cen).ToList();
            return tris.Where(t => { var c = Cen(t); return kc.Any(k => Math.Max(Math.Max(Math.Abs(c[0] - k[0]), Math.Abs(c[1] - k[1])), Math.Abs(c[2] - k[2])) < 0.2); }).ToList();
        }

        /// <summary>Vanilla obj56 with the lumpy iwa01 selection replaced by the circle-hull build; the walkway/bank strip through
        /// the tunnel survives the bbox removal.</summary>
        private static List<double[][]> Iwa01RingObj56(byte[] scn)
        {
            var obj56 = VanillaVNodes(scn).First(x => x.name == "obj56").tris;
            var sel = RockSelTris(obj56, Iwa01SelBox);
            var selk = new HashSet<string>(sel.Select(TriKeyTenth));
            selk.ExceptWith(Iwa01KeepMatch(sel).Select(TriKeyTenth));
            var keep = obj56.Where(t => !selk.Contains(TriKeyTenth(t))).Select(CollisionGeom.CopyTri).ToList();
            keep.AddRange(CollisionGeom.TrisFrom(Iwa01Hull));
            return keep;
        }

        /// <summary>The [(node, world tris)] list for the rebuilt s04g01_v. Never an already-baked scene: its `_v` no longer has
        /// the vanilla obj56 node.</summary>
        internal static List<(string name, List<double[][]> tris)> BakedNamed(byte[] scn)
        {
            var vanV = VanillaVNodes(scn).FirstOrDefault(x => x.name == "v").tris ?? throw new IOException("vanilla s04g01_v node 'v' not found (already-baked scene?)");
            var named = new List<(string, List<double[][]>)> { ("v", vanV) };
            named.AddRange(CollisionMdt.KdSplit(Iwa01RingObj56(scn), 100, proportional: true).Select((bk, i) => ($"c56_{i:D2}", bk)));
            return named;
        }

        /// <summary>The scene with the rock nodes appended to s04g01_a; refuses to double-bake.</summary>
        internal static (byte[] scn, int delta) BakeRocks(byte[] scn)
        {
            if (IsoBytes.Find(scn, System.Text.Encoding.ASCII.GetBytes("rock_iwa01")) >= 0) throw new IOException("s04g01_a already carries the rock nodes (already-baked scene?)");
            var rocks = new List<(string, byte[])>
            {
                ("rock_iwa01", CollisionMdt.Build(CollisionGeom.TrisFrom(Rock_iwa01))),
                ("rock_iwa02", CollisionMdt.Build(CollisionGeom.TrisFrom(Rock_iwa02))),
                ("rock_iwa03", CollisionMdt.Build(CollisionGeom.TrisFrom(Rock_iwa03))),
            };
            return CollisionMdsWriter.AppendVariantNodes(scn, "s04g01", rocks, "_a");
        }
    }
}
