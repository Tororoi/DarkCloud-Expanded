using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The engine's own word on a player pellet's contact. The pellet-contact cave (DebugIfCave.PelletContact) records
    /// each contact step__5CSHOT makes — the pellet's slot, whether it met an enemy or a wall, and the point: for an enemy, the
    /// centre of the hit sphere it struck — so an ability knows, a tick after the engine's frame, exactly which pellet landed on
    /// exactly which enemy, with no distance to guess at. <see cref="Poll"/> hands each consumer every new record once (each
    /// keeps its own cursor, so two abilities watching different pellets do not steal each other's).</summary>
    internal static class PelletContacts
    {
        internal readonly struct Contact
        {
            internal readonly int Slot, Kind; internal readonly float X, H, Y;
            internal Contact(int slot, int kind, float x, float h, float y) { Slot = slot; Kind = kind; X = x; H = h; Y = y; }
            internal bool Enemy => Kind == CodeCaves.ContactEnemy;
            internal bool Wall  => Kind == CodeCaves.ContactWall;
        }
        /// <summary>A consumer's starting cursor: the first poll only learns the count.</summary>
        internal const int Fresh = int.MinValue;

        /// <summary>The cave is in this ISO (the contact call is hooked).</summary>
        internal static bool Native => (uint)Memory.ReadInt(0x20000000L + 0x001ABD88) == (0x0C000000u | (DebugIfCave.PelletContact >> 2));

        /// <summary>Move a consumer's cursor to now: only contacts from here on are its. A consumer that polls only while its own
        /// pellet flies must sync as it fires, or the first poll hands it a record left by an earlier pellet — often in the same
        /// slot — and it "lands" at once.</summary>
        internal static void Sync(ref int seen) => seen = Memory.ReadInt(CodeCaves.PelletContact + CodeCaves.ContactCounter);

        /// <summary>A contact recorded since this consumer's last poll (the most recent, if several came in one tick); false when
        /// none. <paramref name="seen"/> is the consumer's cursor (start it at <see cref="Fresh"/>).</summary>
        internal static bool Poll(ref int seen, out Contact c)
        {
            int n = Memory.ReadInt(CodeCaves.PelletContact + CodeCaves.ContactCounter);
            if (n == seen || seen == Fresh) { seen = n; c = default; return false; }   // the first look only learns the count
            seen = n;
            long b = CodeCaves.PelletContact;
            c = new Contact(Memory.ReadInt(b + CodeCaves.ContactSlot), Memory.ReadInt(b + CodeCaves.ContactKind),
                            Memory.ReadFloat(b + CodeCaves.ContactX), Memory.ReadFloat(b + CodeCaves.ContactH), Memory.ReadFloat(b + CodeCaves.ContactY));
            return true;
        }

        /// <summary>The live enemy whose active hit sphere sits nearest the contact point (the cave copies the struck sphere's own
        /// centre; the enemy has moved a frame or two since), within <see cref="SphereTol"/>; −1 for none.</summary>
        internal static int EnemyAtSphere(in Contact c)
        {
            int best = -1; float bestD = SphereTol * SphereTol;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long b = BodyCollision.SlotBase(s);
                for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
                {
                    if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                    long cc = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                    float dx = Memory.ReadFloat(cc) - c.X, dh = Memory.ReadFloat(cc + 4) - c.H, dy = Memory.ReadFloat(cc + 8) - c.Y, d = dx * dx + dh * dh + dy * dy;
                    if (d < bestD) { bestD = d; best = s; }
                }
            }
            return best;
        }
        private const float SphereTol = 8f;   // a sphere's centre moves with its enemy between the engine's frame and the mod's read (two frames of the fastest enemies)
    }
}
