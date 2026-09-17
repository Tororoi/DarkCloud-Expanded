using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A vanilla-layout dungeon address → where that data actually is this boot.
    ///
    /// GameInit carves every dungeon pool sequentially out of one 27 MB buffer (common, motion, chara, shotfx, texture,
    /// p870/SystemScript, p6a0/BtMes, …), so changing ANY pool's size moves every pool carved after it. The cat needed a
    /// bigger character heap — 210000 → 265000 units (DunPatches) — which moved everything after `chara` up by 880,000 B,
    /// and every mod address captured from a vanilla run kept pointing at where its data used to be. That is how the
    /// dungeon's message text ended up painted across the `gaiji` glyph sheet (see <see cref="DungeonMessageBank"/>, which
    /// resolves its own address from the engine's live pointer), and why the bone-door bypass and the Ungaga door fixes
    /// quietly stopped working: their value checks failed and they logged "couldn't fix" forever (user 2026-09-15).
    ///
    /// The shift is (live chara cap − 210000) × 16: the heap is the only pool the mod resizes and the carve is sequential.
    /// Callers must still VALIDATE what they find — check the engine's own value (the door type, the 150.0 door distance)
    /// before writing. A pool whose 64-unit alignment rounds differently can sit up to ~1 KB further along, and writing
    /// blind into a dungeon pool is exactly what struck out every letter in the game's text.
    /// </summary>
    internal static class DungeonPools
    {
        private const string Tag = "[DunPools] ";
        /// <summary>The heap an UNPATCHED dungeon carves: the layout every hardcoded address in the mod was captured from.</summary>
        internal const int VanillaCharaUnits = 210000;

        private static long _shift;
        private static int _capSeen;

        /// <summary>How far every pool carved after the character heap has moved, in bytes (0 on an unpatched disc, and the
        /// last known value while no dungeon is loaded).</summary>
        internal static long Shift()
        {
            int cap = Memory.ReadInt(DataPools.Chara + DataPools.Cap);
            if (cap <= 0 || cap > 0x100000) return _shift;              // no dungeon loaded — keep what we last measured
            if (cap != _capSeen)
            {
                _capSeen = cap;
                _shift = (long)(cap - VanillaCharaUnits) * 16;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag
                                  + $"character heap {cap:N0} units — dungeon pools sit {_shift:+#,0;-#,0;+0} B from vanilla");
            }
            return _shift;
        }

        /// <summary>A vanilla-layout dungeon address, moved to where its data is now.</summary>
        internal static long Resolve(long vanillaAddress) => vanillaAddress + Shift();
    }
}
