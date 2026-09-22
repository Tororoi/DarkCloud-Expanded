namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Floor enemy slot array — 16 live enemy slots laid out contiguously in RAM.
    /// Use <see cref="SlotAddr"/> to compute the address of any field in any slot.
    /// </summary>
    internal static class EnemyAddresses
    {
        internal static class FloorSlots
        {
            internal const int SlotBase = 0x21E16BA0; // base address of slot 0 (renderStatus / visible)
            internal const int Stride   = 0x190;      // bytes between consecutive enemy slots
            internal const int Count    = 16;

            /// <summary>RAM address of <paramref name="fieldOffset"/> within slot <paramref name="slot"/>.</summary>
            internal static int SlotAddr(int slot, int fieldOffset) => SlotBase + slot * Stride + fieldOffset;
        }

        /// <summary>
        /// CMainMonstorUnit — the parent global (= -0x6320($gp), size 0x60750) that owns both the FloorSlots
        /// array (at Base+0x1E3D0) and the ModelScale / render-object table (ModelBase, at Base+0x1FD60) as
        /// sub-arrays. RE'd across the enemy spawn / motion paths.
        /// </summary>
        internal static class MainMonstorUnit
        {
            internal const long Base      = 0x21DF87D0;
            internal const int  SpeciesRowCount = 0x48;   // int — species rows the loader has set up on the floor (SetupBaseModel counts them)
            internal const int  LiveCount = 0x4C;     // int — number of live enemies on the floor; decrement when freeing a slot
            internal const int  ScriptRunning = 0x50; // int[16] — per slot: 1 while its script label is mid-run (Step resumes it), 0 = Step starts label 100 next frame
            internal static long ScriptRunningAddr(int slot) => Base + ScriptRunning + (long)slot * 4;
            /// <summary>The loader's copy of each species' record (EnemySpeciesTable's 0x9C bytes), one per row: SetupBaseModel
            /// overwrites its shot-config indices (+0x68/+0x6A) with the pack SLOT each config got, and SetupViewMonstor copies
            /// them to a unit's FloorSlots block (+0xAC/+0xAE) when it spawns. A refused config is stored as −(index + 2) by the
            /// shot-slot sharing cave (SharedShots).</summary>
            internal const int  SpeciesRows = 0x1DE30;
        }

        /// <summary>
        /// CCharacter array inside CMainMonstorUnit (stride 0x3510, starting at Base+0x1FCD0), indexed by the
        /// same monster/floor-slot index as <see cref="FloorSlots"/>. This is the array the STB script reads/
        /// writes: <c>_GET_POSITION(-1)</c>/<c>_GET_MONSTOR_POS(idx)</c> resolve to it (handlers @ELF 0x1e1df0/
        /// 0x1e4920), and <c>_SET_POSITION</c>/<c>_SET_MOVE</c> drive it. The live world position is the float
        /// triple at <see cref="PosOffset"/> = (X, Z/height, Y) — distinct from the floor-slot LocationX/Y,
        /// which can stay frozen (e.g. korinoya the ice-arrow emitter). The flying, homing ice arrow IS this
        /// position moving. Vtable ptr sits at +0xA0; <c>_GET_POSITION(-2)</c> = player @0x21EA1D30.
        /// </summary>
        internal static class CharObjects
        {
            internal const long ArrayOffset = 0x1FCD0;  // Base + slot*Stride + ArrayOffset = CCharacter
            internal const int  Stride      = 0x3510;
            internal const int  PosOffset   = 0x10;      // float triple: X @+0x10, Z/height @+0x14, Y @+0x18

            /// <summary>EE address of the CCharacter for <paramref name="slot"/>.</summary>
            internal static long CharAddr(int slot) => MainMonstorUnit.Base + (long)slot * Stride + ArrayOffset;
            /// <summary>EE address of the live position triple (X,Z,Y) for <paramref name="slot"/>.</summary>
            internal static long PosAddr(int slot) => CharAddr(slot) + PosOffset;
        }

        /// <summary>
        /// Enemy GUARD windows — the "guard motion frames" that make a defending enemy block the player's hit.
        /// RE'd 2026-07-07. Each guarding enemy (69/167 species) registers up to 3 windows once in its STB
        /// _INITIALIZE via <c>_SET_GUARD_FRAME(startFrame, endFrame)</c> (cmd 244, handler 0x1E60B0), stored in
        /// CMainMonstorUnit at <c>Base + slot*Stride</c>: a flag short at +0x60550 (per window, stride 2), a start
        /// frame float at +0x60558 and an end frame float at +0x60564 (per window, stride 4). The enemy is
        /// "guarding" whenever its current motion frame (CCharacter +0x1FFC0) falls inside an active window; at
        /// that moment CMonstorUnit::CheckDmg (0x1D9F10) blocks the player's hit — plays the guard clink (SE 0xA2),
        /// nudges the enemy back, and SKIPS the damage. Zeroing the flag makes CheckDmg's guard check find no
        /// active window, so the hit lands (the enemy still animates its guard). Written once at spawn (not per
        /// frame), so a data-side zero holds with no race. Used by DarkCloud.GuardCrushEffect (guard-break).
        /// </summary>
        internal static class GuardWindows
        {
            internal const int  Stride       = 0x20;      // per enemy slot (matches CheckDmg slot*0x20)
            internal const int  FlagOffset   = 0x60550;   // short[3] — window active flags (stride 2)
            internal const int  StartOffset  = 0x60558;   // float[3] — window start frame (stride 4)
            internal const int  EndOffset    = 0x60564;   // float[3] — window end frame (stride 4)
            internal const int  WindowCount  = 3;

            /// <summary>EE address of <paramref name="window"/>'s guard-active flag short for <paramref name="slot"/>.</summary>
            internal static long FlagAddr(int slot, int window)
                => MainMonstorUnit.Base + (long)slot * Stride + FlagOffset + window * 2;
        }

        /// <summary>
        /// Enemy MELEE attack parameters — the per-damage-collision values an enemy sets once in its STB
        /// _INITIALIZE via <c>_SET_DMG_PARA(damage, statusFlags, reactionType[, launch])</c>. Stored in
        /// CMainMonstorUnit at <c>Base + slot*0x350 + col*4</c>: damage int @+0x5A4D0, status-flag int @+0x5A590,
        /// and the HIT REACTION TYPE int @+0x5A510 (2=knockback/guardable, 3=knockdown/guard-break/unguardable,
        /// 4=light). CMonstorUnit::CheckDmg (0x1D9F10) rebuilds the enemy's attack CCollisionData entry from these
        /// each frame (reaction → entry +0x4C), which BtCheckDamageProc reads to decide guardability. So writing
        /// ReactionAddr from 3→2 makes that melee attack guardable (the player's guard then blocks it). See
        /// [[guard-break-and-knockback]] and SeventhHeaven.DivineGuardEffect. Up to 16 cols per enemy.
        /// </summary>
        internal static class EnemyAttackParams
        {
            internal const int SlotStride     = 0x350;
            internal const int ColStride      = 4;
            internal const int MaxCols        = 16;
            internal const int ReactionOffset = 0x5A510; // int — melee hit reaction type (2/3/4)

            /// <summary>EE address of the melee reaction-type int for <paramref name="slot"/>'s dmg-col <paramref name="col"/>.</summary>
            internal static long ReactionAddr(int slot, int col)
                => MainMonstorUnit.Base + (long)slot * SlotStride + col * ColStride + ReactionOffset;
        }
    }

    /// <summary>
    /// Cached per-attack MELEE damage — the array that <c>_SET_DMG_PARA</c> (STB cmd 132, handler ELF 0x1E3FD0)
    /// writes its arg0 into at the enemy's INIT. A per-slot sub-array of CMainMonstorUnit at
    /// Base + slot*0x350 + 0x5A4D0, indexed by body-part (the index comes from a per-slot field at +0x5A690);
    /// entry stride 4 (int). This is the value <c>BtCheckDamageProc</c> ultimately subtracts player defense from.
    ///
    /// ★ This is the live MELEE-damage normalization hook (confirmed 2026-06-19): patching an entry here changes
    /// the dealt damage immediately (e.g. Arthur slot's two 116 entries → 58 ⇒ his hits dropped 62→4 at def 54).
    /// Patching the STB script value instead is too late — _SET_DMG_PARA already latched it here at spawn.
    /// (The _SET_DMG_PARA value list per species is mirrored in EnemyDefaults.MeleeDamage.)
    /// </summary>
    internal static class DmgParaCache
    {
        internal const int OffsetInUnit = 0x5A4D0; // base offset within CMainMonstorUnit
        internal const int SlotStride   = 0x350;   // per-slot
        internal const int EntryStride  = 4;       // per body-part damage int (int32)
        // 16, NOT 32. The per-slot attack block is a set of PARALLEL 16-entry arrays spaced 0x40 apart:
        //   0x5A450 frame ptr | 0x5A490 radius | 0x5A4D0 DAMAGE (this) | 0x5A510 hit REACTION | 0x5A5D0 start...
        // A 32-entry window here is 0x80 wide and therefore runs straight into the reaction array
        // (EnemyAttackParams.ReactionOffset, MaxCols=16) — which EnemyStatScaler then value-matches and
        // REWRITES. Reaction types are 2/3/4, and weak enemies really do have melee damage of 2/3/4, so the
        // damage scaler was silently corrupting hit reactions (7th Heaven's guard-break lever). Do not widen it.
        internal const int EntryCount   = EnemyAddresses.EnemyAttackParams.MaxCols;   // 16 — one per attack column
        internal const int ReadLength   = EntryCount * EntryStride; // 0x40 — exactly this array, no neighbours

        /// <summary>EE base address of <paramref name="slot"/>'s per-body-part melee-damage array.</summary>
        internal static long ArrayAddr(int slot) => EnemyAddresses.MainMonstorUnit.Base + (long)slot * SlotStride + OffsetInUnit;
    }

    /// <summary>
    /// The enemy DAMAGE HITBOX — the per-bone collision spheres your weapon must reach to hurt the enemy
    /// (RE'd + confirmed in-game). NOT EntityScale and NOT the BODY_SIZE triple; this is its own system.
    ///
    /// PIPELINE:
    ///   • At spawn the enemy's STB script issues <c>_SET_BODY_COL(boneName, radius, …)</c> (cmd 0x82, handler ELF
    ///     0x1E39F0), which records, per (slot, bodyPart): the bone CFrame ptr + this RADIUS into the arrays below.
    ///     Simple enemies = 1 sphere (Skeleton Soldier r=6.0, Cave Bat 6.5); bosses = many (Dran: body 20, arms 7…).
    ///   • Every frame <c>CMonstorUnit::CheckDmg</c> (ELF 0x1D9F10) reads this DEFINITION radius + the bone's current
    ///     world position and calls <c>CCollisionData::Set</c> (0x1B57A0) to (re)build a live collision sphere, whose
    ///     radius lands at the CCollisionData element +0x3C.
    ///   • When the player swings, <c>BtCheckDamageProc</c> (dun overlay 0x1DBAFD0) calls
    ///     <c>CCollisionData::CheckHitUser</c> (0x1B5920) → a hit registers when dist(weaponPoint, bonePos) ≤ radius.
    ///
    /// Because CheckDmg rebuilds the sphere each frame FROM this definition, a SINGLE write here (at spawn) changes
    /// the hitbox permanently — no per-frame patching needed (confirmed: writing 30.0 once let a sword hit a skeleton
    /// from ~5× its body radius). Patching the STB literal does NOT work (it only runs at init); patch this instead.
    /// Per-species sphere bone+radius lists are in /enemy-body-collision-table.md (by TableIndex).
    /// </summary>
    internal static class BodyCollision
    {
        // Per-slot body-collision DEFINITION block within CMainMonstorUnit (G = MainMonstorUnit.Base). The sub-arrays
        // are parallel, each indexed by slot*SlotStride + bodyPart*BodyPartStride; up to ~16 body parts per enemy.
        internal const long FramePtrArray = 0x55350; // CFrame* of the bone the sphere is attached to
        internal const long RadiusArray   = 0x55390; // ★ float — the sphere radius = the hittable size (the knob)
        internal const long Param1Array   = 0x553D0; // float — _SET_BODY_COL 3rd arg (0 unless argc==4); height band?
        internal const long Param2Array   = 0x55410; // float — _SET_BODY_COL 4th arg (0 unless argc==4)
        internal const long CentreArray   = 0x55250; // vec4 (CentreStride) — the sphere's world centre, rebuilt each frame
        internal const long ActiveArray   = 0x55450; // int — 1 while the sphere is in use
        internal const long SpareArray    = 0x55490; // 5 ints (SpareStride) — _SET_BODY_COL_PARA's spare table; [1] = the kick type admitted at [0] %
        internal const long DamagePctArray = 0x555D0; // 6 ints (DamagePctStride) — damage % by attacker character (_SET_BODY_COL_PARA 10+char)
        internal const long LastHitSphere = 0x55750; // int per SLOT — the sphere the last accepted hit landed on; CheckDmg writes it past its guard and invincibility gates
        internal const int  SlotStride     = 0x510;
        internal const int  BodyPartStride = 4;
        internal const int  CentreStride   = 0x10, SpareStride = 0x14, DamagePctStride = 0x18;
        internal const int  MaxBodyParts   = 16;     // each sub-array spans 0x40 (= 16 floats) before the next one

        /// <summary>EE address of a slot's body-collision block: add the array and the part's stride.</summary>
        internal static long SlotBase(int slot) => EnemyAddresses.MainMonstorUnit.Base + (long)slot * SlotStride;

        /// <summary>EE address of (slot, bodyPart)'s hitbox sphere radius. Write once at/after spawn to resize it.</summary>
        internal static long RadiusAddr(int slot, int bodyPart = 0) =>
            EnemyAddresses.MainMonstorUnit.Base + RadiusArray + (long)slot * SlotStride + (long)bodyPart * BodyPartStride;
    }

    /// <summary>
    /// Per-slot ATTACK-collision (the enemy's WEAPON hitbox = enemy→player) sphere definitions — the counterpart of
    /// <see cref="BodyCollision"/>. Built at spawn by <c>_SET_DMG_COL(boneName, radius, startFrame, endFrame)</c>
    /// (STB cmd 131, handler ELF 0x1E3DB0): for each of up to 16 sphere slots it records the bone CFrame ptr, the
    /// RADIUS, the active start/end frames, and an "occupied" flag. <c>CMonstorUnit::CheckDmg</c> rebuilds the live
    /// sphere each frame from the RADIUS + the bone's (model-scaled) world position; <c>BtCheckDamageProc</c> tests it
    /// against the player during the attack's hit-window frames. A SINGLE write here (at/after spawn) resizes it, same
    /// as BodyCollision — the STB literal only runs at init.
    ///
    /// ★ This is a DIFFERENT array from BodyCollision (0x55390), so the miniboss body-hitbox scaling never touches it.
    /// When a miniboss model is scaled up, the weapon bone rides higher but this radius stays default → the attack
    /// sphere overshoots short characters. Scaling this radius compensates.
    ///
    /// FULL ARRAY MAP (RE'd from handler 0x1E3DB0). Every field is a parallel sub-array indexed by
    /// <c>slot*SlotStride + sphere*SphereStride</c> within CMainMonstorUnit (G = MainMonstorUnit.Base); up to 16
    /// spheres per slot. In the handler the per-sphere store base is <c>at = G + slot*0x350 + sphere*4 + 0x60000</c>
    /// and each field is a negative displacement off <c>at</c> — converted to a G-relative array offset below:
    ///   • FramePtr   (CFrame* of the attachment bone)  at−0x5BB0 → 0x5A450   (set from SearchFrame(boneName))
    ///   • Radius     (float, the sphere size / knob)    at−0x5B70 → 0x5A490
    ///   • StartFrame (float, hit-window open)           at−0x5A30 → 0x5A5D0
    ///   • EndFrame   (float, hit-window close)          at−0x59F0 → 0x5A610
    ///   • Flag       (int, nonzero once sphere in use)  at−0x59B0 → 0x5A650
    /// Plus a per-SLOT (not per-sphere) field at G + slot*0x350 + 0x5A690 (at−0x5970) holding the index of the sphere
    /// slot last allocated (a count/cursor), written without the sphere*4 term. Slot stride is 0x350 (confirmed via the
    /// EE 3-operand <c>mult $s4,$s2,0x350</c>), distinct from BodyCollision's 0x510.
    /// </summary>
    internal static class AttackCollision
    {
        // Parallel per-(slot,sphere) sub-arrays. G-relative offsets; address = MMU.Base + <Array> + slot*SlotStride + sphere*SphereStride.
        internal const long FramePtrArray   = 0x5A450; // CFrame* of the bone the attack sphere is attached to
        internal const long RadiusArray     = 0x5A490; // ★ float — the enemy attack sphere radius (the knob)
        internal const long StartFrameArray = 0x5A5D0; // float — hit-window open frame
        internal const long EndFrameArray   = 0x5A610; // float — hit-window close frame
        internal const long FlagArray       = 0x5A650; // int   — nonzero once this sphere slot is in use
        internal const int  SlotStride   = 0x350;
        internal const int  SphereStride = 4;
        internal const int  MaxSpheres   = 16;

        internal static long RadiusAddr(int slot, int sphere = 0) =>
            EnemyAddresses.MainMonstorUnit.Base + RadiusArray + (long)slot * SlotStride + (long)sphere * SphereStride;
        internal static long FlagAddr(int slot, int sphere = 0) =>
            EnemyAddresses.MainMonstorUnit.Base + FlagArray + (long)slot * SlotStride + (long)sphere * SphereStride;
    }

    /// <summary>
    /// Per-slot live MOVE SPEED — the per-frame scalar the engine advances the enemy by. Written by <c>_SET_MOVE</c>
    /// (STB cmd 32, handler ELF 0x1E27C0): it normalizes the requested move vector and stores its magnitude here
    /// (store <c>swc1 f0, -0x1BB0(at)</c> with <c>at = MMU.Base + slot*0x3510 + 0x20000</c> ⇒ offset 0x20000−0x1BB0 =
    /// 0x1E450). The normalized direction sits alongside it at 0x1E430/0x1E434/0x1E438 (x/y/z). Same per-slot 0x3510
    /// stride as the CCharacter/model arrays.
    ///
    /// ★ WHY A PER-SLOT SPEED-UP IS NOT FEASIBLE (RE'd; a miniboss "move faster" attempt was dropped here).
    /// <c>CMonstorUnit::Step</c> (ELF 0x1DE540) integrates motion as a plain <c>pos += dir * speed</c> per axis
    /// (0x1DE60C–0x1DE674: lwc1 dir@-0x1BD0/-0x1BCC/-0x1BC8 × speed@-0x1BB0, add to pos) — there is NO third per-slot
    /// scale factor to set once. Both dir and speed are REWRITTEN every frame by _SET_MOVE / _CHK_MOVE while an enemy
    /// steers or chases, straight from the species' SHARED STB literal, so a per-tick PINE write to this field always
    /// loses the race (verified: even ×5 produced no visible change). The only per-slot speed modifier in _SET_MOVE is
    /// a boolean "halve" flag (int @ slot 0x1E3E4, store at−0x1C1C: if >0, speed ×0.5) — it can only SLOW, never speed
    /// up. And same-species slots share one STB native pointer, so there is no per-slot script to patch. ⇒ The only
    /// race-free speed lever is the shared per-species _SET_MOVE STB literal (what FasterEnemies/"Faster enemies"
    /// patches); a true per-enemy speed scale does not exist in this engine path. This field remains useful READ-ONLY
    /// (probe an enemy's live move speed) and for the one-shot "halve" flag if a slow-down is ever wanted.
    /// </summary>
    internal static class MoveControl
    {
        internal const long SpeedOffset    = 0x1E450; // float — current per-frame move speed (per-slot; rewritten each frame, see notes — not a writable knob)
        internal const long DirXOffset     = 0x1E430; // float — normalized move direction x (y @ 0x1E434, z @ 0x1E438)
        internal const long HalveFlagOffset = 0x1E3E4; // int   — if > 0, _SET_MOVE halves this slot's speed (the only per-slot speed modifier; slow-only)
        internal const int  SlotStride     = 0x3510;  // same per-slot stride as the CCharacter/model arrays
        internal static long SpeedAddr(int slot) =>
            EnemyAddresses.MainMonstorUnit.Base + SpeedOffset + (long)slot * SlotStride;
    }

    /// <summary>
    /// Cached per-slot PROJECTILE (shot) damage — the field that <c>_SET_SHOT</c> (STB cmd 134, handler ELF
    /// 0x1E4120) and <c>_SET_SHOT2</c> (STB cmd 136, handler 0x1E4310) write each time the enemy fires. A per-slot
    /// sub-array of CMainMonstorUnit, stride 0x30 (one entry per FloorSlots/CCharacter slot):
    ///   SHOT  struct base  = MMU.Base + slot*0x30 + 0x5FF50,  damage int @ +0x5FF78
    ///   SHOT2 struct base  = MMU.Base + slot*0x30 + 0x60250,  damage int @ +0x60278   (SHOT2 array sits exactly
    ///   after the 16-slot SHOT array: 16*0x30 = 0x300, and 0x60250 − 0x5FF50 = 0x300.)
    /// Struct fields : +0x00/+0x04/+0x08 = three floats (shot params), +0x0C = 1.0f, +0x20 =
    /// validated effect handle, +0x24 = active flag, +0x28 = DAMAGE (int).
    ///
    /// ★ DAMAGE SEMANTICS — UNLIKE MELEE, THIS IS NOT INIT-LATCHED. The handler inits the damage to −1 and only
    /// overwrites it from the STB 5th arg when the call has 5 args (explicit-damage shots). CMonstorUnit::Step
    /// (ELF 0x1DD540) reads the field every frame (reader @0x1DEF88 for SHOT, 0x1DF090 for SHOT2): if the active
    /// flag is set AND damage != −1 it spawns the shot via 0x1AE610 passing the damage, then CLEARS the active
    /// flag. So the active flag is re-armed by _SET_SHOT once per shot, and the damage is re-written from the STB
    /// constant on every shot. CONSEQUENCE: writing this field live loses the race — the next _SET_SHOT overwrites
    /// it from the script before Step fires. The STABLE normalization target is the STB constant the script pushes
    /// (op1 push-const → constant pool), patched once per floor via CRunScript.StbPtr(slot) — NOT this field.
    /// This field is still useful READ-ONLY: after a shooter's first shot the native damage persists here (only the
    /// active flag toggles), so probing it confirms a species' live shot damage. (The per-species explicit values
    /// are mirrored in EnemyDefaults.ProjectileDamage; default/−1 shots source their damage at fire time elsewhere.)
    ///
    /// STB VM reference (RE'dexe__10CRunScript @0x23E080): vmcode_t = 12-byte {op@+0, operandA@+4,
    /// operandB@+8}; opcode dispatch table @0x29FB80 (31 ops). op3 = push-LITERAL (operandA = type 1=int/2=float/
    /// 3=string, operandB = the inline value), op1 = push-VARIABLE (operandB = scope-size, operandA = index into
    /// runtime storage; no inline value), op20 = _PRINT, op21 = external-command call (ext @0x23DD00 → dispatch
    /// table @0x2917C8, cmd id via per-program funcdata). GetStackInt/Float read 8-byte {tag,value} args. Cmd ids:
    /// _SET_DMG_PARA 132, _SET_SHOT 134, _SET_SHOT2 136. So an explicit shooter's damage is an op3 literal (operandB
    /// patchable in place); a "default" shooter (Golem/Sam/Crescent Baron) pushes it via op1 from a runtime source
    /// (no literal — overwrite that push record with an op3 literal carrying the scaled value to normalize it).
    /// </summary>
    internal static class ShotDmgCache
    {
        internal const int SlotStride   = 0x30;     // per enemy slot

        internal const int ShotBase     = 0x5FF50;  // SHOT  struct base within MMU
        internal const int Shot2Base    = 0x60250;  // SHOT2 struct base within MMU
        internal const int DamageField  = 0x28;     // int damage, relative to a struct base (−1 = no explicit dmg)
        internal const int ActiveField  = 0x24;     // int active flag (set by _SET_SHOT, cleared by Step after firing)

        /// <summary>EE address of <paramref name="slot"/>'s SHOT damage int (+0x5FF78).</summary>
        internal static long ShotDamageAddr(int slot)  => EnemyAddresses.MainMonstorUnit.Base + (long)slot * SlotStride + ShotBase  + DamageField;
        /// <summary>EE address of <paramref name="slot"/>'s SHOT2 damage int (+0x60278).</summary>
        internal static long Shot2DamageAddr(int slot) => EnemyAddresses.MainMonstorUnit.Base + (long)slot * SlotStride + Shot2Base + DamageField;
    }

    /// <summary>Enemy slot field offsets relative to slot base address.</summary>
    internal static class EnemySlotOffsets
    {
        // ── Status / Timers ──────────────────────────────────────────────────
        internal const int RenderStatus      = 0x000; // int   — 0=inactive, 1=spawned (not yet aggro'd), 2=active; transitions 1→2 when enemy enters play; -1 = release/death-processing frame (the CMonstorUnit::Step death block — kill-ABS grant + drop spawn — runs on it)
        internal const int KillerCharId      = 0x004; // int   — character id of the last damager; the death block grants kill ABS only when this equals the active character
        internal const int FreezeTimer       = 0x008; // int   — freeze status countdown; 0 at rest
        internal const int PoisonPeriod      = 0x00C; // int   — poison tick interval; 0 at rest
        internal const int StaminaTimer      = 0x010; // int   — stamina/status countdown; starts at a large value (e.g. 0x004F0000 ≈ 5.2M) and decrements each frame; 0 when expired
        internal const int GooeyState        = 0x014; // int   — gooey/slime status; 0 at rest
        internal const int StatusSusceptibility = 0x0DE; // short — species ItemStatusRes copy (unit +0x1E4AE): 0 = immune to poison/freeze/gooey
        internal const int DistanceToPlayer  = 0x018; // float — live distance to player in world units; updated each frame; used as proximity filter

        // ── HP / Stats ───────────────────────────────────────────────────────
        internal const int MaxHp             = 0x020; // int   — maximum HP; set from species data at spawn
        internal const int Hp                = 0x024; // int   — current HP; decrements on hit; enemy dies when ≤0
        // Each resistance pack holds two ushorts: (low ushort, high ushort)
        internal const int ResistancePack1   = 0x028; // [Category, FireRes]      — Category = enemy category index; fire confirmed; scale: 100=neutral, >100=weak, <100=resistant
        internal const int ResistancePack2   = 0x02C; // [IceRes, ThunderRes] — both confirmed
        internal const int ResistancePack3   = 0x030; // [WindRes, HolyRes]   — wind confirmed
        internal const int MinGoldDrop       = 0x034; // int   — minimum gold dropped on death. ⚠ Also what CheckDmg's element branch
                                                       //   multiplies by when a player-side entry's +0x50 holds ONLY status bits (element index 5
                                                       //   = one short past the resistance row) — a vanilla quirk; never plant status bits in +0x50.
        internal const int DropChance        = 0x038; // int   — item drop chance (0–100)

        // ── Identity ─────────────────────────────────────────────────────────
        internal const int EnemySpeciesId    = 0x042; // ushort — enemy species ID; used to look up name, stats, and model data
        // EntityScale / EntityScaleCopy = the enemy's PHYSICAL (movement) collision radii — NOT the combat hitbox (the
        // hittable/attack spheres are STB-driven: _SET_BODY_COL / _SET_DMG_COL, RadiusArray @ MMU+0x55390/0x5A490).
        // CONFIRMED in-game (2026-06-25, Skeleton Soldier at 10x and at 0):
        //   • EntityScale (0x044) = the enemy's own movement-CLEARANCE buffer. MoveCheck/MoveCheck2 block the enemy when
        //     `distance < 6.0 + EntityScale` to walls/the player, keeping it ~EntityScale away. ONE-DIRECTIONAL — it
        //     gates the ENEMY's movement only; the player can still walk right up to it (player collision is separate),
        //     and the model/combat are unaffected. At 60 (10x): big berth + a glitch-jump/jitter when forced too close
        //     (engine snaps it out of the overlap). At 0: normal — walls are still avoided (wall solidity is the
        //     walkable mesh / pathfinding, independent of this buffer); EntityScale only adds clearance on top.
        //   • EntityScaleCopy (0x048) = monster-vs-monster separation (MoveChecMonster). At 0: enemies OVERLAP each
        //     other; large: they can't cluster. Settable per-script via the STB cmd _SET_COLLISION_WIDTH (0x1E5DC0).
        // Both copied from record +0x060 at spawn. CheckDmg also reads EntityScale (hit-effect radius).
        internal const int EntityScale       = 0x044; // float — enemy movement-clearance buffer vs walls/player (not the hitbox); see comment
        internal const int EntityScaleCopy   = 0x048; // float — monster-vs-monster separation radius (settable via STB _SET_COLLISION_WIDTH); see comment
        // SpeciesDataPtr is a PS2-native pointer (0x00xxxxxx range) to the loaded c16a/stb behavior
        // block for this species. All slots of the same species share one block (e.g. every Pirate's
        // Chariot = 0x01C05140). The block begins with a MIPS function pointer table; the on-death
        // callback is believed to sit at a low word index within that table. Nulling the relevant
        // pointer prevents the boss-defeat sequence from firing without touching the species table.
        // Add 0x20000000 to convert the PS2-native pointer to a PCSX2-readable address.
        // See LogBossSlotSpeciesDataPtrs() in EnemySpecies.cs for live-dump diagnostic.
        internal const int SpeciesDataPtr    = 0x04C; // int   — PS2-native ptr to loaded species behavior block (shared across all slots of same species)
        internal const int AiStateCounter    = 0x050; // int   — AI substate tick counter; small value oscillating ~6–25; updates each game frame; not monotonically increasing

        // ── Facing Direction (3D unit vector, updated each frame as enemy turns) ──
        // Magnitude of (FacingX, FacingY, FacingZ) = 1.0 (confirmed).
        // FacingY is 0.0 for ground-bound enemies; nonzero for hovering/flying types (e.g. Auntie Medu FacingY ≈ 0.19).
        // Both FacingX and FacingZ are snapshotted into HitFacingX/HitFacingZ on the frame a hit lands.
        internal const int FacingX           = 0x060; // float — X component of facing unit vector
        internal const int FacingY           = 0x064; // float — Y (vertical) component; 0.0 for ground enemies, nonzero for flying/hovering
        internal const int FacingZ           = 0x068; // float — Z component of facing unit vector

        // ── Pathfinding Target (player world position, tracked in real time) ──
        // Engine writes the player's current dungeon position here each frame so enemies path toward them.
        // Confirmed by cross-referencing [PlayerState] dunPosition values with enemy target on the same poll frame.
        // Coordinate convention matches LocationX/Z/Y (see below).
        internal const int TargetX           = 0x070; // float — mirrors player dunPositionX each frame
        internal const int TargetZ           = 0x074; // float — mirrors player dunPositionZ (height/elevation) each frame
        internal const int TargetY           = 0x078; // float — mirrors player dunPositionY each frame

        internal const int MovementBlend     = 0x080; // float — rest value is enemy species specific (Pirate's Chariot=0.70, Auntie Medu=0.50); drops to 0.0 momentarily on hit or AI pause; likely movement speed blend weight

        // CONFIRMED 2026-06-19 (damage RE): packed DEFENSE pair, copied from the species record at spawn
        // (low = record DamageReduction 0x64, high = record WeaponDefense 0x66). NOT attack. These scale how
        // much damage the enemy TAKES, and are the real per-dungeon DURABILITY scalers (e.g. Mimic DBC 1/10 ->
        // Moon Sea 8/30). CMonstorUnit::CheckDmg (ELF 0x1D9F10) reads them: low is subtracted from incoming
        // damage (flat reduction), high is passed to SwordDmgCheck1 (the player-weapon damage check). See the
        // ENEMY → PLAYER DAMAGE block above EnemySpeciesTable.DamageReduction for the full enemy→player formula.
        internal const int DefenseStats      = 0x090; // packed: low ushort = DamageReduction, high ushort = WeaponDefense

        internal const int HitStunTimer      = 0x098; // int   — INVINCIBILITY frame countdown (record +0x1E468): scripts set it with `_STATUS_SET_MUTEKI` (cmd 101: 9 after a hit, 100 when a mimic wakes, 1000 while dying); CheckDmg__12CMonstorUnit skips the whole hit test while > 0

        internal const int ForceItemDrop     = 0x0A0; // int   — forces a specific item drop when nonzero
        internal const int RenderDistance    = 0x0A4; // float — CONFIRMED controls render distance and map-dot appearance threshold
        internal const int Abs               = 0x0B0; // int   — XP reward granted to the player on kill

        // Vertical physics pair, both READ-ONLY (Step recomputes them every frame: HeightAboveGround = enemyZ − GroundZ;
        // Step never writes LocationZ, so these are outputs, not inputs). To move an enemy vertically, write LocationZ (0x104).
        internal const int HeightAboveGround = 0x0B8; // float — READ ONLY; height above the floor: 0 (≈grounded) at rest, >0 when airborne (knockback launch / jump). CONFIRMED: the STB cmd _STATUS_GET_HEIGHT (ELF 0x1E30F0) returns this.
        internal const int GroundZ           = 0x0BC; // float — READ ONLY; floor/ground Z under the enemy (subtracted to get HeightAboveGround; also positions the CHitMark spark in CheckDmg). ~10 in DBC; shifts as the enemy slides.

        // ── Attack Phase & Hit Events ─────────────────────────────────────────
        // AttackPhase is -1 at rest; flips to 0 during the active hitbox window of an attack.
        // HitReactionType and all on-hit fields (+0x130 onward) fire on the same game frame.
        internal const int AttackPhase       = 0x0C4; // int   — -1=idle, 0=active hitbox window (briefly); written by engine during attack animation
        internal const int HitReactionType   = 0x0C8; // READ ONLY — int; 0 at rest; written by engine on hit (Pirate's Chariot=2, Auntie Medu=2 or 5, Mask of Prajna=5); may encode hit type or weapon category rather than a fixed per-type value

        // ── Item Drop / Resistance ────────────────────────────────────────────
        internal const int StealItemId       = 0x0D8; // int   — packed: low ushort = steal item ID; HIGH ushort (0xDA) = "has drop" gate.
        // ★ The high ushort (slot 0xDA) GATES the entire enemy death-drop: CMonstorUnit::Step skips the whole drop block
        // when it reads 0 (exit branch at ELF 0x1DF4C0, `beq high,0 -> 0x1DF9A4`). It is 1 for normal enemies but 0 for
        // the "can't drop" species (flyers, Gol/Sil) — that, not position/death-path, is why those never drop. Writing
        // it nonzero (see MiniBoss.ApplyMiniBossToSlot) lets their forced loot spawn.
        // internal const int ItemDropId = 0x404; // UNCONFIRMED — address 0x21E16FA4 was noted as "item dropped by weapon kill"
        //                                         // but offset 0x404 falls between slots (stride is 0x190); needs re-investigation

        internal const int ItemResistance    = 0x0DC; // int   — packed ushorts; possibly two resistance values; scale resembles elemental resistance (100=neutral, <100=resistant)

        // ── AI State Machine ──────────────────────────────────────────────────
        // AiStatePacked and AiSpeedParam change together at each AI phase transition.
        // Observed AiStatePacked values: 0x00000001 (idle/patrol), 0x00040002, 0x0002000D, 0x00060010 (attack/chase states).
        // AiSpeedParam is enemy species and state-specific: Auntie Medu alternates 0.36↔0.20; Pirate's Chariot uses 0.25/−1.0; Mask of Prajna uses 0.24–0.35.
        internal const int AiStatePacked     = 0x0EC; // int   — packed AI state. RE'd (ELF _SET_MOTION 0x1e1710 / commit 0x1dd890): low halfword (0xEC) = REQUESTED motion id, high halfword (0xEE) = motion flags. _SET_MOTION writes the queued motion here.
        internal const int AiSpeedParam      = 0x0F0; // float — REQUESTED motion speed; _SET_MOTION writes −1.0 (= use the motion's own KEY speed) here, matching the −1.0 seen at spawn.
        // _SET_MOTION (ELF 0x1E1710) in full, for a mod that requests a motion the way a script does: slot+0xC0 = −1, +0xF0 = −1.0;
        // speed = the model's motion table entry (ModelScaleOffsets.MotionTablePtr → entry motion*0x10, float @+8), halved while
        // Gooey (+0x14 > 0); then the render object's id/flags(0)/speed (ModelScaleOffsets.PlayingMotion*) for the body AND each
        // extra part (+0xB4 of them, ModelScaleOffsets.PartStride apart), and the queue words +0xEC (short id) / +0xEE (0) / +0xF0 (speed).
        internal const int MotionRequestAux  = 0x0C0; // int   — set to −1 by _SET_MOTION with every request
        internal const int MotionRequestFlags= 0x0EE; // short — the request's flags (0 = play as the clip is authored; bit 2 is consumed by Step)
        internal const int PartCount         = 0x0B4; // short — extra render parts of a multi-part enemy (each gets the same motion)
        internal const int MotionCommitFlag  = 0x0F4; // halfword — commit gate (-0x1b3c). CMonstorUnit::Step (ELF 0x1dd890) commits the requested motion (0xEC) into the render object's player ONLY when this is nonzero; the engine sets it when the current clip finishes. Writing 1 forces an immediate motion switch (interrupt).

        // ── Lock-on target (regular enemies) — RESOLVED ──
        // +0x0FC: PS2-native pointer to the enemy's LOCK-ON FRAME — the CFrame node named by the STB's
        // `_STATUS_SET_LOCKON_TRG("lockon", w, h)` (handler 0x1E3710: SearchFrame on the species model →
        // unit+slot*400+0x1E4CC; w/h → ReticleWidth/Height below). Same-species slots share the model, hence
        // the shared pointer a savestate analysis puzzled over. +0x100: that frame's WORLD position
        // (vec4 x, h, y, w), refreshed by DrawMonstor (GetWorldPosition) every draw. setTargetCursor (dun
        // 0x1DC07A0) copies it to the lock-on aim point global 0x1DC4500 — the point Xiao's pellets fly at
        // (BattleActionPlay_Jinn); with no lock-on frame the aim point is the enemy origin raised by 8.
        internal const int LockOnFrame       = 0x0FC; // int   — lock-on CFrame ptr (PS2-native); 0 = species set none
        internal const int LockOnPoint       = 0x100; // vec4  — lock-on frame WORLD position, engine-refreshed every draw
        internal const float LockOnFallbackLift = 8f; // aim = origin + this when there is no lock-on frame

        // ── World Position ────────────────────────────────────────────────────
        internal const int LocationX         = 0x100; // float — world X position; updated each frame as enemy moves
        internal const int LocationZ         = 0x104; // float — world Z (height/elevation); small values (8–13 observed) relative to floor
        internal const int LocationY         = 0x108; // float — world Y position; updated each frame as enemy moves
        internal const int ScaleMultiplier   = 0x10C; // float — 1.0 at spawn for all observed enemies; possibly a per-instance scale override; purpose unconfirmed
        internal const int ReticleWidth      = 0x110; // float — CONFIRMED controls horizontal lock-on reticle width
        internal const int ReticleHeight     = 0x114; // float — CONFIRMED lock-on reticle height; may also set hitbox height
        internal const int LockOnDistance    = 0x118; // float — CONFIRMED distance at which lock-on becomes available; default 120.0
        internal const int Opacity           = 0x120; // float — CONFIRMED enemy opacity; default 128.0; lower = more translucent; PalletStep subtracts OpacityFadeStep (0x124) each frame; drops to ~44 on hit then recovers
        // Per-frame opacity FADE STEP: PalletStep does Opacity (0x120) -= this every frame (while OpacityFadeGate 0x128
        // == 0). + fades the enemy OUT, − fades it IN, 0 = hold. Set by the STB command _STATUS_SET_ALPHA (ELF
        // 0x1E2C60). 0.0 at rest.
        internal const int OpacityFadeStep   = 0x124; // float — per-frame Opacity decrement (fade rate); see comment
        internal const int OpacityFadeGate   = 0x128; // int — when nonzero, PalletStep PAUSES the opacity fade (skips the -= step)

        // ── Flash Overlay System ──────────────────────────────────────────────
        // CONFIRMED: write FlashColorRGB → FlashDecayRate → FlashTimer → FlashActivation to trigger a flash.
        // Activation values 1 and 2 both produce visible flashes; color is set entirely by the RGB channels.
        // Stamina yellow tint is NOT driven by these — all RGB channels read 0.0 at spawn even with stamina active.
        // Hit flash color and decay rate are enemy species specific:
        //   Pirate's Chariot: R=255 G=0   B=0   (red),    FlashDecayRate=0.08 (~12 frames at 30fps)
        //   Auntie Medu:      R=255 G=255 B=0   (yellow), FlashDecayRate=0.20 (~5 frames at 30fps)
        //   Mask of Prajna:   R=255 G=0   B=0   (red),    FlashDecayRate=0.20 (~5 frames at 30fps)
        // FlashDecayRate: engine negates internally (write +0.08, stored −0.08); duration = 1.0/rate frames; for ~2s at 30fps write 0.016.
        // FlashTimer: engine always resets to 1.0 at flash-start regardless of written value; decrements by FlashDecayRate each frame.
        internal const int FlashColorRed     = 0x130; // float — 0.0 at rest; 0–255 red channel
        internal const int FlashColorGreen   = 0x134; // float — 0.0 at rest; 0–255 green channel; 0 for red-only flash types
        internal const int FlashColorBlue    = 0x138; // float — 0.0 at rest; 0–255 blue channel

        // ── Ambient lighting (engine-owned working copy; NOT mod-settable) ─────────────
        // 0x140-0x14C are a 4-float ambient color (R,G,B,A). SOURCE: PalletStep calls MGGetAmbient to read the SCENE's
        // global ambient (≈10,20,20,128 in dungeons) and sceVu0CopyVectors it here every frame, then interpolates the
        // RGB (0x140-0x148, a 3-component loop) toward FlashColor (0x130-0x138) by FlashTimer (0x16C) — i.e. the
        // enemy's ambient = scene ambient flash-blended with the hit color. PalletSet then reads 0x140-0x148 (using
        // Opacity 0x120 as the alpha, NOT 0x14C) and calls MGSetAmbient to apply it to the model. So the "resting
        // 10,20,20,128" is just the global scene ambient (same for every enemy), refreshed each frame — a mod write
        // is overwritten next frame. (Formerly "BehaviorRange"; the "expands during Captain's lunge" was the flash-blend.)
        internal const int AmbientBaseR      = 0x140; // float — ambient RED (scene ambient via MGGetAmbient, flash-blended); was BehaviorRangeX
        internal const int AmbientBaseG      = 0x144; // float — ambient GREEN; was BehaviorRangeY
        internal const int AmbientBaseB      = 0x148; // float — ambient BLUE; was BehaviorRangeZ
        internal const int AmbientBaseA      = 0x14C; // float — ambient ALPHA (4th float of the MGGetAmbient vector; ~128; apply uses Opacity 0x120 instead, so inert here)

        // 0x150-0x158: the enemy's STATUS-TINT ambient color (R/G/B). CheckDmg sets it per-frame from the active
        // status condition — Freeze (slot+0x08) → (160,160,160) icy-white; Gooey (+0x14) → (0,37.5,63.8) teal;
        // Poison (+0x0C) / Stamina (+0x10) → their own colors — and flags slot+0x160. PalletSet then applies it to
        // the model's ambient light via MGSetAmbient, overriding the base ambient (0x140-0x148). 0,0,0 = no status =
        // no tint; this is the visual "frozen/poisoned/etc." glow. (The 127.5/80.0/15.0 once seen on Auntie Medu was
        // just an active status tint.)
        internal const int StatusTintR       = 0x150; // float — status-tint ambient RED   (0 = no status); see comment
        internal const int StatusTintG       = 0x154; // float — status-tint ambient GREEN (0 = no status)
        internal const int StatusTintB       = 0x158; // float — status-tint ambient BLUE  (0 = no status)

        // AiCycleParity flips 0↔1 at each AI attack cycle.
        // Confirmed alternating pattern across Pirate's Chariot, Auntie Medu, and Mask of Prajna.
        // Likely used by the AI to select between two alternate behavior states or attack sets each cycle.
        internal const int AiCycleParity     = 0x160; // int   — 0/1 parity bit; toggles each AI attack cycle; never stays fixed during active combat

        internal const int FlashActivation   = 0x164; // int   — CONFIRMED flash trigger: 0=off, 1 or 2=active; see block comment above
        internal const int FlashDecayRate    = 0x168; // float — CONFIRMED; see block comment above; engine negates (write +0.08 → stored −0.08); duration = 1.0/rate frames
        internal const int FlashTimer        = 0x16C; // float — CONFIRMED engine-owned countdown; always reset to 1.0 by engine at flash-start; decrements by FlashDecayRate each frame

        // ── On-Hit Event Fields ────────────────────────────────────────────────
        // 0x170-0x17C are one 4-float HIT-FACING / KNOCKBACK-DIRECTION vector (X,Y,Z,W), snapshotted on impact and
        // used by Step__CMonstorUnit as the knockback launch direction (it sceVu0CopyVectors slot+0x170 = slotbase+
        // 0x1E540 each knockback frame). HitFacingX/Z are exact snapshots of FacingX/FacingZ (confirmed by same-frame
        // poll). HitFacingW is the homogeneous W: 1.0 at rest (a "point"/unset) → 0.0 on hit (a direction has W=0) —
        // it has no standalone meaning, it just rides with X/Y/Z. (CleanViewMonstor zeroes all four on despawn.)
        internal const int HitFacingX        = 0x170; // READ ONLY — float; FacingX snapshot at moment of impact (0.0 at rest)
        internal const int HitFacingY        = 0x174; // READ ONLY — float; Y of the hit-facing vector (≈0 for horizontal facing)
        internal const int HitFacingZ        = 0x178; // READ ONLY — float; FacingZ snapshot at moment of impact (0.0 at rest)
        internal const int HitFacingW        = 0x17C; // READ ONLY — float; W of the hit-facing vector (1.0 at rest = point, 0.0 on hit = direction)
        // Knockback impulse (the active force): set on a hit to (weapon hit-force × the species' record KnockbackMult,
        // record +0x098); each frame Step__CMonstorUnit feeds it into the enemy's velocity (slot+0x80) and subtracts
        // KnockbackDecay (0x184) until it reaches 0. Higher = bigger launch. 0.0 at rest. The per-enemy KnockbackMult
        // copy lives at slot+0x188.
        internal const int KnockbackForce    = 0x180; // float — active knockback impulse, set on hit, decays to 0; see comment
        // Knockback DECAY rate: subtracted from KnockbackForce (0x180) every frame, so HIGHER = the impulse drains
        // faster = SHORTER slide (the old "Strength" name was backwards). RECOMPUTED on each hit in CheckDmg as
        // (weapon decay param × KnockbackMult 0x188), so a one-time floor-load write is overwritten on the next hit.
        // Observed: Pirate's Chariot=0.10 (slides far), Skel.Soldier/Auntie Medu=0.20, Dasher=0.30. (Was KnockbackStrength.)
        internal const int KnockbackDecay    = 0x184; // float — knockback decay rate (higher = shorter slide); see comment
        // Live per-enemy KNOCKBACK multiplier — copy of the species' record KnockbackMult (record +0x098), set at
        // spawn. CheckDmg reads it on EVERY hit to scale both the impulse (KnockbackForce 0x180 = weaponForce × this)
        // and its decay (KnockbackDecay 0x184). It is written only at spawn/despawn (NOT refreshed per-hit), so
        // writing it on a LIVE slot changes that one enemy's knockback on subsequent hits (unlike 0x180/0x184, which
        // the engine rewrites each hit). 1.0 = normal knockback, 0.0 = immovable (bosses/plants), 0.5-0.8 = heavies.
        // Can be written to values above 1.0 for extreme knockback. 5.0 moves enemies several steps back.
        internal const int KnockbackMult     = 0x188; // float — live per-enemy knockback multiplier (settable); see comment
    }

    /// <summary>
    /// Static enemy species table embedded in SCUS_971.11.
    /// Each record is a spawn template; the engine reads fields from here and writes them into a
    /// live enemy slot at spawn time. The static table layout differs from EnemySlotOffsets —
    /// resistances live near the middle of each record as individual ushorts, not as packed ints at
    /// the slot's 0x028. Do not assume field offsets match the slot.
    ///
    /// Confirmed from ELF binary analysis 2026-06-05 (offsets corrected 2026-06-06):
    ///   ELF file offset 0x17FC00, stride 0x9C (156 bytes).
    ///   RAM = 0x00100000 + (0x17FC00 − 0x100) = 0x0027FB00.
    ///   EIDs are NOT stored sequentially — use EnemyDefaults.TableIndex to locate a record.
    ///   The game engine looks up a record via: entry_ptr = TableBase + TableIndex * Stride.
    ///   (TableIndex is the sequential packed index, not the EID. See EnemyDefaults.TableIndex.)
    ///
    ///   Record layout: 0x000–0x04F = model code header (do not write); 0x050–0x09B = data fields.
    ///   Each record stores the EID of its enemy at +0x07C (EnemySpeciesId field).
    ///
    ///   PREVIOUS BUG (now fixed): ElfOffset was erroneously set to 0x17FC54 (0x54 bytes past the
    ///   true record start), causing all field offsets to be 0x54 too low. MaxHp at 0x098 and
    ///   EnemyCode at 0x048 were reading from the NEXT sequential record (one stride ahead), not
    ///   the current one. All offsets below reflect the corrected true-record-start base.
    /// </summary>
    internal static class EnemySpeciesTable
    {
        // ── Table geometry ────────────────────────────────────────────────────
        internal const int ElfOffset  = 0x17FC00;          // byte offset within SCUS_971.11 file (true record-0 start)
        // RAM = seg.vaddr + (ElfOffset - seg.file_offset) = 0x00100000 + (0x17FC00 - 0x100) = 0x0027FB00
        // Confirmed from ELF program header: LOAD seg file=0x100..0x1A2480, vaddr=0x00100000
        internal const int TableBase  = 0x0027FB00;        // confirmed RAM address of record 0
        internal const int Stride     = 0x9C;              // bytes per record (156)

        /// <summary>RAM address of the template record at the given physical table index (from extracted data).</summary>
        internal static int RecordAddress(int physicalIndex) => TableBase + physicalIndex * Stride;

        /// <summary>RAM address of a specific field within the record at the given physical index.</summary>
        internal static int FieldAddress(int physicalIndex, int fieldOffset) => RecordAddress(physicalIndex) + fieldOffset;

        /// <summary>RAM address of a field for an enemy whose TableIndex is known.</summary>
        internal static int FieldAddress(EnemyDefaults e, int fieldOffset) => RecordAddress(e.TableIndex.Value) + fieldOffset;

        // ── Model code header (0x000–0x04F) — do not write ───────────────────
        // The first 80 bytes of each record are a model/asset header. Only the two code
        // fields are meaningful for regular enemies; everything else is zero-padding.
        // Multi-form and boss entries may store additional variant codes in 0x010–0x03F.
        internal const int ModelCode     = 0x000; // char[4] — 4-char ASCII model identifier (e.g. "e52a"); do not write
        internal const int ModelCodeCopy = 0x040; // char[4] — duplicate of ModelCode at 0x040; do not write

        // ── Data fields (0x050–0x09B) ─────────────────────────────────────────
        // These are the fields copied to or referenced by the live enemy slot at spawn time.

        internal const int MaxHp      = 0x050; // int    — max HP; copied to slot MaxHp (0x020) at spawn

        // Elemental resistances — individual ushorts starting at 0x054.
        // Scale (read as signed short): 0=immune, <0=absorbs (heals enemy), 100=neutral, >100=weak.
        // Absorb uses two's-complement encoding: e.g. -50 stored as 0xFFCE=65486u.
        // Only two enemies absorb: IceQueen absorbs ice (-50), eid=70 absorbs fire (-50).
        internal const int Category   = 0x054; // ushort — enemy category (0=dragon,1=undead,2=marine,3=rock,4=plant,5=beast,6=sky,7=metal,8=mimic,9=mage)
        internal const int FireRes    = 0x056; // short  — fire resistance
        internal const int IceRes     = 0x058; // short  — ice resistance
        internal const int ThunderRes = 0x05A; // short  — thunder resistance
        internal const int WindRes    = 0x05C; // short  — wind resistance
        internal const int HolyRes    = 0x05E; // short  — holy resistance

        // +0x060: the enemy's PHYSICAL (movement) collision radius — NOT the combat hitbox. Varies 2.0–45.0 per
        // species; copied to slot EntityScale (0x044) and EntityScaleCopy (0x048) at spawn. Used by MoveCheck/
        // MoveCheck2/MoveChecMonster for navigation/separation and by CheckDmg for the hit-effect radius. The combat
        // hittable/attack collision is a separate, STB-driven system (_SET_BODY_COL/_SET_DMG_COL). See EnemySlotOffsets.EntityScale.
        internal const int EntityScale = 0x060; // float  — physical/movement collision radius (copied to slot at spawn); not the attack hitbox

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // ENEMY → PLAYER DAMAGE (how hard an enemy hits) — RE'd 2026-06-19. The applied damage is computed
        // in BtCheckDamageProc (dun overlay 0x01DBAFD0) as:
        //     damage = baseAttack − playerDefense      (clamped > 0; halved in some block/guard cases)
        //     then applied via AddNowLife(player, −damage)  (ELF 0x1BE710)
        // baseAttack is set PER-ATTACK by the enemy's STB behavior script (via the _SET_DMG_PARA command,
        // ELF 0x1E3FD0) — it is NOT read from any species-table field. So per-dungeon attack strength lives
        // in each species' STB script, and normalizing it would require runtime STB patching (BossScript-
        // Patcher-style), not a field write. DURABILITY (HP + DamageReduction/WeaponDefense) IS field-driven.
        // ════════════════════════════════════════════════════════════════════════════════════════════

        // +0x064/+0x066: the enemy DEFENSE pair, copied into the live slot's DefenseStats (0x090) low/high at
        // spawn. CONFIRMED 2026-06-19 (damage RE) as the real per-dungeon DURABILITY scalers — they grow with
        // dungeon depth (e.g. regular Mimic: DBC 1/10 → WOF 2/10 → SW 5/20 → Moon Sea 8/30).
        // CMonstorUnit::CheckDmg (ELF 0x1D9F10) reads them when the enemy TAKES damage:
        //   DamageReduction (0x064): subtracted from the incoming damage (flat reduction; halved for one damage
        //     type). Higher = enemy takes less damage.
        //   WeaponDefense   (0x066): passed as the int arg to SwordDmgCheck1 (the player-weapon damage check);
        //     scales weapon damage taken. (Earlier "max shoot range" guess was wrong.)
        internal const int DamageReduction = 0x064; // ushort — flat damage-taken reduction (slot DefenseStats low)
        internal const int WeaponDefense   = 0x066; // ushort — weapon-damage defense, fed to SwordDmgCheck1 (slot DefenseStats high)

        // +0x068/+0x06A: BST shot-effect indices — confirmed 2026-06-20 (projectile-damage RE). Each is the
        // 0-based index into BehaviorScriptTable.PointerArray (0x27FA70) that selects which BST entry governs
        // this species' primary/secondary shot. 0xFFFF = no shot effect. For "default" shooters (STB _SET_SHOT
        // with argc!=6), the engine reads the selected entry's +0x3C base damage. Observed values: 0,2,3,11
        // (primary) and 0xFFFF (secondary, i.e. no secondary shot). Set per species in CMonstorUnit::SetupBaseModel.
        internal const int PrimaryBstIndex   = 0x068; // ushort — primary BST shot-effect index into PointerArray; 0xFFFF = none
        internal const int SecondaryBstIndex = 0x06A; // ushort — secondary BST shot-effect index; 0xFFFF = none (observed for all species so far)

        internal const int Abs        = 0x06C; // int    — XP rewarded to the player on kill; written to slot Abs (0x0B0) at spawn
        internal const int MinGoldDrop= 0x070; // int    — minimum gold dropped on death; written to slot MinGoldDrop (0x034) at spawn
        internal const int DropChance = 0x074; // int    — item drop chance (0–100); written to slot DropChance (0x038) at spawn
        // +0x078: the species' MONSTER TYPE (halfword), copied to the unit at spawn (SetupViewMonstor → unit +0x1E410):
        //   0 = regular   2 = boss (and boss companions)   3 = mimic   4 = king mimic   (1 = nothing shipped; see below)
        // Every reader (docs/enemy-monster-type-field.md):
        //   ArrangementPos (placement): 0 and 3 may fill many slots; ANY other value spawns at most once per floor (the loop
        //     retries, the floor total is unchanged) — so 1 is a "once per floor" with no other effect.
        //   CheckViewLevel: type 2 is never proximity-activated (bosses are script-started).
        //   SoundCheck: type 2 is audible from 350/1000 instead of 50/500 units.
        //   CheckDmg: type 2 is immune to the Critical ability (docs/game-formulas.md).
        //   Step (death): type 2 skips the rare-drop roll.
        //   setTargetCursor / DrawTargetLife: type 2 hides the HP gauge and aims the lock-on cursor differently.
        // The randomizer's one-of-each floors write 1; the injector's spawn-once writes 1 too (2 would make the enemy a boss).
        internal const int MonsterType = 0x078; // halfword (int-sized slot) — see above

        // +0x07C: the enemy's ID (EnemyDefaults.Id; boss companions carry 0), copied to the unit at +0x1E412: the lock-on
        // cursor shows the HP gauge and name only for id > 0, and Steve's monster chatter (weapons 303/312) picks message
        // 4000 + id × 10 from it.
        internal const int EnemySpeciesId    = 0x07C; // ushort — enemy species ID stored in table (matches EnemyDefaults.Id)
        // +0x07E: 2 bytes padding (always 0)

        internal const int StealItemId= 0x080; // ushort — item ID for steal mechanic; 65535 if none
        // ★ The DEATH-DROP gate (RE'd): copied to the slot's StealItemId high word (0xDA) at spawn, which gates the
        // whole drop block (ELF 0x1DF4C0). 1 = enemy drops on death, 0 = never drops. It is independent of the steal
        // item at 0x080 (Cave Bat has steal item 151 but this flag 0). 0 for flyers + Gol/Sil → those never drop;
        // setting it to 1 (see Enemies.EnableEnemyDrops) permanently enables their drops via the static species table.
        internal const int DeathDropFlag  = 0x082; // ushort — death-drop enable (1=drops, 0=no drop)

        // +0x084 ItemDamageRes: the enemy's DAMAGE-TAKEN multiplier for thrown items (gems/bombs/etc.) — scale 100 =
        // neutral, <100 = resistant (same scale as the elemental resistances). Read in CheckDmg (@0x1dc170, on the
        // non-elemental / s3==-1 damage path). CONFIRMED in-game on Minotaur Joe: ItemDamageRes 50->90
        // raised a thrown fire-gem's damage 14->26 (≈ the 90/50 ratio), and 0 made it deal 0 (fully immune). Bosses
        // ~30-50 (item-resistant), regulars ~90-100. (Whether it also scales non-item damage on the same path is untested.)
        internal const int ItemDamageRes   = 0x084; // ushort — thrown-item damage-taken multiplier (×/100, 100=neutral); see comment
        // +0x086 ItemStatusRes: the enemy's STATUS-EFFECT susceptibility (0 = immune, ~100 = fully susceptible) — NOT a
        // damage resistance. In CheckDmg, for each player weapon/item status-attribute bit (0x20/0x40/0x100/0x200/
        // 0x800/0x1000; 0x80 = steal) the engine rolls rand vs ItemStatusRes and, if it passes, sets the matching status
        // timer on the slot (+0x08 Freeze / +0x0C Poison / +0x10 Stamina / +0x14 Gooey). 0 = the roll never passes =
        // immune. All bosses ship 0 (status-immune); regulars are 50-90. CONFIRMED in-game: raising
        // Minotaur Joe's ItemStatusRes 0->90 made him poison-able, and the landed poison then ticked for real damage
        // (~12/tick at his normal HP).
        internal const int ItemStatusRes   = 0x086; // ushort — status-effect susceptibility (0 = immune); see comment


        //
        // +0x088: RARE DROP ITEM ID — the enemy's signature bonus drop. RE'd 2026-06-24 by full-ELF dataflow:
        // SetupViewMonstor (ELF 0x1E02B0) copies this to live enemy slot +0xE0 at spawn;
        // CMonstorUnit::Step's death-drop block (gated at ELF 0x1DF4C0) is the ONLY reader —
        // with a rand() < ~10% roll it seeds ForceItemDrop (slot +0xA0) = this id, then
        // SetGateKey-Stack (ELF 0x1B5680, a 32-entry de-dupe set so a unique item drops only once) + CRandomItem::Set
        // (ELF 0x1D71F0) spawn it; if unset the enemy does its normal random drop (SelectAttachi).
        // King Mimic = 181 = "Treasure Chest Key", regular Mimic = 235 = "Dran's Feather" (ItemNameTbl).
        // Bosses = 65535 (= -1 signed) = "no signature drop" — that −1 is why the engine skips the block.
        internal const int RareDropItemId = 0x088; // ushort — signature/rare drop item ID; 65535(-1)=none (bosses); see block comment

        // +0x08A–+0x094: SIX per-element attack-output multipliers (ushort each; ×value/100, 100 = neutral), in
        // element order 0=Fire, 1=Ice, 2=Thunder, 3=Wind, 4=Holy, 5=NONE/PHYSICAL. DORMANT: the engine reads them
        // but they have no gameplay effect, because no enemy attack ever carries an element. Mechanics:
        //   • Spawn: SetupViewMonstor copies all six into the per-slot body-collision struct at MMU+slot*0x510+0x555D0
        //     (a [16 body-part][6 element] table).
        //   • CMonstorUnit::CheckDmg (0x1D9F10 @0x1dc08c) does dmg = dmg/100 * EA[s3] on the enemy→player path
        //     (→ AddNowLife), where s3 = the hit's element, gated `beq s3,-1 -> skip`. (No read on the player→enemy
        //     path, so this is NOT a damage-taken resistance.)
        //   • s3 = -1 for every enemy attack: melee hits register via CCollisionData::Set (0x1b57a0) which hardcodes
        //     the collision element +0x58 = -1; enemy projectiles are CSHOT_EFFECT, created in CMonstorUnit::Step
        //     (@0x1def48) via Set__CSHOT_EFFECT with element arg = -1 (Initialize @0x1ae470 also defaults -1). Only
        //     the player's special attacks call SetAttribute__CSHOT_EFFECT (@0x1ae350) to give a shot a real element.
        // So EA[s3] is never reached. Per-species values vary (some non-100 spikes) but are inert. Bosses = all-0.
        // To use these, an enemy attack would need a real element (the Set__CSHOT_EFFECT element arg, or SetAttribute).
        // Element enum from CWeaponElement::Set (@0x1b7840) + the resistance-field order (0x056..0x05E);
        // GetWeaponElementAttr (@0x1b69f0) maps element id 5 → no element bit.
        internal const int ElemAtkFire    = 0x08A; // ushort — elemental attack-output multiplier (×/100, 100=neutral); see block
        internal const int ElemAtkIce     = 0x08C; // ushort — elemental attack-output multiplier
        internal const int ElemAtkThunder = 0x08E; // ushort — elemental attack-output multiplier (common signature spike: 120–150)
        internal const int ElemAtkWind    = 0x090; // ushort — elemental attack-output multiplier
        internal const int ElemAtkHoly    = 0x092; // ushort — elemental attack-output multiplier
        internal const int ElemAtkPhysical = 0x094; // ushort — NON-ELEMENTAL (physical) attack multiplier; element id 5 = "none" (NOT dark — see block)

        // +0x098: per-enemy KNOCKBACK-force multiplier (float; 1.0 for all stock enemies = neutral). On a hit,
        // CheckDmg computes knockback = weaponHitForce (0x90 of the hit data) * KnockbackMult and writes it to
        // slot+0x180; Step__CMonstorUnit then drives the enemy's velocity from slot+0x180 each frame and decays it
        // (slot+0x180 -= slot+0x184) to 0. So lower = knockback-resistant, 0 = immovable on hit, higher = flies
        // further. (Copied to slot+0x188 at spawn.) CONFIRMED in-game: Skeleton Soldier at 5.0 was
        // knocked back noticeably further per hit (scales the impulse, which then decays, so distance grows
        // sub-linearly). It is a DELIBERATE per-species knockback-resistance stat (read from the ELF table, all 167
        // records): 0.0 = immovable (every boss + their effect entities, and rooted plants like Cannibal Plant/
        // Cursed Rose/King Prickly), 0.5 = heavies (Golem, Titan, Dragons, Pirate's Chariot, Gol/Sil), 0.6-0.8 =
        // stone/rock (Statue, Statue Dog, Rockanoff), 1.0 = normal enemies (119 of 167). (NOT EntityScale; that's 0x060.)
        internal const int KnockbackMult  = 0x098; // float — per-enemy knockback-force multiplier (1.0 = neutral); see comment
    }

    /// <summary>
    /// Offsets into the model scale table (base 0x21E18530, stride 0x3510 per slot).
    /// This table is separate from the enemy slot array and holds rendering and bounding data.
    /// All values confirmed from full-slot dumps (DBC fl.12 and Shipwreck).
    ///
    /// This 0x3510 region is the per-slot CCharacter "render object": for slot i it lives at
    /// (MonstorUnit 0x21DF87D0 + i*0x3510 + 0x1FCD0), which lands at ModelBase + slot*stride for the
    /// scale fields. It sits directly after the 16 FloorSlots (16 × 0x190 = 0x1900). DrawMonstor reads
    /// THIS object every frame (RenderStatus==2 gate, then TextureAnime__CCharacter) — the rendered
    /// model/animation is driven entirely from here, NOT from any EnemySlotOffsets field. The mesh /
    /// CCharacter pointers in this block are baked once by SetupViewMonstor at spawn and are not
    /// rewritten per frame, so a one-time copy of this block from a slot of another (already-loaded)
    /// species visually re-skins an enemy without any code execution.
    /// </summary>
    internal static class ModelScaleOffsets
    {
        internal const int ModelBase   = 0x21E18530;
        internal const int ModelStride = 0x3510;

        // The model/render block sits INSIDE the MainMonstorUnit object, so the same field has two addressings:
        // ModelBase + slot*ModelStride + off, or MainMonstorUnit.Base + slot*ModelStride + (ModelFromUnit + off).
        // Both appear in the wild — derive the second from the first so there is ONE definition per field. (These
        // were previously duplicated as separate literals, which is two sources of truth for one offset.)
        internal const int ModelFromUnit = ModelBase - (int)EnemyAddresses.MainMonstorUnit.Base;   // 0x1FD60

        // +0x000/+0x004/+0x008: render scale multipliers (width/height/depth).
        // All 1.0 at spawn for regular enemies. MiniBoss.cs writes custom values here to
        // visually resize boss enemies. The game engine reads these for rendering.
        internal const int ScaleX = 0x000;
        internal const int ScaleY = 0x004;
        internal const int ScaleZ = 0x008;

        // +0x010: 0x002A12B0 — shared pointer, identical across all 16 slots and both
        // dungeons tested. Likely a global model resource table pointer or vtable entry.

        // ── "BODY SIZE" triple (+0x020/+0x024/+0x028) — RE'd 2026-06-20 from SCUS_971.11 ──────────────
        // These three floats are the enemy's BODY-COLLISION/SIZE descriptor. SOURCE (confirmed): the text line
        // `BODY_SIZE <height>,<width>,<depth>` in the model's info.cfg, embedded in dun/monstor/<code>.chr in
        // data.dat (same config block as the KEY motion list). The .chr loader dispatches it to CommandBODY_SIZE
        // (ELF 0x13a8d0), which stores the three args onto the live CCharacter at +0xB0/+0xB4/+0xB8 (= ModelBase
        // +0x20/+0x24/+0x28, since ModelBase = CCharacter+0x90). Arg→field: arg0(height)→+0xB4, arg1(width)→+0xB0,
        // arg2(depth)→+0xB8. (The matching STB command is the per-body-part _SET_BODY_COL family, 0x1e39f0.)
        // Fixed per species; populated in EnemyData.cs by tools/extract_bodysize.py (validated vs 41 live dumps).
        // Read LIVE (not cached at spawn — a one-shot write to height took effect immediately in-game). The genuine
        // CCharacter-pointer readers (confirmed by disassembly, after discarding offset-collision false positives —
        // CHitMark/CEffect/ClsMes/CFrame all have their OWN +0xB0/B4/B8): GetScrPosFromChar (height), CCharacter::
        // PickUpPoly (width+height), and the _GET_NPC_BODY_SIZE script getter (all three). See each field below.

        // +0x020 (CCharacter+0xB0): body WIDTH / girth radius (7.0 small → 14.0 Gunny → 32.0 Mask of Prajna). BODY_SIZE
        // arg1. Read by CCharacter::PickUpPoly (ELF 0x156710) as the floor-poly pickup RADIUS, clamped to a min of 2.0
        // — i.e. TERRAIN/floor collision; and by _GET_NPC_BODY_SIZE. NO OBSERVABLE in-game effect: a live x200 write
        // produced no visible change on a flyer (Cave Bat) OR a ground enemy (Skeleton Soldier). Not used by the
        // lock-on reticle (that's the slot's ReticleWidth/Height / EntityScale).
        internal const int BodyWidth = 0x020;

        // +0x024 (CCharacter+0xB4): body HEIGHT. BODY_SIZE arg0. CONFIRMED IN-GAME (x5 raised the un-locked marker):
        // GetScrPosFromChar (ELF 0x14c980) adds const×(+0xB4) to world Y before 2D projection — anchors the name /
        // off-lock target marker / talk bubble at the top of the body. Also read by PickUpPoly and _GET_NPC_BODY_SIZE.
        internal const int BodyHeight = 0x024;

        // +0x028 (CCharacter+0xB8): body DEPTH. 60.0 for ground enemies, 0.0 for ranged/flying (Gunny, Sam, Prajna).
        // BODY_SIZE arg2. Read ONLY by the _GET_NPC_BODY_SIZE script getter — effectively inert in gameplay (a live
        // x5 write produced no observable change, consistent with no draw/collision/marker consumer).
        internal const int BodyDepth = 0x028;

        // +0x048: enemy-specific int (474–1454); likely total keyframe count or mesh triangle count.
        internal const int DataSize = 0x048;

        // Constants observed at the same value across all enemies in both DBC and Shipwreck:
        // +0x230: int=320
        // +0x260/+0x2E0: float=10.0 (repeated twice; matches resting hitbox X from enemy slot)
        // +0x268/+0x2E8: float=0.1  (repeated twice)
        // +0xC6C: float=0.7
        // +0xC80–+0xC8C: float=128.0 x4 (matches resting hitbox W; likely attack hitbox template)
        // +0xC90: float=10.0  |  +0xC94: float=70.0
        // +0xCB0–+0xCBC: float=128.0 x4 (second identical block — possibly second attack state)
        // +0xCC0: float=10.0  |  +0xCC4: float=70.0

        // +0x370: animation clip cap (7–23 per enemy species); capping below true value freezes higher-index animations (attacks); Setting above true count does not change default behavior.
        internal const int AnimCount = 0x370;

        // ── Motion player (RE'd for the boss-death system; see EnemyModelInjector.BossScriptPatcher) ──
        // The render object embeds a "motion player" sub-struct. The RE references it from the motion-block base
        // (MonstorUnit + slot*0x3510 + 0x1FCD0) with offsets +0xc60 speed / +0xc64 flags / +0xc68 motion-id /
        // +0xc70 state, and the PLAYING frame at +0x2F0 (= absolute MonstorUnit+slot*0x3510+0x1FFC0). The consts
        // below are ModelBase-relative (ModelBase = that motion-block base + 0x90), i.e. subtract 0x90 from the
        // RE offsets, so they work with the usual ModelBase + slot*ModelStride + field addressing.
        internal const int PlayingMotionSpeed = 0xBD0; // float — playback speed for the current clip (−1.0 = use the motion's KEY speed). RE +0xc60.
        internal const int PlayingMotionFlags = 0xBD4; // int   — the clip's play flags (_SET_MOTION writes 0; Step's commit writes 2 = play once). RE +0xc64.
        internal const int MotionTablePtr     = 0x2B4; // native ptr — the model's motion (KEY) table: 0x10 per motion, KEY speed float @+8 (what _SET_MOTION reads for −1.0). RE unit+0x20014.
        internal const int MotionTableStride  = 0x10;
        internal const int MotionTableStart   = 0x00;  // int   — the clip's first frame …
        internal const int MotionTableEnd     = 0x04;  // int   — … and its last: a clip's true length, per species, live
        internal const int MotionTableSpeed   = 0x08;  // float — frames advanced per engine frame (its KEY rate)
        internal const int PartStride         = 0x11B0; // an extra render part of a multi-part enemy sits this far past the body's block
        internal const int PlayingMotionId    = 0xBD8; // int   — currently-PLAYING motion id (read by _STATUS_GET_MOTION_ID). RE +0xc68.
        internal const int PlayingMotionIdFromUnit    = ModelFromUnit + PlayingMotionId;    // 0x20938 — same field, unit-relative
        // PLAYING motion FRAME (float). Same field _SET_MOTION_FRM (ELF 0x1e1cb0) writes and _GET_MOTION_FRM reads.
        // NOTE: the motion-table KEY "speed" is NOT the frame-advance rate, so to retime a clip drive THIS directly.
        internal const int PlayingMotionFrame = 0x260; // float — RE +0x2F0.
        internal const int PlayingMotionFrameFromUnit = ModelFromUnit + PlayingMotionFrame; // 0x1FFC0 — same field, unit-relative
    }

    /// <summary>
    /// Addresses and field offsets for the behavior script table.
    /// Each 0x70-byte record describes a special attack or movement behavior pattern.
    /// 34 entries total; a parallel pointer array at <see cref="PointerArray"/> references
    /// entries [25]–[33] (the nine boss-specific scripts used by the pointer-dispatched AI).
    /// ELF base: 0x17EC90 → RAM = 0x00100000 + (0x17EC90 - 0x100) = 0x0027EB90.
    /// </summary>
    internal static class BehaviorScriptTable
    {
        internal const int ElfOffset    = 0x17EC90;
        internal const int Base         = 0x0027EB90; // confirmed RAM address
        internal const int Stride       = 0x70;       // bytes per record (112)
        internal const int Count        = 34;
        // Full 34-entry pointer array — one RAM pointer per BST entry in index order [0]–[33].
        // Located in the gap between BST end (ELF 0x17FB70) and species table (ELF 0x17FC00).
        // PREVIOUS (WRONG): 0x0027FBD4 — that lands inside the species table.
        // Corrected (ELF 0x17FB70 → RAM 0x00100000 + (0x17FB70 - 0x100)): 0x0027FA70.
        // Use: ptr = ReadInt(PointerArray + index * 4); then write Enabled=0 at ptr + 0x14.
        internal const int PointerArray = 0x0027FA70;

        internal static int RecordAddress(int index) => Base + index * Stride;
        internal static int FieldAddress(int index, int fieldOffset) => RecordAddress(index) + fieldOffset;

        // +0x00: 16-byte null-terminated ASCII name embedded in the ELF — do not write.
        internal const int Name               = 0x00;
        // +0x10: 0 = attack/hitbox behavior; 2 = movement/repositioning (kamai / terepo)
        internal const int BehaviorMode       = 0x10;
        // +0x14: always 1 (enabled flag)
        internal const int Enabled            = 0x14;
        internal const int HitboxWidth        = 0x18; // float
        internal const int HitboxHeight       = 0x1C; // float
        internal const int HitboxDepth        = 0x20; // float; 0 for most entries
        // +0x24: always 0.0f
        internal const int TriggerRange       = 0x28; // float — distance at which behavior activates
        internal const int ReachRange         = 0x2C; // float — max effective attack reach; 0 for movement behaviors
        internal const int SecondaryRange     = 0x30; // float — secondary hitbox / follow-through range; 0 for movement behaviors
        // +0x34: always 0
        internal const int DurationFrames     = 0x38; // int   — frames this behavior stays active
        // +0x3C CONFIRMED 2026-06-20 (projectile-damage RE): this is the SHOT BASE DAMAGE, not "attack distance".
        // CSHOT_EFFECT::Set (ELF 0x1ADD60) reads [BT_SHOT_EFFECT+0x3C] (a BST entry here) and writes it as the new
        // shot's damage (CSHOT record +0xA010). For "default" shooters whose STB _SET_SHOT passes no 5th-arg
        // damage (argc!=6: Sam, Crescent Baron, Heart, Thursday, Golem-shot2), THIS is the damage dealt — verified
        // Sam=58, Crescent Baron=70 against in-game measurement. For explicit shooters the STB value (via SetDmg)
        // overrides it. The shot's BST entry is selected by the species record's +0x68/+0x6A shot-effect indices
        // through PointerArray (0x27FA70), in CMonstorUnit::SetupBaseModel.
        internal const int ShotBaseDamage     = 0x3C; // int   — base damage of a shot behavior (was mislabeled AttackDistance)
        // +0x40 CONFIRMED 2026-07-07 (guard/knockback RE): the projectile's ATTACK STATUS FLAGS. When a shot hits,
        // Step__CSHOT_EFFECT (0x1AC180) copies this word into the CCollisionData entry +0x50, and BtCheckDamageProc
        // (dun 0x1DBAFD0) rolls each set status bit (~65% for amulet-gated ailments, 20% for Steal). Only the bits
        // in AttackStatusFlag below are ailments; low bits (0x1..0x80) are non-status shot flags (element/VFX).
        // MELEE attacks carry the SAME flag word via _SET_DMG_PARA's 2nd argument (param array +0x5A590 →
        // CMonstorUnit::CheckDmg passes it as Set__CCollisionData param_9 → entry +0x50) — e.g. the Days-of-week
        // imps Steal, Mummy/Curse Dancer Curse, King Mimics HalfHP, Ice Arrow guaranteed-Freeze.
        internal const int AttackStatusFlags  = 0x40; // int   — projectile status-effect bit flags (see AttackStatusFlag)
        // +0x44 CONFIRMED 2026-07-07 (guard/knockback RE): the HIT REACTION TYPE (2/3/4), NOT a phase count.
        // Step__CSHOT_EFFECT passes it as the CCollisionData entry +0x4C reaction type (and special-cases ==3 to set
        // the launch vector). BtCheckDamageProc keys guard-break + knockdown off it — see AttackReaction below and
        // docs/enemy-attack-damage-table.md. For melee the same reaction type is the _SET_DMG_PARA 3rd STB arg.
        internal const int HitReactionType    = 0x44; // int   — hit reaction type: 2=knockback,3=knockdown(guard-break),4=light
        // +0x48: always 1
        internal const int ScriptMode         = 0x4C; // int   — packed mode flags for the behavior FSM
        internal const int PackedFlags2       = 0x50; // int   — secondary packed flags
        // +0x54: always -1 (0xFFFFFFFF)
        internal const int ProjectileSpeed    = 0x58; // float — non-zero only for projectile behaviors
        internal const int ProjectileLifetime = 0x5C; // int   — frames the projectile lives; 0 if no projectile
        // +0x60–+0x6C: four ints, all -1

        /// <summary>Hit reaction type values (CCollisionData entry +0x4C; BST +0x44 for shots,
        /// _SET_DMG_PARA arg2 for melee). Governs guardability AND knockdown together, resolved in
        /// BtCheckDamageProc. See docs/enemy-attack-damage-table.md.</summary>
        internal static class AttackReaction
        {
            internal const int Knockback = 2; // guardable; unguarded = medium stagger (~80f)
            internal const int Knockdown = 3; // UNGUARDABLE (breaks guard) + hard knockdown/launch (~160f); reads a 4th launch arg
            internal const int Light     = 4; // guardable; unguarded = minimal flinch (~8f), no knockover
        }

        /// <summary>Attack status-effect bits, shared by melee (_SET_DMG_PARA 2nd arg) and shots (BST +0x40 =
        /// AttackStatusFlags); both land in CCollisionData entry +0x50 and are resolved by BtCheckDamageProc.
        /// Amulet-gated ailments roll at ~65% and are blocked by the matching Anti-* amulet. Low bits
        /// (0x1..0x80) are non-status shot/element flags and are not included here. Full per-attack map in
        /// docs/enemy-attack-damage-table.md.</summary>
        internal static class AttackStatusFlag
        {
            internal const int Freeze           = 0x100;    // ~65% roll; blocked by Anti-Freeze Amulet (item 0x84)
            internal const int Poison           = 0x200;    // ~65% roll; blocked by Antidote Amulet (item 0x87)
            internal const int Curse            = 0x400;    // ~65% roll; blocked by Anti-Curse Amulet (item 0x85)
            internal const int Goo              = 0x800;    // ~65% roll; blocked by Anti-Goo Amulet (item 0x86)
            internal const int Stamina          = 0x1000;   // ~65% roll; drains stamina (no amulet)
            internal const int Steal            = 0x40000;  // 20% roll; steals 1/5 of the player's gold, dropped as loot (Days-of-week imps)
            internal const int HalfHpDamage     = 0x80000;  // damage = HALF the player's CURRENT HP, replacing the dmg formula (King Mimic bite)
            internal const int FreezeGuaranteed = 0x100000; // freeze applied unconditionally — no roll, no amulet check (Ice Arrow)
            internal const int AilmentMask      = Freeze | Poison | Curse | Goo | Stamina | Steal | HalfHpDamage | FreezeGuaranteed;
        }
    }

    /// <summary>
    /// Enemy PLACEMENT — the vanilla globals CMonstorUnit::ArrangementPos reads when populating a floor.
    ///
    /// (Was "InjectorAddresses", named for the EnemyModelInjector — which does not use any of it. Its actual
    /// readers are the boss-script patcher and the spawn roster, so the feature name was pure misdirection.)
    /// </summary>
    internal static class EnemyPlacement
    {
        // Per-floor enemy-count target globals read by ArrangementPos — the placement loop runs this many times
        // (capped by walkable spawn tiles). Native 0x01D564xx; these three are the ones used as the count arg.
        internal static readonly long[] PopulationTargets = [0x21D56494, 0x21D5649C, 0x21D564A0];

        // Engine spawn-candidate table, indexed by ArrangementPos at MainMonstorUnit + SpawnCandidateTableOff
        // (stride SpawnCandidateStride; flag @+0, eid @+4).
        internal const long SpawnCandidateTableOff = 0x1DEA8;
        internal const int  SpawnCandidateStride   = 0x9C;
    }
}
