using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Every enemy's eyes on the judgement blade (docs/big-bang.md, docs/aggro-redirect.md): while a blade falls,
    /// every slot's "where is the player" is pointed at CodeCaves.JudgementPos through the per-slot target table
    /// (<see cref="TargetRedirectCaves"/>, written through <see cref="AggroTable"/>), so their own AI turns them to it.
    /// <see cref="JudgementBlade"/> begins it at the drop and keeps JudgementPos on the blade and then the blast; the owners'
    /// loops (Big Bang, the Sword of Zeus) release it when due. The facing hold itself is <see cref="EnemyFacing"/>.</summary>
    internal static class BladeRedirect
    {
        // The target redirect (TargetRedirectCaves): each enemy's `_GET_POSITION(-2)` ("where is the player") reads through
        // the per-slot pointer table (CodeCaves.PtrTable), so while the blade falls every live slot is pointed at
        // CodeCaves.JudgementPos — the blade, then the blast — and their own AI turns them to it before the flash
        // lands and holds them; the pointers go back to the live player just before the blinding ends, so they
        // come out of it looking at the danger and then find Toan again. Mirage's table writer only runs for
        // Ungaga and Angel Gear's for Xiao, so nothing else writes the table while Toan holds this.
        private const int    RedirectSlots   = 20;    // the entries Mirage and Angel Gear manage too (FloorSlots is 16)
        private const double RedirectRelease = 0.4;   // seconds before the blinding ends that they get the player back
        private const double RedirectOrphan  = 2.0;   // a drop that never flashed: let go this long after it began
        internal static bool _redirecting; private static bool _redirectBlindSeen; private static DateTime _redirectSince;

        internal static void BeginRedirect(float x, float h, float y)
        {
            if (!TargetRedirectCaves.Armed) return;                              // the caves are armed at the main menu; without them, nothing to point
            Memory.WriteVec3(CodeCaves.JudgementPos, x, h, y);
            Memory.WriteFloat(CodeCaves.JudgementPos + 12, 1f);
            var ptrs = new byte[RedirectSlots * CodeCaves.PtrStride];
            for (int s = 0; s < RedirectSlots; s++)
                BitConverter.GetBytes(s < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(s) ? CodeCaves.JudgementPosGuest : StbExternCmd.PlayerPosGuest)
                            .CopyTo(ptrs, s * CodeCaves.PtrStride);
            AggroTable.Claim(AggroTable.Holder.JudgementBlade);
            AggroTable.Write(AggroTable.Holder.JudgementBlade, ptrs);
            _redirecting = true; _redirectBlindSeen = false; _redirectSince = GameClock.Now;
        }

        /// <summary>The owner's tick: the table goes back to the player <see cref="RedirectRelease"/> before the blinding ends,
        /// or <see cref="RedirectOrphan"/> after a drop that never flashed.</summary>
        internal static void ReleaseRedirectWhenDue()
        {
            if (!_redirecting) return;
            double left = SunSword.BlindSecondsLeft;
            if (left > 0) _redirectBlindSeen = true;
            bool due = _redirectBlindSeen ? left <= RedirectRelease
                     : !JudgementBlade.Dropping && !JudgementBlade.LandingPending && (GameClock.Now - _redirectSince).TotalSeconds > RedirectOrphan;
            if (due) ReleaseRedirect();
        }

        internal static void ReleaseRedirect()
        {
            if (!_redirecting) return;
            _redirecting = false;
            AggroTable.Release(AggroTable.Holder.JudgementBlade);
        }
    }
}
