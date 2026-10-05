using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Blessing Gun — "Blessed Bait", a fishing passive: while a Blessing Gun is owned (any bag or storage), bait is only
    /// ever spent on a fight. The two ways a cast's bait is otherwise lost — the float sinking, a hooked fish getting off — are
    /// rolls in EdMoveChara that the ISO's bait-keep cave (SmoothRestCave.BaitKeep) fails while CodeCaves.BaitKeep is non-zero;
    /// this holds the word non-zero through every fishing session while the gun is owned, zero otherwise.</summary>
    internal static class BlessingGun
    {
        private const string Tag = "[BlessingGun] ";
        private static bool _keeping;                            // CodeCaves.BaitKeep is set

        /// <summary>Blessing Gun owned in any bag or storage.</summary>
        internal static bool OwnedAnywhere() => WeaponOwnership.Owned(Items.blessinggun);

        /// <summary>Fishing tick: the keep word follows ownership.</summary>
        internal static void FishingTick()
        {
            bool keep = OwnedAnywhere();
            if (keep == _keeping) return;
            _keeping = keep;
            Memory.WriteInt(CodeCaves.BaitKeep, keep ? 1 : 0);
            if (keep) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "Blessed Bait: bait is only spent on a fight this session");
        }

        /// <summary>Session end: the word back to zero.</summary>
        internal static void FishingReset()
        {
            if (!_keeping) return;
            _keeping = false;
            Memory.WriteInt(CodeCaves.BaitKeep, 0);
        }
    }
}
