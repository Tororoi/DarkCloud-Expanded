using System;
using System.Collections.Generic;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The monster shot pack's five slots shared among every shot config a floor's roster needs, so a floor is no
    /// longer limited to five configs (the vanilla loader refused the sixth and left a number that was no slot). The cave in
    /// tools/stubs/shared_shots.s does the work in the machine — a refused config is stored in its negative form, acquired
    /// into a slot when a unit fires it (from disc once per floor, then from a saved copy of the slot), and preloaded into a
    /// free slot while there is one. The mod arms the cave's block (<see cref="CodeCaves.SharedShotBlock"/>), reports what it
    /// does from the block's event ring, and charges the randomizer's budget one image store per config a roster carries.
    /// docs/shot-slot-sharing.md has the design and its lessons.</summary>
    internal static class SharedShots
    {
        private const string Tag = "[SharedShots] ";
        /// <summary>Monster-pool units (16 B) a config costs the floor beyond its own data: the image store its slot bytes
        /// (0xA160) are saved to when it is swapped out, plus the store's 16-byte signature — a from-disc entry's region carries
        /// the same. The randomizer charges it once per config in a roster (<see cref="ImageCost"/>).</summary>
        internal const int ImageUnits = ShotEffectPack.SlotStride / 16 + 1;   // 2,583
        private static readonly string[] KindNames = { "", "entered from disc", "restored", "skipped", "no room", "entry failed" };
        private static uint _ringSeen;             // the ring's write index last read
        private static int  _lastFloor = -1;
        private static bool _wasInFloor, _seeded;
        private static int  _layoutTicks;          // ticks left before the floor's layout is reported (the load settles first)
        private static System.Threading.Thread _thread;

        internal static void Start()
        {
            if (_thread != null) return;
            _thread = new System.Threading.Thread(Loop) { IsBackground = true, Name = "SharedShots" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    if (!_seeded) Seed();
                    bool inFloor = Player.InDungeonFloor();
                    int floor = inFloor ? Memory.ReadUShort(Addresses.checkFloor) : -1;
                    if (inFloor && (!_wasInFloor || floor != _lastFloor))
                    {   // a new floor: the pool is rewound, so every store is stale by its signature — the table and the stamps
                        // start over anyway, and the magic is written again (an emulator restart leaves the block empty)
                        Memory.WriteBytesBatch(CodeCaves.SharedShotBlock + CodeCaves.SharedShotStamps,
                            new byte[CodeCaves.SharedShotRing - CodeCaves.SharedShotStamps]);
                        Memory.WriteInt(CodeCaves.SharedShotBlock, (int)CodeCaves.SharedShotMagic);
                        _layoutTicks = 6;
                    }
                    _wasInFloor = inFloor; _lastFloor = floor;
                    if (inFloor && _layoutTicks > 0 && --_layoutTicks == 0) LogLayout();
                    if (inFloor) DrainRing();
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                System.Threading.Thread.Sleep(250);
            }
        }

        /// <summary>The block armed: cleared, the headroom words left at 0 (the cave's own figures), the magic written last.</summary>
        private static void Seed()
        {
            Memory.WriteInt(CodeCaves.SharedShotBlock, 0);
            Memory.WriteBytesBatch(CodeCaves.SharedShotBlock + 4, new byte[CodeCaves.SharedShotBlockSize - 4]);
            Memory.WriteInt(CodeCaves.SharedShotBlock, (int)CodeCaves.SharedShotMagic);
            _ringSeen = 0; _seeded = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "block armed: the pack's five slots are shared among every config a floor needs");
        }

        /// <summary>The floor's pack and what waits outside it, from the species rows the loader wrote.</summary>
        private static void LogLayout()
        {
            long unit = EnemyAddresses.MainMonstorUnit.Base;
            int rows = Math.Min(Memory.ReadInt(unit + EnemyAddresses.MainMonstorUnit.SpeciesRowCount), 32);
            var slots = new StringBuilder();
            uint pack = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            for (int s = 0; s < ShotEffectPack.PackSlots; s++)
            {
                uint cfg = pack == 0 ? 0 : Memory.ReadUInt(0x20000000L + pack + s * ShotEffectPack.SlotStride);
                slots.Append(s == 0 ? "" : ", ").Append(s).Append(' ').Append(cfg == 0 ? "empty" : ConfigName(cfg));
            }
            var waiting = new List<string>();
            for (int r = 0; r < rows; r++)
            {
                long row = unit + EnemyAddresses.MainMonstorUnit.SpeciesRows + r * EnemySpeciesTable.Stride;
                string code = Encoding.ASCII.GetString(Memory.ReadBytesBatch(row, 4)).TrimEnd('\0');
                foreach (int off in new[] { EnemySpeciesTable.PrimaryBstIndex, EnemySpeciesTable.SecondaryBstIndex })
                {
                    short v = Memory.ReadShort(row + off);
                    if (v <= -2) waiting.Add($"{ConfigName(-v - 2)} ({code})");
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"floor {_lastFloor + 1}: pack slots {slots}; "
                + (waiting.Count == 0 ? "every config in the pack" : "waiting outside it: " + string.Join(", ", waiting)));
        }

        /// <summary>The cave's events since the last tick, in order.</summary>
        private static void DrainRing()
        {
            uint idx = Memory.ReadUInt(CodeCaves.SharedShotBlock + CodeCaves.SharedShotRingIndex);
            if (idx == _ringSeen) return;
            if (idx - _ringSeen > CodeCaves.SharedShotRingCount) _ringSeen = idx - CodeCaves.SharedShotRingCount;   // (older ones overwritten)
            for (; _ringSeen != idx; _ringSeen++)
            {
                byte[] e = Memory.ReadBytesBatch(CodeCaves.SharedShotBlock + CodeCaves.SharedShotRing + (_ringSeen % CodeCaves.SharedShotRingCount) * 16, 16);
                int kind = e[0], slot = (sbyte)e[1], cfgIn = (sbyte)e[2], cfgOut = (sbyte)e[3];
                uint frame = BitConverter.ToUInt32(e, 4), units = BitConverter.ToUInt32(e, 8);
                string what = kind < KindNames.Length ? KindNames[kind] : "?" + kind;
                string over = cfgOut >= 0 ? $" over {ConfigName(cfgOut)}" : "";
                string line = kind switch
                {
                    1 => $"{ConfigName(cfgIn)} {what} into slot {slot}{over}: {units:N0} units of the monster pool ({PoolState()})",
                    2 => $"{ConfigName(cfgIn)} {what} into slot {slot}{over} (a copy of its saved slot)",
                    3 => $"a shot of {ConfigName(cfgIn)} {what}: no slot to take (every slot has a sub-shot in flight)",
                    4 => $"{what} for {ConfigName(cfgIn)} in slot {slot}{over}: it needed {units:N0} units ({PoolState()})",
                    5 => $"{what}: {ConfigName(cfgIn)} into slot {slot}; {(cfgOut >= 0 ? ConfigName(cfgOut) + " put back" : "the slot is empty")}",
                    _ => $"event {kind}: slot {slot}, config in {cfgIn}, out {cfgOut}, {units}",
                };
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + line + $" — frame {frame}");
            }
        }

        private static string PoolState() =>
            $"pool {Memory.ReadInt(DataPools.Monstor + DataPools.Used):N0} of {Memory.ReadInt(DataPools.Monstor + DataPools.Cap):N0} used";

        private static string ConfigName(int index)
        {
            if (index < 0 || index >= ShotEffectPack.CfgCount) return "config " + index;
            var fx = BorrowedShots.TableConfig(index);
            return fx == null ? "config " + index : fx.Name;
        }

        private static string ConfigName(uint cfgAddr)
        {
            for (int i = 0; i < ShotEffectPack.CfgCount; i++)
                if (Memory.ReadUInt(ShotEffectPack.CfgTable + i * 4) == cfgAddr) return ConfigName(i);
            return $"config @0x{cfgAddr:X}";
        }

        /// <summary>The species' shot configs (the species table's two indices; none = 0xFFFF).</summary>
        private static IEnumerable<int> Configs(int ti)
        {
            long row = 0x20000000L + EnemySpeciesTable.TableBase + (long)ti * EnemySpeciesTable.Stride;
            foreach (int off in new[] { EnemySpeciesTable.PrimaryBstIndex, EnemySpeciesTable.SecondaryBstIndex })
            {
                int v = Memory.ReadUShort(row + off);
                if (v != 0xFFFF) yield return v;
            }
        }

        /// <summary>Pool units the image stores of <paramref name="ti"/>'s shot configs add to a roster: one <see cref="ImageUnits"/>
        /// per config the roster does not carry yet (<paramref name="carried"/>).</summary>
        internal static int ImageCost(int ti, HashSet<int> carried)
        {
            int cost = 0;
            foreach (int c in Configs(ti)) if (!carried.Contains(c)) cost += ImageUnits;
            return cost;
        }

        /// <summary>The species is in the roster: its configs are carried.</summary>
        internal static void NoteConfigs(int ti, HashSet<int> carried)
        {
            foreach (int c in Configs(ti)) carried.Add(c);
        }
    }
}
