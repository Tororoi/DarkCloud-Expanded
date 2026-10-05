using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The two pieces of English artwork that spell the desert village "MUSKA RACKA", redrawn to "MUSKA LACKA" from letters the
    /// disc already has — nothing is shipped, every pixel comes from the user's own ISO — and written back in place.
    ///
    /// LOADING CARD: <c>img_1\MT04.tm2</c>, the 384×128 16-colour picture the loading screen shows on the way into the village
    /// (nowload picks <c>mt0&lt;map+1&gt;.tm2</c>). The lettering and the brown silhouette art share the one pixel layer, so the
    /// "R" is erased (its footprint refilled row by row from the art just left of it) and the "L" of "CASTLE" on the Dark Heaven
    /// Castle card (<c>MT40.tm2</c>, the same bold white lettering at the same 55 px height) is copied over it, baseline-aligned,
    /// each source colour mapped to the Muska palette entry of the nearest brightness. "ACKA" does not move: the L's foot ends
    /// where the R's leg did.
    ///
    /// MENU NAME PLATES: the <c>vilname</c> sheet (256×256, 8-bit, PSMT8 block order) in the USA Georama and dungeon menu packs —
    /// six plaques, "MUSKA RACKA" the fourth. Vanilla also left a ghost of an erased letter (a plaque-coloured glyph with a thin
    /// yellow rim) in the gap before the R; the plaque texture from the row's blank left end covers both, then the first "L" of
    /// "VILLAGE" on the row above is copied in — same sheet, same palette, so the indices move as they are. The USA packs hold two
    /// editions of the sheet with identical letter geometry, so one set of coordinates serves all five.
    /// </summary>
    internal static class TownNameArtBakes
    {
        private const string Card = @"img_1\MT04.tm2", LetterCard = @"img_1\MT40.tm2";
        private const int CardW = 384, CardH = 128;

        // Loading card: the R's strokes span x 203..228, y 41..96; the CASTLE L's x 323..346, y 33..87 (the E starts at 346).
        private const int EraseX0 = 200, EraseX1 = 231, EraseY0 = 38, EraseY1 = 99;      // the R plus a 3 px rim
        private const int SrcX0 = 321, SrcX1 = 345, SrcY0 = 30, SrcY1 = 90, CoreX0 = 323;  // the L plus its rim; strokes from 323
        private const int ShiftX = 203 - 323, ShiftY = 96 - 87;                            // stem to the R's stem, baseline to baseline
        private const int BrightLum = 170, OutlineLum = 50;

        private static readonly (string pak, string bank)[] Sheets =
        {
            (@"commenu\a_usa\emenu.pak", "editmenu.img"),
            (@"commenu\a_usa\dungeon\dunmenu3.pak", "btlmenu.img"),
            (@"commenu\a_usa\dungeon\dunmenu4.pak", "btlmenu.img"),
            (@"commenu\a_usa\dungeon\dunmenu5.pak", "btlmenu.img"),
            (@"commenu\a_usa\dungeon\dunmenu_chk.pak", "btlmenu.img"),
        };
        private const int SheetW = 256, SheetH = 256, RowH = 40;
        // Plate rows are 40 px; letters sit in rows 8..31 of a plate. Row 3 (y 120): the ghost + R occupy x 123..149, the A
        // starts at 151. Row 1 (y 40): "VILLAGE"'s first L is x 159..172. Plain plaque: x 15..42 of any row.
        private const int PlateBandY0 = 8, PlateBandY1 = 32;
        private const int FillX0 = 123, FillX1 = 149, FillFrom = 15, MuskaRow = 3;
        private const int LX0 = 159, LX1 = 172, LRow = 1, LDestX = 140;

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            RunCard(arc, log);
            int done = 0;
            foreach (var (pak, bank) in Sheets)
            {
                byte[] file = TryRead(arc, pak);
                if (file == null) { log($"{pak}: not in the archive — skipped"); continue; }
                int r = FixSheet(file, bank);
                if (r < 0) { log($"{pak}: no {bank} vilname sheet — skipped"); continue; }
                if (r == 0) { log($"{pak}: name plate already reads LACKA — skipped"); continue; }
                arc.Overwrite(pak, file);
                done++;
            }
            log($"town name plates redrawn in {done} menu packs");
        }

        private static byte[] TryRead(IsoArchive arc, string name)
        {
            try { return arc.Read(name); } catch (Exception) { return null; }
        }

        // ── the loading card (4-bit, linear) ────────────────────────────────────────────────────────────────────────────

        private sealed class Tim4
        {
            internal readonly byte[] File; internal readonly int Px, Clut, W, H;
            internal Tim4(byte[] file, int w, int h)
            {
                File = file;
                int pic = 0x10;
                if (file.Length < 0x40 || file[0] != 'T' || file[1] != 'I' || file[2] != 'M' || file[3] != '2') throw new InvalidOperationException("not a TIM2");
                int hdr = U16(file, pic + 0x0C), colors = U16(file, pic + 0x0E);
                W = U16(file, pic + 0x14); H = U16(file, pic + 0x16);
                if (W != w || H != h || colors != 16 || file[pic + 0x13] != 4) throw new InvalidOperationException($"not a {w}x{h} 16-colour TIM2");
                Px = pic + hdr; Clut = Px + W * H / 2;
            }
            internal int Get(int x, int y) { int i = y * W + x; byte b = File[Px + (i >> 1)]; return (i & 1) == 0 ? b & 15 : b >> 4; }
            internal void Set(int x, int y, int v)
            {
                int i = y * W + x, o = Px + (i >> 1);
                File[o] = (byte)((i & 1) == 0 ? (File[o] & 0xF0) | v : (File[o] & 0x0F) | (v << 4));
            }
            internal int Lum(int index) { int o = Clut + index * 4; return (File[o] + File[o + 1] + File[o + 2]) / 3; }
            internal int Nearest(int lum) { int best = 0; for (int k = 1; k < 16; k++) if (Math.Abs(Lum(k) - lum) < Math.Abs(Lum(best) - lum)) best = k; return best; }
        }

        private static void RunCard(IsoArchive arc, Action<string> log)
        {
            byte[] cardFile = TryRead(arc, Card), letterFile = TryRead(arc, LetterCard);
            if (cardFile == null || letterFile == null) { log("loading card: not in the archive — skipped"); return; }
            Tim4 card, src;
            try { card = new Tim4(cardFile, CardW, CardH); src = new Tim4(letterFile, CardW, CardH); }
            catch (InvalidOperationException e) { log($"loading card: {e.Message} — skipped"); return; }
            // Vanilla signature: the R's bowl is bright at (224, 54); the source L's stem is bright at (327, 60).
            if (card.Lum(card.Get(224, 54)) < BrightLum) { log("loading card already reads LACKA — skipped"); return; }
            if (src.Lum(src.Get(327, 60)) < BrightLum) { log("loading card: the CASTLE letters are not where expected — skipped"); return; }

            for (int y = EraseY0; y <= EraseY1; y++)
            {
                int fill = card.Get(EraseX0 - 1, y);
                for (int x = EraseX0; x <= EraseX1; x++) card.Set(x, y, fill);
            }
            bool Core(int x, int y) => x >= CoreX0 && x <= SrcX1 && y >= SrcY0 && y <= SrcY1 && src.Lum(src.Get(x, y)) >= BrightLum;
            bool NearCore(int x, int y, int r)
            {
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (Core(x + dx, y + dy)) return true;
                return false;
            }
            for (int y = SrcY0; y <= SrcY1; y++)
                for (int x = SrcX0; x <= SrcX1; x++)
                {
                    int lum = src.Lum(src.Get(x, y));
                    bool keep = Core(x, y)
                             || (lum < OutlineLum && NearCore(x, y, 2))        // the black outline ring
                             || (lum < BrightLum && NearCore(x, y, 1));        // the edge antialias
                    if (keep) card.Set(x + ShiftX, y + ShiftY, card.Nearest(lum));
                }
            arc.Overwrite(Card, cardFile);
            log("loading card redrawn: MUSKA LACKA");
        }

        // ── the menu name plates (8-bit, PSMT8 block order, CSM1 CLUT) ──────────────────────────────────────────────────

        /// <summary>-1 = no sheet; 0 = already done; 1 = redrawn.</summary>
        private static int FixSheet(byte[] pak, string bank)
        {
            int tim = FindVilname(pak, bank);
            if (tim < 0) return -1;
            int pic = tim + 0x10, hdr = U16(pak, pic + 0x0C), colors = U16(pak, pic + 0x0E), w = U16(pak, pic + 0x14), h = U16(pak, pic + 0x16);
            if (w != SheetW || h != SheetH || colors != 256 || pak[pic + 0x13] != 5) return -1;
            int px = pic + hdr, clut = px + SheetW * SheetH;
            int At(int x, int y) => px + Tim8.BlockOffset(x, y, SheetW);
            bool Letter(int index)
            {
                int o = clut + Tim8.Csm1Index(index) * 4; int r = pak[o], g = pak[o + 1], b = pak[o + 2];
                return (r > 150 && g > 120 && b < 110) || (r < 95 && g < 115 && b < 95);   // the yellow fill, the dark green rim
            }
            int top = MuskaRow * RowH;
            if (!Letter(pak[At(148, top + 15)])) return 0;                  // the R's bowl: plaque once the R is gone (the L never reaches it)

            for (int y = top + PlateBandY0; y < top + PlateBandY1; y++)      // plaque over the ghost glyph and the R
                for (int x = FillX0; x <= FillX1; x++) pak[At(x, y)] = pak[At(x - FillX0 + FillFrom, y)];
            int lTop = LRow * RowH;
            for (int y = PlateBandY0; y < PlateBandY1; y++)                  // "VILLAGE"'s L, indices as they are
                for (int x = LX0; x <= LX1; x++)
                {
                    byte i = pak[At(x, lTop + y)];
                    if (Letter(i)) pak[At(x - LX0 + LDestX, top + y)] = i;
                }
            return 1;
        }

        /// <summary>Offset in the pak of the <c>vilname</c> TIM2 inside the IM2 bank <paramref name="bank"/>, or -1.</summary>
        private static int FindVilname(byte[] pak, string bank)
        {
            foreach (var (name, dataOff, size) in PakEntries(pak))
            {
                if (!string.Equals(name, bank, StringComparison.OrdinalIgnoreCase)) continue;
                if (dataOff + 0x10 > pak.Length || pak[dataOff] != 'I' || pak[dataOff + 1] != 'M' || pak[dataOff + 2] != '2') return -1;
                int count = (int)U32(pak, dataOff + 4);
                for (int k = 0; k < count; k++)
                {
                    int e = dataOff + 0x10 + k * 0x30;
                    if (e + 0x30 > pak.Length) break;
                    if (NameAt(pak, e, 32) != "vilname") continue;
                    int off = dataOff + (int)U32(pak, e + 0x20);
                    return off + 4 <= pak.Length && pak[off] == 'T' && pak[off + 1] == 'I' && pak[off + 2] == 'M' && pak[off + 3] == '2' ? off : -1;
                }
                return -1;
            }
            return -1;
        }
    }
}
