using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The 8-bit TIM2 picture as this game's banks hold it, and the two GS orderings its bytes come in.
    /// Picture header (after the 16-byte file header, <see cref="Pic"/>): total +0 (header + 4 × image size in every file of this
    /// game), CLUT size +4, image size +8, header size +0xC (u16), image type +0x13 (5 = 8-bit), width +0x14, height +0x16 (u16);
    /// the pixels follow the header, the CLUT the pixels. An IM2 bank's pixels are in PSMT8 block order (<see cref="Unswizzle8"/>);
    /// a PSMT8 CLUT reaches the GS in CSM1 order (<see cref="Csm1Index"/>).</summary>
    internal static class Tim8
    {
        internal const int Pic = 0x10;                             // the picture header's offset in the file

        /// <summary>A TIM2 picture's header fields: image type (5 = 8-bit, 3 = 32-bit), picture-header size, width, height.</summary>
        internal static (int type, int hdr, int w, int h) PictureInfo(byte[] data)
        {
            if (System.Text.Encoding.ASCII.GetString(data, 0, 4) != "TIM2") throw new IOException("not a TIM2 block");
            return (data[Pic + 0x13], IsoBytes.U16(data, Pic + 0x0C), IsoBytes.U16(data, Pic + 0x14), IsoBytes.U16(data, Pic + 0x16));
        }

        /// <summary>The byte length of the TIM2 picture at <paramref name="at"/> in <paramref name="data"/>: the file header, the
        /// picture header, the image and the CLUT — no padding or adjacent-entry spillover.</summary>
        internal static int CleanLength(byte[] data, int at)
        {
            uint clutSz = IsoBytes.U32(data, at + Pic + 4), imgSz = IsoBytes.U32(data, at + Pic + 8);
            ushort hdrSz = IsoBytes.U16(data, at + Pic + 0xC);
            return Pic + hdrSz + (int)imgSz + (int)clutSz;
        }

        /// <summary>A <paramref name="w"/>×<paramref name="h"/> 8-bit TIM2 built on a vanilla 8-bit picture's headers: the template's file +
        /// picture headers with the sizes set and the GS register words (GsTex0 / GsTex1 / GsRegs / GsTexClut) ZEROED like every vanilla
        /// picture (the engine derives them at load), then <paramref name="pixels"/> (w·h indices) and <paramref name="clut"/> (256 × RGBA).
        /// <paramref name="what"/> names the caller in the template check's message.</summary>
        internal static byte[] Build8(byte[] template, int w, int h, byte[] pixels, byte[] clut, string what)
        {
            var info = PictureInfo(template);
            if (info.type != 5 || info.hdr != 0x30) throw new IOException($"{what}: template is not an 8-bit TIM2 with a 0x30 picture header");
            var hdr = template.AsSpan(0, Pic + 0x30).ToArray();
            IsoBytes.U32(hdr, Pic, (uint)(0x30 + pixels.Length + clut.Length)); IsoBytes.U32(hdr, Pic + 4, (uint)clut.Length); IsoBytes.U32(hdr, Pic + 8, (uint)pixels.Length);
            IsoBytes.U16(hdr, Pic + 0x14, (ushort)w); IsoBytes.U16(hdr, Pic + 0x16, (ushort)h);
            Array.Clear(hdr, Pic + 0x18, 0x18);
            var outp = new byte[hdr.Length + pixels.Length + clut.Length];
            Array.Copy(hdr, outp, hdr.Length);
            Array.Copy(pixels, 0, outp, hdr.Length, pixels.Length);
            Array.Copy(clut, 0, outp, hdr.Length + pixels.Length, clut.Length);
            return outp;
        }

        /// <summary>An 8-bit TIM2 picture whole: its size, its pixels row-major (un-swizzled from the GS block order when
        /// <paramref name="swizzled"/>), and its CLUT bytes as the file has them.</summary>
        internal static (int w, int h, byte[] pixels, byte[] clut) ReadTim8(byte[] tim, bool swizzled)
        {
            if (tim.Length < Pic + 0x30 || tim[0] != 'T' || tim[1] != 'I' || tim[2] != 'M' || tim[3] != '2') throw new IOException("not a TIM2 picture");
            int clutSz = (int)IsoBytes.U32(tim, Pic + 4), imgSz = (int)IsoBytes.U32(tim, Pic + 8), hdrSz = IsoBytes.U16(tim, Pic + 0xC);
            int w = IsoBytes.U16(tim, Pic + 0x14), h = IsoBytes.U16(tim, Pic + 0x16);
            if (tim[Pic + 0x13] != 5 || imgSz != w * h) throw new IOException($"not an 8-bit picture ({w}×{h}, {imgSz} B)");
            byte[] px = tim.AsSpan(Pic + hdrSz, imgSz).ToArray();
            if (swizzled) px = Unswizzle8(px, w, h);
            return (w, h, px, tim.AsSpan(Pic + hdrSz + imgSz, clutSz).ToArray());
        }

        /// <summary>One 8-bit TIM2 picture resampled to <paramref name="n"/>² (nearest texel: 8-bit indices cannot be blended),
        /// row-major: the header kept but for its sizes, the CLUT kept whole.</summary>
        internal static byte[] ResampleTim8(byte[] tim, bool swizzled, int n)
        {
            var (w, h, px, clut) = ReadTim8(tim, swizzled);
            int hdrSz = IsoBytes.U16(tim, Pic + 0xC);
            var outPx = new byte[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                outPx[y * n + x] = px[((y * h + h / 2) / n) * w + (x * w + w / 2) / n];   // the texel under each new texel's centre
            var outp = new byte[Pic + hdrSz + n * n + clut.Length];
            Array.Copy(tim, 0, outp, 0, Pic + hdrSz);
            IsoBytes.U32(outp, Pic + 0, (uint)(hdrSz + 4 * n * n));                      // the files' own convention: header + 4 × image (the bomb's 0x10030, these 0x40030)
            IsoBytes.U32(outp, Pic + 8, (uint)(n * n));
            IsoBytes.U16(outp, Pic + 0x14, (ushort)n);
            IsoBytes.U16(outp, Pic + 0x16, (ushort)n);
            Array.Copy(outPx, 0, outp, Pic + hdrSz, n * n);
            Array.Copy(clut, 0, outp, Pic + hdrSz + n * n, clut.Length);
            return outp;
        }

        /// <summary>Where the pixel (<paramref name="x"/>, <paramref name="y"/>) of a <paramref name="w"/>-wide picture lies in PSMT8
        /// block order — the GS's 16×16 blocks, each stored as eight 32-bit columns.</summary>
        internal static int BlockOffset(int x, int y, int w)
        {
            int blockLoc = (y & ~0xF) * w + (x & ~0xF) * 2;
            int swapSel = (((y + 2) >> 2) & 0x1) * 4;
            int posY = (((y & ~3) >> 1) + (y & 1)) & 0x7;
            int colLoc = posY * w * 2 + ((x + swapSel) & 0x7) * 4;
            int bn = ((y >> 1) & 1) + ((x >> 2) & 2);
            return blockLoc + colLoc + bn;
        }

        /// <summary>Row-major 8-bit pixels into the GS's PSMT8 block order — what an `IM2` bank's pictures hold (<see cref="Unswizzle8"/>
        /// undone).</summary>
        internal static byte[] Swizzle8(byte[] rowMajor, int w, int h)
        {
            var outp = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) outp[BlockOffset(x, y, w)] = rowMajor[y * w + x];
            return outp;
        }

        /// <summary>An 8-bit TIM2 whose picture is row-major, its picture swizzled into the GS block order (what an `IM2` bank's pictures
        /// hold: the engine uploads them as they are, so row-major pixels would land in the wrong blocks).</summary>
        internal static byte[] Swizzled(byte[] tim)
        {
            var (_, hdr, w, h) = PictureInfo(tim);
            byte[] o = (byte[])tim.Clone();
            Swizzle8(tim.AsSpan(Pic + hdr, w * h).ToArray(), w, h).CopyTo(o, Pic + hdr);
            return o;
        }

        /// <summary>PSMT8 pixels from the GS's block order to row-major (a source byte past the data is left 0).</summary>
        internal static byte[] Unswizzle8(byte[] data, int w, int h)
        {
            var outp = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int src = BlockOffset(x, y, w);
                if (src < data.Length) outp[y * w + x] = data[src];
            }
            return outp;
        }

        /// <summary>Where an 8-bit index's colour sits in the CLUT as the GS reads it (CSM1: bits 3 and 4 of the index swapped).</summary>
        internal static int Csm1Index(int i) => (i & ~0x18) | ((i & 0x08) << 1) | ((i & 0x10) >> 1);
    }
}
