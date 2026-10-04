using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.BootCave;
using static Dark_Cloud_Improved_Version.ElfCameraPatches;
using static Dark_Cloud_Improved_Version.ElfWaterPatches;
using static Dark_Cloud_Improved_Version.ElfCanalPatches;
using static Dark_Cloud_Improved_Version.ElfFishingPatches;
using static Dark_Cloud_Improved_Version.ElfCatPatches;
using static Dark_Cloud_Improved_Version.ElfDeadFunctionPatches;
using static Dark_Cloud_Improved_Version.ElfShotPackPatches;
using static Dark_Cloud_Improved_Version.ElfDamagePatches;
using static Dark_Cloud_Improved_Version.ElfFrameChainPatches;
using static Dark_Cloud_Improved_Version.ElfToanMeleePatches;
using static Dark_Cloud_Improved_Version.ElfConfusePatches;
using static Dark_Cloud_Improved_Version.ElfWeaponPatches;
using static Dark_Cloud_Improved_Version.ElfTownAllyPatches;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// ELF (SCUS_971.11) patching: the boot cave that registers fishsign.img (its addresses in BootCave), the cave SEGMENT
    /// hijacked out of phdr3, ElfPatchAndCrc (program-header resolve + the ordered Patch* dispatch + new PCSX2 CRC) and the
    /// map-carve remainder. The feature patches live by area: ElfFishingPatches, ElfCameraPatches, ElfWaterPatches (the
    /// order-coupled water redraw), ElfCanalPatches (the Queens canal's hooks), ElfCatPatches, ElfDeadFunctionPatches (the
    /// dead-function hosts' takeover), ElfShotPackPatches, ElfDamagePatches, ElfFrameChainPatches (the camera-pass frame chain),
    /// ElfToanMeleePatches, ElfConfusePatches, ElfWeaponPatches (the per-weapon odds and ends) and ElfTownAllyPatches (the
    /// swapped-in town ally's motion, ladder and mark).
    /// </summary>
    internal static class ElfPatches
    {
        internal static byte[] BuildCave()
        {
            uint[] w = {
                Addiu(sp, sp, -0x20), Sw(a0, 0x14, sp), Sw(a1, 0x18, sp),
                Move(a0, a1), Lui(a1, StringAddr >> 16), Ori(a1, a1, StringAddr & 0xFFFF), Addiu(a2, zero, 0),
                Jal(GetPackFile), 0,
                Lui(t0, DiagAddr >> 16), Sw(v0, (int)(DiagAddr & 0xFFFF), t0),
                Move(a1, v0), Lui(a0, SysTexMgr >> 16), Ori(a0, a0, SysTexMgr & 0xFFFF),
                Addiu(a2, zero, -1), Addiu(a3, zero, 0), Addiu(t0, zero, 0),
                Jal(EnterIMGFile), 0,
                Lw(a0, 0x14, sp), Lw(a1, 0x18, sp), Addiu(a2, zero, 0),
                Jal(LoadFile), 0,
                Addiu(sp, sp, 0x20), J(RejoinVa), 0,
            };
            var b = new byte[w.Length * 4];
            for (int i = 0; i < w.Length; i++) Array.Copy(BitConverter.GetBytes(w[i]), 0, b, i * 4, 4);
            if (b.Length > MaxBytes) throw new InvalidOperationException($"cave {b.Length}B > {MaxBytes}B");
            return b;
        }

        internal static uint ElfPatchAndCrc(FileStream fs, Rec elf)
        {
            long elfIso = (long)elf.Ext * SectorBytes;
            byte[] eh = Rd(fs, elfIso, 0x34);
            uint phoff = U32(eh, 0x1c); ushort phent = BitConverter.ToUInt16(eh, 0x2a), phnum = BitConverter.ToUInt16(eh, 0x2c);
            long pOff = -1, pVa = -1;
            for (int i = 0; i < phnum; i++)
            {
                byte[] ph = Rd(fs, elfIso + phoff + i * phent, 24);
                uint typ = U32(ph, 0), off = U32(ph, 4), va = U32(ph, 8), fsz = U32(ph, 16);
                if (typ == 1 && fsz > 0 && va <= DetourVa && DetourVa < va + fsz) { pOff = off; pVa = va; break; }
            }
            if (pOff < 0) throw new IOException("No PT_LOAD covers the patch site — wrong ISO/version.");
            // va → ISO file offset. Two segments: the mod's own cave segment (the hijacked phdr3, guest
            // [ElfCave.RegionStart, ElfCave.RegionEnd) ↔ file [SegmentFileOff, +size)), else the main
            // phdr0 linear map. HijackPhdr3CaveSegment (below) creates the former BEFORE any cave write.
            long ElfOff(uint va) =>
                (va >= ElfCave.RegionStart && va < ElfCave.RegionEnd)
                    ? elfIso + ElfCave.SegmentFileOff + (va - ElfCave.RegionStart)
                    : elfIso + pOff + (va - pVa);

            // Create the cave segment FIRST — every ElfCave-targeted write below lands in its file span.
            HijackPhdr3CaveSegment(fs, elfIso, phoff, phent, phnum, elf.Size);

            byte[] cave = BuildCave();
            if (RdU32(fs, ElfOff(DetourVa)) != Jal(LoadFile) || RdU32(fs, ElfOff(DetourVa + 4)) != 0)
                throw new IOException("Boot-loader patch site is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            byte[] caveWas = Rd(fs, ElfOff(CaveAddr), cave.Length);
            foreach (byte x in caveWas) if (x != 0) throw new IOException("Boot-cave region not empty — unexpected ISO.");

            Wr(fs, ElfOff(StringAddr), Encoding.ASCII.GetBytes("fishsign.img\0"));
            Wr(fs, ElfOff(CaveAddr), cave);
            WrU32(fs, ElfOff(DetourVa), J(CaveAddr));

            PatchClaimDeadFunctionHosts(fs, ElfOff);     // the dead debug functions that host caves (DebugInfomationDraw, DebugItemGetKey/Draw, DebugInfomationIF) return at once — before every cave patch; nothing else writes their first words
            PatchFishingLoadFish(fs, ElfOff);
            PatchFishBox(fs, ElfOff);

            PatchNativeCameraPostPass(fs, ElfOff);       // native occlusion camera (collision, pull-in, height — all ELF-baked)
            PatchFishingCameraTarget(fs, ElfOff);        // center the fishing shot on the bobber (kept)
            PatchFishingCameraHeight(fs, ElfOff);        // fishing camera height 40 -> per-spot data word (canal wades at 5)
            PatchFishingCameraGather(fs, ElfOff);        // fishing camera-collision gather: mask 1 -> 0xffff (see ALL camera walls while fishing)
            PatchFishingUncastGate(fs, ElfOff);          // invalid-cast auto-uncast: 31-frame delay -> 4, height check gated on a SETTLED bobber
            PatchDrawWaterCompaction(fs, ElfOff);        // frees the cave the water-redraw hook (below) lives in
            PatchWaterRedraw(fs, ElfOff);                 // moves the water draw after the character ONLY while the wading mailbox is armed (order-gate cave; unarmed = vanilla order, fixes the Matataki-falls DOF artifact)
            PatchCapeEarlyDraw(fs, ElfOff);               // AFTER PatchWaterRedraw: EARLY_STUB also draws the cape early (survives falls)
            PatchCanalEvictFadeHook(fs, ElfOff);          // fully-black fade frame → canal tide-evict map-jump (native, flag-gated)
            PatchQueensSprayHook(fs, ElfOff);             // MainDraw effect step → spray emitters at the Queens canal waterfalls (table-driven)
            PatchSprayBiasShim(fs, ElfOff);               // EffectWaterSpray → add a per-emitter velocity bias (mist facing + height)
            PatchFishLineSplit(fs, ElfOff);               // fishing rope: per-segment rest length (distpAbove/distpBelow) split at anchor 18
            PatchStiltsHeal(fs, ElfOff);                  // Brownboo stilts: re-upload scene bank 1 after FishLineDraw, before the waterside redraw (v4; chains the water-redraw jal)
            PatchCatPelletFollow(fs, ElfOff);             // Divine Beast cat: native pellet follower cave (the dun.bin hook is in DunPatches)
            PatchXiaoMeleeFlinch(fs, ElfOff);             // Divine Beast cat: its melee-type hits may stagger (dun.bin hook in DunPatches)
            PatchCatGlowDraw(fs, ElfOff);                 // Divine Beast cat: blue torch-glow at its torso (dun.bin hooks in DunPatches)
            PatchCatSpherePercent(fs, ElfOff);            // Divine Beast cat: a hurt sphere may admit the cat's kick (spare[1]) at its own % (spare[0]) — Minotaur Joe's face
            PatchCatGuardBypass(fs, ElfOff);              // Divine Beast cat: its hits pass an enemy's guard window (mimics re-register theirs faster than the mod can crush them)
            PatchGuardCrush(fs, ElfOff);                  // crush-marked hits pass every guard window (hooked in front of the cat's cave)
            PatchGuardMask(fs, ElfOff);                   // …and its second link: the guard windows the mod switches off per enemy (GuardGate)
            PatchSetClearsMark(fs, ElfOff);               // …and every entry the engine makes starts unmarked (a reused entry kept the cat's crush mark)
            PatchConfuseAbility(fs, ElfOff);              // the Confuse weapon ability: shown in the status window, native to Babel's Spear
            PatchCatCapeTint(fs, ElfOff);                 // Divine Beast cat: the Super Steve cape draws under its own ambient, not the cat's
            PatchCatMaskTint(fs, ElfOff);                 // …and its mask does too, reached through a private vtable rather than a hook
            PatchAutoGuardMatch(fs, ElfOff);             // reaction 5: guard spark, then ignored outright (with DunPatches' hook)
            PatchBombReaction(fs, ElfOff);               // item-bomb blasts: hardcoded reaction 3 -> a mod-owned data word
            PatchChargeHitRadius(fs, ElfOff);            // Toan's lunge/whirl hit radii: baked immediates -> mod-owned data words
            PatchMeleeKickStrength(fs, ElfOff);          // Toan's combo-3/4/5, lunge and whirl kick strengths: baked immediates -> mod-owned data words (pnach-seeded vanilla)
            PatchNameDrawGate(fs, ElfOff);               // lock-on name plate: its getter ANDs in NOT CodeCaves.NameHide
            PatchStrideScale(fs, ElfOff);                // Toan's stride on motion 33 × CodeCaves.StrideScale (the dun hook is in DunPatches)
            PatchCameraPin(fs, ElfOff);                  // the camera held at a world height while CodeCaves.CameraPin is set (the dun hook is in DunPatches)
            PatchVerticalDrive(fs, ElfOff);                  // the judgement blade's fall stepped by the engine once a frame (chained after the camera pin)
            PatchBladeSpin(fs, ElfOff);                  // …then chara slot 3's yaw turned by CodeCaves.BladeSpin a frame (Babel's spear), before the WHP bill
            PatchFallDrive(fs, ElfOff);                  // …and the blade fall's mode 4 (falling and following, drive rows) between the two
            PatchFollow(fs, ElfOff);                     // …and the follow cave after it (a point carried with a unit: the Terra stars)
            PatchWhpBill(fs, ElfOff);                    // a weapon-HP bill the mod posts (CodeCaves.WhpBill) taken by the engine's own drain — SwordDmgCheck1 — once a frame (the chain's tail)
            PatchLungeGravity(fs, ElfOff);               // the charge lunge's gravity × (1 + CodeCaves.LungeGravityExtra): the seed cave + its main hook (the dun hook is in DunPatches)
            PatchSolarBladeTint(fs, ElfOff);              // the Sun Sword's blade too (BladeTint): the rigid-mesh class's DrawVu1, into the mask cave's body
            PatchCatCopyQueue(fs, ElfOff);                // the cat's mesh copy runs inside the machine instead of over PINE
            PatchPropPelletFollow(fs, ElfOff);            // a chara-slot prop on one of Xiao's pellets — the Matador's charged shot (the hook in DunPatches now lands here)
            PatchBorrowedShotsEnter(fs, ElfOff);            // a species' shot config, borrowed by an ability, entered into every floor's shot pack (dun.bin hook in DunPatches)
            PatchSharedShots(fs, ElfOff);                 // the monster shot pack's five slots shared among every config a floor needs (the step hook in DunPatches)
            PatchPelletSprite(fs, ElfOff);                // a player pellet drawn as the sprite of the item id the mod names (Super Steve with a slingshot's sphere)
            PatchPelletPlant(fs, ElfOff);                 // a player pellet whose damage word is negative plants nothing on contact — it just ends (ZeusShot's bolt pellets)
            PatchPelletContact(fs, ElfOff);               // every player pellet contact recorded (slot, enemy or wall, the point) for the mod, on the engine's frame
            PatchGemDamage(fs, ElfOff);                   // a thrown gem's burst damage × CodeCaves.GemDamageFactor (the Crysknife doubles it)
            PatchConfuseProc(fs, ElfOff);                // the Confuse ability's on-hit roll (in the dead debug item host the stars caves share)
            PatchSecondEffect(fs, ElfOff);                // the second main-character effect instance stepped and drawn beside the live one on demand (Babel's Spear)
            PatchSpearBlock(fs, ElfOff);
            PatchRockShadow(fs, ElfOff);
            PatchUngagaNoDrain(fs, ElfOff);               // Ungaga's charge-effect hits and Babel's spikes cost no weapon HP (the charge's per-shot bill stays)                  // a solid column enemies cannot walk through, while armed (Babel's risen spear)
            PatchSteelLevelUp(fs, ElfOff);                // the Steel Slingshot's level-ups: endurance and max WHP grow twice as much
            PatchCircleEffects(fs, ElfOff);               // the magic circles, every magnitude from CodeCaves.CircleTable (the cave in DebugInfomationIF's body; the dun hook is in DunPatches)
            PatchCallRequest(fs, ElfOff);                 // a native call the mod posts (CodeCaves.CallRequest), made from the camera pass once a frame (the chain's tail)
            PatchFlameSpacing(fs, ElfOff);                // Osmond's flamethrower reach from a mailbox word (the Skunk doubles it)
            PatchCatPalette(fs, ElfOff);                  // …and the cape/mask take the equipped weapon's element colour there too
            PatchCatGlowPalettes(fs, ElfOff);             // the six glow ramps (data) …
            PatchMirageHazeDraw(fs, ElfOff);              // Mirage: the heat shimmer drawn at the clone itself (dun.bin hook in DunPatches)
            PatchSuperSteveIconDraw(fs, ElfOff);          // Super Steve: the attached sphere's weapon icon over Steve on the dungeon HUD (dun.bin hooks in DunPatches)
            PatchCatGlowPalette(fs, ElfOff);              // … and the cave that paints one of them into the 8-bit glow disc
            PatchBlizzardIceImmunity(fs, ElfOff);         // Blizzard takes no ice damage (species-table IceRes 100 → 0, like Ice Gemron)
            PatchXiaoBuildUp(fs, ElfOff);                 // Xiao's build-up tree: Hardshooter → Double Impact only, Double Impact → Matador only
            PatchFishingPrizeSlingshot(fs, ElfOff);       // the fishing prize exchange sells the Flamingo for 1000 FP (vanilla: the Matador for 1400)
            PatchMapCarveRemainder(fs, ElfOff);           // the monster pool = the (grown) map carve minus the floor's map data (DunPatches grows the carve)
            PatchIdleMotionOverride(fs, ElfOff);          // town idle motion (char+0xc68): idle(0)+mailbox → override index (idle→sit for the swapped-in cat); run/walk untouched
            PatchLadderRefusal(fs, ElfOff);               // town ladder-mount gate: BlockLadder mailbox → skip EdInitHashigo + climbing flag (non-Toan ally can't climb) and raise RefusalRequested
            PatchExclamationHeight(fs, ElfOff);           // player "!" mark Y store: add ExclamationYBoost mailbox (0 = vanilla) → lift the mark off a shorter swapped-in ally's mesh (the cat)
            // (ally-swap buffer grow removed — every ally now fits the vanilla arenas; see the note in ElfTownAllyPatches)

            byte[] pelf = Rd(fs, elfIso, (int)elf.Size);
            uint crc = 0;
            for (int i = 0; i < pelf.Length / 4; i++) crc ^= U32(pelf, i * 4);
            return crc;
        }

        // ── The ELF cave SEGMENT: hijack the degenerate phdr3 into a real PT_LOAD ────────────────────
        // SCUS_971.11 ships 4 program headers; phdr3 is a DEGENERATE placeholder (PT_LOAD, filesz=0,
        // MEMSZ=0 — it loads and reserves nothing). Rewrite it to load file span
        // [ElfCave.SegmentFileOff, +0x4000) at guest [ElfCave.RegionStart, RegionEnd): that file span is
        // dead .reldun debug data BEYOND every phdr's file extent (phdr0 loads only 0x100..0x1a2480;
        // phdr1-3 have filesz=0), so PCSX2 never reads it — and RE tooling uses the PRISTINE extracted
        // ELF, so clobbering it in the PATCHED ISO loses nothing. The guest band is inside the mod's
        // scanner-proven clean heap tail (see CodeCaveAddresses ElfCave doc). The loader writes the bytes
        // at BOOT — cold, before any recompilation — so direct j/jal into the caves is safe.
        // The whole span is ZERO-FILLED here (deterministic content between the caves; the debug garbage
        // would otherwise persist), so this MUST run before any ElfCave-targeted cave write.
        internal static void HijackPhdr3CaveSegment(FileStream fs, long elfIso, uint phoff, ushort phent, ushort phnum, uint elfSize)
        {
            const uint SegVa   = ElfCave.RegionStart;
            const uint SegOff  = ElfCave.SegmentFileOff;
            const uint SegSize = ElfCave.RegionEnd - ElfCave.RegionStart;   // 0x4000 (it can never grow past 0x1FB4000 — runtime data there)

            if (phnum != 4)
                throw new IOException($"Expected 4 ELF program headers, got {phnum} — wrong ISO/version.");
            if (elfSize < SegOff + SegSize)
                throw new IOException($"ELF too small (0x{elfSize:X}) for the cave segment span 0x{SegOff:X}..0x{SegOff + SegSize:X}.");
            // The span must lie beyond every phdr's FILE extent (it holds dead debug data, loaded by nobody).
            for (int i = 0; i < phnum; i++)
            {
                byte[] p = Rd(fs, elfIso + phoff + i * phent, 32);
                uint off = U32(p, 4), fsz = U32(p, 16);
                if (i != 3 && off + fsz > SegOff)
                    throw new IOException($"phdr{i} file extent 0x{off:X}+0x{fsz:X} reaches the cave segment span @0x{SegOff:X} — unexpected ELF layout.");
            }

            long p3 = elfIso + phoff + 3 * phent;
            byte[] ph3 = Rd(fs, p3, 32);
            // vanilla phdr3, all 32 bytes exactly: type=1 off=0x1a2480 vaddr=paddr=0x1f06b00 filesz=0 memsz=0 flags=6 align=0x10
            byte[] vanilla =
            {
                0x01,0x00,0x00,0x00, 0x80,0x24,0x1A,0x00, 0x00,0x6B,0xF0,0x01, 0x00,0x6B,0xF0,0x01,
                0x00,0x00,0x00,0x00, 0x00,0x00,0x00,0x00, 0x06,0x00,0x00,0x00, 0x10,0x00,0x00,0x00,
            };
            for (int i = 0; i < 32; i++)
                if (ph3[i] != vanilla[i])
                    throw new IOException($"phdr3 is not the vanilla degenerate placeholder (byte {i}: got 0x{ph3[i]:X2}) — unmodified Dark Cloud (USA) ISO expected.");

            // (SegOff % 0x80 == 0 and SegVa % 0x80 == 0, satisfying p_align.)
            WrU32(fs, p3 + 0,  1);         // p_type   = PT_LOAD
            WrU32(fs, p3 + 4,  SegOff);    // p_offset
            WrU32(fs, p3 + 8,  SegVa);     // p_vaddr
            WrU32(fs, p3 + 12, SegVa);     // p_paddr
            WrU32(fs, p3 + 16, SegSize);   // p_filesz
            WrU32(fs, p3 + 20, SegSize);   // p_memsz
            WrU32(fs, p3 + 24, 7);         // p_flags  = RWX
            WrU32(fs, p3 + 28, 0x80);      // p_align

            Wr(fs, elfIso + SegOff, new byte[SegSize]);   // zero-fill the whole span
        }

        /// <summary>BtMapJumpLoad sizes the monster pool as `0xA7F80 − map.used` (`lui v0,0xA; ori a1,v0,0x7F80` @0x1B2724):
        /// the same 688,000 → 718,000 as DunPatches' map carve, or the pool would end 30,000 units short of its memory.</summary>
        internal static void PatchMapCarveRemainder(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Site = 0x001B2728;                                                   // the ori; the lui 0xA before it is unchanged
            ReplaceWords(fs, ElfOff, Site, new[] { 0x34457F80u }, new[] { DunPatches.MapCarveGrownWord },
                         cur => $"BtMapJumpLoad's map-carve remainder @0x{Site:X} is not vanilla ({cur[0]:X8}) — unmodified Dark Cloud (USA) ISO expected.",
                         (Site - 4, 0x3C02000Au));
        }
    }
}
