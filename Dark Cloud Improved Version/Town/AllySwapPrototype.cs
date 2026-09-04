using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;
using static Dark_Cloud_Improved_Version.FishingLabelAllocator;
using static Dark_Cloud_Improved_Version.FishingScriptBuilder;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// In-place ally model swap, no town reload — the successor to the EditInit-reload ally switch (which
    /// respawns at the entrance and disturbs town state). Reuses the fishing enter/exit vehicle: the STB
    /// command <c>_LOAD_MAIN_CHARA(chr, cfg, 0)</c> (flag 0 = the persistent main-character allocator
    /// @0x1D3A060), wrapped in _GET_POSITION/_GET_ROTATION → load → _SET_POSITION/_SET_ROTATION so the load's
    /// position reset is undone. It runs as a real yielding event fired via <see cref="EditLoop.StartEventNo"/>.
    /// The town character buffers were enlarged (ElfPatches.PatchAllyTextureBudget) so any ally fits.
    ///
    /// COMMIT WIRING: the pnach's `jal EditInit` reload at 0x1F7DB4 is now a NOP (see A5C05C78.pnach), so
    /// committing an ally in the town party menu no longer reloads. This class detects the commit — a Cross
    /// press in the allies menu on an UNLOCKED ally different from the current one — queues it, and fires the
    /// in-place swap the moment the menu closes and walking resumes (the event can only run from walking).
    /// The baked spare label <see cref="AllySwapLabelId"/> (405) holds the script; its chr is rewritten per
    /// swap for the selected ally. NEEDS the ISO patched (label 405 + the buffer enlargements).
    /// </summary>
    internal static class AllySwapPrototype
    {
        internal static bool Enabled = true;

        private const string Tag = "[AllySwap] ";

        // Queens LOW TIDE arms the canal-wading early-draw cave on the player's model root (chara+0xBC). The
        // swap's _LOAD_MAIN_CHARA frees/rebuilds that root, so the cave must be held OFF it while the swap
        // event runs — else it draws a stale/half-built root and HANGS (the "swap froze when morning came in
        // Queens" freeze; medium/high-tide swaps, cave disarmed, never froze). CanalWading.SuppressForSwap
        // tracks the event via GameMode and re-arms the instant it completes. Harmless outside Queens/low-tide.

        // Party-menu cursor (0x1D90470) → (chr path, cfg, name). Same paths the old reload switch fed EditInit.
        private static readonly (string chr, string cfg, string name)[] Allies =
        {
            ("chara/c01d.chr",             "info.cfg", "Toan"),
            ("gedit/e01/chara/c04pcat.chr", "info.cfg", "Xiao"),
            ("gedit/s01/chara/c06p.chr",    "info.cfg", "Goro"),
            ("gedit/e03/chara/c05a.chr",    "info.cfg", "Ruby"),
            ("gedit/s79/chara/c10a.chr",    "info.cfg", "Ungaga"),
            ("gedit/e05/chara/c18p.chr",    "info.cfg", "Osmond"),
        };

        private const long ButtonInputs = 0x21CBC544;   // pad word TownCharacter reads
        private const int  CrossBit      = 0x40;          // Button.Cross
        private const long MenuCursor    = 0x21D90470;    // party-menu highlighted char (0=Toan..5=Osmond)
        private const long AllyCount     = 0x21CD9551;    // unlocked-ally count (pnach's unlock gate)
        private const int  AlliesMenuPage = 3;            // Addresses.selectedMenu value for the party page
        private const int  FactoryMapNo   = 4;            // pnach beeps here — no town switching in the factory

        private static bool _prevCross;
        private static int  _pendingAlly = -1;   // ally cursor committed in the menu, awaiting walking-resume
        private static int  _currentAlly;        // who the town character currently is (0=Toan on town load)
        private static long _installedStb;       // stb base label 405 was written into (0 = not this town)
        private static int  _lastMap = -1;

        internal static void Tick()
        {
            if (!Enabled) return;
            try { TickCore(); }
            catch (Exception e)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
            }
        }

        private static void TickCore()
        {
            if (Memory.ReadByte(Addresses.mode) != 2) return;   // town only

            int map = Memory.ReadInt(EditLoop.MapNo);
            if (map != _lastMap)
            {
                _lastMap = map; _installedStb = 0;
                _currentAlly = 0; _pendingAlly = -1;   // a fresh town load starts as Toan (entry character)
            }

            EnsureInstalled();
            DetectCommit();
            MaybeFirePending();
        }

        /// <summary>In the party menu, a Cross press on an unlocked ally different from the current town
        /// character queues an in-place swap (fired once the menu closes — the event needs walking mode).</summary>
        private static void DetectCommit()
        {
            bool inMenu = Memory.ReadByte(Addresses.selectedMenu) == AlliesMenuPage;
            bool cross = (Memory.ReadInt(ButtonInputs) & CrossBit) != 0;

            if (inMenu && cross && !_prevCross && Memory.ReadInt(EditLoop.MapNo) != FactoryMapNo)
            {
                int cursor = Memory.ReadByte(MenuCursor);
                int count = Memory.ReadByte(AllyCount);
                bool unlocked = cursor == 0 || count > cursor;   // Toan always; ally N needs count > N (pnach gate)
                if (cursor >= 0 && cursor < Allies.Length && unlocked && cursor != _currentAlly)
                {
                    _pendingAlly = cursor;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"commit: {Allies[cursor].name} (cursor {cursor}) queued — swaps when the menu closes");
                }
            }
            _prevCross = cross;
        }

        /// <summary>Once a swap is queued and the menu has closed back to walking, write the selected ally's
        /// script into label 405 and fire it.</summary>
        private static void MaybeFirePending()
        {
            if (_pendingAlly < 0) return;
            if (Memory.ReadByte(Addresses.selectedMenu) == AlliesMenuPage) return;   // menu still open
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking) return;
            if (_installedStb == 0) return;

            int ally = _pendingAlly;
            _pendingAlly = -1;
            var (chr, cfg, name) = Allies[ally];

            long stb = TownScript.Base();
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "swap label 405 missing — skipped"); return; }

            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);
            WriteScript(stb, lab.Off, lab.Off + lab.Size, BuildSwapBytecode(chr, cfg), $"in-place ally swap → {chr}");
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            CanalWading.SuppressForSwap();   // hold the Queens canal early-draw off the model root until the swap event completes (else stale-root draw hangs)
            _currentAlly = ally;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"fired event {AllySwapLabelId} → {name}");
        }

        /// <summary>Confirm the ISO-baked spare label 405 is present and claim it once per town load, so
        /// <see cref="MaybeFirePending"/> can rewrite it per swap. Writes a default (Toan) script to prove
        /// the label is usable.</summary>
        private static void EnsureInstalled()
        {
            long stb = TownScript.Base();
            if (stb == 0 || _installedStb == stb) return;

            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            if (labelCount <= 0 || labelCount > 4096 || tbl <= 0) return;   // stb not built yet

            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null) return;   // ISO not re-patched with the 405 spare yet — nothing to do

            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);
            var (chr, cfg, _) = Allies[0];
            WriteScript(stb, lab.Off, lab.Off + lab.Size, BuildSwapBytecode(chr, cfg), "in-place ally swap (default)");
            _installedStb = stb;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"swap label {AllySwapLabelId} ready (stb 0x{stb:X}, code @+0x{lab.Off:X}) — party-menu commit drives it");
        }

        /// <summary>
        /// The swap flow: yield twice → idle the old model → capture pos/rot (world-coord identity so the
        /// GET/SET round-trip is exact) → <c>_LOAD_MAIN_CHARA(flag 0)</c> into the persistent main-char slot →
        /// yield once for the load → re-place at the captured pos/rot → idle the new model. No fade — the swap
        /// is a single-event in-place model replace.
        /// </summary>
        internal static StbWriter BuildSwapBytecode(string chr, string cfg)
        {
            var w = new StbWriter();
            w.UseLocals(8);   // var1 = wait-loop gate; v2..v4 = pos, v5..v7 = rot

            w.Yield();
            w.Yield();

            w.PushInt(StbCommands.SetNpcMotion); w.PushInt(-1); w.PushInt(0); w.Ext(3);   // idle the old model

            EmitWorldCoordReset(w);
            w.PushInt(StbCommands.GetNpcPos); w.PushInt(-1);
            w.PushVarRefFloat(2); w.PushVarRefFloat(3); w.PushVarRefFloat(4); w.Ext(5);
            w.PushInt(StbCommands.GetNpcRot); w.PushInt(-1);
            w.PushVarRefFloat(5); w.PushVarRefFloat(6); w.PushVarRefFloat(7); w.Ext(5);

            w.PushInt(StbCommands.LoadMainChara);         // 999, flag 0 = persistent main-char allocator
            w.PushString(chr); w.PushString(cfg); w.PushInt(0); w.Ext(4);
            w.Yield();

            EmitWorldCoordReset(w);
            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);
            w.PushVarFloat(2); w.PushVarFloat(3); w.PushVarFloat(4); w.Ext(5);
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushVarFloat(5); w.PushVarFloat(6); w.PushVarFloat(7); w.Ext(5);
            w.PushInt(StbCommands.SetNpcMotion); w.PushInt(-1); w.PushInt(0); w.Ext(3);   // idle the new model

            w.Ret();
            return w;
        }
    }
}
