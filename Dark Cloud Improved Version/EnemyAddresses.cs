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
            internal const int  LiveCount = 0x4C;     // int — number of live enemies on the floor; decrement when freeing a slot
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
    }

    /// <summary>
    /// STB behavior-script file format and VM constants. RE'd 2026-06-19 via exe__10CRunScript @0x23E080.
    ///
    /// Header (all 32-bit little-endian):
    ///   +0x00  magic "STB\0" (0x00425453) — validate before patching
    ///   +0x04  total code-section size in bytes
    ///   +0x08  byte offset of the code section from the header base (read with <see cref="CodeSectionOff"/>)
    ///   +0x0C  byte offset of the label table (8-byte entries: labelId@+0, codeOff@+4)
    ///   +0x10  number of label-table entries
    ///
    /// Every instruction is a fixed-size 12-byte record (vmcode_t): op@+0, operandA@+4, operandB@+8.
    /// Dispatch table at 0x29FB80 (31 ops). See <see cref="Op*"/> constants for the opcodes used in
    /// enemy scripts. External commands (op21) are dispatched via a per-program funcdata table; the
    /// funcId is pushed as an int literal immediately before the op21 instruction. See <see cref="Fn*"/>.
    /// </summary>
    internal static class StbVm
    {
        // ── Header ───────────────────────────────────────────────────────────
        internal const int Magic          = 0x00425453; // "STB\0" — first word; validate before patching
        internal const int CodeSectionOff = 0x08;       // header field: byte offset of the code section
        internal const int LabelTableOff  = 0x0C;       // header field: byte offset of the label table
        internal const int LabelCount     = 0x10;       // header field: number of label-table entries

        // ── Instruction layout ────────────────────────────────────────────
        internal const int InstrSize = 0x0C;    // 12 bytes per vmcode_t
        internal const int OperandA  = 0x04;    // byte offset of operandA within a vmcode_t (type for op3; variable index for op1)
        internal const int OperandB  = 0x08;    // byte offset of operandB within a vmcode_t (value for op3 int-literal; scope for op1; callee offset for call_func)

        // ── Opcodes (op field) ────────────────────────────────────────────
        internal const int OpPush1       = 1;  // push-VARIABLE: operandA = variable index, operandB = scope (ScopeLocal = arg/local)
        internal const int OpPush2       = 2;  // push-VARIABLE: float variant (same layout)
        internal const int OpPush3       = 3;  // push-LITERAL:  operandA = type (TypeInt/Float/String), operandB = value
        internal const int OpCallFunc    = 19; // call_func: jump to sub-program; operandB = code offset of callee
        internal const int OpCallFuncCond = 27; // call_func conditional variant (same layout)
        internal const int OpExt         = 21; // external-command call; operandB must be 0; first pushed arg = funcId

        // ── Operand type/scope qualifiers ─────────────────────────────────
        internal const int TypeInt    = 1; // operandA of OpPush3: int32 literal (operandB = the value)
        internal const int TypeFloat  = 2; // operandA of OpPush3: float literal (operandB = IEEE-754 bits)
        internal const int ScopeLocal = 1; // operandB of OpPush1: scope 1 = local / call argument (as opposed to global)

        // ── External command function IDs (as pushed in the STB as funcId to OpExt) ─────────────
        // These are the per-program funcIds observed in monster STBs, one step before the global
        // dispatch table (0x2917C8). _SET_SHOT and _SET_SHOT2 entries: argc==6 → explicit damage arg
        // (5th pushed value); argc!=6 → "default" shot (damage comes from BehaviorScriptTable +0x3C).
        internal const int FnSetShot      = 133; // _SET_SHOT   with explicit damage (argc==6)
        internal const int FnSetShotReg   = 135; // _SET_SHOT   registry entry (not observed in any monster STB)
        internal const int FnSetShot2     = 229; // _SET_SHOT2 / second-shot with explicit damage (argc==6)
    }

    /// <summary>
    /// CRunScript — the per-enemy-slot STB-VM state, a sub-array inside CMainMonstorUnit at
    /// Base + 0x54DD0 (PCSX2 0x21E4D5A0), stride 0x48. One entry per FloorSlots/CCharacter slot index.
    /// (Listed in EnemyModelInjector._slotBlocks as the third per-slot block.)
    ///
    /// ★ RUNTIME STB LOCATION (confirmed 2026-06-19, live RE): <see cref="StbPtr"/> (+0x3C) holds the NATIVE
    /// base address of the exact .stb behaviour script this slot's VM is executing — i.e. the way to find any
    /// enemy's loaded STB in RAM with no scan and no roster math. This supersedes BossScriptPatcher's
    /// KnownAddrs table + signature scan (which can also locate a STALE/duplicate copy — e.g. Minotaur Joe
    /// loads twice, at 0x011A8990 and 0x01698CC0; CRunScript+0x3C names the live one, 0x011A8990).
    /// Validate a read pointer by checking the STB magic 0x00425453 at +0x00 and the label-1 codeOffset at
    /// +0x54. Usage: stbBase = Memory.ReadInt(CRunScript.StbPtrAddr(slot)); patch at (stbBase | 0x20000000).
    ///
    /// Other observed fields: +0x2C = current instruction pointer (into the running script),
    /// +0x40 = code base (script base + label-1 code offset). See memory enemy-stat-normalization / stb-vm-cracked.
    /// </summary>
    internal static class CRunScript
    {
        internal const long Base   = EnemyAddresses.MainMonstorUnit.Base + 0x54DD0; // 0x21E4D5A0
        internal const int  Stride = 0x48;
        internal const int  CurIp  = 0x2C; // current instruction pointer (native)
        internal const int  StbPtr = 0x3C; // ★ native base of the STB this slot executes
        internal const int  CodeBase = 0x40; // script base + codeOffset

        internal static long SlotAddr(int slot, int fieldOffset) => Base + (long)slot * Stride + fieldOffset;
        /// <summary>EE address of the STB-base pointer field for <paramref name="slot"/>.</summary>
        internal static long StbPtrAddr(int slot) => SlotAddr(slot, StbPtr);
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
        internal const int EntryCount   = 32;      // body-part slots per enemy (covers any observed enemy)
        internal const int ReadLength   = EntryCount * EntryStride; // 0x80 — safe read window for the full array

        /// <summary>EE base address of <paramref name="slot"/>'s per-body-part melee-damage array.</summary>
        internal static long ArrayAddr(int slot) => EnemyAddresses.MainMonstorUnit.Base + (long)slot * SlotStride + OffsetInUnit;
    }

    /// <summary>
    /// The enemy DAMAGE HITBOX — the per-bone collision spheres your weapon must reach to hurt the enemy
    /// (RE'd 2026-06-20 + confirmed in-game). NOT EntityScale and NOT the BODY_SIZE triple; this is its own system.
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
        internal const int  SlotStride     = 0x510;
        internal const int  BodyPartStride = 4;
        internal const int  MaxBodyParts   = 16;     // each sub-array spans 0x40 (= 16 floats) before the next one

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
    /// ★ WHY A PER-SLOT SPEED-UP IS NOT FEASIBLE (RE'd 2026-06-22; a miniboss "move faster" attempt was dropped here).
    /// <c>CMonstorUnit::Step</c> (ELF 0x1DE540) integrates motion as a plain <c>pos += dir * speed</c> per axis
    /// (0x1DE60C–0x1DE674: lwc1 dir@-0x1BD0/-0x1BCC/-0x1BC8 × speed@-0x1BB0, add to pos) — there is NO third per-slot
    /// scale factor to set once. Both dir and speed are REWRITTEN every frame by _SET_MOVE / _CHK_MOVE while an enemy
    /// steers or chases, straight from the species' SHARED STB literal, so a per-tick PINE write to this field always
    /// loses the race (verified: even ×5 produced no visible change). The only per-slot speed modifier in _SET_MOVE is
    /// a boolean "halve" flag (int @ slot 0x1E3E4, store at−0x1C1C: if >0, speed ×0.5) — it can only SLOW, never speed
    /// up. And same-species slots share one STB native pointer, so there is no per-slot script to patch. ⇒ The only
    /// race-free speed lever is the shared per-species _SET_MOVE STB literal (what HarderEnemies/"Faster enemies"
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
    /// Struct fields (RE'd 2026-06-19): +0x00/+0x04/+0x08 = three floats (shot params), +0x0C = 1.0f, +0x20 =
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
    /// STB VM reference (RE'd 2026-06-19, exe__10CRunScript @0x23E080): vmcode_t = 12-byte {op@+0, operandA@+4,
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
        internal const int RenderStatus      = 0x000; // int   — 0=inactive, 1=spawned (not yet aggro'd), 2=active; transitions 1→2 when enemy enters play
        internal const int FreezeTimer       = 0x008; // int   — freeze status countdown; 0 at rest
        internal const int PoisonPeriod      = 0x00C; // int   — poison tick interval; 0 at rest
        internal const int StaminaTimer      = 0x010; // int   — stamina/status countdown; starts at a large value (e.g. 0x004F0000 ≈ 5.2M) and decrements each frame; 0 when expired
        internal const int GooeyState        = 0x014; // int   — gooey/slime status; 0 at rest
        internal const int DistanceToPlayer  = 0x018; // float — live distance to player in world units; updated each frame; used as proximity filter

        // ── HP / Stats ───────────────────────────────────────────────────────
        internal const int MaxHp             = 0x020; // int   — maximum HP; set from species data at spawn
        internal const int Hp                = 0x024; // int   — current HP; decrements on hit; enemy dies when ≤0
        // Each resistance pack holds two ushorts: (low ushort, high ushort)
        internal const int ResistancePack1   = 0x028; // [Category, FireRes]      — Category = enemy category index; fire confirmed; scale: 100=neutral, >100=weak, <100=resistant
        internal const int ResistancePack2   = 0x02C; // [IceRes, ThunderRes] — both confirmed
        internal const int ResistancePack3   = 0x030; // [WindRes, HolyRes]   — wind confirmed
        internal const int MinGoldDrop       = 0x034; // int   — minimum gold dropped on death
        internal const int DropChance        = 0x038; // int   — item drop chance (0–100)

        // ── Identity ─────────────────────────────────────────────────────────
        internal const int EnemySpeciesId    = 0x042; // ushort — enemy species ID; used to look up name, stats, and model data
        internal const int EntityScale       = 0x044; // float — CONFIRMED entity/collision radius; enemy-specific (6.0–8.0); does not affect attack hitbox
        internal const int EntityScaleCopy   = 0x048; // float — copy of EntityScale set at spawn; secondary reference
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
        // ATTACK/DAMAGE block above EnemySpeciesTable.AttackPower for the full enemy→player formula.
        internal const int DefenseStats      = 0x090; // packed: low ushort = DamageReduction, high ushort = WeaponDefense

        internal const int HitStunTimer      = 0x098; // int   — 0 at rest; set to a positive value on hit (e.g. 966 observed); presumably a stun or invincibility-frame countdown

        internal const int ForceItemDrop     = 0x0A0; // int   — forces a specific item drop when nonzero
        internal const int RenderDistance    = 0x0A4; // float — CONFIRMED controls render distance and map-dot appearance threshold
        internal const int Abs               = 0x0B0; // int   — XP reward granted to the player on kill

        internal const int Unk0B8            = 0x0B8; // float — 0.0 at rest; set to a small near-zero value (~3.8e-5, raw 0x35800000) at activation alongside Unk0BC; purpose unknown
        internal const int Unk0BC            = 0x0BC; // float — 10.0 at activation (identical to resting BehaviorRangeX); decreases to a smaller positive value on hit (~2.8–6.97 observed); not a pure hit-driven field

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
        internal const int MotionCommitFlag  = 0x0F4; // halfword — commit gate (-0x1b3c). CMonstorUnit::Step (ELF 0x1dd890) commits the requested motion (0xEC) into the render object's player ONLY when this is nonzero; the engine sets it when the current clip finishes. Writing 1 forces an immediate motion switch (interrupt).

        // ── Species data pointer (regular enemies) ───────────────────────────
        // +0x0FC: PS2-native pointer to a per-species data block, set at spawn and SHARED by all
        // live slots of the same species (slots of species 3 → 0x010A5260, species 6 → 0x011D94B0, etc.;
        // changes with the species). This is the regular-enemy analog of the boss SpeciesDataPtr (0x04C),
        // which is 0 for non-bosses. Confirmed (savestate analysis 2026-06-09) NOT read by the per-frame
        // DrawMonstor / Step / MoveChara / CheckDmg paths, so its exact role is unconfirmed (likely a
        // spawn/despawn or stat/asset reference). Add 0x20000000 for the PCSX2 address.
        // NOTE: the rendered MODEL/animation is NOT driven by this nor by any FloorSlot field — the
        // engine draws each enemy from a separate CCharacter "render object" at
        // (MonstorUnit + slot*0x3510 + 0x1FCD0), i.e. the ModelScaleOffsets region (see below).
        internal const int SpeciesParamPtr   = 0x0FC; // int   — per-species data block ptr (PS2-native); shared by same-species slots; not read per-frame

        // ── World Position ────────────────────────────────────────────────────
        internal const int LocationX         = 0x100; // float — world X position; updated each frame as enemy moves
        internal const int LocationZ         = 0x104; // float — world Z (height/elevation); small values (8–13 observed) relative to floor
        internal const int LocationY         = 0x108; // float — world Y position; updated each frame as enemy moves
        internal const int ScaleMultiplier   = 0x10C; // float — 1.0 at spawn for all observed enemies; possibly a per-instance scale override; purpose unconfirmed
        internal const int ReticleWidth      = 0x110; // float — CONFIRMED controls horizontal lock-on reticle width
        internal const int ReticleHeight     = 0x114; // float — CONFIRMED lock-on reticle height; may also set hitbox height
        internal const int LockOnDistance    = 0x118; // float — CONFIRMED distance at which lock-on becomes available; default 120.0
        internal const int Opacity           = 0x120; // float — CONFIRMED enemy opacity; default 128.0; lower = more translucent; drops to ~44.0 on the hit frame alongside FlashColorRed; recovers after flash
        internal const int Unk124            = 0x124; // float — 0.0 at rest; becomes 4.0 on the hit frame alongside Opacity drop; purpose unknown

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

        // ── Behavior Ranges (set at spawn, static per enemy species) ─────────────
        // All observed enemies start with X=10.0, Y=20.0, Z=20.0, Unk14C=128.0.
        // During certain attacks (e.g. Captain lunge) the forward axis expands dramatically:
        //   BehaviorRangeX: 10→137.4, BehaviorRangeY/Z: 20→9.6 (narrows to a forward spike).
        // Engine overwrites these at 60fps; a one-time write at floor load has no lasting effect.
        // NOTE: none of these gate the attack/aggro DECISION — the AI computes distance fresh each frame via the
        // STB command _GET_DISTANCE (cmd 10) and compares it to per-species float literals baked in the .stb, not
        // to any of these slot fields (the overlay AI code reads neither +0x140 nor +0x14C). Purpose unconfirmed.
        internal const int BehaviorRangeX    = 0x140; // float — resting 10.0; expands on attack (forward reach during the swing)
        internal const int BehaviorRangeY    = 0x144; // float — resting 20.0; narrows during some attacks (lateral range)
        internal const int BehaviorRangeZ    = 0x148; // float — resting 20.0; narrows during some attacks (depth range)
        internal const int Unk14C            = 0x14C; // float — 128.0 for all observed enemies; constant during attacks; NOT aggro/attack range (purpose unknown)

        // Three additional float fields after Unk14C. Observed as 0.0 for regular Auntie Medu and
        // non-zero (127.5, 80.0, 15.0) for the same enemy when the mod's miniboss process was active.
        // The mod process may have indirectly triggered the game engine to write these; they are not
        // believed to be mod-written values. Purpose unknown — possibly secondary AI range tiers used
        // by specific behavior scripts (not all enemy species).
        internal const int Unk150            = 0x150; // float — 0.0 for most enemies; 127.5 observed (≈Unk14C) during mod miniboss activation
        internal const int Unk154            = 0x154; // float — 0.0 for most enemies; 80.0 observed during mod miniboss activation
        internal const int Unk158            = 0x158; // float — 0.0 for most enemies; 15.0 observed during mod miniboss activation

        // AiCycleParity flips 0↔1 at each AI attack cycle.
        // Confirmed alternating pattern across Pirate's Chariot, Auntie Medu, and Mask of Prajna.
        // Likely used by the AI to select between two alternate behavior states or attack sets each cycle.
        internal const int AiCycleParity     = 0x160; // int   — 0/1 parity bit; toggles each AI attack cycle; never stays fixed during active combat

        internal const int FlashActivation   = 0x164; // int   — CONFIRMED flash trigger: 0=off, 1 or 2=active; see block comment above
        internal const int FlashDecayRate    = 0x168; // float — CONFIRMED; see block comment above; engine negates (write +0.08 → stored −0.08); duration = 1.0/rate frames
        internal const int FlashTimer        = 0x16C; // float — CONFIRMED engine-owned countdown; always reset to 1.0 by engine at flash-start; decrements by FlashDecayRate each frame

        // ── On-Hit Event Fields ────────────────────────────────────────────────
        // All written by the engine on the same frame as FlashColorRed; all zero at rest except Unk17C=1.0.
        // HitFacingX and HitFacingZ are exact snapshots of FacingX/FacingZ at the moment of impact —
        // confirmed by comparing same-frame poll values.
        internal const int HitFacingX        = 0x170; // READ ONLY — float; 0.0 at rest; engine snapshots FacingX here at moment of impact (confirmed by same-frame poll comparison)
        internal const int HitFacingZ        = 0x178; // READ ONLY — float; 0.0 at rest; engine snapshots FacingZ here at moment of impact
        internal const int Unk17C            = 0x17C; // READ ONLY — float; 1.0 at rest; engine drops to 0.0 on hit; purpose unknown
        internal const int Unk180            = 0x180; // READ ONLY — float; 0.0 at rest; engine sets to 1.0 on hit (observed Skeleton Soldier); purpose unknown
        internal const int KnockbackStrength = 0x184; // READ ONLY — float; engine writes per enemy species value at hit time; confirmed not settable (write at floor load overwritten by engine on hit); Pirate's Chariot=0.10, Skel.Soldier=0.20, Auntie Medu=0.20, Dasher=0.30
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

        // +0x060: CONFIRMED entity/collision radius. Varies 2.0–45.0 per enemy species. Copied to
        // slot EntityScale (0x044) and EntityScaleCopy (0x048) at spawn. Earlier analysis
        // mistakenly labeled this field Unk00C and assigned EntityScale to the 1.0f constant at
        // 0x098; this is the corrected placement.
        internal const int EntityScale = 0x060; // float  — entity/collision radius copied to slot at spawn

        // +0x064/+0x066: the enemy DEFENSE pair, copied into the live slot's DefenseStats (0x090) low/high at
        // spawn. CONFIRMED 2026-06-19 (damage RE) as the real per-dungeon DURABILITY scalers — they grow with
        // dungeon depth where the attack fields stay flat (e.g. regular Mimic: DBC 1/10 → WOF 2/10 → SW 5/20 →
        // Moon Sea 8/30). CMonstorUnit::CheckDmg (ELF 0x1D9F10) reads them when the enemy TAKES damage:
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
        // +0x078: per-species SPAWN-CAP flag, read (as a halfword) by CMonstorUnit::ArrangementPos when
        // assigning species to floor slots. Value 0 or 3 = repeatable (may fill many slots); any other
        // value (observed 2, 4) = spawn at most ONCE per floor — the placement loop retries so the other
        // slots still fill (floor enemy total is unchanged). The game ships ~19/90 species flagged once.
        // Confirmed 2026-06-09 by disassembly + the EnemyModelInjector spawn-once experiment.
        internal const int SpawnCap   = 0x078; // int    — spawn-cap: 0/3 = repeatable, else once-per-floor

        internal const int EnemySpeciesId    = 0x07C; // ushort — enemy species ID stored in table (matches EnemyDefaults.Id); used by engine to verify record ownership
        // +0x07E: 2 bytes padding (always 0)

        internal const int StealItemId= 0x080; // ushort — item ID for steal mechanic; 65535 if none
        // ★ The DEATH-DROP gate (RE'd): copied to the slot's StealItemId high word (0xDA) at spawn, which gates the
        // whole drop block (ELF 0x1DF4C0). 1 = enemy drops on death, 0 = never drops. It is independent of the steal
        // item at 0x080 (Cave Bat has steal item 151 but this flag 0). 0 for flyers + Gol/Sil → those never drop;
        // setting it to 1 (see Enemies.EnableEnemyDrops) permanently enables their drops via the static species table.
        internal const int StealFlag  = 0x082; // ushort — death-drop enable (1=drops, 0=no drop)

        internal const int ItemResA   = 0x084; // ushort — item resistance A; semantics unconfirmed; scale resembles elemental resistance
        internal const int ItemResB   = 0x086; // ushort — item resistance B

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // ENEMY → PLAYER DAMAGE (how hard an enemy hits) — RE'd 2026-06-19. KEY RESULT: AttackPower below
        // does NOT control actual hit damage. The applied damage is computed in BtCheckDamageProc (dun
        // overlay 0x01DBAFD0) as:
        //     damage = baseAttack − playerDefense      (clamped > 0; halved in some block/guard cases)
        //     then applied via AddNowLife(player, −damage)  (ELF 0x1BE710)
        // baseAttack is set PER-ATTACK by the enemy's STB behavior script (via the _SET_DMG_PARA command,
        // ELF 0x1E3FD0) — it is NOT read from any species-table field. Proof: across all regular mimics
        // (which hit for very different amounts) every attack field here is IDENTICAL (AttackPower=235,
        // all elementals=100); only HP (0x050), DamageReduction (0x064), WeaponDefense (0x066), XP (0x06C)
        // and gold (0x070) differ. So per-dungeon attack strength lives in each species' STB script, and
        // normalizing it would require runtime STB patching (BossScriptPatcher-style), not a field write.
        // DURABILITY (HP + DamageReduction/WeaponDefense) IS field-driven and can be normalized directly.
        // ════════════════════════════════════════════════════════════════════════════════════════════
        //
        // +0x088: base melee attack power. Regular enemies: 82–200. Bosses: 65535 (sentinel; use
        // only behavior-script attacks). 0 if enemy has no melee attack (e.g. pure-projectile flyers).
        // NOTE: this field is NOT the real hit damage (see the block above) — treat it as a category marker.
        // +0x08A–+0x094: six elemental attack-output multipliers in the same element order as the
        // resistance fields: [fire, ice, thunder, wind, holy, unknown(dark?)]. Scale: 100=neutral,
        // >100=enemy's primary attack element, <100=reduced output of that element.
        // Example: thunder-beast eid=12 → [50,50,120,50,50,50] (strong thunder, weak others).
        // Bosses (atk=65535) all have these at 0; pure-projectile enemies may have them at 0 too.
        //
        // IMPORTANT — AttackPower = 65535 does NOT drive the boss-defeat sequence by itself.
        // It is a sentinel that signals "boss-class enemy" to the engine (no melee damage), but the
        // defeat callback that triggers the exit cutscene lives in the SpeciesDataPtr behavior block.
        // ModelCodeCopy (0x040) determines which behavior script the engine dispatches; that script
        // contains the on-death hook pointer that ultimately calls the defeat function at ELF 0x0F5DF0.
        // Writing 150 to AttackPower on a redirected boss slot prevents melee one-shots but does NOT
        // prevent the defeat sequence from firing — the callback is loaded independently of this field.
        internal const int AttackPower    = 0x088; // ushort — base melee attack power; 82–200 normal; 0 no melee; 65535 boss (sentinel only, see block comment)
        internal const int ElemAtkFire    = 0x08A; // ushort — fire    attack multiplier (100=neutral)
        internal const int ElemAtkIce     = 0x08C; // ushort — ice     attack multiplier
        internal const int ElemAtkThunder = 0x08E; // ushort — thunder attack multiplier (spike for rock/beast/metal/mimic categories)
        internal const int ElemAtkWind    = 0x090; // ushort — wind    attack multiplier
        internal const int ElemAtkHoly    = 0x092; // ushort — holy    attack multiplier
        internal const int ElemAtkDark    = 0x094; // ushort — dark(?) attack multiplier; 20 for some physical types
        internal const int Unk042         = 0x096; // ushort — 0 for all observed valid enemies

        // +0x098: constant 1.0f for all observed enemies. Purpose unknown; do not use as EntityScale.
        // (Earlier analysis mistakenly labeled this EntityScale; the true EntityScale is at 0x060.)
        internal const int Unk098         = 0x098; // float  — constant 1.0f; purpose unknown
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
        internal const int PlayingMotionId    = 0xBD8; // int   — currently-PLAYING motion id (mirrored to FloorSlot 0x20938, read by _STATUS_GET_MOTION_ID). RE +0xc68.
        // PLAYING motion FRAME (float). Same field _SET_MOTION_FRM (ELF 0x1e1cb0) writes and _GET_MOTION_FRM reads.
        // NOTE: the motion-table KEY "speed" is NOT the frame-advance rate, so to retime a clip drive THIS directly.
        internal const int PlayingMotionFrame = 0x260; // RE +0x2F0 / absolute MonstorUnit+slot*0x3510+0x1FFC0.
    }

    /// <summary>
    /// Per-dungeon / per-floor enemy spawn layout tables — the data that decides WHICH enemy
    /// species (and therefore which model + AI) spawn on each floor of each dungeon.
    ///
    /// Confirmed from ELF binary analysis 2026-06-07 by disassembling BtLoadMonstor__Fi (dun.bin).
    ///
    /// Reference chain (how a dungeon floor populates its enemy slots):
    ///   BtEnemyLayoutList    @ 0x002917B0  — 7 dungeon pointers (NORMAL floors)
    ///   BtUraEnemyLayoutList @ 0x002917D0  — 7 dungeon pointers (URA / back floors; 裏 = "back")
    ///       │ index by dungeon number ([gp − 0x625C])
    ///       ▼
    ///   BtEnemyLayout0N / BtUraEnemyLayout0N  — per-dungeon array, one 0x70-byte block per floor
    ///       │ + floor * 0x70
    ///       ▼
    ///   Floor block = 9 entries × 0x0C bytes (see EnemyLayoutEntry below)
    ///       │ for each entry whose Id (+0x4) != -1:
    ///       ▼
    ///   CMonstorUnit::SetupBaseModel(this, _, enemyId, 0x26, MonstorModelBuffer)  (ELF 0x001DFE90)
    ///       │ enemyId → packed species record (EnemySpeciesTable @ 0x0027FB00) → ModelCode "eNNa"/"cNNx"
    ///       ▼
    ///   loads mesh into MonstorModelBuffer (PS2 0x01F066D0) and behavior/AI script into
    ///   MonstorScriptBuffer (PS2 0x01F066E0). Both the live enemy slot array (0x21E16BA0) and the
    ///   ModelScale table (0x21E18530) are sub-arrays of one global, MainMonstorUnit (PS2 0x01DF87D0,
    ///   size 0x60750). BtArrengeMonstor__Fv wires each of the 16 slots to a 0x10-byte script entry.
    ///
    /// IMPORTANT — CORRECTED 2026-06-19: the value at entry +0x4 is the physical species-table ROW INDEX
    /// (EnemyDefaults.TableIndex), NOT the internal enemy id. Confirmed two ways: (1) BtLoadMonstor passes
    /// +0x4 straight to SetupBaseModel's tableIndex arg; (2) a raw ELF dump of DBC floor 0 has +0x4 = 52/1/3
    /// = the TableIndex of CaveBat(Id 60)/SkeletonSoldier(Id 3)/Dasher(Id 6), i.e. TableIndex not Id. This is
    /// why SetSpawnRosterMix correctly writes EnemyDefaults.TableIndex here. (The prior note claiming this was
    /// EnemyDefaults.Id was wrong.) Always resolve via EnemySpeciesTable / EnemyData.cs.
    ///
    /// Full decoded rosters for all 7 dungeons (normal + Ura) are in /enemy-spawn-layout.md at repo root.
    /// </summary>
    internal static class BtEnemyLayout
    {
        // ── Master pointer tables (PS2-native = file-resident in the main ELF segment) ──
        // PCSX2 address = native + 0x20000000.  ELF file offset = native − 0x100000 + 0x100.
        internal const int EnemyLayoutListBase    = 0x002917B0; // 7 × 4-byte ptrs → BtEnemyLayout00..06   (normal floors)
        internal const int UraEnemyLayoutListBase = 0x002917D0; // 7 × 4-byte ptrs → BtUraEnemyLayout00..06 (back floors)
        internal const int DungeonCount           = 7;

        // ── Floor block geometry ──
        internal const int FloorStride = 0x70; // bytes per floor block
        internal const int EntryStride = 0x0C; // bytes per enemy entry
        internal const int EntriesPerFloor = 9; // max distinct species entries per floor (block tail at +0x6C, =1)

        // Per-dungeon layout symbol addresses (PS2-native) and floor counts, from ELF symbol sizes.
        // Index = dungeon number. Names are best-effort (dun\dNN*.cfg ordering); floor counts are authoritative.
        //                                     normal       ura          floors  dungeon
        // [0] BtEnemyLayout00 / Ura00         0x002860D0   0x00286760    15      Divine Beast Cave (d01)
        // [1] BtEnemyLayout01 / Ura01         0x00286DF0   0x00287560    17      Wise Owl Forest (d02)
        // [2] BtEnemyLayout02 / Ura02         0x00287CD0   0x002884B0    18      Lake Gilna / Coastal (d03)
        // [3] BtEnemyLayout03 / Ura03         0x00288C90   0x00289470    18      Queens (d04)
        // [4] BtEnemyLayout04 / Ura04         0x00289C50   0x0028A2E0    15      Shipwreck (d05)
        // [5] BtEnemyLayout05 / Ura05         0x0028A970   0x0028B4D0    26      Muska Lacka / Sun & Moon (d06)
        // [6] BtEnemyLayout06 / Ura06         0x0028C030   0x0028EBF0   100      Moon Sea + Demon Shaft (d07)
        internal static readonly int[] LayoutBase    = { 0x002860D0, 0x00286DF0, 0x00287CD0, 0x00288C90, 0x00289C50, 0x0028A970, 0x0028C030 };
        internal static readonly int[] UraLayoutBase = { 0x00286760, 0x00287560, 0x002884B0, 0x00289470, 0x0028A2E0, 0x0028B4D0, 0x0028EBF0 };
        internal static readonly int[] FloorCount    = { 15, 17, 18, 18, 15, 26, 100 };

        /// <summary>PCSX2 address of a floor block's first entry. Pass a base from LayoutBase/UraLayoutBase.</summary>
        internal static int FloorAddress(int layoutBaseNative, int floor) =>
            (int)(layoutBaseNative + Memory.Pcsx2Base) + floor * FloorStride;

        /// <summary>PCSX2 address of entry <paramref name="entry"/> (0–8) within a floor block.</summary>
        internal static int EntryAddress(int layoutBaseNative, int floor, int entry) =>
            FloorAddress(layoutBaseNative, floor) + entry * EntryStride;

        // ── Entry field offsets (0x0C bytes per entry) ──
        // CONFIRMED 2026-06-19 by disassembling the ONLY pool reader, BtLoadMonstor__Fi (dun.bin overlay
        // @ 0x01DB9330): it loads BtEnemyLayoutList[dungeon] (0x2917B0) / Ura (0x2917D0), adds floor*0x70,
        // then walks entries 0..8 at stride 0xC reading ONLY +0x4 (Id), calling SetupBaseModel for each,
        // and BREAKING when +0x4 == -1. It never reads +0x0 or +0x8. A whole-binary scan (main ELF + dun
        // overlay) found no other reader of the 0x2860D0..0x291660 pool. So ONLY Id (+0x4) drives spawns.
        // +0x0: entry 0 holds a per-floor value (3/4/5/7/8, rising with depth — this is DungeonData's
        // "tier"); entries 1..8 are always 1. NOT read by any code → vestigial authoring metadata (matches
        // the 2026-06-09 test where overwriting it changed nothing). Floor population is fixed by spawn-point
        // generation at floor assembly, not by this table.
        internal const int Count    = 0x0; // int   — entry-0 = DungeonData "tier"; UNUSED at runtime (see note)
        // +0x4: species-table row index (= EnemyDefaults.TableIndex; NOT EnemyDefaults.Id — see the note
        // above). -1 = terminator/empty. This is the tableIndex SetupBaseModel loads. (Const kept named
        // `Id` to match the field's conventional name, but it holds a TableIndex.)
        internal const int Id       = 0x4; // int   — species TableIndex; -1 = unused/terminator
        // +0x8: spawn weight (percent); source data sums to ~100 per floor, but NOT read by the spawn path —
        // species selection is uniform rand%(distinct loaded species). No reader of +0x8 exists in the binary.
        internal const int Weight   = 0x8; // int   — spawn weight %; UNUSED at runtime (uniform pick)
    }

    /// <summary>
    /// Global state variables written by the boss-defeat function at ELF 0x0F5DF0.
    ///
    /// The game uses r28 (gp = PS2-native 0x01E00000) as the global data pointer.
    /// All addresses below are PCSX2 (= PS2-native + 0x20000000).
    ///
    /// Write order observed in the function (abbreviated):
    ///   1. 0x21DF94D8 — receives arg1 (boss slot pointer); first write in function
    ///   2. 0x21DF94E0 / 0x21DF9500 / 0x21DF94F8 / 0x21DF9510 / 0x21DF94EC / 0x21DF94F4 — dungeon exit state struct
    ///   3. jal BGM transition (PS2=0x0022BA00) — audio state changes before any C# poll can react
    ///   4. 0x21DF881C — dungeonClear flag (Dungeon.cs already resets this, but too late)
    ///   5. 0x21DF94E4 — written four separate times by the function
    ///   6. 0x21DF94FC — written to 1; likely the "boss defeated / trigger exit" flag
    ///   7. 0x21DF94E8 — cleared to 0
    ///   8. 0x21DF9504 — cleared to 0
    ///   9. 0x21D90408 — secondary write (r1 = 0x01D90000; base is separate from gp block)
    ///
    /// Key insight: all nine writes above happen inside a single MIPS frame.  No C# poller
    /// (even at 50 ms / ~3 frames) can observe and undo them before the engine processes the
    /// exit sequence.  The only reliable prevention is to null the on-death callback pointer
    /// inside the SpeciesDataPtr behavior block BEFORE the boss dies.
    ///
    /// The "boss defeated" exit trigger is believed to be <see cref="BossDefeatedFlag"/> (0x21DF94FC),
    /// since it is the only field explicitly set to 1 rather than copied from a computed value.
    /// Resetting DungeonClear alone is insufficient.
    /// </summary>
    internal static class BossDefeatState
    {
        // All addresses are PCSX2 (PS2-native + 0x20000000).

        // ── Boss slot state cluster (gp − 0x6B28 .. gp − 0x6AEC) ────────────────
        // This block is a contiguous struct. The full span is 0x21DF94D8–0x21DF9510.
        internal const int BossSlotPtr      = 0x21DF94D8; // arg1 passed in; first value written
        internal const int ExitState0       = 0x21DF94E0; // dungeon exit state field
        internal const int ExitStateE4      = 0x21DF94E4; // written four times with computed values
        internal const int ExitStateEC      = 0x21DF94EC; // dungeon exit state field
        internal const int ExitStateF4      = 0x21DF94F4; // dungeon exit state field
        internal const int ExitStateF8      = 0x21DF94F8; // dungeon exit state field
        internal const int ExitState00      = 0x21DF9500; // dungeon exit state field
        internal const int ExitState04      = 0x21DF9504; // cleared to 0
        internal const int ExitState10      = 0x21DF9510; // dungeon exit state field

        // ── "Boss defeated" / exit trigger ──────────────────────────────────────
        // Written to 1 (literal addiu r2,r0,1) late in the function; believed to be the
        // primary flag the engine polls to start the exit/ending sequence.
        // Resetting only DungeonClear (0x21DF881C) is NOT sufficient — this field must also
        // be kept at 0 to prevent the exit from triggering on non-boss floors.
        internal const int BossDefeatedFlag = 0x21DF94FC; // int — 0=normal, 1=boss defeated (triggers exit)

        // ── DungeonClear (separate gp block) ────────────────────────────────────
        // Written by the same defeat function.  Dungeon.cs already polls and resets this,
        // but the reset races against the engine reading it in the same or next frame.
        internal const int DungeonClear     = 0x21DF881C; // int — 0=in progress, nonzero=cleared (already handled by Dungeon.cs)

        // ── Secondary write (different base: r1 = 0x01D90000) ───────────────────
        internal const int Secondary0408    = 0x21D90408; // int — written near function end; purpose unknown
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
        internal const int BehaviorFlags      = 0x40; // int   — packed flags controlling hit response / VFX
        internal const int PhaseCount         = 0x44; // int   — number of animation phases in this behavior
        // +0x48: always 1
        internal const int ScriptMode         = 0x4C; // int   — packed mode flags for the behavior FSM
        internal const int PackedFlags2       = 0x50; // int   — secondary packed flags
        // +0x54: always -1 (0xFFFFFFFF)
        internal const int ProjectileSpeed    = 0x58; // float — non-zero only for projectile behaviors
        internal const int ProjectileLifetime = 0x5C; // int   — frames the projectile lives; 0 if no projectile
        // +0x60–+0x6C: four ints, all -1
    }

    /// <summary>
    /// EE RAM addresses + unit-relative offsets used by the runtime enemy injector / boss-fight orchestration
    /// (EnemyModelInjector). PCSX2 addresses unless noted (= PS2-native + 0x20000000).
    /// </summary>
    internal static class InjectorAddresses
    {
        // Per-floor enemy-count target globals read by CMonstorUnit::ArrangementPos — the placement loop runs this
        // many times (capped by walkable spawn tiles). Native 0x01D564xx; these three are the ones used as the count arg.
        internal static readonly long[] PopulationTargets = [0x21D56494, 0x21D5649C, 0x21D564A0];

        // STB load-address scan window (PS2-native) for the remaining RAM scans (companion locate / pattern scans).
        internal const long StbScanLo = 0x01000000;
        internal const long StbScanHi = 0x01A00000;

        // Engine spawn-candidate table, indexed by ArrangementPos at MainMonstorUnit + SpawnCandidateTableOff
        // (stride SpawnCandidateStride; flag @+0, eid @+4).
        internal const long SpawnCandidateTableOff = 0x1DEA8;
        internal const int  SpawnCandidateStride   = 0x9C;

        // MainMonstorUnit-relative motion fields: unit + slot*ModelScaleOffsets.ModelStride + offset.
        internal const long PlayingMotionFrameOff = 0x1FFC0; // float — same field as ModelScaleOffsets.PlayingMotionFrame (absolute)
        internal const int  PlayingMotionIdOff    = 0x20938; // int   — mirror of ModelScaleOffsets.PlayingMotionId
    }
}
