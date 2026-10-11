using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
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
    ///  • Ceremony name: each pending sphere claims a name channel ("[weapon] SynthSphere",
    ///    AtlaNameChannels) on its sentinel part id.
    ///  • Delivery: when the slot turns -3 (player collected it), the sphere is written to the
    ///    attachment board; the parts entry self-frees natively. The name channel is released
    ///    once the ceremony fully ends (atraGetStatus back to 0 — releasing earlier could
    ///    swap the on-screen message back to the orphan's vanilla text).
    /// Pending spheres are VOLATILE (lost if the mod/game closes before collection) — save-data
    /// persistence is a future improvement.
    /// </summary>
    internal static class AtlamilliaSword
    {
        private sealed class PendingSphere
        {
            public int Dungeon, Floor, Slot, PartsEntry;
            public int Sentinel;       // part id 24-39, unique per dungeon among pendings
            public int Channel;        // name channel index, -1 = none (blank ceremony name)
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

        /// <summary>Atlamillia Insurance: the pass the main loop hands every tick (self-gated to ~2 Hz).</summary>
        public static void AtlamilliaInsuranceEffect()
        {
            if (DateTime.UtcNow < _nextTick) return;
            _nextTick = DateTime.UtcNow.AddMilliseconds(500);

            // Ownership check, refreshed every ~5s
            if (DateTime.UtcNow >= _nextOwnedCheck)
            {
                _nextOwnedCheck = DateTime.UtcNow.AddSeconds(5);
                _owned = IsAtlamilliaOwned();
            }
            AtlaNameChannels.Prepare();

            DeliverCollected();
            AtlaNameChannels.ReleaseCooled();

            // Break detection only matters in a dungeon with the sword owned
            if (!_owned || Memory.ReadByte(Addresses.mode) != 3)
            {
                _snapChar = -1;
                return;
            }

            int character = Player.CurrentCharacterNum();
            if (character < 0 || character > 5) { _snapChar = -1; return; }
            long bagBase = AtlaSystem.StatusBase + character * 0xAA8 + 0x450C;
            byte[] bag = Memory.ReadBytesBatch(bagBase, 10 * WeaponHave.InventoryWeaponSlotStride);
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
                    int off = s * WeaponHave.InventoryWeaponSlotStride;
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

        private static bool IsAtlamilliaOwned()
        {
            for (int s = 0; s < 10; s++)
            {
                if (Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        s * WeaponHave.InventoryWeaponSlotStride) == Items.atlamilliasword)
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

            int level = BitConverter.ToUInt16(snap, off + WeaponHave.InventoryWeaponLevelOffset);
            int pct = Math.Min(50, (level + 1) * 10);
            string name = weaponId < Items.ItemNameTbl.Length ? Items.ItemNameTbl[weaponId] : $"weapon {weaponId}";

            // Build the sphere board entry from the weapon's own stats (attachments are lost)
            byte[] sphere = new byte[AttachBoard.Stride];
            BitConverter.GetBytes((ushort)AttachBoard.SynthSphereId).CopyTo(sphere, 0);
            BitConverter.GetBytes(weaponId).CopyTo(sphere, AttachBoard.EntrySourceId);
            BitConverter.GetBytes(BitConverter.ToUInt16(snap, off + 0xEE)).CopyTo(sphere, AttachBoard.EntryFlags);
            sphere[AttachBoard.EntrySourceLevel] = (byte)Math.Min(level, 255);
            for (int i = 0; i < 4; i++)
            {
                short stat = (short)(BitConverter.ToInt16(snap, off + 4 + i * 2) * pct / 100);
                BitConverter.GetBytes(stat).CopyTo(sphere, AttachBoard.EntryStats + i * 2);
            }
            for (int i = 0; i < 5; i++)
                sphere[AttachBoard.EntryElements + i] = (byte)((sbyte)snap[off + 0x17 + i] * pct / 100);
            for (int i = 0; i < 10; i++)
                sphere[AttachBoard.EntryAntis + i] = (byte)((sbyte)snap[off + WeaponHave.WeaponAntiOffset + i] * pct / 100);

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

            // A sentinel part id (24-39) no other pending atla in this dungeon carries (each sentinel's nameIdx can point at one
            // channel at a time)
            int sentinel = AtlaNameChannels.TakeSentinel(dungeon);
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
                AtlaNameChannels.FreeSentinel(dungeon, sentinel);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[Atlamillia] no free atla slot found in dungeon {dungeon} — {name} not saved");
                return;
            }

            // Wire the ceremony name, then the parts entry, then the floor slot (the atla
            // spawns at that floor's next build).
            int channel = AtlaNameChannels.Claim(dungeon, sentinel, "\n" + name + " SynthSphere");
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

                // The ceremony message may still be latching/on screen — the name channel is released
                // once atraGetStatus settles (AtlaNameChannels.ReleaseCooled).
                AtlaNameChannels.Cool(p.Channel, p.Dungeon, p.Sentinel);

                if (slotVal != AtlaSystem.SlotCollected && slotVal != AtlaSystem.SlotEmpty)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[Atlamillia] atla slot for {p.WeaponName} changed unexpectedly ({slotVal}) — sphere dropped");
                    continue;
                }
                // Collected: hand over the sphere (the slot stays -3, so floor-select keeps
                // showing it as an obtained atla — same as any georama atla)
                bool delivered = false;
                for (int b = 0; b < AttachBoard.ScanCount && !delivered; b++)
                {
                    long entry = AttachBoard.Base + (long)b * AttachBoard.Stride;
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
