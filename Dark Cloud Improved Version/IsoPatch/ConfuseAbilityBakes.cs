using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The "Confuse" weapon ability (bit 0x4000 of the ability word — Effect2 bit 0x40) as the menus show it: its name in the
    /// system message bank and its icon in the status window's icon sheet. ElfWeaponPatches.PatchConfuseAbility points the menus
    /// at both (the SPECIAL list's loop reaches bit 14, whose name is message 0x45) and gives Babel's Spear the bit.
    ///
    /// NAME: message 0x45 ("Confuse") added to the four English system banks (meswin\system_1.mes / systeme.bin and the system14
    /// pair — LoadSystemMessage reads system_1, falling back to systeme). 0x45 is free and sits just before "Big bucks" (0x46);
    /// MesTextBaker.AppendMes keeps the index id-sorted. The count grows by one: the bank is read through its header live
    /// (Atlamillia Insurance's name channels too), and its text ends far below Atlamillia's fixed channel area (byte 48,256).
    ///
    /// ICON: the `charaface` sheet (256×320, 8-bit, PSMT8-swizzled in an IM2 bank) holds the ability icons as 20×20 tiles —
    /// WeaponOptionStatusDraw takes bits 1–7 from column u 0xEC and bits 8–14 from column u 0xD8, row v = 0x50 + 20·(n−1) —
    /// so bit 14 is the empty tile at (0xD8, 0xC8), beside Steal. Its art is Resources/isoPatch/confuse_icon.png (20×20 RGBA,
    /// drawn in the sheet's own palette): each pixel mapped to the sheet's nearest palette entry (transparent → the tiles' clear
    /// corner entry) and written into every US copy of the sheet whose Heal tile is the vanilla one (the same palette).
    /// </summary>
    internal static class ConfuseAbilityBakes
    {
        internal const int  NameMessage = 0x45;
        private static readonly ushort[] Name = { 0xFD23, 0xFD49, 0xFD48, 0xFD40, 0xFD4F, 0xFD4D, 0xFD3F, 0xFF01 };   // "Confuse"
        private static readonly string[] Banks = { @"meswin\system_1.mes", @"meswin\systeme.bin", @"meswin\system14_1.mes", @"meswin\system14e.bin" };
        private static readonly string[] Sheets =
        {
            @"commenu\a_usa\charatex.img", @"commenu\a_usa\_charatex.img", @"commenu\a_usa\nameregi.pak",
            @"commenu\a_usa\dungeon\dunmenu3.pak", @"commenu\a_usa\dungeon\dunmenu_chk.pak",
            @"commenu\a_usa\dungeon\dunmenu4.pak", @"commenu\a_usa\dungeon\dunmenu5.pak",
        };
        private const int SheetW = 256, SheetH = 320, Tile = 20;
        private const int HealU = 0xD8, HealV = 0x8C, ConfuseU = 0xD8, ConfuseV = 0xC8;

        private static readonly byte[] HealCell =
        {
            0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x64,
            0x00, 0x21, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xE6, 0x21, 0x00,
            0x00, 0xEE, 0xEA, 0xDA, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xD5, 0xDA, 0xCD, 0xAD, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xD5, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x07, 0x07, 0x07, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xCD, 0xAD, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0x07, 0x07, 0x07, 0x07, 0x0C, 0x11, 0x11, 0x11, 0x07, 0x07, 0x07, 0x07, 0xEE, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0x07, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0xEE, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0x07, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0xEE, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xEE, 0xEE, 0xEE, 0xEE, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xEE, 0xEE, 0xEE, 0xE6, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0x07, 0x11, 0x11, 0x11, 0xEE, 0xCD, 0xCD, 0xCD, 0xC7, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xEE, 0xEE, 0xEE, 0xEE, 0xE6, 0xD5, 0xCD, 0xCD, 0xCD, 0xB8, 0x7D, 0x00,
            0x00, 0xE1, 0xDA, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xB8, 0x7D, 0x00,
            0x00, 0xCD, 0xB8, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA7, 0xA2, 0x86, 0x60, 0x00,
            0x00, 0x21, 0x71, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x52, 0x21, 0x00,
            0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x64
        };

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            int names = 0, icons = 0;
            foreach (string bank in Banks)
            {
                byte[] mes = TryRead(arc, bank);
                if (mes == null) { log($"{bank}: not in the archive — skipped"); continue; }
                if (HasMessage(mes, NameMessage)) { log($"{bank}: message 0x{NameMessage:X} already there — skipped"); continue; }
                if (!HasMessage(mes, 0x46)) { log($"{bank}: no Big bucks name (0x46) — not the ability-name bank, skipped"); continue; }
                arc.Redirect(bank, MesTextBaker.AppendMes(mes, false, (NameMessage, Name)));   // padding kept: the bank is allocated by its size
                names++;
            }
            foreach (string name in Sheets)
            {
                byte[] file = TryRead(arc, name);
                if (file == null) { log($"{name}: not in the archive — skipped"); continue; }
                int n = PaintIcon(file);
                if (n < 0) { log($"{name}: no vanilla charaface sheet found — skipped"); continue; }
                if (n == 0) { log($"{name}: the Confuse icon is already there — skipped"); continue; }
                arc.Overwrite(name, file);                                  // same size: written where it stands
                icons += n;
            }
            log($"confuse ability: name in {names} bank(s), icon in {icons} sheet(s)");
        }

        private static byte[] TryRead(IsoArchive arc, string name) { try { return arc.Read(name); } catch { return null; } }

        private static bool HasMessage(byte[] mes, int id)
        {
            int cnt = U16(mes, 0);
            for (int i = 0; i < cnt; i++) if (U16(mes, 4 + i * 4) == id) return true;
            return false;
        }

        /// <summary>Paints the icon into every charaface sheet in <paramref name="file"/> (a TIM2 256×320 8-bit picture whose Heal
        /// tile is the vanilla one). The number painted; 0 = all already painted; −1 = none found.</summary>
        private static int PaintIcon(byte[] file)
        {
            int found = 0, painted = 0;
            byte[] rgba = IconRgba();
            for (int t = IndexOf(file, Tim2, 0); t >= 0; t = IndexOf(file, Tim2, t + 4))
            {
                int pic = t + 0x10;
                if (pic + 0x30 > file.Length) break;
                int hdr = U16(file, pic + 0x0C), colors = U16(file, pic + 0x0E), w = U16(file, pic + 0x14), h = U16(file, pic + 0x16);
                if (w != SheetW || h != SheetH || colors != 256 || file[pic + 0x13] != 5) continue;
                int px = pic + hdr, clut = px + SheetW * SheetH;
                if (clut + 1024 > file.Length || !TileEquals(file, px, HealU, HealV, HealCell)) continue;
                found++;
                byte[] icon = ToIndices(rgba, file, clut);
                if (TileEquals(file, px, ConfuseU, ConfuseV, icon)) continue;
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x++)
                        file[px + Swizzled(ConfuseU + x, ConfuseV + y)] = icon[y * Tile + x];
                painted++;
            }
            return found == 0 ? -1 : painted;
        }

        /// <summary>The icon's 20×20 RGBA pixels, from the embedded PNG.</summary>
        private static byte[] IconRgba()
        {
            using var st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.confuse_icon.png")
                ?? throw new System.IO.IOException("Embedded asset missing: confuse_icon.png");
            using var ms = new System.IO.MemoryStream(); st.CopyTo(ms);
            var (w, h, rgba) = Png.DecodeRgba(ms.ToArray());
            if (w != Tile || h != Tile) throw new System.IO.IOException($"confuse_icon.png is {w}×{h}; the ability tiles are {Tile}×{Tile}.");
            return rgba;
        }

        /// <summary>Each RGBA pixel as the sheet's nearest palette entry (its CLUT in the PS2's CSM1 order: index bits 3 and 4 swap);
        /// a transparent pixel (alpha < 128) as the Heal tile's corner entry, the tiles' clear one.</summary>
        private static byte[] ToIndices(byte[] rgba, byte[] file, int clut)
        {
            static int Csm1(int i) => (i & ~0x18) | ((i & 0x08) << 1) | ((i & 0x10) >> 1);
            var pal = new (int r, int g, int b)[256];
            for (int i = 0; i < 256; i++) { int o = clut + Csm1(i) * 4; pal[i] = (file[o], file[o + 1], file[o + 2]); }
            byte clear = HealCell[0];
            var outp = new byte[Tile * Tile];
            for (int k = 0; k < outp.Length; k++)
            {
                int r = rgba[k * 4], g = rgba[k * 4 + 1], b = rgba[k * 4 + 2], a = rgba[k * 4 + 3];
                if (a < 128) { outp[k] = clear; continue; }
                int best = 0, bestD = int.MaxValue;
                for (int i = 0; i < 256; i++)
                {
                    if (file[clut + Csm1(i) * 4 + 3] == 0) continue;                          // never a clear entry for a solid pixel
                    int dr = pal[i].r - r, dg = pal[i].g - g, db = pal[i].b - b, d = dr * dr + dg * dg + db * db;
                    if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
                }
                outp[k] = (byte)best;
            }
            return outp;
        }

        private static bool TileEquals(byte[] file, int px, int u, int v, byte[] tile)
        {
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                    if (file[px + Swizzled(u + x, v + y)] != tile[y * Tile + x]) return false;
            return true;
        }

        /// <summary>A pixel's byte in PSMT8 block order (the sheet's width): the inverse of reading the GS's 32-bit blocks row-major.</summary>
        private static int Swizzled(int x, int y)
        {
            int bl = (y & ~0xF) * SheetW + (x & ~0xF) * 2;
            int ss = (((y + 2) >> 2) & 1) * 4;
            int py = (((y & ~3) >> 1) + (y & 1)) & 7;
            int cl = py * SheetW * 2 + ((x + ss) & 7) * 4;
            int bn = ((y >> 1) & 1) + ((x >> 2) & 2);
            return bl + cl + bn;
        }

        private static readonly byte[] Tim2 = { (byte)'T', (byte)'I', (byte)'M', (byte)'2' };
        private static int IndexOf(byte[] a, byte[] p, int from)
        {
            for (int i = from; i <= a.Length - p.Length; i++)
            {
                int k = 0;
                while (k < p.Length && a[i + k] == p[k]) k++;
                if (k == p.Length) return i;
            }
            return -1;
        }
    }
}
