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
            int[] bases = { BtEnemyLayout.LayoutBase[dungeon], BtEnemyLayout.UraLayoutBase[dungeon] };
            foreach (int layoutBase in bases)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    // entry 0 = this species at 100% weight.
                    long e0 = BtEnemyLayout.EntryAddress(layoutBase, floor, 0);
                    Memory.WriteInt(e0 + BtEnemyLayout.Id, tableIndex);
                    Memory.WriteInt(e0 + BtEnemyLayout.Weight, 100);
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
            Memory.WriteUShort(rec + EnemySpeciesTable.AttackPower, 100);
            Console.WriteLine($"[EnemyInjector] de-sentineled AttackPower (65535->100) for TableIndex {tableIndex}.");
        }

        internal static void SetPopulationTarget(int pop)
        {
            Memory.WriteInt(PopCount6494, pop);
            Memory.WriteInt(PopCount649C, pop);
            Memory.WriteInt(PopCount64A0, pop);
            Console.WriteLine($"[EnemyInjector] population target (0x21D56494/9C/A0) <- {pop} "
                + "(capped by walkable tiles; re-set after floor-select if it gets overwritten).");
        }

        /// <summary>
        /// Sets the current dungeon's roster (normal + Ura, all floors) to a MIX of species: fills entries
        /// 0..n-1 with the given TableIndexes at even weights (summing to 100), disables the rest. Every
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

            // Weights are unused by the spawn path (confirmed: uniform rand%count), so just split evenly.
            int[] w = new int[n];
            int baseW = 100 / n, rem = 100 - baseW * n;
            for (int i = 0; i < n; i++) w[i] = baseW + (i < rem ? 1 : 0);

            // Per-species "spawn once vs repeatable" flag = EnemySpeciesTable.SpawnCap (+0x78): 0/3 = repeatable,
            // anything else = at most one per floor (the placement loop retries, so total stays 15). Write
            // it on the source record so the floor-load copy carries it.
            int armedBoss = -1;
            for (int i = 0; i < n; i++)
            {
                long rec = EnemySpeciesTable.RecordAddress(tableIndices[i]);
                // Boss-class = ModelCode starts with 'c' (cNNx); stable across RegularizeBossRecord (which
                // only touches AttackPower). Multi-part bosses share one skeleton, so >1 instance glitches —
                // force them spawn-once regardless of the '!' flag.
                bool isBossClass = Memory.ReadByte(rec + EnemySpeciesTable.ModelCode) == (byte)'c';
                bool isOnce = (spawnOnce != null && i < spawnOnce.Length && spawnOnce[i]) || isBossClass;
                Memory.WriteInt(rec + EnemySpeciesTable.SpawnCap, isOnce ? 2 : 0);
                RegularizeBossRecord(tableIndices[i]);
                // Arm the script patch for the first boss that has a known _INITIALIZE fix (Dran/Master Utan/
                // MinotaurJoe/c22a). Other 'c' bosses still spawn (spawn-once) but may be displaced — WIP.
                if (armedBoss < 0 && BossScriptPatcher.IsPatchable(tableIndices[i])) armedBoss = tableIndices[i];
            }
            BossScriptPatcher.ArmedBoss = armedBoss;
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
                            Memory.WriteInt(ea + BtEnemyLayout.Weight, w[e]);
                        }
                        else
                        {
                            Memory.WriteInt(ea + BtEnemyLayout.Id, -1);
                        }
                    }
                }
            }
            if (population > 0) SetPopulationTarget(population);
            var onceList = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
                if (spawnOnce != null && i < spawnOnce.Length && spawnOnce[i]) onceList.Add(tableIndices[i]);
            string once = onceList.Count == 0 ? "none" : string.Join(",", onceList);
            Console.WriteLine($"[EnemyInjector] roster mix: dungeon {dungeon}, {n} species "
                + $"[{string.Join(",", tableIndices[..n])}] spawn-once=[{once}] across {floors} floors. Re-enter a floor.");
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

        /// <summary>
        /// "Defuse" boss-class live slots: any slot whose SpeciesDataPtr (slot+0x4C) is non-zero gets it
        /// zeroed, so the engine treats it like a regular enemy (regular enemies legitimately run with
        /// 0x4C == 0, so the engine guards against null) instead of running the arena-bound boss behavior
        /// script that hangs a normal floor. Returns how many slots were defused.
        /// EXPERIMENTAL: if the floor hang is a one-time boss-intro lock fired at spawn, nulling afterward
        /// may not un-stick it — clicking this mid-hang tells us which.
        /// </summary>
        internal static int DefuseBossSlots()
        {
            int defused = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long ptrAddr = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.SpeciesDataPtr);
                int sdp = Memory.ReadInt(ptrAddr);
                if (sdp != 0)
                {
                    ushort sid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                    Memory.WriteInt(ptrAddr, 0);
                    Console.WriteLine($"[EnemyInjector] defused slot {s} (species id {sid}): SpeciesDataPtr 0x{sdp:X8} -> 0");
                    defused++;
                }
            }
            Console.WriteLine($"[EnemyInjector] defuse: {defused} boss-class slot(s) nulled.");
            return defused;
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

        // Per-boss STB layout: TableIndex -> (label-1 codeOffset [signature, read at STB+0x54], _INITIALIZE
        // file offset to NOP). These 4 bosses run cmd 0x24 _INITIALIZE(0,0,0) in label-1; the others lack it
        // and need their own mechanism (see §5c). Offsets extracted from each dun\monstor\<code>.stb.
        private static (uint codeOff, int initOff) BossInfo(int tableIndex) => tableIndex switch
        {
            78  => (0x3AC8u, 0x419C),  // Dran        (c12a)
            79  => (0x4484u, 0x4F48),  // Master Utan (c14a)
            83  => (0x575Cu, 0x631C),  // MinotaurJoe (c16a)
            166 => (0x60C0u, 0x6C20),  // c22a
            _   => (0u, 0),
        };
        internal static bool IsPatchable(int tableIndex) => BossInfo(tableIndex).codeOff != 0;

        private const long ScanLo = 0x01000000, ScanHi = 0x01500000;  // boss-first load window (per dungeon/half)
        private const int  ChunkWords = 8192;           // 32 KB per batched round-trip
        private const uint StbMagic = 0x00425453u;      // "STB\0"
        private const int  InitCmdLen = 60;             // _INITIALIZE call = 5 vmcode_t records (all 4 bosses)

        private static long _stbBase = -1;

        // Known STB load addresses per (boss TableIndex, dungeon). Boss-first makes these roster-independent;
        // one per floor-half (the address shifts across the mid-dungeon event floor) — we probe all candidates,
        // so no half-detection is needed. c16a populated from testing; other bosses/dungeons fill via the scan
        // fallback (logged). Option 2 (live heap-base read) will make these unnecessary.
        private static long[] KnownAddrs(int tableIndex, int dungeon) => (tableIndex, dungeon) switch
        {
            (83, 0) => new long[] { 0x011A8990, 0x011A8950 },             // c16a Divine Beast Cave
            (83, 1) => new long[] { 0x013B7190, 0x013B7250 },             // c16a Wise Owl Forest
            (83, 2) => new long[] { 0x012BF210, 0x012BF190, 0x012CACD0 }, // c16a Shipwreck (3 sections)
            (83, 3) => new long[] { 0x01137750 },                         // c16a Sun & Moon Temple (early floors)
            _ => System.Array.Empty<long>(),
        };

        internal static void Tick()
        {
            int ti = ArmedBoss;
            if (ti < 0) { _stbBase = -1; return; }
            var (codeOff, initOff) = BossInfo(ti);
            if (codeOff == 0) { _stbBase = -1; return; }
            try
            {
                int dungeon = Memory.ReadByte(Addresses.checkDungeon);
                // 1) Known addresses for (boss, dungeon) — instant, deterministic, no scan.
                foreach (long addr in KnownAddrs(ti, dungeon))
                    if (IsBossStb(addr, codeOff)) { EnsureNopped(addr, initOff, ti); return; }
                // 2) Cached hit from a previous (untabled) relocate.
                if (_stbBase >= 0 && IsBossStb(_stbBase, codeOff)) { EnsureNopped(_stbBase, initOff, ti); return; }
                // 3) Fallback scan-by-signature; logs the address so it can be tabled.
                long hit = _stbBase >= 0 ? ScanRange(_stbBase - 0x40000, _stbBase + 0x40000, codeOff) : -1;
                if (hit < 0) hit = ScanRange(ScanLo, ScanHi, codeOff);
                if (hit >= 0)
                {
                    if (hit != _stbBase)
                        Console.WriteLine($"[BossPatch] located boss {ti} STB @ 0x{hit:X8} (dungeon {dungeon}) — add to KnownAddrs for instant patch.");
                    _stbBase = hit;
                    EnsureNopped(hit, initOff, ti);
                }
            }
            catch { /* transient PINE read; retry next tick */ }
        }

        private static long ScanRange(long lo, long hi, uint codeOff)
        {
            if (lo < ScanLo) lo = ScanLo;
            if (hi > ScanHi) hi = ScanHi;
            for (long a = lo; a < hi; a += (long)ChunkWords * 4)
            {
                long hit = ScanChunk(a, codeOff);
                if (hit >= 0) return hit;
            }
            return -1;
        }

        private static bool IsBossStb(long b, uint codeOff)
            => Memory.ReadUInt(b) == StbMagic && (uint)Memory.ReadInt(b + 0x54) == codeOff;

        private static long ScanChunk(long start, uint codeOff)
        {
            long end = Math.Min(start + (long)ChunkWords * 4, ScanHi);
            int n = (int)((end - start) / 4);
            if (n <= 0x16) return -1;
            uint[] w = Memory.ReadUIntBatch(start, n);
            for (int i = 0; i + 0x16 < n; i++)          // word at +0x54 (= i+0x15) holds label-1's codeOffset
                if (w[i] == StbMagic && w[i + 0x15] == codeOff)
                    return start + (long)i * 4;
            return -1;
        }

        private static void EnsureNopped(long stbBase, int initOff, int tableIndex)
        {
            long a = stbBase + initOff;
            if (Memory.ReadInt(a) != 0)                                // _INITIALIZE push still present
            {
                Memory.WriteByteArray(a, new byte[InitCmdLen]);
                Console.WriteLine($"[BossPatch] NOPed _INITIALIZE for boss {tableIndex} @ 0x{a:X8} (STB @ 0x{stbBase:X8})");
            }
        }
    }
}
