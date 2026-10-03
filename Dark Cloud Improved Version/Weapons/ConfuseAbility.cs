using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Confuse weapon ability (docs/confuse-ability.md) on the mod's side: the ISO's roll (tools/stubs/confuse_proc.s, inside
    /// CheckDmg beside Poison and Stop: a flat 5 % on any enemy but a boss, as Critical's 1 %) raises a byte per enemy in
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
                            Diagnose();
                            if (Confusion.AnyConfused() || Confusion.OwnsTable) Confusion.Tick();
                            ConfusionStars.Drive(Confusion.IsConfused);
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                Thread.Sleep(TickMs);
            }
        }

        // TEMP diagnostic: what the equipped weapon's battle copy and each enemy's last hit carry (the roll reads the latter).
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
