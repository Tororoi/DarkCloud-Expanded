using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Sword of Zeus sphere — the sword's lightning from Xiao's slingshot, its lighting throughout
    /// (SunSword.ZeusShotFlash: the 0.5 dim, the electric white, the two-second ease back to the floor's own light, no white on
    /// her, no disc). Hold guard for <see cref="GuardSeconds"/> and the slingshot whitens as the room darkens; primed, the
    /// charge holds up to <see cref="PrimedSeconds"/> unused. Her RELEASE then goes one of two ways. NOT locked on: the
    /// sword's volley — a bolt on each of the nearest enemies in reach (SwordOfZeus.StrikeNearest) and the strike's flash — with
    /// no pellet (retired as it appears). LOCKED ON: the pellet flies, and for <see cref="ChainSeconds"/> from that first
    /// release every pellet that lands on an enemy brings a bolt down on it — the strike, its blast at half Big Bang's steps, the
    /// flash — a window that does not reset (the sword's chain lasts its combo; hers is the clock, so shots can follow fast).
    /// The dim is not held through the window: each flash eases back at its own cadence and the next plunge overrides it.
    /// THE SHOT CHARGE (the shot held <see cref="ShotChargeSeconds"/>, as her other charged shots are made; the room darkens
    /// as it builds, as the sword's charge attack darkens it) marks the next pellet: wherever it dies — on an enemy or not — the
    /// charge bolt comes down there with the strike's flash, locked on or not. Bolts bill the weapon as the sword's do
    /// (SwordOfZeus.StrikeWhp each; a volley once). Solar Harvest and Big Bang's lock-on reach are inherited alongside.</summary>
    internal static class ZeusShot
    {
        private const string Tag = "[ZeusShot] ";
        private const double GuardSeconds = 3.0, ShotChargeSeconds = 1.0;
        private const double PrimedSeconds = 10.0;                       // a charge left unused this long dissipates (as the sword's)
        private const double ChainSeconds = 5.0;                         // locked on: pellet hits strike for this long from the first release
        private const double TintFadeSeconds = 0.25, MissSeconds = 3.0;
        private const float  HitProximity = 40f;                         // an enemy this close to where a pellet died = it landed on one

        private enum Guard { Idle, Charging, Primed, Chain }
        private sealed class Flight
        {
            internal int Slot; internal bool Charged; internal DateTime FiredAt, HitAt; internal bool Pending; internal int HitSlot = -1;
            internal float X, H, Y, DimFrom;
        }
        private static readonly List<Flight> _flights = new List<Flight>();
        private static Guard _guard;
        private static DateTime _holdStart, _primedAt, _chainStart, _releasedAt, _shotHoldStart, _volleyAt;
        private static bool  _bladeFading, _shotHolding, _shotCharged, _shotDimming, _wasShooting, _retirePellet, _volleyPending;
        private static float _volleyDimFrom;
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static readonly List<(int slot, int ticks)> _planted = new List<(int, int)>();
        private static byte _floor = 0xFF;

        /// <summary>The bolt, while Xiao is out with Super Steve carrying a Sword of Zeus sphere (BorrowedShots asks every tick).</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.XiaoId || Player.Weapon.GetCurrentWeaponId() != Items.supersteve) return null;
            int slot = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.XiaoId));
            if (slot < 0 || slot >= DngStatusData.MaxWeaponSlots || SuperSteve.AttachedSphere(DngStatusData.WeaponRecord(Player.XiaoId, slot)) != Items.swordofzeus) return null;
            return SwordOfZeus.Lightning();
        }

        internal static void Drive(bool active)
        {
            SunSword.BlindTick();
            SunSword.ExpireHits(_planted);
            BigBang.ExpireShells();                                                        // the bolts' blast entries, once spent
            if (!active) return;
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _floor) { if (_floor != 0xFF) { EndFlights(); Dissipate(); } _floor = floor; }
            if (SwordOfZeus.LightningSeeded) SwordOfZeus.MaintainScale();
            var p = SunSword.ZeusShotFlash;
            SolarLighting.ToanTintOwned = _guard == Guard.Charging;                      // the cyan build-up is hers while she charges; Zeus holds no primed white

            // The shot charge, as her other charged shots: held ShotChargeSeconds, the charge-complete flash, and the next pellet
            // marked. The room darkens as it builds, as it does under the sword's own charge attack; a release that is not the
            // charge lifts it (a primed guard charge keeps its own).
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!_shotHolding) { _shotHolding = true; _shotCharged = false; _shotHoldStart = GameClock.Now; }
                double held = (GameClock.Now - _shotHoldStart).TotalSeconds;
                if (!_shotCharged && held >= ShotChargeSeconds) { _shotCharged = true; Player.FlashChargeComplete(); }
                if (_guard != Guard.Charging) ChargeTint.Ramp(_shotCharged ? 0 : ShotChargeSeconds - held);
                if (_guard == Guard.Idle || _guard == Guard.Chain)
                { _shotDimming = true; SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / ShotChargeSeconds)); }
            }
            else
            {
                if (_shotHolding && _guard != Guard.Charging) ChargeTint.Clear();          // released: _shotCharged stays for the pellet
                _shotHolding = false;
                if (_shotDimming && !_shotCharged) { _shotDimming = false; SolarLighting.EndDim(); }   // let go short of the charge: the dim lifts
            }
            bool shooting = shotState == PlayerAction.XiaoShotShoot, released = shooting && !_wasShooting;
            _wasShooting = shooting;
            if (!shooting) _retirePellet = false;

            if (_volleyPending)                                                            // the release with no lock: the plunge, then the volley and its flash
            {
                double frames = (GameClock.Now - _volleyAt).TotalSeconds * 60.0;
                SolarLighting.DimRamp(_volleyDimFrom, (float)(frames / SolarLighting.RampFrames));
                if (frames >= SolarLighting.RampFrames)
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
                        SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                        if (k <= 0f) { _bladeFading = false; SolarBlade.Clear(); }
                    }
                    if (!SunSword.BlindRunning && GuardWatch.IsGuarding()) { _guard = Guard.Charging; _holdStart = GameClock.Now; _bladeFading = false; }
                    break;
                case Guard.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding()) { Dissipate(); break; }
                    double held = (GameClock.Now - _holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / GuardSeconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / GuardSeconds));
                    ChargeTint.Ramp(GuardSeconds - held);
                    if (held >= GuardSeconds)
                    {
                        _guard = Guard.Primed; _primedAt = GameClock.Now; ChargeTint.Clear();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — a release locked on opens the strike window; not locked on, the volley");
                    }
                    break;
                }
                case Guard.Primed:
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim);
                    if ((GameClock.Now - _primedAt).TotalSeconds >= PrimedSeconds)
                    { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charge went unused — dissipating"); Dissipate(); break; }
                    if (released)
                    {
                        _releasedAt = GameClock.Now;
                        if (PlayerAction.LockHeld(out int target) && target < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(target))
                        {   // locked on: the window opens; the pellet flies and its landing is the bolt. The prime dim lifts: each flash eases at its own pace from here.
                            _guard = Guard.Chain; _chainStart = GameClock.Now;
                            SolarLighting.EndDim();
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"released locked on slot {target} — bolts on every pellet hit for {ChainSeconds:0} s");
                        }
                        else
                        {   // not locked on: the volley, from where she stands, with no pellet
                            _guard = Guard.Idle; _bladeFading = true; _retirePellet = true; _shotCharged = false;
                            _volleyPending = true; _volleyAt = GameClock.Now; _volleyDimFrom = SolarLighting.LastDim;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "released with no lock — the volley");
                        }
                    }
                    break;
                case Guard.Chain:
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);           // primed through the window
                    if ((GameClock.Now - _chainStart).TotalSeconds >= ChainSeconds)
                    {
                        _guard = Guard.Idle; _bladeFading = true; _releasedAt = GameClock.Now;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "strike window over — the charge is spent");
                    }
                    break;
            }

            // A new pellet: retired (the volley's release), or tracked — the shot charge's is the charge bolt's.
            int slot = NewPellet();
            if (slot < 0) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (_retirePellet)
            {
                if (Memory.IsValidGuest(pool)) Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);
                _retirePellet = false; return;
            }
            long pa = PlayerShotPool.PosAddr(pool, slot);
            var f = new Flight { Slot = slot, Charged = _shotCharged, FiredAt = GameClock.Now, X = Memory.ReadFloat(pa), H = Memory.ReadFloat(pa + 4), Y = Memory.ReadFloat(pa + 8) };
            if (_shotCharged) { _shotCharged = false; ChargeTint.Clear(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged pellet: slot {slot} — the charge bolt where it dies"); }
            if (f.Charged || _guard == Guard.Chain) _flights.Add(f);
        }

        /// <summary>Every tracked pellet: dying on an enemy (the window) or anywhere (the charge), the plunge to black over the
        /// ramp's frames, then the bolt — on the enemy's ground point, or the charge's where the pellet died — and the flash.</summary>
        private static void TrackFlights(SunSword.SolarProfile p)
        {
            if (_flights.Count == 0) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            foreach (Flight f in _flights.ToArray())
            {
                if (f.Pending)
                {
                    double frames = (GameClock.Now - f.HitAt).TotalSeconds * 60.0;
                    SolarLighting.DimRamp(f.DimFrom, (float)(frames / SolarLighting.RampFrames));
                    if (frames < SolarLighting.RampFrames) continue;
                    _flights.Remove(f);
                    bool struck = f.HitSlot >= 0 ? SwordOfZeus.Strike(f.HitSlot, weapon: Items.supersteve)
                                                 : SwordOfZeus.StrikeAt(f.X, f.H, f.Y, -1, weapon: Items.supersteve);
                    if (!struck) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no bolt was free — the flash alone"); SolarLighting.EndDim(); }
                    if (_shotDimming) _shotDimming = false;
                    SunSword.StrikeFlash(p);
                    continue;
                }
                double since = (GameClock.Now - f.FiredAt).TotalSeconds;
                bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, f.Slot)) != 0;
                if (live)
                {
                    long pa = PlayerShotPool.PosAddr(pool, f.Slot);
                    f.X = Memory.ReadFloat(pa); f.H = Memory.ReadFloat(pa + 4); f.Y = Memory.ReadFloat(pa + 8);
                    if (since < MissSeconds) continue;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "pellet still out after 3 s — let go");
                    _flights.Remove(f); if (f.Charged && _shotDimming) { _shotDimming = false; SolarLighting.EndDim(); }
                    continue;
                }
                f.HitSlot = EnemyAt(f.X, f.Y);
                if (f.Charged || (f.HitSlot >= 0 && _guard == Guard.Chain && (GameClock.Now - _chainStart).TotalSeconds < ChainSeconds))
                {
                    f.Pending = true; f.HitAt = GameClock.Now; f.DimFrom = SolarLighting.LastDim;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (f.Charged ? "charged pellet" : "pellet") + $" died at ({f.X:F0},{f.H:F0},{f.Y:F0})" + (f.HitSlot >= 0 ? $" on slot {f.HitSlot}" : "") + " — the bolt follows");
                    continue;
                }
                _flights.Remove(f);
            }
        }

        /// <summary>The nearest live enemy within HitProximity of (x, y), by its own position; −1 for none.</summary>
        private static int EnemyAt(float x, float y)
        {
            int best = -1; float bestD = HitProximity * HitProximity;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long up = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(up) - x, dy = Memory.ReadFloat(up + 8) - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>The first pellet to appear since the last look (a new live pool slot), or −1.</summary>
        private static int NewPellet()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return -1;
            int found = -1;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i]) { _seen[i] = true; if (found < 0) found = i; }
                else if (!live) _seen[i] = false;
            }
            return found;
        }

        private static void EndFlights() { _flights.Clear(); }

        /// <summary>The guard charge spent with nothing to show: the slingshot's white off, the dim lifted.</summary>
        private static void Dissipate()
        {
            SolarBlade.Clear(); ChargeTint.Clear(); SolarLighting.EndDim();
            _guard = Guard.Idle; _bladeFading = false; _volleyPending = false;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended.</summary>
        internal static void Stop()
        {
            EndFlights();
            SolarBlade.Clear(); ChargeTint.Clear(); SolarLighting.Restore();
            SunSword.EndBlinding();
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in _planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            _planted.Clear();
            SolarLighting.ToanTintOwned = false;
            Array.Clear(_seen, 0, _seen.Length);
            _guard = Guard.Idle; _bladeFading = false; _shotHolding = false; _shotCharged = false; _shotDimming = false; _wasShooting = false; _retirePellet = false; _volleyPending = false;
        }
    }
}
