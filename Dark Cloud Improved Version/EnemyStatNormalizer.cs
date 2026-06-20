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
    /// along a 7-step gradient (one baseline per dungeon, ID 0..6), with a single tunable knob.
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
    /// HP/defense are written to the species record (reverted by EnemyModelInjector's snapshot/restore on
    /// dungeon exit) plus a sweep of already-spawned live slots. Damage lives only in the .stb script and is
    /// patched in RAM once per floor (see <see cref="NormalizeDamage"/>); STB-in-RAM addressing is still being
    /// pinned, so the damage path is OFF by default while HP/defense are validated first.
    /// </summary>
    internal static class EnemyStatNormalizer
    {
        // ── Config (the single tuning surface) ───────────────────────────────────────────────────────
        internal static bool  NormalizeEnemyStats   = true;  // master on/off  (TESTING: default on)
        internal static float NormalizationStrength = 1.0f;  // k ∈ [0,1]: 0 = strong enemies run wild (weak still buffed), 1 = flatten all to current
        internal static bool  NormalizeDamage       = true;  // also rescale melee attack damage (cached _SET_DMG_PARA field)
        internal static bool  LogNormalize          = true;  // verbose per-enemy logging (TESTING)

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
        private static readonly HashSet<int> _projPatchedStb = new(); // STB native bases whose shot literals are already scaled this floor (anti-cascade; STBs are shared/duplicated per species)
        private static bool _sweepDone;     // set once the floor's enemies are loaded + normalized; stops the per-tick sweep
        private static int  _sweepIdle;     // consecutive sweep passes with nothing new to patch

        // BST "default" shot-damage normalization. The BehaviorScriptTable (0x27EB90) is a STATIC ELF table — its
        // entries' +0x3C "base damage" feed the default (argc!=6) shots and persist across floors, so we snapshot
        // each entry's boot value the first time we touch it and restore on leaving the dungeon (to town). Keyed by
        // the RAM address of the entry's +0x3C field; never cleared (the originals are the boot values).
        private static readonly Dictionary<long, int> _bstOriginal = new();
        private static bool _bstDirty;      // any BST entry currently holds a scaled value (needs restore on exit)
        private const long SpeciesTableRam = 0x2027FB00;  // EnemySpeciesTable record 0 in RAM (native 0x27FB00)
        private const long BstPtrArrayRam  = 0x2027FA70;  // BST pointer array in RAM (native 0x27FA70)
        private const int  ShotIdxPrimary  = 0x68;        // species record: primary shot-effect index (default shots use this)
        private const int  BstDamageOff    = 0x3C;        // BST entry: shot base damage

        private static bool IsBoss(in EnemyDefaults e) =>
            !string.IsNullOrEmpty(e.ModelCode) && e.ModelCode[0] == 'c';

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
            foreach (FieldInfo f in typeof(Enemies).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType != typeof(EnemyDefaults)) continue;
                var e = (EnemyDefaults)f.GetValue(null);
                if (!e.TableIndex.HasValue) continue;
                _byTableIndex[e.TableIndex.Value] = e;
                (_byId.TryGetValue(e.Id, out var l) ? l : (_byId[e.Id] = new List<int>())).Add(e.TableIndex.Value);
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
                        int[] tis = pools[floor].TableIndices;
                        if (tis == null) continue;
                        int hpdef = HpDefTierOf(d, floor); // for DS this varies by floor band
                        foreach (int ti in tis)
                        {
                            if (!_atkTier.TryGetValue(ti, out int a) || d < a)         _atkTier[ti]   = d;
                            if (!_hpdefTier.TryGetValue(ti, out int h) || hpdef < h)    _hpdefTier[ti] = hpdef;
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
                var hp = new List<double>(); var dr = new List<double>(); var wd = new List<double>();
                foreach (var kv in _hpdefTier)
                {
                    if (kv.Value != t || !_byTableIndex.TryGetValue(kv.Key, out EnemyDefaults e) || IsBoss(e)) continue;
                    if (e.MaxHp.HasValue)           hp.Add(e.MaxHp.Value);
                    if (e.DamageReduction.HasValue) dr.Add(e.DamageReduction.Value);
                    if (e.WeaponDefense.HasValue)   wd.Add(e.WeaponDefense.Value);
                }
                _bHp[t] = (float)Median(hp); _bDr[t] = (float)Median(dr); _bWd[t] = (float)Median(wd);
            }
            for (int t = 0; t < AtkTierCount; t++)
            {
                var mel = new List<double>(); var pr = new List<double>();
                foreach (var kv in _atkTier)
                {
                    if (kv.Value != t || !_byTableIndex.TryGetValue(kv.Key, out EnemyDefaults e) || IsBoss(e)) continue;
                    int m = Representative(e.MeleeDamage);      if (m > 0) mel.Add(m);
                    int p = Representative(e.ProjectileDamage); if (p > 0) pr.Add(p);
                }
                _bMelee[t] = (float)Median(mel); _bProj[t] = (float)Median(pr);
            }
            // The tiers are a difficulty ramp, so each baseline should be a rising trend. Sparse stats produce
            // noisy dips/gaps from tiny sample sizes — enforce non-decreasing (fill empty tiers, then cumulative-max).
            foreach (float[] b in new[] { _bHp, _bDr, _bWd, _bMelee, _bProj }) MakeMonotonic(b);
        }

        // Back-fill leading empty tiers from the first sampled value, then make the series non-decreasing.
        private static void MakeMonotonic(float[] b)
        {
            int first = Array.FindIndex(b, v => v > 0);
            if (first < 0) return;               // stat never sampled — leave all zero (Factor falls back to 1)
            for (int d = 0; d < first; d++) b[d] = b[first];
            for (int d = 1; d < b.Length; d++) if (b[d] < b[d - 1]) b[d] = b[d - 1];
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
            float k = Math.Max(0f, Math.Min(1f, NormalizationStrength));
            return Math.Max(currentDungeon, nativeTier - (nativeTier - currentDungeon) * k);
        }

        private static float Lerp(float[] baseline, float e)
        {
            if (e <= 0) return baseline[0];
            if (e >= baseline.Length - 1) return baseline[baseline.Length - 1];
            int i = (int)Math.Floor(e);
            float f = e - i;
            float v0 = baseline[i], v1 = baseline[i + 1];
            if (v0 <= 0) return v1;           // no native sample in tier i — fall back to the neighbour
            if (v1 <= 0) return v0;
            return v0 * (1 - f) + v1 * f;
        }

        // Scale factor for a stat given its per-dungeon baseline series. 1.0 = leave unchanged.
        private static float Factor(float[] baseline, int nativeTier, int currentDungeon)
        {
            float bn = baseline[nativeTier];
            if (bn <= 0) return 1f;
            float be = Lerp(baseline, EffectiveTier(nativeTier, currentDungeon));
            if (be <= 0) return 1f;
            return be / bn;
        }

        private static int ScaleRound(int nativeValue, float factor)
        {
            if (nativeValue <= 0) return nativeValue;
            return Math.Max(1, (int)Math.Round(nativeValue * factor));
        }

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Per-floor entry point
        // ════════════════════════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Normalize every present non-native species' stats for the current floor. Idempotent and guarded so
        /// it runs once per floor; safe to call each tick. No-op when disabled or in town. Native species
        /// (factor 1) are left untouched, so this is harmless on vanilla floors.
        /// </summary>
        internal static void NormalizeStatsForFloor()
        {
            if (!NormalizeEnemyStats) return;
            // Leaving the floor (town / dungeon exit) must invalidate the cached key, so that RE-entering the same
            // dungeon+floor re-runs the sweep. The game reloads fresh native-value STBs/slots on every entry, so a
            // persisted key (same floor number) would otherwise skip normalization on the 2nd+ visit.
            if (!Player.InDungeonFloor()) { _normalizedKey = -1; RestoreBst(); return; }

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
                _swept.Clear(); _projPatchedStb.Clear(); _sweepDone = false; _sweepIdle = 0; // new floor: sweep until its enemies are loaded
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
                long b = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(b + EnemySlotOffsets.RenderStatus) < 0) continue; // empty slot
                int id = Memory.ReadUShort(b + EnemySlotOffsets.EnemySpeciesId);
                if (_swept.TryGetValue(s, out int sp) && sp == id) continue;  // this enemy already normalized
                if (!_byId.TryGetValue(id, out var tis)) continue;            // not a known species
                _swept[s] = id; normalized++;                                // normalize once (enemies spawn at load, no respawn)

                int slotMaxHp = Memory.ReadInt(b + EnemySlotOffsets.MaxHp);
                int ti = tis.Count == 1 ? tis[0]
                                        : tis.OrderBy(t => Math.Abs((_byTableIndex[t].MaxHp ?? 0) - slotMaxHp)).First();
                EnemyDefaults e = _byTableIndex[ti];
                bool hasHp  = _hpdefTier.TryGetValue(ti, out int nHp);
                bool hasAtk = _atkTier.TryGetValue(ti, out int nAtk);
                bool native = (!hasHp || nHp == curHpDef) && (!hasAtk || nAtk == curAtk);
                if (LogNormalize)
                    Console.WriteLine($"[Normalize] slot {s} {e.Name} (id {id}, ti {ti}): nativeHpTier={(hasHp ? nHp : -1)} nativeAtkTier={(hasAtk ? nAtk : -1)} cur(HpDef={curHpDef},Atk={curAtk}){(native ? " — native, skip" : "")}");
                if (native) continue;

                // HP / defense (HP/def tier).
                if (hasHp && nHp != curHpDef)
                {
                    if (e.MaxHp.HasValue)
                    {
                        int newMax = ScaleRound(e.MaxHp.Value, Factor(_bHp, nHp, curHpDef));
                        int curHp  = Memory.ReadInt(b + EnemySlotOffsets.Hp);
                        int curMax = Math.Max(1, slotMaxHp);
                        Memory.WriteInt(b + EnemySlotOffsets.MaxHp, newMax);
                        Memory.WriteInt(b + EnemySlotOffsets.Hp, Math.Max(1, (int)Math.Round(curHp * (newMax / (double)curMax))));
                    }
                    if (e.DamageReduction.HasValue || e.WeaponDefense.HasValue)
                    {
                        ushort dr = (ushort)ScaleRound(e.DamageReduction ?? 0, Factor(_bDr, nHp, curHpDef));
                        ushort wd = (ushort)ScaleRound(e.WeaponDefense ?? 0, Factor(_bWd, nHp, curHpDef));
                        Memory.WriteInt(b + EnemySlotOffsets.DefenseStats, (dr & 0xFFFF) | (wd << 16));
                    }
                }

                // Damage — melee via the cached _SET_DMG_PARA array, projectile via the STB shot-damage literal.
                if (NormalizeDamage && hasAtk && nAtk != curAtk)
                {
                    PatchMeleeCache(s, e, nAtk, curAtk);
                    PatchProjectileCache(s, ti, nAtk, curAtk);
                }
            }
            return normalized;
        }

        // Scale the live cached melee-damage entries for a slot. Value-matched + cascade-guarded, so it's
        // idempotent.
        private static void PatchMeleeCache(int slot, in EnemyDefaults e, int nAtk, int curAtk)
        {
            if (e.MeleeDamage == null || e.MeleeDamage.Length == 0) return;
            float f = Factor(_bMelee, nAtk, curAtk);
            if (f == 1f) return;

            var map = new Dictionary<int, int>();                 // native value -> scaled value
            foreach (int mv in e.MeleeDamage)
                if (mv > 0 && !map.ContainsKey(mv)) map[mv] = ScaleRound(mv, f);
            var scaledResults = new HashSet<int>(map.Values);     // don't re-patch a value we produced (anti-cascade)

            long arr = DmgParaCache.ArrayAddr(slot);
            byte[] d = Memory.ReadByteArray(arr, 0x80);            // 32 body-part entries — covers any enemy
            if (d == null) return;
            int hits = 0;
            for (int i = 0; i + 4 <= d.Length; i += 4)
            {
                int v = d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
                if (map.TryGetValue(v, out int nv) && nv != v && !scaledResults.Contains(v))
                {
                    Memory.WriteInt(arr + i, nv);
                    hits++;
                    if (LogNormalize) Console.WriteLine($"[Normalize]   slot {slot} melee +0x{i:X}: {v} -> {nv} (factor {f:F2})");
                }
            }
            if (LogNormalize && hits == 0)
                Console.WriteLine($"[Normalize]   slot {slot} melee: factor {f:F2} but none of [{string.Join(",", e.MeleeDamage)}] found in cache @0x{arr:X8}");
        }

        // Magic at RS_PROG_HEADER+0x00 ("STB\0") — validates a loaded STB before patching it.
        private const int StbMagic = 0x00425453;

        // Scale a slot's PROJECTILE damage by patching the STB op3 int-literal that feeds _SET_SHOT/_SET_SHOT2's
        // 5th (damage) arg. Unlike melee — whose value is latched once into the RAM cache at init — the shot-damage
        // RAM field (ShotDmgCache) is rewritten from this STB constant on EVERY shot and consumed the next frame, so
        // the script literal is the only stable target (RE'd 2026-06-19; see EnemyAddresses.ShotDmgCache).
        //
        // We walk the STB code section (RS_PROG_HEADER+0x08) for op21 ext-calls whose first pushed arg is the
        // op3 int-literal funcId 133/135 (_SET_SHOT/_SET_SHOT2 in the STB command registry), then scale the call's
        // LAST arg — the damage. Two forms (RE'd 2026-06-20):
        //   • EXPLICIT: the last arg is an op3 int-literal — the per-species value baked in the script. Scale it.
        //   • SUBROUTINE: the last arg is an op1 runtime variable with scope==1 (a local/argument). The _SET_SHOT
        //     sits in a shared "fire" subroutine; the real base damage is an op3 int-literal pushed at the call_func
        //     site as argument index `idx`. Resolve up the call chain (ScaleArg): a caller may forward its OWN
        //     local[idx'] another level (Ghost 21 / Lich 100 / Lich-Enh 110 do this once) before the literal appears.
        //     Scale every literal found. The engine then distance-scales this base at runtime (point-blank = full,
        //     far = less — per in-game observation), so scaling the base scales the whole curve.
        // Values are read from the STB itself (ground truth), independent of EnemyDefaults.ProjectileDamage. Fixed
        // 12-byte records → no relocation. Unresolved cases (op1 scope!=1 "default"/engine source — Sam/Crescent
        // Baron — or op1 whose call-site arg is itself computed — Ghost/Lich) are skipped. Idempotent across the
        // shared/duplicated per-species STB via <see cref="_projPatchedStb"/> and a per-pass patched-offset set.
        private static void PatchProjectileCache(int slot, int ti, int nAtk, int curAtk)
        {
            float f = Factor(_bProj, nAtk, curAtk);
            if (f == 1f) return;

            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return;
            if (!_projPatchedStb.Add(stbNative)) return;     // this STB's shot literals already scaled this floor
            long stb = ((long)stbNative & 0x1FFFFFFF) | 0x20000000;
            if (Memory.ReadInt(stb) != StbMagic) return;     // not a valid loaded STB

            const int Window = 0xC000;                        // covers the largest enemy STB (~44 KB)
            byte[] d = Memory.ReadByteArray(stb, Window);
            if (d == null || d.Length < 0x10) return;
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(8);                               // code-section start (header+0x08)
            if (code < 0xC || code >= d.Length) return;

            // call_func (op19/27) target offset -> its call-site offsets, for resolving subroutine-arg shooters.
            var callsByTarget = new Dictionary<int, List<int>>();
            for (int i = code; i + 12 <= d.Length; i += 4)
                if (Word(i) == 19 || Word(i) == 27)
                    (callsByTarget.TryGetValue(Word(i + 8), out var l) ? l : (callsByTarget[Word(i + 8)] = new List<int>())).Add(i);

            var patched = new HashSet<int>();   // operandB offsets already scaled this pass (anti double-scale)
            int hits = 0;

            // Scale the op3 int-literal whose operandB sits at litOff (idempotent within this pass).
            void ScaleLiteralAt(int litOff)
            {
                if (litOff < code || litOff + 4 > d.Length || !patched.Add(litOff)) return;
                int v = Word(litOff);
                if (v <= 0) return;
                int nv = ScaleRound(v, f);
                if (nv == v) return;
                Memory.WriteInt(stb + litOff, nv);
                hits++;
                if (LogNormalize) Console.WriteLine($"[Normalize]   slot {slot} proj STB 0x{stbNative:X8}+0x{litOff:X}: {v} -> {nv} (factor {f:F2})");
            }

            // Subroutine entry containing code offset `off` = the largest call_func target <= off.
            int FuncStart(int off)
            {
                int s = code;
                foreach (int t in callsByTarget.Keys) if (t <= off && t > s) s = t;
                return s;
            }

            // Record offsets of the consecutive op1/2/3 pushes immediately before call site `cs`, in push order.
            List<int> ArgsBefore(int cs)
            {
                var a = new List<int>();
                for (int j = cs - 0xC; j >= code && (Word(j) == 1 || Word(j) == 2 || Word(j) == 3); j -= 0xC) a.Add(j);
                a.Reverse();
                return a;
            }

            // Resolve which op3 int-literals ultimately feed argument `idx` of subroutine `fsub`, following op1
            // scope-1 forwarding up the call chain (e.g. Ghost/Lich forward local[1] one extra level), and scale
            // each. Depth- and cycle-guarded.
            void ScaleArg(int fsub, int idx, HashSet<long> seen, int depth)
            {
                if (depth > 8 || !callsByTarget.TryGetValue(fsub, out var sites)) return;
                if (!seen.Add(((long)fsub << 20) ^ (uint)idx)) return;
                foreach (int cs in sites)
                {
                    var args = ArgsBefore(cs);
                    if (idx < 0 || idx >= args.Count) continue;
                    int ar = args[idx];
                    if (Word(ar) == 3 && Word(ar + 4) == 1) ScaleLiteralAt(ar + 8);            // op3 int-literal base damage
                    else if (Word(ar) == 1 && Word(ar + 8) == 1) ScaleArg(FuncStart(cs), Word(ar + 4), seen, depth + 1); // forwarded local
                }
            }

            for (int x = code; x + 12 <= d.Length; x += 4)
            {
                if (Word(x) != 21 || Word(x + 8) != 0) continue;        // op21 ext-call (operandB unused = 0)
                int argc = Word(x + 4);
                if (argc < 2 || argc > 10) continue;
                int fpos = x - argc * 0xC;                              // first pushed arg = the function id
                if (fpos < code) continue;
                if (Word(fpos) != 3 || Word(fpos + 4) != 1) continue;   // op3 int-literal funcId
                int fid = Word(fpos + 8);
                // _SET_SHOT (133) / _SET_SHOT2: the monster scripts actually push funcId 229 for the second shot
                // (registry 135 exists but is never used); 229 carries explicit damage exactly like 133. Missing it
                // left 6 enemies' second shots unscaled (Heart 107, Mr. Blare 80, Billy 93, Sam 58, Bishop Q 130, …).
                if (fid != 133 && fid != 135 && fid != 229) continue;
                if (argc != 6)                                          // argc!=6 → no 5th arg: an engine "default" shot
                {
                    PatchBstDefault(slot, ti, f);                       // scale its BST +0x3C base damage instead
                    continue;
                }
                int dr = x - 0xC;                                       // last pushed arg = the damage

                if (Word(dr) == 3 && Word(dr + 4) == 1)                 // EXPLICIT: op3 int-literal damage
                {
                    ScaleLiteralAt(dr + 8);
                }
                else if (Word(dr) == 1 && Word(dr + 8) == 1)            // SUBROUTINE: op1 scope==1 (call argument)
                {
                    // The shot reads local[idx]; resolve it up the call chain to the op3 literal(s) and scale them.
                    ScaleArg(FuncStart(x), Word(dr + 4), new HashSet<long>(), 0);
                }
            }
            if (LogNormalize && hits == 0)
                Console.WriteLine($"[Normalize]   slot {slot} proj: factor {f:F2}, no scalable _SET_SHOT literal in STB 0x{stbNative:X8} (computed/default shooter or no shot)");
        }

        // Scale a "default" shooter's BST base damage (BehaviorScriptTable entry +0x3C, the source the engine uses
        // when the STB _SET_SHOT passes no explicit damage). The entry is selected by the species record's primary
        // shot-effect index (+0x68) via the pointer array; we snapshot the boot value once (restored on dungeon
        // exit, since the BST is static and persists) and write round(original × factor). Only invoked for enemies
        // that actually fire a default shot, so we never apply one species' factor to an entry an override shooter
        // merely shares (override shooters ignore +0x3C). 0-damage entries (e.g. Heart's bind) scale to nothing.
        private static void PatchBstDefault(int slot, int ti, float f)
        {
            if (ti < 0) return;
            int idx = Memory.ReadUShort(SpeciesTableRam + (long)ti * EnemySpeciesTable.Stride + ShotIdxPrimary);
            if (idx == 0xFFFF) return;                                  // no shot effect
            int btNative = Memory.ReadInt(BstPtrArrayRam + idx * 4);
            if (btNative == 0) return;
            long dmgAddr = (((long)btNative & 0x1FFFFFFF) | 0x20000000) + BstDamageOff;

            if (!_bstOriginal.TryGetValue(dmgAddr, out int orig))      // snapshot boot value once
            {
                orig = Memory.ReadInt(dmgAddr);
                _bstOriginal[dmgAddr] = orig;
            }
            if (orig <= 0) return;                                      // non-damaging shot (bind) — nothing to scale
            int nv = ScaleRound(orig, f);
            int cur = Memory.ReadInt(dmgAddr);
            if (cur == nv) return;                                      // already scaled (re-run / sibling)
            Memory.WriteInt(dmgAddr, nv);
            _bstDirty = true;
            if (LogNormalize) Console.WriteLine($"[Normalize]   slot {slot} proj BST idx{idx} @0x{dmgAddr:X8}: {orig} -> {nv} (factor {f:F2})");
        }

        // Restore every BST entry we scaled back to its boot value. Called on leaving the dungeon floor (to town);
        // the BST is a static ELF table that persists across floors, so without this a scaled default-shot damage
        // would leak into the next dungeon. Originals are kept (boot values) so re-entry re-scales from them.
        private static void RestoreBst()
        {
            if (!_bstDirty) return;
            foreach (var kv in _bstOriginal) Memory.WriteInt(kv.Key, kv.Value);
            _bstDirty = false;
            if (LogNormalize) Console.WriteLine($"[Normalize] restored {_bstOriginal.Count} BST default-shot entr{(_bstOriginal.Count == 1 ? "y" : "ies")} on dungeon exit.");
        }

        // NOTE on damage normalization:
        //   • MELEE is normalized live via the cached _SET_DMG_PARA array (PatchMeleeCache) — patching the STB value
        //     is too late since the engine latches it into that cache at the enemy's init.
        //   • PROJECTILE has two sources, both handled by PatchProjectileCache:
        //       – STB shots (argc==6): scaled at the script literal — explicit op3 literals AND subroutine shooters
        //         (op1 scope-1 arg resolved up the call chain, e.g. Golem 64, Ghost 21).
        //       – "default" shots (argc!=6, no STB damage): scaled at the BST entry +0x3C base damage via
        //         PatchBstDefault, with snapshot/restore (RestoreBst on dungeon exit) since the BST is static.
        //     This covers every projectile shooter; only boss (cN) shots are intentionally left (normalizer skips bosses).
    }
}
