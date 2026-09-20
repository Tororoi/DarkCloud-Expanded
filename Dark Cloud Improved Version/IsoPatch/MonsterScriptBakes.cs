using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Monster-script (dun/monstor/*.stb) changes baked into the disc. A script cannot grow in place (later
    /// labels and every jump target would shift), so a patch REPLACES an existing call block (PUSH cmd, PUSH args…, EXT)
    /// with `CALL_FUNC → appended function` + NOPs for the rest of the block, and appends the function (strings, a
    /// 56-byte funcdata, its code) at the end of the file — every pointer in the format is codeBase-relative, so appended
    /// data is addressable and nothing else moves (docs/stb-script-format.md).
    ///
    /// Hurt spheres — Blizzard (e65a) → Titan's four; Sam (e86a), Billy (e69a) → Mr. Blare's two; Minotaur Joe (c16a) →
    /// every hurt sphere armed for the Divine Beast cat (each block → its own function = the original _SET_BODY_COL
    /// verbatim + _SET_BODY_COL_PARA(0, 100) + _SET_BODY_COL_PARA(1, 2), then RET; ElfCatPatches.PatchCatSpherePercent
    /// reads that spare table for a Xiao-owned hit whose kick type equals entry [1]).
    /// Mimic wake = a GUARD, not invincibility — all 14 chest-mimics: the wake's `_STATUS_SET_MUTEKI(100)` is NOP'd and
    /// the init label gains `_SET_GUARD_FRAME(10, 28)` (King: 10, 27), the "appear" clip's frames.</summary>
    internal static class MonsterScriptBakes
    {
        private const uint FnSetBodyCol = 130, FnSetBodyColPara = 134, FnStatusSetMuteki = 101, FnSetGuardFrame = 244;
        private const uint OpPushConst = 3, OpRet = 15, OpCallFunc = 19, OpExt = 21, OpNop = 22;
        private static readonly byte[] Probe = Encoding.ASCII.GetBytes("DCE-SPHERES-v1\0");   // appended marker: the idempotency check
        private const int CatKick = 2;                                                         // the cat's kick type (+0x98); pellets are 0
        private static readonly string[] Mimics = { "e35a", "e37a", "e39a", "e79a", "e81a", "e83a", "e109a" };       // appear clip 10..28
        private static readonly string[] KingMimics = { "e34a", "e36a", "e38a", "e78a", "e80a", "e82a", "e110a" };   // appear clip 10..27

        private sealed class Spec
        {
            internal string Who, Mode;                       // replace | arm | guard_wake
            internal (string name, float r)[] Spheres;       // replace: the new hurt-sphere set
            internal (uint cmd, object a, object b)[] Para;  // arm: appended per block
            internal (float a, float b) Guard;               // guard_wake
        }

        private static IEnumerable<(string file, Spec spec)> Patches()
        {
            yield return (@"dun\monstor\e65a.stb", new Spec { Who = "Blizzard → Titan", Mode = "replace", Spheres = new[] { ("bcol0", 5.0f), ("bcol1", 10.6f), ("bcol2", 6.0f), ("bcol3", 6.0f) } });
            yield return (@"dun\monstor\e86a.stb", new Spec { Who = "Sam → Mr. Blare", Mode = "replace", Spheres = new[] { ("bcol0", 8.5f), ("bcol1", 6.5f) } });
            yield return (@"dun\monstor\e69a.stb", new Spec { Who = "Billy → Mr. Blare", Mode = "replace", Spheres = new[] { ("bcol0", 8.5f), ("bcol1", 6.5f) } });
            yield return (@"dun\monstor\c16a.stb", new Spec { Who = "Minotaur Joe: every hurt sphere cat-hittable", Mode = "arm", Para = new (uint, object, object)[] { (FnSetBodyColPara, 0, 100), (FnSetBodyColPara, 1, CatKick) } });
            foreach (string c in Mimics) yield return ($@"dun\monstor\{c}.stb", new Spec { Who = $"Mimic {c}: wake guards instead of 100 invincibility frames", Mode = "guard_wake", Guard = (10f, 28f) });
            foreach (string c in KingMimics) yield return ($@"dun\monstor\{c}.stb", new Spec { Who = $"King Mimic {c}: wake guards instead of 100 invincibility frames", Mode = "guard_wake", Guard = (10f, 27f) });
        }

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            int done = 0;
            foreach (var (file, spec) in Patches())
            {
                byte[] src = arc.Read(file);
                var (patched, why) = PatchScript(src, spec);
                if (patched == null) { log($"{file}: {why} — skipped"); continue; }
                log($"patched {why}");
                arc.Redirect(file, patched);
                done++;
            }
            log($"monster scripts: {done} redirected");
        }

        private static uint U(byte[] b, int o) => IsoBytes.U32(b, o);

        /// <summary>Every `cmd(...)` block: (file offset, instruction count, args) — the PUSHes (cmd id + args) and the EXT
        /// that consumes them; args decoded as int / float / string, null for a non-constant push.</summary>
        private static List<(int pos, int n, object[] args)> CallBlocks(byte[] stb, uint cmd)
        {
            int cb = (int)U(stb, 8);
            var outp = new List<(int, int, object[])>();
            for (int i = cb; i + 12 <= stb.Length; i += 4)
            {
                if (U(stb, i) != OpExt || U(stb, i + 8) != 0) continue;
                int argc = (int)U(stb, i + 4);
                if (argc < 2 || argc > 10) continue;
                int fpos = i - argc * 12;
                if (fpos < cb || U(stb, fpos) != OpPushConst || U(stb, fpos + 4) != 1 || U(stb, fpos + 8) != cmd) continue;
                var args = new object[argc - 1];
                for (int k = 1; k < argc; k++)
                {
                    int ap = fpos + k * 12;
                    uint op = U(stb, ap), typ = U(stb, ap + 4), val = U(stb, ap + 8);
                    if (op == OpPushConst && typ == 3) args[k - 1] = IsoBytes.NameAt(stb, cb + (int)val, stb.Length - cb - (int)val);
                    else if (op == OpPushConst && typ == 2) args[k - 1] = BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
                    else if (op == OpPushConst && typ == 1) args[k - 1] = (int)val;
                    else args[k - 1] = null;
                }
                outp.Add((fpos, argc + 1, args));
            }
            return outp;
        }

        private static byte[] Ins(uint op, uint a1 = 0, uint a2 = 0)
        {
            var b = new byte[12]; IsoBytes.U32(b, 0, op); IsoBytes.U32(b, 4, a1); IsoBytes.U32(b, 8, a2); return b;
        }
        private static uint Bits(float f) => BitConverter.ToUInt32(BitConverter.GetBytes(f), 0);
        private static byte[] Push(object v) => v is float f ? Ins(OpPushConst, 2, Bits(f)) : Ins(OpPushConst, 1, unchecked((uint)(int)v));
        private static byte[] FuncData(int codeOffFromCb) { var b = new byte[56]; IsoBytes.U32(b, 0, (uint)codeOffFromCb); return b; }   // fd[0] entry, no locals, no args
        private static byte[] ParaCode(IEnumerable<(uint cmd, object a, object b)> para)
            => Cat(para.Select(p => Cat(Ins(OpPushConst, 1, p.cmd), Push(p.a), Push(p.b), Ins(OpExt, 3, 0))).ToArray());
        private static byte[] Cat(params byte[][] parts) { var ms = new MemoryStream(); foreach (var p in parts) ms.Write(p, 0, p.Length); return ms.ToArray(); }
        private static byte[] Nops(int n) => Cat(Enumerable.Repeat(Ins(OpNop), n).ToArray());
        private static void Pad4(List<byte> o) { while (o.Count % 4 != 0) o.Add(0); }
        private static void Overwrite(List<byte> o, int at, byte[] data) { for (int i = 0; i < data.Length; i++) o[at + i] = data[i]; }

        private static (byte[] patched, string why) PatchScript(byte[] stb, Spec spec)
        {
            if (IsoBytes.Find(stb, Probe) >= 0) return (null, "already patched");
            int cb = (int)U(stb, 8);
            if (spec.Mode == "guard_wake") return PatchGuardWake(stb, spec, cb);
            var blocks = CallBlocks(stb, FnSetBodyCol).Select(b => (b.pos, b.n, name: b.args.OfType<string>().FirstOrDefault())).ToList();
            if (blocks.Count == 0) return (null, "no _SET_BODY_COL block found");
            var o = new List<byte>(stb);
            Pad4(o);
            if (spec.Mode == "replace")
            {
                var strOff = new Dictionary<string, int>();
                foreach (var (name, _) in spec.Spheres)
                    if (!strOff.ContainsKey(name)) { strOff[name] = o.Count - cb; o.AddRange(Encoding.ASCII.GetBytes(name)); o.Add(0); }
                o.AddRange(Probe);
                Pad4(o);
                int fdOff = o.Count;
                o.AddRange(FuncData(fdOff + 56 - cb));
                foreach (var (name, r) in spec.Spheres)
                    o.AddRange(Cat(Ins(OpPushConst, 1, FnSetBodyCol), Ins(OpPushConst, 3, (uint)strOff[name]), Ins(OpPushConst, 2, Bits(r)), Ins(OpExt, 3, 0)));
                o.AddRange(Ins(OpRet));
                var (fpos, n, _) = blocks[0];
                Overwrite(o, fpos, Cat(Ins(OpCallFunc, 0, (uint)(fdOff - cb)), Nops(n - 1)));
                foreach (var (fpos2, n2, _) in blocks.Skip(1)) Overwrite(o, fpos2, Nops(n2));
                return (o.ToArray(), $"{spec.Who}: {blocks.Count} block(s) → one function @+0x{fdOff - cb:X} ({spec.Spheres.Length} spheres), {stb.Length:N0}→{o.Count:N0} B");
            }
            // arm: one function per block — the block verbatim (its string offsets stay valid), our params, RET
            o.AddRange(Probe);
            Pad4(o);
            var funcs = new List<string>();
            foreach (var (fpos, n, name) in blocks)
            {
                int fdOff = o.Count;
                o.AddRange(Cat(FuncData(fdOff + 56 - cb), stb.AsSpan(fpos, n * 12).ToArray(), ParaCode(spec.Para), Ins(OpRet)));
                Overwrite(o, fpos, Cat(Ins(OpCallFunc, 0, (uint)(fdOff - cb)), Nops(n - 1)));
                funcs.Add($"{name}@+0x{fdOff - cb:X}");
            }
            return (o.ToArray(), $"{spec.Who}: {blocks.Count} block(s) → {string.Join(", ", funcs)} ({spec.Para.Length} params each), {stb.Length:N0}→{o.Count:N0} B");
        }

        private static bool Is100(object[] args) => args.Length == 1 && ((args[0] is int i && i == 100) || (args[0] is float f && f == 100f));

        private static (byte[] patched, string why) PatchGuardWake(byte[] stb, Spec spec, int cb)
        {
            var muteki = CallBlocks(stb, FnStatusSetMuteki).Where(b => Is100(b.args)).ToList();
            if (muteki.Count == 0) return (null, "no _STATUS_SET_MUTEKI(100) block found");
            var anchors = CallBlocks(stb, FnSetGuardFrame);
            if (anchors.Count == 0) anchors = CallBlocks(stb, FnSetBodyCol);
            if (anchors.Count == 0) return (null, "no init-label block to hang the guard frame on");
            var (fpos, n, _) = anchors[0];
            var o = new List<byte>(stb);
            Pad4(o);
            o.AddRange(Probe);
            Pad4(o);
            int fdOff = o.Count;
            var (a, b) = spec.Guard;
            o.AddRange(Cat(FuncData(fdOff + 56 - cb), stb.AsSpan(fpos, n * 12).ToArray(), ParaCode(new[] { (FnSetGuardFrame, (object)a, (object)b) }), Ins(OpRet)));
            Overwrite(o, fpos, Cat(Ins(OpCallFunc, 0, (uint)(fdOff - cb)), Nops(n - 1)));
            foreach (var (mpos, mn, _) in muteki) Overwrite(o, mpos, Nops(mn));
            return (o.ToArray(), $"{spec.Who}: {muteki.Count} MUTEKI(100) block(s) NOP'd, guard frames {a:g}..{b:g} added via the block @0x{fpos:X} → function @+0x{fdOff - cb:X}, {stb.Length:N0}→{o.Count:N0} B");
        }
    }
}
