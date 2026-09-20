using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Queens wading-ripple TEXTURE: e01b22 + e01b23's pixels in e03's img.pak overwritten (byte-size-identical) with an
    /// 8-frame expanding-ring loop synthesized from the bobber-ripple sprite `hamon` (the sprite's ring art fills its top-left
    /// 32×32 quadrant, bilinearly upscaled to 64×64, luminance-keyed and gained); the vanilla texanime entry animates it. The
    /// ripple NODE itself is a static part IsoPatcher injects.</summary>
    internal static class CanalRipple
    {
        private static int FindTim2(byte[] c, string want)
        {
            byte[] wb = Encoding.ASCII.GetBytes(want);
            foreach (string magic in new[] { "IMG\0", "IM2\0" })
            {
                byte[] mb = Encoding.ASCII.GetBytes(magic); int i = 0;
                while (true)
                {
                    i = IsoBytes.FindFrom(c, mb, i);
                    if (i < 0) break;
                    int count = (int)IsoBytes.U32(c, i + 4);
                    if (0 < count && count < 64)
                        for (int e = 0; e < count; e++)
                        {
                            int ent = i + 0x10 + e * 0x30;
                            if (ent + 0x30 > c.Length) break;
                            if (IsoBytes.NameAt(c, ent, 0x20) == want) return i + (int)IsoBytes.U32(c, ent + 0x20);
                        }
                    i += 4;
                }
            }
            throw new IOException($"{want} not found in container");
        }

        private static int PakBank(byte[] pak, string entryName)
        {
            int p = 0;
            while (p < pak.Length && pak[p] != 0)
            {
                string name = IsoBytes.NameAt(pak, p, 0x20);
                int dataOff = (int)IsoBytes.U32(pak, p + 0x40), stride = (int)IsoBytes.U32(pak, p + 0x48);
                if (name == entryName) return p + dataOff;
                p += stride;
            }
            throw new IOException($"{entryName} entry not in img.pak");
        }

        private static byte[] Unswizzle8(byte[] data, int w, int h)
        {
            var outp = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int blockLoc = (y & ~0xF) * w + (x & ~0xF) * 2;
                int swapSel = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & ~3) >> 1) + (y & 1)) & 0x7;
                int colLoc = posY * w * 2 + ((x + swapSel) & 0x7) * 4;
                int bn = ((y >> 1) & 1) + ((x >> 2) & 2);
                int src = blockLoc + colLoc + bn;
                if (src < data.Length) outp[y * w + x] = data[src];
            }
            return outp;
        }

        private static int[] Bilerp(int[][] px, int w, int h, double fx, double fy)
        {
            fx = fx < 0 ? 0.0 : (fx > w - 1 ? w - 1.0 : fx);
            fy = fy < 0 ? 0.0 : (fy > h - 1 ? h - 1.0 : fy);
            int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
            double dx = fx - x0, dy = fy - y0;
            int[] c00 = px[y0 * w + x0], c10 = px[y0 * w + x1], c01 = px[y1 * w + x0], c11 = px[y1 * w + x1];
            var r = new int[4];
            for (int i = 0; i < 4; i++) r[i] = (int)(c00[i] * (1 - dx) * (1 - dy) + c10[i] * dx * (1 - dy) + c01[i] * (1 - dx) * dy + c11[i] * dx * dy + 0.5);
            return r;
        }

        private static int[][] HamonRingRgba(byte[] pak)
        {
            int bank = PakBank(pak, "effect.img");
            int count = (int)IsoBytes.U32(pak, bank + 4), t = -1;
            for (int e = 0; e < count; e++) { int ent = bank + 0x10 + e * 0x30; if (IsoBytes.NameAt(pak, ent, 0x20) == "hamon") { t = bank + (int)IsoBytes.U32(pak, ent + 0x20); break; } }
            if (t < 0) throw new IOException("hamon not in effect.img bank");
            int clutSz = (int)IsoBytes.U32(pak, t + 0x14), imgSz = (int)IsoBytes.U32(pak, t + 0x18), hdrSz = IsoBytes.U16(pak, t + 0x1C);
            int w = IsoBytes.U16(pak, t + 0x24), h = IsoBytes.U16(pak, t + 0x26);
            if (w != 64 || h != 64 || pak[t + 0x23] != 5) throw new IOException($"hamon unexpected shape {w}x{h} type {pak[t + 0x23]}");
            byte[] img = pak.AsSpan(t + 0x10 + hdrSz, imgSz).ToArray(), clut = pak.AsSpan(t + 0x10 + hdrSz + imgSz, clutSz).ToArray();
            var pal = new int[256][];
            for (int i = 0; i < 256; i++)
            {
                int blk = i / 32, idx = i % 32;
                if (8 <= idx && idx < 16) idx += 8; else if (16 <= idx && idx < 24) idx -= 8;
                int j = (blk * 32 + idx) * 4;
                pal[i] = new int[] { clut[j], clut[j + 1], clut[j + 2], clut[j + 3] };
            }
            byte[] de = Unswizzle8(img, w, h);
            var tl = new int[32 * 32][]; for (int sy = 0; sy < 32; sy++) for (int sx = 0; sx < 32; sx++) tl[sy * 32 + sx] = pal[de[sy * 64 + sx]];
            var quad = new int[64 * 64][]; for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) quad[y * 64 + x] = Bilerp(tl, 32, 32, x / 2.0, y / 2.0);
            int peak = 0; foreach (var q in quad) peak = Math.Max(peak, Math.Max(q[0], Math.Max(q[1], q[2]))); if (peak == 0) peak = 1;
            const double RingGain = 2.5;
            var outp = new int[64 * 64][];
            for (int i = 0; i < quad.Length; i++)
            {
                int r = quad[i][0], g = quad[i][1], b = quad[i][2], lum = Math.Max(r, Math.Max(g, b));
                outp[i] = new[] { Math.Min(255, (int)PyMath.FloorDiv(r * 255 * RingGain, peak)), Math.Min(255, (int)PyMath.FloorDiv(g * 255 * RingGain, peak)),
                                  Math.Min(255, (int)PyMath.FloorDiv(b * 255 * RingGain, peak)), Math.Min(0x80, (int)PyMath.FloorDiv(lum * 0x80 * RingGain, peak)) };
            }
            return outp;
        }

        private static List<int[][]> RingFrames(byte[] pak)
        {
            var src = HamonRingRgba(pak);
            var frames = new List<int[][]>();
            double[] envs = { 0.55, 1.0, 1.0, 1.0, 1.0, 0.9, 0.7, 0.4 };
            for (int f = 0; f < 8; f++)
            {
                double s = 0.70 + 0.45 * f / 7.0, env = envs[f];
                var outp = new int[64 * 64][];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    double sx = 32 + (x - 32) / s, sy = 32 + (y - 32) / s;
                    int r, g, b, a;
                    if (0 <= sx && sx <= 63 && 0 <= sy && sy <= 63) { var c = Bilerp(src, 64, 64, sx, sy); r = c[0]; g = c[1]; b = c[2]; a = c[3]; }
                    else { r = g = b = a = 0; }
                    outp[y * 64 + x] = new[] { r, g, b, (int)(a * env) };
                }
                frames.Add(outp);
            }
            frames.Reverse();
            return frames;
        }

        /// <summary>e03/img.pak with e01b22 + e01b23's pixels replaced by the bobber-ripple animation, in place.</summary>
        internal static byte[] RetextureRippleBank(byte[] pak, Action<string> log)
        {
            var outp = (byte[])pak.Clone();
            var frames = RingFrames(pak);
            foreach (var (name, fw, fh, blocks) in new[] { ("e01b22", 64, 64, new List<int[][]> { frames[0] }), ("e01b23", 64, 512, frames) })
            {
                int t = FindTim2(outp, name);
                int clutSz = (int)IsoBytes.U32(outp, t + 0x14), imgSz = (int)IsoBytes.U32(outp, t + 0x18), hdrSz = IsoBytes.U16(outp, t + 0x1C);
                int w = IsoBytes.U16(outp, t + 0x24), h = IsoBytes.U16(outp, t + 0x26);
                if (w != fw || h != fh || outp[t + 0x23] != 3 || clutSz != 0 || imgSz != fw * fh * 4) throw new IOException($"{name}: unexpected TIM2 shape {w}x{h} t{outp[t + 0x23]} img {imgSz}");
                int bas = t + 0x10 + hdrSz;
                for (int bi = 0; bi < blocks.Count; bi++)
                    for (int i = 0; i < blocks[bi].Length; i++)
                    {
                        int o = bas + (bi * fw * fw + i) * 4;
                        outp[o] = (byte)blocks[bi][i][0]; outp[o + 1] = (byte)blocks[bi][i][1]; outp[o + 2] = (byte)blocks[bi][i][2]; outp[o + 3] = (byte)blocks[bi][i][3];
                    }
            }
            if (outp.Length != pak.Length) throw new IOException("retexture changed the pak size?!");
            log("ripple retexture: e01b22 + e01b23 pixels -> hamon bobber-ripple 8-frame loop");
            return outp;
        }
    }
}
