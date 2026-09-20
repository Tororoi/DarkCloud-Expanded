using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Dark_Cloud_Improved_Version.CatMath;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The wing graft (<see cref="CatWings"/>) baked into the cat pack as disc data: the engine's skinner is plain linear
    /// blend skinning (one position per vertex in the mesh node's space, per-bone weights from the .wgt), so the BIND pose is
    /// Dran's own bind mapped onto the cat (the open wing), where every carved membrane vertex's bone-local copies agree; a
    /// vertex whose copies disagree there (the two-pose-fitted tab midpoints and fold lifts, the pooled base verts) is placed
    /// where the viewer put it in the LEAP reference pose. Produces, all cat-relative: 8 wing bones as a chain under
    /// cat_sebone2, two wing mesh nodes and the mask node under the cat root, their MDTs, .wgt runs, .mot tracks, .bbp rows,
    /// and the Super Steve cape (a cloth FRAME node whose MDT is the rest lattice, plus its .clo text).</summary>
    internal static class WingBake
    {
        internal static readonly string[] BoneNames = { "cat_rwing1", "cat_rwing2", "cat_rwing3", "cat_rwing4", "cat_lwing1", "cat_lwing2", "cat_lwing3", "cat_lwing4" };
        internal static readonly Dictionary<string, string> MeshNames = new() { ["r"] = "cat_rwingm", ["l"] = "cat_lwingm" };   // no "__" suffix: the membrane draws from both sides
        internal const string MaskNode = "cat_mask";      // the domino mask: one rigid mesh node weighted whole to cat_kao; it SHARES the cape's
        internal const string MaskTex = "catcape";        // texture — a mesh's texture is resolved by NAME at pack load and a private entry never came through
        internal const string WingTex = "catwing";        // the flat white texture
        internal const string CapeNode = "cat_cape";      // the cloth's FRAME node: its MDT is the rest lattice in the engine's (width-outer, hang-inner) order
        internal const string CapeTex = "catcape";
        internal const string CapeClo = "catcape.clo";
        // the .clo grammar: SIZE outer, inner (1..16 each; outer = the WIDTH, inner = the HANG); particle (a, b) = MDT vertex a*inner + b; the
        // engine pins every (a, 0) to LW(anchor) × rest; POLYDIV holds outer−1 chars ('1' flips that strip's winding). Every BOUND names the
        // cape node itself (it must resolve in HER tree at load); the runtime re-points each to the cat bone of the same index in CatWings.CapeBounds.
        private const uint WgtChan = 20;
        private const int SubmeshRecs = 150;              // triangle-list records per submesh (50 tris): well inside what the VU builder sees in vanilla

        internal sealed class Node { internal string Name; internal int Parent; internal double[] Local16; internal byte[] Mdt; }
        internal sealed class Cape { internal string Name; internal int ParentAbs; internal double[] Local16; internal byte[] Mdt; internal int Rows, Cols; internal byte[] Clo; internal string Texture; }
        internal sealed class Result
        {
            internal List<Node> Nodes; internal List<MotTrack> WgtTracks, MotTracks; internal byte[] Bbp;
            internal string[] AllocDbuff; internal string Texture; internal Cape Cape; internal string MaskName, MaskTexture;
            internal (int first, int last) Frames; internal int MotKeys;
        }

        private static MotKeyframe Kf(double frame, IList<double> vals)
        {
            var raw = new byte[MotKeyframe.Size];
            IsoBytes.U32(raw, 0, (uint)(int)frame);
            for (int i = 0; i < 4; i++) IsoBytes.WrF(raw, 0x10 + i * 4, (float)(i < vals.Count ? vals[i] : 0.0));
            return new MotKeyframe(raw);
        }
        private static double[] Local16(double[][] R, double[] T) { var M = MatFromRT(R, T); return M.SelectMany(r => r).ToArray(); }
        private static MotTrack Trk(uint w0, uint w1, uint w2, uint w6, uint w7, List<MotKeyframe> kfs) => new MotTrack { W0 = w0, W1 = w1, W2 = w2, W3 = 32, W6 = w6, W7 = w7, Keyframes = kfs };

        /// <summary>An indexed triangle-list MDT off the skin MDT's header: positions, one UV/NORM entry per vertex, one material naming the texture.</summary>
        private static byte[] ListMdt(MdtMesh skinMdt, byte[] matTemplate, string tex, List<double[]> pos, List<double[]> uv, List<double[]> nrm, List<int[]> tris)
        {
            var m = new MdtMesh { Hdr = (uint[])skinMdt.Hdr.Clone(), HasCol = false, Col = null };
            m.Pos = pos.Select(p => new[] { p[0], p[1], p[2], 1.0 }).ToList();
            m.Uv = uv; m.Norm = nrm;
            var recs = tris.SelectMany(t => t).Select(v => new[] { v, v, v }).ToList();
            m.Submeshes = new List<(int, int, List<int[]>)>();
            for (int k = 0; k < recs.Count; k += SubmeshRecs) m.Submeshes.Add((3, 0, recs.Skip(k).Take(SubmeshRecs).ToList()));
            var mat = (byte[])matTemplate.Clone(); var nb = Encoding.ASCII.GetBytes(tex); Array.Clear(mat, 0x34, 16); Array.Copy(nb, 0, mat, 0x34, nb.Length);
            m.Materials = new List<byte[]> { mat };
            m.Preamble = new[] { 0, 16, m.Submeshes.Count, 0 };
            m.Order = new List<string> { "POS", "DL", "UV", "NORM", "MAT" };
            int dl = 16 + m.Submeshes.Sum(s => 12 + 12 * s.recs.Count);
            m.Pads = new Dictionary<string, byte[]> { ["POS"] = Array.Empty<byte>(), ["DL"] = new byte[PyMath.Mod(-dl, 16)], ["UV"] = Array.Empty<byte>(), ["NORM"] = Array.Empty<byte>(), ["MAT"] = Array.Empty<byte>() };
            int nv = pos.Count;
            m.Hdr[5] = (uint)nv; m.Hdr[7] = 0; m.Hdr[8] = 0xFFFFFFFF; m.Hdr[11] = (uint)nv; m.Hdr[13] = 1; m.Hdr[15] = 0;
            byte[] blob = m.Build();
            if (blob.Length % 16 != 0 || blob[0] != 'M' || blob[1] != 'D' || blob[2] != 'T' || blob[3] != 0) throw new IOException("wing MDT");
            var back = MdtMesh.Parse(blob, 0);
            if (back.Pos.Count != nv || back.Materials.Count != 1 || back.Submeshes.Sum(s => s.recs.Count) != 3 * tris.Count) throw new IOException("wing MDT round-trip");
            return blob;
        }

        /// <summary>The bake: Dran's pack bytes, the WINGLESS cat pack and its (host, cat) node counts, the s86 cat pack (the skin MDT's header
        /// template and the .wgt tag words).</summary>
        internal static Result Build(byte[] dranChr, byte[] packed, int nb, int K0, byte[] catBytes, Action<string> log)
        {
            var data = CatWings.WingGraft(dranChr, packed, nb, K0, log);
            var nodes = data.Nodes; int bas = data.Base, parent = data.Parent; int K = bas;
            var bindOpen = data.BindOpen; var Psp = nodes[parent].World;
            var WOpenMap = new Dictionary<int, double[][]>();
            foreach (int side0 in new[] { bas, bas + 4 })
            {
                var T1 = bindOpen[side0].T;
                var Rs = Enumerable.Range(0, 4).Select(k => bindOpen[side0 + k].R).ToList();
                var Ts = new List<double[]> { (double[])T1.Clone() };
                for (int k = 0; k < 3; k++) { double seg = nodes[side0 + k + 1].T[0]; Ts.Add(new[] { Ts[k][0] + seg * Rs[k][0][0], Ts[k][1] + seg * Rs[k][0][1], Ts[k][2] + seg * Rs[k][0][2] }); }
                for (int k = 0; k < 4; k++) WOpenMap[side0 + k] = MatMul(MatFromRT(Rs[k], Ts[k]), Psp);
            }
            double[][] WOpen(int i) => WOpenMap.TryGetValue(i, out var m) ? m : nodes[i].World;
            var cat = ChrPack.Parse(catBytes);
            byte[] cmds = cat.Require("c04cat.mds").Payload;
            var cnodes = ModelCodec.ReadSkeleton(cmds);
            var skinN = cnodes.First(n => n.Name == "skin");
            var skinMdt = MdtMesh.Parse(cmds, skinN.MeshOff);
            var wgt0 = MotFile.FromPack(cat, "c04cat.wgt"); var tagged = wgt0.Tracks[0]; uint W6 = tagged.W6, W7 = tagged.W7;

            // ── nodes: 8 bones (chain) + 2 mesh nodes + the mask ──
            var outNodes = new List<Node>();
            for (int k = 0; k < BoneNames.Length; k++)
            {
                var n = nodes[bas + k];
                if (!(0 <= n.Parent && n.Parent < bas + k)) throw new IOException($"{BoneNames[k]}: parent {n.Parent}");
                var L = MatMul(WOpen(bas + k), RigidInv(WOpen(n.Parent)));
                var R = RotOf(L); var T = TransOf(L);
                if (n.Parent != parent && !(Math.Abs(T[1]) < 1e-3 && Math.Abs(T[2]) < 1e-3 && Math.Abs(T[0] - n.T[0]) < 1e-3)) throw new IOException($"{BoneNames[k]}: chained local");
                outNodes.Add(new Node { Name = BoneNames[k], Parent = n.Parent, Local16 = Local16(R, T) });
            }
            var ident = Local16(new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 } }, new[] { 0.0, 0.0, 0.0 });
            var meshIndex = new Dictionary<string, int>();
            foreach (string sd in new[] { "r", "l" }) { meshIndex[sd] = K + outNodes.Count; outNodes.Add(new Node { Name = MeshNames[sd], Parent = 0, Local16 = ident }); }
            int maskSlot = outNodes.Count, maskIndex = K + maskSlot;
            outNodes.Add(new Node { Name = MaskNode, Parent = 0, Local16 = ident });

            // ── Dran's per-vertex UV / NORM entries, keyed by its obj1 position index (first record that uses it) ──
            var om = data.Om;
            var dranUvn = new Dictionary<int, (double[] uv, double[] n)>();
            foreach (var (_, _, recs) in om.Submeshes) foreach (var r in recs)
                if (!dranUvn.ContainsKey(r[0])) dranUvn[r[0]] = (om.Uv[r[1]], om.Norm.Count > 0 ? om.Norm[r[2]] : new[] { 0.0, 0.0, 1.0, 0.0 });
            byte[] matTemplate = skinMdt.Materials[0];
            var wgtTracks = new List<MotTrack>(); var motTracks = new List<MotTrack>();
            for (int mi = 0; mi < 2; mi++)
            {
                string sd = mi == 0 ? "r" : "l";
                var me = data.Meshes[mi]; var (_, used, _) = data.WingSets[sd]; int nv = me.Nv;
                double[][] WRef(int b) => data.Wref.TryGetValue(b, out var m) ? m : data.Wcat[b];
                var P = new List<double[]>(); int twoPose = 0;
                for (int i = 0; i < nv; i++)
                {
                    int b0 = me.B0[i], b1 = me.B1[i]; double w = me.W0[i];
                    var pa = XformPt(WOpen(b0), me.P0[i]); var pb = XformPt(WOpen(b1), me.P1[i]);
                    if (w >= 0.999 || PyMath.Dist(pa, pb) < 1e-3) P.Add(new[] { pa[0] * w + pb[0] * (1 - w), pa[1] * w + pb[1] * (1 - w), pa[2] * w + pb[2] * (1 - w) });
                    else
                    {
                        twoPose++;
                        var fa = XformPt(WRef(b0), me.P0[i]); var fb = XformPt(WRef(b1), me.P1[i]);
                        double[] target = { fa[0] * w + fb[0] * (1 - w), fa[1] * w + fb[1] * (1 - w), fa[2] * w + fb[2] * (1 - w) };
                        var A0 = MatMul(RigidInv(WOpen(b0)), WRef(b0)); var A1 = MatMul(RigidInv(WOpen(b1)), WRef(b1));
                        var Mb = new double[4][]; for (int r = 0; r < 4; r++) { Mb[r] = new double[4]; for (int c = 0; c < 4; c++) Mb[r][c] = w * A0[r][c] + (1 - w) * A1[r][c]; }
                        var inv = Inv4(Mb) ?? throw new IOException($"wing vertex {i}: singular LBS blend");
                        P.Add(XformPt(inv, target));
                    }
                }
                var uv = new List<double[]>(); var nrm = new List<double[]>();
                for (int i = 0; i < nv; i++)
                {
                    (double[] uv, double[] n) e; bool have;
                    if (i < used.Count) have = dranUvn.TryGetValue(used[i], out e);
                    else
                    {
                        int j = 0; double best = double.PositiveInfinity;
                        for (int jj = 0; jj < used.Count; jj++) { double d = PyMath.Dist(P[i], P[jj]); if (d < best) { best = d; j = jj; } }
                        have = dranUvn.TryGetValue(used[j], out e);
                    }
                    if (!have) e = (new[] { 0.5, 0.5, 1.0, 1.0 }, new[] { 0.0, 0.0, 1.0, 0.0 });
                    uv.Add((double[])e.uv.Clone()); nrm.Add((double[])e.n.Clone());
                }
                byte[] blob = ListMdt(skinMdt, matTemplate, WingTex, P, uv, nrm, me.Tris);
                outNodes[8 + mi].Mdt = blob;
                // .wgt run: the reset entry, then bones ascending; percents sum to 100 per vertex
                int M = meshIndex[sd];
                var perBone = new Dictionary<int, List<(int, int)>>();
                for (int i = 0; i < nv; i++)
                {
                    int b0 = me.B0[i], b1 = me.B1[i]; double w = me.W0[i];
                    int pa = (int)PyMath.Round(w * 100, 0);
                    if (b0 == b1 || pa >= 100) pa = 100;
                    if (pa <= 0) { b0 = b1; pa = 100; }
                    if (!perBone.ContainsKey(b0)) perBone[b0] = new List<(int, int)>(); perBone[b0].Add((i, pa));
                    if (pa < 100) { if (!perBone.ContainsKey(b1)) perBone[b1] = new List<(int, int)>(); perBone[b1].Add((i, 100 - pa)); }
                }
                var run = new List<MotTrack> { Trk((uint)M, 0, WgtChan, W6, W7, new List<MotKeyframe>()) };
                foreach (int b in perBone.Keys.OrderBy(b => b))
                    run.Add(Trk((uint)M, (uint)b, WgtChan, W6, W7, perBone[b].OrderBy(x => x.Item1).ThenBy(x => x.Item2).Select(x => Kf(x.Item1, new[] { (double)x.Item2 })).ToList()));
                wgtTracks.AddRange(run);
                log($"  {sd} wing baked: {nv} verts, {me.Tris.Count} tris, MDT {blob.Length:N0} B, bones {string.Join(",", perBone.Keys.OrderBy(b => b))}, {run.Sum(t => t.Keyframes.Count)} wgt keys, {twoPose} two-pose verts");
            }
            // ── .mot tracks (already cat-relative node ids) ──
            foreach (var t in data.Tracks)
            {
                if (t.Node < bas) continue;
                if (!(t.Node < bas + 8 && (t.Chan == 0 || t.Chan == 2))) throw new IOException($"wing track node {t.Node}");
                motTracks.Add(Trk((uint)t.Node, 0, (uint)t.Chan, MotTrack.TagW6, MotTrack.TagW7, Enumerable.Range(0, t.Frames.Count).Select(i => Kf(t.Frames[i], t.Vals[i])).ToList()));
            }
            // ── the Super Steve cape: a cloth FRAME node (parent = her node 0, its bind 3×3 at HIDE_SCALE so the cloth she builds from it
            //    collapses to a point) whose MDT is the rest lattice in the cat's anchor bone's space ──
            var catNodes = data.CatNodes;
            var skinNode = catNodes.First(n => n.Name == "cat_skin");
            var skinW = ModelCodec.LoadWeights(data.Pack, "cat.wgt")[skinNode.I];
            var skinMe = ModelCodec.BuildMeshWeighted(data.Mds, skinNode, catNodes, skinW);
            var (rows, cols, cverts0) = CatWings.CapeRestLocal(catNodes, skinMe);
            if (!(1 <= rows && rows <= 16 && 1 <= cols && cols <= 16 && cverts0.Count == rows * cols)) throw new IOException("cape lattice");
            var cverts = new List<double[]>(); for (int c = 0; c < cols; c++) for (int r = 0; r < rows; r++) cverts.Add(cverts0[r * cols + c]);   // → engine order
            var ctris = new List<int[]>();
            for (int c = 0; c < cols - 1; c++) for (int r = 0; r < rows - 1; r++) { int a = c * rows + r; ctris.Add(new[] { a, a + rows, a + rows + 1 }); ctris.Add(new[] { a, a + rows + 1, a + 1 }); }
            var cuv = Enumerable.Range(0, cverts.Count).Select(_ => new[] { 0.5, 0.5, 1.0, 1.0 }).ToList(); var cnrm = Enumerable.Range(0, cverts.Count).Select(_ => new[] { 0.5, 0.5, 1.0, 0.0 }).ToList();
            byte[] capeMdt = ListMdt(skinMdt, matTemplate, CapeTex, cverts, cuv, cnrm, ctris);
            if (MdtMesh.Parse(capeMdt, 0).Pos.Count != rows * cols) throw new IOException("cape MDT");
            double hs = CatPackBakes.HideScale;
            var capeLocal = new[] { hs, 0, 0, 0, 0, hs, 0, 0, 0, 0, hs, 0, 0, 0, 0, 1 };
            var ph = CatWings.CapePhysics;
            string F(double v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            var clo = new StringBuilder();
            clo.Append($"SIZE\t{cols},\t{rows}\r\nFRAME\t\"{CapeNode}\"\r\nWINDEFFECT\t{F(ph.wind)}\r\nNORMAL\t{F(ph.normal)}\r\n");
            clo.Append($"GRAVITY\t{F(ph.gravity)},\t{F(ph.gravity)},\t{F(ph.gravity)}\r\nFOLLOW\t{F(ph.follow0)},\t{F(ph.follow1)},\t{F(ph.follow2)}\r\n");
            clo.Append($"K\t{F(ph.k)},\t{F(ph.k)},\t{F(ph.k)}\r\nPOLYDIV\t\"{new string('0', cols - 1)}\"\r\n");
            foreach (var b in CatWings.CapeBoundsLocal(catNodes))
                clo.Append($"BOUND\t\"{CapeNode}\"\r\n\t{F(b.Up[0])},\t{F(b.Up[1])},\t{F(b.Up[2])}\r\n\t{F(b.A[0])},\t{F(b.A[1])},\t{F(b.A[2])}\r\n\t{F(b.B[0])},\t{F(b.B[1])},\t{F(b.B[2])}\r\n\t{F(b.Radii[0])},\t{F(b.Radii[1])},\t{F(b.Radii[2])}\r\n\t{F(b.Damp)}\r\n");
            var cape = new Cape { Name = CapeNode, ParentAbs = 0, Local16 = capeLocal, Mdt = capeMdt, Rows = rows, Cols = cols, Clo = Encoding.ASCII.GetBytes(clo.ToString()), Texture = CapeTex };
            log($"  cape baked: {rows}×{cols} lattice, MDT {capeMdt.Length:N0} B, anchor {CatWings.CapeAnchor}, {CatWings.CapeBounds.Length} bounds");
            // ── the mask: a ring of geometry around each eye with a real hole in it, bound 100 % to the head bone ──
            var maskMe = CatWings.BuildMaskMesh(catNodes, skinMe, log);
            int headI = catNodes.First(n => n.Name == CatWings.MaskAnchor).I;
            var Wh = catNodes[headI].World;
            var mworld = maskMe.P0.Select(v => XformPt(Wh, v)).ToList();                    // head-local → model space
            var muv = mworld.Select(_ => new[] { 0.5, 0.5, 1.0, 1.0 }).ToList(); var mnrm = mworld.Select(_ => new[] { 0.5, 0.5, 1.0, 0.0 }).ToList();
            outNodes[maskSlot].Mdt = ListMdt(skinMdt, matTemplate, MaskTex, mworld, muv, mnrm, maskMe.Tris);
            wgtTracks.Add(Trk((uint)maskIndex, 0, WgtChan, W6, W7, new List<MotKeyframe>()));
            wgtTracks.Add(Trk((uint)maskIndex, (uint)headI, WgtChan, W6, W7, Enumerable.Range(0, mworld.Count).Select(v => Kf(v, new[] { 100.0 })).ToList()));
            log($"  mask baked: {mworld.Count} verts, {maskMe.Tris.Count} tris, MDT {outNodes[maskSlot].Mdt.Length:N0} B on {CatWings.MaskAnchor}");
            var bbp = new byte[outNodes.Count * 64];
            for (int i = 0; i < outNodes.Count; i++) for (int k = 0; k < 16; k++) IsoBytes.WrF(bbp, i * 64 + k * 4, (float)outNodes[i].Local16[k]);
            return new Result
            {
                Nodes = outNodes, WgtTracks = wgtTracks, MotTracks = motTracks, Bbp = bbp, AllocDbuff = new[] { MeshNames["r"], MeshNames["l"], MaskNode },
                Texture = WingTex, Cape = cape, MaskName = MaskNode, MaskTexture = MaskTex, Frames = (data.FramesAll[0], data.FramesAll[^1]), MotKeys = motTracks.Sum(t => t.Keyframes.Count),
            };
        }
    }
}
