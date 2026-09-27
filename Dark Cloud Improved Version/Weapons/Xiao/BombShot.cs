using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Big Bang sphere — "Detonate" from Xiao's slingshot, as close to Big Bang's as her weapon
    /// allows. Two charges, as Big Bang has two:
    /// <br/>THE GUARD CHARGE (<see cref="GuardSeconds"/>) is Big Bang's Solar Flash: the slingshot whitens, the cyan build-up
    /// runs on her (the room does not darken until a blast is coming); primed, she holds the white. LOCKED ON, a four-times
    /// Bomb — the item's own model, red while it fades in over the whole charge, pulsing once it is in, the Matador's
    /// red-orange disc growing with it — hangs over the target where Big Bang hangs its judgement blade, and her RELEASE lets it fall (the shoot motion plays and bills the shot; its pellet is drawn
    /// from the blank cell and retired the tick it appears): the room darkens along the fall and it lands with the bomb's own
    /// blast at twice its size, its ring out to the blast's reach, and exactly Big Bang's blast (the falloff damage, the kick, every enemy turned to it)
    /// and Big Bang's flash. NOT locked
    /// on, her release IS the flash: Big Bang's flash from where she stands — twice the Sun Sword's share — with no pellet
    /// (the shoot motion plays and bills the shot; its pellet is retired as the drop's is).
    /// <br/>EVERY PELLET IS A BOMB: each shot flies as Halloween's pumpkin bomb (table config 3, `pump_bom`, entered in the
    /// main-character effect instance — the monster pack's is untouched) with the Bomb's mesh grafted onto the pumpkin's node in
    /// place of its own, so it flies as the pumpkin does, with no impact animation and no light or flash of any kind. A PLAIN
    /// shot is the bomb at 1× that hits as her pellet would — the shot's own damage entry, planted every frame of its flight
    /// at a pellet's radius with the pellet's damage (the engine's hit, reaction and weapon HP), with the drop's kick at a
    /// quarter of its distance stamped on it by the guard-bypass cave (Mailbox.PelletKickDamage, as Dragon's Y's shot) — and
    /// bursts in the bomb's half-size visual where it dies, no blast of its own. THE SHOT CHARGE (the shot held <see cref="ShotChargeSeconds"/>, as her other charged shots are made)
    /// is the bomb at 2× wearing the drop's red-orange disc, bursting where it dies with the bomb's own blast at 1.1× with
    /// half the drop's damage, kick and reach. Several may be in the air at once.
    /// <br/>Her white bleeds out over <see cref="TintFadeSeconds"/> once a shot leaves. Weapon HP is the shot's, taken as the
    /// pellet leaves: 20 for the drop, 10 for the bomb shot, 5 for the flash shot. Explosions cannot hurt her while the sphere
    /// is on. Driven from Super Steve's sphere dispatch; Solar Harvest is inherited alongside.</summary>
    internal static class BombShot
    {
        private const string Tag = "[BombShot] ";
        private const double GuardSeconds = 3.0, ShotChargeSeconds = 1.0;
        private const float  HoverScale = 4f;                          // the bomb over a target
        private const float  HoverFxScale = 2f, BombFxScale = 1.1f, PelletFxScale = 0.5f;   // the blast sprites: the drop, the charged shot, the plain shot; each ring reaches its blast's edge
        private const float  ShotBombScale = 2f, PelletBombScale = 1f;  // the bomb on the pumpkin shot, over the pumpkin's own size (the sub-shot object's own scale): charged, plain
        private const float  HoverMargin = 10f;                         // the hanging bomb's bottom this far above the species' authored height (Big Bang's blade: 6)
        private const float  ContactPad = 3f;                           // the bomb shot touches an enemy this far outside its body width
        // THE BOMB'S PULSE: red, 150,0,0, while the hanging bomb fades in; from the frame it is fully in, a smooth cosine
        // between that red and black — black every half second, red every second — with no cut anywhere.
        private static readonly float[] PulseRed = { 150f, 0f, 0f };
        private const double PulseSeconds = 1.0;
        private static DateTime _pulseStart;
        private static bool _pulsing;
        private const float  BombLength = 1.22f;                        // bakudan.mds: its mesh runs 1.22 below the root to 1.98 above it at 1×, so this is root-to-bottom — the drop stops with the bottom ON the root
        private const int    CarrierConfig = 3;                         // the shot table's `pump_bom`: Halloween's pumpkin bomb (no impact or expiry motion: nothing plays under the bomb's own burst)
        private const string CarrierNode = "kabo";                      // the node that draws the pumpkin in each of its trees (under its `null1` root)
        private const byte   PelletReload = 1;                          // frames between the plain shot's flight plants: it hits whatever it flies into
        private const float  BombDamage = 0.5f, BombKick = 0.5f, BombReach = 0.5f;            // the charged shot's blast against the drop's (the kick as DISTANCE)
        private const float  PelletKick = 0.25f;                        // the plain shot's kick DISTANCE, of the drop's (half the charged shot's); its damage is the pellet's own
        /// <summary>The kick strength that throws <paramref name="distanceFraction"/> as far as the drop's: distance ≈ strength² / (2 · decay).</summary>
        private static float KickFor(float distanceFraction) => (float)Math.Sqrt(distanceFraction);
        private const float  PelletRadius = 3f;                         // the carrier's flying radius while the sphere is on: a pellet's own, so its contact sweep and the plain shot's flight plants work (the seed zeroes it)
        private const float  DropFxLift = 2f;                            // the drop's blast drawn this far above the landing, so its ring is not flat on the floor
        private const float  DropWhp = 20f, BombWhp = 10f, FlashWhp = 5f, SwingBase = 1.5f;
        private const double TintFadeSeconds = 0.25, MissSeconds = 3.0;
        private const string GlowDisc = "catglowp";
        private const int    GlowFireRow = 1;                           // the cave's one-based row: the Matador's red-orange, on the bomb
        private const float  BombGlowSize = 0.6f;

        private static readonly BigBang.JudgementOwner Owner = new BigBang.JudgementOwner
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
        /// <summary>A shot in the air: the pool pellet it began as (<see cref="Slot"/>), the pumpkin sub-shot carrying its bomb
        /// (<see cref="Sub"/>, −1 for a pellet flying plain), its disc's node, where it was last seen, and whether its burst is drawn.</summary>
        private sealed class Flight
        {
            internal Shot Kind; internal int Slot = -1, Sub = -1; internal uint Anchor;
            internal DateTime FiredAt; internal bool Burst;
            internal float X, H, Y;
        }
        private static readonly List<Flight> _flights = new List<Flight>();
        private static Guard _guard;
        private static bool  _whiteFading;                             // her primed white bleeding off after a release
        private static DateTime _holdStart, _firedAt, _shotHoldStart;
        private static bool  _shotHolding, _shotCharged;               // the shot charge: held long enough, the next pellet is the bomb
        private static bool  _wasShooting;                             // the shoot state last tick (its rising edge is her release)
        private static bool  _retirePellet;                            // the release was the drop or the flash: the pellet of that shot is retired the tick it appears
        private static bool  _flashPending;                            // the flash from her stands, after the ramp's frames
        private static DateTime _flashAt;
        private static float _flashX, _flashH, _flashY;
        private static bool  _hiding;                                  // pellets drawn from the blank cell while the sphere is on: no pellet of hers is ever meant to be seen
        private static int   _spriteBefore;                            // Mailbox.PelletSpriteId as the hide found it, put back after
        private static BorrowedEffect _carrier;                            // the pumpkin shot, ours in the main-character instance
        private static uint  _graftRoot, _graftVisual;                  // the entered instance's template root (the entry the grafts belong to) and the bomb's visual
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static readonly List<(int slot, int ticks)> _planted = new List<(int, int)>();
        private static byte _floor = 0xFF;

        /// <summary>The shot effect this sphere wants entered on every floor (BorrowedShots asks every tick): the pumpkin shot,
        /// while Xiao is out with Super Steve carrying a Big Bang sphere.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.XiaoId || Player.Weapon.GetCurrentWeaponId() != Items.supersteve) return null;
            int slot = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.XiaoId));
            if (slot < 0 || slot >= DngStatusData.MaxWeaponSlots || SuperSteve.AttachedSphere(DngStatusData.WeaponRecord(Player.XiaoId, slot)) != Items.bigbang) return null;
            return _carrier ??= BorrowedShots.TableConfig(CarrierConfig);
        }

        /// <summary>The Bomb's mesh onto the pumpkin shot's nodes. The entered instance holds a template tree (+0xCC) AND one tree
        /// per sub-shot object (each object's own model pointer) — the sub-shots draw their own — so every tree is walked for
        /// the node that draws (the pumpkin, `kabo`) and its visual pointer swapped for the bomb model's; the bomb's
        /// textures are tagged into the effect's texture block for as long as it stays. The pumpkins' own visuals go back on
        /// Stop. Re-done whenever the instance is re-entered (a new floor: new trees).</summary>
        private static readonly List<(uint node, uint carrierVisual)> _grafts = new List<(uint, uint)>();
        private static readonly Dictionary<long, float> _objScale = new Dictionary<long, float>();   // each sub-shot object's scale while grafted — the fired shot's kind sets it (an animated node's matrix is rebuilt every frame; the object's scale is not)
        private static void GraftBomb()
        {
            if (_carrier == null || !BorrowedShots.Entered(_carrier)) { _graftRoot = 0; _grafts.Clear(); return; }
            uint root = Memory.ReadGuestPtr(_carrier.Instance + 0xCC);
            if (!Memory.IsValidGuest(root)) { _graftRoot = 0; _grafts.Clear(); return; }
            if (root == _graftRoot)
            {
                if (_grafts.Count == 0) return;
                foreach (var (node, _) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) != _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, _graftVisual);   // a rebuild put a pumpkin back
                foreach (var kv in _objScale)
                    if (Math.Abs(Memory.ReadFloat(kv.Key + CCharacter.CharScale) - kv.Value) > 0.01f) Memory.WriteVec3(kv.Key + CCharacter.CharScale, kv.Value, kv.Value, kv.Value);
                return;
            }
            uint bombRoot = BombModel.Root();
            if (bombRoot == 0) return;
            uint bombVis = Memory.ReadGuestPtr(Memory.ToMmu(bombRoot) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(bombVis)) return;
            _grafts.Clear(); _objScale.Clear(); _graftRoot = root; _graftVisual = bombVis;
            var roots = new List<uint> { root };
            int count = Memory.ReadInt(_carrier.Instance + ShotEffectPack.OffCount);
            for (int i = 0; i < Math.Min(count, ShotEffectPack.SubShots); i++)
            {
                long obj = _carrier.Instance + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride;
                uint r = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                if (Memory.IsValidGuest(r) && !roots.Contains(r)) roots.Add(r);
                Memory.WriteVec3(obj + CCharacter.CharScale, PelletBombScale, PelletBombScale, PelletBombScale);   // the object's scale, which the draw re-applies every frame; a fire sets its shot's
                _objScale[obj] = PelletBombScale;
            }
            // The pumpkin's node BY NAME: a tree still being built (the instance re-entered) has the impact's light node with a visual
            // before the pumpkin has one, and a graft onto that node drew the bomb offset — so a tree without a posed pumpkin node
            // waits (nothing recorded; next tick tries again).
            var found = new List<(uint node, uint carrierVisual)>();
            foreach (uint r in roots)
            {
                uint node = NodeNamed(r, CarrierNode);
                uint carrierVis = node != 0 ? Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) : 0;
                if (node == 0 || (!Memory.IsValidGuest(carrierVis) && carrierVis != bombVis)) { _graftRoot = 0; _grafts.Clear(); _objScale.Clear(); return; }
                if (carrierVis != bombVis) found.Add((node, carrierVis));
            }
            foreach (var (node, _) in found) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, bombVis);
            _grafts.AddRange(found);
            if (_grafts.Count == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the pumpkin shot's trees already carry the bomb"); return; }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the bomb's mesh grafted onto `{CarrierNode}` in {_grafts.Count} of the pumpkin shot's trees ({roots.Count} trees: the template and the sub-shots) → visual 0x{bombVis:X}");
        }
        /// <summary>The hanging bomb's tint: red while it fades in; the pulse from the frame it is fully in.</summary>
        private static void PulseTint(bool hanging)
        {
            if (!hanging) { _pulsing = false; return; }
            if (BigBang.HoverAlpha < 1f) { _pulsing = false; BladeProp.Tint(PulseRed[0], PulseRed[1], PulseRed[2]); return; }
            if (!_pulsing) { _pulsing = true; _pulseStart = GameClock.Now; }
            double t = (GameClock.Now - _pulseStart).TotalSeconds / PulseSeconds;
            float k = (float)(0.5 + 0.5 * Math.Cos(2.0 * Math.PI * t));                  // 1 on the second (red), 0 on the half second (black)
            BladeProp.Tint(PulseRed[0] * k, PulseRed[1] * k, PulseRed[2] * k);
        }
        /// <summary>The node under <paramref name="root"/> (root included) named <paramref name="name"/>; 0 when none.</summary>
        private static uint NodeNamed(uint root, string name)
        {
            var work = new Stack<uint>(); work.Push(root); int guard = 0;
            while (work.Count > 0 && guard++ < 64)
            {
                uint n = work.Pop();
                if (!Memory.IsValidGuest(n)) continue;
                byte[] nb = Memory.ReadBytesBatch(Memory.ToMmu(n) + CFrameVu1.Name, 16);
                if (nb != null && System.Text.Encoding.ASCII.GetString(nb).Split('\0')[0] == name) return n;
                for (uint c = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild); Memory.IsValidGuest(c); c = Memory.ReadGuestPtr(Memory.ToMmu(c) + CFrameVu1.RootSibling)) work.Push(c);
            }
            return 0;
        }
        private static void Ungraft()
        {
            if (_grafts.Count > 0 && _carrier != null && Memory.ReadGuestPtr(_carrier.Instance + 0xCC) == _graftRoot)
            {
                foreach (var (node, carrierVis) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) == _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, carrierVis);   // the pumpkin's own visual back
                foreach (long obj in _objScale.Keys) Memory.WriteVec3(obj + CCharacter.CharScale, 1f, 1f, 1f);                                                                  // …and its size
            }
            _graftRoot = 0; _graftVisual = 0; _grafts.Clear(); _objScale.Clear();
        }

        internal static void Drive(bool active)
        {
            Owner.Profile ??= SunSword.BombShotFlash;
            SunSword.BlindTick();
            SunSword.ExpireHits(_planted);
            if (!active) return;
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _floor) { if (_floor != 0xFF) { BombModel.Forget(); _graftRoot = 0; _grafts.Clear(); EndFlights(); Dissipate(); } _floor = floor; }
            BigBang.DriveImmunity(true);
            GraftBomb();                                                                   // the pumpkin shot carries the bomb's mesh while the sphere is on
            if (_carrier != null && Math.Abs(BorrowedShots.PhaseRadius(_carrier, 1) - PelletRadius) > 0.01f) BorrowedShots.SetPhaseRadius(_carrier, 1, PelletRadius);   // NaN when not entered: no write
            // The bomb's textures have ONE home at a time — the pass that is drawing it now: the clone slot's while the copy
            // hangs, the effect's while the pumpkin shot carries it. Two homes swapped every tick flickered the hanging bomb.
            bool copyUp = BladeProp.Active && (_guard == Guard.Charging || _guard == Guard.Primed || _guard == Guard.Dropping);
            if (copyUp) BombModel.KeepTextures(BombModel.WeaponPassBlock);
            else if (_grafts.Count > 0) BombModel.KeepTextures(BombModel.MainEffectBlock);
            else { BombModel.ReleaseTextures(); BombModel.Tick(); }
            PulseTint(copyUp);
            var p = SunSword.BombShotFlash;
            SolarLighting.ToanTintOwned = _guard == Guard.Charging || _guard == Guard.Primed;

            // The shot charge, as her other charged shots: held ShotChargeSeconds, the charge-complete flash, and the next
            // pellet is the bomb. It arms whether or not the guard charge is primed.
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!_shotHolding) { _shotHolding = true; _shotCharged = false; _shotHoldStart = GameClock.Now; }
                double held = (GameClock.Now - _shotHoldStart).TotalSeconds;
                if (!_shotCharged && held >= ShotChargeSeconds) { _shotCharged = true; Player.FlashChargeComplete(); }
                if (_guard != Guard.Charging) ChargeTint.Ramp(_shotCharged ? 0 : ShotChargeSeconds - held);
            }
            else if (_shotHolding && _guard != Guard.Charging) { _shotHolding = false; ChargeTint.Clear(); }   // released: _shotCharged stays for the pellet
            else _shotHolding = false;
            // Every pellet is REPLACED (a bomb) or RETIRED (the drop's, the flash's) the tick it appears — a frame or two after
            // the engine drew it, and a quick shot's pellet can be out before the shoot state is even seen — so for as long as
            // the sphere is on, every pellet is drawn from the sheet's transparent cell (Mailbox.PelletSpriteId =
            // PelletSheetBakes.BlankCell); the sprite id in force before comes back when the sphere goes (Stop).
            if (!_hiding) Hide();
            else if (Memory.ReadInt(CodeCaves.Mailbox.PelletSpriteId) != PelletSheetBakes.BlankCell) Memory.WriteInt(CodeCaves.Mailbox.PelletSpriteId, PelletSheetBakes.BlankCell);   // re-asserted over another writer (a sphere change writes 0)
            bool hanging = _guard == Guard.Primed && BigBang.HoverReady;                     // the bomb hangs over a live enemy (BigBang rides out a lock that blinks)
            bool shooting = shotState == PlayerAction.XiaoShotShoot, released = shooting && !_wasShooting;
            _wasShooting = shooting;
            if (!shooting) _retirePellet = false;                                               // the shot went by without a pellet

            if (_flashPending)                                                                  // her release's flash: the plunge, then the flash from where she stood
            {
                double frames = (GameClock.Now - _flashAt).TotalSeconds * 60.0;
                SolarLighting.DimRamp(p.PrimeDim, (float)(frames / SolarLighting.RampFrames));
                if (frames >= SolarLighting.RampFrames)
                {
                    _flashPending = false;
                    BigBang.TurnEnemiesToward(_flashX, _flashY);                                // every enemy turned to the flash, as to a blast
                    SunSword.FlashAt(p, _flashX, _flashH, _flashY, _planted);                  // Big Bang's flash, as the Sun Sword's from Toan
                }
            }

            // What the NEXT pellet will be billed as (the engine takes it as the pellet leaves).
            if (hanging) ChargedShotWhp.Arm(DropWhp / SwingBase);
            else if (_shotCharged) ChargedShotWhp.Arm(BombWhp / SwingBase);
            else if (_guard == Guard.Primed) ChargedShotWhp.Arm(FlashWhp / SwingBase);

            TrackFlights(p);                                                               // the shots in the air, and their blasts

            switch (_guard)
            {
                case Guard.Idle:
                    if (_whiteFading)                                                          // her white off her after a release
                    {
                        float k = (float)Math.Max(0.0, 1.0 - (GameClock.Now - _firedAt).TotalSeconds / TintFadeSeconds);
                        SunSword.HoldPrimedTint(p, k); SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                        if (k <= 0f) { _whiteFading = false; SolarBlade.Clear(); }
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
                    SolarBlade.Set((float)(held / GuardSeconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    if (p.PrimeDim > 0f) { SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / GuardSeconds)); }
                    ChargeTint.Ramp(GuardSeconds - held);
                    BigBang.JudgementTick(Owner);                                     // locked on, the bomb fades in over the target with the charge (Owner.Alpha)
                    if (held >= GuardSeconds)
                    {
                        _guard = Guard.Primed; ChargeTint.Clear();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — locked on, the bomb hangs for the next shot to drop; else the next shot is the flash");
                    }
                    break;
                }
                case Guard.Primed:
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    if (p.PrimeDim > 0f) { SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim); }
                    SunSword.HoldPrimedTint(p, 1f);
                    BigBang.JudgementTick(Owner);                                     // the bomb over a locked target, following it
                    // Her release — the shoot state's first tick, before the engine has a pellet out — lets the hanging bomb go,
                    // or, with no bomb hanging, IS the flash: from where she stands, the charge spent.
                    if (released && hanging && BigBang.BeginDrop())
                    {
                        _guard = Guard.Dropping; _firedAt = GameClock.Now; _shotCharged = false; _retirePellet = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the hanging bomb is let go on her release");
                    }
                    else if (released && !hanging)
                    {
                        _guard = Guard.Idle; _whiteFading = true; _firedAt = GameClock.Now; _shotCharged = false; _retirePellet = true;
                        _flashPending = true; _flashAt = GameClock.Now;
                        _flashX = Memory.ReadFloat(Addresses.dunPositionX); _flashH = Memory.ReadFloat(Addresses.dunPositionZ); _flashY = Memory.ReadFloat(Addresses.dunPositionY);
                        BigBang.ReleaseJudgement();                                              // a hover fading off a lost lock: gone
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "released with nothing hanging — the flash from where she stands");
                    }
                    break;
                case Guard.Dropping:
                {
                    double since = (GameClock.Now - _firedAt).TotalSeconds;
                    float k = (float)Math.Max(0.0, 1.0 - since / TintFadeSeconds);
                    SunSword.HoldPrimedTint(p, k); SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    BigBang.JudgementTick(Owner);                                     // the fall, and the landing (LandDrop)
                    if (BigBang.TakeDropLanded()) { _guard = Guard.Idle; break; }
                    if (!BigBang.Dropping && !BigBang.LandingPending) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the drop was abandoned — the charge is spent"); Dissipate(); }
                    break;
                }
            }

            // A new pellet: retired (the drop's or the flash's — the release was the shot; the motion and its WHP already hers),
            // the charged bomb, or the plain bomb.
            int slot = NewPellet();
            if (slot < 0) return;
            long poolNow = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (_retirePellet || (hanging && BigBang.BeginDrop()))
            {
                if (!_retirePellet) { _guard = Guard.Dropping; _firedAt = GameClock.Now; _shotCharged = false; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the hanging bomb is let go"); }
                if (Memory.IsValidGuest(poolNow)) Memory.WriteInt(PlayerShotPool.FlagAddr(poolNow, slot), 0);   // never drawn (the blank cell), never flies
                _retirePellet = false;
            }
            else if (_shotCharged) { _shotCharged = false; FireBomb(slot, Shot.Bomb); }
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
                bool live;
                if (f.Sub >= 0)
                {   // the borrowed shot: flying while it is active and its phase is the flight; its object's position is the bomb's
                    long inst = _carrier.Instance;
                    if (f.Kind == Shot.Pellet && f.Burst)
                    {   // its burst drawn last tick; the engine had its impact frame (the native plant): the sub-shot is cut now
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + f.Sub * 2, 0);
                        EndFlight(f); continue;
                    }
                    live = Memory.ReadUShort(inst + ShotEffectPack.OffActive + f.Sub * 2) != 0 && Memory.ReadUShort(inst + ShotEffectPack.OffPhase + f.Sub * 2) < 3;
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
                    {   // out of its flight (its contact, a wall, its end): the bomb's visual here; the native impact runs on for a tick
                        f.Burst = true;
                        BombFx.Spawn(f.X, f.H, f.Y, PelletFxScale, ringRadius: 0f);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"plain bomb bursts at ({f.X:F0},{f.H:F0},{f.Y:F0})");
                        continue;
                    }
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
                else BombFx.Spawn(f.X, f.H, f.Y, PelletFxScale, ringRadius: 0f);              // a plain shot flying as a pellet (no pumpkin shot entered): the visual alone
            }
        }

        /// <summary>The bomb's blast at (x, h, y): the visual at <paramref name="fxScale"/> (drawn <paramref name="fxLift"/> above the
        /// point) with, if <paramref name="ring"/>, its ring out to the blast's reach; Big Bang's falloff blast at
        /// <paramref name="damage"/>/<paramref name="kick"/>/<paramref name="reach"/> of its own, every enemy turned to it, and —
        /// the drop alone — the flash from it.</summary>
        private static void Blast(float x, float h, float y, float fxScale, float damage, float kick, float reach, bool flash, bool ring, float fxLift = 0f)
        {
            BigBang.LastBlast = (x, h, y);
            BombFx.Spawn(x, h + fxLift, y, fxScale, ringRadius: ring ? BigBang.BlastRadius * reach : 0f);
            BigBang.PlantFalloff(x, h, y, damageScale: damage, kickScale: kick, reachScale: reach);
            BigBang.TurnEnemiesToward(x, y);
            if (flash) SunSword.FlashAt(SunSword.BombShotFlash, x, h, y, _planted);
        }
        /// <summary>The hanging bomb's landing (BigBang's owner callback): the full blast, drawn a little above the floor, and the flash.</summary>
        private static void LandDrop(int slot, float x, float h, float y) => Blast(x, h, y, HoverFxScale, 1f, 1f, 1f, flash: true, ring: true, fxLift: DropFxLift);

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

        private static Flight Begin(int slot, long pool, Shot kind)
        {
            long pa = PlayerShotPool.PosAddr(pool, slot);
            var f = new Flight { Kind = kind, Slot = slot, FiredAt = GameClock.Now, X = Memory.ReadFloat(pa), H = Memory.ReadFloat(pa + 4), Y = Memory.ReadFloat(pa + 8) };
            _flights.Add(f);
            _firedAt = GameClock.Now;
            ChargeTint.Clear();
            return f;
        }

        /// <summary>A bomb — charged (<see cref="Shot.Bomb"/>) or plain (<see cref="Shot.Pellet"/>): the pumpkin shot with the bomb's
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
                Memory.WriteFloat(CodeCaves.Mailbox.PelletKickStrength, BigBang.KickStrength * KickFor(PelletKick));
                Memory.WriteFloat(CodeCaves.Mailbox.PelletKickDecay, BigBang.KickDecay);
                Memory.WriteInt  (CodeCaves.Mailbox.PelletKickDamage, damage);
            }
            if (_carrier != null && BorrowedShots.Fire(_carrier, f.X, f.H, f.Y, vx, vh, vy, damage, life, plant: kind == Shot.Pellet, reload: PelletReload))
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
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}: the pumpkin shot #{f.Sub} carrying the bomb at {sc:0.#}×, in the pellet's place ({life} frames" + (kind == Shot.Pellet ? $", damage {damage}, kick {BigBang.KickStrength * KickFor(PelletKick):0.##}" : "") + ")");
                return;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}: the pumpkin shot is not entered on this floor — the pellet flies plain");
        }

        /// <summary>Every pellet drawn from the sheet's transparent cell, until <see cref="Unhide"/>.</summary>
        private static void Hide()
        {
            _spriteBefore = Memory.ReadInt(CodeCaves.Mailbox.PelletSpriteId);
            if (_spriteBefore == PelletSheetBakes.BlankCell) _spriteBefore = 0;
            Memory.WriteInt(CodeCaves.Mailbox.PelletSpriteId, PelletSheetBakes.BlankCell);
            _hiding = true;
        }
        private static void Unhide()
        {
            if (!_hiding) return;
            if (Memory.ReadInt(CodeCaves.Mailbox.PelletSpriteId) == PelletSheetBakes.BlankCell)   // still ours (nothing else wrote it meanwhile)
                Memory.WriteInt(CodeCaves.Mailbox.PelletSpriteId, _spriteBefore);
            _hiding = false;
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
            SunSword.HoldPrimedTint(SunSword.BombShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarLighting.EndDim();
            if (BladeProp.Active) SolarGlow.Fade();
            BigBang.ReleaseJudgement();
            _guard = Guard.Idle; _whiteFading = false; _flashPending = false;
        }

        /// <summary>Is (x, h, y) inside a live enemy's body — within its authored width (scaled with the unit) plus ContactPad
        /// across, and between its feet and its authored height up?</summary>
        private static bool Touching(float x, float h, float y)
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
                return true;
            }
            return false;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended, explosions dangerous again.</summary>
        internal static void Stop()
        {
            if (_guard != Guard.Idle || _whiteFading) SunSword.HoldPrimedTint(SunSword.BombShotFlash, 0f);
            EndFlights();
            Memory.WriteInt(CodeCaves.Mailbox.PelletKickDamage, 0);                         // no kick mark
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.Restore(); Unhide();
            BigBang.ReleaseJudgement(); BigBang.DriveImmunity(false); Ungraft(); BombModel.ReleaseTextures();
            SunSword.EndBlinding();
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in _planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            _planted.Clear();
            SolarLighting.ToanTintOwned = false;
            Array.Clear(_seen, 0, _seen.Length);
            _guard = Guard.Idle; _whiteFading = false; _shotHolding = false; _shotCharged = false; _retirePellet = false; _flashPending = false; _wasShooting = false;
        }
    }
}
