using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Automated fishing data collection loop. Toggle via the mod window — the first manual
    /// session arms the loop, which then handles all subsequent entry and exit automatically.
    /// Toggle again at any time to stop.
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

        private static bool QuitDialogOpen() => Memory.ReadInt(FishingState.OverworldStateAddr) == FishingState.OverworldState_QuitDialog;
        private static bool InOverworld()    => Memory.ReadInt(FishingState.OverworldStateAddr) == FishingState.OverworldState_Overworld;

        // Survey data — accumulated for the lifetime of the run, reset on each Start().
        // Slot counts: fishId → per-TimeOfDay counts keyed by TimeOfDay
        // Session counts: same layout, species-agnostic — tracks how many sessions fell in
        //   each period (denominator for spawn-rate calculation).
        private static readonly ConcurrentDictionary<byte, Dictionary<TimeOfDay, int>> _counts = new();
        private static readonly Dictionary<TimeOfDay, int> _sessionBuckets = new()
        {
            [TimeOfDay.Morning]   = 0,
            [TimeOfDay.Afternoon] = 0,
            [TimeOfDay.Dusk]      = 0,
            [TimeOfDay.Night]     = 0,
        };
        private static int _sessionCount;
        private static DateTime _lastSummaryTime;

        // Writer thread: processes PINE button writes sequentially so the logic thread never
        // blocks on a PINE write. Each queue item is an Action (a write or a sleep). Items
        // execute in order, preserving hold times and inter-button gaps even when PINE is slow.
        private static readonly BlockingCollection<Action> _writeQueue = new();
        private static int _pendingCount = 0; // items enqueued or currently executing

        internal static bool IsRunning => _running;
        internal static bool IsPressingButton => _pendingCount > 0;
        internal static int PendingCount => _pendingCount;
        internal static int SessionCount => _sessionCount;

        // ---- Writer infrastructure ----

        static FishDataFarmer()
        {
            // Writer thread lives for the lifetime of the process (background, won't block exit).
            var writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "FishFarmer-Writer" };
            writerThread.Start();
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
            EnqueueWrite(() => Memory.WriteIntFast(Addresses.buttonInputs, button));
            EnqueueWrite(() => Thread.Sleep(holdMs));
            EnqueueWrite(() => Memory.WriteIntFast(Addresses.buttonInputs, 0));
            if (gapMs > 0)
                EnqueueWrite(() => { if (_running) Thread.Sleep(gapMs); });
        }

        // ---- Lifecycle ----

        /// <summary>
        /// Toggles the farmer on or off. When enabled, the first manual session starts the loop.
        /// </summary>
        internal static void Toggle()
        {
            if (Enabled || _running)
                Stop();
            else
            {
                Enabled = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Enabled — start a session to begin auto-farming");
            }
        }

        /// <summary>
        /// Stops farming immediately and drains any pending controller writes.
        /// </summary>
        internal static void Stop()
        {
            Enabled = false;
            _running = false;
            // Drain pending queue items so the writer exits after its current action.
            while (_writeQueue.TryTake(out _))
                Interlocked.Decrement(ref _pendingCount);
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
            _sessionBuckets[TimeOfDay.Morning]   = 0;
            _sessionBuckets[TimeOfDay.Afternoon] = 0;
            _sessionBuckets[TimeOfDay.Dusk]      = 0;
            _sessionBuckets[TimeOfDay.Night]     = 0;
            _sessionCount = 0;
            _lastSummaryTime = DateTime.UtcNow;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "FishDataFarmer" };
            _thread.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[FishFarmer] Started");
        }

        /// <summary>
        /// Called from TownCharacter when FishingState transitions 1→0.
        /// </summary>
        internal static void OnSessionEnded()
        {
            _fishingEnded = true;
        }

        // ---- Survey recording ----

        /// <summary>
        /// Called once per session from Fishing.LogFishSession before the slot loop.
        /// Increments the session bucket for the current tod float, independent of species.
        /// </summary>
        internal static void RecordSession(float todFloat)
        {
            if (!_running) return;
            TimeOfDay timeOfDay = Fishing.GetCurrentTimeOfDay(todFloat);
            lock (_sessionBuckets) _sessionBuckets[timeOfDay]++;
        }

        /// <summary>
        /// Called from FishPhaseLogger for every slot in each session.
        /// Only records slots with a known fish ID; silently ignores Unknown entries.
        /// </summary>
        internal static void RecordSlot(byte fishId, float todFloat)
        {
            if (!_running) return;
            if (!FishDatabase.TryGetValue(fishId, out _)) return;
            TimeOfDay timeOfDay = Fishing.GetCurrentTimeOfDay(todFloat);
            var todCounts = _counts.GetOrAdd(fishId, _ => new Dictionary<TimeOfDay, int>
            {
                [TimeOfDay.Morning]   = 0,
                [TimeOfDay.Afternoon] = 0,
                [TimeOfDay.Dusk]      = 0,
                [TimeOfDay.Night]     = 0,
            });
            lock (todCounts) todCounts[timeOfDay]++;
        }

        /// <summary>
        /// Returns a formatted summary of accumulated slot and session counts, suitable for UI display.
        /// </summary>
        internal static string GetSurveyText()
        {
            if (_counts.IsEmpty) return "–";
            var builder = new System.Text.StringBuilder();
            int morningSessionCount, afternoonSessionCount, duskSessionCount, nightSessionCount;
            lock (_sessionBuckets)
            {
                morningSessionCount   = _sessionBuckets[TimeOfDay.Morning];
                afternoonSessionCount = _sessionBuckets[TimeOfDay.Afternoon];
                duskSessionCount      = _sessionBuckets[TimeOfDay.Dusk];
                nightSessionCount     = _sessionBuckets[TimeOfDay.Night];
            }
            builder.AppendLine($"Sessions: M={morningSessionCount} A={afternoonSessionCount} D={duskSessionCount} N={nightSessionCount}");
            builder.AppendLine("─────────────────────────────");
            foreach (byte fishId in _counts.Keys.OrderBy(k => k))
            {
                if (!_counts.TryGetValue(fishId, out var todCounts)) continue;
                int morning, afternoon, dusk, night;
                lock (todCounts)
                {
                    morning   = todCounts[TimeOfDay.Morning];
                    afternoon = todCounts[TimeOfDay.Afternoon];
                    dusk      = todCounts[TimeOfDay.Dusk];
                    night     = todCounts[TimeOfDay.Night];
                }
                int total = morning + afternoon + dusk + night;
                builder.AppendLine($"{FishDatabase.GetName(fishId)}: {total}  M={morning} A={afternoon} D={dusk} N={night}");
            }
            return builder.ToString().TrimEnd();
        }

        // ---- Farming loop ----

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
                        if (Memory.ReadByte(FishingState.ActiveAddr) == 1)
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
                    if (Memory.ReadByte(FishingState.ActiveAddr) == 1)
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
                Memory.WriteIntFast(Addresses.buttonInputs, 0);
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
                Enqueue((int)Button.Circle, holdMs: 50, gapMs: 0);

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
                    Enqueue((int)Button.DPad_Down, holdMs: 50, gapMs: 200);
                    Enqueue((int)Button.Cross,     holdMs: 50, gapMs: 0);

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
        /// Queues X → X to interact with the fishing sign.
        /// Waits up to 5 s for TownCharacter to signal the session started.
        /// </summary>
        private static void ReenterFishing()
        {
            _sessionActive = false;

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "[FishFarmer] Queuing re-entry: X → X");
            Enqueue((int)Button.Cross, holdMs: 50, gapMs: 150); // interact with sign
            Enqueue((int)Button.Cross, holdMs: 50, gapMs: 150); // confirm fishing

            if (!WaitUntil(() => _sessionActive, timeoutMs: 5000))
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[FishFarmer] Warning: session not signalled after re-entry");
        }

        // ---- Utilities ----

        private static void LogSurvey()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSurvey] {_sessionCount} sessions\n{GetSurveyText()}");
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
