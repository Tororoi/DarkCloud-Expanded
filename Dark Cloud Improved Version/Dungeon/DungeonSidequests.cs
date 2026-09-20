using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The town side quests that play out on a dungeon floor: Macho's, Gob's, Jake's and Chief Bonka's monster hunts
    /// (kills counted per enemy species), Samba's dagger-only challenge on Moon Sea 7 and the Mayor's clear-the-floor quest in the
    /// Demon Shaft. The dungeon loop calls <see cref="CheckSidequests"/> as a floor starts and <see cref="CheckCurrentSidequests"/>
    /// every tick.</summary>
    internal static class DungeonSidequests
    {
        private static int currentAddress;
        internal static bool[] monstersDead = new bool[15];
        internal static bool monsterQuestActive = false;
        internal static bool monsterQuestMachoActive = false;
        internal static bool monsterQuestGobActive = false;
        internal static bool monsterQuestJakeActive = false;
        internal static bool monsterQuestChiefActive = false;
        private static bool sambaChallengeQuest = false;
        private static bool sambaChallengeQuestActive = false;
        private static bool sambaChallengeQuestCheck = false;
        private static bool mayorQuest = false;
        private static bool mayorQuestCheck = false;
        private static bool mayorQuestActive = false;

        internal static void CheckSidequests(byte currentDungeon, byte currentFloor)
        {
            if (currentDungeon == 4 && currentFloor == 6 && Memory.ReadByte(0x21CE445E) == 1)
            {
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Yellow drops challenge active");
                sambaChallengeQuest = true;
            }
            else
            {
                sambaChallengeQuest = false;
            }

            if (currentDungeon == 6)
            {
                if (Memory.ReadByte(0x21CE4468) == 1) //Mayor quest flag
                {
                    if (currentFloor == Memory.ReadByte(0x21CE4469) -1)
                    {
                        mayorQuest = true;
                        //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Mayor quest active in this floor");
                    }
                    else
                    {
                        mayorQuest = false;
                    }
                }
                else
                {
                    mayorQuest = false;
                }
            }
            else
            {
                mayorQuest = false;
            }
        }

        internal static void CheckCurrentSidequests(byte currentDungeon)
        {
            if (monsterQuestActive)
            {
                if (currentDungeon != 6)
                {
                    for (int i = 0; i < monstersDead.Length; i++)
                    {
                        currentAddress = 0x21E16BC4 + (i * 0x190);

                        if (Memory.ReadUShort(currentAddress) > 0)
                        {
                            monstersDead[i] = false;
                        }
                        else
                        {
                            if (monstersDead[i] == false)
                            {
                                CheckEnemyKill(currentAddress);
                            }

                            monstersDead[i] = true;
                        }
                    }
                }
            }

            if (sambaChallengeQuest)
            {
                SambaChallengeQuest();
            }

            if (mayorQuest)
            {
                MayorQuest();
            }
        }

        private static void CheckEnemyKill(int currentEnemyAddress)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Checking quest...");
            if (monsterQuestMachoActive)
            {
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Macho quest active");
                int currentEnemyAddress2 = currentEnemyAddress + 0x0000001E;
                if (Memory.ReadByte(currentEnemyAddress2) == Memory.ReadByte(0x21CE4406))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                    byte killsleft = Memory.ReadByte(0x21CE4405);
                    killsleft--;
                    Memory.WriteByte(0x21CE4405, killsleft);

                    if (killsleft == 0)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
                        DungeonMessages.DisplayMessage("You completed Macho's quest!\nWell done!", 2, 30, 4000);
                        Memory.WriteByte(0x21CE4402, 2);
                        monsterQuestMachoActive = false;
                    }
                }
            }
            if (monsterQuestGobActive)
            {
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Gob quest active");
                int currentEnemyAddress2 = currentEnemyAddress + 0x0000001E;
                if (Memory.ReadByte(currentEnemyAddress2) == Memory.ReadByte(0x21CE440B))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                    byte killsleft = Memory.ReadByte(0x21CE440A);
                    killsleft--;
                    Memory.WriteByte(0x21CE440A, killsleft);

                    if (killsleft == 0)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
                        DungeonMessages.DisplayMessage("You completed Gob's quest!\nWell done!", 2, 30, 4000);
                        Memory.WriteByte(0x21CE4407, 2);
                        monsterQuestGobActive = false;
                    }
                }
            }
            if (monsterQuestJakeActive)
            {
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Jake quest active");
                int currentEnemyAddress2 = currentEnemyAddress + 0x0000001E;
                if (Memory.ReadByte(currentEnemyAddress2) == Memory.ReadByte(0x21CE4410))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                    byte killsleft = Memory.ReadByte(0x21CE440F);
                    killsleft--;
                    Memory.WriteByte(0x21CE440F, killsleft);

                    if (killsleft == 0)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
                        DungeonMessages.DisplayMessage("You completed Jake's quest!\nWell done!", 2, 30, 4000);
                        Memory.WriteByte(0x21CE440C, 2);
                        monsterQuestJakeActive = false;
                    }
                }
            }
            if (monsterQuestChiefActive)
            {
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Chief quest active");
                int currentEnemyAddress2 = currentEnemyAddress + 0x0000001E;
                if (Memory.ReadByte(currentEnemyAddress2) == Memory.ReadByte(0x21CE4415))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest progress +1!");
                    byte killsleft = Memory.ReadByte(0x21CE4414);
                    killsleft--;
                    Memory.WriteByte(0x21CE4414, killsleft);

                    if (killsleft == 0)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Quest complete!!");
                        DungeonMessages.DisplayMessage("You completed Chief Bonka´s quest!\nWell done!", 2, 35, 4000);
                        Memory.WriteByte(0x21CE4411, 2);
                        monsterQuestChiefActive = false;
                    }
                }
            }
        }

        private static void SambaChallengeQuest()
        {
            ushort currentweaponID = Memory.ReadUShort(0x21EA7590);
            if (sambaChallengeQuestCheck == false && Memory.ReadByte(0x202A34CC) == 1)
            {
                if (Memory.ReadByte(Addresses.hideHud) == 0)
                {
                    if (Memory.ReadByte(0x202A3570) == 0 && (currentweaponID == 258 || currentweaponID == 257))
                    {
                        Memory.WriteInt(0x21CE205C, 0);
                        DungeonMessages.DisplayMessage("Samba's quest started!\nClear all enemies using only Dagger!\nUsing a throwable also\ncancels the mission.", 4, 40, 8000);
                        sambaChallengeQuestActive = true;

                        for (int i = 0; i < 8; i++)
                        {
                            monstersDead[i] = false;
                        }
                    }
                    else if (Memory.ReadByte(0x202A3570) == 0 && currentweaponID != 258 && currentweaponID != 257)
                    {
                        DungeonMessages.DisplayMessage("Samba's quest did not start.\nRe-enter with Dagger equipped.", 2, 30, 4000);
                        sambaChallengeQuestActive = false;
                    }
                    sambaChallengeQuestCheck = true;
                }
            }
            else if (sambaChallengeQuestCheck == true && Memory.ReadByte(0x202A34CC) == 0)
            {
                sambaChallengeQuestCheck = false;
                sambaChallengeQuestActive = false;
            }

            if (sambaChallengeQuestActive)
            {
                if ((currentweaponID != 258 && currentweaponID != 257) || Memory.ReadByte(0x21DC4484) == 26 || Memory.ReadByte(0x21DC4484) == 27)
                {
                    Thread.Sleep(500);
                    DungeonMessages.DisplayMessage("Samba's quest has been cancelled.\nRe-enter in order to activate it.", 2, 40, 4000);
                    sambaChallengeQuestActive = false;
                }
                byte enemieskilled = 0;
                for (int i = 0; i < 8; i++)
                {
                    currentAddress = 0x21E16BC4 + (i * 0x190);

                    if (Memory.ReadUShort(currentAddress) > 0)
                    {
                        monstersDead[i] = false;
                    }
                    else
                    {
                        monstersDead[i] = true;
                        enemieskilled++;
                    }
                }

                if (enemieskilled == 8)
                {
                    DungeonMessages.DisplayMessage("Samba's quest completed!\nWell done!", 2, 28, 4000);
                    Memory.WriteByte(0x21CE4462, 1);
                    sambaChallengeQuest = false;
                }
            }
        }

        private static void MayorQuest()
        {
            if (mayorQuestCheck == false && Memory.ReadByte(0x202A34CC) == 1)
            {
                if (Memory.ReadByte(Addresses.hideHud) == 0)
                {
                    if (Memory.ReadByte(0x202A3570) == Memory.ReadByte(0x21CE446A)) //check if correct ally for quest
                    {
                        Memory.WriteInt(0x21CE205C, 0);
                        DungeonMessages.DisplayMessage("Mayor's quest started!\nClear all enemies.\nCannot change character.\nThrowables are not allowed.", 4, 26, 5000);

                        mayorQuestActive = true;

                        for (int i = 0; i < 8; i++)
                        {
                            monstersDead[i] = false;
                        }
                    }
                    else
                    {
                        DungeonMessages.DisplayMessage("Mayor's quest did not start.\nRe-enter with correct ally.", 2, 30, 4000);
                        mayorQuestActive = false;
                    }
                    mayorQuestCheck = true;
                }
            }
            else if (mayorQuestCheck == true && Memory.ReadByte(0x202A34CC) == 0)
            {
                mayorQuestCheck = false;
                mayorQuestActive = false;
            }

            if (mayorQuestActive)
            {
                if (Memory.ReadByte(0x21DC4484) == 26 || Memory.ReadByte(0x21DC4484) == 27)
                {
                    Thread.Sleep(500);
                    DungeonMessages.DisplayMessage("Mayor's quest has been cancelled.\nRe-enter in order to re-attempt it.", 2, 40, 4000);
                    mayorQuestActive = false;
                }

                byte enemieskilled = 0;
                for (int i = 0; i < 8; i++)
                {
                    currentAddress = 0x21E16BC4 + (i * 0x190);

                    if (Memory.ReadUShort(currentAddress) > 0)
                    {
                        monstersDead[i] = false;
                    }
                    else
                    {
                        monstersDead[i] = true;
                        enemieskilled++;
                    }
                }

                if (enemieskilled == 8)
                {
                    DungeonMessages.DisplayMessage("Mayor's quest completed!\nWell done!", 2, 28, 4000);
                    Memory.WriteByte(0x21CE4468, 2);
                    mayorQuest = false;
                }
            }
        }
    }
}
