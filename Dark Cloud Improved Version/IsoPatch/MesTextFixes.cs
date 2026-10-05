using System;
using System.Collections.Generic;
using System.Linq;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Spelling and typing fixes to the game's English text, applied IN PLACE to every English message bank on the disc
    /// (the `*_1.mes` files and the English `.bin` copies of the system, item and editor banks). No message ever moves and no
    /// bank changes size: the town talk banks are addressed by absolute buffer offset at runtime (Dialogues writes the mod's NPC
    /// text over fixed vanilla messages), and several banks are allocated by their file size.
    ///
    /// Each message is rewritten within its own byte span. A text that gets shorter is re-terminated early (the words after the
    /// 0xFF01 terminator are never read). A text that gets longer first takes the gap words that happen to follow its terminator
    /// (the next message's text starts on its own index offset), then gives up its trailing spaces — a space right before a line
    /// break, a page break or the end draws nothing. A message that still does not fit is copied into the bank's zero padding
    /// just past the real content and its index entries repointed (the count and every other entry stay put); the copy must land
    /// inside the engine's talk-mes buffer (<see cref="BufferLimit"/>).
    ///
    /// <see cref="Fixes"/> are whole-word substitutions (a letter or digit may not touch either end of the match). Two general
    /// rules then run on prose: a double space between two glyphs collapses to one unless it follows a sentence end, and a space
    /// before `. , ? !` after a letter is dropped unless the punctuation starts an ellipsis. Messages with value tokens (0xFB),
    /// pad glyphs (0xF9) or icon glyphs (index below 0x21) are tables and menus whose spacing is alignment, so the rules skip them.
    /// </summary>
    internal static class MesTextFixes
    {
        /// <summary>Whole-word substitutions, in the glyph plane (<see cref="WeaponDescriptions.Encode"/>: ' is the game's apostrophe).</summary>
        internal static readonly (string old, string @new)[] Fixes =
        {
            ("alot", "a lot"), ("Mikala", "Mikara"), ("Atlamilla", "Atlamillia"),
            ("Bamirumba Hamorumba", "Bamilumba Hamolumba"), ("Skelton", "Skeleton"),
            ("auxilliary", "auxiliary"), ("warror", "warrior"), ("misson", "mission"), ("enternal", "eternal"),
            ("greif", "grief"), ("embarassment", "embarrassment"), ("whimpy", "wimpy"), ("whimp", "wimp"),
            ("and and", "and"), ("an fearsome", "a fearsome"), ("Your welcome", "You're welcome"),
            ("how's Tukkie is doing", "how Tukkie is doing"), ("That the best", "That's the best"),
            ("full.Can't", "full. Can't"), ("another town.", "another town?"),
            ("the mystery of the town", "the mystery of the town."), ("for free of charge", "free of charge"),
            ("Mr.Moustache", "Mr. Mustache"), ("Moustache", "Mustache"), ("Stew", "Stu"), ("Marnia", "Mahnia"), ("Suger", "Sugar"),
            ("Yellow Drop", "Yellow Drops"), ("Sun & Moon temple", "Sun & Moon Temple"), ("Muska desert", "Muska Desert"),
            ("No! that's", "No! That's"),
            // NPC and place names: the Georama name plate / menu spelling is the reference.
            ("Nemu", "Nem"), ("AncientBaron", "Ancient Baron"), ("Jibubu's Hose", "Jibubu's House"), ("3sisters' House", "3 Sisters' House"),
            ("LeaningTower", "Leaning Tower"), ("Kye&Momo's", "Kye & Momo's"),
            // The plate bank the USA build loads (gedit\system\editsys.bin) differs from its editsys_1.mes twin in a few resident names.
            ("Dike", "Pike"), ("Xena", "Gina"), ("Strage Guard", "Storage Guard"),
            // The weapon list the USA build loads (systeme.bin) still has the old spelling of Halberd.
            ("Halbert", "Halberd"),
            // Suzy's Queens shop is a "Watery" in every line of dialogue; the Georama menu called the three names "Washery" and gave two
            // of them different adjectives. The menu's adjectives stay, the dialogue's noun wins: Freshen Up / Miracle / Warrior's Watery.
            ("Freshen Up Washery", "Freshen Up Watery"), ("Miracle Washery", "Miracle Watery"), ("Warrior'sWashery", "Warrior's Watery"),
            ("FreshenUp Watery", "Freshen Up Watery"), ("Magical Watery", "Miracle Watery"), ("Fighting Watery", "Warrior's Watery"),
        };

        /// <summary>The English `.bin` banks beside the `_1.mes` files (the system, item-notice and editor banks the engine loads by these names).</summary>
        private static readonly string[] BinBanks = { "systeme.bin", "system14e.bin", "_systeme.bin", "_system14e.bin", "editsys.bin", "system_ae.bin" };

        /// <summary>A relocated text must end below this byte of its bank: the town talk banks load into a fixed buffer of about 48 KB.</summary>
        private const int BufferLimit = 47 * 1024;

        private const ushort LineBreak = 0xFF00, End = 0xFF01, Space = 0xFF02, PageBreak = 0xFF03;
        private const ushort Period = 0xFD6D, Comma = 0xFD6C, Question = 0xFD59, Exclaim = 0xFD58;

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            var tally = new Dictionary<string, int>();
            int banks = 0, messages = 0, spaces = 0, punct = 0, relocated = 0;
            foreach (string name in arc.Names().Where(IsEnglishBank).OrderBy(n => n, StringComparer.Ordinal))
            {
                byte[] mes = arc.Read(name);
                if (!LooksLikeBank(mes)) continue;
                var r = FixBank(mes, name, tally, log);
                if (r.messages == 0) continue;
                arc.Overwrite(name, mes);
                banks++; messages += r.messages; spaces += r.spaces; punct += r.punct; relocated += r.relocated;
            }
            foreach (var f in Fixes)
                log($"  {f.old} -> {f.@new}: {(tally.TryGetValue(f.old, out int n) ? n : 0)}");
            log($"text fixes: {messages} messages in {banks} banks ({spaces} double spaces, {punct} spaces before punctuation, {relocated} relocated)");
        }

        private static bool IsEnglishBank(string name)
        {
            string file = name.Substring(name.LastIndexOf('\\') + 1).ToLowerInvariant();
            return file.EndsWith("_1.mes") || BinBanks.Contains(file);
        }

        private static bool LooksLikeBank(byte[] mes)
        {
            if (mes.Length < 8) return false;
            int cnt = U16(mes, 0);
            return cnt > 0 && cnt <= 5000 && 4 + cnt * 4 < mes.Length;
        }

        private static (int messages, int spaces, int punct, int relocated) FixBank(byte[] mes, string name, Dictionary<string, int> tally, Action<string> log)
        {
            int cnt = U16(mes, 0);
            var ids = new int[cnt]; var starts = new int[cnt];
            for (int i = 0; i < cnt; i++) { ids[i] = U16(mes, 4 + i * 4); starts[i] = 2 * (cnt + U16(mes, 4 + i * 4 + 2) + 1); }
            var sortedStarts = starts.Distinct().OrderBy(s => s).ToArray();
            int contentEnd = mes.Length;                                   // first byte of the trailing zero padding
            while (contentEnd > 4 + cnt * 4 && mes[contentEnd - 1] == 0) contentEnd--;
            contentEnd += contentEnd & 1;

            int messages = 0, spaces = 0, punct = 0, relocated = 0;
            foreach (int tb in sortedStarts)                               // shared texts (several ids, one start) are edited once
            {
                ushort[] words = ReadWords(mes, tb);
                if (words == null) continue;
                var text = new List<ushort>(words);
                bool changed = false;
                foreach (var (old, @new) in Fixes)
                {
                    int n = ReplaceWholeWord(text, WeaponDescriptions.Encode(old), WeaponDescriptions.Encode(@new));
                    if (n == 0) continue;
                    tally[old] = (tally.TryGetValue(old, out int t) ? t : 0) + n;
                    changed = true;
                }
                var (sp, pu) = ApplySpacingRules(text);
                spaces += sp; punct += pu;
                if (sp + pu > 0) changed = true;
                if (!changed) continue;
                messages++;

                int next = sortedStarts.FirstOrDefault(s => s > tb);       // the next text's start bounds this one's span
                int spanWords = ((next > 0 ? next : contentEnd) - tb) / 2;
                if (!ShrinkTo(text, spanWords))
                {
                    int at = contentEnd + 16;                              // past the real content, a zero gap kept before it
                    if (at + text.Count * 2 > Math.Min(mes.Length, BufferLimit))
                        throw new InvalidOperationException($"{name}: message at 0x{tb:X} grew from {words.Length} to {text.Count} words and there is no room to relocate it");
                    for (int i = 0; i < cnt; i++)
                        if (starts[i] == tb) U16(mes, 4 + i * 4 + 2, (ushort)(at / 2 - cnt - 1));
                    Array.Clear(mes, tb, words.Length * 2);                // the old text is never read again
                    WriteWords(mes, at, text);
                    contentEnd = at + text.Count * 2;
                    relocated++;
                    log($"  {name} 0x{tb:X}: relocated to 0x{at:X} ({words.Length} -> {text.Count} words)");
                    continue;
                }
                WriteWords(mes, tb, text);
                for (int b = tb + text.Count * 2; b < tb + words.Length * 2; b++) mes[b] = 0;   // the vacated tail of the old span
            }
            return (messages, spaces, punct, relocated);
        }

        /// <summary>The message's words from its text start through the 0xFF01 terminator, or null if it has none.</summary>
        private static ushort[] ReadWords(byte[] mes, int tb)
        {
            var w = new List<ushort>();
            for (int p = tb; p + 1 < mes.Length; p += 2)
            {
                ushort g = U16(mes, p); w.Add(g);
                if (g == End) return w.ToArray();
            }
            return null;
        }

        private static void WriteWords(byte[] mes, int at, List<ushort> words)
        {
            for (int i = 0; i < words.Count; i++) U16(mes, at + i * 2, words[i]);
        }

        private static bool IsGlyph(ushort w) => (w >> 8) == 0xFD;
        private static bool IsAlnum(ushort w)
        {
            if (!IsGlyph(w)) return false;
            int g = w & 0xFF;
            return (g >= 0x21 && g <= 0x54) || (g >= 0x6F && g <= 0x78);
        }
        private static bool IsSentenceEnd(ushort w) => w == Period || w == Question || w == Exclaim;
        private static bool IsPunct(ushort w) => w == Period || w == Comma || w == Question || w == Exclaim;

        /// <summary>Prose, as opposed to a table or menu: no value tokens, pad glyphs or icon glyphs.</summary>
        private static bool IsProse(List<ushort> t)
        {
            foreach (ushort w in t)
            {
                int hi = w >> 8;
                if (hi == 0xFB || hi == 0xF9) return false;
                if (hi == 0xFD && (w & 0xFF) < 0x21) return false;
            }
            return true;
        }

        /// <summary>Replace every whole-word occurrence of <paramref name="pat"/>; returns how many.</summary>
        private static int ReplaceWholeWord(List<ushort> t, ushort[] pat, ushort[] rep)
        {
            int n = 0;
            for (int i = 0; i + pat.Length <= t.Count; )
            {
                bool match = true;
                for (int k = 0; k < pat.Length && match; k++) match = t[i + k] == pat[k];
                if (match && IsAlnum(pat[0]) && i > 0 && IsAlnum(t[i - 1])) match = false;
                if (match && IsAlnum(pat[pat.Length - 1]) && i + pat.Length < t.Count && IsAlnum(t[i + pat.Length])) match = false;
                if (!match) { i++; continue; }
                t.RemoveRange(i, pat.Length); t.InsertRange(i, rep);
                i += rep.Length; n++;
            }
            return n;
        }

        private static (int spaces, int punct) ApplySpacingRules(List<ushort> t)
        {
            bool prose = IsProse(t);
            int spaces = 0, punct = 0;
            for (int i = 1; i + 1 < t.Count; )
            {
                if (t[i] != Space) { i++; continue; }
                ushort prev = t[i - 1], nxt = t[i + 1];
                if (prose && nxt == Space && i + 2 < t.Count && IsGlyph(prev) && !IsSentenceEnd(prev) && IsGlyph(t[i + 2]))
                { t.RemoveAt(i); spaces++; continue; }
                if (IsPunct(nxt) && IsAlnum(prev) && !(i + 2 < t.Count && t[i + 2] == Period))
                { t.RemoveAt(i); punct++; continue; }
                i++;
            }
            return (spaces, punct);
        }

        /// <summary>Drop trailing spaces (a space right before a line break, page break or the end) until the text has at most
        /// <paramref name="maxWords"/> words; false if that is not enough.</summary>
        private static bool ShrinkTo(List<ushort> t, int maxWords)
        {
            while (t.Count > maxWords)
            {
                int i = t.Count - 1;
                for (; i > 0; i--)
                    if ((t[i] == LineBreak || t[i] == PageBreak || t[i] == End) && t[i - 1] == Space) break;
                if (i <= 0) return false;
                t.RemoveAt(i - 1);
            }
            return true;
        }
    }
}
