using System;
using static Dark_Cloud_Improved_Version.TerraSword;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The boulder's shockwave: syougekiha (dun/mainchara/wep_eff) borrowed into the SECOND main-character effect instance
    /// (<see cref="TerraSword.WantedShot"/> asks for it), burst once where the rock lands at ShockScale× (<see cref="ShockPlay"/>),
    /// held at the spot at its size and rate every tick (<see cref="ShockDrive"/>) until the engine retires it at its clip's end, the
    /// second-effect caves stepping and drawing the instance while CodeCaves.SecondEffectLive is set. One of the
    /// <see cref="TerraSword"/> classes, which share their members through using static.</summary>
    internal static class Shockwave
    {
        private const string Tag = "[TerraSword/Shockwave] ";
        internal static BorrowedEffect _shock;
        private static int _sub = -1;                               // the sub-shot playing the shockwave (−1 = none)

        private static long ShockObj => _shock.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        internal static void ShockPlay()
        {
            ShockStop();
            if (_shock == null || !BorrowedShots.Entered(_shock)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shockwave not entered on this floor — the rock lands without it"); return; }
            if (!BorrowedShots.Burst(_shock, _x, _ground, _y, 0, ShockScale)) return;
            _sub = Memory.ReadInt(_shock.Instance + ShotEffectPack.OffLastIdx);
            ShotEffects.SetClip(ShockObj, 0, ShockStart, ShockRate);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                           // the second instance stepped and drawn (the second-effect caves)
        }

        /// <summary>The shockwave each tick: held at the spot, its size and rate; the engine retires it at its clip's end.</summary>
        internal static void ShockDrive()
        {
            if (_sub < 0 || !Player.CheckDunIsWalkingMode()) return;
            if (Memory.ReadUShort(_shock.Instance + ShotEffectPack.OffActive + _sub * 2) == 0) { _sub = -1; Memory.WriteInt(CodeCaves.SecondEffectLive, 0); return; }
            long o = ShockObj;
            Memory.WriteVec3 (o + ShotEffectPack.ObjPos, _x, _ground, _y);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, ShockRate);
            Memory.WriteVec3 (o + CCharacter.CharScale, ShockScale, ShockScale, ShockScale);
        }

        internal static void ShockStop()
        {
            if (_sub < 0 || _shock == null) return;
            Memory.WriteUShort(_shock.Instance + ShotEffectPack.OffActive + _sub * 2, 0);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _sub = -1;
        }
    }
}
