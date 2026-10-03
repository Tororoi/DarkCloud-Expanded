using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The guard pose held and timed, as the Mirage reads it (<see cref="GuardWatch.HoldPose"/>: R1 down and the active
    /// character in the guard loop or guard walk — Ungaga's and Xiao's alike). <see cref="Held"/> is the seconds it has been held
    /// (0 = not guarding): the clock starts on the first tick in the pose, keeps running through a tick out of the pose while R1
    /// stays down (a swing, a hit), and clears the tick R1 is released. <see cref="Since"/> is the clock itself, for a charge that
    /// starts, reads or clears it on its own terms (default = not holding).</summary>
    internal sealed class GuardHold
    {
        internal DateTime Since;

        internal double Held()
        {
            var (r1, inPose) = GuardWatch.HoldPose();
            if (!r1) { Since = default; return 0; }
            if (!inPose) return Since == default ? 0 : (GameClock.Now - Since).TotalSeconds;
            if (Since == default) Since = GameClock.Now;
            return (GameClock.Now - Since).TotalSeconds;
        }
    }
}
