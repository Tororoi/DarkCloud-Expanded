using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>PS2 sound-processor ADPCM ("VAG" sample data in a `.bd`): 16-byte blocks of 28 samples — a byte of predictor (high
    /// nibble) and shift (low nibble), a flags byte, then 14 bytes of 4-bit residuals, low nibble first. A sample decodes as
    /// (residual &lt;&lt; 12 &gt;&gt; shift) + (s1 × c0 + s2 × c1 + 32) &gt;&gt; 6. Flags: 4 loop start, 1 loop end (stop without 2), 7 a
    /// silent block looping on itself. Plus the few edits MonsterSoundBake makes to a sample before re-encoding it.</summary>
    internal static class VagAudio
    {
        private static readonly int[,] Coef = { { 0, 0 }, { 60, 0 }, { 115, -52 }, { 98, -55 }, { 122, -60 } };

        /// <summary>The sample's PCM, up to and including the block that ends it (flag 1).</summary>
        internal static short[] Decode(byte[] vag)
        {
            var o = new List<short>();
            int s1 = 0, s2 = 0;
            for (int b = 0; b + 16 <= vag.Length; b += 16)
            {
                int shift = vag[b] & 15, f = Math.Min(vag[b] >> 4, 4), flags = vag[b + 1];
                for (int k = 0; k < 28; k++)
                {
                    int n = (vag[b + 2 + k / 2] >> ((k & 1) * 4)) & 15;
                    if (n >= 8) n -= 16;
                    int s = Math.Clamp(((n << 12) >> shift) + ((s1 * Coef[f, 0] + s2 * Coef[f, 1] + 32) >> 6), -32768, 32767);
                    o.Add((short)s); s2 = s1; s1 = s;
                }
                if ((flags & 1) != 0) break;
            }
            return o.ToArray();
        }

        /// <summary>PCM encoded block by block, each with the predictor and shift that reproduce it best: a silent first block, the loop
        /// start on the first sound block, the loop end (a stop) on the last, then a silent block looping on itself — as the game's
        /// one-shot samples are laid out.</summary>
        internal static byte[] Encode(short[] pcm)
        {
            int blocks = (pcm.Length + 27) / 28;
            var o = new byte[(blocks + 2) * 16];
            int s1 = 0, s2 = 0;
            for (int b = 0; b < blocks; b++)
            {
                var x = new int[28];
                for (int k = 0; k < 28; k++) x[k] = b * 28 + k < pcm.Length ? pcm[b * 28 + k] : 0;
                long best = long.MaxValue; int bf = 0, bs = 12; var bn = new int[28]; int b1 = 0, b2 = 0;
                for (int f = 0; f < 5; f++)
                    for (int shift = 0; shift <= 12; shift++)
                    {
                        int p1 = s1, p2 = s2; long err = 0; var n = new int[28];
                        for (int k = 0; k < 28 && err < best; k++)
                        {
                            int pred = (p1 * Coef[f, 0] + p2 * Coef[f, 1] + 32) >> 6;
                            double q = (x[k] - pred) * Math.Pow(2, shift) / 4096.0;
                            int nk = Math.Clamp((int)Math.Round(q), -8, 7);
                            int y = Math.Clamp(((nk << 12) >> shift) + pred, -32768, 32767);
                            err += (long)(x[k] - y) * (x[k] - y); n[k] = nk; p2 = p1; p1 = y;
                        }
                        if (err < best) { best = err; bf = f; bs = shift; bn = n; b1 = p1; b2 = p2; }
                    }
                int at = (b + 1) * 16;
                o[at] = (byte)((bf << 4) | bs);
                o[at + 1] = (byte)(b == 0 ? 4 : b == blocks - 1 ? 1 : 0);
                for (int k = 0; k < 28; k += 2) o[at + 2 + k / 2] = (byte)((bn[k] & 15) | ((bn[k + 1] & 15) << 4));
                s1 = b1; s2 = b2;
            }
            o[o.Length - 15] = 7;                                                      // the trailing silent block, looping on itself
            return o;
        }

        /// <summary>The first <paramref name="samples"/> samples.</summary>
        internal static float[] Cut(short[] pcm, int samples)
        {
            var o = new float[Math.Min(samples, pcm.Length)];
            for (int i = 0; i < o.Length; i++) o[i] = pcm[i] / 32768f;
            return o;
        }

        /// <summary>Half the rate: a windowed-sinc low-pass at the new Nyquist, then every second sample.</summary>
        internal static float[] HalfRate(float[] x)
        {
            const int Taps = 63, Mid = Taps / 2;
            var h = new double[Taps]; double sum = 0;
            for (int i = 0; i < Taps; i++)
            {
                int m = i - Mid;
                double sinc = m == 0 ? 0.45 : Math.Sin(Math.PI * 0.45 * m) / (Math.PI * m);
                h[i] = sinc * (0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (Taps - 1))); sum += h[i];
            }
            var o = new float[x.Length / 2];
            for (int j = 0; j < o.Length; j++)
            {
                double acc = 0;
                for (int i = 0; i < Taps; i++) { int k = 2 * j + i - Mid; if (k >= 0 && k < x.Length) acc += x[k] * h[i]; }
                o[j] = (float)(acc / sum);
            }
            return o;
        }

        /// <summary>Faster by <paramref name="k"/> at the same pitch: Hann-windowed grains of <paramref name="grain"/> samples read k
        /// times faster than they are overlap-added (a quarter grain apart), normalised by the windows' sum.</summary>
        internal static float[] Stretch(float[] x, double k, int grain = 1024)
        {
            int outLen = (int)(x.Length / k), hopOut = grain / 4;
            var o = new double[outLen + grain]; var norm = new double[outLen + grain]; var w = new double[grain];
            for (int i = 0; i < grain; i++) w[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / grain);
            for (int j = 0; j * hopOut < outLen; j++)
            {
                int ip = (int)(j * hopOut * k), op = j * hopOut;
                for (int i = 0; i < grain && ip + i < x.Length; i++) { o[op + i] += x[ip + i] * w[i]; norm[op + i] += w[i]; }
            }
            var r = new float[outLen];
            for (int i = 0; i < outLen; i++) r[i] = norm[i] > 1e-3 ? (float)(o[i] / norm[i]) : 0f;
            return r;
        }

        /// <summary>The last <paramref name="samples"/> samples faded linearly to silence, then as 16-bit PCM.</summary>
        internal static short[] FadeOut(float[] x, int samples)
        {
            var o = new short[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                double g = i < x.Length - samples ? 1.0 : (double)(x.Length - i) / samples;
                o[i] = (short)Math.Clamp(Math.Round(x[i] * g * 32767.0), -32768, 32767);
            }
            return o;
        }
    }
}
