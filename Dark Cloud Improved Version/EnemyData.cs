using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    internal enum EnemyCategory : ushort
    {
        Dragon = 0,
        Undead = 1,
        Marine = 2,
        Rock   = 3,
        Plant  = 4,
        Beast  = 5,
        Sky    = 6,
        Metal  = 7,
        Mimic  = 8,
        Mage   = 9,
    }

    /// <summary>
    /// Known data for a single enemy species, mirroring the in-game slot layout.
    /// Null fields have not yet been confirmed from a slot dump or observed in gameplay.
    /// Resistance scale: 100 = neutral, &gt;100 = weak, &lt;100 = resistant, 0 = immune.
    /// </summary>
    internal struct EnemyDefaults
    {
        internal ushort Id;
        internal string Name;
        // Physical index of this entry's record in the static enemy species table (EnemySpeciesTable).
        // Use EnemySpeciesTable.RecordAddress(TableIndex) to compute the RAM address.
        // EIDs are NOT stored sequentially in the table — TableIndex != Id - 1.
        // Null means the index has not been confirmed from the extracted data.
        internal int?   TableIndex;
        internal string ModelCode;    // char[4] from EnemySpeciesTable.ModelCode (0x000); null for cut enemies with no table entry
        internal int?   MaxHp;
        internal int?   Abs;
        internal int?   MinGoldDrop;
        internal int?   DropChance;   // 0–100

        // Elemental resistances (read as signed short).
        // Slot (EnemySlotOffsets): packed as 3 ints of 2 ushorts at 0x028/0x02C/0x030.
        // Static table (EnemySpeciesTable): individual shorts at 0x000–0x00A.
        // Scale: <0=absorbs (heals), 0=immune, 100=neutral, >100=weak. Absorb stored as two's-complement (e.g. -50 = 0xFFCE).
        internal EnemyCategory? Category; // slot +0x028 low  — CONFIRMED enemy category
        internal ushort? FireRes;     // slot +0x028 high — CONFIRMED fire
        internal ushort? IceRes;      // slot +0x02C low  — CONFIRMED ice
        internal ushort? ThunderRes;  // slot +0x02C high — CONFIRMED thunder
        internal ushort? WindRes;     // slot +0x030 low  — CONFIRMED wind
        internal ushort? HolyRes;     // slot +0x030 high — CONFIRMED holy

        // Slot +0x044: CONFIRMED entity scale / collision radius. Single value reused by multiple
        // engine systems: elemental hit effect spawn radius, AI pathfinding clearance, and likely
        // environment collision. Reducing below default visibly shrinks elemental effects; inflating
        // to 100 causes teleporting (engine resolves collision overlap to nearest valid position) and
        // movement lock (pathfinding can't route a body that large).
        // Slot +0x044 (primary) equals +0x048 (copy) at spawn. Static table: EnemySpeciesTable.EntityScale (0x060).
        internal float? EntityScale;
        internal float? EntityScaleCopy;

        // Enemy DEFENSE pair (slot +0x090 / record 0x064+0x066), loaded at spawn. CONFIRMED 2026-06-19 as the
        // real per-dungeon durability scalers (see EnemyAddresses.EnemySpeciesTable.DamageReduction/WeaponDefense):
        //   DamageReduction = flat reduction subtracted from incoming damage (CheckDmg).
        //   WeaponDefense   = weapon-damage defense, fed to SwordDmgCheck1.
        // (Formerly DamageReduction/WeaponDefense.) These are NOT attack — actual hit damage is STB-script-driven.
        internal ushort? DamageReduction;   // record 0x064 / slot DefenseStats low
        internal ushort? WeaponDefense;     // record 0x066 / slot DefenseStats high

        // Slot +0x0D8: steal item ID — low ushort is the item ID; 65535 if none.
        // Static table: EnemySpeciesTable.StealItemId (0x080) / EnemySpeciesTable.StealFlag (0x082).
        internal ushort? StealItemId;

        // Slot +0x0DC: packed ushorts — semantics unconfirmed. Scale resembles elemental resistances (100=neutral, <100=resistant).
        // Static table: EnemySpeciesTable.ItemResA (0x084) / EnemySpeciesTable.ItemResB (0x086).
        internal ushort? ItemResA;   // slot +0x0DC low
        internal ushort? ItemResB;   // slot +0x0DC high

        // +0x110: CONFIRMED controls horizontal width of the lock-on reticle.
        // +0x114: CONFIRMED lock-on reticle height. May double as hitbox height.
        // Neither appears to control enemy movement speed directly.
        internal float? ReticleWidth;
        internal float? ReticleHeight;

        // Static table +0x034/+0x036–+0x040: base melee attack power and elemental output multipliers.
        // AttackPower: null = boss (65535 sentinel uses behavior-script attacks only) or unconfirmed.
        // ElemAtk*: Same element order as the resistance fields (fire, ice, thunder, wind, holy, dark?). 100=neutral. >100=strong; <100=weak.
        internal ushort? AttackPower;
        internal ushort? ElemAtkFire;
        internal ushort? ElemAtkIce;
        internal ushort? ElemAtkThunder;
        internal ushort? ElemAtkWind;
        internal ushort? ElemAtkHoly;
        internal ushort? ElemAtkDark;

        // Per-attack hit damage decoded from this species' behavior script (dun/monstor/<ModelCode>.stb),
        // mirrored from enemy-attack-damage-table.md. ACTUAL hit damage = baseDamage − playerDefense; baseDamage
        // is these script constants (the static-table AttackPower above is NOT the real damage — see
        // EnemyAddresses.cs / memory enemy-attack-damage-system). Used by the stat-normalization gradient.
        //   MeleeDamage:      arg0 of each _SET_DMG_PARA (STB cmd 0x84), in STB-walk order.
        //   ProjectileDamage: 5th arg of each _SET_SHOT/_SET_SHOT2 (cmd 0x85/0xE5), in STB-walk order;
        //                     -1 = omitted (engine default, do not rescale).
        // Order is preserved so each array entry maps positionally onto its STB occurrence at patch time.
        // null = no .stb / cut enemy; empty = script has no attack command (7 non-attackers, e.g. Ice Queen).
        internal int[] MeleeDamage;
        internal int[] ProjectileDamage;

        // Model scale table (base 0x21E18530, stride 0x3510 per slot) — separate from enemy slot.
        // "BODY SIZE" triple, set from the MODEL file's info.cfg `BODY_SIZE height,width,depth` line via
        // CommandBODY_SIZE (RE'd 2026-06-20; see ModelScaleOffsets.BodyWidth/BodyHeight/BodyDepth). Read live (not cached at spawn).
        // +0x020: body WIDTH / girth radius (7 small → 14 Gunny → 32 Prajna) — CCharacter::PickUpPoly floor-poly
        //         pickup radius (min 2.0, terrain collision) + _GET_NPC_BODY_SIZE script getter. No observable effect.
        internal float? BodyWidth;
        // +0x024: body HEIGHT — confirmed in-game: GetScrPosFromChar anchors the off-lock marker/name above the body.
        internal float? BodyHeight;
        // +0x028: body DEPTH (60 ground, 0 ranged/flying) — only the _GET_NPC_BODY_SIZE script getter; gameplay-inert.
        internal float? BodyDepth;
        // +0x048: enemy-specific int (474–1454); likely mesh or animation data count.
        internal int?   ModelDataSize;
        // +0x370: animation clip cap — setting below true count freezes higher-index animations (attacks); Setting above true count does not change default behavior.
        internal int?   ModelAnimCount;
    }

    internal struct BehaviorScript
    {
        internal int    Index;
        internal string ScriptName;       // 16-byte ASCII identifier from ELF (informational only)
        internal int    BehaviorMode;     // 0 = attack, 2 = repositioning
        internal float  HitboxWidth;
        internal float  HitboxHeight;
        internal float  HitboxDepth;
        internal float  TriggerRange;
        internal float  ReachRange;
        internal float  SecondaryRange;
        internal int    DurationFrames;
        internal int    AttackDistance;
        internal int    BehaviorFlags;
        internal int    PhaseCount;
        internal int    ScriptMode;
        internal int    PackedFlags2;
        internal float  ProjectileSpeed;
        internal int    ProjectileLifetime;

        internal int RamAddress => BehaviorScriptTable.Base + Index * BehaviorScriptTable.Stride;
        internal int FieldAddress(int fieldOffset) => RamAddress + fieldOffset;
    }

    /// <summary>
    /// Default values for all 34 behavior script records as they appear in the base-game ELF.
    /// Names are 16-byte ASCII identifiers embedded in the data segment.
    /// Entries [25]–[33] are additionally referenced by the pointer array at BehaviorScriptTable.PointerArray.
    /// </summary>
    internal static class EnemyBehaviorScripts
    {
        // Index 0 — "gas_h": thin horizontal gas cloud.
        // Tiny hitbox (0.1×0.4×0.1), BehaviorFlags=0x0200. Short dist=26.
        // Likely a slow-drifting horizontal poison or slow-gas puff from a plant or bug enemy.
        internal static readonly BehaviorScript GasH = new BehaviorScript {
            Index=0, ScriptName="gas_h",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=0.4f, HitboxDepth=0.1f,
            TriggerRange=2.3f, ReachRange=7.0f, SecondaryRange=4.3f,
            DurationFrames=220, AttackDistance=26, BehaviorFlags=0x0200,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 1 — "gas_d": downward-drifting gas cloud.
        // Slightly wider hitbox (0.3×0.9×0.1), BehaviorFlags=0x0200. Paired with gas_h.
        // Likely falls downward from ceilings or lobbed in an arc vs. gas_h's lateral drift.
        internal static readonly BehaviorScript GasD = new BehaviorScript {
            Index=1, ScriptName="gas_d",
            BehaviorMode=0, HitboxWidth=0.3f, HitboxHeight=0.9f, HitboxDepth=0.1f,
            TriggerRange=2.3f, ReachRange=7.0f, SecondaryRange=4.3f,
            DurationFrames=190, AttackDistance=26, BehaviorFlags=0x0200,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 2 — "nebaneba": regular sticky/slimy field.
        // "nebaneba" (ねばねば) = sticky/slimy; wider hitbox (0.8×1.4×0.1) than the boss variant.
        // BehaviorFlags=0x0800, very short AttackDistance=5. Regular enemy slow field.
        internal static readonly BehaviorScript NebanebaField = new BehaviorScript {
            Index=2, ScriptName="nebaneba",
            BehaviorMode=0, HitboxWidth=0.8f, HitboxHeight=1.4f, HitboxDepth=0.1f,
            TriggerRange=2.6f, ReachRange=5.0f, SecondaryRange=4.3f,
            DurationFrames=180, AttackDistance=5, BehaviorFlags=0x0800,
            PhaseCount=2, ScriptMode=65536, PackedFlags2=131074,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 3 — "pump_bom": pump bomb projectile.
        // Small hitbox (0.1×1.4), slow ProjectileSpeed=0.4, lifetime=8 frames.
        internal static readonly BehaviorScript PumpBomb = new BehaviorScript {
            Index=3, ScriptName="pump_bom",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=1.4f, HitboxDepth=0f,
            TriggerRange=1.3f, ReachRange=2.0f, SecondaryRange=1.3f,
            DurationFrames=150, AttackDistance=60, BehaviorFlags=0,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0.4f, ProjectileLifetime=8 };

        // Index 4 — "ringo_ex": apple / fruit throw (extended).
        // "ringo" (りんご) = apple in Japanese. (0.2×1.5) hitbox, plt=6 frames, BehaviorFlags=0x0200.
        // Likely FliFli's (plant EID=8) fruit-toss special attack.
        internal static readonly BehaviorScript AppleThrow = new BehaviorScript {
            Index=4, ScriptName="ringo_ex",
            BehaviorMode=0, HitboxWidth=0.2f, HitboxHeight=1.5f, HitboxDepth=0f,
            TriggerRange=1.6f, ReachRange=3.0f, SecondaryRange=4.3f,
            DurationFrames=160, AttackDistance=30, BehaviorFlags=0x0200,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=6 };

        // Index 5 — "f_boll_3" (short/slow variant): slow fireball 3.
        // Smaller hitbox (0.1×1.5) and slower speed (0.5) than the boss variant at index 27.
        // plt=16 frames, SecondaryRange=15.0 (wide follow-through). Flags=0x0001.
        // Likely the regular-enemy fireball 3, or a slower, shorter-lived version.
        internal static readonly BehaviorScript Fireball3Short = new BehaviorScript {
            Index=5, ScriptName="f_boll_3",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=1.5f, HitboxDepth=0f,
            TriggerRange=1.7f, ReachRange=3.8f, SecondaryRange=15.0f,
            DurationFrames=120, AttackDistance=17, BehaviorFlags=1,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0.5f, ProjectileLifetime=16 };

        // Index 6 — "awabres": bubble burst attack.
        // "awa" (泡) = bubble; "bres" likely = burst/breath. (0.1×0.8×0.1), BehaviorFlags=0x0200.
        // Larger TriggerRange=4.0 suggests it fires when slightly farther away.
        internal static readonly BehaviorScript BubbleBurst = new BehaviorScript {
            Index=6, ScriptName="awabres",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=0.8f, HitboxDepth=0.1f,
            TriggerRange=4.0f, ReachRange=6.0f, SecondaryRange=6.0f,
            DurationFrames=160, AttackDistance=45, BehaviorFlags=0x0200,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 7 — "g_wave1": ground wave 1 (narrow).
        // Narrow (0.1×1.4) but fast (pspd=1.2), 3 phases. Low duration=90 frames.
        // Likely a ground shockwave from a stomp or slam — smaller/faster first variant.
        internal static readonly BehaviorScript GroundWave1 = new BehaviorScript {
            Index=7, ScriptName="g_wave1",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=1.4f, HitboxDepth=0f,
            TriggerRange=4.3f, ReachRange=9.0f, SecondaryRange=6.3f,
            DurationFrames=90, AttackDistance=34, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.2f, ProjectileLifetime=0 };

        // Index 8 — "g_wave2": ground wave 2 (wide AoE).
        // Wide (2.1×3.4×1.0) and very fast (pspd=5.0); 3 phases. Covers large area.
        // Likely DarkGenie's floor-wave or a boss ground pound that radiates outward.
        internal static readonly BehaviorScript GroundWave2 = new BehaviorScript {
            Index=8, ScriptName="g_wave2",
            BehaviorMode=0, HitboxWidth=2.1f, HitboxHeight=3.4f, HitboxDepth=1.0f,
            TriggerRange=6.3f, ReachRange=9.0f, SecondaryRange=8.3f,
            DurationFrames=100, AttackDistance=66, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=5.0f, ProjectileLifetime=0 };

        // Index 9 — "magic_noroi": curse magic.
        // "noroi" (呪い) = curse. (1.1×1.8), BehaviorFlags=0x0400; medium range.
        // Likely a debuff projectile — slow/weaken effect on contact.
        internal static readonly BehaviorScript CurseMagic = new BehaviorScript {
            Index=9, ScriptName="magic_noroi",
            BehaviorMode=0, HitboxWidth=1.1f, HitboxHeight=1.8f, HitboxDepth=0f,
            TriggerRange=1.9f, ReachRange=2.3f, SecondaryRange=5.3f,
            DurationFrames=160, AttackDistance=16, BehaviorFlags=0x0400,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 10 — "magic_bin": bind magic.
        // "bin" may relate to binding/freezing. (0.8×1.5), BehaviorFlags=0x1000, AttackDistance=0.
        // Zero attack distance suggests an on-contact root effect rather than a projectile.
        internal static readonly BehaviorScript BindMagic = new BehaviorScript {
            Index=10, ScriptName="magic_bin",
            BehaviorMode=0, HitboxWidth=0.8f, HitboxHeight=1.5f, HitboxDepth=0f,
            TriggerRange=2.9f, ReachRange=2.9f, SecondaryRange=5.3f,
            DurationFrames=180, AttackDistance=0, BehaviorFlags=0x1000,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 11 — "magic_isi": stone/rock magic.
        // "isi" (石) = stone in Japanese. Fast ProjectileSpeed=5.0 despite no plt; flags=0x0100.
        // Likely a launched rock or crystal that travels quickly toward the player.
        internal static readonly BehaviorScript StoneMagic = new BehaviorScript {
            Index=11, ScriptName="magic_isi",
            BehaviorMode=0, HitboxWidth=1.1f, HitboxHeight=1.9f, HitboxDepth=0f,
            TriggerRange=1.8f, ReachRange=2.0f, SecondaryRange=5.3f,
            DurationFrames=160, AttackDistance=5, BehaviorFlags=0x0100,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=5.0f, ProjectileLifetime=0 };

        // Index 12 — "magic_s": small generic magic.
        // Moderate hitbox (1.8×2.8), 3 phases, plt=10 frames. BehaviorFlags=0.
        // General-purpose small magic spell — may be used for fire, ice, or holy projectiles.
        internal static readonly BehaviorScript MagicSmall = new BehaviorScript {
            Index=12, ScriptName="magic_s",
            BehaviorMode=0, HitboxWidth=1.8f, HitboxHeight=2.8f, HitboxDepth=0f,
            TriggerRange=4.5f, ReachRange=5.2f, SecondaryRange=5.3f,
            DurationFrames=140, AttackDistance=50, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=10 };

        // Index 13 — "f_boll_2": fireball variant 2.
        // Tall thin hitbox (0.1×2.5), pspd=1.5, plt=20; SecondaryRange=20.0 (very wide follow-through).
        // BehaviorFlags=0x0001. Intermediate fireball — between the slow variant 3-short and the fast one.
        internal static readonly BehaviorScript Fireball2 = new BehaviorScript {
            Index=13, ScriptName="f_boll_2",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=2.5f, HitboxDepth=0f,
            TriggerRange=0.5f, ReachRange=4.0f, SecondaryRange=20.0f,
            DurationFrames=180, AttackDistance=35, BehaviorFlags=1,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.5f, ProjectileLifetime=20 };

        // Index 14 — "seedshot": seed-shot projectile.
        // Tall, very thin hitbox (0.1×5.5×0.3); 4 phases (most in table). Small TriggerRange=0.3.
        // Likely FliFli or a plant enemy's rapid seed burst — many narrow pellets.
        internal static readonly BehaviorScript SeedShot = new BehaviorScript {
            Index=14, ScriptName="seedshot",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=5.5f, HitboxDepth=0.3f,
            TriggerRange=0.3f, ReachRange=3.5f, SecondaryRange=0.3f,
            DurationFrames=180, AttackDistance=37, BehaviorFlags=0,
            PhaseCount=4, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 15 — "g_canon": ground cannon.
        // Wide hitbox (3.5×5.0), slow pspd=0.8, 3 phases. BehaviorFlags=0.
        // Large slow-moving cannon ball rolling along the floor — high-damage close AoE.
        internal static readonly BehaviorScript GroundCannon = new BehaviorScript {
            Index=15, ScriptName="g_canon",
            BehaviorMode=0, HitboxWidth=3.5f, HitboxHeight=5.0f, HitboxDepth=0f,
            TriggerRange=1.9f, ReachRange=5.2f, SecondaryRange=6.3f,
            DurationFrames=120, AttackDistance=42, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0.8f, ProjectileLifetime=0 };

        // Index 16 — "zibaku_f2": self-destruct fire variant 2.
        // "zibaku" (自爆) = self-destruct. Zero hitbox — triggers by proximity (TriggerRange=14.0).
        // pspd=1.0, plt=40 frames; BehaviorFlags=0x0001. Fire-type AoE detonation.
        internal static readonly BehaviorScript SelfDestructFire2 = new BehaviorScript {
            Index=16, ScriptName="zibaku_f2",
            BehaviorMode=0, HitboxWidth=0f, HitboxHeight=0f, HitboxDepth=0f,
            TriggerRange=14.0f, ReachRange=14.5f, SecondaryRange=10.3f,
            DurationFrames=110, AttackDistance=59, BehaviorFlags=1,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.0f, ProjectileLifetime=40 };

        // Index 17 — "zibaku_r2": self-destruct roll variant 2.
        // Same proximity trigger (14.0), slightly wider SecondaryRange=14.3. BehaviorFlags=0x0002.
        // Roll-type detonation — distinct hit response from the fire variant.
        internal static readonly BehaviorScript SelfDestructRoll2 = new BehaviorScript {
            Index=17, ScriptName="zibaku_r2",
            BehaviorMode=0, HitboxWidth=0f, HitboxHeight=0f, HitboxDepth=0f,
            TriggerRange=14.0f, ReachRange=14.5f, SecondaryRange=14.3f,
            DurationFrames=110, AttackDistance=60, BehaviorFlags=2,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.0f, ProjectileLifetime=40 };

        // Index 18 — "zibaku_t2": self-destruct tail variant 2.
        // Same trigger radius, shorter duration (20 frames) but higher dist=100. BehaviorFlags=0x0004.
        // Tail-sweep detonation — different angle coverage. plt=50 (longest of the three).
        internal static readonly BehaviorScript SelfDestructTail2 = new BehaviorScript {
            Index=18, ScriptName="zibaku_t2",
            BehaviorMode=0, HitboxWidth=0f, HitboxHeight=0f, HitboxDepth=0f,
            TriggerRange=14.0f, ReachRange=14.5f, SecondaryRange=10.3f,
            DurationFrames=20, AttackDistance=100, BehaviorFlags=4,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.0f, ProjectileLifetime=50 };

        // Index 19 — "fuki_ex": exhale/breath extended.
        // "fuki" (吹き) = blow/exhale. Large AoE (4.0×4.5), BehaviorFlags=0x0200, TriggerRange=7.6.
        // Likely a boss breath attack covering a wide cone. Same dimensions as DarkGenieForm2Attack.
        internal static readonly BehaviorScript ExhaleExtended = new BehaviorScript {
            Index=19, ScriptName="fuki_ex",
            BehaviorMode=0, HitboxWidth=4.0f, HitboxHeight=4.5f, HitboxDepth=0f,
            TriggerRange=7.6f, ReachRange=5.5f, SecondaryRange=5.5f,
            DurationFrames=180, AttackDistance=31, BehaviorFlags=0x0200,
            PhaseCount=2, ScriptMode=65536, PackedFlags2=131074,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 20 — "i_boll": ice ball.
        // Narrow (0.1×1.7), BehaviorFlags=0x0002, SecondaryRange=9.0. IceQueen or ice-type enemy special.
        internal static readonly BehaviorScript IceBall = new BehaviorScript {
            Index=20, ScriptName="i_boll",
            BehaviorMode=0, HitboxWidth=0.1f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=1.9f, ReachRange=6.0f, SecondaryRange=9.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=2,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 21 — "mikazuki_ex": crescent-moon extended.
        // "mikazuki" (三日月) = crescent moon. (0.8×1.7), BehaviorFlags=0x0004, ReachRange=6.0.
        // Likely a scythe or blade arc — smaller than the full engetu sweep (index 30).
        internal static readonly BehaviorScript CrescentExtended = new BehaviorScript {
            Index=21, ScriptName="mikazuki_ex",
            BehaviorMode=0, HitboxWidth=0.8f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=1.6f, ReachRange=6.0f, SecondaryRange=6.0f,
            DurationFrames=120, AttackDistance=70, BehaviorFlags=4,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 22 — "b_boll": dark/bomb ball.
        // Wider hitbox (1.2×1.8), BehaviorFlags=0x0100, ReachRange=8.0. Likely a dark-energy orb
        // or a larger explosive ball distinct from the fire/ice/thunder variants.
        internal static readonly BehaviorScript DarkBall = new BehaviorScript {
            Index=22, ScriptName="b_boll",
            BehaviorMode=0, HitboxWidth=1.2f, HitboxHeight=1.8f, HitboxDepth=0f,
            TriggerRange=1.6f, ReachRange=8.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=70, BehaviorFlags=0x0100,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 23 — "t_boll": thunder ball.
        // (1.4×1.7), BehaviorFlags=0x0004. Likely the thunder-type elemental orb.
        internal static readonly BehaviorScript ThunderBall = new BehaviorScript {
            Index=23, ScriptName="t_boll",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=4,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 24 — "e114a_ex": MasterUtan extended attack.
        // "e114" prefix matches MasterUtan (EID=114). Same hitbox as e115a_ex; BehaviorFlags=0x0008.
        // Likely MasterUtan's special vine or slam attack, one flag-bit below KingsCurseCoffin.
        internal static readonly BehaviorScript MasterUtanAttack = new BehaviorScript {
            Index=24, ScriptName="e114a_ex",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=8,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-65535,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // ---- pointer-array entries [25]–[33] — directly referenced by BehaviorScriptTable.PointerArray ----

        // Index 25 — "e115a_ex": KingsCurseCoffin extended melee attack.
        // "e115" prefix matches KingsCurseCoffin (EID=115). BehaviorFlags=0x0010.
        // Likely the claw/arm-grab special that deals high damage at medium range.
        internal static readonly BehaviorScript KingsCurseCoffinAttack = new BehaviorScript {
            Index=25, ScriptName="e115a_ex",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=16,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-65535,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 26 — "last_gw2": last-dungeon ranged projectile.
        // "gw2" likely = Genie World 2; final boss ranged special.
        // ProjectileSpeed=1.5, ProjectileLifetime=20; AttackDistance=130 (longest ranged reach).
        internal static readonly BehaviorScript LastDungeonProjectile = new BehaviorScript {
            Index=26, ScriptName="last_gw2",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=130, BehaviorFlags=1,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.5f, ProjectileLifetime=20 };

        // Index 27 — "f_boll_3" (boss variant): fireball 3 with wider flags.
        // BehaviorFlags=0x0401 (1 | 0x0400) adds extra hit effect vs. index-5 variant.
        // Same projectile params as last_gw2 but shorter AttackDistance=58. Used by a fire-type boss.
        internal static readonly BehaviorScript Fireball3 = new BehaviorScript {
            Index=27, ScriptName="f_boll_3",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=1025,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=1.5f, ProjectileLifetime=20 };

        // Index 28 — "e118a_Ex": DarkGenie Form2 extended AoE.
        // "e118" matches DarkGenie Form2 (EID=118). Large hitbox (4.0×4.5), TriggerRange=7.6.
        // Sweeping arm or ground-slam covering a wide area around the boss.
        internal static readonly BehaviorScript DarkGenieForm2Attack = new BehaviorScript {
            Index=28, ScriptName="e118a_Ex",
            BehaviorMode=0, HitboxWidth=4.0f, HitboxHeight=4.5f, HitboxDepth=0f,
            TriggerRange=7.6f, ReachRange=5.5f, SecondaryRange=5.5f,
            DurationFrames=180, AttackDistance=51, BehaviorFlags=0,
            PhaseCount=2, ScriptMode=65536, PackedFlags2=131074,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 29 — "nebaneba_b": boss sticky/sludge AoE.
        // Tiny hitbox (0.4×0.4×0.1), BehaviorFlags=0x0800. Boss variant of nebaneba (index 2).
        // Very short AttackDistance=15 — close-range grab or sludge pool.
        internal static readonly BehaviorScript NebanebaSludge = new BehaviorScript {
            Index=29, ScriptName="nebaneba_b",
            BehaviorMode=0, HitboxWidth=0.4f, HitboxHeight=0.4f, HitboxDepth=0.1f,
            TriggerRange=2.6f, ReachRange=5.0f, SecondaryRange=4.3f,
            DurationFrames=180, AttackDistance=15, BehaviorFlags=2048,
            PhaseCount=2, ScriptMode=65536, PackedFlags2=131074,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 30 — "engetu": crescent-moon sweep attack.
        // "engetu" (円月) = crescent moon. Very long ReachRange=13.0, AttackDistance=150.
        // Large arc (3.4×3.7). Likely MinotaurJoe's scythe swing or a sword-boss long slash.
        internal static readonly BehaviorScript CrescentSweep = new BehaviorScript {
            Index=30, ScriptName="engetu",
            BehaviorMode=0, HitboxWidth=3.4f, HitboxHeight=3.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=13.0f, SecondaryRange=4.3f,
            DurationFrames=120, AttackDistance=150, BehaviorFlags=0,
            PhaseCount=2, ScriptMode=65535, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 31 — "dash": charging dash attack.
        // Longest DurationFrames=240 and AttackDistance=170; three phases; medium hitbox (2.6×2.4).
        // Likely a boss charging straight at Toan.
        internal static readonly BehaviorScript Dash = new BehaviorScript {
            Index=31, ScriptName="dash",
            BehaviorMode=0, HitboxWidth=2.6f, HitboxHeight=2.4f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=5.0f, SecondaryRange=4.3f,
            DurationFrames=240, AttackDistance=170, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=65536, PackedFlags2=131074,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 32 — "kamai": long-range stance/repositioning (movement mode).
        // "kamai" (構え) = stance/posture. BehaviorMode=2; TriggerRange=30.0 (very wide).
        // Enemy circles or backs up to maintain preferred engagement distance. Three phases.
        internal static readonly BehaviorScript Stance = new BehaviorScript {
            Index=32, ScriptName="kamai",
            BehaviorMode=2, HitboxWidth=0f, HitboxHeight=0f, HitboxDepth=0f,
            TriggerRange=30.0f, ReachRange=0f, SecondaryRange=0f,
            DurationFrames=120, AttackDistance=130, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=-65536, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // Index 33 — "terepo": short-range teleport repositioning (movement mode).
        // BehaviorMode=2; TriggerRange=10.0 — defensive blink when the player gets close.
        // "terepo" = teleport in Japanese. Smaller trigger than kamai — used when cornered.
        internal static readonly BehaviorScript Teleport = new BehaviorScript {
            Index=33, ScriptName="terepo",
            BehaviorMode=2, HitboxWidth=0f, HitboxHeight=0f, HitboxDepth=0f,
            TriggerRange=10.0f, ReachRange=0f, SecondaryRange=0f,
            DurationFrames=120, AttackDistance=90, BehaviorFlags=0,
            PhaseCount=3, ScriptMode=-65536, PackedFlags2=-1,
            ProjectileSpeed=0f, ProjectileLifetime=0 };
    }

    /// <summary>Helpers for reading and writing packed elemental resistances.</summary>
    internal static class EnemyResistances
    {
        internal static (ushort category, ushort fire, ushort ice, ushort thunder, ushort wind, ushort holy)
            Read(int slotBase)
        {
            int p1 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1);
            int p2 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack2);
            int p3 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack3);
            return ((ushort)(p1 & 0xFFFF), (ushort)(p1 >> 16),
                    (ushort)(p2 & 0xFFFF), (ushort)(p2 >> 16),
                    (ushort)(p3 & 0xFFFF), (ushort)(p3 >> 16));
        }

        internal static void Write(int slotBase, ushort category, ushort fire, ushort ice, ushort thunder, ushort wind, ushort holy)
        {
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack1, (fire  << 16) | category);
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack2, (thunder << 16) | ice);
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack3, (holy  << 16) | wind);
        }

        internal static ushort ReadFire(int slotBase)
            => (ushort)(Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1) >> 16);
    }

    /// <summary>
    /// Default stats per enemy species. Named static fields mirror the Fish pattern.
    /// Populated from unmodified floor dumps — null fields are unconfirmed.
    /// Never populate defaults from miniboss slot data; minibosses share the same species ID
    /// as their normal counterpart but have different stats.
    /// </summary>
    internal static class EnemySpecies
    {
        // Per-species animation / motion lists (decoded from each model's <code>.chr info.cfg KEY block) live in
        // /enemy-motion-table.md, ordered by TableIndex. "死亡" marks the death/collapse motion used by
        // EnemyModelInjector.BossScriptPatcher.CollapseMotion. See the datadat-index-and-chr-motions note.

        internal static readonly EnemyDefaults MasterJacket = new EnemyDefaults {
            Id=1, TableIndex=0, ModelCode="e01a",  Name="Master Jacket",    MaxHp=75,  Abs=5,  MinGoldDrop=7,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80,  ThunderRes=100, WindRes=80,  HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=177, ItemResA=100, ItemResB=80,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1123, ModelAnimCount=20, AttackPower=150, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,30}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SkeletonSoldier = new EnemyDefaults {
            Id=3, TableIndex=1, ModelCode="e03a",  Name="Skeleton Soldier", MaxHp=23,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=null, ItemResA=100, ItemResB=90,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ModelDataSize=1080, ModelAnimCount=19, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{20,21}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Statue = new EnemyDefaults {
            Id=5, TableIndex=2, ModelCode="e05a",  Name="Statue",           MaxHp=38,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=20,
            ReticleWidth=1.2f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f, ModelDataSize=792,  ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{26,25,26,25}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Dasher = new EnemyDefaults {
            Id=6, TableIndex=3, ModelCode="e06a",  Name="Dasher",           MaxHp=23,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=1, WeaponDefense=0,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=148, ItemResA=100, ItemResB=90,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{22}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Werewolf = new EnemyDefaults {
            Id=7, TableIndex=4, ModelCode="e07a",  Name="Werewolf",         MaxHp=180, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=5, WeaponDefense=0,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=174, ItemResA=90,  ItemResB=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=1111, ModelAnimCount=19, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{68,62}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults FliFli = new EnemyDefaults {
            Id=8, TableIndex=5, ModelCode="e08a", Name="FliFli",            MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=169, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=3.0f, BodyHeight=21.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{37}, ProjectileDamage=new int[]{35} };

        internal static readonly EnemyDefaults Hornet = new EnemyDefaults {
            Id=9, TableIndex=6, ModelCode="e09a",  Name="Hornet",           MaxHp=60,  Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=120, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{42,40}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Halloween = new EnemyDefaults {
            Id=10, TableIndex=7, ModelCode="e10a", Name="Halloween",        MaxHp=150, Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=168, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=850,  ModelAnimCount=18, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{57,57}, ProjectileDamage=new int[]{60} };

        internal static readonly EnemyDefaults CannibalPlant = new EnemyDefaults {
            Id=11, TableIndex=8, ModelCode="e11a", Name="Cannibal Plant",   MaxHp=60,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=167, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=21.0f, BodyDepth=60.0f, ModelDataSize=474,  ModelAnimCount=7, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{28,28,36}, ProjectileDamage=new int[]{30} };

        internal static readonly EnemyDefaults EarthDigger = new EnemyDefaults {
            Id=12, TableIndex=9, ModelCode="e12a", Name="Earth Digger",     MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=188, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=936,  ModelAnimCount=19, AttackPower=197, ElemAtkFire=50, ElemAtkIce=50, ElemAtkThunder=120, ElemAtkWind=50, ElemAtkHoly=50, ElemAtkDark=50,
            MeleeDamage=new int[]{52}, ProjectileDamage=new int[]{37} };

        internal static readonly EnemyDefaults Sunday = new EnemyDefaults {
            Id=14, TableIndex=10, ModelCode="e14a", Name="Sunday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=170, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1454, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{36,26}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Monday = new EnemyDefaults {
            Id=15, TableIndex=11, ModelCode="e15a", Name="Monday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{32,12}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Tuesday = new EnemyDefaults {
            Id=16, TableIndex=12, ModelCode="e16a", Name="Tuesday",          MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1427, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{31}, ProjectileDamage=new int[]{31} };

        internal static readonly EnemyDefaults Wednesday = new EnemyDefaults {
            Id=17, TableIndex=13, ModelCode="e17a", Name="Wednesday",        MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{30,28}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Thursday = new EnemyDefaults {
            Id=18, TableIndex=14, ModelCode="e18a", Name="Thursday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{29}, ProjectileDamage=new int[]{30} };

        internal static readonly EnemyDefaults Friday = new EnemyDefaults {
            Id=19, TableIndex=15, ModelCode="e19a", Name="Friday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=148, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{29,29}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Saturday = new EnemyDefaults {
            Id=20, TableIndex=16, ModelCode="e20a", Name="Saturday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=148, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{29,29,25}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WitchHellza = new EnemyDefaults {
            Id=21, TableIndex=17, ModelCode="e21a", Name="Witch Hellza",     MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=70,  IceRes=70,  ThunderRes=70,  WindRes=70,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=169, ItemResA=85, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{73}, ProjectileDamage=new int[]{73} };

        internal static readonly EnemyDefaults WitchIllza = new EnemyDefaults {
            Id=22, TableIndex=18, ModelCode="e22a", Name="Witch Illza",      MaxHp=120, Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=90,  IceRes=90,  ThunderRes=90,  WindRes=90,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=169, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=868,  ModelAnimCount=18, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{47}, ProjectileDamage=new int[]{47} };

        internal static readonly EnemyDefaults Gunny = new EnemyDefaults {
            Id=23, TableIndex=19, ModelCode="e23a", Name="Gunny",            MaxHp=250, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=153, ItemResA=95,  ItemResB=70,
            BodyWidth=14.0f, BodyHeight=20.0f, BodyDepth=0.0f,  ModelDataSize=1270, ModelAnimCount=19, AttackPower=193, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{44,44}, ProjectileDamage=new int[]{26} };

        internal static readonly EnemyDefaults Gyon = new EnemyDefaults {
            Id=24, TableIndex=20, ModelCode="e24a", Name="Gyon",             MaxHp=225, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=134, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, ModelDataSize=849,  ModelAnimCount=16, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{59} };

        internal static readonly EnemyDefaults PiratesChariot = new EnemyDefaults {
            Id=25, TableIndex=21, ModelCode="e25a", Name="Pirate's Chariot", MaxHp=270, Abs=8,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80,  ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=5, WeaponDefense=30,
            ReticleWidth=1.9f, ReticleHeight=1.8f, StealItemId=159, ItemResA=95,  ItemResB=60,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, ModelDataSize=835,  ModelAnimCount=19, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{69} };

        // Unk150/154/158 (0x150/154/158) read as 0 for regular Auntie Medu; observed non-zero (127.5/80.0/15.0) when the mod's miniboss process was active on this enemy species.
        internal static readonly EnemyDefaults AuntieMedu = new EnemyDefaults {
            Id=26, TableIndex=22, ModelCode="e26a", Name="Auntie Medu",      MaxHp=300, Abs=10, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=166, ItemResA=100, ItemResB=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=944,  ModelAnimCount=19, AttackPower=245, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{60} };

        internal static readonly EnemyDefaults Captain = new EnemyDefaults {
            Id=27, TableIndex=23, ModelCode="e27a", Name="Captain",          MaxHp=225, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=177, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=873,  ModelAnimCount=19, AttackPower=227, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{74,74,72,72}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Corcea = new EnemyDefaults {
            Id=28, TableIndex=24, ModelCode="e28a", Name="Corcea",           MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=140, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=152, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ModelDataSize=871,  ModelAnimCount=19, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{43,43,42,42}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Golem = new EnemyDefaults {
            Id=30, TableIndex=25, ModelCode="e30a", Name="Golem",            MaxHp=375, Abs=4,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            ReticleWidth=2.4f, ReticleHeight=2.4f, StealItemId=177, ItemResA=100, ItemResB=50,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f, ModelDataSize=1071, ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{64,34} };

        internal static readonly EnemyDefaults MrBlare = new EnemyDefaults {
            Id=31, TableIndex=26, ModelCode="e31a", Name="Mr. Blare",        MaxHp=225, Abs=5,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=0,   IceRes=170, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=10,
            StealItemId=161, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{81,81}, ProjectileDamage=new int[]{80,90} };

        internal static readonly EnemyDefaults Dune = new EnemyDefaults {
            Id=32, TableIndex=27, ModelCode="e32a", Name="Dune",             MaxHp=525, Abs=10, MinGoldDrop=18, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=120, HolyRes=100,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{85,85,85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Titan = new EnemyDefaults {
            Id=33, TableIndex=28, ModelCode="e33a", Name="Titan",            MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{105,105,105,105,90,75,60}, ProjectileDamage=new int[]{90,90} };

        internal static readonly EnemyDefaults KingMimicDBC = new EnemyDefaults {
            Id=34, TableIndex=29, ModelCode="e34a", Name="King Mimic (Divine Beast Cave)", MaxHp=90, Abs=4, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,30,35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDBC = new EnemyDefaults {
            Id=35, TableIndex=30, ModelCode="e35a", Name="Mimic (Divine Beast Cave)", MaxHp=68, Abs=3, MinGoldDrop=10, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=1, WeaponDefense=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=920,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{33,33}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicSMT = new EnemyDefaults {
            Id=36, TableIndex=31, ModelCode="e36a", Name="King Mimic (Sun & Moon Temple)", MaxHp=525, Abs=15, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=20,
            StealItemId=174, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{101,102,45}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicSMT = new EnemyDefaults {
            Id=37, TableIndex=32, ModelCode="e37a", Name="Mimic (Sun & Moon Temple)", MaxHp=270, Abs=6, MinGoldDrop=12, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{71,71}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicMS = new EnemyDefaults {
            Id=38, TableIndex=33, ModelCode="e38a", Name="King Mimic (Moon Sea)", MaxHp=600, Abs=12, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=8, WeaponDefense=30,
            StealItemId=176, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{118,96,90}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicMS = new EnemyDefaults {
            Id=39, TableIndex=34, ModelCode="e39a", Name="Mimic (Moon Sea)",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=8, WeaponDefense=30,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{83,83}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Arthur = new EnemyDefaults {
            Id=40, TableIndex=35, ModelCode="e40a", Name="Arthur",           MaxHp=600, Abs=15, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=150, WindRes=80,  HolyRes=80,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=30.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{116,116}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Ghost = new EnemyDefaults {
            Id=42, TableIndex=36, ModelCode="e42a", Name="Ghost",            MaxHp=15,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=3.6f, EntityScaleCopy=3.6f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.1f, ReticleHeight=1.1f, StealItemId=135, ItemResA=100, ItemResB=90,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1220, ModelAnimCount=21, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{20,20}, ProjectileDamage=new int[]{21} };

        internal static readonly EnemyDefaults Alexander = new EnemyDefaults {
            Id=43, TableIndex=37, ModelCode="e43a", Name="Alexander",        MaxHp=675, Abs=15, MinGoldDrop=17, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=150, IceRes=130, ThunderRes=100, WindRes=120, HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=164, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=32.0f, BodyHeight=30.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{120}, ProjectileDamage=new int[]{124} };

        internal static readonly EnemyDefaults Heart = new EnemyDefaults {
            Id=44, TableIndex=38, ModelCode="e44a", Name="Heart",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=150, ItemResA=80, ItemResB=50, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{107}, ProjectileDamage=new int[]{107} };

        internal static readonly EnemyDefaults Club = new EnemyDefaults {
            Id=45, TableIndex=39, ModelCode="e45a", Name="Club",             MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=147, ItemResA=80, ItemResB=50, AttackPower=134, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{104}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Diamond = new EnemyDefaults {
            Id=46, TableIndex=40, ModelCode="e46a", Name="Diamond",          MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50,  WindRes=150, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=151, ItemResA=80, ItemResB=50, AttackPower=135, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Spade = new EnemyDefaults {
            Id=47, TableIndex=41, ModelCode="e47a", Name="Spade",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=152, ItemResA=80, ItemResB=50, AttackPower=132, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{113,113}, ProjectileDamage=new int[]{} };

        // fire=50/ice=50/thu=50/win=50 (resistant to all), holy=150; all-element-resistant mage
        internal static readonly EnemyDefaults Joker = new EnemyDefaults {
            Id=48, TableIndex=42, ModelCode="e48a", Name="Joker",            MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=150,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=149, ItemResA=50, ItemResB=10, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{115}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults BomberHead = new EnemyDefaults {
            Id=49, TableIndex=43, ModelCode="e49a", Name="Bomber Head",      MaxHp=180, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=75,  ThunderRes=125, WindRes=100, HolyRes=75,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=8, WeaponDefense=20,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=159, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ModelDataSize=1663, ModelAnimCount=23, AttackPower=159, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{61,61}, ProjectileDamage=new int[]{64} };

        internal static readonly EnemyDefaults Mummy = new EnemyDefaults {
            Id=50, TableIndex=44, ModelCode="e50a", Name="Mummy",            MaxHp=150, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=null, ItemResA=100, ItemResB=70,
            BodyWidth=9.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=1029, ModelAnimCount=19, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{54,54,54}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Lich = new EnemyDefaults {
            Id=51, TableIndex=45, ModelCode="e51a", Name="Lich",             MaxHp=300, Abs=12, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Undead, FireRes=20,  IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=160,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=5, WeaponDefense=0,
            StealItemId=176, ItemResA=80, ItemResB=30, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{114,114}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults CurseDancer = new EnemyDefaults {
            Id=52, TableIndex=46, ModelCode="e52a", Name="Curse Dancer",     MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=166, ItemResA=100, ItemResB=70, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{90,90,90,90}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults LivingArmor = new EnemyDefaults {
            Id=55, TableIndex=47, ModelCode="e55a", Name="Living Armor",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80,  HolyRes=80,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=null, ItemResA=100, ItemResB=50, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{95,95,95,95}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WhiteFang = new EnemyDefaults {
            Id=56, TableIndex=48, ModelCode="e56a", Name="White Fang",       MaxHp=525, Abs=10, MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=0, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{90,84}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MoonBug = new EnemyDefaults {
            Id=57, TableIndex=49, ModelCode="e57a", Name="Moon Bug",         MaxHp=450, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=50,  IceRes=120, ThunderRes=150, WindRes=50,  HolyRes=100,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=8, WeaponDefense=40,
            StealItemId=159, ItemResA=90, ItemResB=70, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{70,70}, ProjectileDamage=new int[]{70} };

        internal static readonly EnemyDefaults Phantom = new EnemyDefaults {
            Id=58, TableIndex=50, ModelCode="e58a", Name="Phantom",          MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=125, ThunderRes=100, WindRes=125, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{60,54}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Dragon = new EnemyDefaults {
            Id=59, TableIndex=51, ModelCode="e59a", Name="Dragon",           MaxHp=90,  Abs=5,  MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=5, WeaponDefense=40,
            ReticleWidth=2.9f, ReticleHeight=2.7f, StealItemId=161, ItemResA=90,  ItemResB=70,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, ModelDataSize=1422, ModelAnimCount=19, AttackPower=85, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{45,45}, ProjectileDamage=new int[]{50} };

        internal static readonly EnemyDefaults CaveBat = new EnemyDefaults {
            Id=60, TableIndex=52, ModelCode="e60a", Name="Cave Bat",         MaxHp=12,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=0.8f, ReticleHeight=0.8f, StealItemId=151, ItemResA=100, ItemResB=90,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ModelDataSize=940,  ModelAnimCount=21, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{17,16}, ProjectileDamage=new int[]{} };

        // shares scale=3.0 with CaveBat
        internal static readonly EnemyDefaults EvilBat = new EnemyDefaults {
            Id=61, TableIndex=53, ModelCode="e61a", Name="Evil Bat",         MaxHp=150, Abs=4,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=149, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{86,85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults HellPockle = new EnemyDefaults {
            Id=62, TableIndex=54, ModelCode="e62a", Name="Hell Pockle",      MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=6.0f, BodyHeight=17.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{64,64}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RashDasher = new EnemyDefaults {
            Id=63, TableIndex=55, ModelCode="e63a", Name="Rash Dasher",      MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=2, WeaponDefense=10,
            StealItemId=149, ItemResA=100, ItemResB=70, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SteelGiant = new EnemyDefaults {
            Id=64, TableIndex=56, ModelCode="e64a", Name="Steel Giant",      MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=125, WindRes=80,  HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=177, ItemResA=95, ItemResB=50, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{93,93,98,98,80,70,60}, ProjectileDamage=new int[]{64,64} };

        internal static readonly EnemyDefaults Blizzard = new EnemyDefaults {
            Id=65, TableIndex=57, ModelCode="e65a", Name="Blizzard",         MaxHp=750, Abs=8,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=140, WindRes=140, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=5, WeaponDefense=0,
            StealItemId=162, ItemResA=100, ItemResB=50, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{119,119,119,119,105,90,75}, ProjectileDamage=new int[]{105,105} };

        internal static readonly EnemyDefaults MoonDigger = new EnemyDefaults {
            Id=66, TableIndex=58, ModelCode="e66a", Name="Moon Digger",      MaxHp=420, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=125, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=0,
            StealItemId=187, ItemResA=100, ItemResB=70, AttackPower=197, ElemAtkFire=45, ElemAtkIce=45, ElemAtkThunder=130, ElemAtkWind=45, ElemAtkHoly=45, ElemAtkDark=45,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{83}, ProjectileDamage=new int[]{72} };

        internal static readonly EnemyDefaults DarkFlower = new EnemyDefaults {
            Id=67, TableIndex=59, ModelCode="e67a", Name="Dark Flower",      MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=5, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=147, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{90,90,94}, ProjectileDamage=new int[]{85} };

        internal static readonly EnemyDefaults CursedRose = new EnemyDefaults {
            Id=68, TableIndex=60, ModelCode="e68a", Name="Cursed Rose",      MaxHp=225, Abs=4,  MinGoldDrop=6,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=null, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f, ModelDataSize=476,  ModelAnimCount=7, AttackPower=146, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{49,49,48}, ProjectileDamage=new int[]{46} };

        // thunder=0 (immune)
        internal static readonly EnemyDefaults Billy = new EnemyDefaults {
            Id=69, TableIndex=61, ModelCode="e69a", Name="Billy",            MaxHp=300, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=0,   WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=163, ItemResA=100, ItemResB=70, AttackPower=83, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{93,93}, ProjectileDamage=new int[]{93,110} };

        // fire=65486 (0xFFCE — effectively absorbs fire damage)
        internal static readonly EnemyDefaults Vulcan = new EnemyDefaults {
            Id=70, TableIndex=62, ModelCode="e70a", Name="Vulcan",           MaxHp=480, Abs=12, MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=65486, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=5, WeaponDefense=40,
            StealItemId=81, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{88,94,94}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CrabbyHermit = new EnemyDefaults {
            Id=71, TableIndex=63, ModelCode="e71a", Name="Crabby Hermit",    MaxHp=300, Abs=4,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.9f, ReticleHeight=1.9f, StealItemId=166, ItemResA=95,  ItemResB=70,
            BodyWidth=14.0f, BodyHeight=22.0f, BodyDepth=100.0f, ModelDataSize=1612, ModelAnimCount=22, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{83,83,80}, ProjectileDamage=new int[]{76} };

        internal static readonly EnemyDefaults SpaceGyon = new EnemyDefaults {
            Id=72, TableIndex=64, ModelCode="e72a", Name="Space Gyon",       MaxHp=525, Abs=5,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=75,  IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=153, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{78,78}, ProjectileDamage=new int[]{75} };

        internal static readonly EnemyDefaults BlueDragon = new EnemyDefaults {
            Id=73, TableIndex=65, ModelCode="e73a", Name="Blue Dragon",      MaxHp=600, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=125, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=5, WeaponDefense=30,
            StealItemId=162, ItemResA=80, ItemResB=50, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{90,90}, ProjectileDamage=new int[]{90} };

        internal static readonly EnemyDefaults BlackDragon = new EnemyDefaults {
            Id=74, TableIndex=66, ModelCode="e74a", Name="Black Dragon",     MaxHp=900, Abs=20, MinGoldDrop=22, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=130,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=10, WeaponDefense=60,
            StealItemId=154, ItemResA=50, ItemResB=40, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{135} };

        internal static readonly EnemyDefaults MaskOfPrajna = new EnemyDefaults {
            Id=75, TableIndex=67, ModelCode="e75a", Name="Mask of Prajna",   MaxHp=375, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=5, WeaponDefense=10,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=80,  ItemResB=70,
            BodyWidth=32.0f, BodyHeight=26.0f, BodyDepth=0.0f,  ModelDataSize=1398, ModelAnimCount=19, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{80,78}, ProjectileDamage=new int[]{65} };

        internal static readonly EnemyDefaults CrescentBaron = new EnemyDefaults {
            Id=76, TableIndex=68, ModelCode="e76a", Name="Crescent Baron",   MaxHp=450, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=null, ItemResA=80, ItemResB=70, AttackPower=170, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{98,98,94,94}, ProjectileDamage=new int[]{70} };

        internal static readonly EnemyDefaults Rockanoff = new EnemyDefaults {
            Id=77, TableIndex=69, ModelCode="e77a", Name="Rockanoff",        MaxHp=30,  Abs=3,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=954,  ModelAnimCount=19, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicWOF = new EnemyDefaults {
            Id=78, TableIndex=70, ModelCode="e78a", Name="King Mimic (Wise Owl Forest)", MaxHp=150, Abs=10, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{67,56,45,50}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicWOF = new EnemyDefaults {
            Id=79, TableIndex=71, ModelCode="e79a", Name="Mimic (Wise Owl Forest)", MaxHp=90,  Abs=3,  MinGoldDrop=6,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=914,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{47,47}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicSW = new EnemyDefaults {
            Id=80, TableIndex=72, ModelCode="e80a", Name="King Mimic (Shipwreck)", MaxHp=300, Abs=15, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{84,78,56}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicSW = new EnemyDefaults {
            Id=81, TableIndex=73, ModelCode="e81a", Name="Mimic (Shipwreck)", MaxHp=150, Abs=4, MinGoldDrop=6, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicGoT = new EnemyDefaults {
            Id=82, TableIndex=74, ModelCode="e82a", Name="King Mimic (Gallery of Time)", MaxHp=675, Abs=18, MinGoldDrop=25, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=30,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{134,120,98}, ProjectileDamage=new int[]{} };

        // code=kori (Japanese for "ice" — may be official name)
        internal static readonly EnemyDefaults MimicGoT = new EnemyDefaults {
            Id=83, TableIndex=75, ModelCode="e83a", Name="Mimic (Gallery of Time)", MaxHp=450, Abs=6,  MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{} };

        // SW boss — projectile/summon entity of Ice Queen; not a standalone fight.
        internal static readonly EnemyDefaults IceArrow = new EnemyDefaults {
            Id=84, TableIndex=76, ModelCode="korinoya", Name="Ice Arrow", MaxHp=100,   Abs=17, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f,  EntityScaleCopy=2.0f,  DamageReduction=5,  WeaponDefense=0,
            StealItemId=65535, ItemResA=70, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Sam = new EnemyDefaults {
            Id=85, TableIndex=77, ModelCode="e86a", Name="Sam",              MaxHp=180, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.6f, StealItemId=162, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=19.0f, BodyDepth=0.0f,  ModelDataSize=871,  ModelAnimCount=19, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{64,64,80}, ProjectileDamage=new int[]{58,58} };

        // All bosses have MinGoldDrop=0, DropChance=0, StealItemId=65535 (can't steal).
        internal static readonly EnemyDefaults Dran = new EnemyDefaults {
            Id=112, TableIndex=78, ModelCode="c12a", Name="Dran",            MaxHp=250,   Abs=10, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=50,
            EntityScale=45.0f, EntityScaleCopy=45.0f, DamageReduction=10, WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=60.0f, BodyHeight=60.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{45,45,45,25,25,25,25,25,25,25,25,25,25}, ProjectileDamage=new int[]{17} };

        internal static readonly EnemyDefaults MasterUtan = new EnemyDefaults {
            Id=114, TableIndex=79, ModelCode="c14a", Name="Master Utan",     MaxHp=700,   Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=35.0f, EntityScaleCopy=35.0f, DamageReduction=12, WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=42.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{62,47}, ProjectileDamage=new int[]{13} };

        // SW boss — ice=65486 (0xFFCE, -50 as int16) = fire-absorbing (same encoding as Vulcan's fire)
        internal static readonly EnemyDefaults IceQueen = new EnemyDefaults {
            Id=113, TableIndex=80, ModelCode="c13a", Name="Ice Queen",       MaxHp=700,   Abs=30, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=65486, ThunderRes=80, WindRes=80, HolyRes=120,
            EntityScale=13.0f, EntityScaleCopy=13.0f, DamageReduction=10, WeaponDefense=0,
            StealItemId=65535, ItemResA=40, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=24.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingsCurseCoffin = new EnemyDefaults {
            Id=115, TableIndex=81, ModelCode="c15a", Name="King's Curse Coffin",    MaxHp=2000,  Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=125,
            EntityScale=6.0f,  EntityScaleCopy=6.0f,  DamageReduction=10, WeaponDefense=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=52.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // Unlisted phase entity — code=c15b; not in EnemySpecies.cs; suspected SMT King's-Curse scripted phase.
        internal static readonly EnemyDefaults KingsCurse = new EnemyDefaults {
            Id=100, TableIndex=82, ModelCode="c15b", Name="King's Curse", MaxHp=1000, Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=4.0f,  EntityScaleCopy=4.0f,  DamageReduction=10, WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=52.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{91,91,71}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MinotaurJoe = new EnemyDefaults {
            Id=116, TableIndex=83, ModelCode="c16a", Name="Minotaur Joe",    MaxHp=2000,  Abs=50, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=25.0f, EntityScaleCopy=25.0f, DamageReduction=12, WeaponDefense=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=45.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{100,125,100,100,100}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DarkGenie = new EnemyDefaults {
            Id=117, TableIndex=84, ModelCode="c17a", Name="Dark Genie",      MaxHp=2000,  Abs=60, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=25, WeaponDefense=30,
            StealItemId=65535, ItemResA=30, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=62.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DarkGenieForm2 = new EnemyDefaults {
            Id=118, TableIndex=85, ModelCode="c17b", Name="Dark Genie (form 2)", MaxHp=3200, Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  DamageReduction=0,  WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=57.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RightHand = new EnemyDefaults {
            Id=119, TableIndex=86, ModelCode="c17c", Name="Right Hand",      MaxHp=3200,  Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  DamageReduction=0,  WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=57.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        // Left Hand has no EID=120 record in the table; its HP (90) is stored in Right Hand's u98
        // via the off-by-one. The game spawns it using an anonymous EID=0 record at idx=87.
        internal static readonly EnemyDefaults LeftHand = new EnemyDefaults {
            Id=120, TableIndex=87, ModelCode="c17_", Name="Left Hand",       MaxHp=90,    Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f,  EntityScaleCopy=5.0f,  DamageReduction=0,  WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };  // idx=87 is the anonymous EID=0 record

        // These are ATTACK/EFFECT entities (projectiles, beams, barriers, summons), not standalone enemies —
        // ModelCode is a FAMILY PREFIX, not an exact filename, and they have no "死亡": they vanish via
        // 消滅/爆発 (despawn/explode). For these the boss-death system doesn't apply (scripted despawn).
        // Dark Genie fight companions: code=c17_ = DG attack-effect family. The prefix maps to several .chr:
        // c17_beem.chr (発射/ループ/消滅 = launch/loop/despawn), c17_kaze.chr (wind), c17_hikari.chr (light),
        // c17_syougeki.chr (shock). e.g. c17_beem @ data.dat 0x1b1e9000. Which TableIndex→which is unconfirmed.
        internal static readonly EnemyDefaults DGComp88 = new EnemyDefaults {
            Id=0, TableIndex=88, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=90, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100, EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=20, Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp89 = new EnemyDefaults {
            Id=0, TableIndex=89, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=90, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100, EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=20, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp90 = new EnemyDefaults {
            Id=0, TableIndex=90, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=90, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100, EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=20, Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WineKeg = new EnemyDefaults {
            Id=121, TableIndex=91, ModelCode="e85a", Name="Wine Keg",        MaxHp=80,    Abs=0,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f,  EntityScaleCopy=7.0f,  DamageReduction=0,  WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{8}, ProjectileDamage=new int[]{} };

        // code=b3_r → b3_reiki.chr (霊気 "aura/spirit") @ data.dat 0x1a8c1800 — 1 motion: 0 reiki (aura).
        internal static readonly EnemyDefaults SWComp92 = new EnemyDefaults {
            Id=0, TableIndex=92, ModelCode="b3_r", Name="Ice Aura", MaxHp=80, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,  // reiki: fully neutral (unlike the ice-immune siblings)
            EntityScale=5.0f, EntityScaleCopy=5.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=0, WeaponDefense=0, Abs=0, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp93 = new EnemyDefaults {
            Id=0, TableIndex=93, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=80, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100, EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0, Abs=0, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // listed as non-drop in EnemySpecies.cs
        internal static readonly EnemyDefaults Gol = new EnemyDefaults {
            Id=90, TableIndex=94, ModelCode="e90a", Name="Gol",              MaxHp=600, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{62} };

        // listed as non-drop in EnemySpecies.cs
        internal static readonly EnemyDefaults Sil = new EnemyDefaults {
            Id=91, TableIndex=95, ModelCode="e91a", Name="Sil",              MaxHp=500, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=90,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{62} };

        internal static readonly EnemyDefaults Yammich = new EnemyDefaults {
            Id=301, TableIndex=96, ModelCode="e101", Name="Yammich",         MaxHp=13,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=70,  HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=0, WeaponDefense=1,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults StatueDog = new EnemyDefaults {
            Id=303, TableIndex=97, ModelCode="e103", Name="Statue Dog",      MaxHp=15,  Abs=2,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=160, ItemResA=90,  ItemResB=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=667,  ModelAnimCount=12, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Opar = new EnemyDefaults {
            Id=304, TableIndex=98, ModelCode="e104", Name="Opar",            MaxHp=28,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=60,  ThunderRes=130, WindRes=100, HolyRes=100,
            EntityScale=15.0f, EntityScaleCopy=15.0f, DamageReduction=1, WeaponDefense=2,
            StealItemId=227, ItemResA=90, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=50.0f, BodyHeight=56.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{35,35} };

        internal static readonly EnemyDefaults HaleyHoley = new EnemyDefaults {
            Id=305, TableIndex=99, ModelCode="e105", Name="Haley Holey",     MaxHp=50,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=140, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.0f, ReticleHeight=1.1f, StealItemId=186, ItemResA=100, ItemResB=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ModelDataSize=1046, ModelAnimCount=19, AttackPower=189, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{37,37,37}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingPrickly = new EnemyDefaults {
            Id=306, TableIndex=100, ModelCode="e106", Name="King Prickly",    MaxHp=63,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=10.0f, BodyHeight=110.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{50,50,50}, ProjectileDamage=new int[]{} };

        // IceQueen (SW floor 18) fight companions — all id=0, boss sentinels. Ice-attack effect entities.
        // code=bari → baria.chr (barrier) @ data.dat 0x1e1b1000 — 0 ループ(loop) / 1 消滅(despawn) / 2 出現(appear).
        internal static readonly EnemyDefaults IQComp101 = new EnemyDefaults {
            Id=0, TableIndex=101, ModelCode="bari", Name="Ice Barrier", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=5, WeaponDefense=0, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=70, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=kori → kori.chr (ice arrow); motions documented above at IceArrow (0 ice appear / 1 ice burst).
        internal static readonly EnemyDefaults IQComp102 = new EnemyDefaults {
            Id=0, TableIndex=102, ModelCode="kori", Name="Ice Prison", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=5, WeaponDefense=0, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=70, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=i_me → i_meteo.chr (ice meteor) @ data.dat 0x1e37a000 — 0 氷生成(ice form) / 1 ループ / 2 爆発(explode).
        internal static readonly EnemyDefaults IQComp103 = new EnemyDefaults {
            Id=0, TableIndex=103, ModelCode="i_me", Name="Ice Meteor", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=5, WeaponDefense=0, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=70, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };

        // code=i_ta → i_tatumaki.chr (ice tornado) @ data.dat 0x1e38b800 — 0 柱出現(pillar) / 1 竜巻(tornado) / 2 竜巻消える(vanish).
        internal static readonly EnemyDefaults IQComp104 = new EnemyDefaults {
            Id=0, TableIndex=104, ModelCode="i_ta", Name="Ice Tornado", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=5, WeaponDefense=0, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=70, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Gacious = new EnemyDefaults {
            Id=317, TableIndex=105, ModelCode="e124", Name="Gacious",         MaxHp=1800, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=70,  IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=90, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130,130,130,130,130}, ProjectileDamage=new int[]{} };

        // Dark Genie — FINAL FORM (EID 223, model c23a); the endgame Dark Genie battle, distinct from the earlier
        // forms c17a/c17b. Giant hands + mouth beam. (Its TI 106 sits in the d5 spawn-layout pool in the data, but
        // the final fight is a scripted encounter, not a Muska Lacka floor.)
        // Weak to fire (70) and holy (140). Attacks: 5 hand swings ×85; mouth beam = funcId-229 `_SET_SHOT`
        // (`ex4`/`ex5`), BST default 130 via `last_gw2` (idx26, proj speed 1.5, lifetime 20).
        // Sub-effect STBs: c23_beem (beam) / c23_syougeki (impact) / c23_hasira (pillar).
        internal static readonly EnemyDefaults DarkGenieFinal = new EnemyDefaults {
            Id=223, TableIndex=106, ModelCode="c23a", Name="Dark Genie (Final Form)", MaxHp=5000, Abs=5, MinGoldDrop=5, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            StealItemId=65535, ItemResA=100, ItemResB=0, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{85,85,85,85,85}, ProjectileDamage=new int[]{130} };

        // code=last_mc → last_mc.chr @ data.dat 0x203C9000 — 発射(fire) / 召喚(summon) / 待ち(wait). No own damage script (pure visual).
        internal static readonly EnemyDefaults DGFinalSummon = new EnemyDefaults {
            Id=0, TableIndex=107, ModelCode="last_mc", Name="DG Final summon (last_mc)", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=8, WeaponDefense=0, Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=65535, ItemResA=100, ItemResB=90, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=last_gw1 → last_gw1.chr @ data.dat 0x20397000 — グランドウェイブ(ground wave). No own damage script.
        internal static readonly EnemyDefaults DGFinalGroundWave = new EnemyDefaults {
            Id=0, TableIndex=108, ModelCode="last_gw1", Name="DG Final ground wave (last_gw1)", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=8, WeaponDefense=0, Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=65535, ItemResA=100, ItemResB=90, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=c23_beem → c23_beem.chr @ data.dat 0x203A7800 — 発射(fire) / ループ(loop) / 消滅(vanish). Beam, 175 dmg (= the c17_beem Dark Genie beam).
        internal static readonly EnemyDefaults DGFinalBeam = new EnemyDefaults {
            Id=0, TableIndex=109, ModelCode="c23_beem", Name="DG Final beam", MaxHp=90, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=0, WeaponDefense=20, Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        // code=c23_beem_s → c23_beem_s.chr @ data.dat 0x20851000 — 発動(activate) / ループ(loop) / 消滅(vanish). Beam variant, 175 dmg.
        internal static readonly EnemyDefaults DGFinalBeamS = new EnemyDefaults {
            Id=0, TableIndex=110, ModelCode="c23_beem_s", Name="DG Final beam (small)", MaxHp=90, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            DamageReduction=0, WeaponDefense=20, Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GemronFire = new EnemyDefaults {
            Id=311, TableIndex=111, ModelCode="e111", Name="Gemron (Fire)",   MaxHp=2500, Abs=15, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=0,   IceRes=150, ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=161, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults Nikapous = new EnemyDefaults {
            Id=308, TableIndex=112, ModelCode="e108", Name="Nikapous",        MaxHp=2350, Abs=15, MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=100, ThunderRes=100, WindRes=125, HolyRes=125,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=133, ItemResA=100, ItemResB=70, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=27.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        // These reuse base-game species IDs with new model codes and scaled stats.
        // Not added to Defaults dictionary to avoid overwriting base-game entries.
        internal static readonly EnemyDefaults WhiteFangEnhanced = new EnemyDefaults {
            Id=56,  TableIndex=113, ModelCode="e125", Name="White Fang (Enhanced)",  MaxHp=1750,  Abs=15,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults ArthurEnhanced = new EnemyDefaults {
            Id=40,  TableIndex=114, ModelCode="e126", Name="Arthur (Enhanced)",  MaxHp=2900,  Abs=20,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=50, IceRes=50, ThunderRes=150, WindRes=80, HolyRes=80,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=30.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SilEnhanced = new EnemyDefaults {
            Id=91,  TableIndex=115, ModelCode="e127", Name="Sil (Enhanced)",  MaxHp=1500,  Abs=15,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{111,111,115,115,102,95,85}, ProjectileDamage=new int[]{102} };

        internal static readonly EnemyDefaults HalloweenEnhanced = new EnemyDefaults {
            Id=10,  TableIndex=116, ModelCode="e128", Name="Halloween (Enhanced)",  MaxHp=1800,  Abs=15,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=168, ItemResA=100, ItemResB=70, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults MasterJacketEnhanced = new EnemyDefaults {
            Id=1,   TableIndex=117, ModelCode="e129", Name="Master Jacket (Enhanced)",  MaxHp=2000,  Abs=15,  MinGoldDrop=7,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80, ThunderRes=100, WindRes=80, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=177, ItemResA=100, ItemResB=80, AttackPower=150, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults VulcanEnhanced = new EnemyDefaults {
            Id=70,  TableIndex=118, ModelCode="e130", Name="Vulcan (Enhanced)",  MaxHp=2400,  Abs=15,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=0, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=10, WeaponDefense=40,
            StealItemId=81, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{114,114,114}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MummyEnhanced = new EnemyDefaults {
            Id=50,  TableIndex=119, ModelCode="e131", Name="Mummy (Enhanced)",  MaxHp=1500,  Abs=5,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=9.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{98,98}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DiamondEnhanced = new EnemyDefaults {
            Id=46,  TableIndex=120, ModelCode="e132", Name="Diamond (Enhanced)",  MaxHp=1750,  Abs=10,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50, WindRes=150, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=151, ItemResA=80, ItemResB=50, AttackPower=135, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{123,123}, ProjectileDamage=new int[]{} };

        // Gemron (Ice) — tier 2
        internal static readonly EnemyDefaults GemronIce = new EnemyDefaults {
            Id=312, TableIndex=121, ModelCode="e112", Name="Gemron (Ice)",    MaxHp=4000, Abs=20, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=150, IceRes=0,   ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=15, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=162, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{120} };

        internal static readonly EnemyDefaults HornHead = new EnemyDefaults {
            Id=319, TableIndex=122, ModelCode="e119", Name="Horn Head",       MaxHp=2500, Abs=20, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=186, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults AuntieMeduEnhanced = new EnemyDefaults {
            Id=26,  TableIndex=123, ModelCode="e133", Name="Auntie Medu (Enhanced)",  MaxHp=3750,  Abs=20,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=30, IceRes=150, ThunderRes=30, WindRes=30, HolyRes=30,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=166, ItemResA=100, ItemResB=60, AttackPower=245, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        internal static readonly EnemyDefaults RockanoffEnhanced = new EnemyDefaults {
            Id=77,  TableIndex=124, ModelCode="e134", Name="Rockanoff (Enhanced)",  MaxHp=2500,  Abs=20,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=15, WeaponDefense=50,
            StealItemId=160, ItemResA=90, ItemResB=60, AttackPower=160, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults YammichEnhanced = new EnemyDefaults {
            Id=301, TableIndex=125, ModelCode="e135", Name="Yammich (Enhanced)",  MaxHp=3000,  Abs=20,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WitchHellzaEnhanced = new EnemyDefaults {
            Id=21,  TableIndex=126, ModelCode="e136", Name="Witch Hellza (Enhanced)",  MaxHp=1500,  Abs=20,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=169, ItemResA=85, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults SteelGiantEnhanced = new EnemyDefaults {
            Id=64,  TableIndex=127, ModelCode="e137", Name="Steel Giant (Enhanced)",  MaxHp=3900,  Abs=25,  MinGoldDrop=15,  DropChance=50,
            Category=EnemyCategory.Metal, FireRes=80, IceRes=80, ThunderRes=150, WindRes=80, HolyRes=80,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=15, WeaponDefense=70,
            StealItemId=177, ItemResA=95, ItemResB=50, AttackPower=154, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{163,163,178,178,163,162,161}, ProjectileDamage=new int[]{164,164} };

        internal static readonly EnemyDefaults ClubEnhanced = new EnemyDefaults {
            Id=45,  TableIndex=128, ModelCode="e138", Name="Club (Enhanced)",  MaxHp=2525,  Abs=20,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=147, ItemResA=80, ItemResB=50, AttackPower=134, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{114}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CorceaEnhanced = new EnemyDefaults {
            Id=28,  TableIndex=129, ModelCode="e139", Name="Corcea (Enhanced)",  MaxHp=3250,  Abs=20,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=152, ItemResA=100, ItemResB=70, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{110,110,110,110}, ProjectileDamage=new int[]{} };

        // Demon Shaft enemies reuse base-game eids with new model codes and scaled stats.
        // Each eid appears multiple times in the static table at u090a tiers: 15, 20, 23, 30.
        // The entries below capture the FIRST occurrence (lowest tier) as the EnemyDefaults base.
        // The Gemrons (311-315) are Demon Shaft unique enemies — each appears at a single tier.
        // Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults MimicDS = new EnemyDefaults {
            Id=309, TableIndex=130, ModelCode="e109", Name="Mimic (Demon Shaft)",     MaxHp=3500, Abs=10, MinGoldDrop=26, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // King Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults KingMimicDS = new EnemyDefaults {
            Id=310, TableIndex=131, ModelCode="e110", Name="King Mimic (Demon Shaft)", MaxHp=5000, Abs=20, MinGoldDrop=35, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=15, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // Gemron (Thunder) — tier 3
        internal static readonly EnemyDefaults GemronThunder = new EnemyDefaults {
            Id=313, TableIndex=132, ModelCode="e113", Name="Gemron (Thunder)", MaxHp=5500, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=30,  IceRes=30,  ThunderRes=0,   WindRes=150, HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=20, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=163, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults BishopQ = new EnemyDefaults {
            Id=316, TableIndex=133, ModelCode="e116", Name="Bishop Q",        MaxHp=6000, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=40,  IceRes=40,  ThunderRes=40,  WindRes=40,  HolyRes=140,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=null, ItemResA=50, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=20.0f, BodyHeight=19.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{130,130} };

        internal static readonly EnemyDefaults CaveBatEnhanced = new EnemyDefaults {
            Id=60,  TableIndex=134, ModelCode="e140", Name="Cave Bat (Enhanced)",  MaxHp=1500,  Abs=25,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=151, ItemResA=100, ItemResB=90, AttackPower=0, ElemAtkFire=100, ElemAtkIce=150, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{95,95}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GolEnhanced = new EnemyDefaults {
            Id=90,  TableIndex=135, ModelCode="e141", Name="Gol (Enhanced)",  MaxHp=6000,  Abs=25,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=30, WeaponDefense=80,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{131,131,135,135,132,125,125}, ProjectileDamage=new int[]{132} };

        internal static readonly EnemyDefaults MaskOfPrajnaEnhanced = new EnemyDefaults {
            Id=75,  TableIndex=136, ModelCode="e142", Name="Mask of Prajna (Enhanced)",  MaxHp=5500,  Abs=25,  MinGoldDrop=15,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=20, WeaponDefense=10,
            StealItemId=151, ItemResA=80, ItemResB=70, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=32.0f, BodyHeight=26.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{140,138}, ProjectileDamage=new int[]{146} };

        internal static readonly EnemyDefaults GyonEnhanced = new EnemyDefaults {
            Id=24,  TableIndex=137, ModelCode="e143", Name="Gyon (Enhanced)",  MaxHp=5750,  Abs=25,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=134, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults SpadeEnhanced = new EnemyDefaults {
            Id=47,  TableIndex=138, ModelCode="e144", Name="Spade (Enhanced)",  MaxHp=5000,  Abs=25,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=152, ItemResA=80, ItemResB=50, AttackPower=132, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RashDasherEnhanced = new EnemyDefaults {
            Id=63,  TableIndex=139, ModelCode="e145", Name="Rash Dasher (Enhanced)",  MaxHp=5000,  Abs=25,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=50, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=149, ItemResA=100, ItemResB=70, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CaptainEnhanced = new EnemyDefaults {
            Id=27,  TableIndex=140, ModelCode="e146", Name="Captain (Enhanced)",  MaxHp=4000,  Abs=25,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80, WindRes=80, HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=227, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{99,99,99,99}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDSEnhanced = new EnemyDefaults {
            Id=309, TableIndex=141, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced)",  MaxHp=5000,  Abs=10,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhanced = new EnemyDefaults {
            Id=310, TableIndex=142, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced)",  MaxHp=7500,  Abs=20,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=20, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GemronWind = new EnemyDefaults {
            Id=314, TableIndex=143, ModelCode="e114", Name="Gemron (Wind)",   MaxHp=8000, Abs=30, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=100, ThunderRes=140, WindRes=0,   HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=164, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{140} };

        internal static readonly EnemyDefaults SilverGear = new EnemyDefaults {
            Id=318, TableIndex=144, ModelCode="e118", Name="Silver Gear",     MaxHp=2500, Abs=30, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=30,  IceRes=30,  ThunderRes=30,  WindRes=30,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{110} };

        internal static readonly EnemyDefaults AlexanderEnhanced = new EnemyDefaults {
            Id=43,  TableIndex=145, ModelCode="e149", Name="Alexnder (Enhanced)",  MaxHp=7500,  Abs=30,  MinGoldDrop=17,  DropChance=50,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=120, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=23, WeaponDefense=50,
            StealItemId=164, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=32.0f, BodyHeight=30.0f, BodyDepth=0.0f,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        internal static readonly EnemyDefaults HeartEnhanced = new EnemyDefaults {
            Id=44,  TableIndex=146, ModelCode="e150", Name="Heart (Enhanced)",  MaxHp=5000,  Abs=30,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=150, ItemResA=80, ItemResB=50, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults BomberHeadEnhanced = new EnemyDefaults {
            Id=49,  TableIndex=147, ModelCode="e151", Name="Bomber Head (Enhanced)",  MaxHp=6000,  Abs=30,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=20,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=23, WeaponDefense=20,
            StealItemId=159, ItemResA=100, ItemResB=70, AttackPower=159, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{160} };

        internal static readonly EnemyDefaults CrabbyHermitEnhanced = new EnemyDefaults {
            Id=71,  TableIndex=148, ModelCode="e152", Name="Crabby Hermit (Enhanced)",  MaxHp=6500,  Abs=30,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=23, WeaponDefense=20,
            StealItemId=166, ItemResA=95, ItemResB=70, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=14.0f, BodyHeight=22.0f, BodyDepth=100.0f,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults CursedRoseEnhanced = new EnemyDefaults {
            Id=68,  TableIndex=149, ModelCode="e153", Name="Cursed Rose (Enhanced)",  MaxHp=5000,  Abs=30,  MinGoldDrop=6,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=130, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=146, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{140,140,140}, ProjectileDamage=new int[]{140} };

        internal static readonly EnemyDefaults PiratesChariotEnhanced = new EnemyDefaults {
            Id=25,  TableIndex=150, ModelCode="e154", Name="Pirate's Chariot (Enhanced)",  MaxHp=6750,  Abs=30,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80, ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=23, WeaponDefense=60,
            StealItemId=159, ItemResA=95, ItemResB=60, AttackPower=92, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=20, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{114} };

        internal static readonly EnemyDefaults SpaceGyonEnhanced = new EnemyDefaults {
            Id=72,  TableIndex=151, ModelCode="e155", Name="Space Gyon (Enhanced)",  MaxHp=7800,  Abs=30,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=0, IceRes=0, ThunderRes=20, WindRes=0, HolyRes=0,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=153, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{160} };

        internal static readonly EnemyDefaults MimicDSEnhancedTwice = new EnemyDefaults {
            Id=309, TableIndex=152, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced x2)",  MaxHp=6500,  Abs=15,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhancedTwice = new EnemyDefaults {
            Id=310, TableIndex=153, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced x2)",  MaxHp=10000,  Abs=25,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=23, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // Gemron (Holy) — tier 5
        internal static readonly EnemyDefaults GemronHoly = new EnemyDefaults {
            Id=315, TableIndex=154, ModelCode="e115", Name="Gemron (Holy)",   MaxHp=12500, Abs=35, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=0,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=30, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=165, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults GaciousEnhanced = new EnemyDefaults {
            Id=317, TableIndex=155, ModelCode="e117", Name="Gacious (Enhanced)",  MaxHp=15000,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=0, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=140,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=189, ItemResA=100, ItemResB=90, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{180,180,180,180,180}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults EvilBatEnhanced = new EnemyDefaults {
            Id=61,  TableIndex=156, ModelCode="e158", Name="Evil Bat (Enhanced)",  MaxHp=7500,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=150, IceRes=150, ThunderRes=150, WindRes=150, HolyRes=200,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=149, ElemAtkFire=100, ElemAtkIce=200, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CrescentBaronEnhanced = new EnemyDefaults {
            Id=76,  TableIndex=157, ModelCode="e159", Name="Crescent Baron (Enhanced)",  MaxHp=16000,  Abs=35,  MinGoldDrop=18,  DropChance=50,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=null, ItemResA=80, ItemResB=70, AttackPower=170, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults StatueDogEnhanced = new EnemyDefaults {
            Id=303, TableIndex=158, ModelCode="e160", Name="Statue Dog (Enhanced)",  MaxHp=12500,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults JokerEnhanced = new EnemyDefaults {
            Id=48,  TableIndex=159, ModelCode="e161", Name="Joker (Enhanced)",  MaxHp=9500,  Abs=35,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=150,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=149, ItemResA=50, ItemResB=10, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults LichEnhanced = new EnemyDefaults {
            Id=51,  TableIndex=160, ModelCode="e162", Name="Lich (Enhanced)",  MaxHp=10000,  Abs=35,  MinGoldDrop=15,  DropChance=80,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=160,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=176, ItemResA=80, ItemResB=30, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{110} };

        internal static readonly EnemyDefaults TitanEnhanced = new EnemyDefaults {
            Id=33,  TableIndex=161, ModelCode="e163", Name="Titan (Enhanced)",  MaxHp=11500,  Abs=35,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{155,155,160,160,150,145,140}, ProjectileDamage=new int[]{160,160} };

        internal static readonly EnemyDefaults LivingArmorEnhanced = new EnemyDefaults {
            Id=55,  TableIndex=162, ModelCode="e164", Name="Living Armor (Enhanced)",  MaxHp=9500,  Abs=35,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80, HolyRes=80,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=null, ItemResA=100, ItemResB=50, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDSEnhancedThrice = new EnemyDefaults {
            Id=309, TableIndex=163, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced x3)",  MaxHp=7500,  Abs=20,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhancedThrice = new EnemyDefaults {
            Id=310, TableIndex=164, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced x3)",  MaxHp=19500,  Abs=30,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults BlackKnight = new EnemyDefaults {
            Id=221, TableIndex=165, ModelCode="c21a", Name="Black Knight",    MaxHp=40000, Abs=5,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=100,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=50,
            // Projectile = the `engetu` crescent-slash boss behavior (BST idx30, 150); `terepo` (idx33) is teleport (no dmg).
            BodyWidth=20.0f, BodyHeight=35.0f, BodyDepth=200.0f,
            MeleeDamage=new int[]{170,170}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults BlackKnightMount = new EnemyDefaults {
            // Projectile = the `g_center` _SET_SHOT (BST default 170, = the `dash` charge idx31); `kamai` (idx32) is a stance (no dmg).
            Id=221, TableIndex=166, ModelCode="c22a", Name="Black Knight Mount", MaxHp=50000, AttackPower=0,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100, EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=100, Abs=5, MinGoldDrop=0, DropChance=0, StealItemId=65535, ItemResA=50, ItemResB=0, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=50,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{170} };

        // CUT ENEMY — no species table entry and no CHR model file (e53a.chr/e54a.chr absent).
        // Id=54 is referenced in WOF spawn-pool data and its name appears in the game's name table,
        // but the engine skips EIDs 53–54 entirely in the packed sequential species table.
        // TableIndex is null: there is no record in EnemySpeciesTable for this EID.
        // Only appears on WOF event floors 8 and 16 via a scripted spawn that bypasses the
        // normal species-table lookup. Live-slot stats are unknown until an event-floor dump is taken.
        internal static readonly EnemyDefaults KillerSnake = new EnemyDefaults {
            Id=54, Name="Killer Snake" };

        // ── Lookup by species ID ──────────────────────────────────────────────────
        internal static readonly Dictionary<ushort, EnemyDefaults> Defaults;
        static EnemySpecies()
        {
            EnemyDefaults[] all =
            {
                // DBC
                SkeletonSoldier, MasterJacket, Statue, Dasher, CaveBat, MimicDBC,
                Ghost, Dragon, KingMimicDBC, Rockanoff, StatueDog,
                // WOF
                CannibalPlant, Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday,
                WitchIllza, WitchHellza, MimicWOF, HaleyHoley,
                Werewolf, Hornet, Halloween, EarthDigger, KingMimicWOF,
                // SW
                Captain, PiratesChariot, Gunny, CursedRose, Gyon, AuntieMedu,
                Corcea, MaskOfPrajna, Sam, MimicSW, KingMimicSW,
                // SM
                Mummy, Phantom, BomberHead, MimicSMT, Golem, CrabbyHermit,
                FliFli, KingMimicSMT, MrBlare, Dune, Titan,
                Heart, Club, Diamond, Spade, Joker,
                // MS
                KingMimicMS, MimicMS, Lich, CurseDancer, KillerSnake, LivingArmor,
                WhiteFang, MoonBug,
                // GoT
                Arthur, Alexander, EvilBat, HellPockle, RashDasher,
                SteelGiant, Blizzard, MoonDigger, DarkFlower, Billy,
                Vulcan, SpaceGyon, BlueDragon, BlackDragon, CrescentBaron,
                KingMimicGoT, MimicGoT, Gol, Sil,
                // Overseas
                Yammich, Opar, KingPrickly, Nikapous,
                // Demon Shaft
                MimicDS, KingMimicDS,
                GemronFire, GemronIce, GemronThunder, GemronWind, GemronHoly,
                BishopQ, Gacious, SilverGear, HornHead,
                // Bosses
                IceArrow,
                Dran, IceQueen, MasterUtan,
                KingsCurseCoffin,
                MinotaurJoe,
                DarkGenie, DarkGenieForm2, RightHand, LeftHand, WineKeg,
                DarkGenieFinal,   // final-form boss (Id 223); its companions have Id 0 (collide), kept out like the c17_ ones
                KingsCurse,
                BlackKnight,
            };
            Defaults = new Dictionary<ushort, EnemyDefaults>(all.Length);
            foreach (EnemyDefaults e in all) Defaults[e.Id] = e;
        }

        internal static readonly Dictionary<ushort, string> NormalEnemies = new()
        {
            { MasterJacket.Id,    MasterJacket.Name    }, { SkeletonSoldier.Id, SkeletonSoldier.Name },
            { Statue.Id,          Statue.Name          }, { Dasher.Id,          Dasher.Name          },
            { Werewolf.Id,        Werewolf.Name        }, { FliFli.Id,          FliFli.Name          },
            { Halloween.Id,       Halloween.Name       }, { CannibalPlant.Id,   CannibalPlant.Name   },
            { EarthDigger.Id,     EarthDigger.Name     }, { Sunday.Id,          Sunday.Name          },
            { Monday.Id,          Monday.Name          }, { Tuesday.Id,         Tuesday.Name         },
            { Wednesday.Id,       Wednesday.Name       }, { Thursday.Id,        Thursday.Name        },
            { Friday.Id,          Friday.Name          }, { Saturday.Id,        Saturday.Name        },
            { Gunny.Id,           Gunny.Name           }, { Gyon.Id,            Gyon.Name            },
            { PiratesChariot.Id,  PiratesChariot.Name  }, { AuntieMedu.Id,      AuntieMedu.Name      },
            { Captain.Id,         Captain.Name         }, { Corcea.Id,          Corcea.Name          },
            { Golem.Id,           Golem.Name           }, { MrBlare.Id,         MrBlare.Name         },
            { Dune.Id,            Dune.Name            }, { Titan.Id,           Titan.Name           },
            { KingMimicDBC.Id,    KingMimicDBC.Name    }, { MimicDBC.Id,        MimicDBC.Name        },
            { KingMimicSMT.Id,    KingMimicSMT.Name    }, { MimicSMT.Id,        MimicSMT.Name        },
            { KingMimicMS.Id,     KingMimicMS.Name     }, { MimicMS.Id,         MimicMS.Name         },
            { Arthur.Id,          Arthur.Name          }, { Alexander.Id,       Alexander.Name       },
            { Heart.Id,           Heart.Name           }, { Club.Id,            Club.Name            },
            { Diamond.Id,         Diamond.Name         }, { Spade.Id,           Spade.Name           },
            { Joker.Id,           Joker.Name           }, { BomberHead.Id,      BomberHead.Name      },
            { Mummy.Id,           Mummy.Name           }, { CurseDancer.Id,     CurseDancer.Name     },
            { KillerSnake.Id,     KillerSnake.Name     }, { LivingArmor.Id,     LivingArmor.Name     },
            { WhiteFang.Id,       WhiteFang.Name       }, { MoonBug.Id,         MoonBug.Name         },
            { Dragon.Id,          Dragon.Name          }, { HellPockle.Id,      HellPockle.Name      },
            { RashDasher.Id,      RashDasher.Name      }, { SteelGiant.Id,      SteelGiant.Name      },
            { Blizzard.Id,        Blizzard.Name        }, { MoonDigger.Id,      MoonDigger.Name      },
            { DarkFlower.Id,      DarkFlower.Name      }, { CursedRose.Id,      CursedRose.Name      },
            { Billy.Id,           Billy.Name           }, { Vulcan.Id,          Vulcan.Name          },
            { CrabbyHermit.Id,    CrabbyHermit.Name    }, { SpaceGyon.Id,       SpaceGyon.Name       },
            { BlueDragon.Id,      BlueDragon.Name      }, { BlackDragon.Id,     BlackDragon.Name     },
            { MaskOfPrajna.Id,    MaskOfPrajna.Name    }, { CrescentBaron.Id,   CrescentBaron.Name   },
            { Rockanoff.Id,       Rockanoff.Name       }, { KingMimicWOF.Id,    KingMimicWOF.Name    },
            { MimicWOF.Id,        MimicWOF.Name        }, { KingMimicSW.Id,     KingMimicSW.Name     },
            { MimicSW.Id,         MimicSW.Name         }, { KingMimicGoT.Id,    KingMimicGoT.Name    },
            { MimicGoT.Id,        MimicGoT.Name        }, { Sam.Id,             Sam.Name             },
            { Yammich.Id,         Yammich.Name         }, { StatueDog.Id,       StatueDog.Name       },
            { Opar.Id,            Opar.Name            }, { HaleyHoley.Id,      HaleyHoley.Name      },
            { KingPrickly.Id,     KingPrickly.Name     }, { Nikapous.Id,        Nikapous.Name        },
            { MimicDS.Id,         MimicDS.Name         }, { KingMimicDS.Id,     KingMimicDS.Name     },
            { GemronFire.Id,      GemronFire.Name      }, { GemronIce.Id,       GemronIce.Name       },
            { GemronThunder.Id,   GemronThunder.Name   }, { GemronWind.Id,      GemronWind.Name      },
            { GemronHoly.Id,      GemronHoly.Name      }, { BishopQ.Id,         BishopQ.Name         },
            { Gacious.Id,         Gacious.Name         }, { SilverGear.Id,      SilverGear.Name      },
            { HornHead.Id,        HornHead.Name        },
        };

        internal static readonly Dictionary<ushort, string> FlyingEnemies = new()
        {
            { Hornet.Id,      Hornet.Name      },
            { WitchHellza.Id, WitchHellza.Name },
            { WitchIllza.Id,  WitchIllza.Name  },
            { Ghost.Id,       Ghost.Name       },
            { Lich.Id,        Lich.Name        },
            { Phantom.Id,     Phantom.Name     },
            { CaveBat.Id,     CaveBat.Name     },
            { EvilBat.Id,     EvilBat.Name     },
            { Gol.Id,         Gol.Name         },  // non-flying, but cannot drop an item
            { Sil.Id,         Sil.Name         },  // non-flying, but cannot drop an item
        };
        // Overseas enemies appear in the USA/PAL version of Dark Cloud but are absent
        // from the Japanese release. Pool assignments match the Japanese versions of the
        // same dungeons (DBC area).

        internal static readonly Dictionary<ushort, string> OverseasEnemies = new()
        {
            { Yammich.Id,       Yammich.Name       }, { StatueDog.Id,     StatueDog.Name     },
            { Opar.Id,          Opar.Name          }, { HaleyHoley.Id,    HaleyHoley.Name    },
            { KingPrickly.Id,   KingPrickly.Name   }, { Nikapous.Id,      Nikapous.Name      },
            { MimicDS.Id,       MimicDS.Name       }, { KingMimicDS.Id,   KingMimicDS.Name   },
            { GemronFire.Id,    GemronFire.Name    }, { GemronIce.Id,     GemronIce.Name     },
            { GemronThunder.Id, GemronThunder.Name }, { GemronWind.Id,    GemronWind.Name    },
            { GemronHoly.Id,    GemronHoly.Name    }, { BishopQ.Id,       BishopQ.Name       },
            { Gacious.Id,       Gacious.Name       }, { SilverGear.Id,    SilverGear.Name    },
            { HornHead.Id,      HornHead.Name      },
        };

        internal static readonly Dictionary<ushort, string> BossEnemies = new()
        {
            { IceArrow.Id,    IceArrow.Name    }, { Dran.Id,        Dran.Name        },
            { IceQueen.Id,    IceQueen.Name    }, { MasterUtan.Id,  MasterUtan.Name  },
            { KingsCurseCoffin.Id,  KingsCurseCoffin.Name  }, { MinotaurJoe.Id, MinotaurJoe.Name },
            { DarkGenie.Id,   DarkGenie.Name   }, { RightHand.Id,   RightHand.Name   },
            { LeftHand.Id,    LeftHand.Name    }, { WineKeg.Id,     WineKeg.Name     },
            { BlackKnight.Id, BlackKnight.Name },
            // Ice Queen's eid-0 companions, tagged with synthetic ids (see BossScriptPatcher.TagCompanions) so the
            // logs identify them and they're excluded from miniboss scaling. Not real game enemy ids.
            { 240, "Ice Barrier" },     { 241, "Ice Prison" },    { 242, "Ice Meteor" },
            { 243, "Ice Tornado" }, { 244, "Ice Aura" },
        };
    }

}
