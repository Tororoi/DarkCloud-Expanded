using System;
using System.Collections.Generic;
using System.Linq;
using static Dark_Cloud_Improved_Version.CollisionGeom;
using static Dark_Cloud_Improved_Version.TownCollisionData;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The hand-authored / directed Queens (e03) collision geometry over the generated tables: the both-frame and
    /// player-only walls, the canal containment, the perimeter, the terrain removal rules, the camera winding fixes, the
    /// directed camera simplification jobs and the bridge-gate torch simplification.</summary>
    internal static class QueensTerrainRules
    {
        private static IEnumerable<double[][]> WallTris(double[,] walls)
        {
            for (int i = 0; i < walls.GetLength(0); i++)
            {
                double[] a0 = { walls[i, 0], walls[i, 1], walls[i, 2] }, b0 = { walls[i, 3], walls[i, 4], walls[i, 5] };
                double h = walls[i, 6];
                double[] a1 = { a0[0], a0[1] + h, a0[2] }, b1 = { b0[0], b0[1] + h, b0[2] };
                yield return new[] { a0, b0, b1 };
                yield return new[] { (double[])a0.Clone(), (double[])b1.Clone(), a1 };
            }
        }

        internal static List<double[][]> CameraTris() => TrisFrom(QueensCameraTris);

        internal static List<double[][]> FlatGroundTris()
        {
            var outp = new List<double[][]>();
            for (int i = 0; i < QueensFlatRegions.GetLength(0); i++)
            {
                double x0 = QueensFlatRegions[i, 0], x1 = QueensFlatRegions[i, 1], z0 = QueensFlatRegions[i, 2], z1 = QueensFlatRegions[i, 3], y = QueensFlatRegions[i, 4];
                outp.Add(Tri(x0, y, z0, x1, y, z0, x1, y, z1));
                outp.Add(Tri(x0, y, z0, x1, y, z1, x0, y, z1));
            }
            return outp;
        }

        /// <summary>Each canal pipe stub as its bounding box: 4 side faces + the free-end cap (the embedded |z|=50 end open).</summary>
        internal static List<double[][]> PipeDrumTris()
        {
            var outp = new List<double[][]>();
            for (int i = 0; i < QueensPipeDrums.GetLength(0); i++)
            {
                double cx = QueensPipeDrums[i, 0], cy = QueensPipeDrums[i, 1], z0 = QueensPipeDrums[i, 2], z1 = QueensPipeDrums[i, 3], R = QueensPipeDrums[i, 4];
                double x0 = cx - R, x1 = cx + R, y0 = cy - R, y1 = cy + R;
                bool emb = Math.Abs(z0) > Math.Abs(z1);
                double zf = emb ? z1 : z0;
                outp.AddRange(DirQuad(V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1), V(x1, y0, z1), V(1, 0, 0)));
                outp.AddRange(DirQuad(V(x0, y0, z0), V(x0, y1, z0), V(x0, y1, z1), V(x0, y0, z1), V(-1, 0, 0)));
                outp.AddRange(DirQuad(V(x0, y1, z0), V(x1, y1, z0), V(x1, y1, z1), V(x0, y1, z1), V(0, 1, 0)));
                outp.AddRange(DirQuad(V(x0, y0, z0), V(x1, y0, z0), V(x1, y0, z1), V(x0, y0, z1), V(0, -1, 0)));
                outp.AddRange(DirQuad(V(x0, y0, zf), V(x1, y0, zf), V(x1, y1, zf), V(x0, y1, zf), V(0, 0, emb ? 1 : -1)));
            }
            return outp;
        }

        internal static List<double[][]> BothWallTris()
        {
            var outp = WallTris(QueensBothWalls).ToList();
            outp.AddRange(TrisFrom(QueensManualTris));
            outp.AddRange(FlatGroundTris());
            outp.AddRange(PipeDrumTris());
            return outp;
        }

        internal static List<double[][]> PlayerWallTris() => WallTris(QueensPlayerWalls).ToList();

        /// <summary>Canal containment, every wall's top pulled down to ground(xz) + maxHeight (the feet-level check still hits
        /// it, the higher camera clears it).</summary>
        internal static List<double[][]> InvisibleTris(double maxHeight = 5.0)
        {
            var tris = TrisFrom(QueensCanalContainmentTris);
            string K(double[] p) => $"{PyMath.Round(p[0], 1) + 0.0:R},{PyMath.Round(p[2], 1) + 0.0:R}";
            var ground = new Dictionary<string, double>();
            foreach (var t in tris) foreach (var p in t) { string k = K(p); ground[k] = ground.TryGetValue(k, out double g) ? Math.Min(g, p[1]) : p[1]; }
            return tris.Select(t => t.Select(p => new[] { p[0], Math.Min(p[1], ground[K(p)] + maxHeight), p[2] }).ToArray()).ToList();
        }

        /// <summary>Straight wall quads between the frozen corner points spanning [yBottom, yTop] plus the extra railings; walls
        /// made redundant by an inner wall dropped.</summary>
        internal static List<double[][]> PerimeterWallTris(double yBottom = 170.0, double yTop = 500.0)
        {
            int n = QueensPerimeterCorners.GetLength(0);
            var walls = new List<double[][]>();
            for (int i = 0; i < n; i++)
            {
                double ax = QueensPerimeterCorners[i, 0], az = QueensPerimeterCorners[i, 1], bx = QueensPerimeterCorners[(i + 1) % n, 0], bz = QueensPerimeterCorners[(i + 1) % n, 1];
                walls.Add(Tri(ax, yTop, az, bx, yTop, bz, bx, yBottom, bz));
                walls.Add(Tri(ax, yTop, az, bx, yBottom, bz, ax, yBottom, az));
            }
            var rem = new HashSet<string>(TrisFrom(QueensPerimeterPreRemoveTris).Select(TriKeyInt));
            var outp = walls.Where(w => !rem.Contains(TriKeyInt(w))).ToList();
            outp.AddRange(WallTris(QueensPerimeterExtraWalls));
            return outp;
        }

        private static bool InRemoveRegion(double[][] t) =>
            PlaneZ(t, 1300, -400, 600, 0, 270) || PlaneX(t, 600, 200, 1300, 0, 272) || PlaneX(t, 600, 200, 1250, 262, 376)
            || Box(t, -500, -400, 70, 170, -900, -200) || PlaneX(t, -400, -1000, -150, 70, 276) || PlaneX(t, 900, -1000, -150, 70, 276)
            || PlaneZ(t, -1000, -400, 900, 70, 276)
            || (Box(t, -500, 1000, 260, 278, -1100, -1000) && Horiz(t)) || (Box(t, -500, -350, 260, 278, -1000, -150) && Horiz(t))
            || (Box(t, 850, 1000, 260, 278, -1000, -150) && Horiz(t))
            || PlaneX(t, 1500, 250, 1300, 170, 370) || PlaneZ(t, 1300, 650, 1500, 170, 370) || Box(t, 650, 1600, 370, 370, 100, 1400)
            || Box(t, 1400, 1600, 64, 89, -50, 50) || PlaneX(t, 1400, -50, 50, 89, 170);

        private static readonly HashSet<string> Residue = new(TrisFrom(QueensRemoveResidueTris).Select(TriKeyInt));
        private static readonly HashSet<string> BackfaceKeys = new(TrisFrom(QueensBackfaceTris).Select(TriKeyWinding));

        /// <summary>Reverse the winding of any tri whose winding matches a known-backwards camera wall.</summary>
        internal static List<double[][]> FixCameraWinding(IEnumerable<double[][]> tris)
            => tris.Select(t => BackfaceKeys.Contains(TriKeyWinding(t)) ? new[] { t[0], t[2], t[1] } : t).ToList();

        private static bool InFlatRegion(double[][] t)
        {
            for (int i = 0; i < QueensFlatRegions.GetLength(0); i++)
            {
                double x0 = QueensFlatRegions[i, 0], x1 = QueensFlatRegions[i, 1], z0 = QueensFlatRegions[i, 2], z1 = QueensFlatRegions[i, 3], y = QueensFlatRegions[i, 4];
                if (t.All(p => x0 - 1 <= p[0] && p[0] <= x1 + 1 && z0 - 1 <= p[2] && p[2] <= z1 + 1 && Math.Abs(p[1] - y) <= 2)) return true;
            }
            return false;
        }

        private static bool CanalWall(double[][] t)
            => (t.All(p => Math.Abs(p[2] + 50) < 1) || t.All(p => Math.Abs(p[2] - 50) < 1)) && t.All(p => -1 <= p[1] && p[1] <= 71 && -201 <= p[0] && p[0] <= 901);

        /// <summary>The shared terrain simplification: drop everything at/west of x=−500 and fully east of x=1600, the removal
        /// regions + residue, flat ground replaced by quads, and the x[−200,900] canal side walls.</summary>
        internal static List<double[][]> SimplifyTerrain(List<double[][]> tris)
            => tris.Where(t => t.Max(p => p[0]) > -499.0 && t.Min(p => p[0]) < 1600.0 && !InRemoveRegion(t) && !Residue.Contains(TriKeyInt(t)) && !InFlatRegion(t) && !CanalWall(t)).ToList();

        // ───────────────────────── directed camera simplification ─────────────────────────
        private static readonly HashSet<string> TownWallKeys = new(TrisFrom(QueensTownWallTris).Select(TriKeyInt));
        private static readonly HashSet<string> ArcadeBackKeys = new(TrisFrom(QueensArcadeBackTris).Select(TriKeyInt));
        private static readonly HashSet<string> Z200NotchKeys = new(TrisFrom(QueensZ200NotchTris).Select(TriKeyInt));
        private const double CanalWallTopY = 280.0;

        /// <summary>The major town walls: vertical faces through the hole-preserving coplanar merge (ramparts forced to one height),
        /// the x=−400 arcade's non-walkable sections filled solid, the three walkway tops, the z=200 staircase walls flattened,
        /// the x[1400,1500] sections capped at the obj33 ledge, the x=695 return moved flush to x=700, the west corner closed.</summary>
        private static List<double[][]> TownWallTris()
        {
            var allt = TrisFrom(QueensTownWallTris);
            var vert = allt.Where(t => { var n = TriangleNormal(t); double L = Math.Sqrt(Dot3(n, n)); if (L == 0) L = 1.0; return Math.Abs(n[1]) / L < 0.5; }).ToList();
            var x400Solid = new[] { (200.0, 500.0, 280.0), (600.0, 1300.0, 270.0) };
            bool InSolid(double[][] t)
            {
                if (!t.All(p => Math.Abs(p[0] + 400) < 1)) return false;
                double cz = (t[0][2] + t[1][2] + t[2][2]) / 3;
                return x400Solid.Any(s => s.Item1 - 5 <= cz && cz <= s.Item2 + 5);
            }
            var forced = new (double h, Func<double[][], bool> pred)[]
            {
                (280.0, t => t.All(p => Math.Abs(p[2] - 150) < 1) || t.All(p => Math.Abs(p[0] - 695) < 1)),
                (380.0, t => t.All(p => Math.Abs(p[2] - 250) < 1)),
            };
            var outp = new List<double[][]>();
            var done = new HashSet<double[][]>(ReferenceEqualityComparer.Instance);
            foreach (var (h, pred) in forced)
            {
                var grp = vert.Where(t => pred(t) && !InSolid(t)).ToList();
                foreach (var t in grp) done.Add(t);
                outp.AddRange(SimplifyCoplanar(grp, snap: 10.0, keepWindows: true, top: h));
            }
            var rest = vert.Where(t => !done.Contains(t) && !InSolid(t)).ToList();
            outp.AddRange(SimplifyCoplanar(rest, snap: 10.0, keepWindows: true));
            foreach (var (z0, z1, ty) in x400Solid) outp.AddRange(DirQuad(V(-400, 0, z0), V(-400, 0, z1), V(-400, ty, z1), V(-400, ty, z0), V(1, 0, 0)));
            outp.AddRange(DirQuad(V(-400, 280, 150), V(700, 280, 150), V(700, 280, 200), V(-400, 280, 200), V(0, 1, 0)));
            outp.AddRange(DirQuad(V(600, 380, 200), V(1350, 380, 200), V(1350, 380, 250), V(600, 380, 250), V(0, 1, 0)));
            outp.AddRange(DirQuad(V(600, 380, 200), V(650, 380, 200), V(650, 380, 1300), V(600, 380, 1300), V(0, 1, 0)));
            outp = outp.Where(t => !(t.All(p => Math.Abs(p[2] - 200) < 1 && -1 <= p[0] && p[0] <= 601 && -1 <= p[1] && p[1] <= 281) && TriangleNormal(t)[2] > 0)).ToList();
            outp.AddRange(DirQuad(V(0, 0, 200), V(600, 0, 200), V(600, 280, 200), V(0, 280, 200), V(0, 0, 1)));
            outp = outp.Where(t => !(t.All(p => Math.Abs(p[2] - 200) < 1 && 599 <= p[0] && p[0] <= 1301 && 69 <= p[1] && p[1] <= 381) && TriangleNormal(t)[2] < 0)).ToList();
            outp.AddRange(DirQuad(V(600, 70, 200), V(1300, 70, 200), V(1300, 380, 200), V(600, 380, 200), V(0, 0, -1)));
            bool X1400Sec(double[][] t, double zplane, int nsign) => t.All(p => 1399 <= p[0] && p[0] <= 1501 && 169 <= p[1] && p[1] <= 381 && Math.Abs(p[2] - zplane) < 1) && TriangleNormal(t)[2] * nsign > 0;
            outp = outp.Where(t => !(X1400Sec(t, 250, +1) || X1400Sec(t, 200, -1))).ToList();
            outp.AddRange(DirQuad(V(1400, 170, 250), V(1500, 170, 250), V(1500, 370, 250), V(1400, 370, 250), V(0, 0, 1)));
            outp.AddRange(DirQuad(V(1400, 170, 200), V(1500, 170, 200), V(1500, 370, 200), V(1400, 370, 200), V(0, 0, -1)));
            outp.AddRange(DirQuad(V(1400, 370, 200), V(1500, 370, 200), V(1500, 370, 250), V(1400, 370, 250), V(0, 1, 0)));
            outp = outp.Where(t => !t.All(p => Math.Abs(p[0] - 695) < 1 && 149 <= p[2] && p[2] <= 201 && 69 <= p[1] && p[1] <= 281)).ToList();
            outp.AddRange(DirQuad(V(700, 70, 150), V(700, 70, 200), V(700, 280, 200), V(700, 280, 150), V(1, 0, 0)));
            outp.AddRange(DirQuad(V(-400, 70, 50), V(-400, 70, 150), V(-400, 270, 150), V(-400, 270, 50), V(1, 0, 0)));
            return outp;
        }

        private static bool NorthFace(double[][] t)
            => t.All(p => Math.Abs(p[2] + 100) < 2) && t.Min(p => p[1]) >= 55 && t.Max(p => p[1]) <= 290 && -450 <= (t[0][0] + t[1][0] + t[2][0]) / 3.0 && (t[0][0] + t[1][0] + t[2][0]) / 3.0 <= 1550;

        private static bool WallCap(double[][] t)
        {
            double zmax = t.Max(p => p[2]), zmin = t.Min(p => p[2]);
            return zmax - zmin > 40 && t.All(p => -152 <= p[2] && p[2] <= -98) && t.All(p => -401 <= p[0] && p[0] <= 1501) && t.Min(p => p[1]) >= 258 && TriangleNormal(t)[1] > 0;
        }

        private sealed class Job { internal string Kind; internal Func<double[][], bool> Sel; internal double Snap, Outward; internal double? Top; internal Func<List<double[][]>> Tris; }
        private static readonly Job[] CamMergeJobs =
        {
            new Job { Kind = "merge", Sel = PlaneRegion(2, -150, x: (-400, 900), y: (55, 290)), Snap = 5.0, Outward = 0.0, Top = CanalWallTopY },
            new Job { Kind = "replace", Sel = NorthFace, Tris = () => Quad(V(-400, 70, -100), V(200, 70, -100), V(200, CanalWallTopY, -100), V(-400, CanalWallTopY, -100))
                                                                 .Concat(Quad(V(300, 70, -100), V(1500, 70, -100), V(1500, CanalWallTopY, -100), V(300, CanalWallTopY, -100))).ToList() },
            new Job { Kind = "replace", Sel = WallCap, Tris = () => Quad(V(-400, CanalWallTopY, -100), V(1500, CanalWallTopY, -100), V(1500, CanalWallTopY, -150), V(-400, CanalWallTopY, -150)) },
            new Job { Kind = "replace", Sel = t => TownWallKeys.Contains(TriKeyInt(t)), Tris = TownWallTris },
            new Job { Kind = "replace", Sel = t => ArcadeBackKeys.Contains(TriKeyInt(t)), Tris = () => new List<double[][]>() },
            new Job { Kind = "replace", Sel = t => Z200NotchKeys.Contains(TriKeyInt(t)), Tris = () => new List<double[][]>() },
        };

        /// <summary>Apply each directed job to its selected tris (merge → coplanar merge authored outward; replace → authored quads),
        /// everything else through at full detail.</summary>
        internal static List<double[][]> CamMergeSelected(List<double[][]> tris)
        {
            var remaining = new List<double[][]>(tris); var outp = new List<double[][]>();
            foreach (var job in CamMergeJobs)
            {
                var picked = remaining.Where(job.Sel).ToList();
                remaining = remaining.Where(t => !job.Sel(t)).ToList();
                if (picked.Count == 0) continue;
                if (job.Kind == "replace") outp.AddRange(job.Tris());
                else outp.AddRange(SimplifyCoplanar(picked, snap: job.Snap, outward: job.Outward, top: job.Top));
            }
            outp.AddRange(remaining);
            return outp;
        }

        // ───────────────────────── bridge gate torches ─────────────────────────
        private static string GateKey(double[][] t, double dx = 0.0)
        {
            var v = t.Select(p => new[] { PyMath.Round(p[0] + dx, 1) + 0.0, PyMath.Round(p[1], 1) + 0.0, PyMath.Round(p[2], 1) + 0.0 }).ToList();
            v.Sort((a, b) => { for (int i = 0; i < 3; i++) { int c = a[i].CompareTo(b[i]); if (c != 0) return c; } return 0; });
            return string.Join("|", v.Select(p => $"{p[0]:R},{p[1]:R},{p[2]:R}"));
        }

        /// <summary>obj40/obj44 (identical bridge-gate meshes): each corner torch-post (y ≥ 77) replaced by a full-height corner
        /// column (top + 4 sides down to y=0), the inner side-railings and obsolete filler walls dropped, the walkway top raised +8,
        /// the outer side-face's top edge raised +8, the barrel-vault roof merged to 4 quads, the z-end middle gaps closed.</summary>
        internal static List<double[][]> GateTorchSimplify(List<double[][]> tris)
        {
            double xmin = tris.SelectMany(t => t).Min(p => p[0]), xmax = tris.SelectMany(t => t).Max(p => p[0]);
            double zmin = tris.SelectMany(t => t).Min(p => p[2]), zmax = tris.SelectMany(t => t).Max(p => p[2]);
            double dx = PyMath.Round(xmin - GateXMinRef, 0);
            var dropKeys = new HashSet<string>(TrisFrom(GateRailingTris).Concat(TrisFrom(GateOuterFillerTris)).Select(rt => GateKey(rt, dx)));
            var raiseKeys = new HashSet<string>(TrisFrom(GateWalkwayRaiseTris).Select(rt => GateKey(rt, dx)));
            var extendKeys = new HashSet<string>(TrisFrom(GateOuterFaceExtendTris).Select(rt => GateKey(rt, dx)));
            const double Y0 = 77.25, XW = 7.0, ZW = 8.0, Dy = 8.0;
            var regions = new List<(double x0, double x1, double z0, double z1)>();
            foreach (int sx in new[] { 0, 1 }) foreach (int sz in new[] { 0, 1 })
            {
                var rx = sx == 0 ? (xmin - 0.5, xmin + XW) : (xmax - XW, xmax + 0.5);
                var rz = sz == 0 ? (zmin - 0.5, zmin + ZW) : (zmax - ZW, zmax + 0.5);
                regions.Add((rx.Item1, rx.Item2, rz.Item1, rz.Item2));
            }
            var keep = new List<double[][]>(); var cap = regions.Select(_ => new List<double[][]>()).ToList();
            foreach (var t in tris)
            {
                string k = GateKey(t);
                if (dropKeys.Contains(k)) continue;
                if (raiseKeys.Contains(k)) { keep.Add(t.Select(p => new[] { p[0], p[1] + Dy, p[2] }).ToArray()); continue; }
                if (extendKeys.Contains(k)) { keep.Add(t.Select(p => new[] { p[0], p[1] + (p[1] >= 65 ? Dy : 0.0), p[2] }).ToArray()); continue; }
                double[] c = { (t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3, (t[0][2] + t[1][2] + t[2][2]) / 3 };
                int r = -1;
                if (c[1] >= Y0)
                    for (int ri = 0; ri < regions.Count; ri++)
                    {
                        var (x0, x1, z0, z1) = regions[ri];
                        if (x0 <= c[0] && c[0] <= x1 && z0 <= c[2] && c[2] <= z1) { r = ri; break; }
                    }
                (r < 0 ? keep : cap[r]).Add(t);
            }
            var outp = new List<double[][]>(keep);
            for (int ri = 0; ri < regions.Count; ri++)
            {
                var g = cap[ri];
                if (g.Count == 0) continue;
                double X0 = g.SelectMany(t => t).Min(p => p[0]), X1 = g.SelectMany(t => t).Max(p => p[0]), YT = g.SelectMany(t => t).Max(p => p[1]);
                double Z0 = g.SelectMany(t => t).Min(p => p[2]), Z1 = g.SelectMany(t => t).Max(p => p[2]), YB = 0.0;
                outp.AddRange(DirQuad(V(X0, YT, Z0), V(X1, YT, Z0), V(X1, YT, Z1), V(X0, YT, Z1), V(0, 1, 0)));
                outp.AddRange(DirQuad(V(X0, YB, Z0), V(X0, YB, Z1), V(X0, YT, Z1), V(X0, YT, Z0), V(-1, 0, 0)));
                outp.AddRange(DirQuad(V(X1, YB, Z0), V(X1, YB, Z1), V(X1, YT, Z1), V(X1, YT, Z0), V(1, 0, 0)));
                outp.AddRange(DirQuad(V(X0, YB, Z0), V(X1, YB, Z0), V(X1, YT, Z0), V(X0, YT, Z0), V(0, 0, -1)));
                outp.AddRange(DirQuad(V(X0, YB, Z1), V(X1, YB, Z1), V(X1, YT, Z1), V(X0, YT, Z1), V(0, 0, 1)));
            }
            var roofKeys = new HashSet<string>(TrisFrom(GateRoof24Tris).Select(rt => GateKey(rt, dx)));
            outp = outp.Where(t => !roofKeys.Contains(GateKey(t))).ToList();
            for (int q = 0; q < GateRoof8Quads.GetLength(0); q++)
            {
                double[] P(int k) => new[] { GateRoof8Quads[q, k * 3] + dx, GateRoof8Quads[q, k * 3 + 1], GateRoof8Quads[q, k * 3 + 2] };
                outp.AddRange(DirQuad(P(0), P(1), P(2), P(3), V(0, 1, 0)));
            }
            foreach (var (zc, wn) in new[] { (-50.0, V(0, 0, -1)), (50.0, V(0, 0, 1)) })
                outp.AddRange(DirQuad(V(-68.0 + dx, 70.0, zc), V(-28.0 + dx, 70.0, zc), V(-28.0 + dx, 78.0, zc), V(-68.0 + dx, 78.0, zc), wn));
            return outp;
        }
    }
}
