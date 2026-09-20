using System;
using System.Collections.Generic;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Pure triangle geometry for the collision bakes: box/plane predicates, rounded triangle keys, vector math,
    /// the coplanar-merge engine and the authored-quad builders. Triangles are double[3][3]; keys round the way Python's
    /// round does so the same triangles match.</summary>
    internal static class CollisionGeom
    {
        internal static double[][] Tri(double[] a, double[] b, double[] c) => new[] { a, b, c };
        internal static double[][] Tri(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz)
            => new[] { new[] { ax, ay, az }, new[] { bx, by, bz }, new[] { cx, cy, cz } };
        internal static List<double[][]> TrisFrom(double[,] rows) => CollisionMdt.TrisFrom(rows);
        internal static double[][] CopyTri(double[][] t) => new[] { (double[])t[0].Clone(), (double[])t[1].Clone(), (double[])t[2].Clone() };

        internal static bool Box(double[][] t, double x0, double x1, double y0, double y1, double z0, double z1, double e = 0.5)
            => t.All(p => x0 - e <= p[0] && p[0] <= x1 + e && y0 - e <= p[1] && p[1] <= y1 + e && z0 - e <= p[2] && p[2] <= z1 + e);
        internal static bool PlaneX(double[][] t, double xv, double z0, double z1, double y0, double y1, double e = 0.5)
            => t.All(p => Math.Abs(p[0] - xv) < e && z0 - e <= p[2] && p[2] <= z1 + e && y0 - e <= p[1] && p[1] <= y1 + e);
        internal static bool PlaneZ(double[][] t, double zv, double x0, double x1, double y0, double y1, double e = 0.5)
            => t.All(p => Math.Abs(p[2] - zv) < e && x0 - e <= p[0] && p[0] <= x1 + e && y0 - e <= p[1] && p[1] <= y1 + e);
        internal static bool Horiz(double[][] t)
        {
            double[] e1 = { t[1][0] - t[0][0], t[1][1] - t[0][1], t[1][2] - t[0][2] }, e2 = { t[2][0] - t[0][0], t[2][1] - t[0][1], t[2][2] - t[0][2] };
            double[] n = { e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0] };
            double h = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]); if (h == 0) h = 1.0;
            return Math.Abs(n[1]) > 0.7 * h;
        }

        private static int Cmp3(double[] a, double[] b) { for (int i = 0; i < 3; i++) { int c = a[i].CompareTo(b[i]); if (c != 0) return c; } return 0; }
        private static double[] R3(double[] p, int nd) => new[] { PyMath.Round(p[0], nd) + 0.0, PyMath.Round(p[1], nd) + 0.0, PyMath.Round(p[2], nd) + 0.0 };   // +0.0: −0 keys as 0, as Python's numeric equality does

        /// <summary>Winding-agnostic key: the three integer-rounded vertices, sorted.</summary>
        internal static string TriKeyInt(double[][] t) => TriKeyRounded(t, 0);
        internal static string TriKeyRounded(double[][] t, int nd)
        {
            var v = t.Select(p => R3(p, nd)).ToList(); v.Sort(Cmp3);
            return string.Join("|", v.Select(p => $"{p[0]:R},{p[1]:R},{p[2]:R}"));
        }
        /// <summary>Winding-preserving key: the minimal cyclic rotation of the three integer-rounded vertices.</summary>
        internal static string TriKeyWinding(double[][] t)
        {
            var v = t.Select(p => R3(p, 0)).ToList();
            double[][] best = null;
            for (int r = 0; r < 3; r++)
            {
                var rot = new[] { v[r], v[(r + 1) % 3], v[(r + 2) % 3] };
                if (best == null || CmpRot(rot, best) < 0) best = rot;
            }
            return string.Join("|", best.Select(p => $"{p[0]:R},{p[1]:R},{p[2]:R}"));
        }
        private static int CmpRot(double[][] a, double[][] b) { for (int i = 0; i < 3; i++) { int c = Cmp3(a[i], b[i]); if (c != 0) return c; } return 0; }

        internal static double[] TriangleNormal(double[][] t)
        {
            double[] a = t[0], b = t[1], c = t[2];
            double[] u = { b[0] - a[0], b[1] - a[1], b[2] - a[2] }, v = { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
            return new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
        }
        internal static double Dot3(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        internal static double[] Cross3(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        internal static double[] Unit3(double[] a) { double L = Math.Sqrt(Dot3(a, a)); if (L == 0) L = 1.0; return new[] { a[0] / L, a[1] / L, a[2] / L }; }

        private static bool PtInTri2(double px, double py, (double, double)[] tt)
        {
            var (x0, y0) = tt[0]; var (x1, y1) = tt[1]; var (x2, y2) = tt[2];
            double d1 = (px - x1) * (y0 - y1) - (x0 - x1) * (py - y1);
            double d2 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
            double d3 = (px - x0) * (y2 - y0) - (x2 - x0) * (py - y0);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        /// <summary>Cover the true cells of a grid with maximal rectangles (extend right, then down), never spanning a hole.</summary>
        private static List<(int i0, int j0, int i1, int j1)> GreedyRects(bool[][] cov)
        {
            int nx = cov.Length, ny = cov[0].Length;
            var used = new bool[nx][]; for (int i = 0; i < nx; i++) used[i] = new bool[ny];
            var outp = new List<(int, int, int, int)>();
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
            {
                if (!cov[i][j] || used[i][j]) continue;
                int i1 = i; while (i1 + 1 < nx && cov[i1 + 1][j] && !used[i1 + 1][j]) i1++;
                int j1 = j;
                while (j1 + 1 < ny) { bool ok = true; for (int ii = i; ii <= i1; ii++) if (!(cov[ii][j1 + 1] && !used[ii][j1 + 1])) { ok = false; break; } if (!ok) break; j1++; }
                for (int ii = i; ii <= i1; ii++) for (int jj = j; jj <= j1; jj++) used[ii][jj] = true;
                outp.Add((i, j, i1, j1));
            }
            return outp;
        }

        /// <summary>A rectangle [a0,a1]×[b0,b1] of the plane {p · nn = d} back in 3D via the in-plane basis (u, v), two tris
        /// wound so the face normal matches nn.</summary>
        private static List<double[][]> PlaneQuad(double[] u, double[] v, double[] nn, double d, double a0, double b0, double a1, double b1)
        {
            double[] P(double a, double b) => new[] { a * u[0] + b * v[0] + d * nn[0], a * u[1] + b * v[1] + d * nn[1], a * u[2] + b * v[2] + d * nn[2] };
            double[] A = P(a0, b0), B = P(a1, b0), C = P(a1, b1), D = P(a0, b1);
            double[][] t1 = { A, B, C }, t2 = { A, C, D };
            if (Dot3(TriangleNormal(t1), nn) < 0) { t1 = new[] { A, C, B }; t2 = new[] { A, D, C }; }
            return new List<double[][]> { t1, t2 };
        }

        /// <summary>Merge coplanar tris (any plane orientation) into minimal rectangles, preserving holes; 2D coordinates snap
        /// to a <paramref name="snap"/> grid; vertical facades are extended up to the group's tallest point (only from each
        /// column's lowest — or with <paramref name="keepWindows"/> highest — wall cell), optionally forced to <paramref name="top"/>;
        /// the merged plane is authored <paramref name="outward"/> behind the outermost source point.</summary>
        internal static List<double[][]> SimplifyCoplanar(List<double[][]> tris, double snap = 5.0, double outward = 0.0, double? top = null, bool keepWindows = false)
        {
            double Sn(double x) => PyMath.Round(x / snap, 0) * snap;
            var keys = new List<string>(); var groups = new Dictionary<string, (double[] nn, double d, List<double[][]> g)>();
            var outp = new List<double[][]>();
            foreach (var t in tris)
            {
                var n = TriangleNormal(t);
                if (Math.Sqrt(Dot3(n, n)) < 1e-9) { outp.Add(t); continue; }
                var nn = Unit3(n); double d = Dot3(nn, t[0]);
                double k0 = PyMath.Round(nn[0], 1) + 0.0, k1 = PyMath.Round(nn[1], 1) + 0.0, k2 = PyMath.Round(nn[2], 1) + 0.0, kd = PyMath.Round(d / snap, 0) * snap;
                string key = $"{k0:R},{k1:R},{k2:R}|{kd:R}";
                if (!groups.ContainsKey(key)) { groups[key] = (nn, d, new List<double[][]>()); keys.Add(key); }
                groups[key].g.Add(t);
            }
            foreach (string key in keys)
            {
                var (nn, d0, g) = groups[key];
                if (g.Count < 2) { outp.AddRange(g); continue; }
                double d = g.SelectMany(t => t).Min(p => Dot3(nn, p)) - outward;
                double[] up = { 0.0, 1.0, 0.0 };
                double dup = Dot3(up, nn);
                double[] vp = { -dup * nn[0], -dup * nn[1], -dup * nn[2] };
                vp[1] += 1.0;
                double[] u, v;
                if (Dot3(vp, vp) < 1e-6) { u = Unit3(new[] { 1.0 - nn[0] * nn[0], -nn[0] * nn[1], -nn[0] * nn[2] }); v = Cross3(nn, u); }
                else { v = Unit3(vp); u = Unit3(Cross3(v, nn)); }
                var tri2d = g.Select(t => t.Select(p => (Sn(Dot3(p, u)), Sn(Dot3(p, v)))).ToArray()).ToList();
                var us = tri2d.SelectMany(tt => tt).Select(p => p.Item1).Distinct().OrderBy(x => x).ToList();
                var vs = tri2d.SelectMany(tt => tt).Select(p => p.Item2).Distinct().OrderBy(x => x).ToList();
                if (us.Count < 2 || vs.Count < 2) { outp.AddRange(g); continue; }
                var cov = new bool[us.Count - 1][];
                for (int i = 0; i < us.Count - 1; i++)
                {
                    cov[i] = new bool[vs.Count - 1];
                    for (int j = 0; j < vs.Count - 1; j++)
                        cov[i][j] = tri2d.Any(tt => PtInTri2((us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2, tt));
                }
                if (v[1] > 0.9)
                    foreach (var col in cov)
                    {
                        var occ = Enumerable.Range(0, col.Length).Where(j => col[j]).ToList();
                        if (occ.Count == 0) continue;
                        for (int j = keepWindows ? occ.Max() : occ.Min(); j < col.Length; j++) col[j] = true;
                    }
                var fresh = new List<double[][]>();
                foreach (var (i0, j0, i1, j1) in GreedyRects(cov))
                {
                    double b1 = vs[j1 + 1];
                    if (top != null && v[1] > 0.9 && Math.Abs(b1 - vs[^1]) < 1e-6) b1 = top.Value;
                    fresh.AddRange(PlaneQuad(u, v, nn, d, us[i0], vs[j0], us[i1 + 1], b1));
                }
                outp.AddRange(fresh.Count < g.Count ? fresh : g);
            }
            return outp;
        }

        /// <summary>Selector for tris on the plane {coord[axis] = off} (all verts within 2) whose centroid lies in the ranges.</summary>
        internal static Func<double[][], bool> PlaneRegion(int axis, double off, (double, double)? x = null, (double, double)? y = null, (double, double)? z = null)
            => t =>
            {
                if (!t.All(p => Math.Abs(p[axis] - off) < 2)) return false;
                double[] c = { (t[0][0] + t[1][0] + t[2][0]) / 3.0, (t[0][1] + t[1][1] + t[2][1]) / 3.0, (t[0][2] + t[1][2] + t[2][2]) / 3.0 };
                var rngs = new[] { x, y, z };
                for (int i = 0; i < 3; i++) if (rngs[i] != null && !(rngs[i].Value.Item1 <= c[i] && c[i] <= rngs[i].Value.Item2)) return false;
                return true;
            };

        /// <summary>Quad (a, b, c, d) as two tris wound so the face normal points like <paramref name="want"/>.</summary>
        internal static List<double[][]> DirQuad(double[] a, double[] b, double[] c, double[] d, double[] want)
        {
            var tt = new List<double[][]> { new[] { a, b, c }, new[] { a, c, d } };
            return Dot3(TriangleNormal(tt[0]), want) >= 0 ? tt : new List<double[][]> { new[] { a, c, b }, new[] { a, d, c } };
        }
        internal static List<double[][]> Quad(double[] a, double[] b, double[] c, double[] d) => new List<double[][]> { new[] { a, b, c }, new[] { a, c, d } };
        internal static double[] V(double x, double y, double z) => new[] { x, y, z };
    }
}
