using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Confuse ability's ELF patches (docs/confuse-ability.md): its on-hit roll in DebugItemGetKey's body, its name,
    /// icon and bar in the status window, and the second main-character effect instance (its stars) stepped and drawn beside the
    /// live one. Called in order from ElfPatches.ElfPatchAndCrc (the dead hosts are claimed first by ElfDeadFunctionPatches).</summary>
    internal static class ElfConfusePatches
    {
        /// <summary>The Confuse ability's on-hit roll (tools/stubs/confuse_proc.s, docs/confuse-ability.md): the cave written into
        /// DebugItemGetKey's body (DebugItemCave, claimed by ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts), and CheckDmg's
        /// meeting point after the Stop roll (0x1DBAA4, `lw v1,0x90(s5)`) jumped to it.</summary>
        internal static void PatchConfuseProc(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Hook = 0x001DBAA4, HookVanilla = 0x8EA30090u, HookSlot = 0x00031080u;   // lw v1,0x90(s5) ; sll v0,v1,2
            uint cave = DebugItemCave.ConfuseProc;
            byte[] b = Embedded("confuseProc.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x8EA30090u || U32(b, b.Length - 8) != MipsAsm.J(0x001DBAA8))
                throw new IOException($"confuseProc.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, cave, b, DebugItemCave.StarsStep, "confuseProc.bin runs into the stars step cave.");
            uint cur = RdU32(fs, ElfOff(Hook));
            if (cur != HookVanilla && cur != MipsAsm.J(cave) || RdU32(fs, ElfOff(Hook + 4)) != HookSlot)
                throw new IOException($"CheckDmg's post-Stop meeting point 0x{Hook:X} is not vanilla (0x{cur:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(Hook), MipsAsm.J(cave));
        }

        /// <summary>The CONFUSE weapon ability's ELF side (docs/confuse-ability.md; the name and icon are ConfuseAbilityBakes'): ability bit
        /// 14 (0x4000, Effect2 bit 0x40) shown in the status window — the SPECIAL list's names (MenuClsMes::NowWeaponStatus) and its
        /// icons and bars (MenuClsMes::Draw1), and the icon grid (WeaponOptionStatusDraw) walk bits 1..13 (`slti …,0xE`): raised to 14
        /// (0xF); the bar's colour (IsWeaponOptionGoodOrBad) says good (blue) for it; the list's name lookup (bit + 0x45)
        /// goes through tools/stubs/confuse_name.s so bit 14 reads message 0x45, not 0x53 ("Slot 1"); and Babel's Spear's
        /// template carries the bit natively (a SynthSphere of it carries the ability to other weapons as any ability does).</summary>
        internal static void PatchConfuseAbility(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugIfCave.ConfuseName;
            byte[] b = Embedded("confuseName.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x24E40045u || U32(b, b.Length - 8) != MipsAsm.J(0x0020B8B0))
                throw new IOException($"confuseName.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (cave + (uint)b.Length > DebugIfCave.Host + DebugIfCave.HostSpan)
                throw new IOException("confuseName.bin overruns DebugInfomationIF's span.");
            if (RdU32(fs, ElfOff(0x0020B8AC)) != 0x8E08001Cu) throw new IOException("NowWeaponStatus's hook delay slot (0x20B8AC) is not `lw t0,0x1C(s0)`.");
            WriteBytes(fs, ElfOff, cave, b);
            ReplaceWord(fs, ElfOff, 0x0020B8A8, 0x24E40045u, MipsAsm.J(cave), "NowWeaponStatus's name lookup");
            ReplaceWord(fs, ElfOff, 0x0020B8E4, 0x28E1000Eu, 0x28E1000Fu, "NowWeaponStatus's ability loop bound");
            ReplaceWord(fs, ElfOff, 0x0020F9D4, 0x2AA1000Eu, 0x2AA1000Fu, "WeaponOptionStatusDraw's ability loop bound");
            ReplaceWord(fs, ElfOff, 0x0020BDC4, 0x2A21000Eu, 0x2A21000Fu, "MenuClsMes::Draw1's ability loop bound (the SPECIAL list's icons and bars)");
            // IsWeaponOptionGoodOrBad (0x20F770): the bar's colour (1 = good, blue; 0 = bad, red) from a 14-entry table of shorts
            // (0x293C60) it copies to the stack — bit 14 read past the copy. Rewritten in place: bit 14 is good, every other bit
            // reads the same static table directly.
            uint[] goodOrBad =
            {
                0x00041040u,   // sll   v0,a0,1
                0x2401000Eu,   // addiu at,zero,14
                0x10810004u,   // beq   a0,at,good
                0x3C030029u,   // lui   v1,0x0029            (delay slot)
                0x00621821u,   // addu  v1,v1,v0
                0x03E00008u,   // jr    ra
                0x84623C60u,   // lh    v0,0x3C60(v1)        (delay slot: the table at 0x293C60)
                0x03E00008u,   // good: jr ra
                0x24020001u,   // addiu v0,zero,1           (delay slot)
                0, 0, 0, 0, 0, 0, 0,
            };
            uint g0 = RdU32(fs, ElfOff(0x0020F770));
            if (g0 != 0x27BDFFE0u && g0 != goodOrBad[0] || RdU32(fs, ElfOff(0x0020F7B0)) != 0x27BDFFF0u)
                throw new IOException($"IsWeaponOptionGoodOrBad at 0x20F770 is not vanilla (0x{g0:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WriteWords(fs, ElfOff, 0x0020F770, goodOrBad);
            // Babel's Spear: WeaponList[357 − 257] Effect2 |= 0x40 (bit 0x4000 of the live ability word)
            uint e2 = (uint)(WeaponList.NativeBase + (Items.babelsspear - WeaponList.FirstItemId) * WeaponList.Stride + WeaponList.Effect2);
            uint word = e2 & ~3u; int sh = (int)(e2 & 3) * 8;
            uint w = RdU32(fs, ElfOff(word));
            WrU32(fs, ElfOff(word), w | (0x40u << sh));
        }

        /// <summary>The second main-character effect instance stepped and drawn beside the live one while
        /// CodeCaves.SecondEffectLive is set: two caves in DebugInfomationIF's body, each `Fn(a0 = the live instance, as the
        /// site loaded it)` then, flag set, `Fn(0x01E97BC0)`. DunPatches points the dungeon loop's two `jal` sites at them.</summary>
        internal static void PatchSecondEffect(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Step = 0x001AC180, Draw = 0x001ABF20, Second = 0x01E97BC0;
            HiLo(CodeCaves.SecondEffectLiveGuest, out uint fhi, out uint flo);
            uint stars = 0;                                     // the continuation the cave being written jumps to
            uint[] Cave(uint fn) => new[]
            {
                0x27BDFFF0u,                                    //  0 addiu sp,sp,-0x10
                0xAFBF0000u,                                    //  1 sw    ra,0(sp)
                Jal(fn),                                        //  2 jal   Fn                (a0 = the live instance, as the site loaded it)
                0x00000000u,                                    //  3   nop
                0x3C080000u | fhi,                              //  4 lui   t0,HI(SecondEffectLive)
                0x8D080000u | flo,                              //  5 lw    t0,LO(t0)
                0x11000005u,                                    //  6 beq   t0,zero,done      (+5 → index 12)
                0x00000000u,                                    //  7   nop
                0x3C040000u | (Second >> 16),                   //  8 lui   a0,HI(the second instance)
                0x34840000u | (Second & 0xFFFFu),               //  9 ori   a0,a0,LO
                Jal(fn),                                        // 10 jal   Fn
                0x00000000u,                                    // 11   nop
                J(stars),                                       // 12 done: j StarsStep/StarsDraw (the resident stars, then the epilogue)
                0x00000000u,                                    // 13   nop
                0x00000000u,                                    // 14
                0x00000000u,                                    // 15
            };
            foreach (var (cave, fn, starsCave) in new[] { (DebugIfCave.SecondEffectStep, Step, DebugItemCave.StarsStep), (DebugIfCave.SecondEffectDraw, Draw, DebugItemCave.StarsDraw) })
            {
                stars = starsCave;
                WriteWords(fs, ElfOff, cave, Cave(fn), DebugIfCave.Host + DebugIfCave.HostSpan, "The second-effect caves do not fit their host (DebugInfomationIF).");
                uint[] tail = StarsTail(fn, construct: starsCave == DebugItemCave.StarsStep);
                uint limit = starsCave == DebugItemCave.StarsStep ? DebugItemCave.StarsDraw : DebugItemCave.DrawHost;
                WriteWords(fs, ElfOff, starsCave, tail, limit, "The stars step/draw caves overlap.");
            }
        }

        /// <summary>The second-effect caves' continuation: the RESIDENT STARS instance (CodeCaves.StarsGate +0xC) stepped / drawn too,
        /// behind CodeCaves.StarsGate — live, its region's signature ("BSHT" + the mark) intact, the monster pool at or past the
        /// mark — then the caves' epilogue (their frame: ra at 0(sp), 0x10 B). The step's copy first constructs an instance
        /// StarsLane posted (CodeCaves.StarsConstruct: `__ct__12CSHOT_EFFECT`, then the word cleared). In DebugItemGetKey's dead
        /// body (ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts makes it return at once, before any cave patch).</summary>
        private static uint[] StarsTail(uint fn, bool construct)
        {
            const uint Ctor = 0x00143680;                                     // __ct__12CSHOT_EFFECTFv
            HiLo(CodeCaves.StarsGateGuest, out uint ghi, out uint glo);
            uint Lo(int off) => (glo + (uint)off) & 0xFFFFu;
            HiLo(CodeCaves.StarsConstructGuest, out uint chi, out uint clo);
            var w = new List<uint>();
            if (construct)
            {
                w.Add(0x3C080000u | chi);                                     // lui   t0,HI(StarsConstruct)
                w.Add(0x8D040000u | clo);                                     // lw    a0,construct
                w.Add(0x10800000u | 5);                                       // beq   a0,zero,+5  (past the clear)
                w.Add(0u);                                                    //   nop
                w.Add(Jal(Ctor));                                             // jal   __ct__12CSHOT_EFFECT(instance)
                w.Add(0u);                                                    //   nop
                w.Add(0x3C080000u | chi);                                     // lui   t0,HI(StarsConstruct)
                w.Add(0xAD000000u | clo);                                     // sw    zero,construct   (constructed)
            }
            int b = w.Count, done = b + 24;
            uint Br(uint op, int at) => op | (uint)((done - (at + 1)) & 0xFFFF);
            w.AddRange(new[]
            {
                0x3C080000u | ghi,                              //  0 lui   t0,HI(StarsGate)
                0x8D090000u | Lo(CodeCaves.StarsGateLive),      //  1 lw    t1,live
                Br(0x11200000u, b + 2),                         //  2 beq   t1,zero,done
                0u,                                             //  3   nop
                0x8D090000u | Lo(CodeCaves.StarsGateBase),      //  4 lw    t1,base
                0x8D2AFFF0u,                                    //  5 lw    t2,-0x10(t1)      the region's signature
                0x3C0B5448u,                                    //  6 lui   t3,0x5448
                0x356B5342u,                                    //  7 ori   t3,t3,0x5342      "BSHT"
                Br(0x154B0000u, b + 8),                         //  8 bne   t2,t3,done
                0u,                                             //  9   nop
                0x8D2AFFF4u,                                    // 10 lw    t2,-0xC(t1)       its mark
                0x8D0B0000u | Lo(CodeCaves.StarsGateMark),      // 11 lw    t3,mark
                Br(0x154B0000u, b + 12),                        // 12 bne   t2,t3,done
                0u,                                             // 13   nop
                0x3C1801F0u,                                    // 14 lui   t8,0x01F0
                0x371866D0u,                                    // 15 ori   t8,t8,0x66D0      the monster pool
                0x8F190008u,                                    // 16 lw    t9,8(t8)          its used counter
                0x032B682Au,                                    // 17 slt   t5,t9,t3
                Br(0x15A00000u, b + 18),                        // 18 bne   t5,zero,done      rewound below the mark: stale
                0u,                                             // 19   nop
                0x8D040000u | Lo(CodeCaves.StarsGateInstance),  // 20 lw    a0,instance       the stars instance (t0 still the gate's HI)
                0u,                                             // 21 nop
                Jal(fn),                                        // 22 jal   Fn
                0u,                                             // 23   nop
                0x8FBF0000u,                                    // 24 done: lw ra,0(sp)
                0x27BD0010u,                                    // 25 addiu sp,sp,0x10
                0x03E00008u,                                    // 26 jr    ra
                0u,                                             // 27   nop
            });
            return w.ToArray();
        }
    }
}
