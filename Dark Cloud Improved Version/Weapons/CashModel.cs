using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A model in the game's own item-model cash (CMainItemModel; docs/cash-models.md), so that copies of it can be drawn
    /// (BladeProp) or grafted. Loaded as the item menu loads an active item's model: the two files written into the dungeon
    /// loader's read buffer (idle between floor loads; the menu's buffer exists only while a menu is open), then SetCashModel
    /// through the call-request cave. The entry is labelled with <c>itemKey</c> — an item's id, or for a model of the mod's own
    /// an id no item has. Each cash entry's allocator is 0x9C5 units (40,016 B, GameInit): the model, its built frames and a
    /// copy of the texture bank must fit it. The cash is emptied on a floor change, so the root is checked before every use
    /// and reloaded when it is gone.</summary>
    internal sealed class CashModel
    {
        private readonly string Tag, _what;
        private readonly int _key;                                  // the cash entry's item-id label
        private readonly Func<(byte[] mds, byte[] img)> _files;     // the model's two files (null when unreadable)

        internal CashModel(string tag, int itemKey, string what, Func<(byte[] mds, byte[] img)> files)
        { Tag = tag; _key = itemKey; _what = what; _files = files; }

        /// <summary>The model's two files as the loader gets them (null when unreadable).</summary>
        internal (byte[] mds, byte[] img) Files() => _files();

        /// <summary>A model whose two files <paramref name="build"/> makes once a session (throwing when it cannot): the first call
        /// builds and logs them under <paramref name="tag"/>; a failure is logged once and not retried.</summary>
        internal static CashModel BuiltOnce(string tag, int itemKey, string what, string source, Func<(byte[] mds, byte[] img)> build)
        {
            byte[] mds = null, img = null; bool built = false, failed = false;
            return new CashModel(tag, itemKey, what, () =>
            {
                if (!built && !failed)
                {
                    try
                    {
                        (mds, img) = build();
                        built = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + $"{what} model {source}: model {mds.Length} B, texture bank {img.Length} B");
                    }
                    catch (Exception e) { failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + tag + $"the {what} model could not be built: " + e.Message); }
                }
                return built ? (mds, img) : (null, null);
            });
        }

        /// <summary>A model that is a .chr pack's own files: <paramref name="model"/> (an MDS) and <paramref name="bank"/> (an IMG/IM2
        /// bank) in <paramref name="pack"/>, an ISO path — the bank as it is, or as <paramref name="transformBank"/> makes it (a
        /// stand-in bank when its textures are too big for the cash, <see cref="StandInBank"/>). Built once a session.</summary>
        internal static CashModel FromChrPack(string tag, int itemKey, string what, string pack, string model, string bank, Func<byte[], byte[]> transformBank = null)
            => BuiltOnce(tag, itemKey, what, "read from " + pack, () =>
            {
                byte[] chr = GameDataFiles.TryReadEntry(pack) ?? throw new System.IO.IOException(pack + " not readable");
                var p = ChrPack.Parse(chr);
                byte[] mds = (p.Find(model) ?? throw new System.IO.IOException(pack + " lacks " + model)).Payload;
                byte[] img = (p.Find(bank) ?? throw new System.IO.IOException(pack + " lacks " + bank)).Payload;
                return (mds, transformBank == null ? img : transformBank(img));
            });

        /// <summary>Textures too big for the cash entry (whose allocator holds the texture bank's copy): the model is loaded with small
        /// stand-ins of the same names, then those entries are pointed at the full pictures, kept outside the cash (see
        /// <see cref="ApplyFullTexture"/>). Each: name, width, height (powers of two), 8-bit row-major pixels, the 1 KB CLUT.</summary>
        internal Func<List<(string name, int w, int h, byte[] pixels, byte[] clut)>> FullTexture;
        /// <summary>Where in the dungeon loader's read buffer this model's full textures are kept (each model its own span).</summary>
        internal int FullOffset = 0x300000;

        /// <summary>This model with <see cref="FullTexture"/> set (and <see cref="FullOffset"/>, when given).</summary>
        internal CashModel WithFullTexture(Func<List<(string name, int w, int h, byte[] pixels, byte[] clut)>> full, int? offset = null)
        { FullTexture = full; if (offset != null) FullOffset = offset.Value; return this; }

        /// <summary>An IMG bank (row-major pixels) of the pictures <paramref name="names"/> of <paramref name="bank"/>, each resampled to
        /// <paramref name="n"/>² (nearest texel) with its CLUT kept: what the cash is handed in place of textures too big for its entry;
        /// once the model is loaded their entries are pointed at the full pictures (<see cref="FullPictures"/>, <see cref="FullTexture"/>).</summary>
        internal static byte[] StandInBank(ImgBank bank, int n, params string[] names)
        {
            var items = new List<(string, byte[])>();
            foreach (string t in names) items.Add((t, Tim8.ResampleTim8(bank.Block(t), bank.Swizzled, n)));
            return ImgBank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 }, items);
        }

        /// <summary>The pictures <paramref name="names"/> of <paramref name="bank"/> whole — row-major, CLUTs as the bank has them — in
        /// <see cref="FullTexture"/>'s shape.</summary>
        internal static List<(string name, int w, int h, byte[] pixels, byte[] clut)> FullPictures(ImgBank bank, params string[] names)
        {
            var full = new List<(string name, int w, int h, byte[] pixels, byte[] clut)>();
            foreach (string t in names) { var (w, h, px, clut) = Tim8.ReadTim8(bank.Block(t), bank.Swizzled); full.Add((t, w, h, px, clut)); }
            return full;
        }

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
            // Already in the cash (the game's own load under the same label, or a load of ours the bookkeeping lost)?
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
            int need = EstimateUnits(mds, img), cap = Memory.ReadInt(ItemModels.CashAllocBase + ItemModels.CashAllocCap);
            if (need > cap) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {_what} would not fit a cash entry (~{need} of {cap} units) — not loaded (an overrun hangs the game)"); return 0; }
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
        /// draws) in this model's own cash allocator, once per load of the model; 0 when the model is not loaded or the allocator
        /// lacks <paramref name="needUnits"/> free (an overrun would hang the game).</summary>
        internal uint ShadowRoot(byte[] mds, int needUnits)
        {
            if (Root() == 0) return 0;
            if (_shadowFor == _root) return _shadow;
            if (Player.CheckDunIsPausedOrMenu()) return 0;                                 // the read buffer carries the source for one frame
            _shadowFor = _root; _shadow = 0;
            long alloc = ItemModels.CashAllocBase + (long)_cash * ItemModels.CashAllocStride;
            int used = Memory.ReadInt(alloc + ItemModels.CashAllocUsed), cap = Memory.ReadInt(alloc + ItemModels.CashAllocCap);
            if (cap - used < needUnits) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no room for the {_what}'s shadow in cash {_cash} ({cap - used} of {needUnits} units free)"); return 0; }
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf) || mds.Length > ItemModels.MenuBufferImgOffset) { _shadowFor = 0; return 0; }
            Memory.WriteBytesBatch(Memory.ToMmu(buf), mds);
            if (!NativeCall.Invoke(ItemModels.LoadMDSFile, out uint root, buf, (uint)(alloc - 0x20000000L), ItemModels.MdsKindShadow, 0, 0, timeoutMs: 1500) || !Memory.IsValidGuest(root))
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the {_what}'s shadow model did not load (0x{root:X})"); _shadowFor = 0; return 0; }
            _shadow = root;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_what} shadow model loaded into cash {_cash}: root 0x{root:X}, allocator {used} → {Memory.ReadInt(alloc + ItemModels.CashAllocUsed)} of {cap} units");
            return root;
        }
        private uint _shadow, _shadowFor;

        /// <summary>A safe-side estimate of what SetCashModel will take from the entry's allocator (16-byte units): the texture bank's
        /// copy, a 0x270 frame per node, a visual (with its 64-byte alignment) per mesh, and per sub-mesh ~16 B per record per stream
        /// of VU data + 64, padded 15% (docs/cash-models.md).</summary>
        private static int EstimateUnits(byte[] mds, byte[] img)
        {
            int nodes = (int)IsoBytes.U32(mds, 8), table = (int)IsoBytes.U32(mds, 0xC), meshes = 0;
            double vu = 0;
            for (int i = 0; i < nodes; i++)
            {
                int meshOff = (int)IsoBytes.U32(mds, table + i * 0x70 + 0x28);
                if (meshOff <= 0 || meshOff + 0x40 > mds.Length) continue;
                meshes++;
                var m = MdtFloatCodec.Parse(mds, meshOff);
                int stride = m.hasCol ? 4 : 3;
                foreach (var sub in m.subs) vu += sub.recs.Count * stride * 16 + 64;
            }
            double bytes = img.Length + nodes * CFrameVu1.NodeStride + meshes * 0x80 + vu * 1.15 + 0x100;
            return (int)Math.Ceiling(bytes / 16);
        }

        /// <summary>Forget the load (a floor change empties the cash). The texture window's reservation is handed back when the
        /// manager's cursor still stands on it; kept (and reused by the next load) when something was handed out below it.</summary>
        internal void Forget()
        {
            ReleaseTextures(); _root = 0; _cash = -1; _placedRoot = 0; _shadow = 0; _shadowFor = 0; _fullFor = 0; _full.Clear(); _windowMin = 0;
            if (_winBase != 0 && Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor) == _winBase)
            { Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _cursorSaved); _winBase = 0; _winSize = 0; }
        }

        // ── THE TEXTURE'S HOME (docs/cash-models.md). SetCashModel loads the texture through the cash's block (0x38 + cash), and
        // every block's window is packed up from the same VRAM base — the enemy, effect and weapon blocks over the same pages; the
        // last upload before a draw wins. So the entry's TEX0 (its pixels and its CLUT) is moved to a window RESERVED above every
        // block's top, taken off the manager's downward cursor so nothing is ever handed out on top of it, and the same TEX0 word
        // is patched in the model's own draw packet — which every copy and graft draws from.
        private uint _placedRoot;                                 // the root whose texture was placed (0 = none)
        private uint _winBase, _winSize, _cursorSaved;            // the reserved window, and the cursor before it was taken
        private const uint  WindowAlign = 0x20;                          // 32 GS blocks = one 8 KB page

        private void PlaceTextures()
        {
            if (_root == 0 || _cash < 0 || _placedRoot == _root) return;
            _placedRoot = _root;
            long blk = TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride;
            uint bBase = Memory.ReadUInt(blk + TextureManager.BlkBase), bTop = Memory.ReadUInt(blk + TextureManager.BlkTop);
            if (bTop > bBase && bTop - bBase < _windowMin) bTop = bBase + _windowMin;          // the full textures run past the stand-ins' block
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

        // ── THE FULL TEXTURE, OUTSIDE THE CASH (docs/cash-models.md). The texture manager uploads an entry (ReloadTexture 0x133070)
        // from its own fields: width +2, height +4, bytes per texel +6 (u16), the level-0 pixels at +0x38 (mipmaps +0x3C…, 0 here),
        // the CLUT at +0x48, a swizzled flag at +0x4C, and its VRAM placement from the TEX0 word (+0x28: TBP, TBW bits 14–19, TW
        // 26–29, TH 30–33, CBP from bit 37). The stand-in's entry is re-pointed at the full picture — kept at FullOffset in the
        // dungeon loader's read buffer (about 4 MB; the cash loads use only its first 0xFA10 + an image; menus load into it, so the
        // data is written again after any pause or menu, see KeepTextures) — its sizes and TEX0 rewritten, CBP just past the
        // pixels; the model's packet gets the same TEX0 (swept), and PlaceTextures then reserves a window that big above every
        // block. Every tick a copy is drawn the data is checked and re-written if anything overwrote it.
        private uint _fullFor, _windowMin;
        private DateTime _lastKeep;
        private const int KeepGapMs = 100;                         // the users tick every 16 ms: a longer gap was a pause or a menu
        private readonly List<(uint pixels, uint clut, byte[] px, byte[] cl)> _full = new();

        private void ApplyFullTexture()
        {
            if (FullTexture == null || _root == 0 || _cash < 0 || _fullFor == _root) return;
            _fullFor = _root; _full.Clear(); _windowMin = 0;
            var list = FullTexture();
            if (list == null || list.Count == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no full textures to apply — the stand-ins stay"); return; }
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no read buffer for the full textures — the stand-ins stay"); return; }
            long blk = TextureManager.Base + TextureManager.Blocks + (long)(CashBlockBase + _cash) * TextureManager.BlockStride;
            uint bBase = Memory.ReadUInt(blk + TextureManager.BlkBase);
            uint dst = buf + (uint)FullOffset, vram = 0;                                   // read-buffer cursor; VRAM offset from the block's base
            var moves = new List<(ulong, ulong)>();
            foreach (var (name, w, h, px, clut) in list)
            {
                if (px == null || clut == null || px.Length != w * h || clut.Length != 0x400) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"full `{name}` malformed — its stand-in stays"); continue; }
                long e = FindEntry(name);
                if (e == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"texture `{name}` not found in cash block 0x{CashBlockBase + _cash:X} — its stand-in stays"); continue; }
                uint pixels = dst, clutAt = dst + (uint)px.Length;
                dst = (clutAt + 0x400 + 0x7F) & ~0x7Fu;
                _full.Add((pixels, clutAt, px, clut));
                ulong t = (ulong)Memory.ReadUInt(e + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(e + TextureManager.EntryTex0 + 4) << 32);
                uint pixBlocks = (uint)(w * h) >> 8, tbp = bBase + vram;                       // 256-byte GS blocks; each texture page-aligned
                vram = (vram + pixBlocks + 4 + WindowAlign - 1) & ~(WindowAlign - 1);           // its pixels, then its CLUT
                int lw = 0, lh = 0; while ((1 << lw) < w) lw++; while ((1 << lh) < h) lh++;
                ulong n = t & ~(TextureManager.Tex0AddrMask | (0x3FUL << 14) | (0xFUL << 26) | (0xFUL << 30) | ((ulong)TextureManager.Tex0AddrMask << TextureManager.Tex0CbpShift));
                n |= (tbp & TextureManager.Tex0AddrMask) | ((ulong)Math.Max(1, w / 64) << 14) | ((ulong)lw << 26) | ((ulong)lh << 30)
                   | ((ulong)((tbp + pixBlocks) & TextureManager.Tex0AddrMask) << TextureManager.Tex0CbpShift);
                Memory.WriteUShort(e + 2, (ushort)w);
                Memory.WriteUShort(e + 4, (ushort)h);
                Memory.WriteUInt(e + TextureManager.EntryPixels, pixels);
                for (int lv = 1; lv < 4; lv++) Memory.WriteUInt(e + TextureManager.EntryPixels + lv * 4, 0);
                Memory.WriteUInt(e + TextureManager.EntryClut, clutAt);
                Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)n); Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(n >> 32));
                moves.Add((t, n));
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"full {w}×{h} `{name}` applied: pixels 0x{pixels:X}, CLUT 0x{clutAt:X}; TEX0 0x{t:X} → 0x{n:X}");
            }
            if (_full.Count == 0) return;
            WriteFullData();
            var (patched, visuals) = SweepModel(moves);
            _windowMin = vram;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_full.Count} full texture(s) at read buffer +0x{FullOffset:X}; {patched} register word(s) in {visuals} visual(s); window 0x{_windowMin:X} blocks");
        }

        /// <summary>This model's texture entry named <paramref name="name"/> (in its cash block), or 0.</summary>
        private long FindEntry(string name)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base + TextureManager.Count) + 1);
            for (int i = 0; i < count; i++)
            {
                long c = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                if (Memory.ReadUShort(c + TextureManager.EntryBlock) != CashBlockBase + _cash) continue;
                byte[] nb = Memory.ReadBytesBatch(c + TextureManager.EntryName, 16);
                if (nb != null && System.Text.Encoding.ASCII.GetString(nb).Split('\0')[0] == name) return c;
            }
            return 0;
        }

        private void WriteFullData()
        {
            foreach (var (pixels, clut, px, cl) in _full)
            {
                Memory.WriteBytesBatch(Memory.ToMmu(pixels), px);
                Memory.WriteBytesBatch(Memory.ToMmu(clut), cl);
            }
        }

        /// <summary>The full textures' data still where the entries point (each one's first and last 16 bytes and its CLUT's first 16);
        /// re-written when something has overwritten it.</summary>
        private void CheckFullData()
        {
            foreach (var (pixels, clut, px, cl) in _full)
            {
                byte[] a = Memory.ReadBytesBatch(Memory.ToMmu(pixels), 16), z = Memory.ReadBytesBatch(Memory.ToMmu(pixels) + px.Length - 16, 16), c = Memory.ReadBytesBatch(Memory.ToMmu(clut), 16);
                if (a == null || z == null || c == null) return;
                if (a.AsSpan().SequenceEqual(px.AsSpan(0, 16)) && z.AsSpan().SequenceEqual(px.AsSpan(px.Length - 16)) && c.AsSpan().SequenceEqual(cl.AsSpan(0, 16))) continue;
                WriteFullData();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "a full texture's data was overwritten in the read buffer — written again");
                return;
            }
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

        // ── THE COPY'S UPLOAD (docs/cash-models.md). A copy is drawn in another pass — a chara-slot copy (BladeProp) in the
        // clone-weapon slot's, a graft in the main effect's — and each pass reloads ITS block before it draws, never the cash's.
        // While a copy is up the model's entries are RE-TAGGED into that pass's block, and the block is marked unloaded every tick
        // so its uploader re-sends every entry, ours among them; tagged back when the copy goes. The cash's own block is kept
        // unloaded too, so the game's own draw of the cash model (a thrown item) re-sends it there.
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
            // After any gap in the upkeep (a menu or a pause: menus load into the read buffer, over the full textures) the data is
            // written again whole; otherwise spot-checked.
            if ((DateTime.UtcNow - _lastKeep).TotalMilliseconds > KeepGapMs) WriteFullData(); else CheckFullData();
            _lastKeep = DateTime.UtcNow;
            Tick();
        }
        /// <summary>Every tick a user is on: the cash's block re-sent whenever its own pass runs (the game's own draw of the cash model).</summary>
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
    }
}
