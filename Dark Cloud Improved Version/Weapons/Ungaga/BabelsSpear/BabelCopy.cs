using System;
using static Dark_Cloud_Improved_Version.BabelsSpear;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What Curse of Babel raises, behind one set of calls: Ungaga's is <see cref="BladeProp"/>'s copy of the equipped spear,
    /// baked to point up; Super Steve's is <see cref="SlingshotProp.SpawnStatue"/>'s upright statue of Super Steve (her slingshot is a
    /// skinned model BladeProp cannot copy) — both in chara slot 3, which the rise and spin caves drive. <see cref="F"/> is the form in
    /// use: its size, where its tip sits, how much of it stands clear of the ground, its beam's size, how its spikes catch, its solid
    /// column. One of the <see cref="BabelsSpear"/> classes, which share their members through using static.</summary>
    internal static class BabelCopy
    {
        /// <summary>What rises: its size, the model's top above its root along the rise axis, how much of the model stands
        /// above the ground once risen (model units), its turn from the wielder's facing as it is placed, the confusion-area
        /// beam's size, how its spikes catch, and the solid column's radius (the spear's shaft ~2 wide at 5× plus room: 8; Super
        /// Steve's base, its feet 0.46 from the centre: 2 at 4×).</summary>
        internal sealed record Form(string Name, float Scale, float TipZ, float ExposedModelZ, float Yaw, float ShockScale,
                                   float SpikeStepDeg, float SpikeShare, bool HandSpikes, float BlockRadius);
        internal static readonly Form SpearForm  = new("spear", 5f, 16.0f, 10f, 0f, 1f, 60f, 1f / 6f, false, 8f);    // six spikes: one passes a point every 60°, a sixth of the attack      // c10w10: −9.6 … 16.0 along its axis; z 6 … 16 out (25.6 long unscaled)
        // c04w13 upright: handle's end −1.85, fork tips +2.49 (4.34 long); all of it out but Steve's black feet, sunk 0.4 (1.6 units at
        // 4×) into the ground; placed along the wielder's facing.
        internal static readonly Form StatueForm = new("Super Steve", 4f, 2.49f, 3.94f, 0f, 0.7f, 180f, 0.5f, true, 2f);   // two hands: one passes a point every 180°, half the attack; column = its feet (0.46 × 4)
        internal static Form F = SpearForm;
        internal static float Scale        => F.Scale;
        internal static float TipZ         => F.TipZ;
        private static float ExposedModelZ => F.ExposedModelZ;
        internal static float Exposed      => ExposedModelZ * Scale;   // …in the world's units, at the copy's size

        private static float _copyAlpha = 1f;
        internal static bool CopySpawn()
            => _xiao ? SlingshotProp.SpawnStatue(Scale, new[] { SpearTint, SpearTint, SpearTint }, 1f)
                     : BladeProp.Spawn(Scale, 0, pointDown: false, pointUp: true);
        internal static void CopyPlace(float x, float h, float y)
        {   // turned relative to the wielder: their facing at the summon plus the form's turn
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY) + F.Yaw;
            if (_xiao) SlingshotProp.PlaceProjectile(x, h, y, yaw); else { BladeProp.Place(x, h, y, yaw); BladeProp.Tint(SpearTint, SpearTint, SpearTint); }
        }
        internal static void CopySetHeight(float h) { if (_xiao) SlingshotProp.SetHeight(h); else BladeProp.SetHeight(h); }
        internal static void CopySetXY(float x, float y) { if (_xiao) SlingshotProp.SetXY(x, y); else BladeProp.SetXY(x, y); }
        internal static void CopyAlpha(float a) { _copyAlpha = a; if (!_xiao) BladeProp.Alpha(a); }
        internal static bool CopyMaintain()
        {
            if (!_xiao) return BladeProp.Maintain();
            SlingshotProp.Maintain(_copyAlpha);                                      // re-asserts the slot every tick, the fade with it
            return SlingshotProp.Active;
        }
        internal static void CopyDespawn() { if (_xiao) SlingshotProp.Despawn(); else BladeProp.Despawn(); }
    }
}
