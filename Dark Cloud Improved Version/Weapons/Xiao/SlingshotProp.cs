using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The GIANT SLINGSHOT "wings" prop for Guardian Reflector — v6: an independent, engine-stepped
    /// COPY of the weapon object. Her real slingshot never leaves her hand.
    ///
    /// WHAT ANIMATES THE POUCH (settled 2026-09-08): each slingshot ships its own animation pack
    /// (dun\mainchara\c04w.pac → c04w##.chr: .mot animating the pouch bone `null24`, .wgt = the
    /// VERTEX_ANIME elastic morph, and a cfg KEY table mirroring Xiao's body keys — 9 guard-loop,
    /// 11 構え引き draw 240–251, 12 hold, 13 撃ち shoot 251–255). The weapon object is a CCharacter
    /// with its own motion channel and FRAME_INF, stepped by the game in lockstep with her body.
    ///
    /// THE COPY (CharacterClone's engine-driven pattern, applied to the weapon):
    ///  · TREE: the 6-node weapon tree deep-copied into the WeaponCave (contiguous — MotionProc
    ///    indexes bones as root + i*0x270), links re-based, root parented to XIAO'S MODEL ROOT.
    ///    Draw__10CCharacter seeds the root from the slot's pos/rot/scale every draw and MGDraw
    ///    chains it through the parent's cached world — so the slot transform is a constant OFFSET
    ///    IN HER SPACE: welded at 60 fps with zero per-frame writes, and scale/orientation never
    ///    compound (nothing is baked into matrices).
    ///  · MESH: the weapon's visual is a software-skinned, double-buffered MDT (VERTEX_ANIME), so
    ///    visual + MDT + VU buffers are copied into the MeshCave (CopyMeshNodes' recipe, single-
    ///    buffered) — the copy and the live weapon never skin the same vertices.
    ///  · CHANNEL: the weapon's motion channel cloned into the MotionCave with its own FrameInf /
    ///    BoneMtx caves; skin list KEPT (tree-relative). Keys are driven on the COPY's MotionId.
    ///  · SLOT 3 (Mirage's clone-weapon slot — never co-wielded; Mirage's gate-flag writes stand
    ///    down while <see cref="Active"/>): the patched texgroup formula gives it the weapon's own
    ///    texture pass; NpcOpacity gives us the fade back.
    /// </summary>
    internal static class SlingshotProp
    {
        internal static bool Active { get; private set; }

        private const string Tag = "[SlingshotProp] ";
        private const int  Slot         = 3;
        private const int  CharCopySize = 0xD60;
        private const int  MotionStructSize = 0xC0;
        private const int  MaxNodes     = 8;             // WeaponCave: 0x1400 / 0x270
        private const int  ObjScale     = 0x90;          // CObject scale vec — Draw__10CCharacter → SetScale on the root

        // The weapon's own KEY table (c04w##.cfg).
        internal const int KeyGuardLoop = 9;
        internal const int KeyDraw      = 11;
        internal const int KeyHold      = 12;
        internal const int KeyShoot     = 13;
        internal const int KeyIdle      = 14;   // a dummy KEY (1,1,1.0) rewritten on the COPY's own table as a hold
        private  const int IdleFrame    = 254;  // end of the snap: pouch forward and taut (253 = the forward peak)
        private  const int KeyTableSize = 0x200; // 32 KEY entries {int start, int end, float speed, pad}
        // The authored release is 4 frames at play-rate 0.7 — too snappy at 4x — so the copy's
        // motion-speed override slows it while that key plays (-1 = the KEY rate).
        private const float ShootRate   = 0.3f;

        // Orientation preset (tuned live with a debug cycle, since removed): bits 0-1 = upright pitch quarter-turns,
        // bits 2-3 = facing yaw quarter-turns, composed onto the weapon's authored grip rotation
        // exactly as the earlier welded-weapon bake did (5 = tuned live). The draw re-seeds the ROOT
        // from the slot each frame, so this is baked into the root's child bone (chn30) instead —
        // never re-seeded, never motion-tracked — and the slot rotation stays zero.
        private const int OrientPreset = 7;     // pinned 2026-09-08

        private static uint  _rootGuest;                 // copied tree root (guest)
        private static uint  _liveRoot, _playerRoot;     // for change detection
        private static int   _nodeCount, _key = -1;
        private static float _scale, _up, _ahead, _pull, _orbit;
        private static volatile float _orbitTarget;      // wanted bearing (rad, relative to her facing)
        private static Thread _orbitThread;
        // Orbit smoothing runs at frame rate on its own thread (a 20 Hz step read as a staircase):
        // each frame closes OrbitEase of the remaining angle, at most OrbitRate rad (≈ 180° in 0.3 s).
        private const int   OrbitTickMs = 16;
        private const float OrbitEase   = 0.18f, OrbitRate = 0.20f;   // user: 0.2 (2026-09-09)
        private static uint  _pouchGuest;                // the copy's pouch bone (null24)
        private static uint  _shotNodeGuest;             // extra geometry-less child of the root: the shot-target point
        private static uint  _muzzleGuest;               // the copy's eff30 — where Xiao's own pellets spawn (BattleActionPlay_Jinn: SearchFrame "eff30", zero offset)
        private static float _idleX, _idleY, _idleZ;     // the pouch's idle local translation (scaled), from ScalePull
        // checkCollision (0x1AB740) raises the "player" point by the per-character body value before its
        // sphere test (table @0x26AFE0: Toan 16, XIAO 14, Goro 16, Ruby 16, Ungaga 18, Osmond 15), so the
        // point enemy shots must be aimed at is the POUCH lowered by this much.
        private const float PlayerBodyLift = 14f;

        /// <param name="pull">pouch draw travel in weapon units at x1 (authored ≈ 4.2); ≤ 0 = authored</param>
        internal static bool Spawn(float scale, float up, float ahead, float pull)
        {
            if (Active) return true;
            _scale = scale; _up = up; _ahead = ahead; _pull = pull;
            _orbit = _orbitTarget;                            // appear on the wanted bearing, no swing-in
            if (!CopyTree() || !CopyMesh() || !RegisterSlot()) return false;
            PlaceShotNode();
            if (_orbitThread == null || !_orbitThread.IsAlive)
            { _orbitThread = new Thread(OrbitLoop) { IsBackground = true, Name = "SlingshotOrbit" }; _orbitThread.Start(); }
            _key = KeyIdle;
            Active = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"weapon copy up (x{scale}, slot {Slot}); her slingshot untouched");
            return true;
        }

        /// <summary>Wanted orbit bearing (radians) relative to her facing: the copy sits <c>ahead</c>
        /// units out along it and faces along it. 0 = straight in front of her. The orbit thread
        /// eases the live bearing (<see cref="Orbit"/>) toward it at frame rate.</summary>
        internal static float OrbitTarget { get => _orbitTarget; set => _orbitTarget = value; }
        internal static float Orbit => _orbit;

        private static float Wrap(float a)
        {
            const float twoPi = 2f * (float)Math.PI;
            while (a >  (float)Math.PI) a -= twoPi;
            while (a <= -(float)Math.PI) a += twoPi;
            return a;
        }

        /// <summary>Frame-rate orbit: ease the live bearing toward the target and re-place the copy
        /// (Draw re-seeds the root from these slot fields every frame, so per-frame writes are smooth).</summary>
        private static void OrbitLoop()
        {
            while (true)
            {
                try
                {
                    if (!Active) { Thread.Sleep(100); continue; }
                    float d = Wrap(_orbitTarget - _orbit);
                    if (Math.Abs(d) > 0.003f)
                    {
                        float step = d * OrbitEase;
                        if (step > OrbitRate) step = OrbitRate; else if (step < -OrbitRate) step = -OrbitRate;
                        if (Math.Abs(step) < 0.01f) step = Math.Sign(d) * Math.Min(0.01f, Math.Abs(d));
                        _orbit = Wrap(_orbit + step);
                        long s = SlotAddr();
                        Memory.WriteFloat(s + CCharacter.CharPos,     (float)Math.Sin(_orbit) * _ahead);
                        Memory.WriteFloat(s + CCharacter.CharPos + 8, (float)Math.Cos(_orbit) * _ahead);
                        Memory.WriteFloat(s + CCharacter.CharRotY,    _orbit);
                    }
                }
                catch (Exception e) { Console.WriteLine(Tag + "orbit tick failed: " + e.Message); }
                Thread.Sleep(OrbitTickMs);
            }
        }

        /// <summary>GUEST address of the copy's pouch-node world translation row (x, h, y, w) — a live vec4 the
        /// engine rewrites every draw; 0 when down. What the shot-vs-player pointer aims at.</summary>
        internal static uint PouchWorldGuest => Active && Memory.IsValidGuest(_pouchGuest) ? _pouchGuest + (uint)(CFrameVu1.WorldMatrix + 0x30) : 0u;

        /// <summary>GUEST address of the shot-target node's world translation row: a live vec4 the engine
        /// refreshes every draw = pouch − (0, PlayerBodyLift, 0). 0 when down.</summary>
        internal static uint ShotTargetGuest => Active && Memory.IsValidGuest(_shotNodeGuest) ? _shotNodeGuest + (uint)(CFrameVu1.WorldMatrix + 0x30) : 0u;

        /// <summary>World position of a copied node's cached world matrix (false until drawn / unset).</summary>
        private static bool NodeWorld(uint node, out float x, out float h, out float y)
        {
            x = h = y = 0f;
            if (!Active || !Memory.IsValidGuest(node)) return false;
            long n = Memory.ToMmu(node);
            x = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x30);
            h = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x34);
            y = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x38);
            return !(float.IsNaN(x) || float.IsNaN(h) || float.IsNaN(y)) && !(x == 0f && h == 0f && y == 0f);
        }

        /// <summary>The copy's eff30 in world space — the fork's muzzle, where her own pellets leave from.</summary>
        internal static bool MuzzleWorld(out float x, out float h, out float y) => NodeWorld(_muzzleGuest, out x, out h, out y);

        /// <summary>Her LIVE slingshot's eff30 / null24 world heights (same node layout as the copy, so the
        /// same offsets from the root), for comparing the copy's placement against the real weapon.</summary>
        internal static bool LiveHeights(out float muzzleH, out float pouchH)
        {
            muzzleH = pouchH = 0f;
            if (!Active || !Memory.IsValidGuest(_liveRoot) || !Memory.IsValidGuest(_muzzleGuest) || !Memory.IsValidGuest(_pouchGuest)) return false;
            uint lm = _liveRoot + (_muzzleGuest - _rootGuest), lp = _liveRoot + (_pouchGuest - _rootGuest);
            muzzleH = Memory.ReadFloat(Memory.ToMmu(lm) + CFrameVu1.WorldMatrix + 0x34);
            pouchH  = Memory.ReadFloat(Memory.ToMmu(lp) + CFrameVu1.WorldMatrix + 0x34);
            return true;
        }

        /// <summary>The copy's ROOT (grip) in world space from its cached world matrix; false until drawn.</summary>
        internal static bool RootWorld(out float x, out float h, out float y)
        {
            x = h = y = 0f;
            if (!Active || !Memory.IsValidGuest(_rootGuest)) return false;
            long r = Memory.ToMmu(_rootGuest);
            x = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x30);
            h = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x34);
            y = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x38);
            return !(float.IsNaN(x) || float.IsNaN(h) || float.IsNaN(y)) && !(x == 0f && h == 0f && y == 0f);
        }

        /// <summary>The copy's pouch bone in world space, from its cached world matrix (valid once
        /// the copy has been drawn). False while unset or when the cache looks stale.</summary>
        internal static bool PouchWorld(out float x, out float h, out float y)
        {
            x = h = y = 0f;
            if (!Active || !Memory.IsValidGuest(_pouchGuest)) return false;
            long n = Memory.ToMmu(_pouchGuest), r = Memory.ToMmu(_rootGuest);
            x = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x30);
            h = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x34);
            y = Memory.ReadFloat(n + CFrameVu1.WorldMatrix + 0x38);
            float rx = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x30);
            float rh = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x34);
            float ry = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x38);
            if (float.IsNaN(x) || float.IsNaN(h) || float.IsNaN(y) || (x == 0f && h == 0f && y == 0f)) return false;
            float d = (x - rx) * (x - rx) + (h - rh) * (h - rh) + (y - ry) * (y - ry);
            return d < 60f * 60f;                              // pouch sits within the copy, else stale
        }

        /// <summary>Play one of the weapon's own keys on the COPY (restart flag set).</summary>
        internal static void SetMotion(int key)
        {
            if (!Active) return;
            _key = key;
            long s = SlotAddr();
            Memory.WriteInt  (s + CCharacter.MotionId, key);
            Memory.WriteInt  (s + CCharacter.MotionFlags, Memory.ReadInt(s + CCharacter.MotionFlags) | CCharacter.MotionRestart);
            Memory.WriteFloat(s + CharacterMotion.MotionSpeedOffset, key == KeyShoot ? ShootRate : CharacterMotion.MotionSpeedUseKey);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"copy key {key}");
        }

        internal static void Maintain(float alpha01)
        {
            if (!Active) return;
            uint wpnObj = (uint)Memory.ReadInt(EquippedWeapon.WeaponObjGlobal) & Memory.PhysAddrMask;
            if (((uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask) != _playerRoot
                || !Memory.IsValidGuest(wpnObj)
                || ((uint)Memory.ReadInt(Memory.ToMmu(wpnObj) + 0xBC) & Memory.PhysAddrMask) != _liveRoot)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "model/weapon changed — despawning");
                Despawn();
                return;
            }
            long s = SlotAddr();
            // Placement in HER space (root parented to her model root): `ahead` units out along the
            // orbit bearing and facing along it. +Z = her forward, yaw about +Y — the same R_y that
            // maps her forward to (sin yaw, cos yaw) in the world, so model (sin o, ·, cos o) lands
            // on world bearing yaw+o. The grip orientation is baked below the root.
            Memory.WriteFloat(s + CCharacter.CharPos,     (float)Math.Sin(_orbit) * _ahead);
            Memory.WriteFloat(s + CCharacter.CharPos + 4, _up);
            Memory.WriteFloat(s + CCharacter.CharPos + 8, (float)Math.Cos(_orbit) * _ahead);
            Memory.WriteFloat(s + CCharacter.CharRot,     0f);
            Memory.WriteFloat(s + CCharacter.CharRotY,    _orbit);
            Memory.WriteFloat(s + CCharacter.CharRot + 8, 0f);
            Memory.WriteFloat(s + ObjScale,     _scale);
            Memory.WriteFloat(s + ObjScale + 4, _scale);
            Memory.WriteFloat(s + ObjScale + 8, _scale);
            Memory.WriteUInt (s + CCharacter.CharModel, _rootGuest);
            Memory.WriteFloat(s + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, alpha01)));
            Memory.WriteInt  (s + CCharacter.MotionId, _key);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 1);      // stepped → its own animation plays
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 0);
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 1);            // scene + chara step unlocked
            long r = Memory.ToMmu(_rootGuest);
            if (((uint)Memory.ReadInt(r + CFrameVu1.Parent) & Memory.PhysAddrMask) != _playerRoot)
                Memory.WriteUInt(r + CFrameVu1.Parent, _playerRoot);

        }

        internal static void Despawn()
        {
            if (!Active) return;
            long s = SlotAddr();
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 0);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 0);
            Memory.WriteFloat(s + CCharacter.NpcOpacity, 0f);
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 2);            // restore vanilla gates
            Active = false;
            _orbit = 0f;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "weapon copy down");
        }

        private static long SlotAddr() => DungeonCharaDraw.CharaArray + (long)Slot * DungeonCharaDraw.CharaStride;

        // ───────────────────────────── build ─────────────────────────────

        private static bool CopyTree()
        {
            uint wpnObj = (uint)Memory.ReadInt(EquippedWeapon.WeaponObjGlobal) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(wpnObj)) { Console.WriteLine(Tag + "no weapon object"); return false; }
            _liveRoot = (uint)Memory.ReadInt(Memory.ToMmu(wpnObj) + 0xBC) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(_liveRoot)) { Console.WriteLine(Tag + "no weapon model"); return false; }
            _playerRoot = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask;

            uint min = _liveRoot, max = _liveRoot;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(_liveRoot);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (!Memory.IsValidGuest(n) || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > MaxNodes) { Console.WriteLine(Tag + "weapon tree too large"); return false; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + CFrameVu1.RootChild) & Memory.PhysAddrMask;
                     Memory.IsValidGuest(c);
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + CFrameVu1.RootSibling) & Memory.PhysAddrMask)
                    work.Push(c);
            }
            if ((max - min) % CFrameVu1.NodeStride != 0) { Console.WriteLine(Tag + "span not node-aligned"); return false; }
            _nodeCount = (int)((max - min) / CFrameVu1.NodeStride) + 1;
            int blockSize = _nodeCount * CFrameVu1.NodeStride;
            if (blockSize > CodeCaves.WeaponCaveSize) return false;
            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;
            uint rootOff = _liveRoot - min, caveG = (uint)CodeCaves.WeaponCaveGuest;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                Rebase(block, o + CFrameVu1.Parent,      min, max, caveG, isRoot);
                Rebase(block, o + CFrameVu1.RootChild,   min, max, caveG, false);
                Rebase(block, o + CFrameVu1.RootSibling, min, max, caveG, isRoot);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
                Array.Clear(block, o + CFrameVu1.WorldMatrix, 0x40);
            }
            BitConverter.GetBytes(_playerRoot).CopyTo(block, (int)rootOff + CFrameVu1.Parent);   // weld (ref flag kept as the live root's)

            // ORIENTATION BAKE on the root's direct children: M = grip(3x3, scale-free) · pitch^q · yaw^t
            // — the pose the welded live weapon had at preset 5 — applied as child.local' = child.local · M
            // (translation row included, it lives in root space). The root itself is left for the draw.
            float[] g = new float[9];
            for (int r0 = 0; r0 < 3; r0++)
                for (int c0 = 0; c0 < 3; c0++)
                    g[r0 * 3 + c0] = BitConverter.ToSingle(block, (int)rootOff + CFrameVu1.LocalMatrix + r0 * 0x10 + c0 * 4);
            float gl = (float)Math.Sqrt(g[0] * g[0] + g[1] * g[1] + g[2] * g[2]);
            if (gl > 1e-6f) for (int k = 0; k < 9; k++) g[k] /= gl;
            for (int r0 = 0; r0 < 3; r0++)
            {
                float p0 = g[r0 * 3], p1 = g[r0 * 3 + 1], p2 = g[r0 * 3 + 2];
                for (int qp = OrientPreset & 3; qp > 0; qp--) { float m1 = p2, m2 = -p1; p1 = m1; p2 = m2; }
                for (int qt = (OrientPreset >> 2) & 3; qt > 0; qt--) { float n0 = p2, n2 = -p0; p0 = n0; p2 = n2; }
                g[r0 * 3] = p0; g[r0 * 3 + 1] = p1; g[r0 * 3 + 2] = p2;
            }
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                if ((uint)o == rootOff) continue;
                if (((uint)BitConverter.ToInt32(block, o + CFrameVu1.Parent) & Memory.PhysAddrMask) != caveG + rootOff) continue;
                for (int row = 0; row < 4; row++)               // rows 0-2 = 3x3, row 3 = translation
                {
                    int b = o + CFrameVu1.LocalMatrix + row * 0x10;
                    float a0 = BitConverter.ToSingle(block, b), a1 = BitConverter.ToSingle(block, b + 4), a2 = BitConverter.ToSingle(block, b + 8);
                    BitConverter.GetBytes(a0 * g[0] + a1 * g[3] + a2 * g[6]).CopyTo(block, b);
                    BitConverter.GetBytes(a0 * g[1] + a1 * g[4] + a2 * g[7]).CopyTo(block, b + 4);
                    BitConverter.GetBytes(a0 * g[2] + a1 * g[5] + a2 * g[8]).CopyTo(block, b + 8);
                }
            }
            Memory.WriteBytesBatch(CodeCaves.WeaponCave, block);
            _rootGuest = caveG + rootOff;
            // The pouch bone by NAME (every c04w## .mds orders pati2, chn30, jnt30_1, eff30, null24, mesh).
            _pouchGuest = _rootGuest + 4 * (uint)CFrameVu1.NodeStride;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                int e = o + CFrameVu1.Name, len = 0;
                while (len < 0x20 && block[e + len] != 0) len++;
                string nm = System.Text.Encoding.ASCII.GetString(block, e, len);
                if (nm == "null24") _pouchGuest = caveG + (uint)o;
                if (nm == "eff30")  _muzzleGuest = caveG + (uint)o;
            }
            // SHOT-TARGET NODE: one extra child of the root with no geometry, never animated (no track
            // names it), whose world position the engine recomputes every draw like any bone. Its local
            // offset is set by PlaceShotNode once the idle pouch pose is known.
            if (_nodeCount < MaxNodes)
            {
                int extra = _nodeCount * CFrameVu1.NodeStride;
                byte[] grown = new byte[extra + CFrameVu1.NodeStride];
                Array.Copy(block, grown, extra);
                Array.Copy(block, (int)(_pouchGuest - caveG), grown, extra, CFrameVu1.NodeStride);   // template = the pouch bone
                block = grown;
                uint me = caveG + (uint)extra;
                Array.Clear(block, extra + CFrameVu1.Name, 0x20);
                System.Text.Encoding.ASCII.GetBytes("shotpt").CopyTo(block, extra + CFrameVu1.Name);
                BitConverter.GetBytes(_rootGuest).CopyTo(block, extra + CFrameVu1.Parent);
                BitConverter.GetBytes(0).CopyTo(block, extra + CFrameVu1.RootChild);
                BitConverter.GetBytes(BitConverter.ToUInt32(block, (int)rootOff + CFrameVu1.RootChild)).CopyTo(block, extra + CFrameVu1.RootSibling);
                BitConverter.GetBytes(me).CopyTo(block, (int)rootOff + CFrameVu1.RootChild);          // first child of the root
                BitConverter.GetBytes(0).CopyTo(block, extra + CFrameVu1.GeomPtr);
                BitConverter.GetBytes(0).CopyTo(block, extra + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, extra + CFrameVu1.WorldCacheB);
                BitConverter.GetBytes(0).CopyTo(block, extra + CFrameVu1.DirtyTrs);
                for (int r0 = 0; r0 < 4; r0++)                                                     // local = identity
                    for (int c0 = 0; c0 < 4; c0++)
                        BitConverter.GetBytes(r0 == c0 ? 1f : 0f).CopyTo(block, extra + CFrameVu1.LocalMatrix + r0 * 0x10 + c0 * 4);
                _shotNodeGuest = me;
                _nodeCount++;
            }
            else _shotNodeGuest = 0;
            Memory.WriteBytesBatch(CodeCaves.WeaponCave, block);      // re-write: the grown block + the root's new first child
            Console.WriteLine(Tag + $"weapon tree copied ({_nodeCount} nodes) → 0x{_rootGuest:X}, pouch 0x{_pouchGuest:X}, shot node 0x{_shotNodeGuest:X}, parent = player root 0x{_playerRoot:X}");
            return true;
        }

        /// <summary>CopyMeshNodes' recipe for the weapon's software-skinned visual: visual + VU
        /// packet + MDT into the MeshCave, internal refs re-based, single-buffered.</summary>
        private static bool CopyMesh()
        {
            long cave = CodeCaves.MeshCave; long caveGuest = CodeCaves.MeshCave - 0x20000000;
            long caveEnd = TrackCave;                       // top TrackCaveSize bytes hold the cloned track list
            int copied = 0;
            for (int i = 0; i < _nodeCount; i++)
            {
                long node = CodeCaves.WeaponCave + (long)i * CFrameVu1.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisMDT) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(mdt) || (uint)Memory.ReadInt(Memory.ToMmu(mdt)) != CVisualMDT.MdtMagic) continue;
                uint vu   = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU) & Memory.PhysAddrMask;
                int vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU + 4) * 16;
                int mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + CVisualMDT.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;
                int visSz = CVisualMDT.VisualSize;
                int need = A16(visSz) + A16(vuSz) + A16(mdtSz);
                if (cave + need > caveEnd) { Console.WriteLine(Tag + "mesh does not fit MeshCave"); return false; }
                long cVis = cave;                  uint cVisG = (uint)caveGuest;
                long cVU  = cave + A16(visSz);     uint cVUG  = (uint)(caveGuest + A16(visSz));
                long cMDT = cVU + A16(vuSz);       uint cMDTG = (uint)(caveGuest + A16(visSz) + A16(vuSz));
                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu),  vuSz);
                byte[] mdtB = Memory.ReadBytesBatch(Memory.ToMmu(mdt), mdtSz);
                if (visB == null || vuB == null || mdtB == null) continue;
                RebaseRange(visB, vu, vuSz, cVUG); RebaseRange(visB, mdt, mdtSz, cMDTG);
                foreach (byte[] b in new[] { vuB, mdtB }) { RebaseRange(b, vu, vuSz, cVUG); RebaseRange(b, mdt, mdtSz, cMDTG); }
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);
                Memory.WriteBytesBatch(cVU, vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);
                cave += need; caveGuest += need; copied++;
                Console.WriteLine(Tag + $"mesh n{i}: vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} copied");
            }
            if (copied == 0) { Console.WriteLine(Tag + "no software-skinned mesh found on the weapon"); return false; }
            return true;
        }

        // ── Pull length: the copy's OWN keyframes ──────────────────────────────────────────────
        // MOTION_TYPE +0x04 = head of the .mot track list built by CreateAnimeDataEX (0x149090):
        // Mot_List nodes {+0 node idx, +4 w1, +8 type (0 rot quat / 1 scale / 2 translate),
        // +0xC key count, +0x10 → keys (0x20 each: u32 frame @0, f32x4 value @0x10), +0x14 next}.
        // Every slingshot .mot is ONE translate track on the pouch bone `null24` (46 keys). The
        // list is SHARED with the live weapon, so to shorten only the copy's draw we clone the
        // nodes + that track into the top of the MeshCave and scale the draw-window deltas about
        // the guard rest (frame 240 == the guard-loop pose, cfg KEY 11 240-251 / 13 251-255,
        // settle to 261). Scaling about the rest keeps the direction and the release overshoot.
        private const int  MotListHead   = 0x04;
        private const int  TrackNodeSize = 0x18;
        private const int  TrackKeySize  = 0x20;
        private const int  MaxTracks     = 8;
        private const uint PullWinStart  = 240, PullWinEnd = 261;   // inclusive, .mot frame numbers
        private const uint DrawEnd       = 251;                     // travel measured over the draw
        private const uint DrawnFrame    = 250;                     // full draw (KEY 12 holds here)
        private const int  TrackCaveSize = 0x1000;
        private static long TrackCave      => CodeCaves.MeshCave + CodeCaves.MeshCaveSize - TrackCaveSize;
        private static uint TrackCaveGuest => (uint)(CodeCaves.MeshCaveGuest + CodeCaves.MeshCaveSize - TrackCaveSize);
        private static long KeyTableCave      => TrackCave + 0x800;          // top half of the TrackCave (tracks use < 0x800)
        private static uint KeyTableCaveGuest => TrackCaveGuest + 0x800;
        /// <summary>20 × vec4 (x, height, y, w) — the shield ring's per-enemy "where the player is" positions,
        /// referenced by the AI redirect pointer table (16-byte aligned: sceVu0CopyVector copies a quadword).</summary>
        internal static long RingTable      => TrackCave + 0xC00;
        internal static uint RingTableGuest => TrackCaveGuest + 0xC00;

        /// <summary>Clone the weapon's track list into the TrackCave with every translate track's
        /// draw window rescaled to <see cref="_pull"/> units of travel. Returns the new head (guest) or 0.</summary>
        private static uint CloneTracks(uint head)
        {
            var nodes = new System.Collections.Generic.List<byte[]>();
            for (uint p = head; Memory.IsValidGuest(p) && nodes.Count < MaxTracks;)
            {
                byte[] n = Memory.ReadBytesBatch(Memory.ToMmu(p), TrackNodeSize);
                if (n == null) break;
                nodes.Add(n);
                p = (uint)BitConverter.ToInt32(n, 0x14) & Memory.PhysAddrMask;
            }
            if (nodes.Count == 0) return 0;
            long cave = TrackCave; uint caveG = TrackCaveGuest; long caveEnd = KeyTableCave;
            long keyCave = cave + A16(nodes.Count * TrackNodeSize);
            uint keyCaveG = caveG + (uint)A16(nodes.Count * TrackNodeSize);
            string info = "";
            for (int i = 0; i < nodes.Count; i++)
            {
                byte[] n = nodes[i];
                int type = BitConverter.ToInt32(n, 0x08), count = BitConverter.ToInt32(n, 0x0C);
                uint keys = (uint)BitConverter.ToInt32(n, 0x10) & Memory.PhysAddrMask;
                if (type == 2 && count > 0 && count < 512 && Memory.IsValidGuest(keys) && keyCave + count * TrackKeySize <= caveEnd)
                {
                    byte[] kb = Memory.ReadBytesBatch(Memory.ToMmu(keys), count * TrackKeySize);
                    if (kb != null)
                    {
                        float travel = ScalePull(kb, count, out float k);
                        Memory.WriteBytesBatch(keyCave, kb);
                        BitConverter.GetBytes(keyCaveG).CopyTo(n, 0x10);
                        info += $" [n{BitConverter.ToInt32(n, 0)} t2 x{count} travel {travel:F2} → k {k:F2}]";
                        int used = A16(count * TrackKeySize);
                        keyCave += used; keyCaveG += (uint)used;
                    }
                }
                BitConverter.GetBytes(i + 1 < nodes.Count ? caveG + (uint)((i + 1) * TrackNodeSize) : 0u).CopyTo(n, 0x14);
                Memory.WriteBytesBatch(cave + i * TrackNodeSize, n);
            }
            Console.WriteLine(Tag + $"tracks cloned ({nodes.Count}) → 0x{caveG:X}, pull {_pull:F2}:{info}");
            return caveG;
        }

        /// <summary>Rescale one translate track's [PullWinStart..PullWinEnd] keys about the rest
        /// pose (last key at or before PullWinStart) so the draw travels <see cref="_pull"/> units.
        /// Returns the authored travel; k = applied factor.</summary>
        private static float ScalePull(byte[] kb, int count, out float k)
        {
            float rx = 0, ry = 0, rz = 0; bool haveRest = false;
            for (int i = 0; i < count; i++)
            {
                uint f = BitConverter.ToUInt32(kb, i * TrackKeySize);
                if (f > PullWinStart) break;
                rx = BitConverter.ToSingle(kb, i * TrackKeySize + 0x10);
                ry = BitConverter.ToSingle(kb, i * TrackKeySize + 0x14);
                rz = BitConverter.ToSingle(kb, i * TrackKeySize + 0x18);
                haveRest = true;
            }
            k = 1f;
            if (!haveRest) return 0f;
            float travel = 0f;
            for (int i = 0; i < count; i++)
            {
                uint f = BitConverter.ToUInt32(kb, i * TrackKeySize);
                if (f < PullWinStart || f > DrawEnd) continue;
                float dx = BitConverter.ToSingle(kb, i * TrackKeySize + 0x10) - rx;
                float dy = BitConverter.ToSingle(kb, i * TrackKeySize + 0x14) - ry;
                float dz = BitConverter.ToSingle(kb, i * TrackKeySize + 0x18) - rz;
                travel = Math.Max(travel, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            if (travel < 0.01f) return travel;
            k = _pull / travel;
            int iIdle = -1, iDrawn = -1;
            for (int i = 0; i < count; i++)
            {
                uint f = BitConverter.ToUInt32(kb, i * TrackKeySize);
                if (f < PullWinStart || f > PullWinEnd) continue;
                int o = i * TrackKeySize + 0x10;
                BitConverter.GetBytes(rx + (BitConverter.ToSingle(kb, o)     - rx) * k).CopyTo(kb, o);
                BitConverter.GetBytes(ry + (BitConverter.ToSingle(kb, o + 4) - ry) * k).CopyTo(kb, o + 4);
                BitConverter.GetBytes(rz + (BitConverter.ToSingle(kb, o + 8) - rz) * k).CopyTo(kb, o + 8);
                if (f == IdleFrame) iIdle = i;
                if (f == DrawnFrame) iDrawn = i;
            }
            // NO LOOSE REST, EVER (user 2026-09-09): the authored draw (240→250) leaves from the slack
            // rest pose and the snap settles back into it (255→261). On the copy, the draw is re-pathed
            // to run straight from the taut catch pose (IdleFrame) to full draw as an IMPACT: cubic
            // ease-out, so the caught shot shoves the pouch back at once (~half the travel in the first
            // three frames) and it slows as the elastic loads — a smoothstep read as a 1-2 frame delay
            // after the catch (user 2026-09-09). Everything after the snap's forward peak holds the
            // catch pose. Both end poses are the authored (scaled) ones.
            if (iIdle >= 0 && iDrawn >= 0)
            {
                int oi = iIdle * TrackKeySize + 0x10, od = iDrawn * TrackKeySize + 0x10;
                float ix = BitConverter.ToSingle(kb, oi), iy = BitConverter.ToSingle(kb, oi + 4), iz = BitConverter.ToSingle(kb, oi + 8);
                float dx = BitConverter.ToSingle(kb, od), dy = BitConverter.ToSingle(kb, od + 4), dz = BitConverter.ToSingle(kb, od + 8);
                _idleX = ix; _idleY = iy; _idleZ = iz;
                for (int i = 0; i < count; i++)
                {
                    uint f = BitConverter.ToUInt32(kb, i * TrackKeySize);
                    int o = i * TrackKeySize + 0x10;
                    if (f >= PullWinStart && f < DrawnFrame)
                    {
                        float t = (f - PullWinStart) / (float)(DrawnFrame - PullWinStart);
                        float u = 1f - t;
                        float e = 1f - u * u * u;                           // cubic ease-out (impact)
                        BitConverter.GetBytes(ix + (dx - ix) * e).CopyTo(kb, o);
                        BitConverter.GetBytes(iy + (dy - iy) * e).CopyTo(kb, o + 4);
                        BitConverter.GetBytes(iz + (dz - iz) * e).CopyTo(kb, o + 8);
                    }
                    else if (f > IdleFrame && f <= PullWinEnd)
                    {
                        BitConverter.GetBytes(ix).CopyTo(kb, o);
                        BitConverter.GetBytes(iy).CopyTo(kb, o + 4);
                        BitConverter.GetBytes(iz).CopyTo(kb, o + 8);
                    }
                }
            }
            return travel;
        }

        /// <summary>Put the shot-target node where the engine's raised test point lands on the pouch:
        /// pouch position in ROOT space (chain of the copy's local matrices, row-vector convention
        /// p' = p·L, with the pouch bone at its idle translation) minus PlayerBodyLift/scale in Y (the
        /// root's rotation is yaw only, so root Y is world up; its scale multiplies child offsets).</summary>
        private static void PlaceShotNode()
        {
            if (!Memory.IsValidGuest(_shotNodeGuest) || !Memory.IsValidGuest(_pouchGuest)) return;
            float[] p = { _idleX, _idleY, _idleZ, 1f };
            uint n = _pouchGuest;
            for (int guard = 0; guard < 8 && n != _rootGuest && Memory.IsValidGuest(n); guard++)
            {
                long a = Memory.ToMmu(n);
                float[] m = new float[16];
                for (int k = 0; k < 16; k++) m[k] = Memory.ReadFloat(a + CFrameVu1.LocalMatrix + k * 4);
                if (n == _pouchGuest) { m[12] = _idleX; m[13] = _idleY; m[14] = _idleZ; p = new[] { 0f, 0f, 0f, 1f }; }
                float[] q = new float[4];
                for (int c = 0; c < 4; c++) q[c] = p[0] * m[c] + p[1] * m[4 + c] + p[2] * m[8 + c] + p[3] * m[12 + c];
                p = q;
                n = (uint)Memory.ReadInt(a + CFrameVu1.Parent) & Memory.PhysAddrMask;
            }
            float tx = p[0], ty = p[1] - PlayerBodyLift / Math.Max(0.01f, _scale), tz = p[2];
            long s = Memory.ToMmu(_shotNodeGuest);
            Memory.WriteFloat(s + CFrameVu1.LocalTransX, tx);
            Memory.WriteFloat(s + CFrameVu1.LocalTransY, ty);
            Memory.WriteFloat(s + CFrameVu1.LocalTransZ, tz);
            Memory.WriteInt(s + CFrameVu1.WorldCacheA, 0);
            Console.WriteLine(Tag + $"shot-target node placed at root-space ({tx:F2},{ty:F2},{tz:F2}) = pouch ({p[0]:F2},{p[1]:F2},{p[2]:F2}) lowered {PlayerBodyLift}/{_scale}");
        }

        /// <summary>Slot 3 from the WEAPON's own CCharacter (its draw/motion config), re-aimed at the
        /// copied tree, with its channel cloned (own FrameInf/BoneMtx) and the skin list kept.</summary>
        private static bool RegisterSlot()
        {
            uint wpnObj = (uint)Memory.ReadInt(EquippedWeapon.WeaponObjGlobal) & Memory.PhysAddrMask;
            byte[] buf = Memory.ReadBytesBatch(Memory.ToMmu(wpnObj), CharCopySize);
            if (buf == null) return false;
            BitConverter.GetBytes((uint)CodeCaves.ClothStubGuest).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(_rootGuest).CopyTo(buf, CCharacter.CharModel);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 4);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 8);
            BitConverter.GetBytes(1.0f).CopyTo(buf, CCharacter.DimFactor);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.NpcOpacity);
            BitConverter.GetBytes(KeyIdle).CopyTo(buf, CCharacter.MotionId);
            BitConverter.GetBytes(CharacterMotion.MotionSpeedUseKey).CopyTo(buf, CharacterMotion.MotionSpeedOffset);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, CCharacter.MotionFlags) | (uint)CCharacter.MotionRestart)
                .CopyTo(buf, CCharacter.MotionFlags);

            int fiSize = (_nodeCount + 1) * MotionType.FrameInfEntry;
            int bmSize = (_nodeCount + 1) * MotionType.BoneMtxEntry;
            uint fiOld = 0, fiNew = 0, bmOld = 0, bmNew = 0, trkOld = 0, trkNew = 0, ktOld = 0, ktNew = 0;
            int chans = 0;
            for (int s = 0; s < CCharacter.MotionSlots; s++)
            {
                int po = CCharacter.MotionSlotBase + s * 4;
                uint sp = (uint)BitConverter.ToInt32(buf, po) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(sp)) continue;
                byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(sp), MotionStructSize);
                if (mstr == null) continue;
                uint fi = (uint)BitConverter.ToInt32(mstr, MotionType.FrameInfPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(fi) && (fiNew == 0 || fi == fiOld))
                {
                    if (fiNew == 0)
                    {
                        byte[] fib = Memory.ReadBytesBatch(Memory.ToMmu(fi), fiSize);
                        if (fib != null) { Memory.WriteBytesBatch(CodeCaves.FrameInfCave, fib); fiOld = fi; fiNew = (uint)CodeCaves.FrameInfCaveGuest; }
                    }
                    if (fiNew != 0) BitConverter.GetBytes(fiNew).CopyTo(mstr, MotionType.FrameInfPtr);
                }
                uint bm = (uint)BitConverter.ToInt32(mstr, MotionType.BoneMtxPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(bm) && (bmNew == 0 || bm == bmOld))
                {
                    if (bmNew == 0)
                    {
                        byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                        if (bmb != null) { Memory.WriteBytesBatch(CodeCaves.BoneMtxCave, bmb); bmOld = bm; bmNew = (uint)(CodeCaves.BoneMtxCave & Memory.PhysAddrMask); }
                    }
                    if (bmNew != 0) BitConverter.GetBytes(bmNew).CopyTo(mstr, MotionType.BoneMtxPtr);
                }
                // IDLE POSE: the KEY table (CommandKEY 0x13A660 entries) hangs off the CHANNEL at +0x64 —
                // that is where Step__10CCharacter reads it (NOT CCharacter +0x344, which only mirrors it).
                // It is shared with the live weapon, so the copy gets a CLONE with dummy KEY 14 rewritten
                // as a one-frame hold at IdleFrame — the end of the snap, pouch forward and taut, "in
                // position to catch" — instead of the loose guard-loop pose (user 2026-09-09).
                uint mi = (uint)BitConverter.ToInt32(mstr, MotionType.MotionInfoPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(mi))
                {
                    if (ktNew == 0)
                    {
                        byte[] kt = Memory.ReadBytesBatch(Memory.ToMmu(mi), KeyTableSize);
                        if (kt != null)
                        {
                            BitConverter.GetBytes(IdleFrame).CopyTo(kt, KeyIdle * 0x10);
                            BitConverter.GetBytes(IdleFrame).CopyTo(kt, KeyIdle * 0x10 + 4);
                            BitConverter.GetBytes(0f).CopyTo(kt, KeyIdle * 0x10 + 8);
                            Memory.WriteBytesBatch(KeyTableCave, kt);
                            ktOld = mi; ktNew = KeyTableCaveGuest;
                        }
                    }
                    if (ktNew != 0 && mi == ktOld) BitConverter.GetBytes(ktNew).CopyTo(mstr, MotionType.MotionInfoPtr);
                }
                uint head = (uint)BitConverter.ToInt32(mstr, MotListHead) & Memory.PhysAddrMask;
                if (_pull > 0f && Memory.IsValidGuest(head))
                {
                    if (trkNew == 0) { uint nh = CloneTracks(head); if (nh != 0) { trkOld = head; trkNew = nh; } }
                    if (trkNew != 0 && head == trkOld) BitConverter.GetBytes(trkNew).CopyTo(mstr, MotListHead);
                }
                long cloneChan = CodeCaves.MotionCave + (long)s * MotionStructSize;
                Memory.WriteBytesBatch(cloneChan, mstr);
                BitConverter.GetBytes((uint)(CodeCaves.MotionCaveGuest + s * MotionStructSize)).CopyTo(buf, po);
                chans++;
            }
            if (chans == 0) { Console.WriteLine(Tag + "weapon has no motion channel?"); return false; }
            if (ktNew == 0) { Console.WriteLine(Tag + "weapon KEY table unreadable"); return false; }
            if (((uint)BitConverter.ToInt32(buf, CCharacter.MotionList) & Memory.PhysAddrMask) == ktOld)
                BitConverter.GetBytes(ktNew).CopyTo(buf, CCharacter.MotionList);      // keep the mirror consistent

            Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            long slot = SlotAddr();
            Memory.WriteBytesBatch(slot, buf);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 0);
            Console.WriteLine(Tag + $"slot {Slot}: {chans} channel(s) cloned, FrameInf 0x{fiSize:X}");
            return true;
        }

        private static void Rebase(byte[] block, int off, uint min, uint max, uint caveG, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & Memory.PhysAddrMask;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max) neu = caveG + (old - min);
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        /// <summary>Re-base every 4-byte word in <paramref name="b"/> that points into [src, src+size).</summary>
        /// <summary>Pointer re-basing for copied blocks — shared, segment-checked (Memory.RebaseRange).</summary>
        private static void RebaseRange(byte[] b, uint src, int size, uint dst) => Memory.RebaseRange(b, src, size, dst);

        private static int A16(int n) => (n + 15) & ~15;
    }
}
