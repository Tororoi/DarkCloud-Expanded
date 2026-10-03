using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The spinning stars (gedit\s04\chara\e114ex, the Terra nut's) over CONFUSED enemies, on any floor: sub-shots of the
    /// resident stars instance (StarsLane, <see cref="StarsLane.SubShots"/> of them — given to the confused enemies NEAREST the
    /// player; as one recovers or dies the next nearest takes its star), carried with its enemy by the follow cave
    /// (CodeCaves.FollowTable, entry = the enemy's slot) at its authored height + <see cref="Lift"/>, grown in over
    /// <see cref="GrowSeconds"/> to <see cref="Scale"/>× and scaled by <see cref="Fade"/> (Babel's Spear fades them with its copy),
    /// the clip rewound before its end. Every phase radius is zero: the stars hurt nothing. Driven by ConfuseAbility's loop.</summary>
    internal static class ConfusionStars
    {
        private const string Tag = "[ConfusionStars] ";
        private const string Name = "e114ex", Dir = "gedit/s04/chara/";
        private const int    Template = 5;
        private const float  First = 1f, End = 50f, Rate = 1f, ClipLead = 1.5f;   // its one KEY: frames 1–50 at 1.0
        private const float  Lift = 3f, Scale = 1.5f, MinScale = 0.01f;
        private const double GrowSeconds = 0.25;
        private const int    Slots = 16;

        /// <summary>The stars' size factor 0..1 (1 = full; Babel's Spear sets its copy's fade while it stands).</summary>
        internal static float Fade = 1f;

        private static BorrowedEffect _fx;
        private sealed class Star { public int Sub; public DateTime From; }
        private static readonly Star[] _stars = new Star[Slots];
        private static readonly object _lock = new();

        /// <summary>The stars effect for StarsLane (its eight sub-shots, every radius zeroed).</summary>
        internal static BorrowedEffect Effect()
        {
            if (_fx != null) return _fx;
            _fx = BorrowedShots.VisualOnly(Template, Name, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: Dir, instance: 0);   // instance 0: its own cache key — StarsLane names the instance
            if (_fx == null) return null;
            _fx.SubShots = StarsLane.SubShots;
            return _fx;
        }

        /// <summary>Every tick: a star on each of the (up to eight) confused live enemies nearest the player, none on the rest.</summary>
        internal static void Drive(Func<int, bool> confused)
        {
            long inst = StarsLane.Instance;
            byte[] cfg = StarsLane.Cfg;
            if (inst == 0 || cfg == null || !Player.CheckDunIsWalkingMode()) return;
            lock (_lock)
            {
                var want = Wanted(confused);
                for (int s = 0; s < Slots; s++) if (_stars[s] != null && !want.Contains(s)) Stop(s);
                foreach (int s in want)
                {
                    if (_stars[s] == null && !Start(s, inst, cfg)) continue;
                    Keep(s, inst);
                }
            }
        }

        /// <summary>Every star down (their follow entries off; the sub-shots too while the instance is live).</summary>
        internal static void StopAll() { lock (_lock) for (int s = 0; s < Slots; s++) Stop(s); }

        /// <summary>The confused live slots nearest the player, at most the instance's sub-shots.</summary>
        private static HashSet<int> Wanted(Func<int, bool> confused)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            var list = new List<(float d, int s)>();
            for (int s = 0; s < Slots; s++)
            {
                if (!Enemies.IsLive(s) || !confused(s) || !Confusion.IsActive(s)) continue;   // never over a dormant enemy (a shut chest mimic, one not yet activated)
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - px, dy = Memory.ReadFloat(p + 8) - py;
                list.Add((dx * dx + dy * dy, s));
            }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            var set = new HashSet<int>();
            for (int i = 0; i < list.Count && i < StarsLane.SubShots; i++) set.Add(list[i].s);
            return set;
        }

        private static long Obj(long inst, Star st) => inst + ShotEffectPack.OffObj + st.Sub * ShotEffectPack.ObjStride;

        private static bool Start(int slot, long inst, byte[] cfg)
        {
            long p = EnemyAddresses.CharObjects.PosAddr(slot);
            float x = Memory.ReadFloat(p), h = Memory.ReadFloat(p + 4) + EnemyBody.HeadHeight(slot) + Lift, y = Memory.ReadFloat(p + 8);
            if (!BorrowedShots.BurstIn(cfg, inst, x, h, y, 0, MinScale)) return false;          // all eight busy
            var st = new Star { Sub = Memory.ReadInt(inst + ShotEffectPack.OffLastIdx), From = GameClock.Now };
            _stars[slot] = st;
            Restart(inst, st);
            long e = CodeCaves.FollowTable + (long)slot * CodeCaves.FollowStride;
            Memory.WriteUInt(e + CodeCaves.FollowSrc, 0);
            Memory.WriteUInt(e + CodeCaves.FollowDst, (uint)(Obj(inst, st) + ShotEffectPack.ObjPos - 0x20000000L));
            Memory.WriteVec3(e + CodeCaves.FollowOff, 0f, h - Memory.ReadFloat(p + 4), 0f);
            Memory.WriteUInt(e + CodeCaves.FollowSrc, (uint)(p - 0x20000000L));                 // on, last
            return true;
        }

        /// <summary>The star's one clip from its first frame at its rate.</summary>
        private static void Restart(long inst, Star st) => ShotEffects.SetClip(Obj(inst, st), 0, First, Rate);

        private static void Keep(int slot, long inst)
        {
            var st = _stars[slot];
            long o = Obj(inst, st);
            if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + st.Sub * 2) == 0)
            {   // retired under us: back on
                Memory.WriteUShort(inst + ShotEffectPack.OffPhase + st.Sub * 2, 0);
                Memory.WriteUShort(inst + ShotEffectPack.OffActive + st.Sub * 2, 1);
                Restart(inst, st);
            }
            else if (Memory.ReadFloat(o + ShotEffectPack.ObjFrame) >= End - ClipLead) Restart(inst, st);
            float k = Math.Max(MinScale, (float)Math.Clamp((GameClock.Now - st.From).TotalSeconds / GrowSeconds, 0.0, 1.0) * Scale * Fade);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, Rate);
            Memory.WriteVec3 (o + CCharacter.CharScale, k, k, k);
        }

        private static void Stop(int slot)
        {
            var st = _stars[slot];
            if (st == null) return;
            Memory.WriteUInt(CodeCaves.FollowTable + (long)slot * CodeCaves.FollowStride + CodeCaves.FollowSrc, 0);
            long inst = StarsLane.Instance;   // 0 once the floor is left: its memory is the next floor's pool
            if (inst != 0) Memory.WriteUShort(inst + ShotEffectPack.OffActive + st.Sub * 2, 0);
            _stars[slot] = null;
        }
    }
}
