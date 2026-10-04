using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Bandit's Ring — Steal Shot, inherited from Xiao's Bandit Slingshot (docs/bandit-slingshot.md): a steal that lands on
    /// an enemy with a projectile takes the projectile, and every QUICK shot Ruby fires until the next steal or the floor's end is
    /// that enemy's. The steal, the victim, the notice and the shared driver are the slingshot's (<see cref="BanditSlingshot.Drive"/>);
    /// Ruby's own part is here: the stolen config is entered into the SECOND main-character instance (<see cref="StolenInstance"/>,
    /// the broken-weapon shot's, otherwise idle) beside her own, and each quick shot — a sub-shot of the live instance whose
    /// random-rate word is not the charged release's 2.0 — is put out the tick it appears and the stolen shot fired from the second
    /// instance in its place, with her owner id (<see cref="ReplaceQuickShots"/>); the charged shot stays her own.
    /// <see cref="StealShotEffect"/> runs the driver while the ring is in her hand.</summary>
    internal static class BanditsRing
    {
        /// <summary>The instance the stolen config is entered into for Ruby: the second main-character instance.</summary>
        internal const long StolenInstance = ShotEffectPack.CharaMainEffectCrash;
        private const float ChargedRate = 2f;               // SetRandomRate's mark on Ruby's charged release (a quick shot carries Set's −1)
        private const int   RubyShotLife = 120;             // frames of flight for her stolen shot (a pellet's)
        private static readonly bool[] _rubySeen = new bool[ShotEffectPack.SubShots];

        /// <summary>A new steal: every sub-shot is new again.</summary>
        internal static void ResetSeen() => Array.Clear(_rubySeen, 0, _rubySeen.Length);

        /// <summary>Ruby: each quick shot the tick it appears in her live instance — the stolen shot <paramref name="effect"/> from the
        /// second instance at its position and direction, <see cref="BanditSlingshot.DamageMult"/>× its attack, her owner id — and the
        /// quick shot put out. A charged release (random rate 2.0) is left as it is.</summary>
        internal static void ReplaceQuickShots(BorrowedEffect effect)
        {
            long inst = ShotEffectPack.CharaMainEffect;
            if (Memory.ReadUInt(ShotEffectPack.MainEffectLivePtr) != inst - 0x20000000L) return;   // a broken weapon fires from the second instance: ours already
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return;
            for (int i = 0; i < count; i++)
            {
                bool live = Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) != 0;
                if (live && !_rubySeen[i])
                {
                    _rubySeen[i] = true;
                    if (Memory.ReadFloat(inst + ShotEffectPack.OffA0D0 + i * 4) == ChargedRate) continue;   // her charged shot, her own
                    long obj = inst + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride, dir = inst + ShotEffectPack.OffDir + i * 0x10;
                    float x = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), h = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), y = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                    float vx = Memory.ReadFloat(dir), vh = Memory.ReadFloat(dir + 4), vy = Memory.ReadFloat(dir + 8);
                    int damage = (int)(Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4) * BanditSlingshot.DamageMult);
                    if (BorrowedShots.Fire(effect, x, h, y, vx, vh, vy, damage, RubyShotLife, Player.RubyId))
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + i * 2, 0);   // her quick shot gives way
                }
                else if (!live) _rubySeen[i] = false;
            }
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
        }
    }
}
