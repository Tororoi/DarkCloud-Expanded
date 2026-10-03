using System;
using static Dark_Cloud_Improved_Version.BabelsSpear;
using static Dark_Cloud_Improved_Version.BabelCopy;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What the risen copy does to enemies: the strike the moment the rising tip reaches the target's hit sphere
    /// (<see cref="TipStrike"/>: the weapon's attack, thrown from the wielder as hard as the Baselard throws) and the turning spikes
    /// (<see cref="SpinContacts"/>: every SpikeStepDeg of the spin, each live enemy touching the solid column — or, for Super Steve,
    /// within reach of either of Steve's hands — takes SpikeShare× the attack, no-drain-marked). Its planted hits live ShellLifeTicks
    /// (<see cref="RetireShells"/>). One of the <see cref="BabelsSpear"/> classes, which share their members through using static.</summary>
    internal static class BabelSpikes
    {
        private const string Tag = "[BabelsSpear/BabelSpikes] ";
        internal static readonly PlantedHits _shells = new(clearMark: true);   // its hits carry the no-drain mark: zeroed as they are withdrawn

        /// <summary>The strike, the moment the rising tip reaches the target's hit sphere: the tip's height (the blade-fall cave's
        /// running Y plus the tip's reach) against the lowest active body sphere's underside, with the sphere still over the
        /// spear (its edge within StrikeRadius of the axis). A target that has left, or is gone, is never struck.</summary>
        internal static void TipStrike()
        {
            if (_target < 0) { _struck = true; return; }
            if (!Enemies.IsLive(_target)) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {_target} gone before the tip reached it — no strike"); return; }
            float tip = (_risen ? RootHeight(1f) : Memory.ReadFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY)) + TipZ * Scale;
            long a = EnemyAddresses.FloorSlots.SlotAddr(_target, 0), b = BodyCollision.SlotBase(_target), up = EnemyAddresses.CharObjects.PosAddr(_target);
            float ux = Memory.ReadFloat(up), uh = Memory.ReadFloat(up + 4), uy = Memory.ReadFloat(up + 8);
            float lowest = float.MaxValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;                 // not this frame's placement
                lowest = Math.Min(lowest, ch - Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride));
            }
            if (lowest == float.MaxValue) lowest = uh;                                            // no live sphere: its feet
            if (tip < lowest) { if (_risen) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the tip ({tip:F1}) stopped under enemy slot {_target}'s hit sphere ({lowest:F1}) — no strike"); } return; }
            if (EnemyBody.NearestHitSphereEdge(_target, a, _sx, _sy) > StrikeRadius) { if (_risen) { _struck = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {_target} moved off the spear — no strike"); } return; }
            _struck = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the tip ({tip:F1}) reached enemy slot {_target}'s hit sphere ({lowest:F1})");
            Strike(_target);
        }

        /// <summary>One player-hit sphere on the target's body at the weapon's attack, thrown away from the WIELDER as hard as the
        /// Baselard throws (its kick strength and fade).</summary>
        private static void Strike(int slot)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            Hit(slot, 1f, Baselard.KickStrength, "struck", drains: true, kickFrom: (px, ph, py), kickDecay: Baselard.KickDecay);
        }

        /// <summary>One player-hit sphere on an enemy's body: <paramref name="share"/> of the weapon's attack, thrown away from the
        /// spear at <paramref name="kick"/> (0 = no throw). Withdrawn after ShellLifeTicks if the engine did not take it.</summary>
        private static void Hit(int slot, float share, float kick, string what, bool drains, (float x, float h, float y)? kickFrom = null, float kickDecay = KickDecay)
        {
            int attack = Math.Max(1, (int)Math.Round(Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset) * share));
            var (kx, kh, ky) = kickFrom ?? (_sx, _ground, _sy);          // the kick comes from there (the spear, unless named)
            int idx = EnemyHit.TryPlant(slot, attack, StrikeRadius, kick > 0f ? new EnemyHit.Kick(kx, kh, ky, kick, kickDecay) : (EnemyHit.Kick?)null,
                                        mark: drains ? 0 : CodeCaves.NoDrainMark);   // unmarked, the engine bills it; marked, the no-drain cave bills nothing
            if (idx < 0) return;
            _shells.Add(idx, ShellLifeTicks);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what} enemy slot {slot} for {attack} (entry {idx})");
        }

        // ── the turning spikes ──
        private static float SpikeShare   => F.SpikeShare;     // a spike's hit: a share of the weapon's attack (the spear ⅙, Steve's hands ½)
        private static float SpikeStepDeg => F.SpikeStepDeg;   // the turn between passes at any point (the spear's six spikes 60°, Steve's two hands 180°)
        private const float SpikeReach   = 2f;     // a body sphere this far past the spear's solid column counts as touching
        // Steve's hands: the ends of the fork, in the frame of the fork's centre bone eff30 (c04w13: the arms end ±0.96 either
        // side of it along its own z, at the length it sits at), and how near a hit sphere's surface must be to one (3D).
        private static readonly (float x, float y, float z)[] Hands = { (-0.04f, 0.03f, -0.96f), (-0.04f, 0.03f, 0.96f) };
        private const float HandReach = 6f;
        internal static int  _spinTick = -1;

        /// <summary>Every 60° of the risen spear's turn (the spin rate, counted from the moment it stopped rising), each live enemy
        /// whose hit sphere touches the solid column takes a spike's hit.</summary>
        internal static void SpinContacts(double age)
        {
            double turning = age - (EmergeStartSeconds + EmergeSeconds);
            if (turning < 0) return;
            int tick = (int)(turning * SpinDegPerSec / SpikeStepDeg);
            if (tick == _spinTick) return;
            _spinTick = tick;
            var hands = new System.Collections.Generic.List<(float x, float h, float y)>();
            if (F.HandSpikes)
            {
                foreach (var hp in Hands)
                    if (SlingshotProp.MuzzlePointWorld(hp.x, hp.y, hp.z, out float hx, out float hh, out float hy)) hands.Add((hx, hh, hy));
                if (hands.Count == 0) return;                                  // not drawn yet: no hands to catch with
            }
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (F.HandSpikes)
                {
                    bool caught = false;
                    foreach (var hp in hands) if (SphereEdge3D(s, hp) <= HandReach) { caught = true; break; }
                    if (!caught) continue;
                    Hit(s, SpikeShare, Baselard.HalfKickStrength, "caught by Steve's hand", drains: false, kickDecay: Baselard.KickDecay);   // thrown off, half the Baselard's distance
                    continue;
                }
                if (EnemyBody.NearestHitSphereEdge(s, a, _sx, _sy) > BlockRadius + SpikeReach) continue;
                Hit(s, SpikeShare, Baselard.HalfKickStrength, "spike caught", drains: false, kickDecay: Baselard.KickDecay);
            }
        }

        /// <summary>How near <paramref name="p"/> is to an enemy's body in 3D: the least distance from it to the surface of any of
        /// the enemy's active hit spheres (centre distance − radius; negative inside one) — the spheres this frame placed (within 80
        /// of the unit across the ground; stale ones are skipped, as the strike does). MaxValue with none.</summary>
        private static float SphereEdge3D(int slot, (float x, float h, float y) p)
        {
            long b = BodyCollision.SlotBase(slot), up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uy = Memory.ReadFloat(up + 8), best = float.MaxValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > 80f || Math.Abs(cy - uy) > 80f) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                float dx = cx - p.x, dh = ch - p.h, dy = cy - p.y;
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dh * dh + dy * dy) - r);
            }
            return best;
        }

        /// <summary>Planted hits the engine has not consumed within their life are withdrawn.</summary>
        internal static void RetireShells() => _shells.Expire();
    }
}
