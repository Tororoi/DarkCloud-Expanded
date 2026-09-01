using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// meswin .mes text bakes for the custom fishing towns: carve a message out of one town's .mes
    /// (MesExtract) and inject it into another's (AppendMes / ReplaceEmptyWithMes). Pure byte[] -> byte[];
    /// IsoPatcher.ApplySignPatch redirects the results.
    /// </summary>
    internal static class MesTextBaker
    {
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
