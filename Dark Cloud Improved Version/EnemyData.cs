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
    }

    /// <summary>Enemy slot field offsets relative to slot base address.</summary>
    internal static class EnemySlotOffsets
    {
        // Confirmed via dump analysis
        internal const int RenderStatus     = 0x000;
        internal const int FreezeTimer      = 0x008;
        internal const int PoisonPeriod     = 0x00C;
        internal const int StaminaTimer     = 0x010;
        internal const int GooeyState       = 0x014;
        internal const int DistanceToPlayer = 0x018;
        internal const int MaxHp            = 0x020;
        internal const int Hp               = 0x024;
        // Resistance packs: each int holds two ushorts (low = first, high = second)
        internal const int ResistancePack1  = 0x028; // [Type, FireRes]   — Type is enemy type index, fire confirmed
        internal const int ResistancePack2  = 0x02C; // [IceRes, ThunderRes] — both confirmed
        internal const int ResistancePack3  = 0x030; // [WindRes, HolyRes]   — wind confirmed
        internal const int MinGoldDrop      = 0x034;
        internal const int DropChance       = 0x038;
        internal const int EnemyTypeId      = 0x042; // ushort
        internal const int EntityScale           = 0x044; // float — CONFIRMED entity scale/collision radius; enemy-specific (6.0–8.0); primary value. Does not affect attack collision.
        internal const int EntityScaleCopy           = 0x048; // float — paired copy of EntityScale at spawn; secondary
        internal const int Unk090           = 0x090; // packed ushorts — semantics unclear; write at floor load had no effect on AI; Unk090B non-zero only on projectile enemies
        internal const int ForceItemDrop    = 0x0A0;
        internal const int RenderDistance    = 0x0A4; // float — CONFIRMED controls enemy render distance; also determines when enemy dot appears on map
        internal const int Abs              = 0x0B0;
        internal const int StealItemId      = 0x0D8;
        internal const int ItemResistance   = 0x0DC; // possibly two packed ushorts — needs verification
        internal const int LocationX        = 0x100;
        internal const int LocationZ        = 0x104;
        internal const int LocationY        = 0x108;
        internal const int ReticleWidth     = 0x110; // float — CONFIRMED controls horizontal reticle width
        internal const int ReticleHeight    = 0x114; // float — CONFIRMED lock-on reticle height; may double as hitbox height
        internal const int LockOnDistance   = 0x118; // float — CONFIRMED distance at which lock-on becomes available; default 120.0
        internal const int Opacity           = 0x120; // float — CONFIRMED enemy opacity; default 128.0; lower = more translucent, higher does nothing
        // Flash color overlays — transient additive overlays; 0.0 at rest, up to 255.0 during event flashes (hit, death).
        // Stamina yellow tint is NOT driven by these — all three are 0.0 at spawn even with stamina active.
        internal const int FlashColorRed    = 0x130; // float — 0.0 at rest; e.g. 255.0 on death flash
        internal const int FlashColorGreen  = 0x134; // float — 0.0 at rest; e.g. 255.0 on death flash (yellow: R+G=255), 200.0 on some death flashes
        internal const int FlashColorBlue   = 0x138; // float — 0.0 at rest; e.g. 255.0 on some death flashes (pale purple: R+B=255)
        // FlashActivation (+0x164): 0 at rest, 1 or 2 during flash event
        // FlashDuration (+0x168): 0.08 or 0.20 observed
        // Unk16C (+0x16C): 0.0 at rest; ~0.4–0.8 during flash events — possibly flash speed or intensity
        // +0x140/+0x144/+0x148: live attack hitbox XYZ extents — written by game engine, 0 at floor load then reset to resting defaults.
        // Resting: X=10.0, Y=20.0, Z=20.0. During Captain attack: X=137.4 (forward reach expands), Y=9.6, Z=9.6 (narrows to a lunge).
        // Cannot be permanently set via one-time write — game overwrites at 60fps.
        internal const int HitboxX          = 0x140; // float — resting 10.0; expands on attack (forward reach)
        internal const int HitboxY          = 0x144; // float — resting 20.0; narrows during attack swing
        internal const int HitboxZ          = 0x148; // float — resting 20.0; narrows during attack swing
        internal const int HitboxW          = 0x14C; // float — resting 128.0; appears constant during attacks observed so far
        // +0x150/+0x154/+0x158: non-zero for some enemy types at spawn (e.g. Auntie Medu: 127.5, 80.0, 15.0)
        // Possibly a second hitbox set, AI range values, or special-attack parameters. Zero for basic enemies.
        internal const int Unk150           = 0x150;
        internal const int Unk154           = 0x154;
        internal const int Unk158           = 0x158;
        internal const int Unk160           = 0x160; // binary flag (0/1) — toggles in sync with attack timing
        internal const int FlashActivation  = 0x164; // 0 at rest, 1 or 2 during flash event
        internal const int FlashDuration    = 0x168; // float — 0.08 or 0.20 observed
        internal const int Unk16C           = 0x16C; // float — 0.0 at rest; ~0.4–0.8 during flash events; possibly flash speed or intensity
        // +0x0C4: attack phase counter — -1 at rest, briefly becomes a small positive int (e.g. 10) during active hitbox window
        internal const int AttackPhase      = 0x0C4;
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
    /// Dungeon metadata and the set of enemy enemy type ID IDs that naturally spawn there.
    /// Enemy lists for Shipwreck are confirmed from gameplay; all others are estimates
    /// from game knowledge and should be verified via dump sessions.
    /// </summary>
    internal struct DungeonData
    {
        internal byte     Id;
        internal string   Name;
        // enemy type ID IDs of enemies that naturally spawn in this dungeon. Null = not yet catalogued.
        internal ushort[] EnemyIds;
    }

    /// <summary>
    /// Default stats per enemy type, keyed by enemy type ID ushort.
    /// Populated from unmodified floor dumps. Null fields are unconfirmed.
    /// Note: miniboss variants share the same enemy type ID ID as their normal counterpart but
    /// will have different stats — never populate these defaults from miniboss slot data.
    /// </summary>
    internal static class EnemyDatabase
    {
        internal static readonly Dictionary<ushort, EnemyDefaults> Defaults =
            new Dictionary<ushort, EnemyDefaults>
        {
            // ── Shipwreck (dungeon 2) ──────────────────────────────────────────────

            // Captain (id=27) — confirmed from clean dump 2026-05-29
            { 27, new EnemyDefaults {
                Id=27, Name="Captain", MaxHp=225, Abs=6, MinGoldDrop=12, DropChance=30,
                Type=1,   FireRes=110, IceRes=100, ThunderRes=80,  WindRes=80,  HolyRes=150,
                EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
                ReticleWidth=1.0f, ReticleHeight=1.0f,
                StealItemId=177, ItemResA=100, ItemResB=70 } },

            // Pirate's Chariot (id=25) — confirmed from clean dump 2026-05-29
            { 25, new EnemyDefaults {
                Id=25, Name="Pirate's Chariot", MaxHp=270, Abs=8, MinGoldDrop=15, DropChance=30,
                Type=7,   FireRes=120, IceRes=80,  ThunderRes=140, WindRes=100, HolyRes=100,
                EntityScale=8.0f, EntityScaleCopy=8.0f, Unk090A=5, Unk090B=30,
                ReticleWidth=1.9f, ReticleHeight=1.8f,
                StealItemId=159, ItemResA=95, ItemResB=60 } },

            // Gunny (id=23) — confirmed from clean dump 2026-05-29
            { 23, new EnemyDefaults {
                Id=23, Name="Gunny", MaxHp=250, Abs=4, MinGoldDrop=8, DropChance=30,
                Type=2,   FireRes=120, IceRes=100, ThunderRes=150, WindRes=120, HolyRes=100,
                EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=5, Unk090B=20,
                ReticleWidth=1.5f, ReticleHeight=1.5f,
                StealItemId=153, ItemResA=95, ItemResB=70 } },

            // Cursed Rose (id=68) — confirmed from clean dump 2026-05-29
            { 68, new EnemyDefaults {
                Id=68, Name="Cursed Rose", MaxHp=225, Abs=4, MinGoldDrop=6, DropChance=30,
                Type=4,   FireRes=150, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=130,
                EntityScale=6.5f, EntityScaleCopy=6.5f, Unk090A=2, Unk090B=0,
                ReticleWidth=1.4f, ReticleHeight=1.6f,
                StealItemId=null, ItemResA=100, ItemResB=70 } }, // StealItemId=0xFFFF → no steal item

            // Gyon (id=24) — confirmed from clean dump 2026-05-30
            { 24, new EnemyDefaults {
                Id=24, Name="Gyon", MaxHp=225, Abs=4, MinGoldDrop=8, DropChance=30,
                Type=2,   FireRes=120, IceRes=100, ThunderRes=150, WindRes=100, HolyRes=100,
                EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=0, Unk090B=0,
                ReticleWidth=1.4f, ReticleHeight=1.6f,
                StealItemId=134, ItemResA=100, ItemResB=70 } },

            // Auntie Medu (id=26) — confirmed from clean dump 2026-05-30
            // Note: Unk150/154/158 were observed non-zero in earlier live polls; dump shows 0 at spawn — game-populated during active combat.
            { 26, new EnemyDefaults {
                Id=26, Name="Auntie Medu", MaxHp=300, Abs=10, MinGoldDrop=15, DropChance=30,
                Type=0,   FireRes=100, IceRes=140, ThunderRes=100, WindRes=100, HolyRes=100,
                EntityScale=6.0f, EntityScaleCopy=6.0f, Unk090A=3, Unk090B=0,
                ReticleWidth=1.4f, ReticleHeight=1.4f,
                StealItemId=166, ItemResA=100, ItemResB=60 } },

            // Corcea (id=28) — stub; needs clean dump
            { 28, new EnemyDefaults { Id=28, Name="Corcea" } },

            // Mask of Prajna (id=75) — confirmed from clean dump 2026-05-30
            { 75, new EnemyDefaults {
                Id=75, Name="Mask of Prajna", MaxHp=375, Abs=12, MinGoldDrop=15, DropChance=50,
                Type=1,   FireRes=100, IceRes=100, ThunderRes=100, WindRes=100, HolyRes=145,
                EntityScale=7.0f, EntityScaleCopy=7.0f, Unk090A=5, Unk090B=10,
                ReticleWidth=1.0f, ReticleHeight=1.0f,
                StealItemId=151, ItemResA=80, ItemResB=70 } },

            // King Mimic (Shipwreck) (id=80) — stub; needs clean dump
            { 80, new EnemyDefaults { Id=80, Name="King Mimic (Shipwreck)" } },

            // Mimic (Shipwreck) (id=81) — stub; needs clean dump
            { 81, new EnemyDefaults { Id=81, Name="Mimic (Shipwreck)" } },

            // Sam (id=85) — stub; needs clean dump
            { 85, new EnemyDefaults { Id=85, Name="Sam" } },
        };
    }

    /// <summary>
    /// Per-dungeon metadata repository, keyed by dungeon ID byte.
    /// Dungeon IDs: 0=Divine Beast Cave, 1=Wise Owl, 2=Shipwreck,
    ///              3=Sun and Moon, 4=Moon Sea, 5=Gallery of Time, 6=Demon Shaft.
    /// </summary>
    internal static class DungeonDatabase
    {
        // Enemy lists for Shipwreck are confirmed from dump/gameplay sessions.
        // Enemy lists for all other dungeons are estimated from game knowledge
        // and should be verified via future dump sessions.

        internal static readonly DungeonData DivineBeastCave = new DungeonData
        {
            Id = 0, Name = "Divine Beast Cave",
            EnemyIds = new ushort[] { 6, 7, 10, 11, 12, 34, 35 },
        };

        internal static readonly DungeonData WiseOwl = new DungeonData
        {
            Id = 1, Name = "Wise Owl Forest",
            EnemyIds = new ushort[] { 8, 14, 15, 16, 17, 18, 19, 20, 30, 31, 32, 33, 78, 79 },
        };

        internal static readonly DungeonData Shipwreck = new DungeonData
        {
            Id = 2, Name = "Shipwreck",
            EnemyIds = new ushort[] { 23, 24, 25, 26, 27, 28, 68, 75, 80, 81, 85 },
        };

        internal static readonly DungeonData SunAndMoon = new DungeonData
        {
            Id = 3, Name = "Sun and Moon Temple",
            EnemyIds = new ushort[] { 1, 3, 5, 36, 37, 40, 43, 44, 45, 46, 47, 48 },
        };

        internal static readonly DungeonData MoonSea = new DungeonData
        {
            Id = 4, Name = "Moon Sea",
            EnemyIds = new ushort[] { 38, 39, 49, 50, 52, 54, 55, 56, 57, 59 },
        };

        internal static readonly DungeonData GalleryOfTime = new DungeonData
        {
            Id = 5, Name = "Gallery of Time",
            EnemyIds = new ushort[] { 62, 63, 64, 65, 66, 67, 69, 70, 71, 72, 73, 74, 76, 77, 82, 83 },
        };

        internal static readonly DungeonData DemonShaft = new DungeonData
        {
            Id = 6, Name = "Demon Shaft",
            EnemyIds = new ushort[] { 301, 303, 304, 305, 306, 308, 309, 310 },
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
