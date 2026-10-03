using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Confuse weapon ability (docs/confuse-ability.md) on the mod's side: the ISO's roll (tools/stubs/confuse_proc.s, inside
    /// CheckDmg beside Poison and Stop: 5 %, the monster's status susceptibility) raises a byte per enemy in
    /// CodeCaves.ConfuseProc; this loop confuses each such enemy for <see cref="ProcSeconds"/> (Confusion: it goes after the
    /// nearest enemy, or the player when the player is nearest; whoever it hits turns on it until its confusion ends, then
    /// on its next attacker still confused, else back to the player) and clears the byte. It also ticks Confusion whenever any
    /// enemy is confused (a weapon that confuses — Babel's Spear, the Terra nut — ticks it too; the passes are rate-limited) and
    /// drives the stars over the confused (ConfusionStars, in the resident stars instance).
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
                            if (Confusion.AnyConfused() || Confusion.OwnsTable) Confusion.Tick();
                            ConfusionStars.Drive(Confusion.IsConfused);
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                Thread.Sleep(TickMs);
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
