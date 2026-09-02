using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Super Steve cosmetic: the slingshot adopts the attached sphere weapon's COLOUR PALETTE. PROVEN LIVE:
    /// the weapon model's TIM2 stays resident in EE RAM and feeds the drawn model each frame, so overwriting
    /// it re-skins the weapon instantly (striped-texture probe, 2026-07-08).
    ///
    /// Only the 0x400-byte CLUT is swapped — Super Steve's pixel INDICES (his artwork, shading, and face)
    /// stay untouched, so the whole model recolours coherently in the source weapon's colour scheme (user-
    /// tuned: cleaner than transplanting pixels, which read as abstract mosaics). Because pixels are never
    /// modified, a bottom-half pixel signature stays permanently valid as the locator for live copies.
    ///
    /// Mechanics:
    ///  • Every weapon model is data.dat `commenu\weapon\cNNwMM.chr` (model index = itemId − brokenId, e.g.
    ///    Super Steve 312−299 = c04w13) and embeds ONE TIM2 in a single shared format — 128×128, 8-bit
    ///    indexed: 0x4000 pixel bytes immediately followed by the 0x400 CLUT. Same format for every weapon
    ///    of every character, so any weapon's CLUT drops cleanly onto any other.
    ///  • The SOURCE weapon's CLUT is read straight from data.dat on disk (data.hed 80-byte name entries;
    ///    offset/size at data.hd2 16+i*32) — the game never has to load it.
    ///  • Live copies are located by scanning EE RAM for the pixel signature; each copy is then verified and
    ///    (re)painted independently every tick — this self-heals floor reloads (game restores the vanilla
    ///    CLUT) and catches the weapon menu's separate preview copy (fresh scan on a short cadence while the
    ///    menu is open). Writes go through WriteBytesBatch.
    /// Fails safe: if the extracted game files aren't at <see cref="GameDataFiles.DataDir"/>, the swap
    /// silently disables.
    /// </summary>
    internal static class WeaponTextureSwap
    {
        internal static bool SwapEnabled = true;

        private const int PixBytes  = 0x4000;   // 128×128 8-bit indices
        private const int ClutBytes = 0x400;    // 256 × RGBA32, contiguous after the pixels
        private const int BlobBytes = PixBytes + ClutBytes;
        private const int SigBytes  = 48;
        // Locator: 48 pixel bytes @ +0x3100 (bottom half) of Super Steve's (c04w13) TIM2 — unique in-file,
        // and in a region no code path ever writes, so it identifies live copies forever.
        private const int LocSigOffset = 0x3100;
        private static readonly byte[] LocSig =
        {
            0xFF, 0x5B, 0xF6, 0x6E, 0xFF, 0x6C, 0xDD, 0x6E, 0xFF, 0x6B, 0xF7, 0x52, 0xFF, 0x48, 0xF9, 0x58,
            0xE4, 0x57, 0xE1, 0x48, 0xCC, 0x51, 0xFB, 0x3F, 0xDA, 0x3C, 0xFB, 0x4E, 0xDB, 0x3E, 0xFB, 0x84,
            0xE3, 0x5B, 0x5E, 0x42, 0xE3, 0x6D, 0xFF, 0x42, 0xFB, 0x6D, 0xFF, 0x42, 0xE1, 0x6C, 0xFF, 0x42,
        };
        // Top-half check window (used to self-heal pixels an older build's "shirt swap" may have left).
        private const int TopSigOffset = 0x1D80;

        // ── the global CTextureManager (RE'd: SearchTextureName 0x131180 / GetTexture(int) 0x131290) ──
        // ONE instance at main-BSS 0x1C75870 serves everything: the dungeon effect loader AND the menu
        // weapon-preview loader (EnterWeaponModel 0x20D4C0) both pass it explicitly. Layout: +0 = last
        // entry index; entries at +0x10F8, stride 0x50; per entry: +0x00 u16 block id, +0x08 name (inline,
        // NUL-terminated), +0x38 native ptr to PIXELS, +0x48 native ptr to CLUT (LoadTexture 0x154950 DMAs
        // the GS upload FROM those pointers — which is why writing them re-skins live). Walking this table
        // resolves every live copy of the weapon texture BY NAME in ~4 PINE round-trips — dungeon copy and
        // menu preview alike — replacing the slow 32MB signature scan (kept only as a fallback).
        private const long TexMgr         = 0x21C75870;
        private const int  MgrEntry0      = 0x10F8;
        private const int  MgrEntryStride = 0x50;
        private const int  EntryNameOff   = 0x08;
        private const int  EntryPixOff    = 0x38;
        private const int  MgrMaxEntries  = 2048;   // sanity bound on the count field
        private static readonly byte[] TexName = System.Text.Encoding.ASCII.GetBytes("c04w13");   // Super Steve's registered texture name (prefix match)

        private static bool _initTried, _filesOk;
        private static readonly Dictionary<int, byte[]> _texCache = new Dictionary<int, byte[]>();   // weaponId → 0x4400 blob (null = no model)
        private static readonly List<long> _pixBases = new List<long>();   // located live pixel bases (MMU)
        private static DateTime _nextQuickScan = DateTime.MinValue;   // region-window scan throttle (cheap)
        private static DateTime _nextFullScan  = DateTime.MinValue;   // full-RAM scan throttle (takes seconds)
        private static long _regionMin, _regionMax;                   // heap neighbourhood of past hits (MMU)

        private static Thread _thread;

        /// <summary>Start the always-on swap thread (idempotent). Runs independently of the dungeon
        /// dispatcher so the recolour also works in TOWN (menu previews) — it reads Xiao's equipped weapon
        /// record directly, no matter who the active character is or where the player is.</summary>
        internal static void StartSwapThread()
        {
            if (!SwapEnabled || (_thread != null && _thread.IsAlive)) return;
            _thread = new Thread(SwapLoop) { IsBackground = true };
            _thread.Start();
        }

        private static void SwapLoop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    if (!Memory.IsConnected) continue;

                    int slot = Memory.ReadByte(DngStatusData.Base +
                                               DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
                    bool steve = false; int sphere = 0;
                    if ((uint)slot <= 9)
                    {
                        long rec = DngStatusData.WeaponRecord(Player.XiaoId, slot);
                        steve = Memory.ReadUShort(rec) == Items.supersteve;
                        if (steve) sphere = SuperSteveAbilities.AttachedSphere(rec);
                    }
                    DriveSphereTexture(steve, sphere);
                }
                catch { /* PINE hiccups / game not booted — just keep ticking */ }
            }
        }

        /// <summary>One swap tick: every live copy of Super Steve's texture gets the attached sphere
        /// weapon's CLUT (or his own when no sphere / not equipped). Deliberately independent of pause and
        /// location — the recolour holds (and updates live on sphere swaps) inside weapon menus, whose
        /// preview model is a SEPARATE copy caught by the menu-cadence rescan.</summary>
        private static void DriveSphereTexture(bool active, int sphereWeaponId)
        {
            if (!EnsureInit()) return;

            byte[] vanilla = GetTexture(Items.supersteve);
            if (vanilla == null) return;
            int want = Items.supersteve;
            if (active && sphereWeaponId > 0 && GetTexture(sphereWeaponId) != null) want = sphereWeaponId;
            byte[] wantTex = GetTexture(want);

            // Locate copies — primary path: walk the global CTextureManager's table and resolve every entry
            // registered under Super Steve's texture name (dungeon copy AND menu preview). Deterministic and
            // ~4 round-trips, so it simply runs every tick; a copy is picked up the tick after it loads.
            MergeHits(ResolveCopies());

            // Fallback only: if the table walk finds nothing but the texture should be visible (equipped in
            // a dungeon / previewed in a menu), fall back to the region-then-full signature scan.
            bool menuOpen = Player.CheckIsWeaponMenu();
            if (_pixBases.Count == 0 && active && (menuOpen || Player.InDungeonFloor()))
            {
                bool found = false;
                if (_regionMin != 0 && DateTime.UtcNow >= _nextQuickScan)
                {
                    _nextQuickScan = DateTime.UtcNow.AddSeconds(2);
                    found = MergeHits(ScanEeRam(LocSig, LocSigOffset,
                                               Math.Max(Memory.Pcsx2Base, _regionMin - 0x200000),
                                               Math.Min(Memory.Pcsx2Base + Memory.EeRamSize, _regionMax + 0x200000)));
                }
                if (!found && DateTime.UtcNow >= _nextFullScan)
                {
                    _nextFullScan = DateTime.UtcNow.AddSeconds(10);
                    MergeHits(ScanEeRam(LocSig, LocSigOffset,
                                        Memory.Pcsx2Base, Memory.Pcsx2Base + Memory.EeRamSize));
                }
            }
            if (_pixBases.Count == 0) return;

            int painted = 0;
            for (int i = _pixBases.Count - 1; i >= 0; i--)
            {
                long b = _pixBases[i];

                // Identity check: the never-written bottom-half pixels must still match. Anything else means
                // the heap was reused (e.g. a closed menu's preview) — drop it, never write foreign memory.
                byte[] loc = Memory.ReadBytesBatch(b + LocSigOffset, SigBytes);
                if (loc == null || !SegEquals(loc, LocSig, 0)) { _pixBases.RemoveAt(i); continue; }

                // Self-heal: an older build swapped top-half pixels; restore them to vanilla if present.
                byte[] top = Memory.ReadBytesBatch(b + TopSigOffset, SigBytes);
                if (top != null && !SegEquals(top, vanilla, TopSigOffset))
                {
                    var topHalf = new byte[PixBytes / 2];
                    Array.Copy(vanilla, topHalf, topHalf.Length);
                    Memory.WriteBytesBatch(b, topHalf);
                }

                // The actual effect: make the live CLUT match the wanted weapon's.
                byte[] clut = Memory.ReadBytesBatch(b + PixBytes, ClutBytes);
                if (clut == null || SegEquals(clut, wantTex, PixBytes)) continue;   // already painted
                var wantClut = new byte[ClutBytes];
                Array.Copy(wantTex, PixBytes, wantClut, 0, ClutBytes);
                Memory.WriteBytesBatch(b + PixBytes, wantClut);
                painted++;
            }
            if (painted > 0)
                Console.WriteLine($"[TexSwap] painted {painted} live cop(ies) with weapon {want}'s palette");
        }

        /// <summary>Resolve every live copy of Super Steve's texture through the global CTextureManager:
        /// read the whole entry table in a few batched reads, match entry names against
        /// <see cref="TexName"/>, and return the entries' native pixel pointers (as MMU addresses). Each
        /// candidate is identity-checked against the never-written bottom-half signature before use.</summary>
        private static List<long> ResolveCopies()
        {
            var found = new List<long>();
            int count = Memory.ReadInt(TexMgr);
            if (count < 0 || count > MgrMaxEntries) return found;

            byte[] table = Memory.ReadBytesBatch(TexMgr + MgrEntry0, (count + 1) * MgrEntryStride);
            if (table == null) return found;

            for (int i = 0; i <= count; i++)
            {
                int o = i * MgrEntryStride;
                // Name prefix match at entry+0x08 ("c04w13", however the loader suffixed it).
                bool match = true;
                for (int j = 0; j < TexName.Length && match; j++)
                    match = table[o + EntryNameOff + j] == TexName[j];
                if (!match) continue;

                uint pixNative = BitConverter.ToUInt32(table, o + EntryPixOff);
                if (!Memory.IsValidGuest(pixNative)) continue;
                long pix = Memory.ToMmu(pixNative);
                if (found.Contains(pix) || _pixBases.Contains(pix)) { if (!found.Contains(pix)) found.Add(pix); continue; }

                // Identity check before trusting the pointer (stale entries after frees are possible).
                byte[] loc = Memory.ReadBytesBatch(pix + LocSigOffset, SigBytes);
                if (SegEquals(loc, LocSig, 0)) found.Add(pix);
            }
            if (found.Count > 0 && !_resolvedOnce)
            {
                _resolvedOnce = true;
                Console.WriteLine($"[TexSwap] texture manager resolved {found.Count} live cop(ies) — no scan needed");
            }
            return found;
        }
        private static bool _resolvedOnce;

        /// <summary>Fold freshly-scanned bases into the list and widen the remembered heap neighbourhood.
        /// Returns true if the scan produced any hit.</summary>
        private static bool MergeHits(List<long> hits)
        {
            foreach (long b in hits)
            {
                if (!_pixBases.Contains(b)) _pixBases.Add(b);
                if (_regionMin == 0 || b < _regionMin) _regionMin = b;
                if (b > _regionMax) _regionMax = b;
            }
            return hits.Count > 0;
        }

        // ── data.dat access ──────────────────────────────────────────────────────────────────

        private static bool EnsureInit()
        {
            if (_initTried) return _filesOk;
            _initTried = true;
            _filesOk = GameDataFiles.Available;
            if (!_filesOk)
                Console.WriteLine("[TexSwap] Dark Cloud ISO not found — palette swap disabled");
            return _filesOk;
        }

        /// <summary>The weapon's model file name: model index = itemId − that character's broken-weapon id
        /// (c01 Toan / c04 Xiao / c05 Ruby / c06 Goro / c10 Ungaga / c18 Osmond).</summary>
        private static string ModelChr(int weaponId)
        {
            if (weaponId >= 363) return $"c18w{weaponId - 363:D2}.chr";
            if (weaponId >= 347) return $"c10w{weaponId - 347:D2}.chr";
            if (weaponId >= 331) return $"c05w{weaponId - 331:D2}.chr";
            if (weaponId >= 314) return $"c06w{weaponId - 314:D2}.chr";
            if (weaponId >= 299) return $"c04w{weaponId - 299:D2}.chr";
            if (weaponId >= 257) return $"c01w{weaponId - 257:D2}.chr";
            return null;
        }

        /// <summary>The weapon's 0x4400 pixel+CLUT blob from its model chr in data.dat (cached; null if the
        /// weapon has no model entry or the TIM2 can't be located).</summary>
        private static byte[] GetTexture(int weaponId)
        {
            if (_texCache.TryGetValue(weaponId, out byte[] cached)) return cached;
            byte[] blob = null;
            try
            {
                string chr = ModelChr(weaponId);
                byte[] file = chr != null ? GameDataFiles.TryReadEntry("commenu\\weapon\\" + chr) : null;
                if (file != null)
                {
                    int t = FindTim2(file);
                    if (t >= 0)
                    {
                        int hdrSize = BitConverter.ToUInt16(file, t + 0x1C);
                        int pix = t + 0x10 + hdrSize;
                        blob = new byte[BlobBytes];
                        Array.Copy(file, pix, blob, 0, BlobBytes);
                    }
                }
            }
            catch (Exception e) { Console.WriteLine($"[TexSwap] texture load failed for weapon {weaponId}: {e.Message}"); }
            _texCache[weaponId] = blob;
            return blob;
        }

        private static int FindTim2(byte[] d)
        {
            for (int i = 0; i + 4 <= d.Length; i++)
                if (d[i] == 'T' && d[i + 1] == 'I' && d[i + 2] == 'M' && d[i + 3] == '2') return i;
            return -1;
        }

        // ── live-RAM helpers ─────────────────────────────────────────────────────────────────

        /// <summary>True if <paramref name="cur"/> equals <paramref name="reference"/>'s bytes starting at
        /// <paramref name="refOffset"/> (compared over cur.Length bytes).</summary>
        private static bool SegEquals(byte[] cur, byte[] reference, int refOffset)
        {
            if (cur == null || reference == null || refOffset + cur.Length > reference.Length) return false;
            for (int i = 0; i < cur.Length; i++)
                if (cur[i] != reference[refOffset + i]) return false;
            return true;
        }

        /// <summary>Chunked signature scan over the MMU range [<paramref name="start"/>, <paramref name="end"/>)
        /// (ReadBytesBatch blocks with needle-length overlap). Returns the MMU pixel-base address per hit.</summary>
        private static List<long> ScanEeRam(byte[] needle, int needleOffsetInPixels, long start, long end)
        {
            var bases = new List<long>();
            const int Block = 0x40000;
            int overlap = needle.Length - 1;
            byte first = needle[0];

            for (long addr = start; addr < end; addr += Block - overlap)
            {
                int size = (int)Math.Min(Block, end - addr);
                if (size <= overlap) break;
                byte[] buf;
                try { buf = Memory.ReadBytesBatch(addr, size); }
                catch { continue; }
                if (buf == null) continue;

                for (int i = 0; i + needle.Length <= buf.Length; i++)
                {
                    if (buf[i] != first) continue;
                    bool match = true;
                    for (int j = 1; j < needle.Length && match; j++)
                        match = buf[i + j] == needle[j];
                    if (match) bases.Add(addr + i - needleOffsetInPixels);
                }
            }
            return bases;
        }
    }
}
