using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The once-a-frame chain of caves hung off the dungeon camera pass's epilogue (its `jr ra` becomes a jump into the
    /// camera pin, DunPatches): camera pin → blade fall → fall drive → follow → blade spin → WHP bill → call request, each
    /// leaving for the next and the last returning through the pass's ra. Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfFrameChainPatches
    {
        /// <summary>The CAMERA PIN cave (see DebugInfoCave.CameraPin). Entered by `j` from the dungeon camera
        /// pass's epilogue in place of its `jr ra` (the delay-slot nop and the `addiu sp` before it are the pass's own),
        /// so it runs after the pass has done everything else to the camera, with ra the pass's return and the stack
        /// already unwound. While the pin flag is clear it returns at once. Set, it holds the camera's ABSOLUTE HEIGHT:
        /// its height field is the height above its follow point R (Step renders the camera at R + dist·(sin, cos)(angle),
        /// up by height), so height = P.y − R.y every frame keeps the camera at the pinned world height P.y while the
        /// follow point rises and falls with Toan. Distance and angle stay the engine's — the camera keeps its vanilla
        /// place around him. Caller-saved registers only (t0, t1, a0, f3, f6); no calls, no frame.</summary>
        internal static void PatchCameraPin(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugInfoCave.CameraPin;
            HiLo(CodeCaves.CameraPinGuest, out uint hi, out uint lo);                // the loads' offsets are signed
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(pin)
                0x8D090000u | Lo(CodeCaves.CameraPinFlag),          //  1 lw    t1,flag(t0)
                0x11200000u | 6,                                    //  2 beq   t1,zero,ret (+6 → index 9)
                0x00000000u,                                        //  3   nop
                0x8F849CA8u,                                        //  4 lw    a0,-0x6358(gp)         the camera (NowCamera)
                0xC48302C4u,                                        //  5 lwc1  f3,0x2C4(a0)          R.y
                0xC5060000u | Lo(4),                                //  6 lwc1  f6,P.y(t0)
                0x46033181u,                                        //  7 sub.s f6,f6,f3              height = P.y − R.y
                0xE48602D4u,                                        //  8 swc1  f6,0x2D4(a0)
                MipsAsm.J(DebugInfoCave.BladeFall),       //  9 ret: j BladeFall (which returns through ra)
                0x00000000u,                                        // 10   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugInfoCave.Host + DebugInfoCave.HostSpan, "The camera-pin cave does not fit its host (DebugInfomationDraw).");
        }

        /// <summary>The BLADE cave (see DebugInfoCave.BladeFall): the tail of the camera-pin chain, once a frame.
        /// Flag 1, FALLING: vy += g; y −= vy; if y ≤ stop then y = stop and the flag becomes 2; y and vy stored back, and y
        /// written to the blade copy's slot height. Flag 3, FOLLOWING: the unit position at the guest pointer in the words
        /// gives the copy's x and z, and its height plus the y word (a height OVER the unit) the copy's height — the hover
        /// riding an enemy the engine moves, up and down as well, at the engine's own frame. Caller-saved registers only
        /// (t0..t3, f0..f3); no calls.</summary>
        internal static void PatchBladeFall(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugInfoCave.BladeFall;
            HiLo(CodeCaves.BladeFallGuest, out uint hi, out uint lo);                // signed offsets
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint slotPos = (uint)(DungeonCharaDraw.CharaArray - 0x20000000L) + (uint)(BladeProp.Slot * DungeonCharaDraw.CharaStride) + (uint)CCharacter.CharPos;
            HiLo(slotPos, out uint shi, out uint slo); uint SLo(int o) => (slo + (uint)o) & 0xFFFFu;   // x +0, y +4, z +8 (all past the sign bit alike)
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(fall)
                0x8D090000u | Lo(CodeCaves.BladeFallFlag),          //  1 lw    t1,flag(t0)
                0x240A0001u,                                        //  2 li    t2,1
                0x152A0000u | 19,                                   //  3 bne   t1,t2,follow (+19 → index 23)
                0x00000000u,                                        //  4   nop
                0xC5000000u | Lo(CodeCaves.BladeFallY),             //  5 lwc1  f0,y(t0)
                0xC5010000u | Lo(CodeCaves.BladeFallVy),            //  6 lwc1  f1,vy(t0)
                0xC5020000u | Lo(CodeCaves.BladeFallG),             //  7 lwc1  f2,g(t0)
                0xC5030000u | Lo(CodeCaves.BladeFallStop),          //  8 lwc1  f3,stop(t0)
                0x46020840u,                                        //  9 add.s f1,f1,f2               vy += g
                0x46010001u,                                        // 10 sub.s f0,f0,f1               y −= vy
                0x46030036u,                                        // 11 c.le.s f0,f3                 y ≤ stop ?  (⚠ EE cond code 0x36 — the MIPS 0x3E "LE" is not one the R5900 FPU has, and read as a coin toss)
                0x00000000u,                                        // 12 nop
                0x45000000u | 3,                                    // 13 bc1f  store (+3 → index 17)
                0x240A0002u,                                        // 14   li  t2,2                   (both paths; only stored below)
                0x46001806u,                                        // 15 mov.s f0,f3                  y = stop
                0xAD0A0000u | Lo(CodeCaves.BladeFallFlag),          // 16 sw    t2,flag(t0)            landed
                0xE5000000u | Lo(CodeCaves.BladeFallY),             // 17 store: swc1 f0,y(t0)
                0xE5010000u | Lo(CodeCaves.BladeFallVy),            // 18 swc1  f1,vy(t0)
                0x3C0B0000u | shi,                                  // 19 lui   t3,HI(slot pos)
                0xE5600000u | SLo(4),                               // 20 swc1  f0,y(t3)               the copy's height, this frame
                MipsAsm.J(DebugIfCave.FallDrive),         // 21 j     FallDrive (mode 4), then BladeSpin and the WHP bill
                0x00000000u,                                        // 22   nop
                0x240A0003u,                                        // 23 follow: li t2,3
                0x152A0000u | 15,                                   // 24 bne   t1,t2,ret (+15 → index 40)
                0x00000000u,                                        // 25   nop
                0x8D0A0000u | Lo(CodeCaves.BladeFallUnit),          // 26 lw    t2,unit(t0)            the followed unit's position (guest)
                0xC5400000u,                                        // 27 lwc1  f0,0x0(t2)             its x
                0xC5410008u,                                        // 28 lwc1  f1,0x8(t2)             its z
                0xC5020000u | Lo(CodeCaves.BladeFallY),             // 29 lwc1  f2,y(t0)               the height OVER the unit
                0xC5430004u,                                        // 30 lwc1  f3,0x4(t2)             the unit's own height (a flyer's rises)
                0x46031080u,                                        // 31 add.s f2,f2,f3
                0xC5030000u | Lo(CodeCaves.BladeFallOffX),          // 32 lwc1  f3,offx(t0)            an x/z offset from the unit (0 over an enemy;
                0x46030000u,                                        // 33 add.s f0,f0,f3                ahead of Toan for the charge blade)
                0xC5030000u | Lo(CodeCaves.BladeFallOffZ),          // 34 lwc1  f3,offz(t0)
                0x46030840u,                                        // 35 add.s f1,f1,f3
                0x3C0B0000u | shi,                                  // 36 lui   t3,HI(slot pos)
                0xE5600000u | SLo(0),                               // 37 swc1  f0,x(t3)
                0xE5620000u | SLo(4),                               // 38 swc1  f2,y(t3)
                0xE5610000u | SLo(8),                               // 39 swc1  f1,z(t3)
                MipsAsm.J(DebugIfCave.FallDrive),         // 40 ret: j FallDrive (mode 4), then BladeSpin and the WHP bill
                0x00000000u,                                        // 41   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugInfoCave.Host + DebugInfoCave.HostSpan, "The blade-fall cave does not fit its host (DebugInfomationDraw).");
        }

        /// <summary>The blade fall's MODE 4 (tools/stubs/fall_drive.s, see DebugIfCave.FallDrive): entered from both exits of
        /// the blade-fall cave, acting only on flag 4, and leaving for the follow cave (then the blade-spin cave).</summary>
        internal static void PatchFallDrive(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugIfCave.FallDrive;
            byte[] b = Embedded("fallDrive.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x3C0801FBu || U32(b, b.Length - 8) != MipsAsm.J(DebugInfoCave.Follow))
                throw new IOException($"fallDrive.bin malformed ({b.Length} B) or stale — reassemble its .s (it opens `lui t0,0x01FB` and leaves for the follow cave).");
            WriteBytes(fs, ElfOff, cave, b, DebugIfCave.Host + DebugIfCave.HostSpan, "fallDrive.bin overruns DebugInfomationIF's span.");
        }

        /// <summary>The FOLLOW cave (tools/stubs/follow.s, see DebugInfoCave.Follow): CodeCaves.FollowTable walked every
        /// frame, entered from the fall-drive cave's exit, leaving for the blade-spin cave.</summary>
        internal static void PatchFollow(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugInfoCave.Follow;
            byte[] b = Embedded("follow.bin");
            if (b.Length % 4 != 0 || U32(b, 0) != 0x3C0801FBu || U32(b, b.Length - 8) != MipsAsm.J(DebugIfCave.BladeSpin))
                throw new IOException($"follow.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, cave, b, DebugInfoCave.Host + DebugInfoCave.HostSpan, "follow.bin overruns DebugInfomationDraw's span.");
        }

        /// <summary>The BLADE SPIN cave (DebugIfCave.BladeSpin): the blade-fall cave's two exits land here, once a dungeon
        /// frame. CodeCaves.BladeSpin non-zero → chara slot 3's yaw (+0x64) += it, wrapped to ±π (the engine's angle-to-matrix
        /// diverges past that); then on to the WHP bill, the chain's tail. f0/f2–f5 and t0/t1/t3–t5 are scratch here as in the
        /// caves before it.</summary>
        internal static void PatchBladeSpin(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugIfCave.BladeSpin;
            HiLo(CodeCaves.BladeSpinGuest, out uint hi, out uint lo);
            uint yaw = (uint)(DungeonCharaDraw.CharaArray - 0x20000000L) + (uint)(BladeProp.Slot * DungeonCharaDraw.CharaStride) + (uint)CCharacter.CharRot + 4;
            HiLo(yaw, out uint yhi, out uint ylo);
            uint[] words =
            {
                0x3C080000u | hi,            //  0 lui   t0,HI(spin)
                0xC5000000u | lo,            //  1 lwc1  f0,LO(t0)              the radians a frame
                0x44090000u,                 //  2 mfc1  t1,f0
                0x11200016u,                 //  3 beq   t1,zero,out (+22 → index 26)
                0x00000000u,                 //  4   nop
                0x3C0B0000u | yhi,           //  5 lui   t3,HI(slot 3 yaw)
                0xC5620000u | ylo,           //  6 lwc1  f2,LO(t3)              the yaw
                0x46001080u,                 //  7 add.s f2,f2,f0
                0x3C0C4049u,                 //  8 lui   t4,0x4049
                0x358C0FDBu,                 //  9 ori   t4,t4,0x0FDB           π
                0x448C1800u,                 // 10 mtc1  t4,f3
                0x3C0D40C9u,                 // 11 lui   t5,0x40C9
                0x35AD0FDBu,                 // 12 ori   t5,t5,0x0FDB           2π
                0x448D2000u,                 // 13 mtc1  t5,f4
                0x46021834u,                 // 14 c.lt.s f3,f2                 π < yaw ?
                0x00000000u,                 // 15   nop
                0x45000002u,                 // 16 bc1f  +2 (→ index 19)
                0x00000000u,                 // 17   nop
                0x46041081u,                 // 18 sub.s f2,f2,f4               yaw −= 2π
                0x46001947u,                 // 19 neg.s f5,f3                  −π
                0x46051034u,                 // 20 c.lt.s f2,f5                 yaw < −π ?
                0x00000000u,                 // 21   nop
                0x45000002u,                 // 22 bc1f  +2 (→ index 25)
                0x00000000u,                 // 23   nop
                0x46041080u,                 // 24 add.s f2,f2,f4               yaw += 2π
                0xE5620000u | ylo,           // 25 store: swc1 f2,LO(t3)
                MipsAsm.J(DebugInfoCave.WhpBill),   // 26 out: j WhpBill (the chain's tail, which returns through ra)
                0x00000000u,                 // 27   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugIfCave.Host + DebugIfCave.HostSpan, "The blade-spin cave does not fit its host (DebugInfomationIF).");
        }

        /// <summary>The WHP-BILL cave (see CodeCaves.WhpBill): the tail of the camera-pin chain, once a dungeon frame. A bill
        /// the mod has posted — the magic in place and a non-zero factor — is taken by the engine itself: the factor is
        /// zeroed (consumed before the call) and SwordDmgCheck1(factor, 0) is called, the very routine a landed hit calls,
        /// so the drain, its warnings, the Auto Repair Powder and the break are all the engine's own. The call is made
        /// from the camera pass's epilogue: ra is kept on a 16-byte frame of our own, and the pass returns nothing, so the
        /// caller-saved registers the call spends are nobody's. t0–t2, f1 and f12 are scratch; ends by returning through
        /// the pass's own ra.</summary>
        internal static void PatchWhpBill(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SwordDmgCheck1 = 0x01DB9B30;                                   // dun: SwordDmgCheck1__Ffi (float factor in f12, int whpCost in a0)
            uint cave = DebugInfoCave.WhpBill;
            HiLo(CodeCaves.WhpBillGuest, out uint hi, out uint lo);                  // signed offsets
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint magic = CodeCaves.WhpBillMagicValue;
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(bill)
                0x8D090000u | Lo(CodeCaves.WhpBillMagic),           //  1 lw    t1,magic(t0)
                0x3C0A0000u | (magic >> 16),                        //  2 lui   t2,HI(magic value)
                0x354A0000u | (magic & 0xFFFFu),                    //  3 ori   t2,t2,LO(magic value)
                0x152A0000u | 15,                                   //  4 bne   t1,t2,ret (+15 → index 20)   no magic: nothing is ours here
                0x00000000u,                                        //  5   nop
                0xC50C0000u | Lo(CodeCaves.WhpBillFactor),          //  6 lwc1  f12,factor(t0)              the bill, as SwordDmgCheck1's factor
                0x44800800u,                                        //  7 mtc1  zero,f1
                0x00000000u,                                        //  8 nop
                0x46016032u,                                        //  9 c.eq.s f12,f1                     nothing posted?
                0x00000000u,                                        // 10 nop
                0x45010000u | 8,                                    // 11 bc1t  ret (+8 → index 20)
                0x00000000u,                                        // 12   nop
                0x27BDFFF0u,                                        // 13 addiu sp,sp,-0x10                a frame of our own for ra
                0xAFBF0000u,                                        // 14 sw    ra,0x0(sp)
                0xAD000000u | Lo(CodeCaves.WhpBillFactor),          // 15 sw    zero,factor(t0)            consumed before the call
                MipsAsm.Jal(SwordDmgCheck1),                        // 16 jal   SwordDmgCheck1
                0x00002021u,                                        // 17   addu a0,zero,zero              whpCost 0 (no monster's)
                0x8FBF0000u,                                        // 18 lw    ra,0x0(sp)
                0x27BD0010u,                                        // 19 addiu sp,sp,0x10
                MipsAsm.J(DebugIfCave.CallRequest),       // 20 ret: j CallRequest (the chain's tail, which returns through ra)
                0x00000000u,                                        // 21   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugInfoCave.Host + DebugInfoCave.HostSpan, "The WHP-bill cave does not fit its host (DebugInfomationDraw).");
        }

        /// <summary>The NATIVE CALL cave (see CodeCaves.CallRequest): the tail of the camera-pin chain. A request the mod has
        /// posted — the magic in place — is consumed (the magic cleared first) and the function called with its six integer
        /// arguments and f12 from the words; v0 is stored and Done raised. ra is kept on a frame of our own; the hooked
        /// camera pass returns nothing, so the caller-saved registers the call spends are nobody's.</summary>
        internal static void PatchCallRequest(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = DebugIfCave.CallRequest;
            HiLo(CodeCaves.CallRequestGuest, out uint hi, out uint lo);              // signed offsets
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint magic = CodeCaves.CallMagicValue;
            uint[] words =
            {
                0x3C180000u | hi,                                   //  0 lui   t8,HI(words)
                0x8F190000u | Lo(CodeCaves.CallMagic),              //  1 lw    t9,magic(t8)
                0x3C0F0000u | (magic >> 16),                        //  2 lui   t7,HI(magic value)
                0x35EF0000u | (magic & 0xFFFFu),                    //  3 ori   t7,t7,LO(magic value)
                0x172F0000u | 22,                                   //  4 bne   t9,t7,ret (+22 → index 27)    nothing posted
                0x00000000u,                                        //  5   nop
                0x27BDFFE0u,                                        //  6 addiu sp,sp,-0x20
                0xAFBF0010u,                                        //  7 sw    ra,0x10(sp)
                0xAFB00014u,                                        //  8 sw    s0,0x14(sp)
                0x03008025u,                                        //  9 or    s0,t8,zero
                0xAE000000u | Lo(CodeCaves.CallMagic),              // 10 sw    zero,magic(s0)               consumed before the call
                0x8E190000u | Lo(CodeCaves.CallFunc),               // 11 lw    t9,func(s0)
                0x8E040000u | Lo(CodeCaves.CallA0),                 // 12 lw    a0,a0(s0)
                0x8E050000u | Lo(CodeCaves.CallA1),                 // 13 lw    a1,a1(s0)
                0x8E060000u | Lo(CodeCaves.CallA2),                 // 14 lw    a2,a2(s0)
                0x8E070000u | Lo(CodeCaves.CallA3),                 // 15 lw    a3,a3(s0)
                0x8E080000u | Lo(CodeCaves.CallA4),                 // 16 lw    t0,a4(s0)                    the fifth and sixth, as the EE ABI passes them
                0x8E090000u | Lo(CodeCaves.CallA5),                 // 17 lw    t1,a5(s0)
                0xC60C0000u | Lo(CodeCaves.CallF12),                // 18 lwc1  f12,f12(s0)
                0x0320F809u,                                        // 19 jalr  t9
                0x00000000u,                                        // 20   nop
                0xAE020000u | Lo(CodeCaves.CallV0),                 // 21 sw    v0,v0(s0)
                0x240A0001u,                                        // 22 li    t2,1
                0xAE0A0000u | Lo(CodeCaves.CallDone),               // 23 sw    t2,done(s0)
                0x8FBF0010u,                                        // 24 lw    ra,0x10(sp)
                0x8FB00014u,                                        // 25 lw    s0,0x14(sp)
                0x27BD0020u,                                        // 26 addiu sp,sp,0x20
                0x03E00008u,                                        // 27 ret: jr ra
                0x00000000u,                                        // 28   nop
            };
            if (cave + (uint)words.Length * 4 > DebugIfCave.Host + DebugIfCave.HostSpan)
                throw new IOException("The call-request cave does not fit its host (DebugInfomationIF).");
            if (RdU32(fs, ElfOff(DebugIfCave.Host)) != 0x03E00008u)
                throw new IOException("PatchCallRequest must follow ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts (the host's `jr ra`).");
            WriteWords(fs, ElfOff, cave, words);
        }
    }
}
