using System;
using static Dark_Cloud_Improved_Version.CanalTide;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Canal tide-evict: a player caught in the drained canal when the tide rises is warped to the East Harbor
    /// dock. This side maintains the native fade-hook's flag (arm / boundary / TTL), kills the arrival camera
    /// swing at its source, and sets the dock camera once East Harbor loads (DockCamera).
    /// </summary>
    internal static class CanalEvict
    {
        // Tide-evict: a player caught in the drained canal when the tide rises (morning→afternoon) is warped
        // to the East Harbor dock under the same black fade. Fired by writing the label-403 event id to the
        // engine's start_event_no — EditLoop runs it, the script's _MAP_JUMP does the full load (see
        // CustomFishingSpot.BuildCanalWarpBytecode). One-shot per Queens visit (the load leaves Queens anyway).
        // Direct _MAP_JUMP: the Queens time-change is script EVENT 132 (RunEvent 0x84, GameMode 0xe). Rather
        // than queue a new event via start_event_no (which would run only AFTER 132 ends), we set the map-jump
        // on the CURRENTLY running event — NextMapNo + arrival StartEventNo + the return code EdEventMode reads.
        private const long  CanalEvictFlag = CodeCaves.Mailbox.CanalEvict; // native fade-hook reads this on the fully-black frame
        private const float CanalBankY     = 31f;                        // afternoon (medium) tide height: caught = BELOW the
                                                                          //   incoming waterline (banks/ladder-top are ≈70, well above)
        private const float CanalZPad      = 60f;                        // canal wall z≈±50 + padding; the basin is the only
                                                                          //   walkable ground below tide height in this z-band
        // (A previous version also bounded X against the waterfall walls' span (150..1550) and read the live
        // CWater corners for Z — the X bound silently missed the basin's west end (players at x≈-185, logged
        // 2026-08-23) and neither was needed: y-below-tide + the z-band already identify the basin uniquely.)
        private const int   EvictArmHold = 20;          // short flicker tolerance only (~1s); NOT a lingering "was recently in" window
                                                        //   — a long hold warped players who'd already climbed out when the tide turned
        private const int   FlagTtl      = 300;         // safety: drop the native flag if no fully-black consumed it within ~15s
        private static int  _evictArm, _flagTtl;
        private static float _prevTarget = float.NaN;   // previous tide target — for the low→non-low boundary edge

        // Post-warp dock camera. The mod's town camera MAINTAINS the current dist/angle/height (which is why it
        // keeps the Queens-relative offset on the warp), so once East Harbor loads we set them to the Sunken-Ship
        // dock values (from the CameraDiag leaving the ship: dist 79.7, angle 0, height 5). ref (the look-at)
        // follows the player automatically. Armed when the evict flag is raised; self-clears (hold/timeout).
        // Field map (CCameraFollow / MainCamera): +0x2D0 dist, +0x2D4 height, +0x2D8 TARGET angle, +0x2DC SMOOTHED
        // angle (the one Step actually renders from; eases toward target). We write BOTH angle fields so the camera
        // SNAPS to the dock angle rather than swinging from the Queens angle over ~1s. Nothing overwrites dist/angle
        // in town (the mod stubs CameraAutoMove → no auto-rotate; the pull-in only touches dist near walls), so a
        // brief hold is enough for it to stick. NOTE: a pure-STB native set isn't possible here — the direct-set VM
        // commands (_SET_FOLLOW_CAMERA etc.) run on the battle-only DAT_01d3d210 camera (null in town), and the town
        // _RESET_CAMERA path defers to EventMode, which the arrival event never routes through. So we set it here.
        private const int   EastHarborMapNo = 19;
        private const long  CamOffDist = FollowCamera.Dist, CamOffHeight = FollowCamera.Height,
                            CamOffAngle = FollowCamera.Angle, CamOffAngleSmooth = FollowCamera.AngleNow;
        private const long  CamOffRefX = FollowCamera.RefX, CamOffRefY = FollowCamera.RefY,
                            CamOffRefZ = FollowCamera.RefZ;   // ref (look-at) xyz used by Step
        private const long  CamCurPos = 0x260, CamCurRef = 0x270, CamNextPos = 0x280, CamNextRef = 0x290; // base CCamera ease pair
        private const float DockCamDist = 69.7f, DockCamHeight = 5.0f, DockCamAngle = 0.0f;  // just under BASE_DIST (70, vanilla rest since 2026-08; was 79.7 under the 80 rest)
        private const float DockRefLoadedX = -1000f;         // ref.x below this ⇒ the dock ref is loaded (dock = -1311)
        private const int   CamHold = 45, CamTimeout = 600;
        private static bool _camActive;
        private static int  _camAge, _camHeld;

        private static bool _loggedStaleFlag;          // one log line when a stale evict flag is proactively cleared

        /// <summary>Per-tick evict bookkeeping (Queens only): ARM while the player wades the drained canal, raise
        /// the native evict flag at the low→non-low boundary if they were caught, keep the one-shot flag clean
        /// otherwise, and zero the orbit angle under the Queens fade-out so East Harbor inherits no swing.</summary>
        internal static void Update(float shownLvl, float target)
        {
            // TIDE-EVICT — the timing is owned by NATIVE code now (IsoPatcher.PatchCanalEvictFadeHook hooks
            // EdFadeInOut's fully-black store @0x189970). This side only maintains the flag: ARM while the player
            // wades the drained low-tide canal, and at the period boundary (tide turns low→non-low) raise the
            // native evict flag if they were caught. The fade-hook reads it on the exact fully-black frame and
            // does the _MAP_JUMP to the East Harbor dock (+ clears the flag) — frame-perfect, no fade polling.
            if (shownLvl <= LowTideThreshold && PlayerInCanal()) _evictArm = EvictArmHold;
            else if (_evictArm > 0) _evictArm--;

            // Don't fire while a fishing session is entering/active: _LOAD_FISHING_DATA perturbs the scene's
            // time/water, which can read as a low→non-low tide jump — a FALSE boundary. A player who chose to
            // fish is not being caught by the rising tide.
            if (!float.IsNaN(_prevTarget) && _prevTarget <= LowTideThreshold && target > LowTideThreshold)
            {
                if (_evictArm > 0 && !CustomFishingSpot.InFishingWindow)
                {
                    Memory.WriteInt(CanalEvictFlag, 1);
                    _flagTtl = FlagTtl;
                    _camActive = true; _camAge = 0; _camHeld = 0;   // set the dock camera once East Harbor loads
                    Log($"tide-evict: caught in draining canal ({_prevTarget:0.#}→{target:0.#}) → raised native evict flag");
                }
                else
                    // The suppressed boundary was SILENT — exactly the blind spot when diagnosing "the warp
                    // didn't happen". One line names the gate that blocked it (player not recently in the
                    // basin, or a live fishing window) so a future miss is diagnosable without a re-repro.
                    Log($"tide-evict: boundary ({_prevTarget:0.#}→{target:0.#}) NOT raised " +
                        $"(arm={_evictArm}, fishing={CustomFishingSpot.InFishingWindow}, inCanalNow={PlayerInCanal()})");
            }
            _prevTarget = target;
            // Keep the flag CLEAN between evictions. It's a one-shot the native fade-hook consumes on the next
            // fully-black frame — so a stale/garbage 1 (e.g. left in RAM on a DIRECT boot/state-load into Queens,
            // where the non-Queens reset at the top never ran) would be eaten by the next UNRELATED fade — a
            // fishing-entry fade — and false-warp the player to the dock. While no genuine eviction is pending
            // (TTL==0), pin it to 0; only the boundary above raises it, with a TTL that spans the tide fade.
            if (_flagTtl > 0)
            {
                if (--_flagTtl == 0)
                {
                    // TTL expiry with the flag STILL SET = the native fade-hook never consumed it (no
                    // fully-black EdFadeInOut frame within ~15s of the raise) — log it: that's the
                    // "raised but never warped" failure signature, previously silent.
                    if (Memory.ReadInt(CanalEvictFlag) != 0)
                        Log("tide-evict: raised flag EXPIRED unconsumed (no fully-black fade within TTL) → cleared, no warp");
                    Memory.WriteInt(CanalEvictFlag, 0);
                }
            }
            else if (Memory.ReadInt(CanalEvictFlag) != 0)
            {
                Memory.WriteInt(CanalEvictFlag, 0);
                if (!_loggedStaleFlag) { Log("canal-evict flag was set with no eviction pending → cleared (would have false-warped the next fade)"); _loggedStaleFlag = true; }
            }

            // Kill the arrival camera-SWING at its SOURCE. MainCamera is a persistent object whose orbit angle
            // carries THROUGH the warp, so East Harbor inherits Queens' angle and its smoothed value (+0x2DC)
            // eases to the dock angle = the visible swing (the post-arrival DockCamera write lands too late, after
            // the ease has begun). Instead, while the evict is armed and the screen is dark in the Queens fade-out,
            // zero BOTH orbit-angle fields HERE: hidden by the black (and the time-change camera is panned up, so
            // yaw barely shows), and East Harbor then inherits angle 0 — nothing to ease from. One zero suffices
            // (nothing re-writes the orbit before the warp), and the dark window spans many frames, so a coarse
            // tick reliably catches it — unlike the one-frame fully-black warp trigger, which is why THAT is native.
            if (_camActive && Memory.ReadFloat(EditLoop.FadeBoxAlpha) >= FadeSnapAlpha)
            {
                long cam = FollowCamera.Base();
                if (cam != 0)
                {
                    Memory.WriteFloat(cam + FollowCamera.Angle, 0f);
                    Memory.WriteFloat(cam + FollowCamera.AngleNow, 0f);
                }
            }
        }

        internal static void DockCamera()
        {
            if (!_camActive) return;
            if (++_camAge > CamTimeout) { _camActive = false; return; }               // never arrived — give up
            if (Memory.ReadInt(EditLoop.MapNo) != EastHarborMapNo) return;            // still loading / not there yet
            long cam = FollowCamera.Base();
            if (cam == 0) return;
            // ⚠ The map load REBUILDS the camera a beat after MapNo flips to 19, and the rebuild restores the
            // carried-over Queens angle (5.05) — so an early write (while ref is still (0,0,0)/transient) gets
            // wiped. Wait until the DOCK ref is loaded (ref.x ≈ -1311), THEN force everything: by that point the
            // rebuild is done and nothing rewrites it, so it sticks.
            if (Memory.ReadFloat(cam + CamOffRefX) > DockRefLoadedX) return;          // dock ref not loaded yet — keep waiting
            Memory.WriteFloat(cam + CamOffDist, DockCamDist);
            Memory.WriteFloat(cam + CamOffHeight, DockCamHeight);
            Memory.WriteFloat(cam + CamOffAngle, DockCamAngle);         // target
            Memory.WriteFloat(cam + CamOffAngleSmooth, DockCamAngle);   // smoothed = target → snap, no swing

            // The camera EASES into position: base Step__7CCamera interpolates CURRENT pos/ref (+0x260/+0x270)
            // toward NEXT (+0x280/+0x290) each frame — a normal arrival calls Step(-1) to snap them equal, but our
            // warp never does, so the eye glides in from the old spot. Replicate the snap: compute the dock eye
            // (eye = ref + dist·{sin,cos}(angle) + height, matching Step's own formula) and slam BOTH current and
            // next pos/ref to it. Frame-order-independent (we write the values, not copy a maybe-stale "next").
            float refx = Memory.ReadFloat(cam + CamOffRefX);
            float refy = Memory.ReadFloat(cam + CamOffRefY);
            float refz = Memory.ReadFloat(cam + CamOffRefZ);
            float eyeX = refx + DockCamDist * (float)Math.Sin(DockCamAngle);
            float eyeY = refy + DockCamHeight;
            float eyeZ = refz + DockCamDist * (float)Math.Cos(DockCamAngle);
            foreach (long posOff in new[] { CamCurPos, CamNextPos })
            {
                Memory.WriteFloat(cam + posOff + 0, eyeX);
                Memory.WriteFloat(cam + posOff + 4, eyeY);
                Memory.WriteFloat(cam + posOff + 8, eyeZ);
            }
            foreach (long refOff in new[] { CamCurRef, CamNextRef })
            {
                Memory.WriteFloat(cam + refOff + 0, refx);
                Memory.WriteFloat(cam + refOff + 4, refy);
                Memory.WriteFloat(cam + refOff + 8, refz);
            }
            if (_camHeld == 0) Log($"dock camera: dist {DockCamDist}, angle {DockCamAngle}, height {DockCamHeight}, eye=({eyeX:0.#},{eyeY:0.#},{eyeZ:0.#}) @ East Harbor (snap)");
            if (++_camHeld >= CamHold) _camActive = false;
        }

        /// <summary>Is the player down in the canal basin — below the incoming (afternoon) waterline
        /// (Y &le; <see cref="CanalBankY"/>) and inside the canal's Z channel (|z| &le; <see cref="CanalZPad"/>)?
        /// The banks/ladder-top are ≈70, well above the gate, so bank/bridge standers never count.</summary>
        private static bool PlayerInCanal()
        {
            if (!EditLoop.TryReadPlayerPos(out _, out float py, out float pz)) return false;
            return py <= CanalBankY && pz >= -CanalZPad && pz <= CanalZPad;
        }

        /// <summary>Leaving Queens: re-arm the tide-evict and drop the flag so it can't linger into another town.</summary>
        internal static void Reset()
        {
            _evictArm = 0; _flagTtl = 0; _prevTarget = float.NaN;
            Memory.WriteInt(CanalEvictFlag, 0);
            _loggedStaleFlag = false;
        }
    }
}
