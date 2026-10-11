using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Atla Gemron on the disc (its species record: SpeciesRows / ElfSpeciesPatches): Ice Gemron (e112a) holding a dungeon
    /// atla (<c>dun\etc\atr.mds</c>, scaled to its big gem, turned as a real atla is and by the page's GRAFT_TURN) and wearing Toan's Atlamillia on its forehead, the Atlamillia's light
    /// (<c>gedit\s88\chara\e503ex_atall.chr</c>'s glow pair) playing on its own frames, fly_light glows in place of its wing gems, and a
    /// spawn bone for its shot. The death is copied past the vanilla keys and held; on <see cref="Swap"/> the held atla leaves the draw
    /// (AtlaGemron spawns the real one there). Under the orphan DATA.HED entries e07a__ / __e116a renamed to <see cref="Stem"/>, its
    /// script Holy Gemron's firing from the spawn bone, its name in the dungeon message bank; its shot's pack under the orphan e20a_
    /// renamed <c>dun\effect\atla_s.chr</c>. Byte-exact twins of game_data/viewers/model/atla_gemron.py's build_ship and
    /// build_shot_pack; the numbers that page derived from the geometry come from the embedded atlaGemronRecipe.json, the body sheets'
    /// look from atlaGemronLook.json.</summary>
    internal static class AtlaGemronBake
    {
        internal const string Stem = "e169a";
        private const string Dir = @"dun\monstor\", Base = "e112a", ScriptBase = "e115a";
        /// <summary>The unused archive entries renamed (docs/custom-enemies.md, Archive entries): e07a__.chr and e20a_.chr, duplicates no
        /// record names, and __e116a.stb, a script nothing loads.</summary>
        private const string ChrDonor = "e07a__.chr", StbDonor = "__e116a.stb", ShotDonor = @"dun\monstor\e20a_.chr";
        private const string Name = "Atla Gemron";
        private const string Recipe = "atlaGemronRecipe.json", Look = "atlaGemronLook.json";
        private const string NameBank = @"dun\message\ww_mes\dunmsd00_1.mes";

        private const string AtlaPack = @"dun\pack\maindat.pac", AtlaMds = "atr.mds", AtlaImg = @"dun\etc\atrtx.img";
        private const string Gem = "tama1__m", Head = "obj2_2";
        private const string Toan = @"dun\mainchara\c01d.chr", ToanMds = "c01d.mds", Lens = "renzu__m", LensTex = "c01d03";
        private static readonly int[] LensCrop = { 128, 32, 128, 64 };          // the lens's patch of Toan's sheet (x, y, width, height)
        private const string LightChr = @"gedit\s88\chara\e503ex_atall.chr", LightMds = "e503ex_atall.mds", LightMot = "e503ex_atall.mot";
        private const string LightName = "atlight", LightTex = "e503ex_at03";
        private const string FlyChr = @"gedit\e01\chara\fly_light.chr", FlyTex = "fly_light";
        private static readonly string[] FlyNodes = { "012__CZAPPBA", "013__CZAPPBA" }, WingGems = { "tamas00__m", "tamas03__m" };
        private const double FlySat = 0.5;
        internal const string SpawnName = "shot0";                                // the script's _SET_SHOT names it instead of dcol0
        private const string SpawnFrom = "dcol0";

        // The death: the vanilla death (105–125, key 11 at 0.35) copied to DeathStart on, its last pose held to DeathEnd.
        private const int DeathKey = 11, DeathLoopKey = 12, ClipFrom = 105, ClipTo = 125, DeathStart = 200;
        private const string DeathSpeed = "0.35";
        /// <summary>The copy of vanilla death frame 122 (the gem landed): the held atla leaves the draw.</summary>
        internal const int Swap = DeathStart + 122 - ClipFrom;
        private const int Bounce = 12;
        internal const int DeathEnd = Swap + Bounce;
        private const int TimelineEnd = DeathEnd + 1;
        private const uint SubtreeVisible = 0x33;
        private static readonly uint[] CameraChannels = { 30, 31, 32, 33 };
        private static readonly Dictionary<uint, double> Thin = new() { [1] = 0.02, [2] = 0.05, [40] = 0.03 };

        /// <summary>The shot's pack: e503ex_atall's key 9 tail (frames 221–250 at 0.35) under the shot's own frame and a root turned and
        /// sized (the recipe's shotRows), backing off the flight so the effect stands where it was fired.</summary>
        internal const string ShotModel = "atla_s";
        private const string ShotPack = @"dun\effect\" + ShotModel + ".chr";
        internal const int ShotFrom = 221, ShotTo = 250;
        private const double ShotSpeed = 0.35;
        private static readonly string[] ShotTex = { "e503ex_at01", "e503ex_at03" };
        /// <summary>The shot's speed (a frame), the recipe's: the straight fit of the orbs' forward travel (ElfSpeciesPatches.PatchAtlaShot).</summary>
        internal static float ShotFlight => (float)LoadRecipe().GetProperty("shotFlight").GetDouble();
        /// <summary>A real atla's root turn (w, x, y, z; QuatToMat's order), the recipe's (CodeCaves.AtlaRootQuat).</summary>
        internal static float[] AtlaRootQuat => Vec(LoadRecipe().GetProperty("atlaRootQuat")).Select(x => (float)x).ToArray();
        /// <summary>The recipe, for AtlaGemron's bounce: the held atla's chain on the swap frame, the atla's centre and root record.</summary>
        internal static JsonElement Numbers => LoadRecipe();

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            arc.Claim(Dir + ChrDonor, Dir + Stem + ".chr");
            arc.Claim(Dir + StbDonor, Dir + Stem + ".stb");
            arc.Claim(ShotDonor, ShotPack);
            JsonElement rc = LoadRecipe();
            byte[] chr = Pack(arc, rc);
            arc.Redirect(Dir + Stem + ".chr", chr);
            log($"{Stem}.chr: {chr.Length:N0} B");
            byte[] shot = ShotPackBytes(arc, rc);
            arc.Redirect(ShotPack, shot);
            log($"{ShotPack}: the Atlamillia's burst, {shot.Length:N0} B");
            byte[] stb = Script(arc.Read(Dir + ScriptBase + ".stb"), out string why);
            arc.Redirect(Dir + Stem + ".stb", stb);
            log($"{Stem}.stb: {why}");
            int id = DungeonMessageBank.NameBase + EnemySpecies.AtlaGemron.Id;
            byte[] named = CrystalGemronBake.WithName(arc.Read(NameBank), id, WeaponDescriptions.Encode(Name).Concat(new ushort[] { 0xFF01 }).ToArray());
            if (named == null) log($"name: message {id} already reads '{Name}'");
            else { arc.Redirect(NameBank, named); log($"name: message {id} = '{Name}'"); }
        }

        internal static double[] Vector(JsonElement e) => Vec(e);
        internal static double[][] Matrix(JsonElement e) => Rows(e);

        private static JsonElement LoadRecipe()
        {
            using var doc = JsonDocument.Parse(ElfCaveWriter.Embedded(Recipe, $"{Recipe} is not embedded — the Atla Gemron's numbers"));
            return doc.RootElement.Clone();
        }

        private static double[] Vec(JsonElement e) => e.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        private static double[][] Rows(JsonElement e) => e.EnumerateArray().Select(Vec).ToArray();
        private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

        // ───────────────────────────── the script ─────────────────────────────
        /// <summary>Holy Gemron's script with its one _SET_SHOT (push 133, then the bone's string) firing from <see cref="SpawnName"/>: the
        /// string, the shot function's own copy, renamed in place (the same length).</summary>
        private static byte[] Script(byte[] stb, out string why)
        {
            int cb = (int)IsoBytes.U32(stb, 8);
            var sites = new List<int>();
            for (int c = cb; c + 24 <= stb.Length; c += 4)
                if (IsoBytes.U32(stb, c) == 3 && IsoBytes.U32(stb, c + 4) == 1 && IsoBytes.U32(stb, c + 8) == 133 && IsoBytes.U32(stb, c + 12) == 3 && IsoBytes.U32(stb, c + 16) == 3)
                    sites.Add(cb + (int)IsoBytes.U32(stb, c + 20));
            if (sites.Count != 1) throw new IOException($"{ScriptBase}.stb: {sites.Count} _SET_SHOT bone strings, expected 1");
            string bone = IsoBytes.NameAt(stb, sites[0], 8);
            if (bone != SpawnFrom && bone != SpawnName) throw new IOException($"{ScriptBase}.stb: the shot fires from '{bone}', not {SpawnFrom}");
            byte[] o = (byte[])stb.Clone();
            Encoding.ASCII.GetBytes(SpawnName).CopyTo(o, sites[0]);
            why = $"Holy Gemron's, its shot fired from {SpawnName} ({SpawnFrom} before)";
            return o;
        }

        // ───────────────────────────── the model ─────────────────────────────
        private static byte[] Pack(IsoArchive arc, JsonElement rc)
        {
            var pack = ChrPack.Parse(arc.Read(Dir + Base + ".chr"));
            byte[] mds = pack.Require(Base + ".mds").Payload;
            var nodes = ModelCodec.ReadSkeleton(mds);
            var by = nodes.ToDictionary(n => n.Name);
            var mot = MotFile.FromPack(pack, Base + ".mot");
            MotTrack refTrack = mot.Tracks[^1];
            double[][] ident = { new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }, new double[] { 0, 0, 1 } };
            var grafts = new List<(List<byte[]> recs, Dictionary<int, byte[]> meshes)>();
            // the held atla, under the big gem
            byte[] amds = ChrPack.Parse(arc.Read(AtlaPack)).Require(AtlaMds).Payload;
            var anodes = ModelCodec.ReadSkeleton(amds);
            int atbl = (int)IsoBytes.U32(amds, 12), at = nodes.Count;
            double g = rc.GetProperty("g").GetDouble();
            var graft = new List<byte[]>(); var gmesh = new Dictionary<int, byte[]>();
            for (int j = 0; j < anodes.Count; j++)
            {
                byte[] raw = amds.AsSpan(atbl + j * 0x70, 0x70).ToArray();
                graft.Add(anodes[j].Parent < 0 ? CrystalGemronBake.Record(raw, at + j, by[Gem].I, Rows(rc.GetProperty("graftR")), Vec(rc.GetProperty("graftT")), 1.0)
                                               : CrystalGemronBake.Record(raw, at + j, at + anodes[j].Parent, null, null, g));
                if (anodes[j].MeshOff != 0) gmesh[j] = CrystalGemronBake.ScaledMesh(amds, anodes[j].MeshOff, g);
            }
            grafts.Add((graft, gmesh));
            // the forehead Atlamillia: Toan's lens mesh, his record's stretch and the fit's size in it, on the cropped sheet
            int lensAt = at + anodes.Count;
            var toan = ChrPack.Parse(arc.Read(Toan));
            byte[] tmds = toan.Require(ToanMds).Payload;
            RigNode tn = ModelCodec.ReadSkeleton(tmds).First(n => n.Name == Lens);
            int ttbl = (int)IsoBytes.U32(tmds, 12);
            var lm = MdtMesh.Parse(tmds, tn.MeshOff);
            double[] st = Vec(rc.GetProperty("lensStretch")); double sz = rc.GetProperty("lensSize").GetDouble();
            lm.Pos = lm.Pos.Select(p => new[] { p[0] * st[0], p[1] * st[1], p[2] * st[2], p[3] }).ToList();
            lm.Pos = lm.Pos.Select(p => new[] { p[0] * sz, p[1] * sz, p[2] * sz, p[3] }).ToList();
            lm.Norm = lm.Norm.Select(uv => new[] { (uv[0] * 256 - LensCrop[0]) / LensCrop[2], (uv[1] * 256 - LensCrop[1]) / LensCrop[3] }.Concat(uv.Skip(2)).ToArray()).ToList();
            grafts.Add((new List<byte[]> { CrystalGemronBake.Record(tmds.AsSpan(ttbl + tn.I * 0x70, 0x70).ToArray(), lensAt, by[Head].I, Rows(rc.GetProperty("lensR")), Vec(rc.GetProperty("lensT")), 1.0) },
                        new Dictionary<int, byte[]> { [0] = lm.Build() }));
            // the light: its root on the gem's face, e503ex_atall's glow pair and their ancestors under it
            int lightAt = lensAt + 1;
            var lp = ChrPack.Parse(arc.Read(LightChr));
            byte[] emds = lp.Require(LightMds).Payload;
            var enodes = ModelCodec.ReadSkeleton(emds);
            var emot = MotFile.FromRecord(lp.Require(LightMot));
            int etbl = (int)IsoBytes.U32(emds, 12);
            int[] order = Ints(rc.GetProperty("lightNodes"));
            var nw = new Dictionary<int, int>();
            for (int k = 0; k < order.Length; k++) nw[order[k]] = lightAt + 1 + k;
            var light = new List<byte[]> { PlainRecord(lightAt, lensAt, LightName, Rows(rc.GetProperty("lightR")), Vec(rc.GetProperty("lightRecordT"))) };
            var lmesh = new Dictionary<int, byte[]>();
            for (int k = 0; k < order.Length; k++)
            {
                RigNode n = enodes[order[k]];
                light.Add(CrystalGemronBake.Record(emds.AsSpan(etbl + n.I * 0x70, 0x70).ToArray(), nw[n.I], nw.TryGetValue(n.Parent, out int p) ? p : lightAt, null, null, 1.0));
                if (n.MeshOff != 0) lmesh[1 + k] = Mesh(emds, n.MeshOff);
            }
            grafts.Add((light, lmesh));
            // the wing lights: fly_light's glow pair on each wing gem, scaled to the chosen frame
            int wingAt = lightAt + light.Count;
            var fp = ChrPack.Parse(arc.Read(FlyChr));
            byte[] fmds = fp.Require("fly_light.mds").Payload;
            var fnodes = ModelCodec.ReadSkeleton(fmds);
            int ftbl = (int)IsoBytes.U32(fmds, 12);
            var wing = new List<byte[]>(); var wmesh = new Dictionary<int, byte[]>();
            foreach (string gem in WingGems)
                foreach (string nm in FlyNodes)
                {
                    RigNode n = fnodes.First(x => x.Name == nm);
                    double[] s = Vec(rc.GetProperty("fly").GetProperty(nm));
                    var m = MdtMesh.Parse(fmds, n.MeshOff);
                    m.Pos = m.Pos.Select(q => new[] { q[0] * s[0], q[1] * s[1], q[2] * s[2], q[3] }).ToList();
                    wmesh[wing.Count] = m.Build();
                    wing.Add(CrystalGemronBake.Record(fmds.AsSpan(ftbl + n.I * 0x70, 0x70).ToArray(), wingAt + wing.Count, by[gem].I, ident, new double[] { 0, 0, 0 }, 1.0));
                }
            grafts.Add((wing, wmesh));
            // the shot's spawn bone, under the root
            int spawnAt = wingAt + wing.Count;
            grafts.Add((new List<byte[]> { PlainRecord(spawnAt, 0, SpawnName, ident, Vec(rc.GetProperty("spawnT"))) }, new Dictionary<int, byte[]>()));
            int total = spawnAt + 1;
            pack.Require(Base + ".mds").ReplacePayload(CrystalGemronBake.GraftMds(mds, grafts, new Dictionary<string, byte[]>()));
            ChrRecord bbp = pack.Require(Base + ".bbp");
            bbp.ReplacePayload(bbp.Payload.Concat(new byte[64 * (total - nodes.Count)]).ToArray());   // not skinned: zero
            DeathClip(mot);
            var added = new List<MotTrack> { Track(refTrack, at, 0, SubtreeVisible,
                new[] { (0, 1.0), (Swap - 1, 1.0), (Swap, 0.0), (TimelineEnd, 0.0) }.Select(fv => CrystalGemronBake.Key((uint)fv.Item1, new[] { fv.Item2, 0, 0, 0 }))) };
            var frames = rc.GetProperty("lightFrames").EnumerateArray().Select(e => ((uint)e[0].GetInt32(), e[1].GetDouble())).ToList();
            foreach (var t in emot.Tracks)                                             // the light on the Gemron's frames
            {
                if (!nw.ContainsKey((int)t.W0) || CameraChannels.Contains(t.W2) || t.Keyframes.Count == 0) continue;
                Func<MotTrack, double, double[]> at_ = t.W2 == 0x32 || t.W2 == 0x33 ? Step : CrystalGemronBake.At;
                added.Add(Track(refTrack, nw[(int)t.W0], t.W1, t.W2, Thinned(frames.Select(fe => CrystalGemronBake.Key(fe.Item1, at_(t, fe.Item2))).ToList(), t.W2)));
            }
            double S = rc.GetProperty("lightScale").GetDouble(); double[] L = Vec(rc.GetProperty("lightT"));
            added.Add(Track(refTrack, lightAt, 0, 1, new[] { CrystalGemronBake.Key(0, new[] { S, S, S, 0 }), CrystalGemronBake.Key(TimelineEnd, new[] { S, S, S, 0 }) }));
            added.Add(Track(refTrack, lightAt, 0, 2, new[] { CrystalGemronBake.Key(0, new[] { L[0], L[1], L[2], 0 }), CrystalGemronBake.Key(TimelineEnd, new[] { L[0], L[1], L[2], 0 }) }));
            mot.Tracks.AddRange(added);
            pack.Require(Base + ".mot").ReplacePayload(mot.BuildPayload());
            var smot = MotFile.FromPack(pack, "e112s.mot"); DeathClip(smot); pack.Require("e112s.mot").ReplacePayload(smot.BuildPayload());
            Cfg(pack);
            // the sheets in the look; the atla's, the lens's, the light's (IMG banks: row-major → swizzled) and the wing lights' pictures
            ChrRecord img = pack.Require(Base + "01.img");
            var bank = new ImgBank(img.Payload);
            var look = SheetLook.Parse(ElfCaveWriter.Embedded(Look, $"{Look} is not embedded — the Atla Gemron's look"));
            var items = bank.Entries.Select(e => (e.name, e.name == Base + "01" ? look.ApplyToSheet(bank.Block(e.name), out _, out _)
                                                          : e.name == Base + "02" ? look.ApplyBaseToSheet(bank.Block(e.name)) : bank.Block(e.name))).ToList();
            var ab = new ImgBank(arc.Read(AtlaImg)); items.AddRange(ab.Entries.Select(e => (e.name, Tim8.Swizzled(ab.Block(e.name)))));
            var tb = new ImgBank(toan.Records.First(r => r.Name.EndsWith(".img", StringComparison.Ordinal)).Payload);
            items.Add((LensTex, Tim8.Swizzled(CropTim8(tb.Block(LensTex), LensCrop[0], LensCrop[1], LensCrop[2], LensCrop[3]))));
            var lb = new ImgBank(lp.Require(LightTex + ".img").Payload);
            items.Add((LightTex, lb.Swizzled ? lb.Block(LightTex) : Tim8.Swizzled(lb.Block(LightTex))));   // (an IM2 bank's is block-ordered already)
            items.Add((FlyTex, Desaturate(new ImgBank(fp.Require("fly_light.img").Payload).Block(FlyTex), FlySat)));
            img.ReplacePayload(ImgBank.Build(bank.Magic, items));
            return pack.Rebuild();
        }

        /// <summary>The shot's pack: the cfg, model and motion records named for the shot loader (<c>&lt;model&gt;.cfg</c>), the two sheets
        /// its meshes use as they are.</summary>
        private static byte[] ShotPackBytes(IsoArchive arc, JsonElement rc)
        {
            var p = ChrPack.Parse(arc.Read(LightChr));
            byte[] emds = p.Require(LightMds).Payload;
            var enodes = ModelCodec.ReadSkeleton(emds);
            var emot = MotFile.FromRecord(p.Require(LightMot));
            MotTrack refTrack = emot.Tracks[^1];
            int[] order = Ints(rc.GetProperty("shotNodes"));
            var drawn = new HashSet<int>(Ints(rc.GetProperty("shotMeshes")));
            var nw = new Dictionary<int, int>();
            for (int k = 0; k < order.Length; k++) nw[order[k]] = 2 + k;
            int etbl = (int)IsoBytes.U32(emds, 12);
            double[][] ident = { new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }, new double[] { 0, 0, 1 } };
            var raws = new List<byte[]> { PlainRecord(0, -1, ShotModel, ident, new double[] { 0, 0, 0 }), PlainRecord(1, 0, "fx_shot", Rows(rc.GetProperty("shotRows")), new double[] { 0, 0, 0 }) };
            var meshes = new Dictionary<int, byte[]>();
            foreach (int i in order)
            {
                RigNode n = enodes[i];
                raws.Add(CrystalGemronBake.Record(emds.AsSpan(etbl + i * 0x70, 0x70).ToArray(), nw[i], nw.TryGetValue(n.Parent, out int pp) ? pp : 1, null, null, 1.0));
                if (drawn.Contains(i)) meshes[nw[i]] = Mesh(emds, n.MeshOff);
            }
            var mds = new List<byte>(emds.AsSpan(0, 8).ToArray());
            mds.AddRange(BitConverter.GetBytes(raws.Count)); mds.AddRange(BitConverter.GetBytes(0x10));
            int bse = 0x10 + raws.Count * 0x70; var body = new List<byte>();
            for (int k = 0; k < raws.Count; k++)
            {
                byte[] raw = (byte[])raws[k].Clone();
                bool has = meshes.TryGetValue(k, out byte[] blob);
                IsoBytes.U32(raw, 0x28, has ? (uint)(bse + body.Count) : 0u);
                if (has) { body.AddRange(blob); body.AddRange(new byte[ExactMath.Mod(-body.Count, 16)]); }
                mds.AddRange(raw);
            }
            mds.AddRange(body);
            var tracks = new List<MotTrack>();
            foreach (var t in emot.Tracks)
            {
                if (!nw.ContainsKey((int)t.W0) || CameraChannels.Contains(t.W2) || t.Keyframes.Count == 0) continue;
                Func<MotTrack, double, double[]> at_ = t.W2 == 0x32 || t.W2 == 0x33 ? Step : CrystalGemronBake.At;
                var keys = new List<MotKeyframe> { CrystalGemronBake.Key(ShotFrom, at_(t, ShotFrom)) };
                keys.AddRange(t.Keyframes.Where(k => ShotFrom < k.Frame && k.Frame < ShotTo).Select(k => CrystalGemronBake.Key(k.Frame, CrystalGemronBake.Values(k))));
                keys.Add(CrystalGemronBake.Key(ShotTo, at_(t, ShotTo)));
                tracks.Add(Track(refTrack, nw[(int)t.W0], t.W1, t.W2, Thinned(keys, t.W2)));
            }
            // each drawn mesh out of the draw while it is clear (every mesh drawn takes room in the frame's VIF1 packet, seen or not: the
            // Crystal Gemron's lesson): a subtree-visibility key holds to the next, so a frame shows the mesh if it or the next one does
            var alphaOf = new Dictionary<int, MotTrack>();
            foreach (var t in emot.Tracks) if (t.W2 == 40) alphaOf[(int)t.W0] = t;
            foreach (int i in order)
            {
                if (!drawn.Contains(i) || !alphaOf.TryGetValue(i, out MotTrack a)) continue;
                var seen = Enumerable.Range(ShotFrom, ShotTo - ShotFrom + 1).Select(f => CrystalGemronBake.At(a, f)[0] < 0.98 || CrystalGemronBake.At(a, f + 1)[0] < 0.98).ToList();
                var keys = new List<MotKeyframe>();
                for (int k = 0; k < seen.Count; k++)
                    if (k == 0 || seen[k] != seen[k - 1]) keys.Add(CrystalGemronBake.Key((uint)(ShotFrom + k), new double[] { seen[k] ? 1 : 0, 0, 0, 0 }));
                if (keys[^1].Frame != ShotTo) keys.Add(CrystalGemronBake.Key(ShotTo, new double[] { seen[^1] ? 1 : 0, 0, 0, 0 }));
                tracks.Add(Track(refTrack, nw[i], 0, SubtreeVisible, keys));
            }
            double back = -rc.GetProperty("shotFlight").GetDouble() * (ShotTo - ShotFrom) / ShotSpeed;   // the flight over the window, in node 0's frame
            tracks.Add(Track(refTrack, 1, 0, 2, new[] { CrystalGemronBake.Key(ShotFrom, new double[] { 0, 0, 0, 0 }), CrystalGemronBake.Key(ShotTo, new double[] { 0, 0, back, 0 }) }));
            var lines = new List<string> { "//atla shot" };
            lines.AddRange(ShotTex.Select((n, k) => $"IMG {k},\"{n}.img\""));
            lines.AddRange(new[] { "IMG_END", "MATERIAL_ANIME 1", "VERTEX_ANIME 1", "", $"MODEL \"{ShotModel}.mds\"", "BODY_SIZE 17,7,60", "",
                                   $"MOTION 0, \"{ShotModel}.mot\", \"\", \"\"", "SHADOW_MOTION \"\", \"\", \"\"", "KEY_START 0",
                                   $"KEY\t{ShotFrom},\t{ShotTo},\t0.35,\t// 0", "MOTION_END", "" });
            var o = new ChrPack { Trailer = p.Trailer };
            o.Records.Add(ChrRecord.Create(ShotModel + ".cfg", Encoding.ASCII.GetBytes(string.Join("\r\n", lines))));
            o.Records.Add(ChrRecord.Create(ShotModel + ".mds", mds.ToArray()));
            o.Records.Add(ChrRecord.Create(ShotModel + ".mot", new MotFile { Tracks = tracks }.BuildPayload()));
            foreach (string n in ShotTex) o.Records.Add(p.Require(n + ".img"));
            return o.Rebuild();
        }

        private static byte[] Mesh(byte[] mds, int off) => mds.AsSpan(off, (int)IsoBytes.U32(mds, off + 8)).ToArray();

        private static MotTrack Track(MotTrack refTrack, int node, uint w1, uint chan, IEnumerable<MotKeyframe> keys)
        {
            var t = new MotTrack { W0 = (uint)node, W1 = w1, W2 = chan, W3 = refTrack.W3, W6 = refTrack.W6, W7 = refTrack.W7 };
            t.Keyframes.AddRange(keys);
            return t;
        }

        /// <summary>A node record of the mod's own: its index, size, name and parent, the rows <paramref name="R"/> and position <paramref name="T"/>.</summary>
        private static byte[] PlainRecord(int index, int parent, string name, double[][] R, double[] T)
        {
            var rec = new byte[0x70];
            IsoBytes.U32(rec, 0, (uint)index); IsoBytes.U32(rec, 4, 0x70);
            Encoding.ASCII.GetBytes(name).CopyTo(rec, 0x08);
            IsoBytes.U32(rec, 0x2C, unchecked((uint)parent));
            for (int r = 0; r < 3; r++) { for (int c = 0; c < 3; c++) IsoBytes.WrF(rec, 0x30 + r * 16 + c * 4, (float)R[r][c]); IsoBytes.WrF(rec, 0x30 + r * 16 + 12, 0f); }
            for (int c = 0; c < 3; c++) IsoBytes.WrF(rec, 0x60 + c * 4, (float)T[c]);
            IsoBytes.WrF(rec, 0x6C, 1f);
            return rec;
        }

        /// <summary>A stepped track's value at a frame: the last key on or before it (the first before them all).</summary>
        private static double[] Step(MotTrack tr, double frame)
        {
            MotKeyframe k = tr.Keyframes[0];
            foreach (var kk in tr.Keyframes) if (kk.Frame <= frame) k = kk;
            return CrystalGemronBake.Values(k);
        }

        /// <summary>The keys a track keeps: from the first, each next one as far on as the straight blend to it reproduces every key between
        /// within <see cref="Thin"/>'s tolerance (x, y and z, or the alpha); a channel it does not name keeps them all.</summary>
        private static List<MotKeyframe> Thinned(List<MotKeyframe> keys, uint chan)
        {
            if (!Thin.TryGetValue(chan, out double tol)) return keys;
            int n = chan == 40 ? 1 : 3;
            var outp = new List<MotKeyframe> { keys[0] };
            int i = 0;
            while (i < keys.Count - 1)
            {
                int j = i + 1;
                while (j + 1 < keys.Count)
                {
                    double[] va = CrystalGemronBake.Values(keys[i]), vb = CrystalGemronBake.Values(keys[j + 1]);
                    double fa = keys[i].Frame, fb = keys[j + 1].Frame;
                    bool ok = true;
                    for (int m = i + 1; m <= j && ok; m++)
                    {
                        double[] vm = CrystalGemronBake.Values(keys[m]);
                        double t = (keys[m].Frame - fa) / (fb - fa);
                        for (int c = 0; c < n; c++) if (Math.Abs(va[c] + (vb[c] - va[c]) * t - vm[c]) > tol) { ok = false; break; }
                    }
                    if (!ok) break;
                    j++;
                }
                outp.Add(keys[j]); i = j;
            }
            return outp;
        }

        /// <summary>The vanilla death copied to DeathStart on, every track keyed on each frame of it, its last pose held to DeathEnd.</summary>
        private static void DeathClip(MotFile mot)
        {
            foreach (var tr in mot.Tracks)
            {
                if (tr.Keyframes[^1].Frame >= DeathStart) throw new IOException($"track {tr.W0}/{tr.W2} already reaches {tr.Keyframes[^1].Frame}");
                var made = new List<MotKeyframe>();
                for (int n = DeathStart; n <= DeathStart + ClipTo - ClipFrom; n++) made.Add(CrystalGemronBake.Key((uint)n, CrystalGemronBake.At(tr, n - DeathStart + ClipFrom)));
                made.Add(CrystalGemronBake.Key(DeathEnd, CrystalGemronBake.At(tr, ClipTo)));
                made.Add(CrystalGemronBake.Key(TimelineEnd, CrystalGemronBake.At(tr, ClipTo)));
                tr.Keyframes.AddRange(made);
            }
        }

        /// <summary>Keys: the death on the copy, its loop on the copy's last frame; material animation on (the light's fades).</summary>
        private static void Cfg(ChrPack pack)
        {
            ChrRecord rec = pack.Require("info.cfg");
            Encoding sjis = Encoding.GetEncoding(932);
            string[] cfg = sjis.GetString(rec.Payload).Split('\n');
            int k = -1;
            for (int i = 0; i < cfg.Length; i++)
            {
                string line = cfg[i], s = line.TrimStart();
                if (s.StartsWith("MATERIAL_ANIME")) cfg[i] = "MATERIAL_ANIME 1\r";
                if (s.StartsWith("KEY_START")) { k = 0; continue; }
                if (k >= 0 && s.StartsWith("KEY") && !s.StartsWith("KEY_"))
                {
                    string comment = line.Contains("//") ? line.Substring(line.IndexOf("//", StringComparison.Ordinal)) : "\r";
                    if (k == DeathKey) cfg[i] = $"KEY\t{DeathStart},\t{DeathEnd},\t{DeathSpeed},\t{comment}";
                    if (k == DeathLoopKey) cfg[i] = $"KEY\t{DeathEnd},\t{DeathEnd},\t0.0,\t{comment}";
                    k++;
                }
            }
            rec.ReplacePayload(sjis.GetBytes(string.Join("\n", cfg)));
        }

        /// <summary>An 8-bit TIM2 (row-major) cut to the w × h patch at (x0, y0), the header but its sizes kept (the files' picture size =
        /// header + 4 × image), the CLUT whole.</summary>
        private static byte[] CropTim8(byte[] tim, int x0, int y0, int w, int h)
        {
            const int P = 0x10;
            int clutSz = (int)IsoBytes.U32(tim, P + 4), imgSz = (int)IsoBytes.U32(tim, P + 8), hs = IsoBytes.U16(tim, P + 0x0C), sw = IsoBytes.U16(tim, P + 0x14);
            var o = new byte[P + hs + w * h + clutSz];
            Array.Copy(tim, o, P + hs);
            for (int y = 0; y < h; y++) Array.Copy(tim, P + hs + (y0 + y) * sw + x0, o, P + hs + y * w, w);
            Array.Copy(tim, P + hs + imgSz, o, P + hs + w * h, clutSz);
            IsoBytes.U32(o, P, (uint)(hs + 4 * w * h)); IsoBytes.U32(o, P + 8, (uint)(w * h)); IsoBytes.U16(o, P + 0x14, (ushort)w); IsoBytes.U16(o, P + 0x16, (ushort)h);
            return o;
        }

        /// <summary>An 8-bit TIM2 with each palette colour moved toward its luminance (<paramref name="k"/> = the saturation left).</summary>
        private static byte[] Desaturate(byte[] tim, double k)
        {
            const int P = 0x10;
            byte[] b = (byte[])tim.Clone();
            int clut = P + IsoBytes.U16(b, P + 0x0C) + (int)IsoBytes.U32(b, P + 0x08);
            for (int e = 0; e < 256; e++)
            {
                int o = clut + e * 4;
                double lum = 0.299 * b[o] + 0.587 * b[o + 1] + 0.114 * b[o + 2];
                for (int c = 0; c < 3; c++) b[o + c] = (byte)Math.Max(0, Math.Min(255, Math.Round(lum + (b[o + c] - lum) * k)));
            }
            return b;
        }
    }
}
