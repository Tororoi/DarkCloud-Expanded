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
            uint cave = CodeCaves.ElfCave.NameDrawGate, word = CodeCaves.NameHideGuest;
            uint hi = word >> 16, lo = word & 0xFFFFu; if (lo >= 0x8000) hi += 1;   // lw's offset is signed
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
            uint jump = MipsAsm.J(cave);
            uint got0 = RdU32(fs, ElfOff(Getter)), got1 = RdU32(fs, ElfOff(Getter + 4));
            if (got0 == jump && got1 == 0) return;                                  // idempotent re-run
            if (got0 != VanillaLh || got1 != VanillaJr)
                throw new IOException($"GetMonsterNameDrawFlag 0x{Getter:X} is not vanilla (got 0x{got0:X8}/0x{got1:X8}) " +
                                      "— is this an unmodified Dark Cloud (USA) ISO?");
            WrU32(fs, ElfOff(Getter),     jump);
            WrU32(fs, ElfOff(Getter + 4), 0);                                       // the jump's delay slot
        }

        /// <summary>TOAN'S STRIDE, scaled without touching his animation. The dungeon walk is not root motion in the
        /// .mot sense — the clips play in place — the player key handler builds a per-frame move vector from his yaw
        /// and clip-driven magnitudes and hands it to MoveCheck; at dun 0x1DB0F68 that vector sits in f21 (X) and
        /// f20 (Z), about to be halved for Goo and zeroed for Freeze:
        /// <code>
        ///   0x1DB0F68  li  a0,0x40         →  jal StrideScale cave
        ///   0x1DB0F6C  jal 0x1B1930        →  li  a0,0x40   (the jal's delay slot: the displaced load)
        ///   0x1DB0F70  nop                    (the call returns here, as before)
        /// </code>
        /// The cave adds <see cref="CodeCaves.StrideScale"/> × the vector back onto it while the current motion is 33
        /// (the guard walk), then tail-jumps into the status check the hook displaced, which returns to the handler
        /// with a0 = 0x40 in place. A zero word is vanilla, so nothing needs seeding. Clobbers f0-f2, t0-t2 — all
        /// temporaries the handler reloads before use.</summary>
        internal static void PatchStrideScale(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint StatusCheck = 0x001B1930, MotionIdGuest = 0x01EA2988, Motion = 33;
            uint cave = CodeCaves.DebugInfoCave.StrideScale, word = CodeCaves.StrideScaleGuest;
            uint hi = word >> 16, lo = word & 0xFFFFu; if (lo >= 0x8000) hi += 1;     // lwc1's offset is signed
            uint[] words =
            {
                0x3C080000u | hi,                                   // lui   t0,HI(StrideScale)
                0xC5000000u | lo,                                   // lwc1  f0,LO(t0)              the extra fraction
                0x3C090000u | (MotionIdGuest >> 16),                // lui   t1,HI(MotionId)
                0x8D290000u | (MotionIdGuest & 0xFFFFu),            // lw    t1,LO(t1)              the current motion
                0x240A0000u | Motion,                               // addiu t2,zero,33
                0x152A0005u,                                        // bne   t1,t2,+5 → skip
                0x00000000u,                                        //   nop
                0x4600A842u,                                        // mul.s f1,f21,f0
                0x4600A082u,                                        // mul.s f2,f20,f0
                0x4601AD40u,                                        // add.s f21,f21,f1             X stride += extra × X
                0x4602A500u,                                        // add.s f20,f20,f2             Z stride += extra × Z
                MipsAsm.J(StatusCheck),                             // j     0x1B1930   (skip:)     the displaced call, returning to the handler
                0x00000000u,                                        //   nop
            };
            if (cave + (uint)words.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The stride cave does not fit its host (DebugInfomationDraw).");
            for (int i = 0; i < words.Length; i++)
                WrU32(fs, ElfOff(cave + (uint)(i * 4)), words[i]);
        }

        /// <summary>The CAMERA PIN cave (see CodeCaves.DebugInfoCave.CameraPin). Entered by `j` from the dungeon camera
        /// pass's epilogue in place of its `jr ra` (the delay-slot nop and the `addiu sp` before it are the pass's own),
        /// so it runs after the pass has done everything else to the camera, with ra the pass's return and the stack
        /// already unwound. While the pin flag is clear it returns at once. Set, it holds the camera's ABSOLUTE HEIGHT:
        /// its height field is the height above its follow point R (Step renders the camera at R + dist·(sin, cos)(angle),
        /// up by height), so height = P.y − R.y every frame keeps the camera at the pinned world height P.y while the
        /// follow point rises and falls with Toan. Distance and angle stay the engine's — the camera keeps its vanilla
        /// place around him. Caller-saved registers only (t0, t1, a0, f3, f6); no calls, no frame.</summary>
        internal static void PatchCameraPin(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = CodeCaves.DebugInfoCave.CameraPin, pin = CodeCaves.CameraPinGuest;
            uint hi = pin >> 16, lo = pin & 0xFFFFu; if (lo >= 0x8000) hi += 1;      // the loads' offsets are signed
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(pin)
                0x8D090000u | Lo(CodeCaves.CameraPinFlag),          //  1 lw    t1,flag(t0)
                0x11200000u | 6,                                    //  2 beq   t1,zero,ret (+6 → index 9)
                0x00000000u,                                        //  3   nop
                0x8F849CA8u,                                        //  4 lw    a0,-0x6358(gp)         the camera (NowCamera)
                0xC48302C4u,                                        //  5 lwc1  f3,0x2C4(a0)          R.y
                0xC5060000u | Lo(4),                                //  6 lwc1  f6,P.y(t0)
                0x46033181u,                                        //  7 sub.s f6,f6,f3              height = P.y − R.y
                0xE48602D4u,                                        //  8 swc1  f6,0x2D4(a0)
                MipsAsm.J(CodeCaves.DebugInfoCave.BladeFall),       //  9 ret: j BladeFall (which returns through ra)
                0x00000000u,                                        // 10   nop
            };
            if (cave + (uint)words.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The camera-pin cave does not fit its host (DebugInfomationDraw).");
            for (int i = 0; i < words.Length; i++)
                WrU32(fs, ElfOff(cave + (uint)(i * 4)), words[i]);
        }

        /// <summary>The BLADE cave (see CodeCaves.DebugInfoCave.BladeFall): the tail of the camera-pin chain, once a frame.
        /// Flag 1, FALLING: vy += g; y −= vy; if y ≤ stop then y = stop and the flag becomes 2; y and vy stored back, and y
        /// written to the blade copy's slot height. Flag 3, FOLLOWING: the unit position at the guest pointer in the words
        /// gives the copy's x and z, and its height plus the y word (a height OVER the unit) the copy's height — the hover
        /// riding an enemy the engine moves, up and down as well, at the engine's own frame. Caller-saved registers only
        /// (t0..t3, f0..f3); no calls.</summary>
        internal static void PatchBladeFall(FileStream fs, Func<uint, long> ElfOff)
        {
            uint cave = CodeCaves.DebugInfoCave.BladeFall, w = CodeCaves.BladeFallGuest;
            uint hi = w >> 16, lo = w & 0xFFFFu; if (lo >= 0x8000) hi += 1;          // signed offsets
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint slotPos = (uint)(DungeonCharaDraw.CharaArray - 0x20000000L) + (uint)(BladeProp.Slot * DungeonCharaDraw.CharaStride) + (uint)CCharacter.CharPos;
            uint shi = (slotPos + 0x8000u) >> 16; uint SLo(int o) => (slotPos + (uint)o) & 0xFFFFu;   // x +0, y +4, z +8 (all past the sign bit alike)
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(fall)
                0x8D090000u | Lo(CodeCaves.BladeFallFlag),          //  1 lw    t1,flag(t0)
                0x240A0001u,                                        //  2 li    t2,1
                0x152A0000u | 19,                                   //  3 bne   t1,t2,follow (+19 → index 23)
                0x00000000u,                                        //  4   nop
                0xC5000000u | Lo(CodeCaves.BladeFallY),             //  5 lwc1  f0,y(t0)
                0xC5010000u | Lo(CodeCaves.BladeFallVy),            //  6 lwc1  f1,vy(t0)
                0xC5020000u | Lo(CodeCaves.BladeFallG),             //  7 lwc1  f2,g(t0)
                0xC5030000u | Lo(CodeCaves.BladeFallStop),          //  8 lwc1  f3,stop(t0)
                0x46020840u,                                        //  9 add.s f1,f1,f2               vy += g
                0x46010001u,                                        // 10 sub.s f0,f0,f1               y −= vy
                0x46030036u,                                        // 11 c.le.s f0,f3                 y ≤ stop ?  (⚠ EE cond code 0x36 — the MIPS 0x3E "LE" is not one the R5900 FPU has, and read as a coin toss)
                0x00000000u,                                        // 12 nop
                0x45000000u | 3,                                    // 13 bc1f  store (+3 → index 17)
                0x240A0002u,                                        // 14   li  t2,2                   (both paths; only stored below)
                0x46001806u,                                        // 15 mov.s f0,f3                  y = stop
                0xAD0A0000u | Lo(CodeCaves.BladeFallFlag),          // 16 sw    t2,flag(t0)            landed
                0xE5000000u | Lo(CodeCaves.BladeFallY),             // 17 store: swc1 f0,y(t0)
                0xE5010000u | Lo(CodeCaves.BladeFallVy),            // 18 swc1  f1,vy(t0)
                0x3C0B0000u | shi,                                  // 19 lui   t3,HI(slot pos)
                0xE5600000u | SLo(4),                               // 20 swc1  f0,y(t3)               the copy's height, this frame
                MipsAsm.J(CodeCaves.DebugInfoCave.WhpBill),         // 21 j     WhpBill (the chain's tail, which returns through ra)
                0x00000000u,                                        // 22   nop
                0x240A0003u,                                        // 23 follow: li t2,3
                0x152A0000u | 15,                                   // 24 bne   t1,t2,ret (+15 → index 40)
                0x00000000u,                                        // 25   nop
                0x8D0A0000u | Lo(CodeCaves.BladeFallUnit),          // 26 lw    t2,unit(t0)            the followed unit's position (guest)
                0xC5400000u,                                        // 27 lwc1  f0,0x0(t2)             its x
                0xC5410008u,                                        // 28 lwc1  f1,0x8(t2)             its z
                0xC5020000u | Lo(CodeCaves.BladeFallY),             // 29 lwc1  f2,y(t0)               the height OVER the unit
                0xC5430004u,                                        // 30 lwc1  f3,0x4(t2)             the unit's own height (a flyer's rises)
                0x46031080u,                                        // 31 add.s f2,f2,f3
                0xC5030000u | Lo(CodeCaves.BladeFallOffX),          // 32 lwc1  f3,offx(t0)            an x/z offset from the unit (0 over an enemy;
                0x46030000u,                                        // 33 add.s f0,f0,f3                ahead of Toan for the charge blade)
                0xC5030000u | Lo(CodeCaves.BladeFallOffZ),          // 34 lwc1  f3,offz(t0)
                0x46030840u,                                        // 35 add.s f1,f1,f3
                0x3C0B0000u | shi,                                  // 36 lui   t3,HI(slot pos)
                0xE5600000u | SLo(0),                               // 37 swc1  f0,x(t3)
                0xE5620000u | SLo(4),                               // 38 swc1  f2,y(t3)
                0xE5610000u | SLo(8),                               // 39 swc1  f1,z(t3)
                MipsAsm.J(CodeCaves.DebugInfoCave.WhpBill),         // 40 ret: j WhpBill (the chain's tail, which returns through ra)
                0x00000000u,                                        // 41   nop
            };
            if (cave + (uint)words.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The blade-fall cave does not fit its host (DebugInfomationDraw).");
            for (int i = 0; i < words.Length; i++) WrU32(fs, ElfOff(cave + (uint)(i * 4)), words[i]);
        }

        /// <summary>The WHP-BILL cave (see CodeCaves.WhpBill): the tail of the camera-pin chain, once a dungeon frame. A bill
        /// the mod has posted — the magic in place and a non-zero factor — is taken by the engine itself: the factor is
        /// zeroed (consumed before the call) and SwordDmgCheck1(factor, 0) is called, the very routine a landed hit calls,
        /// so the drain, its warnings, the Auto Repair Powder and the break are all the engine's own. The call is made
        /// from the camera pass's epilogue: ra is kept on a 16-byte frame of our own, and the pass returns nothing, so the
        /// caller-saved registers the call spends are nobody's. t0–t2, f1 and f12 are scratch; ends by returning through
        /// the pass's own ra.</summary>
        internal static void PatchWhpBill(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SwordDmgCheck1 = 0x01DB9B30;                                   // dun: SwordDmgCheck1__Ffi (float factor in f12, int whpCost in a0)
            uint cave = CodeCaves.DebugInfoCave.WhpBill, w = CodeCaves.WhpBillGuest;
            uint hi = w >> 16, lo = w & 0xFFFFu; if (lo >= 0x8000) hi += 1;          // signed offsets
            uint Lo(int o) => (lo + (uint)o) & 0xFFFFu;
            uint magic = CodeCaves.WhpBillMagicValue;
            uint[] words =
            {
                0x3C080000u | hi,                                   //  0 lui   t0,HI(bill)
                0x8D090000u | Lo(CodeCaves.WhpBillMagic),           //  1 lw    t1,magic(t0)
                0x3C0A0000u | (magic >> 16),                        //  2 lui   t2,HI(magic value)
                0x354A0000u | (magic & 0xFFFFu),                    //  3 ori   t2,t2,LO(magic value)
                0x152A0000u | 15,                                   //  4 bne   t1,t2,ret (+15 → index 20)   no magic: nothing is ours here
                0x00000000u,                                        //  5   nop
                0xC50C0000u | Lo(CodeCaves.WhpBillFactor),          //  6 lwc1  f12,factor(t0)              the bill, as SwordDmgCheck1's factor
                0x44800800u,                                        //  7 mtc1  zero,f1
                0x00000000u,                                        //  8 nop
                0x46016032u,                                        //  9 c.eq.s f12,f1                     nothing posted?
                0x00000000u,                                        // 10 nop
                0x45010000u | 8,                                    // 11 bc1t  ret (+8 → index 20)
                0x00000000u,                                        // 12   nop
                0x27BDFFF0u,                                        // 13 addiu sp,sp,-0x10                a frame of our own for ra
                0xAFBF0000u,                                        // 14 sw    ra,0x0(sp)
                0xAD000000u | Lo(CodeCaves.WhpBillFactor),          // 15 sw    zero,factor(t0)            consumed before the call
                MipsAsm.Jal(SwordDmgCheck1),                        // 16 jal   SwordDmgCheck1
                0x00002021u,                                        // 17   addu a0,zero,zero              whpCost 0 (no monster's)
                0x8FBF0000u,                                        // 18 lw    ra,0x0(sp)
                0x27BD0010u,                                        // 19 addiu sp,sp,0x10
                0x03E00008u,                                        // 20 ret: jr ra
                0x00000000u,                                        // 21   nop
            };
            if (cave + (uint)words.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The WHP-bill cave does not fit its host (DebugInfomationDraw).");
            for (int i = 0; i < words.Length; i++) WrU32(fs, ElfOff(cave + (uint)(i * 4)), words[i]);
        }

        /// <summary>The LUNGE GRAVITY caves (see CodeCaves.DebugInfoCave.LungeGravitySeed) and the main-ELF hook. The charge
        /// lunge's parabola is seeded in ToanKey_Play — `lwc1 f12,-0x7f80(gp)` (the shared 0.1) then `jal
        /// ParabolicInitialVector` (0x242E00) — and its vertical speed loses that same 0.1 every frame in the dungeon key
        /// process (dun 0x1DB3638: `lwc1 f0,-0x7f80(gp); sub.s f0,f2,f0`, hooked by DunPatches). Each cave loads the 0.1
        /// itself, scales it by (1 + CodeCaves.LungeGravityExtra) and hands the result on: the seed cave tail-jumps into
        /// ParabolicInitialVector with f12 = g (the `jal` is retargeted to the cave, so the callee returns to the key
        /// handler as before); the step cave returns with the displaced subtraction in its delay slot. f1 is a dead
        /// temporary at both sites; at is the assembler's.</summary>
        internal static void PatchLungeGravity(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Parabolic = 0x001D4080, SeedHook = 0x00242E00;
            uint word = CodeCaves.LungeGravityExtraGuest;
            uint hi = word >> 16, lo = word & 0xFFFFu; if (lo >= 0x8000) hi += 1;    // lwc1's offset is signed
            uint[] seed =
            {
                0x3C010000u | hi,                                   // lui   at,HI(extra)
                0xC4210000u | lo,                                   // lwc1  f1,LO(at)
                0xC78C8080u,                                        // lwc1  f12,-0x7f80(gp)        the shared 0.1
                0x460C0842u,                                        // mul.s f1,f1,f12              0.1 × extra
                MipsAsm.J(Parabolic),                               // j     ParabolicInitialVector
                0x46016300u,                                        //   add.s f12,f12,f1           g = 0.1 × (1 + extra)
            };
            uint[] step =
            {
                0x3C010000u | hi,                                   // lui   at,HI(extra)
                0xC4210000u | lo,                                   // lwc1  f1,LO(at)
                0xC7808080u,                                        // lwc1  f0,-0x7f80(gp)         the shared 0.1
                0x46000842u,                                        // mul.s f1,f1,f0               0.1 × extra
                0x46010000u,                                        // add.s f0,f0,f1               g = 0.1 × (1 + extra)
                0x03E00008u,                                        // jr    ra
                0x46001001u,                                        //   sub.s f0,f2,f0             vy −= g (the displaced op)
            };
            uint seedAt = CodeCaves.DebugInfoCave.LungeGravitySeed, stepAt = CodeCaves.DebugInfoCave.LungeGravityStep;
            if (stepAt + (uint)step.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The lunge-gravity caves do not fit their host (DebugInfomationDraw).");
            for (int i = 0; i < seed.Length; i++) WrU32(fs, ElfOff(seedAt + (uint)(i * 4)), seed[i]);
            for (int i = 0; i < step.Length; i++) WrU32(fs, ElfOff(stepAt + (uint)(i * 4)), step[i]);
            uint was = RdU32(fs, ElfOff(SeedHook));
            if (was != MipsAsm.Jal(Parabolic) && was != MipsAsm.Jal(seedAt))
                throw new IOException($"ToanKey_Play's parabola call is not where expected (0x{was:X8} at 0x{SeedHook:X})");
            WrU32(fs, ElfOff(SeedHook), MipsAsm.Jal(seedAt));
        }

        /// <summary>Xiao's build-up tree, baked: the weapon template table's build-up word (WeaponList +0x3C, bit k = the weapon
        /// 299 + k may be built up into) — Hardshooter → Double Impact alone (vanilla: Double Impact or Matador), Double Impact →
        /// Matador alone (vanilla: Divine Beast Title).</summary>
        /// <summary>Toan's two CHARGE-ATTACK hit radii become data. ToanKey_Play bakes each into its own
        /// instruction pair before handing it to CCollisionData::Set as the sphere radius:
        /// <code>
        ///   0x241AC0  lui v0,0x40C0 ; mtc1 v0,f12   →  6.0, then `li a0,2` — the LUNGE (attack kind 2)
        ///   0x241B90  lui v0,0x4140 ; mtc1 v0,f12   → 12.0, then `li a0,3` — the WHIRLWIND (attack kind 3)
        /// </code>
        /// Each pair is rewritten to load from <see cref="CodeCaves.ChargeHitRadius"/> instead, exactly as
        /// PatchFishingCameraHeight does for the camera's baked 40.0:
        /// <code>
        ///   lui  $2,HI(slot)
        ///   lwc1 $f12,LO(slot)($2)
        /// </code>
        /// What that buys: an ability can resize the ENGINE'S OWN charge hit and let the engine do the rest —
        /// damage, victims, knockback, one plant per frame — instead of watching for the hit from a mod tick and
        /// planting its own spheres, which is timing-sensitive and was repeatedly wrong. ⚠ The words are read on
        /// every charge swing, so the mod seeds both at startup; 0 would be a hit radius of nothing.</summary>
        internal static void PatchChargeHitRadius(FileStream fs, Func<uint, long> ElfOff)
        {
            PatchF12Site(fs, ElfOff, 0x00241AC0, 0x3C0240C0,
                         CodeCaves.ChargeHitRadiusGuest + CodeCaves.ChargeRadiusLunge, "lunge hit-radius");
            PatchF12Site(fs, ElfOff, 0x00241B90, 0x3C024140,
                         CodeCaves.ChargeHitRadiusGuest + CodeCaves.ChargeRadiusWhirl, "whirlwind hit-radius");
        }

        /// <summary>Toan's five baked melee KICK STRENGTHS become data (CodeCaves.MeleeKickWords). ToanKey_Play plants each
        /// hit's kick with `lui v0,IMM; mtc1 v0,f12` ahead of `jal SetKickBack` — combo hit 3 (1.5 at 0x2418D8), hit 4
        /// (2.0 at 0x241978), hit 5 (3.0 at 0x241A38), the lunge (3.0 at 0x241B04) and the whirlwind (3.0 at 0x241BD4);
        /// hits 1 and 2 already read a word. Each pair becomes `lui v0,HI; lwc1 f12,LO(v0)` of its own word, so a sword
        /// can set the knockback of EVERY hit (MeleeKick), pnach-seeded to the vanilla figures otherwise.</summary>
        internal static void PatchMeleeKickStrength(FileStream fs, Func<uint, long> ElfOff)
        {
            uint w = CodeCaves.MeleeKickWordsGuest;
            PatchF12Site(fs, ElfOff, 0x002418D8, 0x3C023FC0, w + (uint)CodeCaves.MeleeKickHit3,  "combo hit 3 kick");
            PatchF12Site(fs, ElfOff, 0x00241978, 0x3C024000, w + (uint)CodeCaves.MeleeKickHit4,  "combo hit 4 kick");
            PatchF12Site(fs, ElfOff, 0x00241A38, 0x3C024040, w + (uint)CodeCaves.MeleeKickHit5,  "combo hit 5 kick");
            PatchF12Site(fs, ElfOff, 0x00241B04, 0x3C024040, w + (uint)CodeCaves.MeleeKickLunge, "lunge kick");
            PatchF12Site(fs, ElfOff, 0x00241BD4, 0x3C024040, w + (uint)CodeCaves.MeleeKickWhirl, "whirlwind kick");
        }

        /// <summary>THE MAGIC CIRCLES as data (tools/stubs/circle_effects.s, CodeCaves.CircleTable): the cave takes over the body
        /// of DebugInfomationIF (CodeCaves.DebugIfCave) — its first two words become `jr ra; li v0,0`, so the debug key's one
        /// call reads "nothing pressed" — and dun.bin's Run_TrapCircle jumps to it (DunPatches). Every circle magnitude is then
        /// a word the mod may set.</summary>
        internal static void PatchCircleEffects(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint Host = CodeCaves.DebugIfCave.Host, Cave = CodeCaves.DebugIfCave.CircleEffects;
            using var st = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dark_Cloud_Improved_Version.Resources.isoPatch.circleEffects.bin")
                ?? throw new IOException("Embedded EE function missing: circleEffects.bin (run tools/stubs/build_ee_stubs.py and rebuild)");
            using var ms = new MemoryStream(); st.CopyTo(ms); byte[] b = ms.ToArray();
            // Shape: the engine's own frame, and the calls it makes — BtSetStatusErr, WeaponDataChangeByRGate, SetWeaponAttachStatus, rand, GetItem, SndSePlay.
            bool statusErr = false, rgate = false, attach = false, rnd = false, getItem = false, se = false;
            for (int i = 0; i + 4 <= b.Length; i += 4)
            {
                uint w = U32(b, i);
                if (w == Jal(0x001B1BB0)) statusErr = true; if (w == Jal(0x0020FCE0)) rgate = true; if (w == Jal(0x00225AA0)) attach = true;
                if (w == Jal(0x001046F8)) rnd = true;       if (w == Jal(0x001BE060)) getItem = true; if (w == Jal(0x0015A6B0)) se = true;
            }
            if (b.Length % 4 != 0 || b.Length < 0x200 || U32(b, 0) != 0x27BDFF70u || !statusErr || !rgate || !attach || !rnd || !getItem || !se)
                throw new IOException($"circleEffects.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (Cave + (uint)b.Length > Host + CodeCaves.DebugIfCave.HostSpan)
                throw new IOException("circleEffects.bin overruns DebugInfomationIF's span.");
            uint w0 = RdU32(fs, ElfOff(Host));
            if (w0 != CodeCaves.DebugIfCave.VanillaWord0 && w0 != 0x03E00008u)
                throw new IOException($"DebugInfomationIF at 0x{Host:X} is not vanilla (`addiu sp,sp,-0x20`) — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(Host),     0x03E00008u);                            // jr   ra
            WrU32(fs, ElfOff(Host + 4), 0x24020000u);                            //   addiu v0,zero,0 — "nothing pressed" to the debug key's caller
            for (int i = 0; i < b.Length; i += 4) WrU32(fs, ElfOff(Cave + (uint)i), U32(b, i));
        }

        /// <summary>An immediate float handed over in f12 (`lui v0,IMM; mtc1 v0,f12`, v0 dead after) becomes a load of
        /// the data word at <paramref name="slot"/> (`lui v0,HI; lwc1 f12,LO(v0)`).</summary>
        private static void PatchF12Site(FileStream fs, Func<uint, long> ElfOff, uint luiAddr, uint vanillaLui,
                                         uint slot, string what)
        {
            const uint VanillaMtc1 = 0x44826000;                 // mtc1 $2,$f12
            uint mtc1Addr = luiAddr + 4;
            uint gotLui = RdU32(fs, ElfOff(luiAddr)), gotMtc1 = RdU32(fs, ElfOff(mtc1Addr));
            uint hi = slot >> 16, lo = slot & 0xFFFF;
            if (lo >= 0x8000) hi += 1;                           // lwc1's offset is SIGNED — compensate like the assembler
            uint wantLui = 0x3C020000u | hi;
            uint wantLwc1 = 0xC4000000u | (2u << 21) | (12u << 16) | lo;
            if (gotLui == wantLui && gotMtc1 == wantLwc1) return;                 // idempotent re-run
            if (gotLui != vanillaLui || gotMtc1 != VanillaMtc1)
                throw new IOException($"Toan's {what} site 0x{luiAddr:X} is not vanilla " +
                                      $"(got 0x{gotLui:X8}/0x{gotMtc1:X8}) — is this an unmodified Dark Cloud (USA) ISO?");
            WrU32(fs, ElfOff(luiAddr),  wantLui);
            WrU32(fs, ElfOff(mtc1Addr), wantLwc1);
        }

        /// <summary>Item-bomb explosions take their hit REACTION from data. SetBombEffect (0x1D5940) builds its
        /// collision entry with the reaction baked in as the eighth argument:
        /// <code>
        ///   0x1D5A14  li   t1,0x3        ← the unguardable knockdown
        ///   0x1D5A20  jal  CCollisionData::Set
        ///   0x1D5A24  nop                ← the call's delay slot, unused
        /// </code>
        /// The literal becomes a load of <see cref="CodeCaves.BombReaction"/>, split across the `li` slot and the
        /// delay slot — which runs BEFORE the call, so t1 is loaded in time and no code has to move:
        /// <code>
        ///   lui t1,HI(BombReaction)  /  jal Set  /  lw t1,LO(BombReaction)(t1)
        /// </code>
        /// Bombs are the one explosion source that does not read a shot config, so this is what lets an ability
        /// cover chest traps, thrown bombs and Halloween's pumpkin alongside the self-destructs.</summary>
        internal static void PatchBombReaction(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint LiAddr = 0x001D5A14, SlotAddr = 0x001D5A24;
            const uint VanillaLi = 0x24090003, VanillaSlot = 0x00000000;   // li t1,3 ; nop
            uint slot = CodeCaves.BombReactionGuest;
            uint hi = slot >> 16, lo = slot & 0xFFFF;
            if (lo >= 0x8000) hi += 1;                                     // lw's offset is SIGNED
            uint wantLui = 0x3C090000u | hi;                               // lui t1,hi
            uint wantLw  = 0x8D290000u | lo;                               // lw  t1,lo(t1)
            uint gotLi = RdU32(fs, ElfOff(LiAddr)), gotSlot = RdU32(fs, ElfOff(SlotAddr));
            if (gotLi == wantLui && gotSlot == wantLw) return;             // idempotent re-run
            if (gotLi != VanillaLi || gotSlot != VanillaSlot)
                throw new IOException($"Item-bomb reaction site 0x{LiAddr:X} is not vanilla `li t1,3` + nop " +
                                      $"(got 0x{gotLi:X8}/0x{gotSlot:X8}) — is this an unmodified Dark Cloud (USA) ISO?");
            WrU32(fs, ElfOff(LiAddr),   wantLui);
            WrU32(fs, ElfOff(SlotAddr), wantLw);
        }

        /// <summary>AUTO-GUARD: a hit carrying reaction 5 is answered with a guard spark and otherwise IGNORED — the
        /// player's damage handler never processes it. The hook is at BtCheckDamageProc's CheckHitUser return, which is
        /// the only place the whole thing is gated on:
        /// <code>
        ///   0x1DBB0D8  jal  CheckHitUser
        ///   0x1DBB0E0  move s0,v0          →  jal AutoGuardMatch
        ///   0x1DBB0E4  addiu v0,zero,-1    →  move s0,v0     (the call's delay slot, where v0 is still the index)
        ///   0x1DBB0E8  beq  s0,v0 → skip everything
        /// </code>
        /// The cave returns v0 = −1 (what the displaced instruction set) and, for a reaction-5 entry, s0 = −1 as well,
        /// so the engine's own `beq` takes it straight to the end. It consumes the entry first and ticks
        /// <see cref="CodeCaves.AutoGuardSignal"/> with the position, which is how the mod knows to answer with rumble,
        /// a sound and a flinch — presentation is far easier to tune in C# than in a cave.
        ///
        /// ⚠ It has to intercept HERE rather than at the reaction dispatch. Before the handler looks at a reaction at
        /// all, it zeroes the player's action word (0x1DC4490, read by ToanKey_Play at 0x24146C) — so a hit that did
        /// nothing still cancelled whatever Toan was doing, which is a charge attack lost to a bomb that cannot hurt
        /// him. By the dispatch the old value is already gone.</summary>
        internal static void PatchAutoGuardMatch(FileStream fs, Func<uint, long> ElfOff)
        {
            uint[] words =
            {
                0x2401FFFF,   // addiu at,zero,-1
                0x10410018,   // beq   v0,at,done          nothing was hit
                0x00000000,   // nop
                0x8F889DF0,   // lw    t0,0x9DF0(gp)       NowColData
                0x00024880,   // sll   t1,v0,2
                0x01224821,   // addu  t1,t1,v0
                0x00094940,   // sll   t1,t1,5             index × 0xA0
                0x01094821,   // addu  t1,t0,t1            the entry
                0x8D2A004C,   // lw    t2,0x4C(t1)         its reaction
                0x240B0005,   // addiu t3,zero,5
                0x154B000F,   // bne   t2,t3,done          not ours: vanilla flow
                0x00000000,   // nop
                0x00025080,   // sll   t2,v0,2
                0x010A5021,   // addu  t2,t0,t2
                0xAD403C00,   // sw    zero,0x3C00(t2)     consume the entry
                0x3C0C01FB,   // lui   t4,0x1FB            the signal block
                0x8D8DF840,   // lw    t5,-0x7C0(t4)       its counter
                0x25AD0001,   // addiu t5,t5,1
                0xAD8DF840,   // sw    t5,-0x7C0(t4)       …ticked, for the mod to notice
                0x8D2D0000,   // lw    t5,0x0(t1)
                0xAD8DF844,   // sw    t5,-0x7BC(t4)       …and where it happened
                0x8D2D0004,   // lw    t5,0x4(t1)
                0xAD8DF848,   // sw    t5,-0x7B8(t4)
                0x8D2D0008,   // lw    t5,0x8(t1)
                0xAD8DF84C,   // sw    t5,-0x7B4(t4)
                0x2410FFFF,   // addiu s0,zero,-1          report: nothing was hit
                0x03E00008,   // jr    ra            done:
                0x2402FFFF,   // addiu v0,zero,-1          what the displaced instruction set
            };
            uint at0 = CodeCaves.DebugInfoCave.AutoGuardMatch;
            if (at0 + words.Length * 4 > CodeCaves.DebugInfoCave.Host + CodeCaves.DebugInfoCave.HostSpan)
                throw new IOException("The auto-guard cave does not fit its host (DebugInfomationDraw).");
            for (int i = 0; i < words.Length; i++)
                WrU32(fs, ElfOff(at0 + (uint)(i * 4)), words[i]);
        }

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
