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

        /// <summary>Part id used for mod-created "SynthSphere" atla. Outside the valid 0-99
        /// range: AtraPartsGet/AtraChipGet both no-op (nothing is granted), and GetAtraMsgNo
        /// (0x218CC0) falls back to system message 999, which is always in range.</summary>
        internal const int SentinelPartId = 100;

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
    ///  • Placement: claim a free parts-list entry (searched from the top) with the sentinel
    ///    part id and count 1, then write its index into a free slot of a random valid floor
    ///    (per MaxFloorTbl/NoEntryTbl). The engine spawns the atla at that floor's next build.
    ///  • Delivery: when the slot turns -3 (player collected it), the sphere is written to the
    ///    attachment board; the parts entry self-frees natively.
    /// Pending spheres are VOLATILE (lost if the mod/game closes before collection) — save-data
    /// persistence is a future improvement.
    /// </summary>
    internal static class AtlamilliaInsurance
    {
        private sealed class PendingSphere
        {
            public int Dungeon, Floor, Slot, PartsEntry;
            public byte[] Sphere;      // 0x20-byte ATTACH_LIST board entry
            public string WeaponName;
        }

        private static readonly List<PendingSphere> _pending = new List<PendingSphere>();
        private static readonly Random _rng = new Random();

        private static DateTime _nextTick = DateTime.MinValue;
        private static DateTime _nextOwnedCheck = DateTime.MinValue;
        private static bool _owned;
        private static int _snapChar = -1;
        private static byte[] _snapBag;    // active character's 10 weapon records

        // ── System-message bank patch: name sub-entry 999 ("" in vanilla) → "SynthSphere" ──
        // The atla ceremony shows the "[name] acquired." template with the name pulled from
        // system-mes sub-entry 999 — GetAtraMsgNo's fallback for invalid part ids, i.e. only
        // ever OUR sentinel atla. The bank (meswin/system_1.mes, ~48KB) loads into the static
        // 96KB SystemMesBuffer at 0x21CBCA00; entry 999 spans a single word, so instead of an
        // in-place rewrite the header pair's offset is REPOINTED at replacement text written
        // into the buffer's free tail. Header offsets are halfword indices into the text
        // stream, biased by +(entryCount + 2) — verified against the extracted file.
        // ⚠ GetTextLineDataTop_system (0x14F520) reads the pair offset as a SIGNED short
        // (short* indexing → lh), so the stored offset must stay ≤ 0x7FFF: the patch text
        // must sit below buffer byte ~66800 (at count=633). The English bank ends at byte
        // 47886, so +50000 is free AND signed-safe; +92000 froze the game (offset 45367
        // read back as -20169 → the renderer scanned garbage for a terminator forever).
        // ⚠ Entry 999 is ALSO the menus' generic empty-name string (blank weapon slot headers,
        // the build-up name placeholder, weapon-stat overlays) — the repoint is therefore
        // switched off while a menu is open (GateSphereNameMessage), never left on in menus.
        private const long SysMesBase = 0x21CBCA00;
        private const long SysMesPatchTextAddr = SysMesBase + 50000;  // free tail, signed-offset safe
        private const int SysMesSphereNameId = 999;
        private const int SysMesEmptyTwinId = 803;    // untouched twin of 999: same vanilla "" text

        private static long _sphereNamePairAddr;      // entry-999 pair offset halfword (0 = not prepared)
        private static ushort _sphereNamePatchedOff;  // offset of our name text
        private static ushort _sphereNameVanillaOff;  // vanilla empty-string offset

        // The one name text slot is rewritten to match whichever atla the player can reach:
        // synced to the current floor's pending sphere ("[weapon] SynthSphere"), with a sticky
        // override set at collection time so the ceremony message can't be re-synced away from
        // the atla actually opened (matters when one floor holds several pending atla).
        private const string SphereNameGeneric = "\nSynthSphere";
        private static string _sphereNameWritten;     // what the text slot currently holds
        private static string _sphereNameOverride;
        private static DateTime _sphereNameOverrideUntil = DateTime.MinValue;

        private static string ComposeSphereName(string weaponName) =>
            "\n" + weaponName + " SynthSphere";

        public static void Tick()
        {
            // Every call (~50ms): flip the 999 repoint on/off with the game state, so the
            // "SynthSphere" text can never linger into a menu (see GateSphereNameMessage),
            // and watch pending atla so the collection name-pin beats the ceremony message
            // even when two pending atla share a floor (one ReadInt per pending sphere).
            GateSphereNameMessage();
            DeliverCollected();

            if (DateTime.UtcNow < _nextTick) return;
            _nextTick = DateTime.UtcNow.AddMilliseconds(500);

            // Ownership check (bag + storage) and message patch prep, refreshed every ~5s
            if (DateTime.UtcNow >= _nextOwnedCheck)
            {
                _nextOwnedCheck = DateTime.UtcNow.AddSeconds(5);
                _owned = IsAtlamilliaOwned();
                PrepareSphereNameMessage();
            }

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

        /// <summary>Prepares the "SynthSphere" name patch: writes the replacement text into
        /// the bank buffer's free tail and caches the entry-999 header pair address plus the
        /// patched/vanilla offsets for <see cref="GateSphereNameMessage"/>. Idempotent;
        /// re-verified periodically in case the bank reloads over the text.</summary>
        private static void PrepareSphereNameMessage()
        {
            int cnt = Memory.ReadUShort(SysMesBase);
            if (cnt <= 0 || cnt > 2000) { _sphereNamePairAddr = 0; return; }  // bank not loaded / foreign
            byte[] hdr = Memory.ReadBytesBatch(SysMesBase + 4, cnt * 4);
            if (hdr == null) return;
            int idx = -1, emptyIdx = -1;
            for (int i = 0; i < cnt; i++)
            {
                ushort id = BitConverter.ToUInt16(hdr, i * 4);
                if (id == SysMesSphereNameId) idx = i;
                if (id == SysMesEmptyTwinId) emptyIdx = i;
            }
            if (idx < 0 || emptyIdx < 0) { _sphereNamePairAddr = 0; return; }

            int textBase = 4 + cnt * 4;
            int wordIdx = (int)((SysMesPatchTextAddr - SysMesBase - textBase) / 2);

            // (Re)write the current name text — also heals a bank reload clobbering the tail.
            string target = _sphereNameWritten ?? SphereNameGeneric;
            byte[] text = EncodeSphereName(target);
            byte[] cur = Memory.ReadBytesBatch(SysMesPatchTextAddr, text.Length);
            if (cur == null || !cur.AsSpan().SequenceEqual(text))
                Memory.WriteByteArray(SysMesPatchTextAddr, text);
            _sphereNameWritten = target;

            _sphereNamePatchedOff = (ushort)(wordIdx + cnt + 2);   // header offset bias = count + 2
            // Vanilla empty-string offset, taken from entry 803 (an untouched twin of 999 —
            // both point at the shared "" text in the vanilla bank).
            _sphereNameVanillaOff = BitConverter.ToUInt16(hdr, emptyIdx * 4 + 2);
            _sphereNamePairAddr = SysMesBase + 4 + idx * 4 + 2;
        }

        /// <summary>Restores entry 999 to the vanilla empty string while the pause/weapon menu
        /// is open, and keeps the "SynthSphere" repoint live everywhere else. The menus reuse
        /// entry 999 as the generic blank-name text (empty weapon slot headers, the build-up
        /// name placeholder, stat overlays), so a permanent repoint leaks "SynthSphere" all
        /// over the weapon menu — but the atla ceremony can never run with the menu open, so
        /// this exact split is safe. Deliberately NOT gated on dungeonMode==1: the ceremony
        /// itself may leave walking mode, and the patch must stay live while its message draws.
        /// Runs every tick call (~50ms) so a menu open shows the leak for at most a frame or two.</summary>
        private static void GateSphereNameMessage()
        {
            if (_sphereNamePairAddr == 0) return;
            byte gameMode = Memory.ReadByte(Addresses.mode);
            bool menuOpen =
                (gameMode == 2 && Memory.ReadByte(Addresses.townMode) == 8) ||
                (gameMode == 3 && Memory.ReadByte(Addresses.dungeonMode) == 2) ||
                Player.CheckIsWorldMapMenu();
            ushort want = menuOpen ? _sphereNameVanillaOff : _sphereNamePatchedOff;
            if (Memory.ReadUShort(_sphereNamePairAddr) != want)
                Memory.WriteUShort(_sphereNamePairAddr, want);

            // Keep the name text matched to the atla the player can reach next: the sticky
            // post-collection override wins (its message may still be on screen), otherwise
            // the pending sphere on the current floor, otherwise the generic name.
            string desired = SphereNameGeneric;
            if (_sphereNameOverride != null && DateTime.UtcNow < _sphereNameOverrideUntil)
            {
                desired = _sphereNameOverride;
            }
            else if (gameMode == 3)
            {
                _sphereNameOverride = null;
                int dungeon = Memory.ReadByte(Addresses.checkDungeon);
                int floor = Memory.ReadByte(Addresses.checkFloor);
                foreach (PendingSphere p in _pending)
                {
                    if (p.Dungeon == dungeon && p.Floor == floor)
                    {
                        desired = ComposeSphereName(p.WeaponName);
                        break;
                    }
                }
            }
            WriteSphereNameText(desired);
        }

        /// <summary>Rewrites the repointed name text (no-op if it already says this).</summary>
        private static void WriteSphereNameText(string text)
        {
            if (text == _sphereNameWritten || _sphereNamePairAddr == 0) return;
            Memory.WriteByteArray(SysMesPatchTextAddr, EncodeSphereName(text));
            _sphereNameWritten = text;
        }

        private static byte[] EncodeSphereName(string text)
        {
            ushort[] words;
            try { words = WeaponDescriptions.Encode(text); }
            catch (ArgumentException) { words = WeaponDescriptions.Encode(SphereNameGeneric); }
            byte[] bytes = new byte[(words.Length + 1) * 2];
            for (int i = 0; i < words.Length; i++)
                BitConverter.GetBytes(words[i]).CopyTo(bytes, i * 2);
            BitConverter.GetBytes((ushort)0xFF01).CopyTo(bytes, words.Length * 2);
            return bytes;
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
            // Two passes: first avoid floors already holding a pending sphere (keeps the
            // per-weapon ceremony name unambiguous), then allow sharing if space ran out.
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

            // Write the parts entry, then the floor slot (the atla spawns at that floor's next build)
            long ea = AtlaSystem.PartsEntryAddr(dungeon, entry);
            Memory.WriteInt(ea, AtlaSystem.SentinelPartId);
            Memory.WriteInt(ea + 4, -1);
            Memory.WriteInt(ea + 8, 1);
            Memory.WriteInt(AtlaSystem.SlotAddr(dungeon, floor, slot), entry);

            _pending.Add(new PendingSphere
            {
                Dungeon = dungeon, Floor = floor, Slot = slot, PartsEntry = entry,
                Sphere = sphere, WeaponName = name,
            });
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[Atlamillia] {name} (+{level}) broke — SynthSphere ({pct}%) placed in an atla on dungeon {dungeon} floor {floor + 1}");
        }

        private static void DeliverCollected()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSphere p = _pending[i];
                int slotVal = Memory.ReadInt(AtlaSystem.SlotAddr(p.Dungeon, p.Floor, p.Slot));
                if (slotVal == p.PartsEntry) continue;              // still waiting
                _pending.RemoveAt(i);
                if (slotVal != AtlaSystem.SlotCollected && slotVal != AtlaSystem.SlotEmpty)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[Atlamillia] atla slot for {p.WeaponName} changed unexpectedly ({slotVal}) — sphere dropped");
                    continue;
                }
                // Pin the ceremony name to THIS atla's weapon: the message may draw after this
                // tick, and the floor-sync must not swap the text to another pending sphere.
                _sphereNameOverride = ComposeSphereName(p.WeaponName);
                _sphereNameOverrideUntil = DateTime.UtcNow.AddSeconds(8);
                WriteSphereNameText(_sphereNameOverride);

                // Collected: hand over the sphere (the slot stays -3, so floor-select keeps
                // showing it as an obtained atla — same as any georama atla)
                for (int b = 0; b < WeaponCollision.AttachBoard.ScanCount; b++)
                {
                    long entry = WeaponCollision.AttachBoard.Base + (long)b * WeaponCollision.AttachBoard.Stride;
                    if (Memory.ReadUShort(entry) <= 0x50)
                    {
                        Memory.WriteByteArray(entry, p.Sphere);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[Atlamillia] SynthSphere of {p.WeaponName} delivered to the attachment board");
                        return;
                    }
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] attachment board full — SynthSphere of {p.WeaponName} lost");
            }
        }
    }
}
