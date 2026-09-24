using System;
using static Dark_Cloud_Improved_Version.DungeonAddresses;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The blinding flash: the dungeon's atmosphere globals (<see cref="DungeonLighting"/> — ambient, the directional light
    /// colours, fog colour and range, whichever set MainDraw is drawing) are captured, driven to white with the fog pulled in
    /// to nothing so the whole scene washes out — the light in <see cref="FlashColour"/>, the fog pure white — and eased
    /// back to the captured values over the BLINDING's own length
    /// (<see cref="EaseSeconds"/> = <see cref="SunSword.BlindSeconds"/>), so the room brightens as the enemies recover.
    /// MainDraw re-reads the globals every frame, so the writes take effect at once and the ease is a per-tick lerp.
    /// </summary>
    internal static class SolarLighting
    {
        // The wash recedes over the whole blinding, so the room brightens back exactly as the enemies recover — tied to that
        // duration rather than restating it, so the two cannot drift apart.
        /// <summary>How long the wash takes to recede. The flash-bang swords tie it to the blinding
        /// (<see cref="SunSword.BlindSeconds"/>) so the room brightens as the enemies recover; the Sword of Zeus's
        /// bolt is a strike, not a blaze, and takes its own shorter figure (SunSword.SolarProfile.EaseSeconds,
        /// set with the colours by ArmLighting). The blinding itself is unchanged by this.</summary>
        internal static double EaseSeconds = SunSword.BlindSeconds;
        /// <summary>The colour the LIGHT is driven to — the ambient, the directional rows and Toan's own pulse — and the
        /// colour the FOG goes. Each sword sets its own pair before it flashes (SunSword.SolarProfile): the Sun Sword's
        /// is <see cref="SunLight"/> / <see cref="SunFog"/>, a warm near-white with the fog pure white (tinting the fog
        /// warm as well muddied the wash rather than warming it).</summary>
        internal static float[] FlashColour = SunLight;
        internal static float[] FogColour   = SunFog;
        internal static readonly float[] SunLight = { 255f, 240f, 200f };
        internal static readonly float[] SunFog   = { 255f, 255f, 255f };
        private const float  FogStart    = 0f,  FogEnd = 1f;   // at the peak: everything past one unit is fog colour (white)
        private const double FogSeconds  = 1.0;    // the fog lifts in a second; only the LIGHT takes the full blinding
        private const double Decay       = 4.0;    // how sharply the wash falls away; higher puts more of the drop in the first moments

        // THE DIM: the light and fog driven DOWN before a flash, so the flash lands from darkness — an impact, not a
        // screen effect. Ambient and colour rows toward DimKeep of themselves, the fog closed in to DimFogPull of its
        // reach and its colour toward black. Same capture as the flash uses, taken once when the dim begins; the
        // flash then writes white over that ORIGINAL capture and eases back to it, never to the dark.
        private const float  DimKeep     = 0.15f;  // how much of the floor's light is left at full dim
        private const float  DimFogPull  = 0.35f;  // the fog's start/end, as a fraction of where they were
        private static bool     _dimming;

        private static bool     _active, _sub;
        private static DateTime _start;
        private static float[]  _amb, _cols, _fog;
        private static byte[]   _fogRgb;

        private static long Ambient  => _sub ? DungeonLighting.SubAmbient  : DungeonLighting.MainAmbient;
        private static long Colors   => _sub ? DungeonLighting.SubColors   : DungeonLighting.MainColors;
        private static long FogRate  => _sub ? DungeonLighting.SubFogRate  : DungeonLighting.MainFogRate;
        private static long FogColor => _sub ? DungeonLighting.SubFogColor : DungeonLighting.MainFogColor;

        internal static bool Active => _active;
        /// <summary>Capture the floor's light and go to full white. A dim in progress hands over its capture — the
        /// floor's real light, taken before the darkening — so the ease returns there.</summary>
        internal static void Flash()
        {
            if (_active) Restore();                         // a flash inside the ease: the floor's own values are the ones to keep
            if (_dimming) _dimming = false;                 // …but a dim's capture IS the floor's own: keep it
            else if (!Capture()) return;
            _active = true; _start = GameClock.Now;
            Write(1f, 1f, RestDim);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] flash: {Describe()}");
        }

        /// <summary>The floor's light set, captured; false (and logged) if it does not look like one.</summary>
        private static bool Capture()
        {
            _sub    = Memory.ReadInt(DungeonLighting.Mode) != 0;
            _amb    = Memory.ReadFloatBatch(Ambient, 4);
            _cols   = Memory.ReadFloatBatch(Colors, DungeonLighting.ColorRows * 4);
            _fog    = Memory.ReadFloatBatch(FogRate, 4);
            _fogRgb = Memory.ReadBytesBatch(FogColor, 3);
            if (_amb == null || _cols == null || _fog == null || _fogRgb == null
                || !Plausible(_amb[0]) || !Plausible(_amb[1]) || !Plausible(_amb[2]) || _fog[0] < 0f || _fog[1] < _fog[0])
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SunSword] lighting globals look wrong ({Describe()}) — the light is kept as it is");
                return false;
            }
            return true;
        }

        /// <summary>Start a dim: capture the floor's light (unless a flash is easing, whose capture already is the
        /// floor's). <see cref="Dim"/> then drives it down; <see cref="Flash"/> takes over from it, or
        /// <see cref="EndDim"/> puts it back if nothing follows.</summary>
        internal static void BeginDim()
        {
            if (_dimming) return;
            if (_active) { _active = false; }               // an easing flash: keep its capture, drop its ease
            else if (!Capture()) return;
            _dimming = true; _dimK = -1f;
        }

        /// <summary>The scene at darkness <paramref name="k"/> (0 = the floor's own light, 1 = full dim). Writes
        /// only while a dim is up and no flash has taken over — a late write after the flash would put a dark frame
        /// on top of the white.</summary>
        /// <summary><see cref="Dim"/>, written only when the level has moved — a held level costs nothing per tick.</summary>
        internal static void DimTo(float k)
        {
            if (!_dimming || _active) return;
            if (Math.Abs(k - _dimK) < 0.005f) return;
            Dim(k);
        }
        private static float _dimK = -1f;
        internal static void Dim(float k)
        {
            if (!_dimming || _active) return;
            _dimK = k;
            Write(0f, 0f, Math.Max(0f, Math.Min(1f, k)));
        }

        /// <summary>A dim that nothing followed: the floor's light back in one write.</summary>
        internal static void EndDim()
        {
            if (!_dimming) return;
            _dimming = false;
            if (!_active) Write(0f, 0f, 0f);
        }

        /// <summary>The wash after the flash — how dark the room RESTS once the white has receded (0 = the floor's own
        /// light, as the flash-bang swords have it), for how long after the flash it rests (<see cref="RestSeconds"/>,
        /// the whole hold), and over how many of its last seconds it comes back to normal (<see cref="RestRelease"/>).
        /// Set with the colours by SunSword.SolarProfile.ArmLighting.</summary>
        internal static float  RestDim = 0f;
        internal static double RestSeconds = 0, RestRelease = 1.0;

        /// <summary>The ease, one step; a no-op when no flash is running. The white recedes over <see cref="EaseSeconds"/>
        /// onto the rest level, which holds until the last <see cref="RestRelease"/> of <see cref="RestSeconds"/>, when
        /// the light and fog ramp back to the floor's own.</summary>
        internal static void Tick()
        {
            if (!_active) return;
            double t  = (GameClock.Now - _start).TotalSeconds;
            double total = RestDim > 0f ? RestSeconds : EaseSeconds;
            if (t >= total) { Restore(); return; }
            // Exponential decay, normalised to reach exactly 0 at the end: most of the wash is gone in the first moments and
            // the last of it lingers, which reads as a flash dying away rather than a dimmer being turned down.
            double te = Math.Min(1.0, t / EaseSeconds);
            double d1 = Math.Exp(-Decay);
            float k    = (float)((Math.Exp(-Decay * te) - d1) / (1.0 - d1));
            double tf  = Math.Min(1.0, t / FogSeconds);
            float kFog = (float)((Math.Exp(-Decay * tf) - d1) / (1.0 - d1));
            float dim  = 0f;
            if (RestDim > 0f)
                dim = t < RestSeconds - RestRelease ? RestDim
                    : RestDim * (float)Math.Max(0.0, (RestSeconds - t) / Math.Max(0.01, RestRelease));
            Write(k, kFog, dim);
        }

        /// <summary>The captured light back in one write (floor change, weapon put away, end of the ease).</summary>
        internal static void Restore()
        {
            if (!_active && !_dimming) return;
            _active = false; _dimming = false;
            Write(0f, 0f, 0f);
        }

        /// <summary>HOW MUCH of the fog wash the flash does, 0..1. The wash is the fog's start/end pulled toward 0..1
        /// (everything past a unit becomes fog colour) and its colour driven white; this scales that pull, so 1 is
        /// the Sun Sword's full white-out, 0 leaves the fog exactly as it was, and a fraction moves it that far.</summary>
        internal static float FogAmount = 1f;

        /// <summary>The scene at white blend <paramref name="k"/> / fog wash <paramref name="kf"/> over a base darkened to
        /// <paramref name="dim"/>: the floor's captured light is pulled down first (ambient and colour rows toward
        /// DimKeep of themselves, the fog closed in to DimFogPull of its reach and its colour toward black), and the
        /// flash's white is blended over THAT — so as the white recedes, it recedes onto the dim, not past it.</summary>
        private static void Write(float k, float kf, float dim)
        {
            kf *= Math.Max(0f, Math.Min(1f, FogAmount));
            var amb = (float[])_amb.Clone();
            for (int c = 0; c < 3; c++) amb[c] = Lerp(Lerp(_amb[c], _amb[c] * DimKeep, dim), FlashColour[c], k);
            var cols = (float[])_cols.Clone();
            for (int r = 0; r < DungeonLighting.ColorRows; r++)
                for (int c = 0; c < 3; c++) cols[r * 4 + c] = Lerp(Lerp(_cols[r * 4 + c], _cols[r * 4 + c] * DimKeep, dim), FlashColour[c], k);
            var fog = (float[])_fog.Clone();
            if (_fog[1] > _fog[0])                          // a floor without fog keeps none: the light alone carries the flash there
            {
                fog[0] = Lerp(Lerp(_fog[0], _fog[0] * DimFogPull, dim), FogStart, kf);
                fog[1] = Lerp(Lerp(_fog[1], _fog[1] * DimFogPull, dim), FogEnd, kf);
            }
            var rgb = new byte[3];
            for (int c = 0; c < 3; c++) rgb[c] = (byte)Math.Round(Lerp(Lerp(_fogRgb[c], 0f, dim), FogColour[c], kf));
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
