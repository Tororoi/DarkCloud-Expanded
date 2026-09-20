using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.DivineBeastCat;
using static Dark_Cloud_Improved_Version.CatCape;
using static Dark_Cloud_Improved_Version.CatFlight;
using static Dark_Cloud_Improved_Version.CatCopy;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The cat's textures: retagged from her block to the slot's group at a spawn and returned at a despawn, recreated or relocated when the block moves. One of the <see cref="DivineBeastCat"/> classes, which share their members through using static.</summary>
    internal static class CatTextures
    {
        // ─────────────────────────────────────────── textures ──────────────────────────────────────────────
        internal const short HerTextureBlock = 0x11;
        internal const short SlotTextureGroup = (short)(DungeonCharaDraw.CharaTexBase + Slot);
        // ⚠ EVERY texture the cat pack carries must be listed here. These are the entries re-tagged into the slot's group
        // and moved into the cat's VRAM window while the copy is up; one left out keeps her block's pages after that block
        // has been cut back, so it samples stale VRAM and draws as garbage, or not at all.
        internal static readonly string[] CatTextureNames = { "c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05", "catwing", "catcape", "catglowp" };   // catwing = the wings' flat white; catglowp = the ONE 8-bit glow disc every look now shares (its palette row carries the colour)
        // The re-tagged entries keep the VRAM addresses they were given in HER window, so slot 1's block takes over that
        // tail of her window (and hers shrinks to just before it) while the copy is up; the cat reserves its window by
        // moving TextureManager.Cursor.
        private static uint _texCursorSaved, _texCursorTaken;
        private static uint _herTopSaved;
        internal static int _texCheckTick;
        /// <summary>A dungeon script event or a menu can rebuild the texture manager under the resident copy: the cat's
        /// entries come back at their vanilla addresses (or vanish) while the copy's packets still name the relocated
        /// ones — garbled fur until a rebuild. Notice it and tear the copy down; it re-spawns clean after the 1 s gate.</summary>
        internal static void CheckTexturesStillOurs()
        {
            long e = FindTexEntry(CatTextureNames[0]);
            uint tbp = e == 0 ? 0u : (Memory.ReadUInt(e + TextureManager.EntryTex0) & TextureManager.Tex0AddrMask);
            if (e != 0 && tbp >= StuckFloor && Memory.ReadShort(e) == SlotTextureGroup) return;   // still relocated and ours
            Log((e == 0 ? "texture manager rebuilt (cat entries gone)" : $"texture manager rebuilt (cat entry back at 0x{tbp:X}, block 0x{Memory.ReadShort(e):X})") + " — rebuilding the copy");
            _texMoved.Clear();                                                   // nothing of ours is in there to restore
            Despawn();
        }

        /// <summary>Re-tag the cat's entries (<see cref="CatTextureNames"/>) into the slot's texture group while the copy is
        /// up, and hand them back on despawn. The dungeon draw loop re-uploads group 0x20+slot to VRAM right before it draws
        /// chara slot i (ReloadTexture 0x133070 uploads every entry tagged with that block); the cat's textures sit in HER
        /// block, whose VRAM pages are gone by then because the upload window is paged and shared, so the copy would sample
        /// whatever replaced them. With <see cref="MoveCatVram"/> on, the entries are also relocated into a reserved window
        /// above every other block's top.</summary>
        internal static void RetagCatTextures(short from, short to)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base));
            int done = 0; uint minTbp = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                if (Memory.ReadShort(e) != from) continue;
                byte[] nb = Memory.ReadBytesBatch(e + TextureManager.EntryName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                string nm = System.Text.Encoding.ASCII.GetString(nb, 0, len);
                if (Array.IndexOf(CatTextureNames, nm) < 0) continue;
                if (to == SlotTextureGroup && (Memory.ReadUInt(e + TextureManager.EntryTex0) & TextureManager.Tex0AddrMask) < StuckFloor)
                {
                    byte[] snap = Memory.ReadBytesBatch(e, TextureManager.EntryStride);           // the whole entry at rest: block, name, image pointers, TEX0
                    if (snap != null) _texSnapshot[nm] = snap;                   // a script event's slot clean-up wipes it; RecreateCatEntries puts it back
                }
                Memory.WriteUShort(e, (ushort)to);
                if (to == SlotTextureGroup)
                {
                    // An entry still sitting in the relocation window is one an earlier despawn failed to put back.
                    // Put its remembered original back first.
                    ulong t = (ulong)Memory.ReadUInt(e + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(e + TextureManager.EntryTex0 + 4) << 32);
                    uint tbp = (uint)(t & TextureManager.Tex0AddrMask);
                    if (tbp >= StuckFloor)
                    {
                        if (_texOriginal.TryGetValue(nm, out ulong orig))
                        {
                            Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)orig); Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(orig >> 32));
                            Log($"texture {nm} was left at 0x{tbp:X} by an earlier despawn — restored to 0x{orig & TextureManager.Tex0AddrMask:X}");
                            t = orig; tbp = (uint)(t & TextureManager.Tex0AddrMask);
                        }
                        else Log($"WARNING: texture {nm} sits at 0x{tbp:X} with no remembered original — its relocation will be wrong this spawn");
                    }
                    else _texOriginal[nm] = t;
                    minTbp = Math.Min(minTbp, tbp);
                }
                done++;
            }
            long her = TextureManager.Base + TextureManager.Blocks + (long)HerTextureBlock * TextureManager.BlockStride;
            long grp = TextureManager.Base + TextureManager.Blocks + (long)SlotTextureGroup * TextureManager.BlockStride;
            if (to == SlotTextureGroup && done > 0 && minTbp != uint.MaxValue && MoveCatVram)
            {
                // The slot loop's group reload is written into the main frame packet, but the slot's draw goes
                // into the chara packet the GS consumes EARLIER in the frame, so the cat's binds precede its own upload and
                // another block can overwrite the pages in between. So the cat's textures live where nothing else uploads: the
                // top of the manager's VRAM range, above every block's top. Entries, the copy's packet buffers and MDT get the
                // new addresses; hers are untouched.
                _herTopSaved = Memory.ReadUInt(her + TextureManager.BlkTop);
                uint size = _herTopSaved - minTbp;
                uint limit = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
                uint newBase = (limit - size) & ~0x1Fu;
                uint highest = 0;
                for (int b = 0; b < 0x48; b++) highest = Math.Max(highest, Memory.ReadUInt(TextureManager.Base + TextureManager.Blocks + (long)b * TextureManager.BlockStride + TextureManager.BlkTop));
                if (highest > newBase) Log($"WARNING: a texture block tops at 0x{highest:X}, inside the cat's window 0x{newBase:X}..0x{newBase + size:X}");
                // RESERVE the window instead of squatting under the cursor. manager+0x14 is a DOWNWARD bump allocator —
                // EnterFixTexture does `lw v0,0x14(s6); subu v0,v0,size; sw v0,0x14(s6)` (0x132354) — so the space just below
                // it is precisely what the game hands out NEXT. Taking the window without moving the cursor meant any texture
                // entered afterwards would land on top of the cat's — fonts are entered that way. Moving the cursor down by
                // the same amount makes this a real allocation.
                _texCursorSaved = limit; _texCursorTaken = newBase;
                Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, newBase);
                int patched = RelocateCatTextures(minTbp, newBase);
                Memory.WriteUInt(grp + TextureManager.BlkBase, newBase);
                Memory.WriteUInt(grp + TextureManager.BlkTop, newBase + size);
                Memory.WriteUInt(grp + TextureManager.BlkLoaded, 0);
                Memory.WriteUInt(grp + TextureManager.BlkDirty, 0);
                Memory.WriteUInt(her + TextureManager.BlkTop, minTbp);
                Log($"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X} and moved 0x{minTbp:X}..0x{_herTopSaved:X} → 0x{newBase:X}..0x{newBase + size:X} ({patched} block(s) swept); her block now tops at 0x{minTbp:X}");
            }
            else if (to == HerTextureBlock)
            {
                if (_texMoved.Count > 0) RelocateCatTextures(0, 0);          // back to their original addresses
                if (_texCursorTaken != 0)                                    // give the reservation back, if nobody built below it
                {
                    uint cur = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
                    if (cur == _texCursorTaken) Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _texCursorSaved);
                    else Log($"texture cursor moved to 0x{cur:X} under our reservation (0x{_texCursorTaken:X}) — leaving it, the window stays reserved");
                    _texCursorTaken = 0; _texCursorSaved = 0;
                }
                if (_herTopSaved != 0) Memory.WriteUInt(her + TextureManager.BlkTop, _herTopSaved);
                Memory.WriteUInt(grp + TextureManager.BlkBase, 0);
                Memory.WriteUInt(grp + TextureManager.BlkTop, 0);
                Memory.WriteUInt(grp + TextureManager.BlkLoaded, 1);
                Memory.WriteUInt(her + TextureManager.BlkLoaded, 0);                          // she re-uploads her whole window next frame
                Log($"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X}; her block restored to top 0x{_herTopSaved:X}");
            }
            else Log($"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X}");
            BuildStep("textures re-tagged");
        }

        // (texture NAME, original tex0) for every cat texture moved this spawn — restored on despawn by looking the
        // entry up by name again (entry addresses shift when the manager registers or drops textures meanwhile).
        private static readonly List<(string name, ulong tex0)> _texMoved = new();
        // name → TEX0 at rest, refreshed at every sane spawn; repairs an entry a failed restore left relocated.
        private static readonly Dictionary<string, ulong> _texOriginal = new();
        private static readonly Dictionary<string, byte[]> _texSnapshot = new();   // name → the manager entry (0x50 B) as it sits in her block
        internal static bool _texDeferLogged;

        /// <summary>A script event's clean-up (EdEventAllClear 0x197810 → DeleteTextureBlock, which zeroes every entry of
        /// a block id) wipes the cat entries while they are tagged to the copy's slot group. Put the remembered entries back
        /// into free manager rows (first empty name from row 1, as SearchTexture 0x131320 allocates) — the image data they
        /// point at is her pack's own IMG bank, still loaded — so the normal re-tag/relocate can run. Returns how many of
        /// the cat's entries the manager now holds.</summary>
        internal static int RecreateCatEntries()
        {
            int present = 0, made = 0;
            foreach (string nm in CatTextureNames)
            {
                if (FindTexEntry(nm) != 0) { present++; continue; }
                if (!_texSnapshot.TryGetValue(nm, out byte[] snap)) continue;
                int idx = -1;
                for (int i = 1; i < TextureManager.MaxEntries; i++)
                    if (Memory.ReadByte(TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride + TextureManager.EntryName) == 0) { idx = i; break; }
                if (idx < 0) { Log("texture manager full — cannot recreate " + nm); break; }
                Memory.WriteBytesBatch(TextureManager.Base + TextureManager.Entries + (long)idx * TextureManager.EntryStride, snap);
                if (idx + 1 > Memory.ReadInt(TextureManager.Base)) Memory.WriteInt(TextureManager.Base, idx + 1);
                present++; made++;
            }
            if (made > 0) Log($"cat textures recreated in the manager ({made} put back, {present} of {CatTextureNames.Length} present) after a script event wiped them");
            return present;
        }

        /// <summary>Whether the cat's textures are RELOCATED to their own VRAM window, as opposed to just being re-tagged into
        /// the slot's group where they sit. The move exists so nothing else uploads over the cat's pages mid-frame. It is also
        /// the only thing the mod does that writes VRAM addresses at all. Left ON — without the move the cat's textures are
        /// unreliable.</summary>
        private const bool MoveCatVram = true;

        private const uint StuckFloor = 0x3000;          // no vanilla block reaches this high (max seen 0x3920 is the manager's own top area)

        /// <summary>The manager entry for a cat texture, found by name (0 if absent).</summary>
        internal static long FindTexEntry(string name)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base));
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                byte[] nb = Memory.ReadBytesBatch(e + TextureManager.EntryName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(nb, 0, len) == name) return e;
            }
            return 0;
        }

        /// <summary>Move the cat entries' TEX0 (TBP0 bits 0..13, CBP bits 37..50) by newBase − oldBase, and patch
        /// the identical register words wherever they sit in the copy's own packet buffers and MDT copy (the
        /// packet is rebuilt from the MDT every frame). With oldBase == 0 the saved originals are put back.</summary>
        private static int RelocateCatTextures(uint oldBase, uint newBase)
        {
            var moves = new List<(ulong oldT, ulong newT)>();
            if (oldBase == 0 && newBase == 0)
            {
                foreach (var (name, tex0) in _texMoved)
                {
                    long entry = FindTexEntry(name);
                    if (entry == 0) { Log($"WARNING: texture {name} is gone from the manager — nothing to restore"); continue; }
                    ulong cur = (ulong)Memory.ReadUInt(entry + 0x28) | ((ulong)Memory.ReadUInt(entry + 0x2C) << 32);
                    moves.Add((cur, tex0));
                    Memory.WriteUInt(entry + 0x28, (uint)tex0); Memory.WriteUInt(entry + 0x2C, (uint)(tex0 >> 32));
                }
                _texMoved.Clear();
            }
            else
            {
                foreach (string name in CatTextureNames)                     // the cat's textures only, by name
                {
                    long e = FindTexEntry(name);
                    if (e == 0) continue;
                    ulong t = (ulong)Memory.ReadUInt(e + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(e + TextureManager.EntryTex0 + 4) << 32);
                    uint tbp = (uint)(t & TextureManager.Tex0AddrMask), cbp = (uint)((t >> TextureManager.Tex0CbpShift) & TextureManager.Tex0AddrMask);
                    // Shift a field ONLY if it is inside the window being moved. A TEX0 carries the texture's page AND its
                    // CLUT's, and a CLUT can sit below the textures: `newBase + (cbp - oldBase)` then underflows, and the
                    // GS keeps 14 bits of it, so the CLUT is uploaded to an essentially arbitrary page. A cat CLUT at 0x0E60
                    // lands exactly on the message font at 0x2BC0, and one at 0x1060 on the font's own CLUT at 0x2DC0 —
                    // a 1 KB band of cape colour dropped across the glyph atlas.
                    uint nt = (tbp >= oldBase && tbp < _herTopSaved) ? newBase + (tbp - oldBase) : tbp;
                    uint nc = (cbp >= oldBase && cbp < _herTopSaved) ? newBase + (cbp - oldBase) : cbp;
                    if (nc == cbp && nt != tbp) Log($"texture {name}: CLUT 0x{cbp:X} is outside the moved window 0x{oldBase:X}..0x{_herTopSaved:X} — left where it is");
                    ulong n = (t & ~(ulong)TextureManager.Tex0AddrMask & ~((ulong)TextureManager.Tex0AddrMask << TextureManager.Tex0CbpShift)) | nt | ((ulong)nc << TextureManager.Tex0CbpShift);
                    _texMoved.Add((name, t));
                    moves.Add((t, n));
                    Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)n); Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(n >> 32));
                }
            }
            int patched = 0;
            var blocks = new List<(long addr, int size)>();
            foreach (var (node, mdt, mdtSz, vu, vu2, vuSz) in _skinNodes)
                blocks.AddRange(new[] { (mdt, mdtSz), (vu, vuSz), (vu2, vuSz) });
            // Rigid cat meshes (the bell) were not copied — their packet is shared with her hidden cat, which is
            // never drawn, so patching it in place is harmless (and undone with the entries on despawn).
            for (int i = 0; i < _nodeCount; i++)
            {
                if (_skinNodes.Exists(sn => sn.node == i)) continue;
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                uint vis = Memory.ReadGuestPtr(node + CFrameVu1.GeomPtr);
                if (!Memory.IsValidGuest(vis)) continue;
                uint vu = Memory.ReadGuestPtr(Memory.ToMmu(vis) + CVisualMDT.VisVU);
                int vuSz = Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU + 4) * 16;
                if (Memory.IsValidGuest(vu) && vuSz > 0 && vuSz < 0x40000) blocks.Add((Memory.ToMmu(vu), vuSz));
                uint vuB = Memory.ReadGuestPtr(Memory.ToMmu(vis) + 0x2C);
                if (vuB != vu && Memory.IsValidGuest(vuB) && vuSz > 0) blocks.Add((Memory.ToMmu(vuB), vuSz));
            }
            // The machine does the hunting. Every one of these blocks is a draw packet the copy just placed, and scanning them
            // from here would mean reading all 300 KB back over PINE. One find/replace job per block per moved texture;
            // the cave sweeps them all in a frame.
            _jobs.Clear(); _pairs.Clear();
            if (moves.Count > CodeCaves.CatCopyMaxPairs)   // never truncate: a dropped pair leaves a texture pointing at nothing
            {
                Log($"texture relocation: {moves.Count} moves exceed the sweep table ({CodeCaves.CatCopyMaxPairs}) — using the slow path");
                foreach (var (addr, size) in blocks)
                {
                    if (addr == 0 || size <= 0) continue;
                    byte[] blk = Memory.ReadBytesBatch(addr, size);
                    if (blk == null) continue;
                    bool dirty = false;
                    for (int o = 0; o + 8 <= blk.Length; o += 4)
                    {
                        ulong w = BitConverter.ToUInt64(blk, o);
                        foreach (var (oldT, newT) in moves)
                            if (w == oldT) { BitConverter.GetBytes(newT).CopyTo(blk, o); dirty = true; patched++; o += 4; break; }
                    }
                    if (dirty) Memory.WriteBytesBatch(addr, blk);
                }
                return patched;
            }
            _pairs.AddRange(moves);
            foreach (var (addr, size) in blocks)
                if (addr != 0 && size > 0) _jobs.Add(new CopyJob(Memory.ToGuest(addr), size));
            patched = _jobs.Count;
            if (_jobs.Count > 0 && !RunCopyJobs() && !CopyJobsBySocket())
                Log("texture relocation: neither path completed — the copy may draw with her texture block");
            _jobs.Clear();
            return patched;
        }
    }
}
