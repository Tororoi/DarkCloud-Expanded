namespace Dark_Cloud_Improved_Version
{
    /// <summary>se_info (ELF .data, 2801 × 6 B: program, note, -, port, vol_no): the (program, note) and MIDI port of each sound id
    /// outside the 100–499 table ranges (GetSeInfo); SndInitSeTable fills vol_no from setbl.txt at boot. Guest addresses.</summary>
    internal static class SeInfo
    {
        internal const uint Table = 0x0025DFB0, RowBytes = 6;
        /// <summary>Id 600, unused in vanilla (program −1, port −1): the life sphere's shatter (MonsterSoundBake.ShatterUnit) on the
        /// monster port — played by the Crystal Gemron's death.</summary>
        internal const int Shatter = 600;
    }
}
