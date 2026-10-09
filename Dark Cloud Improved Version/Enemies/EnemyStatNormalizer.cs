using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Pool-bounds enemy stat normalization for the randomizer.
    ///
    /// When the randomizer places a species outside its home region, its stats are either trivial (an early enemy
    /// deep in) or a wall (a late enemy early on). Each stat is bounded by the region's own spawns, per stat:
    ///   • from a LOWER region:  value = max(native, poolAverage)  — lifted to the pool's mean, never lowered;
    ///   • from a HIGHER region: value = min(native, cap)          — cap = the highest pool max of this region and
    ///                                                              every region before it, never raised.
    /// The pool is the vanilla stats of the region's regular spawns and mimics (bosses and boss support entities
    /// are neither in it nor normalized). A species in the region's own vanilla pool is left alone. Then the
    /// similar-enemy order (<see cref="EnemySpecies.SimilarEnemies"/>) is enforced: walking a chain from its
    /// strongest member down, each member is capped at the next one's value, so a Cave Bat never out-stats an Evil
    /// Bat on the same floor. Stats: HP, ABS, damage reduction, weapon defense, melee and projectile damage; a
    /// 0 melee or projectile means the species has none and stays 0.
    ///
    /// Regions: the six dungeons and the five Demon Shaft floor bands (1-20 … 81-100); a species' home region is the
    /// lowest one it appears in. "Stronger enemies" takes every species' value under the rule and scales it by the next
    /// region's pool average over this region's, so natives move up a region keeping their standing in the pool; past
    /// the last band the next average continues the last two bands' trend; the ratio is never below 1, so Stronger
    /// never lowers a stat. Mimics are one enemy whose variants are vanilla's own normalization, so they keep their
    /// variant's stats wherever they are placed, and with Stronger on are scaled like everything else but at least
    /// the next variant's (the last variants use <see cref="EnemySpecies.MimicBeyond"/> / <see cref="EnemySpecies.KingMimicBeyond"/>). ABS is
    /// exempt: the reward follows the current region whatever the toggle.
    ///
    /// All writes target LIVE per-slot fields in one sweep per floor (enemies all spawn at load, no respawns):
    /// HP/ABS/defense → the enemy slot; melee → the cached _SET_DMG_PARA array; projectile → the per-species STB
    /// shot literal or the static BehaviorScriptTable default (snapshotted, restored on dungeon exit by
    /// <see cref="EnemyStatScaler.RestoreBst"/>). Gated by <see cref="NormalizeEnemyStats"/> / <see cref="NormalizeDamage"/>.
    /// </summary>
    internal static class EnemyStatNormalizer
    {
        // ── Config ───────────────────────────────────────────────────────────────────────────────────
        internal static bool  NormalizeEnemyStats   = true;  // master on/off
        internal static bool  NormalizeDamage       = true;  // also bound attack damage (melee + projectile)
        internal static bool  LogNormalize          = true;  // verbose per-enemy logging
        // "Stronger enemies" difficulty (Options → Harder Enemies → Stronger enemies): every enemy, native included,
        // is scaled by the next region's pool average over the current one's (a virtual region past the top band).
        internal static bool  StrongerEnemies       = false;

        private const int DungeonCount = 7;
        private const int RegionCount  = 11;   // 0..5 = the first six dungeons, 6..10 = Demon Shaft bands
        private const int Beyond       = 11;   // the virtual region above the last band (Stronger enemies), its average extrapolated
        private const int DemonShaft   = 6;    // dungeon id
        private const int DsBandCount  = 5;
        private const int DsBandFloors = 20;

        internal enum Stat { Hp, Abs, Dr, Wd, Melee, Proj }
        private static readonly Stat[] Stats = (Stat[])Enum.GetValues(typeof(Stat));

        // ── Reference data, built once from the C# data model ────────────────────────────────────────
        private static bool _init;
        private static readonly Dictionary<int, EnemyDefaults> _byTableIndex = new();   // every species, keyed by TableIndex
        private static readonly Dictionary<int, List<int>>     _byId         = new();   // enemy Id -> TableIndices (>1 = base/enhanced variants)
        private static readonly Dictionary<int, int>           _homeRegion   = new();   // TableIndex -> lowest region it spawns in
        private static readonly HashSet<int>[]                 _poolMembers  = new HashSet<int>[RegionCount];
        private static readonly float[][]                      _poolAvg      = new float[RegionCount + 1][];   // [region][stat]; 0 = no sample; [Beyond] extrapolated
        private static readonly float[][]                      _poolCap      = new float[RegionCount][];       // running max over regions 0..r
        private static readonly Dictionary<int, EnemyDefaults[]> _chainOf    = new();   // TableIndex -> its similar-enemy chain
        private static readonly Dictionary<int, (EnemyDefaults[] line, int index)> _mimicLine = new();   // TableIndex -> its mimic line and position

        // ── Per-floor state ───────────────────────────────────────────────────────────────────────────
        private static int _normalizedKey = -1;   // packed (dungeon<<16 | floor<<8 | backfloorBit) of the current floor context
        private static int _curRegion;
        private static readonly Dictionary<int, int> _swept = new(); // slot -> species already normalized (patch once on spawn)
        // Slots whose projectile scaling hasn't landed yet: the STB script pointer (CRunScript+0x3C) attaches a few
        // ticks AFTER the CCharacter slot goes live, so the write is retried each sweep pass until the STB is ready.
        private static readonly Dictionary<int, (int tableIndex, float factor)> _projPending = new();
        private static bool _sweepDone;     // set once the floor's enemies are loaded + normalized; stops the per-tick sweep
        private static int  _sweepIdle;     // consecutive sweep passes with nothing new to patch

        /// <summary>A regular floor enemy or a mimic: in the pools and normalizable. Bosses and their support entities are neither.</summary>
        private static bool IsFloorEnemy(int tableIndex) =>
            EnemySpecies.RandomizerValid.ContainsKey(tableIndex)
            || (_byTableIndex.TryGetValue(tableIndex, out EnemyDefaults d) && d.Name != null && d.Name.Contains("Mimic") && d.ModelCode != null && d.ModelCode[0] == 'e');

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Init
        // ════════════════════════════════════════════════════════════════════════════════════════════
        private static void EnsureInit()
        {
            if (_init) return;
            BuildTableIndexMap();
            BuildRegions();
            BuildPools();
            foreach (EnemyDefaults[] chain in EnemySpecies.SimilarEnemies)
                foreach (EnemyDefaults member in chain)
                    if (member.TableIndex.HasValue) _chainOf[member.TableIndex.Value] = chain;
            foreach (EnemyDefaults[] line in new[] { EnemySpecies.MimicLine, EnemySpecies.KingMimicLine })
                for (int i = 0; i < line.Length; i++)
                    if (line[i].TableIndex.HasValue) _mimicLine[line[i].TableIndex.Value] = (line, i);
            _init = true;
            Console.WriteLine($"[Normalize] init: {_byTableIndex.Count} species, {_homeRegion.Count} in pools, {EnemySpecies.SimilarEnemies.Length} similar-enemy chains.");
            for (int r = 0; r <= RegionCount; r++)
                Console.WriteLine($"[Normalize] {RegionName(r)}: {(r < RegionCount ? _poolMembers[r].Count : 0)} spawns; avg/cap " +
                                  string.Join(" ", Stats.Select(s => $"{s}={_poolAvg[r][(int)s]:F0}/{(r < RegionCount ? _poolCap[r][(int)s] : 0):F0}")));
        }

        internal static string RegionName(int region) =>
            region < DemonShaft ? (Dungeons.TryGetValue((byte)region, out DungeonData d) ? d.Name : $"dungeon {region}")
            : region < RegionCount ? $"Demon Shaft {(region - DemonShaft) * DsBandFloors + 1}-{(region - DemonShaft + 1) * DsBandFloors}"
            : "beyond Demon Shaft (virtual)";

        // Demon Shaft floor (1..100) -> band 0..4. Floor 0/descriptor -> band 0.
        private static int DsBand(int floor) => Math.Max(0, Math.Min(DsBandCount - 1, (floor - 1) / DsBandFloors));

        /// <summary>The region of a (dungeon, floor): dungeons 0..5 map straight through; Demon Shaft expands to 6 + band.</summary>
        internal static int RegionOf(int dungeon, int floor) => dungeon < DemonShaft ? dungeon : DemonShaft + DsBand(floor);

        // Reflect over every `static EnemyDefaults` field on EnemySpecies so all 162 entries are captured (incl.
        // enhanced variants that share an Id and so collide in EnemySpecies.Defaults). Keyed by unique TableIndex.
        private static void BuildTableIndexMap()
        {
            foreach (FieldInfo field in typeof(EnemySpecies).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(EnemyDefaults)) continue;
                var enemyDefaults = (EnemyDefaults)field.GetValue(null);
                if (!enemyDefaults.TableIndex.HasValue) continue;
                _byTableIndex[enemyDefaults.TableIndex.Value] = enemyDefaults;
                (_byId.TryGetValue(enemyDefaults.Id, out var tiList) ? tiList : (_byId[enemyDefaults.Id] = [])).Add(enemyDefaults.TableIndex.Value);
            }
        }

        // Region membership and home regions from the vanilla pools. Front[0] is a descriptor (Front[N] = floor N);
        // Back has none (Back[k] = the back side of floor k+1).
        private static void BuildRegions()
        {
            for (int r = 0; r < RegionCount; r++) _poolMembers[r] = new HashSet<int>();
            for (int d = 0; d < DungeonCount; d++) // ascending, so the first (lowest) region wins
            {
                if (!Dungeons.TryGetValue((byte)d, out DungeonData dd)) continue;
                foreach (FloorSpawnPool[] pools in new[] { dd.Front, dd.Back })
                {
                    if (pools == null) continue;
                    bool back = ReferenceEquals(pools, dd.Back);
                    for (int i = 0; i < pools.Length; i++)
                    {
                        int[] tableIndices = pools[i].TableIndices;
                        if (tableIndices == null) continue;
                        int region = RegionOf(d, back ? i + 1 : i);
                        foreach (int tableIndex in tableIndices)
                        {
                            _poolMembers[region].Add(tableIndex);
                            if (!_homeRegion.TryGetValue(tableIndex, out int prev) || region < prev) _homeRegion[tableIndex] = region;
                        }
                    }
                }
            }
            // Mod species have no pool of their own: each takes the home of the vanilla species it stands beside.
            foreach (var kv in EnemySpecies.HomeOf)
                if (!_homeRegion.ContainsKey(kv.Key) && _homeRegion.TryGetValue(kv.Value, out int home)) _homeRegion[kv.Key] = home;
        }

        // Per region and stat: the mean over the region's floor enemies, and the running maximum over this region and
        // every earlier one. A 0 melee/projectile means "none" and is left out of both.
        private static void BuildPools()
        {
            for (int r = 0; r < RegionCount; r++)
            {
                _poolAvg[r] = new float[Stats.Length]; _poolCap[r] = new float[Stats.Length];
                foreach (Stat stat in Stats)
                {
                    var samples = new List<int>();
                    foreach (int ti in _poolMembers[r])
                    {
                        if (!IsFloorEnemy(ti) || !_byTableIndex.TryGetValue(ti, out EnemyDefaults d)) continue;
                        int? v = NativeValue(d, stat);
                        if (v == null || (IsAttack(stat) && v <= 0)) continue;
                        samples.Add(v.Value);
                    }
                    float avg = samples.Count > 0 ? (float)samples.Average() : 0;
                    float max = samples.Count > 0 ? samples.Max() : 0;
                    _poolAvg[r][(int)stat] = avg;
                    _poolCap[r][(int)stat] = Math.Max(max, r > 0 ? _poolCap[r - 1][(int)stat] : 0);
                }
            }
            // The virtual region above the last band: its average continues the last two bands' trend (never below the last band).
            _poolAvg[Beyond] = new float[Stats.Length];
            foreach (Stat stat in Stats)
            {
                int last = RegionCount - 1, i = (int)stat;
                _poolAvg[Beyond][i] = Math.Max(_poolAvg[last][i], 2 * _poolAvg[last][i] - _poolAvg[last - 1][i]);
            }
        }

        private static bool IsAttack(Stat stat) => stat == Stat.Melee || stat == Stat.Proj;

        /// <summary>The species' vanilla value of a stat (melee/projectile = its strongest hit), or null when the record lacks it.</summary>
        internal static int? NativeValue(in EnemyDefaults d, Stat stat) => stat switch
        {
            Stat.Hp    => d.MaxHp,
            Stat.Abs   => d.Abs,
            Stat.Dr    => d.DamageReduction,
            Stat.Wd    => d.WeaponDefense,
            Stat.Melee => Representative(d.MeleeDamage),
            Stat.Proj  => Representative(d.ProjectileDamage),
            _ => null,
        };

        // A species' representative attack = its strongest hit (ignores the -1 "engine default" projectile marker).
        private static int Representative(int[] a)
        {
            if (a == null || a.Length == 0) return 0;
            int best = 0;
            foreach (int v in a) if (v > best) best = v;
            return best;
        }

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // The rule
        // ════════════════════════════════════════════════════════════════════════════════════════════
        // The pool rule alone: the species' stat in `region` before the similar-enemy order is applied. With Stronger
        // enemies the result is scaled by the next region's average over this one's (ABS exempt).
        private static int? PoolValue(int tableIndex, Stat stat, int region, bool stronger)
        {
            if (!_byTableIndex.TryGetValue(tableIndex, out EnemyDefaults d)) return null;
            int? native = NativeValue(d, stat);
            if (native == null) return null;
            if (!_homeRegion.TryGetValue(tableIndex, out int home) || !IsFloorEnemy(tableIndex)) return native;
            int value = native.Value;
            bool mimic = _mimicLine.TryGetValue(tableIndex, out var at);   // a mimic keeps its variant's stats wherever it is placed
            if (!mimic && !_poolMembers[region].Contains(tableIndex) && !(IsAttack(stat) && native <= 0))   // not one of the region's own spawns
            {
                float avg = _poolAvg[region][(int)stat], cap = _poolCap[region][(int)stat];
                if (home < region && avg > 0) value = Math.Max(value, (int)Math.Round(avg, MidpointRounding.AwayFromZero));
                if (home > region && cap > 0) value = Math.Min(value, (int)Math.Round(cap, MidpointRounding.AwayFromZero));
            }
            if (!stronger || stat == Stat.Abs || value <= 0) return value;
            int ratio = StrongerRatio(stat, region);   // in ten-thousandths, so the product is exact in a double
            if (ratio != 10000) value = Math.Max(1, (int)Math.Round(value * ratio / 10000.0, MidpointRounding.AwayFromZero));
            if (mimic) { int? next = NextMimicStat(at.line, at.index, stat); if (next.HasValue) value = Math.Max(value, next.Value); }   // at least the next variant
            return value;
        }

        /// <summary>Stronger enemies: the next region's pool average over this region's in ten-thousandths, never below
        /// 10000 (10000 when either is unsampled). Four decimals keep the ratio identical on every runtime.</summary>
        private static int StrongerRatio(Stat stat, int region)
        {
            float cur = _poolAvg[region][(int)stat], next = _poolAvg[region + 1][(int)stat];
            return cur > 0 && next > 0 ? Math.Max(10000, (int)Math.Round((double)next / cur * 10000)) : 10000;
        }

        // The vanilla stat of the mimic line's next variant, or the extrapolated stats past its last one.
        private static int? NextMimicStat(EnemyDefaults[] line, int index, Stat stat)
        {
            if (index + 1 < line.Length) return NativeValue(line[index + 1], stat);
            var beyond = ReferenceEquals(line, EnemySpecies.KingMimicLine) ? EnemySpecies.KingMimicBeyond : EnemySpecies.MimicBeyond;
            return stat switch { Stat.Hp => beyond.hp, Stat.Dr => beyond.dr, Stat.Wd => beyond.wd, Stat.Melee => beyond.melee, _ => null };
        }

        /// <summary>The species' stat as it should be in <paramref name="region"/>: the pool rule, then the similar-enemy
        /// order (each chain member capped at the next stronger member's value). Null when the record lacks the stat.</summary>
        internal static int? TargetValue(int tableIndex, Stat stat, int region, bool stronger)
        {
            if (!_chainOf.TryGetValue(tableIndex, out EnemyDefaults[] chain)) return PoolValue(tableIndex, stat, region, stronger);
            int cap = int.MaxValue; int? result = null;
            for (int i = chain.Length - 1; i >= 0; i--)
            {
                if (!chain[i].TableIndex.HasValue) continue;
                int memberTi = chain[i].TableIndex.Value;
                int? v = PoolValue(memberTi, stat, region, stronger);
                if (v != null && (!IsAttack(stat) || v > 0)) { v = Math.Min(v.Value, cap); cap = v.Value; }
                if (memberTi == tableIndex) { result = v; break; }
            }
            return result;
        }

        private static float Factor(int tableIndex, Stat stat, int region, bool stronger)
        {
            int? native = _byTableIndex.TryGetValue(tableIndex, out EnemyDefaults d) ? NativeValue(d, stat) : null;
            int? target = TargetValue(tableIndex, stat, region, stronger);
            if (native == null || target == null || native <= 0 || target == native) return 1f;
            return target.Value / (float)native.Value;
        }

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Per-floor entry point
        // ════════════════════════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Call each dungeon-thread tick. Sweeps live slots and normalizes each enemy once when it first appears (the
        /// floor's enemies all spawn at load), then self-terminates for the floor. No-op when disabled or in town;
        /// a region's own spawns are left untouched, so it is harmless on vanilla floors.
        /// </summary>
        internal static void NormalizeStatsForFloor()
        {
            if (!NormalizeEnemyStats && !StrongerEnemies) return;
            // Leaving the floor (town / dungeon exit) must invalidate the cached key, so that RE-entering the same
            // dungeon+floor re-runs the sweep. The game reloads fresh native-value STBs/slots on every entry, so a
            // persisted key (same floor number) would otherwise skip normalization on the 2nd+ visit.
            if (!Player.InDungeonFloor()) { _normalizedKey = -1; EnemyStatScaler.RestoreBst(); return; }

            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            int floor   = Memory.ReadByte(Addresses.checkFloor);
            if (dungeon < 0 || dungeon >= DungeonCount) return;

            EnsureInit();
            // Include the backfloor bit: floor↔backfloor swaps reload FloorSlots with fresh native-stat enemies but
            // keep checkFloor the same, so without this the sweep never re-runs and backfloor enemies go un-normalized.
            // (Each transition respawns fresh, so re-sweeping reads native MaxHp again — no double-scaling.)
            int back = Memory.ReadByte(Addresses.dunBackFloorFlag) != 0 ? 1 : 0;
            int key = (dungeon << 16) | ((floor & 0xFF) << 8) | back;

            if (key != _normalizedKey)
            {
                _normalizedKey = key;
                _curRegion = RegionOf(dungeon, floor);
                _swept.Clear(); _projPending.Clear(); EnemyStatScaler.ResetFloor(); _sweepDone = false; _sweepIdle = 0; // new floor: sweep until its enemies are loaded
                EnemyStatScaler.Verbose = LogNormalize;
                if (LogNormalize)
                    Console.WriteLine($"[Normalize] enter dungeon {dungeon} floor {floor}{(back != 0 ? " BACKFLOOR" : "")} ({RegionName(_curRegion)}); dmg={(NormalizeDamage ? "on" : "off")}{(StrongerEnemies ? ", stronger" : "")}.");
            }

            // All of a floor's enemies spawn at load (no respawns), so the live slots are swept — patching each enemy's
            // slot fields directly — until they've all appeared, then the sweep stops. The enemies often spawn a few
            // ticks AFTER the floor-enter event (the floor key updates before BtLoadMonstor populates the slots), so
            // the sweep must NOT give up before any enemy is seen — only settle once enemies have appeared and no new
            // ones show for several passes. The no-enemy-yet cap must comfortably exceed worst-case spawn latency: a
            // RANDOMIZED floor loads up to 9 distinct enemy models, which can take several seconds (well past 200
            // ticks). A large cap still bails out on genuinely enemy-less floors (event floors), just later.
            if (!_sweepDone)
            {
                int normalized = SweepLiveSlots(_curRegion);
                if (normalized > 0) _sweepIdle = 0; else _sweepIdle++;
                // Don't settle while any slot still owes a projectile patch (its STB hasn't attached yet); the 1500
                // hard cap still bails out if an STB never loads, so this can't hang.
                if ((_swept.Count > 0 && _projPending.Count == 0 && _sweepIdle >= 8) || _sweepIdle >= 1500) _sweepDone = true;
            }
        }

        // Normalize each newly-seen live enemy once. Maps the slot's species Id to a TableIndex via the global _byId
        // map (disambiguating shared base/enhanced Ids by the slot's spawn MaxHp). Returns the number of enemies newly
        // normalized this pass.
        private static int SweepLiveSlots(int region)
        {
            int normalized = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long slotBase = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slotBase + EnemySlotOffsets.RenderStatus) < 0) continue; // empty slot
                int id = Memory.ReadUShort(slotBase + EnemySlotOffsets.EnemySpeciesId);
                if (_swept.TryGetValue(s, out int prevSpeciesId) && prevSpeciesId == id)
                {
                    // Already normalized (the slot writes are one-shot). Only the projectile may still be owed, if its
                    // STB script pointer wasn't attached when this slot was first swept — retry until it lands.
                    if (_projPending.TryGetValue(s, out var pending)
                        && EnemyStatScaler.ScaleProjectile(s, pending.tableIndex, pending.factor))
                    {
                        _projPending.Remove(s); normalized++;
                    }
                    continue;
                }
                if (!_byId.TryGetValue(id, out var tableIndices)) continue;   // not a known species
                _swept[s] = id; normalized++;                                // normalize once (enemies spawn at load, no respawn)

                int slotMaxHp = Memory.ReadInt(slotBase + EnemySlotOffsets.MaxHp);
                int tableIndex = tableIndices.Count == 1 ? tableIndices[0]
                                        : tableIndices.OrderBy(candidate => Math.Abs((_byTableIndex[candidate].MaxHp ?? 0) - slotMaxHp)).First();
                EnemyDefaults enemyDefaults = _byTableIndex[tableIndex];
                bool floorEnemy = IsFloorEnemy(tableIndex) && _homeRegion.ContainsKey(tableIndex);
                bool inPool = floorEnemy && _poolMembers[region].Contains(tableIndex);
                bool native = inPool && !StrongerEnemies;   // Stronger scales the region's own spawns too
                if (LogNormalize)
                    Console.WriteLine($"[Normalize] slot {s} {enemyDefaults.Name} (id {id}, ti {tableIndex}): home {(_homeRegion.TryGetValue(tableIndex, out int home) ? RegionName(home) : "none")}" +
                                      (!floorEnemy ? " — boss/support, skip" : native ? " — native, skip" : ""));
                if (!floorEnemy || native) continue;

                // HP / ABS / defense (per-slot).
                if (enemyDefaults.MaxHp.HasValue) EnemyStatScaler.ScaleHp(s, Factor(tableIndex, Stat.Hp, region, StrongerEnemies));
                if (enemyDefaults.Abs.HasValue && !inPool) EnemyStatScaler.ScaleAbs(s, Factor(tableIndex, Stat.Abs, region, false));
                if (enemyDefaults.DamageReduction.HasValue || enemyDefaults.WeaponDefense.HasValue)
                    EnemyStatScaler.ScaleDefense(s, Factor(tableIndex, Stat.Dr, region, StrongerEnemies),
                                                    Factor(tableIndex, Stat.Wd, region, StrongerEnemies));

                // Damage — melee (per-slot cache) + projectile (per-species STB / BST).
                if (NormalizeDamage || StrongerEnemies)
                {
                    EnemyStatScaler.ScaleMelee(s, enemyDefaults.MeleeDamage, Factor(tableIndex, Stat.Melee, region, StrongerEnemies));
                    float projFactor = Factor(tableIndex, Stat.Proj, region, StrongerEnemies);
                    if (!EnemyStatScaler.ScaleProjectile(s, tableIndex, projFactor))
                        _projPending[s] = (tableIndex, projFactor);   // STB not attached yet — retry next sweep pass
                }
            }
            return normalized;
        }
    }
}
