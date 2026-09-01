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
    /// Town text/script bakes for the custom fishing towns: event.stb spare-label space (ExtendStb) and
    /// baked labels (BakeStbLabel), plus meswin .mes message carving/appending (MesExtract / AppendMes /
    /// ReplaceEmptyWithMes). Pure byte[] -> byte[] transforms; IsoPatcher.ApplySignPatch redirects the results.
    /// </summary>
    internal static class TextBaker
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
        internal static readonly string[] FishingStbs =
            { "gedit/e03/event.stb", "gedit/s13/event.stb", "gedit/s04/event.stb" };   // Queens, Yellow Drops, Brownboo
        // A custom fishing spot installs exactly FOUR scripts. We bake exactly four spare labels — one per
        // script — carrying their FINAL ids and sized (measured Need() + margin) to hold that script in a
        // SINGLE label. The mod's install claims each by id and writes straight into it: no runtime renumber,
        // and nothing spills across two labels (the old "arena run" that retired a second label). The three
        // custom fishing towns have no native 133/134/400/9600, so these ids are collision-free.
        // ⚠ FishSpareIds MUST match CustomFishingSpot.{MenuSubLabelId=9600, FishingLabelId=400} and
        // EventPoints.{FishingExitLabel=133, FishingBaitLabel=134}. Sizes measured 2026-07-24:
        // menu 1780, enter 1994, quit 892, bait 436. Label 401 = the Queens canal-floor per-sign script (its own
        // stance) — baked in every town (unused in Brownboo/Yellow Drops, harmless).
        internal const int FishTermId = 9500;
        internal static readonly int[] FishSpareIds   = { 9600, 400, 401, 133, 134, LADDER_MSG_LABEL, CANAL_WARP_LABEL };  // menu, enter, canal-enter, quit, bait, ladder-msg, tide-evict
        internal static readonly int[] FishSpareSizes = { 0x800, 0xA00, 0xA00, 0x500, 0x300, 0x300, 0x100 };               // one size per id, same order
        // ↑ labels 402 (ladder tide-message) + 403 (tide-evict _MAP_JUMP) baked into every fishing town's stb
        //   (unused outside Queens, harmless — like 401); CustomFishingSpot installs them in Queens only.

        internal static byte[] ExtendStb(byte[] stb)
        {
            uint cbase = U32(stb, 0x08);                               // header: CodeBase @0x08
            uint tbl = U32(stb, 0x0C), cnt = U32(stb, 0x10);           // header: LabelTable @0x0C, LabelCount @0x10
            int origEnd = stb.Length;
            int spares = FishSpareSizes.Length;
            int total = 0; foreach (int s in FishSpareSizes) total += s;
            int newTblOff = origEnd + total;                          // terminator points here; code fills [origEnd, newTblOff)
            var outb = new byte[newTblOff + (int)(cnt + spares + 1) * 8];
            Array.Copy(stb, outb, origEnd);                            // original STB verbatim (appended space stays 0)
            Array.Copy(stb, (int)tbl, outb, newTblOff, (int)cnt * 8);  // original label table, copied unchanged
            int p = newTblOff + (int)cnt * 8;
            int codeOff = origEnd;
            for (int k = 0; k < spares; k++)                           // new spares -> point into the appended code space
            {
                U32(outb, p, (uint)FishSpareIds[k]);                    // FINAL id — the mod claims it by number
                U32(outb, p + 4, (uint)codeOff);                        // codeOffset is ABSOLUTE (runtime uses stb+off)
                // gap[+0] = entry PC as a codeBase-relative offset to the first instruction
                // (codeOff + LabelCodeSkip 0x38 - codeBase); every real label carries this, WriteScript
                // never sets it, so a zero-filled baked label runs from the wrong PC and returns instantly.
                U32(outb, codeOff, (uint)(codeOff + 0x38 - (int)cbase));
                codeOff += FishSpareSizes[k];
                p += 8;
            }
            U32(outb, p, FishTermId);                                  // terminator label: makes the last spare's size computable
            U32(outb, p + 4, (uint)codeOff);                          // == newTblOff (end of the last spare's code)
            U32(outb, 0x0C, (uint)newTblOff);                         // header now points at the relocated table
            U32(outb, 0x10, cnt + (uint)spares + 1);
            return outb;
        }

        // The dock-spawn event body (baked into s09 as DOCK_SPAWN_LABEL): reset the world coord to identity so
        // the coords are plain world, snap the player (charaId -1) to the Shipwreck dock, face DOCK_FACING, RET.
        // Same shape CustomFishingSpot uses for the fishing stance (_SET_WORLD_COORD + _SET_NPC_POS/_ROT).
        internal static byte[] BuildDockSpawnCode()
        {
            const int ResetCamera = 433, ResetCameraAngle = 436;   // VM cmds — same ones East Harbor's entry event 128 issues
            var w = new StbWriter();
            w.PushInt(StbCommands.SetWorldCoord); w.Ext(1);                          // no args = identity
            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);
            w.PushFloat(DOCK_POS[0]); w.PushFloat(DOCK_POS[1]); w.PushFloat(DOCK_POS[2]); w.Ext(5);
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushFloat(0f); w.PushFloat(DOCK_FACING); w.PushFloat(0f); w.Ext(5);
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
            uint cbase = U32(stb, 0x08); uint tbl = U32(stb, 0x0C), cnt = U32(stb, 0x10);
            int codeOff = stb.Length;
            int codeSpace = Align16(0x38 + code.Length);
            int newTblOff = codeOff + codeSpace;
            var outb = new byte[newTblOff + (int)(cnt + 2) * 8];       // +1 new label, +1 terminator
            Array.Copy(stb, outb, codeOff);                            // original stb verbatim
            Array.Copy(stb, (int)tbl, outb, newTblOff, (int)cnt * 8);  // original label table
            U32(outb, codeOff, (uint)(codeOff + 0x38 - (int)cbase));   // fd[0] entry PC
            Array.Copy(code, 0, outb, codeOff + 0x38, code.Length);    // the bytecode
            int p = newTblOff + (int)cnt * 8;
            U32(outb, p, (uint)labelId); U32(outb, p + 4, (uint)codeOff); p += 8;    // new label -> its code
            U32(outb, p, FishTermId);    U32(outb, p + 4, (uint)newTblOff);          // terminator (size sentinel)
            U32(outb, 0x0C, (uint)newTblOff); U32(outb, 0x10, cnt + 2);
            return outb;
        }

        // ── town .mes text: bake in the fishing messages the custom towns lack ──────────────────────────
        //
        // Vanilla fishing towns' mes ship the catch bubble (talk mes msg 2000) and the entry/quit menu options
        // (event mes 20/21/22); custom fishing towns do not, so those bubbles/menus rendered blank. Rather than
        // swap the ClsMes buffer pointer at runtime, we carve the messages from the user's OWN Norune mes and
        // inject them into each custom town's mes, so the engine draws them natively.
        //
        // meswin .mes format (verified against every town's mes): u16 count, u16 endOff, count×{u16 id, u16
        // wordOff}, then the text. A message's text starts at byte 2*(count + wordOff + 1) and ends at 0xFF01;
        // files are zero-padded. AppendMes TRIMS that trailing padding before appending so the injected text
        // stays inside the engine's ~48 KB talk-mes buffer even for the largest town (Queens) — see AppendMes.

        /// <summary>Append the 0xFF01 meswin terminator so an Encode()'d string matches MesExtract's blob
        /// shape (its words run through the terminator, inclusive), which is what AppendMes writes verbatim.</summary>
        internal static ushort[] AppendTerminator(ushort[] words)
        {
            var w = new ushort[words.Length + 1];
            Array.Copy(words, w, words.Length);
            w[words.Length] = 0xFF01;
            return w;
        }

        /// <summary>The glyph words of message <paramref name="id"/> (from its text start to the 0xFF01
        /// terminator, inclusive). Throws if the id is absent.</summary>
        internal static ushort[] MesExtract(byte[] mes, int id)
        {
            int cnt = U16(mes, 0);
            for (int i = 0; i < cnt; i++)
            {
                if (U16(mes, 4 + i * 4) != id) continue;
                int tb = 2 * (cnt + U16(mes, 4 + i * 4 + 2) + 1);
                var w = new List<ushort>();
                while (tb + 1 < mes.Length)
                {
                    ushort g = U16(mes, tb); w.Add(g); tb += 2;
                    if (g == 0xFF01) return w.ToArray();
                }
                break;
            }
            throw new IOException($"meswin message {id} not found (unexpected mes layout)");
        }

        /// <summary>Append <paramref name="add"/> messages to a meswin .mes, first TRIMMING the file's trailing
        /// zero padding so the new text lands right after real content. Existing messages are preserved
        /// byte-for-byte (their text never moves); the index is kept id-sorted.
        ///
        /// The trim is why append works for the largest town. The engine loads a town's talk mes into a fixed
        /// ~48 KB buffer (`MesBuffer`). Every town pads its message region with ~31 KB of trailing zeros, so a
        /// plain append placed msg 2000 ~31 KB deep — at ~offset 78 KB for Queens (e03), the largest town at
        /// ~47 KB of real content — far outside the buffer, and `MakeMesTexture` measured garbage and drew a
        /// stretched, textless catch bubble. Trimming the padding drops 2000 to ~offset 47 KB (just past real
        /// content), inside the buffer, WITHOUT shifting any existing message — so nothing else in the town
        /// regresses. (Prepending the text instead moves every message and corrupted unrelated dialogue, so it
        /// is avoided.) Verified: every non-sentinel message decodes byte-identically to a plain append, and
        /// 2000 lands in-buffer for all three custom towns.</summary>
        internal static byte[] AppendMes(byte[] orig, params (int id, ushort[] words)[] add)
        {
            int cnt = U16(orig, 0), f2 = U16(orig, 2), n = add.Length, newCount = cnt + n;
            int idxEnd = 4 + cnt * 4;                       // byte where the original text blob starts

            // Trim trailing zero padding, keeping a small zero gap so the last real message still terminates
            // (it ends on a 0-word, exactly as it did against the full padding).
            int blobEnd = orig.Length;
            while (blobEnd > idxEnd && orig[blobEnd - 1] == 0) blobEnd--;    // last non-zero byte of the blob
            const int Gap = 16;                                             // zero words kept as a terminator margin
            int raw = (blobEnd - idxEnd) + Gap; raw += raw & 1;             // word-align
            int blobLen = Math.Min(orig.Length - idxEnd, raw);

            var ents = new List<(int id, int off)>(newCount);
            for (int i = 0; i < cnt; i++)                  // existing: +n absorbs the index-growth shift; text unmoved
                ents.Add((U16(orig, 4 + i * 4), U16(orig, 4 + i * 4 + 2) + n));

            var newText = new List<byte>();
            int cum = blobLen;                             // new text laid out after the TRIMMED blob
            foreach (var (id, words) in add)
            {
                int textByte = 4 + newCount * 4 + cum;
                ents.Add((id, textByte / 2 - newCount - 1));
                foreach (ushort g in words) { newText.Add((byte)g); newText.Add((byte)(g >> 8)); }
                cum += words.Length * 2;
            }
            ents.Sort((a, b) => a.id.CompareTo(b.id));     // the engine expects the index id-sorted

            var outb = new byte[4 + newCount * 4 + blobLen + newText.Count];
            U16(outb, 0, (ushort)newCount);
            U16(outb, 2, (ushort)(f2 + n));
            int p = 4;
            foreach (var (id, off) in ents) { U16(outb, p, (ushort)id); U16(outb, p + 2, (ushort)off); p += 4; }
            Array.Copy(orig, idxEnd, outb, p, blobLen); p += blobLen;   // trimmed original blob ...
            newText.CopyTo(outb, p);                                    // ... then the new text
            return outb;
        }

        /// <summary>Inject one message by REPURPOSING the mes's highest-id empty (sentinel) entry instead of
        /// adding a new one — so the message COUNT never grows and no existing message's read position shifts.
        ///
        /// This matters because other systems address talk-mes messages by ABSOLUTE buffer offset — notably the
        /// custom-NPC dialogue writer (Dialogues.cs), which writes e.g. Pickle's text to a hardcoded address. A
        /// count-growing append slides every existing message a few bytes (the format ties a message's read
        /// offset to the message count), so those hardcoded writes land a couple glyphs early and the first
        /// letters get cut off. Repurposing a spare sentinel keeps the count fixed, so every existing message —
        /// and every hardcoded offset into it — stays exactly where it was. The new text is placed after real
        /// content (padding trimmed) so it lands inside the engine's ~48 KB talk-mes buffer.
        ///
        /// The three custom fishing towns each have a high-id empty sentinel (e03 1399, s04 1279, s13 1259).
        /// Verified: repurposing it leaves every other message's read offset unchanged and msg 2000 decodes.</summary>
        internal static byte[] ReplaceEmptyWithMes(byte[] orig, int id, ushort[] words)
        {
            int cnt = U16(orig, 0), f2 = U16(orig, 2), idxEnd = 4 + cnt * 4;

            // Find the highest-id EMPTY entry (its text begins with a 0-word or the terminator) — the sentinel.
            int si = -1, bestId = -1;
            for (int i = 0; i < cnt; i++)
            {
                int woff = U16(orig, 4 + i * 4 + 2), tb = 2 * (cnt + woff + 1);
                bool empty = tb + 1 >= orig.Length || U16(orig, tb) == 0x0000 || U16(orig, tb) == 0xFF01;
                int mid = U16(orig, 4 + i * 4);
                if (empty && mid > bestId) { bestId = mid; si = i; }
            }
            if (si < 0) throw new IOException($"no empty sentinel entry to repurpose for msg {id}");

            // Trim trailing zero padding (keep a small gap); place the new text right after real content.
            int blobEnd = orig.Length;
            while (blobEnd > idxEnd && orig[blobEnd - 1] == 0) blobEnd--;
            int raw = (blobEnd - idxEnd) + 16; raw += raw & 1;
            int blobLen = Math.Min(orig.Length - idxEnd, raw);

            var ents = new List<(int id, int off)>(cnt);
            for (int i = 0; i < cnt; i++) ents.Add((U16(orig, 4 + i * 4), U16(orig, 4 + i * 4 + 2)));
            ents[si] = (id, (4 + cnt * 4 + blobLen) / 2 - cnt - 1);   // repurpose the sentinel slot in place
            ents.Sort((a, b) => a.id.CompareTo(b.id));

            var newText = new List<byte>();
            foreach (ushort g in words) { newText.Add((byte)g); newText.Add((byte)(g >> 8)); }

            var outb = new byte[4 + cnt * 4 + blobLen + newText.Count];
            U16(outb, 0, (ushort)cnt);                      // COUNT unchanged — the whole point
            U16(outb, 2, (ushort)f2);
            int p = 4;
            foreach (var (mid, off) in ents) { U16(outb, p, (ushort)mid); U16(outb, p + 2, (ushort)off); p += 4; }
            Array.Copy(orig, idxEnd, outb, p, blobLen); p += blobLen;
            newText.CopyTo(outb, p);
            return outb;
        }
    }
}
