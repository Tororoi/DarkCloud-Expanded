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
        private static Thread seventhHeavenThread = new Thread(new ThreadStart(SeventhHeaven.SeventhHeavenEffect));
        private static Thread chronicleSwordThread = new Thread(new ThreadStart(ChronicleSword.ChronicleSwordEffect));
        private static Thread evilciseThread = new Thread(new ThreadStart(ToanCurses.EvilciseEffect));
        private static Thread maneaterThread = new Thread(new ThreadStart(ToanCurses.ManeaterEffect));
        private static Thread sunSwordThread = new Thread(new ThreadStart(SunSword.SunSwordEffect));
        private static Thread bigBangThread = new Thread(new ThreadStart(BigBang.BigBangEffect));
        private static Thread crossHinderThread = new Thread(new ThreadStart(CrossHinder.CrossHinderEffect));
        private static Thread boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.BoneKeyNoRevivalEffect));
        private static Thread tsukikageThread = new Thread(new ThreadStart(Tsukikage.TsukikageEffect));
        private static Thread smallSwordThread = new Thread(new ThreadStart(SmallSword.SmallSwordEffect));
        private static Thread darkCloudThread = new Thread(new ThreadStart(DarkCloud.DarkCloudEffect));
        private static Thread kitchenKnifeThread = new Thread(new ThreadStart(KitchenKnife.KitchenKnifeEffect));
        private static Thread angelGearThread = new Thread(new ThreadStart(CustomXiaoEffects.AngelGearEffect));
        private static Thread superSteveThread = new Thread(new ThreadStart(CustomXiaoEffects.SuperSteveEffect));
        private static Thread matadorThread = new Thread(new ThreadStart(CustomXiaoEffects.MatadorEffect));
        private static Thread dragonsYThread = new Thread(new ThreadStart(CustomXiaoEffects.DragonsYEffect));
        private static Thread lockOnSpeedThread = new Thread(new ThreadStart(CustomXiaoEffects.LockOnSpeedEffect));
        private static Thread doubleImpactThread = new Thread(new ThreadStart(CustomXiaoEffects.DoubleImpactEffect));
        private static Thread banditSlingshotThread = new Thread(new ThreadStart(CustomXiaoEffects.BanditSlingshotEffect));
        private static Thread steelSlingshotThread = new Thread(new ThreadStart(CustomXiaoEffects.SteelSlingshotEffect));
        private static Thread hardshooterThread = new Thread(new ThreadStart(CustomXiaoEffects.HardshooterEffect));
        private static Thread lockOnReachThread = new Thread(new ThreadStart(CustomXiaoEffects.LockOnReachEffect));
        private static Thread heavensCloudThread = new Thread(new ThreadStart(HeavensCloud.HeavensCloudEffect));
        private static Thread snailThread = new Thread(new ThreadStart(CustomOsmondEffects.SnailEffect));
        private static Thread agasSwordThread = new Thread(new ThreadStart(AgasSword.AgasSwordEffect));
        private static Thread braveArkThread = new Thread(new ThreadStart(BraveArk.BraveArkEffect));
        private static Thread tallHammerThread = new Thread(new ThreadStart(CustomGoroEffects.TallHammerEffect));
        private static Thread frozenTunaThread = new Thread(new ThreadStart(CustomGoroEffects.FrozenTunaEffect));
        private static Thread infernoHammerThread = new Thread(new ThreadStart(CustomGoroEffects.InfernoEffect));
        private static Thread mobiusRingThread = new Thread(new ThreadStart(CustomRubyEffects.MobiusRingEffect));
        private static Thread herculesWrathThread = new Thread(new ThreadStart(CustomUngagaEffects.HerculesWrathEffect));
        private static Thread babelSpearThread = new Thread(new ThreadStart(CustomUngagaEffects.BabelSpearEffect));
        private static Thread cactusThread = new Thread(new ThreadStart(CustomUngagaEffects.CactusEffect));
        private static Thread supernovaThread = new Thread(new ThreadStart(CustomOsmondEffects.SupernovaEffect));
        private static Thread starBreakerThread = new Thread(new ThreadStart(CustomOsmondEffects.StarBreakerEffect));
        private static Thread skunkThread = new Thread(new ThreadStart(CustomOsmondEffects.SkunkEffect));
        private static Thread wiseOwlSwordThread = new Thread(new ThreadStart(WiseOwlSword.WiseOwlSwordEffect));

        /// <summary>The cursing swords apply on equip, even from the pause menu: their threads start ahead of the walking-mode
        /// launch.</summary>
        internal static void LaunchCurses()
        {
        // Evilcise curse applies immediately on equip, even from the pause menu
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.evilcise &&
            !evilciseThread.IsAlive)
        {
            evilciseThread = new Thread(new ThreadStart(ToanCurses.EvilciseEffect));
            evilciseThread.Start();
        }

        // Maneater curse likewise applies immediately on equip
        if (Player.CurrentCharacterNum() == Player.ToanId &&
            Player.Weapon.GetCurrentWeaponId() == Items.maneater &&
            !maneaterThread.IsAlive)
        {
            maneaterThread = new Thread(new ThreadStart(ToanCurses.ManeaterEffect));
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
                    BoneRapier.BoneRapierEffect(true);

                    if (!boneDoorThread.IsAlive)
                    {
                        boneDoorThread = new Thread(new ThreadStart(BoneRapier.BoneDoorTrigger));
                        boneDoorThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.BoneKeyNoRevivalEffect));
                        boneNoRevivalThread.Start();
                    }
                    break;
                case Items.seventhheaven:
                    BoneRapier.BoneRapierEffect(false);

                    if (!seventhHeavenThread.IsAlive)
                    {
                        seventhHeavenThread = new Thread(new ThreadStart(SeventhHeaven.SeventhHeavenEffect));
                        seventhHeavenThread.Start();
                    }

                    // 7th Heaven also inherits Dark Cloud's Guard Crush (lineage)
                    if (!darkCloudThread.IsAlive)
                    {
                        darkCloudThread = new Thread(new ThreadStart(DarkCloud.DarkCloudEffect));
                        darkCloudThread.Start();
                    }
                    break;
                case Items.chroniclesword:
                    BoneRapier.BoneRapierEffect(false);

                    if (!chronicleSwordThread.IsAlive)
                    {
                        chronicleSwordThread = new Thread(new ThreadStart(ChronicleSword.ChronicleSwordEffect));
                        chronicleSwordThread.Start();
                    }
                    break;

                case Items.heavenscloud:
                    BoneRapier.BoneRapierEffect(false);

                    if (!heavensCloudThread.IsAlive)
                    {
                        heavensCloudThread = new Thread(new ThreadStart(HeavensCloud.HeavensCloudEffect));
                        heavensCloudThread.Start();
                    }

                    // Heaven's Cloud also inherits Moonlit Focus (Tsukikage lineage)
                    if (!tsukikageThread.IsAlive)
                    {
                        tsukikageThread = new Thread(new ThreadStart(Tsukikage.TsukikageEffect));
                        tsukikageThread.Start();
                    }

                    // ...and Quick Draw (Small Sword lineage)
                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.SmallSwordEffect));
                        smallSwordThread.Start();
                    }
                    break;

                case Items.evilcise:
                    BoneRapier.BoneRapierEffect(false);

                    if (!evilciseThread.IsAlive)
                    {
                        evilciseThread = new Thread(new ThreadStart(ToanCurses.EvilciseEffect));
                        evilciseThread.Start();
                    }
                    break;

                case Items.maneater:
                    BoneRapier.BoneRapierEffect(false);

                    if (!maneaterThread.IsAlive)
                    {
                        maneaterThread = new Thread(new ThreadStart(ToanCurses.ManeaterEffect));
                        maneaterThread.Start();
                    }
                    break;

                case Items.tsukikage:
                    BoneRapier.BoneRapierEffect(false);

                    if (!tsukikageThread.IsAlive)
                    {
                        tsukikageThread = new Thread(new ThreadStart(Tsukikage.TsukikageEffect));
                        tsukikageThread.Start();
                    }

                    // Tsukikage also inherits Quick Draw (Small Sword lineage)
                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.SmallSwordEffect));
                        smallSwordThread.Start();
                    }
                    break;


                case Items.smallsword:
                    BoneRapier.BoneRapierEffect(false);

                    if (!smallSwordThread.IsAlive)
                    {
                        smallSwordThread = new Thread(new ThreadStart(SmallSword.SmallSwordEffect));
                        smallSwordThread.Start();
                    }
                    break;

                case Items.darkcloud:
                    BoneRapier.BoneRapierEffect(false);

                    if (!darkCloudThread.IsAlive)
                    {
                        darkCloudThread = new Thread(new ThreadStart(DarkCloud.DarkCloudEffect));
                        darkCloudThread.Start();
                    }
                    break;

                case Items.sunsword:
                    BoneRapier.BoneRapierEffect(false);

                    if (!sunSwordThread.IsAlive)
                    {
                        sunSwordThread = new Thread(new ThreadStart(SunSword.SunSwordEffect));
                        sunSwordThread.Start();
                    }
                    break;

                case Items.bigbang:   // inherits Solar Harvest (Sun Sword lineage) + its own Detonate
                    BoneRapier.BoneRapierEffect(false);

                    if (!sunSwordThread.IsAlive)
                    {
                        sunSwordThread = new Thread(new ThreadStart(SunSword.SunSwordEffect));
                        sunSwordThread.Start();
                    }
                    if (!bigBangThread.IsAlive)
                    {
                        bigBangThread = new Thread(new ThreadStart(BigBang.BigBangEffect));
                        bigBangThread.Start();
                    }
                    break;

                case Items.crosshinder:
                    BoneRapier.BoneRapierEffect(false);

                    if (!crossHinderThread.IsAlive)
                    {
                        crossHinderThread = new Thread(new ThreadStart(CrossHinder.CrossHinderEffect));
                        crossHinderThread.Start();
                    }
                    break;

                case Items.agassword:
                    BoneRapier.BoneRapierEffect(false);

                    if (!agasSwordThread.IsAlive)
                    {
                        agasSwordThread = new Thread(new ThreadStart(AgasSword.AgasSwordEffect));
                        agasSwordThread.Start();
                    }
                    break;

                case Items.braveark:
                    BoneRapier.BoneRapierEffect(false);

                    if (!braveArkThread.IsAlive)
                    {
                        braveArkThread = new Thread(new ThreadStart(BraveArk.BraveArkEffect));
                        braveArkThread.Start();
                    }
                    break;

                // Kitchen Knife is a TOAN sword — its effect gates on ToanId, so registering it under
                // Xiao (where it used to live) made it unreachable: Xiao can never equip a Toan sword,
                // so the thread never started, and the spring blessing could never fire.
                case Items.kitchenknife:
                    BoneRapier.BoneRapierEffect(false);

                    if (!kitchenKnifeThread.IsAlive)
                    {
                        kitchenKnifeThread = new Thread(new ThreadStart(KitchenKnife.KitchenKnifeEffect));
                        kitchenKnifeThread.Start();
                    }
                    break;

                default:
                    BoneRapier.BoneRapierEffect(false);
                    break;
            }
        }

        private static void Xiao()
        {
            // Super Steve manages the bone-door bypass itself (via an attached Bone Rapier / Bone Slingshot sphere); the Bone Slingshot has it below.
            if (Player.Weapon.GetCurrentWeaponId() != Items.supersteve && Player.Weapon.GetCurrentWeaponId() != Items.boneslingshot) BoneRapier.BoneRapierEffect(false);
            if (Dungeon.magicCircleChanged) CustomRubyEffects.SecretArmletDisable(); Dungeon.magicCircleChanged = false;

            // The lock-on movement buff: Dragon's Y's, and the three weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (LockOnSpeed.Grants(Player.Weapon.GetCurrentWeaponId()) && !lockOnSpeedThread.IsAlive)
            {
                lockOnSpeedThread = new Thread(new ThreadStart(CustomXiaoEffects.LockOnSpeedEffect));
                lockOnSpeedThread.Start();
            }
            // The lock-on reach: the Flamingo's, and the four weapons that inherit it (Super Steve's own loop drives its sphere's).
            if (Flamingo.GrantsReach(Player.Weapon.GetCurrentWeaponId()) && !lockOnReachThread.IsAlive)
            {
                lockOnReachThread = new Thread(new ThreadStart(CustomXiaoEffects.LockOnReachEffect));
                lockOnReachThread.Start();
            }
            switch (Player.Weapon.GetCurrentWeaponId())
            {

                case Items.angelgear:
                    if (!angelGearThread.IsAlive)
                    {
                        angelGearThread = new Thread(new ThreadStart(CustomXiaoEffects.AngelGearEffect));
                        angelGearThread.Start();
                    }
                    break;

                case Items.supersteve:
                    if (!superSteveThread.IsAlive)
                    {
                        superSteveThread = new Thread(new ThreadStart(CustomXiaoEffects.SuperSteveEffect));
                        superSteveThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)   // the bone key's no-revival, for a Bone Rapier / Bone Slingshot sphere (the thread checks)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.BoneKeyNoRevivalEffect));
                        boneNoRevivalThread.Start();
                    }
                    if (!crossHinderThread.IsAlive && CrossHinder.CrossHinderWielded())   // Sanctifier, for a Cross Hinder sphere
                    {
                        crossHinderThread = new Thread(new ThreadStart(CrossHinder.CrossHinderEffect));
                        crossHinderThread.Start();
                    }
                    break;

                case Items.matador:
                    if (!matadorThread.IsAlive)
                    {
                        matadorThread = new Thread(new ThreadStart(CustomXiaoEffects.MatadorEffect));
                        matadorThread.Start();
                    }
                    break;

                case Items.dragonsy:
                    if (!dragonsYThread.IsAlive)
                    {
                        dragonsYThread = new Thread(new ThreadStart(CustomXiaoEffects.DragonsYEffect));
                        dragonsYThread.Start();
                    }
                    break;

                case Items.doubleimpact:
                    if (!doubleImpactThread.IsAlive)
                    {
                        doubleImpactThread = new Thread(new ThreadStart(CustomXiaoEffects.DoubleImpactEffect));
                        doubleImpactThread.Start();
                    }
                    break;

                case Items.banditslingshot:
                    if (!banditSlingshotThread.IsAlive)
                    {
                        banditSlingshotThread = new Thread(new ThreadStart(CustomXiaoEffects.BanditSlingshotEffect));
                        banditSlingshotThread.Start();
                    }
                    break;

                case Items.boneslingshot:
                    // the skeleton key, the Bone Rapier's: bone doors open without their key, the undead stay down
                    BoneRapier.BoneRapierEffect(true);

                    if (!boneDoorThread.IsAlive)
                    {
                        boneDoorThread = new Thread(new ThreadStart(BoneRapier.BoneDoorTrigger));
                        boneDoorThread.Start();
                    }
                    if (!boneNoRevivalThread.IsAlive)
                    {
                        boneNoRevivalThread = new Thread(new ThreadStart(BoneRapier.BoneKeyNoRevivalEffect));
                        boneNoRevivalThread.Start();
                    }
                    break;

                case Items.steelslingshot:
                    if (!steelSlingshotThread.IsAlive)
                    {
                        steelSlingshotThread = new Thread(new ThreadStart(CustomXiaoEffects.SteelSlingshotEffect));
                        steelSlingshotThread.Start();
                    }
                    break;

                case Items.hardshooter:
                    if (!hardshooterThread.IsAlive)
                    {
                        hardshooterThread = new Thread(new ThreadStart(CustomXiaoEffects.HardshooterEffect));
                        hardshooterThread.Start();
                    }
                    break;

                default:
                    break;
            }
        }

        private static void Goro()
        {
            BoneRapier.BoneRapierEffect(false);
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
                        frozenTunaThread = new Thread(new ThreadStart(CustomGoroEffects.FrozenTunaEffect));
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
            BoneRapier.BoneRapierEffect(false);

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

                    if (!banditSlingshotThread.IsAlive)
                    {
                        banditSlingshotThread = new Thread(new ThreadStart(CustomXiaoEffects.BanditSlingshotEffect));
                        banditSlingshotThread.Start();
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
            BoneRapier.BoneRapierEffect(false);
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
                        cactusThread = new Thread(new ThreadStart(CustomUngagaEffects.CactusEffect));
                        cactusThread.Start();
                    }
                    break;
                default:
                    break;
            }
        }

        private static void Osmond()
        {
            BoneRapier.BoneRapierEffect(false);
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
                        starBreakerThread = new Thread(new ThreadStart(CustomOsmondEffects.StarBreakerEffect));
                        starBreakerThread.Start();
                    }
                    break;

                case Items.snail:
                    if (!snailThread.IsAlive)
                    {
                        snailThread = new Thread(new ThreadStart(CustomOsmondEffects.SnailEffect));
                        snailThread.Start();
                    }
                    break;

                case Items.skunk:
                    if (!skunkThread.IsAlive)
                    {
                        skunkThread = new Thread(new ThreadStart(CustomOsmondEffects.SkunkEffect));
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
                wiseOwlSwordThread = new Thread(new ThreadStart(WiseOwlSword.WiseOwlSwordEffect));
                wiseOwlSwordThread.Start();
            }
        }
    }
}
