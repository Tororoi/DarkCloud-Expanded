using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The mod's weapon-stat rebalance: the per-weapon base stats, elements, slayer values, abilities, build-up branches
    /// and attachment slots it changes in the engine's static weapon table (<see cref="WeaponTable"/>), written once at startup
    /// (MainMenuThread.ApplyNewChanges) and skipped when the Baselard's Endurance already reads the rebalanced 30.</summary>
    internal static class WeaponBalance
    {
        /// <summary>Applies all the weapon changes to their base values (runs once when starting the mod).</summary>
        public static void Apply()
        {
            if (Memory.ReadUShort(WeaponTable.Endurance + (WeaponTable.Stride * (Items.baselard - WeaponTable.DaggerId))) != 30) //check if changes have already applied
            {

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Applying the new weapon changes...");


                /****************************************
                 *               TOAN                   *
                 ****************************************/

                //Baselard
                Memory.WriteUShort(WeaponTable.Endurance + (WeaponTable.Stride * (Items.baselard - WeaponTable.DaggerId)), 30);   //Endurance set to 30

                //Antique Sword
                Memory.WriteUShort(WeaponTable.Speed + (WeaponTable.Stride * (Items.antiquesword - WeaponTable.DaggerId)), 70);   //Speed set to 70
                Memory.WriteUShort(WeaponTable.Fire + (WeaponTable.Stride * (Items.antiquesword - WeaponTable.DaggerId)), 15);    //Fire set to 15

                //Kitchen Knife
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 50);          //Whp set to 50
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 25);       //Attack set to 25
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 30);    //Endurance set to 30
                Memory.WriteUShort((WeaponTable.Ice + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 0);           //Ice set to 0
                Memory.WriteUShort((WeaponTable.Thunder + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 8);       //Thunder set to 8
                Memory.WriteUShort((WeaponTable.SeaKiller + (WeaponTable.Stride * (Items.kitchenknife - WeaponTable.DaggerId))), 90);          //Sea Killer set to 90

                //Tsukikage
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.tsukikage - WeaponTable.DaggerId))), 33);    //Endurance set to 33
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.tsukikage - WeaponTable.DaggerId))), 80);        //Speed set to 80

                //Macho Sword
                Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.machosword - WeaponTable.DaggerId))), 32);  //Adds ABS up effect

                //Evilcise
                Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.evilcise - WeaponTable.DaggerId))), 4);    //Poor

                //Aga's Sword
                Memory.WriteUShort((WeaponTable.BuildUp   + (WeaponTable.Stride * (Items.agassword - WeaponTable.DaggerId))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.Stride * (Items.agassword - WeaponTable.DaggerId))), 190); //Max attack set to 190

                //Heaven's Cloud
                Memory.WriteUShort((WeaponTable.Synth3 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 1);    //Adds a 3rd regular attachment slot
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 180); //Max attack set to 180
                Memory.WriteUShort((WeaponTable.MaxMagic  + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 180); //Max magic set to 180

                //Lamb's Sword
                Memory.WriteUShort((WeaponTable.Synth3 + (WeaponTable.Stride * (Items.lambsswordnormal - WeaponTable.DaggerId))), 1);    //Adds a 3rd regular attachment slot
                Memory.WriteDouble(WeaponTable.LambTransformThreshold, 0.5);    //Change the percent limit for when the sword should transform
                Memory.WriteFloat(WeaponTable.LambStatsThreshold, (float)0.5); //Change the percent limit for when the sword stats should upgrade

                //Brave Ark
                Memory.WriteUShort((WeaponTable.Synth3 + (WeaponTable.Stride * (Items.braveark - WeaponTable.DaggerId))), 1);    //Adds a 3rd regular attachment slot

                //Big Bang
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 70);    //Speed set to 70

                //Small Sword
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.smallsword - WeaponTable.DaggerId))), 35);      //Whp set to 35
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.Stride * (Items.smallsword - WeaponTable.DaggerId))), 17);    //Magic set to 17
                Memory.WriteUShort((WeaponTable.SeaKiller + (WeaponTable.Stride * (Items.smallsword - WeaponTable.DaggerId))), 0);       //Sea Killer set to 0
                Memory.WriteUShort((WeaponTable.MetalBreaker + (WeaponTable.Stride * (Items.smallsword - WeaponTable.DaggerId))), 10);    //Metal Breaker set to 10

                //Sand Breaker
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.sandbreaker - WeaponTable.DaggerId))), 45);          //Whp set to 45
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.sandbreaker - WeaponTable.DaggerId))), 25);    //Endurance set to 25
                Memory.WriteUShort((WeaponTable.Synth3 + (WeaponTable.Stride * (Items.sandbreaker - WeaponTable.DaggerId))), 1);        //Adds a 3rd regular attachment slot

                //Drain Seeker
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.drainseeker - WeaponTable.DaggerId))), 60); //Whp set to 60

                //Chopper
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.chopper - WeaponTable.DaggerId))), 60); //Speed set to 60

                //Choora
                Memory.WriteUInt((WeaponTable.BuildUp + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 2147483648); //Build-up to Maneater only
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 57);      //Whp set to 57
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 45);   //Attack set to 45
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 70);    //Speed set to 70
                Memory.WriteUShort((WeaponTable.Ice + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 10);      //Ice set to 10
                Memory.WriteUShort((WeaponTable.Thunder + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 15);  //Thunder set to 15
                Memory.WriteUShort((WeaponTable.UndeadBuster + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 15);   //Undead Buster set to 15
                Memory.WriteUShort((WeaponTable.BeastBuster + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 15);    //Beaster Buster set to 15
                Memory.WriteUShort((WeaponTable.MetalBreaker + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 15);    //Metal Breaker set to 15
                Memory.WriteUShort((WeaponTable.Synth3 + (WeaponTable.Stride * (Items.choora - WeaponTable.DaggerId))), 1);    //Adds a 3rd regular attachment slot

                //Claymore
                Memory.WriteUShort((WeaponTable.UndeadBuster + (WeaponTable.Stride * (Items.claymore - WeaponTable.DaggerId))), 10);   //Undead Buster set to 10
                Memory.WriteUShort((WeaponTable.BeastBuster + (WeaponTable.Stride * (Items.claymore - WeaponTable.DaggerId))), 10);    //Beaster Buster set to 10
                Memory.WriteUShort((WeaponTable.MageSlayer + (WeaponTable.Stride * (Items.claymore - WeaponTable.DaggerId))), 10);     //Mage Slayer set to 10

                //Maneater
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 44);    //Endurance set to 44
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 70);        //Speed set to 70
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 45);        //Magic set to 45
                Memory.WriteUShort((WeaponTable.Ice + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);          //Ice set to 15
                Memory.WriteUShort((WeaponTable.Thunder + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);      //Thunder set to 15
                Memory.WriteUShort((WeaponTable.Holy + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);         //Holy set to 15
                Memory.WriteUShort((WeaponTable.UndeadBuster + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);       //Undead Buster set to 15
                Memory.WriteUShort((WeaponTable.BeastBuster + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);        //Beast Buster set to 15
                Memory.WriteUShort((WeaponTable.MetalBreaker + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 15);        //Metal Breaker set to 15
                Memory.WriteUShort((WeaponTable.MimicBreaker + (WeaponTable.Stride * (Items.maneater - WeaponTable.DaggerId))), 10);        //Mimic Breaker set to 10

                //Bone Rapier
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.bonerapier - WeaponTable.DaggerId))), 38);      //Whp set to 38
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.Stride * (Items.bonerapier - WeaponTable.DaggerId))), 26);    //Magic set to 26

                //Sax
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.sax - WeaponTable.DaggerId))), 60);    //Speed set to 60
                Memory.WriteUShort((WeaponTable.Fire + (WeaponTable.Stride * (Items.sax - WeaponTable.DaggerId))), 6);      //Fire set to 6
                Memory.WriteUShort((WeaponTable.SkyHunter + (WeaponTable.Stride * (Items.sax - WeaponTable.DaggerId))), 10);      //Sky Hunter set to 10

                //7 Branch Sword
                Memory.WriteUShort((WeaponTable.Whp + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 47);          //Whp set to 47
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 47);    //Endurance set to 47
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 37);        //Magic set to 37
                Memory.WriteUShort((WeaponTable.DinoSlayer + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);    //Dino Slayer set to 7
                Memory.WriteUShort((WeaponTable.UndeadBuster + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);        //Undead Buster set to 7
                Memory.WriteUShort((WeaponTable.SeaKiller + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);           //Sea Killer set to 7
                Memory.WriteUShort((WeaponTable.StoneBreaker + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);         //Stone Breaker set to 7
                Memory.WriteUShort((WeaponTable.PlantBuster + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);         //Plant Buster set to 7
                Memory.WriteUShort((WeaponTable.BeastBuster + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 8);         //Beast Buster set to 8
                Memory.WriteUShort((WeaponTable.SkyHunter + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);           //Sky Killer set to 7
                Memory.WriteUShort((WeaponTable.MetalBreaker + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 10);        //Metal Breaker set to 10
                Memory.WriteUShort((WeaponTable.MimicBreaker + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 7);         //Mimic Breaker set to 7
                Memory.WriteUShort((WeaponTable.MageSlayer + (WeaponTable.Stride * (Items.sevenbranchsword - WeaponTable.DaggerId))), 8);          //Mage Slayer set to 8

                //Cross Hinder
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.Stride * (Items.crosshinder - WeaponTable.DaggerId))), 50);    //Endurance set to 50
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.Stride * (Items.crosshinder - WeaponTable.DaggerId))), 70);        //Speed set to 70
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.Stride * (Items.crosshinder - WeaponTable.DaggerId))), 32);        //Magic set to 32

                //Chronicle 2
                Memory.WriteUShort(WeaponTable.MaxAttack + (WeaponTable.Stride * (Items.chronicletwo - WeaponTable.DaggerId)), 999); //Max Attack set to 999




                /****************************************
                 *               XIAO                   *
                 ****************************************/

                //Wooden Slingshot
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.woodenslingshot - WeaponTable.WoodenSlingshotId)))), 6); //Attack set to 6
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.woodenslingshot - WeaponTable.WoodenSlingshotId)))), 2);  //Magic set to 2
                Memory.WriteUShort((WeaponTable.Fire + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.woodenslingshot - WeaponTable.WoodenSlingshotId)))), 4);   //Fire set to 4

                //Bandit Slingshot
                // Memory.WriteUInt((WeaponTable.BuildUp + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.banditslingshot - WeaponTable.WoodenSlingshotId)))), 128); //Sets build-up to Double Impact only

                //Bone Slingshot
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.boneslingshot - WeaponTable.WoodenSlingshotId)))), 11);    //Attack set to 11
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.boneslingshot - WeaponTable.WoodenSlingshotId)))), 30); //Endurance set to 30

                //Hardshooter
                Memory.WriteUShort((WeaponTable.Speed + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.hardshooter - WeaponTable.WoodenSlingshotId)))), 60); //Speed set to 60

                //Matador
                Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.matador - WeaponTable.WoodenSlingshotId)))), 16); //Adds Critical effect




                /****************************************
                 *               Goro                   *
                 ****************************************/

                //Turtle Shell
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.turtleshell - WeaponTable.MalletId)))), 10); //Magic set to 10

                //Big Bucks Hammer
                Memory.WriteUInt((WeaponTable.BuildUp + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.bigbuckshammer - WeaponTable.MalletId)))), 8); //Sets build-up branch to Magical Hammer only

                //Frozen Tuna
                Memory.WriteUShort((WeaponTable.Whp       + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 65);  //Whp set to 65
                Memory.WriteUShort((WeaponTable.BuildUp   + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 100); //Max attack set to 100
                Memory.WriteUShort((WeaponTable.MaxMagic  + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 678); //Max MP set to 678
                Memory.WriteByte  ((WeaponTable.Effect1    + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 64);  //Stop
                Memory.WriteUShort((WeaponTable.Synth4    + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.frozentuna - WeaponTable.MalletId)))), 1);   //Adds a 4th regular attachment slot

                //Gaia Hammer
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.gaiahammer - WeaponTable.MalletId)))), 25); //Endurance set to 25

                //Trial Hammer
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.trialhammer - WeaponTable.MalletId)))), 30);    //Attack set to 30
                Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.GoroOffset + (WeaponTable.Stride * (Items.trialhammer - WeaponTable.MalletId)))), 25); //Endurance set to 25




                /****************************************
                 *               Ruby                   *
                 ****************************************/

                //Gold Ring
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.goldring - WeaponTable.GoldRingId)))), 15);  //Attack set to 15
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.goldring - WeaponTable.GoldRingId)))), 30);   //Magic set to 30

                //Bandit's Ring
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.banditsring - WeaponTable.GoldRingId)))), 30);      //Attack set to 30
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.banditsring - WeaponTable.GoldRingId)))), 50);   //Max Attack set to 50
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.banditsring - WeaponTable.GoldRingId)))), 20);       //Magic set to 20
                // Memory.WriteInt((WeaponTable.BuildUp + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.banditsring - WeaponTable.GoldRingId)))), 8200);      //Sets build-up branches to both Crystal Ring and Thorn Armlet

                //Platinum Ring
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.platinumring - WeaponTable.GoldRingId)))), 23); //Attack set to 23

                //Pocklekul
                Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.pocklekul - WeaponTable.GoldRingId)))), 28);      //Attack set to 28
                Memory.WriteUShort((WeaponTable.Magic + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.pocklekul - WeaponTable.GoldRingId)))), 28);       //Magic set to 28
                Memory.WriteUShort((WeaponTable.Holy + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.pocklekul - WeaponTable.GoldRingId)))), 0);         //Holy set to 0
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.pocklekul - WeaponTable.GoldRingId)))), 8256);   //Sets build-up branches to both Fairy Ring and Thorn Armlet

                //Thorn Armlet
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 90);   //Max Attack set to 90
                Memory.WriteUShort((WeaponTable.MaxMagic + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 72);  //Max Magic set to 72
                Memory.WriteUShort((WeaponTable.StoneBreaker + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 20);     //Stone Breaker set to 20
                Memory.WriteUShort((WeaponTable.BeastBuster + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 20);     //Beast Buster set to 20
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 128);  //Sets build-up branches to Destruction Ring

                //Athenas Armlet
                Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.athenasarmlet - WeaponTable.DaggerId)))), 32);     //Adds ABS up effect




                /****************************************
                 *               Ungaga                 *
                 ****************************************/

                for (int ungagaweaponid = 348; ungagaweaponid <= 360; ungagaweaponid++)
                {
                    if (ungagaweaponid != 357)
                    {
                        int CurrWeaponAttack = Memory.ReadUShort((WeaponTable.Attack + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))));          //Reads the current weapon Attack value
                        int CurrWeaponMaxAttack = Memory.ReadUShort((WeaponTable.MaxAttack + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))));    //Reads the current weapon Max Attack value
                        int CurrWeaponEndurance = Memory.ReadUShort((WeaponTable.Endurance + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))));    //Reads the current weapon Endurance value

                        Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))), (ushort)(CurrWeaponAttack + 10));       //Adds +10 Attack to the current weapon being looped through
                        Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))), (ushort)(CurrWeaponMaxAttack + 10)); //Adds +10 Max Attack to the current weapon being looped through
                        Memory.WriteUShort((WeaponTable.Endurance + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (ungagaweaponid - WeaponTable.FightingStickId)))), (ushort)(CurrWeaponEndurance + 15)); //Adds +15 Endurance to the current weapon being looped through
                    }
                }

                //Babel Spear
                Memory.WriteUShort((WeaponTable.Synth4 + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (Items.babelsspear - WeaponTable.FightingStickId)))), 1); //Adds a 4th regular attachment slot




                /****************************************
                 *               Osmond                 *
                 ****************************************/

                for (int osmondweaponid = Items.machinegun; osmondweaponid <= Items.swallow; osmondweaponid++)
                {
                    int CurrWeaponAttack = Memory.ReadUShort((WeaponTable.Attack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (osmondweaponid - WeaponTable.MachineGunId)))));         //Reads the current weapon Attack value
                    int CurrWeaponMaxAttack = Memory.ReadUShort((WeaponTable.MaxAttack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (osmondweaponid - WeaponTable.MachineGunId)))));   //Reads the current weapon Max Attack value

                    Memory.WriteUShort((WeaponTable.Attack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (osmondweaponid - WeaponTable.MachineGunId)))), (ushort)(CurrWeaponAttack + 15));      //Adds +15 Attack to the current weapon being looped through
                    Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (osmondweaponid - WeaponTable.MachineGunId)))), (ushort)(CurrWeaponMaxAttack + 15)); //Adds +15 Max Attack to the current weapon being looped through
                }

                //Jackal
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.jackal - WeaponTable.MachineGunId)))), 4096);    //Build-up to Swallow only

                //Snail
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.snail - WeaponTable.MachineGunId)))), 256);     //Build-up to Hexa Blaster only

                //Blessing Gun
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.blessinggun - WeaponTable.MachineGunId)))), 87);   //Blessing Gun max attack set to 87
                Memory.WriteUShort((WeaponTable.MaxMagic  + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.blessinggun - WeaponTable.MachineGunId)))), 80);   //Blessing Gun max magic set to 80

                //Skunk
                Memory.WriteUShort((WeaponTable.MaxAttack + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.skunk - WeaponTable.MachineGunId)))), 143);        //Skunk max attack set to 143
                Memory.WriteUShort((WeaponTable.MaxMagic  + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.skunk - WeaponTable.MachineGunId)))), 105);         //Skunk max magic set to 105
                Memory.WriteUShort((WeaponTable.BuildUp + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.skunk - WeaponTable.MachineGunId)))), 0);       //No buildup paths (final form)


                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Finished applying new weapon changes!");

            }
            else Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "New weapon changes have already been applied!");
        }
    }
}
