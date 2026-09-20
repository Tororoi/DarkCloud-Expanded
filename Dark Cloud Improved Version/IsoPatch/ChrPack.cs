using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>One sub-file inside a `.chr` pack: name (0x40 bytes, NUL-terminated), payload offset @0x40 (0x50),
    /// size @0x44, stride @0x48 (the next record starts at +stride), a runtime fixup word @0x4C preserved verbatim,
    /// then the payload. <see cref="Raw"/> is the whole record.</summary>
    internal sealed class ChrRecord
    {
        internal const int DataOffStd = 0x50;
        internal const uint RecTag = 0x00140E02;   // the word at +0x4C on every vanilla record
        internal string Name;
        internal int DataOff, Size, Stride;
        internal byte[] Raw;

        internal byte[] Payload => Raw.AsSpan(DataOff, Size).ToArray();

        /// <summary>A fresh record, stride padded to 16 like every vanilla record.</summary>
        internal static ChrRecord Create(string name, byte[] payload, uint tag = RecTag)
        {
            int stride = (DataOffStd + payload.Length + 15) & ~15;
            var raw = new byte[stride];
            byte[] nb = Encoding.Latin1.GetBytes(name);
            Array.Copy(nb, raw, Math.Min(nb.Length, 0x3F));
            IsoBytes.U32(raw, 0x40, DataOffStd); IsoBytes.U32(raw, 0x44, (uint)payload.Length); IsoBytes.U32(raw, 0x48, (uint)stride);
            IsoBytes.U32(raw, 0x4C, tag);
            Array.Copy(payload, 0, raw, DataOffStd, payload.Length);
            return new ChrRecord { Name = name, DataOff = DataOffStd, Size = payload.Length, Stride = stride, Raw = raw };
        }

        /// <summary>Swap the payload; size and stride follow, the stride padded to 16 (a following .mds is DMA'd and
        /// an unaligned one renders as garbled shards).</summary>
        internal void ReplacePayload(byte[] payload)
        {
            int stride = (DataOff + payload.Length + 15) & ~15;
            var raw = new byte[stride];
            Array.Copy(Raw, raw, DataOff);
            IsoBytes.U32(raw, 0x44, (uint)payload.Length); IsoBytes.U32(raw, 0x48, (uint)stride);
            Array.Copy(payload, 0, raw, DataOff, payload.Length);
            Raw = raw; Size = payload.Length; Stride = stride;
        }
    }

    /// <summary>A `.chr` pack: a flat chain of records (no front index) ending at a record whose first byte is 0, plus
    /// the trailing bytes. Parse → Rebuild of an untouched pack is byte-exact.</summary>
    internal sealed class ChrPack
    {
        internal readonly List<ChrRecord> Records = new List<ChrRecord>();
        internal byte[] Trailer = Array.Empty<byte>();

        internal static ChrPack Parse(byte[] blob)
        {
            var pack = new ChrPack();
            int p = 0;
            while (p < blob.Length && blob[p] != 0)
            {
                int dataOff = (int)IsoBytes.U32(blob, p + 0x40), size = (int)IsoBytes.U32(blob, p + 0x44), stride = (int)IsoBytes.U32(blob, p + 0x48);
                if (stride == 0) break;
                pack.Records.Add(new ChrRecord
                {
                    Name = IsoBytes.NameAt(blob, p, 0x40), DataOff = dataOff, Size = size, Stride = stride,
                    Raw = blob.AsSpan(p, stride).ToArray(),
                });
                p += stride;
            }
            pack.Trailer = blob.AsSpan(p).ToArray();
            return pack;
        }

        /// <summary>Case-insensitive basename match, like GetPackFile.</summary>
        internal ChrRecord Find(string name)
        {
            string b = name.Replace('\\', '/');
            b = b.Substring(b.LastIndexOf('/') + 1);
            foreach (var r in Records)
                if (string.Equals(r.Name, b, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        internal ChrRecord Require(string name) => Find(name) ?? throw new IOException($"no record '{name}' in the pack");

        internal byte[] Rebuild()
        {
            var ms = new MemoryStream();
            foreach (var r in Records) ms.Write(r.Raw, 0, r.Raw.Length);
            ms.Write(Trailer, 0, Trailer.Length);
            return ms.ToArray();
        }
    }
}
