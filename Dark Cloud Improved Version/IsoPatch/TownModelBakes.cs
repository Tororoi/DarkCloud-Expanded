using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A swapped-in town ally's model assembled at patch time: the town motion slots (docs/town-swap-animation-map.md:
    /// 0 idle · 1 run · 2 walk · 3 push-door · 4 pull-door · 5 item-get · 6 item-get-loop · 7 damage · 8 fall · 9 land · 10
    /// double-door, then TownLadder's choreography slots) filled by transplanting each chosen clip, same rig by joint name,
    /// into the base model's body + shadow `.mot`, the base cfg's KEY table rewritten to the slots, and the rebuilt `.chr`
    /// redirected into the ISO's free tail. A slot is KEPT (the base's own frames) or GRAFTED (a clip from another model
    /// spliced into a free window, body + shadow together); post-ops hold, offset, reverse, freeze or pin a window. Every
    /// model is read from the player's own ISO.</summary>
    internal static class TownModelBakes
    {
        private sealed class Slot
        {
            internal int Idx; internal (int lo, int hi) Frames; internal double Speed; internal string Name;
            internal string Src, SrcCfg; internal (int lo, int hi) Win;
            internal double[] RootOffset; internal bool Reverse; internal int? Hold, RootFreeze;
            internal (string[] nodes, int refFrame)? PinNodes;
        }
        private sealed class Spin { internal string Node; internal double[] Axis; internal double Rate, HandoffAdvance; internal (int, int) RampUp, Loop, RampDown; }
        private sealed class MeshGraft { internal string Src; internal int PoseFrame; internal string[] Nodes; internal Spin Spin; }
        private sealed class ShadowInject { internal string Src, Model, Mot, Bbp, Wgt; }
        private sealed class Character
        {
            internal string Name, Base; internal bool RootMotion; internal MeshGraft MeshGraft; internal ShadowInject ShadowInject; internal Slot[] Slots;
        }

        private static Slot S(int idx, int lo, int hi, double speed, string name, string src = null, (int, int)? win = null, string srcCfg = null,
                              double[] rootOffset = null, bool reverse = false, int? hold = null, int? rootFreeze = null, (string[], int)? pin = null)
            => new Slot { Idx = idx, Frames = (lo, hi), Speed = speed, Name = name, Src = src, Win = win ?? (0, 0), SrcCfg = srcCfg,
                          RootOffset = rootOffset, Reverse = reverse, Hold = hold, RootFreeze = rootFreeze, PinNodes = pin };

        private static readonly Character[] Chars =
        {
            new Character
            {
                Name = "Xiao", Base = "gedit/e01/chara/c04pcat.chr",
                Slots = new[]
                {
                    S(0, 10, 20, 0.10, "idle"),
                    S(1, 120, 136, 0.50, "run", "gedit/s86/chara/c04cat.chr", (120, 136)),
                    S(2, 60, 80, 0.30, "walk"),
                    // door = e04c04cat #5's vertical opening, pulled back off the door (the cat is longer than Toan, so the
                    // door-open teleport overshoots the head into the door); root_offset −z = back
                    S(3, 150, 159, 0.20, "door", "gedit/e01/chara/e04c04cat.chr", (160, 169), rootOffset: new[] { 0.0, 0.0, -2.0 }),
                    S(4, 150, 159, 0.20, "door2"),                                   // reuse the grafted door frames
                    S(5, 30, 40, 0.10, "sit", "gedit/s86/chara/c04cat.chr", (30, 40)),  // a complete looping sit, shared by both item slots
                    S(6, 30, 40, 0.10, "sit-loop"),
                    // slot 7 (damage, never triggered in town) = the ladder REFUSAL: e613c04cat's head-shake sequence
                    S(7, 228, 278, 0.30, "refuse(shake)", "dun/d01/event/e613c04cat.chr", (115, 165)),
                    S(8, 205, 214, 0.50, "fall(leap)", "gedit/s86/chara/c04cat.chr", (205, 214)),
                    S(9, 215, 227, 0.36, "land", "gedit/s86/chara/c04cat.chr", (215, 227)),
                    S(10, 150, 159, 0.20, "door3(dbl)"),                             // double-door: Toan's c01d has an 11th KEY
                    // choreography (TownLadder's jump script): 11 = s86 #3 "ready" crouch; 12 = the float/hop-up, no root offset
                    S(11, 95, 105, 0.40, "jump-ready", "gedit/s86/chara/c04cat.chr", (95, 105)),
                    S(12, 285, 294, 0.60, "float-up", "gedit/e01/chara/e04c04cat.chr", (160, 169)),
                    S(13, 300, 320, 0.30, "walk-back", "gedit/e01/chara/c04pcat.chr", (60, 80), reverse: true),   // the base's walk, reversed
                },
            },
            // Osmond: base c18p (4 KEYs). The replayable talk-to-Osmond scene plays idx 0 and 3, reproduced EXACTLY; doors reuse
            // the talk clip; run = the same clip in its own window at a bumped speed; item-get = talk held at frame 69.
            new Character
            {
                Name = "Osmond", Base = "gedit/e05/chara/c18p.chr",
                // MESH-NODE GRAFT: c18p lacks the helicopter propeller assembly; the 13 nodes come from the s13 flight model with their
                // rigid MDT chunks and .bbp rows, held at e402c18a's stowed pose (frame 15) outside the heli-clip windows. The spin is
                // SYNTHESIZED on the hub (e402c18a never rotates the assembly; vanilla's spin is an un-ported VERTEX_ANIME stream):
                // rate 72°/clip-frame, the loop phase-shifted by the hand-off advance (rate × TownLadder's 0.5× playback = 36°) so
                // every window hand-off advances the displayed rotation by exactly one engine step.
                MeshGraft = new MeshGraft
                {
                    Src = "gedit/s13/chara/e402c18a.chr", PoseFrame = 15,
                    Nodes = new[] { "bone4", "bone5", "bone6", "tukene1", "obj41", "obj42", "obj43", "obj38", "obj39", "obj40", "obj35", "obj36", "obj37" },
                    Spin = new Spin { Node = "tukene1", Axis = new[] { 0.0, 1.0, 0.0 }, Rate = 72.0, HandoffAdvance = 36.0, RampUp = (240, 255), Loop = (260, 270), RampDown = (275, 290) },
                },
                Slots = new[]
                {
                    S(0, 10, 20, 0.10, "idle"),                                        // EXACT original (cutscene)
                    S(1, 130, 150, 0.75, "run(sped)", "gedit/e05/chara/c18p.chr", (30, 50)),   // its own window: the run↔walk blend breaks when both share one
                    S(2, 30, 50, 0.20, "walk"),
                    S(3, 60, 80, 0.20, "door(talk)"),                                  // EXACT original (cutscene)
                    S(4, 60, 80, 0.30, "door2(talk)"),
                    S(5, 90, 92, 0.10, "item(hold69)", hold: 69),                       // talk hand-out pose, held
                    S(6, 90, 92, 0.10, "item-loop"),
                    S(7, 10, 20, 0.10, "damage(skip)"),
                    S(8, 100, 108, 0.30, "fall", "gedit/e05/chara/e403c18a.chr", (210, 218)),
                    S(9, 110, 125, 0.30, "land", "gedit/e05/chara/e403c18a.chr", (220, 235)),
                    S(10, 60, 80, 0.30, "door3(dbl)"),
                    // choreography: down-ladder = e403 #9 jump-down dive; up-ladder = the helicopter set + the reversed pair for the landing
                    S(11, 155, 175, 0.30, "jump-down", "gedit/e05/chara/e403c18a.chr", (185, 205)),
                    S(12, 180, 235, 0.25, "propeller", "gedit/s13/chara/e402c18a.chr", (280, 335)),
                    S(13, 240, 255, 0.25, "start-fly", "gedit/s13/chara/e402c18a.chr", (345, 360)),
                    S(14, 260, 270, 0.20, "fly-loop", "gedit/s13/chara/e402c18a.chr", (360, 370)),
                    S(15, 275, 290, 0.25, "rev-startfly", "gedit/s13/chara/e402c18a.chr", (345, 360), reverse: true),
                    S(16, 295, 350, 0.30, "rev-propeller(stow)", "gedit/s13/chara/e402c18a.chr", (280, 335), reverse: true),
                },
            },
            // Goro: base c06p has real separate run/walk windows, kept EXACT; doors = idle; item-get = dungeon c06b #34/#35;
            // refusal = #31; fall/land = the vanilla descent's own clips (e101c06a); ladder hops = e102's alternating jumps.
            new Character
            {
                Name = "Goro", Base = "gedit/s01/chara/c06p.chr", RootMotion = true,   // recover root/hip channels the town .mot never carried
                Slots = new[]
                {
                    S(0, 10, 20, 0.15, "idle"), S(1, 60, 80, 0.40, "run"), S(2, 30, 50, 0.25, "walk"),
                    S(3, 10, 20, 0.15, "door(idle)"), S(4, 10, 20, 0.15, "door2(idle)"),
                    S(5, 90, 100, 0.30, "item", "dun/mainchara/c06b.chr", (515, 525)),
                    S(6, 105, 115, 0.30, "item-loop", "dun/mainchara/c06b.chr", (530, 540)),
                    S(7, 120, 149, 0.50, "refuse(no)", "dun/mainchara/c06b.chr", (421, 450)),
                    S(8, 155, 175, 0.30, "fall", "gedit/s01/chara/e101.chr", (75, 95), srcCfg: "e101c06a.cfg"),
                    S(9, 180, 190, 0.25, "land(stand)", "gedit/s01/chara/e101.chr", (95, 105), srcCfg: "e101c06a.cfg"),
                    S(10, 10, 20, 0.15, "door3(idle)"),
                    S(11, 195, 215, 0.30, "climb-hopA", "gedit/s01/chara/e102.chr", (90, 110), srcCfg: "e102c06a.cfg"),
                    S(12, 220, 240, 0.30, "climb-hopB", "gedit/s01/chara/e102.chr", (110, 130), srcCfg: "e102c06a.cfg"),
                    S(13, 245, 255, 0.25, "rev-land", "gedit/s01/chara/e101.chr", (95, 105), srcCfg: "e101c06a.cfg", reverse: true),
                },
            },
            // Ungaga: e323_2c10a (cloth + native run window + shadow). Slots 0–4 keep his own windows (3/4 = the talk clips); the
            // battle run is grafted into the native 60–80 window; new clips go past his 226-frame timeline. Ladders: refused (slot 7).
            new Character
            {
                Name = "Ungaga", Base = "gedit/e04/chara/e323_2c10a.chr", RootMotion = true,
                Slots = new[]
                {
                    S(0, 10, 20, 0.10, "idle"),
                    S(1, 60, 80, 0.55, "run", "dun/mainchara/c10b.chr", (60, 80)),
                    S(2, 30, 50, 0.27, "walk"),
                    S(3, 115, 131, 0.20, "door(chest)", rootOffset: new[] { 0.0, 0.0, -2.5 }),   // the chest-hand talk gesture, pulled back off the door
                    S(4, 115, 131, 0.20, "door2"),
                    S(5, 240, 250, 0.30, "item", "dun/mainchara/c10b.chr", (630, 640)),
                    S(6, 255, 265, 0.30, "item-loop", "dun/mainchara/c10b.chr", (643, 653)),
                    S(7, 270, 310, 0.22, "refuse(NG)", "dun/mainchara/c10b.chr", (490, 530), pin: (new[] { "kon_1", "r_handa" }, 15)),   // the staff rides the hand at its idle grip
                    S(8, 315, 315, 0.0, "fall(296)", "dun/mainchara/c10b.chr", (296, 296)),    // a static falling pose: a 1-frame KEY at speed 0
                    S(9, 357, 358, 0.20, "land(rev)", "dun/mainchara/c10b.chr", (295, 296), reverse: true),
                    S(10, 115, 131, 0.20, "door3"),
                },
            },
            // Ruby: base c05a "simple" (idle/walk/run, NO shadow) + a shadow injected from e223c05a; locomotion from dun c05a; doors =
            // e223's pat; float/jump = e228 (root travel frozen: its jump bakes the cutscene's 43u leap).
            new Character
            {
                Name = "Ruby", Base = "gedit/e03/chara/c05a.chr", RootMotion = true,
                ShadowInject = new ShadowInject { Src = "gedit/e03/chara/e223c05a.chr", Model = "e223c05s.mds", Mot = "e223c05s.mot", Bbp = "e223c05s.bbp", Wgt = "e223c05s.wgt" },
                Slots = new[]
                {
                    S(0, 10, 20, 0.15, "idle", "dun/mainchara/c05a.chr", (10, 20)),
                    S(1, 60, 80, 0.55, "run", "dun/mainchara/c05a.chr", (60, 80)),
                    S(2, 30, 50, 0.25, "walk", "dun/mainchara/c05a.chr", (30, 50)),
                    S(3, 90, 102, 0.15, "door(pat)", "gedit/e03/chara/e223c05a.chr", (100, 112), rootOffset: new[] { 0.0, 0.0, -3.0 }),
                    S(4, 90, 102, 0.15, "door2(pat)"),
                    S(5, 120, 130, 0.30, "item", "dun/mainchara/c05a.chr", (575, 585)),
                    S(6, 135, 145, 0.30, "item-loop", "dun/mainchara/c05a.chr", (590, 600)),
                    S(7, 150, 170, 0.20, "refuse(no)", "dun/mainchara/c05a.chr", (605, 625)),
                    S(8, 175, 195, 0.10, "fall(float)", "gedit/e03/chara/e228c05a.chr", (10, 30)),
                    S(9, 10, 20, 0.15, "land(idle)"),
                    S(10, 90, 102, 0.15, "door3(pat)"),
                    S(11, 200, 230, 0.20, "jump", "gedit/e03/chara/e228c05a.chr", (285, 315), rootFreeze: 200),
                    S(12, 235, 245, 0.10, "jump-loop", "gedit/e03/chara/e228c05a.chr", (320, 330), rootFreeze: 175),
                },
            },
        };

        // ───────────────────────────────────────────── cfg text ─────────────────────────────────────────────
        private static string L1(byte[] b) => Encoding.Latin1.GetString(b);
        private static byte[] L1(string s) => Encoding.Latin1.GetBytes(s);

        private sealed class CfgMotions { internal ChrRecord Cfg; internal string BodyMot, BodyMds, ShadowMot, ShadowMds; }

        /// <summary>The pack's main cfg (the one holding MODEL/MOTION/KEY) and the body/shadow motion + model record names.
        /// Dual-bundled event models (e101/e102 carry Toan AND Goro) need <paramref name="prefer"/>, the exact cfg name.</summary>
        private static CfgMotions CfgMotionsOf(ChrPack pack, string prefer = null)
        {
            var cand = pack.Records.Where(r => r.Name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)).ToList();
            if (prefer != null)
                cand = cand.Where(r => string.Equals(r.Name, prefer, StringComparison.OrdinalIgnoreCase))
                           .Concat(cand.Where(r => !string.Equals(r.Name, prefer, StringComparison.OrdinalIgnoreCase))).ToList();
            ChrRecord cfg = cand.FirstOrDefault(r => { string t = L1(r.Payload); return t.Contains("KEY_START") && t.Contains("MOTION"); }) ?? cand.FirstOrDefault();
            string text = L1(cfg.Payload);
            string Grab(string tag) { var m = Regex.Match(text, tag + "[ \t]+\"([^\"]+)\""); return m.Success ? m.Groups[1].Value : null; }
            string bodyMot = Grab("MOTION[ \t]+0,");
            if (bodyMot == null) { var m = Regex.Match(text, "MOTION[ \t]+0,[ \t]*\"([^\"]+)\""); bodyMot = m.Success ? m.Groups[1].Value : null; }
            var sm = Regex.Match(text, "SHADOW_MOTION[ \t]+\"([^\"]+)\"");
            return new CfgMotions { Cfg = cfg, BodyMot = bodyMot, BodyMds = Grab("MODEL"), ShadowMot = sm.Success ? sm.Groups[1].Value : null, ShadowMds = Grab("SHADOW_MODEL") };
        }

        /// <summary>The body MOTION 0 line's .bbp filename (the second quoted argument).</summary>
        private static string CfgBbp(byte[] cfg)
        {
            var m = Regex.Match(L1(cfg), "MOTION[ \t]+0,[ \t]*\"[^\"]+\",[ \t]*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        // ───────────────────────────────────────────── math ─────────────────────────────────────────────
        /// <summary>Hamilton product, scalar-first (w, x, y, z), the .mot chan-0 storage order.</summary>
        private static double[] QuatMul(double[] a, double[] b) => new[]
        {
            a[0] * b[0] - a[1] * b[1] - a[2] * b[2] - a[3] * b[3],
            a[0] * b[1] + a[1] * b[0] + a[2] * b[3] - a[3] * b[2],
            a[0] * b[2] - a[1] * b[3] + a[2] * b[0] + a[3] * b[1],
            a[0] * b[3] + a[1] * b[2] - a[2] * b[1] + a[3] * b[0],
        };

        /// <summary>3×3 → quaternion, the exact inverse of the engine's quaternion-to-matrix (Shepperd).</summary>
        private static double[] MatToQuat(double[][] R)
        {
            double m00 = R[0][0], m01 = R[0][1], m02 = R[0][2], m10 = R[1][0], m11 = R[1][1], m12 = R[1][2], m20 = R[2][0], m21 = R[2][1], m22 = R[2][2];
            double tr = m00 + m11 + m22, s;
            if (tr > 0) { s = Math.Sqrt(tr + 1.0) * 2; return new[] { 0.25 * s, (m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s }; }
            if (m00 >= m11 && m00 >= m22) { s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2; return new[] { (m21 - m12) / s, 0.25 * s, (m01 + m10) / s, (m02 + m20) / s }; }
            if (m11 >= m22) { s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2; return new[] { (m02 - m20) / s, (m01 + m10) / s, 0.25 * s, (m12 + m21) / s }; }
            s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2; return new[] { (m10 - m01) / s, (m02 + m20) / s, (m12 + m21) / s, 0.25 * s };
        }

        /// <summary>(parent, 3×3 bind rows, bind translation) of node i in a .mds payload.</summary>
        private static (int par, double[][] R, double[] tr) MdsBind(byte[] pl, int i)
        {
            int p = 0x18 + i * 0x70;
            int par = BitConverter.ToInt32(pl, p + 0x24);
            var M = new double[16];
            for (int k = 0; k < 16; k++) M[k] = IsoBytes.F32(pl, p + 0x28 + k * 4);
            return (par, new[] { new[] { M[0], M[1], M[2] }, new[] { M[4], M[5], M[6] }, new[] { M[8], M[9], M[10] } }, new[] { M[12], M[13], M[14] });
        }

        private static double[] Values(MotKeyframe k) => Array.ConvertAll(k.Value, x => (double)x);
        private static void PackValue(MotKeyframe k, double[] v) { for (int i = 0; i < 4; i++) IsoBytes.WrF(k.Raw, 0x10 + i * 4, (float)v[i]); }
        private static MotKeyframe NewKey(uint frame, double[] v)
        {
            var k = new MotKeyframe(new byte[MotKeyframe.Size]);
            k.Frame = frame; PackValue(k, v); return k;
        }
        private static double Dot(double[] a, double[] b) { double s = 0; for (int i = 0; i < a.Length; i++) s += a[i] * b[i]; return s; }
        private static void SortKeys(MotTrack t) => t.Keyframes = t.Keyframes.OrderBy(k => k.Frame).ToList();

        /// <summary>The complement of the graft windows within [lo, hi]: the spans where grafted nodes hold the folded pose.</summary>
        private static List<(int lo, int hi)> FoldSpans(IEnumerable<(int lo, int hi)> windows, int lo, int hi)
        {
            var spans = new List<(int, int)>(); int cur = lo;
            foreach (var (a, b) in windows.OrderBy(w => w.lo).ThenBy(w => w.hi))
            {
                if (cur < a) spans.Add((cur, a - 1));
                cur = Math.Max(cur, b + 1);
            }
            if (cur <= hi) spans.Add((cur, hi));
            return spans;
        }

        /// <summary>The track's interpolated value at <paramref name="frame"/>: lerp for translations, sign-corrected nlerp for
        /// rotation quaternions. Null when the track has no keys.</summary>
        private static double[] SampleTrack(MotTrack t, int frame)
        {
            MotKeyframe prev = null, next = null;
            foreach (var k in t.Keyframes) { if (k.Frame <= frame && (prev == null || k.Frame > prev.Frame)) prev = k; if (k.Frame >= frame && (next == null || k.Frame < next.Frame)) next = k; }
            if (prev == null && next == null) return null;
            if (prev == null) return Values(next);
            if (next == null || next.Frame == prev.Frame) return Values(prev);
            double u = (double)(frame - prev.Frame) / (next.Frame - prev.Frame);
            double[] a = Values(prev), b = Values(next);
            if (t.W2 == 0)
            {
                if (Dot(a, b) < 0) b = b.Select(x => -x).ToArray();
                var v = new double[4]; for (int i = 0; i < 4; i++) v[i] = a[i] + (b[i] - a[i]) * u;
                double n = Math.Sqrt(Dot(v, v)); if (n == 0) n = 1.0;
                return v.Select(x => x / n).ToArray();
            }
            var r = new double[4]; for (int i = 0; i < 4; i++) r[i] = a[i] + (b[i] - a[i]) * u;
            return r;
        }

        private static IEnumerable<int> SpanEdges(List<(int lo, int hi)> spans)
        {
            foreach (var (lo, hi) in spans) { yield return lo; if (hi != lo) yield return hi; }
        }

        private static void ReplaceMot(ChrPack pack, string motName, MotFile m)
        {
            byte[] rec = m.Rebuild();
            pack.Require(motName).ReplacePayload(rec.AsSpan(m.DataOff).ToArray());
        }

        // ───────────────────────────────────────────── mesh-node graft + spin ─────────────────────────────────────────────
        /// <summary>Append the named CFrame nodes (+ their rigid MDT mesh chunks + .bbp bind rows) from the source rig onto the base
        /// body .mds, and give the base body .mot one track per source blade channel anchored to the source's stowed pose across
        /// every span OUTSIDE the graft windows. Runs BEFORE the motion grafts: the splice only writes dest tracks that exist. .mds:
        /// count @+0x08, node table @0x18 stride 0x70 {name, MESH offset @+0x20 (absolute, 0 = none), parent @+0x24, bind matrix
        /// @+0x28, id @+0x68, 0x70 @+0x6C}; the LAST record's two trailing words overlap the first MDT chunk, so appending K nodes
        /// shifts the mesh block by K×0x70 and every existing mesh offset is rebased. .bbp = count × 64-byte bind rows.</summary>
        private static void GraftMeshNodes(ChrPack basePack, ChrPack sp, MeshGraft mg, List<(int lo, int hi)> windows, int maxFrame)
        {
            var scfg = CfgMotionsOf(sp); var cfg = CfgMotionsOf(basePack);
            string[] names = mg.Nodes;
            byte[] bp = basePack.Require(cfg.BodyMds).Payload, spl = sp.Require(scfg.BodyMds).Payload;
            var bnames = MotSplice.ReadMdsFrames(bp); var snames = MotSplice.ReadMdsFrames(spl);
            int nb = bnames.Count, K = names.Length;
            var dup = names.Where(bnames.Contains).ToList();
            if (dup.Count > 0) throw new IOException($"mesh_graft: nodes already in base rig: {string.Join(", ", dup)}");
            var missing = names.Where(n => !snames.Contains(n)).ToList();
            if (missing.Count > 0) throw new IOException($"mesh_graft: nodes not in source rig: {string.Join(", ", missing)}");
            if (cfg.ShadowMds != null)
            {
                var shn = MotSplice.ReadMdsFrames(basePack.Require(cfg.ShadowMds).Payload);
                var clash = names.Where(shn.Contains).ToList();
                if (clash.Count > 0) throw new IOException($"mesh_graft: base SHADOW rig carries {string.Join(", ", clash)} — mirroring unsupported");
            }
            int mesh0 = 0x18 + nb * 0x70 - 8;
            var firsts = Enumerable.Range(0, nb).Select(i => (int)IsoBytes.U32(bp, 0x18 + i * 0x70 + 0x20)).Where(o => o != 0).ToList();
            if (firsts.Min() != mesh0 || !(bp[mesh0] == 'M' && bp[mesh0 + 1] == 'D' && bp[mesh0 + 2] == 'T' && bp[mesh0 + 3] == 0))
                throw new IOException("mesh_graft: base .mds table/mesh-overlap layout not as expected");
            int delta = K * 0x70;
            var newIndex = new Dictionary<string, int>(); for (int k = 0; k < K; k++) newIndex[names[k]] = nb + k;
            // node table: complete the old last record, bump the count, rebase existing mesh offsets
            var table = new List<byte>(bp.Take(mesh0)); table.AddRange(BitConverter.GetBytes((uint)nb)); table.AddRange(BitConverter.GetBytes(0x70u));
            var tb = table.ToArray();
            IsoBytes.U32(tb, 0x08, (uint)(nb + K));
            for (int i = 0; i < nb; i++) { uint off = IsoBytes.U32(tb, 0x18 + i * 0x70 + 0x20); if (off != 0) IsoBytes.U32(tb, 0x18 + i * 0x70 + 0x20, off + (uint)delta); }
            // grafted records (parents remapped by name) + their MDT chunks appended past the block
            byte[] mblock = bp.AsSpan(mesh0).ToArray();
            int pStart = 0x18 + (nb + K) * 0x70 - 8 + mblock.Length;
            var recs = new List<byte>(); var chunks = new List<byte>();
            for (int k = 0; k < K; k++)
            {
                string n = names[k]; int si = snames.IndexOf(n);
                var rec = spl.AsSpan(0x18 + si * 0x70, 0x68).ToArray();
                int spar = BitConverter.ToInt32(rec, 0x24);
                string pname = spar >= 0 ? snames[spar] : null;
                int npar;
                if (pname != null && bnames.Contains(pname)) npar = bnames.IndexOf(pname);
                else if (pname != null && newIndex.ContainsKey(pname)) npar = newIndex[pname];
                else throw new IOException($"mesh_graft: node {n} parent '{pname}' resolves to neither base nor grafted set");
                if (npar >= nb + k) throw new IOException($"mesh_graft: node {n} parent '{pname}' would come AFTER it — reorder the nodes");
                Array.Copy(BitConverter.GetBytes(npar), 0, rec, 0x24, 4);
                uint smo = IsoBytes.U32(rec, 0x20);
                if (smo != 0)
                {
                    if (!(spl[smo] == 'M' && spl[smo + 1] == 'D' && spl[smo + 2] == 'T' && spl[smo + 3] == 0)) throw new IOException($"mesh_graft: node {n} mesh @0x{smo:X} is not an MDT chunk");
                    int csz = (int)IsoBytes.U32(spl, (int)smo + 8);
                    IsoBytes.U32(rec, 0x20, (uint)(pStart + chunks.Count));
                    chunks.AddRange(spl.AsSpan((int)smo, csz).ToArray());
                    while (chunks.Count % 16 != 0) chunks.Add(0);
                }
                recs.AddRange(rec); recs.AddRange(BitConverter.GetBytes((uint)(nb + k + 1))); recs.AddRange(BitConverter.GetBytes(0x70u));
            }
            recs.RemoveRange(recs.Count - 8, 8);                       // the new last record's tail words = the first mesh bytes (overlap)
            basePack.Require(cfg.BodyMds).ReplacePayload(tb.Concat(recs).Concat(mblock).Concat(chunks).ToArray());
            // .bbp: append the source's 64-byte bind rows for the grafted nodes
            string bbp = CfgBbp(cfg.Cfg.Payload), sbbp = CfgBbp(scfg.Cfg.Payload);
            byte[] bb = basePack.Require(bbp).Payload, sb = sp.Require(sbbp).Payload;
            if (bb.Length != nb * 64 || sb.Length != snames.Count * 64) throw new IOException("mesh_graft: .bbp is not count*64 bytes — layout assumption broken");
            var add = new List<byte>(); foreach (string n in names) add.AddRange(sb.AsSpan(snames.IndexOf(n) * 64, 64).ToArray());
            basePack.Require(bbp).ReplacePayload(bb.Concat(add).ToArray());
            // body .mot: one new track per source blade channel, folded anchors outside the windows
            var sm = MotFile.FromRecord(sp.Require(scfg.BodyMot)); var dm = MotFile.FromRecord(basePack.Require(cfg.BodyMot));
            var spans = FoldSpans(windows, 1, maxFrame);
            var spin = mg.Spin;
            if (spin != null && !newIndex.ContainsKey(spin.Node)) throw new IOException($"mesh_graft.spin: node '{spin.Node}' is not in the grafted set");
            var byNode = new Dictionary<string, List<MotTrack>>();
            foreach (var st in sm.Tracks)
            {
                string nn = st.W0 < snames.Count ? snames[(int)st.W0] : null;
                if (nn != null && newIndex.ContainsKey(nn)) { if (!byNode.ContainsKey(nn)) byNode[nn] = new List<MotTrack>(); byNode[nn].Add(st); }
            }
            foreach (string n in names)
            {
                var sts = byNode.TryGetValue(n, out var l) ? l : new List<MotTrack>();
                foreach (var st in sts)
                {
                    var v = SampleTrack(st, mg.PoseFrame);
                    if (v == null) continue;
                    var kfs = new List<MotKeyframe>(); foreach (int f in SpanEdges(spans)) kfs.Add(NewKey((uint)f, v));
                    dm.Tracks.Add(new MotTrack { W0 = (uint)newIndex[n], W1 = st.W1, W2 = st.W2, W3 = st.W3, W6 = st.W6, W7 = st.W7, Keyframes = kfs });
                }
                if (spin != null && n == spin.Node && !sts.Any(st => st.W2 == 0))
                {   // the spin hub has no source rotation track: an anchor-only chan-0 track at the bind orientation, for ApplySpin
                    var qb = MatToQuat(MdsBind(spl, snames.IndexOf(n)).R);
                    var kfs = new List<MotKeyframe>(); foreach (int f in SpanEdges(spans)) kfs.Add(NewKey((uint)f, qb));
                    dm.Tracks.Add(new MotTrack { W0 = (uint)newIndex[n], W1 = 0, W2 = 0, W3 = 0x20, W6 = MotTrack.TagW6, W7 = MotTrack.TagW7, Keyframes = kfs });
                }
            }
            ReplaceMot(basePack, cfg.BodyMot, dm);
        }

        /// <summary>SYNTHETIC SPIN on the mesh graft's hub: rampup = an ease-in cubic θ(t)=at³+bt² (θ'(0)=0, θ'(n)=rate, whole turns),
        /// loop = full rate with a key every frame (rate×len ≡ 0 mod 360), rampdown = a 4-DOF cubic from the hand-off phase to
        /// rest on a whole turn. Keyframes are q_axis(θ) ⊗ q_bind, so θ ≡ 0 equals the bind orientation the fold anchors hold.
        /// Runs AFTER the motion grafts (reversed windows would mirror pre-baked spin).</summary>
        private static void ApplySpin(ChrPack basePack, MeshGraft mg)
        {
            var spin = mg.Spin; var cfg = CfgMotionsOf(basePack);
            byte[] pl = basePack.Require(cfg.BodyMds).Payload;
            var frames = MotSplice.ReadMdsFrames(pl);
            int w0 = frames.IndexOf(spin.Node);
            var dm = MotFile.FromRecord(basePack.Require(cfg.BodyMot));
            var t = dm.TrackBy((uint)w0, 0) ?? throw new IOException($"spin: hub {spin.Node} chan-0 track missing (the mesh graft creates it)");
            var (loL, hiL) = spin.Loop;
            double rate = spin.Rate;
            if (rate <= 0 || rate >= 90.0) throw new IOException($"spin: rate {rate} deg/frame outside (0,90) — quaternion steps would flip");
            double loopDeg = rate * (hiL - loL) % 360.0;
            if (Math.Abs(loopDeg) > 1e-6 && Math.Abs(loopDeg - 360.0) > 1e-6) throw new IOException("spin: rate*looplen is not a whole number of turns");
            double h = spin.HandoffAdvance;
            if (h != 0 && !(0.0 < h && h < 90.0)) throw new IOException($"spin: handoff_advance {h} outside (0,90)");
            var keys = new SortedDictionary<int, double>();
            {
                var (lo, hi) = spin.RampUp; int n = hi - lo;
                double T = 360.0 * Math.Max(1, Math.Round(rate * n * 2.0 / 3.0 / 360.0, MidpointRounding.ToEven));
                double a = (rate * n - 2 * T) / ((double)n * n * n), b = (3 * T - rate * n) / ((double)n * n);
                for (int fr = lo; fr <= hi; fr++) { int x = fr - lo; keys[fr] = a * ((double)x * x * x) + b * ((double)x * x); }
            }
            for (int fr = loL; fr <= hiL; fr++) keys[fr] = h + (fr - loL) * rate;   // phase-shifted +h: one engine step past the rampup's final ≡0
            {
                var (lo, hi) = spin.RampDown; int n = hi - lo;
                Func<double, double> sol = null; var tried = new List<string>();
                foreach (double T in Enumerable.Range(1, 8).Select(k => k * 360.0).OrderBy(T => Math.Abs(T - (h + rate * n * 2.0 / 3.0))))
                {
                    double D = T - h - rate * n;
                    double c3 = (-rate * n - 2 * D) / ((double)n * n * n);
                    double c2 = (D - c3 * ((double)n * n * n)) / ((double)n * n);
                    double Th(double x) => c3 * x * x * x + c2 * x * x + rate * x + h;
                    double Dth(double x) => 3 * c3 * x * x + 2 * c2 * x + rate;
                    var crit = new List<double>(); if (c3 != 0) { double x = -c2 / (3 * c3); if (0 < x && x < n) crit.Add(x); }
                    bool mono = crit.Concat(new[] { 0.0, (double)n }).All(x => Dth(x) > -1e-9);
                    double maxStep = Enumerable.Range(0, n).Max(k => Th(k + 1) - Th(k));
                    tried.Add($"({T}, {mono}, {Math.Round(maxStep, 2)})");
                    if (mono && maxStep < 90.0) { sol = Th; break; }
                }
                if (sol == null) throw new IOException("spin: no whole-turn rampdown cubic satisfies the constraints — tried " + string.Join(" ", tried));
                for (int fr = lo; fr <= hi; fr++) keys[fr] = sol(fr - lo);
            }
            if (t.Keyframes.Any(k => keys.ContainsKey((int)k.Frame))) throw new IOException("spin: a spin window collides with existing hub keys (fold anchors?)");
            var ordered = keys.ToList();
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i].Key == ordered[i - 1].Key + 1 && Math.Abs(ordered[i].Value - ordered[i - 1].Value) >= 90.0)
                    throw new IOException($"spin: step {ordered[i - 1].Value:F1}->{ordered[i].Value:F1} deg at frame {ordered[i].Key} is >= 90");
            double[] ax = spin.Axis;
            var qb = MatToQuat(MdsBind(pl, w0).R);
            foreach (var (fr, deg) in ordered)
            {
                double th = deg * (Math.PI / 180.0), s = Math.Sin(th / 2);
                var q = QuatMul(new[] { Math.Cos(th / 2), ax[0] * s, ax[1] * s, ax[2] * s }, qb);
                t.Keyframes.Add(NewKey((uint)fr, q));
            }
            SortKeys(t);
            double[] prev = null;                                          // hemisphere continuity for the engine's nlerp
            foreach (var k in t.Keyframes)
            {
                var v = Values(k);
                if (prev != null && Dot(prev, v) < 0) { PackValue(k, v.Select(x => -x).ToArray()); v = Values(k); }
                prev = v;
            }
            ReplaceMot(basePack, cfg.BodyMot, dm);
        }

        // ───────────────────────────────────────────── root motion + graft ─────────────────────────────────────────────
        /// <summary>Same-character rigs sometimes differ only in the ROOT node's name: alias src[0] to dst[0]'s name when neither
        /// exists in the other rig (gated under a character's root_motion flag).</summary>
        private static List<string> AliasRoot(List<string> s, List<string> d)
        {
            if (s.Count > 0 && d.Count > 0 && s[0] != d[0] && !d.Contains(s[0]) && !s.Contains(d[0]))
                return new[] { d[0] }.Concat(s.Skip(1)).ToList();
            return s;
        }

        /// <summary>Root-motion recovery: a source track with keys in the graft window whose remapped (w0, w2) has no destination
        /// track would be dropped by the splice. Create it first, with rest anchors at both edges of every non-grafted span
        /// valued at the dest node's BIND (translation row for chan 2, bind-rotation quaternion for chan 0) — what the engine
        /// held while no track existed — inserted keeping (w0, w2) ascending.</summary>
        private static void CreateMissingTracks(MotFile dst, MotFile src, List<string> sframes, List<string> dframes, int slo, int shi, byte[] dpl, List<(int lo, int hi)> spans)
        {
            int[] remap = MotSplice.BuildJointRemap(sframes, dframes);
            var have = new HashSet<(uint, uint)>(dst.Tracks.Select(t => t.Key));
            foreach (var st in src.Tracks)
            {
                if (!st.FramesIn((uint)slo, (uint)shi).Any()) continue;
                int dw = st.W0 < remap.Length ? remap[st.W0] : -1;
                if (dw < 0 || have.Contains(((uint)dw, st.W2))) continue;
                var (_, B, tr) = MdsBind(dpl, dw);
                double[] v = st.W2 == 0 ? MatToQuat(B) : new[] { tr[0], tr[1], tr[2], 0.0 };
                var kfs = new List<MotKeyframe>(); foreach (int f in SpanEdges(spans)) kfs.Add(NewKey((uint)f, v));
                var nt = new MotTrack { W0 = (uint)dw, W1 = st.W1, W2 = st.W2, W3 = st.W3, W6 = st.W6, W7 = st.W7, Keyframes = kfs };
                int at = dst.Tracks.FindIndex(t => t.W0 > (uint)dw || (t.W0 == (uint)dw && t.W2 > st.W2));
                dst.Tracks.Insert(at < 0 ? dst.Tracks.Count : at, nt);
                have.Add(((uint)dw, st.W2));
            }
        }

        private static void Graft(ChrPack dstPack, string dstMot, string dstMds, ChrPack srcPack, string srcMot, string srcMds,
                                  int slo, int shi, int dlo, int dhi, bool rootMotion, List<(int lo, int hi)> spans)
        {
            var dst = MotFile.FromRecord(dstPack.Require(dstMot)); var src = MotFile.FromRecord(srcPack.Require(srcMot));
            byte[] dpl = dstPack.Require(dstMds).Payload;
            var dframes = MotSplice.ReadMdsFrames(dpl); var sframes = MotSplice.ReadMdsFrames(srcPack.Require(srcMds).Payload);
            if (rootMotion) { sframes = AliasRoot(sframes, dframes); CreateMissingTracks(dst, src, sframes, dframes, slo, shi, dpl, spans); }
            MotSplice.SpliceByJoint(dst, src, sframes, dframes, (uint)slo, (uint)shi, (uint)dlo, (uint)dhi);
            ReplaceMot(dstPack, dstMot, dst);
        }

        /// <summary>Every grafted track gets explicit keyframes AT the window edges, valued by sampling the SOURCE at its edges, so
        /// sparse tracks do not interpolate across into the neighbouring clips.</summary>
        private static void SealGraft(ChrPack dstPack, string dstMot, string dstMds, ChrPack srcPack, string srcMot, string srcMds,
                                      int slo, int shi, int dlo, int dhi, bool rootMotion)
        {
            var dframes = MotSplice.ReadMdsFrames(dstPack.Require(dstMds).Payload); var sframes = MotSplice.ReadMdsFrames(srcPack.Require(srcMds).Payload);
            if (rootMotion) sframes = AliasRoot(sframes, dframes);
            var remap = new Dictionary<int, int>(); for (int i = 0; i < sframes.Count; i++) { int j = dframes.IndexOf(sframes[i]); if (j >= 0) remap[i] = j; }
            var dm = MotFile.FromRecord(dstPack.Require(dstMot)); var sm = MotFile.FromRecord(srcPack.Require(srcMot));
            var dtracks = new Dictionary<(uint, uint), MotTrack>(); foreach (var t in dm.Tracks) dtracks[t.Key] = t;
            bool changed = false;
            foreach (var st in sm.Tracks)
            {
                if (!remap.TryGetValue((int)st.W0, out int dw)) continue;
                if (!dtracks.TryGetValue(((uint)dw, st.W2), out var dt) || dt.Keyframes.Count == 0) continue;
                foreach (var (sf, df) in new[] { (slo, dlo), (shi, dhi) })
                {
                    if (dt.Keyframes.Any(k => k.Frame == (uint)df)) continue;
                    var v = SampleTrack(st, sf);
                    if (v == null) continue;
                    var kf = dt.Keyframes[0].Copy(); kf.Frame = (uint)df; PackValue(kf, v);
                    dt.Keyframes.Add(kf); SortKeys(dt); changed = true;
                }
            }
            if (changed) ReplaceMot(dstPack, dstMot, dm);
        }

        // ───────────────────────────────────────────── window post-ops ─────────────────────────────────────────────
        /// <summary>A STATIC held pose: every track sampled at <paramref name="srcFrame"/> and written as sealed keys at dlo and dhi.</summary>
        private static void BakeHold(ChrPack pack, string motName, int srcFrame, int dlo, int dhi)
        {
            var m = MotFile.FromRecord(pack.Require(motName));
            foreach (var t in m.Tracks)
            {
                var v = SampleTrack(t, srcFrame);
                if (v == null || t.Keyframes.Count == 0) continue;
                t.Keyframes = t.Keyframes.Where(k => !(dlo <= k.Frame && k.Frame <= dhi)).ToList();
                if (t.Keyframes.Count == 0) throw new IOException("hold: a track had keys only inside the held window");
                foreach (int f in new[] { dlo, dhi }) { var kf = t.Keyframes[0].Copy(); kf.Frame = (uint)f; PackValue(kf, v); t.Keyframes.Add(kf); }
                SortKeys(t);
            }
            ReplaceMot(pack, motName, m);
        }

        /// <summary>Mirror the keyframes inside [dlo, dhi]: a clip that plays BACKWARDS (the engine cannot).</summary>
        private static void ReverseWindow(ChrPack pack, string motName, int dlo, int dhi)
        {
            var m = MotFile.FromRecord(pack.Require(motName));
            foreach (var t in m.Tracks)
            {
                var win = t.Keyframes.Where(k => dlo <= k.Frame && k.Frame <= dhi).ToList();
                if (win.Count == 0) continue;
                foreach (var kf in win) kf.Frame = (uint)(dlo + dhi - (int)kf.Frame);
                SortKeys(t);
            }
            ReplaceMot(pack, motName, m);
        }

        /// <summary>Add (dx, dy, dz) to the root node's translation keys in [dlo, dhi] (root = the first chan-2 track).</summary>
        private static void ApplyRootOffset(ChrPack pack, string motName, int dlo, int dhi, double[] off)
        {
            var m = MotFile.FromRecord(pack.Require(motName));
            var rt = m.Tracks.FirstOrDefault(t => t.W2 == 2);
            if (rt == null) return;
            foreach (var kf in rt.Keyframes)
                if (dlo <= kf.Frame && kf.Frame <= dhi) { var v = Values(kf); v[0] += off[0]; v[1] += off[1]; v[2] += off[2]; PackValue(kf, v); }
            ReplaceMot(pack, motName, m);
        }

        /// <summary>Freeze translation inside [dlo, dhi]: every chan-2 track with keys in the window gets them all set to its own
        /// value at dest frame <paramref name="refFrame"/> (kills baked root travel).</summary>
        private static void FreezeRoots(ChrPack pack, string motName, int refFrame, int dlo, int dhi)
        {
            var m = MotFile.FromRecord(pack.Require(motName));
            foreach (var t in m.Tracks)
            {
                if (t.W2 != 2) continue;
                var win = t.Keyframes.Where(k => dlo <= k.Frame && k.Frame <= dhi).ToList();
                if (win.Count == 0) continue;
                var v = SampleTrack(t, refFrame);
                if (v == null) continue;
                foreach (var kf in win) PackValue(kf, v);
            }
            ReplaceMot(pack, motName, m);
        }

        /// <summary>Pin named nodes to their <paramref name="refFrame"/> pose across [dlo, dhi]: window keys dropped, the edges sealed
        /// with the sampled value (a held prop the clip's source rig animated under another name).</summary>
        private static void PinNodes(ChrPack pack, string motName, string mdsName, string[] nodes, int refFrame, int dlo, int dhi)
        {
            var m = MotFile.FromRecord(pack.Require(motName));
            var frames = MotSplice.ReadMdsFrames(pack.Require(mdsName).Payload);
            foreach (var t in m.Tracks)
            {
                if (t.W0 >= frames.Count || !nodes.Contains(frames[(int)t.W0])) continue;
                var v = SampleTrack(t, refFrame);
                if (v == null) continue;
                t.Keyframes = t.Keyframes.Where(k => !(dlo <= k.Frame && k.Frame <= dhi)).ToList();
                if (t.Keyframes.Count == 0) throw new IOException("pin: a track had keys only inside the pinned window");
                foreach (int f in new[] { dlo, dhi }) { var kf = t.Keyframes[0].Copy(); kf.Frame = (uint)f; PackValue(kf, v); t.Keyframes.Add(kf); }
                SortKeys(t);
            }
            ReplaceMot(pack, motName, m);
        }

        /// <summary>Give a shadow-less base a shadow: the donor's shadow set (mds/bbp/wgt/mot) copied into the pack and declared
        /// in the cfg (SHADOW_VERTEX_ANIME + SHADOW_MODEL after the MODEL line + a real SHADOW_MOTION).</summary>
        private static void InjectShadow(ChrPack basePack, ChrPack donor, ShadowInject inj)
        {
            foreach (string nm in new[] { inj.Model, inj.Bbp, inj.Wgt, inj.Mot })
            {
                var r = donor.Find(nm) ?? throw new IOException($"shadow_inject: donor lacks {nm}");
                if (basePack.Find(r.Name) != null) throw new IOException($"shadow_inject: base already has {r.Name}");
                basePack.Records.Add(new ChrRecord { Name = r.Name, DataOff = r.DataOff, Size = r.Size, Stride = r.Stride, Raw = (byte[])r.Raw.Clone() });
            }
            var cfg = CfgMotionsOf(basePack).Cfg;
            string pl = L1(cfg.Payload);
            int va = pl.IndexOf("\r\n", pl.IndexOf("VERTEX_ANIME", StringComparison.Ordinal), StringComparison.Ordinal) + 2;
            pl = pl.Substring(0, va) + "SHADOW_VERTEX_ANIME 1\r\n" + pl.Substring(va);
            int eol = pl.IndexOf("\r\n", pl.IndexOf("MODEL ", StringComparison.Ordinal), StringComparison.Ordinal) + 2;
            pl = pl.Substring(0, eol) + $"SHADOW_MODEL \"{inj.Model}\"\r\n" + pl.Substring(eol);
            pl = new Regex("SHADOW_MOTION[^\r\n]*").Replace(pl, $"SHADOW_MOTION \"{inj.Mot}\", \"{inj.Bbp}\", \"{inj.Wgt}\"", 1);
            cfg.ReplacePayload(L1(pl));
        }

        /// <summary>The KEY_START..MOTION_END block's KEY lines replaced with one KEY per slot, in slot order.</summary>
        private static byte[] RewriteKeys(byte[] cfg, Slot[] slots)
        {
            var sb = new StringBuilder();
            foreach (var s in slots.OrderBy(s => s.Idx))
                sb.Append($"KEY\t{s.Frames.lo},\t{s.Frames.hi},\t{s.Speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, //{(s.Name.Length > 12 ? s.Name.Substring(0, 12) : s.Name)}\r\n");
            string pl = L1(cfg);
            int ks = pl.IndexOf("KEY_START", StringComparison.Ordinal);
            int nl = pl.IndexOf("\r\n", ks, StringComparison.Ordinal) + 2;
            int me = pl.IndexOf("MOTION_END", nl, StringComparison.Ordinal);
            return L1(pl.Substring(0, nl) + sb + pl.Substring(me));
        }

        // ───────────────────────────────────────────── assembly ─────────────────────────────────────────────
        private static (byte[] chr, int grafts, int keys) Assemble(byte[] baseBytes, Func<string, byte[]> readSrc, Character ch)
        {
            var basePack = ChrPack.Parse(baseBytes);
            if (ch.ShadowInject != null) InjectShadow(basePack, ChrPack.Parse(readSrc(ch.ShadowInject.Src)), ch.ShadowInject);
            var cfg = CfgMotionsOf(basePack);
            var cache = new Dictionary<string, ChrPack>();
            ChrPack Src(string name) { if (!cache.TryGetValue(name, out var p)) cache[name] = p = ChrPack.Parse(readSrc(name)); return p; }
            int grafts = 0;
            var mg = ch.MeshGraft;
            if (mg != null)
            {
                var windows = ch.Slots.Where(s => s.Src == mg.Src).Select(s => s.Frames).ToList();
                GraftMeshNodes(basePack, Src(mg.Src), mg, windows, ch.Slots.Max(s => s.Frames.hi));
            }
            bool rm = ch.RootMotion;
            List<(int lo, int hi)> rmSpans = null;
            if (rm) rmSpans = FoldSpans(ch.Slots.Where(s => s.Src != null || s.Hold != null).Select(s => s.Frames), 1, ch.Slots.Max(s => s.Frames.hi));
            foreach (var s in ch.Slots)
            {
                var (dlo, dhi) = s.Frames;
                bool touched = false;
                if (s.Src != null)
                {
                    var sp = Src(s.Src); var scfg = CfgMotionsOf(sp, s.SrcCfg);
                    if (mg != null && s.Src != mg.Src)
                    {
                        var clash = mg.Nodes.Intersect(MotSplice.ReadMdsFrames(sp.Require(scfg.BodyMds).Payload)).ToList();
                        if (clash.Count > 0) throw new IOException($"slot {s.Name}: source rig carries grafted nodes {string.Join(", ", clash)} but its window is outside the fold-span model");
                    }
                    var (wlo, whi) = s.Win;
                    Graft(basePack, cfg.BodyMot, cfg.BodyMds, sp, scfg.BodyMot, scfg.BodyMds, wlo, whi, dlo, dhi, rm, rmSpans);
                    SealGraft(basePack, cfg.BodyMot, cfg.BodyMds, sp, scfg.BodyMot, scfg.BodyMds, wlo, whi, dlo, dhi, rm);
                    if (cfg.ShadowMot != null && scfg.ShadowMot != null)
                    {
                        Graft(basePack, cfg.ShadowMot, cfg.ShadowMds, sp, scfg.ShadowMot, scfg.ShadowMds, wlo, whi, dlo, dhi, rm, rmSpans);
                        SealGraft(basePack, cfg.ShadowMot, cfg.ShadowMds, sp, scfg.ShadowMot, scfg.ShadowMds, wlo, whi, dlo, dhi, rm);
                    }
                    touched = true;
                }
                if (s.Hold != null) { BakeHold(basePack, cfg.BodyMot, s.Hold.Value, dlo, dhi); if (cfg.ShadowMot != null) BakeHold(basePack, cfg.ShadowMot, s.Hold.Value, dlo, dhi); touched = true; }
                if (s.RootOffset != null) { ApplyRootOffset(basePack, cfg.BodyMot, dlo, dhi, s.RootOffset); if (cfg.ShadowMot != null) ApplyRootOffset(basePack, cfg.ShadowMot, dlo, dhi, s.RootOffset); touched = true; }
                if (s.Reverse) { ReverseWindow(basePack, cfg.BodyMot, dlo, dhi); if (cfg.ShadowMot != null) ReverseWindow(basePack, cfg.ShadowMot, dlo, dhi); touched = true; }
                if (s.RootFreeze != null) { FreezeRoots(basePack, cfg.BodyMot, s.RootFreeze.Value, dlo, dhi); if (cfg.ShadowMot != null) FreezeRoots(basePack, cfg.ShadowMot, s.RootFreeze.Value, dlo, dhi); touched = true; }
                if (s.PinNodes != null) { PinNodes(basePack, cfg.BodyMot, cfg.BodyMds, s.PinNodes.Value.nodes, s.PinNodes.Value.refFrame, dlo, dhi); touched = true; }
                if (touched) grafts++;
            }
            if (mg?.Spin != null) ApplySpin(basePack, mg);
            cfg.Cfg.ReplacePayload(RewriteKeys(cfg.Cfg.Payload, ch.Slots));
            byte[] chr = basePack.Rebuild();
            var chk = ChrPack.Parse(chr);
            int keys = Regex.Matches(L1(CfgMotionsOf(chk).Cfg.Payload), "KEY[ \t]+\\d+,").Count;
            return (chr, grafts, keys);
        }

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            foreach (var ch in Chars)
            {
                byte[] baseBytes = arc.Read(ch.Base);
                var (chr, grafts, keys) = Assemble(baseBytes, arc.Read, ch);
                log($"{ch.Name}: {ch.Base} assembled — {grafts} grafts, {keys} KEYs, {baseBytes.Length:N0}->{chr.Length:N0} B");
                arc.Redirect(ch.Base, chr);
            }
            log("town-model assembly done");
        }
    }
}
