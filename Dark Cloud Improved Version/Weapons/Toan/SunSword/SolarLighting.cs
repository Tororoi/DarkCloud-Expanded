using System;
using static Dark_Cloud_Improved_Version.DungeonAddresses;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The blinding flash: the dungeon's atmosphere globals (<see cref="DungeonLighting"/> — ambient, the directional light
    /// colours, fog colour and range, whichever set MainDraw is drawing) are captured, driven to white with the fog pulled in
    /// to nothing so the whole scene washes out, and eased back to the captured values over the BLINDING's own length
    /// (<see cref="EaseSeconds"/> = <see cref="SunSword.BlindSeconds"/>), so the room brightens as the enemies recover.
    /// MainDraw re-reads the globals every frame, so the writes take effect at once and the ease is a per-tick lerp.
    /// </summary>
    internal static class SolarLighting
    {
        // The wash recedes over the whole blinding, so the room brightens back exactly as the enemies recover — tied to that
        // duration rather than restating it, so the two cannot drift apart.
        private static double EaseSeconds => SunSword.BlindSeconds;
        private const float  White       = 255f;
        private const float  FogStart    = 0f,  FogEnd = 1f;   // at the peak: everything past one unit is fog colour (white)
        private const double FogSeconds  = 1.0;    // the fog lifts in a second; only the LIGHT takes the full blinding
        private const double Decay       = 4.0;    // how sharply the wash falls away; higher puts more of the drop in the first moments

        private static bool     _active, _sub;
        private static DateTime _start;
        private static float[]  _amb, _cols, _fog;
        private static byte[]   _fogRgb;

        private static long Ambient  => _sub ? DungeonLighting.SubAmbient  : DungeonLighting.MainAmbient;
        private static long Colors   => _sub ? DungeonLighting.SubColors   : DungeonLighting.MainColors;
        private static long FogRate  => _sub ? DungeonLighting.SubFogRate  : DungeonLighting.MainFogRate;
        private static long FogColor => _sub ? DungeonLighting.SubFogColor : DungeonLighting.MainFogColor;

        internal static bool Active => _active;

        /// <summary>Capture the floor's light and go to full white.</summary>
        internal static void Flash()
        {
            if (_active) Restore();                         // a flash inside the ease: the floor's own values are the ones to keep
            _sub    = Memory.ReadInt(DungeonLighting.Mode) != 0;
            _amb    = Memory.ReadFloatBatch(Ambient, 4);
            _cols   = Memory.ReadFloatBatch(Colors, DungeonLighting.ColorRows * 4);
            _fog    = Memory.ReadFloatBatch(FogRate, 4);
            _fogRgb = Memory.ReadBytesBatch(FogColor, 3);
            if (_amb == null || _cols == null || _fog == null || _fogRgb == null
                || !Plausible(_amb[0]) || !Plausible(_amb[1]) || !Plausible(_amb[2]) || _fog[0] < 0f || _fog[1] < _fog[0])
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SunSword] lighting globals look wrong ({Describe()}) — the flash keeps the light as it is");
                return;
            }
            _active = true; _start = GameClock.Now;
            Write(1f, 1f);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] flash: {Describe()}");
        }

        /// <summary>The ease, one step; a no-op when no flash is running.</summary>
        internal static void Tick()
        {
            if (!_active) return;
            double t = (GameClock.Now - _start).TotalSeconds / EaseSeconds;
            if (t >= 1.0) { Restore(); return; }
            // Exponential decay, normalised to reach exactly 0 at the end: most of the wash is gone in the first moments and
            // the last of it lingers, which reads as a flash dying away rather than a dimmer being turned down.
            double d = Math.Exp(-Decay * t), d1 = Math.Exp(-Decay);
            float k = (float)((d - d1) / (1.0 - d1));
            double tf = Math.Min(1.0, (GameClock.Now - _start).TotalSeconds / FogSeconds);
            double df = Math.Exp(-Decay * tf), df1 = Math.Exp(-Decay);
            float kFog = (float)((df - df1) / (1.0 - df1));
            Write(k, kFog);
        }

        /// <summary>The captured light back in one write (floor change, weapon put away, end of the ease).</summary>
        internal static void Restore()
        {
            if (!_active) return;
            _active = false;
            Write(0f, 0f);
        }

        private static void Write(float k, float kf)
        {
            var amb = (float[])_amb.Clone();
            for (int c = 0; c < 3; c++) amb[c] = Lerp(_amb[c], White, k);
            var cols = (float[])_cols.Clone();
            for (int r = 0; r < DungeonLighting.ColorRows; r++)
                for (int c = 0; c < 3; c++) cols[r * 4 + c] = Lerp(_cols[r * 4 + c], White, k);
            var fog = (float[])_fog.Clone();
            if (_fog[1] > _fog[0])                          // a floor without fog keeps none: the light alone carries the flash there
            {
                fog[0] = Lerp(_fog[0], FogStart, kf);
                fog[1] = Lerp(_fog[1], FogEnd, kf);
            }
            var rgb = new byte[3];
            for (int c = 0; c < 3; c++) rgb[c] = (byte)Math.Round(Lerp(_fogRgb[c], White, kf));
            Memory.WriteBytesBatch(Ambient, Bytes(amb));
            Memory.WriteBytesBatch(Colors, Bytes(cols));
            Memory.WriteBytesBatch(FogRate, Bytes(fog));
            Memory.WriteBytesBatch(FogColor, rgb);
        }

        private static float Lerp(float a, float b, float k) => a + (b - a) * k;
        private static bool  Plausible(float v) => v >= 0f && v <= 512f;
        private static byte[] Bytes(float[] f) { var b = new byte[f.Length * 4]; Buffer.BlockCopy(f, 0, b, 0, b.Length); return b; }
        private static string Describe() =>
            $"{(_sub ? "sub" : "main")} set, ambient ({_amb?[0]:0},{_amb?[1]:0},{_amb?[2]:0},{_amb?[3]:0}), fog {_fog?[0]:0}..{_fog?[1]:0} rgb ({_fogRgb?[0]},{_fogRgb?[1]},{_fogRgb?[2]})";
    }
}
