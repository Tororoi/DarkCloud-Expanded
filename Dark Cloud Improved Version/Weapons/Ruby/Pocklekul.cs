using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Pocklekul — a guard held for a second summons a random day-of-the-week pockle (<see cref="PockleModel"/>); guarding again
    /// summons nothing while one stands. The hold is counted in floor frames (<see cref="HealAbility.FramesBetween"/>, the floor's own
    /// clock: it stands still with a pause) while Ruby holds her guard pose — R1 down, motion 9 or 33 (<see cref="GuardWatch.HoldPose"/>:
    /// a held guard is mostly 33, back to 9 a frame now and then).
    ///
    /// PHASE 0 (measurement build): the pockle only stands idle beside Ruby and leaves after <see cref="TestLifeFrames"/> floor frames;
    /// the logs give each day's size in the block, Ruby's character pool beside it and what a menu does to its textures.</summary>
    internal static class Pocklekul
    {
        private const string Tag = "[Pocklekul] ";
        private const int   ChargeFrames  = 60;       // a second of guard loop
        private const int   TestLifeFrames = 600;     // PHASE 0: how long a pockle stands before it goes
        private const float SpawnAhead    = 15f;      // where it appears: this far in front of Ruby, facing her
        private const long  DriveStepHold = 0x202A3564, FrameCapture = 0x202A3568;   // set at a menu's opening (StarsLane's)
        private static readonly Random _rng = new Random();

        /// <summary>The Pocklekul's thread: runs while the ring is in Ruby's hand on a dungeon floor.</summary>
        public static void PockleSummonEffect()
        {
            int prev = Memory.ReadInt(HealAbility.TickCounter), held = 0, life = 0;
            bool latched = false, pending = false;
            while (Player.InDungeonFloor() && Player.CurrentCharacterNum() == Player.RubyId
                   && Player.Weapon.GetCurrentWeaponId() == Items.pocklekul)
            {
                int now = Memory.ReadInt(HealAbility.TickCounter), frames = HealAbility.FramesBetween(prev, now);
                prev = now;
                bool quiet = Quiet();
                bool guard = GuardWatch.HoldPose().inPose;
                if (!guard) { held = 0; latched = false; }
                else if (!latched && (held += frames) >= ChargeFrames)
                {
                    latched = true;
                    if (!PockleModel.Up) pending = true;
                    else Log("a pockle already stands — no summon");
                }
                if (pending && quiet) { pending = false; Summon(); life = 0; }

                PockleModel.Tick(quiet);
                if (PockleModel.Up && (life += frames) >= TestLifeFrames) PockleModel.Despawn("its test time is up");
                Thread.Sleep(16);
            }
            PockleModel.Despawn("the Pocklekul left Ruby's hand");
        }

        /// <summary>A random day's pockle loaded and stood in front of Ruby, facing her.</summary>
        private static void Summon()
        {
            int day = _rng.Next(PockleModel.Days.Length);
            DateTime t0 = DateTime.UtcNow;
            if (!PockleModel.Load(day)) return;
            float x = Memory.ReadFloat(Addresses.dunPositionX), h = Memory.ReadFloat(Addresses.dunPositionZ), y = Memory.ReadFloat(Addresses.dunPositionY);
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            PockleModel.Show(x + SpawnAhead * MathF.Sin(yaw), h, y + SpawnAhead * MathF.Cos(yaw), yaw + MathF.PI);
            Log($"summoned in {(DateTime.UtcNow - t0).TotalMilliseconds:0} ms");
        }

        /// <summary>The floor walking, unpaused, no menu open or opening: the read buffer is the loader's and a native call is made.</summary>
        private static bool Quiet()
            => Player.CheckDunIsWalkingMode() && !Player.CheckDunIsPausedOrMenu()
               && Memory.ReadInt(DriveStepHold) == 0 && Memory.ReadInt(FrameCapture) == 0;

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
