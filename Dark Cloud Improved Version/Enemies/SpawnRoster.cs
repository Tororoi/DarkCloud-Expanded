using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Spawn-roster primitives + snapshot/restore lifecycle, shared by the enemy randomizer and (later) quest
    /// scripting. Edits the static BtEnemyLayout (per-dungeon spawn pools) and EnemySpeciesTable records in place
    /// — pure data writes, no code execution — snapshots the originals, and reverts everything on dungeon exit
    /// via <see cref="RestoreSpawnRoster"/> (which fires the <see cref="Restoring"/> event so per-floor consumers,
    /// e.g. the randomizer, revert their own edits too). Also hosts the roster-spawned-mimic chest disguises.
    /// </summary>
    internal static class SpawnRoster
    {
        /// <summary>Fired at the start of RestoreSpawnRoster, before SpawnRoster reverts its own snapshots, so
        /// per-floor consumers (the randomizer / quest scripts) can revert the BtEnemyLayout edits they made.</summary>
        internal static event System.Action Restoring;

        // ── Spawn-roster editing (crash-free, pure data writes) ──────────────
        /// <summary>
        /// Overwrites the current dungeon's BtEnemyLayout (normal + Ura, all floors) so EVERY spawn is
        /// the given species (by EnemyDefaults.TableIndex). The engine then loads that species' model at
        /// floor load and spawns only it — no code execution, no hook, no crash. Takes effect on the
        /// NEXT floor load (re-enter or descend); already-spawned enemies on the current floor are unchanged.
        /// </summary>
        internal static void SetSpawnRosterToSpecies(int tableIndex, int population = 0)
        {
            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            if (dungeon < 0 || dungeon >= BtEnemyLayout.DungeonCount)
            {
                Console.WriteLine($"[EnemyInjector] roster: not in a known dungeon (checkDungeon={dungeon}). Aborting.");
                return;
            }
            int floors = BtEnemyLayout.FloorCount[dungeon];
            SnapshotRosterIfNeeded(dungeon);
            int[] bases = { BtEnemyLayout.LayoutBase[dungeon], BtEnemyLayout.UraLayoutBase[dungeon] };
            foreach (int layoutBase in bases)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    // entry 0 = this species (Weight +0x8 left untouched — it's vestigial, never read).
                    long entry0Addr = BtEnemyLayout.EntryAddress(layoutBase, floor, 0);
                    Memory.WriteInt(entry0Addr + BtEnemyLayout.Id, tableIndex);
                    // entries 1..8 disabled so nothing else can roll.
                    for (int entry = 1; entry < BtEnemyLayout.EntriesPerFloor; entry++)
                        Memory.WriteInt(BtEnemyLayout.EntryAddress(layoutBase, floor, entry) + BtEnemyLayout.Id, -1);
                }
            }
            // Tame a boss-class species before the floor spawns it. Boss detection is by ModelCode 'c'
            // inside RegularizeBossRecord. This edits the shared species record (persists for the session, like
            // RedirectEnemyModel). A single-species roster MUST be repeatable, or ArrangementPos can't fill
            // the floor and hangs (the spawn-once retry trap). Force SpawnCap repeatable, then regularize.
            SnapshotSpeciesRecordIfNeeded(tableIndex);
            Memory.WriteInt(EnemySpeciesTable.RecordAddress(tableIndex) + EnemySpeciesTable.SpawnCap, 0);
            RegularizeBossRecord(tableIndex);
            // Boss-class species can't be a single-species roster: repeatable spawns many, but multi-part
            // bosses share one skeleton so the extras' limbs desync. Use a mix (e.g. "20,83") so the boss
            // is auto-forced spawn-once and a regular species fills the rest of the floor.
            if (Memory.ReadByte(EnemySpeciesTable.RecordAddress(tableIndex) + EnemySpeciesTable.ModelCode) == (byte)'c')
                Console.WriteLine($"[EnemyInjector] WARNING: TableIndex {tableIndex} is boss-class — a single-species "
                    + "roster will spawn multiple, and multi-part bosses glitch when >1. Use a mix like \"20,83\" instead.");
            BossScriptPatcher.ArmedBoss = -1; // single-species boss isn't a supported config

            if (population > 0) SetPopulationTarget(population);

            Console.WriteLine($"[EnemyInjector] roster: dungeon {dungeon} ({floors} floors, normal+Ura) -> "
                + $"TableIndex {tableIndex} on every spawn. Re-enter a floor to see it.");
        }

        // Tames a boss-class record so a sandbox spawn isn't a damage sponge: lowers HP to 50. Gated on
        // RareDropItemId==65535 ("no signature drop"), which bosses have, as a cheap boss heuristic.
        private static void RegularizeBossRecord(int tableIndex)
        {
            long rec = EnemySpeciesTable.RecordAddress(tableIndex);
            if (Memory.ReadUShort(rec + EnemySpeciesTable.RareDropItemId) != 65535) return;
            // Memory.WriteUShort(rec + EnemySpeciesTable.MaxHp, 50);
        }

        internal static void SetPopulationTarget(int pop)
        {
            foreach (long target in EnemyPlacement.PopulationTargets) Memory.WriteInt(target, pop);
            Console.WriteLine($"[EnemyInjector] population target (0x21D56494/9C/A0) <- {pop} "
                + "(capped by walkable tiles; re-set after floor-select if it gets overwritten).");
        }

        // ── Snapshot / restore so leaving the dungeon reverts to vanilla spawns ──────────────
        // SetSpawnRoster*/SetIceQueenFloorExact overwrite the static BtEnemyLayout (per-dungeon, normal+Ura,
        // all floors) and a few species records IN PLACE. Nothing reloads them, so without this they persist
        // for the whole session. We snapshot the original bytes on the FIRST edit (per dungeon / per record)
        // and write them back on dungeon exit. Dungeon.cs calls RestoreSpawnRoster() there, next to
        // Enemies.RestoreRedirectedEnemies(). Mirrors the RedirectEnemyModel snapshot pattern.
        private static int _rosterSnapDungeon = -1;
        private static byte[] _rosterSnapNormal, _rosterSnapUra;
        private static readonly System.Collections.Generic.Dictionary<int, byte[]> _speciesRecordSnaps = new();
        // Floor-change reset state (see NotifyInFloor): _wasInFloor tracks the on-floor edge; _rosterAppliedToFloor
        // is set once the staged roster's floor has actually loaded, so we revert when LEAVING that floor (not
        // when leaving the floor it was set on, and not mid-load before placement runs).
        private static bool _wasInFloor = false;
        private static bool _rosterAppliedToFloor = false;
        // Set when the randomizer applies a roster to EVERY floor (not just one): persists for the whole dungeon
        // visit, so NotifyInFloor must NOT revert it on each floor change — only RestoreSpawnRoster (dungeon exit).
        private static bool _rosterDungeonWide = false;

        /// <summary>Snapshots the dungeon's BtEnemyLayout (normal+Ura, all floors) once, before the first edit.</summary>
        private static void SnapshotRosterIfNeeded(int dungeon)
        {
            if (_rosterSnapDungeon == dungeon) return; // already captured for this dungeon
            if (_rosterSnapDungeon != -1)
                Console.WriteLine($"[EnemyInjector] WARNING: roster snapshot was for dungeon {_rosterSnapDungeon}, "
                    + $"now editing {dungeon} without a restore in between — replacing snapshot (dungeon {_rosterSnapDungeon} won't revert).");
            int len = BtEnemyLayout.FloorCount[dungeon] * BtEnemyLayout.FloorStride;
            _rosterSnapNormal = Memory.ReadByteArray(BtEnemyLayout.FloorAddress(BtEnemyLayout.LayoutBase[dungeon], 0), len);
            _rosterSnapUra    = Memory.ReadByteArray(BtEnemyLayout.FloorAddress(BtEnemyLayout.UraLayoutBase[dungeon], 0), len);
            _rosterSnapDungeon = dungeon;
            _rosterAppliedToFloor = false; // fresh roster: revert only after its floor has loaded + been left
            Console.WriteLine($"[EnemyInjector] snapshot: BtEnemyLayout dungeon {dungeon} "
                + $"({BtEnemyLayout.FloorCount[dungeon]} floors, normal+Ura, {len} B each).");
        }

        /// <summary>
        /// Drives "roster resets on floor change". Call every dungeon-thread tick with whether the player is on
        /// a dungeon floor. The floor the roster was set for loads fully custom; once you LEAVE that floor (to
        /// the next floor or to town) it reverts to vanilla. Reverting on leave (not on entry) is race-free —
        /// the custom floor has finished loading + placing enemies before we touch the table/records.
        /// </summary>
        internal static void NotifyInFloor(bool inFloor)
        {
            bool rosterActive = _rosterSnapDungeon >= 0 || _speciesRecordSnaps.Count > 0;
            if (inFloor && !_wasInFloor)
            {
                // Entered a floor. If a roster is staged, THIS floor loads with it — mark applied so the next
                // time we leave a floor we revert (but don't revert when leaving the floor it was set on).
                if (rosterActive) _rosterAppliedToFloor = true;
                _mimicChestDone.Clear();   // chest tables reset per floor — allow new mimic chests on this floor
                _mimicWaitLogged.Clear();
            }
            else if (!inFloor && _wasInFloor)
            {
                // Left a floor. If the staged single-floor roster already had its floor, revert now → next floor /
                // town vanilla. A dungeon-wide randomizer roster is NOT reverted here — it spans every floor and
                // only reverts on dungeon exit (RestoreSpawnRoster).
                if (_rosterAppliedToFloor && !_rosterDungeonWide)
                {
                    Console.WriteLine("[EnemyInjector] left the roster's floor — reverting to vanilla spawns.");
                    RestoreSpawnRoster();
                }
            }
            _wasInFloor = inFloor;
        }

        /// <summary>
        /// EXPERIMENT — wake roster-spawned mimics as free-standing proximity enemies.
        /// Mimics ship with their per-species view gate (slot+0xD4) == 0, so CheckViewLevel skips them and
        /// DrawMonstor never draws them (status stuck at 1). Natively they're visible only because a treasure
        /// box is rendered at their position and the chest-open event wakes them — the roster path makes no
        /// box, so they're invisible. Setting the gate low-halfword to 0xFFFF makes CheckViewLevel
        /// proximity-activate them like any enemy. Nothing rewrites 0xD4 after spawn, so one write persists;
        /// the slot resets on the next floor load anyway, so no restore is needed. Gated to custom-roster
        /// floors (won't touch native chest-mimics). Returns the number of mimic slots woken.
        /// </summary>
        internal static bool ActivateRosterMimics = false;   // off: roster mimics stay dormant (pending authentic chest behavior)
        internal static int ActivateMimicSlots()
        {
            if (MakeRosterMimicsChests) return 0;            // mutually exclusive with the authentic-chest path
            if (!ActivateRosterMimics || _rosterSnapDungeon < 0) return 0;
            int woke = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int baseA = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(baseA + EnemySlotOffsets.RenderStatus) < 0) continue;          // free slot
                if (Memory.ReadUShort(baseA + EnemySlotOffsets.ResistancePack1) != 8) continue;   // low ushort = Category; 8 = mimic
                if (Memory.ReadUShort(baseA + 0xD4) == 0xFFFF) continue;                           // already woken
                float x = Memory.ReadFloat(baseA + EnemySlotOffsets.LocationX);
                float z = Memory.ReadFloat(baseA + EnemySlotOffsets.LocationZ);
                float y = Memory.ReadFloat(baseA + EnemySlotOffsets.LocationY);
                Memory.WriteUShort(baseA + 0xD4, 0xFFFF);   // enable the view-activation gate (low halfword)
                Console.WriteLine($"[EnemyInjector] mimic wake: slot {s} gate0xD4 0->0xFFFF, "
                    + $"status={Memory.ReadInt(baseA + EnemySlotOffsets.RenderStatus)} pos=({x:F1},{z:F1},{y:F1}).");
                woke++;
            }
            if (woke > 0) Console.WriteLine($"[EnemyInjector] mimic wake: {woke} mimic slot(s) gated for proximity activation.");
            return woke;
        }

        // ── Authentic mimic chests ───────────────────────────────────────────────────────────────────
        // Make roster-spawned mimics appear as closed treasure chests that wake on open — by replicating
        // SetMimicEvent's pure data writes (no engine code execution). For each dormant mimic-category(8)
        // enemy slot we register a BOX-OBJECT entry (rendered chest) + a STATE entry (Type=8 mimic) inside
        // the CDungeonMap tables, linking BOX.EntityId -> the enemy slot. The engine then renders the chest
        // (DrawItemBox scans all 24 slots), detects proximity (GetActiveIvent over the 48 state slots), and
        // on open OPAnalyz sets the linked enemy's view gate slot+0xD4=1 -> CheckViewLevel wakes the mimic.
        // We DON'T touch the enemy gate (leave it dormant). Per-floor live state — resets on floor reload.
        // See ChestsAddresses.cs for the full table map.
        internal static bool MakeRosterMimicsChests = true;
        private static readonly System.Collections.Generic.HashSet<int> _mimicChestDone = new();
        private static readonly System.Collections.Generic.HashSet<int> _mimicWaitLogged = new(); // diagnostic: logged "waiting at origin" once per slot

        private static int FindFreeBoxSlot()
        {
            for (int i = 0; i < ChestAddresses.ChestSlots.Capacity; i++)
                if (Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(i, ChestSlotOffsets.ActiveFlag)) == 0) return i;
            return -1;
        }
        private static int FindFreeStateSlot()
        {
            for (int i = 0; i < ChestAddresses.ChestStateTable.Capacity; i++)
                if (Memory.ReadInt(ChestAddresses.ChestStateTable.SlotAddr(i, ChestStateOffsets.Type)) == -1) return i;
            return -1;
        }
        // True if an active mimic chest (STATE.Type==8 → its linked BOX) is already linked to this enemy slot —
        // i.e. the engine made a native SetMimicEvent chest at spawn, or we already placed a decoy for it.
        private static bool MimicChestExistsForEnemy(int enemySlot)
        {
            for (int st = 0; st < ChestAddresses.ChestStateTable.Capacity; st++)
            {
                if (Memory.ReadInt(ChestAddresses.ChestStateTable.SlotAddr(st, ChestStateOffsets.Type)) != 8) continue;
                int box = Memory.ReadInt(ChestAddresses.ChestStateTable.SlotAddr(st, ChestStateOffsets.BoxLink));
                if (box < 0 || box >= ChestAddresses.ChestSlots.Capacity) continue;
                if (Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(box, ChestSlotOffsets.ActiveFlag)) == 0) continue;
                if (Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(box, ChestSlotOffsets.EntityId)) == enemySlot) return true;
            }
            return false;
        }

        /// <summary>
        /// Registers a closed-chest disguise for every dormant roster mimic on the floor that doesn't have one
        /// yet. Call each dungeon-thread tick (cheap no-op off custom-roster floors); dedups via _mimicChestDone
        /// and skips not-yet-placed mimics (pos 0,0,0). Returns how many chests were newly created this call.
        /// </summary>
        internal static int SpawnMimicChestsOnFloor()
        {
            if (!MakeRosterMimicsChests || _rosterSnapDungeon < 0) return 0;
            int made = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (_mimicChestDone.Contains(s)) continue;
                int eBase = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(eBase + EnemySlotOffsets.RenderStatus) < 0) continue;            // free slot
                if (Memory.ReadUShort(eBase + EnemySlotOffsets.ResistancePack1) != 8) continue;     // category 8 = mimic
                // Read the SPAWN position from the CCharacter render object (PosAddr), NOT the floor-slot Location:
                // the slot Location is the per-frame world pos updated by Step, but dormant mimics (gate 0) never
                // Step, so their Location stays frozen at 0,0,0. SetupViewMonstor bakes the real spawn pos into the
                // CCharacter, so the mimic IS placed — we were just reading the wrong field.
                byte[] pos = Memory.ReadByteArray(EnemyAddresses.CharObjects.PosAddr(s), 16);        // X,Z,Y,W
                float px = BitConverter.ToSingle(pos, 0), pz = BitConverter.ToSingle(pos, 4), py = BitConverter.ToSingle(pos, 8);
                if (px == 0f && pz == 0f && py == 0f)
                {
                    if (_mimicWaitLogged.Add(s))
                        Console.WriteLine($"[MimicChest] slot {s} mimic CCharacter pos still 0,0,0 — waiting for spawn placement.");
                    continue;
                }
                // PREVENT the duplicate at the source: if this mimic slot already has a mimic chest (Type 8 box
                // linked to it — either a native SetMimicEvent chest the engine made at spawn, or one we already
                // placed), do NOT add a second. Both link BOX.EntityId = enemy slot, so we can match exactly.
                if (MimicChestExistsForEnemy(s))
                {
                    Console.WriteLine($"[MimicChest] slot {s} already has a mimic chest — not adding a decoy.");
                    _mimicChestDone.Add(s);
                    continue;
                }
                Console.WriteLine($"[MimicChest] slot {s} mimic placed (CCharacter) at ({px:F1},{pz:F1},{py:F1}) — creating chest.");

                int boxIdx = FindFreeBoxSlot();
                int stateIdx = FindFreeStateSlot();
                if (boxIdx < 0 || stateIdx < 0)
                {
                    Console.WriteLine($"[MimicChest] no free box/state slot (box={boxIdx}, state={stateIdx}); enemy slot {s} skipped.");
                    _mimicChestDone.Add(s); // don't retry forever
                    continue;
                }

                // King mimics use the BIG chest (ChestSize 0, models -0x4398/-0x439c) with the taller 10.0 height
                // offset; regular mimics use the small/regular chest (ChestSize 1) with the 8.0 offset — matching the
                // native SetMimicEvent s2 (0 big / 1 small). Both are category 8, so distinguish by species Id.
                ushort eid = Memory.ReadUShort(eBase + EnemySlotOffsets.EnemySpeciesId);
                bool isKing = EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.Name != null && def.Name.Contains("King Mimic");
                int chestSize = isKing ? 0 : 1;
                float hOff    = isKing ? 10f : 8f;

                // BOX-OBJECT entry (rendered chest): position vector + occupied + enemy link + type/open state.
                Memory.WriteByteArray(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.WorldX), pos); // -0x10: pos quadword
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ActiveFlag), 1);
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.EntityId),   s);     // link -> enemy slot (OPAnalyz wakes this)
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ConstOne),   1);
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ChestSize),  chestSize); // 1 = small/regular, 0 = big (king)
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.OpenState),  0);

                // STATE/META entry (Type=8 mimic): position + proximity radius + box link. Height/Z offset matches
                // the chest size (8.0 small, 10.0 big) like the native SetMimicEvent path.
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Type),    8);
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Zero),    0);
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.BoxLink), boxIdx);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosX),    px);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosY),    pz);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosZ),    py + hOff);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Height),  hOff);

                // Extend the box loop count so CheckTreasureBox covers the new slot (DrawItemBox already scans all 24).
                int cnt = Memory.ReadInt(ChestAddresses.ChestSlots.BoxLoopCount());
                if (boxIdx + 1 > cnt) Memory.WriteInt(ChestAddresses.ChestSlots.BoxLoopCount(), boxIdx + 1);

                _mimicChestDone.Add(s);
                made++;
                Console.WriteLine($"[MimicChest] enemy slot {s} -> {(isKing ? "BIG" : "small")} chest (box {boxIdx}, state {stateIdx}) at ({px:F1},{pz:F1},{py:F1}); count {cnt}->{Math.Max(cnt, boxIdx + 1)}.");
            }
            return made;
        }


        /// <summary>Snapshots a species record (full 0x9C) once, before the first edit to it.</summary>
        internal static void SnapshotSpeciesRecordIfNeeded(int tableIndex)
        {
            if (_speciesRecordSnaps.ContainsKey(tableIndex)) return;
            _speciesRecordSnaps[tableIndex] =
                Memory.ReadByteArray(EnemySpeciesTable.RecordAddress(tableIndex), EnemySpeciesTable.Stride);
        }

        /// <summary>
        /// Reverts every SetSpawnRoster*/SetIceQueenFloorExact edit by writing the snapshots back: the
        /// dungeon's BtEnemyLayout (normal+Ura) and any modified species records. Also disarms the boss patch.
        /// Call on dungeon exit. Safe/idempotent (no-op when nothing was snapshotted).
        /// </summary>
        internal static void RestoreSpawnRoster()
        {
            // Let per-floor consumers (e.g. the randomizer / quest scripts) revert their own BtEnemyLayout edits first.
            Restoring?.Invoke();
            // Whole-dungeon snapshot (sandbox / SetSpawnRoster* paths). A per-floor consumer marks _rosterSnapDungeon
            // for the mimic gate but leaves these arrays null — it reverts its own edits via the Restoring event above.
            if (_rosterSnapDungeon >= 0 && _rosterSnapNormal != null)
            {
                Memory.WriteByteArray(BtEnemyLayout.FloorAddress(BtEnemyLayout.LayoutBase[_rosterSnapDungeon], 0), _rosterSnapNormal);
                Memory.WriteByteArray(BtEnemyLayout.FloorAddress(BtEnemyLayout.UraLayoutBase[_rosterSnapDungeon], 0), _rosterSnapUra);
                Console.WriteLine($"[EnemyInjector] restore: BtEnemyLayout dungeon {_rosterSnapDungeon} reverted (normal+Ura).");
            }
            if (_speciesRecordSnaps.Count > 0)
            {
                foreach (var kv in _speciesRecordSnaps)
                    Memory.WriteByteArray(EnemySpeciesTable.RecordAddress(kv.Key), kv.Value);
                Console.WriteLine($"[EnemyInjector] restore: {_speciesRecordSnaps.Count} species record(s) reverted "
                    + $"[{string.Join(",", _speciesRecordSnaps.Keys)}].");
            }
            _rosterSnapDungeon = -1; _rosterSnapNormal = null; _rosterSnapUra = null;
            _speciesRecordSnaps.Clear();
            _rosterAppliedToFloor = false;
            _rosterDungeonWide = false;   // next dungeon entry re-randomizes from vanilla
            _mimicChestDone.Clear();
            BossScriptPatcher.ArmedBoss = -1;
        }

        /// <summary>
        /// Claim the current dungeon for a per-floor consumer (the randomizer / quest scripts) that edits BtEnemyLayout
        /// entries itself and reverts them via the <see cref="Restoring"/> event. Sets the mimic-chest gate
        /// (_rosterSnapDungeon) without taking a whole-dungeon snapshot; <paramref name="dungeonWide"/> keeps
        /// NotifyInFloor from reverting on each floor change (only RestoreSpawnRoster, on dungeon exit, reverts).
        /// </summary>
        internal static void MarkActive(int dungeon, bool dungeonWide)
        {
            _rosterSnapDungeon = dungeon;
            if (dungeonWide) _rosterDungeonWide = true;
        }

        /// <summary>
        /// Sets the current dungeon's roster (normal + Ura, all floors) to a MIX of species: fills entries
        /// 0..n-1 with the given TableIndexes (Id +0x4 only; weight is vestigial), disables the rest. Every
        /// distinct species in the roster gets its model loaded at floor load, so this also tests how many
        /// different enemy models a floor can hold. Use regular (non-boss) indices — boss indices still
        /// black-screen regardless of count.
        /// </summary>
        internal static void SetSpawnRosterMix(int[] tableIndices, bool[] spawnOnce = null, int population = 0)
        {
            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            if (dungeon < 0 || dungeon >= BtEnemyLayout.DungeonCount)
            {
                Console.WriteLine($"[EnemyInjector] roster mix: not in a known dungeon (checkDungeon={dungeon}). Aborting.");
                return;
            }
            int floors = BtEnemyLayout.FloorCount[dungeon];
            int n = Math.Min(tableIndices.Length, BtEnemyLayout.EntriesPerFloor);
            SnapshotRosterIfNeeded(dungeon);

            // Step 1 toward snapshot/restore: log the live BtEnemyLayout BEFORE we overwrite it, so we can
            // see the engine's default per-floor entries (Count/Id/Weight) and confirm exactly what the
            // edit changes. Paired with the "after" dump below.
            DumpRoster("before", dungeon);

            // Load any boss-class ('c' ModelCode) species FIRST. BtLoadMonstor allocates models/scripts in
            // roster order, so a boss at entry 0 lands its STB at a heap address independent of the filler
            // species — letting the runtime script patch hit a CONSISTENT address. Stable reorder.
            {
                int[] ti2 = new int[n]; bool[] so2 = new bool[n]; int k = 0;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < n; i++)
                    {
                        bool boss = Memory.ReadByte(EnemySpeciesTable.RecordAddress(tableIndices[i]) + EnemySpeciesTable.ModelCode) == (byte)'c';
                        if (boss == (pass == 0))
                        {
                            ti2[k] = tableIndices[i];
                            so2[k] = spawnOnce != null && i < spawnOnce.Length && spawnOnce[i];
                            k++;
                        }
                    }
                tableIndices = ti2; spawnOnce = so2;
            }

            // (Weight +0x8 is intentionally NOT written: the spawn path never reads it — selection is uniform
            // rand%(distinct loaded species). Only Id +0x4 matters. See EnemyAddresses.BtEnemyLayout.)

            // Per-species "spawn once vs repeatable" flag = EnemySpeciesTable.SpawnCap (+0x78): 0/3 = repeatable,
            // anything else = at most one per floor (the placement loop retries, so total stays 15). Write
            // it on the source record so the floor-load copy carries it.
            // The boss (cNNx) is spawn-once. Mark companions spawn-once with '!'. Leave at least one REGULAR enemy
            // unmarked (repeatable) so it fills the remaining slots up to the floor's population (15) — the floor
            // won't load until all slots are filled, and spawn-once species can each occupy only one slot.
            // (We can't bias the spawn toward the boss by duplicating its entry — BtLoadMonstor loads each entry's
            // model, so a duplicate boss double-loads its large model and blows the buffer, failing the whole spawn.)
            int armedBoss = -1;
            for (int i = 0; i < n; i++)
            {
                long rec = EnemySpeciesTable.RecordAddress(tableIndices[i]);
                // Boss-class = ModelCode starts with 'c' (cNNx); stable across RegularizeBossRecord (which
                // only lowers HP). Multi-part bosses share one skeleton, so >1 instance glitches —
                // force them spawn-once regardless of the '!' flag.
                bool isBossClass = Memory.ReadByte(rec + EnemySpeciesTable.ModelCode) == (byte)'c';
                bool isOnce = (spawnOnce != null && i < spawnOnce.Length && spawnOnce[i]) || isBossClass;
                SnapshotSpeciesRecordIfNeeded(tableIndices[i]);
                Memory.WriteInt(rec + EnemySpeciesTable.SpawnCap, isOnce ? 2 : 0);
                // Tame ONLY the boss (cNNx) — lowers its HP. Companions are left untouched so their behavior is intact.
                if (isBossClass) RegularizeBossRecord(tableIndices[i]);
                // Arm the script patch for the first boss that has a known _INITIALIZE fix (Dran/Master Utan/
                // MinotaurJoe/c22a). Other 'c' bosses still spawn (spawn-once) but may be displaced — WIP.
                if (armedBoss < 0 && BossScriptPatcher.IsPatchable(tableIndices[i])) armedBoss = tableIndices[i];
            }
            BossScriptPatcher.ArmedBoss = armedBoss;
            if (armedBoss == EnemySpecies.IceQueen.TableIndex.Value) BossScriptPatcher.TagCompanions();   // name Ice Queen's eid-0 companions in logs
            if (armedBoss >= 0)
                Console.WriteLine($"[BossPatch] armed for boss {armedBoss} — will NOP its label-1 _INITIALIZE in the loaded STB.");

            int[] bases = { BtEnemyLayout.LayoutBase[dungeon], BtEnemyLayout.UraLayoutBase[dungeon] };
            foreach (int layoutBase in bases)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    for (int e = 0; e < BtEnemyLayout.EntriesPerFloor; e++)
                    {
                        long ea = BtEnemyLayout.EntryAddress(layoutBase, floor, e);
                        if (e < n)
                        {
                            Memory.WriteInt(ea + BtEnemyLayout.Id, tableIndices[e]);
                        }
                        else
                        {
                            Memory.WriteInt(ea + BtEnemyLayout.Id, -1);
                        }
                    }
                }
            }
            if (population > 0) SetPopulationTarget(population);
            // Log the live BtEnemyLayout AFTER the overwrite — compare against the "before" dump to confirm
            // the edit landed and to see the new per-floor entries.
            DumpRoster("after", dungeon);
            var onceList = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
                if (spawnOnce != null && i < spawnOnce.Length && spawnOnce[i]) onceList.Add(tableIndices[i]);
            string once = onceList.Count == 0 ? "none" : string.Join(",", onceList);
            Console.WriteLine($"[EnemyInjector] roster mix: dungeon {dungeon}, {n} species "
                + $"[{string.Join(",", tableIndices[..n])}] spawn-once=[{once}] across {floors} floors. Re-enter a floor.");
        }

        /// <summary>
        /// Diagnostic dump of the current dungeon's BtEnemyLayout (normal + Ura, every floor). Logs each
        /// floor's non-empty entries as raw [entry] Count/Id/Weight words exactly as they sit in EE RAM.
        /// Used to verify SetSpawnRosterMix's edits and as the basis for the snapshot/restore work.
        /// </summary>
        private static void DumpRoster(string tag, int dungeon)
        {
            int floors = BtEnemyLayout.FloorCount[dungeon];
            var bases = new (string name, int layoutBase)[]
            {
                ("normal", BtEnemyLayout.LayoutBase[dungeon]),
                ("ura",    BtEnemyLayout.UraLayoutBase[dungeon]),
            };
            foreach (var (name, layoutBase) in bases)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    for (int e = 0; e < BtEnemyLayout.EntriesPerFloor; e++)
                    {
                        long ea = BtEnemyLayout.EntryAddress(layoutBase, floor, e);
                        int id = Memory.ReadInt(ea + BtEnemyLayout.Id);
                        if (id == -1) continue; // unused entry
                        int cnt = Memory.ReadInt(ea + BtEnemyLayout.Count);
                        int wt  = Memory.ReadInt(ea + BtEnemyLayout.Weight);
                        parts.Add($"[{e}] cnt={cnt} id={id} wt={wt}");
                    }
                    Console.WriteLine($"[RosterDump:{tag}] d{dungeon} {name} f{floor}: "
                        + (parts.Count == 0 ? "(all empty)" : string.Join("  ", parts)));
                }
            }
        }

        // TEST: write the EXACT real Shipwreck floor-18 boss block (count/id/weight per entry) to the current
        // dungeon's floors — including the +0x0 Count field the normal roster path never writes — to see whether
        // matching the game's block reproduces the deterministic boss spawn (Ice Queen -> slot 0 + companions).
        internal static void SetIceQueenFloorExact()
        {
            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            if (dungeon < 0 || dungeon >= BtEnemyLayout.DungeonCount)
            {
                Console.WriteLine($"[IQexact] not in a known dungeon (checkDungeon={dungeon}).");
                return;
            }
            // (count, tableIndex/id, weight) — verbatim from the real SW floor-18 BtEnemyLayout block.
            var block = new (int cnt, int id, int wt)[]
            {
                (1, EnemySpecies.IceQueen.TableIndex.Value,  40),   // Ice Queen
                (1, EnemySpecies.IQComp101.TableIndex.Value, 10),   // baria
                (1, EnemySpecies.IceArrow.TableIndex.Value,  10),   // korinoya
                (1, EnemySpecies.IQComp102.TableIndex.Value, 10),   // kori
                (1, EnemySpecies.IQComp103.TableIndex.Value, 10),   // i_meteo
                (1, EnemySpecies.SWComp92.TableIndex.Value,  10),   // reiki
                (1, EnemySpecies.IQComp104.TableIndex.Value, 10),   // i_tatumaki
            };
            int floors = BtEnemyLayout.FloorCount[dungeon];
            SnapshotRosterIfNeeded(dungeon);
            int armed = -1;
            foreach (var (_, id, _) in block)
            {
                long rec = EnemySpeciesTable.RecordAddress(id);
                bool bossClass = Memory.ReadByte(rec + EnemySpeciesTable.ModelCode) == (byte)'c';
                SnapshotSpeciesRecordIfNeeded(id);
                Memory.WriteInt(rec + EnemySpeciesTable.SpawnCap, bossClass ? 2 : 0);  // boss spawn-once
                if (bossClass) RegularizeBossRecord(id);                                // de-sentinel ONLY the boss (companions stay sentinel effect entities)
                if (armed < 0 && BossScriptPatcher.IsPatchable(id)) armed = id;
            }
            BossScriptPatcher.ArmedBoss = armed;
            if (armed == EnemySpecies.IceQueen.TableIndex.Value) BossScriptPatcher.TagCompanions();   // name Ice Queen's eid-0 companions in logs
            foreach (int layoutBase in new[] { BtEnemyLayout.LayoutBase[dungeon], BtEnemyLayout.UraLayoutBase[dungeon] })
                for (int floor = 0; floor < floors; floor++)
                    for (int e = 0; e < BtEnemyLayout.EntriesPerFloor; e++)
                    {
                        long ea = BtEnemyLayout.EntryAddress(layoutBase, floor, e);
                        if (e < block.Length)
                        {
                            Memory.WriteInt(ea + BtEnemyLayout.Count, block[e].cnt);
                            Memory.WriteInt(ea + BtEnemyLayout.Id, block[e].id);
                            Memory.WriteInt(ea + BtEnemyLayout.Weight, block[e].wt);
                        }
                        else Memory.WriteInt(ea + BtEnemyLayout.Id, -1);
                    }
            Console.WriteLine($"[IQexact] wrote exact SW floor-18 block to dungeon {dungeon} (all floors, incl. Count field). Re-enter a floor.");
        }

        /// <summary>
        /// Index test: point the whole roster at a REGULAR enemy index (Dasher, which DBC spawns
        /// normally and is not boss-gated), then write the boss's ModelCode into Dasher's record so the
        /// spawned "Dashers" load the boss MESH. ModelCodeCopy is left as Dasher's, so it keeps regular
        /// Dasher AI (no boss-spawn handling). If the floor loads with Minotaur-looking Dashers, the
        /// record INDEX was the boss-spawn gate and smuggling the mesh through a regular index is the fix.
        /// (This is the RedirectEnemyModel "ModelCode-only" trick driven from the roster side. Edits the
        /// carrier's record; persists for the session.)
        /// </summary>
        internal static void RosterIndexTest(int bossTableIndex)
        {
            int carrier = EnemySpecies.Dasher.TableIndex.Value;   // regular DBC enemy, not boss-gated
            SetSpawnRosterToSpecies(carrier);                // make every spawn use the carrier index
            // copy boss ModelCode (0x000) -> carrier ModelCode, so the carrier renders as the boss
            byte[] bossCode = Memory.ReadByteArray(
                EnemySpeciesTable.RecordAddress(bossTableIndex) + EnemySpeciesTable.ModelCode, 4);
            Memory.WriteByteArray(
                EnemySpeciesTable.RecordAddress(carrier) + EnemySpeciesTable.ModelCode, bossCode);
            string code = System.Text.Encoding.ASCII.GetString(bossCode);
            Console.WriteLine($"[EnemyInjector] index test: roster -> carrier index {carrier} (Dasher); "
                + $"record {carrier}.ModelCode <- record {bossTableIndex}.ModelCode (\"{code}\"). Re-enter a floor.");
        }

        // NOTE: live slot conversion (transplanting a species' render-object model words from a donor slot)
        // was tested and removed — it re-skins the model + name cosmetically but the converted enemy is
        // un-hittable and keeps the original species' (now-buggy) AI. Collision + AI are engine state set
        // at spawn, not flat data. See enemy-spawn-system.md §"Live slot conversion (tested, not viable)".

        /// <summary>
        /// Post-spawn per-species cap: keeps at most <paramref name="maxKeep"/> live enemies of the given
        /// species (by TableIndex) on the current floor and removes the rest by setting their slot
        /// RenderStatus (0x000) to -1 — which both stops DrawMonstor (it draws only RenderStatus==2) and
        /// halts their AI. Call after the floor has populated. The roster's weighted-random selection has
        /// no per-species hard cap, so this enforcement pass is how you bound a specific monster to n.
        /// </summary>
        internal static int CapSpeciesOnFloor(int tableIndex, int maxKeep)
        {
            // slot's EnemySpeciesId (0x42) holds the species record's EID, not the TableIndex — resolve it.
            ushort eid = Memory.ReadUShort(EnemySpeciesTable.RecordAddress(tableIndex) + EnemySpeciesTable.EnemySpeciesId);
            int kept = 0, removed = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int rs = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus));
                if (rs < 0) continue; // already free/dead
                ushort sid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                if (sid != eid) continue;
                if (kept < maxKeep) { kept++; continue; }
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus), -1);
                removed++;
            }
            Console.WriteLine($"[EnemyInjector] cap: species EID {eid} (TableIndex {tableIndex}) — kept {kept}, "
                + $"removed {removed} (RenderStatus -> -1).");
            return removed;
        }

        /// <summary>
        /// Writes the boss's ModelCode into the carrier (Dasher) record's ModelCodeCopy (0x040) — the
        /// field the boss-AI dispatch reads. Intended to be clicked AFTER the index test has spawned the
        /// boss-mesh-on-Dasher enemies, to see whether the engine re-dispatches them to the boss's AI.
        /// (ModelCodeCopy is normally consumed at spawn, so already-live instances may not pick it up;
        /// if not, the same write done BEFORE the floor spawns will bind boss AI at spawn time instead.)
        /// </summary>
        internal static void SetCarrierBossAI(int bossTableIndex)
        {
            int carrier = EnemySpecies.Dasher.TableIndex.Value;
            byte[] bossCode = Memory.ReadByteArray(
                EnemySpeciesTable.RecordAddress(bossTableIndex) + EnemySpeciesTable.ModelCode, 4);
            Memory.WriteByteArray(
                EnemySpeciesTable.RecordAddress(carrier) + EnemySpeciesTable.ModelCodeCopy, bossCode);
            string code = System.Text.Encoding.ASCII.GetString(bossCode);
            Console.WriteLine($"[EnemyInjector] carrier {carrier}.ModelCodeCopy <- \"{code}\" (boss AI dispatch). "
                + "Existing spawns may not re-dispatch; if not, it binds on the next spawn/floor.");
        }
    }
}
