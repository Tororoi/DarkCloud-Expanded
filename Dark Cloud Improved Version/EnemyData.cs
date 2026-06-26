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

        // Record +0x098: per-species KNOCKBACK resistance (float). On a hit, knockback impulse = weaponForce ×
        // KnockbackMult. 1.0 = full knockback (normal enemies), 0.0 = immovable (all bosses + their effects, rooted
        // plants), 0.5-0.8 = heavy/stone enemies. Confirmed in-game. See EnemyAddresses.EnemySpeciesTable.KnockbackMult.
        internal float?  KnockbackMult;

        // Slot +0x0D8: steal item ID — low ushort is the item ID; 65535 if none.
        // Static table: EnemySpeciesTable.StealItemId (0x080) / EnemySpeciesTable.DeathDropFlag (0x082).
        internal ushort? StealItemId;

        // Record +0x084/+0x086 (→ slot +0x0DC low/high) — both are resistances to thrown ITEMS, confirmed in-game.
        // ItemDamageRes = damage-taken multiplier for thrown items (gems/bombs): 100 = neutral, <100 = resistant (Joe 50
        // -> takes half; raising it 50->90 raised a fire-gem hit 14->26). ItemStatusRes = status-effect susceptibility
        // (0 = immune, ~100 = fully susceptible): rolled per weapon/item status bit in CheckDmg to land poison/freeze/
        // stamina/gooey; all bosses = 0, regulars 50-90. See EnemyAddresses.EnemySpeciesTable.ItemDamageRes/ItemStatusRes.
        internal ushort? ItemDamageRes;   // slot +0x0DC low  — thrown-item damage-taken multiplier (×/100, 100=neutral)
        internal ushort? ItemStatusRes;   // slot +0x0DC high — status-effect susceptibility (0=immune)

        // +0x110: CONFIRMED controls horizontal width of the lock-on reticle.
        // +0x114: CONFIRMED lock-on reticle height. May double as hitbox height.
        // Neither appears to control enemy movement speed directly.
        internal float? ReticleWidth;
        internal float? ReticleHeight;

        // Static table +0x088: RARE DROP ITEM ID — the enemy's signature bonus drop.
        // RE'd 2026-06-24 from the ELF: copied to live slot +0xE0 at spawn, then the
        // death-drop block (CMonstorUnit::Step, ELF 0x1DF4C0) force-drops this item with a ~10% roll, de-duped
        // via SetGateKeyStack so a unique item drops only once. Proof: King Mimic=181="Treasure Chest Key",
        // Mimic=235="Dran's Feather". 65535 = none (bosses). See EnemyAddresses.EnemySpeciesTable.RareDropItemId.
        internal ushort? RareDropItemId;

        // Per-attack hit damage decoded from this species' behavior script (dun/monstor/<ModelCode>.stb),
        // mirrored from enemy-attack-damage-table.md. ACTUAL hit damage = baseDamage − playerDefense; baseDamage
        // is these script constants (no static-table field holds hit damage — the old "AttackPower" at +0x88 is
        // actually RareDropItemId; see EnemyAddresses.cs / memory enemy-attack-damage-system). Used by the stat-normalization gradient.
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
        // +0x048: enemy-specific int (474–1454); likely mesh or animation data count. Despite the name this is NOT the
        // runtime model-buffer footprint and does NOT predict it (it actually anti-correlates) — use ModelFootprint
        // below for budget-aware spawning.
        internal int?   ModelDataSize;
        // +0x370: animation clip cap — setting below true count freezes higher-index animations (attacks); Setting above true count does not change default behavior.
        internal int?   ModelAnimCount;

        // Measured runtime model-buffer footprint in bytes (the MonstorModelBuffer space this species' mesh consumes
        // when loaded — CDataAlloc2 @ 0x21F066D0). This is the RUNTIME load size, NOT the static ModelDataSize above
        // (which doesn't predict it). The randomizer sums these per floor and stops before the per-dungeon buffer cap
        // (DungeonData.ModelBufferCapMin) overflows, which would hang the floor load. Measured via MeasureBufferMode;
        // see /enemy-model-buffer-footprints.md. Composite bosses split across two entries (Black Knight rider + mount).
        internal int?   ModelFootprint;
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
            Id=1, TableIndex=0, Name="Master Jacket", ModelCode="e01a", ModelFootprint=78158, ModelAnimCount=20, ModelDataSize=1123,
            Abs=5, MinGoldDrop=7, DropChance=50, StealItemId=177, RareDropItemId=150,
            MaxHp=75, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80, ThunderRes=100, WindRes=80, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=80,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.2f, ReticleHeight=1.3f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{35,30}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SkeletonSoldier = new EnemyDefaults {
            Id=3, TableIndex=1, Name="Skeleton Soldier", ModelCode="e03a", ModelFootprint=75950, ModelAnimCount=19, ModelDataSize=1080,
            Abs=3, MinGoldDrop=4, DropChance=30, StealItemId=null, RareDropItemId=148,
            MaxHp=23, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=90, ThunderRes=100, WindRes=100, HolyRes=160,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ReticleWidth=1.2f, ReticleHeight=1.3f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{20,21}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Statue = new EnemyDefaults {
            Id=5, TableIndex=2, Name="Statue", ModelCode="e05a", ModelFootprint=54950, ModelAnimCount=18, ModelDataSize=792,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=160, RareDropItemId=92,
            MaxHp=38, DamageReduction=3, WeaponDefense=20, KnockbackMult=0.7f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f, ReticleWidth=1.2f, ReticleHeight=1.7f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{26,25,26,25}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Dasher = new EnemyDefaults {
            Id=6, TableIndex=3, Name="Dasher", ModelCode="e06a", ModelFootprint=37036, ModelAnimCount=19, ModelDataSize=1424,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=148, RareDropItemId=199,
            MaxHp=23, DamageReduction=1, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.7f, ReticleHeight=1.7f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{22}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Werewolf = new EnemyDefaults {
            Id=7, TableIndex=4, Name="Werewolf", ModelCode="e07a", ModelFootprint=60549, ModelAnimCount=19, ModelDataSize=1111,
            Abs=12, MinGoldDrop=15, DropChance=50, StealItemId=174, RareDropItemId=93,
            MaxHp=180, DamageReduction=5, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            ItemDamageRes=90, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.5f, ReticleHeight=1.5f, EntityScale=7.5f, EntityScaleCopy=7.5f,
            MeleeDamage=new int[]{68,62}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults FliFli = new EnemyDefaults {
            Id=8, TableIndex=5, Name="FliFli", ModelCode="e08a", ModelFootprint=27582,
            Abs=3, MinGoldDrop=7, DropChance=30, StealItemId=151, RareDropItemId=169,
            MaxHp=120, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=3.0f, BodyHeight=21.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{37}, ProjectileDamage=new int[]{35} };

        internal static readonly EnemyDefaults Hornet = new EnemyDefaults {
            Id=9, TableIndex=6, Name="Hornet", ModelCode="e09a", ModelFootprint=28825, ModelAnimCount=21, ModelDataSize=1060,
            Abs=3, MinGoldDrop=7, DropChance=30, StealItemId=151, RareDropItemId=84,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=120, ThunderRes=100, WindRes=120, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ReticleWidth=1.2f, ReticleHeight=1.2f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{42,40}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Halloween = new EnemyDefaults {
            Id=10, TableIndex=7, Name="Halloween", ModelCode="e10a", ModelFootprint=51996, ModelAnimCount=18, ModelDataSize=850,
            Abs=3, MinGoldDrop=7, DropChance=40, StealItemId=168, RareDropItemId=148,
            MaxHp=150, DamageReduction=3, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.3f, ReticleHeight=1.3f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{57,57}, ProjectileDamage=new int[]{60} };

        internal static readonly EnemyDefaults CannibalPlant = new EnemyDefaults {
            Id=11, TableIndex=8, Name="Cannibal Plant", ModelCode="e11a", ModelFootprint=40494, ModelAnimCount=7, ModelDataSize=474,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=167, RareDropItemId=145,
            MaxHp=60, DamageReduction=2, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=21.0f, BodyDepth=60.0f, ReticleWidth=1.4f, ReticleHeight=1.6f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{28,28,36}, ProjectileDamage=new int[]{30} };

        internal static readonly EnemyDefaults EarthDigger = new EnemyDefaults {
            Id=12, TableIndex=9, Name="Earth Digger", ModelCode="e12a", ModelFootprint=43573, ModelAnimCount=19, ModelDataSize=936,
            Abs=3, MinGoldDrop=7, DropChance=30, StealItemId=188, RareDropItemId=197,
            MaxHp=120, DamageReduction=2, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=80, WindRes=80, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.3f, ReticleHeight=1.3f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{52}, ProjectileDamage=new int[]{37} };

        internal static readonly EnemyDefaults Sunday = new EnemyDefaults {
            Id=14, TableIndex=10, Name="Sunday", ModelCode="e14a", ModelFootprint=42766, ModelAnimCount=19, ModelDataSize=1454,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=170, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{36,26}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Monday = new EnemyDefaults {
            Id=15, TableIndex=11, Name="Monday", ModelCode="e15a", ModelFootprint=42314, ModelAnimCount=19, ModelDataSize=1424,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=146, RareDropItemId=155,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{32,12}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Tuesday = new EnemyDefaults {
            Id=16, TableIndex=12, Name="Tuesday", ModelCode="e16a", ModelFootprint=43983, ModelAnimCount=19, ModelDataSize=1427,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=151, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{31}, ProjectileDamage=new int[]{31} };

        internal static readonly EnemyDefaults Wednesday = new EnemyDefaults {
            Id=17, TableIndex=13, Name="Wednesday", ModelCode="e17a", ModelFootprint=42198, ModelAnimCount=19, ModelDataSize=1438,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=146, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{30,28}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Thursday = new EnemyDefaults {
            Id=18, TableIndex=14, Name="Thursday", ModelCode="e18a", ModelFootprint=45935,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=151, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{29}, ProjectileDamage=new int[]{30} };

        internal static readonly EnemyDefaults Friday = new EnemyDefaults {
            Id=19, TableIndex=15, Name="Friday", ModelCode="e19a", ModelFootprint=41825, ModelAnimCount=19, ModelDataSize=1438,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=148, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{29,29}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Saturday = new EnemyDefaults {
            Id=20, TableIndex=16, Name="Saturday", ModelCode="e20a", ModelFootprint=36941,
            Abs=3, MinGoldDrop=6, DropChance=40, StealItemId=148, RareDropItemId=145,
            MaxHp=60, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{29,29,25}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WitchHellza = new EnemyDefaults {
            Id=21, TableIndex=17, Name="Witch Hellza", ModelCode="e21a", ModelFootprint=60435,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=169, RareDropItemId=94,
            MaxHp=270, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=70, IceRes=70, ThunderRes=70, WindRes=70, HolyRes=100,
            ItemDamageRes=85, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{73}, ProjectileDamage=new int[]{73} };

        internal static readonly EnemyDefaults WitchIllza = new EnemyDefaults {
            Id=22, TableIndex=18, Name="Witch Illza", ModelCode="e22a", ModelFootprint=44499, ModelAnimCount=18, ModelDataSize=868,
            Abs=3, MinGoldDrop=4, DropChance=30, StealItemId=169, RareDropItemId=94,
            MaxHp=120, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=90, IceRes=90, ThunderRes=90, WindRes=90, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.3f, ReticleHeight=1.4f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{47}, ProjectileDamage=new int[]{47} };

        internal static readonly EnemyDefaults Gunny = new EnemyDefaults {
            Id=23, TableIndex=19, Name="Gunny", ModelCode="e23a", ModelFootprint=35380, ModelAnimCount=19, ModelDataSize=1270,
            Abs=4, MinGoldDrop=8, DropChance=30, StealItemId=153, RareDropItemId=193,
            MaxHp=250, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=70,
            BodyWidth=14.0f, BodyHeight=20.0f, BodyDepth=0.0f, ReticleWidth=1.5f, ReticleHeight=1.5f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{44,44}, ProjectileDamage=new int[]{26} };

        internal static readonly EnemyDefaults Gyon = new EnemyDefaults {
            Id=24, TableIndex=20, Name="Gyon", ModelCode="e24a", ModelFootprint=53038, ModelAnimCount=16, ModelDataSize=849,
            Abs=4, MinGoldDrop=8, DropChance=30, StealItemId=134, RareDropItemId=226,
            MaxHp=225, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, ReticleWidth=1.4f, ReticleHeight=1.6f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{59} };

        internal static readonly EnemyDefaults PiratesChariot = new EnemyDefaults {
            Id=25, TableIndex=21, Name="Pirate's Chariot", ModelCode="e25a", ModelFootprint=28414, ModelAnimCount=19, ModelDataSize=835,
            Abs=8, MinGoldDrop=15, DropChance=30, StealItemId=159, RareDropItemId=92,
            MaxHp=270, DamageReduction=5, WeaponDefense=30, KnockbackMult=0.5f,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80, ThunderRes=140, WindRes=100, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, ReticleWidth=1.9f, ReticleHeight=1.8f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{69} };

        // (The 127.5/80.0/15.0 once read on Auntie Medu at slot 0x150/154/158 was just an active status-tint color —
        // see EnemyAddresses.EnemySlotOffsets.StatusTintR/G/B; 0 = no status.)
        internal static readonly EnemyDefaults AuntieMedu = new EnemyDefaults {
            Id=26, TableIndex=22, Name="Auntie Medu", ModelCode="e26a", ModelFootprint=56893, ModelAnimCount=19, ModelDataSize=944,
            Abs=10, MinGoldDrop=15, DropChance=30, StealItemId=166, RareDropItemId=245,
            MaxHp=300, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.4f, ReticleHeight=1.4f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{60} };

        internal static readonly EnemyDefaults Captain = new EnemyDefaults {
            Id=27, TableIndex=23, Name="Captain", ModelCode="e27a", ModelFootprint=50447, ModelAnimCount=19, ModelDataSize=873,
            Abs=6, MinGoldDrop=12, DropChance=30, StealItemId=177, RareDropItemId=227,
            MaxHp=225, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80, WindRes=80, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{74,74,72,72}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Corcea = new EnemyDefaults {
            Id=28, TableIndex=24, Name="Corcea", ModelCode="e28a", ModelFootprint=51363, ModelAnimCount=19, ModelDataSize=871,
            Abs=4, MinGoldDrop=8, DropChance=30, StealItemId=152, RareDropItemId=91,
            MaxHp=150, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=140, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ReticleWidth=1.4f, ReticleHeight=1.4f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{43,43,42,42}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Golem = new EnemyDefaults {
            Id=30, TableIndex=25, Name="Golem", ModelCode="e30a", ModelFootprint=55300, ModelAnimCount=18, ModelDataSize=1071,
            Abs=4, MinGoldDrop=15, DropChance=30, StealItemId=177, RareDropItemId=92,
            MaxHp=375, DamageReduction=8, WeaponDefense=0, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f, ReticleWidth=2.4f, ReticleHeight=2.4f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{64,34} };

        internal static readonly EnemyDefaults MrBlare = new EnemyDefaults {
            Id=31, TableIndex=26, Name="Mr. Blare", ModelCode="e31a", ModelFootprint=53519,
            Abs=5, MinGoldDrop=15, DropChance=30, StealItemId=161, RareDropItemId=81,
            MaxHp=225, DamageReduction=0, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=0, IceRes=170, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{81,81}, ProjectileDamage=new int[]{80,90} };

        internal static readonly EnemyDefaults Dune = new EnemyDefaults {
            Id=32, TableIndex=27, Name="Dune", ModelCode="e32a", ModelFootprint=43481,
            Abs=10, MinGoldDrop=18, DropChance=30, StealItemId=null, RareDropItemId=160,
            MaxHp=525, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=80, WindRes=120, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=11.0f, EntityScaleCopy=11.0f,
            MeleeDamage=new int[]{85,85,85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Titan = new EnemyDefaults {
            Id=33, TableIndex=28, Name="Titan", ModelCode="e33a", ModelFootprint=54336,
            Abs=12, MinGoldDrop=15, DropChance=30, StealItemId=177, RareDropItemId=160,
            MaxHp=750, DamageReduction=10, WeaponDefense=50, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{105,105,105,105,90,75,60}, ProjectileDamage=new int[]{90,90} };

        internal static readonly EnemyDefaults KingMimicDBC = new EnemyDefaults {
            Id=34, TableIndex=29, Name="King Mimic (Divine Beast Cave)", ModelCode="e34a", ModelFootprint=38794, ModelAnimCount=23, ModelDataSize=1012,
            Abs=4, MinGoldDrop=20, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=90, DamageReduction=3, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, ReticleWidth=1.9f, ReticleHeight=1.65f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{35,30,35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDBC = new EnemyDefaults {
            Id=35, TableIndex=30, Name="Mimic (Divine Beast Cave)", ModelCode="e35a", ModelFootprint=24823, ModelAnimCount=20, ModelDataSize=920,
            Abs=3, MinGoldDrop=10, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=68, DamageReduction=1, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.1f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{33,33}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicSMT = new EnemyDefaults {
            Id=36, TableIndex=31, Name="King Mimic (Sun & Moon Temple)", ModelCode="e36a", ModelFootprint=38858,
            Abs=15, MinGoldDrop=20, DropChance=80, StealItemId=174, RareDropItemId=181,
            MaxHp=525, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{101,102,45}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicSMT = new EnemyDefaults {
            Id=37, TableIndex=32, Name="Mimic (Sun & Moon Temple)", ModelCode="e37a", ModelFootprint=24831, ModelAnimCount=20, ModelDataSize=918,
            Abs=6, MinGoldDrop=12, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=270, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.1f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{71,71}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicMS = new EnemyDefaults {
            Id=38, TableIndex=33, Name="King Mimic (Moon Sea)", ModelCode="e38a", ModelFootprint=39290,
            Abs=12, MinGoldDrop=20, DropChance=80, StealItemId=176, RareDropItemId=181,
            MaxHp=600, DamageReduction=8, WeaponDefense=30, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{118,96,90}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicMS = new EnemyDefaults {
            Id=39, TableIndex=34, Name="Mimic (Moon Sea)", ModelCode="e39a", ModelFootprint=25223,
            Abs=6, MinGoldDrop=15, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=450, DamageReduction=8, WeaponDefense=30, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{83,83}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Arthur = new EnemyDefaults {
            Id=40, TableIndex=35, Name="Arthur", ModelCode="e40a", ModelFootprint=45627,
            Abs=15, MinGoldDrop=15, DropChance=30, StealItemId=177, RareDropItemId=92,
            MaxHp=600, DamageReduction=10, WeaponDefense=60, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=80, IceRes=100, ThunderRes=150, WindRes=80, HolyRes=80,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=30.0f, BodyDepth=60.0f, EntityScale=9.0f, EntityScaleCopy=9.0f,
            MeleeDamage=new int[]{116,116}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Ghost = new EnemyDefaults {
            Id=42, TableIndex=36, Name="Ghost", ModelCode="e42a", ModelFootprint=49096, ModelAnimCount=21, ModelDataSize=1220,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=135, RareDropItemId=133,
            MaxHp=15, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.1f, ReticleHeight=1.1f, EntityScale=3.6f, EntityScaleCopy=3.6f,
            MeleeDamage=new int[]{20,20}, ProjectileDamage=new int[]{21} };

        internal static readonly EnemyDefaults Alexander = new EnemyDefaults {
            Id=43, TableIndex=37, Name="Alexander", ModelCode="e43a", ModelFootprint=39287,
            Abs=15, MinGoldDrop=17, DropChance=50, StealItemId=164, RareDropItemId=81,
            MaxHp=675, DamageReduction=10, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=150, IceRes=130, ThunderRes=100, WindRes=120, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=32.0f, BodyHeight=30.0f, BodyDepth=0.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{120}, ProjectileDamage=new int[]{124} };

        internal static readonly EnemyDefaults Heart = new EnemyDefaults {
            Id=44, TableIndex=38, Name="Heart", ModelCode="e44a", ModelFootprint=58698,
            Abs=6, MinGoldDrop=12, DropChance=50, StealItemId=150, RareDropItemId=133,
            MaxHp=525, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{107}, ProjectileDamage=new int[]{107} };

        internal static readonly EnemyDefaults Club = new EnemyDefaults {
            Id=45, TableIndex=39, Name="Club", ModelCode="e45a", ModelFootprint=49516,
            Abs=6, MinGoldDrop=12, DropChance=50, StealItemId=147, RareDropItemId=134,
            MaxHp=525, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{104}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Diamond = new EnemyDefaults {
            Id=46, TableIndex=40, Name="Diamond", ModelCode="e46a", ModelFootprint=48133,
            Abs=6, MinGoldDrop=12, DropChance=50, StealItemId=151, RareDropItemId=135,
            MaxHp=525, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50, WindRes=150, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Spade = new EnemyDefaults {
            Id=47, TableIndex=41, Name="Spade", ModelCode="e47a", ModelFootprint=48269,
            Abs=6, MinGoldDrop=12, DropChance=50, StealItemId=152, RareDropItemId=132,
            MaxHp=525, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{113,113}, ProjectileDamage=new int[]{} };

        // fire=50/ice=50/thu=50/win=50 (resistant to all), holy=150; all-element-resistant mage
        internal static readonly EnemyDefaults Joker = new EnemyDefaults {
            Id=48, TableIndex=42, Name="Joker", ModelCode="e48a", ModelFootprint=49660,
            Abs=6, MinGoldDrop=12, DropChance=50, StealItemId=149, RareDropItemId=154,
            MaxHp=600, DamageReduction=3, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=150,
            ItemDamageRes=50, ItemStatusRes=10,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{115}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults BomberHead = new EnemyDefaults {
            Id=49, TableIndex=43, Name="Bomber Head", ModelCode="e49a", ModelFootprint=62404, ModelAnimCount=23, ModelDataSize=1663,
            Abs=4, MinGoldDrop=10, DropChance=30, StealItemId=159, RareDropItemId=159,
            MaxHp=180, DamageReduction=8, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=75, ThunderRes=125, WindRes=100, HolyRes=75,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, ReticleWidth=1.3f, ReticleHeight=1.4f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{61,61}, ProjectileDamage=new int[]{64} };

        internal static readonly EnemyDefaults Mummy = new EnemyDefaults {
            Id=50, TableIndex=44, Name="Mummy", ModelCode="e50a", ModelFootprint=68723, ModelAnimCount=19, ModelDataSize=1029,
            Abs=4, MinGoldDrop=10, DropChance=30, StealItemId=null, RareDropItemId=133,
            MaxHp=150, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=9.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.3f, ReticleHeight=1.4f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{54,54,54}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Lich = new EnemyDefaults {
            Id=51, TableIndex=45, Name="Lich", ModelCode="e51a", ModelFootprint=54789,
            Abs=12, MinGoldDrop=15, DropChance=80, StealItemId=176, RareDropItemId=94,
            MaxHp=300, DamageReduction=5, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=160,
            ItemDamageRes=80, ItemStatusRes=30,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{114,114}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults CurseDancer = new EnemyDefaults {
            Id=52, TableIndex=46, Name="Curse Dancer", ModelCode="e52a", ModelFootprint=37022,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=166, RareDropItemId=133,
            MaxHp=300, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=160,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{90,90,90,90}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults LivingArmor = new EnemyDefaults {
            Id=55, TableIndex=47, Name="Living Armor", ModelCode="e55a", ModelFootprint=55777,
            Abs=6, MinGoldDrop=15, DropChance=30, StealItemId=null, RareDropItemId=160,
            MaxHp=450, DamageReduction=10, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80, HolyRes=80,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{95,95,95,95}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WhiteFang = new EnemyDefaults {
            Id=56, TableIndex=48, Name="White Fang", ModelCode="e56a", ModelFootprint=59337,
            Abs=10, MinGoldDrop=12, DropChance=30, StealItemId=null, RareDropItemId=155,
            MaxHp=525, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.5f, EntityScaleCopy=7.5f,
            MeleeDamage=new int[]{90,84}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MoonBug = new EnemyDefaults {
            Id=57, TableIndex=49, Name="Moon Bug", ModelCode="e57a", ModelFootprint=38394,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=159, RareDropItemId=92,
            MaxHp=450, DamageReduction=8, WeaponDefense=40, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=50, IceRes=120, ThunderRes=150, WindRes=50, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{70,70}, ProjectileDamage=new int[]{70} };

        internal static readonly EnemyDefaults Phantom = new EnemyDefaults {
            Id=58, TableIndex=50, Name="Phantom", ModelCode="e58a", ModelFootprint=28865, ModelAnimCount=21, ModelDataSize=1060,
            Abs=4, MinGoldDrop=8, DropChance=30, StealItemId=151, RareDropItemId=84,
            MaxHp=150, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=125, ThunderRes=100, WindRes=125, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ReticleWidth=1.2f, ReticleHeight=1.2f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{60,54}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Dragon = new EnemyDefaults {
            Id=59, TableIndex=51, Name="Dragon", ModelCode="e59a", ModelFootprint=73627, ModelAnimCount=19, ModelDataSize=1422,
            Abs=5, MinGoldDrop=15, DropChance=50, StealItemId=161, RareDropItemId=85,
            MaxHp=90, DamageReduction=5, WeaponDefense=40, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=50, IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, ReticleWidth=2.9f, ReticleHeight=2.7f, EntityScale=17.5f, EntityScaleCopy=17.5f,
            MeleeDamage=new int[]{45,45}, ProjectileDamage=new int[]{50} };

        internal static readonly EnemyDefaults CaveBat = new EnemyDefaults {
            Id=60, TableIndex=52, Name="Cave Bat", ModelCode="e60a", ModelFootprint=18760, ModelAnimCount=21, ModelDataSize=940,
            Abs=3, MinGoldDrop=4, DropChance=30, StealItemId=151, RareDropItemId=199,
            MaxHp=12, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, ReticleWidth=0.8f, ReticleHeight=0.8f, EntityScale=3.0f, EntityScaleCopy=3.0f,
            MeleeDamage=new int[]{17,16}, ProjectileDamage=new int[]{} };

        // shares scale=3.0 with CaveBat
        internal static readonly EnemyDefaults EvilBat = new EnemyDefaults {
            Id=61, TableIndex=53, Name="Evil Bat", ModelCode="e61a", ModelFootprint=18637,
            Abs=4, MinGoldDrop=5, DropChance=30, StealItemId=151, RareDropItemId=149,
            MaxHp=150, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, EntityScale=3.0f, EntityScaleCopy=3.0f,
            MeleeDamage=new int[]{86,85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults HellPockle = new EnemyDefaults {
            Id=62, TableIndex=54, Name="Hell Pockle", ModelCode="e62a", ModelFootprint=42954,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=null, RareDropItemId=148,
            MaxHp=270, DamageReduction=2, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=6.0f, BodyHeight=17.0f, BodyDepth=0.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{64,64}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RashDasher = new EnemyDefaults {
            Id=63, TableIndex=55, Name="Rash Dasher", ModelCode="e63a", ModelFootprint=42868,
            Abs=6, MinGoldDrop=12, DropChance=30, StealItemId=149, RareDropItemId=93,
            MaxHp=600, DamageReduction=2, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=50, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SteelGiant = new EnemyDefaults {
            Id=64, TableIndex=56, Name="Steel Giant", ModelCode="e64a", ModelFootprint=54780,
            Abs=12, MinGoldDrop=15, DropChance=50, StealItemId=177, RareDropItemId=154,
            MaxHp=750, DamageReduction=10, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=80, IceRes=100, ThunderRes=125, WindRes=80, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{93,93,98,98,80,70,60}, ProjectileDamage=new int[]{64,64} };

        internal static readonly EnemyDefaults Blizzard = new EnemyDefaults {
            Id=65, TableIndex=57, Name="Blizzard", ModelCode="e65a", ModelFootprint=51196,
            Abs=8, MinGoldDrop=5, DropChance=30, StealItemId=162, RareDropItemId=82,
            MaxHp=750, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.5f,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=140, WindRes=140, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{119,119,119,119,105,90,75}, ProjectileDamage=new int[]{105,105} };

        internal static readonly EnemyDefaults MoonDigger = new EnemyDefaults {
            Id=66, TableIndex=58, Name="Moon Digger", ModelCode="e66a", ModelFootprint=36157,
            Abs=6, MinGoldDrop=10, DropChance=30, StealItemId=187, RareDropItemId=197,
            MaxHp=420, DamageReduction=2, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=125, ThunderRes=80, WindRes=80, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{83}, ProjectileDamage=new int[]{72} };

        internal static readonly EnemyDefaults DarkFlower = new EnemyDefaults {
            Id=67, TableIndex=59, Name="Dark Flower", ModelCode="e67a", ModelFootprint=37762,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=null, RareDropItemId=147,
            MaxHp=300, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{90,90,94}, ProjectileDamage=new int[]{85} };

        internal static readonly EnemyDefaults CursedRose = new EnemyDefaults {
            Id=68, TableIndex=60, Name="Cursed Rose", ModelCode="e68a", ModelFootprint=33850, ModelAnimCount=7, ModelDataSize=476,
            Abs=4, MinGoldDrop=6, DropChance=30, StealItemId=null, RareDropItemId=146,
            MaxHp=225, DamageReduction=2, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f, ReticleWidth=1.4f, ReticleHeight=1.6f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{49,49,48}, ProjectileDamage=new int[]{46} };

        // thunder=0 (immune)
        internal static readonly EnemyDefaults Billy = new EnemyDefaults {
            Id=69, TableIndex=61, Name="Billy", ModelCode="e69a", ModelFootprint=50139,
            Abs=6, MinGoldDrop=10, DropChance=30, StealItemId=163, RareDropItemId=83,
            MaxHp=300, DamageReduction=5, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=0, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{93,93}, ProjectileDamage=new int[]{93,110} };

        // fire=65486 (0xFFCE — effectively absorbs fire damage)
        internal static readonly EnemyDefaults Vulcan = new EnemyDefaults {
            Id=70, TableIndex=62, Name="Vulcan", ModelCode="e70a", ModelFootprint=45198,
            Abs=12, MinGoldDrop=4, DropChance=30, StealItemId=81, RareDropItemId=160,
            MaxHp=480, DamageReduction=5, WeaponDefense=40, KnockbackMult=1.0f,
            Category=EnemyCategory.Rock, FireRes=65486, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{88,94,94}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CrabbyHermit = new EnemyDefaults {
            Id=71, TableIndex=63, Name="Crabby Hermit", ModelCode="e71a", ModelFootprint=32496, ModelAnimCount=22, ModelDataSize=1612,
            Abs=4, MinGoldDrop=12, DropChance=30, StealItemId=166, RareDropItemId=92,
            MaxHp=300, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=70,
            BodyWidth=14.0f, BodyHeight=22.0f, BodyDepth=100.0f, ReticleWidth=1.9f, ReticleHeight=1.9f, EntityScale=10.0f, EntityScaleCopy=10.0f,
            MeleeDamage=new int[]{83,83,80}, ProjectileDamage=new int[]{76} };

        internal static readonly EnemyDefaults SpaceGyon = new EnemyDefaults {
            Id=72, TableIndex=64, Name="Space Gyon", ModelCode="e72a", ModelFootprint=54258,
            Abs=5, MinGoldDrop=4, DropChance=30, StealItemId=153, RareDropItemId=226,
            MaxHp=525, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=75, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{78,78}, ProjectileDamage=new int[]{75} };

        internal static readonly EnemyDefaults BlueDragon = new EnemyDefaults {
            Id=73, TableIndex=65, Name="Blue Dragon", ModelCode="e73a", ModelFootprint=86880,
            Abs=12, MinGoldDrop=18, DropChance=50, StealItemId=162, RareDropItemId=91,
            MaxHp=600, DamageReduction=5, WeaponDefense=30, KnockbackMult=0.5f,
            Category=EnemyCategory.Dragon, FireRes=125, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=17.5f, EntityScaleCopy=17.5f,
            MeleeDamage=new int[]{90,90}, ProjectileDamage=new int[]{90} };

        internal static readonly EnemyDefaults BlackDragon = new EnemyDefaults {
            Id=74, TableIndex=66, Name="Black Dragon", ModelCode="e74a", ModelFootprint=74987,
            Abs=20, MinGoldDrop=22, DropChance=50, StealItemId=154, RareDropItemId=94,
            MaxHp=900, DamageReduction=10, WeaponDefense=60, KnockbackMult=0.5f,
            Category=EnemyCategory.Dragon, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=130,
            ItemDamageRes=50, ItemStatusRes=40,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=17.5f, EntityScaleCopy=17.5f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{135} };

        internal static readonly EnemyDefaults MaskOfPrajna = new EnemyDefaults {
            Id=75, TableIndex=67, Name="Mask of Prajna", ModelCode="e75a", ModelFootprint=36718, ModelAnimCount=19, ModelDataSize=1398,
            Abs=12, MinGoldDrop=15, DropChance=50, StealItemId=151, RareDropItemId=94,
            MaxHp=375, DamageReduction=5, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            ItemDamageRes=80, ItemStatusRes=70,
            BodyWidth=32.0f, BodyHeight=26.0f, BodyDepth=0.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{80,78}, ProjectileDamage=new int[]{65} };

        internal static readonly EnemyDefaults CrescentBaron = new EnemyDefaults {
            Id=76, TableIndex=68, Name="Crescent Baron", ModelCode="e76a", ModelFootprint=40148,
            Abs=12, MinGoldDrop=18, DropChance=50, StealItemId=null, RareDropItemId=170,
            MaxHp=450, DamageReduction=5, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{98,98,94,94}, ProjectileDamage=new int[]{70} };

        internal static readonly EnemyDefaults Rockanoff = new EnemyDefaults {
            Id=77, TableIndex=69, Name="Rockanoff", ModelCode="e77a", ModelFootprint=16333, ModelAnimCount=19, ModelDataSize=954,
            Abs=3, MinGoldDrop=10, DropChance=30, StealItemId=160, RareDropItemId=160,
            MaxHp=30, DamageReduction=5, WeaponDefense=20, KnockbackMult=0.8f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.7f, ReticleHeight=1.7f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicWOF = new EnemyDefaults {
            Id=78, TableIndex=70, Name="King Mimic (Wise Owl Forest)", ModelCode="e78a", ModelFootprint=38642,
            Abs=10, MinGoldDrop=15, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=150, DamageReduction=5, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{67,56,45,50}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicWOF = new EnemyDefaults {
            Id=79, TableIndex=71, Name="Mimic (Wise Owl Forest)", ModelCode="e79a", ModelFootprint=24811, ModelAnimCount=20, ModelDataSize=914,
            Abs=3, MinGoldDrop=6, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=90, DamageReduction=2, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.1f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{47,47}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicSW = new EnemyDefaults {
            Id=80, TableIndex=72, Name="King Mimic (Shipwreck)", ModelCode="e80a", ModelFootprint=38770, ModelAnimCount=23, ModelDataSize=1012,
            Abs=15, MinGoldDrop=15, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=300, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, ReticleWidth=1.9f, ReticleHeight=1.65f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{84,78,56}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicSW = new EnemyDefaults {
            Id=81, TableIndex=73, Name="Mimic (Shipwreck)", ModelCode="e81a", ModelFootprint=24795, ModelAnimCount=20, ModelDataSize=918,
            Abs=4, MinGoldDrop=6, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=150, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, ReticleWidth=1.1f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{59,59}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicGoT = new EnemyDefaults {
            Id=82, TableIndex=74, Name="King Mimic (Gallery of Time)", ModelCode="e82a", ModelFootprint=38810,
            Abs=18, MinGoldDrop=25, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=675, DamageReduction=5, WeaponDefense=30, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{134,120,98}, ProjectileDamage=new int[]{} };

        // code=kori (Japanese for "ice" — may be official name)
        internal static readonly EnemyDefaults MimicGoT = new EnemyDefaults {
            Id=83, TableIndex=75, Name="Mimic (Gallery of Time)", ModelCode="e83a", ModelFootprint=24831,
            Abs=6, MinGoldDrop=20, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=450, DamageReduction=5, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{} };

        // SW boss — projectile/summon entity of Ice Queen; not a standalone fight.
        internal static readonly EnemyDefaults IceArrow = new EnemyDefaults {
            Id=84, TableIndex=76, Name="Ice Arrow", ModelCode="korinoya", ModelFootprint=7681,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=2.0f, EntityScaleCopy=2.0f,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Sam = new EnemyDefaults {
            Id=85, TableIndex=77, Name="Sam", ModelCode="e86a", ModelFootprint=58888, ModelAnimCount=19, ModelDataSize=871,
            Abs=4, MinGoldDrop=8, DropChance=30, StealItemId=162, RareDropItemId=82,
            MaxHp=180, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=19.0f, BodyDepth=0.0f, ReticleWidth=1.2f, ReticleHeight=1.6f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{64,64,80}, ProjectileDamage=new int[]{58,58} };

        // All bosses have MinGoldDrop=0, DropChance=0, StealItemId=65535 (can't steal).
        internal static readonly EnemyDefaults Dran = new EnemyDefaults {
            Id=112, TableIndex=78, Name="Dran", ModelCode="c12a", ModelFootprint=134603,
            Abs=10, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=250, DamageReduction=10, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=50,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=60.0f, BodyHeight=60.0f, BodyDepth=60.0f, EntityScale=45.0f, EntityScaleCopy=45.0f,
            MeleeDamage=new int[]{45,45,45,25,25,25,25,25,25,25,25,25,25}, ProjectileDamage=new int[]{17} };

        internal static readonly EnemyDefaults MasterUtan = new EnemyDefaults {
            Id=114, TableIndex=79, Name="Master Utan", ModelCode="c14a", ModelFootprint=121379,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=700, DamageReduction=12, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=42.0f, BodyDepth=60.0f, EntityScale=35.0f, EntityScaleCopy=35.0f,
            MeleeDamage=new int[]{62,47}, ProjectileDamage=new int[]{13} };

        // SW boss — ice=65486 (0xFFCE, -50 as int16) = fire-absorbing (same encoding as Vulcan's fire)
        internal static readonly EnemyDefaults IceQueen = new EnemyDefaults {
            Id=113, TableIndex=80, Name="Ice Queen", ModelCode="c13a", ModelFootprint=187,
            Abs=30, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=700, DamageReduction=10, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=65486, ThunderRes=80, WindRes=80, HolyRes=120,
            ItemDamageRes=40, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=24.0f, BodyDepth=60.0f, EntityScale=13.0f, EntityScaleCopy=13.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingsCurseCoffin = new EnemyDefaults {
            Id=115, TableIndex=81, Name="King's Curse Coffin", ModelCode="c15a", ModelFootprint=17117,
            Abs=40, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=2000, DamageReduction=10, WeaponDefense=40, KnockbackMult=0.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=125,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=52.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // Unlisted phase entity — code=c15b; not in EnemySpecies.cs; suspected SMT King's-Curse scripted phase.
        internal static readonly EnemyDefaults KingsCurse = new EnemyDefaults {
            Id=100, TableIndex=82, Name="King's Curse", ModelCode="c15b", ModelFootprint=24187,
            Abs=40, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=1000, DamageReduction=10, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=52.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{91,91,71}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MinotaurJoe = new EnemyDefaults {
            Id=116, TableIndex=83, Name="Minotaur Joe", ModelCode="c16a", ModelFootprint=91889,
            Abs=50, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=2000, DamageReduction=12, WeaponDefense=40, KnockbackMult=0.0f,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=45.0f, BodyDepth=60.0f, EntityScale=25.0f, EntityScaleCopy=25.0f,
            MeleeDamage=new int[]{100,125,100,100,100}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DarkGenie = new EnemyDefaults {
            Id=117, TableIndex=84, Name="Dark Genie", ModelCode="c17a", ModelFootprint=114919,
            Abs=60, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=2000, DamageReduction=25, WeaponDefense=30, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=30, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=62.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{85}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DarkGenieForm2 = new EnemyDefaults {
            Id=118, TableIndex=85, Name="Dark Genie (form 2)", ModelCode="c17b", ModelFootprint=41201,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=3200, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=57.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RightHand = new EnemyDefaults {
            Id=119, TableIndex=86, Name="Right Hand", ModelCode="c17c", ModelFootprint=41249,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=3200, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=57.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };

        // Left Hand has no EID=120 record in the table; its HP (90) is stored in Right Hand's u98
        // via the off-by-one. The game spawns it using an anonymous EID=0 record at idx=87.
        internal static readonly EnemyDefaults LeftHand = new EnemyDefaults {
            Id=120, TableIndex=87, Name="Left Hand", ModelCode="c17_", ModelFootprint=8621,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{125}, ProjectileDamage=new int[]{} };  // idx=87 is the anonymous EID=0 record

        // These are ATTACK/EFFECT entities (projectiles, beams, barriers, summons), not standalone enemies —
        // ModelCode is a FAMILY PREFIX, not an exact filename, and they have no "死亡": they vanish via
        // 消滅/爆発 (despawn/explode). For these the boss-death system doesn't apply (scripted despawn).
        // Dark Genie fight companions: code=c17_ = DG attack-effect family. The prefix maps to several .chr:
        // c17_beem.chr (発射/ループ/消滅 = launch/loop/despawn), c17_kaze.chr (wind), c17_hikari.chr (light),
        // c17_syougeki.chr (shock). e.g. c17_beem @ data.dat 0x1b1e9000. Which TableIndex→which is unconfirmed.
        internal static readonly EnemyDefaults DGComp88 = new EnemyDefaults {
            Id=0, TableIndex=88, Name="(DG companion c17_)", ModelCode="c17_", ModelFootprint=16563,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp89 = new EnemyDefaults {
            Id=0, TableIndex=89, Name="(DG companion c17_)", ModelCode="c17_", ModelFootprint=21882,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp90 = new EnemyDefaults {
            Id=0, TableIndex=90, Name="(DG companion c17_)", ModelCode="c17_", ModelFootprint=7475,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WineKeg = new EnemyDefaults {
            Id=121, TableIndex=91, Name="Wine Keg", ModelCode="e85a", ModelFootprint=3172,
            Abs=0, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=80, DamageReduction=0, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{8}, ProjectileDamage=new int[]{} };

        // code=b3_r → b3_reiki.chr (霊気 "aura/spirit") @ data.dat 0x1a8c1800 — 1 motion: 0 reiki (aura).
        internal static readonly EnemyDefaults SWComp92 = new EnemyDefaults {
            Id=0, TableIndex=92, Name="Ice Aura", ModelCode="b3_r", ModelFootprint=9393,
            Abs=0, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=80, DamageReduction=0, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DGComp93 = new EnemyDefaults {
            Id=0, TableIndex=93, Name="(DG companion c17_)", ModelCode="c17_", ModelFootprint=9076,
            Abs=0, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=80, DamageReduction=0, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // listed as non-drop in EnemySpecies.cs
        internal static readonly EnemyDefaults Gol = new EnemyDefaults {
            Id=90, TableIndex=94, Name="Gol", ModelCode="e90a", ModelFootprint=55308,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=177, RareDropItemId=65535,
            MaxHp=600, DamageReduction=8, WeaponDefense=0, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{62} };

        // listed as non-drop in EnemySpecies.cs
        internal static readonly EnemyDefaults Sil = new EnemyDefaults {
            Id=91, TableIndex=95, Name="Sil", ModelCode="e91a", ModelFootprint=55308,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=177, RareDropItemId=65535,
            MaxHp=500, DamageReduction=10, WeaponDefense=0, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=90, IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{71,71,75,75,62,55,45}, ProjectileDamage=new int[]{62} };

        internal static readonly EnemyDefaults Yammich = new EnemyDefaults {
            Id=301, TableIndex=96, Name="Yammich", ModelCode="e101", ModelFootprint=26595,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=160, RareDropItemId=92,
            MaxHp=13, DamageReduction=0, WeaponDefense=1, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=70, HolyRes=130,
            ItemDamageRes=90, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults StatueDog = new EnemyDefaults {
            Id=303, TableIndex=97, Name="Statue Dog", ModelCode="e103", ModelFootprint=30135, ModelAnimCount=12, ModelDataSize=667,
            Abs=2, MinGoldDrop=5, DropChance=30, StealItemId=160, RareDropItemId=92,
            MaxHp=15, DamageReduction=3, WeaponDefense=10, KnockbackMult=0.6f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            ItemDamageRes=90, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.5f, ReticleHeight=1.5f, EntityScale=9.0f, EntityScaleCopy=9.0f,
            MeleeDamage=new int[]{35}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Opar = new EnemyDefaults {
            Id=304, TableIndex=98, Name="Opar", ModelCode="e104", ModelFootprint=41629,
            Abs=3, MinGoldDrop=5, DropChance=30, StealItemId=227, RareDropItemId=190,
            MaxHp=28, DamageReduction=1, WeaponDefense=2, KnockbackMult=0.0f,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=60, ThunderRes=130, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=100,
            BodyWidth=50.0f, BodyHeight=56.0f, BodyDepth=60.0f, EntityScale=15.0f, EntityScaleCopy=15.0f,
            MeleeDamage=new int[]{35,35}, ProjectileDamage=new int[]{35,35} };

        internal static readonly EnemyDefaults HaleyHoley = new EnemyDefaults {
            Id=305, TableIndex=99, Name="Haley Holey", ModelCode="e105", ModelFootprint=59418, ModelAnimCount=19, ModelDataSize=1046,
            Abs=3, MinGoldDrop=7, DropChance=40, StealItemId=186, RareDropItemId=189,
            MaxHp=50, DamageReduction=3, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Plant, FireRes=140, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.1f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{37,37,37}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingPrickly = new EnemyDefaults {
            Id=306, TableIndex=100, Name="King Prickly", ModelCode="e106", ModelFootprint=44518,
            Abs=3, MinGoldDrop=7, DropChance=40, StealItemId=null, RareDropItemId=199,
            MaxHp=63, DamageReduction=3, WeaponDefense=10, KnockbackMult=0.0f,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=10.0f, BodyHeight=110.0f, BodyDepth=0.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{50,50,50}, ProjectileDamage=new int[]{} };

        // IceQueen (SW floor 18) fight companions — all id=0, boss sentinels. Ice-attack effect entities.
        // code=bari → baria.chr (barrier) @ data.dat 0x1e1b1000 — 0 ループ(loop) / 1 消滅(despawn) / 2 出現(appear).
        internal static readonly EnemyDefaults IQComp101 = new EnemyDefaults {
            Id=0, TableIndex=101, Name="Ice Barrier", ModelCode="bari", ModelFootprint=6805,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=2.0f, EntityScaleCopy=2.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=kori → kori.chr (ice arrow); motions documented above at IceArrow (0 ice appear / 1 ice burst).
        internal static readonly EnemyDefaults IQComp102 = new EnemyDefaults {
            Id=0, TableIndex=102, Name="Ice Prison", ModelCode="kori", ModelFootprint=7401,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=2.0f, EntityScaleCopy=2.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=i_me → i_meteo.chr (ice meteor) @ data.dat 0x1e37a000 — 0 氷生成(ice form) / 1 ループ / 2 爆発(explode).
        internal static readonly EnemyDefaults IQComp103 = new EnemyDefaults {
            Id=0, TableIndex=103, Name="Ice Meteor", ModelCode="i_me", ModelFootprint=7646,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=2.0f, EntityScaleCopy=2.0f,
            MeleeDamage=new int[]{69}, ProjectileDamage=new int[]{} };

        // code=i_ta → i_tatumaki.chr (ice tornado) @ data.dat 0x1e38b800 — 0 柱出現(pillar) / 1 竜巻(tornado) / 2 竜巻消える(vanish).
        internal static readonly EnemyDefaults IQComp104 = new EnemyDefaults {
            Id=0, TableIndex=104, Name="Ice Tornado", ModelCode="i_ta", ModelFootprint=10741,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=5, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=2.0f, EntityScaleCopy=2.0f,
            MeleeDamage=new int[]{74}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults Gacious = new EnemyDefaults {
            Id=317, TableIndex=105, Name="Gacious", ModelCode="e124", ModelFootprint=27766,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=null, RareDropItemId=65535,
            MaxHp=1800, DamageReduction=8, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{130,130,130,130,130}, ProjectileDamage=new int[]{} };

        // Dark Genie — FINAL FORM (EID 223, model c23a); the endgame Dark Genie battle, distinct from the earlier
        // forms c17a/c17b. Giant hands + mouth beam. (Its TI 106 sits in the d5 spawn-layout pool in the data, but
        // the final fight is a scripted encounter, not a Muska Lacka floor.)
        // Weak to fire (70) and holy (140). Attacks: 5 hand swings ×85; mouth beam = funcId-229 `_SET_SHOT`
        // (`ex4`/`ex5`), BST default 130 via `last_gw2` (idx26, proj speed 1.5, lifetime 20).
        // Sub-effect STBs: c23_beem (beam) / c23_syougeki (impact) / c23_hasira (pillar).
        internal static readonly EnemyDefaults DarkGenieFinal = new EnemyDefaults {
            Id=223, TableIndex=106, Name="Dark Genie (Final Form)", ModelCode="c23a", ModelFootprint=80660,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=65535, RareDropItemId=65535,
            MaxHp=5000, DamageReduction=8, WeaponDefense=0, KnockbackMult=0.0f,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            ItemDamageRes=100, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{85,85,85,85,85}, ProjectileDamage=new int[]{130} };

        // code=last_mc → last_mc.chr @ data.dat 0x203C9000 — 発射(fire) / 召喚(summon) / 待ち(wait). No own damage script (pure visual).
        internal static readonly EnemyDefaults DGFinalSummon = new EnemyDefaults {
            Id=0, TableIndex=107, Name="DG Final summon (last_mc)", ModelCode="last_mc", ModelFootprint=8596,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=8, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=last_gw1 → last_gw1.chr @ data.dat 0x20397000 — グランドウェイブ(ground wave). No own damage script.
        internal static readonly EnemyDefaults DGFinalGroundWave = new EnemyDefaults {
            Id=0, TableIndex=108, Name="DG Final ground wave (last_gw1)", ModelCode="last_gw1", ModelFootprint=7416,
            Abs=5, MinGoldDrop=5, DropChance=30, StealItemId=65535, RareDropItemId=65535,
            MaxHp=100, DamageReduction=8, WeaponDefense=0, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=70, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{} };

        // code=c23_beem → c23_beem.chr @ data.dat 0x203A7800 — 発射(fire) / ループ(loop) / 消滅(vanish). Beam, 175 dmg (= the c17_beem Dark Genie beam).
        internal static readonly EnemyDefaults DGFinalBeam = new EnemyDefaults {
            Id=0, TableIndex=109, Name="DG Final beam", ModelCode="c23_beem", ModelFootprint=21938,
            Abs=17, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        // code=c23_beem_s → c23_beem_s.chr @ data.dat 0x20851000 — 発動(activate) / ループ(loop) / 消滅(vanish). Beam variant, 175 dmg.
        internal static readonly EnemyDefaults DGFinalBeamS = new EnemyDefaults {
            Id=0, TableIndex=110, Name="DG Final beam (small)", ModelCode="c23_beem_s", ModelFootprint=7423,
            Abs=20, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=90, DamageReduction=0, WeaponDefense=20, KnockbackMult=0.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, ReticleWidth=1.0f, ReticleHeight=1.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{175}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GemronFire = new EnemyDefaults {
            Id=311, TableIndex=111, Name="Gemron (Fire)", ModelCode="e111", ModelFootprint=64091,
            Abs=15, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=161,
            MaxHp=2500, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=0, IceRes=150, ThunderRes=30, WindRes=30, HolyRes=30,
            ItemDamageRes=70, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults Nikapous = new EnemyDefaults {
            Id=308, TableIndex=112, Name="Nikapous", ModelCode="e108", ModelFootprint=69755,
            Abs=15, MinGoldDrop=8, DropChance=30, StealItemId=133, RareDropItemId=84,
            MaxHp=2350, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=100, ThunderRes=100, WindRes=125, HolyRes=125,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=27.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        // These reuse base-game species IDs with new model codes and scaled stats.
        // Not added to Defaults dictionary to avoid overwriting base-game entries.
        internal static readonly EnemyDefaults WhiteFangEnhanced = new EnemyDefaults {
            Id=56, TableIndex=113, Name="White Fang (Enhanced)", ModelCode="e125", ModelFootprint=68981,
            Abs=15, MinGoldDrop=12, DropChance=30, StealItemId=null, RareDropItemId=155,
            MaxHp=1750, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.5f, EntityScaleCopy=7.5f,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults ArthurEnhanced = new EnemyDefaults {
            Id=40, TableIndex=114, Name="Arthur (Enhanced)", ModelCode="e126", ModelFootprint=45647,
            Abs=20, MinGoldDrop=15, DropChance=30, StealItemId=177, RareDropItemId=92,
            MaxHp=2900, DamageReduction=10, WeaponDefense=60, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=50, IceRes=50, ThunderRes=150, WindRes=80, HolyRes=80,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=30.0f, BodyDepth=60.0f, EntityScale=9.0f, EntityScaleCopy=9.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults SilEnhanced = new EnemyDefaults {
            Id=91, TableIndex=115, Name="Sil (Enhanced)", ModelCode="e127", ModelFootprint=55324,
            Abs=15, MinGoldDrop=5, DropChance=30, StealItemId=177, RareDropItemId=65535,
            MaxHp=1500, DamageReduction=10, WeaponDefense=60, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{111,111,115,115,102,95,85}, ProjectileDamage=new int[]{102} };

        internal static readonly EnemyDefaults HalloweenEnhanced = new EnemyDefaults {
            Id=10, TableIndex=116, Name="Halloween (Enhanced)", ModelCode="e128", ModelFootprint=52028,
            Abs=15, MinGoldDrop=7, DropChance=40, StealItemId=168, RareDropItemId=148,
            MaxHp=1800, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults MasterJacketEnhanced = new EnemyDefaults {
            Id=1, TableIndex=117, Name="Master Jacket (Enhanced)", ModelCode="e129", ModelFootprint=78162,
            Abs=15, MinGoldDrop=7, DropChance=50, StealItemId=177, RareDropItemId=150,
            MaxHp=2000, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80, ThunderRes=100, WindRes=80, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=80,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults VulcanEnhanced = new EnemyDefaults {
            Id=70, TableIndex=118, Name="Vulcan (Enhanced)", ModelCode="e130", ModelFootprint=45214,
            Abs=15, MinGoldDrop=4, DropChance=30, StealItemId=81, RareDropItemId=160,
            MaxHp=2400, DamageReduction=10, WeaponDefense=40, KnockbackMult=1.0f,
            Category=EnemyCategory.Rock, FireRes=0, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{114,114,114}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MummyEnhanced = new EnemyDefaults {
            Id=50, TableIndex=119, Name="Mummy (Enhanced)", ModelCode="e131", ModelFootprint=91635,
            Abs=5, MinGoldDrop=10, DropChance=30, StealItemId=null, RareDropItemId=133,
            MaxHp=1500, DamageReduction=10, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=120,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=9.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{98,98}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults DiamondEnhanced = new EnemyDefaults {
            Id=46, TableIndex=120, Name="Diamond (Enhanced)", ModelCode="e132", ModelFootprint=48133,
            Abs=10, MinGoldDrop=12, DropChance=50, StealItemId=151, RareDropItemId=135,
            MaxHp=1750, DamageReduction=10, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50, WindRes=150, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{123,123}, ProjectileDamage=new int[]{} };

        // Gemron (Ice) — tier 2
        internal static readonly EnemyDefaults GemronIce = new EnemyDefaults {
            Id=312, TableIndex=121, Name="Gemron (Ice)", ModelCode="e112", ModelFootprint=64216,
            Abs=20, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=162,
            MaxHp=4000, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=150, IceRes=0, ThunderRes=30, WindRes=30, HolyRes=30,
            ItemDamageRes=70, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{120} };

        internal static readonly EnemyDefaults HornHead = new EnemyDefaults {
            Id=319, TableIndex=122, Name="Horn Head", ModelCode="e119", ModelFootprint=52419,
            Abs=20, MinGoldDrop=5, DropChance=30, StealItemId=null, RareDropItemId=186,
            MaxHp=2500, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=10.0f, EntityScaleCopy=10.0f,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults AuntieMeduEnhanced = new EnemyDefaults {
            Id=26, TableIndex=123, Name="Auntie Medu (Enhanced)", ModelCode="e133", ModelFootprint=56893,
            Abs=20, MinGoldDrop=15, DropChance=30, StealItemId=166, RareDropItemId=245,
            MaxHp=3750, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=30, IceRes=150, ThunderRes=30, WindRes=30, HolyRes=30,
            ItemDamageRes=100, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        internal static readonly EnemyDefaults RockanoffEnhanced = new EnemyDefaults {
            Id=77, TableIndex=124, Name="Rockanoff (Enhanced)", ModelCode="e134", ModelFootprint=16349,
            Abs=20, MinGoldDrop=10, DropChance=30, StealItemId=160, RareDropItemId=160,
            MaxHp=2500, DamageReduction=15, WeaponDefense=50, KnockbackMult=0.8f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults YammichEnhanced = new EnemyDefaults {
            Id=301, TableIndex=125, Name="Yammich (Enhanced)", ModelCode="e135", ModelFootprint=26611,
            Abs=20, MinGoldDrop=5, DropChance=30, StealItemId=160, RareDropItemId=92,
            MaxHp=3000, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=130,
            ItemDamageRes=90, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults WitchHellzaEnhanced = new EnemyDefaults {
            Id=21, TableIndex=126, Name="Witch Hellza (Enhanced)", ModelCode="e136", ModelFootprint=60483,
            Abs=20, MinGoldDrop=10, DropChance=30, StealItemId=169, RareDropItemId=94,
            MaxHp=1500, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=50,
            ItemDamageRes=85, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults SteelGiantEnhanced = new EnemyDefaults {
            Id=64, TableIndex=127, Name="Steel Giant (Enhanced)", ModelCode="e137", ModelFootprint=54796,
            Abs=25, MinGoldDrop=15, DropChance=50, StealItemId=177, RareDropItemId=154,
            MaxHp=3900, DamageReduction=15, WeaponDefense=70, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=80, IceRes=80, ThunderRes=150, WindRes=80, HolyRes=80,
            ItemDamageRes=95, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=33.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{163,163,178,178,163,162,161}, ProjectileDamage=new int[]{164,164} };

        internal static readonly EnemyDefaults ClubEnhanced = new EnemyDefaults {
            Id=45, TableIndex=128, Name="Club (Enhanced)", ModelCode="e138", ModelFootprint=49516,
            Abs=20, MinGoldDrop=12, DropChance=50, StealItemId=147, RareDropItemId=134,
            MaxHp=2525, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{114}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CorceaEnhanced = new EnemyDefaults {
            Id=28, TableIndex=129, Name="Corcea (Enhanced)", ModelCode="e139", ModelFootprint=51038,
            Abs=20, MinGoldDrop=8, DropChance=30, StealItemId=152, RareDropItemId=91,
            MaxHp=3250, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{110,110,110,110}, ProjectileDamage=new int[]{} };

        // Demon Shaft enemies reuse base-game eids with new model codes and scaled stats.
        // Each eid appears multiple times in the static table at u090a tiers: 15, 20, 23, 30.
        // The entries below capture the FIRST occurrence (lowest tier) as the EnemyDefaults base.
        // The Gemrons (311-315) are Demon Shaft unique enemies — each appears at a single tier.
        // Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults MimicDS = new EnemyDefaults {
            Id=309, TableIndex=130, Name="Mimic (Demon Shaft)", ModelCode="e109", ModelFootprint=25403,
            Abs=10, MinGoldDrop=26, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=3500, DamageReduction=15, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        // King Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults KingMimicDS = new EnemyDefaults {
            Id=310, TableIndex=131, Name="King Mimic (Demon Shaft)", ModelCode="e110", ModelFootprint=39746,
            Abs=20, MinGoldDrop=35, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=5000, DamageReduction=15, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // Gemron (Thunder) — tier 3
        internal static readonly EnemyDefaults GemronThunder = new EnemyDefaults {
            Id=313, TableIndex=132, Name="Gemron (Thunder)", ModelCode="e113", ModelFootprint=62767,
            Abs=25, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=163,
            MaxHp=5500, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=30, IceRes=30, ThunderRes=0, WindRes=150, HolyRes=30,
            ItemDamageRes=70, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults BishopQ = new EnemyDefaults {
            Id=316, TableIndex=133, Name="Bishop Q", ModelCode="e116", ModelFootprint=67591,
            Abs=25, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=94,
            MaxHp=6000, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=40, IceRes=40, ThunderRes=40, WindRes=40, HolyRes=140,
            ItemDamageRes=50, ItemStatusRes=50,
            BodyWidth=20.0f, BodyHeight=19.0f, BodyDepth=0.0f, EntityScale=11.0f, EntityScaleCopy=11.0f,
            MeleeDamage=new int[]{130,130}, ProjectileDamage=new int[]{130,130} };

        internal static readonly EnemyDefaults CaveBatEnhanced = new EnemyDefaults {
            Id=60, TableIndex=134, Name="Cave Bat (Enhanced)", ModelCode="e140", ModelFootprint=18780,
            Abs=25, MinGoldDrop=4, DropChance=30, StealItemId=151, RareDropItemId=0,
            MaxHp=1500, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, EntityScale=3.0f, EntityScaleCopy=3.0f,
            MeleeDamage=new int[]{95,95}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GolEnhanced = new EnemyDefaults {
            Id=90, TableIndex=135, Name="Gol (Enhanced)", ModelCode="e141", ModelFootprint=55324,
            Abs=25, MinGoldDrop=5, DropChance=30, StealItemId=177, RareDropItemId=65535,
            MaxHp=6000, DamageReduction=30, WeaponDefense=80, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=32.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{131,131,135,135,132,125,125}, ProjectileDamage=new int[]{132} };

        internal static readonly EnemyDefaults MaskOfPrajnaEnhanced = new EnemyDefaults {
            Id=75, TableIndex=136, Name="Mask of Prajna (Enhanced)", ModelCode="e142", ModelFootprint=36718,
            Abs=25, MinGoldDrop=15, DropChance=50, StealItemId=151, RareDropItemId=94,
            MaxHp=5500, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            ItemDamageRes=80, ItemStatusRes=70,
            BodyWidth=32.0f, BodyHeight=26.0f, BodyDepth=0.0f, EntityScale=7.5f, EntityScaleCopy=7.5f,
            MeleeDamage=new int[]{140,138}, ProjectileDamage=new int[]{146} };

        internal static readonly EnemyDefaults GyonEnhanced = new EnemyDefaults {
            Id=24, TableIndex=137, Name="Gyon (Enhanced)", ModelCode="e143", ModelFootprint=53050,
            Abs=25, MinGoldDrop=8, DropChance=30, StealItemId=134, RareDropItemId=226,
            MaxHp=5750, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{100,100}, ProjectileDamage=new int[]{100} };

        internal static readonly EnemyDefaults SpadeEnhanced = new EnemyDefaults {
            Id=47, TableIndex=138, Name="Spade (Enhanced)", ModelCode="e144", ModelFootprint=48269,
            Abs=25, MinGoldDrop=12, DropChance=50, StealItemId=152, RareDropItemId=132,
            MaxHp=5000, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults RashDasherEnhanced = new EnemyDefaults {
            Id=63, TableIndex=139, Name="Rash Dasher (Enhanced)", ModelCode="e145", ModelFootprint=42884,
            Abs=25, MinGoldDrop=12, DropChance=30, StealItemId=149, RareDropItemId=93,
            MaxHp=5000, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Beast, FireRes=50, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{102}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CaptainEnhanced = new EnemyDefaults {
            Id=27, TableIndex=140, Name="Captain (Enhanced)", ModelCode="e146", ModelFootprint=50447,
            Abs=25, MinGoldDrop=12, DropChance=30, StealItemId=177, RareDropItemId=227,
            MaxHp=4000, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80, WindRes=80, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{99,99,99,99}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDSEnhanced = new EnemyDefaults {
            Id=309, TableIndex=141, Name="Mimic (Demon Shaft) (Enhanced)", ModelCode="e109", ModelFootprint=25399,
            Abs=10, MinGoldDrop=26, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=5000, DamageReduction=20, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhanced = new EnemyDefaults {
            Id=310, TableIndex=142, Name="King Mimic (Demon Shaft) (Enhanced)", ModelCode="e110", ModelFootprint=39746,
            Abs=20, MinGoldDrop=35, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=7500, DamageReduction=20, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults GemronWind = new EnemyDefaults {
            Id=314, TableIndex=143, Name="Gemron (Wind)", ModelCode="e114", ModelFootprint=66562,
            Abs=30, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=164,
            MaxHp=8000, DamageReduction=23, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=100, ThunderRes=140, WindRes=0, HolyRes=100,
            ItemDamageRes=70, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{140} };

        internal static readonly EnemyDefaults SilverGear = new EnemyDefaults {
            Id=318, TableIndex=144, Name="Silver Gear", ModelCode="e118", ModelFootprint=64198,
            Abs=30, MinGoldDrop=5, DropChance=30, StealItemId=null, RareDropItemId=190,
            MaxHp=2500, DamageReduction=23, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=30, IceRes=30, ThunderRes=30, WindRes=30, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=10.0f, EntityScaleCopy=10.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{110} };

        internal static readonly EnemyDefaults AlexanderEnhanced = new EnemyDefaults {
            Id=43, TableIndex=145, Name="Alexander (Enhanced)", ModelCode="e149", ModelFootprint=39711,
            Abs=30, MinGoldDrop=17, DropChance=50, StealItemId=164, RareDropItemId=81,
            MaxHp=7500, DamageReduction=23, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=120, WindRes=100, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=32.0f, BodyHeight=30.0f, BodyDepth=0.0f, EntityScale=7.0f, EntityScaleCopy=7.0f,
            MeleeDamage=new int[]{122}, ProjectileDamage=new int[]{122} };

        internal static readonly EnemyDefaults HeartEnhanced = new EnemyDefaults {
            Id=44, TableIndex=146, Name="Heart (Enhanced)", ModelCode="e150", ModelFootprint=58698,
            Abs=30, MinGoldDrop=12, DropChance=50, StealItemId=150, RareDropItemId=133,
            MaxHp=5000, DamageReduction=23, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=0, IceRes=0, ThunderRes=0, WindRes=0, HolyRes=0,
            ItemDamageRes=80, ItemStatusRes=50,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=10.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults BomberHeadEnhanced = new EnemyDefaults {
            Id=49, TableIndex=147, Name="Bomber Head (Enhanced)", ModelCode="e151", ModelFootprint=62424,
            Abs=30, MinGoldDrop=10, DropChance=30, StealItemId=159, RareDropItemId=159,
            MaxHp=6000, DamageReduction=23, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=20,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=18.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{160} };

        internal static readonly EnemyDefaults CrabbyHermitEnhanced = new EnemyDefaults {
            Id=71, TableIndex=148, Name="Crabby Hermit (Enhanced)", ModelCode="e152", ModelFootprint=32512,
            Abs=30, MinGoldDrop=12, DropChance=30, StealItemId=166, RareDropItemId=92,
            MaxHp=6500, DamageReduction=23, WeaponDefense=20, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=70,
            BodyWidth=14.0f, BodyHeight=22.0f, BodyDepth=100.0f, EntityScale=10.0f, EntityScaleCopy=10.0f,
            MeleeDamage=new int[]{130,130,130}, ProjectileDamage=new int[]{130} };

        internal static readonly EnemyDefaults CursedRoseEnhanced = new EnemyDefaults {
            Id=68, TableIndex=149, Name="Cursed Rose (Enhanced)", ModelCode="e153", ModelFootprint=33886,
            Abs=30, MinGoldDrop=6, DropChance=30, StealItemId=null, RareDropItemId=146,
            MaxHp=5000, DamageReduction=23, WeaponDefense=10, KnockbackMult=0.0f,
            Category=EnemyCategory.Plant, FireRes=130, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=22.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{140,140,140}, ProjectileDamage=new int[]{140} };

        internal static readonly EnemyDefaults PiratesChariotEnhanced = new EnemyDefaults {
            Id=25, TableIndex=150, Name="Pirate's Chariot (Enhanced)", ModelCode="e154", ModelFootprint=28442,
            Abs=30, MinGoldDrop=15, DropChance=30, StealItemId=159, RareDropItemId=92,
            MaxHp=6750, DamageReduction=23, WeaponDefense=60, KnockbackMult=0.5f,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80, ThunderRes=140, WindRes=100, HolyRes=100,
            ItemDamageRes=95, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=8.0f, EntityScaleCopy=8.0f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{114} };

        internal static readonly EnemyDefaults SpaceGyonEnhanced = new EnemyDefaults {
            Id=72, TableIndex=151, Name="Space Gyon (Enhanced)", ModelCode="e155", ModelFootprint=54606,
            Abs=30, MinGoldDrop=4, DropChance=30, StealItemId=153, RareDropItemId=226,
            MaxHp=7800, DamageReduction=23, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Marine, FireRes=0, IceRes=0, ThunderRes=20, WindRes=0, HolyRes=0,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{160,160}, ProjectileDamage=new int[]{160} };

        internal static readonly EnemyDefaults MimicDSEnhancedTwice = new EnemyDefaults {
            Id=309, TableIndex=152, Name="Mimic (Demon Shaft) (Enhanced x2)", ModelCode="e109", ModelFootprint=25431,
            Abs=15, MinGoldDrop=26, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=6500, DamageReduction=23, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhancedTwice = new EnemyDefaults {
            Id=310, TableIndex=153, Name="King Mimic (Demon Shaft) (Enhanced x2)", ModelCode="e110", ModelFootprint=39746,
            Abs=25, MinGoldDrop=35, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=10000, DamageReduction=23, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        // Gemron (Holy) — tier 5
        internal static readonly EnemyDefaults GemronHoly = new EnemyDefaults {
            Id=315, TableIndex=154, Name="Gemron (Holy)", ModelCode="e115", ModelFootprint=64147,
            Abs=35, MinGoldDrop=20, DropChance=30, StealItemId=null, RareDropItemId=165,
            MaxHp=12500, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Dragon, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=0,
            ItemDamageRes=70, ItemStatusRes=60,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=6.5f, EntityScaleCopy=6.5f,
            MeleeDamage=new int[]{}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults GaciousEnhanced = new EnemyDefaults {
            Id=317, TableIndex=155, Name="Gacious (Enhanced)", ModelCode="e117", ModelFootprint=45760,
            Abs=35, MinGoldDrop=5, DropChance=30, StealItemId=189, RareDropItemId=65535,
            MaxHp=15000, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=0, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=140,
            ItemDamageRes=100, ItemStatusRes=90,
            BodyWidth=7.0f, BodyHeight=23.0f, BodyDepth=60.0f, EntityScale=11.0f, EntityScaleCopy=11.0f,
            MeleeDamage=new int[]{180,180,180,180,180}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults EvilBatEnhanced = new EnemyDefaults {
            Id=61, TableIndex=156, Name="Evil Bat (Enhanced)", ModelCode="e158", ModelFootprint=18673,
            Abs=35, MinGoldDrop=5, DropChance=30, StealItemId=151, RareDropItemId=149,
            MaxHp=7500, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=150, IceRes=150, ThunderRes=150, WindRes=150, HolyRes=200,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=10.0f, BodyDepth=60.0f, EntityScale=3.0f, EntityScaleCopy=3.0f,
            MeleeDamage=new int[]{122,122}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults CrescentBaronEnhanced = new EnemyDefaults {
            Id=76, TableIndex=157, Name="Crescent Baron (Enhanced)", ModelCode="e159", ModelFootprint=40164,
            Abs=35, MinGoldDrop=18, DropChance=50, StealItemId=null, RareDropItemId=170,
            MaxHp=16000, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            ItemDamageRes=80, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=6.0f, EntityScaleCopy=6.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults StatueDogEnhanced = new EnemyDefaults {
            Id=303, TableIndex=158, Name="Statue Dog (Enhanced)", ModelCode="e160", ModelFootprint=30171,
            Abs=35, MinGoldDrop=5, DropChance=30, StealItemId=160, RareDropItemId=92,
            MaxHp=12500, DamageReduction=30, WeaponDefense=10, KnockbackMult=0.6f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            ItemDamageRes=90, ItemStatusRes=100,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=9.0f, EntityScaleCopy=9.0f,
            MeleeDamage=new int[]{110}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults JokerEnhanced = new EnemyDefaults {
            Id=48, TableIndex=159, Name="Joker (Enhanced)", ModelCode="e161", ModelFootprint=49660,
            Abs=35, MinGoldDrop=12, DropChance=50, StealItemId=149, RareDropItemId=154,
            MaxHp=9500, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mage, FireRes=50, IceRes=50, ThunderRes=50, WindRes=50, HolyRes=150,
            ItemDamageRes=50, ItemStatusRes=10,
            BodyWidth=11.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults LichEnhanced = new EnemyDefaults {
            Id=51, TableIndex=160, Name="Lich (Enhanced)", ModelCode="e162", ModelFootprint=54825,
            Abs=35, MinGoldDrop=15, DropChance=80, StealItemId=176, RareDropItemId=94,
            MaxHp=10000, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Undead, FireRes=20, IceRes=20, ThunderRes=20, WindRes=20, HolyRes=160,
            ItemDamageRes=80, ItemStatusRes=30,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{110,110}, ProjectileDamage=new int[]{110} };

        internal static readonly EnemyDefaults TitanEnhanced = new EnemyDefaults {
            Id=33, TableIndex=161, Name="Titan (Enhanced)", ModelCode="e163", ModelFootprint=54352,
            Abs=35, MinGoldDrop=15, DropChance=30, StealItemId=177, RareDropItemId=160,
            MaxHp=11500, DamageReduction=30, WeaponDefense=50, KnockbackMult=0.5f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            ItemDamageRes=100, ItemStatusRes=70,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{155,155,160,160,150,145,140}, ProjectileDamage=new int[]{160,160} };

        internal static readonly EnemyDefaults LivingArmorEnhanced = new EnemyDefaults {
            Id=55, TableIndex=162, Name="Living Armor (Enhanced)", ModelCode="e164", ModelFootprint=55809,
            Abs=35, MinGoldDrop=15, DropChance=30, StealItemId=null, RareDropItemId=160,
            MaxHp=9500, DamageReduction=30, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80, HolyRes=80,
            ItemDamageRes=100, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=25.0f, BodyDepth=60.0f, EntityScale=4.0f, EntityScaleCopy=4.0f,
            MeleeDamage=new int[]{150,150,150,150}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults MimicDSEnhancedThrice = new EnemyDefaults {
            Id=309, TableIndex=163, Name="Mimic (Demon Shaft) (Enhanced x3)", ModelCode="e109", ModelFootprint=25431,
            Abs=20, MinGoldDrop=26, DropChance=80, StealItemId=177, RareDropItemId=235,
            MaxHp=7500, DamageReduction=30, WeaponDefense=10, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=20.0f, BodyDepth=60.0f, EntityScale=5.0f, EntityScaleCopy=5.0f,
            MeleeDamage=new int[]{130}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults KingMimicDSEnhancedThrice = new EnemyDefaults {
            Id=310, TableIndex=164, Name="King Mimic (Demon Shaft) (Enhanced x3)", ModelCode="e110", ModelFootprint=39746,
            Abs=30, MinGoldDrop=35, DropChance=80, StealItemId=175, RareDropItemId=181,
            MaxHp=19500, DamageReduction=30, WeaponDefense=50, KnockbackMult=1.0f,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=90, ItemStatusRes=50,
            BodyWidth=7.0f, BodyHeight=28.0f, BodyDepth=60.0f, EntityScale=12.0f, EntityScaleCopy=12.0f,
            MeleeDamage=new int[]{170,170,170}, ProjectileDamage=new int[]{} };

        internal static readonly EnemyDefaults BlackKnight = new EnemyDefaults {
            Id=221, TableIndex=165, Name="Black Knight", ModelCode="c21a", ModelFootprint=187,
            Abs=5, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=65535,
            MaxHp=40000, DamageReduction=8, WeaponDefense=100, KnockbackMult=0.0f,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=20.0f, BodyHeight=35.0f, BodyDepth=200.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{170,170}, ProjectileDamage=new int[]{150} };

        internal static readonly EnemyDefaults BlackKnightMount = new EnemyDefaults {
            Id=221, TableIndex=166, Name="Black Knight Mount", ModelCode="c22a", ModelFootprint=144491,
            Abs=5, MinGoldDrop=0, DropChance=0, StealItemId=65535, RareDropItemId=0,
            MaxHp=50000, DamageReduction=8, WeaponDefense=100, KnockbackMult=0.0f,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            ItemDamageRes=50, ItemStatusRes=0,
            BodyWidth=7.0f, BodyHeight=17.0f, BodyDepth=60.0f, EntityScale=14.0f, EntityScaleCopy=14.0f,
            MeleeDamage=new int[]{170}, ProjectileDamage=new int[]{170} };

        // CUT ENEMY — no species table entry and no CHR model file (e53a.chr/e54a.chr absent).
        // Id=54 is referenced in WOF spawn-pool data and its name appears in the game's name table,
        // but the engine skips EIDs 53–54 entirely in the packed sequential species table.
        // TableIndex is null: there is no record in EnemySpeciesTable for this EID.
        // Only appears on WOF event floors 8 and 16 via a scripted spawn that bypasses the
        // normal species-table lookup. Live-slot stats are unknown until an event-floor dump is taken.
        internal static readonly EnemyDefaults KillerSnake = new EnemyDefaults {
            Id=54, Name="Killer Snake" };

        // ── Lookups ───────────────────────────────────────────────────────────────
        /// <summary>Every species that owns a static-table record, keyed by physical TableIndex. Unlike
        /// <see cref="Defaults"/> (keyed by the non-unique species Id) this also holds the Demon-Shaft
        /// "Enhanced" re-skins and the Id=0 boss companions, since each owns a distinct TableIndex.
        /// Built by reflection over the EnemyDefaults fields so it never drifts from the declarations.</summary>
        internal static readonly Dictionary<int, EnemyDefaults> All = new();
        /// <summary>Lookup by species Id (one curated record per Id; Enhanced re-skins share Ids, so aren't here).</summary>
        internal static readonly Dictionary<ushort, EnemyDefaults> Defaults;
        /// <summary>Species eligible for generic random placement, keyed by TableIndex: everything in
        /// <see cref="All"/> except bosses, boss companions, Killer Snake, and mimics. Mimics are excluded because
        /// they're never placed generically — the randomizer inserts the current dungeon's mimic + king mimic itself
        /// (weighted, dungeon-aware via <see cref="MimicsByDungeon"/>).</summary>
        internal static readonly Dictionary<int, EnemyDefaults> RandomizerValid;
        /// <summary>Bosses that spawn cleanly as regular roster enemies (no multi-part / script glitches), keyed by
        /// TableIndex. Kept out of <see cref="RandomizerValid"/>; the randomizer will optionally fold these in via a
        /// toggle. Currently just Minotaur Joe.</summary>
        internal static readonly Dictionary<int, EnemyDefaults> RandomizerBosses;
        static EnemySpecies()
        {
            foreach (var f in typeof(EnemySpecies).GetFields(System.Reflection.BindingFlags.Static
                         | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (f.FieldType != typeof(EnemyDefaults)) continue;
                var e = (EnemyDefaults)f.GetValue(null);
                if (e.TableIndex != null) All[e.TableIndex.Value] = e;
            }

            // Derived from All (keyed by TableIndex): one curated record per species Id. Skip the Demon-Shaft
            // "Enhanced" re-skins (which share their base's Id), the Id-0 boss companions (DG/IQ/SW collide entities),
            // and the Black Knight mount record (shares Id 221 with Black Knight) — so the base species wins each Id.
            // Then add Killer Snake, which has no species-table record (TableIndex null), so isn't in All.
            Defaults = new Dictionary<ushort, EnemyDefaults>(All.Count);
            foreach (EnemyDefaults e in All.Values)
            {
                if (e.Id == 0) continue;
                if (e.Name != null && (e.Name.Contains("Enhanced") || e.Name.Contains("Mount"))) continue;
                Defaults[e.Id] = e;
            }
            Defaults[KillerSnake.Id] = KillerSnake;

            // Randomizer-eligible set: drop bosses, boss companions, and mimics. Killer Snake is excluded for free
            // (no record, so absent from All). Tests: Id 0 = collide companion; a non-'e' ModelCode = boss/effect mesh
            // ('c…'); BossEnemies = the named bosses (also catches Wine Keg, which uniquely has an 'e' boss model);
            // a "Mimic" name = handled separately by the dungeon-aware mimic insertion, never placed generically.
            RandomizerValid = new Dictionary<int, EnemyDefaults>(All.Count);
            foreach (var kv in All)
            {
                EnemyDefaults e = kv.Value;
                if (e.Id == 0) continue;
                if (string.IsNullOrEmpty(e.ModelCode) || e.ModelCode[0] != 'e') continue;
                if (BossEnemies.ContainsKey(e.Id)) continue;
                if (e.Name != null && e.Name.Contains("Mimic")) continue;
                RandomizerValid[kv.Key] = e;
            }

            // Bosses the randomizer may optionally include (toggle TBD). Only those that spawn cleanly as a regular
            // roster enemy — currently just Minotaur Joe.
            RandomizerBosses = new Dictionary<int, EnemyDefaults>
            {
                { MinotaurJoe.TableIndex.Value, MinotaurJoe },
            };
        }

        // ── Species groups, keyed by physical TableIndex ───────────────────────────────────────────────────
        // Reference sets used by the enemy randomizer's themed-roster mode (EnemyModelInjector.StageFloorRoster):
        //   • NativeByDungeon[d] = the regular (non-boss) species that natively spawn in dungeon d (0..6 =
        //     DBC, WOF, SW, SMT, MS, GoT, DS), derived from DungeonData's per-floor spawn pools. Used to fill
        //     the rest of a capped themed roster with "dungeon natives", and as a convenient source for hand-
        //     authoring the themed groups below.
        //   • ThemeGroups / MimicsByDungeon = curated "themed" rosters. A floor in themed mode picks one of
        //     these as its whole roster. Tweak the membership freely — these are the knobs to tune.
        private static Dictionary<int, EnemyDefaults> Group(params EnemyDefaults[] members)
        {
            var d = new Dictionary<int, EnemyDefaults>(members.Length);
            foreach (var e in members) if (e.TableIndex != null) d[e.TableIndex.Value] = e;
            return d;
        }

        private static Dictionary<int, EnemyDefaults> Merge(params Dictionary<int, EnemyDefaults>[] groups)
        {
            var d = new Dictionary<int, EnemyDefaults>();
            foreach (var g in groups) foreach (var kv in g) d[kv.Key] = kv.Value;
            return d;
        }

        // Native regular species per dungeon. Mimics are deliberately EXCLUDED — they're never sourced from the
        // native pool; the randomizer's dedicated weighted mimic insertion (always the current dungeon's mimic +
        // king mimic) is the single source of mimics. Bosses / event-only / Id-0 companions are also excluded.
        internal static readonly Dictionary<int, EnemyDefaults> NativeDBC = Group(
            SkeletonSoldier, MasterJacket, Statue, Dasher, CaveBat, Ghost, Dragon, Rockanoff, StatueDog,
            Yammich, Opar);
        internal static readonly Dictionary<int, EnemyDefaults> NativeWOF = Group(
            CannibalPlant, EarthDigger, FliFli, Hornet, Halloween, Werewolf, WitchIllza, KingPrickly, HaleyHoley,
            Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday);
        internal static readonly Dictionary<int, EnemyDefaults> NativeSW = Group(
            Captain, Corcea, Gunny, Gyon, PiratesChariot, AuntieMedu, MaskOfPrajna, CursedRose, Sam);
        internal static readonly Dictionary<int, EnemyDefaults> NativeSMT = Group(
            Golem, Mummy, Phantom, BomberHead, CrabbyHermit, Dune, Titan, SteelGiant, BlueDragon, Gol, Sil, MrBlare);
        internal static readonly Dictionary<int, EnemyDefaults> NativeMS = Group(
            Arthur, WitchHellza, MoonBug, MoonDigger, SpaceGyon, Vulcan, WhiteFang, Titan, CrescentBaron, HellPockle);
        internal static readonly Dictionary<int, EnemyDefaults> NativeGoT = Group(
            Alexander, Billy, BlackDragon, Blizzard, Club, CurseDancer, DarkFlower, Diamond, EvilBat, Heart, Joker,
            Lich, LivingArmor, RashDasher, Spade);
        // Demon Shaft is five 20-floor depth regions, each anchored by a Gemron element (see DungeonData.DS_Front).
        // Each region is also a selectable theme (registered in ThemeGroups); NativeDS is their union, used as the
        // DS native-fill pool. Footprints are far over the DS buffer, so as themes they're trimmed to a fitting
        // subset per floor (non-requireFullFit).
        internal static readonly Dictionary<int, EnemyDefaults> DSFire =                  // floors 1–20    Σ ≈ 618,970 B
            Group(GemronFire, MummyEnhanced, SilEnhanced, Nikapous, HalloweenEnhanced, MasterJacketEnhanced,
                  VulcanEnhanced, DiamondEnhanced, WhiteFangEnhanced, ArthurEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> DSIce =                   // floors 21–40   Σ ≈ 432,321 B
            Group(GemronIce, CorceaEnhanced, ClubEnhanced, RockanoffEnhanced, HornHead, AuntieMeduEnhanced,
                  YammichEnhanced, WitchHellzaEnhanced, SteelGiantEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> DSThunder =               // floors 41–60   Σ ≈ 435,830 B
            Group(GemronThunder, CaptainEnhanced, SpadeEnhanced, GolEnhanced, CaveBatEnhanced, BishopQ,
                  GyonEnhanced, RashDasherEnhanced, MaskOfPrajnaEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> DSWind =                  // floors 61–80   Σ ≈ 441,039 B
            Group(GemronWind, BomberHeadEnhanced, SilverGear, PiratesChariotEnhanced, CrabbyHermitEnhanced,
                  SpaceGyonEnhanced, CursedRoseEnhanced, HeartEnhanced, AlexanderEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> DSHoly =                  // floors 81–99   Σ ≈ 413,561 B
            Group(GemronHoly, LivingArmorEnhanced, StatueDogEnhanced, JokerEnhanced, EvilBatEnhanced,
                  GaciousEnhanced, LichEnhanced, TitanEnhanced, CrescentBaronEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> NativeDS =
            Merge(DSFire, DSIce, DSThunder, DSWind, DSHoly);
        // Indexed 0..6 = DBC, WOF, SW, SMT, MS, GoT, DS (matches checkDungeon / BtEnemyLayout order).
        internal static readonly Dictionary<int, EnemyDefaults>[] NativeByDungeon =
            { NativeDBC, NativeWOF, NativeSW, NativeSMT, NativeMS, NativeGoT, NativeDS };

        // ── Themed groups ──────────────────────────────────────────────────────────────────────────────────
        // The trailing "Σ footprint" is the sum of the members' ModelFootprint (bytes) — the worst-case model-buffer
        // cost if the whole group loads on one floor. Keep it under a dungeon's cap (DungeonData.ModelBufferCapMin,
        // ~270 KB+) or the randomizer will drop members to fit. Update these if you change a group's membership.
        // Special conditions (implemented in EnemyModelInjector.BuildThemedRoster):
        //   • requireFullFit (ThemeGroups flag): Cards, DaysOfTheWeek, GemronElementals are all-or-nothing sets —
        //     only chosen on a floor whose buffer fits the WHOLE group, and never trimmed. On a tight-but-fitting
        //     floor they go whole-group (all repeatable, no mimic/native fill); with headroom they may instead cap
        //     each member to one spawn and fill the rest with mimics/natives.
        //   • ThemeSingleSpawnByTheme: members pinned to a single spawn within a given theme even on a whole-group
        //     floor (e.g. Captain/Sil/Gol in Pirates), while the rest of the group carries the population.
        internal static readonly Dictionary<int, EnemyDefaults> Cards =                   // Σ footprint ≈ 254,276 B  (requireFullFit)
            Group(Heart, Club, Diamond, Spade, Joker);
        internal static readonly Dictionary<int, EnemyDefaults> CardsEnhanced =           // Σ footprint ≈ 254,276 B  (requireFullFit)
            Group(HeartEnhanced, ClubEnhanced, DiamondEnhanced, SpadeEnhanced, JokerEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> DaysOfTheWeek =           // Σ footprint ≈ 295,962 B  (requireFullFit)
            Group(Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday);
        internal static readonly Dictionary<int, EnemyDefaults> Outlaws =                 // Σ footprint ≈ 162,546 B
            Group(Sam, MrBlare, Billy);
        internal static readonly Dictionary<int, EnemyDefaults> Pirates =                 // Σ footprint ≈ 240,840 B  (Captain: single-spawn; ½× weight)
            Group(Captain, Corcea, PiratesChariot, Sil, Gol);
        internal static readonly Dictionary<int, EnemyDefaults> GemronElementals =        // Σ footprint ≈ 321,783 B  (requireFullFit)
            Group(GemronFire, GemronIce, GemronThunder, GemronWind, GemronHoly);

        // Per-element themes — thematic/by-lore, each anchored by its Gemron (and the suit card / dragon that carries
        // the matching elemental affinity in the data: Heart=Fire, Spade=Ice, Diamond=Thunder, Club=Wind). Tweak to taste.
        internal static readonly Dictionary<int, EnemyDefaults> Fire =                    // Σ footprint ≈ 236,451 B
            Group(GemronFire, VulcanEnhanced, MrBlare, Dragon);
        internal static readonly Dictionary<int, EnemyDefaults> Ice =                     // Σ footprint ≈ 261,180 B
            Group(GemronIce, Blizzard, Sam, BlueDragon);
        internal static readonly Dictionary<int, EnemyDefaults> Thunder =                 // Σ footprint ≈ 250,197 B
            Group(GemronThunder, GolEnhanced, EarthDigger, MoonBug, Billy);
        internal static readonly Dictionary<int, EnemyDefaults> Wind =                    // Σ footprint ≈ 258,406 B
            Group(GemronWind, Dune, Golem, SilverGear, Phantom);
        internal static readonly Dictionary<int, EnemyDefaults> Holy =                    // Σ footprint ≈ 225,655 B
            Group(GemronHoly, GaciousEnhanced, MaskOfPrajnaEnhanced, HornHead, YammichEnhanced);

        // Per-category themes — seeded with every base (non-Enhanced) regular of that EnemyCategory.
        // These are deliberately broad; trim each to the most representative species to taste.
        internal static readonly Dictionary<int, EnemyDefaults> Dragons =                 // Σ footprint ≈ 235,494 B
            Group(Dragon, BlueDragon, BlackDragon);
        internal static readonly Dictionary<int, EnemyDefaults> Undead =                  // Σ footprint ≈ 353,280 B  (Lich: single-spawn)
            Group(SkeletonSoldier, MasterJacket, Gacious, HornHead, SilverGear, Lich);
        internal static readonly Dictionary<int, EnemyDefaults> Marine =                  // Σ footprint ≈ 216,801 B
            Group(CrabbyHermit, Gunny, Gyon, Opar, SpaceGyon);
        internal static readonly Dictionary<int, EnemyDefaults> Rock =                    // Σ footprint ≈ 144,285 B
            Group(StatueDog, Dune, Titan, Rockanoff);
        internal static readonly Dictionary<int, EnemyDefaults> Plants =                  // Σ footprint ≈ 139,688 B
            Group(CannibalPlant, DarkFlower, CursedRose, FliFli);
        internal static readonly Dictionary<int, EnemyDefaults> Beasts =                  // Σ footprint ≈ 209,434 B
            Group(Dasher, Werewolf, WhiteFangEnhanced, RashDasher);
        internal static readonly Dictionary<int, EnemyDefaults> SkyDwellers =             // Σ footprint ≈ 77,545 B
            Group(CaveBat, EvilBat, CrescentBaron);
        internal static readonly Dictionary<int, EnemyDefaults> Metal =                   // Σ footprint ≈ 223,885 B
            Group(Arthur, Alexander, SteelGiant, PiratesChariot, LivingArmor);
        internal static readonly Dictionary<int, EnemyDefaults> Mages =                   // Σ footprint ≈ 234,803 B
            Group(CurseDancer, WitchHellza, BishopQ, Nikapous);

        // Concept / gag themes.
        internal static readonly Dictionary<int, EnemyDefaults> NotTheBees =              // Σ footprint ≈ 66,587 B
            Group(Hornet, DarkFlower);
        internal static readonly Dictionary<int, EnemyDefaults> BombsAway =               // Σ footprint ≈ 62,424 B
            Group(BomberHeadEnhanced);
        internal static readonly Dictionary<int, EnemyDefaults> WhackaMole =              // Σ footprint ≈ 102,991 B
            Group(EarthDigger, HaleyHoley);
        internal static readonly Dictionary<int, EnemyDefaults> Bait =                    // Σ footprint ≈ 184,592 B  (King Prickly: single-spawn; 3× weight)
            Group(KingPrickly, MoonDigger, WitchIllza, HaleyHoley);
        internal static readonly Dictionary<int, EnemyDefaults> HalloweenTheme =          // Σ footprint ≈ 347,863 B  (2× weight)
            Group(Halloween, Ghost, SkeletonSoldier, MummyEnhanced, Werewolf, EvilBat);
        internal static readonly Dictionary<int, EnemyDefaults> Gorgon =                  // Σ footprint ≈ 111,843 B
            Group(AuntieMedu, Statue);
        internal static readonly Dictionary<int, EnemyDefaults> Precious =                // Σ footprint ≈ 153,570 B  (½× weight)
            Group(Sil, Gol, HellPockle);
        internal static readonly Dictionary<int, EnemyDefaults> GraveyardShift =          // Σ footprint ≈ 270,729 B  (2× weight)
            Group(MasterJacketEnhanced, SkeletonSoldier, HornHead, SilverGear);

        // Per-dungeon mimic themes (2 entries each: mimic + king mimic). Indexed 0..6 like NativeByDungeon.
        internal static readonly Dictionary<int, EnemyDefaults> MimicsDBC = Group(MimicDBC, KingMimicDBC);  // Σ ≈ 63,617 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsWOF = Group(MimicWOF, KingMimicWOF);  // Σ ≈ 63,453 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsSW  = Group(MimicSW,  KingMimicSW);   // Σ ≈ 63,565 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsSMT = Group(MimicSMT, KingMimicSMT);  // Σ ≈ 63,689 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsMS  = Group(MimicMS,  KingMimicMS);   // Σ ≈ 64,513 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsGoT = Group(MimicGoT, KingMimicGoT);  // Σ ≈ 63,641 B
        internal static readonly Dictionary<int, EnemyDefaults> MimicsDS  = Group(MimicDS,  KingMimicDS);   // Σ ≈ 65,149 B
        internal static readonly Dictionary<int, EnemyDefaults>[] MimicsByDungeon =
            { MimicsDBC, MimicsWOF, MimicsSW, MimicsSMT, MimicsMS, MimicsGoT, MimicsDS };

        // Registry the randomizer draws a random theme from (per-dungeon mimics are handled separately, since
        // which mimic theme applies depends on the current dungeon). requireFullFit = all-or-nothing: only chosen when
        // the whole group fits the floor's model buffer, and never trimmed (see EnemySpecies' themed-group notes).
        internal static readonly (string name, Dictionary<int, EnemyDefaults> members, bool requireFullFit)[] ThemeGroups =
        {
            ("Cards",             Cards,            true),
            ("Cards (Enhanced)",  CardsEnhanced,    true),
            ("Days of the Week",  DaysOfTheWeek,    true),
            ("Dragons",           Dragons,          false),
            ("Outlaws",           Outlaws,          false),
            ("Pirates",           Pirates,          true),
            ("Gemron Elementals", GemronElementals, true),
            ("Fire",              Fire,             true),
            ("Ice",               Ice,              true),
            ("Thunder",           Thunder,          true),
            ("Wind",              Wind,             true),
            ("Holy",              Holy,             true),
            ("Undead",            Undead,           false),
            ("Marine",            Marine,           false),
            ("Rock",              Rock,             false),
            ("Plants",            Plants,           false),
            ("Beasts",            Beasts,           false),
            ("Sky Dwellers",      SkyDwellers,      false),
            ("Metal",             Metal,            false),
            ("Mages",             Mages,            false),
            ("Not the Bees",      NotTheBees,       false),
            ("Bombs Away",        BombsAway,        false),
            ("Whack-a-Mole",      WhackaMole,       false),
            ("Bait",              Bait,             false),
            ("Halloween",         HalloweenTheme,   false),
            ("Gorgon",            Gorgon,           false),
            ("Precious",          Precious,         false),
            ("Graveyard Shift",   GraveyardShift,   false),
            // Demon Shaft depth regions (over budget — trimmed to a fitting subset per floor).
            ("Demon Shaft: Fire",    DSFire,        false),
            ("Demon Shaft: Ice",     DSIce,         false),
            ("Demon Shaft: Thunder", DSThunder,     false),
            ("Demon Shaft: Wind",    DSWind,        false),
            ("Demon Shaft: Holy",    DSHoly,        false),
        };

        // Themes native to a specific dungeon (0..6 = DBC,WOF,SW,SMT,MS,GoT,DS), keyed by display name. A native
        // theme's pick weight ramps down the further the current dungeon is from its home (down to ThemeFarWeight in
        // the most distant dungeon) and is multiplied by ThemeNativeBoost when you're IN its home dungeon. Themes not
        // listed here are non-native (constant weight 1). See EnemyModelInjector.ThemeWeight. Currently the five
        // Demon Shaft depth regions (home = DS).
        internal static readonly Dictionary<string, int> ThemeHomeDungeon = new()
        {
            { "Demon Shaft: Fire", 6 }, { "Demon Shaft: Ice",  6 }, { "Demon Shaft: Thunder", 6 },
            { "Demon Shaft: Wind", 6 }, { "Demon Shaft: Holy", 6 },
        };

        // Flat per-theme selection-weight multipliers, keyed by display name. Applied on top of ThemeWeight (the
        // dungeon-proximity weighting), so a value of 4 makes the theme 4x as likely as a plain non-native theme in
        // every dungeon. Themes not listed default to 1.
        internal static readonly Dictionary<string, double> ThemeWeightMultiplier = new()
        {
            { "Bait", 3.0 },
            { "Pirates", 0.5 },
            { "Whack-a-Mole", 0.5 },
            { "Precious", 0.5 },
            { "Halloween", 2.0 }, { "Bombs Away", 2.0 }, { "Graveyard Shift", 2.0 },
            // Per-category themes — 2× chance.
            { "Dragons", 2.0 }, { "Undead", 2.0 }, { "Marine", 2.0 }, { "Rock", 2.0 }, { "Plants", 2.0 },
            { "Beasts", 2.0 }, { "Sky Dwellers", 2.0 }, { "Metal", 2.0 }, { "Mages", 2.0 },
        };

        // Themes that always place BOTH the current dungeon's mimic + king mimic (repeatable) on their floor — a
        // guaranteed pair instead of the usual random mimic/native fill. Best-effort under the model buffer (on the
        // tightest floor the pair may not fit alongside a full requireFullFit group). See BuildThemedRoster.
        internal static readonly HashSet<string> ThemeGuaranteedMimics = new() { "Pirates" };

        // Per-theme single-spawn: members pinned to SpawnCap 1 (one-of-each) only within the named theme, even on a
        // whole-group floor; the rest of the group carries the floor population. Members spawn normally in their other
        // themes (e.g. Sil/Gol are one-of-each in Pirates but repeatable in Precious and the Demon Shaft regions).
        internal static readonly Dictionary<string, HashSet<int>> ThemeSingleSpawnByTheme = new()
        {
            { "Pirates", new HashSet<int> { Captain.TableIndex.Value, Sil.TableIndex.Value, Gol.TableIndex.Value } },
            { "Undead",  new HashSet<int> { Lich.TableIndex.Value } },
            { "Bait",    new HashSet<int> { KingPrickly.TableIndex.Value } },
        };

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
