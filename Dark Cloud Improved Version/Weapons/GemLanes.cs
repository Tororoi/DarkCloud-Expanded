using System;
using System.Collections.Generic;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A second borrowed effect, entered into the thrown-gem effect instances (MasekiEffect: five resident CSHOT_EFFECTs, one per
    /// element, stepped and drawn by the dungeon loop every frame) whose gems are NOT equipped — only the three active-item slots
    /// can throw a gem, so at least two are always idle. Each LANE holds one gem slot, re-entered with the wanted effect at
    /// <see cref="SubShotsPerLane"/> sub-shots: Babel's Spear shows its stars on every confused enemy this way while its beam
    /// keeps the second main-character instance.
    ///
    /// The ISO's loader cave (ElfCave.BorrowedShotsEnter) serves ONE request block, BorrowedShots' — so a lane borrows it: the
    /// block is saved, the lane's request written (its config, the container, the gem slot, its own region — carved fresh the
    /// first time on a floor), the cave answers, and the block is put back as it was; the lane's config is copied to its own
    /// area (CodeCaves.GemLaneCfg) and the gem slot pointed at it, so the block's config can change under it. The block is only
    /// borrowed while its own effect sits still (entered, in the second instance, not live — its config is read through the
    /// block while stepped).
    ///
    /// A lane gives its slot back the moment that gem is equipped (the gem's own effect entered again from maseki_ex.chr with
    /// its static descriptor) and takes another idle one; the floor loader refills every gem slot on a new floor, so lanes
    /// re-enter after it. GemBurst refuses a slot a lane holds.
    /// </summary>
    internal static class GemLanes
    {
        private const string Tag = "[GemLanes] ";
        internal const int  Lanes = 2, SubShotsPerLane = 8;
        private const int   LaneReserve = 4096;          // units: the stars take ~2,450 at 6 sub-shots; the gem effect's whole arena is 65 KB
        private const double RequestTimeout = 3.0, UnwantedSeconds = 2.0;
        // Wind and Ice last: GemBurst's own users (Super Steve's wind burst, Big Bang's ice fallback) — never live with a lane's
        // ability, but kept clear while another slot will do.
        private static readonly int[] Preference = { MasekiEffect.Holy, MasekiEffect.Thunder, MasekiEffect.Fire, MasekiEffect.Ice, MasekiEffect.Wind };

        private sealed class Lane { public int Element = -1; public bool Entered; public byte[] Region, Snapshot; }
        private static readonly Lane[] _lanes = { new Lane(), new Lane() };
        private static BorrowedEffect _wanted;
        private static DateTime _wantedAt;
        /// <summary>A gem slot about to be handed back (its stars must let go of it first: their follow entries write into it).</summary>
        internal static event Action<long> Releasing;
        private static readonly object _lock = new();

        // the request in flight
        private enum Op { None, Enter }
        private static Op _op;
        private static int _opLane, _opElement;
        private static byte[] _snapshot;
        private static DateTime _opAt;
        private static bool _wasInFloor;

        private static long SlotAddr(int element) => MasekiEffect.SlotBase + (long)element * MasekiEffect.SlotStride;
        private static long LaneCfg(int lane) => CodeCaves.GemLaneCfg + (long)lane * ShotEffectPack.CfgSize;
        private static uint Guest(long mmu) => (uint)(mmu - 0x20000000L);

        /// <summary>An ability wants <paramref name="fx"/> in the lanes (called every tick while it does; lanes go back to their
        /// gems <see cref="UnwantedSeconds"/> after the last call).</summary>
        internal static void Want(BorrowedEffect fx)
        {
            lock (_lock) { if (!ReferenceEquals(_wanted, fx)) { _wanted = fx; foreach (var l in _lanes) l.Entered = false; } _wantedAt = GameClock.Now; }
        }

        /// <summary>The gem slots the wanted effect is entered in, in lane order (an empty one is skipped).</summary>
        internal static List<long> Ready()
        {
            lock (_lock) return _lanes.Where(l => l.Entered && l.Element >= 0).Select(l => SlotAddr(l.Element)).ToList();
        }

        /// <summary>The config the lanes' effect runs from (what BorrowedShots.BurstIn wants).</summary>
        internal static byte[] Cfg => _wanted == null ? null : BorrowedShots.PreparedCfg(_wanted);

        /// <summary>Whether a lane holds this element's gem slot (GemBurst must not fire into it).</summary>
        internal static bool Owns(int element) { lock (_lock) return _lanes.Any(l => l.Element == element); }

        /// <summary>BorrowedShots' loop, every pass: true while a lane's request holds the block (the loop then touches nothing).</summary>
        internal static bool Tick()
        {
            lock (_lock)
            {
                try { return TickLocked(); }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); return _op != Op.None; }
            }
        }

        private static bool TickLocked()
        {
            bool inFloor = Player.InDungeonFloor();
            if (!inFloor)
            {
                if (_wasInFloor) foreach (var l in _lanes) { l.Entered = false; l.Element = -1; l.Region = null; l.Snapshot = null; }   // a new floor reloads every gem slot
                _wasInFloor = false;
                if (_op != Op.None) Abort("left the floor");
                return false;
            }
            _wasInFloor = true;
            if (_op != Op.None) return Progress();

            // the floor loader refilled a slot under us (a new floor): its gem effect is back by itself — the lane starts over
            foreach (var l in _lanes)
                if (l.Entered && l.Element >= 0 && Memory.ReadUInt(SlotAddr(l.Element) + ShotEffectPack.OffCfg) != Guest(LaneCfg(Array.IndexOf(_lanes, l))))
                { l.Entered = false; l.Region = null; l.Snapshot = null; l.Element = -1; }

            // an equipped gem gets its slot back at once (no loader: its own fields written back); an unwanted effect gives every slot back
            var equipped = EquippedElements();
            bool wanted = _wanted != null && (GameClock.Now - _wantedAt).TotalSeconds < UnwantedSeconds;
            foreach (var l in _lanes)
                if (l.Element >= 0 && (equipped.Contains(l.Element) || !wanted))
                {
                    if (l.Snapshot != null) RestoreSlot(l);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"gem slot {l.Element} handed back ({(wanted ? "its gem is equipped" : "no longer wanted")})");
                    l.Element = -1; l.Entered = false; l.Region = null; l.Snapshot = null;
                }

            if (!wanted || !BlockFree()) return false;
            for (int i = 0; i < _lanes.Length; i++)
            {
                var l = _lanes[i];
                if (l.Entered) continue;
                int e = l.Element >= 0 ? l.Element : FreeElement(equipped);
                if (e < 0) continue;
                if (l.Snapshot == null && (l.Snapshot = TakeSnapshot(e)) == null) continue;   // a burst of its own is live: not now
                l.Element = e;
                Start(Op.Enter, i, e);
                return true;
            }
            return false;
        }

        /// <summary>The block may be borrowed: nothing seeded, or its effect entered in the second instance and not live (the second
        /// instance is stepped only while SecondEffectLive is set; the main one every frame — its config is never swapped).</summary>
        private static bool BlockFree()
        {
            var seeded = BorrowedShots.Seeded;
            if (seeded == null) return Memory.ReadUInt(CodeCaves.BorrowedShotBlock) == 0;
            if (seeded.Instance != ShotEffectPack.CharaMainEffectCrash) return false;
            return Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState) == 1 && Memory.ReadInt(CodeCaves.SecondEffectLive) == 0;
        }

        private static HashSet<int> EquippedElements()
        {
            var set = new HashSet<int>();
            foreach (long a in new long[] { Addresses.activeItem1, Addresses.activeItem2, Addresses.activeItem3 })
            {
                int id = Memory.ReadUShort(a);
                if (id >= Items.firegem && id <= Items.holygem) set.Add(id - Items.firegem);
            }
            return set;
        }

        private static int FreeElement(HashSet<int> equipped)
        {
            foreach (int e in Preference)
                if (!equipped.Contains(e) && !_lanes.Any(l => l.Element == e)) return e;
            return -1;
        }

        private static void Start(Op op, int lane, int element)
        {
            _snapshot = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock, CodeCaves.BorrowedShotBlockSize);
            if (_snapshot == null) return;
            long b = CodeCaves.BorrowedShotBlock;
            byte[] cfg = BorrowedShots.PreparedCfg(_wanted), path = BorrowedShots.PathBytes(_wanted.Path);
            int subShots = SubShotsPerLane;
            byte[] region = _lanes[lane].Region;
            var alloc = new byte[0x10];                                                            // {base, 0, used, cap}: 0 = carve a fresh region
            if (region != null) Array.Copy(region, 0, alloc, 0, 0x10);
            Memory.WriteInt(b, 0);                                                                  // quiet while the block changes
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotCfg, cfg);
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotPath, path);
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotAlloc, alloc);
            Memory.WriteInt(b + CodeCaves.BorrowedShotCarveMark, region != null ? BitConverter.ToInt32(region, 0x10) : 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotReserve, LaneReserve);
            Memory.WriteInt(b + CodeCaves.BorrowedShotInstance, (int)Guest(SlotAddr(element)));
            Memory.WriteInt(b + CodeCaves.BorrowedShotMainFlag, 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotSubShots, subShots);
            Memory.WriteInt(b + CodeCaves.BorrowedShotState, 0);
            Memory.WriteInt(b, (int)CodeCaves.BorrowedShotMagic);                                  // last: the cave enters it on its next frame
            _op = op; _opLane = lane; _opElement = element; _opAt = GameClock.Now;
        }

        private static bool Progress()
        {
            int state = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState);
            if (state == 1)
            {
                long slot = SlotAddr(_opElement);
                byte[] cfg = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg, ShotEffectPack.CfgSize);
                byte[] alloc = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc, 0x10);
                int mark = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCarveMark);
                int used = alloc == null ? 0 : BitConverter.ToInt32(alloc, 8);
                RestoreBlock();                                                                     // the block names its own instance again before the slot leaves it
                Memory.WriteBytesBatch(LaneCfg(_opLane), cfg);
                Memory.WriteUInt(slot + ShotEffectPack.OffCfg, Guest(LaneCfg(_opLane)));             // the slot runs from its own config copy
                var l = _lanes[_opLane];
                l.Region = new byte[0x14]; if (alloc != null) Array.Copy(alloc, l.Region, 0x10); BitConverter.GetBytes(mark).CopyTo(l.Region, 0x10);
                l.Entered = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lane {_opLane}: {_wanted?.Name} entered in gem slot {_opElement} ({SubShotsPerLane} sub-shots, region {used:N0} of {LaneReserve:N0} units)");
                _op = Op.None;
                return false;
            }
            if (state < 0 || (GameClock.Now - _opAt).TotalSeconds > RequestTimeout)
            {   // the cave empties the slot before it tries: the gem's own fields go back
                var l = _lanes[_opLane];
                Abort(state < 0 ? $"the cave could not enter it (no room in the monster pool?) — gem slot {_opElement}" : $"no answer in {RequestTimeout:F0} s — gem slot {_opElement}");
                if (l.Snapshot != null) RestoreSlot(l);
                l.Element = -1; l.Region = null; l.Snapshot = null; l.Entered = false;
                return false;
            }
            return true;
        }

        /// <summary>The gem slot's own fields as the floor loader left them (its model, motions and textures stay where they were
        /// loaded — a lane's entry only re-points the slot), read only while it is genuinely the gem's and none of its bursts is live.</summary>
        private static byte[] TakeSnapshot(int element)
        {
            long slot = SlotAddr(element);
            if (Memory.ReadUInt(slot + ShotEffectPack.OffCfg) != Guest(MasekiEffect.DescBase + (long)element * MasekiEffect.DescStride)) return null;
            for (int i = 0; i < ShotEffectPack.SubShots; i++) if (Memory.ReadUShort(slot + ShotEffectPack.OffActive + i * 2) != 0) return null;
            return Memory.ReadBytesBatch(slot, MasekiEffect.SlotStride);
        }

        /// <summary>The slot handed back: its stars let go (Releasing), every sub-shot off, then its own fields written back — the
        /// config pointer last; with nothing active the dungeon loop steps and draws nothing of it meanwhile.</summary>
        private static void RestoreSlot(Lane l)
        {
            long slot = SlotAddr(l.Element);
            Releasing?.Invoke(slot);
            for (int i = 0; i < ShotEffectPack.SubShots; i++) Memory.WriteUShort(slot + ShotEffectPack.OffActive + i * 2, 0);
            Memory.WriteBytesBatch(slot + 4, l.Snapshot.AsSpan(4).ToArray());
            Memory.WriteUInt(slot, BitConverter.ToUInt32(l.Snapshot, 0));
        }

        private static void Abort(string why)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + why);
            RestoreBlock();
            _op = Op.None;
        }

        /// <summary>The block as it was before the lane borrowed it, its magic word last.</summary>
        private static void RestoreBlock()
        {
            if (_snapshot == null) return;
            long b = CodeCaves.BorrowedShotBlock;
            Memory.WriteInt(b, 0);
            Memory.WriteBytesBatch(b + 4, _snapshot.AsSpan(4).ToArray());
            Memory.WriteInt(b, BitConverter.ToInt32(_snapshot, 0));
            _snapshot = null;
        }
    }
}
