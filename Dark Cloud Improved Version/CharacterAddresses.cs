namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The ACTIVE player character's <c>CCharacter</c> object and its motion-control fields (RE'd from
    /// SCUS_971.11: <c>motionDrive__Fv</c> dun 0x1DB7450, <c>Step__10CCharacterFv</c> main 0x138530,
    /// <c>MotionProc__FP6CFrameP12MOTION_STATEP8Mot_List</c> main 0x147D20). See docs/character-motion-table.md.
    ///
    /// The controlled character (Toan/Xiao/Goro/Ruby/Ungaga/Osmond — only one is active at a time) lives at a
    /// FIXED global: <see cref="Base"/> = native 0x1EA1D20. It is the same object <c>WeaponCollision.
    /// EquippedModelPtr</c> (0x21EA1DDC) points into: EquippedModelPtr == Base + <see cref="ModelRootOffset"/>.
    /// </summary>
    internal static class CharacterMotion
    {
        internal const long NativeBase = 0x001EA1D20;
        internal const long Base       = 0x21EA1D20; // MMU (native | 0x20000000) — active-character CCharacter

        // ── Field offsets within the CCharacter object ──
        internal const int ModelRootOffset   = 0xBC;  // → native ptr to the model root CFrame (== EquippedModelPtr)
        internal const int MotionSpeedOffset  = 0xC60; // float — motion play-rate override (see below)
        internal const int MotionFlagsOffset  = 0xC64; // uint  — motion flags: bit0x1=stop, 0x2=?, 0x4=restart
        internal const int MotionIdOffset     = 0xC68; // int   — current motion id (-1 = none); set by motionDrive
        internal const int MotionStatusOffset = 0xC70; // int   — motion-phase status out (0/1/2/3)

        // ── Absolute MMU addresses (Base + offset) ──
        // ★ MOTION PLAY-RATE OVERRIDE — the animation-speed knob. Default -1.0. Step__CCharacter fetches the
        // current motion track's baked KEY step, then: `if (0.0 < *MotionSpeedOverride) trackStep = *MotionSpeedOverride`
        // — i.e. a POSITIVE value REPLACES the per-motion step (an ABSOLUTE rate, NOT a multiplier); -1.0 = use the
        // motion's own KEY speed. Drives WHATEVER motion is playing (attacks AND movement). motionDrive rewrites
        // it to -1.0 only on a motion CHANGE (not every frame — the player has no STB script), so a re-apply loop
        // can hold a value between changes. Directly PINE-writable (data write, no code patch).
        internal const long MotionSpeedOverride = Base + MotionSpeedOffset; // 0x21EA2980
        internal const long MotionFlags         = Base + MotionFlagsOffset; // 0x21EA2984
        internal const long MotionId            = Base + MotionIdOffset;    // 0x21EA2988
        internal const long MotionStatus        = Base + MotionStatusOffset;// 0x21EA2990

        internal const float MotionSpeedUseKey = -1.0f; // sentinel = "use the motion's own KEY speed" (default)
    }

    /// <summary>
    /// The engine's whole-character ambient "flash" — an animated ambient tint applied to the ACTIVE unit
    /// (RE'd from <c>setUnitAmbientAnime__Ffffff</c> dun 0x1DC1000 + <c>unitAmbientAnime__FPf</c> 0x1DC1050).
    /// Setting <see cref="Enable"/> makes the per-frame updater drive the unit's ambient colour =
    /// (colour × pulse) + 64 until the <see cref="Count"/> repeats run out. It is character-agnostic — the
    /// game uses it for drink/face-change and Ruby's Mobius charge flash — so the same data writes flash any
    /// active character. Trigger: write colour + speed + count, reset <see cref="Phase"/>, then set Enable.
    /// gp = 0x2A97F0. See <c>CustomXiaoEffects.TriggerCharacterFlash</c> / the Ruby Mobius flash.
    /// </summary>
    /// <summary>
    /// The player's STATUS BLOCK — the CDngStatusData object at <see cref="Base"/>: one per-character block
    /// holding that character's weapon inventory, equipped slot, atla, transform state and the rest.
    ///
    /// Plain vanilla layout, and deliberately NOT owned by any one feature: the weapon-record lookup here is what
    /// every "what is the player actually holding" question resolves through (weapon effects, the SynthSphere
    /// lookup, texture swapping, the ABS grant). It previously lived inside an ABS-rollover class, which made
    /// unrelated code read as if it cared about ABS.
    /// </summary>
    internal static class UserStatus
    {
        internal const long Base                 = 0x21CD954C; // CDngStatusData / "status base"
        internal const int  CharStride           = 0xAA8;      // per-character block stride
        internal const int  WeaponArrayOffset    = 0x450C;     // char block + this = weapon record 0 (id at +0)
        internal const int  EquipSlotArrayOffset = 0x4340;     // + char = that character's equipped weapon slot (byte, 0-9)
        internal const int  TransformStateOffset = 0x8B10;     // int; TransformedMonster = monster-transformed
        internal const int  TransformedMonster   = 10;
        internal const int  MaxWeaponSlots       = 10;         // bag slots 0-9

        /// <summary>A character's inventory weapon record (WEAPON_HAVE) for one of its bag slots — the id is at
        /// +0, and the element/attach/ABS fields hang off it (see <see cref="WeaponCollision"/>).</summary>
        internal static long WeaponRecord(int character, int slot)
            => Base + (long)character * CharStride + WeaponArrayOffset +
               (long)slot * WeaponCollision.InventoryWeaponSlotStride;

        /// <summary>Address of the byte holding which bag slot <paramref name="character"/> has equipped.</summary>
        internal static long EquippedSlotAddr(int character) => Base + EquipSlotArrayOffset + character;
    }

    internal static class CharacterFlash
    {
        internal const long Speed  = 0x202A36F4; // fGpffff9f04 — pulse speed (Ruby's charge flash uses 15.0)
        internal const long Phase  = 0x202A36F8; // fGpffff9f08 — animation phase; reset to 0 to (re)start
        internal const long Count  = 0x202A36FC; // iGpffff9f0c — remaining flash repeats
        internal const long Enable = 0x202A3700; // iGpffff9f10 — 1 = on; the updater clears it when Count runs out
        internal const long ColorR = 0x21F068D0; // fRam01f068d0 — flash colour RGB (float, 0-255)
        internal const long ColorG = 0x21F068D4; // fRam01f068d4
        internal const long ColorB = 0x21F068D8; // fRam01f068d8

        // The game's STOCK charge flash — the cyan-blue pulse it fires when a charge attack finishes building
        // (Ruby's Mobius peak, Ungaga's guard charge). Every mod flash that means "a charge completed" must use
        // these so it reads as part of the game rather than as a mod effect; picking your own RGB looks wrong.
        internal const float ChargeR     = 0f;
        internal const float ChargeG     = 122f;
        internal const float ChargeB     = 208f;
        internal const float ChargeSpeed = 15f;
        internal const int   ChargeCount = 1;
    }

    /// <summary>
    /// Toan's live status/HP fields (same block Player.Toan wraps with getters; exposed here as
    /// addresses for effects that need raw bit-level status access, e.g. the Evilcise/Maneater
    /// curse loops). The status word and its single shared countdown timer cover ALL status
    /// ailments at once — refreshing the timer extends every active status bit.
    /// </summary>
    internal static class ToanState
    {
        internal const int Hp          = 0x21CD955E; // ushort — current HP
        internal const int Status      = 0x21CDD814; // ushort — status bit field (see Status* bits)
        internal const int StatusTimer = 0x21CDD824; // ushort — shared countdown (frames) for all status bits

        // Status bit masks
        internal const ushort StatusNearDeath = 0x02;
        internal const ushort StatusFreeze    = 0x04;
        internal const ushort StatusStamina   = 0x08;
        internal const ushort StatusPoison    = 0x10;
        internal const ushort StatusCurse     = 0x20;
        internal const ushort StatusGoo       = 0x40;

        /// <summary>Timer value the curse effects use when (re)applying a status: 3600 frames = 60s.</summary>
        internal const ushort StatusDurationFrames = 3600;
    }

    /// <summary>
    /// <c>CCharacter</c> FIELD LAYOUT — vanilla engine struct. Applies to the active player at
    /// <see cref="Base"/> AND to every dungeon chara slot (see DungeonCharaDraw), since those are the same
    /// object type. Nothing here is feature-specific: it's what any code that renders, poses, tints or
    /// fades a character needs. (CharacterMotion holds the motion-control view of the same object.)
    /// </summary>
    internal static class CCharacter
    {
        internal const long Base = CharacterMotion.Base;   // the ACTIVE player character (one source of truth)

        internal const int  CharPos        = 0x10;    // x, z/height, y (+0x1C = w, 1.0)
        internal const int  CharRot        = 0x60;    // CObject EULER rotation x/y/z
        internal const int  CharRotY       = 0x64;    // CObject EULER rotation Y (yaw). GetRotation__7CObject reads
                                                      // +0x60/+0x64/+0x68 — these are ANGLES, not a direction vector
                                                      // (an upright character's X/Z angles are ~0).
        internal const int  CharScale      = 0x90;    // CObject scale x/y/z. Draw__10CCharacter re-applies it EVERY
                                                      // draw, so writing it is the clean whole-model size lever
                                                      // (WeaponCollision.EffectObjectScale is this same field).
        internal const int  MotionFrame    = 0x2F0;   // float — current frame of the playing motion
        internal const int  MotionList     = 0x344;   // → the model's motion list (Mot_List; entry stride below)
        // Mot_List entry, indexed by motion id. Step__10CCharacter (0x138530) reads the entry's STEP as the
        // motion's native play rate, then — if the override at MotionSpeedOffset (+0xC60) is > 0 — TEMPORARILY
        // overwrites the entry with it for that step and restores it afterwards. So the override is an ABSOLUTE
        // rate, not a multiplier: to scale a motion's speed you must read its own step here and scale THAT. The
        // rate is per-motion baked data and is NOT 1.0 in general.
        internal const int  MotionEntryStride = 0x10;
        internal const int  MotionEntryStart  = 0x00;  // int   — first frame
        internal const int  MotionEntryEnd    = 0x04;  // int   — last frame
        internal const int  MotionEntryStep   = 0x08;  // float — the KEY play rate (frames advanced per step)
        internal const int  CharModel      = 0xBC;    // → model root CFrame (== CharacterMotion.ModelRootOffset)
        internal const int  ClothList      = 0xC74;   // → up to 4 CCloth ptrs (null list = no cloth)
        internal const int  MotionSlotBase = 0xC20;   // channel[i] MOTION_TYPE ptr at +0xC20 + i*4
        internal const int  MotionSlots    = 8;
        internal const int  MotionFlags    = 0xC64;   // per-step motion flags
        internal const int  MotionRestart  = 0x4;     //   bit2 = clean restart (frame 0, no blend); consumed once
        internal const int  MotionId       = 0xC68;   // current motion id; Step__10CCharacter early-outs when < 0 → pose FROZEN
        internal const int  CharaTint      = 0xCE0;   // float3 ambient ADD (tint)
        internal const int  NpcOpacity     = 0xCEC;   // model opacity 0..128; Draw folds it into ambient alpha (must be > 0 to draw)
        internal const int  DimFactor      = 0xCF0;   // < 1.0 dims the model
        internal const int  LightFrom      = 0xD00;   // point-light slots — zero them so the light loop skips
        internal const int  LightTo        = 0xD60;
    }

    /// <summary>
    /// <c>CFrameVu1</c> NODE — one bone/frame of a model tree. The tree is child/sibling linked; a node's WORLD
    /// matrix is built by GetLWMatrix (0x1281B0) walking <see cref="Parent"/> up the chain, which is why a tree
    /// can't be cloned by copying its root alone (the shared children still point at the original parent).
    /// The game's own deep copy is CopyFrameVu1 (0x127610): per node a fresh 0x270 struct, geometry SHARED.
    /// </summary>
    internal static class CFrameVu1
    {
        internal const int  NodeStride   = 0x270;  // MUST match the real node size: MotionProc indexes bones as root + i*0x270
        internal const int  RootChild    = 0x138;  // first child
        internal const int  RootSibling  = 0x13C;  // next sibling
        internal const int  Parent       = 0x110;  // parent (the world-matrix chain walks this)
        internal const int  WorldCacheA  = 0x240;  // world-matrix-valid flags — zero to force a recompute
        internal const int  WorldCacheB  = 0x244;
        internal const int  GeomPtr      = 0x260;  // mesh/geometry object (SHARED between copies, never duplicated)
    }

    /// <summary>
    /// <c>CVisualMDTVu1</c> — a SOFTWARE-skinned mesh. Only ~3 bones per character use one; the rest skin on VU1
    /// at draw time (already per-instance). MotionProc2 skins IN PLACE into the MDT vertex array, so two
    /// characters sharing one of these fight over the same verts — a real clone must own its own copy.
    /// </summary>
    internal static class CVisualMDT
    {
        internal const int  VisualSize   = 0x30;        // the visual struct itself
        internal const int  VisVU        = 0x18;        // → VU data ptr; +0x1C = its size
        internal const int  VisMDT       = 0x20;        // → MDT block
        internal const uint MdtMagic     = 0x0054444D;  // "MDT\0" at MDT+0x00
        internal const int  MdtSizeField = 0x08;        // MDT+0x08 = total block size
    }

    /// <summary>
    /// <c>MOTION_TYPE</c> — one animation channel (CCharacter +0xC20 + i*4). Its embedded MOTION_STATE (+0x10)
    /// is the frame counter Step advances, and it points at two PER-CHARACTER scratch buffers that MotionProc2
    /// both reads AND writes — so a copied character that shares them fights the original over its own bones.
    /// </summary>
    internal static class MotionType
    {
        internal const int  BoneMtxPtr    = 0x00;  // → per-bone ANIMATION matrix buffer (MotionProc2 reads *(chan) + bone*0x40)
        internal const int  BoneMtxEntry  = 0x40;  //   per-bone stride
        internal const int  MotionSkinList = 0x08; // → MotionProc2 (software-skinning) list; +0x04 = the rigid list
        internal const int  FrameInfPtr   = 0x60;  // → FRAME_INF, the per-bone SKINNING matrix buffer
        internal const int  FrameInfEntry = 0xD0;  //   per-bone stride
    }

    /// <summary><c>CCloth</c> — a cloth piece hanging off CCharacter +0xC74. Fixed 0x8550 allocation with no
    /// internal cross-refs; the only pointers are its draw packets and its skeleton anchors.</summary>
    internal static class CCloth
    {
        internal const int  ClothObjSize   = 0x8550;  // fixed allocation (__nw 0x8550 in InitCloth)
        internal const int  ClothMaxPieces = 4;       // length of the CCharacter +0xC74 list
        internal const int  ClothActive    = 0x18;    // active draw-packet ptr (engine sets it each frame)
        internal const int  ClothBuf0      = 0x24;    // DBuffID0 packet; +0x28 = DBuffID1 (double-buffered)
        internal const int  ClothAttach    = 0x3C;    // anchor CFrame — drives the SIM when the cloth is stepped
    }

    /// <summary><c>CBound</c> — a body collision capsule the cloth sim collides against. Linked list off
    /// CCloth +0x44. Each capsule is positioned from two body BONES (UpDateDirPos__6CBound → GetLWMatrix), so
    /// re-anchoring those two pointers makes the cloth collide against a different skeleton.</summary>
    internal static class CBound
    {
        internal const int  BoundSize   = 0x130;  // Sizeof__6CBound
        internal const int  BoundNext   = 0x00;   // linked-list next
        internal const int  BoundFrameA = 0xE4;   // capsule endpoint bone A (CFrame*)
        internal const int  BoundFrameB = 0xE8;   // capsule endpoint bone B (CFrame*)
    }

    /// <summary>The EQUIPPED WEAPON is a separate object from the character: its model root (+0xBC) is PARENTED
    /// to the hand bone but DRAWN separately (it is not inside the character's +0xBC tree), in its own texture
    /// pass. So putting a weapon on a copied character means copying its small CFrame tree too.
    ///
    /// To find the HAND BONE, read the weapon root's parent pointer (CFrameVu1.Parent) — do NOT hardcode a bone
    /// index. Every character has a different skeleton (Ungaga 67 bones, Xiao 79, Osmond 84...), so an index is
    /// only ever correct for one of them; the live weapon already tells you which bone it hangs off.</summary>
    internal static class EquippedWeapon
    {
        internal const long WeaponObjGlobal = 0x202A34F0;  // iGpffff9d00 (gp-0x6300)
    }
}
