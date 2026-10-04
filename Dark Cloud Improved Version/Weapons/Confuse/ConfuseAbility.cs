using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Confuse weapon ability's loop (docs/confuse-ability.md), from app start, each tick on a floor: every enemy the ISO's roll
    /// (tools/stubs/confuse_proc.s) flagged in CodeCaves.ConfuseProc is confused for <see cref="ProcSeconds"/> and its byte
    /// cleared; Confusion is ticked whenever any enemy is confused (or it owns the aggro table); the stars over the confused are
    /// driven (ConfusionStars, in the resident stars instance).
    /// </summary>
    internal static class ConfuseAbility
    {
        private const string Tag = "[Confuse] ";
        internal const double ProcSeconds = 20.0;
        private const int    TickMs = 16;
        private static Thread _thread;

        /// <summary>The loop, from app start (it does nothing off a dungeon floor).</summary>
        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ConfuseAbility" };
            _thread.Start();
        }

        private static void Loop()
        {
            bool wasIn = false;
            while (true)
            {
                try
                {
                    bool inFloor = Player.InDungeonFloor();
                    if (!inFloor)
                    {
                        if (wasIn) { Memory.WriteBytesBatch(CodeCaves.ConfuseProc, new byte[16]); }
                        wasIn = false;
                    }
                    else
                    {
                        wasIn = true;
                        if (!Player.CheckDunIsPausedOrMenu())
                        {
                            Procs();
                            if (DebugDiagnostics.Enabled) Diagnose();
                            if (Confusion.AnyConfused() || Confusion.OwnsTable) Confusion.Tick();
                            ConfusionStars.Drive(Confusion.IsConfused);
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                Thread.Sleep(TickMs);
            }
        }

        // DIAGNOSTIC (behind DebugDiagnostics): what the equipped weapon's battle copy and each enemy's last hit carry (the roll reads the latter).
        private static int _wepWord = -1;
        private static readonly int[] _hitWord = new int[16];
        private static void Diagnose()
        {
            int w = Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.AbilityFlagsOffset);
            if (w != _wepWord)
            {
                _wepWord = w;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"battle weapon {Memory.ReadUShort(WeaponHave.BattleWeaponRecord)}: ability word 0x{w:X4} (Confuse {((w & 0x4000) != 0 ? "ON" : "off")})");
            }
            for (int s = 0; s < 16; s++)
            {
                int h = Memory.ReadInt(AbsRewards.SlotStatusFlagsAddr(s));
                if (h == _hitWord[s]) continue;
                _hitWord[s] = h;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot {s} hit carries 0x{h:X} (Confuse {((h & 0x4000) != 0 ? "ON" : "off")}){(Enemies.IsBoss(s) ? ", a boss (never confused)" : "")}");
            }
        }

        /// <summary>Each enemy the ISO's roll confused since the last tick: confused for ProcSeconds (a fresh proc on a confused
        /// enemy restarts its time), its byte cleared.</summary>
        private static void Procs()
        {
            byte[] p = Memory.ReadBytesBatch(CodeCaves.ConfuseProc, 16);
            if (p == null) return;
            for (int s = 0; s < 16; s++)
            {
                if (p[s] == 0) continue;
                Memory.WriteByte(CodeCaves.ConfuseProc + s, 0);
                if (!Enemies.IsLive(s)) continue;
                Confusion.Confuse(s, GameClock.Now.AddSeconds(ProcSeconds));
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {s} confused by a hit ({ProcSeconds:F0} s)");
            }
        }
    }
}
