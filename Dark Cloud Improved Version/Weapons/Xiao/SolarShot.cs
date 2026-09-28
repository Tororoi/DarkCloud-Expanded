using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Sun Sword sphere — "Solar Shot": the Sun Sword's Solar Flash from Xiao's slingshot.
    /// Hold guard for <see cref="ChargeSeconds"/> and the slingshot whitens, the room darkens to the profile's
    /// prime dim, the cyan build-up runs on her and, primed, she carries the white and the gold disc the Sun Sword gives
    /// Toan. The next pellet she fires is the charge: five times its size, the disc riding it (the glow cave takes the pellet's
    /// own position every frame — CodeCaves.GlowPellet — so nothing carries it), her white and the dim held to the flash, as
    /// Toan's are through his swing. The pellet landing on an enemy plunges the room to black over
    /// SolarLighting.RampFrames and then the flash goes off from the impact — the light hit on every enemy in reach of
    /// it, the 5 s blinding with their guards broken, the ease back to normal light — exactly the Sun Sword's
    /// (SunSword.FlashAt, SunSword.SolarShotFlash). The flash goes off wherever the pellet ENDS — an enemy (spared the light
    /// hit, its share is the pellet's), a wall, or the end of its range; only a pellet still out after <see cref="MissSeconds"/>
    /// lets the dim ease back with the charge spent. Driven from Super Steve's sphere dispatch; Solar Harvest is
    /// inherited alongside it there.</summary>
    internal static class SolarShot
    {
        private const string Tag = "[SolarShot] ";
        internal const string GlowDisc    = "catglowp";   // the cat's disc: the one glow disc resident while Xiao is the active character
        internal const int    GlowGoldRow = 9;            // the glow cave's ONE-based palette row: 1–5 the elements, 6 none, 7 the Divine Beast blue, 8 the Angel Shooter white, 9 the Angel Gear gold — the Sun Sword's colour
        internal const string WeaponModel = "c04w13";     // Super Steve's dungeon rig (item 312 = c04w13.chr): what SolarBlade whitens
        private const double  ChargeSeconds = 2.0;        // guard held this long primes the shot
        private const float   PelletScale = 10f;          // the charged pellet's sprite
        private const float   GlowPull = 10f;             // the disc pulled toward the camera this far on the pellet (the discs' usual 5, and 5 more to clear the sprite)
        private const double  MissSeconds = 3.0;          // a pellet out this long without landing on anything: the charge is spent
        private const float   HitProximity = 20f;         // the pellet died this close to an enemy's HIT SPHERE edge = it landed on it (contact is at 2 + the sphere's radius; its position is read once a tick, up to two frames — ten units — before)
        internal const float  GlowSize = 0.75f;           // the disc wider than the ×10 stone (45 units at 1.0 — 0.4 sat behind the 20-unit sprite): on the pouch while primed at the same size, so it looks the same when it rides the pellet
        private const int     PouchNode = 4;              // the slingshot rig's pouch bone, null24: the fifth node from the root (SlingshotProp's layout)
        private const float   ShotWhp = 5f;               // weapon HP the charged pellet costs as it leaves, before Endurance (the Sun Sword's flash bill)
        private const float   SwingBase = 1.5f;           // a shot factor of 1 is this much WHP at zero Endurance

        private enum Phase { Idle, Charging, Primed, Flying, HitPending }
        private static Phase _phase;
        private static DateTime _holdStart, _primedAt, _firedAt, _hitAt, _seenAt;
        private static int   _slot = -1;                  // the charged pellet while it flies
        private static int   _hitSlot = -1;               // the enemy it landed on
        private static bool  _contactWarned;
        private static int   _contactSeen = PelletContacts.Fresh;
        private static float _lastX, _lastH, _lastY;      // where it was last seen (its impact point once it dies)
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static readonly List<(int slot, int ticks)> _planted = new List<(int, int)>();
        /// <summary>Every tick (16 ms) while Super Steve carries the sphere; <paramref name="active"/> false holds everything
        /// as it stands. <see cref="Stop"/> ends it when the sphere or the weapon goes.</summary>
        internal static void Drive(bool active)
        {
            SunSword.BlindTick();                                                 // the ease and the blinding run whoever fired them
            SunSword.ExpireHits(_planted);
            if (!active) return;
            var p = SunSword.SolarShotFlash;
            SolarLighting.ToanTintOwned = _phase == Phase.Charging || _phase == Phase.Primed;   // her tint is the charge's while it is held
            switch (_phase)
            {
                case Phase.Idle:
                    SolarGlow.Tick();                                             // a disc fading off a spent charge finishes fading
                    if (SunSword.BlindRunning) break;                             // one flash at a time
                    if (GuardWatch.IsGuarding()) { _phase = Phase.Charging; _holdStart = GameClock.Now; }
                    break;

                case Phase.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding()) { Dissipate(SunSword.BlindRunning ? "a blinding" : $"guard down (motion {Memory.ReadInt(CCharacter.Base + CCharacter.MotionId)})"); break; }
                    double held = (GameClock.Now - _holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / ChargeSeconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / ChargeSeconds));
                    ChargeTint.Ramp(ChargeSeconds - held);
                    if (held >= ChargeSeconds - SolarGlow.GrowSeconds) { ShowPouchGlow(); SolarGlow.Tick(); }
                    if (held >= ChargeSeconds)
                    {
                        _phase = Phase.Primed; _primedAt = GameClock.Now;
                        ChargeTint.Clear();
                        ShowPouchGlow();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed — the next pellet carries the flash");
                    }
                    break;
                }

                case Phase.Primed:
                {
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim);
                    ShowPouchGlow(); SolarGlow.Tick();
                    SunSword.HoldPrimedTint(p, 1f);
                    ChargedShotWhp.Arm(ShotWhp / SwingBase);                          // the next pellet's bill, taken by the engine as it leaves
                    int slot = NewPellet();
                    if (slot >= 0) Fire(slot);
                    break;
                }

                case Phase.Flying:
                {
                    double since = (GameClock.Now - _firedAt).TotalSeconds;
                    SunSword.HoldPrimedTint(p, 1f);                                   // her white and the slingshot's held to the flash, as Toan's are through his swing
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarGlow.Tick();
                    // The engine's own contact record (the pellet-contact cave): the pellet met an enemy — the flash from its
                    // hit sphere's centre, that enemy spared the light hit — or a wall — the charge is spent.
                    if (PelletContacts.Poll(ref _contactSeen, out var c) && c.Slot == _slot)
                    {
                        if (c.Enemy)
                        {
                            _hitSlot = PelletContacts.EnemyAtSphere(c); _lastX = c.X; _lastH = c.H; _lastY = c.Y;
                            if (_hitSlot < 0) _hitSlot = EnemyAt(c.X, c.Y);
                            _phase = Phase.HitPending; _hitAt = GameClock.Now;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet met slot {_hitSlot} at ({c.X:F0},{c.H:F0},{c.Y:F0}) (the engine's contact)");
                            break;
                        }
                        // A wall: the flash goes off there all the same — the pellet's end is the flash, whatever it met
                        _hitSlot = -1; _lastX = c.X; _lastH = c.H; _lastY = c.Y;
                        _phase = Phase.HitPending; _hitAt = GameClock.Now;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet met a wall at ({c.X:F0},{c.H:F0},{c.Y:F0}) — the flash there");
                        break;
                    }
                    long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                    bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, _slot)) != 0;
                    if (live)
                    {
                        long pa = PlayerShotPool.PosAddr(pool, _slot);
                        _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8); _seenAt = GameClock.Now;
                        if (since < MissSeconds) break;
                    }
                    if (!live)
                    {   // it ended with no record (an enemy, or nothing at all — its range): the plunge to black, then the flash where it ended
                        _hitSlot = EnemyAt(_lastX, _lastY);
                        _phase = Phase.HitPending; _hitAt = GameClock.Now;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet ended at ({_lastX:F0},{_lastH:F0},{_lastY:F0})" + (_hitSlot >= 0 ? $" on slot {_hitSlot}" : " on nothing") + " — the flash there");
                        break;
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet still out after 3 s at ({_lastX:F0},{_lastH:F0},{_lastY:F0}) — the charge is spent");
                    Dissipate("the pellet never ended");
                    break;
                }

                case Phase.HitPending:
                {
                    double frames = (GameClock.Now - _hitAt).TotalSeconds * 60.0;
                    SolarLighting.DimRamp(p.PrimeDim, (float)(frames / SolarLighting.RampFrames));
                    if (frames < SolarLighting.RampFrames) break;
                    SunSword.HoldPrimedTint(p, 0f); SolarBlade.Clear(); SolarGlow.Hide(); EndPellet();
                    SunSword.FlashAt(p, _lastX, _lastH, _lastY, _planted, excludeSlot: _hitSlot);   // the flash's light hit spares the one the pellet struck
                    _phase = Phase.Idle;
                    break;
                }
            }
        }

        /// <summary>The disc on her LIVE slingshot's pouch bone, at the pellet's size (re-hung there if it is up elsewhere).</summary>
        private static void ShowPouchGlow()
        {
            uint wpn = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
            if (!Memory.IsValidGuest(wpn)) return;
            uint root = Memory.ReadGuestPtr(Memory.ToMmu(wpn) + 0xBC);
            if (!Memory.IsValidGuest(root)) return;
            SolarGlow.Show(GlowDisc, anchor: root + (uint)(PouchNode * CFrameVu1.NodeStride), lift: 0f, palRow: GlowGoldRow, scale: GlowSize);
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

        /// <summary>The charged pellet: its sprite at <see cref="PelletScale"/>, the disc moved from her pouch onto the pellet itself
        /// (the glow cave reads the pellet's position from the shot pool every frame).</summary>
        private static void Fire(int slot)
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), PelletScale);
            long pa = PlayerShotPool.PosAddr(pool, slot);
            _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8);
            _slot = slot; _firedAt = GameClock.Now; _phase = Phase.Flying;
            PelletContacts.Sync(ref _contactSeen);                                 // only contacts from here on are this pellet's
            ChargeTint.Clear();
            SolarGlow.Show(GlowDisc, lift: 0f, palRow: GlowGoldRow, scale: GlowSize, pelletSlot: slot, pull: GlowPull);   // re-hung onto the pellet
            if (!PelletContacts.Native && !_contactWarned) { _contactWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the pellet-contact cave is not in this ISO — the landing is read from where the pellet died (re-patch the ISO)"); }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged pellet: slot {slot}, ×{PelletScale:0} sprite, the disc on it");
        }

        private static void EndPellet() => _slot = -1;

        /// <summary>The charge lapses (guard let go early, a pellet that hit nothing): everything back, the dim easing off.</summary>
        private static void Dissipate(string why = "spent")
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charge dissipates ({why}) — phase {_phase}, dim {SolarLighting.LastDim:0.00}, guarding {GuardWatch.IsGuarding()}, blinding {SunSword.BlindRunning}");
            SunSword.HoldPrimedTint(SunSword.SolarShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Fade(); SolarLighting.EndDim(); EndPellet();
            _phase = Phase.Idle;
        }

        /// <summary>The live enemy whose nearest hit sphere's edge is within HitProximity of (x, y) — the one the pellet died on
        /// (a big enemy's root can be far from where the pellet met its body); −1 for none.</summary>
        private static int EnemyAt(float x, float y)
        {
            int best = -1; float bestD = HitProximity;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                float d = BigBang.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), x, y);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>The sphere or the weapon went: everything down, a blinding of hers ended, the hit entries withdrawn.</summary>
        internal static void Stop()
        {
            if (_phase != Phase.Idle) SunSword.HoldPrimedTint(SunSword.SolarShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.Restore(); EndPellet();
            SunSword.EndBlinding();
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in _planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            _planted.Clear();
            SolarLighting.ToanTintOwned = false;
            Array.Clear(_seen, 0, _seen.Length);
            _phase = Phase.Idle; _slot = -1;
        }
    }
}
