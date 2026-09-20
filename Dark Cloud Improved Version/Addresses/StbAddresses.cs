namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// STB behavior-script file format and VM constants. RE'd 2026-06-19 via exe__10CRunScript @0x23E080.
    ///
    /// Header (all 32-bit little-endian):
    ///   +0x00  magic "STB\0" (0x00425453) — validate before patching
    ///   +0x04  total code-section size in bytes
    ///   +0x08  byte offset of the code section from the header base (read with <see cref="CodeSectionOff"/>)
    ///   +0x0C  byte offset of the label table (8-byte entries: labelId@+0, codeOff@+4)
    ///   +0x10  number of label-table entries
    ///
    /// Every instruction is a fixed-size 12-byte record (vmcode_t): op@+0, operandA@+4, operandB@+8.
    /// Dispatch table at 0x29FB80 (31 ops). See <see cref="Op*"/> constants for the opcodes used in
    /// enemy scripts. External commands (op21) are dispatched via a per-program funcdata table; the
    /// funcId is pushed as an int literal immediately before the op21 instruction. See <see cref="Fn*"/>.
    /// </summary>
    internal static class StbVm
    {
        // RAM window (PS2-native) that loaded .stb scripts land in — the bound for companion-locate / pattern scans.
        internal const long ScanLo = 0x01000000;
        internal const long ScanHi = 0x01A00000;

        // ── Header ───────────────────────────────────────────────────────────
        internal const int Magic          = 0x00425453; // "STB\0" — first word; validate before patching
        internal const int CodeSectionOff = 0x08;       // header field: byte offset of the code section
        internal const int LabelTableOff  = 0x0C;       // header field: byte offset of the label table
        internal const int LabelCount     = 0x10;       // header field: number of label-table entries

        // ── Instruction layout ────────────────────────────────────────────
        internal const int InstrSize = 0x0C;    // 12 bytes per vmcode_t
        internal const int OperandA  = 0x04;    // byte offset of operandA within a vmcode_t (type for op3; variable index for op1)
        internal const int OperandB  = 0x08;    // byte offset of operandB within a vmcode_t (value for op3 int-literal; scope for op1; callee offset for call_func)

        // ── Opcodes (op field) ────────────────────────────────────────────
        internal const int OpPush1       = 1;  // push-VARIABLE: operandA = variable index, operandB = scope (ScopeLocal = arg/local)
        internal const int OpPush2       = 2;  // push-VARIABLE: float variant (same layout)
        internal const int OpPush3       = 3;  // push-LITERAL:  operandA = type (TypeInt/Float/String), operandB = value
        internal const int OpCallFunc    = 19; // call_func: jump to sub-program; operandB = code offset of callee
        internal const int OpCallFuncCond = 27; // call_func conditional variant (same layout)
        internal const int OpExt         = 21; // external-command call; operandB must be 0; first pushed arg = funcId

        // ── Operand type/scope qualifiers ─────────────────────────────────
        internal const int TypeInt    = 1; // operandA of OpPush3: int32 literal (operandB = the value)
        internal const int TypeFloat  = 2; // operandA of OpPush3: float literal (operandB = IEEE-754 bits)
        internal const int ScopeLocal = 1; // operandB of OpPush1: scope 1 = local / call argument (as opposed to global)

        // ── External command function IDs (as pushed in the STB as funcId to OpExt) ─────────────
        // These are the per-program funcIds observed in monster STBs, one step before the global
        // dispatch table (0x2917C8). _SET_SHOT and _SET_SHOT2 entries: argc==6 → explicit damage arg
        // (5th pushed value); argc!=6 → "default" shot (damage comes from BehaviorScriptTable +0x3C).
        internal const int FnSetShot      = 133; // _SET_SHOT   with explicit damage (argc==6)
        internal const int FnSetShotReg   = 135; // _SET_SHOT   registry entry (not observed in any monster STB)
        internal const int FnSetShot2     = 229; // _SET_SHOT2 / second-shot with explicit damage (argc==6)
    }

    /// <summary>
    /// ENEMY TARGETING — the vanilla path by which every enemy learns where its target is, and the single
    /// lever for redirecting it. All addresses here are VANILLA engine facts; nothing mod-specific.
    ///
    /// Enemy AI never stores the player's position: each frame it asks for it through two STB external
    /// commands (op21, dispatched via the funcdata table @<see cref="StbExternCmd.DispatchTable"/>):
    ///   • _GET_POSITION — where is my target?
    ///   • _GET_DISTANCE — how far away is it?
    /// Both load <see cref="PlayerPosGuest"/> — the live player global — with a HARDCODED address, then
    /// sceVu0CopyVector it out. So the player's position enters enemy AI at exactly two instructions.
    ///
    /// Because the dispatch entries are function POINTERS the game dereferences, hosting a modified copy of
    /// either function and repointing its slot redirects targeting for ALL enemies with a pure data write —
    /// no in-place code surgery (see CodeCaveFunctions). Splice at the player-load offsets below; the copy's
    /// own `jal sceVu0CopyVector` is left intact and is where a helper jumps back to.
    ///
    /// Mirage's decoy is the first consumer (it makes the load per-enemy indirect), but nothing here is
    /// Mirage's — any feature that wants to lie to enemies about where the player is starts from these.
    /// </summary>
    internal static class StbExternCmd
    {
        internal const long DispatchTable = 0x202917C8;  // funcdata table: 8-byte {funcPtr, id} entries
        internal const int  EntryStride   = 8;

        internal const long GetPositionSlot = 0x202918A8;  // _GET_POSITION funcPtr slot
        internal const long GetDistanceSlot = 0x202918A0;  // _GET_DISTANCE funcPtr slot
        internal const long GetPositionFn   = 0x201E1DF0;  // _GET_POSITION (ELF 0x1E1DF0)
        internal const long GetDistanceFn   = 0x201E1D00;  // _GET_DISTANCE (ELF 0x1E1D00)

        /// <summary>The live player-position global both commands read (16 bytes: x, z/height, y, w).</summary>
        internal const uint PlayerPosGuest  = 0x01EA1D30;

        // Where, inside each function, the hardcoded player-address load sits — and the sceVu0CopyVector jal
        // that consumes it. These are the splice points for anyone hosting a modified copy.
        internal const int  PosPlayerLdOff  = 0x84;
        internal const int  PosCopyJalOff   = 0x8C;
        internal const int  DistPlayerLdOff = 0x5C;
        internal const int  DistCopyJalOff  = 0x64;

        // Sanity words — assert these before cloning, or you'll copy someone's stale in-place patch.
        internal const uint VanillaPrologue = 0x27BDFFB0;  // addiu sp,-0x50
        internal const uint VanillaPlayerLd = 0x3C0201EA;  // lui v0,0x1ea  (the player-addr load itself)
    }

    /// <summary>
    /// CRunScript — the per-enemy-slot STB-VM state, a sub-array inside CMainMonstorUnit at
    /// Base + 0x54DD0 (PCSX2 0x21E4D5A0), stride 0x48. One entry per FloorSlots/CCharacter slot index.
    /// (Listed in EnemyModelInjector._slotBlocks as the third per-slot block.)
    ///
    /// ★ RUNTIME STB LOCATION (confirmed 2026-06-19, live RE): <see cref="StbPtr"/> (+0x3C) holds the NATIVE
    /// base address of the exact .stb behaviour script this slot's VM is executing — i.e. the way to find any
    /// enemy's loaded STB in RAM with no scan and no roster math. This supersedes BossScriptPatcher's
    /// KnownAddrs table + signature scan (which can also locate a STALE/duplicate copy — e.g. Minotaur Joe
    /// loads twice, at 0x011A8990 and 0x01698CC0; CRunScript+0x3C names the live one, 0x011A8990).
    /// Validate a read pointer by checking the STB magic 0x00425453 at +0x00 and the label-1 codeOffset at
    /// +0x54. Usage: stbBase = Memory.ReadInt(CRunScript.StbPtrAddr(slot)); patch at (stbBase | 0x20000000).
    ///
    /// Other observed fields: +0x2C = current instruction pointer (into the running script),
    /// +0x40 = code base (script base + label-1 code offset). See memory enemy-stat-normalization / stb-vm-cracked.
    /// </summary>
    internal static class CRunScript
    {
        internal const long Base   = EnemyAddresses.MainMonstorUnit.Base + 0x54DD0; // 0x21E4D5A0
        internal const int  Stride = 0x48;
        internal const int  CurIp  = 0x2C; // current instruction pointer (native)
        internal const int  StbPtr = 0x3C; // ★ native base of the STB this slot executes
        internal const int  CodeBase = 0x40; // script base + codeOffset

        internal static long SlotAddr(int slot, int fieldOffset) => Base + (long)slot * Stride + fieldOffset;
        /// <summary>EE address of the STB-base pointer field for <paramref name="slot"/>.</summary>
        internal static long StbPtrAddr(int slot) => SlotAddr(slot, StbPtr);
    }
}
