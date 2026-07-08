namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Static per-weapon base-stat table in the ELF (SCUS_971.11), symbol <c>WeaponList</c>.
    /// CONFIRMED. 120 records (one per weapon item ID 257-376), stride 0x4C.
    ///
    /// INDEXING: index = (itemId - 257) == the weapon's ComItemInfo ClassIndex (+0x2). The engine
    /// resolves it via GetItemTypeInfo (type must be 2 = ItemClass.Weapon) then WeaponList[index].
    /// Readers: GetWeaponData__Fi (ELF 0x1D0F50), GetWeaponDataInfo__Fi (ELF 0x1D0D90).
    ///
    /// This backs the per-Dagger absolute addresses in Weapons.cs (its "Base database table Dagger
    /// addresses" are WeaponList[1] = the entry for item 258 at EE 0x2027A70C). The field
    /// offsets below are those names made relative to the entry base. Stat fields are signed shorts
    /// unless noted; the Dagger entry is also used at runtime (see Weapons.cs "(ALSO RUNTIME)").
    ///
    /// <see cref="ItemData.ChestPools"/> ranks weapon rarity by power = <see cref="MaxAttack"/> +
    /// <see cref="MaxMagic"/>/3 (+0x44/+0x46): stronger = rarer (Inferno tops the stats, but
    /// Chronicle 2 is pinned as the single rarest there by design).
    /// </summary>
    internal static class WeaponList
    {
        internal const int NativeBase   = 0x0027A6C0;
        internal const int Base         = 0x2027A6C0; // PCSX2 EE (native + 0x20000000)
        internal const int Stride       = 0x4C;       // 76 bytes per weapon
        internal const int FirstItemId  = 257;        // index 0 = item 257 (Dagger broken)
        internal const int Count        = 120;        // item IDs 257-376

        // ── Field offsets within a weapon entry (from Weapons.cs Dagger map) ──
        internal const int Whp          = 0x00; // short — base weapon health points
        internal const int Attack       = 0x02; // short - base attack (ChestPools power metric)
        internal const int Endurance    = 0x04; // short — base endurance (durability)
        internal const int Speed        = 0x06; // short — base speed
        internal const int Magic        = 0x08; // short — base magic
        // Ownership: 0=Toan 1=Xiao 2=Goro 3=Ruby 4=Ungaga 5=Osmond
        internal const int Ownership    = 0x0A; // byte - owning character
        internal const int Synth1       = 0x0B; // byte  — synth slot 1 (0=none,1=gray,2=blue)
        internal const int Synth2       = 0x0C; // byte
        internal const int Synth3       = 0x0D; // byte
        internal const int Synth4       = 0x0E; // byte
        internal const int Synth5       = 0x0F; // byte
        internal const int Synth6       = 0x10; // byte  — synth slot 6
        // Elemental attack stats (short each)
        internal const int Fire         = 0x12;
        internal const int Ice          = 0x14;
        internal const int Thunder      = 0x16;
        internal const int Wind         = 0x18;
        internal const int Holy         = 0x1A;
        // Anti-/slayer attack stats (short each)
        internal const int DinoSlayer   = 0x1C;
        internal const int UndeadBuster = 0x1E;
        internal const int SeaKiller    = 0x20;
        internal const int StoneBreaker = 0x22;
        internal const int PlantBuster  = 0x24;
        internal const int BeastBuster  = 0x26;
        internal const int SkyHunter    = 0x28;
        internal const int MetalBreaker = 0x2A;
        internal const int MimicBreaker = 0x2C;
        internal const int MageSlayer   = 0x2E;
        internal const int Abs          = 0x30; // short - base absorption (ABS) points
        internal const int AbsAdd       = 0x32; // short - ABS added per weapon level
        // Effect1 bits: 2=BigBucks 4=Poor 8=Quench 16=Thirst 32=Poison 64=Stop 128=Steal
        internal const int Effect1      = 0x38; // byte - special effects set 1
        // Effect2 bits: 1=Fragile 2=Durable 4=Drain 8=Heal 16=Critical 32=ABSUp
        internal const int Effect2      = 0x39; // byte - special effects set 2
        internal const int BuildUp      = 0x3C; // build-up branch data
        internal const int MaxAttack    = 0x44; // short — max attack
        internal const int MaxMagic     = 0x46; // short — max magic

        /// <summary>EE RAM base address of the entry for <paramref name="itemId"/> (257-376),
        /// or -1 if out of range.</summary>
        internal static int EntryAddr(int itemId) =>
            itemId >= FirstItemId && itemId < FirstItemId + Count
                ? Base + (itemId - FirstItemId) * Stride : -1;

        /// <summary>EE RAM address of <paramref name="fieldOffset"/> within the entry for
        /// <paramref name="itemId"/>, or -1 if the ID is out of range.</summary>
        internal static int FieldAddr(int itemId, int fieldOffset)
        {
            int e = EntryAddr(itemId);
            return e < 0 ? -1 : e + fieldOffset;
        }
    }

    /// <summary>
    /// Weapon element attribute table (ELF symbol referenced by <c>GetWeaponElementAttr__Fi</c>,
    /// ELF 0x1B69F0). 6 int entries @native 0x0027B1B0 indexed by element 0-5 (clamped); returns a
    /// per-element attribute value. Separate from <see cref="WeaponList"/> (not per-weapon).
    /// Recorded for completeness; not used by the Chest Randomizer.
    /// </summary>
    internal static class WeaponElementAttr
    {
        internal const int NativeBase = 0x0027B1B0;
        internal const int Base       = 0x2027B1B0;
        internal const int Stride     = 4;
        internal const int Count      = 6; // element 0-5
    }

    /// <summary>
    /// Player melee REACH (how far a weapon hits). Reach is <b>per-weapon and lives in the weapon
    /// MODEL's <c>dcol*</c> "damage-collision" frames</b> — NOT a <see cref="WeaponList"/> stat and NOT
    /// the per-character tolerance at <see cref="PerCharTolerance"/>. Full notes: docs/weapon-reach.md.
    ///
    /// Swing -> hit flow (addresses PCSX2 = native + 0x20000000; <c>0x1DB...</c> are dun.bin overlay):
    ///   1. The per-character swing handler (<c>ToanKey_Play</c> main 0x241690, <c>UngagaKey_Play</c>,
    ///      <c>GoroKey_Play</c>) calls <c>SearchFrame(equippedWeaponModel,"dcol0".."dcol3")</c>
    ///      (main 0x128700) -> <c>GetWorldPosition</c> -> <c>CCollisionData::Set</c> (main 0x1B57A0),
    ///      building the weapon's hit spheres into the global <c>NowColData</c> (0x202A35E0).
    ///   2. Each enemy's <c>CMonstorUnit::CheckDmg</c> (main 0x1D9F10) tests its body sphere against
    ///      that collision (reads center +0x58/+0x60), and on a hit calls <c>SwordDmgCheck1</c>
    ///      (dun 0x1DB9B30) to apply damage + draw the spark at the weapon bone.
    ///
    /// A weapon model has frames named <c>dcol0, dcol1, ...</c> placed along the blade. CONFIRMED via
    /// disasm: the player melee hit only ever uses the frame named <c>dcol1</c> (every SearchFrame in
    /// ToanKey_Play passes "dcol1"); dcol0/2/3 are inert for the hit. So reach = the <c>dcol1</c> frame's
    /// swept world position + the per-attack swing radius. Heaven's Cloud reach is extended by moving the
    /// <c>dcol1</c> frame's local Z out and enlarging the swing radii — see
    /// <c>Weapons.StartHeavensCloudReach</c> and the constants below. (The CHARGE attack uses a separate,
    /// not-yet-located hit path — unaffected.)
    /// </summary>
    internal static class WeaponCollision
    {
        /// <summary>PCSX2 address holding the PS2-native pointer to the <b>character</b> model root
        /// <c>CFrame</c> (the value <c>SwordDmgCheck1</c> passes to <c>SearchFrame</c>; e.g. Toan =
        /// "c01d_..."). The <c>dcol*</c> frames are NOT in this node — they live in the equipped
        /// weapon subtree attached under a hand bone, reachable by walking child/next. Convert the
        /// stored pointer with <c>Memory.ToMmu()</c> before dereferencing.</summary>
        internal const int EquippedModelPtr = 0x21EA1DDC;

        // ── Runtime CFrame object (confirmed from SearchFrame 0x128700, GetLWMatrix 0x1281b0,
        //    SetPosition 0x127e80) ──
        // SearchFrame compares this+0x118 (name) then recurses child(+0x138)/next(+0x13c).
        // Matrices: +0x150 = WORLD matrix (output cache), +0x1d0 = LOCAL matrix; GetLWMatrix
        // recomputes +0x150 = parentWorld * local(+0x1d0). For the dcol* frames the position lives
        // DIRECTLY in the local matrix translation (+0x200/4/8), baked at load — the TRS position
        // (+0x220/4/8) is (0,0,0) and unused for these frames (confirmed via live dump 2026-06-28).
        // So to move a dcol frame: write +0x200/4/8 then force a world recompute (+0x240=0). Do NOT
        // use the +0x220 TRS path here — its base is 0, so writing it would zero the offset, not extend
        // it. (If the local-matrix edit reverts each frame, the engine re-poses from the upstream MDS.)
        internal const int CFrameParent      = 0x110;
        internal const int CFrameName        = 0x118; // NUL-terminated char[]
        internal const int CFrameChild       = 0x138;
        internal const int CFrameNext        = 0x13C;
        internal const int CFrameWorldMatrix = 0x150; // computed; do NOT edit (world-space, recomputed)
        internal const int CFrameLocalMatrix = 0x1D0; // baked at load; its translation row (+0x30) is
        internal const int CFrameLocalTransX = 0x200; //   the frame's real local offset from its parent.
        internal const int CFrameLocalTransY = 0x204; //   Edit these + set CFrameDirtyWorld=0 (NOT
        internal const int CFrameLocalTransZ = 0x208; //   CFrameDirtyTRS, which rebuilds +0x1d0 from TRS).
        internal const int CFrameScaleX      = 0x210; // TRS scale x (written by SetScale; y +0x214, z +0x218)
        internal const int CFramePosX        = 0x220; // local translation x (read/written by SetPosition)
        internal const int CFramePosY        = 0x224;
        internal const int CFramePosZ        = 0x228;
        internal const int CFrameDirtyTRS    = 0x24C; // set 1 when local TRS changed
        internal const int CFrameDirtyWorld  = 0x240; // set 0 to force world-matrix recompute

        // Raw MDS model-node layout (in data.dat / the .chr buffer; distinct from the CFrame object):
        // char name[16] + 4x4 matrix @+0x28; translation = matrix row 3 at +0x58/+0x5C/+0x60.
        internal const int NodeNameLen = 16;
        internal const int NodeMatrix  = 0x28;
        internal const int NodeTransX  = 0x58;
        internal const int NodeTransY  = 0x5C;
        internal const int NodeTransZ  = 0x60;

        /// <summary>Per-character hit tolerance (6 floats, idx 0=Toan..5=Osmond), added to hit tests.
        /// Confirmed in-game [16,14,16,16,18,15] - same across a character's weapons, so NOT reach.</summary>
        internal const int PerCharTolerance = 0x21DC1B40;

        /// <summary>Active weapon collision object (player path): dun $gp 0x21E00000 - 0x6210.</summary>
        internal const int PlayerCollision = 0x21DF9DF0;

        // Weapon model assets in data.dat: commenu/weapon/cXXwNN.chr (XX char, NN = WeaponList +0x48).
        // Heaven's Cloud = c01w14.chr (Toan, within-char idx 14).

        // ── Heaven's Cloud melee reach (see Weapons.cs ReachTick) ─────────────────────
        // RE'd from SCUS_971.11 (ToanKey_Play 0x241690): every Toan melee hit is SearchFrame(equippedModel,
        // "dcol1") -> CCollisionData::Set(pos, radius); the engine only ever uses the frame named "dcol1".
        // Reach is extended by scaling the whole blade mesh at runtime (see the "Runtime weapon-model SCALE"
        // block below) — the dcol hit frames are children of the mesh, so they grow with it.

        // The "dcol1" CFrame in the loaded weapon model (CFrameVu1 template; name = "dcol"+'1'+NUL, local-matrix
        // translation (X,Y,Z) at name+0xE8/+0xEC/+0xF0; Toan weapons are (0,0,Z), Z = the reach). Used by
        // Weapons.LocateWeaponDcol1 to read a weapon's dcol1 Z for sizing its whirl when it's not in ToanWeapons.
        internal const uint  DcolNameWord     = 0x6C6F6364; // "dcol" little-endian
        internal const byte  Dcol1Digit       = 0x31;       // '1' (the active hit-point frame)
        internal const int   DcolNameToLocalX = 0xE8;       // local-matrix X (Y at +0xEC, Z at +0xF0)
        internal const int   DcolNameToLocalZ = 0xF0;       // local-matrix Z (the reach)

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

        // ── Direct POINTER to the fuusya roots (no RAM scan) — RE'd 2026-06-30, offsets CONFIRMED in-game ──
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

        // ── Ruby Mobius-Ring charge-ball growth (see Weapons.SetRubyBallScale / CustomRubyEffects.MobiusRingEffect) ──
        // Ruby's charging ball + fired orbs are CSHOT_EFFECT slots of the MainCharaEffectBase pool (decomp-
        // confirmed: the RubyOrbs addresses are the pool's per-slot fields). The visible ball is a set of
        // BILLBOARD sprite frames (c05_<elem>0N.chr "grid*__cappz") sized by per-frame SCALE keyframe tracks
        // in the .mot — NOT by the root CFrame (root has no tracks; billboards ignore inherited scale, which
        // is why root-matrix scaling shows nothing). So the ball is grown by patching the RUNTIME Mot_List
        // SCALE-track keyframe VALUES (built by CreateAnimeDataEX 0x149090, read by MotionProc 0x147D20):
        // multiply each key's value vec by the factor and the engine animates the bigger ball every frame.
        // All pool slots share ONE Mot_List (Entry2 copies the pointers), so one patch covers ball + orbs.
        // The ball only starts growing once the Mobius charge is fully built; size tracks the damage
        // multiplier M = currentDamage/baseDamage: scale = 1 + (M-1)*RubyBallGrowthPerMultiple, clamped to
        // RubyBallMaxScale. Both knobs are eyeball-calibration (tune live).
        internal const float RubyBallMaxScale          = 5.0f;   // hard cap on ball/orb size (M can reach 1000s)
        internal const float RubyBallGrowthPerMultiple = 1.0f;  // ball-scale gained per +1.0 of damage multiplier
        // Per-lever toggles (crash triage: each lever logs "RubyBall: applying …" when it writes, so the
        // last line in the log before a crash names the culprit; flip that one off).
        // CRASH POST-MORTEM (2026-07-06 "Read Abort" ×3): the sprite walker was patching CHAIN-2 records
        // too — MotionProc2 (0x148860) chain-2 records are VERTEX-SKINNING data (key +0x00 = vertex index
        // used to address memory, +0x10 = blend weight), NOT transform tracks, so the ratio-multiply
        // corrupted vertex animation → EE Read Abort. Present in ALL crash sessions (and v4 — which just
        // got lucky/used a different element), so neither Core nor Collision was ever properly isolated.
        // The walker is now chain-1-only with strict record+key validation.
        // Live bisect (2026-07-06): CORE alone scales EVERYTHING visual (billboards inherit the root
        // scale) — Sprites ON TOP double-scales the sprite layers, so it stays OFF. Collision via the BT
        // radii CRASHES even in isolation (one float write → Read Abort; unresolved) — replaced by
        // enemy-body inflation (Weapons.MaintainRubyOrbHitbox), which is mathematically equivalent.
        internal const bool  RubyBallScaleSprites   = false;     // 1. Mot_List chain-1 SCALE keyframe patch (redundant with Core)
        internal const bool  RubyBallScaleCore      = true;      // 2. CObject scale on template+slots (THE visual lever)
        internal const bool  RubyBallScaleCollision = false;     // 3. BT radii — ⚠ CRASHES, keep off (see Weapons.cs)
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
        internal const int  EffectObjectScale   = 0x90;      // CObject scale x (y +0x94, z +0x98)
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

        // ── Charge attack state (ToanKey_Play, RE'd from SCUS_971.11) ──
        // Drives CustomToanEffects.HeavensCloudEffect's charge ramp + MaintainEnemyHitbox's whirl gate. See
        // Weapons.IsChargingWhirlwind / IsWhirlwindActive and the toan-charge-states memory.
        internal const long ChargeActionState = 0x21DC4494; // DAT_01dc4494 action id (values below)
        internal const int  ActionWindup      = 0xE;        // charge wind-up (meter accumulates; lunge OR whirlwind)
        internal const int  ActionWhirlwind   = 0x18;       // whirlwind executing
        internal const int  ActionComboFirst  = 0x24;       // combo swing states 0x24-0x28 = melee hits 1-5
        internal const int  ActionComboLast   = 0x28;       //   (each combo hit is its own action state)

        // ── Xiao shot states (BattleActionPlay_Jinn, dun 0x1DBC930) — SAME ChargeActionState global ──
        // Xiao's slingshot shot is three c04b motions: idx 11 構え引き "draw" (frames 240-251, spd 0.7)
        // = state 0xB; idx 12 構え引きループ "draw hold" (frame 250-250, spd 0) = state 0xC; idx 13 撃ち
        // "shoot" (frames 251-255, spd 0.7) = state 0xD, pellet released at frame 251.
        internal const int  XiaoShotDraw     = 0xB;
        internal const int  XiaoShotHold     = 0xC;  // zero-speed loop parked on frame 250 until release flag flips
        internal const int  XiaoShotShoot    = 0xD;
        // iRam01dc4498: reset to 0 by BattleActionOn_Jinn at shot start, set to 1 when the fire input
        // releases → what makes the 0xC hold advance to the 0xD shoot. Forcing it = fire now (no hold).
        internal const long XiaoShotReleaseFlag = 0x21DC4498;
        // iRam01dc4490: nonzero while a shot is in progress (set at BattleActionOn start, cleared at the
        // shoot-motion end). iRam01dc44c8 (float): the ranged "speed bar" — BattleActionOn starts a shot
        // only when it reaches 100.0, then resets it to 0; its fill rate is the weapon's speed stat.
        internal const long XiaoShotActive = 0x21DC4490;
        internal const long XiaoShotGauge  = 0x21DC44C8;

        // ── Quick Draw (Small Sword) — first-swing wind-up skip ──
        // ToanKey_Play keys EVERYTHING off the active character's animation frame cursor
        // (DAT_01ea2010 = CCharacter 0x1ea1d20 + 0x2F0, float). First combo swing (action 0x24)
        // timeline, from the ToanKey_Play decompile + ELF gp-data:
        //   820.0–820.5  one-shot forward step-in write (speed 0.17 → DAT_01dc4590)
        //   824.0–825.0  weapon-trail effect spawn (CWeaponEffect)
        //   825.0        swing whoosh sound
        //   825.0–828.0  hit window (CCollisionData::Set + basic_damage)
        //   ~830         motion end → chain to 0x25 / windup / exit
        // Snapping the cursor forward once it has passed the step-in window preserves the
        // step-in, trail, sound and hit — only the wind-up frames disappear. See
        // CustomToanEffects.SmallSwordEffect (Quick Draw).
        internal const long  AnimFrameCursor        = 0x21EA2010; // float: active-char motion frame cursor
        internal const float Combo1WindupSettled    = 820.5f;     // past the engine's one-shot step-in write
        internal const float Combo1QuickDrawTarget  = 824.0f;     // just before trail spawn + hit window

        /// <summary>ELF global <c>hitCnt</c> (native 0x2A2C64): the hit-spark ring counter,
        /// incremented by <c>CMonstorUnit::CheckDmg</c> (0x1D9F10) each time a player attack
        /// deals damage to a monster (wraps 0-15). Watching it across a swing is the "did that
        /// swing connect?" signal. Guarded hits do NOT advance it.</summary>
        internal const long HitSparkCounter   = 0x202A2C64;
        // Charge METER (float, DAT_01dc449c): resets to 1.0 at attack start, accumulates each windup frame,
        // caps at 3.0. Thresholds: ≥1.5 → lunge available, ≥2.5 → whirlwind available (if unlocked).
        internal const long ChargeMeter       = 0x21DC449C;
        // Native meter gain: +1/60 per windup frame (float @ native 0x2A1CAC) = 1.0/second at 60 fps.
        // The charge LEVEL is re-derived from the meter every windup frame, so boosting the meter
        // (e.g. Tsukikage's double-speed charge) advances the levels automatically.
        internal const float ChargeMeterPerSecond = 1.0f;
        internal const float ChargeMeterCap       = 3.0f;
        // Charge LEVEL (DAT_01dc44dc): the tier the meter has crossed during the 0xE windup — the clean signal
        // for WHICH charge attack is being built. Reset to 0 by ToanKey_On at attack start.
        internal const long ChargeLevel       = 0x21DC44DC;
        internal const int  ChargeLevelNone     = 0;        // meter < 1.5
        internal const int  ChargeLevelLunge    = 1;        // meter ≥ 1.5
        internal const int  ChargeLevelWhirl    = 2;        // meter ≥ 2.5 AND whirlwind unlocked (UserStatus+0x4324≠0)
        // Whirlwind-unlock gate: the level-2 transition also requires *(int*)(UserStatus + WhirlwindUnlockOffset)
        // ≠ 0 (the ability learned). Documented for completeness; the level-2 check already folds it in, so code
        // reads ChargeLevel==2 rather than this directly (UserStatus base not needed).
        internal const int  WhirlwindUnlockOffset = 0x4324;
        // Charge-active flag (DAT_01dc44f0): 1 while a lunge/whirlwind hit is live; cleared to 0 INSIDE the 0x18
        // block on the whirlwind's final frame — so (action 0x18 && flag 1) is the true "whirlwind executing"
        // window, and the flag dropping is the earliest, cleanest "attack finished" signal (the action state
        // itself lingers at 0x18 for a frame until ToanKey_On resets it).
        internal const long ChargeActiveFlag  = 0x21DC44F0;

        // ── Runtime weapon-model SCALE (visual blade + dcol collision, together) — CONFIRMED 2026-06-30 ──
        // The equipped weapon's model root is *(*NowWeapon + 0xBC) (NowWeapon = 0x202A34F0); it is a CFrameVu1
        // TEMPLATE node (name@0, NOT the +0x118 runtime CFrame). CFrameVu1 layout: name@0, LOCAL matrix 3x3 at
        // +0xB8 (row-major, rows of 0x10 → diagonal at +0xB8/+0xCC/+0xE0), LOCAL translation at +0xE8/+0xEC/+0xF0
        // (this is the same node type as the dcol1 frame — see DcolNameToLocalX/Z = 0xE8/0xF0 above), WORLD
        // translation at +0x68. The model tree: root "NN_1_1" → **mesh frame "wNN"** (the visible blade; e.g.
        // Heaven's Cloud = "w14", name word "w14\0" = 0x00343177) → dcol0..dcol3 (collision frames, children of
        // the mesh). So scaling the mesh frame "wNN" scales BOTH the visible blade AND the dcol hit point
        // (children inherit the parent's world transform).
        //
        // To scale at runtime: locate the "wNN" frame (name@0) by scanning the model window around the root,
        // then write factor to its local-3x3 DIAGONAL (+0xB8/+0xCC/+0xE0; bind is identity so factor*identity).
        // CONFIRMED: this grows the visual blade AND the melee hit reach by `factor`, and is STABLE (the engine
        // does NOT re-pose this template node) — a pure data write, mid-game safe, NO EE-code patch, NO crash.
        // This is the clean lever the old dcol1-only / code-immediate reach hacks were working around.
        internal const long NowWeaponPtr           = 0x202A34F0; // → native ptr to NowWeapon record
        internal const int  WeaponModelRootOffset  = 0xBC;       // NowWeapon + 0xBC → native ptr to model root CFrameVu1
        internal const int  Vu1LocalMatrixDiag0    = 0xB8;       // CFrameVu1 local 3x3 m00 (m11 +0x14=0xCC, m22 +0x28=0xE0)
        internal const int  Vu1LocalMatrixDiag1    = 0xCC;
        internal const int  Vu1LocalMatrixDiag2    = 0xE0;
        internal const int  Vu1LocalTransX         = 0xE8;       // CFrameVu1 local translation (Y +0xEC, Z +0xF0)
        internal const int  Vu1LocalTransZ         = 0xF0;

        /// <summary>Heaven's Cloud's visible-blade mesh frame name ("w14\0"). Scaling this frame's local 3x3
        /// grows the blade + its dcol collision children together (the runtime reach lever above).</summary>
        internal const uint HcMeshNameWord = 0x00343177;

        /// <summary>Xiao's slingshot weapon mesh frame name ("c04w"). Scaled by Super Steve's Heaven's Cloud
        /// charge (Weapons.ScaleWeaponFrameByName).</summary>
        internal const uint XiaoSlingMeshNameWord = 0x77343063;

        // ── Inventory equip slot (early weapon-swap detection) ──
        // The battle in-hand weapon id (0x21EA7590, Player.Weapon.GetCurrentWeaponId) only refreshes once Toan
        // is walking again after the menu, so keying off it lags a swap. The inventory equip slot updates
        // IMMEDIATELY: a byte slot index at InventoryEquipSlotAddr indexes the weapon list at
        // InventoryWeaponSlot0Id + slot*InventoryWeaponSlotStride (ushort id). See Weapons.GetEquippedWeaponId.
        internal const long InventoryEquipSlotAddr    = 0x21CDD88C; // byte: current equipped weapon slot (0-9)
        internal const long InventoryWeaponSlot0Id    = 0x21CDDA58; // ushort: slot 0's weapon id
        internal const int  InventoryWeaponSlotStride = 0xF8;       // stride between weapon-list slots

        // ── Runtime weapon-record (WEAPON_HAVE) field offsets, relative to a slot's base
        // (InventoryWeaponSlot0Id + slot * InventoryWeaponSlotStride) ──
        internal const int  InventoryWeaponLevelOffset  = 0x02;     // short: weapon level — increments when an
                                                                    //   ABS level-up absorbs the attachments
        internal const int  InventoryWeaponMaxWhpOffset = 0x0C;     // short: max WHP
        internal const int  InventoryWeaponWhpOffset    = 0x10;     // float: current WHP
        internal const int  InventoryWeaponAbsOffset    = 0x14;     // short: current ABS points
        internal const int  WeaponAntiOffset            = 0x1C;     // 10 bytes: anti-category values in
                                                                    //   EnemyCategory order (Dragon..Mage), cap 99
        internal const int  WeaponAntiCount             = 10;
        // Attachment slots within a record: 6 ATTACH_LIST entries of 0x20 bytes at +0x28
        // (layout from WeaponAllValueSet ELF 0x225B60 + PlusAttachmentVolume 0x225810: slots end
        // at +0xE8 where the per-slot flag bytes + the +0xEE ability word live). Entries are
        // memcpy'd from the static AttachList template table (native 0x27CA60, indexed via
        // GetAttachData 0x1D0EF0) when an item is attached, and their VALUES — not the item
        // templates — are what WeaponAllValueSet accumulates for effective stats and what the
        // level-up absorb consumes (AttachMentValuePlus 0x235A10, called from
        // CWeaponLevelUp::SetLevelUpValue/SetStatusBreak). So editing an entry edits that
        // attachment's contribution everywhere: menus, build-up eligibility, battle, and absorb.
        internal const int  WeaponAttachSlot0Offset     = 0x28;
        internal const int  WeaponAttachSlotStride      = 0x20;
        internal const int  WeaponAttachSlotCount       = 6;
        // ATTACH_LIST entry fields: +0x00 item id (ushort), +0x08 four stat shorts
        // (Atk/End/Spd/Mag), +0x10 five element bytes, +0x15 ten anti bytes (Dragon..Mage).
        internal const int  AttachEntryAntiOffset       = 0x15;
        /// <summary>Template anti value of every anti-category attachment (Dinoslayer..Mage
        /// Slayer, items 111-120): +3 to their own category (verified from AttachList).</summary>
        internal const int  AttachAntiBaseValue         = 3;

        /// <summary>The in-battle WEAPON_HAVE copy of the equipped weapon (id at +0; same field
        /// layout as the inventory records — <c>Player.Weapon</c> wraps its common fields).</summary>
        internal const long BattleWeaponRecord          = 0x21EA7590;
        // Effective (post-attachment, POST-CLAMP) stat block inside a WEAPON_HAVE, written by
        // WeaponAllValueSet (0x225B60): Attack+4 (cap MaxAttack), Endurance+6 (cap 99), Speed+8
        // (cap 99 = _DAT_00294178), Magic+10 (cap MaxMagic). Writing past the cap in the BATTLE copy
        // bypasses it for combat while the menu/inventory record still reads 99.
        internal const int  EffAttackOffset    = 0x04;
        internal const int  EffEnduranceOffset = 0x06;
        internal const int  EffSpeedOffset     = 0x08;
        internal const int  EffMagicOffset     = 0x0A;
        internal const int  StatCap            = 99;   // _DAT_00294178 / _DAT_00294174

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
            internal const int  DamageOffset     = 0x2E0; // int
            internal const int  ScaleOffset      = 0x310; // float (1.0 at spawn) — draw__5CSHOT sprite scale ONLY; does NOT size the hitbox
            internal const int  ActiveFlagOffset = 0x3D0; // int (nonzero = slot in use)

            internal static long VelAddr(long poolBase, int slot)   => poolBase + VelOffset   + slot * VecStride;
            internal static long PosAddr(long poolBase, int slot)   => poolBase + PosOffset   + slot * VecStride;
            internal static long FlagAddr(long poolBase, int slot)  => poolBase + ActiveFlagOffset + slot * ScalarStride;
            internal static long DamageAddr(long poolBase, int slot)=> poolBase + DamageOffset + slot * ScalarStride;
            internal static long ScaleAddr(long poolBase, int slot) => poolBase + ScaleOffset  + slot * ScalarStride;
            internal static long NoCollideAddr(long poolBase, int slot) => poolBase + NoCollideOffset + slot * ScalarStride;
        }

        /// <summary>
        /// The attachment "board" (attachment inventory): ATTACH_LIST entries at status-base +
        /// 0x84FC (status base = 0x21CD954C, the object Toan's weapon records at +0x450C =
        /// 0x21CDDA58 live in). RE'd from GetBoardSpace (ELF 0x2315C0: kind-2 scan, empty = id
        /// &lt;= 0x50) and SetStatusBreak (0x2368D0), which writes the SynthSphere here: item id
        /// 0x5A at +0, SOURCE WEAPON id at +2, ability-flag word at +4, source LEVEL byte at +6,
        /// then the standard ATTACH_LIST values (stats +8, elements +0x10, antis +0x15) at 60%.
        /// The break's weapon removal (CWeaponLevelUp::Step case 5, ELF 0x237010) zeroes the bag
        /// record IN PLACE (id → 0xFFFF, no compaction) — which is what makes a post-hoc undo /
        /// sphere rewrite safe.
        /// </summary>
        internal static class AttachBoard
        {
            /// <summary>= status base 0x21CD954C + 0x84FC (the board sits right after the six
            /// characters' weapon arrays: 0x450C + 6×0xAA8 = 0x84FC). Same address the mod has
            /// long used as <see cref="Addresses.firstBagAttachment"/>.</summary>
            internal const long Base      = Addresses.firstBagAttachment; // 0x21CE1A48
            internal const int  Stride    = 0x20;
            internal const int  ScanCount = Player.inventorySizeAttachments + 2; // 42, mirrors Player.GetBagAttachments
            internal const int  EntryItemId      = 0x00; // ushort; SynthSphere = 0x5A; empty <= 0x50
            internal const int  EntrySourceId    = 0x02; // ushort — sphere: source weapon item id
            internal const int  EntryFlags       = 0x04; // ushort — sphere: ability flags
            internal const int  EntrySourceLevel = 0x06; // byte   — sphere: source weapon level
            internal const int  EntryStats       = 0x08; // 4 shorts: Atk/End/Spd/Mag
            internal const int  EntryElements    = 0x10; // 5 bytes
            internal const int  EntryAntis       = 0x15; // 10 bytes
            internal const int  SynthSphereId    = 0x5A;
        }

        /// <summary>
        /// The kill-ABS grant path, RE'd from the CMonstorUnit::Step death block (the
        /// GetWeaponMaxExp call at ELF 0x1DF1CC). On an enemy slot's release frame
        /// (slot field +0x00 == -1) the engine grants the slot's ABS reward to the ACTIVE
        /// character's equipped INVENTORY record:
        ///   rec = UserStatusBase + char*CharStride + WeaponArrayOffset + equipSlot*0xF8,
        ///   equipSlot = byte at UserStatusBase + EquipSlotArrayOffset + char
        /// gated on (slot.KillerCharId == active char) and the equipped weapon not being a
        /// default weapon. Grant modifiers: ×2 when the back-floor flag (BackFloorDoubleAddr)
        /// is set; ×AbsBonusMult (1.2f, DAT_002a1af8) when the slot's status-flag word has
        /// AbsBonusFlag (0x2000) set. THE KEY BEHAVIOR: the whole grant is SKIPPED when
        /// abs &gt;= GetWeaponMaxExp — a crossing kill clamps to exactly max and queues the
        /// "ABS MAX" popup; above max the engine is fully inert. Max ABS is never stored: it is
        /// COMPUTED per call as table base (+0x30, SIGNED CHAR) + level × step (+0x32, short),
        /// clamped to 1..999 (the live Weapons.abs / Weapons.absadd table columns — which is
        /// also why the table can't just be doubled: base values run up to 125, so 2× overflows
        /// the signed char for ~54 weapons). Special cases in the same block: Serpent Sword
        /// (id 268) grants nothing until game flag 0x30 is set, and while the player is
        /// monster-transformed (UserStatusBase+TransformStateOffset == 10) kills DRAIN abs
        /// instead of granting. Backs CustomToanEffects.MachoSwordEffect (ABS rollover).
        /// </summary>
        internal static class AbsRollover
        {
            internal const long UserStatusBase       = 0x21CD954C; // CDngStatusData / "status base"
            internal const int  CharStride           = 0xAA8;      // per-character block stride
            internal const int  WeaponArrayOffset    = 0x450C;     // char block + this = weapon record 0 (id at +0)
            internal const int  EquipSlotArrayOffset = 0x4340;     // + char = equipped weapon slot byte (0-9)
            internal const int  TransformStateOffset = 0x8B10;     // int; 10 = monster-transformed (kills DRAIN abs)

            internal const long BackFloorDoubleAddr  = EnemyAddresses.MainMonstorUnit.Base + 0x44; // int; nonzero = kill ABS ×2 (back floors)
            // Per-slot status-flag word (same 0x510-stride family as the collision arrays).
            internal const long SlotStatusFlagsBase  = EnemyAddresses.MainMonstorUnit.Base + 0x55754;
            internal const int  SlotStatusFlagsStride = 0x510;
            internal const int  AbsBonusFlag         = 0x2000;     // slot flag: ABS reward ×AbsBonusMult
            internal const float AbsBonusMult        = 1.2f;       // DAT_002a1af8

            /// <summary>Rollover ceiling: current ABS may grow to this multiple of the weapon's max.</summary>
            internal const int  RolloverFactor       = 2;

            internal static long SlotStatusFlagsAddr(int slot) => SlotStatusFlagsBase + (long)slot * SlotStatusFlagsStride;
            /// <summary>Inventory weapon record address for a character's bag slot.</summary>
            internal static long RecordAddr(int character, int slot)
                => UserStatusBase + (long)character * CharStride + WeaponArrayOffset + (long)slot * InventoryWeaponSlotStride;
        }

        // Rollover display behavior (all RE'd, all native — nothing to patch):
        //   • Weapon menu panel (DrawWeaponStatusTag 0x1FA0B0): gauge width (abs<<7)/max is
        //     CLAMPED to 0x7E — the bar pins at full length while the NUMBERS draw raw
        //     ("104/100"), which is exactly the wanted rollover presentation.
        //   • Item menu weapon panel (DrawWepDamageDraw 0x1F8D30): clamps the abs NUMBER to max
        //     (shows "100/100" for a rolled weapon) — cosmetic inconsistency only.
        //   • In-dungeon HUD (topStatusInfo 0x1B04F0): abs gauge width abs*(len/max) is NOT
        //     clamped — a rolled weapon's HUD abs bar overdraws proportionally (up to 2×).
        //   • Level-up menu gate (WeaponSelectKey case 3): abs >= max enables a free level-up
        //     (abs < max consumes a Powerup Powder, item 0xB2) — rollover keeps this working.

        // ── Weapon-menu selection globals (read by GetNowSelectWeapon, ELF 0x1F3F00: selected
        // record = menu base + char*0xAA8 + 0x450C + slot*0xF8). Values persist (stale) after
        // the menu closes — gate on them only for things that can't matter outside the menu. ──
        internal const long MenuSelectedWeaponSlot = 0x21D9EA74; // byte: selected weapon slot (0-9)
        internal const long MenuSelectedCharacter  = 0x21D9EA75; // byte: selected character (0=Toan)
        // Weapon-menu submenu state machine (DAT_01d9ea72, driven by WeaponSelectKey ELF 0x1FDF20).
        internal const long MenuSubMode            = 0x21D9EA72; // byte: submenu state
        internal const int  MenuSubModeOptionList         = 1;   // the Attach/BuildUp/StatusBreak option list
        internal const int  MenuSubModeStatusBreakConfirm = 4;   // the "Do status break?" Yes/No dialog
        // Cursor global (DAT_01d9ea90): the option-list index in state 1, the Yes/No cursor
        // (0=Yes 1=No) in the confirm states. SetStatusBreak fires only on (cursor==0 && X).
        internal const long MenuYesNoCursor               = 0x21D9EA90;

        // ── CWeaponLevelUp object (fixed @ 0x21DAA8E0): runs the level-up / status-break /
        // build-up animation flows (Step ELF 0x237010). Fields RE'd from SetStatusBreak/Step:
        //   +0x1302 (0x21DABBE2, short): flow KIND — -1 idle, 1 = status break
        //   +0x1314 (0x21DABBF4, short): flow STATE. Status break: 4 = effect load,
        //     5 = break animation playing (ON COMPLETION case 5 deletes the bag record, bumps
        //     the message counter and queues the "SynthSphere created!" presentation),
        //     6 = quiet wind-down -> native exit.
        // INTERRUPT LEVER: while in state 5, writing state = 6 skips case 5 entirely — the
        // weapon is never deleted, no "created" presentation fires, and the flow exits through
        // its own native wind-down. The sphere (written to the board at confirm by
        // SetStatusBreak) must be cleared separately. Used by the 7BS below-+7 refusal. ──
        internal const long LevelUpFlowKind   = 0x21DABBE2;
        internal const long LevelUpFlowState  = 0x21DABBF4;
        internal const int  FlowKindLevelUp       = 0;   // set by SetLevelUpValue at the menu confirm
        internal const int  FlowKindStatusBreak   = 1;
        internal const int  BreakStateAnimation   = 5;
        internal const int  BreakStateWindDown    = 6;

        // ⚠ LESSON (2026-07-05): PINE writes to EE CODE pages crash PCSX2 outright — the first
        // value-changing poke to a hot instruction killed the emulator the same second (log:
        // 19:24:09). Identical-value writes were benign. ALL patching must stay data-only; the
        // 7 Branch Sword status-break effect is therefore implemented post-hoc on the sphere
        // (see AttachBoard below + CustomToanEffects.SevenBranchSwordEffect), not by patching
        // WeaponStatusBreakEnable/SetStatusBreak.

        /// <summary>The status-break stat-transfer factor: a DATA float (0.6f) at native 0x2A1890,
        /// read live by SetStatusBreak (ELF 0x2368D0) at confirm time — poking it is safe (data,
        /// not code). SHARED with CCloth::Step (cape physics), FishLineStep and BtSystemScriptInit,
        /// so it is swapped to 0.77 only while a 7 Branch Sword is the selected menu weapon and
        /// restored otherwise. Menus freeze the world, so the main exposure is the stale-selection
        /// window after closing the menu (cape/fishline running with 0.77) — logged for evaluation.</summary>
        internal const long  StatusBreakFactorFloat   = 0x202A1890;
        internal const float StatusBreakFactorDefault = 0.6f;
        internal const float StatusBreakFactorSeven   = 0.77f;

        /// <summary>The game's low-durability warning threshold: the HUD WHP gauge blinks while
        /// <c>WHP &lt;= LowWhpWarningFraction * maxWHP</c> (<c>DrawWepDamageDraw</c> ELF 0x1F8D30,
        /// float constant at native 0x2A1870). Used by the Maneater drain to match the visible state.</summary>
        internal const float LowWhpWarningFraction = 0.1f;
    }
}
