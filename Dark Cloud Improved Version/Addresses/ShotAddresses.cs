// Shot address bank: the CSHOT_EFFECT pools — the main-character effect pool and its motion-record chain
// (ShotEffectPool: Ruby's ball/orbs, Toan's whirlwind), the player ranged-shot pool (PlayerShotPool: pellets, shots)
// and the monster shot-effect pack with its BT_SHOT_EFFECT config table (ShotEffectPack).
namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The CSHOT_EFFECT / effect-model pool — the main-character effect slots (Ruby's charge ball and orbs, Toan's
    /// whirlwind <c>fuusya</c>), their per-slot CCharacter objects, and the motion-record layout used to walk them.
    /// Vanilla structure; the features that drive it (Ruby ball scaling, whirlwind sizing) own their own tuning.
    /// </summary>
    internal static class ShotEffectPool
    {

        // ── Whirlwind charge visual (effect model dun/mainchara/wep_eff/c01_fuusya.chr) ──────────────
        // The charge-2 whirlwind swoosh is a DISCRETE effect model, not the dcol weapon trail and not
        // weapon/element-keyed (confirmed by xref + user). Its mesh is baked geometry (.cfg has VERTEX_ANIME;
        // the .mds vertices are undecoded VIF1 packet data), and it renders through runtime CFrames
        // (CMotionModel.Draw -> MGDraw(rootFrame)). The model's frame tree: root "kiru" -> fkiri/null1_3/jkiri
        // -> the *__cappz mesh frames. Since the mesh is VERTEX_ANIME (morph in model space), ONLY the root's
        // LOCAL matrix transforms it — child-frame scaling is inert; we scale the root "kiru" local-matrix 3x3.
        // A "kiru" match is validated as a fuusya root by its next frame (+0x270) being "fkiri" (kiru is generic
        // — the weapon model has one too). Frame names are inline at CFrame+0x118.
        internal const uint FkiriNameWord = 0x72696B66; // "fkir" little-endian (frame "fkiri", validates a kiru root)
        internal const uint KiruNameWord  = 0x7572696B; // "kiru" little-endian (the fuusya root frame)
        internal const int  FuusyaFrameStride = 0x270;  // CFrame object size; fkiri = kiru + this

        // ── Direct POINTER to the fuusya roots (no RAM scan) — RE'd: offsets CONFIRMED in-game ──
        // Toan's charge whirl lives in the main-character effect object (a CSHOT_EFFECT) at the FIXED global
        // 0x1e8da60 (MMU 0x21E8DA60). Set by MainChara_Effect (dun 0x1dba230: Entry2(0x1e8da60,…); gp slot
        // uGpffff9cfc also points at it). It is a POOL of up to 8 CONCURRENT effect instances (CSHOT_EFFECT::
        // Step 0x1ac180 and ::Draw 0x1abf20 both loop slots 0..7), NOT one-per-weapon: a cast grabs the next
        // free slot, so several can be live at once. Layout: a master template CObject at base+0x10, then the
        // 8 drawable slot objects at base + 0x11C0 + slot*0x11B0 (Entry2 __as__CObject-copies the template into
        // each). Each object's root CFrame ("kiru") native pointer is at object + 0xBC (the fuusya CFrame tree
        // is heap-allocated and MOVES per cast, but this pointer stays put). So slot 0's root ptr is at
        // base + 0x11C0 + 0xBC = base + 0x127C, slot s at base + 0x127C + s*0x11B0. We pre-scale ALL 8 pool
        // slots (skip null/invalid) so whichever the next cast activates is already scaled — no first-frame
        // flash, and concurrent casts stay correct. Confirmed live: pointers at +0x127C,+0x242C,+0x35DC …
        // (0x11B0 apart) → the scanned roots (+ the template's own +0xCC, which we skip since it isn't drawn).
        internal const long MainCharaEffectBase = 0x21E8DA60; // CSHOT_EFFECT base (fixed global)
        internal const int  EffectSlotStride    = 0x11B0;     // per-slot object stride
        internal const int  EffectSlotModelOff  = 0x127C;     // base + slot*stride + this = slot's root CFrame native ptr (object +0xBC)
        internal const int  EffectSlotCount     = 8;          // concurrent effect-pool slots (Step/Draw loop 0..7)
        // The render uses the LOCAL matrix (+0x1d0), NOT the TRS scale (+0x210) — proven live: a live
        // instance held scaleX=4.7 with zero visual change. So we scale the root's local-matrix 3x3
        // (rotation/scale block) by bind*scale; its translation row (+0x200) is left alone so the effect
        // stays anchored on Toan. Row-major 4x4: rows 0..2 at +0x1d0/+0x1e0/+0x1f0.
        internal static readonly int[] CFrameLocal3x3 =
            { 0x1D0, 0x1D4, 0x1D8,   0x1E0, 0x1E4, 0x1E8,   0x1F0, 0x1F4, 0x1F8 };

        internal const float RubyOrbBaseRadius      = 5.0f;      // orb damage-sphere radius (BT+0x2C, live-captured [5,5,0,0])
        // Runtime motion-track chain. The pool's template object (pool+0x10) and its 8 slot copies are full
        // CCharacters (__ct__12CSHOT_EFFECT: __ct__10CCharacter at +0x10, slot array ctor 0x143530). Motion
        // data hangs off the CCharacter's MotionParam bank-pointer array at +0xC20 (8 native ptrs, bank motion-
        // id ranges at +0x3E0/+0x400 — RE'd from GetMotionParam 0x1383B0). Each MotionParam (0x80 bytes):
        // +0x00 morph data, +0x04 Mot_List chain-1 (MotionProc), +0x08 chain-2 (MotionProc2), +0x10 MOTION_STATE,
        // +0x60 FRAME_INF, +0x64 MOTION_INFO (from CreateAnimeDataEX 0x149090 + Step__10CCharacter 0x138530).
        // Entry2 copies the template's MotionParam into every slot SHALLOWLY, so all slots share ONE set of
        // Mot_List records/keyframes — one patch covers the ball and the fired orbs.
        // Mot_List record = 6 ints {frameIdx, subIdx, trackType, keyCount, keysPtr, nextPtr}; keys are
        // 0x20-byte records with the time int at +0 and the value vec (3 floats) at +0x10. Track types as in
        // MotionProc: 0=rotation(quat), 1=SCALE, 2=translation, 0xC=morph, 0x28/0x29=material, 0x1E-0x21=camera,
        // 0x32/0x33=visibility.
        internal const long EffectTemplateMotionParams = 0x10 + 0xC20; // pool + this = template's 8 MotionParam ptrs
        internal const int  MotionParamChain1 = 0x04;        // MotionParam + this = Mot_List chain-1 head (native ptr)
        internal const int  MotionParamChain2 = 0x08;        // MotionParam + this = Mot_List chain-2 head (native ptr)
        // Whole-object scale: Draw__10CCharacter (0x139310) pushes the CObject scale (+0x90/94/98) into the
        // root frame's TRS scale on EVERY draw — the engine-maintained whole-hierarchy scale (covers the ball's
        // core geometry, which the per-sprite SCALE tracks don't). Objects: template @pool+0x10, slots
        // @pool+0x11C0+s*0x11B0 (see EffectSlotStride/EffectSlotCount).
        internal const int  EffectTemplateOff   = 0x10;      // pool + this = template CCharacter
        internal const int  EffectSlotObjectsOff= 0x11C0;    // pool + this + s*EffectSlotStride = slot CCharacter
        internal const int  EffectObjectScale   = CCharacter.CharScale;  // CObject scale x (y +0x94, z +0x98)
        // Shot collision: Step__12CSHOT_EFFECT (0x1AC180) builds each shot's damage sphere from the pool's
        // BT_SHOT_EFFECT per-phase radius array — radius = *(float*)(BT + 0x28 + phase*4), phases 0-3; the
        // same values gate the wall-collision check. BT native ptr = *(pool + 0) (set by Entry2).
        internal const int  BtShotPtrOff    = 0x00;          // pool + this = BT_SHOT_EFFECT native ptr
        internal const int  BtShotRadiiOff  = 0x28;          // BT + this = float[4] per-phase collision radius
        internal const int  BtShotRadiiCount= 4;
        internal const int  MotRecFrameIdx  = 0x00;
        internal const int  MotRecType      = 0x08;
        internal const int  MotRecKeyCount  = 0x0C;
        internal const int  MotRecKeysPtr   = 0x10;
        internal const int  MotRecNext      = 0x14;
        internal const int  MotKeyStride    = 0x20;
        internal const int  MotKeyValueOff  = 0x10;
        internal const int  MotTypeScale    = 1;
    }

    /// <summary>
    /// The player shot-effect (CSHOT_EFFECT) pool that ranged attacks fire into — Xiao's
    /// slingshot pellets, Ruby's/Osmond's shots, etc. The pool BASE is a pointer stored at
    /// <see cref="BasePtr"/> (native gp-0x621c, gp=0x2A97F0); BattleActionPlay_Jinn (dun 0x1DBC930)
    /// scans it for a free slot (<see cref="ActiveFlagOffset"/>==0) and writes the new shot's
    /// position/velocity/damage/scale/lifetime. Struct-of-arrays: the vec fields (pos, vel) use a
    /// 0x10 stride; the scalar fields (flag, damage, scale, lifetime) use a 4-byte stride.
    /// </summary>
    internal static class PlayerShotPool
    {
        internal const long BasePtr          = 0x202A35D4; // holds the native pool base pointer
        internal const int  SlotCount        = 12;
        // vec arrays (float3, stride 0x10 from the pool base)
        internal const int  VecStride        = 0x10;
        internal const int  PosOffset        = 0x40;
        internal const int  VelOffset        = 0x1C0;
        // scalar arrays (stride 4 from the pool base)
        internal const int  ScalarStride     = 0x04;
        internal const int  NoCollideOffset  = 0x280; // int: 0 = pellet runs its (fixed 2.0-radius) collision; nonzero = pass through (step__5CSHOT gate)
        internal const int  LifetimeOffset   = 0x2B0; // int (0x78 = 120 frames at spawn)
        internal const int  DamageOffset     = 0x2E0; // int — the entry's base damage on contact; NEGATIVE = the contact plants nothing and the pellet just ends (DebugIfCave.PelletPlant)
        internal const int  ScaleOffset      = 0x310; // float (1.0 at spawn) — draw__5CSHOT sprite scale ONLY; does NOT size the hitbox
        internal const int  ActiveFlagOffset = 0x3D0; // int (nonzero = slot in use)

        internal static long VelAddr(long poolBase, int slot)   => poolBase + VelOffset   + slot * VecStride;
        internal static long PosAddr(long poolBase, int slot)   => poolBase + PosOffset   + slot * VecStride;
        internal static long FlagAddr(long poolBase, int slot)  => poolBase + ActiveFlagOffset + slot * ScalarStride;
        internal static long DamageAddr(long poolBase, int slot)=> poolBase + DamageOffset + slot * ScalarStride;
        internal static long ScaleAddr(long poolBase, int slot) => poolBase + ScaleOffset  + slot * ScalarStride;
        internal static long NoCollideAddr(long poolBase, int slot) => poolBase + NoCollideOffset + slot * ScalarStride;
        internal static long LifetimeAddr(long poolBase, int slot)  => poolBase + LifetimeOffset  + slot * ScalarStride;
    }

    /// <summary>
    /// The monster SHOT-EFFECT pack (CSHOT_EFFECT_PACK at *NowShotEffect): five CSHOT_EFFECT slots of 0xA160, one per
    /// BT_SHOT_EFFECT config entered for the floor (SetupBaseModel → Entry, from the species row's +0x68 into
    /// <see cref="CfgTable"/>), each with eight sub-shots. Per-sub-shot fields index by sub-shot; the sub-shot's own
    /// effect-CCharacter sits at <see cref="OffObj"/> + i × <see cref="ObjStride"/>. Step__12CSHOT_EFFECT plants each hit
    /// as CollisionData: +0x58 = <see cref="OffOwner"/>, +0x60 = <see cref="OffUserCol"/>, +0x64 = <see cref="OffAntiPtr"/>,
    /// +0x6C = <see cref="OffWepFlags"/>, +0x5C = <see cref="OffA060"/>, +0x68 = <see cref="OffA110"/>; the config gives the
    /// victim mask (+0x48: 1 = the player, 2 = enemies), the hit reaction (+0x44), the element (+0x40), the wait (+0x38) and
    /// the default damage (+0x3C). RE: AngelGear, BorrowedShots.
    /// </summary>
    internal static class ShotEffectPack
    {
        internal const long NowShotEffectPtr = 0x202A35D8;
        internal const int  PackSlots  = 5;
        internal const int  SlotStride = 0xA160;
        internal const int  SubShots   = 8;
        internal const int  EnteredSubShots = 6;   // what a borrowed entry gets unless it asks for more (the loader cave's +0x2BC)
        internal const int  OffCfg     = 0x000;     // BT_SHOT_EFFECT cfg ptr (EE): +0x38 wait, +0x3C life, +0x4E fly motion
        internal const int  OffDir     = 0x9F40;    // + i*0x10, vec3 — per-frame position delta (velocity)
        internal const int  OffAttr2   = 0x9FC0;    // + i*2, short — Set param_6
        internal const int  OffWait    = 0x9FD0;    // + i*4 — phase-1 countdown
        internal const int  OffPhase   = 0x9FF0;    // + i*2 — 0 muzzle, 1 flying, 2 the impact after a CONTACT, 3 the burst when the wait ran out (Step: a contact writes 2; a timeout adds 2)
        internal const int  OffActive  = 0xA000;    // + i*2 (Set writes it LAST)
        internal const int  OffDamage  = 0xA010;    // + i*4 — the shot's DAMAGE (Set: cfg+0x3C; SetDmg; Step passes it as entry +0x34). Life/wait = OffWait.
        internal const int  OffOwner   = 0xA050;    // + i*2, short — owner attr → CollisionData +0x58
        internal const int  OffA060    = 0xA060;    // + i*2, short — Set writes 0xFFFF; SetUserID2 (0x1AE400) then stamps the FIRING ENEMY SLOT
        internal const int  OffUserCol = 0xA070;    // + i*4 — Set param_5 (user/collider id → entry +0x60); −1 default
        internal const int  OffA0B0    = 0xA0B0;    // + i*4 — Set: -1
        internal const int  OffA0D0    = 0xA0D0;    // + i*4 — Set: -1.0f
        internal const int  OffA0F0    = 0xA0F0;    // + i*4 — Set: -1
        internal const int  OffA110    = 0xA110;    // + i*4 — Set: -1
        internal const int  OffSndFlag = 0xA130;    // + i, byte
        internal const int  OffReload  = 0xA138;    // + i, byte — after each planted entry the latch is set to this: frames without another plant
        internal const int  OffLatch   = 0xA140;    // + i, byte — held ≥1 = plants no damage
        internal const int  OffLastIdx = 0xA150;    // int — Set records the spawned index
        internal const int  OffCount   = 0xA14C;
        internal const int  OffObj     = 0x11C0;    // + i*0x11B0 — the sub-shot's effect-CCharacter
        internal const int  ObjStride  = 0x11B0;
        internal const int  ObjPos     = 0x10;      // vec: [+0] x, [+4] height, [+8] y
        internal const int  ObjFrame   = 0x2F0;     // motion frame (float)
        internal const int  ObjFrameTb = 0x344;     // → per-motion frame table (int per 0x10)
        internal const int  ObjMotSpd  = 0xC60;     // -1.0f = keyframe rate
        internal const int  ObjMotFlag = 0xC64;     // Set: 4 on the flying phase
        internal const int  ObjMotId   = 0xC68;     // motion id
        internal const int  CfgFlags = 0x40, CfgRadiusFlying = 0x2C;   // BT_SHOT_EFFECT: the element/attribute word (→ entry +0x50); the flying radius
        internal const int  CfgElementBits = 0x1F;                     // CfgFlags: 1 Fire, 2 Ice, 4 Thunder, 8 Wind, 16 Holy; 0x100+ are ailments
        // The per-phase damage radius the step plants with EVERY frame the phase lasts (cfg +0x28 + phase × 4): 0 muzzle, 1 flying,
        // 2 the impact after a contact, 3 the burst when the wait runs out. 0 = that phase plants nothing.
        internal const int  CfgRadiusMuzzle = 0x28, CfgRadiusImpact = 0x30, CfgRadiusExpire = 0x34;
        internal const int  OffWepFlags = 0xA030;   // + i*4 — SetWepStatus: the weapon's ability flags → entry +0x6C
        internal const int  OffAntiPtr  = 0xA090;   // + i*4 — SetVsMonster: → the weapon's anti-category bytes → entry +0x64
        /// <summary>The BT_SHOT_EFFECT configs (0x70 B each, the effect's file name at +0) the species rows index, through the table the
        /// species loader reads (CodeCaves.ShotCfgTable, ElfSpeciesPatches.PatchShotTable): the game's 34, the mod's
        /// <see cref="IceArrowConfig"/> and <see cref="IcePrisonConfig"/> (also in the game's table's two spare entries, where the
        /// shot-slot sharing cave looks), then <see cref="AtlaShotConfig"/> (this table only: the sharing cave leaves it alone).</summary>
        internal const long CfgTable   = CodeCaves.ShotCfgTable;
        internal const int  CfgCount   = 40;
        /// <summary>The game's own table (0x0027FA70, 36 entries up to the species table), the shot-slot sharing cave's.</summary>
        internal const long VanillaCfgTable = 0x2027FA70;
        internal const int  VanillaCfgCount = 36;
        /// <summary>The Atla Gemron's shot: e503ex_atall's burst, stationary, its hit moving out along the Gemron's forward.</summary>
        internal const int  AtlaShotConfig = 36;
        /// <summary>The Crystal Gemron's ice arrow: the Ice Queen's korinoya fired as a shot.</summary>
        internal const int  IceArrowConfig = 34;
        /// <summary>Its ice prison: the Ice Queen's kori, fired at the player's feet when an arrow freezes him (tools/stubs/crystal_shots.s).</summary>
        internal const int  IcePrisonConfig = 35;
        internal const int  CfgSize    = 0x70;
        internal const int  CfgVictimMask = 0x48;   // 1 = hurts the player, 2 = hurts enemies
        /// <summary>The hit REACTION the shot's entry carries (→ entry +0x4C): 2 guardable knockback, 3 unguardable
        /// knockdown, 4 light flinch. BtCheckDamageProc dispatches on exactly those three and subtracts the player's
        /// HP INSIDE each branch, so a value outside {2,3,4} — 1 and 5 are both unused — is inert: the entry is
        /// consumed and nothing happens to him. CMonstorUnit::CheckDmg never reads this field, so changing it does
        /// not alter how the same shot damages ENEMIES (a reflected shot still lands normally).</summary>
        internal const int  CfgReaction   = 0x44;
        internal const int  CfgWait    = 0x38;      // frames of flight before the impact chain
        internal const int  CfgName    = 0x00, CfgNameLen = 0x28;   // the effect's file name: dun/effect/<name>.chr
        // The motion (the .chr's KEY ordinal) each phase plays, shorts; −1 = none: muzzle, flying, impact, expiry burst.
        internal const int  CfgMuzzleMotion = 0x4C, CfgFlyMotion = 0x4E, CfgImpactMotion = 0x50, CfgExpireMotion = 0x52;
        /// <summary>Dragon's Y's charged shot per selected element, 00 Fire … 04 Holy: the Gemrons' f_boll_3, i_boll, t_boll,
        /// e114a_ex, e115a_ex — and at 05 (no element) the Black Dragon's b_boll.</summary>
        internal static readonly int[] DragonsYCfg = { 5, 20, 23, 24, 25, 22 };
        internal const long ReadBufferPtr   = 0x202A2384;   // → the dungeon loader's file read buffer (Entry's third argument)
        /// <summary>The MAIN-CHARACTER effect instances (CharaMainEffect, CharaMainEffectCrash): two more CSHOT_EFFECTs of this
        /// same layout beside the pack, one config each, which the floor loader fills with the active character's wep_eff
        /// effect (MainChara_Effect → Entry2) and the dungeon loop steps and draws through the live pointer at 0x2A34EC.
        /// Xiao's holds mgan01, which nothing of hers fires — the borrowed shots take it over (BorrowedShots).</summary>
        internal const long CharaMainEffect = 0x21E8DA60, CharaMainEffectCrash = 0x21E97BC0, MainEffectLivePtr = 0x202A34EC;
        /// <summary>dun.bin's table of the characters' wep_eff configs (Get_Main_EffectPtr, dun 0x1DBA060 → pointers at 0x1DC21F0):
        /// Toan's whirlwind `c01_fuusya` — muzzle radius 20, wait 160, damage 8, Wind, reaction 2, mask 2, muzzle motion 0 (the
        /// swoosh, KEY 5–40), nothing after: the effect plays out where it is planted.</summary>
        internal const long WhirlwindCfg = 0x201DC1B60;
        internal const uint MonsterPoolAlloc = 0x01F066D0;  // the CDataAlloc2 the floor's monster models and their shot effects come from
        internal const int  EntryParam4     = 0x26;         // what the species loader passes Entry as its fourth argument
    }
}
