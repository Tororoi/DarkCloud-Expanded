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

    /// <summary>Motion splicing between models: a track's w0 is a positional index into the model's `.mds` frame table
    /// (AnimeDataInit: joint = frame_base + w0 × 0x270), not a stable joint id, so a transplant between two models of a
    /// character remaps w0 through the joint NAMES.</summary>
    internal static class MotSplice
    {
        /// <summary>The ordered CFrame node names of a `.mds` payload; index == a track's w0 (count @+0x08, record stride
        /// @+0x14, table @0x18, name at record+0).</summary>
        internal static List<string> ReadMdsFrames(byte[] mds)
        {
            int count = (int)IsoBytes.U32(mds, 8), stride = (int)IsoBytes.U32(mds, 0x14);
            var names = new List<string>(count);
            for (int i = 0; i < count; i++) names.Add(IsoBytes.NameAt(mds, 0x18 + i * stride, 0x20));
            return names;
        }

        /// <summary>src w0 → dst w0 by joint name; frame 0 (the model root, whose name often differs) maps 0→0; names absent
        /// in dst map to −1 (dropped).</summary>
        internal static int[] BuildJointRemap(List<string> src, List<string> dst)
        {
            var dstOf = new Dictionary<string, int>();
            for (int i = 0; i < dst.Count; i++) if (!dstOf.ContainsKey(dst[i])) dstOf[dst[i]] = i;
            var remap = new int[src.Count];
            for (int i = 0; i < src.Count; i++) remap[i] = i == 0 ? 0 : (dstOf.TryGetValue(src[i], out int d) ? d : -1);
            return remap;
        }

        internal sealed class Report
        {
            internal int Written, Remapped, DroppedNoJoint, DroppedNoTrack;
            internal readonly List<string> NoJoint = new List<string>(), NoTrack = new List<string>();
        }

        /// <summary>For every source track with keyframes in [srcLo, srcHi], remap its joint to the dest joint of the same
        /// name (same channel) and replace that dest track's [destLo, destHi] window with the source keys shifted by
        /// destLo − srcLo. Dest tracks that receive nothing keep their originals.</summary>
        internal static Report SpliceByJoint(MotFile dest, MotFile src, List<string> srcFrames, List<string> dstFrames,
                                             uint srcLo, uint srcHi, uint destLo, uint destHi)
        {
            int[] remap = BuildJointRemap(srcFrames, dstFrames);
            long delta = (long)destLo - srcLo;
            var dstBy = new Dictionary<(uint, uint), MotTrack>();
            foreach (var t in dest.Tracks) dstBy[t.Key] = t;          // last wins, as the Python dict did
            var rep = new Report();
            foreach (var st in src.Tracks)
            {
                var win = st.FramesIn(srcLo, srcHi).ToList();
                if (win.Count == 0) continue;
                string nm = st.W0 < srcFrames.Count ? srcFrames[(int)st.W0] : "?";
                int dw = st.W0 < remap.Length ? remap[st.W0] : -1;
                if (dw < 0) { rep.DroppedNoJoint++; rep.NoJoint.Add(nm); continue; }
                if (!dstBy.TryGetValue(((uint)dw, st.W2), out var dt)) { rep.DroppedNoTrack++; rep.NoTrack.Add(nm); continue; }
                var kept = dt.Keyframes.Where(k => !(destLo <= k.Frame && k.Frame <= destHi)).ToList();
                var added = new List<MotKeyframe>();
                foreach (var kf in win)
                {
                    var c = kf.Copy();
                    long f = kf.Frame + delta;
                    if (f < destLo || f > destHi) continue;
                    c.Frame = (uint)f;
                    added.Add(c);
                }
                dt.Keyframes = kept.Concat(added).OrderBy(k => k.Frame).ToList();   // a stable sort, like Python's
                rep.Written++;
                if (st.W0 != (uint)dw) rep.Remapped++;
            }
            return rep;
        }
    }
}
