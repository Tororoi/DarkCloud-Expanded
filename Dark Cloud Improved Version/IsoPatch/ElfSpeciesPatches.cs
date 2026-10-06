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

        internal static void PatchSpeciesExtension(FileStream fs, Func<uint, long> ElfOff)
        {
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
