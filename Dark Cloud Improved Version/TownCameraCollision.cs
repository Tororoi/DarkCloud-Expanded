using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Phase 2 of the from-scratch camera (see town-camera-rewrite): our OWN camera collision, done in C#.
    ///
    /// The vanilla camera clips because its per-frame poly gather is culled/capped and keyed to a per-part CAMERA
    /// frame that may be coarse/absent. We instead read the PLAYER collision (the complete set the player never
    /// clips through) once per area, cache the triangles in WORLD space, and — every camera frame — cast a ray
    /// from the player toward the desired camera position and pull the camera in to the nearest wall. That handles
    /// both rotation and translation (any time a wall is between you and the camera, we pull in).
    ///
    /// Enumeration (from the decouple RE): CEditGround @0x21D1968C → parts → player frame (+0xD0 map / +0x16010
    /// static) → collision object C=*(F+0x04) → poly array *(C+0x34), count *(C+0x38), stride 0x70, verts LOCAL
    /// v0 +0x00 / v1 +0x10 / v2 +0x20 → world matrix F+0x150 (rows +0x150/+0x160/+0x170/+0x180).
    /// </summary>
    internal static class TownCameraCollision
    {
        internal static bool Enabled = true;
        internal static float Margin = 1f;     // keep the camera this far in front of a wall
        internal static float MinDistance = 10f; // never pull closer than this to the player
        // Map (georama building) parts are OFF for now: their _c is a coarse hull AND a static cache can't follow a
        // part the player moves in edit mode, so it clips/over-pulls. STATIC (terrain/canal/perimeter) collision is
        // the reliable base for the new behaviors. Re-enable once collision moves to a native ELF function (which
        // gets correct per-part _c placement + smoothness for free).
        internal static bool IncludeMapParts = false;
        // When true, Build logs each map (building) part's transform + where its tris land — for diagnosing whether
        // building collision is mis-placed (matrix read before the part's transform settled).
        internal static bool Verbose = true;

        // Gather the CAMERA collision (_c: terrain + perimeter + both-walls) instead of the PLAYER collision
        // (_a: also has the canal invisible walls / railings / triggers the camera should pass through). Frame
        // pointers: map _c @+0xDC vs _a @+0xD0; static _c @+0x1601C vs _a @+0x16010.
        internal static bool UseCameraFrame = true;
        private static int MapFrameOff => UseCameraFrame ? 0xDC : 0xD0;
        private static int StaticFrameOff => UseCameraFrame ? 0x1601C : 0x16010;

        private const long EditGroundPtr = 0x21D1968C;

        // Cached world-space triangles as a flat array: [ax,ay,az, bx,by,bz, cx,cy,cz, ...]
        private static float[] _tri = Array.Empty<float>();
        private static int _triCount;
        private static uint _cachedEg = 0xFFFFFFFF;

        private static int _framesSinceCache;

        /// <summary>Re-enumerate the collision if the area (CEditGround pointer) changed. Returns false if none.</summary>
        internal static bool EnsureCache()
        {
            uint eg = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(eg)) { _triCount = 0; _cachedEg = 0xFFFFFFFF; return false; }
            if (eg != _cachedEg)
            {
                Build(Memory.ToMmu(eg));
                _cachedEg = eg;
                _framesSinceCache = 0;
                return _triCount > 0;
            }
            // Re-cache a couple times after entry: map parts' world matrix (+0x150) isn't valid until they've been
            // DRAWN, so buildings cached on the first frame land mis-placed (bbox y went to -140). Rebuild at ~1s
            // and ~3s to pick up the settled transforms, then leave it (no perpetual re-cache stutter).
            _framesSinceCache++;
            if (_framesSinceCache == 60 || _framesSinceCache == 180)
                Build(Memory.ToMmu(eg));
            return _triCount > 0;
        }

        private static void Build(long eg)
        {
            var verts = new List<float>(8192);
            int mapN = 0, statN = 0;
            // MAP parts (buildings/georama): base +0x30, 0x80 slots, active +0xE8>=0. Frame = _c (+0xDC) or _a (+0xD0).
            for (int i = 0; IncludeMapParts && i < 0x80; i++)
            {
                long part = eg + 0x30 + (long)i * 0x2A0;
                if (Memory.ReadInt(part + 0xE8) < 0) continue;
                uint fr = Memory.ReadUInt(part + MapFrameOff) & Memory.PhysAddrMask;
                if (fr == 0) continue;
                mapN++;
                int before = verts.Count;
                try { AddFrame(fr, verts); } catch { }
                // The COLLISION frames (+0xD0 _a / +0xDC _c) come back IDENTITY (never drawn → GetLWMatrix never
                // runs on them), so their tris are in LOCAL space stacked at the origin. The RENDER frame (ptr at
                // part+0xB0) IS drawn every frame, so its +0x150 holds the engine's own local→world matrix for this
                // part (built by DrawParts→SetPosition(+0x10)+SetRotation(+0x60/64/68)→GetLWMatrix, X→Y→Z order).
                // Borrow it to place the _c geometry — the engine's exact transform, no yaw-sign guessing. Fall
                // back to Position+yaw if the render frame isn't placed yet (matrix still identity at cache time).
                if (verts.Count > before)
                {
                    try
                    {
                        bool placed = false;
                        uint frR = Memory.ReadUInt(part + 0xB0) & Memory.PhysAddrMask;
                        if (Memory.IsValidGuest(frR))
                        {
                            float[] M = Memory.ReadFloatBatch(Memory.ToMmu(frR) + 0x150, 16);
                            // Non-trivial translation ⇒ the render frame has been placed (identity = all-zero t).
                            if (Math.Abs(M[12]) + Math.Abs(M[13]) + Math.Abs(M[14]) > 0.5f)
                            {
                                ApplyWorldMatrix(verts, before, M);
                                placed = true;
                            }
                        }
                        if (!placed)
                        {
                            float[] pos = Memory.ReadFloatBatch(part + 0x10, 3);
                            ApplyPlacement(verts, before, pos[0], pos[1], pos[2], Memory.ReadFloat(part + 0x64));
                        }
                    }
                    catch { }
                }
                if (Verbose)
                {
                    int added = (verts.Count - before) / 9;
                    if (added > 0)
                    {
                        // Slot placement (Position +0x10, yaw +0x64) vs where this part's tris actually LAND now.
                        float[] pp = Memory.ReadFloatBatch(part + 0x10, 3);
                        float ry = Memory.ReadFloat(part + 0x64);
                        float pmnx = 1e9f, pmny = 1e9f, pmnz = 1e9f, pmxx = -1e9f, pmxy = -1e9f, pmxz = -1e9f;
                        for (int k = before; k < verts.Count; k += 3)
                        {
                            float x = verts[k], y = verts[k + 1], z = verts[k + 2];
                            if (x < pmnx) pmnx = x; if (x > pmxx) pmxx = x;
                            if (y < pmny) pmny = y; if (y > pmxy) pmxy = y;
                            if (z < pmnz) pmnz = z; if (z > pmxz) pmxz = z;
                        }
                        Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[TownCameraCollision]   map part {i}: " +
                            $"tris={added} pos=({pp[0]:0},{pp[1]:0},{pp[2]:0}) yaw={ry:0.00} bbox x[{pmnx:0},{pmxx:0}] y[{pmny:0},{pmxy:0}] z[{pmnz:0},{pmxz:0}]");
                    }
                }
            }
            int mapTris = verts.Count / 9;   // tris contributed by MAP (georama: buildings/trees) parts
            // STATIC parts (scene.scn: ground, walls): base 0, 0x40 slots, active +0x16028>=0. Frame = _c (+0x1601C) or _a (+0x16010).
            for (int i = 0; i < 0x40; i++)
            {
                long part = eg + (long)i * 0x2A0;
                if (Memory.ReadInt(part + 0x16028) < 0) continue;
                uint fr = Memory.ReadUInt(part + StaticFrameOff) & Memory.PhysAddrMask;
                if (fr == 0) continue;
                statN++;
                try { AddFrame(fr, verts); } catch { }
            }
            _tri = verts.ToArray();
            _triCount = _tri.Length / 9;

            // bbox of the cached geometry — if it matches the town's world extents, the transform is right; if it's
            // garbage (huge/zero/offset), the frame matrix is wrong or invalid at cache time.
            float mnx = 1e9f, mny = 1e9f, mnz = 1e9f, mxx = -1e9f, mxy = -1e9f, mxz = -1e9f;
            for (int i = 0; i < _tri.Length; i += 3)
            {
                float x = _tri[i], y = _tri[i + 1], z = _tri[i + 2];
                if (x < mnx) mnx = x; if (x > mxx) mxx = x;
                if (y < mny) mny = y; if (y > mxy) mxy = y;
                if (z < mnz) mnz = z; if (z > mxz) mxz = z;
            }
            Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[TownCameraCollision] cached {_triCount} tris " +
                $"(map {mapN} parts={mapTris}t, static {statN} parts={_triCount - mapTris}t) bbox " +
                $"x[{mnx:0},{mxx:0}] y[{mny:0},{mxy:0}] z[{mnz:0},{mxz:0}]");
        }

        // Transform verts[start..] in place by a world matrix (rows r0/r1/r2 basis @ m[0..],[4..],[8..],
        // translation r3 @ m[12..14]): world = lx*r0 + ly*r1 + lz*r2 + r3. Matches AddCollision's row layout.
        private static void ApplyWorldMatrix(List<float> v, int start, float[] m)
        {
            float r00 = m[0], r01 = m[1], r02 = m[2];
            float r10 = m[4], r11 = m[5], r12 = m[6];
            float r20 = m[8], r21 = m[9], r22 = m[10];
            float r30 = m[12], r31 = m[13], r32 = m[14];
            for (int k = start; k < v.Count; k += 3)
            {
                float lx = v[k], ly = v[k + 1], lz = v[k + 2];
                v[k]     = lx * r00 + ly * r10 + lz * r20 + r30;
                v[k + 1] = lx * r01 + ly * r11 + lz * r21 + r31;
                v[k + 2] = lx * r02 + ly * r12 + lz * r22 + r32;
            }
        }

        // If true, flip the Y-rotation sign — set this if rotated buildings' collision comes out mirrored.
        internal static bool FlipYaw = false;

        // Place verts[start..] (identity-framed _c geometry, in part-LOCAL space around the origin) into world
        // space by the part's yaw + world position: world = RotY(ry)*local + pos.
        private static void ApplyPlacement(List<float> v, int start, float px, float py, float pz, float ry)
        {
            float c = (float)Math.Cos(ry), s = (float)Math.Sin(ry);
            if (FlipYaw) s = -s;
            for (int k = start; k < v.Count; k += 3)
            {
                float lx = v[k], ly = v[k + 1], lz = v[k + 2];
                v[k]     = lx * c + lz * s + px;   // RotY about the part origin
                v[k + 1] = ly + py;
                v[k + 2] = -lx * s + lz * c + pz;
            }
        }

        // A part's player collision is a TREE of CFrames (each may carry a collision object). Enumerate the whole
        // tree: this frame's own collision, then recurse its child subtree (which chains through siblings). We do
        // NOT recurse the TOP frame's sibling (that would escape the part into the next one).
        private static void AddFrame(uint framePtr, List<float> verts)
        {
            if (!Memory.IsValidGuest(framePtr)) return;
            long F = Memory.ToMmu(framePtr);
            AddCollision(F, verts);
            Walk(Memory.ReadUInt(F + 0x138) & Memory.PhysAddrMask, verts, 0);   // child subtree
        }

        // Walk a frame, its child subtree, and its sibling chain (all within one part's collision tree).
        private static void Walk(uint framePtr, List<float> verts, int depth)
        {
            if (framePtr == 0 || depth > 512 || !Memory.IsValidGuest(framePtr)) return;
            long F = Memory.ToMmu(framePtr);
            AddCollision(F, verts);
            Walk(Memory.ReadUInt(F + 0x138) & Memory.PhysAddrMask, verts, depth + 1);   // children
            Walk(Memory.ReadUInt(F + 0x13C) & Memory.PhysAddrMask, verts, depth + 1);   // siblings
        }

        // Read one frame's collision object (if any), transform its verts local→world by the frame matrix, append.
        private static void AddCollision(long F, List<float> verts)
        {
            uint cPtr = Memory.ReadUInt(F + 0x04) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(cPtr)) return;
            long C = Memory.ToMmu(cPtr);
            uint pPtr = Memory.ReadUInt(C + 0x34) & Memory.PhysAddrMask;
            int n = Memory.ReadInt(C + 0x38);
            if (!Memory.IsValidGuest(pPtr) || n <= 0 || n > 20000) return;
            long P = Memory.ToMmu(pPtr);

            // World matrix rows (basis vectors r0/r1/r2, translation r3): world = lx*r0 + ly*r1 + lz*r2 + r3.
            float[] m = Memory.ReadFloatBatch(F + 0x150, 16);
            float r00 = m[0], r01 = m[1], r02 = m[2];
            float r10 = m[4], r11 = m[5], r12 = m[6];
            float r20 = m[8], r21 = m[9], r22 = m[10];
            float r30 = m[12], r31 = m[13], r32 = m[14];

            // Batch-read the whole poly block (stride 0x70) and pull v0/v1/v2 (each xyzw at +0x00/+0x10/+0x20).
            float[] blk = Memory.ReadFloatBatch(P, n * (0x70 / 4));
            for (int k = 0; k < n; k++)
            {
                int b = k * (0x70 / 4);
                for (int v = 0; v < 3; v++)
                {
                    int o = b + v * 4;   // v0 @+0x00 (words 0..), v1 @+0x10 (word 4), v2 @+0x20 (word 8)
                    float lx = blk[o], ly = blk[o + 1], lz = blk[o + 2];
                    verts.Add(lx * r00 + ly * r10 + lz * r20 + r30);
                    verts.Add(lx * r01 + ly * r11 + lz * r21 + r31);
                    verts.Add(lx * r02 + ly * r12 + lz * r22 + r32);
                }
            }
        }

        /// <summary>Ray from (ox,oy,oz) along unit dir (dx,dy,dz); nearest triangle hit distance in [0,maxT], or
        /// maxT if none. Möller–Trumbore, double-sided.</summary>
        internal static float NearestHit(float ox, float oy, float oz, float dx, float dy, float dz, float maxT)
        {
            float best = maxT;
            float[] t = _tri;
            int c = _triCount;
            for (int i = 0; i < c; i++)
            {
                int j = i * 9;
                float ax = t[j], ay = t[j + 1], az = t[j + 2];
                float e1x = t[j + 3] - ax, e1y = t[j + 4] - ay, e1z = t[j + 5] - az;
                float e2x = t[j + 6] - ax, e2y = t[j + 7] - ay, e2z = t[j + 8] - az;
                // p = dir × e2
                float px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
                float det = e1x * px + e1y * py + e1z * pz;
                if (det > -1e-6f && det < 1e-6f) continue;
                float inv = 1f / det;
                float sx = ox - ax, sy = oy - ay, sz = oz - az;
                float u = (sx * px + sy * py + sz * pz) * inv;
                if (u < 0f || u > 1f) continue;
                float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
                float vv = (dx * qx + dy * qy + dz * qz) * inv;
                if (vv < 0f || u + vv > 1f) continue;
                float dist = (e2x * qx + e2y * qy + e2z * qz) * inv;
                if (dist > 0.01f && dist < best) best = dist;
            }
            return best;
        }

        internal static int CachedTriangleCount => _triCount;
    }
}
