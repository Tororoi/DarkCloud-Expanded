using static Dark_Cloud_Improved_Version.FishingLabelIds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.IsoPatcher;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// event.stb label bakes for the custom fishing towns: append empty, pre-numbered spare labels
    /// (ExtendStb) so the runtime installer always has room, and bake one real script label
    /// (BakeStbLabel / BuildDockSpawnCode) for towns the installer never visits. Pure byte[] -> byte[];
    /// IsoPatcher.ApplySignPatch redirects the results.
    /// </summary>
    internal static class StbLabelBaker
    {
        // ── event.stb: append spare labels so the runtime fishing installer never runs out of room ──
        //
        // Vanilla fishing towns host the minigame inside their event.stb (enter woven into label 256, plus
        // dedicated labels 133/134). Custom fishing towns have no such labels, so the mod builds its scripts
        // at runtime by hijacking the town's few spare labels — and Queens/Yellow Drops simply don't have
        // enough, so quit/bait couldn't install. We give every custom fishing town its own spare pool by
        // appending empty labels to its event.stb (baked into the ISO, exactly how vanilla ships fishing in
        // the STB). The label table has zero slack (it ends right at codeBase), so we RELOCATE it: append the
        // new label-code space, then a grown copy of the table (originals unchanged + new entries), and point
        // the header at it. Original labels keep their absolute codeOffsets since the original code never moves.
        internal static readonly string[] FishingTownStbPaths =
            { "gedit/e03/event.stb", "gedit/s13/event.stb", "gedit/s04/event.stb" };   // Queens, Yellow Drops, Brownboo
        // A custom fishing spot installs exactly FOUR scripts. We bake exactly four spare labels — one per
        // script — carrying their FINAL ids and sized (measured ScriptByteSize() + margin) to hold that script in a
        // SINGLE label. The mod's install claims each by id and writes straight into it: no runtime renumber,
        // and nothing spills across two labels (the old "arena run" that retired a second label). The three
        // custom fishing towns have no native 133/134/400/9600, so these ids are collision-free.
        // Ids come straight from FishingLabelIds / EventPoints — the same constants the runtime installer
        // claims, so bake and install cannot drift. Sizes measured 2026-07-24:
        // menu 1780, enter 1994, quit 892, bait 436. Label 401 = the Queens canal-floor per-sign script (its own
        // stance) — baked in every town (unused in Brownboo/Yellow Drops, harmless).
        internal static readonly int[] FishingSpareLabelIds   =
            { MenuSubLabelId, FishingLabelId, CanalFishingLabelId, EventPoints.FishingExitLabel,
              EventPoints.FishingBaitLabel, LadderMsgLabelId, CanalWarpLabelId };   // menu, enter, canal-enter, quit, bait, ladder-msg, tide-evict
        internal static readonly int[] FishingSpareLabelSizes = { 0x800, 0xA00, 0xA00, 0x500, 0x300, 0x300, 0x100 };               // one size per id, same order
        // ↑ labels 402 (ladder tide-message) + 403 (tide-evict _MAP_JUMP) baked into every fishing town's stb
        //   (unused outside Queens, harmless — like 401); CustomFishingSpot installs them in Queens only.

        // ── ally-swap spare label: DEDICATED, not part of the fishing pool ──
        // AllySwapLabelId (405) is what the in-place town ally swap (AllySwapPrototype) fires. It used to ride the
        // fishing pool, so it only existed in the 3 fishing towns — the swap silently did nothing everywhere else
        // (Norune/Spirit Tree have NO native spare, so even the runtime hijack fallback can't rescue them). It is
        // now baked as its OWN label into every walkable town below, decoupled from fishing (so ally-swap changes
        // can never regress the fishing labels, and vice-versa). Fishing towns get 405 PLUS the fishing pool in a
        // single table-grow (SpareLabelsFor); every other town gets 405 alone.
        internal const int AllySwapSpareSize = 0xC00;   // grown 0x600→0xC00 for the Xiao ladder-jump script (align walk + back-up + ready + arc); appended LAST so fishing label offsets are unchanged
        internal static readonly string[] AllySwapTownStbPaths =
        {
            "gedit/e01/event.stb", "gedit/e02/event.stb", "gedit/e03/event.stb", "gedit/e04/event.stb",
            "gedit/s01/event.stb", "gedit/s03/event.stb", "gedit/s04/event.stb", "gedit/s09/event.stb",
            "gedit/s13/event.stb",
        };

        /// <summary>Spare-label ids+sizes to bake into a town's event.stb: always the ally-swap 405, plus the
        /// fishing pool for the three custom fishing towns (baked in ONE table relocation per town).</summary>
        internal static (int[] ids, int[] sizes) SpareLabelsFor(string stbName)
        {
            if (Array.IndexOf(FishingTownStbPaths, stbName) < 0)
                return (new[] { AllySwapLabelId }, new[] { AllySwapSpareSize });
            // Fishing towns: APPEND 405 AFTER the fishing pool. 405 was the LAST entry of the old combined
            // FishingSpareLabelIds, so appending keeps every fishing label at its master codeOffset — the baked
            // table is byte-identical to master. (Prepending shifted them all by 0x600 and hung the fishing enter.)
            return (Append(FishingSpareLabelIds, AllySwapLabelId), Append(FishingSpareLabelSizes, AllySwapSpareSize));
        }

        private static int[] Append(int[] head, int tail)
        {
            var r = new int[head.Length + 1];
            Array.Copy(head, r, head.Length);
            r[head.Length] = tail;
            return r;
        }

        internal static byte[] ExtendStb(byte[] stb, int[] ids, int[] sizes)
        {
            uint codeBase = U32(stb, 0x08);                               // header: CodeBase @0x08
            uint tbl = U32(stb, 0x0C), cnt = U32(stb, 0x10);           // header: LabelTable @0x0C, LabelCount @0x10
            int origEnd = stb.Length;
            int spares = sizes.Length;
            int total = 0; foreach (int s in sizes) total += s;
            int newTblOff = origEnd + total;                          // terminator points here; code fills [origEnd, newTblOff)
            var outb = new byte[newTblOff + (int)(cnt + spares + 1) * 8];
            Array.Copy(stb, outb, origEnd);                            // original STB verbatim (appended space stays 0)
            Array.Copy(stb, (int)tbl, outb, newTblOff, (int)cnt * 8);  // original label table, copied unchanged
            int p = newTblOff + (int)cnt * 8;
            int codeOff = origEnd;
            for (int k = 0; k < spares; k++)                           // new spares -> point into the appended code space
            {
                U32(outb, p, (uint)ids[k]);                             // FINAL id — the mod claims it by number
                U32(outb, p + 4, (uint)codeOff);                        // codeOffset is ABSOLUTE (runtime uses stb+off)
                // gap[+0] = entry PC as a codeBase-relative offset to the first instruction
                // (codeOff + LabelCodeSkip 0x38 - codeBase); every real label carries this, WriteScript
                // never sets it, so a zero-filled baked label runs from the wrong PC and returns instantly.
                U32(outb, codeOff, (uint)(codeOff + 0x38 - (int)codeBase));
                codeOff += sizes[k];
                p += 8;
            }
            U32(outb, p, FishingTerminatorLabelId);                                  // terminator label: makes the last spare's size computable
            U32(outb, p + 4, (uint)codeOff);                          // == newTblOff (end of the last spare's code)
            U32(outb, 0x0C, (uint)newTblOff);                         // header now points at the relocated table
            U32(outb, 0x10, cnt + (uint)spares + 1);
            return outb;
        }

        // The dock-spawn event body (baked into s09 as DockSpawnEvent): reset the world coord to identity so
        // the coords are plain world, snap the player (charaId -1) to the Shipwreck dock, face DockSpawnFacing, RET.
        // Same shape CustomFishingSpot uses for the fishing stance (_SET_WORLD_COORD + _SET_NPC_POS/_ROT).
        internal static byte[] BuildDockSpawnCode()
        {
            const int ResetCamera = 433, ResetCameraAngle = 436;   // VM cmds — same ones East Harbor's entry event 128 issues
            var w = new StbWriter();
            w.PushInt(StbCommands.SetWorldCoord); w.Ext(1);                          // no args = identity
            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);
            w.PushFloat(DockSpawnPosition[0]); w.PushFloat(DockSpawnPosition[1]); w.PushFloat(DockSpawnPosition[2]); w.Ext(5);
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushFloat(0f); w.PushFloat(DockSpawnFacing); w.PushFloat(0f); w.Ext(5);
            w.PushInt(ResetCamera); w.PushInt(1); w.Ext(2);                          // snap the follow camera behind the player
            w.PushInt(ResetCameraAngle); w.PushInt(0); w.Ext(2);                     // and reset its angle
            w.Ret();
            return w.ToArray();
        }

        // Bake ONE label carrying real bytecode into an event.stb (vs ExtendStb's empty spares) — for towns the
        // runtime installer never visits (East Harbor). Appends [0x38 header + code], then a relocated label
        // table = originals + the new label + a terminator; header @0x0C/0x10 repointed. fd[0] = entry PC
        // (codeOff+0x38 codeBase-relative), exactly as ExtendStb sets for its spares.
        internal static byte[] BakeStbLabel(byte[] stb, int labelId, byte[] code)
        {
            uint codeBase = U32(stb, 0x08); uint tbl = U32(stb, 0x0C), cnt = U32(stb, 0x10);
            int codeOff = stb.Length;
            int codeSpace = Align16(0x38 + code.Length);
            int newTblOff = codeOff + codeSpace;
            var outb = new byte[newTblOff + (int)(cnt + 2) * 8];       // +1 new label, +1 terminator
            Array.Copy(stb, outb, codeOff);                            // original stb verbatim
            Array.Copy(stb, (int)tbl, outb, newTblOff, (int)cnt * 8);  // original label table
            U32(outb, codeOff, (uint)(codeOff + 0x38 - (int)codeBase));   // fd[0] entry PC
            Array.Copy(code, 0, outb, codeOff + 0x38, code.Length);    // the bytecode
            int p = newTblOff + (int)cnt * 8;
            U32(outb, p, (uint)labelId); U32(outb, p + 4, (uint)codeOff); p += 8;    // new label -> its code
            U32(outb, p, FishingTerminatorLabelId);    U32(outb, p + 4, (uint)newTblOff);          // terminator (size sentinel)
            U32(outb, 0x0C, (uint)newTblOff); U32(outb, 0x10, cnt + 2);
            return outb;
        }
    }
}
