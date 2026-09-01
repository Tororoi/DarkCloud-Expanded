using System;
using System.Text;
using static Dark_Cloud_Improved_Version.QueensCanalMist;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Time-of-day canal tide for Queens (MapNo 2). Two coupled pieces:
    ///
    ///  1) FISHING level — <see cref="QueensWaterLevel"/> is pushed into the injected _LOAD_FISHING_DATA water
    ///     arg (CustomFishingSpot), so fish seed at WaterLevel-depth and the bobber rides it at the tide level.
    ///
    ///  2) VISIBLE surface — the canal you see (walking AND fishing) is the scene mesh node <c>mizu__a01</c>
    ///     (sub-file e03c08): 88 verts at Y~30 spanning the whole canal. It loads via LoadMDSFile as a CFrame
    ///     (size 0x270, name@+0x118, local matrix@+0x1d0 whose translation Y is +0x204, world-dirty flag@+0x240).
    ///     The node matrix is identity at Y=0, so world surface Y = vertexY(30) + matrixTransY. Bumping +0x204
    ///     to (level-30) and clearing +0x240 raises the mesh to the tide level; the scene never rewrites a
    ///     static node's LOCAL matrix, so ONE write PERSISTS — no per-tick pinning (unlike the CWater plane,
    ///     which DrawWaterSurface re-derives every frame from the camera — that was the wrong lever).
    ///
    /// The frame is found by name-scanning the map allocator (LoadMDSFile allocates scene frames from the
    /// CDataAlloc2 at guest 0x1F06650); the address is cached and only re-scanned when the town changes.
    ///
    /// Levels (design, judged in tools/queens/queens_viewer.py): LOW = morning 8 (canal floor exposed and fishable —
    /// climb the ladder down), MEDIUM = afternoon + night 31 (vanilla ~30), HIGH = dusk 52. See
    /// <see cref="TargetY"/> for the live values. Period from the same clock the fishing code reads
    /// (<see cref="Fishing.GetCurrentTimeOfDay()"/>).
    ///
    /// QUEENS-ONLY (MapNo 2).
    ///
    /// Split 2026-09: this class owns the tide LEVEL (target/shown, fade-hidden snap, mizu frame write) and
    /// orchestrates the Queens-only sub-systems: CanalEvict (tide-evict + dock camera), CanalWading (early
    /// player/cape draw), CanalWaterEffects (CWater body, refraction pin, ripple decals), the ladder tide
    /// gate (below) and
    /// QueensCanalMist (waterfall mist).
    /// </summary>

    internal static class CanalTide
    {
        internal static bool Enabled = true;

        private const float MizuBaselineY   = 30f;         // mizu__a01 vertex surface height (world, node matrix = 0)
        private const long  FrameName       = 0x118;       // CFrame node-name string
        private const long  FrameMatTransY  = 0x204;       // CFrame local matrix (+0x1d0) row-3 Y
        private const long  FrameWorldDirty = 0x240;       // 0 => world matrix recomputed from local next update
        private static readonly byte[] MizuName = Encoding.ASCII.GetBytes("mizu__a01");

        internal const float LowTideThreshold = 10f;           // matches QueensLowTide's own threshold

        internal static bool Diagnostics = false;     // log frame-find + writes (dev)
        // Snap the tide while the fade box is this dark (of 128). The mod ticks ~50 ms (~3 frames), so this has
        // to be low enough that a tick reliably lands inside the black window, high enough that the screen is
        // genuinely covered — the fade holds near-black across the whole period change, so 100 catches it.
        internal const  float FadeSnapAlpha  = 100f;
        private const  int   FadeGraceTicks = 60;        // ~3 s: no fade seen -> snap anyway, still instantly

        private static long  _frame;                   // cached mizu__a01 CFrame (mmu); 0 = unknown
        private static float _shownWaterLevel = float.NaN;    // water level currently displayed (lags target while hidden)
        private static float _lastMeshLevel = float.NaN;    // last level written to the mesh (set-once while stable)
        private static int   _lastBakedLevel = int.MinValue;
        private static int   _tickCount, _nextFrameScanTick, _pendingFadeTicks;  // re-scan throttle + frames a change has waited for a fade
        private static bool  _loggedFound, _loggedMiss;

        internal static void Log(string m, string tag = nameof(CanalTide)) { if (Diagnostics) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[" + tag + "] " + m); }

        /// <summary>Queens fishing water level for the current time of day (LOW = morning 8, MEDIUM =
        /// afternoon + night 31, HIGH = dusk 52). Pushed into the injected _LOAD_FISHING_DATA water arg
        /// at session setup.</summary>
        internal static float QueensWaterLevel() => TargetY(Fishing.GetCurrentTimeOfDay());

        /// <summary>True at LOW tide — the canal floor is exposed and reachable by the ladder. Derived from the
        /// level itself rather than the time period, so it stays correct if the tide chart is re-tuned.</summary>
        internal static bool QueensLowTide() => QueensWaterLevel() <= 10f;

        internal static void Tick()
        {
            if (!Enabled) return;
            CanalEvict.DockCamera();   // post-warp: set the dock camera in East Harbor — runs in ANY map, so BEFORE the Queens bail
            if (Memory.ReadInt(EditLoop.MapNo) != TownMapNo.Queens)
            {
                _frame = 0; _shownWaterLevel = float.NaN; _lastMeshLevel = float.NaN; _lastBakedLevel = int.MinValue;
                CanalWaterEffects.Reset(); CanalWading.Reset(); CanalEvict.Reset(); _loggedLadderGate = false;   // Queens-only state
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);                         // disarm the baked redraw
                ClearSprayTable();                                                        // no waterfall mist outside Queens
                return;
            }

            _tickCount++;

            float target = QueensWaterLevel();
            if (float.IsNaN(_shownWaterLevel)) _shownWaterLevel = target;    // first frame in town — start at the current level

            CanalEvict.Update(_shownWaterLevel, target);   // tide-evict flag + arrival camera-swing kill (Queens fade-out)

            // (re)locate the mesh CFrame. A fresh find means the town just (re)loaded — safe to snap under the
            // load's own black screen.
            bool freshFrame = false;
            if (!FrameStillMizu(_frame) && _tickCount >= _nextFrameScanTick)
            {
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);   // disarm while the frame is unknown/stale
                _frame = FindMizuFrame();
                if (_frame == 0)
                {
                    _nextFrameScanTick = _tickCount + 100;   // ~5s back-off (the scan is heavy)
                    if (!_loggedMiss) { Log("mizu__a01 CFrame not found in 0x20300000-0x21E00000"); _loggedMiss = true; }
                }
                else
                {
                    freshFrame = true; _loggedMiss = false; _lastMeshLevel = float.NaN;
                    if (!_loggedFound) { Log($"mizu__a01 CFrame @0x{_frame:X}"); _loggedFound = true; }
                }
            }

            // The tide is DISCRETE: the surface only ever JUMPS between the per-period levels, never slides.
            // Hide the jump inside the time-change fade — snap while the screen is blacked (fade alpha near
            // 128) or on a fresh town load. If a change somehow never gets a fade (alpha never rises, e.g. the
            // period rolled over while the mod was mid-attach), snap anyway once the grace period is up: an
            // instant change is right even when it isn't hidden. This used to RAMP in that case, which is what
            // made the water visibly slide between levels.
            if (Math.Abs(target - _shownWaterLevel) > 0.01f)
            {
                float alpha = Memory.ReadFloat(EditLoop.FadeBoxAlpha);
                bool hidden = freshFrame || alpha >= FadeSnapAlpha;
                if (hidden || ++_pendingFadeTicks > FadeGraceTicks) { _shownWaterLevel = target; _pendingFadeTicks = 0; }
            }
            else _pendingFadeTicks = 0;

            // Waterfall mist only at LOW tide (the falls meet the drained canal then); clear it otherwise.
            if (_shownWaterLevel <= LowTideThreshold) WriteSprayTable(_shownWaterLevel); else ClearSprayTable();

            // Write the shown level to the SURFACE mesh (CFrame set-once while stable, re-applied on a fresh
            // frame) and to the RIPPLE (CEditGround CWater body, pinned every frame — see PinRipple).
            if (_frame != 0 && (freshFrame || Math.Abs(_shownWaterLevel - _lastMeshLevel) > 0.01f))
            {
                Memory.WriteFloat(_frame + FrameMatTransY, _shownWaterLevel - MizuBaselineY);
                Memory.WriteIntFast(_frame + FrameWorldDirty, 0);   // force world-matrix recompute from local
                _lastMeshLevel = _shownWaterLevel;
            }
            bool low = _shownWaterLevel <= LowTideThreshold;
            CanalWading.Arm(low);                       // low tide: early player/cape draw under the water pass
            CanalWaterEffects.Tick(_shownWaterLevel, low, _frame);  // refraction pin, quad colour, ripple decals
            LadderGateApply(low);

            // Re-bake the FISHING water (baked into the injected script at install) once the shown level has
            // settled on a new level — re-writes only the fishing bytecode, skipped mid-session.
            int t = (int)MathF.Round(target);
            if (Math.Abs(_shownWaterLevel - target) < 0.01f && t != _lastBakedLevel)
            { CustomFishingSpot.RebuildFishingScript(); _lastBakedLevel = t; }
        }

        private static bool FrameStillMizu(long f)
        {
            if (f == 0) return false;
            byte[] n = Memory.ReadBytesBatch(f + FrameName, MizuName.Length);
            if (n == null) return false;
            for (int i = 0; i < MizuName.Length; i++)
                if (n[i] != MizuName[i]) return false;
            return true;
        }

        // Broad one-shot scan of main RAM for the mizu__a01 CFrame's name (the scene CFrames aren't in the
        // model/base allocators, so locate them directly and cache the address). Logs the hit so the region
        // can be pinned down; EditInfo.Base is logged for reference.
        private static long FindMizuFrame()
        {
            uint ei = Memory.ReadUInt(EditInfo.EditInfoPtr) & Memory.PhysAddrMask;
            Log($"EditInfo.Base=0x{ei:X} — broad-scanning for mizu__a01…");
            const long START = 0x20300000, END = 0x21E00000;
            const int PAGE = 0x40000;                              // 256 KB pages, overlapped by the needle
            for (long p = START; p < END; p += PAGE - MizuName.Length)
            {
                byte[] buf = Memory.ReadBytesBatch(p, (int)Math.Min(PAGE, END - p));
                if (buf == null) continue;
                int idx = ReusableFunctions.IndexOfBytes(buf, MizuName);
                if (idx >= 0)
                {
                    long frame = p + idx - FrameName;
                    Log($"mizu__a01 name @0x{p + idx:X} -> CFrame 0x{frame:X}");
                    return frame;
                }
            }
            Log("mizu__a01 not found in 0x20300000-0x21E00000");
            return 0;
        }

        // ── Canal-ladder tide gate (former CanalLadderGate.cs) ──────────────────────────────────────
        // Canal-ladder tide gate: the injected event points all sit at x≈LadderWorldX (706) — the climb pair (rec
        // types 4/5) plus our co-located type-3 message point (label 402). CheckEventPoint bails on
        // enabled(+0x00)==0, and EdGetEvent matches ONE point, so flipping enabled by tide switches which one
        // the X-press hits: LOW → climb pair on / message off (real climb); otherwise → climb pair off /
        // message on (the "tide too high" line). Re-asserted every tick so a town rebuild can't strand it.
        private const long EvArrPtr = 0x01D19700, EvCountAddr = 0x01D19704;  // live ED_EVENT_POINT array ptr + count (guest form of EventPoints.ArrayPtr/Count)
        private const long EvStride = EventPoints.Stride, EvEnabled = EventPoints.Enabled, EvType = EventPoints.Type,
                           EvLabel = EventPoints.ItemOrLabel, EvPos = EventPoints.Position;
        private const int  LadderMsgLabel = FishingLabelIds.LadderMsgLabelId;   // == 402
        private const float LadderGateX = 706f, LadderGateXTol = 12f;        // LadderWorldX ± tol — only our cluster
        private static bool _loggedLadderGate;

        private static void LadderGateApply(bool low)
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

        private static float TargetY(TimeOfDay tod) => tod switch
        {
            // 2026-08 low-tide-fishing chart: LOW = morning (canal floor walkable/fishable),
            // MEDIUM = afternoon + night (vanilla 31), HIGH = dusk (arch crown underside is Y=60).
            // Low raised 6 -> 8 (more clearance over the floor for the ladder/wading/fish-depth geometry).
            TimeOfDay.Night   => 31f,
            TimeOfDay.Morning => 8f,
            TimeOfDay.Dusk    => 52f,
            _                 => 31f,   // Afternoon (and any fallback): vanilla
        };
    }
}
