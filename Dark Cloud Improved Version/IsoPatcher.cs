using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Creates a patched COPY of the user's stock Dark Cloud (USA) ISO with the fishing signs baked in, and
    /// publishes the matching pnach to the PCSX2 cheats folder. Cross-platform (macOS / Windows / Linux).
    ///
    /// C# port of tools/iso_patch/{build_sign_iso, sign_scene, ps2iso}.py — the PROVEN Python patcher. The
    /// patch only rearranges data already on the user's disc: absorb the trailing DMMY. padding into DATA.DAT,
    /// then redirect DATA.HD2 index entries at the freed tail (mes_tex.pak = boot texture, s04/scene.scn =
    /// native kanban part, s04/mapinfo.cfg = its placement), plus a tiny ELF boot-cave that registers the
    /// texture. Nothing game-derived is bundled — the sign mesh + texture are carved from the user's OWN ISO.
    /// </summary>
    internal static class IsoPatcher
    {
        internal const int    SECTOR      = 2048;
        internal const string OutputName  = "Dark Cloud - Expanded.iso";

        const string HOST_PAK   = "meswin/mes_tex.pak";
        const string SCENE_SCN  = "gedit/s04/scene.scn";
        const string MAPINFO    = "gedit/s04/mapinfo.cfg";
        const int    SIGN_X = 212, SIGN_Y = 9, SIGN_Z = -61, SIGN_RY = 0;

        // ELF boot-cave (register fishsign.img's e01b24 into 0x1c75870 at boot)
        const uint GetPackFile = 0x0013F720, EnterIMGFile = 0x00132BA0, LoadFile = 0x0013F360;
        const uint SysTexMgr = 0x01C75870, DETOUR_VA = 0x00180D7C, REJOIN_VA = 0x00180D84;
        const uint CAVE_VA = 0x002A2314, STR_VA = 0x002452B8, DIAG_VA = 0x01F80000;
        const int  CAVE_LEN = 0x6C;
        const string OLD_CRC = "A5C05C78";

        // ── PCSX2 cheats folder, per OS ──
        internal static string Pcsx2CheatsDir()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(home, "Library", "Application Support", "PCSX2", "cheats");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2", "cheats");
            string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(xdg)) xdg = Path.Combine(home, ".config");
            return Path.Combine(xdg, "PCSX2", "cheats");
        }

        internal static string Patch(string stockIso, string outDir, Action<string> progress)
        {
            if (string.IsNullOrWhiteSpace(stockIso) || !File.Exists(stockIso))
                throw new FileNotFoundException("Stock ISO not found. Select your Dark Cloud (USA) .iso first.");
            if (string.IsNullOrWhiteSpace(outDir)) outDir = Path.GetDirectoryName(stockIso);
            Directory.CreateDirectory(outDir);
            string outIso = Path.Combine(outDir, OutputName);
            if (Path.GetFullPath(outIso).Equals(Path.GetFullPath(stockIso), StringComparison.OrdinalIgnoreCase))
                throw new IOException("That output folder would overwrite your stock ISO — pick a different folder.");

            progress($"Copying ISO → {OutputName} …");
            File.Copy(stockIso, outIso, overwrite: true);

            uint crc;
            using (var fs = new FileStream(outIso, FileMode.Open, FileAccess.ReadWrite))
                crc = ApplySignPatch(fs, progress);

            progress("Publishing pnach to PCSX2 …");
            ReshipPnach(crc);
            return outIso;   // the caller sets the final informative message (avoids overwriting it)
        }

        // ── little-endian FileStream I/O ──
        static byte[] Rd(FileStream fs, long off, int n) { fs.Seek(off, SeekOrigin.Begin); var b = new byte[n]; int r = 0; while (r < n) { int k = fs.Read(b, r, n - r); if (k == 0) break; r += k; } return b; }
        static void  Wr(FileStream fs, long off, byte[] b) { fs.Seek(off, SeekOrigin.Begin); fs.Write(b, 0, b.Length); }
        static uint  RdU32(FileStream fs, long off) => BitConverter.ToUInt32(Rd(fs, off, 4), 0);
        static void  WrU32(FileStream fs, long off, uint v) => Wr(fs, off, BitConverter.GetBytes(v));
        static uint  U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);
        static void  U32(byte[] b, int o, uint v) => Array.Copy(BitConverter.GetBytes(v), 0, b, o, 4);
        static long  Align(long x, int a = SECTOR) => (x + a - 1) & ~((long)a - 1);

        class Rec { public long RecOff; public uint Ext; public uint Size; }

        static Dictionary<string, Rec> ParseRoot(FileStream fs)
        {
            byte[] pvd = Rd(fs, 16L * SECTOR, SECTOR);
            if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
                throw new IOException("Not a 2048-byte ISO9660 image — is this the right file?");
            uint rootLba = U32(pvd, 158), rootSize = U32(pvd, 166);
            byte[] d = Rd(fs, (long)rootLba * SECTOR, (int)rootSize);
            var recs = new Dictionary<string, Rec>();
            int pos = 0;
            while (pos + 33 <= d.Length)
            {
                int ln = d[pos];
                if (ln == 0) { pos = (pos / SECTOR + 1) * SECTOR; continue; }
                uint ext = U32(d, pos + 2), size = U32(d, pos + 10);
                int nlen = d[pos + 32];
                string name = Encoding.Latin1.GetString(d, pos + 33, nlen).Split(';')[0].ToUpperInvariant();
                recs[name] = new Rec { RecOff = (long)rootLba * SECTOR + pos, Ext = ext, Size = size };
                pos += ln;
            }
            return recs;
        }

        // ── DATA.HED name lookup (80-byte slots, backslash paths) ──
        static int ArchiveFind(byte[] hed, string name)
        {
            string want = name.Replace('/', '\\');
            for (int i = 0; i < hed.Length / 80; i++)
            {
                int end = Array.IndexOf(hed, (byte)0, i * 80, 80); if (end < 0) end = i * 80 + 80;
                string n = Encoding.Latin1.GetString(hed, i * 80, end - i * 80);
                if (n == want) return i;
            }
            throw new IOException($"'{name}' not found in the disc archive — is this a Dark Cloud (USA) ISO?");
        }

        // ── PAK: prepend (name,data) sub-files (name@0, dataOff@0x40, size@0x44, stride@0x48; self-relative) ──
        static byte[] PakBuildEntry(string name, byte[] data)
        {
            int stride = (int)Align(0x50 + data.Length, 0x40);
            var e = new byte[stride];
            byte[] nb = Encoding.Latin1.GetBytes(name); Array.Copy(nb, e, nb.Length);
            U32(e, 0x40, 0x50); U32(e, 0x44, (uint)data.Length); U32(e, 0x48, (uint)stride);
            Array.Copy(data, 0, e, 0x50, data.Length);
            return e;
        }
        static byte[] PakPrepend(byte[] pak, string name, byte[] data)
        {
            byte[] ent = PakBuildEntry(name, data);
            var outb = new byte[ent.Length + pak.Length];
            Array.Copy(ent, outb, ent.Length); Array.Copy(pak, 0, outb, ent.Length, pak.Length);
            return outb;
        }

        internal static uint ApplySignPatch(FileStream fs, Action<string> progress)
        {
            var recs = ParseRoot(fs);
            long datIso = (long)recs["DATA.DAT"].Ext * SECTOR;
            long hd2Base = (long)recs["DATA.HD2"].Ext * SECTOR + 16;
            byte[] hed = Rd(fs, (long)recs["DATA.HED"].Ext * SECTOR, (int)recs["DATA.HED"].Size);

            // 1) absorb DMMY. into DATA.DAT -> free tail
            var host = recs["DATA.DAT"]; var dmmy = recs["DMMY."];
            if ((long)host.Ext * SECTOR + host.Size != (long)dmmy.Ext * SECTOR)
                throw new IOException("DATA.DAT and DMMY. are not contiguous — unexpected ISO layout.");
            long freeOff = host.Size;
            long dummySectors = (dmmy.Size + SECTOR - 1) / SECTOR;
            uint newDatSize = (uint)(host.Size + dummySectors * SECTOR);
            Wr(fs, host.RecOff + 10, BitConverter.GetBytes(newDatSize));                                     // LE
            Wr(fs, host.RecOff + 14, new[] { (byte)(newDatSize >> 24), (byte)(newDatSize >> 16), (byte)(newDatSize >> 8), (byte)newDatSize }); // BE
            long freeBytes = newDatSize - freeOff;

            progress("Carving sign assets from your ISO …");
            var (kanbanMds, e01b24Img) = LoadSignAssets(fs, hed, datIso, hd2Base);

            long tail = Align(freeOff);
            byte[] ReadArchive(string name) { long s = hd2Base + (long)ArchiveFind(hed, name) * 32; return Rd(fs, datIso + RdU32(fs, s), (int)RdU32(fs, s + 4)); }
            void Redirect(string name, byte[] data)
            {
                long slot = hd2Base + (long)ArchiveFind(hed, name) * 32;
                if (data.Length > freeOff + freeBytes - tail) throw new IOException("Ran out of tail space (unexpected).");
                Wr(fs, datIso + tail, data);
                uint sec = (uint)(tail >> 11), cnt = (uint)((data.Length + SECTOR - 1) / SECTOR);
                WrU32(fs, slot, (uint)tail); WrU32(fs, slot + 4, (uint)data.Length); WrU32(fs, slot + 8, sec); WrU32(fs, slot + 12, cnt);
                tail = Align(tail + data.Length);
            }

            // 2) texture: prepend e01b24 to mes_tex.pak
            progress("Injecting the fishing-sign texture …");
            Redirect(HOST_PAK, PakPrepend(ReadArchive(HOST_PAK), "fishsign.img", e01b24Img));

            // 3) mesh: inject the kanban as a native georama part + its mapinfo placement
            progress("Injecting the fishing-sign mesh …");
            Redirect(SCENE_SCN, BuildInjectedScene(ReadArchive(SCENE_SCN), kanbanMds));
            Redirect(MAPINFO,   BuildInjectedMapinfo(ReadArchive(MAPINFO), SIGN_X, SIGN_Y, SIGN_Z, SIGN_RY));

            // 4) ELF boot-cave + CRC
            progress("Patching the boot loader …");
            return ElfPatchAndCrc(fs, recs["SCUS_971.11"]);
        }

        // ── scene.scn: append a `kanban` PTS part cloned from s04a01, + a 26th part-table entry ──
        static readonly int[] SIZE_FIELDS = { 0x4C, 0x50, 0x54, 0x78, 0x90, 0xA8, 0xC0, 0xD8 };
        const int MDSSIZE_FIELD = 0x58;

        static byte[] BuildInjectedScene(byte[] scene, byte[] kanbanMds)
        {
            var scn = new List<byte>(scene);
            byte[] s = scene;
            int n = (int)U32(s, 4);
            int toff = -1;
            for (int i = 0; i < n; i++) { int e = 0x10 + i * 0x30; if (NameAt(s, e, 0x10) == "s04a01") { toff = (int)U32(s, e + 0x10); break; } }
            if (toff < 0) throw new IOException("template part s04a01 not found in scene.scn");

            var kb = (byte[])kanbanMds.Clone();
            const int NODE = 0x10, MAT = NODE + 0x30, TRANS = MAT + 12 * 4;      // node 0 matrix / translation row
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                Array.Copy(BitConverter.GetBytes(r == c ? 1.0f : 0.0f), 0, kb, MAT + (r * 4 + c) * 4, 4);   // identity 3x3
            for (int k = 0; k < 3; k++) Array.Copy(BitConverter.GetBytes(0.0f), 0, kb, TRANS + k * 4, 4);   // origin

            var part = new List<byte>();
            part.AddRange(new ArraySegment<byte>(s, toff, 0x160));               // clone s04a01's 0x160 PTS header
            byte[] pname = Encoding.Latin1.GetBytes("kanban_0.mds");
            for (int i = 0; i < 0x10; i++) part[0x08 + i] = i < pname.Length ? pname[i] : (byte)0;
            part.AddRange(kb);
            int psize = part.Count;
            byte[] pa = part.ToArray();
            foreach (int o in SIZE_FIELDS) U32(pa, o, (uint)psize);
            U32(pa, MDSSIZE_FIELD, (uint)kb.Length);

            int blob = (int)Align(scn.Count, 16);
            while (scn.Count < blob) scn.Add(0);
            scn.AddRange(pa);
            byte[] outp = scn.ToArray();
            int ent = 0x10 + n * 0x30;
            byte[] partName = Encoding.Latin1.GetBytes("kanban");
            for (int i = 0; i < 0x10; i++) outp[ent + i] = i < partName.Length ? partName[i] : (byte)0;
            U32(outp, ent + 0x10, (uint)blob); U32(outp, ent + 0x14, (uint)psize);
            U32(outp, 4, (uint)(n + 1));
            return outp;
        }

        static byte[] BuildInjectedMapinfo(byte[] cfg, int x, int y, int z, int ry)
        {
            string t = Encoding.Latin1.GetString(cfg);
            string blk = "\r\n\tGROUND\t\"kanban\",\t\t//fishing sign\r\n"
                       + "\t\t\"\",\t\t\t//level1\r\n\t\t\"\",\t\t\t//level2\r\n\t\t\"\",\t\t\t//level3\r\n"
                       + "\t\t\"\",\t\t\t//\r\n\t\t\"\",\t\t\t//\r\n\t\t\"\",\t\t\t//\r\n\t\t\"\",\t\t\t//?\r\n"
                       + $"\t\t{x}\t,{y}\t,{z},\t//position\r\n\t\t0\t,{ry}\t,0\t//rotation\r\n";
            var matches = Regex.Matches(t, "\\tGROUND\\t\"s04a01\",.*?\\r\\n\\t\\t-?\\d[^\\r\\n]*\\r\\n\\t\\t\\d[^\\r\\n]*,[^\\r\\n]*\\r\\n", RegexOptions.Singleline);
            if (matches.Count == 0) throw new IOException("no GROUND s04a01 block found in mapinfo.cfg");
            int ins = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length;
            return Encoding.Latin1.GetBytes(t.Substring(0, ins) + blk + t.Substring(ins));
        }

        static string NameAt(byte[] b, int o, int max) { int e = Array.IndexOf(b, (byte)0, o, max); if (e < 0) e = o + max; return Encoding.Latin1.GetString(b, o, e - o); }

        // ── ELF boot-cave patch + new PCSX2 CRC ──
        const int zero = 0, v0 = 2, a0 = 4, a1 = 5, a2 = 6, a3 = 7, t0 = 8, sp = 29;
        static uint Lui(int rt, uint i) => 0x3C000000u | ((uint)rt << 16) | (i & 0xFFFF);
        static uint Ori(int rt, int rs, uint i) => 0x34000000u | ((uint)rs << 21) | ((uint)rt << 16) | (i & 0xFFFF);
        static uint Lw(int rt, int o, int b) => 0x8C000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        static uint Sw(int rt, int o, int b) => 0xAC000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        static uint Addiu(int rt, int rs, int i) => 0x24000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(i & 0xFFFF);
        static uint Move(int rd, int rs) => Ori(rd, rs, 0);
        static uint Jal(uint tgt) => 0x0C000000u | ((tgt >> 2) & 0x03FFFFFF);
        static uint J(uint tgt) => 0x08000000u | ((tgt >> 2) & 0x03FFFFFF);

        static byte[] BuildCave()
        {
            uint[] w = {
                Addiu(sp, sp, -0x20), Sw(a0, 0x14, sp), Sw(a1, 0x18, sp),
                Move(a0, a1), Lui(a1, STR_VA >> 16), Ori(a1, a1, STR_VA & 0xFFFF), Addiu(a2, zero, 0),
                Jal(GetPackFile), 0,
                Lui(t0, DIAG_VA >> 16), Sw(v0, (int)(DIAG_VA & 0xFFFF), t0),
                Move(a1, v0), Lui(a0, SysTexMgr >> 16), Ori(a0, a0, SysTexMgr & 0xFFFF),
                Addiu(a2, zero, -1), Addiu(a3, zero, 0), Addiu(t0, zero, 0),
                Jal(EnterIMGFile), 0,
                Lw(a0, 0x14, sp), Lw(a1, 0x18, sp), Addiu(a2, zero, 0),
                Jal(LoadFile), 0,
                Addiu(sp, sp, 0x20), J(REJOIN_VA), 0,
            };
            var b = new byte[w.Length * 4];
            for (int i = 0; i < w.Length; i++) Array.Copy(BitConverter.GetBytes(w[i]), 0, b, i * 4, 4);
            if (b.Length > CAVE_LEN) throw new InvalidOperationException($"cave {b.Length}B > {CAVE_LEN}B");
            return b;
        }

        static uint ElfPatchAndCrc(FileStream fs, Rec elf)
        {
            long elfIso = (long)elf.Ext * SECTOR;
            byte[] eh = Rd(fs, elfIso, 0x34);
            uint phoff = U32(eh, 0x1c); ushort phent = BitConverter.ToUInt16(eh, 0x2a), phnum = BitConverter.ToUInt16(eh, 0x2c);
            long pOff = -1, pVa = -1;
            for (int i = 0; i < phnum; i++)
            {
                byte[] ph = Rd(fs, elfIso + phoff + i * phent, 24);
                uint typ = U32(ph, 0), off = U32(ph, 4), va = U32(ph, 8), fsz = U32(ph, 16);
                if (typ == 1 && fsz > 0 && va <= DETOUR_VA && DETOUR_VA < va + fsz) { pOff = off; pVa = va; break; }
            }
            if (pOff < 0) throw new IOException("No PT_LOAD covers the patch site — wrong ISO/version.");
            long ElfOff(uint va) => elfIso + pOff + (va - pVa);

            byte[] cave = BuildCave();
            if (RdU32(fs, ElfOff(DETOUR_VA)) != Jal(LoadFile) || RdU32(fs, ElfOff(DETOUR_VA + 4)) != 0)
                throw new IOException("Boot-loader patch site is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            byte[] caveWas = Rd(fs, ElfOff(CAVE_VA), cave.Length);
            foreach (byte x in caveWas) if (x != 0) throw new IOException("Boot-cave region not empty — unexpected ISO.");

            Wr(fs, ElfOff(STR_VA), Encoding.ASCII.GetBytes("fishsign.img\0"));
            Wr(fs, ElfOff(CAVE_VA), cave);
            WrU32(fs, ElfOff(DETOUR_VA), J(CAVE_VA));

            byte[] pelf = Rd(fs, elfIso, (int)elf.Size);
            uint crc = 0;
            for (int i = 0; i < pelf.Length / 4; i++) crc ^= U32(pelf, i * 4);
            return crc;
        }

        // ── pnach: copy the mod's own A5C05C78.pnach into the PCSX2 cheats folder as <CRC>.pnach ──
        static void ReshipPnach(uint crc)
        {
            string dir = Pcsx2CheatsDir(); Directory.CreateDirectory(dir);
            string src = Path.Combine(AppContext.BaseDirectory, "Resources", "PNACH", OLD_CRC + ".pnach");
            if (!File.Exists(src)) throw new FileNotFoundException("Bundled pnach not found: " + src);
            string newCrc = crc.ToString("X8");
            string body = Regex.Replace(File.ReadAllText(src), "\\[" + OLD_CRC + "\\]", "[" + newCrc + "]");
            string Norm(string s) => Regex.Replace(s, "\\[[0-9A-Fa-f]{8}\\]", "[]");
            foreach (string old in Directory.GetFiles(dir, "*.pnach"))   // drop OUR stale patched-CRC copies only
            {
                string nm = Path.GetFileNameWithoutExtension(old).ToUpperInvariant();
                if (!Regex.IsMatch(nm, "^[0-9A-F]{8}$") || nm == OLD_CRC || nm == newCrc) continue;
                if (Norm(File.ReadAllText(old)) == Norm(body)) File.Delete(old);
            }
            File.WriteAllText(Path.Combine(dir, newCrc + ".pnach"), body);
        }

        // ── sign assets: CARVE from the user's OWN ISO (Muska Lacka = e04). DC_SIGN_ASSETS env overrides for dev. ──
        static (byte[] kanban, byte[] img) LoadSignAssets(FileStream fs, byte[] hed, long datIso, long hd2Base)
        {
            string env = Environment.GetEnvironmentVariable("DC_SIGN_ASSETS");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "kanban.mds")))
                return (File.ReadAllBytes(Path.Combine(env, "kanban.mds")), File.ReadAllBytes(Path.Combine(env, "e01b24_bank.img")));
            byte[] imgPak = ReadArchive(fs, hed, datIso, hd2Base, "gedit/e04/img.pak");
            byte[] scene  = ReadArchive(fs, hed, datIso, hd2Base, "gedit/e04/scene.scn");
            return (CarveKanban(scene), CarveTexture(imgPak));
        }

        static byte[] ReadArchive(FileStream fs, byte[] hed, long datIso, long hd2Base, string name)
        {
            long s = hd2Base + (long)ArchiveFind(hed, name) * 32;
            return Rd(fs, datIso + RdU32(fs, s), (int)RdU32(fs, s + 4));
        }

        // Carve the e01b24 texture: find the IM2 bank in e04/img.pak that holds it, extract the CLEAN TIM2
        // (0x10 file header + picture header + image + clut — no adjacent-entry spillover), wrap in a 1-entry bank.
        static byte[] CarveTexture(byte[] pak)
        {
            int p = 0;
            while (p < pak.Length && pak[p] != 0)
            {
                uint dataOff = U32(pak, p + 0x40), size = U32(pak, p + 0x44), stride = U32(pak, p + 0x48);
                int b = p + (int)dataOff;
                if (size >= 8 && pak[b] == 'I' && pak[b + 1] == 'M' && pak[b + 2] == '2' && pak[b + 3] == 0)
                {
                    int count = (int)U32(pak, b + 4);
                    for (int i = 0; i < count; i++)
                    {
                        int e = b + 0x10 + i * 0x30;                              // ENT = 0x30, name@0, offset@+0x20
                        if (NameAt(pak, e, 0x20) != "e01b24") continue;
                        int t = b + (int)U32(pak, e + 0x20);                       // TIM2 block (bank-relative offset)
                        uint clutSz = U32(pak, t + 0x14), imgSz = U32(pak, t + 0x18);
                        ushort hdrSz = BitConverter.ToUInt16(pak, t + 0x1C);
                        int clean = 0x10 + hdrSz + (int)imgSz + (int)clutSz;
                        var tim2 = new byte[clean]; Array.Copy(pak, t, tim2, 0, clean);
                        return Im2Build("e01b24", tim2);
                    }
                }
                p += (int)stride;
            }
            throw new IOException("Could not find the fishing-sign texture (e01b24) in the ISO.");
        }

        static byte[] Im2Build(string name, byte[] tim2)
        {
            var outb = new byte[0x40 + tim2.Length];
            outb[0] = (byte)'I'; outb[1] = (byte)'M'; outb[2] = (byte)'2'; outb[3] = 0;
            U32(outb, 4, 1);                                                       // count = 1
            byte[] nb = Encoding.Latin1.GetBytes(name);
            Array.Copy(nb, 0, outb, 0x10, Math.Min(nb.Length, 0x1F));              // name @ entry (0x10)
            U32(outb, 0x30, 0x40);                                                 // entry offset (@+0x20) = 0x40
            Array.Copy(tim2, 0, outb, 0x40, tim2.Length);
            return outb;
        }

        // Carve the kanban mesh: find its node in e04/scene.scn, its containing MDS block + MDT, emit a
        // standalone 1-node MDS (parent -1, block-relative meshOff 0x80). Matches mds_surgery.build.
        static byte[] CarveKanban(byte[] scene)
        {
            int ki = IndexOf(scene, Encoding.ASCII.GetBytes("kanban\0"), 0);
            if (ki < 0) throw new IOException("Could not find the fishing-sign mesh (kanban) in the ISO.");
            int mds = LastIndexOf(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, ki - 8);
            int tbl = (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8);
            int knOff = -1;
            for (int i = 0; i < count; i++) { int no = mds + tbl + i * 0x70; if (NameAt(scene, no + 8, 0x20) == "kanban") { knOff = no; break; } }
            if (knOff < 0) throw new IOException("kanban node index not found.");
            int mdt = mds + (int)U32(scene, knOff + 0x28);                         // meshOff is block-relative
            int mdtTotal = (int)U32(scene, mdt + 8);                              // MDT self-delimiting
            var outb = new byte[0x10 + 0x70 + mdtTotal];
            outb[0] = (byte)'M'; outb[1] = (byte)'D'; outb[2] = (byte)'S'; outb[3] = 0;
            U32(outb, 4, U32(scene, mds + 4)); U32(outb, 8, 1); U32(outb, 0xC, 0x10);   // version, count 1, tbl 0x10
            Array.Copy(scene, knOff, outb, 0x10, 0x70);                            // the node
            U32(outb, 0x10 + 0x28, 0x80);                                          // meshOff = 0x80 (block-relative)
            U32(outb, 0x10 + 0x2C, 0xFFFFFFFF);                                    // parent = -1 (detached root)
            Array.Copy(scene, mdt, outb, 0x10 + 0x70, mdtTotal);
            return outb;
        }

        static int IndexOf(byte[] hay, byte[] needle, int start)
        {
            for (int i = start; i <= hay.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
        static int LastIndexOf(byte[] hay, byte[] needle, int before)
        {
            for (int i = Math.Min(before, hay.Length - needle.Length); i >= 0; i--)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
    }
}
