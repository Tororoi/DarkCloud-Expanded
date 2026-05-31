using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Known data for a single enemy type, mirroring the in-game slot layout.
    /// Null fields have not yet been confirmed from a slot dump or observed in gameplay.
    /// Resistance scale: 100 = neutral, &gt;100 = weak, &lt;100 = resistant, 0 = immune.
    /// </summary>
    internal struct EnemyDefaults
    {
        internal ushort Id;
        internal string Name;
        internal int?   MaxHp;
        internal int?   Abs;
        internal int?   MinGoldDrop;
        internal int?   DropChance;   // 0–100

        // Elemental resistances — packed as 3 ints of 2 ushorts each in memory.
        internal ushort? Type;            // +0x028 low  — CONFIRMED enemy type index (0=dragon, 1=undead, 2=marine, 3=rock, 4=plant, 5=beast, 6=sky, 7=metal, 8=mimic, 9=mage)
        internal ushort? FireRes;     // +0x028 high — CONFIRMED fire
        internal ushort? IceRes;      // +0x02C low  — CONFIRMED ice
        internal ushort? ThunderRes;  // +0x02C high — CONFIRMED thunder
        internal ushort? WindRes;     // +0x030 low  — CONFIRMED wind
        internal ushort? HolyRes;        // +0x030 high — element unknown

        // +0x044/+0x048: CONFIRMED entity scale / collision radius. Single value reused by multiple
        // engine systems: elemental hit effect spawn radius, AI pathfinding clearance, and likely
        // environment collision. Reducing below default visibly shrinks elemental effects; inflating
        // to 100 causes teleporting (engine resolves collision overlap to nearest valid position) and
        // movement lock (pathfinding can't route a body that large). Values are enemy-specific
        // (6.0–8.0 observed); +0x044 always equals +0x048 at spawn — +0x044 appears to be primary.
        internal float? EntityScale;
        internal float? EntityScaleCopy;

        // +0x090: packed ushorts — semantics unclear. Modifying at floor load had no observable effect on AI behaviour,
        // suggesting the value may be read only at spawn initialization, or may not drive AI directly.
        // Observed: Gyon=0/0; Captain/Auntie Medu=3/0; Cursed Rose=2/0; Pirate's Chariot=5/30; Gunny=5/20; Mask of Prajna=5/10.
        internal ushort? Unk090A;   // +0x090 low
        internal ushort? Unk090B;   // +0x090 high — non-zero only on projectile enemies; may be max shoot range

        // +0x0D8: steal item ID — low ushort is the item ID; high ushort is 1 for all observed enemies.
        internal ushort? StealItemId;

        // +0x0DC: packed ushorts — semantics unconfirmed. Scale resembles elemental resistances (100=neutral, <100=resistant).
        internal ushort? ItemResA;   // +0x0DC low
        internal ushort? ItemResB;   // +0x0DC high

        // +0x110: CONFIRMED controls horizontal width of the lock-on reticle.
        // +0x114: CONFIRMED lock-on reticle height. May double as hitbox height.
        // Neither appears to control enemy movement speed directly.
        internal float? ReticleWidth;
        internal float? ReticleHeight;

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
        internal const int MaxHp             = 0x020; // int   — maximum HP; set from type data at spawn
        internal const int Hp                = 0x024; // int   — current HP; decrements on hit; enemy dies when ≤0
        // Each resistance pack holds two ushorts: (low ushort, high ushort)
        internal const int ResistancePack1   = 0x028; // [Type, FireRes]      — Type = enemy type index; fire confirmed; scale: 100=neutral, >100=weak, <100=resistant
        internal const int ResistancePack2   = 0x02C; // [IceRes, ThunderRes] — both confirmed
        internal const int ResistancePack3   = 0x030; // [WindRes, HolyRes]   — wind confirmed
        internal const int MinGoldDrop       = 0x034; // int   — minimum gold dropped on death
        internal const int DropChance        = 0x038; // int   — item drop chance (0–100)

        // ── Identity ─────────────────────────────────────────────────────────
        internal const int EnemyTypeId       = 0x042; // ushort — enemy type ID; used to look up name, stats, and model data
        internal const int EntityScale       = 0x044; // float — CONFIRMED entity/collision radius; enemy-specific (6.0–8.0); does not affect attack hitbox
        internal const int EntityScaleCopy   = 0x048; // float — copy of EntityScale set at spawn; secondary reference
        internal const int TypeDataPtr       = 0x04C; // int   — pointer to shared enemy-type behavior data; identical across all slots of the same type (e.g. all Pirate's Chariots = 0x01C05140)
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

        internal const int MovementBlend     = 0x080; // float — rest value is enemy-type-specific (Pirate's Chariot=0.70, Auntie Medu=0.50); drops to 0.0 momentarily on hit or AI pause; likely movement speed blend weight

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
        // AiSpeedParam is enemy-type and state-specific: Auntie Medu alternates 0.36↔0.20; Pirate's Chariot uses 0.25/−1.0; Mask of Prajna uses 0.24–0.35.
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
        // Hit flash color and decay rate are enemy-type-specific:
        //   Pirate's Chariot: R=255 G=0   B=0   (red),    FlashDecayRate=0.08 (~12 frames at 30fps)
        //   Auntie Medu:      R=255 G=255 B=0   (yellow), FlashDecayRate=0.20 (~5 frames at 30fps)
        //   Mask of Prajna:   R=255 G=0   B=0   (red),    FlashDecayRate=0.20 (~5 frames at 30fps)
        // FlashDecayRate: engine negates internally (write +0.08, stored −0.08); duration = 1.0/rate frames; for ~2s at 30fps write 0.016.
        // FlashTimer: engine always resets to 1.0 at flash-start regardless of written value; decrements by FlashDecayRate each frame.
        internal const int FlashColorRed     = 0x130; // float — 0.0 at rest; 0–255 red channel
        internal const int FlashColorGreen   = 0x134; // float — 0.0 at rest; 0–255 green channel; 0 for red-only flash types
        internal const int FlashColorBlue    = 0x138; // float — 0.0 at rest; 0–255 blue channel

        // ── Behavior Ranges (set at spawn, static per enemy type) ─────────────
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
        // by specific behavior scripts (not all enemy types).
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
        internal const int KnockbackStrength = 0x184; // READ ONLY — float; engine writes per-enemy-type value at hit time; confirmed not settable (write at floor load overwritten by engine on hit); Pirate's Chariot=0.10, Skel.Soldier=0.20, Auntie Medu=0.20, Dasher=0.30
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

        // +0x370: animation clip cap (7–23 per enemy type); capping below true value freezes higher-index animations (attacks); Setting above true count does not change default behavior.
        internal const int AnimCount = 0x370;
    }

    /// <summary>Helpers for reading and writing packed elemental resistances.</summary>
    internal static class EnemyResistances
    {
        internal static (ushort type, ushort fire, ushort ice, ushort thunder, ushort wind, ushort holy)
            Read(int slotBase)
        {
            int p1 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1);
            int p2 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack2);
            int p3 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack3);
            return ((ushort)(p1 & 0xFFFF), (ushort)(p1 >> 16),
                    (ushort)(p2 & 0xFFFF), (ushort)(p2 >> 16),
                    (ushort)(p3 & 0xFFFF), (ushort)(p3 >> 16));
        }

        internal static void Write(int slotBase, ushort type, ushort fire, ushort ice, ushort thunder, ushort wind, ushort holy)
        {
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack1, (fire  << 16) | type);
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack2, (thunder << 16) | ice);
            Memory.WriteInt(slotBase + EnemySlotOffsets.ResistancePack3, (holy  << 16) | wind);
        }

        internal static ushort ReadFire(int slotBase)
            => (ushort)(Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1) >> 16);
    }

    /// <summary>
    /// A contiguous range of dungeon floors that shares a single enemy spawn pool.
    /// </summary>
    internal struct FloorPool
    {
        internal int      StartFloor;
        internal int      EndFloor;
        internal ushort[] EnemyIds;  // null = pool not yet catalogued from a dump
    }

    /// <summary>
    /// Dungeon metadata with per-range enemy pools.
    /// Pools are ordered by StartFloor. Each covers a distinct, non-overlapping floor range.
    /// </summary>
    internal struct DungeonData
    {
        internal byte       Id;
        internal string     Name;
        internal int        TotalFloors;  // 0 = unconfirmed
        internal FloorPool[] Pools;       // ordered by StartFloor
    }

    /// <summary>
    /// Default stats per enemy type. Named static fields mirror the FishDatabase pattern.
    /// Populated from unmodified floor dumps — null fields are unconfirmed.
    /// Never populate defaults from miniboss slot data; minibosses share the same type ID
    /// as their normal counterpart but have different stats.
    /// </summary>
    internal static class EnemyDatabase
    {
        // ── Divine Beast Cave (dungeon 0) ─────────────────────────────────────

        // confirmed from clean dump 2026-05-30, DBC game fl.6
        internal static readonly EnemyDefaults SkeletonSoldier = new EnemyDefaults {
            Id=3,  Name="Skeleton Soldier", MaxHp=23,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Type=1, FireRes=110, IceRes=90,  ThunderRes=100, WindRes=100, HolyRes=160,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=null, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1080, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults MasterJacket = new EnemyDefaults {
            Id=1,  Name="Master Jacket",    MaxHp=75,  Abs=5,  MinGoldDrop=7,  DropChance=50,
            Type=1, FireRes=110, IceRes=80,  ThunderRes=100, WindRes=80,  HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.3f, StealItemId=177, ItemResA=100, ItemResB=80,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1123, ModelAnimCount=20 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        internal static readonly EnemyDefaults Statue = new EnemyDefaults {
            Id=5,  Name="Statue",           MaxHp=38,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Type=3, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=20,
            ReticleWidth=1.2f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=792,  ModelAnimCount=18 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.10; spans both pools
        internal static readonly EnemyDefaults Dasher = new EnemyDefaults {
            Id=6,  Name="Dasher",           MaxHp=23,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Type=5, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=1, Unk090B=0,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=148, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5/fl.12; spans both pools
        internal static readonly EnemyDefaults CaveBat = new EnemyDefaults {
            Id=60, Name="Cave Bat",         MaxHp=12,  Abs=3,  MinGoldDrop=4,  DropChance=30,
            Type=6, FireRes=100, IceRes=100, ThunderRes=100, WindRes=150, HolyRes=100,
            EntityScale=3.0f, EntityScaleCopy=3.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=0.8f, ReticleHeight=0.8f, StealItemId=151, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=940,  ModelAnimCount=21 };

        // confirmed from clean dump 2026-05-30, DBC game fl.6
        internal static readonly EnemyDefaults MimicDBC = new EnemyDefaults {
            Id=35, Name="Mimic (Divine Beast Cave)", MaxHp=68, Abs=3, MinGoldDrop=10, DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=1, Unk090B=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=920,  ModelAnimCount=20 };

        // confirmed from clean dump 2026-05-30, DBC game fl.10/fl.14
        internal static readonly EnemyDefaults Ghost = new EnemyDefaults {
            Id=42, Name="Ghost",            MaxHp=15,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Type=1, FireRes=110, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=3.6f, EntityScaleCopy=3.6f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.1f, ReticleHeight=1.1f, StealItemId=135, ItemResA=100, ItemResB=90,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1220, ModelAnimCount=21 };

        // confirmed from clean dump 2026-05-30, DBC game fl.14
        internal static readonly EnemyDefaults Dragon = new EnemyDefaults {
            Id=59, Name="Dragon",           MaxHp=90,  Abs=5,  MinGoldDrop=15, DropChance=50,
            Type=0, FireRes=50,  IceRes=120, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=17.5f, EntityScaleCopy=17.5f, Unk090A=5, Unk090B=40,
            ReticleWidth=2.9f, ReticleHeight=2.7f, StealItemId=161, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=32.0f, ModelUnk028=60.0f, ModelDataSize=1422, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults KingMimicDBC = new EnemyDefaults {
            Id=34, Name="King Mimic (Divine Beast Cave)", MaxHp=90, Abs=4, MinGoldDrop=20, DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23 };

        // confirmed from clean dump 2026-05-30, DBC fl.12
        internal static readonly EnemyDefaults Rockanoff = new EnemyDefaults {
            Id=77, Name="Rockanoff",        MaxHp=30,  Abs=3,  MinGoldDrop=10, DropChance=30,
            Type=3, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.7f, ReticleHeight=1.7f, StealItemId=160, ItemResA=90,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=954,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, DBC game fl.5
        internal static readonly EnemyDefaults StatueDog = new EnemyDefaults {
            Id=303, Name="Statue Dog",      MaxHp=15,  Abs=2,  MinGoldDrop=5,  DropChance=30,
            Type=3, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=60,
            EntityScale=9.0f, EntityScaleCopy=9.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=160, ItemResA=90,  ItemResB=100,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=667,  ModelAnimCount=12 };

        // ── Wise Owl Forest (dungeon 1) ───────────────────────────────────────

        // confirmed from clean dump 2026-05-30, WO game fl.5/fl.10; spans both pools
        internal static readonly EnemyDefaults CannibalPlant = new EnemyDefaults {
            Id=11, Name="Cannibal Plant",   MaxHp=60,  Abs=3,  MinGoldDrop=5,  DropChance=30,
            Type=4, FireRes=180, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=167, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=21.0f, ModelUnk028=60.0f, ModelDataSize=474,  ModelAnimCount=7 };

        // confirmed from clean dump 2026-05-30, WO game fl.7/fl.14; spans both pools
        internal static readonly EnemyDefaults Sunday = new EnemyDefaults {
            Id=14, Name="Sunday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Type=9, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=170, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1454, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.7
        internal static readonly EnemyDefaults Monday = new EnemyDefaults {
            Id=15, Name="Monday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Type=9, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1424, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.7
        internal static readonly EnemyDefaults Tuesday = new EnemyDefaults {
            Id=16, Name="Tuesday",          MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Type=9, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1427, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.5/fl.7
        internal static readonly EnemyDefaults Wednesday = new EnemyDefaults {
            Id=17, Name="Wednesday",        MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Type=9, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=146, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.5
        internal static readonly EnemyDefaults Friday = new EnemyDefaults {
            Id=19, Name="Friday",           MaxHp=60,  Abs=3,  MinGoldDrop=6,  DropChance=40,
            Type=9, FireRes=100, IceRes=100, ThunderRes=100, WindRes=110, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=148, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1438, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.7/fl.14/fl.16; spans both pools
        internal static readonly EnemyDefaults WitchIllza = new EnemyDefaults {
            Id=22, Name="Witch Illza",      MaxHp=120, Abs=3,  MinGoldDrop=4,  DropChance=30,
            Type=9, FireRes=90,  IceRes=90,  ThunderRes=90,  WindRes=90,  HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=169, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=868,  ModelAnimCount=18 };

        // confirmed from clean dump 2026-05-30, WO game fl.5
        internal static readonly EnemyDefaults MimicWO = new EnemyDefaults {
            Id=79, Name="Mimic (Wise Owl)", MaxHp=90,  Abs=3,  MinGoldDrop=6,  DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=2, Unk090B=10,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=914,  ModelAnimCount=20 };

        // confirmed from clean dump 2026-05-30, WO game fl.5/fl.10; spans both pools
        internal static readonly EnemyDefaults HaleyHoley = new EnemyDefaults {
            Id=305, Name="Haley Holey",     MaxHp=50,  Abs=3,  MinGoldDrop=7,  DropChance=40,
            Type=4, FireRes=140, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.0f, ReticleHeight=1.1f, StealItemId=186, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=1046, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.14/fl.16
        internal static readonly EnemyDefaults Werewolf = new EnemyDefaults {
            Id=7,  Name="Werewolf",         MaxHp=180, Abs=12, MinGoldDrop=15, DropChance=50,
            Type=5, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=150,
            EntityScale=7.5f, EntityScaleCopy=7.5f, Unk090A=5, Unk090B=0,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=174, ItemResA=90,  ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1111, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, WO game fl.10
        internal static readonly EnemyDefaults Hornet = new EnemyDefaults {
            Id=9,  Name="Hornet",           MaxHp=60,  Abs=3,  MinGoldDrop=7,  DropChance=30,
            Type=6, FireRes=100, IceRes=120, ThunderRes=100, WindRes=120, HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21 };

        // confirmed from clean dump 2026-05-30, WO game fl.14/fl.16
        internal static readonly EnemyDefaults Halloween = new EnemyDefaults {
            Id=10, Name="Halloween",        MaxHp=150, Abs=3,  MinGoldDrop=7,  DropChance=40,
            Type=4, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=3, Unk090B=10,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=168, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=850,  ModelAnimCount=18 };

        // confirmed from clean dump 2026-05-30, WO game fl.10/fl.16
        internal static readonly EnemyDefaults EarthDigger = new EnemyDefaults {
            Id=12, Name="Earth Digger",     MaxHp=120, Abs=3,  MinGoldDrop=7,  DropChance=30,
            Type=5, FireRes=100, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=100,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.3f, StealItemId=188, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=17.0f, ModelUnk028=60.0f, ModelDataSize=936,  ModelAnimCount=19 };

        // ── Shipwreck (dungeon 2) ──────────────────────────────────────────────

        // confirmed from clean dump 2026-05-30, SW game fl.8/fl.10; spans both pools
        internal static readonly EnemyDefaults Captain = new EnemyDefaults {
            Id=27, Name="Captain",          MaxHp=225, Abs=6,  MinGoldDrop=12, DropChance=30,
            Type=1, FireRes=110, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=150,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=177, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=873,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.10
        internal static readonly EnemyDefaults PiratesChariot = new EnemyDefaults {
            Id=25, Name="Pirate's Chariot", MaxHp=270, Abs=8,  MinGoldDrop=15, DropChance=30,
            Type=7, FireRes=120, IceRes=80,  ThunderRes=140, WindRes=100, HolyRes=100,
            EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=5, Unk090B=30,
            ReticleWidth=1.9f, ReticleHeight=1.8f, StealItemId=159, ItemResA=95,  ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=835,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.10; Unk020=14.0/Unk028=0.0 — ranged gun enemy
        internal static readonly EnemyDefaults Gunny = new EnemyDefaults {
            Id=23, Name="Gunny",            MaxHp=250, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Type=2, FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.5f, ReticleHeight=1.5f, StealItemId=153, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=20.0f, ModelUnk028=0.0f,  ModelDataSize=1270, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.3; StealItemId=null (0xFFFF in memory)
        internal static readonly EnemyDefaults CursedRose = new EnemyDefaults {
            Id=68, Name="Cursed Rose",      MaxHp=225, Abs=4,  MinGoldDrop=6,  DropChance=30,
            Type=4, FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
            EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=22.0f, ModelUnk028=60.0f, ModelDataSize=476,  ModelAnimCount=7 };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        internal static readonly EnemyDefaults Gyon = new EnemyDefaults {
            Id=24, Name="Gyon",             MaxHp=225, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Type=2, FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.6f, StealItemId=134, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=25.0f, ModelUnk028=60.0f, ModelDataSize=849,  ModelAnimCount=16 };

        // confirmed from clean dump 2026-05-30, SW game fl.17
        // Unk150/154/158 (0x150/154/158) read as 0 for regular Auntie Medu; observed non-zero (127.5/80.0/15.0) when the mod's miniboss process was active on this enemy type.
        internal static readonly EnemyDefaults AuntieMedu = new EnemyDefaults {
            Id=26, Name="Auntie Medu",      MaxHp=300, Abs=10, MinGoldDrop=15, DropChance=30,
            Type=0, FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=166, ItemResA=100, ItemResB=60,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=944,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.3
        internal static readonly EnemyDefaults Corcea = new EnemyDefaults {
            Id=28, Name="Corcea",           MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Type=1, FireRes=110, IceRes=100, ThunderRes=100, WindRes=140, HolyRes=130,
            EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.4f, ReticleHeight=1.4f, StealItemId=152, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=871,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17
        internal static readonly EnemyDefaults MaskOfPrajna = new EnemyDefaults {
            Id=75, Name="Mask of Prajna",   MaxHp=375, Abs=12, MinGoldDrop=15, DropChance=50,
            Type=1, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
            EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=5, Unk090B=10,
            ReticleWidth=1.0f, ReticleHeight=1.0f, StealItemId=151, ItemResA=80,  ItemResB=70,
            ModelUnk020=32.0f, ModelUnk024=26.0f, ModelUnk028=0.0f,  ModelDataSize=1398, ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.14/fl.17; Unk028=0.0 — mage/ranged enemy
        // fire=200 (extreme), ice=0 (immune) — twin elemental extremes match mage-type pattern
        internal static readonly EnemyDefaults Sam = new EnemyDefaults {
            Id=85, Name="Sam",              MaxHp=180, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Type=9, FireRes=200, IceRes=0,   ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.6f, StealItemId=162, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=19.0f, ModelUnk028=0.0f,  ModelDataSize=871,  ModelAnimCount=19 };

        // confirmed from clean dump 2026-05-30, SW game fl.8
        internal static readonly EnemyDefaults MimicSW = new EnemyDefaults {
            Id=81, Name="Mimic (Shipwreck)", MaxHp=150, Abs=4, MinGoldDrop=6, DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20 };

        // confirmed from dump 2026-05-31, SW game fl.16; same model/anim data as KingMimicDBC but higher stats and different Unk090
        internal static readonly EnemyDefaults KingMimicSW = new EnemyDefaults {
            Id=80, Name="King Mimic (Shipwreck)", MaxHp=300, Abs=15, MinGoldDrop=15, DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=12.0f, EntityScaleCopy=12.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.9f, ReticleHeight=1.65f, StealItemId=175, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=28.0f, ModelUnk028=60.0f, ModelDataSize=1012, ModelAnimCount=23 };

        // ── Sun and Moon Temple (dungeon 3) ──────────────────────────────────────

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools; unk020=9.0 (higher than most melee enemies); no steal (0xFFFF in memory)
        internal static readonly EnemyDefaults Mummy = new EnemyDefaults {
            Id=50, Name="Mummy",            MaxHp=150, Abs=4,  MinGoldDrop=10, DropChance=30,
            Type=1, FireRes=150, IceRes=50,  ThunderRes=100, WindRes=100, HolyRes=120,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=null, ItemResA=100, ItemResB=70,
            ModelUnk020=9.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=1029, ModelAnimCount=19 };

        // confirmed from dump 2026-05-31, SM game fl.1–7; spans both pools
        internal static readonly EnemyDefaults Phantom = new EnemyDefaults {
            Id=58, Name="Phantom",          MaxHp=150, Abs=4,  MinGoldDrop=8,  DropChance=30,
            Type=6, FireRes=100, IceRes=125, ThunderRes=100, WindRes=125, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=0, Unk090B=0,
            ReticleWidth=1.2f, ReticleHeight=1.2f, StealItemId=151, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=10.0f, ModelUnk028=60.0f, ModelDataSize=1060, ModelAnimCount=21 };

        // confirmed from dump 2026-05-31, SM game fl.2–7; Unk090A=8 (large, like Golem); steal=159
        internal static readonly EnemyDefaults BomberHead = new EnemyDefaults {
            Id=49, Name="Bomber Head",      MaxHp=180, Abs=4,  MinGoldDrop=10, DropChance=30,
            Type=9, FireRes=200, IceRes=75,  ThunderRes=125, WindRes=100, HolyRes=75,
            EntityScale=4.0f, EntityScaleCopy=4.0f, Unk090A=8, Unk090B=20,
            ReticleWidth=1.3f, ReticleHeight=1.4f, StealItemId=159, ItemResA=100, ItemResB=70,
            ModelUnk020=7.0f, ModelUnk024=18.0f, ModelUnk028=60.0f, ModelDataSize=1663, ModelAnimCount=23 };

        // confirmed from dump 2026-05-31, SM game fl.3–7; same model as MimicSW (dataSize=918, animCount=20)
        internal static readonly EnemyDefaults MimicSunMoon = new EnemyDefaults {
            Id=37, Name="Mimic (Sun & Moon Temple)", MaxHp=270, Abs=6, MinGoldDrop=12, DropChance=80,
            Type=8, FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=100,
            EntityScale=5.0f, EntityScaleCopy=5.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.1f, ReticleHeight=1.0f, StealItemId=177, ItemResA=90,  ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=20.0f, ModelUnk028=60.0f, ModelDataSize=918,  ModelAnimCount=20 };

        // confirmed from dump 2026-05-31, SM game fl.3–7; scale=14.0 (large body); Unk090A=8
        internal static readonly EnemyDefaults Golem = new EnemyDefaults {
            Id=30, Name="Golem",            MaxHp=375, Abs=4,  MinGoldDrop=15, DropChance=30,
            Type=3, FireRes=100, IceRes=100, ThunderRes=110, WindRes=110, HolyRes=100,
            EntityScale=14.0f, EntityScaleCopy=14.0f, Unk090A=8, Unk090B=0,
            ReticleWidth=2.4f, ReticleHeight=2.4f, StealItemId=177, ItemResA=100, ItemResB=50,
            ModelUnk020=7.0f, ModelUnk024=33.0f, ModelUnk028=60.0f, ModelDataSize=1071, ModelAnimCount=18 };

        // confirmed from dump 2026-05-31, SM game fl.7; unk020=14.0 (ranged/large class); unk028=100.0 (highest observed — aquatic/crab movement class?)
        internal static readonly EnemyDefaults CrabbyHermit = new EnemyDefaults {
            Id=71, Name="Crabby Hermit",    MaxHp=300, Abs=4,  MinGoldDrop=12, DropChance=30,
            Type=2, FireRes=100, IceRes=100, ThunderRes=125, WindRes=100, HolyRes=100,
            EntityScale=10.0f, EntityScaleCopy=10.0f, Unk090A=5, Unk090B=20,
            ReticleWidth=1.9f, ReticleHeight=1.9f, StealItemId=166, ItemResA=95,  ItemResB=70,
            ModelUnk020=14.0f, ModelUnk024=22.0f, ModelUnk028=100.0f, ModelDataSize=1612, ModelAnimCount=22 };

        // ── Lookup by type ID ──────────────────────────────────────────────────
        internal static readonly Dictionary<ushort, EnemyDefaults> Defaults;
        static EnemyDatabase()
        {
            EnemyDefaults[] all =
            {
                // DBC
                SkeletonSoldier, MasterJacket, Statue, Dasher, CaveBat, MimicDBC,
                Ghost, Dragon, KingMimicDBC, Rockanoff, StatueDog,
                // WO
                CannibalPlant, Sunday, Monday, Tuesday, Wednesday, Friday,
                WitchIllza, MimicWO, HaleyHoley,
                Werewolf, Hornet, Halloween, EarthDigger,
                // SW
                Captain, PiratesChariot, Gunny, CursedRose, Gyon, AuntieMedu,
                Corcea, MaskOfPrajna, Sam, MimicSW, KingMimicSW,
                // SM
                Mummy, Phantom, BomberHead, MimicSunMoon, Golem, CrabbyHermit,
            };
            Defaults = new Dictionary<ushort, EnemyDefaults>(all.Length);
            foreach (EnemyDefaults e in all) Defaults[e.Id] = e;
        }
    }

    /// <summary>
    /// Per-dungeon metadata repository, keyed by dungeon ID byte.
    /// Dungeon IDs: 0=Divine Beast Cave, 1=Wise Owl, 2=Shipwreck,
    ///              3=Sun and Moon, 4=Moon Sea, 5=Gallery of Time, 6=Demon Shaft.
    /// </summary>
    internal static class DungeonDatabase
    {
        // All floor ranges and enemy splits are estimates from game knowledge unless noted.
        // Update StartFloor/EndFloor and move IDs between pools as confirmed from dump sessions.

        internal static readonly DungeonData DivineBeastCave = new DungeonData
        {
            Id = 0, Name = "Divine Beast Cave", TotalFloors = 15,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=7,  EnemyIds=new ushort[]  // confirmed game fl.5, fl.6
                {
                    EnemyDatabase.SkeletonSoldier.Id, EnemyDatabase.Statue.Id,  EnemyDatabase.Dasher.Id,
                    EnemyDatabase.MimicDBC.Id,        EnemyDatabase.CaveBat.Id, EnemyDatabase.StatueDog.Id,
                } },
                new FloorPool { StartFloor=8,  EndFloor=8,  EnemyIds=null },  // special floor; no enemies
                new FloorPool { StartFloor=9,  EndFloor=14, EnemyIds=new ushort[]  // confirmed game fl.10, fl.12, fl.14
                {
                    EnemyDatabase.MasterJacket.Id, EnemyDatabase.Statue.Id,  EnemyDatabase.Dasher.Id,
                    EnemyDatabase.Ghost.Id,        EnemyDatabase.Dragon.Id,  EnemyDatabase.CaveBat.Id,
                    EnemyDatabase.Rockanoff.Id,
                } },
                new FloorPool { StartFloor=15, EndFloor=15, EnemyIds=null },  // boss floor; needs dump
            },
        };

        internal static readonly DungeonData WiseOwl = new DungeonData
        {
            Id = 1, Name = "Wise Owl Forest", TotalFloors = 17,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=8,  EnemyIds=new ushort[]  // confirmed game fl.5, fl.7
                {
                    EnemyDatabase.CannibalPlant.Id, EnemyDatabase.Sunday.Id,    EnemyDatabase.Monday.Id,
                    EnemyDatabase.Tuesday.Id,       EnemyDatabase.Wednesday.Id, EnemyDatabase.Friday.Id,
                    EnemyDatabase.WitchIllza.Id,    EnemyDatabase.MimicWO.Id,   EnemyDatabase.HaleyHoley.Id,
                } },
                new FloorPool { StartFloor=9,  EndFloor=9,  EnemyIds=null },  // special floor; no enemies
                new FloorPool { StartFloor=10, EndFloor=16, EnemyIds=new ushort[]  // confirmed game fl.10, fl.14, fl.16; EndFloor estimated
                {
                    EnemyDatabase.Werewolf.Id,      EnemyDatabase.Hornet.Id,      EnemyDatabase.Halloween.Id,
                    EnemyDatabase.CannibalPlant.Id, EnemyDatabase.EarthDigger.Id, EnemyDatabase.Sunday.Id,
                    EnemyDatabase.WitchIllza.Id,    EnemyDatabase.HaleyHoley.Id,
                } },
                new FloorPool { StartFloor=17, EndFloor=17, EnemyIds=null },  // boss floor; needs dump
            },
        };

        internal static readonly DungeonData Shipwreck = new DungeonData
        {
            Id = 2, Name = "Shipwreck", TotalFloors = 18,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=8,  EnemyIds=new ushort[]  // confirmed game fl.3, fl.8
                {
                    EnemyDatabase.Gyon.Id,      EnemyDatabase.Captain.Id,   EnemyDatabase.Corcea.Id,
                    EnemyDatabase.CursedRose.Id, EnemyDatabase.MimicSW.Id,
                } },
                new FloorPool { StartFloor=9,  EndFloor=9,  EnemyIds=null },  // special floor; no enemies
                new FloorPool { StartFloor=10, EndFloor=17, EnemyIds=new ushort[]  // confirmed game fl.10, fl.14, fl.16, fl.17; EndFloor estimated
                {
                    EnemyDatabase.Gunny.Id,         EnemyDatabase.PiratesChariot.Id, EnemyDatabase.AuntieMedu.Id,
                    EnemyDatabase.Captain.Id,       EnemyDatabase.MaskOfPrajna.Id,   EnemyDatabase.Sam.Id,
                    EnemyDatabase.KingMimicSW.Id,
                } },
                new FloorPool { StartFloor=18, EndFloor=18, EnemyIds=null },  // boss floor; needs dump
            },
        };

        internal static readonly DungeonData SunAndMoon = new DungeonData
        {
            Id = 3, Name = "Sun and Moon Temple", TotalFloors = 13,  // estimated
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=6,  EnemyIds=new ushort[]  // confirmed game fl.1–6
                {
                    EnemyDatabase.Mummy.Id,       EnemyDatabase.Phantom.Id,  EnemyDatabase.BomberHead.Id,
                    EnemyDatabase.MimicSunMoon.Id, EnemyDatabase.Golem.Id,
                } },
                new FloorPool { StartFloor=7,  EndFloor=12, EnemyIds=new ushort[]  // confirmed game fl.7; EndFloor estimated
                {
                    EnemyDatabase.Mummy.Id,       EnemyDatabase.Phantom.Id,      EnemyDatabase.BomberHead.Id,
                    EnemyDatabase.MimicSunMoon.Id, EnemyDatabase.Golem.Id,        EnemyDatabase.CrabbyHermit.Id,
                } },
                new FloorPool { StartFloor=13, EndFloor=13, EnemyIds=null },  // boss floor; needs dump
            },
        };

        internal static readonly DungeonData MoonSea = new DungeonData
        {
            Id = 4, Name = "Moon Sea", TotalFloors = 10,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=4,  EnemyIds=new ushort[]{ 38, 39, 49, 50, 52 } },      // estimated
                new FloorPool { StartFloor=5,  EndFloor=9,  EnemyIds=new ushort[]{ 54, 55, 56, 57, 59 } },       // estimated
                new FloorPool { StartFloor=10, EndFloor=10, EnemyIds=null },                                       // boss floor; needs dump
            },
        };

        internal static readonly DungeonData GalleryOfTime = new DungeonData
        {
            Id = 5, Name = "Gallery of Time", TotalFloors = 10,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,  EndFloor=4,  EnemyIds=new ushort[]{ 62, 63, 64, 65, 66, 67, 69 } },        // estimated
                new FloorPool { StartFloor=5,  EndFloor=9,  EnemyIds=new ushort[]{ 70, 71, 72, 73, 74, 76, 77, 82, 83 } }, // estimated
                new FloorPool { StartFloor=10, EndFloor=10, EnemyIds=null },                                                // boss floor; needs dump
            },
        };

        internal static readonly DungeonData DemonShaft = new DungeonData
        {
            Id = 6, Name = "Demon Shaft", TotalFloors = 100,
            Pools = new FloorPool[]
            {
                new FloorPool { StartFloor=1,   EndFloor=25,  EnemyIds=null },  // needs dump
                new FloorPool { StartFloor=26,  EndFloor=50,  EnemyIds=null },  // needs dump
                new FloorPool { StartFloor=51,  EndFloor=75,  EnemyIds=null },  // needs dump
                new FloorPool { StartFloor=76,  EndFloor=99,  EnemyIds=null },  // needs dump
                new FloorPool { StartFloor=100, EndFloor=100, EnemyIds=null },  // boss floor; needs dump
            },
        };

        private static readonly Dictionary<byte, DungeonData> ById;
        static DungeonDatabase()
        {
            DungeonData[] all = { DivineBeastCave, WiseOwl, Shipwreck, SunAndMoon, MoonSea, GalleryOfTime, DemonShaft };
            ById = new Dictionary<byte, DungeonData>(all.Length);
            foreach (DungeonData d in all) ById[d.Id] = d;
        }

        internal static bool TryGetValue(byte dungeonId, out DungeonData data) => ById.TryGetValue(dungeonId, out data);
    }
}
