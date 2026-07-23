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
    /// Everything below 0x01FB4300 lives in ONE proven-clean heap tail (CodeCaveScanner, 68 sessions).
    /// It is now packed so that the clone's mesh cave fits the LARGEST character (Goro), which makes all six
    /// clonable. The room came from three places: capping the AI stubs at 32 slots, trimming the node pool
    /// 128→96 bones (Osmond's 84 is the real max), and packing the decoy tables out of a 0x10000 hole.
    ///
    ///   0x01F10000  PNACH mailbox — 4-byte flag slots, see <see cref="Mailbox"/> (0x3C is the next free)
    ///   0x01F10100  AI stubs      32 × 0x400                       → ends 0x01F18100
    ///
    ///   Mirage — decoy aggro redirect:
    ///   0x01F19000  PtrTable      256 × 4B per-slot target pointers
    ///   0x01F19400  DecoyPos      16B (x,z,y,w) — what fooled enemies chase
    ///   0x01F1A000  DistCave      clean copy of _GET_DISTANCE (code)
    ///   0x01F1A400  PosCave       clean copy of _GET_POSITION (code)
    ///
    ///   CharacterClone (see docs/character-clone-footprints.md):
    ///   0x01F19500  ClothStub     16 zero bytes — an empty cloth list
    ///   0x01F19600  RootBuf       the clone's own root CFrame
    ///   0x01F1B000  NodePool      96 × 0x270 CFrames               → ends 0x01F29A00
    ///   0x01F2A000  ClothListCave the 4-entry cloth-ptr array (+0xC74 points here)
    ///   0x01F2A100  ClothObjCave  3 × 0x8550 CCloth               → ends 0x01F430F0
    ///   0x01F44000  ClothBufCave  cloth draw buffers
    ///   0x01F49000  ClothAnchorCave out-of-tree anchor CFrames     → ends 0x01F4B000
    ///   0x01F4B000  ClothBoundCave  copied CBound capsules         → ends 0x01F4E000
    ///   0x01F4E000  MotionCave    8 motion channels
    ///   0x01F4E600  FrameInfCave  per-bone skinning matrices (97 bones)
    ///   0x01F53600  BoneMtxCave   per-bone animation matrices (99 bones)
    ///   0x01F54F00  WeaponCave    the equipped weapon's CFrame tree
    ///   0x01F56400  MeshCave      software-skinned meshes (0x58000) → ends 0x01FAE400
    ///                             ^ 0x5F00 clear of the band top (0x01FB4300)
    ///
    ///   0x01400000  EnemyModelInjector param/code block — a SEPARATE region, deep in main BSS.
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
            // EVERY slot is declared here, deliberately. The 13 that were "documented" in the header comment but
            // never given a constant were being written as RAW LITERALS across five feature files — which is
            // exactly how a fishing flag once ended up squatting on Mirage's scene gate. A map that only exists
            // as prose does not prevent collisions; a constant does. Claim the next slot by taking NextFree and
            // moving it, and never write a bare 0x21F100xx anywhere else.
            internal const long Base = 0x21F10000;

            internal const long EventPoint   = Base + 0x00; // TownCharacter
            internal const long SunMoon      = Base + 0x04; // TownCharacter
            internal const long NearNpc      = Base + 0x08; // TownCharacter
            internal const long XiaoFlag     = Base + 0x0C; // TownCharacter
            internal const long NearNpc2     = Base + 0x10; // TownCharacter
            internal const long InsideMayor  = Base + 0x14; // TownCharacter
            internal const long Element      = Base + 0x18; // Dayuppy
            internal const long Clock        = Base + 0x1C; // TownCharacter
            internal const long PnachActive  = Base + 0x20; // MainMenuThread
            internal const long PineProbe    = Base + 0x24; // MemoryFunctions / MainMenuThread / ModWindow
            internal const long Option1      = Base + 0x28; // ModWindow
            internal const long Option2      = Base + 0x2C; // ModWindow
            internal const long Option3      = Base + 0x30; // ModWindow
            internal const long Option4      = Base + 0x34; // ModWindow

            /// <summary>Mirage 3-state gate, read every frame by the PNACH: 1 = decoy up (NOP the chara-loop
            /// gates so the clone draws + steps), 2 = in a dungeon with no decoy (RESTORE the vanilla words —
            /// PNACH conditionals do NOT auto-revert), 3 = decoy up but PAUSED (drawn, frozen), 0 = town.
            /// Also gates the fire-raster tuning patches (sprite size / dist gate / distortion amplitude).</summary>
            internal const long MirageSceneGate = Base + 0x38;

            /// <summary>The next unclaimed slot. Take it, then MOVE THIS — the whole point of the map.</summary>
            internal const long NextFree = Base + 0x3C;
        }

        /// <summary>Back-compat alias — prefer <see cref="Mailbox.MirageSceneGate"/>.</summary>
        internal const long MirageSceneGateFlag = Mailbox.MirageSceneGate;

        // ── HarderEnemyAI: per-species STB stubs ─────────────────────────────────────────────────────
        // One self-contained stub per SPLICED SPECIES on a floor (not per live enemy — nothing is shareable,
        // every stub embeds script-local CALL/branch targets). A floor has a handful of species, so 32 is
        // ample; the cap exists so this can never grow into the clone caves that follow it.
        internal const long AiStubBase     = 0x21F10100;
        internal const int  AiStubStride   = 0x400;
        internal const int  AiStubMaxSlots = 32;          // 32 × 0x400 = 0x8000 → ends 0x21F18100, clear of PtrTable

        // ── Mirage: decoy aggro redirect ─────────────────────────────────────────────────────────────
        // A cave's CAPACITY lives with the cave, deliberately. Divorcing "how big is it" from "how much do we
        // put in it" is how you get a silent overrun, and this band has already produced two: HarderEnemyAI's
        // stubs growing unbounded toward PtrTable, and the clone's per-bone buffers (sized for Ungaga's 67
        // bones) being overrun by Xiao's 79 — which scribbled over the grafted weapon's root CFrame.
        internal const long PtrTable      = 0x21F19000;   // per-slot target POINTER table (entry = an address to read a position from)
        internal const uint PtrTableGuest = 0x01F19000;   // baked into the cave stubs as `lui a1, PtrTable>>16`
        internal const int  PtrStride     = 4;            // one pointer per enemy slot
        internal const int  TableSlots    = 256;          // 256 × 4 = 0x400
        internal static long PtrAddr(int slot) => PtrTable + (long)slot * PtrStride;

        internal const long DecoyPos      = 0x21F19400;   // the stationary decoy position (x,z,y,w)
        internal const uint DecoyPosGuest = 0x01F19400;   // written into fooled slots' pointer entries

        // Clean cold-copied engine functions, reached via the STB external-command dispatch table (a pure
        // DATA path). NOT reachable by a patched j/jal — that crashes the recompiler.
        internal const long DistCave      = 0x21F1A000;   // _GET_DISTANCE copy (fn 0xF0 + helper @ +0x100)
        internal const uint DistCaveGuest = 0x01F1A000;
        internal const long PosCave       = 0x21F1A400;   // _GET_POSITION copy
        internal const uint PosCaveGuest  = 0x01F1A400;

        // ── Mirage: clone ────────────────────────────────────────────────────────────────────────────
        internal const long ClothStub      = 0x21F19500;  // 16 zero bytes = "no cloth"
        internal const long ClothStubGuest = 0x01F19500;
        internal const long RootBuf        = 0x21F19600;  // the clone's own root CFrame
        internal const long RootBufGuest   = 0x01F19600;

        internal const long NodePool      = 0x21F1B000;   // clone frame-tree pool
        internal const long NodePoolGuest = 0x01F1B000;
        internal const int  MaxNodes      = 96;           // 96 × 0x270 = 0xEA00 → ends 0x21F29A00. Osmond (84) is the
                                                          // largest real character; 96 leaves headroom.

        internal const long ClothListCave  = 0x21F2A000;
        internal const uint ClothListGuest = 0x01F2A000;
        internal const long ClothObjCave   = 0x21F2A100;
        internal const uint ClothObjGuest  = 0x01F2A100;
        /// <summary>CCloth slots this cave holds. Sized for the WORST CASE across all six characters — Toan, at
        /// 3 (Ungaga has 2). Per-character footprints: docs/character-clone-footprints.md.
        /// Capacity: 3 × CCloth(0x8550) = 0x18FF0 → ends 0x21F430F0, safely BELOW ClothBufCave @0x21F44000.</summary>
        internal const int  ClothObjSlots  = 3;

        /// <summary>Cloth draw buffers. The size is declared HERE, next to the address — CharacterClone used to
        /// back-compute it as (ClothAnchorCave − ClothBufCave), i.e. "how big is it" lived somewhere else, which
        /// is the exact split this file exists to prevent.</summary>
        internal const long ClothBufCave   = 0x21F44000;
        internal const int  ClothBufSize   = 0x5000;       // → ends 0x21F49000 = ClothAnchorCave
        internal const uint ClothBufGuest  = 0x01F44000;

        internal const long ClothAnchorCave  = 0x21F49000;   // out-of-tree anchor CFrames (0x270 each)
        internal const uint ClothAnchorGuest = 0x01F49000;
        internal const long ClothAnchorEnd   = 0x21F4B000;
        internal const long ClothBoundCave   = 0x21F4B000;   // copied CBound list (0x130 each)
        internal const uint ClothBoundGuest  = 0x01F4B000;
        internal const long ClothBoundEnd    = 0x21F4E000;

        // ── The clone's PER-BONE buffers — sized by NODE COUNT, so they must fit the LARGEST character ──
        // Three of these scale with bone count and sit immediately before their neighbours, so sizing them
        // against ONE character silently overruns the next cave along (Xiao's 79 bones did exactly that to the
        // weapon tree). Each carries an explicit size, and CharacterClone REFUSES to spawn past MaxCloneNodes.
        internal const long MotionCave      = 0x21F4E000;
        internal const long MotionCaveGuest = 0x01F4E000;
        internal const int  MotionCaveSize  = 0x0600;    // CCharacter.MotionSlots(8) × MotionStructSize(0xC0)

        internal const long FrameInfCave      = 0x21F4E600;
        internal const long FrameInfCaveGuest = 0x01F4E600;
        internal const int  FrameInfCaveSize  = 0x5000;  // (bones+1) × 0xD0 → holds 97 bones

        internal const long BoneMtxCave     = 0x21F53600;
        internal const int  BoneMtxCaveSize = 0x1900;    // (bones+1) × 0x40 → holds 99 bones

        internal const long WeaponCave      = 0x21F54F00;
        internal const long WeaponCaveGuest = 0x01F54F00;
        internal const int  WeaponCaveSize  = 0x1400;    // 0x270/node → 8 nodes (Ungaga's tree is 5, Xiao's 7)

        /// <summary>Max bones a clone may have — covers every character (largest is Osmond at 84) and matches
        /// the node pool. CharacterClone bounds-checks against this before writing a byte.</summary>
        internal const int MaxCloneNodes = MaxNodes;

        // ── EnemyModelInjector: NO CAVE. ────────────────────────────────────────────────────────────
        // It used to claim 0x01400000 in main BSS, "verified" only by eyeballing a zero block — it PREDATES the
        // code-cave scanner and was never swept by it. Rather than launder that into CodeCaveScanner.ModReserved
        // (which would have made the sweeper treat the region as ours and stop telling us the truth about it),
        // the cave is REMOVED. The feature is dormant (EnemyModelInjector.Enabled == false) and must be given a
        // scanner-verified cave from this file before it is ever switched on.

        /// <summary>Software-skinned meshes. Sized for the WORST CASE character — GORO at 0x57B30 — so ALL SIX
        /// are clonable. (It was 0x34000 and excluded Goro/Ruby/Osmond; the room came from capping the AI stubs
        /// at 32 slots, trimming the node pool 128→96, and packing the decoy tables.)</summary>
        internal const long MeshCave       = 0x21F56400;
        internal const long MeshCaveGuest  = 0x01F56400;
        internal const int  MeshCaveSize   = 0x58000;    // → ends 0x21FAE400, 0x5F00 clear of the band top (0x1FB4300)

        // ── Fishing: catch-message template ────────────────────────────────────────────────────────────
        // A tiny 1-entry meswin buffer holding the fishing CATCH template (message id 2000), swapped into the
        // town talk ClsMes (0x21D1B550 +0x17A0) ONLY while a fishing session is open. Custom fishing towns lack
        // msg 2000 in their own talk mes, so the vanilla catch bubble renders EMPTY; this supplies it, and the
        // engine's own flow (fish name from the global system14e.bin, numbers from the value array) fills it in.
        // Sits in the MeshCave safety margin — MeshCave is FIXED-size, so nothing grows into this. ~138 bytes.
        internal const long FishingCatchMes      = 0x21FB4000;   // ends ~0x21FB4090, below band top 0x21FB4300
        internal const uint FishingCatchMesGuest = 0x01FB4000;

        // The fishing MENU text (event mes ids 20 "Fish/Exchange FP/Fishing log/Quit", 21 "no pole", 22
        // "Continue/Quit"), swapped into the EVENT-mes ClsMes (EditEventMes1 0x21D1E4D0 +0x17A0, window 1)
        // while a session is open. Custom towns' event mes lacks these ids; vanilla fishing towns have them.
        // 312 bytes; sits just above FishingCatchMes in the same MeshCave margin.
        internal const long FishingMenuMes       = 0x21FB4100;   // ends 0x21FB4238, below band top 0x21FB4300
        internal const uint FishingMenuMesGuest  = 0x01FB4100;

        // ── Fishing sign injection ───────────────────────────────────────────────────────────────────
        // This region (MeshCave margin, below the fishing-mes blocks, clear of 0x21FB4000) hosts the sign
        // asset buffers + a one-shot load stub + config. It is shared by two mutually-exclusive approaches:
        //   • GlobalSignLoader (CURRENT): loads the kanban as a GLOBAL CFrame via LoadMDSFile and registers
        //     e01b24 into the system texture manager 0x1c75870 (the miracle-chest's path) — then a draw hook
        //     renders it. The two asset buffers below pack exactly into 0x21FAE400..0x21FB3400.
        //   • SignInjector (SUPERSEDED, not armed): built the sign as a villager from a bundled fishsign.chr.
        //     Its SignChrCave alias covers the same bytes; only one loader is ever armed, so there is no clash.
        internal const long SignMdsCave       = 0x21FAE400;   // kanban.mds  (2160 B) → ends 0x21FAEC70
        internal const uint SignMdsCaveGuest  = 0x01FAE400;
        internal const long SignImgCave       = 0x21FAEC70;   // e01b24_bank.img (18320 B, IM2) → ends 0x21FB3400
        internal const uint SignImgCaveGuest  = 0x01FAEC70;
        internal const long SignChrCave       = 0x21FAE400;   // (superseded) fishsign.chr alias — SignInjector
        internal const uint SignChrCaveGuest  = 0x01FAE400;
        internal const long SignStubCave      = 0x21FB3400;   // the cave stub (custom cmd-10 handler)
        internal const uint SignStubCaveGuest = 0x01FB3400;
        internal const long SignConfig        = 0x21FB3600;   // GlobalSignLoader: +0 ready +4 loaded +8 cframe +C fires
        internal const uint SignConfigGuest   = 0x01FB3600;
    }

}
