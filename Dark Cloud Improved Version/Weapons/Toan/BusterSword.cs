using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Buster Sword — anti-category attachments are worth +4 instead of +3.</summary>
    internal static class BusterSword
    {
        // ── Buster Sword "Buster Boost" ─────────────────────────────────────────────────────
        private static DateTime _busterNextTick = DateTime.MinValue;

        /// <summary>
        /// Ability Name: Buster Boost (Buster Sword)
        /// Anti-category attachments (Dinoslayer..Mage Slayer, items 111-120) are worth +4
        /// instead of +3 while attached to a Buster Sword. Implemented by editing the attachment
        /// ENTRY inside the weapon record (its own anti byte, template base 3 → 4): the entry's
        /// values — not the item template — are what the engine reads everywhere, so the +1 is
        /// automatically part of the attachment's temporary stats: it counts in menus, in
        /// build-up (evolution) eligibility (CompareBuildUpModelData2 recomputes from the record
        /// + entries), in combat once the battle copy rebuilds, it disappears when the
        /// attachment is removed (detaching discards the entry), and the level-up absorb bakes
        /// the full +4 into base stats natively (AttachMentValuePlus consumes entry values).
        ///
        /// Stateless and restart-safe by design: an entry at the base value (3) gets bumped
        /// once; an already-bumped entry (4) is left alone. A bumped entry can never migrate to
        /// another weapon: detaching discards it, and the engine refuses Build Up while any
        /// attachment is on the weapon (NowWeaponStatusValue gates the option on
        /// GetNowWeaponAttachNum == 0). Runs from the mod main loop in ALL modes (self-gated to
        /// ~2.5 Hz) because attaching can happen from any menu, sword equipped or not.
        /// </summary>
        public static void BusterBoostEffect()
        {
            if (DateTime.UtcNow < _busterNextTick) return;
            _busterNextTick = DateTime.UtcNow.AddMilliseconds(400);

            byte[] bag = Memory.ReadBytesBatch(WeaponHave.InventoryWeaponSlot0Id,
                10 * WeaponHave.InventoryWeaponSlotStride);
            if (bag == null || bag.Length < 10 * WeaponHave.InventoryWeaponSlotStride) return;

            for (int slot = 0; slot < 10; slot++)
            {
                int off = slot * WeaponHave.InventoryWeaponSlotStride;
                if (BitConverter.ToUInt16(bag, off) != Items.bustersword) continue;

                for (int j = 0; j < WeaponHave.WeaponAttachSlotCount; j++)
                {
                    int entryOff = off + WeaponHave.WeaponAttachSlot0Offset +
                                   j * WeaponHave.WeaponAttachSlotStride;
                    ushort attachId = BitConverter.ToUInt16(bag, entryOff);
                    if (attachId < Items.dragonslayer || attachId > Items.mageslayer) continue;

                    int cat = attachId - Items.dragonslayer;   // ids 111-120 are in anti-byte order
                    int valOff = entryOff + WeaponHave.AttachEntryAntiOffset + cat;
                    if (bag[valOff] != WeaponHave.AttachAntiBaseValue) continue;

                    Memory.WriteByte(WeaponHave.InventoryWeaponSlot0Id + valOff,
                        (byte)(WeaponHave.AttachAntiBaseValue + 1));
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Buster Sword: attachment {attachId} boosted to +{WeaponHave.AttachAntiBaseValue + 1} (bag slot {slot})");
                }
            }
        }
    }
}
