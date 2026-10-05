namespace Dark_Cloud_Improved_Version
{
    /// <summary>Caves that live INSIDE dun.bin (DunPatches writes their bytes over dead overlay code; the main-ELF hooks
    /// that jump to them run only in dungeons, where the overlay is resident).</summary>
    internal static class DunCave
    {
        /// <summary>tools/stubs/cat_guard_bypass.s over MemoryMapDump's body (a printf-only debug routine whose three callers
        /// DunPatches nops): the cat's hits and the Matador's charged pellet ignore an enemy's guard window; Dragon's Y's
        /// shot gets its kick without the bypass (main-ELF hook 0x1DAC78, ElfCatPatches.PatchCatGuardBypass).</summary>
        internal const uint CatGuardBypass = 0x01DAC070;   // 332 B → 0x1DAC1BC = MemoryMapDump's whole span
        internal const uint CatGuardBypassSpan = 0x14C;
    }

    /// <summary>A cave INSIDE a second dead main-ELF function: the body of DebugInfomationIF (0x1B47C0, 3,712 B), the
    /// developers' debug overlay INPUT handler, reached only from the overlay's debug key (dun 0x1DB5150, mode 0xDD).
    /// ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts turns its first two words into `jr ra; li v0,0` — that caller reads
    /// "nothing pressed" — and ElfWeaponPatches.PatchCircleEffects writes the cave from +8.</summary>
    internal static class DebugIfCave
    {
        internal const uint Host = 0x001B47C0, HostSpan = 3712, VanillaWord0 = 0x27BDFFE0;   // `addiu sp,sp,-0x20`
        /// <summary>tools/stubs/circle_effects.s: the magic circles, every magnitude from CodeCaves.CircleTable; dun.bin's
        /// Run_TrapCircle (0x1DBFA70) jumps here (DunPatches).</summary>
        internal const uint CircleEffects = Host + 0x8;
        /// <summary>ElfFrameChainPatches.PatchCallRequest: a native call the mod asks for (CodeCaves.CallRequest), the tail of the
        /// camera-pin chain (after the WHP bill).</summary>
        internal const uint CallRequest   = Host + 0x610;  // 0x1B4DD0, 116 B → 0x1B4E44 (the circle cave ends at 0x1B4DC4)
        /// <summary>ElfShotPackPatches.PatchPelletSprite: the item id a player pellet's sprite cell is taken from — Mailbox.PelletSpriteId
        /// when set, else the equipped weapon's (draw__5CSHOT's hook at 0x1ABC74 calls it).</summary>
        internal const uint PelletSprite  = Host + 0x690;  // 0x1B4E50, 56 B → 0x1B4E88
        /// <summary>ElfShotPackPatches.PatchPelletPlant: a player pellet's contact plants its damage entry natively — or, for a pellet
        /// whose damage word (pool +0x2E0) is NEGATIVE, plants nothing and simply ends (step__5CSHOT's plant call at 0x1ABE04 lands
        /// here). The mod writes −1 into a pellet that must hurt nothing itself (ZeusShot: the bolt is the hit).</summary>
        internal const uint PelletPlant   = Host + 0x6D0;  // 0x1B4E90, 28 B → 0x1B4EAC
        /// <summary>ElfShotPackPatches.PatchPelletContact: a player pellet's contact test made natively, and every contact recorded in
        /// CodeCaves.PelletContact — the pellet's slot, what it met (3 an enemy, 1 a wall) and the point (an enemy's hit-sphere centre)
        /// — on the engine's own frame (step__5CSHOT's contact call at 0x1ABD88 lands here).</summary>
        internal const uint PelletContact = Host + 0x700;  // 0x1B4EC0, 96 B → 0x1B4F20
        /// <summary>ElfDamagePatches.PatchGemDamage: a thrown gem's burst damage (30 × (dungeon + 1)) multiplied by
        /// CodeCaves.GemDamageFactor on its way into the burst (the item-throw step's SetDmg call for a gem lands here); 0 = vanilla.</summary>
        internal const uint GemDamage     = Host + 0x760;  // 0x1B4F20, 32 B → 0x1B4F40
        /// <summary>ElfConfusePatches.PatchSecondEffect: the dungeon loop's step and draw of the LIVE main-character effect
        /// (dun 0x1DB8740 / 0x1DAEB90, `lw a0,-0x6304(gp); jal Step/Draw__12CSHOT_EFFECT`) land here: the live instance as
        /// before, then the SECOND instance (0x01E97BC0) as well while CodeCaves.SecondEffectLive is set — an effect an
        /// ability borrowed into it beside the character's own (Babel's Spear's shockwave) is stepped and drawn.</summary>
        internal const uint SecondEffectStep = Host + 0x780;  // 0x1B4F40, 64 B → 0x1B4F80
        internal const uint SecondEffectDraw = Host + 0x7C0;  // 0x1B4F80, 64 B → 0x1B4FC0
        /// <summary>ElfFrameChainPatches.PatchBladeSpin: chained after the blade-fall cave (whose two exits jump here instead of
        /// the WHP bill), once a dungeon frame: chara slot 3's yaw += CodeCaves.BladeSpin (radians a frame; 0 = off), wrapped
        /// to ±π — the engine's own frame turns the judgement blade / Babel's spear copy.</summary>
        internal const uint BladeSpin        = Host + 0x800;  // 0x1B4FC0, 112 B → 0x1B5030
        /// <summary>tools/stubs/spear_block.s (ElfWeaponPatches.PatchSpearBlock): Step__12CMonstorUnit's `jal MoveChecMonster` (main
        /// 0x1DE344) lands here — the engine's enemy-versus-enemy block, then the same test against CodeCaves.SpearBlock's sphere
        /// while it is armed: a unit heading into it is turned along it at the same speed, sliding round (Babel's risen spear is solid).</summary>
        internal const uint SpearBlock       = Host + 0x870;  // 0x1B5030, 392 B → 0x1B51B8
        /// <summary>ElfDamagePatches.PatchUngagaNoDrain: CheckDmg's two weapon-HP drain calls (main 0x1DB388 for a landed hit,
        /// 0x1DAE94 for a guarded one) land here. An entry of Ungaga's (owner 4) planted by his charge EFFECT (class word +0x38
        /// non-zero — his swings plant 0) or marked by the mod (+0x9C == CodeCaves.NoDrainMark: Babel's spikes) bills nothing;
        /// everything else goes on to SwordDmgCheck1 as before. The charge's own per-shot bill (0.8, in UngagaKey_Play) stays.</summary>
        internal const uint NoDrainLanded    = Host + 0xA00;  // 0x1B51C0, 72 B → 0x1B5208 (the entry at *NowColData + s6)
        internal const uint NoDrainGuarded   = Host + 0xA50;  // 0x1B5210, 72 B → 0x1B5258 (the entry at *NowColData + s1)
        /// <summary>tools/stubs/player_spear_block.s (ElfWeaponPatches.PatchSpearBlock, hooked by DunPatches): the player's move's two
        /// `jal MoveCheck__12CMonstorUnitFPfPfi` (dun 0x1DB39AC / 0x1DB3E58) land here — the engine's player-versus-enemy block,
        /// then CodeCaves.SpearBlock's sphere while it is armed: velocity into the column is dropped, the part along it kept.</summary>
        internal const uint PlayerSpearBlock = Host + 0xAA0;  // 0x1B5260, 232 B → 0x1B5348
        /// <summary>tools/stubs/shot_spear_block.s (ElfWeaponPatches.PatchSpearBlock): Step__12CSHOT_EFFECT's `jal checkCollision`
        /// (main 0x1AC3E8) lands here — the engine's test, then, for a shot that is not the player's (victim mask ≠ 2) and met
        /// nothing, CodeCaves.SpearBlock's column (floor − 2 … top): a wall hit where the shot stands.</summary>
        internal const uint ShotSpearBlock   = Host + 0xB90;  // 0x1B5350, 268 B → 0x1B545C
        /// <summary>tools/stubs/rock_shadow.s (ElfWeaponPatches.PatchRockShadow, hooked by DunPatches): Draw_MainUnitShadow's
        /// `jal MGEndDrawShadow` (dun 0x1DADDD4) lands here — one extra MGDrawShadowFast for the frame CodeCaves.RockShadow names
        /// while its flag is set (the Terra Sword's boulder), then the displaced call.</summary>
        internal const uint RockShadow       = Host + 0xCA0;  // 0x1B5460, 68 B → 0x1B54A4
        /// <summary>tools/stubs/fall_drive.s (ElfFrameChainPatches.PatchFallDrive): the blade fall's MODE 4 — falling and following,
        /// its stop able to follow a float, and CodeCaves.FallDrive's drive rows — between the blade-fall cave and the spin cave.</summary>
        internal const uint FallDrive        = Host + 0xCF0;  // 0x1B54B0, 364 B → 0x1B561C (the host ends at 0x1B5640)
        /// <summary>tools/stubs/confuse_name.s (ElfConfusePatches.PatchConfuseAbility): the status window's SPECIAL list names ability bit
        /// 14 ("Confuse") from system message 0x45 — NowWeaponStatus's `addiu a0,a3,0x45` (main 0x20B8A8) jumps here.</summary>
        internal const uint ConfuseName      = Host + 0xE5C;  // 0x1B561C, 28 B → 0x1B5638
        /// <summary>An older guard-crush hook target (0x1B5600) that ElfDamagePatches still recognises and re-aims; the live cave is
        /// <see cref="DebugInfoCave.GuardCrush"/>.</summary>
        internal const uint GuardCrushFirst  = Host + 0xE40;
    }

    /// <summary>A cave INSIDE a dead main-ELF function: the body of DebugInfomationDraw (0x1B3780, 3,952 B), the developers'
    /// on-screen debug overlay. ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts turns its first word into `jr ra` — its one
    /// caller (dun.bin's DrawProcess, behind a debug flag) returns at once — and ElfShotPackPatches.PatchSharedShots writes the
    /// cave from +8. The band (<see cref="ElfCave"/>) is
    /// full and may not grow; a dead function's body is the home for a cave of this size.</summary>
    internal static class DebugInfoCave
    {
        internal const uint Host = 0x001B3780, HostSpan = 3952, VanillaWord0 = 0x27BDFE90;   // `addiu sp,sp,-0x170`
        /// <summary>tools/stubs/shared_shots.s: the monster shot pack's five slots shared among every shot config a floor
        /// needs (<see cref="SharedShots"/>, block <see cref="SharedShotBlock"/>). Entry points at fixed offsets: +8 the
        /// dungeon step loop's chain head (DunPatches.CatFollowHookNew), +0x10/+0x18 Step__12CMonstorUnit's two fire sites
        /// (0x1DEED0 / 0x1DEFD8), +0x20 SetupBaseModel's two pack calls (0x1E01B0 / 0x1E0224).</summary>
        internal const uint SharedShots      = Host + 0x8;    // 2,864 B → 0x1B42B8 (PelletSprite follows; the host ends at 0x1B4700)
        internal const uint SharedShotsStep  = Host + 0x8;
        internal const uint SharedShotsFire0 = Host + 0x10;
        internal const uint SharedShotsFire1 = Host + 0x18;
        internal const uint SharedShotsEnter = Host + 0x20;
        /// <summary>An older ISO's pellet-sprite cave; the live one is <see cref="DebugIfCave.PelletSprite"/>. ElfShotPackPatches
        /// recognises a draw__5CSHOT hook that still points here and keeps <see cref="SharedShots"/> below it.</summary>
        internal const uint PelletSprite     = Host + 0xB40;  // 0x1B42C0, 32 B → 0x1B42E0
        /// <summary>tools/stubs/steel_level_up.s: the Steel Slingshot's level-up bonus is +2 endurance and twice the max-WHP
        /// roll — four entries at fixed offsets, one per hooked add in SetLevelUpWeaponData (B endurance, C max WHP) and
        /// WeaponLevelUpValueCalc (D endurance, E max WHP).</summary>
        internal const uint SteelLevelUp     = Host + 0xB60;  // 0x1B42E0, 176 B → 0x1B4390
        /// <summary>tools/stubs/guard_mask.s (ElfDamagePatches.PatchGuardMask): the guard gate's second link — a window whose bit is
        /// set in CodeCaves.GuardMask's byte for the enemy passes; anything else goes on to the cat's guard-bypass cave.</summary>
        internal const uint GuardMask        = Host + 0xC10;  // 0x1B4390, 60 B → 0x1B43CC (AutoGuardMatch at 0x1B43E0)
        internal const uint SteelLevelUpB = SteelLevelUp, SteelLevelUpC = SteelLevelUp + 0x8, SteelLevelUpD = SteelLevelUp + 0x10,
                            SteelLevelUpE = SteelLevelUp + 0x18;
        /// <summary>tools-free, 172 B: AUTO-GUARD, hooked at BtCheckDamageProc's CheckHitUser return. An entry whose
        /// reaction is 5 is consumed there and reported as NO HIT, after stamping the guard spark and ringing the
        /// clang — so the handler never runs at all. That matters beyond the damage: on ANY matched hit, before it
        /// looks at the reaction, the handler zeroes the player's action word (0x1DC4490, which ToanKey_Play reads),
        /// which knocked Toan out of a charge even when the hit did nothing to him.</summary>
        internal const uint AutoGuardMatch = Host + 0xC60;   // 0x1B43E0, 172 B → 0x1B448C (the host ends at 0x1B4700)
        /// <summary>52 B: the STRIDE cave (ElfToanMeleePatches.PatchStrideScale) — scales the player's per-frame move
        /// vector by CodeCaves.StrideScale while motion 33 plays, then tail-jumps into the status check it displaced.</summary>
        internal const uint StrideScale    = Host + 0xD10;   // 0x1B4490, 52 B → 0x1B44C4
        /// <summary>44 B: the CAMERA PIN cave (ElfFrameChainPatches.PatchCameraPin) — the dungeon camera pass's epilogue
        /// jumps here (DunPatches, dun 0x1DBF9BC); while CodeCaves.CameraPin's flag is set the camera's height field
        /// is recomputed every frame so that the camera stays at the pinned WORLD HEIGHT while its follow point rises
        /// and falls with Toan (the Sword of Zeus's lunge); distance and angle stay the engine's. Falls straight
        /// through when the flag is clear.</summary>
        internal const uint CameraPin      = Host + 0xD50;   // 0x1B44D0, 44 B → 0x1B44FC (the host ends at 0x1B4700)
        /// <summary>2 × 24 B: the LUNGE GRAVITY caves (ElfToanMeleePatches.PatchLungeGravity). Toan's charge lunge is a
        /// parabola: ToanKey_Play seeds its vertical speed from the shared 0.1 gravity (ParabolicInitialVector, 40
        /// frames) and the dungeon key process takes 0.1 off it every frame. Both sites go through a cave that scales
        /// that gravity by (1 + CodeCaves.LungeGravityExtra): the flight's height scales with it, its length does
        /// not — the Sword of Zeus's level-2 lunge jumps higher in the same frames. 0 = vanilla.</summary>
        internal const uint LungeGravitySeed = Host + 0xD80;   // 0x1B4500 → main hook (the parabola's seed)
        internal const uint LungeGravityStep = Host + 0xDA0;   // 0x1B4520 → dun hook (the per-frame gravity)
        /// <summary>92 B: the BLADE FALL cave (ElfFrameChainPatches.PatchVerticalDrive), chained after the camera-pin cave so it
        /// runs once a frame at the end of the dungeon camera pass. While CodeCaves.VerticalDrive's flag is 1 it steps the
        /// judgement blade's fall — vy += g, y −= vy, stopped at the floor, where the flag becomes 2 — and writes the
        /// copy's slot height: the fall is the engine's own frame, not a mod thread racing it.</summary>
        internal const uint VerticalDrive      = Host + 0xDC0;   // 0x1B4540, 168 B → 0x1B45E8
        internal const uint WhpBill        = Host + 0xE70;   // 0x1B45F0, 88 B → 0x1B4648 (the host ends at 0x1B46F0)
        /// <summary>tools/stubs/guard_crush.s (ElfDamagePatches.PatchGuardCrush): the GUARD GATE — CheckDmg's guard-window hook lands
        /// here first; an entry carrying CodeCaves.CrushMark passes every guard window, anything else goes on to
        /// <see cref="GuardMask"/> and then the cat's guard-bypass cave.</summary>
        internal const uint GuardCrush     = Host + 0xED0;   // 0x1B4650, 56 B → 0x1B4688
        /// <summary>tools/stubs/follow.s (ElfFrameChainPatches.PatchFollow): CodeCaves.FollowTable's points carried with units, every
        /// frame — between the fall-drive cave and the blade-spin cave.</summary>
        internal const uint Follow         = Host + 0xF10;   // 0x1B4690, 96 B → 0x1B46F0 (the host's end)
    }

    /// <summary>Caves inside two dead main-ELF functions: DebugItemGetKey (0x22B240, 880 B) and DebugItemGetDraw (0x22B5B0, 516 B),
    /// the item menu's debug item-get screen — only ever entered in item-menu sub-mode 5 (0x1D9EC08), which nothing in the
    /// ELF or the overlay sets. ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts makes both return at once (Key with −1, its
    /// "leave" value); ElfConfusePatches writes the caves after.</summary>
    internal static class DebugItemCave
    {
        internal const uint Host = 0x0022B240, HostSpan = 880 + 516, DrawHost = 0x0022B5B0;
        internal const uint KeyWord0 = 0x27BDFFC0, DrawWord0 = 0x27BDFF50;   // `addiu sp,sp,-0x40` / `addiu sp,sp,-0xB0`
        /// <summary>tools/stubs/confuse_proc.s: the Confuse ability's on-hit roll (CheckDmg 0x1DBAA4 jumps here).</summary>
        internal const uint ConfuseProc = Host + 0x8;     // 0x22B248, 160 B → 0x22B2E8
        /// <summary>The resident stars instance stepped / drawn after the second-effect caves (ElfConfusePatches.PatchSecondEffect
        /// ends each in a jump here), behind CodeCaves.StarsGate.</summary>
        internal const uint StarsStep = Host + 0xC0;      // 0x22B300, 144 B → 0x22B390 (the construct check + the gate)
        internal const uint StarsDraw = Host + 0x160;     // 0x22B3A0, 112 B → 0x22B410 (the Key host ends at 0x22B5B0)
        /// <summary>tools/stubs/element_menu.s (ElfElementMenuPatches): the dungeon quick-change menu as the weapon's element picker.
        /// The HEAD holds the hook caves — trig (the overlay's SELECT read, +0), xkey (+0x58), pre (+0xF8), start (+0x118), close (+0x160).</summary>
        internal const uint ElementMenuHead  = Host + 0x1D0;      // 0x22B410, 368 B → 0x22B580 (the Key host ends at 0x22B5B0)
        internal const uint ElementMenuTrig  = ElementMenuHead,          ElementMenuXKey  = ElementMenuHead + 0x58,
                            ElementMenuPre   = ElementMenuHead + 0xF8,   ElementMenuStart = ElementMenuHead + 0x118,
                            ElementMenuClose = ElementMenuHead + 0x160;
        /// <summary>…and its TAIL in the Draw host after the claimed `jr ra; nop`: the "wepicon" name (+0), the draw cave (+0xC) and
        /// the cell test.</summary>
        internal const uint ElementMenuSheetName = DrawHost + 0x8;    // 0x22B5B8, 348 B → 0x22B714 (the Draw host ends at 0x22B7B4)
        internal const uint ElementMenuDraw      = DrawHost + 0x14;   // 0x22B5C4
        internal const uint DrawHostEnd          = DrawHost + 516;
    }

    /// <summary>The dead <c>SmoothRest</c> body (262 zero words from 0x27D084, in the main ELF): the camera-height cave takes its head
    /// (ElfCameraPatches, 0x27D090, 408 B → 0x27D228), the rest is free zero words up to 0x27D49C.</summary>
    internal static class SmoothRestCave
    {
        internal const uint Host         = 0x0027D084;
        internal const uint CameraHeight = 0x0027D090;   // 408 B → 0x27D228 (ElfCameraPatches.PatchNativeCameraPostPass)
        /// <summary>tools/stubs/bait_keep.s (ElfFishingPatches.PatchBaitKeep): EdMoveChara's two bait-loss rolls call <c>rand()</c>
        /// through here; 99 comes back while CodeCaves.BaitKeep is non-zero, so neither roll passes and the bait stays.</summary>
        internal const uint BaitKeep     = 0x0027D230;   // 60 B → 0x27D26C
    }
}
