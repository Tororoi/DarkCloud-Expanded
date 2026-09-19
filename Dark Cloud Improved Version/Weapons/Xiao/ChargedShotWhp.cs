using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Xiao's charged shots cost weapon HP the way Ruby's charge does. A ranged character's WHP drains at the SHOT —
    /// BattleActionPlay_Jinn (dun 0x1DBC930) calls SwordDmgCheck1(1.0) for each pellet, Ruby's routine passes 0.8 / 1.2 / 1.8
    /// for a normal / partial / full charge, Osmond's 0.5 — while the melee characters drain per landed hit inside CheckDmg
    /// (owners 0/2/4 only). BattleSubWeaponDmg (0x1B5D90): WHP −= (1.5 − 0.01 × Endurance) × factor, Durable × 0.5,
    /// Fragile × 2, the broken base weapon exempt, with the 10 % / 5 % warnings and the break / Repair Powder at 0.
    ///
    /// The ISO's dun.bin patch (DunPatches) makes Xiao's routine pass <see cref="Mailbox.XiaoShotWhpFactor"/> instead of
    /// its 1.0, so the whole cost goes through the game's own drain: an ability holding a charge calls <see cref="Arm"/>
    /// every tick with the factor its shot will be charged at — <see cref="ChargedFactor"/> for any charged shot, 1.0
    /// otherwise — and <see cref="Tick"/> puts the word back to 1.0 once the pellet
    /// has left (the shoot motion fires it ~half a second after the release). An ISO without the patch charges every shot
    /// its vanilla 1.0 (warned once).
    /// </summary>
    internal static class ChargedShotWhp
    {
        private const string Tag = "[ChargedShotWhp] ";
        internal const float ChargedFactor = 2.0f;   // a charged shot's WHP, against the ordinary shot's 1.0
        private const double ArmSeconds    = 1.5;    // a factor armed but never fired (a cancelled charge) is dropped after this

        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static float    _written = -1f;      // the factor last written (−1 = never: seed on the first tick)
        private static DateTime _armedAt = DateTime.MinValue;
        private static bool     _nativeWarned;

        private static bool Native => (uint)Memory.ReadInt(DunPatches.XiaoShotWhpPatchAddrMmu) == DunPatches.XiaoShotWhpPatchedWord0;

        /// <summary>The factor the NEXT shot fires at — call every tick while the shot is held (1.0 until the charge is
        /// reached). Stays armed through the release until the pellet leaves.</summary>
        internal static void Arm(float factor)
        {
            Write(factor);
            _armedAt = GameClock.Now;
        }

        /// <summary>Every tick on a weapon with a charged shot: back to 1.0 once the armed shot's pellet has left (or the
        /// charge was cancelled).</summary>
        internal static void Tick()
        {
            if (_written < 0f) { Memory.WriteInt(CodeCaves.Mailbox.XiaoShotWhpOwner, 1); Write(1f); }
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            bool fired = false;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i]) { _seen[i] = true; fired = true; }
                else if (!live) _seen[i] = false;
            }
            if (_written <= 1f) return;
            if (fired)
            {
                if (Native) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot fired at WHP factor ×{_written:F2}");
                else if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the shot-WHP patch is not in this ISO — charged shots cost their vanilla WHP (re-patch the ISO)"); }
                Write(1f);
            }
            else if ((GameClock.Now - _armedAt).TotalSeconds > ArmSeconds) Write(1f);   // cancelled: nothing came
        }

        private static void Write(float factor)
        {
            if (factor == _written) return;
            Memory.WriteFloat(CodeCaves.Mailbox.XiaoShotWhpFactor, factor);
            _written = factor;
        }
    }
}
