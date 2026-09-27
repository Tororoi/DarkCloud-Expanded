using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb's item model (dun/item/main_data/bakudan), loaded into the game's own item-model cash so that a
    /// copy of it can be hung and thrown (BombShot). The game loads an active item's model from the item menu — the two
    /// files into the menu's read buffer, then SetCashModel, which allocates, uploads the texture block and builds the
    /// frames — and that is exactly what is done here, from the mod: the files read off the ISO and written into the
    /// dungeon loader's read buffer (idle between floor loads; the menu's buffer exists only while a menu is open),
    /// SetCashModel called through the call-request cave. The cash is emptied on a
    /// floor change, so the root is checked before every use and reloaded when it is gone.</summary>
    internal static class BombModel
    {
        private const string Tag = "[BombModel] ";
        private static uint _root;                                 // the cash root last loaded (0 = none)
        private static int  _cash = -1;
        private static DateTime _lastTry;

        /// <summary>The bomb model's root frame (guest), loading it if the cash no longer holds it; 0 when it cannot be had.</summary>
        internal static uint Root()
        {
            uint self = Memory.ReadGuestPtr(ItemModels.ItemModelPtr);
            if (!Memory.IsValidGuest(self)) return 0;
            long m = Memory.ToMmu(self);
            if (_cash >= 0 && Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + _cash * 4) == _root
                && Memory.ReadInt(m + ItemModels.CashItemOffset + _cash * 4) == ItemModels.BombItemId && Memory.IsValidGuest(_root)) return _root;
            // Already in the cash (an active-item Bomb, or a load of ours the bookkeeping lost)?
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == ItemModels.BombItemId) { _root = r; _cash = i; PlaceTextures(); return r; }
            }
            if ((DateTime.UtcNow - _lastTry).TotalSeconds < 2) return 0;              // a failed load is not retried every tick
            _lastTry = DateTime.UtcNow;
            uint loaded = Load(self, m);
            if (loaded != 0) PlaceTextures();
            return loaded;
        }

        private static uint Load(uint self, long m)
        {
            byte[] mds = GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.mds");
            byte[] img = GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.img");
            if (mds == null || img == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "bakudan.mds/.img not readable from the ISO"); return 0; }
            // The files' home for the one frame the load takes: the dungeon loader's own read buffer (where every floor's packs
            // land; idle between loads), or the item menu's when a menu has one allocated. Neither is in use while play goes on.
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) buf = Memory.ReadGuestPtr(ItemModels.MenuBufferPtr);
            if (!Memory.IsValidGuest(buf) || mds.Length > ItemModels.MenuBufferImgOffset) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no read buffer (0x{buf:X}) or the model is too big ({mds.Length} B)"); return 0; }
            if (Player.CheckDunIsPausedOrMenu()) return 0;                                 // a menu may be reading into it
            Memory.WriteBytesBatch(Memory.ToMmu(buf), mds);
            Memory.WriteBytesBatch(Memory.ToMmu(buf) + ItemModels.MenuBufferImgOffset, img);
            if (!NativeCall.Invoke(ItemModels.SetCashModel, out _, self, (uint)ItemModels.BombItemId, buf, buf + (uint)ItemModels.MenuBufferImgOffset, (uint)img.Length))
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "SetCashModel was not called — is the call-request cave in this ISO?"); return 0; }
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == ItemModels.BombItemId)
                {
                    _root = r; _cash = i;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bomb model loaded into cash {i}: root 0x{r:X} (mds {mds.Length} B, img {img.Length} B)");
                    return r;
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "SetCashModel ran but no cash entry holds the bomb (the cash is full?)");
            return 0;
        }

        /// <summary>Forget the load (a floor change empties the cash). The texture window's reservation is handed back when the
        /// manager's cursor still stands on it; kept (and reused by the next load) when something was handed out below it.</summary>
        internal static void Forget()
        {
            ReleaseTextures(); _root = 0; _cash = -1; _placedRoot = 0;
            if (_winBase != 0 && Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor) == _winBase)
            { Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _cursorSaved); _winBase = 0; _winSize = 0; }
        }

        // ── THE TEXTURE'S HOME. SetCashModel loads the bomb's texture through the cash's block (0x38 + cash), and every block's
        // window is packed up from the same VRAM base — the enemy, effect and weapon blocks over the same pages; the last upload
        // before a draw wins. A copy drawn in the clone slot binds before that slot's own upload lands (its draw is in the packet
        // the GS reads first), and the apple shot's pass uploads the apple's own textures over those pages: the bomb sampled
        // whichever texture held them — the slingshot's atlas, the apple's. So, as the Divine Beast cat's textures: the entry's
        // TEX0 (its pixels and its CLUT) is moved to a window RESERVED above every block's top, taken off the manager's downward
        // cursor so nothing is ever handed out on top of it, and the same TEX0 word is patched in the model's own draw packet —
        // which every copy (BladeProp) and graft (the apple shot) draws from. Once uploaded there, the pages are the bomb's for good.
        private static uint _placedRoot;                                 // the root whose texture was placed (0 = none)
        private static uint _winBase, _winSize, _cursorSaved;            // the reserved window, and the cursor before it was taken
        private const uint  WindowAlign = 0x20;                          // 32 GS blocks = one 8 KB page

        private static void PlaceTextures()
        {
            if (_root == 0 || _cash < 0 || _placedRoot == _root) return;
            _placedRoot = _root;
            long blk = TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride;
            uint bBase = Memory.ReadUInt(blk + TextureManager.BlkBase), bTop = Memory.ReadUInt(blk + TextureManager.BlkTop);
            if (bTop <= bBase || bTop - bBase > 0x800) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cash block 0x{CashBlockBase + _cash:X} window 0x{bBase:X}..0x{bTop:X} — the texture stays where it is"); return; }
            uint size = bTop - bBase;
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            if (_winBase == 0 || _winSize < size || cursor > _winBase)   // no window yet, too small, or the manager began again (its cursor is back above it)
            {
                uint nb = (cursor - size) & ~(WindowAlign - 1);
                uint highest = 0;
                for (int b = 0; b < TextureManager.BlockCount; b++)
                    highest = Math.Max(highest, Memory.ReadUInt(TextureManager.Base + TextureManager.Blocks + (long)b * TextureManager.BlockStride + TextureManager.BlkTop));
                if (highest > nb) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"WARNING: a texture block tops at 0x{highest:X}, inside the bomb's window 0x{nb:X}..0x{nb + size:X}");
                _cursorSaved = cursor; _winBase = nb; _winSize = size;
                Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, nb);           // a real allocation: the next texture goes below it
            }
            // The entries: TBP (bits 0..13) and CBP (bits 37..50) each shifted only when inside the block's window (a CLUT can sit
            // elsewhere, and a shifted address outside the window wraps in the GS's 14 bits onto some other texture's page).
            var moves = new List<(ulong oldT, ulong newT)>();
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base + TextureManager.Count) + 1);
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                if (Memory.ReadUShort(e + TextureManager.EntryBlock) != CashBlockBase + _cash && !_bound.Contains(e)) continue;
                ulong t = (ulong)Memory.ReadUInt(e + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(e + TextureManager.EntryTex0 + 4) << 32);
                uint tbp = (uint)(t & TextureManager.Tex0AddrMask), cbp = (uint)((t >> TextureManager.Tex0CbpShift) & TextureManager.Tex0AddrMask);
                uint nt = tbp >= bBase && tbp < bTop ? _winBase + (tbp - bBase) : tbp;
                uint nc = cbp >= bBase && cbp < bTop ? _winBase + (cbp - bBase) : cbp;
                if (nt == tbp && nc == cbp) continue;                                        // already placed (a rediscovery), or not in the window
                ulong n = (t & ~(ulong)TextureManager.Tex0AddrMask & ~((ulong)TextureManager.Tex0AddrMask << TextureManager.Tex0CbpShift)) | nt | ((ulong)nc << TextureManager.Tex0CbpShift);
                Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)n); Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(n >> 32));
                moves.Add((t, n));
            }
            if (moves.Count == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"texture already in its window 0x{_winBase:X}..0x{_winBase + size:X}"); return; }
            // The model's own packet (and, for a skinned mesh, its MDT) carries the same register word: patched in place, so every
            // copy taken from it and every node grafted onto it draws from the window.
            int patched = 0, visuals = 0;
            foreach (uint node in Nodes(_root))
            {
                uint vis = Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr);
                if (!Memory.IsValidGuest(vis)) continue;
                visuals++;
                long v = Memory.ToMmu(vis);
                uint vu = Memory.ReadGuestPtr(v + CVisualMDT.VisVU); int vuSz = Memory.ReadInt(v + CVisualMDT.VisVU + 4) * 16;
                if (Memory.IsValidGuest(vu) && vuSz > 0 && vuSz < 0x40000) patched += Sweep(Memory.ToMmu(vu), vuSz, moves);
                if (Memory.ReadGuestPtr(v + CVisualMDT.VisVtable) == CVisualMDT.RigidVtable) continue;           // a rigid visual ends at 0x20
                uint vu2 = Memory.ReadGuestPtr(v + 0x2C);
                if (vu2 != vu && Memory.IsValidGuest(vu2) && vuSz > 0 && vuSz < 0x40000) patched += Sweep(Memory.ToMmu(vu2), vuSz, moves);
                uint mdt = Memory.ReadGuestPtr(v + CVisualMDT.VisMDT);
                if (Memory.IsValidGuest(mdt) && Memory.ReadUInt(Memory.ToMmu(mdt)) == CVisualMDT.MdtMagic)
                {
                    int mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + CVisualMDT.MdtSizeField);
                    if (mdtSz > 0 && mdtSz < 0x40000) patched += Sweep(Memory.ToMmu(mdt), mdtSz, moves);
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"texture moved 0x{bBase:X}..0x{bTop:X} -> 0x{_winBase:X}..0x{_winBase + size:X} (above every block; cursor 0x{_cursorSaved:X} -> 0x{_winBase:X}); {moves.Count} entr{(moves.Count == 1 ? "y" : "ies")}, {patched} register word(s) in {visuals} visual(s)");
        }

        /// <summary>Every 64-bit word in [addr, addr+size) equal to a moved TEX0 rewritten to its new value (4-byte steps).</summary>
        private static int Sweep(long addr, int size, List<(ulong oldT, ulong newT)> moves)
        {
            byte[] blk = Memory.ReadBytesBatch(addr, size);
            if (blk == null) return 0;
            int n = 0; bool dirty = false;
            for (int o = 0; o + 8 <= blk.Length; o += 4)
            {
                ulong w = BitConverter.ToUInt64(blk, o);
                foreach (var (oldT, newT) in moves)
                    if (w == oldT) { BitConverter.GetBytes(newT).CopyTo(blk, o); dirty = true; n++; o += 4; break; }
            }
            if (dirty) Memory.WriteBytesBatch(addr, blk);
            return n;
        }

        /// <summary>The frame tree under <paramref name="root"/>, root included (children first-child / next-sibling).</summary>
        private static IEnumerable<uint> Nodes(uint root)
        {
            var stack = new Stack<uint>(); stack.Push(root); int seen = 0;
            while (stack.Count > 0 && seen++ < 256)
            {
                uint n = stack.Pop();
                if (!Memory.IsValidGuest(n)) continue;
                yield return n;
                uint sib = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootSibling);
                if (n != root && Memory.IsValidGuest(sib)) stack.Push(sib);
                uint child = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild);
                if (Memory.IsValidGuest(child)) stack.Push(child);
            }
        }

        // ── THE COPY'S UPLOAD. A chara-slot copy (BladeProp) is drawn in the clone-weapon slot, and the apple shot in the main
        // effect's pass; each pass reloads ITS block before it draws, never the cash's. While a copy is up the bomb's entries are
        // RE-TAGGED into that pass's block, and the block is marked unloaded every tick so its uploader re-sends every entry —
        // ours among them (the trick the Sun Sword's disc uses to stay uploaded); with the window above, one upload is enough,
        // and the rest are cheap. Tagged back when the copy goes. The cash's own block is kept unloaded too, so the game's own
        // draw of a thrown Bomb (the same cash model) re-sends it there.
        private const int CashBlockBase = 0x38;
        internal const int WeaponPassBlock = 0x1D;                        // the clone slot's group under the per-chara formula (chara 3 → 0x11 + 12)
        internal const int MainEffectBlock = 0x10;                        // the main-character effect's (a borrowed shot's)
        private static readonly System.Collections.Generic.List<long> _bound = new System.Collections.Generic.List<long>();
        private static int _boundBlock;

        /// <summary>Every tick a copy of the bomb is drawn through <paramref name="block"/>'s pass: the bomb's entries tagged into
        /// that block, and the block re-sent.</summary>
        internal static void KeepTextures(int block = WeaponPassBlock)
        {
            if (_cash < 0) return;
            if (_bound.Count > 0 && _boundBlock != block) ReleaseTextures();
            if (_bound.Count == 0)
            {
                _boundBlock = block;
                int count = Memory.ReadInt(TextureManager.Base + TextureManager.Count) + 1;
                for (int i = 0; i < Math.Min(count, TextureManager.MaxEntries); i++)
                {
                    long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                    if (Memory.ReadUShort(e + TextureManager.EntryBlock) != CashBlockBase + _cash) continue;
                    Memory.WriteUShort(e + TextureManager.EntryBlock, (ushort)block);
                    _bound.Add(e);
                }
                if (_bound.Count > 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_bound.Count} texture(s) of the bomb tagged into block 0x{block:X} while a copy is drawn through it");
            }
            if (_bound.Count > 0) Memory.WriteUInt(TextureManager.Base + TextureManager.Blocks + (long)_boundBlock * TextureManager.BlockStride + TextureManager.BlkLoaded, 0);
            Tick();
        }
        /// <summary>Every tick the sphere is on: the cash's block re-sent whenever its own pass runs (a thrown Bomb drawn by the game).</summary>
        internal static void Tick()
        {
            if (_placedRoot == 0 || _cash < 0) return;
            Memory.WriteUInt(TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride + TextureManager.BlkLoaded, 0);
        }
        /// <summary>The bomb's entries back in the cash's own block.</summary>
        internal static void ReleaseTextures()
        {
            if (_bound.Count == 0) return;
            foreach (long e in _bound) Memory.WriteUShort(e + TextureManager.EntryBlock, (ushort)(CashBlockBase + _cash));
            _bound.Clear();
        }
    }

    /// <summary>The item bomb's blast VISUAL, by data — what SetBomb writes into a free CItemBombEffect slot (five sprites
    /// that spread and fade, sized by the slot's scale), the bomb's own sound, and the blast ring (CShockWave): the game
    /// spawns it for a blast bigger than 1× at 30 × scale across and 15 × scale high; <paramref name="ringRadius"/> sets
    /// its reach instead (0 = no ring, negative = the game's rule). Nothing of the bomb's collision: the damage is the
    /// caller's (BigBang.PlantFalloff).</summary>
    internal static class BombFx
    {
        internal static bool Spawn(float x, float h, float y, float scale, float ringRadius = -1f)
        {
            uint fx = Memory.ReadGuestPtr(ItemModels.BombEffectPtr);
            if (!Memory.IsValidGuest(fx)) return false;
            long b = 0;
            for (int s = 0; s < ItemModels.BombSlots && b == 0; s++)
            {
                long cand = Memory.ToMmu(fx) + s * ItemModels.BombSlotStride;
                bool free = true;
                for (int i = 0; i < 5; i++) if (Memory.ReadInt(cand + 0xA0 + i * 4) != 0) { free = false; break; }
                if (free) b = cand;
            }
            if (b == 0) return false;
            for (int i = 0; i < 5; i++)
            {
                Memory.WriteVec3 (b + i * 0x10, x, h, y);
                Memory.WriteFloat(b + i * 0x10 + 0xC, 1f);
                Memory.WriteInt  (b + 0x50 + i * 4, 0);
                Memory.WriteInt  (b + 0x64 + i * 4, i * -3);
                Memory.WriteFloat(b + 0x8C + i * 4, 128f);
                Memory.WriteFloat(b + 0x78 + i * 4, 20f);
                Memory.WriteInt  (b + 0xA0 + i * 4, 1);
            }
            Memory.WriteFloat(b + 0xB4, scale);
            Memory.WriteInt  (b + 0x50, 2);
            Memory.WriteInt  (b + 0x54, 1);
            SeSeq.Play(ItemModels.BombSe, 90);
            float ring = ringRadius < 0f ? (scale > 1f ? scale * 30f : 0f) : ringRadius;
            if (ring > 0f)
            {
                uint sw = Memory.ReadGuestPtr(ItemModels.ShockWavePtr);
                if (Memory.IsValidGuest(sw))
                {
                    long w = Memory.ToMmu(sw);
                    Memory.WriteVec3 (w, x, h, y); Memory.WriteFloat(w + 0xC, 1f);
                    Memory.WriteFloat(w + 0x10, ring); Memory.WriteFloat(w + 0x14, ring);
                    Memory.WriteInt  (w + 0x18, 0); Memory.WriteFloat(w + 0x1C, ring * 0.5f);
                    Memory.WriteInt  (w + 0x20, 0); Memory.WriteInt(w + 0x24, 0); Memory.WriteInt(w + 0x28, 1);
                }
            }
            return true;
        }
    }
}
