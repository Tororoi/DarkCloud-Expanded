using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The STB command ids we use. Confirmed from the dispatch table — whose 8-byte entries are
    /// <c>{handler, id}</c>, NOT <c>{id, handler}</c>. Reading them the other way round shifts every command
    /// by one and turns <c>_LOAD_FISHING_DATA</c> into <c>_LOAD_MAIN_CHARA</c>.</summary>
    internal static class StbCommands
    {
        internal const int LoadFishingData = 998;   // (area, x1, z1, x2, z2, water, ground)
        internal const int GotoFishing     = 997;   // ()
        internal const int InitFish        = 996;   // (x1, z1, x2, z2)
        internal const int ExitFishing     = 995;   // ()
        internal const int SetFishingEsa   = 994;   // ()

        internal const int LoadMainChara  = 999;    // (chrPath, cfgName, flag) — swaps the player's model
        internal const int FadeIn        = 500;     // (frames) — 500 is FADE_IN, not FADE_OUT
        internal const int SetWorldCoord = 7;       // (x, y, z, rx, ry, rz)
        internal const int SetNpcMotion  = 133;     // (charaId, motionIdx) — charaId -1 = the player; motion 0 = idle
        internal const int SetNpcPos     = 137;     // (charaId, x, y, z)   charaId -1 = the player
        internal const int SetNpcRot     = 138;     // (charaId, rx, ry, rz)
        internal const int GetNpcPos     = 131;     // (charaId, &x, &y, &z) — reads position into locals (out-pointers)
        internal const int GetNpcRot     = 139;     // (charaId, &rx, &ry, &rz) — reads rotation into locals
        internal const int NpcDraw       = 140;     // (flag, charaId)

        // The bait model pipeline. _SET_FISHING_ESA only points the hook at ITEM FRAME 0 — it does not load
        // anything. The frame has to be built first, and _LOAD_ITEM_FILE is a BACKGROUND (async) read.
        internal const int LoadItemFile     = 49;   // (itemId) — starts an async load of the item's chr + img
        internal const int LoadItem         = 50;   // (0) — builds item frame 0 from the loaded files
        internal const int ClearEventBuff   = 39;   // ()
        internal const int ActiveFileBuffer = 44;   // (a, b)

        /// <summary>
        /// (&amp;out) — out = non-zero while ANY background disc read is still in flight.
        ///
        /// This is the load-complete poll, and it existed all along: <c>ReadBGSync</c> pumps the reader and
        /// scans <c>bg_read_info</c> for a slot that is queued but not yet complete. Non-blocking, so a script
        /// loops on it. Norune's mystery <c>call_func 400</c> is nothing more than
        /// <c>while (_LOAD_SYNC(&amp;v)) YIELD;</c>
        ///
        /// I previously concluded no such command existed, having grepped the command names for CHECK / READ /
        /// BG / WAIT / FILE — none of which match "_LOAD_SYNC".
        /// </summary>
        internal const int LoadSync = 34;

        internal const int FadeOut            = 501; // (frames) — 501 is FADE_OUT; 500 is FADE_IN
        internal const int ClearVillagerBuff  = 38;  // ()

        /// <summary>() — rewinds the villager buffer and reloads every NPC (and its textures) for the current
        /// map from disc. Reads its list from globals, no args. Used on fishing exit to un-garble whatever
        /// villager texture block the session's model/bait loads overwrote.</summary>
        internal const int LoadVillager = 57;

        /// <summary>(&amp;out) — out = non-zero while a fade is still in progress. Same shape as
        /// <see cref="LoadSync"/>: poll it in a YIELD loop instead of counting frames.</summary>
        internal const int CheckFade = 502;

        /// <summary>() — raises the "!" prompt for this frame. It is a PER-FRAME flag (EdEventInit clears it,
        /// the ladder code sets it the same way), so it has to be re-asserted every frame it should show.</summary>
        internal const int DrawExclamationMark = 10;

        /// <summary>(&amp;out) — out = the buttons pressed this frame (after exch_ok_cancel).</summary>
        internal const int GetPadDown = 1;

        /// <summary>
        /// X (Cross) AS A SCRIPT SEES IT — 0x20, not 0x40.
        ///
        /// <c>EdMoveChara</c> tests the raw pad with <c>PadDown(0x40)</c> for confirm, so 0x40 is Cross in
        /// engine code. But <c>_GET_PADDOWN</c> pipes the pad through <c>exch_ok_cancel</c> first, which
        /// SWAPS bits 0x20 and 0x40:
        ///
        /// <code>
        ///   v = pad &amp; ~0x60;
        ///   if (pad &amp; 0x20) v |= 0x40;
        ///   if (pad &amp; 0x40) v |= 0x20;
        /// </code>
        ///
        /// So a script testing 0x40 is testing CIRCLE. That is why the fishing prompt answered to Circle.
        /// </summary>
        internal const int PadCross = 0x20;

        /// <summary>(&amp;outVar) — opens the game's native bait menu (menu_mode 9) over a static bait list,
        /// and writes the chosen item id back through the pointer. The handler REFUSES unless arg1's stack
        /// type is 3 (a pointer), so it must be pushed with PushVarRef.</summary>
        internal const int GotoChangeEsa = 25;

        // ── Menu / select-cursor commands (entry & quit dialogs) ───────────────────────────────────────
        internal const int MesMake        = 192;  // (window, msgId) — draw a message; window 1 = event mes (our menu text)
        internal const int MesClose       = 193;  // (window)
        internal const int SetMesShippo   = 196;  // (window, flag) — speech-bubble tail
        internal const int SetMesDrawSpeed= 198;  // (window, speedFloat) — Norune's fishing menu sets 1.0
        internal const int SetMesPos      = 197;  // (window, posMode)  — Norune's fishing menu uses 9, then 0 to reset
        internal const int SetMesAutoset  = 195;  // (window, x1,y1,x2,y2) — auto-place the bubble to avoid the rect (Norune: 0,0,0,0)
        internal const int SetMesCursor   = 199;  // (window, line) — draw the selection cursor at 0-based line
        internal const int GetApad        = 903;  // (&lx, &ly[, &rx, &ry]) — analog stick floats; LY<-0.5 up, >0.5 down
        internal const int GotoFpChange   = 24;   // () — Exchange FP (menu_mode 8, engine-drawn)
        internal const int GotoFishRanking= 26;   // () — Fishing log (menu_mode 10, engine-drawn)
        internal const int SetReturnCode  = 3;    // (code) — 11 keeps the fishing session running
        internal const int SItemCheck     = 707;  // (itemId, &out) — out = inventory index (>=0) if owned, -1 if not (fishing rod = 185)
        internal const int FishingRodItem = 185;  // the fishing pole checked by the "Fish" option

        // _GET_PADDOWN result masks (post exch_ok_cancel). D-pad bits are unswapped; X arrives as 0x20.
        internal const int PadUp   = 0x1000;
        internal const int PadDown = 0x4000;
    }

    /// <summary>Emit STB VM bytecode: 12-byte instructions <c>{u32 op, u32 a1, u32 a2}</c>.</summary>
    internal sealed class StbWriter
    {
        private const int OpPush  = 3;
        private const int OpExt   = 21;
        private const int OpRet   = 15;
        private const int OpYield = 23;

        private const int TypeInt    = 1;
        private const int TypeFloat  = 2;
        private const int TypeString = 3;

        private readonly System.Collections.Generic.List<byte> _b = new System.Collections.Generic.List<byte>();
        private readonly System.Collections.Generic.List<(string Text, int PatchAt)> _strs =
            new System.Collections.Generic.List<(string, int)>();

        private const int OpVarValue = 1;   // push the VALUE of local var a1
        private const int OpVarRef   = 2;   // push a POINTER to local var a1 (stack type 3)

        /// <summary>
        /// Variable ADDRESSING MODE, and it lives in <c>a2</c> — not <c>a1</c>, which is the variable index.
        ///
        /// <c>exe()</c> case 1/2 switch on <c>a2</c>: 1 = direct (<c>vars[a1]</c>), and 2/4/8/0x10/0x20 are
        /// indirect/array forms that pop an index first. Emitting <c>a2 = 0</c> matches NOTHING, so the
        /// instruction pushes nothing at all — the stack then runs short, EXT reads garbage as the command
        /// id, and the VM derails. That is exactly what froze the game on the bait menu.
        /// </summary>
        private const int VarModeDirect = 1;
        private const int VarModeFloat  = 8;   // direct, but stamps the entry's type tag so floats reinterpret (see PushVarFloat)

        internal void PushInt(int v)     => Emit(OpPush, TypeInt, unchecked((uint)v));
        internal void PushFloat(float v) => Emit(OpPush, TypeFloat, BitConverter.ToUInt32(BitConverter.GetBytes(v), 0));

        /// <summary>Push local variable <paramref name="idx"/>'s value (INT-typed slot).</summary>
        internal void PushVar(int idx) => Emit(OpVarValue, (uint)idx, VarModeDirect);

        /// <summary>
        /// Push a FLOAT local's value / a pointer to a FLOAT local. Same slot address as the int forms, but a2
        /// = <see cref="VarModeFloat"/> (8) instead of 1. That difference matters ONLY for floats: on a value
        /// push, mode 8 stamps the pushed entry's TYPE TAG to non-zero, and <c>GetStackFloat</c> reads a slot
        /// as <c>type==0 ? (float)(int)bits : reinterpret&lt;float&gt;(bits)</c>. With mode 1 the tag stays 0, so
        /// a position float like 0x4309A6C0 gets read as <c>(float)1124760000 ≈ 1.12e9</c> — the player is
        /// flung off the map (black screen on quit). Norune tags every _GET/_SET_NPC_POS/ROT var this way.
        /// (Int vars are unaffected — GetStackInt reads the value word directly — which is why the menu/bait
        /// mode-1 vars are fine.)
        /// </summary>
        internal void PushVarFloat(int idx)    => Emit(OpVarValue, (uint)idx, VarModeFloat);
        internal void PushVarRefFloat(int idx) => Emit(OpVarRef,   (uint)idx, VarModeFloat);

        /// <summary>
        /// Push a POINTER to local variable <paramref name="idx"/> — an OUT parameter.
        ///
        /// This is how <c>_GOTO_CHANGE_ESA</c> hands back the bait you picked: its handler takes
        /// <c>p_use_item = arg1.value</c> (and refuses unless <c>arg1.type == 3</c>), opens the menu, and the
        /// menu writes the chosen item id through that pointer. So stack type 3 is "pointer", not "string" —
        /// a string push is just a pointer into the .stb, which is why <see cref="PushString"/> shares it.
        /// </summary>
        internal void PushVarRef(int idx) => Emit(OpVarRef, (uint)idx, VarModeDirect);

        /// <summary>Highest local variable index used, or -1. A label's header declares how many locals it
        /// has (header slot 0's op field), and the VM reserves that many.</summary>
        internal int Locals { get; private set; }

        internal void UseLocals(int n) { if (n > Locals) Locals = n; }

        /// <summary>Byte offset of the next instruction — used to find an operand again so the mod can patch
        /// it live (see the exit script's position, which is rewritten every frame while fishing).</summary>
        internal int Offset => _b.Count;

        /// <summary>
        /// Push a string. The operand is NOT a file offset — it is an offset relative to the script's CODE
        /// BASE (the u32 at header +0x08). Norune's model swap reads `a2 = 0xED18` and the string really
        /// lives at file 0xEE00, and 0xEE00 - 0xED18 = 0xE8, which is exactly its codeBase. That also matches
        /// <c>load__10CRunScript</c>, which caches <c>base + *(base + 8)</c>.
        ///
        /// The offset cannot be known until the string is placed, so this emits a placeholder and remembers
        /// where to patch it. Call <see cref="EmitStrings"/> once the layout is decided.
        /// </summary>
        internal void PushString(string text)
        {
            _strs.Add((text, _b.Count + 8));   // the a2 field of the instruction we are about to emit
            Emit(OpPush, TypeString, 0);
        }

        /// <summary>
        /// Lay the pushed strings out at <paramref name="blobOffset"/> (an offset within the .stb buffer),
        /// patch every placeholder, and return the bytes to write there. Call AFTER the bytecode is complete;
        /// <see cref="ToArray"/> then returns the patched code.
        /// </summary>
        internal byte[] EmitStrings(int blobOffset, int codeBase)
        {
            var blob = new System.Collections.Generic.List<byte>();
            foreach (var (text, patchAt) in _strs)
            {
                byte[] a2 = BitConverter.GetBytes(blobOffset + blob.Count - codeBase);
                for (int i = 0; i < 4; i++) _b[patchAt + i] = a2[i];
                blob.AddRange(System.Text.Encoding.ASCII.GetBytes(text));
                blob.Add(0);
            }
            return blob.ToArray();
        }

        internal bool HasStrings => _strs.Count > 0;

        /// <summary>Bytes the string blob will occupy (each string is NUL-terminated). Needed to size an
        /// allocation BEFORE the layout is decided.</summary>
        internal int StringBytes
        {
            get
            {
                int n = 0;
                foreach (var (text, _) in _strs) n += text.Length + 1;
                return n;
            }
        }

        /// <summary>Call. <paramref name="stackEntries"/> counts the command id as well as the arguments.</summary>
        internal void Ext(int stackEntries) => Emit(OpExt, (uint)stackEntries, 0);

        internal void Ret() => Emit(OpRet, 0, 0);

        private const int OpCallFunc = 19;

        /// <summary>
        /// Number of arguments this script is CALLED with (funcdata fd[3]). CALL_FUNC frames the callee as
        /// <c>varsBase = sp - args*8</c>, so the caller's pushed args become the callee's first locals. 0 for a
        /// normal event label; the shared menu subroutine declares 2 (msgId, count). WriteScript writes it.
        /// </summary>
        internal int ArgCount { get; private set; }

        /// <summary>Declare this as a callable subroutine taking <paramref name="n"/> stack arguments (which
        /// occupy locals 0..n-1). Also reserves them as locals.</summary>
        internal void SetArgs(int n) { ArgCount = n; UseLocals(n); }

        /// <summary>
        /// CALL_FUNC (op 19): call the funcdata at <paramref name="cbRelFuncOff"/> — a codeBase-relative FILE
        /// offset. Args are pushed by the caller first (they become the callee's locals 0..args-1); a return
        /// value the callee pushes before RET is left on the stack. a1 is unused (0 in every vanilla call).
        /// The offset is an absolute layout value known at emit time, so — unlike jumps/strings — it needs no
        /// post-placement fixup.
        /// </summary>
        internal void CallFunc(int cbRelFuncOff) => Emit(OpCallFunc, 0, unchecked((uint)cbRelFuncOff));

        /// <summary>Suspend until the next frame. A script that never yields is run to completion inside
        /// <c>EdEventInit</c> and demoted to a "simple event" — it never becomes a real event, so its return
        /// code is never acted on. See <c>BuildFishingBytecode</c>.</summary>
        internal void Yield() => Emit(OpYield, 0, 0);

        private const int OpJmp     = 16;   // pc = codeBase + a1
        private const int OpBrFalse = 17;   // pops; branches if false
        private const int OpBrTrue  = 18;   // pops; branches if true

        private readonly System.Collections.Generic.List<int> _marks = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<(int PatchAt, int Mark)> _jumps =
            new System.Collections.Generic.List<(int, int)>();

        /// <summary>Remember this spot as a jump target. Like strings, a jump's operand is an offset from the
        /// script's CODE BASE, so it cannot be resolved until we know where the script will be written.</summary>
        internal int Mark()
        {
            _marks.Add(_b.Count);
            return _marks.Count - 1;
        }

        /// <summary>Reserve a mark for a spot not emitted yet (a forward branch); fix it with
        /// <see cref="PlaceMark"/>.</summary>
        internal int MarkForward()
        {
            _marks.Add(-1);
            return _marks.Count - 1;
        }

        internal void PlaceMark(int mark) => _marks[mark] = _b.Count;

        private const int OpAnd = 24;

        /// <summary>Pop two, push (a &amp; b). Used to test a single button out of the pad bitmask.</summary>
        internal void And() => Emit(OpAnd, 0, 0);

        internal void Jmp(int mark)     => EmitJump(OpJmp, mark);
        internal void BrTrue(int mark)  => EmitJump(OpBrTrue, mark);
        internal void BrFalse(int mark) => EmitJump(OpBrFalse, mark);

        private void EmitJump(int op, int mark)
        {
            _jumps.Add((_b.Count + 4, mark));      // a1 is the operand for jumps, at instruction + 4
            Emit(op, 0, 0);
        }

        /// <summary>Resolve jump targets once the script's position is known.</summary>
        internal void EmitJumps(int scriptOffset, int codeBase)
        {
            foreach (var (patchAt, mark) in _jumps)
            {
                byte[] a1 = BitConverter.GetBytes(scriptOffset + _marks[mark] - codeBase);
                for (int i = 0; i < 4; i++) _b[patchAt + i] = a1[i];
            }
        }

        // Arithmetic / comparison / assignment primitives — verified against the interpreter's opcode switch
        // (exe__10CRunScript 0x23E080). See docs/stb-script-format.md.
        private const int OpStore = 5;    // pop value then ref; *ref = value; PUSHES value back (needs a Pop)
        private const int OpAdd   = 6;
        private const int OpSub   = 7;
        private const int OpPop   = 4;
        private const int OpCmp   = 14;   // a1 = comparator: 0x28== 0x29!= 0x2A< 0x2B<= 0x2C> 0x2D>=

        internal const int CmpEq = 0x28, CmpNe = 0x29, CmpLt = 0x2A, CmpLe = 0x2B, CmpGt = 0x2C, CmpGe = 0x2D;

        /// <summary>Discard the top stack value.</summary>
        internal void Pop() => Emit(OpPop, 0, 0);
        internal void Add() => Emit(OpAdd, 0, 0);
        internal void Sub() => Emit(OpSub, 0, 0);

        /// <summary>Compare: pops two, pushes bool. <paramref name="cmp"/> is one of the <c>Cmp*</c> constants.
        /// The test is <c>(first OP second)</c> — the operand pushed FIRST is the left side. So <c>var &lt; n</c>
        /// is PushVar(var); PushInt(n); Cmp(CmpLt). (Verified against exe__10CRunScript case 0xE.)</summary>
        internal void Cmp(int cmp) => Emit(OpCmp, (uint)cmp, 0);

        /// <summary>Assign <c>var[idx] = &lt;expression already on the stack&gt;</c>. Emits the ref, expects the
        /// value to have been pushed by the caller BEFORE calling — no: emits nothing but the store, so the
        /// order is PushVarRef(idx); &lt;push value&gt;; Store(). Store re-pushes the value, so a statement adds Pop().</summary>
        internal void Store() { Emit(OpStore, 0, 0); }

        private void Emit(int op, uint a1, uint a2)
        {
            _b.AddRange(BitConverter.GetBytes(op));
            _b.AddRange(BitConverter.GetBytes(a1));
            _b.AddRange(BitConverter.GetBytes(a2));
        }

        internal byte[] ToArray() => _b.ToArray();
    }
}
