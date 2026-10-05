using System;
using System.IO;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The dungeon quick-change menu (SELECT) doubling as the weapon's element picker when opened with D-pad UP: the same ring, six
    /// cells for the elements the weapon carries (and None, except for Ruby and Osmond's machine gun) — read from the `wepicon` sheet
    /// the menu's own pack loads, as the character ring shows only the party; X on a cell sets the live weapon's element, records the
    /// pick for <see cref="ElementMenu"/> and closes the menu the way a character change does; Circle leaves.
    ///
    /// The code is tools/stubs/element_menu.s in the two dead DebugItemGet bodies (<see cref="DebugItemCave"/>); the five main-ELF
    /// sites below each lose one `jal`/`lb` to it, and DunPatches points the overlay's SELECT read at the trigger cave and relaxes its
    /// "party of two" test (the picker is for a lone Toan too). The picker flag, the pick and the sheet cache are
    /// <see cref="CodeCaves.ElementMenuMode"/> and its neighbours.
    /// </summary>
    internal static class ElfElementMenuPatches
    {
        private const uint KeyXSite    = 0x00229360, KeyXVanilla    = 0x0C04AE1C;   // CharaChangeKey: `jal Down` (X, the SELECT step)
        private const uint KeyPreSite  = 0x002296D4, KeyPreVanilla  = 0x0C08396C;   // CharaChangeKey: `jal CharaChangeInitToGL`
        private const uint StartSite   = 0x002289D0, StartVanilla   = 0x80420005;   // StartQuickChange: `lb v0,0x5(v0)` (party_size)
        private const uint CloseSite   = 0x00228DD4, CloseVanilla   = 0x0C08B438;   // CharaChangeLoop: `jal MenuTextureReload` on close
        private const uint DrawSite    = 0x00229C24, DrawVanilla    = 0x0C08B3E4;   // CharaChangeDraw: `jal DrawMenu2DSprite` (the cells)

        internal static void PatchElementMenu(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] head = Embedded("elementMenu.bin"), tail = Embedded("elementMenuTail.bin");
            uint H = DebugItemCave.ElementMenuHead, T = DebugItemCave.ElementMenuSheetName;
            if (head.Length == 0 || (head.Length & 3) != 0 || U32(head, 0) != 0x27BDFFF0 || U32(head, (int)(DebugItemCave.ElementMenuXKey - H)) != 0x27BDFFF0
                || U32(head, (int)(DebugItemCave.ElementMenuStart - H)) != 0x80420005)
                throw new IOException($"elementMenu.bin malformed ({head.Length} B) or its caves moved — reassemble element_menu.s and check DebugItemCave.ElementMenu*.");
            if (tail.Length == 0 || (tail.Length & 3) != 0 || U32(tail, 0) != 0x69706577 || U32(tail, (int)(DebugItemCave.ElementMenuDraw - T)) != 0x3C0B01FB
                || U32(tail, (int)(DebugItemCave.ElementMenuPre - T)) != 0x3C0801FB || U32(tail, (int)(DebugItemCave.ElementMenuClose - T)) != 0x3C0801FB)
                throw new IOException($"elementMenuTail.bin malformed ({tail.Length} B) or its caves moved — reassemble element_menu.s and check DebugItemCave.ElementMenu*.");
            WriteBytes(fs, ElfOff, DebugItemCave.ElementMenuHead, head, DebugItemCave.DrawHost, "elementMenu.bin overruns the DebugItemGetKey body");
            WriteBytes(fs, ElfOff, DebugItemCave.ElementMenuSheetName, tail, DebugItemCave.DrawHostEnd, "elementMenuTail.bin overruns the DebugItemGetDraw body");
            ReplaceWord(fs, ElfOff, KeyXSite,   KeyXVanilla,   Jal(DebugItemCave.ElementMenuXKey),  "the quick-change menu's X read");
            ReplaceWord(fs, ElfOff, KeyPreSite, KeyPreVanilla, Jal(DebugItemCave.ElementMenuPre),   "the quick-change menu's character preload");
            ReplaceWord(fs, ElfOff, StartSite,  StartVanilla,  Jal(DebugItemCave.ElementMenuStart), "the quick-change menu's party-size load");
            ReplaceWord(fs, ElfOff, CloseSite,  CloseVanilla,  Jal(DebugItemCave.ElementMenuClose), "the quick-change menu's close");
            ReplaceWord(fs, ElfOff, DrawSite,   DrawVanilla,   Jal(DebugItemCave.ElementMenuDraw),  "the quick-change menu's cell draw");
        }
    }
}
