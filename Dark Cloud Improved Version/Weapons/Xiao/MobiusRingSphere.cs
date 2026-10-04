using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Mobius Ring sphere — Ruby's ramp from Xiao's slingshot. Ruby ramps while CHARGING her ball;
    /// Xiao ramps while HOLDING the drawn shot (states 0xB/0xC). Every <see cref="CycleSeconds"/> held the damage multiplier
    /// compounds ×<see cref="StepMultiplier"/> (Ruby's damage += damage/2 per flash cycle) and Xiao flashes, as Ruby does. The
    /// fired pellet takes damage ×1.5^cycles (capped at Ruby's 65535) and its sprite grows with the multiplier by Ruby's own
    /// ball-growth formula (<see cref="CustomRubyEffects.RubyBallGrowthPerMultiple"/>), capped at <see cref="PelletMaxScale"/>.
    /// The ramp freezes on release so the pellet that fires reads it, and resets on a fresh hold or when the sphere goes.
    /// Driven from Super Steve's sphere dispatch.</summary>
    internal static class MobiusRingSphere
    {
        private const string Tag = "[MobiusRingSphere] ";
        private const double CycleSeconds   = 1.5;    // hold time per ramp step (Ruby's flash cadence)
        private const float  StepMultiplier = 1.5f;   // damage multiplier per completed cycle, compounding
        private const int    DamageCap      = ushort.MaxValue;   // Ruby's ramp cap
        private const float  PelletMaxScale = 15f;    // sprite-size cap — Ruby's ball caps at 5× but a pellet is tiny, so Xiao gets 15×
        private static bool     _holding;
        private static DateTime _holdStart;
        private static int      _cycles;   // completed ramp cycles (frozen on release for the pellet that fires)
        private static readonly bool[] _pelletHandled = new bool[PlayerShotPool.SlotCount];

        /// <summary>Mobius Ring: while <paramref name="active"/>, holding the shot ramps a compounding damage
        /// multiplier (one ×<see cref="StepMultiplier"/> step + a Ruby-style flash per
        /// <see cref="CycleSeconds"/> held); each fired pellet gets the ramped damage and a ball-growth
        /// sprite scale. The ramp freezes on release (so the pellet that fires reads it) and resets when a
        /// fresh hold starts, or when the sphere is swapped.</summary>
        internal static void Drive(bool active)
        {
            if (!active)
            {
                _holding = false; _cycles = 0;
                Array.Clear(_pelletHandled, 0, _pelletHandled.Length);
                return;
            }

            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!_holding) { _holdStart = GameClock.Now; _holding = true; _cycles = 0; }
                int cycles = (int)((GameClock.Now - _holdStart).TotalSeconds / CycleSeconds);
                if (cycles > _cycles)
                {
                    _cycles = cycles;
                    Player.FlashChargeComplete();   // Ruby's Mobius flash per ramp step
                }
                ChargedShotWhp.Arm(_cycles >= 1 ? ChargedShotWhp.ChargedFactor : 1f);                                  // charged from the first ramp step
                ChargeTint.Ramp(CycleSeconds * (_cycles + 1) - (GameClock.Now - _holdStart).TotalSeconds);           // a ramp into every step's flash
            }
            else { _holding = false; ChargeTint.Clear(); }   // keep _cycles frozen for the pellet that fires

            // Stamp fresh pellets once each: damage ×1.5^cycles (capped) + Ruby's ball-growth sprite scale.
            long poolBase = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(poolBase)) return;   // pool not allocated / bad pointer
            float mult = (float)Math.Pow(StepMultiplier, _cycles);
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if (live && !_pelletHandled[i])
                {
                    if (_cycles > 0)
                    {
                        long dmgA = PlayerShotPool.DamageAddr(poolBase, i);
                        Memory.WriteInt(dmgA, (int)Math.Min(DamageCap, Memory.ReadInt(dmgA) * (double)mult));
                        float scale = 1f + (mult - 1f) * CustomRubyEffects.RubyBallGrowthPerMultiple;
                        if (scale > PelletMaxScale) scale = PelletMaxScale;
                        Memory.WriteFloat(PlayerShotPool.ScaleAddr(poolBase, i), scale);
                    }
                    _pelletHandled[i] = true;
                }
                else if (!live) _pelletHandled[i] = false;
            }
        }
    }
}
