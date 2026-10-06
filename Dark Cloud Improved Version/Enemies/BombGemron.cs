using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb Gemron (EnemySpecies.BombGemron) in play. The species itself is the disc's: its record (ElfSpeciesPatches),
    /// model, script and name (ModSpeciesBakes). Its script explodes — a <c>_SET_SHOT2</c> blast at the body — once the death motion
    /// (11) reaches frame 122, both when it dies and when it self-destructs; this draws the Big Bang blast and shock ring at that
    /// moment, once per death, so the explosion reads as the bomb's.</summary>
    internal static class BombGemron
    {
        private const string Tag = "[BombGemron] ";
        private const int DeathMotion = 11;
        private const float BlastFrame = 122f, BlastScale = 3f, BlastLift = 11f;      // the script's frame and the blast's height over the root
        private static readonly bool[] _blown = new bool[EnemyAddresses.FloorSlots.Count];

        /// <summary>Once a dungeon tick.</summary>
        internal static void Tick()
        {
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                long a = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
                if (Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) <= 0 || Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) != EnemySpecies.BombGemron.Id)
                { _blown[slot] = false; continue; }
                long model = ModelScaleOffsets.ModelBase + (long)slot * ModelScaleOffsets.ModelStride;
                if (Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId) != DeathMotion) { _blown[slot] = false; continue; }
                if (_blown[slot] || Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame) < BlastFrame) continue;
                _blown[slot] = true;
                float x = Memory.ReadFloat(a + EnemySlotOffsets.LocationX), h = Memory.ReadFloat(a + EnemySlotOffsets.LocationZ), y = Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
                bool ok = BombFx.Spawn(x, h + BlastLift, y, BlastScale);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot {slot} blew up at ({x:0},{h:0},{y:0}){(ok ? "" : " — no free bomb effect slot")}");
            }
        }

        /// <summary>A new floor: the slots are new enemies.</summary>
        internal static void Reset() => Array.Clear(_blown, 0, _blown.Length);
    }
}
