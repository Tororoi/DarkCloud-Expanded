using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The dungeon's play clock: it stands still while the game holds — the PAUSE screen or the item menu — so a feature
    /// timing a duration against <see cref="Now"/> measures play time, not wall time. One sampler advances the held
    /// total; every feature reads the same clock, so a hold is never counted twice.
    /// </summary>
    internal static class GameClock
    {
        private const int SampleMs = 16;
        private static readonly object _lock = new object();
        private static DateTime _heldAt = DateTime.MinValue;   // when the current hold began, or MinValue
        private static TimeSpan _held   = TimeSpan.Zero;       // every completed hold, summed
        private static Thread _thread;

        /// <summary>Wall time less every hold, including the one in progress.</summary>
        internal static DateTime Now
        {
            get
            {
                lock (_lock)
                {
                    DateTime wall = DateTime.UtcNow;
                    return wall - _held - (_heldAt == DateTime.MinValue ? TimeSpan.Zero : wall - _heldAt);
                }
            }
        }

        /// <summary>Whether the game is holding right now.</summary>
        internal static bool Held { get { lock (_lock) return _heldAt != DateTime.MinValue; } }

        /// <summary>Every hold so far, summed.</summary>
        internal static TimeSpan HeldTotal { get { lock (_lock) return _held; } }

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Sample) { IsBackground = true, Name = "GameClock" };
            _thread.Start();
        }

        private static void Sample()
        {
            while (true)
            {
                try
                {
                    bool holding = Player.InDungeonFloor() && Player.CheckDunIsPausedOrMenu();
                    lock (_lock)
                    {
                        if (holding && _heldAt == DateTime.MinValue) _heldAt = DateTime.UtcNow;
                        else if (!holding && _heldAt != DateTime.MinValue) { _held += DateTime.UtcNow - _heldAt; _heldAt = DateTime.MinValue; }
                    }
                }
                catch (Exception e) { Console.WriteLine("[GameClock] sample failed: " + e.Message); }
                Thread.Sleep(SampleMs);
            }
        }
    }
}
