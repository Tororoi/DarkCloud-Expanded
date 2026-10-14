using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The pockles' motion files trimmed to what their clips play: every keyframe outside all of the pack's KEY windows is
    /// dropped, keeping in each track the keyframe just before and just after every window (the interpolation brackets), so every
    /// clip plays exactly as before. Both the body's motion (MOTION 0) and the shadow model's (SHADOW_MOTION) share the KEY table and
    /// are trimmed alike. It frees about 46 KB per pockle wherever one loads — on a floor and in the summoned ally's block
    /// (AllyBlock), whose model room is sized for the largest trimmed pockle. Idempotent: a trimmed motion has nothing left outside
    /// its windows but the brackets.</summary>
    internal static class PockleMotionBake
    {
        /// <summary>The seven day pockles and Hell Pockle (the ally summon's roster).</summary>
        internal static readonly string[] Codes = { "e14a", "e15a", "e16a", "e17a", "e18a", "e19a", "e20a", "e62a" };

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            int done = 0;
            foreach (string code in Codes)
            {
                string file = $@"dun\monstor\{code}.chr";
                byte[] src = arc.Read(file);
                var pack = ChrPack.Parse(src);
                var cfg = pack.Records.FirstOrDefault(r => r.Name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                          ?? throw new IOException($"{file}: no cfg record");
                string text = Encoding.Latin1.GetString(cfg.Payload);
                var windows = Windows(text);
                if (windows.Count == 0) throw new IOException($"{file}: no KEY windows in {cfg.Name}");
                int before = 0, after = 0;
                foreach (string mot in MotionNames(text))
                {
                    var rec = pack.Require(mot);
                    var m = MotFile.FromRecord(rec);
                    before += m.Tracks.Sum(t => t.Keyframes.Count);
                    Trim(m, windows);
                    after += m.Tracks.Sum(t => t.Keyframes.Count);
                    rec.ReplacePayload(m.BuildPayload());
                }
                if (after == before) { log($"{file}: already trimmed ({after:N0} keys)"); continue; }
                byte[] chr = pack.Rebuild();
                arc.Redirect(file, chr);
                log($"{file}: motions trimmed to their {windows.Count} KEY windows — {before:N0} → {after:N0} keys, {src.Length:N0} → {chr.Length:N0} B");
                done++;
            }
            log($"pockle motions: {done} redirected");
        }

        /// <summary>The cfg's KEY windows (start, end), KEY_START excluded.</summary>
        private static List<(uint lo, uint hi)> Windows(string cfg)
            => Regex.Matches(cfg, @"^\s*KEY\s+(\d+)\s*,\s*(\d+)", RegexOptions.Multiline)
                    .Select(m => (uint.Parse(m.Groups[1].Value), uint.Parse(m.Groups[2].Value))).Distinct().ToList();

        /// <summary>The motion files the cfg loads: MOTION n's and SHADOW_MOTION's.</summary>
        private static IEnumerable<string> MotionNames(string cfg)
            => Regex.Matches(cfg, @"^\s*(?:MOTION\s+\d+|SHADOW_MOTION)\s*,?\s*""([^""]+\.mot)""", RegexOptions.Multiline)
                    .Select(m => m.Groups[1].Value).Distinct();

        /// <summary>Each track keeps its keyframes inside a window and the one just before and just after every window.</summary>
        private static void Trim(MotFile mot, List<(uint lo, uint hi)> windows)
        {
            foreach (var t in mot.Tracks)
            {
                var keys = t.Keyframes.OrderBy(k => k.Frame).ToList();
                var keep = new HashSet<int>();
                foreach (var (lo, hi) in windows)
                {
                    int below = -1, above = -1;
                    for (int i = 0; i < keys.Count; i++)
                    {
                        uint f = keys[i].Frame;
                        if (f < lo) below = i;
                        else if (f <= hi) keep.Add(i);
                        else if (above < 0) above = i;
                    }
                    if (below >= 0) keep.Add(below);
                    if (above >= 0) keep.Add(above);
                }
                t.Keyframes = keep.OrderBy(i => i).Select(i => keys[i]).ToList();
            }
        }
    }
}
