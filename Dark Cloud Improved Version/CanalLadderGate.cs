using System;
using static Dark_Cloud_Improved_Version.CanalTide;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Canal-ladder tide gate: flip the enabled bit on the injected event points at x≈706 so LOW tide exposes
    /// the native climb pair and any other tide exposes the "tide too high" message point (label 402).
    /// </summary>
    internal static class CanalLadderGate
    {
        private static void Log(string m) => CanalTide.Log(m, nameof(CanalLadderGate));

        // Canal-ladder tide gate: the injected event points all sit at x≈LadderWorldX (706) — the climb pair (rec
        // types 4/5) plus our co-located type-3 message point (label 402). CheckEventPoint bails on
        // enabled(+0x00)==0, and EdGetEvent matches ONE point, so flipping enabled by tide switches which one
        // the X-press hits: LOW → climb pair on / message off (real climb); otherwise → climb pair off /
        // message on (the "tide too high" line). Re-asserted every tick so a town rebuild can't strand it.
        private const long EvArrPtr = 0x01D19700, EvCountAddr = 0x01D19704;  // live ED_EVENT_POINT array ptr + count (guest form of EventPoints.ArrayPtr/Count)
        private const long EvStride = EventPoints.Stride, EvEnabled = EventPoints.Enabled, EvType = EventPoints.Type,
                           EvLabel = EventPoints.ItemOrLabel, EvPos = EventPoints.Position;
        private const int  LadderMsgLabel = IsoPatcher.LadderMessageLabel;      // == 402
        private const float LadderGateX = 706f, LadderGateXTol = 12f;        // LadderWorldX ± tol — only our cluster
        private static bool _loggedLadderGate;

        internal static void Apply(bool low)
        {
            uint arrGuest = Memory.ReadUInt(EvArrPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(arrGuest)) return;
            long arr = Memory.ToMmu(arrGuest);
            int count = Memory.ReadInt(EvCountAddr);
            if (count <= 0 || count > 256) return;                            // sanity — array not yet built

            int climbs = 0, msgs = 0;
            for (int i = 0; i < count; i++)
            {
                long rec = arr + i * EvStride;
                int type = Memory.ReadInt(rec + EvType);
                if (type != 3 && type != 4 && type != 5) continue;
                if (Math.Abs(Memory.ReadFloat(rec + EvPos) - LadderGateX) > LadderGateXTol) continue;  // our x706 cluster only
                if (type == 3)
                {
                    if (Memory.ReadInt(rec + EvLabel) != LadderMsgLabel) continue;  // not our message point (e.g. a fishing sign)
                    SetEventEnabled(rec, !low); msgs++;                             // message: on at high tide
                }
                else { SetEventEnabled(rec, low); climbs++; }                       // climb pair: on at low tide
            }
            if (!_loggedLadderGate && (climbs > 0 || msgs > 0))
            {
                Log($"ladder tide-gate wired ({climbs} climb pt, {msgs} message pt; low={low})");
                _loggedLadderGate = true;
            }
        }

        private static void SetEventEnabled(long rec, bool on)
        {
            int want = on ? 1 : 0;
            if (Memory.ReadInt(rec + EvEnabled) != want) Memory.WriteInt(rec + EvEnabled, want);
        }

        internal static void Reset() => _loggedLadderGate = false;
    }
}
