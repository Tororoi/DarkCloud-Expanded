using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The magic circles' magnitudes, as one set. The ISO's circle cave (tools/stubs/circle_effects.s, over
    /// DebugInfomationIF) applies every circle from the words at CodeCaves.CircleTable; the pnach seeds the vanilla figures
    /// while nobody owns the table. Two things write it: a MAGNITUDE set (the Crysknife / Magical Hammer boost, <see cref="Set"/> /
    /// <see cref="Vanilla"/>) and the FAVOUR word (the Secret Armlet: the bad circles dealt as good ones, <see cref="Favour"/>).
    /// The owner word goes on while either is up, so the seeding stops, and comes off when both are gone.</summary>
    internal static class MagicCircles
    {
        /// <summary>Every magnitude a circle applies (the .s says where each is used).</summary>
        internal sealed record Params(
            int AttackFrames,                       // 0: attack ×2 for this many frames
            float GildaUpMult, int GildaUpAdd,      // 1: gilda += trunc(gilda × mult) + add
            float GildaDownFrac,                    // 6: gilda −= gilda × frac
            int MaxWhpUpMin, int MaxWhpUpRange,     // 3: max WHP += min + rand % range
            int StatDownMin, int StatDownRange,     // 7: a stat −= min + rand % range
            int MaxWhpDownMin, int MaxWhpDownRange, // 8: max WHP −= min + rand % range
            float WhpDivisor,                       // 9: WHP /= divisor, floor 1
            int RageFrames,                         // 5: every enemy enraged for this many frames
            int AbsFullItem, int WhpCureItem,       // 2 / 4: an item into the bag if there is room (0 = none)
            int ElemDownMult,                       // 7: the element/anti byte losses × this
            int SlowFrames,                         // 5, favoured: every enemy slowed (gooey) for this many frames
            int RewardCount);                       // 2 / 4: how many of the reward item

        /// <summary>The engine's own figures (what the pnach seeds).</summary>
        internal static readonly Params VanillaParams = new(
            AttackFrames: 0x708, GildaUpMult: 1.2f, GildaUpAdd: 10, GildaDownFrac: 0.2f,
            MaxWhpUpMin: 3, MaxWhpUpRange: 3, StatDownMin: 2, StatDownRange: 3, MaxWhpDownMin: 3, MaxWhpDownRange: 3,
            WhpDivisor: 4f, RageFrames: 300, AbsFullItem: 0, WhpCureItem: 0, ElemDownMult: 1, SlowFrames: 300, RewardCount: 1);

        /// <summary>The circles at <paramref name="m"/> times their effect (2 = the boost of one of the two boosting weapons,
        /// 3 = both): every figure scaled, the WHP loss taken to 1, and the two "to max" circles giving m − 1 powders.</summary>
        internal static Params Boosted(int m) => new(
            AttackFrames: 0x708 * m,
            GildaUpMult: 0.2f * m, GildaUpAdd: 10 * m,      // gilda → (1 + 0.2 m)× + 10 m
            GildaDownFrac: 0.2f * m,
            MaxWhpUpMin: 3 * m, MaxWhpUpRange: 2 * m + 1,    // 3..5 → 6..10 → 9..15
            StatDownMin: 2 * m, StatDownRange: 2 * m + 1,    // 2..4 → 4..8 → 6..12
            MaxWhpDownMin: 3 * m, MaxWhpDownRange: 2 * m + 1,
            WhpDivisor: 1e9f,                                 // WHP to 1 (the cave floors the quotient at 1)
            RageFrames: 300 * m,
            AbsFullItem: Items.poweruppowder, WhpCureItem: Items.autorepairpowder,
            ElemDownMult: m,
            SlowFrames: 300 * m,
            RewardCount: m - 1);

        private static Params _written;                 // the magnitude set in the table (null = the vanilla one, unowned by a set)
        private static bool   _favour;                  // the favour word as written
        private static bool   _owned;                   // the owner word as written

        private static void Own(bool want)
        {
            if (want == _owned) return;
            Memory.WriteInt(CodeCaves.CircleTable + CodeCaves.CircleOwner, want ? 1 : 0);
            _owned = want;
        }

        /// <summary>The table's magnitudes set to <paramref name="p"/> and owned (written only when they change).</summary>
        internal static void Set(Params p)
        {
            if (p.Equals(_written)) return;
            if (p.MaxWhpUpRange < 1 || p.StatDownRange < 1 || p.MaxWhpDownRange < 1) throw new ArgumentException("circle ranges must be ≥ 1 (the cave divides by them)");
            long t = CodeCaves.CircleTable;
            Own(true);                                                                       // ours before the words: the pnach stops re-seeding
            Memory.WriteInt  (t + CodeCaves.CircleAttackFrames,    p.AttackFrames);
            Memory.WriteFloat(t + CodeCaves.CircleGildaUpMult,     p.GildaUpMult);
            Memory.WriteInt  (t + CodeCaves.CircleGildaUpAdd,      p.GildaUpAdd);
            Memory.WriteFloat(t + CodeCaves.CircleGildaDownFrac,   p.GildaDownFrac);
            Memory.WriteInt  (t + CodeCaves.CircleMaxWhpUpMin,     p.MaxWhpUpMin);
            Memory.WriteInt  (t + CodeCaves.CircleMaxWhpUpRange,   p.MaxWhpUpRange);
            Memory.WriteInt  (t + CodeCaves.CircleStatDownMin,     p.StatDownMin);
            Memory.WriteInt  (t + CodeCaves.CircleStatDownRange,   p.StatDownRange);
            Memory.WriteInt  (t + CodeCaves.CircleMaxWhpDownMin,   p.MaxWhpDownMin);
            Memory.WriteInt  (t + CodeCaves.CircleMaxWhpDownRange, p.MaxWhpDownRange);
            Memory.WriteFloat(t + CodeCaves.CircleWhpDivisor,      p.WhpDivisor);
            Memory.WriteInt  (t + CodeCaves.CircleRageFrames,      p.RageFrames);
            Memory.WriteInt  (t + CodeCaves.CircleAbsFullItem,     p.AbsFullItem);
            Memory.WriteInt  (t + CodeCaves.CircleWhpCureItem,     p.WhpCureItem);
            Memory.WriteInt  (t + CodeCaves.CircleElemDownMult,    p.ElemDownMult);
            Memory.WriteInt  (t + CodeCaves.CircleSlowFrames,      p.SlowFrames);
            Memory.WriteInt  (t + CodeCaves.CircleRewardCount,     p.RewardCount);
            _written = p;
        }

        /// <summary>The vanilla magnitudes back; the table is handed back to the pnach's seeding unless the favour word is still up.</summary>
        internal static void Vanilla()
        {
            if (_written == null) return;
            Set(VanillaParams);
            _written = null;
            Own(_favour);
        }

        /// <summary>The favour word: the bad circles dealt as good ones while <paramref name="on"/> (written only on a change).</summary>
        internal static void Favour(bool on)
        {
            if (on == _favour) return;
            if (on) Own(true);
            Memory.WriteInt(CodeCaves.CircleTable + CodeCaves.CircleFavour, on ? 1 : 0);
            _favour = on;
            if (!on) Own(_written != null);
        }
    }
}
