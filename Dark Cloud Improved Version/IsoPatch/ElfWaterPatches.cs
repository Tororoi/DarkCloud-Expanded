using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// ELF water-visual patches, ORDER-COUPLED: PatchDrawWaterCompaction frees the DrawWater cave that
    /// PatchWaterRedraw hosts its hook in, and PatchCapeEarlyDraw retargets a jal that PatchWaterRedraw
    /// writes. ElfPatches.ElfPatchAndCrc calls them in that order.
    /// </summary>
    internal static class ElfWaterPatches
    {
        // ── DrawWater compaction — frees the cave the water-redraw hook (below) lives in ────────────────
        // DrawWater__11CEditGroundFi has a "vtable call bracket" (moveq a0,s2 / call s2's vtable+0x14 /
        // moveq a0,s2 / call s2's vtable+0x94) repeated identically 4 times back-to-back in its mode-4
        // "corner fan" — confirmed branch-free internally and untargeted from outside by a full
        // whole-function branch/jump scan, so it's safe to rewrite in place rather than same-size-repack.
        // Factored into one shared helper (which must stash/restore $ra itself in SCRATCH memory across
        // its own two nested vtable calls — neither survives in a register, and DrawWater's frame has no
        // spare callee-saved slot) + 4 tiny call sites, freeing a pocket that hosts the water-redraw
        // hook's own trampoline (see PatchWaterRedraw).
        //
        // ⚠ LAYOUT ORDER IS LOAD-BEARING. The span's entry (0x1A3768) is reached by FALL-THROUGH from
        // the `bne v0,v1,0x1A3890` immediately before it — NOT by any branch (a branch-target scan comes
        // up empty, which is exactly how a first version of this patch put the HELPER at the span start
        // and hard-hung the game: normal execution fell into the helper uninvited, it stashed the WRONG
        // $ra, ran the vtable calls with garbage args, and its `jr ra` jumped BACKWARD to the preceding
        // jal's return address — an EE infinite loop, black screen, dead inputs, no crash). So the
        // vanilla code chunks stay at the span's front (entry word byte-identical to vanilla), and the
        // helper + hook cave sit at the back behind an unconditional `b 0x1A3890`, reachable only by
        // explicit jal/j. When repacking a span, scan for branch targets AND check how the span is
        // ENTERED — fall-through is a reference no target-scan will ever show.
        internal static void PatchDrawWaterCompaction(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SpanStart = 0x001A3768;   // hosts HELPER + call sites + the water-redraw HOOK_CAVE
            uint[] vanillaSpan =
            {
                0x8E0200F4, 0x00021080, 0x00541021, 0x8C440004, 0x0C05C080, 0x00000000,
                0x46000506, 0xC7A10080, 0x3C023F00, 0x44820000, 0x00000000, 0x46140082,
                0x46020800, 0xE7A00080, 0x27B50088, 0xC6A00000, 0x46020000, 0xE6A00000,
                0x72402628, 0x27A50080, 0x8E5900A0, 0x8F390014, 0x0320F809, 0x00000000,
                0x72402628, 0x8E5900A0, 0x8F390094, 0x0320F809, 0x00000000, 0x27A40090,
                0x27A50080, 0x0C04860C, 0x00000000, 0xC7A00080, 0x46140001, 0xE7A00090,
                0x72402628, 0x27A50090, 0x8E5900A0, 0x8F390014, 0x0320F809, 0x00000000,
                0x72402628, 0x8E5900A0, 0x8F390094, 0x0320F809, 0x00000000, 0xC6A00000,
                0x46140001, 0xE7A00098, 0x72402628, 0x27A50090, 0x8E5900A0, 0x8F390014,
                0x0320F809, 0x00000000, 0x72402628, 0x8E5900A0, 0x8F390094, 0x0320F809,
                0x00000000, 0xC7A00080, 0xE7A00090, 0x72402628, 0x27A50090, 0x8E5900A0,
                0x8F390014, 0x0320F809, 0x00000000, 0x72402628, 0x8E5900A0, 0x8F390094,
                0x0320F809, 0x00000000,
            };
            uint[] patchedSpan =
            {
                0x8E0200F4, 0x00021080, 0x00541021, 0x8C440004, 0x0C05C080, 0x00000000,
                0x46000506, 0xC7A10080, 0x3C023F00, 0x44820000, 0x00000000, 0x46140082,
                0x46020800, 0xE7A00080, 0x27B50088, 0xC6A00000, 0x46020000, 0xE6A00000,
                0x27A50080, 0x0C068E06, 0x00000000, 0x27A40090, 0x27A50080, 0x0C04860C,
                0x00000000, 0xC7A00080, 0x46140001, 0xE7A00090, 0x27A50090, 0x0C068E06,
                0x00000000, 0xC6A00000, 0x46140001, 0xE7A00098, 0x27A50090, 0x0C068E06,
                0x00000000, 0xC7A00080, 0xE7A00090, 0x27A50090, 0x0C068E06, 0x00000000,
                0x1000001F, 0x00000000, 0x3C0101FB, 0xAC3FE604, 0x72402628, 0x8E5900A0,
                0x8F390014, 0x0320F809, 0x00000000, 0x72402628, 0x8E5900A0, 0x8F390094,
                0x0320F809, 0x00000000, 0x3C0101FB, 0x8C3FE604, 0x03E00008, 0x00000000,
                0x3C0101FB, 0x8C28E600, 0x11000003, 0xAC20E600, 0x0C068DC3, 0x00000000,
                0x8F829074, 0x14400003, 0x00000000, 0x0805F07A, 0x00000000, 0x0805F0FA,
                0x00000000, 0x00000000,
            };
            // ^ the hook cave's draw call (0x0C068DC3) targets the FLUSH_STUB at 0x1A370C (below), which
            //   appends a VIF FLUSH before jumping into the relocated payload — see patchedB1.

            // ── the isolated 5th bracket instance: same compaction (call site + relocated `b`), with the
            //    freed 8 words hosting FLUSH_STUB: PkCnt(pkt,0) + PkAddCode(pkt, 0x11000000 VIF-FLUSH) +
            //    j payload. FLUSH stalls VIF1 until the VU1 program ends AND both GIF paths drain — the
            //    ordering guarantee sceVif1PkOpenDirectCode deliberately omits. Without it, the payload's
            //    framebuffer blit (DIRECT/PATH2) executes while the just-appended character batches are
            //    still in the VU1 pipeline (PATH1) — the capture misses the player entirely, which is why
            //    the submerged body never appeared even after the texture-group fix. Placed AFTER the
            //    relocated unconditional `b` so normal DrawWater flow can never fall into it (same
            //    fall-through rule as the helper/hook cave).
            const uint Block1Start = 0x001A36F8;
            uint[] vanillaB1 =
            {
                0x72402628, 0x27A50080, 0x8E5900A0, 0x8F390014, 0x0320F809, 0x00000000,
                0x72402628, 0x8E5900A0, 0x8F390094, 0x0320F809, 0x00000000, 0x1000005A,
                0x00000000,
            };
            uint[] patchedB1 =
            {
                0x27A50080, 0x0C068E06, 0x00000000,                         // call site -> shared HELPER
                0x10000062, 0x00000000,                                     // b 0x1A3890 (guards the stub)
                0x8F848BD4, 0x0C048320, 0x70002E28,                         // FLUSH_STUB: PkCnt(Vif1Packet, 0)
                0x8F848BD4, 0x0C048404, 0x3C051100,                         // PkAddCode(Vif1Packet, FLUSH)
                0x0805EF03, 0x00000000,                                     // j payload capture-half (0x17BC0C) -> mizu -> zbuf/quad
            };

            for (int i = 0; i < vanillaSpan.Length; i++)
                if (RdU32(fs, ElfOff(SpanStart + (uint)i * 4)) != vanillaSpan[i])
                    throw new IOException($"DrawWater compaction site 0x{SpanStart + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < vanillaB1.Length; i++)
                if (RdU32(fs, ElfOff(Block1Start + (uint)i * 4)) != vanillaB1[i])
                    throw new IOException($"DrawWater compaction site 0x{Block1Start + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < patchedSpan.Length; i++)
                WrU32(fs, ElfOff(SpanStart + (uint)i * 4), patchedSpan[i]);
            for (int i = 0; i < patchedB1.Length; i++)
                WrU32(fs, ElfOff(Block1Start + (uint)i * 4), patchedB1[i]);
        }

        /// <summary>
        /// The submerged-body TINT half of the canal healing-spring look. Town <c>MainDraw</c> (0x17B7D0)
        /// draws the CWater ripple/refraction plane BEFORE the player (fb-capture → DrawWaterSurface → … →
        /// EdDrawCharacter), so the plane's alpha-blended refraction never samples the player's own pixels —
        /// the player just draws dry on top afterward. Dungeon healing springs get the tinted/distorted look
        /// specifically because they draw water SECOND (player → fb-capture → water).
        ///
        /// FIRST DESIGN (reverted — crashed to a black screen): call the existing GameMode-gate+payload
        /// block a SECOND time per frame (once normally, once again after EdDrawCharacter), gated by a
        /// one-shot sentinel so it wouldn't fall through into MainDraw's next task. Every MIPS-level
        /// register/control-flow claim checked out under full assemble+decode+trace verification, but the
        /// design had an architectural problem no amount of register-safety checking would catch: the
        /// actual GS draw (<c>DrawWaterSurface → DrawVu1__6CWater</c>) appends its command data into a
        /// SINGLE shared, almost certainly fixed-size, once-per-frame VIF1 packet buffer
        /// (<c>GetVif1Packet()/Vif1Packet</c>, built via <c>sceVif1Pk*</c> calls). Invoking the whole draw
        /// pipeline twice in one frame likely overflowed that buffer — a silent DMA/GS corruption, which
        /// matches the observed symptom (black screen, hard crash, no catchable exception) far better than
        /// a code bug would.
        ///
        /// THIS DESIGN moves the draw instead of duplicating it, so <c>DrawWaterSurface</c> is still
        /// called AT MOST ONCE per frame — same total call count as vanilla, just relocated:
        /// - The payload's own start (<c>0x17BC00</c>) becomes a 3-word STUB: set a one-shot flag, jump to
        ///   0x17BCC4 (the SAME address the GameMode gate's own "no match" path already jumps to) — so the
        ///   water does NOT draw here anymore, it just records that the gate matched.
        /// - The rest of the (now-dead) payload space hosts a RELOCATED COPY of the actual draw calls
        ///   (verbatim, minus the dead `GetRef` call — see elf_compaction.md), ending in `jr ra` instead of
        ///   falling through, so it becomes callable.
        /// - The hook site (<c>0x17C1DC</c>, right after EdDrawCharacter) redirects to HOOK_CAVE (freed by
        ///   PatchDrawWaterCompaction): check the flag (always clearing it, whether set or not, so no stale
        ///   state survives into a frame where the gate doesn't match) — if it was set, `jal` the relocated
        ///   payload; either way, replicate the originally-displaced post-EdDrawCharacter check so control
        ///   flow is byte-for-byte equivalent to vanilla from there on.
        ///
        /// Baked into the ELF (not a runtime PINE write) because MainDraw is hot per-frame code — safe for
        /// the same reason PatchFishingCameraHeight et al. are: the new bytes are on disc before the game
        /// ever boots/JITs anything.
        /// </summary>
        internal static void PatchWaterRedraw(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x0017C1DC, HookDelayAddr = 0x0017C1E0;
            const uint VanillaHookLw = 0x8F829074, VanillaHookBne = 0x14400081;   // lw v0,-0x6f8c(gp) ; bne v0,zero,+0x81(->0x17C3E8)
            const uint PayloadStart = 0x0017BC00;    // GameMode-match payload — becomes stub(3) + relocated-copy(46)
            const uint HookCaveAddr = 0x001A3858; // inside DrawWater, freed by PatchDrawWaterCompaction

            uint[] vanillaPayload =
            {
                0x27A40410, 0x0C04BC4C, 0x00000000, 0xAFA00110, 0xAFA00114, 0x24020280,
                0xAFA20118, 0x240200E0, 0xAFA2011C, 0x3C0201C7, 0x24445870, 0x3C02002A,
                0x2445AB88, 0x2406FFFF, 0x0C04C4B4, 0x00000000, 0x27A60418, 0xDC420028,
                0xFCC20000, 0x27A40410, 0x27A50110, 0x70003E28, 0x70004628, 0x70004E28,
                0x0C04BC84, 0x00000000, 0x3C0201D3, 0x24444540, 0x27A50120, 0x0C0491A8,
                0x00000000, 0x27A40420, 0xDF828BF0, 0xFC820000, 0x93A50424, 0x64030001,
                0x2402FFFE, 0x00A21024, 0x00431025, 0xA3A20424, 0x0C04BBB0, 0x00000000,
                0x8F8490E8, 0x8F859100, 0x0C068CD8, 0x00000000, 0x27848BF0, 0x0C04BBB0,
                0x00000000,
            };
            uint[] patchedPayload =
            {
                0x3C0101FB, 0x0805EF31, 0xAC21E600,                         // STUB: set flag, skip to 0x17BCC4
                // ── capture-half @0x17BC0C: runs FIRST so water_buff holds the player's UNOBSCURED body
                //    (mizu is authored OPAQUE — the vanilla "transparency" is entirely the refraction quad,
                //    so the underwater look must be COMPOSITED: body captured before mizu, quad blends the
                //    captured body back over the opaque texture at CWater SetColor alpha — springs-style) ──
                0x3C0201C7, 0x24445870, 0x8F858BD4, 0x0C04CC1C, 0x24060015, // ReloadTexture(mgr, Vif1Packet, 0x15)
                0x0C04BC4C, 0x27A40410,                                     // MGGetFBuffTex(&tex0)
                0xFFA00110, 0x24020280, 0xAFA20118, 0x240200E0, 0xAFA2011C, // rect: sd zero zeroes x|y, then 640/224
                0x3C0201C7, 0x24445870, 0x3C02002A, 0x2445AB88, 0x0C04C4B4, 0x2406FFFF,   // GetTexture(mgr,"water_buff",-1)
                0xDC420028, 0xFFA20418,                                     // handle: ld v0,0x28(v0); sd v0,0x418(sp)
                0x27A40410, 0x27A50110, 0x27A60418, 0x70003E28, 0x70004628, 0x0C04BC84, 0x70004E28,  // MGMoveImage(...)
                0x0805EF20, 0x00000000,                                     // j zbuf-half (mizu now draws EARLY, not here)
                // ── zbuf-half @0x17BC80 (MIZU_STUB jumps back here): Z-mask + quad + restore ──
                0x27A40420, 0xDF828BF0, 0xFC820000, 0x93A50424, 0x64030001, 0x2402FFFE,
                0x00A21024, 0x00431025, 0x0C04BBB0, 0xA3A20424,             // ZMSK on: MGSetGsZBUF(&copy)
                0x8F8490E8, 0x0C068CD8, 0x8F859100,                         // DrawWaterSurface(pEditGround, NowCamera)
                0x0C04BBB0, 0x27848BF0,                                     // Z restore: MGSetGsZBUF(&mgZBuffer)
                0x08068E1C, 0x00000000,                                     // j 0x1A3870 (constant return, NO $ra)
            };
            // Two hard-won rules baked into this array:
            //  1. It ends `j 0x1A3870` (hard jump to the hook cave's continuation), NOT `jr ra`: the payload
            //     contains six jal's, and the last one leaves $ra pointing at the next instruction — a `jr ra`
            //     there jumps TO ITSELF forever (this was the boot hang, in every version until bisection
            //     found it). Inline code turned into a subroutine has its return path clobbered by its own
            //     calls; with a single fixed caller, a constant j needs no $ra at all.
            //  2. It opens with ReloadTexture(mgr, packet, GROUP 0x15) — the call vanilla makes at 0x17BB48
            //     right before the original payload position, and the dungeon makes (group 0xD) inside its
            //     own healing-spring block. Texture groups are PAGED into GS VRAM on demand; at the hook
            //     position the character draws have paged their own groups in, so without this rebind the
            //     fb-copy blits into (and the water samples from) GS VRAM occupied by character textures —
            //     jittery corrupted surface, no captured player in the refraction. The room for these 5 words
            //     came from moving each call's final arg-setup into its own jal delay slot (safe: the delay
            //     slot executes before the callee — only values that must SURVIVE a call can't live there).

            // ── the GameMode gate [0x17BB74, 0x17BC00): eleven separate li/beq/nop mode checks (35 words)
            //    compacted into one bitmask test (guarded `1 << mode` AND 0x146BF — bits {0,1,2,3,4,5,7,9,
            //    10,14,16}; the sltiu guard rejects modes ≥ 32, which sllv would otherwise alias mod-32).
            //    Semantics identical to vanilla: match -> the flag stub at 0x17BC00, no-match -> 0x17BCC4.
            //    The 23 freed words host MIZU_STUB (0x17BBA4, reachable ONLY via the FLUSH stub's j — the
            //    gate's own paths jump over it): if the C#-armed mailbox FramePtr (0x01FAE608) is nonzero,
            //    ReloadTexture(mailbox TexGroup 0x01FAE60C) + MGDraw(FramePtr) draws the scene-pass-hidden
            //    mizu mesh AFTER the player, then a SECOND VIF FLUSH (mizu renders via VU1/PATH1 — the
            //    payload's DIRECT blit must not overtake it, same race as the player capture) before
            //    joining the payload. Region entry is pure fall-through from 0x17BB70 (whole-ELF scan:
            //    nothing branches into it, not even its first word).
            const uint GateAddr = 0x0017BB74;
            uint[] vanillaGate =
            {
                0x8F838760, 0x2402000A, 0x10620020, 0x00000000, 0x24020005, 0x1062001D,
                0x00000000, 0x24020007, 0x1062001A, 0x00000000, 0x24020009, 0x10620017,
                0x00000000, 0x10600015, 0x00000000, 0x24020003, 0x10620012, 0x00000000,
                0x2402000E, 0x1062000F, 0x00000000, 0x24020002, 0x1062000C, 0x00000000,
                0x24020004, 0x10620009, 0x00000000, 0x24020010, 0x10620006, 0x00000000,
                0x24020001, 0x10620003, 0x00000000, 0x10000032, 0x00000000,
            };
            uint[] patchedGate =
            {
                0x8F838760, 0x2C620020, 0x10400007, 0x24020001, 0x00621004, 0x3C010001,
                0x342146BF, 0x00411024, 0x1440001A, 0x00000000, 0x10000049, 0x00000000,
                0x3C0101FB, 0x8C23E608, 0x1060000E, 0x00000000, 0x3C0201C7, 0x24445870,
                0x8F858BD4, 0x0C04CC1C, 0x24060008, 0x3C0101FB, 0x8C24E608, 0x0C04BB60,
                0x00000000, 0x3C0201C7, 0x24445870, 0x8F858BD4, 0x24060015, 0x0C04CC1C,
                0x00000000, 0x0805EED4, 0x00000000, 0x00000000, 0x00000000,
            };
            // ^ the pocket now hosts EARLY_STUB (0x17BBA4, jal'd from the displaced DrawWater call site at
            //   0x17BB6C): if the C#-armed mailbox FramePtr (0x01FAE608, now the PLAYER's model root) is
            //   nonzero: ReloadTexture(mailbox TexGroup 0x01FAE60C = the player's OWN group, read by C# from
            //   chara+0x148C — the same per-character field EdDrawCharacter binds from) then MGDraw it —
            //   drawing the player EARLY with resident textures, so the water part's own NATIVE pass (with
            //   its native blend state) draws mizu over the submerged half; the normal EdDrawCharacter later
            //   redraws the player and is Z-clipped at the waterline, leaving a crisp dry top half. The stub
            //   then RE-binds group 0x15 (vanilla had just bound it at 0x17BB48 for the water pass this hook
            //   displaced), replays the displaced DrawWater(pEditGround, 0x15) call, and constant-jumps back
            //   to 0x17BB74 (single caller — no $ra). This replaced the MIZU_STUB redraw approach: mizu drawn
            //   via MGDraw at the hook rendered OPAQUE over the body (either authored-opaque or missing the
            //   native pass's blend state), burying the player.

            uint gotLw = RdU32(fs, ElfOff(HookAddr)), gotBne = RdU32(fs, ElfOff(HookDelayAddr));
            if (gotLw != VanillaHookLw || gotBne != VanillaHookBne)
                throw new IOException($"Water-redraw hook site 0x{HookAddr:X} is not vanilla " +
                                      $"(got 0x{gotLw:X8}/0x{gotBne:X8}) — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < vanillaPayload.Length; i++)
                if (RdU32(fs, ElfOff(PayloadStart + (uint)i * 4)) != vanillaPayload[i])
                    throw new IOException($"Water-redraw payload site 0x{PayloadStart + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < vanillaGate.Length; i++)
                if (RdU32(fs, ElfOff(GateAddr + (uint)i * 4)) != vanillaGate[i])
                    throw new IOException($"Water-redraw gate site 0x{GateAddr + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");

            // ── EARLY-PLAYER hook: the `jal ReloadTexture(mgr, pkt, 0x15)` at 0x17BB48 is retargeted to
            //    EARLY_STUB, which (when armed) binds the player's group 8, MGDraws the player, then replays
            //    the displaced 0x15 bind — so the vanilla anime-step (0x17BB50-60) → DrawWater (0x17BB6C)
            //    sequence runs UNTOUCHED after it. Hooking the later DrawWater call instead (previous
            //    version) required re-binding 0x15 AFTER the anime-step, which clobbered the texture
            //    manager's staleness bookkeeping and froze all water texture animation. Delay slot at
            //    0x17BB4C is a vanilla nop, untouched; 0x17BB6C stays fully vanilla.
            const uint Hook2Addr = 0x0017BB48;
            const uint VanillaHook2 = 0x0C04CC1C;   // jal ReloadTexture__15CTextureManagerFP13sceVif1Packeti
            uint gotHook2 = RdU32(fs, ElfOff(Hook2Addr));
            if (gotHook2 != VanillaHook2)
                throw new IOException($"Early-player hook site 0x{Hook2Addr:X} is not vanilla " +
                                      $"(got 0x{gotHook2:X8}) — is this an unmodified Dark Cloud (USA) ISO?");

            for (int i = 0; i < patchedPayload.Length; i++)
                WrU32(fs, ElfOff(PayloadStart + (uint)i * 4), patchedPayload[i]);
            for (int i = 0; i < patchedGate.Length; i++)
                WrU32(fs, ElfOff(GateAddr + (uint)i * 4), patchedGate[i]);
            WrU32(fs, ElfOff(Hook2Addr), 0x0C05EEE9);   // jal EARLY_STUB (0x17BBA4)
            WrU32(fs, ElfOff(HookAddr), J(HookCaveAddr));
            WrU32(fs, ElfOff(HookDelayAddr), 0);   // nop — HOOK_CAVE replicates the displaced check itself
        }

        // ── Cape early-draw (waterfall occlusion, low tide) ──────────────────────────────────────────
        // The low-tide refraction EARLY_STUB (written by PatchWaterRedraw into patchedGate) MGDraws the player's
        // BODY (model root) before the water/waterfall pass, so the body survives the falls' Z-write — but the
        // CAPE (separate CCloth) isn't in the model root, so it's only drawn late and the falls clip it. Redirect
        // the EARLY_STUB's `jal MGDraw` @0x17BBD0 to the capeEarlyDraw cave, which re-does that MGDraw(body) then
        // walks the player's cloth list (char+0xC74, via mailbox CapeCharPtr) and Draw__6CCloths each piece early
        // too. MUST run AFTER PatchWaterRedraw (which writes the `jal MGDraw` this replaces).
        internal static void PatchCapeEarlyDraw(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StubAddr = 0x00228D40;   // dead CharaChange space, past the spray-bias shim (0x228D00, 60 B)
            const uint HookAddr = 0x0017BBD0;   // EARLY_STUB `jal MGDraw` (patchedGate[23], set by PatchWaterRedraw)
            if (RdU32(fs, ElfOff(HookAddr)) != 0x0C04BB60)   // = jal MGDraw (0x0012ED80)
                throw new IOException($"Cape early-draw hook site 0x{HookAddr:X} is not `jal MGDraw` — PatchWaterRedraw must run first / unmodified ISO expected.");
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.capeEarlyDraw.bin")
                ?? throw new IOException("Embedded EE function missing: capeEarlyDraw.bin (reassemble tools/stubs/cape_early_draw.s and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length == 0 || (b.Length & 3) != 0 || U32(b, 0) != 0x27BDFFE0)   // first insn = addiu $sp,$sp,-0x20
                throw new IOException($"capeEarlyDraw.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(StubAddr + (uint)i), U32(b, i));
            WrU32(fs, ElfOff(HookAddr), Jal(StubAddr));   // jal MGDraw → jal capeEarlyDraw (which re-does MGDraw + the cloth loop)
        }
    }
}
