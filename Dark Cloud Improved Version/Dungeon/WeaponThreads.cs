using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The weapon-ability threads and their launchers: the dungeon loop calls <see cref="Launch"/> every walking-mode tick and each
    /// character's launcher starts the threads its equipped weapon (and the weapons it inherits from) needs.</summary>
    internal static class WeaponThreads
    {
        // one thread per weapon ability: started when the weapon is equipped, each ends itself when the weapon is put away
        private static Thread boneDoorThread = new Thread(new ThreadStart(BoneRapier.BoneDoorTrigger));
        private static Thread seventhHeavenThread = new Thread(new ThreadStart(SeventhHeaven.DivineGuardEffect));
        private static Thread chronicleSwordThread = new Thread(new ThreadStart(ChronicleSword.ChronicleSwordEffect));
        private static Thread evilciseThread = new Thread(new ThreadStart(Evilcise.JealousSoulEffect));
        private static Thread maneaterThread = new Thread(new ThreadStart(Maneater.BloodPriceEffect));
        private static Thread sunSwordThread = new Thread(new ThreadStart(SunSword.SolarHarvestEffect));
        private static Thread solarFlashThread = new Thread(() => SunSword.SolarFlashEffect(SunSword.SunSwordFlash));
        private static Thread bigBangThread = new Thread(new ThreadStart(BigBang.DetonateEffect));
        private static Thread zeusThread = new Thread(new ThreadStart(SwordOfZeus.LightningEffect));
        private static Thread crossHinderThread = new Thread(new ThreadStart(CrossHinder.SanctifierEffect));
        private static Thread boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.GravediggerEffect));
        private static Thread tsukikageThread = new Thread(new ThreadStart(Tsukikage.MoonlitFocusEffect));
        private static Thread smallSwordThread = new Thread(new ThreadStart(SmallSword.QuickDrawEffect));
        private static Thread darkCloudThread = new Thread(new ThreadStart(DarkCloud.GuardCrushEffect));
        private static Thread kitchenKnifeThread = new Thread(new ThreadStart(KitchenKnife.SpringsBlessingEffect));
        private static Thread angelGearThread = new Thread(new ThreadStart(AngelGear.GuardianReflectorEffect));
        private static Thread superSteveThread = new Thread(new ThreadStart(SuperSteve.SphereInheritanceEffect));
        private static Thread matadorThread = new Thread(new ThreadStart(Matador.ChargingBullEffect));
        private static Thread dragonsYThread = new Thread(new ThreadStart(DragonsY.DragonsBreathEffect));
        private static Thread lockOnSpeedThread = new Thread(new ThreadStart(DragonsY.LockOnSpeedEffect));
        private static Thread doubleImpactThread = new Thread(new ThreadStart(DoubleImpact.DoubleImpactEffect));
        private static Thread banditSlingshotThread = new Thread(new ThreadStart(BanditSlingshot.StealShotEffect));
        private static Thread banditsRingThread = new Thread(new ThreadStart(BanditsRing.StealShotEffect));
        private static Thread steelSlingshotThread = new Thread(new ThreadStart(SteelSlingshot.EnduranceUpEffect));
        private static Thread hardshooterThread = new Thread(new ThreadStart(Hardshooter.RicochetEffect));
        private static Thread lockOnReachThread = new Thread(new ThreadStart(Flamingo.LockOnDistanceEffect));
        private static Thread angelShooterThread = new Thread(new ThreadStart(AngelShooter.GuardianGraceEffect));
        private static Thread divineBeastTitleThread = new Thread(new ThreadStart(DivineBeastTitle.SpiritBeastEffect));
        private static Thread heavensCloudThread = new Thread(new ThreadStart(HeavensCloud.TyphoonEffect));
        private static Thread snailThread = new Thread(new ThreadStart(CustomOsmondEffects.SlimeTrailEffect));
        private static Thread agasSwordThread = new Thread(new ThreadStart(AgasSword.DefensiveLegacyEffect));
        private static Thread braveArkThread = new Thread(new ThreadStart(BraveArk.HerosCourageEffect));
        private static Thread tallHammerThread = new Thread(new ThreadStart(CustomGoroEffects.TallHammerEffect));
        private static Thread frozenTunaThread = new Thread(new ThreadStart(CustomGoroEffects.ColdStorageEffect));
        private static Thread infernoHammerThread = new Thread(new ThreadStart(CustomGoroEffects.InfernoEffect));
        private static Thread mobiusRingThread = new Thread(new ThreadStart(CustomRubyEffects.MobiusRingEffect));
        private static Thread herculesWrathThread = new Thread(new ThreadStart(CustomUngagaEffects.HerculesWrathEffect));
        private static Thread babelSpearThread = new Thread(new ThreadStart(CustomUngagaEffects.BabelSpearEffect));
        private static Thread cactusThread = new Thread(new ThreadStart(CustomUngagaEffects.AbsorbEffect));
        private static Thread supernovaThread = new Thread(new ThreadStart(CustomOsmondEffects.SupernovaEffect));
        private static Thread starBreakerThread = new Thread(new ThreadStart(CustomOsmondEffects.ShootingStarsEffect));
        private static Thread skunkThread = new Thread(new ThreadStart(CustomOsmondEffects.LongerFlameEffect));
        private static Thread wiseOwlSwordThread = new Thread(new ThreadStart(WiseOwlSword.WiseOwlAlwaysKnowsEffect));

        /// <summary>The cursing swords apply on equip, even from the pause menu: their threads start ahead of the walking-mode
        /// launch.</summary>
        internal static void LaunchCurses()
        {
        // Evilcise curse applies immediately on equip, even from the pause menu
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.evilcise &&
            !evilciseThread.IsAlive)
        {
            evilciseThread = new Thread(new ThreadStart(Evilcise.JealousSoulEffect));
            evilciseThread.Start();
        }

        // Maneater curse likewise applies immediately on equip
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.maneater &&
            !maneaterThread.IsAlive)
        {
            maneaterThread = new Thread(new ThreadStart(Maneater.BloodPriceEffect));
            maneaterThread.Start();
        }
        }

        /// <summary>The equipped weapon's ability threads for the active character, started when not already running; called
        /// every dungeon tick in walking mode.</summary>
        internal static void Launch()
        {
            switch (Player.CurrentCharacterNum())
            {
                case Player.ToanId: Toan(); break;
                case Player.XiaoId: Xiao(); break;
                case Player.GoroId: Goro(); break;
                case Player.RubyId: Ruby(); break;
                case Player.UngagaId: Ungaga(); break;
                case Player.OsmondId: Osmond(); break;
            }
        }

        private static void Toan()
        {
            if(Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.bonerapier:
                    BoneRapier.SkeletonKeyEffect(true);

                    if (!boneDoorThread.IsAlive)
                    {
                        boneDoorThread = new Thread(new ThreadStart(BoneRapier.BoneDoorTrigger));
                        boneDoorThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.GravediggerEffect));
                        boneNoRevivalThread.Start();
                    }
                    break;
                case Items.seventhheaven:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!seventhHeavenThread.IsAlive)
                    {
                        seventhHeavenThread = new Thread(new ThreadStart(SeventhHeaven.DivineGuardEffect));
                        seventhHeavenThread.Start();
                    }

                    // 7th Heaven also inherits Dark Cloud's Guard Crush (lineage)
                    if (!darkCloudThread.IsAlive)
                    {
                        darkCloudThread = new Thread(new ThreadStart(DarkCloud.GuardCrushEffect));
                        darkCloudThread.Start();
                    }
                    break;
                case Items.chroniclesword:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!chronicleSwordThread.IsAlive)
                    {
                        chronicleSwordThread = new Thread(new ThreadStart(ChronicleSword.ChronicleSwordEffect));
                        chronicleSwordThread.Start();
                    }
                    break;

                case Items.heavenscloud:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!heavensCloudThread.IsAlive)
                    {
                        heavensCloudThread = new Thread(new ThreadStart(HeavensCloud.TyphoonEffect));
                        heavensCloudThread.Start();
                    }

                    // Heaven's Cloud also inherits Moonlit Focus (Tsukikage lineage)
                    if (!tsukikageThread.IsAlive)
                    {
                        tsukikageThread = new Thread(new ThreadStart(Tsukikage.MoonlitFocusEffect));
                        tsukikageThread.Start();
                    }

                    // ...and Quick Draw (Small Sword lineage)
                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.QuickDrawEffect));
                        smallSwordThread.Start();
                    }
                    break;

                case Items.evilcise:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!evilciseThread.IsAlive)
                    {
                        evilciseThread = new Thread(new ThreadStart(Evilcise.JealousSoulEffect));
                        evilciseThread.Start();
                    }
                    break;

                case Items.maneater:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!maneaterThread.IsAlive)
                    {
                        maneaterThread = new Thread(new ThreadStart(Maneater.BloodPriceEffect));
                        maneaterThread.Start();
                    }
                    break;

                case Items.tsukikage:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!tsukikageThread.IsAlive)
                    {
                        tsukikageThread = new Thread(new ThreadStart(Tsukikage.MoonlitFocusEffect));
                        tsukikageThread.Start();
                    }

                    // Tsukikage also inherits Quick Draw (Small Sword lineage)
                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.QuickDrawEffect));
                        smallSwordThread.Start();
                    }
                    break;


                case Items.smallsword:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.QuickDrawEffect));
                        smallSwordThread.Start();
                    }
                    break;

                case Items.darkcloud:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!darkCloudThread.IsAlive)
                    {
                        darkCloudThread = new Thread(new ThreadStart(DarkCloud.GuardCrushEffect));
                        darkCloudThread.Start();
                    }
                    break;

                case Items.sunsword:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!sunSwordThread.IsAlive)
                    {
                        sunSwordThread = new Thread(new ThreadStart(SunSword.SolarHarvestEffect));
                        sunSwordThread.Start();
                    }
                    if (!solarFlashThread.IsAlive)
                    {
                        solarFlashThread = new Thread(() => SunSword.SolarFlashEffect(SunSword.SunSwordFlash));
                        solarFlashThread.Start();
                    }
                    break;

                case Items.bigbang:   // inherits Solar Harvest AND Solar Flash (Sun Sword lineage) + its own Detonate
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!sunSwordThread.IsAlive)
                    {
                        sunSwordThread = new Thread(new ThreadStart(SunSword.SolarHarvestEffect));
                        sunSwordThread.Start();
                    }
                    if (!solarFlashThread.IsAlive)
                    {
                        solarFlashThread = new Thread(() => SunSword.SolarFlashEffect(SunSword.BigBangFlash));
                        solarFlashThread.Start();
                    }
                    if (!bigBangThread.IsAlive)
                    {
                        bigBangThread = new Thread(new ThreadStart(BigBang.DetonateEffect));
                        bigBangThread.Start();
                    }
                    break;

                case Items.swordofzeus:   // inherits Solar Harvest AND Solar Flash (Sun Sword lineage) + its lightning
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!sunSwordThread.IsAlive)
                    {
                        sunSwordThread = new Thread(new ThreadStart(SunSword.SolarHarvestEffect));
                        sunSwordThread.Start();
                    }
                    if (!solarFlashThread.IsAlive)
                    {
                        solarFlashThread = new Thread(() => SunSword.SolarFlashEffect(SunSword.ZeusFlash));
                        solarFlashThread.Start();
                    }
                    if (!zeusThread.IsAlive)
                    {
                        zeusThread = new Thread(new ThreadStart(SwordOfZeus.LightningEffect));
                        zeusThread.Start();
                    }
                    break;

                case Items.crosshinder:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!crossHinderThread.IsAlive)
                    {
                        crossHinderThread = new Thread(new ThreadStart(CrossHinder.SanctifierEffect));
                        crossHinderThread.Start();
                    }
                    break;

                case Items.agassword:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!agasSwordThread.IsAlive)
                    {
                        agasSwordThread = new Thread(new ThreadStart(AgasSword.DefensiveLegacyEffect));
                        agasSwordThread.Start();
                    }
                    break;

                case Items.braveark:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!braveArkThread.IsAlive)
                    {
                        braveArkThread = new Thread(new ThreadStart(BraveArk.HerosCourageEffect));
                        braveArkThread.Start();
                    }
                    break;

                // Kitchen Knife is a TOAN sword — its effect gates on ToanId, so registering it under
                // Xiao (where it used to live) made it unreachable: Xiao can never equip a Toan sword,
                // so the thread never started, and the spring blessing could never fire.
                case Items.kitchenknife:
                    BoneRapier.SkeletonKeyEffect(false);

                    if (!kitchenKnifeThread.IsAlive)
                    {
                        kitchenKnifeThread = new Thread(new ThreadStart(KitchenKnife.SpringsBlessingEffect));
                        kitchenKnifeThread.Start();
                    }
                    break;

                default:
                    BoneRapier.SkeletonKeyEffect(false);
                    break;
            }
        }

        private static void Xiao()
        {
            // Super Steve manages the bone-door bypass itself (via an attached Bone Rapier / Bone Slingshot sphere); the Bone Slingshot has it below.
            if (Player.Weapon.GetCurrentWeaponId() != Items.supersteve && Player.Weapon.GetCurrentWeaponId() != Items.boneslingshot) BoneRapier.SkeletonKeyEffect(false);
            if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

            // The lock-on movement buff: Dragon's Y's, and the three weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (DragonsY.LockOnSpeedGrants(Player.Weapon.GetCurrentWeaponId()) && !lockOnSpeedThread.IsAlive)
            {
                lockOnSpeedThread = new Thread(new ThreadStart(DragonsY.LockOnSpeedEffect));
                lockOnSpeedThread.Start();
            }
            // The lock-on reach: the Flamingo's, and the four weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (Flamingo.GrantsReach(Player.Weapon.GetCurrentWeaponId()) && !lockOnReachThread.IsAlive)
            {
                lockOnReachThread = new Thread(new ThreadStart(Flamingo.LockOnDistanceEffect));
                lockOnReachThread.Start();
            }
            // The cat: the Divine Beast Title's, the two weapons that inherit it with their own looks, and Super Steve with any of
            // their spheres — ONE thread for all of them (the cat must never have two drivers).
            if (DivineBeastTitle.Wields() && !divineBeastTitleThread.IsAlive)
            {
                divineBeastTitleThread = new Thread(new ThreadStart(DivineBeastTitle.SpiritBeastEffect));
                divineBeastTitleThread.Start();
            }
            // Guardian Grace: the Angel Shooter's, the Angel Gear's by inheritance, and Super Steve with either sphere — one thread.
            if (AngelShooter.Carries() && !angelShooterThread.IsAlive)
            {
                angelShooterThread = new Thread(new ThreadStart(AngelShooter.GuardianGraceEffect));
                angelShooterThread.Start();
            }
            switch (Player.Weapon.GetCurrentWeaponId())
            {

                case Items.angelgear:
                    if (!angelGearThread.IsAlive)
                    {
                        angelGearThread = new Thread(new ThreadStart(AngelGear.GuardianReflectorEffect));
                        angelGearThread.Start();
                    }
                    break;

                case Items.supersteve:
                    if (!superSteveThread.IsAlive)
                    {
                        superSteveThread = new Thread(new ThreadStart(SuperSteve.SphereInheritanceEffect));
                        superSteveThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)   // the bone key's no-revival, for a Bone Rapier / Bone Slingshot sphere (the thread checks)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.GravediggerEffect));
                        boneNoRevivalThread.Start();
                    }
                    if (!crossHinderThread.IsAlive && CrossHinder.CrossHinderWielded())   // Sanctifier, for a Cross Hinder sphere
                    {
                        crossHinderThread = new Thread(new ThreadStart(CrossHinder.SanctifierEffect));
                        crossHinderThread.Start();
                    }
                    break;

                case Items.matador:
                    if (!matadorThread.IsAlive)
                    {
                        matadorThread = new Thread(new ThreadStart(Matador.ChargingBullEffect));
                        matadorThread.Start();
                    }
                    break;

                case Items.dragonsy:
                    if (!dragonsYThread.IsAlive)
                    {
                        dragonsYThread = new Thread(new ThreadStart(DragonsY.DragonsBreathEffect));
                        dragonsYThread.Start();
                    }
                    break;

                case Items.doubleimpact:
                    if (!doubleImpactThread.IsAlive)
                    {
                        doubleImpactThread = new Thread(new ThreadStart(DoubleImpact.DoubleImpactEffect));
                        doubleImpactThread.Start();
                    }
                    break;

                case Items.banditslingshot:
                    if (!banditSlingshotThread.IsAlive)
                    {
                        banditSlingshotThread = new Thread(new ThreadStart(BanditSlingshot.StealShotEffect));
                        banditSlingshotThread.Start();
                    }
                    break;

                case Items.boneslingshot:
                    // the skeleton key, the Bone Rapier's: bone doors open without their key, the undead stay down
                    BoneRapier.SkeletonKeyEffect(true);

                    if (!boneDoorThread.IsAlive)
                    {
                        boneDoorThread = new Thread(new ThreadStart(BoneRapier.BoneDoorTrigger));
                        boneDoorThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.GravediggerEffect));
                        boneNoRevivalThread.Start();
                    }
                    break;

                case Items.steelslingshot:
                    if (!steelSlingshotThread.IsAlive)
                    {
                        steelSlingshotThread = new Thread(new ThreadStart(SteelSlingshot.EnduranceUpEffect));
                        steelSlingshotThread.Start();
                    }
                    break;

                case Items.hardshooter:
                    if (!hardshooterThread.IsAlive)
                    {
                        hardshooterThread = new Thread(new ThreadStart(Hardshooter.RicochetEffect));
                        hardshooterThread.Start();
                    }
                    break;

                default:
                    break;
            }
        }

        private static void Goro()
        {
            BoneRapier.SkeletonKeyEffect(false);
            if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.tallhammer:
                    if (!tallHammerThread.IsAlive)
                    {
                        tallHammerThread = new Thread(new ThreadStart(CustomGoroEffects.TallHammerEffect));
                        tallHammerThread.Start();
                    }
                    break;
                case Items.frozentuna:
                    if (!frozenTunaThread.IsAlive)
                    {
                        frozenTunaThread = new Thread(new ThreadStart(CustomGoroEffects.ColdStorageEffect));
                        frozenTunaThread.Start();
                    }
                    break;
                case Items.inferno:
                    if (!infernoHammerThread.IsAlive)
                    {
                        infernoHammerThread = new Thread(new ThreadStart(CustomGoroEffects.InfernoEffect));
                        infernoHammerThread.Start();
                    }
                    break;

                default:
                    break;
            }
        }

        private static void Ruby()
        {
            BoneRapier.SkeletonKeyEffect(false);

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.mobiusring:
                    if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

                    if (!mobiusRingThread.IsAlive)
                    {
                        mobiusRingThread = new Thread(new ThreadStart(CustomRubyEffects.MobiusRingEffect));
                        mobiusRingThread.Start();
                    }
                    break;
                case Items.banditsring:
                    if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

                    if (!banditsRingThread.IsAlive)
                    {
                        banditsRingThread = new Thread(new ThreadStart(BanditsRing.StealShotEffect));
                        banditsRingThread.Start();
                    }
                    break;
                case Items.secretarmlet:
                    if (!Dungeon.magicCircleChanged) {
                        bool executed = CustomRubyEffects.SecretArmletEnable();
                        if(executed) Dungeon.magicCircleChanged = true;
                    }
                    break;
                default:
                    if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;
                    break;
            }
        }

        private static void Ungaga()
        {
            BoneRapier.SkeletonKeyEffect(false);
            if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;


            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.herculeswrath:
                    if (!herculesWrathThread.IsAlive)
                    {
                        herculesWrathThread = new Thread(new ThreadStart(CustomUngagaEffects.HerculesWrathEffect));
                        herculesWrathThread.Start();
                    }
                    break;

                case Items.babelsspear:
                    if (!babelSpearThread.IsAlive)
                    {
                        babelSpearThread = new Thread(new ThreadStart(CustomUngagaEffects.BabelSpearEffect));
                        babelSpearThread.Start();
                    }
                    break;

                case Items.cactus:
                    if (!cactusThread.IsAlive)
                    {
                        cactusThread = new Thread(new ThreadStart(CustomUngagaEffects.AbsorbEffect));
                        cactusThread.Start();
                    }
                    break;
                default:
                    break;
            }
        }

        private static void Osmond()
        {
            BoneRapier.SkeletonKeyEffect(false);
            if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.supernova:
                    if (!supernovaThread.IsAlive)
                    {
                        supernovaThread = new Thread(new ThreadStart(CustomOsmondEffects.SupernovaEffect));
                        supernovaThread.Start();
                    }
                    break;

                case Items.starbreaker:
                    if (!starBreakerThread.IsAlive)
                    {
                        starBreakerThread = new Thread(new ThreadStart(CustomOsmondEffects.ShootingStarsEffect));
                        starBreakerThread.Start();
                    }
                    break;

                case Items.snail:
                    if (!snailThread.IsAlive)
                    {
                        snailThread = new Thread(new ThreadStart(CustomOsmondEffects.SlimeTrailEffect));
                        snailThread.Start();
                    }
                    break;

                case Items.skunk:
                    if (!skunkThread.IsAlive)
                    {
                        skunkThread = new Thread(new ThreadStart(CustomOsmondEffects.LongerFlameEffect));
                        skunkThread.Start();
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>The Wise Owl Sword's floor-secrets message runs in Wise Owl Forest whether or not the sword is equipped.</summary>
        internal static void LaunchWiseOwl(byte currentDungeon)
        {
            if (currentDungeon == 1 && !wiseOwlSwordThread.IsAlive)
            {
                wiseOwlSwordThread = new Thread(new ThreadStart(WiseOwlSword.WiseOwlAlwaysKnowsEffect));
                wiseOwlSwordThread.Start();
            }
        }
    }
}
