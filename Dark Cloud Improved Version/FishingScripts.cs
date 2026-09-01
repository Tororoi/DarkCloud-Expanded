using System;
using static Dark_Cloud_Improved_Version.FishingSpots;
using static Dark_Cloud_Improved_Version.FishingScriptBuilder;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The two per-spot fishing scripts: the ENTRY script (prompt-don't-pounce "!", vanilla entry menu, rod
    /// check, fade, villager clear, fishing-Toan swap, _LOAD_FISHING_DATA/_INIT_FISH, stance, _GOTO_FISHING)
    /// and the EXIT script (label 133: confirm menu, fade, restore Toan in place, _EXIT_FISHING, villager reload).
    /// </summary>
    internal static class FishingScripts
    {
        // Toan has TWO models. The ordinary one has no fishing rod and no fishing motions — which is why the
        // "cast" plays whatever animation happens to sit at the fishing motion's index (the atla-opening one).
        // c01d_turi ("turi" = 釣り, fishing) is the fishing Toan: it carries the rod and the right motion table.
        //
        // This is not optional dressing. _GOTO_FISHING does
        //     SearchFrame(chara->model, "sao")        // 竿 = fishing rod
        // and hands that frame to FishLineInit. On a model with no `sao` frame there is no rod to hang the
        // line from, so the line, float and bait have nowhere to be.
        //
        // Norune swaps the model on the way in and swaps it back on the way out.
        internal const string FishingModel    = "chara/c01d_turi.chr";
        private const string FishingModelCfg = "c01d_turi.cfg";
        internal const string NormalModel     = "chara/c01d.chr";
        private const string NormalModelCfg  = "info.cfg";

        internal static StbWriter BuildFishingBytecode(Spot s, int menuCodeBaseOffset)
        {
            var w = new StbWriter();

            // ── PROMPT, DON'T POUNCE ────────────────────────────────────────────────────────────────────
            //
            // A type-3 event point fires its label the moment you are in range — EdMoveChara has no button
            // check for it (only item and ladder points test PadDown). So the "!" prompt and the X press have
            // to come from the SCRIPT, which is exactly what Norune's enormous label 256 is doing.
            //
            // The mechanism is the same rule that cost us three test cycles at the start, used deliberately
            // this time: a script that RETURNS WITHOUT YIELDING is a "simple event". EdEventInit runs it,
            // sees it finish, and never enters event mode — so the player keeps walking around. That means
            // this script can run EVERY FRAME while you stand near the spot, cheaply, drawing the prompt and
            // watching the pad, and only commit — i.e. yield — once you actually press X.
            //
            // It also makes the whole thing repeatable for free: no disarm/re-arm bookkeeping, no leaked
            // fish. Walk away, come back, press X again.
            w.UseLocals(2);                           // var0 = pad bits, var1 = the wait-loop gate

            w.PushInt(StbCommands.DrawExclamationMark);   // 10 — per-frame; re-asserted on every pass
            w.Ext(1);

            w.PushInt(StbCommands.GetPadDown);        // 1
            w.PushVarRef(0);                          // out: var0 = buttons pressed this frame
            w.Ext(2);

            w.PushVar(0);
            w.PushInt(StbCommands.PadCross);
            w.And();
            int idle = w.MarkForward();
            w.BrFalse(idle);                          // no X -> fall out WITHOUT yielding: a simple event

            // Everything past here yields, so pressing X promotes this into a real event — and only then.

            // Snap the player out of whatever animation they were in (mid-walk, most often) to idle the
            // instant the menu commits. Without this the last motion FREEZES on screen for the whole menu —
            // you press X while walking and stand there mid-stride. Norune's label 11 does exactly this here:
            // _SET_NPC_MOTION(charaId -1 = player, motion 0 = idle), before it opens the menu.
            w.PushInt(StbCommands.SetNpcMotion);      // 133
            w.PushInt(-1);                            // charaId -1 = the player
            w.PushInt(0);                             // motion 0 = idle/stand
            w.Ext(3);

            // ── VANILLA ENTRY MENU (Fish / Exchange FP / Fishing log / Quit) ────────────────────────────
            // Norune shows this before fishing starts (its label 11). We reproduce it. But first WAIT for the
            // mod to swap window 1 to our injected menu text: the swap happens on CustomFishingSpot.Tick, which
            // runs on the mod's 50 ms loop (Thread.Sleep(50) ≈ 3 game frames) — NOT per frame. If the menu's
            // _MES_MAKE runs before that tick lands, it builds a garbage-sized texture off the town's own event
            // mes (which lacks msg 20) — the "heavily distorted" bubble. 8 yields (~133 ms) clears a full tick
            // interval with margin, so the swap is reliably done first.
            for (int i = 0; i < 8; i++) w.Yield();
            EmitMenu(w, 20, 4, /*sel*/2, menuCodeBaseOffset, /*pad*/3, /*ly*/4, /*held*/5, /*scratch*/6);

            // Exchange FP (1) / Fishing log (2) each open their own engine menu then return; Quit (3) returns.
            // Only "Fish" (0) falls through to the session setup below. (Norune's label 11 returns after FP/log
            // exactly like this — the engine sub-menu runs on menu_mode after the event ends.)
            int doFish = w.MarkForward();
            w.PushVar(2); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrTrue(doFish);
                int notFp = w.MarkForward();
                w.PushVar(2); w.PushInt(1); w.Cmp(StbWriter.CmpEq); w.BrFalse(notFp);
                    EmitCloseMenu(w);
                    w.PushInt(StbCommands.GotoFpChange); w.Ext(1); w.Yield();
                    w.Ret();
                w.PlaceMark(notFp);
                int notLog = w.MarkForward();
                w.PushVar(2); w.PushInt(2); w.Cmp(StbWriter.CmpEq); w.BrFalse(notLog);
                    EmitCloseMenu(w);
                    w.PushInt(StbCommands.GotoFishRanking); w.Ext(1); w.Yield();
                    w.Ret();
                w.PlaceMark(notLog);
                EmitCloseMenu(w);
                w.Ret();                              // Quit (3) or anything unexpected: back to walking
            w.PlaceMark(doFish);

            // "Fish": require the fishing rod (item 185). EdCheckItem returns -1 when it isn't owned; Norune's
            // label 11 shows the no-pole line (msg 21) and returns in that case, else starts fishing.
            w.PushInt(StbCommands.SItemCheck); w.PushInt(StbCommands.FishingRodItem); w.PushVarRef(2); w.Ext(3);
            int haveRod = w.MarkForward();
            w.PushVar(2); w.PushInt(0); w.Cmp(StbWriter.CmpLt); w.BrFalse(haveRod);   // v2 >= 0 -> owned -> fish
                EmitShowMessage(w, 21, /*padVar*/3);   // no-pole line, wait for X, then close (window still open here)
                w.Ret();
            w.PlaceMark(haveRod);
            EmitCloseMenu(w);   // rod in hand: hide the menu bubble before the fade so it doesn't garble

            // Rod in hand: fall through to the fade + model swap + fishing-data load.

            // FADE TO BLACK BEFORE TOUCHING THE MODEL, and hide the player while we do it. Norune:
            //
            //     _FADE_OUT(30) ; <wait> ; _CLEAR_VILLAGER_BUFF() ; _NPC_DRAW(0, -1) ; _LOAD_MAIN_CHARA(...)
            //
            // We were swapping the model in plain sight, which is why the player visibly vanished and then
            // faded back in wearing the fishing model. The swap is supposed to happen behind black.
            w.PushInt(StbCommands.FadeOut);           // 501 — FADE_OUT (500 is FADE_IN)
            w.PushInt(30);
            w.Ext(2);
            EmitWaitLoop(w, StbCommands.CheckFade, exitOnNonZero: true);   // done once fade_end is set

            // CLEAR THE TOWN'S NPCs for the session. This is HALF of a pair, and it only works with the other
            // half — _LOAD_VILLAGER on exit (see BuildExitBytecode).
            //
            // It rewinds the villager buffer, so the townspeople vanish while you fish (which is what vanilla
            // does — the town is empty during a session) and their memory is free for the 1.8 MB fishing
            // model. On its own it CRASHES: an earlier build called this and never reloaded, so after the
            // session the engine kept iterating villager slots whose memory had become part of a fishing rod,
            // and walking to where one stood killed the game. Reloading them on exit is what makes it safe.
            //
            // Clearing here rather than not-clearing also fixes the OTHER symptom: with villagers still loaded
            // through a session, an open town (Brownboo) shows them — and one renders garbled, because the
            // texture manager reuses a block the model/bait overwrote. Gone for the session, gone the glitch.
            w.PushInt(StbCommands.ClearVillagerBuff); // 38 — paired with _LOAD_VILLAGER on exit
            w.Ext(1);

            // RESET THE FISHING POOL TO ITS BASE *BEFORE* THE MODEL LOAD. This is the Brownboo fix.
            //
            // _LOAD_MAIN_CHARA(turi, flag=1) loads the 1.8 MB model into the same pool _LOAD_FISHING_DATA
            // uses (see §fishing-load). Norune loads the model FIRST and clears the event buffer AFTER, which
            // works only because its pool pointer already sits low. Brownboo has more resident event data, so
            // the pointer is high, and model-start + 1.8 MB runs off the end of the pool -> overflow -> crash
            // (confirmed: skipping the model reaches fishing fine, cpoly=4). Resetting the pool to base first
            // makes the model load from the bottom, so everything packs tight from the base and fits.
            w.PushInt(StbCommands.ClearEventBuff);    // 39 — moved up from after the model swap
            w.Ext(1);

            // We do NOT mirror Norune's _NPC_DRAW(0,-1) here (nor _NPC_DRAW(1,-1) on exit), and that is not a
            // shortcut — for the player it is a NO-OP. Its per-character draw flags are a villager-only array;
            // the player (id -1) bypasses it, and its only other write has zero readers in the binary. So
            // Norune's hide/show around the model swap is vestigial. (game_data/docs/fishing-engine-re.md §npc-draw)

            // SWAP TOAN FOR THE FISHING TOAN.
            //     _LOAD_MAIN_CHARA("chara/c01d_turi.chr", "c01d_turi.cfg", 1)
            // The ordinary c01d has no `sao` (rod) frame for _GOTO_FISHING to hang the line from, and none of
            // the fishing motions — so the cast animation index lands on whatever else is at that slot in
            // c01d's motion table, which is why it played the atla-opening motion.
            if (!s.DiagSkipModel)
            {
                w.PushInt(StbCommands.LoadMainChara);     // 999 — into the pool we just reset to base
                w.PushString(FishingModel);
                w.PushString(FishingModelCfg);
                w.PushInt(1);                             // flag 1 = load into the fishing pool
                w.Ext(4);
                w.Yield();
            }

            w.PushInt(StbCommands.LoadFishingData);   // 998 — NOT 999; the dispatch table is {handler, id}
            w.PushInt(s.AreaId);
            w.PushFloat(s.X1);
            w.PushFloat(s.Z1);
            w.PushFloat(s.X2);
            w.PushFloat(s.Z2);
            // Queens (both the north-bank AND canal-floor spots): the water level follows the day/night clock
            // (CanalTide) so the two stay in sync — the SAME canal surface, just fished from the bank (casting
            // down) or from the exposed floor at low tide. RebuildFishingScript re-bakes EVERY installed script
            // on a tide change, so the canal script tracks the tide too (not frozen at its install-time level).
            // Everywhere else: the spot's fixed height.
            float water = s.MapNo == TownMapNo.Queens ? CanalTide.QueensWaterLevel() : s.Water;
            w.PushFloat(water);
            w.PushFloat(s.Ground);
            w.Ext(8);                                 // 1 command id + 7 arguments

            // _INIT_FISH places the fish, at the centre of the rect it is GIVEN, at WaterLevel-12 (see the
            // fish-depth patch, which changes the 12). This rect is the FISH bounds (fish_rect), distinct
            // from the cast bounds (_LOAD_FISHING_DATA's rect). Give it the smaller water rect when the spot
            // has one, so fish stay in the water instead of wandering the whole cast area / through banks.
            float fx1 = s.HasFishRect ? s.FishX1 : s.X1;
            float fz1 = s.HasFishRect ? s.FishZ1 : s.Z1;
            float fx2 = s.HasFishRect ? s.FishX2 : s.X2;
            float fz2 = s.HasFishRect ? s.FishZ2 : s.Z2;
            w.PushInt(StbCommands.InitFish);          // 996
            w.PushFloat(fx1);
            w.PushFloat(fz1);
            w.PushFloat(fx2);
            w.PushFloat(fz2);
            w.Ext(5);                                 // 1 command id + 4 arguments

            // Snap the player into the fishing stance. Norune does exactly this — _SET_WORLD_COORD, then
            // _SET_NPC_POS / _SET_NPC_ROT at its own fishing point. Without it the player keeps whatever
            // position and facing they walked in with, so the cast is aimed at dry land and the engine
            // rejects it. This is the rod "bug". (Norune's exact coords: game_data/docs/fishing-engine-re.md §norune-script)
            //
            // _SET_WORLD_COORD is set to IDENTITY so that the position and rotation below are plain world
            // coordinates. Norune passes the pond part's transform instead, because its numbers are
            // part-local; ours come straight out of the probe in world space.
            if (s.HasStance)
            {
                EmitWorldCoordReset(w);

                w.PushInt(StbCommands.SetNpcPos);
                w.PushInt(-1);                        // -1 = the player
                w.PushFloat(s.StandX); w.PushFloat(s.StandY); w.PushFloat(s.StandZ);
                w.Ext(5);

                w.PushInt(StbCommands.SetNpcRot);
                w.PushInt(-1);
                w.PushFloat(0f); w.PushFloat(s.Facing); w.PushFloat(0f);
                w.Ext(5);
            }

            // NO BAIT LOAD HERE — and that is deliberate. Norune loads bait ONLY from label 134.
            //
            // Loading it here corrupts the heap. _CLEAR_EVENT_BUFF (which the item load needs) RESETS the
            // bump allocator that _LOAD_FISHING_DATA just allocated fishing.pak and the fish out of. So doing
            // the item load after the fishing load rewinds the arena and drops the bait on top of the fishing
            // data. The session still runs, because that memory is already in hand; but the arena is wrecked,
            // and the next thing to allocate from it — area streaming, once you walk far enough — lands in the
            // wreckage.
            //
            // That was the crash-after-fishing-when-you-walk-away. Vanilla starts a session with no bait and
            // you pick one with Square, so this is also the faithful behaviour, not just the safe one.
            // (allocator + fields: game_data/docs/fishing-engine-re.md §fishing-load)

            w.PushInt(StbCommands.GotoFishing);       // 997 — sets the event return code to 0xB
            w.Ext(1);                                 // command id only; matches Norune's `push 997; EXT argc=1`

            // Norune ends with _FADE_IN(60) — command 500 is FADE_*IN*, not fade-out (the mod's old command
            // table had the ids shifted by one and said otherwise). Without it the screen never fades back,
            // which is the missing transition.
            w.PushInt(StbCommands.FadeIn);
            w.PushInt(60);
            w.Ext(2);

            // The no-X path lands here too, having yielded nothing — so on an ordinary frame the whole script
            // is: draw the "!", read the pad, return. Cheap enough to run every frame you stand there.
            w.PlaceMark(idle);
            w.Ret();
            return w;
        }

        /// <summary>
        /// Label 133 — the engine's hardcoded "leave fishing" script.
        ///
        /// In fishing mode the engine asks for script labels BY NUMBER when you press a button — Cross casts,
        /// Circle requests label 133 (quit), Square requests 134 (bait menu).
        ///
        /// Norune's script HAS labels 133 and 134. A town that never had fishing does not — so the button
        /// asks for a label that does not exist and nothing happens, which is exactly why the session could
        /// not be exited. We synthesise 133 ourselves; it is tiny.
        /// (button -> label map: game_data/docs/fishing-engine-re.md §fishing-buttons)
        ///
        /// The RET matters: we set no return code, so <c>EventMode</c> takes its <c>default:</c> branch and
        /// puts <c>GameMode</c> back to 1 (walking). That is how Norune's exit path ends too.
        /// </summary>
        internal static StbWriter BuildExitBytecode(Spot s, int menuCodeBaseOffset)
        {
            var w = new StbWriter();
            w.Yield();                                // same rule as the main script — see BuildFishingBytecode
            w.Yield();                                // Norune yields twice here before touching the model

            // Snap the player to idle before the confirm menu, exactly as Norune's 133 opens with
            // _SET_NPC_MOTION(-1, 0) (and as our entry script does). Without it the player holds the fishing
            // pose frozen behind the "Quit fishing?" menu; on Continue the session resumes the fishing motion.
            w.PushInt(StbCommands.SetNpcMotion);      // 133
            w.PushInt(-1);                            // charaId -1 = the player
            w.PushInt(0);                             // motion 0 = idle/stand
            w.Ext(3);

            // ── VANILLA QUIT MENU (Continue fishing / Quit fishing) ─────────────────────────────────────
            // Circle during fishing asks the engine for label 133 (this script). Norune's 133 shows a 2-option
            // confirm before actually leaving. The session is already open, so window 1 already holds our menu
            // text (msg 22). Choice lands in var8 (high range, clear of the position locals below).
            EmitMenu(w, 22, 2, /*sel*/8, menuCodeBaseOffset, /*pad*/9, /*ly*/10, /*held*/11, /*scratch*/12);
            int doQuit = w.MarkForward();
            w.PushVar(8); w.PushInt(0); w.Cmp(StbWriter.CmpEq); w.BrFalse(doQuit);   // sel != 0 (Quit) -> leave
                // Continue (0): keep the session running. _SET_RETURN_CODE(11) is exactly what Norune's 133
                // does to fall back into fishing instead of exiting.
                EmitCloseMenu(w);
                w.PushInt(StbCommands.SetReturnCode); w.PushInt(11); w.Ext(2);
                w.Yield();
                w.Ret();
            w.PlaceMark(doQuit);
            EmitCloseMenu(w);   // Quit: hide the menu bubble before the fade-out below
            // Quit: fall through to the fade-out + model restore + _EXIT_FISHING below.

            // FADE OUT OURSELVES before the model swap, don't rely on the fishing quit having done it. When
            // you quit while standing IN the trigger radius, the enter event point (in range) re-fires and
            // pre-empts the quit's fade-out — so the screen was still showing when the exit's fade-in ran,
            // i.e. no visible fade at all. Doing our own fade-out makes it consistent regardless: from an
            // already-black screen this is a harmless no-op (stays black), from a visible one it fades out
            // properly, and the model swap + villager reload then happen behind black as intended.
            w.PushInt(StbCommands.FadeOut);           // 501
            w.PushInt(30);
            w.Ext(2);
            EmitWaitLoop(w, StbCommands.CheckFade, exitOnNonZero: true);

            // RESTORE THE ORDINARY TOAN AND PUT THE PLAYER BACK WHERE THEY ARE — exactly as Norune's exit does.
            //
            // _LOAD_MAIN_CHARA resets the model's position, so the exit MUST re-place the player or you come out
            // of fishing falling through the map. And you can walk around while fishing (until you cast), so the
            // re-place has to land wherever you actually are, not at the entry stance.
            //
            // Norune's 256 exit block does this without any per-frame help: _GET_NPC_POS / _GET_NPC_ROT read the
            // live position into locals BEFORE the model swap, then _SET_NPC_POS / _SET_NPC_ROT write them back
            // AFTER. (Skipped when the entry skipped the swap — there is nothing to undo, and nothing reset the
            // position.)
            //
            // The position/rotation locals MUST be pushed float-safe (PushVarFloat / PushVarRefFloat, a2 = 8):
            // that stamps each stack entry's type tag so _SET_NPC_POS's GetStackFloat REINTERPRETS the float
            // bits instead of doing (float)(int)bits. With the plain int-mode push, a coord like 0x4309A6C0 is
            // read as (float)1124760000 ≈ 1.12e9 and the player (and camera) is flung off the map — a black
            // screen on quit. See PushVarFloat.
            //
            // BOTH GET and SET convert through the GLOBAL world-coord matrix (GetLocalPos in, GetWorldPos out).
            // The reset forces it to identity so the round-trip is exact in raw world space regardless of the
            // fishing setup or the player's per-CFrame flag; re-asserted after the swap so the villager reload
            // downstream also runs against a clean matrix.
            if (!s.DiagSkipModel)
            {
                // v2..v4 = position, v5..v7 = rotation. Deliberately NOT v0..v5: the wait loops below use
                // GateVar (var1) and the confirm menu uses var8, and the float-mode push STAMPS a var's type
                // tag — stamping the wait-loop's gate corrupts its truthiness check and hangs the exit after
                // _EXIT_FISHING. Keep our floats clear of both reserved locals.
                w.UseLocals(8);
                EmitWorldCoordReset(w);                   // identity: GET/SET both operate in world space

                w.PushInt(StbCommands.GetNpcPos);         // 131
                w.PushInt(-1);
                w.PushVarRefFloat(2); w.PushVarRefFloat(3); w.PushVarRefFloat(4);
                w.Ext(5);

                w.PushInt(StbCommands.GetNpcRot);         // 139
                w.PushInt(-1);
                w.PushVarRefFloat(5); w.PushVarRefFloat(6); w.PushVarRefFloat(7);
                w.Ext(5);

                w.PushInt(StbCommands.LoadMainChara);     // 999
                w.PushString(NormalModel);
                w.PushString(NormalModelCfg);
                w.PushInt(0);
                w.Ext(4);

                EmitWorldCoordReset(w);                   // re-assert identity AFTER the swap: the model load can
                                                          // leave a non-identity matrix, which would both mis-place
                                                          // the SET below and corrupt the villager reload downstream

                w.PushInt(StbCommands.SetNpcPos);         // 137
                w.PushInt(-1);
                w.PushVarFloat(2); w.PushVarFloat(3); w.PushVarFloat(4);
                w.Ext(5);

                w.PushInt(StbCommands.SetNpcRot);         // 138
                w.PushInt(-1);
                w.PushVarFloat(5); w.PushVarFloat(6); w.PushVarFloat(7);
                w.Ext(5);
            }

            // AFTER the model swap, not before — Norune's order.
            w.PushInt(StbCommands.ExitFishing);       // 995
            w.Ext(1);

            // RELOAD THE TOWN'S NPCs. This is the fix for the one garbled villager (e.g. Limbo).
            //
            // Fishing loads the 1.8 MB fishing Toan and the bait, and the texture manager reuses blocks — so
            // one villager's texture block gets overwritten during a session. We restore the player model on
            // the way out but never the villagers, so that block stays wrong until the area reloads (which is
            // why walking into a building "fixes" it). _LOAD_VILLAGER rewinds the villager buffer and reloads
            // every NPC and its textures from disc — exactly what Norune's exit does through its own script
            // functions (which we never ported). It takes no arguments; it reads the current map's villager
            // list from globals.
            //
            // After _EXIT_FISHING, so the fishing data is torn down first; behind the fade, so the reload is
            // invisible; and followed by a load-wait, so the town is whole before the screen comes back.
            w.PushInt(StbCommands.LoadVillager);      // 57
            w.Ext(1);
            EmitWaitLoop(w, StbCommands.LoadSync, exitOnNonZero: false);

            w.PushInt(StbCommands.FadeIn);            // 500(60), as Norune's own exit block does
            w.PushInt(60);
            w.Ext(2);

            w.Ret();
            return w;
        }
    }
}
