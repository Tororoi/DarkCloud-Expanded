namespace Dark_Cloud_Improved_Version
{
    /// <summary>The engine's TARGET REDIRECT: every monster script's `_GET_POSITION(-2)` / `_GET_DISTANCE(-2)` ("where is the player")
    /// reads the address held in the per-slot pointer table (<see cref="CodeCaves.PtrTable"/>) instead of the player global.
    /// Both functions are copied into cold-PINE caves (<see cref="CodeCaves.PosCave"/>, <see cref="CodeCaves.DistCave"/>) with
    /// the hardcoded player load replaced by a jump to <see cref="PerSlotTargetHelper"/>, and the STB external-command dispatch
    /// table is repointed at the copies — a pure data path (docs/cave-code-execution.md), so nothing live is rewritten and the
    /// caves are armed once, at the cold window (the main menu), by <see cref="ArmColdPatch"/>. An un-fooled slot holds the
    /// player global itself, so that enemy reads bit-identical vanilla; what points each slot elsewhere is
    /// <see cref="AggroTable"/>'s business (Mirage's decoy, the shield ring, the judgement blade, confusion), all gated on
    /// <see cref="Armed"/>. docs/aggro-redirect.md.</summary>
    internal static class TargetRedirectCaves
    {
        private static bool _armed;                 // both caves hosted + dispatch repointed this session (cold)

        /// <summary>Both per-slot redirect caves armed (the pointer table is live for every enemy read).</summary>
        internal static bool Armed => _armed;

        /// <summary>Arm both engine redirects at the COLD window (from ApplyNewChanges, retried from Mirage's loop while out of a
        /// dungeon). Puts the table at rest first (every slot on the live player) so the first enemy read is valid. Idempotent:
        /// returns at once when armed, and retries any cave that could not arm (e.g. the function was not vanilla yet).</summary>
        internal static void ArmColdPatch()
        {
            if (_armed) return;
            AggroTable.ResetAll();                  // every slot → live-player pointer (valid before any enemy read)
            bool pos  = ArmRedirectCave("_GET_POSITION", StbExternCmd.GetPositionFn, CodeCaves.PosCave,  CodeCaves.PosCaveGuest,
                                        StbExternCmd.GetPositionSlot,  StbExternCmd.PosPlayerLdOff,  StbExternCmd.PosCopyJalOff);
            bool dist = ArmRedirectCave("_GET_DISTANCE", StbExternCmd.GetDistanceFn, CodeCaves.DistCave, CodeCaves.DistCaveGuest,
                                        StbExternCmd.GetDistanceSlot, StbExternCmd.DistPlayerLdOff, StbExternCmd.DistCopyJalOff);
            _armed = pos && dist;
        }

        // ── The cave payload ─────────────────────────────────────────────────────────────────────────
        // The generic hosting mechanism (copy → detour → repoint dispatch) lives in RuntimeCaveWriter; what is specific to
        // the redirect is only the HELPER below — the code spliced into each copy.
        //
        // _GET_POSITION / _GET_DISTANCE both read the PLAYER global (0x1EA1D30) directly. Each copy has that hardcoded load
        // replaced with `j helper / nop`; the helper resolves the CURRENT enemy's slot and sets a1 = *(PtrTable + slot*4) —
        // the per-slot target (the live player global itself for an un-fooled slot, so the mod is not in that enemy's loop).
        // It then jumps back into the copy at the sceVu0CopyVector jal. Explicit-coordinate queries are untouched.
        private const int FnCopySize = 0xF0;    // both functions fit in this
        private const int HelperOff  = 0x100;   // helper sits after the copied body

        private static uint[] PerSlotTargetHelper(uint caveGuest, int jalOff) => new[]
        {
            0x8F889CE0u,                                     // lw   t0, -0x6320(gp)   ; NowMonstorUnit
            0x8D080090u,                                     // lw   t0, 0x90(t0)      ; current enemy slot
            0x00084080u,                                     // sll  t0, t0, 2         ; slot*4
            // PtrTable's FULL 32-bit address: lui sets the high half, ori (zero-extended, unlike addiu) the low half, so the
            // table may sit at any address, low half >= 0x8000 included. (docs/aggro-redirect.md, Lessons.)
            0x3C050000u | (CodeCaves.PtrTableGuest >> 16),      // lui  a1, HI(PtrTable)
            0x34A50000u | (CodeCaves.PtrTableGuest & 0xFFFFu),  // ori  a1, a1, LO(PtrTable)
            0x00A82821u,                                     // addu a1, a1, t0        ; &PtrTable[slot]
            0x8CA50000u,                                     // lw   a1, 0(a1)         ; a1 = per-slot target pointer
            MipsAsm.J(caveGuest + (uint)jalOff),   // j    cave+jalOff       ; back into the copy
            RuntimeCaveWriter.Nop,                           // (j delay)
        };

        /// <summary>Arm one of the two redirect caves. Returns true once armed (idempotent — safe to retry).</summary>
        private static bool ArmRedirectCave(string name, long vanillaFn, long cave, uint caveGuest, long dispatch, int detourOff, int jalOff)
            => RuntimeCaveWriter.ArmDispatchCave(
                name, vanillaFn, FnCopySize, cave, caveGuest, dispatch,
                pristine: new[] { (0, StbExternCmd.VanillaPrologue),            // addiu sp,-0x50
                                  (detourOff, StbExternCmd.VanillaPlayerLd) },  // lui v0,0x1ea (the player-addr load)
                detours:  new[] { (detourOff, new[] { MipsAsm.J(caveGuest + HelperOff),   // was lui v0,0x1ea
                                                      RuntimeCaveWriter.Nop }) },                   // was addiu a1,v0,0x1d30
                helperOff: HelperOff, helper: PerSlotTargetHelper(caveGuest, jalOff));
    }
}
