namespace Dark_Cloud_Improved_Version
{
    /// <summary>The player-facing toggles on the Options tab.</summary>
    internal enum ModOption
    {
        // Graphics byte
        Graphics,           // bit0 graphical improvements  (Mailbox.Option4)
        Fov,                // bit1 FOV / widescreen         (Mailbox.Option3)
        // Audio byte
        WeaponBeeps,        // bit0                          (Mailbox.Option1)
        BattleMusic,        // bit1                          (Mailbox.Option2)
        AttackSounds,       // bit2                          (the attack-sound bytes)
        MuteMusic,          // bit3                          (the music-volume word)
        // Gameplay byte
        FasterEnemies,      // bit0  FasterEnemies.Enabled
        StrongerEnemies,    // bit1  EnemyStatNormalizer.StrongerEnemies
        RandomizedEnemies,  // bit2  EnemyRandomizer.RandomizeEnemies
        HarderAi,           // bit3  HarderEnemyAI.Enabled
    }

    /// <summary>
    /// What each Options-tab toggle does in the game, and where it is remembered. <see cref="Apply"/> performs the
    /// toggle's effect (a mailbox flag, a code byte, a static switch); <see cref="Set"/> also persists it; <see cref="Load"/>
    /// reads every persisted toggle back so the window can restore the boxes and re-apply the effects on a loaded save.
    ///
    /// ── Persisted options, bit-packed into three category-grouped save bytes (all-zero = everything off) ──
    ///   Graphics 0x21CE4490 : bit0 graphical improvements, bit1 FOV          (bits 2-7 free)
    ///   Audio    0x21CE4491 : bit0 weapon beeps, bit1 battle music,
    ///                         bit2 attack sounds, bit3 mute music            (bits 4-7 free)
    ///   Gameplay 0x21CE4492 : bit0 faster enemies, bit1 stronger enemies, bit2 randomized enemies, bit3 harder AI (bits 4-7 free)
    ///
    /// FREE SAVE BYTES for new options (no need to re-derive):
    ///   • 0x21CE4493, 0x21CE4494, 0x21CE4495 are fully UNUSED proven-free bytes — grab one for a new
    ///     category, or pack into the spare upper bits of the three bytes above.
    ///   • The proven-free padding block is exactly 0x21CE4490–0x21CE4495 (zero on every save, mod-only).
    ///   • DO NOT use 0x21CE4496 or later: those are LIVE game save data (old saves hold non-zero there;
    ///     a read-breakpoint fires only via the save memcpy, and writing them risks corruption / wrong
    ///     defaults). Verified by comparing a fresh new-game save vs. an existing save.
    ///
    /// Persistence mechanism: each toggle read-modify-writes its bit here (these bytes live in the save-
    /// data region, so they ride along to the memory card); <see cref="Load"/> reads them back on load.
    /// </summary>
    internal static class ModOptions
    {
        internal const int OptGraphicsByte = 0x21CE4490;
        internal const int OptAudioByte    = 0x21CE4491;
        internal const int OptGameplayByte = 0x21CE4492;

        /// <summary>The attack-sound ids in the swing code (one byte each); zeroed = the attack plays no sound.
        /// <see cref="AttackSoundValues"/> holds the vanilla id of each.</summary>
        internal static readonly int[] AttackSoundAddresses = { 0x20265DBC, 0x20265DC2, 0x20265DC8, 0x20265DCE, 0x20265F0C, 0x20265F12, 0x2026605C, 0x20266062, 0x202661AC, 0x202661B8, 0x202662FC, 0x20266302, 0x20266308, 0x2026644C };
        internal static readonly byte[] AttackSoundValues = { 68, 69, 70, 71, 83, 84, 98, 99, 113, 115, 128, 129, 130, 156 };

        /// <summary>The music-volume halfword: 0 = muted, <see cref="MusicVolumeVanilla"/> = the game's own.</summary>
        internal const int MusicVolumeWord = 0x20299F53;
        internal const ushort MusicVolumeVanilla = 25637;

        /// <summary>The persisted state of every toggle, read once (three bytes).</summary>
        internal readonly struct State
        {
            private readonly byte _gfx, _aud, _play;
            internal State(byte gfx, byte aud, byte play) { _gfx = gfx; _aud = aud; _play = play; }

            internal bool this[ModOption option]
            {
                get
                {
                    var (addr, mask) = Slot(option);
                    byte b = addr == OptGraphicsByte ? _gfx : addr == OptAudioByte ? _aud : _play;
                    return (b & mask) != 0;
                }
            }
        }

        /// <summary>Every persisted toggle, from one read of each category byte.</summary>
        internal static State Load()
            => new State(Memory.ReadByte(OptGraphicsByte), Memory.ReadByte(OptAudioByte), Memory.ReadByte(OptGameplayByte));

        /// <summary>Perform the toggle's effect in the game without touching its persisted bit (used when restoring a loaded save).</summary>
        internal static void Apply(ModOption option, bool on)
        {
            switch (option)
            {
                case ModOption.Graphics:    Memory.WriteByte(Mailbox.Option4, (byte)(on ? 1 : 0)); break;
                case ModOption.Fov:         Memory.WriteByte(Mailbox.Option3, (byte)(on ? 1 : 0)); break;
                case ModOption.WeaponBeeps: Memory.WriteByte(Mailbox.Option1, (byte)(on ? 1 : 0)); break;
                case ModOption.BattleMusic: Memory.WriteByte(Mailbox.Option2, (byte)(on ? 1 : 0)); break;
                case ModOption.AttackSounds:
                    for (int c = 0; c < AttackSoundAddresses.Length && c < AttackSoundValues.Length; c++)
                        Memory.WriteByte(AttackSoundAddresses[c], (byte)(on ? 0 : AttackSoundValues[c]));
                    break;
                case ModOption.MuteMusic:   Memory.WriteUShort(MusicVolumeWord, (ushort)(on ? 0 : MusicVolumeVanilla)); break;
                case ModOption.FasterEnemies:     FasterEnemies.Enabled = on; break;
                case ModOption.StrongerEnemies:   EnemyStatNormalizer.StrongerEnemies = on; break;
                case ModOption.RandomizedEnemies: EnemyRandomizer.RandomizeEnemies = on; break;
                case ModOption.HarderAi:          HarderEnemyAI.Enabled = on; break;
            }
        }

        /// <summary>Perform the toggle's effect and persist it (the Options-tab click).</summary>
        internal static void Set(ModOption option, bool on)
        {
            Apply(option, on);
            var (addr, mask) = Slot(option);
            WriteOptionBit(addr, mask, on);
        }

        /// <summary>The save byte and bit that remember <paramref name="option"/>.</summary>
        private static (int Addr, int Mask) Slot(ModOption option) => option switch
        {
            ModOption.Graphics          => (OptGraphicsByte, 0x01),
            ModOption.Fov               => (OptGraphicsByte, 0x02),
            ModOption.WeaponBeeps       => (OptAudioByte,    0x01),
            ModOption.BattleMusic       => (OptAudioByte,    0x02),
            ModOption.AttackSounds      => (OptAudioByte,    0x04),
            ModOption.MuteMusic         => (OptAudioByte,    0x08),
            ModOption.FasterEnemies     => (OptGameplayByte, 0x01),
            ModOption.StrongerEnemies   => (OptGameplayByte, 0x02),
            ModOption.RandomizedEnemies => (OptGameplayByte, 0x04),
            _                           => (OptGameplayByte, 0x08),   // HarderAi
        };

        // Read-modify-write a single option bit, preserving the other toggles packed into that byte.
        private static void WriteOptionBit(int addr, int mask, bool on)
        {
            byte v = Memory.ReadByte(addr);
            v = (byte)(on ? (v | mask) : (v & ~mask));
            Memory.WriteByte(addr, v);
        }
    }
}
