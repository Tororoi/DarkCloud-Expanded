using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Bandit's Ring — Steal Shot, inherited from Xiao's Bandit Slingshot: a steal that lands on an enemy with a projectile
    /// takes the projectile, and Ruby's FULL charge fires it until the next steal or the floor's end. The steal, the victim, the notice
    /// and the shared driver are the slingshot's (<see cref="BanditSlingshot.Drive"/>); Ruby's own part is here: the stolen config is
    /// entered into the SECOND main-character instance (<see cref="StolenInstance"/>, the broken-weapon shot's, otherwise idle), which
    /// the second-effect caves step and draw beside her own while CodeCaves.SecondEffectLive is set (<see cref="KeepLive"/>). A full
    /// charge's release — the two orbs that appear together, the second marked by SetRandomRate(2.0) — is put out the tick it appears
    /// and the stolen shot fired in each orb's place, at that orb's damage, with her owner id (<see cref="ReplaceFullCharge"/>); her
    /// quick shots and a partial charge stay her own. <see cref="StealShotEffect"/> runs the driver while the ring is in her hand.</summary>
    internal static class BanditsRing
    {
        /// <summary>The instance the stolen config is entered into for Ruby: the second main-character instance.</summary>
        internal const long StolenInstance = ShotEffectPack.CharaMainEffectCrash;
        private const float FullChargeRate = 2f;            // SetRandomRate's mark on a full charge's second orb (every other shot carries Set's −1)
        private const int   HeldBall = 1;                   // SetLoop(1): the charge ball she holds (+0xA0B0), never replaced
        private const int   RubyShotLife = 120;             // frames of flight for her stolen shot (a pellet's)
        private static readonly bool[] _rubySeen = new bool[ShotEffectPack.SubShots];
        private static bool _live;                          // this ring set CodeCaves.SecondEffectLive

        /// <summary>A new steal: every sub-shot is new again.</summary>
        internal static void ResetSeen() => Array.Clear(_rubySeen, 0, _rubySeen.Length);

        /// <summary>Ruby: the orbs a full charge releases, the tick they appear in her live instance — each replaced by the stolen shot
        /// <paramref name="effect"/> from the second instance at its position and direction, at its damage, with her owner id, and put
        /// out. A tick's new orbs are a full charge's when one of them carries the 2.0 mark; a quick shot or a partial charge is left.</summary>
        internal static void ReplaceFullCharge(BorrowedEffect effect)
        {
            long inst = ShotEffectPack.CharaMainEffect;
            if (Memory.ReadUInt(ShotEffectPack.MainEffectLivePtr) != inst - 0x20000000L) { ReleaseLive(); return; }   // a broken weapon fires from the second instance itself
            KeepLive();
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return;
            var fresh = new System.Collections.Generic.List<int>();
            bool full = false;
            for (int i = 0; i < count; i++)
            {
                bool live = Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) != 0;
                if (live && !_rubySeen[i])
                {
                    _rubySeen[i] = true;
                    if (Memory.ReadInt(inst + ShotEffectPack.OffA0B0 + i * 4) == HeldBall) continue;          // the ball she is charging
                    fresh.Add(i);
                    if (Memory.ReadFloat(inst + ShotEffectPack.OffA0D0 + i * 4) == FullChargeRate) full = true;
                }
                else if (!live) _rubySeen[i] = false;
            }
            if (!full) return;
            foreach (int i in fresh)
            {
                long obj = inst + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride, dir = inst + ShotEffectPack.OffDir + i * 0x10;
                float x = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), h = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), y = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                float vx = Memory.ReadFloat(dir), vh = Memory.ReadFloat(dir + 4), vy = Memory.ReadFloat(dir + 8);
                int damage = Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4);
                if (BorrowedShots.Fire(effect, x, h, y, vx, vh, vy, damage, RubyShotLife, Player.RubyId))
                    Memory.WriteUShort(inst + ShotEffectPack.OffActive + i * 2, 0);   // her orb gives way
            }
        }

        /// <summary>The second instance stepped and drawn while she holds a stolen shot (re-asserted: another ability may clear it).</summary>
        private static void KeepLive()
        {
            if (Memory.ReadInt(CodeCaves.SecondEffectLive) != 1) Memory.WriteInt(CodeCaves.SecondEffectLive, 1);
            _live = true;
        }

        /// <summary>Hand the second instance back to the engine when this ring set it live.</summary>
        internal static void ReleaseLive()
        {
            if (!_live) return;
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _live = false;
        }

        /// <summary>The Steal Shot thread for the ring: the slingshot's driver every tick while the ring is in Ruby's hand, stood down
        /// once when it goes.</summary>
        public static void StealShotEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.banditsring)
            {
                BanditSlingshot.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            BanditSlingshot.Stop();
            ReleaseLive();
        }
    }
}
