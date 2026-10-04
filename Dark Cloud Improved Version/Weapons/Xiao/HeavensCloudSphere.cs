using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Heaven's Cloud sphere — a two-stage charge from Xiao's slingshot that ends in a wind-gem
    /// crowd-control blast. Her hold has two stages, each announced by the game's own charge flash: flash 1 (<see cref="ChargeLevel1Frac"/>)
    /// makes the shot "empowered" — from here the pellet, its damage and the wind burst grow with the hold, and ANY shot released
    /// past this point bursts on impact; flash 2 (full charge) is the maximum pellet, burst and blast radius. The burst detonates
    /// where the empowered pellet dies next to an enemy (the engine's contact record when the ISO has the pellet-contact cave): a
    /// wind shockwave with the weapon's selected element, damage in a large radius, and a heavy radial launch (Enemies.RadialKnockback)
    /// from a centre pulled back toward Xiao so everything is thrown clear of her. Faithful to the real Heaven's Cloud: the payoff is
    /// CONTROL, not raw damage. Driven from Super Steve's sphere dispatch (Quick Draw and Moonlit Focus are inherited alongside it).</summary>
    internal static class HeavensCloudSphere
    {
        private const string Tag = "[HeavensCloudSphere] ";
        private const float  MaxPelletScale      = 8f;          // pellet size at MAX charge (shot-pool +0x310)
        private const double ChargeGrowSeconds   = 3;           // hold time from base to FULL charge
        private const float  MaxDamageMultiplier = 1.5f;        // pellet damage at full charge (partial-charge payoff)
        private const float  ChargeLevel1Frac    = 0.125f;      // flash 1: scaling + the wind burst start here
        private const float  ChargeLevel2Frac    = 0.98f;       // flash 2: max (just shy of 1.0 so it reliably latches)
        private static bool  _flashedLevel1;                    // edge latch: one flash per charge stage
        private static bool  _flashedLevel2;
        private static bool  _holding;                          // currently drawing/holding a shot
        private static DateTime _holdStart;                     // when the current hold began
        private static float _holdFraction;                     // 0..1 charge fraction (frozen on release for the pellet)

        // The wind BLAST. Everything scales 0..1 across the empowered band (flash 1 → flash 2), so a barely-charged
        // shot pops a small gust and a full charge clears the room.
        private const float WindFxScaleMin   = 2f;      // burst visual at flash 1
        private const float WindFxScaleMax   = 5f;      // burst visual at full charge
        private const float WindRadiusMin    = 60f;     // blast radius at flash 1 (world units)
        private const float WindRadiusMax    = 160f;    // blast radius at full charge
        private const float WindDamageFrac   = 0.75f;   // blast damage = this × the weapon's attack, × charge
        // Knockback. Enemies.RadialKnockback derives each enemy's force from how far it must travel to clear the
        // blast (distance ≈ force²/(2·decay)), so these only set the CHARACTER of the launch, not its distance —
        // an enemy at the centre is thrown the full radius and one at the edge is nudged. Decay is the drain rate
        // (vanilla runs 0.10 for a Pirate's Chariot to 0.30 for a Dasher); MaxForce is the anti-orbit clamp.
        private const float WindKnockDecay   = 0.25f;   // a brisk, punchy shove rather than a long glide
        private const float WindKnockMaxForce = 6f;     // clamp — a flat 40 threw enemies clean off the map
        private const float WindKnockMargin  = 0f;      // land them ON the edge; no extra shove past it
        private const float WindKnockScale   = 0.5f;    // the model's ideal slide runs LONG in practice — trim it
        // The KNOCKBACK centre sits a little SHORT of the impact, back along the line from Xiao — the VISUAL stays
        // on the impact itself. A pellet can strike a part of the enemy BEHIND its centre, which would put a
        // centred blast behind the enemy and throw it at the player; pulling only the launch origin toward Xiao
        // keeps enemies on the far side of it, so they are always pushed away, without shifting the effect.
        private const float WindCenterPullback = 5f;
        // Play-rate of the burst animation, as a MULTIPLIER of the motion's own baked rate (GemBurst reads the
        // real KEY step and scales it — the engine's override is absolute, and a motion's native rate is NOT 1.0).
        // The bigger the burst, the slower it plays: the animation was authored for a small thrown-gem puff, so at
        // full scale the stock rate makes a room-sized gust look like it snaps rather than billows.
        private const float WindFxSpeedAtMin = 1.0f;    // at flash-1 scale — the motion's own rate, untouched
        private const float WindFxSpeedAtMax = 0.5f;    // at full charge — half speed
        private const float WindColRadiusFrac  = 0.55f;  // damage sphere vs. the knockback radius — the wind pushes
                                                          // further than it hurts, so the edge shoves without hitting
        // The burst model's geometry RISES from its own origin, so anchoring the origin on the hit mark leaves the
        // effect sitting above it — and scaling the model scales that rise too, which is why it looked worse the
        // bigger the charge. The correction therefore has to scale WITH the burst, not be a fixed nudge: drop the
        // origin by this much per 1x of scale, so the visible burst stays centred on the impact at every size.
        private const float WindFxRisePerScale = 5f;

        // The armed pellet, tracked from the frame it is fired to the frame it dies. Its LAST KNOWN POSITION is
        // the impact point. We track the pellet rather than watching for enemy damage because a GUARDED hit deals
        // no damage and does not advance the engine's hit ring — but the pellet still dies on the guard, so the
        // burst and the shove happen regardless of whether the blow got through. (An enemy must be near the death
        // point, or the pellet simply expired at the end of its flight and there is nothing to burst on.)
        private static int      _armedSlot = -1;      // shot-pool slot of the empowered pellet in flight
        private static float    _armedCharge;         // 0..1 across the empowered band, frozen at the shot
        private static float    _armedX, _armedH, _armedY;   // its last seen position
        private static int      _armedElement;        // the weapon's selected element, frozen at the shot
        private const float WindImpactProximity = 40f;  // an enemy must be this close to the pellet's death point

        private static readonly bool[] _pelletHandled = new bool[PlayerShotPool.SlotCount];   // per-slot "already grown" latch — a pellet is stamped exactly once
        private static int _contactSeen = PelletContacts.Fresh;

        /// <summary>Heaven's Cloud (Heaven's Cloud sphere) — a two-stage CHARGE, ending in a crowd-control blast.
        /// Tracks the hold as a 0..1 fraction (frozen on release so the fired pellet reads it) and flashes Xiao at
        /// each stage. Past flash 1 the shot is "empowered": the pellet and its damage grow with
        /// the hold, and the shot arms a wind burst that detonates on impact (see <see cref="ImpactBurstDrive"/>).
        /// When inactive everything resets (latches cleared).</summary>
        internal static void Drive(bool active)
        {
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding  = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;

            // Hold time → 0..1 charge fraction; frozen on release (draw 0xB / nocked-hold 0xC), base on a tap.
            if (holding)
            {
                if (!_holding) { _holdStart = GameClock.Now; _holding = true; }
                _holdFraction = (float)Math.Min(1.0, (GameClock.Now - _holdStart).TotalSeconds / ChargeGrowSeconds);
            }
            else _holding = false;

            // How far into the EMPOWERED band (flash 1 → flash 2) the charge is: 0 below flash 1, 1 at full. This
            // one number drives the pellet, the burst visual, the blast radius and the blast damage.
            float empowered = EmpoweredFraction(_holdFraction);
            if (active && holding)                                       // the shot's weapon HP: charged from flash 1
            {
                ChargedShotWhp.Arm(empowered > 0f ? ChargedShotWhp.ChargedFactor : 1f);
                // The charge on her: a ramp into flash 1, again into flash 2, nothing past it.
                ChargeTint.Ramp(_holdFraction < ChargeLevel1Frac ? (ChargeLevel1Frac - _holdFraction) * ChargeGrowSeconds
                              : _holdFraction < ChargeLevel2Frac ? (ChargeLevel2Frac - _holdFraction) * ChargeGrowSeconds : 0);
            }
            else if (active) ChargeTint.Clear();

            // Grow the fired pellet + scale its damage, once each. A shot fired while empowered arms the burst.
            long poolBase = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (Memory.IsValidGuest(poolBase))
            {
                float pelletScale = active ? 1f + empowered * (MaxPelletScale - 1f) : 1f;
                bool bigPellet = pelletScale > 1.01f;
                for (int i = 0; i < PlayerShotPool.SlotCount; i++)
                {
                    bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                    if (bigPellet && live && !_pelletHandled[i])
                    {
                        Memory.WriteFloat(PlayerShotPool.ScaleAddr(poolBase, i), pelletScale);
                        long dmgA = PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)(Memory.ReadInt(dmgA) * (1f + empowered * (MaxDamageMultiplier - 1f))));
                        _armedSlot   = i;                  // ANY empowered shot bursts on impact, not just a max one
                        PelletContacts.Sync(ref _contactSeen);   // only contacts from here on are this pellet's
                        _armedCharge = empowered;
                        // The burst LOOKS like wind but HURTS like the weapon: it inherits whatever element is
                        // selected on Super Steve (0 = none). Frozen at the shot, like the charge.
                        _armedElement = Weapons.SelectedElementBits(Weapons.EquippedRecord());
                        _pelletHandled[i] = true;
                    }
                    else if (!live) _pelletHandled[i] = false;
                }
            }

            // TWO flashes, each edge-latched and re-armed on release: flash 1 = "empowered from here", flash 2 =
            // "maxed, holding longer buys nothing". Both use the game's own charge-complete pulse.
            if (active && holding)
            {
                if (_holdFraction >= ChargeLevel1Frac && !_flashedLevel1)
                { Player.FlashChargeComplete(); _flashedLevel1 = true; }
                if (_holdFraction >= ChargeLevel2Frac && !_flashedLevel2)
                { Player.FlashChargeComplete(); _flashedLevel2 = true; }
            }
            else if (!holding) { _flashedLevel1 = false; _flashedLevel2 = false; }

            ImpactBurstDrive(active);
        }

        /// <summary>Position within the EMPOWERED band: 0 below flash 1 (an ordinary shot — no growth, no burst),
        /// ramping to 1 at full charge. Everything the charge scales reads this rather than the raw hold, so the
        /// first flash is a real threshold rather than a cosmetic marker.</summary>
        private static float EmpoweredFraction(float holdFraction)
        {
            if (holdFraction < ChargeLevel1Frac) return 0f;
            return Math.Min(1f, (holdFraction - ChargeLevel1Frac) / (ChargeLevel2Frac - ChargeLevel1Frac));
        }

        /// <summary>Detonate the armed wind burst when the empowered pellet lands.
        ///
        /// The trigger is the PELLET's own death, not enemy damage. Earlier versions watched the engine's hit ring
        /// (CheckDmg's hitCnt), but a GUARDED hit deals no damage and never advances that ring — so guarding made
        /// the whole effect vanish. The pellet dies on a guard just the same, so tracking the pellet gives an
        /// impact signal that survives guards, and its last position IS the point of impact. A pellet that simply
        /// expired at the end of its flight has no enemy near it, and bursts on nothing.
        ///
        /// The burst's DAMAGE is the engine's: the effect carries its own collision sphere, widened to cover the
        /// area, so CheckDmg resolves it and guards/elements/death/drops all behave. It must never be an HP write —
        /// that skips the death path and leaves an unkillable walking corpse (see Enemies.RadialKnockback).
        ///
        /// The VISUAL sits on the impact; only the KNOCKBACK origin is pulled back toward Xiao, so enemies are
        /// thrown away from her even when the pellet struck the far side of one.</summary>
        private static void ImpactBurstDrive(bool active)
        {
            if (!active)
            {
                if (_armedSlot >= 0) _armedSlot = -1;
                GemBurst.Restore(MasekiEffect.Wind);   // hand the shared collision radius back to the game
                return;
            }
            if (_armedSlot < 0) return;

            long poolBase = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) { _armedSlot = -1; return; }

            // The engine's own contact record first (the pellet-contact cave): an enemy — the burst on the hit sphere it struck;
            // a wall — no burst. Without a record (an ISO without the cave), the pellet's death and the nearest enemy decide.
            if (PelletContacts.Poll(ref _contactSeen, out var c) && c.Slot == _armedSlot)
            {
                if (!c.Enemy) { _armedSlot = -1; return; }
                _armedX = c.X; _armedH = c.H; _armedY = c.Y;
            }
            else
            {
                // Still in flight → keep its position fresh; the last one we see before it dies is the impact point.
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, _armedSlot)) != 0)
                {
                    long pp = PlayerShotPool.PosAddr(poolBase, _armedSlot);
                    _armedX = Memory.ReadFloat(pp);
                    _armedH = Memory.ReadFloat(pp + 4);
                    _armedY = Memory.ReadFloat(pp + 8);
                    return;
                }
                if (!EnemyNear(_armedX, _armedY, WindImpactProximity)) { _armedSlot = -1; return; }   // hit a wall / flew its full range
            }
            _armedSlot = -1;                                     // it landed — one blast per empowered shot

            float charge = _armedCharge;                         // 0..1 across the empowered band
            float scale  = WindFxScaleMin + charge * (WindFxScaleMax - WindFxScaleMin);
            float radius = WindRadiusMin  + charge * (WindRadiusMax  - WindRadiusMin);
            int   damage = (int)(Player.Weapon.GetCurrentWeaponAttack() * WindDamageFrac * (0.5f + 0.5f * charge));

            // VISUAL: on the impact. Its geometry rises from its own origin and that rise scales with the model,
            // so the origin has to drop proportionally for the burst to stay centred on the impact at every size.
            float fxSpeed = WindFxSpeedAtMin + charge * (WindFxSpeedAtMax - WindFxSpeedAtMin);
            GemBurst.Show(MasekiEffect.Wind, _armedX, _armedH - scale * WindFxRisePerScale, _armedY,
                          scale, damage, radius * WindColRadiusFrac, fxSpeed, _armedElement);

            // KNOCKBACK: from a centre pulled back toward Xiao, so everything is thrown away from her.
            float px = Memory.ReadFloat(PlayerAddresses.DunPositionX), py = Memory.ReadFloat(PlayerAddresses.DunPositionX + 8);
            float ax = _armedX - px, ay = _armedY - py;          // player → impact
            float alen = (float)Math.Sqrt(ax * ax + ay * ay);
            float kx = _armedX, ky = _armedY;
            if (alen > 0.001f)
            {
                kx -= ax / alen * WindCenterPullback;
                ky -= ay / alen * WindCenterPullback;
            }
            Enemies.RadialKnockback(kx, ky, radius, WindKnockDecay, WindKnockMaxForce, WindKnockMargin,
                                    WindKnockScale);
        }

        /// <summary>Is any live enemy within <paramref name="range"/> of (x, y), by its own position? Distinguishes a pellet that
        /// LANDED on something from one that expired at the end of its flight or clipped a wall (the fallback without the contact cave).</summary>
        private static bool EnemyNear(float x, float y, float range)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long up = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(up) - x, dy = Memory.ReadFloat(up + 8) - y;
                if (dx * dx + dy * dy <= range * range) return true;
            }
            return false;
        }
    }
}
