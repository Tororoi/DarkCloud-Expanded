using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Toan's melee ELF patches: his stride on the guard walk, the charge lunge's gravity, and the baked charge hit radii
    /// and kick strengths turned into mod-owned data words. Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfToanMeleePatches
    {
        /// <summary>TOAN'S STRIDE, scaled without touching his animation. The dungeon walk is not root motion in the
        /// .mot sense — the clips play in place — the player key handler builds a per-frame move vector from his yaw
        /// and clip-driven magnitudes and hands it to MoveCheck; at dun 0x1DB0F68 that vector sits in f21 (X) and
        /// f20 (Z), about to be halved for Goo and zeroed for Freeze:
        /// <code>
        ///   0x1DB0F68  li  a0,0x40         →  jal StrideScale cave
        ///   0x1DB0F6C  jal 0x1B1930        →  li  a0,0x40   (the jal's delay slot: the displaced load)
        ///   0x1DB0F70  nop                    (the call returns here, as before)
        /// </code>
        /// The cave adds <see cref="CodeCaves.StrideScale"/> × the vector back onto it while the current motion is 33
        /// (the guard walk), then tail-jumps into the status check the hook displaced, which returns to the handler
        /// with a0 = 0x40 in place. A zero word is vanilla, so nothing needs seeding. Clobbers f0-f2, t0-t2 — all
        /// temporaries the handler reloads before use.</summary>
        internal static void PatchStrideScale(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StatusCheck = 0x001B1930, MotionIdGuest = 0x01EA2988, Motion = 33;
            uint cave = DebugInfoCave.StrideScale;
            HiLo(CodeCaves.StrideScaleGuest, out uint hi, out uint lo);               // lwc1's offset is signed
            uint[] words =
            {
                0x3C080000u | hi,                                   // lui   t0,HI(StrideScale)
                0xC5000000u | lo,                                   // lwc1  f0,LO(t0)              the extra fraction
                0x3C090000u | (MotionIdGuest >> 16),                // lui   t1,HI(MotionId)
                0x8D290000u | (MotionIdGuest & 0xFFFFu),            // lw    t1,LO(t1)              the current motion
                0x240A0000u | Motion,                               // addiu t2,zero,33
                0x152A0005u,                                        // bne   t1,t2,+5 → skip
                0x00000000u,                                        //   nop
                0x4600A842u,                                        // mul.s f1,f21,f0
                0x4600A082u,                                        // mul.s f2,f20,f0
                0x4601AD40u,                                        // add.s f21,f21,f1             X stride += extra × X
                0x4602A500u,                                        // add.s f20,f20,f2             Z stride += extra × Z
                MipsAsm.J(StatusCheck),                             // j     0x1B1930   (skip:)     the displaced call, returning to the handler
                0x00000000u,                                        //   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugInfoCave.Host + DebugInfoCave.HostSpan, "The stride cave does not fit its host (DebugInfomationDraw).");
        }

        /// <summary>The LUNGE GRAVITY caves (see DebugInfoCave.LungeGravitySeed) and the main-ELF hook. The charge
        /// lunge's parabola is seeded in ToanKey_Play — `lwc1 f12,-0x7f80(gp)` (the shared 0.1) then `jal
        /// ParabolicInitialVector` (0x242E00) — and its vertical speed loses that same 0.1 every frame in the dungeon key
        /// process (dun 0x1DB3638: `lwc1 f0,-0x7f80(gp); sub.s f0,f2,f0`, hooked by DunPatches). Each cave loads the 0.1
        /// itself, scales it by (1 + CodeCaves.LungeGravityExtra) and hands the result on: the seed cave tail-jumps into
        /// ParabolicInitialVector with f12 = g (the `jal` is retargeted to the cave, so the callee returns to the key
        /// handler as before); the step cave returns with the displaced subtraction in its delay slot. f1 is a dead
        /// temporary at both sites; at is the assembler's.</summary>
        internal static void PatchLungeGravity(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Parabolic = 0x001D4080, SeedHook = 0x00242E00;
            HiLo(CodeCaves.LungeGravityExtraGuest, out uint hi, out uint lo);        // lwc1's offset is signed
            uint[] seed =
            {
                0x3C010000u | hi,                                   // lui   at,HI(extra)
                0xC4210000u | lo,                                   // lwc1  f1,LO(at)
                0xC78C8080u,                                        // lwc1  f12,-0x7f80(gp)        the shared 0.1
                0x460C0842u,                                        // mul.s f1,f1,f12              0.1 × extra
                MipsAsm.J(Parabolic),                               // j     ParabolicInitialVector
                0x46016300u,                                        //   add.s f12,f12,f1           g = 0.1 × (1 + extra)
            };
            uint[] step =
            {
                0x3C010000u | hi,                                   // lui   at,HI(extra)
                0xC4210000u | lo,                                   // lwc1  f1,LO(at)
                0xC7808080u,                                        // lwc1  f0,-0x7f80(gp)         the shared 0.1
                0x46000842u,                                        // mul.s f1,f1,f0               0.1 × extra
                0x46010000u,                                        // add.s f0,f0,f1               g = 0.1 × (1 + extra)
                0x03E00008u,                                        // jr    ra
                0x46001001u,                                        //   sub.s f0,f2,f0             vy −= g (the displaced op)
            };
            uint seedAt = DebugInfoCave.LungeGravitySeed, stepAt = DebugInfoCave.LungeGravityStep;
            if (stepAt + (uint)step.Length * 4 > DebugInfoCave.Host + DebugInfoCave.HostSpan)
                throw new IOException("The lunge-gravity caves do not fit their host (DebugInfomationDraw).");
            WriteWords(fs, ElfOff, seedAt, seed);
            WriteWords(fs, ElfOff, stepAt, step);
            ReplaceWord(fs, ElfOff, SeedHook, MipsAsm.Jal(Parabolic), MipsAsm.Jal(seedAt),
                        was => $"ToanKey_Play's parabola call is not where expected (0x{was:X8} at 0x{SeedHook:X})");
        }

        /// <summary>Toan's two CHARGE-ATTACK hit radii become data. ToanKey_Play bakes each into its own
        /// instruction pair before handing it to CCollisionData::Set as the sphere radius:
        /// <code>
        ///   0x241AC0  lui v0,0x40C0 ; mtc1 v0,f12   →  6.0, then `li a0,2` — the LUNGE (attack kind 2)
        ///   0x241B90  lui v0,0x4140 ; mtc1 v0,f12   → 12.0, then `li a0,3` — the WHIRLWIND (attack kind 3)
        /// </code>
        /// Each pair is rewritten to load from <see cref="CodeCaves.ChargeHitRadius"/> instead, exactly as
        /// PatchFishingCameraHeight does for the camera's baked 40.0:
        /// <code>
        ///   lui  $2,HI(slot)
        ///   lwc1 $f12,LO(slot)($2)
        /// </code>
        /// What that buys: an ability can resize the ENGINE'S OWN charge hit and let the engine do the rest —
        /// damage, victims, knockback, one plant per frame — instead of watching for the hit from a mod tick and
        /// planting its own spheres, which is timing-sensitive and was repeatedly wrong. ⚠ The words are read on
        /// every charge swing, so the mod seeds both at startup; 0 would be a hit radius of nothing.</summary>
        internal static void PatchChargeHitRadius(FileStream fs, Func<uint, long> ElfOff)
        {
            PatchF12Site(fs, ElfOff, 0x00241AC0, 0x3C0240C0,
                         CodeCaves.ChargeHitRadiusGuest + CodeCaves.ChargeRadiusLunge, "lunge hit-radius");
            PatchF12Site(fs, ElfOff, 0x00241B90, 0x3C024140,
                         CodeCaves.ChargeHitRadiusGuest + CodeCaves.ChargeRadiusWhirl, "whirlwind hit-radius");
        }

        /// <summary>Toan's five baked melee KICK STRENGTHS become data (CodeCaves.MeleeKickWords). ToanKey_Play plants each
        /// hit's kick with `lui v0,IMM; mtc1 v0,f12` ahead of `jal SetKickBack` — combo hit 3 (1.5 at 0x2418D8), hit 4
        /// (2.0 at 0x241978), hit 5 (3.0 at 0x241A38), the lunge (3.0 at 0x241B04) and the whirlwind (3.0 at 0x241BD4);
        /// hits 1 and 2 already read a word. Each pair becomes `lui v0,HI; lwc1 f12,LO(v0)` of its own word, so a sword
        /// can set the knockback of EVERY hit (MeleeKick), pnach-seeded to the vanilla figures otherwise.</summary>
        internal static void PatchMeleeKickStrength(FileStream fs, Func<uint, long> ElfOff)
        {
            uint w = CodeCaves.MeleeKickWordsGuest;
            PatchF12Site(fs, ElfOff, 0x002418D8, 0x3C023FC0, w + (uint)CodeCaves.MeleeKickHit3,  "combo hit 3 kick");
            PatchF12Site(fs, ElfOff, 0x00241978, 0x3C024000, w + (uint)CodeCaves.MeleeKickHit4,  "combo hit 4 kick");
            PatchF12Site(fs, ElfOff, 0x00241A38, 0x3C024040, w + (uint)CodeCaves.MeleeKickHit5,  "combo hit 5 kick");
            PatchF12Site(fs, ElfOff, 0x00241B04, 0x3C024040, w + (uint)CodeCaves.MeleeKickLunge, "lunge kick");
            PatchF12Site(fs, ElfOff, 0x00241BD4, 0x3C024040, w + (uint)CodeCaves.MeleeKickWhirl, "whirlwind kick");
        }

        /// <summary>An immediate float handed over in f12 (`lui v0,IMM; mtc1 v0,f12`, v0 dead after) becomes a load of
        /// the data word at <paramref name="slot"/> (`lui v0,HI; lwc1 f12,LO(v0)`).</summary>
        private static void PatchF12Site(FileStream fs, Func<uint, long> ElfOff, uint luiAddr, uint vanillaLui,
                                         uint slot, string what)
        {
            const uint VanillaMtc1 = 0x44826000;                 // mtc1 $2,$f12
            uint mtc1Addr = luiAddr + 4;
            uint gotLui = RdU32(fs, ElfOff(luiAddr)), gotMtc1 = RdU32(fs, ElfOff(mtc1Addr));
            HiLo(slot, out uint hi, out uint lo);                // lwc1's offset is SIGNED — compensate like the assembler
            uint wantLui = 0x3C020000u | hi;
            uint wantLwc1 = 0xC4000000u | (2u << 21) | (12u << 16) | lo;
            if (gotLui == wantLui && gotMtc1 == wantLwc1) return;                 // idempotent re-run
            if (gotLui != vanillaLui || gotMtc1 != VanillaMtc1)
                throw new IOException($"Toan's {what} site 0x{luiAddr:X} is not vanilla " +
                                      $"(got 0x{gotLui:X8}/0x{gotMtc1:X8}) — is this an unmodified Dark Cloud (USA) ISO?");
            WrU32(fs, ElfOff(luiAddr),  wantLui);
            WrU32(fs, ElfOff(mtc1Addr), wantLwc1);
        }
    }
}
