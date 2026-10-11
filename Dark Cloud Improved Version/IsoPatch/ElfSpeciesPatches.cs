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
            long table = ShotEffectPack.VanillaCfgTable - 0x20000000L;
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
            long table = ShotEffectPack.VanillaCfgTable - 0x20000000L;
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

        /// <summary>The Atla Gemron's shot (config 36, in the free span of the dead sceCdReadChain body) and the native code that drives it
        /// (tools/stubs/atla_shot.s, which crystal_shots.s runs on into). From Holy Gemron's shot (config 25: its damage, reaction, element,
        /// target and sounds), drawing <c>dun\effecttla_s.chr</c> (AtlaGemronBake: the Atlamillia's burst) with one phase — motion 0, then
        /// none, so the shot ends with its motion —, turned to its aim (face_flight). It flies <see cref="AtlaGemronBake.ShotFlight"/> a
        /// frame, moved by the cave every frame (the engine holds a shot that touches the player or a wall: speed[0] is only its aim, 2^-20)
        /// and its hit radius is the cave's, growing from 1 at <see cref="AtlaGrowFrom"/> to 18 at <see cref="AtlaGrowTo"/>. The phases it
        /// never reaches carry the cave's numbers: speed[1] 18, speed[2] the flight / speed[0], radius[1] the growth a frame, radius[2] its
        /// offset, radius[3] 1.</summary>
        internal static void PatchAtlaShot(FileStream fs, Func<uint, long> ElfOff)
        {
            long table = ShotEffectPack.VanillaCfgTable - 0x20000000L;
            byte[] src = Rd(fs, ElfOff(RdU32(fs, ElfOff((uint)(table + CrystalShotSource * 4)))), ShotEffectPack.CfgSize);
            if (NameAt(src, 0, 16) != CrystalShotSourceName) throw new IOException($"shot config {CrystalShotSource} is not {CrystalShotSourceName}");
            byte[] c = (byte[])src.Clone();
            Array.Clear(c, 0, 16); System.Text.Encoding.ASCII.GetBytes(AtlaGemronBake.ShotModel).CopyTo(c, 0);
            U32(c, 0x10, 0); U32(c, 0x14, 1);                                            // flies, turned to its aim
            double k = (AtlaRadiusFull - 1.0) / (AtlaGrowTo - AtlaGrowFrom);
            WrF(c, 0x18, AtlaAim); WrF(c, 0x1C, AtlaRadiusFull); WrF(c, 0x20, AtlaGemronBake.ShotFlight / AtlaAim); WrF(c, 0x24, 0f);
            WrF(c, ShotEffectPack.CfgRadiusMuzzle, 0f); WrF(c, ShotEffectPack.CfgRadiusMuzzle + 4, (float)k);
            WrF(c, ShotEffectPack.CfgRadiusMuzzle + 8, (float)(1.0 - AtlaGrowFrom * k)); WrF(c, ShotEffectPack.CfgRadiusMuzzle + 12, 1f);
            U16(c, 0x4C, 0); U16(c, 0x4E, 0xFFFF); U16(c, 0x50, 0xFFFF); U16(c, 0x52, 0xFFFF);   // motion 0, then none
            WriteBytes(fs, ElfOff, DeadChainCave.AtlaShotConfig, c, DeadChainCave.FreezeBreak, "the Atla shot config overruns its span of the sceCdReadChain body");
            byte[] stub = Embedded("atlaShot.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x8C880000) throw new IOException($"atlaShot.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint seek = RdU32(fs, ElfOff(DeadSeekCave.Host)), ours = DeadSeekCave.Host - DeadCdCave.AtlaShot < stub.Length ? U32(stub, (int)(DeadSeekCave.Host - DeadCdCave.AtlaShot)) : 0;
            if (seek != DeadSeekCave.VanillaWord0 && seek != ours) throw new IOException($"sceCdSeek at 0x{DeadSeekCave.Host:X} is not vanilla (0x{seek:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WriteBytes(fs, ElfOff, DeadCdCave.AtlaShot, stub, DeadSeekCave.End, "atlaShot.bin overruns the sceCdSeek body");
        }
        /// <summary>The Atla Gemron's atla bouncing free of its dying grip (tools/stubs/atla_draw.s, on from atla_shot.s into the dead
        /// sceCdStandby / Stop / Pause bodies): CDungeonMap::DrawAtraBoll's `jal MGDraw` (0x1C51F0) goes through it. The cave's swap frame
        /// and settle length are AtlaGemronBake's.</summary>
        internal static void PatchAtlaDraw(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("atlaDraw.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x3C0801FB) throw new IOException($"atlaDraw.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint swap = 0x3C0E0000u | (BitConverter.SingleToUInt32Bits(AtlaGemronBake.Swap) >> 16);         // lui $t6, the swap frame
            uint settle = BitConverter.SingleToUInt32Bits(1f / (AtlaGemronBake.DeathEnd - AtlaGemronBake.Swap));
            bool hasSwap = false, hasSettle = false;
            for (int i = 0; i + 4 < stub.Length; i += 4)
            {
                hasSwap |= U32(stub, i) == swap;
                hasSettle |= U32(stub, i) == (0x3C0F0000u | (settle >> 16)) && U32(stub, i + 4) == (0x35EF0000u | (settle & 0xFFFF));
            }
            if (!hasSwap || !hasSettle) throw new IOException($"atla_draw.s's swap frame / settle length are not AtlaGemronBake's ({AtlaGemronBake.Swap}, {AtlaGemronBake.DeathEnd - AtlaGemronBake.Swap}) — set them and reassemble.");
            WriteBytes(fs, ElfOff, DeadSeekCave.AtlaDraw, stub, DeadSeekCave.End, "atlaDraw.bin overruns the dead sceCdPause body");
            ReplaceWord(fs, ElfOff, 0x001C51F0, Jal(0x0012ED80), Jal(DeadSeekCave.AtlaDraw), "CDungeonMap::DrawAtraBoll's MGDraw call");
        }

        /// <summary>Collecting an atla made safe for the mod's atlas and for Demon Shaft's floors past the 40-floor slot table
        /// (tools/stubs/atla_collect.s in the dead sceCdReadIOPm body): getAtraToSaveData's `jal GetAtraData` (0x1B7500) goes through it.</summary>
        internal static void PatchAtlaCollect(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("atlaCollect.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x28A80006) throw new IOException($"atlaCollect.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            uint w0 = RdU32(fs, ElfOff(DeadReadIopmCave.Host));
            if (w0 != DeadReadIopmCave.VanillaWord0 && w0 != U32(stub, 0)) throw new IOException($"sceCdReadIOPm at 0x{DeadReadIopmCave.Host:X} is not vanilla (0x{w0:X8}) — unmodified Dark Cloud (USA) ISO expected.");
            WriteBytes(fs, ElfOff, DeadReadIopmCave.AtlaCollect, stub, DeadReadIopmCave.End, "atlaCollect.bin overruns the sceCdReadIOPm body");
            ReplaceWord(fs, ElfOff, 0x001B7500, Jal(0x001BF950), Jal(DeadReadIopmCave.AtlaCollect), "getAtraToSaveData's GetAtraData call");
        }

        /// <summary>Demon Shaft's atlas collected through Gallery of Time's atla tables (tools/stubs/atla_dungeon.s after atla_collect.s):
        /// BtAtraGetShort_Loop's `move s3, a0` (0x1D2C88, an MMI por) becomes `jal` the cave, which hands it 5 for 6.</summary>
        internal static void PatchAtlaDungeon(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("atlaDungeon.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x24080006) throw new IOException($"atlaDungeon.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, DeadReadIopmCave.AtlaDungeon, stub, DeadReadIopmCave.End, "atlaDungeon.bin overruns the sceCdReadIOPm body");
            ReplaceWord(fs, ElfOff, 0x001D2C88, 0x70809E28u, Jal(DeadReadIopmCave.AtlaDungeon), "BtAtraGetShort_Loop's dungeon move");
        }

        private const float AtlaAim = 1f / (1 << 20);           // speed[0]: the aim's length (Set scales the aim by it; the cave moves the shot)
        private const float AtlaRadiusFull = 18f;
        /// <summary>The shot's motion frames (AtlaGemronBake.ShotFrom on, 0.35 a game frame) at attack frames 140 and 145 of the script's
        /// fire (138, at 0.2): 10 and 35 game frames after it.</summary>
        private const double AtlaGrowFrom = AtlaGemronBake.ShotFrom + (140 - 138) / 0.2 * 0.35, AtlaGrowTo = AtlaGemronBake.ShotFrom + (145 - 138) / 0.2 * 0.35;

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

        /// <summary>The Crystal Gemron's eyes and the Atla Gemron's forehead Atlamillia tinted (tools/stubs/eye_tint.s in the dead sceCdReadChain body,
        /// after freeze_break). No hook: the cave is reached only through the private vtables CrystalGemron and AtlaGemron give the visuals.</summary>
        internal static void PatchEyeTint(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("eyeTint.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x3C1801FB) throw new IOException($"eyeTint.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
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
        /// bomb inert), Toan's charge-attack hit radii (6 / 12; 0 would be a hit radius of nothing), the machine-gun flash's
        /// sizes and alphas (5.0 / 0x80 each) and the node tints eye_tint.s adds (the eyes', the atla's).</summary>
        internal static void PatchDataPageDefaults(FileStream fs, Func<uint, long> ElfOff)
        {
            WriteWords(fs, ElfOff, CodeCaves.BombReactionGuest, new[] { (uint)CodeCaves.BombReactionVanilla });
            var radii = new byte[8]; WrF(radii, CodeCaves.ChargeRadiusLunge, CodeCaves.LungeRadiusVanilla); WrF(radii, CodeCaves.ChargeRadiusWhirl, CodeCaves.WhirlRadiusVanilla);
            WriteBytes(fs, ElfOff, CodeCaves.ChargeHitRadiusGuest, radii);
            var sizes = new byte[16 * 4]; for (int i = 0; i < 16; i++) WrF(sizes, i * 4, CodeCaves.FlashSizeVanilla);
            WriteBytes(fs, ElfOff, CodeCaves.FlashSizeTableGuest, sizes);
            var alphas = new byte[16]; Array.Fill(alphas, CodeCaves.FlashAlphaVanilla);
            WriteBytes(fs, ElfOff, (uint)(CodeCaves.FlashAlphaTable - 0x20000000L), alphas);
            var tints = new byte[24];
            for (int i = 0; i < 3; i++) { WrF(tints, i * 4, CodeCaves.EyeTintRgb[i]); WrF(tints, 12 + i * 4, CodeCaves.AtlamilliaTintRgb[i]); }
            WriteBytes(fs, ElfOff, (uint)(CodeCaves.EyeTintColour - 0x20000000L), tints);
            var quat = new byte[16]; float[] q = AtlaGemronBake.AtlaRootQuat;
            for (int i = 0; i < 4; i++) WrF(quat, i * 4, q[i]);
            WriteBytes(fs, ElfOff, (uint)(CodeCaves.AtlaRootQuat - 0x20000000L), quat);
        }

        /// <summary>MotionProc (0x147D20) plays a track between two of its keys; before the first, vanilla takes key −1 — the 0x20 bytes in
        /// front of the keys, the track's header — and blends it in whenever the bogus weight lands in [0, 1]. Here a track is skipped
        /// there instead: at the common point both key searches reach (.L00147F38: `mtc1 $0,$f0; nop; c.lt.s $f20,$f0; nop; bc1t skip`)
        /// the nop becomes `bltz $s0 (the key), skip` — the c.lt.s its delay slot, the mtc1 still a cycle ahead of it — to the function's
        /// own exit for a track it does not play (0x14881C, return list->next). Vanilla tracks start on frame 0, so none changes; the Crystal
        /// Gemron's fades start on its shatter, so only a dying Gemron writes the species' one material.</summary>
        internal static void PatchMotionBeforeFirstKey(FileStream fs, Func<uint, long> ElfOff) =>
            ReplaceWord(fs, ElfOff, 0x00147F3C, 0x00000000u, 0x06000237u, "MotionProc's nop after the weight test's mtc1");

        /// <summary>The species loader's shot table moved to CodeCaves.ShotCfgTable, past the game's 36 entries: SetupBaseModel's two
        /// `lui v0,0x28; addiu v0,v0,-0x590` (0x1E018C, 0x1E0200: BtEntryEffectTbl, 0x0027FA70) address it instead, and it holds the
        /// game's 36 (after PatchBombConfigs and PatchCrystalShots) and the mod's beyond (<paramref name="extra"/>: index → config).</summary>
        internal static void PatchShotTable(FileStream fs, Func<uint, long> ElfOff, params (int index, uint cfg)[] extra)
        {
            var table = new uint[ShotEffectPack.CfgCount];
            for (int i = 0; i < ShotEffectPack.VanillaCfgCount; i++) table[i] = RdU32(fs, ElfOff((uint)(ShotEffectPack.VanillaCfgTable - 0x20000000L) + (uint)i * 4));
            foreach (var (index, cfg) in extra) table[index] = cfg;
            WriteWords(fs, ElfOff, CodeCaves.ShotCfgTableGuest, table);
            uint hi = (CodeCaves.ShotCfgTableGuest + 0x8000u) >> 16, lo = CodeCaves.ShotCfgTableGuest & 0xFFFF;
            foreach (uint site in ShotTableSites)
                ReplaceWords(fs, ElfOff, site, new[] { 0x3C020028u, 0x2442FA70u }, new[] { 0x3C020000u | hi, 0x24420000u | lo }, "SetupBaseModel's shot table address");
        }
        private static readonly uint[] ShotTableSites = { 0x001E018C, 0x001E0200 };

        internal static void PatchSpeciesExtension(FileStream fs, Func<uint, long> ElfOff)
        {
            PatchMotionBeforeFirstKey(fs, ElfOff);
            PatchBombConfigs(fs, ElfOff);
            PatchCrystalShots(fs, ElfOff);
            PatchAtlaShot(fs, ElfOff);
            PatchAtlaDraw(fs, ElfOff);
            PatchAtlaCollect(fs, ElfOff);
            PatchAtlaDungeon(fs, ElfOff);
            PatchShotTable(fs, ElfOff, (ShotEffectPack.AtlaShotConfig, DeadChainCave.AtlaShotConfig));
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
