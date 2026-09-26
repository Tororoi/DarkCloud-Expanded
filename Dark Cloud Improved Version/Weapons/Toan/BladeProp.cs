using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A free-standing COPY of Toan's equipped sword, drawn wherever it is put — Big Bang's judgement blade, hung
    /// point-down over a locked-on enemy. The same engine-drawn weapon copy Xiao's Guardian Reflector and the Matador
    /// use (<see cref="SlingshotProp"/>) and Ungaga's Mirage clone wears (<see cref="CharacterClone"/>), with the parts
    /// only a slingshot needs left out:
    ///  · TREE: the weapon's CFrame tree deep-copied into the WeaponCave, links re-based, root left WORLD-rooted (no
    ///    parent), so the chara slot's own position / yaw / scale place it and nothing per frame is needed to hold it.
    ///  · MESH: the sword's visual is a rigid CVisualVu1, and it is COPIED — visual object and VU packet both — into
    ///    the prop mesh cave, exactly as the slingshot prop copies its skinned mesh. Sharing the live visual drew both
    ///    swords through ONE packet: whichever draw wrote it last placed both, and the copy flickered between the
    ///    target and Toan's hand with every gate steady. (The rigid object is 0x20 B — CVisual plus four words — and
    ///    its +0x18/+0x1C are the VU pointer and size the skinned class inherits, so the recipe is CopyMesh's minus
    ///    the MDT block.) Made while the blade is primed, the copy inherits the tinted vtable and draws tinted too.
    ///  · MOTION: none. The slot's channels are cleared and its motion id held at −1, so the character step
    ///    early-outs and the pose is the one baked below the root: the sword's own grip rotation turned to point
    ///    DOWN, at <see cref="Spawn"/>'s scale.
    ///  · SLOT 3: the clone-weapon chara slot, whose texture pass the patched group formula gives the weapon's own
    ///    atlas. Never co-wielded with the other props — they belong to other characters — but guarded anyway.
    ///  · FADE: NpcOpacity, 0..128, driven by <see cref="Alpha"/>.
    /// </summary>
    internal static class BladeProp
    {
        internal static bool Active { get; private set; }
        /// <summary>GUEST address of the copy's root CFrame (0 when down) — the glow's anchor.</summary>
        internal static uint RootGuest => Active ? _rootGuest : 0u;
        /// <summary>DIAGNOSTIC: where the engine last posed the copy's root (its world-matrix translation) and who it
        /// thinks its parent is — against where the slot says it should be.</summary>
        internal static string Where()
        {
            if (!Active) return "down";
            long r = Memory.ToMmu(_rootGuest), s = SlotAddr();
            return $"root world ({Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x30):F0},{Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x34):F0},{Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x38):F0})"
                 + $" parent 0x{Memory.ReadGuestPtr(r + CFrameVu1.Parent):X}"
                 + $" | slot pos ({Memory.ReadFloat(s + CCharacter.CharPos):F0},{Memory.ReadFloat(s + CCharacter.CharPos + 4):F0},{Memory.ReadFloat(s + CCharacter.CharPos + 8):F0})"
                 + $" model 0x{Memory.ReadGuestPtr(s + CCharacter.CharModel):X} active {Memory.ReadInt(s + DungeonCharaDraw.CharaActive)} reg {Memory.ReadInt(DungeonCharaDraw.CharaRegistry + 3 * 4)}";
        }

        private const string Tag      = "[BladeProp] ";
        internal const int   Slot     = 3;               // the clone-weapon chara slot (texgroup 0x1D)
        private const int    CharCopy = 0xD60;           // the draw-relevant part of a CCharacter (CharacterClone's safe cut)
        private const int    MaxNodes = 8;               // WeaponCave: 0x1400 / 0x270

        private static uint  _rootGuest, _liveRoot, _playerRoot;
        private static int   _nodeCount;
        private static float _scale;
        private static bool  _fromWeapon;                 // copied from the equipped weapon (else from a root handed in: the bomb model)

        /// <summary>Put the copy up at <paramref name="scale"/>, invisible until <see cref="Alpha"/> lifts it: the equipped
        /// weapon's model (<paramref name="rootGuest"/> 0) pointing straight down, or the model rooted at
        /// <paramref name="rootGuest"/> as it is authored (<paramref name="pointDown"/> false). False if the slot or cave is
        /// in use, or there is no model yet.</summary>
        internal static bool Spawn(float scale, uint rootGuest = 0, bool pointDown = true)
        {
            if (Active) return true;
            if (SlingshotProp.Active || CharacterClone.IsActive)
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "slot 3 / WeaponCave in use — no blade copy"); return false; }
            _scale = scale; _fromWeapon = rootGuest == 0;
            if (!CopyTree(rootGuest) || !CopyRigidMesh() || !RegisterSlot()) return false;
            if (pointDown) BakeDownward();
            Active = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{(_fromWeapon ? "blade" : "model")} copy up (x{scale}, slot {Slot}, root 0x{_rootGuest:X}); the original untouched");
            return true;
        }

        /// <summary>The copy's ambient add (the field a status tint uses), per channel 0–255.</summary>
        internal static void Tint(float r, float g, float b)
        {
            if (!Active) return;
            Memory.WriteVec3(SlotAddr() + CCharacter.CharaTint, r, g, b);
        }

        /// <summary>Where it stands and which way its flat faces. Idempotent — the slot's fields are what the draw
        /// reads, so writing the same place again costs nothing and a late tick changes nothing.</summary>
        internal static void Place(float x, float h, float y, float yaw)
        {
            if (!Active) return;
            long s = SlotAddr();
            Memory.WriteVec3 (s + CCharacter.CharPos, x, h, y);                  // one packet: the position lands whole
            Orient(yaw);
        }
        /// <summary>The copy's yaw and scale (world-rooted), written only when they have changed — the position is
        /// somebody else's (a fall or a follow the engine's blade cave places every frame, or <see cref="Place"/>).</summary>
        internal static void Orient(float yaw)
        {
            if (!Active || (_placedYaw == yaw && _placedScale == _scale)) return;
            long s = SlotAddr();
            Memory.WriteVec3(s + CCharacter.CharRot, 0f, yaw, 0f);
            Memory.WriteVec3(s + CCharacter.CharScale, _scale, _scale, _scale);
            _placedYaw = yaw; _placedScale = _scale;
        }
        private static float _placedYaw = float.NaN, _placedScale = float.NaN;

        /// <summary>PIN the copy to a node the ENGINE moves: its root is parented to <paramref name="parentGuest"/>,
        /// and from then on the draw chains its world matrix through the parent's every frame — welded at 60 fps
        /// with no writes at all, the way the slingshot shield rides Xiao. The slot's position becomes an offset in
        /// the parent's space: <paramref name="up"/> units straight up, divided by the parent's own scale so a
        /// grown miniboss does not lift the blade twice.</summary>
        internal static void Pin(uint parentGuest, float up)
        {
            if (!Active || !Memory.IsValidGuest(parentGuest)) return;
            long r = Memory.ToMmu(_rootGuest), par = Memory.ToMmu(parentGuest);
            float ps = Memory.ReadFloat(par + 0x214);                  // the parent's local scale Y
            if (ps < 0.05f || ps > 20f) ps = 1f;
            Memory.WriteUInt(r + CFrameVu1.Parent, parentGuest);
            Memory.WriteInt (r + CFrameVu1.WorldCacheA, 0);
            long s = SlotAddr();
            Memory.WriteVec3 (s + CCharacter.CharPos, 0f, up / ps, 0f);
            Memory.WriteFloat(s + CCharacter.CharRot,     0f);
            Memory.WriteFloat(s + CCharacter.CharRotY,    0f);       // local to the parent; Face turns it
            _localYaw = 0f;
            Memory.WriteFloat(s + CCharacter.CharRot + 8, 0f);
            Memory.WriteFloat(s + CCharacter.CharScale,     _scale / ps);
            Memory.WriteFloat(s + CCharacter.CharScale + 4, _scale / ps);
            Memory.WriteFloat(s + CCharacter.CharScale + 8, _scale / ps);
            _pinned = parentGuest;
        }

        /// <summary>Back to the world, exactly where it is: the root's posed world translation becomes the slot's
        /// position, its parent is cleared, and <see cref="Place"/> owns it again.</summary>
        internal static void Unpin(float yaw)
        {
            if (!Active || _pinned == 0) return;
            long r = Memory.ToMmu(_rootGuest);
            float x = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x30);
            float h = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x34);
            float y = Memory.ReadFloat(r + CFrameVu1.WorldMatrix + 0x38);
            Memory.WriteUInt(r + CFrameVu1.Parent, 0);
            Memory.WriteInt (r + CFrameVu1.WorldCacheA, 0);
            _pinned = 0;
            Place(x, h, y, yaw);
        }
        /// <summary>Which way the PINNED copy's flat faces, in the world: the slot's yaw is local to the parent, so the
        /// parent's own yaw comes off first. Written only when it has moved, so a still pair costs nothing.</summary>
        internal static void Face(float worldYaw, float parentYaw)
        {
            if (!Active || _pinned == 0) return;
            float local = worldYaw - parentYaw;
            while (local >  MathF.PI) local -= 2 * MathF.PI;
            while (local < -MathF.PI) local += 2 * MathF.PI;
            if (Math.Abs(local - _localYaw) < 0.005f) return;
            _localYaw = local;
            Memory.WriteFloat(SlotAddr() + CCharacter.CharRotY, local);
        }
        private static float _localYaw;
        /// <summary>The copy's root height in the world as the engine last posed it; NaN when nothing is up.</summary>
        internal static float WorldHeight() =>
            Active ? Memory.ReadFloat(Memory.ToMmu(_rootGuest) + CFrameVu1.WorldMatrix + 0x34) : float.NaN;
        internal static uint PinnedTo => Active ? _pinned : 0u;
        private static uint _pinned;

        /// <summary>Visibility 0..1 (the slot's NpcOpacity, 0..128).</summary>
        internal static void Alpha(float a)
        {
            if (!Active) return;
            Memory.WriteFloat(SlotAddr() + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, a)));
        }

        /// <summary>Keep the slot registered and drawn, and notice the weapon being swapped or the model rebuilt
        /// under us — the copied tree would then point at freed geometry, so the copy comes down.</summary>
        internal static bool Maintain()
        {
            if (!Active) return false;
            uint wpnObj = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
            bool sourceGone = _fromWeapon
                ? !Memory.IsValidGuest(wpnObj) || Memory.ReadGuestPtr(Memory.ToMmu(wpnObj) + 0xBC) != _liveRoot
                : !Memory.IsValidGuest(_liveRoot);
            if (Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel) != _playerRoot || sourceGone)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "model/weapon changed — despawning");
                Despawn();
                return false;
            }
            long s = SlotAddr();
            Memory.WriteUInt (s + CCharacter.CharModel, _rootGuest);
            Memory.WriteInt  (s + CCharacter.MotionId, -1);                      // never stepped into a pose
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            // The PNACH's chara-loop gate, shared with the slingshot prop and the cat: 1 = extra slots drawn. Mirage's
            // loop writes 2 here every tick a dungeon has no decoy, and stands down only for copies it knows about —
            // this one is on that list, because a 1 and a 2 alternating is a slot drawn every other frame.
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 1);
            return true;
        }

        internal static void Despawn()
        {
            _placedYaw = float.NaN; _placedScale = float.NaN;
            DespawnCore();
        }
        private static void DespawnCore()
        {
            if (!Active) return;
            long s = SlotAddr();
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 0);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 0);
            Memory.WriteFloat(s + CCharacter.NpcOpacity, 0f);
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 2);                 // restore vanilla gates
            Active = false; _pinned = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "blade copy down");
        }

        private static long SlotAddr() => DungeonCharaDraw.CharaArray + (long)Slot * DungeonCharaDraw.CharaStride;

        /// <summary>The weapon's CFrame tree (WeaponObjGlobal → obj +0xBC = model root), deep-copied into the
        /// WeaponCave with its child/sibling/parent links re-based, root parent cleared: world-rooted.</summary>
        private static bool CopyTree(uint rootGuest)
        {
            if (rootGuest == 0)
            {
                uint wpnObj = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
                if (!Memory.IsValidGuest(wpnObj)) { Console.WriteLine(Tag + "no weapon object"); return false; }
                _liveRoot = Memory.ReadGuestPtr(Memory.ToMmu(wpnObj) + 0xBC);
                if (!Memory.IsValidGuest(_liveRoot)) { Console.WriteLine(Tag + "no weapon model"); return false; }
            }
            else _liveRoot = rootGuest;
            _playerRoot = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);

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
                for (uint c = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild);
                     Memory.IsValidGuest(c);
                     c = Memory.ReadGuestPtr(Memory.ToMmu(c) + CFrameVu1.RootSibling))
                    work.Push(c);
            }
            if ((max - min) % CFrameVu1.NodeStride != 0) { Console.WriteLine(Tag + "span not node-aligned"); return false; }
            _nodeCount = (int)((max - min) / CFrameVu1.NodeStride) + 1;
            int blockSize = _nodeCount * CFrameVu1.NodeStride;
            if (blockSize > CodeCaves.WeaponCaveSize) { Console.WriteLine(Tag + "weapon tree exceeds the WeaponCave"); return false; }
            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            uint rootOff = _liveRoot - min, caveG = (uint)CodeCaves.WeaponCaveGuest;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                foreach (int link in new[] { CFrameVu1.RootChild, CFrameVu1.RootSibling, CFrameVu1.Parent })
                {
                    uint p = BitConverter.ToUInt32(block, o + link) & Memory.PhysAddrMask;
                    uint np = (p >= min && p <= max) ? caveG + (p - min) : 0u;     // outside the tree (the hand bone): dropped
                    BitConverter.GetBytes(np).CopyTo(block, o + link);
                }
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);  // every copied node re-poses from scratch
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
            }
            BitConverter.GetBytes(0).CopyTo(block, (int)rootOff + CFrameVu1.Parent); // WORLD-rooted
            Memory.WriteBytesBatch(CodeCaves.WeaponCave, block);
            _rootGuest = caveG + rootOff;
            Console.WriteLine(Tag + $"weapon tree copied ({_nodeCount} nodes) → 0x{_rootGuest:X}, world-rooted");
            return true;
        }

        /// <summary>Every copied node's rigid visual gets its own object and VU packet in the prop mesh cave, and the
        /// node is pointed at the copy. Refuses (no spawn) if nothing rigid was found or the cave is full — a shared
        /// packet is the flicker, so drawing shared is not a fallback worth having.</summary>
        private static bool CopyRigidMesh()
        {
            const int VisualSize = 0x20;                     // CVisual base + the four words CVisualVu1's operator= copies
            const int VuPtr = 0x18, VuSize = 0x1C;           // VU data pointer, and its size in 16-byte units
            long cave = CodeCaves.PropMeshCave, caveEnd = CodeCaves.MeshCave + CodeCaves.MeshCaveSize - 0x1000;   // below the slingshot's track cave
            int copied = 0;
            for (int i = 0; i < _nodeCount; i++)
            {
                long node = CodeCaves.WeaponCave + (long)i * CFrameVu1.NodeStride;
                uint vis = Memory.ReadGuestPtr(node + CFrameVu1.GeomPtr);
                if (!Memory.IsValidGuest(vis)) continue;
                long visM = Memory.ToMmu(vis);
                uint vt = Memory.ReadGuestPtr(visM + CVisualMDT.VisVtable);
                if (vt != CVisualMDT.RigidVtable && vt != CodeCaves.SolarBladeVtableGuest)
                { Console.WriteLine(Tag + $"node {i}: visual class 0x{vt:X} is not rigid — left shared"); continue; }
                uint vu = Memory.ReadGuestPtr(visM + VuPtr);
                int vuSz = Memory.ReadInt(visM + VuSize) * 16;
                if (!Memory.IsValidGuest(vu) || vuSz <= 0 || vuSz > 0x40000) { Console.WriteLine(Tag + $"node {i}: VU block unreadable"); continue; }
                int need = Memory.Align16(VisualSize) + Memory.Align16(vuSz);
                if (cave + need > caveEnd) { Console.WriteLine(Tag + "mesh does not fit the prop mesh cave"); return false; }
                long cVis = cave, cVU = cave + Memory.Align16(VisualSize);
                uint cVisG = (uint)(cVis - 0x20000000L), cVUG = (uint)(cVU - 0x20000000L);
                byte[] visB = Memory.ReadBytesBatch(visM, VisualSize);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu), vuSz);
                if (visB == null || vuB == null) continue;
                Memory.RebaseRange(visB, vu, vuSz, cVUG);     // the VU pointer, and anything else pointing into the block
                Memory.RebaseRange(vuB,  vu, vuSz, cVUG);     // …including the block's own self-references
                BitConverter.GetBytes(cVUG).CopyTo(visB, VuPtr);
                Memory.WriteBytesBatch(cVU, vuB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);
                cave += need; copied++;
                Console.WriteLine(Tag + $"node {i}: rigid visual 0x{VisualSize:X} + vu 0x{vuSz:X} copied → 0x{cVisG:X} (vtable 0x{vt:X})");
            }
            if (copied == 0) { Console.WriteLine(Tag + "no rigid mesh found on the weapon — no copy"); return false; }
            return true;
        }

        /// <summary>The chara slot: cloned from the live weapon object (a valid CCharacter — vtable, light block,
        /// cloth stub), re-aimed at the copied tree, no motion, no tint, invisible until faded in.</summary>
        private static bool RegisterSlot()
        {
            uint wpnObj = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
            byte[] buf = Memory.ReadBytesBatch(Memory.ToMmu(wpnObj), CharCopy);
            if (buf == null) return false;
            BitConverter.GetBytes((uint)CodeCaves.ClothStubGuest).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(_rootGuest).CopyTo(buf, CCharacter.CharModel);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 4);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 8);
            BitConverter.GetBytes(1.0f).CopyTo(buf, CCharacter.DimFactor);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.NpcOpacity);
            BitConverter.GetBytes(-1).CopyTo(buf, CCharacter.MotionId);
            for (int s = 0; s < CCharacter.MotionSlots; s++)                          // rigid: no channels
                BitConverter.GetBytes(0).CopyTo(buf, CCharacter.MotionSlotBase + s * 4);
            Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            long slot = SlotAddr();
            Memory.WriteBytesBatch(slot, buf);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            return true;
        }

        /// <summary>Turn the copy point-down. The root's local 3x3 is what orients a rigid model under the slot's
        /// yaw; it is replaced with a quarter-turn about X composed on the sword's own grip rotation, so "up the
        /// blade" in the model becomes "down" in the world. Translation stays anchored, world cache cleared.</summary>
        private static void BakeDownward()
        {
            long r = Memory.ToMmu(_rootGuest);
            float[] m = new float[9];
            for (int i = 0; i < 9; i++) m[i] = Memory.ReadFloat(r + CFrameVu1.LocalMatrix + (i / 3) * 0x10 + (i % 3) * 4);
            // R_x(+90°): y → z, z → −y — row1' = row2, row2' = −row1. (The other sign hung it point-UP: the
            // sword's authored grip already carries a half-turn, so the quarter-turn goes the other way.)
            float[] o = { m[0], m[1], m[2],  m[6], m[7], m[8],  -m[3], -m[4], -m[5] };
            for (int i = 0; i < 9; i++) Memory.WriteFloat(r + CFrameVu1.LocalMatrix + (i / 3) * 0x10 + (i % 3) * 4, o[i]);
            Memory.WriteInt(r + CFrameVu1.WorldCacheA, 0);
        }
    }
}
