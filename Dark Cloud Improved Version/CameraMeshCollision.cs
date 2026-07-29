using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Build a CCollisionMDT the town camera can gather, from an arbitrary triangle list — the "custom
    /// function passed the meshes". The camera's gather (PickUpNearPoly__CCollisionMDT) iterates a flat poly
    /// list; a visible mesh (CVisualMDTVu1) can't be gathered because its vtable+0x1c is DrawVu1 (renderer),
    /// not a gather. So we materialise triangles into the CCollisionMDT format once, in a cave, and point a
    /// part's CAMERA frame at it. Layout RE'd from CreateCollisionMDT (0x127250) / PickUpNearPoly__CCollisionMDT:
    ///   object 0x40: +0x00 bboxMin(xyzw) +0x10 bboxMax +0x20 vtable(__vt__CCollisionMDT) +0x30 MDT(unused by
    ///                gather) +0x34 polyListPtr +0x38 polyCount
    ///   poly   0x70: +0x00 v0 +0x10 v1 +0x20 v2 +0x30 normal +0x40 colour +0x50 bboxMax +0x60 bboxMin
    /// Verts are LOCAL to the part frame (PickUpCameraPoly SetPosition/SetRotation the frame to the part, then
    /// the gather works in local space and transforms hits back to world).
    ///
    /// STATUS: builder + box PROOF only. `WireProof` points ONE part's collision frame at a test box so we can
    /// confirm the camera gathers a hand-built CCollisionMDT (camera should hug a box at that part). The real
    /// feed (walk part+0xc4 -> CVisualMDTVu1 -> MDT -> parse tris) and a dedicated camera frame come next.
    /// </summary>
    internal static class CameraMeshCollision
    {
        internal static bool Enabled = false;      // OFF by default — proof only
        internal static bool ProofBox = true;      // build a test box and wire it to ProofPartIndex
        internal static int  ProofPartIndex = 0;   // which active MAP part to test

        private const long EditGroundPtr = 0x21D1968C;
        private const int  MapBase = 0x30, MapStride = 0x2A0, MapCount = 0x80, MapActive = 0xE8, MapCamFrame = 0xDC, MapPlayerFrame = 0xD0;

        // free proven-clean cave (CodeCaves heap tail: 0x21FAE400 .. 0x21FB4000, ~0x5C00 bytes)
        private const long Cave = 0x21FAE400;
        private const long VtCCollisionMDT = 0x002A10D0;   // __vt__13CCollisionMDT (stored at object+0x20)

        private static long Guest(long mmu) => mmu & 0x1FFFFFFF;   // pointers stored in game structs are guest-space

        private static bool _done;

        internal static void Tick()
        {
            if (!Enabled || _done) return;
            uint egp = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egp)) { _done = false; return; }
            long eg = Memory.ToMmu(egp);

            if (ProofBox) WireProof(eg);
            _done = true;
        }

        // Proof: build a tall box collision in the cave and point the chosen part's collision frame at it.
        // (Temporarily overwrites the frame's geometry pointer at frame+0x4 — this breaks that part's player
        // collision for the test; it's only to verify the camera gathers our CCollisionMDT.)
        private static void WireProof(long eg)
        {
            long part = eg + MapBase + (long)ProofPartIndex * MapStride;
            if (Memory.ReadInt(part + MapActive) < 0) return;
            int frame = Memory.ReadInt(part + MapPlayerFrame);
            if (frame == 0) return;

            // a 20x120x20 box in LOCAL space (part origin), tall enough for the camera
            float[][] box = Box(-10f, 10f, 0f, 120f, -10f, 10f);
            long obj = Cave;
            BuildCollisionMDT(obj, box);

            // point BOTH collision frames at our object (player frame drives the alias'd camera frame too)
            long frameMmu = Memory.ToMmu((uint)(frame & (int)Memory.PhysAddrMask));
            Memory.WriteInt(frameMmu + 0x4, (int)Guest(obj));

            Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[CamMesh] wired test box CCollisionMDT @0x{Guest(obj):X8} to MAP[{ProofPartIndex}] frame 0x{frame:X8}");
        }

        // Write a CCollisionMDT (object + poly list) at mmu address `at`; tris = array of [x0,y0,z0,x1,y1,z1,x2,y2,z2].
        internal static int BuildCollisionMDT(long at, float[][] tris)
        {
            long polyList = at + 0x40;
            float minx = 1e9f, miny = 1e9f, minz = 1e9f, maxx = -1e9f, maxy = -1e9f, maxz = -1e9f;

            for (int i = 0; i < tris.Length; i++)
            {
                float[] t = tris[i];
                long p = polyList + (long)i * 0x70;
                for (int v = 0; v < 3; v++)
                {
                    Memory.WriteFloat(p + v * 0x10 + 0, t[v * 3 + 0]);
                    Memory.WriteFloat(p + v * 0x10 + 4, t[v * 3 + 1]);
                    Memory.WriteFloat(p + v * 0x10 + 8, t[v * 3 + 2]);
                    Memory.WriteFloat(p + v * 0x10 + 12, 1f);
                }
                // normal = normalize((v1-v0) x (v2-v0))
                float ax = t[3]-t[0], ay = t[4]-t[1], az = t[5]-t[2];
                float bx = t[6]-t[0], by = t[7]-t[1], bz = t[8]-t[2];
                float nx = ay*bz - az*by, ny = az*bx - ax*bz, nz = ax*by - ay*bx;
                float nl = (float)Math.Sqrt(nx*nx + ny*ny + nz*nz); if (nl < 1e-6f) nl = 1f;
                Memory.WriteFloat(p + 0x30, nx/nl); Memory.WriteFloat(p + 0x34, ny/nl); Memory.WriteFloat(p + 0x38, nz/nl); Memory.WriteFloat(p + 0x3C, 0f);
                Memory.WriteInt(p + 0x40, 0); Memory.WriteInt(p + 0x44, 0); Memory.WriteInt(p + 0x48, 0); Memory.WriteInt(p + 0x4C, 0);   // colour

                float px0 = Math.Min(t[0], Math.Min(t[3], t[6])), py0 = Math.Min(t[1], Math.Min(t[4], t[7])), pz0 = Math.Min(t[2], Math.Min(t[5], t[8]));
                float px1 = Math.Max(t[0], Math.Max(t[3], t[6])), py1 = Math.Max(t[1], Math.Max(t[4], t[7])), pz1 = Math.Max(t[2], Math.Max(t[5], t[8]));
                Memory.WriteFloat(p + 0x50, px1); Memory.WriteFloat(p + 0x54, py1); Memory.WriteFloat(p + 0x58, pz1); Memory.WriteFloat(p + 0x5C, 1f);
                Memory.WriteFloat(p + 0x60, px0); Memory.WriteFloat(p + 0x64, py0); Memory.WriteFloat(p + 0x68, pz0); Memory.WriteFloat(p + 0x6C, 1f);

                minx = Math.Min(minx, px0); miny = Math.Min(miny, py0); minz = Math.Min(minz, pz0);
                maxx = Math.Max(maxx, px1); maxy = Math.Max(maxy, py1); maxz = Math.Max(maxz, pz1);
            }

            Memory.WriteFloat(at + 0x0, minx); Memory.WriteFloat(at + 0x4, miny); Memory.WriteFloat(at + 0x8, minz); Memory.WriteFloat(at + 0xC, 1f);
            Memory.WriteFloat(at + 0x10, maxx); Memory.WriteFloat(at + 0x14, maxy); Memory.WriteFloat(at + 0x18, maxz); Memory.WriteFloat(at + 0x1C, 1f);
            Memory.WriteInt(at + 0x20, unchecked((int)VtCCollisionMDT));   // vtable (guest)
            Memory.WriteInt(at + 0x30, 0);                                 // MDT ptr (unused by gather)
            Memory.WriteInt(at + 0x34, (int)Guest(polyList));              // poly list (guest)
            Memory.WriteInt(at + 0x38, tris.Length);
            return 0x40 + tris.Length * 0x70;
        }

        // 12-triangle axis-aligned box.
        private static float[][] Box(float x0, float x1, float y0, float y1, float z0, float z1)
        {
            float[][] v = {
                new[]{x0,y0,z0}, new[]{x1,y0,z0}, new[]{x1,y0,z1}, new[]{x0,y0,z1},
                new[]{x0,y1,z0}, new[]{x1,y1,z0}, new[]{x1,y1,z1}, new[]{x0,y1,z1},
            };
            int[,] f = { {0,1,2},{0,2,3},{4,6,5},{4,7,6},{0,4,5},{0,5,1},{3,2,6},{3,6,7},{0,3,7},{0,7,4},{1,5,6},{1,6,2} };
            float[][] tris = new float[12][];
            for (int i = 0; i < 12; i++)
                tris[i] = new[]{ v[f[i,0]][0],v[f[i,0]][1],v[f[i,0]][2], v[f[i,1]][0],v[f[i,1]][1],v[f[i,1]][2], v[f[i,2]][0],v[f[i,2]][1],v[f[i,2]][2] };
            return tris;
        }
    }
}
