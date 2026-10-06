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
    /// Bomb Gemron (<see cref="BombGemronStem"/>, from Holy Gemron e115a): the body sheet's CLUT desaturated and lightened except
    /// the entries kept as they are (eyes, claws, the underbelly's reds), the three gem spheres' meshes replaced by the thrown-bomb
    /// item model (dun\item\main_data\bakudan) scaled to each sphere and turned so its wick points as designed, the bomb's sheet
    /// added to the pack's bank; the glow overlays dropped. Its script is Holy Gemron's with the death path exploding — a
    /// <c>_SET_SHOT2</c> blast (shot slot 1) at the body once the death motion reaches frame 122 — and a self-destruct: the AI
    /// loop's head calls a function that, with HP under a quarter and the player within 22 units, plays the death motion at
    /// 0.25× and blows up at the same frame (the outlaws Sam / Billy / Mr. Blare's own pattern). The name goes into the empty
    /// message 3000 + species id of dunmsd00_1.mes in place.</summary>
    internal static class ModSpeciesBakes
    {
        internal const string BombGemronStem = "e167a";
        private const string Dir = @"dun\monstor\";
        private const string Source = "e115a", Orphan = "e147a";                   // Holy Gemron; the orphan entries repurposed (e147a.chr / .stb)
        private const string BombMds = @"dun\item\main_data\bakudan.mds", BombImg = @"dun\item\main_data\bakudan.img";
        private const string NameBank = @"dun\message\ww_mes\dunmsd00_1.mes";
        private const string Name = "Bomb Gemron";

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            foreach (string ext in new[] { ".chr", ".stb" })
            {
                string to = Dir + BombGemronStem + ext, from = Dir + Orphan + ext;
                if (arc.Has(to)) continue;
                if (!arc.Has(from)) throw new IOException($"neither {to} nor the orphan entry {from} is in the archive");
                arc.Rename(from, to);
            }
            byte[] chr = BombGemronPack(arc.Read(Dir + Source + ".chr"), arc.Read(BombMds), arc.Read(BombImg));
            arc.Redirect(Dir + BombGemronStem + ".chr", chr);
            log($"{BombGemronStem}.chr: {chr.Length:N0} B");
            byte[] stb = BombGemronScript(arc.Read(Dir + Source + ".stb"), out string why);
            arc.Redirect(Dir + BombGemronStem + ".stb", stb);
            log($"{BombGemronStem}.stb: {why}");
            int id = DungeonMessageBank.NameBase + EnemySpecies.BombGemron.Id;
            byte[] mes = arc.Read(NameBank);
            byte[] named = SetText(mes, id, WeaponDescriptions.Encode(Name).Concat(new ushort[] { 0xFF01 }).ToArray());
            if (named == null) log($"name: message {id} already reads '{Name}'");
            else { arc.Redirect(NameBank, named); log($"name: message {id} = '{Name}'"); }
        }

        // ───────────────────────────── the model ─────────────────────────────
        private const double BombRadius = 1.22;                                    // the bomb body's radius in its own model (the wick rises to 1.98)
        private static readonly double BodySpin = (90 - 200 - 45) * (Math.PI / 180.0);   // about the wick axis before it is aimed: positive = counter-clockwise from the wick's tip
        private static readonly double WingSpin = 180 * (Math.PI / 180.0);
        private static readonly double[] Forward = { 0.0, 0.0, 1.0 }, Up = { 0.0, 1.0, 0.0 };   // the rig at rest: head at +Z, Y up; its right is forward × up = −X
        private const double Sat = 0.9, Bri = 1.0, Con = 0.9, Light = 0.1;         // the sheet adjustment chosen on the preview page
        private static readonly HashSet<int> Keep = new() { 0, 22, 23, 24, 25, 39, 43, 44, 50, 51, 54, 56, 58, 59, 60, 62, 64, 65, 66, 67, 87, 90, 93, 94, 97, 109, 114, 115, 138, 139, 149, 173, 253, 254, 255 };
        private const string Mds = "e115a.mds", Img = "e115a01.img", Sheet = "e115a01";
        private const string BodyNode = "tama1__m";
        private static readonly string[] WingNodes = { "tamas00__m", "tamas03__m" }, GlowNodes = { "tama__appz", "tamas01__appz", "tamas02__appz" };

        private static byte[] BombGemronPack(byte[] srcChr, byte[] bombMds, byte[] bombImg)
        {
            var pack = ChrPack.Parse(srcChr);
            byte[] mds = pack.Require(Mds).Payload;
            var nodes = ModelCodec.ReadSkeleton(mds);
            var by = nodes.ToDictionary(n => n.Name);
            RigNode body = by[BodyNode];
            double[] right = Cross(Forward, Up);
            var replaced = new Dictionary<string, byte[]>();
            foreach (string g in GlowNodes) replaced[g] = null;
            // the body bomb: wick down, forward and to the right
            double[] dWorld = Unit(Add(Up.Select(c => -c).ToArray(), Forward, right));
            replaced[BodyNode] = BombMdt(bombMds, 3.5 / BombRadius, RotYTo(Unit(ToLocal(body, dWorld))), BodySpin);
            // the wing bombs: wick up, toward the body and forward
            foreach (string wn in WingNodes)
            {
                RigNode w = by[wn];
                double[] toBody = Unit(new[] { body.WorldPos[0] - w.WorldPos[0], body.WorldPos[1] - w.WorldPos[1], body.WorldPos[2] - w.WorldPos[2] });
                dWorld = Unit(Add(Up, toBody, Forward));
                replaced[wn] = BombMdt(bombMds, 1.3 / BombRadius, RotYTo(Unit(ToLocal(w, dWorld))), WingSpin);
            }
            pack.Require(Mds).ReplacePayload(ReplaceMeshes(mds, replaced));
            // the sheet recoloured, the bomb's sheet added to the bank
            var bank = new ImgBank(pack.Require(Img).Payload); var bbank = new ImgBank(bombImg);
            var items = bank.Entries.Select(e => (e.name, e.name == Sheet ? Recolor(bank.Block(e.name)) : bank.Block(e.name))).ToList();
            items.AddRange(bbank.Entries.Select(e => (e.name, bbank.Block(e.name))));
            pack.Require(Img).ReplacePayload(ImgBank.Build(bank.Magic, items));
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
        private static byte[] ReplaceMeshes(byte[] mds, Dictionary<string, byte[]> replaced)
        {
            int count = (int)IsoBytes.U32(mds, 8), tbl = (int)IsoBytes.U32(mds, 12);
            var nodesBlob = new List<byte>(); var meshes = new List<byte>();
            int bse = 0x10 + count * 0x70;
            for (int i = 0; i < count; i++)
            {
                byte[] raw = mds.AsSpan(tbl + i * 0x70, 0x70).ToArray();
                string name = IsoBytes.NameAt(raw, 8, 0x20);
                int off = (int)IsoBytes.U32(raw, 0x28);
                byte[] blob = replaced.TryGetValue(name, out byte[] nb) ? nb : off != 0 ? mds.AsSpan(off, (int)IsoBytes.U32(mds, off + 8)).ToArray() : null;
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

        /// <summary>The sheet's TIM2 with every CLUT entry but the kept ones adjusted (alpha as it was). The CLUT is stored in CSM1 order
        /// (index bits 3/4 swapped), so storage slot j holds pixel index unsw(j).</summary>
        private static byte[] Recolor(byte[] block)
        {
            const int pic = 0x10;
            int clutSz = (int)IsoBytes.U32(block, pic + 4), imgSz = (int)IsoBytes.U32(block, pic + 8), hdrSz = IsoBytes.U16(block, pic + 0x0C);
            int start = pic + hdrSz + imgSz;
            var o = (byte[])block.Clone();
            for (int j = 0; j < clutSz / 4; j++)
            {
                int k = (j & ~0x18) | ((j & 0x08) << 1) | ((j & 0x10) >> 1);
                if (Keep.Contains(k)) continue;
                int p = start + j * 4;
                var (r, g, b) = Adjust(o[p], o[p + 1], o[p + 2]);
                o[p] = r; o[p + 1] = g; o[p + 2] = b;
            }
            return o;
        }

        /// <summary>One colour through the saturation / contrast / brightness / lightness setting — the preview page's slider maths.</summary>
        private static (byte r, byte g, byte b) Adjust(byte r, byte g, byte b)
        {
            double l = 0.299 * r / 255 + 0.587 * g / 255 + 0.114 * b / 255;
            var o = new byte[3];
            double[] cs = { r / 255.0, g / 255.0, b / 255.0 };
            for (int i = 0; i < 3; i++)
            {
                double c = cs[i];
                c = l + (c - l) * Sat;
                c = (c - 0.5) * Con + 0.5;
                c = c * Bri + Light;
                o[i] = (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, c)) * 255, MidpointRounding.ToEven);
            }
            return (o[0], o[1], o[2]);
        }

        // vectors (row convention; sums as the Python builder's, compensated)
        private static double[] Unit(double[] v)
        {
            double n = Math.Sqrt(ExactMath.Sum(v.Select(c => c * c)));
            if (n == 0) n = 1.0;
            return v.Select(c => c / n).ToArray();
        }
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double Dot(double[] a, double[] b) => ExactMath.Sum(a.Zip(b, (x, y) => x * y));
        private static double[] Add(params double[][] vs) => Enumerable.Range(0, 3).Select(c => ExactMath.Sum(vs.Select(v => v[c]))).ToArray();
        private static double[] Mul(double[][] m, double[] v) => Enumerable.Range(0, 3).Select(r => ExactMath.Sum(Enumerable.Range(0, 3).Select(c => m[r][c] * v[c]))).ToArray();

        /// <summary>The rotation (3×3, rows) taking +Y onto unit vector d (Rodrigues).</summary>
        private static double[][] RotYTo(double[] d)
        {
            double[] y = { 0.0, 1.0, 0.0 }; d = Unit(d);
            double c = Math.Max(-1.0, Math.Min(1.0, Dot(y, d))); double[] axis = Cross(y, d); double s = Math.Sqrt(Dot(axis, axis));
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
        private const uint CmpEq = 40, CmpGt = 44, CmpGe = 45;
        private const uint FnGetDistance = 10, FnGetPosition = 11, FnSetMoveCancel = 34, FnSetMuteki = 101, FnSetAlpha = 102, FnSetDead = 104,
                           FnGetLifeRate = 109, FnSetMotion = 200, FnChkMotionFrm = 201, FnSetShot2 = 229;
        private const int DeathMotion = 11, HeaderBytes = 56;
        private const float BlastFrame = 122f, BlastHeight = 11f, BlastDamage = 150f, SelfDestructSpeed = 0.25f, SelfDestructHp = 25f, SelfDestructRange = 22f;
        private const uint SelfDestructMuteki = 1600, FadeHeader = 0;                // func 0x60 (the fade-out), header offset codeBase-relative
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
            int waitHdr = o2.Count; o2.AddRange(Header(waitHdr + HeaderBytes - cb, locals: 9, args: 2));
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
            IsoBytes.U32(outb, deathCall + 8, (uint)(newDeathHdr - cb));
            IsoBytes.U32(outb, loopHead, OpCall); IsoBytes.U32(outb, loopHead + 4, 0); IsoBytes.U32(outb, loopHead + 8, (uint)(selfHdr - cb));
            why = $"death → blast wait @+0x{waitHdr - cb:X} (frame {BlastFrame:g}), self-destruct @+0x{selfHdr - cb:X} from the AI loop head @0x{loopHead:X}, {stb.Length:N0}→{outb.Length:N0} B";
            return outb;
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

        /// <summary>v0 = cry sound, v1 = its frame (the arguments); v2 cried, v3 motion done, v4 frame, v5 blown, v6–v8 position.</summary>
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
            c.Var(5, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "loop");
            c.Var(4, VFloat); c.Float(BlastFrame); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "loop");
            Blast(c, strOff, 6, 7, 8);
            c.Set(5, VInt, () => c.Int(1));
            c.Branch(OpJmp, "loop");
            c.Mark("end");
            c.Ret(0);
            return c;
        }

        /// <summary>v0 HP rate, v1 distance, v2 motion done, v3 blown, v4 frame, v5–v7 position. Returns 1 (the loop head's own value)
        /// unless it blew up, then 0.</summary>
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
            c.Int((int)FnSetMuteki); c.Int((int)SelfDestructMuteki); c.Ext(2);
            c.Int((int)FnSetMotion); c.Int(DeathMotion); c.Float(SelfDestructSpeed); c.Int(2); c.Int(4); c.Add(OpOr); c.Ext(4);
            c.Set(2, VInt, () => c.Int(0)); c.Set(3, VInt, () => c.Int(0));
            c.Mark("loop");
            c.Var(2, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "end");
            c.Add(OpYield);
            c.Int((int)FnChkMotionFrm); c.Ref(2, VInt); c.Ref(4, VFloat); c.Ext(3);
            c.Var(3, VInt); c.Int(0); c.Add(OpCmp, CmpEq); c.Branch(OpBrFalse, "loop");
            c.Var(4, VFloat); c.Float(BlastFrame); c.Add(OpCmp, CmpGe); c.Branch(OpBrFalse, "loop");
            Blast(c, strOff, 5, 6, 7);
            c.Set(3, VInt, () => c.Int(1));
            c.Int((int)FnSetAlpha); c.Float(0.5f); c.Ext(2);
            c.Branch(OpJmp, "loop");
            c.Mark("end");
            c.Float(FadeStep); c.Float(FadeChime); c.Add(OpCall, 0, (uint)fadeHdr); c.Add(OpDrop);
            c.Int((int)FnSetDead); c.Ext(1);
            c.Ret(0);
            return c;
        }

        /// <summary>_GET_POSITION(-1) into x/y/z, y raised BlastHeight, _SET_SHOT2('bcol0', x, y, z, BlastDamage).</summary>
        private static void Blast(Code c, int strOff, int x, int y, int z)
        {
            c.Int((int)FnGetPosition); c.Int(1); c.Add(OpNeg); c.Ref(x, VFloat); c.Ref(y, VFloat); c.Ref(z, VFloat); c.Ext(5);
            c.Set(y, VFloat, () => { c.Var(y, VFloat); c.Float(BlastHeight); c.Add(OpAdd); });
            c.Int((int)FnSetShot2); c.Str(strOff); c.Var(x, VFloat); c.Var(y, VFloat); c.Var(z, VFloat); c.Int((int)BlastDamage); c.Ext(6);
        }

        // ───────────────────────────── the name ─────────────────────────────
        /// <summary>Message <paramref name="id"/>'s text set in place: its index entry re-pointed at the end of the real text (the bank's
        /// trailing padding absorbs the words; the file keeps its size, so the pool it is carved into is unchanged). Null when the
        /// message already reads so.</summary>
        private static byte[] SetText(byte[] mes, int id, ushort[] words)
        {
            int cnt = IsoBytes.U16(mes, 0), idxEnd = 4 + cnt * 4, entry = -1;
            for (int i = 0; i < cnt; i++) if (IsoBytes.U16(mes, 4 + i * 4) == id) entry = 4 + i * 4;
            if (entry < 0) throw new IOException($"message {id} is not in the bank");
            int cur = 2 * (cnt + IsoBytes.U16(mes, entry + 2) + 1);
            bool same = true;
            for (int i = 0; i < words.Length && same; i++) same = cur + i * 2 + 1 < mes.Length && IsoBytes.U16(mes, cur + i * 2) == words[i];
            if (same) return null;
            int blobEnd = mes.Length;
            while (blobEnd > idxEnd && mes[blobEnd - 1] == 0) blobEnd--;
            int at = blobEnd + 16; at += at & 1;
            if (at + words.Length * 2 > mes.Length) throw new IOException($"no room for message {id} in the bank's padding");
            var o = (byte[])mes.Clone();
            IsoBytes.U16(o, entry + 2, (ushort)(at / 2 - cnt - 1));
            for (int i = 0; i < words.Length; i++) IsoBytes.U16(o, at + i * 2, words[i]);
            return o;
        }
    }
}
