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
            foreach (string t in Textures) items.Add((t, CashModel.ResampleTim8(bank.Block(t), swizzled, TexSize)));
            return CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 }, items);
        }
    }
}
