using System;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Read-only access to original game data, sourced from the player's OWN Dark Cloud ISO at runtime — they
    /// are running it in the emulator, so the disc image is present on their machine. No pre-extraction and no
    /// environment variables: the ISO is found by searching common locations (and an optional config file),
    /// validated by finding the DC data archive inside it. Features must fail soft when <see cref="Available"/>
    /// is false (a user who hasn't pointed us at their ISO simply doesn't get these enhancements — no crash).
    ///
    /// The DC archive lives inside the ISO9660 filesystem as three files: DATA.HED = 80-byte full-path name
    /// entries; entry i's (offset,size) within DATA.DAT are the two u32s at DATA.HD2 + 16 + i*32.
    /// </summary>
    internal static class GameDataFiles
    {
        // Optional override: a text file whose first line is the full path to the DC1 ISO. Lets a user point us
        // at a non-standard location without any env var. (Config, not code — safe for game features to read.)
        private static string ConfigFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DarkCloudEnhanced", "dc1_iso.txt");

        private static bool _resolved;
        private static Iso9660 _iso;                 // the open, validated DC1 disc image (null if none found)
        private static byte[] _hed, _hd2;            // cached indexes (~1 MB total)

        /// <summary>True once we've located a DC1 ISO with the data archive and loaded its indexes.</summary>
        internal static bool Available
        {
            get { Resolve(); return _iso != null; }
        }

        /// <summary>The full bytes of the DATA.DAT entry whose path ends with <paramref name="suffix"/>
        /// (e.g. <c>"monstor\\e50a.stb"</c>, <c>"commenu\\weapon\\c04w13.chr"</c>), or null.</summary>
        internal static byte[] TryReadEntry(string suffix)
        {
            if (!Available) return null;
            try
            {
                for (int i = 0; i < _hed.Length / 80; i++)
                {
                    int end = Array.IndexOf(_hed, (byte)0, i * 80, 80); if (end < 0) end = i * 80 + 80;
                    string name = System.Text.Encoding.ASCII.GetString(_hed, i * 80, end - i * 80);
                    if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                    uint off = BitConverter.ToUInt32(_hd2, 16 + i * 32);
                    uint size = BitConverter.ToUInt32(_hd2, 16 + i * 32 + 4);
                    return _iso.ReadFile("DATA.DAT", off, size);   // seek within DATA.DAT's extent + off
                }
            }
            catch (Exception e) { Console.WriteLine($"[GameData] read failed for {suffix}: {e.Message}"); }
            return null;
        }

        /// <summary>Find + open a DC1 ISO once. Order: config file → *.iso under ~/ROMs, the app dir, and the
        /// user profile. A candidate qualifies only if DATA.HED/HD2/DAT are present inside it.</summary>
        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            foreach (string cand in Candidates())
            {
                try
                {
                    var iso = Iso9660.TryOpen(cand);
                    if (iso == null) continue;
                    byte[] hed = iso.ReadWholeFile("DATA.HED");
                    byte[] hd2 = iso.ReadWholeFile("DATA.HD2");
                    if (hed == null || hd2 == null || iso.FindFile("DATA.DAT") == null) { iso.Dispose(); continue; }
                    _iso = iso; _hed = hed; _hd2 = hd2;
                    Console.WriteLine($"[GameData] using ISO: {cand}");
                    return;
                }
                catch { /* not a usable DC1 ISO — try the next */ }
            }
            Console.WriteLine("[GameData] no Dark Cloud ISO found (searched ~/ROMs, app dir; or set "
                + ConfigFile + ") — features needing original disc data are disabled");
        }

        private static System.Collections.Generic.IEnumerable<string> Candidates()
        {
            if (File.Exists(ConfigFile))
            {
                string p = File.ReadAllLines(ConfigFile).FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
                if (!string.IsNullOrEmpty(p)) yield return p;
            }
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string dir in new[] { Path.Combine(home, "ROMs"), AppContext.BaseDirectory, home })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string f in SafeEnum(dir, "*.iso")) yield return f;
            }
        }

        private static string[] SafeEnum(string dir, string pat)
        { try { return Directory.GetFiles(dir, pat); } catch { return Array.Empty<string>(); } }
    }

    /// <summary>Minimal read-only ISO9660 reader — just enough to locate a root-level file by name and read its
    /// bytes. Handles the standard 2048-byte logical sector image (what PS2 game ISOs use).</summary>
    internal sealed class Iso9660 : IDisposable
    {
        private const int SECTOR = 2048;
        private readonly FileStream _fs;
        // name -> (byte offset of extent in the image, size)
        private readonly System.Collections.Generic.Dictionary<string, (long off, long size)> _root
            = new System.Collections.Generic.Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

        private Iso9660(FileStream fs) { _fs = fs; }

        /// <summary>Open + parse the root directory, or return null if this isn't a 2048-byte ISO9660 image.</summary>
        internal static Iso9660 TryOpen(string path)
        {
            FileStream fs = null;
            try
            {
                fs = File.OpenRead(path);
                var pvd = ReadAt(fs, 16L * SECTOR, SECTOR);
                if (pvd[0] != 1 || pvd[1] != 'C' || pvd[2] != 'D' || pvd[3] != '0' || pvd[4] != '0' || pvd[5] != '1')
                { fs.Dispose(); return null; }                     // not a plain 2048/sector ISO9660
                uint rootLba = BitConverter.ToUInt32(pvd, 156 + 2);
                uint rootSize = BitConverter.ToUInt32(pvd, 156 + 10);
                var iso = new Iso9660(fs);
                iso.ParseDir(rootLba, rootSize);
                return iso;
            }
            catch { fs?.Dispose(); return null; }
        }

        private void ParseDir(uint lba, uint size)
        {
            byte[] dir = ReadAt(_fs, (long)lba * SECTOR, (int)size);
            int pos = 0;
            while (pos + 33 <= dir.Length)
            {
                int len = dir[pos];
                if (len == 0) { pos = (pos / SECTOR + 1) * SECTOR; continue; }   // pad to next sector
                uint ext = BitConverter.ToUInt32(dir, pos + 2);
                uint dlen = BitConverter.ToUInt32(dir, pos + 10);
                int flags = dir[pos + 25];
                int nlen = dir[pos + 32];
                if ((flags & 2) == 0 && nlen > 0)                                // a FILE (not a directory)
                {
                    string name = System.Text.Encoding.ASCII.GetString(dir, pos + 33, nlen);
                    int semi = name.IndexOf(';'); if (semi >= 0) name = name.Substring(0, semi);  // strip ";1"
                    _root[name] = ((long)ext * SECTOR, dlen);
                }
                pos += len;
            }
        }

        internal (long off, long size)? FindFile(string name)
            => _root.TryGetValue(name, out var v) ? v : ((long, long)?)null;

        internal byte[] ReadWholeFile(string name)
        {
            var e = FindFile(name); if (e == null) return null;
            return ReadAt(_fs, e.Value.off, (int)e.Value.size);
        }

        /// <summary>Read <paramref name="size"/> bytes at <paramref name="innerOffset"/> within a file's extent.</summary>
        internal byte[] ReadFile(string name, long innerOffset, uint size)
        {
            var e = FindFile(name); if (e == null) return null;
            return ReadAt(_fs, e.Value.off + innerOffset, (int)size);
        }

        private static byte[] ReadAt(FileStream fs, long offset, int size)
        {
            var buf = new byte[size];
            fs.Seek(offset, SeekOrigin.Begin);
            int got = 0; while (got < size) { int r = fs.Read(buf, got, size - got); if (r <= 0) break; got += r; }
            return buf;
        }

        public void Dispose() => _fs?.Dispose();
    }
}
