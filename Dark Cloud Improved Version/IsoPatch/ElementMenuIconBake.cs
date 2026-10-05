using System;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The element picker's "None" cell: a desaturated copy of the synth sphere's icon (cell 129), written into the quick-change menu's
    /// own `wepicon` sheet (commenu\a_usa\quickchr.pac → quickchr.img) at cell 125 — dummy item 86's cell, right after the five element
    /// stones (items 81..85 = cells 120..124, row 15; the attachment icons start at cell 120, ComItemInfo's icon_index + 87), so the
    /// picker's six cells sit in one row (ElfElementMenuPatches, element_menu.s).
    /// The sheet is 8-bit in PSMT8 block order with a CSM1 CLUT; every source pixel takes the palette entry nearest its grey
    /// (alpha kept), so the tile needs nothing shipped. Same-size overwrite; a second run finds its own tile and skips.
    /// </summary>
    internal static class ElementMenuIconBake
    {
        private const string Pak = @"commenu\a_usa\quickchr.pac", Bank = "quickchr.img", Sheet = "wepicon";
        private const int SheetW = 256, SheetH = 640, Cell = 32, CellsPerRow = 8;
        private const int SynthSphereIcon = 129, NoneIcon = 125;   // the sheet cells of items 90 and 86 (icon_index 42 / 38, + 87)

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            byte[] file;
            try { file = arc.Read(Pak); } catch (Exception) { log($"{Pak}: not in the archive — skipped"); return; }
            int tim = FindSheet(file);
            if (tim < 0) { log($"{Pak}: no {Sheet} sheet in {Bank} — skipped ({Describe(file)})"); return; }
            int pic = tim + 0x10, hdr = U16(file, pic + 0x0C), colors = U16(file, pic + 0x0E), w = U16(file, pic + 0x14), h = U16(file, pic + 0x16);
            if (w != SheetW || h != SheetH || colors != 256 || file[pic + 0x13] != 5) { log($"{Pak}: {Sheet} is not a 256×640 8-bit sheet — skipped"); return; }
            int px = pic + hdr, clut = px + SheetW * SheetH;

            int src = SynthSphereIcon, dst = NoneIcon;
            int sx = (src % CellsPerRow) * Cell, sy = (src / CellsPerRow) * Cell, dx = (dst % CellsPerRow) * Cell, dy = (dst / CellsPerRow) * Cell;
            var tile = new byte[Cell * Cell];
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    int o = clut + Tim8.Csm1Index(file[px + Tim8.BlockOffset(sx + x, sy + y, SheetW)]) * 4;
                    int grey = (file[o] * 299 + file[o + 1] * 587 + file[o + 2] * 114) / 1000, a = file[o + 3];
                    tile[y * Cell + x] = (byte)Nearest(file, clut, grey, a);
                }
            bool same = true;
            for (int y = 0; y < Cell && same; y++)
                for (int x = 0; x < Cell; x++)
                    if (file[px + Tim8.BlockOffset(dx + x, dy + y, SheetW)] != tile[y * Cell + x]) { same = false; break; }
            if (same) { log($"{Pak}: the None cell is already there — skipped"); return; }
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    file[px + Tim8.BlockOffset(dx + x, dy + y, SheetW)] = tile[y * Cell + x];
            arc.Overwrite(Pak, file);
            log($"{Pak}: the element picker's None cell written (cell {NoneIcon}, a grey synth sphere)");
        }

        /// <summary>The palette index whose colour is nearest the grey (alpha weighted heavily: transparent stays transparent).</summary>
        private static int Nearest(byte[] file, int clut, int grey, int a)
        {
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < 256; i++)
            {
                int o = clut + Tim8.Csm1Index(i) * 4;
                int dr = file[o] - grey, dg = file[o + 1] - grey, db = file[o + 2] - grey, da = file[o + 3] - a;
                int d = dr * dr + dg * dg + db * db + 16 * da * da;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The pak's entries and the banks' texture names, for the skip message.</summary>
        private static string Describe(byte[] pak)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, dataOff, size) in PakEntries(pak))
            {
                sb.Append(name).Append('[').Append(size).Append(']');
                if (dataOff + 0x10 <= pak.Length && pak[dataOff] == 'I' && pak[dataOff + 1] == 'M')
                {
                    int count = (int)U32(pak, dataOff + 4);
                    for (int k = 0; k < count && k < 8; k++) sb.Append(' ').Append(NameAt(pak, dataOff + 0x10 + k * 0x30, 32));
                }
                sb.Append("; ");
            }
            return sb.ToString();
        }

        /// <summary>Offset in the pak of the sheet's TIM2, or -1.</summary>
        private static int FindSheet(byte[] pak)
        {
            foreach (var (name, dataOff, size) in PakEntries(pak))
            {
                if (!string.Equals(name, Bank, StringComparison.OrdinalIgnoreCase)) continue;
                if (dataOff + 0x10 > pak.Length || pak[dataOff] != 'I' || pak[dataOff + 1] != 'M' || pak[dataOff + 2] != '2') return -1;
                int count = (int)U32(pak, dataOff + 4);
                for (int k = 0; k < count; k++)
                {
                    int e = dataOff + 0x10 + k * 0x30;
                    if (e + 0x30 > pak.Length) break;
                    if (NameAt(pak, e, 32) != Sheet) continue;
                    int off = dataOff + (int)U32(pak, e + 0x20);
                    return off + 4 <= pak.Length && pak[off] == 'T' && pak[off + 1] == 'I' && pak[off + 2] == 'M' && pak[off + 3] == '2' ? off : -1;
                }
                return -1;
            }
            return -1;
        }
    }
}
