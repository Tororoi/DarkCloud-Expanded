namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// THE MOD'S CAVE MEMORY MAP — every region of EE RAM this mod claims, in one place.
    ///
    /// A "cave" here is any block of free EE RAM we own: hosted CODE (clean copies of engine functions,
    /// reached via a DATA-driven dispatch — never a direct j/jal, see docs/cave-code-execution.md), DATA
    /// the engine iterates for us (the clone's frame tree, meshes, cloth), and PNACH mailboxes (flags the
    /// mod writes and the PNACH conditionals read).
    ///
    /// Keep every cave address HERE and nowhere else. These regions are only safe because they were swept
    /// clean (tools/find_code_caves.py + CodeCaveScanner.cs → CodeCaveFindings.txt); two systems silently
    /// overlapping is the failure mode this file exists to prevent. Anything added here must also be
    /// reflected in CodeCaveScanner's ModReserved list so the sweeper doesn't flag our own writes.
    ///
    /// Addresses come in two forms:
    ///   • MMU  (0x21xxxxxx) — what Memory.Read*/Write* take.
    ///   • Guest(0x01xxxxxx) — what the GAME sees, i.e. what we bake into pointers/instructions.
    ///
    /// ── MAP (guest) ───────────────────────────────────────────────────────────────────────────────────
    ///   0x01F10000  PNACH mailbox — 4-byte flag slots, see <see cref="Mailbox"/> (0x3C is the next free)
    ///   0x01F10100  HarderEnemyAI per-species STB stubs           → <see cref="AiStubBase"/>
    ///               ...bounded at 0x01F30000 by Mirage's PtrTable → <see cref="AiStubMaxSlots"/>
    ///   0x01400000  EnemyModelInjector param/code block (BSS)     → <see cref="ModelInjectorCave"/>
    ///
    ///   Mirage — decoy aggro redirect:
    ///   0x01F30000  PtrTable      256 × 4B per-slot target pointers
    ///   0x01F30400  DecoyPos      16B (x,z,y,w) — the stationary decoy fooled slots point at
    ///   0x01F34000  DistCave      clean copy of _GET_DISTANCE (code)
    ///   0x01F34200  PosCave       clean copy of _GET_POSITION (code)
    ///
    ///   Mirage — clone (deep per-instance copy of the player; see docs/character-clone-footprints.md):
    ///   0x01F33000  ClothStub     16 zero bytes — an empty cloth list
    ///   0x01F33200  RootBuf       the clone's own root CFrame
    ///   0x01F40000  NodePool      128 × 0x270 CFrames        → ends 0x01F53800
    ///   0x01F54000  ClothListCave the 4-entry cloth-ptr array the clone's +0xC74 points at
    ///   0x01F54100  ClothObjCave  3 × 0x8550 CCloth objects  → ends 0x01F6D0F0
    ///   0x01F6E000  ClothBufCave  cloth draw buffers
    ///   0x01F73000  ClothAnchorCave out-of-tree anchor CFrames → ends 0x01F75000
    ///   0x01F75000  ClothBoundCave  copied CBound collision list → ends 0x01F78000
    ///   0x01F78000  MotionCave    motion channel copies
    ///   0x01F79000  FrameInfCave
    ///   0x01F7D000  BoneMtxCave / MeshScratchCave
    ///   0x01F7E200  WeaponCave    the equipped weapon's CFrame tree (0x1D00)
    ///   0x01F80000  MeshCave      software-skinned meshes (0x34000) → ends 0x01FB4000
    ///                             ^ top of the clean band (~0x01FB4300). Growing this needs a RELOCATION,
    ///                               not an extension — Goro/Ruby/Osmond don't fit today (0x57B30 for Goro).
    /// </summary>
    internal static class CodeCaves
    {
        // ── PNACH mailboxes ──────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// The 4-byte flag slots at 0x01F10000 that the mod WRITES and the PNACH conditionals READ.
        /// EVERY slot must be unique — two systems on one slot silently corrupt each other, and the symptom
        /// shows up in the *other* system (this is not hypothetical: a reserved-but-unimplemented fishing flag
        /// was sitting on Mirage's scene-gate slot; had it ever been wired up, writing 1 to boost the fishing
        /// radius would have read as "decoy up" and NOP'd the chara-loop gates with no clone present).
        ///
        ///   +0x00 eventpoint   +0x04 sun/moon    +0x08 nearNPC     +0x0C xiaoFlag
        ///   +0x10 nearNPC(2)   +0x14 insideMayor +0x18 element     +0x1C clock
        ///   +0x20 pnachActive  +0x24 PINE probe (MemoryFunctions)  +0x28 option1   +0x2C option2
        ///   +0x30 option3      +0x34 option4     +0x38 MIRAGE scene gate
        ///   +0x3C.. FREE
        /// </summary>
        internal static class Mailbox
        {
            internal const long Base = 0x21F10000;

            /// <summary>Mirage 3-state gate, read every frame by the PNACH: 1 = decoy up (NOP the chara-loop
            /// gates so the clone draws + steps), 2 = in a dungeon with no decoy (RESTORE the vanilla words —
            /// PNACH conditionals do NOT auto-revert), 3 = decoy up but PAUSED (drawn, frozen), 0 = town.
            /// Also gates the fire-raster tuning patches (sprite size / dist gate / distortion amplitude).</summary>
            internal const long MirageSceneGate = 0x21F10038;

            internal const long NextFree = 0x21F1003C;
        }

        /// <summary>Back-compat alias — prefer <see cref="Mailbox.MirageSceneGate"/>.</summary>
        internal const long MirageSceneGateFlag = Mailbox.MirageSceneGate;

        // ── HarderEnemyAI: per-species STB stubs ─────────────────────────────────────────────────────
        // One self-contained stub per spliced species (nothing is shareable — every stub contains
        // script-local CALL/branch targets). Starts just past the mailbox and grows upward, so it is
        // HARD-BOUNDED by Mirage's PtrTable at 0x1F30000: 0x1FF00 / 0x400 = 127 slots. Overrunning that
        // would silently scribble over the decoy aggro table and the clone's frame tree.
        internal const long AiStubBase     = 0x21F10100;
        internal const int  AiStubStride   = 0x400;
        internal const int  AiStubMaxSlots = (int)((PtrTable - AiStubBase) / AiStubStride);   // 127

        // ── EnemyModelInjector ───────────────────────────────────────────────────────────────────────
        /// <summary>Param + code block, deep inside a 3.4 MB contiguous zero block in main BSS
        /// (0x01340E20..0x016A0000). Guest address — the injected code loads it internally.</summary>
        internal const uint ModelInjectorCave = 0x01400000;

        // ── Mirage: decoy aggro redirect ─────────────────────────────────────────────────────────────
        internal const long PtrTable      = 0x21F30000;   // per-slot target POINTER table (entry = an address to read a position from)
        internal const uint PtrTableGuest = 0x01F30000;   // baked into the cave stubs as `lui a1, PtrTable>>16`
        internal const long DecoyPos      = 0x21F30400;   // the stationary decoy position (x,z,y,w)
        internal const uint DecoyPosGuest = 0x01F30400;   // written into fooled slots' pointer entries

        // Clean cold-copied engine functions, reached via the STB external-command dispatch table (a pure
        // DATA path). NOT reachable by a patched j/jal — that crashes the recompiler.
        internal const long DistCave      = 0x21F34000;   // _GET_DISTANCE copy
        internal const uint DistCaveGuest = 0x01F34000;
        internal const long PosCave       = 0x21F34200;   // _GET_POSITION copy
        internal const uint PosCaveGuest  = 0x01F34200;

        // ── Mirage: clone ────────────────────────────────────────────────────────────────────────────
        internal const long ClothStub      = 0x21F33000;  // 16 zero bytes = "no cloth"
        internal const long ClothStubGuest = 0x01F33000;
        internal const long RootBuf        = 0x21F33200;  // the clone's own root CFrame
        internal const long RootBufGuest   = 0x01F33200;

        internal const long NodePool      = 0x21F40000;   // clone frame-tree pool
        internal const long NodePoolGuest = 0x01F40000;
        internal const int  MaxNodes      = 128;          // 128 × 0x270 = 0x13800 → ends 0x21F53800 (max char is Osmond @84)

        internal const long ClothListCave  = 0x21F54000;
        internal const uint ClothListGuest = 0x01F54000;
        internal const long ClothObjCave   = 0x21F54100;  // 3 slots × 0x8550
        internal const uint ClothObjGuest  = 0x01F54100;
        internal const long ClothBufCave   = 0x21F6E000;
        internal const uint ClothBufGuest  = 0x01F6E000;

        internal const long ClothAnchorCave  = 0x21F73000;   // out-of-tree anchor CFrames (0x270 each)
        internal const uint ClothAnchorGuest = 0x01F73000;
        internal const long ClothAnchorEnd   = 0x21F75000;
        internal const long ClothBoundCave   = 0x21F75000;   // copied CBound list (0x130 each)
        internal const uint ClothBoundGuest  = 0x01F75000;
        internal const long ClothBoundEnd    = 0x21F78000;

        internal const long MotionCave      = 0x21F78000;
        internal const long MotionCaveGuest = 0x01F78000;
        internal const long FrameInfCave      = 0x21F79000;
        internal const long FrameInfCaveGuest = 0x01F79000;
        internal const long MeshScratchCave = 0x21F7D000;
        internal const long BoneMtxCave     = 0x21F7D000;

        internal const long WeaponCave      = 0x21F7E200;
        internal const long WeaponCaveGuest = 0x01F7E200;
        internal const int  WeaponCaveSize  = 0x1D00;

        internal const long MeshCave       = 0x21F80000;  // software-skinned meshes
        internal const long MeshCaveGuest  = 0x01F80000;
        internal const int  MeshCaveSize   = 0x34000;     // fits Toan/Xiao/Ungaga; NOT Goro/Ruby/Osmond
    }
}
