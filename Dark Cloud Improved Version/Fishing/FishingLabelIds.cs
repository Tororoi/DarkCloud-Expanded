namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Every script-label id the custom fishing towns use — the single home for BOTH sides of the contract:
    /// IsoPatcher / StbLabelBaker bake these ids into the towns' event.stb (ExtendStb / BakeStbLabel), and
    /// CustomFishingSpot / FishingLabelAllocator claim them by the same numbers at runtime. The engine-owned
    /// quit/bait ids (133/134) stay in <see cref="EventPoints"/> (FishingExitLabel / FishingBaitLabel).
    /// </summary>
    internal static class FishingLabelIds
    {
        /// <summary>An id nothing will ever ask for, given to labels whose code we have overwritten.</summary>
        internal const int RetiredLabelId = 9000;

        /// <summary>
        /// The id our fishing script answers to (north-bank / primary spot). Deliberately outside the range
        /// any town uses (the highest real label seen anywhere is 310), so the ONLY thing that can dispatch
        /// it is our own event point.
        /// </summary>
        internal const int FishingLabelId = 400;

        /// <summary>The Queens canal-floor spot — its own label + stance (the kanbanc sign's trigger).</summary>
        internal const int CanalFishingLabelId = 401;

        /// <summary>The canal-ladder "tide too high" message label (a type-3 point co-located with the climb
        /// pair; CanalTide's ladder gate enables it at high tide) + the event-mes id its script shows.</summary>
        internal const int LadderMsgLabelId = 402;
        internal const int LadderMsgId = 23;

        /// <summary>Canal tide-evict warp: label 403 holds a <c>_MAP_JUMP(EastHarborMapArg)</c> script that
        /// CanalTide fires as an event when the tide rises on a player stuck in the drained canal. The map arg
        /// is 1-BASED (VM does MapJump(arg-1)), so 20 → MapNo 19 = East Harbor.</summary>
        internal const int CanalWarpLabelId = 403;
        internal const int EastHarborMapArg = 20;   // MapNo 19 + 1 (the _MAP_JUMP arg is 1-based)

        /// <summary>Dock-spawn event baked into East Harbor (s09): the canal warp's arrival event, placing the
        /// player at the Shipwreck dock instead of the Queens-side entry.</summary>
        internal const int DockSpawnEvent = 404;

        /// <summary>In-place ally model swap (AllySwapPrototype): _GET_POSITION/_GET_ROTATION →
        /// _LOAD_MAIN_CHARA(ally, "info.cfg", 0) → re-place. Fired by writing the id to
        /// <c>start_event_no</c>; nothing else dispatches it.</summary>
        internal const int AllySwapLabelId = 405;

        /// <summary>Id for the shared menu-select subroutine's label. Nothing dispatches it as an event — it is
        /// only ever reached by CALL_FUNC (vanilla parks the same routine as an anonymous funcdata) — so this
        /// just needs to be an id no town uses and clear of the <see cref="RetiredLabelId"/> range.</summary>
        internal const int MenuSubLabelId = 9600;

        /// <summary>Terminator entry StbLabelBaker appends after the spare-label table, making the last
        /// spare's size computable (labels tile end-to-end; size = gap to the next codeOffset).</summary>
        internal const int FishingTerminatorLabelId = 9500;
    }
}
