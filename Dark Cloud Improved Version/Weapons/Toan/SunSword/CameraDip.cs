using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The camera brought down as a charge builds, and eased back up once it has gone off or been broken — through
    /// the engine's own height regulation. The dungeon camera pass decays the follow camera's height toward a REST
    /// every frame and holds it above a FLOOR (CodeCaves.CameraRestHeight / CameraMinHeight: the constants made data
    /// by DunPatches), so the mod only moves those two words — the engine eases the camera to them at its own rate,
    /// and back when they return to vanilla. Nothing is written to the camera itself, and nothing fights the pass.
    /// </summary>
    internal static class CameraDip
    {
        private const float  DipTo          = -5f;    // the rest height at full charge (vanilla 5.0: the camera goes below its look-at point)
        private const double ReleaseSeconds = 0.8;    // how long the words take to come back up (the camera follows at ≤0.5 a frame on its own)
        private const float  Step           = 0.05f;  // the smallest change worth a write
        private static float _rest = CodeCaves.CameraRestVanilla;   // the rest last written
        private static bool  _owned;                  // the owner flag is ours: the PNACH has stopped re-seeding
        private static float _held;                   // the rest held when the release began
        private static DateTime _releaseAt;           // when the release began (default = not releasing)

        /// <summary>The camera down by <paramref name="k"/> (0..1) of the dip: the charge's own level.</summary>
        internal static void Drive(float k)
        {
            _releaseAt = default;
            k = Math.Max(0f, Math.Min(1f, k));
            Apply(CodeCaves.CameraRestVanilla + (DipTo - CodeCaves.CameraRestVanilla) * k);
        }
        /// <summary>Let it come back up over <see cref="ReleaseSeconds"/>; <see cref="Tick"/> carries it.</summary>
        internal static void Release()
        {
            if (!_owned || _releaseAt != default) return;
            _held = _rest; _releaseAt = GameClock.Now;
        }
        /// <summary>The release, one step; nothing when the camera is where it belongs.</summary>
        internal static void Tick()
        {
            if (_releaseAt == default) return;
            double t = (GameClock.Now - _releaseAt).TotalSeconds / ReleaseSeconds;
            if (t >= 1.0) { Reset(); return; }
            float u = (float)(1.0 - Math.Pow(1.0 - t, 2));            // eases out: quick to start, settling at the top
            Apply(_held + (CodeCaves.CameraRestVanilla - _held) * u);
        }
        /// <summary>HOLD the camera at its world height now (its follow point's height plus its height field), written to
        /// <see cref="CodeCaves.CameraPin"/> with the flag raised, so the camera-pin cave keeps it there every frame while
        /// the follow point rises and falls with Toan — until <see cref="Unpin"/>. Distance and angle stay the engine's.</summary>
        internal static void Pin()
        {
            uint p = Memory.ReadGuestPtr(DungeonCamera.NowCamera);
            if (!Memory.IsValidGuest(p)) return;
            long cam = Memory.ToMmu(p);
            float ry = Memory.ReadFloat(cam + FollowCamera.RefY), h = Memory.ReadFloat(cam + FollowCamera.Height);
            Memory.WriteFloat(CodeCaves.CameraPin + 4, ry + h);
            Memory.WriteInt(CodeCaves.CameraPin + CodeCaves.CameraPinFlag, 1);
            _pinned = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[CameraDip] camera height pinned at {ry + h:F1} (follow point {ry:F1} + {h:F1})");
        }
        /// <summary>Let the camera follow again (its height then eases from where the pin left it).</summary>
        internal static void Unpin()
        {
            if (!_pinned) return;
            Memory.WriteInt(CodeCaves.CameraPin + CodeCaves.CameraPinFlag, 0);
            _pinned = false;
        }
        private static bool _pinned;

        /// <summary>Straight back to vanilla, and the words handed back to the PNACH — a floor left, the sword put away.</summary>
        internal static void Reset()
        {
            Unpin();
            _releaseAt = default;
            if (!_owned) return;
            Memory.WriteFloat(CodeCaves.CameraRestHeight, CodeCaves.CameraRestVanilla);
            Memory.WriteFloat(CodeCaves.CameraMinHeight, CodeCaves.CameraMinVanilla);
            Memory.WriteInt(CodeCaves.CameraHeightOwner, 0);
            _owned = false; _rest = CodeCaves.CameraRestVanilla;
        }

        /// <summary>The rest at <paramref name="rest"/>, the floor no higher than it (the floor is what would hold the
        /// camera up otherwise), the owner flag ours.</summary>
        private static void Apply(float rest)
        {
            if (_owned && Math.Abs(rest - _rest) < Step) return;
            if (!_owned) { Memory.WriteInt(CodeCaves.CameraHeightOwner, 1); _owned = true; }
            Memory.WriteFloat(CodeCaves.CameraMinHeight, Math.Min(CodeCaves.CameraMinVanilla, rest));
            Memory.WriteFloat(CodeCaves.CameraRestHeight, rest);
            _rest = rest;
        }
    }
}
