using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// STB bytecode emit primitives for the fishing scripts, built on StbWriter: wait loops, the bait load,
    /// the cursor menu (inline + CALL_FUNC subroutine forms), message show/close, and the small standalone
    /// scripts (bait menu 134, canal-ladder message 402, tide-evict warp 403). Spot-free by design.
    /// 
    /// The STB VM is 12-byte instructions {op, a1, a2}. Push type 1 = int, 2 = float (IEEE bits). EXT (op 21)
    /// takes the STACK ENTRY COUNT in a1, including the command id, which is the first entry. Modelled on
    /// Norune's real scripts (exact offsets: game_data/docs/fishing-engine-re.md §norune-script).
    /// </summary>
    internal static class FishingScriptBuilder
    {
        /// <summary>
        /// The script local that <c>_LOAD_SYNC</c> reports into, so the load loop waits exactly as long as the
        /// disc takes — no more, and crucially no less. Index 1, because the bait menu uses var0 for its
        /// result.
        /// </summary>
        internal const int GateVar = 1;

        /// <summary>
        /// Reset the world coordinate to identity, so <c>_SET_NPC_POS</c> / <c>_SET_NPC_ROT</c> take plain
        /// WORLD coordinates. (Norune passes the pond part's transform instead, because its numbers are
        /// part-local; ours come out of the probe in world space.)
        ///
        /// Call it with NO ARGUMENTS. <c>_SET_WORLD_COORD</c>'s handler branches on the argument count, and
        /// the zero-arg path is exactly this reset — <c>sceVu0UnitMatrix</c> on both matrices. Pushing six
        /// zero floats does the same thing the long way round, for 6 extra instructions.
        /// </summary>
        internal static void EmitWorldCoordReset(StbWriter w)
        {
            w.PushInt(StbCommands.SetWorldCoord);     // 7, with no args = "identity"
            w.Ext(1);
        }

        /// <summary>
        /// <c>while (poll(&amp;v)) YIELD;</c> — wait on something the engine will finish in its own time.
        ///
        /// Both of the game's "are you done yet" commands have the same shape, reporting through a pointer
        /// argument because that is the ONLY way an EXT command can return anything (EXT pushes no result;
        /// <c>SetStack</c> demands a type-3 pointer arg):
        ///
        ///   <see cref="StbCommands.LoadSync"/>  (34)  — a background disc read is still in flight
        ///   <see cref="StbCommands.CheckFade"/> (502) — a fade is still running
        ///
        /// This is what Norune's opaque <c>call_func 400</c> was all along, once the funcdata format fell out:
        /// a four-instruction loop. Counting frames instead is a race — and losing the load one does not look
        /// wrong, it CRASHES (an item frame built from an empty buffer, then a call through a garbage
        /// pointer). See docs/stb-script-format.md.
        /// </summary>
        /// <param name="exitOnNonZero">
        /// WATCH THE POLARITY — the two poll commands report OPPOSITE senses:
        ///
        ///   _LOAD_SYNC  -> ReadBGSync()    : non-zero while a read is STILL PENDING  (exit on zero)
        ///   _CHECK_FADE -> fade_end        : non-zero once the fade has FINISHED     (exit on non-zero)
        ///
        /// They look identical at the call site, which is exactly how the fade wait ended up exiting on its
        /// first iteration: fade_end is 0 the moment EdFadeOut starts, so "loop while non-zero" waited zero
        /// frames and the model swap happened in plain view, before the screen had faded.
        /// </param>
        internal static void EmitWaitLoop(StbWriter w, int pollCommand, bool exitOnNonZero)
        {
            w.UseLocals(GateVar + 1);

            int retry = w.Mark();
            w.PushInt(pollCommand);
            w.PushVarRef(GateVar);
            w.Ext(2);

            w.PushVar(GateVar);
            int done = w.MarkForward();
            if (exitOnNonZero) w.BrTrue(done);
            else w.BrFalse(done);
            w.Yield();
            w.Jmp(retry);
            w.PlaceMark(done);
        }

        /// <summary>
        /// Load the model for the bait in var0 and hang it on the hook. ONLY valid from label 134.
        ///
        /// This must not be emitted into the entry script. It calls <c>_CLEAR_EVENT_BUFF</c>, which rewinds
        /// the bump allocator that <c>_LOAD_FISHING_DATA</c> allocates from — running it after the fishing
        /// load drops the bait on top of fishing.pak and corrupts the arena. See BuildFishingBytecode.
        /// </summary>
        private static void EmitBaitLoad(StbWriter w)
        {
            w.UseLocals(GateVar + 1);

            void PushItem() => w.PushVar(0);        // var0 = whatever the menu chose

            w.PushInt(StbCommands.LoadItemFile);    // 49 — issues a BACKGROUND disc read and returns at once
            PushItem();
            w.Ext(2);

            // WAIT FOR THE READ, rather than betting on a frame count.
            //
            // _LOAD_ITEM builds an item frame from the read buffer, and if the data has not landed it builds
            // one out of nothing — the game then calls through a garbage pointer and dies ("Jump to unaligned
            // address"). A fixed YIELD spin is a race: 5 frames lost it, 10 might, and a slower disc surely
            // would. It was never a cosmetic knob.
            //
            //     while (_LOAD_SYNC(&v)) YIELD;
            //
            // _LOAD_SYNC (34) is the load poll — it pumps the reader and reports whether anything is still in
            // flight. This is EXACTLY what Norune's opaque `call_func 400` turned out to be, once the funcdata
            // format was cracked (see docs/stb-script-format.md).
            EmitWaitLoop(w, StbCommands.LoadSync, exitOnNonZero: false);   // busy while non-zero

            w.PushInt(StbCommands.ClearEventBuff);  // 39
            w.Ext(1);

            w.PushInt(StbCommands.ActiveFileBuffer);// 44
            w.PushInt(0);
            w.PushInt(0);
            w.Ext(3);

            w.PushInt(StbCommands.LoadItem);        // 50 — builds item frame 0
            w.PushInt(0);
            w.Ext(2);
            w.Yield();

            w.PushInt(StbCommands.SetFishingEsa);   // 994
            PushItem();
            w.Ext(2);
        }

        /// <summary>
        /// Label 134 — the REAL bait menu.
        ///
        /// <c>_GOTO_CHANGE_ESA</c> (command 25) drives the game's own use-item menu: it copies a built-in bait
        /// list (so we do not have to supply one) and opens the menu. Its one meaningful argument is a POINTER
        /// to a script local, which the menu writes the chosen item id into. Hence
        /// <see cref="StbWriter.PushVarRef"/>. (game_data/docs/fishing-engine-re.md §fishing-esa)
        ///
        /// The single YIELD after it is enough: while <c>menu_mode != 0</c>, <c>EdEventMode</c> runs the menu
        /// instead of stepping the script, so we resume only once the player has chosen.
        /// </summary>
        internal static StbWriter BuildBaitBytecode()
        {
            var w = new StbWriter();
            w.UseLocals(1);                         // var0 = the chosen bait, written by the menu
            w.Yield();

            w.PushInt(StbCommands.GotoChangeEsa);   // 25
            w.PushVarRef(0);                        // out: var0 <- the item the player picked
            w.Ext(2);
            w.Yield();                              // the menu owns the frames; we wake when it closes

            // ONLY load a bait if the player actually PICKED one. The menu leaves var0 <= 0 on cancel (var0 is
            // a zeroed local, and the engine writes an id only on a real selection). Vanilla's label 134 guards
            // the load with exactly this `var0 > 0` test; without it, cancelling still ran the load and pointed
            // _SET_FISHING_ESA at item 0 — which reads back as the first esa-table entry (Evy) with no model on
            // the hook. The load itself waits on the disc rather than a frame count (crash-safe).
            w.PushVar(0); w.PushInt(0); w.Cmp(StbWriter.CmpGt);
            int cancelled = w.MarkForward();
            w.BrFalse(cancelled);                   // var0 <= 0 -> menu was cancelled, leave the esa untouched
                EmitBaitLoad(w);
            w.PlaceMark(cancelled);

            // GO BACK TO FISHING. Every fishing sub-script runs as a normal event, and when an event RETs,
            // EventMode switches on its return code — whose `default:` branch is `GameMode = 1`, i.e. WALKING.
            // So a script that ends without asking for something specific silently ENDS THE SESSION. That is
            // exactly how label 133 (quit) works, and pressing Square used to quit for the same reason.
            //
            // Asking for fishing again puts EventMode back through `case 0xb: GameMode = 0x10`.
            w.PushInt(StbCommands.GotoFishing);     // 997
            w.Ext(1);

            // NO _FADE_IN. Norune's label 134 has no fade of any kind — picking a bait returns you to fishing
            // instantly. The fade here was mine, copied from the entry script where it belongs.

            w.Ret();
            return w;
        }

        /// <summary>Emit <c>var[idx] = &lt;value pushed by <paramref name="push"/>&gt;</c> as a statement
        /// (STORE re-pushes the stored value, so this drops it with POP).</summary>
        private static void SetVar(StbWriter w, int idx, System.Action push)
        {
            w.PushVarRef(idx); push(); w.Store(); w.Pop();
        }

        /// <summary>
        /// Draw event-mes <paramref name="msgId"/> (window 1 — our injected menu text) as an
        /// <paramref name="count"/>-line cursor menu and block until the player presses X, leaving the chosen
        /// 0-based line in local <paramref name="vSel"/>. The caller then branches on <paramref name="vSel"/>.
        ///
        /// This reproduces Norune's shared fishing-select cursor with the documented VM primitives: the LEFT
        /// ANALOG STICK (LY, ±0.5 threshold, with a "held" edge flag so one push moves one line) AND the d-pad,
        /// X to confirm. No CALL_FUNC and none of the vanilla subroutine's analog-acceleration bulk.
        /// Scratch locals <paramref name="vPad"/>/<paramref name="vLy"/>/<paramref name="vHeld"/>/
        /// <paramref name="vScratch"/> are clobbered.
        /// </summary>
        /// <summary>Inline form: emit the menu straight into the caller's frame, leaving the choice in
        /// <paramref name="vSel"/>. Used only as a fallback when no shared subroutine could be allocated.</summary>
        private static void EmitSelectMenu(StbWriter w, int msgId, int count,
                                           int vSel, int vPad, int vLy, int vHeld, int vScratch)
            => EmitMenuBody(w, () => w.PushInt(msgId), () => w.PushInt(count - 1),
                            vSel, vPad, vLy, vHeld, vScratch);

        /// <summary>
        /// The menu cursor loop, shared by the inline form (<see cref="EmitSelectMenu"/>) and the CALL_FUNC
        /// subroutine (<see cref="BuildMenuSubroutine"/>). msgId and (count-1) are supplied via delegates so
        /// the SAME bytecode serves both a literal-const inline copy and a subroutine reading them from its
        /// arg locals — one implementation to maintain, exactly as vanilla shares its 0x4bd4/0x4264 pair.
        /// </summary>
        private static void EmitMenuBody(StbWriter w, System.Action pushMsgId, System.Action pushCountMinus1,
                                         int vSel, int vPad, int vLy, int vHeld, int vScratch)
        {
            const int Win = 1;
            // Locals is a COUNT (indices 0..count-1), so reserve highest-index + 1 — see WriteScript.
            int maxIdx = System.Math.Max(System.Math.Max(vSel, vPad), System.Math.Max(System.Math.Max(vLy, vHeld), vScratch));
            w.UseLocals(maxIdx + 1);

            // Matches Norune's menu-show + its caller (label 11): tail off, draw the message, position it, and
            // AUTO-PLACE the bubble (this is what was missing — without _SET_MES_AUTOSET the bubble sat top-left).
            w.PushInt(StbCommands.SetMesShippo);    w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.SetMesDrawSpeed); w.PushInt(Win); w.PushFloat(0.0f); w.Ext(3); // 0 = instant (per-char delay); vanilla's select-loop value
            w.PushInt(StbCommands.MesMake);       w.PushInt(Win); pushMsgId(); w.Ext(3);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(9); w.Ext(3);
            w.PushInt(StbCommands.SetMesAutoset); w.PushInt(Win); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.Ext(6);
            SetVar(w, vSel,  () => w.PushInt(0));
            SetVar(w, vHeld, () => w.PushInt(0));

            int loop        = w.Mark();
            int afterAnalog = w.MarkForward();

            w.PushInt(StbCommands.SetMesCursor); w.PushInt(Win); w.PushVar(vSel); w.Ext(3);
            w.Yield();
            w.PushInt(StbCommands.GetApad);   w.PushVarRef(vScratch); w.PushVarRef(vLy); w.Ext(3); // vLy = LY
            w.PushInt(StbCommands.GetPadDown); w.PushVarRef(vPad); w.Ext(2);

            // Analog, edge-detected: LY > 0.5 = down, LY < -0.5 = up; move only on the neutral->pushed edge.
            // ±0.5 is vanilla's exact threshold (dir() subroutine, symmetric, no hysteresis). A wider deadzone
            // would make double-taps re-arm HARDER, not easier — the dip between taps must reach |LY| <= 0.5.
            int notADown = w.MarkForward();
            w.PushVar(vLy); w.PushFloat(0.5f); w.Cmp(StbWriter.CmpGt); w.BrFalse(notADown);
                int adHeld = w.MarkForward();
                w.PushVar(vHeld); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(adHeld);   // held -> only set flag
                    int adSkip = w.MarkForward();
                    w.PushVar(vSel); pushCountMinus1(); w.Cmp(StbWriter.CmpLt); w.BrFalse(adSkip);
                        SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Add(); });
                    w.PlaceMark(adSkip);
                w.PlaceMark(adHeld);
                SetVar(w, vHeld, () => w.PushInt(1));
                w.Jmp(afterAnalog);
            w.PlaceMark(notADown);
            int notAUp = w.MarkForward();
            w.PushVar(vLy); w.PushFloat(-0.5f); w.Cmp(StbWriter.CmpLt); w.BrFalse(notAUp);
                int auHeld = w.MarkForward();
                w.PushVar(vHeld); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(auHeld);
                    int auSkip = w.MarkForward();
                    w.PushVar(vSel); w.PushInt(0); w.Cmp(StbWriter.CmpGt); w.BrFalse(auSkip);
                        SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Sub(); });
                    w.PlaceMark(auSkip);
                w.PlaceMark(auHeld);
                SetVar(w, vHeld, () => w.PushInt(1));
                w.Jmp(afterAnalog);
            w.PlaceMark(notAUp);
            SetVar(w, vHeld, () => w.PushInt(0));   // stick neutral -> re-arm the edge
            w.PlaceMark(afterAnalog);

            // D-pad (already edge events from _GET_PADDOWN): DOWN then UP.
            int notDDown = w.MarkForward();
            w.PushVar(vPad); w.PushInt(StbCommands.PadDown); w.And(); w.BrFalse(notDDown);
                int ddSkip = w.MarkForward();
                w.PushVar(vSel); pushCountMinus1(); w.Cmp(StbWriter.CmpLt); w.BrFalse(ddSkip);
                    SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Add(); });
                w.PlaceMark(ddSkip);
            w.PlaceMark(notDDown);
            int notDUp = w.MarkForward();
            w.PushVar(vPad); w.PushInt(StbCommands.PadUp); w.And(); w.BrFalse(notDUp);
                int duSkip = w.MarkForward();
                w.PushVar(vSel); w.PushInt(0); w.Cmp(StbWriter.CmpGt); w.BrFalse(duSkip);
                    SetVar(w, vSel, () => { w.PushVar(vSel); w.PushInt(1); w.Sub(); });
                w.PlaceMark(duSkip);
            w.PlaceMark(notDUp);

            // X confirms the highlighted line; otherwise loop for another frame.
            w.PushVar(vPad); w.PushInt(StbCommands.PadCross); w.And(); w.BrFalse(loop);
            // Turn the selection cursor OFF: line -1 makes DrawMesWin draw no pointer and no highlight (it gates
            // the cursor on line >= 0). Vanilla's select-loop does exactly this on exit — it's why the no-pole
            // line that follows renders as a plain dialog bubble instead of a menu.
            w.PushInt(StbCommands.SetMesCursor); w.PushInt(Win); w.PushInt(-1); w.Ext(3);
            // Restore pos + tail as vanilla's menu-show/label 11 do. Do NOT _MES_CLOSE here: vanilla leaves the
            // window open so the dispatch path reuses it — the mode change (fishing/quit) clears it, and the
            // no-pole path's _MES_MAKE(21) replaces it in-place. Closing here left msg 21 built-but-not-shown.
            w.PushInt(StbCommands.SetMesPos);    w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.SetMesShippo); w.PushInt(Win); w.PushInt(1); w.Ext(3);
        }

        // The shared menu subroutine's frame: locals 0,1 are the arguments (msgId, count); 2..6 are its
        // scratch. Matches vanilla 0x4bd4 (args in the low locals, cursor state above).
        private const int MenuMsgArg = 0, MenuCountArg = 1;
        private const int MenuSel = 2, MenuPad = 3, MenuLy = 4, MenuHeld = 5, MenuScratch = 6;

        /// <summary>
        /// Build the shared menu-select subroutine — the CALL_FUNC target that both the entry menu and the quit
        /// confirm invoke, exactly as Norune's label 11 and 133 both call its 0x4bd4. Takes (msgId, count) as
        /// stack args and returns the chosen 0-based line ON THE STACK (push then RET), which the caller STOREs.
        /// </summary>
        internal static StbWriter BuildMenuSubroutine()
        {
            var w = new StbWriter();
            w.SetArgs(2);                                        // var0 = msgId, var1 = count
            EmitMenuBody(w, () => w.PushVar(MenuMsgArg),
                            () => { w.PushVar(MenuCountArg); w.PushInt(1); w.Sub(); },
                            MenuSel, MenuPad, MenuLy, MenuHeld, MenuScratch);
            w.PushVar(MenuSel);                                  // vanilla returns v6 the same way: push, then RET
            w.Ret();
            return w;
        }

        /// <summary>
        /// Invoke the shared menu subroutine and leave the choice in <paramref name="destVar"/>. This is
        /// vanilla's call shape verbatim (label 11 / 133): push the destination REF first so it sits below the
        /// args, push the args, CALL_FUNC, then STORE (which writes *destVar and re-pushes the value) and POP.
        /// </summary>
        private static void EmitMenuCall(StbWriter w, int msgId, int count, int destVar, int menuCodeBaseOffset)
        {
            w.UseLocals(destVar + 1);
            w.PushVarRef(destVar);        // ref stays at the bottom of the stack for the trailing STORE
            w.PushInt(msgId);             // arg0 -> callee var0
            w.PushInt(count);             // arg1 -> callee var1
            w.CallFunc(menuCodeBaseOffset);        // returns the chosen line on the stack
            w.Store();                    // *destVar = choice
            w.Pop();
        }

        /// <summary>Emit the entry/quit menu: the shared CALL_FUNC subroutine when one was allocated
        /// (<paramref name="menuCodeBaseOffset"/> &gt;= 0), else an inline copy as a fallback. Either way the choice
        /// lands in <paramref name="destVar"/> for the caller to branch on.</summary>
        internal static void EmitMenu(StbWriter w, int msgId, int count, int destVar, int menuCodeBaseOffset,
                                     int vPad, int vLy, int vHeld, int vScratch)
        {
            if (menuCodeBaseOffset >= 0) EmitMenuCall(w, msgId, count, destVar, menuCodeBaseOffset);
            else EmitSelectMenu(w, msgId, count, destVar, vPad, vLy, vHeld, vScratch);
        }

        /// <summary>The canal-ladder "tide too high" script (label 402). Same prompt-don't-pounce shape as the
        /// fishing entry: it runs every frame while the player is in the point's radius as a SIMPLE EVENT
        /// (returns without yielding → the player keeps walking), draws the "!" prompt and watches the pad, and
        /// only COMMITS — yields — when X is pressed. On X it snaps the player to idle and shows event-mes 23
        /// (EmitShowMessage waits for X, then closes). CanalTide enables this point only at high tide, so it
        /// never competes with the native climb-down (which it enables at low tide instead).</summary>
        internal static StbWriter BuildLadderMessageBytecode()
        {
            var w = new StbWriter();
            w.UseLocals(2);                               // var0 = pad bits, var1 = EmitShowMessage's wait-loop pad var

            w.PushInt(StbCommands.DrawExclamationMark);   // 10 — per-frame; re-asserted on every pass
            w.Ext(1);

            w.PushInt(StbCommands.GetPadDown);            // 1
            w.PushVarRef(0);                              // out: var0 = buttons pressed this frame
            w.Ext(2);
            w.PushVar(0);
            w.PushInt(StbCommands.PadCross);
            w.And();
            int idle = w.MarkForward();
            w.BrFalse(idle);                              // no X -> fall out WITHOUT yielding: a simple event

            // X pressed: snap the player to idle (Norune's label 11 does this before opening a window), then
            // show the line — everything past the branch yields, so the message only fires on a real press.
            w.PushInt(StbCommands.SetNpcMotion);          // 133
            w.PushInt(-1);                                // charaId -1 = the player
            w.PushInt(0);                                 // motion 0 = idle/stand
            w.Ext(3);
            EmitShowMessage(w, LadderMsgId, /*padVar*/1);

            w.PlaceMark(idle);
            w.Ret();
            return w;
        }

        /// <summary>The canal tide-evict script (label 403): a single <c>_MAP_JUMP(EastHarborMapArg)</c> then
        /// RET. CanalTide fires this as an EVENT (writes 403 to start_event_no under the time-change black fade)
        /// when the tide rises on a player caught in the drained canal — the VM runs it, _MAP_JUMP sets the
        /// transition flag, and when the script RETs the event-mode state machine performs the full load to the
        /// East Harbor dock. This is the game's own scripted-warp path (no state-machine poking).</summary>
        internal static StbWriter BuildCanalWarpBytecode()
        {
            var w = new StbWriter();
            w.PushInt(StbCommands.MapJump);   // cmd 15
            w.PushInt(EastHarborMapArg);      // arg0 = mapNo (1-based) → MapNo 19 East Harbor
            w.PushInt(DockSpawnEvent);        // arg1 = StartEventNo → baked s09 label 404 spawns at the dock
            w.Ext(3);                         // 3 stack entries = cmd + 2 args → argCount 2
            w.Ret();
            return w;
        }

        /// <summary>Show event-mes <paramref name="msgId"/> in window 1 (no cursor), wait for X, then close.
        /// This is Norune's no-pole line: window 1, pos 8 (anchors it talk-box style). Two things are essential
        /// here. (1) The caller must NOT have closed the menu window first — the show-flag (ClsMes+0x94) is set
        /// to 1 only at event start, and _MES_MAKE's rebuild path never re-raises it, so a preceding _MES_CLOSE
        /// would leave this built-but-invisible. (2) We WAIT for X: window 1 is only drawn in event mode, so
        /// without staying in the event the line renders for a single frame and vanishes as the event ends.</summary>
        internal static void EmitShowMessage(StbWriter w, int msgId, int padVar)
        {
            const int Win = 1;
            w.UseLocals(padVar + 1);    // self-declare: the inline menu no longer reserves this var for us
            // Tail OFF (flag 0) for the whole display: DrawMesWin_sub draws the shippo tail whenever
            // ClsMes+0x58 != 0, and its anchor sits mid-screen — turning it on here is what put the stray
            // triangle above the player. Vanilla also shows this line with the tail off.
            w.PushInt(StbCommands.SetMesShippo);  w.PushInt(Win); w.PushInt(0); w.Ext(3);
            w.PushInt(StbCommands.MesMake);       w.PushInt(Win); w.PushInt(msgId); w.Ext(3);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(8); w.Ext(3);
            w.PushInt(StbCommands.SetMesAutoset); w.PushInt(Win); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.PushInt(0); w.Ext(6);
            w.PushInt(StbCommands.SetMesPos);     w.PushInt(Win); w.PushInt(0); w.Ext(3);
            int loop = w.Mark();
            w.Yield();
            w.PushInt(StbCommands.GetPadDown); w.PushVarRef(padVar); w.Ext(2);
            w.PushVar(padVar); w.PushInt(StbCommands.PadCross); w.And(); w.BrFalse(loop);
            EmitCloseMenu(w);
        }

        /// <summary>Hide window 1. The menu leaves the bubble SHOWN after a selection (so the no-pole path can
        /// reuse it); every other dispatch path calls this before its transition so the bubble doesn't linger
        /// and render its bare frame+tail during the fade. Safe across opens: the show-flag is re-raised at the
        /// next event start.</summary>
        internal static void EmitCloseMenu(StbWriter w)
        {
            w.PushInt(StbCommands.MesClose); w.PushInt(1); w.Ext(2);
        }
    }
}
