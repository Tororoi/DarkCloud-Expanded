namespace Dark_Cloud_Improved_Version
{
    /// <summary>The ELF boot cave that registers fishsign.img's bank into the system texture manager at boot: the engine entry
    /// points it calls, the loader site it detours and rejoins, where the cave, its file-name string and its diagnostic word live,
    /// and the pak the texture is prepended to. Written by ElfPatches.BuildCave / ElfPatchAndCrc and IsoPatcher.ApplySignPatch.</summary>
    internal static class BootCave
    {
        internal const string TexturePak = "meswin/mes_tex.pak";   // the boot-loaded pak fishsign.img is prepended to
        internal const uint GetPackFile = 0x0013F720, EnterIMGFile = 0x00132BA0, LoadFile = 0x0013F360;
        internal const uint SysTexMgr = 0x01C75870;                // the texture manager EnterIMGFile(-1) registers the bank into
        internal const uint DetourVa = 0x00180D7C, RejoinVa = 0x00180D84;   // the boot loader's `jal LoadFile; nop` the cave replaces, and where it returns
        internal const uint CaveAddr = 0x002A2314, StringAddr = 0x002452B8, DiagAddr = 0x01F80000;
        internal const int  MaxBytes = 0x6C;
    }
}
