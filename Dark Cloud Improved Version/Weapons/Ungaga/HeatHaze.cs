using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Mirage clone's heat shimmer — the game's one framebuffer distortion (CFireOmni::DrawRaster, the haze over a
    /// fire), drawn at the clone by ElfCave.MirageHazeDraw every frame.
    ///
    /// The cave hooks the dungeon draw loop's raster pass and, while <see cref="Mailbox.MirageHazeOn"/> is set, draws
    /// one raster at the clone's root CFrame (<see cref="Mailbox.MirageHazeNode"/>, its posed world translation as of
    /// that frame) lifted by <see cref="Mailbox.MirageHazeLift"/>. Nothing crosses PINE per frame; the mod only names
    /// the root and ramps the strength, a multiplicative gain in RODATA (<see cref="FireRaster.DistortionGain"/>).
    /// A dungeon with no fire pack has no distortion mask and the cave draws nothing — not an error.
    /// </summary>
    internal static class HeatHaze
    {
        private const string Tag = "[HeatHaze] ";
        private static bool  _on;
        private static float _gainOrig;      // the vanilla distortion gain (~1.3), captured once
        private static bool  _nativeWarned;

        internal static bool IsShowing => _on;

        /// <summary>The ISO carries the raster hook: the dungeon draw loop calls the haze cave.</summary>
        private static bool Native => (uint)Memory.ReadInt(DunPatches.MirageHazeHookAddrMmu) == DunPatches.MirageHazeHookNew;

        /// <summary>Pin the shimmer to <paramref name="rootGuest"/> (the clone's root CFrame), <paramref name="lift"/> units
        /// up, at strength <paramref name="gain01"/> (0..1). Call every tick while it should show — only the gain changes.
        /// Returns false with nothing drawn when the ISO lacks the cave or the clone is down.</summary>
        internal static bool Show(uint rootGuest, float lift, float gain01)
        {
            if (rootGuest == 0) return false;
            if (!Native)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "raster hook not in this ISO — no shimmer (re-patch the ISO)"); }
                return false;
            }
            if (_gainOrig <= 0f)                                 // capture the vanilla gain once, never a value we wrote
            {
                float g = Memory.ReadFloat(FireRaster.DistortionGain);
                _gainOrig = g > 0f && g < 100f ? g : 1.3f;       // 1.3 is the shipped value
            }
            Memory.WriteFloat(FireRaster.DistortionGain, Math.Clamp(gain01, 0f, 1f) * _gainOrig);
            if (!_on)
            {
                Memory.WriteUInt (CodeCaves.Mailbox.MirageHazeNode, rootGuest);
                Memory.WriteFloat(CodeCaves.Mailbox.MirageHazeLift, lift);
                Memory.WriteInt  (CodeCaves.Mailbox.MirageHazeOn, 1);   // on LAST: the cave reads the node once this is set
                _on = true;
            }
            return true;
        }

        /// <summary>Stop drawing and restore the vanilla gain. Safe to call when not showing.</summary>
        internal static void Hide()
        {
            if (_on) { Memory.WriteInt(CodeCaves.Mailbox.MirageHazeOn, 0); _on = false; }
            if (_gainOrig > 0f) Memory.WriteFloat(FireRaster.DistortionGain, _gainOrig);
        }
    }
}
