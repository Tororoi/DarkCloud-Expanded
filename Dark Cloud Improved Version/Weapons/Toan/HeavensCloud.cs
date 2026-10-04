using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Heaven's Cloud — holding the whirlwind charge grows the blade. <see cref="TyphoonEffect"/> is the thread (WeaponThreads,
    /// while the sword is equipped on a floor). Everything is keyed to a blade <c>factor</c> (1.0 = original): three data-side writes,
    /// no EE-code patch (docs/weapon-reach.md) —
    ///  • the blade mesh "w14" scaled by factor (<see cref="ScaleBlade"/>): the visible blade AND its dcol hit points grow together;
    ///  • the whirlwind effect (c01_fuusya) scaled to match (<see cref="ScaleWhirl"/>: reach = <see cref="StockDcol1Z"/> × factor);
    ///  • enemy body radii inflated, distance-gated, to the same reach (<see cref="ScaleHitbox"/>), so close swings connect and far
    ///    hits never land past the blade.
    /// <see cref="ResetReach"/> snaps all three back. While the sword is equipped this class owns the shared whirl scale
    /// (WhirlwindScale.Tick stands aside for it).</summary>
    internal static class HeavensCloud
    {
        /// <summary>HC commenu dcol1 Z: the stock hit distance from the hand.</summary>
        internal const float StockDcol1Z = 9.2053f;
        private const string ModelCode = "c01w14";   // Heaven's Cloud; its mesh frame is the 'w14' child (see WeaponModelFrames.ResolveBladeFrame)
        private static readonly EnemyHitboxInflation _hitbox = new EnemyHitboxInflation();

        /// <summary>
        /// Heaven's Cloud effect (charge scaling). While the whirlwind is being charged the blade grows over
        /// real time (up to 3x after <c>growSeconds</c>), independent of the polling rate; the whirl visual and
        /// enemy hitboxes are kept matched to the current size the whole time it's charging AND executing, so
        /// they're ready the instant the whirlwind fires. When the charge attack finishes the blade snaps back
        /// to its original size. Scaling goes through <see cref="WeaponModelFrames"/>.
        /// </summary>
        public static void TyphoonEffect()
        {
            const float maxScale = 3.0f;       // blade grows up to 3x
            const double growSeconds = 4.0;    // play time to grow from 1x to maxScale
            // Growth CURVE. factor = 1 + (maxScale-1) * t^growExponent, t = 0..1 over growSeconds. An exponent
            // > 1 makes the curve convex: the blade creeps at the start of the hold and accelerates into the
            // last second, so committing to a long charge feels like it pays off. Total duration is unchanged —
            // only the shape is. (1.0 = the old linear ramp; raise it for a later, sharper surge.)
            const float growExponent = 2.5f;
            DateTime growStart = DateTime.MinValue;   // when this windup began, on the play clock — so a hold does not grow the blade
            float factor = 1.0f;
            bool active = false;               // a non-base scale is applied and still needs resetting
            bool charging = false;             // in a whirlwind windup
            bool flashedMax = false;           // max-size flash already fired for THIS charge
            bool warnedNoBlade = false;

            while (Player.Weapon.GetCurrentWeaponId() == Items.heavenscloud &&
                   Player.InDungeonFloor())
            {
                if (IsChargingWhirlwind())      // whirlwind charge specifically → grow the blade over time
                {
                    // The game's charge freezes while held, and so does the ramp: growStart is on GameClock. Keep
                    // `charging` set so the resume is not mistaken for a fresh charge.
                    if (Player.CheckDunIsPausedOrMenu()) { Thread.Sleep(30); continue; }

                    if (!charging)                                   // whirlwind charge just began
                    {
                        charging = true; flashedMax = false;
                        growStart = GameClock.Now;
                    }

                    float t = (float)Math.Min(1.0, (GameClock.Now - growStart).TotalSeconds / growSeconds);
                    factor = 1.0f + (float)Math.Pow(t, growExponent) * (maxScale - 1.0f);
                    active = true;

                    // Hit full size → flash Toan once, the same engine ambient-pulse that marks Ruby's Mobius
                    // charge peaking. It's the only feedback that the blade has stopped growing and holding
                    // longer buys nothing.
                    if (t >= 1.0f && !flashedMax)
                    {
                        flashedMax = true;
                        Player.FlashChargeComplete();
                    }
                }
                else if (IsWhirlwindActive())   // whirlwind executing → hold the size reached during the charge
                {
                    charging = false;
                    active = true;
                }
                else if (active)                        // charge finished → snap the blade back to its original size
                {
                    charging = false;
                    factor = 1.0f;
                    ResetReach();
                    active = false;
                }

                // Keep blade + whirl + enemy hitboxes matched to the current size for the whole charge→whirlwind
                // window: pre-scaled while charging (enemies are already inflated and the fuusya pool is already
                // sized when the whirlwind lands) and maintained while it executes (the effect re-poses its root
                // on each cast, so the scale must be re-applied while it's live).
                if (active)
                {
                    // If the blade frame won't resolve, say so ONCE and dump the tree — the model's frame names
                    // are not guessable (HC's mesh is a 'w14' child; the Kitchen Knife's is its root).
                    if (!ScaleBlade(factor) && !warnedNoBlade)
                    {
                        warnedNoBlade = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            "[HeavensCloud] blade mesh not located — dumping the model's frame tree:");
                        WeaponModelFrames.DumpTree();
                    }
                    ScaleWhirl(factor);
                    ScaleHitbox(factor + 2.0f);
                }

                Thread.Sleep(30);
            }

            ResetReach();   // unequipped → make sure everything is back to normal
        }

        // ── reach control (data-side; driven by TyphoonEffect's charge ramp) ──

        /// <summary>Scale Heaven's Cloud's blade (visible blade + dcol hit points) to <paramref name="factor"/>× through
        /// <see cref="WeaponModelFrames.ScaleBlade"/> (its mesh frame is the 'w14' child). Returns false until the frame is located
        /// (model not loaded yet).</summary>
        public static bool ScaleBlade(float factor) => WeaponModelFrames.ScaleBlade(ModelCode, factor);

        /// <summary>The whirlwind visual sized to a blade <paramref name="factor"/> (reach = stock Z × factor) and re-applied to the fuusya roots.</summary>
        public static void ScaleWhirl(float factor) => WhirlwindScale.SizeTo(StockDcol1Z * factor);

        /// <summary>Enemy body hitboxes inflated (distance-gated) to match a blade <paramref name="factor"/> (reach = stock Z × factor).</summary>
        public static void ScaleHitbox(float factor) => MaintainEnemyHitbox(StockDcol1Z * factor);

        /// <summary>Snap Heaven's Cloud back to original: blade 1×, enemy hitboxes restored, whirl at base scale and handed back to
        /// WhirlwindScale for whatever is equipped next.</summary>
        public static void ResetReach()
        {
            WeaponModelFrames.ScaleBlade(ModelCode, 1.0f);
            _hitbox.Restore();
            ScaleWhirl(1.0f);
            WhirlwindScale.ForgetWeapon();   // force its tick to recompute the whirl scale for whatever is equipped next
        }

        /// <summary>True while the player is winding up a WHIRLWIND charge (action 0xE at whirlwind charge
        /// level 2 — i.e. the meter has passed the whirlwind threshold and it's unlocked). The lunge-level
        /// windup (level 0/1) is excluded, so the blade only grows once the whirlwind charge specifically begins.</summary>
        public static bool IsChargingWhirlwind()
            => Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWindup
            && Memory.ReadInt(PlayerAction.ChargeLevel) == PlayerAction.ChargeLevelWhirl;

        /// <summary>True while the whirlwind attack is actually executing (action 0x18 with the charge-active
        /// flag still set). The flag clears on the final whirlwind frame, so this goes false immediately when the
        /// attack finishes — even though the action state lingers at 0x18 for a frame.</summary>
        public static bool IsWhirlwindActive()
            => Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWhirlwind
            && Memory.ReadInt(PlayerAction.ChargeActiveFlag) == 1;

        // Distance-gated enemy hitbox: for each active enemy, if its horizontal distance to the player is within
        // the range gate, ADD the reach to its body radii so close swings connect; otherwise it is held at its
        // cached stock value so far hits never land past the blade (EnemyHitboxInflation does the snapshot, the
        // writes and the restore). The whirlwind charge sweeps a wide arc, so its range gate is slightly wider
        // than the combo's and the lunge's. The enemy slot carries both its own world position (LocationX/Y) and
        // the player's (TargetX/Y mirrors player dunPosition each frame), so the distance is read entirely from
        // the slot.
        private static void MaintainEnemyHitbox(float reach)
        {
            bool whirl = Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWhirlwind;
            float gateSq = whirl ? reach * reach + 5.0f : reach * reach + 2.8f; // squared distance to player for the bonus to apply
            _hitbox.Maintain(reach, slot =>
            {
                float dx = Memory.ReadFloat(slot + EnemySlotOffsets.LocationX) - Memory.ReadFloat(slot + EnemySlotOffsets.TargetX);
                float dy = Memory.ReadFloat(slot + EnemySlotOffsets.LocationY) - Memory.ReadFloat(slot + EnemySlotOffsets.TargetY);
                return dx * dx + dy * dy <= gateSq;
            });
        }
    }
}
