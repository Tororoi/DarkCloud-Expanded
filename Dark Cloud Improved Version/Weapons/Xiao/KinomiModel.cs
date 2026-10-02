using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The nut (木の実) from gedit\s04\chara\e114kinomi.chr in the item-model cash (CashModel), for Super Steve's Terra Sword
    /// sphere (TerraSword): the pack's two files as they are — e114kinomi.mds (6,352 B: one mesh `sphere3`, its root at the nut's
    /// centre, ±1.9) and kinomi.img (an IMG bank of one 128×128 8-bit texture, `kinomi`, 17,536 B) — which fit a cash entry whole.</summary>
    internal static class KinomiModel
    {
        private const string Tag = "[KinomiModel] ";
        internal const int  CashKey = 30003;                         // the cash entry's label: no item has this id
        private const string Pack = @"gedit\s04\chara\e114kinomi.chr", Model = "e114kinomi.mds", Bank = "kinomi.img";
        /// <summary>The nut's radius about its root at 1× (the mesh's extent).</summary>
        internal const float Radius = 1.9f;

        private static readonly CashModel M = new CashModel(Tag, CashKey, "kinomi", Files);
        private static byte[] _mds, _img;
        private static bool _built, _failed;

        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures() => M.KeepTextures(CashModel.WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();

        private static (byte[] mds, byte[] img) Files()
        {
            if (!_built && !_failed)
            {
                try
                {
                    byte[] chr = GameDataFiles.TryReadEntry(Pack) ?? throw new IOException(Pack + " not readable");
                    var pack = ChrPack.Parse(chr);
                    _mds = (pack.Find(Model) ?? throw new IOException(Pack + " lacks " + Model)).Payload;
                    _img = (pack.Find(Bank) ?? throw new IOException(Pack + " lacks " + Bank)).Payload;
                    _built = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"read: model {_mds.Length} B, texture {_img.Length} B");
                }
                catch (Exception e) { _failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "could not be read: " + e.Message); }
            }
            return _built ? (_mds, _img) : (null, null);
        }
    }
}
