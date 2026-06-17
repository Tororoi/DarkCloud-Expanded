using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia;
using System;
using System.Diagnostics;
using System.Threading;
using MsBoxButtonEnum = MessageBox.Avalonia.Enums.ButtonEnum;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;
using ButtonResult = MsBox.Avalonia.Enums.ButtonResult;
using ThreadState = System.Threading.ThreadState;

namespace Dark_Cloud_Improved_Version
{
    public partial class ModWindow : Window
    {
        private static ModWindow instance;

        private readonly DispatcherTimer _fishStatusTimer;

        public ModWindow()
        {
            InitializeComponent();
            instance = this;
            UserModeLaunch();

            _fishStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _fishStatusTimer.Tick += (_, _) => UpdateFishFarmerStatus();
            _fishStatusTimer.Start();
        }

        private void UpdateFishFarmerStatus()
        {
            string status = FishDataFarmer.IsRunning ? "Running" :
                            FishDataFarmer.Enabled   ? "Waiting" : "Stopped";
            Label_FishFarmer_Status.Text = $"Status: {status}";

            Label_FishFarmer_Sessions.Text =
                $"Sessions: {FishDataFarmer.SessionCount}   Queue: {FishDataFarmer.PendingCount}";

            int[] p = TownCharacter.FishProbe;
            Label_FishFarmer_Probe.Text =
                $"708={p[0]:X8}  714={p[1]:X8}  3E20={p[2]:X8}  3E24={p[3]:X8}  3E28={p[4]:X8}";
            Label_FishFarmer_Survey.Text = FishDataFarmer.GetSurveyText();
        }

        public static Thread townThread = new Thread(new ThreadStart(TownCharacter.MainScript)) { IsBackground = true };
        public static Thread TASSThread = new Thread(new ThreadStart(TASThread.RunTAS)) { IsBackground = true };
        public static Thread TASSThread2 = new Thread(new ThreadStart(TASThread.RecordTAS)) { IsBackground = true };
        public static Thread dungeonthread = new Thread(new ThreadStart(Dungeon.InsideDungeonThread)) { IsBackground = true };
        public static Thread debugThread = new Thread(new ThreadStart(CheatCodes.DebugOptions)) { IsBackground = true };
        public static Thread launchThread = new Thread(new ThreadStart(MainMenuThread.CheckEmulatorAndGame)) { IsBackground = true };

        public int[] attackSoundAddresses = { 0x20265DBC, 0x20265DC2, 0x20265DC8, 0x20265DCE, 0x20265F0C, 0x20265F12, 0x2026605C, 0x20266062, 0x202661AC, 0x202661B8, 0x202662FC, 0x20266302, 0x20266308, 0x2026644C };
        public byte[] attackSoundValues = { 68, 69, 70, 71, 83, 84, 98, 99, 113, 115, 128, 129, 130, 156 };
        public bool nightlyVersion = false;

        #region Static callbacks (called from background threads)

        public static void EmulatorCount(int newValue)
        {
            if (newValue == 0)
                instance.NoEmulatorsActive(true);
            else if (newValue > 1)
                instance.TooManyEmulatorsActive(true);
            else if (newValue == 1)
                instance.GameNotActive(true);
        }

        public static void PnachNotActive()
        {
            instance.FormPnachNotActive(true);
        }

        public static void NightlyVersionCheck()
        {
            instance.nightlyVersion = true;
        }

        public static void CurrentlyInMainMenu()
        {
            instance.FormCurrentlyInMainMenu(true);
        }

        public static void CurrentlyInGame()
        {
            instance.FormCurrentlyInGame(true);
        }

        public static void SaveStateDetected()
        {
            instance.FormSaveStateDetected(true);
        }

        public static void FirstLaunchGameMode(bool validGameMode)
        {
            if (!validGameMode)
                instance.InvalidFirstLaunchGameMode(true);
            else
                instance.ValidFirstLaunchGameMode(true);
        }

        public static void NotEnhancedModSaveFile()
        {
            instance.FormNotEnhancedModSaveFile(true);
        }

        public static void EnhancedModAlreadyOpen()
        {
            instance.FormEnhancedModAlreadyOpen(true);
        }

        public static void PineWritesFailing()
        {
            instance.ShowPineWritesFailing();
        }

        public static void ModWindowOptionsEnabled()
        {
            instance.ModWindowSettingsCheck(true);
        }

        #endregion

        #region UI update methods (dispatched to UI thread)

        void NoEmulatorsActive(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Cannot detect PCSX2-Emulator!\n\nPlease launch your emulator to continue.");
        }

        void TooManyEmulatorsActive(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Too many PCSX2-emulators open!\n\nPlease make sure only one is running at time.");
        }

        void GameNotActive(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Please boot Dark Cloud (USA) to continue.");
        }

        public static void PineNotConnected()
        {
            instance.ShowPineNotConnected();
        }

        void ShowPineNotConnected()
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "PINE not enabled in PCSX2!\n\nGo to Settings → Advanced → PINE Server\nand enable it on port 28011.");
        }

        void ShowPineWritesFailing()
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "PCSX2 is pausing and rejecting memory writes.\n\nIn PCSX2: Settings → General\nUncheck \"Pause Emulation When Focus is Lost\"\nthen click Launch as User again.");
        }

        void InvalidFirstLaunchGameMode(bool enable)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Label_UserMode_PlaceholderText.Text = "Detected a save file already running!\n\nPlease re-boot Dark Cloud to start the Mod.";
                MainMenuThread.saveFileMessageBox = true;
                string message = "Detected a save file already running! Enhanced Mod currently not active.\n\nThe mod needs to be launched while in the Main Menu.\n\nDo you want the mod to return your game to Main Menu?";
                var box = MessageBoxManager.GetMessageBoxStandard("Save file running!", message, MsBoxButtonEnum.YesNo, MsBoxIcon.Warning);
                var result = await box.ShowWindowDialogAsync(this);

                Label_UserMode_PlaceholderText.Text = "Detected a save file already running!\n\nPlease re-boot Dark Cloud to start the Mod.";
                if (result == ButtonResult.Yes)
                {
                    if (Player.InDungeonFloor())
                        Memory.WriteInt(Addresses.dungeonDebugMenu, 151);
                    else
                        Memory.WriteByte(Addresses.townSoftReset, 1);
                }
            });
        }

        void ValidFirstLaunchGameMode(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Dark Cloud has been booted!");
        }

        void FormPnachNotActive(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "PNACH File not active!\n\nPlease put the Enhanced Mod's PNACH file into the Emulator's Cheats folder and active cheats in Emulator with System->Enable Cheats");
        }

        void FormCurrentlyInMainMenu(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Label_UserMode_PlaceholderText.Text = "Enhanced Mod is active! Currently in Main menu.\n\nYou can start a new game or load a save.";
                if (instance.nightlyVersion)
                    instance.CBox_UserMode_Graphics.IsEnabled = false;
            });
        }

        void FormCurrentlyInGame(bool enable)
        {
            BeginQuestPolling();
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Enhanced Mod is active and running!\n\nRemember to NEVER use save states with the mod! Always save the game normally through the game's save menu.");
        }

        void RefreshQuestTab()
        {
            string summary = QuestTracker.GetActiveQuestsSummary();
            Dispatcher.UIThread.Post(() =>
            {
                var label = this.FindControl<TextBlock>("Label_QuestTracker");
                label?.SetValue(TextBlock.TextProperty, summary);
            });
        }

        static bool questPollingStarted = false;

        void BeginQuestPolling()
        {
            if (questPollingStarted) return;
            questPollingStarted = true;

            var thread = new Thread(() =>
            {
                while (true)
                {
                    if (QuestTracker.HasStateChanged())
                        RefreshQuestTab();
                    Thread.Sleep(1000);
                }
            }) { IsBackground = true };
            thread.Start();
        }

        void FormSaveStateDetected(bool enable)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Topmost = true;
                MainMenuThread.saveStateUsed = true;
                string message = "The mod has detected a possible save state load!\n\nUsing save states is NOT ALLOWED while using the Enhanced Mod, since it can cause major issues.\n\nThe game has been reset, and this mod will be closed.";
                var box = MessageBoxManager.GetMessageBoxStandard("Save state detected!", message, MsBoxButtonEnum.Ok, MsBoxIcon.Warning);
                await box.ShowWindowDialogAsync(this);
                Topmost = false;
                Label_UserMode_PlaceholderText.Text = "A possible save state used! Mod has been terminated.";
                Memory.WriteByte(0x21F10024, 0);
                Close();
            });
        }

        void FormNotEnhancedModSaveFile(bool enable)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Topmost = true;
                string message = "Loaded a Dark Cloud save file which was not started with Enhanced Mod!\n\nPlease load a save file which you have started with Enhanced Mod, or start a New Game with the mod.";
                var box = MessageBoxManager.GetMessageBoxStandard("Invalid save file!", message, MsBoxButtonEnum.Ok, MsBoxIcon.Warning);
                await box.ShowWindowDialogAsync(this);
                Topmost = false;
            });
        }

        void FormEnhancedModAlreadyOpen(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
                Label_UserMode_PlaceholderText.Text = "Another instance of Enhanced Mod is already active!\n\nYou can close this window.");
        }

        void ModWindowSettingsCheck(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (Memory.ReadByte(0x21CE4490) == 1)
                {
                    CBox_UserMode_Graphics.IsChecked = true;
                    Memory.WriteByte(0x21F10034, 1);
                }
                else
                {
                    CBox_UserMode_Graphics.IsChecked = false;
                    Memory.WriteByte(0x21F10034, 0);
                }

                if (Memory.ReadByte(0x21CE4491) == 1)
                {
                    CBox_UserMode_Widescreen.IsChecked = true;
                    Memory.WriteByte(0x21F10030, 1);
                }
                else
                {
                    CBox_UserMode_Widescreen.IsChecked = false;
                    Memory.WriteByte(0x21F10030, 0);
                }

                if (Memory.ReadByte(0x21CE4492) == 1)
                {
                    CBox_UserMode_WeaponBeeps.IsChecked = true;
                    Memory.WriteByte(0x21F10028, 1);
                }
                else
                {
                    CBox_UserMode_WeaponBeeps.IsChecked = false;
                    Memory.WriteByte(0x21F10028, 0);
                }

                if (Memory.ReadByte(0x21CE4493) == 1)
                {
                    CBox_UserMode_BattleMusic.IsChecked = true;
                    Memory.WriteByte(0x21F1002C, 1);
                }
                else
                {
                    CBox_UserMode_BattleMusic.IsChecked = false;
                    Memory.WriteByte(0x21F1002C, 0);
                }

                if (Memory.ReadByte(0x21CE4495) == 1)
                {
                    CBox_UserMode_MuteMusic.IsChecked = true;
                    Memory.WriteUShort(0x20299F53, 0);
                }
                else
                {
                    CBox_UserMode_MuteMusic.IsChecked = false;
                    Memory.WriteUShort(0x20299F53, 25637);
                }

                if (Memory.ReadByte(0x21CE4494) == 1)
                {
                    Cbox_Usermode_AttackSounds.IsChecked = true;
                    for (int c = 0; c < attackSoundAddresses.Length && c < attackSoundValues.Length; c++)
                        Memory.WriteByte(attackSoundAddresses[c], 0);
                }
                else
                {
                    Cbox_Usermode_AttackSounds.IsChecked = false;
                    for (int c = 0; c < attackSoundAddresses.Length && c < attackSoundValues.Length; c++)
                        Memory.WriteByte(attackSoundAddresses[c], attackSoundValues[c]);
                }
            });
        }

        void UserModeLaunch()
        {
            TabControl_USER.IsVisible = true;
            Container_MainModes.IsVisible = false;
            if (!launchThread.IsAlive) launchThread.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            Memory.WriteByte(0x21F10024, 0);
            base.OnClosed(e);
            Environment.Exit(0);
        }

        #endregion

        #region Mode selection

        private void buttonLaunchModAsUser(object sender, RoutedEventArgs e)
        {
            TabControl_USER.IsVisible = true;
            Container_MainModes.IsVisible = false;
            if (!launchThread.IsAlive) launchThread.Start();
        }

        private void buttonLaunchModAsDev(object sender, RoutedEventArgs e)
        {
            TabControl_DEV.IsVisible = true;
            Container_MainModes.IsVisible = false;

            DEV_Page2_TextBox_Gilda.Text = Player.Gilda.ToString();

            var enemyBoxes = new[] { DEV_Page2_TextBox_Enemy1, DEV_Page2_TextBox_Enemy2, DEV_Page2_TextBox_Enemy3, DEV_Page2_TextBox_Enemy4, DEV_Page2_TextBox_Enemy5, DEV_Page2_TextBox_Enemy6, DEV_Page2_TextBox_Enemy7, DEV_Page2_TextBox_Enemy8, DEV_Page2_TextBox_Enemy9, DEV_Page2_TextBox_Enemy10, DEV_Page2_TextBox_Enemy11, DEV_Page2_TextBox_Enemy12, DEV_Page2_TextBox_Enemy13, DEV_Page2_TextBox_Enemy14, DEV_Page2_TextBox_Enemy15, DEV_Page2_TextBox_Enemy16 };
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                enemyBoxes[i].Text = Memory.ReadUInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)).ToString();
        }

        #endregion

        #region User Page 1

        private async void Btn_UserMode_Quit_Clicked(object sender, RoutedEventArgs e)
        {
            if (Memory.ReadByte(Addresses.mode) == 2 || Memory.ReadByte(Addresses.mode) == 3)
            {
                Topmost = true;
                string message = "Closing the mod will return your game to the Main Menu, remember to save your game!\n\nAre you sure you want to quit?\n\nTip: You can soft-reset your game back to the Main Menu by holding Start+Select+L1+L2+R1+R2, and then quit the mod without any warnings.";
                var box = MessageBoxManager.GetMessageBoxStandard("Are you sure you want to quit?", message, MsBoxButtonEnum.YesNo, MsBoxIcon.Warning);
                var result = await box.ShowWindowDialogAsync(this);
                Topmost = false;
                if (result == ButtonResult.Yes)
                    Close();
            }
            else
            {
                Close();
            }
        }

        #endregion

        #region User Page 2 — Options

        private void CBox_UserMode_WeaponBeepsChanged(object sender, RoutedEventArgs e)
        {
            if (CBox_UserMode_WeaponBeeps.IsChecked == true)
            {
                Memory.WriteByte(0x21F10028, 1);
                Memory.WriteByte(0x21CE4492, 1);
            }
            else
            {
                Memory.WriteByte(0x21F10028, 0);
                Memory.WriteByte(0x21CE4492, 0);
            }
        }

        private void CBox_UserMode_GraphicsChanged(object sender, RoutedEventArgs e)
        {
            if (CBox_UserMode_BattleMusic.IsChecked == true)
            {
                Memory.WriteByte(0x21F1002C, 1);
                Memory.WriteByte(0x21CE4493, 1);
            }
            else
            {
                Memory.WriteByte(0x21F1002C, 0);
                Memory.WriteByte(0x21CE4493, 0);
            }
        }

        private void CBox_UserMode_Widescreen_Changed(object sender, RoutedEventArgs e)
        {
            if (CBox_UserMode_Widescreen.IsChecked == true)
            {
                Memory.WriteByte(0x21F10030, 1);
                Memory.WriteByte(0x21CE4491, 1);
            }
            else
            {
                Memory.WriteByte(0x21F10030, 0);
                Memory.WriteByte(0x21CE4491, 0);
            }
        }

        private void CBox_UserMode_Graphics_Changed(object sender, RoutedEventArgs e)
        {
            if (CBox_UserMode_Graphics.IsChecked == true)
            {
                Memory.WriteByte(0x21F10034, 1);
                Memory.WriteByte(0x21CE4490, 1);
            }
            else
            {
                Memory.WriteByte(0x21F10034, 0);
                Memory.WriteByte(0x21CE4490, 0);
            }
        }

        private void Cbox_Usermode_AttackSounds_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (Cbox_Usermode_AttackSounds.IsChecked == true)
            {
                for (int c = 0; c < attackSoundAddresses.Length && c < attackSoundValues.Length; c++)
                    Memory.WriteByte(attackSoundAddresses[c], 0);
                Memory.WriteByte(0x21CE4494, 1);
            }
            else
            {
                for (int c = 0; c < attackSoundAddresses.Length && c < attackSoundValues.Length; c++)
                    Memory.WriteByte(attackSoundAddresses[c], attackSoundValues[c]);
                Memory.WriteByte(0x21CE4494, 0);
            }
        }

        // Sets the current dungeon's spawn roster so every spawn is the given species (by TableIndex).
        // Pure data writes (crash-free); takes effect when you re-enter / descend to a floor.
        private void Btn_Injector_Test_Click(object sender, RoutedEventArgs e)
        {
            // Box accepts "20" or a comma list "20,3,6". A trailing "!" marks a species spawn-once
            // (at most 1 per floor; the rest fill normally, total stays 15), e.g. "20!,60" = one Gyon + Cursed Roses.
            // Special token "iq" = write the exact real Ice Queen (SW floor-18) boss block, incl. the Count field.
            if (Tbox_Injector_Table.Text.Trim().ToLowerInvariant() == "iq")
            {
                EnemyModelInjector.SetIceQueenFloorExact();
                return;
            }
            var idx = new System.Collections.Generic.List<int>();
            var once = new System.Collections.Generic.List<bool>();
            foreach (string p in Tbox_Injector_Table.Text.Split(','))
            {
                string t = p.Trim();
                bool o = t.EndsWith("!");
                if (o) t = t.Substring(0, t.Length - 1).Trim();
                if (!int.TryParse(t, out int v)) continue;
                idx.Add(v); once.Add(o);
            }
            if (idx.Count == 0)
            {
                Console.WriteLine("Injector: enter a TableIndex or list, e.g. 20  |  20,3,6  |  20!,60 (Gyon once)");
                return;
            }
            int population = 0; // 0 (or unparseable) = keep original
            if (idx.Count == 1) EnemyModelInjector.SetSpawnRosterToSpecies(idx[0], population);
            else EnemyModelInjector.SetSpawnRosterMix(idx.ToArray(), once.ToArray(), population);
        }

        // Post-spawn cap: keep at most 1 of the TableIndex species on the current floor, remove extras.
        private void Btn_Injector_BossAI_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(Tbox_Injector_Table.Text, out int tableIndex))
            {
                Console.WriteLine("Injector: TableIndex must be an integer (e.g. 20 = Gyon).");
                return;
            }
            EnemyModelInjector.CapSpeciesOnFloor(tableIndex, 1);
        }

        private void CBox_UserMode_MuteMusic_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CBox_UserMode_MuteMusic.IsChecked == true)
            {
                Memory.WriteUShort(0x20299F53, 0);
                Memory.WriteByte(0x21CE4495, 1);

                if (CBox_UserMode_BattleMusic.IsChecked == false)
                {
                    Memory.WriteByte(0x21F1002C, 1);
                    Memory.WriteByte(0x21CE4493, 1);
                    CBox_UserMode_BattleMusic.IsChecked = true;
                }
            }
            else
            {
                Memory.WriteUShort(0x20299F53, 25637);
                Memory.WriteByte(0x21CE4495, 0);
            }
        }

        #endregion

        #region Dev Page 1

        private void DEV_Page1_Btn_Dayuppy(object sender, RoutedEventArgs e)
        {
        }

        private void DEV_Page1_Btn_Mike(object sender, RoutedEventArgs e)
        {
            if (MainMenuThread.changesThread.ThreadState == ThreadState.Unstarted)
                MainMenuThread.changesThread.Start();
            if (Weapons.weaponsMenuListener.ThreadState == ThreadState.Unstarted)
                Weapons.weaponsMenuListener.Start();
        }

        private void DEV_Page1_Btn_Plgue(object sender, RoutedEventArgs e)
        {
        }

        private void DEV_Page1_Btn_WordOfWind(object sender, RoutedEventArgs e)
        {
            Program.ConsoleLogging();
        }

        private void DEV_Page1_Btn_DungeonThread(object sender, RoutedEventArgs e)
        {
            if (dungeonthread.ThreadState == ThreadState.Unstarted)
                dungeonthread.Start();
        }

        private void DEV_Page1_Btn_TownThread(object sender, RoutedEventArgs e)
        {
            if (townThread.ThreadState == ThreadState.Unstarted)
                townThread.Start();
        }

        private void DEV_Page1_CBox_DebugThread(object sender, RoutedEventArgs e)
        {
            if (CBox_DebugThread.IsChecked == true)
            {
                if (debugThread.ThreadState == ThreadState.Unstarted)
                    debugThread.Start();
                CBox_DebugThread.IsEnabled = false;
            }
        }

        private void DEV_Page1_Btn_FishFarmer_Toggle(object sender, RoutedEventArgs e)
        {
            FishDataFarmer.Toggle();
            Btn_FishFarmer_Toggle.Content = (FishDataFarmer.Enabled || FishDataFarmer.IsRunning) ? "Stop" : "Start";
        }

        #endregion

        #region Dev Page 2

        private void DEV_Page2_TextBox_Gilda_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Gilda.Text == "")
            {
                DEV_Page2_TextBox_Gilda.Text = "0";
                return;
            }
            if (ushort.TryParse(DEV_Page2_TextBox_Gilda.Text, out ushort val))
                Player.Gilda = val;
        }

        private void DEV_Page2_Btn_SetEnemiesMaxHP_Click(object sender, RoutedEventArgs e)
        {
            int max = int.MaxValue;
            var enemyBoxes = new[] { DEV_Page2_TextBox_Enemy1, DEV_Page2_TextBox_Enemy2, DEV_Page2_TextBox_Enemy3, DEV_Page2_TextBox_Enemy4, DEV_Page2_TextBox_Enemy5, DEV_Page2_TextBox_Enemy6, DEV_Page2_TextBox_Enemy7, DEV_Page2_TextBox_Enemy8, DEV_Page2_TextBox_Enemy9, DEV_Page2_TextBox_Enemy10, DEV_Page2_TextBox_Enemy11, DEV_Page2_TextBox_Enemy12, DEV_Page2_TextBox_Enemy13, DEV_Page2_TextBox_Enemy14, DEV_Page2_TextBox_Enemy15, DEV_Page2_TextBox_Enemy16 };
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
            {
                enemyBoxes[i].Text = max.ToString();
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp), max);
            }
        }

        private void DEV_Page2_TextBox_Enemy1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy1.Text == "") DEV_Page2_TextBox_Enemy1.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy1.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(0, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy2.Text == "") DEV_Page2_TextBox_Enemy2.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy2.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(1, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy3_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy3.Text == "") DEV_Page2_TextBox_Enemy3.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy3.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(2, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy4_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy4.Text == "") DEV_Page2_TextBox_Enemy4.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy4.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(3, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy5_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy5.Text == "") DEV_Page2_TextBox_Enemy5.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy5.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(4, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy6_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy6.Text == "") DEV_Page2_TextBox_Enemy6.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy6.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(5, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy7_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy7.Text == "") DEV_Page2_TextBox_Enemy7.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy7.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(6, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy8_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy8.Text == "") DEV_Page2_TextBox_Enemy8.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy8.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(7, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy9_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy9.Text == "") DEV_Page2_TextBox_Enemy9.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy9.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(8, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy10_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy10.Text == "") DEV_Page2_TextBox_Enemy10.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy10.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(9, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy11_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy11.Text == "") DEV_Page2_TextBox_Enemy11.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy11.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(10, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy12_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy12.Text == "") DEV_Page2_TextBox_Enemy12.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy12.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(11, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy13_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy13.Text == "") DEV_Page2_TextBox_Enemy13.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy13.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(12, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy14_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy14.Text == "") DEV_Page2_TextBox_Enemy14.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy14.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(13, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy15_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy15.Text == "") DEV_Page2_TextBox_Enemy15.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy15.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(14, EnemySlotOffsets.Hp), v);
        }

        private void DEV_Page2_TextBox_Enemy16_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DEV_Page2_TextBox_Enemy16.Text == "") DEV_Page2_TextBox_Enemy16.Text = "0";
            if (int.TryParse(DEV_Page2_TextBox_Enemy16.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(15, EnemySlotOffsets.Hp), v);
        }

        #endregion

        #region Info/links

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://docs.google.com/forms/d/e/1FAIpQLSdIaCjLTJ9aRqQVO731o2UwQKByF85W_yAj54pssO1RMkLewQ/viewform?usp=sf_link") { UseShellExecute = true });
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/8KcnBjgRHP") { UseShellExecute = true });
        }

        #endregion
    }
}
