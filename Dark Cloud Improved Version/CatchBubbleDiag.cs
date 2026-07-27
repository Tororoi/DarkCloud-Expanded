using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC for the Queens catch-bubble distortion. The catch bubble and the mod's fishing
    /// menu both draw into the town event-message ClsMes objects — <c>EditEventMes1</c> (guest 0x1D1E4D0) and
    /// <c>EditEventMes2</c> (0x1D1FC90). Each ClsMes holds its CURRENT speech-bubble (fukidashi) box at fixed
    /// offsets (from MakeFukidashi_sub @0x150F50): top-left (x1,y1) @ +0x3C/+0x44, bottom-right (x2,y2) @
    /// +0x40/+0x48, computed w/h @ +0x4C/+0x50, window type @ +0x38. A "stretched" bubble is simply a huge
    /// x2−x1. This logs BOTH ClsMes boxes whenever they change while in a custom fishing town, so the window
    /// sequence during a catch is visible.
    ///
    /// Run it in Queens (broken) AND Brownboo (works) and compare the logs — the difference will show which
    /// window goes wide and when (e.g. the catch reusing a stale menu window). Flip <see cref="Enabled"/> off
    /// once the bug is understood.
    /// </summary>
    internal static class CatchBubbleDiag
    {
        internal static bool Enabled = true;

        // The town ClsMes family. The catch bubble draws into the town-talk ones (EditMes1/2); the fish name,
        // the X/Square/Hook prompt, and the mod's fishing menu are separate objects — watch them all so we see
        // exactly which window goes wide.
        private static readonly (long addr, string tag)[] Objs =
        {
            (0x21D1B550, "Talk1"),    // EditMes1  — town-talk (the catch bubble)
            (0x21D1CD10, "Talk2"),    // EditMes2
            (0x21D21450, "Name"),     // EditNameMes — the caught fish's name
            (0x21D243D0, "Help"),     // EditHelpMes — likely the X/Square/Hook fishing prompt
            (0x21D1E4D0, "Event1"),   // EditEventMes1 — the mod's fishing menu
            (0x21D1FC90, "Event2"),   // EditEventMes2
        };
        private const int BoxOff = 0x38, BoxLen = 0x24;   // 9 ints: +0x38..+0x58
        private static readonly string[] _last = new string[6];

        internal static void Tick()
        {
            if (!Enabled) return;
            int map = Memory.ReadInt(EditLoop.MapNo);
            if (map != 2 && map != 14 && map != 23) return;   // custom fishing towns only (Queens/Brownboo/YellowDrops)
            string town = map == 2 ? "QUEENS" : map == 14 ? "BROWNBOO" : "YELLOWDROPS";

            for (int i = 0; i < Objs.Length; i++)
            {
                // Read the box region TWICE and only trust it when both reads agree — PINE reads race the EE's
                // writes, so a single read can tear (page-aligned "millions" values). Two matching reads = a
                // value the game actually held for a frame, not a torn snapshot.
                byte[] b = Memory.ReadBytesBatch(Objs[i].addr + BoxOff, BoxLen);
                byte[] b2 = Memory.ReadBytesBatch(Objs[i].addr + BoxOff, BoxLen);
                if (b == null || b2 == null) continue;
                bool torn = false;
                for (int k = 0; k < BoxLen; k++) if (b[k] != b2[k]) { torn = true; break; }
                if (torn) continue;                            // skip torn reads entirely
                var v = new int[BoxLen / 4];
                bool zero = true;
                for (int k = 0; k < v.Length; k++) { v[k] = BitConverter.ToInt32(b, k * 4); if (v[k] != 0) zero = false; }
                // The two fields that decide rebuild-vs-stale: msgId (+0x16BC) and shown (+0x94). MakeMesWin
                // rebuilds geometry only when msgId CHANGES; same id -> GoNextPage reuses the old (stale) box.
                int msgId = Memory.ReadInt(Objs[i].addr + 0x16BC);
                int shown = Memory.ReadInt(Objs[i].addr + 0x94);
                string state = string.Join(",", v) + $"|m{msgId}|s{shown}";
                if (state == _last[i]) continue;              // log only on change
                _last[i] = state;
                if (zero && msgId == 0) continue;              // skip the all-zero idle state
                var s = new System.Text.StringBuilder($"[{town}] {Objs[i].tag}: msgId={msgId} shown={shown}  ");
                for (int k = 0; k < v.Length; k++) s.Append($"+{BoxOff + k * 4:X}={v[k]} ");
                Log(s.ToString());
            }
        }

        private static void Log(string m) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[MesWinDiag] " + m);
    }
}
