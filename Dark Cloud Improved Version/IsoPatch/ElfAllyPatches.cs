using System;
using System.IO;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A summoned ally monster run by the game's own monster code (the Pocklekul's pockle): tools/stubs/ally_manager.s in the dead
    /// sceMcSetFileInfo / sceMcRename bodies (<see cref="DeadMcCave"/>), which DunPatches hooks at the dungeon's three calls on the floor's
    /// monster manager — each then made again on a private manager (CodeCaves.AllyManager) with NowMonstorUnit pointing at it — plus a
    /// native-call entry that makes a call (CodeCaves.AllyCallFunc) the same way.</summary>
    internal static class ElfAllyPatches
    {
        private const uint StepFn = 0x001DD540, DrawFn = 0x001D8CD0, ShadowFn = 0x001D9800;   // the floor manager's Step / DrawMonstor / DrawShadowMonstor
        internal static void PatchAllyManager(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("allyManager.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, (int)(DeadMcCave.AllyCallPriv - DeadMcCave.Host)) != 0x27BDFFE0u)
                throw new IOException($"allyManager.bin malformed ({stub.Length} B) or its entries moved — reassemble ally_manager.s and check DeadMcCave.");
            // each entry's `lui t9 / ori t9` must name its function exactly (a wrong upper half jumps into the middle of other code)
            foreach (var (entry, fn) in new[] { (DeadMcCave.AllyStep, StepFn), (DeadMcCave.AllyDraw, DrawFn), (DeadMcCave.AllyShadow, ShadowFn) })
            {
                int o = (int)(entry - DeadMcCave.Host);
                uint lui = U32(stub, o), ori = U32(stub, o + 4);
                if ((lui & 0xFFFF0000u) != 0x3C190000u || (ori & 0xFFFF0000u) != 0x37390000u || (((lui & 0xFFFFu) << 16) | (ori & 0xFFFFu)) != fn)
                    throw new IOException($"allyManager.bin's entry at 0x{entry:X} does not call 0x{fn:X} — reassemble ally_manager.s.");
            }
            uint word0 = RdU32(fs, ElfOff(DeadMcCave.Host));
            if (word0 != DeadMcCave.VanillaWord0 && word0 != U32(stub, 0))
                throw new IOException($"sceMcSetFileInfo @0x{DeadMcCave.Host:X} is not vanilla ({word0:X8}).");
            WriteBytes(fs, ElfOff, DeadMcCave.Host, stub, DeadMcCave.End, "allyManager.bin overruns the sceMcSetFileInfo / sceMcRename bodies");
        }
    }
}
