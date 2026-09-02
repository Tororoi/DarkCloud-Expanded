using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;
using static Dark_Cloud_Improved_Version.FishingLabelAllocator;
using static Dark_Cloud_Improved_Version.FishingScriptBuilder;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// PROTOTYPE: in-place ally model swap, no town reload — the successor to the EditInit-reload
    /// ally switch (which respawns at the entrance and disturbs town state). It reuses the EXACT vehicle
    /// the fishing enter/exit uses to swap Toan ↔ the rod-holding Toan: the STB command
    /// <c>_LOAD_MAIN_CHARA(chr, cfg, 0)</c> (flag 0 = the persistent main-character allocator @0x1D3A060,
    /// which self-rewinds on every load, so repeated swaps cannot leak), wrapped in the same
    /// _GET_POSITION/_GET_ROTATION → load → _SET_POSITION/_SET_ROTATION capture-and-replace that the
    /// fishing exit uses so the load's position reset is undone in the same frame.
    ///
    /// The whole thing runs as a SIMPLE (non-yielding) event: writing the label id to
    /// <see cref="EditLoop.StartEventNo"/> while walking makes EditLoop launch it, and because it never
    /// yields it runs to completion inside that launch and returns straight to walking mode — the swap and
    /// the re-placement happen atomically, one frame, no visible flicker at the origin.
    ///
    /// Test binding: R3 (right stick click) swaps the current town character to Ungaga (has cloth — the
    /// stress case for an in-place swap). L3 is already the fish-data-farmer toggle, so R3 is used here.
    /// Requires the ISO to be re-patched so label <see cref="AllySwapLabelId"/> (405) is baked into the
    /// town's event.stb (added to StbLabelBaker's spare-label set for the fishing towns; Queens is one).
    /// </summary>
    internal static class AllySwapPrototype
    {
        internal static bool Enabled = true;

        private const string Tag = "[AllySwap] ";

        // DIAGNOSTIC target selector. All allies use cfg "info.cfg" (the reload-based switch feeds EditInit
        // these exact chr paths). Sizes (from data.hd2): c01d 850K (resident/same skeleton) · Ruby c05a 455K
        // (DIFFERENT skeleton, SMALLER) · Ungaga c10a 1.07M (different skeleton, LARGER).
        // Ruby vs c01d isolates SIZE (Ungaga overflows a buffer) from CHARACTER (different skeleton itself):
        //   Ruby works → the freeze is Ungaga's SIZE; Ruby freezes → it's the different character/skeleton.
        internal enum Target { C01d, Ruby, Ungaga, Turi }
        // Ungaga = the model that overran the 625K texture buffer. With the PatchAllyTextureBudget ELF patch
        // (texture buffer 625K→1562K), it should now fit. NEEDS ISO RE-PATCH for that ELF change to take effect.
        internal static Target DiagTarget = Target.Ungaga;

        private static (string chr, string cfg, string name) TargetFiles() => DiagTarget switch
        {
            Target.C01d   => ("chara/c01d.chr",           "info.cfg",      "c01d/Toan"),
            Target.Ruby   => ("gedit/e03/chara/c05a.chr",  "info.cfg",      "Ruby (c05a, 455K)"),
            Target.Ungaga => ("gedit/s79/chara/c10a.chr",  "info.cfg",      "Ungaga (c10a, 1.07M)"),
            // c01d_turi = Toan's fishing model: 1.8M (BIGGER than Ungaga) but Toan's OWN base skeleton.
            // Big + same-skeleton → if it works, the freeze is Ungaga-specific, not size; if it freezes, size.
            _             => ("chara/c01d_turi.chr",       "c01d_turi.cfg", "c01d_turi (1.8M, same skeleton)"),
        };

        private const long ButtonInputs = 0x21CBC544;   // same pad word TownCharacter reads
        private const int  R3Bit        = 0x400;         // L3=0x200 (taken by FishDataFarmer), R3=0x400

        private static bool _prevBtn;
        private static long _installedStb;   // stb base label 405 was written into (0 = not installed for this town)
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
            if (map != _lastMap) { _lastMap = map; _installedStb = 0; _probedMap = -1; }   // town (re)loaded

            ProbeArenas();
            CheckAllocOverflow();
            EnsureInstalled();

            int buttons = Memory.ReadInt(ButtonInputs);
            bool btn = (buttons & R3Bit) != 0;
            if (btn && !_prevBtn) Fire();
            _prevBtn = btn;
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
            _probedMap = map;
        }

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

        /// <summary>Write the swap script into the ISO-baked spare label 405 once per town load. Uses the
        /// baked label directly (no hijack-allocator interaction with CustomFishingSpot's install).</summary>
        private static void EnsureInstalled()
        {
            long stb = TownScript.Base();
            if (stb == 0 || _installedStb == stb) return;

            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            if (labelCount <= 0 || labelCount > 4096 || tbl <= 0) return;   // stb not built yet

            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, AllySwapLabelId);
            if (lab == null) return;   // ISO not re-patched with the 405 spare yet — nothing to do

            Memory.WriteInt(CodeCaves.Mailbox.AllocProbeArena, 0);   // clear the overflow probe for this town
            _lastOverflowArena = 0;

            var (chr, cfg, name) = TargetFiles();
            Memory.WriteInt(stb + lab.Entry, AllySwapLabelId);
            WriteScript(stb, lab.Off, lab.Off + lab.Size, BuildSwapBytecode(chr, cfg),
                        $"in-place ally swap → {chr}");
            _installedStb = stb;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"swap label {AllySwapLabelId} installed (stb 0x{stb:X}, code @+0x{lab.Off:X}) — R3 swaps to {name}");
        }

        private static void Fire()
        {
            if (_installedStb == 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                    "R3 pressed but the swap label is not installed (ISO re-patch needed for baked label 405)");
                return;
            }
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "R3 ignored — not in walking mode");
                return;
            }
            // Launch the event: EditLoop sees start_event_no > 0 next frame and runs the label. A simple
            // (non-yielding) script completes inside that launch and drops back to walking.
            Memory.WriteInt(EditLoop.StartEventNo, AllySwapLabelId);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"fired event {AllySwapLabelId} (target {TargetFiles().name})");
        }

        /// <summary>
        /// Clean simple swap — the version proven to work for the c01d control. Real yielding event: capture
        /// pos/rot, load the model into the persistent main-char slot (flag 0), re-place, idle. NO buffer
        /// clears / fade / villager reload (those were tested and did NOT fix the larger-model freeze). The
        /// <see cref="DiagTarget"/> selector decides which model this loads, to isolate size vs. character.
        /// </summary>
        internal static StbWriter BuildSwapBytecode(string chr, string cfg)
        {
            var w = new StbWriter();
            w.UseLocals(8);   // v2..v4 = pos, v5..v7 = rot

            w.Yield();                                    // become a REAL event; the fishing exit yields twice
            w.Yield();

            w.PushInt(StbCommands.SetNpcMotion); w.PushInt(-1); w.PushInt(0); w.Ext(3);   // idle the old model

            EmitWorldCoordReset(w);                       // GET in plain world space
            w.PushInt(StbCommands.GetNpcPos); w.PushInt(-1);
            w.PushVarRefFloat(2); w.PushVarRefFloat(3); w.PushVarRefFloat(4); w.Ext(5);
            w.PushInt(StbCommands.GetNpcRot); w.PushInt(-1);
            w.PushVarRefFloat(5); w.PushVarRefFloat(6); w.PushVarRefFloat(7); w.Ext(5);

            w.PushInt(StbCommands.LoadMainChara);         // 999, flag 0 = persistent main-char allocator
            w.PushString(chr); w.PushString(cfg); w.PushInt(0); w.Ext(4);
            w.Yield();                                    // let the load settle (the fishing enter yields here)

            EmitWorldCoordReset(w);                       // re-assert identity after the load
            w.PushInt(StbCommands.SetNpcPos); w.PushInt(-1);
            w.PushVarFloat(2); w.PushVarFloat(3); w.PushVarFloat(4); w.Ext(5);
            w.PushInt(StbCommands.SetNpcRot); w.PushInt(-1);
            w.PushVarFloat(5); w.PushVarFloat(6); w.PushVarFloat(7); w.Ext(5);
            w.PushInt(StbCommands.SetNpcMotion); w.PushInt(-1); w.PushInt(0); w.Ext(3);   // idle the new model

            w.Ret();                                      // natural end → EdEventFinish → back to walking
            return w;
        }
    }
}
