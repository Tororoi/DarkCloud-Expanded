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

        /// <summary>Shot config 8 (<c>g_wave2</c>, which no species or script uses) rewritten as the Bomb Gemron's explosion: <c>zibaku_f2</c>
        /// (the outlaws' self-destruct fireball — its model, fire element, knockdown reaction, player-only mask and 110-frame life) with
        /// the damage radius of its one phase at <see cref="BlastRadius"/> instead of 14; the script's <c>_SET_SHOT2</c> argument is the
        /// damage. The engine plants the hit every frame of the shot's life, so the player takes one hit (then his invincibility
        /// frames) anywhere inside the radius while the fireball lasts.</summary>
        private const int BlastConfig = 8, BlastSource = 16;
        internal const float BlastRadius = 50f;
        private const string BlastSourceName = "zibaku_f2", BlastVanillaName = "g_wave2";

        internal static void PatchBlastConfig(FileStream fs, Func<uint, long> ElfOff)
        {
            long table = ShotEffectPack.CfgTable - 0x20000000L;
            uint cfg = RdU32(fs, ElfOff((uint)(table + BlastConfig * 4))), src = RdU32(fs, ElfOff((uint)(table + BlastSource * 4)));
            byte[] cur = Rd(fs, ElfOff(cfg), ShotEffectPack.CfgSize), c = Rd(fs, ElfOff(src), ShotEffectPack.CfgSize);
            string name = NameAt(cur, 0, 16);
            if (name != BlastVanillaName && name != BlastSourceName) throw new IOException($"shot config {BlastConfig} is '{name}', not {BlastVanillaName} — unmodified Dark Cloud (USA) ISO expected");
            if (NameAt(c, 0, 16) != BlastSourceName) throw new IOException($"shot config {BlastSource} is not {BlastSourceName}");
            WrF(c, ShotEffectPack.CfgRadiusMuzzle, BlastRadius);                         // phase 0 = the whole life of a stationary shot
            WrF(c, ShotEffectPack.CfgRadiusFlying, 0f); WrF(c, ShotEffectPack.CfgRadiusImpact, 0f); WrF(c, ShotEffectPack.CfgRadiusExpire, 0f);
            WriteBytes(fs, ElfOff, cfg, c);
        }

        /// <summary>The data page's words the cold ELF patches read with a vanilla meaning (the app seeds them too, but the page is now
        /// loaded from the ELF on every boot, so a game reset must not leave them zero): the item-bomb reaction (3; 0 would make every
        /// bomb inert) and Toan's charge-attack hit radii (6 / 12; 0 would be a hit radius of nothing).</summary>
        internal static void PatchDataPageDefaults(FileStream fs, Func<uint, long> ElfOff)
        {
            WriteWords(fs, ElfOff, CodeCaves.BombReactionGuest, new[] { (uint)CodeCaves.BombReactionVanilla });
            var radii = new byte[8]; WrF(radii, CodeCaves.ChargeRadiusLunge, CodeCaves.LungeRadiusVanilla); WrF(radii, CodeCaves.ChargeRadiusWhirl, CodeCaves.WhirlRadiusVanilla);
            WriteBytes(fs, ElfOff, CodeCaves.ChargeHitRadiusGuest, radii);
        }

        internal static void PatchSpeciesExtension(FileStream fs, Func<uint, long> ElfOff)
        {
            PatchBlastConfig(fs, ElfOff);
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
