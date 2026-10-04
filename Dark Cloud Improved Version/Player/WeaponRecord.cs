namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Field offsets inside one inventory weapon record (the engine's WEAPON_HAVE, 0xF8 bytes) and the address law
    /// that places a record: character block (0xAA8 apart) → bag slot (0xF8 apart) → field.
    ///
    /// <c>WeaponRecord.Address(Player.ToanId, 0, WeaponRecord.Id)</c> == 0x21CDDA58 (Toan slot 0's weapon id), and
    /// the in-battle copy of the equipped weapon (<c>WeaponHave.BattleWeaponRecord</c>) shares this layout.
    /// </summary>
    internal static class WeaponRecord
    {
        internal const int Stride = WeaponHave.InventoryWeaponSlotStride;   // 0xF8 between bag slots

        // ── Stats ──
        internal const int Id        = 0x00;   // ushort: weapon item id
        internal const int Level     = 0x02;   // ushort: the "+N" after the weapon name
        internal const int Attack    = 0x04;   // ushort
        internal const int Endurance = 0x06;   // ushort
        internal const int Speed     = 0x08;   // ushort
        internal const int Magic     = 0x0A;   // ushort
        internal const int WhpMax    = 0x0C;   // ushort
        internal const int Whp       = 0x10;   // float
        internal const int Xp        = 0x14;   // ushort: ABS points

        // ── Elements ──
        internal const int ElementHud = 0x16;  // byte: 00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy, 05 None
        internal const int Fire       = 0x17;  // byte
        internal const int Ice        = 0x18;
        internal const int Thunder    = 0x19;
        internal const int Wind       = 0x1A;
        internal const int Holy       = 0x1B;

        // ── Anti-category (slayer) bytes, EnemyCategory order ──
        internal const int AntiDragon = 0x1C;
        internal const int AntiUndead = 0x1D;
        internal const int AntiMarine = 0x1E;
        internal const int AntiRock   = 0x1F;
        internal const int AntiPlant  = 0x20;
        internal const int AntiBeast  = 0x21;
        internal const int AntiSky    = 0x22;
        internal const int AntiMetal  = 0x23;
        internal const int AntiMimic  = 0x24;
        internal const int AntiMage   = 0x25;

        // ── Attachment slot 1 (the first ATTACH_LIST entry at +0x28; entries are 0x20 apart) ──
        internal const int Slot1ItemId               = 0x28;   // ushort: the socketed item
        internal const int Slot1SynthesisedItemId    = 0x2A;   // ushort: synth spheres only — names the sphere in the description / icon
        internal const int Slot1Special1             = 0x2C;   // byte
        internal const int Slot1Special2             = 0x2D;   // byte
        internal const int Slot1SynthesisedItemLevel = 0x2E;   // ushort: synth spheres only — the "+X" after the name
        internal const int Slot1Attack               = 0x30;   // ushort
        internal const int Slot1Endurance            = 0x32;
        internal const int Slot1Speed                = 0x34;
        internal const int Slot1Magic                = 0x36;
        internal const int Slot1Fire                 = 0x38;   // byte
        internal const int Slot1Ice                  = 0x39;
        internal const int Slot1Thunder              = 0x3A;
        internal const int Slot1Wind                 = 0x3B;
        internal const int Slot1Holy                 = 0x3C;
        internal const int Slot1Dragon               = 0x3D;
        internal const int Slot1Undead               = 0x3E;
        internal const int Slot1Sea                  = 0x3F;
        internal const int Slot1Rock                 = 0x40;
        internal const int Slot1Plant                = 0x41;
        internal const int Slot1Beast                = 0x42;
        internal const int Slot1Sky                  = 0x43;
        internal const int Slot1Metal                = 0x44;
        internal const int Slot1Mimic                = 0x45;
        internal const int Slot1Mage                 = 0x46;

        // ── Mod bookkeeping (words the synth-sphere listener in WeaponSynthSphereLevel keeps inside the record) ──
        internal const int HasChangedBySynth      = 0xC8;   // ushort: flag for WeaponSynthSphereLevel.Listen
        internal const int WeaponFormerStatsValue = 0xCA;   // ushort: the level delta that listener last applied

        // ── Ability bitfields ──
        internal const int Special1 = 0xEE;    // byte: 01 unknown (default on Chronicle 2), 02 Big Bucks, 04 Poor, 08 Quench, 16 Thirst, 32 Poison, 64 Stop, 128 Steal
        internal const int Special2 = 0xEF;    // byte: 02 Durable, 04 Drain, 08 Heal, 16 Critical, 32 Abs Up

        /// <summary>Absolute address of <paramref name="field"/> in <paramref name="character"/>'s bag slot
        /// <paramref name="slot"/> (0-9), via <see cref="DngStatusData.WeaponRecord"/>.</summary>
        internal static long Address(int character, int slot, int field)
            => DngStatusData.WeaponRecord(character, slot) + field;
    }
}
