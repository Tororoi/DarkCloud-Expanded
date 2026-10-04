using System;
using System.Collections.Generic;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Plane geometry for the authored meshes: the signed doubled area of a triangle, Bowyer-Watson Delaunay triangulation
    /// and a point-in-polygon test. Points are double[2].</summary>
    internal static class Geometry2D
    {
        internal static double Area2(double[] a, double[] b, double[] c) => (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1]);
        private static bool InCircum(List<double[]> P, int[] t, double[] p)
        {
            double[] a = P[t[0]], b = P[t[1]], c = P[t[2]];
            double ax = a[0] - p[0], ay = a[1] - p[1], bx = b[0] - p[0], by = b[1] - p[1], cx = c[0] - p[0], cy = c[1] - p[1];
            return ((ax * ax + ay * ay) * (bx * cy - by * cx) - (bx * bx + by * by) * (ax * cy - ay * cx) + (cx * cx + cy * cy) * (ax * by - ay * bx)) > 0;
        }
        /// <summary>Bowyer-Watson Delaunay triangulation, triples wound counter-clockwise, in the Python's emission order.</summary>
        internal static List<int[]> Delaunay(List<double[]> pts)
        {
            double minx = pts.Min(q => q[0]), maxx = pts.Max(q => q[0]), miny = pts.Min(q => q[1]), maxy = pts.Max(q => q[1]);
            double cx = 0.5 * (minx + maxx), cy = 0.5 * (miny + maxy), r = 10 * Math.Max(maxx - minx, maxy - miny) + 1;
            var P = new List<double[]>(pts) { new[] { cx - r, cy - r }, new[] { cx + r, cy - r }, new[] { cx, cy + r } };
            int n = pts.Count;
            var tris = new List<int[]> { new[] { n, n + 1, n + 2 } };
            for (int i = 0; i < n; i++)
            {
                var bad = new List<int[]>(); var keep = new List<int[]>();
                foreach (var t in tris) (InCircum(P, t, P[i]) ? bad : keep).Add(t);
                var edgeOrder = new List<(int, int)>(); var edgeCount = new Dictionary<(int, int), int>();
                foreach (var t in bad) for (int k = 0; k < 3; k++)
                {
                    var e = (Math.Min(t[k], t[(k + 1) % 3]), Math.Max(t[k], t[(k + 1) % 3]));
                    if (!edgeCount.ContainsKey(e)) { edgeCount[e] = 0; edgeOrder.Add(e); }
                    edgeCount[e]++;
                }
                tris = keep;
                foreach (var (u, v) in edgeOrder)
                {
                    if (edgeCount[(u, v)] != 1) continue;
                    tris.Add(Area2(P[u], P[v], P[i]) > 0 ? new[] { u, v, i } : new[] { v, u, i });
                }
            }
            return tris.Where(t => t.Max() < n).ToList();
        }
        internal static bool PtInRing(double[] p, List<double[]> ring)
        {
            bool inside = false;
            for (int i = 0; i < ring.Count; i++)
            {
                double[] a = ring[i], b = ring[i == 0 ? ring.Count - 1 : i - 1];
                if ((a[1] > p[1]) != (b[1] > p[1]) && p[0] < a[0] + (p[1] - a[1]) / (b[1] - a[1]) * (b[0] - a[0])) inside = !inside;
            }
            return inside;
        }
    }
}
