using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The mod's enemy species on the disc (their records: ElfSpeciesPatches). Each takes a vanilla species' model and
    /// script under a new file stem in an orphan DATA.HED entry (an index entry no vanilla data references, renamed: the engine
    /// finds a file by a linear name scan), and its name in the dungeon message bank.
    ///
    /// Bomb Gemron (<see cref="BombGemronStem"/>): Ice Gemron's model, motions and sheets (e112a; the five Gemrons share one rig and
    /// texture layout) with Holy Gemron's script (e115a). The sheets take the look made on the preview page (<see cref="SheetLook"/>,
    /// the embedded bombGemronLook.json: per-section sliders over the body sheet, merged to 256 colours), the three gem spheres' meshes replaced by the thrown-bomb
    /// item model (dun\item\main_data\bakudan) scaled to each sphere and turned so its wick points as designed, the bomb's sheet
    /// added to the pack's bank; the glow overlays dropped. Its script is Holy Gemron's with the death path exploding — a
    /// <c>_SET_SHOT2</c> blast (shot slot 1: the radius-50 fireball of ElfSpeciesPatches.PatchBlastConfig) at the body once the death
    /// motion reaches frame 122 — and a self-destruct: the AI
    /// loop's head calls a function that, with HP under a quarter and the player within 22 units, plays the death motion at
    /// 0.25× and blows up at the same frame (the outlaws Sam / Billy / Mr. Blare's own pattern); its throw is aimed at the player's
    /// feet + 14, where the shot's contact test looks. The name goes into the empty
    /// message 3000 + species id of dunmsd00_1.mes in place. Its self-destruct plays key 14, baked as motion 7 reversed
    /// (<see cref="BakeSelfDestructMotion"/>), and blows up on its final pose.</summary>
    internal static class BombGemronBake
    {
        internal const string BombGemronStem = "e167a";
        private const string Dir = @"dun\monstor\";
        private const string ModelSource = "e112a", ScriptSource = "e115a";   // Ice Gemron's model and sheets, Holy Gemron's script
        /// <summary>The unused archive entries renamed to the species' (docs/custom-enemies.md, Archive entries): e147a.chr, a duplicate of
        /// e109a's no record names, and _c13a.stb, a script nothing loads (e147a.stb is the Demon Shaft mimic's, record 141).</summary>
        private const string ChrDonor = "e147a.chr", StbDonor = "_c13a.stb";
        private const string Look = "bombGemronLook.json";                         // the look made on the preview page (SheetLook), an embedded resource
        private const string BombMds = @"dun\item\main_data\bakudan.mds", BombImg = @"dun\item\main_data\bakudan.img";
        private const string NameBank = @"dun\message\ww_mes\dunmsd00_1.mes";
        /// <summary>The Bomb Gemron's shots' drawing model (ElfSpeciesPatches.PatchBombConfigs): <c>dun\effect\g_wave2.chr</c>, whose own config
        /// those shots replace, rebuilt as Witch Illza's apple shot (<c>ringo_ex</c>) with the apple's mesh (<c>dokuring__m</c>) swapped for the
        /// item bomb's and the bomb's picture added to its bank — the bomb the Big Bang's pellets fly as; its cfg record renamed to the
        /// name the shot pack's loader asks for (<c>&lt;model&gt;.cfg</c>).</summary>
        private const string ShotSource = @"dun\effect\ringo_ex.chr", ShotPack = @"dun\effect\g_wave2.chr";
        private const string ShotMds = "ringo_ex.mds", ShotImg = "dokuring.img", ShotCfg = "ringo_ex.cfg", ShotCfgAs = "g_wave2.cfg", ShotNode = "dokuring__m";
        private const string Name = "Bomb Gemron";

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            arc.Claim(Dir + ChrDonor, Dir + BombGemronStem + ".chr");
            arc.Claim(Dir + StbDonor, Dir + BombGemronStem + ".stb");
            byte[] light = new ImgBank((ChrPack.Parse(arc.Read(GlowDisc.SourcePack)).Find("fire.img") ?? throw new IOException("glow source pack lacks fire.img")).Payload).Block(GlowDisc.SourcePicture);
            byte[] chr = BombGemronPack(arc.Read(Dir + ModelSource + ".chr"), arc.Read(BombMds), arc.Read(BombImg), ChrPack.Parse(arc.Read(DiscQuadPack)).Require(DiscQuadMds).Payload, light);
            arc.Redirect(Dir + BombGemronStem + ".chr", chr);
            log($"{BombGemronStem}.chr: {chr.Length:N0} B");
            byte[] stb = BombGemronScript(arc.Read(Dir + ScriptSource + ".stb"), out string why);
            arc.Redirect(Dir + BombGemronStem + ".stb", stb);
            log($"{BombGemronStem}.stb: {why}");
            byte[] shot = BombShotPack(arc.Read(ShotSource), arc.Read(BombMds), arc.Read(BombImg));
            arc.Redirect(ShotPack, shot);
            log($"{ShotPack}: the apple shot wearing the bomb, {shot.Length:N0} B");
            int id = DungeonMessageBank.NameBase + EnemySpecies.BombGemron.Id;
            byte[] mes = arc.Read(NameBank);
            byte[] named = MesTextBaker.SetMes(mes, id, WeaponDescriptions.Encode(Name).Concat(new ushort[] { 0xFF01 }).ToArray());
            if (named == null) log($"name: message {id} already reads '{Name}'");
            else { arc.Redirect(NameBank, named); log($"name: message {id} = '{Name}'"); }
        }

        // ───────────────────────────── the model ─────────────────────────────
        private const double BombRadius = 1.22;                                    // the bomb body's radius in its own model (the wick rises to 1.98)
        private static readonly double BodySpin = (90 - 200 - 45 - 50) * (Math.PI / 180.0);   // about the wick axis before it is aimed: positive = counter-clockwise from the wick's tip
        private static readonly double WingSpin = 180 * (Math.PI / 180.0);
        /// <summary>The big bomb turned after it is aimed (degrees, chosen on the preview page): pitch about the Gemron's left (+ tips the top
        /// forward), roll about its forward (+ to its right), yaw about its up (+ toward its left), applied in that order about the bomb's
        /// centre.</summary>
        private const double BodyPitch = -2.5, BodyRoll = -45.0, BodyYaw = -3.5;
        private static readonly double[] Forward = { 0.0, 0.0, 1.0 }, Up = { 0.0, 1.0, 0.0 };   // the rig at rest: head at +Z, Y up; its right is forward × up = −X
        private const string Mds = ModelSource + ".mds", Img = ModelSource + "01.img", Sheet = ModelSource + "01", Sheet2 = ModelSource + "02";
        private const string BodyNode = "tama1__m";
        private static readonly string[] WingNodes = { "tamas00__m", "tamas03__m" }, GlowNodes = { "tama__appz", "tamas01__appz", "tamas02__appz" };

        private static byte[] BombGemronPack(byte[] srcChr, byte[] bombMds, byte[] bombImg, byte[] quadMds, byte[] light)
        {
            var pack = ChrPack.Parse(srcChr);
            byte[] mds = pack.Require(Mds).Payload;
            var nodes = ModelCodec.ReadSkeleton(mds);
            var by = nodes.ToDictionary(n => n.Name);
            RigNode body = by[BodyNode];
            double[] right = RigMath.Cross(Forward, Up);
            var replaced = new Dictionary<string, byte[]>();
            foreach (string g in GlowNodes) replaced[g] = null;
            // the body bomb: wick down, forward and to the right
            double[] dWorld = RigMath.Unit(Add(Up.Select(c => -c).ToArray(), Forward, right));
            replaced[BodyNode] = BombMdt(bombMds, 3.5 / BombRadius, MatMul(BodyTurn(body), RotYTo(RigMath.Unit(ToLocal(body, dWorld)))), BodySpin);
            // the wing bombs: wick up, toward the body and forward
            foreach (string wn in WingNodes)
            {
                RigNode w = by[wn];
                double[] toBody = RigMath.Unit(new[] { body.WorldPos[0] - w.WorldPos[0], body.WorldPos[1] - w.WorldPos[1], body.WorldPos[2] - w.WorldPos[2] });
                dWorld = RigMath.Unit(Add(Up, toBody, Forward));
                replaced[wn] = BombMdt(bombMds, 1.3 / BombRadius, RotYTo(RigMath.Unit(ToLocal(w, dWorld))), WingSpin);
            }
            // the glow disc: a node of its own on the big bomb's bone, appended after the last
            int discIndex = nodes.Count;
            pack.Require(Mds).ReplacePayload(ReplaceMeshes(mds, replaced, new[] { (DiscRecord(discIndex, body.I), DiscQuad(quadMds)) }));
            pack.Require(Bbp).ReplacePayload(pack.Require(Bbp).Payload.Concat(new byte[BbpEntry]).ToArray());   // its bind pose (the model is not skinned: all zero)
            // the sheet recoloured, the bomb's sheet and the glow disc added to the bank
            var bank = new ImgBank(pack.Require(Img).Payload); var bbank = new ImgBank(bombImg);
            var look = SheetLook.Parse(ElfCaveWriter.Embedded(Look, $"{Look} is not embedded — the Bomb Gemron's look"));
            var items = bank.Entries.Select(e => (e.name, e.name == Sheet ? look.ApplyToSheet(bank.Block(e.name), out _, out _) : e.name == Sheet2 ? look.ApplyBaseToSheet(bank.Block(e.name)) : bank.Block(e.name))).ToList();
            items.AddRange(bbank.Entries.Select(e => (e.name, bbank.Block(e.name))));
            items.Add((DiscTex, Tim8.Swizzled(Tim8.ResampleTim8(GlowDisc.BuildT8(bank.Block(Sheet), light, GlowDisc.Elements[0].core, GlowDisc.Elements[0].outer), false, DiscTexSize))));
            pack.Require(Img).ReplacePayload(ImgBank.Build(bank.Magic, items));
            BakeSelfDestructMotion(pack);
            AddDiscTrack(pack, discIndex);
            return pack.Rebuild();
        }

        // ───────────────────────────── the glow disc ─────────────────────────────
        /// <summary>The Big Bang hanging bomb's glow — the torch glow disc in the Fire ramp (GlowDisc.Elements[0], the glow cave's row 1),
        /// drawn by the wall-torch routine at 0.6 (45 across at 1.0: 27) round a 4× bomb, 5 toward the camera — as a node of this model, so
        /// each Gemron draws its own: a quad with the game's glow-sprite flags (czappba: unlit, no depth write, additive, camera-facing: the
        /// engine turns its +Z to the camera), a little in front of the bomb, grown by a scale track from nothing as the fuse burns. The
        /// camera-facing draw scales the quad's offset by the node's Z scale, so the track scales X and Y only: the disc grows, its pull stays.</summary>
        private const string DiscNode = "bombglow__czappba", DiscTex = "bombglow", Bbp = ModelSource + ".bbp";
        private const int BbpEntry = 64;
        private const string DiscQuadPack = @"dun\effect\_b_boll.chr", DiscQuadMds = "b_boll.mds", DiscQuadNode = "bool__czappba";   // a game glow sprite's quad, cloned for its MDT shape
        private const double DiscDiameter = 30.0;                              // across, at full size (chosen on the preview page)
        private const double DiscPull = 9.4;                                   // its pull toward the camera, constant (chosen on the preview page): clear of the bomb from the start
        private const float DiscMin = 0.001f;                                   // "none": a scale of 0 would zero the node's axes (the draw takes their inverse lengths)
        /// <summary>(frame, scale) on the one timeline: none until a fuse, full at its blast, gone the frame after — the death 105→122, the
        /// self-destruct from its first frame (SelfDestructFuseStart → SelfDestructEnd − 1). The last key lies past SelfDestructEnd: the
        /// engine plays a track only between two of its keys (MotionProc skips it on its last key, and the node keeps whatever the last Bomb
        /// Gemron drawn left in the species' shared frames), and the self-destruct holds on its end.</summary>
        private static readonly (uint frame, float scale)[] DiscKeys =
            { (0, DiscMin), (105, DiscMin), (122, 1f), (123, DiscMin), (SelfDestructFuseStart, DiscMin), (SelfDestructEnd - 1, 1f), (SelfDestructEnd, DiscMin), (SelfDestructEnd + 1, DiscMin) };

        /// <summary>The disc's texture size. GlowDisc builds it 64×64 (the torch routine's rect), but no picture in a monster's `IM2` bank is
        /// narrower than 128, the size the block order below is proven at, so the disc is doubled (nearest texel; the GS filters it).</summary>
        private const int DiscTexSize = 128;


        /// <summary>The glow sprite's quad, re-laid as a DiscDiameter square DiscPull out along +Z (the camera, once the engine turns it),
        /// the whole disc on it, its material on DiscTex.</summary>
        private static byte[] DiscQuad(byte[] quadMds)
        {
            RigNode q = ModelCodec.ReadSkeleton(quadMds).First(n => n.Name == DiscQuadNode);
            var m = MdtMesh.Parse(quadMds, q.MeshOff);
            double r = DiscDiameter / 2;
            double[][] corners = { new[] { -r, r, 0.0, 0.0 }, new[] { -r, -r, 0.0, 1.0 }, new[] { r, r, 1.0, 0.0 }, new[] { r, -r, 1.0, 1.0 } };   // (x, y, u, v) per POSITION, the source quad's winding
            m.Pos = corners.Select(c => new[] { c[0], c[1], DiscPull, 1.0 }).ToList();
            // A vertex record is (position, normal, texture coordinate) and the source quad pairs them crosswise (position 3 takes
            // coordinate 2, position 2 coordinate 3), so each coordinate is set from the position its records use — by list index the
            // two right-hand corners swap UVs and the disc maps sheared, its centre at the edges. The codec's Norm is the coordinates.
            var uv = new double[m.Norm.Count][];
            foreach (var (_, _, recs) in m.Submeshes)
                foreach (int[] rec in recs)
                {
                    double[] want = { corners[rec[0]][2], corners[rec[0]][3], 1.0, 0.0 };
                    if (uv[rec[2]] != null && !uv[rec[2]].SequenceEqual(want)) throw new IOException($"{DiscQuadNode}: coordinate {rec[2]} is shared by two corners");
                    uv[rec[2]] = want;
                }
            if (uv.Any(c => c == null)) throw new IOException($"{DiscQuadNode}: a texture coordinate no record uses");
            m.Norm = uv.ToList();
            byte[] mat = (byte[])m.Materials[0].Clone();
            Array.Clear(mat, 0x34, 0x20); Encoding.ASCII.GetBytes(DiscTex).CopyTo(mat, 0x34);
            m.Materials = new List<byte[]> { mat };
            return m.Build();
        }

        /// <summary>The glow node's MDS record: index, size, name, no mesh yet, the bomb's bone as parent, identity at its centre.</summary>
        private static byte[] DiscRecord(int index, int parent)
        {
            var rec = new byte[0x70];
            IsoBytes.U32(rec, 0, (uint)index); IsoBytes.U32(rec, 4, 0x70);
            Encoding.ASCII.GetBytes(DiscNode).CopyTo(rec, 0x08);
            IsoBytes.U32(rec, 0x2C, (uint)parent);
            float[] id = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            for (int i = 0; i < 16; i++) IsoBytes.WrF(rec, 0x30 + i * 4, id[i]);
            return rec;
        }

        /// <summary>The glow node's scale track (DiscKeys) after the body motion's last track, its tags the file's own.</summary>
        private static void AddDiscTrack(ChrPack pack, int node)
        {
            ChrRecord rec = pack.Require(ModelSource + ".mot");
            var mot = MotFile.FromRecord(rec);
            MotTrack last = mot.Tracks[^1];
            var tr = new MotTrack { W0 = (uint)node, W1 = 0, W2 = 1, W3 = last.W3, W6 = last.W6, W7 = last.W7 };
            foreach (var (frame, s) in DiscKeys)
            {
                var k = new MotKeyframe(new byte[MotKeyframe.Size]) { Frame = frame };
                IsoBytes.WrF(k.Raw, 0x10, s); IsoBytes.WrF(k.Raw, 0x14, s); IsoBytes.WrF(k.Raw, 0x18, 1f);   // Z stays 1: the pull
                tr.Keyframes.Add(k);
            }
            mot.Tracks.Add(tr);
            rec.ReplacePayload(mot.BuildPayload());
        }

        // ───────────────────────────── the self-destruct motion ─────────────────────────────
        /// <summary>Key 14 (a duplicate of the attack, 130–150, which the script never plays) becomes the self-destruct, baked into both
        /// motion files (the body's and the shadow's) from frame 200 at 0.15: <see cref="ReversedReturn"/>, then <see cref="GuardLoop"/>
        /// once. A key on every frame, the in-between poses sampled from the vanilla keys (rotation slerped, the rest lerped), so the
        /// engine plays it the same however it steps between keys. Motion 6 itself, the guard loop, is untouched, so guarding never
        /// blows up.</summary>
        internal const int SelfDestructMotion = 14;
        internal const int SelfDestructStart = 200;
        private const string SelfDestructSpeed = "0.15";
        /// <summary>Motion 7 (the guard's return, 185–195) reversed, in pieces (source from, source to, key frames) at the key's 0.15: a
        /// piece's speed is 0.15 × source frames / key frames, and a length may be fractional. Now 195→191 at 0.15, 191→188 at 0.3,
        /// 188→185 at 0.6.</summary>
        private static readonly (int from, int to, double frames)[] ReversedReturn = { (195, 191, 4), (191, 188, 1.5), (188, 185, 0.75) };
        /// <summary>Motion 6 (the guard loop, 170–180, which starts and ends on 185's pose), played on its own keys at its own 0.15 on a
        /// whole frame: a fractional return is preceded by its first pose held for the fraction, so it ends on the frame motion 6 starts.</summary>
        private static readonly (int from, int to) GuardLoop = (170, 180);
        private static int LoopStart => (int)Math.Ceiling(ReversedReturn.Sum(p => p.frames));
        /// <summary>The key's end: one held frame past the final pose, as the script blows up when _CHK_MOTION_FRM reports done
        /// (frame ≥ end − 1), on the final pose (217).</summary>
        internal const int SelfDestructEnd = 218;
        /// <summary>Where the self-destruct's fuse starts to burn (the spark's walk, the glow, the tint): its first frame.</summary>
        internal const int SelfDestructFuseStart = SelfDestructStart;
        private static readonly string[] Motions = { ModelSource + ".mot", "e112s.mot" };   // the body's and the shadow's

        /// <summary>The source range shown <paramref name="tau"/> key frames into the self-destruct and the source frame it is at (a
        /// piece's end frame belongs to it).</summary>
        private static ((int from, int to) range, double source) PieceAt(double tau)
        {
            double lead = LoopStart - ReversedReturn.Sum(p => p.frames);           // the first pose held, so the return ends on a whole frame
            if (tau <= lead) return ((ReversedReturn[0].from, ReversedReturn[0].to), ReversedReturn[0].from);
            double at = lead;
            foreach (var (from, to, frames) in ReversedReturn)
            {
                if (tau <= at + frames) return ((from, to), from + (to - from) * (tau - at) / frames);
                at += frames;
            }
            var last = ReversedReturn[^1];
            if (tau <= LoopStart) return ((last.from, last.to), last.to);
            return (GuardLoop, Math.Min(GuardLoop.to, GuardLoop.from + (tau - LoopStart)));
        }

        private static void BakeSelfDestructMotion(ChrPack pack)
        {
            if (SelfDestructStart + LoopStart + (GuardLoop.to - GuardLoop.from) + 1 != SelfDestructEnd) throw new IOException("SelfDestructEnd does not match the self-destruct's pieces");
            foreach (string name in Motions)
            {
                ChrRecord rec = pack.Require(name);
                var mot = MotFile.FromRecord(rec);
                foreach (var tr in mot.Tracks)
                {
                    if (tr.Keyframes.Count > 0 && tr.Keyframes[^1].Frame >= SelfDestructStart) throw new IOException($"{name}: a track already reaches frame {tr.Keyframes[^1].Frame}");
                    var made = new List<MotKeyframe>();
                    for (int f = SelfDestructStart; f <= SelfDestructEnd + 1; f++)   // a key past the end: the engine skips a track on its last key, and the self-destruct holds on its end
                    {
                        var (range, s) = PieceAt(f - SelfDestructStart);
                        int lo = Math.Min(range.from, range.to), hi = Math.Max(range.from, range.to);
                        var keys = tr.Keyframes.Where(k => k.Frame >= lo && k.Frame <= hi).ToDictionary(k => k.Frame);
                        if (!keys.ContainsKey((uint)lo) || !keys.ContainsKey((uint)hi)) throw new IOException($"{name}: track {tr.W0}/{tr.W2} has no key on {lo} or {hi}");
                        made.Add(Sample(keys, tr.W2, s, (uint)f));
                    }
                    tr.Keyframes.AddRange(made);
                }
                rec.ReplacePayload(mot.BuildPayload());
            }
            ChrRecord cfgRec = pack.Require("info.cfg");
            Encoding sjis = Encoding.GetEncoding(932);
            string cfg = sjis.GetString(cfgRec.Payload);
            var lines = cfg.Split('\n');
            int key = -1, n = 0;
            for (int i = 0; i < lines.Length && key < 0; i++)
            {
                string s = lines[i].TrimStart();
                if (s.StartsWith("KEY_START")) { n = 0; continue; }
                if (s.StartsWith("KEY")) { if (n == SelfDestructMotion) key = i; n++; }
            }
            if (key < 0) throw new IOException("info.cfg: no key 14");
            string comment = lines[key].Contains("//") ? lines[key].Substring(lines[key].IndexOf("//", StringComparison.Ordinal)) : "\r";
            lines[key] = $"KEY\t{SelfDestructStart},\t{SelfDestructEnd},\t{SelfDestructSpeed},\t{comment}";
            cfgRec.ReplacePayload(sjis.GetBytes(string.Join("\n", lines)));
        }

        /// <summary>The track's pose at source frame <paramref name="s"/> as a key on <paramref name="frame"/>: the key itself when s
        /// is on one, else the pair around s blended (channel 0, rotation, slerped; the rest lerped).</summary>
        private static MotKeyframe Sample(Dictionary<uint, MotKeyframe> keys, uint chan, double s, uint frame)
        {
            uint a = keys.Keys.Where(f => f <= s).Max();
            MotKeyframe k = keys[a].Copy(); k.Frame = frame;
            if (s == a) return k;
            uint b = keys.Keys.Where(f => f > s).Min();
            double t = (s - a) / (b - a);
            float[] va = keys[a].Value, vb = keys[b].Value;
            double[] v = chan == 0 ? RigMath.SlerpKeys(va, vb, t) : Enumerable.Range(0, 4).Select(i => va[i] + (vb[i] - (double)va[i]) * t).ToArray();
            for (int i = 0; i < 4; i++) IsoBytes.WrF(k.Raw, 0x10 + i * 4, (float)v[i]);
            return k;
        }


        /// <summary>The apple shot's pack with the apple drawn as the bomb (<see cref="ShotPack"/>).</summary>
        private static byte[] BombShotPack(byte[] srcChr, byte[] bombMds, byte[] bombImg)
        {
            var pack = ChrPack.Parse(srcChr);
            var bombNodes = ModelCodec.ReadSkeleton(bombMds);
            int bombOff = bombNodes[0].MeshOff;
            byte[] bomb = bombMds.AsSpan(bombOff, (int)IsoBytes.U32(bombMds, bombOff + 8)).ToArray();
            ChrRecord mds = pack.Require(ShotMds);
            mds.ReplacePayload(ReplaceMeshes(mds.Payload, new Dictionary<string, byte[]> { [ShotNode] = bomb }));
            ChrRecord img = pack.Require(ShotImg);
            var bank = new ImgBank(img.Payload); var bbank = new ImgBank(bombImg);
            var items = bank.Entries.Select(e => (e.name, bank.Block(e.name))).ToList();
            items.AddRange(bbank.Entries.Select(e => (e.name, bbank.Block(e.name))));
            img.ReplacePayload(ImgBank.Build(bank.Magic, items));
            ChrRecord cfg = pack.Require(ShotCfg);
            Array.Clear(cfg.Raw, 0, 0x40); Encoding.Latin1.GetBytes(ShotCfgAs).CopyTo(cfg.Raw, 0); cfg.Name = ShotCfgAs;
            return pack.Rebuild();
        }

        /// <summary>The bomb's MDT with every position and normal scaled and rotated: p' = R · spin · (scale · p). The codec names the two
        /// attribute blocks the way the viewer does: <c>Uv</c> holds the NORMALS, <c>Norm</c> the texture coordinates, which stay.</summary>
        private static byte[] BombMdt(byte[] bombMds, double scale, double[][] R, double spin)
        {
            var nodes = ModelCodec.ReadSkeleton(bombMds);
            var m = MdtMesh.Parse(bombMds, nodes[0].MeshOff);
            double c = Math.Cos(spin), s = Math.Sin(spin);
            double[][] sp = { new[] { c, 0, s }, new[] { 0, 1.0, 0 }, new[] { -s, 0, c } };     // about the bomb's own +Y (the wick), right-hand rule
            var Rs = new double[3][];
            for (int r = 0; r < 3; r++) { Rs[r] = new double[3]; for (int col = 0; col < 3; col++) Rs[r][col] = ExactMath.Sum(R[r][0] * sp[0][col], R[r][1] * sp[1][col], R[r][2] * sp[2][col]); }
            m.Pos = m.Pos.Select(p => Mul(Rs, new[] { p[0] * scale, p[1] * scale, p[2] * scale }).Append(p[3]).ToArray()).ToList();
            m.Uv = m.Uv.Select(n => Mul(Rs, new[] { n[0], n[1], n[2] }).Append(n[3]).ToArray()).ToList();
            return m.Build();
        }

        /// <summary>The MDS with some nodes' meshes replaced (null drops the mesh), every mesh offset re-laid.</summary>
        /// <summary>The MDS with some nodes' meshes replaced (name → MDT bytes, or null to drop the mesh) and <paramref name="appended"/>
        /// (record, MDT bytes) nodes added after the last; every mesh offset re-laid.</summary>
        private static byte[] ReplaceMeshes(byte[] mds, Dictionary<string, byte[]> replaced, (byte[] rec, byte[] mdt)[] appended = null)
        {
            int count = (int)IsoBytes.U32(mds, 8), tbl = (int)IsoBytes.U32(mds, 12);
            var raws = Enumerable.Range(0, count).Select(i => mds.AsSpan(tbl + i * 0x70, 0x70).ToArray()).ToList();
            replaced = new Dictionary<string, byte[]>(replaced);
            foreach (var (rec, mdt) in appended ?? Array.Empty<(byte[], byte[])>()) { raws.Add((byte[])rec.Clone()); replaced[IsoBytes.NameAt(rec, 8, 0x20)] = mdt; }
            count = raws.Count;
            var nodesBlob = new List<byte>(); var meshes = new List<byte>();
            int bse = 0x10 + count * 0x70;
            for (int i = 0; i < count; i++)
            {
                byte[] raw = raws[i];
                string name = IsoBytes.NameAt(raw, 8, 0x20);
                int off = (int)IsoBytes.U32(raw, 0x28);
                byte[] blob = replaced.TryGetValue(name, out byte[] nb) ? nb : off != 0 && off < mds.Length ? mds.AsSpan(off, (int)IsoBytes.U32(mds, off + 8)).ToArray() : null;
                if (blob != null)
                {
                    IsoBytes.U32(raw, 0x28, (uint)(bse + meshes.Count));
                    meshes.AddRange(blob);
                    meshes.AddRange(new byte[ExactMath.Mod(-meshes.Count, 16)]);
                }
                else IsoBytes.U32(raw, 0x28, 0);
                nodesBlob.AddRange(raw);
            }
            var o = new List<byte>(mds.AsSpan(0, 8).ToArray());
            o.AddRange(BitConverter.GetBytes(count)); o.AddRange(BitConverter.GetBytes(0x10));
            o.AddRange(nodesBlob); o.AddRange(meshes);
            return o.ToArray();
        }


        // vectors (row convention; sums as the Python builder's, compensated)
        private static double[] Add(params double[][] vs) => Enumerable.Range(0, 3).Select(c => ExactMath.Sum(vs.Select(v => v[c]))).ToArray();
        private static double[] Mul(double[][] m, double[] v) => Enumerable.Range(0, 3).Select(r => ExactMath.Sum(Enumerable.Range(0, 3).Select(c => m[r][c] * v[c]))).ToArray();

        /// <summary>The rotation (3×3, rows) taking +Y onto unit vector d (Rodrigues).</summary>
        /// <summary>The rotation (3×3, rows) by angle <paramref name="a"/> about unit axis <paramref name="k"/>, right-hand rule (Rodrigues).</summary>
        private static double[][] AxisRot(double[] k, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a), C = 1 - c, x = k[0], y = k[1], z = k[2];
            return new[]
            {
                new[] { c + x * x * C, x * y * C - z * s, x * z * C + y * s },
                new[] { y * x * C + z * s, c + y * y * C, y * z * C - x * s },
                new[] { z * x * C - y * s, z * y * C + x * s, c + z * z * C },
            };
        }

        private static double[][] MatMul(double[][] A, double[][] B) =>
            Enumerable.Range(0, 3).Select(r => Enumerable.Range(0, 3).Select(c => ExactMath.Sum(A[r][0] * B[0][c], A[r][1] * B[1][c], A[r][2] * B[2][c])).ToArray()).ToArray();

        /// <summary>BodyPitch / BodyRoll / BodyYaw as a rotation in the body bone's frame: yaw · roll · pitch about the Gemron's up,
        /// forward and left in that frame.</summary>
        private static double[][] BodyTurn(RigNode body)
        {
            double[] right = RigMath.Cross(Forward, Up);
            double[] left = RigMath.Unit(ToLocal(body, right.Select(c => -c).ToArray())), fwd = RigMath.Unit(ToLocal(body, Forward)), up = RigMath.Unit(ToLocal(body, Up));
            const double D2R = Math.PI / 180.0;
            return MatMul(AxisRot(up, BodyYaw * D2R), MatMul(AxisRot(fwd, BodyRoll * D2R), AxisRot(left, BodyPitch * D2R)));
        }

        private static double[][] RotYTo(double[] d)
        {
            double[] y = { 0.0, 1.0, 0.0 }; d = RigMath.Unit(d);
            double c = Math.Max(-1.0, Math.Min(1.0, RigMath.Dot(y, d))); double[] axis = RigMath.Cross(y, d); double s = Math.Sqrt(RigMath.Dot(axis, axis));
            if (s < 1e-6)
                return c > 0 ? new[] { new[] { 1.0, 0, 0 }, new[] { 0, c, 0 }, new[] { 0, 0, c } } : new[] { new[] { 1.0, 0, 0 }, new[] { 0, -1.0, 0 }, new[] { 0, 0, -1.0 } };
            double[] k = axis.Select(a => a / s).ToArray(); double C = 1 - c;
            return new[]
            {
                new[] { c + k[0] * k[0] * C, k[0] * k[1] * C - k[2] * s, k[0] * k[2] * C + k[1] * s },
                new[] { k[1] * k[0] * C + k[2] * s, c + k[1] * k[1] * C, k[1] * k[2] * C - k[0] * s },
                new[] { k[2] * k[0] * C - k[1] * s, k[2] * k[1] * C + k[0] * s, c + k[2] * k[2] * C },
            };
        }

        /// <summary>A world direction in the node's frame: the matrices take row vectors (world = local · M), so a world direction comes
        /// back to local through M's rotation rows as a column operation.</summary>
        private static double[] ToLocal(RigNode node, double[] dWorld)
        {
            var rows = new double[3][]; for (int r = 0; r < 3; r++) rows[r] = new[] { node.World[r][0], node.World[r][1], node.World[r][2] };
            return Mul(rows, dWorld);
        }

        // ───────────────────────────── the script ─────────────────────────────
        private static readonly byte[] Probe = Encoding.ASCII.GetBytes("DCE-BOMBGEMRON-v1\0");
        private const uint OpPushVar = 1, OpPushRef = 2, OpPushConst = 3, OpDrop = 4, OpStore = 5, OpAdd = 6, OpNeg = 11, OpCmp = 14, OpRet = 15,
                           OpJmp = 16, OpBrFalse = 17, OpCall = 19, OpExt = 21, OpNop = 22, OpYield = 23, OpOr = 25;
        private const uint TInt = 1, TFloat = 2, TStr = 3, VInt = 1, VFloat = 8;    // push-const types; push-var / push-ref local kinds
        private const uint CmpEq = 40, CmpLt = 42, CmpGt = 44, CmpGe = 45;
        private const uint FnGetDistance = 10, FnGetPosition = 11, FnSetMoveCancel = 34, FnSetMuteki = 101, FnSetAlpha = 102, FnSetDead = 104,
                           FnGetLifeRate = 109, FnSetMotion = 200, FnChkMotionFrm = 201, FnSetShot2 = 229;
        private const int DeathMotion = 11, HeaderBytes = 56;
        private const float BlastFrame = 122f, ClipStart = 105f, BlastHeight = 11f, BlastDamage = 150f, SelfDestructHp = 25f, SelfDestructRange = 22f;
        private const uint FadeHeader = 0;                                            // func 0x60 (the fade-out), header offset codeBase-relative
        private const float FadeStep = 4f, FadeChime = 84f;                          // its arguments, as the vanilla death passes them

        /// <summary>A cell list with symbolic branch targets, laid at a known code offset.</summary>
        private sealed class Code
        {
            internal readonly List<(uint op, uint a1, uint a2, string label)> Cells = new();
            internal readonly Dictionary<string, int> Labels = new();
            internal void Mark(string l) => Labels[l] = Cells.Count;
            internal void Add(uint op, uint a1 = 0, uint a2 = 0) => Cells.Add((op, a1, a2, null));
            internal void Branch(uint op, string label) => Cells.Add((op, 0, 0, label));
            internal void Int(int v) => Add(OpPushConst, TInt, unchecked((uint)v));
            internal void Float(float v) => Add(OpPushConst, TFloat, BitConverter.ToUInt32(BitConverter.GetBytes(v), 0));
            internal void Str(int off) => Add(OpPushConst, TStr, (uint)off);
            internal void Var(int v, uint kind) => Add(OpPushVar, (uint)v, kind);
            internal void Ref(int v, uint kind) => Add(OpPushRef, (uint)v, kind);
            internal void Ext(int argc) => Add(OpExt, (uint)argc);
            internal void Set(int v, uint kind, Action value) { Ref(v, kind); value(); Add(OpStore); Add(OpDrop); }
            internal void Ret(int v) { Int(v); Add(OpRet); Add(0); }                     // a zero cell after the RET, as every vanilla function ends
            internal byte[] Bytes(int codeOff, int codeBase)
            {
                var b = new byte[Cells.Count * 12];
                for (int i = 0; i < Cells.Count; i++)
                {
                    var (op, a1, a2, label) = Cells[i];
                    if (label != null) a1 = (uint)(codeOff + Labels[label] * 12 - codeBase);
                    IsoBytes.U32(b, i * 12, op); IsoBytes.U32(b, i * 12 + 4, a1); IsoBytes.U32(b, i * 12 + 8, a2);
                }
                return b;
            }
        }

        private static uint U(byte[] b, int o) => IsoBytes.U32(b, o);

        private static byte[] BombGemronScript(byte[] stb, out string why)
        {
            int cb = (int)U(stb, 8);
            if (IsoBytes.Find(stb, Probe) >= 0) { why = "already patched"; return stb; }
            int bcol0 = IsoBytes.FindFrom(stb, Encoding.ASCII.GetBytes("bcol0\0"), cb);
            if (bcol0 < 0) throw new IOException("e115a.stb: no 'bcol0' string");
            int label120 = LabelHeader(stb, 120), label100 = LabelHeader(stb, 100);
            int deathCall = cb + (int)U(stb, label120) ;                               // label 120's code: CALL death; drop; push 0; RET
            if (U(stb, deathCall) != OpCall) throw new IOException("e115a.stb: label 120 does not start with a CALL");
            int deathHdr = cb + (int)U(stb, deathCall + 8), deathCode = cb + (int)U(stb, deathHdr);
            // the death function's cells end at its RET; its wait call (CALL func 0x5C4, after the motion is set) is the cell to retarget
            int deathEnd = deathCode; while (U(stb, deathEnd) != OpRet) deathEnd += 12; deathEnd += 12;
            var calls = new List<int>(); for (int o = deathCode; o < deathEnd; o += 12) if (U(stb, o) == OpCall) calls.Add(o);
            if (calls.Count != 2) throw new IOException($"e115a.stb: the death function has {calls.Count} calls, not 2 (wait, fade)");
            int waitCall = calls[0], fadeHdr = (int)U(stb, calls[1] + 8);
            // label 100's loop head: `.L0; push 1; BR_FALSE` — the push becomes the self-destruct call (its return value is the 1)
            int loopHead = -1;
            for (int o = cb + (int)U(stb, label100); o + 24 < stb.Length; o += 12)
                if (U(stb, o) == OpNop && U(stb, o + 12) == OpPushConst && U(stb, o + 16) == TInt && U(stb, o + 20) == 1 && U(stb, o + 24) == OpBrFalse) { loopHead = o + 12; break; }
            if (loopHead < 0) throw new IOException("e115a.stb: label 100's loop head not found");

            var o2 = new List<byte>(stb);
            while (o2.Count % 4 != 0) o2.Add(0);
            o2.AddRange(Probe);
            while (o2.Count % 4 != 0) o2.Add(0);
            int strOff = bcol0 - cb;
            // 1. the blast wait: the vanilla wait (motion end, the death cry at its frame) plus the blast at BlastFrame
            int waitHdr = o2.Count; o2.AddRange(Header(waitHdr + HeaderBytes - cb, locals: 10, args: 2));
            o2.AddRange(BlastWait(strOff).Bytes(waitHdr + HeaderBytes, cb));
            // 2. the death function: the vanilla one, its wait call retargeted
            int newDeathHdr = o2.Count; o2.AddRange(Header(newDeathHdr + HeaderBytes - cb, locals: 0, args: 0));
            byte[] death = stb.AsSpan(deathCode, deathEnd - deathCode).ToArray();
            IsoBytes.U32(death, waitCall - deathCode + 8, (uint)(waitHdr - cb));
            o2.AddRange(death); o2.AddRange(new byte[12]);
            // 3. the self-destruct: the loop head's `push 1` as a call that returns 1, or blows up and returns 0
            int selfHdr = o2.Count; o2.AddRange(Header(selfHdr + HeaderBytes - cb, locals: 8, args: 0));
            o2.AddRange(SelfDestruct(strOff, fadeHdr).Bytes(selfHdr + HeaderBytes, cb));
            byte[] outb = o2.ToArray();
            // the throw aimed at the player's feet + AimHeight (Holy's + 7.4 flies under his contact point, feet + 14–18, by more than
            // the 6 a shot with no radius of its own has to come within)
            int aim = AimCell(stb);
            IsoBytes.U32(outb, aim + 8, BitConverter.ToUInt32(BitConverter.GetBytes(AimHeight), 0));
            IsoBytes.U32(outb, deathCall + 8, (uint)(newDeathHdr - cb));
            IsoBytes.U32(outb, loopHead, OpCall); IsoBytes.U32(outb, loopHead + 4, 0); IsoBytes.U32(outb, loopHead + 8, (uint)(selfHdr - cb));
            why = $"death → blast wait @+0x{waitHdr - cb:X} (frame {BlastFrame:g}), self-destruct @+0x{selfHdr - cb:X} from the AI loop head @0x{loopHead:X}, {stb.Length:N0}→{outb.Length:N0} B";
            return outb;
        }

        private const float HolyAimHeight = 7.4f, AimHeight = 14f;

        /// <summary>The one `push 7.4f` cell — the shot's target raised above the player's feet before `_SET_SHOT('dcol0', …)`.</summary>
        private static int AimCell(byte[] stb)
        {
            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(HolyAimHeight), 0);
            int found = -1, n = 0;
            for (int o = (int)U(stb, 8); o + 12 <= stb.Length; o += 4)
                if (U(stb, o) == OpPushConst && U(stb, o + 4) == TFloat && U(stb, o + 8) == bits) { found = o; n++; }
            if (n != 1) throw new IOException($"e115a.stb: {n} `push 7.4f` cells, expected the shot's aim alone");
            return found;
        }

        private static int LabelHeader(byte[] stb, int id)
        {
            int tbl = (int)U(stb, 0xC), cnt = (int)U(stb, 0x10);
            for (int i = 0; i < cnt; i++) if (U(stb, tbl + i * 8) == id) return (int)U(stb, tbl + i * 8 + 4);
            throw new IOException($"e115a.stb: no label {id}");
        }

        private static byte[] Header(int codeOffFromCb, int locals, int args)
        {
            var b = new byte[HeaderBytes];
            IsoBytes.U32(b, 0, (uint)codeOffFromCb); IsoBytes.U32(b, 8, (uint)locals); IsoBytes.U32(b, 12, (uint)args);
            return b;
        }

        /// <summary>v0 = cry sound, v1 = its frame (the arguments); v2 cried, v3 motion done, v4 frame, v5 blown, v6–v8 position, v9 the death
        /// clip seen running before its blast frame (the frame read on the first ticks can still be the previous clip's, past 122).</summary>
        private static Code BlastWait(int strOff)
        {
            var c = new Code();
            c.Set(3, VInt, () => c.Int(0)); c.Set(2, VInt, () => c.Int(0)); c.Set(5, VInt, () => c.Int(0));
            c.Mark("loop");
            c.Var(3, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "end");
            c.Add(OpYield);
            c.Int((int)FnChkMotionFrm); c.Ref(3, VInt); c.Ref(4, VFloat); c.Ext(3);
            c.Var(2, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "blast");
            c.Var(4, VFloat); c.Var(1, VFloat); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "blast");
            c.Int(141); c.Var(0, VInt); c.Ext(2);                                    // _SET_SND_NOW(v0)
            c.Set(2, VInt, () => c.Int(1));
            c.Mark("blast");
            Started(c, 4, 9);
            c.Var(5, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "loop");
            c.Var(9, VInt); c.Int(1); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "loop");
            c.Var(4, VFloat); c.Float(BlastFrame); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "loop");
            Blast(c, strOff, 6, 7, 8);
            c.Set(5, VInt, () => c.Int(1));
            c.Branch(OpJmp, "loop");
            c.Mark("end");
            c.Ret(0);
            return c;
        }

        /// <summary>v0 HP rate, v1 distance, v2 motion done (counted from frame 200 on), v4 frame, v5–v7 position. Returns 1 (the loop head's own value) unless it blew
        /// up, then 0. Hittable throughout (no invincibility: the outlaws' 1600 frames of it made the fuse untouchable).</summary>
        private static Code SelfDestruct(int strOff, int fadeHdr)
        {
            var c = new Code();
            c.Int((int)FnGetLifeRate); c.Ref(0, VFloat); c.Ext(2);
            c.Var(0, VFloat); c.Float(SelfDestructHp); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "near");
            c.Ret(1);
            c.Mark("near");
            c.Int((int)FnGetDistance); c.Ref(1, VFloat); c.Ext(2);
            c.Var(1, VFloat); c.Float(SelfDestructRange); c.Add(OpCmp, CmpGt); c.Branch(OpBrFalse, "go");
            c.Ret(1);
            c.Mark("go");
            c.Int((int)FnSetMoveCancel); c.Ext(1);
            c.Int((int)FnSetMotion); c.Int(SelfDestructMotion); c.Float(-1f); c.Int(2); c.Int(4); c.Add(OpOr); c.Ext(4);   // its key's speed; held on its last frame
            c.Set(2, VInt, () => c.Int(0));
            c.Mark("loop");
            c.Var(2, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "end");
            c.Add(OpYield);
            c.Int((int)FnChkMotionFrm); c.Ref(2, VInt); c.Ref(4, VFloat); c.Ext(3);   // 1 on the motion's last frame
            c.Var(4, VFloat); c.Float(SelfDestructStart); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "stale");   // a frame below 200 is the old clip's
            c.Branch(OpJmp, "loop");
            c.Mark("stale");
            c.Set(2, VInt, () => c.Int(0));
            c.Branch(OpJmp, "loop");
            c.Mark("end");
            Blast(c, strOff, 5, 6, 7);
            c.Int((int)FnSetAlpha); c.Float(0.5f); c.Ext(2);
            c.Float(FadeStep); c.Float(FadeChime); c.Add(OpCall, 0, (uint)fadeHdr); c.Add(OpDrop);
            c.Int((int)FnSetDead); c.Ext(1);
            c.Ret(0);
            return c;
        }

        /// <summary><paramref name="started"/> = 1 once <paramref name="frame"/> has been seen inside the death clip below its blast frame.</summary>
        private static void Started(Code c, int frame, int started)
        {
            string skip = "started" + started;
            c.Var(frame, VFloat); c.Float(ClipStart); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, skip);
            c.Var(frame, VFloat); c.Float(BlastFrame); c.Add(OpCmp, CmpLt); c.Branch(OpBrFalse, skip);
            c.Set(started, VInt, () => c.Int(1));
            c.Mark(skip);
        }

        /// <summary>_GET_POSITION(-1) into x/y/z, y raised BlastHeight, _SET_SHOT2('bcol0', x, y, z, BlastDamage).</summary>
        private static void Blast(Code c, int strOff, int x, int y, int z)
        {
            c.Int((int)FnGetPosition); c.Int(1); c.Add(OpNeg); c.Ref(x, VFloat); c.Ref(y, VFloat); c.Ref(z, VFloat); c.Ext(5);
            c.Set(y, VFloat, () => { c.Var(y, VFloat); c.Float(BlastHeight); c.Add(OpAdd); });
            c.Int((int)FnSetShot2); c.Str(strOff); c.Var(x, VFloat); c.Var(y, VFloat); c.Var(z, VFloat); c.Int((int)BlastDamage); c.Ext(6);
        }
    }
}
