using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Monster sounds wherever the randomizer places a species: tools/stubs/monster_bank.s builds a floor's monster bank from the
    /// sounds of the species on it (MonsterSoundBake's library) on the floors whose bit is set in CodeCaves.MonsterBankFloors. This
    /// marks the floors whose roster the mod rewrites (the randomizer's staged floors, the Sandbox roster's), clears them as they
    /// revert, and logs each bank the cave builds.</summary>
    internal static class MonsterSounds
    {
        private const string Tag = "[MonsterSounds] ";
        private const int Floors = 128;
        private static readonly byte[] _floors = new byte[Floors / 8];
        private static uint _banks, _port;

        /// <summary>The floor's bank is built from its species (EnemyRandomizer staged it).</summary>
        internal static void MarkFloor(int floor) => Set(floor, true);

        /// <summary>The floor is vanilla again.</summary>
        internal static void ClearFloor(int floor) => Set(floor, false);

        /// <summary>No floor is (the dungeon's staged floors reverted).</summary>
        internal static void ClearAll()
        {
            Array.Clear(_floors, 0, _floors.Length);
            Memory.WriteBytesBatch(CodeCaves.MonsterBankFloors, _floors);
        }

        private static void Set(int floor, bool on)
        {
            if (floor < 0 || floor >= Floors) return;
            byte bit = (byte)(1 << (floor & 7));
            _floors[floor >> 3] = on ? (byte)(_floors[floor >> 3] | bit) : (byte)(_floors[floor >> 3] & ~bit);
            Memory.WriteBytesBatch(CodeCaves.MonsterBankFloors, _floors);
        }

        /// <summary>Once a dungeon tick: a bank the cave built since the last tick, in the log.</summary>
        internal static void Tick()
        {
            uint port = Memory.ReadUInt(SoundDriver.MidiState + 3 * SoundDriver.PortStride);   // the monster port's bank header
            if (port != _port)
            {
                _port = port;
                bool ours = port != 0 && port == Memory.ReadUInt(CodeCaves.MonsterBankStats + 0x10);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"monster port now holds bank 0x{port:X} ({(ours ? "the floor's built bank" : "not a built bank")}); " +
                    $"sound set {Memory.ReadInt(SoundDriver.NowSoundSet)}, loading {Memory.ReadInt(SoundDriver.LoadSoundSet)}");
            }
            uint banks = Memory.ReadUInt(CodeCaves.MonsterBankStats + 0x0C);
            if (banks == _banks) return;
            _banks = banks;
            if (banks == 0) return;
            uint bytes = Memory.ReadUInt(CodeCaves.MonsterBankStats), taken = Memory.ReadUInt(CodeCaves.MonsterBankStats + 4),
                 left = Memory.ReadUInt(CodeCaves.MonsterBankStats + 8);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"floor's monster bank: {taken} sound unit(s), {bytes:N0} B" +
                (left > 0 ? $"; {left} left out (over the cap) — those species stay silent" : ""));
            uint hd = Memory.ReadUInt(SoundDriver.Bank), staged = Memory.ReadUInt(SoundDriver.Bank + 4), size = Memory.ReadUInt(SoundDriver.Bank + 8),
                 spu = Memory.ReadUInt(SoundDriver.Bank + 0xC);
            uint Port(int p, int field) => Memory.ReadUInt(SoundDriver.MidiState + p * SoundDriver.PortStride + field);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"driver: gBank hd 0x{hd:X} staged 0x{staged:X} {size:N0} B " +
                (size == bytes ? "(this bank)" : "(NOT this bank: the staging buffer was not allocated)") +
                $" spu 0x{spu:X}; monster port hd 0x{Port(3, 0):X} spu 0x{Port(3, 4):X}; E spu 0x{Port(2, 4):X}, I spu 0x{Port(4, 4):X}");
        }
    }
}
