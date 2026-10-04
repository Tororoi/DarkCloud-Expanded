namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The NPC the player is standing next to in town, as shared state between the dialogue writer
    /// (<see cref="Dialogues.SetDialogue"/> fills it), the side-quest generator (<see cref="SideQuestManager"/>
    /// writes the id and the mayor's reward) and the town tick (<see cref="TownLoop"/> reads it to drive the
    /// shopkeeper / sidequest / "it's finished" dialogue options and the quest state machine).
    /// Also the compile-time entry points the thread starters and ModWindow still bind to; they forward to
    /// <see cref="GameLoop"/>, <see cref="AllySwitch"/> and <see cref="Fishing"/>.
    /// </summary>
    class TownCharacter
    {
        /// <summary>Id word of the NPC the last SetDialogue targeted (the `characterIdData` short at villager+0x..D9).</summary>
        public static int characterIDData;
        /// <summary>False only for the llama (id 14132): no near-NPC mailbox flag is raised for it.</summary>
        public static bool talkableNPC = true;
        /// <summary>The nearby NPC is a shopkeeper / storage keeper (its talk menu gets the mod's dialogue ids on different slots).</summary>
        public static bool shopkeeper = false;
        /// <summary>Talk-message id of the current area's sidequest option, set per area by SetDialogue.</summary>
        public static int sidequestDialogueID = 0;
        /// <summary>Talk-message id of the current area's "It's finished!" option, set per area by SetDialogue.</summary>
        public static int itsfinishedDialogueID = 0;
        /// <summary>Sam's Queens fishing quest flag (SideQuestManager sets it; the reward step writes 0x21CE4430).</summary>
        public static bool queensQuest = false;
        /// <summary>Item id the mayor hands over at the end of his quest (SideQuestManager picks it).</summary>
        public static byte mayorReward;

        /// <summary>Thread-start entry kept for MainMenuThread / ModWindow: the all-mode game loop.</summary>
        public static void MainScript() => GameLoop.Run();

        /// <summary>Entry kept for MainMenuThread: seeds the relocated .chr/.cfg path slots with Toan.</summary>
        public static void InitializeCharacterOffsetValues() => AllySwitch.InitializeCharacterOffsetValues();

        /// <summary>Entry kept for ModWindow's status panel: the fishing sub-state probe (<see cref="Fishing.FishProbe"/>).</summary>
        internal static int[] FishProbe => Fishing.FishProbe;
    }
}
