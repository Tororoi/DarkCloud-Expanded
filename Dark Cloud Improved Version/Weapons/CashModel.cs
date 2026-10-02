using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A model loaded into the game's own item-model cash so that a copy of it can be drawn (BladeProp) or grafted:
    /// the Bomb (BombModel), Muska Lacka's palm (PalmModel). The game loads an active item's model from the item menu — the
    /// two files into the menu's read buffer, then SetCashModel, which allocates, uploads the texture block and builds the
    /// frames — and that is exactly what is done here, from the mod: the files written into the dungeon loader's read buffer
    /// (idle between floor loads; the menu's buffer exists only while a menu is open), SetCashModel called through the
    /// call-request cave. SetCashModel only stores the item id as the entry's label, so a model of the mod's own takes a
    /// label no item has. Each cash entry's allocator is 0x9C5 units (40,016 B, GameInit): the model, its built frames and a
    /// copy of the texture bank must fit it. The cash is emptied on a floor change, so the root is checked before every use
    /// and reloaded when it is gone.</summary>
    internal sealed class CashModel
    {
        private readonly string Tag, _what;
        private readonly int _key;                                  // the cash entry's item-id label
        private readonly Func<(byte[] mds, byte[] img)> _files;     // the model's two files (null when unreadable)

        internal CashModel(string tag, int itemKey, string what, Func<(byte[] mds, byte[] img)> files)
        { Tag = tag; _key = itemKey; _what = what; _files = files; }

        /// <summary>A texture too big for the cash entry (whose allocator holds the texture bank's copy): the model is loaded with a
        /// small stand-in of the same name, then that entry is pointed at the full picture, kept outside the cash (see
        /// <see cref="ApplyFullTexture"/>). Name, width, height (powers of two), 8-bit row-major pixels, and the 1 KB CLUT.</summary>
        internal Func<(string name, int w, int h, byte[] pixels, byte[] clut)> FullTexture;

        private uint _root;                                 // the cash root last loaded (0 = none)
        private int  _cash = -1;
        private DateTime _lastTry;

        /// <summary>The model's root frame (guest), loading it if the cash no longer holds it; 0 when it cannot be had.</summary>
        internal uint Root()
        {
            uint self = Memory.ReadGuestPtr(ItemModels.ItemModelPtr);
            if (!Memory.IsValidGuest(self)) return 0;
            long m = Memory.ToMmu(self);
            if (_cash >= 0 && Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + _cash * 4) == _root
                && Memory.ReadInt(m + ItemModels.CashItemOffset + _cash * 4) == _key && Memory.IsValidGuest(_root)) return _root;
            // Already in the cash (an active-item Bomb, or a load of ours the bookkeeping lost)?
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == _key) { _root = r; _cash = i; ApplyFullTexture(); PlaceTextures(); return r; }
            }
            if ((DateTime.UtcNow - _lastTry).TotalSeconds < 2) return 0;              // a failed load is not retried every tick
            _lastTry = DateTime.UtcNow;
            uint loaded = Load(self, m);
            if (loaded != 0) { ApplyFullTexture(); PlaceTextures(); }
            return loaded;
        }

        private uint Load(uint self, long m)
        {
            var (mds, img) = _files();
            if (mds == null || img == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {_what} model's files are not readable from the ISO"); return 0; }
            // The files' home for the one frame the load takes: the dungeon loader's own read buffer (where every floor's packs
            // land; idle between loads), or the item menu's when a menu has one allocated. Neither is in use while play goes on.
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) buf = Memory.ReadGuestPtr(ItemModels.MenuBufferPtr);
            if (!Memory.IsValidGuest(buf) || mds.Length > ItemModels.MenuBufferImgOffset) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no read buffer (0x{buf:X}) or the model is too big ({mds.Length} B)"); return 0; }
            if (Player.CheckDunIsPausedOrMenu()) return 0;                                 // a menu may be reading into it
            Memory.WriteBytesBatch(Memory.ToMmu(buf), mds);
            Memory.WriteBytesBatch(Memory.ToMmu(buf) + ItemModels.MenuBufferImgOffset, img);
            if (!NativeCall.Invoke(ItemModels.SetCashModel, out _, self, (uint)_key, buf, buf + (uint)ItemModels.MenuBufferImgOffset, (uint)img.Length))
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "SetCashModel was not called — is the call-request cave in this ISO?"); return 0; }
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == _key)
                {
                    _root = r; _cash = i;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_what} model loaded into cash {i}: root 0x{r:X} (mds {mds.Length} B, img {img.Length} B)");
                    return r;
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"SetCashModel ran but no cash entry holds the {_what} (the cash is full?)");
            return 0;
        }

        /// <summary>A SHADOW model of <paramref name="mds"/> (LoadMDSFile kind 8: CVisualShadow visuals, what MGDrawShadowFast
        /// draws) in this model's own cash allocator, once per load of the model; 0 when the model is not loaded or the
        /// allocator lacks <paramref name="needUnits"/> free (an overrun would hang the game).</summary>
        internal uint ShadowRoot(byte[] mds, int needUnits)
        {
            if (Root() == 0) return 0;
            if (_shadowFor == _root) return _shadow;
            _shadowFor = _root; _shadow = 0;
            long alloc = ItemModels.CashAllocBase + (long)_cash * ItemModels.CashAllocStride;
            int used = Memory.ReadInt(alloc + ItemModels.CashAllocUsed), cap = Memory.ReadInt(alloc + ItemModels.CashAllocCap);
            if (cap - used < needUnits) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no room for the {_what}'s shadow in cash {_cash} ({cap - used} of {needUnits} units free)"); return 0; }
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) buf = Memory.ReadGuestPtr(ItemModels.MenuBufferPtr);
            if (!Memory.IsValidGuest(buf) || mds.Length > ItemModels.MenuBufferImgOffset || Player.CheckDunIsPausedOrMenu()) { _shadowFor = 0; return 0; }
            Memory.WriteBytesBatch(Memory.ToMmu(buf), mds);
            if (!NativeCall.Invoke(ItemModels.LoadMDSFile, out uint root, buf, (uint)(alloc - 0x20000000L), ItemModels.MdsKindShadow, 0, 0, timeoutMs: 1500) || !Memory.IsValidGuest(root))
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {_what}'s shadow model did not load (0x{root:X})"); _shadowFor = 0; return 0; }
            _shadow = root;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_what} shadow model loaded into cash {_cash}: root 0x{root:X}, allocator {used} → {Memory.ReadInt(alloc + ItemModels.CashAllocUsed)} of {cap} units");
            return root;
        }
        private uint _shadow, _shadowFor;

        /// <summary>Forget the load (a floor change empties the cash). The texture window's reservation is handed back when the
        /// manager's cursor still stands on it; kept (and reused by the next load) when something was handed out below it.</summary>
        internal void Forget()
        {
            ReleaseTextures(); _root = 0; _cash = -1; _placedRoot = 0; _shadow = 0; _shadowFor = 0; _fullFor = 0; _fullPixels = 0; _windowMin = 0;
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
        private uint _placedRoot;                                 // the root whose texture was placed (0 = none)
        private uint _winBase, _winSize, _cursorSaved;            // the reserved window, and the cursor before it was taken
        private const uint  WindowAlign = 0x20;                          // 32 GS blocks = one 8 KB page

        private void PlaceTextures()
        {
            if (_root == 0 || _cash < 0 || _placedRoot == _root) return;
            _placedRoot = _root;
            long blk = TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride;
            uint bBase = Memory.ReadUInt(blk + TextureManager.BlkBase), bTop = Memory.ReadUInt(blk + TextureManager.BlkTop);
            if (bTop > bBase && bTop - bBase < _windowMin) bTop = bBase + _windowMin;          // the full texture runs past the stand-in's block
            if (bTop <= bBase || bTop - bBase > 0x800) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cash block 0x{CashBlockBase + _cash:X} window 0x{bBase:X}..0x{bTop:X} — the texture stays where it is"); return; }
            uint size = bTop - bBase;
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            if (_winBase == 0 || _winSize < size || cursor > _winBase)   // no window yet, too small, or the manager began again (its cursor is back above it)
            {
                uint nb = (cursor - size) & ~(WindowAlign - 1);
                uint highest = 0;
                for (int b = 0; b < TextureManager.BlockCount; b++)
                    highest = Math.Max(highest, Memory.ReadUInt(TextureManager.Base + TextureManager.Blocks + (long)b * TextureManager.BlockStride + TextureManager.BlkTop));
                if (highest > nb) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"WARNING: a texture block tops at 0x{highest:X}, inside the {_what}'s window 0x{nb:X}..0x{nb + size:X}");
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
            var (patched, visuals) = SweepModel(moves);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"texture moved 0x{bBase:X}..0x{bTop:X} -> 0x{_winBase:X}..0x{_winBase + size:X} (above every block; cursor 0x{_cursorSaved:X} -> 0x{_winBase:X}); {moves.Count} entr{(moves.Count == 1 ? "y" : "ies")}, {patched} register word(s) in {visuals} visual(s)");
        }

        /// <summary>Every visual of the model (its VU packet, a skinned one's second packet and MDT) swept for the moved TEX0 words.</summary>
        private (int patched, int visuals) SweepModel(List<(ulong oldT, ulong newT)> moves)
        {
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
            return (patched, visuals);
        }

        // ── THE FULL TEXTURE, OUTSIDE THE CASH. The texture manager uploads an entry (ReloadTexture 0x133070) from its own fields:
        // width +2, height +4, bytes per texel +6 (u16), the level-0 pixels at +0x38 (mipmaps +0x3C…, 0 here), the CLUT at +0x48,
        // a swizzled flag at +0x4C, and its VRAM placement from the TEX0 word (+0x28: TBP, TBW bits 14–19, TW 26–29, TH 30–33, CBP
        // from bit 37). So the stand-in's entry is re-pointed at the full picture — kept high in the dungeon loader's read buffer
        // (about 4 MB, idle through a floor; the cash loads use only its first 0xFA10 + an image) — and its sizes and TEX0
        // rewritten, CBP just past the pixels; the model's packet gets the same TEX0 (swept), and PlaceTextures then reserves a
        // window that big above every block. Every tick a copy is drawn the data is checked and re-written if anything overwrote it.
        private const int FullOffset = 0x300000;                  // from the read buffer's start
        private uint _fullFor, _fullPixels, _fullClut, _windowMin;
        private byte[] _fullPx, _fullCl;

        private void ApplyFullTexture()
        {
            if (FullTexture == null || _root == 0 || _cash < 0 || _fullFor == _root) return;
            _fullFor = _root;
            var (name, w, h, px, clut) = FullTexture();
            if (px == null || clut == null || px.Length != w * h || clut.Length != 0x400) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no full texture to apply — the stand-in stays"); return; }
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no read buffer for the full texture — the stand-in stays"); return; }
            long e = 0;
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base + TextureManager.Count) + 1);
            for (int i = 0; i < count && e == 0; i++)
            {
                long c = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                if (Memory.ReadUShort(c + TextureManager.EntryBlock) != CashBlockBase + _cash) continue;
                byte[] nb = Memory.ReadBytesBatch(c + TextureManager.EntryName, 16);
                if (nb != null && System.Text.Encoding.ASCII.GetString(nb).Split('\0')[0] == name) e = c;
            }
            if (e == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"texture `{name}` not found in cash block 0x{CashBlockBase + _cash:X} — the stand-in stays"); return; }
            _fullPx = px; _fullCl = clut;
            _fullPixels = buf + FullOffset; _fullClut = _fullPixels + (uint)px.Length;
            WriteFullData();
            ulong t = (ulong)Memory.ReadUInt(e + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(e + TextureManager.EntryTex0 + 4) << 32);
            uint tbp = (uint)(t & TextureManager.Tex0AddrMask), pixBlocks = (uint)(w * h) >> 8;   // 256-byte GS blocks
            int lw = 0, lh = 0; while ((1 << lw) < w) lw++; while ((1 << lh) < h) lh++;
            ulong n = t & ~((0x3FUL << 14) | (0xFUL << 26) | (0xFUL << 30) | ((ulong)TextureManager.Tex0AddrMask << TextureManager.Tex0CbpShift));
            n |= ((ulong)Math.Max(1, w / 64) << 14) | ((ulong)lw << 26) | ((ulong)lh << 30) | ((ulong)((tbp + pixBlocks) & TextureManager.Tex0AddrMask) << TextureManager.Tex0CbpShift);
            Memory.WriteUShort(e + 2, (ushort)w);
            Memory.WriteUShort(e + 4, (ushort)h);
            Memory.WriteUInt(e + TextureManager.EntryPixels, _fullPixels);
            for (int lv = 1; lv < 4; lv++) Memory.WriteUInt(e + TextureManager.EntryPixels + lv * 4, 0);
            Memory.WriteUInt(e + TextureManager.EntryClut, _fullClut);
            Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)n); Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(n >> 32));
            var (patched, visuals) = SweepModel(new List<(ulong, ulong)> { (t, n) });
            _windowMin = (pixBlocks + 4 + WindowAlign - 1) & ~(WindowAlign - 1);                 // the pixels and the CLUT, page-aligned
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"full {w}×{h} `{name}` applied: pixels 0x{_fullPixels:X}, CLUT 0x{_fullClut:X} (read buffer +0x{FullOffset:X}); TEX0 0x{t:X} → 0x{n:X}, {patched} register word(s) in {visuals} visual(s); window 0x{_windowMin:X} blocks");
        }

        private void WriteFullData()
        {
            Memory.WriteBytesBatch(Memory.ToMmu(_fullPixels), _fullPx);
            Memory.WriteBytesBatch(Memory.ToMmu(_fullClut), _fullCl);
        }

        /// <summary>The full texture's data still where the entry points (its first and last 16 bytes and the CLUT's first 16); re-written
        /// when something has overwritten it.</summary>
        private void CheckFullData()
        {
            if (_fullPixels == 0 || _fullPx == null) return;
            byte[] a = Memory.ReadBytesBatch(Memory.ToMmu(_fullPixels), 16), z = Memory.ReadBytesBatch(Memory.ToMmu(_fullPixels) + _fullPx.Length - 16, 16), c = Memory.ReadBytesBatch(Memory.ToMmu(_fullClut), 16);
            if (a == null || z == null || c == null) return;
            bool ok = a.AsSpan().SequenceEqual(_fullPx.AsSpan(0, 16)) && z.AsSpan().SequenceEqual(_fullPx.AsSpan(_fullPx.Length - 16)) && c.AsSpan().SequenceEqual(_fullCl.AsSpan(0, 16));
            if (ok) return;
            WriteFullData();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the full texture's data was overwritten in the read buffer — written again");
        }

        /// <summary>Every 64-bit word in [addr, addr+size) equal to a moved TEX0 rewritten to its new value (4-byte steps).</summary>
        private int Sweep(long addr, int size, List<(ulong oldT, ulong newT)> moves)
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
        private readonly System.Collections.Generic.List<long> _bound = new System.Collections.Generic.List<long>();
        private int _boundBlock;

        /// <summary>Every tick a copy of the model is drawn through <paramref name="block"/>'s pass: its entries tagged into
        /// that block, and the block re-sent.</summary>
        internal void KeepTextures(int block = WeaponPassBlock)
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
                if (_bound.Count > 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_bound.Count} texture(s) of the {_what} tagged into block 0x{block:X} while a copy is drawn through it");
            }
            if (_bound.Count > 0) Memory.WriteUInt(TextureManager.Base + TextureManager.Blocks + (long)_boundBlock * TextureManager.BlockStride + TextureManager.BlkLoaded, 0);
            CheckFullData();
            Tick();
        }
        /// <summary>Every tick the sphere is on: the cash's block re-sent whenever its own pass runs (a thrown Bomb drawn by the game).</summary>
        internal void Tick()
        {
            if (_placedRoot == 0 || _cash < 0) return;
            Memory.WriteUInt(TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride + TextureManager.BlkLoaded, 0);
        }
        /// <summary>The model's entries back in the cash's own block.</summary>
        internal void ReleaseTextures()
        {
            if (_bound.Count == 0) return;
            foreach (long e in _bound) Memory.WriteUShort(e + TextureManager.EntryBlock, (ushort)(CashBlockBase + _cash));
            _bound.Clear();
        }

        /// <summary>One 8-bit TIM2 picture resampled to <paramref name="n"/>² (nearest texel: 8-bit indices cannot be blended), row-major: the header kept but for its sizes, the CLUT kept whole.
        /// TIM2 picture header (after the 16-byte file header): total +0 (header + 4 × image size in every file of this game), CLUT size +4, image size +8, header size +0xC (u16),
        /// image type +0x13 (5 = 8-bit), width +0x14, height +0x16 (u16); the pixels follow the header, the CLUT the pixels.</summary>
        /// <summary>An 8-bit TIM2 picture whole: its size, its pixels row-major (un-swizzled from the GS block order when
        /// <paramref name="swizzled"/>), and its CLUT bytes as the file has them.</summary>
        internal static (int w, int h, byte[] pixels, byte[] clut) ReadTim8(byte[] tim, bool swizzled)
        {
            const int pic = 0x10;
            if (tim.Length < pic + 0x30 || tim[0] != 'T' || tim[1] != 'I' || tim[2] != 'M' || tim[3] != '2') throw new System.IO.IOException("not a TIM2 picture");
            int clutSz = (int)IsoBytes.U32(tim, pic + 4), imgSz = (int)IsoBytes.U32(tim, pic + 8), hdrSz = IsoBytes.U16(tim, pic + 0xC);
            int w = IsoBytes.U16(tim, pic + 0x14), h = IsoBytes.U16(tim, pic + 0x16);
            if (tim[pic + 0x13] != 5 || imgSz != w * h) throw new System.IO.IOException($"not an 8-bit picture ({w}×{h}, {imgSz} B)");
            byte[] px = tim.AsSpan(pic + hdrSz, imgSz).ToArray();
            if (swizzled) px = Unswizzle8(px, w, h);
            return (w, h, px, tim.AsSpan(pic + hdrSz + imgSz, clutSz).ToArray());
        }

        internal static byte[] ResampleTim8(byte[] tim, bool swizzled, int n)
        {
            const int pic = 0x10;
            if (tim.Length < pic + 0x30 || tim[0] != 'T' || tim[1] != 'I' || tim[2] != 'M' || tim[3] != '2') throw new System.IO.IOException("not a TIM2 picture");
            int clutSz = (int)IsoBytes.U32(tim, pic + 4), imgSz = (int)IsoBytes.U32(tim, pic + 8), hdrSz = IsoBytes.U16(tim, pic + 0xC);
            int w = IsoBytes.U16(tim, pic + 0x14), h = IsoBytes.U16(tim, pic + 0x16);
            if (tim[pic + 0x13] != 5 || imgSz != w * h) throw new System.IO.IOException($"not an 8-bit picture ({w}×{h}, {imgSz} B)");
            byte[] px = tim.AsSpan(pic + hdrSz, imgSz).ToArray();
            if (swizzled) px = Unswizzle8(px, w, h);
            var outPx = new byte[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                outPx[y * n + x] = px[((y * h + h / 2) / n) * w + (x * w + w / 2) / n];   // the texel under each new texel's centre
            var outp = new byte[pic + hdrSz + n * n + clutSz];
            Array.Copy(tim, 0, outp, 0, pic + hdrSz);
            IsoBytes.U32(outp, pic + 0, (uint)(hdrSz + 4 * n * n));                      // the files' own convention: header + 4 × image (the bomb's 0x10030, these 0x40030)
            IsoBytes.U32(outp, pic + 8, (uint)(n * n));
            IsoBytes.U16(outp, pic + 0x14, (ushort)n);
            IsoBytes.U16(outp, pic + 0x16, (ushort)n);
            Array.Copy(outPx, 0, outp, pic + hdrSz, n * n);
            Array.Copy(tim, pic + hdrSz + imgSz, outp, pic + hdrSz + n * n, clutSz);
            return outp;
        }

        /// <summary>PSMT8 pixels from the GS's block order to row-major (CanalRipple's un-swizzle).</summary>
        internal static byte[] Unswizzle8(byte[] data, int w, int h)
        {
            var outp = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int blockLoc = (y & ~0xF) * w + (x & ~0xF) * 2;
                int swapSel = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & ~3) >> 1) + (y & 1)) & 0x7;
                int colLoc = posY * w * 2 + ((x + swapSel) & 0x7) * 4;
                int bn = ((y >> 1) & 1) + ((x >> 2) & 2);
                int src = blockLoc + colLoc + bn;
                if (src < data.Length) outp[y * w + x] = data[src];
            }
            return outp;
        }
    }
}
