using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Per-weapon ELF patches: Babel's spear column, the Terra Sword's boulder shadow, the Sun Sword's blade tint, the
    /// lock-on name-plate gate, the magic circles, Xiao's build-up tree, the Steel Slingshot's level-ups, the flamethrower spacing,
    /// the Super Steve HUD icon and Mirage's heat shimmer. Called in order from ElfPatches.ElfPatchAndCrc (the dead hosts are claimed first by
    /// ElfDeadFunctionPatches).</summary>
    internal static class ElfWeaponPatches
    {
        /// <summary>The spear-block caves written into DebugInfomationIF's body: the enemies' (DebugIfCave.SpearBlock,
        /// tools/stubs/spear_block.s) with Step__12CMonstorUnit's `jal MoveChecMonster` (main 0x1DE344) pointed at it, and the
        /// player's (DebugIfCave.PlayerSpearBlock, tools/stubs/player_spear_block.s), whose hooks are the overlay's (DunPatches), and
        /// the enemy shots' (DebugIfCave.ShotSpearBlock, tools/stubs/shot_spear_block.s) with Step__12CSHOT_EFFECT's `jal checkCollision`
        /// (main 0x1AC3E8) pointed at it.</summary>
        internal static void PatchSpearBlock(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x001DE344, MoveChecMonster = 0x001DD140;
            uint cave = DebugIfCave.SpearBlock;
            byte[] b = Embedded("spearBlock.bin");
            bool callsMove = ContainsWord(b, Jal(MoveChecMonster));
            if (b.Length % 4 != 0 || b.Length < 0x80 || U32(b, 0) != 0x27BDFFE0u || !callsMove)
                throw new IOException($"spearBlock.bin malformed ({b.Length} B) or stale — reassemble its .s (it must call MoveChecMonster).");
            WriteBytes(fs, ElfOff, cave, b, DebugIfCave.Host + DebugIfCave.HostSpan, "spearBlock.bin overruns DebugInfomationIF's span.");
            uint cur = RdU32(fs, ElfOff(HookAddr)), ours = Jal(cave);
            if (cur != Jal(MoveChecMonster) && cur != ours)
                throw new IOException($"Step__12CMonstorUnit's site 0x{HookAddr:X} is not vanilla `jal MoveChecMonster` — unmodified Dark Cloud (USA) ISO expected.");
            if (RdU32(fs, ElfOff(HookAddr + 4)) != 0) throw new IOException("Step__12CMonstorUnit is not laid out as expected around MoveChecMonster (delay slot).");
            WrU32(fs, ElfOff(HookAddr), ours);

            // The player's side (tools/stubs/player_spear_block.s): the cave here; its two hooks are in the overlay (DunPatches).
            const uint PlayerMoveCheck = 0x001DC820;
            uint pcave = DebugIfCave.PlayerSpearBlock;
            byte[] pb = Embedded("playerSpearBlock.bin");
            bool callsPlayer = ContainsWord(pb, Jal(PlayerMoveCheck));
            if (pb.Length % 4 != 0 || pb.Length < 0x80 || U32(pb, 0) != 0x27BDFFE0u || !callsPlayer)
                throw new IOException($"playerSpearBlock.bin malformed ({pb.Length} B) or stale — reassemble its .s (it must call MoveCheck__12CMonstorUnit).");
            WriteBytes(fs, ElfOff, pcave, pb, DebugIfCave.Host + DebugIfCave.HostSpan, "playerSpearBlock.bin overruns DebugInfomationIF's span.");

            // Enemy shots (tools/stubs/shot_spear_block.s): Step__12CSHOT_EFFECT's `jal checkCollision` → the cave.
            const uint ShotHookAddr = 0x001AC3E8, CheckCollision = 0x001AB740;
            uint scave = DebugIfCave.ShotSpearBlock;
            byte[] sb = Embedded("shotSpearBlock.bin");
            bool callsCheck = ContainsWord(sb, Jal(CheckCollision));
            if (sb.Length % 4 != 0 || sb.Length < 0x80 || U32(sb, 0) != 0x27BDFFD0u || !callsCheck)
                throw new IOException($"shotSpearBlock.bin malformed ({sb.Length} B) or stale — reassemble its .s (it must call checkCollision).");
            WriteBytes(fs, ElfOff, scave, sb, DebugIfCave.Host + DebugIfCave.HostSpan, "shotSpearBlock.bin overruns DebugInfomationIF's span.");
            uint scur = RdU32(fs, ElfOff(ShotHookAddr)), sours = Jal(scave);
            if (scur != Jal(CheckCollision) && scur != sours)
                throw new IOException($"Step__12CSHOT_EFFECT's site 0x{ShotHookAddr:X} is not vanilla `jal checkCollision` — unmodified Dark Cloud (USA) ISO expected.");
            if (RdU32(fs, ElfOff(ShotHookAddr + 4)) != 0) throw new IOException("Step__12CSHOT_EFFECT is not laid out as expected around checkCollision (delay slot).");
            WrU32(fs, ElfOff(ShotHookAddr), sours);
        }

        /// <summary>The Terra Sword's boulder shadow (tools/stubs/rock_shadow.s): the cave here; its hook — Draw_MainUnitShadow's `jal
        /// MGEndDrawShadow` — is in the overlay (DunPatches).</summary>
        internal static void PatchRockShadow(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint DrawShadowFast = 0x001303B0, EndDrawShadow = 0x00130B30;
            uint cave = DebugIfCave.RockShadow;
            byte[] b = Embedded("rockShadow.bin");
            bool callsDraw = ContainsWord(b, Jal(DrawShadowFast)), endsInEnd = ContainsWord(b, MipsAsm.J(EndDrawShadow));
            if (b.Length % 4 != 0 || U32(b, 0) != 0x27BDFFE0u || !callsDraw || !endsInEnd)
                throw new IOException($"rockShadow.bin malformed ({b.Length} B) or stale — reassemble its .s (it must call MGDrawShadowFast and jump to MGEndDrawShadow).");
            WriteBytes(fs, ElfOff, cave, b, DebugIfCave.Host + DebugIfCave.HostSpan, "rockShadow.bin overruns DebugInfomationIF's span.");
        }

        /// <summary>The Sun Sword's blade under its own ambient (BladeTint): the mask-tint cave's BODY is generic — it adds
        /// CatBlock.CatCapeTint to the ambient, calls the DrawVu1 in t9, restores — only its two 3-word entries name the skinned
        /// class's overloads. A weapon model's mesh is a CVisualVu1, so these two entries load THAT class's overloads and jump
        /// into the same body. Six words in the cave band's last gap; the private vtable BladeTint builds points its DrawVu1
        /// slots here.</summary>
        internal static void PatchSolarBladeTint(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint DrawVu1Words = 0x00135000u, DrawVu1Packet = 0x00134BC0u;          // DrawVu1__10CVisualVu1, the uint* and sceVif1Packet* overloads
            uint body = ElfCave.CatMaskTint + 0x18;                             // past the mask cave's own two entries
            if (RdU32(fs, ElfOff(ElfCave.CatMaskTint)) != 0x3C190013u || RdU32(fs, ElfOff(body)) != 0x27BDFF70u)
                throw new IOException("PatchSolarBladeTint must follow PatchCatMaskTint (its entries `lui t9,0x13` and body `addiu sp,sp,-0x90`).");
            uint[] words =
            {
                0x3C190000u | (DrawVu1Words >> 16),  J(body), 0x37390000u | (DrawVu1Words & 0xFFFFu),    // lui t9,HI; j body; ori t9,t9,LO  (vtable slot 6)
                0x3C190000u | (DrawVu1Packet >> 16), J(body), 0x37390000u | (DrawVu1Packet & 0xFFFFu),   // (vtable slot 7)
            };
            for (int i = 0; i < words.Length; i++)
            {
                uint cur = RdU32(fs, ElfOff(ElfCave.SolarBladeTint + (uint)(i * 4)));
                if (cur != 0 && cur != words[i])
                    throw new IOException($"Cave gap 0x{ElfCave.SolarBladeTint + i * 4:X} holds 0x{cur:X8} — not free.");
                WrU32(fs, ElfOff(ElfCave.SolarBladeTint + (uint)(i * 4)), words[i]);
            }
        }

        /// <summary>The lock-on NAME PLATE, gated by a mod word. MonsterNameDraw asks GetMonsterNameDrawFlag (0x20EB70,
        /// the flag's only reader), and setTargetCursor re-raises the flag through its setter every frame the target is
        /// on screen — so an ability cannot hide the plate by writing the flag; it is back next frame. The getter,
        /// <code>
        ///   0x20EB70  lh  v0,-0x69E0(gp)
        ///   0x20EB74  jr  ra
        ///   0x20EB78  nop
        /// </code>
        /// becomes `j NameDrawGate; nop`, and the cave returns the flag AND NOT <see cref="CodeCaves.NameHide"/>:
        /// <code>
        ///   lh  v0,-0x69E0(gp)  /  lui at,HI  /  lw at,LO(at)  /  nor at,zero,at  /  jr ra  /  and v0,v0,at
        /// </code>
        /// A zero word — what fresh memory holds — is vanilla, so nothing needs seeding; `at` is the only register
        /// touched beyond the return value.</summary>
        internal static void PatchNameDrawGate(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Getter = 0x0020EB70;
            const uint VanillaLh = 0x87829620u, VanillaJr = 0x03E00008u;            // lh v0,-0x69e0(gp) ; jr ra
            uint cave = ElfCave.NameDrawGate;
            HiLo(CodeCaves.NameHideGuest, out uint hi, out uint lo);                 // lw's offset is signed
            uint[] words =
            {
                VanillaLh,                      // lh  v0,-0x69e0(gp)   the flag, as the getter read it
                0x3C010000u | hi,               // lui at,HI(NameHide)
                0x8C210000u | lo,               // lw  at,LO(at)
                0x00010827u,                    // nor at,zero,at       ~hide
                VanillaJr,                      // jr  ra
                0x00411024u,                    // and v0,v0,at         (delay slot)
            };
            for (int i = 0; i < words.Length; i++)
            {
                uint cur = RdU32(fs, ElfOff(cave + (uint)(i * 4)));
                if (cur != 0 && cur != words[i])
                    throw new IOException($"Cave gap 0x{cave + i * 4:X} holds 0x{cur:X8} — not free.");
                WrU32(fs, ElfOff(cave + (uint)(i * 4)), words[i]);
            }
            ReplaceWords(fs, ElfOff, Getter, new[] { VanillaLh, VanillaJr }, new[] { MipsAsm.J(cave), 0u },   // j cave; its delay slot
                         got => $"GetMonsterNameDrawFlag 0x{Getter:X} is not vanilla (got 0x{got[0]:X8}/0x{got[1]:X8}) " +
                                "— is this an unmodified Dark Cloud (USA) ISO?");
        }

        /// <summary>THE MAGIC CIRCLES as data (tools/stubs/circle_effects.s, CodeCaves.CircleTable): the cave in the body of
        /// DebugInfomationIF (DebugIfCave, claimed by ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts so the debug
        /// key's one call reads "nothing pressed"), which dun.bin's Run_TrapCircle jumps to (DunPatches). Every circle magnitude is
        /// then a word the mod may set.</summary>
        internal static void PatchCircleEffects(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Host = DebugIfCave.Host, Cave = DebugIfCave.CircleEffects;
            byte[] b = Embedded("circleEffects.bin");
            // Shape: the engine's own frame, and the calls it makes — BtSetStatusErr, WeaponDataChangeByRGate, SetWeaponAttachStatus, rand, GetItem, SndSePlay.
            bool statusErr = ContainsWord(b, Jal(0x001B1BB0)), rgate = ContainsWord(b, Jal(0x0020FCE0)), attach = ContainsWord(b, Jal(0x00225AA0));
            bool rnd = ContainsWord(b, Jal(0x001046F8)), getItem = ContainsWord(b, Jal(0x001BE060)), se = ContainsWord(b, Jal(0x0015A6B0));
            if (b.Length % 4 != 0 || b.Length < 0x200 || U32(b, 0) != 0x27BDFF70u || !statusErr || !rgate || !attach || !rnd || !getItem || !se)
                throw new IOException($"circleEffects.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (Cave + (uint)b.Length > Host + DebugIfCave.HostSpan)
                throw new IOException("circleEffects.bin overruns DebugInfomationIF's span.");
            WriteBytes(fs, ElfOff, Cave, b);
        }

        /// <summary>Xiao's build-up tree, baked: the weapon template table's build-up word (WeaponList +0x3C, bit k = the weapon
        /// 299 + k may be built up into) — Hardshooter → Double Impact alone (vanilla: Double Impact or Matador), Double Impact →
        /// Matador alone (vanilla: Divine Beast Title).</summary>
        internal static void PatchXiaoBuildUp(FileStream fs, Func<uint, long> ElfOff)
        {
            foreach (var (item, vanilla, ours, what) in new[]
            {
                (Items.hardshooter,  0x00001080u, 0x00000080u, "Hardshooter → Double Impact"),
                (Items.doubleimpact, 0x00000200u, 0x00001000u, "Double Impact → Matador"),
            })
            {
                uint addr = (uint)(WeaponTable.BuildUp - 0x20000000 + WeaponTable.XiaoOffset + WeaponTable.Stride * (item - WeaponTable.WoodenSlingshotId));
                ReplaceWord(fs, ElfOff, addr, vanilla, ours,   // {what}
                            cur => $"Build-up word of weapon {item} at 0x{addr:X} is 0x{cur:X}, not vanilla 0x{vanilla:X} — unmodified Dark Cloud (USA) ISO expected.");
            }
        }

        /// <summary>The Steel Slingshot's level-up bonus is +2 endurance instead of +1 and twice the max-WHP roll (tools/stubs/
        /// steel_level_up.s, after the pellet-sprite cave in DebugInfomationDraw's body): one add per stat in the commit routine
        /// and the item-use routine becomes a call into the cave, which reads the record's item id and does the vanilla add for
        /// every other weapon. The attached items' sums are untouched.</summary>
        internal static void PatchSteelLevelUp(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = DebugInfoCave.SteelLevelUp;
            byte[] b = Embedded("steelLevelUp.bin");
            int jrRa = 0;
            for (int i = 0; i + 4 <= b.Length; i += 4) if (U32(b, i) == 0x03E00008u) jrRa++;
            if (b.Length % 4 != 0 || b.Length < 0x50 || (U32(b, 0) >> 16) != 0x1000 || (U32(b, 0x18) >> 16) != 0x1000 || jrRa != 4)
                throw new IOException($"steelLevelUp.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > DebugInfoCave.Host + DebugInfoCave.HostSpan)
                throw new IOException("steelLevelUp.bin overruns DebugInfomationDraw's span.");
            if (RdU32(fs, ElfOff(DebugInfoCave.Host)) != 0x03E00008u)
                throw new IOException("PatchSteelLevelUp must follow ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts (the host's `jr ra`).");
            WriteBytes(fs, ElfOff, CaveAddr, b);
            // (site, the vanilla word it replaces, the delay-slot word that stays, the cave entry)
            foreach (var (site, vanilla, delay, entry) in new[]
            {
                (0x002367CCu, 0x24630001u, 0xA4830006u, DebugInfoCave.SteelLevelUpB),   // SetLevelUpWeaponData: addiu v1,v1,1; sh v1,6(a0)
                (0x00236880u, 0x00641821u, 0xA6230000u, DebugInfoCave.SteelLevelUpC),   // SetLevelUpWeaponData: addu v1,v1,a0; sh v1,0(s1)
                (0x00235D94u, 0x87A200AAu, 0x24430001u, DebugInfoCave.SteelLevelUpD),   // WeaponLevelUpValueCalc: lh v0,0xAA(sp); addiu v1,v0,1
                (0x00235EE4u, 0x00641821u, 0xA6A3000Cu, DebugInfoCave.SteelLevelUpE),   // WeaponLevelUpValueCalc: addu v1,v1,a0; sh v1,0xC(s5)
            })
                ReplaceWords(fs, ElfOff, site, new[] { vanilla }, new[] { Jal(entry) },
                             _ => $"Weapon level-up site 0x{site:X} is not vanilla (0x{vanilla:X8} then 0x{delay:X8}) — unmodified Dark Cloud (USA) ISO expected.",
                             (site + 4, delay));
        }

        /// <summary>Osmond's flamethrower reach from a data word: Set__13CSHOT_FIREBAR (0x1AED88) and Init__13CSHOT_FIREBAR (0x1AEB6C)
        /// each load the particle spacing as `lui v0,0x4000; mtc1 v0,f12` (2.0) before scaling the aim by it → `lui v0,HI;
        /// lwc1 f12,LO(v0)` of Mailbox.FlameSpacing (pnach-seeded 2.0; the Skunk writes 4.0 = twice the reach).</summary>
        internal static void PatchFlameSpacing(FileStream fs, Func<uint, long> ElfOff)
        {
            uint hi = 0x3C020000u | (uint)((Mailbox.FlameSpacing - 0x20000000) >> 16);
            uint lo = 0xC44C0000u | (uint)((Mailbox.FlameSpacing - 0x20000000) & 0xFFFF);
            foreach (var (site, next) in new[] { (0x001AED88u, 0x27A40060u), (0x001AEB6Cu, 0x27A40070u) })
                ReplaceWords(fs, ElfOff, site, new[] { 0x3C024000u, 0x44826000u }, new[] { hi, lo },
                             _ => $"Flamethrower spacing site 0x{site:X} is not vanilla `lui v0,0x4000; mtc1 v0,f12` — unmodified Dark Cloud (USA) ISO expected.",
                             (site + 8, next));
        }

        /// <summary>Every main-ELF call of DngActiveWeaponTextureCopy — the game's copy opportunities, each while a menu has
        /// the wepicon sheet registered: WeaponSelectKey, BtMenuLoad2, ExitDunEnterMenu, CharaChangeLoop.</summary>
        internal static readonly uint[] SsIconCopyMainHooks = { 0x001FE05C, 0x0020EA98, 0x00226560, 0x00228DDC };

        /// <summary>The dungeon HUD gains the icon of the weapon whose SynthSphere Super Steve carries, over Steve. Two
        /// caves: the DRAW (the overlay's `jal topStatusInfo`, dun 0x1DB0364, hooked by DunPatches) and the COPY that keeps
        /// the icon in a spare cell of the HUD sheet on every DngActiveWeaponTextureCopy call — the overlay's two sites
        /// (DunPatches) and the four menu paths in <see cref="SsIconCopyMainHooks"/> (hooked here).</summary>
        internal static void PatchSuperSteveIconDraw(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = ElfCave.SuperSteveIconDraw;
            byte[] b = Embedded("superSteveIconDraw.bin");
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFC0u)   // opens its frame: addiu sp,sp,-0x40
                throw new IOException($"superSteveIconDraw.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            // The words that make it THIS cave: the displaced call (jal topStatusInfo) and the draw (jal set2DSprite).
            bool orig = ContainsWord(b, DunPatches.SsIconHookOrig), draw = ContainsWord(b, 0x0C0570C4u);
            if (!orig || !draw)
                throw new IOException("superSteveIconDraw.bin lacks the topStatusInfo call or the set2DSprite call.");
            WriteBytes(fs, ElfOff, CaveAddr, b, ElfCave.NextFree, "superSteveIconDraw.bin overruns its cave — move ElfCave.NextFree.");

            const uint CopyAddr = ElfCave.SuperSteveIconCopy;
            byte[] c = Embedded("superSteveIconCopy.bin");
            bool origCopy = ContainsWord(c, DunPatches.SsIconCopyHookOrig), move = ContainsWord(c, 0x0C06C7BCu);
            if (c.Length < 8 || U32(c, 0) != 0x27BDFFE0u || !origCopy || !move)
                throw new IOException("superSteveIconCopy.bin malformed or stale — it must call DngActiveWeaponTextureCopy and setItemToReserved.");
            WriteBytes(fs, ElfOff, CopyAddr, c, ElfCave.NextFree, "superSteveIconCopy.bin overruns its cave — move ElfCave.NextFree.");
            foreach (uint site in SsIconCopyMainHooks)
                ReplaceWord(fs, ElfOff, site, DunPatches.SsIconCopyHookOrig, DunPatches.SsIconCopyHookNew,
                            cur => $"copy hook site 0x{site:X} is not `jal DngActiveWeaponTextureCopy` (0x{cur:X8}) — unmodified Dark Cloud (USA) is required.");
        }

        /// <summary>Mirage's heat shimmer at the clone itself: the dungeon draw loop's raster pass (dun 0x1DAEBCC, hooked by DunPatches)
        /// comes to this cave, which performs it and then draws one raster at the Mirage clone's root when the mailbox says so.</summary>
        internal static void PatchMirageHazeDraw(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = ElfCave.MirageHazeDraw;
            byte[] b = Embedded("mirageHazeDraw.bin");
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFE0u)   // opens its frame: addiu sp,sp,-0x20
                throw new IOException($"mirageHazeDraw.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            // The two words that make it THIS cave: the "alpha01" string's address (ori a1,a1,0xA0E8) and the draw call
            // (jal DrawRaster__9CFireOmni 0x162310). A wrong immediate in either fails invisibly — nothing drawn, no error.
            bool name = ContainsWord(b, 0x34A5A0E8u), draw = ContainsWord(b, 0x0C0588C4u);
            if (!name || !draw)
                throw new IOException("mirageHazeDraw.bin lacks the \"alpha01\" address or the DrawRaster call — it would draw nothing.");
            WriteBytes(fs, ElfOff, CaveAddr, b, ElfCave.NextFree, "mirageHazeDraw.bin overruns its cave — move ElfCave.NextFree.");
        }
    }
}
