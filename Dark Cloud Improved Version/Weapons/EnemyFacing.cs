using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Which way the enemies look. <see cref="TurnEnemiesToward"/> turns every live enemy to a point and holds
    /// it there for a few ticks (<see cref="FaceTick"/>), the yaw written in whichever of the engine's four conventions
    /// the floor's own enemies reveal (<see cref="YawConvention"/>). The REDIRECT (<see cref="BeginRedirect"/> …
    /// <see cref="ReleaseRedirect"/>) points every slot's "where is the player" at CodeCaves.JudgementPos through the
    /// Mirage pointer table while a judgement blade falls, so their own AI turns them to it. <see cref="PlayerFacing"/>
    /// is the player's own yaw, for the things placed square to him. Big Bang, the Big Bang shot and JudgementBlade
    /// drive it (docs/big-bang.md).</summary>
    internal static class EnemyFacing
    {
        // ── every enemy's eyes on the blade ──────────────────────────────────────────────────
        // Mirage's decoy redirect, borrowed: each enemy's `_GET_POSITION(-2)` ("where is the player") reads through
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
            if (!Mirage.Armed) return;                                           // the caves are armed at the main menu; without them, nothing to point
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

        /// <summary>Every living enemy turned to face the point, and HELD there for <see cref="FaceHoldTicks"/>: the
        /// facing vector is the unit its AI steers by and the CCharacter yaw is what it is drawn with, and a single
        /// write of either is undone by the next Step — the AI steering back toward Toan, the flash's own hit
        /// reaction — before the blinding's script hold takes over.</summary>
        internal static void TurnEnemiesToward(float x, float y)
        {
            _faceX = x; _faceY = y; _faceHold = FaceHoldTicks;
            FaceAll();
        }
        private const int FaceHoldTicks = 12;   // ≈0.36 s: through the flash's stagger, into the script hold
        internal static int _faceHold; private static float _faceX, _faceY;
        /// <summary>The facing hold's tick — whichever blade's loop is running calls it.</summary>
        internal static void FaceTick() { if (_faceHold > 0) { _faceHold--; FaceAll(); } }
        private static void FaceAll()
        {
            int conv = YawConvention();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                // An enemy the blast cannot move (knockback 0: bosses, rooted plants) is not turned either: with no
                // shove to settle it, a turn does not hold against its own AI.
                if (Memory.ReadFloat(a + EnemySlotOffsets.KnockbackMult) <= 0f) continue;
                float dx = _faceX - Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(s));       // the unit's own position (the floor-slot location fields read 0 for some enemies)
                float dy = _faceY - Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(s) + 8);
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-3f) continue;
                float fx = dx / len, fz = dy / len;
                Memory.WriteFloat(a + EnemySlotOffsets.FacingX, fx);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingY, 0f);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingZ, fz);
                if (conv >= 0) Memory.WriteFloat(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharRotY, YawOf(conv, fx, fz));
            }
        }

        /// <summary>The engine's yaw from a facing vector — which of the four sign/axis conventions the game uses is
        /// READ OFF LIVE ENEMIES the first time it is needed on a floor (each one's own facing against its own yaw),
        /// rather than assumed. −1 while no enemy has told us yet (the yaw is then left to the engine).</summary>
        private static float YawOf(int conv, float fx, float fz) => conv switch
        {
            0 => (float)Math.Atan2(fx, fz), 1 => (float)Math.Atan2(-fx, fz),
            2 => (float)Math.Atan2(fz, fx), _ => (float)Math.Atan2(-fz, fx),
        };
        internal static int _yawConv = -1;
        private static int YawConvention()
        {
            if (_yawConv >= 0) return _yawConv;
            var err = new double[4]; int n = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                float fx = Memory.ReadFloat(a + EnemySlotOffsets.FacingX), fz = Memory.ReadFloat(a + EnemySlotOffsets.FacingZ);
                if (fx * fx + fz * fz < 0.5f) continue;
                float yaw = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharRotY);
                if (float.IsNaN(yaw) || Math.Abs(yaw) > 100f) continue;
                for (int c = 0; c < 4; c++)
                {
                    double d = Math.Abs(YawOf(c, fx, fz) - yaw) % (2 * Math.PI);
                    err[c] += Math.Min(d, 2 * Math.PI - d);
                }
                n++;
            }
            if (n == 0) return -1;
            int best = 0; for (int c = 1; c < 4; c++) if (err[c] < err[best]) best = c;
            if (err[best] / n > 0.35) return -1;                                   // none of them fits: leave the yaw alone
            _yawConv = best;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[EnemyFacing] enemy yaw convention {best} (mean error {err[best] / n:F2} rad over {n})");
            return best;
        }

        internal static float PlayerFacing()
        {
            uint root = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (Memory.IsValidGuest(root))
            {
                long m = Memory.ToMmu(root);
                if (Memory.ReadInt(m + CFrameVu1.EulerValid) == 0) return Memory.ReadFloat(m + CFrameVu1.EulerY);
            }
            return Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
        }
    }
}
