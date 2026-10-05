using System;
using System.Diagnostics;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A duration for enemy poison. The engine's poison is a 180-frame cycle that re-arms itself (slot +0x0C: tick, take a tenth
    /// of max HP, tick again) and never ends on its own; the species' status susceptibility (slot +0xDE, the ItemStatusRes copy:
    /// 0 immune, regulars 50–90) only decides whether a hit lands it. Here a poisoned enemy is cured after susceptibility × 2
    /// seconds — the more a species gives in to status hits, the longer the poison holds — counted from the tick the poison was
    /// first seen on the slot (half that under the "Harder Enemy AI" option); a hit that re-poisons an already poisoned enemy does
    /// not extend it. Freeze or death ending the
    /// poison early clears the count, so the next poisoning starts a fresh window. Mod-planted poison (EnemyStatus) is the same
    /// slot word and is timed the same way.
    /// </summary>
    internal static class EnemyPoisonDuration
    {
        private const string Tag = "[PoisonDuration] ";
        internal const float SecondsPerSusceptibility = 2f, HarderAiFactor = 0.5f;
        private static readonly Stopwatch[] since = new Stopwatch[EnemyAddresses.FloorSlots.Count];

        /// <summary>Once a dungeon tick.</summary>
        internal static void Tick()
        {
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                long a = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
                bool poisoned = Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadInt(a + EnemySlotOffsets.PoisonPeriod) > 0;
                if (!poisoned) { since[slot] = null; continue; }
                if (since[slot] == null) { since[slot] = Stopwatch.StartNew(); continue; }
                int susceptibility = Memory.ReadShort(a + EnemySlotOffsets.StatusSusceptibility);
                float limit = Math.Max(1, susceptibility) * SecondsPerSusceptibility * (HarderEnemyAI.Enabled ? HarderAiFactor : 1f);
                if (since[slot].Elapsed.TotalSeconds < limit) continue;
                Memory.WriteInt(a + EnemySlotOffsets.PoisonPeriod, 0);
                since[slot] = null;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot {slot} cured after {limit:0} s (susceptibility {susceptibility})");
            }
        }

        /// <summary>Forget every count (a new floor: the slots are new enemies).</summary>
        internal static void Reset() => Array.Clear(since, 0, since.Length);
    }
}
