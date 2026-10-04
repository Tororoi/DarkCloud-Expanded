namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The player's own globals: money, bag sizes, map items, position, the active character and the camera /
    /// pause words the <see cref="Player"/> mode checks read. The per-character stat arrays that
    /// <see cref="Character"/> indexes live here too (bases + strides), so every character address has one law
    /// instead of six hand-copied constants.
    ///
    /// Addresses are PCSX2 MMU addresses (native | 0x20000000). Character ids (<see cref="Player.ToanId"/> ..
    /// <see cref="Player.OsmondId"/>) stay on <see cref="Player"/>; they are not addresses.
    /// </summary>
    internal static class PlayerAddresses
    {
        // ── Money and bag ──
        internal const int Gilda                    = 0x21CDD892;   // ushort
        internal const int InventoryCurrentSize     = 0x21CDD8AD;   // byte: items currently in the bag
        internal const int InventoryTotalSize       = 0x21CDD8AC;   // byte: bag capacity
        internal const int InventorySizeWeapons     = 65;           // weapon list span incl. the 5 gaps between characters
        internal const int InventorySizeAttachments = 40;           // attachment bag slots (+2 yellow slots when scanned)

        // ── Map items (dungeon) ──
        internal const int MagicCrystal = 0x202A35A0;
        internal const int Map          = 0x202A359C;
        internal const int MiniMap      = 0x202A35B0;
        internal const int Visibility   = 0x202A359C;

        // ── Last damage the player dealt (character swing or throwable) ──
        internal const int MostRecentDamage = 0x21DC452C;   // int: most recent damage caused by the player
        internal const int DamageSource     = 0x21DC4530;   // int: character id, or -1 for a throwable

        // ── Position ──
        internal const int PositionX    = 0x21D331D8;   // town
        internal const int PositionY    = 0x21D331D0;
        internal const int PositionZ    = 0x21D331D4;
        internal const int DunPositionX = 0x21EA1D30;   // dungeon position vector (what _GET_POSITION(-2) copies): X @+0
        internal const int DunPositionY = 0x21EA1D38;   //   planar Y @+8
        internal const int DunPositionZ = 0x21EA1D34;   //   height @+4

        // ── Active character and its animation ──
        internal const int CurrentCharacter = 0x21CD9550;   // byte: 0 = Toan .. 5 = Osmond
        internal const int AnimationId      = 0x21DC448C;   // byte: ToanKey_Play state (14 = charging an attack)
        internal const int GodMode          = 0x21D564B0;   // byte: the debug "Ultraman" invincibility (0 = off, 2 = on)

        // ── Mode words the Player.Check* methods read ──
        internal const int DunCameraPerspective = 0x202A35EC;   // ushort: 0 = Normal, 10 = FPS, 155 = Static, 121/131 = chest, 400/401 = interacting
        internal const int FloorLoadedFlag      = 0x21CD954F;   // byte: 255 in town AND on the dungeon-select screen, else a floor is loaded
        internal const int DunPauseTitle        = 0x202A35C4;   // ushort: 1 while the PAUSE title shows
        internal const int DunPausePlayerState  = 0x202A3564;   // ushort: 1 while the player is pause-frozen
        internal const int DunPauseEnemyState   = 0x202A34DC;   // ushort: 1 while enemies are pause-frozen

        // ── Per-character stat arrays, indexed by character id (0 = Toan .. 5 = Osmond) ──
        // Each is base + id * stride (checked against every character's previously hand-written address).
        // All sit inside the CDngStatusData block (DngStatusData.Base = 0x21CD954C).
        internal const int HpArray          = 0x21CD955E;   // ushort[6], stride 2   (status block +0x12)
        internal const int MaxHpArray       = 0x21CD9552;   // ushort[6], stride 2   (status block +0x06)
        internal const int HpStride         = 2;
        internal const int DefenseArray     = 0x21CDD894;   // int[6],    stride 4   (status block +0x4348)
        internal const int ThirstArray      = 0x21CDD850;   // float[6],  stride 4   (status block +0x4304)
        internal const int ThirstMaxArray   = 0x21CDD838;   // float[6],  stride 4   (status block +0x42EC)
        internal const int StatusArray      = 0x21CDD814;   // ushort read, stride 4 (status block +0x42C8): 02 Near Death, 04 Freeze, 08 Stamina, 16 Poison, 32 Curse, 64 Goo
        internal const int StatusTimerArray = 0x21CDD824;   // ushort read, stride 4 (status block +0x42D8): frames left on the status
        internal const int WordStride       = 4;
        // NOTE: StatusArray + 4*id for Ungaga/Osmond lands on StatusTimerArray's Toan/Xiao words, and Osmond's status
        // timer lands on Toan's ThirstMaxArray word. These are the addresses the six character classes always used
        // (kept byte-for-byte); the overlap is flagged, not resolved.
    }
}
