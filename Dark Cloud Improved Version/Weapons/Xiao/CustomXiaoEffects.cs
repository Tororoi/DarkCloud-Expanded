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

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo each tick.
                SuperSteveAbilities.DriveBraveArk(active && sphere == Items.braveark);

                // Bone Rapier: bone-door bypass (the Xiao dispatcher no longer force-clears it, so this owns it).
                CustomToanEffects.BoneRapierEffect(active && sphere == Items.bonerapier);

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

                // Xiao Effects

                // Angel Gear: slow party-wide HP regen.
                DriveAngelGear(active && sphere == Items.angelgear);

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
            SuperSteveAbilities.DriveMobiusRing(false);   // resets the damage ramp
            CustomGoroEffects.FrozenTunaDrive(false, xiaoTuna, 0, ssTuna);   // resets the healing pool
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
