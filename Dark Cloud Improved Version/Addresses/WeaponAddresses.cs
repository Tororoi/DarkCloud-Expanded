// Weapon address bank: the static WeaponList table and its Dagger-entry absolute view (WeaponTable), the element
// attribute table, the melee kick words, the WEAPON_HAVE record layout, the equipped weapon object and its model /
// dcol frames, the swing-trail ribbon, the weapon menu / CWeaponLevelUp flow, the attachment board and the kill-ABS grant.
// Shot pools are in ShotAddresses.cs, the player action state in CharacterAddresses.cs, item models in ItemAddresses.cs.
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
    /// This backs the per-Dagger absolute addresses in <see cref="WeaponTable"/> (WeaponList[1] = the entry
    /// for item 258 at EE 0x2027A70C). The field offsets below are those names made relative to the entry
    /// base. Stat fields are signed shorts unless noted; the Dagger entry is also used at runtime (see
    /// WeaponTable's "(ALSO RUNTIME)" columns).
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

        // ── Field offsets within a weapon entry (WeaponTable has them as the Dagger entry's absolute addresses) ──
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
    /// The mod's ABSOLUTE-address view of <see cref="WeaponList"/>, as its startup table edits address it
    /// (WeaponBalance, WeaponSpecialReroll, MachoSword's ABS columns, ElfWeaponPatches): every field address is the
    /// Dagger entry's (WeaponList[1] = item 258 at EE 0x2027A70C), and a weapon's own field is
    /// <c>field + Stride × (itemId − &lt;the character's default weapon id&gt;) + &lt;the character's block offset&gt;</c>
    /// (Toan's block has no offset). Each constant is <see cref="DaggerEntry"/> + the <see cref="WeaponList"/> field
    /// offset of the same name; the EE address it comes to is in its trailing comment. "(ALSO RUNTIME)" marks the
    /// columns the engine reads back during play, not only when it builds a weapon.
    /// </summary>
    internal static class WeaponTable
    {
        internal const int DaggerEntry = 0x2027A70C;                        // WeaponList.Base + Stride: item 258 (Dagger)
        internal const int Stride      = WeaponList.Stride;                 // 0x4C between weapons

        // ── Per-character block offsets from the Dagger entry: the block starts at the character's default weapon ──
        internal const int XiaoOffset   = 0xC78;    // Wooden Slingshot's entry
        internal const int GoroOffset   = 0x10EC;   // Mallet's entry
        internal const int RubyOffset   = 0x15F8;   // Gold Ring's entry
        internal const int UngagaOffset = 0x1AB8;   // Fighting Stick's entry
        internal const int OsmondOffset = 0x1F78;   // Machine Gun's entry

        // ── Default weapon ids: the weapon each character's block is indexed from ──
        internal const int DaggerId         = Items.dagger;
        internal const int WoodenSlingshotId = Items.woodenslingshot;
        internal const int MalletId         = Items.mallet;
        internal const int GoldRingId       = Items.goldring;
        internal const int FightingStickId  = Items.fightingstick;
        internal const int MachineGunId     = Items.machinegun;

        // ── Dagger-entry field addresses ──
        internal const int Whp          = DaggerEntry + WeaponList.Whp;          // 0x2027A70C base weapon health points
        internal const int Attack       = DaggerEntry + WeaponList.Attack;       // 0x2027A70E base Attack
        internal const int Endurance    = DaggerEntry + WeaponList.Endurance;    // 0x2027A710 base Endurance
        internal const int Speed        = DaggerEntry + WeaponList.Speed;        // 0x2027A712 base Speed
        internal const int Magic        = DaggerEntry + WeaponList.Magic;        // 0x2027A714 base Magic
        internal const int Synth3       = DaggerEntry + WeaponList.Synth3;       // 0x2027A719 synth slot 3 (0 = none, 1 = regular gray slot, 2 = synth blue slot) (ALSO RUNTIME)
        internal const int Synth4       = DaggerEntry + WeaponList.Synth4;       // 0x2027A71A synth slot 4 (same values) (ALSO RUNTIME)
        internal const int Fire         = DaggerEntry + WeaponList.Fire;         // 0x2027A71E
        internal const int Ice          = DaggerEntry + WeaponList.Ice;          // 0x2027A720
        internal const int Thunder      = DaggerEntry + WeaponList.Thunder;      // 0x2027A722
        internal const int Wind         = DaggerEntry + WeaponList.Wind;         // 0x2027A724
        internal const int Holy         = DaggerEntry + WeaponList.Holy;         // 0x2027A726
        internal const int DinoSlayer   = DaggerEntry + WeaponList.DinoSlayer;   // 0x2027A728
        internal const int UndeadBuster = DaggerEntry + WeaponList.UndeadBuster; // 0x2027A72A
        internal const int SeaKiller    = DaggerEntry + WeaponList.SeaKiller;    // 0x2027A72C
        internal const int StoneBreaker = DaggerEntry + WeaponList.StoneBreaker; // 0x2027A72E
        internal const int PlantBuster  = DaggerEntry + WeaponList.PlantBuster;  // 0x2027A730
        internal const int BeastBuster  = DaggerEntry + WeaponList.BeastBuster;  // 0x2027A732
        internal const int SkyHunter    = DaggerEntry + WeaponList.SkyHunter;    // 0x2027A734
        internal const int MetalBreaker = DaggerEntry + WeaponList.MetalBreaker; // 0x2027A736
        internal const int MimicBreaker = DaggerEntry + WeaponList.MimicBreaker; // 0x2027A738
        internal const int MageSlayer   = DaggerEntry + WeaponList.MageSlayer;   // 0x2027A73A
        internal const int Abs          = DaggerEntry + WeaponList.Abs;          // 0x2027A73C base absorption points (ALSO RUNTIME)
        internal const int AbsAdd       = DaggerEntry + WeaponList.AbsAdd;       // 0x2027A73E ABS added per weapon level (ALSO RUNTIME)
        internal const int Effect1      = DaggerEntry + WeaponList.Effect1;      // 0x2027A744 special effects set 1 (ALSO RUNTIME): 2=Big Bucks, 4=Poor, 8=Quench, 16=Thirst, 32=Poison, 64=Stop, 128=Steal
        internal const int Effect2      = DaggerEntry + WeaponList.Effect2;      // 0x2027A745 special effects set 2 (ALSO RUNTIME): 1=Fragile, 2=Durable, 4=Drain, 8=Heal, 16=Critical, 32=Abs Up
        internal const int BuildUp      = DaggerEntry + WeaponList.BuildUp;      // 0x2027A748 build-up branches
        internal const int MaxAttack    = DaggerEntry + WeaponList.MaxAttack;    // 0x2027A750 (ALSO RUNTIME)
        internal const int MaxMagic     = DaggerEntry + WeaponList.MaxMagic;     // 0x2027A752 (ALSO RUNTIME)

        // ── Lamb's Sword: the two ELF thresholds its transform and stat upgrade compare against ──
        internal const int LambTransformThreshold = Addresses.lambSwordPercent;      // 0x202A1818 double: the percent at which it transforms into the Wolf Sword (vanilla 0.2; WeaponBalance writes 0.5)
        internal const int LambStatsThreshold     = Addresses.lambSwordStatsPercent; // 0x202A188C float:  the percent at which its stats upgrade (vanilla 0.2; WeaponBalance writes 0.5)
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

    /// <summary>The words Toan's melee hits take their KNOCKBACK from. ToanKey_Play calls SetKickBack(strength, decay)
    /// as each hit's collision is planted; the kick moves the enemy `strength` a frame, less `decay` each frame, so it
    /// travels ≈ strength² / (2·decay). Strengths: hits 1–2 the shared 1.2 word (read from ten places in the ELF, Goro's
    /// smash among them); hits 3–5, the lunge and the whirlwind bake theirs as immediates, made data by the ISO patch
    /// (CodeCaves.MeleeKickWords). Decays: three words — 0.2 for hits 1–2, 0.3 for hits 3 and 5 and both charge attacks,
    /// 0.4 for hit 4. Set as a whole and put back by <see cref="MeleeKick"/>.</summary>
    internal static class MeleeKickWords
    {
        internal const long  Strength12 = 0x202A1AF8, Decay12 = 0x202A1C50, Decay35 = 0x202A1A80, Decay4 = 0x202A1B5C;
        internal const float VanillaStrength12 = 1.2f, VanillaHit3 = 1.5f, VanillaHit4 = 2.0f, VanillaHit5 = 3.0f, VanillaCharge = 3.0f;
        internal const float VanillaDecay12 = 0.2f, VanillaDecay35 = 0.3f, VanillaDecay4 = 0.4f;
    }

    /// <summary>
    /// The weapon RECORD — the engine's <c>WEAPON_HAVE</c> struct (the type name appears throughout the ELF, e.g.
    /// <c>SetWeaponElementStatus__FP11WEAPON_HAVE</c>). One per bag slot, plus a live BATTLE copy.
    ///
    /// Everything a weapon "is" hangs off this: level, WHP, ABS, the anti-monster array, the attachment slots, and
    /// the element block. Address a record with <see cref="DngStatusData.WeaponRecord"/> — do NOT re-derive the
    /// inventory base here; that is the status block's job.
    /// </summary>
    internal static class WeaponHave
    {

        // ── Inventory equip slot (early weapon-swap detection) ──
        // The battle in-hand weapon id (0x21EA7590, Player.Weapon.GetCurrentWeaponId) only refreshes once Toan
        // is walking again after the menu, so keying off it lags a swap. The inventory equip slot updates
        // IMMEDIATELY: a byte slot index at InventoryEquipSlotAddr indexes the weapon list at
        // InventoryWeaponSlot0Id + slot*InventoryWeaponSlotStride (ushort id). See WhirlwindScale.EquippedWeaponId.
        internal const long InventoryEquipSlotAddr    = 0x21CDD88C; // byte: current equipped weapon slot (0-9)
        internal const long InventoryWeaponSlot0Id    = 0x21CDDA58; // ushort: slot 0's weapon id
        internal const int  InventoryWeaponSlotStride = 0xF8;       // stride between weapon-list slots

        // ── Runtime weapon-record (WEAPON_HAVE) field offsets, relative to a slot's base
        // (InventoryWeaponSlot0Id + slot * InventoryWeaponSlotStride) ──
        internal const int  InventoryWeaponLevelOffset  = 0x02;     // short: weapon level — increments when an
                                                                    //   ABS level-up absorbs the attachments
        internal const int  InventoryWeaponMaxWhpOffset = 0x0C;     // short: max WHP
        internal const int  InventoryWeaponWhpOffset    = 0x10;     // float: current WHP
        internal const int  AbilityFlagsOffset          = 0xEE;     // ushort: the live ability flags (Effect1 | Effect2 << 8)
        internal const int  FragileFlag = 0x100, DurableFlag = 0x200;   // WHP drain ×2 / ×0.5 (BattleSubWeaponDmg 0x1B5D90)
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

        // ── The weapon record's ELEMENT block (WEAPON_HAVE; record-relative) ──
        // SetWeaponElementStatus (0x20F680) picks the STRONGEST of the five element levels and stores its index
        // as the weapon's SELECTED element:
        //     best = 0; for (i=1;i<5;i++) if (levels[best] < levels[i]) best = i;  record[0x16] = best;
        // Every weapon has this, slingshots included. Note the index is 0 (Fire) when the weapon has NO element
        // at all — the loop starts at 0 and only moves on a strict >, so ALWAYS check the LEVEL before trusting
        // the index. A hit carries the element as BITS (1 << index): CheckDmg maps 0x01→Fire, 0x02→Ice,
        // 0x04→Thunder, 0x08→Wind, 0x10→Holy, anything else → 5 = "no element".
        internal const int  SelectedElementOffset = 0x16; // byte  — index 0..4 of the strongest element
        internal const int  ElementLevelsOffset   = 0x17; // 5 bytes — Fire, Ice, Thunder, Wind, Holy
        internal const int  ElementCount          = 5;
        // Effective (post-attachment, POST-CLAMP) stat block inside a WEAPON_HAVE, written by
        // WeaponAllValueSet (0x225B60): Attack+4 (cap MaxAttack), Endurance+6 (cap 99), Speed+8
        // (cap 99 = _DAT_00294178), Magic+10 (cap MaxMagic). Writing past the cap in the BATTLE copy
        // bypasses it for combat while the menu/inventory record still reads 99.
        internal const int  EffAttackOffset    = 0x04;
        internal const int  EffEnduranceOffset = 0x06;
        internal const int  EffSpeedOffset     = 0x08;
        internal const int  EffMagicOffset     = 0x0A;
        internal const int  StatCap            = 99;   // _DAT_00294178 / _DAT_00294174
        // Anti-enemy (slayer) BYTE array at record +0x1C, indexed by EnemyCategory (0=Dragon..9=Mage).
        // CheckDmg (docs/game-formulas.md §3.4): dmg += dmg × 0.015 × anti[category] — read LIVE per hit via
        // the collision entry's pointer (NowWeaponHave+0x1C), so writing the BATTLE copy changes damage
        // immediately and can exceed the 99 menu cap (byte max 255 ≈ ×4.8).
        internal const int  AntiArrayOffset    = 0x1C;
    }

    /// <summary>
    /// The equipped weapon's MODEL — the live <c>NowWeapon</c> object, its <c>CFrameVu1</c> tree, and the
    /// <c>dcol*</c> "damage-collision" frames that define melee REACH (full notes: docs/weapon-reach.md).
    /// The CFrame node offsets here duplicate <see cref="CFrameVu1"/>, the owner of that struct.
    /// </summary>
    internal static class WeaponModel
    {

        // Weapon model assets in data.dat: commenu/weapon/cXXwNN.chr (XX char, NN = WeaponList +0x48).
        // Heaven's Cloud = c01w14.chr (Toan, within-char idx 14).

        // ── Heaven's Cloud melee reach (see WhirlwindScale.Tick / HeavensCloud) ───────
        // RE'd from SCUS_971.11 (ToanKey_Play 0x241690): every Toan melee hit is SearchFrame(equippedModel,
        // "dcol1") -> CCollisionData::Set(pos, radius); the engine only ever uses the frame named "dcol1".
        // Reach is extended by scaling the whole blade mesh at runtime (see the "Runtime weapon-model SCALE"
        // block below) — the dcol hit frames are children of the mesh, so they grow with it.

        // The "dcol1" CFrame in the loaded weapon model (CFrameVu1 template; name = "dcol"+'1'+NUL, local-matrix
        // translation (X,Y,Z) at name+0xE8/+0xEC/+0xF0; Toan weapons are (0,0,Z), Z = the reach). Used by
        // WhirlwindScale.LocateWeaponDcol1 to read a weapon's dcol1 Z for sizing its whirl when it's not in ToanWeapons.
        internal const uint  DcolNameWord     = 0x6C6F6364; // "dcol" little-endian
        internal const byte  Dcol1Digit       = 0x31;       // '1' (the active hit-point frame)
        internal const int   DcolNameToLocalX = 0xE8;       // local-matrix X (Y at +0xEC, Z at +0xF0)
        internal const int   DcolNameToLocalZ = 0xF0;       // local-matrix Z (the reach)

        // ── Runtime weapon-model SCALE (visual blade + dcol collision, together) — CONFIRMED live ──
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
        internal const long NowWeaponPtr           = 0x202A34F0; // → native ptr to NowWeapon record
        internal const int  WeaponModelRootOffset  = 0xBC;       // NowWeapon + 0xBC → native ptr to model root CFrameVu1
        internal const int  Vu1LocalMatrixDiag0    = 0xB8;       // CFrameVu1 local 3x3 m00 (m11 +0x14=0xCC, m22 +0x28=0xE0)
        internal const int  Vu1LocalMatrixDiag1    = 0xCC;
        internal const int  Vu1LocalMatrixDiag2    = 0xE0;
        internal const int  Vu1LocalTransX         = 0xE8;       // CFrameVu1 local translation (Y +0xEC, Z +0xF0)
        internal const int  Vu1LocalTransZ         = 0xF0;
    }

    /// <summary>
    /// <c>CWeaponFx</c> — the engine's SWORD-SWING TRAIL, a 32-rib ribbon renderer. A single resident instance
    /// (sizeof 0x540), Stepped and Drawn UNCONDITIONALLY every frame from the dungeon battle loops.
    ///
    /// THE POINT: it is completely IDLE while playing as Xiao. The only thing that arms it (Set__13CWeaponEffect)
    /// is called from ToanKey_Play (7 sites) and UngagaKey_Play (3) — there is no Xiao caller. So for Xiao the
    /// object is dead weight that the engine still draws every frame, which makes it free real estate for a mod
    /// ribbon. No contention, nothing to restore mid-fight.
    ///
    /// HOW IT WORKS. Each "rib" is a PAIR of world points (an inner edge sampled from the weapon's dcol0 frame and
    /// an outer edge from dcol1). Draw stitches consecutive ribs into a quad strip — additive, gouraud, Z-write
    /// masked — emitting a quad between rib i-1 and rib i ONLY if both are active. So an inactive rib BREAKS the
    /// strip, which is what lets one 32-rib buffer carry several independent ribbons.
    ///
    /// HOW TO TAKE IT OVER (pure data):
    ///   MasterAlpha (+0x52C) = 0  → Step never samples the dcol frames and never advances Head, so it stops
    ///                               extending the ribbon and leaves the arrays entirely to us.
    ///   RibAlphaDecay (+0x534) = 0 → Step's per-rib fade loop becomes a no-op, so our Active flags are never
    ///                               cleared out from under us.
    /// Then write RibPos / RibAlpha / RibActive directly. Draw keeps rendering from those arrays every frame.
    /// Re-assert the two knobs periodically: EquipWeaponFrame (on any weapon equip / floor load) zeroes the
    /// active flags and rewrites the colours.
    ///
    /// GATE: Draw and Step both early-out if FrameA/FrameB (+0x00/+0x04) are null. EquipWeaponFrame points them
    /// at the equipped weapon's dcol0/dcol1 frames — every weapon model has those — so for a normal equipped
    /// weapon they are already valid and nothing needs writing.
    /// </summary>
    internal static class WeaponTrailFx
    {
        internal const long Base = 0x21E58F40;

        internal const int  FrameA        = 0x000; // CFrame* (dcol0) — Step/Draw early-out if this is null
        internal const int  FrameB        = 0x004; // CFrame* (dcol1)

        internal const int  RibCount      = 32;
        internal const int  RibStride     = 0x20;  // one rib = posA float4 then posB float4
        internal const int  RibPos        = 0x010; // + i*0x20 → posA float4 (+0x00) and posB float4 (+0x10)
        internal const int  RibPosBytes   = RibCount * RibStride;        // 0x400 — one contiguous block
        internal const int  RibAlpha      = 0x410; // + i*4 (float 0..255; drawn as a u8)
        internal const int  RibAlphaBytes = RibCount * 4;                // 0x80
        internal const int  RibActive     = 0x490; // + i*4 (int 0/1) — an inactive rib BREAKS the quad strip
        internal const int  RibActiveBytes= RibCount * 4;                // 0x80

        internal const int  ColorA0       = 0x510; // spRGBA of the posA edge (byte +3 = alpha, Draw overwrites it)
        internal const int  ColorB0       = 0x514; // spRGBA of the posB edge
        internal const int  ColorA1       = 0x518;
        internal const int  ColorB1       = 0x51C;

        internal const int  Head          = 0x520; // rib the engine would write next (unused once we take over)
        internal const int  MasterAlpha   = 0x52C; // float — >0 means "a swing is live"; ZERO IT to take over
        internal const int  MasterDecay   = 0x530; // float — master-alpha drain per frame
        internal const int  RibAlphaDecay = 0x534; // float — per-rib fade per frame; ZERO IT so our ribs persist
    }

    /// <summary>
    /// The weapon MENU / <c>CWeaponLevelUp</c> flow — which weapon and character the menu has selected, the
    /// level-up vs status-break flow state, and the vanilla status-break transfer factor.
    /// NOTE <see cref="StatusBreakFactorFloat"/> is SHARED with cloth physics — retarget the load, never edit the
    /// float in place.
    /// </summary>
    internal static class WeaponMenu
    {
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

        // ⚠ PINE writes to EE CODE pages crash PCSX2 (docs/code-caves.md § PINE write safety): everything in
        // this class is poked as DATA only. The 7 Branch Sword status-break effect is applied post-hoc on the
        // sphere (AttachBoard below + SevenBranchSword.SevenfoldRiteEffect), not by patching
        // WeaponStatusBreakEnable/SetStatusBreak.

        /// <summary>The status-break stat-transfer factor: a DATA float (0.6f) at native 0x2A1890,
        /// read live by SetStatusBreak (ELF 0x2368D0) at confirm time — poking it is safe (data,
        /// not code). SHARED with CCloth::Step (cape physics), FishLineStep and BtSystemScriptInit,
        /// so it is swapped to 0.77 only while a 7 Branch Sword is the selected menu weapon and
        /// restored otherwise. Menus freeze the world, so the main exposure is the stale-selection
        /// window after closing the menu (cape/fishline running with 0.77) — logged for evaluation.</summary>
        internal const long  StatusBreakFactorFloat   = 0x202A1890;
        internal const float StatusBreakFactorDefault = 0.6f;

        /// <summary>The game's low-durability warning threshold: the HUD WHP gauge blinks while
        /// <c>WHP &lt;= LowWhpWarningFraction * maxWHP</c> (<c>DrawWepDamageDraw</c> ELF 0x1F8D30,
        /// float constant at native 0x2A1870). Used by the Maneater drain to match the visible state.</summary>
        internal const float LowWhpWarningFraction = 0.1f;
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
        /// characters' weapon arrays: 0x450C + 6×0xAA8 = 0x84FC) = <see cref="Addresses.firstBagAttachment"/>.</summary>
        internal const long Base      = Addresses.firstBagAttachment; // 0x21CE1A48
        internal const int  Stride    = 0x20;
        internal const int  ScanCount = PlayerAddresses.InventorySizeAttachments + 2; // 42, mirrors Inventory.GetBagAttachments
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
    /// The kill-ABS grant path (VANILLA), RE'd from the CMonstorUnit::Step death block (the GetWeaponMaxExp
    /// call at ELF 0x1DF1CC). On an enemy slot's release frame (slot field +0x00 == -1) the engine grants the
    /// slot's ABS reward to the ACTIVE character's equipped inventory record (see
    /// <see cref="DngStatusData.WeaponRecord"/>), gated on (slot.KillerCharId == active char) and the equipped
    /// weapon not being a default weapon.
    ///
    /// Grant modifiers: ×2 when the back-floor flag (<see cref="BackFloorDoubleAddr"/>) is set;
    /// ×<see cref="AbsBonusMult"/> when the slot's status-flag word has <see cref="AbsBonusFlag"/> set.
    ///
    /// THE KEY BEHAVIOR: the whole grant is SKIPPED when abs &gt;= GetWeaponMaxExp — a crossing kill clamps to
    /// exactly max and queues the "ABS MAX" popup; above max the engine is fully inert. Max ABS is never
    /// stored: it is COMPUTED per call as table base (+0x30, SIGNED CHAR) + level × step (+0x32, short),
    /// clamped to 1..999 (the live WeaponTable.Abs / WeaponTable.AbsAdd columns — which is also why the table
    /// can't just be doubled: base values run up to 125, so 2× overflows the signed char for ~54 weapons).
    /// Special cases in the same block: Serpent Sword (id 268) grants nothing until game flag 0x30 is set, and
    /// while the player is monster-transformed (<see cref="DngStatusData.TransformStateOffset"/> == 10) kills
    /// DRAIN abs instead of granting.
    ///
    /// Backs MachoSword.OvertrainingEffect ("Overtraining"), which owns the rollover POLICY — this class
    /// holds only what the game itself does.
    /// </summary>
    internal static class AbsRewards
    {
        internal const long BackFloorDoubleAddr  = EnemyAddresses.MainMonstorUnit.Base + 0x44; // int; nonzero = kill ABS ×2 (back floors)
        // Per-slot status-flag word (same 0x510-stride family as the collision arrays).
        internal const long SlotStatusFlagsBase  = EnemyAddresses.MainMonstorUnit.Base + 0x55754;
        internal const int  SlotStatusFlagsStride = 0x510;
        internal const int  AbsBonusFlag         = 0x2000;     // slot flag: ABS reward ×AbsBonusMult
        internal const float AbsBonusMult        = 1.2f;       // DAT_002a1af8

        internal static long SlotStatusFlagsAddr(int slot) => SlotStatusFlagsBase + (long)slot * SlotStatusFlagsStride;
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
