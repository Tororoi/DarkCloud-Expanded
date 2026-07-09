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
    /// <c>DriveHeavensCloud</c> = the charge → grow + shrapnel burst, <c>DriveAgasSword</c> = Defensive Legacy,
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

        // ── Moonlit Focus + Heaven's Cloud (charge → grow + shrapnel burst) ──
        private const float HcPelletScale    = 8f;          // Heaven's Cloud: pellet at MAX charge (shot-pool +0x310)
        private const float HcSlingshotScale = 2f;          // Heaven's Cloud: slingshot 'c04w' mesh at MAX charge
        private const double ChargeGrowSeconds = 1;         // hold time to reach max charge (base→full)
        private const float HcMaxDamageMult  = 1.5f;        // Heaven's Cloud: pellet damage at full charge (partial-charge payoff)
        private static bool _ssFlashedThisCharge;           // edge latch: one flash per charge-to-max
        private static float _ssSlingScale = 1f;            // current slingshot mesh scale
        private static bool  _ssHolding;                    // currently drawing/holding a shot
        private static DateTime _ssHoldStart;               // when the current hold began
        private static float _ssHoldFrac;                   // 0..1 charge fraction (frozen on release for the pellet)

        // Heaven's Cloud impact = a shrapnel BURST: on a pellet hit, spawn 8 real CSHOT pellets radiating out
        // from the hit enemy along the ground plane. They're actual pellets (collision ENABLED, +0x280=0) so
        // they deal real weapon damage + hit reactions to whatever they fly into — the game's own hitbox does
        // the "splash", no faked HP writes. They spawn just outside the hit enemy's body, then fly out + expire.
        private const int   BurstCount          = 8;      // pellets per burst (8 compass directions)
        private const float BurstSpeed          = 2.5f;   // outward units/frame (matches a normal pellet ~5.0/3.5)
        // MUST spawn outside the origin enemy's hitbox (2.0 + its part_radius, checkCollision 0x1AB740), or the
        // shrapnel collides with it on frame 1 and dies before moving. Big enemies have radius ~4-8; 16 also
        // stops the occasional stray pellet re-clipping the hit enemy for a double-hit.
        private const float BurstStartOffset    = 16f;    // spawn this far from the enemy center
        private const int   BurstLifetime       = 0x40;   // frames the shrapnel lives → reach ≈ Speed × this
        private const float BurstDamageFraction = 0.5f;   // each shrapnel's +0x2E0 attack = this × the weapon's attack stat
        private const float BurstScale          = 1f;     // shrapnel sprite scale (+0x310) — normal pellet size (only the charged shot grows)
        // The burst only fires from a MAX-charge shot. A max-charge pellet spawning arms this timestamp; the
        // next Xiao hit within the window bursts and consumes it (covers the pellet's flight time).
        private const float BurstMaxChargeThreshold = 0.95f;                       // charge fraction that counts as "max"
        private static readonly TimeSpan BurstChargeWindow = TimeSpan.FromSeconds(2.5);
        private static DateTime _ssMaxChargeFiredAt = DateTime.MinValue;
        // Movement AND collision share one block in step__5CSHOT (pos+=vel only runs when +0x280==0), so pass-
        // through pellets can't move. Real shrapnel = collision on (false). true = frozen visibility debug only.
        internal static bool _hcBurstPassThrough = false;
        private static readonly int[] _ssSplashPrevHp = new int[EnemyAddresses.FloorSlots.Count];
        private static byte _ssSplashFloor = 0xFF;
        // Per-slot "already applied" latches — one per driver, so a pellet gets each effect exactly once.
        private static readonly bool[] _moonlitHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];
        private static readonly bool[] _hcHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];
        // Slots we spawned as shrapnel, still in flight. While any are alive, hit-detection is suppressed so
        // the shrapnel's own kills don't spawn more shrapnel (cascade). Plus a short settle cooldown after.
        private static readonly List<int> _hcBurstSlots = new List<int>();
        private static DateTime _hcBurstCooldownUntil;

        /// <summary>Moonlit Focus (Tsukikage / Heaven's Cloud): doubles each newly-fired pellet's +0x1C0
        /// velocity (2× shot speed), once per pellet. Per-slot latched; the latch clears when the slot frees.</summary>
        internal static void DriveTsukikage(bool active)
        {
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (poolBase == 0 || poolBase >= 0x02000000) return;   // pool not allocated / bad pointer
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

        /// <summary>Heaven's Cloud (Heaven's Cloud sphere) — a CHARGE effect. Tracks the hold → 0..1 charge
        /// fraction (frozen on release so the fired pellet reads it), grows the slingshot mesh and the fired
        /// pellet (+0x310 scale) and scales its damage up to <see cref="HcMaxDamageMult"/>, flashes Xiao once
        /// at full charge, and bursts shrapnel on hit. When inactive it resets (slingshot back to 1×, latches
        /// cleared) while still keeping the burst HP snapshot fresh.</summary>
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

            // Grow the fired pellet + scale its damage up to HcMaxDamageMult with the held charge, once each.
            // A max-charge shot arms the shrapnel burst for its next hit.
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (poolBase != 0 && poolBase < 0x02000000)
            {
                float pelletScale = active ? 1f + _ssHoldFrac * (HcPelletScale - 1f) : 1f;
                bool bigPellet = pelletScale > 1.01f;
                for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
                {
                    bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                    if (bigPellet && live && !_hcHandled[i])
                    {
                        float chargeFrac = (pelletScale - 1f) / (HcPelletScale - 1f);   // 0..1
                        Memory.WriteFloat(WeaponCollision.PlayerShotPool.ScaleAddr(poolBase, i), pelletScale);
                        long dmgA = WeaponCollision.PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)(Memory.ReadInt(dmgA) * (1f + chargeFrac * (HcMaxDamageMult - 1f))));
                        if (chargeFrac >= BurstMaxChargeThreshold) _ssMaxChargeFiredAt = DateTime.UtcNow;
                        _hcHandled[i] = true;
                    }
                    else if (!live) _hcHandled[i] = false;
                }
            }

            // Slingshot 'c04w' mesh: grow with the hold, HOLD through the shoot (0xD), snap back after. Multiply-
            // scale preserves the frame's baked rotation; _ssHoldFrac stays frozen for 0xB/0xC/0xD.
            _ssSlingScale = (active && (holding || shooting)) ? 1f + _ssHoldFrac * (HcSlingshotScale - 1f) : 1f;
            Weapons.ScaleWeaponFrameByName(WeaponCollision.XiaoSlingMeshNameWord, _ssSlingScale);

            // Whole-character flash ONCE on reaching full charge (edge-latched; rearms on release) — burst armed.
            if (active && holding && _ssHoldFrac >= BurstMaxChargeThreshold && !_ssFlashedThisCharge)
            { Player.FlashActiveCharacter(0f, 122f, 208f, 15f, 1); _ssFlashedThisCharge = true; }
            else if (!holding) _ssFlashedThisCharge = false;

            // Each pellet hit bursts into shrapnel radiating from the enemy (real collision damage).
            HeavensCloudBurstDrive(active);
        }

        /// <summary>Heaven's Cloud impact = a shrapnel BURST. When a Xiao pellet hits an enemy (its HP drops
        /// with the damage source == Xiao), spawn <see cref="BurstCount"/> real CSHOT pellets radiating out
        /// along the ground plane from the hit enemy. They collide natively (fixed 2.0 hitbox) and deal real
        /// weapon damage to whatever they fly into — no faked HP writes. Hit-detection is suppressed while any
        /// shrapnel is still airborne (and briefly after) so the shrapnel's own kills don't spawn more shrapnel.</summary>
        private static void HeavensCloudBurstDrive(bool active)
        {
            int n = EnemyAddresses.FloorSlots.Count;
            int[] cur = new int[n];
            for (int s = 0; s < n; s++) cur[s] = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));

            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _ssSplashFloor)   // new floor: reseat the baseline, forget any in-flight shrapnel
            {
                _ssSplashFloor = floor;
                for (int s = 0; s < n; s++) _ssSplashPrevHp[s] = cur[s];
                _hcBurstSlots.Clear();
                return;
            }

            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            bool poolOk = poolBase != 0 && poolBase < 0x02000000;

            // Prune shrapnel that has hit/expired; while any is still airborne, don't react to HP drops (its
            // own collision damage would otherwise read as fresh hits → runaway cascade).
            if (poolOk)
                _hcBurstSlots.RemoveAll(s => Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, s)) == 0);
            // Only a MAX-charge shot's hit bursts (armed at fire, valid for the pellet's flight window).
            bool maxChargeArmed = DateTime.UtcNow - _ssMaxChargeFiredAt < BurstChargeWindow;
            bool clearToBurst = poolOk && maxChargeArmed && _hcBurstSlots.Count == 0 &&
                                DateTime.UtcNow >= _hcBurstCooldownUntil;

            if (active && clearToBurst && ReusableFunctions.GetDamageSourceCharacterID() == Player.XiaoId)
            {
                // Shrapnel +0x2E0 is an ATTACK value (defense is applied when it lands), so base it on the
                // weapon's raw attack stat — NOT the direct hit's HP drop (that's already post-defense, so
                // feeding it back in roughly reconstitutes full attack instead of a fraction).
                int dmg = (int)(Player.Weapon.GetCurrentWeaponAttack() * BurstDamageFraction);
                for (int h = 0; h < n && dmg > 0; h++)
                {
                    if (_ssSplashPrevHp[h] <= 0 || cur[h] >= _ssSplashPrevHp[h]) continue;   // detect a fresh Xiao hit
                    SpawnHcBurst(poolBase, h, dmg);
                    _ssMaxChargeFiredAt = DateTime.MinValue;   // consume — one burst per max-charge shot
                    break;
                }
            }

            for (int s = 0; s < n; s++) _ssSplashPrevHp[s] = cur[s];
        }

        /// <summary>Spawn <see cref="BurstCount"/> real pellets fanning out from enemy <paramref name="h"/> in
        /// the ground plane (pos layout: [0]=X @0x100, [1]=height @0x104, [2]=Y @0x108). Each is a genuine
        /// CSHOT pellet — collision ENABLED (+0x280=0) with damage <paramref name="dmg"/> — spawned
        /// <see cref="BurstStartOffset"/> out so it clears the hit enemy's body before flying on.</summary>
        private static void SpawnHcBurst(long poolBase, int h, int dmg)
        {
            long ePos = EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.LocationX);   // X, height, Y (contiguous)
            float ex = Memory.ReadFloat(ePos), eh = Memory.ReadFloat(ePos + 4), ey = Memory.ReadFloat(ePos + 8);

            for (int i = 0; i < BurstCount; i++)
            {
                int slot = -1;
                for (int j = 0; j < WeaponCollision.PlayerShotPool.SlotCount; j++)
                    if (Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, j)) == 0) { slot = j; break; }
                if (slot < 0) break;   // pool full — fire as many as we can

                double ang = i * (2.0 * Math.PI / BurstCount);
                float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang);   // ground-plane unit direction

                long pos = WeaponCollision.PlayerShotPool.PosAddr(poolBase, slot);
                Memory.WriteFloat(pos, ex + dx * BurstStartOffset);
                Memory.WriteFloat(pos + 4, eh);                               // same height
                Memory.WriteFloat(pos + 8, ey + dy * BurstStartOffset);
                long vel = WeaponCollision.PlayerShotPool.VelAddr(poolBase, slot);
                Memory.WriteFloat(vel, dx * BurstSpeed);
                Memory.WriteFloat(vel + 4, 0f);                               // no vertical drift
                Memory.WriteFloat(vel + 8, dy * BurstSpeed);
                Memory.WriteInt(WeaponCollision.PlayerShotPool.NoCollideAddr(poolBase, slot), _hcBurstPassThrough ? 1 : 0);   // collision ON (0) unless debugging visibility
                Memory.WriteInt(poolBase + WeaponCollision.PlayerShotPool.LifetimeOffset +
                                slot * WeaponCollision.PlayerShotPool.ScalarStride, BurstLifetime);
                Memory.WriteInt(WeaponCollision.PlayerShotPool.DamageAddr(poolBase, slot), dmg);
                Memory.WriteFloat(WeaponCollision.PlayerShotPool.ScaleAddr(poolBase, slot), BurstScale);
                Memory.WriteInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, slot), 1);
                _hcBurstSlots.Add(slot);
            }
            _hcBurstCooldownUntil = DateTime.UtcNow.AddMilliseconds(150);   // settle window after the last shrapnel clears
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
                    Player.FlashActiveCharacter(0f, 122f, 208f, 15f, 1);   // Ruby's Mobius flash per ramp step
                }
            }
            else _mrHolding = false;   // keep _mrCycles frozen for the pellet that fires

            // Stamp fresh pellets once each: damage ×1.5^cycles (capped) + Ruby's ball-growth sprite scale.
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (poolBase == 0 || poolBase >= 0x02000000) return;   // pool not allocated / bad pointer
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
