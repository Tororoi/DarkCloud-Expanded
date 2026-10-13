using System;
using System.IO;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A summoned ally monster run by the game's own monster code (the Pocklekul's pockle), all of it native:
    /// tools/stubs/ally_manager.s in the dead sceMcSetFileInfo / sceMcRename bodies (<see cref="DeadMcCave"/>), which DunPatches hooks at the
    /// dungeon's three calls on the floor's monster manager — each then made again on a private manager (CodeCaves.AllyManager) with
    /// NowMonstorUnit pointing at it, the brain run after the floor's Step; tools/stubs/ally_brain.s (<see cref="DeadDmaCave"/>, its summon in
    /// <see cref="DeadPadPressCave"/>) — the guard charge, the summon, the target, the end; tools/stubs/ally_follow.s
    /// (<see cref="DeadSifRpcCave"/>, its turn after ally_manager.s) — the walk beside Ruby; tools/stubs/ally_despawn.s
    /// (<see cref="DeadSifDataCave"/>) — the end; and tools/stubs/ally_collide.s in the dead sceSifLoadModuleBuffer body
    /// (<see cref="DeadSifCave"/>) at Step's monster-vs-monster check, so the two managers' units block each other.</summary>
    internal static class ElfAllyPatches
    {
        private const uint StepFn = 0x001DD540, DrawFn = 0x001D8CD0, ShadowFn = 0x001D9800;   // the floor manager's Step / DrawMonstor / DrawShadowMonstor
        private const uint StepMoveCheckSite = 0x001DE344;   // Step's `jal MoveChecMonster` (a0 = the stepping manager)
        internal static void PatchAllyManager(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("allyManager.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || !ContainsWord(stub, MipsAsm.Jal(DeadDmaCave.AllyBrain)))
                throw new IOException($"allyManager.bin malformed ({stub.Length} B) or stale — reassemble ally_manager.s (it must call the brain).");
            // each entry's `lui t9 / ori t9` must name its function exactly (a wrong upper half jumps into the middle of other code)
            foreach (var (entry, fn) in new[] { (DeadMcCave.AllyStep, StepFn), (DeadMcCave.AllyDraw, DrawFn), (DeadMcCave.AllyShadow, ShadowFn) })
            {
                int o = (int)(entry - DeadMcCave.Host);
                uint lui = U32(stub, o), ori = U32(stub, o + 4);
                if ((lui & 0xFFFF0000u) != 0x3C190000u || (ori & 0xFFFF0000u) != 0x37390000u || (((lui & 0xFFFFu) << 16) | (ori & 0xFFFFu)) != fn)
                    throw new IOException($"allyManager.bin's entry at 0x{entry:X} does not call 0x{fn:X} — reassemble ally_manager.s.");
            }
            Claim(fs, ElfOff, "sceMcSetFileInfo", DeadMcCave.Host, DeadMcCave.VanillaWord0, stub, DeadMcCave.AllyTurn);

            byte[] brain = Embedded("allyBrain.bin"), summon = Embedded("allySummon.bin");
            Require(brain, "allyBrain.bin", "ally_brain.s", MipsAsm.Jal(DeadPadPressCave.AllySummon), MipsAsm.Jal(DeadSifRpcCave.AllyFollow), MipsAsm.Jal(DeadSifDataCave.AllyDespawn));
            Require(summon, "allySummon.bin", "ally_brain.s", MipsAsm.Jal(DeadSifDataCave.AllyDespawn));
            Claim(fs, ElfOff, "sceDmaSendN", DeadDmaCave.Host, DeadDmaCave.VanillaWord0, brain, DeadDmaCave.End);
            Claim(fs, ElfOff, "scePadInfoPressMode", DeadPadPressCave.Host, DeadPadPressCave.VanillaWord0, summon, DeadPadPressCave.End);

            byte[] follow = Embedded("allyFollow.bin"), turn = Embedded("allyTurn.bin");
            Require(follow, "allyFollow.bin", "ally_follow.s");
            Require(turn, "allyTurn.bin", "ally_follow.s");
            Claim(fs, ElfOff, "sceSifSetRpcQueue", DeadSifRpcCave.Host, DeadSifRpcCave.VanillaWord0, follow, DeadSifRpcCave.End);
            WriteBytes(fs, ElfOff, DeadMcCave.AllyTurn, turn, DeadMcCave.End, "allyTurn.bin overruns the sceMcRename body");

            byte[] despawn = Embedded("allyDespawn.bin");
            Require(despawn, "allyDespawn.bin", "ally_despawn.s");
            Claim(fs, ElfOff, "sceSifGetOtherData", DeadSifDataCave.Host, DeadSifDataCave.VanillaWord0, despawn, DeadSifDataCave.End);

            byte[] collide = Embedded("allyCollide.bin");
            if (collide.Length == 0 || (collide.Length & 3) != 0 || U32(collide, 0) != 0x27BDFFE0u || !ContainsWord(collide, MipsAsm.Jal(DebugIfCave.SpearBlock))
)
                throw new IOException($"allyCollide.bin malformed ({collide.Length} B) or stale — reassemble ally_collide.s (it must call the spear-block cave).");
            Claim(fs, ElfOff, "sceSifLoadModuleBuffer", DeadSifCave.Host, DeadSifCave.VanillaWord0, collide, DeadSifCave.End);
            // ElfWeaponPatches.PatchSpearBlock (earlier) points the site at its cave; this takes it over and calls that cave in turn.
            uint site = RdU32(fs, ElfOff(StepMoveCheckSite)), ours = MipsAsm.Jal(DeadSifCave.AllyCollide);
            if (site != MipsAsm.Jal(DebugIfCave.SpearBlock) && site != ours)
                throw new IOException($"CMonstorUnit::Step's site 0x{StepMoveCheckSite:X} is 0x{site:X8}, not the spear-block cave's jal — patch order changed?");
            WrU32(fs, ElfOff(StepMoveCheckSite), ours);
        }

        /// <summary>A stub well-formed (whole words) and carrying each of <paramref name="calls"/>.</summary>
        private static void Require(byte[] b, string bin, string src, params uint[] calls)
        {
            if (b.Length == 0 || (b.Length & 3) != 0)
                throw new IOException($"{bin} malformed ({b.Length} B) — reassemble {src}.");
            foreach (uint w in calls)
                if (!ContainsWord(b, w))
                    throw new IOException($"{bin} lacks 0x{w:X8} — its callees moved; reassemble {src} and check DeadFunctionCaveAddresses.");
        }

        /// <summary>A dead host's body replaced by <paramref name="b"/> (vanilla or ours already), within <paramref name="limit"/>.</summary>
        private static void Claim(FileStream fs, Func<uint, long> ElfOff, string name, uint host, uint vanillaWord0, byte[] b, uint limit)
        {
            uint word0 = RdU32(fs, ElfOff(host));
            if (word0 != vanillaWord0 && word0 != U32(b, 0))
                throw new IOException($"{name} @0x{host:X} is not vanilla ({word0:X8}).");
            WriteBytes(fs, ElfOff, host, b, limit, $"{name}'s cave overruns its host ({b.Length} B from 0x{host:X}, the host ends at 0x{limit:X})");
        }
    }
}
