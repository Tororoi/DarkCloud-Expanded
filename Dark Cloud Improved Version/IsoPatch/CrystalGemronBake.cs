using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Crystal Gemron on the disc (its species record: SpeciesRows / ElfSpeciesPatches): Holy Gemron (e115a) with each of
    /// its three gem spheres replaced by the breaking crystal ball of the e209 event (<c>gedit\s34\chara\e209ball_b.chr</c>: eight shell
    /// pieces, ten inner shards and the camera-facing glints, beams and rings around them), the ball's whole node tree grafted under each
    /// gem bone and scaled to the gem, turned so it breaks upright in the death pose. The death plays the ball's shatter from the frame
    /// the vanilla death reaches <see cref="ShatterFrom"/>; the body sheets take the look made on the preview page (the embedded
    /// crystalGemronLook.json, its eye's soft edge included). Under the orphan DATA.HED entries e148a renamed to <see cref="Stem"/>, its
    /// script Holy Gemron's, its name in the dungeon message bank. A byte-exact twin of game_data/viewers/model/crystal_gemron.py's
    /// baked build.</summary>
    internal static class CrystalGemronBake
    {
        internal const string Stem = "e168a";
        private const string Dir = @"dun\monstor\", Base = "e115a", Orphan = "e148a";
        private const string Name = "Crystal Gemron";
        private const string Look = "crystalGemronLook.json";
        private const string BallChr = @"gedit\s34\chara\e209ball_b.chr", Ball = "e209ball_b";
        private const double BallRadius = 1.0;                                  // the ball's shell in its own model: centred 1.0 above its root
        private static readonly string[] Gems = { "tama1__m", "tamas00__m", "tamas03__m" };   // the gem bones the balls hang from
        private static readonly HashSet<string> GemDrop = new() { "tama1__m", "tamas00__m", "tamas03__m", "tama__appz", "tamas01__appz", "tamas02__appz" };
        private const string BallScroll = "TEX_SCROLL_DATA \"ball_b01\",0,32,128,32,\"ball_b01\",0,0,0.5,0.5,0,1\t//玉";   // the ball's own cfg line

        // The death clip, rebuilt from DeathStart in both motion files: the vanilla death (105–125, key 11 at 0.35) copied there, the
        // Gemron then holding its last pose while the ball's shatter (its own frames 1–21, played at 0.1 in the event) runs from the copy
        // of ShatterFrom. Stretch (0.35 / 0.1) keeps the ball's own speed under the death's key speed; its effects play EffectSpeedup times
        // that and its pieces (everything under PiecesRoot) PieceSpeedup times.
        private const int DeathKey = 11, DeathLoopKey = 12;
        private const int ClipFrom = 105, ClipTo = 125;
        private const int ShatterFrom = 121;
        private const string DeathSpeed = "0.35";
        private const double BallSpeed = 0.1;
        private const int DeathStart = 200;
        private static readonly double Stretch = double.Parse(DeathSpeed, System.Globalization.CultureInfo.InvariantCulture) / BallSpeed;
        private const string PiecesRoot = "null9";
        /// <summary>A Crystal Gemron draws ~220 meshes with the effects and shards in, and every draw takes room in the frame's VIF1 packet
        /// (50,000 B, double-buffered, unguarded): two on screen and a hit's sparks overran it (VIF1 garbage, the game reset). So those
        /// subtrees are out of the draw except while they show — a subtree-visibility track (MotionProc type 0x33: value &lt; 1 → draw_on 2,
        /// the node and its subtree skipped; else 1), per Gemron like every track.</summary>
        private const string EffectsRoot = "null14", ShardsRoot = "null10";
        private const uint SubtreeVisible = 0x33;
        private const double PieceSpeedup = 2.0, EffectSpeedup = 1.5;
        private static int ShatterStart => DeathStart + ShatterFrom - ClipFrom;
        private static int ShatterEnd => ShatterStart + (int)Math.Round(20 * Stretch / EffectSpeedup);
        private static int DeathEnd => ShatterEnd;
        /// <summary>The engine plays a track only between two of its keys (MotionProc: before the first key, or on or past the last, it is
        /// skipped — the node keeps what the last Crystal Gemron drawn left in the species' shared frames — or, before the first, may read
        /// the track's header as a key), so every track runs from frame 0 to here, one past the last frame any key plays.</summary>
        private static int TimelineEnd => DeathEnd + 1;
        private const uint MaterialAlpha = 40;
        private const float Hidden = 0.001f;                                    // "none": a scale of 0 would zero the node's axes

        /// <summary>The vanilla death's wait cries at frame 214, which its 105–125 clip never reaches; the rebuilt clip does, so the
        /// frame is moved past it (the vanilla death stays silent there).</summary>
        private const float CryFrame = 214f, NeverFrame = 9999f;

        private static readonly double[] Forward = { 0.0, 0.0, 1.0 }, Up = { 0.0, 1.0, 0.0 };

        /// <summary>The eyes are painted on the head mesh; for a tint of their own (CrystalGemron gives this node's visual a private vtable
        /// whose draw adds EyeTint to the ambient, tools/stubs/eye_tint.s) the head's triangles on the painted eyes — UV centroid inside
        /// EyeUv — move into a node of their own under the head (EyeNode: same material, UVs and frame; no flags), and out of the head's
        /// mesh (its strips split around them).</summary>
        internal const string EyeNode = "eyes";
        private const string EyeHead = "obj2_2";
        private const double EyeU0 = 0.21, EyeU1 = 0.34, EyeV0 = 0.50, EyeV1 = 0.61;

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            foreach (string ext in new[] { ".chr", ".stb" })
            {
                string to = Dir + Stem + ext, from = Dir + Orphan + ext;
                if (arc.Has(to)) continue;
                if (!arc.Has(from)) throw new IOException($"neither {to} nor the orphan entry {from} is in the archive");
                arc.Rename(from, to);
            }
            byte[] chr = Pack(arc.Read(Dir + Base + ".chr"), arc.Read(BallChr));
            arc.Redirect(Dir + Stem + ".chr", chr);
            log($"{Stem}.chr: {chr.Length:N0} B");
            byte[] stb = Script(arc.Read(Dir + Base + ".stb"), out string why);
            arc.Redirect(Dir + Stem + ".stb", stb);
            log($"{Stem}.stb: {why}");
            int id = DungeonMessageBank.NameBase + EnemySpecies.CrystalGemron.Id;
            const string bank = @"dun\message\ww_mes\dunmsd00_1.mes";
            byte[] named = WithName(arc.Read(bank), id, WeaponDescriptions.Encode(Name).Concat(new ushort[] { 0xFF01 }).ToArray());
            if (named == null) log($"name: message {id} already reads '{Name}'");
            else { arc.Redirect(bank, named); log($"name: message {id} = '{Name}'"); }
        }

        /// <summary>The name bank with message <paramref name="id"/> reading <paramref name="words"/>, or null when it already does. The
        /// bank has no entry for a new species' id (its index runs to 3320, the Bomb Gemron's, then 3999), so one is added
        /// (MesTextBaker.AppendMes: the index stays id-sorted, every message keeps its text) with the text just past the bank's own, and
        /// the file is padded back to its size: what the engine loads after it does not move.</summary>
        private static byte[] WithName(byte[] mes, int id, ushort[] words)
        {
            int cnt = IsoBytes.U16(mes, 0);
            for (int i = 0; i < cnt; i++) if (IsoBytes.U16(mes, 4 + i * 4) == id) return ModSpeciesBakes.SetText(mes, id, words);
            byte[] grown = MesTextBaker.AppendMes(mes, true, (id, words));
            if (grown.Length > mes.Length) throw new IOException($"message {id} does not fit the name bank's padding");
            var o = new byte[mes.Length];
            grown.CopyTo(o, 0);
            return o;
        }

        // ───────────────────────────── the script ─────────────────────────────
        /// <summary>Holy Gemron's script with its death wait's cry frame (the float pushed before the death function's first CALL) moved
        /// out of reach.</summary>
        private static byte[] Script(byte[] stb, out string why)
        {
            uint U(int o) => IsoBytes.U32(stb, o);
            int cb = (int)U(8), tbl = (int)U(0xC), cnt = (int)U(0x10), label120 = -1;
            for (int i = 0; i < cnt; i++) if (U(tbl + i * 8) == 120) label120 = (int)U(tbl + i * 8 + 4);
            if (label120 < 0) throw new IOException($"{Base}.stb: no label 120");
            int deathCall = cb + (int)U(label120);
            if (U(deathCall) != 19) throw new IOException($"{Base}.stb: label 120 does not start with a CALL");
            int code = cb + (int)U(cb + (int)U(deathCall + 8));
            int call = code; while (U(call) != 19) { if (U(call) == 15) throw new IOException($"{Base}.stb: the death function has no CALL"); call += 12; }
            int push = call - 12;
            if (U(push) != 3 || U(push + 4) != 2) throw new IOException($"{Base}.stb: the death wait's frame is not a pushed float");
            float frame = BitConverter.ToSingle(stb, push + 8);
            byte[] o = (byte[])stb.Clone();
            if (frame == NeverFrame) { why = "already patched"; return o; }
            if (frame != CryFrame) throw new IOException($"{Base}.stb: the death wait cries at {frame}, not {CryFrame}");
            IsoBytes.WrF(o, push + 8, NeverFrame);
            why = $"Holy Gemron's, the death cry's frame {CryFrame} → {NeverFrame} (never reached, as in the vanilla clip)";
            return o;
        }

        // ───────────────────────────── the model ─────────────────────────────
        private static byte[] Pack(byte[] srcChr, byte[] ballChr)
        {
            var pack = ChrPack.Parse(srcChr);
            var ball = ChrPack.Parse(ballChr);
            byte[] mds = pack.Require(Base + ".mds").Payload;
            var nodes = ModelCodec.ReadSkeleton(mds);
            var by = nodes.ToDictionary(n => n.Name);
            byte[] bmds = ball.Require(Ball + ".mds").Payload;
            var bnodes = ModelCodec.ReadSkeleton(bmds);
            int btbl = (int)IsoBytes.U32(bmds, 12);
            var braw = Enumerable.Range(0, bnodes.Count).Select(i => bmds.AsSpan(btbl + i * 0x70, 0x70).ToArray()).ToList();
            var bmot = MotFile.FromRecord(ball.Require(Ball + ".mot"));
            var mot = MotFile.FromPack(pack, Base + ".mot");
            var wbreak = PoseWorld(nodes, mot, ShatterFrom);                       // the pose the balls break in
            MotTrack refTrack = mot.Tracks[^1];
            var pieces = new HashSet<int>();
            foreach (var n in bnodes) if (n.Name == PiecesRoot || pieces.Contains(n.Parent)) pieces.Add(n.I);
            var grafts = new List<(List<byte[]> recs, Dictionary<int, byte[]> meshes)>();
            var added = new List<MotTrack>();
            int at = nodes.Count;
            foreach (string gem in Gems)
            {
                RigNode g = by[gem];
                var gm = MdtMesh.Parse(mds, g.MeshOff);
                double s = gm.Pos.SelectMany(p => p.Take(3)).Max(c => Math.Abs(c)) / BallRadius;   // the gem's radius
                double[][] R = Upright(wbreak[g.I]);
                double[] T = R[1].Select(c => -s * BallRadius * c).ToArray();          // the ball's centre on the gem's
                var recs = new List<byte[]>(); var meshes = new Dictionary<int, byte[]>();
                for (int j = 0; j < bnodes.Count; j++)
                {
                    RigNode n = bnodes[j];
                    recs.Add(n.Parent < 0 ? Record(braw[j], at + j, g.I, R, T, 1.0) : Record(braw[j], at + j, at + n.Parent, null, null, s));
                    if (n.MeshOff != 0) meshes[j] = ScaledMesh(bmds, n.MeshOff, s);
                }
                grafts.Add((recs, meshes));
                foreach (var t in bmot.Tracks)
                {
                    double stretch = Stretch / (pieces.Contains((int)t.W0) ? PieceSpeedup : EffectSpeedup);
                    var tr = new MotTrack { W0 = (uint)at + t.W0, W1 = t.W1, W2 = t.W2, W3 = refTrack.W3, W6 = refTrack.W6, W7 = refTrack.W7 };
                    foreach (var k in t.Keyframes)
                    {
                        float[] v = k.Value;
                        double[] val = t.W2 == 2 ? new double[] { v[0] * s, v[1] * s, v[2] * s, v[3] } : v.Select(x => (double)x).ToArray();
                        tr.Keyframes.Add(Key((uint)(ShatterStart + Math.Round(((double)k.Frame - 1) * stretch)), val));
                    }
                    double[] first = Values(tr.Keyframes[0]), last = Values(tr.Keyframes[^1]);
                    // A material-alpha key writes the species' one material (every Crystal Gemron draws the same): the fades are keyed from
                    // the shatter on only, so only a dying Gemron writes it — the patched engine skips a track before its first key
                    // (ElfSpeciesPatches.PatchMotionBeforeFirstKey; vanilla read the track's header as a key there). Every other track is
                    // per Gemron: at rest from 0.
                    if (t.W2 != MaterialAlpha) tr.Keyframes.Insert(0, Key(0, first));
                    if (tr.Keyframes[^1].Frame < TimelineEnd) tr.Keyframes.Add(Key((uint)TimelineEnd, last));
                    added.Add(tr);
                }
                // the effects drawn only through the shatter, the shards only from it on
                int Node(string name) => bnodes.First(n => n.Name == name).I;
                foreach (var (root, keys) in new[]
                {
                    (EffectsRoot, new[] { (0, 0.0), (ShatterStart - 1, 0.0), (ShatterStart, 1.0), (ShatterEnd - 1, 1.0), (ShatterEnd, 0.0), (TimelineEnd, 0.0) }),
                    (ShardsRoot, new[] { (0, 0.0), (ShatterStart - 1, 0.0), (ShatterStart, 1.0), (TimelineEnd, 1.0) }),
                })
                {
                    var tr = new MotTrack { W0 = (uint)(at + Node(root)), W1 = 0, W2 = SubtreeVisible, W3 = refTrack.W3, W6 = refTrack.W6, W7 = refTrack.W7 };
                    foreach (var (f, v) in keys) tr.Keyframes.Add(Key((uint)f, new[] { v, 0, 0, 0 }));
                    added.Add(tr);
                }
                // the glints, beams and rings also scaled to nothing outside the shatter (their camera-facing parents)
                for (int j = 0; j < bnodes.Count; j++)
                {
                    if (!bnodes[j].Name.Contains("__ba")) continue;
                    var tr = new MotTrack { W0 = (uint)(at + j), W1 = 0, W2 = 1, W3 = refTrack.W3, W6 = refTrack.W6, W7 = refTrack.W7 };
                    tr.Keyframes.Add(Key(0, new double[] { Hidden, Hidden, Hidden, 0 }));
                    tr.Keyframes.Add(Key((uint)(ShatterStart - 1), new double[] { Hidden, Hidden, Hidden, 0 }));
                    tr.Keyframes.Add(Key((uint)ShatterStart, new double[] { 1, 1, 1, 0 }));
                    tr.Keyframes.Add(Key((uint)(ShatterEnd - 1), new double[] { 1, 1, 1, 0 }));
                    tr.Keyframes.Add(Key((uint)ShatterEnd, new double[] { Hidden, Hidden, Hidden, 0 }));
                    tr.Keyframes.Add(Key((uint)TimelineEnd, new double[] { Hidden, Hidden, Hidden, 0 }));
                    added.Add(tr);
                }
                at += bnodes.Count;
            }
            // the eyes: their triangles out of the head into a node of their own (SplitEyes), no tracks (it keeps its bind pose)
            var (headMdt, eyeMdt) = SplitEyes(mds, by[EyeHead]);
            grafts.Add((new List<byte[]> { EyeRecord(at, by[EyeHead].I) }, new Dictionary<int, byte[]> { [0] = eyeMdt })); at += 1;
            pack.Require(Base + ".mds").ReplacePayload(GraftMds(mds, grafts, new Dictionary<string, byte[]> { [EyeHead] = headMdt }));
            ChrRecord bbp = pack.Require(Base + ".bbp");
            bbp.ReplacePayload(bbp.Payload.Concat(new byte[64 * (at - nodes.Count)]).ToArray());   // bind poses (not skinned: zero)
            DeathClip(mot);
            mot.Tracks.AddRange(added);
            pack.Require(Base + ".mot").ReplacePayload(mot.BuildPayload());
            var smot = MotFile.FromPack(pack, "e115s.mot"); DeathClip(smot); pack.Require("e115s.mot").ReplacePayload(smot.BuildPayload());
            // the sheets in the look; the ball's pictures into the bank (an IM2 bank's are stored swizzled; e209ex comes from a row-major IMG bank)
            ChrRecord img = pack.Require(Base + "01.img");
            var bank = new ImgBank(img.Payload);
            var look = SheetLook.Parse(ElfCaveWriter.Embedded(Look, $"{Look} is not embedded — the Crystal Gemron's look"));
            var items = bank.Entries.Select(e => (e.name, e.name == Base + "01" ? look.ApplyToSheet(bank.Block(e.name), out _, out _)
                                                          : e.name == Base + "02" ? look.ApplyBaseToSheet(bank.Block(e.name)) : bank.Block(e.name))).ToList();
            var bbank = new ImgBank(ball.Require("ball.img").Payload); items.AddRange(bbank.Entries.Select(e => (e.name, bbank.Block(e.name))));
            var ex = new ImgBank(ball.Require("e209ex.img").Payload); items.AddRange(ex.Entries.Select(e => (e.name, Swizzled(ex.Block(e.name)))));
            img.ReplacePayload(ImgBank.Build(bank.Magic, items));
            Cfg(pack);
            return pack.Rebuild();
        }

        /// <summary>Keys: the death on the new clip, its loop on the clip's last frame; material animation on (the effects' fades); the ball's scroll as a second TEX_ANIME group (as c23a carries two).</summary>
        private static void Cfg(ChrPack pack)
        {
            ChrRecord rec = pack.Require("info.cfg");
            Encoding sjis = Encoding.GetEncoding(932);
            string[] cfg = sjis.GetString(rec.Payload).Split('\n');
            int k = -1; bool scrolled = false;
            for (int i = 0; i < cfg.Length; i++)
            {
                string line = cfg[i], s = line.TrimStart();
                if (s.StartsWith("KEY_START")) { k = 0; continue; }
                if (s.StartsWith("TEX_ANIME_END") && !scrolled) { cfg[i] = line + "\nTEX_ANIME 1,1\r\n" + BallScroll + "\r\nTEX_ANIME_END\r"; scrolled = true; }
                if (s.StartsWith("MATERIAL_ANIME")) cfg[i] = "MATERIAL_ANIME 1\r";             // the effects' fades
                if (k >= 0 && s.StartsWith("KEY") && !s.StartsWith("KEY_"))
                {
                    string comment = line.Contains("//") ? line.Substring(line.IndexOf("//", StringComparison.Ordinal)) : "\r";
                    if (k == DeathKey) cfg[i] = $"KEY\t{DeathStart},\t{DeathEnd},\t{DeathSpeed},\t{comment}";
                    if (k == DeathLoopKey) cfg[i] = $"KEY\t{DeathEnd},\t{DeathEnd},\t0.0,\t{comment}";
                    k++;
                }
            }
            if (!scrolled) throw new IOException("info.cfg: no TEX_ANIME_END to follow with the ball's scroll");
            rec.ReplacePayload(sjis.GetBytes(string.Join("\n", cfg)));
        }

        /// <summary>The vanilla death copied to DeathStart on, every track keyed on each frame of it, its last pose held to DeathEnd.</summary>
        private static void DeathClip(MotFile mot)
        {
            foreach (var tr in mot.Tracks)
            {
                if (tr.Keyframes[^1].Frame >= DeathStart) throw new IOException($"track {tr.W0}/{tr.W2} already reaches {tr.Keyframes[^1].Frame}");
                var made = new List<MotKeyframe>();
                for (int n = DeathStart; n <= DeathStart + ClipTo - ClipFrom; n++) made.Add(Key((uint)n, At(tr, n - DeathStart + ClipFrom)));
                made.Add(Key((uint)DeathEnd, At(tr, ClipTo)));
                made.Add(Key((uint)TimelineEnd, At(tr, ClipTo)));
                tr.Keyframes.AddRange(made);
            }
        }

        private static double[] Values(MotKeyframe k) => k.Value.Select(x => (double)x).ToArray();

        /// <summary>A keyframe as the Python builder makes it: zero, the frame, four floats.</summary>
        private static MotKeyframe Key(uint frame, double[] v)
        {
            var k = new MotKeyframe(new byte[MotKeyframe.Size]) { Frame = frame };
            for (int i = 0; i < 4; i++) IsoBytes.WrF(k.Raw, 0x10 + i * 4, (float)v[i]);
            return k;
        }

        /// <summary>A track's value at a frame: the first or last key outside its range, else the pair around it blended (rotation
        /// slerped, the rest lerped) — on a key too, t = 0 (crystal_gemron._at).</summary>
        private static double[] At(MotTrack tr, double frame)
        {
            var keys = tr.Keyframes;
            if (frame <= keys[0].Frame) return keys[0].Value.Select(x => (double)x).ToArray();
            if (frame >= keys[^1].Frame) return keys[^1].Value.Select(x => (double)x).ToArray();
            int b = keys.FindIndex(k => k.Frame > frame), a = b - 1;
            double t = (frame - keys[a].Frame) / ((double)keys[b].Frame - keys[a].Frame);
            float[] va = keys[a].Value, vb = keys[b].Value;
            return tr.W2 == 0 ? Slerp(va, vb, t) : Enumerable.Range(0, 4).Select(i => va[i] + (vb[i] - (double)va[i]) * t).ToArray();
        }

        private static double[] Slerp(float[] a, float[] bf, double t)
        {
            double[] b = { bf[0], bf[1], bf[2], bf[3] };
            double d = (double)a[0] * b[0] + (double)a[1] * b[1] + (double)a[2] * b[2] + (double)a[3] * b[3];
            if (d < 0) { b = new[] { -b[0], -b[1], -b[2], -b[3] }; d = -d; }
            double w0, w1;
            if (d > 0.9995) { w0 = 1 - t; w1 = t; }
            else { double th = Math.Acos(Math.Min(1.0, d)), sn = Math.Sin(th); w0 = Math.Sin((1 - t) * th) / sn; w1 = Math.Sin(t * th) / sn; }
            var q = new double[4];
            for (int i = 0; i < 4; i++) q[i] = w0 * a[i] + w1 * b[i];
            double n = Math.Sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
            if (n == 0) n = 1.0;
            return new[] { q[0] / n, q[1] / n, q[2] / n, q[3] / n };
        }

        /// <summary>Every node's world matrix (row vectors) at a frame of the motion.</summary>
        private static List<double[][]> PoseWorld(List<RigNode> nodes, MotFile mot, double frame)
        {
            var rot = new Dictionary<uint, MotTrack>(); var trans = new Dictionary<uint, MotTrack>();
            foreach (var t in mot.Tracks) { if (t.W2 == 0) rot[t.W0] = t; else if (t.W2 == 2) trans[t.W0] = t; }
            var W = new List<double[][]>();
            foreach (var n in nodes)
            {
                double[][] R = rot.TryGetValue((uint)n.I, out var rt) ? QuatMat(At(rt, frame)) : n.R;
                double[] T = trans.TryGetValue((uint)n.I, out var tt) ? At(tt, frame).Take(3).ToArray() : n.T;
                var L = RigMath.MatFromRT(R, T);
                W.Add(n.Parent < 0 ? L : RigMath.MatMul(L, W[n.Parent]));
            }
            return W;
        }

        private static double[][] QuatMat(double[] q)
        {
            double w = q[0], x = q[1], y = q[2], z = q[3];
            return new[]
            {
                new[] { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) },
                new[] { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) },
                new[] { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) },
            };
        }

        /// <summary>The ball root's rotation in its gem bone's frame W: the ball's +Y up the world, its +Z the Gemron's forward.</summary>
        private static double[][] Upright(double[][] W)
        {
            double[] u = Local(W, Up), f = Local(W, Forward);
            f = Unit(Enumerable.Range(0, 3).Select(i => f[i] - Dot(f, u) * u[i]).ToArray());
            return new[] { Cross(u, f), u, f };
        }

        private static double[] Local(double[][] W, double[] d) =>
            Unit(Enumerable.Range(0, 3).Select(r => ExactMath.Sum(W[r][0] * d[0], W[r][1] * d[1], W[r][2] * d[2])).ToArray());
        private static double[] Unit(double[] v)
        {
            double n = Math.Sqrt(ExactMath.Sum(v.Select(c => c * c)));
            if (n == 0) n = 1.0;
            return v.Select(c => c / n).ToArray();
        }
        private static double Dot(double[] a, double[] b) => ExactMath.Sum(a.Zip(b, (x, y) => x * y));
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        // ───────────────────────────── the eyes' node ─────────────────────────────
        /// <summary>(the head's MDT without the eye triangles, the eyes' MDT): selected by UV centroid; the head's strips split around them
        /// (it draws two-sided, so a piece starting on an odd triangle needs no reordering), the eyes one triangle list per material.</summary>
        private static (byte[] head, byte[] eyes) SplitEyes(byte[] mds, RigNode head)
        {
            var m = MdtMesh.Parse(mds, head.MeshOff);
            bool Eye(int[] a, int[] b, int[] c)
            {
                if (a[0] == b[0] || b[0] == c[0] || a[0] == c[0]) return false;
                double u = ExactMath.Sum(m.Norm[a[2]][0], m.Norm[b[2]][0], m.Norm[c[2]][0]) / 3, v = ExactMath.Sum(m.Norm[a[2]][1], m.Norm[b[2]][1], m.Norm[c[2]][1]) / 3;   // as Python's sum()
                return EyeU0 < u && u < EyeU1 && EyeV0 < v && v < EyeV1;
            }
            var keep = new List<(int, int, List<int[]>)>();
            var eyes = new List<(int mat, List<int[]> recs)>();
            void AddEye(int mi, params int[][] t) { int k = eyes.FindIndex(e => e.mat == mi); if (k < 0) { eyes.Add((mi, new List<int[]>())); k = eyes.Count - 1; } eyes[k].recs.AddRange(t); }
            foreach (var (prim, mi, recs) in m.Submeshes)
            {
                if (prim == 3)
                {
                    var kept = new List<int[]>();
                    for (int k = 0; k + 2 < recs.Count; k += 3)
                    {
                        if (Eye(recs[k], recs[k + 1], recs[k + 2])) AddEye(mi, recs[k], recs[k + 1], recs[k + 2]);
                        else kept.AddRange(new[] { recs[k], recs[k + 1], recs[k + 2] });
                    }
                    if (kept.Count > 0) keep.Add((3, mi, kept));
                }
                else
                {
                    int n = recs.Count - 2, run = -1;
                    for (int k = 0; k <= n; k++)
                    {
                        bool sel = k < n && Eye(recs[k], recs[k + 1], recs[k + 2]);
                        if (sel) { if ((k & 1) == 1) AddEye(mi, recs[k + 1], recs[k], recs[k + 2]); else AddEye(mi, recs[k], recs[k + 1], recs[k + 2]); }
                        if (k < n && !sel) { if (run < 0) run = k; continue; }
                        if (run >= 0) { keep.Add((4, mi, recs.GetRange(run, k + 2 - run))); run = -1; }
                    }
                }
            }
            var headM = MdtMesh.Parse(mds, head.MeshOff); headM.Submeshes = keep;
            m.Submeshes = eyes.Select(e => (3, e.mat, e.recs)).ToList();
            AlignDisplayList(headM); AlignDisplayList(m);
            return (headM.Build(), m.Build());
        }

        /// <summary>The padding after <paramref name="m"/>'s display list sized so the blocks after it start 16-aligned (the game reads
        /// them by quadword).</summary>
        private static void AlignDisplayList(MdtMesh m)
        {
            int dl = 0x10 + m.Submeshes.Sum(s => 0xC + s.recs.Count * (m.HasCol ? 16 : 12));
            m.Pads["DL"] = new byte[(16 - dl % 16) % 16];
        }

        private static byte[] EyeRecord(int index, int parent)
        {
            var rec = new byte[0x70];
            IsoBytes.U32(rec, 0, (uint)index); IsoBytes.U32(rec, 4, 0x70);
            Encoding.ASCII.GetBytes(EyeNode).CopyTo(rec, 0x08);
            IsoBytes.U32(rec, 0x2C, (uint)parent);
            float[] id = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            for (int i = 0; i < 16; i++) IsoBytes.WrF(rec, 0x30 + i * 4, id[i]);
            return rec;
        }

        // ───────────────────────────── the graft ─────────────────────────────
        /// <summary>A ball node's record under the Gemron: its index and parent; the root's rotation and position given, the rest's position
        /// scaled.</summary>
        private static byte[] Record(byte[] raw, int index, int parent, double[][] R, double[] T, double scale)
        {
            byte[] rec = (byte[])raw.Clone();
            IsoBytes.U32(rec, 0x00, (uint)index);
            IsoBytes.U32(rec, 0x2C, unchecked((uint)parent));
            if (R != null)
                for (int r = 0; r < 3; r++) { for (int c = 0; c < 3; c++) IsoBytes.WrF(rec, 0x30 + r * 16 + c * 4, (float)R[r][c]); IsoBytes.WrF(rec, 0x30 + r * 16 + 12, 0f); }
            double[] t = T ?? new double[] { IsoBytes.F32(rec, 0x60) * scale, IsoBytes.F32(rec, 0x64) * scale, IsoBytes.F32(rec, 0x68) * scale };
            for (int c = 0; c < 3; c++) IsoBytes.WrF(rec, 0x60 + c * 4, (float)t[c]);
            return rec;
        }

        private static byte[] ScaledMesh(byte[] mds, int off, double s)
        {
            var m = MdtMesh.Parse(mds, off);
            m.Pos = m.Pos.Select(p => new[] { p[0] * s, p[1] * s, p[2] * s, p[3] }).ToList();
            return m.Build();
        }

        /// <summary>The MDS with the gems' meshes (and their glow shells) removed, <paramref name="replace"/>'s meshes swapped in and the
        /// grafts' records appended in order, every mesh offset re-laid.</summary>
        private static byte[] GraftMds(byte[] mds, List<(List<byte[]> recs, Dictionary<int, byte[]> meshes)> grafts, Dictionary<string, byte[]> replace)
        {
            int count = (int)IsoBytes.U32(mds, 8), tbl = (int)IsoBytes.U32(mds, 12);
            var raws = new List<byte[]>(); var blobs = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                byte[] raw = mds.AsSpan(tbl + i * 0x70, 0x70).ToArray();
                string name = IsoBytes.NameAt(raw, 8, 0x20);
                int off = (int)IsoBytes.U32(raw, 0x28);
                raws.Add(raw);
                blobs.Add(GemDrop.Contains(name) || off == 0 ? null : replace.TryGetValue(name, out byte[] r) ? r : mds.AsSpan(off, (int)IsoBytes.U32(mds, off + 8)).ToArray());
            }
            foreach (var (recs, meshes) in grafts)
                for (int j = 0; j < recs.Count; j++) { raws.Add((byte[])recs[j].Clone()); blobs.Add(meshes.TryGetValue(j, out byte[] m) ? m : null); }
            int bse = 0x10 + raws.Count * 0x70;
            var nodesBlob = new List<byte>(); var body = new List<byte>();
            for (int i = 0; i < raws.Count; i++)
            {
                byte[] raw = raws[i], blob = blobs[i];
                IsoBytes.U32(raw, 0x28, blob != null && blob.Length > 0 ? (uint)(bse + body.Count) : 0u);
                if (blob != null && blob.Length > 0) { body.AddRange(blob); body.AddRange(new byte[ExactMath.Mod(-body.Count, 16)]); }
                nodesBlob.AddRange(raw);
            }
            var o = new List<byte>(mds.AsSpan(0, 8).ToArray());
            o.AddRange(BitConverter.GetBytes(raws.Count)); o.AddRange(BitConverter.GetBytes(0x10));
            o.AddRange(nodesBlob); o.AddRange(body);
            return o.ToArray();
        }

        /// <summary>A row-major 8-bit picture's pixels in the GS block order, for an IM2 bank.</summary>
        private static byte[] Swizzled(byte[] tim)
        {
            var (_, hdr, w, h) = Tim8.PictureInfo(tim);
            byte[] o = (byte[])tim.Clone();
            Tim8.Swizzle8(tim.AsSpan(Tim8.Pic + hdr, w * h).ToArray(), w, h).CopyTo(o, Tim8.Pic + hdr);
            return o;
        }
    }
}
