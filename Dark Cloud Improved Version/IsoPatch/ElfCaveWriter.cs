using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The ISO-patch primitives the Elf*Patches and DunPatches caves share: an embedded stub's bytes, a cave written
    /// word by word behind a bounds check, the signed lui/lw split of a guest address, a hook word replaced behind its
    /// vanilla-or-ours guard, and a stub-signature scan. Each guard throws an IOException carrying the site's own text.</summary>
    internal static class ElfCaveWriter
    {
        private const string ResourcePrefix = "Dark_Cloud_Improved_Version.Resources.isoPatch.";

        /// <summary>The bytes of the embedded stub <c>Resources/isoPatch/&lt;name&gt;</c> (tools/stubs/build_ee_stubs.py output).</summary>
        internal static byte[] Embedded(string name) =>
            Embedded(name, $"Embedded EE function missing: {name} (run tools/stubs/build_ee_stubs.py and rebuild)");

        /// <summary>The bytes of the embedded resource <c>Resources/isoPatch/&lt;name&gt;</c>; <paramref name="missing"/> is the
        /// whole message thrown when the assembly does not carry it.</summary>
        internal static byte[] Embedded(string name, string missing)
        {
            using var st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name)
                ?? throw new IOException(missing);
            using var ms = new MemoryStream(); st.CopyTo(ms); return ms.ToArray();
        }

        /// <summary>The words written at <paramref name="addr"/> upwards, after <paramref name="overrun"/> is thrown when they would
        /// reach <paramref name="limit"/> (the first address past the cave's host or gap).</summary>
        internal static void WriteWords(FileStream fs, Func<uint, long> ElfOff, uint addr, uint[] words, uint limit, string overrun)
        {
            if (addr + (uint)words.Length * 4 > limit) throw new IOException(overrun);
            WriteWords(fs, ElfOff, addr, words);
        }

        /// <summary>The words written at <paramref name="addr"/> upwards (the caller has bounded them).</summary>
        internal static void WriteWords(FileStream fs, Func<uint, long> ElfOff, uint addr, uint[] words)
        {
            for (int i = 0; i < words.Length; i++) WrU32(fs, ElfOff(addr + (uint)(i * 4)), words[i]);
        }

        /// <summary>A stub's bytes written at <paramref name="addr"/> upwards a word at a time, after <paramref name="overrun"/> is
        /// thrown when they would reach <paramref name="limit"/>.</summary>
        internal static void WriteBytes(FileStream fs, Func<uint, long> ElfOff, uint addr, byte[] b, uint limit, string overrun)
        {
            if (addr + (uint)b.Length > limit) throw new IOException(overrun);
            WriteBytes(fs, ElfOff, addr, b);
        }

        /// <summary>A stub's bytes written at <paramref name="addr"/> upwards a word at a time (the caller has bounded them).</summary>
        internal static void WriteBytes(FileStream fs, Func<uint, long> ElfOff, uint addr, byte[] b)
        {
            for (int i = 0; i < b.Length; i += 4) WrU32(fs, ElfOff(addr + (uint)i), U32(b, i));
        }

        /// <summary>A guest address split for a `lui rX,hi` / `lw …,lo(rX)` pair: lo is the low half as the SIGNED immediate the
        /// load sign-extends, hi the high half carried by one when lo ≥ 0x8000, so (hi &lt;&lt; 16) + (short)lo == addr.</summary>
        internal static void HiLo(uint addr, out uint hi, out uint lo)
        {
            hi = (addr + 0x8000u) >> 16;
            lo = addr & 0xFFFFu;
        }

        /// <summary>The word at <paramref name="site"/> becomes <paramref name="ours"/> when it is <paramref name="vanilla"/> or already
        /// ours; anything else throws "<paramref name="what"/> at 0x… is 0x…, not vanilla 0x… — unmodified Dark Cloud (USA) ISO expected."</summary>
        internal static void ReplaceWord(FileStream fs, Func<uint, long> ElfOff, uint site, uint vanilla, uint ours, string what) =>
            ReplaceWord(fs, ElfOff, site, vanilla, ours,
                        cur => $"{what} at 0x{site:X} is 0x{cur:X8}, not vanilla 0x{vanilla:X8} — unmodified Dark Cloud (USA) ISO expected.");

        /// <summary>The word at <paramref name="site"/> becomes <paramref name="ours"/> when it is <paramref name="vanilla"/> or already
        /// ours; anything else throws the message <paramref name="notVanilla"/> builds from the word found.</summary>
        internal static void ReplaceWord(FileStream fs, Func<uint, long> ElfOff, uint site, uint vanilla, uint ours, Func<uint, string> notVanilla)
        {
            uint cur = RdU32(fs, ElfOff(site));
            if (cur != vanilla && cur != ours) throw new IOException(notVanilla(cur));
            WrU32(fs, ElfOff(site), ours);
        }

        /// <summary>Whether any aligned word of a stub's bytes is <paramref name="word"/> (a call, jump or immediate the stub must carry).</summary>
        internal static bool ContainsWord(byte[] b, uint word)
        {
            for (int i = 0; i + 4 <= b.Length; i += 4) if (U32(b, i) == word) return true;
            return false;
        }
    }
}
