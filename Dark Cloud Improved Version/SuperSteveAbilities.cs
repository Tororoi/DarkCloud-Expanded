using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Supporting implementations for Super Steve's SynthSphere inheritance. The master dispatch loop lives in
    /// <see cref="CustomXiaoEffects.SuperSteveEffect"/>; these are the per-ability drivers/helpers it pulses
    /// each tick (one call per ability, gated by which sphere grants it). Callers qualify them
    /// (<c>SuperSteveAbilities.X</c>), so the names omit the "SuperSteve" prefix.
    ///
    /// Enemy-side abilities reuse the <c>CustomToanEffects</c> drivers directly (they only touch enemy data);
    /// what lives here is the Xiao body adaptations. Each driver is named after its SOURCE weapon (the one
    /// whose sphere grants it): <c>DriveSmallSword</c> = Quick Draw, <c>DriveTsukikage</c> = Moonlit Focus,
    /// <c>DriveHeavensCloud</c> = the two-stage charge → wind-gem crowd-control blast, <c>DriveAgasSword</c> =
    /// Defensive Legacy,
    /// <c>DriveBraveArk</c> = Hero's Courage.
    /// </summary>
    internal static class SuperSteveAbilities
    {
        /// <summary>The source weapon id of the single SynthSphere attached to Super Steve's record at
        /// <paramref name="rec"/> (0 if none). A weapon holds at most one SynthSphere (id 0x5A), so the first
        /// one found is the only one — its source id (attach-entry +0x02) selects the inherited effect.</summary>
        internal static int AttachedSphere(long rec)
        {
            for (int slot = 0; slot < WeaponCollision.WeaponAttachSlotCount; slot++)
            {
                long entry = rec + WeaponCollision.WeaponAttachSlot0Offset +
                             slot * WeaponCollision.WeaponAttachSlotStride;
                if (Memory.ReadUShort(entry) == WeaponCollision.AttachBoard.SynthSphereId)
                    return Memory.ReadUShort(entry + WeaponCollision.AttachBoard.EntrySourceId);
            }
            return 0;
        }

        // ── Quick Draw (Small Sword / Tsukikage / Heaven's Cloud sphere) ──
        private const float ShotFireFrame = 251.0f;  // inside the (251,252) pellet-release window (shoot motion idx 13)
        private static bool _xqReleaseArmed;          // edge latch so one X-release = one instant shot
        private const ushort BattleSpeed = 350;       // effective Speed in Super Steve's BATTLE copy (past 99) → keeps the rate-of-fire gauge from being the bottleneck

        /// <summary>
        /// Quick Draw inheritance for Xiao, driven each tick from <see cref="CustomXiaoEffects.SuperSteveEffect"/>.
        /// Xiao (controlled) shares Toan's shot plumbing — motion-frame cursor = AnimFrameCursor (0x21EA2010),
        /// shot action-state = ChargeActionState (0x21DC4494). Her shot is three c04b motions: draw (0xB, frames
        /// 240→251), a zero-speed "nocked" HOLD parked on 250 (0xC) that lasts until the fire input releases,
        /// then shoot (0xD) — the pellet leaves at frame 251.
        ///
        /// Vanilla feel is preserved: hold X to nock/aim, the shot fires on RELEASE. Quick Draw makes that
        /// release instant. Two levers:
        ///   1. Rate-of-fire: the between-shots gauge (0x21DC44C8) fills at effectiveSpeed/30 and gates the next
        ///      shot at 100; we write Super Steve's BATTLE-copy speed (+8) past the game's 99 cap so it refills
        ///      near-instantly. Menu/inventory speed is untouched (still reads 99).
        ///   2. Instant release: the moment the engine's release flag (0x21DC4498, the real X-release — never
        ///      forced) is set during the draw/hold, we jump the state straight to shoot (0xD) and drop the
        ///      cursor onto the shoot-motion start (251.0) so the pellet crosses the (251,252) fire window
        ///      exactly once. Edge-latched (<see cref="_xqReleaseArmed"/>) so one release = one pellet. (251.5
        ///      sat inside the window and double-fired; 251.0 = the clean boundary.)
        /// The draw motion plays at a fixed KEY rate that speed never scales — only the gauge does (motionDrive,
        /// dun 0x1DB7450) — which is why the fix is gauge-speed + release-jump, not a motion-rate override.
        /// </summary>
        internal static void DriveSmallSword(bool active)
        {
            // Only touch the battle-speed when the BATTLE weapon really is Super Steve (it can lag the equipped
            // record during swaps). Writing past the 99 cap (BATTLE copy only) removes the between-shots wait.
            bool boosted = active && Memory.ReadUShort(WeaponCollision.BattleWeaponRecord) == Items.supersteve;
            long battleSpeedAddr = WeaponCollision.BattleWeaponRecord + WeaponCollision.EffSpeedOffset;
            if (boosted && Memory.ReadUShort(battleSpeedAddr) != BattleSpeed)
                Memory.WriteUShort(battleSpeedAddr, BattleSpeed);

            // Instant shot on X RELEASE: while drawing (0xB) or holding (0xC), the moment the fire input is
            // released (engine sets XiaoShotReleaseFlag), jump to the shoot state (0xD) and drop the cursor into
            // the fire window (251). Holding still aims; a tap fires almost instantly. Edge-triggered.
            int shotState = Memory.ReadInt(WeaponCollision.ChargeActionState);
            bool inDrawOrHold = shotState == WeaponCollision.XiaoShotDraw ||
                                shotState == WeaponCollision.XiaoShotHold;
            bool released = inDrawOrHold && Memory.ReadInt(WeaponCollision.XiaoShotReleaseFlag) != 0;
            if (boosted && released && !_xqReleaseArmed)
            {
                Memory.WriteInt(WeaponCollision.XiaoShotReleaseFlag, 1);
                Memory.WriteInt(WeaponCollision.ChargeActionState, WeaponCollision.XiaoShotShoot);
                Memory.WriteFloat(WeaponCollision.AnimFrameCursor, ShotFireFrame);
            }
            _xqReleaseArmed = released;
        }

        // ── Defensive Legacy (Aga's Sword) ──
        private const int AgasDefenseBoost = 15;   // mirrors CustomToanEffects.AgasSwordEffect
        private static bool _ssAgasApplied;        // whether the +15 defense is currently on Xiao

        /// <summary>Defensive Legacy (Aga's Sword): balanced ±<see cref="AgasDefenseBoost"/> on Xiao's defense.</summary>
        internal static void DriveAgasSword(bool want)
        {
            if (want && !_ssAgasApplied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() + AgasDefenseBoost); _ssAgasApplied = true; }
            else if (!want && _ssAgasApplied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() - AgasDefenseBoost); _ssAgasApplied = false; }
        }

        /// <summary>Hero's Courage (Brave Ark): while <paramref name="active"/>, clear Freeze/Poison/Curse/Goo
        /// from Xiao's status word. Stateless (nothing to restore), so it just no-ops when inactive.</summary>
        internal static void DriveBraveArk(bool active)
        {
            if (!active) return;
            const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                      ToanState.StatusCurse  | ToanState.StatusGoo;
            ushort status = Memory.ReadUShort(Player.Xiao.status);
            if ((status & resistMask) != 0)
                Memory.WriteUShort(Player.Xiao.status, (ushort)(status & ~resistMask));
        }

        // ── Angel Gear (Xiao's own weapon: slow party-wide HP regen) ──
        private const double AngelGearHealSeconds = 5.0;   // matches CustomXiaoEffects.AngelGearEffect's 5000ms cadence
        private const ushort AngelGearHealAmount  = 1;
        private static DateTime _angelGearNextHeal = DateTime.MinValue;

        /// <summary>Angel Gear: while <paramref name="active"/> and in walking mode, every
        /// <see cref="AngelGearHealSeconds"/> heal each ally by <see cref="AngelGearHealAmount"/> (skipping the
        /// dead and the already-full). Xiao is healed too UNLESS the equipped weapon carries the native Heal
        /// build-up attribute (Special2 % 16 in 8..11), which already regenerates her — avoids double-healing.
        /// Stateless apart from the interval timer, so it just no-ops when inactive.</summary>
        internal static void DriveAngelGear(bool active)
        {
            if (!active || !Player.CheckDunIsWalkingMode()) return;
            if (DateTime.UtcNow < _angelGearNextHeal) return;
            _angelGearNextHeal = DateTime.UtcNow.AddSeconds(AngelGearHealSeconds);

            HealAlly(Player.Toan.GetHp(),   Player.Toan.GetMaxHp(),   Player.Toan.SetHp);
            HealAlly(Player.Goro.GetHp(),   Player.Goro.GetMaxHp(),   Player.Goro.SetHp);
            HealAlly(Player.Ruby.GetHp(),   Player.Ruby.GetMaxHp(),   Player.Ruby.SetHp);
            HealAlly(Player.Ungaga.GetHp(), Player.Ungaga.GetMaxHp(), Player.Ungaga.SetHp);
            HealAlly(Player.Osmond.GetHp(), Player.Osmond.GetMaxHp(), Player.Osmond.SetHp);

            // Xiao only if the equipped weapon lacks the native Heal attribute (else the game already regens her).
            int special2 = Player.Weapon.GetCurrentWeaponSpecial2() % 16;
            if (special2 < 8 || special2 > 11)
                HealAlly(Player.Xiao.GetHp(), Player.Xiao.GetMaxHp(), Player.Xiao.SetHp);
        }

        private static void HealAlly(ushort hp, int maxHp, Action<ushort> setHp)
        {
            if (hp > 0 && hp < maxHp) setHp((ushort)(hp + AngelGearHealAmount));
        }

        // ── Moonlit Focus + Heaven's Cloud (two-stage charge → a wind-gem crowd-control blast) ──
        // Faithful to the real Heaven's Cloud: the payoff is CONTROL, not raw damage. Xiao's hold has TWO stages,
        // each announced by the game's own charge flash:
        //   flash 1 (ChargeLevel1Frac) — the shot is "empowered": from here the pellet and the wind burst grow
        //                                with the hold, and ANY shot released past this point bursts on impact.
        //   flash 2 (full charge)      — maximum pellet, maximum burst, maximum blast radius.
        // On impact the burst detonates as a wind shockwave: damage in a LARGE radius plus a heavy RADIAL launch
        // that throws everything caught in it clear of the blast (Enemies.RadialKnockback).
        private const float HcPelletScale    = 8f;          // pellet size at MAX charge (shot-pool +0x310)
        private const float HcSlingshotScale = 1f;          // slingshot 'c04w' mesh at MAX charge
        private const double ChargeGrowSeconds = 3;         // hold time from base to FULL charge
        private const float HcMaxDamageMult  = 1.5f;        // pellet damage at full charge (partial-charge payoff)
        private const float ChargeLevel1Frac = 0.125f;        // flash 1: scaling + the wind burst start here
        private const float ChargeLevel2Frac = 0.98f;       // flash 2: max (just shy of 1.0 so it reliably latches)
        private static bool _ssFlashedLevel1;               // edge latch: one flash per charge stage
        private static bool _ssFlashedLevel2;
        private static float _ssSlingScale = 1f;            // current slingshot mesh scale
        private static bool  _ssHolding;                    // currently drawing/holding a shot
        private static DateTime _ssHoldStart;               // when the current hold began
        private static float _ssHoldFrac;                   // 0..1 charge fraction (frozen on release for the pellet)

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
        private static int      _ssArmedSlot = -1;      // shot-pool slot of the empowered pellet in flight
        private static float    _ssArmedCharge;         // 0..1 across the empowered band, frozen at the shot
        private static float    _ssArmedX, _ssArmedH, _ssArmedY;   // its last seen position
        private static int      _ssArmedElement;        // the weapon's selected element, frozen at the shot
        private const float WindImpactProximity = 40f;  // an enemy must be this close to the pellet's death point

        // Per-slot "already applied" latches — one per driver, so a pellet gets each effect exactly once.
        private static readonly bool[] _moonlitHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];
        private static readonly bool[] _hcHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];

        /// <summary>Moonlit Focus (Tsukikage / Heaven's Cloud): doubles each newly-fired pellet's +0x1C0
        /// velocity (2× shot speed), once per pellet. Per-slot latched; the latch clears when the slot frees.</summary>
        internal static void DriveTsukikage(bool active)
        {
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) return;   // pool not allocated / bad pointer
            for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if (active && live && !_moonlitHandled[i])
                {
                    long vel = WeaponCollision.PlayerShotPool.VelAddr(poolBase, i);
                    for (int c = 0; c < 3; c++)
                        Memory.WriteFloat(vel + c * 4, Memory.ReadFloat(vel + c * 4) * 2f);
                    _moonlitHandled[i] = true;
                }
                else if (!live) _moonlitHandled[i] = false;
            }
        }

        /// <summary>Heaven's Cloud (Heaven's Cloud sphere) — a two-stage CHARGE, ending in a crowd-control blast.
        /// Tracks the hold as a 0..1 fraction (frozen on release so the fired pellet reads it) and flashes Xiao at
        /// each stage. Past flash 1 the shot is "empowered": the slingshot, the pellet and its damage all grow with
        /// the hold, and the shot arms a wind burst that detonates on impact (see <see cref="ImpactBurstDrive"/>).
        /// When inactive everything resets (slingshot back to 1×, latches cleared).</summary>
        internal static void DriveHeavensCloud(bool active)
        {
            int shotState = Memory.ReadInt(WeaponCollision.ChargeActionState);
            bool holding  = shotState == WeaponCollision.XiaoShotDraw || shotState == WeaponCollision.XiaoShotHold;
            bool shooting = shotState == WeaponCollision.XiaoShotShoot;

            // Hold time → 0..1 charge fraction; frozen on release (draw 0xB / nocked-hold 0xC), base on a tap.
            if (holding)
            {
                if (!_ssHolding) { _ssHoldStart = DateTime.UtcNow; _ssHolding = true; }
                _ssHoldFrac = (float)Math.Min(1.0, (DateTime.UtcNow - _ssHoldStart).TotalSeconds / ChargeGrowSeconds);
            }
            else _ssHolding = false;

            // How far into the EMPOWERED band (flash 1 → flash 2) the charge is: 0 below flash 1, 1 at full. This
            // one number drives the pellet, the burst visual, the blast radius and the blast damage.
            float empowered = EmpoweredFrac(_ssHoldFrac);

            // Grow the fired pellet + scale its damage, once each. A shot fired while empowered arms the burst.
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (Memory.IsValidGuest(poolBase))
            {
                float pelletScale = active ? 1f + empowered * (HcPelletScale - 1f) : 1f;
                bool bigPellet = pelletScale > 1.01f;
                for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
                {
                    bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                    if (bigPellet && live && !_hcHandled[i])
                    {
                        Memory.WriteFloat(WeaponCollision.PlayerShotPool.ScaleAddr(poolBase, i), pelletScale);
                        long dmgA = WeaponCollision.PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)(Memory.ReadInt(dmgA) * (1f + empowered * (HcMaxDamageMult - 1f))));
                        _ssArmedSlot   = i;                  // ANY empowered shot bursts on impact, not just a max one
                        _ssArmedCharge = empowered;
                        // The burst LOOKS like wind but HURTS like the weapon: it inherits whatever element is
                        // selected on Super Steve (0 = none). Frozen at the shot, like the charge.
                        _ssArmedElement = Weapons.SelectedElementBits(Weapons.EquippedRecord());
                        _hcHandled[i] = true;
                    }
                    else if (!live) _hcHandled[i] = false;
                }
            }

            // Slingshot 'c04w' mesh: grow with the hold, HOLD through the shoot (0xD), snap back after. Multiply-
            // scale preserves the frame's baked rotation; _ssHoldFrac stays frozen for 0xB/0xC/0xD.
            _ssSlingScale = (active && (holding || shooting)) ? 1f + empowered * (HcSlingshotScale - 1f) : 1f;
            Weapons.ScaleWeaponFrameByName(WeaponCollision.XiaoSlingMeshNameWord, _ssSlingScale);

            // TWO flashes, each edge-latched and re-armed on release: flash 1 = "empowered from here", flash 2 =
            // "maxed, holding longer buys nothing". Both use the game's own charge-complete pulse.
            if (active && holding)
            {
                if (_ssHoldFrac >= ChargeLevel1Frac && !_ssFlashedLevel1)
                { Player.FlashChargeComplete(); _ssFlashedLevel1 = true; }
                if (_ssHoldFrac >= ChargeLevel2Frac && !_ssFlashedLevel2)
                { Player.FlashChargeComplete(); _ssFlashedLevel2 = true; }
            }
            else if (!holding) { _ssFlashedLevel1 = false; _ssFlashedLevel2 = false; }

            ImpactBurstDrive(active);
        }

        /// <summary>Position within the EMPOWERED band: 0 below flash 1 (an ordinary shot — no growth, no burst),
        /// ramping to 1 at full charge. Everything the charge scales reads this rather than the raw hold, so the
        /// first flash is a real threshold rather than a cosmetic marker.</summary>
        private static float EmpoweredFrac(float holdFrac)
        {
            if (holdFrac < ChargeLevel1Frac) return 0f;
            return Math.Min(1f, (holdFrac - ChargeLevel1Frac) / (ChargeLevel2Frac - ChargeLevel1Frac));
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
                if (_ssArmedSlot >= 0) _ssArmedSlot = -1;
                GemBurst.Restore(MasekiEffect.Wind);   // hand the shared collision radius back to the game
                return;
            }
            if (_ssArmedSlot < 0) return;

            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) { _ssArmedSlot = -1; return; }

            // Still in flight → keep its position fresh; the last one we see before it dies is the impact point.
            if (Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, _ssArmedSlot)) != 0)
            {
                long pp = WeaponCollision.PlayerShotPool.PosAddr(poolBase, _ssArmedSlot);
                _ssArmedX = Memory.ReadFloat(pp);
                _ssArmedH = Memory.ReadFloat(pp + 4);
                _ssArmedY = Memory.ReadFloat(pp + 8);
                return;
            }

            _ssArmedSlot = -1;                                   // it landed — one blast per empowered shot
            if (!EnemyNear(_ssArmedX, _ssArmedY, WindImpactProximity)) return;   // hit a wall / flew its full range

            float charge = _ssArmedCharge;                       // 0..1 across the empowered band
            float scale  = WindFxScaleMin + charge * (WindFxScaleMax - WindFxScaleMin);
            float radius = WindRadiusMin  + charge * (WindRadiusMax  - WindRadiusMin);
            int   damage = (int)(Player.Weapon.GetCurrentWeaponAttack() * WindDamageFrac * (0.5f + 0.5f * charge));

            // VISUAL: on the impact. Its geometry rises from its own origin and that rise scales with the model,
            // so the origin has to drop proportionally for the burst to stay centred on the impact at every size.
            float fxSpeed = WindFxSpeedAtMin + charge * (WindFxSpeedAtMax - WindFxSpeedAtMin);
            GemBurst.Show(MasekiEffect.Wind, _ssArmedX, _ssArmedH - scale * WindFxRisePerScale, _ssArmedY,
                          scale, damage, radius * WindColRadiusFrac, fxSpeed, _ssArmedElement);

            // KNOCKBACK: from a centre pulled back toward Xiao, so everything is thrown away from her.
            float px = Memory.ReadFloat(Player.dunPositionX), py = Memory.ReadFloat(Player.dunPositionX + 8);
            float ax = _ssArmedX - px, ay = _ssArmedY - py;      // player → impact
            float alen = (float)Math.Sqrt(ax * ax + ay * ay);
            float kx = _ssArmedX, ky = _ssArmedY;
            if (alen > 0.001f)
            {
                kx -= ax / alen * WindCenterPullback;
                ky -= ay / alen * WindCenterPullback;
            }
            Enemies.RadialKnockback(kx, ky, radius, WindKnockDecay, WindKnockMaxForce, WindKnockMargin,
                                    WindKnockScale);
        }

        /// <summary>Is any live enemy within <paramref name="range"/> of (x, y)? Distinguishes a pellet that LANDED
        /// on something from one that expired at the end of its flight or clipped a wall.</summary>
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

        // ── Mobius Ring (Ruby's weapon: damage ramps the longer the attack is charged) ──
        // Xiao adaptation: Ruby ramps while CHARGING her ball; Xiao ramps while HOLDING the drawn shot
        // (states 0xB/0xC). Every MobiusCycleSeconds held, the damage multiplier compounds ×MobiusStepMult
        // (mirroring Ruby's damage += damage/2 per flash cycle) and Xiao flashes — the same repeated-flash
        // feedback Ruby gets. The fired pellet takes damage ×1.5^cycles (capped at Ruby's 65535) and its
        // sprite grows with the multiplier using Ruby's own ball-growth formula/constants.
        private const double MobiusCycleSeconds = 1.5;    // hold time per ramp step (Ruby's flash cadence)
        private const float  MobiusStepMult     = 1.5f;   // damage multiplier per completed cycle, compounding
        private const int    MobiusDamageCap    = ushort.MaxValue;   // Ruby's ramp cap
        private const float  MobiusPelletMaxScale = 15f;  // sprite-size cap — Ruby's ball caps at 5× but a pellet is tiny, so Xiao gets 15×
        private static bool     _mrHolding;
        private static DateTime _mrHoldStart;
        private static int      _mrCycles;   // completed ramp cycles (frozen on release for the pellet that fires)
        private static readonly bool[] _mrHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];

        /// <summary>Mobius Ring: while <paramref name="active"/>, holding the shot ramps a compounding damage
        /// multiplier (one ×<see cref="MobiusStepMult"/> step + a Ruby-style flash per
        /// <see cref="MobiusCycleSeconds"/> held); each fired pellet gets the ramped damage and a ball-growth
        /// sprite scale. The ramp freezes on release (so the pellet that fires reads it) and resets when a
        /// fresh hold starts, or when the sphere is swapped.</summary>
        internal static void DriveMobiusRing(bool active)
        {
            if (!active)
            {
                _mrHolding = false; _mrCycles = 0;
                Array.Clear(_mrHandled, 0, _mrHandled.Length);
                return;
            }

            int shotState = Memory.ReadInt(WeaponCollision.ChargeActionState);
            bool holding = shotState == WeaponCollision.XiaoShotDraw || shotState == WeaponCollision.XiaoShotHold;
            if (holding)
            {
                if (!_mrHolding) { _mrHoldStart = DateTime.UtcNow; _mrHolding = true; _mrCycles = 0; }
                int cycles = (int)((DateTime.UtcNow - _mrHoldStart).TotalSeconds / MobiusCycleSeconds);
                if (cycles > _mrCycles)
                {
                    _mrCycles = cycles;
                    Player.FlashChargeComplete();   // Ruby's Mobius flash per ramp step
                }
            }
            else _mrHolding = false;   // keep _mrCycles frozen for the pellet that fires

            // Stamp fresh pellets once each: damage ×1.5^cycles (capped) + Ruby's ball-growth sprite scale.
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) return;   // pool not allocated / bad pointer
            float mult = (float)Math.Pow(MobiusStepMult, _mrCycles);
            for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if (live && !_mrHandled[i])
                {
                    if (_mrCycles > 0)
                    {
                        long dmgA = WeaponCollision.PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)Math.Min(MobiusDamageCap, Memory.ReadInt(dmgA) * (double)mult));
                        float scale = 1f + (mult - 1f) * WeaponCollision.RubyBallGrowthPerMultiple;
                        if (scale > MobiusPelletMaxScale) scale = MobiusPelletMaxScale;
                        Memory.WriteFloat(WeaponCollision.PlayerShotPool.ScaleAddr(poolBase, i), scale);
                    }
                    _mrHandled[i] = true;
                }
                else if (!live) _mrHandled[i] = false;
            }
        }
    }
}
