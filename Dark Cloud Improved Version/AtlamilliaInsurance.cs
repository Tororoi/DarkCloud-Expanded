using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Atla placement/collection system inside the CDngStatusData object (base 0x21CD954C).
    /// RE'd 2026-07-05 from GetAtraNum/GetMaxAtraNum/GetAtraData/SetGetAtra (ELF 0x1BF770..),
    /// BtAtraListMake (0x1C09C0), BtAtraFloorCyoice (0x1C0D20, called by CDungeonMap::
    /// buildEventData at floor build — spawns entities for UNOBTAINED slots only) and
    /// BtAtraGetShort_Loop (0x1D2C70, the touch/collect flow → getAtraToSaveData →
    /// AtraPartsGet/AtraChipGet + GetAtraData marks the slot collected).
    /// </summary>
    internal static class AtlaSystem
    {
        internal const long StatusBase = 0x21CD954C;

        // ── Per-floor slots: 8 ints per floor, per dungeon ──
        internal const long SlotsBase       = StatusBase + 0x278;   // 0x21CD97C4
        internal const int  DungeonStride   = 0x500;                // 40 floors × 0x20
        internal const int  FloorStride     = 0x20;
        internal const int  SlotsPerFloor   = 8;
        internal const int  DungeonCount    = 6;
        internal const int  SlotEmpty       = -1;   // no atla
        internal const int  SlotRandomPart  = -2;   // unobtained, resolves to a random pool part
        internal const int  SlotCollected   = -3;   // obtained (read ONLY by the floor-select screen)
        // values >= 0: unobtained, index into the dungeon's parts list below

        internal static long SlotAddr(int dungeon, int floor, int slot) =>
            SlotsBase + dungeon * DungeonStride + floor * FloorStride + slot * 4;

        // ── Per-dungeon parts list: 100 entries of {int partId, int half, int count} ──
        // partId -1 = free entry. Collection decrements count and frees the entry at 0.
        internal const long PartsBase          = StatusBase + 0x2078;   // 0x21CDB5C4
        internal const int  PartsDungeonStride = 0x4B0;
        internal const int  PartsEntryStride   = 0xC;
        internal const int  PartsCount         = 100;

        internal static long PartsEntryAddr(int dungeon, int entry) =>
            PartsBase + dungeon * PartsDungeonStride + entry * PartsEntryStride;

        /// <summary>Part ids usable for mod-created atla: 24-39. AtraPartsGet no-ops for them
        /// in EVERY dungeon (GetEditPartsInfo 0x157FF0 and GetEditAtraPartsData 0x158E00 both
        /// hard-bound at id 0x17) — nothing is granted and nothing is written to save data.
        /// ⚠ Do NOT use ids ≥ 40: getAtraToSaveData routes them to AtraChipGet (0x158760),
        /// which has NO upper bounds check and appends the raw chip index (id-40) into the
        /// 128-short elem list in SAVE DATA (saveData + 0x4CCC + dungeon*0x100). The old
        /// sentinel 100 polluted saves with garbage value 60 this way (cleaned up by
        /// AtlamilliaInsurance.CleanChipListPollution).</summary>
        internal const int SentinelPartIdFirst = 24;
        internal const int SentinelPartIdLast  = 39;

        /// <summary>EditElementData (ELF 0x25AF00): static per-(dungeon, partId) table of
        /// {int modelIdx, int nameIdx, int count}, 100 × 12B per dungeon. GetAtraMsgNo
        /// (0x218CC0) computes the ceremony/georama name message id as
        /// nameIdx + 1000 + 200*dungeon (+0x28 per category tier for ids ≥ 40). nameIdx is a
        /// full int32 and vanilla -1 for ids 24-39 — writing it points a sentinel part at ANY
        /// message id. Plain data (not code): safe to write via PINE.</summary>
        internal const long EditElementData = 0x2025AF00;
        internal static long NameIdxAddr(int dungeon, int partId) =>
            EditElementData + dungeon * 0x4B0 + partId * 12 + 4;

        /// <summary>Ceremony state global (BtAtraGetShort_Loop): 0 idle, 1 ball animation,
        /// 2 message shown. Returns to 0 in the same step that calls ClearSystemMes — the
        /// safe moment to undo name plumbing used by the message.</summary>
        internal const long AtraGetStatus = 0x202A3524;

        /// <summary>Chips/residents obtained list in save data: saveData + 0x4CCC +
        /// dungeon*0x100, 128 shorts, first negative value terminates the list.</summary>
        internal const int SaveElemListOffset = 0x4CCC;
        internal const int SaveElemListStride = 0x100;

        // ── Static ELF tables (int[6] / ptr[6]) ──
        internal const long MaxFloorTbl = 0x20279E40;   // floors per dungeon
        internal const long NoEntryTbl  = 0x20279E00;   // 6 native ptrs → -1-terminated lists of
                                                        // 1-based floor numbers atla must not use
    }

    /// <summary>
    /// Ability Name: Atlamillia Insurance (Atlamillia Sword)
    /// While the Atlamillia Sword is owned (bag or storage), any non-default weapon that BREAKS
    /// in a dungeon is saved: a new atla appears on a random valid floor of that dungeon
    /// containing a SynthSphere of the broken weapon. Sphere stats scale with the weapon's
    /// level: 10% per level+1, capped at 50% (+0=10% .. +4/+=50%). Attachments are lost.
    ///
    /// Mechanics (all data-only):
    ///  • Collected atla are left untouched (-3): the floor-select screen keeps showing the
    ///    obtained markers and per-floor counts. Dynamic atla go into floors that still have a
    ///    free slot (fewer than 8 atla) — breaks are rare enough that space is plentiful, and
    ///    the new atla simply raises that floor's displayed count by one.
    ///  • Break detection: the active character's bag records are snapshotted; a record wiped to
    ///    0xFFFF outside any menu is a break (Status Break requires the menu, so it can't be
    ///    confused; the engine wipes broken weapons in place).
    ///  • Placement: claim a free parts-list entry (searched from the top) with a sentinel part
    ///    id (24-39: grant-proof, see AtlaSystem) and count 1, then write its index into a free
    ///    slot of a random valid floor (per MaxFloorTbl/NoEntryTbl). The engine spawns the atla
    ///    at that floor's next build.
    ///  • Ceremony name: each pending sphere claims a NAME CHANNEL — one of 13 orphaned
    ///    message pairs in the system bank (cut-content entries no vanilla code path can
    ///    request; enumerated against the full EditElementData table, including the georama
    ///    UI readers EdEditBuildHelpMes/MoveEditCursor/AtoraNameDraw, which all go through
    ///    GetAtraMsgNo). The orphan's offset is repointed at "[weapon] SynthSphere" text in
    ///    the bank buffer's free tail, and the sentinel's EditElementData nameIdx is aimed at
    ///    it. The game then resolves the right name natively at open time — no polling, no
    ///    rewrite races, and entry 999 (the menus' shared empty-name string) stays vanilla.
    ///  • Delivery: when the slot turns -3 (player collected it), the sphere is written to the
    ///    attachment board; the parts entry self-frees natively. The name channel is released
    ///    once the ceremony fully ends (atraGetStatus back to 0 — releasing earlier could
    ///    swap the on-screen message back to the orphan's vanilla text).
    /// Pending spheres are VOLATILE (lost if the mod/game closes before collection) — save-data
    /// persistence is a future improvement.
    /// </summary>
    internal static class AtlamilliaInsurance
    {
        private sealed class PendingSphere
        {
            public int Dungeon, Floor, Slot, PartsEntry;
            public int Sentinel;       // part id 24-39, unique per dungeon among pendings
            public int Channel;        // name channel index, -1 = none (blank ceremony name)
            public byte[] Sphere;      // 0x20-byte ATTACH_LIST board entry
            public string WeaponName;
        }

        private sealed class CoolingChannel
        {
            public int Channel, Dungeon, Sentinel;
            public DateTime Deadline;  // release even if atraGetStatus never settles
        }

        private static readonly List<PendingSphere> _pending = new List<PendingSphere>();
        private static readonly List<CoolingChannel> _cooling = new List<CoolingChannel>();
        private static readonly Random _rng = new Random();

        private static DateTime _nextTick = DateTime.MinValue;
        private static DateTime _nextOwnedCheck = DateTime.MinValue;
        private static bool _owned;
        private static int _snapChar = -1;
        private static byte[] _snapBag;    // active character's 10 weapon records

        // ── Name channels: 13 orphaned system-mes pairs ──
        // The bank (meswin/system_1.mes, 633 entries) loads into the static 96KB
        // SystemMesBuffer at 0x21CBCA00. These 13 ids exist in the bank but are UNREACHABLE
        // from the vanilla EditElementData table (verified by enumerating every id
        // GetAtraMsgNo can produce, incl. the dungeon-2 flag special case) — leftover cut
        // content ("Dummy", "Amuleo", "Wise Owl Entry", "Pillar" + blanks). Nothing vanilla
        // ever requests them, so their pair offsets can be repointed freely.
        // Header pair offsets are halfword indices biased by +(entryCount + 2).
        // ⚠ GetTextLineDataTop_system (0x14F520) reads the pair offset as a SIGNED short
        // (short* indexing → lh), so stored offsets must stay ≤ 0x7FFF: channel text must sit
        // below buffer byte ~66800 (at count=633). The English bank ends at byte 47906.
        private const long SysMesBase = 0x21CBCA00;
        private const int ChannelTextBase = 48256;     // free tail, signed-offset safe
        private const int ChannelTextStride = 96;      // 48 words per channel text slot
        private static readonly ushort[] ChannelMsgIds =
            { 1256, 1257, 1258, 1414, 1415, 1614, 1615, 1814, 1885, 1886, 2012, 2069, 2080 };
        private const int ChannelCount = 13;

        private static bool _bankReady;
        private static readonly long[] _chPairAddr = new long[ChannelCount];      // offset halfword address
        private static readonly ushort[] _chVanillaOff = new ushort[ChannelCount];
        private static readonly ushort[] _chPatchedOff = new ushort[ChannelCount];
        private static readonly string[] _chText = new string[ChannelCount];      // null = free

        private static long ChannelTextAddr(int k) => SysMesBase + ChannelTextBase + k * ChannelTextStride;

        public static void Tick()
        {
            if (DateTime.UtcNow < _nextTick) return;
            _nextTick = DateTime.UtcNow.AddMilliseconds(500);

            // Ownership check, name-channel prep/heal and save cleanup, refreshed every ~5s
            if (DateTime.UtcNow >= _nextOwnedCheck)
            {
                _nextOwnedCheck = DateTime.UtcNow.AddSeconds(5);
                _owned = IsAtlamilliaOwned();
                PrepareNameChannels();
                CleanChipListPollution();
            }

            DeliverCollected();
            ReleaseCooledChannels();

            // Break detection only matters in a dungeon with the sword owned
            if (!_owned || Memory.ReadByte(Addresses.mode) != 3)
            {
                _snapChar = -1;
                return;
            }

            int character = Player.CurrentCharacterNum();
            if (character < 0 || character > 5) { _snapChar = -1; return; }
            long bagBase = AtlaSystem.StatusBase + character * 0xAA8 + 0x450C;
            byte[] bag = Memory.ReadBytesBatch(bagBase, 10 * WeaponCollision.InventoryWeaponSlotStride);
            if (bag == null) return;

            // Only trust wipe transitions where BOTH snapshots were taken in WALKING mode
            // (dungeonMode 1): breaks happen in combat, while menus and shops can legally
            // remove weapon records (Status Break, selling). Any excursion out of walking mode
            // invalidates the snapshot, so a sale can never be bridged and misread as a break.
            if (Memory.ReadByte(Addresses.dungeonMode) != 1)
            {
                _snapChar = -1;
                _snapBag = null;
                return;
            }
            if (_snapChar == character && _snapBag != null)
            {
                for (int s = 0; s < 10; s++)
                {
                    int off = s * WeaponCollision.InventoryWeaponSlotStride;
                    ushort prevId = BitConverter.ToUInt16(_snapBag, off);
                    ushort curId = BitConverter.ToUInt16(bag, off);
                    // A weapon record wiped in place (id -> 0xFFFF) mid-combat = it broke.
                    if (prevId >= 257 && prevId <= 376 && curId == 0xFFFF)
                        OnWeaponBroke(_snapBag, off, prevId);
                }
            }
            _snapChar = character;
            _snapBag = bag;
        }

        /// <summary>Scans the system-mes bank header: caches each orphan pair's address,
        /// vanilla offset and per-channel patched offset, and re-applies text + repoint for
        /// channels that are claimed (heals a bank reload). Also restores entry 999 to the
        /// vanilla empty string if a previous mod version left it repointed (999 doubles as
        /// the menus' generic blank-name text — it must stay vanilla).</summary>
        private static void PrepareNameChannels()
        {
            _bankReady = false;
            int cnt = Memory.ReadUShort(SysMesBase);
            if (cnt <= 0 || cnt > 2000) return;                    // bank not loaded / foreign
            byte[] hdr = Memory.ReadBytesBatch(SysMesBase + 4, cnt * 4);
            if (hdr == null) return;

            int textBase = 4 + cnt * 4;
            int found = 0;
            long pair999 = 0, pair803 = 0;
            ushort off999 = 0, off803 = 0;
            for (int i = 0; i < cnt; i++)
            {
                ushort id = BitConverter.ToUInt16(hdr, i * 4);
                ushort off = BitConverter.ToUInt16(hdr, i * 4 + 2);
                if (id == 999) { pair999 = SysMesBase + 4 + i * 4 + 2; off999 = off; }
                if (id == 803) { pair803 = SysMesBase + 4 + i * 4 + 2; off803 = off; }
                for (int k = 0; k < ChannelCount; k++)
                {
                    if (id != ChannelMsgIds[k]) continue;
                    _chPairAddr[k] = SysMesBase + 4 + i * 4 + 2;
                    int wordIdx = (ChannelTextBase + k * ChannelTextStride - textBase) / 2;
                    _chPatchedOff[k] = (ushort)(wordIdx + cnt + 2);
                    // Capture the vanilla offset only while unclaimed (a claimed channel's
                    // header word may currently hold OUR patched offset).
                    if (_chText[k] == null) _chVanillaOff[k] = off;
                    found++;
                    break;
                }
            }
            if (found < ChannelCount) return;                      // unexpected bank variant
            _bankReady = true;

            // Heal claimed channels after a bank reload: text and repoint must both hold.
            for (int k = 0; k < ChannelCount; k++)
            {
                if (_chText[k] == null) continue;
                byte[] text = EncodeSphereName(_chText[k]);
                byte[] cur = Memory.ReadBytesBatch(ChannelTextAddr(k), text.Length);
                if (cur == null || !cur.AsSpan().SequenceEqual(text))
                    Memory.WriteByteArray(ChannelTextAddr(k), text);
                if (Memory.ReadUShort(_chPairAddr[k]) != _chPatchedOff[k])
                    Memory.WriteUShort(_chPairAddr[k], _chPatchedOff[k]);
            }

            // Hygiene: entries 999 and 803 are vanilla twins (both point at the shared "").
            // If they differ, a previous mod version's 999 repoint is still live — restore it.
            if (pair999 != 0 && pair803 != 0 && off999 != off803)
                Memory.WriteUShort(pair999, off803);
        }

        /// <summary>Claims a free name channel for a pending sphere: writes the ceremony text
        /// into the channel's tail slot, repoints the orphan pair at it, and aims the
        /// sentinel's EditElementData nameIdx at the orphan id. Returns the channel index or
        /// -1 (no channel free / bank not ready → the ceremony shows a blank name instead).</summary>
        private static int ClaimNameChannel(int dungeon, int sentinel, string weaponName)
        {
            if (!_bankReady) return -1;
            for (int k = 0; k < ChannelCount; k++)
            {
                if (_chText[k] != null) continue;
                _chText[k] = "\n" + weaponName + " SynthSphere";
                Memory.WriteByteArray(ChannelTextAddr(k), EncodeSphereName(_chText[k]));
                Memory.WriteUShort(_chPairAddr[k], _chPatchedOff[k]);
                Memory.WriteInt(AtlaSystem.NameIdxAddr(dungeon, sentinel),
                    ChannelMsgIds[k] - 1000 - 200 * dungeon);
                return k;
            }
            return -1;
        }

        /// <summary>Returns a channel to the pool: orphan pair offset back to vanilla, the
        /// sentinel's nameIdx back to -1.</summary>
        private static void ReleaseNameChannel(int channel, int dungeon, int sentinel)
        {
            Memory.WriteInt(AtlaSystem.NameIdxAddr(dungeon, sentinel), -1);
            if (channel < 0 || channel >= ChannelCount || _chText[channel] == null) return;
            if (_chPairAddr[channel] != 0)
                Memory.WriteUShort(_chPairAddr[channel], _chVanillaOff[channel]);
            _chText[channel] = null;
        }

        /// <summary>Releases cooling channels once their ceremony has fully ended
        /// (atraGetStatus back to 0 clears the message in the same step), or at the deadline
        /// as a backstop.</summary>
        private static void ReleaseCooledChannels()
        {
            if (_cooling.Count == 0) return;
            bool idle = Memory.ReadInt(AtlaSystem.AtraGetStatus) == 0;
            for (int i = _cooling.Count - 1; i >= 0; i--)
            {
                CoolingChannel c = _cooling[i];
                if (!idle && DateTime.UtcNow < c.Deadline) continue;
                ReleaseNameChannel(c.Channel, c.Dungeon, c.Sentinel);
                _cooling.RemoveAt(i);
            }
        }

        private static byte[] EncodeSphereName(string text)
        {
            ushort[] words;
            try { words = WeaponDescriptions.Encode(text); }
            catch (ArgumentException) { words = WeaponDescriptions.Encode("\nSynthSphere"); }
            if ((words.Length + 1) * 2 > ChannelTextStride)        // must fit the channel slot
                words = WeaponDescriptions.Encode("\nSynthSphere");
            byte[] bytes = new byte[(words.Length + 1) * 2];
            for (int i = 0; i < words.Length; i++)
                BitConverter.GetBytes(words[i]).CopyTo(bytes, i * 2);
            BitConverter.GetBytes((ushort)0xFF01).CopyTo(bytes, words.Length * 2);
            return bytes;
        }

        /// <summary>Strips garbage value 60 from the save's chips/residents lists — pollution
        /// left by the old sentinel part id 100 (AtraChipGet(100-40) has no bounds check and
        /// appended raw 60 on every insurance atla collected). 60 is invalid in every dungeon
        /// (chips run 0-20ish, residents 40-54), so removal is unambiguous.</summary>
        private static void CleanChipListPollution()
        {
            int native = Memory.ReadInt(FishingRankList.SaveDataPtr);
            if (native < 0x80000 || native >= 0x2000000) return;   // no save loaded
            long listsBase = Memory.ToMmu(native) + AtlaSystem.SaveElemListOffset;
            byte[] all = Memory.ReadBytesBatch(listsBase, AtlaSystem.DungeonCount * AtlaSystem.SaveElemListStride);
            if (all == null) return;
            for (int d = 0; d < AtlaSystem.DungeonCount; d++)
            {
                int baseOff = d * AtlaSystem.SaveElemListStride;
                var kept = new List<short>();
                int len = 0;
                bool dirty = false;
                for (; len < 128; len++)
                {
                    short v = BitConverter.ToInt16(all, baseOff + len * 2);
                    if (v < 0) break;
                    if (v == 60) { dirty = true; continue; }
                    kept.Add(v);
                }
                if (!dirty) continue;
                byte[] rebuilt = new byte[AtlaSystem.SaveElemListStride];
                for (int i = 0; i < rebuilt.Length; i += 2)
                    BitConverter.GetBytes((short)-1).CopyTo(rebuilt, i);
                for (int i = 0; i < kept.Count; i++)
                    BitConverter.GetBytes(kept[i]).CopyTo(rebuilt, i * 2);
                Memory.WriteByteArray(listsBase + baseOff, rebuilt);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] stripped {len - kept.Count} stray chip-60 entries from dungeon {d}'s save list");
            }
        }

        private static bool IsAtlamilliaOwned()
        {
            for (int s = 0; s < 10; s++)
            {
                if (Memory.ReadUShort(WeaponCollision.InventoryWeaponSlot0Id +
                        s * WeaponCollision.InventoryWeaponSlotStride) == Items.atlamilliasword)
                    return true;
            }
            for (int s = 0; s < 30; s++)   // storage records (see CheckChronicle2)
            {
                if (Memory.ReadUShort(0x21CE22D8 + s * 0xF8) == Items.atlamilliasword)
                    return true;
            }
            return false;
        }

        private static void OnWeaponBroke(byte[] snap, int off, ushort weaponId)
        {
            int dungeon = Memory.ReadByte(Addresses.checkDungeon);
            if (dungeon < 0 || dungeon >= AtlaSystem.DungeonCount) return;

            int level = BitConverter.ToUInt16(snap, off + WeaponCollision.InventoryWeaponLevelOffset);
            int pct = Math.Min(50, (level + 1) * 10);
            string name = weaponId < Items.ItemNameTbl.Length ? Items.ItemNameTbl[weaponId] : $"weapon {weaponId}";

            // Build the sphere board entry from the weapon's own stats (attachments are lost)
            byte[] sphere = new byte[WeaponCollision.AttachBoard.Stride];
            BitConverter.GetBytes((ushort)WeaponCollision.AttachBoard.SynthSphereId).CopyTo(sphere, 0);
            BitConverter.GetBytes(weaponId).CopyTo(sphere, WeaponCollision.AttachBoard.EntrySourceId);
            BitConverter.GetBytes(BitConverter.ToUInt16(snap, off + 0xEE)).CopyTo(sphere, WeaponCollision.AttachBoard.EntryFlags);
            sphere[WeaponCollision.AttachBoard.EntrySourceLevel] = (byte)Math.Min(level, 255);
            for (int i = 0; i < 4; i++)
            {
                short stat = (short)(BitConverter.ToInt16(snap, off + 4 + i * 2) * pct / 100);
                BitConverter.GetBytes(stat).CopyTo(sphere, WeaponCollision.AttachBoard.EntryStats + i * 2);
            }
            for (int i = 0; i < 5; i++)
                sphere[WeaponCollision.AttachBoard.EntryElements + i] = (byte)((sbyte)snap[off + 0x17 + i] * pct / 100);
            for (int i = 0; i < 10; i++)
                sphere[WeaponCollision.AttachBoard.EntryAntis + i] = (byte)((sbyte)snap[off + WeaponCollision.WeaponAntiOffset + i] * pct / 100);

            // Claim a free parts-list entry (from the top, away from the native list)
            int entry = -1;
            for (int e = AtlaSystem.PartsCount - 1; e >= 0; e--)
            {
                if (Memory.ReadInt(AtlaSystem.PartsEntryAddr(dungeon, e)) == -1) { entry = e; break; }
            }
            if (entry < 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] no free parts entry in dungeon {dungeon} — {name} not saved");
                return;
            }

            // Pick a sentinel part id (24-39) not used by another pending atla in this dungeon
            // (each sentinel's nameIdx can only point at one channel at a time).
            int sentinel = -1;
            for (int id = AtlaSystem.SentinelPartIdFirst; id <= AtlaSystem.SentinelPartIdLast && sentinel < 0; id++)
            {
                bool taken = _pending.Exists(p => p.Dungeon == dungeon && p.Sentinel == id) ||
                             _cooling.Exists(c => c.Dungeon == dungeon && c.Sentinel == id);
                if (!taken) sentinel = id;
            }
            if (sentinel < 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] all sentinel part ids busy in dungeon {dungeon} — {name} not saved");
                return;
            }

            // Pick a random valid floor with a free slot
            int floors = Memory.ReadInt(AtlaSystem.MaxFloorTbl + dungeon * 4);
            var noEntry = new HashSet<int>();
            int listPtr = Memory.ReadInt(AtlaSystem.NoEntryTbl + dungeon * 4);
            if (listPtr != 0)
            {
                long list = Memory.ToMmu(listPtr);
                for (int i = 0; i < 64; i++)
                {
                    int v = Memory.ReadInt(list + i * 4);
                    if (v == -1) break;
                    noEntry.Add(v);   // 1-based floor numbers
                }
            }
            // Two passes: first avoid floors already holding a pending sphere, then allow
            // sharing if space ran out (names stay correct either way — each atla carries its
            // own sentinel id — this just spreads them out).
            int floor = -1, slot = -1;
            for (int pass = 0; pass < 2 && floor < 0; pass++)
            {
                for (int attempt = 0; attempt < 60 && floor < 0; attempt++)
                {
                    int f = _rng.Next(Math.Max(1, floors));
                    if (noEntry.Contains(f + 1)) continue;
                    if (pass == 0 && _pending.Exists(p => p.Dungeon == dungeon && p.Floor == f)) continue;
                    for (int s = 0; s < AtlaSystem.SlotsPerFloor; s++)
                    {
                        if (Memory.ReadInt(AtlaSystem.SlotAddr(dungeon, f, s)) == AtlaSystem.SlotEmpty)
                        {
                            floor = f; slot = s;
                            break;
                        }
                    }
                }
            }
            if (floor < 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] no free atla slot found in dungeon {dungeon} — {name} not saved");
                return;
            }

            // Wire the ceremony name, then the parts entry, then the floor slot (the atla
            // spawns at that floor's next build).
            int channel = ClaimNameChannel(dungeon, sentinel, name);
            long ea = AtlaSystem.PartsEntryAddr(dungeon, entry);
            Memory.WriteInt(ea, sentinel);
            Memory.WriteInt(ea + 4, -1);
            Memory.WriteInt(ea + 8, 1);
            Memory.WriteInt(AtlaSystem.SlotAddr(dungeon, floor, slot), entry);

            _pending.Add(new PendingSphere
            {
                Dungeon = dungeon, Floor = floor, Slot = slot, PartsEntry = entry,
                Sentinel = sentinel, Channel = channel,
                Sphere = sphere, WeaponName = name,
            });
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[Atlamillia] {name} (+{level}) broke — SynthSphere ({pct}%) placed in an atla on " +
                $"dungeon {dungeon} floor {floor + 1} (sentinel {sentinel}, channel {channel})");
        }

        private static void DeliverCollected()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSphere p = _pending[i];
                int slotVal = Memory.ReadInt(AtlaSystem.SlotAddr(p.Dungeon, p.Floor, p.Slot));
                if (slotVal == p.PartsEntry) continue;              // still waiting
                _pending.RemoveAt(i);

                // The ceremony message may still be latching/on screen — release the name
                // channel only once atraGetStatus settles (see ReleaseCooledChannels).
                _cooling.Add(new CoolingChannel
                {
                    Channel = p.Channel, Dungeon = p.Dungeon, Sentinel = p.Sentinel,
                    Deadline = DateTime.UtcNow.AddSeconds(60),
                });

                if (slotVal != AtlaSystem.SlotCollected && slotVal != AtlaSystem.SlotEmpty)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[Atlamillia] atla slot for {p.WeaponName} changed unexpectedly ({slotVal}) — sphere dropped");
                    continue;
                }
                // Collected: hand over the sphere (the slot stays -3, so floor-select keeps
                // showing it as an obtained atla — same as any georama atla)
                bool delivered = false;
                for (int b = 0; b < WeaponCollision.AttachBoard.ScanCount && !delivered; b++)
                {
                    long entry = WeaponCollision.AttachBoard.Base + (long)b * WeaponCollision.AttachBoard.Stride;
                    if (Memory.ReadUShort(entry) <= 0x50)
                    {
                        Memory.WriteByteArray(entry, p.Sphere);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[Atlamillia] SynthSphere of {p.WeaponName} delivered to the attachment board");
                        delivered = true;
                    }
                }
                if (!delivered)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[Atlamillia] attachment board full — SynthSphere of {p.WeaponName} lost");
            }
        }
    }
}
