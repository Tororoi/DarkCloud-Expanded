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
        // See LogBossSlotSpeciesDataPtrs() in Enemies.cs for live-dump diagnostic.
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

        // +0x064/+0x066: loaded into the live slot's Unk090 (0x090) low/high ushorts at spawn.
        // Low ushort: observed values 0–5; non-zero for enemies with ranged/special AI.
        // High ushort: non-zero only for ranged enemies (Gunny=20, Pirate's Chariot=30); may be max shoot range in world units.
        internal const int Unk010     = 0x064; // ushort — copied to slot Unk090 low  at spawn
        internal const int Unk012     = 0x066; // ushort — copied to slot Unk090 high at spawn; non-zero on ranged enemies

        internal const int Unk014     = 0x068; // ushort — 65535 for most enemies; non-zero for some (values 0,2,3,11); purpose unknown
        internal const int Unk016     = 0x06A; // ushort — 65535 for all observed valid enemies

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
        internal const int StealFlag  = 0x082; // ushort — 1 if enemy has a steal item, 0 if not

        internal const int ItemResA   = 0x084; // ushort — item resistance A; semantics unconfirmed; scale resembles elemental resistance
        internal const int ItemResB   = 0x086; // ushort — item resistance B

        // +0x088: base melee attack power. Regular enemies: 82–200. Bosses: 65535 (sentinel; use
        // only behavior-script attacks). 0 if enemy has no melee attack (e.g. pure-projectile flyers).
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
    /// IMPORTANT — the Id at entry +0x4 is the game's internal enemy id (0–166), the same value as
    /// EnemyDefaults.Id. It is NOT the physical species-table record index (use EnemyDefaults.TableIndex
    /// for that), and the "Id N → model eNNa" convention holds for many mid-range ids but NOT all
    /// (bosses use cNNx; several ids have no direct species record and route through event/scripted
    /// paths — e.g. ids 53/54 Killer Snake). Always resolve via EnemySpeciesTable / EnemyData.cs.
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
            (layoutBaseNative + 0x20000000) + floor * FloorStride;

        /// <summary>PCSX2 address of entry <paramref name="entry"/> (0–8) within a floor block.</summary>
        internal static int EntryAddress(int layoutBaseNative, int floor, int entry) =>
            FloorAddress(layoutBaseNative, floor) + entry * EntryStride;

        // ── Entry field offsets (0x0C bytes per entry) ──
        // +0x0: purpose UNCONFIRMED (entry 0 commonly 3–4, other entries 1). It is NOT the floor
        // population: live testing (2026-06-09) showed that overwriting it (1 vs 16) does not change how
        // many enemies spawn — the floor's enemy COUNT is fixed by its spawn-point generation at floor
        // assembly, independent of this table. The roster only selects WHICH species fill those points
        // (weighted by +0x8). Earlier "spawn count / population" label was wrong.
        internal const int Count    = 0x0; // int   — UNCONFIRMED; not the population (see note above)
        // +0x4: enemy id (= EnemyDefaults.Id). -1 = empty slot. This is what SetupBaseModel loads.
        internal const int Id       = 0x4; // int   — enemy id; -1 = unused
        // +0x8: spawn weight (percent). Active entries' weights sum to ≈100 per floor → weighted-random pick.
        internal const int Weight   = 0x8; // int   — spawn weight %
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
}
