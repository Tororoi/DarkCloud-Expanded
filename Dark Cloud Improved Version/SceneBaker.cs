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
    /// scene.scn / mapinfo.cfg bakes: inject a kanban-style part (+ its EPARTS_FUNC_DATA event points and
    /// optional `_a` collision), per-town scene patches (Yellow Drops water/bank, Queens part swaps), the
    /// Brownboo edit-mode geometry cleanup, and the mapinfo placement/water tuning. All byte[] transforms.
    /// </summary>
    internal static class SceneBaker
    {
        // ── scene.scn: append a `kanban` PTS part cloned from s04a01, + a 26th part-table entry ──
        internal static readonly int[] SIZE_FIELDS = { 0x4C, 0x50, 0x54, 0x78, 0x90, 0xA8, 0xC0, 0xD8 };
        internal const int MDSSIZE_FIELD = 0x58;
        // LoadPTS (0x19f6f0) reads the `_a` collision variant's OFFSET at part+0x78 and its SIZE (gate) at
        // part+0x7c; if size>0 it feeds part+offset to LoadCollisionFile -> CreateCollisionMDT.
        internal const int COLL_OFF_FIELD = 0x78, COLL_SIZE_FIELD = 0x7C;
        internal const int MDS_OFF_FIELD  = 0x48;   // part+0x48 = MDS data offset (LoadPTS); shifts when func-data precedes the MDS

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

        /// <summary>Yellow Drops water raised 0 -> 4.25 (user request: the bank-notch lip height).
        /// Three coordinated levels: the mapinfo WATER_SURFACE plane rows (here), the suimenn visual
        /// sheet (RaiseYdSuimenn), and Spot 23's gameplay water (5.25 = surface + the same +1 the
        /// spot always used).</summary>
        internal const float YD_WATER_Y = 4.25f;

        internal static byte[] RaiseYdWater(byte[] mapinfo)
        {
            string txt = Encoding.Latin1.GetString(mapinfo);
            string oldMin = "\t\t\t-320, 0, -320,", newMin = "\t\t\t-320, 4.25, -320,";
            string oldMax = "\t\t\t320, 0, 320,",  newMax = "\t\t\t320, 4.25, 320,";
            if (txt.IndexOf(oldMin, StringComparison.Ordinal) < 0 || txt.IndexOf(oldMax, StringComparison.Ordinal) < 0)
                throw new Exception("YD water raise: WATER_SURFACE rows not found in s13 mapinfo");
            txt = txt.Replace(oldMin, newMin).Replace(oldMax, newMax);
            Console.WriteLine($"   YD water surface: WATER_SURFACE raised to {YD_WATER_Y}");
            return Encoding.Latin1.GetBytes(txt);
        }

        /// <summary>Raise the suimenn visual sheet (s1302, town-wide yellow liquid) by writing the
        /// node's matrix Y translation (entry 1, sub-relative 0x244). Guarded on the vanilla ~0 value.</summary>
        internal static byte[] RaiseYdSuimenn(byte[] scene)
        {
            int n = (int)U32(scene, 4);
            for (int i = 0; i < n; i++)
            {
                int ent = 0x10 + i * 0x30;
                if (Encoding.Latin1.GetString(scene, ent, 6) != "s1302\0") continue;
                int off = (int)U32(scene, ent + 0x10) + 0x244;
                float cur = BitConverter.ToSingle(scene, off);
                if (Math.Abs(cur) > 0.001f)
                    throw new Exception($"YD water raise: suimenn Ty is {cur}, expected ~0 — layout drift");
                Array.Copy(BitConverter.GetBytes(YD_WATER_Y), 0, scene, off, 4);
                Console.WriteLine($"   YD water surface: suimenn sheet raised to {YD_WATER_Y}");
                return scene;
            }
            throw new Exception("YD water raise: s1302 not found");
        }

        /// <summary>Queens georama-part subfile swaps (Resources/isoPatch/queens_parts.bin, built by
        /// tools/export_queens_parts.py: u32 count; per part name[8] + u32 origSize + u32 newSize +
        /// bytes 16-aligned). Currently e03h06: `_c` camera hull doubled in height + `_a` player
        /// collision replaced with the full visual mesh split into sub-200-poly nodes. Each rebuilt
        /// sub is appended to scene.scn and its directory entry repointed; guarded on the original
        /// sub size. Missing bin = skip (vanilla part collision stays).</summary>
        internal static byte[] ApplyQueensPartSwaps(byte[] scene)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "isoPatch", "queens_parts.bin");
            if (!File.Exists(path))
            {
                Console.WriteLine("   queens_parts.bin missing (tools/export_queens_parts.py) — vanilla part collision stays");
                return scene;
            }
            byte[] bin = File.ReadAllBytes(path);
            int nparts = BitConverter.ToInt32(bin, 0);
            int rp = 4;
            var scn = new List<byte>(scene);
            int n = (int)U32(scene, 4);
            for (int k = 0; k < nparts; k++)
            {
                string name = Encoding.Latin1.GetString(bin, rp, 8).TrimEnd('\0');
                int origSize = BitConverter.ToInt32(bin, rp + 8);
                int newSize = BitConverter.ToInt32(bin, rp + 12);
                rp += 16;
                byte[] rebuilt = new byte[newSize];
                Array.Copy(bin, rp, rebuilt, 0, newSize);
                rp += newSize + ((-newSize) % 16 + 16) % 16;
                byte[] cur = scn.ToArray();
                int ent = -1;
                for (int i = 0; i < n; i++)
                    if (Encoding.Latin1.GetString(cur, 0x10 + i * 0x30, name.Length + 1) == name + "\0")
                    { ent = 0x10 + i * 0x30; break; }
                if (ent < 0) throw new Exception($"part swap: {name} not in e03 scene directory");
                if (U32(cur, ent + 0x14) != (uint)origSize)
                    throw new Exception($"part swap: {name} size {U32(cur, ent + 0x14)} != expected {origSize} — regenerate the bin");
                int blob = (int)Align(scn.Count, 16);
                while (scn.Count < blob) scn.Add(0);
                scn.AddRange(rebuilt);
                byte[] outp = scn.ToArray();
                U32(outp, ent + 0x10, (uint)blob); U32(outp, ent + 0x14, (uint)newSize);
                scn = new List<byte>(outp);
                Console.WriteLine($"   {name}: rebuilt collision swapped in ({newSize} bytes @0x{blob:x})");
            }
            return scn.ToArray();
        }

        /// <summary>
        /// Yellow Drops WEST-BANK BULGE (smoothed, 2x station density). The subdivided bank grows
        /// the grid10/grid11 visual MDTs plus the s1301_a crown wall and s1301_c camera wall, so a
        /// float-patch can't carry it — instead the ENTIRE s1301 subfile is rebuilt offline
        /// (tools/westbank_smooth_bake.py -> Resources/isoPatch/s1301_smooth.bin: re-laid nested
        /// MDS blocks, edge-split + sine-shifted geometry, verified byte-identical everywhere else)
        /// and swapped in here: the new sub is appended to scene.scn and the s1301 directory entry
        /// repointed at it (old bytes become dead space; the DATA.DAT tail copy absorbs the growth).
        /// Guarded on the original sub's size so a foreign scene fails loudly. Missing bin = skip.
        /// </summary>
        internal static byte[] ReplaceS13Ground(byte[] scene)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "isoPatch", "s1301_smooth.bin");
            if (!File.Exists(path))
            {
                Console.WriteLine("   s1301_smooth.bin missing (tools/westbank_smooth_bake.py) — bank stays vanilla");
                return scene;
            }
            byte[] rebuilt = File.ReadAllBytes(path);
            int n = (int)U32(scene, 4);
            int ent = -1;
            for (int i = 0; i < n; i++)
                if (Encoding.Latin1.GetString(scene, 0x10 + i * 0x30, 6) == "s1301\0") { ent = 0x10 + i * 0x30; break; }
            if (ent < 0) throw new Exception("s1301 not found in s13 scene directory");
            uint oldSize = U32(scene, ent + 0x14);
            if (oldSize != 0x4ca50)
                throw new Exception($"s1301 size 0x{oldSize:x} != expected 0x4ca50 — regenerate s1301_smooth.bin");
            var scn = new List<byte>(scene);
            int blob = (int)Align(scn.Count, 16);
            while (scn.Count < blob) scn.Add(0);
            scn.AddRange(rebuilt);
            byte[] outp = scn.ToArray();
            U32(outp, ent + 0x10, (uint)blob);
            U32(outp, ent + 0x14, (uint)rebuilt.Length);
            Console.WriteLine($"   s1301 replaced with smoothed west bank ({rebuilt.Length} bytes @0x{blob:x})");
            return outp;
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
            foreach (int o in SIZE_FIELDS) U32(pa, o, (uint)psize);
            U32(pa, MDSSIZE_FIELD, (uint)kb.Length);
            if (funcData != null)
            {
                int src = (int)U32(pa, 4);                             // __src sub-block offset within the part (0xe0)
                U32(pa, MDS_OFF_FIELD, (uint)mdsOff);                  // part+0x48: MDS data offset, now past the func block
                U32(pa, src + 0x70, 0x80);                            // __src+0x70: func-data offset (= part 0x160, right after hdr)
                U32(pa, src + 0x74, (uint)(funcLen / FUNC_STRIDE));   // __src+0x74: entry count -> EdInitEventPoint loop bound
                U32(pa, src + 0x04, (uint)(0x80 + funcLen));          // __src+0x04: memcpy size must cover the func block
            }
            if (collisionMds != null)
            {
                U32(pa, COLL_OFF_FIELD,  (uint)collOff);              // part+0x78: `_a` collision offset (overrides the SIZE_FIELDS write)
                U32(pa, COLL_SIZE_FIELD, (uint)collLen);             // part+0x7c: its size — LoadPTS loads it only when > 0
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
            var e = new byte[FUNC_STRIDE];
            void F(int o, float v) => Array.Copy(BitConverter.GetBytes(v), 0, e, o, 4);
            U32(e, 0x10, (uint)type);
            F(0x18, t0); F(0x1C, t1);
            U32(e, 0x20, (uint)link); U32(e, 0x24, (uint)mapflag);
            if (!string.IsNullOrEmpty(name))
            {
                byte[] nb = Encoding.Latin1.GetBytes(name);
                Array.Copy(nb, 0, e, 0x30, Math.Min(nb.Length, 0x1F));
            }
            F(0x40, pos[0]); F(0x44, pos[1]); F(0x48, pos[2]);
            F(0x50, rot[0]); F(0x54, rot[1]); F(0x58, rot[2]);
            if (radius != null) { F(0x60, radius[0]); F(0x64, radius[1]); F(0x68, radius[2]); }
            F(0x70, p70); F(0x74, p74);
            return e;
        }

        // Type-3 fishing trigger (func type 0x12): +0x70 = the SCRIPT LABEL id (fptosi'd, must be > 0),
        // +0x60 = trigger radius. Always-on ([0,24]); no frame gate.
        internal static byte[] BuildFishingFunc(float[] localPos, int label = FISH_LABEL)
            => BuildFuncEntry(0x12, 0f, 24f, 0, 0, "", localPos, new[] { 0f, 0f, 0f },
                              new[] { 10f, 10f, 10f }, label, 0f);

        // Ladder climb pair: func 0x13 -> rec type-4 BOTTOM (climb-up), func 0x14 -> rec type-5 TOP
        // (climb-down), paired by link id. Radius is engine-fixed 6.0 for ladders. +0x74 = rung count
        // (mirrors native hasigo1: 12 bottom / 2 top). Gated to the ladder frame ("hasigo").
        internal static byte[] BuildLadderFunc()
        {
            var b = BuildFuncEntry(0x13, 0f, 24f, LAD_LINK, 0, "hasigo", LAD_BOTTOM, LAD_FACE, null, 0f, LAD_RUNGS_BOT);
            var t = BuildFuncEntry(0x14, 0f, 24f, LAD_LINK, 0, "hasigo", LAD_TOP,    LAD_FACE, null, 0f, LAD_RUNGS_TOP);
            // Tide-message trigger co-located with the climb-down (TOP) end: a type-3 script point naming
            // label 402. CanalTide enables EITHER the ladder pair (low tide → climb) OR this point (high tide
            // → "tide too high" on X-press), never both. Radius 8 ≈ the ladder's fixed 6 so it fires where the
            // climb would. Mirrors the climb-down's "hasigo" frame + LAD_TOP so it resolves to the same spot.
            var m = BuildFuncEntry(0x12, 0f, 24f, 0, 0, "hasigo", LAD_TOP, LAD_FACE, new[] { 8f, 8f, 8f }, LADDER_MSG_LABEL, 0f);
            var outb = new byte[b.Length + t.Length + m.Length];
            Array.Copy(b, 0, outb, 0, b.Length); Array.Copy(t, 0, outb, b.Length, t.Length);
            Array.Copy(m, 0, outb, b.Length + t.Length, m.Length);
            return outb;
        }

        // ── scene.scn: enable backface culling on Brownboo's upper crater walls (edit-mode view fix) ──
        // The crater wall is a vertical stack of `s04g01NN__X` mesh nodes. At MDS load the engine's
        // SetFrameAttr reads the node-name suffix after "__" and turns each letter into a render flag:
        // 's' enables backface culling (single-sided), 'n' leaves it off (two-sided). The artist tagged the
        // lower rings (Y 0..300) `__s` but the upper rings (Y 300..1200) `__n`, so the upper walls draw
        // double-sided — their inward-facing back faces show through as stray geometry that hides the town
        // from an overhead edit-mode camera. Flipping the 12 upper nodes' suffix to `__s` makes them
        // attribute-identical to the (correctly culled) lower rings. One byte per node; geometry unchanged.
        internal static readonly string[] UPPER_WALL_NODES = {
            "s04g0105__n", "s04g0106__n", "s04g0107__n", "s04g0108__n", "s04g0109__n", "s04g0110__n",
            "s04g0111__n", "s04g0112__n", "s04g0113__n", "s04g0114__n", "s04g0115__n", "s04g0116__n",
        };

        internal static byte[] CullUpperCraterWalls(byte[] scene)
        {
            foreach (string node in UPPER_WALL_NODES)
            {
                byte[] key = Encoding.Latin1.GetBytes(node + "\0");   // the null-terminated node-name field
                int at = Find(scene, key);
                if (at < 0) throw new IOException($"crater-wall node '{node}' not found in scene.scn");
                scene[at + node.Length - 1] = (byte)'s';              // trailing 'n' -> 's' (culling on)
            }
            return scene;
        }

        // ── scene.scn: make Brownboo's houses single-sided so the camera, when it ends up INSIDE a house, sees
        //    straight through it instead of hitting the near walls (the camera already clips in; the problem is
        //    the occlusion). Same SetFrameAttr suffix mechanism as the crater walls — the '__s' suffix turns on
        //    backface culling, so a wall viewed from inside (its exterior face pointing away) is culled and the
        //    whole house becomes see-through from within, while looking identical from outside. h0201/h0202 are
        //    already '__s'; the '__n' houses flip to '__s'; the suffix-less houses get a '__s' written into the
        //    16-byte name field's null padding (verified all-zero, so no bytes shift).
        //    (Briefly retired 2026-08 for a custom s04g01_v camera-collision rebuild — that experiment was
        //    reverted: camera clipping persisted even with per-leg collision nodes; see brownboo_camera_collision.)
        internal static byte[] CullBuildings(byte[] scene)
        {
            foreach (string node in new[] { "h0101__n", "h0102__n", "h0103__n" })   // '__n' -> '__s'
            {
                int at = Find(scene, Encoding.Latin1.GetBytes(node + "\0"));
                if (at < 0) throw new IOException($"building node '{node}' not found in scene.scn");
                scene[at + node.Length - 1] = (byte)'s';
            }
            foreach (var (node, expect) in new[] { ("h0104", 1), ("h0301", 3), ("h0302", 3) })  // append '__s'
            {
                byte[] key = Encoding.Latin1.GetBytes(node + "\0");
                byte[] suf = Encoding.Latin1.GetBytes("__s\0");
                int from = 0, hits = 0, at;
                while ((at = FindFrom(scene, key, from)) >= 0)
                {
                    Array.Copy(suf, 0, scene, at + node.Length, suf.Length);   // overwrite '\0' + padding
                    from = at + node.Length; hits++;
                }
                if (hits != expect) throw new IOException($"building node '{node}': found {hits}, expected {expect}");
            }
            return scene;
        }

        // ── scene.scn: delete stray horizontal triangles that a top-down edit camera sees (edit-mode view fix) ──
        // Two sets, both up-facing horizontal triangles sitting outside the town, so they stay visible from an
        // overhead edit camera even after the vertical walls cull:
        //   • Each square crater ring carries 4 tiny corner-fill triangles at its (±500,±500) corners. A ring's
        //     ONLY up-facing tris are those 4 corners, so we remove every up-facing tri (yMax = +inf).
        //   • The crater floors s04g0117__s and s04g0117__s1 (the pond bottom) are genuinely horizontal surfaces
        //     (Y 0..76 / mostly up-facing) but each ALSO has 2 sunken corner strays down at Y=-100. For them we
        //     remove up-facing tris only BELOW Y=-50, catching just those without touching the real floor.
        //     Together the two nodes hold all 4 crater-floor corners.
        // Each such tri lives in a primType-3 triangle LIST (independent tris), so collapsing its two trailing
        // index-records onto the first yields a zero-area triangle the GS discards — no strip/layout disturbance.
        // Record stride is 3 or 4 ints depending on whether the mesh carries a per-vertex colour block (see the
        // stride computed below); s04g0117__s1 is a 4-int-record "variant" mesh. Must run BEFORE
        // CullUpperCraterWalls (which renames the upper rings' `__n` suffix to `__s`).
        internal static readonly (string node, double yMax)[] CORNER_TRI_NODES = {
            ("s040101__s", 1e9), ("s04g0102__s", 1e9), ("s04g0103__s", 1e9), ("s04g0104__s", 1e9),
            ("s04g0105__n", 1e9), ("s04g0106__n", 1e9), ("s04g0107__n", 1e9), ("s04g0108__n", 1e9),
            ("s04g0109__n", 1e9), ("s04g0110__n", 1e9), ("s04g0111__n", 1e9), ("s04g0112__n", 1e9),
            ("s04g0113__n", 1e9), ("s04g0114__n", 1e9), ("s04g0115__n", 1e9), ("s04g0116__n", 1e9),
            ("s04g0117__s", -50.0), ("s04g0117__s1", -50.0),
        };

        internal static byte[] RemoveRingCornerTris(byte[] scene)
        {
            foreach (var (node, yMax) in CORNER_TRI_NODES)
            {
                int mdt = FindMdt(scene, node);
                uint dl = BitConverter.ToUInt32(scene, mdt + 10 * 4);
                int vcount = (int)BitConverter.ToUInt32(scene, mdt + 3 * 4);           // hw[3] = vertex count
                int vbase = mdt + (int)BitConverter.ToUInt32(scene, mdt + 4 * 4);      // hw[4] = vertex-block offset
                uint hw8 = BitConverter.ToUInt32(scene, mdt + 8 * 4);                  // colour block offset, or 0xffffffff
                int rb = (hw8 != 0xffffffff && hw8 > 0 ? 4 : 3) * 4;                   // record size in bytes (4-int if colour)
                int numsub = (int)BitConverter.ToUInt32(scene, (int)(mdt + dl + 8));   // submesh count
                int o = (int)(dl + 0x10);
                for (int sm = 0; sm < numsub; sm++)
                {
                    int prim = BitConverter.ToInt32(scene, mdt + o);
                    int vcnt = BitConverter.ToInt32(scene, mdt + o + 4);
                    o += 0xC;
                    int recbase = mdt + o;                                             // first index-record of this submesh
                    o += vcnt * rb;
                    if (prim != 3) continue;                                           // only the triangle LIST holds them
                    for (int k = 0; k + 2 < vcnt; k += 3)
                    {
                        int i0 = BitConverter.ToInt32(scene, recbase + (k + 0) * rb);
                        int i1 = BitConverter.ToInt32(scene, recbase + (k + 1) * rb);
                        int i2 = BitConverter.ToInt32(scene, recbase + (k + 2) * rb);
                        if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vcount || i1 >= vcount || i2 >= vcount) continue;
                        if (i0 == i1 || i1 == i2 || i0 == i2) continue;
                        double cyc = (F(scene, vbase + i0 * 0x10 + 4) + F(scene, vbase + i1 * 0x10 + 4) + F(scene, vbase + i2 * 0x10 + 4)) / 3.0;
                        if (cyc >= yMax) continue;                                      // only strays below the cutoff
                        if (!UpFacing(scene, vbase, i0, i1, i2)) continue;
                        U32(scene, recbase + (k + 1) * rb, (uint)i0);                  // collapse -> zero-area tri
                        U32(scene, recbase + (k + 2) * rb, (uint)i0);
                    }
                }
            }
            return scene;
        }

        // True if triangle (i0,i1,i2) faces straight up. Verts are LOCAL XYZW floats at vbase + idx*0x10;
        // these nodes have identity rotation, so a local +Y normal is a world +Y normal.
        internal static bool UpFacing(byte[] s, int vbase, int i0, int i1, int i2)
        {
            float ax = F(s, vbase + i0 * 0x10), ay = F(s, vbase + i0 * 0x10 + 4), az = F(s, vbase + i0 * 0x10 + 8);
            float bx = F(s, vbase + i1 * 0x10), by = F(s, vbase + i1 * 0x10 + 4), bz = F(s, vbase + i1 * 0x10 + 8);
            float cx = F(s, vbase + i2 * 0x10), cy = F(s, vbase + i2 * 0x10 + 4), cz = F(s, vbase + i2 * 0x10 + 8);
            double nx = (by - ay) * (cz - az) - (bz - az) * (cy - ay);
            double ny = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
            double nz = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return len > 0 && ny / len > 0.9;
        }

        // Tune the canal water refraction via the e03 mapinfo (pure data). Kept CAMERA-FOLLOWING (last-3
        // params `1, 0, 0` = per-axis follow flags → body+0x24/28/2c; `1` on X keeps the plane small and
        // centred on the view along the canal). World-anchoring was tried and REVERTED: a fixed plane over
        // the whole 2100-unit canal gave unacceptable directional refraction STRETCH (elongated cells +
        // grazing angles). Camera-following keeps the covered area small (±320/±70), making a square grid
        // possible. Changes from vanilla:
        //   • Grid 48x16 → 64x14. X is HARD-CAPPED at 64 by CreateVUData (indexes its scratch by column*256
        //     floats into a 16384-float buffer = exactly 64 columns; more overflows/crashes). Over the
        //     ±320/±70 window that's 640/64 = 10 u/cell in X and 140/14 = 10 u/cell in Z → SQUARE 10x10
        //     cells = the finest no-stretch grid at this coverage (finer would need a smaller window).
        //   • p4 (4th param) kept at vanilla 2.0. p4 = the REFRACTION-OFFSET SCALE (fbCoord = base + p4*wobble,
        //     CreateVUData @0x160b38). It scales the refraction strength AND the above-water edge-pull (Toan's
        //     head) — screen-space refraction can't be depth-masked on PS2, so p4 is the only lever (lower =
        //     subtler distortion + less edge-pull; 1.0 = fountain parity). Now that the jitter is handled by
        //     the Y offset, kept at the full vanilla 2.0 for the strongest look; lower here to taste.
        //   • No poke sources — fixed-cell WATER_SHAKE reads as nothing on a camera-relative grid; removed.
        //     Just the vanilla gentle ambient wander.
        // The Z-fight jitter (mizu mesh vs refraction at the same tide Y) is handled by CanalTide.Refraction
        // YOffset. Corners/pos/colour/follow-flags otherwise unchanged from vanilla. Guarded: one match.
        internal static byte[] TuneCanalWater(byte[] cfg)
        {
            string t = Encoding.Latin1.GetString(cfg);
            const string OLD =
                "WATER_SURFACE \"\",48, 16,\r\n" +
                "\t\t\t-320, 0, -70,\r\n\t\t\t320, 0, 70,\r\n\t\t\t0, 31, 0,\r\n" +
                "\t\t\t0.1, 0.015, 0.0, 2.0,\r\n\t\t\t128, 128, 128,\r\n\t\t\t1, 0, 0\r\n" +
                "\tWATER_SHAKE\t-1, -1, -0.5, 0.0";
            const string NEW =
                "WATER_SURFACE \"\",64, 14,\r\n" +                         // finest no-stretch grid (X cap = 64)
                "\t\t\t-320, 0, -70,\r\n\t\t\t320, 0, 70,\r\n\t\t\t0, 31, 0,\r\n" +
                "\t\t\t0.1, 0.015, 0.0, 2.0,\r\n\t\t\t128, 128, 128,\r\n\t\t\t1, 0, 0\r\n" +
                "\tWATER_SHAKE\t-1, -1, -0.5, 0.0";
            int n = 0, idx = 0;
            while ((idx = t.IndexOf(OLD, idx, StringComparison.Ordinal)) >= 0) { n++; idx += OLD.Length; }
            if (n != 1)
                throw new IOException($"Canal WATER_SURFACE block found {n} times in e03 mapinfo (expected 1).");
            return Encoding.Latin1.GetBytes(t.Replace(OLD, NEW));
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
