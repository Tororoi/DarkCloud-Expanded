using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The boulder Master Utan throws in his cutscene (gedit\s96\chara\iwa.chr) in the item-model cash (CashModel), for
    /// the Terra Sword's falling rock (TerraSword). Both files are built from the pack once a session, off the ISO:
    ///  · the MODEL is iwa.mds whole (5,968 B): one rigid mesh `iwa`, its root at the rock's centre, ±20.4 on every axis;
    ///  · its TEXTURE is d02b10 of iwa.img — 256×256 8-bit (66.6 KB, more than a cash entry's 40,016-byte allocator takes with the
    ///    model), so the bank handed to the cash holds a stand-in resampled to <see cref="TexSize"/>² (the Bomb's own size), CLUT
    ///    kept, row-major under the IMG magic (the source bank is IM2: PSMT8 block order, un-swizzled); once loaded, the entry is
    ///    pointed at the full 256² picture kept outside the cash (<see cref="CashModel.FullTexture"/>).</summary>
    internal static class IwaModel
    {
        private const string Tag = "[IwaModel] ";
        internal const int  CashKey = 30001;                         // the cash entry's label: no item has this id (the palm is 30000)
        private const int   TexSize = 128;
        private const string Pack = @"gedit\s96\chara\iwa.chr", Model = "iwa.mds", Bank = "iwa.img", Texture = "d02b10";
        /// <summary>The rock's radius about its root (the mesh's extent, every axis).</summary>
        internal const float Radius = 20.4f;

        private static readonly CashModel M = new CashModel(Tag, CashKey, "rock", Files) { FullTexture = Full };
        private static byte[] _mds, _img;
        private static bool _built, _failed;

        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures() => M.KeepTextures(CashModel.WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();
        /// <summary>iwa.mds as the pack has it (GroundShadow's disc borrows its header, node and materials); null when unreadable.</summary>
        internal static byte[] ModelBytes() { var (mds, _) = Files(); return mds; }

        private static byte[] _texBlock; private static bool _texSwizzled;

        /// <summary>An IMG bank of d02b10 alone at <paramref name="n"/>² — the texture GroundShadow's disc (iwa's materials) is loaded with.</summary>
        internal static byte[] StandInBank(int n)
        {
            Files();
            if (_texBlock == null) return null;
            return CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 },
                                           new List<(string, byte[])> { (Texture, CashModel.ResampleTim8(_texBlock, _texSwizzled, n)) });
        }

        private static byte[] _fullPx, _fullClut;
        private static int _fullW, _fullH;

        /// <summary>The full d02b10, row-major, and its CLUT — the picture the stand-in's entry is pointed at.</summary>
        private static List<(string, int, int, byte[], byte[])> Full()
        {
            Files();
            return _fullPx == null ? null : new List<(string, int, int, byte[], byte[])> { (Texture, _fullW, _fullH, _fullPx, _fullClut) };
        }

        private static (byte[] mds, byte[] img) Files()
        {
            if (!_built && !_failed)
            {
                try
                {
                    byte[] chr = GameDataFiles.TryReadEntry(Pack) ?? throw new IOException(Pack + " not readable");
                    var pack = ChrPack.Parse(chr);
                    _mds = (pack.Find(Model) ?? throw new IOException(Pack + " lacks " + Model)).Payload;
                    var bank = new CatPackBakes.Bank((pack.Find(Bank) ?? throw new IOException(Pack + " lacks " + Bank)).Payload);
                    bool swizzled = bank.Magic[2] == (byte)'2';
                    _texBlock = bank.Block(Texture); _texSwizzled = swizzled;
                    (_fullW, _fullH, _fullPx, _fullClut) = CashModel.ReadTim8(bank.Block(Texture), swizzled);
                    _img = CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 },
                                                   new System.Collections.Generic.List<(string, byte[])> { (Texture, CashModel.ResampleTim8(bank.Block(Texture), swizzled, TexSize)) });
                    _built = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"built from Master Utan's boulder: model {_mds.Length} B, texture {_img.Length} B");
                }
                catch (Exception e) { _failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "could not be built: " + e.Message); }
            }
            return _built ? (_mds, _img) : (null, null);
        }
    }
}
