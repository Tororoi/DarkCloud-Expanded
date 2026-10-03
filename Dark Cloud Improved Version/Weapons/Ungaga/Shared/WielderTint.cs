namespace Dark_Cloud_Improved_Version
{
    /// <summary>The wielder's weapon tinted through the blade lever the Sun Sword uses (<see cref="SolarBlade"/>): Ungaga's weapon
    /// by its mesh frame, or — the sphere's wielder — Super Steve's whole slingshot (<see cref="SuperSteveRig.WeaponModel"/>), as
    /// the Solar Shot whitens it. <paramref name="k"/> is 0 (the weapon's own colour) to 1 (the full <paramref name="tint"/>).</summary>
    internal static class WielderTint
    {
        internal static void Set(float k, string ungagaModelCode, uint ungagaFrame, float[] tint, bool xiao)
        {
            if (xiao) SolarBlade.Set(k, SuperSteveRig.WeaponModel, 0, 0, tint);
            else SolarBlade.Set(k, ungagaModelCode, ungagaFrame, 0, tint);
        }
    }
}
