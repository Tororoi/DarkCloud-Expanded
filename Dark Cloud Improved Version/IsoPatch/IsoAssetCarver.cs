using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Carve the mod's sign/ladder textures and the kanban + ripple-decal meshes out of the user's OWN ISO
    /// (nothing game-derived is bundled): TIM2 extraction, IM2 bank building, and the 1-node MDS wrappers.
    /// </summary>
    internal static class IsoAssetCarver
    {
        // ── sign + ladder textures: CARVE from the user's OWN ISO. The sign glyph e01b24 lives in Muska Lacka
        //    (e04/img.pak); the metal-ladder texture e05t06 lives in the Factory (e05/img.pak). Both go into ONE
        //    IM2 bank (fishsign.img) as two entries — a single boot-cave EnterIMGFile(-1) registers every entry
        //    in a bank (that is how the town loads its own multi-texture e03t01.img in one call), so the ladder
        //    material "e05t06" resolves globally exactly like the sign's "e01b24". No boot-cave change needed.
        //    DC_SIGN_ASSETS overrides ONLY the kanban mesh for dev; textures always come from the ISO. ──
        internal static (byte[] kanban, byte[] img) LoadSignAssets(FileStream fs, byte[] hed, long datIso, long hd2Base)
        {
            byte[] e04img = ReadArchiveEntry(fs, hed, datIso, hd2Base, "gedit/e04/img.pak");
            byte[] e05img = ReadArchiveEntry(fs, hed, datIso, hd2Base, "gedit/e05/img.pak");
            byte[] bank = Im2BuildMulti(new[] { "e01b24", "e05t06" },
                                        new[] { CarveTim2(e04img, "e01b24"), CarveTim2(e05img, "e05t06") });
            string env = Environment.GetEnvironmentVariable("DC_SIGN_ASSETS");
            byte[] kanban = (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "kanban.mds")))
                ? File.ReadAllBytes(Path.Combine(env, "kanban.mds"))
                : CarveKanban(ReadArchiveEntry(fs, hed, datIso, hd2Base, "gedit/e04/scene.scn"));
            return (kanban, bank);
        }

        // Carve a named texture's CLEAN TIM2 (0x10 file header + picture header + image + clut — no adjacent-entry
        // spillover) out of an img.pak's IM2 bank. Returns just the TIM2 block; Im2BuildMulti wraps banks.
        internal static byte[] CarveTim2(byte[] pak, string texName)
        {
            int p = 0;
            while (p < pak.Length && pak[p] != 0)
            {
                uint dataOff = U32(pak, p + 0x40), size = U32(pak, p + 0x44), stride = U32(pak, p + 0x48);
                int b = p + (int)dataOff;
                if (size >= 8 && pak[b] == 'I' && pak[b + 1] == 'M' && (pak[b + 2] == '2' || pak[b + 2] == 'G') && pak[b + 3] == 0)
                {
                    int count = (int)U32(pak, b + 4);
                    for (int i = 0; i < count; i++)
                    {
                        int e = b + 0x10 + i * 0x30;                              // ENT = 0x30, name@0, offset@+0x20
                        if (NameAt(pak, e, 0x20) != texName) continue;
                        int t = b + (int)U32(pak, e + 0x20);                       // TIM2 block (bank-relative offset)
                        uint clutSz = U32(pak, t + 0x14), imgSz = U32(pak, t + 0x18);
                        ushort hdrSz = BitConverter.ToUInt16(pak, t + 0x1C);
                        int clean = 0x10 + hdrSz + (int)imgSz + (int)clutSz;
                        var tim2 = new byte[clean]; Array.Copy(pak, t, tim2, 0, clean);
                        return tim2;
                    }
                }
                p += (int)stride;
            }
            throw new IOException($"Could not find texture {texName} in img.pak.");
        }

        // Wrap N clean TIM2 blocks into one IM2 bank (header 0x10, per-entry 0x30 = name@0 + bank-relative
        // offset@+0x20; TIM2 blocks 16-aligned after the entry table). Matches the native bank layout that
        // EnterIMGFile(-1) registers wholesale, so every entry's name resolves for meshes that reference it.
        internal static byte[] Im2BuildMulti(string[] names, byte[][] tim2s)
        {
            int count = names.Length;
            int dataStart = (0x10 + count * 0x30 + 0xF) & ~0xF;
            var offs = new int[count]; int cur = dataStart;
            for (int i = 0; i < count; i++) { offs[i] = cur; cur += (tim2s[i].Length + 0xF) & ~0xF; }
            var outb = new byte[cur];
            outb[0] = (byte)'I'; outb[1] = (byte)'M'; outb[2] = (byte)'2'; outb[3] = 0;
            U32(outb, 4, (uint)count);
            for (int i = 0; i < count; i++)
            {
                int e = 0x10 + i * 0x30;
                byte[] nb = Encoding.Latin1.GetBytes(names[i]);
                Array.Copy(nb, 0, outb, e, Math.Min(nb.Length, 0x1F));
                U32(outb, e + 0x20, (uint)offs[i]);
                Array.Copy(tim2s[i], 0, outb, offs[i], tim2s[i].Length);
            }
            return outb;
        }

        // Carve the kanban mesh: find its node in e04/scene.scn, its containing MDS block + MDT, emit a
        // standalone 1-node MDS (parent -1, block-relative meshOff 0x80). Matches mds_surgery.build.
        internal static byte[] CarveKanban(byte[] scene)
        {
            int ki = FindFrom(scene, Encoding.ASCII.GetBytes("kanban\0"), 0);
            if (ki < 0) throw new IOException("Could not find the fishing-sign mesh (kanban) in the ISO.");
            int mds = FindLast(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, ki - 8);
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

        // Build the wading ripple decal as a SINGLE flat quad mapping the ring texture ONCE. The donor
        // `hamon__A01z` (Norune waterwheel) is 56 tris that EACH map the full 0→1 texture — in-game that
        // tiled the ring ~28× across a big patch ("wrong texture, too big"). Here we carve only hamon's
        // MATERIAL (`e01b22`, Queens' TEX_ANIME ripple texture, ring-retextured by the bake post-step) and
        // emit a fresh 4-vert / 2-tri quad at ±RippleDecalHalfExtent with UV 0→1, so the ring shows once. Node keeps
        // the `__za01` suffix attrs (z=no-Z-write, a01=alpha-test). Injected as static part "wripple";
        // CanalTide flips the part LAYER (+0xE4) to 0x15 so DrawWater's per-layer loop draws it in the
        // WATER pass (water texture group resident — a normal-layer part sampling it renders garbage).
        internal const float RippleDecalHalfExtent = 5.5f;  // ring half-extent (11 units across, tight around the player's feet)
        internal static byte[] CarveRippleDecal(byte[] scene, float half = RippleDecalHalfExtent)
        {
            const string RippleNodeName = "hamon__A01z";
            int ki = FindFrom(scene, Encoding.ASCII.GetBytes(RippleNodeName + "\0"), 0);
            if (ki < 0) throw new IOException("Could not find the ripple decal (hamon__A01z) in the ISO.");
            int mds = FindLast(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, ki - 8);
            int tbl = (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8);
            int ndOff = -1;
            for (int i = 0; i < count; i++) { int no = mds + tbl + i * 0x70; if (NameAt(scene, no + 8, 0x20) == RippleNodeName) { ndOff = no; break; } }
            if (ndOff < 0) throw new IOException("hamon__A01z node index not found.");
            int mdt = mds + (int)U32(scene, ndOff + 0x28);
            // carve the material descriptor verbatim (hw[14] = MAT offset; stride 0x60, name "e01b22" @+0x34)
            int matOff = mdt + (int)U32(scene, mdt + 0x38);
            byte[] mat = new byte[0x60]; Array.Copy(scene, matOff, mat, 0, 0x60);
            if (NameAt(mat, 0x34, 0x20) != "e01b22") throw new IOException("hamon material is not e01b22.");

            // ── build the quad MDT. Blocks in the vanilla order POS/DL/UV/NORM/MAT, each 16-aligned.
            //    Codec semantics (RE'd, canal_visual_cap): a record is (posIdx, hw6Idx, hw12Idx); the hw6
            //    block ("UV" in the header) holds NORMALS, the hw12 block ("NORM") holds TEXCOORDS.
            float H = half;
            float[][] posv = { new[] { -H, 0f, -H }, new[] { H, 0f, -H }, new[] { H, 0f, H }, new[] { -H, 0f, H } };
            float[][] tcv  = { new[] { 0f, 0f }, new[] { 1f, 0f }, new[] { 1f, 1f }, new[] { 0f, 1f } };   // u,v corners
            int[][] recs   = { new[] { 0, 0, 0 }, new[] { 1, 0, 1 }, new[] { 2, 0, 2 },                    // tri 0
                               new[] { 0, 0, 0 }, new[] { 2, 0, 2 }, new[] { 3, 0, 3 } };                  // tri 1
            int POS = 0x40, POSsz = 4 * 16;
            int DL = POS + POSsz;                                             // 0x80 (aligned)
            int DLsz = 16 + 12 + recs.Length * 3 * 4;                         // preamble + submesh hdr + records = 0x64
            int UV = Align16(DL + DLsz);                                      // normals block (1 entry)
            int NORM = UV + 1 * 16;                                           // texcoords block (4 entries)
            int MAT = NORM + 4 * 16;
            int total = MAT + 0x60;
            var mm = new byte[total];
            mm[0] = (byte)'M'; mm[1] = (byte)'D'; mm[2] = (byte)'T'; mm[3] = 0;
            U32(mm, 0x04, 0x40); U32(mm, 0x08, (uint)total); U32(mm, 0x0C, 4);          // hdr[1] flag, total, pos count
            U32(mm, 0x10, (uint)POS); U32(mm, 0x14, 1); U32(mm, 0x18, (uint)UV); U32(mm, 0x1C, 0);
            U32(mm, 0x20, 0xFFFFFFFF); U32(mm, 0x24, (uint)DLsz); U32(mm, 0x28, (uint)DL); U32(mm, 0x2C, 4);
            U32(mm, 0x30, (uint)NORM); U32(mm, 0x34, 1); U32(mm, 0x38, (uint)MAT); U32(mm, 0x3C, 0xCDCDCDCD);
            for (int v = 0; v < 4; v++)                                       // positions (w = 1)
            {
                int o = POS + v * 16;
                WrF(mm, o, posv[v][0]); WrF(mm, o + 4, posv[v][1]); WrF(mm, o + 8, posv[v][2]); WrF(mm, o + 12, 1f);
            }
            U32(mm, DL + 0, 0xCDCDCDCD); U32(mm, DL + 4, 0x10); U32(mm, DL + 8, 1); U32(mm, DL + 12, 0xCDCDCDCD);  // preamble (submesh count = 1)
            U32(mm, DL + 16, 3); U32(mm, DL + 20, (uint)recs.Length); U32(mm, DL + 24, 0);                        // submesh: prim 3, record count (6), matIdx 0
            for (int r = 0; r < recs.Length; r++)
                for (int k = 0; k < 3; k++) U32(mm, DL + 28 + (r * 3 + k) * 4, (uint)recs[r][k]);
            WrF(mm, UV, 0f); WrF(mm, UV + 4, 1f); WrF(mm, UV + 8, 0f); WrF(mm, UV + 12, 1f);                       // 1 up-normal (0,1,0)
            for (int t = 0; t < 4; t++)                                       // 4 texcoords (u, v)
            {
                int o = NORM + t * 16;
                WrF(mm, o, tcv[t][0]); WrF(mm, o + 4, tcv[t][1]); WrF(mm, o + 8, 1f); WrF(mm, o + 12, 0f);
            }
            Array.Copy(mat, 0, mm, MAT, 0x60);

            // wrap as a kanban-style 1-node MDS (clone hamon's node record for its attr/name fields)
            var outb = new byte[0x10 + 0x70 + total];
            outb[0] = (byte)'M'; outb[1] = (byte)'D'; outb[2] = (byte)'S'; outb[3] = 0;
            U32(outb, 4, U32(scene, mds + 4)); U32(outb, 8, 1); U32(outb, 0xC, 0x10);
            Array.Copy(scene, ndOff, outb, 0x10, 0x70);
            U32(outb, 0x10 + 0x28, 0x80);                                     // meshOff = 0x80 (block-relative)
            U32(outb, 0x10 + 0x2C, 0xFFFFFFFF);                               // parent = -1 (detached root)
            Array.Copy(mm, 0, outb, 0x10 + 0x70, total);
            return outb;
        }
    }
}
