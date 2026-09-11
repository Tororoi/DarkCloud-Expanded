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
    /// ── MAP (guest) ───────────────────────────────────────────────────────────────────────────────────
    /// Everything below 0x01FB4300 lives in ONE proven-clean heap tail (CodeCaveScanner, 68 sessions).
    /// It is now packed so that the clone's mesh cave fits the LARGEST character (Goro), which makes all six
    /// clonable. The room came from three places: capping the AI stubs at 32 slots, trimming the node pool
    /// 128→96 bones (Osmond's 84 is the real max), and packing the decoy tables out of a 0x10000 hole.
    ///
    ///   0x01F10000  PNACH mailbox — 4-byte flag slots, see <see cref="Mailbox"/> (its NextFree marks the
    ///               next unclaimed slot — do not trust any prose copy of it)
    ///   0x01F10040  Town-camera scratch: stick ease @+0x00, E_prev quad @+0x10 (16B) — written per frame
    ///               by the ISO-baked camera function (boot-zeroed; moved here off its code page, 2026-08)
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
    /// 0x01FB0000–0x01FB2000 (the hijacked phdr3 — see <see cref="ElfCave"/>). Loader-loaded at boot,
    /// so direct j/jal into it is legal; the scanner's heap-tail claim already covers it, and THIS FILE
    /// is its only registry. (The caves USED to live in 0x228BB0–0x22A210, believed-dead CharaChange
    /// code — which turned out to be the LIVE dungeon character-change screen. Never again.)
    /// ⚠ PAGE-ISOLATION RULE: the segment's HOST PAGES (16KB granularity — Apple Silicon; 4KB on Intel)
    /// must contain NO runtime-written data. Once any cave on a page executes, PCSX2 compiles and
    /// WRITE-PROTECTS that page; the next PINE write to any address sharing it SIGBUSes the PINE server
    /// thread — a hard crash. This happened: the segment's first home 0x01FAE700 shared its page with
    /// the live water-redraw mailboxes @0x01FAE600-610 → PINE SIGBUS at 0x1FAE60C every Queens session.
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
        ///   +0x30 option3      +0x34 option4     +0x38 MIRAGE scene gate           +0x3C fish cam height
        ///   +0x40 camera stick (EXTERNAL — off-limits)             +0x44 cape char ptr
        ///   +0x48 line distp below   +0x50-0x5F camera E_prev (EXTERNAL — off-limits)
        ///   +0x60 canal evict  +0x64 camera rest H  +0x68 cam gather count  +0x6C fish wall latch
        ///   +0x70 idle-motion override  +0x74 block ladder  +0x78 refusal requested  +0x7C "!" Y boost
        ///   +0x80 idle-motion flags (bit1 = play-once)
        ///   +0x84.. FREE (<see cref="NextFree"/> is authoritative — this prose is a courtesy copy)
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

            /// <summary>FISHING CAMERA HEIGHT (float). <c>EdMoveChara</c> hard-codes <c>SetHeight(40.0)</c> for
            /// fishing (`lui $2,0x4220` @0x16C2DC); <see cref="IsoPatcher"/>'s PatchFishingCameraHeight rewrites
            /// those two instructions to load this word instead, so the height becomes per-spot data. 40 = the
            /// vanilla fishing angle (looking down into the water); the Queens CANAL spot uses the standard town
            /// height 5 because there you stand IN the water and the downward view is counterproductive.
            /// ⚠ The patched code reads this EVERY FRAME while fishing, in EVERY town — it must never be 0 or
            /// the camera drops to height 0. Seeded at mod start (MainMenuThread) and re-asserted per tick.</summary>
            internal const long FishCamHeight = Base + 0x3C;

            /// <summary>Canal tide-evict flag. CanalTide writes 1 the instant the tide turns while the player is
            /// caught in the drained Queens canal; the EdFadeInOut fade-hook (IsoPatcher.PatchCanalEvictFadeHook,
            /// stub @<see cref="ElfCave.CanalEvictFadeHook"/>) reads it on the exact fully-black frame and requests
            /// the _MAP_JUMP to the East Harbor dock, then clears it. So the mod only maintains the flag — native code owns the timing.</summary>
            /// <summary>⚠ RESERVED — NOT a mailbox slot. The ISO-baked town-camera collision function
            /// (tools/stubs/town_camera_collision.s, hooked into EdMoveChara) uses guest 0x01F10040 as its
            /// smoothed right-stick scratch (one float, rewritten every camera frame) and 0x01F10050–5F
            /// as its persisted swept-slide origin E_prev. Those functions grabbed the page directly,
            /// bypassing this allocator, so these bytes are OFF-LIMITS: the mailbox must never hand them
            /// out. CanalEvict USED to live at 0x40 and collided — a non-zero stick value read as a set
            /// evict flag false-warped the player to the dock (and the mod's per-tick flag writes stomped
            /// the camera's stick). CanalEvict now lives at 0x60, past E_prev.</summary>
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
            /// 0x01F10048. Mod seeds/tunes it while fishing; MUST be > 0 (0 collapses the hang). See
            /// game_data/docs/fishing-line-split-and-cast-feasibility.md.</summary>
            internal const long LineDistpBelow = Base + 0x48;

            /// <summary>Canal tide-evict flag (relocated from 0x40 — see <see cref="CameraStick"/> for why).
            /// CanalTide writes 1 the instant the tide turns while the player is caught in the drained Queens
            /// canal; the EdFadeInOut fade-hook (IsoPatcher.PatchCanalEvictFadeHook, stub @<see cref="ElfCave.CanalEvictFadeHook"/>)
            /// reads it on the exact fully-black frame, requests the _MAP_JUMP to the East Harbor dock, then clears it.
            /// The fade-hook bakes the guest form 0x01F10060 (tools/stubs/canal_evict_fade_hook.s) — keep in sync.</summary>
            internal const long CanalEvict = Base + 0x60;

            /// <summary>Town-camera RESTING eye height (float), read EVERY frame by the town-camera collision fn
            /// (town_camera_collision.s: `lw $t0,0x24($t3)` where $t3=0x01F10040 → this word @0x01F10064) as its
            /// REST_H height target. Data-driven so the fishing rest height is a TARGET the camera eases to, not
            /// EdMoveChara's per-frame SetHeight clamp (which desynced the swept-slide and pinned the distance in
            /// with no recovery). The mod (CustomFishingSpot.PinFishCamHeight) writes the town rest (5) normally
            /// and the active spot's fishing height while a session is live. ⚠ Read every frame in EVERY town —
            /// seeded at startup and re-asserted per tick; a 0 here would drop the camera to the pivot.</summary>
            internal const long CameraRestH = Base + 0x64;

            /// <summary>TRUE per-frame camera-gather CCPoly count (int), written by the cameraNormSide stub
            /// (tools/stubs/camera_norm_side.s) from $s8 at town-camera cave entry. ⚠ The WorkBuffer struct's
            /// `used` field is NOT a fill level — EdMoveChara resets it and Alloc(2000) re-reserves the whole
            /// buffer every frame, so `used`==2000 merely means "the gather ran" (the old Queens
            /// "used=2000 = saturated" dumps measured the alloc, not the count). Only updates while the town
            /// camera cave runs (stale in menus/dungeons).</summary>
            internal const long CamGatherCount = Base + 0x68;   // external (camera_norm_side.s) — do not reuse

            /// <summary>The next unclaimed slot. Take it, then MOVE THIS — the whole point of the map.</summary>
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

            // 0x70-0x7C were retired freeze-hunt diagnostic mailboxes (alloc probe / breadcrumb / shadow-skip);
            // those ELF hooks + their mod-side readers were removed 2026-09, and the slots were RECLAIMED by the
            // town-swap behavior mailboxes above/below (0x70 idle, 0x74/0x78 ladder, 0x7C "!" boost).

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
            /// GuardianReflector.ArmBlockPatch rewrites those two words (cold) to load THIS word instead, so the
            /// block distance becomes data: 6.0 = vanilla, RingRadius while Angel Gear's shield is up (enemies
            /// and their scripted lunges stop at the slingshot). Cave reads GUEST 0x01F10084. ⚠ Read every
            /// enemy step in every dungeon once armed — must never be 0/garbage; seeded 6.0 at arm.</summary>
            internal const long ShieldBlockAddend = Base + 0x84;

            /// <summary>SHOT-VS-PLAYER TARGET POINTER. <c>checkCollision</c> (0x1AB740) is every shot's "did I hit
            /// the player" test; it loads her position global with `lui $v0,0x1ea; addiu $a1,$v0,0x1d30` @0x1AB828.
            /// GuardianReflector.ArmShotPatch rewrites those two words (cold) to `lui $a1,HI; lw $a1,LO($a1)` — a
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
            /// ⚠ LIVES HERE, NOT IN THE ELF CAVE SEGMENT: a PINE write into a page holding executed cave code
            /// SIGBUSes PCSX2 (2026-09-09 crash when the word was at 0x01FB0D90). The mailbox page holds no code.</summary>
            internal const long ShieldGaugeRate = Base + 0x90;

            /// <summary>The next unclaimed slot. Take it, then MOVE THIS — the whole point of the map.</summary>
            /// <summary>Divine Beast cat ↔ the native pellet catcher/follower (ElfCave.CatPelletFollow; DivineBeastCat.cs).
            /// The cat copy sits resident and hidden in chara slot 1. At the charge threshold the mod writes the growth
            /// reciprocal and the head rest offset (cat space × cat scale), zeroes frames/slot, and sets state 3 (waiting);
            /// the cave then binds the next NEW pellet on its birth frame (slot+1, state 1, opacity 128) and owns slot 1's
            /// position and scale every frame until that pellet ends (slot 0, state 2 → the mod fades and re-hides).</summary>
            internal const long CatPelletSlot  = Base + 0x94;   // int, bound pellet slot + 1; 0 = none (page boots zero-filled)
            internal const long CatState       = Base + 0x98;   // int: 0 idle, 1 following, 2 pellet ended, 3 waiting for a new pellet
            internal const long CatGrowFrames  = Base + 0x9C;   // int, frames since bound (cave increments)
            internal const long CatGrowInv     = Base + 0xA0;   // float, 1 / growth frames
            internal const long CatHeadX       = Base + 0xA4;   // float ×3: head rest offset in CAT space (x, height, z)
            internal const long CatHeadH       = Base + 0xA8;
            internal const long CatHeadZ       = Base + 0xAC;
            internal const long CatSeenMask    = Base + 0xB0;   // int, active pellet-slot bits last frame (cave)
            // States 4 (falling) / 5 (landed) / 6 (running): at full size the cave breaks the cat away from the pellet
            // (expiring it), falls it with the pellet's forward speed, snaps it to CatFloorH on the landing frame and,
            // once the mod sets state 6, runs it at ½ the pellet speed toward CatTargetPtr (or straight).
            internal const long CatVx          = Base + 0xB4;   // float ×3: fall velocity, captured at the breakaway (cave)
            internal const long CatVh          = Base + 0xB8;
            internal const long CatVz          = Base + 0xBC;
            internal const long CatGravity     = Base + 0xC0;   // float, units/frame² (mod)
            internal const long CatFloorH      = Base + 0xC4;   // float, landing height (mod)
            internal const long CatRunSpeed    = Base + 0xC8;   // float, ½·|pellet horizontal speed| (cave)
            internal const long CatTargetPtr   = Base + 0xCC;   // uint, guest address of the target's position vector, 0 = none (mod)
            internal const long CatDirX        = Base + 0xD0;   // float ×2: unit run direction (cave; the mod faces the cat along it)
            internal const long CatDirZ        = Base + 0xD4;
            internal const long CatGrowN       = Base + 0xD8;   // int, growth frames (mod)
            // State 5 (landing): the land clip plays straight through; the cave reads the copy's live motion frame (slot 1
            // +0xC20 → MOTION_TYPE +0x10) and keeps the fall's forward momentum until the paws-touch frame, then runs the
            // moment the clip reaches its end (or wraps).
            internal const long CatLandStopFrame = Base + 0xDC; // float, clip frame where the paws touch — momentum stops (mod)
            internal const long CatLandEndFrame  = Base + 0xE0; // float, clip end frame — straight into the run (mod)
            internal const long CatPrevFrame     = Base + 0xE4; // float, motion frame seen last time (cave; wrap detection)
            internal const long CatLandLead      = Base + 0xE8; // float, frames before the predicted touchdown at which the land clip starts (mod)
            internal const long CatMoveKey       = Base + 0xEC; // int, motion key played while moving after the landing (mod: the brisk walk)
            internal const long CatMoveFrac      = Base + 0xF0; // float, ground speed after the landing as a fraction of the pellet's speed (mod)
            internal const long CatProbeUp       = Base + 0xF4; // float, floor probe reach above the cat (mod)
            internal const long CatProbeDown     = Base + 0xF8; // float, floor probe reach below the cat (mod)
            internal const long CatProbeFront    = Base + 0xFC; // float, extra floor cast this far AHEAD of the root along its direction (mod)
            internal const long CatProbeBack     = Base + 0x100; // float, … and this far BEHIND; the cat stands on the highest of the three casts
            // Clip rate while moving: the cave writes slot +0xC60 (motion-speed override) = min(base + perSpeed · ground
            // speed, max) — the town's own walk mapping for this rig (EdMoveChara: 0.8·(0.2 + stick) capped 0.85 with
            // ground 1.6·stick → 0.16 + 0.5·ground). Not planted feet; the ratio the designers tuned. A forward wall
            // probe stops the cat (CatBlocked = 1, idle key).
            internal const long CatRateBase      = Base + 0x104; // float, clip rate at zero speed (mod: 0.16)
            internal const long CatRatePerSpeed  = Base + 0x108; // float, clip rate per unit of ground speed (mod: 0.5)
            internal const long CatRateMax       = Base + 0x10C; // float, clip rate cap (mod: 0.85)
            internal const long CatReserved110   = Base + 0x110; // (was the run key; unused)
            internal const long CatIdleKey       = Base + 0x114; // int, stand key (mod)
            internal const long CatBlocked       = Base + 0x118; // int, 1 while a wall stops the cat (cave)
            internal const long NextFree = Base + 0x11C;
        }

        // ── ELF-BAKED CAVES — the mod's own PT_LOAD segment (hijacked phdr3) ─────────────────────────
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
        /// ⚠ PAGE ISOLATION IS LOAD-BEARING. The segment starts 16KB-ALIGNED, and its host pages
        /// [0x1FB0000, 0x1FB4000) (16KB granularity — Apple Silicon; 4KB on Intel) must NEVER hold
        /// runtime-written data: once any cave on a page executes, PCSX2 compiles + WRITE-PROTECTS the
        /// page, and the app's next PINE write to anything sharing it SIGBUSes the PINE server thread —
        /// a hard crash. This happened (2026-09): the segment's first home 0x01FAE700 shared its 4KB page
        /// with the live mizu mailboxes @0x01FAE600-610; QueensSpray ran every Queens frame, and the next
        /// MizuRedrawTexGroup write faulted at 0x1FAE60C. So 0x1FB2000..0x1FB4000 is reserved for future
        /// SEGMENT growth or ISO-baked read-only data ONLY — never hand it out as a runtime mailbox/cave.
        /// The nearest runtime-written words: BobberPtr @0x01FB4000 (its own 4KB and 16KB page) above,
        /// the mizu mailboxes / MeshCave margin below 0x01FAE614.
        ///
        /// ⚠ THE OLD HOME 0x228BB0–0x22A210 IS LIVE DUNGEON CODE — NEVER PLACE ANYTHING THERE AGAIN.
        /// It was believed dead ("the CharaChange screen this mod never reaches"), but it is the dungeon
        /// SELECT quick-menu's character-change screen: CharaChangeLoop @0x228BB0 / CharaChangeKey @0x228E90 /
        /// CharaChangeDraw @0x229740, CALLED FROM THE dun.bin OVERLAY (file offset 0x1DD0) — which is why
        /// main-ELF-only xref analysis mislabeled it unreachable. The caves overwrote it and broke the
        /// dungeon SELECT menu (2026-09). The region must stay byte-for-byte VANILLA.
        ///
        /// ⚠ THE OVERLAP FAILURE MODE IS ALSO REAL: PatchIdleMotionOverride was first placed at what is now
        /// FishLineSplit+0x40 — inside fishlineSplitCaves.bin — and every Queens fishing session hung on a
        /// black screen. It byte-verified cleanly because the check ran on a VANILLA ELF, where the fishline
        /// bin doesn't exist yet. So: claim <see cref="NextFree"/>, keep this table in ADDRESS ORDER with the
        /// cave's SIZE and END, and never place a cave from a patch-local literal. Bin-backed sizes are the
        /// .bin file's byte size (Resources/isoPatch); hand-built sizes are the instruction-word count × 4.
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
        ///   0x1FB0D90  FREE → 0x1FB2000 (segment end, ~0x1270 B)
        /// </summary>
        internal static class ElfCave
        {
            /// <summary>Guest bounds of the hijacked-phdr3 segment; RegionEnd − RegionStart is its p_filesz/p_memsz.
            /// RegionStart must stay 16KB-aligned (page isolation — see the class doc) and 0x80-aligned (p_align).</summary>
            internal const uint RegionStart = 0x01FB0000;
            internal const uint RegionEnd   = 0x01FB2000;
            /// <summary>ELF-file offset the segment loads from (span RegionEnd−RegionStart, zero-filled at patch
            /// time; formerly .reldun debug bytes — outside every phdr's file extent, never read at runtime).</summary>
            internal const uint SegmentFileOff = 0x002AF000;

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
            /// @0x1FB0550 (jal'd from 0x16D314), the v10 settled-gated bobber cave @0x1FB0910, and the
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
            /// armed (<see cref="Mailbox.CatState"/> = 3) binds chara slot 1 to the next NEW pellet on its birth frame,
            /// then places it every frame (head on the pellet, growth scale, sprite fade) until that pellet ends.</summary>
            internal const uint CatPelletFollow    = 0x01FB0D90;   // 2036 B → 0x1FB1584 (frame 0x80, sq/lq saves)

            /// <summary>The next unclaimed spot. Take it, then MOVE THIS — and add the cave to the table above
            /// (address order, size, end) so the next placement can see it.</summary>
            internal const uint NextFree = 0x01FB1590;   // ⚠ code pages: never a runtime-written data word (PINE SIGBUS) — use the Mailbox
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

        // ── 0x21FB4000 .. 0x21FB4300 (guest 0x01FB4000, 0x300 B, top of the MeshCave margin) ─────────────
        // +0x00 (4 B) NOW HOLDS the shallow-fishing bobber-anchor global (TownAddresses.FishLineShallow.BobberPtr):
        //   the cold-patched FishLineStep reads game-addr 0x01FB4000 for the bobber's point address, and a data
        //   write here toggles vanilla point[18] vs shallow point[20]. The rest of the block is spare. (It once
        //   held the old ClsMes catch/menu scratch, now baked into each town's mes by IsoPatcher.) Inside the
        //   CodeCaveScanner ModReserved heap-tail claim (0x1F10000..0x1FB4300), so the sweeper still shows it clean.

        // ── 0x21FB0000 .. 0x21FB2000: the ELF-baked cave SEGMENT (<see cref="ElfCave"/>) ─────────────────
        // Loader-loaded CODE from the hijacked phdr3 — no runtime writes belong here, or ANYWHERE on its
        // 16KB host pages 0x21FB0000..0x21FB4000 (page isolation — a PINE write to a page holding compiled
        // code SIGBUSes PCSX2's PINE thread; see the ElfCave doc). 0x21FB2000..0x21FB4000 is therefore
        // reserved for future segment growth / ISO-baked read-only data ONLY.
        //
        // ── FREE: 0x21FAE614 .. 0x21FB0000 (~0x19EC B) ───────────────────────────────────────────────────
        // What remains of the MeshCave margin below the ELF cave segment — the last heap-tail span still
        // free for RUNTIME data (its pages already carry runtime-written words: mizu mailboxes, MeshCave).
        // Inside the CodeCaveScanner ModReserved heap-tail claim (0x1F10000..0x1FB4300), so it stays clean.

        /// <summary>Town water "submerged tint" redraw — see <c>IsoPatcher.PatchWaterRedraw</c> /
        /// <c>PatchDrawWaterCompaction</c>. The redraw CODE lives baked inside MainDraw/DrawWater's own
        /// ELF footprint (the draw is MOVED to after the character, not duplicated — a duplicate call
        /// overflowed the shared per-frame VIF1 packet buffer and crashed); these two words are plain
        /// runtime DATA the baked code reads/writes, so BSS is fine here (unlike code, data doesn't need
        /// to exist before boot).
        /// +0x00 <c>WaterRedrawPendingFlag</c> — MainDraw's payload-start STUB sets it when the GameMode
        /// gate matches (instead of drawing there); the hook-site cave checks and unconditionally clears
        /// it (every frame, whether set or not, so no stale state survives into a non-matching frame)
        /// to decide whether to `jal` the relocated draw payload.
        /// +0x04 <c>DrawWaterHelperRaScratch</c> — the DrawWater vtable-call-bracket helper's own $ra
        /// stash, needed because it makes two nested calls and neither survives in a register (no free
        /// callee-saved slot in DrawWater's frame, and its sp-relative locals rule out the helper opening
        /// a second stack frame).</summary>
        internal const uint WaterRedrawPendingFlag     = 0x01FAE600;
        internal const uint DrawWaterHelperRaScratch   = 0x01FAE604;

        /// <summary>Low-tide mizu-reorder mailbox — see <c>IsoPatcher.PatchWaterRedraw</c>'s MIZU_STUB and
        /// <c>CanalTide</c>. The baked stub (hosted in the compacted GameMode gate) reads these at the
        /// post-character hook: if FramePtr is nonzero it ReloadTexture(TexGroup)s and MGDraw()s that frame
        /// — drawing the (scene-pass-hidden) water mesh AFTER the player so its own semi-transparent
        /// texture blends over the submerged body. C# owns both words: TexGroup is written BEFORE FramePtr
        /// (the pointer is the stub's gate), and FramePtr is zeroed on map change / frame loss / non-low
        /// tide. BSS = zero at boot = feature off until armed.</summary>
        internal const long MizuRedrawFramePtr      = 0x21FAE608;   // MMU (C# writes)
        internal const uint MizuRedrawFramePtrGuest = 0x01FAE608;   // guest (baked into the stub)
        internal const long MizuRedrawTexGroup      = 0x21FAE60C;
        internal const uint MizuRedrawTexGroupGuest = 0x01FAE60C;
        /// <summary>Water-redraw payload dynamic-return slot (waterOrderGate.bin: COND/SHIM write it,
        /// RET_THUNK `jr`s through it). EE-side only — C# never touches it.</summary>
        internal const uint WaterRedrawReturnSlotGuest = 0x01FAE610;
    }

}
