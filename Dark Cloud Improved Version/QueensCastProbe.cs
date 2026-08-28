using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// LOGGING-ONLY probe for the Queens canal cast clamp (FishLineClamp cave @0x229100). While fishing
    /// in Queens it samples the rope state every few frames and tracks the per-cast maximum z, so one
    /// test cast answers: is point[18] (the bobber the ELF clamp targets) actually held at z&lt;=48 while
    /// the VISIBLE bobber crosses the wall (= the visual is driven by something else, e.g. the rod rig's
    /// animation bones), or does point[18] itself exceed 48 (= the cave isn't running / wrong site)?
    /// Pure reads — the fix itself lives in the ISO patch, this only observes it.
    /// </summary>
    internal static class QueensCastProbe
    {
        internal static bool Enabled = true;

        private const long MapNoAddr    = 0x202A2518;   // town MapNo (2 = Queens)
        private const long CharaFishing = 0x202A26E8;   // fishing state machine
        private const long SetUkiPos    = 0x202A2B74;   // pin-active flag (consumed by FishLineStep)
        private const long Point18Z     = 0x21D55F58;   // point[18].z — the bobber the clamp targets
        private const long Ukip0Z       = 0x21D56358;   // ukip[0].z — float visual cluster
        private const long UkiTargetZ   = 0x21D56458;   // uki_target.z — the animation-bone pin target
        private const long OldP18Z      = 0x21D560D8;   // old_p[18].z — clamp also writes this

        private static int _tick, _lastState;
        private static float _maxP18, _maxUki, _maxTgt;

        internal static void Tick()
        {
            if (!Enabled || Memory.ReadInt(MapNoAddr) != 2) return;
            int st = Memory.ReadInt(CharaFishing);
            if (st < 2) { _lastState = st; return; }        // only during cast/flight/waiting

            float p18 = Memory.ReadFloat(Point18Z);
            float uki = Memory.ReadFloat(Ukip0Z);
            float tgt = Memory.ReadFloat(UkiTargetZ);
            if (st != _lastState && _lastState < 2) { _maxP18 = _maxUki = _maxTgt = float.MinValue; }
            _lastState = st;
            _maxP18 = Math.Max(_maxP18, p18); _maxUki = Math.Max(_maxUki, uki); _maxTgt = Math.Max(_maxTgt, tgt);

            if (++_tick % 10 != 0) return;                  // ~2 Hz at the town loop rate
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[QCast] st={st} p18z={p18:0.0}(max {_maxP18:0.0}) ukiz={uki:0.0}(max {_maxUki:0.0}) " +
                $"tgtz={tgt:0.0}(max {_maxTgt:0.0}) oldz={Memory.ReadFloat(OldP18Z):0.0} pin={Memory.ReadInt(SetUkiPos)}");
        }
    }
}
