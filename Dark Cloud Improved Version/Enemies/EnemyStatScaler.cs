using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Generalized live enemy-stat scaling primitives — the reusable core extracted from EnemyStatNormalizer.
    /// Each method multiplies a LIVE per-slot (or per-species) stat by a caller-supplied factor, so any feature
    /// (the dungeon normalizer, miniboss buffs, etc.) can drive arbitrary stat changes through one pipeline.
    ///
    /// SCOPE of each stat (important — some are per-slot, some are shared per species):
    ///   • <see cref="ScaleHp"/>       — per-SLOT (FloorSlot MaxHp/Hp). Affects only this enemy.
    ///   • <see cref="ScaleDefense"/>  — per-SLOT (packed DefenseStats: DamageReduction + WeaponDefense).
    ///   • <see cref="ScaleMelee"/>    — per-SLOT (the cached _SET_DMG_PARA array latched at the enemy's init).
    ///   • <see cref="ScaleProjectile"/> — per-SPECIES (the shared loaded STB shot literal / BehaviorScriptTable
    ///       default). Scaling it affects EVERY enemy of that species on the floor, not just this slot — so it is a
    ///       SPECIES buff, not a single-enemy one. Use deliberately. The BST default also persists across floors and
    ///       is snapshot/restored via <see cref="RestoreBst"/>.
    ///
    /// Per-floor lifecycle: call <see cref="ResetFloor"/> on each floor entry (clears the per-floor projectile-STB
    /// dedup), and <see cref="RestoreBst"/> on leaving the dungeon floor (reverts scaled static BST entries).
    /// </summary>
    internal static class EnemyStatScaler
    {
        internal static bool Verbose = false;   // verbose per-write logging

        // Per-floor: STB native bases whose shot literals are already scaled (the STB is shared/duplicated per
        // species, so scale it at most once per floor regardless of how many callers/slots reference it).
        private static readonly HashSet<int> _projPatchedStb = new();
        // BehaviorScriptTable default-shot damage: a STATIC ELF table that persists across floors, so we snapshot
        // each entry's boot value the first time we touch it and restore on dungeon exit (keyed by the +0x3C addr).
        private static readonly Dictionary<long, int> _bstOriginal = new();
        private static bool _bstDirty;
        // Per-slot projectile scaling (see MaintainSlotProjectile): ShotDmgCache field addr -> the scaled value we
        // last wrote, so re-reading our own value doesn't compound.
        private static readonly Dictionary<long, int> _shotScaled = new();

        private static int ScaleRound(int value, float factor)
            => value <= 0 ? value : Math.Max(1, (int)Math.Round(value * factor));

        /// <summary>Clear the per-floor projectile-STB dedup. Call once on each floor entry.</summary>
        internal static void ResetFloor() => _projPatchedStb.Clear();

        /// <summary>Clear the per-slot projectile (ShotDmgCache) scaling cache. Call when the scaled-slot set changes.</summary>
        internal static void ResetSlotProjectile() => _shotScaled.Clear();

        // ── Projectile, PER-SLOT (per-tick maintenance) ──────────────────────────────────────────────────
        /// <summary>
        /// Scale ONE enemy's projectile damage by rewriting its per-slot ShotDmgCache field — call EVERY tick for
        /// each slot you want buffed. Unlike <see cref="ScaleProjectile"/> (per-species STB), this is per-SLOT: the
        /// shot reads its damage from this field at ~hit time, and _SET_SHOT rewrites it (with the species base) on
        /// every shot, so it must be re-applied continuously. Whenever the field holds a NEW positive base we write
        /// base×factor; the shot then deals the scaled amount IF our write lands before it resolves (works at range —
        /// a point-blank shot can outrun the rewrite). Only scales positive values (never turns an idle -1 into a
        /// shot), and remembers each scaled value so it can't compound. Use ONLY for a few slots (e.g. minibosses) —
        /// it's a per-tick read/write per slot, so don't run it over all enemies.
        /// </summary>
        internal static void MaintainSlotProjectile(int slot, float factor)
        {
            if (factor == 1f) return;
            ScaleShotField(ShotDmgCache.ShotDamageAddr(slot), factor);
            ScaleShotField(ShotDmgCache.Shot2DamageAddr(slot), factor);
        }

        private static void ScaleShotField(long addr, float factor)
        {
            int cur = Memory.ReadInt(addr);
            if (cur <= 0) return;                                          // -1/0 = no explicit pending shot
            if (_shotScaled.TryGetValue(addr, out int t) && cur == t) return;  // already our scaled value (no compound)
            int scaled = Math.Max(1, (int)Math.Round(cur * factor));
            Memory.WriteInt(addr, scaled);
            _shotScaled[addr] = scaled;
        }

        // ── HP (per-slot) ───────────────────────────────────────────────────────────────────────────────
        /// <summary>Scale a slot's MaxHp by <paramref name="factor"/>, keeping the current health fraction.</summary>
        internal static void ScaleHp(int slot, float factor)
        {
            if (factor == 1f) return;
            long b = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
            int curMax = Memory.ReadInt(b + EnemySlotOffsets.MaxHp);
            if (curMax <= 0) return;
            int curHp  = Memory.ReadInt(b + EnemySlotOffsets.Hp);
            int newMax = ScaleRound(curMax, factor);
            int newHp  = Math.Max(1, (int)Math.Round(curHp * (newMax / (double)curMax)));
            Memory.WriteInt(b + EnemySlotOffsets.MaxHp, newMax);
            Memory.WriteInt(b + EnemySlotOffsets.Hp, newHp);
            if (Verbose) Console.WriteLine($"[StatScale]   slot {slot} HP: {curMax} -> {newMax} (×{factor:F2}); curHP {curHp} -> {newHp}");
        }

        // ── Defense (per-slot) ──────────────────────────────────────────────────────────────────────────
        /// <summary>Scale a slot's DamageReduction and WeaponDefense (the packed DefenseStats halfwords).</summary>
        internal static void ScaleDefense(int slot, float drFactor, float wdFactor)
        {
            if (drFactor == 1f && wdFactor == 1f) return;
            long b = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
            int packed = Memory.ReadInt(b + EnemySlotOffsets.DefenseStats);
            ushort dr = (ushort)ScaleRound(packed & 0xFFFF, drFactor);
            ushort wd = (ushort)ScaleRound((packed >> 16) & 0xFFFF, wdFactor);
            Memory.WriteInt(b + EnemySlotOffsets.DefenseStats, (dr & 0xFFFF) | (wd << 16));
            if (Verbose) Console.WriteLine($"[StatScale]   slot {slot} def DR: {packed & 0xFFFF} -> {dr} (×{drFactor:F2}), WD: {(packed >> 16) & 0xFFFF} -> {wd} (×{wdFactor:F2})");
        }

        // ── Melee (per-slot) ────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Scale a slot's cached melee-damage entries. Value-matched against <paramref name="meleeDamage"/> (the
        /// species' known per-attack values) so it only touches the damage ints, not the hitbox-geometry ints that
        /// share the _SET_DMG_PARA cache. Cascade-guarded (won't re-scale a value it just produced) → idempotent.
        /// </summary>
        internal static void ScaleMelee(int slot, int[] meleeDamage, float factor)
        {
            if (meleeDamage == null || meleeDamage.Length == 0 || factor == 1f) return;

            var map = new Dictionary<int, int>();                 // native value -> scaled value
            foreach (int nativeDmg in meleeDamage)
                if (nativeDmg > 0 && !map.ContainsKey(nativeDmg)) map[nativeDmg] = ScaleRound(nativeDmg, factor);
            var produced = new HashSet<int>(map.Values);          // anti-cascade

            long addr = DmgParaCache.ArrayAddr(slot);
            byte[] d = Memory.ReadByteArray(addr, DmgParaCache.ReadLength);
            if (d == null) return;
            int hits = 0;
            for (int i = 0; i + 4 <= d.Length; i += 4)
            {
                int v = d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
                if (map.TryGetValue(v, out int nv) && nv != v && !produced.Contains(v))
                {
                    Memory.WriteInt(addr + i, nv);
                    hits++;
                    if (Verbose) Console.WriteLine($"[StatScale]   slot {slot} melee +0x{i:X}: {v} -> {nv} (×{factor:F2})");
                }
            }
            if (Verbose && hits == 0)
                Console.WriteLine($"[StatScale]   slot {slot} melee: ×{factor:F2} but none of [{string.Join(",", meleeDamage)}] found in cache @0x{addr:X8}");
        }

        // ── Projectile (per-SPECIES — shared STB; affects all enemies of the species) ─────────────────────
        /// <summary>
        /// Scale a species' PROJECTILE damage by patching the STB op3 int-literal feeding _SET_SHOT/_SET_SHOT2's
        /// 5th (damage) arg, and the BehaviorScriptTable default for "default" shooters. ⚠ PER-SPECIES: the STB is
        /// shared, so this buffs every live enemy of the species, not just <paramref name="slot"/>. Scaled at most
        /// once per floor per STB (see <see cref="ResetFloor"/>). See EnemyAddresses StbVm / ShotDmgCache for the RE.
        /// </summary>
        /// <returns><c>true</c> when the slot's STB was READY and processed (or there was nothing to do), so the
        /// caller can stop retrying; <c>false</c> when the STB script pointer (CRunScript+0x3C) hasn't attached yet —
        /// the slot's VM state loads a few ticks AFTER its CCharacter slot goes live, so the caller must retry until
        /// this returns true or it never will (see EnemyStatNormalizer's projectile-pending retry).</returns>
        internal static bool ScaleProjectile(int slot, int tableIndex, float factor)
        {
            if (factor == 1f) return true;                   // nothing to scale — done
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return false;  // STB not attached yet → retry next tick
            if (!_projPatchedStb.Add(stbNative)) return true;     // this STB's shot literals already scaled this floor
            long stb = Memory.ToMmu(stbNative);
            if (Memory.ReadInt(stb) != StbVm.Magic) { _projPatchedStb.Remove(stbNative); return false; }  // stale read → retry

            const int Window = 0xC000;
            byte[] d = Memory.ReadByteArray(stb, Window);
            if (d == null || d.Length < 0x10) { _projPatchedStb.Remove(stbNative); return false; }        // read failed → retry
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(StbVm.CodeSectionOff);
            if (code < StbVm.LabelTableOff || code >= d.Length) return true;   // malformed but present — won't improve on retry

            var callsByTarget = new Dictionary<int, List<int>>();
            for (int i = code; i + StbVm.InstrSize <= d.Length; i += 4)
                if (Word(i) == StbVm.OpCallFunc || Word(i) == StbVm.OpCallFuncCond)
                    (callsByTarget.TryGetValue(Word(i + StbVm.OperandB), out var cs) ? cs : (callsByTarget[Word(i + StbVm.OperandB)] = [])).Add(i);

            var patched = new HashSet<int>();
            int hits = 0;

            void ScaleLiteralAt(int litOff)
            {
                if (litOff < code || litOff + 4 > d.Length || !patched.Add(litOff)) return;
                int v = Word(litOff);
                if (v <= 0) return;
                int nv = ScaleRound(v, factor);
                if (nv == v) return;
                Memory.WriteInt(stb + litOff, nv);
                hits++;
                if (Verbose) Console.WriteLine($"[StatScale]   slot {slot} proj STB 0x{stbNative:X8}+0x{litOff:X}: {v} -> {nv} (×{factor:F2})");
            }

            int FuncStart(int off)
            {
                int start = code;
                foreach (int t in callsByTarget.Keys) if (t <= off && t > start) start = t;
                return start;
            }

            List<int> ArgsBefore(int callSite)
            {
                var argOffsets = new List<int>();
                for (int j = callSite - StbVm.InstrSize; j >= code && Word(j) is StbVm.OpPush1 or StbVm.OpPush2 or StbVm.OpPush3; j -= StbVm.InstrSize) argOffsets.Add(j);
                argOffsets.Reverse();
                return argOffsets;
            }

            void ScaleArg(int funcStart, int idx, HashSet<long> seen, int depth)
            {
                if (depth > 8 || !callsByTarget.TryGetValue(funcStart, out var sites)) return;
                if (!seen.Add(((long)funcStart << 20) ^ (uint)idx)) return;
                foreach (int callSite in sites)
                {
                    var args = ArgsBefore(callSite);
                    if (idx < 0 || idx >= args.Count) continue;
                    int argInstr = args[idx];
                    if (Word(argInstr) == StbVm.OpPush3 && Word(argInstr + StbVm.OperandA) == StbVm.TypeInt) ScaleLiteralAt(argInstr + StbVm.OperandB);
                    else if (Word(argInstr) == StbVm.OpPush1 && Word(argInstr + StbVm.OperandB) == StbVm.ScopeLocal) ScaleArg(FuncStart(callSite), Word(argInstr + StbVm.OperandA), seen, depth + 1);
                }
            }

            for (int x = code; x + StbVm.InstrSize <= d.Length; x += 4)
            {
                if (Word(x) != StbVm.OpExt || Word(x + StbVm.OperandB) != 0) continue;
                int argc = Word(x + StbVm.OperandA);
                if (argc < 2 || argc > 10) continue;
                int funcIdPos = x - argc * StbVm.InstrSize;
                if (funcIdPos < code) continue;
                if (Word(funcIdPos) != StbVm.OpPush3 || Word(funcIdPos + StbVm.OperandA) != StbVm.TypeInt) continue;
                int funcId = Word(funcIdPos + StbVm.OperandB);
                if (funcId != StbVm.FnSetShot && funcId != StbVm.FnSetShotReg && funcId != StbVm.FnSetShot2) continue;
                if (argc != 6) { PatchBstDefault(slot, tableIndex, factor); continue; }   // default shot → BST +0x3C
                int dmgArgInstr = x - StbVm.InstrSize;
                if (Word(dmgArgInstr) == StbVm.OpPush3 && Word(dmgArgInstr + StbVm.OperandA) == StbVm.TypeInt)
                    ScaleLiteralAt(dmgArgInstr + StbVm.OperandB);
                else if (Word(dmgArgInstr) == StbVm.OpPush1 && Word(dmgArgInstr + StbVm.OperandB) == StbVm.ScopeLocal)
                    ScaleArg(FuncStart(x), Word(dmgArgInstr + StbVm.OperandA), [], 0);
            }
            if (Verbose && hits == 0)
                Console.WriteLine($"[StatScale]   slot {slot} proj: ×{factor:F2}, no scalable _SET_SHOT literal in STB 0x{stbNative:X8} (computed/default shooter or no shot)");
            return true;   // STB was ready and fully scanned
        }

        // Scale a "default" shooter's BST base damage (BehaviorScriptTable entry +0x3C). Snapshots the boot value
        // once (restored on dungeon exit, since the BST is static), then writes round(original × factor).
        private static void PatchBstDefault(int slot, int tableIndex, float factor)
        {
            if (tableIndex < 0) return;
            int idx = Memory.ReadUShort(EnemySpeciesTable.TableBase + (long)tableIndex * EnemySpeciesTable.Stride + EnemySpeciesTable.PrimaryBstIndex);
            if (idx == 0xFFFF) return;
            int btNative = Memory.ReadInt(BehaviorScriptTable.PointerArray + idx * 4);
            if (btNative == 0) return;
            long dmgAddr = Memory.ToMmu(btNative) + BehaviorScriptTable.ShotBaseDamage;

            if (!_bstOriginal.TryGetValue(dmgAddr, out int orig))
            {
                orig = Memory.ReadInt(dmgAddr);
                _bstOriginal[dmgAddr] = orig;
            }
            if (orig <= 0) return;
            int nv = ScaleRound(orig, factor);
            if (Memory.ReadInt(dmgAddr) == nv) return;
            Memory.WriteInt(dmgAddr, nv);
            _bstDirty = true;
            if (Verbose) Console.WriteLine($"[StatScale]   slot {slot} proj BST idx{idx} @0x{dmgAddr:X8}: {orig} -> {nv} (×{factor:F2})");
        }

        /// <summary>Restore every scaled static BST default-shot entry to its boot value. Call on dungeon-floor exit.</summary>
        internal static void RestoreBst()
        {
            if (!_bstDirty) return;
            foreach (var kv in _bstOriginal) Memory.WriteInt(kv.Key, kv.Value);
            _bstDirty = false;
            if (Verbose) Console.WriteLine($"[StatScale] restored {_bstOriginal.Count} BST default-shot entr{(_bstOriginal.Count == 1 ? "y" : "ies")} on dungeon exit.");
        }
    }
}
