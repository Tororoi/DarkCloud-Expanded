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
    /// clean (tools/analysis/find_code_caves.py + CodeCaveScanner.cs → CodeCaveFindings.txt); two systems silently
    /// overlapping is the failure mode this file exists to prevent. Anything added here must also be
    /// reflected in CodeCaveScanner's ModReserved list so the sweeper doesn't flag our own writes.
    ///
    /// Addresses come in two forms:
    ///   • MMU  (0x21xxxxxx) — what Memory.Read*/Write* take.
    ///   • Guest(0x01xxxxxx) — what the GAME sees, i.e. what we bake into pointers/instructions.
    ///
    /// The rules this map follows (page isolation / PINE write safety, address-ordered tables, NextFree,
    /// capacity beside the cave, the monster-pool carve) and the incidents behind them: docs/code-caves.md.
    ///
    /// ── MAP (guest) ───────────────────────────────────────────────────────────────────────────────────
    /// Everything below 0x01FB4300 lives in ONE proven-clean heap tail (CodeCaveScanner, 68 sessions),
    /// packed so that the clone's mesh cave fits the LARGEST character (Goro): all six are clonable.
    ///
    ///   0x01F10000  PNACH mailbox — 4-byte flag slots, see <see cref="Mailbox"/> (its NextFree marks the
    ///               next unclaimed slot — do not trust any prose copy of it)
    ///   0x01F10040  Town-camera scratch: stick ease @+0x00, E_prev quad @+0x10 (16B) — written per frame
    ///               by the ISO-baked camera function (boot-zeroed)
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
    ///
    /// A SECOND band holds the ISO-baked ELF caves: a mod-created PT_LOAD segment at guest
    /// 0x01FB0000–0x01FB4000 (the hijacked phdr3 — <see cref="ElfCave"/>, its only registry). Loader-loaded
    /// at boot, so direct j/jal into it is legal; the scanner's heap-tail claim covers it. Its host pages hold
    /// NO runtime-written data (the page-isolation rule, stated on ElfCave). ⚠ 0x228BB0–0x22A210 is NOT
    /// free: it is the live dungeon character-change screen, however dead it looks.
    /// </summary>
    internal static class CodeCaves
    {
        // The sibling registries: <see cref="Mailbox"/> (the PNACH mailbox page), <see cref="ElfCave"/> (the ISO-baked
        // segment), DunCave / DebugIfCave / DebugInfoCave / DebugItemCave (dead-function hosts), <see cref="CatBlock"/>
        // and <see cref="WaterRedraw"/> (runtime words).

        // ── HarderEnemyAI: per-species STB stubs ─────────────────────────────────────────────────────
        // One self-contained stub per SPLICED SPECIES on a floor (not per live enemy — nothing is shareable,
        // every stub embeds script-local CALL/branch targets). A floor has a handful of species, so 32 is
        // ample; the cap exists so this can never grow into the clone caves that follow it.
        internal const long AiStubBase     = 0x21F10100;
        internal const int  AiStubStride   = 0x400;
        internal const int  AiStubMaxSlots = 32;          // 32 × 0x400 = 0x8000 → ends 0x21F18100, clear of PtrTable
        /// <summary>The TOP eight stub slots are the Solar Flash's blinding programs (SolarScript: one block per held
        /// species — its guard hold, its guard-lowering clip and its stagger — jumped to from a single instruction at the
        /// head of each of the species' AI and hit-reaction labels). HarderEnemyAI hands out the 24 below them.</summary>
        internal const int  AiStubSolarSlot = 24;
        internal const long SolarStubBase   = AiStubBase + (long)AiStubSolarSlot * AiStubStride;  // 0x21F16100
        internal const int  SolarStubBlock  = 0x300;                                                // per species
        internal const int  SolarStubBlocks = (AiStubMaxSlots - AiStubSolarSlot) * AiStubStride / SolarStubBlock;   // 10, within 0x21F16100..0x21F18100

        // A cave's CAPACITY (size or slot count) is declared beside its address, never back-computed from the
        // gap to the next cave, and the code that fills it bounds-checks against that constant.
        // ── Queens waterfall spray table ─────────────────────────────────────────────────────────────
        // Populated by CanalTide each Queens tick, read every frame by the queensSprayCave (hooked into MainDraw
        // @0x17c5a0). Layout: word[0] = emitter count, then `count` × 32-byte entries { pos x,y,z,w; spread x,y,z,w }.
        // Sits in the free gap between the AiStub band (ends 0x21F18100) and PtrTable (0x21F19000). ~16 entries max
        // (0x10 + 16×0x20 = 0x210 → ends 0x21F18610, clear of PtrTable). Town-only, so no clash with the AI stubs
        // (dungeon) even though both live on the mailbox page. The cave bakes the physical form 0x01F18400.
        internal const long QueensSprayTable      = 0x21F18400;
        internal const uint QueensSprayTableGuest = 0x01F18400;
        internal const int  QueensSprayMaxEmitters = 40;              // 40 × 0x30 + 0x10 = 0x790 → ends 0x21F18B90, clear of PtrTable
        internal const int  QueensSprayEntryStride = 0x30;            // pos(16) + spread(16) + bias(16); matches queens_spray_cave.s
        // Transient velocity-bias vector the spray-bias shim (sprayBiasShim.bin, hooked into EffectWaterSpray) adds
        // to each particle's initial velocity. The spray cave sets it per emitter from the table's bias field and
        // re-zeros it after the loop, so Matataki's own spray (same EffectWaterSpray) stays unbiased. 3 floats.
        internal const long QueensSprayBias       = 0x21F18300;       // cave bakes physical 0x01F18300

        // ── Mirage: decoy aggro redirect ─────────────────────────────────────────────────────────────
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

        /// <summary>Cloth draw buffers; the size is declared here, beside the address.</summary>
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
        // against ONE character silently overruns the next cave along. Each carries an explicit size, and
        // CharacterClone REFUSES to spawn past MaxCloneNodes.
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
        // ⚠ 0x01400000 in main BSS is NOT verified free — never swept by the code-cave scanner, only seen as a zero
        // block. It is deliberately NOT in CodeCaveScanner.ModReserved, which would make the sweeper treat the
        // region as ours and stop reporting the truth about it.
        // The feature is dormant (EnemyModelInjector.Enabled == false) and must be given a
        // scanner-verified cave from this file before it is ever switched on.

        // ── MeshCave / PropCat: the clone's software-skinned meshes, shared between the cat and the prop (0x21F56400 .. 0x21FAE400) ──
        /// <summary>Software-skinned meshes. Sized for the WORST CASE character — GORO at 0x57B30 — so ALL SIX
        /// are clonable.</summary>
        internal const long MeshCave       = 0x21F56400;
        internal const long MeshCaveGuest  = 0x01F56400;
        internal const int  MeshCaveSize   = 0x58000;    // → ends 0x21FAE400, 0x5F00 clear of the band top (0x1FB4300)

        // ── The Divine Beast cat and the Angel Gear slingshot prop are up TOGETHER (both arm on the Angel Gear):
        //    the cat keeps the BOTTOM of the motion / FrameInf / BoneMtx / mesh caves, the prop the TOP (under the prop's own
        //    0x1000 track cave at the very top of the MeshCave). Sizes: the prop is ≤ 8 nodes (WeaponCave), the cat 47 + 1. ──
        internal const int  PropMeshReserve       = 0xC000;                                             // the prop's mesh region
        internal const long CatMeshCaveEnd        = MeshCave + MeshCaveSize - 0x1000 - PropMeshReserve;  // the cat's meshes end here
        internal const long PropMeshCave          = CatMeshCaveEnd;
        /// <summary>Inside the prop's 0x1000 track cave at the top of the MeshCave (its cloned tracks below +0x800, their key table at
        /// +0x800): 20 × vec4 (x, height, y, w) at +0xC00 — the Angel Gear shield ring's per-enemy "where the player is" positions,
        /// referenced by the AI redirect <see cref="PtrTable"/> (16-byte aligned: sceVu0CopyVector copies a quadword).</summary>
        internal const long ShieldRingTable       = MeshCave + MeshCaveSize - 0x400;
        internal const uint ShieldRingTableGuest  = (uint)(MeshCaveGuest + MeshCaveSize - 0x400);
        internal const int  PropFrameInfCaveSize  = 0x800;                                              // 9 × 0xD0 = 0x750
        internal const long PropFrameInfCave      = FrameInfCave + FrameInfCaveSize - PropFrameInfCaveSize;
        internal const long PropFrameInfCaveGuest = FrameInfCaveGuest + FrameInfCaveSize - PropFrameInfCaveSize;
        internal const int  CatFrameInfCaveSize   = FrameInfCaveSize - PropFrameInfCaveSize;              // 0x4800 → 88 bones
        internal const int  PropBoneMtxCaveSize   = 0x280;                                              // 10 × 0x40
        internal const long PropBoneMtxCave       = BoneMtxCave + BoneMtxCaveSize - PropBoneMtxCaveSize;
        internal const int  CatBoneMtxCaveSize    = BoneMtxCaveSize - PropBoneMtxCaveSize;                // 0x1680 → 90 bones
        internal const int  PropMotionSlot0       = 4;                                                  // the prop's channels sit at MotionCave slots 4..7
        // ── The cat's OVERFLOW mesh space: with the wings the cat's copies need ~407 KB (skin 0x30 + 2×0x180E0 +
        //    0xB2B0, each wing 0x30 + 2×0x7B60 + ~0x3300, + 16 B/vertex of skin sources) — more than the whole MeshCave. The
        //    cat borrows CharacterClone's cloth caves (ClothObjCave .. MotionCave, 0x23F00 B) for SECOND VU buffers and the
        //    skin sources: a cloth clone (the town ally switch, Mirage's Ungaga clone) can never be up while Xiao's cat is —
        //    the cat exists only while Xiao is the active dungeon character, and Mirage tears down on a party swap. ──
        internal const long CatOverflowCave      = ClothObjCave;
        internal const long CatOverflowCaveGuest = ClothObjCave - 0x20000000;
        internal const int  CatOverflowCaveSize  = (int)(MotionCave - ClothObjCave);                        // 0x23F00

        // ── 0x21FB4000 .. 0x21FB4300 (guest 0x01FB4000, 0x300 B, top of the MeshCave margin) ─────────────
        // +0x00 (4 B) holds the shallow-fishing bobber-anchor global (FishLineShallow.BobberPtr, FishingAddresses.cs):
        //   the cold-patched FishLineStep reads game-addr 0x01FB4000 for the bobber's point address, and a data
        //   write here toggles vanilla point[18] vs shallow point[20]. The rest of the block is the cat's
        //   (<see cref="CatBlock"/>). Inside the CodeCaveScanner ModReserved heap-tail claim (0x1F10000..0x1FB4300).

        // ── 0x21FB0000 .. 0x21FB4000: the ELF-baked cave SEGMENT (<see cref="ElfCave"/>) ─────────────────
        // Loader-loaded CODE from the hijacked phdr3 — no runtime writes belong anywhere on its 16KB host
        // pages (page isolation, stated on ElfCave; docs/code-caves.md). 0x21FB2000..0x21FB4000 is reserved
        // for segment growth / ISO-baked read-only data ONLY.

        // ── 0x21FAE600 .. 0x21FB0000: the RUNTIME-DATA span under the ELF cave segment ───────────────────
        // What remains of the MeshCave margin — the last heap-tail span free for runtime data. Its pages
        // already carry runtime-written words (WaterRedraw, the blocks below), so a PINE write here cannot
        // fault the way one into a code page does. Inside the ModReserved heap-tail claim, so it stays clean.
        //
        // ── CatCopyQueue / CatCopyPairs: the cat's mesh-copy job queue (0x21FAE620, 0x910 B; its pair table sits at 0x21FAFCC0) ──
        /// <summary>The cat's mesh-copy QUEUE (ElfCave.CatCopyQueue reads it). Data, not code, in the runtime-data span.
        /// +0x00 job count (0 = idle), jobs from +0x10, 0x30 B each: src, dst, size, then two (src, size, dst) rebase specs.</summary>
        internal const long CatCopyQueue      = 0x21FAE620;
        internal const uint CatCopyQueueGuest = 0x01FAE620;
        internal const int  CatCopyQueueJobs  = 48;            // 48 × 0x30 + 0x10 = 0x910 B of the span below — the
                                                               // texture relocation needs one job per block per moved texture
        internal const int  CatCopyJobStride  = 0x30;
        // The jobs end at +0x910 (0x21FAEF30), just short of BorrowedShotBlock (0x21FAEF40). A find/replace job's old→new pair
        // table is NOT after them: the job names it (+0x10), and it lives at CatCopyPairs.
        /// <summary>How many old→new pairs a sweep can carry. MUST cover every name in CatTextures.CatTextureNames —
        /// there are TEN. A name that does not fit is silently dropped, and a dropped name's register keeps pointing into her
        /// old block (catcape is one the MASK draws with: dropped, the mask draws black). The count is checked against this
        /// rather than truncated.</summary>
        internal const int  CatCopyMaxPairs   = 16;                                           // × 16 B at CatCopyPairs

        // ── BorrowedShotBlock: the borrowed shot config and its carve state (0x21FAEF40, 0x2C0 B) ──
        /// <summary>The borrowed shot config in use (ElfCave.BorrowedShotsEnter keeps it entered in the main-character effect
        /// instance, BorrowedShots writes it): +0x00 "SHOT" (0 = nothing to enter — the mod's clear, or the cave's after a
        /// failure), the BT_SHOT_EFFECT copy (0x70, victim mask = enemies) at +0x10, +0x250 the state (mod: 0 = enter; cave:
        /// 1 = entered, −1 = no room / failed), +0x258 the effect file's path (≤ 63 chars, mod), +0x298 the CDataAlloc2 the
        /// cave carves from the monster pool once per floor {base, 0, used, cap}, +0x2A8 LoadFile's size out, +0x2AC the
        /// region's size in 16-byte units (mod), +0x2B0 the pool's used counter right after the carve (cave: while the live
        /// counter still equals it the region is the pool's top and is reused instead of carved again). Runtime data on a
        /// runtime-data page.</summary>
        internal const long BorrowedShotBlock      = 0x21FAEF40;
        internal const uint BorrowedShotBlockGuest = 0x01FAEF40;
        internal const uint BorrowedShotMagic      = 0x544F4853;   // "SHOT"
        internal const int  BorrowedShotCfg = 0x10, BorrowedShotState = 0x250, BorrowedShotPath = 0x258, BorrowedShotPathLen = 0x40,
                            BorrowedShotAlloc = 0x298, BorrowedShotReserve = 0x2AC, BorrowedShotCarveMark = 0x2B0,
                            BorrowedShotInstance = 0x2B4, BorrowedShotMainFlag = 0x2B8, BorrowedShotSubShots = 0x2BC, BorrowedShotBlockSize = 0x2C0;   // +0x2BC the sub-shots to enter (mod; ≤ 8)   // +0x2B4 the instance (guest), +0x2B8 1 = the main one (texture block cleared, live pointer set)

        // ── SharedShotBlock: the shot-slot sharing block (0x21FAF200, 0x270 B) ──
        /// <summary>The shot-slot sharing block (DebugInfoCave.SharedShots shares the monster pack's five slots among every config
        /// a floor needs; SharedShots seeds and reads it): +0x00 "SHRE" (mod; without it a refused config is only skipped when it
        /// fires), +0x04 the cave's frame counter, +0x08 disc entries, +0x0C restores, +0x10 skipped fires, +0x14 no room, +0x18 /
        /// +0x1C the monster-pool headroom an entry needs for two / six sub-shots, units of 16 B (mod; 0 = the cave's 24,000 /
        /// 48,000), +0x20 five stamps (the frame a slot last fired), +0x40 the config table (34 × {image store, mark}), +0x150 the
        /// event ring (16 × {kind, slot, config in, config out — bytes; frame; data units}), +0x250 its write index, +0x260 the
        /// allocator handed to the entry. Runtime data on a runtime-data page.</summary>
        internal const long SharedShotBlock      = 0x21FAF200;
        internal const uint SharedShotBlockGuest = 0x01FAF200;
        internal const uint SharedShotMagic      = 0x45524853;   // "SHRE"
        internal const int  SharedShotFrame = 0x04, SharedShotEntries = 0x08, SharedShotRestores = 0x0C, SharedShotSkips = 0x10, SharedShotNoRoom = 0x14,
                            SharedShotNeed2 = 0x18, SharedShotNeed6 = 0x1C, SharedShotStamps = 0x20, SharedShotCfgTable = 0x40, SharedShotRing = 0x150,
                            SharedShotRingCount = 16, SharedShotRingIndex = 0x250, SharedShotAlloc = 0x260, SharedShotBlockSize = 0x270;

        // ── LockOnFactorTable: the per-character lock-on reach factors (0x21FAF480, 0x24 B) ──
        /// <summary>The lock-on reach factor per character (DunPatches: SetNearLockOnTarget and setTargetCursor read their
        /// six-float table from HERE instead of dun 0x1DC1B20 — the enemy's lock-on distance × this = the reach): Toan 1.2,
        /// Xiao 1.4, Goro 1.1, Ruby 1.5, Ungaga 1.0, Osmond 1.8, indexed by character id. The PNACH re-seeds the six every frame
        /// while the owner word at +0x20 is 0; the mod sets it to 1 and writes what it wants (the Flamingo: Xiao's 2.8).
        /// 16-byte aligned (the routines copy it with lq). Runtime data on a runtime-data page.</summary>
        internal const long LockOnFactorTable      = 0x21FAF480;
        internal const uint LockOnFactorTableGuest = 0x01FAF480;
        internal const int  LockOnFactorCount = 6, LockOnFactorOwner = 0x20;
        internal static readonly float[] LockOnFactorVanilla = { 1.2f, 1.4f, 1.1f, 1.5f, 1.0f, 1.8f };

        // ── Single runtime words (0x21FAF4B0 .. 0x21FAFFF0): one feature's flag, vector or small table each, in address order ──
        /// <summary>A private copy of __vt__13CVisualMDTVu1 (32 B) for the Sun Sword's blade mesh: its two DrawVu1 slots point
        /// at ElfCave.CatMaskTint, so the blade draws under the ambient CatBlock.CatCapeTint adds (BladeTint). Toan's sword and
        /// Xiao's cat are never live together, so the cave and its tint word are free for the blade.</summary>
        internal const long SolarBladeVtable      = 0x21FAF4B0;
        internal const uint SolarBladeVtableGuest = 0x01FAF4B0;
        /// <summary>BLESSED BAIT (BlessingGun.FishingTick): non-zero through a fishing session while a Blessing Gun is owned —
        /// SmoothRestCave.BaitKeep then fails EdMoveChara's two bait-loss rolls, so bait is only spent on a fight.</summary>
        internal const long BaitKeep      = 0x21FAF4D0;
        internal const uint BaitKeepGuest = 0x01FAF4D0;
        // 0x21FAF4E0..0x21FAF830 FREE

        /// <summary>Toan's CHARGE-ATTACK hit radii, turned from baked immediates into DATA by
        /// <c>ElfToanMeleePatches.PatchChargeHitRadius</c>: +0x00 the lunge's (vanilla 6.0), +0x04 the whirlwind's
        /// (vanilla 12.0). ToanKey_Play built both with `lui v0,imm; mtc1 v0,f12` (0x241AC0 / 0x241B90) and hands
        /// the result to CCollisionData::Set as the sphere radius, so an ability that writes here resizes the
        /// engine's OWN charge-attack hit — no mod-side hit detection and no planted spheres.
        /// ⚠ Read every time a charge attack swings, so the mod SEEDS both to their vanilla values at startup:
        /// a 0 here is a hit radius of nothing and the charge attacks would connect with empty air.</summary>
        internal const long ChargeHitRadius      = 0x21FAF830;
        internal const uint ChargeHitRadiusGuest = 0x01FAF830;
        internal const int  ChargeRadiusLunge = 0x00, ChargeRadiusWhirl = 0x04;
        internal const float LungeRadiusVanilla = 6.0f, WhirlRadiusVanilla = 12.0f;

        /// <summary>The hit REACTION every item-bomb explosion carries — chest traps and anything thrown, and
        /// (by the look of it) Halloween's pumpkin. SetBombEffect passed a literal 3 (the unguardable knockdown);
        /// <c>ElfDamagePatches.PatchBombReaction</c> makes it read this word instead, so an ability can make bomb
        /// blasts inert (a reaction outside {2,3,4} is ignored by the player's damage handler) without touching the
        /// shot configs, which bombs do not use. ⚠ Seeded to the vanilla 3 at startup and restored by anything that
        /// changes it: 0 here would also be inert, i.e. bombs would stop working for everyone.</summary>
        internal const long BombReaction      = 0x21FAF838;
        internal const uint BombReactionGuest = 0x01FAF838;
        internal const int  BombReactionVanilla = 3;

        /// <summary>What the auto-guard cave leaves for the mod: +0x00 a COUNTER it ticks each time it swallows a
        /// hit, +0x04/+0x08/+0x0C where that hit was. The cave does the part only it can do — make the engine forget
        /// the hit — and the FEEDBACK is the mod's, because rumble, sound and a flash are one line each in C# and a
        /// dozen fiddly instructions in a cave.</summary>
        internal const long AutoGuardSignal      = 0x21FAF840;
        internal const uint AutoGuardSignalGuest = 0x01FAF840;
        internal const int  AutoGuardCount = 0x00, AutoGuardX = 0x04, AutoGuardH = 0x08, AutoGuardY = 0x0C;

        /// <summary>HIDE THE LOCK-ON NAME PLATE while nonzero. The plate's draw asks GetMonsterNameDrawFlag (0x20EB70)
        /// and setTargetCursor re-raises that flag through its setter EVERY frame the target is on screen, so a mod
        /// write of 0 loses every other frame (a name flickering over the judgement blade). The getter jumps to
        /// <see cref="ElfCave.NameDrawGate"/> instead, which ANDs the flag with NOT this word — 0 (fresh memory, no
        /// seed needed) is vanilla; 1 hides the plate without touching the flag or the enemy.</summary>
        internal const long NameHide      = 0x21FAF850;
        internal const uint NameHideGuest = 0x01FAF850;

        /// <summary>WHERE THE JUDGEMENT BLADE IS (x, h, y, 1): the position every enemy's `_GET_POSITION(-2)` is
        /// pointed at through <see cref="PtrTable"/> while the blade falls and the flash holds them, so they turn
        /// to the danger and stay turned (Mirage's decoy redirect, driven by Big Bang; the fall thread keeps it on
        /// the blade, the landing leaves it on the blast).</summary>
        internal const long JudgementPos      = 0x21FAF860;
        internal const uint JudgementPosGuest = 0x01FAF860;

        /// <summary>TOAN'S STRIDE on motion 33 (the guard walk), as an EXTRA fraction of the engine's own: the player
        /// key handler builds his per-frame move vector (X in f21, Z in f20 at dun 0x1DB0F68) and the stride cave
        /// (ElfToanMeleePatches.PatchStrideScale, hooked there by DunPatches) adds this × that vector back onto it while
        /// the current motion is 33 — the stride grows, the animation plays at its own rate. 0 (fresh memory) is
        /// vanilla; the Sword of Zeus writes 0.3 while a lock is held and 0 otherwise.</summary>
        internal const long StrideScale      = 0x21FAF870;
        internal const uint StrideScaleGuest = 0x01FAF870;

        // THE DUNGEON CAMERA'S HEIGHT is regulated every frame by the camera pass (OpC_MotionProcess, dun 0x1DBF300):
        // nearer than 60 to Toan it climbs 0.5 a frame; farther, it decays toward a rest of 5.0 at 0.05 × the excess per
        // frame (0.15..0.5), never below a floor of 1.6. A write to the height field is undone within frames; a hold
        // needs the cave below.
        /// <summary>THE CAMERA PIN: a world HEIGHT (+4; +0/+8 unused) and a flag (+0xC). While the flag is set the
        /// camera-pin cave (DebugInfoCave.CameraPin, run at the end of the dungeon camera pass every frame) sets the
        /// follow camera's height field so that it sits at exactly that world height whatever its follow point does —
        /// the camera held low while Toan lunges, still around him as the engine places it. 0 (fresh memory) = off.</summary>
        internal const long CameraPin      = 0x21FAF890;
        internal const uint CameraPinGuest = 0x01FAF890;
        internal const int  CameraPinFlag  = 0xC;
        /// <summary>The charge lunge's EXTRA GRAVITY, as a fraction of the vanilla 0.1 (see DebugInfoCave.LungeGravitySeed):
        /// the parabola's launch speed and its per-frame gravity are both × (1 + this), so the jump is (1 + this) times
        /// as high over the same frames. 0 (fresh memory) = vanilla; the Sword of Zeus writes 0.5 for its level-2 lunge.</summary>
        internal const long LungeGravityExtra      = 0x21FAF8A0;
        internal const uint LungeGravityExtraGuest = 0x01FAF8A0;
        /// <summary>THE BLADE, MOVED BY THE ENGINE (DebugInfoCave.VerticalDrive, once a frame): +0 flag — 1 = FALLING (vy += g,
        /// y −= vy, stopped at +0x10 where the flag becomes 2), 3 = FOLLOWING (x and z copied from the unit position at
        /// the guest address in +0x14 plus the x/z offsets at +0x18/+0x1C, height = the unit's + the y word), 0 = off;
        /// +4 the grip's world height y (falling) or its height OVER the unit (following), +8 its speed vy, +0xC the
        /// gravity g per frame², +0x10 the height a fall stops at, +0x14 the followed unit's position (CharObjects.PosAddr
        /// or Toan's own position words, guest), +0x18/+0x1C the x/z offset from it (0 over an enemy; the spot ahead of
        /// Toan for the Zeus charge blade). The cave writes the blade copy's chara slot position (BladeProp.Slot) each
        /// frame. For a fall, g = 2·span/N² lands it in exactly N frames.</summary>
        internal const long VerticalDrive      = 0x21FAF8B0;
        internal const uint VerticalDriveGuest = 0x01FAF8B0;
        internal const int  VerticalDriveFlag = 0x0, VerticalDriveY = 0x4, VerticalDriveVy = 0x8, VerticalDriveG = 0xC, VerticalDriveStop = 0x10, VerticalDriveUnit = 0x14;
        internal const int  VerticalDriveOffX = 0x18, VerticalDriveOffZ = 0x1C;   // following: an x/z offset from the unit (the charge blade ahead of Toan: the unit is HIM)
        internal const int  VerticalDriveOff = 0, DriveFalling = 1, DriveLanded = 2, DriveFollowing = 3;
        /// <summary>Mode 4 (DebugIfCave.FallDrive): falling as mode 1 AND, across the ground, the unit at +0x14's x/z plus the offsets —
        /// or, with no unit, the slot's own x/z plus the offsets each frame (a drift); with <see cref="FallDrive"/>'s stop source and
        /// drive rows. Lands as mode 1 does (flag 2).</summary>
        internal const int  DriveFallFollowing = 4;
        /// <summary>A WEAPON-HP BILL FOR THE ENGINE TO TAKE (DebugInfoCave.WhpBill, the tail of the camera-pin chain, once a
        /// dungeon frame): +0 the factor of a bill the mod has posted (swing-equivalents: base WHP / 1.5), +4 a magic the mod
        /// writes ahead of it (<see cref="WhpBillMagicValue"/>) so stale memory never posts one. While the magic matches
        /// and the factor is non-zero the cave zeroes the factor and calls SwordDmgCheck1(factor, 0) — the engine's own
        /// per-swing drain: Endurance, Durable and Fragile, its warnings, its Auto Repair Powder and its BREAK, exactly as a
        /// landed hit would have them. WeaponWhp posts the bills (a bolt, a blast, a flash) the moment they strike.</summary>
        internal const long WhpBill      = 0x21FAF8D0;
        internal const uint WhpBillGuest = 0x01FAF8D0;
        internal const int  WhpBillFactor = 0x0, WhpBillMagic = 0x4;
        internal const uint WhpBillMagicValue = 0x4C494257;   // "WBIL"

        /// <summary>TOAN'S MELEE KICK STRENGTHS as data (ElfToanMeleePatches.PatchMeleeKickStrength). ToanKey_Play hands
        /// SetKickBack an immediate strength for five of its seven hits — combo hit 3 (1.5), hit 4 (2.0), hit 5, the
        /// lunge and the whirlwind (3.0 each); hits 1 and 2 read the shared 1.2 word (MeleeKick.Strength12) — so those
        /// five become words here: +0x0 hit 3, +0x4 hit 4, +0x8 hit 5, +0xC lunge, +0x10 whirlwind, +0x14 owner (the
        /// pnach re-seeds the vanilla five every frame while 0; the mod sets 1 and writes its own — MeleeKick).</summary>
        internal const long MeleeKickWords      = 0x21FAF8E0;
        internal const uint MeleeKickWordsGuest = 0x01FAF8E0;
        internal const int  MeleeKickHit3 = 0x0, MeleeKickHit4 = 0x4, MeleeKickHit5 = 0x8, MeleeKickLunge = 0xC, MeleeKickWhirl = 0x10, MeleeKickOwner = 0x14;

        /// <summary>THE MAGIC CIRCLE TABLE (tools/stubs/circle_effects.s, DebugIfCave.CircleEffects — the cave dun.bin's
        /// Run_TrapCircle jumps to): every magnitude a circle applies, as words the cave reads. +0x00 owner (the pnach re-seeds
        /// the vanilla figures every frame while 0; a sword that changes the circles sets 1 — Weapons.MagicCircles), then
        /// AttackFrames, GildaUpMult (f), GildaUpAdd, GildaDownFrac (f), MaxWhpUpMin, MaxWhpUpRange, StatDownMin, StatDownRange,
        /// MaxWhpDownMin, MaxWhpDownRange, WhpDivisor (f), RageFrames, AbsFullItem, WhpCureItem, ElemDownMult, Favour (the Secret
        /// Armlet: the bad circles dealt as good ones), SlowFrames, RewardCount — the .s says what each does. Ranges must stay ≥ 1.</summary>
        internal const long CircleTable      = 0x21FAF900;
        internal const uint CircleTableGuest = 0x01FAF900;
        internal const int  CircleOwner = 0x00, CircleAttackFrames = 0x04, CircleGildaUpMult = 0x08, CircleGildaUpAdd = 0x0C,
                            CircleGildaDownFrac = 0x10, CircleMaxWhpUpMin = 0x14, CircleMaxWhpUpRange = 0x18, CircleStatDownMin = 0x1C,
                            CircleStatDownRange = 0x20, CircleMaxWhpDownMin = 0x24, CircleMaxWhpDownRange = 0x28, CircleWhpDivisor = 0x2C,
                            CircleRageFrames = 0x30, CircleAbsFullItem = 0x34, CircleWhpCureItem = 0x38, CircleElemDownMult = 0x3C,
                            CircleFavour = 0x40, CircleSlowFrames = 0x44, CircleRewardCount = 0x48;
        internal const int  CircleTableBytes = 0x4C;

        /// <summary>A NATIVE CALL REQUEST (DebugIfCave.CallRequest, the tail of the camera-pin chain, once a dungeon frame):
        /// the mod fills the function and its arguments, writes the magic LAST, and the cave calls it from the camera pass's
        /// epilogue — clearing the magic first, storing v0 and raising Done after. Up to six integer arguments (a0–a3, then
        /// t0/t1 as the EE ABI passes the fifth and sixth) and one float in f12. Core/NativeCall drives it and waits on Done.
        /// Only for routines that are themselves called from the dungeon's per-frame update (SetCashModel and its like).</summary>
        internal const long CallRequest      = 0x21FAF950;
        internal const uint CallRequestGuest = 0x01FAF950;
        internal const int  CallMagic = 0x00, CallFunc = 0x04, CallA0 = 0x08, CallA1 = 0x0C, CallA2 = 0x10, CallA3 = 0x14,
                            CallA4 = 0x18, CallA5 = 0x1C, CallF12 = 0x20, CallV0 = 0x24, CallDone = 0x28;
        internal const uint CallMagicValue = 0x4C4C4143;   // "CALL"

        /// <summary>GLOW ON A PELLET (the cat glow draw cave, tools/stubs/cat_glow_draw.s): a player pellet's pool slot + 1, or 0.
        /// Non-zero, the glow disc sits on that pellet's own position (the shot pool, +0x40 + slot × 0x10) every frame instead of
        /// on the frames in CatGlowNodeA/B — a disc rides a pellet with nothing to carry it (SolarGlow.Show's pelletSlot).</summary>
        internal const long GlowPellet      = 0x21FAF980;
        internal const uint GlowPelletGuest = 0x01FAF980;

        /// <summary>PELLET CONTACTS (the pellet-contact cave, DebugIfCave.PelletContact): the last player pellet contact the engine made.
        /// +0 a counter the cave steps per contact (the mod polls it), +4 the pellet's pool slot, +8 what it met (checkCollision's
        /// return: 3 an enemy, 1 a wall), +0xC/+0x10/+0x14 the point — an enemy's hit-sphere centre (its own table's), or the wall
        /// point. Read by PelletContacts.</summary>
        internal const long PelletContact      = 0x21FAF990;
        internal const uint PelletContactGuest = 0x01FAF990;
        internal const int  ContactCounter = 0x00, ContactSlot = 0x04, ContactKind = 0x08, ContactX = 0x0C, ContactH = 0x10, ContactY = 0x14;
        internal const int  ContactEnemy = 3, ContactWall = 1;

        /// <summary>GEM DAMAGE FACTOR (the gem-damage cave, DebugIfCave.GemDamage): a thrown gem's burst damage is multiplied by this
        /// integer as the throw sets it; 0 (fresh memory) = vanilla. The Crysknife's "Crystal Affinity" writes 2 while it is in hand.</summary>
        internal const long GemDamageFactor      = 0x21FAF9B0;
        internal const uint GemDamageFactorGuest = 0x01FAF9B0;

        /// <summary>Babel's Spear: a confused enemy with nothing to go after wanders — its target-pointer entry names one of
        /// these, its own (x, height, y, 1) quadword the mod moves every few seconds. 16 slots × 16 B, quadword-aligned
        /// (the redirect copies it with sceVu0CopyVector).</summary>
        internal const long BabelWander      = 0x21FAF9C0;
        internal const uint BabelWanderGuest = 0x01FAF9C0;
        internal const int  BabelWanderStride = 16;

        /// <summary>1 while the SECOND main-character effect instance (CharaMainEffectCrash) holds a sub-shot an ability wants
        /// stepped and drawn beside the live one (the second-effect caves read it every frame); 0 otherwise.</summary>
        internal const long SecondEffectLive      = 0x21FAFAC0;
        internal const uint SecondEffectLiveGuest = 0x01FAFAC0;
        /// <summary>Radians added to chara slot 3's yaw every dungeon frame by the blade-spin cave (0 = still).</summary>
        internal const long BladeSpin      = 0x21FAFAD0;
        internal const uint BladeSpinGuest = 0x01FAFAD0;
        /// <summary>Written at a collision entry's +0x9C (a word Set__14CCollisionData never writes) to tell the no-drain caves the
        /// hit is Ungaga's and costs no weapon HP; the mod clears it when it withdraws the entry.</summary>
        internal const uint NoDrainMark = 0x4B495053;   // "SPIK"
        internal const int  NoDrainMarkOff = 0x9C;

        /// <summary>A solid column for enemies (the spear-block cave): +0 flag (0 = off), +4 x, +8 height (unused), +0xC y, +0x10 radius.</summary>
        internal const long SpearBlock      = 0x21FAFAE0;
        internal const uint SpearBlockGuest = 0x01FAFAE0;
        internal const int  SpearBlockFlag = 0x0, SpearBlockX = 0x4, SpearBlockH = 0x8, SpearBlockY = 0xC, SpearBlockR = 0x10, SpearBlockTop = 0x14;   // H = the floor; Top = the column's top (enemy shots stop below it)

        /// <summary>Hercules' Wrath: a copy of Ungaga's charge-effect config (BT_SHOT_EFFECT, 0x70 B) with every phase's hit radius
        /// doubled; the main-character instance is pointed at it while the spear is his.</summary>
        internal const long HerculesCfg      = 0x21FAFB00;
        internal const uint HerculesCfgGuest = 0x01FAFB00;

        /// <summary>The Terra Sword's boulder shadow (DebugIfCave.RockShadow, in the dungeon's shadow pass): +0 flag (0 = off), +4 the
        /// frame to draw as a shadow (guest), +0x10 the plane point (x, h, y, 1), +0x20 the projection direction (0, 1, 0, 0) —
        /// quadword-aligned (MGDrawShadowFast copies both with sceVu0CopyVector).</summary>
        internal const long RockShadow      = 0x21FAFB80;
        internal const uint RockShadowGuest = 0x01FAFB80;
        internal const int  RockShadowFlag = 0x0, RockShadowFrame = 0x4, RockShadowPlane = 0x10, RockShadowDir = 0x20;

        /// <summary>The blade fall's mode 4 extras (DebugIfCave.FallDrive): +0 the stop source (guest address of a float; 0 = none: the
        /// stop is VerticalDrive's own), +4 the offset added to it; from +0x10, <see cref="FallDriveRowCount"/> DRIVE ROWS of 0x20 —
        /// +0 dst (guest; 0 = off), +4 count (≥ 1), +8 a, +0xC b, +0x10 lo, +0x14 hi: clamp(a + b·y, lo, hi) written as a float to
        /// dst and the count−1 words after it, every falling frame (y = the fall height).</summary>
        internal const long FallDrive      = 0x21FAFBC0;
        internal const uint FallDriveGuest = 0x01FAFBC0;
        internal const int  FallDriveStopSrc = 0x0, FallDriveStopOff = 0x4, FallDriveRows = 0x10, FallDriveRowStride = 0x20, FallDriveRowCount = 5;
        /// <summary>The ARMED HOP: while +0xB0 is set, a mode-4 landing becomes the hop on the same frame — +0xB4 set (the mod's
        /// signal), vy (+0xB8), the x/z drift a frame (+0xBC/+0xC0) and the stop (+0xC4) taken from here, no unit, no stop source.</summary>
        internal const int  FallDriveHopArmed = 0xB0, FallDriveHopped = 0xB4, FallDriveHopVy = 0xB8, FallDriveHopDx = 0xBC, FallDriveHopDz = 0xC0, FallDriveHopStop = 0xC4;
        internal const int  FallRowDst = 0x0, FallRowCount = 0x4, FallRowA = 0x8, FallRowB = 0xC, FallRowLo = 0x10, FallRowHi = 0x14;
        /// <summary>A CRUSHING hit of the mod's, at a collision entry's +0x9C: passes every guard window (DebugIfCave.GuardCrush). Its
        /// high half is <see cref="NoDrainMark"/>'s, which is all the no-drain caves test — an Ungaga crushing hit bills no weapon HP.</summary>
        internal const uint CrushMark = 0x4B495243;   // "CRIK"

        // 0x21FAFC90..0x21FAFCB0 FREE

        /// <summary>The guard gate's per-enemy window mask (DebugInfoCave.GuardMask, written by GuardGate): one byte per slot, bit w set =
        /// enemy window w blocks nothing (7 = none of its windows).</summary>
        internal const long GuardMask   = 0x21FAFCB0;   // 16 B

        /// <summary>The cat copy queue's old→new pair table (16 B each, CatCopyMaxPairs of them): a find/replace job names it.</summary>
        internal const long CatCopyPairs      = 0x21FAFCC0;   // 0x100 B
        internal const uint CatCopyPairsGuest = 0x01FAFCC0;

        /// <summary>The points the engine carries with units (DebugInfoCave.Follow, every frame): FollowCount entries of FollowStride,
        /// each +0 the source (guest address of a position: x, height, y; 0 = off), +4 the destination (guest), +8/+0xC/+0x10 the
        /// x/height/y offsets added. The stars: the Terra nut's bonked enemy (entry 0), Babel's confused enemies (one each).</summary>
        internal const long FollowTable = 0x21FAFDC0;
        internal const int  FollowCount = 16, FollowStride = 0x14, FollowSrc = 0x0, FollowDst = 0x4, FollowOff = 0x8;

        /// <summary>The resident stars instance's config copy (one BT_SHOT_EFFECT, 0x70 B): StarsLane points the instance here.</summary>
        internal const long StarsCfg = 0x21FAFF00;      // 0x70 → 0x21FAFF70
        /// <summary>The stars instance to CONSTRUCT (guest; 0 = none): StarsLane posts a freshly carved instance here, the stars
        /// step cave runs `__ct__12CSHOT_EFFECT` on it (its nine CCharacters' vtables and sub-objects — Initialize and Entry2
        /// make virtual calls through them) and writes 0 back.</summary>
        internal const long StarsConstruct = 0x21FAFF70; // 4 B (0x21FAFF74..0x21FAFFE0 free)
        internal const uint StarsConstructGuest = 0x01FAFF70;
        /// <summary>The Confuse ability's procs (confuse_proc.s): a byte per enemy slot, 1 = the roll succeeded this hit; the mod
        /// (ConfuseAbility) confuses the slot and writes it back to 0.</summary>
        internal const long ConfuseProc = 0x21FAFFE0;   // 16 B
        /// <summary>The resident stars instance's gate (the stars step/draw caves): +0 live (the mod: 1 once entered on this floor),
        /// +4 its region's allocator base, +8 the region's mark, +0xC the instance (guest) — a CSHOT_EFFECT (0xA160) StarsLane carves
        /// from the monster pool just below the region. The instance is stepped and drawn only while live and the region still
        /// carries its signature ("BSHT" + the mark, 16 B below the base) with the monster pool at or past the mark.</summary>
        internal const long StarsGate   = 0x21FAFFF0;   // 16 B (to the ELF cave segment)
        internal const uint StarsGateGuest = 0x01FAFFF0;
        internal const int  StarsGateLive = 0x0, StarsGateBase = 0x4, StarsGateMark = 0x8, StarsGateInstance = 0xC;
    }

}
