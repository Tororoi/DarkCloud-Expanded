using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Xiao's SHOT charge, as her charged shots are made: the shot held (drawn or holding) for a charge time, the
    /// stock charge-complete flash as it is reached, and <see cref="Charged"/> left standing once she lets go so the next
    /// pellet is the charged one (the ability clears it as that pellet is taken). The ability's charge tint is ramped over the
    /// hold and cleared on the release unless the ability asks otherwise (its own guard charge owns the tint then). Also the
    /// shoot state's edge — <see cref="Released"/> on its first tick, before the engine has a pellet out — and
    /// <see cref="RetirePellet"/>, the ability's mark that the shot under way is to have its pellet retired as it appears, dropped
    /// on its own once the shoot state has gone by. One per ability; ticked every tick the ability is active.</summary>
    internal sealed class ShotCharge
    {
        private DateTime _holdStart;
        private bool _wasShooting;

        /// <summary>The shot is held this tick (its draw or its hold state).</summary>
        internal bool Holding { get; private set; }
        /// <summary>Seconds the current hold has run (the last tick's value once let go).</summary>
        internal double Held { get; private set; }
        /// <summary>The hold reached the charge time; stays up through the release until the ability takes it for the pellet.</summary>
        internal bool Charged;
        /// <summary>The shoot state is on this tick.</summary>
        internal bool Shooting { get; private set; }
        /// <summary>The shoot state's first tick: her release.</summary>
        internal bool Released { get; private set; }
        /// <summary>The pellet of the shot under way is to be retired the tick it appears; cleared whenever the shoot state is off.</summary>
        internal bool RetirePellet;

        /// <summary>One tick: the hold, the charge, and the shoot edge. <paramref name="rampTint"/> false leaves ChargeTint
        /// alone this tick (no ramp while holding, no clear on the release).</summary>
        internal void Tick(double chargeSeconds, bool rampTint)
        {
            int state = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = state == PlayerAction.XiaoShotDraw || state == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!Holding) { Holding = true; Charged = false; _holdStart = GameClock.Now; }
                Held = (GameClock.Now - _holdStart).TotalSeconds;
                if (!Charged && Held >= chargeSeconds) { Charged = true; Player.FlashChargeComplete(); }
                if (rampTint) ChargeTint.Ramp(Charged ? 0 : chargeSeconds - Held);
            }
            else
            {
                if (Holding && rampTint) ChargeTint.Clear();                                   // released: Charged stays for the pellet
                Holding = false;
            }
            Shooting = state == PlayerAction.XiaoShotShoot; Released = Shooting && !_wasShooting;
            _wasShooting = Shooting;
            if (!Shooting) RetirePellet = false;                                                // the shot went by without a pellet
        }

        internal void Reset()
        {
            Holding = false; Charged = false; Shooting = false; Released = false; RetirePellet = false; _wasShooting = false; Held = 0;
        }
    }
}
