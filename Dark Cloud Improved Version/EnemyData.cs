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
        // +0x020: unknown float — semantics unconfirmed; see ModelScaleOffsets.Unk020.
        internal float? ModelUnk020;
        // +0x024: unknown float — semantics unconfirmed; see ModelScaleOffsets.Unk024.
        internal float? ModelUnk024;
        // +0x028: unknown float — semantics unconfirmed; see ModelScaleOffsets.Unk028.
        internal float? ModelUnk028;
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
    internal static class Enemies
    {
        // ── Divine Beast Cave (dungeon 0) ─────────────────────────────────────

        // confirmed from clean dump 2026-05-30, DBC game fl.6
        // Motions: e03a.chr @ data.dat 0x1b370800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–70	ダミー	(dummy)
        // 15	50–70	ダミー	(dummy)
        // 16	75–95	攻撃2	attack2
        // 17	75–95	ダミー	(dummy)
        // 18	75–95	ダミー	(dummy)
        internal static readonly EnemyDefaults SkeletonSoldier = new EnemyDefaults {
            Id=3, TableIndex=1, ModelCode="e03a",  Name="Skeleton Soldier", MaxHp=23,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=null, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1080, ModelAnimCount=19, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{20,21}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        // Motions: e01a.chr @ data.dat 0x1b260000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–61	振りかぶり	wind-up
        // 15	61–70	振り下ろし	downswing
        // 16	75–95	攻撃2	attack2
        // 17	75–95	ダミー	(dummy)
        // 18	75–95	ダミー	(dummy)
        // 19	280–295	飛び込み	lunge
        internal static readonly EnemyDefaults MasterJacket = new EnemyDefaults {
            Id=1, TableIndex=0, ModelCode="e01a",  Name="Master Jacket",    MaxHp=75,  Abs=5,  MinGoldDrop=7,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80,  ThunderRes=100, WindRes=80,  HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=177, ItemResA=100, ItemResB=80,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1123, ModelAnimCount=20, AttackPower=150, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,30}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        // Motions: e05a.chr @ data.dat 0x1b4c6000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	30–50	ダミー	(dummy)
        // 3	30–50	ダミー	(dummy)
        // 4	30–50	ダミー	(dummy)
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	10–20	ダミー	(dummy)
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	180–210	死亡	death
        // 12	60–80	攻撃1	attack1
        // 13	60–80	ダミー	(dummy)
        // 14	60–80	ダミー	(dummy)
        // 15	90–120	攻撃2	attack2
        // 16	90–104	ジャンプ	jump
        // 17	104–120	アタック	アタック
        internal static readonly EnemyDefaults Statue = new EnemyDefaults {
            Id=5, TableIndex=2, ModelCode="e05a",  Name="Statue",           MaxHp=38,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=20,
            ReticleWidth=1.2f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=792,  ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{26,25,26,25}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.10; spans both pools
        // Motions: e06a.chr @ data.dat 0x1b57c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	120–140	バックステップ	back step
        // 3	60–80	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡ループ	death loop
        // 13	150–180	攻撃1(縦振り）	attack1(縦振り)
        // 14	150–180	攻撃1予備動作１	attack1予備動作１
        // 15	150–180	攻撃1予備動作２	attack1予備動作２
        // 16	190–215	攻撃２（走り頭突き入り）	attack２(run頭突き(enter))
        // 17	215–223	攻撃２予備動作１（走りリピート）	attack２予備動作１(runリピート)
        // 18	223–230	攻撃２予備動作２ （走り頭突き終わり）	attack２予備動作２ (run頭突き終わり)
        internal static readonly EnemyDefaults Dasher = new EnemyDefaults {
            Id=6, TableIndex=3, ModelCode="e06a",  Name="Dasher",           MaxHp=23,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=1, WeaponDefense=0,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=148, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{22}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        // Motions: e60a.chr @ data.dat 0x1d2e9800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 3	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 4	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 5	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 6	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 7	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 8	70–80	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 10	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 11	90–100	死亡始まり	death始まり
        // 12	100–105	落下ﾙｰﾌﾟ	落下ﾙｰﾌﾟ
        // 13	105–115	死亡	death
        // 14	115	死亡停止	death停止
        // 15	120–145	攻撃１	attack１
        // 16	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 17	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	150–171	攻撃2予備動作	attack2予備動作
        // 19	171–189	攻撃	attack
        // 20	189–215	戻り	(return)
        internal static readonly EnemyDefaults CaveBat = new EnemyDefaults {
            Id=60, TableIndex=52, ModelCode="e60a", Name="Cave Bat",         MaxHp=12,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=0.8f, ReticleHeight=0.8f, StealItemId=151, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=940,  ModelAnimCount=21, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{17,16}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.6
        // Motions: e35a.chr @ data.dat 0x1c3d0000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	蓋閉じてる	蓋閉じてる
        internal static readonly EnemyDefaults MimicDBC = new EnemyDefaults {
            Id=35, TableIndex=30, ModelCode="e35a", Name="Mimic (Divine Beast Cave)", MaxHp=68, Abs=3, MinGoldDrop=10, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=1, WeaponDefense=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=920,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{33,33}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.10/fl.14
        // Motions: e42a.chr @ data.dat 0x1c61a800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	バックステップ	back step
        // 3	120–140	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡 ループ	death (loop)
        // 13	150–190	攻撃1	attack1
        // 14	150–190	攻撃1予備動作１	attack1予備動作１
        // 15	150–190	攻撃1予備動作２	attack1予備動作２
        // 16	200–230	攻撃２	attack２
        // 17	200–230	攻撃２予備動作１	attack２予備動作１
        // 18	200–215	ワープ開始	ワープ開始
        // 19	215	ワープ中（要らない？）	ワープ中(要らない？)
        // 20	215–230	ワープ終了	ワープ終了
        internal static readonly EnemyDefaults Ghost = new EnemyDefaults {
            Id=42, TableIndex=36, ModelCode="e42a", Name="Ghost",            MaxHp=15,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=3.6f, EntityScaleCopy=3.6f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.1f, ReticleHeight=1.1f, StealItemId=135, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1220, ModelAnimCount=21, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{20,20}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.14
        // Motions: e59a.chr @ data.dat 0x1d223800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	230–240	ダメージ１	damage１
        // 9	150–160	ダメージ２（後ろに下がる）	damage２(後ろに下がる)
        // 10	30–50	起き上がり(歩きで移動）	get up(walkでmove)
        // 11	250–260	死亡	death
        // 12	260	死亡ループ	death loop
        // 13	310–350	攻撃1 （火の球）	attack1 (fireの球)
        // 14	310–350	攻撃1予備動作１	attack1予備動作１
        // 15	310–350	攻撃1予備動作２	attack1予備動作２
        // 16	270–300	攻撃２（頭突き）	attack２(頭突き)
        // 17	270–300	攻撃２予備動作１	attack２予備動作１
        // 18	270–300	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Dragon = new EnemyDefaults {
            Id=59, TableIndex=51, ModelCode="e59a", Name="Dragon",           MaxHp=90,  Abs=5,  MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=5, WeaponDefense=40,
            ReticleWidth=2.9f, ReticleHeight=2.7f, StealItemId=161, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=32.0f, ModelUnk028=60.0f, ModelDataSize=1422, ModelAnimCount=19, AttackPower=85, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{45,45}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        // Motions: e34a.chr @ data.dat 0x1c354800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicDBC = new EnemyDefaults {
            Id=34, TableIndex=29, ModelCode="e34a", Name="King Mimic (Divine Beast Cave)", MaxHp=90, Abs=4, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,30,35}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        // Motions: e77a.chr @ data.dat 0x1db53000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バック	バック
        // 3	70–80	右	右
        // 4	90–100	左	左
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	110–120	ダメージ1	damage1
        // 9	130–150	ダメージ2	damage2
        // 10	150–160	起き上がり	get up
        // 11	170–190	死亡	death
        // 12	190	死亡ループ	death loop
        // 13	200–210	攻撃1	attack1
        // 14	210–220	攻撃1	attack1
        // 15	220–230	攻撃1	attack1
        // 16	240–260	攻撃2	attack2
        // 17	240–260	攻撃2	attack2
        // 18	240–260	攻撃2	attack2
        internal static readonly EnemyDefaults Rockanoff = new EnemyDefaults {
            Id=77, TableIndex=69, ModelCode="e77a", Name="Rockanoff",        MaxHp=30,  Abs=3,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=954,  ModelAnimCount=19, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, DBC game fl.5
        // Motions: e103a.chr @ data.dat 0x1e15d000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–15	立ち	idle
        // 1	20–40	歩き	walk
        // 2	45–55	バックステップ	back step
        // 3	60–70	右ステップ	right step
        // 4	75–85	左ステップ	left step
        // 5	90–100	ガード入り	guard (enter)
        // 6	100	ガードループ	guard (loop)
        // 7	105–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–160	死亡	death
        // 10	160	死亡L	deathL
        // 11	165–180	攻撃1	attack1
        internal static readonly EnemyDefaults StatueDog = new EnemyDefaults {
            Id=303, TableIndex=97, ModelCode="e103", Name="Statue Dog",      MaxHp=15,  Abs=2,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=160, ItemResA=90,  ItemResB=100,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=667,  ModelAnimCount=12, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        // ── Wise Owl Forest (dungeon 1) ───────────────────────────────────────

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.10; spans both pools
        // from static table 2026-06-04
        // Motions: e08a.chr @ data.dat 0x1b6b2000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	100–110	バックステップ	back step
        // 3	60–70	右ステップ	right step
        // 4	80–90	左ステップ	left step
        // 5	115–120	ガード入り	guard (enter)
        // 6	120–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	144–150	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	235	死亡ループ	death loop
        // 12	160–180	攻撃1	attack1
        // 13	160–180	ダミー	(dummy)
        // 14	160–180	ダミー	(dummy)
        // 15	190–200	攻撃2	attack2
        // 16	190–200	ダミー	(dummy)
        // 17	190–200	ダミー	(dummy)
        internal static readonly EnemyDefaults FliFli = new EnemyDefaults {
            Id=8, TableIndex=5, ModelCode="e08a", Name="FliFli",            MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=169, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{37}, ProjectileDamage=new int[]{35} };

        // Motions: e11a.chr @ data.dat 0x1b808800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	35–55	ダメージ	damage
        // 2	60–80	死亡	death
        // 3	80	死亡ループ	death loop
        // 4	85–120	攻撃葉	attack葉
        // 5	125–160	攻撃液前	attack液前
        // 6	165–200	攻撃液上	attack液上
        internal static readonly EnemyDefaults CannibalPlant = new EnemyDefaults {
            Id=11, TableIndex=8, ModelCode="e11a", Name="Cannibal Plant",   MaxHp=60,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=167, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=21.0f, ModelUnk028=60.0f, ModelDataSize=474,  ModelAnimCount=7, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{28,28,36}, ProjectileDamage=new int[]{1} };

        // confirmed from clean dump 2026-05-30, WOF game fl.7/fl.14; spans both pools
        // Motions: e14a.chr @ data.dat 0x1b964000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–280	攻撃1（出刃包丁）	attack1(出刃包丁)
        // 14	255–280	攻撃1予備動作１	attack1予備動作１
        // 15	255–280	攻撃1予備動作２	attack1予備動作２
        // 16	285–297	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	285–297	攻撃8予備動作１	attack8予備動作１
        // 18	285–297	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Sunday = new EnemyDefaults {
            Id=14, TableIndex=10, ModelCode="e14a", Name="Sunday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=170, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1454, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{36,26}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.7
        // Motions: e15a.chr @ data.dat 0x1b9e5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–270	攻撃2（槍）	attack2(槍)
        // 14	255–270	攻撃2予備動作１	attack2予備動作１
        // 15	255–270	攻撃2予備動作２	attack2予備動作２
        // 16	280–292	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	280–292	攻撃8予備動作１	attack8予備動作１
        // 18	280–292	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Monday = new EnemyDefaults {
            Id=15, TableIndex=11, ModelCode="e15a", Name="Monday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{32,12}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.7
        // Motions: e16a.chr @ data.dat 0x1ba63800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–275	攻撃3（吹き矢）	attack3(吹き矢)
        // 14	255–275	攻撃3予備動作１	attack3予備動作１
        // 15	255–275	攻撃3予備動作２	attack3予備動作２
        // 16	285–297	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	285–297	攻撃8予備動作１	attack8予備動作１
        // 18	285–297	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Tuesday = new EnemyDefaults {
            Id=16, TableIndex=12, ModelCode="e16a", Name="Tuesday",          MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1427, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{31}, ProjectileDamage=new int[]{31} };

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.7
        // Motions: e17a.chr @ data.dat 0x1bae3000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–280	攻撃4（斧/飛んで前に出る）	attack4(斧/飛んで前に出る)
        // 14	255–280	攻撃4予備動作１	attack4予備動作１
        // 15	255–280	攻撃4予備動作２	attack4予備動作２
        // 16	290–302	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	290–302	攻撃8予備動作１	attack8予備動作１
        // 18	290–302	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Wednesday = new EnemyDefaults {
            Id=17, TableIndex=13, ModelCode="e17a", Name="Wednesday",        MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{30,28}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; name follows day-of-week series (Sun=14…Fri=19); stats match confirmed day mages exactly; needs dump confirmation
        // Motions: e18a.chr @ data.dat 0x1bb61000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–275	攻撃5（ランダム投げ）	attack5(ランダムthrow)
        // 14	255–275	攻撃5予備動作１	attack5予備動作１
        // 15	255–275	攻撃5予備動作２	attack5予備動作２
        // 16	285–297	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	285–297	攻撃8予備動作１	attack8予備動作１
        // 18	285–297	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Thursday = new EnemyDefaults {
            Id=18, TableIndex=14, ModelCode="e18a", Name="Thursday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{29}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.5
        // Motions: e19a.chr @ data.dat 0x1bbe0000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–262	攻撃6（ジャンプキック）	attack6(jumpキック)
        // 14	262–271	攻撃6予備動作１	attack6予備動作１
        // 15	271–275	攻撃6予備動作２	attack6予備動作２
        // 16	285–297	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	285–297	攻撃8予備動作１	attack8予備動作１
        // 18	285–297	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Friday = new EnemyDefaults {
            Id=19, TableIndex=15, ModelCode="e19a", Name="Friday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=148, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{29,29}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; name follows day-of-week series; stats match confirmed day mages exactly; needs dump confirmation
        // Motions: e20a.chr @ data.dat 0x1bc5d800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–280	攻撃7（パンチ）	attack7(パンチ)
        // 14	255–280	攻撃7予備動作１	attack7予備動作１
        // 15	255–280	攻撃7予備動作２	attack7予備動作２
        // 16	290–302	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	290–302	攻撃8予備動作１	attack8予備動作１
        // 18	290–302	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults Saturday = new EnemyDefaults {
            Id=20, TableIndex=16, ModelCode="e20a", Name="Saturday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=148, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{29,29,25}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.7/fl.14/fl.16; spans both pools
        // Motions: e22a.chr @ data.dat 0x1bd64000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	移動ループ前	move loop (fwd)
        // 2	50–60	移動ループ後	move loop (back)
        // 3	70–80	移動ループ右	move loop右
        // 4	90–100	移動ループ左	move loop左
        // 5	165–170	ガード入り	guard (enter)
        // 6	170–180	ガードループ	guard (loop)
        // 7	180–185	ガード戻り	guard (return)
        // 8	195–205	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	215–235	死亡	death
        // 12	110–130	攻撃1	attack1
        // 13	60–80	ダミー	(dummy)
        // 14	60–80	ダミー	(dummy)
        // 15	60–80	ダミー	(dummy)
        // 16	60–80	ダミー	(dummy)
        // 17	60–80	ダミー	(dummy)
        internal static readonly EnemyDefaults WitchIllza = new EnemyDefaults {
            Id=22, TableIndex=18, ModelCode="e22a", Name="Witch Illza",      MaxHp=120, Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=90,  IceRes=90,  ThunderRes=90,  WindRes=90,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=169, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=868,  ModelAnimCount=18, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{47}, ProjectileDamage=new int[]{2} };

        // confirmed from clean dump 2026-05-30, WOF game fl.5
        // Motions: e79a.chr @ data.dat 0x1dc01800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	出現	appear
        internal static readonly EnemyDefaults MimicWOF = new EnemyDefaults {
            Id=79, TableIndex=71, ModelCode="e79a", Name="Mimic (Wise Owl Forest)", MaxHp=90,  Abs=3,  MinGoldDrop=6,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=914,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{47,47}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.10; spans both pools
        // Motions: e105a.chr @ data.dat 0x1e789800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	220–240	バックステップ	back step
        // 3	190–215	右ステップ	right step
        // 4	160–185	左ステップ	left step
        // 5	60–75	もぐる	もぐる
        // 6	75–85	もぐりるーぷ	もぐりるーぷ
        // 7	85–100	這い出る	這い出る
        // 8	110–120	ダメージ	damage
        // 9	110–120	ダメージ	damage
        // 10	30–50	ダミー	(dummy)
        // 11	130–150	死亡	death
        // 12	150	死亡ループ	death loop
        // 13	250–265	攻撃予備動作1	attack予備動作1
        // 14	300–310	攻撃	attack
        // 15	275–290	戻り	(return)
        // 16	265–275	攻撃2	attack2
        internal static readonly EnemyDefaults HaleyHoley = new EnemyDefaults {
            Id=305, TableIndex=99, ModelCode="e105", Name="Haley Holey",     MaxHp=50,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=140, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.0f, ReticleHeight=1.1f, StealItemId=186, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1046, ModelAnimCount=19, AttackPower=189, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{37,37,37}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.14/fl.16
        // Motions: e07a.chr @ data.dat 0x1b5e7000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	90–110	バックステップ	back step
        // 3	190–210	右ステップ	right step
        // 4	215–235	左ステップ	left step
        // 5	240–250	ガード入り	guard (enter)
        // 6	250–260	ガードループ	guard (loop)
        // 7	260–270	ガード戻り	guard (return)
        // 8	115–125	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	130–160	死亡	death
        // 12	160	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–70	ダミー	(dummy)
        // 15	50–70	ダミー	(dummy)
        // 16	70–90	攻撃2	attack2
        // 17	70–90	ダミー	(dummy)
        // 18	70–90	ダミー	(dummy)
        internal static readonly EnemyDefaults Werewolf = new EnemyDefaults {
            Id=7, TableIndex=4, ModelCode="e07a",  Name="Werewolf",         MaxHp=180, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=5, WeaponDefense=0,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=174, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1111, ModelAnimCount=19, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{68,62}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.10
        // Motions: e09a.chr @ data.dat 0x1b6fd800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	ホバリング	ホバリング
        // 1	40–50	飛行	flight
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	10–30	ホバリング	ホバリング
        // 6	10–30	ホバリング	ホバリング
        // 7	10–30	ホバリング	ホバリング
        // 8	120–140	ダメージ	damage
        // 9	10–30	ホバリング	ホバリング
        // 10	10–30	ホバリング	ホバリング
        // 11	150–170	死亡入り	death(enter)
        // 12	270–280	死亡落ちループ	death落ち(loop)
        // 13	290–310	死亡	death
        // 14	310	死亡ループ	death loop
        // 15	180–210	攻撃1	attack1
        // 16	180–210	攻撃1	attack1
        // 17	180–210	攻撃1	attack1
        // 18	220–260	攻撃2	attack2
        // 19	220–260	攻撃2	attack2
        // 20	220–260	攻撃2	attack2
        internal static readonly EnemyDefaults Hornet = new EnemyDefaults {
            Id=9, TableIndex=6, ModelCode="e09a",  Name="Hornet",           MaxHp=60,  Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=120, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{42,40}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, WOF game fl.14/fl.16
        // Motions: e10a.chr @ data.dat 0x1b75f000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–45	前移動ループ	move loop (fwd)
        // 2	30–45	ダミー	(dummy)
        // 3	110–130	右ステップ	right step
        // 4	140–160	左ステップ	left step
        // 5	170–180	ガード入り	guard (enter)
        // 6	180–190	ガードループ	guard (loop)
        // 7	190–200	ガード戻り	guard (return)
        // 8	210–220	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	230–255	死亡	death
        // 12	55–79	攻撃1	attack1
        // 13	265–285	バックステップ	back step
        // 14	295–315	歩き	walk
        // 15	85–103	攻撃2	attack2
        // 16	85–103	ダミー	(dummy)
        // 17	85–103	ダミー	(dummy)
        internal static readonly EnemyDefaults Halloween = new EnemyDefaults {
            Id=10, TableIndex=7, ModelCode="e10a", Name="Halloween",        MaxHp=150, Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=10,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=168, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=850,  ModelAnimCount=18, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{57,57}, ProjectileDamage=new int[]{60} };

        // confirmed from clean dump 2026-05-30, WOF game fl.10/fl.16
        // Motions: e12a.chr @ data.dat 0x1b87d800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	10–20	ダミー	(dummy)
        // 3	10–20	ダミー	(dummy)
        // 4	10–20	ダミー	(dummy)
        // 5	60–70	もぐり	もぐり
        // 6	70–80	もぐりループ	もぐり(loop)
        // 7	80–90	出現	appear
        // 8	100–110	ダメージ	damage
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	120–140	攻撃1	attack1
        // 14	120–140	ダミー	(dummy)
        // 15	120–140	ダミー	(dummy)
        // 16	150–170	攻撃2	attack2
        // 17	150–170	ダミー	(dummy)
        // 18	150–170	ダミー	(dummy)
        internal static readonly EnemyDefaults EarthDigger = new EnemyDefaults {
            Id=12, TableIndex=9, ModelCode="e12a", Name="Earth Digger",     MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=188, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=936,  ModelAnimCount=19, AttackPower=197, ElemAtkFire=50, ElemAtkIce=50, ElemAtkThunder=120, ElemAtkWind=50, ElemAtkHoly=50, ElemAtkDark=50,
            MeleeDamage=new int[]{52}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; stronger WitchIllza variant; needs dump confirmation
        // Motions: e21a.chr @ data.dat 0x1bcc9000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	移動ループ前	move loop (fwd)
        // 2	50–60	移動ループ後	move loop (back)
        // 3	70–80	移動ループ右	move loop右
        // 4	90–100	移動ループ左	move loop左
        // 5	165–170	ガード入り	guard (enter)
        // 6	170–180	ガードループ	guard (loop)
        // 7	180–185	ガード戻り	guard (return)
        // 8	195–205	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	215–235	死亡	death
        // 12	110–128	攻撃1	attack1
        // 13	110–128	ダミー	(dummy)
        // 14	110–128	ダミー	(dummy)
        // 15	110–128	ダミー	(dummy)
        // 16	110–128	ダミー	(dummy)
        // 17	110–128	ダミー	(dummy)
        internal static readonly EnemyDefaults WitchHellza = new EnemyDefaults {
            Id=21, TableIndex=17, ModelCode="e21a", Name="Witch Hellza",     MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=70,  IceRes=70,  ThunderRes=70,  WindRes=70,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=169, ItemResA=85, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{73}, ProjectileDamage=new int[]{2} };

        // from static table 2026-06-04; same model/stats tier as KingMimicDBC but different code; needs dump confirmation
        // Motions: e78a.chr @ data.dat 0x1db86800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicWOF = new EnemyDefaults {
            Id=78, TableIndex=70, ModelCode="e78a", Name="King Mimic (Wise Owl Forest)", MaxHp=150, Abs=10, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{67,56,45,50}, ProjectileDamage=new int[]{} };

        // ── Shipwreck (dungeon 2) ──────────────────────────────────────────────

        // confirmed from clean dump 2026-05-30, SW game fl.8/fl.10; spans both pools
        // Motions: e27a.chr @ data.dat 0x1bfc4800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	歩き	walk
        // 1	30–50	歩き	walk
        // 2	115–125	バックステップ	back step
        // 3	140–150	右ステップ	right step
        // 4	160–170	左ステップ	left step
        // 5	180–184	ガード入り	guard (enter)
        // 6	184–188	ガードループ	guard (loop)
        // 7	188–192	ガード戻り	guard (return)
        // 8	200–210	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	235	死亡ループ	death loop
        // 13	60–81	攻撃1	attack1
        // 14	60–81	ダミー	(dummy)
        // 15	60–81	ダミー	(dummy)
        // 16	90–106	攻撃2	attack2
        // 17	90–106	ダミー	(dummy)
        // 18	90–106	ダミー	(dummy)
        internal static readonly EnemyDefaults Captain = new EnemyDefaults {
            Id=27, TableIndex=23, ModelCode="e27a", Name="Captain",          MaxHp=225, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=177, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=873,  ModelAnimCount=19, AttackPower=227, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{74,74,72,72}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, SW game fl.10
        // Motions: e25a.chr @ data.dat 0x1bee3800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	後退	後退
        // 3	90–110	右旋回	右turn
        // 4	120–140	左旋回	左turn
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	150–160	ダメージ	damage
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	170–185	死亡	death
        // 12	185	死亡ループ	death loop
        // 13	190–210	攻撃1	attack1
        // 14	190–210	ダミー	(dummy)
        // 15	190–210	ダミー	(dummy)
        // 16	250–280	攻撃2予備動作	attack2予備動作
        // 17	250–280	攻撃	attack
        // 18	250–280	戻り	(return)
        internal static readonly EnemyDefaults PiratesChariot = new EnemyDefaults {
            Id=25, TableIndex=21, ModelCode="e25a", Name="Pirate's Chariot", MaxHp=270, Abs=8,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80,  ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=5, WeaponDefense=30,
            ReticleWidth=1.9f, ReticleHeight=1.8f, StealItemId=159, ItemResA=95,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=835,  ModelAnimCount=19, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{69} };

        // confirmed from clean dump 2026-05-30, SW game fl.10; Unk020=14.0/Unk028=0.0 — ranged gun enemy
        // Motions: e23a.chr @ data.dat 0x1bde9000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–42	歩き	walk
        // 2	70–80	バックステップ	back step
        // 3	90–100	右ステップ	right step
        // 4	110–120	左ステップ	left step
        // 5	130–136	ガード入り	guard (enter)
        // 6	136–142	ガードループ	guard (loop)
        // 7	142–146	ガード戻り	guard (return)
        // 8	150–170	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–190	死亡	death
        // 12	190	死亡ループ	death loop
        // 13	200–215	攻撃1(はさみ）	attack1(はさみ)
        // 14	200–215	攻撃1予備動作１	attack1予備動作１
        // 15	200–215	攻撃1予備動作２	attack1予備動作２
        // 16	220–245	攻撃２（口泡）	attack２(口泡)
        // 17	220–245	攻撃２予備動作１	attack２予備動作１
        // 18	220–245	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Gunny = new EnemyDefaults {
            Id=23, TableIndex=19, ModelCode="e23a", Name="Gunny",            MaxHp=250, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=153, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=20.0f, ModelUnk028=0.0f,  ModelDataSize=1270, ModelAnimCount=19, AttackPower=193, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{44,44}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, SW game fl.3; StealItemId=null (0xFFFF in memory)
        // Motions: e68a.chr @ data.dat 0x1d664000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	35–55	ダメージ	damage
        // 2	60–80	死亡	death
        // 3	80	死亡ループ	death loop
        // 4	85–120	攻撃葉	attack葉
        // 5	125–160	攻撃液前	attack液前
        // 6	165–200	攻撃液上	attack液上
        internal static readonly EnemyDefaults CursedRose = new EnemyDefaults {
            Id=68, TableIndex=60, ModelCode="e68a", Name="Cursed Rose",      MaxHp=225, Abs=4,  MinGoldDrop=6,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=2, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=476,  ModelAnimCount=7, AttackPower=146, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{49,49,48}, ProjectileDamage=new int[]{1} };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        // Motions: e24a.chr @ data.dat 0x1be46000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	バタアシ	バタアシ
        // 2	60–80	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	90–110	右ｽﾃｯﾌﾟ	right step
        // 4	120–140	左ｽﾃｯﾌﾟ	left step
        // 5	150–160	ｶﾞｰﾄﾞ	guard
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	190–200	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	210–225	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	235–260	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	270–290	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	300–325	死亡	death
        // 12	325	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	335–355	攻撃１	attack１
        // 14	365–385	攻撃２	attack２
        // 15	390–410	攻撃3	attack3
        internal static readonly EnemyDefaults Gyon = new EnemyDefaults {
            Id=24, TableIndex=20, ModelCode="e24a", Name="Gyon",             MaxHp=225, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=134, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=849,  ModelAnimCount=16, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{2} };

        // confirmed from clean dump 2026-05-30, SW game fl.17
        // Unk150/154/158 (0x150/154/158) read as 0 for regular Auntie Medu; observed non-zero (127.5/80.0/15.0) when the mod's miniboss process was active on this enemy species.
        // Motions: e26a.chr @ data.dat 0x1bf24800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	左ｽﾃｯﾌﾟ	left step
        // 3	80–90	右ｽﾃｯﾌﾟ	right step
        // 4	100–110	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 5	120–125	ｶﾞｰﾄﾞ	guard
        // 6	125–135	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	135–140	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	150–160	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	165–170	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	175–180	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	185–195	死亡	death
        // 12	195	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	210–230	攻撃１	attack１
        // 14	210–230	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	210–230	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	240–260	攻撃２	attack２
        // 17	240–260	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	240–260	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults AuntieMedu = new EnemyDefaults {
            Id=26, TableIndex=22, ModelCode="e26a", Name="Auntie Medu",      MaxHp=300, Abs=10, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=166, ItemResA=100, ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=944,  ModelAnimCount=19, AttackPower=245, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{60} };

        // confirmed from clean dump 2026-05-30, SW game fl.3
        // Motions: e28a.chr @ data.dat 0x1c068000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	115–125	バックステップ	back step
        // 3	140–150	右ステップ	right step
        // 4	160–170	左ステップ	left step
        // 5	180–184	ガード入り	guard (enter)
        // 6	184–188	ガードループ	guard (loop)
        // 7	188–192	ガード戻り	guard (return)
        // 8	200–210	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	215–235	死亡	death
        // 12	235	死亡ループ	death loop
        // 13	60–81	攻撃1	attack1
        // 14	60–81	ダミー	(dummy)
        // 15	60–81	ダミー	(dummy)
        // 16	90–106	攻撃2	attack2
        // 17	90–106	ダミー	(dummy)
        // 18	90–106	ダミー	(dummy)
        internal static readonly EnemyDefaults Corcea = new EnemyDefaults {
            Id=28, TableIndex=24, ModelCode="e28a", Name="Corcea",           MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=140, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=152, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=871,  ModelAnimCount=19, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{43,43,42,42}, ProjectileDamage=new int[]{} };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17
        // Motions: e75a.chr @ data.dat 0x1da6d800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バックステップ	back step
        // 3	70–80	右ステップ	right step
        // 4	90–100	左ステップ	left step
        // 5	110–115	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	140–150	ダメージ１	damage１
        // 9	160–172	ダメージ２ (吹っ飛び）	damage２ (吹っ飛び)
        // 10	10–20	起き上がり（歩きで移動）	get up(walkでmove)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	260–290	攻撃1（切り裂き）	attack1(切り裂き)
        // 14	260–290	攻撃1予備動作１	attack1予備動作１
        // 15	260–290	攻撃1予備動作２	attack1予備動作２
        // 16	240–255	攻撃２（ファイヤーボール）	attack２(ファイヤーボール)
        // 17	240–255	攻撃２予備動作１	attack２予備動作１
        // 18	240–255	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults MaskOfPrajna = new EnemyDefaults {
            Id=75, TableIndex=67, ModelCode="e75a", Name="Mask of Prajna",   MaxHp=375, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=5, WeaponDefense=10,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=80,  ItemResB=70,
            ModelUnk020=32.0f, ModelUnk024=26.0f, ModelUnk028=0.0f,  ModelDataSize=1398, ModelAnimCount=19, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{80,78}, ProjectileDamage=new int[]{65} };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17;
        // Motions: e86a.chr @ data.dat 0x1ddff000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–130	ガードループ	guard (loop)
        // 7	10–20	ダミー	(dummy)
        // 8	10–20	ダミー	(dummy)
        // 9	220–234	死亡	death
        // 10	234–240	死亡ループ	death loop
        // 11	160–185	攻撃1	attack1
        // 12	160–185	ダミー	(dummy)
        // 13	160–185	ダミー	(dummy)
        // 14	190–215	攻撃2	attack2
        // 15	190–215	ダミー	(dummy)
        // 16	190–215	ダミー	(dummy)
        internal static readonly EnemyDefaults Sam = new EnemyDefaults {
            Id=85, TableIndex=77, ModelCode="e86a", Name="Sam",              MaxHp=180, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.6f, StealItemId=162, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=19.0f, ModelUnk028=0.0f,  ModelDataSize=871,  ModelAnimCount=19, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{64,64,80}, ProjectileDamage=new int[]{-1} };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        // Motions: e81a.chr @ data.dat 0x1dccb000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults MimicSW = new EnemyDefaults {
            Id=81, TableIndex=73, ModelCode="e81a", Name="Mimic (Shipwreck)", MaxHp=150, Abs=4, MinGoldDrop=6, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{} };

        // confirmed from dump 2026-05-31, SW game fl.16; same model/anim data as KingMimicDBC but higher stats and different DamageReduction/WeaponDefense
        // Motions: e80a.chr @ data.dat 0x1dc4f800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicSW = new EnemyDefaults {
            Id=80, TableIndex=72, ModelCode="e80a", Name="King Mimic (Shipwreck)", MaxHp=300, Abs=15, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{84,78,56}, ProjectileDamage=new int[]{} };

        // ── Sun and Moon Temple (dungeon 3) ──────────────────────────────────────

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools; unk020=9.0 (higher than most melee enemies); no steal (0xFFFF in memory)
        // Motions: e50a.chr @ data.dat 0x1cbaa000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–310	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	360–385	攻撃1 （引っかき）	attack1 (claw)
        // 14	360–385	ダミー	(dummy)
        // 15	360–385	ダミー	(dummy)
        // 16	320–350	攻撃2  (ブレス）	attack2 (breath)
        // 17	320–350	ダミー	(dummy)
        // 18	320–350	ダミー	(dummy)
        internal static readonly EnemyDefaults Mummy = new EnemyDefaults {
            Id=50, TableIndex=44, ModelCode="e50a", Name="Mummy",            MaxHp=150, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=9.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1029, ModelAnimCount=19, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{54,54,54}, ProjectileDamage=new int[]{} };

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools
        // Motions: e58a.chr @ data.dat 0x1d1c2800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	ホバリング	ホバリング
        // 1	40–50	飛行	flight
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	10–30	ホバリング	ホバリング
        // 6	10–30	ホバリング	ホバリング
        // 7	10–30	ホバリング	ホバリング
        // 8	120–140	ダメージ	damage
        // 9	10–30	ホバリング	ホバリング
        // 10	10–30	ホバリング	ホバリング
        // 11	150–170	死亡入り	death(enter)
        // 12	270–280	死亡落ちループ	death落ち(loop)
        // 13	290–310	死亡	death
        // 14	310	死亡ループ	death loop
        // 15	180–210	攻撃1	attack1
        // 16	180–210	攻撃1	attack1
        // 17	180–210	攻撃1	attack1
        // 18	220–260	攻撃2	attack2
        // 19	220–260	攻撃2	attack2
        // 20	220–260	攻撃2	attack2
        internal static readonly EnemyDefaults Phantom = new EnemyDefaults {
            Id=58, TableIndex=50, ModelCode="e58a", Name="Phantom",          MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=125, ThunderRes=100, WindRes=125, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{60,54}, ProjectileDamage=new int[]{} };

        // confirmed from dump 2026-05-31, SM game fl.2–7; DamageReduction=8 (large, like Golem); steal=159
        // Motions: e49a.chr @ data.dat 0x1ca46000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–45	ジャンプ歩き	jumpwalk
        // 2	350–370	バックステップ	back step
        // 3	110–130	右ステップ	right step
        // 4	140–160	左ステップ	left step
        // 5	170–180	ガード入り	guard (enter)
        // 6	180–190	ガードループ	guard (loop)
        // 7	190–200	ガード戻り	guard (return)
        // 8	210–220	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	230–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–300	攻撃1(殴り）	attack1(殴り)
        // 14	270–300	攻撃1予備動作1	attack1予備動作1
        // 15	270–300	攻撃1予備動作2	attack1予備動作2
        // 16	310–321	攻撃2 （ジャンプ入り）	attack2 (jump(enter))
        // 17	321–324	攻撃2予備動作1 （ジャンプ中）	attack2予備動作1 (jump中)
        // 18	324–335	攻撃2予備動作2 （着地）	attack2予備動作2 (landing)
        // 19	85–103	攻撃3 （投げ）	attack3 (throw)
        // 20	85–103	攻撃3予備動作1	attack3予備動作1
        // 21	85–103	攻撃3予備動作2	attack3予備動作2
        // 22	380–400	歩き	walk
        internal static readonly EnemyDefaults BomberHead = new EnemyDefaults {
            Id=49, TableIndex=43, ModelCode="e49a", Name="Bomber Head",      MaxHp=180, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=75,  ThunderRes=125, WindRes=100, HolyRes=75,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=8, WeaponDefense=20,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=159, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1663, ModelAnimCount=23, AttackPower=159, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{61,61}, ProjectileDamage=new int[]{64} };

        // confirmed from dump 2026-05-31, SM game fl.3–7; same model as MimicSW (dataSize=918, animCount=20)
        // Motions: e37a.chr @ data.dat 0x1c499800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults MimicSMT = new EnemyDefaults {
            Id=37, TableIndex=32, ModelCode="e37a", Name="Mimic (Sun & Moon Temple)", MaxHp=270, Abs=6, MinGoldDrop=12, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{71,71}, ProjectileDamage=new int[]{} };

        // confirmed from dump 2026-05-31, SM game fl.3–7; scale=14.0 (large body); DamageReduction=8
        // Motions: e30a.chr @ data.dat 0x1c10d000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Golem = new EnemyDefaults {
            Id=30, TableIndex=25, ModelCode="e30a", Name="Golem",            MaxHp=375, Abs=4,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            ReticleWidth=2.4f, ReticleHeight=2.4f, StealItemId=177, ItemResA=100, ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=33.0f, ModelUnk028=60.0f, ModelDataSize=1071, ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{1,-1} };

        // confirmed from dump 2026-05-31, SM game fl.7; unk020=14.0 (ranged/large class); unk028=100.0 (highest observed — aquatic/crab movement class?)
        // Motions: e71a.chr @ data.dat 0x1d7b9800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	30–42	立ち〈ダミー）	idle〈(dummy))
        // 1	30–42	歩き	walk
        // 2	70–80	バックステップ	back step
        // 3	90–100	右ステップ	right step
        // 4	110–120	左ステップ	left step
        // 5	130–136	ガード入り	guard (enter)
        // 6	136–142	ガードループ	guard (loop)
        // 7	142–146	ガード戻り	guard (return)
        // 8	150–170	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–190	死亡	death
        // 12	190	死亡ループ	death loop
        // 13	200–215	攻撃1(はさみ）	attack1(はさみ)
        // 14	200–215	攻撃1予備動作１	attack1予備動作１
        // 15	200–215	攻撃1予備動作２	attack1予備動作２
        // 16	220–245	攻撃２（口泡）	attack２(口泡)
        // 17	220–245	攻撃２予備動作１	attack２予備動作１
        // 18	220–245	攻撃２予備動作２	attack２予備動作２
        // 19	255–275	攻撃２（棘）	attack２(棘)
        // 20	255–275	攻撃２予備動作１	attack２予備動作１
        // 21	255–275	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults CrabbyHermit = new EnemyDefaults {
            Id=71, TableIndex=63, ModelCode="e71a", Name="Crabby Hermit",    MaxHp=300, Abs=4,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=5, WeaponDefense=20,
            ReticleWidth=1.9f, ReticleHeight=1.9f, StealItemId=166, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=22.0f, ModelUnk028=100.0f, ModelDataSize=1612, ModelAnimCount=22, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{83,83,80}, ProjectileDamage=new int[]{76} };

        // from static table 2026-06-04; King Mimic for SM dungeon; needs dump confirmation
        // Motions: e36a.chr @ data.dat 0x1c41e000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicSMT = new EnemyDefaults {
            Id=36, TableIndex=31, ModelCode="e36a", Name="King Mimic (Sun & Moon Temple)", MaxHp=525, Abs=15, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=20,
            StealItemId=174, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{101,102,45}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; needs dump confirmation
        // Motions: e31a.chr @ data.dat 0x1c1b3800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–130	ガードループ	guard (loop)
        // 7	10–20	ダミー	(dummy)
        // 8	10–20	ダミー	(dummy)
        // 9	220–234	死亡	death
        // 10	234	死亡ループ	death loop
        // 11	160–185	攻撃1	attack1
        // 12	160–185	ダミー	(dummy)
        // 13	160–185	ダミー	(dummy)
        // 14	190–215	攻撃2	attack2
        // 15	190–215	ダミー	(dummy)
        // 16	190–215	ダミー	(dummy)
        internal static readonly EnemyDefaults MrBlare = new EnemyDefaults {
            Id=31, TableIndex=26, ModelCode="e31a", Name="Mr. Blare",        MaxHp=225, Abs=5,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=0,   IceRes=170, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=10,
            StealItemId=161, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{81,81}, ProjectileDamage=new int[]{80} };

        // from static table 2026-06-04; needs dump confirmation
        // Motions: e32a.chr @ data.dat 0x1c217800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	70–90	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	100–120	左ｽﾃｯﾌﾟ	left step
        // 4	130–150	右ｽﾃｯﾌﾟ	right step
        // 5	160–180	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	190–200	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	210–230	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	240–270	死亡	death
        // 9	270	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 10	280–310	ｻﾝﾄﾞｱｯﾊﾟｰ	ｻﾝﾄﾞｱｯﾊﾟｰ
        // 11	320–340	爪	爪
        internal static readonly EnemyDefaults Dune = new EnemyDefaults {
            Id=32, TableIndex=27, ModelCode="e32a", Name="Dune",             MaxHp=525, Abs=10, MinGoldDrop=18, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=120, HolyRes=100,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{85,85,85}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; needs dump confirmation
        // Motions: e33a.chr @ data.dat 0x1c2af000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Titan = new EnemyDefaults {
            Id=33, TableIndex=28, ModelCode="e33a", Name="Titan",            MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{105,105,105,105,90,75,60}, ProjectileDamage=new int[]{1,90} };

        // from static table 2026-06-04; playing-card mage set; needs dump confirmation for dungeon assignment
        // Motions: e44a.chr @ data.dat 0x1c6f6000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(ビーム292/hit）	attack1(beam292/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Heart = new EnemyDefaults {
            Id=44, TableIndex=38, ModelCode="e44a", Name="Heart",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=150, ItemResA=80, ItemResB=50, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{107}, ProjectileDamage=new int[]{} };

        // Motions: e45a.chr @ data.dat 0x1c7a0800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(殴る929/hit）	attack1(殴る929/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Club = new EnemyDefaults {
            Id=45, TableIndex=39, ModelCode="e45a", Name="Club",             MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=147, ItemResA=80, ItemResB=50, AttackPower=134, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{104}, ProjectileDamage=new int[]{} };

        // Motions: e46a.chr @ data.dat 0x1c84b800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(つき286/hit）	attack1(つき286/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Diamond = new EnemyDefaults {
            Id=46, TableIndex=40, ModelCode="e46a", Name="Diamond",          MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50,  WindRes=150, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=151, ItemResA=80, ItemResB=50, AttackPower=135, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        // Motions: e47a.chr @ data.dat 0x1c8f2000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(切る290/hit）	attack1(切る290/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Spade = new EnemyDefaults {
            Id=47, TableIndex=41, ModelCode="e47a", Name="Spade",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=152, ItemResA=80, ItemResB=50, AttackPower=132, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{113,113}, ProjectileDamage=new int[]{} };

        // fire=50/ice=50/thu=50/win=50 (resistant to all), holy=150; all-element-resistant mage
        // Motions: e48a.chr @ data.dat 0x1c99b000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(裂く286/hit）	attack1(裂く286/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Joker = new EnemyDefaults {
            Id=48, TableIndex=42, ModelCode="e48a", Name="Joker",            MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=150,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=3, WeaponDefense=0,
            StealItemId=149, ItemResA=50, ItemResB=10, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{115}, ProjectileDamage=new int[]{} };

        // ── Moon Sea (dungeon 4) ──────────────────────────────────────────────────
        // All entries from static table 2026-06-04; pool assignments estimated; needs dump confirmation.

        // Motions: e38a.chr @ data.dat 0x1c4e7800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicMS = new EnemyDefaults {
            Id=38, TableIndex=33, ModelCode="e38a", Name="King Mimic (Moon Sea)", MaxHp=600, Abs=12, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=8, WeaponDefense=30,
            StealItemId=176, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{118,96,90}, ProjectileDamage=new int[]{} };

        // Motions: e39a.chr @ data.dat 0x1c564000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults MimicMS = new EnemyDefaults {
            Id=39, TableIndex=34, ModelCode="e39a", Name="Mimic (Moon Sea)",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=8, WeaponDefense=30,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{83,83}, ProjectileDamage=new int[]{} };

        // Motions: e51a.chr @ data.dat 0x1cdd4000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	バックステップ	back step
        // 3	120–140	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡 ループ	death (loop)
        // 13	150–190	攻撃1	attack1
        // 14	150–190	攻撃1予備動作１	attack1予備動作１
        // 15	150–190	攻撃1予備動作２	attack1予備動作２
        // 16	200–230	攻撃２	attack２
        // 17	200–230	攻撃２予備動作１	attack２予備動作１
        // 18	200–215	ワープ開始	ワープ開始
        // 19	215	ワープ中（要らない？）	ワープ中(要らない？)
        // 20	215–230	ワープ終了	ワープ終了
        internal static readonly EnemyDefaults Lich = new EnemyDefaults {
            Id=51, TableIndex=45, ModelCode="e51a", Name="Lich",             MaxHp=300, Abs=12, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Undead, FireRes=20,  IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=160,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=5, WeaponDefense=0,
            StealItemId=176, ItemResA=80, ItemResB=30, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{114,114}, ProjectileDamage=new int[]{} };

        // Motions: e52a.chr @ data.dat 0x1cf09800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	70–92	バックステップ	back step
        // 3	100–122	右ステップ	right step
        // 4	130–152	左ステップ	left step
        // 5	160–164	ガード入り	guard (enter)
        // 6	164–172	ガードループ	guard (loop)
        // 7	172–176	ガード戻り	guard (return)
        // 8	185–205	ダメージ1	damage1
        // 9	300–310	ダメージ2	damage2
        // 10	320–340	起き上がり	get up
        // 11	210–228	死亡	death
        // 12	229–230	死亡ループ	death loop
        // 13	240–262	攻撃1	attack1
        // 14	240–262	攻撃1予備動作1	attack1予備動作1
        // 15	240–262	攻撃1予備動作2	attack1予備動作2
        // 16	270–293	攻撃2	attack2
        // 17	270–293	攻撃1予備動作1	attack1予備動作1
        // 18	270–293	攻撃2予備動作1	attack2予備動作1
        internal static readonly EnemyDefaults CurseDancer = new EnemyDefaults {
            Id=52, TableIndex=46, ModelCode="e52a", Name="Curse Dancer",     MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=166, ItemResA=100, ItemResB=70, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{90,90,90,90}, ProjectileDamage=new int[]{} };

        // CUT ENEMY — no species table entry and no CHR model file (e53a.chr/e54a.chr absent).
        // Id=54 is referenced in WOF spawn-pool data and its name appears in the game's name table,
        // but the engine skips EIDs 53–54 entirely in the packed sequential species table.
        // TableIndex is null: there is no record in EnemySpeciesTable for this EID.
        // Only appears on WOF event floors 8 and 16 via a scripted spawn that bypasses the
        // normal species-table lookup. Live-slot stats are unknown until an event-floor dump is taken.
        // Motions: e55a.chr @ data.dat 0x1cfde800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	30–50	バックステップ	back step
        // 3	30–50	右ステップ	right step
        // 4	30–50	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–202	死亡	death
        // 12	202	死亡ループ	death loop
        // 13	60–80	攻撃1 (横）	attack1 (横)
        // 14	60–80	攻撃1予備動作１	attack1予備動作１
        // 15	60–80	攻撃1予備動作２	attack1予備動作２
        // 16	90–120	攻撃2 （ランス突き）	attack2 (ランス突き)
        // 17	90–120	攻撃２予備動作１	attack２予備動作１
        // 18	90–120	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults KillerSnake = new EnemyDefaults {
            Id=54, Name="Killer Snake" };

        // Motions: e55a.chr @ data.dat 0x1cfde800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	30–50	バックステップ	back step
        // 3	30–50	右ステップ	right step
        // 4	30–50	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–202	死亡	death
        // 12	202	死亡ループ	death loop
        // 13	60–80	攻撃1 (横）	attack1 (横)
        // 14	60–80	攻撃1予備動作１	attack1予備動作１
        // 15	60–80	攻撃1予備動作２	attack1予備動作２
        // 16	90–120	攻撃2 （ランス突き）	attack2 (ランス突き)
        // 17	90–120	攻撃２予備動作１	attack２予備動作１
        // 18	90–120	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults LivingArmor = new EnemyDefaults {
            Id=55, TableIndex=47, ModelCode="e55a", Name="Living Armor",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80,  HolyRes=80,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=null, ItemResA=100, ItemResB=50, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{95,95,95,95}, ProjectileDamage=new int[]{} };

        // Motions: e56a.chr @ data.dat 0x1d098000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	90–110	バックステップ	back step
        // 3	190–210	右ステップ	right step
        // 4	215–235	左ステップ	left step
        // 5	240–250	ガード入り	guard (enter)
        // 6	250–260	ガードループ	guard (loop)
        // 7	260–270	ガード戻り	guard (return)
        // 8	115–125	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	130–160	死亡	death
        // 12	160	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–70	ダミー	(dummy)
        // 15	50–70	ダミー	(dummy)
        // 16	70–90	攻撃2	attack2
        // 17	70–90	ダミー	(dummy)
        // 18	70–90	ダミー	(dummy)
        internal static readonly EnemyDefaults WhiteFang = new EnemyDefaults {
            Id=56, TableIndex=48, ModelCode="e56a", Name="White Fang",       MaxHp=525, Abs=10, MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=0, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{90,84}, ProjectileDamage=new int[]{} };

        // Motions: e57a.chr @ data.dat 0x1d160000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–75	ﾀﾞﾒｰｼﾞ	damage
        // 3	85–105	死亡	death
        // 4	105	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 5	115–135	攻撃１	attack１
        // 6	145–155	攻撃１ﾙｰﾌﾟ	attack１ﾙｰﾌﾟ
        // 7	165–175	攻撃１戻り	attack１(return)
        // 8	185–235	攻撃２	attack２
        internal static readonly EnemyDefaults MoonBug = new EnemyDefaults {
            Id=57, TableIndex=49, ModelCode="e57a", Name="Moon Bug",         MaxHp=450, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=50,  IceRes=120, ThunderRes=150, WindRes=50,  HolyRes=100,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=8, WeaponDefense=40,
            StealItemId=159, ItemResA=90, ItemResB=70, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{70,70}, ProjectileDamage=new int[]{70} };

        // ── Gallery of Time (dungeon 5) ───────────────────────────────────────────
        // All entries from static table 2026-06-04; pool assignments estimated; needs dump confirmation.

        // Motions: e40a.chr @ data.dat 0x1c5b2800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	70–80	右ｽﾃｯﾌﾟ	right step
        // 4	90–100	左ｽﾃｯﾌﾟ	left step
        // 5	110–120	ｶﾞｰﾄﾞ	guard
        // 6	130–140	ｶﾞｰﾄﾞループ	guard (loop)
        // 7	150–160	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	170–190	ﾀﾞﾒｰｼﾞ	damage
        // 9	200–230	死亡	death
        // 10	230	死亡ループ	death loop
        // 11	240–270	攻撃１	attack１
        // 12	280–310	攻撃２	attack２
        // 13	320–340	攻撃３	attack３
        // 14	350–360	攻撃３ループ	attack３(loop)
        // 15	370–390	攻撃３戻り	attack３(return)
        // 16	0–10	立ち	idle
        internal static readonly EnemyDefaults Arthur = new EnemyDefaults {
            Id=40, TableIndex=35, ModelCode="e40a", Name="Arthur",           MaxHp=600, Abs=15, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=150, WindRes=80,  HolyRes=80,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{116,116}, ProjectileDamage=new int[]{} };

        // Motions: e43a.chr @ data.dat 0x1c6a7800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バックステップ	back step
        // 3	70–80	右ステップ	right step
        // 4	90–100	左ステップ	left step
        // 5	110–115	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	140–150	ダメージ１	damage１
        // 9	160–172	ダメージ２ (吹っ飛び）	damage２ (吹っ飛び)
        // 10	10–20	起き上がり（歩きで移動）	get up(walkでmove)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	210–230	攻撃1（切り裂き）	attack1(切り裂き)
        // 14	210–230	攻撃1予備動作１	attack1予備動作１
        // 15	210–230	攻撃1予備動作２	attack1予備動作２
        // 16	240–255	攻撃２（ファイヤーボール）	attack２(ファイヤーボール)
        // 17	240–255	攻撃２予備動作１	attack２予備動作１
        // 18	240–255	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Alexander = new EnemyDefaults {
            Id=43, TableIndex=37, ModelCode="e43a", Name="Alexander",        MaxHp=675, Abs=15, MinGoldDrop=17, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=150, IceRes=130, ThunderRes=100, WindRes=120, HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=164, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{120}, ProjectileDamage=new int[]{124} };

        // shares scale=3.0 with CaveBat
        // Motions: e61a.chr @ data.dat 0x1d325000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 3	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 4	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 5	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 6	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 7	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 8	70–80	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 10	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 11	90–100	死亡始まり	death始まり
        // 12	100–105	落下ﾙｰﾌﾟ	落下ﾙｰﾌﾟ
        // 13	105–115	死亡	death
        // 14	115	死亡停止	death停止
        // 15	120–145	攻撃１	attack１
        // 16	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 17	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	150–171	攻撃2予備動作	attack2予備動作
        // 19	171–175	攻撃	attack
        // 20	175–200	攻撃2予備動作	attack2予備動作
        internal static readonly EnemyDefaults EvilBat = new EnemyDefaults {
            Id=61, TableIndex=53, ModelCode="e61a", Name="Evil Bat",         MaxHp=150, Abs=4,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=149, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{86,85}, ProjectileDamage=new int[]{} };

        // Motions: e62a.chr @ data.dat 0x1d360800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	150–160	ダメージ１	damage１
        // 9	170–190	ダメージ２	damage２
        // 10	200–220	起き上がり	get up
        // 11	230–245	死亡	death
        // 12	245	死亡ループ	death loop
        // 13	255–280	攻撃4（斧/飛んで前に出る）	attack4(斧/飛んで前に出る)
        // 14	255–280	攻撃4予備動作１	attack4予備動作１
        // 15	255–280	攻撃4予備動作２	attack4予備動作２
        // 16	290–302	攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）	attack8(共通スティール/体当たり。盗ったら早walkでfleeる)
        // 17	290–302	攻撃8予備動作１	attack8予備動作１
        // 18	290–302	攻撃8予備動作２	attack8予備動作２
        internal static readonly EnemyDefaults HellPockle = new EnemyDefaults {
            Id=62, TableIndex=54, ModelCode="e62a", Name="Hell Pockle",      MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{64,64}, ProjectileDamage=new int[]{} };

        // Motions: e63a.chr @ data.dat 0x1d3e1800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	120–140	バックステップ	back step
        // 3	60–80	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡ループ	death loop
        // 13	150–180	攻撃1(縦振り）	attack1(縦振り)
        // 14	150–180	攻撃1予備動作１	attack1予備動作１
        // 15	150–180	攻撃1予備動作２	attack1予備動作２
        // 16	190–215	攻撃２（走り頭突き入り）	attack２(run頭突き(enter))
        // 17	215–223	攻撃２予備動作１（走りリピート）	attack２予備動作１(runリピート)
        // 18	223–230	攻撃２予備動作２ （走り頭突き終わり）	attack２予備動作２ (run頭突き終わり)
        internal static readonly EnemyDefaults RashDasher = new EnemyDefaults {
            Id=63, TableIndex=55, ModelCode="e63a", Name="Rash Dasher",      MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=2, WeaponDefense=10,
            StealItemId=149, ItemResA=100, ItemResB=70, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        // Motions: e64a.chr @ data.dat 0x1d461800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults SteelGiant = new EnemyDefaults {
            Id=64, TableIndex=56, ModelCode="e64a", Name="Steel Giant",      MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=125, WindRes=80,  HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=177, ItemResA=95, ItemResB=50, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{93,93,98,98,80,70,60}, ProjectileDamage=new int[]{1,64} };

        // Motions: e65a.chr @ data.dat 0x1d506800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Blizzard = new EnemyDefaults {
            Id=65, TableIndex=57, ModelCode="e65a", Name="Blizzard",         MaxHp=750, Abs=8,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=140, WindRes=140, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=5, WeaponDefense=0,
            StealItemId=162, ItemResA=100, ItemResB=50, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{119,119,119,119,105,90,75}, ProjectileDamage=new int[]{1,105} };

        // Motions: e66a.chr @ data.dat 0x1d5a5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	10–20	ダミー	(dummy)
        // 3	10–20	ダミー	(dummy)
        // 4	10–20	ダミー	(dummy)
        // 5	60–70	もぐり	もぐり
        // 6	70–80	もぐりループ	もぐり(loop)
        // 7	80–90	出現	appear
        // 8	100–110	ダメージ	damage
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	120–140	攻撃1	attack1
        // 14	120–140	ダミー	(dummy)
        // 15	120–140	ダミー	(dummy)
        // 16	150–170	攻撃2	attack2
        // 17	150–170	ダミー	(dummy)
        // 18	150–170	ダミー	(dummy)
        internal static readonly EnemyDefaults MoonDigger = new EnemyDefaults {
            Id=66, TableIndex=58, ModelCode="e66a", Name="Moon Digger",      MaxHp=420, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=125, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=2, WeaponDefense=0,
            StealItemId=187, ItemResA=100, ItemResB=70, AttackPower=197, ElemAtkFire=45, ElemAtkIce=45, ElemAtkThunder=130, ElemAtkWind=45, ElemAtkHoly=45, ElemAtkDark=45,
            MeleeDamage=new int[]{83}, ProjectileDamage=new int[]{} };

        // Motions: e67a.chr @ data.dat 0x1d602000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	35–55	ダメージ	damage
        // 2	60–80	死亡	death
        // 3	80	死亡ループ	death loop
        // 4	85–120	攻撃葉	attack葉
        // 5	125–160	攻撃液前	attack液前
        // 6	165–200	攻撃液上	attack液上
        internal static readonly EnemyDefaults DarkFlower = new EnemyDefaults {
            Id=67, TableIndex=59, ModelCode="e67a", Name="Dark Flower",      MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=5, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=147, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{90,90,94}, ProjectileDamage=new int[]{1} };

        // thunder=0 (immune)
        // Motions: e69a.chr @ data.dat 0x1d6bc000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–130	ガードループ	guard (loop)
        // 7	10–20	ダミー	(dummy)
        // 8	10–20	ダミー	(dummy)
        // 9	220–234	死亡	death
        // 10	234–240	死亡ループ	death loop
        // 11	160–185	攻撃1	attack1
        // 12	160–185	ダミー	(dummy)
        // 13	160–185	ダミー	(dummy)
        // 14	190–215	攻撃2	attack2
        // 15	190–215	ダミー	(dummy)
        // 16	190–215	ダミー	(dummy)
        internal static readonly EnemyDefaults Billy = new EnemyDefaults {
            Id=69, TableIndex=61, ModelCode="e69a", Name="Billy",            MaxHp=300, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=0,   WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=163, ItemResA=100, ItemResB=70, AttackPower=83, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{93,93}, ProjectileDamage=new int[]{93} };

        // fire=65486 (0xFFCE — effectively absorbs fire damage)
        // Motions: e70a.chr @ data.dat 0x1d71b000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	70–90	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	100–120	左ｽﾃｯﾌﾟ	left step
        // 4	130–150	右ｽﾃｯﾌﾟ	right step
        // 5	160–180	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	190–200	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	210–230	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	240–270	死亡	death
        // 9	270	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 10	280–310	ｻﾝﾄﾞｱｯﾊﾟｰ	ｻﾝﾄﾞｱｯﾊﾟｰ
        // 11	320–340	爪	爪
        internal static readonly EnemyDefaults Vulcan = new EnemyDefaults {
            Id=70, TableIndex=62, ModelCode="e70a", Name="Vulcan",           MaxHp=480, Abs=12, MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=65486, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=5, WeaponDefense=40,
            StealItemId=81, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{88,94,94}, ProjectileDamage=new int[]{} };

        // Motions: e72a.chr @ data.dat 0x1d812800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	バタアシ	バタアシ
        // 2	60–80	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	90–110	右ｽﾃｯﾌﾟ	right step
        // 4	120–140	左ｽﾃｯﾌﾟ	left step
        // 5	150–160	ｶﾞｰﾄﾞ	guard
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	190–200	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	210–225	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	235–260	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	270–290	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	300–325	死亡	death
        // 12	325	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	335–355	攻撃１	attack１
        // 14	365–385	攻撃２	attack２
        // 15	390–410	攻撃3	attack3
        internal static readonly EnemyDefaults SpaceGyon = new EnemyDefaults {
            Id=72, TableIndex=64, ModelCode="e72a", Name="Space Gyon",       MaxHp=525, Abs=5,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=75,  IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=0, WeaponDefense=0,
            StealItemId=153, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{78,78}, ProjectileDamage=new int[]{2} };

        // Motions: e73a.chr @ data.dat 0x1d8b0000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	230–240	ダメージ１	damage１
        // 9	150–160	ダメージ２（後ろに下がる）	damage２(後ろに下がる)
        // 10	30–50	起き上がり(歩きで移動）	get up(walkでmove)
        // 11	250–260	死亡	death
        // 12	260	死亡ループ	death loop
        // 13	310–350	攻撃1 （火の球）	attack1 (fireの球)
        // 14	310–350	攻撃1予備動作２	attack1予備動作２
        // 15	270–300	攻撃２（頭突き）	attack２(頭突き)
        // 16	270–300	攻撃２予備動作１	attack２予備動作１
        // 17	270–300	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults BlueDragon = new EnemyDefaults {
            Id=73, TableIndex=65, ModelCode="e73a", Name="Blue Dragon",      MaxHp=600, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=125, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=5, WeaponDefense=30,
            StealItemId=162, ItemResA=80, ItemResB=50, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{90,90}, ProjectileDamage=new int[]{} };

        // Motions: e74a.chr @ data.dat 0x1d9a5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–125	ガード入り	guard (enter)
        // 6	125–135	ガードループ	guard (loop)
        // 7	135–140	ガード戻り	guard (return)
        // 8	230–240	ダメージ１	damage１
        // 9	150–160	ダメージ２（後ろに下がる）	damage２(後ろに下がる)
        // 10	30–50	起き上がり(歩きで移動）	get up(walkでmove)
        // 11	250–260	死亡	death
        // 12	260	死亡ループ	death loop
        // 13	310–350	攻撃1 （火の球）	attack1 (fireの球)
        // 14	310–350	攻撃1予備動作２	attack1予備動作２
        // 15	270–300	攻撃２（頭突き）	attack２(頭突き)
        // 16	270–300	攻撃２予備動作１	attack２予備動作１
        // 17	270–300	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults BlackDragon = new EnemyDefaults {
            Id=74, TableIndex=66, ModelCode="e74a", Name="Black Dragon",     MaxHp=900, Abs=20, MinGoldDrop=22, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=130,
            EntityScale=17.5f, EntityScaleCopy=17.5f, DamageReduction=10, WeaponDefense=60,
            StealItemId=154, ItemResA=50, ItemResB=40, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        // Motions: e76a.chr @ data.dat 0x1dadd800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	バック	バック
        // 3	90–110	右	右
        // 4	120–140	左	左
        // 5	150–155	ガード（入り）	guard((enter))
        // 6	155–165	ガードループ	guard (loop)
        // 7	165–170	ガード戻り	guard (return)
        // 8	180–190	ダメージ1	damage1
        // 9	200–220	ダメージ2	damage2
        // 10	10–20	ダミー	(dummy)
        // 11	230–250	死亡	death
        // 12	250	死亡ループ	death loop
        // 13	260–280	攻撃1	attack1
        // 14	260–280	攻撃1	attack1
        // 15	260–280	攻撃1	attack1
        // 16	290–310	攻撃2	attack2
        // 17	290–310	攻撃2	attack2
        // 18	290–310	攻撃2	attack2
        // 19	320–340	攻撃3	attack3
        internal static readonly EnemyDefaults CrescentBaron = new EnemyDefaults {
            Id=76, TableIndex=68, ModelCode="e76a", Name="Crescent Baron",   MaxHp=450, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=5, WeaponDefense=10,
            StealItemId=null, ItemResA=80, ItemResB=70, AttackPower=170, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{98,98,94,94}, ProjectileDamage=new int[]{-1} };

        // Motions: e82a.chr @ data.dat 0x1dd19000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicGoT = new EnemyDefaults {
            Id=82, TableIndex=74, ModelCode="e82a", Name="King Mimic (Gallery of Time)", MaxHp=675, Abs=18, MinGoldDrop=25, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=5, WeaponDefense=30,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{134,120,98}, ProjectileDamage=new int[]{} };

        // code=kori (Japanese for "ice" — may be official name)
        // Motions: e83a.chr @ data.dat 0x1dd94800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	まちかまえ	まちstance
        internal static readonly EnemyDefaults MimicGoT = new EnemyDefaults {
            Id=83, TableIndex=75, ModelCode="e83a", Name="Mimic (Gallery of Time)", MaxHp=450, Abs=6,  MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=5, WeaponDefense=20,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{} };

        // listed as non-drop in Enemies.cs
        // Motions: e90a.chr @ data.dat 0x1de72000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Gol = new EnemyDefaults {
            Id=90, TableIndex=94, ModelCode="e90a", Name="Gol",              MaxHp=600, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{1} };

        // listed as non-drop in Enemies.cs
        // Motions: e91a.chr @ data.dat 0x1df13800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults Sil = new EnemyDefaults {
            Id=91, TableIndex=95, ModelCode="e91a", Name="Sil",              MaxHp=500, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=90,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{1} };

        // ── Overseas (USA/PAL-exclusive enemies) ──────────────────────────────────
        // Overseas enemies appear in the USA/PAL version of Dark Cloud but are absent
        // from the Japanese release. Pool assignments match the Japanese versions of the
        // same dungeons (DBC area).

        // confirmed from clean dump 2026-05-30, DBC game fl.5 — Yammich (overseas DBC fl.1-7)
        // Motions: e101a.chr @ data.dat 0x1e730800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–15	立ち	idle
        // 1	5–15	歩き	walk
        // 2	20–35	バックステップ	back step
        // 3	40–50	右ステップ	right step
        // 4	55–65	左ステップ	left step
        // 5	5–15	ガード入り	guard (enter)
        // 6	5–15	ガードループ	guard (loop)
        // 7	5–15	ガード戻り	guard (return)
        // 8	130–140	ダメージ1	damage1
        // 9	130–140	ダメージ2	damage2
        // 10	130–140	起き上がり	get up
        // 11	145–165	死亡	death
        // 12	165	死亡ループ	death loop
        // 13	75–90	攻撃１入り	attack１(enter)
        // 14	95–105	攻撃１ループ	attack１(loop)
        // 15	110–125	攻撃１戻り	attack１(return)
        internal static readonly EnemyDefaults Yammich = new EnemyDefaults {
            Id=301, TableIndex=96, ModelCode="e101", Name="Yammich",         MaxHp=13,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=70,  HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=0, WeaponDefense=1,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; needs dump confirmation
        // Motions: e104a.chr @ data.dat 0x1dfb5800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き(あしふみ）	walk(あしふみ)
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	60–70	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	80–100	死亡	death
        // 12	100	死亡ループ	death loop
        // 13	110–120	攻撃1（エラからネバネバ液）	attack1(エラからネバネバ液)
        // 14	110–120	攻撃1予備動作１（エラからネバネバ液）	attack1予備動作１(エラからネバネバ液)
        // 15	110–120	攻撃1予備動作２（エラからネバネバ液）	attack1予備動作２(エラからネバネバ液)
        // 16	130–150	攻撃２（超重プレス）	attack２(超重プレス)
        // 17	160–180	攻撃２予備動作１（起き上がり）	attack２予備動作１(get up)
        // 18	160–180	攻撃２予備動作２（起き上がり）	attack２予備動作２(get up)
        internal static readonly EnemyDefaults Opar = new EnemyDefaults {
            Id=304, TableIndex=98, ModelCode="e104", Name="Opar",            MaxHp=28,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=60,  ThunderRes=130, WindRes=100, HolyRes=100,
            EntityScale=15.0f, EntityScaleCopy=15.0f, DamageReduction=1, WeaponDefense=2,
            StealItemId=227, ItemResA=90, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{35,35} };

        // from static table 2026-06-04; needs dump confirmation
        // Motions: e106a.chr @ data.dat 0x1e242800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	10–30	歩き	walk
        // 2	10–30	バックステップ	back step
        // 3	10–30	右ステップ	right step
        // 4	10–30	左ステップ	left step
        // 5	115–125	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	115–125	ガード戻り	guard (return)
        // 8	135–145	ダメージ１	damage１
        // 9	154–165	ダメージ２	damage２
        // 10	175–190	起き上がり	get up
        // 11	10–30	死亡（消える）	death(消える)
        // 12	10–30	死亡ループ （消える）	death loop (消える)
        // 13	200–205	攻撃1	attack1
        // 14	205–225	攻撃1予備動作1	attack1予備動作1
        // 15	225–235	攻撃1予備動作2	attack1予備動作2
        // 16	245–250	攻撃2	attack2
        // 17	250–260	攻撃2予備動作1	attack2予備動作1
        // 18	260–265	攻撃2予備動作2	attack2予備動作2
        // 19	275–280	攻撃3	attack3
        // 20	280–300	攻撃3予備動作1	attack3予備動作1
        // 21	300–305	攻撃3予備動作2	attack3予備動作2
        internal static readonly EnemyDefaults KingPrickly = new EnemyDefaults {
            Id=306, TableIndex=100, ModelCode="e106", Name="King Prickly",    MaxHp=63,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=3, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{50,50,50}, ProjectileDamage=new int[]{} };

        // from static table 2026-06-04; appears in overseas late dungeons; needs dump confirmation
        // Motions: e108a.chr @ data.dat 0x1e028000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	10–20	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 3	10–20	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 4	10–20	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 5	40–80	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	85–95	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	100–110	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	115–130	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	135–155	死亡	death
        // 12	155	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	160–190	攻撃１	attack１
        // 14	160–190	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	160–190	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	195–225	攻撃２	attack２
        // 17	195–225	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	195–225	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults Nikapous = new EnemyDefaults {
            Id=308, TableIndex=112, ModelCode="e108", Name="Nikapous",        MaxHp=2350, Abs=15, MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=100, ThunderRes=100, WindRes=125, HolyRes=125,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=133, ItemResA=100, ItemResB=70, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        // ── Demon Shaft (dungeon 6) ───────────────────────────────────────────────
        // Demon Shaft enemies reuse base-game eids with new model codes and scaled stats.
        // Each eid appears multiple times in the static table at u090a tiers: 15, 20, 23, 30.
        // The entries below capture the FIRST occurrence (lowest tier) as the EnemyDefaults base.
        // The Gemrons (311-315) are Demon Shaft unique enemies — each appears at a single tier.

        // Mimic (Demon Shaft) — tier 1
        // Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	まちかまえ	まちstance
        internal static readonly EnemyDefaults MimicDS = new EnemyDefaults {
            Id=309, TableIndex=130, ModelCode="e109", Name="Mimic (Demon Shaft)",     MaxHp=3500, Abs=10, MinGoldDrop=26, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // King Mimic (Demon Shaft) — tier 1
        // Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicDS = new EnemyDefaults {
            Id=310, TableIndex=131, ModelCode="e110", Name="King Mimic (Demon Shaft)", MaxHp=5000, Abs=20, MinGoldDrop=35, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=15, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // Gemron (Fire) — tier 1
        // Motions: e111a.chr @ data.dat 0x1e860000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	40–50	バックステップ	back step
        // 3	55–65	右ステップ	right step
        // 4	70–80	左ステップ	left step
        // 5	155–165	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	185–195	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	85–100	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	105–125	死亡	death
        // 12	125	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	130–150	攻撃１	attack１
        // 14	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	130–150	攻撃２	attack２
        // 17	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults GemronFire = new EnemyDefaults {
            Id=311, TableIndex=111, ModelCode="e111", Name="Gemron (Fire)",   MaxHp=2500, Abs=15, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=0,   IceRes=150, ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=161, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{100} };

        // Gemron (Ice) — tier 2
        // Motions: e112a.chr @ data.dat 0x1e8ba000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	40–50	バックステップ	back step
        // 3	55–65	右ステップ	right step
        // 4	70–80	左ステップ	left step
        // 5	155–165	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	185–195	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	85–100	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	105–125	死亡	death
        // 12	125	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	130–150	攻撃１	attack１
        // 14	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	130–150	攻撃２	attack２
        // 17	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults GemronIce = new EnemyDefaults {
            Id=312, TableIndex=121, ModelCode="e112", Name="Gemron (Ice)",    MaxHp=4000, Abs=20, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=150, IceRes=0,   ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=15, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=162, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{120} };

        // Gemron (Thunder) — tier 3
        // Motions: e113a.chr @ data.dat 0x1e914000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	40–50	バックステップ	back step
        // 3	55–65	右ステップ	right step
        // 4	70–80	左ステップ	left step
        // 5	155–165	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	185–195	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	85–100	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	105–125	死亡	death
        // 12	125	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	130–150	攻撃１	attack１
        // 14	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	130–150	攻撃２	attack２
        // 17	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults GemronThunder = new EnemyDefaults {
            Id=313, TableIndex=132, ModelCode="e113", Name="Gemron (Thunder)", MaxHp=5500, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=30,  IceRes=30,  ThunderRes=0,   WindRes=150, HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=20, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=163, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{130} };

        // Gemron (Wind) — tier 4
        // Motions: e114a.chr @ data.dat 0x1e96e000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	40–50	バックステップ	back step
        // 3	55–65	右ステップ	right step
        // 4	70–80	左ステップ	left step
        // 5	155–165	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	185–195	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	85–100	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	105–125	死亡	death
        // 12	125	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	130–150	攻撃１	attack１
        // 14	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	130–150	攻撃２	attack２
        // 17	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults GemronWind = new EnemyDefaults {
            Id=314, TableIndex=143, ModelCode="e114", Name="Gemron (Wind)",   MaxHp=8000, Abs=30, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=100, ThunderRes=140, WindRes=0,   HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=164, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{140} };

        // Gemron (Holy) — tier 5
        // Motions: e115a.chr @ data.dat 0x1e9c8000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–35	歩き	walk
        // 2	40–50	バックステップ	back step
        // 3	55–65	右ステップ	right step
        // 4	70–80	左ステップ	left step
        // 5	155–165	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	185–195	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	85–100	ﾀﾞﾒｰｼﾞ	damage
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	105–125	死亡	death
        // 12	125	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	130–150	攻撃１	attack１
        // 14	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	130–150	攻撃２	attack２
        // 17	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	130–150	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults GemronHoly = new EnemyDefaults {
            Id=315, TableIndex=154, ModelCode="e115", Name="Gemron (Holy)",   MaxHp=12500, Abs=35, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=0,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=30, WeaponDefense=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=165, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{150} };

        // Motions: e116a.chr @ data.dat 0x1ea22000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バックステップ	back step
        // 3	70–80	右ステップ	right step
        // 4	90–100	左ステップ	left step
        // 5	110–115	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	140–153	ダメージ１	damage１
        // 9	165–183	ダメージ２	damage２
        // 10	225–243	死亡	death
        // 11	243	死亡ループ	death loop
        // 12	255–285	攻撃1（近距離パンチ）	attack1(近距離パンチ)
        // 13	255–285	攻撃1予備動作２	attack1予備動作２
        // 14	295–322	攻撃２（魔法）	attack２(magic)
        // 15	295–322	攻撃２予備動作１	attack２予備動作１
        // 16	295–322	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults BishopQ = new EnemyDefaults {
            Id=316, TableIndex=133, ModelCode="e116", Name="Bishop Q",        MaxHp=6000, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=40,  IceRes=40,  ThunderRes=40,  WindRes=40,  HolyRes=140,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=null, ItemResA=50, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{130,130} };

        // code=e124
        // Motions: e124a.chr @ data.dat 0x1ec15000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–104	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	116–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–64	攻撃1	attack1
        // 14	64–90	攻撃11	attack11
        // 15	50–90	ダミー	(dummy)
        // 16	310–330	攻撃2	attack2
        // 17	330–360	攻撃2	attack2
        // 18	310–360	ダミー	(dummy)
        internal static readonly EnemyDefaults Gacious = new EnemyDefaults {
            Id=317, TableIndex=105, ModelCode="e124", Name="Gacious",         MaxHp=1800, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=70,  IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=0,
            StealItemId=null, ItemResA=100, ItemResB=90, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130,130,130,130,130}, ProjectileDamage=new int[]{} };

        // Motions: e118a.chr @ data.dat 0x1eab7800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–75	攻撃1	attack1
        // 14	50–75	ダミー	(dummy)
        // 15	50–75	ダミー	(dummy)
        // 16	50–60	攻撃2構え	attack2stance
        // 17	60–75	発射	launch
        // 18	50–75	ダミー	(dummy)
        internal static readonly EnemyDefaults SilverGear = new EnemyDefaults {
            Id=318, TableIndex=144, ModelCode="e118", Name="Silver Gear",     MaxHp=2500, Abs=30, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=30,  IceRes=30,  ThunderRes=30,  WindRes=30,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{110} };

        // Motions: e119a.chr @ data.dat 0x1eb74000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	280–290	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	106–114	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	300–325	攻撃1	attack1
        // 14	300–325	ダミー	(dummy)
        // 15	300–325	ダミー	(dummy)
        // 16	330–360	攻撃2	attack2
        // 17	330–360	ダミー	(dummy)
        // 18	330–360	ダミー	(dummy)
        internal static readonly EnemyDefaults HornHead = new EnemyDefaults {
            Id=319, TableIndex=122, ModelCode="e119", Name="Horn Head",       MaxHp=2500, Abs=20, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=186, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{} };

        // ── Bosses ─────────────────────────────────────────────────────────────
        // All bosses have MinGoldDrop=0, DropChance=0, StealItemId=65535 (can't steal).
        // TableIndex values confirmed from ISO extraction 2026-06-04.

        // ═══════════════════════════════════════════════════════════════════════════════════════════════
        // BOSS MOTION LISTS — each boss's animations, decoded from its model's <code>.chr info.cfg KEY block.
        // Source: data.dat (inside the ISO) → the .chr's first member is text info.cfg with a KEY_START…
        // MOTION_END list, one `KEY <start>,<end>,<speed>, //<name(Shift-JIS)> <idx>` per motion. Locate the
        // .chr via data.hed (filename @ index*80) → data.hd2 (entry @ 16 + index*32: [+0]=data.dat offset,
        // [+4]=size, [+8]=sector). idx below = motion-table index (the `_SET_MOTION` arg); "死亡" = the
        // death/collapse motion used by EnemyModelInjector.BossScriptPatcher.CollapseMotion. See the
        // datadat-index-and-chr-motions note + enemy-spawn-system.md §"Boss death animation".
        // ═══════════════════════════════════════════════════════════════════════════════════════════════

        // DBC boss. Motions: c12a.chr info.cfg @ data.dat 0x1a8d6000.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	飛行	flight
        // 1	35–55	着地	landing (touches down @50)
        // 2	270–300	突進（離陸）	charge (takeoff)
        // 3	200–205	突進ループ	charge loop
        // 4	70–80	突進（着地）	charge (land @74)
        // 5	85–95	ダメージ	damage
        // 6	100–120	死亡	death ← collapse
        // 7	125–145	離陸	takeoff
        // 8	175–196	火	fire breath
        // 9	210–215	毛繕い（入り）	grooming (enter)
        // 10	215–225	毛繕いループ	grooming loop
        // 11	225–240	毛繕い（戻り）	grooming (return)
        // 12	245–265	じたじた	squirm
        internal static readonly EnemyDefaults Dran = new EnemyDefaults {
            Id=112, TableIndex=78, ModelCode="c12a", Name="Dran",            MaxHp=250,   Abs=10, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=50,
            EntityScale=45.0f, EntityScaleCopy=45.0f, DamageReduction=10, WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{45,45,45,25,25,25,25,25,25,25,25,25,25}, ProjectileDamage=new int[]{17} };

        // WOF boss. Motions: c14a.chr info.cfg @ data.dat 0x1ad22000.
        // NOTE: the .chr's own labels skip 11 (…10, 12, 13, 14, 15); Idx below is the sequential table index
        // (the _SET_MOTION arg). So verify death = 13 (sequential) vs the .chr's "14" when wiring CollapseMotion.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–75	走り	run
        // 3	80–96	通常攻撃	normal attack
        // 4	110–163	ため攻撃	charge attack
        // 5	175–230	種マシンガン	seed machinegun
        // 6	240–257	ダメージ左足入り	damage L-leg (enter)
        // 7	257–273	フゥフゥループ	panting loop
        // 8	273–278	ダメージ左足戻り	damage L-leg (return)
        // 9	285–302	ダメージ右足入り	damage R-leg (enter)
        // 10	302–318	フゥフゥループ	panting loop
        // 11	318–323	ダメージ右足戻り	damage R-leg (return)
        // 12	335–349	ダメージ	damage
        // 13	360–385	死亡	death ← collapse
        // 14	400–420	バックステップ	back step
        internal static readonly EnemyDefaults MasterUtan = new EnemyDefaults {
            Id=114, TableIndex=79, ModelCode="c14a", Name="Master Utan",     MaxHp=700,   Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=35.0f, EntityScaleCopy=35.0f, DamageReduction=12, WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{62,47}, ProjectileDamage=new int[]{13} };

        // SW boss — ice=65486 (0xFFCE, -50 as int16) = fire-absorbing (same encoding as Vulcan's fire)
        // Motions: c13a.chr info.cfg @ data.dat 0x1a9f6800.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	50–70	バックステップ	back step
        // 3	100–120	右ステップ	right step
        // 4	75–95	左ステップ	left step
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	150–160	ダメージ1	damage 1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	165–185	死亡	death ← collapse
        // 12	185	死亡ループ	death loop
        // 13	125–145	攻撃1	attack 1
        // 14	10–20	ダミー	(dummy)
        // 15	10–20	ダミー	(dummy)
        // 16	10–20	攻撃2	attack 2
        // 17	10–20	ダミー	(dummy)
        // 18	10–20	ダミー	(dummy)
        // 19	190–200	竜巻（入り）	tornado (enter)
        // 20	205–215	竜巻（ループ）	tornado (loop)
        // 21	216–230	竜巻（戻り）	tornado (return)
        // 22	240–270	氷落とし	ice drop
        // 23	280–313	機雷たつまき	mine tornado
        // 24	320–326	バリア（入り）	barrier (enter)
        // 25	327–335	バリア（ループ）	barrier (loop)
        // 26	336–340	バリア（戻り）	barrier (return)
        // Damage model (confirmed from the genuine fight 2026-06-18): IceRes=65486=0xFFCE=-50 → she ABSORBS ice
        // (ice magic HEALS her). Weak to FIRE (150) and HOLY (120); resists thunder/wind (80). So she is damaged with
        // fire/holy magic, never ice. Her companions (barrier/arrow/prison/meteor/tornado) are all ice-IMMUNE (IceRes=0)
        // and fire-weak (200); reiki/Ice Aura alone is fully neutral. MaxHp 700.
        internal static readonly EnemyDefaults IceQueen = new EnemyDefaults {
            Id=113, TableIndex=80, ModelCode="c13a", Name="Ice Queen",       MaxHp=700,   Abs=30, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=65486, ThunderRes=80, WindRes=80, HolyRes=120,
            EntityScale=13.0f, EntityScaleCopy=13.0f, DamageReduction=10, WeaponDefense=0,
            StealItemId=65535, ItemResA=40, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // SW boss — projectile/summon entity of Ice Queen; not a standalone fight.
        // Motions: kori.chr info.cfg @ data.dat 0x1e3a8800 — 2 (projectile; no death anim).
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	氷出現	ice appear
        // 1	40–55	氷破裂	ice burst
        // NOTE: idx 76's real ModelCode is "korinoya" (STB c13_korinoya) — the flying, damaging ice-arrow
        // PROJECTILE, not the static "kori" ice source (that's idx 102). The 4-char "kori" here was a truncation.
        internal static readonly EnemyDefaults IceArrow = new EnemyDefaults {
            Id=84, TableIndex=76, ModelCode="korinoya", Name="Ice Arrow", MaxHp=100,   Abs=17, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f,  EntityScaleCopy=2.0f,  DamageReduction=5,  WeaponDefense=0,
            StealItemId=65535, ItemResA=70, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };

        // IceQueen (SW floor 18) fight companions — all id=0, boss sentinels. Ice-attack effect entities.
        // code=bari → baria.chr (barrier) @ data.dat 0x1e1b1000 — 0 ループ(loop) / 1 消滅(despawn) / 2 出現(appear).
        internal static readonly EnemyDefaults IQComp101 = new EnemyDefaults {
            Id=0, TableIndex=101, ModelCode="bari", Name="Ice Barrier", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };
        // code=kori → kori.chr (ice arrow); motions documented above at IceArrow (0 ice appear / 1 ice burst).
        // Motions: kori.chr @ data.dat 0x1e3a8800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	氷出現	iceappear
        // 1	40–55	氷破裂	ice破裂
        internal static readonly EnemyDefaults IQComp102 = new EnemyDefaults {
            Id=0, TableIndex=102, ModelCode="kori", Name="Ice Prison", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };
        // code=i_me → i_meteo.chr (ice meteor) @ data.dat 0x1e37a000 — 0 氷生成(ice form) / 1 ループ / 2 爆発(explode).
        internal static readonly EnemyDefaults IQComp103 = new EnemyDefaults {
            Id=0, TableIndex=103, ModelCode="i_me", Name="Ice Meteor", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };
        // code=b3_r → b3_reiki.chr (霊気 "aura/spirit") @ data.dat 0x1a8c1800 — 1 motion: 0 reiki (aura).
        internal static readonly EnemyDefaults SWComp92 = new EnemyDefaults {
            Id=0, TableIndex=92, ModelCode="b3_r", Name="Ice Aura", MaxHp=80, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,  // reiki: fully neutral (unlike the ice-immune siblings)
            EntityScale=5.0f, EntityScaleCopy=5.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };
        // code=i_ta → i_tatumaki.chr (ice tornado) @ data.dat 0x1e38b800 — 0 柱出現(pillar) / 1 竜巻(tornado) / 2 竜巻消える(vanish).
        internal static readonly EnemyDefaults IQComp104 = new EnemyDefaults {
            Id=0, TableIndex=104, ModelCode="i_ta", Name="Ice Tornado", MaxHp=100, AttackPower=65535,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f, EntityScaleCopy=2.0f, ReticleWidth=1.0f, ReticleHeight=1.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };

        // SMT boss. Motions: c15a.chr info.cfg @ data.dat 0x1ae5c000.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	待機	idle
        // 1	30–50	開く	open
        // 2	50–55	開きループ	open loop
        // 3	55–65	閉じる	close
        // 4	70–80	ダメージ	damage
        // 5	90–110	死亡	death ← collapse
        // 6	110–115	死亡ループ	death loop
        internal static readonly EnemyDefaults KingsCurseCoffin = new EnemyDefaults {
            Id=115, TableIndex=81, ModelCode="c15a", Name="King's Curse Coffin",    MaxHp=2000,  Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=125,
            EntityScale=6.0f,  EntityScaleCopy=6.0f,  DamageReduction=10, WeaponDefense=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // Unlisted phase entity — code=c15b; not in Enemies.cs; suspected SMT King's-Curse scripted phase.
        // Motions: c15b.chr info.cfg @ data.dat 0x1ae94000 — no "死亡" (transformation entity; no collapse).
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–95	攻撃	attack
        // 3	115–135	出現	appear
        // 4	140–145	ノーマル〜黒玉	normal → black-orb
        // 5	150–155	黒玉〜ノーマル	black-orb → normal
        // 6	160–165	ノーマル〜棺桶	normal → coffin
        // 7	180–185	黒玉〜棺桶	black-orb → coffin
        // 8	190–200	黒丸ループ	black-orb loop
        // 9	165–175	棺桶ループ	coffin loop
        internal static readonly EnemyDefaults KingsCurse = new EnemyDefaults {
            Id=100, TableIndex=82, ModelCode="c15b", Name="King's Curse", MaxHp=1000, Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=4.0f,  EntityScaleCopy=4.0f,  DamageReduction=10, WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{91,91,71}, ProjectileDamage=new int[]{} };

        // MS boss. Motions: c16a.chr info.cfg @ data.dat 0x1aee3000.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	走り	run
        // 3	140–170	飲む	drink
        // 4	210–260	雄たけび	roar ← BossScriptPatcher.RoarMotion
        // 5	180–202	攻撃1	attack 1
        // 6	270–294	攻撃2	attack 2
        // 7	90–105	ダメージけつ	damage (rear)
        // 8	115–130	ダメージ顔	damage (face)
        // 9	300–330	死亡	death ← collapse
        // 10	340–356	バックステップ	back step
        internal static readonly EnemyDefaults MinotaurJoe = new EnemyDefaults {
            Id=116, TableIndex=83, ModelCode="c16a", Name="Minotaur Joe",    MaxHp=2000,  Abs=50, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=25.0f, EntityScaleCopy=25.0f, DamageReduction=12, WeaponDefense=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{100,125,100,100,100}, ProjectileDamage=new int[]{} };

        // GoT boss (final). Motions: c17a.chr info.cfg @ data.dat 0x1afed000.
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	待機	idle
        // 1	30–38	たたみ入り	wing-fold (enter)
        // 2	39–50	たたみループ	wing-fold (loop)
        // 3	51–65	たたみ戻り	wing-fold (return)
        // 4	75–81	はばたき入り	wing-flap (enter)
        // 5	81–91	はばたきループ	wing-flap (loop)
        // 6	91–97	はばたき戻り	wing-flap (return)
        // 7	110–121	右攻撃	right attack
        // 8	121–122	ループ	loop
        // 9	122–130	戻り	return
        // 10	145–156	左攻撃	left attack
        // 11	156–157	ループ	loop
        // 12	157–165	戻り	return
        // 13	175–188	ビーム入り	beam (enter)
        // 14	188–189	ビームループ	beam (loop)
        // 15	188–197	ビーム戻り	beam (return)
        // 16	210–222	ダメージ目	damage (eye)
        // 17	230–235	ダメージ右手	damage (R hand)
        // 18	245–250	ダメージ左手	damage (L hand)
        // 19	260–295	死亡	death ← collapse
        // 20	292–295	死亡ループ	death loop
        internal static readonly EnemyDefaults DarkGenie = new EnemyDefaults {
            Id=117, TableIndex=84, ModelCode="c17a", Name="Dark Genie",      MaxHp=2000,  Abs=60, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=25, WeaponDefense=30,
            StealItemId=65535, ItemResA=30, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{85}, ProjectileDamage=new int[]{} };

        // Dark Genie second form — not named in Enemies.cs; code=c17c; same resistance profile as hands
        // Motions: c17b.chr info.cfg @ data.dat 0x1b0d8800 — only 2 (no death anim; defeat is scripted).
        // Idx	Frames	Name (JP)	Meaning
        // 0	9–10	攻撃のかまえ	attack stance
        // 1	20–30	ダメージ	damage
        internal static readonly EnemyDefaults DarkGenieForm2 = new EnemyDefaults {
            Id=118, TableIndex=85, ModelCode="c17b", Name="Dark Genie (form 2)", MaxHp=3200, Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  DamageReduction=0,  WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        // Dark Genie hands. Motions: c17c.chr info.cfg @ data.dat 0x1b160800 — only 2 (no death anim).
        // Idx	Frames	Name (JP)	Meaning
        // 0	9–10	攻撃のかまえ	attack stance
        // 1	20–30	ダメージ	damage
        internal static readonly EnemyDefaults RightHand = new EnemyDefaults {
            Id=119, TableIndex=86, ModelCode="c17c", Name="Right Hand",      MaxHp=3200,  Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  DamageReduction=0,  WeaponDefense=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        // Left Hand has no EID=120 record in the table; its HP (90) is stored in Right Hand's u98
        // via the off-by-one. The game spawns it using an anonymous EID=0 record at idx=87.
        // Motions: ModelCode "c17_" has no own .chr (shares the hand model); see Right Hand (c17c) above.
        internal static readonly EnemyDefaults LeftHand = new EnemyDefaults {
            Id=120, TableIndex=87, ModelCode="c17_", Name="Left Hand",       MaxHp=90,    Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f,  EntityScaleCopy=5.0f,  DamageReduction=0,  WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };  // idx=87 is the anonymous EID=0 record

        // These are ATTACK/EFFECT entities (projectiles, beams, barriers, summons), not standalone enemies —
        // ModelCode is a FAMILY PREFIX, not an exact filename, and they have no "死亡": they vanish via
        // 消滅/爆発 (despawn/explode). For these the boss-death system doesn't apply (scripted despawn).

        // Dark Genie fight companions: code=c17_ = DG attack-effect family. The prefix maps to several .chr:
        // c17_beem.chr (発射/ループ/消滅 = launch/loop/despawn), c17_kaze.chr (wind), c17_hikari.chr (light),
        // c17_syougeki.chr (shock). e.g. c17_beem @ data.dat 0x1b1e9000. Which TableIndex→which is unconfirmed.
        internal static readonly EnemyDefaults DGComp88 = new EnemyDefaults {
            Id=0, TableIndex=88, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=0, AttackPower=65535,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };
        // Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–50	発射	launch
        // 1	60–68	ループ	(loop)
        // 2	80–90	消滅	despawn
        internal static readonly EnemyDefaults DGComp89 = new EnemyDefaults {
            Id=0, TableIndex=89, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=0, AttackPower=65535,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };
        // code=c17_
        // Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–50	発射	launch
        // 1	60–68	ループ	(loop)
        // 2	80–90	消滅	despawn
        internal static readonly EnemyDefaults DGComp90 = new EnemyDefaults {
            Id=0, TableIndex=90, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=0, AttackPower=65535,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };
        // code=c17_
        // Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–50	発射	launch
        // 1	60–68	ループ	(loop)
        // 2	80–90	消滅	despawn
        internal static readonly EnemyDefaults DGComp93 = new EnemyDefaults {
            Id=0, TableIndex=93, ModelCode="c17_", Name="(DG companion c17_)", MaxHp=0, AttackPower=65535,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // Motions: e85a.chr info.cfg @ data.dat 0x1ddf5000 — 2 (object; no death anim).
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–15	回る	spin / roll
        // 1	20–25	落ちてる	lying fallen
        internal static readonly EnemyDefaults WineKeg = new EnemyDefaults {
            Id=121, TableIndex=91, ModelCode="e85a", Name="Wine Keg",        MaxHp=80,    Abs=0,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f,  EntityScaleCopy=7.0f,  DamageReduction=0,  WeaponDefense=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0,
            MeleeDamage=new int[]{8}, ProjectileDamage=new int[]{} };

        // DS boss — confirmed from EnemySpeciesTable scan 2026-06-05: tbl_165 is BlackKnight (id=221, hp=50000, BOSS, code=c22a).
        // Motions: c21a.chr info.cfg @ data.dat 0x1e3d5800. (c22a is the same list + motion 27; see below.)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	50–70	走り	run
        // 3	75–95	バックステップ	back step
        // 4	100–120	右ステップ	right step
        // 5	125–145	左ステップ	left step
        // 6	150–160	ダメージ１	damage 1
        // 7	265–285	死亡	death ← collapse
        // 8	285	死亡ループ	death loop
        // 9	25–45	右歩き	right walk
        // 10	25–45	左歩き	left walk
        // 11	165–185	攻撃１（突進）	attack 1 (charge)
        // 12	190–225	攻撃２（円月輪）	attack 2 (chakram)
        // 13	230–260	攻撃３（カマイタチ）	attack 3 (wind-slash)
        // 14	290–310	死亡後のつなぎ	post-death link
        // 15	315–325	立ち	idle (form 2)
        // 16	330–350	歩き	walk (form 2)
        // 17	380–400	右ステップ	right step (form 2)
        // 18	405–425	左ステップ	left step (form 2)
        // 19	355–375	バックステップ	back step (form 2)
        // 20	430–440	ダメージ１	damage 1 (form 2)
        // 21	535–540	ガード入り	guard (enter)
        // 22	540–550	ガードループ	guard (loop)
        // 23	550–555	ガード戻り	guard (return)
        // 24	510–530	攻撃１（二刀流斬りその1）	attack 1 (dual-slash A)
        // 25	445–470	攻撃２（円月輪）	attack 2 (chakram)
        // 26	560–585	攻撃１（二刀流斬りその2）	attack 1 (dual-slash B)
        internal static readonly EnemyDefaults BlackKnight = new EnemyDefaults {
            Id=221, TableIndex=165, ModelCode="c21a", Name="Black Knight",    MaxHp=50000, Abs=5,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=8, WeaponDefense=100,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=50,
            MeleeDamage=new int[]{170,170}, ProjectileDamage=new int[]{26,6,8,8} };

        // tbl_166 = Black Knight Mount; present in DS floor 100 binary pool.
        // Motions: c22a.chr info.cfg @ data.dat 0x1e565800 — identical to c21a (0–26, death=7) plus:
        // Idx	Frames	Name (JP)	Meaning
        // 27	595–645	攻撃３（強攻撃）	attack 3 (heavy attack)
        internal static readonly EnemyDefaults BlackKnightMount = new EnemyDefaults {
            Id=221, TableIndex=166, ModelCode="c22a", Name="Black Knight", MaxHp=50000, AttackPower=0,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{4,15,-1} };

        // ── Demon Shaft enhanced tier variants ────────────────────────────────────
        // These reuse base-game species IDs with new model codes and scaled stats.
        // Not added to Defaults dictionary to avoid overwriting base-game entries.
        // Stats from EnemySpeciesTable scan 2026-06-05.

        // GemronFire group (floors 1-20): tbl_113–120
        // e125
        // Motions: e125a.chr @ data.dat 0x1ec7d000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	90–110	バックステップ	back step
        // 3	190–210	右ステップ	right step
        // 4	215–235	左ステップ	left step
        // 5	240–250	ガード入り	guard (enter)
        // 6	250–260	ガードループ	guard (loop)
        // 7	260–270	ガード戻り	guard (return)
        // 8	115–125	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	130–160	死亡	death
        // 12	160	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–70	ダミー	(dummy)
        // 15	50–70	ダミー	(dummy)
        // 16	70–90	攻撃2	attack2
        // 17	70–90	ダミー	(dummy)
        // 18	70–90	ダミー	(dummy)
        internal static readonly EnemyDefaults WhiteFangEnhanced = new EnemyDefaults {
            Id=56,  TableIndex=113, ModelCode="e125", Name="White Fang (Enhanced)",  MaxHp=1750,  Abs=15,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        // e126
        // Motions: e126a.chr @ data.dat 0x1ed6b000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	70–80	右ｽﾃｯﾌﾟ	right step
        // 4	90–100	左ｽﾃｯﾌﾟ	left step
        // 5	110–120	ｶﾞｰﾄﾞ	guard
        // 6	130–140	ｶﾞｰﾄﾞループ	guard (loop)
        // 7	150–160	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	170–190	ﾀﾞﾒｰｼﾞ	damage
        // 9	200–230	死亡	death
        // 10	230	死亡ループ	death loop
        // 11	240–270	攻撃１	attack１
        // 12	280–310	攻撃２	attack２
        // 13	320–340	攻撃３	attack３
        // 14	350–360	攻撃３ループ	attack３(loop)
        // 15	370–390	攻撃３戻り	attack３(return)
        // 16	0–10	立ち	idle
        internal static readonly EnemyDefaults ArthurEnhanced = new EnemyDefaults {
            Id=40,  TableIndex=114, ModelCode="e126", Name="Arthur (Enhanced)",  MaxHp=2900,  Abs=20,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=50, IceRes=50, ThunderRes=150, WindRes=80, HolyRes=80,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        // e127
        // Motions: e127a.chr @ data.dat 0x1edd3000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults SilEnhanced = new EnemyDefaults {
            Id=91,  TableIndex=115, ModelCode="e127", Name="Sil (Enhanced)",  MaxHp=1500,  Abs=15,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=10, WeaponDefense=60,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            MeleeDamage=new int[]{111,111,115,115,102,95,85}, ProjectileDamage=new int[]{1} };

        // e128
        // Motions: e128a.chr @ data.dat 0x1ee7a800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–45	前移動ループ	move loop (fwd)
        // 2	30–45	ダミー	(dummy)
        // 3	110–130	右ステップ	right step
        // 4	140–160	左ステップ	left step
        // 5	170–180	ガード入り	guard (enter)
        // 6	180–190	ガードループ	guard (loop)
        // 7	190–200	ガード戻り	guard (return)
        // 8	210–220	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	230–255	死亡	death
        // 12	55–79	攻撃1	attack1
        // 13	265–285	バックステップ	back step
        // 14	295–315	歩き	walk
        // 15	85–103	攻撃2	attack2
        // 16	85–103	ダミー	(dummy)
        // 17	85–103	ダミー	(dummy)
        internal static readonly EnemyDefaults HalloweenEnhanced = new EnemyDefaults {
            Id=10,  TableIndex=116, ModelCode="e128", Name="Halloween (Enhanced)",  MaxHp=1800,  Abs=15,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=168, ItemResA=100, ItemResB=70, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{100} };

        // e129
        // Motions: e129a.chr @ data.dat 0x1ef24000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–70	攻撃1	attack1
        // 14	50–61	振りかぶり	wind-up
        // 15	61–70	振り下ろし	downswing
        // 16	75–95	攻撃2	attack2
        // 17	75–95	ダミー	(dummy)
        // 18	75–95	ダミー	(dummy)
        // 19	280–295	飛び込み	lunge
        internal static readonly EnemyDefaults MasterJacketEnhanced = new EnemyDefaults {
            Id=1,   TableIndex=117, ModelCode="e129", Name="Master Jacket (Enhanced)",  MaxHp=2000,  Abs=15,  MinGoldDrop=7,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80, ThunderRes=100, WindRes=80, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=177, ItemResA=100, ItemResB=80, AttackPower=150, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        // e130
        // Motions: e130a.chr @ data.dat 0x1f034800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	70–90	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	100–120	左ｽﾃｯﾌﾟ	left step
        // 4	130–150	右ｽﾃｯﾌﾟ	right step
        // 5	160–180	ｶﾞｰﾄﾞ入り	guard(enter)
        // 6	190–200	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	210–230	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	240–270	死亡	death
        // 9	270	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 10	280–310	ｻﾝﾄﾞｱｯﾊﾟｰ	ｻﾝﾄﾞｱｯﾊﾟｰ
        // 11	320–340	爪	爪
        internal static readonly EnemyDefaults VulcanEnhanced = new EnemyDefaults {
            Id=70,  TableIndex=118, ModelCode="e130", Name="Vulcan (Enhanced)",  MaxHp=2400,  Abs=15,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=0, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=10, WeaponDefense=40,
            StealItemId=81, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{114,114,114}, ProjectileDamage=new int[]{} };

        // e131
        // Motions: e131a.chr @ data.dat 0x1f0d3000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–310	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–105	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	115–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	360–385	攻撃1 （引っかき）	attack1 (claw)
        // 14	360–385	ダミー	(dummy)
        // 15	360–385	ダミー	(dummy)
        // 16	320–350	攻撃2  (ブレス）	attack2 (breath)
        // 17	320–350	ダミー	(dummy)
        // 18	320–350	ダミー	(dummy)
        internal static readonly EnemyDefaults MummyEnhanced = new EnemyDefaults {
            Id=50,  TableIndex=119, ModelCode="e131", Name="Mummy (Enhanced)",  MaxHp=1500,  Abs=5,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=10, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{98,98}, ProjectileDamage=new int[]{} };

        // e132
        // Motions: e132a.chr @ data.dat 0x1f218000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(つき286/hit）	attack1(つき286/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults DiamondEnhanced = new EnemyDefaults {
            Id=46,  TableIndex=120, ModelCode="e132", Name="Diamond (Enhanced)",  MaxHp=1750,  Abs=10,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50, WindRes=150, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=10, WeaponDefense=50,
            StealItemId=151, ItemResA=80, ItemResB=50, AttackPower=135, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{123,123}, ProjectileDamage=new int[]{} };


        // GemronIce group (floors 21-40): tbl_123–129
        // e133
        // Motions: e133a.chr @ data.dat 0x1f2be800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	左ｽﾃｯﾌﾟ	left step
        // 3	80–90	右ｽﾃｯﾌﾟ	right step
        // 4	100–110	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 5	120–125	ｶﾞｰﾄﾞ	guard
        // 6	125–135	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	135–140	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	150–160	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	165–170	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	175–180	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	185–195	死亡	death
        // 12	195	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	210–230	攻撃１	attack１
        // 14	210–230	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 15	210–230	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 16	240–260	攻撃２	attack２
        // 17	240–260	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	240–260	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        internal static readonly EnemyDefaults AuntieMeduEnhanced = new EnemyDefaults {
            Id=26,  TableIndex=123, ModelCode="e133", Name="Auntie Medu (Enhanced)",  MaxHp=3750,  Abs=20,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=30, IceRes=150, ThunderRes=30, WindRes=30, HolyRes=30,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=166, ItemResA=100, ItemResB=60, AttackPower=245, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        // e134
        // Motions: e134a.chr @ data.dat 0x1f35e800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バック	バック
        // 3	70–80	右	右
        // 4	90–100	左	左
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	110–120	ダメージ1	damage1
        // 9	130–150	ダメージ2	damage2
        // 10	150–160	起き上がり	get up
        // 11	170–190	死亡	death
        // 12	190	死亡ループ	death loop
        // 13	200–210	攻撃1	attack1
        // 14	210–220	攻撃1	attack1
        // 15	220–230	攻撃1	attack1
        // 16	240–260	攻撃2	attack2
        // 17	240–260	攻撃2	attack2
        // 18	240–260	攻撃2	attack2
        internal static readonly EnemyDefaults RockanoffEnhanced = new EnemyDefaults {
            Id=77,  TableIndex=124, ModelCode="e134", Name="Rockanoff (Enhanced)",  MaxHp=2500,  Abs=20,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=15, WeaponDefense=50,
            StealItemId=160, ItemResA=90, ItemResB=60, AttackPower=160, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        // e135
        // Motions: e135a.chr @ data.dat 0x1f392000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–15	立ち	idle
        // 1	5–15	歩き	walk
        // 2	20–35	バックステップ	back step
        // 3	40–50	右ステップ	right step
        // 4	55–65	左ステップ	left step
        // 5	5–15	ガード入り	guard (enter)
        // 6	5–15	ガードループ	guard (loop)
        // 7	5–15	ガード戻り	guard (return)
        // 8	130–140	ダメージ1	damage1
        // 9	130–140	ダメージ2	damage2
        // 10	130–140	起き上がり	get up
        // 11	145–165	死亡	death
        // 12	165	死亡ループ	death loop
        // 13	75–90	攻撃１入り	attack１(enter)
        // 14	95–105	攻撃１ループ	attack１(loop)
        // 15	110–125	攻撃１戻り	attack１(return)
        internal static readonly EnemyDefaults YammichEnhanced = new EnemyDefaults {
            Id=301, TableIndex=125, ModelCode="e135", Name="Yammich (Enhanced)",  MaxHp=3000,  Abs=20,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        // e136
        // Motions: e136a.chr @ data.dat 0x1f3eb800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	移動ループ前	move loop (fwd)
        // 2	50–60	移動ループ後	move loop (back)
        // 3	70–80	移動ループ右	move loop右
        // 4	90–100	移動ループ左	move loop左
        // 5	165–170	ガード入り	guard (enter)
        // 6	170–180	ガードループ	guard (loop)
        // 7	180–185	ガード戻り	guard (return)
        // 8	195–205	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	215–235	死亡	death
        // 12	110–128	攻撃1	attack1
        // 13	110–128	ダミー	(dummy)
        // 14	110–128	ダミー	(dummy)
        // 15	110–128	ダミー	(dummy)
        // 16	110–128	ダミー	(dummy)
        // 17	110–128	ダミー	(dummy)
        internal static readonly EnemyDefaults WitchHellzaEnhanced = new EnemyDefaults {
            Id=21,  TableIndex=126, ModelCode="e136", Name="Witch Hellza (Enhanced)",  MaxHp=1500,  Abs=20,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=169, ItemResA=85, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{100}, ProjectileDamage=new int[]{2} };

        // e137
        // Motions: e137a.chr @ data.dat 0x1f486800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults SteelGiantEnhanced = new EnemyDefaults {
            Id=64,  TableIndex=127, ModelCode="e137", Name="Steel Giant (Enhanced)",  MaxHp=3900,  Abs=25,  MinGoldDrop=15,  DropChance=50,
            Category=EnemyCategory.Metal, FireRes=80, IceRes=80, ThunderRes=150, WindRes=80, HolyRes=80,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=15, WeaponDefense=70,
            StealItemId=177, ItemResA=95, ItemResB=50, AttackPower=154, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=80, ElemAtkDark=20,
            MeleeDamage=new int[]{163,163,178,178,163,162,161}, ProjectileDamage=new int[]{1,164} };

        // e138
        // Motions: e138a.chr @ data.dat 0x1f52b800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(殴る929/hit）	attack1(殴る929/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults ClubEnhanced = new EnemyDefaults {
            Id=45,  TableIndex=128, ModelCode="e138", Name="Club (Enhanced)",  MaxHp=2525,  Abs=20,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=147, ItemResA=80, ItemResB=50, AttackPower=134, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{114}, ProjectileDamage=new int[]{} };

        // e139
        // Motions: e139a.chr @ data.dat 0x1f5d6800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	115–125	バックステップ	back step
        // 3	140–150	右ステップ	right step
        // 4	160–170	左ステップ	left step
        // 5	180–184	ガード入り	guard (enter)
        // 6	184–188	ガードループ	guard (loop)
        // 7	188–192	ガード戻り	guard (return)
        // 8	200–210	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	215–235	死亡	death
        // 12	235	死亡ループ	death loop
        // 13	60–81	攻撃1	attack1
        // 14	60–81	ダミー	(dummy)
        // 15	60–81	ダミー	(dummy)
        // 16	90–106	攻撃2	attack2
        // 17	90–106	ダミー	(dummy)
        // 18	90–106	ダミー	(dummy)
        internal static readonly EnemyDefaults CorceaEnhanced = new EnemyDefaults {
            Id=28,  TableIndex=129, ModelCode="e139", Name="Corcea (Enhanced)",  MaxHp=3250,  Abs=20,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=15, WeaponDefense=10,
            StealItemId=152, ItemResA=100, ItemResB=70, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110,110,110,110}, ProjectileDamage=new int[]{} };


        // GemronThunder group (floors 41-60): tbl_134–142
        // e140
        // Motions: e140a.chr @ data.dat 0x1f67a000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 3	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 4	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 5	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 6	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 7	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 8	70–80	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 10	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 11	90–100	死亡始まり	death始まり
        // 12	100–105	落下ﾙｰﾌﾟ	落下ﾙｰﾌﾟ
        // 13	105–115	死亡	death
        // 14	115	死亡停止	death停止
        // 15	120–145	攻撃１	attack１
        // 16	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 17	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	150–171	攻撃2予備動作	attack2予備動作
        // 19	171–189	攻撃	attack
        // 20	189–215	戻り	(return)
        internal static readonly EnemyDefaults CaveBatEnhanced = new EnemyDefaults {
            Id=60,  TableIndex=134, ModelCode="e140", Name="Cave Bat (Enhanced)",  MaxHp=1500,  Abs=25,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=151, ItemResA=100, ItemResB=90, AttackPower=0, ElemAtkFire=100, ElemAtkIce=150, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{95,95}, ProjectileDamage=new int[]{} };

        // e141
        // Motions: e141a.chr @ data.dat 0x1f6b5800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults GolEnhanced = new EnemyDefaults {
            Id=90,  TableIndex=135, ModelCode="e141", Name="Gol (Enhanced)",  MaxHp=6000,  Abs=25,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=30, WeaponDefense=80,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{131,131,135,135,132,125,125}, ProjectileDamage=new int[]{1} };

        // e142
        // Motions: e142a.chr @ data.dat 0x1f75d000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バックステップ	back step
        // 3	70–80	右ステップ	right step
        // 4	90–100	左ステップ	left step
        // 5	110–115	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	140–150	ダメージ１	damage１
        // 9	160–172	ダメージ２ (吹っ飛び）	damage２ (吹っ飛び)
        // 10	10–20	起き上がり（歩きで移動）	get up(walkでmove)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	260–290	攻撃1（切り裂き）	attack1(切り裂き)
        // 14	260–290	攻撃1予備動作１	attack1予備動作１
        // 15	260–290	攻撃1予備動作２	attack1予備動作２
        // 16	240–255	攻撃２（ファイヤーボール）	attack２(ファイヤーボール)
        // 17	240–255	攻撃２予備動作１	attack２予備動作１
        // 18	240–255	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults MaskOfPrajnaEnhanced = new EnemyDefaults {
            Id=75,  TableIndex=136, ModelCode="e142", Name="Mask of Prajna (Enhanced)",  MaxHp=5500,  Abs=25,  MinGoldDrop=15,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.5f, EntityScaleCopy=7.5f, DamageReduction=20, WeaponDefense=10,
            StealItemId=151, ItemResA=80, ItemResB=70, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{140,138}, ProjectileDamage=new int[]{146} };

        // e143
        // Motions: e143a.chr @ data.dat 0x1f7cd000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	バタアシ	バタアシ
        // 2	60–80	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	90–110	右ｽﾃｯﾌﾟ	right step
        // 4	120–140	左ｽﾃｯﾌﾟ	left step
        // 5	150–160	ｶﾞｰﾄﾞ	guard
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	190–200	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	210–225	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	235–260	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	270–290	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	300–325	死亡	death
        // 12	325	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	335–355	攻撃１	attack１
        // 14	365–385	攻撃２	attack２
        // 15	390–410	攻撃3	attack3
        internal static readonly EnemyDefaults GyonEnhanced = new EnemyDefaults {
            Id=24,  TableIndex=137, ModelCode="e143", Name="Gyon (Enhanced)",  MaxHp=5750,  Abs=25,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=134, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{2} };

        // e144
        // Motions: e144a.chr @ data.dat 0x1f86a800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(切る290/hit）	attack1(切る290/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults SpadeEnhanced = new EnemyDefaults {
            Id=47,  TableIndex=138, ModelCode="e144", Name="Spade (Enhanced)",  MaxHp=5000,  Abs=25,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=152, ItemResA=80, ItemResB=50, AttackPower=132, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        // e145
        // Motions: e145a.chr @ data.dat 0x1f913800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	120–140	バックステップ	back step
        // 3	60–80	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡ループ	death loop
        // 13	150–180	攻撃1(縦振り）	attack1(縦振り)
        // 14	150–180	攻撃1予備動作１	attack1予備動作１
        // 15	150–180	攻撃1予備動作２	attack1予備動作２
        // 16	190–215	攻撃２（走り頭突き入り）	attack２(run頭突き(enter))
        // 17	215–223	攻撃２予備動作１（走りリピート）	attack２予備動作１(runリピート)
        // 18	223–230	攻撃２予備動作２ （走り頭突き終わり）	attack２予備動作２ (run頭突き終わり)
        internal static readonly EnemyDefaults RashDasherEnhanced = new EnemyDefaults {
            Id=63,  TableIndex=139, ModelCode="e145", Name="Rash Dasher (Enhanced)",  MaxHp=5000,  Abs=25,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=50, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=149, ItemResA=100, ItemResB=70, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        // e146
        // Motions: e146a.chr @ data.dat 0x1f994000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	歩き	walk
        // 1	30–50	歩き	walk
        // 2	115–125	バックステップ	back step
        // 3	140–150	右ステップ	right step
        // 4	160–170	左ステップ	left step
        // 5	180–184	ガード入り	guard (enter)
        // 6	184–188	ガードループ	guard (loop)
        // 7	188–192	ガード戻り	guard (return)
        // 8	200–210	ダメージ1	damage1
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	235	死亡ループ	death loop
        // 13	60–81	攻撃1	attack1
        // 14	60–81	ダミー	(dummy)
        // 15	60–81	ダミー	(dummy)
        // 16	90–106	攻撃2	attack2
        // 17	90–106	ダミー	(dummy)
        // 18	90–106	ダミー	(dummy)
        internal static readonly EnemyDefaults CaptainEnhanced = new EnemyDefaults {
            Id=27,  TableIndex=140, ModelCode="e146", Name="Captain (Enhanced)",  MaxHp=4000,  Abs=25,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80, WindRes=80, HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=227, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{99,99,99,99}, ProjectileDamage=new int[]{} };

        // e109
        // Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	まちかまえ	まちstance
        internal static readonly EnemyDefaults MimicDSEnhanced = new EnemyDefaults {
            Id=309, TableIndex=141, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced)",  MaxHp=5000,  Abs=10,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=20, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // e110
        // Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicDSEnhanced = new EnemyDefaults {
            Id=310, TableIndex=142, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced)",  MaxHp=7500,  Abs=20,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=20, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };


        // GemronWind group (floors 61-80): tbl_145–153
        // e149
        // Motions: e149a.chr @ data.dat 0x1fb03800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–40	歩き	walk
        // 2	50–60	バックステップ	back step
        // 3	70–80	右ステップ	right step
        // 4	90–100	左ステップ	left step
        // 5	110–115	ガード入り	guard (enter)
        // 6	115–125	ガードループ	guard (loop)
        // 7	125–130	ガード戻り	guard (return)
        // 8	140–150	ダメージ１	damage１
        // 9	160–172	ダメージ２ (吹っ飛び）	damage２ (吹っ飛び)
        // 10	10–20	起き上がり（歩きで移動）	get up(walkでmove)
        // 11	180–200	死亡	death
        // 12	200	死亡ループ	death loop
        // 13	210–230	攻撃1（切り裂き）	attack1(切り裂き)
        // 14	210–230	攻撃1予備動作１	attack1予備動作１
        // 15	210–230	攻撃1予備動作２	attack1予備動作２
        // 16	240–255	攻撃２（ファイヤーボール）	attack２(ファイヤーボール)
        // 17	240–255	攻撃２予備動作１	attack２予備動作１
        // 18	240–255	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults AlexanderEnhanced = new EnemyDefaults {
            Id=43,  TableIndex=145, ModelCode="e149", Name="Alexnder (Enhanced)",  MaxHp=7500,  Abs=30,  MinGoldDrop=17,  DropChance=50,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=120, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, DamageReduction=23, WeaponDefense=50,
            StealItemId=164, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        // e150
        // Motions: e150a.chr @ data.dat 0x1fb54000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(ビーム292/hit）	attack1(beam292/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults HeartEnhanced = new EnemyDefaults {
            Id=44,  TableIndex=146, ModelCode="e150", Name="Heart (Enhanced)",  MaxHp=5000,  Abs=30,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=150, ItemResA=80, ItemResB=50, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // e151
        // Motions: e151a.chr @ data.dat 0x1fbfe800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–45	ジャンプ歩き	jumpwalk
        // 2	350–370	バックステップ	back step
        // 3	110–130	右ステップ	right step
        // 4	140–160	左ステップ	left step
        // 5	170–180	ガード入り	guard (enter)
        // 6	180–190	ガードループ	guard (loop)
        // 7	190–200	ガード戻り	guard (return)
        // 8	210–220	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	230–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–300	攻撃1(殴り）	attack1(殴り)
        // 14	270–300	攻撃1予備動作1	attack1予備動作1
        // 15	270–300	攻撃1予備動作2	attack1予備動作2
        // 16	310–321	攻撃2 （ジャンプ入り）	attack2 (jump(enter))
        // 17	321–324	攻撃2予備動作1 （ジャンプ中）	attack2予備動作1 (jump中)
        // 18	324–335	攻撃2予備動作2 （着地）	attack2予備動作2 (landing)
        // 19	85–103	攻撃3 （投げ）	attack3 (throw)
        // 20	85–103	攻撃3予備動作1	attack3予備動作1
        // 21	85–103	攻撃3予備動作2	attack3予備動作2
        // 22	380–400	歩き	walk
        internal static readonly EnemyDefaults BomberHeadEnhanced = new EnemyDefaults {
            Id=49,  TableIndex=147, ModelCode="e151", Name="Bomber Head (Enhanced)",  MaxHp=6000,  Abs=30,  MinGoldDrop=10,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=20,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=23, WeaponDefense=20,
            StealItemId=159, ItemResA=100, ItemResB=70, AttackPower=159, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{160} };

        // e152
        // Motions: e152a.chr @ data.dat 0x1fcc1000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	30–42	立ち〈ダミー）	idle〈(dummy))
        // 1	30–42	歩き	walk
        // 2	70–80	バックステップ	back step
        // 3	90–100	右ステップ	right step
        // 4	110–120	左ステップ	left step
        // 5	130–136	ガード入り	guard (enter)
        // 6	136–142	ガードループ	guard (loop)
        // 7	142–146	ガード戻り	guard (return)
        // 8	150–170	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–190	死亡	death
        // 12	190	死亡ループ	death loop
        // 13	200–215	攻撃1(はさみ）	attack1(はさみ)
        // 14	200–215	攻撃1予備動作１	attack1予備動作１
        // 15	200–215	攻撃1予備動作２	attack1予備動作２
        // 16	220–245	攻撃２（口泡）	attack２(口泡)
        // 17	220–245	攻撃２予備動作１	attack２予備動作１
        // 18	220–245	攻撃２予備動作２	attack２予備動作２
        // 19	255–275	攻撃２（棘）	attack２(棘)
        // 20	255–275	攻撃２予備動作１	attack２予備動作１
        // 21	255–275	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults CrabbyHermitEnhanced = new EnemyDefaults {
            Id=71,  TableIndex=148, ModelCode="e152", Name="Crabby Hermit (Enhanced)",  MaxHp=6500,  Abs=30,  MinGoldDrop=12,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, DamageReduction=23, WeaponDefense=20,
            StealItemId=166, ItemResA=95, ItemResB=70, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{130} };

        // e153
        // Motions: e153a.chr @ data.dat 0x1fd1a000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	35–55	ダメージ	damage
        // 2	60–80	死亡	death
        // 3	80	死亡ループ	death loop
        // 4	85–120	攻撃葉	attack葉
        // 5	125–160	攻撃液前	attack液前
        // 6	165–200	攻撃液上	attack液上
        internal static readonly EnemyDefaults CursedRoseEnhanced = new EnemyDefaults {
            Id=68,  TableIndex=149, ModelCode="e153", Name="Cursed Rose (Enhanced)",  MaxHp=5000,  Abs=30,  MinGoldDrop=6,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=130, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=6.5f, EntityScaleCopy=6.5f, DamageReduction=23, WeaponDefense=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=146, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{140,140,140}, ProjectileDamage=new int[]{1} };

        // e154
        // Motions: e154a.chr @ data.dat 0x1fd72800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	後退	後退
        // 3	90–110	右旋回	右turn
        // 4	120–140	左旋回	左turn
        // 5	10–20	ダミー	(dummy)
        // 6	10–20	ダミー	(dummy)
        // 7	10–20	ダミー	(dummy)
        // 8	150–160	ダメージ	damage
        // 9	10–20	ダミー	(dummy)
        // 10	10–20	ダミー	(dummy)
        // 11	170–185	死亡	death
        // 12	185	死亡ループ	death loop
        // 13	190–210	攻撃1	attack1
        // 14	190–210	ダミー	(dummy)
        // 15	190–210	ダミー	(dummy)
        // 16	250–280	攻撃2予備動作	attack2予備動作
        // 17	250–280	攻撃	attack
        // 18	250–280	戻り	(return)
        internal static readonly EnemyDefaults PiratesChariotEnhanced = new EnemyDefaults {
            Id=25,  TableIndex=150, ModelCode="e154", Name="Pirate's Chariot (Enhanced)",  MaxHp=6750,  Abs=30,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80, ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, DamageReduction=23, WeaponDefense=60,
            StealItemId=159, ItemResA=95, ItemResB=60, AttackPower=92, ElemAtkFire=80, ElemAtkIce=80, ElemAtkThunder=150, ElemAtkWind=80, ElemAtkHoly=20, ElemAtkDark=100,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{114} };

        // e155
        // Motions: e155a.chr @ data.dat 0x1fdb3800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	バタアシ	バタアシ
        // 2	60–80	ﾊﾞｯｸｽﾃｯﾌﾟ	back step
        // 3	90–110	右ｽﾃｯﾌﾟ	right step
        // 4	120–140	左ｽﾃｯﾌﾟ	left step
        // 5	150–160	ｶﾞｰﾄﾞ	guard
        // 6	170–180	ｶﾞｰﾄﾞﾙｰﾌﾟ	guardﾙｰﾌﾟ
        // 7	190–200	ｶﾞｰﾄﾞ戻り	guard (return)
        // 8	210–225	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	235–260	ﾀﾞﾒｰｼﾞ２	damage２
        // 10	270–290	ﾀﾞﾒｰｼﾞ戻り	damage(return)
        // 11	300–325	死亡	death
        // 12	325	死亡ﾙｰﾌﾟ	deathﾙｰﾌﾟ
        // 13	335–355	攻撃１	attack１
        // 14	365–385	攻撃２	attack２
        // 15	390–410	攻撃3	attack3
        internal static readonly EnemyDefaults SpaceGyonEnhanced = new EnemyDefaults {
            Id=72,  TableIndex=151, ModelCode="e155", Name="Space Gyon (Enhanced)",  MaxHp=7800,  Abs=30,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=0, IceRes=0, ThunderRes=20, WindRes=0, HolyRes=0,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=153, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{2} };

        // e109
        // Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	まちかまえ	まちstance
        internal static readonly EnemyDefaults MimicDSEnhancedTwice = new EnemyDefaults {
            Id=309, TableIndex=152, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced x2)",  MaxHp=6500,  Abs=15,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=23, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // e110
        // Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicDSEnhancedTwice = new EnemyDefaults {
            Id=310, TableIndex=153, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced x2)",  MaxHp=10000,  Abs=25,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=23, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };


        // GemronHoly group (floors 81-99): tbl_155–165
        // e117
        // Motions: e117a.chr @ data.dat 0x1e2d6800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	25–45	歩き	walk
        // 2	295–305	バックステップ	back step
        // 3	230–250	右ステップ	right step
        // 4	255–275	左ステップ	left step
        // 5	100–104	ガード入り	guard (enter)
        // 6	105–115	ガードループ	guard (loop)
        // 7	116–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–165	ダメージ2	damage2
        // 10	165–185	起き上がり	get up
        // 11	205–225	死亡	death
        // 12	225	死亡ループ	death loop
        // 13	50–64	攻撃1	attack1
        // 14	64–90	攻撃11	attack11
        // 15	50–90	ダミー	(dummy)
        // 16	310–330	攻撃2	attack2
        // 17	330–360	攻撃2	attack2
        // 18	310–360	ダミー	(dummy)
        internal static readonly EnemyDefaults GaciousEnhanced = new EnemyDefaults {
            Id=317, TableIndex=155, ModelCode="e117", Name="Gacious (Enhanced)",  MaxHp=15000,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=0, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=140,
            EntityScale=11.0f, EntityScaleCopy=11.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=189, ItemResA=100, ItemResB=90, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{180,180,180,180,180}, ProjectileDamage=new int[]{} };

        // e158
        // Motions: e158a.chr @ data.dat 0x1ff1e800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–30	立ち	idle
        // 1	40–60	歩き	walk
        // 2	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 3	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 4	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 5	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 6	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 7	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 8	70–80	ﾀﾞﾒｰｼﾞ１	damage１
        // 9	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 10	40–60	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 11	90–100	死亡始まり	death始まり
        // 12	100–105	落下ﾙｰﾌﾟ	落下ﾙｰﾌﾟ
        // 13	105–115	死亡	death
        // 14	115	死亡停止	death停止
        // 15	120–145	攻撃１	attack１
        // 16	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 17	120–145	ﾀﾞﾐｰ	ﾀﾞﾐｰ
        // 18	150–171	攻撃2予備動作	attack2予備動作
        // 19	171–175	攻撃	attack
        // 20	175–200	攻撃2予備動作	attack2予備動作
        internal static readonly EnemyDefaults EvilBatEnhanced = new EnemyDefaults {
            Id=61,  TableIndex=156, ModelCode="e158", Name="Evil Bat (Enhanced)",  MaxHp=7500,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=150, IceRes=150, ThunderRes=150, WindRes=150, HolyRes=200,
            EntityScale=3.0f, EntityScaleCopy=3.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=149, ElemAtkFire=100, ElemAtkIce=200, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        // e159
        // Motions: e159a.chr @ data.dat 0x1ff5a000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	バック	バック
        // 3	90–110	右	右
        // 4	120–140	左	左
        // 5	150–155	ガード（入り）	guard((enter))
        // 6	155–165	ガードループ	guard (loop)
        // 7	165–170	ガード戻り	guard (return)
        // 8	180–190	ダメージ1	damage1
        // 9	200–220	ダメージ2	damage2
        // 10	10–20	ダミー	(dummy)
        // 11	230–250	死亡	death
        // 12	250	死亡ループ	death loop
        // 13	260–280	攻撃1	attack1
        // 14	260–280	攻撃1	attack1
        // 15	260–280	攻撃1	attack1
        // 16	290–310	攻撃2	attack2
        // 17	290–310	攻撃2	attack2
        // 18	290–310	攻撃2	attack2
        // 19	320–340	攻撃3	attack3
        internal static readonly EnemyDefaults CrescentBaronEnhanced = new EnemyDefaults {
            Id=76,  TableIndex=157, ModelCode="e159", Name="Crescent Baron (Enhanced)",  MaxHp=16000,  Abs=35,  MinGoldDrop=18,  DropChance=50,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=null, ItemResA=80, ItemResB=70, AttackPower=170, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        // e160
        // Motions: e160a.chr @ data.dat 0x1ffcf800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	5–15	立ち	idle
        // 1	20–40	歩き	walk
        // 2	45–55	バックステップ	back step
        // 3	60–70	右ステップ	right step
        // 4	75–85	左ステップ	left step
        // 5	90–100	ガード入り	guard (enter)
        // 6	100	ガードループ	guard (loop)
        // 7	105–120	ガード戻り	guard (return)
        // 8	125–135	ダメージ1	damage1
        // 9	140–160	死亡	death
        // 10	160	死亡L	deathL
        // 11	165–180	攻撃1	attack1
        internal static readonly EnemyDefaults StatueDogEnhanced = new EnemyDefaults {
            Id=303, TableIndex=158, ModelCode="e160", Name="Statue Dog (Enhanced)",  MaxHp=12500,  Abs=35,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        // e161
        // Motions: e161a.chr @ data.dat 0x20023800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–70	バックステップ	back step
        // 3	80–90	右ステップ	right step
        // 4	100–110	左ステップ	left step
        // 5	120–130	ガード入り	guard (enter)
        // 6	130–140	ガードループ	guard (loop)
        // 7	140–150	ガード戻り	guard (return)
        // 8	160–175	ダメージ１	damage１
        // 9	185–200	ダメージ２	damage２
        // 10	210–225	起き上がり	get up
        // 11	235–255	死亡	death
        // 12	255	死亡ループ	death loop
        // 13	270–305	攻撃1(裂く286/hit）	attack1(裂く286/hit)
        // 14	270–305	攻撃1予備動作１	attack1予備動作１
        // 15	270–305	攻撃1予備動作２	attack1予備動作２
        // 16	270–305	攻撃２	attack２
        // 17	270–305	攻撃２予備動作１	attack２予備動作１
        // 18	270–305	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults JokerEnhanced = new EnemyDefaults {
            Id=48,  TableIndex=159, ModelCode="e161", Name="Joker (Enhanced)",  MaxHp=9500,  Abs=35,  MinGoldDrop=12,  DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=150,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=149, ItemResA=50, ItemResB=10, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{} };

        // e162
        // Motions: e162a.chr @ data.dat 0x200ce800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	60–80	バックステップ	back step
        // 3	120–140	右ステップ	right step
        // 4	90–110	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	240–260	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	270–290	死亡	death
        // 12	290	死亡 ループ	death (loop)
        // 13	150–190	攻撃1	attack1
        // 14	150–190	攻撃1予備動作１	attack1予備動作１
        // 15	150–190	攻撃1予備動作２	attack1予備動作２
        // 16	200–230	攻撃２	attack２
        // 17	200–230	攻撃２予備動作１	attack２予備動作１
        // 18	200–215	ワープ開始	ワープ開始
        // 19	215	ワープ中（要らない？）	ワープ中(要らない？)
        // 20	215–230	ワープ終了	ワープ終了
        internal static readonly EnemyDefaults LichEnhanced = new EnemyDefaults {
            Id=51,  TableIndex=160, ModelCode="e162", Name="Lich (Enhanced)",  MaxHp=10000,  Abs=35,  MinGoldDrop=15,  DropChance=80,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=160,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=176, ItemResA=80, ItemResB=30, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        // e163
        // Motions: e163a.chr @ data.dat 0x2016c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–70	歩き	walk
        // 2	10–20	バックステップ	back step
        // 3	10–20	右ステップ	right step
        // 4	10–20	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–210	死亡	death
        // 12	80–125	攻撃1(パンチ）	attack1(パンチ)
        // 13	80–125	攻撃1予備動作１	attack1予備動作１
        // 14	80–125	攻撃1予備動作２	attack1予備動作２
        // 15	130–160	攻撃２（地面叩く）	attack２(地面叩く)
        // 16	130–160	攻撃２予備動作１	attack２予備動作１
        // 17	130–160	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults TitanEnhanced = new EnemyDefaults {
            Id=33,  TableIndex=161, ModelCode="e163", Name="Titan (Enhanced)",  MaxHp=11500,  Abs=35,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{155,155,160,160,150,145,140}, ProjectileDamage=new int[]{1,160} };

        // e164
        // Motions: e164a.chr @ data.dat 0x20211800  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	10–20	立ち	idle
        // 1	30–50	歩き	walk
        // 2	30–50	バックステップ	back step
        // 3	30–50	右ステップ	right step
        // 4	30–50	左ステップ	left step
        // 5	10–20	ガード入り	guard (enter)
        // 6	10–20	ガードループ	guard (loop)
        // 7	10–20	ガード戻り	guard (return)
        // 8	10–20	ダメージ１	damage１
        // 9	10–20	ダメージ２	damage２
        // 10	10–20	起き上がり	get up
        // 11	180–202	死亡	death
        // 12	202	死亡ループ	death loop
        // 13	60–80	攻撃1 (横）	attack1 (横)
        // 14	60–80	攻撃1予備動作１	attack1予備動作１
        // 15	60–80	攻撃1予備動作２	attack1予備動作２
        // 16	90–120	攻撃2 （ランス突き）	attack2 (ランス突き)
        // 17	90–120	攻撃２予備動作１	attack２予備動作１
        // 18	90–120	攻撃２予備動作２	attack２予備動作２
        internal static readonly EnemyDefaults LivingArmorEnhanced = new EnemyDefaults {
            Id=55,  TableIndex=162, ModelCode="e164", Name="Living Armor (Enhanced)",  MaxHp=9500,  Abs=35,  MinGoldDrop=15,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80, HolyRes=80,
            EntityScale=4.0f, EntityScaleCopy=4.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=null, ItemResA=100, ItemResB=50, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{} };

        // e109
        // Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	40–50	立ち	idle
        // 1	60–70	移動ループ前	move loop (fwd)
        // 2	80–90	移動ループ後	move loop (back)
        // 3	100–110	移動ループ右	move loop右
        // 4	120–130	移動ループ左	move loop左
        // 5	170–175	ガード入り	guard (enter)
        // 6	175–185	ガードループ	guard (loop)
        // 7	185–190	ガード戻り	guard (return)
        // 8	200–207	ダメージ	damage
        // 9	40–50	ダミー	(dummy)
        // 10	40–50	ダミー	(dummy)
        // 11	220–235	死亡	death
        // 12	140–160	攻撃1	attack1
        // 13	140–160	ダミー	(dummy)
        // 14	140–160	ダミー	(dummy)
        // 15	140–160	ダミー	(dummy)
        // 16	140–160	ダミー	(dummy)
        // 17	140–160	ダミー	(dummy)
        // 18	10–28	出現	appear
        // 19	10	まちかまえ	まちstance
        internal static readonly EnemyDefaults MimicDSEnhancedThrice = new EnemyDefaults {
            Id=309, TableIndex=163, ModelCode="e109", Name="Mimic (Demon Shaft) (Enhanced x3)",  MaxHp=7500,  Abs=20,  MinGoldDrop=26,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, DamageReduction=30, WeaponDefense=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // e110
        // Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)
        // Idx	Frames	Name (JP)	Meaning
        // 0	35–45	立ち	idle
        // 1	55–65	移動ループ前	move loop (fwd)
        // 2	75–85	移動ループ後	move loop (back)
        // 3	95–105	移動ループ右	move loop右
        // 4	115–125	移動ループ左	move loop左
        // 5	35–45	ダミー	(dummy)
        // 6	35–45	ダミー	(dummy)
        // 7	35–45	ダミー	(dummy)
        // 8	35–45	ダミー	(dummy)
        // 9	35–45	ダミー	(dummy)
        // 10	35–45	ダミー	(dummy)
        // 11	200–220	死亡	death
        // 12	135–152	攻撃1	attack1
        // 13	135–152	ダミー	(dummy)
        // 14	135–152	ダミー	(dummy)
        // 15	160–174	攻撃2	attack2
        // 16	160–174	ダミー	(dummy)
        // 17	160–174	ダミー	(dummy)
        // 18	180–194	攻撃3	attack3
        // 19	180–194	ダミー	(dummy)
        // 20	180–194	ダミー	(dummy)
        // 21	10–27	出現	appear
        // 22	10	待ち構え	待ちstance
        internal static readonly EnemyDefaults KingMimicDSEnhancedThrice = new EnemyDefaults {
            Id=310, TableIndex=164, ModelCode="e110", Name="King Mimic (Demon Shaft) (Enhanced x3)",  MaxHp=19500,  Abs=30,  MinGoldDrop=35,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, DamageReduction=30, WeaponDefense=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // ── Lookup by species ID ──────────────────────────────────────────────────
        internal static readonly Dictionary<ushort, EnemyDefaults> Defaults;
        static Enemies()
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
