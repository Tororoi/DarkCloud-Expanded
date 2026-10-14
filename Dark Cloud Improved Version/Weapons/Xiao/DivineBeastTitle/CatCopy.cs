using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.DivineBeastTitle;
using static Dark_Cloud_Improved_Version.CatCape;
using static Dark_Cloud_Improved_Version.CatFlight;
using static Dark_Cloud_Improved_Version.CatTextures;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The cat in chara slot 1: its pack loaded by the machine into Xiao's region (ElfCave.CatLoad), the loaded tree read back,
    /// the look's meshes hidden and the slot set up to draw. One of the <see cref="DivineBeastTitle"/> classes, which share their members
    /// through using static.</summary>
    internal static class CatCopy
    {
        /// <summary>The pack the cave loads (CatPackBakes.CatPackChr, in LoadFile's spelling).</summary>
        private static readonly string PackPath = CatPackBakes.CatPackChr.Replace('\\', '/');
        private const int MaxCatNodes = 64;                    // the pack has 49: cat 37 + wings + mask + the cape's frame
        private const double LoadTimeoutSeconds = 5.0;
        private const uint ReserveHookAddr = 0x01DBA894;       // DunPatches: LoadChara2 → ElfCave.CatLoadReserve
        private static bool _noLoaderLogged;

        /// <summary>The slot's motion channel (MOTION 0 of the cat pack): its MOTION_STATE carries the live frame and blend.</summary>
        internal static long _chan;
        /// <summary>The unused tail of Xiao's region past what the load took: the skin-list clone and the mask's vtable copy go here.</summary>
        private static long _regionFree, _regionEnd;

        /// <summary>Load the cat into slot 1 and read its tree back: the cave runs SetupBaseModel's sequence on the slot in the dungeon's
        /// next frame (a disc read; the frame waits for it). Then the frames the flight and the glow need are found, the look's
        /// meshes are found for hiding, and the slot gets the cat's draw state.</summary>
        internal static bool Spawn()
        {
            if (Active) return true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            if (!RequestLoad()) return false;
            long s = SlotAddr();
            uint root = Memory.ReadGuestPtr(s + CCharacter.CharModel);
            if (!Memory.IsValidGuest(root)) { Log("the load left slot 1 without a model"); Unload(); return false; }
            _liveRoot = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            _copyRoot = root;
            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(root), MaxCatNodes * CFrameVu1.NodeStride);
            if (block == null) { Log("could not read the loaded tree"); Unload(); return false; }

            // The loaded frames are ONE contiguous 0x270 array from the root (.mds node order): walk it while the names stay
            // printable and every parent points back into the array.
            int n = 0;
            for (; n < MaxCatNodes; n++)
            {
                string nm = NameAt(block, n);
                uint par = (uint)BitConverter.ToInt32(block, n * CFrameVu1.NodeStride + CFrameVu1.Parent) & Memory.PhysAddrMask;
                uint at = root + (uint)(n * CFrameVu1.NodeStride);
                bool parOk = n == 0 || (par >= root && par < at && (par - root) % CFrameVu1.NodeStride == 0);
                if (nm.Length == 0 || !parOk) break;
                bool printable = true;
                foreach (char ch in nm) if (ch < 0x20 || ch > 0x7E) { printable = false; break; }
                if (!printable) break;
            }
            _nodeCount = n;
            if (NameAt(block, 0) != CatRootName) { Log($"slot 1's model is not the cat (root '{NameAt(block, 0)}')"); Unload(); return false; }
            Memory.WriteVec3(Memory.ToMmu(root) + CFrameVu1.LocalTransX, 0f, 0f, 0f);   // the root sits at the object origin

            FindHead(block);
            _wingMeshIdx.Clear();
            foreach (string nm in WingMeshNodes) { int wi = FindNode(block, nm); if (wi >= 0) _wingMeshIdx.Add(wi); }
            _maskMeshIdx = FindNode(block, MaskNodeName);
            uint maskVis = _maskMeshIdx >= 0 ? (uint)BitConverter.ToInt32(block, _maskMeshIdx * CFrameVu1.NodeStride + CFrameVu1.GeomPtr) & Memory.PhysAddrMask : 0;
            _maskVisual = Memory.IsValidGuest(maskVis) ? Memory.ToMmu(maskVis) : 0;
            _chan = Memory.ToMmu(Memory.ReadGuestPtr(s + CCharacter.MotionSlotBase));
            if (!SetupSlot()) { Unload(); return false; }
            Active = true;
            Log($"cat up: {_nodeCount} nodes at 0x{root:X} in slot {Slot}, channel 0x{Memory.ToGuest(_chan):X}, in {clock.ElapsedMilliseconds:N0} ms");
            return true;
        }

        /// <summary>Ask the cave for the load and wait for its answer. Refused when this ISO has no loader, when LoadChara2 made no region
        /// (Xiao is not the character the floor loaded) or too small a one, while an event's character holds slot 1, or while a texture
        /// block reaches into the cat's VRAM span.</summary>
        private static bool RequestLoad()
        {
            if ((uint)Memory.ReadInt(0x20000000L + ReserveHookAddr) != MipsAsm.Jal(ElfCave.CatLoadReserve))
            {
                if (!_noLoaderLogged) { _noLoaderLogged = true; Log("this ISO has no cat loader (LoadChara2 is not hooked) — re-patch"); }
                return false;
            }
            long req = CodeCaves.CatLoad;
            uint region = Memory.ReadUInt(req + CodeCaves.CatLoadRegion);
            int units = Memory.ReadInt(req + CodeCaves.CatLoadUnits);
            if (!Memory.IsValidGuest(region)) { Log($"no region for the cat: LoadChara2 did not make one on this floor ({units * 16:N0} B of room)"); return false; }
            if (units < XiaoRegion.CatMinUnits) { Log($"Xiao's region is {units * 16:N0} B, under the {XiaoRegion.CatMinUnits * 16:N0} B the cat may need — not loading it"); return false; }
            // Slot 1 is one of the NPC slots the floor's event scripts load characters into: while one of theirs is there, wait.
            uint held = Memory.ReadGuestPtr(SlotAddr() + CCharacter.CharModel);
            if (held != 0 && held != Memory.ReadUInt(req + CodeCaves.CatLoadRoot)) { Log($"slot 1 holds an event's character (0x{held:X}) — not loading the cat"); return false; }
            // The cat's VRAM span sits just under the texture cursor; a block whose top already reaches into it would make the game's own
            // EndEnterTextureBlock hang, so the load waits.
            uint cursor0 = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            uint heldW = Memory.ReadUInt(req + CodeCaves.CatLoadVram);
            if (heldW != 0 && cursor0 == heldW) cursor0 = Memory.ReadUInt(req + CodeCaves.CatLoadCursor);   // the release gives an earlier span back first
            uint span = (cursor0 - (uint)CatPackBakes.CatVramBlocks) & ~0x1Fu;
            byte[] blocks = Memory.ReadBytesBatch(TextureManager.Base + TextureManager.Blocks, TextureManager.BlockCount * TextureManager.BlockStride);
            if (blocks == null) return false;
            uint highest = 0;
            for (int i = 0; i < TextureManager.BlockCount; i++)
                if (i != SlotTextureGroup) highest = Math.Max(highest, BitConverter.ToUInt32(blocks, i * TextureManager.BlockStride + TextureManager.BlkTop));
            if (highest > span) { Log($"a texture block tops at 0x{highest:X}, inside the cat's VRAM span 0x{span:X}..0x{cursor0:X} — not loading it"); return false; }
            var path = new byte[0x40];
            Encoding.ASCII.GetBytes(PackPath).CopyTo(path, 0);
            Memory.WriteBytesBatch(req + CodeCaves.CatLoadPath, path);
            Memory.WriteInt(req + CodeCaves.CatLoadBlocks, CatPackBakes.CatVramBlocks);
            Memory.WriteInt(req + CodeCaves.CatLoadStatus, 1);
            var waited = System.Diagnostics.Stopwatch.StartNew();
            int status = 1;
            while (waited.Elapsed.TotalSeconds < LoadTimeoutSeconds)
            {
                status = Memory.ReadInt(req + CodeCaves.CatLoadStatus);
                if (status != 1) break;
                Thread.Sleep(2);
            }
            byte[] r = Memory.ReadBytesBatch(req, 0x30);
            if (r == null) return false;
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            if (status != 2)
            {
                if (status == 1) { Memory.WriteInt(req + CodeCaves.CatLoadStatus, 0); Log($"the cat loader did not answer in {LoadTimeoutSeconds:F0} s"); }
                else if (status == -1) Log("the cat loader refused: slot 1 taken by an event's character, or no region");
                else Log($"the cat load was dropped (status {status}) — the floor reloaded under it");
                return false;
            }
            int used = BitConverter.ToInt32(r, CodeCaves.CatLoadAlloc + 8);
            uint w = BitConverter.ToUInt32(r, CodeCaves.CatLoadVram);
            _regionFree = Memory.ToMmu(region) + (long)used * 16;
            _regionEnd = Memory.ToMmu(region) + (long)units * 16;
            Log($"cat loaded in {waited.ElapsedMilliseconds} ms: {used * 16:N0} of {units * 16:N0} B of Xiao's region at 0x{region:X}; "
                + $"VRAM 0x{w:X}..0x{w + (uint)CatPackBakes.CatVramBlocks:X} (cursor now 0x{cursor:X}, highest other block top 0x{highest:X})");
            return true;
        }

        /// <summary>Ask the cave to unload: while slot 1 still holds the cat, block 0x21 deleted and slot 1 left without a model; the VRAM
        /// cursor given back. Not waited on — the cave answers in the dungeon's next frame, and a load that comes first releases the same
        /// things.</summary>
        internal static void Unload() => Memory.WriteInt(CodeCaves.CatLoad + CodeCaves.CatLoadStatus, 3);

        /// <summary>Whether the cat the cave loaded is still there: a floor load or a party switch (the reserve entry) forgets it, and an
        /// event's character can take slot 1.</summary>
        internal static bool StillLoaded() => Memory.ReadInt(CodeCaves.CatLoad + CodeCaves.CatLoadStatus) == 2
                                           && Memory.ReadGuestPtr(SlotAddr() + CCharacter.CharModel) == _copyRoot;

        /// <summary>Space in the region's unused tail (0 when it is full).</summary>
        internal static long TakeRegion(int bytes, out uint guest)
        {
            long need = Memory.Align16(bytes);
            if (_regionFree + need > _regionEnd) { guest = 0; return 0; }
            long a = _regionFree; _regionFree += need; guest = Memory.ToGuest(a);
            return a;
        }

        private static string NameAt(byte[] block, int i)
        {
            int o = i * CFrameVu1.NodeStride + CFrameVu1.Name, len = 0;
            while (len < 0x20 && block[o + len] != 0) len++;
            return Encoding.ASCII.GetString(block, o, len);
        }

        private static int FindNode(byte[] block, string name)
        {
            for (int i = 0; i < _nodeCount; i++) if (NameAt(block, i) == name) return i;
            return -1;
        }

        /// <summary>The guest address of the loaded frame <paramref name="i"/>.</summary>
        internal static uint NodeAddr(int i) => _copyRoot + (uint)(i * CFrameVu1.NodeStride);

        /// <summary>Locate the frames the cave needs and measure the head's rest offset. Publishes <see cref="HeadNodeName"/> (the cave's
        /// contact point) and the two glow anchors to the mailbox, clears CatGlowReady so the glow rebinds, then walks the head's local
        /// matrices up to the root for its position in cat space — the offset the flight keeps on the pellet's line. Falls back to the
        /// root height if the head is missing.</summary>
        private static void FindHead(byte[] block)
        {
            _headX = 0f; _headH = HeadFallbackHeight; _headZ = 0f;
            int head = FindNode(block, HeadNodeName);
            Memory.WriteInt(CatBlock.CatHeadNode, head >= 0 ? (int)NodeAddr(head) : 0);   // the cave's contact point = this frame's posed position
            int ga = FindNode(block, GlowNodeA), gb = FindNode(block, GlowNodeB);           // the glow's anchor frames
            Memory.WriteInt(CatBlock.CatGlowNodeA, ga >= 0 ? (int)NodeAddr(ga) : 0);
            Memory.WriteInt(CatBlock.CatGlowNodeB, gb >= 0 ? (int)NodeAddr(gb) : 0);
            Memory.WriteInt(CatBlock.CatGlowReady, 0);
            if (ga < 0 || gb < 0) Log($"glow anchors: {GlowNodeA} n{ga}, {GlowNodeB} n{gb} — falling back to the root");
            if (head < 0) return;
            float[] p = { 0f, 0f, 0f, 1f };
            int n = head;
            for (int guard = 0; guard < 64 && n > 0; guard++)
            {
                int b = n * CFrameVu1.NodeStride + CFrameVu1.LocalMatrix;
                float[] q = new float[4];
                for (int c = 0; c < 4; c++)
                    q[c] = p[0] * BitConverter.ToSingle(block, b + c * 4) + p[1] * BitConverter.ToSingle(block, b + 0x10 + c * 4)
                         + p[2] * BitConverter.ToSingle(block, b + 0x20 + c * 4) + p[3] * BitConverter.ToSingle(block, b + 0x30 + c * 4);
                p = q;
                uint par = (uint)BitConverter.ToInt32(block, n * CFrameVu1.NodeStride + CFrameVu1.Parent) & Memory.PhysAddrMask;
                n = par >= _copyRoot ? (int)((par - _copyRoot) / CFrameVu1.NodeStride) : 0;
            }
            _headX = p[0]; _headH = p[1]; _headZ = p[2];
            Log($"head ({HeadNodeName}, n{head}) rests at ({_headX:F2},{_headH:F2},{_headZ:F2}) in cat space");
        }

        /// <summary>A frame's name (0x20 bytes, NUL-terminated); empty when the read fails.</summary>
        internal static string ReadName(uint node)
        {
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(node) + CFrameVu1.Name, 0x20);
            if (b == null) return "";
            int len = 0; while (len < b.Length && b[len] != 0) len++;
            return Encoding.ASCII.GetString(b, 0, len);
        }

        /// <summary>Give the mask the CAPE's colour rather than the cat's. A cloth is easy — Draw__10CCharacter walks its cloth list calling
        /// Draw__6CCloth, so ElfCave.CatCapeTint wraps that one call — but a mesh has no such seam: the ambient is set once, MGDraw runs
        /// over the whole frame tree, and a mesh's tint is ADDED to its lit colour rather than multiplied through its texture, so the cat's
        /// blue lands on the mask and turns red to pink. Meshes draw through C++ virtual calls and the mask's CVisualMDT belongs to this
        /// load alone, so this copies __vt__13CVisualMDTVu1 into the region's tail, points its two DrawVu1 slots at ElfCave.CatMaskTint,
        /// and writes that copy into the mask's visual — no engine code touched. The cave adds CatBlock.CatCapeTint, the same delta the
        /// cape uses, so the two match by construction. ⚠ The vptr is at +0x08, not offset 0; refuses unless the visual really holds the
        /// stock vtable, so a layout surprise is a no-op.</summary>
        internal static void MaskTint()
        {
            if (_maskVisual == 0) { Log("mask tint: the cat has no mask visual — it keeps the cat's colour"); return; }
            uint vt = Memory.ReadGuestPtr(_maskVisual + CVisualMDT.VisVtable);
            if (vt != CVisualMDT.Vu1Vtable)
            { Log($"mask tint: the mask visual's vtable is 0x{vt:X}, not the expected 0x{CVisualMDT.Vu1Vtable:X} — leaving it alone"); return; }
            byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(CVisualMDT.Vu1Vtable), CVisualMDT.Vu1VtableBytes);
            if (tbl == null) { Log("mask tint: could not read the vtable"); return; }
            BitConverter.GetBytes(ElfCave.CatMaskTint).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);           // the uint* overload
            BitConverter.GetBytes(ElfCave.CatMaskTint + 0x0Cu).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);  // the packet overload
            long at = TakeRegion(CVisualMDT.Vu1VtableBytes, out uint atG);
            if (at == 0) { Log("mask tint: no room in the region for the vtable copy"); return; }
            Memory.WriteBytesBatch(at, tbl);
            Memory.WriteUInt(_maskVisual + CVisualMDT.VisVtable, atG);
            Log($"mask tint: the mask draws through its own vtable at 0x{atG:X} → cave 0x{ElfCave.CatMaskTint:X}, under the cape's ambient");
        }

        private const int SkinNodeSize = 0x18;   // one skin-list node: {mesh, bone, type 20, count, keys, next}
        /// <summary>Hide the meshes this look does not wear — the wings on a wingless weapon, the mask on anything but Super Steve. They are
        /// all SKINNED frames, so nulling their geometry alone is unsafe (MotionProc2 writes every mesh's .wgt run through its visual each
        /// frame). The channel gets a clone of its skin list, in the region's tail, with those meshes' runs left out — the keys stay the
        /// load's, only the chain is new — and THEN their geometry pointers are cleared so nothing draws them. Before the slot goes live.</summary>
        internal static void HideMeshes(List<int> hide, string what)
        {
            if (hide.Count == 0) return;
            uint head = Memory.ReadGuestPtr(_chan + MotionType.MotionSkinList);
            var nodes = new List<byte[]>();
            for (uint p = head; Memory.IsValidGuest(p) && nodes.Count < 256;)
            {
                byte[] nd = Memory.ReadBytesBatch(Memory.ToMmu(p), SkinNodeSize);
                if (nd == null) break;
                nodes.Add(nd);
                p = (uint)BitConverter.ToInt32(nd, 0x14) & Memory.PhysAddrMask;
            }
            if (nodes.Count == 0) { Log($"{what}-off: the channel's skin list is unreadable — they stay visible"); return; }
            var keep = new List<byte[]>();
            foreach (byte[] nd in nodes) if (!hide.Contains(BitConverter.ToInt32(nd, 0))) keep.Add(nd);
            if (keep.Count == nodes.Count) { Log($"{what}-off: no such runs in the skin list — they stay visible"); return; }
            long at = TakeRegion(keep.Count * SkinNodeSize, out uint atG);
            if (at == 0) { Log($"{what}-off: no room in the region for the skin list clone — they stay visible"); return; }
            var clone = new byte[keep.Count * SkinNodeSize];
            for (int i = 0; i < keep.Count; i++)
            {
                BitConverter.GetBytes(i + 1 < keep.Count ? atG + (uint)((i + 1) * SkinNodeSize) : 0u).CopyTo(keep[i], 0x14);
                Array.Copy(keep[i], 0, clone, i * SkinNodeSize, SkinNodeSize);
            }
            Memory.WriteBytesBatch(at, clone);
            Memory.WriteUInt(_chan + MotionType.MotionSkinList, atG);
            foreach (int i in hide) Memory.WriteUInt(Memory.ToMmu(NodeAddr(i)) + CFrameVu1.GeomPtr, 0);
            Log($"{what} hidden: skin list {nodes.Count} → {keep.Count} runs (clone at 0x{atG:X}), {hide.Count} geometry pointers cleared");
        }

        /// <summary>The slot's draw state on top of what the load built: no shadow, no ambient add yet, full dim, opacity, no point
        /// lights, the cat's place and size, the leap clip at its KEY rate — and the CNPCharacter tail past the CCharacter block (foot-sound
        /// and event tables, the NPC sequence state PlaySeq runs every step, the fade/ramp words), initialized the way
        /// Initialize__12CNPCharacter / ClearSeq do: tables free (−1.0f frames), sequences cleared. A look without the cape gets the empty
        /// cloth list, so the load's cape is neither stepped nor drawn.</summary>
        private static bool SetupSlot()
        {
            long slot = SlotAddr();
            byte[] buf = Memory.ReadBytesBatch(slot, CharCopySize);
            if (buf == null) return false;
            BitConverter.GetBytes(0).CopyTo(buf, ShadowModel);
            for (int i = 0; i < CCharacter.MotionSlots; i++) BitConverter.GetBytes(0).CopyTo(buf, ShadowSlotBase + i * 4);
            if (!_look.Cape) BitConverter.GetBytes((uint)(CodeCaves.ClothStub & Memory.PhysAddrMask)).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 4);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 8);
            BitConverter.GetBytes(1.0f).CopyTo(buf, CCharacter.DimFactor);
            BitConverter.GetBytes(128f).CopyTo(buf, CCharacter.NpcOpacity);
            for (int o = CCharacter.LightFrom; o < CCharacter.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            BitConverter.GetBytes(_x).CopyTo(buf, CCharacter.CharPos);
            BitConverter.GetBytes(_h).CopyTo(buf, CCharacter.CharPos + 4);
            BitConverter.GetBytes(_y).CopyTo(buf, CCharacter.CharPos + 8);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharRot);
            BitConverter.GetBytes(_yaw).CopyTo(buf, CCharacter.CharRotY);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharRot + 8);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale + 4);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale + 8);
            BitConverter.GetBytes(KeyLeap).CopyTo(buf, CCharacter.MotionId);
            BitConverter.GetBytes(CharacterMotion.MotionSpeedUseKey).CopyTo(buf, CharacterMotion.MotionSpeedOffset);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, CCharacter.MotionFlags) | (uint)CCharacter.MotionRestart)
                .CopyTo(buf, CCharacter.MotionFlags);
            if (!_look.Cape) Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            var tail = new byte[DungeonCharaDraw.CharaStride - CharCopySize];
            for (int i = 0; i < FootSlots; i++)  BitConverter.GetBytes(-1f).CopyTo(tail, FootTable  + i * FootStride  - CharCopySize);
            for (int i = 0; i < EventSlots; i++) BitConverter.GetBytes(-1f).CopyTo(tail, EventTable + i * EventStride - CharCopySize);
            BitConverter.GetBytes(1).CopyTo(tail, SeqEnable - CharCopySize);
            BitConverter.GetBytes(-1).CopyTo(tail, DungeonCharaDraw.CharaRampB - CharCopySize);
            BitConverter.GetBytes(-1).CopyTo(tail, SeqWord1490 - CharCopySize);
            Memory.WriteBytesBatch(slot, buf);
            Memory.WriteBytesBatch(slot + CharCopySize, tail);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampB, 0);
            Log($"slot {Slot}: keys {Memory.ReadInt(slot + ChanKeyStart)}..{Memory.ReadInt(slot + ChanKeyEnd)}, cloth list {(_look.Cape ? "the load's (cape)" : "empty")}");
            return true;
        }

        /// <summary>The slot goes live: the engine steps and draws the cat from its next frame. Called once every mesh the look does not
        /// wear is hidden (<see cref="HideMeshes"/>), so no frame ever skins or draws them.</summary>
        internal static void GoLive()
        {
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, _held ? 1 : 0);
            Memory.WriteInt(Mailbox.MirageSceneGate, 1);
        }
    }
}
