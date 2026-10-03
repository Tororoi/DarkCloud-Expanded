namespace Dark_Cloud_Improved_Version
{
    /// <summary>Whether a weapon is OWNED: in any character's hand or bag (the six characters' ten inventory records), or
    /// in storage (its thirty records). What an ownership passive gates on.</summary>
    internal static class WeaponOwnership
    {
        private const int Characters = 6, StorageSlots = 30;
        internal static bool Owned(int weaponId)
        {
            for (int c = 0; c < Characters; c++)
                for (int s = 0; s < DngStatusData.MaxWeaponSlots; s++)
                    if (Memory.ReadUShort(DngStatusData.WeaponRecord(c, s)) == weaponId) return true;
            for (int s = 0; s < StorageSlots; s++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + s * WeaponHave.InventoryWeaponSlotStride) == weaponId) return true;
            return false;
        }
    }
}
