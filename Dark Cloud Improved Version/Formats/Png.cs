using System;
using System.IO;
using System.IO.Compression;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A minimal PNG reader for patch-time assets (no image library is cross-platform in .NET): 8-bit RGBA or RGB,
    /// non-interlaced, every filter type. Returns the pixels as RGBA.</summary>
    internal static class Png
    {
        internal static (int w, int h, byte[] rgba) DecodeRgba(byte[] png)
        {
            ReadOnlySpan<byte> sig = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(sig)) throw new IOException("not a PNG");
            int w = 0, h = 0, colorType = -1, o = 8;
            using var idat = new MemoryStream();
            while (o + 8 <= png.Length)
            {
                int len = (png[o] << 24) | (png[o + 1] << 16) | (png[o + 2] << 8) | png[o + 3];
                string type = System.Text.Encoding.ASCII.GetString(png, o + 4, 4);
                if (type == "IHDR")
                {
                    w = (png[o + 8] << 24) | (png[o + 9] << 16) | (png[o + 10] << 8) | png[o + 11];
                    h = (png[o + 12] << 24) | (png[o + 13] << 16) | (png[o + 14] << 8) | png[o + 15];
                    int depth = png[o + 16]; colorType = png[o + 17]; int interlace = png[o + 20];
                    if (depth != 8 || (colorType != 6 && colorType != 2) || interlace != 0)
                        throw new IOException($"PNG must be 8-bit RGBA or RGB, non-interlaced (depth {depth}, colour type {colorType}, interlace {interlace})");
                }
                else if (type == "IDAT") idat.Write(png, o + 8, len);
                else if (type == "IEND") break;
                o += 12 + len;
            }
            int bpp = colorType == 6 ? 4 : 3, stride = w * bpp;
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            using var rawMs = new MemoryStream(); z.CopyTo(rawMs);
            byte[] raw = rawMs.ToArray();
            var outp = new byte[w * h * 4];
            var prev = new byte[stride]; var line = new byte[stride];
            int p = 0;
            for (int y = 0; y < h; y++)
            {
                int f = raw[p++];
                Array.Copy(raw, p, line, 0, stride); p += stride;
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? line[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                    int pr = f switch
                    {
                        1 => a, 2 => b, 3 => (a + b) >> 1,
                        4 => Math.Abs(b - c) <= Math.Abs(a - c) && Math.Abs(b - c) <= Math.Abs(a + b - 2 * c) ? a : (Math.Abs(a - c) <= Math.Abs(a + b - 2 * c) ? b : c),
                        _ => 0,
                    };
                    line[i] = (byte)(line[i] + pr);
                }
                for (int x = 0; x < w; x++)
                {
                    int d = (y * w + x) * 4, s = x * bpp;
                    outp[d] = line[s]; outp[d + 1] = line[s + 1]; outp[d + 2] = line[s + 2]; outp[d + 3] = bpp == 4 ? line[s + 3] : (byte)255;
                }
                (prev, line) = (line, prev);
            }
            return (w, h, outp);
        }
    }
}
