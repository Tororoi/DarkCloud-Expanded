namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// SHARED data-only sound-effect trigger for the weapon-ability family — the long-sought
    /// "data-writable SE path" (RE'd 2026-09-07 for Guardian Grace's heal chime).
    ///
    /// The engine keeps a 32-slot SE sequencer pool `se_seq` @0x1CFB530 (8 B/slot) that
    /// SndSeSeqStep (0x15AFA0, called from the global per-frame SndStep) iterates: a slot whose
    /// counter is 0 fires SndSePlay(id, -1, channel); when counter reaches duration it fires
    /// SndSeStop and frees the slot (id = -1). So fabricating a slot is a pure data write and THE
    /// ENGINE plays the sound on its next frame — the trick-the-game pattern, no native code.
    ///
    /// Slot layout (shorts): +0 id (-1 = free), +2 duration frames, +4 counter, +6 channel.
    /// The id is written LAST so the stepper never sees a half-built slot (id &lt; 0 hides it).
    /// The slot self-frees after `duration` frames; for one-shot SEs pick a duration past the
    /// sample's end so the stop is a no-op. Only ids resident in the current sound bank play
    /// (SndSePlay resolves via GetSeInfo and silently drops unknown ids).
    /// </summary>
    internal static class SeSeq
    {
        private const long Pool   = 0x21CFB530;   // se_seq (main .bss 0x1CFB530)
        private const int  Slots  = 32, Stride = 8;

        /// <summary>Healing-spring chime — played by HealingWater (0x1AF980) the instant it spawns
        /// the CHealEffect sparkle. Resident in dungeon banks (springs are dungeon features).</summary>
        internal const ushort HealChime = 0x1B8;

        /// <summary>Character-change jingle — played by the dungeon state-0x122 (materialize) handler
        /// (dun 0x1DB6940) alongside the golden NewChangeFx burst and a speed-90 white flash.</summary>
        internal const ushort ChangeJingle = 0xF;

        /// <summary>Queue a sound effect through the engine's SE sequencer pool. Returns false if
        /// every slot is busy (vanishingly rare — vanilla barely uses the pool) — then no sound,
        /// never a glitch. durationFrames only times the SndSeStop; give one-shots headroom.</summary>
        internal static bool Play(ushort seId, ushort durationFrames, ushort channel = 0)
        {
            for (int i = 0; i < Slots; i++)
            {
                long slot = Pool + i * Stride;
                if (Memory.ReadUShort(slot) != 0xFFFF) continue;   // free slots hold id -1 (boot-time zeros read busy)
                Memory.WriteUShort(slot + 2, durationFrames);
                Memory.WriteUShort(slot + 4, 0);                   // counter 0 -> stepper fires the play next frame
                Memory.WriteUShort(slot + 6, channel);
                Memory.WriteUShort(slot, seId);                    // id last: the slot goes live atomically
                return true;
            }
            return false;
        }
    }
}
