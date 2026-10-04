using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Dark_Cloud_Improved_Version.RigMath;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Super Steve cape: a rest lattice the engine runs as a CCloth, authored over the cat's skinned fur — the collar
    /// sampled on the shoulder ring, each row walked out over the body and hung off the rump, lifted clear of the collision bounds
    /// the .clo names. Rigidly bound to <see cref="CapeAnchor"/>; CatGraftEncoder writes the node, its MDT and the .clo text.</summary>
    internal static class CatCapeBake
    {
        private static readonly int[] CapeCollar = { 483, 503, 485, 477, 478 };
        private const double CapeWidth = 3.5, CapeLength = 4.0, CapeLift = 0.22;
        internal const int CapeCols = 12, CapeRows = 16;
        private const double CapeHang = 0.52, CapeRowTaut = 0.55, CapeSideSlope = 0.9, CapeBillow = 0.5;
        private static readonly double[] CapeCollarBias = { 0.18, 0.18 };
        internal const string CapeAnchor = "cat_sebone2";
        internal static readonly (double k, double gravity, double follow0, double follow1, double follow2, double wind, double normal) CapePhysics = (0.12, 0.0, 0.40, 0.30, 0.40, 0.25, -1.0);
        internal sealed class CapeBound { internal string Bone; internal double[] Centre, Axis, Radii; internal double Damp; }
        internal static readonly CapeBound[] CapeBounds =
        {
            new CapeBound { Bone = "cat_sebone2", Centre = new[] { 0.0, 3.05, 2.00 }, Axis = new[] { 0.0, 0.0, 1.0 }, Radii = new[] { 1.25, 0.70, 2.50 }, Damp = 0.7 },
            new CapeBound { Bone = "cat_sebone1", Centre = new[] { 0.0, 3.16, -0.30 }, Axis = new[] { 0.0, 0.0, 1.0 }, Radii = new[] { 1.25, 0.70, 2.30 }, Damp = 0.7 },
            new CapeBound { Bone = "cat_kosibone", Centre = new[] { 0.0, 2.75, -1.00 }, Axis = new[] { 0.0, 0.0, 1.0 }, Radii = new[] { 1.35, 1.10, 2.20 }, Damp = 0.7 },
            new CapeBound { Bone = "cat_kao", Centre = new[] { 0.0, 3.15, 3.55 }, Axis = new[] { 0.0, 0.0, 1.0 }, Radii = new[] { 1.10, 1.00, 0.90 }, Damp = 0.7 },
        };

        internal sealed class BoundLocal { internal string Bone; internal double[] A, B, Up, Radii; internal double Damp; }
        /// <summary>The bounds as the .clo BOUND lines want them, in each bone's bind-local space (A/B = centre ± axis, up = world +y).</summary>
        internal static List<BoundLocal> CapeBoundsLocal(List<RigNode> catNodes)
        {
            var byName = catNodes.ToDictionary(n => n.Name);
            var outp = new List<BoundLocal>();
            foreach (var b in CapeBounds)
            {
                var n = byName[b.Bone]; var inv = RigidInv(n.World); var R = n.World;
                var A = XformPt(inv, new[] { b.Centre[0] + b.Axis[0], b.Centre[1] + b.Axis[1], b.Centre[2] + b.Axis[2] });
                var B = XformPt(inv, new[] { b.Centre[0] - b.Axis[0], b.Centre[1] - b.Axis[1], b.Centre[2] - b.Axis[2] });
                var up = new double[3]; for (int j = 0; j < 3; j++) up[j] = ExactMath.Sum(R[j][0] * 0.0, R[j][1] * 1.0, R[j][2] * 0.0);
                outp.Add(new BoundLocal { Bone = b.Bone, A = A.Select(c => ExactMath.Round(c, 6)).ToArray(), B = B.Select(c => ExactMath.Round(c, 6)).ToArray(), Up = up.Select(c => ExactMath.Round(c, 6)).ToArray(), Radii = (double[])b.Radii.Clone(), Damp = b.Damp });
            }
            return outp;
        }

        private sealed class BoundWorld { internal int Node; internal double[] Centre; internal double[][] Axes; internal double[] Radii; }
        private static List<BoundWorld> CapeBoundsWorld(List<RigNode> catNodes)
        {
            var byName = catNodes.ToDictionary(n => n.Name);
            var outp = new List<BoundWorld>();
            foreach (var b in CapeBoundsLocal(catNodes))
            {
                var n = byName[b.Bone]; var Wm = n.World;
                var Aw = XformPt(Wm, b.A); var Bw = XformPt(Wm, b.B);
                var upw = new double[3]; for (int c = 0; c < 3; c++) upw[c] = ExactMath.Sum(b.Up[0] * Wm[0][c], b.Up[1] * Wm[1][c], b.Up[2] * Wm[2][c]);
                var z = Unit(new[] { Aw[0] - Bw[0], Aw[1] - Bw[1], Aw[2] - Bw[2] }); var x = Unit(Cross(upw, z)); var y = Cross(z, x);
                outp.Add(new BoundWorld { Node = n.I, Centre = new[] { (Aw[0] + Bw[0]) / 2, (Aw[1] + Bw[1]) / 2, (Aw[2] + Bw[2]) / 2 }, Axes = new[] { x, y, z }, Radii = (double[])b.Radii.Clone() });
            }
            return outp;
        }
        private static double BoundDepth(BoundWorld bw, double[] p)
        {
            double[] d = { p[0] - bw.Centre[0], p[1] - bw.Centre[1], p[2] - bw.Centre[2] };
            return Math.Sqrt(ExactMath.Sum(Enumerable.Range(0, 3).Select(i => Math.Pow(ExactMath.Sum(d[0] * bw.Axes[i][0], d[1] * bw.Axes[i][1], d[2] * bw.Axes[i][2]) / bw.Radii[i], 2))));
        }
        private static double[] ClearBounds(List<BoundWorld> bws, double[] p, double margin = 0.03, double maxMove = 1.0, double step = 0.04)
        {
            var q = (double[])p.Clone();
            if (bws.All(b => BoundDepth(b, q) >= 1 + margin)) return q;
            BoundWorld deep = null; double best = double.PositiveInfinity;
            foreach (var b in bws) { double dd = BoundDepth(b, q); if (dd < best) { best = dd; deep = b; } }
            double[] d;
            if (q[1] > deep.Centre[1]) d = new[] { 0.0, 1.0, 0.0 };
            else
            {
                d = new[] { q[0] - deep.Centre[0], 0.0, q[2] - deep.Centre[2] };
                double n = Math.Sqrt(d[0] * d[0] + d[2] * d[2]);
                d = n > 1e-6 ? new[] { d[0] / n, 0.0, d[2] / n } : new[] { 0.0, -1.0, 0.0 };
            }
            for (int it = 0; it < (int)(maxMove / step); it++)
            {
                q = new[] { q[0] + d[0] * step, q[1] + d[1] * step, q[2] + d[2] * step };
                if (bws.All(b => BoundDepth(b, q) >= 1 + margin)) return q;
            }
            return (double[])p.Clone();
        }

        /// <summary>(rows, cols, verts) — the cape's rest lattice row-major from the collar row, in the anchor's bind-local space.</summary>
        internal static (int rows, int cols, List<double[]> verts) CapeRestLocal(List<RigNode> catNodes, SkinMesh skin)
        {
            var me = BuildCapeMesh(catNodes, skin);
            double[][] W(int b) => catNodes[b].World;
            var world = Enumerable.Range(0, me.Nv).Select(i => ModelCodec.Skinned(me, i, W)).ToList();
            var anchor = catNodes.First(n => n.Name == CapeAnchor); var inv = RigidInv(anchor.World);
            return (CapeRows, CapeCols, world.Select(v => XformPt(inv, v)).ToList());
        }

        /// <summary>The cape's rest lattice: each row walks the body's surface outward from the spine, continues past the flank
        /// at CapeSideSlope, is flattened toward its chord and sampled at equal arc length; rows past the rump hang off the last;
        /// the spine profile is raised onto its upper hull, the billow eases in along the hang, rows clear the collision bounds.
        /// Bound rigidly to the anchor bone, as the engine's cloth is.</summary>
        internal static SkinMesh BuildCapeMesh(List<RigNode> catNodes, SkinMesh skin)
        {
            int cols = CapeCols;
            double[][] W(int b) => catNodes[b].World;
            var fur = Enumerable.Range(0, skin.Nv).Select(i => ModelCodec.Skinned(skin, i, W)).ToList();
            double? Cast(double x, double z)
            {
                double? best = null;
                foreach (var t in skin.Tris)
                {
                    double[] a = fur[t[0]], b = fur[t[1]], c = fur[t[2]];
                    double d = (b[2] - a[2]) * (c[0] - a[0]) - (c[2] - a[2]) * (b[0] - a[0]);
                    if (Math.Abs(d) < 1e-9) continue;
                    double u = ((z - a[2]) * (c[0] - a[0]) - (x - a[0]) * (c[2] - a[2])) / d;
                    double v = ((x - a[0]) * (b[2] - a[2]) - (z - a[2]) * (b[0] - a[0])) / d;
                    if (u < 0 || v < 0 || u + v > 1) continue;
                    double y = a[1] + u * (b[1] - a[1]) + v * (c[1] - a[1]);
                    if (best == null || y > best) best = y;
                }
                return best;
            }
            double? Ridge(double x, double z, double dz = 0.2)
            {
                var here = Cast(x, z);
                if (here == null) return null;
                var hits = new List<(double w, double y)>();
                foreach (var (w, o) in new[] { (0.25, -dz), (0.25, dz) }) { var y = Cast(x, z + o); if (y != null) hits.Add((w, y.Value)); }
                hits.Add((0.5, here.Value));
                return ExactMath.Sum(hits.Select(h => h.w * h.y)) / ExactMath.Sum(hits.Select(h => h.w));
            }
            var bws = CapeBoundsWorld(catNodes);
            var ring = CapeCollar.Select(i => ModelCodec.Skinned(skin, i, W)).ToList();
            var arc = new List<double> { 0.0 };
            for (int k = 1; k < ring.Count; k++) arc.Add(arc[^1] + ExactMath.Dist(ring[k - 1], ring[k]));
            var collar = new List<double[]>();
            for (int c = 0; c < cols; c++)
            {
                double want = arc[^1] * c / (cols - 1);
                int k = Enumerable.Range(1, ring.Count - 1).FirstOrDefault(kk => arc[kk] >= want, ring.Count - 1);
                double t = arc[k] > arc[k - 1] ? (want - arc[k - 1]) / (arc[k] - arc[k - 1]) : 0.0;
                collar.Add(new[] { ring[k - 1][0] + (ring[k][0] - ring[k - 1][0]) * t, ring[k - 1][1] + (ring[k][1] - ring[k - 1][1]) * t, ring[k - 1][2] + (ring[k][2] - ring[k - 1][2]) * t });
            }
            var top = new List<double[]>();
            for (int c = 0; c < cols; c++)
            {
                double g = Math.Cos(Math.PI / 2 * Math.Abs(2 * c / (double)(cols - 1) - 1));
                var p = collar[c]; top.Add(new[] { p[0], p[1] + CapeCollarBias[0] * g, p[2] + CapeCollarBias[1] * g });
            }
            double zc = ExactMath.Sum(collar.Select(p => p[2])) / cols;
            double topHalf = top.Max(p => Math.Abs(p[0]));

            List<double[]> Section(double z, double halfX)
            {
                const double step = 0.03;
                var outp = new double[cols][];
                var raw = new Dictionary<int, List<double[]>>();
                foreach (int way in new[] { -1, 1 })
                {
                    var pts = new List<double[]>(); double x = 0.0; double[] edge = null;
                    while (Math.Abs(x) <= halfX + step)
                    {
                        double y; var ry = Ridge(x, z);
                        if (ry != null) { y = ry.Value + CapeLift; edge = new[] { x, y }; }
                        else if (edge != null) y = edge[1] - CapeSideSlope * Math.Abs(x - edge[0]);
                        else return null;
                        pts.Add(new[] { x, y }); x += way * step;
                    }
                    raw[way] = pts;
                }
                foreach (int way in new[] { -1, 1 })
                {
                    var pts = raw[way];
                    for (int it = 0; it < 6; it++) for (int i = 1; i < pts.Count - 1; i++) pts[i][1] = 0.25 * pts[i - 1][1] + 0.5 * pts[i][1] + 0.25 * pts[i + 1][1];
                }
                if (CapeRowTaut < 1.0)
                {
                    double xa = raw[-1][^1][0], ya = raw[-1][^1][1], xb = raw[1][^1][0], yb = raw[1][^1][1];
                    foreach (int way in new[] { -1, 1 })
                        foreach (var q in raw[way])
                        {
                            double t = Math.Abs(xb - xa) > 1e-6 ? (q[0] - xa) / (xb - xa) : 0.0;
                            double chord = ya + (yb - ya) * t;
                            q[1] = chord + (q[1] - chord) * CapeRowTaut;
                        }
                }
                foreach (int way in new[] { -1, 1 })
                {
                    var pts = new List<double[]>(); double a = 0.0; double[] last = null;
                    foreach (var q in raw[way]) { if (last != null) a += ExactMath.Dist(q, last); pts.Add(new[] { q[0], q[1], a }); last = q; }
                    for (int c = 0; c < cols; c++)
                    {
                        double f = (c / (double)(cols - 1) - 0.5) * 2;
                        if ((f < 0 && way > 0) || (f > 0 && way < 0) || (f == 0 && way > 0)) continue;
                        double want = Math.Abs(f) * pts[^1][2];
                        double qx = pts[^1][0], qy = pts[^1][1];
                        for (int i = 1; i < pts.Count; i++)
                            if (pts[i][2] >= want)
                            {
                                double x0 = pts[i - 1][0], y0 = pts[i - 1][1], a0 = pts[i - 1][2], x1 = pts[i][0], y1 = pts[i][1], a1 = pts[i][2];
                                double t = a1 > a0 ? (want - a0) / (a1 - a0) : 0.0;
                                qx = x0 + (x1 - x0) * t; qy = y0 + (y1 - y0) * t; break;
                            }
                        outp[c] = new[] { qx, qy, z };
                    }
                }
                return outp.ToList();
            }

            var rows = new List<List<double[]>> { top.Select(p => (double[])p.Clone()).ToList() };
            for (int r = 1; r < CapeRows; r++)
            {
                double s_ = r / (double)(CapeRows - 1), z = zc - CapeLength * s_;
                var row = Section(z, topHalf * (1 - s_) + (CapeWidth / 2) * s_);
                if (row == null) row = Enumerable.Range(0, cols).Select(c => new[] { rows[^1][c][0], rows[^1][c][1] - CapeHang * (CapeLength / (CapeRows - 1)), z }).ToList();
                rows.Add(row);
            }
            int mid = cols / 2;
            var prof = Enumerable.Range(0, CapeRows).Select(r => (z: rows[r][mid][2], y: rows[r][mid][1], r)).OrderBy(q => q.z).ThenBy(q => q.y).ThenBy(q => q.r).ToList();
            var hull = new List<(double z, double y, int r)>();
            foreach (var q in prof)
            {
                while (hull.Count >= 2)
                {
                    var (oz, oy, _) = hull[^2]; var (az, ay, _) = hull[^1];
                    if ((az - oz) * (q.y - oy) - (ay - oy) * (q.z - oz) >= 0) hull.RemoveAt(hull.Count - 1); else break;
                }
                hull.Add(q);
            }
            foreach (var (z, y, r) in prof)
            {
                if (r == 0) continue;
                for (int i = 0; i + 1 < hull.Count; i++)
                {
                    var (z0, y0, _) = hull[i]; var (z1, y1, _) = hull[i + 1];
                    if (z0 <= z && z <= z1)
                    {
                        double lift = (y0 + (y1 - y0) * (z1 > z0 ? (z - z0) / (z1 - z0) : 0.0)) - y;
                        if (lift > 0) for (int c = 0; c < cols; c++) rows[r][c][1] += lift;
                        break;
                    }
                }
            }
            for (int r = 1; r < CapeRows; r++) { double lift = CapeBillow * Math.Pow(r / (double)(CapeRows - 1), 1.5); for (int c = 0; c < cols; c++) rows[r][c][1] += lift; }
            for (int r = 1; r < CapeRows; r++)
            {
                double lift = rows[r].Max(v => ClearBounds(bws, v)[1] - v[1]);
                if (lift > 0) for (int c = 0; c < cols; c++) rows[r][c][1] += lift;
            }
            var verts = rows.SelectMany(row => row).Select(v => (double[])v.Clone()).ToList();
            var tris = new List<int[]>();
            for (int r = 0; r < CapeRows - 1; r++) for (int c = 0; c < cols - 1; c++) { int a = r * cols + c, bb = a + 1, cc = a + cols, dd = cc + 1; tris.Add(new[] { a, bb, dd }); tris.Add(new[] { a, dd, cc }); }
            var anchor = catNodes.First(n => n.Name == CapeAnchor); var inv = RigidInv(anchor.World);
            var p0 = verts.Select(v => XformPt(inv, v)).ToList();
            var me = new SkinMesh { Node = skin.Node, Skin = true, Nv = verts.Count, Tris = tris, Tag = "cape" };
            for (int i = 0; i < verts.Count; i++) { me.B0.Add(anchor.I); me.P0.Add(p0[i]); me.B1.Add(anchor.I); me.P1.Add((double[])p0[i].Clone()); me.W0.Add(1.0); }
            return me;
        }
    }
}
