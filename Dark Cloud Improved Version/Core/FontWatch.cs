using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// DIAGNOSTIC — temporary (2026-09-15). Dungeon text windows draw some glyphs with a short bar struck through them: the same
    /// mark on every copy of a letter, different letters in different kinds of window. It happens with the app CLOSED, in
    /// dungeons only, never in town (user test), so the patched disc is involved and the mod's runtime code is not.
    ///
    /// The texture manager (EnterTexture 0x1313B0) gives every texture VRAM by bumping its GROUP's top upward, and most groups
    /// share one upload window that starts at 0x1A40: each is re-uploaded from its EE copy right before it draws. The groups
    /// that sit ABOVE that window (the message font's pages are up there) are only safe while no shared-window group grows
    /// into them. Xiao's pack now carries the cat's textures, which lifted her group's top from 0x1E00 to 0x21A0 — past a
    /// dedicated group at 0x20D0. That may or may not be it; vanilla groups reach high too. So instead of a fifth theory this
    /// logs the manager's whole layout once per dungeon floor (and whenever it changes, e.g. a character switch): every
    /// group's VRAM range, every texture's pages, and for each texture OUTSIDE the shared window, which other groups reach
    /// into its pages. The EE copies of the font textures are saved to EnhancedModLogs/fontdump whenever their bytes change,
    /// so the damaged bytes themselves can be inspected.
    /// </summary>
    internal static class FontWatch
    {
        private const string Tag = "[FontWatch] ";
        private const long Manager = 0x21C75870;                                   // CTextureManager, 20,040 B
        private const int  Blocks = 0x18, BlockStride = 0x3C, BlockCount = 0x48, BlkBase = 0x20, BlkTop = 0x24;
        private const int  Entries = 0x10F8, EntryStride = 0x50, EntryCount = 0xC4; // 196 entries fill the object exactly
        private const int  EntW = 0x02, EntH = 0x04, EntBpp = 0x06, EntName = 0x08, EntTex0 = 0x28, EntPix = 0x38, EntClut = 0x48;
        private const uint SharedWindow = 0x1A40;                                   // the upload window most groups page into
        private const int  MaxDumpBytes = 0x80000;
        private static readonly string[] FontNames = { "font", "ank", "gaiji", "syst", "fuki", "mes" };

        private static Thread _thread;
        private static string _lastLayout = "";
        private static readonly Dictionary<string, byte[]> _lastBytes = new();

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
        }

        private static void Loop()
        {
            bool wasInDungeon = false;
            DateTime nextLayout = DateTime.MaxValue, nextBytes = DateTime.MaxValue;
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);
                    if (!Memory.IsConnected) continue;
                    bool inDungeon = Player.InDungeonFloor();
                    if (inDungeon && !wasInDungeon) { nextLayout = DateTime.UtcNow.AddSeconds(8); nextBytes = nextLayout; }   // let the floor finish loading
                    wasInDungeon = inDungeon;
                    if (!inDungeon) continue;
                    if (DateTime.UtcNow >= nextLayout) { LogLayoutIfChanged(); nextLayout = DateTime.UtcNow.AddSeconds(10); }
                    if (DateTime.UtcNow >= nextBytes)  { DumpFontsIfChanged(); nextBytes = DateTime.UtcNow.AddSeconds(30); }
                }
                catch (Exception ex) { Console.WriteLine(Tag + "error: " + ex.Message); Thread.Sleep(5000); }
            }
        }

        private readonly record struct Entry(int Index, short Block, int W, int H, int Bpp, string Name, ulong Tex0, uint Pix, uint Clut)
        {
            public uint Tbp => (uint)(Tex0 & 0x3FFF);
            public uint Cbp => (uint)((Tex0 >> 37) & 0x3FFF);
            public int  Psm => (int)((Tex0 >> 20) & 0x3F);
            public uint PixBlocks => (uint)((W * H * Bpp + 0xFF) >> 8);             // VRAM blocks are 256 B
            public bool Indexed => Psm == 0x13;
        }

        private static (List<Entry> entries, (uint b, uint t)[] blocks)? ReadManager()
        {
            byte[] m = Memory.ReadBytesBatch(Manager, Entries + EntryCount * EntryStride);   // the whole object in one trip
            if (m == null) return null;
            var blocks = new (uint, uint)[BlockCount];
            for (int b = 0; b < BlockCount; b++)
                blocks[b] = (BitConverter.ToUInt32(m, Blocks + b * BlockStride + BlkBase), BitConverter.ToUInt32(m, Blocks + b * BlockStride + BlkTop));
            var entries = new List<Entry>();
            for (int i = 0; i < EntryCount; i++)
            {
                int o = Entries + i * EntryStride;
                if (m[o + EntName] == 0) continue;
                int len = 0; while (len < 32 && m[o + EntName + len] != 0) len++;
                entries.Add(new Entry(i, BitConverter.ToInt16(m, o), BitConverter.ToUInt16(m, o + EntW), BitConverter.ToUInt16(m, o + EntH),
                                      BitConverter.ToUInt16(m, o + EntBpp), Encoding.ASCII.GetString(m, o + EntName, len),
                                      BitConverter.ToUInt64(m, o + EntTex0), BitConverter.ToUInt32(m, o + EntPix), BitConverter.ToUInt32(m, o + EntClut)));
            }
            return (entries, blocks);
        }

        private static void LogLayoutIfChanged()
        {
            var r = ReadManager();
            if (r == null) return;
            var (entries, blocks) = r.Value;
            var sig = new StringBuilder();
            for (int b = 0; b < BlockCount; b++) if (blocks[b].t != 0) sig.Append($"{b:X2}:{blocks[b].b:X}-{blocks[b].t:X};");
            foreach (var e in entries) sig.Append($"{e.Index}:{e.Name}:{e.Tex0:X};");
            string s = sig.ToString();
            if (s == _lastLayout) return;
            _lastLayout = s;

            string stamp = ReusableFunctions.GetDateTimeForLog();
            Console.WriteLine(stamp + Tag + $"layout changed — {entries.Count} texture(s)");
            var gl = new StringBuilder();
            for (int b = 0; b < BlockCount; b++)
                if (blocks[b].t != 0) gl.Append($" [{b:X2}] 0x{blocks[b].b:X}-0x{blocks[b].t:X}{(blocks[b].b == SharedWindow ? "" : " (own pages)")}");
            Console.WriteLine(Tag + "groups:" + gl);
            foreach (var e in entries)
            {
                uint pEnd = e.Tbp + e.PixBlocks;
                string line = $"  #{e.Index,3} {e.Name,-20} grp 0x{e.Block:X2} {e.W}x{e.H}x{e.Bpp} psm 0x{e.Psm:X2} pages 0x{e.Tbp:X}-0x{pEnd:X}"
                            + (e.Indexed ? $" clut 0x{e.Cbp:X}" : "") + $" ee 0x{e.Pix:X}/0x{e.Clut:X}";
                bool shared = e.Block >= 0 && e.Block < BlockCount && blocks[e.Block].b == SharedWindow;
                if (!shared)                                                   // outside the shared window: who reaches it?
                {
                    var hits = new List<string>();
                    for (int b = 0; b < BlockCount; b++)
                    {
                        if (b == e.Block || blocks[b].t == 0 || blocks[b].t <= blocks[b].b) continue;
                        bool pix = blocks[b].t > e.Tbp && blocks[b].b < pEnd;
                        bool clut = e.Indexed && blocks[b].t > e.Cbp && blocks[b].b < e.Cbp + 4;
                        if (pix || clut) hits.Add($"0x{b:X2}(0x{blocks[b].b:X}-0x{blocks[b].t:X}{(pix ? "" : " clut only")})");
                    }
                    if (hits.Count > 0) line += "  <-- reached by " + string.Join(", ", hits);
                }
                Console.WriteLine(Tag + line);
            }
        }

        private static void DumpFontsIfChanged()
        {
            var r = ReadManager();
            if (r == null) return;
            string dir = Path.Combine(AppContext.BaseDirectory, "EnhancedModLogs", "fontdump");
            foreach (var e in r.Value.entries)
            {
                string lower = e.Name.ToLowerInvariant();
                if (!FontNames.Any(lower.Contains)) continue;
                int size = e.W * e.H * e.Bpp;
                if (size <= 0 || size > MaxDumpBytes || !Memory.IsValidGuest(e.Pix)) continue;
                byte[] pix = Memory.ReadBytesBatch(Memory.ToMmu(e.Pix), size);
                if (pix == null) continue;
                byte[] clut = e.Indexed && Memory.IsValidGuest(e.Clut) ? Memory.ReadBytesBatch(Memory.ToMmu(e.Clut), 0x400) : null;
                byte[] both = clut == null ? pix : pix.Concat(clut).ToArray();
                string key = $"{e.Index}:{e.Name}";
                if (_lastBytes.TryGetValue(key, out var prev) && prev.AsSpan().SequenceEqual(both)) continue;

                Directory.CreateDirectory(dir);
                string file = $"{DateTime.Now:HHmmss}_{e.Index}_{e.Name}_{e.W}x{e.H}x{e.Bpp}_psm{e.Psm:X2}";
                File.WriteAllBytes(Path.Combine(dir, file + ".bin"), pix);
                if (clut != null) File.WriteAllBytes(Path.Combine(dir, file + "_clut.bin"), clut);
                string diff = "";
                if (prev != null && prev.Length == both.Length)
                {
                    var runs = new List<string>();
                    for (int i = 0; i < both.Length && runs.Count < 8; i++)
                    {
                        if (both[i] == prev[i]) continue;
                        int j = i; while (j < both.Length && both[j] != prev[j]) j++;
                        runs.Add(i < pix.Length ? $"pix+0x{i:X} ({(i / e.Bpp) % Math.Max(1, e.W)},{(i / e.Bpp) / Math.Max(1, e.W)}) ×{j - i}" : $"clut+0x{i - pix.Length:X} ×{j - i}");
                        i = j;
                    }
                    diff = " — changed since last dump: " + string.Join(", ", runs);
                }
                _lastBytes[key] = both;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"saved #{e.Index} {e.Name} ({size:N0} B{(clut != null ? " + clut" : "")}) → fontdump/{file}.bin{diff}");
            }
        }
    }
}
