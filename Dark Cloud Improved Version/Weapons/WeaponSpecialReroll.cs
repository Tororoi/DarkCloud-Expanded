using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The weapon special attributes (<see cref="WeaponTable.Effect1"/> / <see cref="WeaponTable.Effect2"/>) the mod lets
    /// certain weapons carry at a CHANCE, rerolled in the static weapon table once a second for as long as a save is in play (so each
    /// weapon the game builds from the table rolls its own): the thread body SessionController.weaponspecialeffectThread runs.
    /// Abilities a weapon always has (Macho Sword's ABS Up, Goddess Ring's Heal, DeSanga's Drain, …) are written once by
    /// <see cref="WeaponBalance"/> instead.</summary>
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

                /************************
                 *    Bone Slingshot    *
                 ************************/

                int attributeRoll = rnd.Next(100);

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

                Thread.Sleep(1000);
            }
        }
    }
}
