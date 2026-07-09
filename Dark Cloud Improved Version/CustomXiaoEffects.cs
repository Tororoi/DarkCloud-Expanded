using System.Threading;

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
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                int sphere = SuperSteveAbilities.AttachedSphere(rec);
                bool active = !Player.CheckDunIsPaused();
                // (The sphere-palette recolour is NOT driven here — WeaponTextureSwap runs its own always-on
                // thread so the swap also covers town/menu, where this dungeon dispatcher never runs.)

                // Toan Effects
                // Divine Guard (7th Heaven) + Guard Crush (Dark Cloud; 7th Heaven inherits Guard Crush by lineage).
                CustomToanEffects.SeventhHeavenSoftenAttacks(active && sphere == Items.seventhheaven);
                CustomToanEffects.DarkCloudDriveGuards(active && (sphere == Items.seventhheaven || sphere == Items.darkcloud));

                // Defensive Legacy (Aga's Sword): +15 Xiao defense.
                SuperSteveAbilities.DriveAgasSword(active && sphere == Items.agassword);

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
                SuperSteveAbilities.DriveAngelGear(active && sphere == Items.angelgear);

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
            SuperSteveAbilities.DriveMobiusRing(false);   // resets the damage ramp
            CustomGoroEffects.FrozenTunaDrive(false, xiaoTuna, 0, ssTuna);   // resets the healing pool
            // (palette restore is handled by WeaponTextureSwap's own thread — it repaints vanilla the
            // moment Super Steve is no longer Xiao's equipped weapon)
        }

    }
}
