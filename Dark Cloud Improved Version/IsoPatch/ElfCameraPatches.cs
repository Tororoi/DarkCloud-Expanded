using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// ELF camera patches: the native occlusion camera (PatchNativeCameraPostPass — vanilla controller +
    /// surgical NOPs + the townCameraCollision/cameraNormSide/cameraHeight caves with their tunables) and the
    /// fishing-camera target / height / gather patches. Called in order from ElfPatches.ElfPatchAndCrc.
    /// </summary>
    internal static class ElfCameraPatches
    {
        // ── NATIVE camera — ELF port, LEVERAGE vanilla's own controller + surgical edits ─────────────
        // We DON'T reimplement the camera: EdMoveChara already has a full
        // controller (decompile line 583) — right-stick rotation (AddAngle), stick-Y height (AddHeight), L1/R1
        // rotation (PadOn 8/4), auto-follow-behind when idle, distance/height clamping. We surgically fix the two
        // things that made it feel bad:
        //   1. Its rotation is COLLISION-GATED by bVar4(=s6)/bVar5(=s4) — "is the right/left side clear of walls"
        //      — so it refuses to rotate INTO a wall (the original "bounce"/stuck feel). Those flags are cleared
        //      only at 0x16B1E8 (`clear s6`, right within 5u) and 0x16B2FC (`clear s4`, left within 5u); NOP both
        //      → flags stay true → FREE rotation everywhere. (Stage 2 adds smooth pull-in to replace the gating.)
        //   2. Stub CheckCameraWidth (single caller 0x16AF98) → its width-slide AND the bVar gating inside that
        //      block are disabled; its `if (result != 0)` block just never fires.
        // Everything else stays vanilla (the old C#-driven camera era — TownCamera.cs etc. — is deleted).
        // Reversible: restore the guarded vanilla words. Addresses RE'd via Ghidra-EE (docs/town-camera-elf-port-plan).
        internal static void PatchNativeCameraPostPass(FileStream fs, Func<uint, long> ElfOff)
        {
            // CLEAN FREE-CAMERA BASE + STAGE-2 PULL-IN: strip ALL of vanilla's camera collision, then add our OWN
            // smooth pull-in with a space buffer. Sites guarded before any write. (To A/B vanilla's orbit-the-hit-point
            // slide instead, drop the CameraAutoMove stub and NOP its AddAngle 0x169CF8/D14 + AddHeight 0x169D50.)
            const uint StubAddr  = 0x0014B830;   // CheckCameraWidth entry           (guard addiu sp,-0x100)
            const uint RotationGateRightAddr = 0x0016B1E8;   // `clear s6` (bVar4=false, right blocked → gates rotation)
            const uint RotationGateLeftAddr = 0x0016B2FC;   // `clear s4` (bVar5=false, left  blocked → gates rotation)
            const uint CameraAutoMoveAddr   = 0x00169B70;   // CameraAutoMove entry (slide/auto-follow/rotate/height-when-close)
            const uint CeilingRaiseAddr  = 0x0016B6A8;   // vertical-adjust SetHeight (raise to clear a ceiling within 18u)
            const uint ResetDistanceAddr  = 0x0016B724;   // reset: SetDistance(near)   \ fires when clipped-close or too-high
            const uint ResetHeightAddr  = 0x0016B738;   // reset: SetHeight(10)       |  → snaps + flips the camera to the
            const uint ResetAngleAddr  = 0x0016B764;   // reset: AddAngle(flip)      / opposite side of the player
            const uint HeightFloorSnapAddr = 0x0016BC54; // floor-snap SetHeight(5/35): clamps height UP to 5 when it's lower,
                                                    // which blocks the ground-relative DROP (camera below the player when
                                                    // he walks uphill). NOP so height can go < 5 / negative; our baseline
                                                    // keeps the eye BASE_H above the ground under it, so it never clips.
            const uint StickHeightAddr1 = 0x0016B834; // vanilla stick-Y AddHeight (accumulative, height<30 branch) — replaced by
            const uint StickHeightAddr2 = 0x0016B84C; //   our deadzoned absolute stick offset in the pull-in. NOP both.
            const uint PullInAddr = 0x0014B838;
            const uint HookAddr  = 0x0016B5DC;   // jal CheckHitVertical → retarget to our pull-in (s5=buf,s8=count live)
            // Town camera clamps distance to [near=70, far=80] AFTER our hook, easing our pull-in back UP to 70 every
            // frame (line 618-621) — so the pull-in "mostly passes through". NOP the near-clamp + the bVar6 SetDistance
            // so our pull-in owns +0x2D0. (Far-clamp 0x16B994 left as a safety; we already clamp target ≤ BASE 80.)
            const uint DistanceNearClampAddr = 0x0016B9E8;  // AddDistance(+(70-dist)/10) near-clamp → fights pull-in
            const uint DistanceResetAddr   = 0x0016BBCC;  // if(bVar6) SetDistance(70) → hard-resets distance
            // The "too close" reset block (fires when dist < near*0.5 = 35, CONSTANT in the canal) snapped 4 things;
            // we NOPped its SetDistance/SetHeight/AddAngle (RSTD/RSTH/RSTA) but MISSED the SetAngleSoon @0x16B754 —
            // which sets rendered-angle(+0x2DC)=target(+0x2D8), KILLING the angle easing every frame we're pulled in.
            // That snap (size = the easing lag, so bigger the faster you rotate) was the reproducible slide jump. NOP it.
            const uint ResetAngleSoonAddr  = 0x0016B754;  // reset-block SetAngleSoon → kills angle easing (the slide jump)
            const uint ResetAngleSoonMapAddr = 0x0016B7A8;  // sibling SetAngleSoon (MapNo 0x23 only; NOP for consistency)
            // The reset block's LAST effect: a vtable call (**(cam+0x2B8)+8)(cam,-1) = Step(cam,-1), whose param_2<0
            // path does +0x2DC=+0x2D8 — ANOTHER hard angle snap. Fires when dist<35 (constant while pulled in). CSV
            // proved it: rendered angle catches up ~2.4° the frame dist crosses 35. NOP the jalr → block fully inert.
            const uint ResetStepVtableAddr   = 0x0016B7C0;  // reset-block jalr t9 = Step(cam,-1) → snaps +0x2DC (the residual jump)

            void Guard(uint va, uint want, string what) {
                if (RdU32(fs, ElfOff(va)) != want)
                    throw new IOException($"Native-camera site 0x{va:X} ({what}) not vanilla — unmodified Dark Cloud (USA) ISO expected.");
            }
            Guard(StubAddr,  0x27BDFF00, "CheckCameraWidth");
            Guard(RotationGateRightAddr, 0x7000B628, "rotation-gate R"); Guard(RotationGateLeftAddr, 0x7000A628, "rotation-gate L");
            Guard(CameraAutoMoveAddr,   0x27BDFFA0, "CameraAutoMove");
            Guard(CeilingRaiseAddr,  0x0C0492EC, "vertical SetHeight");
            Guard(ResetDistanceAddr,  0x0C0492DC, "reset SetDistance"); Guard(ResetHeightAddr, 0x0C0492EC, "reset SetHeight");
            Guard(ResetAngleAddr,  0x0C0492D4, "reset AddAngle");
            // Camera scratch (stick ease @0x01F10040, E_prev quad @0x01F10050) lives on the MAILBOX DATA page —
            // boot-zeroed heap, no ELF init needed/possible; moved off the code page so per-frame writes stop
            // forcing PCSX2 to re-JIT the camera function (see CodeCaveAddresses cave map).
            Guard(HookAddr,  0x0C052820, "pull-in hook (jal CheckHitVertical)");
            Guard(DistanceNearClampAddr, 0x0C0492E4, "distance near-clamp"); Guard(DistanceResetAddr, 0x0C0492DC, "bVar6 SetDistance");
            Guard(ResetAngleSoonAddr, 0x0C0492CC, "reset SetAngleSoon"); Guard(ResetAngleSoonMapAddr, 0x0C0492CC, "reset SetAngleSoon(map)");
            Guard(ResetStepVtableAddr, 0x0320F809, "reset vtable Step(-1)");

            WrU32(fs, ElfOff(StubAddr + 0), 0x03E00008);   // CheckCameraWidth → jr ra
            WrU32(fs, ElfOff(StubAddr + 4), 0x00001021);   //   addu v0,zero,zero (return 0 → width-slide off)
            WrU32(fs, ElfOff(RotationGateRightAddr), 0x00000000);      // free rotation right (bVar4 stays true)
            WrU32(fs, ElfOff(RotationGateLeftAddr), 0x00000000);      // free rotation left  (bVar5 stays true)
            WrU32(fs, ElfOff(CameraAutoMoveAddr + 0), 0x03E00008);    // CameraAutoMove → jr ra (no collision slide/rotate/height)
            WrU32(fs, ElfOff(CameraAutoMoveAddr + 4), 0x00000000);    //   nop (delay slot)
            WrU32(fs, ElfOff(CeilingRaiseAddr), 0x00000000);       // no ceiling height-rise ("angle goes up")
            WrU32(fs, ElfOff(ResetDistanceAddr), 0x00000000);       // no reset distance snap
            WrU32(fs, ElfOff(ResetHeightAddr), 0x00000000);       // no reset height snap
            WrU32(fs, ElfOff(ResetAngleAddr), 0x00000000);       // no reset angle flip ("reset to opposite side")
            WrU32(fs, ElfOff(DistanceNearClampAddr), 0x00000000);     // no near-clamp ease-up (was forcing dist back to 70)
            WrU32(fs, ElfOff(DistanceResetAddr), 0x00000000);       // no bVar6 SetDistance(70) hard-reset
            WrU32(fs, ElfOff(ResetAngleSoonAddr), 0x00000000);     // no reset SetAngleSoon → angle easing survives (fixes slide jump)
            WrU32(fs, ElfOff(ResetAngleSoonMapAddr), 0x00000000);    // no sibling SetAngleSoon (MapNo 0x23)
            WrU32(fs, ElfOff(ResetStepVtableAddr), 0x00000000);      // no reset Step(cam,-1) → +0x2DC angle snap gone (block inert)
            Guard(HeightFloorSnapAddr, 0x0C0492EC, "height floor-snap SetHeight");
            Guard(0x0016BC0C, 0x0C0492EC, "height CEILING snap SetHeight(60)");   // vanilla snaps +0x2D4 down to 60 every frame it
            WrU32(fs, ElfOff(0x0016BC0C), 0x00000000);                            //   exceeds 60 — capped EVERY tall-cliff mechanism
            WrU32(fs, ElfOff(HeightFloorSnapAddr), 0x00000000); // no floor-snap → height can drop below 5 (camera below player)
            Guard(StickHeightAddr1, 0x0C0492F4, "vanilla stick-Y AddHeight #1");
            Guard(StickHeightAddr2, 0x0C0492F4, "vanilla stick-Y AddHeight #2");
            WrU32(fs, ElfOff(StickHeightAddr1), 0x00000000);    // our deadzoned stick offset replaces the vanilla accumulative one
            WrU32(fs, ElfOff(StickHeightAddr2), 0x00000000);

            // ── STAGE 2: our own smooth pull-in (hosted in the reclaimed CheckCameraWidth slack) ──
            // Wraps `jal CheckHitVertical` @0x16B5DC (s5=CCPoly buffer, s8=poly count live in callee-saved regs):
            // calls the real CheckHitVertical (transparent, returns its v0), then reads MainCamera ref(+0x2C0)/
            // angle(+0x2D8)/height(+0x2D4), casts the 3D sightline ray from ref up to (ref.y+height) at BASE=80 in the
            // angle(+0x2D8) direction, CheckHit(s5,s8,rayFrom→rayTo, mode=0). (Look-ahead removed: it didn't flatten the
            // target discontinuity AND broke on the +0x2D8 wrap flip -π..π ↔ 0..2π making the lead ≈±2π garbage.) mode=0 is
            // critical: it hits ALL polys — mode=1 skipped the tall area-dividing walls (attribute bit 0) while
            // buildings (bit clear) still registered, so the camera passed through walls. AND param_6=1 (NEAREST):
            // CheckHit with param_6=0 returns the FIRST poly in buffer order, which on our 80u ray is often a far
            // one (dist≥80 → clamps to base → no pull-in); param_6=1 tracks the nearest hit. On hit, shrinks
            // distance(+0x2D0) to horiz(hit)−MARGIN(8, the space buffer) — floored at MIN 12 (target ≤72<BASE so no
            // max-clamp needed), then eased toward it at 0.15 SYMMETRIC (fast-in disabled — testing; planned: slow the
            // camera rotation when it would clip instead of snapping in, + height-rise when MIN can't be held).
            // MIN must stay UNDER the wall distance or the floor lands the camera PAST close walls (Queens canal walls
            // ~19-34u out). Trade-off: low MIN lets the camera near the player at buildings — real fix = height-rise
            // (go OVER when forced close), TODO. ⚠ EE-ASM GOTCHAS baked in (see mips_asm.py): FP compares use c.OLT.s
            // (.word 0x46..0034) NOT keystone c.lt.s (0x3C — the R5900 doesn't set cc for it); a nop follows every
            // mtc1 and every FP compare (two EE latency hazards). One-frame-stale angle (line 583 after) imperceptible.
            // ===== CAMERA TUNABLES (tools/stubs/town_camera_collision.s) — edit these; they inject into the template's constant slots
            //       below on each patch (no asm regen). PutVal = single `lui` (integer / .25 step, low16==0);
            //       PutEase = `lui`+`ori` (any float). Indices auto-located from the source; guards trip loudly on drift.
            // ARCHITECTURE: dist target = BaseDistance always (no wall-ray pull-in). Height target = RestHeight + stick;
            //   ceiling probe DUCKS it (tunnel), ground probe FLOORS it (hard, world-space). ALL height motion is
            //   RATE-LIMITED: falls ≤ HeightFallRate/frame in WORLD Y (cameraHeight.bin cave sub @0x27D090 — a falling
            //   player outruns the camera; WarpBreak skips the bound across true warps), climb rises ≤ ClimbRise/
            //   frame (anchored to last APPLIED height, not the eased value — the ease decay otherwise eats the rise).
            //   The height sub also owns the CLIFF logic: pinned+occluded → the boom glides toward the player at
            //   GroundGlideGain·current/frame (progressive ratchet over the lip; floor GlideMinDistance), and while
            //   descending (excess > DescentHold) the boom may shorten but never extend (kills the lip-crossover
            //   bounce). The SWEPT-SLIDE (persisted origin E_prev @0x01F10050, mailbox data page — off the code page
            //   so PCSX2 doesn't re-JIT per frame) resolves wall contact on the authored-normal side via the weighted
            //   d/h/θ decomposition (SlideBias = angle share); |n_t|-scaled friction (head-on undamped →
            //   SlideFriction keep at full tangency); θ REACQUISITION (SlideGain, stick-gated) slides toward rest;
            //   occlusion-gated GEOMETRIC CLIMB h = RestHeight + CLIMB_K·(BASE−d')² (LOS pivot→E_prev, 5th cast, flag
            //   @0x54(sp) — NOT 0x98, the corner-verify spill slot); CORNER VERIFY resolves a second plane (min-norm)
            //   for concave seams. Vanilla height clamps NOP'd BOTH ways (floor snap 0x16BC54 + ceiling snap 0x16BC0C
            //   — the 60-unit ceiling silently capped every tall-cliff mechanism until found). ⚠ EE gotchas:
            //   c.OLT.s/sqrt.s/max.s/min.s are .word-encoded — DERIVE from the formula, fd is bits 10:6 (the fd=31
            //   no-op bug); nop after mtc1/FP-compare; CheckHit args 5-7 = REGISTERS t0/t1/t2 (hitOut/mode/skip) —
            //   set explicitly at every cast, NEVER inherit (stale t2 = the mask-skipping saga). [[native-camera-functions]]
            const float BaseDistance   = 70f;   // resting orbit distance when nothing blocks (vanilla
                                             // EdInitCameraParam's camera_near_dist ~70; was 80 during
                                             // the camera rework — reverted to vanilla feel 2026-08)
            const float RestHeight      = 5f;   // resting eye height above the pivot (flat — no slope-rise/climb anymore)
            const float CeilingProbeDistance   = 80f;  // how far UP the ceiling probe looks for a tunnel roof to duck under
            const float MinCeilingClearance = 4f;// eye stays this far BELOW a detected ceiling (tunnel duck depth)
            const float StickDeadzone = 0.3f; // right-stick Y below this |deflection| (0..1) does nothing
            const float StickScale    = -25f; // manual height offset at full stick deflection (up = raise)
            const float StickEase     = 0.1f; // per-frame ease of the stick offset (LOW = gentle onset)
            const float HeightEase = 0.3f;  // per-frame ease of height toward its target
            const float DistanceEase   = 0.3f; // per-frame ease of horizontal distance toward its target
            const float SlideMargin = 8f;   // swept-slide standoff + proximity-extension reach. KEEP <= MARGIN (else the two setpoints oscillate)
            const float SlideBias = 0.125f; // angle-axis weight² in the slide: 1 = neutral (resists rotation), small = FREE glide (dist/height resolve, rotation flows)
            const float SlideFriction = 0.6f; // contact drag at FULL tangency (keep-floor); head-on contact is undamped — keep = 1 − (1−F)·|n_t|
            float SLIDE_FRICTION_INV = 1f - SlideFriction;   // injected form (asm folds 1−F to save the 1.0 load)
            const float ClimbPeak = 60f;    // height the climb curve reaches at full pinch (d' = 0); the BELL's peak
            float CLIMB_K = (ClimbPeak - RestHeight) / (BaseDistance * BaseDistance);   // quadratic climb gain - zero slope at touch
            const float ClimbRise = 2f;     // climb RE-ENABLED for pull-in only (its intrusion term max(BASE−d', 0)
                                             // is zero at/beyond rest, so it natively fires only when pinched in), at
                                             // the original rate cap. Composes with the height freeze: the clamp chain
                                             // max(min(curve, last+RISE), h_e) ratchets the eye UP over the wall while
                                             // pinned; descent back to rest goes through the ease once d recovers past
                                             // the gate. Occlusion-gated (visible only) — occlusion still moves nothing.
            const float SlideGain = 0f;     // 0 = θ auto-slide DISABLED (same user rule — no automatic motion at/inside
                                             // rest; it only ever acted on contact frames. PutVal steps 0.0625/0.125/0.25)
            const float MinGroundClearance = 6f; // eye never gets closer than this to the ground under it (stick-down guard)
            const float HeightFallRate    = 2f;    // max WORLD-space height drop per frame (absolute descent bound — a falling player outruns the camera)
            const float WarpBreak     = 400f; // world-y discontinuity beyond which the descent bound is skipped (true warps only —
            // the eased desired-height drops ~30% of the offset per frame, so a LONG fall legitimately opens a
            // gap of hundreds of units; 400 misread that as a warp and released the bound mid-fall)
            const float GroundGlideGain = 0f; // 0 = glide DISABLED (same user rule — the pinned+occluded pull-in was the
                                             // camera "trying to clear the occlusion" on its own; occlusion no longer
                                             // drives any automatic movement)
            const float GlideMinDistance = 12f;   // the ground glide never pulls the boom closer than this
            const float DescentHold   = 15f;   // height excess above rest that freezes OUTWARD dist recovery (kills the lip-crossover bounce)
            // Assembled template (378 words) from tools/stubs/town_camera_collision.s — pull-in + ceiling-duck + stick, one-sided _c, no climb. The KNOBS are the consts above, NOT the hex — they get
            // written into the flagged word slots after this literal (PutVal/PutEase, indices guarded). Regenerate this
            // array via mips_asm.py only if the CODE changes. R5900 quirks: c.OLT.s / sqrt.s are .word-encoded; a nop
            // follows every mtc1 and every FP compare.
            uint[] pullIn = LoadWordsResource("Dark_Cloud_Improved_Version.Resources.isoPatch.townCameraCollision.bin", 0x27BDFF60);   // Resources/isoPatch/townCameraCollision.bin (embedded) —
                                                      // assembled from tools/stubs/town_camera_collision.s @0x14B838
            // Inject the tunables above into the template's constant-load slots (indices auto-located from
            // tools/stubs/town_camera_collision.s; guards trip loudly if the array drifts). PutVal = single `lui $t0` (float low16 must
            // be 0 — integers / .25 steps); PutEase = `lui $t0` + `ori $t0`.
            static uint[] LoadWordsResource(string res, uint expectedFirstWord)
            {
                using var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(res)
                    ?? throw new IOException($"Embedded EE function missing: {res} (reassemble its .s in tools/ and rebuild)");
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                byte[] b = ms.ToArray();
                if (b.Length == 0 || (b.Length & 3) != 0)
                    throw new IOException($"EE function resource {res} is malformed ({b.Length} bytes).");
                uint[] w = new uint[b.Length / 4];
                Buffer.BlockCopy(b, 0, w, 0, b.Length);
                if (w[0] != expectedFirstWord)
                    throw new IOException($"{res} doesn't start with the expected prologue (got 0x{w[0]:X8}) — stale or mis-assembled.");
                return w;
            }
            static void PutValIn(uint[] arr, int idx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((b & 0xFFFF) != 0)
                    throw new Exception($"Tunable {nm}={f} isn't a single-lui float (low16!=0).");
                if ((arr[idx] & 0xFFFF0000u) != 0x3C080000u)
                    throw new Exception($"Tunable {nm} slot {idx} moved — refresh indices from the .s.");
                arr[idx] = 0x3C080000u | (b >> 16);
            }
            void PutVal(int idx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((b & 0xFFFF) != 0)
                    throw new Exception($"Camera tunable {nm}={f} isn't a single-lui float (low16!=0, got 0x{b:X8}); use an integer or a .25 step.");
                if ((pullIn[idx] & 0xFFFF0000u) != 0x3C080000u)
                    throw new Exception($"Camera tunable {nm} slot {idx} is not a `lui $t0` — slot indices are stale — reassemble tools/stubs/town_camera_collision.s and refresh them.");
                pullIn[idx] = 0x3C080000u | (b >> 16);
            }
            void PutEase(int luiIdx, int oriIdx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((pullIn[luiIdx] & 0xFFFF0000u) != 0x3C080000u || (pullIn[oriIdx] & 0xFFFF0000u) != 0x35080000u)
                    throw new Exception($"Camera tunable {nm} slots ({luiIdx},{oriIdx}) moved — slot indices are stale — reassemble tools/stubs/town_camera_collision.s and refresh them.");
                pullIn[luiIdx] = 0x3C080000u | (b >> 16);
                pullIn[oriIdx] = 0x35080000u | (b & 0xFFFF);
            }
            float STICK_DZ2 = StickDeadzone * StickDeadzone;   // deadzone² (compared vs stickY²)
            // ⚠ slot indices are +2 from the 2026-08 winding-agnostic insert (jal cameraNormSide + nop at
            //   words 14/15 of town_camera_collision.s) — every slot at/after word 14 shifted by 2.
            PutVal(144, BaseDistance, nameof(BaseDistance));   // resting dist target
            PutVal(436, BaseDistance, nameof(BaseDistance));   // reacquisition rest
            PutVal(453, BaseDistance, nameof(BaseDistance));   // climb intrusion reference
            // NOTE: word 147 (the height-target RestHeight) is now a `lw $t0,0x28($t3)` that reads RestHeight from the
            // CameraRestH mailbox (data-driven — see town_camera_collision.s). The mod writes town-rest (5) there
            // normally and the spot's fishing height while a session is live, so the fishing rest height is a
            // TARGET the camera eases to, not a per-frame hard clamp. So NO PutVal(147) here anymore.
            PutVal(471, RestHeight, nameof(RestHeight));   // climb-curve base (still baked at town rest — climb only RAISES, so it's inert while the fishing rest is higher)
            PutVal(43, CeilingProbeDistance, nameof(CeilingProbeDistance));
            PutVal(155, MinCeilingClearance, nameof(MinCeilingClearance));   // tunnel-duck clearance
            PutVal(168, MinGroundClearance, nameof(MinGroundClearance));
            PutVal(480, ClimbRise, nameof(ClimbRise));   // climb rise rate cap
            PutVal(271, SlideMargin, nameof(SlideMargin));   // proximity-extension reach
            PutVal(349, SlideMargin, nameof(SlideMargin));   // need standoff
            PutVal(562, SlideMargin, nameof(SlideMargin));   // corner second-resolution standoff
            PutVal(442, SlideGain, nameof(SlideGain));   // θ reacquisition
            PutVal(118, StickScale, nameof(StickScale));
            PutEase(110, 111, STICK_DZ2, nameof(STICK_DZ2));
            PutEase(129, 130, StickEase, nameof(StickEase));
            PutEase(181, 182, HeightEase, nameof(HeightEase));
            PutEase(197, 198, DistanceEase, nameof(DistanceEase));
            PutEase(374, 375, SlideBias, nameof(SlideBias));
            PutEase(423, 424, SLIDE_FRICTION_INV, nameof(SLIDE_FRICTION_INV));
            PutEase(466, 467, CLIMB_K, nameof(CLIMB_K));
            if (pullIn.Length > 634)   // 0x14B838 + 634*4 == 0x14C220 == set2DSprite_Start: flush, no headroom left
                throw new IOException($"townCameraCollision.bin is {pullIn.Length} words — overruns set2DSprite_Start @0x14C220 (max 634).");
            for (int i = 0; i < pullIn.Length; i++)
                WrU32(fs, ElfOff(PullInAddr + (uint)(i * 4)), pullIn[i]);
            // Camera-cave auxiliary bank (tools/stubs/camera_norm_side.s, dead CharaChange region past
            // fishlineUncastGate): entry @0x228F00 = gather-count export (Mailbox.CamGatherCount; called by
            // the cave at word 14), SubA @0x228F40 / SubB @0x229000 = per-contact WINDING-AGNOSTIC normal
            // prep for the swept-slide / corner-verify (normalize + flip N̂ to E_prev's side of the hit
            // plane, so vanilla `_c`/`_v` meshes work regardless of authored winding — a buffer-wide
            // ref-side flip was tried first and pulled the camera inside closed shells' far walls).
            uint[] normSide = LoadWordsResource("Dark_Cloud_Improved_Version.Resources.isoPatch.cameraNormSide.bin", 0x3C0A01F1);
            for (int i = 0; i < normSide.Length; i++)
                WrU32(fs, ElfOff(0x00228F00 + (uint)(i * 4)), normSide[i]);
            // ── FISHING LINE CANAL CLAMP (cave @0x229100 in the bank above, camera_norm_side.s) ──
            // Wraps EdMoveChara's single FishLineStep call: after the real Verlet step, in QUEENS ONLY,
            // the rope tail (bobber point[18], hang-down line, uki/hook clusters — pos AND old_p, so the
            // z-velocity zeroes) is clamped against the FAR canal wall (|z| 48; walls ~±50): a cast toward
            // the wall stops dead there and the bobber drops into the water at its base. Only the wall
            // opposite the rod is clamped, so reel-in is never obstructed. The cast button always works
            // (v1 rejected the cast at the button — bad feel, and its facing ray over-rejected).
            Guard(0x0016D314, 0x0C06A8D0, "fishing line-clamp hook (jal FishLineStep)");
            WrU32(fs, ElfOff(0x0016D314), 0x0C08A440);   // jal 0x229100 (FishLineClamp wrapper)
            Guard(0x0027D090, 0x00000000, "world-height cave (ex-autorotate area, zero words in vanilla)");
            uint[] heightFn = LoadWordsResource("Dark_Cloud_Improved_Version.Resources.isoPatch.cameraHeight.bin", 0x27BDFFE0);
            // REACQUISITION GATE (word 3 of the sub, 2026-08, HEIGHT-ONLY since the recovery fix): when
            // wall-pinched strictly inside rest the sub freezes only the HEIGHT target at current — the
            // DIST target always seeks BaseDistance so a wall-pinned camera recovers back out to resting
            // distance once unconstrained (the swept-slide caps it against walls meanwhile); height
            // unfreezes as soon as distance recovers past the gate. Slot 4 = the gate threshold, BASE−1:
            // STRICT so open-field rest (d eases asymptotically to BASE) never freezes — the right-stick
            // height control must keep working at rest.
            // ⚠ indices +22 from the 2026-08 XZ warp-skip insert (camera_height.s: a cross-map warp can
            //   land at nearly the same world y — Queens→Brownboo Δy≈110, under the 400 y-break — so the
            //   y-only test left the descent bound grinding the eye down from the SOURCE map's height =
            //   the warp-arrival pan. E_prev >128u from the ref in X or Z now skips the bound.)
            PutValIn(heightFn, 4, BaseDistance - 1f, "REACQ_GATE");
            PutValIn(heightFn, 43, WarpBreak, nameof(WarpBreak));
            PutValIn(heightFn, 50, HeightFallRate, nameof(HeightFallRate));
            PutValIn(heightFn, 68, GroundGlideGain, nameof(GroundGlideGain));
            PutValIn(heightFn, 73, GlideMinDistance, nameof(GlideMinDistance));
            PutValIn(heightFn, 81, DescentHold, nameof(DescentHold));
            for (int i = 0; i < heightFn.Length; i++)
                WrU32(fs, ElfOff(0x0027D090 + (uint)(i * 4)), heightFn[i]);
            WrU32(fs, ElfOff(HookAddr), 0x0C052E0E);        // retarget jal CheckHitVertical → our pull-in @0x14B838
        }

        // ── Fishing camera → center on the bobber instead of the player/bobber midpoint ──────────────
        // While the line is cast (chara_fishing states with the hook in water), EdMoveChara aims the
        // follow-camera at the MIDPOINT of the player and the float:
        //     FishLineGetUki(&t);              // t = bobber world pos          @0x16D0B4
        //     sceVu0AddVector(&t,&t,&player);  // t = bobber + player           @0x16D0C8
        //     sceVu0ScaleVector(0.5f,&t,&t);   // t = (bobber + player) * 0.5   @0x16D0E0
        //     SetFollow(t.x,t.y,t.z,cam);      // camera looks at the midpoint  @0x16D0F8
        // NOP-ing the add+scale calls leaves `t` = the raw bobber position, so the shot centers on the
        // float where the action is. Both delay slots are already nop, so this is two jal->nop writes.
        // Runs only during fishing (the enclosing block is gated on the fishing flag), so it needs no
        // town gating and improves every fishing spot, vanilla or custom. (Distance/height are vanilla now —
        // the town-camera tweak patches were removed; the camera is being rewritten from scratch.)
        internal static void PatchFishingCameraTarget(FileStream fs, Func<uint, long> ElfOff)
        {
            (uint va, uint vanilla, string what)[] sites =
            {
                (0x0016D0C8, 0x0C0485E8, "jal sceVu0AddVector (bobber += player)"),
                (0x0016D0E0, 0x0C0485FA, "jal sceVu0ScaleVector (midpoint *= 0.5)"),
            };
            foreach (var s in sites)
                if (RdU32(fs, ElfOff(s.va)) != s.vanilla)
                    throw new IOException($"Fishing-camera site 0x{s.va:X} ({s.what}) is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            foreach (var s in sites)
                WrU32(fs, ElfOff(s.va), 0x00000000);   // nop -> keep the bobber position as the camera target
        }

        /// <summary>Make the FISHING CAMERA HEIGHT data-driven instead of a hard-coded 40.
        ///
        /// <c>EdMoveChara</c> forces the fishing camera angle with a literal <c>SetHeight(40.0)</c>:
        /// <code>
        ///   0x16C2DC  lui  $2,0x4220     ; 40.0f
        ///   0x16C2E0  mtc1 $2,$f12
        ///   0x16C2E8  jal  SetHeight
        /// </code>
        /// It re-runs EVERY FRAME of a session, so a runtime write to the camera loses the race. Instead we
        /// rewrite those two instructions to LOAD the height from a mod-owned word
        /// (<see cref="CodeCaves.Mailbox.FishCamHeight"/>), turning a code constant into per-spot data:
        /// <code>
        ///   lui  $2,HI(FishCamHeight)
        ///   lwc1 $f12,LO(FishCamHeight)($2)
        /// </code>
        /// 40 keeps the vanilla look-down-into-the-water angle; the Queens canal spot writes 5 (the standard
        /// town height) because there the player stands IN the water and the high angle fights the view.
        /// ⚠ The word is read every frame in EVERY town, so the mod seeds it to 40 at startup and re-asserts it
        /// per tick — a 0 there would drop the camera to height 0.</summary>
        internal static void PatchFishingCameraHeight(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint LuiAddr = 0x0016C2DC, Mtc1Addr = 0x0016C2E0;
            const uint VanillaLui = 0x3C024220, VanillaMtc1 = 0x44826000;   // lui $2,0x4220 (40.0f) ; mtc1 $2,$f12
            uint gotLui = RdU32(fs, ElfOff(LuiAddr)), gotMtc1 = RdU32(fs, ElfOff(Mtc1Addr));
            if (gotLui != VanillaLui || gotMtc1 != VanillaMtc1)
                throw new IOException($"Fishing camera-height site 0x{LuiAddr:X} is not vanilla " +
                                      $"(got 0x{gotLui:X8}/0x{gotMtc1:X8}) — is this an unmodified Dark Cloud (USA) ISO?");

            const uint SLOT = (uint)(CodeCaves.Mailbox.FishCamHeight & 0x1FFFFFFF);   // guest (PINE addr minus the 0x20000000 view)
            uint hi = SLOT >> 16, lo = SLOT & 0xFFFF;
            if (lo >= 0x8000) hi += 1;                       // lwc1's offset is SIGNED — compensate like the assembler
            WrU32(fs, ElfOff(LuiAddr),  0x3C020000u | hi);                      // lui  $2,hi
            WrU32(fs, ElfOff(Mtc1Addr), 0xC4000000u | (2u << 21) | (12u << 16) | lo);  // lwc1 $f12,lo($2)
        }

        // ── Fishing camera-collision gather: see ALL camera walls while fishing ──────────────────────
        // EdMoveChara's camera block gathers _c polys for every probe/sweep via PickUpCameraPoly — but with
        // attribute mask 1 while FISHING (DAT_01d19714 != 0) vs 0xffff normally (branch @0x16AF38). With
        // mask 1 almost no walls are gathered, so the bobber-pinned fishing camera (and the mod's swept-slide,
        // which constrains against this same gather) can sail straight through buildings — vanilla shipped it
        // this way because its fishing camera barely moved; the bobber-centred view + extended cast expose it.
        // Fix: one word — the fishing path's `li a3,1` becomes `ori a3,zero,0xffff`, same mask as walking.
        internal static void PatchFishingCameraGather(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint FishingMaskAddr = 0x0016AF4C;   // fishing-path `li a3,0x1` feeding jal PickUpCameraPoly @0x16AF50
            uint got = RdU32(fs, ElfOff(FishingMaskAddr));
            if (got == 0x3407FFFF) return;     // already patched (idempotent re-run)
            if (got != 0x24070001)
                throw new IOException($"Fishing camera-gather mask site 0x{FishingMaskAddr:X} is not vanilla `li a3,1` (got 0x{got:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(FishingMaskAddr), 0x3407FFFF);   // ori a3,zero,0xffff — full camera-poly mask while fishing
        }
    }
}
