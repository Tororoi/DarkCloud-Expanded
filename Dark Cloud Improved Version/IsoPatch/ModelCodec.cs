using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.CatMath;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A character rig node: the .mds frame record (name, mesh offset, parent, bind rotation rows R and translation T),
    /// its quaternion, and the bind world / inverse (row-vector 4×4s).</summary>
    internal sealed class RigNode
    {
        internal int I, MeshOff, Parent; internal string Name;
        internal double[][] R; internal double[] T, Quat;
        internal double[][] World, InvWorld; internal double[] WorldPos;
        internal RigNode Copy() => new RigNode { I = I, MeshOff = MeshOff, Parent = Parent, Name = Name, R = Clone(R), T = (double[])T.Clone(), Quat = (double[])Quat.Clone(), World = World == null ? null : Clone(World), InvWorld = InvWorld == null ? null : Clone(InvWorld), WorldPos = WorldPos == null ? null : (double[])WorldPos.Clone() };
        internal void Bind(IList<RigNode> nodes)
        {
            var L = MatFromRT(R, T);
            World = Parent < 0 ? L : MatMul(L, nodes[Parent].World);
            InvWorld = RigidInv(World);
            WorldPos = new[] { World[3][0], World[3][1], World[3][2] };
        }
    }

    /// <summary>A skinned mesh the way the viewer models it: per vertex two bone influences (b0/p0, b1/p1, w0), triangles as
    /// index triples.</summary>
    internal sealed class SkinMesh
    {
        internal int Node, Nv; internal bool Skin; internal string Tag;
        internal List<int[]> Tris = new();
        internal List<int> B0 = new(), B1 = new();
        internal List<double[]> P0 = new(), P1 = new();
        internal List<double> W0 = new();
    }

    /// <summary>The parts of the model codec the cat bake reads: skeletons, mesh triangles, .wgt weights, motion tracks, cfg names.</summary>
    internal static class ModelCodec
    {
        internal static List<RigNode> ReadSkeleton(byte[] mds)
        {
            int count = (int)IsoBytes.U32(mds, 8);
            var nodes = new List<RigNode>(count);
            for (int i = 0; i < count; i++)
            {
                int p = 0x18 + i * 0x70;
                var R = new double[3][]; for (int r = 0; r < 3; r++) R[r] = new double[] { IsoBytes.F32(mds, p + 0x28 + r * 16), IsoBytes.F32(mds, p + 0x28 + r * 16 + 4), IsoBytes.F32(mds, p + 0x28 + r * 16 + 8) };
                var T = new double[] { IsoBytes.F32(mds, p + 0x58), IsoBytes.F32(mds, p + 0x5C), IsoBytes.F32(mds, p + 0x60) };
                nodes.Add(new RigNode { I = i, Name = IsoBytes.NameAt(mds, p, 0x20), MeshOff = (int)IsoBytes.U32(mds, p + 0x20), Parent = BitConverter.ToInt32(mds, p + 0x24), R = R, T = T, Quat = MatToQuat(R) });
            }
            foreach (var n in nodes) n.Bind(nodes);
            return nodes;
        }

        internal static ChrRecord FindCfg(ChrPack pack, string prefer = null)
        {
            if (prefer != null) foreach (var r in pack.Records) if (r.Name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) && r.Name.ToLowerInvariant().Contains(prefer)) return r;
            foreach (var r in pack.Records) if (r.Name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>(mds name, mot name) of a cfg: the MODEL line and the first MOTION line's .mot.</summary>
        internal static (string mds, string mot) ParseCfgNames(byte[] cfg)
        {
            string raw = Encoding.Latin1.GetString(cfg);
            var m1 = Regex.Match(raw, "MODEL\\s+\"([^\"]+)\""); var m2 = Regex.Match(raw, "MOTION\\s+\\d+\\s*,\\s*\"([^\"]+\\.mot)\"");
            return (m1.Success ? m1.Groups[1].Value : null, m2.Success ? m2.Groups[1].Value : null);
        }

        /// <summary>Position-index triangles of a mesh (strips with degenerates dropped).</summary>
        internal static List<int[]> MdtTriangles(MdtMesh m) => m.Triangles(true).Select(t => new[] { t.a[0], t.b[0], t.c[0] }).ToList();

        /// <summary>{mesh node: {vertex: [(bone, weight 0..1)]}} from the pack's .wgt (chan-20 tracks: w0 = the mesh node, w1 = the
        /// bone, keys = (vertex, percent)); null when the record is missing. Vertex order = first appearance across the tracks.</summary>
        internal static Dictionary<int, Dictionary<int, List<(int bone, double w)>>> LoadWeights(ChrPack pack, string wgtName)
        {
            if (wgtName == null || pack.Find(wgtName) == null) return null;
            var wgt = MotFile.FromPack(pack, wgtName);
            var outp = new Dictionary<int, Dictionary<int, List<(int, double)>>>();
            foreach (var t in wgt.Tracks)
            {
                if (t.W2 != 20) continue;
                if (!outp.TryGetValue((int)t.W0, out var per)) outp[(int)t.W0] = per = new Dictionary<int, List<(int, double)>>();
                foreach (var kf in t.Keyframes)
                {
                    if (!per.TryGetValue((int)kf.Frame, out var lst)) per[(int)kf.Frame] = lst = new List<(int, double)>();
                    lst.Add(((int)t.W1, kf.Value[0] / 100.0));
                }
            }
            return outp;
        }

        private static readonly Dictionary<(byte[], int), double> MeshDimCache = new(ReferenceKeyComparer.Instance);
        private sealed class ReferenceKeyComparer : IEqualityComparer<(byte[], int)>
        {
            internal static readonly ReferenceKeyComparer Instance = new();
            public bool Equals((byte[], int) a, (byte[], int) b) => ReferenceEquals(a.Item1, b.Item1) && a.Item2 == b.Item2;
            public int GetHashCode((byte[], int) k) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(k.Item1) ^ k.Item2;
        }
        private static double MeshDim(byte[] mds, RigNode node)
        {
            var key = (mds, node.MeshOff);
            if (MeshDimCache.TryGetValue(key, out double d)) return d;
            double dim;
            try
            {
                var m = MdtMesh.Parse(mds, node.MeshOff);
                double xs = m.Pos.Max(v => v[0]) - m.Pos.Min(v => v[0]), ys = m.Pos.Max(v => v[1]) - m.Pos.Min(v => v[1]), zs = m.Pos.Max(v => v[2]) - m.Pos.Min(v => v[2]);
                dim = Math.Max(Math.Max(xs, ys), zs);
            }
            catch (Exception) { dim = 0.0; }
            MeshDimCache[key] = dim;
            return dim;
        }
        private static double D2(double[] a, double[] b) => Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2) + Math.Pow(a[2] - b[2], 2);

        /// <summary>A node's mesh with auto-skinning: rigid parts → the owner; large body meshes → the two nearest joints.</summary>
        internal static SkinMesh BuildMesh(byte[] mds, RigNode node, List<RigNode> nodes)
        {
            var m = MdtMesh.Parse(mds, node.MeshOff);
            var localPos = m.Pos.Select(v => new[] { v[0], v[1], v[2] }).ToList();
            var tris = MdtTriangles(m);
            if (tris.Count == 0) return null;
            double dim = Math.Max(Math.Max(localPos.Max(v => v[0]) - localPos.Min(v => v[0]), localPos.Max(v => v[1]) - localPos.Min(v => v[1])), localPos.Max(v => v[2]) - localPos.Min(v => v[2]));
            bool isSkin = dim > 5.0;
            var ow = node.World;
            var cand = nodes.Where(n => n.Parent >= 0 && !(n.MeshOff != 0 && MeshDim(mds, n) > 5.0)).ToList();
            var outp = new SkinMesh { Node = node.I, Skin = isSkin, Nv = localPos.Count, Tris = tris };
            foreach (var v in localPos)
            {
                var vm = XformPt(ow, v);
                if (!isSkin || cand.Count == 0) { outp.B0.Add(node.I); outp.P0.Add(v); outp.B1.Add(node.I); outp.P1.Add(new[] { 0.0, 0.0, 0.0 }); outp.W0.Add(1.0); continue; }
                var d = cand.Select(n => (d: D2(vm, n.WorldPos), n)).OrderBy(x => x.d).ToList();
                double d0 = Math.Sqrt(d[0].d) + 1e-4, d1 = Math.Sqrt(d[1].d) + 1e-4;
                double wa = (1.0 / d0) / (1.0 / d0 + 1.0 / d1);
                outp.B0.Add(d[0].n.I); outp.P0.Add(XformPt(d[0].n.InvWorld, vm)); outp.B1.Add(d[1].n.I); outp.P1.Add(XformPt(d[1].n.InvWorld, vm)); outp.W0.Add(wa);
            }
            return outp;
        }

        /// <summary>A node's mesh with the pack's real weights (top two influences per vertex, renormalised; unweighted vertices ride
        /// the owner).</summary>
        internal static SkinMesh BuildMeshWeighted(byte[] mds, RigNode node, List<RigNode> nodes, Dictionary<int, List<(int bone, double w)>> perVertex)
        {
            var m = MdtMesh.Parse(mds, node.MeshOff);
            var localPos = m.Pos.Select(v => new[] { v[0], v[1], v[2] }).ToList();
            var tris = MdtTriangles(m);
            if (tris.Count == 0) return null;
            var ow = node.World;
            var outp = new SkinMesh { Node = node.I, Skin = true, Nv = localPos.Count, Tris = tris };
            for (int vi = 0; vi < localPos.Count; vi++)
            {
                var v = localPos[vi]; var vm = XformPt(ow, v);
                var infl = (perVertex.TryGetValue(vi, out var l) ? l : new List<(int, double)>()).OrderBy(bw => -bw.Item2).Take(2).Where(bw => 0 <= bw.Item1 && bw.Item1 < nodes.Count).ToList();
                if (infl.Count == 0) { outp.B0.Add(node.I); outp.P0.Add(v); outp.B1.Add(node.I); outp.P1.Add(new[] { 0.0, 0.0, 0.0 }); outp.W0.Add(1.0); continue; }
                var (b0, wa) = infl[0]; var (b1, wb) = infl.Count > 1 ? infl[1] : (b0, 0.0);
                double tot = wa + wb;
                outp.B0.Add(b0); outp.P0.Add(XformPt(nodes[b0].InvWorld, vm)); outp.B1.Add(b1); outp.P1.Add(XformPt(nodes[b1].InvWorld, vm)); outp.W0.Add(tot > 0 ? wa / tot : 1.0);
            }
            return outp;
        }

        /// <summary>The rotation (chan 0) and translation (chan 2) tracks of a .mot for nodes below nodeCount, as frame/value lists.</summary>
        internal static List<Track> BuildTracks(ChrPack pack, string motName, int nodeCount)
        {
            var mot = MotFile.FromPack(pack, motName);
            var tracks = new List<Track>();
            foreach (var t in mot.Tracks)
            {
                if (t.W0 >= nodeCount || (t.W2 != 0 && t.W2 != 2) || t.Keyframes.Count == 0) continue;
                var tr = new Track { Node = (int)t.W0, Chan = (int)t.W2 };
                foreach (var kf in t.Keyframes) { tr.Frames.Add(kf.Frame); tr.Vals.Add(kf.Value.Select(x => (double)x).ToArray()); }
                tracks.Add(tr);
            }
            return tracks;
        }
    }
}
