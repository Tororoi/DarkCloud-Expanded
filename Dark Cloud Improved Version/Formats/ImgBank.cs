using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A texture bank with the IM2 table layout under either magic (`IMG\0` or `IM2\0`): 0x10 header (magic, count @+4),
    /// 0x30 entries (name @0, bank-relative TIM2 offset @+0x20), the TIM2 blocks after the table. An IM2 bank's 8-bit pictures are in
    /// PSMT8 block order, an IMG bank's row-major (<see cref="Swizzled"/>). Read by the bakes and, at runtime, by the cash models
    /// (CashModel, QueensTrees, IwaModel).</summary>
    internal sealed class ImgBank
    {
        private const int Hdr = 0x10, Ent = 0x30;
        internal readonly byte[] Magic, Data; internal readonly List<(string name, int off)> Entries = new();
        /// <summary>An IM2 bank's 8-bit pictures are in PSMT8 block order (an IMG bank's row-major).</summary>
        internal bool Swizzled => Magic[2] == (byte)'2';
        internal ImgBank(byte[] data)
        {
            string m = Encoding.Latin1.GetString(data, 0, 4);
            if (m != "IMG\0" && m != "IM2\0") throw new IOException("not an IMG/IM2 bank");
            Magic = data.AsSpan(0, 4).ToArray(); Data = data;
            int count = (int)IsoBytes.U32(data, 4);
            for (int i = 0; i < count; i++) { int e = Hdr + i * Ent; Entries.Add((IsoBytes.NameAt(data, e, 0x20), (int)IsoBytes.U32(data, e + 0x20))); }
        }
        /// <summary>The bytes of entry <paramref name="name"/> up to the next entry's offset (or the bank's end): the TIM2 and any
        /// padding after it.</summary>
        internal byte[] Block(string name)
        {
            var offs = Entries.Select(x => x.off).OrderBy(o => o).ToList();
            foreach (var (n, o) in Entries)
                if (n == name) { int nxt = offs.FirstOrDefault(x => x > o, Data.Length); return Data.AsSpan(o, nxt - o).ToArray(); }
            throw new KeyNotFoundException(name);
        }
        internal static byte[] Build(byte[] magic, List<(string name, byte[] blob)> items)
        {
            var outp = new List<byte>();
            outp.AddRange(magic); outp.AddRange(BitConverter.GetBytes(items.Count)); outp.AddRange(new byte[8]); outp.AddRange(new byte[items.Count * Ent]);
            for (int i = 0; i < items.Count; i++)
            {
                int e = Hdr + i * Ent;
                var nb = Encoding.Latin1.GetBytes(items[i].name); int len = Math.Min(nb.Length, 0x1F);
                for (int k = 0; k < len; k++) outp[e + k] = nb[k];
                var ob = BitConverter.GetBytes(outp.Count); for (int k = 0; k < 4; k++) outp[e + 0x20 + k] = ob[k];
                outp.AddRange(items[i].blob);
                outp.AddRange(new byte[ExactMath.Mod(-outp.Count, 16)]);
            }
            return outp.ToArray();
        }

        /// <summary>Whether a bank's magic (`IMG\0` or `IM2\0`) sits at <paramref name="at"/> in <paramref name="data"/>.</summary>
        internal static bool IsBankAt(byte[] data, int at)
            => at + 4 <= data.Length && data[at] == 'I' && data[at + 1] == 'M' && (data[at + 2] == '2' || data[at + 2] == 'G') && data[at + 3] == 0;

        /// <summary>The ABSOLUTE offset in <paramref name="data"/> of entry <paramref name="name"/>'s TIM2 in the bank at
        /// <paramref name="bank"/> (count @+4, 0x30 entries from +0x10, name @0, bank-relative offset @+0x20); −1 when absent or when
        /// the table would run past the data.</summary>
        internal static int EntryOffset(byte[] data, int bank, string name)
        {
            int count = (int)IsoBytes.U32(data, bank + 4);
            for (int e = 0; e < count; e++)
            {
                int ent = bank + Hdr + e * Ent;
                if (ent + Ent > data.Length) return -1;
                if (IsoBytes.NameAt(data, ent, 0x20) == name) return bank + (int)IsoBytes.U32(data, ent + 0x20);
            }
            return -1;
        }
    }
}
