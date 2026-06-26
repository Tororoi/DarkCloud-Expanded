namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Static per-weapon base-stat table in the ELF (SCUS_971.11), symbol <c>WeaponList</c>.
    /// CONFIRMED. 120 records (one per weapon item ID 257-376), stride 0x4C.
    ///
    /// INDEXING: index = (itemId - 257) == the weapon's ComItemInfo ClassIndex (+0x2). The engine
    /// resolves it via GetItemTypeInfo (type must be 2 = ItemClass.Weapon) then WeaponList[index].
    /// Readers: GetWeaponData__Fi (ELF 0x1D0F50), GetWeaponDataInfo__Fi (ELF 0x1D0D90).
    ///
    /// This backs the per-Dagger absolute addresses in Weapons.cs (its "Base database table Dagger
    /// addresses" are WeaponList[1] = the entry for item 258 at EE 0x2027A70C). The field
    /// offsets below are those names made relative to the entry base. Stat fields are signed shorts
    /// unless noted; the Dagger entry is also used at runtime (see Weapons.cs "(ALSO RUNTIME)").
    ///
    /// <see cref="ItemData.ChestPools"/> ranks weapon rarity by power = <see cref="MaxAttack"/> +
    /// <see cref="MaxMagic"/>/3 (+0x44/+0x46): stronger = rarer (Inferno tops the stats, but
    /// Chronicle 2 is pinned as the single rarest there by design).
    /// </summary>
    internal static class WeaponList
    {
        internal const int NativeBase   = 0x0027A6C0;
        internal const int Base         = 0x2027A6C0; // PCSX2 EE (native + 0x20000000)
        internal const int Stride       = 0x4C;       // 76 bytes per weapon
        internal const int FirstItemId  = 257;        // index 0 = item 257 (Dagger broken)
        internal const int Count        = 120;        // item IDs 257-376

        // ── Field offsets within a weapon entry (from Weapons.cs Dagger map) ──
        internal const int Whp          = 0x00; // short — base weapon health points
        internal const int Attack       = 0x02; // short - base attack (ChestPools power metric)
        internal const int Endurance    = 0x04; // short — base endurance (durability)
        internal const int Speed        = 0x06; // short — base speed
        internal const int Magic        = 0x08; // short — base magic
        // Ownership: 0=Toan 1=Xiao 2=Goro 3=Ruby 4=Ungaga 5=Osmond
        internal const int Ownership    = 0x0A; // byte - owning character
        internal const int Synth1       = 0x0B; // byte  — synth slot 1 (0=none,1=gray,2=blue)
        internal const int Synth2       = 0x0C; // byte
        internal const int Synth3       = 0x0D; // byte
        internal const int Synth4       = 0x0E; // byte
        internal const int Synth5       = 0x0F; // byte
        internal const int Synth6       = 0x10; // byte  — synth slot 6
        // Elemental attack stats (short each)
        internal const int Fire         = 0x12;
        internal const int Ice          = 0x14;
        internal const int Thunder      = 0x16;
        internal const int Wind         = 0x18;
        internal const int Holy         = 0x1A;
        // Anti-/slayer attack stats (short each)
        internal const int DinoSlayer   = 0x1C;
        internal const int UndeadBuster = 0x1E;
        internal const int SeaKiller    = 0x20;
        internal const int StoneBreaker = 0x22;
        internal const int PlantBuster  = 0x24;
        internal const int BeastBuster  = 0x26;
        internal const int SkyHunter    = 0x28;
        internal const int MetalBreaker = 0x2A;
        internal const int MimicBreaker = 0x2C;
        internal const int MageSlayer   = 0x2E;
        internal const int Abs          = 0x30; // short - base absorption (ABS) points
        internal const int AbsAdd       = 0x32; // short - ABS added per weapon level
        // Effect1 bits: 2=BigBucks 4=Poor 8=Quench 16=Thirst 32=Poison 64=Stop 128=Steal
        internal const int Effect1      = 0x38; // byte - special effects set 1
        // Effect2 bits: 1=Fragile 2=Durable 4=Drain 8=Heal 16=Critical 32=ABSUp
        internal const int Effect2      = 0x39; // byte - special effects set 2
        internal const int BuildUp      = 0x3C; // build-up branch data
        internal const int MaxAttack    = 0x44; // short — max attack
        internal const int MaxMagic     = 0x46; // short — max magic

        /// <summary>EE RAM base address of the entry for <paramref name="itemId"/> (257-376),
        /// or -1 if out of range.</summary>
        internal static int EntryAddr(int itemId) =>
            itemId >= FirstItemId && itemId < FirstItemId + Count
                ? Base + (itemId - FirstItemId) * Stride : -1;

        /// <summary>EE RAM address of <paramref name="fieldOffset"/> within the entry for
        /// <paramref name="itemId"/>, or -1 if the ID is out of range.</summary>
        internal static int FieldAddr(int itemId, int fieldOffset)
        {
            int e = EntryAddr(itemId);
            return e < 0 ? -1 : e + fieldOffset;
        }
    }

    /// <summary>
    /// Weapon element attribute table (ELF symbol referenced by <c>GetWeaponElementAttr__Fi</c>,
    /// ELF 0x1B69F0). 6 int entries @native 0x0027B1B0 indexed by element 0-5 (clamped); returns a
    /// per-element attribute value. Separate from <see cref="WeaponList"/> (not per-weapon).
    /// Recorded for completeness; not used by the Chest Randomizer.
    /// </summary>
    internal static class WeaponElementAttr
    {
        internal const int NativeBase = 0x0027B1B0;
        internal const int Base       = 0x2027B1B0;
        internal const int Stride     = 4;
        internal const int Count      = 6; // element 0-5
    }
}
