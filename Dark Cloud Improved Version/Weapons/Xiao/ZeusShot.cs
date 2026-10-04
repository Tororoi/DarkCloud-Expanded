using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Sword of Zeus sphere — the sword's lightning from Xiao's slingshot, under its lighting
    /// (<see cref="FlashProfile"/>). Guard held <see cref="GuardSeconds"/> primes it; the release NOT locked on is the sword's
    /// volley (SwordOfZeus.StrikeNearest) with no pellet, LOCKED ON it opens a <see cref="ChainSeconds"/> window in which every
    /// pellet that reaches an enemy brings a bolt down on it. The shot charge (<see cref="ShotChargeSeconds"/>) marks the next
    /// pellet: the charge bolt comes down wherever it ends. A bolt pellet hurts nothing itself (<see cref="NoDamage"/> + the
    /// PelletPlant cave): the bolt is the hit. Solar Harvest and Big Bang's lock-on reach are inherited. (docs/sword-of-zeus.md)</summary>
    internal static class ZeusShot
    {
        /// <summary>Super Steve with a Sword of Zeus sphere (ZeusShot): the Sword of Zeus's look on her slingshot — its dim, its
        /// electric light, the two-second ease, no white on her while primed, no disc, no hit of its own (the bolts' blasts do).</summary>
        internal static readonly SunSword.SolarProfile FlashProfile = new SunSword.SolarProfile(
            Items.supersteve, 0f, null, SuperSteve.WeaponModel, 0, 0, "ZeusShot", fog: 0.8f,
            light: new[] { 228f, 240f, 255f }, fogRgb: new[] { 238f, 246f, 255f },
            primeDim: 0.5f, easeSeconds: 2.0, holdsPrimedTint: false);
        private const string Tag = "[ZeusShot] ";
        private const double GuardSeconds = 3.0, ShotChargeSeconds = 1.0;
        private const double PrimedSeconds = 10.0;                       // a charge left unused this long dissipates (as the sword's)
        private const double ChainSeconds = 5.0;                         // locked on: pellet hits strike for this long from the first release
        private const double TintFadeSeconds = 0.25, MissSeconds = 3.0;
        private const int    NoDamage = -1;                              // a pellet's damage word below zero: its contact plants nothing and ends it (the PelletPlant cave)

        private enum Guard { Idle, Charging, Primed, Chain }
        private sealed class Flight
        {
            internal int Slot; internal bool Charged; internal DateTime FiredAt;
            internal float X, H, Y;
        }
        private static readonly List<Flight> _flights = new List<Flight>();
        private static bool ChargedOut => _flights.Exists(f => f.Charged);   // a charged pellet is in the air
        private static bool _nativeWarned;
        private static int  _contactSeen = PelletContacts.Fresh;
        private static bool Native => (uint)Memory.ReadInt(0x20000000L + 0x001ABE04) == (0x0C000000u | (DebugIfCave.PelletPlant >> 2));   // the plant hook is in this ISO
        private static Guard _guard;
        private static DateTime _holdStart, _primedAt, _chainStart, _releasedAt, _volleyAt;
        private static bool  _bladeFading, _shotDimming, _volleyPending, _chainStruck;
        private static float _volleyDimFrom;
        private static readonly ShotCharge _shot = new ShotCharge();     // the shot charge (the next pellet marked), her release, and the retire mark (the volley's release: its pellet is retired as it appears)
        private static readonly PelletWatch _pellets = new PelletWatch();
        private static byte _floor = 0xFF;

        /// <summary>The bolt, while Xiao is out with Super Steve carrying a Sword of Zeus sphere (BorrowedShots asks every tick).</summary>
        internal static BorrowedEffect WantedShot() => PelletWatch.SuperSteveSphereOn(Items.swordofzeus) ? SwordOfZeus.Lightning() : null;

        internal static void Drive(bool active)
        {
            SunSword.BlindTick();
            BlastFalloff.ExpireShells();                                                        // the bolts' blast entries, once spent
            if (!active) return;
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _floor) { if (_floor != 0xFF) { EndFlights(); Dissipate(); } _floor = floor; }
            if (SwordOfZeus.LightningSeeded) SwordOfZeus.MaintainScale();
            var p = ZeusShot.FlashProfile;
            SceneLighting.ToanTintOwned = _guard == Guard.Charging;                      // the cyan build-up is hers while she charges; Zeus holds no primed white

            // The shot charge, as her other charged shots: held ShotChargeSeconds, the charge-complete flash, and the next pellet
            // marked (the guard charge owns the tint while it runs). The room darkens as it builds, as it does under the sword's
            // own charge attack; a release that is not the charge lifts it (a primed guard charge keeps its own).
            _shot.Tick(ShotChargeSeconds, rampTint: _guard != Guard.Charging);
            if (_shot.Holding)
            {
                if (_guard == Guard.Idle || _guard == Guard.Chain)
                { _shotDimming = true; SceneLighting.BeginDim(); SceneLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, _shot.Held / ShotChargeSeconds)); }
            }
            else if (_shotDimming && !_shot.Charged && !ChargedOut) { _shotDimming = false; SceneLighting.EndDim(); }   // let go short of the charge: the dim lifts
            if (ChargedOut && _guard != Guard.Primed) { SceneLighting.BeginDim(); SceneLighting.DimTo(p.PrimeDim); }   // the charge's dim held while its pellet flies, to the bolt
            bool released = _shot.Released;

            if (_volleyPending)                                                            // the release with no lock: the plunge, then the volley and its flash
            {
                double frames = (GameClock.Now - _volleyAt).TotalSeconds * 60.0;
                SceneLighting.DimRamp(_volleyDimFrom, (float)(frames / SceneLighting.RampFrames));
                if (frames >= SceneLighting.RampFrames)
                {
                    _volleyPending = false;
                    int struck = SwordOfZeus.StrikeNearest(weapon: Items.supersteve);
                    SunSword.StrikeFlash(p);                                                // the white easing back over two seconds, her pulse, the blinding
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"volley on release: {struck} bolt(s)");
                }
            }

            TrackFlights(p);

            switch (_guard)
            {
                case Guard.Idle:
                    if (_bladeFading)
                    {
                        float k = (float)Math.Max(0.0, 1.0 - (GameClock.Now - _releasedAt).TotalSeconds / TintFadeSeconds);
                        BladeTint.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                        if (k <= 0f) { _bladeFading = false; BladeTint.Clear(); }
                    }
                    if (!SunSword.BlindRunning && GuardWatch.IsGuarding()) { _guard = Guard.Charging; _holdStart = GameClock.Now; _bladeFading = false; }
                    break;
                case Guard.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding()) { Dissipate(); break; }
                    double held = (GameClock.Now - _holdStart).TotalSeconds;
                    GuardCharge.Frame(p, held, GuardSeconds);                                 // its profile dims (PrimeDim 0.5)
                    if (held >= GuardSeconds)
                    {
                        _guard = Guard.Primed; _primedAt = GameClock.Now; ChargeTint.Clear();
                        Player.FlashChargeComplete();                                            // the stock charge-complete flash on her: the cue that it is ready (Zeus puts no white on its wielder)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — a release locked on opens the strike window; not locked on, the volley");
                    }
                    break;
                }
                case Guard.Primed:
                    BladeTint.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SceneLighting.BeginDim(); SceneLighting.DimTo(p.PrimeDim);
                    if ((GameClock.Now - _primedAt).TotalSeconds >= PrimedSeconds)
                    { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charge went unused — dissipating"); Dissipate(); break; }
                    if (released)
                    {
                        _releasedAt = GameClock.Now;
                        if (PlayerAction.LockHeld(out int target) && target < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(target))
                        {   // locked on: the window opens; the pellet flies and reaching an enemy is the bolt. The prime dim holds until the first bolt.
                            _guard = Guard.Chain; _chainStart = GameClock.Now; _chainStruck = false;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"released locked on slot {target} — bolts on every pellet hit for {ChainSeconds:0} s");
                        }
                        else
                        {   // not locked on: the volley, from where she stands, with no pellet
                            _guard = Guard.Idle; _bladeFading = true; _shot.RetirePellet = true; _shot.Charged = false;
                            _volleyPending = true; _volleyAt = GameClock.Now; _volleyDimFrom = SceneLighting.LastDim;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "released with no lock — the volley");
                        }
                    }
                    break;
                case Guard.Chain:
                    BladeTint.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);           // primed through the window
                    if (!_chainStruck) { SceneLighting.BeginDim(); SceneLighting.DimTo(p.PrimeDim); }   // the prime dim, until the first bolt's flash takes it over
                    if ((GameClock.Now - _chainStart).TotalSeconds >= ChainSeconds)
                    {
                        _guard = Guard.Idle; _bladeFading = true; _releasedAt = GameClock.Now;
                        if (!_chainStruck) SceneLighting.EndDim();                              // no bolt came: the dim lifts
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "strike window over — the charge is spent");
                    }
                    break;
            }

            // A new pellet: retired (the volley's release), or tracked — the shot charge's is the charge bolt's.
            int slot = _pellets.NewPellet();
            if (slot < 0) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (_shot.RetirePellet)
            {
                if (Memory.IsValidGuest(pool)) Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);
                _shot.RetirePellet = false; return;
            }
            long pa = PlayerShotPool.PosAddr(pool, slot);
            var f = new Flight { Slot = slot, Charged = _shot.Charged, FiredAt = GameClock.Now, X = Memory.ReadFloat(pa), H = Memory.ReadFloat(pa + 4), Y = Memory.ReadFloat(pa + 8) };
            if (_shot.Charged) { _shot.Charged = false; ChargeTint.Clear(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged pellet: slot {slot} — the charge bolt where it ends"); }
            if (f.Charged || _guard == Guard.Chain)
            {
                if (_flights.Count == 0) PelletContacts.Sync(ref _contactSeen);            // nothing was flying: only contacts from here on count
                Memory.WriteInt(PlayerShotPool.DamageAddr(pool, slot), NoDamage);          // a pellet that calls a bolt hurts nothing itself: its contact just ends it
                if (!Native && !_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the pellet-plant cave is not in this ISO — bolt pellets land a hit of their own (re-patch the ISO)"); }
                _flights.Add(f);
            }
        }

        /// <summary>Every tracked pellet: the tick it has ended — its contact (planting nothing, by the cave), a wall, its range —
        /// the bolt comes down on the enemy it ended on (the window, or the charge); the charge's pellet ending on nothing brings
        /// its bolt down where it ended. The flash follows the bolt at once — no plunge: the prime dim (or a flash still easing)
        /// is what it goes off from.</summary>
        private static void TrackFlights(SunSword.SolarProfile p)
        {
            if (_flights.Count == 0) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            // The engine's own contact record (the pellet-contact cave): the enemy a tracked pellet met, by its hit sphere.
            int contactSlot = -1, contactEnemy = -1;
            if (PelletContacts.Poll(ref _contactSeen, out var c) && c.Enemy) { contactSlot = c.Slot; contactEnemy = PelletContacts.EnemyAtSphere(c); }
            foreach (Flight f in _flights.ToArray())
            {
                double since = (GameClock.Now - f.FiredAt).TotalSeconds;
                bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, f.Slot)) != 0;
                if (live && f.Slot != contactSlot)
                {
                    long pa = PlayerShotPool.PosAddr(pool, f.Slot);
                    f.X = Memory.ReadFloat(pa); f.H = Memory.ReadFloat(pa + 4); f.Y = Memory.ReadFloat(pa + 8);
                    if (since < MissSeconds) continue;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "pellet still out after 3 s — let go");
                    _flights.Remove(f); if (f.Charged && _shotDimming) { _shotDimming = false; SceneLighting.EndDim(); }
                    continue;
                }
                int hit = f.Slot == contactSlot && contactEnemy >= 0 ? contactEnemy : PelletWatch.EnemyAt(f.X, f.Y);   // the engine's word first
                _flights.Remove(f);
                // A pellet is tracked only if it left inside the window (or charged), so a window that lapsed while it flew still owes
                // it its bolt: only newly released shots stop calling lightning.
                if (!f.Charged && hit < 0) continue;
                bool struck = hit >= 0 ? SwordOfZeus.Strike(hit, weapon: Items.supersteve)
                                       : SwordOfZeus.StrikeAt(f.X, f.H, f.Y, -1, weapon: Items.supersteve);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (f.Charged ? "charged pellet" : "pellet") + $" ended at ({f.X:F0},{f.H:F0},{f.Y:F0})" + (hit >= 0 ? $" on slot {hit}" : "") + (struck ? " — the bolt" : " — no bolt was free: the flash alone"));
                if (!struck && _guard != Guard.Chain) SceneLighting.EndDim();
                _chainStruck = true; _shotDimming = false;
                SunSword.StrikeFlash(p);
            }
        }

        private static void EndFlights() { _flights.Clear(); }

        /// <summary>The guard charge spent with nothing to show: the slingshot's white off, the dim lifted.</summary>
        private static void Dissipate()
        {
            BladeTint.Clear(); ChargeTint.Clear(); SceneLighting.EndDim();
            _guard = Guard.Idle; _bladeFading = false; _volleyPending = false; _chainStruck = false;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended.</summary>
        internal static void Stop()
        {
            EndFlights();
            BladeTint.Clear(); ChargeTint.Clear(); SceneLighting.Restore();
            SunSword.EndBlinding();
            SceneLighting.ToanTintOwned = false;
            _pellets.Reset(); _shot.Reset();
            _guard = Guard.Idle; _bladeFading = false; _shotDimming = false; _volleyPending = false;
        }
    }
}
