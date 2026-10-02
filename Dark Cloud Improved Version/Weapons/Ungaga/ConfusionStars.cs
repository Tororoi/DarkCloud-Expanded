using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The spinning stars (gedit\s04\chara\e114ex, the Terra nut's) over every enemy a spear has CONFUSED: one sub-shot
    /// each, in the gem slots GemLanes holds the effect in (two lanes × 8 — the whole floor), carried with its enemy by the follow
    /// cave (CodeCaves.FollowTable, entry = the enemy's slot) at its authored height + <see cref="Lift"/>, grown in over
    /// <see cref="GrowSeconds"/> to <see cref="Scale"/>× and scaled with the spear's fade, the clip rewound before its end.
    /// Every phase radius is zero: the stars hurt nothing.</summary>
    internal static class ConfusionStars
    {
        private const string Tag = "[ConfusionStars] ";
        private const string Name = "e114ex", Dir = "gedit/s04/chara/";
        private const int    Template = 5;
        private const float  First = 1f, End = 50f, Rate = 1f, ClipLead = 1.5f;   // its one KEY: frames 1–50 at 1.0
        private const float  Lift = 3f, Scale = 1.5f, MinScale = 0.01f;
        private const double GrowSeconds = 0.25;
        private const int    Slots = 16;

        private static BorrowedEffect _fx;
        private sealed class Star { public long Inst; public int Sub; public DateTime From; }
        private static readonly Star[] _stars = new Star[Slots];
        private static bool _noLaneLogged;
        private static readonly object _lock = new();

        static ConfusionStars() { GemLanes.Releasing += Release; }

        /// <summary>A gem slot going back to its gem: the stars in it let go (their follow entries off — they write into it); the
        /// enemies take a star from the other lane next tick.</summary>
        private static void Release(long inst)
        {
            lock (_lock)
                for (int s = 0; s < Slots; s++)
                    if (_stars[s] != null && _stars[s].Inst == inst)
                    {
                        Memory.WriteUInt(CodeCaves.FollowTable + (long)s * CodeCaves.FollowStride + CodeCaves.FollowSrc, 0);
                        _stars[s] = null;
                    }
        }

        /// <summary>The effect for GemLanes (its eight sub-shots, every radius zeroed).</summary>
        internal static BorrowedEffect Effect()
        {
            if (_fx != null) return _fx;
            _fx = BorrowedShots.CustomConfig(Template, Name, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1, dir: Dir, instance: MasekiEffect.SlotBase);
            if (_fx == null) return null;
            _fx.SubShots = GemLanes.SubShotsPerLane;
            for (int ph = 0; ph < 4; ph++) BorrowedShots.SetPhaseRadius(_fx, ph, 0f);
            return _fx;
        }

        /// <summary>Every tick: a star on each confused live enemy, none on the rest; <paramref name="fade"/> scales them (the spear's).</summary>
        internal static void Drive(Func<int, bool> confused, float fade)
        {
            if (!Player.CheckDunIsWalkingMode()) return;
            var lanes = GemLanes.Ready();                                                     // taken before our lock: GemLanes calls in while holding its own
            byte[] cfg = GemLanes.Cfg;
            lock (_lock)
            for (int s = 0; s < Slots; s++)
            {
                bool want = Enemies.IsLive(s) && confused(s);
                if (!want) { Stop(s); continue; }
                if (_stars[s] == null && !Start(s, lanes, cfg)) continue;
                Keep(s, fade);
            }
        }

        /// <summary>Every star down.</summary>
        internal static void StopAll() { lock (_lock) for (int s = 0; s < Slots; s++) Stop(s); }

        private static bool Start(int slot, System.Collections.Generic.List<long> lanes, byte[] cfg)
        {
            if (cfg == null || lanes.Count == 0)
            {
                if (!_noLaneLogged) { _noLaneLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no gem slot holds the stars yet — the confused go unmarked for now"); }
                return false;
            }
            _noLaneLogged = false;
            long p = EnemyAddresses.CharObjects.PosAddr(slot);
            float x = Memory.ReadFloat(p), h = Memory.ReadFloat(p + 4) + TerraSword.HeadHeight(slot) + Lift, y = Memory.ReadFloat(p + 8);
            uint laneCfgs = (uint)(CodeCaves.GemLaneCfg - 0x20000000L);
            foreach (long inst in lanes)
            {
                uint c = Memory.ReadUInt(inst + ShotEffectPack.OffCfg);
                if (c < laneCfgs || c >= laneCfgs + GemLanes.Lanes * ShotEffectPack.CfgSize) continue;   // handed back since the list was taken (the hand-back waits on our lock)
                if (!BorrowedShots.BurstIn(cfg, inst, x, h, y, 0, MinScale)) continue;             // all eight busy: the next lane
                var st = new Star { Inst = inst, Sub = Memory.ReadInt(inst + ShotEffectPack.OffLastIdx), From = GameClock.Now };
                _stars[slot] = st;
                Restart(st);
                long e = CodeCaves.FollowTable + (long)slot * CodeCaves.FollowStride;
                Memory.WriteUInt(e + CodeCaves.FollowSrc, 0);
                Memory.WriteUInt(e + CodeCaves.FollowDst, (uint)(Obj(st) + ShotEffectPack.ObjPos - 0x20000000L));
                Memory.WriteVec3(e + CodeCaves.FollowOff, 0f, h - Memory.ReadFloat(p + 4), 0f);
                Memory.WriteUInt(e + CodeCaves.FollowSrc, (uint)(p - 0x20000000L));             // on, last
                return true;
            }
            return false;
        }

        private static long Obj(Star st) => st.Inst + ShotEffectPack.OffObj + st.Sub * ShotEffectPack.ObjStride;

        private static void Restart(Star st)
        {
            long o = Obj(st);
            Memory.WriteInt  (o + ShotEffectPack.ObjMotId, 0);
            Memory.WriteInt  (o + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat(o + ShotEffectPack.ObjFrame, First);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, Rate);
        }

        private static void Keep(int slot, float fade)
        {
            var st = _stars[slot];
            long o = Obj(st);
            if (Memory.ReadUShort(st.Inst + ShotEffectPack.OffActive + st.Sub * 2) == 0)
            {   // retired under us: back on
                Memory.WriteUShort(st.Inst + ShotEffectPack.OffPhase + st.Sub * 2, 0);
                Memory.WriteUShort(st.Inst + ShotEffectPack.OffActive + st.Sub * 2, 1);
                Restart(st);
            }
            else if (Memory.ReadFloat(o + ShotEffectPack.ObjFrame) >= End - ClipLead) Restart(st);
            float k = Math.Max(MinScale, (float)Math.Clamp((GameClock.Now - st.From).TotalSeconds / GrowSeconds, 0.0, 1.0) * Scale * fade);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, Rate);
            Memory.WriteVec3 (o + CCharacter.CharScale, k, k, k);
        }

        private static void Stop(int slot)
        {
            var st = _stars[slot];
            if (st == null) return;
            Memory.WriteUInt(CodeCaves.FollowTable + (long)slot * CodeCaves.FollowStride + CodeCaves.FollowSrc, 0);
            Memory.WriteUShort(st.Inst + ShotEffectPack.OffActive + st.Sub * 2, 0);
            _stars[slot] = null;
        }
    }
}
