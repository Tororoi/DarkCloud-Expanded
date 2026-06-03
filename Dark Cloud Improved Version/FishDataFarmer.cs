using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Automated fishing data collection loop. Toggle with L3 while standing next to a fishing
    /// sign — the first manual session arms the loop, which then handles all subsequent entry
    /// and exit automatically. Press L3 again at any time to stop.
    /// </summary>
    internal static class FishDataFarmer
    {
        internal static bool Enabled = false;

        private static Thread _thread;
        private static volatile bool _running;
        // Set by OnSessionDetected (TownCharacter's thread) every time a session starts.
        private static volatile bool _sessionActive = false;
        // Set by OnSessionEnded (TownCharacter's thread) when FishingState drops to 0.
        private static volatile bool _fishingEnded = false;

        // PS2 controller bitmask constants (from TASThread.cs comments)
        private const int Cross    = 64;
        private const int Circle   = 32;
        private const int DPadDown = 16384;

        private const int ButtonAddr = 0x21CBC544;

        private static bool QuitDialogOpen() => Memory.ReadInt(FishingState.Addr708) == FishingState.State708_QuitDialog;
        private static bool InOverworld()    => Memory.ReadInt(FishingState.Addr708) == FishingState.State708_Overworld;

        // Survey data — accumulated for the lifetime of the run, reset on each Start().
        // Slot counts: fishId → [Morning_H1, Morning_H2, Morning_Peak, ..., Night_Peak]
        // Session counts: same 12-bucket layout, species-agnostic — tracks how many sessions
        //   fell in each sub-bucket (denominator for spawn-rate calculation).
        // H1/H2 split at the period midpoint; Peak = 0.2-unit window centered there, overlapping each half by 0.1.
        private static readonly Dictionary<byte, int[]> _counts = new Dictionary<byte, int[]>();
        private static readonly Dictionary<byte, float> _minSize = new Dictionary<byte, float>();
        private static readonly int[] _sessionBuckets = new int[12];
        private const int Morning_H1   = 0,  Morning_H2   = 1,  Morning_Peak   = 2;
        private const int Afternoon_H1 = 3,  Afternoon_H2 = 4,  Afternoon_Peak = 5;
        private const int Dusk_H1      = 6,  Dusk_H2      = 7,  Dusk_Peak      = 8;
        private const int Night_H1     = 9,  Night_H2     = 10, Night_Peak     = 11;
        private static int _sessionCount;
        private static DateTime _lastSummaryTime;

        // Writer thread: processes PINE button writes sequentially so the logic thread never
        // blocks on a PINE write. Each queue item is an Action (a write or a sleep). Items
        // execute in order, preserving hold times and inter-button gaps even when PINE is slow.
        private static readonly BlockingCollection<Action> _writeQueue = new();
        private static int _pendingCount = 0; // items enqueued but not yet executed

        internal static bool IsRunning => _running;
        internal static bool IsPressingButton => _pendingCount > 0;
        internal static int PendingCount => _pendingCount;
        internal static int SessionCount => _sessionCount;

        static FishDataFarmer()
        {
            // Writer thread lives for the lifetime of the process (background, won't block exit).
            var t = new Thread(WriterLoop) { IsBackground = true, Name = "FishFarmer-Writer" };
            t.Start();
        }

        private static void WriterLoop()
        {
            while (true)
            {
                if (!_writeQueue.TryTake(out Action action, millisecondsTimeout: 50))
                    continue;
                try   { action(); }
                catch (Exception ex)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishFarmer] Writer error: {ex.Message}");
                }
                finally { Interlocked.Decrement(ref _pendingCount); }
            }
        }

        private static void EnqueueWrite(Action action)
        {
            Interlocked.Increment(ref _pendingCount);
            _writeQueue.Add(action);
        }

        // Queues a full button press: write button → hold holdMs → write 0 → gap gapMs.
        // Gap sleeps are skipped when _running is false so Stop() drains quickly.
        private static void Enqueue(int button, int holdMs = 50, int gapMs = 0)
        {
            EnqueueWrite(() => Memory.WriteIntFast(ButtonAddr, button));
            EnqueueWrite(() => Thread.Sleep(holdMs));
            EnqueueWrite(() => Memory.WriteIntFast(ButtonAddr, 0));
            if (gapMs > 0)
                EnqueueWrite(() => { if (_running) Thread.Sleep(gapMs); });
        }

        /// <summary>
        /// Cycles through armed → running → stopped states on each L3 press.
        /// </summary>
        internal static void Toggle()
        {
            if (_running)
            {
                Stop();
            }
            else if (Enabled)
            {
                Enabled = false;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Disarmed");
            }
            else
            {
                Enabled = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Armed — start the first session manually to begin auto-farming");
            }
        }

        /// <summary>
        /// Called from TownCharacter every time a fishing session is detected (FishingState 0→1).
        /// Sets _sessionActive so Run() can react without reading FishingState itself.
        /// On the very first call it also starts the background thread.
        /// </summary>
        internal static void OnSessionDetected()
        {
            if (!Enabled) return;
            _sessionActive = true;
            _fishingEnded = false; // clear any stale exit signal from a previous session
            if (_running) return;
            _counts.Clear();
            _minSize.Clear();
            Array.Clear(_sessionBuckets, 0, _sessionBuckets.Length);
            _sessionCount = 0;
            _lastSummaryTime = DateTime.UtcNow;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "FishDataFarmer" };
            _thread.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Started");
        }

        internal static void Stop()
        {
            Enabled = false;
            _running = false;
            // Drain pending queue items so the writer exits after its current action.
            while (_writeQueue.TryTake(out _))
                Interlocked.Decrement(ref _pendingCount);
        }

        /// <summary>
        /// Called from TownCharacter when FishingState transitions 1→0.
        /// </summary>
        internal static void OnSessionEnded()
        {
            _fishingEnded = true;
        }

        // Returns (baseIdx, secondHalf, peak) for a tod float.
        // baseIdx: 0=Morning, 3=Afternoon, 6=Dusk, 9=Night.
        // Peak = 0.2-unit window centered on the period midpoint, overlapping each half by 0.1.
        private static (int baseIdx, bool secondHalf, bool peak) ClassifyTod(float t)
        {
            if (t >= 8.5f && t < 11.5f) // Morning, mid=10.0
                return (Morning_H1, t >= 10.0f, t >= 9.9f && t < 10.1f);
            if (t >= 5.5f && t < 8.5f)  // Night, mid=7.0
                return (Night_H1, t >= 7.0f, t >= 6.9f && t < 7.1f);
            if (t >= 2.5f && t < 5.5f)  // Dusk, mid=4.0
                return (Dusk_H1, t >= 4.0f, t >= 3.9f && t < 4.1f);
            // Afternoon (wraps 11.5→12→0→2.5), mid=1.0; second half is the non-wrapping segment
            return (Afternoon_H1, t >= 1.0f && t < 2.5f, t >= 0.9f && t < 1.1f);
        }

        /// <summary>
        /// Called once per session from Fishing.LogFishSession before the slot loop.
        /// Increments the session bucket for the current tod float, independent of species.
        /// </summary>
        internal static void RecordSession(float todFloat)
        {
            if (!_running) return;
            var (periodStart, secondHalf, peak) = ClassifyTod(todFloat);
            _sessionBuckets[periodStart + (secondHalf ? 1 : 0)]++;
            if (peak) _sessionBuckets[periodStart + 2]++;
        }

        /// <summary>
        /// Called from Fishing.LogFishSession for every slot in each session.
        /// Only records slots with a known fish ID; silently ignores Unknown entries.
        /// </summary>
        internal static void RecordSlot(byte fishId, float todFloat, float size)
        {
            if (!_running) return;
            if (!FishDatabase.TryGetValue(fishId, out _)) return;

            if (!_counts.TryGetValue(fishId, out int[] c))
            {
                c = new int[12];
                _counts[fishId] = c;
            }
            var (baseIdx, secondHalf, peak) = ClassifyTod(todFloat);
            c[baseIdx + (secondHalf ? 1 : 0)]++;
            if (peak) c[baseIdx + 2]++;

            if (!_minSize.TryGetValue(fishId, out float cur) || size < cur)
                _minSize[fishId] = size;
        }

        internal static string GetSurveyText()
        {
            if (_counts.Count == 0) return "–";
            var sb = new System.Text.StringBuilder();
            int[] s = _sessionBuckets;
            int mSess = s[Morning_H1]   + s[Morning_H2];
            int aSess = s[Afternoon_H1] + s[Afternoon_H2];
            int dSess = s[Dusk_H1]      + s[Dusk_H2];
            int nSess = s[Night_H1]     + s[Night_H2];
            sb.AppendLine($"M={mSess}(H1={s[Morning_H1]},H2={s[Morning_H2]},pk={s[Morning_Peak]}) A={aSess}(H1={s[Afternoon_H1]},H2={s[Afternoon_H2]},pk={s[Afternoon_Peak]})");
            sb.AppendLine($"D={dSess}(H1={s[Dusk_H1]},H2={s[Dusk_H2]},pk={s[Dusk_Peak]}) N={nSess}(H1={s[Night_H1]},H2={s[Night_H2]},pk={s[Night_Peak]})");
            sb.AppendLine("─────────────────────────────");
            foreach (byte id in _counts.Keys.OrderBy(k => k))
            {
                int[] counts = _counts[id];
                int morning   = counts[Morning_H1]   + counts[Morning_H2];
                int afternoon = counts[Afternoon_H1] + counts[Afternoon_H2];
                int dusk      = counts[Dusk_H1]      + counts[Dusk_H2];
                int night     = counts[Night_H1]     + counts[Night_H2];
                _minSize.TryGetValue(id, out float minSize);
                sb.AppendLine($"{FishDatabase.GetName(id)}: {morning + afternoon + dusk + night}  min={(int)(minSize * 10)}cm  M={morning} A={afternoon} D={dusk} N={night}");
            }
            return sb.ToString().TrimEnd();
        }

        private static void Run()
        {
            try
            {
                while (_running)
                {
                    // Wait for TownCharacter to signal a session start rather than reading
                    // FishingState ourselves — avoids pressing X on a stale PINE read.
                    if (!WaitUntil(() => _sessionActive, timeoutMs: 10000))
                    {
                        if ((DateTime.UtcNow - _lastSummaryTime).TotalMinutes >= 1.0)
                        {
                            LogSurvey();
                            _lastSummaryTime = DateTime.UtcNow;
                        }
                        // No signal after 10 s. Check state: if still in fishing (e.g. O was
                        // delivered too late and auto-cast fired), force an exit first.
                        if (!_running) break;
                        if (Memory.ReadByte(FishingState.FishingStateAddr) == 1)
                            ExitFishing();
                        ReenterFishing();
                        continue;
                    }

                    _sessionActive = false; // consume the event
                    _sessionCount++;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishFarmer] Session {_sessionCount} active");
                    if (!_running) break;

                    ExitFishing();
                    if (!_running) break;

                    // If ExitFishing's two-attempt recovery still couldn't get out of fishing,
                    // stop rather than send re-entry X presses into the bait screen.
                    if (Memory.ReadByte(FishingState.FishingStateAddr) == 1)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            "[FishFarmer] Could not exit fishing — stopping to prevent unwanted cast");
                        break;
                    }

                    // Survey after exit, not before — LogSurvey can block on Console I/O
                    // and must not delay the O press that exits the bait screen.
                    if ((DateTime.UtcNow - _lastSummaryTime).TotalMinutes >= 1.0)
                    {
                        LogSurvey();
                        _lastSummaryTime = DateTime.UtcNow;
                    }

                    Thread.Sleep(1500); // let overworld time advance for broader ToD spread
                    ReenterFishing();
                }
            }
            finally
            {
                _running = false;
                // Drain queue before the synchronous write-zero so no enqueued write fires after it.
                while (_writeQueue.TryTake(out _))
                    Interlocked.Decrement(ref _pendingCount);
                Memory.WriteIntFast(ButtonAddr, 0);
                LogSurvey();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Stopped");
            }
        }

        /// <summary>
        /// Exits the bait screen using state-based detection on 0x21D19708.
        /// Waits 300ms for the bait screen to become interactive, then queues O and polls
        /// for the quit dialog (0x85). Once the dialog is open, retries Down→X up to 3 times
        /// (safe because Down doesn't wrap back to "Continue Fishing"). Retries O up to 3 times
        /// if the dialog never appears — never presses X speculatively, because pressing X on
        /// the bait screen casts the rod.
        /// </summary>
        private static void ExitFishing()
        {
            // Fast path: TownCharacter already signalled exit before we arrived.
            if (_fishingEnded) { _fishingEnded = false; Thread.Sleep(400); return; }
            _fishingEnded = false;

            bool exited = false;
            for (int oAttempt = 1; oAttempt <= 3 && _running && !exited; oAttempt++)
            {
                // First attempt: brief pause so the bait screen is fully interactive.
                // Retries: short gap before re-pressing O.
                Thread.Sleep(oAttempt == 1 ? 300 : 500);

                if (oAttempt > 1)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishFarmer] Quit dialog not seen (O attempt {oAttempt - 1}) — retrying O");

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishFarmer] Queuing O (attempt {oAttempt}) → waiting for quit dialog");
                Enqueue(Circle, holdMs: 50, gapMs: 0);

                if (!WaitUntil(QuitDialogOpen, timeoutMs: 2000))
                    continue;

                // Dialog confirmed open — retry Down→X until exit confirmed or dialog closes.
                for (int downXAttempt = 1; downXAttempt <= 3 && _running; downXAttempt++)
                {
                    if (downXAttempt > 1)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[FishFarmer] Retrying Down → X (attempt {downXAttempt})");
                    else
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            "[FishFarmer] Quit dialog open → Down → X");

                    Thread.Sleep(200); // give dialog time to become interactive before pressing
                    Enqueue(DPadDown, holdMs: 50, gapMs: 200);
                    Enqueue(Cross,    holdMs: 50, gapMs: 0);

                    if (WaitUntil(() => _fishingEnded || InOverworld(), timeoutMs: 4000))
                    {
                        exited = true;
                        break;
                    }

                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishFarmer] Exit not confirmed after Down → X (attempt {downXAttempt})");

                    if (!QuitDialogOpen()) break; // dialog dismissed without reaching overworld — re-press O
                }
            }

            if (!_fishingEnded && !InOverworld() && _running)
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[FishFarmer] ExitFishing failed — farming may be out of sync");
            _fishingEnded = false;
        }

        /// <summary>
        /// Queues TriggerIndex restore and X → X to interact with the fishing sign.
        /// Waits up to 15 s for TownCharacter to signal the session started.
        /// </summary>
        private static void ReenterFishing()
        {
            _sessionActive = false;

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "[FishFarmer] Queuing re-entry: X → X");
            Enqueue(Cross, holdMs: 50, gapMs: 150); // interact with sign
            Enqueue(Cross, holdMs: 50, gapMs: 150);   // confirm fishing

            if (!WaitUntil(() => _sessionActive, timeoutMs: 5000))
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[FishFarmer] Warning: session not signalled after re-entry");
        }

        private static void LogSurvey()
        {
            int[] s = _sessionBuckets;
            int mSess = s[Morning_H1]   + s[Morning_H2];
            int aSess = s[Afternoon_H1] + s[Afternoon_H2];
            int dSess = s[Dusk_H1]      + s[Dusk_H2];
            int nSess = s[Night_H1]     + s[Night_H2];
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSurvey] {_sessionCount} sessions  " +
                $"M={mSess}(H1={s[Morning_H1]},H2={s[Morning_H2]},pk={s[Morning_Peak]}) " +
                $"A={aSess}(H1={s[Afternoon_H1]},H2={s[Afternoon_H2]},pk={s[Afternoon_Peak]}) " +
                $"D={dSess}(H1={s[Dusk_H1]},H2={s[Dusk_H2]},pk={s[Dusk_Peak]}) " +
                $"N={nSess}(H1={s[Night_H1]},H2={s[Night_H2]},pk={s[Night_Peak]})");
            foreach (byte id in _counts.Keys.OrderBy(k => k))
            {
                int[] counts  = _counts[id];
                int morning   = counts[Morning_H1]   + counts[Morning_H2];
                int afternoon = counts[Afternoon_H1] + counts[Afternoon_H2];
                int dusk      = counts[Dusk_H1]      + counts[Dusk_H2];
                int night     = counts[Night_H1]     + counts[Night_H2];
                _minSize.TryGetValue(id, out float minSize);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishSurvey] {FishDatabase.GetName(id)}(id={id}) " +
                    $"M={morning}(H1={counts[Morning_H1]},H2={counts[Morning_H2]},pk={counts[Morning_Peak]}) " +
                    $"A={afternoon}(H1={counts[Afternoon_H1]},H2={counts[Afternoon_H2]},pk={counts[Afternoon_Peak]}) " +
                    $"D={dusk}(H1={counts[Dusk_H1]},H2={counts[Dusk_H2]},pk={counts[Dusk_Peak]}) " +
                    $"N={night}(H1={counts[Night_H1]},H2={counts[Night_H2]},pk={counts[Night_Peak]}) " +
                    $"total={morning + afternoon + dusk + night} minSize={minSize:F4}({(int)(minSize * 10)}cm)");
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (_running && DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(16);
            }
            return condition();
        }
    }
}
