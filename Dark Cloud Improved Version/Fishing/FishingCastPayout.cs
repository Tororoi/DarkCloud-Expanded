using System;
using static Dark_Cloud_Improved_Version.CustomFishingSpot;
using static Dark_Cloud_Improved_Version.FishingRope;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The cast pay-out: stage the Verlet rope's aerial rest length (distpAbove) like a real cast — sling at the
    /// start scale, then ramp it out under the flying bobber once the rod-tip wind-up + forward fling is seen,
    /// amplifying the flight; snap back on reel-in / uncast. Pure per-tick memory math over the rope arrays.
    /// </summary>
    internal static class FishingCastPayout
    {
        // Rope arrays: FishingAddresses.FishingRope (point/ukip/ukiv/hookv), resolved from the SCUS_971.11 symtab.
        private static bool  _distpScaled;   // is the shared line rest-length currently stretched for this spot?
        private static float _facing;        // the player's LIVE yaw this tick (forward = (sin f, cos f)) — PointFrontDist's axis

        private const float CastRodTipFront   = 2f;    // rod tip this far FORWARD OF ITS REST position (post-wind-up) -> start the pay-out
        private const float CastWindupThresh  = -5f;   // rod tip must swing this far BEHIND rest first (the wind-up) to arm the trigger
        private static float _rodRestFront;            // rod-tip front-distance at rest (baselined while not casting)
        private static bool  _castWoundUp;             // the wind-up pull-back has happened this cast -> forward-fling trigger is armed
        private const float CastRetractDist   = 6f;    // bobber back within this of the rod (uncast pull-in) -> snap the paid-out line to start
        private const float CastPayoutRate    = 0.25f; // distpAbove growth per tick while paying out (1.667->5.0 in ~13 ticks — ahead of the flight)
        private const float CastCarryFactor   = 1.16f; // per-tick horizontal velocity multiplier during the pay-out window (~2.7x over the ramp)

        /// <summary>In-flight carry: multiply the bobber + hook HORIZONTAL velocities (Y untouched — gravity
        /// owns the arc). Runs only inside the pay-out window, so it can never touch settled/reeled states.</summary>
        private static void AmplifyFlight(float f)
        {
            for (int i = 0; i < 4; i++) ScaleVeloXZ(FishingRope.Ukiv  + i * 0x10, f);
            for (int i = 0; i < 3; i++) ScaleVeloXZ(FishingRope.Hookv + i * 0x10, f);
        }
        private static void ScaleVeloXZ(long addr, float f)
        {
            Memory.WriteFloat(addr + 0, Memory.ReadFloat(addr + 0) * f);
            Memory.WriteFloat(addr + 8, Memory.ReadFloat(addr + 8) * f);
        }

        /// <summary>Horizontal rod-tip -> bobber distance (diagnostic; direction-blind).</summary>
        private static float BobberOutDist() { ComputeCastDir(out float d); return d; }

        /// <summary>Full 3D rod-tip → bobber distance — the pay-out's "slung" magnitude. 3D on purpose:
        /// a high-bank cast throws steeply DOWN (large drop, small horizontal), and the horizontal-only
        /// version never saw it as slung.</summary>
        private static float BobberSlungDist()
        {
            float dx = Memory.ReadFloat(FishingRope.Ukip + 0) - Memory.ReadFloat(FishingRope.Point + 0);
            float dy = Memory.ReadFloat(FishingRope.Ukip + 4) - Memory.ReadFloat(FishingRope.Point + 4);
            float dz = Memory.ReadFloat(FishingRope.Ukip + 8) - Memory.ReadFloat(FishingRope.Point + 8);
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static bool _payingOut;   // TEMP: payout start/end log edge

        /// <summary>Signed distance of the ROD TIP (point[0], where the line connects) in front of the player.
        /// The rod tip is animation-driven and swings forward MONOTONICALLY on the cast — unlike the bobber,
        /// which whips wildly and can read as "flung" the instant the cast starts — so it's the reliable
        /// pay-out trigger.</summary>
        private static float RodTipFrontDist() => PointFrontDist(FishingRope.Point);

        /// <summary>Signed distance of a rope point in front of the player, along the PLAYER'S FACING
        /// (the LIVE yaw fed to Tick each tick — the player can turn before casting; forward = (sin f, cos f)).
        /// ⚠ This used to project along the CAMERA-forward axis (angS), which INVERTS when the follow
        /// camera ends up in front of the player — the wind-up/fling gates then read backwards and the
        /// cast pay-out silently never fired ("line doesn't lengthen when the camera is in front").
        /// The rest-baseline delta logic above is unaffected: only the axis changed.</summary>
        private static float PointFrontDist(long ropePoint)
        {
            if (!EditLoop.TryReadPlayerPos(out float px, out _, out float pz)) return 0f;
            float f = _facing;
            float dx = Memory.ReadFloat(ropePoint + 0) - px, dz = Memory.ReadFloat(ropePoint + 8) - pz;
            return dx * (float)Math.Sin(f) + dz * (float)Math.Cos(f);
        }

        /// <summary>Horizontal cast direction = normalize(bobber - rod-tip) in XZ (the throw already aimed it);
        /// <paramref name="outDist"/> = the un-normalized horizontal distance (how far the bobber is cast out front).</summary>
        private static (float x, float y, float z) ComputeCastDir(out float outDist)
        {
            float dx = Memory.ReadFloat(FishingRope.Ukip + 0) - Memory.ReadFloat(FishingRope.Point + 0);
            float dz = Memory.ReadFloat(FishingRope.Ukip + 8) - Memory.ReadFloat(FishingRope.Point + 8);
            outDist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (outDist < 0.01f) return (0f, 0f, 0f);
            return (dx / outDist, 0f, dz / outDist);
        }

        /// <summary>Per-tick line LENGTH + cast pay-out (see the comment block inside). <paramref name="aboveStart"/> /
        /// <paramref name="above"/> are the session-resolved distpAbove scales (LineConfigSplit), <paramref name="facing"/>
        /// the player's LIVE yaw (the stance yaw only as a fallback — the player can turn before casting, and a stale
        /// facing inverted the wind-up/fling gates the same way the old camera-axis projection did).
        /// Restores the vanilla rest length the moment the session ends.</summary>
        internal static void Tick(bool live, float aboveStart, float above, float facing)
        {
            _facing = facing;
            if (live)
            {
                float start  = FishLineShallow.VanillaDistp * aboveStart;
                float target = FishLineShallow.VanillaDistp * above;
                float cur = Memory.ReadFloat(FishLineShallow.DistpAddr);
                int cf = Memory.ReadInt(FishingAddresses.FishCatchConfirm);   // chara_fishing @0x202A26E8 (1=walk, 2=cast trigger, 3=throw, 4=waiting…)
                // The rod tip is held out in front the WHOLE time, so its absolute front-distance is > 2 even at
                // rest. Baseline it: while not casting (cf < 3) the rod is at rest, so keep _rodRestFront = the
                // current rod-tip front-distance. During the cast, how far the tip has swung FORWARD OF REST is
                // the real signal (negative during the wind-up pull-back, positive on the forward fling).
                if (cf < 3) { _rodRestFront = RodTipFrontDist(); _castWoundUp = false; }
                float rodFwd = RodTipFrontDist() - _rodRestFront;
                // The cast has a tiny forward BLIP (fwd ~0->3) at the very start, BEFORE the wind-up pull-back —
                // that blip was false-triggering the payout. So require the wind-up FIRST: only arm once the rod
                // tip has swung strongly BEHIND rest (fwd < CastWindupThresh). The real forward fling only comes
                // after that, so it's the reliable precursor (the "pull back" you described). Blip: max ~3, no
                // wind-up yet -> not armed. Real casts wind up to -9..-21, well past the -5 gate.
                if (cf >= 3 && rodFwd < CastWindupThresh) _castWoundUp = true;
                if (cf == 3 && Diagnostics) Log($"[cast-rod] fwd={rodFwd:0.##} wound={_castWoundUp} cur={cur:0.##}");
                if (cf < 3 || cur > target)
                    cur = cf < 3 ? start : target;                            // reeled in (or target shrank): snap
                // RETRACT on the uncast pull-in: chara_fishing stays >= 3 through the whole uncast animation,
                // so the state test alone left the line at full length until the animation finished (and
                // sometimes a re-cast beat it there). The bobber coming back NEAR the rod is the reliable
                // signal — when it's within CastRetractDist with the line still paid out, snap back to start.
                // A fresh cast is unaffected: pre-whip the bobber is near the rod but cur == start already.
                else if (cur > start + 0.01f && BobberSlungDist() < CastRetractDist)
                    cur = start;
                // Trigger on the rod tip swinging forward of rest by CastRodTipFront, but ONLY after the
                // wind-up armed it (_castWoundUp) — that rejects the pre-wind-up blip. Once started, keep paying.
                else if (cur < target && ((_castWoundUp && rodFwd > CastRodTipFront) || cur > start + 0.01f))
                {
                    cur = Math.Min(cur + CastPayoutRate, target);             // flung forward: pay out (and keep paying once started)
                    // IN-FLIGHT CARRY: the payout only stops the line BRAKING the bobber — release velocity is
                    // still vanilla, so range would be ~vanilla too. While paying out (= the flight window),
                    // amplify the bobber/hook horizontal velocity multiplicatively: it follows its own arc (no
                    // direction guessing), compounds to a genuinely longer throw, and ends with the payout.
                    AmplifyFlight(CastCarryFactor);
                }
                bool paying = cur > start + 0.01f && cur < target;
                if (paying != _payingOut)
                { if (Diagnostics) Log($"[payout] {(paying ? $"START (rodtip {RodTipFrontDist():0.#}, out {BobberOutDist():0.#})" : $"end (distp {cur:0.##}, out {BobberOutDist():0.#})")}"); _payingOut = paying; }
                Memory.WriteFloat(FishLineShallow.DistpAddr, cur);
                _distpScaled = true;
            }
            else if (_distpScaled)
            {
                Memory.WriteFloat(FishLineShallow.DistpAddr, FishLineShallow.VanillaDistp);
                _distpScaled = false;
            }
        }

        /// <summary>Map change / install reset: put the shared rest length back to vanilla if this spot stretched it.</summary>
        internal static void Reset()
        {
            if (_distpScaled) { Memory.WriteFloat(FishLineShallow.DistpAddr, FishLineShallow.VanillaDistp); _distpScaled = false; }
        }
    }
}
