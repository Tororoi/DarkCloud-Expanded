using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.TerraSword;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The boulder's damage: the blast where it lands (<see cref="Blast"/>: every live enemy whose hit spheres come within
    /// BlastRadius of the spot, BlastShare× the attack, thrown away from it) and the pinned hits while it rests (<see cref="StuckHits"/>).
    /// Every hit is a crush-marked player-hit entry (<see cref="Shell"/>) that passes any guard and bills no weapon HP; a watched one
    /// that left its enemy's HP as it was is planted again (<see cref="RetireShells"/>), its first miss diagnosed. The nut's bonk plants
    /// its shell here too. One of the <see cref="TerraSword"/> classes, which share their members through using static.</summary>
    internal static class RockImpact
    {
        private const string Tag = "[TerraSword/RockImpact] ";
        /// <summary>A planted hit entry. A WATCHED one (Slot ≥ 0: the impact's and the bonk's) that leaves its enemy's HP as it was —
        /// consumed for nothing or never taken — is planted again (Replant), up to <see cref="HitTries"/> times, its first miss logged
        /// with what CheckDmg would have seen.</summary>
        internal sealed class Shell { public int Idx, Ticks, Slot = -1, Hp, Tries; public Func<Shell, bool> Replant; public string What; }
        internal static readonly List<Shell> _shells = new();
        private const int    HitTries        = 4;                   // plants of a watched hit before it is given up

        /// <summary>Every live enemy whose hit spheres come within BlastRadius of the spot: BlastShare× the attack, thrown away from it.</summary>
        internal static void Blast()
        {
            int planted = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                if (EnemyBody.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), _x, _y) > BlastRadius) continue;
                if (Hit(s, BlastShare, KickStrength, "crushed")) planted++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"blast: {planted} enem{(planted == 1 ? "y" : "ies")} within {BlastRadius:F0}");
        }

        /// <summary>Every live enemy whose root is inside the resting rock: StuckShare× the attack, no throw.</summary>
        internal static void StuckHits()
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - _x, dy = Memory.ReadFloat(p + 8) - _y;
                if (dx * dx + dy * dy > IwaModel.Radius * IwaModel.Radius) continue;
                Hit(s, StuckShare, 0f, "pinned under the rock");
            }
        }

        /// <summary>One player-hit sphere on an enemy's body: <paramref name="share"/> of the weapon's attack, thrown away from the
        /// rock at <paramref name="kick"/> (0 = no throw), crush-marked: it passes any guard and bills no weapon HP.</summary>
        private static bool Hit(int slot, float share, float kick, string what)
        {
            var sh = new Shell { What = what };
            if (kick > 0f) { sh.Slot = slot; sh.Hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)); }   // the impact's hits are watched
            sh.Replant = x => PlantHit(x, slot, share, kick);
            return sh.Replant(sh);
        }

        private static bool PlantHit(Shell sh, int slot, float share, float kick)
        {
            string what = sh.What;
            int attack = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * share));
            EnemyHit.Kick? k = null;
            if (kick > 0f)
            {   // the kick comes from the rock — for the enemy under it (no way across the ground away from the spot), from just past
                // it on the side away from the player, so it is thrown toward the player
                long vp = EnemyAddresses.CharObjects.PosAddr(slot);
                float ox = _x, oy = _y, vx = Memory.ReadFloat(vp), vy = Memory.ReadFloat(vp + 8);
                if ((vx - _x) * (vx - _x) + (vy - _y) * (vy - _y) < 4f)
                {
                    float px = Memory.ReadFloat(Addresses.dunPositionX) - vx, py = Memory.ReadFloat(Addresses.dunPositionY) - vy, pl = (float)Math.Sqrt(px * px + py * py);
                    if (pl < 1e-3f) { px = 1f; py = 0f; pl = 1f; }
                    ox = vx - px / pl * KickLead; oy = vy - py / pl * KickLead;
                }
                k = new EnemyHit.Kick(ox, _ground, oy, kick, KickDecay);
            }
            // Crush-marked: through any guard (the guard-crush cave), no weapon HP per hit; no poison, stop, critical, steal or drain from a drop.
            int idx = EnemyHit.TryPlant(slot, attack, HitRadius, k, out var at, mark: CodeCaves.CrushMark, weaponAbilities: false);
            if (idx < 0) return false;
            sh.Idx = idx; sh.Ticks = ShellLifeTicks; sh.Tries++;
            _shells.Add(sh);
            long up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uh = Memory.ReadFloat(up + 4), uy = Memory.ReadFloat(up + 8);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{what}{(sh.Tries > 1 ? $" (again, {sh.Tries})" : "")}: enemy slot {slot} for {attack} (entry {idx}) — planted at ({at.X:F0},{at.H:F0},{at.Y:F0}) r {at.Radius:F1}, the unit at ({ux:F0},{uh:F0},{uy:F0}), HP {Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp))}");
            return true;
        }

        /// <summary>Planted hits whose life is up, or that the engine has consumed, are withdrawn (their mark with them); a WATCHED one
        /// that left its enemy's HP as it was is planted again (its own list, not PlantedHits: each Shell carries its retry state and the
        /// diagnostic reads the entry before it goes).</summary>
        internal static void RetireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var sh = _shells[i];
                bool spent = pool != 0 && !CollisionPool.IsActive(pool, sh.Idx);                 // the engine is done with it: its mark must not ride on into the entry's next use
                if (--sh.Ticks > 0 && !spent) continue;
                if (DebugDiagnostics.Enabled && sh.Slot >= 0 && pool != 0 && !spent) Diagnose(sh, pool);                    // still there: nothing took it — what CheckDmg saw, before it goes
                CollisionPool.Withdraw(pool, sh.Idx, clearMark: true);
                _shells.RemoveAt(i);
                if (sh.Slot < 0 || !Enemies.IsLive(sh.Slot)) continue;
                int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(sh.Slot, EnemySlotOffsets.Hp));
                if (hp != sh.Hp) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{sh.What}: enemy slot {sh.Slot} HP {sh.Hp} → {hp}"); continue; }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{sh.What}: enemy slot {sh.Slot} took nothing (entry {sh.Idx} {(spent ? "consumed" : "never taken")}, try {sh.Tries}/{HitTries})");
                if (DebugDiagnostics.Enabled && spent && sh.Tries == 1) Diagnose(sh, pool);
                if (sh.Tries < HitTries) sh.Replant(sh);
            }
        }

        /// <summary>What CheckDmg tests a watched hit against, logged: the enemy's invincibility (+0x1E468), motion frame, guard windows,
        /// and each active hurt sphere — its distance from the entry against the two radii, its frame window, its damage % for
        /// the attacker.</summary>
        private static void Diagnose(Shell sh, long pool)
        {
            int slot = sh.Slot;
            long e = pool + sh.Idx * CollisionPool.Stride, mu = EnemyAddresses.MainMonstorUnit.Base;
            float ex = Memory.ReadFloat(e), eh = Memory.ReadFloat(e + 4), ey = Memory.ReadFloat(e + 8), er = Memory.ReadFloat(e + CollisionPool.Radius);
            int owner = Memory.ReadInt(e + CollisionPool.Owner);
            var sb = new System.Text.StringBuilder();
            sb.Append($"diagnose slot {slot}: muteki {Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, 0x98))}, frame {Memory.ReadFloat(mu + slot * 0x3510L + 0x1FFC0):F1}, entry mark 0x{Memory.ReadUInt(e + CodeCaves.NoDrainMarkOff):X8} owner {owner}, guard");
            for (int w = 0; w < 3; w++)
            {
                long g = mu + slot * 0x20L + EnemyAddresses.GuardWindows.FlagOffset;
                if (Memory.ReadShort(g + w * 2) != 0) sb.Append($" [{Memory.ReadFloat(g + 8 + w * 4):F0}–{Memory.ReadFloat(g + 0x14 + w * 4):F0}]");
            }
            long b = BodyCollision.SlotBase(slot);
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c) - ex, ch = Memory.ReadFloat(c + 4) - eh, cy = Memory.ReadFloat(c + 8) - ey;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                float lo = Memory.ReadFloat(b + BodyCollision.Param1Array + part * BodyCollision.BodyPartStride), hi = Memory.ReadFloat(b + BodyCollision.Param2Array + part * BodyCollision.BodyPartStride);
                int pct = owner >= 0 && owner < 6 ? Memory.ReadInt(b + BodyCollision.DamagePctArray + part * BodyCollision.DamagePctStride + owner * 4) : -1;
                sb.Append($"; sphere {part} d {(float)Math.Sqrt(cx * cx + ch * ch + cy * cy):F1} ≤ {r + er:F1}? win {lo:F0}–{hi:F0} pct {pct}");
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + sb);
        }
    }
}
