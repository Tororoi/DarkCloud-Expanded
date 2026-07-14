using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Read-only access to the EXTRACTED game data files on disk (data.hed / data.hd2 / data.dat) — the
    /// mod runs on the same machine as the game image, so features can pull original assets/scripts
    /// without the game having to load them (weapon textures, enemy STB bytes, …).
    /// Index format: data.hed = 80-byte full-path name entries; entry i's offset/size are the two u32s at
    /// data.hd2 16 + i*32. Features should fail soft when <see cref="Available"/> is false.
    /// </summary>
    internal static class GameDataFiles
    {
        internal const string DataDir = "/Users/thomascantwell/ROMs/dc_extracted";

        private static bool? _available;
        private static byte[] _hed, _hd2;   // cached indexes (~1MB total)

        internal static bool Available
        {
            get
            {
                if (_available == null)
                    _available = File.Exists(Path.Combine(DataDir, "data.hed")) &&
                                 File.Exists(Path.Combine(DataDir, "data.hd2")) &&
                                 File.Exists(Path.Combine(DataDir, "data.dat"));
                return _available.Value;
            }
        }

        /// <summary>The full bytes of the data.dat entry whose path ends with <paramref name="suffix"/>
        /// (e.g. <c>"monstor\\e50a.stb"</c>, <c>"commenu\\weapon\\c04w13.chr"</c>), or null.</summary>
        internal static byte[] TryReadEntry(string suffix)
        {
            if (!Available) return null;
            try
            {
                _hed ??= File.ReadAllBytes(Path.Combine(DataDir, "data.hed"));
                _hd2 ??= File.ReadAllBytes(Path.Combine(DataDir, "data.hd2"));
                for (int i = 0; i < _hed.Length / 80; i++)
                {
                    int end = Array.IndexOf(_hed, (byte)0, i * 80, 80); if (end < 0) end = i * 80 + 80;
                    string name = System.Text.Encoding.ASCII.GetString(_hed, i * 80, end - i * 80);
                    if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                    uint off = BitConverter.ToUInt32(_hd2, 16 + i * 32);
                    uint size = BitConverter.ToUInt32(_hd2, 16 + i * 32 + 4);
                    var file = new byte[size];
                    using (var dat = File.OpenRead(Path.Combine(DataDir, "data.dat")))
                    { dat.Seek(off, SeekOrigin.Begin); dat.Read(file, 0, (int)size); }
                    return file;
                }
            }
            catch (Exception e) { Console.WriteLine($"[GameData] read failed for {suffix}: {e.Message}"); }
            return null;
        }
    }
}
