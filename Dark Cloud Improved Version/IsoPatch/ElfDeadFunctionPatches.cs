using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The dead main-ELF functions whose bodies host the mod's caves, each made to return at once from its first two
    /// words: DebugInfomationDraw (DebugInfoCave), DebugItemGetKey and DebugItemGetDraw (DebugItemCave) and
    /// DebugInfomationIF (DebugIfCave). Called from ElfPatches.ElfPatchAndCrc before every cave patch; the caves
    /// themselves are written from +8 by the Elf*Patches classes.</summary>
    internal static class ElfDeadFunctionPatches
    {
        /// <summary>Each host's first words become its return — DebugInfomationDraw `jr ra; nop` (the overlay draws nothing),
        /// DebugItemGetKey `jr ra; addiu v0,zero,-1` (the screen's "leave") and DebugItemGetDraw `jr ra; nop`, DebugInfomationIF
        /// `jr ra; addiu v0,zero,0` ("nothing pressed" to the debug key's caller) — behind a vanilla-or-ours guard on each
        /// first word. No cave reaches below +8 of its host, so the order against the cave patches does not change a byte.</summary>
        internal static void PatchClaimDeadFunctionHosts(FileStream fs, Func<uint, long> ElfOff)
        {
            // DebugInfomationDraw: the shot-slot sharing cave's host (ElfShotPackPatches.PatchSharedShots).
            const uint Host = DebugInfoCave.Host;
            uint w0 = RdU32(fs, ElfOff(Host));
            if (w0 != DebugInfoCave.VanillaWord0 && w0 != 0x03E00008u)
                throw new IOException($"DebugInfomationDraw at 0x{Host:X} is not vanilla (`addiu sp,sp,-0x170`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(Host), 0x03E00008u);                                   // jr ra: the overlay draws nothing
            WrU32(fs, ElfOff(Host + 4), 0);                                          // (its delay slot)

            // DebugItemGetKey / DebugItemGetDraw: the Confuse roll's and the stars caves' hosts (ElfConfusePatches).
            uint key0 = RdU32(fs, ElfOff(DebugItemCave.Host)), draw0 = RdU32(fs, ElfOff(DebugItemCave.DrawHost));
            if (key0 != DebugItemCave.KeyWord0 && key0 != 0x03E00008u || draw0 != DebugItemCave.DrawWord0 && draw0 != 0x03E00008u)
                throw new IOException($"DebugItemGetKey/Draw (0x{DebugItemCave.Host:X}/0x{DebugItemCave.DrawHost:X}) are not vanilla — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(DebugItemCave.Host), 0x03E00008u);         // jr ra
            WrU32(fs, ElfOff(DebugItemCave.Host + 4), 0x2402FFFFu);     //   addiu v0,zero,-1 (the screen's "leave")
            WrU32(fs, ElfOff(DebugItemCave.DrawHost), 0x03E00008u);     // jr ra
            WrU32(fs, ElfOff(DebugItemCave.DrawHost + 4), 0u);          //   nop

            // DebugInfomationIF: the magic-circle cave's host (ElfWeaponPatches.PatchCircleEffects) and a dozen smaller caves'.
            const uint IfHost = DebugIfCave.Host;
            uint if0 = RdU32(fs, ElfOff(IfHost));
            if (if0 != DebugIfCave.VanillaWord0 && if0 != 0x03E00008u)
                throw new IOException($"DebugInfomationIF at 0x{IfHost:X} is not vanilla (`addiu sp,sp,-0x20`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(IfHost),     0x03E00008u);                            // jr   ra
            WrU32(fs, ElfOff(IfHost + 4), 0x24020000u);                            //   addiu v0,zero,0 — "nothing pressed" to the debug key's caller
        }
    }
}
