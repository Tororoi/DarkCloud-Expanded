using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class CustomXiaoEffects
    {

        // ── Angel Gear ─────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Angel Gear thread: runs while the weapon is equipped and hands every tick to
        /// <see cref="DriveAngelGear"/>, which owns the cadence — so a pause, a menu, a chest or a conversation only
        /// holds the interval, never restarts it.</summary>
        public static void AngelGearEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.angelgear)
            {
                DriveAngelGear(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
        }

        // ── Matador ────────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Matador thread: hands every tick to <see cref="ChargingBull.Drive"/> while the weapon is equipped,
        /// and stands it down once when it goes.</summary>
        public static void MatadorEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.matador)
            {
                ChargingBull.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            ChargingBull.Stop();
        }

        // ── Dragon's Y ─────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Dragon's Y thread: hands every tick to <see cref="DragonsY.Drive"/> (the charged shot) while the
        /// weapon is equipped, and stands it down once when it goes. Its movement buff has its own thread,
        /// <see cref="LockOnSpeedEffect"/>, shared with the weapons that inherit it.</summary>
        public static void DragonsYEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.dragonsy)
            {
                DragonsY.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            DragonsY.Stop();
        }

        // ── Bandit Slingshot / Bandit's Ring ──────────────────────────────────────────────
        /// <summary>The stolen-projectile thread: hands every tick to <see cref="BanditSlingshot.Drive"/> while Xiao's Bandit
        /// Slingshot or Ruby's Bandit's Ring is equipped (<see cref="BanditSlingshot.Carries"/>), and stands it down once when
        /// it goes. Super Steve drives the same from <see cref="SuperSteveEffect"/> when its sphere is either one's.</summary>
        public static void BanditSlingshotEffect()
        {
            while (Player.InDungeonFloor() && BanditSlingshot.Carries(Player.Weapon.GetCurrentWeaponId()))
            {
                BanditSlingshot.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            BanditSlingshot.Stop();
        }

        // ── Hardshooter ────────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Hardshooter thread: hands every tick to <see cref="Hardshooter.Drive"/> while the weapon is equipped,
        /// and stands it down once when it goes.</summary>
        public static void HardshooterEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.hardshooter)
            {
                Hardshooter.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            Hardshooter.Stop();
        }

        // ── Steel Slingshot ────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Steel Slingshot thread: hands every tick to <see cref="SteelSlingshot.Drive"/> while the weapon is
        /// equipped, and stands it down once when it goes.</summary>
        public static void SteelSlingshotEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.steelslingshot)
            {
                SteelSlingshot.Drive(!Player.CheckDunIsPaused());
                Thread.Sleep(16);
            }
            SteelSlingshot.Stop();
        }

        // ── Double Impact ──────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Double Impact thread: hands every tick to <see cref="DoubleImpact.Drive"/> while the weapon is
        /// equipped, and stands it down once when it goes.</summary>
        public static void DoubleImpactEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.doubleimpact)
            {
                DoubleImpact.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            DoubleImpact.Stop();
        }

        // ── Lock-on reach (Flamingo, Dragon's Y, Divine Beast Title, Angel Shooter, Angel Gear) ──
        /// <summary>The lock-on reach's thread: hands every tick to <see cref="Flamingo.Drive"/> while one of the weapons that
        /// carry it is equipped (<see cref="Flamingo.GrantsReach"/>), and releases it once when it goes. Super Steve drives the
        /// same reach from <see cref="SuperSteveEffect"/> when its sphere is one of theirs.</summary>
        public static void LockOnReachEffect()
        {
            while (Player.InDungeonFloor() && Flamingo.GrantsReach(Player.Weapon.GetCurrentWeaponId()))
            {
                Flamingo.Drive(!Player.CheckDunIsPaused());
                Thread.Sleep(16);
            }
            Flamingo.Stop();
        }

        // ── Lock-on speed (Dragon's Y, Divine Beast Title, Angel Shooter, Angel Gear) ─────────
        /// <summary>The lock-on movement buff's thread: hands every tick to <see cref="LockOnSpeed.Drive"/> while one of the
        /// weapons that carry it is equipped (<see cref="LockOnSpeed.Grants"/>), and releases it once when it goes. Super Steve
        /// drives the same buff from <see cref="SuperSteveEffect"/> when its sphere is one of theirs.</summary>
        public static void LockOnSpeedEffect()
        {
            while (Player.InDungeonFloor() && LockOnSpeed.Grants(Player.Weapon.GetCurrentWeaponId()))
            {
                LockOnSpeed.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            LockOnSpeed.Stop();
        }

        private const ushort AngelGearHealAmount = 1;
        private static int _healTickPrev = -1;   // the counter last seen; -1 = not watching, re-seed on the next tick

        /// <summary>Angel Gear's regen, driven every tick by Xiao's own thread and by Super Steve's when it inherits the
        /// weapon. It rides the native HEAL ability's own cadence: each wrap of <see cref="HealAbility.TickCounter"/> —
        /// the frame the game grants its +1 — heals each ally by <see cref="AngelGearHealAmount"/> (skipping the dead and
        /// the already-full). Xiao is healed too UNLESS the equipped weapon carries the native Heal build-up attribute
        /// (Special2 % 16 in 8..11), which already regenerates her. Opening mid-cycle never procs retroactively. While Xiao
        /// guards, <see cref="GuardianGrace"/> floors the counter so the native tick fires every second, and the party heal
        /// follows — the counter only ever climbs, is set upward by that floor, or resets to 0 on a proc, so any decrease
        /// is a proc.</summary>
        internal static void DriveAngelGear(bool active)
        {
            if (!active || Player.CheckDunIsPausedOrMenu() || !Player.CheckDunIsWalkingMode()) { _healTickPrev = -1; return; }
            int c = Memory.ReadInt(HealAbility.TickCounter);
            bool wrapped = _healTickPrev >= 0 && c < _healTickPrev;   // the native +1 just fired
            _healTickPrev = c;
            if (!wrapped) return;

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

        // ── Super Steve "Sphere Inheritance" ───────────────────────────────────────────────
        /// <summary>
        /// Super Steve (Xiao's ultimate slingshot) inherits the custom effect of the weapon whose SynthSphere
        /// is attached to it. A weapon Status-Broken into a SynthSphere records its SOURCE weapon id at the
        /// attach-entry's +0x02 (SetStatusBreak, ELF 0x2368D0), and attaching copies the whole 0x20-byte entry
        /// into the weapon record's ATTACH_LIST, so the source id survives in-record and can be read straight
        /// off the record — no stat-fingerprinting needed.
        ///
        /// This is the master dispatch loop: read the single attached sphere, then pulse each ability's driver
        /// with <c>active &amp;&amp; sphere == Items.X</c>. Enemy-side abilities reuse the CustomToanEffects
        /// drivers verbatim (they only touch enemy data); the Xiao body adaptations live in
        /// <see cref="SuperSteveAbilities"/>. Every driver is pulsed each tick (enabled or not) so it
        /// self-restores the instant the sphere is swapped — no explicit per-swap teardown needed.
        ///
        /// NOT dispatched here: MIRAGE (Mirage / Hercules' Wrath spheres). It owns a thread and a state machine
        /// (guard charge → decoy → clone → shimmer), so it gates itself in <see cref="Mirage"/> rather than being
        /// pulsed per-tick like the stateless abilities below. Nothing to add here when its sphere is attached.
        ///
        /// NOT every weapon's ability transfers. Excluded by design:
        ///   • Macho Sword, Wise Owl Sword, Chronicle 2 — rely on weapon ownership
        ///   • Buster Sword, 7 Branch Sword - modify upgrading / status-breaks
        /// </summary>
        public static void SuperSteveEffect()
        {
            var ssSun = new CustomToanEffects.SunHarvestState(EnemyAddresses.FloorSlots.Count);
            var xiaoCurse = new CustomToanEffects.CurseAddrs(Player.Xiao.status, Player.Xiao.statusTimer, Player.Xiao.hp);
            var ssEvilcise = new CustomToanEffects.CurseState();
            var ssManeater = new CustomToanEffects.CurseState();
            var xiaoTuna = new CustomGoroEffects.FrozenTunaWielder(Player.XiaoId, Player.Xiao.hp, Player.Xiao.maxHP,
                                                                   Player.Xiao.status, Player.Xiao.statusTimer);
            var ssTuna = new CustomGoroEffects.FrozenTunaState();
            var ssTallHammer = new CustomGoroEffects.TallHammerState();
            var ssCactus = new CustomUngagaEffects.CactusState();
            var ssSnail = new CustomOsmondEffects.SnailState();
            var ssStarBreaker = new CustomOsmondEffects.StarBreakerState();
            int lastSphere = 0;   // the sphere last seen: Charging Bull keeps a resident copy that must go when its sphere does
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(DngStatusData.Base +
                                                DngStatusData.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = DngStatusData.WeaponRecord(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                int sphere = SuperSteveAbilities.AttachedSphere(rec);
                bool active = !Player.CheckDunIsPaused();

                // Toan Effects
                // Divine Guard (7th Heaven) + Guard Crush (Dark Cloud; 7th Heaven inherits Guard Crush by lineage).
                CustomToanEffects.SeventhHeavenSoftenAttacks(active && sphere == Items.seventhheaven);
                CustomToanEffects.DarkCloudDriveGuards(active && (sphere == Items.seventhheaven || sphere == Items.darkcloud));

                // Defensive Legacy (Aga's Sword): +15 Xiao defense.
                SuperSteveAbilities.DriveAgasSword(active && sphere == Items.agassword);

                // The attached sphere's weapon icon on Xiao's character-menu panel (in place of the old palette swap).
                SuperSteveAbilities.DriveSphereIcon(sphere);

                // …and its pellet, when the sphere came from a slingshot.
                SuperSteveAbilities.DriveSphereSprite(sphere);

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo each tick.
                SuperSteveAbilities.DriveBraveArk(active && sphere == Items.braveark);

                // Bone Rapier: bone-door bypass (the Xiao dispatcher no longer force-clears it, so this owns it).
                CustomToanEffects.BoneRapierEffect(active && (sphere == Items.bonerapier || sphere == Items.boneslingshot));

                // Solar Harvest (Sun Sword / Big Bang): ~1% of the floor's enemies drop a Sun attachment.
                CustomToanEffects.SunHarvestDrive(sphere == Items.sunsword || sphere == Items.bigbang, ssSun);

                // Curses (full inherit): curse Xiao. Not pause-gated — mirrors the Toan loops.
                CustomToanEffects.EvilciseDrive(sphere == Items.evilcise, xiaoCurse, ssEvilcise);
                CustomToanEffects.ManeaterDrive(sphere == Items.maneater, xiaoCurse, rec, ssManeater);

                // Quick Draw (Small Sword / Tsukikage / Heaven's Cloud): instant fire-on-release + rate-of-fire.
                SuperSteveAbilities.DriveSmallSword(active && (sphere == Items.smallsword || sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Moonlit Focus (Tsukikage / Heaven's Cloud): ×2 shot speed.
                SuperSteveAbilities.DriveTsukikage(active && (sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Heaven's Cloud (Heaven's Cloud): charge → grow the slingshot + pellet, flash, shrapnel burst.
                SuperSteveAbilities.DriveHeavensCloud(active && sphere == Items.heavenscloud);

                // A charged shot's weapon HP (Heaven's Cloud / Mobius Ring / the cat arm it): the word returns to 1.0 once fired.
                ChargedShotWhp.Tick();

                // Xiao Effects

                // Angel Gear: slow party-wide HP regen.
                DriveAngelGear(active && sphere == Items.angelgear);

                // Lock-on speed (Dragon's Y / Divine Beast Title / Angel Shooter / Angel Gear): ×1.3 movement while locked on.
                LockOnSpeed.Drive(active && LockOnSpeed.Grants(sphere));

                // Lock-on reach (Flamingo / Dragon's Y / Divine Beast Title / Angel Shooter / Angel Gear): enemies locked from twice as far.
                Flamingo.Drive(active && Flamingo.GrantsReach(sphere));

                // Dragon's Y: the charged shot — the Gemron ball of Super Steve's own selected element.
                DragonsY.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.dragonsy);

                // Bandit Slingshot / Bandit's Ring: a steal takes the enemy's projectile; every pellet is that shot at 2× the attack.
                BanditSlingshot.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && (sphere == Items.banditslingshot || sphere == Items.banditsring));

                // Double Impact: every shot is two pellets, each at 0.75× the attack (their ricochets driven with them).
                DoubleImpact.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.doubleimpact);

                // Hardshooter: a pellet that lands on an enemy ricochets at the next one.
                if (lastSphere == Items.hardshooter && sphere != Items.hardshooter) Hardshooter.Stop();
                Hardshooter.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.hardshooter);

                // Steel Slingshot: half the WHP per shot while the weapon's WHP is low (the level-up bonus stays the Steel's own).
                if (lastSphere == Items.steelslingshot && sphere != Items.steelslingshot) SteelSlingshot.Stop();
                SteelSlingshot.Drive(active && sphere == Items.steelslingshot);

                // Matador (Charging Bull): the charged pellet crushes guards and flies as a projection of the slingshot — Super
                // Steve's own model, since the copy is of the live weapon. A held copy goes with the sphere.
                if (lastSphere == Items.matador && sphere != Items.matador) ChargingBull.Stop();
                ChargingBull.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.matador);
                lastSphere = sphere;

                // Goro Effects

                // Cold Storage (Frozen Tuna): WHP losses bank a healing pool that drains after Xiao is hit;
                // on-hit 5% chance to stop all non-ice enemies at the price of freezing Xiao too.
                CustomGoroEffects.FrozenTunaDrive(active && sphere == Items.frozentuna, xiaoTuna, equipSlot, ssTuna);

                // Tall Hammer: shrinks enemies Xiao's pellets hit.
                CustomGoroEffects.TallHammerDrive(active && sphere == Items.tallhammer, Player.XiaoId, ssTallHammer);

                // Ruby Effects

                // Mobius Ring: holding the shot ramps damage ×1.5 per 1.5s (flash per step); the fired
                // pellet gets the ramped damage + a Ruby-ball-style size to match.
                SuperSteveAbilities.DriveMobiusRing(active && sphere == Items.mobiusring);

                // Ungaga Effects

                // Absorb (Cactus): pellet hits restore Xiao's thirst scaled by damage (rock/metal/undead immune).
                CustomUngagaEffects.CactusDrive(active && sphere == Items.cactus, Player.XiaoId,
                                                Player.Xiao.thirst, Player.Xiao.thirstMax, ssCactus);

                // Osmond Effects

                // Snail: 5% chance on hit to inflict gooey on the struck enemy.
                CustomOsmondEffects.SnailDrive(active && sphere == Items.snail, Player.XiaoId, ssSnail);

                // Star Breaker: 2% chance on an enemy kill to receive an empty SynthSphere.
                CustomOsmondEffects.StarBreakerDrive(active && sphere == Items.starbreaker, ssStarBreaker);

                Thread.Sleep(16);
            }

            // Restore everything on unequip / character-switch / dungeon exit (no-ops if not driven).
            CustomToanEffects.SeventhHeavenSoftenAttacks(false);
            CustomToanEffects.DarkCloudDriveGuards(false);
            CustomToanEffects.BoneRapierEffect(false);
            CustomToanEffects.SunHarvestDrive(false, ssSun);
            CustomToanEffects.EvilciseDrive(false, xiaoCurse, ssEvilcise);
            CustomToanEffects.ManeaterDrive(false, xiaoCurse, 0, ssManeater);
            SuperSteveAbilities.DriveSmallSword(false);
            SuperSteveAbilities.DriveTsukikage(false);
            SuperSteveAbilities.DriveHeavensCloud(false);   // resets slingshot + flash latch
            SuperSteveAbilities.DriveAgasSword(false);
            SuperSteveAbilities.DriveSphereIcon(0);
            SuperSteveAbilities.DriveSphereSprite(0);
            SuperSteveAbilities.DriveMobiusRing(false);   // resets the damage ramp
            CustomGoroEffects.FrozenTunaDrive(false, xiaoTuna, 0, ssTuna);   // resets the healing pool
            LockOnSpeed.Stop();
            Flamingo.Stop();
            DragonsY.Stop();
            ChargingBull.Stop();   // the resident slingshot copy too
            DoubleImpact.Stop();
            BanditSlingshot.Stop();
            SteelSlingshot.Stop();
            Hardshooter.Stop();
        }

        /// <summary>Pack three contiguous floats for a single batched write. Position and velocity are
        /// each three adjacent floats, so one 12-byte write replaces three round-trips — which matters
        /// here because the halo is pinned to an absolute point every tick, and every round-trip between
        /// sampling the player and writing the pellets is lag the ring shows as jitter.</summary>
        private static byte[] Vec3(float a, float b, float c)
        {
            var v = new byte[12];
            BitConverter.GetBytes(a).CopyTo(v, 0);
            BitConverter.GetBytes(b).CopyTo(v, 4);
            BitConverter.GetBytes(c).CopyTo(v, 8);
            return v;
        }
    }
}
