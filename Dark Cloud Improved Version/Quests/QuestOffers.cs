namespace Dark_Cloud_Improved_Version
{
    /// <summary>Where a quest NPC stands with the player: nothing to offer, a quest to hand out, or a quest under way.</summary>
    internal enum QuestPhase { None, Available, Ongoing }

    /// <summary>What a talk menu shows for a quest NPC. <see cref="QuestPhase.Available"/>: saying "Hello" opens the quest
    /// dialogue and the menu has no quest line. <see cref="QuestPhase.Ongoing"/>: the menu gains <see cref="Label"/> as its
    /// extra line and "Hello" stays the greeting.</summary>
    internal readonly struct QuestOffer
    {
        internal readonly QuestPhase Phase;
        internal readonly string Label;
        internal QuestOffer(QuestPhase phase, string label) { Phase = phase; Label = label; }
        internal static readonly QuestOffer None = new QuestOffer(QuestPhase.None, null);
        internal bool SameAs(QuestOffer o) => Phase == o.Phase && Label == o.Label;
    }

    /// <summary>The quest a town NPC currently offers, read from the quest save bytes (docs/mod-quests.md): the repeatable
    /// monster and fishing quests, the item hunts, and the Norune mayor's chain. NPCs are the villager id
    /// shorts <see cref="Dialogues"/> keys on, qualified by area because the two-digit codes repeat between towns.</summary>
    internal static class QuestOffers
    {
        /// <summary>The menu line of an ongoing quest.</summary>
        internal const string OngoingLabel = "About the quest.";

        // (area, npc id, availability flag, status byte) — availability 0 = intro not heard yet, status 0 = no quest running,
        // 1 = running, 2 = finished and waiting for its reward.
        private static readonly (int area, int npc, long avail, long status)[] Repeatable =
        {
            (0, 12592, 0x21CE4474, 0x21CE4402),   // Macho — monsters
            (1, 13618, 0x21CE4476, 0x21CE4407),   // Gob — monsters
            (2, 13108, 0x21CE4478, 0x21CE440C),   // Jake — monsters
            (3, 14388, 0x21CE447A, 0x21CE4411),   // Chief Bonka — monsters
            (0, 13872, 0x21CE4475, 0x21CE4416),   // Pike — fishing
            (1, 13362, 0x21CE4477, 0x21CE441E),   // Pao — fishing
            (2, 13363, 0x21CE4479, 0x21CE4427),   // Sam — fishing
            (3, 13109, 0x21CE447B, 0x21CE4431),   // Devia — fishing
        };

        // (area, npc id, "heard the rumour" flag) — the backfloor item hunts stay on the menu once heard.
        private static readonly (int area, int npc, long heard)[] ItemHunts =
        {
            (0, 13360, 0x21CE4451),   // Laura — Medusa Powder
            (1, 12594, 0x21CE4452),   // Ro — Warp Powder
            (2, 12852, 0x21CE4453),   // Phil — Shell Ring
            (3, 12341, 0x21CE4454),   // Zabo — Hardening Powder
        };

        private const int  NoruneMayor      = 13361;
        private const long MayorQuestStage  = 0x21CE4464;

        internal static QuestOffer For(int area, int npc)
        {
            foreach (var q in Repeatable)
                if (q.area == area && q.npc == npc)
                    return Memory.ReadByte(q.avail) == 0 || Memory.ReadByte(q.status) == 0
                        ? new QuestOffer(QuestPhase.Available, null)
                        : new QuestOffer(QuestPhase.Ongoing, OngoingLabel);
            foreach (var q in ItemHunts)
                if (q.area == area && q.npc == npc)
                    return Memory.ReadByte(q.heard) == 0
                        ? new QuestOffer(QuestPhase.Available, null)
                        : new QuestOffer(QuestPhase.Ongoing, OngoingLabel);
            if (area == 0 && npc == NoruneMayor)
                return Memory.ReadByte(MayorQuestStage) == 0
                    ? new QuestOffer(QuestPhase.Available, null)
                    : new QuestOffer(QuestPhase.Ongoing, OngoingLabel);
            return QuestOffer.None;
        }
    }
}
