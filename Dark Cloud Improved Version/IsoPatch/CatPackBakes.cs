using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Divine Beast Title "cat shot": the cat as a character pack of its own (<see cref="CatPackChr"/>, a claimed orphan entry),
    /// which the cat loader (tools/stubs/cat_load.s) loads into dungeon chara slot 1 when a weapon with a cat look is drawn on a floor.
    /// It is the s86 town cat rig (gedit\s86\chara\c04cat.chr) with its nodes named `catroot` / `cat_*`, its clips trimmed to the
    /// nine KEY windows (ids <see cref="KeyStart"/>..) with the town cat's float-up spliced in, and on top Dran's wings, the Super
    /// Steve cape and the mask (CatWingGraft / CatCapeBake / CatMaskBake, encoded by <see cref="CatGraftEncoder"/>), a flat white wing
    /// texture, the flat cape texture and its own copy of the 8-bit element glow disc (<see cref="CatGlowName"/>). Xiao's own pack keeps
    /// only that disc (GlowDisc), which Matador, Big Bang and the Solar Shot draw. Everything is read from the player's own ISO.</summary>
    internal static class CatPackBakes
    {
        internal const string HostChr = @"dun\mainchara\c04b.chr";
        internal const string HostCfg = "base.cfg", HostMds = "c04b.mds", HostBbp = "c04b.bbp", HostImg = "c04b01.img";
        internal const string CatChr = @"gedit\s86\chara\c04cat.chr";
        internal const string FloatChr = @"gedit\e01\chara\e04c04cat.chr";        // the town cat's vertical float/hop-up (its #5 clip, frames 160..169)
        private static readonly (uint lo, uint hi) FloatSrc = (160, 169), FloatDst = (285, 294);   // grafted into cat.mot where s86 has no keys
        /// <summary>The per-ELEMENT glow (GlowDisc): ONE 8-bit disc (5,184 B) under this name, whose CLUT the palette cave repaints.</summary>
        internal const string GlowT8Name = "catglowp";
        /// <summary>The cat's own copy of that disc, in its pack: it loads into the cat's reserved VRAM span with the rest of its textures,
        /// where nothing else uploads — her block's pages are reused for her own textures while she draws, so a glow bound to her copy
        /// samples whatever replaced it.</summary>
        internal const string CatGlowName = "catglowc";
        internal const string CapeCloName = "catcape.clo";
        internal const string DranChr = @"dun\monstor\c12a.chr";                   // the wing donor
        private static readonly byte[] WingRgba = { 255, 255, 255, 0x80 };          // the wings' flat texture: solid white, GS alpha 0x80 = opaque
        private static readonly byte[] CapeRgba = { 128, 28, 0, 0x80 };             // the cape's flat texture; the colour on screen comes from the ambient the runtime gives the cloth
        internal const string NodePrefix = "cat_";                                  // every cat bone (her rig already carries `kao`, `skin`, …)
        internal const string CatRootName = "catroot";                              // what the runtime looks for in her tree
        internal const string CatSkinName = NodePrefix + "skin";
        internal const double HideScale = 0.001;                                    // bind 3×3 of the cat root on HER (the copy restores 1.0)
        private const int CatParent = -1;                                           // UNPARENTED: LoadMDSFile calls SetParent(frame, NULL) for parent < 0
        private const string VersionMark = "//catpack v20 standalone";
        internal const int KeyStart = 64;                                           // cat channel key ids 64.. (her own ids end at 45)
        internal static readonly (int s, int e, double sp, string cm)[] CatKeys =    // s86 c04cat windows; ids = KeyStart + index
        {
            (10, 20, 0.1, "cat stand"), (95, 105, 0.4, "cat ready"), (120, 136, 0.5, "cat run"), (190, 204, 0.5, "cat take-off"),
            (205, 214, 0.5, "cat leap"), (215, 227, 0.36, "cat land"), (60, 80, 1.0, "cat walk (s86 KEY 2, brisk)"),
            (285, 294, 0.6, "cat float-up (e04c04cat #5 160..169)"),               // 71: the vertical leap, as the town ladder jump
            (30, 40, 0.1, "cat sit (s86 KEY 1)"),                                    // 72: in place when there is no enemy to go for
        };
        // The vanilla DATA.HD2 records (USA disc): an ISO patched by the earlier weapon-pack version is put back before the bake.
        private static readonly (uint, uint, uint, uint) HostVanilla = (0x195D5800, 0x218220, 0x32BAB, 0x431);
        private static readonly (string name, (uint, uint, uint, uint) rec)[] WeaponRevert =
        {
            (@"commenu\weapon\c04w09.chr", (0x17275000, 0x90E0, 0x2E4EA, 0x13)),
            (@"commenu\c04wtes.chr", (0x168A800, 0x8A950, 0x2D15, 0x116)),
            (@"dun\mainchara\c04w.pac", (0x1985B800, 0x641A0, 0x330B7, 0xC9)),
        };

        // ───────────────────────────── pack helpers ─────────────────────────────
        private static List<(string name, int meshOff, int parent)> MdsNodes(byte[] pl)
        {
            int cnt = (int)IsoBytes.U32(pl, 8);
            var outp = new List<(string, int, int)>();
            for (int i = 0; i < cnt; i++) { int p = 0x18 + i * 0x70; outp.Add((IsoBytes.NameAt(pl, p, 0x20), (int)IsoBytes.U32(pl, p + 0x20), BitConverter.ToInt32(pl, p + 0x24))); }
            return outp;
        }

        /// <summary>A size×size 8-bit TIM2 whose 256-colour CLUT is all `rgba`, built on a vanilla 8-bit picture's headers (the wings'
        /// and the cape's flat textures).</summary>
        private static byte[] FlatTim2(byte[] template, byte[] rgba, int size = 32)
        {
            var clut = new byte[256 * 4];
            for (int i = 0; i < 256; i++) Array.Copy(rgba, 0, clut, i * 4, 4);
            return Tim8.Build8(template, size, size, new byte[size * size], clut, "flat_tim2");
        }

        // ───────────────────────────── the graft ─────────────────────────────
        private static string CatName(string orig, int index) => index == 0 ? CatRootName : NodePrefix + orig;

        /// <summary>Every node of the cat rig appended to the host .mds: the cat root renamed and unparented with its bind 3×3 at HideScale, the
        /// rest prefixed with their parents re-based; the cat's MDT chunks land after the host's mesh block. Returns (payload, nb, K).</summary>
        private static (byte[] payload, int nb, int K) GraftMds(byte[] bp, byte[] spl)
        {
            var bnodes = MdsNodes(bp); var snodes = MdsNodes(spl);
            int nb = bnodes.Count, K = snodes.Count;
            var bnames = bnodes.Select(b => b.name).ToHashSet();
            if (bnames.Contains(CatRootName)) throw new IOException("cat rig already grafted into this pack (patched ISO used as the base?)");
            var clash = snodes.Select((n, k) => CatName(n.name, k)).Where(bnames.Contains).ToList();
            if (clash.Count > 0) throw new IOException("node name clash between host and cat rig: " + string.Join(", ", clash));
            if (IsoBytes.U32(bp, 0x14) != 0x70 || IsoBytes.U32(spl, 0x14) != 0x70) throw new IOException("unexpected .mds record stride");
            int mesh0 = 0x18 + nb * 0x70 - 8;
            int firstMesh = bnodes.Where(b => b.meshOff != 0).Min(b => b.meshOff);
            if (firstMesh != mesh0 || Encoding.ASCII.GetString(bp, mesh0, 4) != "MDT\0") throw new IOException("host .mds table/mesh overlap layout not as expected");
            int delta = K * 0x70;
            var table = bp.AsSpan(0, mesh0).ToArray().Concat(BitConverter.GetBytes(nb)).Concat(BitConverter.GetBytes(0x70)).ToArray();
            IsoBytes.U32(table, 8, (uint)(nb + K));
            for (int i = 0; i < nb; i++) { uint off = IsoBytes.U32(table, 0x18 + i * 0x70 + 0x20); if (off != 0) IsoBytes.U32(table, 0x18 + i * 0x70 + 0x20, off + (uint)delta); }
            var mblock = bp.AsSpan(mesh0).ToArray();
            int pStart = 0x18 + (nb + K) * 0x70 - 8 + mblock.Length;
            var recs = new List<byte>(); var chunks = new List<byte>();
            for (int k = 0; k < K; k++)
            {
                var (nm, smo, spar) = snodes[k];
                var rec = spl.AsSpan(0x18 + k * 0x70, 0x68).ToArray();
                Array.Clear(rec, 0, 0x20); var nb2 = Encoding.Latin1.GetBytes(CatName(nm, k)); Array.Copy(nb2, rec, Math.Min(nb2.Length, 0x20));
                if (spar < 0)
                {
                    if (k != 0) throw new IOException("cat rig has more than one root");
                    Array.Copy(BitConverter.GetBytes(CatParent), 0, rec, 0x24, 4);
                    for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) { int o = 0x28 + (r * 4 + c) * 4; IsoBytes.WrF(rec, o, (float)(IsoBytes.F32(rec, o) * HideScale)); }
                }
                else Array.Copy(BitConverter.GetBytes(nb + spar), 0, rec, 0x24, 4);
                if (smo != 0)
                {
                    if (Encoding.ASCII.GetString(spl, smo, 4) != "MDT\0") throw new IOException($"cat node {nm} mesh @0x{smo:X} is not an MDT chunk");
                    int csz = (int)IsoBytes.U32(spl, smo + 8);
                    IsoBytes.U32(rec, 0x20, (uint)(pStart + chunks.Count));
                    chunks.AddRange(spl.AsSpan(smo, csz).ToArray());
                    chunks.AddRange(new byte[ExactMath.Mod(-chunks.Count, 16)]);
                }
                recs.AddRange(rec); recs.AddRange(BitConverter.GetBytes(nb + k + 1)); recs.AddRange(BitConverter.GetBytes(0x70));
            }
            recs.RemoveRange(recs.Count - 8, 8);
            return (table.Concat(recs).Concat(mblock).Concat(chunks).ToArray(), nb, K);
        }

        /// <summary>`extra` = (name, parent index ABSOLUTE in the payload's table, local16, mdt or null) appended to an .mds payload: the records go
        /// after the last node, every existing mesh offset shifts by the inserted records, the new MDTs land after the existing mesh block.</summary>
        private static byte[] GraftExtraNodes(byte[] payload, List<(string name, int par, double[] local16, byte[] mdt)> extra)
        {
            int nOld = (int)IsoBytes.U32(payload, 8);
            if (IsoBytes.U32(payload, 0x14) != 0x70) throw new IOException("mds stride");
            int mesh0 = 0x18 + nOld * 0x70 - 8, delta = extra.Count * 0x70;
            var head = payload.AsSpan(0, mesh0).ToArray().Concat(BitConverter.GetBytes(nOld)).Concat(BitConverter.GetBytes(0x70)).ToArray();
            IsoBytes.U32(head, 8, (uint)(nOld + extra.Count));
            for (int i = 0; i < nOld; i++) { uint mo = IsoBytes.U32(head, 0x18 + i * 0x70 + 0x20); if (mo != 0) IsoBytes.U32(head, 0x18 + i * 0x70 + 0x20, mo + (uint)delta); }
            var mblock = payload.AsSpan(mesh0).ToArray();
            int pStart = 0x18 + (nOld + extra.Count) * 0x70 - 8 + mblock.Length;
            var chunks = new List<byte>(); var recs = new List<byte>();
            for (int k = 0; k < extra.Count; k++)
            {
                var (name, par, local16, mdt) = extra[k];
                var nm = Encoding.ASCII.GetBytes(name);
                if (nm.Length > 0x1F) throw new IOException("node name too long: " + name);
                var rec = new byte[0x68];
                Array.Copy(nm, rec, nm.Length);
                IsoBytes.U32(rec, 0x20, (uint)(mdt != null ? pStart + chunks.Count : 0));
                Array.Copy(BitConverter.GetBytes(par), 0, rec, 0x24, 4);
                for (int i = 0; i < 16; i++) IsoBytes.WrF(rec, 0x28 + i * 4, (float)local16[i]);
                if (mdt != null) { chunks.AddRange(mdt); chunks.AddRange(new byte[ExactMath.Mod(-mdt.Length, 16)]); }
                recs.AddRange(rec); recs.AddRange(BitConverter.GetBytes(nOld + k + 1)); recs.AddRange(BitConverter.GetBytes(0x70));
            }
            var outp = head.Concat(recs).ToList();
            outp.RemoveRange(outp.Count - 8, 8);                                  // the last record's tail merges into the first MDT again
            return outp.Concat(mblock).Concat(chunks).ToArray();
        }

        /// <summary>Only the keyframes inside the clip windows (plus the bracketing key on each side) are kept. Bone ids stay RELATIVE to the cat root.</summary>
        private static void TrimTracks(MotFile mot, IEnumerable<(int lo, int hi)> windows)
        {
            foreach (var t in mot.Tracks)
            {
                var keys = t.Keyframes.OrderBy(k => k.Frame).ToList();
                var keep = new HashSet<int>();
                foreach (var (lo, hi) in windows)
                {
                    int before = -1, after = -1;
                    for (int i = 0; i < keys.Count; i++) { if (keys[i].Frame < lo) before = i; if (after < 0 && keys[i].Frame > hi) after = i; }
                    if (before >= 0) keep.Add(before);
                    if (after >= 0) keep.Add(after);
                    for (int i = 0; i < keys.Count; i++) if (lo <= keys[i].Frame && keys[i].Frame <= hi) keep.Add(i);
                }
                t.Keyframes = keep.Count > 0 ? keep.OrderBy(i => i).Select(i => keys[i]).ToList() : keys.Take(1).ToList();
            }
        }

        private static string Strip(string s) => s.Trim(' ', '\t', '\n', '\r', '\v', '\f');

        internal sealed class Report { internal int Nodes, Textures, MotKeys, VramBlocks, CatBytes, HostBytes; }

        /// <summary>The VRAM the pack's textures take in the slot's texture group, in 256-byte blocks — what the cat loader
        /// (tools/stubs/cat_load.s, via CodeCaves.CatLoad) reserves below the texture manager's cursor before the load, since a block
        /// may not grow past it. EnterTextureEX's own sum: per picture its pixels (bytes / 256, plus a quarter and a sixteenth for
        /// mipmaps) and a 4-block CLUT for an 8-bit one, rounded up to 32 blocks. The bake refuses a pack whose sum differs.</summary>
        internal const int CatVramBlocks = 768;

        /// <summary>The cat bake: the s86 cat rig as a pack of its own (<see cref="CatPackChr"/>), with the trimmed clips (keys
        /// <see cref="KeyStart"/>.., the float-up spliced in and baked at <see cref="DivineBeastTitle.FloatRate"/>), Dran's wings, the
        /// mask and the Super Steve cape on top; and Xiao's pack as vanilla plus the element glow disc, which Matador, Big Bang and the
        /// Solar Shot draw too. The wings, mask and cape are worked out exactly as before, on the cat grafted into a vanilla c04b.chr
        /// in memory (CatWingGraft reads that layout), then carried over cat-relative.</summary>
        internal static (byte[] cat, byte[] host, Report rep) Assemble(byte[] vanillaHost, byte[] catBytes, byte[] floatBytes, byte[] glowBytes, byte[] dranBytes, Action<string> log)
        {
            var bas = ChrPack.Parse(vanillaHost); var cat = ChrPack.Parse(catBytes); var flt = ChrPack.Parse(floatBytes); var glowPack = ChrPack.Parse(glowBytes);
            var glowImg = glowPack.Find("fire.img") ?? throw new IOException("glow source pack lacks fire.img");
            foreach (string n in new[] { "e04c04cat.mds", "e04c04cat.mot" }) if (flt.Find(n) == null) throw new IOException($"float-up pack lacks {n}");
            foreach (string n in new[] { HostCfg, HostMds, HostBbp, HostImg }) if (bas.Find(n) == null) throw new IOException($"host pack lacks {n}");
            foreach (string n in new[] { "c04cat.mds", "c04cat.bbp", "c04cat.img", "c04cat.mot", "c04cat.wgt" }) if (cat.Find(n) == null) throw new IOException($"cat pack lacks {n}");
            byte[] spl = cat.Require("c04cat.mds").Payload, cb = cat.Require("c04cat.bbp").Payload;
            var cimg = new ImgBank(cat.Require("c04cat.img").Payload);

            // ── the clips: trimmed to the KEY windows, the float-up grafted by joint NAME into frames FloatDst ──
            var mot = MotFile.FromPack(cat, "c04cat.mot"); TrimTracks(mot, CatKeys.Select(k => (k.s, k.e)));
            var fmot = MotFile.FromPack(flt, "e04c04cat.mot");
            var frep = MotSplice.SpliceByJoint(mot, fmot, MotSplice.ReadMdsFrames(flt.Require("e04c04cat.mds").Payload), MotSplice.ReadMdsFrames(spl),
                                               FloatSrc.lo, FloatSrc.hi, FloatDst.lo, FloatDst.hi);
            if (frep.Written == 0) throw new IOException("float-up graft wrote no tracks");
            var wgt = MotFile.FromPack(cat, "c04cat.wgt");

            // ── the wings, mask and cape, worked out on the cat grafted into her pack (the layout CatWingGraft reads) ──
            var (graftMds, nb, K) = GraftMds(bas.Require(HostMds).Payload, spl);
            bas.Require(HostMds).ReplacePayload(graftMds);
            byte[] bb = bas.Require(HostBbp).Payload;
            if (bb.Length != nb * 64 || cb.Length != K * 64) throw new IOException(".bbp is not count*64 bytes");
            bas.Require(HostBbp).ReplacePayload(bb.Concat(cb).ToArray());
            bas.Records.Add(ChrRecord.Create("cat.mot", mot.BuildPayload())); bas.Records.Add(ChrRecord.Create("cat.wgt", wgt.BuildPayload())); bas.Records.Add(ChrRecord.Create("cat.bbp", cb));
            var wd = CatGraftEncoder.Build(dranBytes, bas.Rebuild(), nb, K, catBytes, log);

            // ── the cat's own pack ──
            var nodes = new List<(string name, int par, double[] local16, byte[] mdt)>();
            nodes.AddRange(wd.Nodes.Select(n => (n.Name, n.Parent, n.Local16, n.Mdt)));
            byte[] mds = GraftExtraNodes(RenameRig(spl), nodes);
            int anchor = MdsNodes(mds).FindIndex(n => n.name == CatCapeBake.CapeAnchor);
            if (anchor < 0) throw new IOException($"no {CatCapeBake.CapeAnchor} in the cat rig");
            var cape = wd.Cape;
            var ident = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            mds = GraftExtraNodes(mds, new List<(string, int, double[], byte[])> { (cape.Name, anchor, ident, cape.Mdt) });   // the cloth's frame: its anchor bone's own place
            var crow = new byte[64]; for (int i = 0; i < 16; i++) IsoBytes.WrF(crow, i * 4, (float)ident[i]);
            byte[] bbp = cb.Concat(wd.Bbp).Concat(crow).ToArray();
            var mot2 = new MotFile { Head = mot.Head, DataOff = mot.DataOff, Tracks = mot.Tracks.Concat(wd.MotTracks).ToList() };
            var wgt2 = new MotFile { Head = wgt.Head, DataOff = wgt.DataOff, Tracks = wgt.Tracks.Concat(wd.WgtTracks).ToList() };
            var items = cimg.Entries.Select(e => (e.name, cimg.Block(e.name))).ToList();                   // the real cat fur
            items.Add((wd.Texture, FlatTim2(cimg.Block("c04cat01"), WingRgba)));
            items.Add((cape.Texture, FlatTim2(cimg.Block("c04cat01"), CapeRgba)));                          // the mask shares it
            byte[] light = new ImgBank(glowImg.Payload).Block(GlowDisc.SourcePicture);
            byte[] disc = GlowDisc.BuildT8(cimg.Block("c04cat01"), light, GlowDisc.Elements[5].core, GlowDisc.Elements[5].outer);   // resting CLUT "None"
            items.Add((CatGlowName, disc));
            if (items.Select(x => x.Item1).Distinct().Count() != items.Count) throw new IOException("cat texture entry name clash");
            byte[] img = ImgBank.Build(cimg.Magic, items);
            string clo = Encoding.ASCII.GetString(cape.Clo);
            foreach (var b in CatCapeBake.CapeBounds)                                                       // each capsule on its own bone, in the .clo's order
            {
                int at = clo.IndexOf($"BOUND\t\"{cape.Name}\"", StringComparison.Ordinal);
                if (at < 0) throw new IOException("cape .clo has fewer BOUND lines than CapeBounds");
                clo = clo.Substring(0, at) + $"BOUND\t\"{b.Bone}\"" + clo.Substring(at + $"BOUND\t\"{cape.Name}\"".Length);
            }
            var pack = new ChrPack();
            pack.Records.Add(ChrRecord.Create(CatCfgName, Encoding.Latin1.GetBytes(CatCfg(wd))));
            pack.Records.Add(ChrRecord.Create(CatMdsName, mds));
            pack.Records.Add(ChrRecord.Create(CatImgName, img));
            pack.Records.Add(ChrRecord.Create(CatMotName, mot2.BuildPayload()));
            pack.Records.Add(ChrRecord.Create(CatBbpName, bbp));
            pack.Records.Add(ChrRecord.Create(CatWgtName, wgt2.BuildPayload()));
            pack.Records.Add(ChrRecord.Create(CapeCloName, Encoding.ASCII.GetBytes(clo)));
            pack.Trailer = new byte[0x50];
            byte[] catPack = pack.Rebuild();

            // ── her pack: vanilla, plus the one element glow disc (its resting CLUT is "None") ──
            var host = ChrPack.Parse(vanillaHost);
            var hbank = new ImgBank(host.Require(HostImg).Payload);
            var hitems = hbank.Entries.Select(e => (e.name, hbank.Block(e.name))).ToList();
            if (hitems.Any(x => x.Item1 == GlowT8Name)) throw new IOException("the vanilla host already holds a glow disc");
            hitems.Add((GlowT8Name, disc));
            host.Require(HostImg).ReplacePayload(ImgBank.Build(hbank.Magic, hitems));
            byte[] hostOut = host.Rebuild();

            int vram = VramBlocks(new ImgBank(img));
            var rep = new Report { Nodes = MdsNodes(mds).Count, Textures = items.Count, MotKeys = mot2.Tracks.Sum(t => t.Keyframes.Count), VramBlocks = vram, CatBytes = catPack.Length, HostBytes = hostOut.Length };
            VerifyCatPack(catPack, catBytes, wd, vram);
            return (catPack, hostOut, rep);
        }

        internal const string CatPackChr = @"dun\monstor\dbcat.chr";       // the cat's own pack (21 characters: the donor entry's length)
        private const string CatPackDonor = @"dun\monstor\e156a.chr";     // an orphan: a duplicate mimic container no species record names
        internal const string CatCfgName = "info.cfg", CatMdsName = "dbcat.mds", CatImgName = "dbcat.img", CatMotName = "dbcat.mot", CatBbpName = "dbcat.bbp", CatWgtName = "dbcat.wgt";

        /// <summary>The s86 rig's .mds with its nodes named as the runtime looks them up: the root `catroot`, the rest `cat_` + name.</summary>
        private static byte[] RenameRig(byte[] spl)
        {
            byte[] outp = (byte[])spl.Clone();
            var nodes = MdsNodes(spl);
            for (int k = 0; k < nodes.Count; k++)
            {
                int p = 0x18 + k * 0x70;
                Array.Clear(outp, p, 0x20);
                var nm = Encoding.Latin1.GetBytes(CatName(nodes[k].name, k));
                if (nm.Length > 0x1F) throw new IOException("cat node name too long: " + CatName(nodes[k].name, k));
                Array.Copy(nm, 0, outp, p, nm.Length);
                if (k == 0 && nodes[k].parent >= 0) throw new IOException("the cat rig's first node is not its root");
            }
            return outp;
        }

        /// <summary>The pack's cfg: its picture bank, the double-buffered (skinned) meshes and the cape's own lattice, the model, the cape
        /// cloth, and one motion channel whose keys start at <see cref="KeyStart"/> — the ids the pellet cave and the runtime play.</summary>
        private static string CatCfg(CatGraftEncoder.Result wd)
        {
            const string nl = "\r\n";
            var l = new List<string> { "//Divine Beast Title cat (CatPackBakes)", $"IMG 0,\"{CatImgName}\"", "IMG_END", "MATERIAL_ANIME 0", "VERTEX_ANIME 1", "SHADOW_VERTEX_ANIME 0", "",
                                       $"ALLOC_MDT \"{wd.Cape.Name}\"", $"ALLOC_DBUFF \"{CatSkinName}\"" };
            l.AddRange(wd.AllocDbuff.Select(nm => $"ALLOC_DBUFF \"{nm}\""));
            l.AddRange(new[] { $"MODEL \"{CatMdsName}\"", $"CLOTH \"{CapeCloName}\"", "BODY_SIZE 3,7,60", "",
                               $"MOTION 0, \"{CatMotName}\", \"{CatBbpName}\", \"{CatWgtName}\"", $"KEY_START {KeyStart}" });
            for (int i = 0; i < CatKeys.Length; i++)
            {
                var (s, e, sp, cm) = CatKeys[i];
                double rate = KeyStart + i == DivineBeastTitle.KeyFloat ? DivineBeastTitle.FloatRate : sp;   // the float-up holds its last frame at this rate
                l.Add($"\tKEY\t{s},\t{e},\t{ExactMath.Repr(rate)},\t\t//{KeyStart + i} {cm}");
            }
            l.Add("MOTION_END"); l.Add(VersionMark); l.Add("");
            return string.Join(nl, l);
        }

        /// <summary>EnterTextureEX's VRAM sum for a bank (see <see cref="CatVramBlocks"/>).</summary>
        internal static int VramBlocks(ImgBank bank)
        {
            int total = 0;
            foreach (var (name, _) in bank.Entries)
            {
                byte[] b = bank.Block(name);
                var (type, _, w, h) = Tim8.PictureInfo(b);
                int bpp = type switch { 5 => 1, 3 => 4, 2 => 3, _ => throw new IOException($"texture {name}: picture type {type} is not one EnterTextureEX enters") };
                int bytes = bpp * w * h, blocks = bytes >> 8;
                if (b[Tim8.Pic + 0x11] > 1) blocks += (bytes >> 10) + (bytes >> 12);
                if (bpp == 1) blocks += 4;
                total += (blocks + 0x1F) & ~0x1F;
            }
            return total;
        }

        /// <summary>Structural checks on the cat's pack.</summary>
        private static void VerifyCatPack(byte[] bytes, byte[] catBytes, CatGraftEncoder.Result wd, int vram)
        {
            void Check(bool ok, string what) { if (!ok) throw new IOException("cat pack verify: " + what); }
            var pk = ChrPack.Parse(bytes);
            Check(ChrPack.Parse(pk.Rebuild()).Rebuild().AsSpan().SequenceEqual(bytes), "pack round-trip");
            var cn = MdsNodes(ChrPack.Parse(catBytes).Require("c04cat.mds").Payload);
            byte[] mds = pk.Require(CatMdsName).Payload; var nn = MdsNodes(mds);
            int K = cn.Count, X = wd.Nodes.Count;
            Check(nn.Count == K + X + 1 && IsoBytes.U32(mds, 8) == K + X + 1, "node count");
            Check(nn[0].name == CatRootName && nn[0].parent < 0, "cat root");
            for (int k = 1; k < K; k++) Check(nn[k].name == NodePrefix + cn[k].name && nn[k].parent == cn[k].parent, $"cat node {k}");
            for (int k = 0; k < X; k++) Check(nn[K + k].name == wd.Nodes[k].Name && nn[K + k].parent == wd.Nodes[k].Parent && (nn[K + k].meshOff != 0) == (wd.Nodes[k].Mdt != null), $"wing node {wd.Nodes[k].Name}");
            var capeNode = nn[K + X];
            Check(capeNode.name == wd.Cape.Name && nn[capeNode.parent].name == CatCapeBake.CapeAnchor && capeNode.meshOff != 0, "cape node");
            Check(IsoBytes.U32(mds, capeNode.meshOff + 12) == wd.Cape.Rows * wd.Cape.Cols, "cape lattice vertex count");
            foreach (var (nm, mo, _) in nn) if (mo != 0) Check(Encoding.ASCII.GetString(mds, mo, 4) == "MDT\0", $"{nm} mesh magic");
            Check(pk.Require(CatBbpName).Size == nn.Count * 64, "bbp rows");
            foreach (string name in new[] { CatMotName, CatWgtName }) Check(MotFile.FromRecord(pk.Require(name)).Tracks.All(t => t.W0 < nn.Count), $"{name} track ids");
            string clo = Encoding.ASCII.GetString(pk.Require(CapeCloName).Payload);
            Check(CatCapeBake.CapeBounds.All(b => clo.Contains($"BOUND\t\"{b.Bone}\"")) && !clo.Contains($"BOUND\t\"{wd.Cape.Name}\""), "cape bounds name their bones");
            Check(clo.Contains($"FRAME\t\"{wd.Cape.Name}\""), "cape frame");
            string cfg = Encoding.Latin1.GetString(pk.Require(CatCfgName).Payload);
            Check(cfg.Contains($"KEY_START {KeyStart}") && cfg.Contains($"ALLOC_DBUFF \"{CatSkinName}\"") && wd.AllocDbuff.All(nm => cfg.Contains($"ALLOC_DBUFF \"{nm}\"")), "cfg");
            Check(cfg.IndexOf("ALLOC_MDT", StringComparison.Ordinal) < cfg.IndexOf("MODEL", StringComparison.Ordinal)
                  && cfg.IndexOf("MODEL", StringComparison.Ordinal) < cfg.IndexOf("CLOTH", StringComparison.Ordinal)
                  && cfg.IndexOf("CLOTH", StringComparison.Ordinal) < cfg.IndexOf("MOTION 0", StringComparison.Ordinal), "cfg order: ALLOC lines, MODEL, CLOTH, MOTION");
            var bank = new ImgBank(pk.Require(CatImgName).Payload);
            var names = bank.Entries.Select(e => e.name).ToHashSet();
            Check(new[] { "c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05", wd.Texture, wd.Cape.Texture, CatGlowName }.All(names.Contains) && !names.Contains(GlowT8Name), "cat textures");
            Check(vram == CatVramBlocks, $"the textures take {vram} VRAM blocks, not CatVramBlocks ({CatVramBlocks}) — set CatPackBakes.CatVramBlocks");
        }

        /// <summary>The glow disc in her pack, and no cat.</summary>
        private static bool HasCat(byte[] chr, string mdsName = HostMds)
        {
            try { return MdsNodes(ChrPack.Parse(chr).Require(mdsName).Payload).Any(n => n.name == CatRootName); }
            catch (Exception) { return false; }
        }

        /// <summary>The post-step: the earlier weapon-pack bake reverted if the ISO carries it; the cat's pack built from the ISO's own
        /// files into its claimed entry (<see cref="CatPackChr"/>) and her pack rebuilt from its vanilla record with the glow disc —
        /// both redirected into the DATA.DAT tail only when they differ from what the ISO holds. Idempotent.</summary>
        internal static void Run(IsoArchive arc, Action<string> log)
        {
            foreach (var (name, vanilla) in WeaponRevert)
            {
                var cur = arc.Slot(name);
                if (cur == vanilla) continue;
                bool isChr = name.EndsWith(".chr", StringComparison.Ordinal) && !name.Contains("wtes");
                byte[] van = arc.ReadAt(vanilla.Item1, (int)vanilla.Item2);
                bool ok;
                try
                {
                    var pk = ChrPack.Parse(van);
                    byte[] inner = isChr ? van : pk.Require("c04w09.chr").Payload;
                    ok = ChrPack.Parse(inner).Find("c04w09.mds") != null && !HasCat(inner, "c04w09.mds");
                }
                catch (Exception) { ok = false; }
                if (!ok) throw new IOException($"{name}: record differs from vanilla and the vanilla bytes are not where expected — refusing to revert");
                arc.SetSlot(name, vanilla);
                log($"reverted {name} to its vanilla record (earlier weapon-pack bake removed)");
            }
            byte[] van0 = arc.ReadAt(HostVanilla.Item1, (int)HostVanilla.Item2);
            if (HasCat(van0) || ChrPack.Parse(van0).Find(HostMds) == null) throw new IOException("c04b.chr's vanilla bytes are not where expected — refusing");
            arc.Claim(CatPackDonor, CatPackChr);
            var (catPack, hostOut, rep) = Assemble(van0, arc.Read(CatChr), arc.Read(FloatChr), arc.Read(GlowDisc.SourcePack), arc.Read(DranChr), log);
            log($"Divine Beast cat pack: {rep.Nodes} nodes, {rep.Textures} textures ({rep.VramBlocks} VRAM blocks), {rep.MotKeys:N0} keys, {rep.CatBytes:N0} B; her pack {rep.HostBytes:N0} B with the glow disc");
            if (arc.Read(CatPackChr).AsSpan().SequenceEqual(catPack)) log($"{CatPackChr} already current — skipped");
            else arc.Redirect(CatPackChr, catPack);
            if (arc.Read(HostChr).AsSpan().SequenceEqual(hostOut)) log($"{HostChr} already current — skipped");
            else arc.Redirect(HostChr, hostOut);
            log("DONE (Divine Beast Title cat pack)");
        }
    }
}
