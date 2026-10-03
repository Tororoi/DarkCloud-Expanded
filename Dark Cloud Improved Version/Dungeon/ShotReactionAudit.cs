using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>DIAGNOSTIC, behind <see cref="DebugDiagnostics"/>: the shot-effect config table (BehaviorScriptTable — shared, static ELF data every enemy shot is
    /// planted from, and every character's guard judged against) watched for changes. Every <see cref="PeriodSeconds"/> in a
    /// dungeon, each config's hit-reaction (+0x44) and victim-mask (+0x48) words and the bomb-blast reaction word
    /// (CodeCaves.BombReaction) are compared with their vanilla values; a difference is logged the first time it appears and the
    /// first time it goes back, by config name — so a type-3 shot found guardable can be traced to whatever rewrote it.</summary>
    internal static class ShotReactionAudit
    {
        private const double PeriodSeconds = 2.0;
        // The 34 configs' vanilla hit reactions (cfg +0x44), in table order (docs/enemy-shot-effects-table.md).
        private static readonly int[] VanillaReaction = { 2, 2, 2, 2, 2, 3, 2, 3, 3, 2, 2, 2, 3, 3, 4, 3, 3, 3, 3, 2, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 3, 3, 3 };
        private static readonly int[] VanillaMask = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };   // cfg +0x48: every stock config hurts the player; magic_bin (10) hurts both sides
        private static readonly bool[] _off = new bool[BehaviorScriptTable.Count];
        private static bool _bombOff;
        private static DateTime _last;

        internal static void Tick()
        {
            if ((GameClock.Now - _last).TotalSeconds < PeriodSeconds) return;
            _last = GameClock.Now;
            for (int i = 0; i < BehaviorScriptTable.Count; i++)
            {
                long rec = Memory.Pcsx2Base | (uint)BehaviorScriptTable.RecordAddress(i);
                int reaction = Memory.ReadInt(rec + ShotEffectPack.CfgReaction), mask = Memory.ReadInt(rec + ShotEffectPack.CfgVictimMask);
                bool off = reaction != VanillaReaction[i] || mask != VanillaMask[i];
                if (off == _off[i]) continue;
                _off[i] = off;
                string name = System.Text.Encoding.ASCII.GetString(Memory.ReadBytesBatch(rec, 16) ?? new byte[0]).Split('\0')[0];
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[ShotReactionAudit] cfg {i} `{name}`: reaction {reaction} (vanilla {VanillaReaction[i]}), mask {mask} (vanilla {VanillaMask[i]})" + (off ? " — ALTERED" : " — back to vanilla"));
            }
            int bomb = Memory.ReadInt(CodeCaves.BombReaction);
            bool bombOff = bomb != CodeCaves.BombReactionVanilla;
            if (bombOff != _bombOff)
            {
                _bombOff = bombOff;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[ShotReactionAudit] bomb-blast reaction word {bomb} (vanilla {CodeCaves.BombReactionVanilla})" + (bombOff ? " — ALTERED" : " — back to vanilla"));
            }
        }
    }
}
