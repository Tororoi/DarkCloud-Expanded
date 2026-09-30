using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Muska Lacka's oasis palm — georama part 12 "木" (mapinfo.cfg GRD_PARTS 12, sub-file e04t01 of gedit\e04\scene.scn) —
    /// in the item-model cash (CashModel), for Super Steve's Desert Bloom (CactusSpike). Both files are built from the town's
    /// own data once a session, off the ISO:
    ///  · the MODEL is the part's display block, e04t01_0.mds — the sub-file's first MDS block, up to its second (the part's
    ///    collision block, e04t01_a.mds); its offsets are block-relative, so the block stands alone (6,864 B). Two rigid
    ///    meshes: the trunk cyl283__s (node at z 8; its base ring centred there, radius 5.8, leaning to z 13.6 at its top,
    ///    y 0 … 50) and the fronds ha__a7ft (y 31 … 69.3), authored upright;
    ///  · its TEXTURES are e04b04 (the trunk, which samples its right half) and e04b10 (the fronds) of the building bank
    ///    e04b01.img in gedit\e04\img.pak — 256×256 8-bit each, 66.6 KB apiece, which with the model would overrun a cash
    ///    entry's 40,016-byte allocator. So the bank handed to the cash holds those two alone, each resampled (nearest texel:
    ///    8-bit indices cannot be blended) to <see cref="TexSize"/>² with its CLUT kept, written row-major under the IMG magic
    ///    (the source bank is IM2, its pixels in the GS's PSMT8 block order, un-swizzled here).</summary>
    internal static class PalmModel
    {
        private const string Tag = "[PalmModel] ";
        internal const int  CashKey = 30000;                         // the cash entry's label: no item has this id
        private const int   TexSize = 64;
        private const string SubFile = "e04t01", TexBank = "e04b01.img";
        private static readonly string[] Textures = { "e04b04", "e04b10" };

        private static readonly CashModel M = new CashModel(Tag, CashKey, "palm", Files);
        private static byte[] _mds, _img;
        private static bool _built, _failed;

        internal const int WeaponPassBlock = CashModel.WeaponPassBlock;
        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures() => M.KeepTextures(WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();

        private static (byte[] mds, byte[] img) Files()
        {
            if (!_built && !_failed)
            {
                try { _mds = BuildModel(); _img = BuildTextures(); _built = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"built from Muska Lacka: model {_mds.Length} B, textures {_img.Length} B"); }
                catch (Exception e) { _failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "could not be built: " + e.Message); }
            }
            return _built ? (_mds, _img) : (null, null);
        }

        /// <summary>The part's display block: the scene's sub-file directory (0x30-byte entries from 0x10: name, then offset and
        /// size at +0x10 / +0x14) gives the sub-file; its first two 16-aligned `MDS\0` blocks are the display and collision models.</summary>
        private static byte[] BuildModel()
        {
            byte[] scn = GameDataFiles.TryReadEntry(@"gedit\e04\scene.scn") ?? throw new IOException("gedit\\e04\\scene.scn not readable");
            int off = -1, size = 0;
            for (int o = 0x10; o + 0x30 <= scn.Length; o += 0x30)
            {
                string nm = IsoBytes.NameAt(scn, o, 16);
                if (nm.Length == 0 || !char.IsLetterOrDigit(nm[0])) break;
                if (nm == SubFile) { off = (int)IsoBytes.U32(scn, o + 0x10); size = (int)IsoBytes.U32(scn, o + 0x14); break; }
            }
            if (off <= 0 || size <= 0 || off + size > scn.Length) throw new IOException($"sub-file {SubFile} not in the scene directory");
            var starts = new List<int>();
            for (int p = 0; p + 4 <= size && starts.Count < 2; p += 16)
                if (scn[off + p] == 'M' && scn[off + p + 1] == 'D' && scn[off + p + 2] == 'S' && scn[off + p + 3] == 0) starts.Add(p);
            if (starts.Count < 2) throw new IOException($"{SubFile}: display/collision MDS blocks not found");
            return scn.AsSpan(off + starts[0], starts[1] - starts[0]).ToArray();
        }

        /// <summary>The two textures from the building bank, each resampled to TexSize², in a bank of their own.</summary>
        private static byte[] BuildTextures()
        {
            byte[] pak = GameDataFiles.TryReadEntry(@"gedit\e04\img.pak") ?? throw new IOException("gedit\\e04\\img.pak not readable");
            byte[] bankBytes = null;
            for (int p = 0; p + 0x50 <= pak.Length; )
            {
                string nm = IsoBytes.NameAt(pak, p, 0x40);
                int dataOff = (int)IsoBytes.U32(pak, p + 0x40), sz = (int)IsoBytes.U32(pak, p + 0x44), stride = (int)IsoBytes.U32(pak, p + 0x48);
                if (stride == 0 || nm.Length == 0) break;
                if (string.Equals(nm, TexBank, StringComparison.OrdinalIgnoreCase)) { bankBytes = pak.AsSpan(p + dataOff, sz).ToArray(); break; }
                p += stride;
            }
            if (bankBytes == null) throw new IOException($"{TexBank} not in img.pak");
            var bank = new CatPackBakes.Bank(bankBytes);
            bool swizzled = bank.Magic[2] == (byte)'2';
            var items = new List<(string, byte[])>();
            foreach (string t in Textures) items.Add((t, Resample(bank.Block(t), swizzled)));
            return CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 }, items);
        }

        /// <summary>One 8-bit TIM2 picture at TexSize², row-major: the header kept but for its sizes, the CLUT kept whole.
        /// TIM2 picture header (after the 16-byte file header): total +0 (header + 4 × image size in every file of this game), CLUT size +4, image size +8, header size +0xC (u16),
        /// image type +0x13 (5 = 8-bit), width +0x14, height +0x16 (u16); the pixels follow the header, the CLUT the pixels.</summary>
        private static byte[] Resample(byte[] tim, bool swizzled)
        {
            const int pic = 0x10;
            if (tim.Length < pic + 0x30 || tim[0] != 'T' || tim[1] != 'I' || tim[2] != 'M' || tim[3] != '2') throw new IOException("not a TIM2 picture");
            int clutSz = (int)IsoBytes.U32(tim, pic + 4), imgSz = (int)IsoBytes.U32(tim, pic + 8), hdrSz = IsoBytes.U16(tim, pic + 0xC);
            int w = IsoBytes.U16(tim, pic + 0x14), h = IsoBytes.U16(tim, pic + 0x16);
            if (tim[pic + 0x13] != 5 || imgSz != w * h) throw new IOException($"not an 8-bit picture ({w}×{h}, {imgSz} B)");
            byte[] px = tim.AsSpan(pic + hdrSz, imgSz).ToArray();
            if (swizzled) px = Unswizzle8(px, w, h);
            int n = TexSize;
            var outPx = new byte[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                outPx[y * n + x] = px[((y * h + h / 2) / n) * w + (x * w + w / 2) / n];   // the texel under each new texel's centre
            var outp = new byte[pic + hdrSz + n * n + clutSz];
            Array.Copy(tim, 0, outp, 0, pic + hdrSz);
            IsoBytes.U32(outp, pic + 0, (uint)(hdrSz + 4 * n * n));                      // the files' own convention: header + 4 × image (the bomb's 0x10030, these 0x40030)
            IsoBytes.U32(outp, pic + 8, (uint)(n * n));
            IsoBytes.U16(outp, pic + 0x14, (ushort)n);
            IsoBytes.U16(outp, pic + 0x16, (ushort)n);
            Array.Copy(outPx, 0, outp, pic + hdrSz, n * n);
            Array.Copy(tim, pic + hdrSz + imgSz, outp, pic + hdrSz + n * n, clutSz);
            return outp;
        }

        /// <summary>PSMT8 pixels from the GS's block order to row-major (CanalRipple's un-swizzle).</summary>
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
    }
}
