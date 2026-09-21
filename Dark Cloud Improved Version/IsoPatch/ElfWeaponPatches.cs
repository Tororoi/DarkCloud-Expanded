using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Weapon-ability ELF patches: the Matador's pellet prop, the borrowed-shots keeper, the shot-slot sharing cave, the pellet sprite, Xiao's build-up tree, the Steel Slingshot's level-ups, the flamethrower spacing and the Super Steve HUD icon. Called in order from ElfPatches.ElfPatchAndCrc.</summary>
    internal static class ElfWeaponPatches
    {
        /// <summary>The prop-on-a-pellet cave (tools/stubs/prop_pellet_follow.s): the cat follower hook's new first stop. It must
        /// call CatCopyQueue (the cat's chain performs the displaced step__5CSHOT) and read the shot pool.</summary>
        internal static void PatchPropPelletFollow(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.PropPelletFollow;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.propPelletFollow.bin")
                ?? throw new IOException("Embedded EE function missing: propPelletFollow.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            uint chain = 0x0C000000u | (CodeCaves.ElfCave.CatCopyQueue >> 2);
            bool chained = false, pool = false;
            for (int i = 0; i + 4 <= b.Length; i += 4) { uint w = U32(b, i); if (w == chain) chained = true; if (w == 0x8D4A35D4u) pool = true; }
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFE0u || !chained || !pool)
                throw new IOException("propPelletFollow.bin malformed or stale — it must call CatCopyQueue and read the shot pool.");
            if (CaveAddr + (uint)b.Length > 0x01FB2000u)
                throw new IOException("propPelletFollow.bin overruns its gap — it must end before 0x1FB2000 (the first band's end).");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
        }

        /// <summary>The borrowed-shots cave (tools/stubs/borrowed_shots_enter.s): the head of the step chain — must call
        /// PropPelletFollow (the rest of the chain) and Entry__17CSHOT_EFFECT_PACK, and read NowShotEffect.</summary>
        internal static void PatchBorrowedShotsEnter(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.ElfCave.BorrowedShotsEnter;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.borrowedShotsEnter.bin")
                ?? throw new IOException("Embedded EE function missing: borrowedShotsEnter.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            bool chainCall = false;
            uint chain = 0x0C000000u | (CodeCaves.ElfCave.PropPelletFollow >> 2);
            for (int i = 0; i + 4 <= b.Length; i += 4) { uint w = U32(b, i); if (w == chain) chainCall = true; }
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFE0u || !chainCall)
                throw new IOException("borrowedShotsEnter.bin (the head) malformed or stale — it must call PropPelletFollow.");
            if (CaveAddr + (uint)b.Length > 0x01FB2000u)
                throw new IOException("borrowedShotsEnter.bin (the head) overruns its gap — it must end by 0x1FB2000.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            // …and the tail piece, at the band's end
            using var st2 = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.borrowedShotsEnterTail.bin")
                ?? throw new IOException("Embedded EE function missing: borrowedShotsEnterTail.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms2 = new MemoryStream(); st2.CopyTo(ms2); byte[] t = ms2.ToArray();
            const uint TailAddr = CodeCaves.ElfCave.BorrowedShotsEnterTail;
            bool jrRa = false;
            for (int i = 0; i + 4 <= t.Length; i += 4) if (U32(t, i) == 0x03E00008u) jrRa = true;
            if (t.Length < 8 || t.Length % 4 != 0 || !jrRa)
                throw new IOException("borrowedShotsEnterTail.bin malformed or stale — reassemble its .s.");
            if (TailAddr + (uint)t.Length > CodeCaves.ElfCave.RegionEnd)
                throw new IOException("borrowedShotsEnterTail.bin overruns the band — it must end by ElfCave.RegionEnd.");
            for (int i = 0; i < t.Length; i += 4)
                WrU32(fs, ElfOff(TailAddr + (uint)i), U32(t, i));
        }

        /// <summary>The monster shot pack's five slots shared among every shot config a floor needs (tools/stubs/shared_shots.s,
        /// hosted in the dead DebugInfomationDraw — its first word becomes `jr ra`, so its debug-flag caller returns at once):
        /// the species loader's two pack calls store a refused config's negative form in the species row, Step's two fire sites
        /// acquire a config that is not in the pack before firing, and the dungeon step loop's chain head (DunPatches.
        /// CatFollowHookNew) preloads one a frame while a slot is free.</summary>
        internal static void PatchSharedShots(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Host = CodeCaves.DebugInfoCave.Host;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.sharedShots.bin")
                ?? throw new IOException("Embedded EE function missing: sharedShots.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: opens with the four-entry branch table, calls the keeper (its chain), Entry__17, Initialize__12 and Entry__12.
            uint keeper = Jal(CodeCaves.ElfCave.BorrowedShotsEnter);
            bool hasKeeper = false, hasEntry17 = false, hasInit = false, hasEntry12 = false;
            for (int i = 0; i + 4 <= b.Length; i += 4)
            {
                uint w = U32(b, i);
                if (w == keeper) hasKeeper = true; if (w == Jal(0x001AE4C0)) hasEntry17 = true;
                if (w == Jal(0x001AE440)) hasInit = true; if (w == Jal(0x001ACC70)) hasEntry12 = true;
            }
            if (b.Length % 4 != 0 || b.Length < 0x40 || (U32(b, 0) >> 16) != 0x1000 || (U32(b, 8) >> 16) != 0x1000 || !hasKeeper || !hasEntry17 || !hasInit || !hasEntry12)
                throw new IOException($"sharedShots.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CodeCaves.DebugInfoCave.SharedShots + (uint)b.Length > CodeCaves.DebugInfoCave.PelletSprite)
                throw new IOException("sharedShots.bin overruns into the pellet-sprite cave that follows it in DebugInfomationDraw.");
            uint w0 = RdU32(fs, ElfOff(Host));
            if (w0 != CodeCaves.DebugInfoCave.VanillaWord0 && w0 != 0x03E00008u)
                throw new IOException($"DebugInfomationDraw at 0x{Host:X} is not vanilla (`addiu sp,sp,-0x170`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(Host), 0x03E00008u);                                   // jr ra: the overlay draws nothing
            WrU32(fs, ElfOff(Host + 4), 0);                                          // (its delay slot)
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(Host + 8 + (uint)i), U32(b, i));
            // SetupBaseModel's two pack calls: `jal Entry__17CSHOT_EFFECT_PACK; nop; addiu v1,zero,-1`
            uint enter = Jal(CodeCaves.DebugInfoCave.SharedShotsEnter);
            foreach (uint site in new[] { 0x001E01B0u, 0x001E0224u })
            {
                uint cur = RdU32(fs, ElfOff(site));
                if ((cur != Jal(0x001AE4C0) && cur != enter) || RdU32(fs, ElfOff(site + 4)) != 0 || RdU32(fs, ElfOff(site + 8)) != 0x2403FFFFu)
                    throw new IOException($"Species-loader pack call at 0x{site:X} is not vanilla `jal Entry__17CSHOT_EFFECT_PACK; nop; addiu v1,zero,-1` — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), enter);
            }
            // Step's two fire sites: the `lui at,0x6` before the request load; its delay slot, the vanilla `addu at,a0,at`, stays
            // (the cave re-forms at = a0 + 0x60000 before returning to the load)
            foreach (var (site, entry, load) in new[] { (0x001DEED0u, CodeCaves.DebugInfoCave.SharedShotsFire0, 0x8C23FF74u), (0x001DEFD8u, CodeCaves.DebugInfoCave.SharedShotsFire1, 0x8C230274u) })
            {
                uint cur = RdU32(fs, ElfOff(site)), ours = Jal(entry);
                if ((cur != 0x3C010006u && cur != ours) || RdU32(fs, ElfOff(site + 4)) != 0x00810821u || RdU32(fs, ElfOff(site + 8)) != load || RdU32(fs, ElfOff(site + 12)) != 0x24020002u)
                    throw new IOException($"Monster fire site at 0x{site:X} is not vanilla `lui at,0x6; addu at,a0,at; lw v1,…(at); addiu v0,zero,2` — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), ours);
            }
        }

        /// <summary>A player pellet's sprite cell taken from the item id in Mailbox.PelletSpriteId when it is set (tools/stubs/
        /// pellet_sprite.s, after the sharing cave in DebugInfomationDraw's body): draw__5CSHOT's read of the equipped weapon's
        /// id (0x1ABC74 `lw v0,-0x62FC(gp); lh v0,0(v0)`) becomes a call to it.</summary>
        internal static void PatchPelletSprite(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.DebugInfoCave.PelletSprite, HookAddr = 0x001ABC74;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.pelletSprite.bin")
                ?? throw new IOException("Embedded EE function missing: pelletSprite.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: reads the mailbox word, carries the vanilla `lw v0,-0x62FC(gp)` and `lh v0,0(v0)`, ends in `jr ra`.
            bool vanillaRead = false, jrRa = false;
            for (int i = 0; i + 4 <= b.Length; i += 4) { uint w = U32(b, i); if (w == 0x8F829D04u) vanillaRead = true; if (w == 0x03E00008u) jrRa = true; }
            if (b.Length % 4 != 0 || b.Length < 0x20 || U32(b, 0) != 0x3C0101F1u || !vanillaRead || !jrRa)
                throw new IOException($"pelletSprite.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.DebugInfoCave.SteelLevelUp)
                throw new IOException("pelletSprite.bin overruns into the level-up cave that follows it in DebugInfomationDraw.");
            if (RdU32(fs, ElfOff(CodeCaves.DebugInfoCave.Host)) != 0x03E00008u)
                throw new IOException("PatchPelletSprite must follow PatchSharedShots (the host's `jr ra`).");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            uint cur0 = RdU32(fs, ElfOff(HookAddr)), cur1 = RdU32(fs, ElfOff(HookAddr + 4)), ours = Jal(CaveAddr);
            bool vanilla = cur0 == 0x8F829D04u && cur1 == 0x84420000u, patched = cur0 == ours && cur1 == 0;
            if (!(vanilla || patched) || RdU32(fs, ElfOff(HookAddr + 8)) != 0x2443FED5u)
                throw new IOException($"Pellet draw site 0x{HookAddr:X} is not vanilla `lw v0,-0x62FC(gp); lh v0,0(v0); addiu v1,v0,-0x12B` — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(HookAddr), ours);
            WrU32(fs, ElfOff(HookAddr + 4), 0);
        }

        /// <summary>The Sun Sword's blade under its own ambient (SolarBlade): the mask-tint cave's BODY is generic — it adds
        /// Mailbox.CatCapeTint to the ambient, calls the DrawVu1 in t9, restores — only its two 3-word entries name the skinned
        /// class's overloads. A weapon model's mesh is a CVisualVu1, so these two entries load THAT class's overloads and jump
        /// into the same body. Six words in the cave band's last gap; the private vtable SolarBlade builds points its DrawVu1
        /// slots here.</summary>
        internal static void PatchSolarBladeTint(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint DrawVu1Words = 0x00135000u, DrawVu1Packet = 0x00134BC0u;          // DrawVu1__10CVisualVu1, the uint* and sceVif1Packet* overloads
            uint body = CodeCaves.ElfCave.CatMaskTint + 0x18;                             // past the mask cave's own two entries
            if (RdU32(fs, ElfOff(CodeCaves.ElfCave.CatMaskTint)) != 0x3C190013u || RdU32(fs, ElfOff(body)) != 0x27BDFF70u)
                throw new IOException("PatchSolarBladeTint must follow PatchCatMaskTint (its entries `lui t9,0x13` and body `addiu sp,sp,-0x90`).");
            uint J(uint target) => 0x08000000u | ((target >> 2) & 0x03FFFFFFu);
            uint[] words =
            {
                0x3C190000u | (DrawVu1Words >> 16),  J(body), 0x37390000u | (DrawVu1Words & 0xFFFFu),    // lui t9,HI; j body; ori t9,t9,LO  (vtable slot 6)
                0x3C190000u | (DrawVu1Packet >> 16), J(body), 0x37390000u | (DrawVu1Packet & 0xFFFFu),   // (vtable slot 7)
            };
            for (int i = 0; i < words.Length; i++)
            {
                uint cur = RdU32(fs, ElfOff(CodeCaves.ElfCave.SolarBladeTint + (uint)(i * 4)));
                if (cur != 0 && cur != words[i])
                    throw new IOException($"Cave gap 0x{CodeCaves.ElfCave.SolarBladeTint + i * 4:X} holds 0x{cur:X8} — not free.");
                WrU32(fs, ElfOff(CodeCaves.ElfCave.SolarBladeTint + (uint)(i * 4)), words[i]);
            }
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
                uint addr = (uint)(Weapons.buildup - 0x20000000 + Weapons.xiaooffset + Weapons.weaponoffset * (item - Weapons.woodenid));
                uint cur = RdU32(fs, ElfOff(addr));
                if (cur != vanilla && cur != ours)
                    throw new IOException($"Build-up word of weapon {item} at 0x{addr:X} is 0x{cur:X}, not vanilla 0x{vanilla:X} — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(addr), ours);   // {what}
            }
        }

        /// <summary>The Steel Slingshot's level-up bonus is +2 endurance instead of +1 and twice the max-WHP roll (tools/stubs/
        /// steel_level_up.s, after the pellet-sprite cave in DebugInfomationDraw's body): one add per stat in the commit routine
        /// and the item-use routine becomes a call into the cave, which reads the record's item id and does the vanilla add for
        /// every other weapon. The attached items' sums are untouched.</summary>
        internal static void PatchSteelLevelUp(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = CodeCaves.DebugInfoCave.SteelLevelUp;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.steelLevelUp.bin")
                ?? throw new IOException("Embedded EE function missing: steelLevelUp.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            int jrRa = 0;
            for (int i = 0; i + 4 <= b.Length; i += 4) if (U32(b, i) == 0x03E00008u) jrRa++;
            if (b.Length % 4 != 0 || b.Length < 0x50 || (U32(b, 0) >> 16) != 0x1000 || (U32(b, 0x18) >> 16) != 0x1000 || jrRa != 4)
                throw new IOException($"steelLevelUp.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (CaveAddr + (uint)b.Length > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("steelLevelUp.bin overruns DebugInfomationDraw's span.");
            if (RdU32(fs, ElfOff(CodeCaves.DebugInfoCave.Host)) != 0x03E00008u)
                throw new IOException("PatchSteelLevelUp must follow PatchSharedShots (the host's `jr ra`).");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));
            // (site, the vanilla word it replaces, the delay-slot word that stays, the cave entry)
            foreach (var (site, vanilla, delay, entry) in new[]
            {
                (0x002367CCu, 0x24630001u, 0xA4830006u, CodeCaves.DebugInfoCave.SteelLevelUpB),   // SetLevelUpWeaponData: addiu v1,v1,1; sh v1,6(a0)
                (0x00236880u, 0x00641821u, 0xA6230000u, CodeCaves.DebugInfoCave.SteelLevelUpC),   // SetLevelUpWeaponData: addu v1,v1,a0; sh v1,0(s1)
                (0x00235D94u, 0x87A200AAu, 0x24430001u, CodeCaves.DebugInfoCave.SteelLevelUpD),   // WeaponLevelUpValueCalc: lh v0,0xAA(sp); addiu v1,v0,1
                (0x00235EE4u, 0x00641821u, 0xA6A3000Cu, CodeCaves.DebugInfoCave.SteelLevelUpE),   // WeaponLevelUpValueCalc: addu v1,v1,a0; sh v1,0xC(s5)
            })
            {
                uint cur = RdU32(fs, ElfOff(site)), ours = Jal(entry);
                if ((cur != vanilla && cur != ours) || RdU32(fs, ElfOff(site + 4)) != delay)
                    throw new IOException($"Weapon level-up site 0x{site:X} is not vanilla (0x{vanilla:X8} then 0x{delay:X8}) — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), ours);
            }
        }

        /// <summary>Osmond's flamethrower reach from a data word: Set__13CSHOT_FIREBAR (0x1AED88) and Init__13CSHOT_FIREBAR (0x1AEB6C)
        /// each load the particle spacing as `lui v0,0x4000; mtc1 v0,f12` (2.0) before scaling the aim by it → `lui v0,HI;
        /// lwc1 f12,LO(v0)` of Mailbox.FlameSpacing (pnach-seeded 2.0; the Skunk writes 4.0 = twice the reach).</summary>
        internal static void PatchFlameSpacing(FileStream fs, Func<uint, long> ElfOff)
        {
            uint hi = 0x3C020000u | (uint)((CodeCaves.Mailbox.FlameSpacing - 0x20000000) >> 16);
            uint lo = 0xC44C0000u | (uint)((CodeCaves.Mailbox.FlameSpacing - 0x20000000) & 0xFFFF);
            foreach (var (site, next) in new[] { (0x001AED88u, 0x27A40060u), (0x001AEB6Cu, 0x27A40070u) })
            {
                uint w0 = RdU32(fs, ElfOff(site)), w1 = RdU32(fs, ElfOff(site + 4));
                bool vanilla = w0 == 0x3C024000u && w1 == 0x44826000u, ours = w0 == hi && w1 == lo;
                if (!(vanilla || ours) || RdU32(fs, ElfOff(site + 8)) != next)
                    throw new IOException($"Flamethrower spacing site 0x{site:X} is not vanilla `lui v0,0x4000; mtc1 v0,f12` — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), hi);
                WrU32(fs, ElfOff(site + 4), lo);
            }
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
            const uint CaveAddr = CodeCaves.ElfCave.SuperSteveIconDraw;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.superSteveIconDraw.bin")
                ?? throw new IOException("Embedded EE function missing: superSteveIconDraw.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFC0u)   // opens its frame: addiu sp,sp,-0x40
                throw new IOException($"superSteveIconDraw.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            // The words that make it THIS cave: the displaced call (jal topStatusInfo) and the draw (jal set2DSprite).
            bool orig = false, draw = false;
            for (int i = 0; i + 4 <= b.Length; i += 4) { uint w = U32(b, i); if (w == DunPatches.SsIconHookOrig) orig = true; if (w == 0x0C0570C4u) draw = true; }
            if (!orig || !draw)
                throw new IOException("superSteveIconDraw.bin lacks the topStatusInfo call or the set2DSprite call.");
            if (CaveAddr + (uint)b.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("superSteveIconDraw.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < b.Length; i += 4)
                WrU32(fs, ElfOff(CaveAddr + (uint)i), U32(b, i));

            const uint CopyAddr = CodeCaves.ElfCave.SuperSteveIconCopy;
            using var st2 = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.superSteveIconCopy.bin")
                ?? throw new IOException("Embedded EE function missing: superSteveIconCopy.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms2 = new MemoryStream(); st2.CopyTo(ms2); byte[] c = ms2.ToArray();
            bool origCopy = false, move = false;
            for (int i = 0; i + 4 <= c.Length; i += 4) { uint w = U32(c, i); if (w == DunPatches.SsIconCopyHookOrig) origCopy = true; if (w == 0x0C06C7BCu) move = true; }
            if (c.Length < 8 || U32(c, 0) != 0x27BDFFE0u || !origCopy || !move)
                throw new IOException("superSteveIconCopy.bin malformed or stale — it must call DngActiveWeaponTextureCopy and setItemToReserved.");
            if (CopyAddr + (uint)c.Length > CodeCaves.ElfCave.NextFree)
                throw new IOException("superSteveIconCopy.bin overruns its cave — move ElfCave.NextFree.");
            for (int i = 0; i < c.Length; i += 4)
                WrU32(fs, ElfOff(CopyAddr + (uint)i), U32(c, i));
            foreach (uint site in SsIconCopyMainHooks)
            {
                uint cur = RdU32(fs, ElfOff(site));
                if (cur != DunPatches.SsIconCopyHookOrig && cur != DunPatches.SsIconCopyHookNew)
                    throw new IOException($"copy hook site 0x{site:X} is not `jal DngActiveWeaponTextureCopy` (0x{cur:X8}) — unmodified Dark Cloud (USA) is required.");
                WrU32(fs, ElfOff(site), DunPatches.SsIconCopyHookNew);
            }
        }
    }
}
