using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Mirage line's lock-on reach: the wielder's entry in the lock-on factor table (the same data the Cross Hinder and
    /// the Flamingo drive) ×<see cref="ReachFactor"/> while a weapon of the line is out — the Mirage and what is built up from it:
    /// the Terra Sword, Hercules' Wrath, Babel's Spear — Ungaga's own or Super Steve's sphere. Driven from Mirage's loop, which
    /// runs on every floor: <see cref="Hold"/> while <see cref="Wielded"/>, else <see cref="Release"/>. Needs the ISO's lock-on
    /// table patch (<see cref="DunPatches.LockOnTableWord0"/>). docs/mirage.md.</summary>
    internal static class MirageLineReach
    {
        private const string Tag = "[MirageLineReach] ";
        private const float ReachFactor = 2.0f;
        private static int  _reachChar = -1;              // whose lock-on entry the reach was raised on (−1 = none)
        private static readonly int[] Line = { Items.mirage, Items.terrasword, Items.herculeswrath, Items.babelsspear };

        /// <summary>A weapon of the line is out: Ungaga's own, or Super Steve with one of their spheres.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Line);

        /// <summary>The active character's lock-on reach ×ReachFactor. A character switch hands the raised entry back first.</summary>
        internal static void Hold()
        {
            if ((uint)Memory.ReadInt(DunPatches.LockOnTableHookAddrMmu) != DunPatches.LockOnTableWord0) return;   // table patch not in this ISO
            int ch = Player.CurrentCharacterNum();
            if (ch < 0 || ch >= CodeCaves.LockOnFactorVanilla.Length) return;
            if (_reachChar >= 0 && _reachChar != ch) Release();
            long entry = CodeCaves.LockOnFactorTable + ch * 4;
            float reach = CodeCaves.LockOnFactorVanilla[ch] * ReachFactor;
            if (Memory.ReadFloat(entry) == reach) return;
            Memory.WriteInt(CodeCaves.LockOnFactorTable + CodeCaves.LockOnFactorOwner, 1);   // ours: the PNACH stops re-seeding
            Memory.WriteFloat(entry, reach);
            if (_reachChar != ch) { _reachChar = ch; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lock-on reach ×{ReachFactor:F1} (character {ch})"); }
        }

        /// <summary>The raised entry back to vanilla (if it still holds our value).</summary>
        internal static void Release()
        {
            if (_reachChar < 0) return;
            long entry = CodeCaves.LockOnFactorTable + _reachChar * 4;
            float reach = CodeCaves.LockOnFactorVanilla[_reachChar] * ReachFactor;
            if (Memory.ReadFloat(entry) == reach) Memory.WriteFloat(entry, CodeCaves.LockOnFactorVanilla[_reachChar]);
            _reachChar = -1;
        }
    }
}
