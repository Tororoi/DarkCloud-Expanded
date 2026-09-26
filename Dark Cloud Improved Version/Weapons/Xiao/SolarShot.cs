using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Sun Sword sphere — "Solar Shot": the Sun Sword's Solar Flash from Xiao's slingshot.
    /// Hold guard for <see cref="ChargeSeconds"/> and the slingshot whitens, the room darkens to the profile's
    /// prime dim, the cyan build-up runs on her and, primed, she carries the white and the gold disc the Sun Sword gives
    /// Toan. The next pellet she fires is the charge: five times its size, the disc riding it (a hidden copy of the
    /// slingshot placed on the pellet by the pellet-follow cave carries the disc), the white bleeding out of her over
    /// <see cref="TintFadeSeconds"/>, the dim held. The pellet landing on an enemy plunges the room to black over
    /// SolarLighting.RampFrames and then the flash goes off from the impact — the light hit on every enemy in reach of
    /// it, the 5 s blinding with their guards broken, the ease back to normal light — exactly the Sun Sword's
    /// (SunSword.FlashAt, SunSword.SolarShotFlash). A pellet that lands on nothing, or is out for <see cref="MissSeconds"/>,
    /// lets the dim ease back and the charge is spent. Driven from Super Steve's sphere dispatch; Solar Harvest is
    /// inherited alongside it there.</summary>
    internal static class SolarShot
    {
        private const string Tag = "[SolarShot] ";
        internal const string GlowDisc    = "catglowp";   // the cat's disc: the one glow disc resident while Xiao is the active character
        internal const int    GlowGoldRow = 8;            // the glow cave's palette row: the Angel Gear cat's gold, the Sun Sword's own colour
        internal const string WeaponModel = "c04w13";     // Super Steve's dungeon rig (item 312 = c04w13.chr): what SolarBlade whitens
        private const double  ChargeSeconds = 5.0;        // guard held this long primes the shot
        private const float   PelletScale = 5f;           // the charged pellet's sprite
        private const double  TintFadeSeconds = 0.25;     // her white, gone this long after the pellet leaves
        private const double  MissSeconds = 3.0;          // a pellet out this long without landing on anything: the charge is spent
        private const float   HitProximity = 40f;         // an enemy this close to where the pellet died = it landed on one
        internal const float  GlowSize = 0.4f;            // the disc at the pellet's size — on the pouch while primed, so it looks the same when it rides the pellet
        private const int     PouchNode = 4;              // the slingshot rig's pouch bone, null24: the fifth node from the root (SlingshotProp's layout)
        private const float   ShotWhp = 5f;               // weapon HP the charged pellet costs as it leaves, before Endurance (the Sun Sword's flash bill)
        private const float   SwingBase = 1.5f;           // a shot factor of 1 is this much WHP at zero Endurance
        private static readonly float[] CopyTint = { 0f, 0f, 0f };   // the copy on the pellet is never shown: it only carries the disc
        private const float   CopyDim = 1f;

        private enum Phase { Idle, Charging, Primed, Flying, HitPending }
        private static Phase _phase;
        private static DateTime _holdStart, _primedAt, _firedAt, _hitAt;
        private static int   _slot = -1;                  // the charged pellet while it flies
        private static float _lastX, _lastH, _lastY;      // where it was last seen (its impact point once it dies)
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static readonly List<(int slot, int ticks)> _planted = new List<(int, int)>();
        private static bool _nativeWarned;

        private static bool Native => (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew;

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
                    if (SunSword.BlindRunning) break;                             // one flash at a time
                    if (GuardWatch.IsGuarding()) { _phase = Phase.Charging; _holdStart = GameClock.Now; }
                    break;

                case Phase.Charging:
                {
                    if (SunSword.BlindRunning || !GuardWatch.IsGuarding()) { Dissipate(); break; }
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
                    float k = (float)Math.Max(0.0, 1.0 - since / TintFadeSeconds);   // her white and the slingshot's, gone over TintFadeSeconds
                    SunSword.HoldPrimedTint(p, k);
                    SolarBlade.Set(k, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarGlow.Tick();
                    long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                    bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, _slot)) != 0;
                    if (live)
                    {
                        long pa = PlayerShotPool.PosAddr(pool, _slot);
                        _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8);
                        if (since < MissSeconds) break;
                    }
                    if (!live && EnemyNear(_lastX, _lastY, HitProximity))
                    {   // it landed on an enemy: the plunge to black, then the flash
                        _phase = Phase.HitPending; _hitAt = GameClock.Now;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet landed at ({_lastX:F0},{_lastH:F0},{_lastY:F0})");
                        break;
                    }
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (live ? "pellet still out after 3 s" : "pellet landed on nothing") + " — the charge is spent");
                    Dissipate();
                    break;
                }

                case Phase.HitPending:
                {
                    double frames = (GameClock.Now - _hitAt).TotalSeconds * 60.0;
                    SolarLighting.DimRamp(p.PrimeDim, (float)(frames / SolarLighting.RampFrames));
                    if (frames < SolarLighting.RampFrames) break;
                    SunSword.HoldPrimedTint(p, 0f); SolarBlade.Clear(); SolarGlow.Hide(); EndPellet();
                    SunSword.FlashAt(p, _lastX, _lastH, _lastY, _planted);
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

        /// <summary>The charged pellet: its sprite at <see cref="PelletScale"/>, the disc moved from her onto it (a hidden copy
        /// of the slingshot, placed on the pellet each frame by the pellet-follow cave, is what the disc hangs on).</summary>
        private static void Fire(int slot)
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, slot), PelletScale);
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            _lastX = Memory.ReadFloat(pa); _lastH = Memory.ReadFloat(pa + 4); _lastY = Memory.ReadFloat(pa + 8);
            _slot = slot; _firedAt = GameClock.Now; _phase = Phase.Flying;
            ChargeTint.Clear();
            if (!Native)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the pellet-follow cave is not in this ISO — the disc stays on her (re-patch the ISO)"); }
                return;
            }
            float yaw = (float)Math.Atan2(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
            if (!SlingshotProp.Active && !SlingshotProp.SpawnProjectile(1f, yaw, CopyTint, CopyDim)) return;
            SlingshotProp.PlaceProjectile(_lastX, _lastH, _lastY, yaw);
            SlingshotProp.Maintain(0f);                                            // never shown: it only carries the disc
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowLift, 0f);
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowSpin, 0f);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowEnded, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowSlot, slot + 1);        // LAST: the cave places the copy from this frame
            uint node = SlingshotProp.CentreGuest != 0 ? SlingshotProp.CentreGuest : SlingshotProp.RootGuest;
            if (node != 0) SolarGlow.Show(GlowDisc, anchor: node, lift: 0f, palRow: GlowGoldRow, scale: GlowSize);   // re-hung onto the pellet
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged pellet: slot {slot}, ×{PelletScale:0} sprite, the disc on it");
        }

        private static void EndPellet()
        {
            if (_slot < 0) return;
            Memory.WriteInt(CodeCaves.Mailbox.PropFollowSlot, 0);
            if (SlingshotProp.Active) SlingshotProp.Despawn();
            _slot = -1;
        }

        /// <summary>The charge lapses (guard let go early, a pellet that hit nothing): everything back, the dim easing off.</summary>
        private static void Dissipate()
        {
            SunSword.HoldPrimedTint(SunSword.SolarShotFlash, 0f);
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Fade(); SolarLighting.EndDim(); EndPellet();
            _phase = Phase.Idle;
        }

        private static bool EnemyNear(float x, float y, float range)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long pos = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX);
                float dx = Memory.ReadFloat(pos) - x, dy = Memory.ReadFloat(pos + 8) - y;
                if (dx * dx + dy * dy <= range * range) return true;
            }
            return false;
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
