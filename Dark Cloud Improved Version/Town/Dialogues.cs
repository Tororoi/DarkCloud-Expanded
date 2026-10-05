using System;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The town dialogue WRITER. Encodes the mod's text (<see cref="TownDialogueText"/>, the sidequest lines from
    /// <see cref="SideQuestManager"/>, the two Brownboo quests) into the game's 2-byte glyph stream and writes it
    /// over fixed talk-message buffers: the NPC line the player is standing next to (<see cref="SetDialogue"/>),
    /// the "Hello." placeholder written ahead of it (<see cref="SetDefaultDialogue"/>), the talk-menu options
    /// inside / outside buildings, the storage keeper's original text saved and restored, the ally
    /// fishing-refusal line, the Fairy King fix, the intro line, and the Toan-glyph to ally-glyph pass over the
    /// whole message bank / the shop bank.
    /// </summary>
    class Dialogues
    {
        private const string Tag = "[Dialogues] ";

        static string[] customDialogues = new string[15];
        static string[] customDialogues2 = new string[15];
        static string[] itsfinishedDialogue = new string[15];
        static string brownbooPickleExtraDialogue;
        static string currentDialogue;
        static string currentDialogueOptions;
        static string prevDialogue;
        static string dialogueOptions;


        static bool isUsingAlly;

        static int currentAddress;
        static int currentsidequestAddress;
        static int currentArea = 255;
        static int currentChar;
        static int characterIdData;
        static int savedDialogueCheck;
        static int[] noruneCharacters = { 12592, 12848, 13104, 13360, 13616, 13872, 14128, 14384, 14640, 12337, 12849, 13105, 13361 };   //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
        static int[] norunesidequestCharacters = { 12592, 13872, 13360, 13361};
        static int[] matatakisidequestCharacters = { 13618, 13362, 12594 };
        static int[] queenssidequestCharacters = { 13108, 13363, 12852 };
        static int[] muskarackasidequestCharacters = { 14388, 13109, 12341 };
        static int[] matatakiCharacters = { 12594, 12850, 13106, 13362, 13618, 13874, 14130, 14386, 14642, 12339, 12595, 12851 }; //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
        static int[] queensCharacters = { 13107, 13363, 13619, 13875, 14131, 14643, 12340, 12596, 12852, 13108, 13364, 13620, 14644 }; //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
        static int[] muskarackaCharacters = { 13876, 14388, 12341, 12597, 12853, 13109, 13365, 13621, 13877, 14133, 14389 }; //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
        static int[] sunmoonCharacters = { 12337, 13111 };
        static int[] yellowdropsTalkableCharacterIDs = { 2, 3, 4, 5, 6, 7, 9, 10, 11, 12 };
        static int[] brownbooTalkableCharacterIDs = { 6, 7, 8, 10, 11, 12 };
        static int[] customDialoguesCheck = new int[15];      
        static int[] noruneXiaoCheck = new int[15];
        static int[] noruneGoroCheck = new int[15];
        static int[] noruneRubyCheck = new int[15];
        static int[] noruneUngagaCheck = new int[15];
        static int[] noruneOsmondCheck = new int[15];
        static int[] matatakiXiaoCheck = new int[15];
        static int[] matatakiGoroCheck = new int[15];
        static int[] matatakiRubyCheck = new int[15];
        static int[] matatakiUngagaCheck = new int[15];
        static int[] matatakiOsmondCheck = new int[15];
        static int[] queensXiaoCheck = new int[15];
        static int[] queensGoroCheck = new int[15];
        static int[] queensRubyCheck = new int[15];
        static int[] queensUngagaCheck = new int[15];
        static int[] queensOsmondCheck = new int[15];
        static int[] muskarackaXiaoCheck = new int[15];
        static int[] muskarackaGoroCheck = new int[15];
        static int[] muskarackaRubyCheck = new int[15];
        static int[] muskarackaUngagaCheck = new int[15];
        static int[] muskarackaOsmondCheck = new int[15];
        static int[] sunmoonXiaoCheck = new int[15];
        static int[] sunmoonGoroCheck = new int[15];
        static int[] sunmoonRubyCheck = new int[15];
        static int[] sunmoonUngagaCheck = new int[15];
        static int[] sunmoonOsmondCheck = new int[15];
        static int[] yellowdropsXiaoCheck = new int[15];
        static int[] yellowdropsGoroCheck = new int[15];
        static int[] yellowdropsRubyCheck = new int[15];
        static int[] yellowdropsUngagaCheck = new int[15];
        static int[] yellowdropsOsmondCheck = new int[15];
        static int[] brownbooXiaoCheck = new int[15];
        static int[] brownbooGoroCheck = new int[15];
        static int[] brownbooRubyCheck = new int[15];
        static int[] brownbooUngagaCheck = new int[15];
        static int[] brownbooOsmondCheck = new int[15];
        static int[] darkheavenXiaoCheck = new int[15];
        static int[] darkheavenGoroCheck = new int[15];
        static int[] darkheavenRubyCheck = new int[15];
        static int[] darkheavenUngagaCheck = new int[15];
        static int[] darkheavenOsmondCheck = new int[15];


        public static byte[] storageOriginalDialogue;
        public static byte[] storageAllDialogues;

        static int[] noruneSidequestIDs = { 87, 247, 207, 227, 187, 127, 67, 167, 147, 267, 47, 107, 0};
        static int[] noruneSidequestDialogueAddresses = { 0x2064B36C, 0x206507BE, 0x2064F350, 0x2064FC66, 0x2064EAC2, 0x2064CB04, 0x2064A36A, 0x2064DFB0, 0x2064D6C2, 0x206519EE, 0x20649916, 0x2064C088, 0 };

        static byte[] value1 = new byte[1];
        static byte[] value = new byte[2];
        static byte[] value4 = new byte[4];
        static char[] gameCharacters = { '^', '§', '_', '¤', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', 'Ȟ', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', '§', //32
                              'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', //58
                              'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', //84
                              '´', '=', '"', '!', '?', '#', '&', '+', '-', '*', '/', '%', '(', ')', '@', '|', '<', '>', '{', '}', '[', ']', ':', ',', '.', '$',
                              '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'Ť', 'Ӿ', 'Ʊ', 'Ʀ', 'Ų', 'Ō', ' ' };

        static byte[] gameCharacters2 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 88, 87, 90, 110, 96, 91, 85, 97, 98, 94, 92, 108, 93, 109, 95,
                                            111, 112, 113, 114, 115, 117, 118, 119, 120, 121, 107, 0, 101, 86, 102, 89, 99, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
                                            105, 0, 106, 0, 2, 0, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 103, 100, 104, 2 };


        public static void SetDialogue(int offset, bool isAlly, bool isSidequest, bool finishedDialogue = false)
        {
            isUsingAlly = isAlly;
            if (Memory.ReadByte(0x202A2518) != currentArea)     //DOESNT UPDATE when switching ally, fix later!!
            {
                /*if (Memory.ReadByte(0x202A2518) == 0) currentArea = 0;
                else if (Memory.ReadByte(0x202A2518) == 1) currentArea = 1;
                else if (Memory.ReadByte(0x202A2518) == 2) currentArea = 2;
                else if (Memory.ReadByte(0x202A2518) == 3) currentArea = 3;
                else if (Memory.ReadByte(0x202A2518) == 14) currentArea = 14; */
                currentArea = Memory.ReadByte(0x202A2518);
                SetDefaultDialogue(currentArea);

                for (int i = 0; i < customDialoguesCheck.Length; i++)   //reset NPC dialogue progress if changed area
                {
                    customDialoguesCheck[i] = 0;
                }
                currentChar = 0;
            }

            if (finishedDialogue == true)
            {
                GetCurrentAreaFinishedDialogues(currentArea);
            }

            int ally = AllySwapPrototype.CurrentAlly;   // who the town character is (0 Toan .. 5 Osmond), as the in-place swap tracks it
            if (currentChar != ally && isAlly == true)  //if using different ally, switch dialogue data
            {              

                if (ally == 1) //Xiao
                {
                    if (currentArea == 0)
                    {
                        customDialogues = TownDialogueText.NoruneXiao;
                        customDialogues2 = TownDialogueText.NoruneXiao2;
                        customDialoguesCheck = noruneXiaoCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[0];
                    }
                    else if (currentArea == 1)
                    {
                        customDialogues = TownDialogueText.MatatakiXiao;
                        customDialogues2 = TownDialogueText.MatatakiXiao2;
                        customDialoguesCheck = matatakiXiaoCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[1];
                    }
                    else if (currentArea == 2)
                    {
                        customDialogues = TownDialogueText.QueensXiao;
                        customDialogues2 = TownDialogueText.QueensXiao2;
                        customDialoguesCheck = queensXiaoCheck;
                    }
                    else if (currentArea == 3)
                    {
                        customDialogues = TownDialogueText.MuskarackaXiao;
                        customDialogues2 = TownDialogueText.MuskarackaXiao2;
                        customDialoguesCheck = muskarackaXiaoCheck;
                    }
                    else if (currentArea == 14)
                    {
                        customDialogues = TownDialogueText.BrownbooXiao;
                        customDialogues2 = TownDialogueText.BrownbooXiao2;
                        customDialoguesCheck = brownbooXiaoCheck;
                    }
                    else if (currentArea == 23)
                    {
                        customDialogues = TownDialogueText.YellowdropsXiao;
                        customDialogues2 = TownDialogueText.YellowdropsXiao2;
                        customDialoguesCheck = yellowdropsXiaoCheck;
                    }
                    else if (currentArea == 38)
                    {
                        customDialogues[0] = TownDialogueText.DarkheavenXiao;
                        customDialogues2[0] = TownDialogueText.DarkheavenXiao2;
                        customDialoguesCheck = darkheavenXiaoCheck;
                    }
                    else if (currentArea == 42)
                    {
                        customDialogues = TownDialogueText.SunmoonXiao;
                        customDialogues2 = TownDialogueText.SunmoonXiao2;
                        customDialoguesCheck = sunmoonXiaoCheck;
                    }
                }
                else if (ally == 2)  //Goro
                {
                    if (currentArea == 0)
                    {
                        customDialogues = TownDialogueText.NoruneGoro;
                        customDialogues2 = TownDialogueText.NoruneGoro2;
                        customDialoguesCheck = noruneGoroCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[0];
                    }
                    else if (currentArea == 1)
                    {
                        customDialogues = TownDialogueText.MatatakiGoro;
                        customDialogues2 = TownDialogueText.MatatakiGoro2;
                        customDialoguesCheck = matatakiGoroCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[1];
                    }
                    else if (currentArea == 2)
                    {
                        customDialogues = TownDialogueText.QueensGoro;
                        customDialogues2 = TownDialogueText.QueensGoro2;
                        customDialoguesCheck = queensGoroCheck;
                    }
                    else if (currentArea == 3)
                    {
                        customDialogues = TownDialogueText.MuskarackaGoro;
                        customDialogues2 = TownDialogueText.MuskarackaGoro2;
                        customDialoguesCheck = muskarackaGoroCheck;
                    }
                    else if (currentArea == 14)
                    {
                        customDialogues = TownDialogueText.BrownbooGoro;
                        customDialogues2 = TownDialogueText.BrownbooGoro2;
                        customDialoguesCheck = brownbooGoroCheck;
                    }
                    else if (currentArea == 23)
                    {
                        customDialogues = TownDialogueText.YellowdropsGoro;
                        customDialogues2 = TownDialogueText.YellowdropsGoro2;
                        customDialoguesCheck = yellowdropsGoroCheck;
                    }
                    else if (currentArea == 38)
                    {
                        customDialogues[0] = TownDialogueText.DarkheavenGoro;
                        customDialogues2[0] = TownDialogueText.DarkheavenGoro2;
                        customDialoguesCheck = darkheavenGoroCheck;
                    }
                    else if (currentArea == 42)
                    {
                        customDialogues = TownDialogueText.SunmoonGoro;
                        customDialogues2 = TownDialogueText.SunmoonGoro2;
                        customDialoguesCheck = sunmoonGoroCheck;
                    }
                }

                else if (ally == 3)  //Ruby
                {
                    if (currentArea == 0)
                    {
                        customDialogues = TownDialogueText.NoruneRuby;
                        customDialogues2 = TownDialogueText.NoruneRuby2;
                        customDialoguesCheck = noruneRubyCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[0];
                    }
                    else if (currentArea == 1)
                    {
                        customDialogues = TownDialogueText.MatatakiRuby;
                        customDialogues2 = TownDialogueText.MatatakiRuby2;
                        customDialoguesCheck = matatakiRubyCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[1];
                    }
                    else if (currentArea == 2)
                    {
                        customDialogues = TownDialogueText.QueensRuby;
                        customDialogues2 = TownDialogueText.QueensRuby2;
                        customDialoguesCheck = queensRubyCheck;
                    }
                    else if (currentArea == 3)
                    {
                        customDialogues = TownDialogueText.MuskarackaRuby;
                        customDialogues2 = TownDialogueText.MuskarackaRuby2;
                        customDialoguesCheck = muskarackaRubyCheck;
                    }
                    else if (currentArea == 14)
                    {
                        customDialogues = TownDialogueText.BrownbooRuby;
                        customDialogues2 = TownDialogueText.BrownbooRuby2;
                        customDialoguesCheck = brownbooRubyCheck;
                    }
                    else if (currentArea == 23)
                    {
                        customDialogues = TownDialogueText.YellowdropsRuby;
                        customDialogues2 = TownDialogueText.YellowdropsRuby2;
                        customDialoguesCheck = yellowdropsRubyCheck;
                    }
                    else if (currentArea == 38)
                    {
                        customDialogues[0] = TownDialogueText.DarkheavenRuby;
                        customDialogues2[0] = TownDialogueText.DarkheavenRuby2;
                        customDialoguesCheck = darkheavenRubyCheck;
                    }
                    else if (currentArea == 42)
                    {
                        customDialogues = TownDialogueText.SunmoonRuby;
                        customDialogues2 = TownDialogueText.SunmoonRuby2;
                        customDialoguesCheck = sunmoonRubyCheck;
                    }
                }

                else if (ally == 4)  //Ungaga
                {
                    if (currentArea == 0)
                    {
                        customDialogues = TownDialogueText.NoruneUngaga;
                        customDialogues2 = TownDialogueText.NoruneUngaga2;
                        customDialoguesCheck = noruneUngagaCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[0];
                    }
                    else if (currentArea == 1)
                    {
                        customDialogues = TownDialogueText.MatatakiUngaga;
                        customDialogues2 = TownDialogueText.MatatakiUngaga2;
                        customDialoguesCheck = matatakiUngagaCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[1];
                    }
                    else if (currentArea == 2)
                    {
                        customDialogues = TownDialogueText.QueensUngaga;
                        customDialogues2 = TownDialogueText.QueensUngaga2;
                        customDialoguesCheck = queensUngagaCheck;
                    }
                    else if (currentArea == 3)
                    {
                        customDialogues = TownDialogueText.MuskarackaUngaga;
                        customDialogues2 = TownDialogueText.MuskarackaUngaga2;
                        customDialoguesCheck = muskarackaUngagaCheck;
                    }
                    else if (currentArea == 14)
                    {
                        customDialogues = TownDialogueText.BrownbooUngaga;
                        customDialogues2 = TownDialogueText.BrownbooUngaga2;
                        customDialoguesCheck = brownbooUngagaCheck;
                    }
                    else if (currentArea == 23)
                    {
                        customDialogues = TownDialogueText.YellowdropsUngaga;
                        customDialogues2 = TownDialogueText.YellowdropsUngaga2;
                        customDialoguesCheck = yellowdropsUngagaCheck;
                    }
                    else if (currentArea == 38)
                    {
                        customDialogues[0] = TownDialogueText.DarkheavenUngaga;
                        customDialogues2[0] = TownDialogueText.DarkheavenUngaga2;
                        customDialoguesCheck = darkheavenUngagaCheck;
                    }
                    else if (currentArea == 42)
                    {
                        customDialogues = TownDialogueText.SunmoonUngaga;
                        customDialogues2 = TownDialogueText.SunmoonUngaga2;
                        customDialoguesCheck = sunmoonUngagaCheck;
                    }
                }

                else if (ally == 5)  //Osmond
                {
                    if (currentArea == 0)
                    {
                        customDialogues = TownDialogueText.NoruneOsmond;
                        customDialogues2 = TownDialogueText.NoruneOsmond2;
                        customDialoguesCheck = noruneOsmondCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[0];
                    }
                    else if (currentArea == 1)
                    {
                        customDialogues = TownDialogueText.MatatakiOsmond;
                        customDialogues2 = TownDialogueText.MatatakiOsmond2;
                        customDialoguesCheck = matatakiOsmondCheck;
                        currentDialogueOptions = TownDialogueText.SideQuestDialogueOption[1];
                    }
                    else if (currentArea == 2)
                    {
                        customDialogues = TownDialogueText.QueensOsmond;
                        customDialogues2 = TownDialogueText.QueensOsmond2;
                        customDialoguesCheck = queensOsmondCheck;
                    }
                    else if (currentArea == 3)
                    {
                        customDialogues = TownDialogueText.MuskarackaOsmond;
                        customDialogues2 = TownDialogueText.MuskarackaOsmond2;
                        customDialoguesCheck = muskarackaOsmondCheck;
                    }
                    else if (currentArea == 14)
                    {
                        customDialogues = TownDialogueText.BrownbooOsmond;
                        customDialogues2 = TownDialogueText.BrownbooOsmond2;
                        customDialoguesCheck = brownbooOsmondCheck;
                    }
                    else if (currentArea == 23)
                    {
                        customDialogues = TownDialogueText.YellowdropsOsmond;
                        customDialogues2 = TownDialogueText.YellowdropsOsmond2;
                        customDialoguesCheck = yellowdropsOsmondCheck;
                    }
                    else if (currentArea == 38)
                    {
                        customDialogues[0] = TownDialogueText.DarkheavenOsmond;
                        customDialogues2[0] = TownDialogueText.DarkheavenOsmond2;
                        customDialoguesCheck = darkheavenOsmondCheck;
                    }
                    else if (currentArea == 42)
                    {
                        customDialogues = TownDialogueText.SunmoonOsmond;
                        customDialogues2 = TownDialogueText.SunmoonOsmond2;
                        customDialoguesCheck = sunmoonOsmondCheck;
                    }
                }

                currentChar = ally;

                currentArea = Memory.ReadByte(0x202A2518);
                SetDefaultDialogue(currentArea);

                for (int i = 0; i < customDialoguesCheck.Length; i++)   //reset NPC dialogue progress if changed ally
                {
                    customDialoguesCheck[i] = 0;
                }
            }
            else if (currentChar != Memory.ReadInt(currentAddress) && isAlly == false)
            {
                currentArea = Memory.ReadByte(0x202A2518);
                SetDefaultDialogue(currentArea);
            }

            currentAddress = offset * 0x14A0 + 0x21D26FD9;
            characterIdData = Memory.ReadShort(currentAddress);     //store the ID value of nearby character

            // A quest on offer makes the ally's "Hello" the quest dialogue itself (the PNACH pins an ally's greeting to the mod's
            // message, so the intro is written there); the quest line is for ongoing quests (QuestOffers).
            bool helloQuest = isAlly && !isSidequest && !finishedDialogue
                              && QuestOffers.For(currentArea, characterIdData).Phase == QuestPhase.Available;
            if (helloQuest) isSidequest = true;
            if (currentArea == 0)
            {
                for (int i = 0; i < noruneCharacters.Length; i++)   //search through array to find character match
                {
                    if (characterIdData == noruneCharacters[i]) //when the NPC is detected, fetch the correct dialogues
                    {
                        if (customDialoguesCheck[i] != 1) //checks which one of the two dialogues should be written. This results in unoptimal extra code, but I can't be bothered with it
                        {
                            if (isSidequest)
                            {
                                if (norunesidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 12849)
                                {
                                    currentDialogue = "I needed your help earlier,^but I´m okay now.¤You see, I slipped on this^pink thing which made me all^slow and slimy.¤Well, I survived from that disaster.";
                                }
                                else if (characterIdData == 14640) 
                                {
                                    if (AllySwapPrototype.CurrentAlly == 0) //check toan
                                    {
                                        currentDialogue = "Did you buy bombs from Gaffer´s shop?^Please be careful Ť,^I just want you to come home safely.";    //dialogue requested by beta tester
                                    }
                                    else
                                    {
                                        currentDialogue = "Sorry, I don´t have any quests currently.";
                                    }
                                }                               
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }
                        else
                        {
                            if (isSidequest)
                            {
                                if (norunesidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 12849)
                                {
                                    currentDialogue = "I needed your help earlier,^but I´m okay now.¤You see, I slipped on this^pink thing which made me all^slow and slimy.¤Well, I survived from that disaster.";
                                }
                                else if (characterIdData == 14640)
                                {
                                    if (AllySwapPrototype.CurrentAlly == 0) //check toan
                                    {
                                        currentDialogue = "Did you buy bombs from Gaffer´s shop?^Please be careful Ť,^I just want you to come home safely.";    //dialogue requested by beta tester
                                    }
                                    else
                                    {
                                        currentDialogue = "Sorry, I don´t have any quests currently.";
                                    }
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues2[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }

                        if (i == 1 || i == 11)  //check for shopkeeper
                        {
                            TownCharacter.shopkeeper = true;
                        }
                        else
                        {
                            TownCharacter.shopkeeper = false;
                        }

                        TownCharacter.sidequestDialogueID = 107;
                        currentsidequestAddress = 0x2064C088; //hag's first hello normal dialogue
                    }
                }
            }
            else if (currentArea == 1)
            {
                for (int i = 0; i < matatakiCharacters.Length; i++)   //search through array to find character match
                {
                    if (characterIdData == matatakiCharacters[i])
                    {
                        if (customDialoguesCheck[i] != 1)
                        {
                            if (isSidequest)
                            {
                                if (matatakisidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 14386)
                                {
                                    currentDialogue = "I wish you happened to be there.¤One day I accidentally ventured^too deep into the forest and^was surrounded by monsters.¤Luckily, I had this red pouch which^allowed me to get back to safety.";
                                }
                                else if (characterIdData == 13106)
                                {
                                    currentDialogue = "Unless I was made of Gilda, I wouldn´t^buy fish bait from Mr. Mustache.^With the right weapon, you can find^plenty of bait in the forest.";  //dialogue requested by beta tester
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }
                        else
                        {
                            if (isSidequest)
                            {
                                if (matatakisidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 14386)
                                {
                                    currentDialogue = "I wish you happened to be there.¤One day I accidentally ventured^too deep into the forest and^was surrounded by monsters.¤Luckily, I had this red pouch which^allowed me to get back to safety.";
                                }
                                else if (characterIdData == 13106)
                                {
                                    currentDialogue = "Unless I was made of Gilda, I wouldn´t^buy fish bait from Mr. Mustache.^With the right weapon, you can find^plenty of bait in the forest.";  //dialogue requested by beta tester
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues2[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }

                        if (i == 10 || i == 11)  //check for shopkeeper
                        {
                            TownCharacter.shopkeeper = true;
                        }
                        else
                        {
                            TownCharacter.shopkeeper = false;
                        }

                        TownCharacter.sidequestDialogueID = 107;
                        currentsidequestAddress = 0x2064C492; //hag's first hello normal dialogue
                    }
                }
            }
            else if (currentArea == 2)
            {
                for (int i = 0; i < queensCharacters.Length; i++)   //search through array to find character match
                {
                    if (characterIdData == queensCharacters[i])
                    {
                        if (customDialoguesCheck[i] != 1)
                        {
                            if (isSidequest)
                            {
                                if (queenssidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 13107)
                                {
                                    bool hasLamp = SideQuestManager.CheckItemQuestReward(241, true, false);
                                    if (hasLamp)
                                    {
                                        currentDialogue = "What do you want?¤Wait... that lamp...¤...that cursed lamp...¤NO! Get it away from me!";
                                    }
                                    else
                                    {
                                        currentDialogue = "I don´t need you to do sidequests,^those are for my henchmen.";
                                    }
                                }
                                else if (characterIdData == 13364)
                                {
                                    currentDialogue = "There was a large fight a while ago.¤I almost wanted to call you for help.^There was this thief who suddenly^took a sip of something and^became more powerful.¤Thanks to the strength of Macho^Brothers´s bloodline, I was able^to deal with him myself.";
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }
                        else
                        {
                            if (isSidequest)
                            {
                                if (queenssidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 13107)
                                {
                                    bool hasLamp = SideQuestManager.CheckItemQuestReward(241, true, false);
                                    if (hasLamp)
                                    {
                                        currentDialogue = "What do you want?¤Wait... that lamp...¤...that cursed lamp...¤NO! Get it away from me!";
                                    }
                                    else
                                    {
                                        currentDialogue = "I don´t need you to do sidequests,^those are for my henchmen.";
                                    }
                                }
                                else if (characterIdData == 13364)
                                {
                                    currentDialogue = "There was a large fight a while ago.¤I almost wanted to call you for help.^There was this thief who suddenly^took a sip of something and^became more powerful.¤Thanks to the strength of Macho^Brothers´s bloodline, I was able^to deal with him myself.";
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues2[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }

                        if (i == 2 || i == 3 || i == 4 || i == 5 || i == 7 || i == 12)  //check for shopkeeper
                        {
                            TownCharacter.shopkeeper = true;
                        }
                        else
                        {
                            TownCharacter.shopkeeper = false;
                        }

                        TownCharacter.sidequestDialogueID = 127;
                        currentsidequestAddress = 0x2064DB3A; //basker's first hello normal dialogue
                    }
                }
            }
            else if (currentArea == 3)
            {
                for (int i = 0; i < muskarackaCharacters.Length; i++)   //search through array to find character match
                {
                    if (characterIdData == muskarackaCharacters[i])
                    {
                        if (customDialoguesCheck[i] != 1)
                        {
                            if (isSidequest)
                            {
                                if (muskarackasidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 13877)
                                {
                                    currentDialogue = "How about you get me out of here?¤I wish I had something to^blow up this darn door.¤Oh wait, I probably shouldn´t^be in the cell then.";
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }
                        else
                        {
                            if (isSidequest)
                            {
                                if (muskarackasidequestCharacters.Contains(characterIdData))
                                {
                                    currentDialogue = SideQuestManager.GetQuestDialogue(currentDialogue, characterIdData);
                                }
                                else if (characterIdData == 13877)
                                {
                                    currentDialogue = "How about you get me out of here?¤I wish I had something to^blow up this darn door.¤Oh wait, I probably shouldn´t^be in the cell then.";
                                }
                                else
                                {
                                    currentDialogue = "Sorry, I don´t have any quests currently.";
                                }
                            }
                            else if (finishedDialogue)
                            {
                                currentDialogue = itsfinishedDialogue[i];
                            }
                            else
                            {
                                currentDialogue = customDialogues2[i];    //gets the correct dialogue and stores it
                                savedDialogueCheck = i;
                            }
                        }

                        if (i == 6 || i == 7)  //check for shopkeeper
                        {
                            TownCharacter.shopkeeper = true;
                        }
                        else
                        {
                            TownCharacter.shopkeeper = false;
                        }
                    }
                    TownCharacter.sidequestDialogueID = 147;
                    currentsidequestAddress = 0x2064DDB8; //basker's first hello normal dialogue
                }
            }
            else if (currentArea == 14)
            {
                currentAddress = currentAddress - 0x00000005;
                byte currentNPCID = Memory.ReadByte(currentAddress);
                if (currentNPCID == 9)
                {
                    if (Memory.ReadByte(0x21CE43FE) == 0)
                    {
                        currentDialogue = TownDialogueText.BrownbooPickle;
                    }
                    else
                    {
                        BrownbooCollectionQuest.CheckItems();
                        int allitems = BrownbooCollectionQuest.obtainableItemsList.Length + BrownbooCollectionQuest.obtainableAttachmentsList.Length;
                        if (BrownbooCollectionQuest.obtainedItems == allitems && BrownbooCollectionQuest.obtainedUltWeapons == BrownbooCollectionQuest.obtainableUltWeapons.Length && BrownbooCollectionQuest.obtainedSecretItems == BrownbooCollectionQuest.obtainableSecretItems.Length)
                        {
                            string cheatdialogue = "";
                            if (Memory.ReadByte(0x21CE446C) == 1)
                            {
                                cheatdialogue = "¤I hope you didn´t^use any cheats!... right?";
                            }

                            brownbooPickleExtraDialogue = "WOW! Congratulations,^you´ve actually done it!^You collected everything!¤As a present, have this old key.^You should totally try^if you can use it somewhere!" + cheatdialogue;
                            bool hasKey = SideQuestManager.CheckItemQuestReward(248);
                            if (hasKey == false)
                            {
                                Memory.WriteUShort(Addresses.firstBagItem + (0x2 * Inventory.GetBagItemsFirstAvailableSlot()), 248);
                                Memory.WriteByte(0x21CE4463, 1);
                            }
                        }
                        else
                        {
                            brownbooPickleExtraDialogue = "Hmm, seems like you don´t have^100% collection yet. Don´t worry,^it´s a massive achievement to reach!¤Keep in mind that all quest items and^character stat-boosting items are not^part of the collection.^Good luck!";
                        }
                        
                        currentDialogue = "You have collected:^" + BrownbooCollectionQuest.obtainedItems + " / " + allitems + " obtainable items and attachments^" + BrownbooCollectionQuest.obtainedUltWeapons + " / " + BrownbooCollectionQuest.obtainableUltWeapons.Length +" obtainable ultimate weapons^" + BrownbooCollectionQuest.obtainedSecretItems + " / " + BrownbooCollectionQuest.obtainableSecretItems.Length +" secret items¤" + brownbooPickleExtraDialogue;
                        BrownbooCollectionQuest.ResetProgress();
                    }
                }
                else if (currentNPCID == 5)
                {
                    if (Memory.ReadByte(0x21CE444B) == 0)
                    {
                        currentDialogue = "Do you like fishing?^I have a great challenge for you.¤Catch all 17 different types of fish,^and I will reward you with a rare item!¤You can talk to me again^to know your current progress.";
                    }
                    else
                    {
                        MasterFishQuest.CheckFish();
                        if (MasterFishQuest.masterFishQuestComplete)
                        {
                            MasterFishQuest.CheckMasterFishQuestReward();
                            if (MasterFishQuest.alreadyHasSavingBook)
                            {
                                currentDialogue = "I already gave you my reward,^but if you somehow lose it you^can ask for another!";
                            }
                            else
                            {
                                if (Memory.ReadByte(0x21CE4450) == 0)
                                {
                                    currentDialogue = "So you have completed my challenge?¤Cool, you truly are a Master of Fishing!^Here, have this rare book that^I found the other day.¤I don´t know what it does, but^maybe some people collect them.";                                    
                                }
                                else
                                {
                                    currentDialogue = "What? You lost the reward??¤Well, I have another one here,^don´t lose it again!";
                                }
                            }
                        }
                        else
                        {
                            currentDialogue = "You still have to^find the following fish:¤" + MasterFishQuest.fishToFind + "¤Come back when you have caught them!^Good luck!";
                        }
                    }
                }
                else if (currentNPCID == 3)
                {
                    if (Memory.ReadByte(0x21CE4455) == 0)
                    {
                        if (Memory.ReadByte(0x21CDD811) == 255)
                        {
                            currentDialogue = "Have you heard about this?¤There is a legend that somewhere at^north-east of the Matataki Village,^you can find an very long evil tower.¤If you happen to find it, come back^to me. I might have more to tell.";
                        }
                        else
                        {
                            currentDialogue = "According to a legend, there is a^rare artifact hidden somewhere in^the darkness of the Demon Shaft.¤I heard it´s so valuable that^not even money can buy it.";
                        }
                    }
                    else
                    {
                        bool hasItem = SideQuestManager.CheckItemQuestReward(241);
                        if (hasItem)
                        {
                            currentDialogue = "What´s that?^You found the rare artifact?¤Magical lamp... hmmm....¤If you don´t know what to do with it,^you should try speaking to someone^in Queens, there´s plenty of^artifact merchandise.";
                        }
                        else
                        {
                            currentDialogue = "Have you found the Demon Shaft´s^legendary artifact yet? Try searching^behind every stone and WALL.";
                        }
                    }
                }
                else
                {
                    byte NPCID = Memory.ReadByte(currentAddress);
                    if (brownbooTalkableCharacterIDs.Contains(NPCID))
                    {
                        if (customDialoguesCheck[NPCID] != 1)
                        {
                            currentDialogue = customDialogues[NPCID];
                            savedDialogueCheck = NPCID;
                        }
                        else
                        {
                            currentDialogue = customDialogues2[NPCID];
                            savedDialogueCheck = NPCID;
                        }

                        if (isUsingAlly == false)
                        {
                            currentDialogue = "Hello.";
                        }
                    }
                    //currentDialogue = "Sup buddy. I don´t have a dialogue yet.";
                }
            }
            else if (currentArea == 23)
            {
                currentAddress = currentAddress - 0x00000005;
                byte NPCID = Memory.ReadByte(currentAddress);
                if (yellowdropsTalkableCharacterIDs.Contains(NPCID))
                {
                    if (customDialoguesCheck[NPCID] != 1)
                    {
                        currentDialogue = customDialogues[NPCID];
                        savedDialogueCheck = NPCID;
                    }
                    else
                    {
                        currentDialogue = customDialogues2[NPCID];
                        savedDialogueCheck = NPCID;
                    }
                }
                else if (NPCID == 0)
                {
                    if (isSidequest)
                    {
                        if (Memory.ReadByte(0x21CE4459) == 0)
                        {
                            if (Memory.ReadByte(0x21CD9551) != 6)
                            {
                                currentDialogue = "Have you seen our Boss anywhere?¤Go search for him first.^Come back to me if you find him.";
                            }
                            else
                            {
                                currentDialogue = "I have an extremely valuable crystal.¤I know you are strong,^but if you want my crystal, you need^to prove that you are smart as well!¤I have a puzzle for you.^Talk to me again if you^wish to know more.";
                            }
                            
                        }
                        else if (Memory.ReadByte(0x21CE4459) == 1)
                        {
                            bool hasCorrect = SideQuestManager.CheckItemsForMCQuest();
                            if (hasCorrect)
                            {
                                Memory.WriteByte(0x21CE445D, 1);
                                currentDialogue = "That´s correct, well done!^You solved the puzzle!¤As a reward, have this^ultra-rare Magical Crystal.¤While this is in your inventory,^it works similar to the^Magical Crystal you find in the^dungeons, but it is always activated!¤Also, the Magical Crystal you^find in dungeons gets replaced^with extra loot. Worth it, right?";
                            }
                            else
                            {
                                Memory.WriteByte(0x21CE445D, 0);
                                currentDialogue = "I want you to show me certain^four items in a correct order. Place^those items in the first four slots^of your inventory.¤What are the items?^I´m not straight up telling them!^However...¤In the Blue Terra, your homeworld,^you might have met some people^who needed something.¤Perhaps they are the^clue to the right answer?";
                            }
                        }
                        else 
                        {
                            currentDialogue = "I already gave you my reward,^you don´t need a second crystal!";
                        }
                        
                    }
                }
                else if (NPCID == 1)
                {
                    if (isSidequest)
                    {
                        if (Memory.ReadByte(0x21CE445E) == 0)
                        {
                            if (Memory.ReadByte(0x21CD9551) != 6)
                            {
                                currentDialogue = "Have you seen our Boss anywhere?¤Go search for him first.^Come back to me if you find him.";
                            }
                            else
                            {
                                Memory.WriteByte(0x21CDD7C6, 255);
                                currentDialogue = "I have a challenge to test^your true strength.¤If you can conquer Moon Sea Floor 7´s^backside only with Ť and^only using Dagger, you can have^my reward.¤The healing fountains are also^disabled there, so be sure to stock up.";
                            }
                        }
                        else if (Memory.ReadByte(0x21CE445E) == 1)
                        {
                            if (Memory.ReadByte(0x21CE4462) == 0)
                            {
                                currentDialogue = "If you can conquer Moon Sea Floor 7´s^backside only with Ť and^only using Dagger, you can have^my reward.¤The healing fountains are also^disabled there, so be sure to stock up.";
                            }
                            else
                            {
                                currentDialogue = "You completed the challenge? Extraordinary!¤Here, you can have this special Map.¤While this is in your inventory,^it works similar to the Map you find^in the dungeons, but it is^always activated!¤Also, the Map you find in dungeons^gets replaced with extra loot.^Pretty cool, huh?";
                            }
                            
                        }
                        else
                        {
                            currentDialogue = "I already gave you my reward,^you don´t need a second map!";
                        }
                    }
                }

                if (NPCID == 0 || NPCID == 1)
                {
                    TownCharacter.shopkeeper = true;
                }
                else
                {
                    TownCharacter.shopkeeper = false;
                }

                TownCharacter.sidequestDialogueID = 262;

            }
            else if (currentArea == 38)
            {
                if (customDialoguesCheck[0] != 1)
                {
                    currentDialogue = customDialogues[0];
                    savedDialogueCheck = 0;
                }
                else
                {
                    currentDialogue = customDialogues2[0];
                    savedDialogueCheck = 0;
                }
            }
            else if (currentArea == 42)
            {
                for (int i = 0; i < sunmoonCharacters.Length; i++)   //search through array to find character match
                {
                    if (characterIdData == sunmoonCharacters[i])
                    {
                        if (customDialoguesCheck[i] != 1)
                        {
                            currentDialogue = customDialogues[i];    //gets the correct dialogue and stores it
                            savedDialogueCheck = i;
                        }
                        else
                        {
                            currentDialogue = customDialogues2[i];    //gets the correct dialogue and stores it
                            savedDialogueCheck = i;
                        }
                    }
                }
            }

            if (characterIdData == 14132)
            {
                TownCharacter.talkableNPC = false;
            }
            if (currentArea == 0)
            {
                currentAddress = 0x206507BE; //gaffers first normal "hello" dialogue
                if (isSidequest && !helloQuest)
                {
                    currentAddress = currentsidequestAddress;
                }
                else if (finishedDialogue)
                {
                    currentAddress = 0x206519EE; //renee first normal hello (267)
                    TownCharacter.itsfinishedDialogueID = 267;
                }
            }
            else if (currentArea == 1)
            {
                currentAddress = 0x2064ECBC; //pao's first normal "hello" dialogue
                if (isSidequest && !helloQuest)
                {
                    currentAddress = currentsidequestAddress;
                }
                else if (finishedDialogue)
                {
                    currentAddress = 0x2064F7BC; //kye first normal hello (187)
                    TownCharacter.itsfinishedDialogueID = 187;
                }
            }
            else if (currentArea == 2)
            {
                currentAddress = 0x2064BED8; //suzy's first normal "hello" dialogue
                if (isSidequest && !helloQuest)
                {
                    currentAddress = currentsidequestAddress;
                }
                else if (finishedDialogue)
                {
                    currentAddress = 0x2064ED8A; //stu first normal hello (167)
                    TownCharacter.itsfinishedDialogueID = 167;
                }
            }
            else if (currentArea == 3)
            {
                currentAddress = 0x20649A56; //bonka's first normal hello dialogue
                if (isSidequest && !helloQuest)
                {
                    currentAddress = currentsidequestAddress;
                }
                else if (finishedDialogue)
                {
                    currentAddress = 0x2064E9B4; //brooke first normal hello (167)
                    TownCharacter.itsfinishedDialogueID = 167;
                }
            }
            else if (currentArea == 14)
            {
                currentAddress = 0x2064ADCA; //pickle's 1st message                   
            }
            else if (currentArea == 23)
            {
                currentAddress = 0x2064AE4A; //Aily's 1st message (id 240)
                if (isSidequest)
                {
                    currentAddress = 0x2064B11C; //yellow drops last message
                }
            }
            else if (currentArea == 38)
            {
                currentAddress = 0x20649784;
            }
            else if (currentArea == 42)
            {
                currentAddress = 0x20648FBA;
            }

            TownCharacter.characterIDData = characterIdData;

            if (currentDialogue != null)
            {
                byte[] dialogueArray = new byte[currentDialogue.Length * 2];
                int currentByte = 0;
                int dialogueAddress = currentAddress;
                for (int i = 0; i < currentDialogue.Length; i++)
                {
                    char character = currentDialogue[i];

                    for (int a = 0; a < gameCharacters.Length; a++)
                    {
                        if (character.Equals(gameCharacters[a]))
                        {
                            if (a > 120)
                            {
                                if (a == 121)
                                {
                                    value1 = BitConverter.GetBytes(250);
                                }
                                else if (a == 122)
                                {
                                    value1 = BitConverter.GetBytes(251);
                                }
                                else if (a == 123)
                                {
                                    value1 = BitConverter.GetBytes(252);
                                }
                                else if (a == 124)
                                {
                                    value1 = BitConverter.GetBytes(253);
                                }
                                else if (a == 125)
                                {
                                    value1 = BitConverter.GetBytes(254);
                                }
                                else if (a == 126)
                                {
                                    value1 = BitConverter.GetBytes(255);
                                }
                                else if (a == 127)
                                {
                                    value1 = BitConverter.GetBytes(2);
                                }
                            }
                            else
                            {
                                value1 = BitConverter.GetBytes(a);
                            }

                            break;
                        }
                    }


                    //Memory.WriteByte(currentAddress, value1[0]);
                    dialogueArray[currentByte] = value1[0];

                    currentAddress += 0x00000001;
                    currentByte++;

                    if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                    {
                        value1 = BitConverter.GetBytes(255);
                        //Memory.WriteByte(currentAddress, value1[0]);
                        dialogueArray[currentByte] = value1[0];
                    }
                    else if (value1[0] == 250 || value1[0] == 251 || value1[0] == 252 || value1[0] == 253 || value1[0] == 254 || value1[0] == 255)
                    {
                        value1 = BitConverter.GetBytes(250);
                        //Memory.WriteByte(currentAddress, value1[0]);
                        dialogueArray[currentByte] = value1[0];
                    }
                    else
                    {
                        value1 = BitConverter.GetBytes(253);
                        //Memory.WriteByte(currentAddress, value1[0]);
                        dialogueArray[currentByte] = value1[0];
                    }

                    currentAddress += 0x00000001;
                    currentByte++;
                }

                Memory.WriteByteArray(dialogueAddress, dialogueArray);

                Memory.WriteByte(currentAddress, 1);
                currentAddress += 0x00000001;
                Memory.WriteByte(currentAddress, 255);
            }
            else
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Currentdialogue is null!");
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "nearNPC");
        }

        public static void SetDefaultDialogue(int area) //this is used if player is too quick talking to NPC and the mod hasn't finished writing dialogue yet
        {
            string defDialogue = "Hello.";
            
            if (area == 0)
            {
                currentAddress = 0x206494C4;
            }
            else if (area == 1)
            {
                currentAddress = 0x20649B88;
            }
            else if (area == 2)
            {
                currentAddress = 0x20649B8A;
            }
            else if (area == 3)
            {
                currentAddress = 0x2065207C;
            }
            else if (area == 14)
            {
                if (isUsingAlly == true)
                {
                    currentAddress = 0x20649000; //brownboo first dialogue option
                    defDialogue = "Hey chill, don´t come talking so fast!";
                }

                else
                {
                    currentAddress = 0x20649000; //brownboo first dialogue option
                     if (Memory.ReadByte(0x21CE43FE) == 0  || Memory.ReadByte(0x21CE444B) == 0)
                        defDialogue = "Wait a second please,^I was doing something.";
                     else
                        defDialogue = "Checking your data... Please wait.";
                }
            }
            else if (area == 23)
            {
                currentAddress = 0x20648F50;
            }
            else if (area == 38)
            {
                currentAddress = 0x20649710; //ID 100, 54 characters
                defDialogue = "Wait a second please,^I was doing something.";
            }
            else if (area == 42)
            {
                currentAddress = 0x20648EC8;           
            }

            for (int i = 0; i < defDialogue.Length; i++)
            {
                char character = defDialogue[i];

                for (int a = 0; a < gameCharacters.Length; a++)
                {
                    if (character.Equals(gameCharacters[a]))
                    {
                        if (a == 127)
                        {
                            value1 = BitConverter.GetBytes(2);
                        }
                        else
                        {
                            value1 = BitConverter.GetBytes(a);
                        }

                        break;
                    }
                }


                Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });

                currentAddress += 0x00000001;

                if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                {
                    Memory.WriteOneByte(currentAddress, BitConverter.GetBytes(255));
                }
                else
                {                 
                    Memory.WriteOneByte(currentAddress, BitConverter.GetBytes(253));
                }

                currentAddress += 0x00000001;
            }

            Memory.WriteOneByte(currentAddress, BitConverter.GetBytes(1));
            currentAddress += 0x00000001;
            Memory.WriteOneByte(currentAddress, BitConverter.GetBytes(255));

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "nearNPC+SetDefaultDialogue");

        }

        /// <summary>The talk menu's quest line: present only while the NPC's quest is ongoing.</summary>
        static string QuestLine(QuestOffer offer) => offer.Phase == QuestPhase.Ongoing ? "^  " + offer.Label : "";

        /// <summary>The talk menu's lines for the area and building, with the quest line of the NPC at hand (<paramref name="offer"/>).</summary>
        public static void SetDialogueOptions(int currentArea, bool buildingCheck, QuestOffer offer = default)
        {
            bool dialogueSet = false;
            if (currentArea == 0)
            {
                if (buildingCheck == false) //if player is not inside (storage) house
                {
                    currentAddress = 0x206492F6; //norune dialogueoptions after event finish
                    dialogueOptions = "Hello.^  How should I rebuild Norune?^  It´s finished!" + QuestLine(offer);
                    dialogueSet = true;
                }
                else
                {
                    if (Memory.ReadByte(0x202A2820) == 5) //check for hag
                    {
                        currentAddress = 0x20649364; //can I check for items? dialogue
                        dialogueOptions = "   Can I check in some items?^  Hello.^  How should I rebuild Norune?^  It´s finished!";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered hag's");
                        dialogueSet = true;
                    }
                    else if (Memory.ReadInt(0x202A2820) == -1) 
                    {
                        currentAddress = 0x206492F6; //norune dialogueoptions after event finish
                        dialogueOptions = "Hello." + QuestLine(offer);
                        dialogueSet = true;
                    }
                    else
                    {
                        currentAddress = 0x206492F6; //norune dialogueoptions after event finish
                        dialogueOptions = "Hello.^  How should I rebuild Norune?^  It´s finished!" + QuestLine(offer);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (not hag's)");
                        dialogueSet = true;
                    }
                }
            }
            else if (currentArea == 1)
            {
                if (buildingCheck == false) //if player is not inside (storage) house
                {
                    currentAddress = 0x20649306; //matataki dialogueoptions after event finish
                    dialogueOptions = "Hello.^  How should I rebuild Matataki Village?^  It´s finished!" + QuestLine(offer);
                    dialogueSet = true;
                }
                else
                {
                    if (Memory.ReadByte(0x202A2820) == 5) //check for couscous
                    {
                        currentAddress = 0x2064938A; //can I check for items? dialogue
                        dialogueOptions = "  Can I check in some items?^  Hello.^  How should I rebuild Matataki Village?^  It´s finished!";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered couscous");
                        dialogueSet = true;
                    }
                    else
                    {
                        currentAddress = 0x20649306; //matataki dialogueoptions after event finish
                        dialogueOptions = "Hello.^  How should I rebuild Matataki Village?^  It´s finished!" + QuestLine(offer);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (not couscous)");
                        dialogueSet = true;
                    }
                }
            }
            else if (currentArea == 2)
            {
                if (buildingCheck == false) //if player is not inside (storage) house
                {
                    currentAddress = 0x206492DA; //queens dialogueoptions after event finish
                    dialogueOptions = "Hi.^  Any requests for rebuilding Queens?^  It´s finished!" + QuestLine(offer);
                    dialogueSet = true;
                }
                else
                {
                    if (Memory.ReadByte(0x202A2820) == 7) //check for basker
                    {
                        currentAddress = 0x20649354; //can I check for items? dialogue
                        dialogueOptions = "  Can I check in some items?^  Hello.^  Any requests for rebuilding Queens?^  It´s finished!";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered basker");
                        dialogueSet = true;
                    }
                    else
                    {
                        currentAddress = 0x206492DA; //queens dialogueoptions after event finish
                        dialogueOptions = "Hi.^  Any requests for rebuilding Queens?^  It´s finished!" + QuestLine(offer);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (not basker)");
                        dialogueSet = true;
                    }
                }
            }
            else if (currentArea == 3)
            {
                if (buildingCheck == false) //if player is not inside (storage) house
                {
                    currentAddress = 0x20649288; //muska dialogueoptions after event finish
                    dialogueOptions = "Hello.^  Any requests for building Muska Lacka?^  It´s finished!" + QuestLine(offer);
                    dialogueSet = true;
                }
                else
                {
                    if (Memory.ReadByte(0x202A2820) == 5) //check for basker
                    {
                        currentAddress = 0x2064930C; //can I check for items? dialogue
                        dialogueOptions = "  Can I check in some items?^  Hello.^  Any requests for building Muska Lacka?^  It´s finished!";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered Enga");
                        dialogueSet = true;
                    }
                    else
                    {
                        currentAddress = 0x20649288; //muska dialogueoptions after event finish
                        dialogueOptions = "Hi.^  Any requests for building Muska Lacka?^  It´s finished!" + QuestLine(offer);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (not enga)");
                        dialogueSet = true;
                    }
                }
            }
            else if (currentArea == 23)
            {
                if (buildingCheck == true)
                {
                    if (Memory.ReadByte(0x21D26FD4) == 0)
                    {
                        currentAddress = 0x20649004;
                        dialogueOptions = "Can I shop here?^  Hello.^  Do you have any sidequests?";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (shop)");
                        dialogueSet = true;
                    }
                    else if (Memory.ReadByte(0x21D26FD4) == 1)
                    {
                        currentAddress = 0x20649004;
                        dialogueOptions = "Can I check in some items?^  Hello.^  Do you have any sidequests?";
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered building (storage)");
                        dialogueSet = true;
                    }
                }
            }
            if (dialogueSet)
            {
                for (int i = 0; i < dialogueOptions.Length; i++)
                {
                    char character = dialogueOptions[i];

                    for (int a = 0; a < gameCharacters.Length; a++)
                    {
                        if (character.Equals(gameCharacters[a]))
                        {
                            if (a > 120)
                            {
                                if (a == 121)
                                {
                                    value1 = BitConverter.GetBytes(250);
                                }
                                else if (a == 122)
                                {
                                    value1 = BitConverter.GetBytes(251);
                                }
                                else if (a == 123)
                                {
                                    value1 = BitConverter.GetBytes(252);
                                }
                                else if (a == 124)
                                {
                                    value1 = BitConverter.GetBytes(253);
                                }
                                else if (a == 125)
                                {
                                    value1 = BitConverter.GetBytes(254);
                                }
                                else if (a == 126)
                                {
                                    value1 = BitConverter.GetBytes(255);
                                }
                                else if (a == 127)
                                {
                                    value1 = BitConverter.GetBytes(2);
                                }
                            }
                            else
                            {
                                value1 = BitConverter.GetBytes(a);
                            }

                            break;
                        }
                    }


                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });

                    currentAddress += 0x00000001;

                    if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                    {
                        value1 = BitConverter.GetBytes(255);
                        Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                    }
                    else if (value1[0] == 250 || value1[0] == 251 || value1[0] == 252 || value1[0] == 253 || value1[0] == 254 || value1[0] == 255)
                    {
                        value1 = BitConverter.GetBytes(250);
                        Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                    }
                    else
                    {
                        value1 = BitConverter.GetBytes(253);
                        Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                    }

                    currentAddress += 0x00000001;
                }

                Memory.WriteByte(currentAddress, 1);
                currentAddress += 0x00000001;
                Memory.WriteByte(currentAddress, 255);

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Custom dialogue options set!");
            }
            else
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "!!! Custom dialogue options were not set!");
            }
        }

        public static void SetStorageDialogue(int currentArea, bool inStorage)
        {
            if (inStorage)
            {
                if (currentArea == 0)
                {
                    if (storageOriginalDialogue != null)
                    {
                        Memory.WriteByteArray(0x2064C088, storageOriginalDialogue);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue written");
                    }
                }
                else if (currentArea == 1)
                {
                    if (storageOriginalDialogue != null)
                    {
                        Memory.WriteByteArray(0x2064C492, storageOriginalDialogue);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue written");
                    }
                }
                else if (currentArea == 2)
                {
                    if (storageOriginalDialogue != null)
                    {
                        Memory.WriteByteArray(0x2064DB3A, storageOriginalDialogue);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue written");
                    }
                }
                else if (currentArea == 3)
                {
                    if (storageOriginalDialogue != null)
                    {
                        Memory.WriteByteArray(0x2064DDB8, storageOriginalDialogue);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue written");
                    }
                }
                else if (currentArea == 23)
                {
                    storageOriginalDialogue = Memory.ReadByteArray(0x2064B11C, 200);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue stored");
                }
            }
            else
            {
                if (currentArea == 0)       storageOriginalDialogue = Memory.ReadByteArray(0x2064C088, 1000);

                else if (currentArea == 1)  storageOriginalDialogue = Memory.ReadByteArray(0x2064C492, 1000);

                else if (currentArea == 2)  storageOriginalDialogue = Memory.ReadByteArray(0x2064DB3A, 1000);

                else if (currentArea == 3)  storageOriginalDialogue = Memory.ReadByteArray(0x2064DDB8, 1000);

                else if (currentArea == 23)
                {
                    if (storageOriginalDialogue != null)
                    {
                        if (storageOriginalDialogue[0] != 0)
                        {
                            Memory.WriteByteArray(0x2064B11C, storageOriginalDialogue);
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue written");
                        }
                    }
                }

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Storage dialogue stored");
            }
        }

        public static void SetFishingDisabledDialogue(int area)
        {
            currentDialogue = "Only Ť is able to fish here.";

            if (area == 0)
            {
                currentAddress = 0x204334F6;
            }
            else if (area == 1)
            {
                currentAddress = 0x2042F628;
            }
            else if (area == 19)
            {
                currentAddress = 0x20429AD6;
            }
            else if (area == 3)
            {
                currentAddress = 0x204305B8;
            }

            for (int i = 0; i < currentDialogue.Length; i++)
            {
                char character = currentDialogue[i];

                for (int a = 0; a < gameCharacters.Length; a++)
                {
                    if (character.Equals(gameCharacters[a]))
                    {
                        if (a > 120)
                        {
                            if (a == 121)
                            {
                                value1 = BitConverter.GetBytes(250);
                            }
                            else if (a == 127)
                            {
                                value1 = BitConverter.GetBytes(2);
                            }
                        }
                        else
                        {
                            value1 = BitConverter.GetBytes(a);
                        }

                        break;
                    }
                }


                Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });

                currentAddress += 0x00000001;

                if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                {
                    value1 = BitConverter.GetBytes(255);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else if (value1[0] == 250)
                {
                    value1 = BitConverter.GetBytes(250);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else
                {
                    value1 = BitConverter.GetBytes(253);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }

                currentAddress += 0x00000001;
            }

            Memory.WriteByte(currentAddress, 1);
            currentAddress += 0x00000001;
            Memory.WriteByte(currentAddress, 255);

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Fishing disabled");
        }

        public static void FixFairyKingDialogue()
        {
            currentDialogue = "You can call your allies everywhere^in the world, but however...";
            currentAddress = 0x20425014;

            for (int i = 0; i < currentDialogue.Length; i++)
            {
                char character = currentDialogue[i];

                for (int a = 0; a < gameCharacters.Length; a++)
                {
                    if (character.Equals(gameCharacters[a]))
                    {
                        if (a > 120)
                        {
                            if (a == 121)
                            {
                                value1 = BitConverter.GetBytes(250);
                            }
                            else if (a == 127)
                            {
                                value1 = BitConverter.GetBytes(2);
                            }
                        }
                        else
                        {
                            value1 = BitConverter.GetBytes(a);
                        }

                        break;
                    }
                }


                Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });

                currentAddress += 0x00000001;

                if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                {
                    value1 = BitConverter.GetBytes(255);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else if (value1[0] == 250)
                {
                    value1 = BitConverter.GetBytes(250);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else
                {
                    value1 = BitConverter.GetBytes(253);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }

                currentAddress += 0x00000001;
            }

            Memory.WriteByte(currentAddress, 3);
            currentAddress += 0x00000001;
            Memory.WriteByte(currentAddress, 255);

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Fairy King (renee house) dialogue fixed");
        }

        public static void IntroTextAtNorune()
        {
            currentDialogue = "Wait...¤Have you already done^this before?¤Hmm... well,^whatever the case is...¤Prepare for a great journey,^or should I say...¤An Enhanced journey!!";
            currentAddress = 0x20370A4E;

            for (int i = 0; i < currentDialogue.Length; i++)
            {
                char character = currentDialogue[i];

                for (int a = 0; a < gameCharacters.Length; a++)
                {
                    if (character.Equals(gameCharacters[a]))
                    {
                        if (a > 120)
                        {
                            if (a == 121)
                            {
                                value1 = BitConverter.GetBytes(250);
                            }
                            else if (a == 127)
                            {
                                value1 = BitConverter.GetBytes(2);
                            }
                        }
                        else
                        {
                            value1 = BitConverter.GetBytes(a);
                        }

                        break;
                    }
                }


                Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });

                currentAddress += 0x00000001;

                if (value1[0] == 0 || value1[0] == 2 || value1[0] == 3)
                {
                    value1 = BitConverter.GetBytes(255);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else if (value1[0] == 250)
                {
                    value1 = BitConverter.GetBytes(250);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }
                else
                {
                    value1 = BitConverter.GetBytes(253);
                    Memory.WriteOneByte(currentAddress, new byte[] { value1[0] });
                }

                currentAddress += 0x00000001;
            }

            Memory.WriteByte(currentAddress, 1);
            currentAddress += 0x00000001;
            Memory.WriteByte(currentAddress, 255);

            //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Intro cutscene custom msg");
        }

        public static void ChangeDialogue()
        {
            if (customDialoguesCheck[savedDialogueCheck] != 1)  //change dialogue flag between 1st and 2nd dialogue
            {
                customDialoguesCheck[savedDialogueCheck] = 1;
            }
            else
            {
                customDialoguesCheck[savedDialogueCheck] = 0;
            }
        }

        public static void GetCurrentAreaFinishedDialogues(int area)
        {
            switch (area)
            {
                case 0:
                    itsfinishedDialogue = TownDialogueText.NoruneFinishedDialogue;
                    break;
                case 1:
                    itsfinishedDialogue = TownDialogueText.MatatakiFinishedDialogue;
                    break;
                case 2:
                    itsfinishedDialogue = TownDialogueText.QueensFinishedDialogue;
                    break;
                case 3:
                    itsfinishedDialogue = TownDialogueText.MuskaFinishedDialogue;
                    break;

            }
        }

        public static void FixCharacterNamesInDialogues() //replaces all mentions of Toan with correct ally, in some cases it doesn't fit well
        {
            int ally = AllySwapPrototype.CurrentAlly;                       // 0 Toan .. 5 Osmond
            byte charByte = (byte)(ally > 0 ? 250 + ally : 0);               // the name token: 251 Xiao .. 255 Osmond

            if (charByte > 250)
            {            
                 storageAllDialogues = Memory.ReadByteArray(0x20645000, 200000);
                 Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "started char fixing, all dialogue length: " + storageAllDialogues.Length);
                 for (int i = 0; i < storageAllDialogues.Length; i++)
                 {
                     if (storageAllDialogues[i] == 250)
                     {
                         i++;
                         if (storageAllDialogues[i] == 250)
                         {
                             i--;
                             storageAllDialogues[i] = charByte;
                             i++;
                         }
                     }
                     else
                     {
                         i++;
                     }
                 }
                 Memory.WriteByteArray(0x20645000, storageAllDialogues);
                 Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Finished char fixing");
                 
            }
        }

        public static void FixCharacterNamesInShopDialogues()
        {
            int ally = AllySwapPrototype.CurrentAlly;                       // 0 Toan .. 5 Osmond
            byte charByte = (byte)(ally > 0 ? 250 + ally : 0);               // the name token: 251 Xiao .. 255 Osmond

            if (charByte > 250)
            {
                storageAllDialogues = Memory.ReadByteArray(0x218229E0, 37000);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "started shop char fixing, all dialogue length: " + storageAllDialogues.Length);
                for (int i = 0; i < storageAllDialogues.Length; i++)
                {
                    if (storageAllDialogues[i] == 250)
                    {
                        i++;
                        if (storageAllDialogues[i] == 250)
                        {
                            i--;
                            storageAllDialogues[i] = charByte;
                            i++;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
                Memory.WriteByteArray(0x218229E0, storageAllDialogues);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Finished shop char fixing");
            }

        }

    }
}
