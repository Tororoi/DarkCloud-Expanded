using System;
using System.Collections.Generic;
using System.Numerics;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The few Python float semantics the ported bakes depend on for byte-identical output: exact decimal rounding
    /// (round-half-even on the double's EXACT value, as CPython's round does), float floor division, and str.splitlines.</summary>
    internal static class PyMath
    {
        /// <summary>Python's round(x, ndigits): the exact binary value rounded half-to-even at that decimal, returned as the
        /// nearest double to the decimal result.</summary>
        internal static double Round(double x, int ndigits)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0) return x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long mant = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else mant |= 1L << 52;
            exp -= 1075;                                                         // x = mant × 2^exp
            BigInteger num = mant, den = BigInteger.One;
            if (exp > 0) num <<= exp; else den <<= -exp;
            BigInteger scale = BigInteger.Pow(10, Math.Abs(ndigits));
            if (ndigits >= 0) num *= scale; else den *= scale;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            BigInteger twice = r * 2;
            if (twice > den || (twice == den && !q.IsEven)) q += 1;
            double result = ndigits >= 0 ? (double)q / (double)scale : (double)q * (double)scale;
            if ((double)q != 0 && ndigits >= 0 && BigInteger.Abs(q) < (BigInteger.One << 53)) result = (double)(long)q / (double)scale;
            return neg ? -result : result;
        }

        /// <summary>Python's a // b for floats (CPython float_floor_div: fmod-based, sign-corrected, floored).</summary>
        internal static double FloorDiv(double a, double b)
        {
            double mod = Math.IEEERemainder(0, 1); // placeholder to keep the shape simple below
            mod = a % b;                                                          // C fmod
            double div = (a - mod) / b;
            if (mod != 0) { if ((b < 0) != (mod < 0)) { mod += b; div -= 1.0; } }
            double floordiv;
            if (div != 0) { floordiv = Math.Floor(div); if (div - floordiv > 0.5) floordiv += 1.0; }
            else floordiv = Math.CopySign(0.0, a / b);
            return floordiv;
        }

        /// <summary>Python's str.splitlines on latin1-decoded text (\n, \r, \r\n, \v, \f, \x1c, \x1d, \x1e, \x85).</summary>
        internal static List<string> SplitLines(string s)
        {
            var outp = new List<string>(); int i = 0, n = s.Length;
            while (i < n)
            {
                int j = i;
                while (j < n && !IsBreak(s[j])) j++;
                outp.Add(s.Substring(i, j - i));
                if (j >= n) break;
                if (s[j] == '\r' && j + 1 < n && s[j + 1] == '\n') j++;
                i = j + 1;
            }
            return outp;
        }
        private static bool IsBreak(char c) => c == '\n' || c == '\r' || c == '\v' || c == '\f' || c == '\x1c' || c == '\x1d' || c == '\x1e' || c == '\x85';

        internal static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>CPython 3.12+'s built-in sum() over floats from the default start of 0: the first term as 0.0 + x, then Neumaier
        /// compensated summation, the compensation added at the end (Python/bltinmodule.c). Every dot product, matrix product and
        /// norm the bakes mirror went through it, so the last bits only agree this way.</summary>
        internal static double Sum(IEnumerable<double> xs)
        {
            bool first = true; double f = 0.0, c = 0.0;
            foreach (double x in xs)
            {
                if (first) { f = 0.0 + x; first = false; continue; }
                double t = f + x;
                if (Math.Abs(f) >= Math.Abs(x)) c += (f - t) + x; else c += (x - t) + f;
                f = t;
            }
            if (first) return 0.0;
            if (c != 0 && double.IsFinite(c)) f += c;
            return f;
        }
        internal static double Sum(params double[] xs) => Sum((IEnumerable<double>)xs);

        /// <summary>Python's a % b for floats (the result takes the divisor's sign).</summary>
        internal static double FMod(double a, double b)
        {
            double mod = a % b;
            if (mod != 0 && ((b < 0) != (mod < 0))) mod += b;
            else if (mod == 0) mod = Math.CopySign(0.0, b);
            return mod;
        }

        /// <summary>Python's repr of a float for the values the bakes write into text (a short round-trip form, ".0" on integers).</summary>
        internal static string Repr(double x)
        {
            string s = x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e')) s += ".0";
            return s;
        }

        // ── CPython 3.11+ math.hypot / math.dist: the scaled double-length sum with one differential correction ──
        private static (double hi, double lo) DlMul(double x, double y) { double hi = x * y; return (hi, Math.FusedMultiplyAdd(x, y, -hi)); }
        private static (double hi, double lo) DlFastSum(double a, double b) { double hi = a + b; return (hi, (a - hi) + b); }

        internal static double VectorNorm(double[] vec, double max)
        {
            int n = vec.Length;
            if (double.IsInfinity(max)) return max;
            foreach (double v in vec) if (double.IsNaN(v)) return double.NaN;
            if (max == 0.0 || n <= 1) return max;
            int maxE = Math.ILogB(max) + 1;                                   // frexp: max = m × 2^maxE, m in [0.5, 1)
            if (maxE < -1023)
            {
                var scaled = new double[n]; for (int i = 0; i < n; i++) scaled[i] = vec[i] / double.Epsilon;
                return double.Epsilon * VectorNorm(scaled, max / double.Epsilon);
            }
            double scale = Math.ScaleB(1.0, -maxE), csum = 1.0, frac1 = 0.0, frac2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                double x = vec[i] * scale;
                var pr = DlMul(x, x);
                var sm = DlFastSum(csum, pr.hi);
                csum = sm.hi; frac1 += pr.lo; frac2 += sm.lo;
            }
            double h = Math.Sqrt(csum - 1.0 + (frac1 + frac2));
            var pr2 = DlMul(-h, h);
            var sm2 = DlFastSum(csum, pr2.hi);
            csum = sm2.hi; frac1 += pr2.lo; frac2 += sm2.lo;
            double xx = csum - 1.0 + (frac1 + frac2);
            h += xx / (2.0 * h);
            return h / scale;
        }

        /// <summary>Python's math.dist(p, q).</summary>
        internal static double Dist(double[] p, double[] q)
        {
            var d = new double[p.Length]; double max = 0;
            for (int i = 0; i < p.Length; i++) { d[i] = Math.Abs(p[i] - q[i]); if (d[i] > max) max = d[i]; }
            return VectorNorm(d, max);
        }

        /// <summary>Python's math.hypot(*coords).</summary>
        internal static double Hypot(params double[] v)
        {
            var d = new double[v.Length]; double max = 0;
            for (int i = 0; i < v.Length; i++) { d[i] = Math.Abs(v[i]); if (d[i] > max) max = d[i]; }
            return VectorNorm(d, max);
        }
    }
}
