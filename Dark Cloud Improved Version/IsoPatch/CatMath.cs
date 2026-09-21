using System;
using System.Collections.Generic;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The small linear algebra of the cat bake, operation for operation as the Python computed it (row-vector 4×4
    /// matrices M[r][c], v' = v · M; rotations as 3×3 row lists; quaternions scalar-first).</summary>
    internal static class CatMath
    {
        internal static double[] V(params double[] v) => v;
        internal static double[][] Rows(params double[][] r) => r;
        internal static double[][] Clone(double[][] m) => m.Select(r => (double[])r.Clone()).ToArray();

        /// <summary>a · b for row-vector 4×4s (extract_model.mat_mul).</summary>
        internal static double[][] MatMul(double[][] a, double[][] b)
        {
            var r = new double[4][];
            for (int i = 0; i < 4; i++) { r[i] = new double[4]; for (int j = 0; j < 4; j++) r[i][j] = PyMath.Sum(a[i][0] * b[0][j], a[i][1] * b[1][j], a[i][2] * b[2][j], a[i][3] * b[3][j]); }
            return r;
        }
        internal static double[][] MatFromRT(double[][] R, double[] T) => new[]
        {
            new[] { R[0][0], R[0][1], R[0][2], 0.0 }, new[] { R[1][0], R[1][1], R[1][2], 0.0 }, new[] { R[2][0], R[2][1], R[2][2], 0.0 }, new[] { T[0], T[1], T[2], 1.0 },
        };
        /// <summary>Inverse of a rigid row-vector 4×4.</summary>
        internal static double[][] RigidInv(double[][] m)
        {
            var Rt = new double[3][]; for (int i = 0; i < 3; i++) { Rt[i] = new double[3]; for (int j = 0; j < 3; j++) Rt[i][j] = m[j][i]; }
            double[] t = { m[3][0], m[3][1], m[3][2] };
            var nt = new double[3]; for (int j = 0; j < 3; j++) nt[j] = -(t[0] * Rt[0][j] + t[1] * Rt[1][j] + t[2] * Rt[2][j]);
            return MatFromRT(Rt, nt);
        }
        internal static double[] XformPt(double[][] m, double[] v) => new[]
        {
            v[0] * m[0][0] + v[1] * m[1][0] + v[2] * m[2][0] + m[3][0],
            v[0] * m[0][1] + v[1] * m[1][1] + v[2] * m[2][1] + m[3][1],
            v[0] * m[0][2] + v[1] * m[1][2] + v[2] * m[2][2] + m[3][2],
        };
        internal static double[][] RotOf(double[][] m) => new[] { new[] { m[0][0], m[0][1], m[0][2] }, new[] { m[1][0], m[1][1], m[1][2] }, new[] { m[2][0], m[2][1], m[2][2] } };
        internal static double[] TransOf(double[][] m) => new[] { m[3][0], m[3][1], m[3][2] };

        /// <summary>extract_model.mat_to_quat: Shepperd with the engine's sign convention, normalised.</summary>
        internal static double[] MatToQuat(double[][] R)
        {
            double m00 = R[0][0], m01 = R[0][1], m02 = R[0][2], m10 = R[1][0], m11 = R[1][1], m12 = R[1][2], m20 = R[2][0], m21 = R[2][1], m22 = R[2][2];
            double tr = m00 + m11 + m22, w, x, y, z, s;
            if (tr > 0) { s = Math.Sqrt(tr + 1.0) * 2; w = 0.25 * s; x = (m21 - m12) / s; y = (m02 - m20) / s; z = (m10 - m01) / s; }
            else if (m00 >= m11 && m00 >= m22) { s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2; w = (m21 - m12) / s; x = 0.25 * s; y = (m01 + m10) / s; z = (m02 + m20) / s; }
            else if (m11 >= m22) { s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2; w = (m02 - m20) / s; x = (m01 + m10) / s; y = 0.25 * s; z = (m12 + m21) / s; }
            else { s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2; w = (m10 - m01) / s; x = (m02 + m20) / s; y = (m12 + m21) / s; z = 0.25 * s; }
            double n = Math.Sqrt(w * w + x * x + y * y + z * z); if (n == 0) n = 1.0;
            return new[] { w / n, x / n, y / n, z / n };
        }
        internal static double[][] QuatToMat(double[] q)
        {
            double w = q[0], x = q[1], y = q[2], z = q[3];
            return new[]
            {
                new[] { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) },
                new[] { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) },
                new[] { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) },
            };
        }
        internal static double[][] Mul3(double[][] a, double[][] b)
        {
            var r = new double[3][];
            for (int i = 0; i < 3; i++) { r[i] = new double[3]; for (int c = 0; c < 3; c++) r[i][c] = PyMath.Sum(a[i][0] * b[0][c], a[i][1] * b[1][c], a[i][2] * b[2][c]); }
            return r;
        }
        internal static double[][] T3(double[][] a) { var r = new double[3][]; for (int i = 0; i < 3; i++) { r[i] = new double[3]; for (int c = 0; c < 3; c++) r[i][c] = a[c][i]; } return r; }
        internal static double[] Pitch(double[] v, double p) { double c = Math.Cos(p), s = Math.Sin(p); return new[] { v[0], v[1] * c + v[2] * s, -v[1] * s + v[2] * c }; }
        internal static double[] RotY(double[] v, double yaw) { double c = Math.Cos(yaw), s = Math.Sin(yaw); return new[] { v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c }; }
        internal static double Sum3(double[] a, double[] b) => PyMath.Sum(a[0] * b[0], a[1] * b[1], a[2] * b[2]);   // sum(a[i]*b[i] for i in range(3))
        internal static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        internal static double[] Unit(double[] v) { double n = Math.Sqrt(PyMath.Sum(v.Select(c => c * c))); if (n == 0) n = 1.0; return v.Select(c => c / n).ToArray(); }
        internal static double Smooth(double t) { t = Math.Min(Math.Max(t, 0.0), 1.0); return t * t * (3 - 2 * t); }
        internal static double Radians(double deg) => deg * (Math.PI / 180.0);

        internal static double[] Slerp(double[] q0, double[] q1, double t)
        {
            double d = PyMath.Sum(Enumerable.Range(0, 4).Select(i => q0[i] * q1[i]));
            if (d < 0) { q1 = q1.Select(x => -x).ToArray(); d = -d; }
            double[] r;
            if (d > 0.9995) r = Enumerable.Range(0, 4).Select(i => q0[i] + (q1[i] - q0[i]) * t).ToArray();
            else
            {
                double th = Math.Acos(Math.Max(-1.0, Math.Min(1.0, d))), s0 = Math.Sin((1 - t) * th) / Math.Sin(th), s1 = Math.Sin(t * th) / Math.Sin(th);
                r = Enumerable.Range(0, 4).Select(i => q0[i] * s0 + q1[i] * s1).ToArray();
            }
            double n = Math.Sqrt(PyMath.Sum(r.Select(x => x * x))); if (n == 0) n = 1.0;
            return r.Select(x => x / n).ToArray();
        }

        internal sealed class Track { internal int Node, Chan; internal List<double> Frames = new(); internal List<double[]> Vals = new(); }

        /// <summary>A track at fractional frame f: the viewer's clamp + slerp/lerp.</summary>
        internal static double[] Sample(Track t, double f)
        {
            var F = t.Frames; var Vv = t.Vals;
            if (f <= F[0]) return (double[])Vv[0].Clone();
            if (f >= F[^1]) return (double[])Vv[^1].Clone();
            int hi = F.FindIndex(x => x > f), lo = hi - 1;
            double u = (f - F[lo]) / (F[hi] - F[lo]);
            if (t.Chan == 0) return Slerp(Vv[lo], Vv[hi], u);
            return Enumerable.Range(0, Vv[lo].Length).Select(i => Vv[lo][i] + (Vv[hi][i] - Vv[lo][i]) * u).ToArray();
        }

        /// <summary>Gaussian elimination with partial pivoting; null if singular.</summary>
        internal static double[] Solve(double[][] A, double[] b)
        {
            int n = b.Length;
            var M = new double[n][]; for (int i = 0; i < n; i++) { M[i] = new double[n + 1]; for (int j = 0; j < n; j++) M[i][j] = A[i][j]; M[i][n] = b[i]; }
            for (int c = 0; c < n; c++)
            {
                int p = c; for (int r = c; r < n; r++) if (Math.Abs(M[r][c]) > Math.Abs(M[p][c])) p = r;
                if (Math.Abs(M[p][c]) < 1e-15) return null;
                (M[c], M[p]) = (M[p], M[c]);
                for (int r = 0; r < n; r++)
                {
                    if (r == c) continue;
                    double f = M[r][c] / M[c][c];
                    if (f != 0) { var row = new double[n + 1]; for (int k = 0; k <= n; k++) row[k] = M[r][k] - f * M[c][k]; M[r] = row; }
                }
            }
            var x = new double[n]; for (int i = 0; i < n; i++) x[i] = M[i][n] / M[i][i];
            return x;
        }

        /// <summary>Inverse of a 4×4; null if singular.</summary>
        internal static double[][] Inv4(double[][] M)
        {
            const int n = 4;
            var A = new double[n][]; for (int i = 0; i < n; i++) { A[i] = new double[2 * n]; for (int j = 0; j < n; j++) A[i][j] = M[i][j]; A[i][n + i] = 1.0; }
            for (int c = 0; c < n; c++)
            {
                int p = c; for (int r = c; r < n; r++) if (Math.Abs(A[r][c]) > Math.Abs(A[p][c])) p = r;
                if (Math.Abs(A[p][c]) < 1e-15) return null;
                (A[c], A[p]) = (A[p], A[c]);
                double f = A[c][c]; A[c] = A[c].Select(a => a / f).ToArray();
                for (int r = 0; r < n; r++)
                    if (r != c && A[r][c] != 0) { double g = A[r][c]; var row = new double[2 * n]; for (int k = 0; k < 2 * n; k++) row[k] = A[r][k] - g * A[c][k]; A[r] = row; }
            }
            return A.Select(row => row.Skip(n).ToArray()).ToArray();
        }

        /// <summary>A bone's posed world in column form: (R with world = R·p + t, t).</summary>
        internal static (double[][] R, double[] t) Col(double[][] M)
        {
            var R = new double[3][]; for (int i = 0; i < 3; i++) { R[i] = new double[3]; for (int j = 0; j < 3; j++) R[i][j] = M[j][i]; }
            return (R, new[] { M[3][0], M[3][1], M[3][2] });
        }

        /// <summary>Linear-blend position of a two-bone vertex.</summary>
        internal static double[] Blend(double w, (double[][] R, double[] t) M0, (double[][] R, double[] t) M1, double[] p0, double[] p1)
        {
            var r = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double a = PyMath.Sum(M0.R[i][0] * p0[0], M0.R[i][1] * p0[1], M0.R[i][2] * p0[2]);
                double b = PyMath.Sum(M1.R[i][0] * p1[0], M1.R[i][1] * p1[1], M1.R[i][2] * p1[2]);
                r[i] = w * (a + M0.t[i]) + (1 - w) * (b + M1.t[i]);
            }
            return r;
        }

        /// <summary>Bone-local positions (p0, p1) of a two-bone vertex that land on tgtA under pose A and tgtB under pose B: a 6×6
        /// minimum-norm least-squares solve; null if it explodes.</summary>
        internal static (double[] p0, double[] p1)? TwoPoseFit(double w, ((double[][] R, double[] t) a, (double[][] R, double[] t) b) MA,
                                                                ((double[][] R, double[] t) a, (double[][] R, double[] t) b) MB, double[] tgtA, double[] tgtB, double guard = 50.0)
        {
            var A = new List<double[]>(); var rhs = new List<double>();
            foreach (var (R0, t0, R1, t1, tgt) in new[] { (MA.a.R, MA.a.t, MA.b.R, MA.b.t, tgtA), (MB.a.R, MB.a.t, MB.b.R, MB.b.t, tgtB) })
                for (int i = 0; i < 3; i++)
                {
                    var row = new double[6]; for (int j = 0; j < 3; j++) { row[j] = w * R0[i][j]; row[3 + j] = (1 - w) * R1[i][j]; }
                    A.Add(row); rhs.Add(tgt[i] - w * t0[i] - (1 - w) * t1[i]);
                }
            var N = new double[6][]; var g = new double[6];
            for (int i = 0; i < 6; i++) { N[i] = new double[6]; for (int j = 0; j < 6; j++) N[i][j] = PyMath.Sum(Enumerable.Range(0, 6).Select(k => A[k][i] * A[k][j])); g[i] = PyMath.Sum(Enumerable.Range(0, 6).Select(k => A[k][i] * rhs[k])); }
            double mx = double.NegativeInfinity; for (int i = 0; i < 6; i++) mx = Math.Max(mx, N[i][i]);
            double lam = 1e-8 * (mx + 1e-12);
            for (int i = 0; i < 6; i++) N[i][i] += lam;
            var x = Solve(N, g);
            if (x == null || x.Any(v => !double.IsFinite(v)) || x.Max(v => Math.Abs(v)) >= guard) return null;
            return (new[] { x[0], x[1], x[2] }, new[] { x[3], x[4], x[5] });
        }

        /// <summary>The least principal axis of a point cloud (cyclic Jacobi on the 3×3 scatter matrix).</summary>
        internal static double[] PcaNormal(List<double[]> pts, double[] c)
        {
            var S = new double[3][]; for (int i = 0; i < 3; i++) { S[i] = new double[3]; for (int j = 0; j < 3; j++) S[i][j] = PyMath.Sum(pts.Select(p => (p[i] - c[i]) * (p[j] - c[j]))); }
            var Vm = new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 } };
            for (int it = 0; it < 60; it++)
            {
                var offs = new List<double>(); for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) if (i != j) offs.Add(Math.Pow(S[i][j], 2));
                double off = PyMath.Sum(offs);
                if (off < 1e-18) break;
                for (int p = 0; p < 3; p++) for (int q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(S[p][q]) < 1e-30) continue;
                    double th = 0.5 * Math.Atan2(2 * S[p][q], S[q][q] - S[p][p]), cs = Math.Cos(th), sn = Math.Sin(th);
                    for (int k = 0; k < 3; k++) { double skp = S[k][p], skq = S[k][q]; S[k][p] = cs * skp - sn * skq; S[k][q] = sn * skp + cs * skq; }
                    for (int k = 0; k < 3; k++) { double spk = S[p][k], sqk = S[q][k]; S[p][k] = cs * spk - sn * sqk; S[q][k] = sn * spk + cs * sqk; }
                    for (int k = 0; k < 3; k++) { double vkp = Vm[k][p], vkq = Vm[k][q]; Vm[k][p] = cs * vkp - sn * vkq; Vm[k][q] = sn * vkp + cs * vkq; }
                }
            }
            int m = 0; for (int i = 1; i < 3; i++) if (S[i][i] < S[m][m]) m = i;
            var v = new[] { Vm[0][m], Vm[1][m], Vm[2][m] };
            double nn = Math.Sqrt(PyMath.Sum(v[0] * v[0], v[1] * v[1], v[2] * v[2])); if (nn == 0) nn = 1.0;
            return v.Select(a => a / nn).ToArray();
        }

        /// <summary>Row-vector rotation of +deg about the unit axis (Rodrigues, transposed).</summary>
        internal static double[][] RotAbout(double[] axis, double deg)
        {
            var u = Unit(axis); double x = u[0], y = u[1], z = u[2], c = Math.Cos(Radians(deg)), sn = Math.Sin(Radians(deg)), t = 1 - c;
            var R = new[]
            {
                new[] { t * x * x + c, t * x * y - sn * z, t * x * z + sn * y },
                new[] { t * x * y + sn * z, t * y * y + c, t * y * z - sn * x },
                new[] { t * x * z - sn * y, t * y * z + sn * x, t * z * z + c },
            };
            return T3(R);
        }

        /// <summary>(frame, value) keys → the value at f: held outside, smoothstepped between.</summary>
        internal static double KeyedVal(IList<(double f, double v)> keys, double f)
        {
            double v = f <= keys[0].f ? keys[0].v : keys[^1].v;
            for (int i = 0; i + 1 < keys.Count; i++) if (keys[i].f <= f && f <= keys[i + 1].f) v = keys[i].v + (keys[i + 1].v - keys[i].v) * Smooth((f - keys[i].f) / (keys[i + 1].f - keys[i].f));
            return v;
        }
    }
}
