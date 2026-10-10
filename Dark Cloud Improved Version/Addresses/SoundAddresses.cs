namespace Dark_Cloud_Improved_Version
{
    /// <summary>The sound driver's EE-side state (sound.cpp, main BSS). MMU addresses (guest + 0x20000000).</summary>
    internal static class SoundDriver
    {
        /// <summary>gBank (MIDI_BANK, 0x40 B): the bank a load hands the driver — +0 header's IOP address, +4 the body's IOP staging
        /// buffer, +8 the body's bytes, +0xC its sound-memory address. TransHdBd leaves it as it was when the staging buffer cannot
        /// be allocated (and the load binds the previous header).</summary>
        internal const long Bank = 0x21CE8200;
        /// <summary>midi_state (8 ports × 0x80 B): +0 the port's bank header (IOP), +4 its body's sound-memory address. Port 2 = the
        /// E bank, 3 = the monster bank (MIDI channel 10), 4 = the I bank.</summary>
        internal const long MidiState = 0x21CE8240, PortStride = 0x80;
        /// <summary>now_sound_set: the sound set loaded last (snd{n}.snd); load_snd_set: one loading in the background, −1 = none.</summary>
        internal const long NowSoundSet = 0x202A25EC, LoadSoundSet = 0x202A2630;
    }
}
