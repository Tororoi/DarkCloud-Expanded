using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The "Randomize Enemies" feature: on dungeon entry it stages each non-boss/event floor's BtEnemyLayout
    /// (normal + Ura) with a randomized roster — a budget-aware mix or a themed group (EnemySpecies.ThemeGroups),
    /// weighted by dungeon proximity. Uses <see cref="SpawnRoster"/> for the snapshot/restore mechanics and
    /// subscribes to SpawnRoster.Restoring to revert its staged floors on dungeon exit.
    /// </summary>
    internal static class EnemyRandomizer
    {
        static EnemyRandomizer() { SpawnRoster.Restoring += OnRosterRestoring; }

        // ── Enemy randomizer ("Randomize Enemies" option) ───────────────────────────────────────────────────
        // On dungeon entry (floor-select), fill EVERY non-boss/event floor's roster (normal + Ura) with a random
        // 9-species mix from the eligible pool, weighted to favor the dungeon's NATIVE mimic + king mimic. Re-rolled
        // each dungeon entry; reverted on dungeon exit via RestoreSpawnRoster. Reuses the snapshot + mimic-chest
        // machinery (so roster mimics still render as chests) and is marked dungeon-wide so NotifyInFloor doesn't
        // revert it per-floor. The (mostly non-native) enemies are rescaled to the dungeon's level by EnemyStatNormalizer.
        internal static bool RandomizeEnemies = false;
        private const double MimicChance = 0.40, KingMimicChance = 0.28;
        // Themed-group mode (coexists with the random mix). Each floor has a ThemeChance of being a single themed
        // group (cards, days of the week, dragons, …; see EnemySpecies.ThemeGroups) instead of the random mix.
        // When a theme is chosen: 50% the whole roster is that group (spawn-cap repeatable, so the floor fills with
        // them); otherwise the group is capped to one-of-each and the rest of the roster is filled 50/50 by the
        // dungeon's mimic + king mimic or by dungeon-native enemies.
        internal static bool UseThemedGroups = true;
        private const double ThemeChance = 0.50;
        private const double ThemeCapOneChance = 0.50;     // within a theme: chance the group is capped to one-of-each
        private const double ThemeMimicFillChance = 0.50;  // when capped: chance the rest is mimics (else dungeon natives)
        // Distinct species per floor.
        private const int RosterFillCount = BtEnemyLayout.EntriesPerFloor;
        private static readonly System.Random _randomizerRng = new System.Random();
        private static int[] _eligiblePool;                       // TableIndices of eligible enemies (base + enhanced; no bosses/mimics)
        private static (int mimic, int king)[] _mimicByDungeon;   // per dungeon (0..6): native mimic + king-mimic TableIndex
        // Per-floor staging (lazy): which dungeon is staged, and for each staged floor index its captured VANILLA
        // Id ints (normal[9], ura[9]) so RestoreSpawnRoster reverts only the floors actually visited. A floor is
        // staged at most once per dungeon visit (so backfloor toggles / backtracking never re-roll it); the dict
        // is cleared on dungeon exit so the next entry re-randomizes whatever floors are picked.
        private static int _stageDungeon = -1;
        private static readonly System.Collections.Generic.Dictionary<int, (int[] normal, int[] ura)> _stagedFloors = new();
        private static int _menuFloor = -1;   // the single floor currently staged from the floor-select menu (the highlighted one)

        private static System.Collections.Generic.Dictionary<int, (string code, string name)> _speciesByTI;
        private static System.Collections.Generic.Dictionary<int, int> _footprintByTI;   // TableIndex → model-buffer footprint (bytes)

        private static void EnsureRandomizerData()
        {
            if (_eligiblePool != null) return;
            // Per-TableIndex maps for logging + budget-aware fill, over every species with a record.
            _speciesByTI = new System.Collections.Generic.Dictionary<int, (string, string)>();
            _footprintByTI = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var kv in EnemySpecies.All)
            {
                EnemyDefaults e = kv.Value;
                _speciesByTI[kv.Key] = (e.ModelCode ?? "?", e.Name ?? "?");                  // for buffer-sample logging
                if (e.ModelFootprint != null) _footprintByTI[kv.Key] = e.ModelFootprint.Value;   // for budget-aware fill
            }

            // Non-themed mix pool: exactly the generic-placement set (bosses, companions, Killer Snake and mimics are
            // already excluded by EnemySpecies.RandomizerValid; mimics are inserted separately, weighted and always the
            // current dungeon's pair). RandomizerBosses (e.g. Minotaur Joe) will fold in via a toggle.
            _eligiblePool = new int[EnemySpecies.RandomizerValid.Count];
            EnemySpecies.RandomizerValid.Keys.CopyTo(_eligiblePool, 0);

            _mimicByDungeon = new (int, int)[BtEnemyLayout.DungeonCount];
            _mimicByDungeon[0] = (EnemySpecies.MimicDBC.TableIndex.Value, EnemySpecies.KingMimicDBC.TableIndex.Value);
            _mimicByDungeon[1] = (EnemySpecies.MimicWOF.TableIndex.Value, EnemySpecies.KingMimicWOF.TableIndex.Value);
            _mimicByDungeon[2] = (EnemySpecies.MimicSW.TableIndex.Value,  EnemySpecies.KingMimicSW.TableIndex.Value);
            _mimicByDungeon[3] = (EnemySpecies.MimicSMT.TableIndex.Value, EnemySpecies.KingMimicSMT.TableIndex.Value);
            _mimicByDungeon[4] = (EnemySpecies.MimicMS.TableIndex.Value,  EnemySpecies.KingMimicMS.TableIndex.Value);
            _mimicByDungeon[5] = (EnemySpecies.MimicGoT.TableIndex.Value, EnemySpecies.KingMimicGoT.TableIndex.Value);
            _mimicByDungeon[6] = (EnemySpecies.MimicDS.TableIndex.Value,  EnemySpecies.KingMimicDS.TableIndex.Value);

            Console.WriteLine($"[Randomizer] eligible pool: {_eligiblePool.Length} species.");
        }

        // ── DEBUG: per-floor model-buffer usage log (roster vs buffer capacity) ──────────────────────────────────
        // Once a floor's load settles (model-buffer `used` stops climbing), log one line: the loaded roster
        // (TableIndexes → ModelCode/Name) + the model buffer used/cap. Shows how much of the per-area buffer the
        // randomized roster consumes — used to spot near-overflow floors and to capture each area's cap (e.g. the
        // outstanding Demon Shaft cap). Captures hangs too (used stalls at the stuck value). The buffer struct
        // (from SetDataBuffer): used@+0x8, cap@+0xC.
        internal static bool DebugBufferSamples = true;
        private const long ModelBufUsed = 0x21F066D8, ModelBufCap = 0x21F066DC;
        private static int _sampKey = -1, _sampLastUsed = -1, _sampStable = 0;
        private static int _sampMin = int.MaxValue, _sampPrevUsed = -1;
        private static bool _sampDone;
        internal static void SampleBufferUsage()
        {
            if (!DebugBufferSamples) return;
            if (!Player.InDungeonFloor()) { _sampKey = -1; return; }
            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            int floor   = Memory.ReadByte(Addresses.checkFloor);
            int back    = Memory.ReadByte(Addresses.dunBackFloorFlag) != 0 ? 1 : 0;
            if (dungeon < 0 || dungeon >= BtEnemyLayout.DungeonCount) return;
            int key = (dungeon << 16) | (floor << 8) | back;
            if (key != _sampKey) { _sampKey = key; _sampLastUsed = -1; _sampStable = 0; _sampMin = int.MaxValue; _sampDone = false; }
            if (_sampDone) return;
            // Boss/event floors aren't staged (vanilla roster) — sampling them logs garbage. Skip: mark done, no log.
            var excl = Dungeon.GetDungeonEventFloors((byte)dungeon);
            if (excl != null && excl.Contains((byte)floor)) { _sampDone = true; return; }

            int used = Memory.ReadInt(ModelBufUsed), cap = Memory.ReadInt(ModelBufCap);
            if (used <= 0 || cap <= 0 || cap > 8_000_000) return;          // wait for a sane, loaded buffer
            if (used < _sampMin) _sampMin = used;                          // track this floor's reset/low watermark
            if (used == _sampLastUsed) _sampStable++; else { _sampStable = 0; _sampLastUsed = used; }
            if (_sampStable < 40) return;                                  // settled (~1s+ of no change)
            // Confirm the buffer actually (re)loaded for THIS floor rather than still holding the previous floor's
            // value (stale read). Accept if: the value differs from the last sample (a different model loaded), OR
            // the buffer was seen to reset (min < half the settled value — catches genuinely same-size reloads), OR
            // it's been stable a long time (a pending reload would have happened by now). Ptr-independent (the
            // allocator reuses addresses for small single-species loads, which broke the earlier ptr-based gate).
            if (used == _sampPrevUsed && _sampMin >= used / 2 && _sampStable < 200) return;
            _sampDone = true;
            _sampPrevUsed = used;

            EnsureRandomizerData();
            int layoutBase = back != 0 ? BtEnemyLayout.UraLayoutBase[dungeon] : BtEnemyLayout.LayoutBase[dungeon];
            var parts = new System.Collections.Generic.List<string>();
            for (int e = 0; e < BtEnemyLayout.EntriesPerFloor; e++)
            {
                int ti = Memory.ReadInt(BtEnemyLayout.EntryAddress(layoutBase, floor, e) + BtEnemyLayout.Id);
                if (ti < 0) { parts.Add("-1"); continue; }
                parts.Add(_speciesByTI != null && _speciesByTI.TryGetValue(ti, out var sp) ? $"{ti}:{sp.code}:{sp.name}" : $"{ti}:?");
            }
            Console.WriteLine($"[BufferSample] dun={dungeon} floor={floor} back={back} used={used} cap={cap} "
                + $"species=[{string.Join(" | ", parts)}]");
        }

        /// <summary>
        /// Floor-select menu (dunMode==4): keep exactly ONE floor staged — the highlighted one. The player selects a
        /// single floor, so as the cursor moves we un-stage the previous highlight and stage the new one; whichever
        /// floor is confirmed is the one left staged (no per-hover accumulation). Pre-load (the menu precedes load).
        /// </summary>
        internal static void StageSelectedFloor(byte dungeon, int floor)
        {
            if (!RandomizeEnemies) return;
            if (_menuFloor == floor) return;                    // highlight unchanged — already handled
            if (_menuFloor >= 0) RevertStagedFloor(_menuFloor); // un-stage the floor we just moved off
            _menuFloor = floor;
            StageFloorRoster(dungeon, floor);
        }

        /// <summary>Revert a single staged floor (normal + Ura) to its captured vanilla Ids and drop it from the set.</summary>
        private static void RevertStagedFloor(int floor)
        {
            if (_stageDungeon < 0 || !_stagedFloors.TryGetValue(floor, out var orig)) return;
            int[] bases = { BtEnemyLayout.LayoutBase[_stageDungeon], BtEnemyLayout.UraLayoutBase[_stageDungeon] };
            int[][] o = { orig.normal, orig.ura };
            for (int b = 0; b < 2; b++)
                for (int entry = 0; entry < BtEnemyLayout.EntriesPerFloor; entry++)
                    Memory.WriteInt(BtEnemyLayout.EntryAddress(bases[b], floor, entry) + BtEnemyLayout.Id, o[b][entry]);
            _stagedFloors.Remove(floor);
        }

        /// <summary>
        /// SpawnRoster.Restoring handler (dungeon exit / defensive restore): revert every floor we staged this visit
        /// to its captured vanilla Ids, then reset so the next entry re-randomizes from scratch. SpawnRoster reverts
        /// the SpawnCap edits separately (it owns the species-record snapshots).
        /// </summary>
        private static void OnRosterRestoring()
        {
            if (_stagedFloors.Count > 0 && _stageDungeon >= 0)
            {
                int[] bases = { BtEnemyLayout.LayoutBase[_stageDungeon], BtEnemyLayout.UraLayoutBase[_stageDungeon] };
                foreach (var kv in _stagedFloors)
                {
                    int[][] orig = { kv.Value.normal, kv.Value.ura };
                    for (int b = 0; b < 2; b++)
                        for (int entry = 0; entry < BtEnemyLayout.EntriesPerFloor; entry++)
                            Memory.WriteInt(BtEnemyLayout.EntryAddress(bases[b], kv.Key, entry) + BtEnemyLayout.Id, orig[b][entry]);
                }
                Console.WriteLine($"[Randomizer] restore: {_stagedFloors.Count} staged floor(s) reverted (dungeon {_stageDungeon}: {string.Join(",", _stagedFloors.Keys)}).");
            }
            _stagedFloors.Clear(); _stageDungeon = -1; _menuFloor = -1;   // next entry re-randomizes whatever floors are picked
        }

        /// <summary>
        /// Randomize ONE floor's roster (normal + Ura) just before it loads. Call with the floor index the player
        /// is about to enter — via StageSelectedFloor on the floor-select cursor (dunMode==4) or checkFloor+1 on the
        /// next-floor screen (dunMode==7), both of which fire pre-load. 0-indexed (== checkFloor == BtEnemyLayout
        /// index). Captures the floor's vanilla Ids first (lazy per-floor snapshot) and stages each floor at most once
        /// per visit, so backfloor toggles and backtracking never re-roll it; reverts on dungeon exit.
        /// </summary>
        internal static void StageFloorRoster(byte dungeon, int floor)
        {
            if (!RandomizeEnemies || dungeon >= BtEnemyLayout.DungeonCount) return;
            if (floor < 0 || floor >= BtEnemyLayout.FloorCount[dungeon]) return;
            EnsureRandomizerData();
            if (_eligiblePool.Length < BtEnemyLayout.EntriesPerFloor) return;  // safety: need enough to fill a floor

            // Defensive: a new dungeon without a restore in between (shouldn't happen — RestoreSpawnRoster runs on
            // exit) — revert the old one so its staged floors don't leak. (Fires Restoring → OnRosterRestoring, which
            // reverts our staged floors and resets _stageDungeon.)
            if (_stageDungeon != dungeon && _stageDungeon >= 0) SpawnRoster.RestoreSpawnRoster();
            if (_stagedFloors.ContainsKey(floor)) return;   // already randomized this floor this visit

            var exclude = Dungeon.GetDungeonEventFloors(dungeon);   // boss + event floors — left vanilla
            if (exclude != null && exclude.Contains((byte)floor)) return;

            _stageDungeon = dungeon;
            // Claim the dungeon: set the mimic-chest gate + mark dungeon-wide so NotifyInFloor won't revert per-floor.
            SpawnRoster.MarkActive(dungeon, dungeonWide: true);

            var (mimicTI, kingTI) = _mimicByDungeon[dungeon];
            int budget = FloorBufferBudget(dungeon);   // bytes the roster may consume (model buffer cap × safety)
            int[] bases = { BtEnemyLayout.LayoutBase[dungeon], BtEnemyLayout.UraLayoutBase[dungeon] };

            // Decide ONCE per floor whether to use a themed group. A themed floor shares one roster (and one set of
            // SpawnCap edits) across normal + Ura, so the global per-species caps stay consistent whichever side
            // loads. A non-themed floor falls back to the existing budget-aware random mix, rolled per layout.
            var capEdits = new System.Collections.Generic.List<(int ti, int cap)>();
            string themeName = null;
            int[] themedRoster = null;
            if (UseThemedGroups && _randomizerRng.NextDouble() < ThemeChance)
                themedRoster = BuildThemedRoster(dungeon, mimicTI, kingTI, budget, capEdits, out themeName);

            var snap = new int[2][];   // [0]=normal, [1]=Ura — captured vanilla Ids for restore
            bool kingInRoster = false;
            for (int b = 0; b < 2; b++)
            {
                snap[b] = new int[BtEnemyLayout.EntriesPerFloor];
                int[] roster = themedRoster ?? BuildFloorRoster(mimicTI, kingTI, budget);
                if (Array.IndexOf(roster, kingTI) >= 0) kingInRoster = true;
                for (int entry = 0; entry < BtEnemyLayout.EntriesPerFloor; entry++)
                {
                    long addr = BtEnemyLayout.EntryAddress(bases[b], floor, entry) + BtEnemyLayout.Id;
                    snap[b][entry] = Memory.ReadInt(addr);   // capture vanilla Id
                    Memory.WriteInt(addr, roster[entry]);    // write random Id
                }
            }
            // King mimics are limited to one-of-each on every floor EXCEPT a pure-mimics ("Mimics") theme floor —
            // whose whole roster is just the mimic + king, so the king must stay repeatable to fill it. Appended last
            // so it overrides any repeatable cap a mimic-fill set for the king; the regular mimic still carries the
            // population (it's added before the king and stays repeatable).
            if (kingInRoster && themeName != "Mimics")
                capEdits.Add((kingTI, 1));
            // Apply this floor's SpawnCap edits (snapshotted for restore on dungeon exit). Written at stage time —
            // i.e. just before this floor loads — so the per-species cap is correct when BtLoadMonstor reads it.
            foreach (var (ti, cap) in capEdits)
            {
                SpawnRoster.SnapshotSpeciesRecordIfNeeded(ti);
                Memory.WriteInt(EnemySpeciesTable.RecordAddress(ti) + EnemySpeciesTable.SpawnCap, cap);
            }
            _stagedFloors[floor] = (snap[0], snap[1]);
            if (themedRoster != null)
                Console.WriteLine($"[Randomizer] staged dungeon {dungeon} floor {floor} (normal+Ura); "
                    + $"theme: \"{themeName}\"; {capEdits.Count} cap edit(s); budget {budget}B.");
            else
                Console.WriteLine($"[Randomizer] staged dungeon {dungeon} floor {floor} (normal+Ura); "
                    + $"theme: none (random mix); native mimic {mimicTI}@{MimicChance:P0}, king {kingTI}@{KingMimicChance:P0}; budget {budget}B.");
        }

        // ── Budget-aware roster sizing (prevents the floor-load buffer overflow / hang) ──────────────────────────
        // Per-species footprints live in EnemyData.cs (EnemyDefaults.ModelFootprint) and per-dungeon caps in
        // DungeonData.cs (DungeonData.ModelBufferCapMin); the randomizer just reads them. The roster builder sums the
        // footprints as it fills a floor and stops before the buffer cap overflows (BtLoadMonstor hangs at ~99.9%).
        private static int Footprint(int ti) =>
            (_footprintByTI != null && _footprintByTI.TryGetValue(ti, out int f)) ? f : 60000;   // unknown → conservative

        // Fraction of the cap the roster may fill — headroom for measurement slop + intra-dungeon area variation, so the
        // load never approaches 100% (the hang was at 99.9%).
        private const double BufferSafetyFactor = 0.90;

        // Bytes this floor's roster may consume: min(known-dungeon cap, sane live cap) × safety. Robust to the stale cap
        // reported at dungeon entry (clamped by the per-dungeon constant) and to unmeasured dungeons (falls back to live).
        private static int FloorBufferBudget(int dungeon)
        {
            int known = Dungeons.TryGetValue((byte)dungeon, out var d) ? d.ModelBufferCapMin : 0;
            int live  = Memory.ReadInt(ModelBufCap);
            bool liveSane = live > 100_000 && live < 4_000_000;
            int basis;
            if (known > 0 && liveSane) basis = System.Math.Min(known, live);
            else if (known > 0)        basis = known;
            else if (liveSane)         basis = live;       // e.g. Demon Shaft (no constant yet) — trust the live cap
            else                       basis = 270_000;    // neither available — conservative floor
            return (int)(basis * BufferSafetyFactor);
        }

        // One floor's roster: weighted native mimic/king (each at most once), the rest distinct random eligible species.
        // Adds species until the next would push the summed model-buffer footprint past `budget`, then STOPS (leaves the
        // rest empty). It does NOT keep re-rolling for a species that happens to fit — that would bias small species to
        // appear more often. The first species is always allowed (any single model fits a floor). Duplicate re-rolls are
        // size-independent (just enforcing distinct entries), so they don't skew the distribution.
        private static int[] BuildFloorRoster(int mimicTI, int kingTI, int budget)
        {
            var roster = new System.Collections.Generic.List<int>(BtEnemyLayout.EntriesPerFloor);
            int used = 0;
            bool TryAdd(int ti)
            {
                if (roster.Count > 0 && used + Footprint(ti) > budget) return false;
                roster.Add(ti); used += Footprint(ti); return true;
            }
            if (_randomizerRng.NextDouble() < MimicChance)     TryAdd(mimicTI);
            if (_randomizerRng.NextDouble() < KingMimicChance) TryAdd(kingTI);
            while (roster.Count < RosterFillCount)
            {
                int pick = _eligiblePool[_randomizerRng.Next(_eligiblePool.Length)];
                if (roster.Contains(pick)) continue;   // distinct entries → even spawn distribution (size-independent)
                if (!TryAdd(pick)) break;              // budget reached — stop rather than seek a smaller-fitting species
            }
            while (roster.Count < BtEnemyLayout.EntriesPerFloor) roster.Add(-1);   // remaining entries empty (Id -1)
            return roster.ToArray();
        }

        // ── Themed roster ────────────────────────────────────────────────────────────────────────────────────
        // Build a floor roster from a single themed group (EnemySpecies.ThemeGroups, plus the per-dungeon "Mimics"
        // theme). Returns the roster and, via capEdits, the (TableIndex, SpawnCap) writes the caller must apply:
        //   • whole-group floor (ThemeCapOneChance miss): every group member at SpawnCap 0 (repeatable) so the floor
        //     fills with them — the roster is just the group, rest empty.
        //   • capped floor (ThemeCapOneChance hit): every group member at SpawnCap 1 (one-of-each), then the rest of
        //     the roster is filled by repeatable fillers — the dungeon mimic + king mimic, or dungeon-native regulars
        //     forced repeatable — added FIRST so the floor always has a repeatable species (else the load hangs).
        // Theme conditions (data in EnemySpecies):
        //   • Mimics theme: IS the dungeon mimic + king mimic; always whole-group (never capped/filled) so they aren't
        //     placed twice. Mimics are otherwise absent from every theme and from the native pools, so they only ever
        //     enter via this theme or the capped mimic-fill branch — a single dungeon-native concern.
        //   • requireFullFit themes (cards/days/gemrons): excluded by PickTheme on any floor that can't fit the WHOLE
        //     group, and placed in full first (never trimmed). They go capped only if a repeatable filler still fits;
        //     otherwise they stay whole-group so nothing is dropped.
        //   • ThemeSingleSpawnByTheme members (e.g. Captain/Sil/Gol in Pirates): pinned to SpawnCap 1 within that
        //     theme even on a whole-group floor.
        // Budget-aware like BuildFloorRoster: shuffles candidates and stops adding once the next model would overflow
        // the model buffer, so an over-budget (non-requireFullFit) theme contributes a random subset; no species twice.
        private static int[] BuildThemedRoster(int dungeon, int mimicTI, int kingTI, int budget,
            System.Collections.Generic.List<(int ti, int cap)> capEdits, out string themeName)
        {
            PickTheme(dungeon, budget, out themeName, out int[] members, out bool requireFullFit, out bool isMimicTheme);
            string theme = themeName;   // local copy: out params can't be captured by the local functions below
            var roster = new System.Collections.Generic.List<int>(BtEnemyLayout.EntriesPerFloor);
            int used = 0;
            bool TryAdd(int ti)
            {
                if (roster.Contains(ti)) return true;                             // already present — treat as a no-op success
                if (roster.Count > 0 && used + Footprint(ti) > budget) return false;
                roster.Add(ti); used += Footprint(ti); return true;
            }
            // Cap for a themed member on a whole-group (repeatable) floor: members listed in this theme's
            // ThemeSingleSpawnByTheme set are pinned to one spawn; everything else is repeatable so it can carry
            // the population.
            int WholeGroupCap(int ti) =>
                EnemySpecies.ThemeSingleSpawnByTheme.TryGetValue(theme, out var ss) && ss.Contains(ti) ? 1 : 0;

            Shuffle(members);

            if (isMimicTheme || requireFullFit)
            {
                // The Mimics theme IS the dungeon's mimic + king mimic; a requireFullFit theme (cards/days/gemrons) was
                // only selected because its WHOLE set fits the buffer. Either way every member is placed in full first
                // and never trimmed. A non-mimic group then either guarantees the dungeon mimic pair (ThemeGuaranteedMimics)
                // or may go capped (each member once-per-floor + mimic/native fillers) if a repeatable filler still fits;
                // otherwise it stays whole-group (all repeatable, no fillers). Mimics never cap/fill (no double-place).
                foreach (int ti in members)
                {
                    if (roster.Count >= RosterFillCount) break;
                    TryAdd(ti);                          // fits by construction (full set ≤ budget)
                }

                bool capped = false;
                if (!isMimicTheme && EnemySpecies.ThemeGuaranteedMimics.Contains(themeName))
                {
                    // Always place both the dungeon mimic + king mimic (repeatable), best-effort under budget. On the
                    // tightest floors a full requireFullFit group can leave no room for the pair.
                    if (TryAdd(mimicTI)) capEdits.Add((mimicTI, 0));
                    if (TryAdd(kingTI))  capEdits.Add((kingTI, 0));
                }
                else if (!isMimicTheme && _randomizerRng.NextDouble() < ThemeCapOneChance)
                {
                    int[] fillers = ChooseFillers(dungeon, mimicTI, kingTI);
                    foreach (int ti in fillers)
                    {
                        if (roster.Count >= RosterFillCount) break;
                        if (roster.Contains(ti)) continue;
                        if (TryAdd(ti)) { capEdits.Add((ti, 0)); capped = true; }   // repeatable filler carries population
                    }
                }
                foreach (int ti in members)              // finalize themed caps once the mode is known
                    if (roster.Contains(ti)) capEdits.Add((ti, capped ? 1 : WholeGroupCap(ti)));
            }
            else if (_randomizerRng.NextDouble() >= ThemeCapOneChance)
            {
                // Whole-group floor: the themed species ARE the roster, repeatable (SpawnCap 0; single-spawn members
                // excepted) so the floor populates from them. Budget may trim the group.
                foreach (int ti in members)
                {
                    if (roster.Count >= RosterFillCount) break;
                    if (!TryAdd(ti)) break;              // budget reached
                    capEdits.Add((ti, WholeGroupCap(ti)));
                }
            }
            else
            {
                // Capped floor: each themed species appears at most once (SpawnCap 1). But the floor's population
                // target exceeds the 9 roster slots, so a roster of ONLY once-per-floor species can never fill every
                // spawn position and the load hangs forever (the spawn-once retry trap). Repeatable fillers — the
                // dungeon's mimics, or dungeon natives forced repeatable — must carry the population. Add one filler
                // FIRST (the first add always fits the buffer) so a repeatable species is guaranteed present even when
                // the themed models consume the whole budget; then the themed one-offs; then top up with more fillers.
                int[] fillers = ChooseFillers(dungeon, mimicTI, kingTI);

                int guaranteed = -1;
                foreach (int ti in fillers)
                    if (TryAdd(ti)) { capEdits.Add((ti, 0)); guaranteed = ti; break; }

                foreach (int ti in members)             // the themed one-offs (once-per-floor)
                {
                    if (roster.Count >= RosterFillCount) break;
                    if (!TryAdd(ti)) break;             // budget reached
                    capEdits.Add((ti, 1));
                }

                foreach (int ti in fillers)             // remaining slots: more repeatable fillers (SpawnCap 0)
                {
                    if (roster.Count >= RosterFillCount) break;
                    if (ti == guaranteed || roster.Contains(ti)) continue;
                    if (TryAdd(ti)) capEdits.Add((ti, 0));
                }
            }

            while (roster.Count < BtEnemyLayout.EntriesPerFloor) roster.Add(-1);
            return roster.ToArray();
        }

        // The repeatable fillers for a capped themed floor: a 50/50 roll between the dungeon's mimic + king mimic and
        // a shuffled list of dungeon natives (mimic-free).
        private static int[] ChooseFillers(int dungeon, int mimicTI, int kingTI)
        {
            if (_randomizerRng.NextDouble() < ThemeMimicFillChance) return new[] { mimicTI, kingTI };
            int[] natives = NativeFillIndices(dungeon);
            Shuffle(natives);
            return natives;
        }

        // Pick a theme's member TableIndexes plus its flags, weighted by ThemeWeight: dungeon-native themes (e.g. the
        // Demon Shaft regions) are rarer far from home and 4x as likely in their home dungeon. The per-dungeon mimic
        // theme is folded in as one extra option (resolved to the current dungeon, weight 1). requireFullFit themes are
        // excluded up front on any floor whose buffer can't fit the whole group, so they're never partially placed.
        private static void PickTheme(int dungeon, int budget, out string themeName, out int[] members,
            out bool requireFullFit, out bool isMimicTheme)
        {
            var groups = EnemySpecies.ThemeGroups;
            // Weighted candidate list: eligible theme indices (+ the Mimics sentinel = groups.Length). Non-native
            // themes weigh 1; a dungeon-native theme weighs by its proximity to the current dungeon (ThemeWeight).
            var idx = new System.Collections.Generic.List<int>(groups.Length + 1);
            var wts = new System.Collections.Generic.List<double>(groups.Length + 1);
            double total = 0;
            void AddCand(int i, double w) { idx.Add(i); wts.Add(w); total += w; }
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].requireFullFit && ThemeFootprint(groups[i].members) > budget) continue;
                int home = EnemySpecies.ThemeHomeDungeon.TryGetValue(groups[i].name, out int h) ? h : -1;
                double mult = EnemySpecies.ThemeWeightMultiplier.TryGetValue(groups[i].name, out double m) ? m : 1.0;
                AddCand(i, ThemeWeight(home, dungeon) * mult);
            }
            AddCand(groups.Length, 1.0);                 // the per-dungeon Mimics theme (small; always fits), unweighted

            double r = _randomizerRng.NextDouble() * total;
            int pick = idx[idx.Count - 1];
            for (int k = 0; k < idx.Count; k++) { r -= wts[k]; if (r < 0) { pick = idx[k]; break; } }
            System.Collections.Generic.Dictionary<int, EnemyDefaults> dict;
            if (pick == groups.Length) { themeName = "Mimics"; requireFullFit = false; isMimicTheme = true;  dict = EnemySpecies.MimicsByDungeon[dungeon]; }
            else                       { themeName = groups[pick].name; requireFullFit = groups[pick].requireFullFit; isMimicTheme = false; dict = groups[pick].members; }
            members = new System.Collections.Generic.List<int>(dict.Keys).ToArray();
        }

        // Total model-buffer footprint of a themed group (sum of member footprints).
        private static int ThemeFootprint(System.Collections.Generic.Dictionary<int, EnemyDefaults> members)
        {
            int sum = 0;
            foreach (int ti in members.Keys) sum += Footprint(ti);
            return sum;
        }

        // Selection weight for a theme native to dungeon `home`, when staging a floor in `dungeon`. A non-native
        // theme (home < 0) weighs 1. A native theme ramps linearly from ThemeFarWeight (in the most distant dungeon)
        // up to 1.0 at its home dungeon, then gets the ThemeNativeBoost multiplier when you're actually in its home —
        // so the current dungeon's native theme is ThemeNativeBoost× as likely as any non-native theme.
        private const double ThemeNativeBoost = 4.0;   // native theme in its home dungeon
        private const double ThemeFarWeight   = 0.10;  // native theme in the most distant dungeon
        private static double ThemeWeight(int home, int dungeon)
        {
            if (home < 0) return 1.0;
            int maxDist = BtEnemyLayout.DungeonCount - 1;
            double prox = ThemeFarWeight + (1.0 - ThemeFarWeight) * (1.0 - System.Math.Abs(dungeon - home) / (double)maxDist);
            return dungeon == home ? prox * ThemeNativeBoost : prox;
        }

        // Dungeon-native regulars eligible to backfill a capped themed floor. The native pools are mimic-free by
        // construction (EnemySpecies.Native*), so mimics never appear here — they come only from the mimic-fill
        // branch and the dedicated weighted insertion, keeping mimics a single, dungeon-native concern.
        private static int[] NativeFillIndices(int dungeon)
        {
            var keys = EnemySpecies.NativeByDungeon[dungeon].Keys;
            int[] arr = new int[keys.Count];
            keys.CopyTo(arr, 0);
            return arr;
        }

        // In-place Fisher-Yates using the randomizer RNG.
        private static void Shuffle(int[] a)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = _randomizerRng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }
    }
}
