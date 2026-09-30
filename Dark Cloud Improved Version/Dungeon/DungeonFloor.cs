using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The floor's height under a point of the active dungeon floor, read from the same collision the engine walks on:
    /// the map part under the point (setCollisionData 0x1C0FC0 — the tile grid, or the placed-parts list), its collision frame
    /// placed as the engine places it (turned r × −90° about the vertical, at tile × 160), and the ready-built CCPolys of each
    /// collision node, taken to the world by the node's local matrix under its parent's. The floor is the highest upward-facing
    /// triangle under the point at or below the given height.</summary>
    internal static class DungeonFloor
    {
        private const float StepUp    = 10f;     // a floor this far above the query height still counts (a pellet skims low)
        private const float FloorNy   = 0.5f;    // a triangle this upright (|normal.h| / |normal|) or more is floor, not wall
        private const int   MaxPolys  = 4096;
        private const int   MaxDepth  = 16;

        /// <summary>The floor height under (<paramref name="x"/>, <paramref name="y"/>) at or below <paramref name="fromH"/>
        /// (+ a small step); false when no floor is found there.</summary>
        internal static bool HeightAt(float x, float y, float fromH, out float h)
        {
            h = 0f; bool found = false; float best = float.NegativeInfinity;
            uint map = Memory.ReadGuestPtr(DungeonTileGrid.NowDngMapPtr);
            if (!Memory.IsValidGuest(map)) return false;
            long m = Memory.ToMmu(map);
            if (Memory.ReadInt(m + DungeonTileGrid.GatherModeOffset) == 1)
            {
                int cx = (int)(x / DungeonTileGrid.TileWorldSize), cy = (int)(y / DungeonTileGrid.TileWorldSize);
                for (int ty = cy - 1; ty <= cy + 1; ty++)
                for (int tx = cx - 1; tx <= cx + 1; tx++)
                {
                    if (tx < 0 || ty < 0 || tx >= DungeonTileGrid.GridSize || ty >= DungeonTileGrid.GridSize) continue;
                    long tile = m + (tx + ty * DungeonTileGrid.GridSize) * DungeonTileGrid.TileStride;
                    int part = Memory.ReadInt(tile + DungeonTileGrid.TilePartsOffset);
                    if (part < 0) continue;
                    long entry = m + DungeonTileGrid.PartsTableOffset + (long)part * DungeonTileGrid.PartsStride;
                    int r = Memory.ReadInt(tile + DungeonTileGrid.TileRotOffset) + Memory.ReadShort(entry + DungeonTileGrid.PartRotBase);
                    Part(entry, r, tx * DungeonTileGrid.TileWorldSize, 0f, ty * DungeonTileGrid.TileWorldSize, x, y, fromH, ref best, ref found);
                }
            }
            else
            {
                for (int i = 0; i < 256; i++)
                {
                    long entry = m + DungeonTileGrid.PartsTableOffset + (long)i * DungeonTileGrid.PartsStride;
                    if (Memory.ReadInt(m + (long)i * DungeonTileGrid.PartsStride + DungeonTileGrid.PlacedActive) == 0) break;
                    long pos = m + (long)i * DungeonTileGrid.PartsStride + DungeonTileGrid.PlacedPos;
                    int r = (int)Memory.ReadFloat(m + (long)i * DungeonTileGrid.PartsStride + DungeonTileGrid.PlacedRot) + Memory.ReadShort(entry + DungeonTileGrid.PartRotBase);
                    Part(entry, r, Memory.ReadFloat(pos), Memory.ReadFloat(pos + 4), Memory.ReadFloat(pos + 8), x, y, fromH, ref best, ref found);
                }
            }
            if (found) h = best;
            return found;
        }

        /// <summary>One placed part: its collision frame turned by the engine's wrapped quarter-turn count and set at (px, ph, py).</summary>
        private static void Part(long entry, int r, float px, float ph, float py, float x, float y, float fromH, ref float best, ref bool found)
        {
            uint frame = Memory.ReadGuestPtr(entry + DungeonTileGrid.PartColFrame);
            if (!Memory.IsValidGuest(frame)) return;
            if (r > 3) r -= 3;
            if (r == 3) r = -1;
            float[] root = Local(Memory.ToMmu(frame), rotY: (float)(r * -90.0 * Math.PI / 180.0), pos: new[] { px, ph, py });
            Walk(Memory.ToMmu(frame), root, x, y, fromH, ref best, ref found, 0);
        }

        /// <summary>A collision node and its children (the engine's PickUpNearPoly__6CFrame walk): each node's polys under
        /// <paramref name="world"/>; children unless the node stops the descent (bit 1) or is skipped (bit 2).</summary>
        private static void Walk(long node, float[] world, float x, float y, float fromH, ref float best, ref bool found, int depth)
        {
            if (depth > MaxDepth) return;
            uint flags = (uint)Memory.ReadInt(node + DungeonTileGrid.FrameFlags);
            if (flags == 4) return;
            uint col = Memory.ReadGuestPtr(node + DungeonTileGrid.FrameColObj);
            if ((flags & 1) != 0 && Memory.IsValidGuest(col)) Polys(Memory.ToMmu(col), world, x, y, fromH, ref best, ref found);
            if ((flags & 2) != 0 || (flags & 4) != 0) return;
            for (uint c = Memory.ReadGuestPtr(node + DungeonTileGrid.FrameFirstChild); Memory.IsValidGuest(c); c = Memory.ReadGuestPtr(Memory.ToMmu(c) + DungeonTileGrid.FrameNextSibling))
            {
                long cn = Memory.ToMmu(c);
                Walk(cn, Mul(Local(cn), world), x, y, fromH, ref best, ref found, depth + 1);
            }
        }

        private static void Polys(long col, float[] w, float x, float y, float fromH, ref float best, ref bool found)
        {
            uint arr = Memory.ReadGuestPtr(col + DungeonTileGrid.ColObjPolys);
            int n = Memory.ReadInt(col + DungeonTileGrid.ColObjPolyCount);
            if (!Memory.IsValidGuest(arr) || n <= 0 || n > MaxPolys) return;
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(arr), n * DungeonTileGrid.ColPolyStride);
            if (b == null || b.Length < n * DungeonTileGrid.ColPolyStride) return;
            var v = new float[9];
            for (int i = 0; i < n; i++)
            {
                int o = i * DungeonTileGrid.ColPolyStride;
                for (int k = 0; k < 3; k++)
                {
                    float lx = BitConverter.ToSingle(b, o + k * 16), lh = BitConverter.ToSingle(b, o + k * 16 + 4), ly = BitConverter.ToSingle(b, o + k * 16 + 8);
                    v[k * 3]     = lx * w[0] + lh * w[4] + ly * w[8]  + w[12];
                    v[k * 3 + 1] = lx * w[1] + lh * w[5] + ly * w[9]  + w[13];
                    v[k * 3 + 2] = lx * w[2] + lh * w[6] + ly * w[10] + w[14];
                }
                // normal = (v1 − v0) × (v2 − v0), components (x, h, y)
                float ax = v[3] - v[0], ah = v[4] - v[1], ay = v[5] - v[2], bx = v[6] - v[0], bh = v[7] - v[1], by = v[8] - v[2];
                float nx = ah * by - ay * bh, nh = ay * bx - ax * by, ny = ax * bh - ah * bx;
                float len = (float)Math.Sqrt(nx * nx + nh * nh + ny * ny);
                if (len < 1e-6f || Math.Abs(nh) / len < FloorNy) continue;
                // barycentric in the ground plane (x, y)
                float d = (v[5] - v[8]) * (v[0] - v[6]) + (v[6] - v[3]) * (v[2] - v[8]);
                if (Math.Abs(d) < 1e-6f) continue;
                float l0 = ((v[5] - v[8]) * (x - v[6]) + (v[6] - v[3]) * (y - v[8])) / d;
                float l1 = ((v[8] - v[2]) * (x - v[6]) + (v[0] - v[6]) * (y - v[8])) / d;
                float l2 = 1f - l0 - l1;
                const float E = -1e-4f;
                if (l0 < E || l1 < E || l2 < E) continue;
                float hh = l0 * v[1] + l1 * v[4] + l2 * v[7];
                if (hh > fromH + StepUp || hh <= best) continue;
                best = hh; found = true;
            }
        }

        /// <summary>A node's local matrix (row-vector, 16 floats), as GetLWMatrix builds it; <paramref name="rotY"/> and
        /// <paramref name="pos"/> stand in for the part root's SetRotation / SetPosition.</summary>
        private static float[] Local(long node, float? rotY = null, float[] pos = null)
        {
            byte[] b = Memory.ReadBytesBatch(node + DungeonTileGrid.FrameMatrix, 0x80);
            var m = new float[16];
            for (int i = 0; i < 16; i++) m[i] = BitConverter.ToSingle(b, i * 4);
            bool composed = rotY != null || BitConverter.ToInt32(b, DungeonTileGrid.FrameComposed - DungeonTileGrid.FrameMatrix) != 0;
            if (!composed) return m;
            float F(int off) => BitConverter.ToSingle(b, off - DungeonTileGrid.FrameMatrix);
            for (int r = 0; r < 3; r++) { float k = F(DungeonTileGrid.FrameScale + r * 4); if (k != 0f) for (int c = 0; c < 4; c++) m[r * 4 + c] *= k; }
            uint rflags = (uint)BitConverter.ToInt32(b, DungeonTileGrid.FrameRotFlags - DungeonTileGrid.FrameMatrix) | (rotY != null ? 1u : 0u);
            bool keepT = (rflags & 2) != 0;
            float tx = m[12], th = m[13], ty = m[14];
            if (keepT) { m[12] = m[13] = m[14] = 0f; }
            if ((rflags & 1) != 0)
            {
                float ax = rotY != null ? 0f : F(DungeonTileGrid.FrameRot), ay = rotY ?? F(DungeonTileGrid.FrameRot + 4), az = rotY != null ? 0f : F(DungeonTileGrid.FrameRot + 8);
                if (ax != 0f) m = Mul(m, RotX(ax));
                if (ay != 0f) m = Mul(m, RotY(ay));
                if (az != 0f) m = Mul(m, RotZ(az));
            }
            float[] p = pos ?? new[] { F(DungeonTileGrid.FramePos), F(DungeonTileGrid.FramePos + 4), F(DungeonTileGrid.FramePos + 8) };
            if (keepT) { m[12] = tx; m[13] = th; m[14] = ty; }
            m[12] += p[0]; m[13] += p[1]; m[14] += p[2];
            return m;
        }

        // The engine's RotMatrixY (0x123650): row 0 = (cos, 0, −sin), row 2 = (sin, 0, cos); X and Z the same pattern.
        private static float[] RotY(float a) { float c = (float)Math.Cos(a), s = (float)Math.Sin(a); return new float[] { c,0,-s,0, 0,1,0,0, s,0,c,0, 0,0,0,1 }; }
        private static float[] RotX(float a) { float c = (float)Math.Cos(a), s = (float)Math.Sin(a); return new float[] { 1,0,0,0, 0,c,s,0, 0,-s,c,0, 0,0,0,1 }; }
        private static float[] RotZ(float a) { float c = (float)Math.Cos(a), s = (float)Math.Sin(a); return new float[] { c,s,0,0, -s,c,0,0, 0,0,1,0, 0,0,0,1 }; }

        /// <summary>a × b (row-vector: a's transform first, then b's).</summary>
        private static float[] Mul(float[] a, float[] b)
        {
            var r = new float[16];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++)
                r[i * 4 + j] = a[i * 4] * b[j] + a[i * 4 + 1] * b[4 + j] + a[i * 4 + 2] * b[8 + j] + a[i * 4 + 3] * b[12 + j];
            return r;
        }
    }
}
