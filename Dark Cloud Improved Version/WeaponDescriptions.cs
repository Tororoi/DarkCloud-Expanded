using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Patches the in-game weapon DESCRIPTION text (the loaded <c>allmenu.mes</c> blob) with each weapon's
    /// <see cref="WeaponData.ModDescription"/>, so the menu shows the mod's custom-effect abilities.
    /// Full RE notes: docs/weapon-descriptions.md; encoding mirrors tools/mes_decode.py.
    ///
    /// How it works: every menu screen's texture-enter loader loads its own menu pak, fetches
    /// <c>GetPackFile(pak, "allmenu.mes")</c> and registers the pointer via <c>SetBuff__6ClsMesFPs</c> into
    /// fixed ClsMes globals (+0x17A0) — <see cref="MesTextPtrSlots"/>. The copies differ per pak (offsets AND
    /// text), so entries are located by ORDINAL (Nth 0xFF01 terminator = <see cref="WeaponData.MesEntry"/>)
    /// and rewritten IN PLACE within each entry's own span (name line kept, description lines replaced,
    /// padded with spaces). Re-applied whenever a fresh copy registers (pointer change) or the loaded copy
    /// reverts to vanilla (signature word check) — a one-shot write can't stick because each menu screen
    /// reloads its pak's copy from disc.
    /// </summary>
    internal static class WeaponDescriptions
    {
        // ── Fixed addresses (RE'd 2026-07-01; docs/weapon-descriptions.md "Runtime pointer chain") ──
        // ClsMes+0x17A0 text-buffer slots of the four fixed menu ClsMes objects; all hold the same native
        // pointer to the currently-loaded allmenu.mes data. We read slot 0.
        internal static readonly long[] MesTextPtrSlots =
            { 0x21DA2330, 0x21DA3AF0, 0x21DA52B0, 0x21DA6A70 };

        // Recognized .mes banks and the ordinal shift of their weapon entries vs WeaponData.MesEntry:
        //  • allmenu.mes  (count 0x1EF, or 0x1F1 for dunmenu4 — its 2 extra entries come AFTER the weapon
        //    block): weapon entries at MesEntry (delta 0). The plane-0-encoded dunmenu3/dunmenu_chk variants
        //    (also 0x1F1) are rejected by the glyph-plane probe below.
        //  • itemshop.bin (count 0x137, the SHOP menu's bank in itemshop.pak): the same 111 weapon entries
        //    (identical description text/budgets) at MesEntry − 182 (verified for all 111).
        // Anything else in the slot (incl. a still-loading buffer reading count 0) is skipped; a change of
        // the count at the same pointer re-triggers classification (async BG loads fill the buffer AFTER the
        // pointer registers, so the first read often sees count 0).
        const int ShopOrdinalDelta = 182;
        static int BankOrdinalDelta(int c) =>
            c == 0x1EF || c == 0x1F1 ? 0 : c == 0x137 ? ShopOrdinalDelta : -1;

        const int MaxBlobBytes = 0x13800;   // largest variant is 0x1330C
        const int PollMs       = 250;

        // ── Patch table (built once from the six per-character weapon classes) ──
        sealed class Patch
        {
            public int      Id;        // item id (for logging)
            public string   Name;      // weapon name (for logging)
            public int      MesEntry;  // entry ordinal in allmenu.mes
            public ushort[] Desc;      // encoded description: lines joined by 0xFF00 (no leading/trailing control)
        }
        static List<Patch> _patches;

        static bool _started;
        static int  _lastPtr = -1;
        static int  _lastCnt = -1; // entry count last seen at _lastPtr (a change = the async load completed / bank swapped)
        static long _sigAddr;      // address of the first EDITED description word (0 = nothing edited)
        static ushort _sigWord;    // its expected (patched) value — mismatch means the copy reloaded

        // ── Status Break hint (entry 47 in the allmenu banks): dynamic swap for the 7 Branch
        // Sword's "Sevenfold Rite" (gate +7, 77% transfer — CustomToanEffects.SevenBranchSwordEffect).
        // Both texts encode to the entry's exact 75-word span. Entry 47 sits BEFORE the weapon
        // block, so its ordinal is identical in every allmenu variant (dunmenu4's 2 extra
        // entries come after the block); the shop bank (0x137) doesn't contain it.
        const int StatusBreakHintEntry = 47;
        const string StatusBreakHintVanilla = "\n:Seal 60% weapon\nability to sphere.\n(Do w/o attachment.)\n Do status break?";
        const string StatusBreakHintSeven   = "\n:Seal 77% weapon\nability to sphere.\n(Level 7 or more.)\n Do status break?";
        static ushort[] _hintVanilla, _hintSeven;
        static volatile bool _hintSevenActive;

        // ── SynthSphere "acquired" line (entry 179 = ComItemInfo[0x5A].msgNo + 100): the text
        // the break flow's message window shows (CWeaponLevelUp::DrawMes state 6). Swapped to a
        // "resists" message while a below-+7 7 Branch Sword is selected, so an interrupted break
        // explains itself through the game's own popup. The vanilla words contain icon (0xFBxx)
        // and format codes the encoder can't produce, so they're stored raw (34-word span,
        // verified against tools/allmenu.mes.bin). ──
        const int BreakAcquiredEntry = 179;
        static readonly ushort[] _acquiredVanilla =
        {
            0xFF00, 0xFD12, 0xFD57, 0xFBFE, 0xFBFA, 0xFD57, 0xFF00, 0xFD13, 0xFD57, 0xFD33,
            0xFD53, 0xFD48, 0xFD4E, 0xFD42, 0xFD33, 0xFD4A, 0xFD42, 0xFD3F, 0xFD4C, 0xFD3F,
            0xFD57, 0xFF00, 0xFF02, 0xFF02, 0xFF02, 0xFD3B, 0xFD3D, 0xFD4B, 0xFD4F, 0xFD43,
            0xFD4C, 0xFD3F, 0xFD3E, 0xFD6D,
        };
        // 34-word span budget. Glyph 0x0B ('+') renders INCONSISTENTLY across the menu bank
        // variants (a '+' in some, a d-pad icon in others — the town/dungeon/map paks carry
        // different font pages), so mod text avoids '+' entirely and spells out "level 7".
        const string BreakAcquiredResists = "\nStatus break fails\nbelow level 7.";
        static ushort[] _acquiredResists;
        static volatile bool _acquiredResistsActive;

        /// <summary>Selects which Status Break hint text the menu shows: the 7 Branch Sword
        /// version (+7 / 77%) or vanilla (60%). Called by CustomToanEffects.SevenBranchSwordEffect as the
        /// weapon-menu selection changes; a change forces a repatch on the next tick.</summary>
        public static void SetStatusBreakHint(bool sevenBranch)
        {
            if (_hintSevenActive == sevenBranch) return;
            _hintSevenActive = sevenBranch;
            _lastCnt = -1;   // force PatchTick to rewrite even though pointer/count are unchanged
        }

        /// <summary>Swaps the SynthSphere "acquired" message line between vanilla and the
        /// 7 Branch Sword "resists" text. Keyed proactively on "below-+7 7BS selected" (set
        /// before any break can be confirmed), so the popup after an interrupted break reads as
        /// the refusal message; vanilla is restored when the selection changes.</summary>
        public static void SetBreakResultResists(bool resists)
        {
            if (_acquiredResistsActive == resists) return;
            _acquiredResistsActive = resists;
            _lastCnt = -1;
        }

        /// <summary>Starts the description patcher thread (idempotent).</summary>
        public static void StartDescriptionPatcher()
        {
            if (_started) return;
            _started = true;
            BuildPatches();
            new Thread(PatchLoop) { IsBackground = true }.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"WpnDesc: description patcher started ({_patches.Count} weapon entries)");
        }

        static void BuildPatches()
        {
            _hintVanilla = Encode(StatusBreakHintVanilla);
            _hintSeven   = Encode(StatusBreakHintSeven);
            _acquiredResists = Encode(BreakAcquiredResists);
            _patches = new List<Patch>();
            foreach (WeaponData[] family in new[] { ToanWeapons.All, XiaoWeapons.All, GoroWeapons.All,
                                                    RubyWeapons.All, UngagaWeapons.All, OsmondWeapons.All })
                foreach (WeaponData w in family)
                {
                    if (w.MesEntry <= 0 || string.IsNullOrEmpty(w.ModDescription)) continue;
                    try
                    {
                        _patches.Add(new Patch { Id = w.Id, Name = w.Name, MesEntry = w.MesEntry,
                                                 Desc = Encode(w.ModDescription) });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"WpnDesc: {w.Name} (id {w.Id}) description not encodable — skipped ({ex.Message})");
                    }
                }
        }

        static void PatchLoop()
        {
            while (true)
            {
                try { PatchTick(); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "WpnDesc error: " + ex.Message); }
                Thread.Sleep(PollMs);
            }
        }

        static void PatchTick()
        {
            int p = Memory.ReadInt(MesTextPtrSlots[0]);
            if (!IsRamPtr(p)) { _lastPtr = p; _lastCnt = -1; _sigAddr = 0; return; }

            // Repatch when a new buffer registers, when its entry count changes (the async BG read completing,
            // or a different bank landing at the same address), or when the content reverted to vanilla (menu
            // re-opened into the same arena slot — the signature word only differs from vanilla for entries the
            // user actually edited, so unedited tables never trigger rewrites).
            long b = Memory.ToMmu(p);
            int cnt = Memory.ReadUShort(b);
            bool fresh = p != _lastPtr || cnt != _lastCnt;
            if (!fresh && (_sigAddr == 0 || Memory.ReadUShort(_sigAddr) == _sigWord)) return;
            _lastPtr = p; _lastCnt = cnt; _sigAddr = 0;

            int delta = BankOrdinalDelta(cnt);
            if (delta < 0) return;                                         // unknown bank / still loading (count change re-triggers)

            // Read the whole blob once (batched), then do all locating/diffing locally.
            ushort[] w = ReadBlob(b, MaxBlobBytes);
            int textBase = (4 + cnt * 4) / 2;                              // first text WORD index

            // Entry starts by terminator ordinal.
            var starts = new int[cnt + 2];
            starts[0] = textBase;
            int found = 0;
            for (int i = textBase; i < w.Length && found <= cnt; i++)
                if (w[i] == 0xFF01) starts[++found] = i + 1;

            // Glyph-plane check on the first weapon entry: its first non-newline code must be the 0xFD-plane
            // quote. The dunmenu3/dunmenu_chk variants use a different encoding (plane 0x00) — skip those.
            int firstWeaponOrd = 204 - delta;
            if (firstWeaponOrd >= found) return;
            int probe = SkipNewlines(w, starts[firstWeaponOrd]);
            if (probe < 0 || w[probe] != 0xFD57)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"WpnDesc: buffer @0x{p:X8} uses a foreign glyph encoding (code 0x{(probe >= 0 ? w[probe] : 0):X4}) — not patched");
                return;
            }

            int patched = 0, words = 0;
            long sigAddr = 0; ushort sigWord = 0;
            foreach (Patch pa in _patches)
            {
                int ord = pa.MesEntry - delta;
                if (ord <= 0 || ord + 1 > found) continue;                 // not present in this bank
                int s = starts[ord], e = starts[ord + 1] - 1;              // e = the 0xFF01 terminator index

                int q = SkipNewlines(w, s);
                if (q < 0 || q >= e || w[q] != 0xFD57) continue;           // no quoted name — unexpected shape, skip
                while (q < e && w[q] != 0xFF00) q++;                       // end of the name line
                if (q >= e) continue;                                      // no description region
                int descStart = q + 1;                                     // description words: [descStart, e)

                // Build the target: encoded lines, truncated to fit, padded with spaces.
                int span = e - descStart;
                if (span <= 0) continue;
                ushort[] target = new ushort[span];
                int n = Math.Min(span, pa.Desc.Length);
                Array.Copy(pa.Desc, target, n);
                if (pa.Desc.Length > span)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"WpnDesc: {pa.Name} description too long for this copy ({pa.Desc.Length} > {span} glyphs) — truncated");
                for (int i = n; i < span; i++) target[i] = 0xFF02;         // pad with spaces

                // Diff-write: only words that differ (unedited placeholders == vanilla → zero writes).
                bool changed = false;
                for (int i = 0; i < span; i++)
                {
                    if (w[descStart + i] == target[i]) continue;
                    Memory.WriteUShort(b + (descStart + i) * 2L, target[i]);
                    words++; changed = true;
                }
                if (changed)
                {
                    patched++;
                    if (sigAddr == 0) { sigAddr = b + descStart * 2L; sigWord = target[0]; }
                }
            }

            // Dynamic system entries (allmenu banks only — the shop bank doesn't contain them):
            // the Status Break hint (47) and the SynthSphere "acquired" line (179), each swapped
            // between vanilla and its 7 Branch Sword text. Targets are exact-span replacements;
            // a span mismatch means an unexpected bank layout and the entry is skipped.
            var dynamicEntries = new (int Ordinal, ushort[] Target, bool NonVanilla)[]
            {
                (StatusBreakHintEntry, PadTo(_hintSevenActive ? _hintSeven : _hintVanilla,
                                             _hintVanilla.Length), _hintSevenActive),
                (BreakAcquiredEntry, _acquiredResistsActive ? PadTo(_acquiredResists, _acquiredVanilla.Length)
                                                            : _acquiredVanilla, _acquiredResistsActive),
            };
            if (delta == 0)
            {
                foreach (var (ordinal, target, nonVanilla) in dynamicEntries)
                {
                    if (ordinal + 1 > found) continue;
                    int s = starts[ordinal], e = starts[ordinal + 1] - 1;
                    if (e - s != target.Length || w[s] != 0xFF00)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"WpnDesc: dynamic entry {ordinal} has unexpected shape (span {e - s}) — not patched");
                        continue;
                    }
                    for (int i = 0; i < target.Length; i++)
                    {
                        if (w[s + i] == target[i]) continue;
                        Memory.WriteUShort(b + (s + i) * 2L, target[i]);
                        words++;
                        if (sigAddr == 0 && nonVanilla) { sigAddr = b + (s + i) * 2L; sigWord = target[i]; }
                    }
                }
            }

            _sigAddr = sigAddr; _sigWord = sigWord;
            if (words > 0)
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"WpnDesc: patched {patched} description(s) + status-break hint ({(_hintSevenActive ? "7BS" : "vanilla")}) " +
                    $"({words} words) in {(delta == 0 ? "menu" : "shop")} bank @0x{p:X8}");
        }

        static int SkipNewlines(ushort[] w, int i)
        {
            while (i < w.Length && w[i] == 0xFF00) i++;
            return i < w.Length ? i : -1;
        }

        /// <summary>Pads an encoded text with spaces (0xFF02) to exactly <paramref name="span"/> words.</summary>
        static ushort[] PadTo(ushort[] text, int span)
        {
            if (text.Length == span) return text;
            var outw = new ushort[span];
            Array.Copy(text, outw, Math.Min(text.Length, span));
            for (int i = text.Length; i < span; i++) outw[i] = 0xFF02;
            return outw;
        }

        // Read `bytes` of the blob at MMU addr `b` as a ushort[] via batched u32 reads.
        static ushort[] ReadBlob(long b, int bytes)
        {
            const int ChunkWords = 2048;                                   // u32s per batch (8KB)
            int total = bytes / 4;
            var outw = new ushort[total * 2];
            for (int done = 0; done < total; done += ChunkWords)
            {
                int n = Math.Min(ChunkWords, total - done);
                uint[] chunk = Memory.ReadUIntBatch(b + done * 4L, n);
                for (int i = 0; i < n; i++)
                {
                    outw[(done + i) * 2]     = (ushort)(chunk[i] & 0xFFFF);
                    outw[(done + i) * 2 + 1] = (ushort)(chunk[i] >> 16);
                }
            }
            return outw;
        }

        /// <summary>Encode an ASCII description ('\n' = line break) into meswin glyph codes
        /// (docs/weapon-descriptions.md; mirrors tools/mes_decode.py encode()).</summary>
        internal static ushort[] Encode(string s)
        {
            var outw = new List<ushort>(s.Length);
            foreach (char ch in s)
            {
                if (ch == '\n') { outw.Add(0xFF00); continue; }
                if (ch == ' ')  { outw.Add(0xFF02); continue; }
                int g = ch switch
                {
                    '\'' => 0x55, '"' => 0x57, '&' => 0x5B, '-' => 0x5D, '/' => 0x5F,
                    '(' => 0x61, ')' => 0x62, ',' => 0x6C, '.' => 0x6D,
                    // Verified from vanilla text (status-break hint, entry 47): NOT on the
                    // linear '!'..'Z' plane despite falling in its ASCII range.
                    '%' => 0x60, '?' => 0x59,
                    >= '0' and <= '9' => ch - '0' + 0x6F,
                    >= '!' and <= 'Z' => ch - 0x20,
                    >= 'a' and <= 'z' => ch - 0x26,
                    _ => throw new ArgumentException($"unencodable char '{ch}'")
                };
                outw.Add((ushort)(0xFD00 | g));
            }
            return outw.ToArray();
        }

        static bool IsRamPtr(int p) => (uint)p >= 0x80000 && (uint)p < Memory.EeRamSize;
    }
}
