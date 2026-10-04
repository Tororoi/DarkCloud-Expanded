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
        private static Thread baselardThread = new Thread(new ThreadStart(Baselard.HeavyHandEffect));
        private static Thread claymoreThread = new Thread(new ThreadStart(Claymore.GreatswordEffect));
        private static Thread shamshirThread = new Thread(new ThreadStart(Shamshir.SwiftStrikesEffect));
        private static Thread dusackThread = new Thread(new ThreadStart(Dusack.NoFoolsGoldEffect));
        private static Thread saxThread = new Thread(new ThreadStart(Sax.FineFareEffect));
        private static Thread gladiusThread = new Thread(new ThreadStart(Gladius.JacketHunterEffect));
        private static Thread crysKnifeThread = new Thread(new ThreadStart(CrysKnife.CrystalAffinityEffect));
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
        private static Thread snailThread = new Thread(new ThreadStart(Snail.SlimeTrailEffect));
        private static Thread agasSwordThread = new Thread(new ThreadStart(AgasSword.DefensiveLegacyEffect));
        private static Thread braveArkThread = new Thread(new ThreadStart(BraveArk.HerosCourageEffect));
        private static Thread tallHammerThread = new Thread(new ThreadStart(TallHammer.TallHammerEffect));
        private static Thread frozenTunaThread = new Thread(new ThreadStart(FrozenTuna.ColdStorageEffect));
        private static Thread infernoHammerThread = new Thread(new ThreadStart(Inferno.InfernoEffect));
        private static Thread mobiusRingThread = new Thread(new ThreadStart(MobiusRing.MobiusRingEffect));
        private static Thread halberdLineChargeThread = new Thread(new ThreadStart(Halberd.TornadoChargeBuffEffect));
        private static Thread partisanThread = new Thread(new ThreadStart(Partisan.QuickSwingEffect));
        private static Thread deSangaThread = new Thread(new ThreadStart(DeSanga.VampireEffect));
        private static Thread javelinThread = new Thread(new ThreadStart(Javelin.MarineEffect));
        private static Thread scorpionVenomThread = new Thread(new ThreadStart(Scorpion.VenomEffect));
        private static Thread cactusThread = new Thread(new ThreadStart(Cactus.DesertBloomEffect));
        private static Thread absorbThread = new Thread(new ThreadStart(Cactus.AbsorbEffect));   // the Cactus's second loop: 50 ms, Ungaga's own hand only, no pause gate — not Desert Bloom's 16 ms sphere-aware loop
        private static Thread herculesWrathThread = new Thread(new ThreadStart(HerculesWrath.AirStrikeEffect));
        private static Thread babelSpearThread = new Thread(new ThreadStart(BabelsSpear.CurseOfBabelEffect));
        private static Thread terraSwordThread = new Thread(new ThreadStart(TerraSword.BigRockEffect));
        private static Thread supernovaThread = new Thread(new ThreadStart(Supernova.SupernovaEffect));
        private static Thread starBreakerThread = new Thread(new ThreadStart(StarBreaker.ShootingStarsEffect));
        private static Thread skunkThread = new Thread(new ThreadStart(Skunk.LongerFlameEffect));
        private static Thread wiseOwlSwordThread = new Thread(new ThreadStart(WiseOwlSword.WiseOwlAlwaysKnowsEffect));

        /// <summary>Starts <paramref name="entry"/> on a fresh thread in <paramref name="thread"/> unless the one there is still running.</summary>
        private static void Ensure(ref Thread thread, ThreadStart entry)
        {
            if (thread.IsAlive) return;
            thread = new Thread(entry);
            thread.Start();
        }
        /// <summary>The same, for an ability that runs only while <paramref name="wanted"/> holds (a Super Steve sphere): the
        /// predicate is read only while the thread is down.</summary>
        private static void Ensure(ref Thread thread, Func<bool> wanted, ThreadStart entry)
        {
            if (thread.IsAlive || !wanted()) return;
            thread = new Thread(entry);
            thread.Start();
        }

        /// <summary>The cursing swords apply on equip, even from the pause menu: their threads start ahead of the walking-mode
        /// launch.</summary>
        internal static void LaunchCurses()
        {
        // Evilcise curse applies immediately on equip, even from the pause menu
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.evilcise)
            Ensure(ref evilciseThread, Evilcise.JealousSoulEffect);

        // Maneater curse likewise applies immediately on equip
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.maneater)
            Ensure(ref maneaterThread, Maneater.BloodPriceEffect);
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

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.bonerapier:
                    BoneRapier.SkeletonKeyEffect(true);

                    Ensure(ref boneDoorThread, BoneRapier.BoneDoorTrigger);
                    Ensure(ref boneNoRevivalThread, BoneRapier.GravediggerEffect);
                    break;
                case Items.seventhheaven:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref seventhHeavenThread, SeventhHeaven.DivineGuardEffect);

                    // 7th Heaven also inherits Dark Cloud's Guard Crush (lineage)
                    Ensure(ref darkCloudThread, DarkCloud.GuardCrushEffect);
                    break;
                case Items.chroniclesword:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref chronicleSwordThread, ChronicleSword.ChronicleSwordEffect);
                    Ensure(ref shamshirThread, Shamshir.SwiftStrikesEffect);   // Swift Strikes, inherited from the Shamshir
                    Ensure(ref saxThread, Sax.FineFareEffect);        // Fine Fare, inherited down the Sax line (with Treasure Keys → Gold Bullion, Repair → Auto Repair one in four)
                    break;

                case Items.dusack:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref dusackThread, Dusack.NoFoolsGoldEffect);     // No Fool's Gold
                    Ensure(ref saxThread, Sax.FineFareEffect);        // Fine Fare, inherited from the Sax (plus Treasure Keys → Gold Bullion)
                    Ensure(ref shamshirThread, Shamshir.SwiftStrikesEffect);   // Swift Strikes, inherited from the Shamshir
                    break;

                case Items.sevenbranchsword:
                case Items.atlamilliasword:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref shamshirThread, Shamshir.SwiftStrikesEffect);   // Swift Strikes, inherited from the Shamshir
                    Ensure(ref saxThread, Sax.FineFareEffect);        // Fine Fare, inherited down the Sax line (with Treasure Keys → Gold Bullion, Repair → Auto Repair one in four)
                    break;


                case Items.heavenscloud:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref heavensCloudThread, HeavensCloud.TyphoonEffect);

                    // Heaven's Cloud also inherits Moonlit Focus (Tsukikage lineage)
                    Ensure(ref tsukikageThread, Tsukikage.MoonlitFocusEffect);

                    // ...and Quick Draw (Small Sword lineage)
                    Ensure(ref smallSwordThread, SmallSword.QuickDrawEffect);
                    break;

                case Items.evilcise:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref evilciseThread, Evilcise.JealousSoulEffect);
                    break;

                case Items.maneater:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref maneaterThread, Maneater.BloodPriceEffect);
                    break;

                case Items.tsukikage:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref tsukikageThread, Tsukikage.MoonlitFocusEffect);

                    // Tsukikage also inherits Quick Draw (Small Sword lineage)
                    Ensure(ref smallSwordThread, SmallSword.QuickDrawEffect);
                    break;


                case Items.smallsword:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref smallSwordThread, SmallSword.QuickDrawEffect);
                    break;

                case Items.darkcloud:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref darkCloudThread, DarkCloud.GuardCrushEffect);
                    break;

                case Items.sunsword:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref sunSwordThread, SunSword.SolarHarvestEffect);
                    Ensure(ref solarFlashThread, () => SunSword.SolarFlashEffect(SunSword.SunSwordFlash));
                    break;

                case Items.bigbang:   // inherits Solar Harvest AND Solar Flash (Sun Sword lineage) + its own Detonate
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref sunSwordThread, SunSword.SolarHarvestEffect);
                    Ensure(ref solarFlashThread, () => SunSword.SolarFlashEffect(SunSword.BigBangFlash));
                    Ensure(ref bigBangThread, BigBang.DetonateEffect);
                    break;

                case Items.swordofzeus:   // inherits Solar Harvest AND Solar Flash (Sun Sword lineage) + its lightning
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref sunSwordThread, SunSword.SolarHarvestEffect);
                    Ensure(ref solarFlashThread, () => SunSword.SolarFlashEffect(SunSword.ZeusFlash));
                    Ensure(ref zeusThread, SwordOfZeus.LightningEffect);
                    break;

                case Items.crosshinder:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref crossHinderThread, CrossHinder.SanctifierEffect);
                    break;

                case Items.agassword:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref agasSwordThread, AgasSword.DefensiveLegacyEffect);
                    break;

                case Items.braveark:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref braveArkThread, BraveArk.HerosCourageEffect);
                    Ensure(ref dusackThread, Dusack.NoFoolsGoldEffect);     // No Fool's Gold, inherited from the Dusack
                    break;

                // Kitchen Knife is a TOAN sword — its effect gates on ToanId, so registering it under
                // Xiao (where it used to live) made it unreachable: Xiao can never equip a Toan sword,
                // so the thread never started, and the spring blessing could never fire.
                case Items.kitchenknife:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref kitchenKnifeThread, KitchenKnife.SpringsBlessingEffect);
                    break;

                case Items.baselard:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref baselardThread, Baselard.HeavyHandEffect);
                    break;

                case Items.claymore:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref claymoreThread, Claymore.GreatswordEffect);
                    break;

                case Items.sax:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref saxThread, Sax.FineFareEffect);
                    break;

                case Items.shamshir:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref shamshirThread, Shamshir.SwiftStrikesEffect);
                    break;

                case Items.crystalknife:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref crysKnifeThread, CrysKnife.CrystalAffinityEffect);
                    break;

                case Items.gladius:
                    BoneRapier.SkeletonKeyEffect(false);

                    Ensure(ref gladiusThread, Gladius.JacketHunterEffect);
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

            // The lock-on movement buff: Dragon's Y's, and the three weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (DragonsY.LockOnSpeedGrants(Player.Weapon.GetCurrentWeaponId())) Ensure(ref lockOnSpeedThread, DragonsY.LockOnSpeedEffect);
            // The lock-on reach: the Flamingo's, and the four weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (Flamingo.GrantsReach(Player.Weapon.GetCurrentWeaponId())) Ensure(ref lockOnReachThread, Flamingo.LockOnDistanceEffect);
            // The cat: the Divine Beast Title's, the two weapons that inherit it with their own looks, and Super Steve with any of
            // their spheres — ONE thread for all of them (the cat must never have two drivers).
            if (DivineBeastTitle.Wields()) Ensure(ref divineBeastTitleThread, DivineBeastTitle.SpiritBeastEffect);
            // Guardian Grace: the Angel Shooter's, the Angel Gear's by inheritance, and Super Steve with either sphere — one thread.
            if (AngelShooter.Carries()) Ensure(ref angelShooterThread, AngelShooter.GuardianGraceEffect);
            switch (Player.Weapon.GetCurrentWeaponId())
            {

                case Items.angelgear:
                    // The party regen only: the reflector itself (shield, ring, intercept) runs on AngelGear's own loop, started at the
                    // main menu, which is also what drives it for Super Steve's Angel Gear sphere (SuperSteve's loop pulses the regen).
                    Ensure(ref angelGearThread, AngelGear.GuardianReflectorEffect);
                    break;

                case Items.supersteve:
                    Ensure(ref superSteveThread, SuperSteve.SphereInheritanceEffect);
                    Ensure(ref deSangaThread, DeSanga.Wielded, DeSanga.VampireEffect);   // every kill heals the weapon 5 WHP, for a DeSanga sphere
                    Ensure(ref javelinThread, Javelin.Wielded, Javelin.MarineEffect);   // marine enemies defenseless and worth double ABS, for a Javelin sphere
                    Ensure(ref scorpionVenomThread, Scorpion.Wielded, Scorpion.VenomEffect);   // Scorpion's venom, for a Scorpion sphere
                    Ensure(ref babelSpearThread, BabelsSpear.Wielded, BabelsSpear.CurseOfBabelEffect);   // Curse of Babel, for a Babel's Spear sphere (Super Steve itself rises)
                    Ensure(ref cactusThread, Cactus.Wielded, Cactus.DesertBloomEffect);   // Desert Bloom, for a Cactus sphere (Queens' trees)
                    Ensure(ref terraSwordThread, TerraSword.Wielded, TerraSword.BigRockEffect);   // the nutfall, for a Terra Sword sphere
                    Ensure(ref herculesWrathThread, HerculesWrath.Wielded, HerculesWrath.AirStrikeEffect);   // Hercules' Wrath's ultimate, for its sphere
                    Ensure(ref boneNoRevivalThread, BoneRapier.GravediggerEffect);   // the bone key's no-revival, for a Bone Rapier / Bone Slingshot sphere (the thread checks)
                    if (CrossHinder.CrossHinderWielded()) Ensure(ref crossHinderThread, CrossHinder.SanctifierEffect);   // Sanctifier, for a Cross Hinder sphere
                    Ensure(ref gladiusThread, Gladius.Wielded, Gladius.JacketHunterEffect);   // Jacket Hunter, for a Gladius sphere
                    Ensure(ref crysKnifeThread, CrysKnife.Wielded, CrysKnife.CrystalAffinityEffect);   // Crystal Affinity, for a Crysknife sphere (its circle passive is ownership's, not the sphere's)
                    break;

                case Items.matador:
                    Ensure(ref matadorThread, Matador.ChargingBullEffect);
                    break;

                case Items.dragonsy:
                    Ensure(ref dragonsYThread, DragonsY.DragonsBreathEffect);
                    break;

                case Items.doubleimpact:
                    Ensure(ref doubleImpactThread, DoubleImpact.DoubleImpactEffect);
                    break;

                case Items.banditslingshot:
                    Ensure(ref banditSlingshotThread, BanditSlingshot.StealShotEffect);
                    break;

                case Items.boneslingshot:
                    // the skeleton key, the Bone Rapier's: bone doors open without their key, the undead stay down
                    BoneRapier.SkeletonKeyEffect(true);

                    Ensure(ref boneDoorThread, BoneRapier.BoneDoorTrigger);
                    Ensure(ref boneNoRevivalThread, BoneRapier.GravediggerEffect);
                    break;

                case Items.steelslingshot:
                    Ensure(ref steelSlingshotThread, SteelSlingshot.EnduranceUpEffect);
                    break;

                case Items.hardshooter:
                    Ensure(ref hardshooterThread, Hardshooter.RicochetEffect);
                    break;

                default:
                    break;
            }
        }

        private static void Goro()
        {
            BoneRapier.SkeletonKeyEffect(false);

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.tallhammer:
                    Ensure(ref tallHammerThread, TallHammer.TallHammerEffect);
                    break;
                case Items.frozentuna:
                    Ensure(ref frozenTunaThread, FrozenTuna.ColdStorageEffect);
                    break;
                case Items.inferno:
                    Ensure(ref infernoHammerThread, Inferno.InfernoEffect);
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

                    Ensure(ref mobiusRingThread, MobiusRing.MobiusRingEffect);
                    break;
                case Items.banditsring:

                    Ensure(ref banditsRingThread, BanditsRing.StealShotEffect);
                    break;
                default:
                    break;
            }
        }

        private static void Ungaga()
        {
            BoneRapier.SkeletonKeyEffect(false);


            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.herculeswrath:
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);
                    Ensure(ref herculesWrathThread, HerculesWrath.AirStrikeEffect);   // its own: the ultimate
                    break;

                case Items.babelsspear:
                    Ensure(ref babelSpearThread, BabelsSpear.CurseOfBabelEffect);
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    break;

                case Items.cactus:
                    Ensure(ref absorbThread, Cactus.AbsorbEffect);   // its hits water Ungaga (50 ms)
                    Ensure(ref cactusThread, Cactus.DesertBloomEffect);   // its guard: the cactus rising ahead (16 ms)
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    break;

                case Items.halberd:
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    break;

                case Items.partisan:
                    Ensure(ref partisanThread, Partisan.QuickSwingEffect);   // the combo swings a third faster (Shamshir's factor)
                    break;

                case Items.desanga:
                    Ensure(ref deSangaThread, DeSanga.VampireEffect);   // every kill heals the weapon 5 WHP
                    break;

                case Items.javelin:
                    Ensure(ref javelinThread, Javelin.MarineEffect);   // marine enemies defenseless and worth double ABS
                    break;

                case Items.scorpion:
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    Ensure(ref scorpionVenomThread, Scorpion.VenomEffect);   // its poison landing cures the wielder and feeds the weapon
                    break;

                case Items.mirage:
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    break;

                case Items.terrasword:
                    Ensure(ref halberdLineChargeThread, Halberd.TornadoChargeBuffEffect);   // the Halberd line's charge
                    Ensure(ref terraSwordThread, TerraSword.BigRockEffect);   // its guard: the boulder
                    break;

                default:
                    break;
            }
        }

        private static void Osmond()
        {
            BoneRapier.SkeletonKeyEffect(false);

            switch (Player.Weapon.GetCurrentWeaponId())
            {
                case Items.supernova:
                    Ensure(ref supernovaThread, Supernova.SupernovaEffect);
                    break;

                case Items.starbreaker:
                    Ensure(ref starBreakerThread, StarBreaker.ShootingStarsEffect);
                    break;

                case Items.snail:
                    Ensure(ref snailThread, Snail.SlimeTrailEffect);
                    break;

                case Items.skunk:
                    Ensure(ref skunkThread, Skunk.LongerFlameEffect);
                    break;
                default:
                    break;
            }
        }

        /// <summary>The Wise Owl Sword's floor-secrets message runs in Wise Owl Forest whether or not the sword is equipped.</summary>
        internal static void LaunchWiseOwl(byte currentDungeon)
        {
            if (currentDungeon == 1) Ensure(ref wiseOwlSwordThread, WiseOwlSword.WiseOwlAlwaysKnowsEffect);
        }
    }
}
