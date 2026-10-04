namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Pickle's (Brownboo) 100%-collection quest: which of the obtainable items, attachments, ultimate weapons and
    /// secret items the player holds anywhere (active slots, bag, storage, weapon bags). <see cref="CheckItems"/>
    /// tallies them into the three counters; <see cref="Dialogues.SetDialogue"/> reads the counters for Pickle's
    /// progress line (and hands out the old key at 100%) and then calls <see cref="ResetProgress"/>.
    /// </summary>
    internal static class BrownbooCollectionQuest
    {
        private const string Tag = "[BrownbooCollection] ";

        static int currentAddress;
        static bool[] itemIDCheckList = new bool[380];
        internal static int[] obtainableAttachmentsList = { 81, 82, 83, 84, 85, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120 };
        internal static int[] obtainableItemsList = { 132, 133, 134, 135, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 174, 175, 176, 177, 178, 181, 183, 185, 186, 187, 188, 189, 190, 192, 193, 197, 199, 224, 225, 226, 227, 228, 229, 230, 231, 235, 245, 246, 247, 253 };
        internal static int[] obtainableUltWeapons = { 280, 295, 296, 297, 298, 312, 313, 324, 329, 341, 345, 356, 357, 372, 373 };
        internal static int[] obtainableSecretItems = { 171, 172, 173, 191, 233, 234, 241, 243 };
        /// <summary>Items AND attachments found (both lists count into this one).</summary>
        internal static int obtainedItems = 0;
        internal static int obtainedUltWeapons = 0;
        internal static int obtainedSecretItems = 0;

        public static void CheckItems()
        {
            int itemid;

            currentAddress = 0x21CDD8AE; //first active item slot
            for (int i = 0; i < 3; i++) //check which items player has active slots
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x00000002;
            }

            currentAddress = 0x21CDD8BA; //first inventory slot
            for (int i = 0; i < 100; i++) //check which items player has in bag
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x00000002;
            }
            currentAddress = 0x21CE21E8; //first storage slot
            for (int i = 0; i < 60; i++) //check which items player has in storage
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x00000002;
            }

            for (int i = 0; i < obtainableItemsList.Length; i++) //increase counter for each unique item
            {
                if (itemIDCheckList[obtainableItemsList[i]] == true)
                {
                    obtainedItems++;
                }
            }

            currentAddress = 0x21CE1A48; //first attachment slot
            for (int i = 0; i < 40; i++) //check which attachments player has in bag
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x00000020;
            }

            currentAddress = 0x21CE3FE8; //first storage attachment slot
            for (int i = 0; i < 30; i++) //check which attachments player has in storage
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x00000020;
            }

            for (int i = 0; i < obtainableAttachmentsList.Length; i++) //increase counter for each unique item
            {
                if (itemIDCheckList[obtainableAttachmentsList[i]] == true)
                {
                    obtainedItems++;
                }
            }

            currentAddress = 0x21CDDA58; //first weapon ID in bag
            for (int i = 0; i < 65; i++) //check which weapons player is carrying
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x000000F8;
            }

            currentAddress = 0x21CE22D8; //first weapon slot in storage
            for (int i = 0; i < 30; i++) //check which weapons player has in storage
            {
                itemid = Memory.ReadUShort(currentAddress);
                if (itemid < 380)
                {
                    itemIDCheckList[Memory.ReadUShort(currentAddress)] = true;
                }
                currentAddress += 0x000000F8;
            }

            for (int i = 0; i < obtainableUltWeapons.Length; i++) //increase counter for each unique item
            {
                if (itemIDCheckList[obtainableUltWeapons[i]] == true)
                {
                    obtainedUltWeapons++;
                }
            }

            for (int i = 0; i < obtainableSecretItems.Length; i++) //increase counter for each unique item
            {
                if (itemIDCheckList[obtainableSecretItems[i]] == true)
                {
                    obtainedSecretItems++;
                }
            }
        }

        /// <summary>Clears the counters and the seen-id list after a progress read, ready for the next tally.</summary>
        internal static void ResetProgress()
        {
            obtainedItems = 0;
            obtainedUltWeapons = 0;
            obtainedSecretItems = 0;
            for (int i = 0; i < itemIDCheckList.Length; i++)
            {
                itemIDCheckList[i] = false;
            }
        }
    }
}
