using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Enemy body-sphere inflation: every live enemy's body radii held at stock + a bonus (for those an optional range test
    /// admits; at stock for the rest), and all of them put back on <see cref="Restore"/>. An ability whose hit reaches farther than the
    /// engine's own sphere — a scaled blade, a grown orb — makes its targets bigger instead: hit ⇔ dist &lt; hitR + bodyR, so adding to
    /// bodyR is the same as adding to the hit, with none of the side effects of touching the engine's collision words. Each part's
    /// stock radius is captured the first time it is seen (a part with no real sphere — ≤ 0.01 or ≥ 1000 — is skipped) and forgotten
    /// when its slot empties, so the next occupant is captured from its own stock; a radius within 0.05 of what is wanted is left
    /// alone. One instance per ability, each with its own snapshot: HeavensCloud (distance-gated, through the whirlwind charge) and
    /// MobiusRing (ungated, while the orbs fly). Claymore keeps its own pass: its gate is the body EDGE (the largest live sphere) and
    /// it re-captures a radius something else rewrote under it.</summary>
    internal sealed class EnemyHitboxInflation
    {
        private readonly float[,] _stock = new float[EnemyAddresses.FloorSlots.Count, BodyCollision.MaxBodyParts];   // 0 = not captured
        private bool _active;   // a Maintain has run since the last Restore

        /// <summary>One pass over the floor slots: each live enemy's parts at stock + <paramref name="bonus"/> when
        /// <paramref name="inRange"/> (given the slot's address) says so — always, when null — and at stock otherwise.</summary>
        internal void Maintain(float bonus, Func<long, bool> inRange = null)
        {
            _active = true;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long slot = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slot + EnemySlotOffsets.RenderStatus) <= 0)   // empty slot → forget cached stock
                {
                    for (int p = 0; p < BodyCollision.MaxBodyParts; p++) _stock[s, p] = 0f;
                    continue;
                }
                bool close = inRange == null || inRange(slot);
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    long addr = BodyCollision.RadiusAddr(s, p);
                    float r = Memory.ReadFloat(addr);
                    float orig = _stock[s, p];
                    if (orig <= 0.01f)                                   // capture stock the first time we see it
                    {
                        if (r <= 0.01f || r >= 1000f) continue;         // no real hitbox on this part
                        orig = _stock[s, p] = r;
                    }
                    float want = close ? orig + bonus : orig;            // inflate while close, else stock
                    if (Math.Abs(r - want) > 0.05f) Memory.WriteFloat(addr, want);
                }
            }
        }

        /// <summary>Every captured radius back at stock and forgotten; nothing when no Maintain has run since the last Restore.</summary>
        internal void Restore()
        {
            if (!_active) return;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                    if (_stock[s, p] > 0.01f)
                    {
                        Memory.WriteFloat(BodyCollision.RadiusAddr(s, p), _stock[s, p]);
                        _stock[s, p] = 0f;
                    }
            _active = false;
        }
    }
}
