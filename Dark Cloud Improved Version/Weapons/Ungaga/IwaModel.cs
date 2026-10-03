using System.Collections.Generic;

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
        internal const int  CashKey = 30001;                         // the cash entry's label: no item has this id (Queens' trees are 30000)
        private const int   TexSize = 128;
        private const string Pack = @"gedit\s96\chara\iwa.chr", Model = "iwa.mds", Bank = "iwa.img", Texture = "d02b10";
        /// <summary>The rock's radius about its root (the mesh's extent, every axis).</summary>
        internal const float Radius = 20.4f;

        private static readonly CashModel M = CashModel.FromChrPack(Tag, CashKey, "rock", Pack, Model, Bank, StandIn).WithFullTexture(Full);
        private static CatPackBakes.Bank _bank;                                           // iwa.img parsed, once the files are built
        private static List<(string name, int w, int h, byte[] pixels, byte[] clut)> _full;   // d02b10 whole: the picture the stand-in's entry is pointed at

        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures() => M.KeepTextures(CashModel.WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();
        /// <summary>iwa.mds as the pack has it (GroundShadow's disc borrows its header, node and materials); null when unreadable.</summary>
        internal static byte[] ModelBytes() => M.Files().mds;

        /// <summary>The bank handed to the cash: d02b10 alone at <see cref="TexSize"/>²; the source bank and the full picture kept.</summary>
        private static byte[] StandIn(byte[] img)
        {
            _bank = new CatPackBakes.Bank(img);
            _full = CashModel.FullPictures(_bank, Texture);
            return CashModel.StandInBank(_bank, TexSize, Texture);
        }

        /// <summary>An IMG bank of d02b10 alone at <paramref name="n"/>² — the texture GroundShadow's disc (iwa's materials) is loaded with.</summary>
        internal static byte[] StandInBank(int n)
        {
            M.Files();
            return _bank == null ? null : CashModel.StandInBank(_bank, n, Texture);
        }

        private static List<(string name, int w, int h, byte[] pixels, byte[] clut)> Full() { M.Files(); return _full; }
    }
}
