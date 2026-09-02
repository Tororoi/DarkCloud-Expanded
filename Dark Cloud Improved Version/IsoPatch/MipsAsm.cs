namespace Dark_Cloud_Improved_Version
{
    /// <summary>MIPS (EE) instruction encoders + register numbers for the hand-built ELF caves.</summary>
    internal static class MipsAsm
    {
        internal const int zero = 0, v0 = 2, a0 = 4, a1 = 5, a2 = 6, a3 = 7, t0 = 8, sp = 29;
        internal static uint Lui(int rt, uint i) => 0x3C000000u | ((uint)rt << 16) | (i & 0xFFFF);
        internal static uint Ori(int rt, int rs, uint i) => 0x34000000u | ((uint)rs << 21) | ((uint)rt << 16) | (i & 0xFFFF);
        internal static uint Lw(int rt, int o, int b) => 0x8C000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        internal static uint Sw(int rt, int o, int b) => 0xAC000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        internal static uint Addiu(int rt, int rs, int i) => 0x24000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(i & 0xFFFF);
        internal static uint Move(int rd, int rs) => Ori(rd, rs, 0);
        internal static uint Jal(uint tgt) => 0x0C000000u | ((tgt >> 2) & 0x03FFFFFF);
        internal static uint J(uint tgt) => 0x08000000u | ((tgt >> 2) & 0x03FFFFFF);
    }
}
