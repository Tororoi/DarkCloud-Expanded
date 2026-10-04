using System;
using static Dark_Cloud_Improved_Version.AngelGear;
using static Dark_Cloud_Improved_Version.ShieldPatches;
using static Dark_Cloud_Improved_Version.MeleeHitWatch;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The shield ring: while the slingshot is up, every live enemy "sees" Xiao at its own point on a ring of the
    /// slingshot's radius along its approach line, so it walks straight at her and stops and attacks on reaching the
    /// slingshot (<see cref="UpdateRing"/>); released, every enemy sees her real position again (<see cref="ReleaseRing"/>).
    /// Pure data through <see cref="AggroTable"/>, the one writer of the AI redirect pointer table, with MoveCheck2's
    /// body-block distance widened to the ring. One of the <see cref="AngelGear"/> classes, which share their members
    /// through using static.</summary>
    internal static class ShieldRing
    {
        private const string Tag = "[AngelGear/ShieldRing] ";

        // Enemies keep Xiao's CENTER as their target, but each one "sees" her at the point on a ring of the slingshot's
        // radius along its own approach line — so it walks straight at her and stops/attacks on reaching the slingshot.
        // Pure data: the target redirect caves (TargetRedirectCaves) make _GET_POSITION/_GET_DISTANCE read a per-slot POINTER
        // (CodeCaves.PtrTable, written only through AggroTable); the ring points each live enemy's pointer at its own
        // ring position in CodeCaves.ShieldRingTable. Enemies already inside the ring see her real position (they are
        // past the shield). Released → every pointer back to the live player.
        private const float RingRadius   = PropAhead + PropHitRadius;   // the slingshot's far face: bodies stop at the volume a swing is tested against
        private const int   RingSlots    = 20;               // per-slot entries managed (Mirage manages the same 20)

        private static bool _ringWarned;
        /// <summary>The shield ring holds the AI redirect pointer table (<see cref="AggroTable.Holder.ShieldRing"/>).</summary>
        internal static bool RingActive { get; private set; }

        /// <summary>Per tick while the slingshot is up: every live enemy farther than RingRadius gets a
        /// pointer to its own ring position (her center + RingRadius along the line to it); nearer ones,
        /// and empty slots, read the live player. Positions are written BEFORE the pointers.</summary>
        internal static void UpdateRing(float xx, float xh, float xy)
        {
            if (!TargetRedirectCaves.Armed)
            {
                if (!_ringWarned) { _ringWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "redirect caves not armed — shield ring inactive (arms at the next town visit)"); }
                return;
            }
            var ring = new byte[RingSlots * 16];
            var ptrs = new byte[RingSlots * CodeCaves.PtrStride];
            int ringed = 0;
            for (int s = 0; s < RingSlots; s++)
            {
                uint ptr = StbExternCmd.PlayerPosGuest;
                if (s < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(s))
                {
                    long p = EnemyAddresses.CharObjects.PosAddr(s);
                    float dx = Memory.ReadFloat(p) - xx, dy = Memory.ReadFloat(p + 8) - xy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > RingRadius)
                    {
                        BitConverter.GetBytes(xx + dx / d * RingRadius).CopyTo(ring, s * 16);
                        BitConverter.GetBytes(xh).CopyTo(ring, s * 16 + 4);
                        BitConverter.GetBytes(xy + dy / d * RingRadius).CopyTo(ring, s * 16 + 8);
                        BitConverter.GetBytes(1f).CopyTo(ring, s * 16 + 12);
                        ptr = CodeCaves.ShieldRingTableGuest + (uint)(s * 16);
                        ringed++;
                    }
                }
                BitConverter.GetBytes(ptr).CopyTo(ptrs, s * CodeCaves.PtrStride);
            }
            bool first = !RingActive;
            RingActive = true;
            if (first) AggroTable.Claim(AggroTable.Holder.ShieldRing);                        // the table is the ring's while it is up
            if (first && _blockArmed) Memory.WriteFloat(Mailbox.ShieldBlockAddend, RingRadius);   // bodies stop at the ring
            Memory.WriteBytesBatch(CodeCaves.ShieldRingTable, ring);
            AggroTable.Write(AggroTable.Holder.ShieldRing, ptrs);
            if (first) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shield ring up (R={RingRadius:F1}, {ringed} enemies ringed)");
        }

        /// <summary>Every managed pointer back to the live player (the table handed back to <see cref="AggroTable"/>) and the
        /// body-block distance back to vanilla.</summary>
        internal static void ReleaseRing()
        {
            if (!RingActive) return;
            AggroTable.Release(AggroTable.Holder.ShieldRing);                                 // every slot back on the player
            if (_blockArmed) Memory.WriteFloat(Mailbox.ShieldBlockAddend, VanillaBlockAddend);
            RingActive = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shield ring released");
        }
    }
}
