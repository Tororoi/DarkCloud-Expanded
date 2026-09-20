using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>DATA.DAT through the ISO for the patch flow's post-steps: read an archive file by its DATA.HED name, or
    /// redirect one to new bytes written into the free tail (past the highest byte any DATA.HD2 slot uses, so every
    /// earlier redirect is respected). The ISO stays open for the object's lifetime.</summary>
    internal sealed class IsoArchive : IDisposable
    {
        private readonly FileStream _fs;
        private readonly byte[] _hed;
        private readonly long _datIso, _datSize, _hd2Base;
        private long _tail;
        private readonly Action<string> _log;

        internal long FreeBytes => _datSize - _tail;

        internal IsoArchive(string iso, Action<string> log = null)
        {
            _log = log ?? (_ => { });
            _fs = new FileStream(iso, FileMode.Open, FileAccess.ReadWrite);
            var recs = ParseRoot(_fs);
            _datIso = (long)recs["DATA.DAT"].Ext * SectorBytes;
            _datSize = recs["DATA.DAT"].Size;
            _hd2Base = (long)recs["DATA.HD2"].Ext * SectorBytes + 16;
            _hed = Rd(_fs, (long)recs["DATA.HED"].Ext * SectorBytes, (int)recs["DATA.HED"].Size);
            long end = 0;
            for (int i = 0; i < _hed.Length / 80; i++)
            {
                long off = RdU32(_fs, Slot(i)), size = RdU32(_fs, Slot(i) + 4);
                if (off + size > 0 && off + size <= _datSize) end = Math.Max(end, off + size);
            }
            _tail = Align(end);
        }

        private long Slot(int i) => _hd2Base + (long)i * 32;
        private long SlotOf(string name) => Slot(ArchiveFind(_hed, name));

        internal byte[] Read(string name)
        {
            long s = SlotOf(name);
            return Rd(_fs, _datIso + RdU32(_fs, s), (int)RdU32(_fs, s + 4));
        }

        /// <summary>A file's DATA.HD2 record: (offset into DATA.DAT, size, sector, sector count).</summary>
        internal (uint off, uint size, uint sec, uint cnt) Slot(string name)
        {
            long s = SlotOf(name);
            return (RdU32(_fs, s), RdU32(_fs, s + 4), RdU32(_fs, s + 8), RdU32(_fs, s + 12));
        }
        /// <summary>A file's DATA.HD2 record rewritten (a revert to the vanilla record: the bytes are already on the disc).</summary>
        internal void SetSlot(string name, (uint off, uint size, uint sec, uint cnt) rec)
        {
            long s = SlotOf(name);
            WrU32(_fs, s, rec.off); WrU32(_fs, s + 4, rec.size); WrU32(_fs, s + 8, rec.sec); WrU32(_fs, s + 12, rec.cnt);
        }
        /// <summary>Raw DATA.DAT bytes at an offset.</summary>
        internal byte[] ReadAt(long off, int size) => Rd(_fs, _datIso + off, size);

        /// <summary>The file now reads as <paramref name="data"/>: written at the tail, the slot repointed, read back.</summary>
        internal void Redirect(string name, byte[] data)
        {
            long slot = SlotOf(name);
            if (_tail + data.Length > _datSize) throw new IOException("Ran out of DATA.DAT tail space.");
            Wr(_fs, _datIso + _tail, data);
            uint sec = (uint)(_tail >> 11), cnt = (uint)((data.Length + SectorBytes - 1) / SectorBytes);
            WrU32(_fs, slot, (uint)_tail); WrU32(_fs, slot + 4, (uint)data.Length); WrU32(_fs, slot + 8, sec); WrU32(_fs, slot + 12, cnt);
            byte[] back = Rd(_fs, _datIso + (long)sec * SectorBytes, data.Length);
            if (!back.AsSpan().SequenceEqual(data)) throw new IOException($"{name}: readback after redirect differs.");
            _log($"redirected {name}: -> {data.Length:N0} B @sector 0x{sec:x}");
            _tail = Align(_tail + data.Length);
        }

        public void Dispose() => _fs.Dispose();
    }
}
