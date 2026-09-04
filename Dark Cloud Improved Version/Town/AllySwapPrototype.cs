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

        /// <summary>BISECT: the swap freeze has the SAME fingerprint with or without the texture purge
        /// (wild jump into data → undefined-syscall kernel spin; regs a0=0xA a1=0x8000 a2=0x1C75520 = the
        /// texture manager, every crash; emulog: 0xC8-byte copy from NULL). So the purge is neither cause
        /// nor fix. Default OFF to remove it as a variable while we chase the real texture-manager corrupt
        /// pointer (differential: mgr header ptrs @0x1C78F88/0x1C7B0B4 zeroed in frozen vs valid in healthy).</summary>
        internal static bool PurgeEnabled = false;

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
                _lastMap = map; _installedStb = 0; _probedMap = -1;
                _currentAlly = 0; _pendingAlly = -1;   // a fresh town load starts as Toan (entry character)
                Memory.WriteInt(CodeCaves.Mailbox.SkipCharShadow, 0);   // town loads must run WITH shadows
                _shadowSkipClearAfter = DateTime.MinValue;
                _purgePending = DateTime.MinValue;
            }

            ProbeArenas();
            CheckAllocOverflow();
            CheckReadInfoBreadcrumb();
            EnsureInstalled();

            DetectCommit();
            MaybeFirePending();
            MaybePurgeBehindBlack();
            MaybeClearShadowSkip();
        }

        /// <summary>ELF <c>fade_end</c> (EdFadeInOut's fully-black latch @gp-0x6DF4; the canal-evict native
        /// hook keys on its store) — 1 once a fade-out has reached full black.</summary>
        private const long FadeEnd = 0x202A29FC;
        private static DateTime _purgePending = DateTime.MinValue;
        private static DateTime _shadowSkipClearAfter = DateTime.MinValue;

        /// <summary>Clear the shadow-skip mailbox once the swap has settled back to walking (earliest 2 s
        /// after fire, so the load definitely ran under it) — town-entry loads must see it CLEAR so normal
        /// characters keep their shadows.</summary>
        private static void MaybeClearShadowSkip()
        {
            if (_shadowSkipClearAfter == DateTime.MinValue) return;
            if (DateTime.UtcNow < _shadowSkipClearAfter) return;
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking &&
                DateTime.UtcNow < _shadowSkipClearAfter.AddSeconds(13)) return;   // wait for walking, 15 s cap
            Memory.WriteInt(CodeCaves.Mailbox.SkipCharShadow, 0);
            _shadowSkipClearAfter = DateTime.MinValue;
        }

        /// <summary>The swap script fades out, waits for full black, then idles ~20 frames before the load —
        /// that idle is OUR window: purge the outgoing character's texture entries the moment
        /// <see cref="FadeEnd"/> reports fully black. Behind black the (transiently texture-less) old model
        /// is invisible, nothing has moved for other blocks, and the load starts with clean block-8 state.</summary>
        private static void MaybePurgeBehindBlack()
        {
            if (_purgePending == DateTime.MinValue) return;
            if (DateTime.UtcNow > _purgePending)   // safety: never leave a stale pending purge armed
            {
                _purgePending = DateTime.MinValue;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "purge window expired without a black frame — purge skipped");
                return;
            }
            if (Memory.ReadInt(FadeEnd) == 0) return;   // not fully black yet
            _purgePending = DateTime.MinValue;
            PurgeCharacterTextures();
        }

        private static int _lastCfgCmd = -2;

        /// <summary>The .chr cfg-command names, by dispatch index (handler table @0x251470, resolved via the
        /// symbol table — VERTEX_ANIME..EVENT). The breadcrumb mailbox holds the LAST index dispatched.</summary>
        private static readonly string[] CfgCmdNames =
        {
            "VERTEX_ANIME", "SHADOW_VERTEX_ANIME", "MODEL", "SHADOW_MODEL", "MOTION", "SHADOW_MOTION",
            "KEY", "KEY_START", "MOTION_END", "CLOTH", "BODY_SIZE", "ALLOC_MDT", "ALLOC_DBUFF",
            "ALLOC_SHADOW_MDT", "ALLOC_SHADOW_DBUFF", "IMG", "IMG_END", "FOOT", "EVENT",
        };

        /// <summary>Log the ReadInfo cfg-command breadcrumb (PatchReadInfoBreadcrumb) on change. The load runs
        /// many commands inside one game frame, so at 50 ms ticks we mostly sample the LAST dispatched — which
        /// is exactly what matters on a freeze: the value frozen in the mailbox names the hanging command.
        /// <see cref="MaybeFirePending"/> resets the latch so the post-swap value always logs, even when it
        /// equals the pre-swap one.</summary>
        private static void CheckReadInfoBreadcrumb()
        {
            int cmd = Memory.ReadInt(CodeCaves.Mailbox.ReadInfoCmd);
            if (cmd == _lastCfgCmd) return;
            _lastCfgCmd = cmd;
            string name = cmd >= 0 && cmd < CfgCmdNames.Length ? CfgCmdNames[cmd] : "?";
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cfg-cmd breadcrumb: [{cmd}] {name}");
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

            Memory.WriteInt(CodeCaves.Mailbox.AllocProbeArena, 0);   // clear the overflow probe for this swap
            _lastOverflowArena = 0;
            Memory.WriteInt(CodeCaves.Mailbox.ReadInfoCmd, -1);      // re-arm the breadcrumb so the frozen value always logs
            _lastCfgCmd = -2;
            // Arm the behind-black purge. fade_end is a COMPLETION LATCH left at 1 by the PREVIOUS swap's
            // fade — a level check fired instantly at commit (textures vanished before any black; crash,
            // savestate 3). CLEAR it now and wait for THIS swap's fade-out to set it fresh (rising edge).
            Memory.WriteInt(FadeEnd, 0);
            _purgePending = PurgeEnabled ? DateTime.UtcNow.AddSeconds(5) : DateTime.MinValue;
            // BISECT: shadow-skip DISABLED (it never actually gated ArrangeShadowMDT, which runs from the
            // MODEL command's CreateVisual, not CommandSHADOW_MODEL). Leaving the mailbox clear = normal
            // shadows + CommandSHADOW_MODEL runs. Re-enable only if a dump proves it's needed.
            _shadowSkipClearAfter = DateTime.MinValue;
            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);
            WriteScript(stb, lab.Off, lab.Off + lab.Size, BuildSwapBytecode(chr, cfg), $"in-place ally swap → {chr}");
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            CanalWading.SuppressForSwap();   // hold the Queens canal early-draw off the model root until the swap event completes (else stale-root draw hangs)
            _currentAlly = ally;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"fired event {AllySwapLabelId} → {name} (texmgr entries {Memory.ReadInt(TexMgr)})");
        }

        // CTextureManager (0x1C75870): +0 = last-entry index; entries @+0x10F8, stride 0x50, 0xC4 slots
        // (u16 blockId @+0x00, NUL name @+0x08, native pixel ptr @+0x38, CLUT ptr @+0x48 — the SuperSteve
        // texture-swap RE). Character textures are registered under BLOCK 8 (EdLoadMainChara →
        // LoadPackData2(..., texture_block=8, ...)).
        private const long TexMgr = 0x21C75870;
        private const long TexEntryBase = TexMgr + 0x10F8;
        private const int  TexEntryStride = 0x50, TexEntryCount = 0xC4;
        private const int  CharTexBlockId = 8;
        private const long CharTexBlockAddr = TexMgr + 0x18 + CharTexBlockId * 0x3C;

        /// <summary>
        /// THE REPEATED-SWAP FIX. Every _LOAD_MAIN_CHARA registers the character's textures in the global
        /// CTextureManager with pointers into the texture arena — and the engine's own char-change path
        /// (the dungeon menu) calls MenuTextureDelete + CleanUpTextureList BEFORE loading the next
        /// character. Our in-place swap skipped that, so after one swap the manager held STALE entries
        /// whose pointers dangle into rewound-and-overwritten arena memory; the next load's
        /// SearchTextureName then walks poison and the game dies on a wild jump (proven by savestate
        /// forensics: SYSCALL exception, EPC in SearchTextureName, texture-manager pointers in the regs).
        ///
        /// Replicates DeleteTextureBlock(mgr, 8) + CleanUpTextureList(mgr) (0x133700 / 0x133A60) as pure
        /// data writes: reset the block-8 CTextureBlock, Initialize every entry whose blockId == 8, then
        /// compact live entries down over the holes exactly as CleanUp does (indices 1..0xC2).
        /// </summary>
        private static void PurgeCharacterTextures()
        {
            byte[] entries = Memory.ReadBytesBatch(TexEntryBase, TexEntryCount * TexEntryStride);
            if (entries == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "texture purge: entry read failed — skipped"); return; }

            // Delete-only, NO compaction. CleanUp's compaction MOVES other blocks' entries, and live code
            // (scene/villager texture uploads) holds cached pointers/indices into the table — compacting
            // mid-town scrambled them (garbled textures then a wild-jump crash, savestate slot 2). The
            // engine itself uses standalone DeleteTextureBlock (holes left in place) outside menus.
            int deleted = 0;
            for (int i = 0; i < TexEntryCount; i++)
            {
                int off = i * TexEntryStride;
                if (BitConverter.ToUInt16(entries, off) != CharTexBlockId) continue;
                InitTextureEntry(entries, off);
                deleted++;
            }

            Memory.WriteBytesBatch(TexEntryBase, entries);

            // Do NOT touch the block-8 CTextureBlock struct. It holds the GS-side layout (VRAM base /
            // upload-chain pointers) that EditInit set up ONCE and the character load REUSES — zeroing it
            // sent the first draw's texture-upload DMA down a NULL chain (the emulog's
            // `TLB Miss addr=0x0..0xc4` kernel walk = every purge-era freeze, one frame after the load).

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"texture purge: {deleted} block-{CharTexBlockId} entries dropped (block struct untouched)");
        }

        /// <summary>Initialize__8CTexture (0x130F20) field-for-field: blockId/u16s at +0..+6, name[0] @+8,
        /// and the pointer/GS fields at +0x28..+0x4C. The name TAIL (+0x09..+0x27) is left as-is, same as
        /// the engine.</summary>
        private static void InitTextureEntry(byte[] buf, int off)
        {
            for (int o = 0; o < 8; o++) buf[off + o] = 0;         // +0x00..+0x07 (blockId + 3 u16s)
            buf[off + 8] = 0;                                      // name[0]
            for (int o = 0x28; o < 0x50; o++) buf[off + o] = 0;    // +0x28..+0x4F (ptrs, sizes, GS addrs)
        }

        private static int _probedMap = -1;
        // Every CDataAlloc2 arena SetDataBuffer carves in EditInit (+ the villager pools), so the resident
        // snapshot shows which SMALL buffer a big character's load would overrun (Ungaga froze right after
        // its geometry fit the enlarged 0x1D3A060, so the overflow is elsewhere — a motion/dbuff/cloth arena).
        private static readonly long[] _probeArenas =
        {
            0x21D3A050, 0x21D3A060, 0x21D3A070, 0x21D3A080,
            0x21D1B360, 0x21D1B370, 0x21D1B390, 0x21D1B3A0,
            0x21D331B0, 0x212AB010, 0x212AB020, 0x212AB030,
        };
        private static readonly string[] _probeNames =
        {
            "0x1D3A050(19M)", "0x1D3A060(main-char)", "0x1D3A070", "0x1D3A080(texture)",
            "0x1D1B360(fish/villager)", "0x1D1B370", "0x1D1B390(13000)", "0x1D1B3A0(16000)",
            "0x1D331B0(8000)", "0x2AB010(100)", "0x2AB020(0x1edc)", "0x2AB030(10)",
        };
        private static readonly int[] _probeLastUsed = new int[12];

        /// <summary>
        /// DIAGNOSTIC: log each CDataAlloc2 arena's used/cap, and re-log any arena whose USED changes — so a
        /// working Ruby swap reveals which arena a character load actually consumes and by how much (the
        /// geometry arena 0x1d3a060 reads 0 while walking because it is scratch, reset between loads, so we
        /// can't assume from the static snapshot). The freeze is <c>Alloc__14CDataAlloc2</c> (0x1278a0)
        /// spinning on <c>if (cap &lt; used + n) { printf; do{}while(true); }</c>. Struct: [0]=base, [2]=used,
        /// [3]=cap (0x10-byte BLOCKS). Persistent (post-load) deltas are what matter; a transient mid-load
        /// spike inside one game frame won't be caught at the 50 ms tick.
        /// </summary>
        private static void ProbeArenas()
        {
            long stb = TownScript.Base();
            if (stb == 0) return;
            int map = Memory.ReadInt(EditLoop.MapNo);
            bool firstForTown = map != _probedMap;

            for (int i = 0; i < _probeArenas.Length; i++)
            {
                int used = Memory.ReadInt(_probeArenas[i] + 8);
                int cap  = Memory.ReadInt(_probeArenas[i] + 0xC);
                if (firstForTown || used != _probeLastUsed[i])
                {
                    if (firstForTown && i == 0)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"arena probe (MapNo {map}) — models: c01d 850K/Ruby 455K/Ungaga 1067K/turi 1772K:");
                    uint bas = Memory.ReadUInt(_probeArenas[i]);
                    string capStr = cap == -1 ? "UNBOUNDED" : $"{(long)cap * 0x10 / 1024}K";
                    string tag = used != _probeLastUsed[i] && !firstForTown
                        ? $" (Δ {(long)(used - _probeLastUsed[i]) * 0x10 / 1024:+#;-#;0}K)" : "";
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"  {_probeNames[i],-26} base=0x{bas:X8} used={(long)used * 0x10 / 1024}K/{capStr} " +
                        $"(free {(cap == -1 ? "∞" : ((long)(cap - used) * 0x10 / 1024) + "K")}){tag}");
                    _probeLastUsed[i] = used;
                }
            }
            int texCount = Memory.ReadInt(TexMgr);
            if (firstForTown || texCount != _lastTexCount)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                    $"  texmgr entries = {texCount}" + (firstForTown ? "" : $" (Δ {texCount - _lastTexCount:+#;-#;0})"));
                _lastTexCount = texCount;
            }
            _probedMap = map;
        }

        private static int _lastTexCount;
        private static uint _lastOverflowArena;

        /// <summary>Read the Alloc-overflow mailbox (set by PatchAllocOverflowProbe on the exact overflow that
        /// hangs the game). On a swap freeze this names WHICH CDataAlloc2 ran out — match its struct address
        /// against the known arenas so we enlarge the right one.</summary>
        private static void CheckAllocOverflow()
        {
            uint arena = Memory.ReadUInt(CodeCaves.Mailbox.AllocProbeArena) & Memory.PhysAddrMask;
            if (arena == 0 || arena == _lastOverflowArena) return;
            _lastOverflowArena = arena;
            uint reqEnd = Memory.ReadUInt(CodeCaves.Mailbox.AllocProbeSize);
            // Name it if it's one we know (struct addresses are the guest 0x1D3Axxx / 0x1D1Bxxx forms).
            long guest = arena;
            string name = "unknown";
            for (int i = 0; i < _probeArenas.Length; i++)
                if ((_probeArenas[i] & Memory.PhysAddrMask) == guest) { name = _probeNames[i]; break; }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"*** ALLOC OVERFLOW: arena 0x{guest:X8} ({name}) requested end {(long)reqEnd * 0x10 / 1024}K " +
                $"({reqEnd} blk) — THIS buffer is the freeze; enlarge it next.");
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
        /// The swap flow: fade out → wait fully black → ~20-frame idle (the mod's purge window — see
        /// <see cref="MaybePurgeBehindBlack"/>) → capture pos/rot → load into the persistent main-char slot
        /// (flag 0) → re-place + idle → fade in. The purge must land before the load and behind black; the
        /// idle frames give the 50 ms mod tick ample margin.
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
