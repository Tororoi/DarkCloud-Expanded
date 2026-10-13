using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The poof a summon appears in: e228ex (gedit\e03's smoke, baked onto the dead dun/effect/zibaku_t) borrowed into the SECOND
    /// main-character effect instance (<see cref="BorrowedShots"/>; Ungaga's and Ruby's are otherwise idle), burst at a spot at the
    /// summoner's scale (0.5 for both), its one clip played at <see cref="ClipRate"/> — three times its own speed — turned to the
    /// summoner's facing,
    /// held there (or following its summon natively: the pockle walks off in it) and faded out from frame <see cref="FadeFrom"/> to nothing at
    /// <see cref="ClipEnd"/>, where the engine retires it.
    /// Cactus' Desert Bloom rises in it and the Pocklekul's pockle appears in it; their growths are timed on its frames
    /// (<see cref="ClipStart"/>, <see cref="ClipRate"/>). One plays at a time: the two weapons are never wielded together.</summary>
    internal static class SummonSmoke
    {
        private const string Tag = "[SummonSmoke] ";
        internal const float ClipStart = 1f, ClipEnd = 50f, ClipRate = 0.6f, FadeFrom = 32f;   // e228ex's one KEY: frames 1–50 (its own rate 0.2)
        private const int    Template = 5;                 // a stock config's shape; the name and motions are replaced
        private const string Name     = "zibaku_t";

        private static BorrowedEffect _fx;
        private static int _sub = -1;                       // the sub-shot playing it (−1 = none)
        private static float _x, _ground, _y, _yaw;
        private static bool  _held;                         // held at (x, ground, y) each tick (false: something else places it — the pockle's native pin)

        /// <summary>The borrowed effect (the providers hand it to <see cref="BorrowedShots"/> while their weapon is wielded). Every phase
        /// radius zeroed: it is the visual only. Its one clip is the muzzle motion: the engine retires the sub-shot at the clip's last
        /// frame (49–50), where the fade has already reached nothing.</summary>
        internal static BorrowedEffect Effect()
            => _fx ??= BorrowedShots.VisualOnly(Template, Name, muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                                dir: BorrowedShots.EffectDir, instance: ShotEffectPack.CharaMainEffectCrash);

        /// <summary>A poof at (x, ground, y) turned to <paramref name="yaw"/>, <paramref name="scale"/> times its own size — held there each
        /// tick, or (<paramref name="held"/> false) left where something else puts it (<see cref="PosAddr"/>: the pockle's smoke follows it
        /// natively, frame by frame); false (logged) when the effect is not entered on this floor.</summary>
        internal static bool Start(float x, float ground, float y, float yaw, float scale, string who, bool held = true)
        {
            End();
            if (_fx == null || !BorrowedShots.Entered(_fx)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"not entered on this floor — the {who} appears without it"); return false; }
            if (!BorrowedShots.Burst(_fx, x, ground, y, 0, scale)) return false;
            _sub = Memory.ReadInt(_fx.Instance + ShotEffectPack.OffLastIdx);
            _x = x; _ground = ground; _y = y; _yaw = yaw; _held = held;
            long o = Obj;
            SubShotFade.Set(o, 1f);                                                   // a sub-shot a previous smoke faded, back to full
            ShotEffects.SetClip(o, 0, ClipStart, ClipRate);                           // the rate absolute (its own KEY is 0.2)
            TurnRoot(o);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                           // the second instance stepped and drawn (the second-effect caves)
            return true;
        }

        /// <summary>Each tick while it may play: held at its spot, faded from <see cref="FadeFrom"/>; ended once the engine retires it.</summary>
        internal static void Drive()
        {
            if (_sub < 0 || !Player.CheckDunIsWalkingMode()) return;
            long o = Obj;
            if (Memory.ReadUShort(_fx.Instance + ShotEffectPack.OffActive + _sub * 2) == 0) { End(); return; }
            if (_held) Memory.WriteVec3(o + ShotEffectPack.ObjPos, _x, _ground, _y);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, ClipRate);                 // held: nothing resets it mid-clip, but a phase change would
            float frame = Memory.ReadFloat(o + ShotEffectPack.ObjFrame);
            if (frame >= FadeFrom) SubShotFade.Set(o, 1f - Math.Clamp((frame - FadeFrom) / (ClipEnd - FadeFrom), 0f, 1f));
        }

        /// <summary>The playing clip's frame (<see cref="ClipStart"/>..<see cref="ClipEnd"/>), or −1 when none plays — a summon's own
        /// appearance (the pockle's tint) runs on it, game frames rather than a clock.</summary>
        internal static float Frame => _sub < 0 ? -1f : Memory.ReadFloat(Obj + ShotEffectPack.ObjFrame);

        /// <summary>The playing sub-shot's position (MMU x, h, y), or 0 when none plays.</summary>
        internal static long PosAddr => _sub < 0 ? 0 : Obj + ShotEffectPack.ObjPos;

        /// <summary>Put out (if playing) and the second instance left to the engine.</summary>
        internal static void End()
        {
            if (_sub >= 0 && _fx != null)
            {
                Memory.WriteUShort(_fx.Instance + ShotEffectPack.OffActive + _sub * 2, 0);
                SubShotFade.Set(Obj, 1f);                                             // handed back at full
                Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            }
            _sub = -1;
        }

        private static long Obj => _fx.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        /// <summary>The smoke turned to the summoner's facing about the vertical through its model's ROOT frame (null7: no motion track
        /// touches it), its local 3×3 set to the engine's RotMatrixY — row 0 = (cos, 0, −sin), row 2 = (sin, 0, cos), so its local +Z faces
        /// (sin, cos) — and its world cache dropped, as BladeProp bakes its copies upright. An absolute write: the sub-shot's model is
        /// reused burst to burst. (The sub-shot object's own Euler angles, +0x60, do not reach a shot effect's draw.)</summary>
        private static void TurnRoot(long obj)
        {
            uint root = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
            if (!Memory.IsValidGuest(root)) return;
            long r = Memory.ToMmu(root);
            float c = (float)Math.Cos(_yaw), sn = (float)Math.Sin(_yaw);
            float[] m = { c, 0f, -sn,  0f, 1f, 0f,  sn, 0f, c };
            for (int i = 0; i < 9; i++) Memory.WriteFloat(r + CFrameVu1.LocalMatrix + (i / 3) * 0x10 + (i % 3) * 4, m[i]);
            Memory.WriteInt(r + CFrameVu1.WorldCacheA, 0);
        }
    }
}
