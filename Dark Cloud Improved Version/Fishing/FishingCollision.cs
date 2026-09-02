using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Fishing collision fabrication (split out of CustomFishingSpot): at session start, drop Brownboo's
    /// ladder-top platforms from the cpoly, append the town's custom DCFC collision (Queens / Yellow Drops
    /// fish walls), and keep the fish's cpoly count in sync. CustomFishingSpot.WatchFishingStart drives it.
    /// </summary>
    internal static class FishingCollision
    {
        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishingCollision] " + s);

        /// <summary>
        /// Brownboo-only: drop the walkable LADDER-TOP platforms from the fishing cpoly so the bobber's ground-lift
        /// can't deposit it on a platform above the water. The player needs those floors (its own collision
        /// system, not cpoly), so this has to be a fishing-time filter. Everything else — walls included — is KEPT:
        /// the bobber/hook probe never treats a wall as ground (|normal.y| &lt;= 0.2), and the fish are STOPPED by
        /// walls, which is exactly the containment the native geometry provides. (This used to strip every wall
        /// to make room for the runtime rock append; the rocks are ISO-baked now and no town needs the room.)
        ///
        /// Pure runtime memory op: forward-compact the buffer in place (write index never outruns read index)
        /// and lower CPolyNum. Runs once, AFTER PickUpPoly has gathered. Reloading the town restores the set.
        /// </summary>
        internal static void DropLadderTopFloors(int mapNo)
        {
            uint p = Memory.ReadUInt(FishingSpot.CPoly) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) { Log("   ladder-top filter: cpoly ptr invalid — skipping"); return; }
            long buf = Memory.ToMmu(p);

            int nativeCount = Memory.ReadInt(FishingSpot.CPolyNum);
            if (nativeCount <= 0 || nativeCount > FishingSpot.CPolyMax)
            { Log($"   ladder-top filter: native count {nativeCount} unusable — skipping"); return; }

            // Capture the FULL native gather at the current cast rect (the viewer's ground truth for the
            // fishing-gather replica); Diagnostics-gated inside.
            DumpFullGather(buf, nativeCount, mapNo);
            if (mapNo != TownMapNo.Brownboo) return;   // only Brownboo has in-water ladders

            int keep = 0, ladtops = 0;
            for (int i = 0; i < nativeCount; i++)
            {
                long poly = buf + (long)i * 0x50;
                if (IsLadderTopFloor(poly, mapNo)) { ladtops++; continue; }
                if (keep != i)
                    Memory.WriteBytesBatch(buf + (long)keep * 0x50, Memory.ReadBytesBatch(poly, 0x50));
                keep++;
            }
            Memory.WriteInt(FishingSpot.CPolyNum, keep);
            Log($"   ladder-top filter: dropped {ladtops} ladder-top floors — cpoly {nativeCount} → {keep}");
        }

        /// <summary>Brownboo's in-water ladder (s04r*) XZ positions, from gedit/s04/mapinfo.cfg. Used to
        /// reclaim the FLOOR platforms on top of each ladder. Radius/height must match the viewer's van_cut
        /// (tools/brownboo/brownboo_viewer.py: LAD_POS / LAD_R / LAD_Y).</summary>
        private static readonly (float x, float z)[] BrownbooLadders =
        {
            (0f, 74f), (-57f, 48f), (32f, -67f), (82f, 109f), (62f, -127f), (-55f, -116f), (-91f, 76f),
        };
        private const float LadderRadius  = 45f;   // top platforms lean out up to ~42u from the base position
        private const float LadderTopMinY = 25f;   // a floor poly at/above this height near a ladder is a top

        /// <summary>True if a cpoly triangle is a floor platform on top of one of Brownboo's ladders: its
        /// lowest vertex is above <see cref="LadderTopMinY"/> AND its centre lies within
        /// <see cref="LadderRadius"/> of a ladder. Brownboo-only (the positions are its own).</summary>
        private static bool IsLadderTopFloor(long poly, int mapNo)
        {
            if (mapNo != TownMapNo.Brownboo) return false;

            float y0 = Memory.ReadFloat(poly + 4);
            float y1 = Memory.ReadFloat(poly + 0x10 + 4);
            float y2 = Memory.ReadFloat(poly + 0x20 + 4);
            if (Math.Min(y0, Math.Min(y1, y2)) < LadderTopMinY) return false;

            float cx = (Memory.ReadFloat(poly) + Memory.ReadFloat(poly + 0x10) + Memory.ReadFloat(poly + 0x20)) / 3f;
            float cz = (Memory.ReadFloat(poly + 8) + Memory.ReadFloat(poly + 0x10 + 8) + Memory.ReadFloat(poly + 0x20 + 8)) / 3f;
            foreach (var (lx, lz) in BrownbooLadders)
            {
                float dx = cx - lx, dz = cz - lz;
                if (dx * dx + dz * dz < LadderRadius * LadderRadius) return true;
            }
            return false;
        }

        /// <summary>Append the town's fabricated fishing collision from the DCFC .bin to cpoly — the decoded
        /// rock triangles plus any hand-picked triangles (both baked into the .bin by
        /// tools/build_fishing_collision.py). Runs after the floors-only compaction, so it lands in the slots
        /// freed by the dropped walls.</summary>
        internal static void AppendCustomCollision(int mapNo)
        {
            uint p = Memory.ReadUInt(FishingSpot.CPoly) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) { Log("   rocks: cpoly ptr invalid — skipping"); return; }
            long buf = Memory.ToMmu(p);

            int count = Memory.ReadInt(FishingSpot.CPolyNum);
            if (count <= 0 || count > FishingSpot.CPolyMax) { Log($"   rocks: cpoly count {count} unusable"); return; }

            byte[] template = Memory.ReadBytesBatch(buf, 0x50);   // a real poly, for its non-vertex fields
            var polys = new System.Collections.Generic.List<byte[]>();
            if (!File.Exists(MeshCollisionFile(mapNo))) return;   // town's custom collision is ISO-baked (Brownboo rocks) or none
            int added = AddMeshTriangles(polys, template, mapNo);
            if (added == 0) { Log($"   collision: mesh file for map {mapNo} has 0 tris"); return; }

            int total = count + polys.Count;
            if (total > FishingSpot.CPolyBufferMax)
            { Log($"   collision: {count} + {polys.Count} = {total} > {FishingSpot.CPolyBufferMax} buffer — skipping"); return; }

            for (int i = 0; i < polys.Count; i++)
                Memory.WriteBytesBatch(buf + (long)(count + i) * 0x50, polys[i]);
            Memory.WriteInt(FishingSpot.CPolyNum, total);
            Log($"   collision: appended {polys.Count} fabricated tris (cpoly {count} → {total})");
        }

        // Fish array + collision-count layout: `Fish` (ptr) @0x202A2B58, `FishNum` @0x202A2B64; each CFish is
        // 0x2410 bytes; SetCPoly__5CFishFP6CCPolyi (0x240470) stores the poly list @+0x2400 and the COUNT
        // @+0x2404, which Step__5CFishFv (0x240480) reads to test movement against.
        private const long FishPtr = FishingSpot.Fish, FishNumAddr = FishingSpot.FishNum;
        private const long FishStride = 0x2410, FishCPolyCount = 0x2404;

        /// <summary>Re-point every live fish's cpoly COUNT at the current cpoly_num.
        ///
        /// _LOAD_FISHING_DATA copies the gathered polys into the global cpoly and _INIT_FISH hands each fish a
        /// SNAPSHOT of the count (SetCPoly -> fish+0x2404) a few frames later — but BEFORE that our
        /// AppendCustomCollision has grown the buffer (45 -> 758). So the fish test only the original polys and
        /// swim straight through the containment walls we appended at the TAIL. The list pointer they hold is
        /// the same growing buffer, so only the count is stale: bump it and they test the whole buffer.
        /// Runs every live frame (the fish appear a few frames after the append); cheap (&lt;=6 int writes,
        /// and only when the value actually differs).</summary>
        internal static void SyncFishCPolyCount()
        {
            uint fishBase = Memory.ReadUInt(FishPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(fishBase)) return;
            int num = Memory.ReadInt(FishNumAddr);
            if (num <= 0 || num > 6) return;
            int count = Memory.ReadInt(FishingSpot.CPolyNum);
            if (count <= 0 || count > FishingSpot.CPolyBufferMax) return;
            long b = Memory.ToMmu(fishBase);
            for (int i = 0; i < num; i++)
            {
                long fish = b + (long)i * FishStride;
                if (Memory.ReadInt(fish + FishCPolyCount) != count)
                    Memory.WriteIntFast(fish + FishCPolyCount, count);
            }
        }

        /// <summary>Where the FULL native gather (floors + walls, pre-removal) is written at the CURRENT cast
        /// rect, for the viewer (tools/brownboo/brownboo_viewer.py) to split into floor/slope/wall. Overwrites the
        /// stale reference each capture, which is correct — the rect it reflects is whatever is live now.</summary>
        // Dev-only diagnostic; runs only when DC_DUMP_DIR is set (see .env.sample), else skipped — no fallback.
        // Per-town path (game_data/<town>/vanilla_cpoly.csv, sibling of DC_DUMP_DIR) so each town's dump feeds
        // its own viewer and towns don't clobber each other. DC_DUMP_DIR itself points at game_data/brownboo,
        // so map 14 resolves to exactly the historical path.
        private static string FullGatherCsvFor(int mapNo)
        {
            string town = mapNo switch { 2 => "queens", 14 => "brownboo", 23 => "yellowdrops", _ => null };
            if (town == null) return null;
            // Prefer DC_DUMP_DIR's parent (game_data) when the env var is set; otherwise derive game_data from
            // the running assembly's location (bin/Debug/net8.0 -> repo root) so the dump works even when the
            // mod is launched from an IDE that never sourced .env.
            string dumpDir = Environment.GetEnvironmentVariable("DC_DUMP_DIR");
            string gameData = !string.IsNullOrEmpty(dumpDir)
                ? Path.GetDirectoryName(dumpDir.TrimEnd('/', '\\'))
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "game_data"));
            return Path.Combine(gameData, town, "vanilla_cpoly.csv");
        }

        /// <summary>Dump every cpoly triangle (3 verts + normal) to a CSV. One line per triangle:
        /// v0x,v0y,v0z,v1x,v1y,v1z,v2x,v2y,v2z,nx,ny,nz.</summary>
        private static void DumpFullGather(long buf, int count, int mapNo)
        {
            if (!CustomFishingSpot.Diagnostics) { Log("   full-gather: Diagnostics off — skipping vanilla-cpoly dump"); return; }
            string csv = FullGatherCsvFor(mapNo);
            if (csv == null) { Log($"   full-gather: map {mapNo} has no dump folder — skipping"); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("v0x,v0y,v0z,v1x,v1y,v1z,v2x,v2y,v2z,nx,ny,nz");
            for (int i = 0; i < count; i++)
            {
                long poly = buf + (long)i * 0x50;
                for (int v = 0; v < 3; v++)
                {
                    sb.Append(Memory.ReadFloat(poly + v * 0x10).ToString("0.###")).Append(',');
                    sb.Append(Memory.ReadFloat(poly + v * 0x10 + 4).ToString("0.###")).Append(',');
                    sb.Append(Memory.ReadFloat(poly + v * 0x10 + 8).ToString("0.###")).Append(',');
                }
                sb.Append(Memory.ReadFloat(poly + 0x30).ToString("0.###")).Append(',');
                sb.Append(Memory.ReadFloat(poly + 0x30 + 4).ToString("0.###")).Append(',');
                sb.Append(Memory.ReadFloat(poly + 0x30 + 8).ToString("0.###"));
                sb.AppendLine();
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(csv));
                System.IO.File.WriteAllText(csv, sb.ToString());
                Log($"   full-gather: wrote {count} polys (floors+walls) -> {csv}");
            }
            catch (Exception e)
            {
                Log($"   full-gather: write FAILED: {e.Message}");
            }
        }

        private static string MeshCollisionFile(int mapNo)
        {
            string town = mapNo switch { 2 => "queens", 14 => "brownboo", 23 => "yellowdrops", _ => "brownboo" };
            return Path.Combine(AppContext.BaseDirectory, "Resources", "FishingCollision", $"{town}_{mapNo}.bin");
        }

        /// <summary>Append the spot's EXACT mesh triangles (decoded offline from the town's visual mesh) to
        /// the poly list, each with a real plane normal so the hook/bobber rest on up-facing faces and the
        /// fish are stopped by side faces. Returns the number of triangles added (0 if no data file).</summary>
        private static int AddMeshTriangles(System.Collections.Generic.List<byte[]> outp, byte[] template, int mapNo)
        {
            string path = MeshCollisionFile(mapNo);
            if (!File.Exists(path)) return 0;

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception e) { Log($"   mesh collision: read failed ({e.Message})"); return 0; }

            // Header: 'DCFC', uint version, uint mapNo, uint triCount; then triCount * 9 floats (3 verts).
            if (data.Length < 16 || data[0] != (byte)'D' || data[1] != (byte)'C' || data[2] != (byte)'F' || data[3] != (byte)'C')
            { Log("   mesh collision: bad magic"); return 0; }
            int triCount = BitConverter.ToInt32(data, 12);
            int need = 16 + triCount * 9 * 4;
            if (triCount < 0 || data.Length < need) { Log($"   mesh collision: truncated ({data.Length} < {need})"); return 0; }

            float F32(int i) => BitConverter.ToSingle(data, i);
            int p = 16, added = 0;
            for (int t = 0; t < triCount; t++, p += 36)
            {
                float ax = F32(p),      ay = F32(p + 4),  az = F32(p + 8);
                float bx = F32(p + 12), by = F32(p + 16), bz = F32(p + 20);
                float cx = F32(p + 24), cy = F32(p + 28), cz = F32(p + 32);

                // plane normal = (b-a) x (c-a), normalized
                float ux = bx - ax, uy = by - ay, uz = bz - az;
                float vx = cx - ax, vy = cy - ay, vz = cz - az;
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6f) continue;
                nx /= len; ny /= len; nz /= len;

                byte[] q = (byte[])template.Clone();
                PutVec(q, 0x00, ax, ay, az);
                PutVec(q, 0x10, bx, by, bz);
                PutVec(q, 0x20, cx, cy, cz);
                PutVec(q, 0x30, nx, ny, nz);
                outp.Add(q);
                added++;
            }
            return added;
        }

        /// <summary>Move the spawned fish to WaterLevel-FishDepth by writing their depth directly (data only) —
        /// replaces the crash-prone FishingInitFish code patch.
        ///
        /// Depth is the CFrame translation Y at <c>fish+0x1264</c> (Fish::Get/SetPosition route through the frame
        /// at fish+0x1250; the +0xB0..0xB8 "LivePos" fields are only a readout cache). FishingStepFish moves the
        /// fish with a depth delta hardcoded to 0, so the depth is a fixed point of its per-frame read/add/write
        /// loop — one write to the real translation sticks (we also mirror the cache for the same-frame visual).</summary>
        internal static void ApplyFishDepth(float fishDepth)
        {
            uint p = Memory.ReadUInt(FishingSpot.Fish) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long baseAddr = Memory.ToMmu(p);
            float depth = Memory.ReadFloat(FishingSpot.WaterLevel) - fishDepth;
            int num = Memory.ReadInt(FishingSpot.FishNum);

            const int FrameDepthY = 0x1264;   // CFrame translation Y (depth) — the authoritative field
            for (int i = 0; i < num && i < 6; i++)
            {
                long fish = baseAddr + (long)i * FishSlotOffsets.Stride;
                Memory.WriteFloat(fish + FrameDepthY, depth);                 // authoritative
                Memory.WriteFloat(fish + FishSlotOffsets.LivePosZ, depth);    // readout cache (immediate visual)
            }
            Log($"   fish moved to WaterLevel-{fishDepth} ({num} fish)");
        }

        private static void PutVec(byte[] b, int off, float x, float y, float z)
        {
            Array.Copy(BitConverter.GetBytes(x), 0, b, off + 0, 4);
            Array.Copy(BitConverter.GetBytes(y), 0, b, off + 4, 4);
            Array.Copy(BitConverter.GetBytes(z), 0, b, off + 8, 4);
        }
    }
}
