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

        // ── Heaven's Cloud reach extension (see Weapons.ApplyHeavensCloudReach) ─────────────────────
        // Reverse-engineered from SCUS_971.11 (ToanKey_Play 0x241690): every Toan melee hit is built as
        // SearchFrame(equippedModel, "dcol1") -> CCollisionData::Set(pos, radius). The engine only ever
        // searches the frame named "dcol1" — dcol0/2/3 are never used for the player hit. So reach = the
        // "dcol1" frame's swept world position + the per-attack swing radius. We extend HC by moving the
        // "dcol1" frame's Z out (scales the visible swing + the hit) and enlarging the swing radii.
        // The CHARGE attacks use the SAME dcol1 position but radii baked as CODE IMMEDIATES (not the gp
        // constants): lunge (frame 193-196) = 6.0, whirlwind (720-722) = 12.0. With dcol1 pushed out the
        // lunge's small radius overshoots a point-blank enemy, so we patch the lunge's immediate too.

        /// <summary>The 3 swing hit-sphere radius constants in main $gp data (0x002A97F0 − 0x7b88/7b84/
        /// 7b80 → MMU below); stock 2.8 / 5.3 / 6.2. All read ONLY by ToanKey_Play, so bumping them while
        /// HC is equipped is safe and effectively per-weapon (restore on unequip).</summary>
        internal static readonly long[] SwingRadiusAddrs = { 0x202A1C68, 0x202A1C6C, 0x202A1C70 };
        internal const float SwingRadiusStock0 = 2.8f; // sanity value at SwingRadiusAddrs[0]

        /// <summary>The LUNGE charge's hit radius is a code immediate: <c>lui $v0, 0x40c0</c> (=6.0) at
        /// MMU 0x20241AC0 (instruction word 0x3C0240C0). Patch the immediate (high 16 bits of the float)
        /// to raise the lunge radius so it reaches back over the player at extended dcol1. Whirlwind's
        /// 12.0 is at 0x20241B90 (left alone — not the problem).</summary>
        internal const long LungeRadiusInsn          = 0x20241AC0;
        internal const uint LungeRadiusStockInsn     = 0x3C0240C0; // lui $v0, 0x40c0  (radius 6.0)
        internal const long WhirlwindRadiusInsn      = 0x20241B90;
        internal const uint WhirlwindRadiusStockInsn = 0x3C024140; // lui $v0, 0x4140  (radius 12.0)

        // The "dcol1" CFrame in the loaded weapon model (commenu template, ~native 0x660xxx; address
        // varies per session — located by scan). Name is the 4-byte "dcol" + '1' + NUL; the frame's
        // local-matrix translation is (X,Y,Z) at name+0xE8/+0xEC/+0xF0. For Toan weapons it's (0,0,Z);
        // editing Z extends reach. Stock dcol1 Z = 9.2053.
        internal const uint  DcolNameWord     = 0x6C6F6364; // "dcol" little-endian
        internal const byte  Dcol1Digit       = 0x31;       // '1' (the active hit-point frame)
        internal const int   DcolNameToLocalX = 0xE8;       // local-matrix X (Y at +0xEC, Z at +0xF0)
        internal const int   DcolNameToLocalZ = 0xF0;       // local-matrix Z (the reach knob)
        internal const float Dcol1StockZ      = 9.2053f;    // stock dcol1 distance for Heaven's Cloud (matched in the scan)

        // EE main-RAM scan window (MMU) used to locate the dcol1 frame, in 32 KB-safe chunks.
        internal const long ReachScanLo = 0x20100000;
        internal const long ReachScanHi = 0x22000000;
        internal const int  ReachScanChunkWords = 2048;
    }
}
