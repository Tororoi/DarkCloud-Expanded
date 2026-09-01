
namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The script-label ids the custom fishing spots use (baked into the towns' event.stb by IsoPatcher /
    /// StbLabelBaker, claimed by FishingLabelAllocator at runtime). One home for both sides of that contract.
    /// </summary>
    internal static class FishingLabelIds
    {
        /// <summary>An id nothing will ever ask for, given to labels whose code we have overwritten.</summary>
        internal const int RetiredLabelId = 9000;

        /// <summary>
        /// The id our fishing script answers to. Deliberately outside the range any town uses (the highest
        /// real label seen anywhere is 310), so the ONLY thing that can dispatch it is our own event point.
        /// </summary>
        internal const int FishingLabelId = 400;

        /// <summary>Id for the shared menu-select subroutine's label. Nothing dispatches it as an event — it is
        /// only ever reached by CALL_FUNC (vanilla parks the same routine as an anonymous funcdata) — so this
        /// just needs to be an id no town uses and clear of the <see cref="RetiredLabelId"/> range.</summary>
        internal const int MenuSubLabelId = 9600;

        /// <summary>The canal-ladder "tide too high" message label + its event-mes id — both baked into the
        /// ISO by IsoPatcher (spare label 402, event-mes 23). Must match <see cref="IsoPatcher.LadderMessageLabel"/>.</summary>
        internal const int LadderMsgLabelId = IsoPatcher.LadderMessageLabel;
        internal const int LadderMsgId = 23;

        /// <summary>Canal tide-evict warp: label 403 holds a <c>_MAP_JUMP(EastHarborMapArg)</c> script that
        /// CanalTide fires as an event when the tide rises on a player stuck in the drained canal. The map arg
        /// is 1-BASED (VM does MapJump(arg-1)), so 20 → MapNo 19 = East Harbor. Must match
        /// <see cref="IsoPatcher.CanalWarpLabel"/>.</summary>
        internal const int CanalWarpLabelId = IsoPatcher.CanalWarpLabel;
        internal const int EastHarborMapArg = 20;   // MapNo 19 + 1 (the _MAP_JUMP arg is 1-based)
        internal const int DockSpawnEvent   = IsoPatcher.DockSpawnLabel;   // baked s09 event that places the player at the dock
    }
}
