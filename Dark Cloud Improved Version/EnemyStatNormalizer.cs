using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Gradient-based enemy stat normalization for the randomizer.
    ///
    /// When the randomizer injects a species into a dungeon that is not its native one, its stats are
    /// either trivially weak (early enemy in a late dungeon) or a brick wall (late enemy in an early
    /// dungeon). This rescales HP, defense and attack damage toward the CURRENT dungeon's power level
    /// along a per-dungeon gradient with a single tunable knob. Two tier systems (see below): ATTACK uses the
    /// 7 dungeons (Demon Shaft = one tier); HP/DEFENSE uses 11 (Demon Shaft split into its 5 floor bands).
    ///
    /// Native tier `n` (the dungeon a species belongs to), current dungeon `c`, knob `k` = <see cref="NormalizationStrength"/>:
    ///     effectiveTier e = max(c, n − (n − c)·k)          // "floor weak, tame strong"
    ///     factor_S        = lerp(Baseline_S, e) / Baseline_S[n]
    ///     newValue_S      = round(nativeValue_S · factor_S)
    /// Behavior:
    ///   • weak (n &lt; c): e clamps to c for any k ⇒ always buffed up to the current dungeon's level.
    ///   • native (n == c): factor 1 ⇒ untouched.
    ///   • strong (n &gt; c): k=0 full native power, k=1 flattened to current; in between, still above
    ///     current but tamed. The factor depends only on (n, c, k), so two same-tier species keep their
    ///     relative ordering (Holy Gemron stays above Fire Gemron — only the gap to the current dungeon closes).
    ///
    /// All writes target LIVE per-slot fields in one sweep per floor (enemies all spawn at load, no respawns) —
    /// no species-record patch:
    ///   • HP/defense → the enemy slot (MaxHp/Hp, packed DefenseStats).
    ///   • Melee damage → the cached _SET_DMG_PARA array (the value the engine latched at the enemy's init).
    ///   • Projectile damage → the per-slot shot cache (explicit shots) or, for "default" shots, the static
    ///     BehaviorScriptTable +0x3C entry.
    /// Slot + per-floor caches self-revert on floor reload; the static BST table is snapshotted and restored on
    /// dungeon exit (<see cref="EnemyStatScaler.RestoreBst"/>). Gated by <see cref="NormalizeEnemyStats"/> / <see cref="NormalizeDamage"/>.
    /// </summary>
    internal static class EnemyStatNormalizer
    {
        // ── Config (the single tuning surface) ───────────────────────────────────────────────────────
        internal static bool  NormalizeEnemyStats   = true;  // master on/off  (TESTING: default on)
        internal static float NormalizationStrength = 1.0f;  // k ∈ [0,1]: 0 = strong enemies run wild (weak still buffed), 1 = flatten all to current
        internal static bool  NormalizeDamage       = true;  // also rescale attack damage (melee + projectile)
        internal static bool  LogNormalize          = true;  // verbose per-enemy logging (TESTING)
        // "Stronger enemies" difficulty (Options → Harder Enemies → Stronger enemies). When on, normalize EVERY
        // enemy (native included) to the tier ONE ABOVE the current — HP/defense to the next HP/def tier (Demon
        // Shaft band), attack to the next dungeon — extrapolating a hypothetical band past the top when already at
        // the highest. Overrides the gradient knob while active. Scales HP, defense AND attack.
        internal static bool  StrongerEnemies       = false;

        private const int DungeonCount = 7;

        // Two tier systems:
        //  • ATTACK uses the 7 dungeons as-is (Demon Shaft = one tier 6). Melee/projectile damage is fairly flat
        //    within Demon Shaft, so one tier is representative.
        //  • HP/DEFENSE splits Demon Shaft into its 5 floor bands (its enemies' durability spans a huge range —
        //    ~1900 HP early to ~11500 deep — so a single DS median is unrepresentative). Tiers: 0..5 = the first
        //    six dungeons, 6..10 = Demon Shaft bands (floors 1-20, 21-40, 41-60, 61-80, 81-100).
        private const int AtkTierCount   = 7;
        private const int HpDefTierCount = 11;
        private const int DemonShaft     = 6;   // dungeon id
        private const int DsBandCount    = 5;
        private const int DsBandFloors   = 20;  // floors per Demon Shaft band

        // ── Lazily-built reference data (from the C# data model, once) ───────────────────────────────
        private static bool _init;
        private static readonly Dictionary<int, EnemyDefaults> _byTableIndex = new();   // every species, keyed by TableIndex
        private static readonly Dictionary<int, List<int>>     _byId         = new();   // enemy Id -> TableIndices (usually 1; >1 = base/enhanced variants)
        private static readonly Dictionary<int, int>           _atkTier      = new();   // TableIndex -> native ATTACK tier (0..6)
        private static readonly Dictionary<int, int>           _hpdefTier    = new();   // TableIndex -> native HP/DEF tier (0..10)
        // Baselines; 0 = no native sample for that stat in that tier (Factor falls back to 1).
        private static readonly float[] _bHp    = new float[HpDefTierCount];
        private static readonly float[] _bDr    = new float[HpDefTierCount];
        private static readonly float[] _bWd    = new float[HpDefTierCount];
        private static readonly float[] _bMelee = new float[AtkTierCount];
        private static readonly float[] _bProj  = new float[AtkTierCount];

        // ── Per-floor state ───────────────────────────────────────────────────────────────────────────
        private static int _normalizedKey = -1;   // packed (dungeon<<8 | floor) of the current floor
        private static int _curHpDef, _curAtk;    // current-floor tiers
        private static readonly Dictionary<int, int> _swept = new(); // slot -> species already normalized (patch once on spawn)
        private static bool _sweepDone;     // set once the floor's enemies are loaded + normalized; stops the per-tick sweep
        private static int  _sweepIdle;     // consecutive sweep passes with nothing new to patch
        // The actual per-stat RAM writes (HP/defense/melee/projectile + the shared per-floor STB dedup and the static
        // BST default-shot snapshot/restore) live in EnemyStatScaler; this class only computes the gradient factors.

        private static bool IsBoss(in EnemyDefaults enemyDefaults) =>
            !string.IsNullOrEmpty(enemyDefaults.ModelCode) && enemyDefaults.ModelCode[0] == 'c';

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Init
        // ════════════════════════════════════════════════════════════════════════════════════════════
        private static void EnsureInit()
        {
            if (_init) return;
            BuildTableIndexMap();
            BuildNativeTierMap();
            BuildBaselines();
            _init = true;
            Console.WriteLine($"[Normalize] init: {_byTableIndex.Count} species, {_atkTier.Count} placed in pools.");
            for (int t = 0; t < HpDefTierCount; t++)
            {
                string atk = t < AtkTierCount ? $" melee={_bMelee[t]:F0} proj={_bProj[t]:F0}" : "";
                string lbl = t < DemonShaft ? $"dungeon {t}" : $"DS band {t - DemonShaft} (fl {(t-DemonShaft)*DsBandFloors+1}-{(t-DemonShaft+1)*DsBandFloors})";
                Console.WriteLine($"[Normalize] HP/def tier {t} ({lbl}): HP={_bHp[t]:F0} DR={_bDr[t]:F0} WD={_bWd[t]:F0}{atk}");
            }
        }

        // Demon Shaft floor (1..100) -> band 0..4. Floor 0/descriptor -> band 0.
        private static int DsBand(int floor) => Math.Max(0, Math.Min(DsBandCount - 1, (floor - 1) / DsBandFloors));

        // HP/DEF tier for a (dungeon, floor): dungeons 0..5 map straight through; Demon Shaft expands to 6 + band.
        private static int HpDefTierOf(int dungeon, int floor) =>
            dungeon < DemonShaft ? dungeon : DemonShaft + DsBand(floor);

        // Reflect over every `static EnemyDefaults` field on Enemies so we capture all 162 entries (incl.
        // enhanced variants that share an Id and so collide in Enemies.Defaults). Keyed by unique TableIndex.
        private static void BuildTableIndexMap()
        {
            foreach (FieldInfo field in typeof(Enemies).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(EnemyDefaults)) continue;
                var enemyDefaults = (EnemyDefaults)field.GetValue(null);
                if (!enemyDefaults.TableIndex.HasValue) continue;
                _byTableIndex[enemyDefaults.TableIndex.Value] = enemyDefaults;
                (_byId.TryGetValue(enemyDefaults.Id, out var tiList) ? tiList : (_byId[enemyDefaults.Id] = [])).Add(enemyDefaults.TableIndex.Value);
            }
        }

        // Native tiers from the vanilla pools: ATTACK = lowest dungeon (0..6); HP/DEF = lowest tier where the
        // lowest takes Demon Shaft floor bands into account (so a deep-DS enemy gets a higher HP/def tier than a
        // shallow-DS one, even though both are attack tier 6).
        private static void BuildNativeTierMap()
        {
            for (int d = 0; d < DungeonCount; d++) // ascending, so the first (lowest) wins
            {
                if (!Dungeons.TryGetValue((byte)d, out DungeonData dd)) continue;
                foreach (FloorSpawnPool[] pools in new[] { dd.Front, dd.Back })
                {
                    if (pools == null) continue;
                    for (int floor = 0; floor < pools.Length; floor++)
                    {
                        int[] tableIndices = pools[floor].TableIndices;
                        if (tableIndices == null) continue;
                        int hpdef = HpDefTierOf(d, floor); // for DS this varies by floor band
                        foreach (int tableIndex in tableIndices)
                        {
                            if (!_atkTier.TryGetValue(tableIndex, out int prevAtkTier) || d < prevAtkTier)         _atkTier[tableIndex]   = d;
                            if (!_hpdefTier.TryGetValue(tableIndex, out int prevHpDefTier) || hpdef < prevHpDefTier)    _hpdefTier[tableIndex] = hpdef;
                        }
                    }
                }
            }
        }

        // Baselines = median of the stat over each tier's native, non-boss species. HP/DR/WD use the 11 HP/def
        // tiers; melee/proj use the 7 attack tiers.
        private static void BuildBaselines()
        {
            for (int t = 0; t < HpDefTierCount; t++)
            {
                var hpSamples = new List<double>(); var drSamples = new List<double>(); var wdSamples = new List<double>();
                foreach (var kv in _hpdefTier)
                {
                    if (kv.Value != t || !_byTableIndex.TryGetValue(kv.Key, out EnemyDefaults enemyDefaults) || IsBoss(enemyDefaults)) continue;
                    if (enemyDefaults.MaxHp.HasValue)           hpSamples.Add(enemyDefaults.MaxHp.Value);
                    if (enemyDefaults.DamageReduction.HasValue) drSamples.Add(enemyDefaults.DamageReduction.Value);
                    if (enemyDefaults.WeaponDefense.HasValue)   wdSamples.Add(enemyDefaults.WeaponDefense.Value);
                }
                _bHp[t] = (float)Median(hpSamples); _bDr[t] = (float)Median(drSamples); _bWd[t] = (float)Median(wdSamples);
            }
            for (int t = 0; t < AtkTierCount; t++)
            {
                var meleeSamples = new List<double>(); var projSamples = new List<double>();
                foreach (var kv in _atkTier)
                {
                    if (kv.Value != t || !_byTableIndex.TryGetValue(kv.Key, out EnemyDefaults enemyDefaults) || IsBoss(enemyDefaults)) continue;
                    int meleeRep = Representative(enemyDefaults.MeleeDamage);      if (meleeRep > 0) meleeSamples.Add(meleeRep);
                    int projRep  = Representative(enemyDefaults.ProjectileDamage); if (projRep  > 0) projSamples.Add(projRep);
                }
                _bMelee[t] = (float)Median(meleeSamples); _bProj[t] = (float)Median(projSamples);
            }
            // The tiers are a difficulty ramp, so each baseline should be a rising trend. Sparse stats produce
            // noisy dips/gaps from tiny sample sizes — enforce non-decreasing (fill empty tiers, then cumulative-max).
            foreach (float[] baseline in new[] { _bHp, _bDr, _bWd, _bMelee, _bProj }) MakeMonotonic(baseline);
        }

        // Back-fill leading empty tiers from the first sampled value, then make the series non-decreasing.
        private static void MakeMonotonic(float[] baseline)
        {
            int first = Array.FindIndex(baseline, v => v > 0);
            if (first < 0) return;               // stat never sampled — leave all zero (Factor falls back to 1)
            for (int tier = 0; tier < first; tier++) baseline[tier] = baseline[first];
            for (int tier = 1; tier < baseline.Length; tier++) if (baseline[tier] < baseline[tier - 1]) baseline[tier] = baseline[tier - 1];
        }

        // A species' representative attack = its strongest hit (ignores the -1 "engine default" projectile marker).
        private static int Representative(int[] a)
        {
            if (a == null || a.Length == 0) return 0;
            int best = 0;
            foreach (int v in a) if (v > best) best = v;
            return best;
        }

        private static double Median(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            xs.Sort();
            int n = xs.Count;
            return n % 2 == 1 ? xs[n / 2] : 0.5 * (xs[n / 2 - 1] + xs[n / 2]);
        }

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Gradient math
        // ════════════════════════════════════════════════════════════════════════════════════════════
        private static float EffectiveTier(int nativeTier, int currentDungeon)
        {
            // "Stronger enemies": every enemy targets the tier ABOVE the current floor's, regardless of its native
            // tier or the knob. currentDungeon+1 can exceed the top tier (Demon Shaft) — Lerp extrapolates it.
            if (StrongerEnemies) return currentDungeon + 1;
            float k = Math.Max(0f, Math.Min(1f, NormalizationStrength));
            return Math.Max(currentDungeon, nativeTier - (nativeTier - currentDungeon) * k);
        }

        private static float Lerp(float[] baseline, float effectiveTier)
        {
            if (effectiveTier <= 0) return baseline[0];
            int last = baseline.Length - 1;
            if (effectiveTier >= last)
            {
                // At/above the top tier: extrapolate along the last segment's slope (the hypothetical dungeon/region
                // a "Stronger" target asks for past Demon Shaft / its highest band). effectiveTier == last → baseline[last].
                float vLast = baseline[last], vPrev = baseline[last - 1];
                if (vLast <= 0) return vPrev;
                if (vPrev <= 0) return vLast;
                return vLast + (effectiveTier - last) * (vLast - vPrev);
            }
            int i = (int)Math.Floor(effectiveTier);
            float frac = effectiveTier - i;
            float v0 = baseline[i], v1 = baseline[i + 1];
            if (v0 <= 0) return v1;           // no native sample in tier i — fall back to the neighbour
            if (v1 <= 0) return v0;
            return v0 * (1 - frac) + v1 * frac;
        }

        // Scale factor for a stat given its per-dungeon baseline series. 1.0 = leave unchanged.
        private static float Factor(float[] baseline, int nativeTier, int currentDungeon)
        {
            float baselineNative = baseline[nativeTier];
            if (baselineNative <= 0) return 1f;
            float baselineEffective = Lerp(baseline, EffectiveTier(nativeTier, currentDungeon));
            if (baselineEffective <= 0) return 1f;
            return baselineEffective / baselineNative;
        }

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Per-floor entry point
        // ════════════════════════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Call each dungeon-thread tick. Sweeps live slots and normalizes each non-native enemy once when it
        /// first appears (the floor's enemies all spawn at load), then self-terminates for the floor. No-op when
        /// disabled or in town; native species (factor 1) are left untouched, so it's harmless on vanilla floors.
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
            int key = (dungeon << 8) | (floor & 0xFF);

            if (key != _normalizedKey)
            {
                _normalizedKey = key;
                _curHpDef = HpDefTierOf(dungeon, floor); // 0..10
                _curAtk   = dungeon;                     // 0..6
                _swept.Clear(); EnemyStatScaler.ResetFloor(); _sweepDone = false; _sweepIdle = 0; // new floor: sweep until its enemies are loaded
                EnemyStatScaler.Verbose = LogNormalize;
                if (LogNormalize)
                    Console.WriteLine($"[Normalize] enter dungeon {dungeon} floor {floor} (HP/def tier {_curHpDef}, atk tier {_curAtk}); k={NormalizationStrength:F2}, dmg={(NormalizeDamage ? "on" : "off")}.");
            }

            // All of a floor's enemies spawn at load (no respawns), so we just sweep the live slots — patching each
            // enemy's slot HP/defense and cached melee damage directly — until they've all appeared, then stop.
            // No species-record patch is needed (nothing spawns late to inherit it). The enemies often spawn a few
            // ticks AFTER the floor-enter event (the floor key updates before BtLoadMonstor populates the slots), so
            // we must NOT give up before any enemy is seen — only settle once enemies have appeared and no new ones
            // show for several passes. A large absolute cap still bails out on genuinely enemy-less floors.
            if (!_sweepDone)
            {
                int normalized = SweepLiveSlots(_curHpDef, _curAtk);
                if (normalized > 0) _sweepIdle = 0; else _sweepIdle++;
                if ((_swept.Count > 0 && _sweepIdle >= 8) || _sweepIdle >= 200) _sweepDone = true;
            }
        }


        // Normalize each newly-seen live enemy once (HP/defense slot fields + cached melee damage). Maps the
        // slot's species Id to a TableIndex via the global _byId map (disambiguating shared base/enhanced Ids by
        // the slot's spawn MaxHp). Returns the number of enemies newly normalized this pass.
        private static int SweepLiveSlots(int curHpDef, int curAtk)
        {
            int normalized = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long slotBase = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slotBase + EnemySlotOffsets.RenderStatus) < 0) continue; // empty slot
                int id = Memory.ReadUShort(slotBase + EnemySlotOffsets.EnemySpeciesId);
                if (_swept.TryGetValue(s, out int prevSpeciesId) && prevSpeciesId == id) continue;  // this enemy already normalized
                if (!_byId.TryGetValue(id, out var tableIndices)) continue;   // not a known species
                _swept[s] = id; normalized++;                                // normalize once (enemies spawn at load, no respawn)

                int slotMaxHp = Memory.ReadInt(slotBase + EnemySlotOffsets.MaxHp);
                int tableIndex = tableIndices.Count == 1 ? tableIndices[0]
                                        : tableIndices.OrderBy(candidate => Math.Abs((_byTableIndex[candidate].MaxHp ?? 0) - slotMaxHp)).First();
                EnemyDefaults enemyDefaults = _byTableIndex[tableIndex];
                bool hasHp  = _hpdefTier.TryGetValue(tableIndex, out int nativeHpTier);
                bool hasAtk = _atkTier.TryGetValue(tableIndex, out int nativeAtkTier);
                // "Stronger" targets one tier ABOVE current, so it must scale NATIVE enemies too (the gradient
                // randomizer mode only ever touches non-native ones, where native factor = 1).
                bool native = (!hasHp || nativeHpTier == curHpDef) && (!hasAtk || nativeAtkTier == curAtk);
                if (LogNormalize)
                    Console.WriteLine($"[Normalize] slot {s} {enemyDefaults.Name} (id {id}, ti {tableIndex}): nativeTier(HpDef={(hasHp ? nativeHpTier : -1)},Atk={(hasAtk ? nativeAtkTier : -1)}) curTier(HpDef={curHpDef},Atk={curAtk}){(native && !StrongerEnemies ? " — native, skip" : "")}");
                if (native && !StrongerEnemies) continue;

                // HP / defense (HP/def tier) — per-slot scaling via the shared EnemyStatScaler pipeline.
                if (hasHp && (StrongerEnemies || nativeHpTier != curHpDef))
                {
                    if (enemyDefaults.MaxHp.HasValue)
                        EnemyStatScaler.ScaleHp(s, Factor(_bHp, nativeHpTier, curHpDef));
                    if (enemyDefaults.DamageReduction.HasValue || enemyDefaults.WeaponDefense.HasValue)
                        EnemyStatScaler.ScaleDefense(s, Factor(_bDr, nativeHpTier, curHpDef), Factor(_bWd, nativeHpTier, curHpDef));
                }

                // Damage — melee (per-slot cache) + projectile (per-species STB / BST), via the shared pipeline.
                if ((NormalizeDamage || StrongerEnemies) && hasAtk && (StrongerEnemies || nativeAtkTier != curAtk))
                {
                    EnemyStatScaler.ScaleMelee(s, enemyDefaults.MeleeDamage, Factor(_bMelee, nativeAtkTier, curAtk));
                    EnemyStatScaler.ScaleProjectile(s, tableIndex, Factor(_bProj, nativeAtkTier, curAtk));
                }
            }
            return normalized;
        }
    }
}
