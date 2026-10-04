using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The player-pellet and monster-shot-pack ELF patches: the Matador's prop-on-a-pellet follower, the borrowed-shots
    /// keeper that enters a species' config into a floor's pack, the shot-slot sharing cave in DebugInfomationDraw's body, and a
    /// player pellet's sprite cell, contact plant and contact record. Called in order from ElfPatches.ElfPatchAndCrc (the dead
    /// hosts are claimed first by ElfDeadFunctionPatches).</summary>
    internal static class ElfShotPackPatches
    {
        /// <summary>The prop-on-a-pellet cave (tools/stubs/prop_pellet_follow.s): the cat follower hook's new first stop. It must
        /// call CatCopyQueue (the cat's chain performs the displaced step__5CSHOT) and read the shot pool.</summary>
        internal static void PatchPropPelletFollow(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = ElfCave.PropPelletFollow;
            byte[] b = Embedded("propPelletFollow.bin");
            bool chained = ContainsWord(b, Jal(ElfCave.CatCopyQueue)), pool = ContainsWord(b, 0x8D4A35D4u);
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFE0u || !chained || !pool)
                throw new IOException("propPelletFollow.bin malformed or stale — it must call CatCopyQueue and read the shot pool.");
            WriteBytes(fs, ElfOff, CaveAddr, b, 0x01FB2000u, "propPelletFollow.bin overruns its gap — it must end before 0x1FB2000 (the first band's end).");
        }

        /// <summary>The borrowed-shots cave (tools/stubs/borrowed_shots_enter.s): the head of the step chain — must call
        /// PropPelletFollow (the rest of the chain) and Entry__17CSHOT_EFFECT_PACK, and read NowShotEffect.</summary>
        internal static void PatchBorrowedShotsEnter(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint CaveAddr = ElfCave.BorrowedShotsEnter;
            byte[] b = Embedded("borrowedShotsEnter.bin");
            bool chainCall = ContainsWord(b, Jal(ElfCave.PropPelletFollow));
            if (b.Length < 8 || U32(b, 0) != 0x27BDFFE0u || !chainCall)
                throw new IOException("borrowedShotsEnter.bin (the head) malformed or stale — it must call PropPelletFollow.");
            WriteBytes(fs, ElfOff, CaveAddr, b, 0x01FB2000u, "borrowedShotsEnter.bin (the head) overruns its gap — it must end by 0x1FB2000.");
            // …and the tail piece, at the band's end
            byte[] t = Embedded("borrowedShotsEnterTail.bin");
            const uint TailAddr = ElfCave.BorrowedShotsEnterTail;
            bool jrRa = ContainsWord(t, 0x03E00008u);
            if (t.Length < 8 || t.Length % 4 != 0 || !jrRa)
                throw new IOException("borrowedShotsEnterTail.bin malformed or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, TailAddr, t, ElfCave.RegionEnd, "borrowedShotsEnterTail.bin overruns the band — it must end by ElfCave.RegionEnd.");
        }

        /// <summary>The monster shot pack's five slots shared among every shot config a floor needs (tools/stubs/shared_shots.s,
        /// hosted in the dead DebugInfomationDraw — claimed by ElfDeadFunctionPatches.PatchClaimDeadFunctionHosts, so its
        /// debug-flag caller returns at once): the species loader's two pack calls store a refused config's negative form in the
        /// species row, Step's two fire sites acquire a config that is not in the pack before firing, and the dungeon step loop's
        /// chain head (DunPatches.CatFollowHookNew) preloads one a frame while a slot is free.</summary>
        internal static void PatchSharedShots(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] b = Embedded("sharedShots.bin");
            // Shape: opens with the four-entry branch table, calls the keeper (its chain), Entry__17, Initialize__12 and Entry__12.
            bool hasKeeper = ContainsWord(b, Jal(ElfCave.BorrowedShotsEnter)), hasEntry17 = ContainsWord(b, Jal(0x001AE4C0));
            bool hasInit = ContainsWord(b, Jal(0x001AE440)), hasEntry12 = ContainsWord(b, Jal(0x001ACC70));
            if (b.Length % 4 != 0 || b.Length < 0x40 || (U32(b, 0) >> 16) != 0x1000 || (U32(b, 8) >> 16) != 0x1000 || !hasKeeper || !hasEntry17 || !hasInit || !hasEntry12)
                throw new IOException($"sharedShots.bin malformed ({b.Length} B) or stale — reassemble its .s.");
            if (DebugInfoCave.SharedShots + (uint)b.Length > DebugInfoCave.PelletSprite)
                throw new IOException("sharedShots.bin overruns into the pellet-sprite cave that follows it in DebugInfomationDraw.");
            WriteBytes(fs, ElfOff, DebugInfoCave.SharedShots, b);
            // SetupBaseModel's two pack calls: `jal Entry__17CSHOT_EFFECT_PACK; nop; addiu v1,zero,-1`
            uint enter = Jal(DebugInfoCave.SharedShotsEnter);
            foreach (uint site in new[] { 0x001E01B0u, 0x001E0224u })
            {
                uint cur = RdU32(fs, ElfOff(site));
                if ((cur != Jal(0x001AE4C0) && cur != enter) || RdU32(fs, ElfOff(site + 4)) != 0 || RdU32(fs, ElfOff(site + 8)) != 0x2403FFFFu)
                    throw new IOException($"Species-loader pack call at 0x{site:X} is not vanilla `jal Entry__17CSHOT_EFFECT_PACK; nop; addiu v1,zero,-1` — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), enter);
            }
            // Step's two fire sites: the `lui at,0x6` before the request load; its delay slot, the vanilla `addu at,a0,at`, stays
            // (the cave re-forms at = a0 + 0x60000 before returning to the load)
            foreach (var (site, entry, load) in new[] { (0x001DEED0u, DebugInfoCave.SharedShotsFire0, 0x8C23FF74u), (0x001DEFD8u, DebugInfoCave.SharedShotsFire1, 0x8C230274u) })
            {
                uint cur = RdU32(fs, ElfOff(site)), ours = Jal(entry);
                if ((cur != 0x3C010006u && cur != ours) || RdU32(fs, ElfOff(site + 4)) != 0x00810821u || RdU32(fs, ElfOff(site + 8)) != load || RdU32(fs, ElfOff(site + 12)) != 0x24020002u)
                    throw new IOException($"Monster fire site at 0x{site:X} is not vanilla `lui at,0x6; addu at,a0,at; lw v1,…(at); addiu v0,zero,2` — unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, ElfOff(site), ours);
            }
        }

        /// <summary>A player pellet's sprite cell (draw__5CSHOT's hook at 0x1ABC74 calls the cave for the item id the cell is
        /// taken from, in v0): Mailbox.PelletSpriteId when it is set, else the equipped weapon's. Eight words at
        /// DebugIfCave.PelletSprite; `at` and v0 are the only registers touched. (The sheet's bottom row is the mod's —
        /// PelletSheetBakes: cell 315 transparent, 314 free, 312/313 Super Steve's and Angel Gear's own.)</summary>
        internal static void PatchPelletSprite(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x001ABC74;
            uint cave = DebugIfCave.PelletSprite;
            HiLo((uint)(Mailbox.PelletSpriteId - 0x20000000L), out uint shi, out uint slo);
            uint[] words =
            {
                0x3C010000u | shi,                                  //  0 lui   at,HI(PelletSpriteId)
                0x8C220000u | slo,                                  //  1 lw    v0,LO(at)                     the mod's id (0 = the weapon's)
                0x14400000u | 3,                                    //  2 bne   v0,zero,ret (+3 → index 6)
                0x00000000u,                                        //  3   nop
                0x8F829D04u,                                        //  4 lw    v0,-0x62FC(gp)                NowWeaponHave
                0x84420000u,                                        //  5 lh    v0,0(v0)                      its item id
                0x03E00008u,                                        //  6 ret: jr ra
                0x00000000u,                                        //  7   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugIfCave.Host + DebugIfCave.HostSpan, "The pellet-sprite cave does not fit its host (DebugInfomationIF).");
            for (int i = words.Length; i < 14; i++) WrU32(fs, ElfOff(cave + (uint)(i * 4)), 0);   // the span an earlier build used
            uint cur0 = RdU32(fs, ElfOff(HookAddr)), cur1 = RdU32(fs, ElfOff(HookAddr + 4)), ours = Jal(cave), older = Jal(DebugInfoCave.PelletSprite);
            bool vanilla = cur0 == 0x8F829D04u && cur1 == 0x84420000u, patched = (cur0 == ours || cur0 == older) && cur1 == 0;
            if (!(vanilla || patched) || RdU32(fs, ElfOff(HookAddr + 8)) != 0x2443FED5u)
                throw new IOException($"Pellet draw site 0x{HookAddr:X} is not vanilla `lw v0,-0x62FC(gp); lh v0,0(v0); addiu v1,v0,-0x12B` — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(HookAddr), ours);
            WrU32(fs, ElfOff(HookAddr + 4), 0);
        }

        /// <summary>A player pellet's contact, with a way to make it hurt nothing. step__5CSHOT, on a pellet's contact
        /// (checkCollision, radius 2 + the part's), calls <c>Set__14CCollisionData(3.0, 0, NowColData, pos, damage, 1, 2, 2, 0, 0)</c>
        /// — the damage from the pellet's own word (pool +0x2E0) in a2 — then fills the planted entry in (owner 1, element, ability
        /// flags, anti bytes), retires the pellet (<c>sw zero,0(s0)</c>: s0 = its active word) and falls into the loop's tail at
        /// 0x1ABEBC, which needs only s0/s1/s2/s4 — all untouched by the call. The call becomes `jal PelletPlant`:
        /// <code>
        ///   bltz a2,nodmg / nop / j Set__14CCollisionData / nop      a pellet with a damage word ≥ 0: the native plant, returning into the fill-in
        ///   nodmg: sw zero,0(s0) / j 0x1ABEBC / nop                    a NEGATIVE damage word: nothing planted (no fill-in either — it would
        ///                                                              stamp whichever entry the pool last set), the pellet ended, the tail
        /// </code>
        /// Vanilla never writes a negative pellet damage, so an unmarked pellet is untouched; the mod marks a pellet with −1
        /// (ZeusShot) and the contact still ends it on the engine's own frame.</summary>
        internal static void PatchPelletPlant(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x001ABE04, SetCollision = 0x001B57A0, LoopTail = 0x001ABEBC;
            uint cave = DebugIfCave.PelletPlant;
            uint[] words =
            {
                0x04C00003u,        // 0 bltz  a2,nodmg (+3 → index 4)
                0x00000000u,        // 1   nop
                J(SetCollision),    // 2 j     Set__14CCollisionData         the native plant; ra still points into step__5CSHOT
                0x00000000u,        // 3   nop
                0xAE000000u,        // 4 nodmg: sw zero,0(s0)                 the pellet retired, as the native path does after its fill-in
                J(LoopTail),        // 5 j     0x1ABEBC                       the loop's tail: its lifetime count, the next slot
                0x00000000u,        // 6   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugIfCave.Host + DebugIfCave.HostSpan, "The pellet-plant cave does not fit its host (DebugInfomationIF).");
            uint cur = RdU32(fs, ElfOff(HookAddr)), ours = Jal(cave);
            if (cur != Jal(SetCollision) && cur != ours)
                throw new IOException($"Pellet plant site 0x{HookAddr:X} is not vanilla `jal Set__14CCollisionData` — unmodified Dark Cloud (USA) ISO expected.");
            if (RdU32(fs, ElfOff(HookAddr + 4)) != 0 || RdU32(fs, ElfOff(0x001ABEB8)) != 0xAE000000u || RdU32(fs, ElfOff(LoopTail)) != 0x02912021u)
                throw new IOException("step__5CSHOT is not laid out as expected around its plant (delay slot, retire store, loop tail).");
            WrU32(fs, ElfOff(HookAddr), ours);
        }

        /// <summary>Every player pellet contact recorded for the mod. step__5CSHOT tests each live pellet with
        /// <c>checkCollision(2.0, out, pos, dir, 2)</c> (0x1ABD88; out = its frame's sp+0x70) — 3 when a hit sphere of an enemy is
        /// within 2 + its radius (the sphere's centre copied to out), 1 when a wall is (the wall point), 0 otherwise — and only
        /// then plants and retires the pellet. The call becomes `jal PelletContact`, which makes the call and, on a non-zero
        /// result, writes CodeCaves.PelletContact: the counter stepped, the slot (s2 — the loop's index), the result and the out
        /// point; v0 and the caller's saved registers are untouched. The mod polls the counter: a contact is known the tick after
        /// the engine's own frame, with the enemy named by its sphere.</summary>
        internal static void PatchPelletContact(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint HookAddr = 0x001ABD88, CheckCollision = 0x001AB740;
            uint cave = DebugIfCave.PelletContact;
            HiLo(CodeCaves.PelletContactGuest, out uint bhi, out uint blo);   // lui/offset pair: the block is below the 0x01FB0000 page, so the offsets are negative
            uint Off(int field) => (blo + (uint)field) & 0xFFFFu;
            uint[] words =
            {
                0x27BDFFF0u,                                    //  0 addiu sp,sp,-0x10
                0xAFBF0000u,                                    //  1 sw    ra,0(sp)
                Jal(CheckCollision),                            //  2 jal   checkCollision                 a0..a3 straight through
                0x00000000u,                                    //  3   nop
                0x8FBF0000u,                                    //  4 lw    ra,0(sp)
                0x27BD0010u,                                    //  5 addiu sp,sp,0x10                     the caller's frame is sp again: its out vector is at 0x70(sp)
                0x10400000u | 13,                               //  6 beq   v0,zero,done (+13 → index 20)
                0x00000000u,                                    //  7   nop
                0x3C080000u | bhi,                              //  8 lui   t0,HI(PelletContact)
                0x8D090000u | Off(CodeCaves.ContactCounter),    //  9 lw    t1,counter(t0)
                0x25290001u,                                    // 10 addiu t1,t1,1
                0xAD090000u | Off(CodeCaves.ContactCounter),    // 11 sw    t1,counter(t0)
                0xAD120000u | Off(CodeCaves.ContactSlot),       // 12 sw    s2,slot(t0)                    the pellet's pool slot
                0xAD020000u | Off(CodeCaves.ContactKind),       // 13 sw    v0,kind(t0)                    3 an enemy, 1 a wall
                0x8FAA0070u,                                    // 14 lw    t2,0x70(sp)                    the point: x
                0xAD0A0000u | Off(CodeCaves.ContactX),          // 15 sw    t2,x(t0)
                0x8FAA0074u,                                    // 16 lw    t2,0x74(sp)                    h
                0xAD0A0000u | Off(CodeCaves.ContactH),          // 17 sw    t2,h(t0)
                0x8FAA0078u,                                    // 18 lw    t2,0x78(sp)                    y
                0xAD0A0000u | Off(CodeCaves.ContactY),          // 19 sw    t2,y(t0)
                0x03E00008u,                                    // 20 done: jr ra
                0x00000000u,                                    // 21   nop
            };
            WriteWords(fs, ElfOff, cave, words, DebugIfCave.Host + DebugIfCave.HostSpan, "The pellet-contact cave does not fit its host (DebugInfomationIF).");
            uint cur = RdU32(fs, ElfOff(HookAddr)), ours = Jal(cave);
            if (cur != Jal(CheckCollision) && cur != ours)
                throw new IOException($"Pellet contact site 0x{HookAddr:X} is not vanilla `jal checkCollision` — unmodified Dark Cloud (USA) ISO expected.");
            if (RdU32(fs, ElfOff(HookAddr + 4)) != 0 || RdU32(fs, ElfOff(0x001ABD78)) != 0x27A40070u)   // its delay slot; `addiu a0,sp,0x70`: the out vector
                throw new IOException("step__5CSHOT is not laid out as expected around its contact test.");
            WrU32(fs, ElfOff(HookAddr), ours);
        }
    }
}
