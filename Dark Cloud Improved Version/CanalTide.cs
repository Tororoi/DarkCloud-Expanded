using System;
using System.Text;

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
    /// Levels (design, judged in tools/queens_viewer.py): afternoon 31 (low / vanilla ~30), morning &amp; dusk
    /// 50, night 64 (bridge-arch crown underside Y=60 — night a touch into the arch by choice). Period from the
    /// same clock the fishing code reads (<see cref="Fishing.GetCurrentTimeOfDay()"/>).
    ///
    /// QUEENS-ONLY (MapNo 2).
    /// </summary>
    internal static class CanalTide
    {
        internal static bool Enabled = true;
        internal const int QueensMapNo = 2;

        private const float MizuBaselineY   = 30f;         // mizu__a01 vertex surface height (world, node matrix = 0)
        private const long  FrameName       = 0x118;       // CFrame node-name string
        private const long  FrameMatTransY  = 0x204;       // CFrame local matrix (+0x1d0) row-3 Y
        private const long  FrameWorldDirty = 0x240;       // 0 => world matrix recomputed from local next update
        private static readonly byte[] MizuName = Encoding.ASCII.GetBytes("mizu__a01");

        internal static bool Diagnostics = true;      // log frame-find + writes while we validate the lever
        private const  long  FadeAlpha    = 0x21D3D1CC;  // Ed time-change fade box alpha: 0 clear .. 128 black

        // Canal RIPPLE lever. The animated water is the mapinfo WATER "e03c08" body drawn by
        // DrawWaterSurface__11CEditGround. The CEditGround is *(gp-0x6f18) = *(0x202A28D8) — NOT edit_info; its
        // CWater array is at base+0x15040 (4 bodies, stride 0x3B0), pos.y @ +0x44, active @ +0x20 (Y-follow flag
        // off, so a write to +0x44 holds). The canal body sits at Y31; the two fountain pools (Y113 / Y5.6) are
        // left alone. Re-pinned every frame because a Queens area transition rebuilds the array back to Y31.
        private const long RippleEditGroundPtr = 0x202A28D8;
        private const long RippleArrOff = 0x15040, RippleStride = 0x3B0;
        private const int  RippleSlots  = 4;
        private static long  _frame;                   // cached mizu__a01 CFrame (mmu); 0 = unknown
        private static float _shownLvl = float.NaN;    // water level currently displayed (lags target while hidden)
        private static float _lastMizu = float.NaN;    // last level written to the mesh (set-once while stable)
        private static int   _rebakeLvl = int.MinValue;
        private static int   _tick, _nextScan, _pend;  // re-scan throttle + frames a change has waited for a fade
        private static bool  _loggedFound, _loggedMiss;

        private static void Log(string m) { if (Diagnostics) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[CanalTide] " + m); }

        /// <summary>Queens fishing water level for the current time of day (LOW = morning 6, MEDIUM =
        /// afternoon + night 31, HIGH = dusk 52). Pushed into the injected _LOAD_FISHING_DATA water arg
        /// at session setup.</summary>
        internal static float QueensWaterLevel() => TargetY(Fishing.GetCurrentTimeOfDay());

        internal static void Tick()
        {
            if (!Enabled) return;
            if (Memory.ReadInt(EditLoop.MapNo) != QueensMapNo)
            { _frame = 0; _shownLvl = float.NaN; _lastMizu = float.NaN; _rebakeLvl = int.MinValue; return; }
            _tick++;

            float target = QueensWaterLevel();
            if (float.IsNaN(_shownLvl)) _shownLvl = target;    // first frame in town — start at the current level

            // (re)locate the mesh CFrame. A fresh find means the town just (re)loaded — safe to snap under the
            // load's own black screen.
            bool freshFrame = false;
            if (!FrameStillMizu(_frame) && _tick >= _nextScan)
            {
                _frame = FindMizuFrame();
                if (_frame == 0)
                {
                    _nextScan = _tick + 100;   // ~5s back-off (the scan is heavy)
                    if (!_loggedMiss) { Log("mizu__a01 CFrame not found in 0x20300000-0x21E00000"); _loggedMiss = true; }
                }
                else
                {
                    freshFrame = true; _loggedMiss = false; _lastMizu = float.NaN;
                    if (!_loggedFound) { Log($"mizu__a01 CFrame @0x{_frame:X}"); _loggedFound = true; }
                }
            }

            // Move the shown level toward the target, but HIDE the change: snap only while the time-change fade
            // has blacked the screen (0x1D3D1CC alpha near 128), or during a fresh load. If a change somehow
            // gets no fade, ramp it slowly instead of jumping.
            if (Math.Abs(target - _shownLvl) > 0.01f)
            {
                float alpha = Memory.ReadFloat(FadeAlpha);
                if (freshFrame || alpha >= 110f) { _shownLvl = target; _pend = 0; }
                else if (++_pend > 40) _shownLvl += Math.Sign(target - _shownLvl) * Math.Min(1.2f, Math.Abs(target - _shownLvl));
            }
            else _pend = 0;

            // Write the shown level to the SURFACE mesh (CFrame set-once while stable, re-applied on a fresh
            // frame) and to the RIPPLE (CEditGround CWater body, pinned every frame — see PinRipple).
            if (_frame != 0 && (freshFrame || Math.Abs(_shownLvl - _lastMizu) > 0.01f))
            {
                Memory.WriteFloat(_frame + FrameMatTransY, _shownLvl - MizuBaselineY);
                Memory.WriteIntFast(_frame + FrameWorldDirty, 0);   // force world-matrix recompute from local
                _lastMizu = _shownLvl;
            }
            PinRipple(_shownLvl);

            // Re-bake the FISHING water (baked into the injected script at install) once the shown level has
            // settled on a new level — re-writes only the fishing bytecode, skipped mid-session.
            int t = (int)MathF.Round(target);
            if (Math.Abs(_shownLvl - target) < 0.01f && t != _rebakeLvl)
            { CustomFishingSpot.RebuildFishingScript(); _rebakeLvl = t; }
        }

        /// <summary>Pin the canal ripple (CEditGround CWater body for WATER "e03c08") to the shown tide level so
        /// it tracks the mizu surface. Writes pos.y (+0x44) of the active body sitting in the tide range; the two
        /// fountain pools (Y~113 / ~5.6) are outside it and left alone. Cheap enough to run every frame, which is
        /// needed because a town-area transition rebuilds the array back to the mapinfo Y (31).</summary>
        private static void PinRipple(float level)
        {
            uint egGuest = Memory.ReadUInt(RippleEditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egGuest)) return;
            long arr = Memory.ToMmu(egGuest) + RippleArrOff;
            for (int i = 0; i < RippleSlots; i++)
            {
                long b = arr + i * RippleStride;
                if (Memory.ReadInt(b + 0x20) == 0) continue;               // inactive body
                float y = Memory.ReadFloat(b + 0x44);
                if (y < 20f || y > 80f) continue;                          // a fountain pool, not the canal
                if (Math.Abs(y - level) > 0.01f) Memory.WriteFloat(b + 0x44, level);
            }
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
            uint ei = Memory.ReadUInt(0x202A27B0) & Memory.PhysAddrMask;
            Log($"EditInfo.Base=0x{ei:X} — broad-scanning for mizu__a01…");
            const long START = 0x20300000, END = 0x21E00000;
            const int PAGE = 0x40000;                              // 256 KB pages, overlapped by the needle
            for (long p = START; p < END; p += PAGE - MizuName.Length)
            {
                byte[] buf = Memory.ReadBytesBatch(p, (int)Math.Min(PAGE, END - p));
                if (buf == null) continue;
                int idx = IndexOf(buf, MizuName);
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

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        private static float TargetY(TimeOfDay tod) => tod switch
        {
            // 2026-08 low-tide-fishing chart: LOW = morning (canal floor walkable/fishable),
            // MEDIUM = afternoon + night (vanilla 31), HIGH = dusk (arch crown underside is Y=60).
            TimeOfDay.Night   => 31f,
            TimeOfDay.Morning => 6f,
            TimeOfDay.Dusk    => 52f,
            _                 => 31f,   // Afternoon (and any fallback): vanilla
        };
    }
}
