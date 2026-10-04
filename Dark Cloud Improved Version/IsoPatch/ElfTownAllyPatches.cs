using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The swapped-in town ally's ELF patches (the in-place `_LOAD_MAIN_CHARA` swap, AllySwapPrototype / TownIdleSit /
    /// TownLadder): the idle motion overridden from a mailbox (idle → sit for the cat), the ladder mount refused for an ally the
    /// Toan-rigged climb overlay would crash on, and the player's "!" mark lifted off a shorter ally's mesh. Three hand-built
    /// caves in the mod's ELF cave segment. Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfTownAllyPatches
    {
        // ── Town idle-motion override (idle → sit for the swapped-in cat) ────────────────────────────
        // EdMoveChara @0x16a160 drives the town character's idle/run/walk animation with ONE grounded write:
        //   0x16a6a8  sw s0,0xc68(s2)   ; *(char+0xc68) = motion   (s0 = 0 idle / 1 run / 2 walk, s2 = char)
        // guarded by `if ((chara_mode & 6)==0 && chara_fishing < 2)` — the plain locomotion store, NOT the
        // airborne fall/land writes (which store the constants 8 and 9) nor the fishing-state writes. Redirect
        // it to a tiny cave in the mod's ELF cave segment (loader-loaded at boot — a jal there is legal; runtime-
        // written heap caves crash the recompiler): the cave keeps run/walk as-is, and when the motion is idle (0) it stores
        // the IdleMotionOverride mailbox (guest 0x01F10070) instead — so a non-zero mailbox (the mod's sit index)
        // makes an idle town character sit, while a zero mailbox leaves vanilla idle untouched. The jal's delay
        // slot is the following `sw zero,0xc64(s2)` (kept — order-independent), and the cave returns via `jr $ra`
        // to 0x16a6b0 (the c60 stores). Scratch = $v0 (reloaded by `lui v0` at the return) and $at (dead after
        // the guard branch), both dead across the hook; $ra is stack-saved at function entry (`sq ra,0xc0(sp)`),
        // so the jal's $ra clobber is safe. $s0/$s2 are read-only. (Cave hand-built via the MipsAsm encoders.)
        internal static void PatchIdleMotionOverride(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x0016A6A8;   // EdMoveChara grounded locomotion store `sw s0,0xc68(s2)`
            // NOT a hand-picked literal — a first placement sat inside the fishline-split bin and clobbered the
            // rope step-cave's tail → every Queens fishing session hung on entry. The ELF cave-segment map
            // lives in CodeCaveAddresses.ElfCave — place new caves from THERE, never from a patch-local literal.
            const uint CaveAddr = ElfCave.IdleMotionOverride;
            uint mbGuest = (uint)(Mailbox.IdleMotionOverride - 0x20000000);   // 0x01F10070 (guest form the cave reads)

            if (RdU32(fs, ElfOff(HookAddr)) != Sw(s0, 0xc68, s2))   // 0xAE500C68
                throw new IOException($"Idle-motion hook site 0x{HookAddr:X} is not vanilla `sw s0,0xc68(s2)` — unmodified Dark Cloud (USA) ISO expected.");

            uint mbFlagsGuest = (uint)(Mailbox.IdleMotionFlags - 0x20000000);   // 0x01F10080 — same upper half as the index mailbox
            uint[] cave = {
                Move(v0, s0),                                          // v0 = motion (default: as the engine computed)
                Bne(s0, zero, 5), 0,                                   // motion != 0 (run/walk) → keep it; branch to the store. delay = nop
                Lui(at, mbGuest >> 16),
                Lw(v0, (int)(mbGuest & 0xFFFF), at),                   // idle: v0 = *IdleMotionOverride (0 → still idle)
                Lw(at, (int)(mbFlagsGuest & 0xFFFF), at),              // at = *IdleMotionFlags (bit1 = play once + hold last frame)
                Sw(at, 0xc64, s2),                                     // re-write char+0xc64 (the hook's delay slot zeroed it) — 0 = vanilla loop
                Jr(ra), Sw(v0, 0xc68, s2),                             // return to 0x16a6b0; delay slot stores the motion id to char+0xc68
            };
            WriteWords(fs, ElfOff, CaveAddr, cave);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // store → jal cave; delay slot `sw zero,0xc64(s2)` runs first (harmless — c64 is zeroed either way)
        }

        // ── Town ladder-mount refusal (a swapped-in non-Toan ally must never climb) ──────────────────
        // The town ladder MOUNT loads a Toan-rigged climb overlay onto the active model; with a non-Toan ally
        // swapped in that rig mismatch crashes. EdMoveChara @0x16a160 mounts a ladder (event-point type 4/5) with
        // exactly two instructions, both under the SAME `PadDown(Cross) && (iVar9!=0 || viewMode==0)` press gate:
        //   0x16c0fc  jal EdInitHashigo(0x16d720)   ; a0=0x1d3d1d0, a1=s5 — THE MOUNT (loads the climb overlay)
        //   0x16c104  li  s8,0x1                     ; climbing flag → stored to DAT_01d1970c @0x16c268
        // (s8's not-climbing default is -1, set by `moveq s8,s6` @0x16bf18 with s6=-1; the vanilla no-press path
        // simply skips 0x16c104, leaving s8=-1.) Both must be skipped TOGETHER or the game thinks it is climbing
        // with no setup and hangs. Redirect the `jal EdInitHashigo` to a cave in the mod's ELF cave segment
        // (loader-loaded at boot — a jal there is legal; runtime-written heap caves crash the recompiler): it reads BlockLadder,
        // and when it is 0 it replicates vanilla exactly (calls EdInitHashigo with a0/a1 still set, then sets the
        // climbing flag s8=1), returning past the `li s8,1` to 0x16c108. When BlockLadder != 0 it raises the
        // RefusalRequested mailbox and returns to 0x16c108 WITHOUT the mount and WITHOUT touching s8 (so the
        // not-climbing -1 flows straight through) — the refusal is armed only here, inside the mount press gate,
        // so it fires once per Cross press like the vanilla one-shot mount, not every frame near the ladder. Both
        // paths return via `j 0x16c108`, so the inner EdInitHashigo call clobbering $ra is irrelevant (vanilla
        // clobbers it at the same site anyway, and the function reloads $ra from the stack at its epilogue).
        // Scratch = $at + $v0 (both dead across the site); $s5/$a0/$a1 are read-only until the preserved call.
        // (Cave hand-built via the MipsAsm encoders, like PatchIdleMotionOverride.)
        internal static void PatchLadderRefusal(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr      = 0x0016C0FC;   // EdMoveChara ladder mount `jal EdInitHashigo`
            const uint EdInitHashigo = 0x0016D720;   // the mount (loads the climb overlay)
            const uint AfterFlag     = 0x0016C108;   // return target: `li s4,1`, one past the `li s8,1` climbing-flag set
            const uint CaveAddr      = ElfCave.LadderRefusal;   // registry: CodeCaveAddresses.ElfCave
            uint blockGuest   = (uint)(Mailbox.BlockLadder      - 0x20000000);   // 0x01F10074 (guest form the cave reads)
            uint refusalGuest = (uint)(Mailbox.RefusalRequested - 0x20000000);   // 0x01F10078 (guest form the cave sets)

            if (RdU32(fs, ElfOff(HookAddr)) != Jal(EdInitHashigo))   // 0x0C05B5C8
                throw new IOException($"Ladder-mount hook site 0x{HookAddr:X} is not vanilla `jal EdInitHashigo` — unmodified Dark Cloud (USA) ISO expected.");

            uint[] cave = {
                Lui(at, blockGuest >> 16),                                   // at = mailbox page (0x01F10000)
                Lw(v0, (int)(blockGuest & 0xFFFF), at),                      // v0 = *BlockLadder
                Bne(v0, zero, 6), 0,                                         // BlockLadder != 0 → BLOCK path (word 9); delay nop
                Jal(EdInitHashigo), 0,                                       // vanilla: EdInitHashigo(a0=0x1d3d1d0, a1=s5); delay nop
                Addiu(s8, zero, 1),                                          // climbing flag s8 = 1
                J(AfterFlag), 0,                                             // return past the `li s8,1`; delay nop
                Addiu(v0, zero, 1),                                          // BLOCK: v0 = 1
                Sw(v0, (int)(refusalGuest & 0xFFFF), at),                    // *RefusalRequested = 1 (at still = mailbox page)
                J(AfterFlag), 0,                                             // return WITHOUT mount, s8 untouched (stays -1); delay nop
            };
            WriteWords(fs, ElfOff, CaveAddr, cave);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // jal EdInitHashigo → jal cave; delay slot @0x16c100 (nop) runs first
        }

        // ── Player exclamation-mark height boost (lift the "!" off a shorter ally's mesh) ─────────────
        // EdDrawSysCursor @0x17cbf0 draws the PLAYER's floating "!" event-trigger mark inside its
        // `if (DAT_01d3d4a8 != 0)` block. The mark's world Y is accumulated in fStack_c (= auStack_10+4, sp+0x94):
        //   0x17cf58  add.S f0,f0,f1     ; f0 = fStack_c + (3.0 + *(Chara+0xb4) + sinf(a)*0.5)   = vanilla Y
        //   0x17cf5c  swc1  f0,0x94(sp)  ; STORE Y → auStack_10+4   ← THE HOOK
        //   0x17cf60  lwc1  f1,-0x6e54(gp); f1 = a_1906   (bob-angle reload — the jal's delay slot)
        //   0x17cf68  add.S f1,f1,f0     ; a_1906 += delta  ← still needs f1 = a_1906
        // A swapped-in ally with different proportions (the cat) sits lower, so the mark pokes through its mesh.
        // Redirect the store to a cave in the mod's ELF cave segment (loader-loaded at boot — a jal there is legal;
        // runtime-written heap caves crash the recompiler): it adds *ExclamationYBoost (guest 0x01F1007C, EE-writable) to the Y and
        // then performs the displaced store, so the mark rides `vanilla Y + boost`. A 0.0 boost reproduces vanilla
        // bit-exactly for any real position (x + 0.0 == x). This is the PLAYER mark ONLY — the NPC-cursor loop
        // earlier in the function (the `DAT_01d25c44 + 2.0 / offset_1876` store `swc1 f0,0x0(s3)` @0x17cd28) is
        // untouched. Register contract at the hook: the `jal cave` return address is 0x17cf64 (jal+8) and its delay
        // slot @0x17cf60 runs FIRST, so on cave entry f1 already = a_1906 (must be PRESERVED for 0x17cf68) and f0
        // still = the vanilla Y (0x17cf60 doesn't touch f0). The cave therefore scratches f2 (dead — last held
        // 0.5*sin, consumed at 0x17cf50) and $at (dead here; also clobbered by the later SetPosition call), never
        // f0/f1. $ra is stack-saved at entry (`sq ra,0x60(sp)`), so the jal's $ra clobber is safe.
        // (Cave hand-built via the MipsAsm encoders, like PatchIdleMotionOverride.)
        internal static void PatchExclamationHeight(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x0017CF5C;   // EdDrawSysCursor PLAYER-mark final Y store `swc1 f0,0x94(sp)`
            const uint CaveAddr = ElfCave.ExclamationHeight;   // registry: CodeCaveAddresses.ElfCave
            uint mbGuest = (uint)(Mailbox.ExclamationYBoost - 0x20000000);   // 0x01F1007C (guest form the cave reads)

            if (RdU32(fs, ElfOff(HookAddr)) != Swc1(f0, 0x94, sp))   // 0xE7A00094
                throw new IOException($"Exclamation-height hook site 0x{HookAddr:X} is not vanilla `swc1 f0,0x94(sp)` — unmodified Dark Cloud (USA) ISO expected.");

            uint[] cave = {
                Lui(at, mbGuest >> 16),                     // at = mailbox page (0x01F10000)
                Lwc1(f2, (int)(mbGuest & 0xFFFF), at),      // f2 = *ExclamationYBoost   (0.0 → identity)
                AddS(f0, f0, f2),                           // f0 = vanilla Y + boost
                Swc1(f0, 0x94, sp),                         // displaced original store → auStack_10+4
                Jr(ra), 0,                                  // return to 0x17cf64 (jal+8); delay slot nop
            };
            WriteWords(fs, ElfOff, CaveAddr, cave);

            WrU32(fs, ElfOff(HookAddr), Jal(CaveAddr));   // store → jal cave; delay slot @0x17cf60 (lwc1 f1,a_1906) runs first, unchanged
        }

        // ── Ally in-place-swap buffer grow: REMOVED 2026-09-04 ───────────────────────────────────────
        // PatchAllyTextureBudget grew the town TEXTURE arena (0x1d3a080) + GEOMETRY arena (0x1d3a060) and
        // shrank the scene arena (0x1d3a050) so Ungaga's oversized cloth-less NPC model (c10a, 1.05MB) would
        // fit an in-place _LOAD_MAIN_CHARA. It had a nasty side effect: growing the TEXTURE arena pushed the
        // GEOMETRY arena's base later, and the town player's CLOTH lives in geometry — so on the first cold
        // Queens load it INTERMITTENTLY landed on uninitialised boot memory and its Verlet solver blew up
        // (Toan's "missing front cape"). Switching Ungaga to his 479KB PLAYER model c10p (which also gives him
        // real cloth) dropped every ally under Toan's 850KB, so all fit the VANILLA arenas — the grow became
        // unnecessary and removing it returned the cloth buffer to its benign vanilla home, fixing Toan's cape
        // (confirmed clean across cold boots 2026-09-04). Recover from git if a future ally ever exceeds vanilla.
        // See [[ccloth-particle-layout]], [[town-ally-switch-reload]].
    }
}
