using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class SessionController
    {
        public static bool firstlaunch;
        public static bool ingame;
        public static bool ingameFlag;
        public static bool userMode = false;
        public static bool saveStateUsed = false;
        public static bool saveFileMessageBox = false;
        public static int PID = 0;
        public static int PID_attempts = 0;
        public static int currentFrameCounter = 0;
        public static int previousFrameCounter = 0;
        public static int previousMode = -1;
        public static Thread townThread = new Thread(new ThreadStart(GameLoop.Run));
        public static Thread changesThread = new Thread(new ThreadStart(ApplyNewChanges));
        public static Thread dungeonthread = new Thread(new ThreadStart(Dungeon.InsideDungeonThread));
        public static Thread weaponspecialeffectThread = new Thread(new ThreadStart(WeaponSpecialReroll.Run));
        public static Thread codeCaveScannerThread = new Thread(new ThreadStart(CodeCaveScanner.Run));

        internal static void ApplyNewChanges()
        {
            WeaponBalance.Apply();
            Shop.UpdateShopPrices();
            Enemies.EnableEnemyDrops();   // let the "can't drop" species (flyers, Gol/Sil) drop on death (static species-table patch)
            WeaponDescriptions.StartDescriptionPatcher();   // keep weapon menu descriptions = WeaponData.ModDescription
            MachoSword.ApplyAbsCodePatches();   // ABS rollover display patches (EE code; menu/HUD code is still cold here)
            TargetRedirectCaves.ArmColdPatch();   // the enemy target redirect: clean _GET_POSITION + _GET_DISTANCE copies in cold-PINE caves, reading the per-slot target table (AggroTable)
            ShieldPatches.ArmBlockPatch();   // Angel Gear shield: MoveCheck2's enemy-block addend becomes a data word (cold; enemy code never ran yet)
            ShieldPatches.ArmShotPatch();    // Angel Gear shield: checkCollision's player position becomes a pointer (cold) — shots can collide with the pouch
            HarderEnemyAI.StartThread();   // "Harder enemy AI" toggle: enemies with a get-up motion can revive
            GameClock.Start();           // the play clock every dungeon feature times against
            Mirage.Start();   // Ungaga's Mirage: watches for charge-release, drives the decoy via data writes
            CatCape.Start();   // the cat's cape off Xiao's own cloth list the moment her pack builds it (whatever weapon she holds)
            HeapWatch.Start();   // DIAGNOSTIC (DebugDiagnostics.Enabled): the character heap and its pools logged as they change
            BorrowedShots.Start(DragonsY.WantedShot, BanditSlingshot.WantedShot, BigBang.WantedShot, SwordOfZeus.WantedShot, BigBangShot.WantedShot, ZeusShot.WantedShot, BabelsSpear.WantedShot, HerculesWrath.WantedShot, Cactus.WantedShot, TerraSword.WantedShot);
            ConfuseAbility.Start();   // the Confuse ability's procs, the confusion's ticks and the stars over the confused, on every floor
            //   ↑ shot effects borrowed for Xiao's abilities, entered on every floor; each ability names the one it wants
            SharedShots.Start();         // the monster shot pack's five slots shared among every config a floor needs (the cave does it; the mod arms and reports)
            AngelGear.Start();   // Xiao's Angel Gear "Guardian Reflector": the reflector loop + melee hit watch for the life of the app (idle until the weapon or its sphere is live on a floor; the loop also retries the two cold patches above while not on one)
            CustomFishingSpot.InstallShallowLinePatch();   // COLD: rewrite FishLineStep's bobber anchor to read a data global (before any fishing JITs it)
            CustomFishingSpot.SeedFishCamHeight();   // the ISO-patched fishing SetHeight reads a data word EVERY town — seed it to vanilla 40 before any session
            ExplosionImmunity.SeedBombReaction();              // …and the item-bomb reaction word, read by every bomb blast
            ChargeHitRadii.Seed();            // …and the ISO-patched charge-attack radii, read on every charge swing
        }

        public static void CheckEmulatorAndGame()
        {
            firstlaunch = true;
            Program.ConsoleLogging(); //LOGS CONSOLE WRITES TO TEXT FILE!
            while (true)
            {
                if (Memory.process != null && Memory.IsConnected)
                {
                    Memory.WriteByte(Mailbox.PineProbe, 0); //mod's flag for PNACH
                }
                if (PID == 0)
                {
                    PID_attempts++;
                    if (PID_attempts < 3600) //If program has been inactive for more than hour, starts checking every 30s to decrease console clogging
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Thread.Sleep(30000);
                    }

                    Program.GetPCSX2Executable();
                    if (Memory.process != null)
                    {
                        PID = Memory.process.Id;
                    }
                    else
                    {
                        PID = 0;
                    }
                }
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "CheckEmulatorAndGame");
                if (PID == 0)
                {
                    //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Memory PID 0");
                    ModWindow.EmulatorCount(0); //no emulators running
                }
                else if (PID != 0)
                {
                    if (!Memory.IsConnected)
                    {
                        try { Memory.Connect(); }
                        catch
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "PINE not reachable. Enable PINE in PCSX2: Settings → Advanced → PINE Server (port 28011)");
                            ModWindow.PineNotConnected();
                            Thread.Sleep(2000);
                            continue;
                        }
                    }
                    //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Memory.ReadInt(0x20299540));
                    if (Memory.ReadInt(0x20299540) != 1802658116) //check if DC1 has been booted
                    {
                        Console.WriteLine("DC1 check: " + Memory.ReadInt(0x20299540));
                        PID = 0;
                        ModWindow.EmulatorCount(1);
                    }
                    else
                    {
                        if (Memory.ReadByte(Mailbox.PnachActive) == 1) //check PNACH flag
                        {
                            if (firstlaunch)
                            {
                                if (Memory.ReadByte(Addresses.mode) == 2 || Memory.ReadByte(Addresses.mode) == 3 || Memory.ReadByte(Addresses.mode) == 5) //checks if player is already in-game
                                {
                                    if (saveFileMessageBox == false)
                                    {
                                        ModWindow.FirstLaunchGameMode(false);
                                        Thread.Sleep(100);
                                    }
                                }
                                else
                                {
                                    ModWindow.FirstLaunchGameMode(true);
                                    //firstlaunch = false;
                                    ingame = false;
                                    userMode = true;
                                    TitleMenu();
                                }
                            }
                        }
                        else
                        {
                            ModWindow.PnachNotActive();
                        }
                    }
                }

                if (saveStateUsed)
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }

        public static void TitleMenu()
        {
            // Check for another active mod instance before claiming the flag
            while (true)
            {
                if (Memory.ReadByte(Mailbox.PineProbe) == 1)
                {
                    ModWindow.EnhancedModAlreadyOpen();
                }
                else
                {
                    break;
                }
            }

            // Claim the flag immediately to minimise the gap during which PNACH shows the "Launch Enhanced Mod" message
            Memory.WriteByte(Mailbox.PineProbe, 1);

            AllySwitch.InitializeCharacterOffsetValues();
            while (true)
            {
                currentFrameCounter = Memory.ReadInt(0x202A2400);
                if (currentFrameCounter > 0)
                {
                    break;
                }
            }
            previousFrameCounter = currentFrameCounter;
            Thread.Sleep(10);

            while (true)
            {
                Memory.WriteByte(Mailbox.PineProbe, 1); //mod's flag for PNACH
                currentFrameCounter = Memory.ReadInt(0x202A2400);
                int currentMode = Memory.ReadByte(Addresses.mode);
                if (currentMode != previousMode && previousMode != -1)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Mode changed: {previousMode} -> {currentMode}");
                previousMode = currentMode;
                if (currentFrameCounter > 0)
                {
                    if (ingame == false)
                    {
                        if (currentMode == 0 || currentMode == 1)
                        {
                            ingame = false;
                            ingameFlag = false;
                            if (Memory.ReadByte(0x202A3420) == 9) //Opening book mode after you press start
                            {
                                ModWindow.CurrentlyInGame();
                            }
                            else
                            {
                                ModWindow.CurrentlyInMainMenu();
                            }
                        }
                        else if (currentMode == 2 || currentMode == 3 || currentMode == 5)
                        {
                            Thread.Sleep(100);
                            currentMode = Memory.ReadByte(Addresses.mode);
                            if (currentMode == 2 || currentMode == 3 || currentMode == 5)
                            {
                                if (ingameFlag == false)
                                {
                                    Thread.Sleep(100);

                                    if (currentMode == 5)
                                    {
                                        Thread.Sleep(800);
                                        Memory.WriteByte(0x21CE448A, 1);
                                        Thread.Sleep(200);
                                        Dialogues.IntroTextAtNorune();
                                    }


                                    if (Memory.ReadByte(0x21CE448A) == 1)
                                    {
                                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Entered ingame, starting all threads!");
                                        changesThread = new Thread(() => ApplyNewChanges());
                                        townThread = new Thread(() => GameLoop.Run());
                                        dungeonthread = new Thread(() => Dungeon.InsideDungeonThread());
                                        weaponspecialeffectThread = new Thread(() => WeaponSpecialReroll.Run());
                                        if (!changesThread.IsAlive) changesThread.Start();
                                        if (!townThread.IsAlive) townThread.Start();
                                        if (!dungeonthread.IsAlive) dungeonthread.Start();
                                        if (!weaponspecialeffectThread.IsAlive) weaponspecialeffectThread.Start();
                                        if (DebugDiagnostics.Enabled && !codeCaveScannerThread.IsAlive) codeCaveScannerThread.Start(); // passive code-cave sweep (diagnostic), findings -> CodeCaveFindings.txt

                                        // TEST weapon grants: Dev/TestWeaponGrant.cs (behind DebugDiagnostics) hands a character a weapon
                                        // through the game's own CDngStatusData::GetItem once a floor is walkable.

                                        CheckModWindowOptions(currentMode);
                                        ingameFlag = true;
                                    }
                                    else
                                    {
                                        if (Player.InDungeonFloor() == true)
                                            Memory.WriteInt(Addresses.dungeonDebugMenu, 151); //If we are in a dungeon, this will take us to the main menu
                                        else
                                            Memory.WriteByte(Addresses.mode, 1);
                                        ModWindow.NotEnhancedModSaveFile();
                                        break;
                                    }
                                }
                                ingame = true;
                                ModWindow.CurrentlyInGame();
                            }

                        }
                    }
                    else
                    {
                        if (currentMode == 0 || currentMode == 1)
                        {
                            Thread.Sleep(100);
                            currentMode = Memory.ReadByte(Addresses.mode);
                            if (currentMode == 0 || currentMode == 1)
                            {
                                ingame = false;
                                ingameFlag = false;
                                ModWindow.CurrentlyInMainMenu();
                            }
                        }
                        else if (currentMode == 2 || currentMode == 3 || currentMode == 5)
                        {
                            ingame = true;
                            ModWindow.CurrentlyInGame();
                        }
                    }
                }

                if (Memory.ReadInt(0x20299540) != 1802658116)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Dark Cloud was either closed, or save state was used!");
                    Thread.Sleep(50);
                    if (Memory.ReadInt(0x20299540) != 1802658116)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Dark Cloud was closed!");
                        break;
                    }
                }

                if (currentFrameCounter < previousFrameCounter || currentFrameCounter > previousFrameCounter + 720 || currentFrameCounter == 0)
                {
                    Thread.Sleep(200);
                    if (currentFrameCounter == 0)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Save state detected! Reason: currentFrameCounter == 0");
                    else if (currentFrameCounter < previousFrameCounter)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Save state detected! Reason: currentFrameCounter ({currentFrameCounter}) < previousFrameCounter ({previousFrameCounter})");
                    else
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Save state detected! Reason: currentFrameCounter ({currentFrameCounter}) > previousFrameCounter ({previousFrameCounter}) + 360");
                    if (Player.InDungeonFloor() == true)
                        Memory.WriteInt(Addresses.dungeonDebugMenu, 151); //If we are in a dungeon, this will take us to the main menu
                    else
                        Memory.WriteByte(Addresses.townSoftReset, 1);

                    ModWindow.SaveStateDetected();

                }

                if (currentFrameCounter > 0)
                {
                    if (Memory.ReadByte(Mailbox.PnachActive) != 1) //check PNACH flag
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "PNACH cheats were disabled!");
                        break;
                    }
                }

                previousFrameCounter = currentFrameCounter;

                if (saveStateUsed)
                {
                    break;
                }

                Thread.Sleep(1);
            }

            Memory.WriteByte(Mailbox.PineProbe, 0); //disable mod's flag for pnach
        }

        public static void CheckModWindowOptions(int mode)
        {
            if (mode != 5)
            {
                ModWindow.ModWindowOptionsEnabled();
            }
        }
    }
}
