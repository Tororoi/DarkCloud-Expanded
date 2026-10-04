using System;
using System.Collections.Generic;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
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
