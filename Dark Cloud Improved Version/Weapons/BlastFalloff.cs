using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The falloff blast: one player hit entry per live enemy in reach, its damage the <see cref="Falloff"/> step for
    /// that enemy's distance from the blast point (nearest hurt-sphere edge), its kick away from the blast at
    /// <see cref="KickStrength"/> / <see cref="KickDecay"/>, optionally crush-marked through any guard. Big Bang's whirlwind
    /// and judgement blade, the Sword of Zeus's bolt, Hercules' Wrath's strike and the Big Bang shot all plant it; the
    /// planted entries are tracked (<see cref="_shells"/>) and the unconsumed ones withdrawn by <see cref="ExpireShells"/>.
    /// <see cref="LastBlast"/> is where the last one went off, for the flash fired on it.</summary>
    internal static class BlastFalloff
    {
        internal const float KickStrength     = 3.5f;   // the blast's kick; with KickDecay: distance ≈ force²/(2·decay) ≈ 50 units
        internal const float KickDecay        = 0.12f;  // vanilla melee is 1.2 at 0.2, roughly 3.6 units

        // THE BLAST FALLS OFF WITH DISTANCE: the damage step an enemy takes is the innermost radius its distance from the
        // blast is within (outermost first here; PlantFalloff keeps the last match). One hit entry per enemy, centred
        // on its own body, carries that step — an entry is consumed by the first enemy it touches, so the sizing is
        // what makes it that enemy's alone.
        internal static readonly (float radius, float times)[] Falloff = { (50f, 1f), (40f, 2f), (25f, 3f), (10f, 4f) };
        /// <summary>How far the falloff blast reaches (its outermost step).</summary>
        internal static float BlastRadius => Falloff[0].radius;

        /// <summary>Plant the blast at (x, h, y): one hit entry per live enemy in reach, its damage the <see cref="Falloff"/>
        /// step for that enemy's distance (the radii × <paramref name="reachScale"/>, the steps × <paramref name="damageScale"/>),
        /// its kick from the blast × <paramref name="kickScale"/>. <paramref name="noKickSlot"/> is an enemy struck directly
        /// (the Sword of Zeus's bolt), which takes the hit where it stands: a zero-strength kick, the reaction without the
        /// shove. With <paramref name="guardBreak"/> each entry carries CodeCaves.CrushMark: it passes every guard window (the
        /// ISO's guard-crush cave) and — the mark sharing the no-drain mark's high half — bills Ungaga no weapon HP per hit
        /// (Hercules' Wrath's strike, which bills its own once).</summary>
        internal static void PlantFalloff(float x, float h, float y, int noKickSlot = -1, float damageScale = 1f, float kickScale = 1f, float reachScale = 1f, bool guardBreak = false)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int attack = Player.Weapon.GetCurrentWeaponAttack();
            // ONE entry per enemy, centred on its own body and sized to it, so that entry can only be consumed by that
            // enemy and each takes exactly one hit; its damage is the falloff step its distance from the blast falls in.
            int planted = 0, inRange = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                // The distance is to the nearest edge of the enemy's HIT SPHERES (its script's _SET_BODY_COL set, the ones CheckDmg
                // tests): a blast touching any part of a big enemy is on it, however far its root is.
                float dist = EnemyBody.NearestHitSphereEdge(s, a, x, y);
                float times = 0f;
                foreach (var (radius, t) in Falloff) if (dist <= radius * reachScale) times = t;   // steps ordered outermost first: the last match is the innermost
                if (times <= 0f) continue;
                inRange++;
                if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) break;
                int slot = CollisionPool.TakeFreeSlot(pool);
                if (slot < 0) break;
                EnemyBody.BodyCentre(s, a, out float cx, out float ch, out float cy, out float cr);
                byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, cr, Math.Max(1, (int)Math.Round(attack * times * damageScale)), 0);
                CollisionPool.SetKick(e, x, h, y, s == noKickSlot ? 0f : KickStrength * kickScale, KickDecay);   // thrown away from the blast (the struck enemy: the reaction without the shove)
                if (guardBreak) BitConverter.GetBytes(CodeCaves.CrushMark).CopyTo(e, CodeCaves.NoDrainMarkOff);   // no guard stops it (the ISO's guard-crush cave)
                CollisionPool.Plant(pool, slot, e);
                _shells.Add(slot, ShellLifeTicks);
                planted++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BlastFalloff] falloff blast: {planted} sphere(s) on {inRange} enem" + (inRange == 1 ? "y" : "ies") + $" within {Falloff[0].radius * reachScale:F0}");
        }

        private const int ShellLifeTicks = 3;   // ticks an unconsumed entry (a miss) stays before it is withdrawn
        private const int ShellPoolReserve = 16; // free entries the blast always leaves the engine
        // A blast's entries may carry the crush mark, zeroed as they go; one the engine has consumed is done at once (its mark must
        // not ride on into the entry's next use).
        internal static readonly PlantedHits _shells = new(clearMark: true, retireSpent: true);

        /// <summary>Withdraw the hit entries the engine has not consumed (an enemy that moved off its own), and forget those it has.</summary>
        internal static void ExpireShells() => _shells.Expire();

        /// <summary>Where the last blast went off — the flash's own point when it is fired for a landing.</summary>
        internal static (float x, float h, float y) LastBlast { get; set; }   // set by whichever caller plants (the Sword of Zeus: its strike)
    }
}
