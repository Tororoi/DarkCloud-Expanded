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
    /// (<see cref="DeadSifRpcCave"/>, its turn after ally_manager.s) — the walk beside Ruby; tools/stubs/ally_grow.s (after the turn) — its
    /// growth as it appears; tools/stubs/ally_despawn.s
    /// (<see cref="DeadSifDataCave"/>) — the end; and tools/stubs/ally_collide.s in the dead sceSifLoadModuleBuffer body
    /// (<see cref="DeadSifCave"/>) at Step's monster-vs-monster check, so the two managers' units block each other, and at the player's
    /// move (through the player-spear cave), so Ruby is blocked by it as by any monster.</summary>
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
            Require(brain, "allyBrain.bin", "ally_brain.s", MipsAsm.Jal(DeadPadPressCave.AllySummon), MipsAsm.Jal(DeadSifRpcCave.AllyFollow),
                    MipsAsm.Jal(DeadSifDataCave.AllyDespawn), MipsAsm.Jal(DeadMcCave.AllyGrow), MipsAsm.Jal(DeadSifRpcCave.AllyPark), MipsAsm.Jal(DeadSifRpcCave.AllyRelease),
                    MipsAsm.Jal(DeadDmaWatchCave.AllyAim), MipsAsm.Jal(DeadSifElfCave.AllyPinCopy));
            byte[] aim = Embedded("allyAim.bin");
            Require(aim, "allyAim.bin", "ally_aim.s", 0x3C0A0001u, 0x354AFDE0u);   // the pockle's position: lui t2, 1 / ori t2, 0xFDE0
            Claim(fs, ElfOff, "sceDmaWatch", DeadDmaWatchCave.Host, DeadDmaWatchCave.VanillaWord0, aim, DeadDmaWatchCave.End);
            Require(summon, "allySummon.bin", "ally_brain.s", MipsAsm.Jal(DeadSifDataCave.AllyDespawn), MipsAsm.Jal(DeadSifElfCave.AllyShotsBefore), MipsAsm.Jal(DeadSifElfCave.AllyShotsAfter));
            Claim(fs, ElfOff, "sceDmaSendN", DeadDmaCave.Host, DeadDmaCave.VanillaWord0, brain, DeadDmaCave.End);
            Claim(fs, ElfOff, "scePadInfoPressMode", DeadPadPressCave.Host, DeadPadPressCave.VanillaWord0, summon, DeadPadPressCave.End);

            byte[] follow = Embedded("allyFollow.bin"), turn = Embedded("allyTurn.bin");
            // its CCharacter is block + 0x1FDD0 and its render scale block + 0x1FE60: lui 1 / ori (ori is zero-extended, so lui 2 there
            // lands 0x10000 past — writing an unused unit's data while the pockle stood frozen and never grew)
            Require(follow, "allyFollow.bin", "ally_follow.s", 0x3C080001u, 0x3508FDD0u);
            foreach (uint entry in new[] { DeadSifRpcCave.AllyPark, DeadSifRpcCave.AllyRelease })   // each opens with `lui t0, 5`
                if (U32(follow, (int)(entry - DeadSifRpcCave.Host)) != 0x3C080005u)
                    throw new IOException($"allyFollow.bin has no park / release at 0x{entry:X} — reassemble ally_follow.s and check DeadSifRpcCave.");
            Require(turn, "allyTurn.bin", "ally_follow.s");
            Claim(fs, ElfOff, "sceSifSetRpcQueue", DeadSifRpcCave.Host, DeadSifRpcCave.VanillaWord0, follow, DeadSifRpcCave.End);
            WriteBytes(fs, ElfOff, DeadMcCave.AllyTurn, turn, DeadMcCave.End, "allyTurn.bin overruns the sceMcRename body");

            // ally_grow.s builds its table's address by hand (lui 0x0012 / addiu): it must name where the table landed
            byte[] grow = Embedded("allyGrow.bin");
            Require(grow, "allyGrow.bin", "ally_grow.s", 0x3C0D0001u, 0x35ADFE60u, 0x3C0B0012u, 0x256B0000u | (uint)((int)DeadMcCave.AllyGrowTable - 0x120000 & 0xFFFF));
            // park builds the YIELD records' address by hand (lui t1, 0x0012 / addiu t1): it must name where they landed
            Require(follow, "allyFollow.bin", "ally_follow.s", 0x3C090012u, 0x25290000u | (uint)((int)DeadMcCave.AllyYield - 0x120000 & 0xFFFF));
            if (grow.Length != (int)(DeadMcCave.AllyYield - DeadMcCave.AllyGrow) + 3 * 12 || U32(grow, (int)(DeadMcCave.AllyYield - DeadMcCave.AllyGrow)) != 23u)
                throw new IOException($"allyGrow.bin is {grow.Length} B — its table or YIELD records moved; reassemble ally_grow.s and check DeadMcCave.AllyGrowTable / AllyYield.");
            if (DeadMcCave.AllyTurn + (uint)turn.Length > DeadMcCave.AllyGrow)
                throw new IOException("allyTurn.bin runs into ally_grow.s — move DeadMcCave.AllyGrow.");
            WriteBytes(fs, ElfOff, DeadMcCave.AllyGrow, grow, DeadMcCave.End, "allyGrow.bin overruns the sceMcRename body");

            byte[] despawn = Embedded("allyDespawn.bin");
            Require(despawn, "allyDespawn.bin", "ally_despawn.s", MipsAsm.Jal(DeadSifElfCave.AllyShotsClear));
            byte[] shots = Embedded("allyShots.bin");
            Require(shots, "allyShots.bin", "ally_shots.s", MipsAsm.Jal(0x001AE440));   // Initialize__12CSHOT_EFFECTFv
            foreach (var (entry, word) in new[] { (DeadSifElfCave.AllyShotsBefore, 0x8F889DE8u), (DeadSifElfCave.AllyShotsAfter, 0x3C0E01FBu), (DeadSifElfCave.AllyShotsClear, 0x3C0E01FBu) })
                if (U32(shots, (int)(entry - DeadSifElfCave.Host)) != word)
                    throw new IOException($"allyShots.bin's entry at 0x{entry:X} moved — reassemble ally_shots.s and check DeadSifElfCave.");
            Claim(fs, ElfOff, "sceSifLoadElfPart", DeadSifElfCave.Host, DeadSifElfCave.VanillaWord0, shots, DeadSifElfCave.AllySwing);

            // CMonstorUnit::CheckDmg's `jal Set__14CCollisionData` (the unit's open attack sphere into the pool) → ally_swing.s: the
            // ally's goes in with mask 0, so it never hits the player.
            byte[] swing = Embedded("allySwing.bin");
            Require(swing, "allySwing.bin", "ally_swing.s", MipsAsm.J(SetCollisionData), MipsAsm.J(DeadSifCave.AllyCollideDone));   // blocked returns to ally_collide.s's exit
            WriteBytes(fs, ElfOff, DeadSifElfCave.AllySwing, swing, DeadSifElfCave.End, "allySwing.bin overruns the sceSifLoadElfPart bodies");
            uint cur = RdU32(fs, ElfOff(CheckDmgSwingSite)), toSwing = MipsAsm.Jal(DeadSifElfCave.AllySwing);
            if (cur != MipsAsm.Jal(SetCollisionData) && cur != toSwing)
                throw new IOException($"CheckDmg's attack-sphere call 0x{CheckDmgSwingSite:X} is 0x{cur:X8}, not vanilla — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(CheckDmgSwingSite), toSwing);
            // …and its incoming-hit mask test (the load moves into the hook's delay slot): the ally's unit also takes bit 8.
            if (U32(swing, (int)(DeadSifElfCave.AllyMask - DeadSifElfCave.AllySwing)) != 0x3C1901FBu)
                throw new IOException("allySwing.bin has no mask entry at DeadSifElfCave.AllyMask — reassemble ally_swing.s.");
            Hook(fs, ElfOff, CheckDmgMaskSite, LoadMask, MipsAsm.Jal(DeadSifElfCave.AllyMask));
            Hook(fs, ElfOff, CheckDmgMaskSite + 4, AndMaskPlayerHits, LoadMask);
            Claim(fs, ElfOff, "sceSifGetOtherData", DeadSifDataCave.Host, DeadSifDataCave.VanillaWord0, despawn, DeadSifDataCave.End);

            byte[] collide = Embedded("allyCollide.bin");
            if (collide.Length == 0 || (collide.Length & 3) != 0 || U32(collide, 0) != 0x27BDFFE0u || !ContainsWord(collide, MipsAsm.Jal(DebugIfCave.SpearBlock))
                || !ContainsWord(collide, MipsAsm.J(DeadSifElfCave.AllyBlocked)) || U32(collide, (int)(DeadSifCave.AllyCollideDone - DeadSifCave.Host)) != 0x8FBF0000u)
                throw new IOException($"allyCollide.bin malformed ({collide.Length} B) or stale — reassemble ally_collide.s (it must call the spear-block cave, stop through ally_swing.s's blocked, exit where DeadSifCave says).");
            Claim(fs, ElfOff, "sceSifLoadModuleBuffer", DeadSifCave.Host, DeadSifCave.VanillaWord0, collide, DeadSifCave.End);
            // ElfWeaponPatches.PatchSpearBlock (earlier) points the site at its cave; this takes it over and calls that cave in turn.
            uint site = RdU32(fs, ElfOff(StepMoveCheckSite)), ours = MipsAsm.Jal(DeadSifCave.AllyCollide);
            if (site != MipsAsm.Jal(DebugIfCave.SpearBlock) && site != ours)
                throw new IOException($"CMonstorUnit::Step's site 0x{StepMoveCheckSite:X} is 0x{site:X8}, not the spear-block cave's jal — patch order changed?");
            WrU32(fs, ElfOff(StepMoveCheckSite), ours);

            // The player's move: ElfWeaponPatches.PatchSpearBlock (earlier) wrote the player-spear cave, which makes the move's
            // MoveCheck call; that call now comes to AllyPlayerBlock, which makes it and then makes it again on the ally's manager.
            if (U32(collide, (int)(DeadSifCave.AllyPlayerBlock - DeadSifCave.Host)) != 0x27BDFFE0u)
                throw new IOException("allyCollide.bin has no player block at DeadSifCave.AllyPlayerBlock — reassemble ally_collide.s.");
            uint moveCheck = MipsAsm.Jal(PlayerMoveCheck), toAlly = MipsAsm.Jal(DeadSifCave.AllyPlayerBlock);
            int took = 0;
            for (uint a = DebugIfCave.PlayerSpearBlock; a < DebugIfCave.PlayerSpearBlock + PlayerSpearBlockSpan; a += 4)
            {
                uint w = RdU32(fs, ElfOff(a));
                if (w == moveCheck || w == toAlly) { WrU32(fs, ElfOff(a), toAlly); took++; }
            }
            if (took != 1)
                throw new IOException($"the player-spear cave at 0x{DebugIfCave.PlayerSpearBlock:X} has {took} MoveCheck calls, not 1 — patch order changed?");
        }

        private const uint CheckDmgSwingSite = 0x001DA098, SetCollisionData = 0x001B57A0;   // CheckDmg's `jal Set__14CCollisionDataFPfiiffiiii`
        private const uint CheckDmgMaskSite = 0x001DAA7C, LoadMask = 0x8CA20048, AndMaskPlayerHits = 0x30420002;   // lw v0,0x48(a1) / andi v0,v0,2

        /// <summary>A word that holds <paramref name="vanilla"/> (or <paramref name="ours"/> already) made <paramref name="ours"/>.</summary>
        private static void Hook(FileStream fs, Func<uint, long> ElfOff, uint at, uint vanilla, uint ours)
        {
            uint cur = RdU32(fs, ElfOff(at));
            if (cur != vanilla && cur != ours)
                throw new IOException($"0x{at:X} is 0x{cur:X8}, not vanilla 0x{vanilla:X8} — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(at), ours);
        }
        private const uint PlayerMoveCheck = 0x001DC820, PlayerSpearBlockSpan = 240;   // MoveCheck__12CMonstorUnitFPfPfi; the player-spear cave's bytes

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
