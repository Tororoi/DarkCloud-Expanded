using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Raw byte / ISO9660 / DATA.HED archive plumbing shared by every ISO-patch class: little-endian
    /// FileStream and byte[] accessors, alignment, byte search, the root-directory + archive-slot lookups,
    /// PAK entry building and the scene node -> MDT resolver. Consumers `using static` this class.
    /// </summary>
    internal static class IsoBytes
    {
        internal const int SectorBytes = 2048;

        // ── little-endian FileStream I/O ──
        internal static byte[] Rd(FileStream fs, long off, int n) { fs.Seek(off, SeekOrigin.Begin); var b = new byte[n]; int r = 0; while (r < n) { int k = fs.Read(b, r, n - r); if (k == 0) break; r += k; } return b; }
        internal static void  Wr(FileStream fs, long off, byte[] b) { fs.Seek(off, SeekOrigin.Begin); fs.Write(b, 0, b.Length); }
        internal static uint  RdU32(FileStream fs, long off) => BitConverter.ToUInt32(Rd(fs, off, 4), 0);
        internal static void  WrU32(FileStream fs, long off, uint v) => Wr(fs, off, BitConverter.GetBytes(v));
        internal static uint   U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);
        internal static void   U32(byte[] b, int o, uint v) => Array.Copy(BitConverter.GetBytes(v), 0, b, o, 4);
        internal static ushort U16(byte[] b, int o) => BitConverter.ToUInt16(b, o);
        internal static void   U16(byte[] b, int o, ushort v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        internal static long  Align(long x, int a = SectorBytes) => (x + a - 1) & ~((long)a - 1);

        internal class Rec { public long RecOff; public uint Ext; public uint Size; }

        internal static Dictionary<string, Rec> ParseRoot(FileStream fs)
        {
            byte[] pvd = Rd(fs, 16L * SectorBytes, SectorBytes);
            if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
                throw new IOException("Not a 2048-byte ISO9660 image — is this the right file?");
            uint rootLba = U32(pvd, 158), rootSize = U32(pvd, 166);
            byte[] d = Rd(fs, (long)rootLba * SectorBytes, (int)rootSize);
            var recs = new Dictionary<string, Rec>();
            int pos = 0;
            while (pos + 33 <= d.Length)
            {
                int ln = d[pos];
                if (ln == 0) { pos = (pos / SectorBytes + 1) * SectorBytes; continue; }
                uint ext = U32(d, pos + 2), size = U32(d, pos + 10);
                int nlen = d[pos + 32];
                string name = Encoding.Latin1.GetString(d, pos + 33, nlen).Split(';')[0].ToUpperInvariant();
                recs[name] = new Rec { RecOff = (long)rootLba * SectorBytes + pos, Ext = ext, Size = size };
                pos += ln;
            }
            return recs;
        }

        // ── DATA.HED name lookup (80-byte slots, backslash paths) ──
        internal static int ArchiveFind(byte[] hed, string name)
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
        internal static byte[] PakBuildEntry(string name, byte[] data)
        {
            int stride = (int)Align(0x50 + data.Length, 0x40);
            var e = new byte[stride];
            byte[] nb = Encoding.Latin1.GetBytes(name); Array.Copy(nb, e, nb.Length);
            U32(e, 0x40, 0x50); U32(e, 0x44, (uint)data.Length); U32(e, 0x48, (uint)stride);
            Array.Copy(data, 0, e, 0x50, data.Length);
            return e;
        }
        internal static byte[] PakPrepend(byte[] pak, string name, byte[] data)
        {
            byte[] ent = PakBuildEntry(name, data);
            var outb = new byte[ent.Length + pak.Length];
            Array.Copy(ent, outb, ent.Length); Array.Copy(pak, 0, outb, ent.Length, pak.Length);
            return outb;
        }

        internal static int Find(byte[] hay, byte[] needle) => FindFrom(hay, needle, 0);

        internal static int FindFrom(byte[] hay, byte[] needle, int start) => ReusableFunctions.IndexOfBytes(hay, needle, start);

        internal static int FindLast(byte[] hay, byte[] needle, int before) => ReusableFunctions.LastIndexOfBytes(hay, needle, before);

        internal static float F32(byte[] b, int o) => BitConverter.ToSingle(b, o);

        internal static int FindMdt(byte[] scene, string node)
        {
            int at = Find(scene, Encoding.Latin1.GetBytes(node + "\0"));               // node name lives at node+8
            if (at < 0) throw new IOException($"ring node '{node}' not found in scene.scn");
            int meshOff = BitConverter.ToInt32(scene, (at - 8) + 0x28);                // meshOff at node+0x28
            int mds = FindLast(scene, Encoding.ASCII.GetBytes("MDS\0"), at);           // owning MDS block base
            foreach (int cand in new[] { meshOff, mds + meshOff })                     // meshOff: absolute or block-relative
                if (cand > 0 && cand < scene.Length - 3 && scene[cand] == 'M' && scene[cand + 1] == 'D' && scene[cand + 2] == 'T')
                    return cand;
            throw new IOException($"MDT for '{node}' not resolved");
        }

        internal static string NameAt(byte[] b, int o, int max) { int e = Array.IndexOf(b, (byte)0, o, max); if (e < 0) e = o + max; return Encoding.Latin1.GetString(b, o, e - o); }

        // ── DATA.HD2 slot lookup + read (the DATA.HED name -> hd2 slot -> DATA.DAT bytes) ──
        internal static byte[] ReadArchiveEntry(FileStream fs, byte[] hed, long datIso, long hd2Base, string name)
        {
            long s = hd2Base + (long)ArchiveFind(hed, name) * 32;
            return Rd(fs, datIso + RdU32(fs, s), (int)RdU32(fs, s + 4));
        }

        internal static int Align16(int x) => (int)Align(x, 16);
        internal static void WrF(byte[] b, int o, float f) => Array.Copy(BitConverter.GetBytes(f), 0, b, o, 4);
    }
}
