using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A vanilla-layout dungeon address → where that data actually is this boot.
    ///
    /// GameInit carves every dungeon pool sequentially out of one 27 MB buffer (common, motion, chara, shotfx, texture,
    /// p870/SystemScript, p6a0/BtMes, …), so changing ANY pool's size moves every pool carved after it. DunPatches grows the
    /// character heap (210000 → 273400 units) and takes 8,400 of those units back from p870 (35000 → 26600), so the pools from
    /// shotfx through p870 sit (heap − 210000) × 16 B further on and every pool after p870 sits (heap − 210000 − (35000 − p870))
    /// × 16 B on. A mod address captured from a vanilla run points at where its data is no longer: the dungeon's message text then
    /// paints across the `gaiji` glyph sheet (see <see cref="DungeonMessageBank"/>, which resolves its own address from the
    /// engine's live pointer), and the bone-door bypass and the Ungaga door fixes (event script values in p870, markers in the
    /// pools after it) fail their value checks and log "couldn't fix" forever.
    ///
    /// The carve aligns each pool to 64 bytes and every size involved is a multiple of 64, so the shifts are exact. Callers must
    /// still VALIDATE what they find — check the engine's own value (the door type, the 150.0 door distance) before writing:
    /// writing blind into a dungeon pool is exactly what struck out every letter in the game's text.
    /// </summary>
    internal static class DungeonPools
    {
        private const string Tag = "[DunPools] ";
        /// <summary>The heap and the floor system script's pool an UNPATCHED dungeon carves: the layout every hardcoded address in the
        /// mod was captured from.</summary>
        internal const int VanillaCharaUnits = 210000, VanillaScriptUnits = 35000;
        /// <summary>Vanilla bounds: shotfx's base (the first pool after the heap; nothing before it moves) and p870's end (the last pool the
        /// heap's growth moves on its own).</summary>
        private const long VanillaShotFxBase = 0x2075E340, VanillaScriptEnd = 0x20998840;

        private static long _heapShift, _afterShift;
        private static (int chara, int script) _capsSeen;

        /// <summary>How far the pools from shotfx through p870 have moved, and how far the pools after p870 have, in bytes (0 on an
        /// unpatched disc, and the last known values while no dungeon is loaded).</summary>
        private static (long heap, long after) Shifts()
        {
            int cap = Memory.ReadInt(DataPools.Chara + DataPools.Cap), script = Memory.ReadInt(DataPools.P870 + DataPools.Cap);
            if (cap <= 0 || cap > 0x100000 || script <= 0 || script > 0x100000) return (_heapShift, _afterShift);   // no dungeon loaded
            if ((cap, script) != _capsSeen)
            {
                _capsSeen = (cap, script);
                _heapShift = (long)(cap - VanillaCharaUnits) * 16;
                _afterShift = _heapShift - (long)(VanillaScriptUnits - script) * 16;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag
                                  + $"character heap {cap:N0} units, system script pool {script:N0} — shotfx..p870 sit {_heapShift:+#,0;-#,0;+0} B "
                                  + $"from vanilla, the pools after them {_afterShift:+#,0;-#,0;+0} B");
            }
            return (_heapShift, _afterShift);
        }

        /// <summary>A vanilla-layout dungeon address, moved to where its data is now.</summary>
        internal static long Resolve(long vanillaAddress)
        {
            if (vanillaAddress < VanillaShotFxBase) return vanillaAddress;
            var (heap, after) = Shifts();
            return vanillaAddress + (vanillaAddress < VanillaScriptEnd ? heap : after);
        }
    }
}
