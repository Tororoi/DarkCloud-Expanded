namespace Dark_Cloud_Improved_Version
{
    /// <summary>The engine-frame vertical drive of a prop copy's height — the cave at CodeCaves.VerticalDrive (named for the judgement
    /// blade, the first thing it moved): each frame y −= vy, vy += g, stopped at y ≤ stop. Babel's Spear and the Cactus rise
    /// through it; the judgement blade falls through it.</summary>
    internal static class VerticalDrive
    {
        /// <summary>An eased rise from <paramref name="y0"/> to <paramref name="y1"/> over <paramref name="frames"/> engine frames:
        /// an upward velocity v0 = 2D/T and a deceleration g = v0/T reach zero together at the top — fast off the mark, slow to
        /// settle. The stop test is disarmed with a stop far below; the caller takes the integrator off when the time is up.</summary>
        internal static void StartEase(float y0, float y1, float frames)
        {
            float d = y1 - y0, v0 = 2f * d / frames, g = v0 / frames;
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY, y0);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveVy, -v0);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveG, g);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveStop, -1e9f);
            Memory.WriteInt  (CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.DriveFalling);
        }
    }
}
