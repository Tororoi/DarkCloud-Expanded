using System;
using System.Globalization;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Runtime enemy model + AI re-skin via an injected MIPS code cave.
    ///
    /// Lets you load ANY species into ANY of the 16 live enemy slots after a floor has loaded,
    /// by driving the engine's own routines:
    ///   PRELOAD (mode 1): CMonstorUnit::SetupBaseModel(this, N, tableIndex, 0x26, MonstorModelBuffer)
    ///                     @ ELF 0x001DFE90 — loads the species mesh + 0x9C species record into
    ///                     model block N. Does blocking disc I/O, so run it at a safe moment.
    ///   INSTANTIATE (mode 2): free slot N (RenderStatus = -1), then
    ///                     CMonstorUnit::SetupViewMonstor(this, N, &pos, 0) @ ELF 0x001E02B0 —
    ///                     builds the live monster from model block N into the freed slot. No disc I/O.
    ///
    /// The cave polls a PARAM block each frame from a 1-instruction detour at OpA_MotionProcess
    /// (native 0x01DB6C04). See tools/build_cave.py for the assembler source / regeneration, and
    /// the BtEnemyLayout / EnemySpeciesTable docs in EnemyAddresses.cs for the data model.
    ///
    /// IMPORTANT
    ///  • <see cref="CaveBase"/> must point at a debugger-verified FREE EE RAM region (>= 0x200 bytes).
    ///    The default 0x01F70000 is a placeholder — verify it, then update and call <see cref="Install"/>.
    ///  • The hook lives in the dun overlay, which reloads per floor — call <see cref="Install"/> on
    ///    each floor load.
    ///  • SetupViewMonstor instantiates into the FIRST slot whose RenderStatus(+0)==-1, building it
    ///    from THAT slot's model block. Mode 2 frees only slot N, so it lands in N only if no
    ///    lower-numbered slot is also free. On a fully-spawned floor that is automatic; otherwise
    ///    occupy the lower free slots first, and always preload (mode 1) the SAME N you instantiate.
    ///  • Single-unit enemies only; multi-part/bosses need extra model blocks + a behavior script.
    /// </summary>
    internal static class EnemyModelInjector
    {
        // ── Configuration ────────────────────────────────────────────────────
        /// <summary>
        /// Master switch. Stays FALSE until <see cref="CaveBase"/> has been verified against a known-free
        /// RAM region with the PCSX2 debugger. While false, <see cref="Install"/> is a no-op, so the
        /// (placeholder) cave is never written and game code is never patched. Flip to true only after
        /// verifying the base — otherwise you risk corrupting RAM / crashing during normal play.
        /// </summary>
        internal static bool Enabled = false;

        /// <summary>
        /// Native PS2 base of the cave region (PARAM block is the first 0x20 bytes; code at +0x20).
        /// MUST be RAM the game never writes. History of bad picks (and why a write breakpoint is the
        /// only real test): 0x01F70000 was inside the active dungeon heap (garbage -> crash); 0x0027D090
        /// looked like static padding in two dumps but the use-item/back-floor menu writes there.
        /// 0x01400000 is buried deep inside a 3.4 MB contiguous zero block in main BSS (0x01340E20..
        /// 0x01698CC0), zero across three states including the back-floor menu, no nonzero within
        /// 8 KB before / 16 KB after — i.e. reserved/unused. Still: verify with a 512-byte write
        /// breakpoint across your scenarios before trusting it.
        /// The embedded template is assembled for 0x01F70000 and relocated to this base at runtime.
        /// </summary>
        internal const uint CaveBase = 0x01400000;   // deep in a 3.4 MB unused BSS block (verify w/ write bp)
        // Runtime hook address, in the OpA_MotionProcess EPILOGUE (the single per-frame `jr ra`
        // return path; the top of the function is first-frame init the common path skips). The dun
        // overlay loads WITH its 0x80 file header, so symbol 0x01DB73A0 is at +0x80 = 0x01DB7420
        // (verified against live dungeon eeMemory dumps). Main-segment call targets are unshifted.
        internal const uint HookAddr = 0x01DB7420;   // OpA_MotionProcess epilogue; word reproduced by the cave
        internal const uint OriginalHookWord = 0xC7BA0018; // `lwc1 $f26,0x18($sp)` — restored by Uninstall()

        // PARAM block field offsets (relative to CaveBase)
        private const int P_TRIGGER = 0x00; // write !=0 to fire; cave clears it when done
        private const int P_MODE    = 0x04; // 1 = preload, 2 = instantiate
        private const int P_N       = 0x08; // model-block index / live slot to free
        private const int P_T       = 0x0C; // tableIndex (mode 1)
        private const int P_POS     = 0x10; // float[3] position (mode 2)
        private const int P_HEARTBEAT = 0x1C; // int — incremented by the cave every frame (diagnostic)
        private const int MODE_PRELOAD = 1;
        private const int MODE_INSTANTIATE = 2;

        // ── Cave template (generated by tools/build_cave.py for CaveBase = 0x01F70000) ──
        // Relocated to the configured CaveBase by BuildCave(); regenerate with the script only if
        // you change the cave logic, hook site, or engine addresses.
        private const string CaveTemplateHex =
            "90ffbd270000bfaf0400a1af0800a2af0c00a3af1000a4af1400a5af1800a6af1c00a7af2000a8af2400a9af2800aaaf2c00adaf3000b0af3400b1af3800b2af104800003c00a9af124800004000a9aff701023c000042341c00498c010029251c0049ac" +
            "0000438c300060100000000000000000000040ac04004d8c0800518c0c00528cdf01103cd0871036010001340900a1110000000000000000020001341100a11100000000000000001f000010000000000000000021200002212820022130400226000724" +
            "f001083cd0660835a47f070c00000000000000001300001000000000000000009001033418002302124800000100013cd0e321342148210121480902ffff0a2400002aad2120000221282002f701063c1000c63400000724ac80070c0000000000000000" +
            "4000a98f130020013c00a98f110020010000bf8f0400a18f0800a28f0c00a38f1000a48f1400a58f1800a68f1c00a78f2000a88f2400a98f2800aa8f2c00ad8f3000b08f3400b18f3800b28f7000bd271800bac70add76080000000000000000";

        // Byte offsets within the cave code of the address immediates to relocate.
        // (lui/ori pairs that load the PARAM base and the &pos pointer.)
        private const int OFF_PARAM_HI = 0x50; // lui $v0, hi(PARAM)
        private const int OFF_PARAM_LO = 0x54; // ori $v0, lo(PARAM)
        private const int OFF_POS_HI   = 0x114; // lui $a2, hi(PARAM+0x10)
        private const int OFF_POS_LO   = 0x118; // ori $a2, lo(PARAM+0x10)
        private const uint TEMPLATE_BASE = 0x01F70000; // base the template was assembled for

        // ── Install ──────────────────────────────────────────────────────────
        /// <summary>
        /// Writes the relocated cave and the hook detour. Call once per floor load.
        /// No-op unless <see cref="Enabled"/> is true (so an unverified <see cref="CaveBase"/>
        /// never patches game memory).
        /// </summary>
        internal static void Install()
        {
            if (!Enabled) return;
            // Order matters: write the cave, fully clear PARAM, THEN arm the hook last. Otherwise the
            // hook goes live while PARAM still holds stale/garbage trigger+mode and the cave fires
            // SetupBaseModel/SetupViewMonstor with bad args on the next frame (instant crash).
            // Write the cave with read-back verification + retry FIRST and only arm the hook if the
            // cave fully landed — otherwise the hook would jump into a half-written / zeroed region
            // (a nop-sled through BSS) and crash.
            bool caveOk = WriteVerified(CaveBase + 0x20, BuildCave(), "cave");
            Memory.WriteInt(CaveBase + P_TRIGGER, 0);
            Memory.WriteInt(CaveBase + P_MODE, 0);
            Memory.WriteInt(CaveBase + P_HEARTBEAT, 0);
            if (!caveOk)
            {
                Console.WriteLine("[EnemyInjector] cave did not verify. Aborting install.");
                return;
            }
            if (!EnableCodeHook)
            {
                // CONFIRMED: writing the `j cave` patch into the live recompiled code page at HookAddr
                // crashes PCSX2 (PINE cannot safely modify executing EE code — the emulator disconnects).
                // The cave is harmless on its own; we just never arm the code hook. A working trigger
                // needs a DATA-based hook (a function pointer in writable RAM that the engine calls each
                // frame) or driving the game's own script/spawn data — see notes in this file's header.
                Console.WriteLine("[EnemyInjector] cave written; code hook DISABLED (PINE code-patching "
                    + "crashes PCSX2). Injector is inert until a data-based trigger is implemented.");
                return;
            }
            bool hookOk = WriteVerified(HookAddr, BuildHook(), "hook");
            Console.WriteLine($"[EnemyInjector] install {(hookOk ? "OK" : "FAILED")}; "
                + $"cave@0x{CaveBase + 0x20:X8}=0x{Memory.ReadUInt(CaveBase + 0x20):X8} "
                + $"hook@0x{HookAddr:X8}=0x{Memory.ReadUInt(HookAddr):X8}");
        }

        /// <summary>
        /// Arming the cave requires patching live EE code at <see cref="HookAddr"/>, which CRASHES PCSX2
        /// (PINE writes to a recompiled code page take down the emulator — confirmed). Leave false until a
        /// data-based trigger (writable per-frame function pointer / script injection) replaces the code hook.
        /// </summary>
        internal static bool EnableCodeHook = false;

        /// <summary>Writes <paramref name="data"/> and verifies it stuck, retrying mismatched bytes. Logs the outcome.</summary>
        private static bool WriteVerified(long addr, byte[] data, string label, int passes = 4)
        {
            for (int attempt = 0; attempt < passes; attempt++)
            {
                Memory.WriteByteArray(addr, data);
                byte[] back = Memory.ReadByteArray(addr, data.Length);
                int firstBad = -1, badCount = 0;
                for (int i = 0; i < data.Length; i++)
                    if (back[i] != data[i]) { badCount++; if (firstBad < 0) firstBad = i; }
                if (badCount == 0)
                {
                    if (attempt > 0) Console.WriteLine($"[EnemyInjector] {label} verified after {attempt + 1} passes ({data.Length} B).");
                    return true;
                }
                Console.WriteLine($"[EnemyInjector] {label} pass {attempt + 1}: {badCount}/{data.Length} bytes wrong "
                    + $"(first at +0x{firstBad:X}: got 0x{back[firstBad]:X2} want 0x{data[firstBad]:X2}).");
            }
            return false;
        }

        /// <summary>Reads the cave's per-frame heartbeat counter. If it advances, the hook+cave are running.</summary>
        internal static int Heartbeat() => Memory.ReadInt(CaveBase + P_HEARTBEAT);

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
                    long e0 = BtEnemyLayout.EntryAddress(layoutBase, floor, 0);
                    Memory.WriteInt(e0 + BtEnemyLayout.Id, tableIndex);
                    // entries 1..8 disabled so nothing else can roll.
                    for (int e = 1; e < BtEnemyLayout.EntriesPerFloor; e++)
                        Memory.WriteInt(BtEnemyLayout.EntryAddress(layoutBase, floor, e) + BtEnemyLayout.Id, -1);
                }
            }
            // De-sentinel boss-class species before the floor spawns it. AttackPower == 65535 in the
            // species record is the "boss-class" marker; it makes the engine run boss-spawn handling
            // (arena-origin placement + encounter/arena setup) that stalls a normal floor load (black
            // screen). Setting it to a normal value (100) makes the engine spawn it as an ordinary
            // enemy. This edits the shared species record (persists for the session, like RedirectEnemyModel).
            // A single-species roster MUST be repeatable, or ArrangementPos can't fill the floor and hangs
            // (the spawn-once retry trap). Force SpawnCap repeatable, then regularize if it's a boss.
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

        // Per-floor enemy-count target globals read by CMonstorUnit::ArrangementPos (the placement loop
        // runs this many times). They're set by the floor-select/dungeon-entry code, so write them at/after
        // floor selection; ArrangementPos still caps the actual count at the floor's walkable spawn tiles.
        // PCSX2 addresses (native 0x01D564xx); 0x6494/0x649C/0x64A0 are the ones used as the count arg.
        private const long PopCount6494 = 0x21D56494;
        private const long PopCount649C = 0x21D5649C;
        private const long PopCount64A0 = 0x21D564A0;
        // De-sentinels a boss-class record's AttackPower (65535 -> 100) so it isn't treated as a melee
        // one-shot. (The boss-spawn origin-displacement is NOT driven by the attack-block fields — tested.)
        private static void RegularizeBossRecord(int tableIndex)
        {
            long rec = EnemySpeciesTable.RecordAddress(tableIndex);
            if (Memory.ReadUShort(rec + EnemySpeciesTable.AttackPower) != 65535) return;
            // Memory.WriteUShort(rec + EnemySpeciesTable.AttackPower, 100);
            Memory.WriteUShort(rec + EnemySpeciesTable.MaxHp, 50);
            // Console.WriteLine($"[EnemyInjector] de-sentineled AttackPower (65535->100) for TableIndex {tableIndex}.");
        }

        internal static void SetPopulationTarget(int pop)
        {
            Memory.WriteInt(PopCount6494, pop);
            Memory.WriteInt(PopCount649C, pop);
            Memory.WriteInt(PopCount64A0, pop);
            Console.WriteLine($"[EnemyInjector] population target (0x21D56494/9C/A0) <- {pop} "
                + "(capped by walkable tiles; re-set after floor-select if it gets overwritten).");
        }

        // ── Snapshot / restore so leaving the dungeon reverts to vanilla spawns ──────────────
        // SetSpawnRoster*/SetIceQueenFloorExact overwrite the static BtEnemyLayout (per-dungeon, normal+Ura,
        // all floors) and a few species records IN PLACE. Nothing reloads them, so without this they persist
        // for the whole session. We snapshot the original bytes on the FIRST edit (per dungeon / per record)
        // and write them back on dungeon exit. Dungeon.cs calls RestoreSpawnRoster() there, next to
        // EnemySlots.RestoreRedirectedEnemies(). Mirrors the RedirectEnemyModel snapshot pattern.
        private static int _rosterSnapDungeon = -1;
        private static byte[] _rosterSnapNormal, _rosterSnapUra;
        private static readonly System.Collections.Generic.Dictionary<int, byte[]> _speciesRecordSnaps = new();
        // Floor-change reset state (see NotifyInFloor): _wasInFloor tracks the on-floor edge; _rosterAppliedToFloor
        // is set once the staged roster's floor has actually loaded, so we revert when LEAVING that floor (not
        // when leaving the floor it was set on, and not mid-load before placement runs).
        private static bool _wasInFloor = false;
        private static bool _rosterAppliedToFloor = false;

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
                // Left a floor. If the staged roster already had its floor, revert now → next floor / town vanilla.
                if (_rosterAppliedToFloor)
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
                Console.WriteLine($"[MimicChest] slot {s} mimic placed (CCharacter) at ({px:F1},{pz:F1},{py:F1}) — creating chest.");

                int boxIdx = FindFreeBoxSlot();
                int stateIdx = FindFreeStateSlot();
                if (boxIdx < 0 || stateIdx < 0)
                {
                    Console.WriteLine($"[MimicChest] no free box/state slot (box={boxIdx}, state={stateIdx}); enemy slot {s} skipped.");
                    _mimicChestDone.Add(s); // don't retry forever
                    continue;
                }

                // BOX-OBJECT entry (rendered chest): position vector + occupied + enemy link + type/open state.
                Memory.WriteByteArray(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.WorldX), pos); // -0x10: pos quadword
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ActiveFlag), 1);
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.EntityId),   s);     // link -> enemy slot (OPAnalyz wakes this)
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ConstOne),   1);
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.ChestSize),  1);     // 1 = small/regular chest model (0 = big); matches native mimics (SetMimicEvent s2=1)
                Memory.WriteInt(ChestAddresses.ChestSlots.SlotAddr(boxIdx, ChestSlotOffsets.OpenState),  0);

                // STATE/META entry (Type=8 mimic): position + proximity radius + box link.
                // Height/Z offset = 8 to match the ChestSize=1 (small) native path (SetMimicEvent s2==1).
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Type),    8);
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Zero),    0);
                Memory.WriteInt  (ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.BoxLink), boxIdx);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosX),    px);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosY),    pz);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.PosZ),    py + 8f);
                Memory.WriteFloat(ChestAddresses.ChestStateTable.SlotAddr(stateIdx, ChestStateOffsets.Height),  8f);

                // Extend the box loop count so CheckTreasureBox covers the new slot (DrawItemBox already scans all 24).
                int cnt = Memory.ReadInt(ChestAddresses.ChestSlots.BoxLoopCount);
                if (boxIdx + 1 > cnt) Memory.WriteInt(ChestAddresses.ChestSlots.BoxLoopCount, boxIdx + 1);

                _mimicChestDone.Add(s);
                made++;
                Console.WriteLine($"[MimicChest] enemy slot {s} -> chest (box {boxIdx}, state {stateIdx}) at ({px:F1},{pz:F1},{py:F1}); count {cnt}->{Math.Max(cnt, boxIdx + 1)}.");
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
            if (_rosterSnapDungeon < 0 && _speciesRecordSnaps.Count == 0) return;
            if (_rosterSnapDungeon >= 0)
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
            _mimicChestDone.Clear();
            BossScriptPatcher.ArmedBoss = -1;
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
                // only touches AttackPower). Multi-part bosses share one skeleton, so >1 instance glitches —
                // force them spawn-once regardless of the '!' flag.
                bool isBossClass = Memory.ReadByte(rec + EnemySpeciesTable.ModelCode) == (byte)'c';
                bool isOnce = (spawnOnce != null && i < spawnOnce.Length && spawnOnce[i]) || isBossClass;
                SnapshotSpeciesRecordIfNeeded(tableIndices[i]);
                Memory.WriteInt(rec + EnemySpeciesTable.SpawnCap, isOnce ? 2 : 0);
                // De-sentinel ONLY the boss (cNNx). Companions are sentinels by design (AttackPower 65535 = effect
                // entity, not a melee attacker); de-sentineling them breaks their attack behavior.
                if (isBossClass) RegularizeBossRecord(tableIndices[i]);
                // Arm the script patch for the first boss that has a known _INITIALIZE fix (Dran/Master Utan/
                // MinotaurJoe/c22a). Other 'c' bosses still spawn (spawn-once) but may be displaced — WIP.
                if (armedBoss < 0 && BossScriptPatcher.IsPatchable(tableIndices[i])) armedBoss = tableIndices[i];
            }
            BossScriptPatcher.ArmedBoss = armedBoss;
            if (armedBoss == 80) BossScriptPatcher.TagCompanions();   // name Ice Queen's eid-0 companions in logs
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
                (1, 80, 40), (1, 101, 10), (1, 76, 10), (1, 102, 10), (1, 103, 10), (1, 92, 10), (1, 104, 10)
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
            if (armed == 80) BossScriptPatcher.TagCompanions();   // name Ice Queen's eid-0 companions in logs
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
            int carrier = Enemies.Dasher.TableIndex.Value;   // regular DBC enemy, not boss-gated
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
            int carrier = Enemies.Dasher.TableIndex.Value;
            byte[] bossCode = Memory.ReadByteArray(
                EnemySpeciesTable.RecordAddress(bossTableIndex) + EnemySpeciesTable.ModelCode, 4);
            Memory.WriteByteArray(
                EnemySpeciesTable.RecordAddress(carrier) + EnemySpeciesTable.ModelCodeCopy, bossCode);
            string code = System.Text.Encoding.ASCII.GetString(bossCode);
            Console.WriteLine($"[EnemyInjector] carrier {carrier}.ModelCodeCopy <- \"{code}\" (boss AI dispatch). "
                + "Existing spawns may not re-dispatch; if not, it binds on the next spawn/floor.");
        }

        /// <summary>Returns the cave code relocated to <see cref="CaveBase"/>.</summary>
        internal static byte[] BuildCave()
        {
            byte[] code = FromHex(CaveTemplateHex);
            uint native = CaveBase & 0x1FFFFFFF;      // EE physical address the cave loads internally
            uint paramAddr = native;
            uint posAddr   = native + P_POS;

            // Sanity-check we are patching the right instructions (catches a wrong offset / stale template).
            ExpectImm(code, OFF_PARAM_HI, (ushort)(TEMPLATE_BASE >> 16), "lui $v0 (PARAM hi)");
            ExpectImm(code, OFF_PARAM_LO, (ushort)(TEMPLATE_BASE & 0xFFFF), "ori $v0 (PARAM lo)");
            ExpectImm(code, OFF_POS_HI,   (ushort)((TEMPLATE_BASE + P_POS) >> 16), "lui $a2 (pos hi)");
            ExpectImm(code, OFF_POS_LO,   (ushort)((TEMPLATE_BASE + P_POS) & 0xFFFF), "ori $a2 (pos lo)");

            PatchImm(code, OFF_PARAM_HI, (ushort)(paramAddr >> 16));
            PatchImm(code, OFF_PARAM_LO, (ushort)(paramAddr & 0xFFFF));
            PatchImm(code, OFF_POS_HI,   (ushort)(posAddr >> 16));
            PatchImm(code, OFF_POS_LO,   (ushort)(posAddr & 0xFFFF));
            return code;
        }

        /// <summary>
        /// Restores the original instruction at the hook site, removing the detour. Call when disabling
        /// mid-dungeon (only meaningful while the dun overlay is loaded). Safe to call repeatedly.
        /// </summary>
        internal static void Uninstall()
        {
            Memory.WriteInt(HookAddr, unchecked((int)OriginalHookWord));
        }

        /// <summary>The 4-byte `j cave` detour written at <see cref="HookAddr"/> (delay slot = original next instr).</summary>
        internal static byte[] BuildHook()
        {
            uint codeStart = (CaveBase & 0x1FFFFFFF) + 0x20;
            uint jWord = (0x02u << 26) | ((codeStart >> 2) & 0x03FFFFFF);
            return BitConverter.GetBytes(jWord);
        }

        // ── High-level operations ────────────────────────────────────────────
        /// <summary>
        /// PRELOAD: load <paramref name="tableIndex"/>'s mesh + species record into model block
        /// <paramref name="blockIndex"/>. Blocking disc I/O — call at a safe moment (e.g. floor entry).
        /// </summary>
        internal static void PreloadSpecies(int blockIndex, int tableIndex)
        {
            Memory.WriteInt(CaveBase + P_N, blockIndex);
            Memory.WriteInt(CaveBase + P_T, tableIndex);
            Memory.WriteInt(CaveBase + P_MODE, MODE_PRELOAD);
            Memory.WriteInt(CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// INSTANTIATE: free slot <paramref name="slot"/> and build the live monster from model
        /// block <paramref name="slot"/> at the given position. No disc I/O. Preload the same index first.
        /// Position floats are written in the engine's slot Location order (X, Z, Y).
        /// </summary>
        internal static void SpawnIntoSlot(int slot, float x, float z, float y)
        {
            Memory.WriteInt  (CaveBase + P_N, slot);
            Memory.WriteFloat(CaveBase + P_POS + 0, x);
            Memory.WriteFloat(CaveBase + P_POS + 4, z);
            Memory.WriteFloat(CaveBase + P_POS + 8, y);
            Memory.WriteInt  (CaveBase + P_MODE, MODE_INSTANTIATE);
            Memory.WriteInt  (CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// INSTANTIATE using the slot's CURRENT world position (copies the 12-byte Location triple
        /// verbatim, sidestepping any float-order ambiguity). Use after the slot already held a live enemy.
        /// </summary>
        internal static void SpawnIntoSlotAtCurrentPos(int slot)
        {
            long locAddr = EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.LocationX);
            byte[] pos = Memory.ReadByteArray(locAddr, 12); // X(0x100), Z(0x104), Y(0x108)
            Memory.WriteInt(CaveBase + P_N, slot);
            Memory.WriteByteArray(CaveBase + P_POS, pos);
            Memory.WriteInt(CaveBase + P_MODE, MODE_INSTANTIATE);
            Memory.WriteInt(CaveBase + P_TRIGGER, 1);   // trigger LAST
        }

        /// <summary>
        /// Convenience: full re-skin of an already-spawned slot. Preloads, waits for the load to
        /// complete, then instantiates at the slot's current position. Returns false on timeout.
        /// </summary>
        internal static bool ReskinSlot(int slot, int tableIndex, int loadTimeoutMs = 2000)
        {
            PreloadSpecies(slot, tableIndex);
            if (!WaitTriggerClear(loadTimeoutMs)) return false;
            SpawnIntoSlotAtCurrentPos(slot);
            return WaitTriggerClear(loadTimeoutMs);
        }

        /// <summary>Polls until the cave clears the trigger (i.e. it fired) or the timeout elapses.</summary>
        internal static bool WaitTriggerClear(int timeoutMs)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (Memory.ReadInt(CaveBase + P_TRIGGER) == 0) return true;
                Thread.Sleep(8);
            }
            return false;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static void PatchImm(byte[] code, int off, ushort imm)
        {
            code[off]     = (byte)(imm & 0xFF);        // little-endian low 16 bits of the instruction word
            code[off + 1] = (byte)((imm >> 8) & 0xFF);
        }

        private static void ExpectImm(byte[] code, int off, ushort imm, string what)
        {
            ushort actual = (ushort)(code[off] | (code[off + 1] << 8));
            if (actual != imm)
                throw new InvalidOperationException(
                    $"EnemyModelInjector cave template mismatch at 0x{off:X} ({what}): " +
                    $"expected 0x{imm:X4}, found 0x{actual:X4}. Regenerate via tools/build_cave.py.");
        }

        private static byte[] FromHex(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            return b;
        }
    }

    /// <summary>
    /// Runtime patch that lets a boss spawn correctly on a normal floor. A boss's behavior script
    /// (dun\monstor\&lt;code&gt;.stb) runs program label-1 at spawn (CMonstorUnit::SetupViewMonstor), and for the
    /// bosses below that program calls cmd 0x24 (_INITIALIZE 0,0,0) — an arena origin-reset that, off the boss
    /// arena, displaces and locks the boss. We NOP just that one command (5 vmcode_t records = 60 bytes at a
    /// boss-specific STB file offset) in the loaded copy of the STB, keeping parts/animation/attacks intact.
    /// (Multi-part bosses share one skeleton, so only ONE instance animates — the roster code forces boss
    /// species spawn-once.)
    ///
    /// The STB reloads each floor entry, so while a patchable boss is in the roster we re-find and re-NOP it
    /// every dungeon-thread tick, catching the loaded STB before ArrangementPos spawns the boss. The boss is
    /// loaded first (roster reorder), so its STB address depends only on (dungeon, floor-half); known addresses
    /// are probed directly (instant), with a windowed scan-by-signature fallback that logs new ones. Touches
    /// only loaded EE RAM — the disc/ISO is never modified. See enemy-spawn-system.md §5c.
    /// </summary>
    internal static class BossScriptPatcher
    {
        // TableIndex of a patch-capable boss currently in the roster (-1 = none). Set by the roster code.
        internal static int ArmedBoss = -1;

        // Per-boss STB layout: TableIndex -> (label-1 codeOffset [signature @ STB+0x54], _SET_POSITION call
        // file offset [spawn fix — NOP], _RUN_SCRIPT cmdId-value file offset in label-120 [cutscene fix —
        // change 0x6F->0x68 = _STATUS_SET_DEAD]). Offsets extracted from each dun\monstor\<code>.stb.
        //   - label-1 cmd 0x24 _SET_POSITION(0,0,0): the arena origin-reset that displaces a normal-floor spawn.
        //   - label-120 (death) cmd 0x6F _RUN_SCRIPT: launches the boss-defeat event/cutscene. Regulars call
        //     0x68 _STATUS_SET_DEAD here instead, so retargeting it makes the boss die cleanly (no cutscene).
        // c22a (166) has no _RUN_SCRIPT in label-120 (different death mechanism) -> runScriptOff 0, not yet fixed.
        private static (uint codeOff, int initOff, int runScriptOff) BossInfo(int tableIndex) => tableIndex switch
        {
            78  => (0x3AC8u, 0x419C, 0x3A3C),  // Dran        (c12a)
            79  => (0x4484u, 0x4F48, 0x438C),  // Master Utan (c14a)
            80  => (0x2914u, 0x2B44, 0x288C),  // Ice Queen   (c13a) — codeOff@0x54; label-1 _SET_POSITION; label-120 _RUN_SCRIPT
            81  => (0x778u,  0x8BC,  0x70C),   // King's Curse(c15a) — label-1 SET_POS origin-reset @0x8BC; label-120 _RUN_SCRIPT(510) cutscene cmdId(0x6F) @0x70C (op1 push1, not op3)
            83  => (0x575Cu, 0x631C, 0x5700),  // MinotaurJoe (c16a)
            166 => (0x60C0u, 0x6C20, 0),       // c22a (no label-120 _RUN_SCRIPT; cutscene fix TBD)
            _   => (0u, 0, 0),
        };
        internal static bool IsPatchable(int tableIndex) => BossInfo(tableIndex).codeOff != 0;

        // STB load-address window for the remaining RAM scans (Ice Queen companion locate + command-pattern
        // scans). Boss STB location no longer scans — it reads CRunScript+0x3C (see LocateStbByCodeOff).
        private const long ScanLo = 0x01000000, ScanHi = 0x01A00000;
        private const int  ChunkWords = 8192;           // 32 KB per batched round-trip
        private const uint StbMagic = 0x00425453u;      // "STB\0"
        private const int  InitCmdLen = 60;             // _INITIALIZE call = 5 vmcode_t records (all 4 bosses)

        private static long _stbBase = -1;              // the located boss STB base (set by Tick, used by EnsureNopped/death handling)

        // ════════════════════════════════════════════════════════════════════════════════════════════
        // BOSS DEATH = cancel → (roar) → slow-motion collapse → hold → fade out → remove.  No cutscene.
        // ════════════════════════════════════════════════════════════════════════════════════════════
        // Bosses don't enter the engine's normal dying-state (they trigger the defeat cutscene instead) and
        // their AI (STB label-100) keeps running after HP<=0. So on death we drive the whole sequence from C#
        // by CLOBBERING label-100's bytecode in EE RAM (data only — never recompiled code) and writing a few
        // per-slot fields. Step runs whatever is at label-100, so it runs our sequence. CollapseDrive() (called
        // each tick from EnsureNopped) is the state machine:
        //   1. CANCEL the current motion: clobber label-100 -> _SET_MOTION(deathMotion) [DeathSeq], which queues
        //      the motion in slot+0xEC, then write slot+0xF4=1 (MotionCommitFlag) so the engine commits it NOW
        //      instead of after the current clip finishes. (RE'd from CMonstorUnit::Step commit @ELF 0x1dd890.)
        //   2. CAPTURE: once the engine starts the motion (frame enters its range), re-clobber label-100 to a
        //      no-op (HoldSeq) to FREEZE engine playback, and advance the motion frame ourselves at PlaybackFps
        //      (the motion-table KEY "speed" is NOT the frame-advance rate, so this is the only reliable slow-mo).
        //   3. ROAR -> COLLAPSE: if EnableRoar, phase 1 plays the roar motion, then phase 2 plays the collapse.
        //   4. HOLD + FADE: pin the last collapse frame, ramp Opacity (slot+0x120) 128->0 over FadeMs, then
        //      remove (RenderStatus=-1, decrement live count). label-100 is restored when no boss is dead.
        // Per-boss motion indices + frame ranges come from the model's info.cfg KEY list (decoded from <code>.chr;
        // see the datadat-index-and-chr-motions memory). c16a/MinotaurJoe: motion 9 = 死亡 "death" (300-330),
        // motion 4 = 雄たけび "roar" (210-260).
        //
        // WHY THE CLOBBER (simpler label-120 rewrites were tried on Joe and DON'T work): the cutscene-type bosses'
        // collapse is locked INSIDE the defeat cutscene (label-120 calls _RUN_SCRIPT(510)); suppressing the cutscene
        // loses the animation, and Joe's AI (label-100) keeps running regardless, overriding any motion set from a
        // one-shot label-120. We tried rewriting Joe's label-120 to two self-contained death patterns:
        //   • the REGULAR-enemy pattern (… _STATUS_SET_DEAD) — AI kept running, no clean death.
        //   • the c22a/Black Knight Mount pattern (… _DEL_REFERENCE(1), 0xFB) — same: AI continues as if not dead.
        // Neither stops label-100, so only the per-frame label-100 clobber below makes the collapse stick on Joe-type
        // bosses. (c22a works WITHOUT a clobber because its engine death-state stops its AI; Joe's cutscene path
        // doesn't engage that state. See EngineDeathCleanup for the c22a/166 case.)

        // ── Per-boss motion data (TableIndex -> motion index / frame range; -1/0 = none) ──────────────
        // 78 = Dran (c12a): death = motion 6 (飛行=0 … 死亡=6, frames 100–120), no roar. NOTE its info.cfg has a
        //      "KEY start key, end key, step" header line; if that counts as table index 0 the death is 7, not 6 —
        //      verify in-game and bump to 7 if the wrong clip plays. (Dran is a FLYER — see the flying-AI note below.)
        // 79 = Master Utan (c14a): death is the 14th KEY entry; its info.cfg labels it "14" but the labels skip
        //      11, so the sequential table index is 13 (frames 360–385). No roar, no death-loop. If the collapse
        //      plays the wrong clip in-game, try 14 (i.e. the .chr loader honoured the printed label, not order).
        private static int CollapseMotion(int tableIndex)     => tableIndex switch { 78 => 6,   79 => 13,  80 => 11,  81 => 5,   83 => 9,   _ => -1 };
        // STB-relative start offset of label-120 (the "defeated" script) for bosses whose ENTIRE label-120 must be NOPed
        // (its early commands trigger the engine dungeon-end). -1 = use the per-_RUN_SCRIPT rewrite instead. KC c15a: 0x624.
        private static int Label120Start(int tableIndex)      => tableIndex switch { 81 => 0x624, _ => -1 };
        private static int CollapseStartFrame(int tableIndex) => tableIndex switch { 78 => 100, 79 => 360, 80 => 165, 81 => 90,  83 => 300, _ => 0  };
        private static int CollapseEndFrame(int tableIndex)   => tableIndex switch { 78 => 120, 79 => 385, 80 => 185, 81 => 110, 83 => 330, _ => 0  };
        private static int RoarMotion(int tableIndex)         => EnableRoar ? (tableIndex switch { 83 => 4, _ => -1 }) : -1;
        private static int RoarStartFrame(int tableIndex)     => tableIndex switch { 83 => 210, _ => 0  };
        private static int RoarEndFrame(int tableIndex)       => tableIndex switch { 83 => 260, _ => 0  };

        // ── Tunables ──────────────────────────────────────────────────────────────────────────────────
        public  const bool  EnableRoar       = false;     // play the boss "roar" before the collapse (false = collapse only)
        private const float PlaybackFps      = 30f;      // C#-driven death-animation rate (lower = slower motion)
        private const int   FadeMs           = 1600;     // hold the down pose + fade out this long after the collapse, then remove
        private const int   CollapseTimeoutMs = 20000;   // fallback: remove this long after death even if the frame never reaches the end

        // ── Runtime state ───────────────────────────────────────────────────────────────────────────────
        private static int        _deathPhase = 0;       // 0=none/alive, 1=roar, 2=collapse
        private static bool       _captured   = false;   // true once engine playback is frozen and we're driving the frame
        private static float      _captureFrame = 0f;    // animation frame at capture (C#-advance starts here)
        private static System.DateTime _captureTime;     // when C#-advance for the current phase began
        private static byte[]     _origLabel100;         // saved original AI bytecode (restored when no boss is dead)
        private static readonly System.Collections.Generic.Dictionary<int, System.DateTime> _collapseStart = new(); // per-slot: hold/fade start
        private static readonly System.Collections.Generic.Dictionary<int, System.DateTime> _deadSince     = new(); // per-slot: death detected (timeout)
        private static readonly System.Collections.Generic.Dictionary<int, float[]>          _palStart      = new(); // per-slot: opacity at fade start

        // ── EE memory layout ──────────────────────────────────────────────────────────────────────────
        private const long MainMonstorUnit  = EnemyAddresses.MainMonstorUnit.Base;          // = -0x6320($gp)
        private const long MotionBlock      = ModelScaleOffsets.ModelStride;                // 0x3510 render-object stride
        private const long MotionFrameField = 0x1FFC0;     // PLAYING motion frame (float) @ unit + idx*0x3510 + 0x1FFC0 (= ModelScaleOffsets.PlayingMotionFrame)


        internal static void Tick()
        {
            int ti = ArmedBoss;
            if (ti < 0) { _stbBase = -1; return; }
            var (codeOff, initOff, _) = BossInfo(ti);
            if (codeOff == 0) { _stbBase = -1; return; }
            try
            {
                // Track the floor's map_no / MAPPARTS BEFORE Ice Queen's arena overlay sets map_no=800 and nulls
                // the tile grid. Capture the grid the moment it's populated (early ticks) so we have the floor's
                // walkable tiles even after she activates.
                int mapNow = Memory.ReadInt(DungeonAddresses.Map.MapNo);
                uint mpNow = Memory.ReadUInt(DungeonAddresses.Map.MapPartsPtr);
                if (mapNow != _lastMapNo) { Console.WriteLine($"[MAP] map_no {_lastMapNo}->{mapNow}  mapparts=0x{mpNow:X8}"); _lastMapNo = mapNow; _mapDumped = false; _anchorSet = false; _kcPhaseLogged = false; _kcSwapDone = false; }   // re-arm per floor
                // The tile grid is EMBEDDED in the CDungeonMap at +0x9C50 (ArrangementPos reads it to place enemies);
                // it's valid at floor-entry, independent of the standalone `mapparts` global (which is NULL in her arena).
                // Dump it as soon as the floor's CDungeonMap exists — i.e. BEFORE we ever swap Ice Queen in.
                if (DungeonAddresses.Map.Deref(Memory.ReadUInt(DungeonAddresses.Map.NowDngMapPtr)) != 0) DumpMapParts();

                // Ice Queen. Two strategies:
                //  TrySlotSwap: physically reorder all 7 entities to their NATIVE slots (0-6) so the real companion
                //    scripts run unmodified (no remap, no stand-ins) — gives the real arrow flight + reiki damage.
                //  else: leave them scattered and drive the fight via the _GET_MONSTOR remap + global stand-ins.
                if (ti == 80)
                {
                    if (TrySlotSwap)
                    {
                        ReorderToNativeOnce();                                 // correct slots -> the real companion scripts run
                        if (IqNudgeX != 0f || IqNudgeY != 0f)
                        {
                            NudgeCompanionPositions();                          // move companions the SAME offset as Ice Queen (preserve layout)
                            WakeShiftFloorSlots();                              // pre-shift the dormant floor-slot (the engine's wake-origin) to the cluster
                            PatchBariaShield();                                 // zero baria's orbit radius so the shield lands on her, not ~+2000
                            RestoreShieldNativeEid();                           // (toggle) restore baria's slot eid to native 0
                        }
                        else if (IqTranslate)                                  // off for the Moon Sea native test (she stays at her -Y corner)
                        {
                            if (TranslateCompanions) PatchCompanionPositions();
                            PatchKorinoyaArrow();
                        }
                    }
                    else { if (TranslateCompanions) PatchCompanionPositions(); PatchShieldTarget(); KorinoyaStandIn(); }
                }
                if (ti == 81) ReorderKingsCurseOnce();   // coffin(115)->slot0, king(100)->slot1 once both are live
                // Locate the boss's live STB directly via its slot's CRunScript pointer (CRunScript+0x3C) — any
                // boss in any dungeon. No KnownAddrs table, no RAM scan.
                long stb = LocateStbByCodeOff(codeOff);
                if (stb >= 0)
                {
                    if (stb != _stbBase) Console.WriteLine($"[BossPatch] boss {ti} STB @ 0x{stb:X8} (via CRunScript+0x3C).");
                    _stbBase = stb;
                    EnsureNopped(stb, initOff, ti);
                }
            }
            catch { /* transient PINE read; retry next tick */ }
        }

        // Validate a located boss STB: "STB\0" magic at +0x00 and the label-1 codeOffset at +0x54.
        private static bool IsBossStb(long b, uint codeOff)
            => Memory.ReadUInt(b) == StbMagic && (uint)Memory.ReadInt(b + 0x54) == codeOff;

        // Ice Queen's attacks are separate companion entities (kori/i_meteo/i_tatumaki/baria) coordinated via global
        // ints. The fight's coordination depends on everyone starting in the native arena layout (NOP-ing the label-1
        // _SET_POSITION calls breaks it). But that layout is an off-map corner near the origin, so she's unreachable.
        // Fix: TRANSLATE the whole fight onto a walkable chest tile — rewrite every entity's _SET_POSITION by ONE
        // shared delta = (chestX, chestY - IqNativeY). The engine spawns them there (no loc-write), Ice Queen lands on
        // the chest (reachable), and the relative layout (hence the coordination) is preserved. All natives have X=0;
        // only Y differs. baria self-positions (follows Ice Queen via the slot lookup), so it needs no translate.
        // _SET_POSITION only takes effect at spawn, so writing it every tick is harmless after spawn and needs no
        // floor-tracking — native+delta is idempotent. (kori & i_meteo share codeOff 0x5EC; disambiguate by which
        // candidate offset actually holds the _SET_POSITION push.)
        private const float IqNativeX = 0f, IqNativeY = 177f;
        // She lands exactly on the chest tile and gets stuck inside the chest object, so shift the whole fight off it
        // (world +Y). Tunable — large boss body needs real clearance; bump/flip if she lands in a wall.
        private const float ChestClearOffsetY = 10f;
        // false = leave companions at native positions (they move/attack there); true = translate them with the boss.
        private const bool TranslateCompanions = true;
        private static bool _iqXlateLogged = false;
        // Offset test: nudge Ice Queen from her native position toward the player (worldX +X, worldY +Y) to find how
        // far she can move before the companion repositioning breaks. Both 0 = fall back to full chest translate.
        // These start as a fallback guess but are OVERWRITTEN at floor-entry by ComputeWalkableAnchor() once the
        // MAPPARTS grid is populated: we find the largest open room and set the nudge to drop her cluster at its
        // world center (cell = 160 world units; worldX = col*160, worldY = row*160 — calibrated against live player/IQ).
        private static float IqNudgeX = 360f;
        private static float IqNudgeY = 360f;   // worldY moves from native -177 toward the player's +Y side
        private static float _iqNatX = float.NaN, _iqNatZ;   // captured native _SET_POSITION worldX / z-arg
        private static float ReadCoordArg(long opAddr, long valAddr)
        {
            int op = Memory.ReadInt(opAddr), v = Memory.ReadInt(valAddr);
            return op == 2 ? BitConverter.Int32BitsToSingle(v) : (float)v;   // op2=float operand, else int
        }
        // Each companion's REAL spawn _SET_POSITION (the worldY=-380 / worldY=0 anchor that sets its idle/char position),
        // found via the [CompSpawn] dump. Nudging these (NOT the effect/emitter blocks the old list targeted) actually
        // relocates the companion's body with Ice Queen. Shared codeOffs (0x5EC kori+i_meteo, 0x3AC i_tatumaki+reiki)
        // are disambiguated per-STB by IsSetPosPush — the wrong offset isn't a _SET_POSITION in that STB, so it's skipped.
        private static readonly (uint codeOff, int initOff, float nativeY)[] _iqCompanionPos =
        {
            // korinoya (0x7D0) — all worldY=-380 position anchors
            (0x7D0u, 0x724, IqNativeY), (0x7D0u, 0x928, IqNativeY), (0x7D0u, 0x1898, IqNativeY), (0x7D0u, 0x1988, IqNativeY),
            // kori + i_meteo (0x5EC) — worldY=-380 anchors (per-STB IsSetPosPush picks the valid offsets)
            (0x5ECu, 0x66C, IqNativeY), (0x5ECu, 0xB3C, IqNativeY), (0x5ECu, 0x714, IqNativeY), (0x5ECu, 0xCFC, IqNativeY),
            // i_tatumaki + reiki (0x3AC) — worldY=0 anchors
            (0x3ACu, 0x4E0, IqNativeY), (0x3ACu, 0x7C8, IqNativeY), (0x3ACu, 0x4BC, IqNativeY), (0x3ACu, 0xD9C, IqNativeY),
            // baria (0x874) — worldY=-380 anchor
            (0x874u, 0x79C, IqNativeY),
        };
        private static readonly long[] _compAddr = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        // Per-companion captured native _SET_POSITION coords (worldX / z-arg), for the same-offset nudge as Ice Queen.
        private static readonly float[] _compNatX = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
        private static readonly float[] _compNatZ = new float[13];

        private static bool IsSetPosPush(long a)   // 5-record _SET_POSITION block starts with push(op3,operand1,0x24)
            => Memory.ReadInt(a) == 3 && Memory.ReadInt(a + 4) == 1 && Memory.ReadInt(a + 8) == 0x24;

        // First active chest with a sane coord = a guaranteed-walkable target (chests are placed before enemies, so
        // the table is populated by the time we patch the STBs pre-spawn).
        private static bool TryGetChestTarget(out float cx, out float cy)
        {
            cx = cy = 0f;
            int cnt = Memory.ReadInt(ChestAddresses.ChestSlots.CountAddr);
            if (cnt < 1 || cnt > 16) return false;
            for (int c = 0; c < cnt; c++)
            {
                if (Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.ActiveFlag)) != 1) continue;
                float wx = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldX));
                float wy = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldY));
                if (wx > 10f && wx < 5000f && wy > 10f && wy < 5000f) { cx = wx; cy = wy; return true; }
            }
            return false;
        }

        // Rewrite a _SET_POSITION block's X (arg1) and Y (arg3) to float literals; leave arg2 (height) untouched.
        // arg1: operand@+16, value@+20.  arg3: operand@+40, value@+44.  (operand 2 = float literal.)
        // WRITE ONCE: skip if the block already holds the target (operand=float + matching value). The native STB
        // has operand!=2, so the first (pre-spawn) tick writes; afterwards we no-op until the floor reloads native.
        // Rewriting the block every tick while the companion's AI runs freezes its attack (idle/"stuck in position").
        private static void WritePosArgs(long block, float newX, float newY)
        {
            if (Memory.ReadInt(block + 16) == 2 && Memory.ReadInt(block + 40) == 2
                && Math.Abs(BitConverter.Int32BitsToSingle(Memory.ReadInt(block + 20)) - newX) < 0.5f
                && Math.Abs(BitConverter.Int32BitsToSingle(Memory.ReadInt(block + 44)) - newY) < 0.5f)
                return;   // already translated this floor — don't touch the running interpreter
            Memory.WriteInt(block + 16, 2);
            Memory.WriteInt(block + 20, BitConverter.SingleToInt32Bits(newX));
            Memory.WriteInt(block + 40, 2);
            Memory.WriteInt(block + 44, BitConverter.SingleToInt32Bits(newY));
        }

        // Like WritePosArgs but for STBs whose _SET_POSITION uses op1 push1 INT literals (c15a/c15b King's Curse) instead
        // of op3 type/value pushes. The x value is the op1 operand @block+16 (rec1+4), z @block+40 (rec3+4). Keep op1;
        // just overwrite the ints. Write-once (skip if already at target) so we don't re-touch the running interpreter.
        private static void WritePosArgsInt(long block, int newX, int newZ)
        {
            if (Memory.ReadInt(block + 16) == newX && Memory.ReadInt(block + 40) == newZ) return;
            Memory.WriteInt(block + 16, newX);
            Memory.WriteInt(block + 40, newZ);
        }

        // King's Curse (c15a, table 81) + its phase entity (c15b, table 82) both spawn at a near-origin arena anchor.
        // Relocate the WHOLE pair to the largest room by the SAME world offset (preserve their relative layout), exactly
        // like the Ice Queen cluster. Each native _SET_POSITION is op1 ints (x@+16, z@+40; worldY = -z). We anchor 81 at
        // the room center (its native worldY = -500 => nudgeY = anchorY + 500) and shift 82 by that same nudge.
        private static void RelocateKingsCurseCoffinCluster(long stb81, long block81)
        {
            float nudgeX = _anchorX, nudgeY = _anchorY + 500f;
            WritePosArgsInt(block81, (int)(0 + nudgeX), (int)(500 - nudgeY));     // c15a native (0,0,500) -> room center
            int cx = (int)(0 + nudgeX), cz = (int)(237 - nudgeY);                // c15b room coords (native 0,0,237)
            // c15b (codeOff 0x2fdc, _SET_POSITION @ +0x3678): patch the LIVE instance, located via its slot's
            // CRunScript STB pointer (LiveKcPhaseStb). This runs every tick, so it catches c15b as soon as it's in
            // a slot and rewrites its _SET_POSITION to the room.
            long live = LiveKcPhaseStb();
            if (live >= 0)
            {
                WritePosArgsInt(live + 0x3678, cx, cz);
                if (!_kcPhaseLogged) { Console.WriteLine($"[KCxlate] c15b live STB 0x{live:X8} (coffin81 @0x{stb81:X8}) -> room +({nudgeX:F0},{nudgeY:F0})"); _kcPhaseLogged = true; }
            }
        }
        private static bool _kcPhaseLogged;

        // Resolve the LIVE c15b (King's Curse, eid 100) STB directly from its slot's CRunScript pointer
        // (CRunScript+0x3C = the STB base). Confirmed by codeOff(+0x54)==0x2fdc. -1 until live.
        private static long LiveKcPhaseStb()
        {
            int slot = FindSlotByEid(100);
            if (slot < 0) return -1;
            long stb = SlotStbBase(slot);
            return stb >= 0 && (uint)Memory.ReadInt(stb + 0x54) == 0x2FDCu ? stb : -1;
        }

        private static void PatchCompanionPositions()
        {
            if (!TryGetChestTarget(out float cx, out float cy)) return;   // no walkable target yet — leave native this tick
            // _SET_POSITION arg3 is WORLD Y *negated* (arg3 = +1577 → loc Y = -1577), while arg1 is world X directly.
            // The walkable area is all +Y, so to land world (cx, cy) we need argX = cx, argY = -cy. Baking the sign
            // into dy keeps the shared-delta layout (native_argY + dy) correct.
            float dx = cx - IqNativeX, dy = -cy - IqNativeY - ChestClearOffsetY;
            // Fast path: re-validate cached STBs (cheap); only full-scan if any entry is unresolved.
            bool needScan = false;
            for (int k = 0; k < _iqCompanionPos.Length; k++)
            {
                long a = _compAddr[k];
                if (a >= 0 && Memory.ReadUInt(a) == StbMagic && (uint)Memory.ReadInt(a + 0x54) == _iqCompanionPos[k].codeOff)
                    WritePosArgs(a + _iqCompanionPos[k].initOff, IqNativeX + dx, _iqCompanionPos[k].nativeY + dy);
                else { _compAddr[k] = -1; needScan = true; }
            }
            if (!needScan) return;
            for (long a = ScanLo; a < ScanHi; a += (long)ChunkWords * 4)
            {
                long end = Math.Min(a + (long)ChunkWords * 4, ScanHi);
                int n = (int)((end - a) / 4);
                if (n <= 0x16) continue;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 0x16 < n; i++)
                {
                    if (w[i] != StbMagic) continue;
                    uint co = w[i + 0x15];
                    long stb = a + (long)i * 4;
                    for (int k = 0; k < _iqCompanionPos.Length; k++)
                    {
                        if (_compAddr[k] >= 0 || _iqCompanionPos[k].codeOff != co) continue;
                        int io = _iqCompanionPos[k].initOff;
                        if (IsSetPosPush(stb + io))
                        {
                            _compAddr[k] = stb;
                            WritePosArgs(stb + io, IqNativeX + dx, _iqCompanionPos[k].nativeY + dy);
                            Console.WriteLine($"[IQxlate] companion #{k} STB 0x{stb:X8} -> ({IqNativeX + dx:F0},{_iqCompanionPos[k].nativeY + dy:F0})");
                        }
                    }
                }
            }
        }

        // Move each companion by the SAME offset as Ice Queen's nudge (IqNudgeX/IqNudgeY), relative to its OWN
        // native spawn — preserving the whole cluster's relative layout so the fight behaves as it does at native,
        // just shifted. Native coords captured per-companion once (constant). worldY = -zArg, so +IqNudgeY worldY
        // means zArg -= IqNudgeY.
        private static void NudgeOne(int k, long stb)
        {
            long block = stb + _iqCompanionPos[k].initOff;
            if (float.IsNaN(_compNatX[k]))
            {
                if (!IsSetPosPush(block)) return;
                _compNatX[k] = ReadCoordArg(block + 16, block + 20);
                _compNatZ[k] = ReadCoordArg(block + 40, block + 44);
            }
            WritePosArgs(block, _compNatX[k] + IqNudgeX, _compNatZ[k] - IqNudgeY);
        }

        // Resolve a slot's loaded STB base directly from its CRunScript record: +0x3C holds the native base of the
        // STB this slot's VM is executing (RE'd 2026-06-19; see EnemyAddresses.CRunScript). No scan, no codeOff
        // table — and it's inherently slot-specific, so it disambiguates shared-codeOff companions for free.
        // Returns the PCSX2 address, or -1 if the slot has no valid STB.
        private static long SlotStbBase(int slot)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return -1;
            long stb = ((long)stbNative & 0x1FFFFFFF) | 0x20000000;
            return Memory.ReadUInt(stb) == StbMagic ? stb : -1;
        }

        // Locate the loaded STB for an enemy/boss with the given label-1 codeOffset, by reading each live slot's
        // CRunScript STB pointer (SlotStbBase). Replaces the KnownAddrs table + signature scan — works for any
        // boss in any dungeon. Returns the PCSX2 STB base, or -1 if no present slot runs that script.
        private static long LocateStbByCodeOff(uint codeOff)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, 0) + EnemySlotOffsets.RenderStatus) < 0) continue;
                long stb = SlotStbBase(s);
                if (stb >= 0 && (uint)Memory.ReadInt(stb + 0x54) == codeOff) return stb;
            }
            return -1;
        }

        // One-shot per floor: dump the MAPPARTS grid (room/walkable layout) + raw sample cells, so we can locate a
        // large room and (with the corner-record conversion) place Ice Queen there instead of a fixed offset/chest.
        private static bool _mapDumped;
        private static int _lastMapNo = -1;

        private const int GridRows = 24, GridCols = 20;
        private const float CellWorld = 160f;   // SearchiDoPutArea: each MAPPARTS cell = 160 world units (calibrated)
        private static bool _anchorSet;          // once we've locked the walkable anchor from a populated grid
        private static float _anchorX, _anchorY;  // raw world center of the largest room (= the anchor cell), boss-agnostic
        private static int _anchorRoomW, _anchorRoomH;  // largest-room dimensions (cells); a wiped/arena grid yields a degenerate room

        private static void DumpMapParts()
        {
            if (_mapDumped) return;
            long nowDng = DungeonAddresses.Map.Deref(Memory.ReadUInt(DungeonAddresses.Map.NowDngMapPtr));
            if (nowDng == 0) return;
            var objCells = ReadObjectCells(nowDng);                 // chest/atra/trap grid cells (engine placement-exclusions)
            var (populated, tile) = LogMapGrid(nowDng, objCells);
            if (populated == 0) return;     // still loading — keep trying next tick (don't latch _mapDumped on a wiped read)
            _mapDumped = true;
            if (populated >= 16 && !_anchorSet) SetWalkAnchorFromGrid(tile, objCells);
        }

        // Render the MAPPARTS tile grid as an ASCII map (one [MAP] line per row), overlaying chest/atra/trap markers.
        // Returns the populated-tile count and the boolean tile grid (true = placed/walkable cell, f0 != 0 && f0 != -1).
        private static (int populated, bool[,] tile) LogMapGrid(long nowDng, System.Collections.Generic.Dictionary<int, char> objCells)
        {
            long gb = nowDng + 0x9C50;   // MAPPARTS is embedded in the CDungeonMap (ArrangementPos: a0 = CDungeonMap + 0x9C50)
            var tile = new bool[GridRows, GridCols];
            int populated = 0, wiped = 0, empty = 0;
            Console.WriteLine($"[MAP] mapno={Memory.ReadInt(DungeonAddresses.Map.MapNo)} CDungeonMap@0x{nowDng:X8} MAPPARTS@0x{gb:X8}  (' '=empty '-'=wiped '#'=tile  C=chest A=atra T=trap)");
            for (int row = 0; row < GridRows; row++)
            {
                var sb = new System.Text.StringBuilder();
                for (int col = 0; col < GridCols; col++)
                {
                    int f0 = Memory.ReadInt(DungeonAddresses.MapPartsGrid.CellAddr(gb, row, col));
                    char ch;
                    if (f0 == 0) { ch = ' '; empty++; }
                    else if (f0 == -1) { ch = '-'; wiped++; }
                    else { ch = '#'; populated++; tile[row, col] = true; }
                    if (objCells.TryGetValue(row * GridCols + col, out var m)) ch = m;   // overlay object marker
                    sb.Append(ch);
                }
                Console.WriteLine($"[MAP] r{row:D2} {sb}");
            }
            Console.WriteLine($"[MAP] cells: {populated} tiles, {empty} empty, {wiped} wiped(-1), {objCells.Count} objects — grid is {(populated > 0 ? "POPULATED (valid floor layout)" : wiped > 0 ? "WIPED (-1, too late)" : "EMPTY")}");
            return (populated, tile);
        }

        // Chest/atra/trap-circle cells the engine excludes from enemy placement (see DungeonAddresses.DungeonObjects).
        // Keyed by row*GridCols+col; value is the map marker char. We mirror these so Ice Queen's cluster never lands
        // on a chest. World pos -> cell: col = x/160, row = z/160.
        private static System.Collections.Generic.Dictionary<int, char> ReadObjectCells(long nowDng)
        {
            var cells = new System.Collections.Generic.Dictionary<int, char>();
            void Add(float x, float z, char c)
            {
                int col = (int)(x / CellWorld), row = (int)(z / CellWorld);
                if (row >= 0 && row < GridRows && col >= 0 && col < GridCols) cells[row * GridCols + col] = c;
            }
            // Chests
            int nChest = System.Math.Clamp(Memory.ReadInt(nowDng + DungeonAddresses.DungeonObjects.ChestCount), 0, DungeonAddresses.DungeonObjects.ChestMax);
            for (int i = 0; i < nChest; i++)
            {
                long b = nowDng + DungeonAddresses.DungeonObjects.ChestArray + i * DungeonAddresses.DungeonObjects.ChestStride;
                if (Memory.ReadInt(b + DungeonAddresses.DungeonObjects.ChestActive) == 0) continue;
                Add(Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.ChestPos), Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.ChestPos + 8), 'C');
            }
            // Atra
            int nAtra = System.Math.Clamp(Memory.ReadInt(nowDng + DungeonAddresses.DungeonObjects.AtraCount), 0, DungeonAddresses.DungeonObjects.AtraMax);
            for (int i = 0; i < nAtra; i++)
            {
                long b = nowDng + DungeonAddresses.DungeonObjects.AtraArray + i * DungeonAddresses.DungeonObjects.AtraStride;
                if (Memory.ReadInt(b + DungeonAddresses.DungeonObjects.AtraActive) == 0) continue;
                Add(Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.AtraPos), Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.AtraPos + 8), 'A');
            }
            // Trap circles
            for (int i = 0; i < DungeonAddresses.DungeonObjects.TrapMax; i++)
            {
                long b = nowDng + DungeonAddresses.DungeonObjects.TrapArray + i * DungeonAddresses.DungeonObjects.TrapStride;
                if (Memory.ReadInt(b + DungeonAddresses.DungeonObjects.TrapActive) == 0) continue;
                Add(Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.TrapPos), Memory.ReadFloat(b + DungeonAddresses.DungeonObjects.TrapPos + 8), 'T');
            }
            return cells;
        }

        // Find the largest open room (maximal all-tile rectangle) and aim Ice Queen's cluster at its world center.
        // worldX = col*160, worldY(floor) = row*160. Her native is (worldX 0, worldY -177), and the nudge is added:
        //   IqNudgeX = targetX - nativeX(≈0);  worldY = -177 + IqNudgeY  =>  IqNudgeY = targetY + 177.
        // Chest/atra/trap cells are excluded from the candidate tiles so the room (and her cluster) never covers one.
        private static void SetWalkAnchorFromGrid(bool[,] tile, System.Collections.Generic.Dictionary<int, char> objCells)
        {
            // Find the room from the FULL tile geometry — a chest/atra/trap inside a room does NOT split it (you just
            // don't stand ON the object). Excluding object cells first fragments a real room into slivers smaller than
            // a corridor, which made her spawn off-walkable. Then anchor at the object-free cell nearest the room center.
            var (top, left, h, w) = LargestRoomRect(tile);
            if (h < 2 || w < 2) { Console.WriteLine($"[ANCHOR] largest room only {w}x{h} — too small, keeping fallback nudge ({IqNudgeX:F0},{IqNudgeY:F0})"); return; }
            float fcr = top + (h - 1) / 2f, fcc = left + (w - 1) / 2f;   // geometric center of the room
            int bestR = -1, bestC = -1; double bestD = double.MaxValue;
            for (int r = top; r < top + h; r++)
                for (int c = left; c < left + w; c++)
                {
                    if (objCells.ContainsKey(r * GridCols + c)) continue;   // don't anchor ON a chest/atra/trap
                    double d = (r - fcr) * (r - fcr) + (c - fcc) * (c - fcc);
                    if (d < bestD) { bestD = d; bestR = r; bestC = c; }
                }
            if (bestR < 0) { Console.WriteLine($"[ANCHOR] room {w}x{h} is entirely objects — keeping fallback nudge"); return; }
            float targetX = (bestC + 0.5f) * CellWorld;
            float targetY = (bestR + 0.5f) * CellWorld;
            IqNudgeX = targetX;          // native worldX ≈ 0
            IqNudgeY = targetY + 177f;   // worldY = -177 + IqNudgeY = targetY
            _anchorX = targetX;          // raw room center (boss-agnostic; used by King's Curse cluster relocate)
            _anchorY = targetY;
            _anchorRoomW = w; _anchorRoomH = h;
            _anchorSet = true;
            Console.WriteLine($"[ANCHOR] largest room {w}x{h} @grid(r{top},c{left}); anchor cell (r{bestR},c{bestC}) -> world({targetX:F0},{targetY:F0}); nudge=({IqNudgeX:F0},{IqNudgeY:F0})");
        }

        // Largest all-true rectangle in the tile grid (histogram method). Returns (centerRow, centerCol, height, width).
        // Scored by the NARROW dimension first (min(w,h)) then area, so we pick the roomiest open space for the 7-member
        // cluster — not a 1-wide boundary-wall strip or a long thin corridor that would win on raw area.
        private static (int top, int left, int h, int w) LargestRoomRect(bool[,] tile)
        {
            int[] hist = new int[GridCols];
            int bestMin = 0, bestArea = 0, bTop = 0, bLeft = 0, bH = 0, bW = 0;
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++) hist[col] = tile[row, col] ? hist[col] + 1 : 0;
                for (int left = 0; left < GridCols; left++)
                {
                    if (hist[left] == 0) continue;
                    int minH = hist[left];
                    for (int right = left; right < GridCols; right++)
                    {
                        if (hist[right] == 0) break;
                        minH = System.Math.Min(minH, hist[right]);
                        int w = right - left + 1, area = w * minH, minDim = System.Math.Min(w, minH);
                        if (minDim > bestMin || (minDim == bestMin && area > bestArea))
                        { bestMin = minDim; bestArea = area; bH = minH; bW = w; bTop = row - minH + 1; bLeft = left; }
                    }
                }
            }
            return (bTop, bLeft, bH, bW);
        }

        // baria's native math is IQ.pos + facing×15 (GET_MONSTOR_POS(0)+GET_MONSTOR_VEC(0)), which sits the shield
        // just in front of her — correct now that the slot-swap puts her at slot 0 with a correctly-shifted position.
        // The +2000 fling was an artifact of the OLD pre-swap setup; zeroing the six 15.0 radius floats was a band-aid
        // that parks the shield AT her center (wrong) and may be what keeps it from working. Default OFF — let the
        // native formula run. Flip true only if the shield flings again.
        private static readonly bool ZeroBariaRadius = false;
        // baria to ~+2000. Zero those radius constants -> it lands AT her, stays hittable, and can break. Re-armed/floor.
        private static bool _bariaPatched;
        private static void PatchBariaShield()
        {
            if (!ZeroBariaRadius) return;   // let baria's native IQ.pos + facing×15 position the shield in front of her
            if (_bariaPatched) return;
            long stb = LocateStbByCodeOff(0x874);   // baria (CRunScript+0x3C; was KnownAddrs + scan)
            if (stb < 0) return;
            _bariaPatched = true;
            int n = 0;
            foreach (int o in new[] { 0xD68, 0xDC8, 0xE28, 0x1158, 0x11B8, 0x1218 })   // a2 (value) of each "push 15.0"
                if (Memory.ReadInt(stb + o) == 1097859072) { Memory.WriteInt(stb + o, 0); n++; }
            Console.WriteLine($"[BARIApatch] zeroed {n}/6 orbit-radius floats @0x{stb:X8} — shield should stay on Ice Queen");
        }

        // The engine's enemy-wake culling reads the floor-slot LocationX/Y, which sits at the arrange origin (~0,0)
        // while the enemy is dormant and only jumps to the shifted spot once its AI wakes. So pre-write the dormant
        // floor-slot to the shifted cluster position — then the wake fires when the player approaches HER, not origin.
        // Only touch slots still near origin (dormant); once active (already shifted) we leave them to the AI.
        private static void WakeShiftFloorSlots()
        {
            float wx = 0f + IqNudgeX, wy = -177f + IqNudgeY;   // Ice Queen's shifted worldX / worldY
            for (int s = 0; s <= 6; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) == -1) continue;
                float lx = BitConverter.Int32BitsToSingle(Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX)));
                if (Math.Abs(lx) >= 100f) continue;   // already active/shifted — don't fight the AI
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX), BitConverter.SingleToInt32Bits(wx));
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY), BitConverter.SingleToInt32Bits(wy));
            }
        }

        private static void NudgeCompanionPositions()
        {
            bool needScan = false;
            for (int k = 0; k < _iqCompanionPos.Length; k++)
            {
                long a = _compAddr[k];
                if (a >= 0 && Memory.ReadUInt(a) == StbMagic && (uint)Memory.ReadInt(a + 0x54) == _iqCompanionPos[k].codeOff)
                    NudgeOne(k, a);
                else { _compAddr[k] = -1; needScan = true; }
            }
            if (!needScan) return;
            for (long a = ScanLo; a < ScanHi; a += (long)ChunkWords * 4)
            {
                long end = Math.Min(a + (long)ChunkWords * 4, ScanHi);
                int n = (int)((end - a) / 4);
                if (n <= 0x16) continue;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 0x16 < n; i++)
                {
                    if (w[i] != StbMagic) continue;
                    uint co = w[i + 0x15];
                    long stb = a + (long)i * 4;
                    for (int k = 0; k < _iqCompanionPos.Length; k++)
                    {
                        if (_compAddr[k] >= 0 || _iqCompanionPos[k].codeOff != co) continue;
                        if (IsSetPosPush(stb + _iqCompanionPos[k].initOff))
                        {
                            _compAddr[k] = stb;
                            NudgeOne(k, stb);
                            Console.WriteLine($"[IQnudge] companion #{k} STB 0x{stb:X8} +({IqNudgeX:F0},{IqNudgeY:F0})");
                        }
                    }
                }
            }
        }

        // Ice Queen's shield (baria, c13_baria.stb) hardcodes the boss as slot 0: it calls _GET_MONSTOR_POS(0)
        // and _GET_MONSTOR_VECTOR(0) to wrap around her. The roster places her in a random slot, so the shield
        // looks up the wrong slot and lands away from her. We can't force her to slot 0 (engine-coded placement;
        // the cave needs a code hook that crashes PINE), so instead we PATCH the shield's STB to read her ACTUAL
        // slot. baria codeOff@0x54 = 0x874; the 4 slot-index value fields are at cmdId-push + 0x14.
        // Option C — full slot remap. EVERY companion (not just baria/korinoya) hardcodes the boss as native slot 0
        // via _GET_MONSTOR_POS(0)/_GET_MONSTOR_VECTOR(0); on a random floor she's elsewhere, so unpatched companions
        // position against the wrong slot and the handshake never advances. We scan for all companion STBs (codeOffs
        // are shared: i_tatumaki/reiki=0x3AC, kori/i_meteo=0x5EC, korinoya=0x7D0, baria=0x874), and grid-walk each to
        // re-point every _GET_MONSTOR ref at Ice Queen's real slot. (Global-int mailboxes are literal/slot-independent,
        // and _GET_POSITION positive args resolve to neither self nor a slot in the handler — so neither is remapped.)
        private static int _remappedForSlot = -1;   // IQ slot we last remapped for (re-armed when she despawns)
        private static int _waveCount;               // attack/shield wave alternation counter (KorinoyaStandIn)

        // ── EXPERIMENTAL: physical slot reorder ──────────────────────────────────────────────────────────
        // Monsters are direct-indexed off MainMonstorUnit (no entity pointer to swap), and nothing stores its own
        // slot index — the slot IS the array position. So to move an entity from slot A to slot B we physically
        // swap all four per-slot data blocks; the scripts then find it at its new index. INCREMENTAL TEST: swap
        // only Ice Queen -> slot 0 and check it's stable. Risk: the game runs during the (~25-50ms) swap and could
        // read half-swapped state. Toggle off here if it crashes/glitches.
        private const bool TrySlotSwap = true;
        // When false: NO position translation — Ice Queen + companions stay at their native (genuine-fight) positions
        // so the real coordinate frame is intact (for the Moon Sea test where her -Y corner is reachable). The death
        // patch + slot reorder still apply. Flip true to translate her onto a walkable chest (reachability) again.
        private const bool IqTranslate = false;
        private static bool _swapDone;

        private static readonly (long baseAddr, int stride)[] _slotBlocks =
        {
            (0x21E16BA0, 0x190),   // FloorSlot         (MMU + slot*0x190 + 0x1E3D0)
            (0x21E184A0, 0x3510),  // CCharacter        (MMU + slot*0x3510 + 0x1FCD0) — position/render/motion
            (0x21E4D5A0, 0x48),    // CRunScript        (MMU + slot*0x48 + 0x54DD0)   — STB VM state
            // NOTE: the per-slot alloc-ptr at MMU+slot*4 is deliberately NOT swapped — for slot 0 that's MMU+0
            // (the CMainMonstorUnit header), and swapping it corrupted the unit (frame-counter reset on activation).
        };
        // Native slot layout — CORRECTED from the genuine fight (2026-06-18). We had derived this from the global-int
        // MAILBOX index, which is NOT the actual slot. The genuine arena proved: slot 0=Queen(113), slot 1=the SHIELD
        // (baria — the only companion that takes damage, HP cycling 100→0→100), slot 2=korinoya(84), slot 5=reiki
        // (hp 80/scale 5). The eid-0 trio kori/i_meteo/i_tatumaki occupy the remaining slots (3/4/6) — order among
        // them is unconfirmed but the engine's shield gate keys on slot 1, so it shouldn't matter. Companions are
        // located here by their TagCompanions eid (240-244) only to drive the reorder.
        private static readonly (int nativeSlot, ushort eid)[] _nativeLayout =
        {
            (0, 113),   // Ice Queen
            (1, 240),   // baria (shield)   — genuine slot 1 (the damageable companion)
            (2, 84),    // korinoya (ice arrow)
            (3, 241),   // kori (ice source)
            (4, 242),   // i_meteo (meteor)
            (5, 244),   // reiki (aura)     — genuine slot 5 (hp 80, scale 5)
            (6, 243),   // i_tatumaki (tornado) — moved off slot 1 (slot 1 is the shield)
        };
        private static int FindSlotByEid(ushort eid)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) != -1
                    && Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId)) == eid)
                    return s;
            return -1;
        }
        // Once all 7 entities have spawned, physically move each to its native slot (selection-place: re-find by eid
        // each step since swaps shuffle things). Runs once per fight; re-armed when Ice Queen despawns.
        private static void ReorderToNativeOnce()
        {
            if (FindSlotByEid(113) < 0) { _swapDone = false; _korPatched = false; _bariaPatched = false; _mapDumped = false; _anchorSet = false; _shieldSlot = -1; return; }   // Ice Queen gone — re-arm
            if (_swapDone) return;
            foreach (var (_, eid) in _nativeLayout)
                if (FindSlotByEid(eid) < 0) return;                       // wait until all 7 are present
            _swapDone = true;
            int swaps = 0;
            foreach (var (t, eid) in _nativeLayout)
            {
                int s = FindSlotByEid(eid);
                if (s >= 0 && s != t) { SwapSlots(s, t); swaps++; }
            }
            // With the slottest iso (deterministic sequential spawn) the group should already be at native slots,
            // so 0 swaps => the spawn order matched _nativeLayout and NO physical slot-swap happened. Any swaps>0
            // means the spawn was still scrambled (e.g. unpatched iso or population != roster size).
            if (swaps == 0)
                Console.WriteLine("[IQreorder] spawn order ALREADY NATIVE (0-6) — 0 swaps, reorder skipped (deterministic spawn confirmed)");
            else
                Console.WriteLine($"[IQreorder] reordered to native slots 0-6 with {swaps} physical swap(s) — spawn was scrambled");
        }

        // King's Curse native slot layout (confirmed from the genuine SMT fight): coffin (c15a, id 115) at slot 0,
        // king (c15b, id 100) at slot 1. The two scripts coordinate by slot (like the Ice Queen cluster), so a roster
        // spawn that scrambles them breaks the fight. Physically move them to native slots once both are live.
        private static readonly (int nativeSlot, ushort eid)[] _kcNativeLayout =
        {
            (0, 115),   // coffin (c15a) — the HP/kill target
            (1, 100),   // king   (c15b) — the active, lockable attacker
        };
        private static bool _kcSwapDone;
        private static void ReorderKingsCurseOnce()
        {
            if (FindSlotByEid(115) < 0 && FindSlotByEid(100) < 0) { _kcSwapDone = false; return; }   // both gone — re-arm
            if (_kcSwapDone) return;
            foreach (var (_, eid) in _kcNativeLayout)
                if (FindSlotByEid(eid) < 0) return;                       // wait until BOTH are present
            _kcSwapDone = true;
            int swaps = 0;
            foreach (var (t, eid) in _kcNativeLayout)
            {
                int s = FindSlotByEid(eid);
                if (s >= 0 && s != t) { SwapSlots(s, t); swaps++; }
            }
            if (swaps == 0) Console.WriteLine("[KCreorder] coffin(115)/king(100) already at native slots 0/1 — 0 swaps");
            else Console.WriteLine($"[KCreorder] placed coffin(115)->slot0, king(100)->slot1 with {swaps} swap(s)");
        }

        // EXPERIMENT: the shield (baria) is natively eid 0; TagCompanions rewrites it to 240, which appears to stop
        // the engine treating it as the damageable shield that gates the Queen's invulnerability. Restore the live
        // slot's eid to 0 (its native identity) once it's located, while keeping the table-record tag for setup.
        // If the engine re-recognizes it, the shield takes physical damage + the Queen is protected. We track baria
        // by slot index thereafter (FindSlotByEid(240) no longer finds it). +0x42 is a ushort — never WriteInt it
        // (that clobbers +0x44 EntityScale).
        // Restore baria's live-slot eid to its native 0 after the reorder. Slot-order alone (baria -> slot 1) proved
        // INCONSISTENT — the eid-240 barrier took no physical damage even when reachable and correctly positioned,
        // while the genuine eid-0 barrier reliably does. The tag is needed only to drive the reorder; once baria is
        // at slot 1 we hand the engine its native identity so it treats it as the damageable shield. ON.
        private static readonly bool RestoreShieldEid = false;
        private static int _shieldSlot = -1;
        private static void RestoreShieldNativeEid()
        {
            if (!RestoreShieldEid) return;
            if (!_swapDone) return;   // CRITICAL: the reorder finds baria by eid 240 — don't zero it until baria is placed at slot 1
            if (_shieldSlot < 0) _shieldSlot = FindSlotByEid(240);   // locate while still tagged
            if (_shieldSlot < 0) return;
            long a = EnemyAddresses.FloorSlots.SlotAddr(_shieldSlot, EnemySlotOffsets.EnemySpeciesId);
            if (Memory.ReadUShort(a) != 0)
            {
                Memory.WriteUShort(a, 0);
                Console.WriteLine($"[IQshield] restored baria slot {_shieldSlot} eid 240 -> 0 (native shield identity)");
            }
        }

        // Force korinoya's [2] handler to reach its ack (SET_GLOBAL_INT(0,1) @0x104C) + homing. At +Y her reposition
        // mirrors to -Y, failing the distance/state checks that gate the ack via BR_FALSE @0x1004 (wait loop ->0xFD4)
        // and @0x1040 (skip-ack ->0x170C). Convert both BR_FALSE (op17) to POP (op16): POP consumes the same condition
        // the preceding BINOP pushed, then falls through — so she always acks (handshake advances) and runs her real
        // arrow homing, no stand-in. Re-armed per floor (STB reloads).
        private static bool _korPatched;
        private static void PatchKorinoyaArrow()
        {
            if (_korPatched) return;
            long stb = LocateStbByCodeOff(0x7D0);   // korinoya (CRunScript+0x3C; was a full RAM scan)
            if (stb < 0) return;
            _korPatched = true;
            int n = 0;
            if (Memory.ReadInt(stb + 0x1004) == 17) { Memory.WriteInt(stb + 0x1004, 16); n++; }
            if (Memory.ReadInt(stb + 0x1040) == 17) { Memory.WriteInt(stb + 0x1040, 16); n++; }
            Console.WriteLine($"[KORpatch] korinoya @0x{stb:X8}: {n} BR_FALSE gate(s) -> POP (force ack + homing)");
        }

        // Read both slots' blocks fully first, then write them swapped, to minimize the half-swapped window.
        private static void SwapSlots(int a, int b)
        {
            int nb = _slotBlocks.Length;
            var aData = new uint[nb][];
            var bData = new uint[nb][];
            for (int i = 0; i < nb; i++)
            {
                var (baseAddr, stride) = _slotBlocks[i];
                int words = stride / 4;
                aData[i] = Memory.ReadUIntBatch(baseAddr + (long)a * stride, words);
                bData[i] = Memory.ReadUIntBatch(baseAddr + (long)b * stride, words);
            }
            // Relocate self/cross pointers: a word pointing into the SOURCE slot's region must be re-aimed at the
            // DEST slot (+ (dst-src)*stride). aData came from slot a -> goes to b; bData from b -> goes to a.
            int relA = RelocatePointers(aData, a, b);
            int relB = RelocatePointers(bData, b, a);

            for (int i = 0; i < nb; i++)
            {
                var (baseAddr, stride) = _slotBlocks[i];
                Memory.WriteUIntBatch(baseAddr + (long)a * stride, bData[i]);
                Memory.WriteUIntBatch(baseAddr + (long)b * stride, aData[i]);
            }
            Console.WriteLine($"[IQswap] swapped slot {a} <-> {b}; relocated {relA}+{relB} self-pointers");
        }

        // Adjust every word in <data> that points into <srcSlot>'s FloorSlot/CCharacter/CRunScript region so it
        // points into <dstSlot>'s corresponding region (the entity is moving srcSlot -> dstSlot). Pointers are
        // PS2-native (addr & 0x1FFFFFFF), 4-byte aligned. Returns the count relocated.
        private static int RelocatePointers(uint[][] data, int srcSlot, int dstSlot)
        {
            int relocated = 0;
            for (int blk = 0; blk < data.Length; blk++)
                for (int wi = 0; wi < data[blk].Length; wi++)
                {
                    uint val = data[blk][wi];
                    long pn = val & 0x1FFFFFFF;
                    if ((pn & 3) != 0) continue;                         // pointers are word-aligned
                    for (int r = 0; r < _slotBlocks.Length; r++)
                    {
                        long lo = (_slotBlocks[r].baseAddr & 0x1FFFFFFF) + (long)srcSlot * _slotBlocks[r].stride;
                        if (pn >= lo && pn < lo + _slotBlocks[r].stride)
                        {
                            data[blk][wi] = (uint)(val + (long)(dstSlot - srcSlot) * _slotBlocks[r].stride);
                            relocated++;
                            break;
                        }
                    }
                }
            return relocated;
        }

        // Ice Queen's companions ship with EnemySpeciesId 0 in their table records (only korinoya/IceQueen have
        // real ids), so logs show them as "Unknown id=0". Write a synthetic id into each record BEFORE spawn (the
        // slot inherits the record's id at spawn) so [EnemyInfo]/the observer name them via Enemies.BossEnemies,
        // which also excludes them from miniboss scaling. Idempotent (only writes records still at 0).
        private static readonly (int tableIndex, ushort eid)[] _companionTags =
        { (101, 240), (102, 241), (103, 242), (104, 243), (92, 244) };  // baria, kori, i_meteo, i_tatumaki, reiki
        internal static void TagCompanions()
        {
            foreach (var (idx, eid) in _companionTags)
            {
                long a = EnemySpeciesTable.RecordAddress(idx) + EnemySpeciesTable.EnemySpeciesId;
                if (Memory.ReadUShort(a) == 0)
                {
                    Memory.WriteInt(a, eid);   // eid in low 16 bits; +0x7E padding (always 0) stays 0
                    Console.WriteLine($"[IQtag] companion table idx {idx} -> eid {eid}");
                }
            }
        }

        // Re-point every companion's boss reference at Ice Queen's actual slot — WITHOUT relying on the codeOff
        // (only korinoya/baria have code starting there; kori/i_meteo/i_tatumaki/reiki keep theirs elsewhere). We
        // signature-scan the whole boss-load window for the _GET_MONSTOR command shape, on the 12-byte vmcode grid:
        //   entry0 = push(cmdId 0xD5/0xE1)   entry1 = push(slot)   [ext follows]
        // A push is op 1 (value @+4) OR op 3 (value @+8). We only rewrite slot==0 (the native boss) -> iqSlot, so a
        // self/neighbor ref (non-zero) is never clobbered. Runs once per slot (re-armed when Ice Queen despawns).
        private static int PushVal(uint op, uint w1, uint w2) => op == 1 ? (int)w1 : op == 3 ? (int)w2 : int.MinValue;
        private static void RemapCompanionBossRefs(int iqSlot)
        {
            if (_remappedForSlot == iqSlot) return;
            var stbStarts = new System.Collections.Generic.List<(long a, uint co)>();
            var hits = new System.Collections.Generic.List<long>();
            const int Step = ChunkWords - 12;   // overlap so a command spanning a chunk edge isn't missed
            for (long a = ScanLo; a < ScanHi; a += (long)Step * 4)
            {
                long end = Math.Min(a + (long)ChunkWords * 4, ScanHi);
                int n = (int)((end - a) / 4);
                if (n <= 0x16) continue;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 7 < n; i++)
                {
                    if (w[i] == StbMagic && i + 0x15 < n) { stbStarts.Add((a + (long)i * 4, w[i + 0x15])); continue; }
                    int cmd = PushVal(w[i], w[i + 1], w[i + 2]);              // entry0 = push(cmdId)?
                    if (cmd != 0xD5 && cmd != 0xE1) continue;                // not _GET_MONSTOR_POS/_VECTOR
                    uint argOp = w[i + 3];                                    // entry1 = push(slot)
                    int slotWord = argOp == 1 ? i + 4 : argOp == 3 ? i + 5 : -1;
                    if (slotWord < 0 || slotWord >= n) continue;
                    if (w[slotWord] == 0)                                     // native boss ref only
                    {
                        Memory.WriteInt(a + (long)slotWord * 4, iqSlot);
                        hits.Add(a + (long)i * 4);
                    }
                }
            }
            _remappedForSlot = iqSlot;
            // attribute each hit to the STB that contains it (most recent start <= hit), for confirmation logging
            stbStarts.Sort((x, y) => x.a.CompareTo(y.a));
            var perCo = new System.Collections.Generic.SortedDictionary<uint, int>();
            foreach (long h in hits)
            {
                uint co = 0;
                foreach (var s in stbStarts) { if (s.a <= h) co = s.co; else break; }
                perCo[co] = perCo.TryGetValue(co, out int v) ? v + 1 : 1;
            }
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kv in perCo) parts.Add($"co=0x{kv.Key:X}:{kv.Value}");
            Console.WriteLine($"[IQremap] re-pointed {hits.Count} _GET_MONSTOR boss refs -> slot {iqSlot} [{string.Join(" ", parts)}]");
        }
        // One-run diagnostic: confirm the _SET_POSITION arg↔loc-axis mapping on Ice Queen herself, and that the
        // chest table is populated/valid at patch time — so the chest-based position translation can be wired up.
        private static bool _iqDiagDone = false;
        private static void DiagnoseIQ(int iqSlot)
        {
            if (_iqDiagDone) return;
            var (codeOff, initOff, _) = BossInfo(80);
            if (_stbBase < 0 || !IsBossStb(_stbBase, codeOff)) return;
            long b = _stbBase + initOff;   // _SET_POSITION block: args at +20/+32/+44, operands at +16/+28/+40
            int o1 = Memory.ReadInt(b + 16), v1 = Memory.ReadInt(b + 20);
            int o2 = Memory.ReadInt(b + 28), v2 = Memory.ReadInt(b + 32);
            int o3 = Memory.ReadInt(b + 40), v3 = Memory.ReadInt(b + 44);
            float lx = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(iqSlot, EnemySlotOffsets.LocationX));
            float lz = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(iqSlot, EnemySlotOffsets.LocationZ));
            float ly = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(iqSlot, EnemySlotOffsets.LocationY));
            float f1 = BitConverter.Int32BitsToSingle(v1), f2 = BitConverter.Int32BitsToSingle(v2), f3 = BitConverter.Int32BitsToSingle(v3);
            Console.WriteLine($"[IQdiag] _SET_POSITION args: a1(op{o1})={v1}/{f1:F1}  a2(op{o2})={v2}/{f2:F1}  a3(op{o3})={v3}/{f3:F1}");
            Console.WriteLine($"[IQdiag] IQ loc: X(0x100)={lx:F1}  Z/height(0x104)={lz:F1}  Y(0x108)={ly:F1}");
            int cc = Memory.ReadInt(ChestAddresses.ChestSlots.CountAddr);
            Console.WriteLine($"[IQdiag] chest count={cc}");
            for (int c = 0; c < Math.Min(Math.Max(cc, 0), 16); c++)
            {
                int af = Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.ActiveFlag));
                float wx = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldX));
                float wy = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldY));
                Console.WriteLine($"[IQdiag]   chest[{c}] active={af} world=({wx:F1},{wy:F1})");
            }
            _iqDiagDone = true;
        }

        // One-shot: list EVERY loaded STB (address + codeOff) across a wide window so we can find the companion
        // STBs the boss-ref scan missed (kori/i_meteo), and dump the bytecode grid of the codeOff-0x3AC companion
        // (the one with COMPUTED global indices) so we can decode how it builds its mailbox index.
        private static bool _stbListDone;
        private static void DumpAllStbsOnce()
        {
            if (_stbListDone) return; _stbListDone = true;
            const long lo = 0x01000000, hi = 0x01A00000;
            var found = new System.Collections.Generic.List<(long addr, uint co)>();
            for (long a = lo; a < hi; a += (long)ChunkWords * 4)
            {
                long end = Math.Min(a + (long)ChunkWords * 4, hi);
                int n = (int)((end - a) / 4);
                if (n <= 0x16) continue;
                uint[] w = Memory.ReadUIntBatch(a, n);
                for (int i = 0; i + 0x16 < n; i++)
                    if (w[i] == StbMagic) found.Add((a + (long)i * 4, w[i + 0x15]));
            }
            Console.WriteLine($"[IQstblist] {found.Count} STBs in 0x{lo:X}-0x{hi:X}:");
            foreach (var (addr, co) in found) Console.WriteLine($"[IQstblist]   0x{addr:X8} codeOff=0x{co:X}");
            foreach (var (addr, co) in found)
                if (co == 0x3AC)
                {
                    Console.WriteLine($"[IQgrid] STB 0x{addr:X8} co=0x3AC bytecode (op a1 a2):");
                    for (int k = 0; k < 130; k++)
                    {
                        long e = addr + co + (long)k * 12;
                        int op = Memory.ReadInt(e), a1 = Memory.ReadInt(e + 4), a2 = Memory.ReadInt(e + 8);
                        Console.WriteLine($"[IQgrid]   +0x{co + (uint)(k * 12):X}: op={op} a1={a1} a2={a2}");
                        if (op == 15) break;
                    }
                }
        }

        private static void PatchShieldTarget()
        {
            int n = EnemyAddresses.FloorSlots.Count;
            int iq = -1;
            for (int s = 0; s < n; s++)
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) != -1
                    && Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId)) == 113) { iq = s; break; }
            if (iq < 0) { _remappedForSlot = -1; return; }   // Ice Queen gone — re-arm the remap for next floor

            DiagnoseIQ(iq);

            // Option C: re-point EVERY companion's _GET_MONSTOR refs (baria, korinoya, kori, i_meteo, i_tatumaki,
            // reiki) at Ice Queen's actual slot — not just baria/korinoya — so every companion can position relative
            // to her and ack the handshake regardless of where the random spawn placed her.
            RemapCompanionBossRefs(iq);
            DumpAllStbsOnce();
        }

        // The handshake (global[0]) only advances when each companion ACKS its trigger. Confirmed by testing: at a
        // non-native slot the real korinoya never consumes [2] (stays frozen at idle) and global[0] is stuck at 0 —
        // IQ loops block 1 forever. The real companions only self-ack when the random spawn lands them near their
        // native slots; the _GET_MONSTOR remap fixes their positioning but NOT their ack. So we drive the handshake
        // via globals: korinoya's [2] block -> ack [0]=1 + fire [3]=1 (kori icicle) + consume [2]; reiki's [5] block
        // -> end the shield phase + loop back. (The visible flying arrow is the separate CFrame projectile.)
        private static void KorinoyaStandIn()
        {
            // korinoya ([2]): ack the master handshake so IQ advances past block 1, and trigger the kori icicle.
            long g2 = GlobalIntBase + 2 * 4;
            if (Memory.ReadInt(g2) == 1)
            {
                Memory.WriteInt(GlobalIntBase + 3 * 4, 1);  // fire the kori icicle effect
                Memory.WriteInt(GlobalIntBase + 0 * 4, 1);  // ack [0] = 1 (korinoya acted) -> IQ advances
                Memory.WriteInt(g2, 0);                     // consume the trigger
                Console.WriteLine("[IQkorinoya] [2]=1 -> set [3]=1, [0]=1, [2]=0 (ack handshake + icicle)");
            }

            // After kori acks ([0]=3) the genuine IQ alternates ATTACK waves ([0]3->4, i_meteo/meteor) and SHIELD
            // waves ([0]3->5, baria). The choice is driven by her internal counters [6]/[7]/[8], which don't cycle to
            // 2 off-native — so we only ever get attack waves and never a shield. Drive the alternation explicitly:
            // every 3rd completed wave is a shield wave (cancel the queued meteor + trigger reiki to end it & loop).
            if (Memory.ReadInt(GlobalIntBase) == 3 && Memory.ReadInt(GlobalIntBase + 4 * 4) == 1)
            {
                _waveCount++;
                if (_waveCount % 3 == 0)
                {
                    Memory.WriteInt(GlobalIntBase, 5);          // [0]=5 -> baria shield phase
                    Memory.WriteInt(GlobalIntBase + 4 * 4, 0);  // shield wave, not a meteor wave
                    Memory.WriteInt(GlobalIntBase + 5 * 4, 1);  // trigger reiki -> its stand-in ends the phase & loops
                    Console.WriteLine($"[IQwave] #{_waveCount}: SHIELD wave ([0]=3->5)");
                }
                else
                {
                    Memory.WriteInt(GlobalIntBase, 4);          // [0]=4 -> meteor attack wave
                    Console.WriteLine($"[IQwave] #{_waveCount}: meteor wave ([0]=3->4)");
                }
            }

            // reiki ([5]): ends the shield/defensive phase (sets [0]=0) so her dispatch loops back to the attack
            // rotation. Without it [0] stays 5 (shield) and she loops the shield forever instead of re-attacking.
            long g5 = GlobalIntBase + 5 * 4;
            if (Memory.ReadInt(g5) == 1)
            {
                Memory.WriteInt(g5, 0);                     // reiki acks its trigger
                Memory.WriteInt(GlobalIntBase + 0 * 4, 0);  // reiki clears the master handshake
                // Reset her internal barrier counters [6]/[7]/[8] to 0 (she does this herself each cycle in the
                // genuine fight; off-arena they stick at 1). This lets block 1 re-run -> re-trigger korinoya -> the
                // attack rotation loops instead of stalling after one wave.
                Memory.WriteInt(GlobalIntBase + 6 * 4, 0);
                Memory.WriteInt(GlobalIntBase + 7 * 4, 0);
                Memory.WriteInt(GlobalIntBase + 8 * 4, 0);
                Console.WriteLine("[IQreiki] [5]=1 -> reset [5],[0],[6],[7],[8]=0 (loop back to attacks)");
            }

            // Keep the rotation LOOPING: off-native IQ never recycles her phase counters [6]/[7]/[8] (they stick at
            // 1 after block 1), so block 1 never re-runs and the rotation stalls after one wave — which is why the
            // shield/attacks only appear intermittently. Re-arm block 1 each cycle by clearing them during the idle
            // gap ([0]==0). Guarded to only fire (and log) when they're actually stuck non-zero.
            if (Memory.ReadInt(GlobalIntBase) == 0
                && (Memory.ReadInt(GlobalIntBase + 6 * 4) != 0 || Memory.ReadInt(GlobalIntBase + 7 * 4) != 0 || Memory.ReadInt(GlobalIntBase + 8 * 4) != 0))
            {
                Memory.WriteInt(GlobalIntBase + 6 * 4, 0);
                Memory.WriteInt(GlobalIntBase + 7 * 4, 0);
                Memory.WriteInt(GlobalIntBase + 8 * 4, 0);
                Console.WriteLine("[IQloop] [0]==0 -> reset [6][7][8] (re-arm block 1)");
            }
        }

        // ── FIGHT OBSERVER (Ice Queen) ──────────────────────────────────────────────────────────────────
        // Logs the live-slot lifecycle (which species appear/clear, position, playing motion), Ice Queen's own
        // playing motion, and changes to the script GLOBAL-INT array (her companions read these to drive attacks).
        // Active only while species 113 (Ice Queen) is on the floor — run it during the GENUINE SW boss fight to
        // capture how the companions are summoned/positioned and the global-int coordination protocol.
        private const long GlobalIntBase  = 0x21D8FC80;   // _SET/_GET_GLOBAL_INT array (handler @ELF 0x1e5190): global[i] = base + i*4
        private const int  GlobalIntCount = 64;
        // The ice arrow (korinoya) homes to the PLAYER — _GET_POSITION(-2) copies the player vector from here.
        private const long PlayerPosVec = 0x21EA1D30;   // (X @+0, height @+4, Y @+8)
        private static System.DateTime _lastHomeLog = System.DateTime.MinValue;
        private static readonly int[] _lastHp = InitLastHp();   // per-slot HP-change watch
        private static int[] InitLastHp() { var a = new int[32]; for (int i = 0; i < a.Length; i++) a[i] = int.MinValue; return a; }
        // One-shot: identify the loaded map + dump the map/tile-grid frame, so the genuine Ice Queen arena
        // (where the ice-arrow homing works) can be compared against a random floor (where it doesn't).
        // Map structure documented in DungeonAddresses.cs.
        // One-shot: dump the engine's resolved spawn-candidate table (ArrangementPos indexes it at this+0x1dea8,
        // stride 0x9C, entry count = float @ this+0x48; this = CMonstorUnit = MainMonstorUnit). The deterministic-spawn
        // iso patch picks roster entry by table index, so we need to know whether the table preserves the roster order
        // (entry 0 == Ice Queen / tableIndex 80). We can't read the tableIndex directly, so dump the eid (+0x7C) and a
        // few raw words per entry to identify each. Runs at fight-detect on ANY build (independent of the iso patch).
        private static bool _spawnTblDumped;
        private static void DumpSpawnTable()
        {
            if (_spawnTblDumped) return;
            _spawnTblDumped = true;
            long mmu = MainMonstorUnit;
            int count = Memory.ReadInt(mmu + 0x48);   // INT count (engine cvt.s.w's it to float for the rand math)
            Console.WriteLine($"[SPAWNTBL] count={count} (table @ MMU+0x1dea8, stride 0x9C; flag@+0, eid@+4):");
            for (int i = 0; i < 9; i++)
            {
                long e = mmu + 0x1dea8 + (long)i * 0x9C;
                Console.WriteLine($"[SPAWNTBL]  [{i}] flag@+0={Memory.ReadInt(e)} eid@+4={Memory.ReadInt(e + 4)}");
            }
        }

        private static void DumpArena()
        {
            int mapNo = Memory.ReadInt(DungeonAddresses.Map.MapNo);
            uint mpRaw = Memory.ReadUInt(DungeonAddresses.Map.MapPartsPtr), ndRaw = Memory.ReadUInt(DungeonAddresses.Map.NowDngMapPtr);
            long mp = DungeonAddresses.Map.Deref(mpRaw), nd = DungeonAddresses.Map.Deref(ndRaw);
            Console.WriteLine($"[IQmap] map_no={mapNo}  mapparts=0x{mpRaw:X8}->0x{mp:X8}  NowDngMap=0x{ndRaw:X8}->0x{nd:X8}");
            void HexWin(string tag, long a)
            {
                if (a == 0) { Console.WriteLine($"[IQmap]   {tag}: <null>"); return; }
                uint[] w = Memory.ReadUIntBatch(a, 16);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < w.Length; i++) sb.Append(w[i].ToString("X8")).Append(' ');
                Console.WriteLine($"[IQmap]   {tag}@0x{a:X8}: {sb}");
            }
            HexWin("DngMap.head", nd != 0 ? nd : DungeonAddresses.Map.MainDungeonMap);
            HexWin("mapparts.head", mp);
        }
        private static bool _obsActive;
        private static readonly int[] _obsEid = new int[EnemyAddresses.FloorSlots.Count];
        private static readonly int[] _obsSdp = new int[EnemyAddresses.FloorSlots.Count];
        private static readonly int[] _obsMot = new int[EnemyAddresses.FloorSlots.Count];
        private static int[] _obsGlob;
        internal static void ObserveBossFight()
        {
            try
            {
                string ts = System.DateTime.Now.ToString("HH:mm:ss.fff");
                int n = EnemyAddresses.FloorSlots.Count;
                int iq = -1;
                for (int s = 0; s < n; s++)
                    if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) != -1
                        && Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId)) == 113)
                    { iq = s; break; }
                if (iq < 0)
                {
                    if (_obsActive) { Console.WriteLine("[IQobs] Ice Queen gone — stop"); _obsActive = false; for (int s = 0; s < n; s++) { _obsEid[s] = -2; _obsSdp[s] = 0; _obsMot[s] = -2; } _obsGlob = null; }
                    return;
                }
                if (!_obsActive)
                {
                    _obsActive = true; for (int s = 0; s < n; s++) _obsMot[s] = -2;
                    Console.WriteLine("[IQobs] Ice Queen fight detected — observing slots + globals");
                    DumpArena();
                    DumpSpawnTable();
                }
                float F(long a) => System.BitConverter.Int32BitsToSingle(Memory.ReadInt(a));
                bool rosterChanged = false;
                for (int s = 0; s < n; s++)
                {
                    bool live = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) != -1;
                    int eid = live ? Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId)) : -1;
                    int sdp = live ? (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.SpeciesDataPtr)) & 0x1FFFFFFF) : 0;
                    if (eid != _obsEid[s] || sdp != _obsSdp[s])
                    {
                        rosterChanged = true;
                        if (live)
                        {
                            int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                            int mhp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.MaxHp));
                            int pm = Memory.ReadInt(MainMonstorUnit + (long)s * MotionBlock + 0x20938);
                            // Native collision/targeting fields (compare genuine baria vs the Queen to learn what our
                            // spawn strips): EntityScale(+0x44 collision radius), lock-on reticle(+0x110/+0x114), COL_OFF(+0xA8).
                            float esc = F(EnemyAddresses.FloorSlots.SlotAddr(s, 0x44));
                            float rw = F(EnemyAddresses.FloorSlots.SlotAddr(s, 0x110)), rh = F(EnemyAddresses.FloorSlots.SlotAddr(s, 0x114));
                            int coff = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, 0xA8));
                            Console.WriteLine($"{ts} [IQobs] slot {s} LIVE eid={eid} sdp=0x{sdp:X6} hp={hp}/{mhp} scale={esc:F2} reticle={rw:F2}/{rh:F2} colOff={coff} pos=({F(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX)):F0},{F(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ)):F0},{F(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY)):F0}) playMot={pm}");
                        }
                        else Console.WriteLine($"{ts} [IQobs] slot {s} CLEARED");
                        _obsEid[s] = eid; _obsSdp[s] = sdp;
                    }
                    // track EVERY live slot's playing motion (companions react to her global signals)
                    int pmNow = live ? Memory.ReadInt(MainMonstorUnit + (long)s * MotionBlock + 0x20938) : -1;
                    if (pmNow != _obsMot[s])
                    {
                        if (live) Console.WriteLine($"{ts} [IQobs] slot {s}{(s == iq ? "(IceQueen)" : eid == 84 ? "(IceArrow)" : "")} playMot {_obsMot[s]} -> {pmNow}");
                        _obsMot[s] = pmNow;
                    }
                }
                // On any roster change (entity spawns / clears / changes eid), print a clean readable slot snapshot.
                if (rosterChanged) EnemySlots.BossInfo($"roster @ {ts}");
                // Per-frame HP watch — log the instant ANY slot's HP changes, so a landed hit is unmistakable and we
                // learn which entity takes which damage type. In the GENUINE fight this reveals baria's real eid and
                // its physical break threshold; the Queen should only drop to Ruby's MAGIC, baria only to PHYSICAL.
                for (int s = 0; s < n; s++)
                {
                    bool live = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) != -1;
                    int hp = live ? Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) : int.MinValue;
                    if (hp != _lastHp[s])
                    {
                        if (live && _lastHp[s] != int.MinValue)
                        {
                            int eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                            Console.WriteLine($"{ts} [HP] slot {s} eid={eid} {_lastHp[s]} -> {hp}");
                        }
                        _lastHp[s] = hp;
                    }
                }
                // Homing diagnostic. korinoya (eid 84) is a stationary EMITTER — its floor-slot LocationX/Y never
                // moves even in the genuine fight (where the arrow visibly flies & homes). The real moving position
                // is the CFrame (render transform) of the CCharacter at MainMonstorUnit + slot*0x3510 + 0x1fcd0,
                // reached via +0xBC -> +0x110 = CFrame; its local matrix is @ CFrame+0x150 (translation row TBD).
                // _SET_MOVE / _GET_POSITION(-1) operate on THIS, not the floor slot. We dump the matrix once for
                // Ice Queen (whose floor-slot pos is known, so we can confirm which row is the translation) and
                // log the world pos each tick for IQ + korinoya.
                if ((System.DateTime.Now - _lastHomeLog).TotalMilliseconds > 250)
                {
                    _lastHomeLog = System.DateTime.Now;
                    float pX = F(PlayerPosVec), pY = F(PlayerPosVec + 8);
                    void LogChar(string tag, int s)
                    {
                        // CCharacter live position (what _SET_POSITION/_SET_MOVE drive): the flying homing arrow
                        // IS this moving. Confirmed: korinoya idle = (0,10,-380) = its _SET_POSITION(0,10,380), Y neg.
                        long pos = EnemyAddresses.CharObjects.PosAddr(s);
                        float cx = F(pos), cz = F(pos + 4), cy = F(pos + 8);
                        float fx = F(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                        float fy = F(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                        int mot = Memory.ReadInt(MainMonstorUnit + (long)s * MotionBlock + 0x20938);
                        Console.WriteLine($"{ts} [IQpos] {tag} slot{s} char=({cx:F0},{cz:F0},{cy:F0}) floor=({fx:F0},{fy:F0}) mot={mot} player=({pX:F0},{pY:F0})");
                    }
                    LogChar("IceQueen", iq);
                    for (int s = 0; s < n; s++)
                    {
                        if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) == -1) continue;
                        ushort sid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                        if (sid == 84) LogChar("korinoya", s);
                        else if (sid == 240) LogChar("baria/shield", s);   // expect IQ.pos + facing×15 (just in front of her)
                    }
                }
                uint[] g = Memory.ReadUIntBatch(GlobalIntBase, GlobalIntCount);
                if (_obsGlob != null)
                {
                    bool anyChange = false;
                    for (int i = 0; i < GlobalIntCount; i++)
                        if ((int)g[i] != _obsGlob[i]) { Console.WriteLine($"{ts} [IQobs] global[{i}] {_obsGlob[i]} -> {(int)g[i]}"); anyChange = true; }
                    // On any attack-trigger change, snapshot the geometry so we can learn what gates each attack:
                    // IQ HP%, her position, the player's position, and the horizontal distance between them.
                    if (anyChange)
                    {
                        int hp  = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(iq, EnemySlotOffsets.Hp));
                        int mhp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(iq, EnemySlotOffsets.MaxHp));
                        float ix = F(EnemyAddresses.FloorSlots.SlotAddr(iq, EnemySlotOffsets.LocationX));
                        float iy = F(EnemyAddresses.FloorSlots.SlotAddr(iq, EnemySlotOffsets.LocationY));
                        float px = Memory.ReadFloat(Player.dunPositionX);
                        float py = Memory.ReadFloat(Player.dunPositionY);
                        float dist = (float)System.Math.Sqrt((ix - px) * (ix - px) + (iy - py) * (iy - py));
                        int pct = mhp > 0 ? hp * 100 / mhp : 0;
                        // Movement diagnostics: commanded speed (+0x80 MovementBlend) + facing vector (+0x60/64/68)
                        // + the collision-off countdown (+0xA8). If spd>0 but she doesn't move => collision-blocked;
                        // if spd==0 => her AI isn't commanding movement (rotation gated before the move step).
                        float spd = F(EnemyAddresses.FloorSlots.SlotAddr(iq, 0x80));
                        float fx = F(EnemyAddresses.FloorSlots.SlotAddr(iq, 0x60)), fz = F(EnemyAddresses.FloorSlots.SlotAddr(iq, 0x68));
                        int colOff = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(iq, 0xA8));
                        Console.WriteLine($"{ts} [IQctx] hp={hp}/{mhp} ({pct}%)  IQ=({ix:F0},{iy:F0})  player=({px:F0},{py:F0})  dist={dist:F0}  spd={spd:F1} face=({fx:F2},{fz:F2}) colOff={colOff}");
                    }
                }
                _obsGlob = new int[GlobalIntCount];
                for (int i = 0; i < GlobalIntCount; i++) _obsGlob[i] = (int)g[i];
            }
            catch { /* transient PINE read */ }
        }

        // Applies both STB patches to a located boss STB, then services the death->collapse->removal:
        //   (1) SPAWN FIX: NOP the label-1 _SET_POSITION(0,0,0) call (60 bytes) so the boss doesn't reset to
        //       the arena origin on a normal floor.
        //   (2) DEATH FIX: in label-120 (death), change cmd 0x6F _RUN_SCRIPT(510) -> 0xC8 _SET_MOTION(collapse)
        //       so the boss plays its own death/collapse animation instead of launching the defeat cutscene.
        //       The call stays a 2-item op21 (cmdId + 1 arg = motion index), so the stack is balanced.
        // Idempotent: each patch only writes if not already applied.
        private static void EnsureNopped(long stbBase, int initOff, int tableIndex)
        {
            long a = stbBase + initOff;
            if (tableIndex == 80)
            {
                // Capture her native _SET_POSITION coords once (constant across floors). worldX = arg1 (direct),
                // arg3 = z (worldY = -z). Used so we can nudge her by a fixed offset relative to native.
                if (float.IsNaN(_iqNatX))
                {
                    _iqNatX = ReadCoordArg(a + 16, a + 20);
                    _iqNatZ = ReadCoordArg(a + 40, a + 44);
                    Console.WriteLine($"[IQnudge] Ice Queen native worldX={_iqNatX:F0} zArg={_iqNatZ:F0} (worldY={-_iqNatZ:F0})");
                }
                if (IqNudgeX != 0f || IqNudgeY != 0f)
                {
                    // OFFSET TEST: +IqNudgeX worldX, +IqNudgeY worldY (zArg = nativeZ - IqNudgeY since worldY = -zArg)
                    WritePosArgs(a, _iqNatX + IqNudgeX, _iqNatZ - IqNudgeY);
                    // ALSO shift her dispatcher's arena-home SET_POS(0,0,177) @+0x2B48 — the native re-anchor the
                    // attack/activation keys off. Its args are push1 ints: x@+0x2B58, z@+0x2B70 (worldY = -z).
                    if (Memory.ReadInt(stbBase + 0x2B48) == 3 && Memory.ReadInt(stbBase + 0x2B48 + 8) == 0x24)
                    {
                        Memory.WriteInt(stbBase + 0x2B58, (int)IqNudgeX);          // x: 0 -> +IqNudgeX
                        Memory.WriteInt(stbBase + 0x2B70, 177 - (int)IqNudgeY);    // z: 177 -> 177-IqNudgeY
                    }
                }
                else if (IqTranslate && TryGetChestTarget(out float cx, out float cy))
                {
                    WritePosArgs(a, cx, -cy - ChestClearOffsetY);   // full chest translation (reachability, breaks the frame)
                    if (!_iqXlateLogged) { Console.WriteLine($"[IQxlate] Ice Queen STB 0x{stbBase:X8} -> chest ({cx:F0},{cy:F0})"); _iqXlateLogged = true; }
                }
            }
            else if (tableIndex == 81)
            {
                // King's Curse: OFFSET both him and the phase entity c15b to the largest room by the same delta (like Ice
                // Queen) so they spawn together on walkable ground. This needs a REAL room — if the grid was wiped/arena-
                // nulled (map_no 800) the "largest room" is a degenerate corner clump, which strands him somewhere
                // unreachable. In that case fall back to NOP (the engine's own ArrangementPos placement = findable).
                bool realRoom = _anchorSet && Math.Min(_anchorRoomW, _anchorRoomH) >= 3;
                if (realRoom) RelocateKingsCurseCoffinCluster(stbBase, a);
                else if (Memory.ReadInt(a) != 0)
                {
                    Memory.WriteByteArray(a, new byte[InitCmdLen]);
                    Console.WriteLine($"[BossPatch] boss 81: no usable room (anchorSet={_anchorSet} room={_anchorRoomW}x{_anchorRoomH}) — NOPed _SET_POSITION @ 0x{a:X8} (engine ArrangementPos)");
                }
            }
            else if (Memory.ReadInt(a) != 0)                            // other bosses: NOP the _SET_POSITION push as before
            {
                Memory.WriteByteArray(a, new byte[InitCmdLen]);
                Console.WriteLine($"[BossPatch] NOPed _SET_POSITION for boss {tableIndex} @ 0x{a:X8} (STB @ 0x{stbBase:X8})");
            }
            int rs = BossInfo(tableIndex).runScriptOff;
            int motion = CollapseMotion(tableIndex);
            int l120 = Label120Start(tableIndex);
            if (rs != 0 && motion >= 0 && l120 >= 0)
            {
                // King's Curse: rewriting only the _RUN_SCRIPT isn't enough — label-120 has EARLIER commands (before the
                // _RUN_SCRIPT) that fire the engine's dungeon-end (memory-card/save). NOP the WHOLE defeated-script with a
                // RET at its start so none of it runs; the collapse is driven entirely by CollapseDrive (label-100), which
                // keeps running after HP<=0 and overrides any label-120 motion anyway.
                if (Memory.ReadInt(stbBase + l120) != 15)
                {
                    byte[] ret = new byte[12]; System.BitConverter.GetBytes(15u).CopyTo(ret, 0);
                    Memory.WriteByteArray(stbBase + l120, ret);
                    Console.WriteLine($"[BossPatch] NOPed label-120 defeated-script (RET @start) for boss {tableIndex} @ 0x{stbBase + l120:X8}");
                }
                CollapseDrive(stbBase, tableIndex);
            }
            else if (rs != 0 && motion >= 0)
            {
                // label-120 -> _SET_MOTION(motion,1,2) so the death program plays the collapse (no cutscene).
                // rs points at the _RUN_SCRIPT cmdId byte (0x6F), pushed via op3 (value@+8 => cmdId vmcode @ rs-8).
                long rec0 = stbBase + rs - 8;
                if (Memory.ReadByte(stbBase + rs) == 0x6F)
                {
                    byte[] blk = new byte[60];
                    void Rec(int i, uint op, uint opnd, uint val)
                    {
                        System.BitConverter.GetBytes(op).CopyTo(blk, i * 12);
                        System.BitConverter.GetBytes(opnd).CopyTo(blk, i * 12 + 4);
                        System.BitConverter.GetBytes(val).CopyTo(blk, i * 12 + 8);
                    }
                    Rec(0, 3, 1, 0xC8); Rec(1, 3, 1, (uint)motion); Rec(2, 3, 1, 1); Rec(3, 3, 1, 2); Rec(4, 0x15, 4, 0);
                    Memory.WriteByteArray(rec0, blk);
                    Console.WriteLine($"[BossPatch] label-120 -> _SET_MOTION({motion},1,2) for boss {tableIndex} @ 0x{rec0:X8}");
                }
                CollapseDrive(stbBase, tableIndex);
            }
            else if (rs != 0)
            {
                long r = stbBase + rs;
                if (Memory.ReadByte(r) == 0x6F) { Memory.WriteByte(r, 0x68); Console.WriteLine($"[BossPatch] _RUN_SCRIPT->_STATUS_SET_DEAD for boss {tableIndex} @ 0x{r:X8}"); }
            }
            else
            {
                // No label-120 _RUN_SCRIPT (e.g. c22a/166): the engine already plays a clean death + fade, but
                // never frees the slot, so the collision lingers as an invisible blocker. Just remove it once faded.
                EngineDeathCleanup(tableIndex);
            }
        }

        // For bosses whose STOCK death already animates + fades cleanly (no cutscene, no label-120 _RUN_SCRIPT to
        // retarget), the only defect is that the engine leaves the slot live (collision persists). We let the engine
        // do the whole death animation, then free the slot ourselves once it has faded out (Opacity ~0) or timed out.
        private static void EngineDeathCleanup(int tableIndex)
        {
            ushort bossEid = Memory.ReadUShort(EnemySpeciesTable.RecordAddress(tableIndex) + EnemySpeciesTable.EnemySpeciesId);
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                ushort sid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                int rstat = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus));
                int hp    = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                if (sid != bossEid || rstat == -1 || hp > 0) { _deadSince.Remove(s); continue; }
                if (!_deadSince.ContainsKey(s))
                {
                    _deadSince[s] = System.DateTime.UtcNow;
                    Console.WriteLine($"[BossDeath] slot {s}: boss {tableIndex} dead — engine plays death+fade, awaiting fade-out to free the slot");
                }
                float opacity = System.BitConverter.Int32BitsToSingle(Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Opacity)));
                bool faded = opacity < 8f;     // engine has faded it out (default 128; hit-flash only dips to ~44)
                bool timedOut = (System.DateTime.UtcNow - _deadSince[s]).TotalMilliseconds > CollapseTimeoutMs;
                if (faded || timedOut)
                {
                    Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus), -1);
                    int cnt = Memory.ReadInt(MainMonstorUnit + EnemyAddresses.MainMonstorUnit.LiveCount);
                    if (cnt > 0) Memory.WriteInt(MainMonstorUnit + EnemyAddresses.MainMonstorUnit.LiveCount, cnt - 1);
                    Console.WriteLine($"[BossDeath] slot {s}: freed boss {tableIndex} ({(faded ? $"faded (opacity {opacity:F0})" : "timeout")}; count {cnt}->{cnt - 1})");
                    _deadSince.Remove(s);
                }
            }
        }

        // ┌─ SHELVED: Dran (c12a, TableIndex 78) flight on normal floors ───────────────────────────────────┐
        // Dran spawns and is defeatable, but on a normal floor he hovers high over the player (loc height ~57)
        // and won't descend, so only RANGED attacks (e.g. Xiao) can reach him — melee can't. This was chased
        // hard and PARKED (his spawn is intentionally left enabled; just omit him from a roster if you don't
        // want a ranged-only fight). Findings, so the next person doesn't redo them (full detail in
        // engine-symbols-and-collision.md):
        //   • He already PHASES walls: horizontal collision (CMonstorUnit::Step CheckHit/CheckWidth) is gated by
        //     the FALL flag FloorSlot+0x88, which is 0 for flyers. Forcing it to 1 re-enables collision and
        //     FREEZES him — proving the gate, and proving the "stuck on a wall" look is really hover, not a pin.
        //   • The jam is purely hover ALTITUDE. His position is written every frame through a CFrame hierarchy
        //     (loc = world matrix, not a single field), so mod-side clamps (slower than 60fps) get overwritten —
        //     altitude can't be held from C#. There is no settable "fly height" field/command in the symbols.
        //   • Dead ends (all verified in-game): redirecting flight/takeoff _SET_MOTION (changes only animation);
        //     zeroing flight _SET_MOVE speeds; holding _STATUS_SET_COL_OFF (FloorSlot+0xA8) high (gates the
        //     normal movers, not terrain); zeroing FALL (+0x88) (already 0); clamping CFrame +0x224 / loc Z.
        //   • To resume: break on the loc-height write and step up into the flight code to find the target-
        //     altitude value, then patch it once in the STB (no per-frame fight). That's the only viable path.
        // └──────────────────────────────────────────────────────────────────────────────────────────────────┘

        // ┌─ KNOWN ISSUE: Master Utan (79) displaces the roster-index-1 species ──────────────────────────┐
        // When Master Utan is force-spawned, the species at ROSTER INDEX 1 (the entry right after the boss)
        // spawns at a bad position — off-map / underground — for ALL its instances. Roster index 0 (the boss)
        // and index 2+ are fine. Minotaur Joe / Dran do NOT trigger it, so it's specific to Utan (likely its
        // multi-part / raised-arena setup).
        // Investigated (2026-06-14) and PARKED — findings so the next person doesn't redo them:
        //   • It is purely a POSITION corruption, not a model/block overlap: the index-1 species' model loads
        //     fine (valid, distinct model pointer; the model renders, just in the wrong place).
        //   • BOTH the logical slot position (LocationX/Z/Y @0x100/04/08) and the render-object position
        //     (unit+slot*0x3510+0x1FCD0 +0x10/+0x14/+0x18, where SetPosition@0x138fb0 stores it) are off-map.
        //   • The engine RE-APPLIES the bad position every frame, so correcting either live field loses the
        //     race (a live render<->slot sync only made the lock-on reticle flicker). Must be fixed at SPAWN.
        //   • Root cause not pinned: presumably the spawn-position assignment (ArrangementPos 0x1D7FC0 /
        //     SetupViewMonstor 0x1E02B0) uses a per-species offset or shared spawn-anchor that Utan shifts by 1.
        // WORKAROUND (in use): put a NON-SPAWNING entry at roster index 1 (e.g. a mimic — mimics don't spawn
        // via the roster; they use a separate dungeon-furniture mechanism), with real enemies at index 2+.
        // If this recurs on other bosses, resume from the spawn-position trace above.
        // └────────────────────────────────────────────────────────────────────────────────────────────────┘

        // Reads the STB program table to find the codeOffset (entry+4) of a label. run__CRunScript sets the
        // running program via ctx+0x2C = STB + that codeOffset, so this is what we redirect to.
        private static int LabelCodeOffset(long stbBase, int label)
        {
            int tblOff = Memory.ReadInt(stbBase + 0xC);
            int cnt = Memory.ReadInt(stbBase + 0x10);
            for (int i = 0; i < cnt && i < 64; i++)
            {
                long e = stbBase + tblOff + i * 8;
                if (Memory.ReadInt(e) == label) return Memory.ReadInt(e + 4);
            }
            return -1;
        }

        // Death state machine — runs each tick while the boss STB is patched. See the BOSS DEATH overview above.
        // Drives every dead boss slot (HP<=0): cancel -> (roar) -> slow collapse -> hold -> fade -> remove, by
        // clobbering the boss's label-100 bytecode and writing per-slot fields. Restores label-100 when none dead.
        private static void CollapseDrive(long stbBase, int tableIndex)
        {
            ushort bossEid = Memory.ReadUShort(EnemySpeciesTable.RecordAddress(tableIndex) + EnemySpeciesTable.EnemySpeciesId);
            int motion = CollapseMotion(tableIndex);
            int cb = Memory.ReadInt(stbBase + 0x8);                          // STB codeBase offset
            int coff100 = LabelCodeOffset(stbBase, 0x64);                    // label-100 = AI program
            if (coff100 < 0) return;
            long l100 = stbBase + cb + Memory.ReadInt(stbBase + coff100);   // label-100 vmcode start

            bool anyDead = false;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                ushort sid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                int rstat = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus));
                int hp    = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                if (sid != bossEid || rstat == -1 || hp > 0) { _collapseStart.Remove(s); _deadSince.Remove(s); _palStart.Remove(s); continue; }
                anyDead = true;
                int endF = CollapseEndFrame(tableIndex);
                int roar = RoarMotion(tableIndex);
                long frameAddr = MainMonstorUnit + (long)s * MotionBlock + MotionFrameField;
                // Phase start: cancel the current motion and play the ROAR (or go straight to collapse if no roar).
                if (_deathPhase == 0)
                {
                    _origLabel100 ??= ReadBytes(l100, 84);
                    Memory.WriteByteArray(l100, DeathSeq(roar >= 0 ? roar : motion));
                    _deathPhase = roar >= 0 ? 1 : 2;
                    _captured = false;
                    Console.WriteLine($"[BossDeath] death start -> {(roar >= 0 ? $"roar {roar}" : $"collapse {motion}")}");
                }
                if (!_deadSince.ContainsKey(s)) _deadSince[s] = System.DateTime.UtcNow;
                float frame = System.BitConverter.Int32BitsToSingle(Memory.ReadInt(frameAddr));
                // Current phase's motion frame range.
                int phStart = _deathPhase == 1 ? RoarStartFrame(tableIndex) : CollapseStartFrame(tableIndex);
                int phEnd   = _deathPhase == 1 ? RoarEndFrame(tableIndex)   : endF;

                if (!_captured)
                {
                    // INTERRUPT the current motion: the requested motion (slot+0xEC, set by our _SET_MOTION) is only
                    // committed to the render object when slot+0xF4 != 0 — which the engine normally sets only when the
                    // current clip finishes (that's the "finish current motion first" delay). Set it ourselves so the
                    // roar/collapse starts immediately. RE'd from CMonstorUnit::Step commit @ELF 0x1dd890.
                    Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.MotionCommitFlag), 1);
                    // Once the engine starts this phase's motion, freeze its playback so we drive the frame ourselves.
                    if (frame >= phStart - 2 && frame <= phEnd + 5)
                    {
                        Memory.WriteByteArray(l100, HoldSeq());          // no-op: stop engine frame-advance
                        _captured = true;
                        _captureFrame = frame;
                        _captureTime = System.DateTime.UtcNow;
                        Console.WriteLine($"[BossDeath] slot {s}: captured phase {_deathPhase} @frame {frame:F0} -> C#-advance @{PlaybackFps}fps");
                    }
                }
                else
                {
                    // C#-drive the frame from the capture point to the phase end at PlaybackFps.
                    float pf = _captureFrame + (float)(System.DateTime.UtcNow - _captureTime).TotalSeconds * PlaybackFps;
                    if (pf >= phEnd) pf = phEnd;
                    Memory.WriteInt(frameAddr, System.BitConverter.SingleToInt32Bits(pf));
                    if ((Environment.TickCount & 0x3F) < 8)
                        Console.WriteLine($"[BossDeath] slot {s}: phase={_deathPhase} playFrame={pf:F1}/{phEnd}");
                    if (pf >= phEnd)
                    {
                        if (_deathPhase == 1)               // roar done -> start the collapse
                        {
                            Memory.WriteByteArray(l100, DeathSeq(motion));
                            _deathPhase = 2;
                            _captured = false;
                            Console.WriteLine($"[BossDeath] roar done -> collapse {motion}");
                        }
                        else                                // collapse done -> hold the down pose + fade out
                        {
                            if (!_collapseStart.ContainsKey(s))
                            {
                                _collapseStart[s] = System.DateTime.UtcNow;
                                float op0 = System.BitConverter.Int32BitsToSingle(Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Opacity)));
                                _palStart[s] = new[] { op0 };
                                Console.WriteLine($"[BossDeath] slot {s}: collapse done -> hold+fade {FadeMs}ms (opacity {op0:F0})");
                            }
                            // frame is already pinned at endF by the pf write above. Ramp Opacity (default 128) -> 0.
                            float t = (float)(System.DateTime.UtcNow - _collapseStart[s]).TotalMilliseconds / FadeMs;
                            if (t > 1f) t = 1f;
                            if (_palStart.TryGetValue(s, out var p0))
                                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Opacity),
                                                System.BitConverter.SingleToInt32Bits(p0[0] * (1f - t)));
                        }
                    }
                }
                bool held = _collapseStart.TryGetValue(s, out var hs) && (System.DateTime.UtcNow - hs).TotalMilliseconds > FadeMs;
                bool timedOut = (System.DateTime.UtcNow - _deadSince[s]).TotalMilliseconds > CollapseTimeoutMs;
                if (held || timedOut)
                {
                    Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus), -1);
                    int cnt = Memory.ReadInt(MainMonstorUnit + EnemyAddresses.MainMonstorUnit.LiveCount);
                    if (cnt > 0) Memory.WriteInt(MainMonstorUnit + EnemyAddresses.MainMonstorUnit.LiveCount, cnt - 1);
                    Console.WriteLine($"[BossDeath] slot {s}: removed ({(held ? "hold done" : "timeout")}; count {cnt}->{cnt - 1})");
                    _deadSince.Remove(s); _collapseStart.Remove(s); _palStart.Remove(s);
                }
            }
            if (!anyDead && _deathPhase > 0 && _origLabel100 != null)
            {
                Memory.WriteByteArray(l100, _origLabel100);             // restore AI for live/respawned bosses
                _deathPhase = 0;
                _captured = false;
                Console.WriteLine("[BossDeath] restored label-100 AI (no dead boss)");
            }
        }

        private static byte[] DeathSeq(int motion)
        {
            // _SET_MOVE_CANSEL; _SET_MOTION(motion, 1, 2); push 0; RET — stop movement (_SET_MOVE_CANSEL cancels
            // MOVEMENT only, not the animation) and queue the motion (slot+0xEC). The actual animation interrupt is
            // CollapseDrive writing MotionCommitFlag (slot+0xF4). Re-issued each frame from the clobbered label-100.
            var recs = new (uint op, uint opnd, uint val)[]
            { (3,1,0x22),(0x15,1,0), (3,1,0xC8),(3,1,(uint)motion),(3,1,1),(3,1,2),(0x15,4,0), (3,1,0),(0xF,0,0) };
            byte[] blk = new byte[recs.Length * 12];
            for (int i = 0; i < recs.Length; i++)
            {
                System.BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * 12);
                System.BitConverter.GetBytes(recs[i].opnd).CopyTo(blk, i * 12 + 4);
                System.BitConverter.GetBytes(recs[i].val).CopyTo(blk, i * 12 + 8);
            }
            return blk;
        }


        private static byte[] HoldSeq()
        {
            // push 0; RET  — does nothing (label-100 no-op), so the current motion holds its last pose
            var recs = new (uint op, uint opnd, uint val)[] { (3,1,0),(0xF,0,0) };
            byte[] blk = new byte[recs.Length * 12];
            for (int i = 0; i < recs.Length; i++)
            {
                System.BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * 12);
                System.BitConverter.GetBytes(recs[i].opnd).CopyTo(blk, i * 12 + 4);
                System.BitConverter.GetBytes(recs[i].val).CopyTo(blk, i * 12 + 8);
            }
            return blk;
        }

        private static byte[] ReadBytes(long addr, int len)
        {
            byte[] b = new byte[len];
            for (int i = 0; i < len; i += 4)
                System.BitConverter.GetBytes(Memory.ReadInt(addr + i)).CopyTo(b, i);
            return b;
        }

    }
}
