using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The item and attachment bags: finding the first free slot and writing an attachment in.
    /// </summary>
    internal static class Inventory
    {
        private const string LogTag = "[Inventory] ";

        /// <summary>
        /// Returns the total sum quantity of all 3 items currently on the active item slots.
        /// </summary>
        public static int GetActiveItemsQuantity()
        {
            const byte itemOffset = 0x2;
            int quantityTotal = 0;

            for (int slot = 0; slot < 3; slot++)
            {
                int itemQuantity = Memory.ReadUShort(Addresses.activeItem1Quantity + (itemOffset * slot));
                quantityTotal += itemQuantity;
            }

            return quantityTotal;
        }

        /// <summary>
        /// Returns an array with all the current inventory slots.
        /// </summary>
        /// <returns>[ItemId] if there is an item on the slot;<br>[-1] if slot is empty;</br></returns>
        public static int[] GetBagItems()
        {
            const byte itemOffset = 0x2;
            byte inventorySize = Memory.ReadByte(PlayerAddresses.InventoryTotalSize);
            int[] inventoryItems = new int[inventorySize + 2]; //The 2 is to account for 2 extra yellow item slots

            //Run through the inventory bag (+ 2 is to reserve 2 yellow slots)
            for (int slot = 0; slot < inventorySize + 2; slot++)
            {
                //Read the current item ID
                int itemId = Memory.ReadUShort(Addresses.firstBagItem + (itemOffset * slot));

                //Check if the item is an inventory item, store the ID if it is; -1 if it isn't (aka empty)
                if (itemId >= Items.dummy129 && itemId <= Items.dummy256)
                {
                    inventoryItems[slot] = itemId;
                }
                else inventoryItems[slot] = -1;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Finished GetBagItems process!");
            return inventoryItems;
        }

        /// <summary>
        /// Returns the first empty slot found in the inventory.
        /// </summary>
        /// <returns>SlotNumber of the inventory slot if an empty one is found;<br>-1 if no empty slot is found;</br></returns>
        public static int GetBagItemsFirstAvailableSlot()
        {
            int slot = 0;
            int counter = GetActiveItemsQuantity();
            int[] inventoryBag = GetBagItems();

            foreach (int item in inventoryBag)
            {
                //Run until it find an empty slot and return the slot number if found
                if (item == -1)
                {
                    if (counter == 0) return slot;
                    counter--;
                }
                slot++;
            }

            //Return -1 if no empty slot was found
            return -1;
        }

        /// <summary>
        /// Returns an array of the attachment inventory slots and the item ids occupying it.
        /// </summary>
        public static int[] GetBagAttachments()
        {
            const byte itemOffset = 0x20;
            byte inventorySize = PlayerAddresses.InventorySizeAttachments;
            int[] inventoryAttachments = new int[inventorySize + 2];

            //Run through the attachment bag
            for (int slot = 0; slot < inventorySize + 2; slot++)
            {
                //Store the attachment ID
                int itemId = Memory.ReadUShort(Addresses.firstBagAttachment + (itemOffset * slot));

                //Check if there is an attachment in the slot and store its ID or store -1 if no attachment is found
                if (itemId >= Items.fire && itemId <= 1000)
                {
                    inventoryAttachments[slot] = itemId;
                }
                else
                {
                    inventoryAttachments[slot] = -1;
                }
            }

            return inventoryAttachments;
        }

        /// <summary>
        /// Returns the first empty slot found in the attachment bag.<br> Returns -1 if no empty slot is found.</br>
        /// </summary>
        public static int GetBagAttachmentsFirstAvailableSlot()
        {
            int slot = 0;
            int[] attachmentBag = GetBagAttachments();

            //Run until you find an empty slot and return the slot number if found
            foreach (int item in attachmentBag)
            {
                if (item == -1)
                {
                    return slot;
                }
                slot++;
            }

            return -1;
        }

        /// <summary>
        /// Set an item in the chosen attachment inventory slot.
        /// </summary>
        /// <param name="attachmentId">The item id.</param>
        /// <param name="slot">The slot in the attachment inventory.</param>
        public static void SetBagAttachments(int attachmentId, int slot = -1)
        {
            const int attachmentOffset = 0x20;
            const int attachmentValuesRange = 0x1F;
            const int tableAttachmentFirstAddress = 0x2027CA60;

            if (slot >= 0)
            {
                if (GetBagAttachments()[slot] == -1)
                {
                    try
                    {
                        //Fetch the values from the original values database
                        byte[] attachmentValues = Memory.ReadByteArray(tableAttachmentFirstAddress + (attachmentOffset * (attachmentId - Items.fire)), attachmentValuesRange);

                        //Write the values on the specified location
                        Memory.WriteByteArray(Addresses.firstBagAttachment + (attachmentOffset * slot), attachmentValues);
                    }
                    catch
                    {
                        // Compares the slot against the bag-size ADDRESS (0x21CDD8AC), so this retry never fires; kept as it was.
                        if (slot > PlayerAddresses.InventoryTotalSize && (attachmentId >= Items.fire && attachmentId <= Items.mageslayer)) SetBagAttachments(attachmentId, GetBagAttachmentsFirstAvailableSlot());
                        else Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Invalid inputs for SetBagAttachments\n");
                    }
                }
                else Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Attachment bag is full!\n");
            }
            else
            {
                SetBagAttachments(attachmentId, GetBagAttachmentsFirstAvailableSlot());
                return;
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Finished SetBagAttachments process!\n");
        }
    }
}
