using System;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The player-pellet sprite sheet, <see cref="SheetName"/>: a 128×128 8-bit TIM2 in a 4×4 grid of 32×32 cells, one per
    /// slingshot (draw__5CSHOT draws cell <c>weapon id − 300</c>). The bottom row (cells 312–315) is four byte-exact copies of
    /// the Angel Shooter's stone (cell <see cref="StoneCell"/>): Super Steve (312) and Angel Gear (313) fire that stone from
    /// their own cells — kept, so a custom sprite for either is a bake of its cell — and 314–315 are Goro's first hammers by
    /// id, which never shoot, so those two cells are never drawn in vanilla. Cell <see cref="BlankCell"/> is baked fully
    /// TRANSPARENT here (every pixel <see cref="BlankIndex"/>, a palette index no vanilla pixel uses, its colour 0,0,0,0),
    /// so an invisible pellet is the data write <c>Mailbox.PelletSpriteId = BlankCell</c>; <see cref="FreeCell"/> still
    /// holds the stone, free for a custom sprite. Idempotent.
    /// </summary>
    internal static class PelletSheetBakes
    {
        internal const string SheetName = @"dun\effect\basefx01.img";
        internal const string Picture   = "basefx01";
        internal const int CellBase  = 300;           // cell = sprite id − CellBase
        internal const int StoneCell = 309;           // the Angel Shooter's stone, what the bottom row duplicates
        internal const int FreeCell  = 314;           // still the stone; free for a custom sprite (312/313 are Super Steve's and Angel Gear's own)
        internal const int BlankCell = 315;           // fully transparent: the invisible pellet
        internal const byte BlankIndex = 0;           // the palette index the blank cell is painted with
        private const int Size = 128, Cell = 32, Cols = 4, Pic = 0x10;

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            byte[] file = arc.Read(SheetName);
            var bank = new CatPackBakes.Bank(file);
            int at0 = bank.Entries.First(e => e.name == Picture).off;   // the picture's place in the bank: edited where it lies, the bank's own bytes kept
            byte[] tim = bank.Block(Picture);
            if (System.Text.Encoding.ASCII.GetString(tim, 0, 4) != "TIM2") throw new IOException($"{SheetName}: {Picture} is not a TIM2");
            uint clutSz = IsoBytes.U32(tim, Pic + 4), imgSz = IsoBytes.U32(tim, Pic + 8);
            int hdr = IsoBytes.U16(tim, Pic + 0xC), type = tim[Pic + 0x13], w = IsoBytes.U16(tim, Pic + 0x14), h = IsoBytes.U16(tim, Pic + 0x16);
            if (type != 5 || w != Size || h != Size || hdr != 0x30 || clutSz != 256 * 4 || imgSz != Size * Size)
                throw new IOException($"{SheetName}: not the 128×128 8-bit sheet expected (type {type}, {w}×{h}, hdr 0x{hdr:X}, clut {clutSz}, img {imgSz})");
            int px = Pic + hdr, cl = px + (int)imgSz;
            int At(int cell, int x, int y) { int c = cell - CellBase; return px + ((c >> 2) * Cell + y) * Size + (c & (Cols - 1)) * Cell + x; }
            bool SameAsStone(int cell) { for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++) if (tim[At(cell, x, y)] != tim[At(StoneCell, x, y)]) return false; return true; }
            bool Blank(int cell) { for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++) if (tim[At(cell, x, y)] != BlankIndex) return false; return true; }
            int clutAt = cl + Tim8.Csm1Index(BlankIndex) * 4;                 // where the GS reads the index's colour (CSM1 order)
            bool clear = IsoBytes.U32(tim, clutAt) == 0;

            if (Blank(BlankCell) && clear) { log($"pellet sheet: cell {BlankCell} already blank — skipped"); return; }
            if (!SameAsStone(BlankCell)) throw new IOException($"{SheetName}: cell {BlankCell} is not the stone (cell {StoneCell}) — not the vanilla sheet");
            for (int i = px; i < cl; i++) if (tim[i] == BlankIndex) throw new IOException($"{SheetName}: palette index {BlankIndex} is in use — cannot paint it transparent");

            var outp = file.ToArray();
            for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++) outp[at0 + At(BlankCell, x, y)] = BlankIndex;
            IsoBytes.U32(outp, at0 + clutAt, 0);   // colour 0,0,0, alpha 0: nothing whichever way the sprite is blended
            arc.Redirect(SheetName, outp);
            log($"pellet sheet: cell {BlankCell} painted transparent (palette index {BlankIndex} -> 0,0,0,0); {file.Length:N0} -> {outp.Length:N0} B");
        }
    }
}
