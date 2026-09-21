using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.DivineBeastTitle;
using static Dark_Cloud_Improved_Version.CatCape;
using static Dark_Cloud_Improved_Version.CatFlight;
using static Dark_Cloud_Improved_Version.CatTextures;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The cat's copy: the cat subtree deep-copied out of Xiao's pack into the mod's caves (meshes, skin buffers, the copy jobs), the draw-slot registration, and the watchdog on her MOTION 1 channel. One of the <see cref="DivineBeastTitle"/> classes, which share their members through using static.</summary>
    internal static class CatCopy
    {
        // ──────────────────────────────────────────── the copy ─────────────────────────────────────────────

        /// <summary>Wall-clock through the build. The copy crosses the PINE socket a batch at a time, so the seconds
        /// between the switch and a firable cat are mostly real work rather than a wait. Every milestone reports
        /// milliseconds since Spawn began.</summary>
        private static System.Diagnostics.Stopwatch _buildClock;
        private static long _tripMark;
        internal static void BuildStep(string what)
        {
            if (_buildClock == null) return;
            Log($"  build +{_buildClock.ElapsedMilliseconds,5} ms  {Memory.Trips - _tripMark,5} trip(s)  {what}");
            _tripMark = Memory.Trips;
        }

        /// <summary>Deep-copy the cat subtree out of XIAO's live frame tree into the NodePool (the bake appends the 37
        /// `cat_` nodes after her 79 body nodes, so they are one contiguous run ending the array), make the copied cat root
        /// a free-standing root, un-hide it, give it its own skin buffers and motion channel, and host it in a dungeon
        /// chara slot.</summary>
        internal static bool Spawn()
        {
            if (Active) return true;
            _buildClock = System.Diagnostics.Stopwatch.StartNew();
            Memory.ResetTrips(); _tripMark = 0;
            if (FindTexEntry(CatTextureNames[0]) == 0 && RecreateCatEntries() < CatTextureNames.Length)
            {
                if (!_texDeferLogged) { _texDeferLogged = true; Log("her cat textures are not in the manager and none are remembered — spawn deferred (retrying)"); }
                return false;
            }
            _texDeferLogged = false;
            uint playerRoot = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (!Memory.IsValidGuest(playerRoot)) { Log("no player model"); return false; }
            _liveRoot = playerRoot;

            // Her frames are ONE contiguous 0x270 array from the model root (.mds node order): walk it while the
            // names stay printable and the parent pointers stay inside the array. The cat root is the first
            // `catroot`; everything after it is the cat.
            var names = new List<string>();
            int catIdx = -1;
            for (int i = 0; i < MaxTreeNodes; i++)
            {
                uint n = playerRoot + (uint)(i * CFrameVu1.NodeStride);
                if (!Memory.IsValidGuest(n)) break;
                string nm = ReadName(n);
                uint par = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.Parent);
                // Every body node's parent is inside the array; the cat root is UNPARENTED (the bake gives it parent
                // -1 so she never draws or skins it) and its children parent back into the cat run.
                bool parOk = i == 0 || nm == CatRootName || (par >= playerRoot && par < n && (par - playerRoot) % CFrameVu1.NodeStride == 0);
                if (nm.Length == 0 || !parOk || nm == CapeNodeName) break;      // the cape's cloth FRAME node follows the cat run: hers, not the copy's
                bool printable = true;
                foreach (char ch in nm) if (ch < 0x20 || ch > 0x7E) { printable = false; break; }
                if (!printable) break;
                names.Add(nm);
                if (catIdx < 0 && nm == CatRootName) catIdx = i;
            }
            string chanInfo = "";
            for (int c = 0; c < CCharacter.MotionSlots; c++)
            {
                uint cp = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.MotionSlotBase + c * 4);
                if (!Memory.IsValidGuest(cp)) continue;
                chanInfo += $" ch{c}=0x{cp:X} keys {Memory.ReadInt(CCharacter.Base + ChanKeyStart + c * 4)}..{Memory.ReadInt(CCharacter.Base + ChanKeyEnd + c * 4)}";
            }
            Log($"live player tree @0x{playerRoot:X}: {names.Count} node(s); channels:{chanInfo}");
            BuildStep("player tree scanned");
            if (catIdx < 0)
            {
                Log("nodes: " + string.Join(",", names));
                Log("no '" + CatRootName + "' in her tree — the loaded c04b.chr has no cat (ISO not patched, or PCSX2 still on the old image)");
                return false;
            }
            _catIndex  = catIdx;
            _nodeCount = names.Count - catIdx;
            uint min = playerRoot + (uint)(catIdx * CFrameVu1.NodeStride);
            uint max = min + (uint)((_nodeCount - 1) * CFrameVu1.NodeStride);
            int blockSize = _nodeCount * CFrameVu1.NodeStride;
            if (_nodeCount > CodeCaves.MaxNodes) { Log($"{_nodeCount} cat nodes exceed the NodePool"); return false; }
            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            _skinNodes.Clear();
            uint poolG = (uint)CodeCaves.NodePoolGuest;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                bool isRoot = o == 0;
                Memory.Rebase(block, o + CFrameVu1.Parent,      min, max, poolG, isRoot);   // cat root's parent (her root) → none
                Memory.Rebase(block, o + CFrameVu1.RootChild,   min, max, poolG, false);
                Memory.Rebase(block, o + CFrameVu1.RootSibling, min, max, poolG, isRoot);   // ...and its sibling chain → none
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
                Array.Clear(block, o + CFrameVu1.WorldMatrix, 0x40);
            }
            // Un-hide: the bake scaled the cat root's bind 3x3 by HideScale so SHE never shows it.
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int b = CFrameVu1.LocalMatrix + r * 0x10 + c * 4;
                    BitConverter.GetBytes(BitConverter.ToSingle(block, b) / HideScale).CopyTo(block, b);
                }
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransX);           // root sits at the object origin
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransY);
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransZ);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX + 4);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX + 8);
            BitConverter.GetBytes(0).CopyTo(block, CFrameVu1.DirtyTrs);
            Memory.WriteBytesBatch(CodeCaves.NodePool, block);
            _copyRoot = poolG;
            FindHead(block);
            _wingMeshIdx.Clear();
            foreach (string nm in WingMeshNodes) { int wi = FindNode(block, nm); if (wi >= 0) _wingMeshIdx.Add(wi); }
            _maskMeshIdx = FindNode(block, MaskNodeName); _maskVisual = 0;

            if (!CopyMeshes()) return false;
            if (!RegisterSlot(min, blockSize)) return false;
            Active = true;
            BuildStep("channel + slot registered");
            Log($"cat copy up: {_nodeCount} nodes (her n{_catIndex}..n{_catIndex + _nodeCount - 1}) → 0x{_copyRoot:X}, slot {Slot}, built in {_buildClock.ElapsedMilliseconds:N0} ms over {Memory.Trips:N0} round trip(s), {Memory.TripBytes:N0} B");
            _buildClock = null;                    // the stamps are for the BUILD: left running they reported a despawn's
            return true;                           // texture restore as a 70 s, half-million-trip step (it was 70 s of play)
        }

        /// <summary>The head's rest position in the cat's own space (row-vector chain of local matrices from
        /// `cat_kao` up to the root), so the flight can keep the HEAD on the pellet's line.</summary>
        private static int FindNode(byte[] block, string name)
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                int o = i * CFrameVu1.NodeStride + CFrameVu1.Name, len = 0;
                while (len < 0x20 && block[o + len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(block, o, len) == name) return i;
            }
            return -1;
        }

        /// <summary>Locate the frames the cave needs and measure the head's rest offset. Publishes the pool addresses of
        /// <see cref="HeadNodeName"/> (the cave's contact point) and the two glow anchors to the mailbox, clears CatGlowReady
        /// so the glow rebinds, then walks the head's local matrices up to the root for its position in cat space — the
        /// offset the flight keeps on the pellet's line. Falls back to the root height if the head is missing.</summary>
        private static void FindHead(byte[] block)
        {
            _headX = 0f; _headH = HeadFallbackHeight; _headZ = 0f;
            int head = -1;
            for (int i = 0; i < _nodeCount; i++)
            {
                int o = i * CFrameVu1.NodeStride + CFrameVu1.Name, len = 0;
                while (len < 0x20 && block[o + len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(block, o, len) == HeadNodeName) { head = i; break; }
            }
            Memory.WriteInt(CodeCaves.Mailbox.CatHeadNode, head >= 0 ? (int)(CodeCaves.NodePoolGuest + head * CFrameVu1.NodeStride) : 0);   // the cave's contact point = this frame's posed position
            int ga = FindNode(block, GlowNodeA), gb = FindNode(block, GlowNodeB);                                                              // the glow's anchor frames
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowNodeA, ga >= 0 ? (int)(CodeCaves.NodePoolGuest + ga * CFrameVu1.NodeStride) : 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowNodeB, gb >= 0 ? (int)(CodeCaves.NodePoolGuest + gb * CFrameVu1.NodeStride) : 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowReady, 0);
            if (ga < 0 || gb < 0) Log($"glow anchors: {GlowNodeA} n{ga}, {GlowNodeB} n{gb} — falling back to the root");
            if (head < 0) return;
            uint poolG = (uint)CodeCaves.NodePoolGuest;
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
                n = par >= poolG ? (int)((par - poolG) / CFrameVu1.NodeStride) : 0;
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
            return System.Text.Encoding.ASCII.GetString(b, 0, len);
        }

        private static long _ovFree;
        /// <summary>Hand out cave space from two arenas: every mesh's visual, first VU buffer and MDT go in the MeshCave
        /// (below the Angel Gear prop's region); the SECOND VU buffers and the skin sources take whatever fits — the
        /// MeshCave's remainder, else the overflow cave borrowed from CharacterClone's cloth caves, idle while Xiao is
        /// the active character. Returns 0 when neither has room.</summary>
        internal static long TakeCave(int bytes, out uint guest)
        {
            long need = Memory.Align16(bytes);
            if (_caveFree + need <= CodeCaves.CatMeshCaveEnd) { long a = _caveFree; _caveFree += need; guest = Memory.ToGuest(a); return a; }
            if (_ovFree + need <= CodeCaves.CatOverflowCave + CodeCaves.CatOverflowCaveSize) { long a = _ovFree; _ovFree += need; guest = Memory.ToGuest(a); return a; }
            guest = 0; return 0;
        }
        /// <summary>Give the mask the CAPE's colour rather than the cat's. A cloth is easy — Draw__10CCharacter walks its cloth
        /// list calling Draw__6CCloth, so ElfCave.CatCapeTint wraps that one call — but a mesh has no such seam: the ambient is
        /// set once, MGDraw runs over the whole frame tree, and a mesh's tint is ADDED to its lit colour rather than multiplied
        /// through its texture, so the cat's blue lands on the mask and turns red to pink. Meshes draw through C++ virtual
        /// calls and <see cref="CopyMeshes"/> has already given every cat mesh a PRIVATE CVisualMDT in the mod's cave, so this
        /// copies __vt__13CVisualMDTVu1, points its two DrawVu1 slots at ElfCave.CatMaskTint, and writes that copy into the
        /// mask's visual alone — no engine code touched, and the only pointer to the cave is in an object the mod allocated.
        /// The cave adds Mailbox.CatCapeTint, the same delta the cape uses, so the two match by construction. ⚠ The vptr is at
        /// +0x08, not offset 0; refuses unless the visual really holds the stock vtable, so a layout surprise is a no-op.</summary>
        internal static void MaskTint()
        {
            if (_maskVisual == 0) { Log("mask tint: the mask has no copied visual — it keeps the cat's colour"); return; }
            uint vt = Memory.ReadGuestPtr(_maskVisual + CVisualMDT.VisVtable);
            if (vt != CVisualMDT.Vu1Vtable)
            { Log($"mask tint: the mask visual's vtable is 0x{vt:X}, not the expected 0x{CVisualMDT.Vu1Vtable:X} — leaving it alone"); return; }
            byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(CVisualMDT.Vu1Vtable), CVisualMDT.Vu1VtableBytes);
            if (tbl == null) { Log("mask tint: could not read the vtable"); return; }
            BitConverter.GetBytes(CodeCaves.ElfCave.CatMaskTint).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);           // the uint* overload
            BitConverter.GetBytes(CodeCaves.ElfCave.CatMaskTint + 0x0Cu).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);  // the packet overload
            long cave = TakeCave(CVisualMDT.Vu1VtableBytes, out uint caveG);
            if (cave == 0) { Log("mask tint: no cave room for the vtable copy"); return; }
            Memory.WriteBytesBatch(cave, tbl);
            Memory.WriteUInt(_maskVisual + CVisualMDT.VisVtable, caveG);
            Log($"mask tint: the mask draws through its own vtable at 0x{caveG:X} → cave 0x{CodeCaves.ElfCave.CatMaskTint:X}, under the cape's ambient");
        }

        /// <summary>Give the cat's software-skinned meshes their own copies in the MeshCave (CopyMeshNodes' recipe). HER copy
        /// of the cat skin sits collapsed under the hidden root, and MotionProc2 would skin a shared buffer for whichever
        /// character stepped last, so the copy must own it or the spawn is refused.</summary>
        private static bool CopyMeshes()
        {
            _copied.Clear(); _jobs.Clear(); _pending.Clear();
            long cave = CodeCaves.MeshCave, caveGuest = CodeCaves.MeshCaveGuest;
            long caveEnd = CodeCaves.CatMeshCaveEnd;                                // above it: the Angel Gear prop's meshes, then its track cave
            _ovFree = CodeCaves.CatOverflowCave;
            int copied = 0;
            var second = new List<(long vis, byte[] vuB, int vuSz, int idx)>();
            byte[] pool = Memory.ReadBytesBatch(CodeCaves.NodePool, _nodeCount * CFrameVu1.NodeStride);   // every node in one read
            if (pool == null) { Log("could not read the copy's node pool"); return false; }
            for (int i = 0; i < _nodeCount; i++)
            {
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                int po = i * CFrameVu1.NodeStride;
                uint vis = (uint)BitConverter.ToInt32(pool, po + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                if (!_look.Wings && _wingMeshIdx.Contains(i)) continue;              // a wingless look: the wings are never copied (HideMeshes unlinks their runs and nulls their geometry)
                if (!_look.Cape && i == _maskMeshIdx) continue;                      // and the mask belongs to Super Steve alone
                int visSz = CVisualMDT.VisualSize;
                // One read for the whole visual, one for the MDT's header — per-field round trips cost milliseconds each.
                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                if (visB == null) continue;
                uint mdt  = (uint)BitConverter.ToInt32(visB, CVisualMDT.VisMDT) & Memory.PhysAddrMask;
                uint vu   = (uint)BitConverter.ToInt32(visB, CVisualMDT.VisVU)  & Memory.PhysAddrMask;
                int  vuSz = BitConverter.ToInt32(visB, CVisualMDT.VisVU + 4) * 16;
                if (!Memory.IsValidGuest(mdt)) continue;
                byte[] mdtHdr = Memory.ReadBytesBatch(Memory.ToMmu(mdt), 16);
                if (mdtHdr == null || (uint)BitConverter.ToInt32(mdtHdr, 0) != CVisualMDT.MdtMagic) continue;
                int mdtSz = BitConverter.ToInt32(mdtHdr, CVisualMDT.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;
                int need = Memory.Align16(visSz) + Memory.Align16(vuSz) + Memory.Align16(mdtSz);
                if (cave + need > caveEnd) { Log("cat meshes do not fit the MeshCave"); return false; }
                long cVis = cave;              uint cVisG = (uint)caveGuest;
                long cVU  = cave + Memory.Align16(visSz); uint cVUG  = (uint)(caveGuest + Memory.Align16(visSz));
                long cMDT = cVU + Memory.Align16(vuSz);   uint cMDTG = (uint)(caveGuest + Memory.Align16(visSz) + Memory.Align16(vuSz));
                // The two BIG blocks go to the machine; the visual is 0x30 B and needs fields poked into it anyway, so it
                // stays here. A job carries the copy and both rebases, exactly what RebaseRange does to vuB/mdtB below.
                _jobs.Add(new CopyJob(vu, cVUG, vuSz, vu, vuSz, cVUG, mdt, mdtSz, cMDTG));
                _jobs.Add(new CopyJob(mdt, cMDTG, mdtSz, vu, vuSz, cVUG, mdt, mdtSz, cMDTG));
                Memory.RebaseRange(visB, vu, vuSz, cVUG); Memory.RebaseRange(visB, mdt, mdtSz, cMDTG);
                byte[] vuB = null;
                // The engine writes the skinned draw packet into buffer[DBuffID] (+0x28 / +0x2C) every frame while
                // the GIF is still reading the other. A single-buffered copy tears (flicker); give the copy both — the
                // second one is placed after every mesh's primary data has a home (second pass below).
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);
                Memory.WriteBytesBatch(cVis, visB);
                _pending.Add((cVU, vuSz, cMDT, mdtSz, vis, vu, mdt, cVUG, cMDTG));
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);
                if (i == _maskMeshIdx) _maskVisual = cVis;                           // the mask's visual is ours alone — MaskTint retints it
                cave += need; caveGuest += need; copied++;
                _skinNodes.Add((i, cMDT, mdtSz, cVU, 0L, vuSz));
                second.Add((cVis, vuB, vuSz, _skinNodes.Count - 1));
                int nl = 0; while (nl < 0x20 && pool[po + CFrameVu1.Name + nl] != 0) nl++;     // the name is in the pool we read
                Log($"mesh n{i} ({System.Text.Encoding.ASCII.GetString(pool, po + CFrameVu1.Name, nl)}): vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} copied");
            }
            _caveFree = cave;
            if (copied == 0) { Log("no software-skinned cat mesh found — refusing to share her collapsed copy of the skin"); return false; }
            foreach (var (vis, vuB, vuSz, idx) in second)                          // second VU buffers: MeshCave remainder, else the overflow cave
            {
                long cVU2 = TakeCave(vuSz, out uint cVU2G);
                if (cVU2 == 0) { Log($"mesh n{_skinNodes[idx].node}: no room for a second VU buffer — single-buffered (may flicker)"); continue; }
                var pe = _pending[idx];
                _jobs.Add(new CopyJob(pe.vu, Memory.ToGuest(cVU2), vuSz, pe.vu, vuSz, pe.cVUG, pe.mdt, pe.mdtSz, pe.cMDTG));
                Memory.WriteUInt(vis + 0x2c, cVU2G);
                var e = _skinNodes[idx]; _skinNodes[idx] = (e.node, e.mdt, e.mdtSz, e.vu, cVU2, e.vuSz);
            }
            if (!RunCopyJobs() && !CopyJobsBySocket()) return false;               // the machine does it, or we do it the slow way
            Log($"mesh caves: main {_caveFree - CodeCaves.MeshCave:N0} of {CodeCaves.CatMeshCaveEnd - CodeCaves.MeshCave:N0} B, overflow {_ovFree - CodeCaves.CatOverflowCave:N0} of {CodeCaves.CatOverflowCaveSize:N0} B");
            BuildStep("meshes copied");
            return true;
        }

        internal static readonly List<(int node, long mdt, int mdtSz, long vu, long vu2, int vuSz)> _skinNodes = new();
        private static long _caveFree;

        /// <summary>Every block CopyMeshes wrote, by the address it went to. RelocateCatTextures has to hunt TEX0 register
        /// words through all of it, and scans these bytes rather than re-reading 300 KB back over PINE, writing back only
        /// what it changed. The
        /// second draw buffer gets a CLONE, not the same array: the two blocks hold identical bytes, and sharing one array
        /// would let the first patch mark the second clean and leave it unpatched on screen.</summary>
        private static readonly Dictionary<long, byte[]> _copied = new();

        /// <summary>The mesh copy, handed to the machine. Each job is "move `size` bytes src → dst, then re-point every
        /// pointer-looking word of the copy that falls in one of two source ranges". ElfCave's CatCopyQueue does it in one
        /// frame; the C# below does the same a batch at a time over PINE. The queue is written jobs-first and COUNT LAST so the
        /// cave can never see a half-written list, and it falls back whenever the cave does not answer — slow is a far better
        /// failure than wrong.</summary>
        internal readonly struct CopyJob
        {
            public readonly uint Src, Dst; public readonly int Size;
            public readonly uint R1Src, R1Dst; public readonly int R1Size;
            public readonly uint R2Src, R2Dst; public readonly int R2Size;
            public readonly int Op;                                                  // 0 = copy + re-point, 1 = find/replace 64-bit
            public CopyJob(uint src, uint dst, int size, uint r1s, int r1n, uint r1d, uint r2s, int r2n, uint r2d)
            { Src = src; Dst = dst; Size = size; R1Src = r1s; R1Size = r1n; R1Dst = r1d; R2Src = r2s; R2Size = r2n; R2Dst = r2d; Op = 0; }
            /// <summary>Sweep one block for every old→new pair in <see cref="_pairs"/> — the cat's TEX0 registers, after its
            /// textures move. The pair count and table address are filled in when the queue is written.</summary>
            public CopyJob(uint block, int size)
            { Src = 0; Dst = block; Size = size; R1Src = 0; R1Size = 0; R1Dst = 0; R2Src = 0; R2Size = 0; R2Dst = 0; Op = 1; }
        }
        internal static readonly List<CopyJob> _jobs = new();
        internal static readonly List<(ulong oldV, ulong newV)> _pairs = new();          // shared by every find/replace job
        private static readonly List<(long cVU, int vuSz, long cMDT, int mdtSz, long vis, uint vu, uint mdt, uint cVUG, uint cMDTG)> _pending = new();

        private const int SkinNodeSize = 0x18;   // one skin-list node: {mesh, bone, type 20, count, keys, next}
        /// <summary>Hide the meshes this look does not wear — the wings on a wingless weapon, the mask on anything but Super
        /// Steve. They are all SKINNED frames, so nulling their geometry alone is unsafe (MotionProc2 writes every mesh's .wgt
        /// run through its visual each frame). The copy's channel gets a PRIVATE clone of the skin list with those meshes' runs
        /// left out — the keys still point at her data, only the chain is ours — and THEN their geometry pointers are cleared
        /// so nothing draws them. Her own list is untouched; one pass builds the clone once.</summary>
        internal static void HideMeshes(List<int> hide, string what)
        {
            if (hide.Count == 0) return;
            long chan = CodeCaves.MotionCave;                                              // the copy's channel struct
            uint head = Memory.ReadGuestPtr(chan + MotionType.MotionSkinList);
            var nodes = new List<byte[]>();
            for (uint p = head; Memory.IsValidGuest(p) && nodes.Count < 256;)
            {
                byte[] n = Memory.ReadBytesBatch(Memory.ToMmu(p), SkinNodeSize);
                if (n == null) break;
                nodes.Add(n);
                p = (uint)BitConverter.ToInt32(n, 0x14) & Memory.PhysAddrMask;
            }
            if (nodes.Count == 0) { Log($"{what}-off: the copy's skin list is unreadable — they stay visible"); return; }
            var keep = new List<byte[]>();
            foreach (byte[] n in nodes) if (!hide.Contains(BitConverter.ToInt32(n, 0))) keep.Add(n);
            if (keep.Count == nodes.Count) { Log($"{what}-off: no such runs in the skin list — they stay visible"); return; }
            long cave = TakeCave(keep.Count * SkinNodeSize, out uint caveG);
            if (cave == 0) { Log($"{what}-off: no cave room for the skin list clone — they stay visible"); return; }
            for (int i = 0; i < keep.Count; i++)
            {
                BitConverter.GetBytes(i + 1 < keep.Count ? caveG + (uint)((i + 1) * SkinNodeSize) : 0u).CopyTo(keep[i], 0x14);
                Memory.WriteBytesBatch(cave + i * SkinNodeSize, keep[i]);
            }
            Memory.WriteUInt(chan + MotionType.MotionSkinList, caveG);
            foreach (int i in hide) Memory.WriteUInt(CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride + CFrameVu1.GeomPtr, 0);
            Log($"{what} hidden: skin list {nodes.Count} → {keep.Count} runs (private clone at 0x{caveG:X}), {hide.Count} geometry pointers cleared");
        }

        /// <summary>The old path, kept whole as the fallback: read each source block, rebase it here, write the copy. Only runs
        /// when the cave is absent or silent.</summary>
        internal static bool CopyJobsBySocket()
        {
            foreach (CopyJob j in _jobs)
            {
                if (j.Op == 1)
                {
                    byte[] blk = Memory.ReadBytesBatch(Memory.ToMmu(j.Dst), j.Size);
                    if (blk == null) continue;
                    bool hit = false;
                    for (int o = 0; o + 8 <= blk.Length; o += 4)
                    {
                        ulong w = BitConverter.ToUInt64(blk, o);
                        foreach (var (oldV, newV) in _pairs)
                            if (w == oldV) { BitConverter.GetBytes(newV).CopyTo(blk, o); hit = true; o += 4; break; }
                    }
                    if (hit) Memory.WriteBytesBatch(Memory.ToMmu(j.Dst), blk);
                    continue;
                }
                byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(j.Src), j.Size);
                if (b == null) { Log($"copy fallback: could not read 0x{j.Src:X}"); return false; }
                if (j.R1Size > 0) Memory.RebaseRange(b, j.R1Src, j.R1Size, j.R1Dst);
                if (j.R2Size > 0) Memory.RebaseRange(b, j.R2Src, j.R2Size, j.R2Dst);
                Memory.WriteBytesBatch(Memory.ToMmu(j.Dst), b);
                _copied[Memory.ToMmu(j.Dst)] = b;                                   // the texture pass scans these instead of re-reading
            }
            _jobs.Clear(); return true;
        }

        /// <summary>Hand the queued copy jobs to the cave, draining in queue-sized batches when there are more jobs than the
        /// queue holds. Refuses outright if this ISO lacks the hook, and on a failed batch restores the full list — the jobs
        /// already done plus the untouched tail — so the caller can fall back to the C# path with nothing half-applied.</summary>
        internal static bool RunCopyJobs()
        {
            if (_jobs.Count == 0) return true;
            if ((uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) != DunPatches.CatFollowHookNew) return false;
            while (_jobs.Count > CodeCaves.CatCopyQueueJobs)                            // drain in queue-sized batches
            {
                var head = _jobs.GetRange(0, CodeCaves.CatCopyQueueJobs);
                var tailJobs = _jobs.GetRange(CodeCaves.CatCopyQueueJobs, _jobs.Count - CodeCaves.CatCopyQueueJobs);
                _jobs.Clear(); _jobs.AddRange(head);
                if (!RunCopyJobs()) { _jobs.Clear(); _jobs.AddRange(head); _jobs.AddRange(tailJobs); return false; }
                _jobs.Clear(); _jobs.AddRange(tailJobs);
            }
            var buf = new byte[0x10 + _jobs.Count * CodeCaves.CatCopyJobStride];
            for (int i = 0; i < _jobs.Count; i++)
            {
                int o = 0x10 + i * CodeCaves.CatCopyJobStride; CopyJob j = _jobs[i];
                BitConverter.GetBytes(j.Src).CopyTo(buf, o); BitConverter.GetBytes(j.Dst).CopyTo(buf, o + 4);
                BitConverter.GetBytes(j.Size).CopyTo(buf, o + 8);
                BitConverter.GetBytes(j.R1Src).CopyTo(buf, o + 0x0C); BitConverter.GetBytes(j.R1Size).CopyTo(buf, o + 0x10);
                BitConverter.GetBytes(j.R1Dst).CopyTo(buf, o + 0x14);
                BitConverter.GetBytes(j.R2Src).CopyTo(buf, o + 0x18); BitConverter.GetBytes(j.R2Size).CopyTo(buf, o + 0x1C);
                BitConverter.GetBytes(j.R2Dst).CopyTo(buf, o + 0x20);
                BitConverter.GetBytes(j.Op).CopyTo(buf, o + 0x24);
                if (j.Op == 1)
                {
                    BitConverter.GetBytes(_pairs.Count).CopyTo(buf, o + 0x0C);
                    BitConverter.GetBytes(CodeCaves.CatCopyQueueGuest + (uint)CodeCaves.CatCopyPairsOff).CopyTo(buf, o + 0x10);
                }
            }
            if (_pairs.Count > 0)
            {
                var pb = new byte[_pairs.Count * 16];
                for (int i = 0; i < _pairs.Count; i++)
                { BitConverter.GetBytes(_pairs[i].oldV).CopyTo(pb, i * 16); BitConverter.GetBytes(_pairs[i].newV).CopyTo(pb, i * 16 + 8); }
                Memory.WriteBytesBatch(CodeCaves.CatCopyQueue + CodeCaves.CatCopyPairsOff, pb);
            }
            Memory.WriteBytesBatch(CodeCaves.CatCopyQueue, buf);                        // jobs first…
            Memory.WriteInt(CodeCaves.CatCopyQueue, _jobs.Count);                       // …then the count: the cave's go signal
            // The cave services the whole queue in the frame it next runs, so the only cost here is noticing. Poll tightly:
            // sleeping 4 ms between checks put a floor of several hundred ms on a build that is otherwise one frame of work.
            var waited = System.Diagnostics.Stopwatch.StartNew();
            for (int spin = 0; spin < 4000; spin++)
            {
                if (Memory.ReadInt(CodeCaves.CatCopyQueue) == 0)
                {
                    Log($"copy queue: {_jobs.Count} job(s) run in the machine ({_jobs.Sum(j => j.Size):N0} B) — "
                                          + $"{waited.ElapsedMilliseconds} ms, {spin + 1} poll(s)");
                    _jobs.Clear(); return true;
                }
                if (spin >= 40) Thread.Sleep(1);                                        // first 40 are back to back
                if (waited.ElapsedMilliseconds > 2000) break;
            }
            Memory.WriteInt(CodeCaves.CatCopyQueue, 0);
            Log("copy queue: the cave did not answer — falling back to copying over PINE");
            return false;
        }

        /// <summary>The skinner's SOURCE vertices. AnimeDataInit (0x1493A0) runs once per character — from
        /// CommandMOTION only while CCharacter+0x2CC is still null, i.e. for MOTION 0 — and builds, for every mesh
        /// in THAT channel's skin list, a bind-transformed copy of its vertices into FRAME_INF[mesh] (+4 count,
        /// +8 → copy). Her cat skin is a MOTION 1 mesh, so its entry never got one: MotionProc2 would read its
        /// source vertices from address 0. Build it here in
        /// the MeshCave exactly as the initializer does: def[i] = bindMatrix(node) · vertex[i], with the bind
        /// matrix taken from the table row the caller built from the copied node.</summary>
        private static bool BuildSkinSources(byte[] fib)
        {
            foreach (var (node, mdt, _, _, _, _) in _skinNodes)
            {
                int count = Memory.ReadInt(mdt + CVisualMDT.MdtVertCount);
                int vOff  = Memory.ReadInt(mdt + CVisualMDT.MdtVertOffset);
                if (count <= 0 || count > 3000 || vOff <= 0) { Log($"skin n{node}: odd MDT header (count {count}, verts @+0x{vOff:X})"); return false; }
                int bytes = count * 16;
                long cave = TakeCave(bytes, out uint caveG);
                if (cave == 0) { Log("skin source vertices do not fit the mesh caves"); return false; }
                byte[] src = Memory.ReadBytesBatch(mdt + vOff, bytes);
                if (src == null) return false;
                int e = node * MotionType.FrameInfEntry;
                float[] m = new float[16];
                for (int k = 0; k < 16; k++) m[k] = BitConverter.ToSingle(fib, e + 0x10 + k * 4);   // the entry's bind matrix
                byte[] dst = new byte[bytes];
                for (int v = 0; v < count; v++)
                {
                    float x = BitConverter.ToSingle(src, v * 16), y = BitConverter.ToSingle(src, v * 16 + 4), z = BitConverter.ToSingle(src, v * 16 + 8), w = BitConverter.ToSingle(src, v * 16 + 12);
                    for (int c = 0; c < 4; c++)
                        BitConverter.GetBytes(x * m[c] + y * m[4 + c] + z * m[8 + c] + w * m[12 + c]).CopyTo(dst, v * 16 + c * 4);
                }
                Memory.WriteBytesBatch(cave, dst);
                BitConverter.GetBytes(count).CopyTo(fib, e + 4);
                BitConverter.GetBytes(caveG).CopyTo(fib, e + 8);
                Log($"skin n{node}: {count} source vertices built at 0x{caveG:X} from bind [{m[0]:F2} {m[5]:F2} {m[10]:F2} | {m[12]:F2},{m[13]:F2},{m[14]:F2}]");
                BuildStep($"skin n{node} built ({count} vertices)");
            }
            return true;
        }

        // ── Her MOTION 1 channel: watchdog + repair ────────────────────────────────────────────────────────
        private const int  ChanInlineBase = 0x420, ChanInlineStride = 0x80;   // CommandMOTION's inline channel: character + 0x420 + 0x80·n
        private static bool _herChanValid = true;
        internal static DateTime _lastDespawn = DateTime.MinValue;

        /// <summary>Watch her channel-1 pointer (+0xC24): when it reads invalid every later shot fails. No engine writer of
        /// that word runs mid-floor — DeleteExtendMotion is town-only, Initialize/CommandMOTION run on loads, operator= only
        /// for town NPCs — and the mod never writes her table, so this logs the tick it changes along with the raw words.
        /// <see cref="RepairHerCatChannel"/> puts it back while the inline channel struct is still intact.</summary>
        internal static void WatchHerCatChannel()
        {
            uint raw = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.MotionSlotBase + CatChannel * 4);
            bool valid = Memory.IsValidGuest(raw & Memory.PhysAddrMask);
            if (valid == _herChanValid) return;
            _herChanValid = valid;
            if (valid) { Log($"her MOTION 1 pointer is back (0x{raw:X8})"); return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < CCharacter.MotionSlots; i++)
                sb.Append($" ch{i}=0x{(uint)Memory.ReadInt(CCharacter.Base + CCharacter.MotionSlotBase + i * 4):X8}/{Memory.ReadInt(CCharacter.Base + ChanKeyStart + i * 4)}..{Memory.ReadInt(CCharacter.Base + ChanKeyEnd + i * 4)}");
            Log(
                $"her MOTION 1 pointer LOST (raw 0x{raw:X8}) — phase {(Active ? _phase.ToString() : "idle")}, {(GameClock.Now - _lastDespawn).TotalSeconds:F2} s after the last despawn, event mode {Memory.ReadInt(DungeonScriptEvent.BtEventMode)}; table:{sb}");
        }

        /// <summary>Put her channel-1 pointer and key range back when the inline MOTION struct still holds its data
        /// (its KEY table and bind rows are valid pointers). Returns true and refreshes <paramref name="buf"/> on success.</summary>
        private static bool RepairHerCatChannel(ref byte[] buf)
        {
            long inline = CCharacter.Base + ChanInlineBase + CatChannel * ChanInlineStride;
            uint keyTable = Memory.ReadGuestPtr(inline + MotionType.MotionInfoPtr);
            uint boneRows = Memory.ReadGuestPtr(inline + MotionType.BoneMtxPtr);
            if (!Memory.IsValidGuest(keyTable) || !Memory.IsValidGuest(boneRows))
            {
                Log($"her MOTION 1 struct @0x{inline & Memory.PhysAddrMask:X} is empty too (KEY 0x{keyTable:X}, rows 0x{boneRows:X}) — cannot repair");
                return false;
            }
            Memory.WriteUInt(CCharacter.Base + CCharacter.MotionSlotBase + CatChannel * 4, (uint)(inline & Memory.PhysAddrMask));
            Memory.WriteInt (CCharacter.Base + ChanKeyStart + CatChannel * 4, KeyBase);
            Memory.WriteInt (CCharacter.Base + ChanKeyEnd   + CatChannel * 4, KeyBase + KeyCount);
            Log($"her MOTION 1 pointer repaired → 0x{inline & Memory.PhysAddrMask:X} (keys {KeyBase}..{KeyBase + KeyCount})");
            buf = Memory.ReadBytesBatch(CCharacter.Base, CharCopySize) ?? buf;
            return true;
        }

        /// <summary>Host slot from XIAO's own CCharacter (draw/texture config, as the Mirage clone does), aimed at
        /// the copy, with her cat channel (MOTION 1) cloned as the copy's ONLY channel — own FrameInf/BoneMtx (any
        /// bone pointers into the live cat block re-based to the copy), the KEY table and track list shared
        /// read-only — and the copy's key range set to the cat keys so <see cref="SetKey"/> with 64..69 selects it.
        /// The cat channel's tracks are indexed relative to the cat root, which IS the copy's node 0.</summary>
        private static bool RegisterSlot(uint min, int blockSize)
        {
            byte[] buf = Memory.ReadBytesBatch(CCharacter.Base, CharCopySize);
            if (buf == null) return false;
            uint chan = (uint)BitConverter.ToInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chan) && RepairHerCatChannel(ref buf))
                chan = (uint)BitConverter.ToInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chan)) { Log($"she has no MOTION 1 channel (raw 0x{BitConverter.ToUInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4):X8}) — the loaded c04b.chr has no cat"); return false; }
            byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(chan), MotionStructSize);
            if (mstr == null) return false;
            int fiSize = (_nodeCount + 1) * MotionType.FrameInfEntry;
            int bmSize = (_nodeCount + 1) * MotionType.BoneMtxEntry;
            if (fiSize > CodeCaves.CatFrameInfCaveSize || bmSize > CodeCaves.CatBoneMtxCaveSize) { Log("bone buffers exceed the caves"); return false; }
            uint poolG = (uint)CodeCaves.NodePoolGuest;
            // FRAME_INF is indexed by NODE (AnimeDataInit 0x1493A0: entry i = {parent index, skin vertex count, →
            // bind-vertex copy, bind matrix @+0x10, two scratch matrices @+0x50/+0x90}). The initializer fills the
            // rows only for nodes reachable from her model root — and the cat is UNPARENTED on purpose — so her
            // rows for the cat are uninitialized heap (they printed as floats). Build the copy's table from the
            // copied nodes themselves: parent index made cat-relative (root → itself) and the bind matrix = the
            // node's local matrix (never posed on her; the root's is the un-hidden one written above).
            {
                byte[] fib = new byte[fiSize];
                for (int i = 0; i < _nodeCount; i++)
                {
                    long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                    uint par = Memory.ReadGuestPtr(node + CFrameVu1.Parent);
                    int rel = (par >= poolG && par < poolG + (uint)blockSize) ? (int)((par - poolG) / CFrameVu1.NodeStride) : 0;
                    int e = i * MotionType.FrameInfEntry;
                    BitConverter.GetBytes(rel).CopyTo(fib, e);
                    byte[] local = Memory.ReadBytesBatch(node + CFrameVu1.LocalMatrix, 0x40);
                    if (local == null) return false;
                    local.CopyTo(fib, e + 0x10);
                }
                if (!BuildSkinSources(fib)) return false;
                Memory.WriteBytesBatch(CodeCaves.FrameInfCave, fib);
                BitConverter.GetBytes((uint)CodeCaves.FrameInfCaveGuest).CopyTo(mstr, MotionType.FrameInfPtr);
            }
            // Per-bone BIND matrices (channel +0): CreateAnimeDataEX (0x149090) makes this buffer a straight copy of
            // the channel's .bbp file, and the skinner chains it into the inverse-bind side (posed world × inv(bind
            // world) × vertex). Her channel 1's buffer IS cat.bbp — 37 rows in cat order — so take rows 0..36 as they
            // are. (The .mds local matrices are NOT the same thing: initializing from them stretched every blended
            // joint — neck, shoulders, tail tip.)
            uint bm = (uint)BitConverter.ToInt32(mstr, MotionType.BoneMtxPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(bm)) { Log("her cat channel has no bind rows"); return false; }
            {
                byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                if (bmb == null) return false;
                Memory.WriteBytesBatch(CodeCaves.BoneMtxCave, bmb);
                BitConverter.GetBytes((uint)(CodeCaves.BoneMtxCave & Memory.PhysAddrMask)).CopyTo(mstr, MotionType.BoneMtxPtr);
            }
            uint keyTable = (uint)BitConverter.ToInt32(mstr, MotionType.MotionInfoPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(keyTable)) { Log("cat KEY table unreadable"); return false; }
            // The float-up's play rate goes into its KEY entry, not the speed override: Step's play-once stop test
            // (0x138530) looks ahead by the KEY rate while the advance uses the override, so an override faster than
            // the KEY rate overshoots the last frame and the clip wraps, so the float loops instead of holding.
            // The table is her hidden cat channel's, played by nobody but this copy.
            Memory.WriteFloat(Memory.ToMmu(keyTable) + (KeyFloat - KeyBase) * CCharacter.MotionEntryStride + 8, FloatRate);
            Memory.RebaseRange(mstr, min, blockSize, poolG);
            Memory.WriteBytesBatch(CodeCaves.MotionCave, mstr);

            BitConverter.GetBytes((uint)CodeCaves.MotionCaveGuest).CopyTo(buf, CCharacter.MotionSlotBase);
            for (int s = 1; s < CCharacter.MotionSlots; s++) BitConverter.GetBytes(0).CopyTo(buf, CCharacter.MotionSlotBase + s * 4);
            BitConverter.GetBytes(KeyBase).CopyTo(buf, ChanKeyStart);
            BitConverter.GetBytes(KeyBase + KeyCount).CopyTo(buf, ChanKeyEnd);
            for (int s = 1; s < CCharacter.MotionSlots; s++)
            {
                BitConverter.GetBytes(0).CopyTo(buf, ChanKeyStart + s * 4);
                BitConverter.GetBytes(0).CopyTo(buf, ChanKeyEnd + s * 4);
            }
            BitConverter.GetBytes(keyTable).CopyTo(buf, CCharacter.MotionList);
            // No shadow: her CCharacter carries her shadow rig (+0xC0) and shadow channels (+0xC40+i*4), which
            // ShadowStep would step with OUR motion id against HER shadow KEY table (37 entries, ids 64..69 fall
            // off its end) and pose HER shadow tree. The slingshot copy has none either.
            BitConverter.GetBytes(0).CopyTo(buf, ShadowModel);
            for (int i = 0; i < CCharacter.MotionSlots; i++) BitConverter.GetBytes(0).CopyTo(buf, ShadowSlotBase + i * 4);
            BitConverter.GetBytes((uint)CodeCaves.ClothStubGuest).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(_copyRoot).CopyTo(buf, CCharacter.CharModel);
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

            Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            long slot = SlotAddr();
            // The CNPCharacter tail past the copied 0xD60 block (foot-sound + event tables, the NPC sequence
            // state PlaySeq runs every step, the fade/ramp words) is whatever this slot last held. Initialize it
            // the way Initialize__12CNPCharacter / ClearSeq do: tables free (-1.0f frames), sequences cleared.
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
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, _held ? 1 : 0);
            Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 1);
            RetagCatTextures(HerTextureBlock, SlotTextureGroup);
            uint boneHead = (uint)BitConverter.ToInt32(mstr, MotListHead) & Memory.PhysAddrMask, skinHead = (uint)BitConverter.ToInt32(mstr, MotionType.MotionSkinList) & Memory.PhysAddrMask;
            string heads = $"bone list 0x{boneHead:X}" + (Memory.IsValidGuest(boneHead) ? $" (w0 {Memory.ReadInt(Memory.ToMmu(boneHead))}, type {Memory.ReadInt(Memory.ToMmu(boneHead) + 8)}, keys {Memory.ReadInt(Memory.ToMmu(boneHead) + 0xC)})" : "")
                         + $", skin list 0x{skinHead:X}" + (Memory.IsValidGuest(skinHead) ? $" (mesh {Memory.ReadInt(Memory.ToMmu(skinHead))}, bone {Memory.ReadInt(Memory.ToMmu(skinHead) + 4)}, type {Memory.ReadInt(Memory.ToMmu(skinHead) + 8)}, keys {Memory.ReadInt(Memory.ToMmu(skinHead) + 0xC)})" : "");
            Log($"slot {Slot}: cat channel cloned (keys {KeyBase}..{KeyBase + KeyCount - 1}, KEY table 0x{keyTable:X}), FrameInf 0x{fiSize:X}; {heads}");
            return true;
        }
    }
}
