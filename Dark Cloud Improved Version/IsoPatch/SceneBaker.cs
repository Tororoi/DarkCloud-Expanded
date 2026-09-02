using static Dark_Cloud_Improved_Version.FishingLabelIds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Generic scene.scn / mapinfo.cfg bake machinery: inject a kanban-style part (+ its EPARTS_FUNC_DATA
    /// event points and optional `_a` collision), build func-data entries, and place parts in mapinfo.
    /// The per-town scene patches live in TownSceneBakes. All byte[] transforms.
    /// </summary>
    internal static class SceneBaker
    {
        // ── scene.scn: append a `kanban` PTS part cloned from s04a01, + a 26th part-table entry ──
        internal static readonly int[] PartSizeFields = { 0x4C, 0x50, 0x54, 0x78, 0x90, 0xA8, 0xC0, 0xD8 };
        internal const int PartMdsSizeField = 0x58;
        // LoadPTS (0x19f6f0) reads the `_a` collision variant's OFFSET at part+0x78 and its SIZE (gate) at
        // part+0x7c; if size>0 it feeds part+offset to LoadCollisionFile -> CreateCollisionMDT.
        internal const int PartCollisionOffsetField = 0x78, PartCollisionSizeField = 0x7C;
        internal const int PartMdsOffsetField  = 0x48;   // part+0x48 = MDS data offset (LoadPTS); shifts when func-data precedes the MDS

        /// <summary>The kanban's collision: a single solid PANEL hugging the sign, as an MDS-wrapped COLLISION
        /// MDT. This mirrors how Muska Lacka's native sign is collided (e04m01_a @ the kanban): one flat box
        /// ~13 wide x ~3 thick x 16 tall spanning the whole sign, NOT thin post/board boxes (those were too
        /// flimsy and let the player clip through). Verts are LOCAL — the mapinfo places/rotates them with the
        /// sign, so they line up with the visual. Format reverse-engineered from CreateCollisionMDT
        /// (0x127250) + LoadCollisionFile (0x126f70): MDT needs magic, +0x08 total size, +0x0C vert count,
        /// +0x10 POS offset, +0x28 display-list offset, +0x38 colour block (0 = none); the DL has the triangle
        /// count at +0x14 and 5-int32 records (v0,v1,v2,colour,pad) at +0x18; POS verts are x,y,z,1 at 0x10.</summary>
        internal static byte[] BuildKanbanCollision(string node = "kanban_a")
        {
            var verts = new List<float[]>();
            var tris  = new List<int[]>();
            void Box(float x0, float x1, float y0, float y1, float z0, float z1)
            {
                int b = verts.Count;
                verts.Add(new[]{x0,y0,z0}); verts.Add(new[]{x1,y0,z0}); verts.Add(new[]{x1,y0,z1}); verts.Add(new[]{x0,y0,z1});
                verts.Add(new[]{x0,y1,z0}); verts.Add(new[]{x1,y1,z0}); verts.Add(new[]{x1,y1,z1}); verts.Add(new[]{x0,y1,z1});
                int[][] f = { new[]{0,1,2}, new[]{0,2,3}, new[]{4,6,5}, new[]{4,7,6}, new[]{0,4,5}, new[]{0,5,1},
                              new[]{3,2,6}, new[]{3,6,7}, new[]{0,3,7}, new[]{0,7,4}, new[]{1,5,6}, new[]{1,6,2} };
                foreach (var t in f) tris.Add(new[]{ b+t[0], b+t[1], b+t[2] });   // winding is moot — collision is two-sided
            }
            // One solid panel over the whole sign (kanban local bbox is X[-6,6] Y[0,16] Z[0,2]); ~3 thick in Z
            // and slightly over-wide, matching Muska Lacka's native ~13 x 3 x 16 sign collision.
            Box(-6.5f, 6.5f, 0f, 16f, -1f, 2f);

            int vc = verts.Count, tc = tris.Count;
            int posOff = 0x40, dlOff = posOff + vc * 0x10, mdtLen = dlOff + 0x18 + tc * 0x14;
            var mdt = new byte[mdtLen];
            U32(mdt, 0x00, 0x0054444Du);            // 'MDT\0'
            U32(mdt, 0x08, (uint)mdtLen);           // total size (memcpy in CreateCollisionMDT)
            U32(mdt, 0x0C, (uint)vc);               // POS vertex count (CreateBBox)
            U32(mdt, 0x10, (uint)posOff);           // POS offset
            U32(mdt, 0x28, (uint)dlOff);            // display-list offset
            U32(mdt, 0x38, 0);                      // colour block: none
            for (int i = 0; i < vc; i++)
            {
                int o = posOff + i * 0x10;
                Array.Copy(BitConverter.GetBytes(verts[i][0]), 0, mdt, o + 0, 4);
                Array.Copy(BitConverter.GetBytes(verts[i][1]), 0, mdt, o + 4, 4);
                Array.Copy(BitConverter.GetBytes(verts[i][2]), 0, mdt, o + 8, 4);
                Array.Copy(BitConverter.GetBytes(1.0f),        0, mdt, o + 12, 4);
            }
            U32(mdt, dlOff + 0x14, (uint)tc);       // triangle count
            for (int i = 0; i < tc; i++)
            {
                int o = dlOff + 0x18 + i * 0x14;
                U32(mdt, o + 0, (uint)tris[i][0]); U32(mdt, o + 4, (uint)tris[i][1]); U32(mdt, o + 8, (uint)tris[i][2]);
                // +0x0C colour index, +0x10 pad — left 0
            }

            // MDS wrapper: [0x10 header][0x70 node][MDT @ 0x80] — the node has an identity matrix + parent -1.
            const int nodeOff = 0x10, mdtStart = 0x80;
            var mds = new byte[mdtStart + mdt.Length];
            U32(mds, 0x00, 0x0053444Du); U32(mds, 0x04, 1); U32(mds, 0x08, 1); U32(mds, 0x0C, 0x10);   // MDS,ver,nodeCount,tblOff
            U32(mds, nodeOff + 0x04, 0x70);
            byte[] nn = Encoding.Latin1.GetBytes(node);
            Array.Copy(nn, 0, mds, nodeOff + 0x08, nn.Length);
            U32(mds, nodeOff + 0x28, mdtStart);            // meshOff (MDS-relative) -> the collision MDT
            U32(mds, nodeOff + 0x2C, 0xFFFFFFFFu);         // parent = -1
            for (int i = 0; i < 4; i++) Array.Copy(BitConverter.GetBytes(1.0f), 0, mds, nodeOff + 0x30 + i * 0x14, 4);  // identity 4x4
            Array.Copy(mdt, 0, mds, mdtStart, mdt.Length);
            return mds;
        }

        /// <summary>Injects a `kanban` part into a scene.scn. <paramref name="templateHeader"/> is a 0x160-byte
        /// PTS part header (carved from s04a01 with <see cref="PartHeader"/>) — self-contained, so the same one
        /// is reused for Brownboo AND Queens.</summary>
        internal static byte[] BuildInjectedScene(byte[] scene, byte[] kanbanMds, byte[] templateHeader, byte[] collisionMds = null,
                                         string partName = "kanban", bool bakeIdentity = true, byte[] funcData = null)
        {
            var scn = new List<byte>(scene);
            int n = (int)U32(scene, 4);

            var kb = (byte[])kanbanMds.Clone();
            const int NODE = 0x10, MAT = NODE + 0x30, TRANS = MAT + 12 * 4;      // node 0 matrix / translation row
            if (bakeIdentity)   // kanban verts are local; force identity+origin so the mapinfo positions it.
            {                   // the ladder MDS already carries world-baked verts (identity), so skip.
                for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                    Array.Copy(BitConverter.GetBytes(r == c ? 1.0f : 0.0f), 0, kb, MAT + (r * 4 + c) * 4, 4);   // identity 3x3
                for (int k = 0; k < 3; k++) Array.Copy(BitConverter.GetBytes(0.0f), 0, kb, TRANS + k * 4, 4);   // origin
            }

            var part = new List<byte>();
            part.AddRange(templateHeader);                                      // the reusable 0x160 PTS header
            byte[] pname = Encoding.Latin1.GetBytes(partName + "_0.mds");
            for (int i = 0; i < 0x10; i++) part[0x08 + i] = i < pname.Length ? pname[i] : (byte)0;
            // NATIVE EVENT POINTS: the func-data block sits BETWEEN the 0x160 header and the MDS (native layout,
            // so the event-loader's memcpy of __src stays small). It pushes the MDS/collision down by its length.
            int funcLen = funcData?.Length ?? 0;
            if (funcData != null) part.AddRange(funcData);
            int mdsOff = part.Count;                                            // 0x160 + funcLen
            part.AddRange(kb);
            int collOff = 0, collLen = 0;
            if (collisionMds != null)
            {
                while ((part.Count & 0xF) != 0) part.Add(0);          // 16-align the collision block
                collOff = part.Count; collLen = collisionMds.Length;
                part.AddRange(collisionMds);
            }
            int psize = part.Count;
            byte[] pa = part.ToArray();
            foreach (int o in PartSizeFields) U32(pa, o, (uint)psize);
            U32(pa, PartMdsSizeField, (uint)kb.Length);
            if (funcData != null)
            {
                int src = (int)U32(pa, 4);                             // __src sub-block offset within the part (0xe0)
                U32(pa, PartMdsOffsetField, (uint)mdsOff);                  // part+0x48: MDS data offset, now past the func block
                U32(pa, src + 0x70, 0x80);                            // __src+0x70: func-data offset (= part 0x160, right after hdr)
                U32(pa, src + 0x74, (uint)(funcLen / EventFuncEntryStride));   // __src+0x74: entry count -> EdInitEventPoint loop bound
                U32(pa, src + 0x04, (uint)(0x80 + funcLen));          // __src+0x04: memcpy size must cover the func block
            }
            if (collisionMds != null)
            {
                U32(pa, PartCollisionOffsetField,  (uint)collOff);              // part+0x78: `_a` collision offset (overrides the PartSizeFields write)
                U32(pa, PartCollisionSizeField, (uint)collLen);             // part+0x7c: its size — LoadPTS loads it only when > 0
            }

            int blob = (int)Align(scn.Count, 16);
            while (scn.Count < blob) scn.Add(0);
            scn.AddRange(pa);
            byte[] outp = scn.ToArray();
            int ent = 0x10 + n * 0x30;
            byte[] pn = Encoding.Latin1.GetBytes(partName);
            for (int i = 0; i < 0x10; i++) outp[ent + i] = i < pn.Length ? pn[i] : (byte)0;
            U32(outp, ent + 0x10, (uint)blob); U32(outp, ent + 0x14, (uint)psize);
            U32(outp, 4, (uint)(n + 1));
            return outp;
        }

        // ── EPARTS_FUNC_DATA builders (one 0xC0 entry per event point) ──────────────────────────────────
        // Field map (RE'd; town-event-points.md): +0x10 func type, +0x18/+0x1c time window (HOURS 0-24,
        // ConvertTime'd -> rec TimeStart/End; [0,24] == always-on), +0x20 link id, +0x24 map flag, +0x30
        // anchor frame name (SearchFrame -> rec FramePtr gate), +0x40 pos (PART-LOCAL), +0x50 rot, +0x60
        // radius(3f, type-3 only), +0x70/+0x74 type-specific params.
        internal static byte[] BuildFuncEntry(int type, float t0, float t1, int link, int mapflag, string name,
                                     float[] pos, float[] rot, float[] radius, float p70, float p74)
        {
            var e = new byte[EventFuncEntryStride];
            void WriteF32(int o, float v) => Array.Copy(BitConverter.GetBytes(v), 0, e, o, 4);
            U32(e, 0x10, (uint)type);
            WriteF32(0x18, t0); WriteF32(0x1C, t1);
            U32(e, 0x20, (uint)link); U32(e, 0x24, (uint)mapflag);
            if (!string.IsNullOrEmpty(name))
            {
                byte[] nb = Encoding.Latin1.GetBytes(name);
                Array.Copy(nb, 0, e, 0x30, Math.Min(nb.Length, 0x1F));
            }
            WriteF32(0x40, pos[0]); WriteF32(0x44, pos[1]); WriteF32(0x48, pos[2]);
            WriteF32(0x50, rot[0]); WriteF32(0x54, rot[1]); WriteF32(0x58, rot[2]);
            if (radius != null) { WriteF32(0x60, radius[0]); WriteF32(0x64, radius[1]); WriteF32(0x68, radius[2]); }
            WriteF32(0x70, p70); WriteF32(0x74, p74);
            return e;
        }

        // Type-3 fishing trigger (func type 0x12): +0x70 = the SCRIPT LABEL id (fptosi'd, must be > 0),
        // +0x60 = trigger radius. Always-on ([0,24]); no frame gate.
        internal static byte[] BuildFishingFunc(float[] localPos, int label = FishingLabelId)
            => BuildFuncEntry(0x12, 0f, 24f, 0, 0, "", localPos, new[] { 0f, 0f, 0f },
                              new[] { 10f, 10f, 10f }, label, 0f);

        // Ladder climb pair: func 0x13 -> rec type-4 BOTTOM (climb-up), func 0x14 -> rec type-5 TOP
        // (climb-down), paired by link id. Radius is engine-fixed 6.0 for ladders. +0x74 = rung count
        // (mirrors native hasigo1: 12 bottom / 2 top). Gated to the ladder frame ("hasigo").
        internal static byte[] BuildLadderFunc()
        {
            var b = BuildFuncEntry(0x13, 0f, 24f, LadderLinkId, 0, "hasigo", LadderClimbBottom, LadderRotation, null, 0f, LadderRungsBottom);
            var t = BuildFuncEntry(0x14, 0f, 24f, LadderLinkId, 0, "hasigo", LadderClimbTop,    LadderRotation, null, 0f, LadderRungsTop);
            // Tide-message trigger co-located with the climb-down (TOP) end: a type-3 script point naming
            // label 402. CanalTide enables EITHER the ladder pair (low tide → climb) OR this point (high tide
            // → "tide too high" on X-press), never both. Radius 8 ≈ the ladder's fixed 6 so it fires where the
            // climb would. Mirrors the climb-down's "hasigo" frame + LadderClimbTop so it resolves to the same spot.
            var m = BuildFuncEntry(0x12, 0f, 24f, 0, 0, "hasigo", LadderClimbTop, LadderRotation, new[] { 8f, 8f, 8f }, LadderMsgLabelId, 0f);
            var outb = new byte[b.Length + t.Length + m.Length];
            Array.Copy(b, 0, outb, 0, b.Length); Array.Copy(t, 0, outb, b.Length, t.Length);
            Array.Copy(m, 0, outb, b.Length + t.Length, m.Length);
            return outb;
        }

        internal static byte[] BuildInjectedMapinfo(byte[] cfg, int x, int y, int z, int ry, string anchorPart, string atari = "",
                                           string partName = "kanban")
        {
            string t = Encoding.Latin1.GetString(cfg);
            // Slot 5 (after name + level1/2/3 + one blank) is the `_a` (atari/collision) mesh — matches how
            // native GROUND blocks reference e.g. "e03g04_a.mds".
            // Number format MUST match native exactly — "N,\tN,\tN" (comma immediately after each value,
            // THEN a tab). The earlier "N\t,N\t,N" (tab before comma) parsed positions but corrupted the
            // rotation Y for injected entries, leaving the canal sign stuck facing east regardless of ry.
            string blk = "\r\n\tGROUND\t\"" + partName + "\",\t\t//injected part\r\n"
                       + "\t\t\"\",\t\t\t//level1\r\n\t\t\"\",\t\t\t//level2\r\n\t\t\"\",\t\t\t//level3\r\n"
                       + "\t\t\"\",\t\t\t//\r\n\t\t\"" + atari + "\",\t\t\t//atari\r\n\t\t\"\",\t\t\t//\r\n\t\t\"\",\t\t\t//?\r\n"
                       + $"\t\t{x},\t{y},\t{z},\t//position\r\n\t\t0,\t{ry},\t0\t//rotation\r\n";
            var matches = Regex.Matches(t, "\\tGROUND\\t\"" + Regex.Escape(anchorPart) + "\",.*?\\r\\n\\t\\t-?\\d[^\\r\\n]*\\r\\n\\t\\t\\d[^\\r\\n]*,[^\\r\\n]*\\r\\n", RegexOptions.Singleline);
            if (matches.Count == 0) throw new IOException($"no GROUND {anchorPart} block found in mapinfo.cfg");
            int ins = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length;
            return Encoding.Latin1.GetBytes(t.Substring(0, ins) + blk + t.Substring(ins));
        }

        /// <summary>Carve a part's 0x160-byte PTS header out of a scene.scn (used as the kanban template).</summary>
        internal static byte[] PartHeader(byte[] scene, string partName)
        {
            int n = (int)U32(scene, 4);
            for (int i = 0; i < n; i++)
            {
                int e = 0x10 + i * 0x30;
                if (NameAt(scene, e, 0x10) == partName)
                {
                    int off = (int)U32(scene, e + 0x10);
                    return new ArraySegment<byte>(scene, off, 0x160).ToArray();
                }
            }
            throw new IOException($"template part {partName} not found in scene.scn");
        }
    }
}
