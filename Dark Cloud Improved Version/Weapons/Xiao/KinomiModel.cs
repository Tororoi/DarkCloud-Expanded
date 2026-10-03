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

        private static readonly CashModel M = CashModel.FromChrPack(Tag, CashKey, "kinomi", Pack, Model, Bank);

        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures() => M.KeepTextures(CashModel.WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();
    }
}
