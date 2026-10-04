using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Where an enemy's body is, read off the live unit: its largest hurt sphere posed this frame
    /// (<see cref="BodyCentre"/>), how far a ground point is from the nearest hurt-sphere edge
    /// (<see cref="NearestHitSphereEdge"/>), its own world height (<see cref="UnitHeight"/>), the species' authored
    /// head height scaled with the unit (<see cref="HeadHeight"/>), whether it still has HP (<see cref="HasHp"/>), and
    /// whether another live unit draws through the same model root (<see cref="RootShared"/>). Shared by the falloff
    /// blast, the judgement blade, EnemyHit, PelletWatch, Confusion and Ungaga's props — everything that plants a hit
    /// on, hangs over, or measures against an enemy.</summary>
    internal static class EnemyBody
    {
        /// <summary>The enemy's largest live body sphere placed this frame (centre and radius), the surest thing a hit sphere of the
        /// same size at the same place will touch; its root and a plain radius where none is (a sphere left far from its unit —
        /// not posed this frame — would put the hit where the enemy no longer is).</summary>
        internal static void BodyCentre(int slot, long a, out float cx, out float ch, out float cy, out float cr)
        {
            long b = BodyCollision.SlotBase(slot);
            cx = Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(slot)); cy = Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(slot) + 8);
            ch = UnitHeight(slot) + BodyRadiusFallback;
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
        internal static float NearestHitSphereEdge(int slot, long a, float x, float y)
        {
            long b = BodyCollision.SlotBase(slot), up = EnemyAddresses.CharObjects.PosAddr(slot);
            float ux = Memory.ReadFloat(up), uy = Memory.ReadFloat(up + 8);
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
