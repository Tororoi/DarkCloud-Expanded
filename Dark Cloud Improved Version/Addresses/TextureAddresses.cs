namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The global CTextureManager (SearchTextureName 0x131180 / GetTexture 0x131290). ONE instance at main-BSS
    /// 0x1C75870 serves everything — the dungeon loaders and the menu weapon preview (EnterWeaponModel 0x20D4C0) both
    /// pass it explicitly. LoadTexture (0x154950) DMAs the GS upload FROM an entry's pixel and CLUT pointers, which is
    /// why writing them re-skins live.
    ///
    /// Blocks are the manager's VRAM groups (CTextureBlock at <see cref="Blocks"/>): ReloadTexture re-uploads an entry
    /// only if its VRAM address is at or below the block's dirty watermark (capped by the block's top) or the loaded
    /// flag is 0 — a block whose base and top are 0 uploads nothing. <see cref="Cursor"/> is the VRAM bump cursor and
    /// counts DOWN (Initialize sets it, EnterFixTexture subtracts from it at 0x132354); everything below it is free.
    /// </summary>
    internal static class TextureManager
    {
        internal const long Base        = 0x21C75870;
        internal const int  Count       = 0x00;     // last entry index
        internal const int  Cursor      = 0x14;
        internal const int  Blocks      = 0x18;
        internal const int  BlockStride = 0x3C;
        internal const int  BlockCount  = 0x48;
        internal const int  BlkBase = 0x20, BlkTop = 0x24, BlkLoaded = 0x28, BlkDirty = 0x30;
        internal const int  Entries     = 0x10F8;
        internal const int  EntryStride = 0x50;
        internal const int  MaxEntries  = 0xC4;     // the table holds 196 entries and no more
        internal const int  EntryBlock  = 0x00;     // u16: the block the entry belongs to
        internal const int  EntryName   = 0x08;     // inline, NUL-terminated
        internal const int  EntryPixels = 0x38;     // native pointer
        internal const int  EntryClut   = 0x48;     // native pointer to the palette copy
        internal const int  EntryTex0    = 0x28;    // u64 GS TEX0 register
        internal const uint Tex0AddrMask = 0x3FFF;  // its two VRAM addresses are 14-bit fields: TBP at bit 0, CBP at Tex0CbpShift
        internal const int  Tex0CbpShift = 37;
    }
}
