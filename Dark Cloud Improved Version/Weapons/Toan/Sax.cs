using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sax — "Fine Fare": while Toan carries the Sax, every water a floor's chests hold is Premium Water and every
    /// food is Premium Chicken. The Dusack carries the same and, on top, turns every Treasure Key in a chest into Gold Bullion;
    /// the 7 Branch Sword carries the Dusack's and, on top, gives each Repair Powder one chance in four of being Auto Repair
    /// Powder; the Atlamillia Sword and the Chronicle Sword carry the 7 Branch Sword's.
    ///
    /// Chest contents are settled once, when the floor is built (the engine's tables, then the mod's randomizer over them,
    /// for the front and the back floor's map both), so the work is done once per floor: <see cref="OnFloorChestsReady"/>,
    /// called right after the randomizer, reads every placed item chest on both maps (ChestAddresses.ChestSlots: 24 boxes a
    /// map, the item id in the box's first word, size 1 = an item chest) and PLANS what each qualifying one becomes — the
    /// chance rolls made there, once per chest per floor. <see cref="Apply"/> writes the plan's rows the equipped weapon
    /// carries and <see cref="Revert"/> puts the originals back; they run when the floor is built with a qualifying sword out,
    /// when such a sword is drawn (<see cref="FineFareEffect"/>, from WeaponThreads), and when it is put away or another
    /// character with a plain weapon takes over. Super Steve carrying any of the five swords' SynthSpheres has that sword's form
    /// (<see cref="DriveSphere"/>). Weapon chests and mimic boxes (their first word is an enemy slot index) never match; an
    /// opened chest is left alone either way.</summary>
    internal static class Sax
    {
        private const int TickMs = 250;

        /// <summary>A chest upgrade: what is found, what it becomes, and the odds (one in <see cref="OneIn"/>; 1 = always).</summary>
        private readonly struct Upgrade
        {
            public readonly int From, To, OneIn;
            public Upgrade(int from, int to, int oneIn = 1) { From = from; To = to; OneIn = oneIn; }
        }

        // Every row any of the swords carries, with the rank of the sword it first appears on: Sax 0, Dusack 1, 7 Branch 2.
        private static readonly (Upgrade row, int rank)[] Rows =
        {
            (new Upgrade(Items.regularwater, Items.premiumwater),   0), (new Upgrade(Items.tastywater, Items.premiumwater), 0),
            (new Upgrade(Items.bread,        Items.premiumchicken), 0), (new Upgrade(Items.cheese,     Items.premiumchicken), 0),
            (new Upgrade(Items.treasurechestkey, Items.goldbullion), 1),
            (new Upgrade(Items.repairpowder, Items.autorepairpowder, oneIn: 4), 2),
        };

        /// <summary>The rank of the rows a weapon carries: 0 the Sax's, 1 the Dusack's, 2 the 7 Branch Sword's (and the swords
        /// that inherit its form); −1 for a weapon that carries none.</summary>
        private static int RankOf(int weaponId) =>
            weaponId == Items.sax ? 0 : weaponId == Items.dusack ? 1 :
            weaponId == Items.sevenbranchsword || weaponId == Items.atlamilliasword || weaponId == Items.chroniclesword ? 2 : -1;

        /// <summary>Whether a weapon carries Fine Fare in any form.</summary>
        internal static bool Grants(int weaponId) => RankOf(weaponId) >= 0;

        /// <summary>Every dispatch tick while Super Steve is out: the chests at the form of the sword her sphere came from, or the
        /// originals when the sphere is none of the five (or the dispatch is going down). Toan's thread and this never run at
        /// once — his sword is sheathed while she is out.</summary>
        internal static void DriveSphere(int sphere, bool active) => Apply(active ? RankOf(sphere) : -1);

        /// <summary>One planned chest: which map's box, what it held, what it becomes, and the rank of sword that gets it.</summary>
        private readonly struct Planned
        {
            public readonly long IdAddr; public readonly int Box, From, To, Rank;
            public Planned(long idAddr, int box, int from, int to, int rank) { IdAddr = idAddr; Box = box; From = from; To = to; Rank = rank; }
        }

        private static readonly object _gate = new();
        private static readonly List<Planned> _plan = new();
        private static int _appliedRank = -1;   // the rank the chests currently hold (−1 = originals)
        private static readonly Random _rng = new();

        /// <summary>Right after the floor's chests are written: the plan for this floor, and the upgrade applied if the sword out
        /// carries it.</summary>
        internal static void OnFloorChestsReady()
        {
            lock (_gate)
            {
                _plan.Clear();
                _appliedRank = -1;
                foreach (long map in new[] { DungeonAddresses.Map.MainDungeonMap, DungeonAddresses.Map.UraDungeonMap })
                    for (int box = 0; box < ChestAddresses.ChestSlots.Capacity; box++)
                    {
                        long b = map + ChestAddresses.ChestSlots.TableOffset + (long)box * ChestAddresses.ChestSlots.Stride;
                        if (Memory.ReadInt(b + ChestSlotOffsets.ActiveFlag) != 1) continue;    // not a placed chest
                        if (Memory.ReadInt(b + ChestSlotOffsets.ChestSize) != 1) continue;     // a weapon chest
                        int id = Memory.ReadInt(b + ChestSlotOffsets.EntityId);
                        foreach (var (row, rank) in Rows)
                        {
                            if (id != row.From) continue;
                            if (row.OneIn > 1 && _rng.Next(row.OneIn) != 0)
                            {
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Sax] {(map == DungeonAddresses.Map.UraDungeonMap ? "back-floor " : "")}chest {box}: {Item.GetName((ushort)row.From)} stays (one in {row.OneIn} missed)");
                                break;
                            }
                            _plan.Add(new Planned(b + ChestSlotOffsets.EntityId, box, row.From, row.To, rank));
                            break;
                        }
                    }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Sax] floor chests read: {_plan.Count} to upgrade");
                int wid = Player.Weapon.GetCurrentWeaponId();
                Apply(RankOf(wid == Items.supersteve ? SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) : wid));   // her sphere's form, when it is she who is out
            }
        }

        /// <summary>The plan's rows of rank ≤ <paramref name="rank"/> written (a chest still holding its original and still shut);
        /// a lower rank than the one applied first reverts. −1 = the originals back.</summary>
        private static void Apply(int rank)
        {
            lock (_gate)
            {
                if (rank == _appliedRank) return;
                if (_appliedRank >= 0) Revert();
                if (rank < 0) return;
                int done = 0;
                foreach (Planned p in _plan)
                {
                    if (p.Rank > rank) continue;
                    if (Memory.ReadInt(p.IdAddr + ChestSlotOffsets.ActiveFlag) != 1 || Memory.ReadInt(p.IdAddr) != p.From) continue;   // opened, or not as read
                    Memory.WriteInt(p.IdAddr, p.To);
                    done++;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Sax] chest {p.Box}: {Item.GetName((ushort)p.From)} → {Item.GetName((ushort)p.To)}");
                }
                _appliedRank = rank;
                if (done == 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Sax] nothing left to upgrade on this floor");
            }
        }

        /// <summary>Every upgraded chest that is still shut and still holds the upgrade back to what it held.</summary>
        private static void Revert()
        {
            lock (_gate)
            {
                if (_appliedRank < 0) return;
                int done = 0;
                foreach (Planned p in _plan)
                {
                    if (p.Rank > _appliedRank) continue;
                    if (Memory.ReadInt(p.IdAddr + ChestSlotOffsets.ActiveFlag) != 1 || Memory.ReadInt(p.IdAddr) != p.To) continue;
                    Memory.WriteInt(p.IdAddr, p.From);
                    done++;
                }
                _appliedRank = -1;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Sax] sword away: {done} chest(s) back to what they held");
            }
        }

        /// <summary>While a Fine Fare sword is the active character's weapon on a floor: the upgrade applied on draw (and re-applied
        /// on a swap between two of the swords), the originals back when it goes or another character takes over.</summary>
        public static void FineFareEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Sax] fine fare: chest water → Premium Water, bread and cheese → Premium Chicken; Dusack + Treasure Keys → Gold Bullion; 7 Branch (Atlamillia, Chronicle) + Repair → Auto Repair one time in four");
            for (int rank = RankOf(Player.Weapon.GetCurrentWeaponId()); rank >= 0 && Player.InDungeonFloor(); rank = RankOf(Player.Weapon.GetCurrentWeaponId()))
            {
                Apply(rank);
                Thread.Sleep(TickMs);
            }
            Revert();
        }
    }
}
