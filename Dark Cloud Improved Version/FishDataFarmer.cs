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

        private const int ButtonAddr        = 0x21CBC544;
        private const int FishingStateAddr  = 0x21D19714;
        private const int TriggerIndexAddr  = 0x202A1F64; // game writes active fishing trigger here
        private const int MuskaLackaTrigger = 5;

        private const int Addr708             = 0x21D19708;
        private const int State708_QuitDialog = 0x00000085; // quit dialog open
        private const int State708_Overworld  = 0x0000000C; // back in overworld

        // 0x21D33E28 — fishing phase state machine
        private const int FishPhase_BaitScreen   = 0x00000000;
        private const int FishPhase_Casting      = 0x00000004;
        private const int FishPhase_HookInWater  = 0x00000005;
        private const int FishPhase_DraggingHook = 0x0000000D; // moving Toan to drag hook
        private const int FishPhase_Uncasting    = 0x00000007; // X cancel while rod out
        private const int FishPhase_ReelingIn    = 0x0000000C;
        private const int FishPhase_PullingOut   = 0x0000000A; // fish leaving water
        private const int FishPhase_HoldingFish  = 0x00000008; // measurements shown
        private const int FishPhase_ThrowingBack = 0x00000009; // landing animation

        private static bool QuitDialogOpen() => Memory.ReadInt(Addr708) == State708_QuitDialog;
        private static bool InOverworld()    => Memory.ReadInt(Addr708) == State708_Overworld;

        // Survey data — accumulated for the lifetime of the run, reset on each Start().
        // fishId → slot count per ToD: [Morning=0, Afternoon=1, Dusk=2, Night=3]
        private static readonly Dictionary<byte, int[]> _counts = new Dictionary<byte, int[]>();
        // fishId → smallest size float seen
        private static readonly Dictionary<byte, float> _minSize = new Dictionary<byte, float>();
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

        /// <summary>
        /// Called from Fishing.LogFishSession for every slot in each session.
        /// Only records slots with a known fish ID; silently ignores Unknown entries.
        /// </summary>
        internal static void RecordSlot(byte fishId, TimeOfDay tod, float size)
        {
            if (!_running) return;
            if (!FishDatabase.TryGetValue(fishId, out _)) return;

            if (!_counts.TryGetValue(fishId, out int[] c))
            {
                c = new int[4];
                _counts[fishId] = c;
            }
            c[(int)tod]++;

            if (!_minSize.TryGetValue(fishId, out float cur) || size < cur)
                _minSize[fishId] = size;
        }

        internal static string GetSurveyText()
        {
            if (_counts.Count == 0) return "–";
            var sb = new System.Text.StringBuilder();
            foreach (byte id in _counts.Keys.OrderBy(k => k))
            {
                int[] c = _counts[id];
                int total = c[0] + c[1] + c[2] + c[3];
                _minSize.TryGetValue(id, out float min);
                sb.AppendLine($"{FishDatabase.GetName(id)}: {total}  min={(int)(min * 10)}cm");
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
                        if (Memory.ReadByte(FishingStateAddr) == 1)
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
                    if (Memory.ReadByte(FishingStateAddr) == 1)
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
        /// for the quit dialog (0x85). If the dialog appears, queues Down→X and waits for
        /// the overworld (0x0C) or _fishingEnded. Retries O up to 3 times on failure — never
        /// presses X speculatively, because pressing X on the bait screen casts the rod.
        /// </summary>
        private static void ExitFishing()
        {
            // Fast path: TownCharacter already signalled exit before we arrived.
            if (_fishingEnded) { _fishingEnded = false; Thread.Sleep(400); return; }
            _fishingEnded = false;

            for (int attempt = 1; attempt <= 3 && _running; attempt++)
            {
                // First attempt: brief pause so the bait screen is fully interactive.
                // Retries: short gap before re-pressing O.
                Thread.Sleep(attempt == 1 ? 300 : 500);

                if (attempt > 1)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[FishFarmer] Quit dialog not seen (attempt {attempt - 1}) — retrying O");

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishFarmer] Queuing O (attempt {attempt}) → waiting for quit dialog");
                Enqueue(Circle, holdMs: 50, gapMs: 0);

                if (!WaitUntil(QuitDialogOpen, timeoutMs: 2000))
                    continue;

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[FishFarmer] Quit dialog open → Down → X");
                Thread.Sleep(200); // give dialog time to become interactive before pressing
                Enqueue(DPadDown, holdMs: 50, gapMs: 100);
                Enqueue(Cross,    holdMs: 50, gapMs: 0);

                if (WaitUntil(() => _fishingEnded || InOverworld(), timeoutMs: 4000)) break;

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[FishFarmer] Exit not confirmed after Down → X");
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

            // TriggerIndex write goes through the queue so it doesn't block the logic thread.
            EnqueueWrite(() => Memory.WriteIntFast(TriggerIndexAddr, MuskaLackaTrigger));

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
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[FishSurvey] {_sessionCount} sessions");
            foreach (byte id in _counts.Keys.OrderBy(k => k))
            {
                int[] c = _counts[id];
                int total = c[0] + c[1] + c[2] + c[3];
                _minSize.TryGetValue(id, out float min);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[FishSurvey] {FishDatabase.GetName(id)}(id={id}) " +
                    $"Morning={c[0]} Afternoon={c[1]} Dusk={c[2]} Night={c[3]} total={total} " +
                    $"minSize={min:F4}({(int)(min * 10)}cm)");
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
