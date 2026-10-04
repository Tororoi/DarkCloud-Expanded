namespace Dark_Cloud_Improved_Version
{
    /// <summary>MIPS (EE) instruction encoders + register numbers for the hand-built ELF caves. Integer, branch and the few cop1
    /// (FPU) forms the caves use; the FPU encodings are verified against EdDrawSysCursor's own listing: lwc1 f1,0(v0) = 0xC4410000,
    /// swc1 f0,0x94(sp) = 0xE7A00094, add.S f0,f0,f1 = 0x46010000.</summary>
    internal static class MipsAsm
    {
        internal const int zero = 0, at = 1, v0 = 2, a0 = 4, a1 = 5, a2 = 6, a3 = 7, t0 = 8, s0 = 16, s2 = 18, sp = 29, s8 = 30, ra = 31;
        internal const int f0 = 0, f2 = 2;                                                   // FPR numbers
        internal static uint Lui(int rt, uint i) => 0x3C000000u | ((uint)rt << 16) | (i & 0xFFFF);
        internal static uint Ori(int rt, int rs, uint i) => 0x34000000u | ((uint)rs << 21) | ((uint)rt << 16) | (i & 0xFFFF);
        internal static uint Lw(int rt, int o, int b) => 0x8C000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        internal static uint Sw(int rt, int o, int b) => 0xAC000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        internal static uint Addiu(int rt, int rs, int i) => 0x24000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(i & 0xFFFF);
        internal static uint Move(int rd, int rs) => Ori(rd, rs, 0);
        internal static uint Bne(int rs, int rt, int off) => 0x14000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(off & 0xFFFF);
        internal static uint Jr(int rs) => ((uint)rs << 21) | 0x08u;
        internal static uint Jal(uint tgt) => 0x0C000000u | ((tgt >> 2) & 0x03FFFFFF);
        internal static uint J(uint tgt) => 0x08000000u | ((tgt >> 2) & 0x03FFFFFF);
        internal static uint Lwc1(int ft, int off, int b) => 0xC4000000u | ((uint)b << 21) | ((uint)ft << 16) | (uint)(off & 0xFFFF);
        internal static uint Swc1(int ft, int off, int b) => 0xE4000000u | ((uint)b << 21) | ((uint)ft << 16) | (uint)(off & 0xFFFF);
        internal static uint AddS(int fd, int fs, int ft) => 0x46000000u | ((uint)ft << 16) | ((uint)fs << 11) | ((uint)fd << 6);
    }
}
