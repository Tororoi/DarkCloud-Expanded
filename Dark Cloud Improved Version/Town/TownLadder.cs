using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ladder handling for swapped-in town allies. Toan's climb overlay (chara\c01dhashigo.chr) is Toan-rigged,
    /// so mounting a ladder as any other model loads that overlay onto the wrong skeleton → crash/corruption.
    /// <see cref="ElfPatches"/>' ladder-refusal cave hooks the mount inside EdMoveChara (@0x16C0FC): when the
    /// mailbox <see cref="BlockLadder"/> is nonzero it skips both EdInitHashigo and the climbing flag (no mount,
    /// no crash) and sets <see cref="RefusalRequested"/>=1 on that blocked Cross press (one-shot per press).
    ///
    /// This ticker sets BlockLadder for any non-Toan ally and consumes RefusalRequested. BASELINE (now): block
    /// only — the ally walks to a ladder, presses Cross, and nothing happens (no crash). The shake-head refusal
    /// animation is the next layer: play a refusal motion when RefusalRequested fires, then clear it.
    /// </summary>
    internal static class TownLadder
    {
        internal static bool Enabled = true;

        private const string Tag = "[Ladder] ";

        private const long BlockLadder      = CodeCaves.Mailbox.BlockLadder;       // EE 0x21F10074 — mod writes (0=vanilla mount / Toan)
        private const long RefusalRequested = CodeCaves.Mailbox.RefusalRequested;  // EE 0x21F10078 — cave sets on a blocked press, mod clears

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
            if (Memory.ReadByte(Addresses.mode) != 2) return;   // town only (the cave is inside the town mover)

            // Non-Toan cannot mount ladders (Toan's climb overlay would crash on their rig). Written every tick so
            // it stays correct across swaps and emulator resets; the cave is town-only so its value is inert elsewhere.
            Memory.WriteInt(BlockLadder, AllySwapPrototype.CurrentAlly != 0 ? 1 : 0);

            // Consume the cave's one-shot refusal request. Baseline: clear it (no animation yet) and log so the
            // blocked mount is visible while testing.
            if (Memory.ReadInt(RefusalRequested) != 0)
            {
                Memory.WriteInt(RefusalRequested, 0);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "ladder mount blocked (non-Toan) — refusal requested");
            }
        }
    }
}
