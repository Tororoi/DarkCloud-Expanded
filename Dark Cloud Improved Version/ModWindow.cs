using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using MsBoxButtonEnum = MessageBox.Avalonia.Enums.ButtonEnum;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;
using ButtonResult = MsBox.Avalonia.Enums.ButtonResult;
using ThreadState = System.Threading.ThreadState;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Launch mode, chosen by a command-line arg (see launchSettings.json / Makefile). Sandbox = User + the Sandbox tab.</summary>
    public enum LaunchMode { User, Dev, Sandbox }

    public partial class ModWindow : Window
    {
        private static ModWindow instance;

        // Set from the command-line arg in Program.Main before the window is created.
        public static LaunchMode Mode = LaunchMode.User;

        public ModWindow()
        {
            InitializeComponent();
            instance = this;
            if (Mode == LaunchMode.Dev) DevModeLaunch();
            else UserModeLaunch();   // User and Sandbox both use the user tabs; Sandbox additionally shows the Sandbox tab
        }

        public static Thread townThread = new Thread(new ThreadStart(GameLoop.Run)) { IsBackground = true };
        public static Thread dungeonthread = new Thread(new ThreadStart(Dungeon.InsideDungeonThread)) { IsBackground = true };
        public static Thread launchThread = new Thread(new ThreadStart(SessionController.CheckEmulatorAndGame)) { IsBackground = true };

        public bool nightlyVersion = false;

        /// <summary>The sixteen per-floor-slot enemy HP boxes on Dev page 2, in slot order.</summary>
        private TextBox[] EnemyHpBoxes => new[] { DEV_Page2_TextBox_Enemy1, DEV_Page2_TextBox_Enemy2, DEV_Page2_TextBox_Enemy3, DEV_Page2_TextBox_Enemy4, DEV_Page2_TextBox_Enemy5, DEV_Page2_TextBox_Enemy6, DEV_Page2_TextBox_Enemy7, DEV_Page2_TextBox_Enemy8, DEV_Page2_TextBox_Enemy9, DEV_Page2_TextBox_Enemy10, DEV_Page2_TextBox_Enemy11, DEV_Page2_TextBox_Enemy12, DEV_Page2_TextBox_Enemy13, DEV_Page2_TextBox_Enemy14, DEV_Page2_TextBox_Enemy15, DEV_Page2_TextBox_Enemy16 };

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
                SessionController.saveFileMessageBox = true;
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
                SessionController.saveStateUsed = true;
                string message = "The mod has detected a possible save state load!\n\nUsing save states is NOT ALLOWED while using the Enhanced Mod, since it can cause major issues.\n\nThe game has been reset, and this mod will be closed.";
                var box = MessageBoxManager.GetMessageBoxStandard("Save state detected!", message, MsBoxButtonEnum.Ok, MsBoxIcon.Warning);
                await box.ShowWindowDialogAsync(this);
                Topmost = false;
                Label_UserMode_PlaceholderText.Text = "A possible save state used! Mod has been terminated.";
                Memory.WriteByte(Mailbox.PineProbe, 0);
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

        // Restore the Options tab from the persisted toggles (ModOptions owns the save bytes and each toggle's effect):
        // every box is set from its bit and the effect re-applied, so a loaded save plays with the options it was saved with.
        void ModWindowSettingsCheck(bool enable)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ModOptions.State saved = ModOptions.Load();   // the three category bytes, read once

                // ── Graphics ──
                RestoreOption(CBox_UserMode_Graphics,          ModOption.Graphics,          saved);
                RestoreOption(CBox_UserMode_Widescreen,        ModOption.Fov,               saved);
                // ── Audio ──
                RestoreOption(CBox_UserMode_WeaponBeeps,       ModOption.WeaponBeeps,       saved);
                RestoreOption(CBox_UserMode_BattleMusic,       ModOption.BattleMusic,       saved);
                RestoreOption(Cbox_Usermode_AttackSounds,      ModOption.AttackSounds,      saved);
                RestoreOption(CBox_UserMode_MuteMusic,         ModOption.MuteMusic,         saved);
                // ── Gameplay ──
                RestoreOption(CBox_UserMode_FasterEnemies,     ModOption.FasterEnemies,     saved);
                RestoreOption(CBox_UserMode_StrongerEnemies,   ModOption.StrongerEnemies,   saved);
                RestoreOption(CBox_UserMode_RandomizedEnemies, ModOption.RandomizedEnemies, saved);
                RestoreOption(CBox_UserMode_HarderAI,          ModOption.HarderAi,          saved);
            });
        }

        private static void RestoreOption(CheckBox box, ModOption option, ModOptions.State saved)
        {
            bool on = saved[option];
            box.IsChecked = on;
            ModOptions.Apply(option, on);
        }

        void UserModeLaunch()
        {
            TabControl_USER.IsVisible = true;
            Container_MainModes.IsVisible = false;
            Tab_Sandbox.IsVisible = (Mode == LaunchMode.Sandbox);   // Sandbox tab (roster editor + tools) only in sandbox mode
            if (!launchThread.IsAlive) launchThread.Start();
        }

        void DevModeLaunch()
        {
            TabControl_DEV.IsVisible = true;
            Container_MainModes.IsVisible = false;
            if (!launchThread.IsAlive) launchThread.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            Memory.WriteByte(Mailbox.PineProbe, 0);
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

            TextBox[] enemyBoxes = EnemyHpBoxes;
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

        // Each toggle: ModOptions performs its effect and persists its bit (Core/ModOptions.cs).
        private void CBox_UserMode_WeaponBeepsChanged(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.WeaponBeeps, CBox_UserMode_WeaponBeeps.IsChecked == true);

        // Handles the Battle Music toggle (legacy method name).
        private void CBox_UserMode_GraphicsChanged(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.BattleMusic, CBox_UserMode_BattleMusic.IsChecked == true);

        private void CBox_UserMode_Widescreen_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.Fov, CBox_UserMode_Widescreen.IsChecked == true);

        private void CBox_UserMode_Graphics_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.Graphics, CBox_UserMode_Graphics.IsChecked == true);

        // Difficulty toggles. "Faster enemies" = FasterEnemies (movement + attack speed, with the hit-window dwell);
        // "Stronger enemies" = EnemyStatNormalizer normalizing every enemy to the next dungeon/band up.
        private void CBox_UserMode_FasterEnemies_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.FasterEnemies, CBox_UserMode_FasterEnemies.IsChecked == true);

        private void CBox_UserMode_StrongerEnemies_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.StrongerEnemies, CBox_UserMode_StrongerEnemies.IsChecked == true);

        private void CBox_UserMode_RandomizedEnemies_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.RandomizedEnemies, CBox_UserMode_RandomizedEnemies.IsChecked == true);

        // "Harder enemy AI" — first behavior: every enemy with a get-up motion can revive after death
        // (HarderEnemyAI splices a revive roll into each loaded death script; native revivers get buffed odds).
        private void CBox_UserMode_HarderAI_Changed(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.HarderAi, CBox_UserMode_HarderAI.IsChecked == true);

        private void Cbox_Usermode_AttackSounds_CheckedChanged(object sender, RoutedEventArgs e)
            => ModOptions.Set(ModOption.AttackSounds, Cbox_Usermode_AttackSounds.IsChecked == true);

        // Sets the current dungeon's spawn roster from the box's spec ("20", "20,3,6", "20!,60" = one Gyon + Cursed Roses,
        // "iq" = the exact Ice Queen boss block) — SpawnRoster.ApplySpec parses and writes it; takes effect on the next floor.
        private void Btn_Injector_Test_Click(object sender, RoutedEventArgs e)
        {
            SpawnRoster.ApplySpec(Tbox_Injector_Table.Text);
        }

        // Post-spawn cap: keep at most 1 of the TableIndex species on the current floor, remove extras.
        private void Btn_Injector_BossAI_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(Tbox_Injector_Table.Text, out int tableIndex))
            {
                Console.WriteLine("Injector: TableIndex must be an integer (e.g. 20 = Gyon).");
                return;
            }
            SpawnRoster.CapSpeciesOnFloor(tableIndex, 1);
        }

        private void CBox_UserMode_MuteMusic_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool on = CBox_UserMode_MuteMusic.IsChecked == true;
            ModOptions.Set(ModOption.MuteMusic, on);

            // Muting also forces Battle Music off (its handler writes the effect + audio bit1).
            if (on && CBox_UserMode_BattleMusic.IsChecked == false)
                CBox_UserMode_BattleMusic.IsChecked = true;
        }

        #endregion

        #region Dev Page 1

        private void DEV_Page1_Btn_Dayuppy(object sender, RoutedEventArgs e)
        {
        }

        private void DEV_Page1_Btn_Mike(object sender, RoutedEventArgs e)
        {
            if (SessionController.changesThread.ThreadState == ThreadState.Unstarted)
                SessionController.changesThread.Start();
            if (WeaponSynthSphereLevel.Listener.ThreadState == ThreadState.Unstarted)
                WeaponSynthSphereLevel.Listener.Start();
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

        // Starts the debug-menu button watcher (the same thread the in-game "debug menus" cheat starts).
        private void DEV_Page1_CBox_DebugThread(object sender, RoutedEventArgs e)
        {
            if (CBox_DebugThread.IsChecked == true)
            {
                if (CheatCodes.InputBuffer.debugThread.ThreadState == ThreadState.Unstarted)
                    CheatCodes.InputBuffer.debugThread.Start();
                CBox_DebugThread.IsEnabled = false;
            }
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
            TextBox[] enemyBoxes = EnemyHpBoxes;
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
            {
                enemyBoxes[i].Text = max.ToString();
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp), max);
            }
        }

        // One handler for all sixteen enemy HP boxes: each box's Tag (ModWindow.axaml) is its 0-based floor slot.
        // An emptied box is reset to "0" (which re-enters here and writes 0); a parseable value is written to that slot's HP.
        private void DEV_Page2_TextBox_Enemy_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            int slot = Convert.ToInt32(box.Tag);
            if (box.Text == "") box.Text = "0";
            if (int.TryParse(box.Text, out int v)) Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp), v);
        }

        #endregion

        #region Info/links

        // "Submit Bug Report" — opens the feedback form.
        private void Btn_BugReport_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://docs.google.com/forms/d/e/1FAIpQLSdIaCjLTJ9aRqQVO731o2UwQKByF85W_yAj54pssO1RMkLewQ/viewform?usp=sf_link") { UseShellExecute = true });
        }

        // "Join our Discord!"
        private void Btn_Discord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/8KcnBjgRHP") { UseShellExecute = true });
        }

        // ── ISO patch flow (General tab) ──────────────────────────────────────────────────────────────
        private async void Btn_BrowseIso_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select your Dark Cloud (USA) ISO",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("PS2 ISO") { Patterns = new[] { "*.iso", "*.ISO" } } }
            });
            if (files.Count == 0) return;
            string path = files[0].Path.LocalPath;
            Txt_IsoPath.Text = path;
            if (string.IsNullOrWhiteSpace(Txt_OutDir.Text))   // default the output folder to the ISO's own folder
                Txt_OutDir.Text = Path.GetDirectoryName(path);
            Btn_CreatePatched.IsEnabled = true;
            Txt_PatchStatus.Text = "";
        }

        private async void Btn_BrowseOut_Click(object sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the output folder for the patched ISO",
                AllowMultiple = false
            });
            if (folders.Count > 0) Txt_OutDir.Text = folders[0].Path.LocalPath;
        }

        private async void Btn_CreatePatched_Click(object sender, RoutedEventArgs e)
        {
            string iso = Txt_IsoPath.Text;
            string outDir = string.IsNullOrWhiteSpace(Txt_OutDir.Text) ? Path.GetDirectoryName(iso) : Txt_OutDir.Text;
            Btn_CreatePatched.IsEnabled = false; Btn_BrowseIso.IsEnabled = false; Btn_BrowseOut.IsEnabled = false;
            try
            {
                string outIso = await Task.Run(() => IsoPatcher.Patch(iso, outDir,
                    msg => Dispatcher.UIThread.Post(() => Txt_PatchStatus.Text = msg)));
                Txt_PatchStatus.Text = $"Done! Patched ISO:\n{outIso}\nPnach published to your PCSX2 cheats folder — enable Cheats in PCSX2 for this game.";
            }
            catch (Exception ex)
            {
                Txt_PatchStatus.Text = "Patch failed: " + ex.Message;
            }
            finally
            {
                Btn_CreatePatched.IsEnabled = true; Btn_BrowseIso.IsEnabled = true; Btn_BrowseOut.IsEnabled = true;
            }
        }

        #endregion
    }
}
