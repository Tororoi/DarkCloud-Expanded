using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A SECOND, FULLY INDEPENDENT INSTANCE OF A PLAYER CHARACTER — rendered, animated, and cloth-simulated
    /// on its own, sharing nothing mutable with the original. All DATA writes; no injected code.
    ///
    /// This is a CAPABILITY, not a feature: Mirage's decoy is only its first consumer. Anything that wants a
    /// "second you" — a summon, an ally, a mirror boss, a ghost replay — wants this.
    ///
    /// Why it costs what it costs: the per-instance state is IRREDUCIBLE. A character is not a thing you can
    /// point at twice, because the engine mutates it in place while drawing:
    ///   • FRAME TREE — a bone's world matrix is built by GetLWMatrix walking its PARENT pointer, so a copied
    ///     root with shared children renders on the ORIGINAL. The whole tree must be deep-copied and relinked.
    ///   • MESHES — software-skinned CVisualMDTVu1 bones are skinned IN PLACE into their vertex array, so two
    ///     characters sharing one fight over the same verts. (Most bones skin on VU1 and are already per-instance.)
    ///   • MOTION — channels hold the frame counter and per-bone matrix buffers that MotionProc2 WRITES, so a
    ///     shared channel makes both advance the same animation at double speed.
    ///   • CLOTH — the sim reads its anchor CFrame and collision capsules off the SKELETON, so a copied cloth
    ///     must be re-anchored to the copy's bones or it drapes on the original.
    /// The clone is drawn by the vanilla DungeonCharaDraw facility (fill a slot, set active, register it) and
    /// STEPPED by the engine's own chara loop, so its animation and cloth physics run natively at 60fps.
    ///
    /// CAPACITY: the caves now fit ALL SIX characters — the mesh cave is sized to the worst case (Goro, 0x57B30)
    /// and the bone buffers to the largest skeleton (Osmond, 84). <see cref="Spawn"/> still REFUSES anything that
    /// would not fit rather than overflowing into the next cave, because an overrun does not fail — it silently
    /// corrupts a neighbour (that is how Xiao's 79 bones once wiped the grafted weapon's root CFrame).
    /// See docs/character-clone-footprints.md and CodeCaveAddresses.cs.
    /// </summary>
    internal static class CharacterClone
    {
        /// <summary>Motion id pinned onto the clone every tick (-1 = leave whatever the engine's step chose).
        /// The clone's step reads +0xC68 each frame, so pinning holds a stable pose regardless of the player.</summary>
        internal static int HoldMotion = -1;

        internal static bool IsActive => _cloneSlot >= 0;
        internal static int  Slot     => _cloneSlot;

        private static float _x, _z, _y, _yaw, _alpha;   // pose + opacity, supplied by the caller each tick

        /// <summary>Build the clone and place it. <paramref name="alpha01"/> is 0..1 opacity — pass ~0 to have it
        /// materialize in, since anything higher makes it pop at full strength for a frame before the first
        /// Maintain lands. Returns false if the character is too large for the mesh cave (see LIMIT above).</summary>
        internal static bool Spawn(float x, float z, float y, float yaw, float alpha01)
        {
            _x = x; _z = z; _y = y; _yaw = yaw; _alpha = alpha01;
            SpawnInternal();
            return IsActive;
        }

        /// <summary>Re-assert the clone every tick: the engine resets its pose/opacity/registry between our ticks.</summary>
        internal static void Maintain(float x, float z, float y, float yaw, float alpha01)
        {
            _x = x; _z = z; _y = y; _yaw = yaw; _alpha = alpha01;
            MaintainInternal();
        }

        internal static void Despawn() => DespawnInternal();

        private const int CharCopySize     = 0xD60;   // copy ONLY the draw-relevant CCharacter fields (see below)
        private const int MotionStructSize = 0xC0;    // bytes copied per motion channel
        private const int WeaponCharaSlot  = 3;       // the weapon draws in its own chara slot (own texture pass)

        // ── Feature switches ────────────────────────────────────────────────────────────────────────
        private const bool  CloneEnabled     = true;     // master switch
        private const bool  GhostSilhouette = false;     // translucent character clone (false = normal appearance, rely on the heat-haze)
        private const bool  EngineDrivenAnim = true;     // let the engine's chara-step pose the clone tree natively (no polling)
        private const bool  IndependentMesh = true;      // copy the software-skinned body meshes → fully independent clone
        private const bool  WeaponEnabled   = true;      // graft the equipped weapon onto the clone's hand bone
        private const bool  ClothEnabled    = true;      // copy the player's cloth onto the clone
        private const bool  ClothPhysics     = true;     // re-anchor cloth to the CLONE's skeleton so it really simulates
                                                         // (rather than rendering a frozen snapshot of the player's drape)
        private const float GhostOpacity = 90f;          // of 128; lower = more see-through
        private const float GhostDim     = 1.0f;         // full lighting so any resident character texture shows
        // Ambient ADD tint (+0xCE0 RGB) folded into the clone's lighting each draw; scene ambient ~128 = neutral,
        // so positive brightens and B>R,G gives the blue tint. Tune to taste.
        private const float GhostTintR = 20f;
        private const float GhostTintG = 30f;
        private const float GhostTintB = 65f;
        private static int _cloneSlot = -1;   // NPC type slot we borrowed, or -1
        private static uint _srcModelRoot;    // the model root the clone copies/renders (player or test enemy)
        private static uint _cloneRootGuest;  // guest addr of the deep-copied clone root (node pool slot 0)
        private static int  _cloneNodeCount;  // nodes in the last deep copy
        private static uint _weaponRootGuest; // copied weapon tree root (guest); 0 = no weapon grafted
        private static uint _wpnObjPtr;       // the live weapon object (iGpffff9d00) — source of the grip transform
        private static int  _meshesCopied;    // # software-skinned meshes given independent copies this spawn
        private static readonly System.Collections.Generic.List<(long player, long clone)> _chanMap = new();  // motion-channel frame-sync map

        /// <summary>The source model root for the clone: the player's, or (TEST) a live enemy's — to check
        /// whether the slot can render ANY geometry at the requested pose.</summary>
        private static uint CloneSourceModelRoot()
        {
            return (uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel);
        }

        private static void SpawnInternal()
        {
            if (!CloneEnabled) return;

            // Independent deep copy of the player's whole frame tree.
            _weaponRootGuest = 0; _wpnObjPtr = 0;
            if (!BuildCloneTree()) { _cloneSlot = -1; return; }
            // A mesh-cave overflow is FATAL: the uncopied meshes stay shared with the player and get skinned in
            // place by both, so bail out rather than render a half-clone (see CopyMeshNodes).
            if (IndependentMesh && !CopyMeshNodes()) { DespawnInternal(); _cloneSlot = -1; return; }
            if (WeaponEnabled) GraftWeapon();       // graft the equipped weapon onto the clone's hand bone

            // Host = chara slot 0, drawn by the DUNGEON scene draw (Draw__11CSeireiKing) as a single
            // Draw__12CNPCharacter with its own texture group (0x20). Fill the player's draw fields, aim the
            // model at the clone tree, freeze, set position/opacity, mark active, and register it for draw.
            _cloneSlot = 0;
            long slot = DungeonCharaDraw.CharaArray + (long)_cloneSlot * DungeonCharaDraw.CharaStride;

            byte[] buf = Memory.ReadBytesBatch(CCharacter.Base, CharCopySize);
            if (buf == null) { _cloneSlot = -1; return; }
            // Cloth: snapshot the player's cloth pieces into clone-owned copies and hang them off the CLONE's own
            // +0xC74. The clone's Draw renders them (ghost tint — NO player flash bleed). In PHYSICS mode the
            // anchor/collision are re-based to the clone skeleton (in CopyCloth) and the clone is ClothStep'd by
            // the PNACH chara-loop patch (jal ShadowStep→ClothStep while the scene-gate flag is set), so they simulate.
            uint clothListGuest = ClothEnabled ? CopyCloth() : (uint)CodeCaves.ClothStubGuest;
            BitConverter.GetBytes(clothListGuest).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(GhostSilhouette ? GhostDim : 1.0f).CopyTo(buf, CCharacter.DimFactor);
            // PIN the clone to the guard-loop motion instead of whatever the player was mid-doing at spawn. The
            // engine's chara-step (unlocked via the PNACH step-gate NOP) reads +0xC68 every frame → SetMotionEX,
            // so a stale/transition capture would stick. Force motion 9 and set the clean-restart flag (+0xC64
            // bit2) so the first step starts it at frame 0 with no blend. MaintainClone re-asserts +0xC68 = 9.
            // NOTE: leave +0xC60 (motion speed) at the copied player value — native speed. A near-zero override
            // made MotionProc loop-to-freeze covering a motion span by 0.001-steps.
            BitConverter.GetBytes(HoldMotion).CopyTo(buf, CCharacter.MotionId);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, CCharacter.MotionFlags) | (uint)CCharacter.MotionRestart)
                .CopyTo(buf, CCharacter.MotionFlags);
            // Spawn at the caller's alpha (pass ~0 to materialize in). Writing FULL opacity here regardless
            // draws the clone at full strength for the frames between SpawnClone and the first MaintainClone —
            // that's the visible "flash" before the haze ramps in.
            BitConverter.GetBytes((GhostSilhouette ? GhostOpacity : 128f) * _alpha).CopyTo(buf, CCharacter.NpcOpacity);  // +0xcec
            BitConverter.GetBytes(GhostSilhouette ? GhostTintR : 0f).CopyTo(buf, CCharacter.CharaTint);      // +0xce0 ambient ADD (brighter + blue)
            BitConverter.GetBytes(GhostSilhouette ? GhostTintG : 0f).CopyTo(buf, CCharacter.CharaTint + 4);
            BitConverter.GetBytes(GhostSilhouette ? GhostTintB : 0f).CopyTo(buf, CCharacter.CharaTint + 8);
            for (int o = CCharacter.LightFrom; o < CCharacter.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            // Keep the copied tex-anime object (+0xdc) intact — it selects the face/skin textures; zeroing it
            // left the face dark and arms mis-textured. (Was zeroed for the old shared-texture NPC host.)
            BitConverter.GetBytes(_x).CopyTo(buf, CCharacter.CharPos);       // +0x10 X
            BitConverter.GetBytes(_z).CopyTo(buf, CCharacter.CharPos + 4);   // +0x14 Z/height
            BitConverter.GetBytes(_y).CopyTo(buf, CCharacter.CharPos + 8);   // +0x18 Y
            BitConverter.GetBytes(_cloneRootGuest).CopyTo(buf, CCharacter.CharModel);

            // Give the clone INDEPENDENT motion channels so the engine-step advances its OWN frame counter
            // (not the player's shared one → the 2× fix). Copy each active channel struct into the cave and
            // repoint buf's +0xC20[i]. Internal ptrs (shared keyframe data) copy as-is.
            // When the clone skins its own meshes, it also needs its OWN FRAME_INF bone-matrix buffer (copy
            // once; shared across channels). Otherwise the clone's MotionProc2 clobbers the player's matrices.
            bool ownSkin = IndependentMesh && _meshesCopied > 0;
            uint fiOld = 0, fiNew = 0, bmOld = 0, bmNew = 0;
            int  fiSize = (_cloneNodeCount + 1) * MotionType.FrameInfEntry;
            int  bmSize = (_cloneNodeCount + 1) * MotionType.BoneMtxEntry;

            // These buffers scale with the bone count and sit immediately before their neighbours, so an
            // overrun does not fail — it silently corrupts the NEXT cave (this is exactly how Xiao's 79 bones
            // wiped the grafted weapon's root). Check before writing a single byte.
            if (fiSize > CodeCaves.FrameInfCaveSize || bmSize > CodeCaves.BoneMtxCaveSize)
            {
                Console.WriteLine($"[Clone] ABORT: {_cloneNodeCount} bones need FrameInf 0x{fiSize:X}/0x{CodeCaves.FrameInfCaveSize:X} " +
                                  $"and BoneMtx 0x{bmSize:X}/0x{CodeCaves.BoneMtxCaveSize:X} — would overrun the next cave. " +
                                  $"Max is {CodeCaves.MaxCloneNodes} bones (see CodeCaveAddresses.cs).");
                DespawnInternal(); _cloneSlot = -1; return;
            }
            _chanMap.Clear();
            if (EngineDrivenAnim)
            {
                for (int s = 0; s < CCharacter.MotionSlots; s++)
                {
                    int po = CCharacter.MotionSlotBase + s * 4;
                    uint sp = (uint)BitConverter.ToInt32(buf, po) & Memory.PhysAddrMask;
                    if (!Memory.IsValidGuest(sp)) continue;
                    byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(sp), MotionStructSize);
                    if (mstr == null) continue;

                    // Give the clone its own FRAME_INF (skinning-matrix buffer), copied once and reused.
                    if (ownSkin)
                    {
                        uint fi = (uint)BitConverter.ToInt32(mstr, MotionType.FrameInfPtr) & Memory.PhysAddrMask;
                        if (Memory.IsValidGuest(fi))
                        {
                            if (fiNew == 0 || fi == fiOld)
                            {
                                if (fiNew == 0)
                                {
                                    byte[] fib = Memory.ReadBytesBatch(Memory.ToMmu(fi), fiSize);
                                    if (fib != null) { Memory.WriteBytesBatch(CodeCaves.FrameInfCave, fib); fiOld = fi; fiNew = (uint)CodeCaves.FrameInfCaveGuest; }
                                }
                                if (fiNew != 0) BitConverter.GetBytes(fiNew).CopyTo(mstr, MotionType.FrameInfPtr);
                            }
                        }

                        // Give the clone its own per-bone ANIMATION-matrix buffer (channel+0x00), copied once.
                        uint bm = (uint)BitConverter.ToInt32(mstr, MotionType.BoneMtxPtr) & Memory.PhysAddrMask;
                        if (Memory.IsValidGuest(bm))
                        {
                            if (bmNew == 0 || bm == bmOld)
                            {
                                if (bmNew == 0)
                                {
                                    byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                                    if (bmb != null) { Memory.WriteBytesBatch(CodeCaves.BoneMtxCave, bmb); bmOld = bm; bmNew = (uint)(CodeCaves.BoneMtxCave & Memory.PhysAddrMask | 0x01000000); }
                                }
                                if (bmNew != 0) BitConverter.GetBytes(bmNew).CopyTo(mstr, MotionType.BoneMtxPtr);
                            }
                        }
                    }
                    // The SKINNING list (+0x08) drives MotionProc2, which software-skins the mesh vertex buffer.
                    // If the clone has its OWN mesh copies (IndependentMesh), let it skin them → fully independent
                    // body. Otherwise zero the list so it never touches the SHARED mesh (body mirrors the player).
                    // Only let the clone software-skin if it got its OWN mesh copies; else zero the skin list so
                    // it never touches the SHARED mesh (safe fallback = body mirrors, player uncorrupted).
                    if (!IndependentMesh || _meshesCopied == 0) BitConverter.GetBytes(0).CopyTo(mstr, MotionType.MotionSkinList);
                    long cloneChan = CodeCaves.MotionCave + (long)s * MotionStructSize;
                    Memory.WriteBytesBatch(cloneChan, mstr);
                    BitConverter.GetBytes((uint)(CodeCaves.MotionCaveGuest + s * MotionStructSize)).CopyTo(buf, po);
                    _chanMap.Add((Memory.ToMmu(sp), cloneChan));   // player channel → clone channel (for frame sync)
                }
            }

            Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            Memory.WriteBytesBatch(slot, buf);   // fill the CCharacter fields (0..0xD60)
            Memory.WriteInt(slot + DungeonCharaDraw.CharaActive, 1);    // active (Draw__12CNPCharacter gate; beyond 0xD60)
            // Engine-driven animation: enable the chara motion step (Step__12CNPCharacter → Step__10CCharacter →
            // SetMotionEX) and zero the opacity ramp so it doesn't fight our GhostOpacity.
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, EngineDrivenAnim ? 1 : 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA, 0);     // opacity-ramp amount = 0 (no fade drift)
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)_cloneSlot * 4, 1);   // register → dungeon draw draws it
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)_cloneSlot * 4, 0);   // un-skip → dungeon step loop steps it (clear the 1 a prior DespawnClone left)
            Console.WriteLine($"[Clone] clone in chara slot {_cloneSlot} (texgroup 0x{DungeonCharaDraw.CharaTexBase + _cloneSlot:X}) @({_x:0.#},{_z:0.#},{_y:0.#}) root=0x{_cloneRootGuest:X}");

            if (WeaponEnabled) RegisterWeaponChara();   // clone weapon in its own slot 3 / texgroup 0x1d pass
        }

        /// <summary>Deep-copy the source model's CFrame tree into the cave pool as a CONTIGUOUS 0x270-stride
        /// array preserving the player's memory order. This is REQUIRED for engine posing: MotionProc
        /// (0x147d20) addresses bones as <c>root + boneIndex*0x270</c> — raw array indexing, NOT pointer walks.
        /// So the copy must keep the same stride (0x270) and order; only the link pointers (parent/child/
        /// sibling) are re-based by a single offset (clonePool − playerBase). Geometry (+0x260) stays SHARED.
        /// The result both renders (draw follows the re-based child/sibling) and poses (SetMotionEX indexes the
        /// contiguous array) correctly. Returns false if the tree isn't resolvable or overflows the pool.</summary>
        /// <summary>Graft the equipped weapon onto the clone's hand. The weapon (WeaponObjGlobal → obj, +0xBC =
        /// model root) is a small CFrame tree parented to the player's hand bone but drawn SEPARATELY (not part of
        /// the player's +0xBC tree). We deep-copy that tree into WeaponCave (rebasing its internal links exactly
        /// like BuildCloneTree) and splice the copy's root into the CLONE's hand-bone child list, so the clone's
        /// own MGDraw walks into it and draws the weapon at the clone's hand — no separate draw call needed.
        /// Mesh visuals (node+0x260) are left SHARED: if the weapon is VU1 (rigid) they're per-instance already;
        /// if the weapon flickers it's software-skinned (MDT) and needs the CopyMeshNodes double-buffer copy.</summary>
        private static void GraftWeapon()
        {
            uint wpnObj = (uint)Memory.ReadInt(EquippedWeapon.WeaponObjGlobal) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(wpnObj)) { Console.WriteLine("[Clone/wpn] no weapon object"); return; }
            uint wRoot = (uint)Memory.ReadInt(Memory.ToMmu(wpnObj) + 0xBC) & Memory.PhysAddrMask;   // weapon model root
            if (!Memory.IsValidGuest(wRoot)) { Console.WriteLine("[Clone/wpn] no weapon model"); return; }

            // DFS child/sibling for the tree's contiguous 0x270-stride address span (same layout as the body).
            uint min = wRoot, max = wRoot;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(wRoot);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (!Memory.IsValidGuest(n) || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > 64) { Console.WriteLine("[Clone/wpn] weapon tree > 64 nodes — bailing"); return; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + CFrameVu1.RootChild) & Memory.PhysAddrMask;
                     Memory.IsValidGuest(c);
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + CFrameVu1.RootSibling) & Memory.PhysAddrMask)
                    work.Push(c);
            }
            if ((max - min) % CFrameVu1.NodeStride != 0)
            { Console.WriteLine($"[Clone/wpn] span 0x{max - min:X} not 0x270-aligned — bailing"); return; }
            int nodeCount = (int)((max - min) / CFrameVu1.NodeStride) + 1;
            int blockSize = nodeCount * CFrameVu1.NodeStride;
            if (blockSize > CodeCaves.WeaponCaveSize)
            { Console.WriteLine($"[Clone/wpn] weapon tree 0x{blockSize:X} > WeaponCave 0x{CodeCaves.WeaponCaveSize:X} — skipping the graft"); return; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return;

            uint rootOff = wRoot - min;
            uint wCaveG  = (uint)CodeCaves.WeaponCaveGuest;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                WpnRebase(block, o + CFrameVu1.Parent,      min, max, wCaveG, isRoot);   // root parent → set below
                WpnRebase(block, o + CFrameVu1.RootChild,   min, max, wCaveG, false);
                WpnRebase(block, o + CFrameVu1.RootSibling, min, max, wCaveG, isRoot);   // root sibling → set below
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
            }
            Memory.WriteBytesBatch(CodeCaves.WeaponCave, block);

            // Parent the copied root to the clone's hand bone (for positioning ONLY — MGDraw seeds a drawn
            // root's world matrix from its parent's cached world, exactly like the player's weapon object). We do
            // NOT insert it into the hand's child list: the weapon draws in its OWN chara slot / texgroup-0x1d
            // pass (RegisterWeaponChara), so the body's 0x11 pass never draws it with the wrong texture.
            uint wRootG = (uint)(wCaveG + rootOff);

            // Find the hand bone by ASKING, not by index. The live weapon's root is already parented to the
            // wielder's hand bone, so read that pointer and map it into the clone's tree with the same
            // same-offset mapping the cloth anchors use. A hardcoded bone INDEX cannot work: it was Ungaga's
            // (39), and every character has a different skeleton — Xiao's tree is 79 nodes to Ungaga's 67, so
            // index 39 is simply a different bone on her, and the weapon grafted somewhere that isn't a hand.
            uint pModelRoot = _srcModelRoot & Memory.PhysAddrMask;
            uint pHand  = (uint)Memory.ReadInt(Memory.ToMmu(wRoot) + CFrameVu1.Parent) & Memory.PhysAddrMask;
            uint treeLo = pModelRoot;
            uint treeHi = pModelRoot + (uint)(_cloneNodeCount * CFrameVu1.NodeStride);
            if (!Memory.IsValidGuest(pHand) || pHand < treeLo || pHand >= treeHi)
            {
                Console.WriteLine($"[Clone/wpn] weapon's parent bone 0x{pHand:X} is outside the body tree " +
                                  $"(0x{treeLo:X}..0x{treeHi:X}) — cannot map it onto the clone; skipping the graft");
                return;
            }
            uint handG   = (uint)CodeCaves.NodePoolGuest + (pHand - pModelRoot);
            int  handIdx = (int)((pHand - pModelRoot) / CFrameVu1.NodeStride);   // for logging only

            Memory.WriteUInt(Memory.ToMmu(wRootG) + CFrameVu1.Parent,      handG);
            Memory.WriteUInt(Memory.ToMmu(wRootG) + CFrameVu1.RootSibling, 0);   // standalone root
            _weaponRootGuest = wRootG;
            _wpnObjPtr = wpnObj;
            Console.WriteLine($"[Clone/wpn] weapon tree copied ({nodeCount} nodes) → root 0x{wRootG:X}, parented to clone bone {handIdx} (read from the live weapon)");
        }

        /// <summary>Register the copied weapon tree as its OWN chara slot (WeaponCharaSlot). Cloned from the body
        /// slot for a valid CCharacter (vtable/light/cloth-stub), then re-aimed: model → weapon tree (parent =
        /// clone hand), transform ← the real weapon object (pos 0,0,0 + grip rot/scale), motion channels zeroed
        /// (rigid). Drawn (registry) but NOT stepped (step-skip table + CharaMotionA=0). The patched group
        /// formula gives this slot texgroup 0x1d, so the weapon samples its own atlas — correct texture.</summary>
        private static void RegisterWeaponChara()
        {
            if (_weaponRootGuest == 0 || _wpnObjPtr == 0 || _cloneSlot < 0) return;
            long src = DungeonCharaDraw.CharaArray + (long)_cloneSlot        * DungeonCharaDraw.CharaStride;   // body slot (valid)
            long dst = DungeonCharaDraw.CharaArray + (long)WeaponCharaSlot * DungeonCharaDraw.CharaStride;
            byte[] cslot = Memory.ReadBytesBatch(src, DungeonCharaDraw.CharaStride);
            if (cslot == null) return;
            byte[] wtf = Memory.ReadBytesBatch(Memory.ToMmu(_wpnObjPtr) + CCharacter.CharPos, 0x90);   // +0x10..+0xA0 TRS
            if (wtf != null) wtf.CopyTo(cslot, CCharacter.CharPos);
            BitConverter.GetBytes(_weaponRootGuest).CopyTo(cslot, CCharacter.CharModel);               // +0xbc = weapon tree
            for (int s = 0; s < CCharacter.MotionSlots; s++)                                            // rigid: no motion
                BitConverter.GetBytes(0).CopyTo(cslot, CCharacter.MotionSlotBase + s * 4);
            Memory.WriteBytesBatch(dst, cslot);
            Memory.WriteInt  (dst + DungeonCharaDraw.CharaActive, 1);                                         // +0x146c draw gate
            Memory.WriteFloat(dst + CCharacter.NpcOpacity, (GhostSilhouette ? GhostOpacity : 128f) * _alpha);   // +0xcec — spawn transparent (see above)
            Memory.WriteInt  (dst + DungeonCharaDraw.CharaMotionA, 0);                                        // never step
            Memory.WriteInt  (dst + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable  + (long)WeaponCharaSlot * 4, 1);    // step loop skips it
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry  + (long)WeaponCharaSlot * 4, 1);    // draw loop draws it
            Console.WriteLine($"[Clone/wpn] weapon chara slot {WeaponCharaSlot} (texgroup 0x1d) model=0x{_weaponRootGuest:X}");

            // DIAG: the graft/registration are byte-identical across characters, so if a weapon renders for one
            // wielder and not another the difference has to be in the TRANSFORMS. Dump the weapon object's TRS
            // (what we copy into the slot) and the copied root's LOCAL scale/translation (what positions it off
            // the hand bone). A zero/degenerate scale collapses the model to a point = invisible, not misplaced.
            long wo = Memory.ToMmu(_wpnObjPtr), wr = Memory.ToMmu(_weaponRootGuest);
            Console.WriteLine(
                $"[Clone/wpn] DIAG obj pos=({Memory.ReadFloat(wo + CCharacter.CharPos):0.##}," +
                $"{Memory.ReadFloat(wo + CCharacter.CharPos + 4):0.##},{Memory.ReadFloat(wo + CCharacter.CharPos + 8):0.##}) " +
                $"objScale=({Memory.ReadFloat(wo + 0x210):0.###},{Memory.ReadFloat(wo + 0x214):0.###},{Memory.ReadFloat(wo + 0x218):0.###}) | " +
                $"rootLocalScale=({Memory.ReadFloat(wr + 0x210):0.###},{Memory.ReadFloat(wr + 0x214):0.###},{Memory.ReadFloat(wr + 0x218):0.###}) " +
                $"rootLocalTrans=({Memory.ReadFloat(wr + 0x220):0.##},{Memory.ReadFloat(wr + 0x224):0.##},{Memory.ReadFloat(wr + 0x228):0.##}) " +
                $"rootParent=0x{(uint)Memory.ReadInt(wr + CFrameVu1.Parent):X} opacity={Memory.ReadFloat(dst + CCharacter.NpcOpacity):0.#}");
        }

        /// <summary>Re-base a weapon-tree link into WeaponCave (mirror of Rebase but for the weapon cave); zero
        /// external refs and, when forceZero, the root's parent/sibling (set explicitly by the graft splice).</summary>
        private static void WpnRebase(byte[] block, int off, uint min, uint max, uint caveGuest, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & Memory.PhysAddrMask;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max) neu = (uint)(caveGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        private static bool BuildCloneTree()
        {
            _srcModelRoot = CloneSourceModelRoot();
            uint rootPhys = _srcModelRoot & Memory.PhysAddrMask;
            if (_srcModelRoot == 0 || !Memory.IsValidGuest(rootPhys)) return false;

            // DFS over child/sibling to find the tree's address SPAN (the bones form a contiguous 0x270 array
            // rooted at the model root, so min == rootPhys and the block is [min, max+0x270)).
            uint min = rootPhys, max = rootPhys;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(rootPhys);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (!Memory.IsValidGuest(n) || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > CodeCaves.MaxNodes)
                { Console.WriteLine($"[Clone] clone tree > {CodeCaves.MaxNodes} nodes — aborting"); return false; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + CFrameVu1.RootChild) & Memory.PhysAddrMask;
                     Memory.IsValidGuest(c);
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + CFrameVu1.RootSibling) & Memory.PhysAddrMask)
                    work.Push(c);
            }

            int nodeCount = (int)((max - min) / CFrameVu1.NodeStride) + 1;
            int blockSize = nodeCount * CFrameVu1.NodeStride;
            if ((max - min) % CFrameVu1.NodeStride != 0)
            { Console.WriteLine($"[Clone] tree span 0x{max - min:X} not 0x270-aligned — bailing"); return false; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            // Re-base every link pointer by (clonePool − playerBase); zero external refs and the root's
            // parent/sibling; invalidate world caches. Same-offset translation keeps array indexing valid.
            uint rootOff = rootPhys - min;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                Rebase(block, o + CFrameVu1.Parent,      min, max, isRoot);   // root's parent → 0
                Rebase(block, o + CFrameVu1.RootChild,   min, max, false);
                Rebase(block, o + CFrameVu1.RootSibling, min, max, isRoot);   // root's sibling → 0
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
            }
            Memory.WriteBytesBatch(CodeCaves.NodePool, block);

            _cloneNodeCount = nodeCount;
            _cloneRootGuest = (uint)(CodeCaves.NodePoolGuest + rootOff);   // clone root = pool + root offset
            Console.WriteLine($"[Clone] contiguous {nodeCount}-node clone tree (0x270 stride) → root 0x{_cloneRootGuest:X}");
            return true;
        }

        /// <summary>Re-base a node link pointer: if it targets a node inside the copied block [min, max+0x270),
        /// rewrite it to the clone pool at the same offset; otherwise (external ref, or forceZero for the
        /// root's parent/sibling) zero it.</summary>
        private static void Rebase(byte[] block, int off, uint min, uint max, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & Memory.PhysAddrMask;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max)
                neu = (uint)(CodeCaves.NodePoolGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        /// <summary>Give the clone its own copy of every SOFTWARE-SKINNED mesh (CVisualMDTVu1). Those are the
        /// only bones whose vertex buffer MotionProc2 deforms in place; copying them (visual + VU data + MDT)
        /// and repointing node+0x260 lets the clone skin its OWN buffers → a body independent of the player.
        /// The other bones skin on VU1 at draw time and are already per-instance. Cross-references between the
        /// three copied blocks are rebased by scanning for words in the old address ranges (safe: model data
        /// pointers are low 0x00xxxxxx addresses, distinct from vertex floats).</summary>
        /// <returns>False if the character's meshes do not FIT the cave. That must abort the whole spawn:
        /// a partial copy leaves the uncopied meshes SHARED with the player, and MotionProc2 skins those in
        /// place — so player and clone fight over the same vertices and the body renders in two places at
        /// once. Failing loudly beats a half-clone. (Goro/Ruby/Osmond don't fit today; see the class LIMIT.)</returns>
        private static bool CopyMeshNodes()
        {
            long cave = CodeCaves.MeshCave, caveGuest = CodeCaves.MeshCaveGuest;
            long caveEnd = CodeCaves.MeshCave + CodeCaves.MeshCaveSize;
            int copied = 0;
            for (int i = 0; i < _cloneNodeCount; i++)
            {
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisMDT) & Memory.PhysAddrMask;
                uint magic = (Memory.IsValidGuest(mdt)) ? (uint)Memory.ReadInt(Memory.ToMmu(mdt)) : 0xDEAD;
                if (!Memory.IsValidGuest(mdt)) continue;
                if (magic != CVisualMDT.MdtMagic) continue;   // not software-skinned

                uint vu    = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU) & Memory.PhysAddrMask;
                int  vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU + 4) * 16;   // field is in QWORDS
                int  mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + CVisualMDT.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;

                int visSz = CVisualMDT.VisualSize;
                int need  = Align16(visSz) + Align16(vuSz) + Align16(mdtSz);
                if (cave + need > caveEnd)
                {
                    Console.WriteLine($"[Clone/mesh] ABORT: character does not fit MeshCave (needed 0x{need:X} more at n{i}, " +
                                      $"cave is 0x{CodeCaves.MeshCaveSize:X}). A partial copy would leave meshes SHARED with " +
                                      "the player and render the body in two places — refusing to spawn. " +
                                      "See docs/character-clone-footprints.md (Goro/Ruby/Osmond need a bigger, relocated cave).");
                    return false;
                }

                long cVis = cave;              uint cVisG = (uint)caveGuest;
                long cVU  = cave + Align16(visSz); uint cVUG = (uint)(caveGuest + Align16(visSz));
                long cMDT = cVU + Align16(vuSz);   uint cMDTG = (uint)(caveGuest + Align16(visSz) + Align16(vuSz));

                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu),  vuSz);
                byte[] mdtB = Memory.ReadBytesBatch(Memory.ToMmu(mdt), mdtSz);
                if (visB == null || vuB == null || mdtB == null) continue;

                // The visual ALWAYS repoints to the clone copies (+0x18 → clone VU, +0x20 → clone MDT).
                RebaseRange(visB, vu, vuSz, cVUG);
                RebaseRange(visB, mdt, mdtSz, cMDTG);
                foreach (byte[] b in new[] { vuB, mdtB })
                {
                    RebaseRange(b, vu,  vuSz,  cVUG);
                    RebaseRange(b, mdt, mdtSz, cMDTG);
                }

                // Force BOTH double-buffer slots (+0x28/+0x2c) and the current ptr (+0x18) to the clone's single
                // VU copy. n66 is truly double-buffered (two distinct buffers, DBuffID-toggled); copying only one
                // left +0x2c aimed at the PLAYER's buffer, so on DBuffID=1 frames the clone wrote the player's
                // buffer → flicker. Single-buffering the clone (like n64/n65) can't corrupt the player.
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);

                Memory.WriteBytesBatch(cVU,  vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);   // node draws the clone's own mesh
                cave += need; caveGuest += need; copied++;
                Console.WriteLine($"[Clone/mesh] n{i}: vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} = 0x{need:X}");
            }
            _meshesCopied = copied;
            // Instrument actual cave usage (character-dependent — needed to right-size MeshCaveSize for BOTH
            // Ungaga and Xiao/Super Steve once we have real numbers; the reservation stays generous until then).
            long used = cave - CodeCaves.MeshCave;
            Console.WriteLine($"[Clone/mesh] {copied} mesh(es), used 0x{used:X} of 0x{CodeCaves.MeshCaveSize:X} MeshCave");
            return true;
        }

        private static int Align16(int n) => (n + 15) & ~15;

        /// <summary>Rewrite every 4-byte word in <paramref name="b"/> whose value points into [oldBase,
        /// oldBase+size) to the corresponding offset in the clone copy at newBaseGuest.</summary>
        private static void RebaseRange(byte[] b, uint oldBase, int size, uint newBaseGuest)
        {
            for (int o = 0; o + 4 <= b.Length; o += 4)
            {
                uint w = (uint)BitConverter.ToInt32(b, o);
                uint phys = w & Memory.PhysAddrMask;
                if (phys >= oldBase && phys < oldBase + (uint)size)
                    BitConverter.GetBytes(newBaseGuest + (phys - oldBase)).CopyTo(b, o);
            }
        }

        /// <summary>Snapshot the player's cloth pieces into clone-owned copies (frozen drape) and return the guest
        /// address of a cloth-ptr list to hang off the clone's +0xC74. Each CCloth is a self-contained 0x8550 object
        /// with no internal cross-refs; the only fix-ups are its draw-packet fields (+0x18 active, +0x24/+0x28
        /// double-buffer) → a single clone-owned buffer (frozen verts never change, so single-buffering is safe).
        /// Draw__10CCharacter renders the list automatically; nothing steps it in the dungeon, so it stays frozen at
        /// the world-space pose captured now (= where the caller is placing the clone). Returns the zero stub
        /// on any read failure so the clone simply gets no cloth rather than a bad pointer.</summary>
        private static uint CopyCloth()
        {
            uint listGuest = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.ClothList) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(listGuest)) return (uint)CodeCaves.ClothStubGuest;

            // Character-adaptive: walk the WHOLE +0xC74 list (up to ClothMaxPieces) rather than a hardcoded count,
            // so Xiao/Super Steve's cloth works too. Objects are PACKED DENSELY by success count (not source index)
            // so gaps in the list don't waste object slots, and both the object and buffer caves are capacity-guarded
            // (skip-and-log on overflow rather than corrupt). Full 0x8550 copy = physics-ready (Step touches +0x7550+).
            uint[] cloneList = new uint[CCloth.ClothMaxPieces];   // guest ptrs; 0 = empty (Draw skips)
            int copied = 0, bufOff = 0, anchorOff = 0, boundOff = 0;
            int bufCap = (int)CodeCaves.ClothAnchorCave - (int)CodeCaves.ClothBufCave;   // room before anchor cave
            uint modelRoot = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask;
            var boundDedupe = new System.Collections.Generic.Dictionary<uint, uint>();   // player bound-head → clone head

            for (int i = 0; i < CCloth.ClothMaxPieces; i++)
            {
                uint srcObj = (uint)Memory.ReadInt(Memory.ToMmu(listGuest) + i * 4) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(srcObj)) continue;   // empty list slot

                if (copied >= CodeCaves.ClothObjSlots)
                { Console.WriteLine($"[Clone/cloth] piece {i}: object cave full ({CodeCaves.ClothObjSlots} slots) — skip"); continue; }

                byte[] obj = Memory.ReadBytesBatch(Memory.ToMmu(srcObj), CCloth.ClothObjSize);
                if (obj == null) { Console.WriteLine($"[Clone/cloth] piece {i}: obj read (0x{CCloth.ClothObjSize:X}) failed @0x{srcObj:X} — skip"); continue; }

                int rows = BitConverter.ToInt32(obj, 0x2C), cols = BitConverter.ToInt32(obj, 0x30);
                // The two draw buffers (+0x24/+0x28) point at external packets; their gap is the allocated size.
                uint sBuf0 = (uint)BitConverter.ToInt32(obj, CCloth.ClothBuf0)     & Memory.PhysAddrMask;
                uint sBuf1 = (uint)BitConverter.ToInt32(obj, CCloth.ClothBuf0 + 4) & Memory.PhysAddrMask;
                int bufSize = (int)(sBuf1 - sBuf0);
                if (bufSize <= 0 || bufSize > 0x4000)
                { Console.WriteLine($"[Clone/cloth] piece {i}: odd buf span 0x{sBuf0:X}..0x{sBuf1:X} → fallback 0x2000"); bufSize = 0x2000; }
                bufSize = (bufSize + 0x3F) & ~0x3F;                        // qword-align
                if (bufOff + bufSize > bufCap) { Console.WriteLine($"[Clone/cloth] piece {i}: buffer cave full — skip"); continue; }

                uint cloneBufGuest = CodeCaves.ClothBufGuest + (uint)bufOff;   // single clone-owned buffer
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, CCloth.ClothActive);       // +0x18 active
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, CCloth.ClothBuf0);         // +0x24 DBuffID0
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, CCloth.ClothBuf0 + 4);     // +0x28 DBuffID1
                // Leave +0x3c (attach frame) pointing at the player's bone: it's never read while unstepped, and a
                // valid frame is safer than a null if some path ever did step it.

                long cloneObj      = CodeCaves.ClothObjCave  + (long)copied * CCloth.ClothObjSize;   // DENSE
                uint cloneObjGuest = CodeCaves.ClothObjGuest + (uint)(copied * CCloth.ClothObjSize);
                Memory.WriteBytesBatch(cloneObj, obj);
                cloneList[copied] = cloneObjGuest;
                bufOff += bufSize;
                Console.WriteLine($"[Clone/cloth] piece {i}: grid {rows}x{cols} → obj 0x{cloneObjGuest:X}, buf 0x{bufSize:X} @0x{cloneBufGuest:X}");

                // PHYSICS: re-anchor +0x3c to a clone-space frame so the stepped sim tracks the CLONE skeleton.
                if (ClothPhysics && _cloneNodeCount > 0)
                {
                    uint pAttach = (uint)BitConverter.ToInt32(obj, CCloth.ClothAttach) & Memory.PhysAddrMask;
                    uint cAttach = ResolveCloneAttach(pAttach, modelRoot, ref anchorOff);
                    if (cAttach != 0)
                    { Memory.WriteUInt(cloneObj + CCloth.ClothAttach, cAttach);
                      Console.WriteLine($"[Clone/cloth] piece {i}: attach 0x{pAttach:X} → clone 0x{cAttach:X}"); }
                    else
                        Console.WriteLine($"[Clone/cloth] piece {i}: attach resolve FAILED (0x{pAttach:X}) — cloth would follow player");

                    // Re-anchor the body-collision capsules so the cloth collides with the CLONE, not the player.
                    uint pBounds = (uint)BitConverter.ToInt32(obj, 0x44) & Memory.PhysAddrMask;   // CCloth+0x44 = CBound list
                    uint cBounds = ResolveCloneBounds(pBounds, modelRoot, ref anchorOff, ref boundOff, boundDedupe);
                    if (cBounds != 0) Memory.WriteUInt(cloneObj + 0x44, cBounds);
                }
                copied++;
            }

            if (copied == 0) return (uint)CodeCaves.ClothStubGuest;

            byte[] listBytes = new byte[CCloth.ClothMaxPieces * 4];
            for (int i = 0; i < cloneList.Length; i++) BitConverter.GetBytes(cloneList[i]).CopyTo(listBytes, i * 4);
            Memory.WriteBytesBatch(CodeCaves.ClothListCave, listBytes);
            Console.WriteLine($"[Clone/cloth] {copied} piece(s) → clone; {copied}×0x{CCloth.ClothObjSize:X} obj + 0x{bufOff:X} buf bytes");
            return CodeCaves.ClothListGuest;
        }

        /// <summary>Resolve a player cloth attach CFrame to a CLONE-space frame whose world matrix follows the
        /// CLONE skeleton. GetLWMatrix (0x1281b0) recomputes world matrices on-demand by walking +0x110 parents,
        /// so the sim just needs +0x3c to point at a frame that chains up to the clone tree. The clone tree is a
        /// contiguous same-offset copy (clone = NodePool + (player − modelRoot)); attach frames INSIDE the tree map
        /// directly, and frames allocated PAST it are copied into the anchor cave and re-parented to the clone's
        /// copy of their first in-tree ancestor (child/sibling zeroed so GetLWMatrix never walks back into player
        /// frames; world cache zeroed to force recompute). Returns 0 (caller keeps player attach) on any failure.</summary>
        private static uint ResolveCloneAttach(uint playerAttach, uint modelRoot, ref int anchorOff)
        {
            if (!Memory.IsValidGuest(playerAttach) || modelRoot == 0) return 0;
            uint treeLo = modelRoot, treeHi = modelRoot + (uint)_cloneNodeCount * 0x270;
            uint CloneOf(uint p) => (uint)CodeCaves.NodePoolGuest + (p - modelRoot);   // in-tree same-offset map

            // Walk out-of-tree ancestors (nearest-first) up to the first in-tree frame.
            var outChain = new System.Collections.Generic.List<uint>();
            uint n = playerAttach;
            while (Memory.IsValidGuest(n) && !(n >= treeLo && n < treeHi))
            {
                outChain.Add(n);
                if (outChain.Count > 32) return 0;   // runaway guard
                n = (uint)Memory.ReadInt(Memory.ToMmu(n) + CFrameVu1.Parent) & Memory.PhysAddrMask;
            }
            if (outChain.Count == 0) return CloneOf(playerAttach);   // attach itself is in-tree
            uint inTreeAncestor = n;
            if (inTreeAncestor == 0) return 0;   // chain never reached the tree — can't anchor

            // Copy each out-of-tree frame to the anchor cave; build an old→new(guest) map.
            var map = new System.Collections.Generic.Dictionary<uint, uint>();
            foreach (uint f in outChain)
            {
                if (CodeCaves.ClothAnchorCave + anchorOff + 0x270 > CodeCaves.ClothAnchorEnd)
                { Console.WriteLine("[Clone/cloth] anchor cave full"); return 0; }
                byte[] fb = Memory.ReadBytesBatch(Memory.ToMmu(f), 0x270);
                if (fb == null) return 0;
                Memory.WriteBytesBatch(CodeCaves.ClothAnchorCave + anchorOff, fb);
                map[f] = CodeCaves.ClothAnchorGuest + (uint)anchorOff;
                anchorOff += 0x270;
            }
            // Fix each copy: parent → clone-space, zero child/sibling + world cache.
            foreach (uint f in outChain)
            {
                long copyMmu = CodeCaves.ClothAnchorCave + (long)(map[f] - CodeCaves.ClothAnchorGuest);
                uint parent  = (uint)Memory.ReadInt(Memory.ToMmu(f) + CFrameVu1.Parent) & Memory.PhysAddrMask;
                uint newParent = map.TryGetValue(parent, out uint mp) ? mp : CloneOf(parent);
                Memory.WriteUInt(copyMmu + CFrameVu1.Parent,      newParent);
                Memory.WriteInt (copyMmu + CFrameVu1.RootChild,   0);
                Memory.WriteInt (copyMmu + CFrameVu1.RootSibling, 0);
                Memory.WriteInt (copyMmu + CFrameVu1.WorldCacheA, 0);
                Memory.WriteInt (copyMmu + CFrameVu1.WorldCacheB, 0);
            }
            return map[playerAttach];
        }

        /// <summary>Copy a cloth's CBound collision list (+0x44) into the clone bound cave and re-anchor each
        /// capsule's endpoint bones (+0xe4/+0xe8) to the clone skeleton, so the cloth collides against the CLONE's
        /// body (legs/hips) instead of the player's. Deduped by list head (a character's cloth pieces share one
        /// body-collision list). Returns the clone list head, or 0 (caller keeps the player list) on failure.</summary>
        private static uint ResolveCloneBounds(uint playerHead, uint modelRoot, ref int anchorOff, ref int boundOff,
                                               System.Collections.Generic.Dictionary<uint, uint> dedupe)
        {
            if (!Memory.IsValidGuest(playerHead)) return 0;
            if (dedupe.TryGetValue(playerHead, out uint cached)) return cached;

            var list = new System.Collections.Generic.List<uint>();
            for (uint b = playerHead; Memory.IsValidGuest(b) && list.Count < 48;
                 b = (uint)Memory.ReadInt(Memory.ToMmu(b) + CBound.BoundNext) & Memory.PhysAddrMask)
                list.Add(b);

            var map = new System.Collections.Generic.Dictionary<uint, uint>();
            foreach (uint pb in list)
            {
                if (CodeCaves.ClothBoundCave + boundOff + CBound.BoundSize > CodeCaves.ClothBoundEnd)
                { Console.WriteLine("[Clone/cloth] bound cave full"); break; }
                byte[] bb = Memory.ReadBytesBatch(Memory.ToMmu(pb), CBound.BoundSize);
                if (bb == null) break;
                Memory.WriteBytesBatch(CodeCaves.ClothBoundCave + boundOff, bb);
                map[pb] = CodeCaves.ClothBoundGuest + (uint)boundOff;
                boundOff += CBound.BoundSize;
            }
            if (map.Count == 0) return 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (!map.TryGetValue(list[i], out uint copy)) continue;
                long copyMmu = CodeCaves.ClothBoundCave + (long)(copy - CodeCaves.ClothBoundGuest);
                uint next = (i + 1 < list.Count && map.TryGetValue(list[i + 1], out uint nc)) ? nc : 0;
                Memory.WriteUInt(copyMmu + CBound.BoundNext, next);
                foreach (int fo in new[] { CBound.BoundFrameA, CBound.BoundFrameB })
                {
                    uint pf = (uint)Memory.ReadInt(Memory.ToMmu(list[i]) + fo) & Memory.PhysAddrMask;
                    if (pf == 0) continue;
                    uint cf = ResolveCloneAttach(pf, modelRoot, ref anchorOff);
                    if (cf != 0) Memory.WriteUInt(copyMmu + fo, cf);
                }
            }
            uint head = map[list[0]];
            dedupe[playerHead] = head;
            Console.WriteLine($"[Clone/cloth] bounds: {map.Count} CBound → clone list 0x{head:X}");
            return head;
        }

        /// <summary>Re-assert the ghost's ambient-ADD tint (+0xCE0 RGB) on a chara slot — brighter + blue.</summary>
        private static void WriteGhostTint(long charaSlot)
        {
            Memory.WriteFloat(charaSlot + CCharacter.CharaTint,     GhostSilhouette ? GhostTintR : 0f);
            Memory.WriteFloat(charaSlot + CCharacter.CharaTint + 4, GhostSilhouette ? GhostTintG : 0f);
            Memory.WriteFloat(charaSlot + CCharacter.CharaTint + 8, GhostSilhouette ? GhostTintB : 0f);
        }

        private static void MaintainInternal()
        {
            if (_cloneSlot < 0) return;
            long slot = DungeonCharaDraw.CharaArray + (long)_cloneSlot * DungeonCharaDraw.CharaStride;
            // Hold the requested pose, keep the model + active/opacity set, and keep it registered for draw
            // (the game may reset the registry / opacity-ramp between our ticks).
            float cx = _x, cz = _z, cy = _y, cyaw = _yaw;
            Memory.WriteFloat(slot + CCharacter.CharPos,     cx);
            Memory.WriteFloat(slot + CCharacter.CharPos + 4, cz);
            Memory.WriteFloat(slot + CCharacter.CharPos + 8, cy);
            Memory.WriteUInt (slot + CCharacter.CharModel,   _cloneRootGuest);
            // PIN the heading. The engine steps the clone (so its yaw can drift) and a respawned clone copies
            // the player's CURRENT facing — either way it could diverge from the yaw the haze is pushed back
            // along — a caller placing an effect relative to the clone's facing would see it drift off-target.
            Memory.WriteFloat(slot + CCharacter.CharRotY, cyaw);

            float op = (GhostSilhouette ? GhostOpacity : 128f) * _alpha;   // fade in/out envelope (haze is NOT gated by it)
            Memory.WriteInt  (slot + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteFloat(slot + CCharacter.NpcOpacity,  op);
            Memory.WriteFloat(slot + CCharacter.DimFactor,   GhostSilhouette ? GhostDim : 1.0f);   // re-assert like opacity/tint (spawn-only would drift)
            WriteGhostTint(slot);
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)_cloneSlot * 4, 1);   // draw loop draws it
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)_cloneSlot * 4, 0);   // step loop STEPS it (DespawnClone set this to 1; must clear on re-cast or physics only works once)

            // Engine-driven animation: the engine's own chara-step (unlocked via the PNACH step-gate NOP) poses
            // the clone tree at 60fps — no polling. Re-assert the step flags (game may reset between our ticks)
            // and HOLD motion 9 (guard loop) so the clone stays in a clean guard pose regardless of what the
            // player is doing — the step reads +0xC68 each frame, so pinning it here keeps the motion stable.
            if (EngineDrivenAnim)
            {
                Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, 1);
                Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA,   0);
                Memory.WriteInt(slot + CCharacter.MotionId,     HoldMotion);
            }
            // (Legacy per-tick TRS poll disabled: it fought the 60fps step and read as jitter.)

            // Re-assert the weapon chara slot (drawn, never stepped) — the game may reset registry/opacity.
            if (_weaponRootGuest != 0)
            {
                long wslot = DungeonCharaDraw.CharaArray + (long)WeaponCharaSlot * DungeonCharaDraw.CharaStride;
                Memory.WriteUInt (wslot + CCharacter.CharModel,   _weaponRootGuest);
                Memory.WriteInt  (wslot + DungeonCharaDraw.CharaActive, 1);
                Memory.WriteFloat(wslot + CCharacter.NpcOpacity,  op);   // same envelope as the body — fade together
                Memory.WriteFloat(wslot + CCharacter.DimFactor,   GhostSilhouette ? GhostDim : 1.0f);   // ...and the same ghost dim
                WriteGhostTint(wslot);
                Memory.WriteInt  (wslot + DungeonCharaDraw.CharaMotionA, 0);
                Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)WeaponCharaSlot * 4, 1);
                Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)WeaponCharaSlot * 4, 1);
            }

            // NOTE: iGpffff9e18 (0x202A3608) is the "scene active" flag — setting it draws the chara loop BUT
            // freezes enemies (motionDrive steps enemies only when it's 0). So we DON'T set it here; the clone
            // is drawn instead via an in-place patch of Draw__11CSeireiKing's chara-loop gate (SceneDrawPatch),
            // which lets the loop run while the flag stays 0 so enemies keep moving.
        }

        private static void DespawnInternal()
        {
            // (CodeCaves.MirageSceneGateFlag is driven by the Loop now: 2 in-dungeon → PNACH restores vanilla gates, 0 in town.)
            if (_cloneSlot < 0) return;
            long slot = DungeonCharaDraw.CharaArray + (long)_cloneSlot * DungeonCharaDraw.CharaStride;
            Memory.WriteInt (DungeonCharaDraw.CharaRegistry + (long)_cloneSlot * 4, 0);   // unregister (draw)
            Memory.WriteInt (DungeonCharaDraw.StepSkipTable + (long)_cloneSlot * 4, 1);   // step loop skips it (belt+braces)
            Memory.WriteInt (slot + DungeonCharaDraw.CharaActive,  0);                    // inactive
            Memory.WriteInt (slot + DungeonCharaDraw.CharaMotionA, 0);                    // don't step (leftover clone)
            Memory.WriteUInt(slot + CCharacter.CharModel, 0);                       // clear model

            if (_weaponRootGuest != 0)   // tear down the weapon chara slot too
            {
                long wslot = DungeonCharaDraw.CharaArray + (long)WeaponCharaSlot * DungeonCharaDraw.CharaStride;
                Memory.WriteInt (DungeonCharaDraw.CharaRegistry + (long)WeaponCharaSlot * 4, 0);
                Memory.WriteInt (DungeonCharaDraw.StepSkipTable + (long)WeaponCharaSlot * 4, 0);
                Memory.WriteInt (wslot + DungeonCharaDraw.CharaActive, 0);
                Memory.WriteUInt(wslot + CCharacter.CharModel, 0);
                _weaponRootGuest = 0;
            }
            _cloneSlot = -1;
        }


    }
}

