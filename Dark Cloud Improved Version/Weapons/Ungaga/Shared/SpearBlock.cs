namespace Dark_Cloud_Improved_Version
{
    /// <summary>The spear-block caves' one solid column (CodeCaves.SpearBlock): a standing copy made solid to enemies (the enemy
    /// move cave), the player (the player move cave) and enemy shots (the shot cave, which stop below its top). Armed with its
    /// place and size, the flag written last; disarmed by the flag alone.</summary>
    internal static class SpearBlock
    {
        /// <summary>The column at (<paramref name="x"/>, <paramref name="y"/>) across the ground, its base on the floor at
        /// <paramref name="floor"/>, <paramref name="radius"/> wide, up to <paramref name="top"/>.</summary>
        internal static void Arm(float x, float floor, float y, float radius, float top)
        {
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockX, x);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockH, floor);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockY, y);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockR, radius);
            Memory.WriteFloat(CodeCaves.SpearBlock + CodeCaves.SpearBlockTop, top);
            Memory.WriteInt  (CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 1);
        }

        /// <summary>Passable again.</summary>
        internal static void Disarm() => Memory.WriteInt(CodeCaves.SpearBlock + CodeCaves.SpearBlockFlag, 0);
    }
}
