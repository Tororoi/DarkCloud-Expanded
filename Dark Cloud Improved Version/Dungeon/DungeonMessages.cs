using System;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The dungeon message window: the mod's own text shown in the game's dungeon notice box (message 10 of the
    /// dungeon bank, or the floor-clear slot), timed and queued so callers never fight over the window. Every feature that
    /// speaks in a dungeon goes through <see cref="DisplayMessage"/>; the text lands where <see cref="DungeonMessageBank"/>
    /// says the bank is this boot.</summary>
    internal static class DungeonMessages
    {
        public static Thread messageThread;
        static byte[] dungeonMessage;

        /// <summary>
        /// Processes the dungeon message information and displays it onscreen
        /// </summary>
        /// <param name="message">The contents of the message.</param>
        /// <param name="height">The height of the message window. Each value represents a line, ie 2 = paragrah with 2 lines.</param>
        /// <param name="width">The width of the message window. Each value represents a character in the string, ie 24 = 24 characters wide.</param>
        /// <param name="displayTime">The amount of time to display the message. Keep in mind there is a 5 second timeout threshold in place.</param>
        /// <returns>An array of bytes with the output message.</returns>
        public static void DisplayMessage(string message, int height = 4, int width = 40, int displayTime = 8000, bool isFloorClearMessage = false)
        {
            //Convert miliseconds to frames per second
            displayTime = (int)System.Math.Round(displayTime / 16.7f);

            messageThread = new Thread(() => DisplayMessageProcess(message, height, width, displayTime, isFloorClearMessage));
            messageThread.Start();
        }

        /// <summary>
        /// Returns true if no message is being displayed on screen, await a set amount of time if it is.
        /// </summary>
        /// <param name="timeout">Set a timeout in miliseconds (Default is 8 seconds)</param>
        /// <returns></returns>
        internal static bool CheckDisplayMessageAvailable(int timeout = 8000)
        {
            int ms;

            //Check if a dungeon message is displaying / player is on chest opening state
            if ((Memory.ReadInt(Addresses.dunMessage) != -1
                && Memory.ReadInt(Addresses.dunMessage) != 171) //Thirst Message
                || Memory.ReadInt(Addresses.dunItemMessage) != -1
                || (Memory.ReadByte(Addresses.dungeonDebugMenu) == 121 //big chest opening state
                || Memory.ReadByte(Addresses.dungeonDebugMenu) == 131)) //small chest opening state
            {
                //Reset timer
                ms = 0;

                //Wait for whatever message is currently displaying
                while (Memory.ReadInt(Addresses.dunMessage) != -1 && ms < timeout)
                {
                    Thread.Sleep(100);
                    ms += 100;
                    continue;
                }
                //if(ms < timeout) return true; else return false;
                return true;
            }
            else return true;
        }

        static byte[] DisplayMessageProcess(string message, int height, int width, int displayTime, bool isFloorClearMessage)
        {
            while (!CheckDisplayMessageAvailable())
            {
                continue;
            }

            // WHERE the text goes is resolved from the live message bank, never hardcoded. The dungeon carves its pools out
            // of one buffer in order, so any change to the character heap moves this bank (the mod's bigger heap moves it
            // 880,512 B); a write to a captured address lands on the `gaiji` glyph sheet instead — a band of message text
            // struck through the letters in every dungeon window. See DungeonMessageBank.
            long messageAddress = DungeonMessageBank.TextAddress(isFloorClearMessage ? DungeonMessageBank.FloorClearId
                                                                                     : DungeonMessageBank.CustomId);
            if (messageAddress == 0) return new byte[0];   // bank unreadable: drop the message rather than write over whatever is there

            byte[] customMessage = Encoding.GetEncoding(10000).GetBytes(message);
            dungeonMessage = Memory.ReadByteArray(messageAddress, message.Length);

            byte[] outputMessage = new byte[customMessage.Length * 2];

            byte[] normalCharTable =
            {0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
            0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, //A-Z

            0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
            0x70, 0x71, 0x72, 0x73, 0x74 ,0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, //a-z

            //0     1     2     3     4     5     6     7     8     9
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,

            //'     =     "     !     ?     #     &     +     -     *     (     )    @     |     ^
            0x27, 0x3D, 0x22, 0x21, 0x3F, 0x23, 0x26, 0x2B, 0x2D, 0x2A, 0x28, 0x29, 0x40, 0x7C, 0x5E,

            //<     >     {    }     [     ]
            0x3C, 0x3E, 0x7B, 0x7D, 0x5B, 0x5D,

            //.    $     \n    SPC
            0x2E, 0x24,  0x0A, 0x20,

            //Cross     Circle
              0x8,       0x6,
            };

            byte[] dcCharTable =
            {0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, //A-Z

            0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
            0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, //a-z

            //0     1     2     3     4     5     6     7     8     9
            0x6F, 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,

            //'     =     "     !     ?     #     &     +     -     *     (     )     @    |     ^
            0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x61, 0x62, 0x63, 0x64, 0xFF, //Just needed for detection, doesn't matter what this is

            //<     >     {    }     [      ]
            0x65, 0x66, 0x67, 0x68, 0x69, 0x6A,

            //.     $    \n    SPC
            0x6D, 0x6E, 0x00, 0x02,

            //Cross     Circle
              0x8,       0x6,
            };


            //Initialize outputMessage to 0xFD
            for (int i = 0; i < outputMessage.Length; i++)
            {
                outputMessage[i] = 0xFD;
            }

            //Initialize current dungeonMessage to nothing
            for (int i = 0; i < dungeonMessage.Length; i++)
            {
                dungeonMessage[i] = 0xFD;
            }

            /*
            for (int i = 0; i < dungeonMessage.Length; i += width) //Initialize Dungeon message with three lines.
            {
                //newLine
                dungeonMessage[i] = 0x00;
                dungeonMessage[i + 1] = 0xFF;
            }
            */

            Memory.WriteByteArray(messageAddress, dungeonMessage);

            for (int i = 0; i < customMessage.Length; i++)
            {
                for (int t = 0; t < dcCharTable.Length; t++)
                {
                    if (customMessage[i] == normalCharTable[t])
                    {
                        if (normalCharTable[t] == 0x0A) //newLine
                        {
                            outputMessage[i * 2] = 0x00;
                            outputMessage[i * 2 + 1] = 0xFF;
                        }

                        else if (normalCharTable[t] == 0x20) //SPC
                        {
                            outputMessage[i * 2] = 0x02;
                            outputMessage[i * 2 + 1] = 0xFF;
                        }

                        else if (normalCharTable[t] == 0x5E) //^
                        {
                            if (customMessage[i + 1] == 0x57) //W
                            {
                                i++;  //Skip displaying the W
                                outputMessage[i * 2] = 0x01; //White
                                outputMessage[i * 2 + 1] = 0xFC;
                            }

                            else if (customMessage[i + 1] == 0x59) //Y
                            {
                                i++;
                                outputMessage[i * 2] = 0x02; //Yellow
                                outputMessage[i * 2 + 1] = 0xFC;
                            }

                            else if (customMessage[i + 1] == 0x42) //B
                            {
                                i++;
                                outputMessage[i * 2] = 0x03; //Blue
                                outputMessage[i * 2 + 1] = 0xFC;
                            }

                            else if (customMessage[i + 1] == 0x47) //G
                            {
                                i++;
                                outputMessage[i * 2] = 0x04; //Green
                                outputMessage[i * 2 + 1] = 0xFC;
                            }

                            //0x05 is a nasty brown color

                            else if (customMessage[i + 1] == 0x4F)
                            {
                                i++;
                                outputMessage[i * 2] = 0x06; //Orange
                                outputMessage[i * 2 + 1] = 0xFC;
                            }

                            //0x07 is a gray

                            else if (customMessage[i + 1] == 0x52)
                            {
                                i++;
                                outputMessage[i * 2] = 0xFF; //Red
                                outputMessage[i * 2 + 1] = 0xFC;
                            }
                        }

                        else
                            outputMessage[i * 2] = dcCharTable[t];
                    }
                }

                if(i == customMessage.Length - 1)
                {
                    long aux = messageAddress + outputMessage.Length;

                    Memory.WriteByte(aux, 1);
                    Memory.WriteByte(aux + 0x1, 255);
                }
            }


            byte[] hornHead = { 40, 253, 73, 253, 76, 253, 72, 253, 40, 253, 63, 253, 59, 253, 62, 253, 1, 255 };
            byte[] original10Message = {52, 253, 66, 253, 63, 253, 76, 253, 63, 253, 2, 255, 67, 253, 77, 253, 2, 255, 72, 253, 73, 253, 2, 255, 77, 253, 67, 253, 65, 253, 72, 253, 2, 255, 73, 253, 64, 253, 2, 255, 71, 253, 73, 253, 72, 253, 77, 253, 78, 253, 63, 253, 76, 253, 77, 253, 2, 255, 0, 255, 73, 253, 72, 253, 2, 255, 78, 253, 66, 253, 67, 253, 77, 253, 2, 255, 64, 253, 70, 253, 73, 253, 73, 253, 76, 253, 109, 253, 2, 255, 57, 253, 73, 253, 79, 253, 2, 255, 61, 253, 59, 253, 72, 253, 2, 255, 79, 253, 77, 253, 63, 253, 2, 255, 83, 253, 73, 253, 79, 253, 76, 253, 2, 255, 0, 255, 63, 253, 77, 253, 61, 253, 59, 253, 74, 253, 63, 253, 2, 255, 77, 253, 69, 253, 67, 253, 70, 253, 70, 253, 109, 253, 0, 255, 3, 252, 87, 253, 44, 253, 63, 253, 59, 253, 80, 253, 63, 253, 2, 255, 36, 253, 79, 253, 72, 253, 65, 253, 63, 253, 73, 253, 72, 253, 87, 253, 0, 252, 2, 255, 59, 253, 80, 253, 59, 253, 67, 253, 70, 253, 59, 253, 60, 253, 70, 253, 63, 253, 88, 253, 1, 255};
            int messageId = DungeonMessageBank.CustomId;

            if (isFloorClearMessage)
            {
                messageId = DungeonMessageBank.FloorClearId;
                outputMessage = original10Message;
            }

            Memory.WriteUInt(Addresses.dunMessage, 4294967295);         //Clear any display message
            Memory.WriteByteArray(messageAddress, outputMessage);       //Write our message string onto memory
            Memory.WriteInt(Addresses.dunMessage, messageId);           //Display our custom message
            Memory.WriteInt(Addresses.dunMessageDuration, displayTime); //Set our custom message duration
            Thread.Sleep(300);
            if (isFloorClearMessage && CheckDisplayMessageAvailable()) Memory.WriteByteArray(messageAddress, hornHead); //In case it is the floor clear message, re-write the HornHead string back onto memory

            return outputMessage;
        }
    }
}
