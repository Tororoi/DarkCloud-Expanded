using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The mod's all-mode game loop: one background thread, started once a save is in play
    /// (MainMenuThread and ModWindow start it as <see cref="Run"/>).
    /// Start-up applies the save-load pokes (item table, mayor-quest HP, daily shop, Sword of Zeus max
    /// attack, the ally .chr/.cfg path slots). Then, every 50 ms, in this order: the weapon ownership
    /// passives (<see cref="WeaponPassives"/>), the town branch while the game is in town mode 2
    /// (<see cref="TownLoop.Tick"/>), the user-mode exit when the game drops back to the title/menu
    /// modes 0/1, the credits hand-off in mode 13, and the per-tick town features
    /// (<see cref="TownLoop.TickFeatures"/>, each self-gated to town).
    /// </summary>
    internal static class GameLoop
    {
        private const string Tag = "[GameLoop] ";

        static bool playerAtCredits = false;

        /// <summary>The loop body. Returns only when user mode sees the game leave the in-game modes.</summary>
        public static void Run()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "running");

            TownDialogueText.Initialize(); //pre-loads all custom dialogue
            Memory.WriteByte(0x2027DD50, 0); //make shell ring discardable
            Memory.WriteByte(0x2027DD28, 0); //make magical lamp discardable
            Memory.WriteByte(0x2027DC80, 8); //change map ordering
            Memory.WriteByte(0x2027DC94, 8); //change magical crystal ordering
            Memory.WriteByte(0x20291CEE, 1); //make hardening powder cost 1g
            Memory.WriteByte(0x2027D808, 0); //make escape powder equippable+stackable
            Memory.WriteByte(0x2027D7F8, 2); //make escape powder have arrow point to active slots
            Memory.WriteByte(0x2027D81C, 0); //make revival powder stackable
            Memory.WriteByte(0x2027D830, 0); //make repair powder equipable+stackable
            Memory.WriteByte(0x2027D8A8, 0); //make auto-repair powder stackable
            Memory.WriteByte(0x21CB6AEC, 50); //fix matador model in chest loot
            Memory.WriteByte(0x21CB6AF7, 50); //
            Memory.WriteByte(0x21CB6B02, 51); //
            Memory.WriteByte(0x21CB6B0D, 51); //


            if (Memory.ReadByte(0x21CE4464) != 0) //max hps for mayor quest
            {
                Memory.WriteByte(0x20293978, 250);
                Memory.WriteByte(0x2029397A, 250);
                Memory.WriteByte(0x2029397C, 250);
                Memory.WriteByte(0x2029397E, 250);
                Memory.WriteByte(0x20293980, 250);
                Memory.WriteByte(0x20293982, 250);
            }

            DailyShopItem.BaseShopChanges();
            DailyShopItem.SetDailyItemsToShop();
            TownLoop.Init();

            Dungeon.ChangeSoZMaxAtt(Memory.ReadUShort(0x21CE446D)); //NEEDS TO BE APPLIED AFTER SAVE LOAD!

            //Seed the relocated character-file slots with Toan (the game reads its .chr path / .cfg name from there)
            AllySwitch.InitializeCharacterOffsetValues();

            while (true)
            {
                WeaponPassives.Tick();

                //Check if player is in town
                if (Memory.ReadByte(Addresses.mode) == 2)
                {
                    TownLoop.Tick();
                } //end of check if player is in town mode

                if (MainMenuThread.userMode == true)
                {
                    if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                    {
                        Thread.Sleep(100);
                        if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Not ingame anymore! Exited from the game loop!");
                            break;
                        }
                    }
                }

                if (Memory.ReadByte(Addresses.mode) == 13)
                {
                    CheckCreditsScene(); //when player finishes credits, properly save the game and redirect to demon shaft
                }

                TownLoop.TickFeatures();

                Thread.Sleep(50); //resets the code loop in 50ms intervals. Sleep is required, otherwise CPU usage will skyrocket
            }

        }

        /// <summary>Mode 13: the credits. Sets the mod's game-cleared flag (and the Demon Shaft visit count) on the
        /// credits scene, then steers the post-credits menu into a proper save.</summary>
        static void CheckCreditsScene()
        {
            if (Memory.ReadInt(0x202A2518) == -1 && playerAtCredits == false) //credits scene
            {
                playerAtCredits = true;
                Memory.WriteByte(0x21CE448B, 1); //game cleared flag, our custom flag
                if (Memory.ReadByte(0x21CE70A0) == 0)
                {
                    Memory.WriteByte(0x21CE70A0, 1); //demon shaft visit count
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Game beaten, entered save mode after credits!");
            }
            else if (Memory.ReadInt(0x202A2518) != 51 && playerAtCredits == true)
            {
                Memory.WriteInt(0x202A2518, 60);
                if (Memory.ReadByte(0x21DA8AD0) == 2 && Memory.ReadByte(0x21DA8AE3) < 255)
                {
                    Memory.WriteByte(0x21DA8AD0, 1); //changes the menu to properly save the game
                    playerAtCredits = false;
                }
            }
        }
    }
}
