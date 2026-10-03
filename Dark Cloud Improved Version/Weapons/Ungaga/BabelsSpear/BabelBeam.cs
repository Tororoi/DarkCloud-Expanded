using System;
using static Dark_Cloud_Improved_Version.BabelsSpear;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The confusion area's beam: the Dark Genie's small beam, recoloured by the ISO patch onto the dead zibaku_f name and
    /// borrowed into the SECOND main-character effect instance (<see cref="BabelsSpear.WantedShot"/>; the ISO's second-effect caves
    /// step and draw it while CodeCaves.SecondEffectLive is set), burst at the spear's spot (<see cref="ShockStart"/>) and driven
    /// each tick (<see cref="ShockDrive"/>): its KEY 0 as the spear rises, KEY 1 looped while it stands, KEY 2 as it vanishes, all at
    /// ShockRate; a retire before the vanish is re-armed. One of the <see cref="BabelsSpear"/> classes, which share their members
    /// through using static.</summary>
    internal static class BabelBeam
    {
        private const string Tag = "[BabelsSpear/BabelBeam] ";
        internal static BorrowedEffect _shock;
        private static int _shockSlot = -1;   // the sub-shot playing the shockwave (−1 = none)
        private static int _shockKey = -1;    // the KEY it is on
        private static int _shockRearms;      // DIAGNOSTIC: times the engine retired the sub-shot and it was re-armed
        private static DateTime _shockReport;

        internal static void ShockStart()
        {
            _shockSlot = -1; _shockKey = -1; _shockRearms = 0;
            if (_shock == null || !BorrowedShots.Entered(_shock)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shockwave not entered on this floor — the area goes undrawn"); return; }
            if (!BorrowedShots.Burst(_shock, _sx, _ground, _sy, 0, 1f)) return;
            _shockSlot = Memory.ReadInt(_shock.Instance + ShotEffectPack.OffLastIdx);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 1);                                    // the second instance stepped and drawn beside the live one (the ISO's second-effect caves)
            ShockDrive(0);
        }

        /// <summary>The sub-shot each tick: alive, at the spear, scaled to the area, on the KEY the spear calls for at ShockRate.
        /// The rise hands over to the loop and the loop rewinds to its first frame <see cref="ClipLead"/> frames before the
        /// clip's end (the cursor parks a fraction short of it and reads finished); the vanish starts when the spear's time is
        /// up and, being the config's muzzle motion, ends the sub-shot through the engine at its last frame. A retire before
        /// that (a swap, a reload) is re-armed.</summary>
        private const float ClipLead = 0.5f;   // frames before a KEY's end to move on: a tick's advance (0.3 at ShockRate) plus the step the cursor parks short by
        internal static void ShockDrive(double age)
        {
            if (_shockSlot < 0 || _shock == null || !Player.CheckDunIsWalkingMode()) return;
            long inst = _shock.Instance, obj = inst + ShotEffectPack.OffObj + _shockSlot * ShotEffectPack.ObjStride;
            bool alive = Memory.ReadUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2) != 0;
            float frame = Memory.ReadFloat(obj + ShotEffectPack.ObjFrame);
            int status = Memory.ReadInt(obj + CharacterMotion.MotionStatusOffset);
            bool riseDone = _shockKey == ShockRise && (status == 3 || frame >= ShockRiseEnd - ClipLead);
            int key = age >= SpearSeconds ? ShockVanish : (_shockKey == ShockRise && !riseDone) || _shockKey < 0 ? ShockRise : ShockLoop;
            float start = key == ShockRise ? ShockRiseStart : key == ShockLoop ? ShockLoopStart : ShockVanishStart;
            float end   = key == ShockRise ? ShockRiseEnd   : key == ShockLoop ? ShockLoopEnd   : ShockVanishEnd;
            bool restart = key != _shockKey || (key == ShockLoop && (status == 3 || frame >= end - ClipLead || frame < start - 1));
            if (DebugDiagnostics.Enabled && (GameClock.Now - _shockReport).TotalMilliseconds >= 250)
            {   // DIAGNOSTIC
                _shockReport = GameClock.Now;
                uint root = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                string rootName = Memory.IsValidGuest(root) ? System.Text.Encoding.ASCII.GetString(Memory.ReadBytesBatch(Memory.ToMmu(root) + CFrameVu1.Name, 12) ?? new byte[0]).TrimEnd('\0') : "none";
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shock #{_shockSlot} age {age:F1}: alive {alive}, phase {Memory.ReadUShort(inst + ShotEffectPack.OffPhase + _shockSlot * 2)}, key {key} (held {_shockKey}), frame {frame:F1}, motId {Memory.ReadInt(obj + ShotEffectPack.ObjMotId)}, flags {Memory.ReadInt(obj + ShotEffectPack.ObjMotFlag)}, status {Memory.ReadInt(obj + CharacterMotion.MotionStatusOffset)}, spd {Memory.ReadFloat(obj + ShotEffectPack.ObjMotSpd):F2}, scale ({Memory.ReadFloat(obj + CCharacter.CharScale):F1},{Memory.ReadFloat(obj + CCharacter.CharScale + 4):F1},{Memory.ReadFloat(obj + CCharacter.CharScale + 8):F1}), pos ({Memory.ReadFloat(obj + ShotEffectPack.ObjPos):F0},{Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4):F0},{Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8):F0}), root `{rootName}`, re-arms {_shockRearms}");
            }
            if (age >= SpearSeconds + VanishSeconds || (!alive && key == ShockVanish)) { if (alive) Memory.WriteUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2, 0); _shockSlot = -1; Memory.WriteInt(CodeCaves.SecondEffectLive, 0); return; }   // the vanish ran out: the engine ended it
            if (!alive) { Memory.WriteUShort(inst + ShotEffectPack.OffPhase + _shockSlot * 2, 0); Memory.WriteUShort(inst + ShotEffectPack.OffActive + _shockSlot * 2, 1); restart = true; _shockRearms++; }
            if (restart)
            {
                Memory.WriteInt  (obj + ShotEffectPack.ObjMotId, key);
                Memory.WriteInt  (obj + ShotEffectPack.ObjMotFlag, 6);
                Memory.WriteFloat(obj + ShotEffectPack.ObjFrame, start);
                _shockKey = key;
            }
            Memory.WriteFloat(obj + ShotEffectPack.ObjMotSpd, ShockRate);                              // an absolute rate: ShockRate frames a tick
            Memory.WriteVec3 (obj + ShotEffectPack.ObjPos, _sx, _ground, _sy);
            Memory.WriteVec3 (obj + CCharacter.CharScale, ShockScale, ShockScale, ShockScale);
        }

        internal static void ShockStop()
        {
            if (_shockSlot < 0 || _shock == null) return;
            Memory.WriteUShort(_shock.Instance + ShotEffectPack.OffActive + _shockSlot * 2, 0);
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _shockSlot = -1; _shockKey = -1;
        }
    }
}
