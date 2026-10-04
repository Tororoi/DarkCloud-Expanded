namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Mint's (Brownboo) master-fish quest: catch all 17 species. <see cref="CheckFish"/> reads the per-species
    /// caught flags (0x21CE4439..), sets the completion flag 0x21CE444F and builds the still-to-find list;
    /// <see cref="CheckMasterFishQuestReward"/> looks for the reward (item 191) in bag and storage;
    /// <see cref="GiveMasterFishQuestReward"/> hands it over and sets 0x21CE4450 (TownLoop calls it when Toan
    /// talks to Mint with the quest complete).
    /// </summary>
    internal static class MasterFishQuest
    {
        private const string Tag = "[MasterFishQuest] ";

        static int currentAddress;
        static string[] allfish = { "Bobo", "Gobbler", "Nonky", "Kaiji", "Baku Baku", "Mardan Garayan", "Gummy", "Niler", "null", "Umadakara", "Tarton", "Piccoly", "Bon", "Hamahama", "Negie", "Den", "Heela", "Baron Garayan" };
        /// <summary>The species not yet caught, one per line, bubble breaks every four (built by CheckFish).</summary>
        internal static string fishToFind;
        static bool[] fishCheckList = new bool[18];
        internal static bool masterFishQuestComplete = false;
        public static bool alreadyHasSavingBook = false;

        public static void CheckMasterFishQuestReward()
        {
            int itemid;
            alreadyHasSavingBook = false;

            currentAddress = 0x21CDD8BA; //first inventory slot
            for (int i = 0; i < 100; i++) //check which items player has in bag
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid == 191)
                {
                    alreadyHasSavingBook = true;
                }
                currentAddress += 0x00000002;
            }

            currentAddress = 0x21CE21E8; //first storage slot
            for (int i = 0; i < 60; i++) //check which items player has in storage
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid == 191)
                {
                    alreadyHasSavingBook = true;
                }
                currentAddress += 0x00000002;
            }
        }

        public static void GiveMasterFishQuestReward()
        {
            Memory.WriteUShort(Addresses.firstBagItem + (0x2 * Inventory.GetBagItemsFirstAvailableSlot()), 191);
            Memory.WriteByte(0x21CE4450, 1);
        }

        public static void CheckFish() //this is the checklist for the fishing master quest
        {
            currentAddress = 0x21CE4439;
            byte fishCount = 0;
            for (int i = 0; i < allfish.Length; i++)
            {
                if (i != 8)
                {
                    if (Memory.ReadByte(currentAddress) == 1)
                    {
                        fishCheckList[i] = true;
                        fishCount++;
                    }
                }
                currentAddress += 0x00000001;
            }
            if (fishCount == 17)
            {
                masterFishQuestComplete = true;
                Memory.WriteByte(0x21CE444F, 1);
            }
            else
            {
                masterFishQuestComplete = false;
                byte fishAmountToFind = 0;
                fishToFind = "";
                for (int i = 0; i < allfish.Length; i++)
                {
                    if (i != 8)
                    {
                        if (fishCheckList[i] != true)
                        {
                            fishToFind += allfish[i];
                            fishAmountToFind++;
                            if (fishAmountToFind == 4 || fishAmountToFind == 8 || fishAmountToFind == 12)
                            {
                                fishToFind += "¤";
                            }
                            else
                            {
                                fishToFind += "^";
                            }
                        }
                    }
                }
                if (fishAmountToFind == 4 || fishAmountToFind == 8 || fishAmountToFind == 12)
                {
                    fishToFind = fishToFind.Remove(fishToFind.Length - 1, 1);
                }
            }
        }
    }
}
