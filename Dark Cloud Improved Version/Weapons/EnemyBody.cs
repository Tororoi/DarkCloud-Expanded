using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Where an enemy's body is, read off the live unit: its largest hurt sphere posed this frame
    /// (<see cref="BodyCentre"/>), how far a ground point is from the nearest hurt-sphere edge
    /// (<see cref="NearestHitSphereEdge"/>), how near a shot's path comes to it (<see cref="SegmentEdge"/>), where an attacker is to
    /// go for it (<see cref="AimPoint"/>) — a floor enemy's or the summoned ally's (<see cref="Body"/>) — its own world height (<see cref="UnitHeight"/>), the species' authored
    /// head height scaled with the unit (<see cref="HeadHeight"/>), whether it still has HP (<see cref="HasHp"/>), and
    /// whether another live unit draws through the same model root (<see cref="RootShared"/>). Shared by the falloff
    /// blast, the judgement blade, EnemyHit, PelletWatch, Confusion's friendly fire and Ungaga's props — everything that plants a hit
    /// on, hangs over, or measures against an enemy.</summary>
    internal static class EnemyBody
    {
        /// <summary>A monster's body as the helpers read it: its hit-sphere block (BodyCollision's arrays from here: its manager plus
        /// its unit × BodyCollision.SlotStride) and its position (its CCharacter's, x, h, y). A floor enemy's (<see cref="Of"/>) or the
        /// summoned ally's, unit 0 of its private manager (<see cref="Ally"/>).</summary>
        internal readonly record struct Body(long Spheres, long Pos)
        {
            internal static Body Of(int slot) => new(BodyCollision.SlotBase(slot), EnemyAddresses.CharObjects.PosAddr(slot));
            internal static Body Ally(long manager) => new(manager, manager + AllyChara + 0x10);
        }
        private const long AllyChara = 0x1FCD0;   // a manager's chara[0] (its unit 0's CCharacter)

        /// <summary>The enemy's largest live body sphere placed this frame (centre and radius), the surest thing a hit sphere of the
        /// same size at the same place will touch; its root and a plain radius where none is (a sphere left far from its unit —
        /// not posed this frame — would put the hit where the enemy no longer is).</summary>
        internal static void BodyCentre(int slot, long a, out float cx, out float ch, out float cy, out float cr)
            => BodyCentre(Body.Of(slot), out cx, out ch, out cy, out cr);

        internal static void BodyCentre(Body body, out float cx, out float ch, out float cy, out float cr)
        {
            long b = body.Spheres;
            cx = Memory.ReadFloat(body.Pos); cy = Memory.ReadFloat(body.Pos + 8);
            ch = Memory.ReadFloat(body.Pos + 4) + BodyRadiusFallback;
            cr = BodyRadiusFallback;
            float ux = cx, uy = cy, best = 0f;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                if (r <= best) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float sx = Memory.ReadFloat(c), sh = Memory.ReadFloat(c + 4), sy = Memory.ReadFloat(c + 8);
                if (Math.Abs(sx - ux) > UnposedSphere || Math.Abs(sy - uy) > UnposedSphere) continue;   // not this frame's placement: a hit there lands on nothing
                best = r; cr = r;
                cx = sx; ch = sh; cy = sy;
            }
        }
        private const float BodyRadiusFallback = 10f;

        /// <summary>How far (x, y) is from the nearest edge of the enemy's active hit spheres, in the ground plane (0 = inside one);
        /// with none active (or none posed — a sphere the frame has not placed sits far from its unit), from its own position
        /// less the fallback radius.</summary>
        internal static float NearestHitSphereEdge(int slot, long a, float x, float y) => NearestHitSphereEdge(Body.Of(slot), x, y);

        internal static float NearestHitSphereEdge(Body body, float x, float y)
        {
            long b = body.Spheres;
            float ux = Memory.ReadFloat(body.Pos), uy = Memory.ReadFloat(body.Pos + 8);
            float best = float.MaxValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > UnposedSphere || Math.Abs(cy - uy) > UnposedSphere) continue;   // not this frame's placement
                float dx = cx - x, dy = cy - y;
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dy * dy) - r);
            }
            if (best == float.MaxValue)
            {
                float dx = ux - x, dy = uy - y;
                best = (float)Math.Sqrt(dx * dx + dy * dy) - BodyRadiusFallback;
            }
            return Math.Max(0f, best);
        }
        private const float UnposedSphere = 80f;   // a hit sphere farther than this from its own unit has not been placed this frame

        /// <summary>How near the segment <paramref name="a"/>→<paramref name="b"/> (a shot's path since the last tick) comes to the body:
        /// the least distance from it to the surface of any of its posed hit spheres, and the segment's point there (<paramref name="at"/>).
        /// MaxValue with none.</summary>
        internal static float SegmentEdge(Body body, (float x, float h, float y) a, (float x, float h, float y) b, out (float x, float h, float y) at)
        {
            at = b;
            long bc = body.Spheres;
            float ux = Memory.ReadFloat(body.Pos), uy = Memory.ReadFloat(body.Pos + 8), best = float.MaxValue;
            float dx = b.x - a.x, dh = b.h - a.h, dy = b.y - a.y, len2 = dx * dx + dh * dh + dy * dy;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(bc + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = bc + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), ch = Memory.ReadFloat(c + 4), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > UnposedSphere || Math.Abs(cy - uy) > UnposedSphere) continue;
                float t = len2 > 1e-6f ? Math.Clamp(((cx - a.x) * dx + (ch - a.h) * dh + (cy - a.y) * dy) / len2, 0f, 1f) : 0f;
                float qx = a.x + dx * t, qh = a.h + dh * t, qy = a.y + dy * t, px = qx - cx, ph = qh - ch, py = qy - cy;
                float edge = (float)Math.Sqrt(px * px + ph * ph + py * py) - Memory.ReadFloat(bc + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                if (edge < best) { best = edge; at = (qx, qh, qy); }
            }
            return best;
        }

        /// <summary>Where an attacker at (x, y) is to go for the enemy in <paramref name="slot"/> — what its script reads as "the player":
        /// a point inside the enemy's posed hit sphere nearest it (by distance less radius), <see cref="AimDepth"/> in from its surface
        /// toward the attacker (its centre for a sphere smaller than that, or an attacker already that deep), at the unit's height. A
        /// large enemy's root lies deep in its body, where the attacker's approach (and the block against the body) never got it near
        /// enough to swing; this puts the mark as deep as an ordinary enemy's root sits. No posed sphere: the root.</summary>
        internal static (float x, float h, float y) AimPoint(int slot, float x, float y)
        {
            var body = Body.Of(slot);
            long b = body.Spheres;
            float ux = Memory.ReadFloat(body.Pos), uh = Memory.ReadFloat(body.Pos + 4), uy = Memory.ReadFloat(body.Pos + 8);
            float best = float.MaxValue, bx = ux, by = uy, br = 0f;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                float cx = Memory.ReadFloat(c), cy = Memory.ReadFloat(c + 8);
                if (Math.Abs(cx - ux) > UnposedSphere || Math.Abs(cy - uy) > UnposedSphere) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                float edge = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                if (edge < best) { best = edge; bx = cx; by = cy; br = r; }
            }
            if (best == float.MaxValue) return (ux, uh, uy);
            float dx = x - bx, dy = y - by, d = (float)Math.Sqrt(dx * dx + dy * dy), depth = br - AimDepth;
            if (depth > 0f && d > depth) { bx += dx / d * depth; by += dy / d * depth; }
            return (bx, uh, by);
        }
        internal const float AimDepth = 8f;   // as deep in its nearest sphere as an ordinary enemy's root sits (tools/stubs/ally_aim.s: the same)

        /// <summary>The unit's own world height: its CCharacter position (per slot — the model root's world matrix is
        /// the SPECIES' tree, posed for whichever unit drew last, and the slot's LocationZ is floor-relative).</summary>
        internal static float UnitHeight(int slot) => Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(slot) + 4);

        /// <summary>The species' authored height over its root (EnemySpecies HeightFromRoot, 15 where none), scaled with the unit — the
        /// top of its head.</summary>
        internal static float HeadHeight(int slot)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            float height = EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.HeightFromRoot.HasValue ? def.HeightFromRoot.Value : 15f;
            float scale = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharScale + 4);
            if (!(scale > 0.05f) || scale > 20f) scale = 1f;                                       // a grown miniboss
            return height * scale;
        }

        /// <summary>Alive: HP above zero — the liveness test the hover and the blast use (nothing they change themselves).</summary>
        internal static bool HasHp(int slot) =>
            slot >= 0 && slot < EnemyAddresses.FloorSlots.Count
            && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) > 0;

        /// <summary>Whether another live unit draws through the same model root as <paramref name="slot"/> — the
        /// species' tree is one object, so a pin to it can only be trusted when this unit is its sole user.</summary>
        internal static bool RootShared(uint root, int slot)
        {
            if (!Memory.IsValidGuest(root)) return true;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (s == slot || !Enemies.IsLive(s)) continue;
                if (Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharModel) == root) return true;
            }
            return false;
        }
    }
}
