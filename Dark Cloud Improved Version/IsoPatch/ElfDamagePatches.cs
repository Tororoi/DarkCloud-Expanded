using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The damage-pipeline ELF patches: CheckDmg's guard gate (the crush and mask caves in front of the cat's bypass),
    /// every engine-made collision entry starting unmarked, Ungaga's no-drain hits, the auto-guard reaction, the item-bomb
    /// reaction word, a thrown gem's damage factor and Blizzard's ice immunity in the species table. Called in order from
    /// ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfDamagePatches
    {
        /// <summary>The GUARD CRUSH cave (tools/stubs/guard_crush.s, see DebugInfoCave.GuardCrush): CheckDmg's guard-window hook
        /// (main 0x1DAC78, ElfCatPatches.PatchCatGuardBypass's site) is re-aimed here; the cave goes on to the guard-mask cave, then the cat's.</summary>
        internal static void PatchGuardCrush(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugInfoCave.GuardCrush;
            byte[] b = Embedded("guardCrush.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x8F819DF0u || U32(b, 40) != MipsAsm.J(DebugInfoCave.GuardMask))
                throw new IOException($"guardCrush.bin malformed ({b.Length} B) or stale — reassemble its .s (it goes on to the guard-mask cave).");
            WriteBytes(fs, ElfOff, cave, b, DebugInfoCave.Follow, "guardCrush.bin runs into the follow cave.");
            uint site = ElfCatPatches.GuardBypassHookAddr, cur = RdU32(fs, ElfOff(site));
            if (cur != MipsAsm.J(DunCave.CatGuardBypass) && cur != MipsAsm.J(cave) && cur != MipsAsm.J(DebugIfCave.GuardCrushFirst) || RdU32(fs, ElfOff(site + 4)) != 0)
                throw new IOException($"The guard-window hook 0x{site:X} is not the cat cave's — PatchCatGuardBypass must run first.");
            WrU32(fs, ElfOff(site), MipsAsm.J(cave));
        }

        /// <summary>Every engine-made damage entry starts with no mark at +0x9C. `CCollisionData::Set` (0x1B57A0) fills an entry's fields
        /// but never +0x9C, so a mark planted on an entry (the cat's crush mark, which the cat cave stamps after its Set) stayed when
        /// the engine reused the entry — a plain pellet planted there next passed every guard. Set writes +0x20 twice in a row
        /// (`sw zero,0x20(a1)` then `sw a0,0x20(a1)` = 1.0), so the first, dead store is pointed at +0x9C instead.</summary>
        internal static void PatchSetClearsMark(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Site = 0x001B5858, Vanilla = 0xACA00020u, Patched = 0xACA00000u | (uint)CodeCaves.NoDrainMarkOff;   // sw zero,0x20(a1) → sw zero,0x9C(a1)
            ReplaceWords(fs, ElfOff, Site, new[] { Vanilla }, new[] { Patched },
                         _ => $"CCollisionData::Set at 0x{Site:X} is not vanilla (`sw zero,0x20(a1); sw a0,0x20(a1)`) — unmodified Dark Cloud (USA) ISO expected.",
                         (Site + 4, 0xACA40020u));
        }

        /// <summary>The GUARD MASK cave (tools/stubs/guard_mask.s, see DebugInfoCave.GuardMask): the guard gate's second link,
        /// entered from the guard-crush cave, leaving for the cat's guard-bypass cave.</summary>
        internal static void PatchGuardMask(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugInfoCave.GuardMask;
            byte[] b = Embedded("guardMask.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x3C0101DFu || U32(b, 44) != MipsAsm.J(DunCave.CatGuardBypass))
                throw new IOException($"guardMask.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, cave, b, DebugInfoCave.AutoGuardMatch, "guardMask.bin runs into the auto-guard cave.");
        }

        /// <summary>Ungaga's weapon-HP rebalance: CheckDmg's two drain calls (a landed hit 0x1DB388, a guarded one 0x1DAE94) go
        /// through a cave that bills nothing for an entry of Ungaga's (owner 4) that his charge EFFECT planted (+0x38 non-zero; his
        /// swings plant 0) or the mod marked (+0x9C == CodeCaves.NoDrainMark), and tail-jumps to SwordDmgCheck1 otherwise. The
        /// call's own a0 / f12 are untouched; t0–t2 are free across a call.</summary>
        internal static void PatchUngagaNoDrain(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SwordDmgCheck1 = 0x01DB9B30;
            uint mhi = CodeCaves.NoDrainMark >> 16;                // the marks' shared high half (NoDrainMark, CrushMark)
            uint[] Cave(uint entryReg) => new[]
            {
                0x8F880000u | 0x9DF0u,                      //  0 lw    t0,-0x6210(gp)          NowColData
                0x01000021u | (entryReg << 16) | (8u << 11),//  1 addu  t0,t0,entryReg          the entry
                0x8D090058u,                                //  2 lw    t1,0x58(t0)             its owner
                0x240A0004u,                                //  3 li    t2,4                    Ungaga
                0x152A0009u,                                //  4 bne   t1,t2,go (+9 → 14)
                0x00000000u,                                //  5   nop
                0x8D090038u,                                //  6 lw    t1,0x38(t0)             the class word: his charge effect's
                0x15200008u,                                //  7 bne   t1,zero,skip (+8 → 16)
                0x00000000u,                                //  8   nop
                0x8D090000u | (uint)CodeCaves.NoDrainMarkOff,//  9 lw   t1,0x9C(t0)             the mod's mark…
                0x00094C02u,                                // 10 srl   t1,t1,16               …its high half (SPIK's, CRIK's)
                0x340A0000u | mhi,                          // 11 ori   t2,zero,HI(mark)
                0x112A0003u,                                // 12 beq   t1,t2,skip (+3 → 16)
                0x00000000u,                                // 13   nop                         (no jump in a delay slot)
                MipsAsm.J(SwordDmgCheck1),                  // 14 go: j SwordDmgCheck1 (ra is the caller's)
                0x00000000u,                                // 15   nop
                0x03E00008u,                                // 16 skip: jr ra
                0x00000000u,                                // 17   nop
            };
            foreach (var (cave, site, reg) in new[] { (DebugIfCave.NoDrainLanded, 0x001DB388u, 22u /*s6*/), (DebugIfCave.NoDrainGuarded, 0x001DAE94u, 17u /*s1*/) })
            {
                WriteWords(fs, ElfOff, cave, Cave(reg), DebugIfCave.Host + DebugIfCave.HostSpan, "The no-drain caves do not fit their host (DebugInfomationIF).");
                ReplaceWord(fs, ElfOff, site, Jal(SwordDmgCheck1), Jal(cave),
                            _ => $"CheckDmg's drain call 0x{site:X} is not vanilla `jal SwordDmgCheck1` — unmodified Dark Cloud (USA) ISO expected.");
            }
        }

        /// <summary>AUTO-GUARD: a hit carrying reaction 5 is answered with a guard spark and otherwise IGNORED — the
        /// player's damage handler never processes it. The hook is at BtCheckDamageProc's CheckHitUser return, which is
        /// the only place the whole thing is gated on:
        /// <code>
        ///   0x1DBB0D8  jal  CheckHitUser
        ///   0x1DBB0E0  move s0,v0          →  jal AutoGuardMatch
        ///   0x1DBB0E4  addiu v0,zero,-1    →  move s0,v0     (the call's delay slot, where v0 is still the index)
        ///   0x1DBB0E8  beq  s0,v0 → skip everything
        /// </code>
        /// The cave returns v0 = −1 (what the displaced instruction set) and, for a reaction-5 entry, s0 = −1 as well,
        /// so the engine's own `beq` takes it straight to the end. It consumes the entry first and ticks
        /// <see cref="CodeCaves.AutoGuardSignal"/> with the position, which is how the mod knows to answer with rumble,
        /// a sound and a flinch — presentation is far easier to tune in C# than in a cave.
        ///
        /// ⚠ It has to intercept HERE rather than at the reaction dispatch. Before the handler looks at a reaction at
        /// all, it zeroes the player's action word (0x1DC4490, read by ToanKey_Play at 0x24146C) — so a hit that did
        /// nothing still cancelled whatever Toan was doing, which is a charge attack lost to a bomb that cannot hurt
        /// him. By the dispatch the old value is already gone.</summary>
        internal static void PatchAutoGuardMatch(FileStream fs, Func<uint, long> ElfOff)
        {
            HiLo(CodeCaves.AutoGuardSignalGuest, out uint sigHi, out uint sigLo);   // the signal block, lui/lw split
            uint[] words =
            {
                0x2401FFFF,   // addiu at,zero,-1
                0x10410018,   // beq   v0,at,done          nothing was hit
                0x00000000,   // nop
                0x8F889DF0,   // lw    t0,0x9DF0(gp)       NowColData
                0x00024880,   // sll   t1,v0,2
                0x01224821,   // addu  t1,t1,v0
                0x00094940,   // sll   t1,t1,5             index × 0xA0
                0x01094821,   // addu  t1,t0,t1            the entry
                0x8D2A004C,   // lw    t2,0x4C(t1)         its reaction
                0x240B0005,   // addiu t3,zero,5
                0x154B000F,   // bne   t2,t3,done          not ours: vanilla flow
                0x00000000,   // nop
                0x00025080,   // sll   t2,v0,2
                0x010A5021,   // addu  t2,t0,t2
                0xAD403C00,   // sw    zero,0x3C00(t2)     consume the entry
                0x3C0C0000 | sigHi,         // lui   t4,HI(AutoGuardSignal)   the signal block
                0x8D8D0000 | sigLo,         // lw    t5,LO(t4)                its counter
                0x25AD0001,                 // addiu t5,t5,1
                0xAD8D0000 | sigLo,         // sw    t5,LO(t4)                …ticked, for the mod to notice
                0x8D2D0000,                 // lw    t5,0x0(t1)
                0xAD8D0000 | (sigLo + 4),   // sw    t5,LO+4(t4)              …and where it happened
                0x8D2D0004,                 // lw    t5,0x4(t1)
                0xAD8D0000 | (sigLo + 8),   // sw    t5,LO+8(t4)
                0x8D2D0008,                 // lw    t5,0x8(t1)
                0xAD8D0000 | (sigLo + 12),  // sw    t5,LO+12(t4)
                0x2410FFFF,   // addiu s0,zero,-1          report: nothing was hit
                0x03E00008,   // jr    ra            done:
                0x2402FFFF,   // addiu v0,zero,-1          what the displaced instruction set
            };
            WriteWords(fs, ElfOff, DebugInfoCave.AutoGuardMatch, words, DebugInfoCave.Host + DebugInfoCave.HostSpan, "The auto-guard cave does not fit its host (DebugInfomationDraw).");
        }

        /// <summary>Item-bomb explosions take their hit REACTION from data. SetBombEffect (0x1D5940) builds its
        /// collision entry with the reaction baked in as the eighth argument:
        /// <code>
        ///   0x1D5A14  li   t1,0x3        ← the unguardable knockdown
        ///   0x1D5A20  jal  CCollisionData::Set
        ///   0x1D5A24  nop                ← the call's delay slot, unused
        /// </code>
        /// The literal becomes a load of <see cref="CodeCaves.BombReaction"/>, split across the `li` slot and the
        /// delay slot — which runs BEFORE the call, so t1 is loaded in time and no code has to move:
        /// <code>
        ///   lui t1,HI(BombReaction)  /  jal Set  /  lw t1,LO(BombReaction)(t1)
        /// </code>
        /// Bombs are the one explosion source that does not read a shot config, so this is what lets an ability
        /// cover chest traps, thrown bombs and Halloween's pumpkin alongside the self-destructs.</summary>
        internal static void PatchBombReaction(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint LiAddr = 0x001D5A14, SlotAddr = 0x001D5A24;
            const uint VanillaLi = 0x24090003, VanillaSlot = 0x00000000;   // li t1,3 ; nop
            HiLo(CodeCaves.BombReactionGuest, out uint hi, out uint lo);  // lw's offset is SIGNED
            uint wantLui = 0x3C090000u | hi;                               // lui t1,hi
            uint wantLw  = 0x8D290000u | lo;                               // lw  t1,lo(t1)
            uint gotLi = RdU32(fs, ElfOff(LiAddr)), gotSlot = RdU32(fs, ElfOff(SlotAddr));
            if (gotLi == wantLui && gotSlot == wantLw) return;             // idempotent re-run
            if (gotLi != VanillaLi || gotSlot != VanillaSlot)
                throw new IOException($"Item-bomb reaction site 0x{LiAddr:X} is not vanilla `li t1,3` + nop " +
                                      $"(got 0x{gotLi:X8}/0x{gotSlot:X8}) — is this an unmodified Dark Cloud (USA) ISO?");
            WrU32(fs, ElfOff(LiAddr),   wantLui);
            WrU32(fs, ElfOff(SlotAddr), wantLw);
        }

        /// <summary>A thrown gem's burst damage under a mod factor. CMainItemModel::Step, landing a thrown elemental gem (items
        /// 161–165), computes 30 × (selectMapNo + 1), moves it to a1 and calls <c>SetDmg__12CSHOT_EFFECT(slot, damage)</c> on the
        /// gem's Maseki burst (0x1D52D4; the Holy Water burst's call at 0x1D51F0 is left alone). That call becomes `jal GemDamage`:
        /// <code>
        ///   lui t0,HI / lw t0,LO(GemDamageFactor) / beq t0,zero,go / nop / mult a1,t0 / mflo a1 / go: j SetDmg / nop
        /// </code>
        /// a1 multiplied by the factor when the word is set; a zero word — fresh memory — is vanilla.</summary>
        internal static void PatchGemDamage(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x001D52D4, SetDmg = 0x001AE310;
            uint cave = DebugIfCave.GemDamage;
            HiLo(CodeCaves.GemDamageFactorGuest, out uint whi, out uint wlo);
            uint[] words =
            {
                0x3C080000u | whi,                              // 0 lui   t0,HI(GemDamageFactor)
                0x8D080000u | wlo,                              // 1 lw    t0,LO(t0)
                0x11000003u,                                    // 2 beq   t0,zero,go (+3 → index 6)
                0x00000000u,                                    // 3   nop
                0x00A80018u,                                    // 4 mult  a1,t0                         the damage × the factor
                0x00002812u,                                    // 5 mflo  a1
                J(SetDmg),                                      // 6 go: j SetDmg__12CSHOT_EFFECT         ra still the step's
                0x00000000u,                                    // 7   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugIfCave.Host + DebugIfCave.HostSpan, "The gem-damage cave does not fit its host (DebugInfomationIF).");
            uint cur = RdU32(fs, ElfOff(HookAddr)), ours = Jal(cave);
            if (cur != Jal(SetDmg) && cur != ours)
                throw new IOException($"Gem damage site 0x{HookAddr:X} is not vanilla `jal SetDmg__12CSHOT_EFFECT` — unmodified Dark Cloud (USA) ISO expected.");
            if (RdU32(fs, ElfOff(HookAddr + 4)) != 0 || RdU32(fs, ElfOff(HookAddr - 4)) != 0x70402E28u)   // its delay slot; `moveq a1,v0`: the damage into a1
                throw new IOException("CMainItemModel::Step is not laid out as expected around the gem's SetDmg.");
            WrU32(fs, ElfOff(HookAddr), ours);
        }

        // ── Blizzard: immune to ice ──────────────────────────────────────────────────────────────────────
        // The enemy species table is static ELF data (EnemySpeciesTable @0x27FB00, 0x9C per record; element resistances
        // are signed shorts, 0 = immune, 100 = neutral). Blizzard (row 57, "e65a") ships ice-neutral; the user wants it ice-immune
        // like Ice Gemron. EnemyData.cs carries the patched value so the mod's tables agree with the disc.
        internal static void PatchBlizzardIceImmunity(FileStream fs, Func<uint, long> ElfOff)
        {
            const int Row = 57;                                                                     // EnemyData.Blizzard.TableIndex
            long rec = ElfOff((uint)EnemySpeciesTable.RecordAddress(Row));
            long ice = rec + EnemySpeciesTable.IceRes;
            ushort cur = U16(Rd(fs, ice, 2), 0);
            bool vanilla = cur == 100, ours = cur == 0;
            if (RdU32(fs, rec) != 0x61353665u /* "e65a" */ || !(vanilla || ours)
                || U16(Rd(fs, rec + EnemySpeciesTable.FireRes, 2), 0) != 100 || U16(Rd(fs, rec + EnemySpeciesTable.ThunderRes, 2), 0) != 140)
                throw new IOException($"Species row {Row} is not Blizzard as shipped (\"e65a\", fire 100 / ice 100 / thunder 140) — unmodified Dark Cloud (USA) ISO expected.");
            Wr(fs, ice, new byte[] { 0, 0 });                                                       // IceRes = 0: immune
        }
    }
}
