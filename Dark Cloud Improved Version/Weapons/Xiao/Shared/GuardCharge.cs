using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>One frame of a Super Steve sphere's GUARD charge (BigBangShot, ZeusShot, SolarShot), <paramref name="held"/> seconds
    /// into a charge of <paramref name="seconds"/>: the slingshot whitened by that fraction, the room dimmed toward the profile's
    /// prime dim (profiles with none skip the dim), and the stock cyan build-up ramped to the time left.</summary>
    internal static class GuardCharge
    {
        internal static void Frame(SunSword.SolarProfile p, double held, double seconds)
        {
            SolarBlade.Set((float)(held / seconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
            if (p.PrimeDim > 0f) { SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / seconds)); }
            ChargeTint.Ramp(seconds - held);
        }
    }
}
