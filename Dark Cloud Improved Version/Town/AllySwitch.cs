using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Overworld ally switching. The game reads the playable character's .chr path and .cfg name from two
    /// relocated string slots (<see cref="Addresses.chrFileLocation"/> / <see cref="Addresses.chrConfigFileLocation"/>;
    /// the code offset at <see cref="Addresses.chrConfigFileOffset"/> points the loader at them). This class owns
    /// those strings: it seeds them with Toan at start-up, rewrites the .chr path while the player cycles the
    /// Allies menu (and writes the current character back when the menu closes without a switch), and swaps
    /// back to Toan when the player leaves an area so an area-entry cutscene never runs on an ally rig.
    /// </summary>
    internal static class AllySwitch
    {
        private const string Tag = "[AllySwitch] ";

        /// <summary>Path-text alphabet: a character's game byte is its index here + 32.</summary>
        static char[] characters = { ' ', '!', '"', '#', '$', '%', '&', '`', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?', '@',
                              'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '§', ']', '^', '_', '`',
                              'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~' };

        static int currentAddress;
        static byte[] value1 = new byte[1];
        static string cfgFile;
        static string chrFilePath;
        static string currentCharacter = "chara/c01d.chr";

        static bool menuExited = true;
        static bool changingLocation;
        static int charNumber;
        static int prevCharNumber = 255;
        static int allyCount;

        /// <summary>
        /// Points the game's .cfg loader at the relocated slot, clears both slots and writes "info.cfg" and
        /// Toan's "chara/c01d.chr" into them. Run at game entry (SessionController) and again at game-loop start.
        /// </summary>
        public static void InitializeCharacterOffsetValues()
        {
            //This process is required at the start of the game to edit the memory offsets for the character file switching
            //The game reads the character file (for example Xiao's model and such) from a specific address,
            //but this location needs to be moved, so that we can apply longer character file paths

            Memory.Write(Addresses.chrConfigFileOffset, BitConverter.GetBytes(608545264)); //this changes the offset value in game's code to make it read the file in right location

            currentAddress = Addresses.chrConfigFileLocation;

            for (int i = 0; i < 15; i++)    //clear the previous values in config file location
            {
                Memory.WriteByte(currentAddress, 0);
                currentAddress += 0x00000001;
            }

            currentAddress = 0x2029AA18;

            for (int i = 0; i < 9; i++)    //clear the previous values in config file location
            {
                Memory.WriteByte(currentAddress, 0);
                currentAddress += 0x00000001;
            }

            cfgFile = "info.cfg";

            currentAddress = Addresses.chrConfigFileLocation;

            for (int i = 0; i < cfgFile.Length; i++)
            {
                char character = cfgFile[i];

                for (int a = 0; a < characters.Length; a++)
                {
                    if (character.Equals(characters[a]))
                    {
                        value1 = BitConverter.GetBytes(a + 32);
                    }
                }

                Memory.WriteByte(currentAddress, value1[0]);
                currentAddress += 0x00000001;
            }



            chrFilePath = "chara/c01d.chr"; //the path to the character file that should be loaded

            currentAddress = Addresses.chrFileLocation;

            for (int i = 0; i < chrFilePath.Length; i++)
            {
                char character = chrFilePath[i];

                for (int a = 0; a < characters.Length; a++)
                {
                    if (character.Equals(characters[a]))
                    {
                        value1 = BitConverter.GetBytes(a + 32);
                    }
                }
                Memory.WriteByte(currentAddress, value1[0]);
                currentAddress += 0x00000001;
            }
        }

        /// <summary>
        /// Town tick, Allies menu: while the Allies page of the pause menu is open, every cursor move writes the
        /// highlighted ally's .chr path into the slot; when the menu closes without a switch the character that
        /// was loaded before is written back.
        /// </summary>
        internal static void TickAlliesMenu()
        {
            if (Memory.ReadByte(Addresses.mode) == 0x2 && Memory.ReadByte(Addresses.selectedMenu) == 0x3) //check if player entered allies menu in the overworld
            {

                if (Memory.ReadInt(0x202A28F4) == 0)    //set currentCharacter variable after cycling through allies
                {
                    currentCharacter = chrFilePath;
                }

                charNumber = Memory.ReadByte(0x21D90470);

                if (prevCharNumber != charNumber) //whenever player cycles through the allies, the correct character file will be determined
                {
                    allyCount = Memory.ReadByte(0x21CD9551);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + charNumber);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "different char");
                    currentAddress = Addresses.chrFileLocation;

                    for (int i = 0; i < 30; i++) //clears the memory so the character file name wont be broken
                    {
                        Memory.WriteByte(currentAddress, 0);
                        currentAddress += 0x00000001;
                    }


                    switch (charNumber) //gets character file path based on selected ally
                    {
                        case 0:
                            chrFilePath = "chara/c01d.chr";
                            break;
                        case 1:
                            chrFilePath = "gedit/e01/chara/c04pcat.chr";
                            break;
                        case 2:
                            chrFilePath = "gedit/s01/chara/c06p.chr";
                            break;
                        case 3:
                            chrFilePath = "gedit/e03/chara/c05a.chr";
                            break;
                        case 4:
                            chrFilePath = "gedit/s79/chara/c10a.chr";
                            break;
                        case 5:
                            chrFilePath = "gedit/e05/chara/c18p.chr";
                            break;
                        default:
                            chrFilePath = "chara/c01d.chr";
                            break;
                    }

                    currentAddress = Addresses.chrFileLocation;

                    for (int i = 0; i < chrFilePath.Length; i++) //writes character file path to the memory (USES OLD METHOD, SHOULD BE STRING ARRAY)
                    {
                        char character = chrFilePath[i];

                        for (int a = 0; a < characters.Length; a++)
                        {
                            if (character.Equals(characters[a]))
                            {
                                value1 = BitConverter.GetBytes(a + 32);
                            }
                        }

                        Memory.WriteByte(currentAddress, value1[0]);

                        currentAddress += 0x00000001;
                    }


                    prevCharNumber = charNumber;
                    menuExited = false;
                }
            }
            else if (menuExited == false && Memory.ReadByte(0x202A1E90) == 255)   //if player exits allies menu without switching character, write the current character back
            {
                chrFilePath = currentCharacter;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "re-writing current character back...");

                currentAddress = Addresses.chrFileLocation;

                for (int i = 0; i < chrFilePath.Length; i++)
                {
                    char character = chrFilePath[i];

                    for (int a = 0; a < characters.Length; a++)
                    {
                        if (character.Equals(characters[a]))
                        {
                            value1 = BitConverter.GetBytes(a + 32);
                        }
                    }

                    Memory.WriteByte(currentAddress, value1[0]);
                    currentAddress += 0x00000001;
                }
                menuExited = true;
                prevCharNumber = 99;
            }
        }

        /// <summary>
        /// Town tick, area exit: when the next-map word leaves its idle value the .chr slot is rewritten to Toan
        /// (an area-entry cutscene may play), event points are re-enabled, and the tick waits ~350 ms plus up to
        /// 1.25 s for the screen brightness to recover before clearing the clock mailbox. The latch releases once
        /// the next-map word reads idle again.
        /// </summary>
        internal static void TickLocationChange()
        {
            if (Memory.ReadByte(0x202A1E90) != 255 && Memory.ReadUShort(0x202A1E90) != 1000 && changingLocation == false)  //if changing location, swap back to Toan
            {
                changingLocation = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "changing location");
                chrFilePath = "chara/c01d.chr";
                Memory.WriteByte(Mailbox.EventPoint, 0); //re-enable eventpoints in case they were disabled'

                //when player is about to enter another area, a cutscene might play. We switch back to Toan to prevent any character models from breaking

                currentAddress = Addresses.chrFileLocation;

                for (int i = 0; i < 30; i++)
                {
                    Memory.WriteByte(currentAddress, 0);
                    currentAddress += 0x00000001;
                }

                currentAddress = Addresses.chrFileLocation;

                for (int i = 0; i < chrFilePath.Length; i++)
                {
                    char character = chrFilePath[i];

                    for (int a = 0; a < characters.Length; a++)
                    {
                        if (character.Equals(characters[a]))
                        {
                            value1 = BitConverter.GetBytes(a + 32);
                        }
                    }

                    Memory.WriteByte(currentAddress, value1[0]);
                    currentAddress += 0x00000001;
                }

                Thread.Sleep(350);

                if (Memory.ReadByte(0x21D2DA4C) < 61) //sometimes when teleporting from yellow drops or dark heaven, the area might turn dark. This part tries to prevent it
                {
                    int timerCheck = 0;
                    while (Memory.ReadByte(0x21D2DA4C) < 58 && timerCheck <= 25)
                    {
                        Thread.Sleep(50);
                        timerCheck++;
                    }
                }
                Memory.WriteByte(Mailbox.Clock, 0);
            }

            if (Memory.ReadByte(0x202A1E90) == 255) //not 100% sure about this value, but it should be static while the player is not in the process of switching areas
            {
                changingLocation = false;
            }
        }
    }
}
