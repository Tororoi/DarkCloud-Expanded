using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Heaven's Cloud — holding the whirlwind charge grows the blade.</summary>
    internal static class HeavensCloud
    {
        // ── Heaven's Cloud ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Heaven's Cloud effect (charge scaling). While the whirlwind is being charged the blade grows over
        /// real time (up to 3x after <c>growSeconds</c>), independent of the polling rate; the whirl visual and
        /// enemy hitboxes are kept matched to the current size the whole time it's charging AND executing, so
        /// they're ready the instant the whirlwind fires. When the charge attack finishes the blade snaps back
        /// to its original size. Scaling reuses the helpers in <see cref="Weapons"/>.
        /// </summary>
        public static void HeavensCloudEffect()
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
                if (Weapons.IsChargingWhirlwind())      // whirlwind charge specifically → grow the blade over time
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
                else if (Weapons.IsWhirlwindActive())   // whirlwind executing → hold the size reached during the charge
                {
                    charging = false;
                    active = true;
                }
                else if (active)                        // charge finished → snap the blade back to its original size
                {
                    charging = false;
                    factor = 1.0f;
                    Weapons.ResetHeavensCloudReach();
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
                    if (!Weapons.ScaleHeavensCloudBlade(factor) && !warnedNoBlade)
                    {
                        warnedNoBlade = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            "[HeavensCloud] blade mesh not located — dumping the model's frame tree:");
                        Weapons.DumpWeaponFrameTree();
                    }
                    Weapons.ScaleHeavensCloudWhirl(factor);
                    Weapons.ScaleHeavensCloudHitbox(factor + 2.0f);
                }

                Thread.Sleep(30);
            }

            Weapons.ResetHeavensCloudReach();   // unequipped → make sure everything is back to normal
        }
    }
}
