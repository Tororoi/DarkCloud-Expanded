using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The per-town scene.scn / mapinfo.cfg bakes (split from SceneBaker 2026-09): Yellow Drops water +
    /// west-bank ground swap, Queens georama-part swaps + canal-water tuning, and the Brownboo edit-mode
    /// geometry cleanup (crater-wall culling, see-through houses, stray corner tris). All byte[] transforms,
    /// consumed by IsoPatcher.ApplySignPatch.
    /// </summary>
    internal static class TownSceneBakes
    {
        /// <summary>Yellow Drops water raised 0 -> 4.25 (user request: the bank-notch lip height).
        /// Three coordinated levels: the mapinfo WATER_SURFACE plane rows (here), the suimenn visual
        /// sheet (RaiseYellowDropsSurfaceMesh), and Spot 23's gameplay water (5.25 = surface + the same +1 the
        /// spot always used).</summary>
        internal const float YellowDropsWaterY = 4.25f;

        internal static byte[] RaiseYellowDropsWaterPlane(byte[] mapinfo)
        {
            string txt = Encoding.Latin1.GetString(mapinfo);
            string oldMin = "\t\t\t-320, 0, -320,", newMin = "\t\t\t-320, 4.25, -320,";
            string oldMax = "\t\t\t320, 0, 320,",  newMax = "\t\t\t320, 4.25, 320,";
            if (txt.IndexOf(oldMin, StringComparison.Ordinal) < 0 || txt.IndexOf(oldMax, StringComparison.Ordinal) < 0)
                throw new Exception("YD water raise: WATER_SURFACE rows not found in s13 mapinfo");
            txt = txt.Replace(oldMin, newMin).Replace(oldMax, newMax);
            Console.WriteLine($"   YD water surface: WATER_SURFACE raised to {YellowDropsWaterY}");
            return Encoding.Latin1.GetBytes(txt);
        }

        /// <summary>Raise the suimenn visual sheet (s1302, town-wide yellow liquid) by writing the
        /// node's matrix Y translation (entry 1, sub-relative 0x244). Guarded on the vanilla ~0 value.</summary>
        internal static byte[] RaiseYellowDropsSurfaceMesh(byte[] scene)
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
                Array.Copy(BitConverter.GetBytes(YellowDropsWaterY), 0, scene, off, 4);
                Console.WriteLine($"   YD water surface: suimenn sheet raised to {YellowDropsWaterY}");
                return scene;
            }
            throw new Exception("YD water raise: s1302 not found");
        }

        /// <summary>Queens georama-part subfile swaps (Resources/isoPatch/queens_parts.bin, built by
        /// tools/queens/queens_snake_statue_collision.py: u32 count; per part name[8] + u32 origSize + u32 newSize +
        /// bytes 16-aligned). Currently e03h06: `_c` camera hull doubled in height + `_a` player
        /// collision replaced with the full visual mesh split into sub-200-poly nodes. Each rebuilt
        /// sub is appended to scene.scn and its directory entry repointed; guarded on the original
        /// sub size. Missing bin = skip (vanilla part collision stays).</summary>
        internal static byte[] ApplyQueensPartSwaps(byte[] scene)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "isoPatch", "queens_parts.bin");
            if (!File.Exists(path))
            {
                Console.WriteLine("   queens_parts.bin missing (tools/queens/queens_snake_statue_collision.py) — vanilla part collision stays");
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
        /// (tools/yellowdrops/bake_yellowdrops_westbank.py -> Resources/isoPatch/yellowdrops_westbank_ground.bin: re-laid nested
        /// MDS blocks, edge-split + sine-shifted geometry, verified byte-identical everywhere else)
        /// and swapped in here: the new sub is appended to scene.scn and the s1301 directory entry
        /// repointed at it (old bytes become dead space; the DATA.DAT tail copy absorbs the growth).
        /// Guarded on the original sub's size so a foreign scene fails loudly. Missing bin = skip.
        /// </summary>
        internal static byte[] ReplaceYellowDropsGround(byte[] scene)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "isoPatch", "yellowdrops_westbank_ground.bin");
            if (!File.Exists(path))
            {
                Console.WriteLine("   yellowdrops_westbank_ground.bin missing (tools/yellowdrops/bake_yellowdrops_westbank.py) — bank stays vanilla");
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
                throw new Exception($"s1301 size 0x{oldSize:x} != expected 0x4ca50 — regenerate yellowdrops_westbank_ground.bin");
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
        // ── scene.scn: enable backface culling on Brownboo's upper crater walls (edit-mode view fix) ──
        // The crater wall is a vertical stack of `s04g01NN__X` mesh nodes. At MDS load the engine's
        // SetFrameAttr reads the node-name suffix after "__" and turns each letter into a render flag:
        // 's' enables backface culling (single-sided), 'n' leaves it off (two-sided). The artist tagged the
        // lower rings (Y 0..300) `__s` but the upper rings (Y 300..1200) `__n`, so the upper walls draw
        // double-sided — their inward-facing back faces show through as stray geometry that hides the town
        // from an overhead edit-mode camera. Flipping the 12 upper nodes' suffix to `__s` makes them
        // attribute-identical to the (correctly culled) lower rings. One byte per node; geometry unchanged.
        internal static readonly string[] BrownbooUpperWallNodes = {
            "s04g0105__n", "s04g0106__n", "s04g0107__n", "s04g0108__n", "s04g0109__n", "s04g0110__n",
            "s04g0111__n", "s04g0112__n", "s04g0113__n", "s04g0114__n", "s04g0115__n", "s04g0116__n",
        };

        internal static byte[] CullUpperCraterWalls(byte[] scene)
        {
            foreach (string node in BrownbooUpperWallNodes)
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
        internal static readonly (string node, double yMax)[] BrownbooCornerTriNodes = {
            ("s040101__s", 1e9), ("s04g0102__s", 1e9), ("s04g0103__s", 1e9), ("s04g0104__s", 1e9),
            ("s04g0105__n", 1e9), ("s04g0106__n", 1e9), ("s04g0107__n", 1e9), ("s04g0108__n", 1e9),
            ("s04g0109__n", 1e9), ("s04g0110__n", 1e9), ("s04g0111__n", 1e9), ("s04g0112__n", 1e9),
            ("s04g0113__n", 1e9), ("s04g0114__n", 1e9), ("s04g0115__n", 1e9), ("s04g0116__n", 1e9),
            ("s04g0117__s", -50.0), ("s04g0117__s1", -50.0),
        };

        internal static byte[] RemoveRingCornerTris(byte[] scene)
        {
            foreach (var (node, yMax) in BrownbooCornerTriNodes)
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
                        double cyc = (F32(scene, vbase + i0 * 0x10 + 4) + F32(scene, vbase + i1 * 0x10 + 4) + F32(scene, vbase + i2 * 0x10 + 4)) / 3.0;
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
            float ax = F32(s, vbase + i0 * 0x10), ay = F32(s, vbase + i0 * 0x10 + 4), az = F32(s, vbase + i0 * 0x10 + 8);
            float bx = F32(s, vbase + i1 * 0x10), by = F32(s, vbase + i1 * 0x10 + 4), bz = F32(s, vbase + i1 * 0x10 + 8);
            float cx = F32(s, vbase + i2 * 0x10), cy = F32(s, vbase + i2 * 0x10 + 4), cz = F32(s, vbase + i2 * 0x10 + 8);
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
        // The Z-fight jitter (mizu mesh vs refraction at the same tide Y) is handled by CanalWaterEffects.Refraction
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
    }
}
