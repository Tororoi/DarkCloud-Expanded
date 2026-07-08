using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomXiaoEffects
    {

        // ── Angel Gear ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Angel Gear effect: Applies the Heal regeneration effect to all allies
        /// </summary>
        public static void AngelGearEffect()
        {
            //Initialize variables
            ushort HpValueAdd = 1;
            ushort Delay = 5000;
            ushort XiaoHp = 0;
            ushort XiaoMaxHp = 0;
            bool isHealXiao = false;

            //Run while Angel Gear is equipped and Player is in valid state
            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                //Fetch HP values for characters
                ushort ToanHp = Player.Toan.GetHp();
                ushort ToanMaxHp = Player.Toan.GetMaxHp();
                ushort GoroHp = Player.Goro.GetHp();
                ushort GoroMaxHp = Player.Goro.GetMaxHp();
                ushort RubyHp = Player.Ruby.GetHp();
                ushort RubyMaxHp = Player.Ruby.GetMaxHp();
                ushort UngagaHp = Player.Ungaga.GetHp();
                ushort UngagaMaxHp = Player.Ungaga.GetMaxHp();
                ushort OsmondHp = Player.Osmond.GetHp();
                ushort OsmondMaxHp = Player.Osmond.GetMaxHp();

                //Check for the Heal special attribute on the weapon
                if (Player.Weapon.GetCurrentWeaponSpecial2() % 16 < 8 ||
                    Player.Weapon.GetCurrentWeaponSpecial2() % 16 > 11)
                {
                    isHealXiao = true;
                    XiaoHp = Player.Xiao.GetHp();
                    XiaoMaxHp = Player.Xiao.GetMaxHp();
                }

                //Add the HP value to the characters current HP
                if (ToanHp < ToanMaxHp && ToanHp > 0) Player.Toan.SetHp((ushort)(ToanHp + HpValueAdd));
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Toan HP add: " + (ToanHp + HpValueAdd));
                if (GoroHp < GoroMaxHp && GoroHp > 0) Player.Goro.SetHp((ushort)(GoroHp + HpValueAdd));
                if (RubyHp < RubyMaxHp && RubyHp > 0) Player.Ruby.SetHp((ushort)(RubyHp + HpValueAdd));
                if (UngagaHp < UngagaMaxHp && UngagaHp > 0) Player.Ungaga.SetHp((ushort)(UngagaHp + HpValueAdd));
                if (OsmondHp < OsmondMaxHp && OsmondHp > 0) Player.Osmond.SetHp((ushort)(OsmondHp + HpValueAdd));

                //Only affect Xiao if Angel Gear does not have the Heal attribute already
                if (isHealXiao && XiaoHp < XiaoMaxHp && XiaoHp > 0) Player.Xiao.SetHp((ushort)(XiaoHp + HpValueAdd));

                //Wait in between additions
                Thread.Sleep(Delay);
            }
        }

        // ── Super Steve "Sphere Inheritance" ───────────────────────────────────────────────
        /// <summary>
        /// Super Steve (Xiao's ultimate slingshot) inherits the custom effect of every weapon
        /// whose SynthSphere is attached to it. A weapon Status-Broken into a SynthSphere records
        /// its SOURCE weapon id at the attach-entry's +0x02 (SetStatusBreak, ELF 0x2368D0), and
        /// attaching copies the whole 0x20-byte entry into the weapon record's ATTACH_LIST
        /// (rec+0x28+slot*0x20), so the source id survives in-record and can be read straight off
        /// Super Steve's six slots — no stat-fingerprinting needed.
        ///
        /// Enemy-side abilities reuse the Toan drivers verbatim (they only touch enemy data, so
        /// they work for whoever is active): a 7th Heaven sphere grants Divine Guard AND Guard
        /// Crush (mirroring the 7th Heaven → Dark Cloud lineage), a Dark Cloud sphere grants Guard
        /// Crush. Player-body abilities (Quick Draw, Moonlit Focus, Heaven's Cloud, the curses,
        /// Defensive Legacy, …) need Xiao-specific adaptations and are added incrementally.
        /// </summary>
        private const int AgasDefenseBoost = 15;   // Defensive Legacy — mirrors CustomToanEffects.AgasSwordEffect
        private static bool _ssAgasApplied;        // whether the +15 defense is currently on Xiao

        public static void SuperSteveEffect()
        {
            var ssSun = new CustomToanEffects.SunHarvestState(EnemyAddresses.FloorSlots.Count);
            var xiaoCurse = new CustomToanEffects.CurseAddrs(Player.Xiao.status, Player.Xiao.statusTimer, Player.Xiao.hp);
            var ssEvilcise = new CustomToanEffects.CurseState();
            var ssManeater = new CustomToanEffects.CurseState();
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                // A weapon holds at most ONE SynthSphere, so its single source weapon id picks the effect.
                // Every driver is pulsed each tick (enabled or not) so it self-restores the instant the
                // sphere is swapped — no explicit per-swap teardown needed.
                int sphere = SuperSteveAttachedSphere(rec);
                bool active = !Player.CheckDunIsPaused();

                // Divine Guard (7th Heaven) + Guard Crush (Dark Cloud; 7th Heaven inherits Guard Crush by lineage).
                CustomToanEffects.SeventhHeavenSoftenAttacks(active && sphere == Items.seventhheaven);
                CustomToanEffects.DarkCloudDriveGuards(active && (sphere == Items.seventhheaven || sphere == Items.darkcloud));

                // Defensive Legacy (Aga's Sword): +15 Xiao defense.
                DriveAgasDefense(active && sphere == Items.agassword);

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo each tick.
                ClearXiaoResistAilments(active && sphere == Items.braveark);

                // Bone Rapier: bone-door bypass (the Xiao dispatcher no longer force-clears it, so this owns it).
                CustomToanEffects.BoneRapierEffect(active && sphere == Items.bonerapier);

                // Solar Harvest (Sun Sword / Big Bang): ~1% of the floor's enemies drop a Sun attachment.
                CustomToanEffects.SunHarvestDrive(sphere == Items.sunsword || sphere == Items.bigbang, ssSun);

                // Curses (full inherit): curse Xiao. Not pause-gated — mirrors the Toan loops.
                CustomToanEffects.EvilciseDrive(sphere == Items.evilcise, xiaoCurse, ssEvilcise);
                CustomToanEffects.ManeaterDrive(sphere == Items.maneater, xiaoCurse, rec, ssManeater);

                // Body abilities: Moonlit Focus (×2 shot speed) + Heaven's Cloud (charge → grow + shrapnel burst).
                bool hasMoonlit = sphere == Items.tsukikage || sphere == Items.heavenscloud;
                DriveHeavensCloudCharge(active, hasMoonlit, sphere == Items.heavenscloud);

                Thread.Sleep(16);
            }

            // Restore everything on unequip / character-switch / dungeon exit (no-ops if not driven).
            CustomToanEffects.SeventhHeavenSoftenAttacks(false);
            CustomToanEffects.DarkCloudDriveGuards(false);
            CustomToanEffects.BoneRapierEffect(false);
            CustomToanEffects.SunHarvestDrive(false, ssSun);
            CustomToanEffects.EvilciseDrive(false, xiaoCurse, ssEvilcise);
            CustomToanEffects.ManeaterDrive(false, xiaoCurse, 0, ssManeater);
            DriveHeavensCloudCharge(false, false, false);   // resets slingshot + shot drive + flash latch
            _ssHolding = false; _ssHoldFrac = 0f;
            DriveAgasDefense(false);
        }

        /// <summary>The source weapon id of the single SynthSphere attached to Super Steve's record at
        /// <paramref name="rec"/> (0 if none). A weapon holds at most one SynthSphere (id 0x5A), so the first
        /// one found is the only one — its source id (attach-entry +0x02) selects the inherited effect.</summary>
        private static int SuperSteveAttachedSphere(long rec)
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

        /// <summary>Defensive Legacy (Aga's Sword): balanced ±<see cref="AgasDefenseBoost"/> on Xiao's defense.</summary>
        private static void DriveAgasDefense(bool want)
        {
            if (want && !_ssAgasApplied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() + AgasDefenseBoost); _ssAgasApplied = true; }
            else if (!want && _ssAgasApplied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() - AgasDefenseBoost); _ssAgasApplied = false; }
        }

        /// <summary>Hero's Courage (Brave Ark): while <paramref name="active"/>, clear Freeze/Poison/Curse/Goo
        /// from Xiao's status word. Stateless (nothing to restore), so it just no-ops when inactive.</summary>
        private static void ClearXiaoResistAilments(bool active)
        {
            if (!active) return;
            const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                      ToanState.StatusCurse  | ToanState.StatusGoo;
            ushort status = Memory.ReadUShort(Player.Xiao.status);
            if ((status & resistMask) != 0)
                Memory.WriteUShort(Player.Xiao.status, (ushort)(status & ~resistMask));
        }

        /// <summary>Moonlit Focus + Heaven's Cloud charge effect, driven each tick. Tracks the hold → 0..1
        /// charge fraction (frozen on release so the fired pellet reads it), grows the slingshot mesh + fired
        /// pellet, flashes Xiao once at full charge, and bursts on hit. Called with all-false to restore
        /// (snaps the slingshot back and clears the latches).</summary>
        private static void DriveHeavensCloudCharge(bool active, bool hasMoonlit, bool hasHeavensCloud)
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

            // Moonlit ×2 speed + Heaven's Cloud pellet size (scales with the held fraction).
            float pelletScale = hasHeavensCloud ? 1f + _ssHoldFrac * (HcPelletScale - 1f) : 1f;
            SuperSteveShotSpawnDrive(active && hasMoonlit, hasHeavensCloud ? pelletScale : 1f);

            // Slingshot 'c04w' mesh: grow with the hold, HOLD through the shoot (0xD), snap back after. Multiply-
            // scale preserves the frame's baked rotation; _ssHoldFrac stays frozen for 0xB/0xC/0xD.
            _ssSlingScale = (hasHeavensCloud && (holding || shooting)) ? 1f + _ssHoldFrac * (HcSlingshotScale - 1f) : 1f;
            Weapons.ScaleWeaponFrameByName(WeaponCollision.XiaoSlingMeshNameWord, _ssSlingScale);

            // Whole-character flash ONCE on reaching full charge (edge-latched; rearms on release) — burst armed.
            if (hasHeavensCloud && holding && _ssHoldFrac >= BurstMaxChargeThreshold && !_ssFlashedThisCharge)
            { Player.FlashActiveCharacter(0f, 122f, 208f, 15f, 1); _ssFlashedThisCharge = true; }
            else if (!holding) _ssFlashedThisCharge = false;

            // Each pellet hit bursts into shrapnel radiating from the enemy (real collision damage).
            HeavensCloudBurstDrive(active && hasHeavensCloud);
        }

        // ── Super Steve → Quick Draw (Small Sword / Tsukikage / Heaven's Cloud sphere) ─────────
        private const float XiaoShotFireFrame  = 251.0f;  // inside the (251,252) pellet-release window (shoot motion idx 13)
        private static bool _xqReleaseArmed;              // edge latch so one X-release = one instant shot
        private const ushort XiaoBattleSpeed = 350;   // effective Speed in Super Steve's BATTLE copy (past 99) → keeps the rate-of-fire gauge from being the bottleneck

        /// <summary>
        /// Tight-poll companion to <see cref="SuperSteveEffect"/>: the Quick Draw inheritance for Xiao.
        /// Xiao (controlled) shares Toan's shot plumbing — motion-frame cursor = AnimFrameCursor
        /// (0x21EA2010), shot action-state = ChargeActionState (0x21DC4494). Her shot is three c04b
        /// motions: draw (0xB, frames 240→251), a zero-speed "nocked" HOLD parked on 250 (0xC) that lasts
        /// until the fire input releases, then shoot (0xD) — the pellet leaves at frame 251.
        ///
        /// Vanilla feel is preserved: hold X to nock/aim, the shot fires on RELEASE. Quick Draw makes
        /// that release instant. Two levers, both gated on an attached Small Sword / Tsukikage / Heaven's
        /// Cloud sphere:
        ///   1. Rate-of-fire: the between-shots gauge (0x21DC44C8) fills at effectiveSpeed/30 and gates
        ///      the next shot at 100; we write Super Steve's BATTLE-copy speed (+8) past the game's 99
        ///      cap so it refills near-instantly. Menu/inventory speed is untouched (still reads 99).
        ///   2. Instant release: the moment the engine's release flag (0x21DC4498, the real X-release —
        ///      never forced) is set during the draw/hold, we jump the state straight to shoot (0xD) and
        ///      drop the cursor onto the shoot-motion start (251.0) so the pellet crosses the (251,252)
        ///      fire window exactly once. Edge-latched (<see cref="_xqReleaseArmed"/>) so one release =
        ///      one pellet. (251.5 sat inside the window and double-fired; 251.0 = the clean boundary.)
        /// The draw motion itself plays at a fixed KEY rate that speed never scales — only the gauge does
        /// (motionDrive, dun 0x1DB7450) — which is why the fix is gauge-speed + release-jump, not a
        /// motion-rate override (that steps over the 1-frame-wide fire window and desyncs).
        /// </summary>
        public static void XiaoQuickDrawEffect()
        {
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                // Speed the rate-of-fire GAUGE: it fills at effectiveSpeed/30 (motionDrive, dun 0x1DB7450)
                // and gates the next shot at 100, so combat's effective Speed (post-clamp, at BATTLE-copy
                // +8) is the fill rate. WeaponAllValueSet caps it at 99 when it rebuilds the record; writing
                // past that here (BATTLE copy only, so the menu still reads Super Steve's real 99) removes
                // the between-shots wait. Only touch it when the battle weapon really is Super Steve.
                bool battleIsSteve = Memory.ReadUShort(WeaponCollision.BattleWeaponRecord) == Items.supersteve;
                long battleSpeedAddr = WeaponCollision.BattleWeaponRecord + WeaponCollision.EffSpeedOffset;
                bool boosted = !Player.CheckDunIsPaused() && battleIsSteve && SuperSteveHasQuickDrawSphere(rec);
                if (boosted && Memory.ReadUShort(battleSpeedAddr) != XiaoBattleSpeed)
                    Memory.WriteUShort(battleSpeedAddr, XiaoBattleSpeed);

                // Instant shot on X RELEASE: while drawing (0xB) or holding (0xC), the moment the fire
                // input is released (engine sets XiaoShotReleaseFlag), jump straight to the shoot state
                // (0xD) and drop the cursor into the fire window (251), so the pellet leaves at once no
                // matter how far the draw got. Holding still aims (nothing happens until release); a tap
                // fires almost instantly. Edge-triggered so exactly one pellet fires per release.
                int shotState = Memory.ReadInt(WeaponCollision.ChargeActionState);
                bool inDrawOrHold = shotState == WeaponCollision.XiaoShotDraw ||
                                    shotState == WeaponCollision.XiaoShotHold;
                bool released = inDrawOrHold && Memory.ReadInt(WeaponCollision.XiaoShotReleaseFlag) != 0;
                if (boosted && released && !_xqReleaseArmed)
                {
                    Memory.WriteInt(WeaponCollision.XiaoShotReleaseFlag, 1);
                    Memory.WriteInt(WeaponCollision.ChargeActionState, WeaponCollision.XiaoShotShoot);
                    Memory.WriteFloat(WeaponCollision.AnimFrameCursor, XiaoShotFireFrame);
                }
                _xqReleaseArmed = released;

                // Tight poll: latency here directly delays the shot (mirrors Toan's SmallSwordEffect).
                Thread.Sleep(8);
            }
        }

        /// <summary>True if the attached SynthSphere is a Small Sword / Tsukikage / Heaven's Cloud (the Quick
        /// Draw lineage).</summary>
        private static bool SuperSteveHasQuickDrawSphere(long rec)
        {
            int s = SuperSteveAttachedSphere(rec);
            return s == Items.smallsword || s == Items.tsukikage || s == Items.heavenscloud;
        }

        // ── Super Steve → per-pellet effects (Moonlit Focus + Heaven's Cloud pellet scale) ─────
        private const float HcPelletScale    = 8f;         // Heaven's Cloud: pellet at MAX charge (shot-pool +0x310)
        private const float HcSlingshotScale = 2f;          // Heaven's Cloud: slingshot 'c04w' mesh at MAX charge
        private const double ChargeGrowSeconds = 2;       // hold time to reach max charge (base→full)
        // Heaven's Cloud impact = a shrapnel BURST: on a pellet hit, spawn 8 real CSHOT pellets radiating
        // out from the hit enemy along the ground plane. They're actual pellets (collision ENABLED, +0x280=0)
        // so they deal real weapon damage + hit reactions to whatever they fly into — the game's own hitbox
        // does the "splash", no faked HP writes. They spawn just outside the hit enemy's body so they don't
        // instantly re-hit it, then fly out and expire.
        private const float HcMaxDamageMult  = 1.5f;        // Heaven's Cloud: pellet damage at full charge (partial-charge payoff)
        // Max-charge whole-character FLASH — addresses in CharacterAddresses.CharacterFlash (the engine's
        // setUnitAmbientAnime, applied to the active unit, so it works for Xiao). Written via TriggerCharacterFlash.
        private static bool _ssFlashedThisCharge;            // edge latch: one flash per charge-to-max
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
        private static float _ssSlingScale = 1f;            // current slingshot mesh scale
        private static bool  _ssHolding;                    // currently drawing/holding a shot
        private static DateTime _ssHoldStart;               // when the current hold began
        private static float _ssHoldFrac;                   // 0..1 charge fraction (frozen on release for the pellet)

        private static readonly int[] _ssSplashPrevHp = new int[EnemyAddresses.FloorSlots.Count];
        private static byte _ssSplashFloor = 0xFF;
        private static readonly bool[] _ssShotHandled = new bool[WeaponCollision.PlayerShotPool.SlotCount];

        // Slots we spawned as shrapnel, still in flight. While any are alive, hit-detection is suppressed so
        // the shrapnel's own kills don't spawn more shrapnel (cascade). Plus a short settle cooldown after.
        private static readonly List<int> _hcBurstSlots = new List<int>();
        private static DateTime _hcBurstCooldownUntil;

        /// <summary>Applies the per-pellet Super Steve effects at spawn, once each, by watching the player
        /// shot pool (<see cref="WeaponCollision.PlayerShotPool"/>) for slots that just went active (flag
        /// 0→1): <paramref name="doubleVel"/> = Moonlit Focus (×2 the +0x1C0 velocity → 2× speed);
        /// <paramref name="pelletScale"/> = Heaven's Cloud pellet size (the +0x310 scale; >1 only, so a base
        /// pellet is left alone). Per-slot latched so each is applied exactly once; latch clears on free.</summary>
        private static void SuperSteveShotSpawnDrive(bool doubleVel, float pelletScale)
        {
            bool bigPellet = pelletScale > 1.01f;
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (poolBase == 0 || poolBase >= 0x02000000) return;   // pool not allocated / bad pointer
            for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if ((doubleVel || bigPellet) && live && !_ssShotHandled[i])
                {
                    if (doubleVel)
                    {
                        long vel = WeaponCollision.PlayerShotPool.VelAddr(poolBase, i);
                        for (int c = 0; c < 3; c++)
                            Memory.WriteFloat(vel + c * 4, Memory.ReadFloat(vel + c * 4) * 2f);
                    }
                    if (bigPellet)
                    {
                        float chargeFrac = (pelletScale - 1f) / (HcPelletScale - 1f);   // 0..1
                        Memory.WriteFloat(WeaponCollision.PlayerShotPool.ScaleAddr(poolBase, i), pelletScale);
                        // Damage scales up to HcMaxDamageMult with the charge held — the partial-charge payoff.
                        long dmgA = WeaponCollision.PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)(Memory.ReadInt(dmgA) * (1f + chargeFrac * (HcMaxDamageMult - 1f))));
                        // A max-charge shot arms the shrapnel burst for its next hit.
                        if (chargeFrac >= BurstMaxChargeThreshold) _ssMaxChargeFiredAt = DateTime.UtcNow;
                    }
                    _ssShotHandled[i] = true;
                }
                else if (!live)
                {
                    _ssShotHandled[i] = false;
                }
            }
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

    }
}
