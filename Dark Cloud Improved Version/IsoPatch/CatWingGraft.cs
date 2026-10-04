using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Dark_Cloud_Improved_Version.RigMath;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Divine Beast cat's wings: Dran's (dun\monstor\c12a.chr) wing bones and membrane grafted onto the s86 cat rig — every
    /// pose, tab polygon, weight and motion key authored here, every vertex taken from the player's own disc at patch time. Operation
    /// for operation the Python authoring model, so the baked bytes are the same. The cape is CatCapeBake's, the mask CatMaskBake's;
    /// CatGraftEncoder turns the <see cref="WingData"/> into pack records.</summary>
    internal static class CatWingGraft
    {
        // ───────────────────────── the wing graft's knobs ─────────────────────────
        internal static readonly string[] WingBones = { "r_wing1", "r_wing2", "r_wing3", "r_wing4", "l_wing1", "l_wing2", "l_wing3", "l_wing4" };
        private const string WingAttachNode = "cat_sebone2";     // the wing roots ride this mid-spine bone
        private const double WingScaleMul = 0.5;                  // wing length = the cat's body length × this (Dran's ≈ 59 units)
        private const double WingSizeMul = 0.7;                   // the wing mesh and chain at this fraction, about the roots
        private static readonly double[] WingAttachBind = { 0.0, 3.4, 1.7 };
        private const double WingRaise = 0.2;
        private const double WingYaw = 0.0;
        private const double WingPitchDeg = 8.0;                  // angle of attack: the leading edge rides high
        private const double WingExtendXIn = 0.20, WingExtendXOut = 0.95;
        private const double WingTabTiltDeg = 15.0;               // the tabs' dihedral about the body axis, down toward the spine
        private const double WingExtendZBack = 1.1, WingExtendZFront = 2.03, WingExtendZFrontOut = 1.85;
        private const double WingExtendLift = 0.1;
        private static readonly string[][] WingTabPolys = { new[] { "Rb", "Ri", "ir" }, new[] { "Ri", "if", "ir" }, new[] { "RIM", "if" }, new[] { "Rg", "Rf", "if", "under" } };
        private static readonly (double pull, double reach) WingApexPull = (0.30, 0.6);
        private static readonly (double fwd, double zmax, double xmax, double xfull) WingRootForward = (0.15, 0.8, 0.85, 0.45);
        private static readonly (int li, double d)[] WingFoldLift = { (28, 0.25), (24, 0.15), (29, 0.10), (19, 0.05) };
        private const int WingTabSubdiv = 2;
        private const double WingTabBulge = 0.2;
        private const int WingTabAnchorFrame = 15;
        // which Dran clip drives which cat clip: cat KEY index → (Dran start, end, speed, loop)
        private static readonly (int ki, int ds, int de, double dspd, bool loop)[] WingClips = { (4, 200, 205, 0.2, true), (7, 295, 300, 0.15, false) };
        private static readonly (int lcs, int lce, int lds, int lde, int lfe) WingLand = (215, 227, 70, 75, 220);
        private static readonly (double a, double b)[] WingLandLag = { (220.0, 225.0), (220.5, 226.0), (221.0, 226.5), (221.5, 227.0) };
        private const int WingFoldFrame = 15;
        private static readonly (double[] span, double[] top)[] WingFold =
        {
            (new[] { -0.08, -0.96, -0.27 }, new[] { -0.83, 0.38, 0.10 }), (new[] { -0.05, 0.35, -0.94 }, new[] { -1.00, 0.00, 0.00 }),
            (new[] { 0.03, 0.20, -0.98 }, new[] { -0.90, 0.42, 0.00 }), (new[] { 0.10, 0.15, -0.98 }, new[] { -0.90, 0.42, 0.00 }),
        };
        private static readonly double[] WingFoldRootShift = { -0.43, 0.08, 0.0 };
        private static readonly (int bone, double deg)[] WingHandDroop = { (1, -10.0), (2, 8.0), (3, -2.0) }; private static readonly (double, double) WingHandDroopFrames = (224, 227);
        private static readonly (int bone, double deg)[] WingHandRoll = { (2, 6.0), (3, 10.0) }; private static readonly (double, double) WingHandRollFrames = (224, 227);
        // the authored float-up stroke (cat clip 7): flap angle keys, extension keys, lag, sweep, the inboard hinge
        private static readonly (double f, double v)[] StrokePhi = { (285.0, 78.0), (287.5, 100.0), (288.5, 100.0), (292.0, -20.0), (294.0, 0.0) };
        private static readonly (double f, double v)[] StrokeExtend = { (285.0, 0.0), (287.5, 1.0) };
        private const double StrokeLag = 0.2, StrokeSweep = 60.0, StrokeHingeIn = 0.25; private const int StrokeKi = 7;
        private static readonly (int ki, double frame)[] WingHold = { (8, 285.0) };
        private static readonly (double f, double v)[] ClipPitch7 = { (285.0, 30.0), (292.0, 15.0), (294.0, 10.0) };
        private const int WingLevelAt = 4;


        // ───────────────────────── the graft ─────────────────────────
        internal sealed class WingData
        {
            internal List<RigNode> Nodes; internal List<SkinMesh> Meshes; internal List<Track> Tracks;
            internal int Base, Parent, RefFrame; internal List<int> FramesAll;
            internal Dictionary<string, (List<int[]> wt, List<int> used, Dictionary<int, int> remap)> WingSets;
            internal MdtMesh Om; internal Dictionary<int, double[][]> Wref; internal double[][][] Wcat;
            internal Dictionary<int, (double[][] R, double[] T, double[][] world)> BindOpen;
            internal List<RigNode> CatNodes; internal byte[] Mds; internal ChrPack Pack;
        }

        /// <summary>read_skeleton on the whole .mds, then the cat's records re-indexed 0..count−1 with bind worlds from the cat root.</summary>
        internal static List<RigNode> SubtreeNodes(byte[] mds, int first, int count)
        {
            var all = ModelCodec.ReadSkeleton(mds);
            var outp = new List<RigNode>();
            for (int k = 0; k < count; k++) { var n = all[first + k].Copy(); n.I = k; n.Parent = n.Parent < 0 ? -1 : n.Parent - first; outp.Add(n); }
            foreach (var n in outp) n.Bind(outp);
            return outp;
        }

        /// <summary>The bake's entry: the wing graft on the WINGLESS cat pack (`nb` host nodes + `K` cat nodes), Dran read from the disc.</summary>
        internal static WingData WingGraft(byte[] dranChr, byte[] packed, int nb, int K, Action<string> log)
        {
            var pack = ChrPack.Parse(packed);
            var mds = pack.Require(CatPackBakes.HostMds).Payload; int root = 0x18 + nb * 0x70;
            if (IsoBytes.NameAt(mds, root, 0x20) != CatPackBakes.CatRootName) throw new IOException("cat root record");
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) { int o = root + 0x28 + (r * 4 + c) * 4; IsoBytes.WrF(mds, o, (float)(IsoBytes.F32(mds, o) / CatPackBakes.HideScale)); }
            var nodes = SubtreeNodes(mds, nb, K);
            var dran = ChrPack.Parse(dranChr); var dcfg = ModelCodec.FindCfg(dran);
            var (dmdsName, dmotName) = ModelCodec.ParseCfgNames(dcfg.Payload);
            byte[] dmds = dran.Require(dmdsName).Payload; var dnodes = ModelCodec.ReadSkeleton(dmds);
            var outp = BuildWingedCat(nodes, mds, pack, dnodes, dmds, dran, dmotName, log);
            outp.CatNodes = nodes; outp.Mds = mds; outp.Pack = pack;
            return outp;
        }

        private static double[][][] CatWorldAt(List<RigNode> catNodes, Dictionary<(int, int), Track> ctr, double frame)
        {
            var W = new double[catNodes.Count][][];
            foreach (var n in catNodes)
            {
                var q = ctr.TryGetValue((n.I, 0), out var tq) ? Sample(tq, frame) : n.Quat;
                var t = ctr.TryGetValue((n.I, 2), out var tt) ? Sample(tt, frame).Take(3).ToArray() : n.T;
                var L = MatFromRT(QuatToMat(q), t);
                W[n.I] = n.Parent < 0 ? L : MatMul(L, W[n.Parent]);
            }
            return W;
        }

        private static (int a, int b) Edge(int x, int y) => (Math.Min(x, y), Math.Max(x, y));
        private static WingData BuildWingedCat(List<RigNode> catNodes, byte[] catMds, ChrPack catPack, List<RigNode> dranNodes, byte[] dranMds, ChrPack dranPack, string dmotName, Action<string> log)
        {
            var dn = dranNodes.ToDictionary(n => n.Name); var cn = catNodes.ToDictionary(n => n.Name);
            double[] r1 = dn["r_wing1"].T, l1 = dn["l_wing1"].T, r4 = dn["r_wing4"].T;
            double[] center = { (r1[0] + l1[0]) / 2, (r1[1] + l1[1]) / 2, (r1[2] + l1[2]) / 2 };
            double wingLen = ExactMath.Dist(r1, r4);
            var skin = cn["cat_skin"]; var sm = MdtMesh.Parse(catMds, skin.MeshOff);
            var pts = sm.Pos.Select(v => XformPt(skin.World, new[] { v[0], v[1], v[2] })).ToList();
            double[] ext = { pts.Max(p => p[0]) - pts.Min(p => p[0]), pts.Max(p => p[1]) - pts.Min(p => p[1]), pts.Max(p => p[2]) - pts.Min(p => p[2]) };
            double bodyLen = Math.Max(ext[0], ext[2]);
            double S = bodyLen / wingLen * WingScaleMul;
            log($"wing graft: cat body {bodyLen:F1} long × {ext[1]:F1} high; Dran wing {wingLen:F1} → scale {S:F3} × size {WingSizeMul}");
            var nodes = catNodes.Select(n => n.Copy()).ToList();
            int parent = cn[WingAttachNode].I;
            var rootInv = nodes[parent].InvWorld;
            var tracks = ModelCodec.BuildTracks(catPack, "cat.mot", catNodes.Count);
            var ctr = new Dictionary<(int, int), Track>(); foreach (var t in tracks) ctr[(t.Node, t.Chan)] = t;
            double[][][] CatWorld(double frame) => CatWorldAt(catNodes, ctr, frame);
            var (rcs, rce, _, _) = CatPackBakes.CatKeys[WingLevelAt];
            int refFrame = (rcs + rce) / 2;
            var P_ref = CatWorld(refFrame)[parent];
            var R_ref = RotOf(P_ref);
            double[] attach = { WingAttachBind[0], WingAttachBind[1] + WingRaise, WingAttachBind[2] };
            var anchorLocal = XformPt(rootInv, attach);
            var droot_R = dranNodes[0].R;
            double pr = Radians(WingPitchDeg);
            var yawR = new[] { new[] { Math.Cos(WingYaw), 0.0, Math.Sin(WingYaw) }, new[] { 0.0, 1.0, 0.0 }, new[] { -Math.Sin(WingYaw), 0.0, Math.Cos(WingYaw) } };
            var pitchR = new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, Math.Cos(pr), -Math.Sin(pr) }, new[] { 0.0, Math.Sin(pr), Math.Cos(pr) } };
            var droot4 = new[] { new[] { droot_R[0][0], droot_R[0][1], droot_R[0][2], 0.0 }, new[] { droot_R[1][0], droot_R[1][1], droot_R[1][2], 0.0 }, new[] { droot_R[2][0], droot_R[2][1], droot_R[2][2], 0.0 }, new[] { 0.0, 0.0, 0.0, 1.0 } };
            var R_refT = T3(R_ref);
            (double[][] R, double[] T) LocalOf(double[][] Rw, double[] Td)
            {
                double[] rel = { Td[0] - center[0], Td[1] - center[1], Td[2] - center[2] };
                rel = XformPt(droot4, rel);
                rel = Pitch(RotY(rel, WingYaw), pr).Select(c => c * S).ToArray();
                var Rl = Mul3(Rw, R_refT);
                var Tl = new double[3]; for (int i = 0; i < 3; i++) Tl[i] = anchorLocal[i] + ExactMath.Sum(rel[0] * R_refT[0][i], rel[1] * R_refT[1][i], rel[2] * R_refT[2][i]);
                return (Rl, Tl);
            }
            double[][] WorldR(double[][] Rd) => Mul3(Mul3(Mul3(Rd, droot_R), yawR), pitchR);
            var weightsAll = ModelCodec.LoadWeights(dranPack, "c12a.wgt")[dn["obj1"].I];
            var obj1 = dn["obj1"]; var om = MdtMesh.Parse(dranMds, obj1.MeshOff);
            var vwAll = om.Pos.Select(v => XformPt(obj1.World, new[] { v[0], v[1], v[2] })).ToList();
            int bas = nodes.Count; var wid = new Dictionary<int, int>();
            for (int k = 0; k < WingBones.Length; k++)
            {
                var d = dn[WingBones[k]];
                var (R, T) = LocalOf(WorldR(d.R), d.T);
                var n = new RigNode { I = bas + k, Name = WingBones[k], MeshOff = 0, Parent = parent, R = R, T = T, Quat = MatToQuat(R) };
                n.World = MatMul(MatFromRT(R, T), nodes[parent].World); n.InvWorld = RigidInv(n.World); n.WorldPos = new[] { n.World[3][0], n.World[3][1], n.World[3][2] };
                nodes.Add(n); wid[d.I] = bas + k;
            }
            var bindOpen = nodes.Skip(bas).ToDictionary(n => n.I, n => (n.R, n.T, n.World));
            var sides = new (string sd, string[] ch)[] { ("r", new[] { "r_wing1", "r_wing2", "r_wing3", "r_wing4" }), ("l", new[] { "l_wing1", "l_wing2", "l_wing3", "l_wing4" }) };
            var segLen = new Dictionary<string, double[]>(); var pivotLocal = new Dictionary<string, double[]>();
            foreach (var (sd, ch) in sides)
            {
                segLen[sd] = Enumerable.Range(0, 3).Select(i => ExactMath.Dist(dn[ch[i]].T, dn[ch[i + 1]].T) * S * WingSizeMul).ToArray();
                pivotLocal[sd] = (double[])nodes[wid[dn[ch[0]].I]].T.Clone();
            }
            List<double[]> FkLocals(string sd, IList<double[][]> Rls, double[] root = null)
            {
                var T = new List<double[]> { (double[])(root ?? pivotLocal[sd]).Clone() };
                for (int k = 0; k < 3; k++) T.Add(new[] { T[k][0] + segLen[sd][k] * Rls[k][0][0], T[k][1] + segLen[sd][k] * Rls[k][0][1], T[k][2] + segLen[sd][k] * Rls[k][0][2] });
                return T;
            }
            double[][] Basis(double[] span, double[] top)
            {
                span = Unit(span); double d = Sum3(top, span); top = Unit(new[] { top[0] - d * span[0], top[1] - d * span[1], top[2] - d * span[2] });
                return new[] { span, top, Cross(span, top) };
            }
            int loopStart = WingClips.Where(c => c.loop).Min(c => c.ds);
            var dtracks = new Dictionary<(int, int), Track>(); foreach (var t in ModelCodec.BuildTracks(dranPack, dmotName, dranNodes.Count)) dtracks[(t.Node, t.Chan)] = t;
            double[][] DranLocalR(string name, double? df)
            {
                var d = dn[name]; dtracks.TryGetValue((d.I, 0), out var rsrc);
                var Rw = (df != null && rsrc != null) ? WorldR(QuatToMat(Sample(rsrc, df.Value))) : WorldR(d.R);
                return LocalOf(Rw, d.T).R;
            }
            var R_stand = RotOf(CatWorld(WingFoldFrame)[parent]); var R_standT = T3(R_stand);
            var foldLocal = new Dictionary<string, (List<double[][]> Rls, List<double[]> Tls)>(); var foldRoot = new Dictionary<string, double[]>();
            foreach (var (sd, ch) in sides)
            {
                double sx = sd == "l" ? -1.0 : 1.0;
                var Rls = new List<double[][]>();
                for (int k = 0; k < 4; k++)
                {
                    string name = ch[k];
                    var Rf = DranLocalR(name, loopStart);
                    int bid = dn[name].I;
                    var loc = new List<double[]>();
                    foreach (var (v, infl) in weightsAll) if (infl.Any(bw => bw.bone == bid && bw.w >= 20)) loc.Add(   // (weights are 0..1: never true, so the sheet as a whole is used below — as authored)
                        XformPt(dranNodes[bid].InvWorld, vwAll[v]));
                    if (loc.Count < 4)
                    {
                        loc = new List<double[]>();
                        foreach (var (v, infl) in weightsAll) if (infl.Any(bw => dranNodes[bw.bone].Name.StartsWith(name.Substring(0, 2), StringComparison.Ordinal))) loc.Add(XformPt(dranNodes[bid].InvWorld, vwAll[v]));
                    }
                    double[] c = { ExactMath.Sum(loc.Select(p => p[0])) / loc.Count, ExactMath.Sum(loc.Select(p => p[1])) / loc.Count, ExactMath.Sum(loc.Select(p => p[2])) / loc.Count };
                    var nrm = PcaNormal(loc, c);
                    var spanF = (double[])Rf[0].Clone();
                    var topF = new double[3]; for (int i = 0; i < 3; i++) topF[i] = ExactMath.Sum(nrm[0] * Rf[0][i], nrm[1] * Rf[1][i], nrm[2] * Rf[2][i]);
                    var topW = new double[3]; for (int i = 0; i < 3; i++) topW[i] = ExactMath.Sum(topF[0] * R_ref[0][i], topF[1] * R_ref[1][i], topF[2] * R_ref[2][i]);
                    if (topW[1] < 0) topF = topF.Select(c_ => -c_).ToArray();
                    double[] spanT0 = { WingFold[k].span[0] * sx, WingFold[k].span[1], WingFold[k].span[2] }, topT0 = { WingFold[k].top[0] * sx, WingFold[k].top[1], WingFold[k].top[2] };
                    var spanT = new double[3]; var topT = new double[3];
                    for (int i = 0; i < 3; i++) { spanT[i] = ExactMath.Sum(spanT0[0] * R_standT[0][i], spanT0[1] * R_standT[1][i], spanT0[2] * R_standT[2][i]); topT[i] = ExactMath.Sum(topT0[0] * R_standT[0][i], topT0[1] * R_standT[1][i], topT0[2] * R_standT[2][i]); }
                    var Bf = Basis(spanF, topF); var Bt = Basis(spanT, topT);
                    var A = Mul3(T3(Bf), Bt);
                    Rls.Add(Mul3(Rf, A));
                }
                double[] shiftW = { WingFoldRootShift[0] * sx, WingFoldRootShift[1], WingFoldRootShift[2] };
                var shiftL = new double[3]; for (int i = 0; i < 3; i++) shiftL[i] = ExactMath.Sum(shiftW[0] * R_standT[0][i], shiftW[1] * R_standT[1][i], shiftW[2] * R_standT[2][i]);
                foldRoot[sd] = new[] { pivotLocal[sd][0] + shiftL[0], pivotLocal[sd][1] + shiftL[1], pivotLocal[sd][2] + shiftL[2] };
                foldLocal[sd] = (Rls, FkLocals(sd, Rls, foldRoot[sd]));
                for (int k = 0; k < 4; k++)
                {
                    var n = nodes[wid[dn[ch[k]].I]]; n.R = Rls[k]; n.T = foldLocal[sd].Tls[k]; n.Quat = MatToQuat(Rls[k]);
                    n.World = MatMul(MatFromRT(n.R, n.T), nodes[parent].World); n.InvWorld = RigidInv(n.World); n.WorldPos = new[] { n.World[3][0], n.World[3][1], n.World[3][2] };
                }
                for (int k = 1; k < 4; k++)
                {
                    var n = nodes[wid[dn[ch[k]].I]]; var pn = nodes[wid[dn[ch[k - 1]].I]];
                    var L = MatMul(n.World, RigidInv(pn.World));
                    n.Parent = pn.I; n.R = RotOf(L); n.T = TransOf(L); n.Quat = MatToQuat(n.R);
                    if (!(Math.Abs(n.T[0] - segLen[sd][k - 1]) < 1e-3 && Math.Abs(n.T[1]) < 1e-3 && Math.Abs(n.T[2]) < 1e-3)) throw new IOException($"wing chain {ch[k]}: segment length drift");
                }
            }
            // ── wing meshes carved from obj1 by weight ──
            var tris = ModelCodec.MdtTriangles(om);
            string Side(int vi)
            {
                if (!weightsAll.TryGetValue(vi, out var infl) || infl.Count == 0) return null;
                var best = infl[0]; foreach (var bw in infl) if (bw.w > best.w) best = bw;
                string nm = best.bone < dranNodes.Count ? dranNodes[best.bone].Name : "";
                return nm.StartsWith("r_wing", StringComparison.Ordinal) ? "r" : nm.StartsWith("l_wing", StringComparison.Ordinal) ? "l" : null;
            }
            var meshes = new List<SkinMesh>(); var wingSets = new Dictionary<string, (List<int[]>, List<int>, Dictionary<int, int>)>();
            foreach (var (sd, rootName) in new[] { ("r", "r_wing1"), ("l", "l_wing1") })
            {
                var wt = tris.Where(t => t.All(v => Side(v) == sd)).ToList();
                var used = wt.SelectMany(t => t).Distinct().OrderBy(v => v).ToList(); var remap = used.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
                wingSets[sd] = (wt, used, remap);
                var me = new SkinMesh { Node = wid[dn[rootName].I], Skin = true, Nv = used.Count, Tris = wt.Select(t => new[] { remap[t[0]], remap[t[1]], remap[t[2]] }).ToList() };
                foreach (int vi in used)
                {
                    var infl = (weightsAll.TryGetValue(vi, out var l) ? l : new List<(int, double)>()).Where(bw => wid.ContainsKey(bw.Item1)).OrderBy(bw => -bw.Item2).Take(2).ToList();
                    if (infl.Count == 0) infl = new List<(int, double)> { (dn[rootName].I, 1.0) };
                    var (ba, wa) = infl[0]; var (bb, wb) = infl.Count > 1 ? infl[1] : (ba, 0.0);
                    double tot = wa + wb;
                    var pa = XformPt(dranNodes[ba].InvWorld, vwAll[vi]).Select(c => c * S * WingSizeMul).ToArray();
                    var pb = XformPt(dranNodes[bb].InvWorld, vwAll[vi]).Select(c => c * S * WingSizeMul).ToArray();
                    me.B0.Add(wid[ba]); me.P0.Add(pa); me.B1.Add(wid[bb]); me.P1.Add(pb); me.W0.Add(tot != 0 ? wa / tot : 1.0);
                }
                meshes.Add(me);
                log($"  {sd} wing: {used.Count} verts, {wt.Count} tris");
            }
            var cw = ModelCodec.LoadWeights(catPack, "cat.wgt");
            foreach (var n in catNodes)
            {
                if (n.MeshOff == 0) continue;
                var per = cw != null && cw.TryGetValue(n.I, out var pv) && pv.Count > 0 ? pv : null;
                var mm = per != null ? ModelCodec.BuildMeshWeighted(catMds, n, nodes, per) : ModelCodec.BuildMesh(catMds, n, nodes);
                if (mm != null) meshes.Add(mm);
            }
            // ── tracks: the wing bones rigid to the spine, Dran's sampled pose inside the driven windows, the authored landing ──
            var windows = new List<(int cs, int ce, int ds, int de, int cycles, bool loop, int ki)>();
            foreach (var (ki, ds, de, dspd, loop) in WingClips.OrderBy(c => c.ki))
            {
                var (cs, ce, cspd, _) = CatPackBakes.CatKeys[ki];
                double winGame = (ce - cs) / cspd, loopGame = (de - ds) / dspd;
                int cycles = loop ? Math.Max(1, (int)ExactMath.Round(winGame / loopGame, 0)) : 1;
                windows.Add((cs, ce, ds, de, cycles, loop, ki));
            }
            var (lcs, lce, lds, lde, lfe) = WingLand;
            (double[] fa, double[] va) StrokeAxes() => (new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 });
            var idleRots = new Dictionary<string, List<double[][]>>(); var flapSign = new Dictionary<string, double>(); var sweepSign = new Dictionary<string, double>();
            {
                var w7 = windows.First(w => w.ki == StrokeKi); int wcs_ = w7.cs, wde_ = w7.de;
                var (fa, va) = StrokeAxes();
                foreach (var (sd, ch) in sides)
                {
                    var G = ch.Select(name => DranLocalR(name, wde_)).ToList();
                    double[] TipOf(IList<double[][]> Rs) => FkLocals(sd, Rs)[3];
                    var up = TipOf(G.Select(R => Mul3(R, RotAbout(fa, 80))).ToList());
                    flapSign[sd] = up[1] < TipOf(G)[1] ? 1.0 : -1.0;
                    var back = TipOf(G.Select(R => Mul3(R, RotAbout(va, 30))).ToList());
                    sweepSign[sd] = back[0] < TipOf(G)[0] ? 1.0 : -1.0;
                }
            }
            Dictionary<int, (double[][] R, double[] T)> WingLocals(double f)
            {
                foreach (var (ki, src) in WingHold) { var (hs, he, _, _) = CatPackBakes.CatKeys[ki]; if (hs <= f && f <= he) return WingLocals(src); }
                var outp = new Dictionary<int, (double[][], double[])>();
                foreach (var (sd, ch) in sides)
                {
                    double? df = null; int wki = -1;
                    foreach (var (cs, ce, ds, de, cycles, loop, ki) in windows)
                        if (cs <= f && f <= ce)
                        {
                            double u0 = (f - cs) / (double)(ce - cs);
                            df = ds + (loop ? (u0 * cycles * (de - ds)) % (de - ds) : u0 * (de - ds)); wki = ki;
                        }
                    double[] root = null; List<double[][]> Rls;
                    if (df != null)
                    {
                        if (wki == StrokeKi)
                        {
                            var w7 = windows.First(w => w.ki == wki); int wcs = w7.cs, wce = w7.ce, wde = w7.de;
                            double u = (f - wcs) / (double)(wce - wcs);
                            var (fa, va) = StrokeAxes();
                            if (!idleRots.ContainsKey(sd))
                            {
                                var idle = WingLocals(WingFoldFrame);
                                idleRots[sd] = ch.Select(name => idle[wid[dn[name].I]].R).ToList();
                            }
                            var stroke = new List<double[][]>(); Rls = new List<double[][]>();
                            {
                                var piv = pivotLocal[sd]; double hz = StrokeHingeIn * (piv[2] < 0 ? 1.0 : -1.0);
                                double[] C = { piv[0], piv[1], piv[2] + hz }, arm = { piv[0] - C[0], piv[1] - C[1], piv[2] - C[2] };
                                var Q = RotAbout(fa, KeyedVal(StrokePhi, f) * flapSign[sd]);
                                root = new double[3]; for (int i = 0; i < 3; i++) root[i] = C[i] + ExactMath.Sum(arm[0] * Q[0][i], arm[1] * Q[1][i], arm[2] * Q[2][i]);
                            }
                            for (int k = 0; k < 4; k++)
                            {
                                double uk = Math.Pow(u, 1.0 + k * StrokeLag), fk = wcs + uk * (wce - wcs);
                                var R = Mul3(DranLocalR(ch[k], wde), RotAbout(fa, KeyedVal(StrokePhi, fk) * flapSign[sd]));
                                double psi = StrokeSweep * (u - uk);
                                if (Math.Abs(psi) > 1e-6) R = Mul3(R, RotAbout(va, psi * sweepSign[sd]));
                                stroke.Add(R);
                                if (k == 0) { Rls.Add(R); continue; }
                                double e = KeyedVal(StrokeExtend, fk);
                                var relS = Mul3(stroke[k], T3(stroke[k - 1])); var relI = Mul3(idleRots[sd][k], T3(idleRots[sd][k - 1]));
                                var rel = e >= 1 ? relS : e <= 0 ? relI : QuatToMat(Slerp(MatToQuat(relI), MatToQuat(relS), e));
                                Rls.Add(Mul3(rel, Rls[k - 1]));
                            }
                        }
                        else Rls = ch.Select(name => DranLocalR(name, df)).ToList();
                        if (wki == 7)
                        {
                            double deg = f <= ClipPitch7[0].f ? ClipPitch7[0].v : ClipPitch7[^1].v;
                            for (int i = 0; i + 1 < ClipPitch7.Length; i++) if (ClipPitch7[i].f <= f && f <= ClipPitch7[i + 1].f) deg = ClipPitch7[i].v + (ClipPitch7[i + 1].v - ClipPitch7[i].v) * Smooth((f - ClipPitch7[i].f) / (ClipPitch7[i + 1].f - ClipPitch7[i].f));
                            if (Math.Abs(deg) > 1e-6)
                            {
                                double th = Radians(deg), c = Math.Cos(th), sn = Math.Sin(th);
                                var Qz = new[] { new[] { c, sn, 0.0 }, new[] { -sn, c, 0.0 }, new[] { 0.0, 0.0, 1.0 } };
                                Rls = Rls.Select(R => Mul3(R, Qz)).ToList();
                            }
                        }
                    }
                    else if (lcs <= f && f <= lce)
                    {
                        double dfl = lds + (lde - lds) * Math.Min(1.0, (f - lcs) / (double)(lfe - lcs));
                        Rls = new List<double[][]>();
                        for (int k = 0; k < 4; k++)
                        {
                            var Rf = DranLocalR(ch[k], dfl); var Rt = foldLocal[sd].Rls[k];
                            var (a, b) = WingLandLag[k]; double t = Smooth((f - a) / (b - a));
                            Rls.Add(t <= 0 ? Rf : t >= 1 ? Rt : QuatToMat(Slerp(MatToQuat(Rf), MatToQuat(Rt), t)));
                        }
                        { var (a, b) = WingLandLag[0]; double t = Smooth((f - a) / (b - a)); root = new double[3]; for (int i = 0; i < 3; i++) root[i] = pivotLocal[sd][i] + (foldRoot[sd][i] - pivotLocal[sd][i]) * t; }
                    }
                    else { Rls = foldLocal[sd].Rls; root = foldRoot[sd]; }
                    bool landing = lcs <= f && f <= lce;
                    if (landing || df == null)
                    {
                        var (a, b) = WingHandDroopFrames;
                        double s_ = (df == null && !landing) ? 1.0 : Smooth((f - a) / (b - a));
                        if (s_ > 0)
                        {
                            Rls = new List<double[][]>(Rls);
                            foreach (var (k, deg) in WingHandDroop)
                            {
                                double th = Radians(deg * s_), c = Math.Cos(th), sn = Math.Sin(th);
                                var Qz = new[] { new[] { c, sn, 0.0 }, new[] { -sn, c, 0.0 }, new[] { 0.0, 0.0, 1.0 } };
                                Rls[k] = Mul3(Rls[k], Qz);
                            }
                        }
                    }
                    if (landing || df == null)
                    {
                        var (a, b) = WingHandRollFrames;
                        double s_ = (df == null && !landing) ? 1.0 : Smooth((f - a) / (b - a));
                        if (s_ > 0)
                        {
                            double sgn = sd == "r" ? 1.0 : -1.0;
                            Rls = new List<double[][]>(Rls);
                            foreach (var (k, deg) in WingHandRoll)
                            {
                                double th = Radians(deg * s_ * sgn), c = Math.Cos(th), sn = Math.Sin(th);
                                var Rx = new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, c, sn }, new[] { 0.0, -sn, c } };
                                Rls[k] = Mul3(Rx, Rls[k]);
                            }
                        }
                    }
                    var Tls = FkLocals(sd, Rls, root);
                    for (int k = 0; k < 4; k++) outp[wid[dn[ch[k]].I]] = (Rls[k], Tls[k]);
                }
                return outp;
            }
            (Dictionary<int, double[][]> W, double[] anchor) WingPose(double f)
            {
                var Pw = CatWorld(f)[parent]; var loc = WingLocals(f);
                return (loc.ToDictionary(kv => kv.Key, kv => MatMul(MatFromRT(kv.Value.R, kv.Value.T), Pw)), XformPt(Pw, anchorLocal));
            }
            var (Wref, anchorRef) = WingPose(refFrame);
            var PrefInv = RigidInv(CatWorld(refFrame)[parent]);
            var fullEdges = new Dictionary<(int, int), int>();
            foreach (var t in tris) foreach (var e in new[] { Edge(t[0], t[1]), Edge(t[1], t[2]), Edge(t[2], t[0]) }) fullEdges[e] = fullEdges.TryGetValue(e, out int c0) ? c0 + 1 : 1;
            var Wcat = CatWorld(refFrame);
            var skinMe = meshes.First(x => x.Node == cn["cat_skin"].I);
            var skinRef = Enumerable.Range(0, skinMe.Nv).Select(i => ModelCodec.Skinned(skinMe, i, b => Wcat[b])).ToList();
            foreach (var (sd, _) in sides)
            {
                var me = meshes[sd == "r" ? 0 : 1];
                var (wt, used, remap) = wingSets[sd];
                var wedgeOrder = new List<(int, int)>(); var wedges = new Dictionary<(int, int), int>();
                foreach (var t in wt) foreach (var e in new[] { Edge(t[0], t[1]), Edge(t[1], t[2]), Edge(t[2], t[0]) }) { if (!wedges.ContainsKey(e)) { wedges[e] = 0; wedgeOrder.Add(e); } wedges[e]++; }
                var attachEdges = wedgeOrder.Where(e => wedges[e] == 1 && fullEdges.TryGetValue(e, out int fc) && fc >= 2).ToList();
                var adj = new Dictionary<int, List<int>>(); var adjOrder = new List<int>();
                foreach (var e in attachEdges)
                {
                    int va = e.Item1, vb = e.Item2;
                    if (!adj.ContainsKey(va)) { adj[va] = new List<int>(); adjOrder.Add(va); } adj[va].Add(vb);
                    if (!adj.ContainsKey(vb)) { adj[vb] = new List<int>(); adjOrder.Add(vb); } adj[vb].Add(va);
                }
                var chains = new List<List<int>>(); var seen = new HashSet<int>();
                foreach (int start in adjOrder.OrderBy(v => adj[v].Count).ThenBy(v => v))
                {
                    if (seen.Contains(start)) continue;
                    var ch_ = new List<int> { start }; int cur = start; int? prev = null; seen.Add(start);
                    while (true)
                    {
                        var nxt = adj[cur].Where(v => v != prev && !seen.Contains(v)).ToList();
                        if (nxt.Count == 0) break;
                        prev = cur; cur = nxt[0]; ch_.Add(cur); seen.Add(cur);
                    }
                    if (ch_.Count > 2 && adj[ch_[^1]].Contains(ch_[0])) ch_.Add(ch_[0]);
                    chains.Add(ch_);
                }
                double[] Wpos(int li) => ModelCodec.Skinned(me, li, b => Wref.TryGetValue(b, out var m) ? m : Wcat[b]);
                double sx = sd == "l" ? 1.0 : -1.0;
                int arm = cn[sd == "l" ? "cat_arm_hidari" : "cat_arm_migi"].I;
                int sb1 = cn["cat_sebone1"].I, sb2 = cn["cat_sebone2"].I;
                double? SkinY2(double x, double z)
                {
                    double? best = null;
                    foreach (var t in skinMe.Tris)
                    {
                        double[] A = skinRef[t[0]], B = skinRef[t[1]], C = skinRef[t[2]];
                        double det = (B[0] - A[0]) * (C[2] - A[2]) - (C[0] - A[0]) * (B[2] - A[2]);
                        if (Math.Abs(det) < 1e-9) continue;
                        double l1 = ((B[0] - x) * (C[2] - z) - (C[0] - x) * (B[2] - z)) / det;
                        double l2 = ((C[0] - x) * (A[2] - z) - (A[0] - x) * (C[2] - z)) / det;
                        double l3 = 1 - l1 - l2;
                        if (l1 < -1e-6 || l2 < -1e-6 || l3 < -1e-6) continue;
                        double y = l1 * A[1] + l2 * B[1] + l3 * C[1];
                        if (best == null || y > best) best = y;
                    }
                    return best;
                }
                var chainMax = chains.OrderByDescending(c => c.Count).First();
                var chain0 = new List<int>(chainMax);
                if (chain0.Count > 1 && chain0[0] == chain0[^1]) chain0.RemoveAt(chain0.Count - 1);
                var P = chain0.ToDictionary(vi => vi, vi => Wpos(remap[vi]));
                int bi = 0; for (int k = 1; k < chain0.Count; k++) if (Math.Abs(P[chain0[k]][0]) < Math.Abs(P[chain0[bi]][0])) bi = k;
                var chain = chain0.Skip(bi).Concat(chain0.Take(bi)).ToList();
                if (chain.Count > 2 && P[chain[1]][1] < P[chain[^1]][1]) chain = new List<int> { chain[0] }.Concat(chain.Skip(1).Reverse()).ToList();
                int fi = 0; for (int k = 1; k < chain.Count; k++) if (P[chain[k]][2] > P[chain[fi]][2]) fi = k;
                var top = chain.Skip(1).Take(fi).ToList();
                var pts_ = new Dictionary<string, int> { ["Rb"] = remap[chain[0]], ["Ri"] = remap[top[0]], ["Rf"] = remap[top[^1]], ["Rg"] = remap[fi + 1 < chain.Count ? chain[fi + 1] : chain[^1]] };
                int eb = chain[0], ei = top[0];
                var rt = wt.Where(t => t.Contains(eb) && t.Contains(ei)).SelectMany(t => t).Where(v => v != eb && v != ei).ToList();
                pts_["Rt"] = rt.Count > 0 ? remap[rt[0]] : pts_["Ri"];
                double[][] MRef(int b) => Wref.TryGetValue(b, out var m) ? m : Wcat[b];
                double[] WpAny(int i) => ModelCodec.Skinned(me, i, MRef);
                {   // the inboard fan's tip pulled outward toward the wing root
                    var (pull, reach) = WingApexPull;
                    foreach (int vi in used)
                    {
                        int li = remap[vi]; var q = Wpos(li); double x = Math.Abs(q[0]);
                        if (x >= reach) continue;
                        double d = pull * (reach - x) / reach;
                        double[] q2 = { q[0] + (sd == "r" ? -d : d), q[1], q[2] };
                        me.P0[li] = XformPt(RigidInv(MRef(me.B0[li])), q2); me.P1[li] = XformPt(RigidInv(MRef(me.B1[li])), q2);
                    }
                }
                var (Wfold, _) = WingPose(WingFoldFrame); var WcFold = CatWorld(WingFoldFrame);
                double[][] MFold(int b) => Wfold.TryGetValue(b, out var m) ? m : WcFold[b];
                {   // the fan's back edge moved forward so it stops cutting into the folded wing behind it
                    var (fwd, zmax, xmax, xfull) = WingRootForward;
                    foreach (int vi in used)
                    {
                        int li = remap[vi]; var q = Wpos(li); double x = Math.Abs(q[0]);
                        if (q[2] >= zmax || x >= xmax) continue;
                        double f_ = fwd * Math.Min(1.0, (xmax - x) / (xmax - xfull));
                        foreach (int slot in new[] { 0, 1 })
                        {
                            var M = MFold(slot == 0 ? me.B0[li] : me.B1[li]);
                            double[] dl = { f_ * M[0][2], f_ * M[1][2], f_ * M[2][2] };
                            var pp = slot == 0 ? me.P0 : me.P1;
                            pp[li] = new[] { pp[li][0] + dl[0], pp[li][1] + dl[1], pp[li][2] + dl[2] };
                        }
                    }
                }
                foreach (var (li, d) in WingFoldLift)
                {   // lift specific verts in the folded pose only (two-pose fit; the leap stays exact)
                    if (li >= used.Count) continue;
                    int b0_ = me.B0[li], b1_ = me.B1[li]; double w_ = me.W0[li];
                    var MA = (Col(MRef(b0_)), Col(MRef(b1_))); var MB = (Col(MFold(b0_)), Col(MFold(b1_)));
                    var tgtA = Blend(w_, MA.Item1, MA.Item2, me.P0[li], me.P1[li]); var tgtB = Blend(w_, MB.Item1, MB.Item2, me.P0[li], me.P1[li]); tgtB[1] += d;
                    var sol = TwoPoseFit(w_, MA, MB, tgtA, tgtB);
                    if (sol != null) { me.P0[li] = sol.Value.p0; me.P1[li] = sol.Value.p1; }
                }
                {   // Rm = the midpoint of the base's rear edge Rb–Rt, skinned as the pooled average of its ends
                    var accOrder = new List<int>(); var acc = new Dictionary<int, (double w, double[] p)>();
                    foreach (int vi in new[] { pts_["Rb"], pts_["Rt"] })
                        foreach (var (b, w, pl) in new[] { (me.B0[vi], me.W0[vi], me.P0[vi]), (me.B1[vi], 1.0 - me.W0[vi], me.P1[vi]) })
                        {
                            if (w <= 1e-6) continue;
                            if (!acc.ContainsKey(b)) { acc[b] = (0.0, new double[3]); accOrder.Add(b); }
                            var a = acc[b]; a.w += 0.5 * w; for (int c = 0; c < 3; c++) a.p[c] += 0.5 * w * pl[c]; acc[b] = a;
                        }
                    var infl = accOrder.Select(b => (b, acc[b])).OrderBy(x => -x.Item2.w).Take(2).ToList();
                    double tot = ExactMath.Sum(infl.Select(x => x.Item2.w));
                    var (ba, aa) = infl[0]; var (bb, ab) = infl.Count > 1 ? infl[1] : infl[0];
                    pts_["Rm"] = me.Nv; me.Nv++;
                    me.B0.Add(ba); me.P0.Add(aa.p.Select(c => c / aa.w).ToArray()); me.W0.Add(aa.w / tot);
                    me.B1.Add(bb); me.P1.Add(ab.p.Select(c => c / ab.w).ToArray());
                }
                (int i, double[] pos) BackPt(double x, double z)
                {
                    double xx = sx * x; var sy = SkinY2(xx, z);
                    while (sy == null && Math.Abs(xx) > 0.05) { xx -= sx * 0.05; sy = SkinY2(xx, z); }
                    double syv = sy ?? (anchorRef[1] - 0.4);
                    double[] pos = { xx, syv + WingExtendLift, z };
                    int spine = z < 0.9 ? sb1 : sb2;
                    double wSpine = Math.Min(Math.Max((0.6 - Math.Abs(xx)) / (0.6 - 0.35), 0.0), 1.0);
                    int i = me.Nv; me.Nv++;
                    me.B0.Add(spine); me.P0.Add(XformPt(RigidInv(Wcat[spine]), pos));
                    me.B1.Add(arm); me.P1.Add(XformPt(RigidInv(Wcat[arm]), pos)); me.W0.Add(wSpine);
                    return (i, pos);
                }
                var corners = new Dictionary<string, (double, double)> { ["ir"] = (WingExtendXIn, WingExtendZBack), ["if"] = (WingExtendXIn, WingExtendZFront), ["of"] = (WingExtendXOut, WingExtendZFrontOut), ["or"] = (WingExtendXOut, WingExtendZBack) };
                var cpos = new Dictionary<int, double[]>();
                foreach (string nm in WingTabPolys.SelectMany(p => p))
                    if (corners.ContainsKey(nm) && !pts_.ContainsKey(nm)) { var (i, pos) = BackPt(corners[nm].Item1, corners[nm].Item2); pts_[nm] = i; cpos[i] = pos; }
                var polys = new List<int[]>();
                var upper = chain.Take(fi + 1).Select(v => remap[v]).ToList();
                var lower = new List<int> { remap[chain[0]] }; lower.AddRange(chain.Skip(fi + 1).Reverse().Select(v => remap[v])); lower.Add(remap[chain[fi]]);
                {   // close the base: zip the ring's upper rim to its lower rim, always closing the shorter diagonal
                    int i_ = 0, j_ = 0;
                    while (i_ < upper.Count - 1 || j_ < lower.Count - 1)
                    {
                        bool advUpper;
                        if (i_ == upper.Count - 1) advUpper = false;
                        else if (j_ == lower.Count - 1) advUpper = true;
                        else { double du = ExactMath.Dist(WpAny(upper[i_ + 1]), WpAny(lower[j_])), dl = ExactMath.Dist(WpAny(upper[i_]), WpAny(lower[j_ + 1])); advUpper = du <= dl; }
                        if (advUpper) { if (upper[i_ + 1] != lower[j_]) polys.Add(new[] { upper[i_], upper[i_ + 1], lower[j_] }); i_++; }
                        else { if (lower[j_ + 1] != upper[i_]) polys.Add(new[] { upper[i_], lower[j_ + 1], lower[j_] }); j_++; }
                    }
                }
                var tabPolys = new List<(int[] tri, bool under)>();
                foreach (var poly in WingTabPolys)
                {
                    if (poly[0] == "RIM") { var rim = top.Select(v => remap[v]).ToList(); for (int k = 0; k + 1 < rim.Count; k++) tabPolys.Add((new[] { rim[k], rim[k + 1], pts_[poly[1]] }, false)); }
                    else tabPolys.Add((new[] { pts_[poly[0]], pts_[poly[1]], pts_[poly[2]] }, poly.Length > 3));
                }
                double[] WpOf(int i) => ModelCodec.Skinned(me, i, MRef);
                {   // the tabs' dihedral: each corner lowered toward the spine from the rim's height beside it
                    double th = Radians(WingTabTiltDeg), hx = P[top[0]][0];
                    double z0 = P[top[0]][2], y0 = P[top[0]][1], z1 = P[top[^1]][2], y1 = P[top[^1]][1];
                    foreach (var (i, pos) in cpos.ToList())
                    {
                        double t = z1 == z0 ? 0.0 : Math.Min(Math.Max((pos[2] - z0) / (z1 - z0), 0.0), 1.0);
                        double yRim = y0 + (y1 - y0) * t - WingRaise;
                        pos[1] = yRim - Math.Abs(pos[0] - hx) * Math.Tan(th);
                        me.P0[i] = XformPt(RigidInv(Wcat[me.B0[i]]), pos); me.P1[i] = XformPt(RigidInv(Wcat[me.B1[i]]), pos);
                    }
                }
                // subdivide the top tabs: each → n² triangles, new points pooled from their edge's ends
                int n = WingTabSubdiv; int rmIdx = pts_["Rm"];
                bool WingVert(int i) => i < used.Count || i == rmIdx;
                var mixed = new List<int>();
                var cache = new Dictionary<string, int>();
                int SubVertex(string key, List<(int e, double wgt)> ends)
                {
                    if (cache.TryGetValue(key, out int have)) return have;
                    var accOrder = new List<int>(); var acc = new Dictionary<int, (double w, double[] p)>();
                    foreach (var (vi, wgt) in ends)
                        foreach (var (b, w, pl) in new[] { (me.B0[vi], me.W0[vi], me.P0[vi]), (me.B1[vi], 1.0 - me.W0[vi], me.P1[vi]) })
                        {
                            if (w <= 1e-6) continue;
                            if (!acc.ContainsKey(b)) { acc[b] = (0.0, new double[3]); accOrder.Add(b); }
                            var a = acc[b]; a.w += wgt * w; for (int c = 0; c < 3; c++) a.p[c] += wgt * w * pl[c]; acc[b] = a;
                        }
                    var infl = accOrder.Select(b => (b, acc[b])).OrderBy(x => -x.Item2.w).Take(2).ToList();
                    double tot = ExactMath.Sum(infl.Select(x => x.Item2.w));
                    var (ba, aa) = infl[0]; var (bb, ab) = infl.Count > 1 ? infl[1] : infl[0];
                    int i = me.Nv; me.Nv++;
                    me.B0.Add(ba); me.P0.Add(aa.p.Select(c => c / aa.w).ToArray()); me.W0.Add(aa.w / tot);
                    me.B1.Add(bb); me.P1.Add(ab.p.Select(c => c / ab.w).ToArray());
                    if (ends.Any(e => WingVert(e.e)) && !ends.All(e => WingVert(e.e))) mixed.Add(i);
                    cache[key] = i; return i;
                }
                var fine = new List<int[]>();
                foreach (var (tri, under) in tabPolys)
                {
                    if (under) { polys.Add(tri); continue; }
                    var grid = new Dictionary<(int, int, int), int>();
                    for (int a_ = 0; a_ <= n; a_++) for (int b_ = 0; b_ <= n - a_; b_++)
                    {
                        int c_ = n - a_ - b_; var bc = new[] { a_, b_, c_ };
                        if (bc.Count(v => v == n) == 1) { grid[(a_, b_, c_)] = tri[Array.IndexOf(bc, n)]; continue; }
                        var zero = Enumerable.Range(0, 3).Where(k => bc[k] == 0).ToList();
                        string key; List<(int, double)> ends;
                        if (zero.Count == 1)
                        {
                            var nz = Enumerable.Range(0, 3).Where(k => k != zero[0]).ToList(); int e0 = nz[0], e1 = nz[1];
                            int lo = tri[e0] < tri[e1] ? e0 : e1, hi = tri[e0] < tri[e1] ? e1 : e0;
                            key = $"e|{tri[lo]}|{tri[hi]}|{bc[hi]}"; ends = new List<(int, double)> { (tri[e0], bc[e0] / (double)n), (tri[e1], bc[e1] / (double)n) };
                        }
                        else { key = $"t|{tri[0]},{tri[1]},{tri[2]}|{a_},{b_},{c_}"; ends = Enumerable.Range(0, 3).Select(k => (tri[k], bc[k] / (double)n)).ToList(); }
                        grid[(a_, b_, c_)] = SubVertex(key, ends);
                    }
                    for (int a_ = 0; a_ < n; a_++) for (int b_ = 0; b_ < n - a_; b_++)
                    {
                        int c_ = n - a_ - b_;
                        fine.Add(new[] { grid[(a_ + 1, b_, c_ - 1)], grid[(a_, b_ + 1, c_ - 1)], grid[(a_, b_, c_)] });
                        if (c_ >= 2) fine.Add(new[] { grid[(a_ + 1, b_, c_ - 1)], grid[(a_ + 1, b_ + 1, c_ - 2)], grid[(a_, b_ + 1, c_ - 1)] });
                    }
                }
                int? MidOf(int a_, int b_) => cache.TryGetValue($"e|{Math.Min(a_, b_)}|{Math.Max(a_, b_)}|1", out int m) ? m : null;
                List<int[]> Conform(int[] t)
                {
                    int a_ = t[0], b_ = t[1], c_ = t[2]; var m = new List<int?> { MidOf(a_, b_), MidOf(b_, c_), MidOf(c_, a_) };
                    int k = m.Count(x => x != null);
                    if (k == 0) return new List<int[]> { t };
                    if (k == 3) return new List<int[]> { new[] { a_, m[0].Value, m[2].Value }, new[] { m[0].Value, b_, m[1].Value }, new[] { m[2].Value, m[1].Value, c_ }, new[] { m[0].Value, m[1].Value, m[2].Value } };
                    while (m[0] == null || (k == 2 && m[1] == null)) { (a_, b_, c_) = (b_, c_, a_); m = new List<int?> { m[1], m[2], m[0] }; }
                    if (k == 1) return new List<int[]> { new[] { a_, m[0].Value, c_ }, new[] { m[0].Value, b_, c_ } };
                    return new List<int[]> { new[] { m[0].Value, b_, m[1].Value }, new[] { a_, m[0].Value, m[1].Value }, new[] { a_, m[1].Value, c_ } };
                }
                me.Tris = me.Tris.SelectMany(Conform).ToList();
                polys = polys.SelectMany(Conform).ToList();
                if (pts_.ContainsKey("ir") && pts_.ContainsKey("if"))
                {
                    var floor = Enumerable.Range(0, lower.Count - 2).Select(k => new[] { pts_["ir"], lower[k], lower[k + 1] }).ToList();
                    floor.Add(new[] { pts_["ir"], lower[^2], pts_["if"] });
                    floor = floor.SelectMany(Conform).ToList();
                    polys.AddRange(floor);
                }
                polys.AddRange(fine);
                if (mixed.Count > 0)
                {   // round the fold: each wing↔body midpoint pushed out along the shoulder in the folded pose (exact in the leap)
                    var nrm = Unit(new[] { 0.6 * (sd == "l" ? 1.0 : -1.0), 0.8, 0.0 });
                    foreach (int i in mixed)
                    {
                        int b0_ = me.B0[i], b1_ = me.B1[i]; double w_ = me.W0[i];
                        var MA = (Col(MRef(b0_)), Col(MRef(b1_))); var MB = (Col(MFold(b0_)), Col(MFold(b1_)));
                        var tgtA = Blend(w_, MA.Item1, MA.Item2, me.P0[i], me.P1[i]);
                        var bB = Blend(w_, MB.Item1, MB.Item2, me.P0[i], me.P1[i]); double[] tgtB = { bB[0] + WingTabBulge * nrm[0], bB[1] + WingTabBulge * nrm[1], bB[2] + WingTabBulge * nrm[2] };
                        var sol = TwoPoseFit(w_, MA, MB, tgtA, tgtB);
                        if (sol != null) { me.P0[i] = sol.Value.p0; me.P1[i] = sol.Value.p1; }
                    }
                }
                me.Tris.AddRange(polys);
                log($"  {sd} wing tabs: {polys.Count} tris, {cache.Count} new verts");
            }
            // ── the wing tracks: keys over the driven windows, the landing and the hold, bind brackets one frame outside ──
            var spans = windows.Select(w => (w.cs, w.ce)).ToList(); spans.Add((lcs, lce)); foreach (var (ki, _) in WingHold) { var (a, b, _, _) = CatPackBakes.CatKeys[ki]; spans.Add((a, b)); }
            var keyed = new SortedSet<int>(); foreach (var (cs, ce) in spans) for (int f = cs; f <= ce; f++) keyed.Add(f);
            var framesAll = new SortedSet<int>(keyed); foreach (var (cs, ce) in spans) { framesAll.Add(cs - 1); framesAll.Add(ce + 1); }
            var perFrame = framesAll.ToDictionary(f => f, f => WingLocals(f));
            foreach (int f in framesAll)
            {
                var loc = perFrame[f];
                foreach (var (sd, ch) in sides)
                {
                    var ids = ch.Select(name => wid[dn[name].I]).ToList();
                    var Ms = ids.Select(i => MatFromRT(loc[i].R, loc[i].T)).ToList();
                    for (int k = 1; k < 4; k++)
                    {
                        var L = MatMul(Ms[k], RigidInv(Ms[k - 1]));
                        double dev = Math.Max(Math.Max(Math.Abs(L[3][0] - segLen[sd][k - 1]), Math.Abs(L[3][1])), Math.Abs(L[3][2]));
                        if (!(dev < 1e-3)) throw new IOException($"wing chain drift at frame {f}, {ch[k]}");
                        loc[ids[k]] = (RotOf(L), nodes[ids[k]].T);
                    }
                }
            }
            var frames = framesAll.ToList();
            for (int k = 0; k < WingBones.Length; k++)
            {
                int nid = bas + k;
                var tr = new Track { Node = nid, Chan = 0 }; foreach (int f in frames) { tr.Frames.Add(f); tr.Vals.Add(MatToQuat(perFrame[f][nid].R)); }
                tracks.Add(tr);
                if (nodes[nid].Parent == parent) { var tt = new Track { Node = nid, Chan = 2 }; foreach (int f in frames) { tt.Frames.Add(f); tt.Vals.Add(new[] { perFrame[f][nid].T[0], perFrame[f][nid].T[1], perFrame[f][nid].T[2], 0.0 }); } tracks.Add(tt); }
            }
            log($"  wing tracks: keys at {frames[0]}..{frames[^1]} ({frames.Count} frames)");
            return new WingData { Nodes = nodes, Meshes = meshes, Tracks = tracks, Base = bas, Parent = parent, WingSets = wingSets, Om = om, FramesAll = frames, RefFrame = refFrame, Wref = Wref, Wcat = Wcat, BindOpen = bindOpen };
        }
    }
}
