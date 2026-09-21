using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A charge the character has no motion for, shown on the character: the active character's own ambient tint
    /// (CCharacter +0xCE0, the add Draw__10CCharacter lays over the scene ambient) ramps from nothing to half the game's
    /// charge-complete colour (<see cref="CharacterFlash.ChargeR"/>…) over the last <see cref="WindowSeconds"/> before the
    /// next charge level, and drops to nothing the tick that level's flash fires (the flash — the engine's global ambient
    /// pulse, the same colour, a separate term — is the spike, on its own). One level: the ramp, the flash, then nothing.
    /// Several: the same ramp again before each. Abilities call <see cref="Ramp"/> every tick while the shot is held with
    /// the time left to the next level, and <see cref="Clear"/> when it is not held.
    /// </summary>
    internal static class ChargeTint
    {
        private const float  Peak = 0.5f;            // of the charge-complete colour, just before the flash
        private const double WindowSeconds = 0.5;    // the ramp runs over the last half second before a level, however long the hold
        private static bool  _on;
        private static float _last = -1f;

        /// <summary>Seconds left to the next charge level (≤ 0 = it has fired; anything past the window or none to come = nothing shown).</summary>
        internal static void Ramp(double secondsToLevel)
        {
            float k = secondsToLevel <= 0 || double.IsInfinity(secondsToLevel) ? 0f : (float)(Math.Clamp(1.0 - secondsToLevel / WindowSeconds, 0.0, 1.0) * Peak);
            if (_on && Math.Abs(k - _last) < 0.002f) return;
            Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, CharacterFlash.ChargeR * k, CharacterFlash.ChargeG * k, CharacterFlash.ChargeB * k);
            _on = true; _last = k;
        }

        /// <summary>No charge held: the tint back to nothing (one write, on the transition).</summary>
        internal static void Clear()
        {
            if (!_on) return;
            Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, 0f, 0f, 0f);
            _on = false; _last = -1f;
        }
    }
}
