using System;

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
        // The authored release is 4 frames at play-rate 0.7 — too snappy at 4x — so the copy's
        // motion-speed override slows it while that key plays (-1 = the KEY rate).
        private const float ShootRate   = 0.3f;

        // Orientation preset (L3+R3 cycles it in game): bits 0-1 = upright pitch quarter-turns,
        // bits 2-3 = facing yaw quarter-turns, composed onto the weapon's authored grip rotation
        // exactly as the earlier welded-weapon bake did (5 = tuned live). The draw re-seeds the ROOT
        // from the slot each frame, so this is baked into the root's child bone (chn30) instead —
        // never re-seeded, never motion-tracked — and the slot rotation stays zero.
        internal static int OrientPreset = 7;   // tuned live 2026-09-08

        private static uint  _rootGuest;                 // copied tree root (guest)
        private static uint  _liveRoot, _playerRoot;     // for change detection
        private static int   _nodeCount, _key = -1, _lastDiag;
        private static float _scale, _up, _behind;

        internal static bool Spawn(float scale, float up, float behind)
        {
            if (Active) return true;
            _scale = scale; _up = up; _behind = behind;
            if (!CopyTree() || !CopyMesh() || !RegisterSlot()) return false;
            _key = KeyGuardLoop;
            Active = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"weapon copy up (x{scale}, slot {Slot}); her slingshot untouched");
            return true;
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
            // Placement = constants in her space (root is parented to her model root).
            Memory.WriteFloat(s + CCharacter.CharPos,     0f);
            Memory.WriteFloat(s + CCharacter.CharPos + 4, _up);
            Memory.WriteFloat(s + CCharacter.CharPos + 8, -_behind);
            Memory.WriteFloat(s + CCharacter.CharRot,     0f);   // orientation is baked below the root
            Memory.WriteFloat(s + CCharacter.CharRotY,    0f);
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

            if (Environment.TickCount - _lastDiag > 1000)
            {
                _lastDiag = Environment.TickCount;
                Console.WriteLine(Tag + $"DIAG copy motionId={Memory.ReadInt(s + CCharacter.MotionId)} frame={Memory.ReadFloat(s + 0x2F0):F1} " +
                    $"rootW=({Memory.ReadFloat(r + 0x180):F0},{Memory.ReadFloat(r + 0x184):F0},{Memory.ReadFloat(r + 0x188):F0}) " +
                    $"xiao=({Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos):F0},{Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8):F0})");
            }
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
            Console.WriteLine(Tag + $"weapon tree copied ({_nodeCount} nodes) → 0x{_rootGuest:X}, parent = player root 0x{_playerRoot:X}");
            return true;
        }

        /// <summary>CopyMeshNodes' recipe for the weapon's software-skinned visual: visual + VU
        /// packet + MDT into the MeshCave, internal refs re-based, single-buffered.</summary>
        private static bool CopyMesh()
        {
            long cave = CodeCaves.MeshCave; long caveGuest = CodeCaves.MeshCave - 0x20000000;
            long caveEnd = CodeCaves.MeshCave + CodeCaves.MeshCaveSize;
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
            BitConverter.GetBytes(KeyGuardLoop).CopyTo(buf, CCharacter.MotionId);
            BitConverter.GetBytes(CharacterMotion.MotionSpeedUseKey).CopyTo(buf, CharacterMotion.MotionSpeedOffset);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, CCharacter.MotionFlags) | (uint)CCharacter.MotionRestart)
                .CopyTo(buf, CCharacter.MotionFlags);

            int fiSize = (_nodeCount + 1) * MotionType.FrameInfEntry;
            int bmSize = (_nodeCount + 1) * MotionType.BoneMtxEntry;
            uint fiOld = 0, fiNew = 0, bmOld = 0, bmNew = 0;
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
                long cloneChan = CodeCaves.MotionCave + (long)s * MotionStructSize;
                Memory.WriteBytesBatch(cloneChan, mstr);
                BitConverter.GetBytes((uint)(CodeCaves.MotionCaveGuest + s * MotionStructSize)).CopyTo(buf, po);
                chans++;
            }
            if (chans == 0) { Console.WriteLine(Tag + "weapon has no motion channel?"); return false; }

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
        private static void RebaseRange(byte[] b, uint src, int size, uint dst)
        {
            for (int o = 0; o + 4 <= b.Length; o += 4)
            {
                uint v = (uint)BitConverter.ToInt32(b, o) & Memory.PhysAddrMask;
                if (v >= src && v < src + (uint)size) BitConverter.GetBytes(dst + (v - src)).CopyTo(b, o);
            }
        }

        private static int A16(int n) => (n + 15) & ~15;
    }
}
