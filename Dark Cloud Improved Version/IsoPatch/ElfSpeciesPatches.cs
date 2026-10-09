using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The species table's extension (SpeciesRows): the table's one reader, CMonstorUnit::SetupBaseModel, forms
    /// <c>&amp;MonstorTable[model_no]</c> through the species-lookup stub (tools/stubs/species_lookup.s at SmoothRestCave.SpeciesLookup) —
    /// the lui/addiu at 0x1DFEE0 become <c>jal stub; nop</c>, the addu after them stays — and an index past the 167 vanilla rows
    /// resolves into CodeCaves.SpeciesRows, the data page the cave segment loads in front of the band (ElfCave.DataPageStart): each
    /// row is the vanilla record of the species it is modelled on with its EnemyData fields over it (SpeciesRows.Build).</summary>
    internal static class ElfSpeciesPatches
    {
        private const uint HookSite = 0x001DFEE0;                                       // lui v0, %hi(MonstorTable); addiu v0, v0, %lo(MonstorTable)
        private static readonly uint[] HookVanilla = { 0x3C020028, 0x2442FB00 };

        /// <summary>The Bomb Gemron's two shots, both ending in the item bomb's blast (SHOT_END_EFFECT_BOMB → SetBombEffect: the item bomb's
        /// sprites at the config's scale, its shock ring above scale 1, and one knockdown entry of radius 20 × scale with the config's
        /// damage, aimed at the config's target) — the blast the Big Bang's shots and drop draw. Neither plants a hit of its own (every
        /// radius 0); a shot explodes when it reaches the impact or the burst phase (contact — the player's head point, his feet + 14–18,
        /// within 6 of its next point; the Gemron's script aims it there, BombGemronBake —, a wall, or its time running out) because
        /// those phases have no motion. The drawing model of both is
        /// <c>g_wave2</c>, the apple pack wearing the bomb (BombGemronBake.BombShotPack).
        ///  · config 8 (<c>g_wave2</c>, unused): the thrown bomb — <c>ringo_ex</c>'s flight (the apple it throws), at the Big Bang pellet's
        ///    blast scale 0.5;
        ///  · config 27 (the second <c>f_boll_3</c>, unused): the death and self-destruct blast — no flight and no life, so it goes off where
        ///    the script places it, at <see cref="BlastScale"/> (the Big Bang drop's).
        /// Both carry <see cref="RadiusPerScale"/> in their padding halfword +0x56: PatchBombRadius's cave makes the hit radius and the
        /// shock ring 25 × scale (12.5 and no ring for the throw, 50 and 50 for the death blast).</summary>
        private const int ThrowConfig = 8, BlastConfig = 27, ThrowSource = 4;
        internal const float ThrowScale = 0.5f, BlastScale = 2f;
        internal const ushort RadiusPerScale = 25;                                      // both blasts' hit radius and shock ring per unit of scale (PatchBombRadius; the engine's are 20 / 30) — the Big Bang's rule too
        internal const int ThrowDamage = 150, BlastDamage = 150;
        private const string Model = "g_wave2", ThrowSourceName = "ringo_ex";
        private const int ShotEndEffectBomb = 100, Target = 1, Knockdown = 3, CfgBlastRadius = 0x56;   // +0x56: padding in every vanilla config

        internal static void PatchBombConfigs(FileStream fs, Func<uint, long> ElfOff)
        {
            long table = ShotEffectPack.CfgTable - 0x20000000L;
            uint Cfg(int index) => RdU32(fs, ElfOff((uint)(table + index * 4)));
            byte[] src = Rd(fs, ElfOff(Cfg(ThrowSource)), ShotEffectPack.CfgSize);
            if (NameAt(src, 0, 16) != ThrowSourceName) throw new IOException($"shot config {ThrowSource} is not {ThrowSourceName}");
            Expect(fs, ElfOff, Cfg(ThrowConfig), ThrowConfig, "g_wave2", 0);
            Expect(fs, ElfOff, Cfg(BlastConfig), BlastConfig, "f_boll_3", 0x401);

            byte[] thrown = Bomb(src, ThrowScale, ThrowDamage);                          // ringo_ex's speeds 0.2 / 1.5 and its 160-frame flight
            WriteBytes(fs, ElfOff, Cfg(ThrowConfig), thrown);

            byte[] blast = Bomb(src, BlastScale, BlastDamage);
            for (int k = 0; k < 4; k++) WrF(blast, 0x18 + k * 4, 0f);                    // no flight
            U32(blast, ShotEffectPack.CfgWait, 0);                                       // no life: the burst on its first step

            WriteBytes(fs, ElfOff, Cfg(BlastConfig), blast);
        }

        /// <summary>The Crystal Gemron's two shots — the Ice Queen's ice arrow and ice prison as monster shots — and the native code that
        /// drives them (tools/stubs/crystal_shots.s, crystal_home.s, crystal_prison.s; the hook is DunPatches'). Both configs start from
        /// Holy Gemron's shot (config 25) and are written into the dead sceIoctl body; the shot table's two spare entries (34, 35, the
        /// zero words before the species table) point at them. The three dead hosts' first two words become `jr ra; li v0,0`.
        ///  · The ice arrow (34) draws <c>dun\effect\_i_boll.chr</c> (CrystalGemronBake: the korinoya), flies from the moment it is fired at
        ///    <see cref="IceArrowSpeed"/> (the korinoya's `_SET_MOVE(…, 1.6)` is 1.6 a step) for as long as the korinoya can (its 480-frame
        ///    cap; a wall ends either), and hits with the vanilla freezing shots' Freeze (0x100: the hit's 65% status roll, an Anti-Freeze
        ///    Amulet blocks it), the korinoya's guardable knockback (2) and the Ice element.
        ///  · The ice prison (35) draws <c>dun\effect\_f_boll_2.chr</c> (the kori): stationary, its keys 0 (the ice forming), 2 (held)
        ///    and 1 (shattering) as its four phases' motions, no hit and no contact (radius −100 while it stands: CSHOT_EFFECT's contact
        ///    test is "within radius + 6 of the player"), and a long life the cave cuts short when the freeze ends.</summary>
        internal static void PatchCrystalShots(FileStream fs, Func<uint, long> ElfOff)
        {
            long table = ShotEffectPack.CfgTable - 0x20000000L;
            uint Cfg(int index) => RdU32(fs, ElfOff((uint)(table + index * 4)));
            byte[] src = Rd(fs, ElfOff(Cfg(CrystalShotSource)), ShotEffectPack.CfgSize);
            if (NameAt(src, 0, 16) != CrystalShotSourceName) throw new IOException($"shot config {CrystalShotSource} is not {CrystalShotSourceName}");

            byte[] arrow = (byte[])src.Clone();
            Array.Clear(arrow, 0, 16); System.Text.Encoding.ASCII.GetBytes(CrystalGemronBake.IceArrowModel).CopyTo(arrow, 0);
            U32(arrow, ShotEffectPack.CfgFlags, (uint)BehaviorScriptTable.AttackStatusFlag.Freeze | IceElement);
            WrF(arrow, 0x18 + 4, IceArrowSpeed);                                        // the flight (phase 1; crystal_home.s steers it at this speed)
            U32(arrow, ShotEffectPack.CfgWait, IceArrowLife);
            U32(arrow, ShotEffectPack.CfgReaction, (uint)BehaviorScriptTable.AttackReaction.Knockback);

            byte[] prison = (byte[])src.Clone();
            Array.Clear(prison, 0, 16); System.Text.Encoding.ASCII.GetBytes(CrystalGemronBake.IcePrisonModel).CopyTo(prison, 0);
            U32(prison, 0x10, 1); U32(prison, 0x14, 0);                                  // stationary, not turned to a flight
            for (int k = 0; k < 4; k++) { WrF(prison, 0x18 + k * 4, 0f); WrF(prison, ShotEffectPack.CfgRadiusMuzzle + k * 4, k < 2 ? -100f : 0f); }
            U32(prison, ShotEffectPack.CfgWait, IcePrisonLife);
            U32(prison, 0x3C, 0); U32(prison, ShotEffectPack.CfgFlags, 0);               // no damage, no ailment
            U16(prison, 0x4C, 0); U16(prison, 0x4E, 2); U16(prison, 0x50, 1); U16(prison, 0x52, 1);   // forming, held, shattering ×2

            foreach (var (host, vanilla, name) in new[] { (DeadIoctlCave.Host, DeadIoctlCave.VanillaWord0, "sceIoctl"),
                                                          (DeadDiskReadyCave.Host, DeadDiskReadyCave.VanillaWord0, "sceCdDiskReady"),
                                                          (DeadApplyNCmdCave.Host, DeadApplyNCmdCave.VanillaWord0, "sceCdApplyNCmd") })
            {
                ReplaceWord(fs, ElfOff, host, vanilla, 0x03E00008u, $"{name}'s first word");   // jr ra
                WrU32(fs, ElfOff(host + 4), 0x24020000u);                                         //   li v0,0
            }
            WriteBytes(fs, ElfOff, DeadIoctlCave.IceArrowConfig, arrow, DeadIoctlCave.End, "the ice-arrow config overruns the sceIoctl body");
            WriteBytes(fs, ElfOff, DeadIoctlCave.IcePrisonConfig, prison, DeadIoctlCave.End, "the ice-prison config overruns the sceIoctl body");
            ReplaceWord(fs, ElfOff, (uint)(table + ShotEffectPack.IceArrowConfig * 4), 0u, DeadIoctlCave.IceArrowConfig, "shot config table entry 34");
            ReplaceWord(fs, ElfOff, (uint)(table + ShotEffectPack.IcePrisonConfig * 4), 0u, DeadIoctlCave.IcePrisonConfig, "shot config table entry 35");
            foreach (var (bin, first, at, end, host) in new[] {
                ("crystalPrison.bin", 0x080461EAu, DeadIoctlCave.CrystalPrison, DeadIoctlCave.End, "sceIoctl"),
                ("crystalHome.bin",   0x27BDFF90u, DeadDiskReadyCave.CrystalHome, DeadDiskReadyCave.End, "sceCdDiskReady"),
                ("crystalShots.bin",  0x27BDFF80u, DeadApplyNCmdCave.CrystalShots, DeadApplyNCmdCave.End, "sceCdApplyNCmd") })
            {
                byte[] stub = Embedded(bin);
                if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != first) throw new IOException($"{bin} malformed ({stub.Length} B) or stale — reassemble its .s.");
                WriteBytes(fs, ElfOff, at, stub, end, $"{bin} overruns the {host} body");
            }
        }
        private const int CrystalShotSource = 25;               // Holy Gemron's shot
        private const string CrystalShotSourceName = "e115a_ex";
        private const uint IcePrisonLife = 1800;                // 30 s at most; the cave breaks it as the freeze ends
        private const uint IceElement = 0x2;                    // CfgFlags' Ice bit
        private const float IceArrowSpeed = 1.2f;               // three quarters of the Ice Queen's korinoya (_SET_MOVE(…, 1.6), a step)
        private const uint IceArrowLife = 480;                  // the korinoya's flight's cap, in frames

        /// <summary>A config that draws <c>g_wave2</c>, plants nothing, and ends in an item-bomb blast of <paramref name="scale"/>.</summary>
        private static byte[] Bomb(byte[] src, float scale, int damage)
        {
            var c = (byte[])src.Clone();
            U16(c, CfgBlastRadius, RadiusPerScale);
            Array.Clear(c, 0, 16); System.Text.Encoding.ASCII.GetBytes(Model).CopyTo(c, 0);
            for (int k = 0; k < 4; k++) WrF(c, ShotEffectPack.CfgRadiusMuzzle + k * 4, 0f);   // no hit of its own in any phase
            U32(c, 0x3C, 0); U32(c, ShotEffectPack.CfgFlags, 0);                         // no damage, no poison
            U32(c, ShotEffectPack.CfgReaction, Knockdown); U32(c, ShotEffectPack.CfgVictimMask, Target);
            U16(c, 0x4C, 0xFFFF); U16(c, 0x4E, 0); U16(c, 0x50, 0xFFFF); U16(c, 0x52, 0xFFFF);   // motion[4]: straight into the flight (the bomb), no impact or burst clip
            U16(c, 0x54, ShotEndEffectBomb);
            WrF(c, 0x58, scale); U32(c, 0x5C, (uint)damage);
            for (int k = 0; k < 4; k++) U32(c, 0x60 + k * 4, 0xFFFFFFFF);
            return c;
        }

        /// <summary>The config is the vanilla one (named <paramref name="vanilla"/>, flags <paramref name="flags"/>) or already ours.</summary>
        private static void Expect(FileStream fs, Func<uint, long> ElfOff, uint cfg, int index, string vanilla, uint flags)
        {
            byte[] c = Rd(fs, ElfOff(cfg), ShotEffectPack.CfgSize);
            string name = NameAt(c, 0, 16);
            bool ours = name == Model && U16(c, 0x54) == ShotEndEffectBomb;
            if (!ours && !(name == vanilla && U32(c, ShotEffectPack.CfgFlags) == flags))
                throw new IOException($"shot config {index} is '{name}' — unmodified Dark Cloud (USA) ISO expected");
        }

        /// <summary>The machine-gun hit flash per slot (tools/stubs/flash_slot.s at SmoothRestCave.FlashSlot): Draw's constant size
        /// (`lui v0, 0x40A0; mtc1 v0, f12` at 0x1AEA7C) becomes `jal FlashSlot; nop`, and its constant alpha (`addiu t2, zero, 0x80` at
        /// 0x1AEA9C) becomes `or t2, t3, zero` — the alpha the cave leaves in t3. The cave reads CodeCaves.FlashSizeTable / FlashAlphaTable
        /// (baked 5.0 / 0x80, PatchDataPageDefaults: Osmond's flashes unchanged) and runs a pinned slot's spark (CodeCaves.FlashPinTable).</summary>
        internal static void PatchFlashSlot(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SizeSite = 0x001AEA7C, AlphaSite = 0x001AEA9C;
            byte[] stub = Embedded("flashSlot.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x3C1901FB) throw new IOException($"flashSlot.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint cutoff = 0x3C190000u | (BitConverter.SingleToUInt32Bits(BombGemronBake.SelfDestructEnd - 1) >> 16);   // lui $t9, the final pose's frame
            bool found = false;
            for (int i = 0; i < stub.Length; i += 4) found |= U32(stub, i) == cutoff;
            if (!found) throw new IOException($"flash_slot.s's self-destruct cutoff is not the final pose ({BombGemronBake.SelfDestructEnd - 1}.0, lui 0x{cutoff & 0xFFFF:X4}) — set it there and reassemble.");
            WriteBytes(fs, ElfOff, SmoothRestCave.FlashSlot, stub, SmoothRestCave.End, "flashSlot.bin overruns the SmoothRest cave");
            ReplaceWords(fs, ElfOff, SizeSite, new[] { 0x3C0240A0u, 0x44826000u }, new[] { Jal(SmoothRestCave.FlashSlot), 0u }, "the machine-gun flash's size (Draw__21CHIT_MACHINGUN_EFFECT)");
            ReplaceWord(fs, ElfOff, AlphaSite, 0x240A0080u, 0x01605025u, "the machine-gun flash's alpha (Draw__21CHIT_MACHINGUN_EFFECT)");
        }

        /// <summary>A shot config's own blast radius (tools/stubs/bomb_radius.s in the dead sceCdGetToc body): CSHOT_EFFECT::Step's three
        /// `jal SetBombEffect` (0x1AC824, 0x1AC978, 0x1ACAE0) go through the cave, which sets the hit entry's radius and (above scale 1) the
        /// shock ring to the config's halfword +0x56 × its scale when that halfword is not 0.</summary>
        internal static void PatchBombRadius(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("bombRadius.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x27BDFFE0) throw new IOException($"bombRadius.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, DeadCdCave.BombRadius, stub, DeadCdCave.End, "bombRadius.bin overruns the sceCdGetToc body");
            foreach (uint site in new uint[] { 0x001AC824, 0x001AC978, 0x001ACAE0 })
                ReplaceWord(fs, ElfOff, site, 0x0C075650u, Jal(DeadCdCave.BombRadius), "CSHOT_EFFECT::Step's SetBombEffect call");
        }

        /// <summary>The Bomb Gemron's big bomb reddening as its fuse burns (tools/stubs/bomb_tint.s in the dead sceCdReadChain body). No
        /// hook: the cave is reached only through the private vtable BombGemron gives the bomb's visual.</summary>
        internal static void PatchBombTint(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("bombTint.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x3C1901FB) throw new IOException($"bombTint.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint w0 = RdU32(fs, ElfOff(DeadChainCave.Host));
            if (w0 != DeadChainCave.VanillaWord0 && w0 != U32(stub, 0)) throw new IOException($"sceCdReadChain at 0x{DeadChainCave.Host:X} is not vanilla (0x{w0:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WriteBytes(fs, ElfOff, DeadChainCave.BombTint, stub, DeadChainCave.End, "bombTint.bin overruns the sceCdReadChain body");
        }

        /// <summary>The Crystal Gemron's eyes tinted (tools/stubs/eye_tint.s in the dead sceCdReadChain body, after freeze_break). No
        /// hook: the cave is reached only through the private vtable CrystalGemron gives the eyes' visual.</summary>
        internal static void PatchEyeTint(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("eyeTint.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x3C1901FB) throw new IOException($"eyeTint.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, DeadChainCave.EyeTint, stub, DeadChainCave.End, "eyeTint.bin overruns the sceCdReadChain body");
        }

        /// <summary>Each Bomb Gemron's wick captured while its pose is drawn (tools/stubs/fuse_capture.s in the dead sceCdGetToc body):
        /// CMonstorUnit::DrawMonstor's per-unit `jal MGSetAmbient` (0x1D8F84) goes through the cave.</summary>
        internal static void PatchFuseCapture(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("fuseCapture.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x27BDFFD0) throw new IOException($"fuseCapture.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, DeadCdCave.FuseCapture, stub, DeadCdCave.End, "fuseCapture.bin overruns the sceCdGetToc body");
            ReplaceWord(fs, ElfOff, 0x001D8F84, 0x0C04B740u, Jal(DeadCdCave.FuseCapture), "DrawMonstor's per-unit MGSetAmbient call");
        }

        /// <summary>The blow-direction cave (tools/stubs/blow_dir.s) into the dead sceCdGetToc body; DunPatches points BtCheckDamageProc's two
        /// copies of a hit's velocity into blowVelo at it.</summary>
        internal static void PatchBlowDir(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("blowDir.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x8C680074) throw new IOException($"blowDir.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint w0 = RdU32(fs, ElfOff(DeadCdCave.Host));
            if (w0 != DeadCdCave.VanillaWord0 && w0 != U32(stub, 0)) throw new IOException($"sceCdGetToc at 0x{DeadCdCave.Host:X} is not vanilla (0x{w0:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WriteBytes(fs, ElfOff, DeadCdCave.BlowDir, stub, DeadCdCave.End, "blowDir.bin overruns the sceCdGetToc body");
        }

        /// <summary>The data page's words the cold ELF patches read with a vanilla meaning (the app seeds them too, but the page is now
        /// loaded from the ELF on every boot, so a game reset must not leave them zero): the item-bomb reaction (3; 0 would make every
        /// bomb inert), Toan's charge-attack hit radii (6 / 12; 0 would be a hit radius of nothing) and the machine-gun flash's
        /// sizes and alphas (5.0 / 0x80 each).</summary>
        internal static void PatchDataPageDefaults(FileStream fs, Func<uint, long> ElfOff)
        {
            WriteWords(fs, ElfOff, CodeCaves.BombReactionGuest, new[] { (uint)CodeCaves.BombReactionVanilla });
            var radii = new byte[8]; WrF(radii, CodeCaves.ChargeRadiusLunge, CodeCaves.LungeRadiusVanilla); WrF(radii, CodeCaves.ChargeRadiusWhirl, CodeCaves.WhirlRadiusVanilla);
            WriteBytes(fs, ElfOff, CodeCaves.ChargeHitRadiusGuest, radii);
            var sizes = new byte[16 * 4]; for (int i = 0; i < 16; i++) WrF(sizes, i * 4, CodeCaves.FlashSizeVanilla);
            WriteBytes(fs, ElfOff, CodeCaves.FlashSizeTableGuest, sizes);
            var alphas = new byte[16]; Array.Fill(alphas, CodeCaves.FlashAlphaVanilla);
            WriteBytes(fs, ElfOff, (uint)(CodeCaves.FlashAlphaTable - 0x20000000L), alphas);
        }

        /// <summary>MotionProc (0x147D20) plays a track between two of its keys; before the first, vanilla takes key −1 — the 0x20 bytes in
        /// front of the keys, the track's header — and blends it in whenever the bogus weight lands in [0, 1]. Here a track is skipped
        /// there instead: at the common point both key searches reach (.L00147F38: `mtc1 $0,$f0; nop; c.lt.s $f20,$f0; nop; bc1t skip`)
        /// the nop becomes `bltz $s0 (the key), skip` — the c.lt.s its delay slot, the mtc1 still a cycle ahead of it — to the function's
        /// own exit for a track it does not play (0x14881C, return list->next). Vanilla tracks start on frame 0, so none changes; the Crystal
        /// Gemron's fades start on its shatter, so only a dying Gemron writes the species' one material.</summary>
        internal static void PatchMotionBeforeFirstKey(FileStream fs, Func<uint, long> ElfOff) =>
            ReplaceWord(fs, ElfOff, 0x00147F3C, 0x00000000u, 0x06000237u, "MotionProc's nop after the weight test's mtc1");

        internal static void PatchSpeciesExtension(FileStream fs, Func<uint, long> ElfOff)
        {
            PatchMotionBeforeFirstKey(fs, ElfOff);
            PatchBombConfigs(fs, ElfOff);
            PatchCrystalShots(fs, ElfOff);
            PatchDataPageDefaults(fs, ElfOff);
            byte[] rows = SpeciesRows.Build(ti => Rd(fs, ElfOff((uint)EnemySpeciesTable.RecordAddress(ti)), EnemySpeciesTable.Stride));
            WriteBytes(fs, ElfOff, CodeCaves.SpeciesRowsGuest, rows, CodeCaves.SpeciesRowsGuest + (uint)rows.Length, "the species rows overrun their reservation");
            byte[] stub = Embedded("speciesLookup.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x28C200A7)   // first insn = slti v0, a2, 167
                throw new IOException($"speciesLookup.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, SmoothRestCave.SpeciesLookup, stub, SmoothRestCave.End, "speciesLookup.bin overruns the SmoothRest cave");
            ReplaceWords(fs, ElfOff, HookSite, HookVanilla, new[] { Jal(SmoothRestCave.SpeciesLookup), 0u }, "SetupBaseModel's MonstorTable address");
        }
    }
}
