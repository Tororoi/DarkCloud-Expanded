using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Spawn a ring of REAL pellets fanning out from a point along the ground plane — a "shrapnel" burst.
    ///
    /// A general capability, not tied to any one weapon: it borrows the player's own CSHOT pellet pool, so each
    /// fragment is a genuine pellet with collision ENABLED. They deal real weapon damage and real hit reactions
    /// to whatever they fly into — the game's own hitbox does the splash, and no HP is ever faked.
    ///
    /// Hard-won details, both of which will silently ruin the effect if ignored:
    ///   • Fragments MUST spawn OUTSIDE the origin enemy's hitbox (2.0 + its part radius, checkCollision
    ///     0x1AB740), or they collide with it on frame 1 and die before moving. Big enemies have radii ~4-8;
    ///     <see cref="DefaultStartOffset"/> also stops a stray fragment re-clipping the origin for a double-hit.
    ///   • Movement and collision share ONE block in step__5CSHOT (pos += vel only runs when +0x280 == 0), so a
    ///     pass-through (non-colliding) fragment cannot move. Pass-through is therefore a visibility-debug aid
    ///     only, never a way to make the burst harmless.
    ///
    /// The caller owns the returned slot list: while any of those slots is still flagged live the burst is
    /// airborne, which is how callers avoid mistaking a fragment's own kill for a fresh primary hit.
    /// </summary>
    internal static class ShrapnelBurst
    {
        internal const int   DefaultCount       = 8;      // fragments per burst (8 compass directions)
        internal const float DefaultSpeed       = 2.5f;   // outward units/frame (a normal pellet runs ~3.5-5.0)
        internal const float DefaultStartOffset = 16f;    // spawn this far out from the origin point (see above)
        internal const int   DefaultLifetime    = 0x40;   // frames a fragment lives → reach ≈ speed × this
        internal const float DefaultScale       = 1f;     // fragment sprite scale (+0x310) — normal pellet size

        /// <summary>Fan <paramref name="count"/> pellets out from (<paramref name="x"/>, <paramref name="height"/>,
        /// <paramref name="y"/>) in the ground plane, each carrying <paramref name="damage"/> as its ATTACK value
        /// (+0x2E0 — the enemy's defense is applied when it lands, so pass a raw attack figure, NOT a post-defense
        /// HP delta). Returns the pool slots taken; fires as many as the pool has room for.
        ///
        /// <paramref name="passThrough"/> disables collision, which also freezes the fragments in place (see the
        /// class remarks) — debug only.</summary>
        internal static List<int> Spawn(long poolBase, float x, float height, float y, int damage,
                                        int count = DefaultCount, float speed = DefaultSpeed,
                                        float startOffset = DefaultStartOffset, int lifetime = DefaultLifetime,
                                        float scale = DefaultScale, bool passThrough = false)
        {
            var taken = new List<int>();
            if (damage <= 0 || count <= 0) return taken;

            for (int i = 0; i < count; i++)
            {
                int slot = FreeSlot(poolBase);
                if (slot < 0) break;   // pool full — fire as many as we can

                double ang = i * (2.0 * System.Math.PI / count);
                float dx = (float)System.Math.Cos(ang), dy = (float)System.Math.Sin(ang);   // ground-plane unit dir

                long pos = PlayerShotPool.PosAddr(poolBase, slot);
                Memory.WriteFloat(pos,     x + dx * startOffset);
                Memory.WriteFloat(pos + 4, height);                    // same height as the origin
                Memory.WriteFloat(pos + 8, y + dy * startOffset);

                long vel = PlayerShotPool.VelAddr(poolBase, slot);
                Memory.WriteFloat(vel,     dx * speed);
                Memory.WriteFloat(vel + 4, 0f);                        // no vertical drift
                Memory.WriteFloat(vel + 8, dy * speed);

                Memory.WriteInt(PlayerShotPool.NoCollideAddr(poolBase, slot), passThrough ? 1 : 0);
                Memory.WriteInt(poolBase + PlayerShotPool.LifetimeOffset +
                                slot * PlayerShotPool.ScalarStride, lifetime);
                Memory.WriteInt(PlayerShotPool.DamageAddr(poolBase, slot), damage);
                Memory.WriteFloat(PlayerShotPool.ScaleAddr(poolBase, slot), scale);
                Memory.WriteInt(PlayerShotPool.FlagAddr(poolBase, slot), 1);   // live LAST
                taken.Add(slot);
            }
            return taken;
        }

        /// <summary>True while any of <paramref name="slots"/> is still flagged live. Callers use this to tell a
        /// fragment's own hit apart from a fresh primary hit; it also prunes the list in place.</summary>
        internal static bool Airborne(long poolBase, List<int> slots)
        {
            slots.RemoveAll(s => Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, s)) == 0);
            return slots.Count > 0;
        }

        private static int FreeSlot(long poolBase)
        {
            for (int j = 0; j < PlayerShotPool.SlotCount; j++)
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(poolBase, j)) == 0) return j;
            return -1;
        }
    }
}
