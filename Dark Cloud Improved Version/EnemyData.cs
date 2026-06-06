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
        // Slot +0x044 (primary) equals +0x048 (copy) at spawn. Static table: EnemySpeciesTable.EntityScale (0x044).
        internal float? EntityScale;
        internal float? EntityScaleCopy;

        // Slot +0x090: packed ushorts — source is EnemySpeciesTable.Unk010/Unk012; loaded at spawn.
        // Modifying the static table value changes the value the slot gets on next spawn.
        // Observed: Gyon=0/0; Captain/Auntie Medu=3/0; Cursed Rose=2/0; Pirate's Chariot=5/30; Gunny=5/20; Mask of Prajna=5/10.
        internal ushort? Unk090A;   // slot +0x090 low  — static table: EnemySpeciesTable.Unk010 (0x010)
        internal ushort? Unk090B;   // slot +0x090 high — static table: EnemySpeciesTable.Unk012 (0x012); non-zero on ranged enemies; may be max shoot range

        // Slot +0x0D8: steal item ID — low ushort is the item ID; 65535 if none.
        // Static table: EnemySpeciesTable.StealItemId (0x02C) / EnemySpeciesTable.StealFlag (0x02E).
        internal ushort? StealItemId;

        // Slot +0x0DC: packed ushorts — semantics unconfirmed. Scale resembles elemental resistances (100=neutral, <100=resistant).
        // Static table: EnemySpeciesTable.ItemResA (0x030) / EnemySpeciesTable.ItemResB (0x032).
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
        internal const int SpeciesDataPtr    = 0x04C; // int   — pointer to shared enemy-species behavior data; identical across all slots of the same species (e.g. all Pirate's Chariots = 0x01C05140)
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

        internal const int Unk090            = 0x090; // packed ushorts — semantics unclear; write at floor load had no effect on AI; high ushort non-zero only on projectile enemies (may be max shoot range)

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
        internal const int StealItemId       = 0x0D8; // int   — packed: low ushort = item ID; high ushort = 1 for all observed enemies
        internal const int ItemResistance    = 0x0DC; // int   — packed ushorts; possibly two resistance values; scale resembles elemental resistance (100=neutral, <100=resistant)

        // ── AI State Machine ──────────────────────────────────────────────────
        // AiStatePacked and AiSpeedParam change together at each AI phase transition.
        // Observed AiStatePacked values: 0x00000001 (idle/patrol), 0x00040002, 0x0002000D, 0x00060010 (attack/chase states).
        // AiSpeedParam is enemy species and state-specific: Auntie Medu alternates 0.36↔0.20; Pirate's Chariot uses 0.25/−1.0; Mask of Prajna uses 0.24–0.35.
        internal const int AiStatePacked     = 0x0EC; // int   — packed AI state: upper ushort = state ID, lower ushort = substep; transitions on each AI phase change
        internal const int AiSpeedParam      = 0x0F0; // float — state-dependent speed/behavior parameter; changes with AiStatePacked; −1.0 observed at first spawn initialization

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
        // All observed enemies start with X=10.0, Y=20.0, Z=20.0, AggroRange=128.0.
        // During certain attacks (e.g. Captain lunge) the forward axis expands dramatically:
        //   BehaviorRangeX: 10→137.4, BehaviorRangeY/Z: 20→9.6 (narrows to a forward spike).
        // Engine overwrites these at 60fps; a one-time write at floor load has no lasting effect.
        internal const int BehaviorRangeX    = 0x140; // float — resting 10.0; expands on attack (forward reach / close-range threshold)
        internal const int BehaviorRangeY    = 0x144; // float — resting 20.0; narrows during some attacks (lateral range)
        internal const int BehaviorRangeZ    = 0x148; // float — resting 20.0; narrows during some attacks (depth range)
        internal const int AggroRange        = 0x14C; // float — 128.0 for all observed enemies; constant during attacks; likely max aggro/detection radius

        // Three additional float fields after AggroRange. Observed as 0.0 for regular Auntie Medu and
        // non-zero (127.5, 80.0, 15.0) for the same enemy when the mod's miniboss process was active.
        // The mod process may have indirectly triggered the game engine to write these; they are not
        // believed to be mod-written values. Purpose unknown — possibly secondary AI range tiers used
        // by specific behavior scripts (not all enemy species).
        internal const int Unk150            = 0x150; // float — 0.0 for most enemies; 127.5 observed (≈AggroRange) during mod miniboss activation
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
    /// resistances live at the start of each record as individual ushorts, not as packed ints at
    /// the slot's 0x028. Do not assume field offsets match the slot.
    ///
    /// Confirmed from ELF binary analysis 2026-06-05:
    ///   ELF file offset 0x17FC54, stride 0x9C (156 bytes).
    ///   RAM = 0x00100000 + (0x17FC54 − 0x100) = 0x0027FB54.
    ///   EIDs are NOT stored sequentially — use dc_enemies.json TableIndex to locate a record.
    ///   MaxHp is at +0x098 (end of record), not +0x020 as in the live slot.
    /// </summary>
    internal static class EnemySpeciesTable
    {
        // ── Table geometry ────────────────────────────────────────────────────
        internal const int ElfOffset  = 0x17FC54;          // byte offset within SCUS_971.11 file
        // RAM = seg.vaddr + (ElfOffset - seg.file_offset) = 0x00100000 + (0x17FC54 - 0x100) = 0x0027FB54
        // Confirmed from ELF program header: LOAD seg file=0x100..0x1A2480, vaddr=0x00100000
        internal const int TableBase  = 0x0027FB54;        // confirmed RAM address
        internal const int Stride     = 0x9C;              // bytes per record (156)

        /// <summary>RAM address of the template record at the given physical table index (from extracted data).</summary>
        internal static int RecordAddress(int physicalIndex) => TableBase + physicalIndex * Stride;

        /// <summary>RAM address of a specific field within the record at the given physical index.</summary>
        internal static int FieldAddress(int physicalIndex, int fieldOffset) => RecordAddress(physicalIndex) + fieldOffset;

        /// <summary>RAM address of a field for an enemy whose TableIndex is known.</summary>
        internal static int FieldAddress(EnemyDefaults e, int fieldOffset) => RecordAddress(e.TableIndex.Value) + fieldOffset;

        // ── Field offsets within each record ─────────────────────────────────
        // NOTE: these differ from EnemySlotOffsets. The static record starts with
        // six individual resistance ushorts; the slot packs them into three ints at 0x028.

        // Elemental resistances — individual ushorts at record start.
        // Scale (read as signed short): 0=immune, <0=absorbs (heals enemy), 100=neutral, >100=weak.
        // Absorb uses two's-complement encoding: e.g. -50 stored as 0xFFCE=65486u.
        // Only two enemies absorb: IceQueen absorbs ice (-50), eid=70 absorbs fire (-50).
        internal const int Category   = 0x000; // ushort — enemy category (0=dragon,1=undead,2=marine,3=rock,4=plant,5=beast,6=sky,7=metal,8=mimic,9=mage)
        internal const int FireRes    = 0x002; // short  — fire resistance
        internal const int IceRes     = 0x004; // short  — ice resistance
        internal const int ThunderRes = 0x006; // short  — thunder resistance
        internal const int WindRes    = 0x008; // short  — wind resistance
        internal const int HolyRes    = 0x00A; // short  — holy resistance

        // +0x00C: float — enemy-specific float that varies from 2.0 to 45.0; not constant.
        // Likely a base interaction radius or aggro sphere used at spawn. Not the same as EntityScale.
        internal const int Unk00C     = 0x00C; // float  — enemy-specific (e.g. Skeleton=6.0, Werewolf=7.5, Gunny=5.0)

        // +0x010/+0x012: loaded into the live slot's Unk090 (0x090) low/high ushorts at spawn.
        // Low ushort: observed values 0–5; non-zero for enemies with ranged/special AI.
        // High ushort: non-zero only for ranged enemies (Gunny=20, Pirate's Chariot=30); may be max shoot range in world units.
        internal const int Unk010     = 0x010; // ushort — copied to slot Unk090 low  at spawn
        internal const int Unk012     = 0x012; // ushort — copied to slot Unk090 high at spawn; non-zero on ranged enemies

        internal const int Unk014     = 0x014; // ushort — 65535 for most enemies; non-zero for some (values 0,2,3,11); purpose unknown
        internal const int Unk016     = 0x016; // ushort — 65535 for all observed valid enemies

        internal const int Abs        = 0x018; // int    — XP rewarded to the player on kill; written to slot Abs (0x0B0) at spawn
        internal const int MinGoldDrop= 0x01C; // int    — minimum gold dropped on death; written to slot MinGoldDrop (0x034) at spawn
        internal const int DropChance = 0x020; // int    — item drop chance (0–100); written to slot DropChance (0x038) at spawn
        internal const int Unk024     = 0x024; // int    — ranges 0–70; purpose unknown

        internal const int EnemySpeciesId    = 0x028; // ushort — enemy species ID (matches EnemyDefaults.Id)
        // +0x02A: 2 bytes padding (always 0)

        internal const int StealItemId= 0x02C; // ushort — item ID for steal mechanic; 65535 if none
        internal const int StealFlag  = 0x02E; // ushort — 1 if enemy has a steal item, 0 if not

        internal const int ItemResA   = 0x030; // ushort — item resistance A; semantics unconfirmed; scale resembles elemental resistance
        internal const int ItemResB   = 0x032; // ushort — item resistance B

        // +0x034: base melee attack power. Regular enemies: 82–200. Bosses: 65535 (sentinel; use
        // only behavior-script attacks). 0 if enemy has no melee attack (e.g. pure-projectile flyers).
        // +0x036–+0x040: six elemental attack-output multipliers in the same element order as the
        // resistance fields: [fire, ice, thunder, wind, holy, unknown(dark?)]. Scale: 100=neutral,
        // >100=enemy's primary attack element, <100=reduced output of that element.
        // Example: thunder-beast eid=12 → [50,50,120,50,50,50] (strong thunder, weak others).
        // Example: sky eid=60      → [100,150,100,100,100,100] (strong ice or wind — unconfirmed).
        // Bosses (atk=65535) all have these at 0; pure-projectile enemies may have them at 0 too.
        internal const int AttackPower    = 0x034; // ushort — base melee attack power; 82–200 normal; 0 no melee; 65535 boss
        internal const int ElemAtkFire    = 0x036; // ushort — fire    attack multiplier (100=neutral)
        internal const int ElemAtkIce     = 0x038; // ushort — ice     attack multiplier
        internal const int ElemAtkThunder = 0x03A; // ushort — thunder attack multiplier (spike for rock/beast/metal/mimic categories)
        internal const int ElemAtkWind    = 0x03C; // ushort — wind    attack multiplier
        internal const int ElemAtkHoly    = 0x03E; // ushort — holy    attack multiplier
        internal const int ElemAtkDark    = 0x040; // ushort — dark(?) attack multiplier; 20 for some physical types
        internal const int Unk042         = 0x042; // ushort — 0 for all observed valid enemies

        internal const int EntityScale= 0x044; // float  — CONFIRMED entity/collision radius copied to slot at spawn

        // +0x048: 4-char ASCII type code embedded in ELF (e.g. "e06a", "c14a"). Do not write.
        internal const int EnemyCode  = 0x048; // char[4] — primary identifier code

        // +0x04C–+0x087: mostly zero for regular enemies. Multi-form and boss entries store
        // additional variant code strings here (e.g. "c14b"/"c14c" at +0x058/+0x068). Do not write.

        // +0x088: duplicate of EnemyCode; same 4-char value. Do not write.
        internal const int EnemyCodeCopy = 0x088; // char[4] — copy of EnemyCode

        internal const int Unk090     = 0x090; // int    — 0 for all observed valid enemies; purpose unknown
        internal const int Unk094     = 0x094; // int    — 0 for all observed valid enemies; purpose unknown

        internal const int MaxHp      = 0x098; // int    — max HP; copied to slot MaxHp (0x020) at spawn
    }

    /// <summary>
    /// Offsets into the model scale table (base 0x21E18530, stride 0x3510 per slot).
    /// This table is separate from the enemy slot array and holds rendering and bounding data.
    /// All values confirmed from full-slot dumps (DBC fl.12 and Shipwreck).
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

        // +0x020: unknown float; 7.0 for most ground/melee enemies, 14.0 for Gunny, 32.0 for Mask of Prajna.
        // Semantics unconfirmed — x5 write at floor load had no visual or gameplay effect.
        internal const int Unk020 = 0x020;

        // +0x024: unknown float (10.0–32.0); correlates with model height. NOT a visual scale — x5 write had no effect.
        // Semantics unconfirmed.
        internal const int Unk024 = 0x024;

        // +0x028: unknown float; 60.0 for ground enemies, 0.0 for ranged/flying (Gunny, Sam, Mask of Prajna).
        // Inversely paired with +0x020. Semantics unconfirmed — x5 write had no effect.
        internal const int Unk028 = 0x028;

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
        // Array of 9 RAM pointers referencing entries [25]–[33]; sits immediately before the enemy species table.
        internal const int PointerArray = 0x0027FBD4;

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
        internal const int AttackDistance     = 0x3C; // int   — distance scaling applied to the attack
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
    internal static class BehaviorScriptDatabase
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
        // Likely MasterUtan's special vine or slam attack, one flag-bit below KingsCurse.
        internal static readonly BehaviorScript MasterUtanAttack = new BehaviorScript {
            Index=24, ScriptName="e114a_ex",
            BehaviorMode=0, HitboxWidth=1.4f, HitboxHeight=1.7f, HitboxDepth=0f,
            TriggerRange=2.6f, ReachRange=7.0f, SecondaryRange=8.0f,
            DurationFrames=120, AttackDistance=58, BehaviorFlags=8,
            PhaseCount=3, ScriptMode=65535, PackedFlags2=-65535,
            ProjectileSpeed=0f, ProjectileLifetime=0 };

        // ---- pointer-array entries [25]–[33] — directly referenced by BehaviorScriptTable.PointerArray ----

        // Index 25 — "e115a_ex": KingsCurse extended melee attack.
        // "e115" prefix matches KingsCurse (EID=115). BehaviorFlags=0x0010.
        // Likely the claw/arm-grab special that deals high damage at medium range.
        internal static readonly BehaviorScript KingsCurseAttack = new BehaviorScript {
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
        internal static readonly EnemyDefaults SkeletonSoldier = new EnemyDefaults {
            Id=3, TableIndex=1,  Name="Skeleton Soldier", MaxHp=23,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=null, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1080, ModelAnimCount=19, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults MasterJacket = new EnemyDefaults {
            Id=1, TableIndex=0,  Name="Master Jacket",    MaxHp=75,  Abs=5,  MinGoldDrop=7,  DropChance=50,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=80,  ThunderRes=100, WindRes=80,  HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=177, ItemResA=100, ItemResB=80,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1123, ModelAnimCount=20, AttackPower=150, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        internal static readonly EnemyDefaults Statue = new EnemyDefaults {
            Id=5, TableIndex=2,  Name="Statue",           MaxHp=38,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=20,
            ReticleWidth=1.2f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=792,  ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.10; spans both pools
        internal static readonly EnemyDefaults Dasher = new EnemyDefaults {
            Id=6, TableIndex=3,  Name="Dasher",           MaxHp=23,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=1, Unk090B=0,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=148, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        internal static readonly EnemyDefaults CaveBat = new EnemyDefaults {
            Id=60, TableIndex=52, Name="Cave Bat",         MaxHp=12,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=0.8f, ReticleHeight=0.8f, StealItemId=151, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=940,  ModelAnimCount=21, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.6
        internal static readonly EnemyDefaults MimicDBC = new EnemyDefaults {
            Id=35, TableIndex=30, Name="Mimic (Divine Beast Cave)", MaxHp=68, Abs=3, MinGoldDrop=10, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=1, Unk090B=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=920,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.10/fl.14
        internal static readonly EnemyDefaults Ghost = new EnemyDefaults {
            Id=42, TableIndex=36, Name="Ghost",            MaxHp=15,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=3.6f, EntityScaleCopy=3.6f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.1f, ReticleHeight=1.1f, StealItemId=135, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1220, ModelAnimCount=21, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.14
        internal static readonly EnemyDefaults Dragon = new EnemyDefaults {
            Id=59, TableIndex=51, Name="Dragon",           MaxHp=90,  Abs=5,  MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, Unk090A=5, Unk090B=40,
            ReticleWidth=2.9f, ReticleHeight=2.7f, StealItemId=161, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=32.0f, ModelUnk028=60.0f, ModelDataSize=1422, ModelAnimCount=19, AttackPower=85, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults KingMimicDBC = new EnemyDefaults {
            Id=34, TableIndex=29, Name="King Mimic (Divine Beast Cave)", MaxHp=90, Abs=4, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults Rockanoff = new EnemyDefaults {
            Id=77, TableIndex=69, Name="Rockanoff",        MaxHp=30,  Abs=3,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=954,  ModelAnimCount=19, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5
        internal static readonly EnemyDefaults StatueDog = new EnemyDefaults {
            Id=303, TableIndex=97, Name="Statue Dog",      MaxHp=15,  Abs=2,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=160, ItemResA=90,  ItemResB=100,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=667,  ModelAnimCount=12, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Wise Owl Forest (dungeon 1) ───────────────────────────────────────

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.10; spans both pools
        // from static table 2026-06-04
        internal static readonly EnemyDefaults FliFli = new EnemyDefaults {
            Id=8, TableIndex=5, Name="FliFli",            MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=0, Unk090B=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=169, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults CannibalPlant = new EnemyDefaults {
            Id=11, TableIndex=8, Name="Cannibal Plant",   MaxHp=60,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=167, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=21.0f, ModelUnk028=60.0f, ModelDataSize=474,  ModelAnimCount=7, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.7/fl.14; spans both pools
        internal static readonly EnemyDefaults Sunday = new EnemyDefaults {
            Id=14, TableIndex=10, Name="Sunday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=170, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1454, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.7
        internal static readonly EnemyDefaults Monday = new EnemyDefaults {
            Id=15, TableIndex=11, Name="Monday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.7
        internal static readonly EnemyDefaults Tuesday = new EnemyDefaults {
            Id=16, TableIndex=12, Name="Tuesday",          MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1427, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.7
        internal static readonly EnemyDefaults Wednesday = new EnemyDefaults {
            Id=17, TableIndex=13, Name="Wednesday",        MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; name follows day-of-week series (Sun=14…Fri=19); stats match confirmed day mages exactly; needs dump confirmation
        internal static readonly EnemyDefaults Thursday = new EnemyDefaults {
            Id=18, TableIndex=14, Name="Thursday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.5
        internal static readonly EnemyDefaults Friday = new EnemyDefaults {
            Id=19, TableIndex=15, Name="Friday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=148, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; name follows day-of-week series; stats match confirmed day mages exactly; needs dump confirmation
        internal static readonly EnemyDefaults Saturday = new EnemyDefaults {
            Id=20, TableIndex=16, Name="Saturday",         MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            StealItemId=148, ItemResA=100, ItemResB=70, AttackPower=145, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.7/fl.14/fl.16; spans both pools
        internal static readonly EnemyDefaults WitchIllza = new EnemyDefaults {
            Id=22, TableIndex=18, Name="Witch Illza",      MaxHp=120, Abs=3,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=90,  IceRes=90,  ThunderRes=90,  WindRes=90,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=169, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=868,  ModelAnimCount=18, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.5
        internal static readonly EnemyDefaults MimicWOF = new EnemyDefaults {
            Id=79, TableIndex=71, Name="Mimic (Wise Owl Forest)", MaxHp=90,  Abs=3,  MinGoldDrop=6,  DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=2, Unk090B=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=914,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.5/fl.10; spans both pools
        internal static readonly EnemyDefaults HaleyHoley = new EnemyDefaults {
            Id=305, TableIndex=99, Name="Haley Holey",     MaxHp=50,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=140, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.0f, ReticleHeight=1.1f, StealItemId=186, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1046, ModelAnimCount=19, AttackPower=189, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.14/fl.16
        internal static readonly EnemyDefaults Werewolf = new EnemyDefaults {
            Id=7, TableIndex=4,  Name="Werewolf",         MaxHp=180, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, Unk090A=5, Unk090B=0,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=174, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1111, ModelAnimCount=19, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.10
        internal static readonly EnemyDefaults Hornet = new EnemyDefaults {
            Id=9, TableIndex=6,  Name="Hornet",           MaxHp=60,  Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=120, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.14/fl.16
        internal static readonly EnemyDefaults Halloween = new EnemyDefaults {
            Id=10, TableIndex=7, Name="Halloween",        MaxHp=150, Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=168, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=850,  ModelAnimCount=18, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, WOF game fl.10/fl.16
        internal static readonly EnemyDefaults EarthDigger = new EnemyDefaults {
            Id=12, TableIndex=9, Name="Earth Digger",     MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=188, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=936,  ModelAnimCount=19, AttackPower=197, ElemAtkFire=50, ElemAtkIce=50, ElemAtkThunder=120, ElemAtkWind=50, ElemAtkHoly=50, ElemAtkDark=50 };

        // from static table 2026-06-04; stronger WitchIllza variant; needs dump confirmation
        internal static readonly EnemyDefaults WitchHellza = new EnemyDefaults {
            Id=21, TableIndex=17, Name="Witch Hellza",     MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=70,  IceRes=70,  ThunderRes=70,  WindRes=70,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=0, Unk090B=0,
            StealItemId=169, ItemResA=85, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; same model/stats tier as KingMimicDBC but different code; needs dump confirmation
        internal static readonly EnemyDefaults KingMimicWOF = new EnemyDefaults {
            Id=78, TableIndex=70, Name="King Mimic (Wise Owl Forest)", MaxHp=150, Abs=10, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=5, Unk090B=10,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Shipwreck (dungeon 2) ──────────────────────────────────────────────

        // confirmed from clean dump 2026-05-30, SW game fl.8/fl.10; spans both pools
        internal static readonly EnemyDefaults Captain = new EnemyDefaults {
            Id=27, TableIndex=23, Name="Captain",          MaxHp=225, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=177, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=873,  ModelAnimCount=19, AttackPower=227, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.10
        internal static readonly EnemyDefaults PiratesChariot = new EnemyDefaults {
            Id=25, TableIndex=21, Name="Pirate's Chariot", MaxHp=270, Abs=8,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=120, IceRes=80,  ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=5, Unk090B=30,
            ReticleWidth=1.9f, ReticleHeight=1.8f, StealItemId=159, ItemResA=95,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=835,  ModelAnimCount=19, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.10; Unk020=14.0/Unk028=0.0 — ranged gun enemy
        internal static readonly EnemyDefaults Gunny = new EnemyDefaults {
            Id=23, TableIndex=19, Name="Gunny",            MaxHp=250, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=153, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=20.0f, ModelUnk028=0.0f,  ModelDataSize=1270, ModelAnimCount=19, AttackPower=193, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.3; StealItemId=null (0xFFFF in memory)
        internal static readonly EnemyDefaults CursedRose = new EnemyDefaults {
            Id=68, TableIndex=60, Name="Cursed Rose",      MaxHp=225, Abs=4,  MinGoldDrop=6,  DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=476,  ModelAnimCount=7, AttackPower=146, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        internal static readonly EnemyDefaults Gyon = new EnemyDefaults {
            Id=24, TableIndex=20, Name="Gyon",             MaxHp=225, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=134, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=849,  ModelAnimCount=16, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.17
        // Unk150/154/158 (0x150/154/158) read as 0 for regular Auntie Medu; observed non-zero (127.5/80.0/15.0) when the mod's miniboss process was active on this enemy species.
        internal static readonly EnemyDefaults AuntieMedu = new EnemyDefaults {
            Id=26, TableIndex=22, Name="Auntie Medu",      MaxHp=300, Abs=10, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=166, ItemResA=100, ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=944,  ModelAnimCount=19, AttackPower=245, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.3
        internal static readonly EnemyDefaults Corcea = new EnemyDefaults {
            Id=28, TableIndex=24, Name="Corcea",           MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=140, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=152, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=871,  ModelAnimCount=19, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17
        internal static readonly EnemyDefaults MaskOfPrajna = new EnemyDefaults {
            Id=75, TableIndex=67, Name="Mask of Prajna",   MaxHp=375, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=5, Unk090B=10,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=80,  ItemResB=70,
            ModelUnk020=32.0f, ModelUnk024=26.0f, ModelUnk028=0.0f,  ModelDataSize=1398, ModelAnimCount=19, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17;
        internal static readonly EnemyDefaults Sam = new EnemyDefaults {
            Id=85, TableIndex=77, Name="Sam",              MaxHp=180, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.6f, StealItemId=162, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=19.0f, ModelUnk028=0.0f,  ModelDataSize=871,  ModelAnimCount=19, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        internal static readonly EnemyDefaults MimicSW = new EnemyDefaults {
            Id=81, TableIndex=73, Name="Mimic (Shipwreck)", MaxHp=150, Abs=4, MinGoldDrop=6, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SW game fl.16; same model/anim data as KingMimicDBC but higher stats and different Unk090
        internal static readonly EnemyDefaults KingMimicSW = new EnemyDefaults {
            Id=80, TableIndex=72, Name="King Mimic (Shipwreck)", MaxHp=300, Abs=15, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Sun and Moon Temple (dungeon 3) ──────────────────────────────────────

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools; unk020=9.0 (higher than most melee enemies); no steal (0xFFFF in memory)
        internal static readonly EnemyDefaults Mummy = new EnemyDefaults {
            Id=50, TableIndex=44, Name="Mummy",            MaxHp=150, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Undead, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=9.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1029, ModelAnimCount=19, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools
        internal static readonly EnemyDefaults Phantom = new EnemyDefaults {
            Id=58, TableIndex=50, Name="Phantom",          MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=125, ThunderRes=100, WindRes=125, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SM game fl.2–7; Unk090A=8 (large, like Golem); steal=159
        internal static readonly EnemyDefaults BomberHead = new EnemyDefaults {
            Id=49, TableIndex=43, Name="Bomber Head",      MaxHp=180, Abs=4,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=75,  ThunderRes=125, WindRes=100, HolyRes=75,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=8, Unk090B=20,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=159, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1663, ModelAnimCount=23, AttackPower=159, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SM game fl.3–7; same model as MimicSW (dataSize=918, animCount=20)
        internal static readonly EnemyDefaults MimicSMT = new EnemyDefaults {
            Id=37, TableIndex=32, Name="Mimic (Sun & Moon Temple)", MaxHp=270, Abs=6, MinGoldDrop=12, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SM game fl.3–7; scale=14.0 (large body); Unk090A=8
        internal static readonly EnemyDefaults Golem = new EnemyDefaults {
            Id=30, TableIndex=25, Name="Golem",            MaxHp=375, Abs=4,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=8, Unk090B=0,
            ReticleWidth=2.4f, ReticleHeight=2.4f, StealItemId=177, ItemResA=100, ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=33.0f, ModelUnk028=60.0f, ModelDataSize=1071, ModelAnimCount=18, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // confirmed from dump 2026-05-31, SM game fl.7; unk020=14.0 (ranged/large class); unk028=100.0 (highest observed — aquatic/crab movement class?)
        internal static readonly EnemyDefaults CrabbyHermit = new EnemyDefaults {
            Id=71, TableIndex=63, Name="Crabby Hermit",    MaxHp=300, Abs=4,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.9f, ReticleHeight=1.9f, StealItemId=166, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=22.0f, ModelUnk028=100.0f, ModelDataSize=1612, ModelAnimCount=22, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; King Mimic for SM dungeon; needs dump confirmation
        internal static readonly EnemyDefaults KingMimicSMT = new EnemyDefaults {
            Id=36, TableIndex=31, Name="King Mimic (Sun & Moon Temple)", MaxHp=525, Abs=15, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=5, Unk090B=20,
            StealItemId=174, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; needs dump confirmation
        internal static readonly EnemyDefaults MrBlare = new EnemyDefaults {
            Id=31, TableIndex=26, Name="Mr. Blare",        MaxHp=225, Abs=5,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=0,   IceRes=170, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=10,
            StealItemId=161, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; needs dump confirmation
        internal static readonly EnemyDefaults Dune = new EnemyDefaults {
            Id=32, TableIndex=27, Name="Dune",             MaxHp=525, Abs=10, MinGoldDrop=18, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=120, HolyRes=100,
            EntityScale=11.0f, EntityScaleCopy=11.0f, Unk090A=0, Unk090B=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; needs dump confirmation
        internal static readonly EnemyDefaults Titan = new EnemyDefaults {
            Id=33, TableIndex=28, Name="Titan",            MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=10, Unk090B=50,
            StealItemId=177, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; playing-card mage set; needs dump confirmation for dungeon assignment
        internal static readonly EnemyDefaults Heart = new EnemyDefaults {
            Id=44, TableIndex=38, Name="Heart",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=0,
            StealItemId=150, ItemResA=80, ItemResB=50, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Club = new EnemyDefaults {
            Id=45, TableIndex=39, Name="Club",             MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=100, ThunderRes=100, WindRes=50,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=0,
            StealItemId=147, ItemResA=80, ItemResB=50, AttackPower=134, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Diamond = new EnemyDefaults {
            Id=46, TableIndex=40, Name="Diamond",          MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=50,  WindRes=150, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=0,
            StealItemId=151, ItemResA=80, ItemResB=50, AttackPower=135, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Spade = new EnemyDefaults {
            Id=47, TableIndex=41, Name="Spade",            MaxHp=525, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=0,
            StealItemId=152, ItemResA=80, ItemResB=50, AttackPower=132, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // fire=50/ice=50/thu=50/win=50 (resistant to all), holy=150; all-element-resistant mage
        internal static readonly EnemyDefaults Joker = new EnemyDefaults {
            Id=48, TableIndex=42, Name="Joker",            MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=50,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=150,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=0,
            StealItemId=149, ItemResA=50, ItemResB=10, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Moon Sea (dungeon 4) ──────────────────────────────────────────────────
        // All entries from static table 2026-06-04; pool assignments estimated; needs dump confirmation.

        internal static readonly EnemyDefaults KingMimicMS = new EnemyDefaults {
            Id=38, TableIndex=33, Name="King Mimic (Moon Sea)", MaxHp=600, Abs=12, MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=8, Unk090B=30,
            StealItemId=176, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults MimicMS = new EnemyDefaults {
            Id=39, TableIndex=34, Name="Mimic (Moon Sea)",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=8, Unk090B=30,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Lich = new EnemyDefaults {
            Id=51, TableIndex=45, Name="Lich",             MaxHp=300, Abs=12, MinGoldDrop=15, DropChance=80,
            Category=EnemyCategory.Undead, FireRes=20,  IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=160,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=5, Unk090B=0,
            StealItemId=176, ItemResA=80, ItemResB=30, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults CurseDancer = new EnemyDefaults {
            Id=52, TableIndex=46, Name="Curse Dancer",     MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            StealItemId=166, ItemResA=100, ItemResB=70, AttackPower=133, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults LivingArmor = new EnemyDefaults {
            Id=55, TableIndex=47, Name="Living Armor",     MaxHp=450, Abs=6,  MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Rock, FireRes=100, IceRes=100, ThunderRes=100, WindRes=80,  HolyRes=80,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=10, Unk090B=50,
            StealItemId=null, ItemResA=100, ItemResB=50, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults WhiteFang = new EnemyDefaults {
            Id=56, TableIndex=48, Name="White Fang",       MaxHp=525, Abs=10, MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, Unk090A=0, Unk090B=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=155, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults MoonBug = new EnemyDefaults {
            Id=57, TableIndex=49, Name="Moon Bug",         MaxHp=450, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=50,  IceRes=120, ThunderRes=150, WindRes=50,  HolyRes=100,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=8, Unk090B=40,
            StealItemId=159, ItemResA=90, ItemResB=70, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Gallery of Time (dungeon 5) ───────────────────────────────────────────
        // All entries from static table 2026-06-04; pool assignments estimated; needs dump confirmation.

        internal static readonly EnemyDefaults Arthur = new EnemyDefaults {
            Id=40, TableIndex=35, Name="Arthur",           MaxHp=600, Abs=15, MinGoldDrop=15, DropChance=30,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=150, WindRes=80,  HolyRes=80,
            EntityScale=9.0f, EntityScaleCopy=9.0f, Unk090A=10, Unk090B=60,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Alexander = new EnemyDefaults {
            Id=43, TableIndex=37, Name="Alexander",        MaxHp=675, Abs=15, MinGoldDrop=17, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=150, IceRes=130, ThunderRes=100, WindRes=120, HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=10, Unk090B=50,
            StealItemId=164, ItemResA=100, ItemResB=70, AttackPower=81, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // shares scale=3.0 with CaveBat
        internal static readonly EnemyDefaults EvilBat = new EnemyDefaults {
            Id=61, TableIndex=53, Name="Evil Bat",         MaxHp=150, Abs=4,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, Unk090A=0, Unk090B=0,
            StealItemId=151, ItemResA=100, ItemResB=70, AttackPower=149, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults HellPockle = new EnemyDefaults {
            Id=62, TableIndex=54, Name="Hell Pockle",      MaxHp=270, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=2, Unk090B=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=148, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults RashDasher = new EnemyDefaults {
            Id=63, TableIndex=55, Name="Rash Dasher",      MaxHp=600, Abs=6,  MinGoldDrop=12, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=50,  IceRes=150, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=2, Unk090B=10,
            StealItemId=149, ItemResA=100, ItemResB=70, AttackPower=93, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults SteelGiant = new EnemyDefaults {
            Id=64, TableIndex=56, Name="Steel Giant",      MaxHp=750, Abs=12, MinGoldDrop=15, DropChance=50,
            Category=EnemyCategory.Metal, FireRes=80,  IceRes=100, ThunderRes=125, WindRes=80,  HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=10, Unk090B=50,
            StealItemId=177, ItemResA=95, ItemResB=50, AttackPower=154, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults Blizzard = new EnemyDefaults {
            Id=65, TableIndex=57, Name="Blizzard",         MaxHp=750, Abs=8,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=140, WindRes=140, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=5, Unk090B=0,
            StealItemId=162, ItemResA=100, ItemResB=50, AttackPower=82, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults MoonDigger = new EnemyDefaults {
            Id=66, TableIndex=58, Name="Moon Digger",      MaxHp=420, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=125, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=2, Unk090B=0,
            StealItemId=187, ItemResA=100, ItemResB=70, AttackPower=197, ElemAtkFire=45, ElemAtkIce=45, ElemAtkThunder=130, ElemAtkWind=45, ElemAtkHoly=45, ElemAtkDark=45 };

        internal static readonly EnemyDefaults DarkFlower = new EnemyDefaults {
            Id=67, TableIndex=59, Name="Dark Flower",      MaxHp=300, Abs=5,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Plant, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=5, Unk090B=0,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=147, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // thunder=0 (immune)
        internal static readonly EnemyDefaults Billy = new EnemyDefaults {
            Id=69, TableIndex=61, Name="Billy",            MaxHp=300, Abs=6,  MinGoldDrop=10, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=0,   WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=10,
            StealItemId=163, ItemResA=100, ItemResB=70, AttackPower=83, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // fire=65486 (0xFFCE — effectively absorbs fire damage)
        internal static readonly EnemyDefaults Vulcan = new EnemyDefaults {
            Id=70, TableIndex=62, Name="Vulcan",           MaxHp=480, Abs=12, MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=65486, IceRes=180, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=5, Unk090B=40,
            StealItemId=81, ItemResA=100, ItemResB=70, AttackPower=160, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults SpaceGyon = new EnemyDefaults {
            Id=72, TableIndex=64, Name="Space Gyon",       MaxHp=525, Abs=5,  MinGoldDrop=4,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=75,  IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            StealItemId=153, ItemResA=100, ItemResB=70, AttackPower=226, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults BlueDragon = new EnemyDefaults {
            Id=73, TableIndex=65, Name="Blue Dragon",      MaxHp=600, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=125, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, Unk090A=5, Unk090B=30,
            StealItemId=162, ItemResA=80, ItemResB=50, AttackPower=91, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults BlackDragon = new EnemyDefaults {
            Id=74, TableIndex=66, Name="Black Dragon",     MaxHp=900, Abs=20, MinGoldDrop=22, DropChance=50,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=130,
            EntityScale=17.5f, EntityScaleCopy=17.5f, Unk090A=10, Unk090B=60,
            StealItemId=154, ItemResA=50, ItemResB=40, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults CrescentBaron = new EnemyDefaults {
            Id=76, TableIndex=68, Name="Crescent Baron",   MaxHp=450, Abs=12, MinGoldDrop=18, DropChance=50,
            Category=EnemyCategory.Sky, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=5, Unk090B=10,
            StealItemId=null, ItemResA=80, ItemResB=70, AttackPower=170, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults KingMimicGoT = new EnemyDefaults {
            Id=82, TableIndex=74, Name="King Mimic (Gallery of Time)", MaxHp=675, Abs=18, MinGoldDrop=25, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=5, Unk090B=30,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // code=kori (Japanese for "ice" — may be official name)
        internal static readonly EnemyDefaults MimicGoT = new EnemyDefaults {
            Id=83, TableIndex=75, Name="Mimic (Gallery of Time)", MaxHp=450, Abs=6,  MinGoldDrop=20, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=20,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // listed as non-drop in Enemies.cs
        internal static readonly EnemyDefaults Gol = new EnemyDefaults {
            Id=90, TableIndex=94, Name="Gol",              MaxHp=600, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=120, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=8, Unk090B=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // listed as non-drop in Enemies.cs
        internal static readonly EnemyDefaults Sil = new EnemyDefaults {
            Id=91, TableIndex=95, Name="Sil",              MaxHp=500, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Rock, FireRes=90,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=10, Unk090B=0,
            StealItemId=177, ItemResA=100, ItemResB=50, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // ── Overseas (USA/PAL-exclusive enemies) ──────────────────────────────────
        // Overseas enemies appear in the USA/PAL version of Dark Cloud but are absent
        // from the Japanese release. Pool assignments match the Japanese versions of the
        // same dungeons (DBC area).

        // confirmed from clean dump 2026-05-30, DBC game fl.5 — Yammich (overseas DBC fl.1-7)
        internal static readonly EnemyDefaults Yammich = new EnemyDefaults {
            Id=301, TableIndex=96, Name="Yammich",         MaxHp=13,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=70,  HolyRes=130,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=0, Unk090B=1,
            StealItemId=160, ItemResA=90, ItemResB=100, AttackPower=92, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; needs dump confirmation
        internal static readonly EnemyDefaults Opar = new EnemyDefaults {
            Id=304, TableIndex=98, Name="Opar",            MaxHp=28,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Marine, FireRes=100, IceRes=60,  ThunderRes=130, WindRes=100, HolyRes=100,
            EntityScale=15.0f, EntityScaleCopy=15.0f, Unk090A=1, Unk090B=2,
            StealItemId=227, ItemResA=90, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; needs dump confirmation
        internal static readonly EnemyDefaults KingPrickly = new EnemyDefaults {
            Id=306, TableIndex=100, Name="King Prickly",    MaxHp=63,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Category=EnemyCategory.Beast, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=10,
            StealItemId=null, ItemResA=100, ItemResB=70, AttackPower=199, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // from static table 2026-06-04; appears in overseas late dungeons; needs dump confirmation
        internal static readonly EnemyDefaults Nikapous = new EnemyDefaults {
            Id=308, TableIndex=112, Name="Nikapous",        MaxHp=2350, Abs=15, MinGoldDrop=8,  DropChance=30,
            Category=EnemyCategory.Mage, FireRes=50,  IceRes=100, ThunderRes=100, WindRes=125, HolyRes=125,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=10, Unk090B=10,
            StealItemId=133, ItemResA=100, ItemResB=70, AttackPower=84, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Demon Shaft (dungeon 6) ───────────────────────────────────────────────
        // Demon Shaft enemies reuse base-game eids with new model codes and scaled stats.
        // Each eid appears multiple times in the static table at u090a tiers: 15, 20, 23, 30.
        // The entries below capture the FIRST occurrence (lowest tier) as the EnemyDefaults base.
        // The Gemrons (311-315) are Demon Shaft unique enemies — each appears at a single tier.

        // Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults MimicDS = new EnemyDefaults {
            Id=309, TableIndex=130, Name="Mimic (Demon Shaft)",     MaxHp=3500, Abs=10, MinGoldDrop=26, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=15, Unk090B=10,
            StealItemId=177, ItemResA=90, ItemResB=50, AttackPower=235, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // King Mimic (Demon Shaft) — tier 1
        internal static readonly EnemyDefaults KingMimicDS = new EnemyDefaults {
            Id=310, TableIndex=131, Name="King Mimic (Demon Shaft)", MaxHp=5000, Abs=20, MinGoldDrop=35, DropChance=80,
            Category=EnemyCategory.Mimic, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=15, Unk090B=50,
            StealItemId=175, ItemResA=90, ItemResB=50, AttackPower=181, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=150, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=20 };

        // Gemron (Fire) — tier 1
        internal static readonly EnemyDefaults GemronFire = new EnemyDefaults {
            Id=311, TableIndex=111, Name="Gemron (Fire)",   MaxHp=2500, Abs=15, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=0,   IceRes=150, ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=10, Unk090B=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=161, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // Gemron (Ice) — tier 2
        internal static readonly EnemyDefaults GemronIce = new EnemyDefaults {
            Id=312, TableIndex=121, Name="Gemron (Ice)",    MaxHp=4000, Abs=20, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=150, IceRes=0,   ThunderRes=30,  WindRes=30,  HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=15, Unk090B=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=162, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // Gemron (Thunder) — tier 3
        internal static readonly EnemyDefaults GemronThunder = new EnemyDefaults {
            Id=313, TableIndex=132, Name="Gemron (Thunder)", MaxHp=5500, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=30,  IceRes=30,  ThunderRes=0,   WindRes=150, HolyRes=30,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=20, Unk090B=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=163, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // Gemron (Wind) — tier 4
        internal static readonly EnemyDefaults GemronWind = new EnemyDefaults {
            Id=314, TableIndex=143, Name="Gemron (Wind)",   MaxHp=8000, Abs=30, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=100, IceRes=100, ThunderRes=140, WindRes=0,   HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=23, Unk090B=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=164, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // Gemron (Holy) — tier 5
        internal static readonly EnemyDefaults GemronHoly = new EnemyDefaults {
            Id=315, TableIndex=154, Name="Gemron (Holy)",   MaxHp=12500, Abs=35, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Dragon, FireRes=50,  IceRes=50,  ThunderRes=50,  WindRes=50,  HolyRes=0,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=30, Unk090B=10,
            StealItemId=null, ItemResA=70, ItemResB=60, AttackPower=165, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults BishopQ = new EnemyDefaults {
            Id=316, TableIndex=133, Name="Bishop Q",        MaxHp=6000, Abs=25, MinGoldDrop=20, DropChance=30,
            Category=EnemyCategory.Mage, FireRes=40,  IceRes=40,  ThunderRes=40,  WindRes=40,  HolyRes=140,
            EntityScale=11.0f, EntityScaleCopy=11.0f, Unk090A=20, Unk090B=10,
            StealItemId=null, ItemResA=50, ItemResB=50, AttackPower=94, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // code=c23a (boss model prefix);
        internal static readonly EnemyDefaults Gacious = new EnemyDefaults {
            Id=317, TableIndex=105, Name="Gacious",         MaxHp=1800, Abs=5,  MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=70,  IceRes=100, ThunderRes=100, WindRes=100, HolyRes=140,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=8, Unk090B=0,
            StealItemId=null, ItemResA=100, ItemResB=90, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults SilverGear = new EnemyDefaults {
            Id=318, TableIndex=144, Name="Silver Gear",     MaxHp=2500, Abs=30, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=30,  IceRes=30,  ThunderRes=30,  WindRes=30,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, Unk090A=23, Unk090B=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=190, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        internal static readonly EnemyDefaults HornHead = new EnemyDefaults {
            Id=319, TableIndex=122, Name="Horn Head",       MaxHp=2500, Abs=20, MinGoldDrop=5,  DropChance=30,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=20,  ThunderRes=20,  WindRes=20,  HolyRes=150,
            EntityScale=10.0f, EntityScaleCopy=10.0f, Unk090A=15, Unk090B=10,
            StealItemId=null, ItemResA=100, ItemResB=100, AttackPower=186, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=100 };

        // ── Bosses ─────────────────────────────────────────────────────────────
        // All bosses have MinGoldDrop=0, DropChance=0, StealItemId=65535 (can't steal).
        // TableIndex values confirmed from ISO extraction 2026-06-04.

        // SW boss — projectile/summon entity of Ice Queen; not a standalone fight
        internal static readonly EnemyDefaults IceArrow = new EnemyDefaults {
            Id=84, TableIndex=76, Name="Ice Arrow",        MaxHp=100,   Abs=17, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=2.0f,  EntityScaleCopy=2.0f,  Unk090A=5,  Unk090B=0,
            StealItemId=65535, ItemResA=70, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=100, ElemAtkHoly=0, ElemAtkDark=0 };

        // DBC boss
        internal static readonly EnemyDefaults Dran = new EnemyDefaults {
            Id=112, TableIndex=78, Name="Dran",            MaxHp=250,   Abs=10, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=150, ThunderRes=100, WindRes=100, HolyRes=50,
            EntityScale=45.0f, EntityScaleCopy=45.0f, Unk090A=10, Unk090B=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // SW boss — ice=65486 (0xFFCE, -50 as int16) = fire-absorbing (same encoding as Vulcan's fire)
        internal static readonly EnemyDefaults IceQueen = new EnemyDefaults {
            Id=113, TableIndex=80, Name="Ice Queen",       MaxHp=700,   Abs=30, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=150, IceRes=65486, ThunderRes=80, WindRes=80, HolyRes=120,
            EntityScale=13.0f, EntityScaleCopy=13.0f, Unk090A=10, Unk090B=0,
            StealItemId=65535, ItemResA=40, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // WOF boss
        internal static readonly EnemyDefaults MasterUtan = new EnemyDefaults {
            Id=114, TableIndex=79, Name="Master Utan",     MaxHp=700,   Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=35.0f, EntityScaleCopy=35.0f, Unk090A=12, Unk090B=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // SMT boss
        internal static readonly EnemyDefaults KingsCurse = new EnemyDefaults {
            Id=115, TableIndex=81, Name="King's Curse",    MaxHp=2000,  Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=110, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=125,
            EntityScale=6.0f,  EntityScaleCopy=6.0f,  Unk090A=10, Unk090B=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // MS boss
        internal static readonly EnemyDefaults MinotaurJoe = new EnemyDefaults {
            Id=116, TableIndex=83, Name="Minotaur Joe",    MaxHp=2000,  Abs=50, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Beast, FireRes=100, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=25.0f, EntityScaleCopy=25.0f, Unk090A=12, Unk090B=40,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // GoT boss (final)
        internal static readonly EnemyDefaults DarkGenie = new EnemyDefaults {
            Id=117, TableIndex=84, Name="Dark Genie",      MaxHp=2000,  Abs=60, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=25, Unk090B=30,
            StealItemId=65535, ItemResA=30, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // Dark Genie second form — not named in Enemies.cs; code=c17c; same resistance profile as hands
        internal static readonly EnemyDefaults DarkGenieForm2 = new EnemyDefaults {
            Id=118, TableIndex=85, Name="Dark Genie (form 2)", MaxHp=3200, Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  Unk090A=0,  Unk090B=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // Dark Genie hands
        internal static readonly EnemyDefaults RightHand = new EnemyDefaults {
            Id=119, TableIndex=86, Name="Right Hand",      MaxHp=3200,  Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=8.0f,  EntityScaleCopy=8.0f,  Unk090A=0,  Unk090B=20,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // Left Hand has no EID=120 record in the table; its HP (90) is stored in Right Hand's u98
        // via the off-by-one. The game spawns it using an anonymous EID=0 record at idx=87.
        internal static readonly EnemyDefaults LeftHand = new EnemyDefaults {
            Id=120, TableIndex=87, Name="Left Hand",       MaxHp=90,    Abs=20, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f,  EntityScaleCopy=5.0f,  Unk090A=0,  Unk090B=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };  // idx=87 is the anonymous EID=0 record

        internal static readonly EnemyDefaults WineKeg = new EnemyDefaults {
            Id=121, TableIndex=91, Name="Wine Keg",        MaxHp=80,    Abs=0,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Mage, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f,  EntityScaleCopy=7.0f,  Unk090A=0,  Unk090B=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // Unlisted phase entity — code=c16a; not in Enemies.cs; suspected SW/SM scripted phase
        internal static readonly EnemyDefaults UnknownPhase100 = new EnemyDefaults {
            Id=100, TableIndex=82, Name="(phase entity 100)", MaxHp=1000, Abs=40, MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Undead, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=4.0f,  EntityScaleCopy=4.0f,  Unk090A=10, Unk090B=0,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=0, ElemAtkIce=0, ElemAtkThunder=0, ElemAtkWind=0, ElemAtkHoly=0, ElemAtkDark=0 };

        // DS boss — confirmed from EnemySpeciesTable scan 2026-06-05: tbl_165 is BlackKnight (id=221, hp=50000, BOSS, code=c22a).
        // tbl_166 is a garbage/padding row (hp=0, empty code) — not a valid spawn entry.
        // tbl_164 is KingMimicDS boss tier (id=310, hp=40000, code=c21a); used in DS floor 100 pool alongside BlackKnight.
        internal static readonly EnemyDefaults BlackKnight = new EnemyDefaults {
            Id=221, TableIndex=165, Name="Black Knight",    MaxHp=50000, Abs=5,  MinGoldDrop=0, DropChance=0,
            Category=EnemyCategory.Metal, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=8, Unk090B=100,
            StealItemId=65535, ItemResA=50, ItemResB=0, AttackPower=65535, ElemAtkFire=100, ElemAtkIce=100, ElemAtkThunder=100, ElemAtkWind=100, ElemAtkHoly=100, ElemAtkDark=50 };

        // ── Boss companion / phase entities ──────────────────────────────────────
        // All have id=0 (no species entity), AttackPower=65535 (boss sentinel).
        // Not added to Defaults dictionary (id=0 is not a valid lookup key).

        // Dark Genie fight companions: code=c17_ (DG phase model), unnamed
        internal static readonly EnemyDefaults DGComp88 = new EnemyDefaults {
            Id=0, TableIndex=88, Name="(DG companion c17_)", MaxHp=0, AttackPower=65535 };
        internal static readonly EnemyDefaults DGComp89 = new EnemyDefaults {
            Id=0, TableIndex=89, Name="(DG companion c17_)", MaxHp=0, AttackPower=65535 };
        // code=e85a
        internal static readonly EnemyDefaults DGComp90 = new EnemyDefaults {
            Id=0, TableIndex=90, Name="(DG companion e85a)", MaxHp=0, AttackPower=65535 };
        // SW boss companion: code=c17_
        internal static readonly EnemyDefaults SWComp92 = new EnemyDefaults {
            Id=0, TableIndex=92, Name="(SW boss companion c17_)", MaxHp=0, AttackPower=65535 };
        // code=e90a
        internal static readonly EnemyDefaults DGComp93 = new EnemyDefaults {
            Id=0, TableIndex=93, Name="(DG companion e90a)", MaxHp=0, AttackPower=65535 };

        // IceQueen (SW floor 18) fight companions — all id=0, boss sentinels
        internal static readonly EnemyDefaults IQComp101 = new EnemyDefaults {
            Id=0, TableIndex=101, Name="(IQ companion kori)", MaxHp=0, AttackPower=65535 };
        internal static readonly EnemyDefaults IQComp102 = new EnemyDefaults {
            Id=0, TableIndex=102, Name="(IQ companion i_me)", MaxHp=0, AttackPower=65535 };
        internal static readonly EnemyDefaults IQComp103 = new EnemyDefaults {
            Id=0, TableIndex=103, Name="(IQ companion i_ta)", MaxHp=0, AttackPower=65535 };
        internal static readonly EnemyDefaults IQComp104 = new EnemyDefaults {
            Id=0, TableIndex=104, Name="(IQ companion e124)", MaxHp=0, AttackPower=65535 };

        // ── Demon Shaft enhanced tier variants ────────────────────────────────────
        // These reuse base-game species IDs with new model codes and scaled stats.
        // Not added to Defaults dictionary to avoid overwriting base-game entries.
        // Stats from EnemySpeciesTable scan 2026-06-05.

        // GemronFire group (floors 1-20): tbl_113–120
        // e126
        internal static readonly EnemyDefaults WhiteFangEnhanced = new EnemyDefaults {
            Id=56,  TableIndex=113, Name="White Fang (Enhanced)",  MaxHp=2900,  AttackPower=155, Category=EnemyCategory.Beast };
        // e127
        internal static readonly EnemyDefaults ArthurEnhanced = new EnemyDefaults {
            Id=40,  TableIndex=114, Name="Arthur (Enhanced)",  MaxHp=1500,  AttackPower=92,  Category=EnemyCategory.Metal };
        // e128; BOSS sentinel (atk=65535)
        internal static readonly EnemyDefaults SilEnhanced = new EnemyDefaults {
            Id=91,  TableIndex=115, Name="Sil (Enhanced)",   MaxHp=1800,  AttackPower=65535, Category=EnemyCategory.Rock };
        // e129
        internal static readonly EnemyDefaults HalloweenEnhanced = new EnemyDefaults {
            Id=10,  TableIndex=116, Name="Halloween (Enhanced)",  MaxHp=2000,  AttackPower=148, Category=EnemyCategory.Plant };
        // e130
        internal static readonly EnemyDefaults MasterJacketEnhanced = new EnemyDefaults {
            Id=1,   TableIndex=117, Name="Master Jacket (Enhanced)",  MaxHp=2400,  AttackPower=150, Category=EnemyCategory.Undead };
        // e131
        internal static readonly EnemyDefaults VulcanEnhanced = new EnemyDefaults {
            Id=70,  TableIndex=118, Name="Vulcan (Enhanced)",   MaxHp=1500,  AttackPower=160, Category=EnemyCategory.Rock };
        // e132
        internal static readonly EnemyDefaults MummyEnhanced = new EnemyDefaults {
            Id=50,  TableIndex=119, Name="Mummy (Enhanced)", MaxHp=1750,  AttackPower=133, Category=EnemyCategory.Undead };
        // e112
        internal static readonly EnemyDefaults DiamondEnhanced = new EnemyDefaults {
            Id=46,  TableIndex=120, Name="Diamond (Enhanced)",   MaxHp=4000,  AttackPower=135, Category=EnemyCategory.Mage };

        // GemronIce group (floors 21-40): tbl_123–129
        // e134
        internal static readonly EnemyDefaults AuntieMeduEnhanced = new EnemyDefaults {
            Id=26,  TableIndex=123, Name="Auntie Medu (Enhanced)", MaxHp=2500,  AttackPower=245, Category=EnemyCategory.Dragon };
        // e135
        internal static readonly EnemyDefaults RockanoffEnhanced = new EnemyDefaults {
            Id=77,  TableIndex=124, Name="Rockanoff (Enhanced)",   MaxHp=3000,  AttackPower=160, Category=EnemyCategory.Rock };
        // e136
        internal static readonly EnemyDefaults YammichEnhanced = new EnemyDefaults {
            Id=301, TableIndex=125, Name="Yammich (Enhanced)",MaxHp=1500,  AttackPower=92,  Category=EnemyCategory.Undead };
        // e137
        internal static readonly EnemyDefaults WitchHellzaEnhanced = new EnemyDefaults {
            Id=21,  TableIndex=126, Name="Witch Hellza (Enhanced)",   MaxHp=3900,  AttackPower=94,  Category=EnemyCategory.Mage };
        // e138
        internal static readonly EnemyDefaults SteelGiantEnhanced = new EnemyDefaults {
            Id=64,  TableIndex=127, Name="Steel Giant (Enhanced)",  MaxHp=2525,  AttackPower=154, Category=EnemyCategory.Metal };
        // e139
        internal static readonly EnemyDefaults ClubEnhanced = new EnemyDefaults {
            Id=45,  TableIndex=128, Name="Club (Enhanced)",   MaxHp=3250,  AttackPower=134, Category=EnemyCategory.Mage };
        // e109a
        internal static readonly EnemyDefaults CorceaEnhanced = new EnemyDefaults {
            Id=28,  TableIndex=129, Name="Corcea (Enhanced)",MaxHp=3500,  AttackPower=91,  Category=EnemyCategory.Undead };

        // GemronThunder group (floors 41-60): tbl_134–142
        // e141
        internal static readonly EnemyDefaults CaveBatEnhanced = new EnemyDefaults {
            Id=60,  TableIndex=134, Name="Cave Bat (Enhanced)",    MaxHp=6000,  AttackPower=0,   Category=EnemyCategory.Sky };
        // e142
        internal static readonly EnemyDefaults GolEnhanced = new EnemyDefaults {
            Id=90,  TableIndex=135, Name="Gol (Enhanced)",   MaxHp=5500,  AttackPower=65535, Category=EnemyCategory.Rock };
        // e143
        internal static readonly EnemyDefaults MaskOfPrajnaEnhanced = new EnemyDefaults {
            Id=75,  TableIndex=136, Name="Mask of Prajna (Enhanced)", MaxHp=5750,  AttackPower=94,  Category=EnemyCategory.Undead };
        // e144
        internal static readonly EnemyDefaults GyonEnhanced = new EnemyDefaults {
            Id=24,  TableIndex=137, Name="Gyon (Enhanced)", MaxHp=5000,  AttackPower=226, Category=EnemyCategory.Marine };
        // e145
        internal static readonly EnemyDefaults SpadeEnhanced = new EnemyDefaults {
            Id=47,  TableIndex=138, Name="Spade (Enhanced)",   MaxHp=5000,  AttackPower=132, Category=EnemyCategory.Mage };
        // e146
        internal static readonly EnemyDefaults RashDasherEnhanced = new EnemyDefaults {
            Id=63,  TableIndex=139, Name="Rash Dasher (Enhanced)",  MaxHp=4000,  AttackPower=93,  Category=EnemyCategory.Beast };
        // e109b
        internal static readonly EnemyDefaults CaptainEnhanced = new EnemyDefaults {
            Id=27,  TableIndex=140, Name="Captain (Enhanced)",MaxHp=5000,  AttackPower=227, Category=EnemyCategory.Undead };
        // e110a
        internal static readonly EnemyDefaults MimicDSEnhanced = new EnemyDefaults {
            Id=309, TableIndex=141, Name="Mimic (Demon Shaft) (Enhanced)", MaxHp=7500,  AttackPower=235, Category=EnemyCategory.Mimic };
        // e114
        internal static readonly EnemyDefaults KingMimicDSEnhanced = new EnemyDefaults {
            Id=310, TableIndex=142, Name="King Mimic (Demon Shaft) (Enhanced)",MaxHp=8000, AttackPower=181, Category=EnemyCategory.Mimic };

        // GemronWind group (floors 61-80): tbl_145–153
        // e150
        internal static readonly EnemyDefaults AlexanderEnhanced = new EnemyDefaults {
            Id=43,  TableIndex=145, Name="Alexnder (Enhanced)",  MaxHp=5000,  AttackPower=81,  Category=EnemyCategory.Metal };
        // e151
        internal static readonly EnemyDefaults HeartEnhanced = new EnemyDefaults {
            Id=44,  TableIndex=146, Name="Heart (Enhanced)",   MaxHp=6000,  AttackPower=133, Category=EnemyCategory.Mage };
        // e152
        internal static readonly EnemyDefaults BomberHeadEnhanced = new EnemyDefaults {
            Id=49,  TableIndex=147, Name="Bomber Head (Enhanced)",   MaxHp=6500,  AttackPower=159, Category=EnemyCategory.Mage };
        // e153
        internal static readonly EnemyDefaults CrabbyHermitEnhanced = new EnemyDefaults {
            Id=71,  TableIndex=148, Name="Crabby Hermit (Enhanced)", MaxHp=5000,  AttackPower=92,  Category=EnemyCategory.Marine };
        // e154
        internal static readonly EnemyDefaults CursedRoseEnhanced = new EnemyDefaults {
            Id=68,  TableIndex=149, Name="Cursed Rose (Enhanced)",  MaxHp=6750,  AttackPower=146, Category=EnemyCategory.Plant };
        // e155
        internal static readonly EnemyDefaults PiratesChariotEnhanced = new EnemyDefaults {
            Id=25,  TableIndex=150, Name="Pirate's Chariot (Enhanced)",  MaxHp=7800,  AttackPower=92,  Category=EnemyCategory.Metal };
        // e109c
        internal static readonly EnemyDefaults SpaceGyonEnhanced = new EnemyDefaults {
            Id=72,  TableIndex=151, Name="Space Gyon (Enhanced)",MaxHp=6500,  AttackPower=226, Category=EnemyCategory.Marine };
        // e110b
        internal static readonly EnemyDefaults MimicDSEnhancedTwice = new EnemyDefaults {
            Id=309, TableIndex=152, Name="Mimic (Demon Shaft) (Enhanced x2)", MaxHp=10000, AttackPower=235, Category=EnemyCategory.Mimic };
        // e115
        internal static readonly EnemyDefaults KingMimicDSEnhancedTwice = new EnemyDefaults {
            Id=310, TableIndex=153, Name="King Mimic (Demon Shaft) (Enhanced x2)",MaxHp=12500,AttackPower=181, Category=EnemyCategory.Mimic };

        // GemronHoly group (floors 81-99): tbl_155–165
        // e158
        internal static readonly EnemyDefaults GaciousEnhanced = new EnemyDefaults {
            Id=317, TableIndex=155, Name="Gacious (Enhanced)",MaxHp=7500,  AttackPower=65535, Category=EnemyCategory.Undead };
        // e159
        internal static readonly EnemyDefaults EvilBatEnhanced = new EnemyDefaults {
            Id=61,  TableIndex=156, Name="Evil Bat (Enhanced)",    MaxHp=16000, AttackPower=149, Category=EnemyCategory.Sky };
        // e160
        internal static readonly EnemyDefaults CrescentBaronEnhanced = new EnemyDefaults {
            Id=76,  TableIndex=157, Name="Crescent Baron (Enhanced)",    MaxHp=12500, AttackPower=170, Category=EnemyCategory.Sky };
        // e161
        internal static readonly EnemyDefaults StatueDogEnhanced = new EnemyDefaults {
            Id=303, TableIndex=158, Name="Statue Dog (Enhanced)",  MaxHp=9500,  AttackPower=92,  Category=EnemyCategory.Rock };
        // e162
        internal static readonly EnemyDefaults JokerEnhanced = new EnemyDefaults {
            Id=48,  TableIndex=159, Name="Joker (Enhanced)",   MaxHp=10000, AttackPower=154, Category=EnemyCategory.Mage };
        // e163
        internal static readonly EnemyDefaults LichEnhanced = new EnemyDefaults {
            Id=51,  TableIndex=160, Name="Lich (Enhanced)", MaxHp=11500, AttackPower=94,  Category=EnemyCategory.Undead };
        // e164
        internal static readonly EnemyDefaults TitanEnhanced = new EnemyDefaults {
            Id=33,  TableIndex=161, Name="Titan (Enhanced)",   MaxHp=9500,  AttackPower=160, Category=EnemyCategory.Rock };
        // e109d
        internal static readonly EnemyDefaults LivingArmorEnhanced = new EnemyDefaults {
            Id=55,  TableIndex=162, Name="Living Armor (Enhanced)",  MaxHp=7500,  AttackPower=160, Category=EnemyCategory.Rock };
        // e110c
        internal static readonly EnemyDefaults MimicDSEnhancedThrice = new EnemyDefaults {
            Id=309, TableIndex=163, Name="Mimic (Demon Shaft) (Enhanced x3)", MaxHp=19500, AttackPower=235, Category=EnemyCategory.Mimic };
        // c21a
        internal static readonly EnemyDefaults KingMimicDSEnhancedThrice = new EnemyDefaults {
            Id=310, TableIndex=164, Name="King Mimic (Demon Shaft) (Enhanced x3)", MaxHp=40000, AttackPower=181, Category=EnemyCategory.Mimic };
        // tbl_165 = BlackKnight — see BlackKnight field above
        // tbl_166 = garbage/padding row (hp=0, empty code); present in DS floor 100 binary pool
        internal static readonly EnemyDefaults DS166 = new EnemyDefaults {
            Id=0, TableIndex=166, Name="(DS padding tbl_166)", MaxHp=0, AttackPower=0 };

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
                KingMimicMS, MimicMS, Lich, CurseDancer, LivingArmor,
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
                KingsCurse,
                MinotaurJoe,
                DarkGenie, DarkGenieForm2, RightHand, LeftHand, WineKeg,
                UnknownPhase100,
                BlackKnight,
            };
            Defaults = new Dictionary<ushort, EnemyDefaults>(all.Length);
            foreach (EnemyDefaults e in all) Defaults[e.Id] = e;
        }
    }

}
