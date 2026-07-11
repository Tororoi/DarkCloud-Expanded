using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class CustomXiaoEffects
    {

        // ── Angel Gear: pellet halo + homing ────────────────────────────────────────────────
        private const float HaloRadius       = 8f;     // halo ring radius around the player
        private const float HaloHeight      = 20f;    // ring height above the player's feet
        private const float HaloSpinStep    = 0.08f;  // radians per tick (~0.85 rev/s at 15ms)
        private const float HomingRange     = 300f;   // any live enemy this close to the player releases the halo
        private const float HomingAimLift   = 5f;     // aim above the enemy's ground point
        private const float MinHomingSpeed  = 3f;     // floor for pellets captured while nearly stationary
        private const int   HaloTickMs      = 15;
        private const int   PelletLifeTopUp = 120;    // native spawn lifetime — rewritten so held pellets never expire

        /// <summary>
        /// Angel Gear extra: pellets fired with no live enemy within <see cref="HomingRange"/> of the
        /// player are captured into a spinning halo above her head (position rewritten every tick,
        /// velocity zeroed, lifetime topped up, collision off). The moment any enemy comes within
        /// range, every held pellet is released and HOMES: velocity re-aimed at its nearest live enemy
        /// each tick at the pellet's own captured speed (collision back on). Pellets fired while
        /// enemies are already near home immediately. Runs alongside the healing effect.
        /// </summary>
        public static void AngelGearHaloEffect()
        {
            var mode  = new int[WeaponCollision.PlayerShotPool.SlotCount];    // 0 = untracked, 1 = halo, 2 = homing
            var speed = new float[WeaponCollision.PlayerShotPool.SlotCount];
            float spin = 0f;

            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                long poolNative = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr) & 0x1FFFFFFF;
                if (poolNative == 0 || poolNative >= 0x02000000) { Thread.Sleep(200); continue; }
                long pool = Memory.ToMmu(poolNative);

                float px = Memory.ReadFloat(Addresses.dunPositionX);
                float ph = Memory.ReadFloat(Addresses.dunPositionZ);   // height
                float py = Memory.ReadFloat(Addresses.dunPositionY);

                // Any live enemy within range of the PLAYER — with tile line of sight, so pellets are
                // never released at a target behind a dungeon wall — releases (and pre-empts) the halo.
                bool gridOk = RefreshTileGrid();
                bool enemyNear = gridOk &&
                    NearestLiveEnemy(px, py, out _, out _, out _, out float bestD, requireLos: true) &&
                    bestD <= HomingRange;

                // Even angular spacing for however many pellets are currently held.
                int held = 0;
                for (int i = 0; i < mode.Length; i++) if (mode[i] == 1) held++;
                int haloN = 0;
                spin += HaloSpinStep;

                for (int slot = 0; slot < WeaponCollision.PlayerShotPool.SlotCount; slot++)
                {
                    if (Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(pool, slot)) == 0)
                    { mode[slot] = 0; continue; }   // slot free (hit something or expired)

                    long velAddr = WeaponCollision.PlayerShotPool.VelAddr(pool, slot);
                    long posAddr = WeaponCollision.PlayerShotPool.PosAddr(pool, slot);

                    if (mode[slot] == 0)
                    {
                        // Newly fired pellet: capture its natural speed, then hold or hunt.
                        float vx = Memory.ReadFloat(velAddr), vh = Memory.ReadFloat(velAddr + 4), vy = Memory.ReadFloat(velAddr + 8);
                        speed[slot] = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy);
                        if (speed[slot] < MinHomingSpeed) speed[slot] = MinHomingSpeed;
                        mode[slot] = enemyNear ? 2 : 1;
                    }

                    if (mode[slot] == 1 && enemyNear)
                        mode[slot] = 2;   // release the halo

                    if (mode[slot] == 1)
                    {
                        double a = spin + (haloN++) * (2 * Math.PI / Math.Max(held, 1));
                        Memory.WriteFloat(posAddr,     px + HaloRadius * (float)Math.Cos(a));
                        Memory.WriteFloat(posAddr + 4, ph + HaloHeight);
                        Memory.WriteFloat(posAddr + 8, py + HaloRadius * (float)Math.Sin(a));
                        Memory.WriteFloat(velAddr, 0f); Memory.WriteFloat(velAddr + 4, 0f); Memory.WriteFloat(velAddr + 8, 0f);
                        Memory.WriteInt(pool + WeaponCollision.PlayerShotPool.LifetimeOffset + slot * 4, PelletLifeTopUp);
                        Memory.WriteInt(WeaponCollision.PlayerShotPool.NoCollideAddr(pool, slot), 1);
                    }
                    else if (mode[slot] == 2)
                    {
                        float sx = Memory.ReadFloat(posAddr), sh = Memory.ReadFloat(posAddr + 4), sy = Memory.ReadFloat(posAddr + 8);
                        if (!NearestLiveEnemy(sx, sy, out float ex, out float eh, out float ey, out _, requireLos: true))
                        { mode[slot] = 1; continue; }   // no VISIBLE target from here — back to the halo

                        float dx = ex - sx, dh = (eh + HomingAimLift) - sh, dy = ey - sy;
                        float len = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy);
                        if (len < 0.01f) continue;      // on top of the target — let the collision land
                        float k = speed[slot] / len;
                        Memory.WriteFloat(velAddr,     dx * k);
                        Memory.WriteFloat(velAddr + 4, dh * k);
                        Memory.WriteFloat(velAddr + 8, dy * k);
                        Memory.WriteInt(pool + WeaponCollision.PlayerShotPool.LifetimeOffset + slot * 4, PelletLifeTopUp);
                        Memory.WriteInt(WeaponCollision.PlayerShotPool.NoCollideAddr(pool, slot), 0);
                    }
                }
                Thread.Sleep(HaloTickMs);
            }

            // Weapon swapped / state left: free anything still held so pellets die naturally.
            long endNative = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr) & 0x1FFFFFFF;
            if (endNative != 0 && endNative < 0x02000000)
            {
                long pool = Memory.ToMmu(endNative);
                for (int slot = 0; slot < WeaponCollision.PlayerShotPool.SlotCount; slot++)
                    if (mode[slot] == 1)
                    {
                        Memory.WriteInt(WeaponCollision.PlayerShotPool.NoCollideAddr(pool, slot), 0);
                        Memory.WriteInt(pool + WeaponCollision.PlayerShotPool.LifetimeOffset + slot * 4, 30);
                    }
            }
        }

        // ── tile-grid line of sight (see DungeonTileGrid for the RE) ────────────────────────
        private static readonly bool[] _tileOpen = new bool[DungeonTileGrid.GridSize * DungeonTileGrid.GridSize];
        private static readonly int[]  _tilePart = new int[DungeonTileGrid.GridSize * DungeonTileGrid.GridSize];
        private static readonly int[]  _tileRot  = new int[DungeonTileGrid.GridSize * DungeonTileGrid.GridSize];
        private static System.Collections.Generic.Dictionary<int, float[]> _partSegs =
            new System.Collections.Generic.Dictionary<int, float[]>();   // partIdx → flat 2D wall segments {x0,z0,x1,z1,...} in part-local space
        private static System.Collections.Generic.Dictionary<int, int> _partRotBase =
            new System.Collections.Generic.Dictionary<int, int>();
        private static long _gridInstance;   // CDungeonMap the cache was read from (refreshed per floor)
        private static int  _gridRefreshTick;
        private static float[] _doorSegs = new float[0];                       // shared door frame, local 2D wall segments
        private static readonly bool[]  _doorActive = new bool[DungeonTileGrid.DoorCount];
        private static readonly float[] _doorX = new float[DungeonTileGrid.DoorCount];
        private static readonly float[] _doorY = new float[DungeonTileGrid.DoorCount];

        /// <summary>Refresh the walkable-tile cache when the CDungeonMap instance changes (new floor) or
        /// every ~100 ticks (cheap: one 6.25KB batched read).</summary>
        private static bool RefreshTileGrid()
        {
            long native = (uint)Memory.ReadInt(DungeonTileGrid.NowDngMapPtr) & 0x1FFFFFFF;
            if (native == 0 || native >= 0x02000000) { _gridInstance = 0; return false; }
            long inst = Memory.ToMmu(native);
            if (inst == _gridInstance && ++_gridRefreshTick % 100 != 0)
            {
                if (_gridRefreshTick % 10 == 0) RefreshDoors(inst);
                return true;
            }
            bool newFloor = inst != _gridInstance;
            _gridInstance = inst;

            int n = DungeonTileGrid.GridSize * DungeonTileGrid.GridSize;
            byte[] raw = Memory.ReadBytesBatch(inst + DungeonTileGrid.TilePartsOffset, n * DungeonTileGrid.TileStride);
            if (raw == null) { _gridInstance = 0; return false; }
            for (int i = 0; i < n; i++)
            {
                _tilePart[i] = BitConverter.ToInt32(raw, i * DungeonTileGrid.TileStride);
                _tileRot[i]  = BitConverter.ToInt32(raw, i * DungeonTileGrid.TileStride + 4);
                _tileOpen[i] = _tilePart[i] != -1;
            }
            if (newFloor) ExtractPartGeometry(inst);   // part models are static for the floor's lifetime
            RefreshDoors(inst);
            return true;
        }

        /// <summary>Door slots are dynamic (doors open/unlock mid-floor): re-read active flags and
        /// positions. One 1.5KB batched read.</summary>
        private static void RefreshDoors(long inst)
        {
            byte[] raw = Memory.ReadBytesBatch(inst + DungeonTileGrid.DoorSlotsOffset,
                                               DungeonTileGrid.DoorCount * DungeonTileGrid.DoorStride);
            if (raw == null) return;
            for (int i = 0; i < DungeonTileGrid.DoorCount; i++)
            {
                int b = i * DungeonTileGrid.DoorStride;
                _doorActive[i] = BitConverter.ToInt32(raw, b) != 0;
                _doorX[i] = BitConverter.ToSingle(raw, b + DungeonTileGrid.DoorPosOffset);
                _doorY[i] = BitConverter.ToSingle(raw, b + DungeonTileGrid.DoorPosOffset + 8);
            }
        }

        /// <summary>Read each distinct map part's collision triangles from its live CFrame → MDT blob
        /// (see DungeonTileGrid for the RE) and keep the WALL edges as 2D part-local segments: pillars
        /// and in-room walls become visible to line of sight. Every read is validated (pointer ranges,
        /// vertex-index bounds, coordinate sanity) — a part that fails extraction just contributes no
        /// segments, degrading that tile to the coarse tile-only test.</summary>
        private static void ExtractPartGeometry(long inst)
        {
            _partSegs.Clear(); _partRotBase.Clear();
            int parts = 0, segs = 0;
            var distinct = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < _tilePart.Length; i++) if (_tilePart[i] >= 0) distinct.Add(_tilePart[i]);

            foreach (int idx in distinct)
            {
                long entry = inst + DungeonTileGrid.PartsTableOffset + (long)idx * DungeonTileGrid.PartsStride;
                _partRotBase[idx] = (short)Memory.ReadUShort(entry + DungeonTileGrid.PartRotBase);
                long frame = (uint)Memory.ReadInt(entry + DungeonTileGrid.PartColFrame) & 0x1FFFFFFF;
                var list = new System.Collections.Generic.List<float>();
                if (frame != 0 && frame < 0x02000000) CollectFrameSegments(Memory.ToMmu(frame), list, 0);
                _partSegs[idx] = list.ToArray();
                parts++; segs += list.Count / 4;
            }

            // The shared door collision frame (engine translates it per active door slot, no rotation).
            var door = new System.Collections.Generic.List<float>();
            long doorFrame = (uint)Memory.ReadInt(inst + DungeonTileGrid.DoorFrameOffset) & 0x1FFFFFFF;
            if (doorFrame != 0 && doorFrame < 0x02000000) CollectFrameSegments(Memory.ToMmu(doorFrame), door, 0);
            _doorSegs = door.ToArray();
            Console.WriteLine($"[AngelLOS] floor geometry: {parts} part types, {segs} wall segments, door: {_doorSegs.Length / 4} segments");
        }

        /// <summary>Walk a collision CFrame tree (flags/colObj/child/sibling per DungeonTileGrid) and
        /// append wall-edge segments from each node's MDT triangle blob.</summary>
        private static void CollectFrameSegments(long frameMmu, System.Collections.Generic.List<float> outSegs, int depth)
        {
            if (depth > 6) return;
            int flags = Memory.ReadInt(frameMmu + DungeonTileGrid.FrameFlags);
            if (flags == 4) return;
            long colObj = (uint)Memory.ReadInt(frameMmu + DungeonTileGrid.FrameColObj) & 0x1FFFFFFF;
            if (colObj != 0 && colObj < 0x02000000 && (flags & 1) != 0)
                AppendMdtWallSegments(Memory.ToMmu(colObj), outSegs);
            if ((flags & 2) != 0) return;
            long ch = (uint)Memory.ReadInt(frameMmu + DungeonTileGrid.FrameFirstChild) & 0x1FFFFFFF;
            for (int guard = 0; ch != 0 && ch < 0x02000000 && guard < 64; guard++)
            {
                long chMmu = Memory.ToMmu(ch);
                if ((Memory.ReadInt(chMmu + DungeonTileGrid.FrameFlags) & 4) == 0)
                    CollectFrameSegments(chMmu, outSegs, depth + 1);
                ch = (uint)Memory.ReadInt(chMmu + DungeonTileGrid.FrameNextSibling) & 0x1FFFFFFF;
            }
        }

        private static void AppendMdtWallSegments(long colObj, System.Collections.Generic.List<float> outSegs)
        {
            long blob = (uint)Memory.ReadInt(colObj + DungeonTileGrid.ColObjBlobPtr) & 0x1FFFFFFF;
            if (blob == 0 || blob >= 0x02000000) return;
            blob = Memory.ToMmu(blob);
            int vertsOff = Memory.ReadInt(blob + DungeonTileGrid.BlobVertsOffset);
            int polysOff = Memory.ReadInt(blob + DungeonTileGrid.BlobPolysOffset);
            if (vertsOff <= 0 || polysOff <= 0 || vertsOff > 0x100000 || polysOff > 0x100000) return;
            long verts = blob + vertsOff, polySec = blob + polysOff;
            int count = Memory.ReadInt(polySec + DungeonTileGrid.PolySecCount);
            if (count <= 0 || count > 2048) return;

            byte[] recs = Memory.ReadBytesBatch(polySec + DungeonTileGrid.PolySecRecords, count * DungeonTileGrid.PolyRecordStride);
            if (recs == null) return;
            int maxIdx = 0;
            for (int p = 0; p < count; p++)
                for (int k = 0; k < 3; k++)
                    maxIdx = Math.Max(maxIdx, BitConverter.ToInt32(recs, p * DungeonTileGrid.PolyRecordStride + k * 4));
            if (maxIdx < 0 || maxIdx > 0x4000) return;
            byte[] vraw = Memory.ReadBytesBatch(verts, (maxIdx + 1) * 0x10);
            if (vraw == null) return;

            for (int p = 0; p < count; p++)
            {
                var vx = new float[3]; var vy = new float[3]; var vz = new float[3];
                bool sane = true;
                for (int k = 0; k < 3; k++)
                {
                    int vi = BitConverter.ToInt32(recs, p * DungeonTileGrid.PolyRecordStride + k * 4);
                    vx[k] = BitConverter.ToSingle(vraw, vi * 0x10);
                    vy[k] = BitConverter.ToSingle(vraw, vi * 0x10 + 4);
                    vz[k] = BitConverter.ToSingle(vraw, vi * 0x10 + 8);
                    if (float.IsNaN(vx[k]) || Math.Abs(vx[k]) > 4000f || Math.Abs(vz[k]) > 4000f || Math.Abs(vy[k]) > 4000f) sane = false;
                }
                if (!sane) continue;
                // wall filter: face normal mostly horizontal, and the triangle spans pellet altitude
                float ax = vx[1] - vx[0], ay = vy[1] - vy[0], az = vz[1] - vz[0];
                float bx = vx[2] - vx[0], by = vy[2] - vy[0], bz = vz[2] - vz[0];
                float nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                float nlen = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nlen < 1e-3f || Math.Abs(ny) / nlen > 0.7f) continue;          // floor/ceiling
                float yMin = Math.Min(vy[0], Math.Min(vy[1], vy[2]));
                float yMax = Math.Max(vy[0], Math.Max(vy[1], vy[2]));
                if (yMax < 5f || yMin > 40f) continue;                              // outside pellet altitudes
                for (int k = 0; k < 3; k++)
                {
                    int k2 = (k + 1) % 3;
                    if (Math.Abs(vx[k] - vx[k2]) < 0.5f && Math.Abs(vz[k] - vz[k2]) < 0.5f) continue; // vertical edge
                    outSegs.Add(vx[k]); outSegs.Add(vz[k]); outSegs.Add(vx[k2]); outSegs.Add(vz[k2]);
                }
            }
        }

        private static bool TileOpenAt(int tx, int ty) =>
            tx >= 0 && ty >= 0 && tx < DungeonTileGrid.GridSize && ty < DungeonTileGrid.GridSize &&
            _tileOpen[tx + ty * DungeonTileGrid.GridSize];

        /// <summary>Straight segment (x0,y0)→(x1,y1) in world coords crosses only open tiles?
        /// Supercover DDA: on diagonal cell steps BOTH orthogonal neighbors must be open too, so a
        /// pellet can never cut a wall corner.</summary>
        private static bool TileLineOfSight(float x0, float y0, float x1, float y1)
        {
            float inv = 1f / DungeonTileGrid.TileWorldSize, bias = DungeonTileGrid.TileWorldBias;
            int tx = (int)((x0 + bias) * inv), ty = (int)((y0 + bias) * inv);
            int ex = (int)((x1 + bias) * inv), ey = (int)((y1 + bias) * inv);
            if (!TileOpenAt(tx, ty) || !TileOpenAt(ex, ey)) return false;

            int sx = Math.Sign(ex - tx), sy = Math.Sign(ey - ty);
            float dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            // distance (as a fraction of the segment) to the first vertical / horizontal tile border
            float bx = sx > 0 ? ((tx + 1) * DungeonTileGrid.TileWorldSize - bias - x0) : sx < 0 ? (x0 - (tx * DungeonTileGrid.TileWorldSize - bias)) : float.MaxValue;
            float by = sy > 0 ? ((ty + 1) * DungeonTileGrid.TileWorldSize - bias - y0) : sy < 0 ? (y0 - (ty * DungeonTileGrid.TileWorldSize - bias)) : float.MaxValue;
            float tMaxX = dx > 0.0001f ? bx / dx : float.MaxValue;
            float tMaxY = dy > 0.0001f ? by / dy : float.MaxValue;
            float tDeltaX = dx > 0.0001f ? DungeonTileGrid.TileWorldSize / dx : float.MaxValue;
            float tDeltaY = dy > 0.0001f ? DungeonTileGrid.TileWorldSize / dy : float.MaxValue;

            Span<int> crossed = stackalloc int[128];
            int nCrossed = 0;
            crossed[nCrossed++] = tx; crossed[nCrossed++] = ty;
            for (int guard = 0; guard < 62 && (tx != ex || ty != ey); guard++)
            {
                if (Math.Abs(tMaxX - tMaxY) < 1e-6f)
                {   // exact corner: require both orthogonal neighbors open (no corner cutting)
                    if (!TileOpenAt(tx + sx, ty) || !TileOpenAt(tx, ty + sy)) return false;
                    tx += sx; ty += sy; tMaxX += tDeltaX; tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY) { tx += sx; tMaxX += tDeltaX; }
                else                    { ty += sy; tMaxY += tDeltaY; }
                if (!TileOpenAt(tx, ty)) return false;
                crossed[nCrossed++] = tx; crossed[nCrossed++] = ty;
            }

            // Fine pass: the ray must also clear each crossed tile's PART GEOMETRY (pillars,
            // in-room walls) — the ray is inverse-transformed into part-local space per tile
            // (cheaper than transforming the segments) and tested edge-vs-edge.
            for (int c = 0; c < nCrossed; c += 2)
                if (RayHitsTileGeometry(crossed[c], crossed[c + 1], x0, y0, x1, y1)) return false;

            // Doors: shared segment set, translated to each ACTIVE door slot (no rotation — the
            // engine itself only translates the shared door frame). Cheap AABB pre-reject per door.
            if (_doorSegs.Length > 0)
            {
                float rxMin = Math.Min(x0, x1) - 200f, rxMax = Math.Max(x0, x1) + 200f;
                float ryMin = Math.Min(y0, y1) - 200f, ryMax = Math.Max(y0, y1) + 200f;
                for (int d = 0; d < DungeonTileGrid.DoorCount; d++)
                {
                    if (!_doorActive[d]) continue;
                    if (_doorX[d] < rxMin || _doorX[d] > rxMax || _doorY[d] < ryMin || _doorY[d] > ryMax) continue;
                    float lx0 = x0 - _doorX[d], ly0 = y0 - _doorY[d];
                    float lx1 = x1 - _doorX[d], ly1 = y1 - _doorY[d];
                    for (int k = 0; k + 3 < _doorSegs.Length; k += 4)
                        if (SegmentsIntersect(lx0, ly0, lx1, ly1, _doorSegs[k], _doorSegs[k + 1], _doorSegs[k + 2], _doorSegs[k + 3]))
                            return false;
                }
            }
            return true;
        }

        /// <summary>Does the 2D world segment hit any wall segment of the part placed on tile (tx,ty)?
        /// Placement per setCollisionData: rotate r×−90° about Y, translate tile×160, where
        /// r = wrap(tileRotVariant + partRotBase) with the engine's exact wrap
        /// (r &gt; 3 → r−3, then r == 3 → −1).</summary>
        private static bool RayHitsTileGeometry(int tx, int ty, float x0, float y0, float x1, float y1)
        {
            int i = tx + ty * DungeonTileGrid.GridSize;
            int part = _tilePart[i];
            if (part < 0 || !_partSegs.TryGetValue(part, out float[] segs) || segs.Length == 0) return false;

            int r = _tileRot[i] + (_partRotBase.TryGetValue(part, out int rb) ? rb : 0);
            if (r > 3) r -= 3;
            if (r == 3) r = -1;
            // angle = r × −90°; inverse rotation = transpose. cos/sin exact for quarter turns.
            int q = ((-r % 4) + 4) % 4;             // forward angle in +90° quarter turns
            float c = q == 0 ? 1f : q == 2 ? -1f : 0f;
            float sn = q == 1 ? 1f : q == 3 ? -1f : 0f;
            float ox = tx * DungeonTileGrid.TileWorldSize, oy = ty * DungeonTileGrid.TileWorldSize;
            // local = R^T · (world − T):  lx = c·wx − s·wz ; lz = s·wx + c·wz
            float ax0 = c * (x0 - ox) - sn * (y0 - oy), ay0 = sn * (x0 - ox) + c * (y0 - oy);
            float ax1 = c * (x1 - ox) - sn * (y1 - oy), ay1 = sn * (x1 - ox) + c * (y1 - oy);

            for (int k = 0; k + 3 < segs.Length; k += 4)
                if (SegmentsIntersect(ax0, ay0, ax1, ay1, segs[k], segs[k + 1], segs[k + 2], segs[k + 3]))
                    return true;
            return false;
        }

        private static bool SegmentsIntersect(float ax, float ay, float bx, float by,
                                              float cx, float cy, float dx, float dy)
        {
            float d1 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            float d2 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
            if (d1 * d2 > 0f) return false;
            float d3 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx);
            float d4 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
            return d3 * d4 <= 0f;
        }

        /// <summary>Nearest live enemy to (x, y) on the ground plane; false if none on the floor.</summary>
        private static bool NearestLiveEnemy(float x, float y, out float ex, out float eh, out float ey, out float dist, bool requireLos = false)
        {
            ex = eh = ey = 0f; dist = float.MaxValue;
            bool found = false;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                if (id == 0 || id == 0xFFFF) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long p = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX);
                float cx = Memory.ReadFloat(p), ch = Memory.ReadFloat(p + 4), cy = Memory.ReadFloat(p + 8);
                float dx = cx - x, dy = cy - y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d >= dist) continue;
                if (requireLos && !TileLineOfSight(x, y, cx, cy)) continue;
                dist = d; ex = cx; eh = ch; ey = cy; found = true;
            }
            return found;
        }

        // ── Angel Gear ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Angel Gear effect: Applies the Heal regeneration effect to all allies
        /// </summary>
        public static void AngelGearEffect()
        {
            //Initialize variables
            ushort HpValueAdd = 1;
            ushort Delay = 5000;
            ushort XiaoHp = 0;
            ushort XiaoMaxHp = 0;
            bool isHealXiao = false;

            //Run while Angel Gear is equipped and Player is in valid state
            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                //Fetch HP values for characters
                ushort ToanHp = Player.Toan.GetHp();
                ushort ToanMaxHp = Player.Toan.GetMaxHp();
                ushort GoroHp = Player.Goro.GetHp();
                ushort GoroMaxHp = Player.Goro.GetMaxHp();
                ushort RubyHp = Player.Ruby.GetHp();
                ushort RubyMaxHp = Player.Ruby.GetMaxHp();
                ushort UngagaHp = Player.Ungaga.GetHp();
                ushort UngagaMaxHp = Player.Ungaga.GetMaxHp();
                ushort OsmondHp = Player.Osmond.GetHp();
                ushort OsmondMaxHp = Player.Osmond.GetMaxHp();

                //Check for the Heal special attribute on the weapon
                if (Player.Weapon.GetCurrentWeaponSpecial2() % 16 < 8 ||
                    Player.Weapon.GetCurrentWeaponSpecial2() % 16 > 11)
                {
                    isHealXiao = true;
                    XiaoHp = Player.Xiao.GetHp();
                    XiaoMaxHp = Player.Xiao.GetMaxHp();
                }

                //Add the HP value to the characters current HP
                if (ToanHp < ToanMaxHp && ToanHp > 0) Player.Toan.SetHp((ushort)(ToanHp + HpValueAdd));
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Toan HP add: " + (ToanHp + HpValueAdd));
                if (GoroHp < GoroMaxHp && GoroHp > 0) Player.Goro.SetHp((ushort)(GoroHp + HpValueAdd));
                if (RubyHp < RubyMaxHp && RubyHp > 0) Player.Ruby.SetHp((ushort)(RubyHp + HpValueAdd));
                if (UngagaHp < UngagaMaxHp && UngagaHp > 0) Player.Ungaga.SetHp((ushort)(UngagaHp + HpValueAdd));
                if (OsmondHp < OsmondMaxHp && OsmondHp > 0) Player.Osmond.SetHp((ushort)(OsmondHp + HpValueAdd));

                //Only affect Xiao if Angel Gear does not have the Heal attribute already
                if (isHealXiao && XiaoHp < XiaoMaxHp && XiaoHp > 0) Player.Xiao.SetHp((ushort)(XiaoHp + HpValueAdd));

                //Wait in between additions
                Thread.Sleep(Delay);
            }
        }

        // ── Super Steve "Sphere Inheritance" ───────────────────────────────────────────────
        /// <summary>
        /// Super Steve (Xiao's ultimate slingshot) inherits the custom effect of the weapon whose SynthSphere
        /// is attached to it. A weapon Status-Broken into a SynthSphere records its SOURCE weapon id at the
        /// attach-entry's +0x02 (SetStatusBreak, ELF 0x2368D0), and attaching copies the whole 0x20-byte entry
        /// into the weapon record's ATTACH_LIST, so the source id survives in-record and can be read straight
        /// off the record — no stat-fingerprinting needed.
        ///
        /// This is the master dispatch loop: read the single attached sphere, then pulse each ability's driver
        /// with <c>active &amp;&amp; sphere == Items.X</c>. Enemy-side abilities reuse the CustomToanEffects
        /// drivers verbatim (they only touch enemy data); the Xiao body adaptations live in
        /// <see cref="SuperSteveAbilities"/>. Every driver is pulsed each tick (enabled or not) so it
        /// self-restores the instant the sphere is swapped — no explicit per-swap teardown needed.
        ///
        /// NOT every weapon's ability transfers. Excluded by design:
        ///   • Macho Sword, Wise Owl Sword, Chronicle 2 — rely on weapon ownership
        ///   • Buster Sword, 7 Branch Sword - modify upgrading / status-breaks
        /// </summary>
        public static void SuperSteveEffect()
        {
            var ssSun = new CustomToanEffects.SunHarvestState(EnemyAddresses.FloorSlots.Count);
            var xiaoCurse = new CustomToanEffects.CurseAddrs(Player.Xiao.status, Player.Xiao.statusTimer, Player.Xiao.hp);
            var ssEvilcise = new CustomToanEffects.CurseState();
            var ssManeater = new CustomToanEffects.CurseState();
            var xiaoTuna = new CustomGoroEffects.FrozenTunaWielder(Player.XiaoId, Player.Xiao.hp, Player.Xiao.maxHP,
                                                                   Player.Xiao.status, Player.Xiao.statusTimer);
            var ssTuna = new CustomGoroEffects.FrozenTunaState();
            var ssTallHammer = new CustomGoroEffects.TallHammerState();
            var ssCactus = new CustomUngagaEffects.CactusState();
            var ssSnail = new CustomOsmondEffects.SnailState();
            var ssStarBreaker = new CustomOsmondEffects.StarBreakerState();
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                int sphere = SuperSteveAbilities.AttachedSphere(rec);
                bool active = !Player.CheckDunIsPaused();
                // (The sphere-palette recolour is NOT driven here — WeaponTextureSwap runs its own always-on
                // thread so the swap also covers town/menu, where this dungeon dispatcher never runs.)

                // Toan Effects
                // Divine Guard (7th Heaven) + Guard Crush (Dark Cloud; 7th Heaven inherits Guard Crush by lineage).
                CustomToanEffects.SeventhHeavenSoftenAttacks(active && sphere == Items.seventhheaven);
                CustomToanEffects.DarkCloudDriveGuards(active && (sphere == Items.seventhheaven || sphere == Items.darkcloud));

                // Defensive Legacy (Aga's Sword): +15 Xiao defense.
                SuperSteveAbilities.DriveAgasSword(active && sphere == Items.agassword);

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo each tick.
                SuperSteveAbilities.DriveBraveArk(active && sphere == Items.braveark);

                // Bone Rapier: bone-door bypass (the Xiao dispatcher no longer force-clears it, so this owns it).
                CustomToanEffects.BoneRapierEffect(active && sphere == Items.bonerapier);

                // Solar Harvest (Sun Sword / Big Bang): ~1% of the floor's enemies drop a Sun attachment.
                CustomToanEffects.SunHarvestDrive(sphere == Items.sunsword || sphere == Items.bigbang, ssSun);

                // Curses (full inherit): curse Xiao. Not pause-gated — mirrors the Toan loops.
                CustomToanEffects.EvilciseDrive(sphere == Items.evilcise, xiaoCurse, ssEvilcise);
                CustomToanEffects.ManeaterDrive(sphere == Items.maneater, xiaoCurse, rec, ssManeater);

                // Quick Draw (Small Sword / Tsukikage / Heaven's Cloud): instant fire-on-release + rate-of-fire.
                SuperSteveAbilities.DriveSmallSword(active && (sphere == Items.smallsword || sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Moonlit Focus (Tsukikage / Heaven's Cloud): ×2 shot speed.
                SuperSteveAbilities.DriveTsukikage(active && (sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Heaven's Cloud (Heaven's Cloud): charge → grow the slingshot + pellet, flash, shrapnel burst.
                SuperSteveAbilities.DriveHeavensCloud(active && sphere == Items.heavenscloud);

                // Xiao Effects

                // Angel Gear: slow party-wide HP regen.
                SuperSteveAbilities.DriveAngelGear(active && sphere == Items.angelgear);

                // Goro Effects

                // Cold Storage (Frozen Tuna): WHP losses bank a healing pool that drains after Xiao is hit;
                // on-hit 5% chance to stop all non-ice enemies at the price of freezing Xiao too.
                CustomGoroEffects.FrozenTunaDrive(active && sphere == Items.frozentuna, xiaoTuna, equipSlot, ssTuna);

                // Tall Hammer: shrinks enemies Xiao's pellets hit.
                CustomGoroEffects.TallHammerDrive(active && sphere == Items.tallhammer, Player.XiaoId, ssTallHammer);

                // Ruby Effects

                // Mobius Ring: holding the shot ramps damage ×1.5 per 1.5s (flash per step); the fired
                // pellet gets the ramped damage + a Ruby-ball-style size to match.
                SuperSteveAbilities.DriveMobiusRing(active && sphere == Items.mobiusring);

                // Ungaga Effects

                // Absorb (Cactus): pellet hits restore Xiao's thirst scaled by damage (rock/metal/undead immune).
                CustomUngagaEffects.CactusDrive(active && sphere == Items.cactus, Player.XiaoId,
                                                Player.Xiao.thirst, Player.Xiao.thirstMax, ssCactus);

                // Osmond Effects

                // Snail: 5% chance on hit to inflict gooey on the struck enemy.
                CustomOsmondEffects.SnailDrive(active && sphere == Items.snail, Player.XiaoId, ssSnail);

                // Star Breaker: 2% chance on an enemy kill to receive an empty SynthSphere.
                CustomOsmondEffects.StarBreakerDrive(active && sphere == Items.starbreaker, ssStarBreaker);

                Thread.Sleep(16);
            }

            // Restore everything on unequip / character-switch / dungeon exit (no-ops if not driven).
            CustomToanEffects.SeventhHeavenSoftenAttacks(false);
            CustomToanEffects.DarkCloudDriveGuards(false);
            CustomToanEffects.BoneRapierEffect(false);
            CustomToanEffects.SunHarvestDrive(false, ssSun);
            CustomToanEffects.EvilciseDrive(false, xiaoCurse, ssEvilcise);
            CustomToanEffects.ManeaterDrive(false, xiaoCurse, 0, ssManeater);
            SuperSteveAbilities.DriveSmallSword(false);
            SuperSteveAbilities.DriveTsukikage(false);
            SuperSteveAbilities.DriveHeavensCloud(false);   // resets slingshot + flash latch
            SuperSteveAbilities.DriveAgasSword(false);
            SuperSteveAbilities.DriveMobiusRing(false);   // resets the damage ramp
            CustomGoroEffects.FrozenTunaDrive(false, xiaoTuna, 0, ssTuna);   // resets the healing pool
            // (palette restore is handled by WeaponTextureSwap's own thread — it repaints vanilla the
            // moment Super Steve is no longer Xiao's equipped weapon)
        }

    }
}
