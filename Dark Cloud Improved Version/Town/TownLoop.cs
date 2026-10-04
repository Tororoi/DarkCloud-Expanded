using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The town-mode branch of the game loop (<see cref="GameLoop"/> calls <see cref="Tick"/> only while the game
    /// mode is 2) plus the per-tick town features it calls in every mode (<see cref="TickFeatures"/>).
    /// One tick, in order: the Allies-menu .chr rewrite (<see cref="AllySwitch"/>); the landing-animation cancel;
    /// the ally branch (house-completion gate, Joker's door, the trade bunny, event-point/Yaya/mayor-door
    /// mailboxes, the villager-touch dialogue scan, Xiao's talk camera, the shopkeeper / sidequest / "it's finished"
    /// dialogue options) or the Toan branch (sidequest options in the four towns and Yellow Drops, Brownboo's own
    /// NPC scan with the master-fish reward); area-entry work (clock, default dialogues, storage dialogue reset,
    /// the character-name fix thread); inside/outside building dialogue options; the area-exit swap back to Toan;
    /// the pad log and the fish-farmer L3 toggle; the fishing session (<see cref="Fishing.Tick"/>); shop entry
    /// (broken-dagger fix, shop name fix); the weapon level-up check, the Demon Shaft gate, the synth-sphere
    /// listener; the daily shop reroll.
    /// </summary>
    internal static class TownLoop
    {
        private const string Tag = "[TownLoop] ";

        static int currentAddress;
        /// <summary>Per-area id of the mod's custom talk message (index = area id).</summary>
        static byte[] townDialogueIDs = { 247, 167, 87, 27, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 200, 0, 0, 0, 0, 0, 0, 0, 0, 240, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 101, 0, 0, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        static bool isUsingAlly = false;
        // Kept as a diagnostic switch only. The real Brownboo fishing crash was engine-side — the event-mode
        // NPC stepper calling vtables on villagers the session had freed — fixed by suspending the villager
        // COUNT during the session (CustomFishingSpot.UpdateFishingWindow). Brownboo walking dialogue works
        // normally again; the TownDialogueSuspended() gate below still keeps the mod's own villager scans
        // quiet during the event/fishing window (correct regardless).
        internal static bool DisableBrownbooDialogue = false;

        /// <summary>
        /// TRUE only during an actual fishing session — when the villager buffer has been freed (cmd 38) and
        /// the mod's FIVE villager scanners (0x21D26FF8 + i*0x14A0) would read freed-then-overwritten memory.
        /// Keyed to CustomFishingSpot's precise fishing-window (running-event id EdEventInfo == our fishing
        /// labels), NOT to plain event mode — so ordinary town dialogue, sidequest scenes and the collection
        /// reward (which also run in event mode, with villagers intact) keep working normally.
        /// </summary>
        static bool TownDialogueSuspended() => CustomFishingSpot.InFishingWindow;

        static bool jokerHouse = false;
        static bool nearNPC = false;
        static bool nearNPCSD = false;
        static bool checkBuildingFlag = false;
        static bool areaChanged = false;
        static bool sidequestOptionFlag = false;
        static bool itsfinishedOptionFlag = false;
        static bool isSideQuestDialogueActive = false;
        static bool _prevL3 = false;
        static int _prevButtonRead = 0;
        static bool currentlyInShop = false;
        static bool shopDataCleared = false;
        static bool demonshaftUnlocked = false;
        static bool areaEnteredClockCheck = false;
        static bool areaEnteredCheck = false;
        static bool mintTalk = false;

        static int onDialogueFlag = 0;
        static int sidequestonDialogueFlag = 0;
        static int itsfinishedonDialogueFlag = 0;
        static int currentHouseID;
        static int checkCompletion;
        static int partsCollected = 0;
        static int currentArea;
        static int buildingCheck;
        static int currentInGameDay = 0;
        static Thread characterNamesFixThread = new Thread(() => Dialogues.FixCharacterNamesInDialogues());

        //The following comments are for various flags that we utilize within unused game memory
        //used bool checks in addresses: 21F10000,21F10004,21F10008 (check if player is next to NPC), 21F1000C,
        //21F10010 (toan next to pickle in brownboo), 21F10014, 21F10018 (element check), 21F1001C (clock check),
        //21F10020 (PNACH flag), 21F10024 (mod flag), 21F10028 (Option 1 Flag), 21F1002C (Option 2 Flag), 21F10030 (Option 3 Flag), 21F10034 (Option 4 Flag)

        /// <summary>Game-loop start-up: latch the in-game day the daily-shop reroll compares against and re-arm the
        /// Demon Shaft gate.</summary>
        internal static void Init()
        {
            currentInGameDay = Memory.ReadUShort(0x21CD4318);
            demonshaftUnlocked = false;
        }

        /// <summary>One town-mode tick (the caller has already read mode == 2).</summary>
        internal static void Tick()
        {
            AllySwitch.TickAlliesMenu();

            if (Memory.ReadByte(0x21D33E28) == 9 && Fishing.SessionActive == false)   //cancel landing animation to avoid being stuck
            {
                Memory.WriteByte(0x21D33E30, 3);
            }

            if (Memory.ReadInt(0x2029AA0E) != 1680945251)   //If not using Toan, force any house event to be cancelled
            {
                isUsingAlly = true;
                currentHouseID = Memory.ReadByte(0x202A2820);

                if (currentHouseID != 255)  //check if its actual georama house
                {
                    checkCompletion = 0xE8 * currentHouseID + 0x21D19C58;

                    if (Memory.ReadByte(checkCompletion) == 0)  //checks if house has been completed when opening the door
                    {
                        int parts = 4;
                        checkCompletion = 0xE8 * currentHouseID + 0x21D19C80;

                        if (Memory.ReadByte(0x202A2518) == 0) //check if claude's house
                        {
                            if (currentHouseID == 4)
                            {
                                parts = 5;
                            }
                        }
                        else if (Memory.ReadByte(0x202A2518) == 1) //check if cacaos house
                        {
                            if (currentHouseID == 1)
                            {
                                parts = 5;
                            }
                        }


                        for (int i = 0; i < parts; i++)
                        {
                            partsCollected += Memory.ReadByte(checkCompletion);
                            checkCompletion += 0x20;
                        }

                        if ((parts == 4 && partsCollected == 4) || (parts == 5 && partsCollected == 5))
                        {
                            Memory.WriteByte(0x202A282C, 0);
                        }
                        else
                        {
                            Memory.WriteByte(0x202A282C, 128);
                        }
                        partsCollected = 0;
                    }
                    else
                    {
                        Memory.WriteByte(0x202A282C, 128);
                    }

                    if (Memory.ReadByte(0x202A2518) == 2)
                    {
                        if (Memory.ReadByte(0x21D196A4) == 4)
                        {
                            if (Memory.ReadByte(0x21D19FF8) != 1)
                            {
                                if (Memory.ReadByte(0x21D19710) == 1 && jokerHouse == false) //check if at joker's house door (INCONSISTENT, DOESN'T WORK ALL TIMES)
                                {
                                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "entered jokers");
                                    Memory.WriteByte(0x202A2A08, 0);
                                    jokerHouse = true;
                                }
                                else if (Memory.ReadByte(0x21D19710) == 0 && jokerHouse == true)
                                {
                                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "left jokers");
                                    Memory.WriteByte(0x202A2A08, 1);
                                    jokerHouse = false;
                                }
                            }
                        }
                    }

                }
                else
                {
                    Memory.WriteByte(0x202A282C, 128);
                }

                if (Memory.ReadByte(0x202A2518) == 23)
                {
                    if (Memory.ReadByte(0x21D28474) == 8)
                    {
                        Memory.WriteByte(0x21D2849C, 0); //despawn trade quest bunny
                    }
                    else
                    {
                        Memory.WriteByte(0x21D2849C, 1);
                    }
                }
                currentArea = Memory.ReadByte(0x202A2518);
                if (currentArea == 11 || currentArea == 13 || currentArea == 33 || currentArea == 35 || currentArea == 37 || currentArea == 14)
                {
                    Memory.WriteByte(Mailbox.EventPoint, 1); //disable eventpoints/triggers, pnach does the rest
                }
                else
                {
                    Memory.WriteByte(Mailbox.EventPoint, 0);
                }


                if (Memory.ReadByte(0x21CDD80D) != 255)
                {
                    Memory.WriteByte(Mailbox.SunMoon, 1); //enable yaya
                }

                if (Memory.ReadByte(Mailbox.InsideMayor) == 1)
                {
                    Memory.WriteByte(0x20415508, 0); //disable mayor door event
                    Memory.WriteByte(0x20415538, 0); //disable mayor door event mark
                }

                // Skip the villager-touching dialogue scan when the villager buffer may be freed
                // (event/fishing mode) or when Brownboo dialogue is disabled by the bisect switch.
                if (!TownDialogueSuspended() && !(currentArea == 14 && DisableBrownbooDialogue))
                {
                int checkNearNPC = 0;

                for (int i = 0; i < 6; i++)     //check if player is next to a character. If so, jumps to SetDialogue() and writes the dialogues
                {
                    //this following dialogue handling is a bit of mess, but it works fine and I don't want to break it lol
                    currentAddress = i * 0x14A0 + 0x21D26FF8;
                    if (Memory.ReadByte(currentAddress) == 1)
                    {
                        if (nearNPC == false || onDialogueFlag == 1)
                        {
                            Dialogues.SetDialogue(i, true, false);
                            if (TownCharacter.talkableNPC != false) //check if NPC is not llama
                            {
                                Memory.WriteByte(Mailbox.NearNpc, 1); //nearNPC flag for PNACH to use
                            }
                            TownCharacter.talkableNPC = true;
                            nearNPC = true;
                            if (onDialogueFlag == 1) onDialogueFlag = 2;    //if player was already on a dialogue, the next one was written ready
                        }
                        checkNearNPC++;
                    }
                }
                if (checkNearNPC == 0)
                {
                    nearNPC = false;
                    Memory.WriteByte(Mailbox.NearNpc, 0); //nearNPC flag for PNACH to use
                    onDialogueFlag = 0;
                }
                }

                if (Memory.ReadByte(0x21D1CC0C) == townDialogueIDs[currentArea] && onDialogueFlag == 0) //check if current dialogue is our custom dialogue, set a flag
                {
                    onDialogueFlag = 1;
                    Dialogues.ChangeDialogue(); //when we detect that player activates a dialogue, change the flag
                }
                else if (Memory.ReadByte(0x21D1CC0C) == townDialogueIDs[currentArea] && onDialogueFlag == 3) //check if current dialogue is our custom dialogue, set a flag
                {
                    onDialogueFlag = 0;
                }

                if (onDialogueFlag == 2)
                {
                    if (Memory.ReadByte(0x21D1CC0C) == 255) //check if previous custom dialogue has ended
                    {
                        onDialogueFlag = 3;
                    }
                }

                if (Memory.ReadInt(0x2029AA18) == 1882468451)   //if using Xiao, change talk camera
                {
                    switch (currentArea)
                    {
                        case 0:     //Norune
                            Memory.WriteByte(0x202A2A6C, 0);
                            Memory.WriteByte(0x202A2A6E, 9);
                            break;
                        case 1:     //Matataki
                            Memory.WriteUShort(0x202A2A6C, 2544);
                            Memory.WriteByte(0x202A2A6E, 9);
                            break;
                        case 2:     //Queens
                            Memory.WriteUShort(0x202A2A6C, 2306);
                            Memory.WriteByte(0x202A2A6E, 9);
                            break;
                        case 3:     //Muska Lacka
                            Memory.WriteUShort(0x202A2A6C, 2306);
                            Memory.WriteByte(0x202A2A6E, 9);
                            break;
                        case 14:    //Brownboo
                            Memory.WriteUShort(0x202A2A6C, 6);
                            Memory.WriteByte(0x202A2A6E, 0);
                            break;
                        case 23:    //Yellow Drops
                            Memory.WriteUShort(0x202A2A6C, 55);
                            Memory.WriteByte(0x202A2A6E, 6);
                            break;
                        case 38:     //Dark Haven Castle
                            Memory.WriteUShort(0x202A2A6C, 48);
                            Memory.WriteByte(0x202A2A6E, 7);
                            break;
                        case 42:     //Muska Lacka (outside)
                            Memory.WriteUShort(0x202A2A6C, 72);
                            Memory.WriteByte(0x202A2A6E, 6);
                            break;
                    }

                    Memory.WriteByte(Mailbox.XiaoFlag, 1); //xiaoFlag for PNACH
                }
                else
                {
                    Memory.WriteByte(Mailbox.XiaoFlag, 0); //xiaoFlag for PNACH
                }

                if (TownCharacter.shopkeeper == true) //check for shopkeeper and change dialogue ID, this part is a bit poorly written and could be cleaner
                {
                    if (currentArea != 23)
                    {
                        Memory.WriteUShort(0x21D3D438, townDialogueIDs[currentArea]);

                        if (itsfinishedOptionFlag == false && (Memory.ReadByte(0x21D1CC0C) == 12 || Memory.ReadByte(0x21D1CC0C) == 13))
                        {
                            itsfinishedOptionFlag = true;
                        }
                        else if (itsfinishedOptionFlag == true && Memory.ReadByte(0x21D1CC0C) == 255)
                        {
                            itsfinishedOptionFlag = false;
                        }

                        if (itsfinishedOptionFlag == true)
                        {
                            Memory.WriteInt(0x21D3D440, TownCharacter.itsfinishedDialogueID); //its finished dialogue ID setup
                            SetItsFinishedDialogue();
                        }
                        else
                        {
                            itsfinishedonDialogueFlag = 0;
                        }
                    }
                    else
                    {
                        if (sidequestOptionFlag == false && Memory.ReadByte(0x21D1CC0C) == 12)
                        {
                            sidequestOptionFlag = true;
                        }
                        else if (sidequestOptionFlag == true && Memory.ReadByte(0x21D1CC0C) == 255)
                        {
                            sidequestOptionFlag = false;
                        }

                        if (sidequestOptionFlag == true)
                        {
                            Memory.WriteInt(0x21D3D43C, TownCharacter.sidequestDialogueID); //THIS IS USED FOR POSSIBLE 4TH DIALOGUE OPTION (sidequests)
                            SetSideQuestDialogue();

                            if (Memory.ReadUShort(0x21D1CC0C) == TownCharacter.sidequestDialogueID && isSideQuestDialogueActive == false)
                            {
                                CheckSideQuestDialogue();
                                isSideQuestDialogueActive = true;
                            }
                            else if (Memory.ReadUShort(0x21D1CC0C) != TownCharacter.sidequestDialogueID)
                            {
                                isSideQuestDialogueActive = false;
                            }
                        }
                        else
                        {
                            sidequestonDialogueFlag = 0;
                            itsfinishedonDialogueFlag = 0;
                        }
                    }
                }
                else
                {
                    if (currentArea != 38 || currentArea != 19)
                    {
                        Memory.WriteUShort(0x21D3D434, townDialogueIDs[currentArea]);
                    }
                    if (sidequestOptionFlag == false && (Memory.ReadByte(0x21D1CC0C) == 11 || (currentArea == 2 && Memory.ReadByte(0x21D1CC0C) == 15)))
                    {
                        sidequestOptionFlag = true;
                    }
                    else if (sidequestOptionFlag == true && Memory.ReadByte(0x21D1CC0C) == 255)
                    {
                        sidequestOptionFlag = false;
                    }

                    if (sidequestOptionFlag == true)
                    {
                        if (Memory.ReadByte(Mailbox.InsideMayor) == 1)
                        {
                            Memory.WriteInt(0x21D3D438, TownCharacter.sidequestDialogueID);
                        }
                        else
                        {
                            Memory.WriteInt(0x21D3D440, TownCharacter.sidequestDialogueID); //THIS IS USED FOR POSSIBLE 4TH DIALOGUE OPTION (sidequests)
                            Memory.WriteInt(0x21D3D43C, TownCharacter.itsfinishedDialogueID); //its finished dialogue ID setup
                        }
                        SetSideQuestDialogue();
                        SetItsFinishedDialogue();

                        if (Memory.ReadUShort(0x21D1CC0C) == TownCharacter.sidequestDialogueID && isSideQuestDialogueActive == false)
                        {
                            CheckSideQuestDialogue();
                            isSideQuestDialogueActive = true;
                        }
                        else if (Memory.ReadUShort(0x21D1CC0C) != TownCharacter.sidequestDialogueID)
                        {
                            isSideQuestDialogueActive = false;
                        }
                    }
                    else
                    {
                        sidequestonDialogueFlag = 0;
                        itsfinishedonDialogueFlag = 0;
                    }
                }

            }
            else //after the massive if check for ally usage, some custom dialogue is set for Toan
            {
                isUsingAlly = false;
                Memory.WriteByte(Mailbox.EventPoint, 0); //re-enable eventpoints if they were disable
                Memory.WriteByte(Mailbox.XiaoFlag, 0); //xiaoFlag for PNACH

                //if (Memory.ReadByte(0x21D1CC0C) == 12)

                currentArea = Memory.ReadByte(0x202A2518);
                if (currentArea == 0 || currentArea == 1 || currentArea == 2 || currentArea == 3)
                {
                    if (sidequestOptionFlag == false && Memory.ReadByte(0x21D1CC0C) == 11)
                    {
                        sidequestOptionFlag = true;
                    }
                    else if (sidequestOptionFlag == true && Memory.ReadByte(0x21D1CC0C) == 255)
                    {
                        sidequestOptionFlag = false;
                    }
                    if (sidequestOptionFlag == true)
                    {
                        if (Memory.ReadByte(Mailbox.InsideMayor) == 1)
                        {
                            Memory.WriteInt(0x21D3D438, TownCharacter.sidequestDialogueID);
                        }
                        else
                        {
                            Memory.WriteInt(0x21D3D440, TownCharacter.sidequestDialogueID); //THIS IS USED FOR POSSIBLE 4TH DIALOGUE OPTION (sidequests)
                        }
                        SetSideQuestDialogue();

                        if (Memory.ReadUShort(0x21D1CC0C) == TownCharacter.sidequestDialogueID && isSideQuestDialogueActive == false)
                        {
                            CheckSideQuestDialogue();
                            isSideQuestDialogueActive = true;
                        }
                        else if (Memory.ReadUShort(0x21D1CC0C) != TownCharacter.sidequestDialogueID)
                        {
                            isSideQuestDialogueActive = false;
                        }
                    }
                    else
                    {
                        sidequestonDialogueFlag = 0;
                    }
                }
                else if (currentArea == 23)
                {
                    if (sidequestOptionFlag == false && Memory.ReadByte(0x21D1CC0C) == 12)
                    {
                        sidequestOptionFlag = true;
                    }
                    else if (sidequestOptionFlag == true && Memory.ReadByte(0x21D1CC0C) == 255)
                    {
                        sidequestOptionFlag = false;
                    }

                    if (sidequestOptionFlag == true)
                    {
                        Memory.WriteInt(0x21D3D43C, TownCharacter.sidequestDialogueID); //THIS IS USED FOR POSSIBLE 4TH DIALOGUE OPTION (sidequests)
                        SetSideQuestDialogue();

                        if (Memory.ReadUShort(0x21D1CC0C) == TownCharacter.sidequestDialogueID && isSideQuestDialogueActive == false)
                        {
                            CheckSideQuestDialogue();
                            isSideQuestDialogueActive = true;
                        }
                        else if (Memory.ReadUShort(0x21D1CC0C) != TownCharacter.sidequestDialogueID)
                        {
                            isSideQuestDialogueActive = false;
                        }
                    }
                    else
                    {
                        sidequestonDialogueFlag = 0;
                    }
                }
                else if (currentArea == 14 && !DisableBrownbooDialogue && !TownDialogueSuspended()) //Brownboo is the only area where Toan has custom dialogue (outside of sidequests), so this special part is needed
                {
                    int checkNearNPC = 0;
                    for (int i = 0; i < 6; i++)     //check if player is next to a character. If so, jumps to SetDialogue() and writes the dialogues
                    {
                        currentAddress = i * 0x14A0 + 0x21D26FF8;
                        if (Memory.ReadByte(currentAddress) == 1)
                        {
                            if (nearNPC == false || onDialogueFlag == 1)
                            {

                                Dialogues.SetDialogue(i, false, false);
                                Memory.WriteByte(Mailbox.NearNpc2, 1); //nearNPC flag for PNACH to use
                                nearNPC = true;
                                if (onDialogueFlag == 1) onDialogueFlag = 2;
                            }
                            checkNearNPC++;
                            currentAddress = currentAddress - 0x00000024;
                            if (Memory.ReadByte(currentAddress) == 5)
                            {
                                mintTalk = true;
                            }
                            else
                            {
                                mintTalk = false;
                            }
                        }
                    }

                    if (checkNearNPC == 0)
                    {
                        nearNPC = false;
                        Memory.WriteByte(Mailbox.NearNpc2, 0); //nearNPC flag for PNACH to use
                        onDialogueFlag = 0;
                    }

                    if (Memory.ReadByte(0x21D1CC0C) == 200 && onDialogueFlag == 0) //check if current dialogue is our custom dialogue, set a flag
                    {
                        onDialogueFlag = 1;
                        if (mintTalk)
                        {
                            if (Memory.ReadByte(0x21CE444F) == 1)
                            {
                                if (MasterFishQuest.alreadyHasSavingBook == false)
                                {
                                    MasterFishQuest.GiveMasterFishQuestReward();
                                }
                            }
                        }
                    }

                    if (onDialogueFlag == 2)
                    {
                        if (Memory.ReadByte(0x21D1CC0C) == 255) //check if previous custom dialogue has ended
                        {
                            onDialogueFlag = 0;
                        }
                    }
                }
            }  //END OF CHARACTER RELATED DIALOGUE SETUP

            buildingCheck = Memory.ReadByte(0x202A281C); //is player inside house?

            int currentAreaFrames = Memory.ReadInt(0x202A2880);

            if (currentAreaFrames < 25)
            {
                if (currentAreaFrames > 5)
                {
                    if (!areaEnteredClockCheck) //Enables clock for areas that dont originally have it (yellow drops & dark heaven)
                    {
                        CheckClockAdvancement(currentArea);
                        TownCharacter.shopkeeper = false;
                        areaEnteredClockCheck = true;
                    }
                }
            }
            else
            {
                areaEnteredClockCheck = false;
            }

            if (currentAreaFrames < 50) //check player duration in new area (to check if its a new/changed area)
            {
                if (currentAreaFrames > 30 && areaChanged == false)
                {
                    if (!areaEnteredCheck) //Initializes some data when entering an area for the first time, might do it multiple times but its fine, just spams the console a bit
                    {
                        areaChanged = true;
                        CheckAllyFishing();
                        if (currentArea == 42) Dialogues.SetDefaultDialogue(42);
                        else if (currentArea == 14 && !DisableBrownbooDialogue) Dialogues.SetDefaultDialogue(14);

                        if (currentArea == 23)
                        {
                            if (Dialogues.storageOriginalDialogue != null)
                            {
                                if (Dialogues.storageOriginalDialogue.Length > 0)
                                {
                                    Array.Clear(Dialogues.storageOriginalDialogue, 0, Dialogues.storageOriginalDialogue.Length);
                                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Cleared storage original dialogue");
                                }
                            }
                        }
                        if (!characterNamesFixThread.IsAlive)
                        {
                            characterNamesFixThread = new Thread(() => Dialogues.FixCharacterNamesInDialogues());
                            characterNamesFixThread.Start();
                        }
                        areaEnteredCheck = true;
                    }
                }
            }
            else
            {
                areaChanged = false;
                areaEnteredCheck = false;
            }

            if ((buildingCheck == 0 && checkBuildingFlag == true) || areaChanged == true) //check if player is not inside a house
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Currently in outside area");
                Dialogues.SetDialogueOptions(currentArea, false);
                Dialogues.SetStorageDialogue(currentArea, false);
                checkBuildingFlag = false;
                CheckAllyFishing();

            }
            else if (buildingCheck == 1 && checkBuildingFlag == false)
            {
                if (currentArea != 23)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Currently inside building");
                    Dialogues.SetDialogueOptions(currentArea, true);
                    Dialogues.SetStorageDialogue(currentArea, true);
                    checkBuildingFlag = true;

                    if (currentArea == 0 && currentHouseID == 0) //renee house
                    {
                        if (Memory.ReadUShort(0x20425014) == 64820)
                        {
                            Dialogues.FixFairyKingDialogue(); //just to change one dialogue...
                        }
                    }
                }
                else
                {
                    if (Memory.ReadByte(0x21D26FD4) == 0 || Memory.ReadByte(0x21D26FD4) == 1)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Currently inside building");
                        Dialogues.SetDialogueOptions(currentArea, true);
                        Dialogues.SetStorageDialogue(currentArea, true);
                        checkBuildingFlag = true;
                    }
                }
            }

            AllySwitch.TickLocationChange();

            int buttonRead = Memory.ReadInt(0x21CBC544);
            bool l3Down = (buttonRead & 512) != 0;
            if (l3Down && !_prevL3) FishDataFarmer.Toggle();
            _prevL3 = l3Down;
            if (buttonRead != _prevButtonRead && !FishDataFarmer.IsPressingButton)
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Input] Player buttons: {FormatButtons(buttonRead)}");
            _prevButtonRead = buttonRead;

            Fishing.Tick(currentArea);

            if (!currentlyInShop)
            {
                if (Memory.ReadByte(0x21DA52E4) == 1 && Memory.ReadByte(0x21DA52E8) == 11)
                {
                    currentlyInShop = true;
                    shopDataCleared = false;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Entered a shop");
                }
            }

            if (currentlyInShop)
            {
                if (!shopDataCleared)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Fixing broken dagger glitch...");
                    FixBrokenDagger();
                    shopDataCleared = true;
                    Dialogues.FixCharacterNamesInShopDialogues();
                }

                if (Memory.ReadByte(0x21DA52E4) != 1)
                {
                    currentlyInShop = false;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Exited a shop");
                }
            }

            Dungeon.CheckWepLvlUp(); //check if player is upgrading a weapon

            DemonShaftUnlockCheck(); //prevents players from accessing post-game dungeon before completing the base game

            //Check if player is inside the weapon customize menu
            if (Player.CheckIsWeaponCustomizeMenu())
            {
                //The Synthsphere Listener thread
                if (Weapons.weaponsMenuListener.ThreadState == ThreadState.Unstarted)
                {
                    Weapons.weaponsMenuListener.Start();
                }
                else if (Weapons.weaponsMenuListener.ThreadState == ThreadState.Stopped)
                {
                    Weapons.weaponsMenuListener = new Thread(new ThreadStart(Weapons.WeaponListenForSynthSphere));
                    Weapons.weaponsMenuListener.Start();
                }
            }

            if (Memory.ReadUShort(0x21CD4318) > currentInGameDay) //whenever the ingame day counter increases, reroll the shops
            {
                DailyShopItem.RerollDailyRotation(currentInGameDay);
                currentInGameDay = Memory.ReadUShort(0x21CD4318);
            }
        }

        /// <summary>The per-tick town features. Called in every mode; each one gates itself to town.</summary>
        internal static void TickFeatures()
        {
            TownEditMode.Tick();       //overhead camera + safe exit, every town
            AllySwapPrototype.Tick();  //PROTOTYPE: in-place ally model swap (R3 → Ungaga), no reload
            TownIdleSit.Tick();        //idle→sit for the swapped-in cat (arms the ElfPatches idle-motion cave)
            TownLadder.Tick();         //block ladder mounts for non-Toan allies (Toan-rigged climb → crash)
            CustomFishingSpot.Tick();  //inject a fishing spot into Queens / Brownboo / Yellow Drops
            FishingCatchCamera.Tick(); //the fishing camera's distance/yaw put back after a catch's close-up
            CanalTide.Tick();          //Queens canal water + ripple rise/fall with the day-night clock
            TownCameraPolyBuffer.Tick(); //relocate+enlarge the camera gather arena (fixes the 400-poly overrun)
        }

        /// <summary>Zeroes the 18000-byte shop buffer the broken-dagger glitch reads from (on shop entry).</summary>
        public static void FixBrokenDagger()
        {
            currentAddress = 0x21839528;
            byte[] arrayy = new byte[18000];

            Memory.WriteByteArray(currentAddress, arrayy);

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Broken dagger fix finished");
        }


        public static void SetSideQuestDialogue()
        {
            if (TownDialogueSuspended()) return;    // villager buffer may be freed during a fishing session
            int checkNearNPC = 0;
            for (int i = 0; i < 6; i++)     //check if player is next to a character. If so, jumps to SetDialogue() and writes the dialogues
            {
                currentAddress = i * 0x14A0 + 0x21D26FF8;
                if (Memory.ReadByte(currentAddress) == 1)
                {
                    if (sidequestonDialogueFlag == 0)
                    {

                        Dialogues.SetDialogue(i, false, true);
                        Memory.WriteByte(Mailbox.NearNpc2, 1); //nearNPC flag for PNACH to use
                        nearNPCSD = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "sidequestdialogue set");
                        sidequestonDialogueFlag = 1;
                    }
                    checkNearNPC++;
                }
            }
        }

        public static void SetItsFinishedDialogue()
        {
            if (TownDialogueSuspended()) return;    // villager buffer may be freed during a fishing session
            int checkNearNPC = 0;
            for (int i = 0; i < 6; i++)     //check if player is next to a character. If so, jumps to SetDialogue() and writes the dialogues
            {
                currentAddress = i * 0x14A0 + 0x21D26FF8;
                if (Memory.ReadByte(currentAddress) == 1)
                {
                    if (itsfinishedonDialogueFlag == 0)
                    {
                        Dialogues.SetDialogue(i, false, false, true);
                        Memory.WriteByte(Mailbox.NearNpc2, 1); //nearNPC flag for PNACH to use
                        nearNPCSD = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "its finished dialogue set");
                        itsfinishedonDialogueFlag = 1;
                    }
                    checkNearNPC++;
                }
            }
        }

        /// <summary>While an ally is loaded, the native fishing spots (Norune, Matataki, Queens' harbour incl. the
        /// submarine, Muska Lacka) are disabled and their prompt replaced with "Only Toan is able to fish here."</summary>
        public static void CheckAllyFishing()
        {
            if (isUsingAlly)
            {
                if (currentArea == 0)
                {
                    Memory.WriteOneByte(0x2041BF4E, BitConverter.GetBytes(1)); //disable fishing
                    Dialogues.SetFishingDisabledDialogue(currentArea);
                }
                else if (currentArea == 1)
                {
                    Memory.WriteOneByte(0x2041AABA, BitConverter.GetBytes(1)); //disable fishing
                    Dialogues.SetFishingDisabledDialogue(currentArea);
                }
                else if (currentArea == 19)
                {
                    Memory.WriteOneByte(0x2041495E, BitConverter.GetBytes(1)); //disable fishing
                    Dialogues.SetFishingDisabledDialogue(currentArea);
                    Memory.WriteByte(0x20420B6C, 0); //disable submarine
                    Memory.WriteByte(0x20420B7C, 0); //disable submarine
                }
                else if (currentArea == 3)
                {
                    Memory.WriteOneByte(0x20421A8A, BitConverter.GetBytes(1)); //disable fishing
                    Dialogues.SetFishingDisabledDialogue(currentArea);
                }
            }
        }

        /// <summary>
        /// Runs once when the sidequest dialogue of the NPC the player is talking to comes up: advances that NPC's
        /// quest state byte (offered → accepted → rewarded via <see cref="SideQuestManager"/>), and in Yellow Drops
        /// hands out the Magical Crystal / Map challenge rewards.
        /// </summary>
        public static void CheckSideQuestDialogue()
        {
            sidequestOptionFlag = false;

            if (TownCharacter.characterIDData == 12592) //macho sidequest
            {
                if (Memory.ReadByte(0x21CE4474) == 1)
                {
                    if (Memory.ReadByte(0x21CE4402) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4402) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4402) == 2)
                    {
                        SideQuestManager.MonsterQuestReward();
                        Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4474, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13618) //gob sidequest
            {
                if (Memory.ReadByte(0x21CE4476) == 1)
                {
                    if (Memory.ReadByte(0x21CE4407) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4407, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4407) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4407) == 2)
                    {
                        SideQuestManager.MonsterQuestReward();
                        Memory.WriteOneByte(0x21CE4407, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4476, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13108) //jake sidequest
            {
                if (Memory.ReadByte(0x21CE4478) == 1)
                {
                    if (Memory.ReadByte(0x21CE440C) == 0)
                    {
                        Memory.WriteOneByte(0x21CE440C, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE440C) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE440C) == 2)
                    {
                        SideQuestManager.MonsterQuestReward();
                        Memory.WriteOneByte(0x21CE440C, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4478, 1);
                }
            }
            else if (TownCharacter.characterIDData == 14388) //chiefbonka sidequest
            {
                if (Memory.ReadByte(0x21CE447A) == 1)
                {
                    if (Memory.ReadByte(0x21CE4411) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4411, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4411) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE4402, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4411) == 2)
                    {
                        SideQuestManager.MonsterQuestReward();
                        Memory.WriteOneByte(0x21CE4411, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE447A, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13872) //pike
            {
                if (Memory.ReadByte(0x21CE4475) == 1)
                {
                    if (Memory.ReadByte(0x21CE4416) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4416, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4416) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE4416, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4416) == 2)
                    {
                        SideQuestManager.GetFishingQuestReward();
                        Memory.WriteOneByte(0x21CE4416, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4475, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13362) //pao
            {
                if (Memory.ReadByte(0x21CE4477) == 1)
                {
                    if (Memory.ReadByte(0x21CE441E) == 0)
                    {
                        Memory.WriteOneByte(0x21CE441E, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE441E) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE441E, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE441E) == 2)
                    {
                        SideQuestManager.GetFishingQuestReward();
                        Memory.WriteOneByte(0x21CE441E, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4477, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13363) //sam
            {
                if (Memory.ReadByte(0x21CE4479) == 1)
                {
                    if (Memory.ReadByte(0x21CE4427) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4427, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4427) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE441E, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4427) == 2)
                    {
                        SideQuestManager.GetFishingQuestReward();
                        if (TownCharacter.queensQuest)
                        {
                            Memory.WriteByte(0x21CE4430, 1);
                        }
                        Memory.WriteOneByte(0x21CE4427, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE4479, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13109) //devia
            {
                if (Memory.ReadByte(0x21CE447B) == 1)
                {
                    if (Memory.ReadByte(0x21CE4431) == 0)
                    {
                        Memory.WriteOneByte(0x21CE4431, BitConverter.GetBytes(1));
                    }
                    else if (Memory.ReadByte(0x21CE4431) == 1)
                    {
                        //Memory.WriteOneByte(0x21CE441E, BitConverter.GetBytes(2));
                    }
                    else if (Memory.ReadByte(0x21CE4431) == 2)
                    {
                        SideQuestManager.GetFishingQuestReward();
                        Memory.WriteOneByte(0x21CE4431, BitConverter.GetBytes(0));
                    }
                }
                else
                {
                    Memory.WriteByte(0x21CE447B, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13360) //laura
            {
                if (Memory.ReadByte(0x21CE4451) == 0)
                {
                    Memory.WriteByte(0x21CE4451, 1);
                }
            }
            else if (TownCharacter.characterIDData == 12594) //ro
            {
                if (Memory.ReadByte(0x21CE4452) == 0)
                {
                    Memory.WriteByte(0x21CE4452, 1);
                }
            }
            else if (TownCharacter.characterIDData == 12852) //phil
            {
                if (Memory.ReadByte(0x21CE4453) == 0)
                {
                    Memory.WriteByte(0x21CE4453, 1);
                }
            }
            else if (TownCharacter.characterIDData == 12341) //zabo
            {
                if (Memory.ReadByte(0x21CE4454) == 0)
                {
                    Memory.WriteByte(0x21CE4454, 1);
                }
            }
            else if (TownCharacter.characterIDData == 13361) //mayor
            {
                if (Memory.ReadByte(0x21CE4464) == 0)
                {
                    if (Memory.ReadByte(0x21CE4463) == 1)
                        Memory.WriteByte(0x21CE4464, 1);
                }
                else if (Memory.ReadByte(0x21CE4464) == 1)
                {
                    Memory.WriteByte(0x21CE4464, 2);
                    DailyShopItem.SetDailyItemsToShop();
                }
                else if (Memory.ReadByte(0x21CE4464) == 2)
                {
                    if (Memory.ReadByte(0x21CE4468) == 0)
                    {
                        Memory.WriteByte(0x21CE4468, 1);
                    }
                    else if (Memory.ReadByte(0x21CE4468) == 2)
                    {
                        Memory.WriteUShort(Addresses.firstBagItem + (0x2 * Inventory.GetBagItemsFirstAvailableSlot()), TownCharacter.mayorReward);
                        Memory.WriteByte(0x21CE4468, 0);
                        if (Memory.ReadByte(0x21CE446B) == 1)
                        {
                            Memory.WriteByte(0x21CE4464, 3);
                        }
                    }
                }
            }

            if (currentArea == 23)
            {
                if (Memory.ReadByte(0x21D26FD4) == 0)
                {
                    if (Memory.ReadByte(0x21CE445D) == 1)
                    {
                        Memory.WriteByte(0x21CE445D, 2);
                        Memory.WriteByte(0x21CE4459, 2);
                        Memory.WriteUShort(Addresses.firstBagItem + (0x2 * Inventory.GetBagItemsFirstAvailableSlot()), 234);
                    }
                }
                if (Memory.ReadByte(0x21D26FD4) == 1)
                {
                    if (Memory.ReadByte(0x21CE4462) == 1)
                    {
                        Memory.WriteByte(0x21CE445E, 2);
                        Memory.WriteByte(0x21CE4462, 2);
                        Memory.WriteUShort(Addresses.firstBagItem + (0x2 * Inventory.GetBagItemsFirstAvailableSlot()), 233);
                    }
                }
            }
        }

        /// <summary>Yellow Drops, Dark Heaven Castle and area 40 have no running clock natively: on entry the clock is
        /// enabled and seeded with the current time of day (the Clock mailbox tells the PNACH side).</summary>
        public static void CheckClockAdvancement(int area)
        {
            if (area == 23 || area == 40 || area == 38)
            {
                float currentClock = Memory.ReadFloat(0x21CD4310);

                Memory.WriteByte(Mailbox.Clock, 1);
                Thread.Sleep(10);

                Memory.WriteByte(0x203A3920, 0); //enable clock
                Memory.WriteFloat(0x202A28F4, currentClock);

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Enabled clock");
            }
        }

        /// <summary>Until the mod's game-cleared flag is set, the Demon Shaft visit count is zeroed whenever the world
        /// map is open, so the post-game dungeon cannot be entered before the base game is finished.</summary>
        public static void DemonShaftUnlockCheck()
        {
            if (demonshaftUnlocked == false)
            {
                if (Memory.ReadByte(0x21CE448B) == 1)
                {
                    demonshaftUnlocked = true; //if DS is unlocked, set this so we don't need to check for it anymore
                }

                if (Memory.ReadByte(Addresses.selectedMenu) == 13) //check if worldmap open
                {
                    if (Memory.ReadByte(0x21CE448B) == 0) //check if game cleared flag not true
                    {
                        Memory.WriteByte(0x21CE70A0, 0);
                    }
                }
            }
        }

        private static string FormatButtons(int mask) =>
            mask == 0 ? "none" : ((Button)(ushort)mask).ToString();
    }
}
