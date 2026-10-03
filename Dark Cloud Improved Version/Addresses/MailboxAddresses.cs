namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The 4-byte flag slots at 0x01F10000 that the mod WRITES and the PNACH conditionals (and cold-patched
    /// engine code) READ. The page holds no code, so PINE writes here are safe (docs/code-caves.md § PINE write
    /// safety). EVERY slot must be unique — two systems on one slot silently corrupt each other, and the symptom
    /// shows up in the *other* system.
    ///
    ///   +0x00 eventpoint   +0x04 sun/moon    +0x08 nearNPC     +0x0C xiaoFlag
    ///   +0x10 nearNPC(2)   +0x14 insideMayor +0x18 element     +0x1C clock
    ///   +0x20 pnachActive  +0x24 PINE probe (MemoryFunctions)  +0x28 option1   +0x2C option2
    ///   +0x30 option3      +0x34 option4     +0x38 MIRAGE scene gate           +0x3C fish cam height
    ///   +0x40 camera stick (EXTERNAL — off-limits)             +0x44 cape char ptr
    ///   +0x48 line distp below   +0x50-0x5F camera E_prev (EXTERNAL — off-limits)
    ///   +0x60 canal evict  +0x64 camera rest H  +0x68 cam gather count  +0x6C fish wall latch
    ///   +0x70 idle-motion override  +0x74 block ladder  +0x78 refusal requested  +0x7C "!" Y boost
    ///   +0x80 idle-motion flags (bit1 = play-once)
    ///   +0x84 shield block addend  +0x88 shot hit target  +0x8C/+0x90 shield gauge owner/rate
    ///   +0x94-0x9C Mirage haze  +0xA0-0xBC Super Steve icon  +0xC0-0xCC prop pellet follow
    ///   +0xD0-0xDC pellet crush/kick  +0xE8/+0xEC Xiao shot WHP  +0xF0 pellet kick damage
    ///   +0xF4 pellet sprite id  +0xF8/+0xFC flame spacing/owner
    ///   +0x100 = AiStubBase: the page is FULL (<see cref="NextFree"/> is authoritative — this prose is a courtesy copy)
    /// </summary>
    internal static class Mailbox
    {
        // EVERY slot is declared here: claim one by taking NextFree and moving it, and never write a bare
        // 0x21F100xx anywhere else. A map that only exists as prose does not prevent collisions; a constant does.
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

        /// <summary>FISHING CAMERA HEIGHT (float). <c>EdMoveChara</c> hard-codes <c>SetHeight(40.0)</c> for
        /// fishing (`lui $2,0x4220` @0x16C2DC); <see cref="IsoPatcher"/>'s PatchFishingCameraHeight rewrites
        /// those two instructions to load this word instead, so the height becomes per-spot data. 40 = the
        /// vanilla fishing angle (looking down into the water); the Queens CANAL spot uses the standard town
        /// height 5 because there you stand IN the water and the downward view is counterproductive.
        /// ⚠ The patched code reads this EVERY FRAME while fishing, in EVERY town — it must never be 0 or
        /// the camera drops to height 0. Seeded at mod start (MainMenuThread) and re-asserted per tick.</summary>
        internal const long FishCamHeight = Base + 0x3C;

        /// <summary>⚠ RESERVED — NOT mailbox slots. The ISO-baked town-camera collision function
        /// (tools/stubs/town_camera_collision.s, hooked into EdMoveChara) uses guest 0x01F10040 as its
        /// smoothed right-stick scratch (one float, rewritten every camera frame) and 0x01F10050–5F
        /// as its persisted swept-slide origin E_prev. The stub addresses the page directly, bypassing
        /// this allocator, so these bytes are OFF-LIMITS: the mailbox must never hand them out.</summary>
        internal const long CameraStick = Base + 0x40;   // external (town_camera_collision.s) — do not reuse
        internal const long CameraEprev = Base + 0x50;   // external, 16 bytes (0x50-0x5F) — do not reuse

        /// <summary>Player CCharacter ptr for the low-tide cape early-draw. CanalTide arms this alongside the
        /// body's model root (MizuRedrawFramePtr); the capeEarlyDraw cave (IsoPatcher.PatchCapeEarlyDraw,
        /// reached by redirecting the refraction EARLY_STUB's `jal MGDraw`) reads it to walk char+0xC74 and
        /// Draw__6CCloth each cloth piece EARLY — so the cape survives the falls' Z-write like the body.
        /// The cave bakes the guest form 0x01F10044.</summary>
        internal const long CapeCharPtr = Base + 0x44;

        /// <summary>Fishing rope BELOW-bobber rest length (float). The split caves (IsoPatcher.PatchFishLineSplit)
        /// select this vs the existing distp @0x202A1FA4 (=above) per segment at anchor 18, so hook depth
        /// (bobber→hook) is tuned independently of cast reach (rod→bobber). The cave bakes the guest form
        /// 0x01F10048. Mod seeds/tunes it while fishing; MUST be > 0 (0 collapses the hang).</summary>
        internal const long LineDistpBelow = Base + 0x48;

        /// <summary>Canal tide-evict flag — the mod maintains the flag, native code owns the timing.
        /// CanalTide writes 1 the instant the tide turns while the player is caught in the drained Queens
        /// canal; the EdFadeInOut fade-hook (IsoPatcher.PatchCanalEvictFadeHook, stub @<see cref="ElfCave.CanalEvictFadeHook"/>)
        /// reads it on the exact fully-black frame, requests the _MAP_JUMP to the East Harbor dock, then clears it.
        /// The fade-hook bakes the guest form 0x01F10060 (tools/stubs/canal_evict_fade_hook.s) — keep in sync.</summary>
        internal const long CanalEvict = Base + 0x60;

        /// <summary>Town-camera RESTING eye height (float), read EVERY frame by the town-camera collision fn
        /// (town_camera_collision.s: `lw $t0,0x24($t3)` where $t3=0x01F10040 → this word @0x01F10064) as its
        /// REST_H height target: the fishing rest height is a TARGET the camera eases to, not a per-frame
        /// SetHeight clamp. The mod (CustomFishingSpot.PinFishCamHeight) writes the town rest (5) normally
        /// and the active spot's fishing height while a session is live. ⚠ Read every frame in EVERY town —
        /// seeded at startup and re-asserted per tick; a 0 here would drop the camera to the pivot.</summary>
        internal const long CameraRestH = Base + 0x64;

        /// <summary>TRUE per-frame camera-gather CCPoly count (int), written by the cameraNormSide stub
        /// (tools/stubs/camera_norm_side.s) from $s8 at town-camera cave entry. ⚠ The WorkBuffer struct's
        /// `used` field is NOT a fill level — EdMoveChara resets it and Alloc(2000) re-reserves the whole
        /// buffer every frame, so `used`==2000 merely means "the gather ran". Only updates while the town
        /// camera cave runs (stale in menus/dungeons).</summary>
        internal const long CamGatherCount = Base + 0x68;   // external (camera_norm_side.s) — do not reuse

        /// <summary>Per-cast wall latch for the Queens FishLineClamp (camera_norm_side.s): written by
        /// the cave while NOT casting — 1 = the bobber dangles inside the canal region (|z|&lt;60, floor-
        /// spot stances) so the flight wall clamp arms; 0 = bank stance, walls stay off (no line snap).</summary>
        internal const long FishWallLatch = Base + 0x6C;

        /// <summary>Town-character idle-motion override (the swapped-in cat's idle→sit). The ELF cave
        /// <c>ElfPatches.PatchIdleMotionOverride</c> intercepts EdMoveChara's grounded LOCOMOTION store
        /// <c>*(char+0xc68) = motion</c> (0 = idle / 1 = run / 2 = walk, @0x16a6a8): when the motion the
        /// engine computed is 0 (idle) AND this word is non-zero, the cave stores THIS value instead (e.g.
        /// the sit motion index), so an idle town character plays the override animation. Run/walk (1/2) and
        /// a 0 here pass through unchanged — vanilla. The cave reads the GUEST form 0x01F10070; the mod
        /// writes MMU 0x21F10070 (= guest + 0x20000000). 0 = off. Owned by the mod's idle→sit timer logic.</summary>
        internal const long IdleMotionOverride = Base + 0x70;

        /// <summary>Town ladder-mount block (the swapped-in non-Toan ally must never climb — the mount
        /// loads a Toan-rigged climb overlay onto a foreign model → crash). The ELF cave
        /// <c>ElfPatches.PatchLadderRefusal</c> redirects EdMoveChara's single ladder-mount call
        /// (<c>jal EdInitHashigo</c> @0x16c0fc) plus the climbing-flag set (<c>li s8,1</c> @0x16c104) to a
        /// cave: when this word is 0 it mounts exactly as vanilla (calls EdInitHashigo + sets the climbing
        /// flag s8=1 → DAT_01d1970c); when non-zero it SKIPS both (no mount, s8 stays -1 = not climbing) and
        /// raises <see cref="RefusalRequested"/>. The cave reads the GUEST form 0x01F10074; the mod writes MMU
        /// 0x21F10074 (= guest + 0x20000000). 0 = off (vanilla ladders). Set once per town by the swap logic.</summary>
        internal const long BlockLadder = Base + 0x74;

        /// <summary>Ladder-refusal request (one-shot). The <c>PatchLadderRefusal</c> cave sets this to 1 when
        /// (and only when) a mount was actually attempted-and-blocked — i.e. under the SAME PadDown(Cross)
        /// press condition that would have mounted in vanilla, so it fires once per Cross press, not every
        /// frame the ally merely stands by the ladder. The mod polls MMU 0x21F10078, plays the shake-head
        /// refusal, then clears it back to 0. Only <see cref="BlockLadder"/> being non-zero can raise it.</summary>
        internal const long RefusalRequested = Base + 0x78;

        /// <summary>Player "!" event-trigger mark HEIGHT boost (float). A swapped-in ally with different
        /// proportions (the cat) sits lower, so the exclamation mark pokes through its mesh. The ELF cave
        /// <c>ElfPatches.PatchExclamationHeight</c> redirects the PLAYER mark's final Y store in
        /// <c>EdDrawSysCursor</c> (<c>swc1 f0,0x94(sp)</c> @0x17cf5c, the store of
        /// <c>fStack_c + *(Chara+0xb4) + 3.0 + sinf(a)*0.5</c>) to a cave that adds THIS word to the Y before
        /// storing it — so the mark rides `vanilla Y + boost`. The cave reads the GUEST form 0x01F1007C; the
        /// mod writes MMU 0x21F1007C (= guest + 0x20000000). 0.0 = vanilla (bit-exact for real positions);
        /// a positive float lifts the cat's mark clear. NPC cursors (the earlier loop) are untouched. Owned by
        /// the ally-swap logic: seed 0.0 for Toan, the cat's clearance for the cat.</summary>
        internal const long ExclamationYBoost = Base + 0x7C;

        /// <summary>Motion FLAGS the idle-motion cave writes to char+0xc64 alongside an override
        /// (<see cref="IdleMotionOverride"/>). CCharacter::Step reads +0xc64 as native playback flags —
        /// bit0 (1) = freeze, bit1 (2) = PLAY ONCE then hold the LAST frame (the engine's own one-shot,
        /// used by the land animation), bit2 (4) = restart from the clip start (self-clearing). 0 = loop
        /// (vanilla). Only applied while an override index is armed; run/walk frames keep the vanilla
        /// zeroing. Contract: the mod arms flags WITH the index and zeroes BOTH on release — a stray
        /// (index=0, flags=2) combo freezes idle on its last frame. Cave reads GUEST 0x01F10080; mod
        /// writes MMU 0x21F10080. Sit = 0 (loops); refusal = 2 (one shake, hold neutral).</summary>
        internal const long IdleMotionFlags = Base + 0x80;

        /// <summary>ENEMY-VS-PLAYER BLOCK ADDEND (float). <c>CMonstorUnit::MoveCheck2</c> (0x1DCDD0) stops an
        /// enemy's scripted movement when its next position is within (its move radius +0x1E414 + 6.0) of
        /// the player; the 6.0 is a per-site immediate (`lui $v1,0x40c0; mtc1 $v1,$f1` @0x1DCFD0).
        /// AngelGear.ArmBlockPatch rewrites those two words (cold) to load THIS word instead, so the
        /// block distance becomes data: 6.0 = vanilla, RingRadius while Angel Gear's shield is up (enemies
        /// and their scripted lunges stop at the slingshot). Cave reads GUEST 0x01F10084. ⚠ Read every
        /// enemy step in every dungeon once armed — must never be 0/garbage; seeded 6.0 at arm.</summary>
        internal const long ShieldBlockAddend = Base + 0x84;

        /// <summary>SHOT-VS-PLAYER TARGET POINTER. <c>checkCollision</c> (0x1AB740) is every shot's "did I hit
        /// the player" test; it loads her position global with `lui $v0,0x1ea; addiu $a1,$v0,0x1d30` @0x1AB828.
        /// AngelGear.ArmShotPatch rewrites those two words (cold) to `lui $a1,HI; lw $a1,LO($a1)` — a
        /// POINTER read from this word: 0x01EA1D30 (vanilla) or, while Angel Gear's shield is solid, the copy's
        /// pouch-node world translation (engine-refreshed every draw) — so enemy shots collide with the POUCH
        /// natively, no per-frame writes. Cave reads GUEST 0x01F10088. ⚠ Read by every enemy shot every frame
        /// once armed — must always hold a valid vec4 address; seeded to the player global at arm.</summary>
        internal const long ShotHitTarget = Base + 0x88;

        /// <summary>Angel Gear shield: who drives <see cref="ShieldGaugeRate"/>. 0 = nobody — the pnach re-seeds
        /// the rate to 1.5 (vanilla) every frame; 1 = the app (shield up / broken) owns it. Guest 0x01F1008C.</summary>
        internal const long ShieldGaugeOwner = Base + 0x8C;
        /// <summary>Xiao's ATTACK-GAUGE REFILL MULTIPLIER (float). The ISO's dun.bin patch (DunPatches) makes the
        /// overlay refill `gauge += max(1, speed/30) × THIS` instead of the immediate 1.5 (@0x1DB8090/94).
        /// 1.5 = vanilla, 0 = hold (the shield's HP bar), small = slow refill after a break. Guest 0x01F10090.
        /// A runtime-written word, so it lives on this code-free page and never in the ELF cave segment.</summary>
        internal const long ShieldGaugeRate = Base + 0x90;

        /// <summary>Mirage's heat shimmer, drawn by ElfCave.MirageHazeDraw at the clone: 1 = draw it (written LAST);
        /// the clone's root CFrame (guest) whose posed world translation places it; a height added to that
        /// (negative lowers it — the raster is built to rise above its anchor).</summary>
        internal const long MirageHazeOn   = Base + 0x94;
        internal const long MirageHazeNode = Base + 0x98;
        internal const long MirageHazeLift = Base + 0x9C;
        /// <summary>Super Steve's sphere icon on the dungeon HUD: 1 = on (written LAST); the screen position and drawn
        /// size ElfCave.SuperSteveIconDraw draws at. The icon itself is kept in itempack's spare cell (64, 32) by
        /// ElfCave.SuperSteveIconCopy from the equipped record alone — nothing in that path reads the mailbox, so a
        /// save loaded straight into a dungeon is served by the entry-time copy. Steve — the equipped weapon's
        /// icon — is at (29, 388), 32 × 32.</summary>
        internal const long SsIconOn    = Base + 0xA0;
        internal const long SsIconX     = Base + 0xA4;
        internal const long SsIconY     = Base + 0xA8;
        internal const long SsIconSize  = Base + 0xAC;
        /// <summary>Counters the two caves bump — the draw cave's draws issued; the copy cave's calls, calls with wepicon
        /// registered, and copies issued — reported once in SuperSteve.DriveSphereIcon's log line.</summary>
        internal const long SsIconDiagDraws      = Base + 0xB0;
        internal const long SsIconDiagCopyCalls  = Base + 0xB4;
        internal const long SsIconDiagSheetSeen  = Base + 0xB8;
        internal const long SsIconDiagCopies     = Base + 0xBC;
        /// <summary>A chara-slot prop riding one of Xiao's pellets (ElfCave.PropPelletFollow; SlingshotProp in projectile
        /// mode on chara slot 3): the pellet slot + 1 (written LAST; 0 = off), a height lift, a yaw added per frame, and
        /// the cave's "the pellet ended" word (it clears the slot itself; the mod fades the prop).</summary>
        internal const long PropFollowSlot  = Base + 0xC0;
        internal const long PropFollowLift  = Base + 0xC4;
        internal const long PropFollowSpin  = Base + 0xC8;
        internal const long PropFollowEnded = Base + 0xCC;
        /// <summary>The Matador's charged pellet, for DunCave.CatGuardBypass: a Xiao-owned damage entry whose base damage
        /// (+0x34) equals this word passes an enemy's guard window. 0 = no charged pellet out.</summary>
        internal const long PelletCrushDamage = Base + 0xD0;
        /// <summary>…and the kick that cave stamps on it (step__5CSHOT plants pellets with none): strength and decay,
        /// as SetKickBack takes them (Goro's hammer swing: 2.5 / 0.1).</summary>
        internal const long PelletKickStrength = Base + 0xD4;
        internal const long PelletKickDecay    = Base + 0xD8;
        /// <summary>…and the kick's origin (x, height, y): CheckDmg shoves along (enemy point − origin), so a point well behind
        /// the pellet on its flight line makes the shove follow the flight. For a PelletKickDamage entry the cave reads it as an
        /// OFFSET from the entry's own sphere centre instead (Dragon's Y: back along the flight; BigBangShot/Baselard: 0).</summary>
        internal const long PelletKickOrigin   = Base + 0xDC;
        /// <summary>A pellet-planted entry (Xiao-owned) with THIS base damage gets the kick above WITHOUT passing the guard
        /// window — Dragon's Y's ball. 0 = none.</summary>
        internal const long PelletKickDamage   = Base + 0xF0;
        /// <summary>Xiao's per-shot WHP factor (float): the ISO's dun.bin patch (DunPatches) makes her fire routine pass THIS
        /// to SwordDmgCheck1 instead of its immediate 1.0 — 1.0 = vanilla, ChargedShotWhp's 1.5 / 2.25 while a charged shot
        /// is held. While the owner word is 0 (the app is not running it) the PNACH re-seeds 1.0 every frame.</summary>
        internal const long XiaoShotWhpFactor = Base + 0xE8;
        internal const long XiaoShotWhpOwner  = Base + 0xEC;
        /// <summary>The item id whose basefx01 cell every player pellet is drawn as (DebugInfoCave.PelletSprite, hooked into
        /// draw__5CSHOT): Super Steve carrying a slingshot's SynthSphere shows that slingshot's pellet. 0 = the equipped
        /// weapon's (vanilla).</summary>
        internal const long PelletSpriteId    = Base + 0xF4;
        /// <summary>Osmond's flamethrower (CSHOT_FIREBAR, gun mode 2: the Blessing Gun and the Skunk): the spacing of its 24
        /// flame particles along the aim, in units — the reach is 23 × this. The ISO's ELF patch makes Set and Init read it
        /// here instead of their immediate 2.0; the PNACH re-seeds 2.0 every frame while the owner word is 0, the mod sets
        /// the owner to 1 and writes 4.0 while the Skunk is equipped (Skunk).</summary>
        internal const long FlameSpacing      = Base + 0xF8;
        internal const long FlameSpacingOwner = Base + 0xFC;
        /// <summary>The next unclaimed slot. Take it, then MOVE THIS — the whole point of the map.</summary>
        internal const long NextFree = Base + 0x100;  // ⚠ FULL: +0x100 = AiStubBase — the next runtime word goes to the free band below the ELF caves (CodeCaves, 0x21FAF4B0..)
    }
}
