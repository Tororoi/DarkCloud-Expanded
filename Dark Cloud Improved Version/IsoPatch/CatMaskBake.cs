using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Dark_Cloud_Improved_Version.RigMath;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The domino mask: a 2-D design (two lobes joined over a nose bridge, an eye hole in each) outlined as a signed-distance
    /// field, triangulated (Geometry2D.Delaunay) and cast outward from the head's centre onto the cat's face, lifted off the fur,
    /// bound whole to <see cref="MaskAnchor"/>.</summary>
    internal static class CatMaskBake
    {
        internal const string MaskAnchor = "cat_kao";
        private static readonly double[] MaskCore = { 0.0, 3.36, 3.35 };
        private const double MaskR = 0.88;
        private static readonly double[] MaskEye = { 0.507, 3.368 };
        private static readonly double[] MaskHole = { 0.32, 0.20 };
        private const double MaskHoleAt = 0.500, MaskHoleTilt = 11.0, MaskHoleRound = 0.70;
        private static readonly double[] MaskEyeShape = { 0.796, 0.999, 1.055, 0.980, 0.924, 0.934, 0.965, 0.990, 1.014, 1.024, 0.993, 0.926, 0.915, 1.025, 1.163, 1.199, 1.116, 1.023, 0.987, 0.952, 0.883, 0.827, 0.746, 0.681 };
        private static readonly double[] MaskLobe = { 0.80, 0.33, 0.40 };
        private const double MaskLobeRise = 0.03, MaskLobeTilt = 5.0, MaskBridge = 0.00;
        private const double MaskNoseTop = 3.20, MaskNoseW = 0.55, MaskNoseBlend = 0.13;
        private const double MaskLift = 0.050, MaskDrape = 0.00;
        private const int MaskSegs = 120; private const double MaskSmooth = 0.010, MaskLattice = 0.20, MaskGrade = 0.42; private const int MaskHoleSegs = 22;

        private static double EllipseSd(double[] p, double[] c, double rx, double ryu, double ryd, double tilt)
        {
            double ca = Math.Cos(-tilt), sa = Math.Sin(-tilt), dx = p[0] - c[0], dy = p[1] - c[1];
            double ux = dx * ca - dy * sa, uy = dx * sa + dy * ca, ry = uy >= 0 ? ryu : ryd;
            return (ExactMath.Hypot(ux / rx, uy / ry) - 1.0) * Math.Min(rx, ry);
        }
        private static double Smin(double a, double b, double k)
        {
            if (k <= 1e-6) return Math.Min(a, b);
            double h = Math.Max(0.0, Math.Min(1.0, 0.5 + 0.5 * (b - a) / k));
            return b * (1 - h) + a * h - k * h * (1 - h);
        }
        private static double Smax(double a, double b, double k) => -Smin(-a, -b, k);
        private static double MaskField(double[] p)
        {
            double tilt = Radians(MaskLobeTilt);
            double l0 = EllipseSd(p, new[] { -1 * MaskEye[0], MaskEye[1] + MaskLobeRise }, MaskLobe[0], MaskLobe[1], MaskLobe[2], -1 * tilt);
            double l1 = EllipseSd(p, new[] { 1 * MaskEye[0], MaskEye[1] + MaskLobeRise }, MaskLobe[0], MaskLobe[1], MaskLobe[2], 1 * tilt);
            double f = Smin(l0, l1, MaskBridge);
            if (MaskNoseW > 0) f = Smax(f, (MaskNoseTop - 0.25 * Math.Pow(p[0] / MaskNoseW, 2)) - p[1], MaskNoseBlend);
            return f;
        }
        private static double[] OutlineAt(double th)
        {
            double[] c = { 0.0, MaskEye[1] + MaskLobeRise }, d = { Math.Cos(th), Math.Sin(th) };
            double lo = 0.0, hi = 4.0;
            for (int i = 0; i < 40; i++) { double mid = 0.5 * (lo + hi); if (MaskField(new[] { c[0] + d[0] * mid, c[1] + d[1] * mid }) < 0) lo = mid; else hi = mid; }
            double r = 0.5 * (lo + hi);
            return new[] { c[0] + d[0] * r, c[1] + d[1] * r };
        }
        private static double SegDist(double[] p, double[] a, double[] b)
        {
            int n = a.Length; var ab = new double[n]; var ap = new double[n];
            for (int k = 0; k < n; k++) { ab[k] = b[k] - a[k]; ap[k] = p[k] - a[k]; }
            double n2 = ExactMath.Sum(ab.Select(c => c * c));
            double t = n2 < 1e-18 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ExactMath.Sum(Enumerable.Range(0, n).Select(k => ap[k] * ab[k])) / n2));
            return Math.Sqrt(ExactMath.Sum(Enumerable.Range(0, n).Select(k => Math.Pow(ap[k] - t * ab[k], 2))));
        }
        private static List<int> DecimateIdx(List<double[]> keys, double tol, int lo, int hi)
        {
            if (hi - lo < 2) return new List<int> { lo, hi };
            double worst = -1.0; int wi = lo;
            for (int i = lo + 1; i < hi; i++) { double d = SegDist(keys[i], keys[lo], keys[hi]); if (d > worst) { worst = d; wi = i; } }
            if (worst <= tol) return new List<int> { lo, hi };
            var left = DecimateIdx(keys, tol, lo, wi); left.RemoveAt(left.Count - 1); left.AddRange(DecimateIdx(keys, tol, wi, hi)); return left;
        }
        private static List<double[]> MaskOutlineHalf(Func<double, double, double[]> project)
        {
            int dense = MaskSegs; double tol = MaskSmooth;
            var line = new List<double[]>();
            for (int k = 0; k <= dense; k++) line.Add(OutlineAt(-Math.PI / 2 + Math.PI * k / dense));
            line[0] = new[] { 0.0, line[0][1] }; line[^1] = new[] { 0.0, line[^1][1] };
            var keys = project == null ? line : line.Select(q => project(q[0], q[1])).ToList();
            var kept = DecimateIdx(keys, tol, 0, keys.Count - 1);
            var outp = new List<int>();
            for (int i = 0; i + 1 < kept.Count; i++)
            {
                int a = kept[i], b = kept[i + 1];
                outp.Add(a);
                int n = (int)(ExactMath.Dist(line[a], line[b]) / MaskLattice);
                for (int k = 1; k < n; k++) outp.Add(a + (int)ExactMath.Round((b - a) * k / (double)n, 0));
            }
            outp.Add(kept[^1]);
            var seen = new HashSet<int>(); var idx = new List<int>();
            foreach (int i in outp) if (seen.Add(i)) idx.Add(i);
            return idx.Select(i => line[i]).ToList();
        }
        private static double EyeRadius(double th)
        {
            int n = MaskEyeShape.Length;
            double x = ExactMath.FMod(th, 2 * Math.PI) / (2 * Math.PI) * n;
            int i = ExactMath.Mod((int)x, n); double f = x - (int)x;
            double r = MaskEyeShape[i] * (1 - f) + MaskEyeShape[(i + 1) % n] * f;
            return r * (1 - MaskHoleRound) + MaskHoleRound;
        }
        private static List<double[]> MaskHoleRing(int side, int segs = 0, double grow = 0.0)
        {
            if (segs == 0) segs = MaskHoleSegs;
            double tilt = Radians(side * MaskHoleTilt), ca = Math.Cos(tilt), sa = Math.Sin(tilt), cx = side * MaskHoleAt, cy = MaskEye[1];
            var pts = new List<double[]>();
            for (int k = 0; k < segs; k++)
            {
                double th = -2 * Math.PI * k / segs;
                double r = EyeRadius(side > 0 ? th : Math.PI - th);
                double ux = MaskHole[0] * r * Math.Cos(th), uy = MaskHole[1] * r * Math.Sin(th);
                if (grow != 0) { double d = ExactMath.Hypot(ux, uy); if (d > 1e-9) { ux *= (d + grow) / d; uy *= (d + grow) / d; } }
                pts.Add(new[] { cx + ux * ca - uy * sa, cy + ux * sa + uy * ca });
            }
            return pts;
        }
        private static (List<double[]> poly, List<int[]> tris) MaskMesh2d(Func<double, double, double[]> project)
        {
            double h = MaskLattice;
            var pts = new List<double[]>(MaskOutlineHalf(project)); pts.AddRange(MaskHoleRing(1));
            var lip = MaskHoleRing(1, 64, 0.0);
            var bnd = new List<double[]>(pts);
            double step = h / 3.0;
            double y = MaskEye[1] - 1.2;
            while (y < MaskEye[1] + 1.2)
            {
                double x = 0.0;
                while (x < 1.8)
                {
                    double[] q = { x, y };
                    double d = bnd.Min(b => ExactMath.Dist(q, b));
                    double r = Math.Max(MaskGrade * h, Math.Min(h, 0.85 * d));
                    if (x > 1e-9 && x < 0.5 * r) { x += step; continue; }
                    if (MaskField(q) < -0.40 * r && !Geometry2D.PtInRing(q, MaskHoleRing(1, 40, 0.40 * r)) && pts.All(o => ExactMath.Dist(q, o) >= r)) pts.Add(q);
                    x += step;
                }
                y += step;
            }
            var half = new List<int[]>();
            foreach (var t in Geometry2D.Delaunay(pts))
            {
                double[] c = { ExactMath.Sum(pts[t[0]][0], pts[t[1]][0], pts[t[2]][0]) / 3.0, ExactMath.Sum(pts[t[0]][1], pts[t[1]][1], pts[t[2]][1]) / 3.0 };
                if (c[0] < 0) continue;
                if (MaskField(c) < 0 && !Geometry2D.PtInRing(c, lip)) half.Add(t);
            }
            var outp = new List<double[]>(pts);
            var mir = new Dictionary<int, int>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (Math.Abs(pts[i][0]) < 1e-9) mir[i] = i;
                else { mir[i] = outp.Count; outp.Add(new[] { -pts[i][0], pts[i][1] }); }
            }
            var tris = new List<int[]>(half); tris.AddRange(half.Select(t => new[] { mir[t[2]], mir[t[1]], mir[t[0]] }));
            var used = tris.SelectMany(t => t).Distinct().OrderBy(i => i).ToList();
            var ren = used.Select((i, k) => (i, k)).ToDictionary(x => x.i, x => x.k);
            return (used.Select(i => outp[i]).ToList(), tris.Select(t => new[] { ren[t[0]], ren[t[1]], ren[t[2]] }).ToList());
        }
        private static double MaskClearance()
        {
            double? worst = null;
            foreach (int side in new[] { -1, 1 }) foreach (var p in MaskHoleRing(side, 72)) { double d = -MaskField(p); if (worst == null || d < worst) worst = d; }
            return worst.Value;
        }
        private static double[] MaskDir(double u, double v)
        {
            double yaw = u / MaskR, pitch = (v - MaskCore[1]) / MaskR;
            return new[] { Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch) };
        }
        private static (double[] p, double[] n)? MaskRay(List<double[]> fur, List<int[]> tris, double u, double v)
        {
            var d = MaskDir(u, v); var o = MaskCore;
            double? best = null; var acc = new List<double[]>();
            foreach (var tri in tris)
            {
                double[] a = fur[tri[0]], b = fur[tri[1]], c = fur[tri[2]];
                double[] e1 = { b[0] - a[0], b[1] - a[1], b[2] - a[2] }, e2 = { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
                var h = Cross(d, e2);
                double det = Sum3(e1, h);
                if (Math.Abs(det) < 1e-9) continue;
                double[] s = { o[0] - a[0], o[1] - a[1], o[2] - a[2] };
                double uu = Sum3(s, h) / det;
                if (uu < -1e-9 || uu > 1 + 1e-9) continue;
                var q = Cross(s, e1);
                double vv = Sum3(d, q) / det;
                if (vv < -1e-9 || uu + vv > 1 + 1e-9) continue;
                double t = Sum3(e2, q) / det;
                if (t <= 0.05) continue;
                var n = Unit(Cross(e1, e2));
                if (Sum3(n, d) < 0) n = n.Select(x => -x).ToArray();
                if (best == null || t > best + 1e-9) { best = t; acc = new List<double[]> { n }; }
                else if (t > best - 1e-9) acc.Add(n);
            }
            if (best == null) return null;
            var nn = Unit(new[] { ExactMath.Sum(acc.Select(v_ => v_[0])), ExactMath.Sum(acc.Select(v_ => v_[1])), ExactMath.Sum(acc.Select(v_ => v_[2])) });
            return (new[] { o[0] + d[0] * best.Value, o[1] + d[1] * best.Value, o[2] + d[2] * best.Value }, nn);
        }
        /// <summary>The mask as a mesh rigidly bound to the head bone: the 2D design cast outward from the head's centre onto
        /// the face (every point on the right half, mirrored back), lifted off the fur.</summary>
        internal static SkinMesh BuildMaskMesh(List<RigNode> catNodes, SkinMesh skin, Action<string> log)
        {
            double[][] W(int b) => catNodes[b].World;
            var fur = Enumerable.Range(0, skin.Nv).Select(i => ModelCodec.Skinned(skin, i, W)).ToList();
            var head = catNodes.First(n => n.Name == MaskAnchor); var inv = RigidInv(head.World);
            double gap = MaskClearance();
            if (gap < 0.02) throw new IOException($"mask: the eye hole breaks the outline (clearance {gap:+0.000}) — widen MASK_LOBE or shrink MASK_HOLE");
            double[] Project(double u, double v) { var hit = MaskRay(fur, skin.Tris, Math.Abs(u), v); return hit != null ? hit.Value.p : new[] { Math.Abs(u), v, MaskCore[2] + MaskR }; }
            var (poly, tris) = MaskMesh2d(Project);
            var dirs = new List<(double[] d, double sd)>(); var floor = new List<double>(); int misses = 0;
            foreach (var uv in poly)
            {
                double u = uv[0], v = uv[1], au = Math.Abs(u);
                var d = MaskDir(au, v);
                var hit = MaskRay(fur, skin.Tris, au, v);
                foreach (double pull in new[] { 0.92, 0.85, 0.78, 0.7 })
                {
                    if (hit != null) break;
                    hit = MaskRay(fur, skin.Tris, au * pull, MaskEye[1] + (v - MaskEye[1]) * pull);
                    if (hit != null) misses++;
                }
                if (hit == null) { floor.Add(MaskR); misses++; }
                else
                {
                    var (p, n) = hit.Value;
                    double face = Math.Abs(Sum3(d, n));
                    floor.Add(ExactMath.Dist(p, MaskCore) + MaskLift / Math.Max(face, 0.35));
                }
                dirs.Add((d, u < 0 ? -1.0 : 1.0));
            }
            var ride = floor;                                                  // MaskDrape = 0: the mask hugs the head
            var verts = new List<double[]>();
            for (int i = 0; i < poly.Count; i++) { var (d, sd) = dirs[i]; double r = ride[i]; verts.Add(new[] { sd * (MaskCore[0] + d[0] * r), MaskCore[1] + d[1] * r, MaskCore[2] + d[2] * r }); }
            if (misses > 0) log($"  mask: {misses} of {poly.Count} points found no face on their ray (they fall back to the head sphere)");
            var me = new SkinMesh { Node = head.I, Skin = true, Nv = verts.Count, Tris = tris, Tag = "mask" };
            foreach (var v in verts) { var p = XformPt(inv, v); me.B0.Add(head.I); me.P0.Add(p); me.B1.Add(head.I); me.P1.Add((double[])p.Clone()); me.W0.Add(1.0); }
            return me;
        }
    }
}
