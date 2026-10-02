using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Queens' trees — georama part 12 (mapinfo.cfg GRD_PARTS 12, sub-file e03t01 of gedit\e03\scene.scn) — whole, in the
    /// item-model cash (CashModel), for Super Steve's Desert Bloom (CactusSpike). Built from the town's own data once a session, off
    /// the ISO:
    ///  · the MODEL is the part's display block, e03t01_0.mds — the sub-file's first MDS block, up to its second (the part's
    ///    collision block); its offsets are block-relative, so the block stands alone (16,304 B). Sixteen nodes under the root
    ///    `null4` (at the origin): two trees — trunk `cyl28__s` at (0, 8), 51 tall, its crown `ha__a7f`; trunk `cyl283__s` at
    ///    (−10, −11), 72.5 tall, its crown `ha2__a7f` — ten grass sprites `k1__a40by` … `k10__a40by` scattered between them, and a
    ///    50×50 grass square `grid__a7f` at y 0.1 (x −31 … 19, z −26 … 24, centred (−6, −1)) whose mesh is DROPPED (textured to
    ///    match Queens' ground, it looked wrong on a dungeon floor). More than BladeProp's eight-node cave: the copy goes to its
    ///    large-tree cave;
    ///  · its TEXTURE is e03b04 (the trees and the sprites; e03b10 was the dropped grass square's) of the building bank e03b01.img in
    ///    gedit\e03\img.pak — 256×256 8-bit. The bank handed to the cash holds a <see cref="TexSize"/>² stand-in (nearest texel,
    ///    CLUT kept, row-major under the IMG magic; the source bank is IM2, un-swizzled here) — once loaded, their entries are pointed
    ///    at the full 256² pictures kept outside the cash (<see cref="CashModel.FullTexture"/>).</summary>
    internal static class QueensTrees
    {
        private const string Tag = "[QueensTrees] ";
        internal const int  CashKey = 30000;                         // the cash entry's label: no item has this id
        private const int   TexSize = 8;                          // the stand-ins only need their entries: the full pictures replace them
        private const string SubFile = "e03t01", TexBank = "e03b01.img", Scene = @"gedit\e03\scene.scn", ImgPak = @"gedit\e03\img.pak";
        private static readonly string[] Textures = { "e03b04" };   // the trees and the sprites (e03b10, the grass square's, goes with it)
        private const string Ground = "grid__a7f";                 // Queens' paving-matched grass square: dropped, it looked wrong on a dungeon floor

        private static readonly CashModel M = new CashModel(Tag, CashKey, "Queens trees", Files) { FullTexture = Full, FullOffset = 0x320000 };   // past the rock's (IwaModel, +0x300000)
        private static List<(string, int, int, byte[], byte[])> _full;

        /// <summary>The two textures whole (row-major, CLUTs as the bank has them) — what the stand-ins' entries are pointed at.</summary>
        private static List<(string, int, int, byte[], byte[])> Full() { Files(); return _full; }
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
                try { _mds = BuildModel(); _img = BuildTextures(); _built = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"built from Queens: model {_mds.Length} B, textures {_img.Length} B"); }
                catch (Exception e) { _failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "could not be built: " + e.Message); }
            }
            return _built ? (_mds, _img) : (null, null);
        }

        /// <summary>The part's display block: the scene's sub-file directory (0x30-byte entries from 0x10: name, then offset and
        /// size at +0x10 / +0x14) gives the sub-file; its first two 16-aligned `MDS\0` blocks are the display and collision models.</summary>
        private static byte[] BuildModel()
        {
            byte[] scn = GameDataFiles.TryReadEntry(Scene) ?? throw new IOException(Scene + " not readable");
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
            byte[] mds = scn.AsSpan(off + starts[0], starts[1] - starts[0]).ToArray();
            // The grass square's mesh dropped: its node's mesh offset (+0x28 of its 0x70-byte entry in the node table: count +8, table
            // +0xC) zeroed, so LoadMDSFile builds it as an empty frame and every other node keeps its index and parent.
            int count = (int)IsoBytes.U32(mds, 8), table = (int)IsoBytes.U32(mds, 0xC);
            bool dropped = false;
            for (int i = 0; i < count; i++)
                if (IsoBytes.NameAt(mds, table + i * 0x70 + 8, 0x20) == Ground) { IsoBytes.U32(mds, table + i * 0x70 + 0x28, 0); dropped = true; }
            if (!dropped) throw new IOException($"{SubFile}: node {Ground} not found");
            return mds;
        }

        /// <summary>The two textures from the building bank, each resampled to TexSize², in a bank of their own.</summary>
        private static byte[] BuildTextures()
        {
            byte[] pak = GameDataFiles.TryReadEntry(ImgPak) ?? throw new IOException(ImgPak + " not readable");
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
            var full = new List<(string, int, int, byte[], byte[])>();
            foreach (string t in Textures)
            {
                items.Add((t, CashModel.ResampleTim8(bank.Block(t), swizzled, TexSize)));
                var (w, h, px, clut) = CashModel.ReadTim8(bank.Block(t), swizzled);
                full.Add((t, w, h, px, clut));
            }
            _full = full;
            return CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 }, items);
        }
    }
}
