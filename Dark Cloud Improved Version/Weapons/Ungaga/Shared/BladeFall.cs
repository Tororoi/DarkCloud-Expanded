namespace Dark_Cloud_Improved_Version
{
    /// <summary>The blade-fall cave (CodeCaves.BladeFall, Big Bang's) driving a copy's height on the engine's frames: each frame
    /// y −= vy, vy += g, landed at y ≤ stop.</summary>
    internal static class BladeFall
    {
        /// <summary>An eased rise from <paramref name="y0"/> to <paramref name="y1"/> over <paramref name="frames"/> engine frames:
        /// an upward velocity v0 = 2D/T and a deceleration g = v0/T reach zero together at the top — fast off the mark, slow to
        /// settle. The stop test is disarmed with a stop far below; the caller takes the integrator off when the time is up.</summary>
        internal static void StartEase(float y0, float y1, float frames)
        {
            float d = y1 - y0, v0 = 2f * d / frames, g = v0 / frames;
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, y0);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, -v0);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, g);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, -1e9f);
            Memory.WriteInt  (CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFalling);
        }
    }
}
