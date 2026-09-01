using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Draw glowing world-space TRAILS behind anything the mod moves, by taking over the engine's sword-swing
    /// ribbon (<see cref="WeaponTrailFx"/>).
    ///
    /// A general capability, not an Angel Gear thing: any feature that wants a streak behind a moving point can
    /// take a lane. It works because the swing ribbon is a resident 32-rib quad-strip renderer that the engine
    /// Steps and Draws every frame — and one that NOTHING arms while playing as Xiao (only Toan's and Ungaga's
    /// swings call its setter). Zeroing two floats stops the engine writing to it, and the arrays are then ours.
    /// No injected code, no contention.
    ///
    /// THE CATCH, which the first version of this class got wrong: being unarmed for Xiao is not purely a gift.
    /// The object's two frame pointers are resolved BY NAME from the equipped weapon's model
    /// (<c>InitSet(fx, *(NowWeapon + 0xBC), "dcol0", "dcol1")</c>, from <c>selectChrUnit</c>) — and Xiao's
    /// slingshots have no dcol frames at all, so both stay null. Both <c>Step</c> AND <c>Draw</c> open with
    /// <c>if (FrameA != 0 &amp;&amp; FrameB != 0)</c>, so with Xiao equipped the ribbon renders NOTHING. The
    /// very thing that leaves it free is what kills it.
    ///
    /// <see cref="Arm"/> therefore supplies the pointers itself (see <c>EnsureFrames</c>). It is safe because
    /// <c>Draw</c> only null-CHECKS them — every vertex it emits comes from the rib arrays — and <c>Step</c>
    /// only dereferences them inside <c>if (0 &lt; MasterAlpha)</c>, which Arm zeroes.
    ///
    /// So: **Arm() before Ready()**, always. Asking first would answer "no" forever.
    ///
    /// SEVERAL trails out of ONE ribbon: Draw only emits a quad between two CONSECUTIVE ribs when both are active,
    /// so a single inactive rib cuts the strip. The 32 ribs are therefore carved into <see cref="MaxLanes"/> runs
    /// of <see cref="RibsPerLane"/> active ribs, each followed by a dead separator rib.
    ///
    /// A rib is a PAIR of points (the two edges of the ribbon), so a lane is fed a short history of world
    /// positions and a width: each sample becomes an edge pair straddling the path, and the alpha ramp down the
    /// history is what makes it read as a comet tail rather than a stick.
    /// </summary>
    internal static class TrailRibbon
    {
        internal const int MaxLanes    = 6;   // 6 x (4 ribs + 1 separator) = 30, leaving ribs 30/31 dead
        internal const int RibsPerLane = 4;   // 4 ribs = 3 quads = enough to read as a streak
        private  const int LaneStride  = RibsPerLane + 1;   // + the separator that breaks the strip

        // Alpha down the tail, newest sample first. Written once per update — the engine's fade is disabled, so
        // this ramp IS the taper.
        private static readonly float[] TailAlpha = { 255f, 190f, 120f, 50f };

        // Ribbon colours (spRGBA; the engine overwrites the alpha byte itself, so only RGB matters here).
        // Bright gold on the leading edge, near-black on the trailing one — the falloff across the quad is what
        // gives it a glow rather than a flat band.
        private static readonly byte[] EdgeBright = { 0xFF, 0xD8, 0x60 };
        private static readonly byte[] EdgeDark   = { 0x30, 0x20, 0x08 };

        // Local mirrors of the three rib arrays; we rewrite them wholesale and push each as ONE batched write
        // (3 writes/tick instead of ~240 individual ones).
        private static readonly byte[] _pos    = new byte[WeaponTrailFx.RibPosBytes];
        private static readonly byte[] _alpha  = new byte[WeaponTrailFx.RibAlphaBytes];
        private static readonly byte[] _active = new byte[WeaponTrailFx.RibActiveBytes];

        // The engine's own fade rate, snapshotted the first time we take the ribbon. We must hand this BACK: it
        // is shared state, and Toan's and Ungaga's real swing trails read it. Never guess a "default" here — the
        // only correct value is whatever the game actually had.
        private static bool  _saved;
        private static float _origRibDecay;

        // Whether WE supplied the frame pointers (see Arm), so Release knows to put the nulls back.
        private static bool _framesFaked;

        /// <summary>Take the ribbon over (idempotent — call it every tick; it is cheap and it re-asserts after an
        /// equip or floor load, which is when the engine resets the object behind our back).</summary>
        internal static void Arm()
        {
            if (!_saved)
            {
                _origRibDecay = Memory.ReadFloat(WeaponTrailFx.Base + WeaponTrailFx.RibAlphaDecay);
                _saved = true;
            }

            EnsureFrames();

            // Zeroing MasterAlpha is enough to stop Step sampling the weapon frames and advancing the ribbon.
            // MasterDecay is deliberately NOT touched — it belongs to the engine's swing trail, and clobbering it
            // would break Toan's and Ungaga's swings the moment the party switches back.
            Memory.WriteFloat(WeaponTrailFx.Base + WeaponTrailFx.MasterAlpha,   0f);
            Memory.WriteFloat(WeaponTrailFx.Base + WeaponTrailFx.RibAlphaDecay, 0f);     // our ribs never fade out

            for (int i = 0; i < 3; i++)   // colour both edges of both ribs of the quad
            {
                Memory.WriteByte(WeaponTrailFx.Base + WeaponTrailFx.ColorA0 + i, EdgeBright[i]);
                Memory.WriteByte(WeaponTrailFx.Base + WeaponTrailFx.ColorA1 + i, EdgeBright[i]);
                Memory.WriteByte(WeaponTrailFx.Base + WeaponTrailFx.ColorB0 + i, EdgeDark[i]);
                Memory.WriteByte(WeaponTrailFx.Base + WeaponTrailFx.ColorB1 + i, EdgeDark[i]);
            }
        }

        /// <summary>
        /// Give the ribbon two non-null frame pointers, because for Xiao it will never have any of its own.
        ///
        /// <c>selectChrUnit</c> initialises the object with
        /// <c>InitSet(fx, *(NowWeapon + 0xBC), "dcol0", "dcol1")</c> — it resolves the two frames BY NAME out
        /// of the equipped weapon's model. Toan's swords carry <c>dcol0..dcol3</c> and Ungaga's staff carries
        /// <c>dcol0</c>; **Xiao's slingshots carry no dcol frames at all**, so both pointers stay null.
        ///
        /// That is fatal, because both <c>Step</c> and <c>Draw</c> open with
        /// <c>if (FrameA != 0 &amp;&amp; FrameB != 0)</c>. So the very thing that leaves the ribbon free for
        /// Xiao — nothing ever arms it — is also what stops it drawing. The original design missed this.
        ///
        /// The get-out is that <c>Draw</c> only NULL-CHECKS them: every vertex it emits comes from the rib
        /// arrays, and it never dereferences a frame. <c>Step</c> does dereference them, but only inside
        /// <c>if (0 &lt; MasterAlpha)</c> — which <see cref="Arm"/> already zeroes. So any valid pointer will
        /// do; we use the equipped weapon's model root, which is exactly what InitSet would have passed and is
        /// guaranteed to be a real CFrame.
        /// </summary>
        private static void EnsureFrames()
        {
            if (Memory.IsValidGuest((uint)Memory.ReadInt(WeaponTrailFx.Base + WeaponTrailFx.FrameA) & Memory.PhysAddrMask) &&
                Memory.IsValidGuest((uint)Memory.ReadInt(WeaponTrailFx.Base + WeaponTrailFx.FrameB) & Memory.PhysAddrMask))
                return;   // the weapon supplies its own (Toan / Ungaga) — leave the engine's alone

            int root = WeaponModelRoot();
            if (root == 0) return;

            Memory.WriteInt(WeaponTrailFx.Base + WeaponTrailFx.FrameA, root);
            Memory.WriteInt(WeaponTrailFx.Base + WeaponTrailFx.FrameB, root);
            _framesFaked = true;
        }

        /// <summary>Native (guest) pointer to the equipped weapon's model root, <c>*(*NowWeapon + 0xBC)</c>,
        /// or 0 if nothing is equipped.</summary>
        private static int WeaponModelRoot()
        {
            uint nw = (uint)Memory.ReadInt(WeaponModel.NowWeaponPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(nw)) return 0;

            int root = Memory.ReadInt(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            return Memory.IsValidGuest((uint)root & Memory.PhysAddrMask) ? root : 0;
        }

        /// <summary>True if the ribbon will render at all — i.e. both frame pointers are non-null, which is what
        /// <c>Step</c> and <c>Draw</c> gate on. <see cref="Arm"/> supplies them when the weapon does not.</summary>
        internal static bool Ready()
            => Memory.IsValidGuest((uint)Memory.ReadInt(WeaponTrailFx.Base + WeaponTrailFx.FrameA) & Memory.PhysAddrMask) &&
               Memory.IsValidGuest((uint)Memory.ReadInt(WeaponTrailFx.Base + WeaponTrailFx.FrameB) & Memory.PhysAddrMask);

        /// <summary>Start a frame: every lane goes dark until something claims it.</summary>
        internal static void Begin()
        {
            Array.Clear(_active, 0, _active.Length);
            Array.Clear(_alpha, 0, _alpha.Length);
        }

        /// <summary>Which way the ribbon's width runs.</summary>
        internal enum Plane
        {
            /// <summary>Width runs perpendicular to the path IN THE GROUND PLANE, so the ribbon lies flat —
            /// an orbiting pellet reads as a band lying in the plane of its orbit.</summary>
            Flat,

            /// <summary>Width runs along the world UP axis, so the ribbon stands perpendicular to the ground —
            /// the trail reads as a vertical fin following the path, and it stays visible edge-on from above.</summary>
            Upright,
        }

        /// <summary>
        /// Lay one lane's trail along <paramref name="hist"/> — a short history of world positions, NEWEST FIRST
        /// (x, height, y triples). Supply at least <see cref="RibsPerLane"/> samples.
        ///
        /// <paramref name="halfWidth"/> is half the ribbon's width; <paramref name="plane"/> is which way that
        /// width runs (see <see cref="Plane"/>).
        /// </summary>
        internal static void SetLane(int lane, float[] hist, float halfWidth, Plane plane = Plane.Flat)
        {
            if ((uint)lane >= MaxLanes || hist == null || hist.Length < RibsPerLane * 3) return;

            for (int k = 0; k < RibsPerLane; k++)
            {
                int rib = lane * LaneStride + k;
                float x = hist[k * 3], h = hist[k * 3 + 1], y = hist[k * 3 + 2];

                float ax, ah, ay, bx, bh, by;

                if (plane == Plane.Upright)
                {
                    // Straight up and down through the sample. No need for the travel direction at all: the
                    // width axis IS world up, so it is the same whichever way the pellet is going — which also
                    // means a stationary pellet still draws a ribbon instead of collapsing to a line.
                    ax = x; ah = h + halfWidth; ay = y;
                    bx = x; bh = h - halfWidth; by = y;
                }
                else
                {
                    // Direction of travel at this sample (toward the newer sample; the newest borrows the next).
                    int j = k == 0 ? 1 : k;
                    float dx = hist[(j - 1) * 3] - hist[j * 3];
                    float dy = hist[(j - 1) * 3 + 2] - hist[j * 3 + 2];
                    float dl = (float)Math.Sqrt(dx * dx + dy * dy);

                    // Perpendicular, in the ground plane: cross(dir, up) = (-dy, 0, dx). A still pellet has no
                    // direction, so fall back to a fixed axis rather than collapsing the ribbon to a line.
                    float wx, wy;
                    if (dl > 0.0001f) { wx = -dy / dl * halfWidth; wy = dx / dl * halfWidth; }
                    else              { wx = halfWidth; wy = 0f; }

                    ax = x + wx; ah = h; ay = y + wy;
                    bx = x - wx; bh = h; by = y - wy;
                }

                WriteVec4(_pos, rib * WeaponTrailFx.RibStride,      ax, ah, ay);   // edge A (bright)
                WriteVec4(_pos, rib * WeaponTrailFx.RibStride + 16, bx, bh, by);   // edge B (dark)

                BitConverter.GetBytes(TailAlpha[k]).CopyTo(_alpha, rib * 4);
                BitConverter.GetBytes(1).CopyTo(_active, rib * 4);
            }
            // The separator rib stays inactive (Begin cleared it), which is what stops this lane's strip from
            // being stitched onto the next lane's.
        }

        /// <summary>Push the frame to the engine — three contiguous batched writes.</summary>
        internal static void Commit()
        {
            Memory.WriteBytesBatch(WeaponTrailFx.Base + WeaponTrailFx.RibPos,    _pos);
            Memory.WriteBytesBatch(WeaponTrailFx.Base + WeaponTrailFx.RibAlpha,  _alpha);
            Memory.WriteBytesBatch(WeaponTrailFx.Base + WeaponTrailFx.RibActive, _active);
        }

        /// <summary>Blank every trail and hand the ribbon back, restoring the engine's OWN fade rate so a later
        /// Toan or Ungaga swing trail behaves exactly as it did.</summary>
        internal static void Release()
        {
            Begin();
            Commit();
            if (_saved)
            {
                Memory.WriteFloat(WeaponTrailFx.Base + WeaponTrailFx.RibAlphaDecay, _origRibDecay);
                _saved = false;
            }
            if (_framesFaked)
            {
                // Put the nulls back. selectChrUnit re-runs InitSet on every character load, so leaving our
                // pointers behind would only be papered over by luck — and a stale frame pointer here is
                // exactly the sort of thing that resurfaces three features later.
                Memory.WriteInt(WeaponTrailFx.Base + WeaponTrailFx.FrameA, 0);
                Memory.WriteInt(WeaponTrailFx.Base + WeaponTrailFx.FrameB, 0);
                _framesFaked = false;
            }
        }

        private static void WriteVec4(byte[] buf, int off, float x, float h, float y)
        {
            BitConverter.GetBytes(x).CopyTo(buf, off);
            BitConverter.GetBytes(h).CopyTo(buf, off + 4);
            BitConverter.GetBytes(y).CopyTo(buf, off + 8);
            BitConverter.GetBytes(1f).CopyTo(buf, off + 12);   // w = 1 (a point, not a direction)
        }
    }
}
