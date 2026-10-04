using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>One `.mot` keyframe: 0x20 bytes — frame index @0 (ascending, sparse on the .mot's single timeline), three
    /// zero words, then f32[4] (a quaternion for rotation tracks).</summary>
    internal sealed class MotKeyframe
    {
        internal const int Size = 0x20;
        internal readonly byte[] Raw;
        internal MotKeyframe(byte[] raw) { if (raw.Length != Size) throw new ArgumentException("keyframe size"); Raw = raw; }
        internal uint Frame { get => IsoBytes.U32(Raw, 0); set => IsoBytes.U32(Raw, 0, value); }
        internal float[] Value => new[] { IsoBytes.F32(Raw, 0x10), IsoBytes.F32(Raw, 0x14), IsoBytes.F32(Raw, 0x18), IsoBytes.F32(Raw, 0x1C) };
        internal MotKeyframe Copy() => new MotKeyframe((byte[])Raw.Clone());
    }

    /// <summary>One `.mot` track: w0 = the joint's index in the model's frame table, w2 = channel (0 rotation, 2 the second
    /// channel), w3 = 0x20, then the keyframes; `cont` (the track's size) is 0 on the last track.</summary>
    internal sealed class MotTrack
    {
        internal const int HeaderSize = 0x20;
        internal const uint TagW6 = 0x74700000, TagW7 = 0x747BFE95;
        internal uint W0, W1, W2, W3, W6, W7;
        internal List<MotKeyframe> Keyframes = new List<MotKeyframe>();

        internal (uint, uint) Key => (W0, W2);
        internal IEnumerable<MotKeyframe> FramesIn(uint lo, uint hi) => Keyframes.Where(k => lo <= k.Frame && k.Frame <= hi);

        internal byte[] Build(bool isLast)
        {
            int count = Keyframes.Count;
            uint cont = isLast ? 0u : (uint)(HeaderSize + count * MotKeyframe.Size);
            var o = new byte[HeaderSize + count * MotKeyframe.Size];
            IsoBytes.U32(o, 0, W0); IsoBytes.U32(o, 4, W1); IsoBytes.U32(o, 8, W2); IsoBytes.U32(o, 12, W3);
            IsoBytes.U32(o, 16, (uint)count); IsoBytes.U32(o, 20, cont); IsoBytes.U32(o, 24, W6); IsoBytes.U32(o, 28, W7);
            for (int i = 0; i < count; i++) Array.Copy(Keyframes[i].Raw, 0, o, HeaderSize + i * MotKeyframe.Size, MotKeyframe.Size);
            return o;
        }
    }

    /// <summary>A decoded `.mot` record: the pack-record header (name + size words) and its tracks. Named motions are not
    /// in the .mot: they are `KEY start,end,speed` lines in the pack's cfg, each a window on the one frame timeline.</summary>
    internal sealed class MotFile
    {
        internal byte[] Head;         // the record's 0x00..DataOff
        internal int DataOff;
        internal List<MotTrack> Tracks = new List<MotTrack>();

        internal static MotFile FromRecord(ChrRecord rec)
        {
            byte[] blob = rec.Raw;
            var m = new MotFile { Head = blob.AsSpan(0, rec.DataOff).ToArray(), DataOff = rec.DataOff };
            int p = rec.DataOff, end = rec.DataOff + rec.Size;
            while (p < end)
            {
                var t = new MotTrack
                {
                    W0 = IsoBytes.U32(blob, p), W1 = IsoBytes.U32(blob, p + 4), W2 = IsoBytes.U32(blob, p + 8), W3 = IsoBytes.U32(blob, p + 12),
                    W6 = IsoBytes.U32(blob, p + 24), W7 = IsoBytes.U32(blob, p + 28),
                };
                int count = (int)IsoBytes.U32(blob, p + 16); uint cont = IsoBytes.U32(blob, p + 20);
                int ko = p + MotTrack.HeaderSize;
                for (int i = 0; i < count; i++) t.Keyframes.Add(new MotKeyframe(blob.AsSpan(ko + i * MotKeyframe.Size, MotKeyframe.Size).ToArray()));
                m.Tracks.Add(t);
                p = ko + count * MotKeyframe.Size;
                if (cont == 0) break;
            }
            return m;
        }

        internal static MotFile FromPack(ChrPack pack, string motName) => FromRecord(pack.Require(motName));

        internal MotTrack TrackBy(uint bone, uint chan) => Tracks.FirstOrDefault(t => t.W0 == bone && t.W2 == chan);

        internal byte[] BuildPayload()
        {
            var ms = new System.IO.MemoryStream();
            for (int i = 0; i < Tracks.Count; i++) { byte[] b = Tracks[i].Build(i == Tracks.Count - 1); ms.Write(b, 0, b.Length); }
            return ms.ToArray();
        }

        /// <summary>The full pack record (header + payload) with the size and stride words refreshed (the stride unpadded,
        /// as the game's own .mot records are).</summary>
        internal byte[] Rebuild()
        {
            byte[] payload = BuildPayload();
            var head = (byte[])Head.Clone();
            IsoBytes.U32(head, 0x44, (uint)payload.Length); IsoBytes.U32(head, 0x48, (uint)(DataOff + payload.Length));
            var o = new byte[head.Length + payload.Length];
            Array.Copy(head, o, head.Length); Array.Copy(payload, 0, o, head.Length, payload.Length);
            return o;
        }
    }
}
