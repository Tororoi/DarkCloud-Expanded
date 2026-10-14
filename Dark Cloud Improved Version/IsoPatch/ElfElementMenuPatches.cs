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
    /// pick for <see cref="ElementMenu"/> and closes the menu the way a character change does; Circle leaves. The menu no longer ends a
    /// lock-on (its exit's `jal LockOffTargte` is a nop), for an element pick or a character change alike.
    ///
    /// The code is tools/stubs/element_menu.s in the two dead DebugItemGet bodies (<see cref="DebugItemCave"/>) plus element_menu_ruby.s in
    /// the SmoothRest body (Ruby's per-element shot effect reloaded when the picker closes on her); the five main-ELF
    /// sites below each lose one `jal`/`lb` to it (a sixth, the exit's lock-off, is a nop), and DunPatches points the overlay's SELECT read at the trigger cave and relaxes its
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
        private const uint LockOffSite = 0x00228F88, LockOffVanilla = 0x0C76FF38;   // CharaChangeKey: `jal LockOffTargte` (the LOADED step's exit)

        internal static void PatchElementMenu(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] head = Embedded("elementMenu.bin"), tail = Embedded("elementMenuTail.bin"), ruby = Embedded("elementMenuRuby.bin");
            uint H = DebugItemCave.ElementMenuHead, T = DebugItemCave.ElementMenuSheetName;
            if (head.Length == 0 || (head.Length & 3) != 0 || U32(head, 0) != 0x27BDFFF0 || U32(head, (int)(DebugItemCave.ElementMenuXKey - H)) != 0x27BDFFF0
                || U32(head, (int)(DebugItemCave.ElementMenuStart - H)) != 0x80420005)
                throw new IOException($"elementMenu.bin malformed ({head.Length} B) or its caves moved — reassemble element_menu.s and check DebugItemCave.ElementMenu*.");
            if (tail.Length == 0 || (tail.Length & 3) != 0 || U32(tail, 0) != 0x69706577 || U32(tail, (int)(DebugItemCave.ElementMenuDraw - T)) != 0x3C0B01FB
                || U32(tail, (int)(DebugItemCave.ElementMenuPre - T)) != 0x3C0801FB || U32(tail, (int)(DebugItemCave.ElementMenuClose - T)) != 0x3C0801FB)
                throw new IOException($"elementMenuTail.bin malformed ({tail.Length} B) or its caves moved — reassemble element_menu.s and check DebugItemCave.ElementMenu*.");
            if (ruby.Length == 0 || (ruby.Length & 3) != 0 || U32(ruby, 0) != 0x27BDFF90)
                throw new IOException($"elementMenuRuby.bin malformed ({ruby.Length} B) — reassemble element_menu_ruby.s.");
            WriteBytes(fs, ElfOff, SmoothRestCave.ElementMenuRuby, ruby, SmoothRestCave.End, "elementMenuRuby.bin overruns the SmoothRest body");
            WriteBytes(fs, ElfOff, DebugItemCave.ElementMenuHead, head, DebugItemCave.DrawHost, "elementMenu.bin overruns the DebugItemGetKey body");
            WriteBytes(fs, ElfOff, DebugItemCave.ElementMenuSheetName, tail, DebugItemCave.DrawHostEnd, "elementMenuTail.bin overruns the DebugItemGetDraw body");
            ReplaceWord(fs, ElfOff, KeyXSite,   KeyXVanilla,   Jal(DebugItemCave.ElementMenuXKey),  "the quick-change menu's X read");
            ReplaceWord(fs, ElfOff, KeyPreSite, KeyPreVanilla, Jal(DebugItemCave.ElementMenuPre),   "the quick-change menu's character preload");
            ReplaceWord(fs, ElfOff, StartSite,  StartVanilla,  Jal(DebugItemCave.ElementMenuStart), "the quick-change menu's party-size load");
            ReplaceWord(fs, ElfOff, CloseSite,  CloseVanilla,  Jal(DebugItemCave.ElementMenuClose), "the quick-change menu's close");
            ReplaceWord(fs, ElfOff, DrawSite,   DrawVanilla,   Jal(DebugItemCave.ElementMenuDraw),  "the quick-change menu's cell draw");
            ReplaceWord(fs, ElfOff, LockOffSite, LockOffVanilla, 0u, "the quick-change menu's lock-off");
        }

        // WeaponMenuCheckEnableSetElem (0x1FC110): the weapon menu's element pick. Its head jumps to the lock test, which sits in the
        // function's own tail where its two debug printfs were (the guard before them now branches straight to the epilogue).
        private const uint LockHeadSite  = 0x001FC130;   // lb v0,0x16(a0) / bne v0,a2,+0x13 / nop
        private const uint LockGuardSite = 0x001FC1B8;   // beq s2,zero,epilogue — before the printfs
        private const uint LockSite      = 0x001FC1C0;   // the printfs (14 words) → the lock test

        /// <summary>The weapon menu refuses, with warning 9 (the warning Ruby's "an element can't be removed" uses; WeaponDescriptions
        /// gives it the curse's text), any element but the one <see cref="CodeCaves.ElementLock"/> holds its item to. Ruby taking off
        /// that element stays the retail refusal. The D-pad picker reads the same word (element_menu.s `valid`).</summary>
        internal static void PatchElementLock(FileStream fs, Func<uint, long> ElfOff)
        {
            ReplaceWords(fs, ElfOff, LockHeadSite,
                new uint[] { 0x80820016, 0x14460013, 0x00000000 },
                new uint[] {
                    0x10000023,                    // b     lock
                    0x80820016,                    // lb    v0,0x16(a0)        (delay) best_elem
                    0x14460012,                    // bne   v0,a2,0x1FC184     (its delay slot is the next word, lh a0,0(s2))
                }, "WeaponMenuCheckEnableSetElem's head", (0x001FC13C, 0x86440000));
            ReplaceWord(fs, ElfOff, LockGuardSite, 0x12400010, 0x10000010, "WeaponMenuCheckEnableSetElem's printf guard");
            ReplaceWords(fs, ElfOff, LockSite,
                new uint[] { 0x82450016, 0x3C02002A, 0x2444D8C8, 0x0C0411A6, 0x00000000, 0x82430016, 0x24020005,
                             0x10620007, 0x00000000, 0x00711021, 0x80450017, 0x3C02002A, 0x2444D8E0, 0x0C0411A6 },
                new uint[] {
                    0x3C0801FB,                    // lock: lui t0,0x01FB
                    0x8509E6B0,                    // lh    t1,-0x1950(t0)     ElementLock: the item held (0 = none)
                    0x848A0000,                    // lh    t2,0(a0)           weapon->item_no
                    0x152AFFDA,                    // bne   t1,t2,0x1FC138     not it: as retail
                    0x8509E6B2,                    // lh    t1,-0x194E(t0)     (delay) the element it is held to
                    0x1126FFD8,                    // beq   t1,a2,0x1FC138     that element: as retail
                    0x00000000,                    // nop
                    0x2402000B,                    // li    v0,0xB
                    0x3C0101DA,                    // lui   at,0x01DA
                    0xA422EA7C,                    // sh    v0,-0x1584(at)     WepMenu.state = WARNING
                    0x24020009,                    // li    v0,9
                    0xA022EA78,                    // sb    v0,-0x1588(at)     WepMenu.warning_no = 9
                    0x10000002,                    // b     0x1FC1FC           the epilogue
                    0x24100001,                    // li    s0,1               (delay) refused
                }, "WeaponMenuCheckEnableSetElem's printfs", (0x001FC1FC, 0x72001628));
        }
    }
}
