using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Creates a patched COPY of the user's stock Dark Cloud (USA) ISO with the fishing signs baked in, and
    /// publishes the matching pnach to the PCSX2 cheats folder. Cross-platform (macOS / Windows / Linux).
    ///
    /// LEGAL: the sign's mesh + texture are carved from the user's OWN ISO at patch time (from the Muska Lacka
    /// oasis) and re-injected into the fishing towns — nothing game-derived is bundled with the mod. The patch
    /// only rearranges data already on the user's disc (absorb the trailing DMMY. padding into DATA.DAT, then
    /// redirect DATA.HD2 entries), so their original ISO is never modified.
    ///
    /// This is the C# port of tools/iso_patch/build_sign_iso.py (+ sign_scene.py + ps2iso.py). The byte-level
    /// patch steps are ported in <see cref="ApplySignPatch"/>.
    /// </summary>
    internal static class IsoPatcher
    {
        internal const int  SECTOR     = 2048;
        internal const string OutputName = "Dark Cloud - Expanded.iso";

        /// <summary>PCSX2's per-user cheats folder, resolved per OS (created if missing).</summary>
        internal static string Pcsx2CheatsDir()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dir;
            if (OperatingSystem.IsMacOS())
                dir = Path.Combine(home, "Library", "Application Support", "PCSX2", "cheats");
            else if (OperatingSystem.IsWindows())
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2", "cheats");
            else
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdg)) xdg = Path.Combine(home, ".config");
                dir = Path.Combine(xdg, "PCSX2", "cheats");
            }
            return dir;
        }

        /// <summary>Patch a COPY of <paramref name="stockIso"/> into <paramref name="outDir"/>. Returns the
        /// output ISO path. Reports human-readable progress via <paramref name="progress"/>. Throws on error.</summary>
        internal static string Patch(string stockIso, string outDir, Action<string> progress)
        {
            if (string.IsNullOrWhiteSpace(stockIso) || !File.Exists(stockIso))
                throw new FileNotFoundException("Stock ISO not found. Select your Dark Cloud (USA) .iso first.");
            if (string.IsNullOrWhiteSpace(outDir)) outDir = Path.GetDirectoryName(stockIso);
            Directory.CreateDirectory(outDir);

            string outIso = Path.Combine(outDir, OutputName);
            if (Path.GetFullPath(outIso).Equals(Path.GetFullPath(stockIso), StringComparison.OrdinalIgnoreCase))
                throw new IOException("That output folder would overwrite your stock ISO — pick a different folder.");

            progress($"Copying ISO → {OutputName} …");
            File.Copy(stockIso, outIso, overwrite: true);

            using (var fs = new FileStream(outIso, FileMode.Open, FileAccess.ReadWrite))
            {
                uint crc = ApplySignPatch(fs, progress);
                progress("Publishing pnach to PCSX2 …");
                ReshipPnach(crc, progress);
            }

            progress("Done.");
            return outIso;
        }

        /// <summary>The byte-level patch on the open output ISO. Returns the patched ELF CRC (for the pnach).
        /// PORT IN PROGRESS — mirrors build_sign_iso.py: absorb DMMY., redirect mes_tex.pak/scene.scn/mapinfo.cfg,
        /// carve the sign assets, ELF boot-cave, CRC.</summary>
        private static uint ApplySignPatch(FileStream fs, Action<string> progress)
        {
            throw new NotImplementedException(
                "The ISO byte-patch (absorb + redirects + carve + ELF cave) is being ported from build_sign_iso.py.");
        }

        private static void ReshipPnach(uint crc, Action<string> progress)
        {
            // Reads the mod's bundled A5C05C78.pnach (its own patch data — not a game asset) and writes
            // <CRC>.pnach into the PCSX2 cheats folder with the CRC swapped in the gametitle.
            throw new NotImplementedException("pnach reship — ported alongside ApplySignPatch.");
        }
    }
}
