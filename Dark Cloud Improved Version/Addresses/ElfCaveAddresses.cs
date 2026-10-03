namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Every ISO-baked cave in the ELF. They live in a NEW loadable segment the ISO patcher creates by
    /// rewriting SCUS_971.11's degenerate 4th program header (phdr3: PT_LOAD filesz=0 memsz=0 — a linker
    /// placeholder) to load file span 0x2AF000..0x2B1000 (dead .reldun debug data past every phdr's file
    /// extent — PCSX2 never reads it) at guest 0x01FB0000..0x01FB2000 (see ElfPatches.HijackPhdr3CaveSegment).
    /// The guest band sits inside the scanner-proven-clean heap tail (0x1F10000..0x1FB4300, ModReserved),
    /// below FishLineShallow.BobberPtr @0x01FB4000. The bytes are loaded by the ELF LOADER at boot — cold,
    /// before any recompilation — so a direct j/jal into them is legal (unlike runtime-written heap
    /// caves, which crash the recompiler; docs/cave-code-execution.md).
    ///
    /// ⚠ PAGE ISOLATION (docs/code-caves.md § PINE write safety): the segment starts 16KB-ALIGNED, and its
    /// host pages [0x1FB0000, 0x1FB4000) (16KB granularity — Apple Silicon; 4KB on Intel) hold NO
    /// runtime-written data. Once any cave on a page executes, PCSX2 compiles + WRITE-PROTECTS the page,
    /// and a PINE write to anything sharing it SIGBUSes the PINE server thread — a hard crash. So
    /// 0x1FB2000..0x1FB4000 is reserved for SEGMENT growth or ISO-baked read-only data ONLY — never a
    /// runtime mailbox/cave — and the band never grows past 0x1FB4000, where runtime data begins
    /// (TownAddresses.BobberPtr, then <see cref="CatBlock"/>). The nearest runtime-written words below
    /// are WaterRedraw's at 0x01FAE600-610.
    ///
    /// ⚠ 0x228BB0–0x22A210 IS LIVE DUNGEON CODE, however dead it looks: the dungeon SELECT quick-menu's
    /// character-change screen (CharaChangeLoop @0x228BB0 / CharaChangeKey @0x228E90 / CharaChangeDraw
    /// @0x229740), CALLED FROM THE dun.bin OVERLAY (file offset 0x1DD0) — main-ELF-only xref analysis
    /// misses it. The region stays byte-for-byte VANILLA.
    ///
    /// Placement: claim <see cref="NextFree"/>, keep this table in ADDRESS ORDER with the cave's SIZE and END,
    /// never place a cave from a patch-local literal, and check overlaps against a PATCHED ELF (a vanilla ELF
    /// does not contain the neighbouring bins). Bin-backed sizes are the .bin file's byte size
    /// (Resources/isoPatch); hand-built sizes are the instruction-word count × 4.
    ///
    ///   0x1FB0000  CanalEvictFadeHook   64 B → 0x1FB0040   canalEvictFadeHook.bin
    ///   0x1FB0050  QueensSpray         180 B → 0x1FB0104   queensSprayCave.bin
    ///   0x1FB0150  SprayBiasShim        60 B → 0x1FB018C   sprayBiasShim.bin
    ///   0x1FB0190  CapeEarlyDraw       124 B → 0x1FB020C   capeEarlyDraw.bin
    ///   0x1FB0210  FishLineSplit        88 B → 0x1FB0268   fishlineSplitCaves.bin (step entry @+0x2C)
    ///   0x1FB0270  FishLineUncastGate  148 B → 0x1FB0304   fishlineUncastGate.bin
    ///   0x1FB0350  CameraNormSideBank 2128 B → 0x1FB0BA0   cameraNormSide.bin (multi-entry, see below)
    ///   0x1FB0BD0  StiltsHeal           88 B → 0x1FB0C28   stiltsHeal.bin
    ///   0x1FB0C50  WaterOrderGate       88 B → 0x1FB0CA8   waterOrderGate.bin
    ///   0x1FB0CD0  LadderRefusal        52 B → 0x1FB0D04   hand-built (PatchLadderRefusal)
    ///   0x1FB0D10  ExclamationHeight    24 B → 0x1FB0D28   hand-built (PatchExclamationHeight)
    ///   0x1FB0D50  IdleMotionOverride   36 B → 0x1FB0D74   hand-built (PatchIdleMotionOverride)
    ///   0x1FB0D90  CatPelletFollow    4256 B → 0x1FB1E30   catPelletFollow.bin
    ///   0x1FB1E30  PropPelletFollow    156 B → 0x1FB1ECC   propPelletFollow.bin
    ///   0x1FB1ED0  BorrowedShotsEnter  296 B → 0x1FB1FF8   borrowedShotsEnter.bin (the HEAD; its tail is at 0x1FB3F40)
    ///   (the second band, 0x1FB2000 →, is the table in <see cref="ElfCave"/> below)
    /// </summary>
    internal static class ElfCave
    {
        /// <summary>Guest bounds of the hijacked-phdr3 segment; RegionEnd − RegionStart is its p_filesz/p_memsz.
        /// RegionStart must stay 16KB-aligned (page isolation — see the class doc) and 0x80-aligned (p_align).</summary>
        internal const uint RegionStart = 0x01FB0000;
        internal const uint RegionEnd   = 0x01FB4000;   // ⚠ the band NEVER grows past this: 0x1FB4000.. is runtime data (TownAddresses.BobberPtr, then CatBlock.CatBase) — page isolation
        /// <summary>ELF-file offset the segment loads from (span RegionEnd−RegionStart, zero-filled at patch
        /// time): dead .reldun debug bytes, outside every phdr's file extent and never read at runtime.</summary>
        internal const uint SegmentFileOff = 0x002AD000;   // 0x4000 B of dead .reldun (0x29FE60..0x2B11C8)

        internal const uint CanalEvictFadeHook = 0x01FB0000;   // 64 B → 0x1FB0040
        internal const uint QueensSpray        = 0x01FB0050;   // 180 B → 0x1FB0104
        internal const uint SprayBiasShim      = 0x01FB0150;   // 60 B → 0x1FB018C
        internal const uint CapeEarlyDraw      = 0x01FB0190;   // 124 B → 0x1FB020C
        internal const uint FishLineSplit      = 0x01FB0210;   // 88 B → 0x1FB0268 (init entry; ONE bin, two caves)
        internal const uint FishLineSplitStep  = 0x01FB023C;   //   the step cave inside it (@+0x2C)
        internal const uint FishLineUncastGate = 0x01FB0270;   // 148 B → 0x1FB0304

        /// <summary>ONE 2128-byte bin (cameraNormSide.bin / camera_norm_side.s) with several entry points —
        /// the whole span 0x1FB0350–0x1FB0BA0 is occupied, not just the labeled words: gather-count export
        /// @0x1FB0350, winding-agnostic normal SubA @0x1FB0390 / SubB @0x1FB0450, FishLineClamp wrapper
        /// @0x1FB0550 (jal'd from 0x16D314), the settled-gated bobber cave @0x1FB0910, and the
        /// uki ground-store bank sub @0x1FB0AE0. Keep entry offsets in sync with the .s when reassembling.</summary>
        internal const uint CameraNormSideBank = 0x01FB0350;   // 2128 B → 0x1FB0BA0
        internal const uint CamBankFishLineClamp = 0x01FB0550;
        internal const uint CamBankSettledCave   = 0x01FB0910;
        internal const uint CamBankUkiGroundSub  = 0x01FB0AE0;

        internal const uint StiltsHeal         = 0x01FB0BD0;   // 88 B → 0x1FB0C28
        internal const uint WaterOrderGate     = 0x01FB0C50;   // 88 B → 0x1FB0CA8
        internal const uint LadderRefusal      = 0x01FB0CD0;   // 52 B → 0x1FB0D04
        internal const uint ExclamationHeight  = 0x01FB0D10;   // 24 B → 0x1FB0D28
        internal const uint IdleMotionOverride = 0x01FB0D50;   // 36 B → 0x1FB0D74
        /// <summary>Divine Beast cat pellet catcher + follower (tools/stubs/cat_pellet_follow.s): takes the dungeon
        /// step loop's `jal step__5CSHOT` (dun 0x1DB874C), performs it, tracks which pellet slots are active, and when
        /// armed (<see cref="CatBlock.CatState"/> = 3) binds chara slot 1 to the next NEW pellet on its birth frame,
        /// then places it every frame (head on the pellet, growth scale, sprite fade) until that pellet ends.</summary>
        internal const uint CatPelletFollow    = 0x01FB0D90;   // 4256 B → 0x1FB1E30 (PropPelletFollow follows directly) (frame 0x80, sq/lq saves)
        /// <summary>A chara-slot prop on one of Xiao's pellets (tools/stubs/prop_pellet_follow.s): the Matador's charged shot.
        /// The hook's target (DunPatches.CatFollowHookNew): calls CatCopyQueue — the cat's chain, which performs the
        /// displaced step__5CSHOT — then places chara slot 3 on the pellet Mailbox.PropFollowSlot names.</summary>
        internal const uint PropPelletFollow   = 0x01FB1E30;   // 156 B → 0x1FB1ECC
        /// <summary>Keeps the borrowed shot config in BorrowedShotBlock entered in the MAIN-CHARACTER effect instance
        /// (ShotEffectPack.CharaMainEffect; tools/stubs/borrowed_shots_enter.s): the head of the step chain
        /// (DunPatches.CatFollowHookNew) — calls PropPelletFollow, then re-enters the instance whenever the loader refilled
        /// it or the mod seeded another config, from a signed region it carves from the monster pool. Two pieces: the head
        /// here and the tail at <see cref="BorrowedShotsEnterTail"/> (the head ends in a `b` to it).</summary>
        internal const uint BorrowedShotsEnter     = 0x01FB1ED0;   // 296 B → 0x1FB1FF8 (the first band's end is 0x1FB2000)
        // CatGuardBypass: see DunCave.CatGuardBypass (dun.bin)
        /// <summary>Divine Beast cat glow (tools/stubs/cat_glow_draw.s): hooked in place of the dungeon draw loop's two torch
        /// passes (dun 0x1DAEBF8 / 0x1DAEC10), performs them, then draws the `catglow` disc at the cat's torso with the torch
        /// routine. +0x00 = the "catglow" name, +0x08 = entry A (DrawFire), +0x20 = entry B (DrawFireFreeStyle).</summary>
        internal const uint CatGlowDraw        = 0x01FB2000;   // 476 B → 0x1FB21DC (second band; the sphere-percent cave follows at 0x1FB21E0)
        internal const uint CatGlowDrawEntryA  = CatGlowDraw + 0x08;
        internal const uint CatGlowDrawEntryB  = CatGlowDraw + 0x20;
        /// <summary>Cat sphere percentage (tools/stubs/cat_sphere_percent.s): CheckDmg's per-attacker damage-% load
        /// (main-ELF 0x1DC084) re-entered so a Xiao-owned hit whose kick type equals the sphere's spare[1]
        /// (`_SET_BODY_COL_PARA(1, kick)`, disc-baked on Minotaur Joe's face for the cat's kick 2) reads spare[0] instead
        /// of her column. Returns to 0x1DC08C.</summary>
        internal const uint CatSpherePercent   = 0x01FB21E0;   // 112 B → 0x1FB2250
        /// <summary>Xiao melee-type flinch (tools/stubs/xiao_melee_flinch.s): CheckDmg's \"Xiao's hits never stagger\" rule,
        /// re-entered from main-ELF 0x1DB410 (CheckDmg is ELF code, not the dun overlay) so that a Xiao-owned entry with a melee-type kick (+0x98 == 2, the Divine Beast cat)
        /// takes the normal flinch decision; plain pellets (kick 0) are unchanged. Returns to 0x1DB420. ElfCatPatches also
        /// accepts (and re-aims) an ISO whose hook still points at 0x1FB1FA0.</summary>
        internal const uint XiaoMeleeFlinch    = 0x01FB2250;   // 40 B → 0x1FB2278
        /// <summary>The Sun Sword's blade under its own ambient (SolarBlade): two 3-word entries that load the DrawVu1
        /// overloads of CVisualVu1 (the rigid-mesh class a weapon model is; 0x135000 / 0x134BC0) into t9 and jump into the
        /// BODY of <see cref="CatMaskTint"/> (+0x18, past its own two entries), which does the ambient add generically and
        /// calls whatever t9 holds. Written as words by ElfWeaponPatches.PatchSolarBladeTint.</summary>
        internal const uint SolarBladeTint     = 0x01FB2278;   // 24 B → 0x1FB2290
        /// <summary>The name-plate getter's body with a hide gate (ElfWeaponPatches.PatchNameDrawGate): six words —
        /// `lh v0,flag(gp); lui/lw at,NameHide; nor at,zero,at; jr ra; and v0,v0,at`.</summary>
        internal const uint NameDrawGate       = 0x01FB2290;   // 24 B → 0x1FB22A8
        // 0x01FB22A8..0x1FB22C0 (24 B) FREE — the band's last gap
        internal const uint CatCapeTint        = 0x01FB22C0;   // 176 B → 0x1FB2370: the cape's cloth draws under its own ambient
        internal const uint CatMaskTint        = 0x01FB2370;   // 228 B → 0x1FB2454: the mask's MESH does too, via a private vtable
        internal const uint CatCopyQueue       = 0x01FB2480;   // 584 B → 0x1FB26C8: the cat's mesh copy, done inside the machine; its tail also calls CatPalette
        /// <summary>The cape/mask element colour, repainted in the machine (cat_palette.s). The six colours are the
        /// cave's first six words and the CODE starts at +0x18 — that offset is what the copy-queue cave calls.</summary>
        internal const uint CatPalette         = 0x01FB2700;   // 344 B → 0x1FB2858: the colour table, then the code
        internal const uint CatPaletteEntry    = CatPalette + 0x18;   // the entry point, past the colour table
        /// <summary>The GLOW disc's six per-element palettes: 512 B each, in element order (00 Fire … 05 None). Pure
        /// DATA, written at patch time by ElfCatPatches.PatchCatGlowPalettes from the blob `build_cat_pack.py --palettes`
        /// bakes off the same index map as the disc — regenerate BOTH together, or the ramp will not match the
        /// pixels. Patch-time data in a code page is fine; a RUNTIME write here would SIGBUS PCSX2.</summary>
        internal const uint CatGlowPalTables   = 0x01FB2880;   // 4608 B → 0x1FB3A80 (9 rows: 6 elements + 3 weapon looks)
        internal const uint CatGlowPalette     = 0x01FB3A80;   // the cave that copies one table into the disc's CLUT
        internal const uint MirageHazeDraw     = 0x01FB3C40;   // 152 B → 0x1FB3CD8: one more raster, at the Mirage clone (dun hook in DunPatches)
        internal const uint SuperSteveIconDraw = 0x01FB3CE0;   // 200 B → 0x1FB3DA8: the sphere's weapon icon over Steve on the HUD (dun hook in DunPatches)
        internal const uint SuperSteveIconCopy = 0x01FB3DC0;   // 356 B → 0x1FB3F24: …and the copy that keeps the CURRENT sphere's icon in the HUD sheet (on every DngActiveWeaponTextureCopy call: four menu paths + two overlay sites)
        internal const uint BorrowedShotsEnterTail = 0x01FB3F40;   // 176 B → 0x1FB3FF0 (the band's end is 0x1FB4000): the tail of <see cref="BorrowedShotsEnter"/>
        /// <summary>The next unclaimed spot. Take it, then MOVE THIS — and add the cave to the table above
        /// (address order, size, end) so the next placement can see it.</summary>
        internal const uint NextFree = RegionEnd;    // the band is FULL; the last gap: 0x1FB22A8..0x1FB22C0 (24 B)
    }
}
