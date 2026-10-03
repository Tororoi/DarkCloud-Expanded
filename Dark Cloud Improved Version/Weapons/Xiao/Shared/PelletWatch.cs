using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What the slingshot abilities share around Xiao's pellets. An INSTANCE is one ability's watch on the player shot
    /// pool for the pellet that has just left (<see cref="NewPellet"/>): each ability owns its own, so one marking a slot as seen
    /// never hides that pellet from another. The statics are stateless: the enemy a dead pellet landed on
    /// (<see cref="EnemyAt"/>) and whether Xiao is out with Super Steve carrying a given sphere (<see cref="SuperSteveSphereOn"/>).
    /// An ability's planted hit entries are a <see cref="PlantedHits"/> of its own.</summary>
    internal sealed class PelletWatch
    {
        /// <summary>How close a dead pellet's last position may be to an enemy's HIT SPHERE edge to count as having landed on
        /// it: contact is at 2 + the sphere's radius, and the position is read once a tick, up to two frames — ten units —
        /// before the pellet died.</summary>
        internal const float HitProximity = 20f;

        private readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];

        /// <summary>The first pellet to appear since the last look (a new live pool slot), or −1. Every live slot is marked
        /// seen on the way (so a look that finds two new pellets reports the lower slot and the other is never reported), and
        /// a slot gone dead is forgotten, so the next pellet to take it is new again.</summary>
        internal int NewPellet()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return -1;
            int found = -1;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i]) { _seen[i] = true; if (found < 0) found = i; }
                else if (!live) _seen[i] = false;
            }
            return found;
        }

        /// <summary>Every slot forgotten: the next look reports whatever is live as new.</summary>
        internal void Reset() => Array.Clear(_seen, 0, _seen.Length);

        /// <summary>The live enemy whose nearest hit sphere's edge is within <see cref="HitProximity"/> of (x, y) — the one a
        /// pellet died on (a big enemy's root can be far from where the pellet met its body); −1 for none.</summary>
        internal static int EnemyAt(float x, float y)
        {
            int best = -1; float bestD = HitProximity;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                float d = EnemyBody.NearestHitSphereEdge(s, EnemyAddresses.FloorSlots.SlotAddr(s, 0), x, y);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>Xiao is the character out, Super Steve is her current weapon, and the sphere attached to it is
        /// <paramref name="sphereId"/> (a source weapon id, SuperSteve.AttachedSphere).</summary>
        internal static bool SuperSteveSphereOn(int sphereId)
        {
            if (Player.CurrentCharacterNum() != Player.XiaoId || Player.Weapon.GetCurrentWeaponId() != Items.supersteve) return false;
            int slot = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.XiaoId));
            return slot >= 0 && slot < DngStatusData.MaxWeaponSlots && SuperSteve.AttachedSphere(DngStatusData.WeaponRecord(Player.XiaoId, slot)) == sphereId;
        }
    }
}
