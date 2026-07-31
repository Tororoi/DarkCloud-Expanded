using System;
using System.Collections.Generic;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Phase 1 of the camera ELF port (docs/town-camera-elf-port-plan.md): CONFIRM the game's RUNTIME camera CCPoly
    /// buffer is world-correct and actually contains BUILDINGS — the whole premise for reusing it natively (it would
    /// retire the C# _c placement problem). Read-only.
    ///
    /// The camera collision set is a transient per-frame buffer. In EdMoveChara @0x16AEB4: *(WorkBuffer+8)=0 (reset),
    /// v0 = Alloc(WorkBuffer, 2000) → base, then PickUpCameraPoly__CEditGround(base, box, i) fills it and RETURNS the
    /// count (not stored globally). The camera gather is the LAST WorkBuffer use each frame, so *(struct) holds the
    /// camera polys most of the frame.
    ///
    /// WorkBuffer struct ptr lives at gp(_gp=0x2A97F0) - 0x7468 = 0x2A2388 (PINE 0x202A2388). CDataAlloc2 layout
    /// {+0 dataPtr, +8 used, +C cap}; after the used-reset Alloc returns *dataPtr, so base = *(struct+0). CCPoly
    /// stride 0x50: v0 @+0x00, v1 @+0x10, v2 @+0x20, normal @+0x30 (all xyzw). Count isn't stored → we scan valid
    /// polys (unit-length normal, finite verts) until the first invalid one.
    /// </summary>
    internal static class CameraCPolyProbe
    {
        internal static bool Enabled = true;

        private const long WorkBufferPtr = 0x202A2388;   // gp-0x7468: pointer to the WorkBuffer CDataAlloc2 struct
        private const long EditGroundPtr = 0x21D1968C;   // to trigger one dump per area
        private const long MainCameraPtr = 0x21D19678;   // ptr to MainCamera; ref @+0x2C0, height +0x2D4, angle +0x2D8
        private const int  MaxPolys = 512;               // buffer caps ~400 (Alloc 2000 blocks * 0x10 / 0x50 ≈ 409)

        // Known Queens (MapNo 2) building XZ from the map-part placement log — for a direct "are buildings covered"
        // metric. A building is "covered" if a near-vertical (wall) poly sits within Radius of its XZ.
        private static readonly (float x, float z)[] QueensBuildings =
        {
            (250, -550), (1250, 1050), (1250, 750), (650, -550), (450, -800), (-150, -300),
            (50, -800), (1250, 500), (-200, -800), (350, 750), (800, 1050), (350, 1050),
        };
        private const float BuildingRadius = 140f;

        private static readonly string DumpDir = Environment.GetEnvironmentVariable("DC_DUMP_DIR");

        private static uint _lastEg = 0xFFFFFFFF;
        private static int _settle = -1;
        private static int _retries;

        internal static void Tick()
        {
            if (!Enabled) return;
            uint eg = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(eg)) { _lastEg = 0xFFFFFFFF; return; }
            Monitor();   // per-frame jump watcher
            if (eg != _lastEg) { _lastEg = eg; _settle = 90; _retries = 8; return; }   // ~1.5s after area change
            if (_settle > 0 && --_settle == 0) Dump();   // one dump per area (periodic spam disabled post-diagnosis)
        }

        // Per-frame watcher for the reproducible camera JUMP. Computes the ACTUAL camera eye position the way Step
        // renders it (ref + dist*{sin,cos}(SMOOTHED angle +0x2DC) + height), and when the eye leaps in one frame,
        // breaks down WHICH component moved: dist(+0x2D0), target angle(+0x2D8), smoothed angle(+0x2DC), height(+0x2D4),
        // or ref(+0x2C0). The pull-in only touches dist — so a jump in angle/ref points outside our code (vanilla).
        // Full-precision per-frame RECORDER (the "simulator input"). Keeps the last ~15s in a ring and rewrites a CSV
        // every 60 frames, so after reproducing a jump the file holds it. Columns are the raw camera state PLUS what
        // our pull-in WOULD compute (simTarget = nearest-hit − MARGIN, floored MIN) so we can replay the 0.15 ease
        // offline and see whether the game's dist matches our model (→ our code) or diverges (→ vanilla writing dist).
        private static readonly System.Collections.Generic.List<string> _csv = new();
        private static int _csvTick = 0, _csvFlush = 0;
        private static string CsvPath => System.IO.Path.Combine(DumpDir ?? AppContext.BaseDirectory, "camera_trace.csv");

        private static void Monitor()
        {
            uint camRaw = Memory.ReadUInt(MainCameraPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(camRaw)) return;
            long cm = Memory.ToMmu(camRaw);
            float a8 = Memory.ReadFloat(cm + 0x2A8);
            float[] cf = Memory.ReadFloatBatch(cm + 0x2C0, 8);  // 2C0 ref xyz,2D0 dist,2D4 h,2D8 angT,2DC angS
            float refx = cf[0], refy = cf[1], refz = cf[2], dist = cf[4], h = cf[5], angT = cf[6], angS = cf[7];
            float hit = NearestHitHoriz(refx, refy, refz, angT, h, out _, out _);   // our ray at the target angle
            float simTarget = hit < 0 ? 80f : Math.Max(12f, hit - 8f);
            float eyeX = refx + dist * (float)Math.Sin(angS);   // the REAL eye (Step renders with +0x2DC = angS)
            float eyeZ = refz + dist * (float)Math.Cos(angS);
            _csvTick++;
            _csv.Add($"{_csvTick},{dist:0.####},{angT:0.######},{angS:0.######},{a8:0.###},{simTarget:0.###},{refx:0.###},{refz:0.###},{eyeX:0.###},{eyeZ:0.###}");
            if (_csv.Count > 900) _csv.RemoveRange(0, _csv.Count - 900);
            if (++_csvFlush >= 60)
            {
                _csvFlush = 0;
                try { System.IO.File.WriteAllText(CsvPath, "tick,dist,angT,angS,a8,simTarget,refx,refz,eyeX,eyeZ\n" + string.Join("\n", _csv)); }
                catch (Exception e) { Log("csv write failed: " + e.Message); }
            }
        }

        // nearest-hit horiz distance (-1 miss) along the pull-in ray, vs the live buffer — for the trace's target column.
        private static float NearestHitHoriz(float refx, float refy, float refz, float ang, float h, out float hcx, out float hcz)
        {
            hcx = hcz = 0;
            uint structRaw = Memory.ReadUInt(WorkBufferPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(structRaw)) return -1;
            long structMmu = Memory.ToMmu(structRaw);
            uint baseRaw = Memory.ReadUInt(structMmu) & Memory.PhysAddrMask;
            int used = Memory.ReadInt(structMmu + 8);
            if (!Memory.IsValidGuest(baseRaw)) return -1;
            long b = Memory.ToMmu(baseRaw);
            const float BASE = 80f;
            float[] rf = { refx, refy, refz };
            float[] rt = { refx + BASE * (float)Math.Sin(ang), refy + h, refz + BASE * (float)Math.Cos(ang) };
            float best = -1;
            int lim = Math.Min(MaxPolys, Math.Max(1, used / 5 + 4));
            for (int i = 0; i < lim; i++)
            {
                float[] p = Memory.ReadFloatBatch(b + (long)i * 0x50, 15);
                float nl = (float)Math.Sqrt(p[12] * p[12] + p[13] * p[13] + p[14] * p[14]);
                if (!(IsFinite(p[0]) && Math.Abs(p[0]) < 1e5f && nl > 1e-4f && (p[0] != p[4] || p[2] != p[6]))) continue;
                if (RayHitsTri(rf, rt, p))
                {
                    float cx = (p[0] + p[4] + p[8]) / 3f, cz = (p[2] + p[6] + p[10]) / 3f;
                    float dx = cx - refx, dz = cz - refz, hd = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (best < 0 || hd < best) { best = hd; hcx = cx; hcz = cz; }
                }
            }
            return best;
        }

        /// <summary>Force a dump now (e.g. from a key binding) regardless of the settle timer.</summary>
        internal static void DumpNow() => Dump();

        private static void Dump()
        {
            uint structRaw = Memory.ReadUInt(WorkBufferPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(structRaw)) { Log($"WorkBuffer struct ptr invalid (0x{structRaw:X8})"); return; }
            long structMmu = Memory.ToMmu(structRaw);
            uint baseRaw = Memory.ReadUInt(structMmu) & Memory.PhysAddrMask;
            int used = Memory.ReadInt(structMmu + 8), cap = Memory.ReadInt(structMmu + 0xC);
            Log($"WorkBuffer struct @0x{structRaw:X8}  data @0x{baseRaw:X8}  used={used} cap={cap} blocks");
            if (!Memory.IsValidGuest(baseRaw)) { Log($"WorkBuffer data ptr invalid (0x{baseRaw:X8})"); return; }
            long b = Memory.ToMmu(baseRaw);

            // Raw dump of the first entries so we can see the actual layout (the stored normal at +0x30 is NOT
            // pre-normalized — the engine sceVu0Normalize's it on read — so don't gate on |normal|).
            for (int i = 0; i < 6; i++)
            {
                float[] p = Memory.ReadFloatBatch(b + (long)i * 0x50, 15);
                Log($"   [{i}] v0=({p[0]:0.#},{p[1]:0.#},{p[2]:0.#}) v1=({p[4]:0.#},{p[5]:0.#},{p[6]:0.#}) " +
                    $"v2=({p[8]:0.#},{p[9]:0.#},{p[10]:0.#}) n=({p[12]:0.##},{p[13]:0.##},{p[14]:0.##})");
            }

            // Camera look-at point (ray origin the pull-in uses) + where it's aimed, so we can tell whether the
            // wall you're FACING is actually represented in the buffer.
            float refx = 0, refy = 0, refz = 0, camDeg = 0, camH = 0, camDist = 0; bool haveRef = false;
            float[] rayFrom = new float[3], rayTo = new float[3];
            uint camRaw = Memory.ReadUInt(MainCameraPtr) & Memory.PhysAddrMask;
            if (Memory.IsValidGuest(camRaw))
            {
                float[] cf = Memory.ReadFloatBatch(Memory.ToMmu(camRaw) + 0x2C0, 8);  // 2C0 refx,2C4 refy,2C8 refz,2D0 dist,2D4 h,2D8 angle
                refx = cf[0]; refy = cf[1]; refz = cf[2]; camDist = cf[4]; camH = cf[5];
                float ang = cf[6]; camDeg = ang * 180f / (float)Math.PI; haveRef = true;
                const float BASE = 80f;   // same ray the native pull-in casts: ref -> ref + (BASE*sin, height, BASE*cos)
                rayFrom[0] = refx; rayFrom[1] = refy; rayFrom[2] = refz;
                rayTo[0] = refx + BASE * (float)Math.Sin(ang);
                rayTo[1] = refy + camH;
                rayTo[2] = refz + BASE * (float)Math.Cos(ang);
            }
            var nearWalls = new List<(float dist, float dir, float cx, float cy, float cz)>();
            float rayHitDist = -1; float rhcx = 0, rhcy = 0, rhcz = 0;   // nearest poly our pull-in ray actually hits
            var polys = new List<float[]>();   // keep valid polys for a second ray test toward the nearest wall

            int n = 0, walls = 0, floors = 0, invalid = 0;
            float mnx = 1e9f, mny = 1e9f, mnz = 1e9f, mxx = -1e9f, mxy = -1e9f, mxz = -1e9f;
            float wallMinY = 1e9f, wallMaxY = -1e9f;
            int[] wallYband = new int[5];   // <50, 50-100, 100-150, 150-250, 250+
            var covered = new bool[QueensBuildings.Length];
            var sb = (DumpDir != null) ? new StringBuilder("v0x,v0y,v0z,v1x,v1y,v1z,v2x,v2y,v2z,nx,ny,nz\n") : null;

            // Scan the actual filled range (used blocks / 5 per CCPoly), capped. SKIP invalid entries instead of
            // stopping at the first — an overrun/mid-gather buffer has garbage interleaved with the real polys.
            int scanLimit = Math.Min(MaxPolys, Math.Max(1, used / 5 + 4));
            for (int i = 0; i < scanLimit; i++)
            {
                float[] p = Memory.ReadFloatBatch(b + (long)i * 0x50, 15);   // v0(0-2) v1(4-6) v2(8-10) normal(12-14)
                float nx = p[12], ny = p[13], nz = p[14];
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                // Valid = finite, in-bounds verts, a non-zero normal, and a non-degenerate triangle. The stored
                // normal is NOT unit (engine normalizes on read), so only require it to be non-zero.
                bool okVerts = IsFinite(p[0]) && IsFinite(p[2]) && IsFinite(p[8]) &&
                               Math.Abs(p[0]) < 1e5f && Math.Abs(p[1]) < 1e5f && Math.Abs(p[2]) < 1e5f;
                bool okTri = nl > 1e-4f && (p[0] != p[4] || p[2] != p[6] || p[0] != p[8]);
                if (!okVerts || !okTri) { invalid++; continue; }   // skip garbage, keep scanning
                polys.Add((float[])p.Clone());

                n++;
                nx /= nl; ny /= nl; nz /= nl;   // normalize for the wall/floor test
                float cy = (p[1] + p[5] + p[9]) / 3f;
                float cx = (p[0] + p[4] + p[8]) / 3f, cz = (p[2] + p[6] + p[10]) / 3f;
                for (int v = 0; v < 3; v++)
                {
                    float x = p[v * 4], y = p[v * 4 + 1], z = p[v * 4 + 2];
                    if (x < mnx) mnx = x; if (x > mxx) mxx = x;
                    if (y < mny) mny = y; if (y > mxy) mxy = y;
                    if (z < mnz) mnz = z; if (z > mxz) mxz = z;
                }

                bool wall = Math.Abs(ny) < 0.5f;   // same wall filter the native camera uses (|normal.y| < 0.5)
                if (wall)
                {
                    walls++;
                    if (cy < wallMinY) wallMinY = cy; if (cy > wallMaxY) wallMaxY = cy;
                    wallYband[cy < 50 ? 0 : cy < 100 ? 1 : cy < 150 ? 2 : cy < 250 ? 3 : 4]++;
                    for (int q = 0; q < QueensBuildings.Length; q++)
                    {
                        float dx = cx - QueensBuildings[q].x, dz = cz - QueensBuildings[q].z;
                        if (dx * dx + dz * dz < BuildingRadius * BuildingRadius) covered[q] = true;
                    }
                    if (haveRef)
                    {
                        float ddx = cx - refx, ddz = cz - refz;
                        float d = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                        float dir = (float)(Math.Atan2(ddx, ddz) * 180.0 / Math.PI);   // 0=+Z, 90=+X (compass around ref)
                        nearWalls.Add((d, dir, cx, cy, cz));
                    }
                }
                else floors++;

                // Does OUR pull-in ray hit this poly? (same seg-tri test CheckHit does) → nearest hit = what the
                // native camera should be pulling in to. If this stays "no hit" while you FACE the wall, the ray
                // geometry is wrong; if it hits here but the camera passes through, the native side is the bug.
                if (haveRef && RayHitsTri(rayFrom, rayTo, p))
                {
                    float a = cx - refx, c2 = cz - refz;
                    float hd = (float)Math.Sqrt(a * a + c2 * c2);
                    if (rayHitDist < 0 || hd < rayHitDist) { rayHitDist = hd; rhcx = cx; rhcy = cy; rhcz = cz; }
                }

                sb?.Append($"{p[0]:0.##},{p[1]:0.##},{p[2]:0.##},{p[4]:0.##},{p[5]:0.##},{p[6]:0.##}," +
                           $"{p[8]:0.##},{p[9]:0.##},{p[10]:0.##},{nx:0.###},{ny:0.###},{nz:0.###}\n");
            }

            if (n == 0 && _retries-- > 0) { _settle = 20; return; }   // maybe caught mid-reset; try again shortly
            int cov = 0; foreach (var c in covered) if (c) cov++;
            int map = Memory.ReadInt(EditLoop.MapNo);
            Log($"camera CCPoly buffer @0x{baseRaw:X8}: {n} valid polys ({walls} wall / {floors} floor)  " +
                $"[{invalid} invalid skipped, scanned {scanLimit}]  bbox x[{mnx:0},{mxx:0}] y[{mny:0},{mxy:0}] z[{mnz:0},{mxz:0}]");
            Log($"   wall Y {wallMinY:0}..{wallMaxY:0}  bands <50={wallYband[0]} 50-100={wallYband[1]} " +
                $"100-150={wallYband[2]} 150-250={wallYband[3]} 250+={wallYband[4]}   (buildings live at y≥150 → " +
                $"non-zero high bands = buildings present)");
            if (map == 2)
                Log($"   Queens buildings with camera wall-collision within {BuildingRadius:0}u: {cov}/{QueensBuildings.Length}");

            // Wall-proximity report: are there wall polys near where you're standing, and in which direction?
            // Stand FACING a dividing wall (camAngle points at it); if no wall poly shows up near & in that
            // direction, the wall isn't in the buffer (tile-based / not gathered) → mesh collision can't catch it.
            if (haveRef)
            {
                nearWalls.Sort((a, c) => a.dist.CompareTo(c.dist));
                int near90 = 0; foreach (var w in nearWalls) if (w.dist < 90f) near90++;
                Log($"   ref=({refx:0},{refy:0},{refz:0}) camAngle={camDeg:0}deg dist={camDist:0} h={camH:0}  " +
                    $"rayTo=({rayTo[0]:0},{rayTo[1]:0},{rayTo[2]:0})");
                if (rayHitDist >= 0)
                    Log($"   >>> camera-dir ray HITS at horiz {rayHitDist:0}u  poly c=({rhcx:0},{rhcy:0},{rhcz:0})  " +
                        $"(target dist would be {Math.Max(12f, rayHitDist - 8f):0})");
                else
                    Log($"   >>> camera-dir ray hits NOTHING (camera stays at base 80 → passes through)");

                // Angle-INDEPENDENT test: aim a ray straight at the nearest wall (camera pos IF it were pointed there)
                // and see if our seg-tri test catches it. If this misses, the wall is un-hittable by a single ray
                // (needs width rays / a fatter probe). If it hits, the problem is only that a lone center ray needs
                // near-perfect aim to catch off-center walls.
                if (near90 > 0)
                {
                    var w = nearWalls[0];
                    float dx = w.cx - refx, dz = w.cz - refz, len = (float)Math.Sqrt(dx * dx + dz * dz);
                    float[] rf = { refx, refy, refz };
                    float[] rt = { refx + 80f * dx / len, refy + camH, refz + 80f * dz / len };
                    float best = -1; float bcx = 0, bcy = 0, bcz = 0;
                    foreach (var q in polys)
                        if (RayHitsTri(rf, rt, q))
                        {
                            float qcx = (q[0] + q[4] + q[8]) / 3f, qcy = (q[1] + q[5] + q[9]) / 3f, qcz = (q[2] + q[6] + q[10]) / 3f;
                            float a = qcx - refx, c2 = qcz - refz; float hd = (float)Math.Sqrt(a * a + c2 * c2);
                            if (best < 0 || hd < best) { best = hd; bcx = qcx; bcy = qcy; bcz = qcz; }
                        }
                    if (best >= 0)
                        Log($"   >>> ray-AT-nearest-wall HITS at {best:0}u c=({bcx:0},{bcy:0},{bcz:0})  → wall IS hittable, lone center ray just needs aim");
                    else
                        Log($"   >>> ray-AT-nearest-wall MISSES (nearest wall d={w.dist:0} at y-centroid {w.cy:0}, ray y {refy:0}..{refy + camH:0}) → wall un-hittable by single ray");
                }
                Log($"   wall polys within 90u: {near90} | nearest:");
                for (int k = 0; k < nearWalls.Count && k < 8; k++)
                {
                    var w = nearWalls[k];
                    Log($"      d={w.dist:0} dir={w.dir:0}deg  c=({w.cx:0},{w.cy:0},{w.cz:0})");
                }
            }

            if (sb != null && n > 0)
            {
                try
                {
                    string path = System.IO.Path.Combine(DumpDir, "camera_cpoly.csv");
                    System.IO.File.WriteAllText(path, sb.ToString());
                    Log($"   wrote {n} polys -> {path}");
                }
                catch (Exception e) { Log($"   CSV write failed: {e.Message}"); }
            }
        }

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        // Segment (rayFrom→rayTo) vs triangle p[v0(0-2),v1(4-6),v2(8-10), normal(12-14)] — mirrors CheckHit:
        // opposite-sides plane cross, then point-in-triangle via same-side edge cross. Normal need not be unit.
        private static bool RayHitsTri(float[] rf, float[] rt, float[] p)
        {
            float nx = p[12], ny = p[13], nz = p[14];
            // signed distances of from/to to the poly plane (through v0)
            float d0 = nx * (rf[0] - p[0]) + ny * (rf[1] - p[1]) + nz * (rf[2] - p[2]);
            float d1 = nx * (rt[0] - p[0]) + ny * (rt[1] - p[1]) + nz * (rt[2] - p[2]);
            if (!(((d0 <= 0) || (d1 <= 0)) && ((d0 >= 0) || (d1 >= 0)))) return false;  // same side → no cross
            float denom = d0 - d1;
            if (Math.Abs(denom) < 1e-9f) return false;
            float t = d0 / denom;
            if (t < 0f || t > 1f) return false;
            float ix = rf[0] + t * (rt[0] - rf[0]), iy = rf[1] + t * (rt[1] - rf[1]), iz = rf[2] + t * (rt[2] - rf[2]);
            // point-in-triangle: dot(normal, edge × (P - edgeStart)) >= 0 for all three edges
            return Edge(p, 0, 4, ix, iy, iz, nx, ny, nz) && Edge(p, 4, 8, ix, iy, iz, nx, ny, nz) &&
                   Edge(p, 8, 0, ix, iy, iz, nx, ny, nz);
        }

        private static bool Edge(float[] p, int a, int b, float ix, float iy, float iz, float nx, float ny, float nz)
        {
            float ex = p[b] - p[a], ey = p[b + 1] - p[a + 1], ez = p[b + 2] - p[a + 2];
            float vx = ix - p[a], vy = iy - p[a + 1], vz = iz - p[a + 2];
            float cx = ey * vz - ez * vy, cy = ez * vx - ex * vz, cz = ex * vy - ey * vx;   // edge × (P-a)
            return nx * cx + ny * cy + nz * cz >= 0f;
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [CameraCPolyProbe] " + s);
    }
}
