using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Runtime villager handling around the custom fishing spots: hiding/restoring the townspeople for a
    /// fishing session (the freed-villager crash guard — see HideForSession), and nudging Mango, on
    /// Brownboo's boardwalk, out from under the baked fishing sign.
    ///
    /// (The fishing sign itself is now BAKED into the ISO's gedit/&lt;code&gt;/scene.scn and drawn natively by
    /// the engine. The old runtime sign-injection — loading kanban.mds + e01b24 into a code cave via a cmd-10
    /// dispatch redirect and drawing the CFrame through a hijacked TreasureCursor / cloned event point — was
    /// superseded by that disc bake; see git history if it's ever needed again.)
    /// </summary>
    internal static class VillagerPlacement
    {
        internal static bool Enabled = true;

        // Mango = villager SLOT 1, moved back along the boardwalk toward shore so the baked sign isn't blocked.
        // Addressed by SLOT, not id: +0x1449 is a SHARED model id (14134 is Kiwi AND Mango), so an id-match hit
        // the wrong villager. Brownboo-specific — the caller gates this to that town.
        internal static int   MangoSlot = 1;
        internal static float MangoX = 255f, MangoY = 11f, MangoZ = -55f;
        private static bool _mangoDumped;

        /// <summary>Move Mango at his SOURCE: the AI (EdMoveVillager) re-applies VILLAGER_INFO[slot]+0x70 to the
        /// model every frame, so writing +0x70 (with +0x54 = 0, "static, position straight from +0x70") is where
        /// the game naturally positions him.</summary>
        internal static void PinMango()
        {
            if (!Enabled) return;
            long info = 0x21D329D0 + MangoSlot * 0x90;        // VILLAGER_INFO[slot] — the AI's position source
            if (!_mangoDumped)
            {
                _mangoDumped = true;
                long v = Villagers.ObjBase + MangoSlot * 0x14A0;
                Log($"Mango = villager[{MangoSlot}] modelId={Memory.ReadShort(v + 0x1449)}: VILLAGER_INFO+0x70 was " +
                    $"({Memory.ReadFloat(info + 0x70):0.#}, {Memory.ReadFloat(info + 0x74):0.#}, {Memory.ReadFloat(info + 0x78):0.#}) " +
                    $"-> ({MangoX}, {MangoY}, {MangoZ})");
            }
            Memory.WriteInt  (info + 0x54, 0);
            Memory.WriteFloat(info + 0x70, MangoX);
            Memory.WriteFloat(info + 0x74, MangoY);
            Memory.WriteFloat(info + 0x78, MangoZ);
        }

        // The engine's event-mode NPC stepper loops `for i < Villagers.Count` and calls VIRTUAL functions on
        // each villager object. A fishing session frees the villager buffer (cmd 38) and loads the 1.8 MB
        // model over those objects, so their vtable pointers become garbage — and the stepper jumps through
        // one to an unmapped page (the recLUT crash). Brownboo is populated so the count is large and it
        // steps the freed villagers; Yellow Drops is nearly empty so the loop body never runs — which is why
        // only Brownboo crashed. Vanilla's villager clear must pair with suspending this count; we do the
        // same: zero the count for the whole fishing window and restore it on the way out. One write covers
        // every event-mode villager iterator, not just the one we traced.
        // (iterators + object layout: game_data/docs/fishing-engine-re.md §villager-suspend)
        private static int _savedVillagerCount = -1;

        // The engine's villager DRAW walks a HARDCODED 10 slots (so the count knob above does NOT cover it),
        // each gated by a per-object draw flag. The objects live at a FIXED base that the model load does NOT
        // overwrite — only the VISUAL sub-object they point to is freed, and dispatching its vtable through
        // the garbage pointer is the recLUT crash. Zero the fixed draw flags and the gate returns 0 first, so
        // the freed visual is never touched. Restore on exit.
        // (draw routine + object/flag offsets: game_data/docs/fishing-engine-re.md §villager-suspend)
        private static readonly int[] _savedDrawFlags = new int[Villagers.DrawSlots];
        private static bool _drawFlagsSaved;
        private static bool _villagersHidden;   // one-shot latch for UpdateVillagerHide

        /// <summary>
        /// Hides the town's villagers for the session — but only once the screen has fully faded to black.
        ///
        /// This is split out of CustomFishingSpot.UpdateFishingWindow on purpose. That method's OPEN transition
        /// fires at entry-menu time, and the townspeople must stay on screen while the menu is up (the player
        /// can still back out with FP / log / quit). The enter script only commits to fishing AFTER the menu:
        /// it fades to black (_FADE_OUT), waits for the fade to finish, and THEN frees the villager buffer
        /// (cmd 38) and loads the 1.8 MB fishing model over it. Hiding the villagers exactly at "fully black"
        /// means the pop-out is invisible (the user sees them right up to the fade) yet the count is zeroed
        /// and the draw flags cleared before the engine can step/draw a villager whose memory is being reused.
        ///
        /// Fade state: a fade-out holds <c>fade_in_out == -1</c> and flips <c>fade_end</c> to 1 only when the
        /// black alpha reaches full, staying there until the fade-in. So "fully black on a fade-out" ==
        /// <c>fade_in_out == -1 &amp;&amp; fade_end != 0</c>. We also hide on a live session (cpoly / fishing mode)
        /// as a safety net for attaching mid-session, where the buffer is already freed and leaving villagers
        /// stepping would crash. (fade global: game_data/docs/fishing-engine-re.md §fade-state)
        /// </summary>
        internal static void HideForSession(bool inFishingWindow)
        {
            if (_villagersHidden || !inFishingWindow) return;

            bool fullyBlack = Memory.ReadInt(FishingSpot.FadeInOut) == -1 &&
                              Memory.ReadInt(FishingSpot.FadeEnd) != 0;
            bool sessionLive = Memory.ReadInt(FishingSpot.CPolyNum) > 0 ||
                               Memory.ReadFloat(FishingSpot.WaterLevel) != 0f ||
                               Memory.ReadInt(EditLoop.GameMode) == EditLoop.GameModeFishing;
            if (!fullyBlack && !sessionLive) return;   // entry menu still up (or fade still ramping) — leave them

            // (1) STEP — the event-mode NPC stepper loops `i < count`, so zero the count.
            _savedVillagerCount = Memory.ReadInt(Villagers.Count);
            if (_savedVillagerCount > 0) Memory.WriteInt(Villagers.Count, 0);
            // (2) DRAW — the villager draw walks a fixed 10 slots gated by a per-object flag; zero the 10
            //     fixed-address flags so the gate returns 0 and skips the freed visual. (§villager-suspend)
            for (int i = 0; i < Villagers.DrawSlots; i++)
            {
                long f = Villagers.ObjBase + (long)i * Villagers.ObjStride + Villagers.DrawFlag;
                _savedDrawFlags[i] = Memory.ReadInt(f);
                Memory.WriteInt(f, 0);
            }
            _drawFlagsSaved = true;
            _villagersHidden = true;
            Log($"   villagers hidden at fade-to-black: count {_savedVillagerCount} -> 0, " +
                $"{Villagers.DrawSlots} draw flags -> 0 " +
                $"({(fullyBlack ? "fade complete" : "session live")})");
        }

        /// <summary>Put the villagers back after a session — AFTER the exit script's _LOAD_VILLAGER (57) has
        /// rebuilt them. Called on the fishing-window CLOSE transition.</summary>
        internal static void RestoreAfterSession()
        {
            // Restore AFTER the exit script's _LOAD_VILLAGER (57) has rebuilt the villagers.
            if (_savedVillagerCount > 0) Memory.WriteInt(Villagers.Count, _savedVillagerCount);
            if (_drawFlagsSaved)
            {
                for (int i = 0; i < Villagers.DrawSlots; i++)
                    Memory.WriteInt(Villagers.ObjBase + (long)i * Villagers.ObjStride + Villagers.DrawFlag,
                                    _savedDrawFlags[i]);
                _drawFlagsSaved = false;
                Log($"   villagers restored (count -> {_savedVillagerCount}, draw flags restored)");
            }
            _savedVillagerCount = -1;
            _villagersHidden = false;   // re-arm the fade-gated hide for the next session
        }

        /// <summary>Per-town reset of the session state: the saved count belongs to the OLD town — never restore
        /// it into the new one.</summary>
        internal static void ResetSession()
        {
            _savedVillagerCount = -1;
            _drawFlagsSaved = false;
        }

        /// <summary>Per-town reset. Nothing to restore — the town reloads its villagers fresh on a map change,
        /// so Mango returns to his stock spot on his own; we only re-arm the one-shot log.</summary>
        internal static void Uninstall() => _mangoDumped = false;

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [VillagerPlacement] " + s);
    }
}
