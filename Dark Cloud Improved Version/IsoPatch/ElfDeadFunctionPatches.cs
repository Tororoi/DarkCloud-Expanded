using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

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
            ReplaceWord(fs, ElfOff, Host, DebugInfoCave.VanillaWord0, 0x03E00008u,   // jr ra: the overlay draws nothing
                        _ => $"DebugInfomationDraw at 0x{Host:X} is not vanilla (`addiu sp,sp,-0x170`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(Host + 4), 0);                                          // (its delay slot)

            // DebugItemGetKey / DebugItemGetDraw: the Confuse roll's and the stars caves' hosts (ElfConfusePatches).
            string itemHosts = $"DebugItemGetKey/Draw (0x{DebugItemCave.Host:X}/0x{DebugItemCave.DrawHost:X}) are not vanilla — unmodified Dark Cloud (USA) ISO expected.";
            ReplaceWord(fs, ElfOff, DebugItemCave.Host, DebugItemCave.KeyWord0, 0x03E00008u, _ => itemHosts);        // jr ra
            WrU32(fs, ElfOff(DebugItemCave.Host + 4), 0x2402FFFFu);     //   addiu v0,zero,-1 (the screen's "leave")
            ReplaceWord(fs, ElfOff, DebugItemCave.DrawHost, DebugItemCave.DrawWord0, 0x03E00008u, _ => itemHosts);   // jr ra
            WrU32(fs, ElfOff(DebugItemCave.DrawHost + 4), 0u);          //   nop

            // DebugInfomationIF: the magic-circle cave's host (ElfWeaponPatches.PatchCircleEffects) and a dozen smaller caves'.
            const uint IfHost = DebugIfCave.Host;
            ReplaceWord(fs, ElfOff, IfHost, DebugIfCave.VanillaWord0, 0x03E00008u,   // jr ra
                        _ => $"DebugInfomationIF at 0x{IfHost:X} is not vanilla (`addiu sp,sp,-0x20`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(IfHost + 4), 0x24020000u);                            //   addiu v0,zero,0 — "nothing pressed" to the debug key's caller
        }
    }
}
