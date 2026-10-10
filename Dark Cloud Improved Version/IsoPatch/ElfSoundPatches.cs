using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Monster sounds and Steve's lines wherever the randomizer places a species: tools/stubs/monster_bank.s (in the dead
    /// soft-float routines) builds each randomized floor's monster bank and Steve lines from the library MonsterSoundBake writes,
    /// with its helpers in tools/stubs/monster_bank_io.s (the dead CD streaming routines); the dun hook is DunPatches'.</summary>
    internal static class ElfSoundPatches
    {
        internal static void PatchMonsterBank(FileStream fs, Func<uint, long> ElfOff)
        {
            foreach (var (host, vanilla, name) in new[] { (DeadFloatCave.Host, DeadFloatCave.VanillaWord0, "the soft-float routines"),
                                                          (DeadStreamCave.Host, DeadStreamCave.VanillaWord0, "sceCdStInit") })
            {
                ReplaceWord(fs, ElfOff, host, vanilla, 0x03E00008u, $"{name}' first word");   // jr ra
                WrU32(fs, ElfOff(host + 4), 0x24020000u);                                       //   li v0,0
            }
            foreach (var (bin, check, at, end, host) in new (string, Func<uint, bool>, uint, uint, string)[] {
                ("monsterBank.bin",   w => w == 0x6E756F73u, DeadFloatCave.MonsterBank, DeadFloatCave.End, "soft-float"),           // "soun", the library's path
                ("monsterBankIo.bin", w => w >> 26 == 2,     DeadStreamCave.MonsterBankIo, DeadStreamCave.End, "CD streaming") })   // its jump table's first `j`
            {
                byte[] stub = Embedded(bin);
                if (stub.Length == 0 || (stub.Length & 3) != 0 || !check(U32(stub, 0))) throw new IOException($"{bin} malformed ({stub.Length} B) or stale — reassemble its .s.");
                WriteBytes(fs, ElfOff, at, stub, end, $"{bin} overruns the {host} routines' bodies");
            }
        }
    }
}
