using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Runtime repositioning of town villagers. Currently one adjustment: nudging Mango, on Brownboo's
    /// boardwalk, out from under the baked fishing sign.
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

        /// <summary>Per-town reset. Nothing to restore — the town reloads its villagers fresh on a map change,
        /// so Mango returns to his stock spot on his own; we only re-arm the one-shot log.</summary>
        internal static void Uninstall() => _mangoDumped = false;

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [VillagerPlacement] " + s);
    }
}
