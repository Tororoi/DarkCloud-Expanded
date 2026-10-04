using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.BombCarrier;
using static Dark_Cloud_Improved_Version.PelletHide;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Big Bang sphere — "Detonate" from Xiao's slingshot. A <see cref="GuardSeconds"/> guard charge whitens
    /// the slingshot and, locked on, hangs a four-times Bomb over the target (the judgement blade's hover, pulsing red); her release
    /// drops it for the bomb's blast with Big Bang's falloff damage, kick and flash, or — nothing hanging — flashes from where she
    /// stands. Every pellet flies as Witch Illza's apple shot wearing the Bomb's mesh (<see cref="BombCarrier"/>) and bursts where it
    /// dies: a plain shot with the pellet's own damage, a shot held <see cref="ShotChargeSeconds"/> with half the drop's blast; her
    /// pellets themselves are drawn from the blank cell (<see cref="PelletHide"/>). Driven from Super Steve's sphere dispatch; the
    /// helpers share this class's members through using static (docs/big-bang.md).</summary>
    internal static class BigBangShot
    {
        /// <summary>Super Steve with a Big Bang sphere (BigBangShot): Big Bang's flash — twice the Sun Sword's share, the cool
        /// light — from where her bomb lands; no dim while she primes (the room darkens only along the fall, or the four frames
        /// before a shot's flash); no disc on her (the bomb carries its own).</summary>
        internal static readonly SunSword.SolarProfile FlashProfile = new SunSword.SolarProfile(
            Items.supersteve, 0.50f, null, SuperSteve.WeaponModel, 0, 0, "BigBangShot", fog: 0.8f,
            light: new[] { 236f, 226f, 255f }, fogRgb: new[] { 242f, 236f, 255f },
            primeDim: 0f, bladeGlowOnly: true);                                     // no dim while it primes: the darkening is the fall's, or the four frames before a shot's flash
        private const string Tag = "[BigBangShot] ";
        private const double GuardSeconds = 3.0, ShotChargeSeconds = 1.0;
        private const float  HoverScale = 4f;                          // the bomb over a target
        private const float  HoverFxScale = 2f, BombFxScale = 1.1f, PelletFxScale = 0.5f;   // the blast sprites: the drop, the charged shot, the plain shot; each ring reaches its blast's edge
        internal const float ShotBombScale = 2f, PelletBombScale = 1f;  // the bomb on the apple shot, over the apple's own size (the sub-shot object's own scale): charged, plain
        private const float  HoverMargin = 10f;                         // the hanging bomb's bottom this far above the species' authored height (Big Bang's blade: 6)
        private const float  ContactPad = 3f;                           // the bomb shot touches an enemy this far outside its body width
        // THE BOMB'S PULSE: red, 150,0,0, while the hanging bomb fades in; from the frame it is fully in, a smooth cosine
        // between that red and black — black every half second, red every second — with no cut anywhere.
        private static readonly float[] PulseRed = { 150f, 0f, 0f };
        private const double PulseSeconds = 1.0;
        private static DateTime _pulseStart;
        private static bool _pulsing;
        private const float  BombLength = 1.22f;                        // bakudan.mds: its mesh runs 1.22 below the root to 1.98 above it at 1×, so this is root-to-bottom — the drop stops with the bottom ON the root
        private const int    CarrierConfig = 4;                         // the shot table's `ringo_ex`: Witch Illza's thrown apple (its flight motion turns the bomb as the apple turns)
        internal const string CarrierNode = "dokuring__m";               // the node that draws the apple in each of its trees (the impact's light, `hikari04__bacapp`, sits beside it)
        private const float  BombDamage = 0.5f, BombKick = 0.5f, BombReach = 0.5f;            // the charged shot's blast against the drop's (the kick as DISTANCE)
        private const float  PelletKick = 0.25f;                        // the plain shot's kick DISTANCE, of the drop's (half the charged shot's); its damage is the pellet's own
        /// <summary>The kick strength that throws <paramref name="distanceFraction"/> as far as the drop's: distance ≈ strength² / (2 · decay).</summary>
        private static float KickFor(float distanceFraction) => (float)Math.Sqrt(distanceFraction);
        private const float  DropFxLift = 2f;                            // the drop's blast drawn this far above the landing, so its ring is not flat on the floor
        private const float  DropWhp = 20f, BombWhp = 10f, FlashWhp = 5f, SwingBase = 1.5f;
        private const double TintFadeSeconds = 0.25, MissSeconds = 3.0;
        private const double CutWait = 0.1;                             // how long a plain shot out of its flight waits for the engine's impact plant before it is cut regardless
        private const string GlowDisc = "catglowp";
        private const int    GlowFireRow = 1;                           // the cave's one-based row: the Matador's red-orange, on the bomb
        private const float  BombGlowSize = 0.6f;

        private static readonly JudgementBlade.JudgementOwner Owner = new JudgementBlade.JudgementOwner
        {
            WeaponId = Items.supersteve, Glow = GlowDisc, Profile = null, Redirect = true, RampWholeFall = true,
            IsPrimed = () => _guard == Guard.Charging || _guard == Guard.Primed || _guard == Guard.Dropping,   // hung from the charge's start, fading in with it
            Alpha = () => _guard == Guard.Charging ? (float)Math.Min(1.0, (GameClock.Now - _holdStart).TotalSeconds / GuardSeconds) : 1f,
            Scale = HoverScale, Length = () => BombLength, SpawnRoot = BombModel.Root, Upright = true,
            GlowRow = GlowFireRow, GlowScale = BombGlowSize, GlowLift = 0f, Margin = HoverMargin,
            Land = LandDrop,
        };

        private enum Guard { Idle, Charging, Primed, Dropping }
        private enum Shot  { Bomb, Pellet }                            // a shot in flight: the charged bomb, the plain bomb
        /// <summary>A shot in the air: the pool pellet it began as (<see cref="Slot"/>), the apple sub-shot carrying its bomb
        /// (<see cref="Sub"/>, −1 for a pellet flying plain), its disc's node, and where it was last seen.</summary>
        private sealed class Flight
        {
            internal Shot Kind; internal int Slot = -1, Sub = -1; internal uint Anchor;
            internal DateTime FiredAt, EndedAt; internal bool BurstShown;   // when its flight was seen to end, and whether its burst is drawn
            internal float X, H, Y;
            internal int Damage; internal int[] Hp;                 // a plain shot: its planted damage, and every enemy's HP as it left (the hit report)
        }
        private static readonly List<Flight> _flights = new List<Flight>();
        private static Guard _guard;
        private static bool  _whiteFading;                             // her primed white bleeding off after a release
        private static DateTime _holdStart, _firedAt;
        private static readonly ShotCharge _shot = new ShotCharge();   // the shot charge (held long enough, the next pellet is the bomb), her release, and the retire mark (the release was the drop or the flash: the pellet of that shot is retired the tick it appears)
        private static bool  _flashPending;                            // the flash from her stands, after the ramp's frames
        private static DateTime _flashAt;
        private static float _flashX, _flashH, _flashY;
        internal static BorrowedEffect _carrier;                            // the apple shot, ours in the main-character instance
        private static readonly PelletWatch _pellets = new PelletWatch();
        private static readonly PlantedHits _planted = new();   // the flash's light hits, withdrawn on their ticks
        private static byte _floor = 0xFF;

        /// <summary>The shot effect this sphere wants entered on every floor (BorrowedShots asks every tick): the apple shot,
        /// while Xiao is out with Super Steve carrying a Big Bang sphere.</summary>
        internal static BorrowedEffect WantedShot()
            => PelletWatch.SuperSteveSphereOn(Items.bigbang) ? _carrier ??= BorrowedShots.TableConfig(CarrierConfig) : null;

        /// <summary>The hanging bomb's tint: red while it fades in; the pulse from the frame it is fully in.</summary>
        private static void PulseTint(bool hanging)
        {
            if (!hanging) { _pulsing = false; return; }
            if (JudgementBlade.HoverAlpha < 1f) { _pulsing = false; BladeProp.Tint(PulseRed[0], PulseRed[1], PulseRed[2]); return; }
            if (!_pulsing) { _pulsing = true; _pulseStart = GameClock.Now; }
            double t = (GameClock.Now - _pulseStart).TotalSeconds / PulseSeconds;
            float k = (float)(0.5 + 0.5 * Math.Cos(2.0 * Math.PI * t));                  // 1 on the second (red), 0 on the half second (black)
            BladeProp.Tint(PulseRed[0] * k, PulseRed[1] * k, PulseRed[2] * k);
        }

        internal static void Drive(bool active)
        {
            Owner.Profile ??= BigBangShot.FlashProfile;
            SunSword.BlindTick();
            _planted.Expire();
            if (!active) return;
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _floor) { if (_floor != 0xFF) { BombModel.Forget(); _graftRoot = 0; _grafts.Clear(); EndFlights(); Dissipate(); } _floor = floor; }
            ExplosionImmunity.DriveImmunity(true);
            GraftBomb();                                                                   // the apple shot carries the bomb's mesh while the sphere is on
            // The bomb's textures have ONE home at a time — the pass that is drawing it now: the clone slot's while the copy
            // hangs, the effect's while the apple shot carries it.
            bool copyUp = BladeProp.Active && (_guard == Guard.Charging || _guard == Guard.Primed || _guard == Guard.Dropping);
            if (copyUp) BombModel.KeepTextures(BombModel.WeaponPassBlock);
            else if (_grafts.Count > 0) BombModel.KeepTextures(BombModel.MainEffectBlock);
            else { BombModel.ReleaseTextures(); BombModel.Tick(); }
            PulseTint(copyUp);
            var p = BigBangShot.FlashProfile;
            SceneLighting.ToanTintOwned = _guard == Guard.Charging || _guard == Guard.Primed;

            // The shot charge, as her other charged shots: held ShotChargeSeconds, the charge-complete flash, and the next
            // pellet is the bomb. It arms whether or not the guard charge is primed; the guard charge owns the tint while it runs.
            _shot.Tick(ShotChargeSeconds, rampTint: _guard != Guard.Charging);
            // Every pellet is REPLACED (a bomb) or RETIRED (the drop's, the flash's) the tick it appears — a frame or two after
            // the engine drew it, and a quick shot's pellet can be out before the shoot state is even seen — so for as long as
            // the sphere is on, every pellet is drawn from the sheet's transparent cell (Mailbox.PelletSpriteId =
            // PelletSheetBakes.BlankCell); the sprite id in force before comes back when the sphere goes (Stop).
            if (!_hiding) Hide();
            else if (Memory.ReadInt(Mailbox.PelletSpriteId) != PelletSheetBakes.BlankCell) Memory.WriteInt(Mailbox.PelletSpriteId, PelletSheetBakes.BlankCell);   // re-asserted over another writer (a sphere change writes 0)
            bool hanging = _guard == Guard.Primed && JudgementBlade.HoverReady;                     // the bomb hangs over a live enemy (JudgementBlade rides out a lock that blinks)
            bool released = _shot.Released;

            if (_flashPending)                                                                  // her release's flash: the plunge, then the flash from where she stood
            {
                double frames = (GameClock.Now - _flashAt).TotalSeconds * 60.0;
                SceneLighting.DimRamp(p.PrimeDim, (float)(frames / SceneLighting.RampFrames));
                if (frames >= SceneLighting.RampFrames)
                {
                    _flashPending = false;
                    EnemyFacing.TurnEnemiesToward(_flashX, _flashY);                                // every enemy turned to the flash, as to a blast
                    SunSword.FlashAt(p, _flashX, _flashH, _flashY, _planted);                  // Big Bang's flash, as the Sun Sword's from Toan
                }
            }

            // What the NEXT pellet will be billed as (the engine takes it as the pellet leaves).
            if (hanging) ChargedShotWhp.Arm(DropWhp / SwingBase);
            else if (_shot.Charged) ChargedShotWhp.Arm(BombWhp / SwingBase);
            else if (_guard == Guard.Primed) ChargedShotWhp.Arm(FlashWhp / SwingBase);

            TrackFlights(p);                                                               // the shots in the air, and their blasts

            switch (_guard)
            {
                case Guard.Idle:
                    if (_whiteFading)                                                          // her white off her after a release
                    {
                        float k = (float)Math.Max(0.0, 1.0 - (GameClock.Now - _firedAt).TotalSeconds / TintFadeSeconds);
                        SunSword.HoldPrimedTint(p, k); BladeTint.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                        if (k <= 0f) { _whiteFading = false; BladeTint.Clear(); }
                    }
                    if (!SunSword.BlindRunning && GuardWatch.IsGuarding()) { _guard = Guard.Charging; _holdStart = GameClock.Now; _whiteFading = false; }
                    break;
                case Guard.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding())
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charge broken: " + (SunSword.BlindRunning ? "a blinding" : $"guard down (motion {Memory.ReadInt(CCharacter.Base + CCharacter.MotionId)}, R1 {((Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0)})"));
                        Dissipate(); break;
                    }
                    double held = (GameClock.Now - _holdStart).TotalSeconds;
                    GuardCharge.Frame(p, held, GuardSeconds);
                    JudgementBlade.JudgementTick(Owner);                                     // locked on, the bomb fades in over the target with the charge (Owner.Alpha)
                    if (held >= GuardSeconds)
                    {
                        _guard = Guard.Primed; ChargeTint.Clear();
                        Player.FlashChargeComplete();                                     // the stock charge-complete flash on her: the cue that it is ready (the hanging bomb is a fainter one than Toan's blade)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — locked on, the bomb hangs for the next shot to drop; else the next shot is the flash");
                    }
                    break;
                }
                case Guard.Primed:
                    BladeTint.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    if (p.PrimeDim > 0f) { SceneLighting.BeginDim(); SceneLighting.DimTo(p.PrimeDim); }
                    SunSword.HoldPrimedTint(p, 1f);
                    JudgementBlade.JudgementTick(Owner);                                     // the bomb over a locked target, following it
                    // Her release — the shoot state's first tick, before the engine has a pellet out — lets the hanging bomb go,
                    // or, with no bomb hanging, IS the flash: from where she stands, the charge spent.
                    if (released && hanging && JudgementBlade.BeginDrop())
                    {
                        _guard = Guard.Dropping; _firedAt = GameClock.Now; _shot.Charged = false; _shot.RetirePellet = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the hanging bomb is let go on her release");
                    }
                    else if (released && !hanging)
                    {
                        _guard = Guard.Idle; _whiteFading = true; _firedAt = GameClock.Now; _shot.Charged = false; _shot.RetirePellet = true;
                        _flashPending = true; _flashAt = GameClock.Now;
                        _flashX = Memory.ReadFloat(Addresses.dunPositionX); _flashH = Memory.ReadFloat(Addresses.dunPositionZ); _flashY = Memory.ReadFloat(Addresses.dunPositionY);
                        JudgementBlade.ReleaseJudgement();                                              // a hover fading off a lost lock: gone
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "released with nothing hanging — the flash from where she stands");
                    }
                    break;
                case Guard.Dropping:
                {
                    double since = (GameClock.Now - _firedAt).TotalSeconds;
                    float k = (float)Math.Max(0.0, 1.0 - since / TintFadeSeconds);
                    SunSword.HoldPrimedTint(p, k); BladeTint.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    JudgementBlade.JudgementTick(Owner);                                     // the fall, and the landing (LandDrop)
                    if (JudgementBlade.TakeDropLanded()) { _guard = Guard.Idle; break; }
                    if (!JudgementBlade.Dropping && !JudgementBlade.LandingPending) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the drop was abandoned — the charge is spent"); Dissipate(); }
                    break;
                }
            }

            // A new pellet: retired (the drop's or the flash's — the release was the shot; the motion and its WHP already hers),
            // the charged bomb, or the plain bomb.
            int slot = _pellets.NewPellet();
            if (slot < 0) return;
            long poolNow = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (_shot.RetirePellet || (hanging && JudgementBlade.BeginDrop()))
            {
                if (!_shot.RetirePellet) { _guard = Guard.Dropping; _firedAt = GameClock.Now; _shot.Charged = false; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the hanging bomb is let go"); }
                if (Memory.IsValidGuest(poolNow)) Memory.WriteInt(PlayerShotPool.FlagAddr(poolNow, slot), 0);   // never drawn (the blank cell), never flies
                _shot.RetirePellet = false;
            }
            else if (_shot.Charged) { _shot.Charged = false; FireBomb(slot, Shot.Bomb); }
            else FireBomb(slot, Shot.Pellet);
        }

        /// <summary>Every shot in the air. A charged bomb bursts the tick it TOUCHES an enemy (the shot's own contact sweep is
        /// not waited for) and wherever it dies otherwise — a wall, or the end of its flight — as a pellet would; a plain bomb
        /// hits natively and shows its burst where its flight ends.</summary>
        private static void TrackFlights(SunSword.SolarProfile p)
        {
            if (_flights.Count == 0) return;
            if (!BladeProp.Active) SolarGlow.Tick();                                       // the hover's owner ticks it while a copy hangs
            Flight discOwner = _flights.FindLast(f => f.Kind == Shot.Bomb && f.Anchor != 0);
            foreach (Flight f in _flights.ToArray())
            {
                double since = (GameClock.Now - f.FiredAt).TotalSeconds;
                if (DebugDiagnostics.Enabled && f.Hp != null) ReportHits(f);                                           // DIAGNOSTIC: every HP drop while a plain shot is out, as it happens
                bool live;
                if (f.Sub >= 0)
                {   // the borrowed shot: flying while it is active and its phase is the flight; its object's position is the bomb's
                    long inst = _carrier.Instance;
                    live = Memory.ReadUShort(inst + ShotEffectPack.OffActive + f.Sub * 2) != 0 && Memory.ReadUShort(inst + ShotEffectPack.OffPhase + f.Sub * 2) < 2;   // 2 = its contact (the impact's first frame has planted), 3 = its wait ran out
                    if (live)
                    {
                        long op = inst + ShotEffectPack.OffObj + f.Sub * ShotEffectPack.ObjStride + ShotEffectPack.ObjPos;
                        f.X = Memory.ReadFloat(op); f.H = Memory.ReadFloat(op + 4); f.Y = Memory.ReadFloat(op + 8);
                        if (f == discOwner && !BladeProp.Active && !SolarGlow.OnAnchor(f.Anchor))
                            SolarGlow.Show(GlowDisc, anchor: f.Anchor, lift: 0f, growSeconds: 0, palRow: GlowFireRow, scale: BombGlowSize);   // the charged bomb's red-orange disc, full size at once
                        if (f.Kind == Shot.Pellet) { if (since < MissSeconds) continue; }        // the plain shot hits natively: its own sweep ends its flight
                        else if (!Touching(f.X, f.H, f.Y) && since < MissSeconds) continue;
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + f.Sub * 2, 0);   // the bomb is spent where it is
                    }
                    else if (f.Kind == Shot.Pellet)
                    {   // out of its flight (its contact, a wall, or its end): the bomb's burst here at once. The hit is the engine's plant on
                        // the impact's FIRST frame — the frame after the contact — and a mod tick can fall between the two, so the sub-shot is
                        // cut only once the plant has happened (the reload latch it sets), or once nothing can plant (inactive, or a wait past
                        // any impact frame: a wall, or the flight's end), never on sight.
                        if (!f.BurstShown)
                        {
                            f.BurstShown = true; f.EndedAt = GameClock.Now;
                            BombFx.Spawn(f.X, f.H, f.Y, PelletFxScale, ringRadius: 0f);
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"plain bomb bursts at ({f.X:F0},{f.H:F0},{f.Y:F0})");
                        }
                        bool planted  = Memory.ReadByte(inst + ShotEffectPack.OffLatch + f.Sub) != 0;
                        bool inactive = Memory.ReadUShort(inst + ShotEffectPack.OffActive + f.Sub * 2) == 0;
                        if (!planted && !inactive && (GameClock.Now - f.EndedAt).TotalSeconds < CutWait) continue;   // the impact frame has not run yet
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + f.Sub * 2, 0);
                        EndFlight(f); continue;
                    }
                    else Memory.WriteUShort(inst + ShotEffectPack.OffActive + f.Sub * 2, 0);   // the charged bomb's flight ended on the engine's own contact: cut before its impact animation lingers (it plants nothing, so nothing waits)
                }
                else
                {
                    long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                    live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, f.Slot)) != 0;
                    if (live)
                    {
                        long pa = PlayerShotPool.PosAddr(pool, f.Slot);
                        f.X = Memory.ReadFloat(pa); f.H = Memory.ReadFloat(pa + 4); f.Y = Memory.ReadFloat(pa + 8);
                        if (since < MissSeconds) continue;
                    }
                }
                EndFlight(f);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{(f.Kind == Shot.Bomb ? "charged" : "plain")} bomb bursts at ({f.X:F0},{f.H:F0},{f.Y:F0})" + (live ? " on contact" : " where it died"));
                if (f.Kind == Shot.Bomb) Blast(f.X, f.H, f.Y, BombFxScale, BombDamage, KickFor(BombKick), BombReach, flash: false, ring: true);
                else BombFx.Spawn(f.X, f.H, f.Y, PelletFxScale, ringRadius: 0f);              // a plain shot flying as a pellet (no apple shot entered): the visual alone
            }
        }

        /// <summary>The bomb's blast at (x, h, y): the visual at <paramref name="fxScale"/> (drawn <paramref name="fxLift"/> above the
        /// point) with, if <paramref name="ring"/>, its ring out to the blast's reach; Big Bang's falloff blast at
        /// <paramref name="damage"/>/<paramref name="kick"/>/<paramref name="reach"/> of its own, every enemy turned to it, and —
        /// the drop alone — the flash from it.</summary>
        private static void Blast(float x, float h, float y, float fxScale, float damage, float kick, float reach, bool flash, bool ring, float fxLift = 0f)
        {
            BlastFalloff.LastBlast = (x, h, y);
            BombFx.Spawn(x, h + fxLift, y, fxScale, ringRadius: ring ? BlastFalloff.BlastRadius * reach : 0f);
            BlastFalloff.PlantFalloff(x, h, y, damageScale: damage, kickScale: kick, reachScale: reach);
            EnemyFacing.TurnEnemiesToward(x, y);
            if (flash) SunSword.FlashAt(BigBangShot.FlashProfile, x, h, y, _planted);
        }
        /// <summary>The hanging bomb's landing (BigBang's owner callback): the full blast, drawn a little above the floor, and the flash.</summary>
        private static void LandDrop(int slot, float x, float h, float y) => Blast(x, h, y, HoverFxScale, 1f, 1f, 1f, flash: true, ring: true, fxLift: DropFxLift);

        private static Flight Begin(int slot, long pool, Shot kind)
        {
            long pa = PlayerShotPool.PosAddr(pool, slot);
            var f = new Flight { Kind = kind, Slot = slot, FiredAt = GameClock.Now, X = Memory.ReadFloat(pa), H = Memory.ReadFloat(pa + 4), Y = Memory.ReadFloat(pa + 8) };
            _flights.Add(f);
            _firedAt = GameClock.Now;
            ChargeTint.Clear();
            return f;
        }

        /// <summary>A bomb — charged (<see cref="Shot.Bomb"/>) or plain (<see cref="Shot.Pellet"/>): the apple shot with the bomb's
        /// mesh on it, at its kind's size, fired in the pellet's place on the pellet's own line and speed, planting nothing of its
        /// own (the blast where it dies is the damage); the charged one carries the drop's red-orange disc. Without the shot
        /// entered on this floor the pellet flies plain and bursts where it dies.</summary>
        private static void FireBomb(int slot, Shot kind)
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            Flight f = Begin(slot, pool, kind);
            long va = PlayerShotPool.VelAddr(pool, slot);
            float vx = Memory.ReadFloat(va), vh = Memory.ReadFloat(va + 4), vy = Memory.ReadFloat(va + 8);
            int life = Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot));
            string what = kind == Shot.Bomb ? "charged bomb" : "plain bomb";
            int damage = 0;
            if (kind == Shot.Pellet)
            {   // the pellet's own damage, planted by the shot itself; the drop's kick at PelletKick, stamped on that damage's entries by the bypass cave
                damage = Memory.ReadInt(PlayerShotPool.DamageAddr(pool, slot));
                f.Damage = damage;
                if (DebugDiagnostics.Enabled)
                {   // DIAGNOSTIC: every enemy's HP as the shot leaves, for ReportHits
                    f.Hp = new int[EnemyAddresses.FloorSlots.Count];
                    for (int s = 0; s < f.Hp.Length; s++) f.Hp[s] = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                }
                Memory.WriteFloat(Mailbox.PelletKickStrength, BlastFalloff.KickStrength * KickFor(PelletKick));
                Memory.WriteFloat(Mailbox.PelletKickDecay, BlastFalloff.KickDecay);
                Memory.WriteVec3 (Mailbox.PelletKickOrigin, 0f, 0f, 0f);             // no offset: straight out of the burst
                Memory.WriteInt  (Mailbox.PelletKickDamage, damage);
            }
            if (_carrier != null && BorrowedShots.Fire(_carrier, f.X, f.H, f.Y, vx, vh, vy, damage, life, plant: kind == Shot.Pellet))
            {
                Memory.WriteInt(PlayerShotPool.FlagAddr(pool, slot), 0);                  // the pellet gives way to the shot
                f.Sub = Memory.ReadInt(_carrier.Instance + ShotEffectPack.OffLastIdx);
                long obj = _carrier.Instance + ShotEffectPack.OffObj + f.Sub * ShotEffectPack.ObjStride;
                float sc = kind == Shot.Bomb ? ShotBombScale : PelletBombScale;
                Memory.WriteVec3(obj + CCharacter.CharScale, sc, sc, sc); _objScale[obj] = sc;
                if (kind == Shot.Bomb)
                {
                    uint r = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                    uint node = Memory.IsValidGuest(r) ? NodeNamed(r, CarrierNode) : 0;
                    f.Anchor = node != 0 ? node : r;                                        // the disc hangs on the bomb's own node
                    if (f.Anchor != 0 && !BladeProp.Active)                                  // …from this very tick, full size (TrackFlights keeps it there)
                    {
                        if (SolarGlow.IsUp && !SolarGlow.OnAnchor(f.Anchor)) SolarGlow.Hide();
                        SolarGlow.Show(GlowDisc, anchor: f.Anchor, lift: 0f, growSeconds: 0, palRow: GlowFireRow, scale: BombGlowSize);
                    }
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}: the apple shot #{f.Sub} carrying the bomb at {sc:0.#}×, in the pellet's place ({life} frames" + (kind == Shot.Pellet ? $", damage {damage}, kick {BlastFalloff.KickStrength * KickFor(PelletKick):0.##}" : "") + ")");
                return;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}: the apple shot is not entered on this floor — the pellet flies plain");
        }

        /// <summary>DIAGNOSTIC: each HP drop while a plain shot is out, the tick it shows, with the terms CheckDmg applied — the
        /// sphere the last hit landed on and that sphere's damage percent for Xiao, the enemy's defense, and her distance to it
        /// (point-blank ×1.5 within 20, down to ×0.5 past 100). Two sources hitting one enemy show as two lines.</summary>
        private static void ReportHits(Flight f)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            for (int s = 0; s < f.Hp.Length; s++)
            {
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                int hp = Memory.ReadInt(a + EnemySlotOffsets.Hp);
                if (hp >= f.Hp[s]) { f.Hp[s] = hp; continue; }
                int before = f.Hp[s]; f.Hp[s] = hp;
                long b = BodyCollision.SlotBase(s);
                int part = Memory.ReadInt(b + BodyCollision.LastHitSphere);
                int pct = part >= 0 && part < BodyCollision.MaxBodyParts ? Memory.ReadInt(b + BodyCollision.DamagePctArray + part * BodyCollision.DamagePctStride + Player.XiaoId * 4) : -1;
                float r = part >= 0 && part < BodyCollision.MaxBodyParts ? Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride) : 0f;
                uint defs = Memory.ReadUInt(a + EnemySlotOffsets.DefenseStats);
                long up = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(up) - px, dy = Memory.ReadFloat(up + 8) - py;
                // The guard: the enemy's three guard windows (flag, start..end frame) against its playing motion frame — a hit inside
                // an active window is BLOCKED (chip damage), as a pellet's would be.
                float frame = Memory.ReadFloat(EnemyAddresses.MainMonstorUnit.Base + (long)s * EnemyAddresses.CharObjects.Stride + ModelScaleOffsets.PlayingMotionFrameFromUnit);
                string guard = "";
                for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                {
                    ushort flag = Memory.ReadUShort(EnemyAddresses.GuardWindows.FlagAddr(s, w));
                    if (flag == 0) continue;
                    long wb = EnemyAddresses.MainMonstorUnit.Base + (long)s * EnemyAddresses.GuardWindows.Stride;
                    float ws = Memory.ReadFloat(wb + EnemyAddresses.GuardWindows.StartOffset + w * 4), we = Memory.ReadFloat(wb + EnemyAddresses.GuardWindows.EndOffset + w * 4);
                    guard += $" w{w} {ws:0}..{we:0}{(frame >= ws && frame <= we ? " IN" : "")}";
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"plain bomb hit slot {s} (species {Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId)}) {(GameClock.Now - f.FiredAt).TotalMilliseconds:0} ms after the shot: HP {before} -> {hp} (-{before - hp}); base {f.Damage}, sphere {part} ({pct}% for Xiao, r {r:0.#}), defense {defs & 0xFFFF} / weapon {defs >> 16}, her distance {Math.Sqrt(dx * dx + dy * dy):0}, motion frame {frame:0}, guard windows:{(guard.Length == 0 ? " none" : guard)}");
            }
        }

        /// <summary>A flight over: its disc hidden if it is on it.</summary>
        private static void EndFlight(Flight f)
        {
            _flights.Remove(f);
            if (f.Anchor != 0 && SolarGlow.AnchoredTo(f.Anchor)) SolarGlow.Hide();
        }
        private static void EndFlights() { foreach (Flight f in _flights.ToArray()) EndFlight(f); }

        /// <summary>The guard charge spent with nothing to show: her white and the blade off, a hover let go. Shots in the air fly on.</summary>
        private static void Dissipate()
        {
            SunSword.HoldPrimedTint(BigBangShot.FlashProfile, 0f);
            BladeTint.Clear(); ChargeTint.Clear(); SceneLighting.EndDim();
            if (BladeProp.Active) SolarGlow.Fade();
            JudgementBlade.ReleaseJudgement();
            _guard = Guard.Idle; _whiteFading = false; _flashPending = false;
        }

        /// <summary>Is (x, h, y) inside a live enemy's body — within its authored width (scaled with the unit) plus ContactPad
        /// across, and between its feet and its authored height up?</summary>
        private static bool Touching(float x, float h, float y) => TouchingSlot(x, h, y) >= 0;
        /// <summary>The live enemy whose body (x, h, y) is inside, or −1.</summary>
        private static int TouchingSlot(float x, float h, float y)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                long up = EnemyAddresses.CharObjects.PosAddr(s);                                  // the unit's own position and world height
                float ex = Memory.ReadFloat(up), eh = Memory.ReadFloat(up + 4), ey = Memory.ReadFloat(up + 8);
                ushort eid = Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId);
                float width = 7f, height = 20f;
                if (EnemySpecies.Defaults.TryGetValue(eid, out var def)) { width = def.BodyWidth ?? width; height = def.HeightFromRoot ?? height; }
                float scale = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharScale + 4);
                if (!(scale > 0.05f) || scale > 20f) scale = 1f;
                float r = width * scale + ContactPad, dx = ex - x, dy = ey - y;
                if (dx * dx + dy * dy > r * r) continue;
                if (h < eh - ContactPad || h > eh + height * scale + ContactPad) continue;
                return s;
            }
            return -1;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended, explosions dangerous again.</summary>
        internal static void Stop()
        {
            if (_guard != Guard.Idle || _whiteFading) SunSword.HoldPrimedTint(BigBangShot.FlashProfile, 0f);
            EndFlights();
            Memory.WriteInt(Mailbox.PelletKickDamage, 0);                         // no kick mark
            BladeTint.Clear(); ChargeTint.Clear(); SolarGlow.Hide(); SceneLighting.Restore(); Unhide();
            JudgementBlade.ReleaseJudgement(); ExplosionImmunity.DriveImmunity(false); Ungraft(); BombModel.ReleaseTextures();
            SunSword.EndBlinding();
            _planted.WithdrawAll();
            SceneLighting.ToanTintOwned = false;
            _pellets.Reset(); _shot.Reset();
            _guard = Guard.Idle; _whiteFading = false; _flashPending = false;
        }
    }
}
