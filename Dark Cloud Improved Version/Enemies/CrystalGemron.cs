namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Crystal Gemron (EnemySpecies.CrystalGemron) in play. The species is the disc's: its record (SpeciesRows), model (the
    /// breaking crystal balls, its eyes a node of their own), script and name (CrystalGemronBake). In play this only gives the eyes'
    /// visual its private vtable, so the eyes draw tinted (<see cref="Eyes"/>: tools/stubs/eye_tint.s adds the tint to the ambient for that
    /// one draw).</summary>
    internal static class CrystalGemron
    {
        private static readonly NodeDrawHook Eyes = new("the Crystal Gemron's eyes", "eye_tint", DeadChainCave.EyeTint,
            CodeCaves.EyeTintStock, CodeCaves.EyeTintVtable, CodeCaves.EyeTintVtableGuest);

        /// <summary>Once a dungeon tick; <paramref name="active"/> false while a load is on (the floor's units and pools are rebuilt).</summary>
        internal static void Tick(bool active)
        {
            if (!active) return;
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++)
                if (IsCrystalGemron(unit)) { Eyes.Arm(unit, CrystalGemronBake.EyeNode); return; }   // the species' one eyes visual
        }

        /// <summary>A new floor: its visual is another object.</summary>
        internal static void Reset() => Eyes.Reset();

        private static bool IsCrystalGemron(int unit)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(unit, 0);
            return Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) == EnemySpecies.CrystalGemron.Id;
        }
    }
}
