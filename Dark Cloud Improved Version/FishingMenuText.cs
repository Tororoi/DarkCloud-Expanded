using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The meswin text the custom fishing towns lack: the catch bubble (talk-mes msg 2000) and the entry/quit
    /// menu options (event-mes 20/21/22). Each is a tiny meswin buffer built once into a reserved scratch
    /// region; CustomFishingSpot.UpdateFishingWindow swaps the ClsMes buffer pointer to it for a session.
    /// </summary>
    internal static class FishingMenuText
    {
        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishingMenuText] " + s);

        /// <summary>Re-arm both templates so they are rewritten after a town reload (the guest scratch region
        /// may have been cleared). Called from CustomFishingSpot's per-town reset.</summary>
        internal static void Reset() { _catchMesWritten = false; _menuMesWritten = false; }

        // ── CATCH-MESSAGE TEMPLATE (the empty-bubble fix) ──────────────────────────────────────────────
        // The engine draws the fishing catch bubble via MakeMesWin(townTalkClsMes, 2000): message id 2000 in
        // the TOWN's talk mes is the "[fish] (Xcm) caught! ..." template. Vanilla fishing towns ship it; our
        // custom fishing towns do NOT, so the bubble renders EMPTY. Rather than rebuild the whole ~80 KB talk
        // mes to add one entry, we hold a tiny 1-entry meswin buffer (msg 2000 only) in a reserved scratch
        // region and SWAP the ClsMes talk-buffer pointer to it for the duration of a fishing session — no NPC
        // dialogue runs while fishing, so the town's own messages are not needed in that window. The fish NAME
        // ([fbfe]) and the numbers resolve from the ClsMes SYSTEM buffer (global system14e.bin) and value
        // array, which are untouched — so injecting only the template is enough.

        // meswin buffer format (cracked via GetTextLineDataTop 0x14f4b0): u16 count · u16 (SetBuff's +0x17A8
        // delta) · count×{u16 id, u16 wordOff} · text. A message's text is at byte 2*(count + wordOff + 1);
        // glyphs are 16-bit LE, 0xFF01 terminates. This is msg 2000's exact glyph stream from Norune's English
        // talk mes (e01talk_1). The [fbXX] codes are placeholders the renderer fills: fbfe = fish name,
        // fbfa/fbf9/fbf8 = the numbers (length, points, total).
        private static readonly ushort[] CatchTemplateWords =
        {
            0xfbfe,0xff02,0xfd61,0xfbfa,0xfd3d,0xfd47,0xfd62,0xff00,0xfd3d,0xfd3b,0xfd4f,0xfd41,0xfd42,0xfd4e,
            0xfd58,0xfd58,0xff03,0xfd26,0xfd43,0xfd4d,0xfd42,0xfd43,0xfd48,0xfd41,0xff02,0xfd30,0xfd49,0xfd43,
            0xfd48,0xfd4e,0xfd4d,0xff02,0xfd5c,0xfbf9,0xff00,0xfd34,0xfd49,0xfd4e,0xfd3b,0xfd46,0xff02,0xfd30,
            0xfd49,0xfd43,0xfd48,0xfd4e,0xfd4d,0xff02,0xff02,0xff02,0xfbf8,0xff00,0xfd32,0xfd3f,0xfd3d,0xfd49,
            0xfd4c,0xfd3e,0xff02,0xfbfe,0xff02,0xfbfa,0xfd3d,0xfd47,0xff01,
        };
        private const int  CatchMsgId = 2000;
        private static bool _catchMesWritten;

        /// <summary>Build the 1-entry meswin buffer for msg 2000 once, into the reserved scratch region. Layout:
        /// count=1, entry(id=2000, wordOff=2), text at byte 8 — matching GetTextLineDataTop's 2*(count+off+1).</summary>
        internal static void EnsureCatchTemplate()
        {
            if (_catchMesWritten) return;
            var b = new byte[8 + CatchTemplateWords.Length * 2];
            void W16(int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); }
            W16(0, 1);           // count = 1
            W16(2, 8);           // +0x17A8 delta → text region (unused on the catch path, kept sane)
            W16(4, CatchMsgId);  // entry id
            W16(6, 2);           // entry wordOff → text at 2*(1+2+1) = byte 8
            for (int i = 0; i < CatchTemplateWords.Length; i++) W16(8 + i * 2, CatchTemplateWords[i]);
            Memory.WriteBytesBatch(CodeCaves.FishingCatchMes, b);
            _catchMesWritten = true;
            Log($"catch template (msg {CatchMsgId}) written to scratch 0x{CodeCaves.FishingCatchMes:X} ({b.Length} B)");
        }

        // ── FISHING MENU TEXT (event mes, window 1) ────────────────────────────────────────────────────
        // The entry/exit menus draw their option text via _MES_MAKE(1, id): window 1 = EditEventMes1
        // (0x21D1E4D0), fed by the town EVENT mes (<code>_1.mes). Vanilla fishing towns ship ids 20/21/22;
        // custom towns do not, so the menus would render blank. We hold a 3-message buffer (20 = the 4-option
        // entry menu, 21 = the no-pole line, 22 = the 2-option quit menu) and swap the event-mes buffer to it
        // for the session, exactly like the catch template does for the talk mes. This is a COMPLETE meswin
        // buffer (header + index + text) built offline and verified against GetTextLineDataTop; unlike the
        // catch template it carries multiple entries, so it is stored whole rather than header-in-code.
        private static readonly ushort[] MenuMesBuffer =
        {
            0x0003,0x0010,0x0014,0x0004,0x0015,0x0036,0x0016,0x0076,0xff02,0xff02,0xfd26,0xfd43,0xfd4d,0xfd42,
            0xff00,0xff02,0xff02,0xfd25,0xfd52,0xfd3d,0xfd42,0xfd3b,0xfd48,0xfd41,0xfd3f,0xff02,0xfd26,0xfd30,
            0xff00,0xff02,0xff02,0xfd26,0xfd43,0xfd4d,0xfd42,0xfd43,0xfd48,0xfd41,0xff02,0xfd46,0xfd49,0xfd41,
            0xff00,0xff02,0xff02,0xfd31,0xfd4f,0xfd43,0xfd4e,0xff02,0xfd40,0xfd43,0xfd4d,0xfd42,0xfd43,0xfd48,
            0xfd41,0xff01,0xfd33,0xfd3f,0xfd3f,0xfd47,0xfd4d,0xff02,0xfd46,0xfd43,0xfd45,0xfd3f,0xff02,0xfd53,
            0xfd49,0xfd4f,0xff02,0xfd3d,0xfd3b,0xfd48,0xff02,0xfd40,0xfd43,0xfd4d,0xfd42,0xff02,0xfd42,0xfd3f,
            0xfd4c,0xfd3f,0xff00,0xfd3c,0xfd4f,0xfd4e,0xff02,0xfd53,0xfd49,0xfd4f,0xff02,0xfd3e,0xfd49,0xfd48,
            0xfd55,0xfd4e,0xff02,0xfd42,0xfd3b,0xfd50,0xfd3f,0xff02,0xfd3b,0xff02,0xfd40,0xfd43,0xfd4d,0xfd42,
            0xfd43,0xfd48,0xfd41,0xff02,0xfd4a,0xfd49,0xfd46,0xfd3f,0xfd6d,0xff01,0xff02,0xff02,0xfd23,0xfd49,
            0xfd48,0xfd4e,0xfd43,0xfd48,0xfd4f,0xfd3f,0xff02,0xfd40,0xfd43,0xfd4d,0xfd42,0xfd43,0xfd48,0xfd41,
            0xff00,0xff02,0xff02,0xfd31,0xfd4f,0xfd43,0xfd4e,0xff02,0xfd40,0xfd43,0xfd4d,0xfd42,0xfd43,0xfd48,
            0xfd41,0xff01,
        };
        private static bool _menuMesWritten;

        /// <summary>Write the 3-message fishing-menu buffer to its scratch region once.</summary>
        internal static void EnsureMenuTemplate()
        {
            if (_menuMesWritten) return;
            var b = new byte[MenuMesBuffer.Length * 2];
            for (int i = 0; i < MenuMesBuffer.Length; i++)
            { b[i * 2] = (byte)MenuMesBuffer[i]; b[i * 2 + 1] = (byte)(MenuMesBuffer[i] >> 8); }
            Memory.WriteBytesBatch(CodeCaves.FishingMenuMes, b);
            _menuMesWritten = true;
            Log($"menu text (event msg 20/21/22) written to scratch 0x{CodeCaves.FishingMenuMes:X} ({b.Length} B)");
        }
    }
}
