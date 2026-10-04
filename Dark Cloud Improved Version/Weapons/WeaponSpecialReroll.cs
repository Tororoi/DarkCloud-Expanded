using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The weapon special attributes (<see cref="WeaponTable.Effect1"/> / <see cref="WeaponTable.Effect2"/>) the mod lets
    /// certain weapons carry, rerolled in the static weapon table once a second for as long as a save is in play (so each weapon
    /// the game builds from the table rolls its own): the thread body SessionController.weaponspecialeffectThread runs.</summary>
    internal static class WeaponSpecialReroll
    {
        static Random rnd = new Random();

        /// <summary>Process to roll the new weapon special attributes on weapons that now may have them; returns once the
        /// game has left the in-game modes.</summary>
        public static void Run()
        {
            while (true)
            {
                if (SessionController.userMode == true)
                {
                    if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                    {
                        Thread.Sleep(100);

                        if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Not ingame anymore! Exited from WeaponRerollEffectsThread!");
                            break;
                        }
                    }
                }

                //Base weapon special effects (Set 1); (ALSO RUNTIME) - 2=Big bucks, 4=poor, 8=quench, 16=thirst, 32=poison, 64=stop, 128=steal
                //Base weapon special effects (Set 2); (ALSO RUNTIME) - 1=fragile, 2=durable, 4=drain, 8=heal, 16=critical, 32=absup

                /*********************
                 *   Heavens Cloud   *
                 *********************/

                int attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets attribute
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which attribute it gets
                    {
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 32);
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 16);
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 0);
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.heavenscloud - WeaponTable.DaggerId))), 0);
                }


                /**********************
                 *     Dark Cloud     *
                 **********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.darkcloud - WeaponTable.DaggerId))), 32);
                    }
                    else
                    {
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.darkcloud - WeaponTable.DaggerId))), 64);
                    }
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.darkcloud - WeaponTable.DaggerId))), 0);
                }

                /*********************
                 *      Big Bang     *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 16);
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 64);
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 0);
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.bigbang - WeaponTable.DaggerId))), 0);
                }

                /************************
                 *   Atlamillia Sword   *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 8);
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 64);
                        Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 0);
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.Stride * (Items.atlamilliasword - WeaponTable.DaggerId))), 0);
                }

                /*********************
                 *       Dusack      *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.dusack - WeaponTable.DaggerId))), 128);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.Stride * (Items.dusack - WeaponTable.DaggerId))), 0);
                }

                /************************
                 *    Bone Slingshot    *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.boneslingshot - WeaponTable.WoodenSlingshotId)))), 1);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.boneslingshot - WeaponTable.WoodenSlingshotId)))), 0);
                }

                /*********************
                 *    Hardshooter    *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.hardshooter - WeaponTable.WoodenSlingshotId)))), 1);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.XiaoOffset + (WeaponTable.Stride * (Items.hardshooter - WeaponTable.WoodenSlingshotId)))), 0);
                }

                /**********************
                 *    Goddess Ring    *
                 **********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.goddessring - WeaponTable.GoldRingId)))), 8);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.goddessring - WeaponTable.GoldRingId)))), 0);
                }

                /************************
                 *   Destruction Ring   *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.destructionring - WeaponTable.GoldRingId)))), 16);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.destructionring - WeaponTable.GoldRingId)))), 0);
                }

                /*********************
                 *    Satans Ring    *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.satansring - WeaponTable.GoldRingId)))), 4);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.satansring - WeaponTable.GoldRingId)))), 0);
                }

                /*********************
                 *   Thorn Armlet   *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 32); //Poison
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.RubyOffset + (WeaponTable.Stride * (Items.thornarmlet - WeaponTable.GoldRingId)))), 0);
                }

                /*********************
                 *      De Sanga     *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 30)
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (Items.desanga - WeaponTable.FightingStickId)))), 4);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect2 + (WeaponTable.UngagaOffset + (WeaponTable.Stride * (Items.desanga - WeaponTable.FightingStickId)))), 0);
                }

                /*********************
                 *       Skunk       *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.skunk - WeaponTable.MachineGunId)))), 32);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.skunk - WeaponTable.MachineGunId)))), 0);
                }

                /*********************
                 *      Swallow      *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.swallow - WeaponTable.MachineGunId)))), 128);
                }
                else
                {
                    Memory.WriteByte((WeaponTable.Effect1 + (WeaponTable.OsmondOffset + (WeaponTable.Stride * (Items.swallow - WeaponTable.MachineGunId)))), 0);
                }

                Thread.Sleep(1000);
            }
        }
    }
}
