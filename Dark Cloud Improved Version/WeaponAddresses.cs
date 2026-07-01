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
        // Weapons.LocateWeaponDcol1 to read a weapon's dcol1 Z for sizing its whirl when it's not in WeaponDb.
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

        // ── Charge attack state (ToanKey_Play, RE'd from SCUS_971.11) ──
        // Drives CustomEffects.HeavensCloudEffect's charge ramp + MaintainEnemyHitbox's whirl gate. See
        // Weapons.IsChargingWhirlwind / IsWhirlwindActive and the toan-charge-states memory.
        internal const long ChargeActionState = 0x21DC4494; // DAT_01dc4494 action id (values below)
        internal const int  ActionWindup      = 0xE;        // charge wind-up (meter accumulates; lunge OR whirlwind)
        internal const int  ActionWhirlwind   = 0x18;       // whirlwind executing
        // Charge METER (float, DAT_01dc449c): resets to 1.0 at attack start, accumulates each windup frame,
        // caps at 3.0. Thresholds: ≥1.5 → lunge available, ≥2.5 → whirlwind available (if unlocked).
        internal const long ChargeMeter       = 0x21DC449C;
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

        // ── Inventory equip slot (early weapon-swap detection) ──
        // The battle in-hand weapon id (0x21EA7590, Player.Weapon.GetCurrentWeaponId) only refreshes once Toan
        // is walking again after the menu, so keying off it lags a swap. The inventory equip slot updates
        // IMMEDIATELY: a byte slot index at InventoryEquipSlotAddr indexes the weapon list at
        // InventoryWeaponSlot0Id + slot*InventoryWeaponSlotStride (ushort id). See Weapons.GetEquippedWeaponId.
        internal const long InventoryEquipSlotAddr    = 0x21CDD88C; // byte: current equipped weapon slot (0-9)
        internal const long InventoryWeaponSlot0Id    = 0x21CDDA58; // ushort: slot 0's weapon id
        internal const int  InventoryWeaponSlotStride = 0xF8;       // stride between weapon-list slots
    }
}
