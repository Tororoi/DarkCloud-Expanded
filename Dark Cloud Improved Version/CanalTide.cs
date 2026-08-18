using System;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Time-of-day canal tide for Queens (MapNo 2). Two coupled pieces:
    ///
    ///  1) FISHING level — <see cref="QueensWaterLevel"/> is pushed into the injected _LOAD_FISHING_DATA water
    ///     arg (CustomFishingSpot), so fish seed at WaterLevel-depth and the bobber rides it at the tide level.
    ///
    ///  2) VISIBLE surface — the canal you see (walking AND fishing) is the scene mesh node <c>mizu__a01</c>
    ///     (sub-file e03c08): 88 verts at Y~30 spanning the whole canal. It loads via LoadMDSFile as a CFrame
    ///     (size 0x270, name@+0x118, local matrix@+0x1d0 whose translation Y is +0x204, world-dirty flag@+0x240).
    ///     The node matrix is identity at Y=0, so world surface Y = vertexY(30) + matrixTransY. Bumping +0x204
    ///     to (level-30) and clearing +0x240 raises the mesh to the tide level; the scene never rewrites a
    ///     static node's LOCAL matrix, so ONE write PERSISTS — no per-tick pinning (unlike the CWater plane,
    ///     which DrawWaterSurface re-derives every frame from the camera — that was the wrong lever).
    ///
    /// The frame is found by name-scanning the map allocator (LoadMDSFile allocates scene frames from the
    /// CDataAlloc2 at guest 0x1F06650); the address is cached and only re-scanned when the town changes.
    ///
    /// Levels (design, judged in tools/queens_viewer.py): LOW = morning 8 (canal floor exposed and fishable —
    /// climb the ladder down), MEDIUM = afternoon + night 31 (vanilla ~30), HIGH = dusk 52. See
    /// <see cref="TargetY"/> for the live values. Period from the same clock the fishing code reads
    /// (<see cref="Fishing.GetCurrentTimeOfDay()"/>).
    ///
    /// QUEENS-ONLY (MapNo 2).
    /// </summary>
    internal static class CanalTide
    {
        internal static bool Enabled = true;
        internal const int QueensMapNo = 2;

        private const float MizuBaselineY   = 30f;         // mizu__a01 vertex surface height (world, node matrix = 0)
        private const long  FrameName       = 0x118;       // CFrame node-name string
        private const long  FrameMatTransY  = 0x204;       // CFrame local matrix (+0x1d0) row-3 Y
        private const long  FrameWorldDirty = 0x240;       // 0 => world matrix recomputed from local next update
        private static readonly byte[] MizuName = Encoding.ASCII.GetBytes("mizu__a01");

        // ── WADING (current design): at LOW TIDE, the PLAYER is drawn EARLY, mizu stays 100% vanilla ─────
        // GS transparency only reveals what is ALREADY in the framebuffer, so for the submerged body to sit
        // "under" the water it must be in the framebuffer before the water part's own native pass draws.
        // At low tide this class arms the mailbox word the ISO-baked EARLY_STUB reads (IsoPatcher.
        // PatchWaterRedraw: the retargeted `jal DrawWater(ground, 0x15)` at 0x17BB6C): MGDraw(player model
        // root) runs just before the water pass, mizu then draws over the submerged half with its native
        // pass/state, and the normal EdDrawCharacter redraw later is Z-clipped at the waterline — leaving a
        // crisp dry top half over the water-blended lower half.
        //
        // Rejected variants (details in memory/water-surface-and-timeofday.md): permanent/low-tide Z-off,
        // whole-quad colour tint, hide-mizu+MGDraw-post-player (frame+0xB0 gates the visual draw itself),
        // park-part-layer+MGDraw-post-player (drew opaque over the body — authored-opaque and/or missing
        // the native pass's blend state).
        private const float LowTideThreshold = 10f;           // matches QueensLowTide's own threshold
        private const long  CharModelOff = 0xBC;              // CCharacter +0xBC -> model root CFrame
        private const long  ClothListOff = 0xC74;             // CCharacter +0xC74 -> cloth-piece list (cape early-draw)
        private const int   ClothMaxPieces = 4;               // Draw__CCharacter walks 4 cloth slots
        private const int   CapeStableTicks = 4;              // cloth chain must be valid+unchanged this many ticks before the cape is drawn early
        private const float CapeFadeDisarm  = 16f;            // fade alpha (0..128) past which the cape disarms — the fishing model swaps run under the black
        // The town PLAYER's texture group is HARDCODED 8 in EdDrawCharacter (0x172980: `li a2,8` →
        // ReloadTexture → TextureAnime(player, 8)). The +0x148C per-character group field only exists on
        // the VILLAGER array records (stride 0x14A0 off EdDrawCharacter's a3) — on the player object it
        // reads 0, which is what garbled the early draw when we trusted it.
        private const int   PlayerTexGroup = 8;

        // ── CWater quad colour (body+0x120..0x123 R/G/B/A): kept NATIVE in the current design (the
        // underwater blend comes from mizu's own pass over the early-drawn player, not the quad).
        // ApplyLowTideColor(false) each tick self-restores anything an earlier build overrode. Kept as a
        // tuning lever: alpha is the quad's blend factor (0x80 native), RGB modulates the refraction.
        private const long ColorOff = 0x120;
        private const byte BlendR = 0x80, BlendG = 0x80, BlendB = 0x80, BlendA = 0x40;

        // ── REFRACTION DIAGNOSTIC (temporary): hide the textured mizu__a01 mesh and tint the CWater
        // refraction body a very light translucent colour, so the refraction plane is visible on its own —
        // to see whether the "flash between refraction / no refraction" tracks the mizu texture's opacity.
        // Toggle off to restore. Runtime-only (no ISO change), Queens low-tide-independent.
        internal static bool WaterDiag = false;
        private const long FrameDrawFlag = 0xB0;               // CFrame draw gate (bit0; 0 = hidden)
        private const byte DiagR = 210, DiagG = 230, DiagB = 255, DiagA = 0x30;   // light translucent
        private static int _mizuDrawSaved = int.MinValue;      // captured mizu draw flag (restore on diag-off)

        // ── PLAYER RIPPLES (pure data — no EE patch) ─────────────────────────────────────────────────────
        // CWater lives at waterBody+0x90. Layout RE'd from Shake/SetSize/SetVertex/CheckClip:
        //   [0]=grid W, [1]=grid H, [2]=CURRENT height buffer (W*H floats), [3]/[4]=the other wave buffers,
        //   +0x20/+0x30/+0x40/+0x50 = the four LOCAL corner verts, +0xB0 = the water's CFrame (== body+0x140).
        // Shake__6CWater (0x161370) is just `buf[x*H + y] += amp` with the cell clamped to [1, dim-2] — so a
        // plain write into that buffer at the player's cell is a "shake", and the engine's own StepWater/Hamon
        // (0x1a3150 / 0x1611d0, already running every frame for this body) propagates and renders it. That is
        // the whole player-interaction effect, with no code patch: town water has no player-contact path of its
        // own (the dungeon's ring system is separate), so we supply the contact.
        private const long WaterOff = 0x90;                     // CWater within the CEditGround water body
        private const long CwW = 0x00, CwH = 0x04, CwBuf = 0x08;

        // Corners are ABSOLUTE WORLD x/z (see the fix note on PlayerRipple below) — no frame translation
        // needed to interpret them, so CWater's own frame (+0xB0) plays no part in this math.
        private const long CwCorner = 0x20, CwCornerStride = 0x10;
        // ⚠ DEV VALUES — cranked WAY up so the movement disturbance is obvious while we tune it. The cfg
        // WATER_SHAKE baseline is ~0.5; dial RippleAmp back down toward that once the behaviour is right.
        private const float RippleAmp = 12f;                    // per-spike amplitude (dev; baseline ~0.5)
        private const float RippleMinMove = 0.15f;              // only ripple when actually moving through the water
        private const int   RippleRadius = 1;                   // cell radius of the spike (1 = 3x3, rounder wave)
        // Water DISTORTION is now fully NATIVE (e03 mapinfo: the canal body is world-anchored and its
        // WATER_SHAKE sources agitate it — IsoPatcher.WorldAnchorCanalWater). The C# height-buffer poking
        // below (PlayerRipple + probe) is DISABLED; PinRipple (tide-level pin) and RippleDecal (the ring
        // decal) still run. Flip CSharpWaterShake true only to re-enable the old runtime experiments.
        internal static bool CSharpWaterShake = false;
        internal static bool RippleProbe = false;               // DEV: gentle wandering spike, sim-propagated (see PlayerRipple)
        private const float RippleProbeAmp = 1.5f;              // DEV probe spike amplitude (let Hamon spread it)
        private const long  CamPtrVar = 0x21D19678;             // follow-camera ptr (ref/look-at @cam+0x2C0/+0x2C8)
        private const long  WaveSpeedOff = 0x94, WaveDampOff = 0x98;   // Hamon params in CWater (cw = body+0x90)
        private const float RippleDampAdd = 0.25f;              // extra damping to keep the disturbance focused
        private static float _dampOrig = float.NaN;             // captured authored damping (restore/uncompound)
        private const float RippleMoveNorm = 4f;                // movement that yields a full-amplitude spike
        private const float RippleMaxDisp = 3f;                 // HARD cap on cell displacement (kills buildup)

        private static int _rippleSlot = -1;                    // cached canal CWater body index (see CanalBody)
        private static int  _rippleLogTick;                     // throttle for the PlayerRipple diagnostic
        private static float _lastPx, _lastPz;
        private static bool _loggedArm;                // one log line per low-tide arming
        private static uint _lastClothSig;             // signature of the player cloth chain last tick (stability gate for the cape early-draw)
        private static int  _capeStableTicks;          // consecutive ticks the cloth chain has been valid+unchanged
        private static bool _loggedCapeGate;           // one log line when the cape is gated off mid-swap
        private static bool _loggedStaleFlag;          // one log line when a stale evict flag is proactively cleared
        private static int  _colorSaved = -1;          // canal body's OWN colour bytes, captured before we touch them

        internal static bool Diagnostics = true;      // log frame-find + writes while we validate the lever
        private const  long  FadeAlpha    = 0x21D3D1CC;  // Ed time-change fade box alpha: 0 clear .. 128 black
        // Snap the tide while the fade box is this dark (of 128). The mod ticks ~50 ms (~3 frames), so this has
        // to be low enough that a tick reliably lands inside the black window, high enough that the screen is
        // genuinely covered — the fade holds near-black across the whole period change, so 100 catches it.
        private const  float FadeSnapAlpha  = 100f;
        private const  int   FadeGraceTicks = 60;        // ~3 s: no fade seen -> snap anyway, still instantly

        // Canal RIPPLE lever. The animated water is the mapinfo WATER "e03c08" body drawn by
        // DrawWaterSurface__11CEditGround. The CEditGround is *(gp-0x6f18) = *(0x202A28D8) — NOT edit_info; its
        // CWater array is at base+0x15040 (4 bodies, stride 0x3B0), pos.y @ +0x44, active @ +0x20 (Y-follow flag
        // off, so a write to +0x44 holds). The canal body sits at Y31; the two fountain pools (Y113 / Y5.6) are
        // left alone. Re-pinned every frame because a Queens area transition rebuilds the array back to Y31.
        private const long RippleEditGroundPtr = 0x202A28D8;
        private const long RippleArrOff = 0x15040, RippleStride = 0x3B0;
        private const int  RippleSlots  = 4;

        // ── WADING RIPPLE (v7 — static part drawn IN THE WATER PASS via its LAYER; full RE:
        // game_data/docs/water-rendering-re.md §TEXANIME). The look = the plant/stilt ring look: a
        // persistent mesh (Norune's hamon splat, ±39, part "wripple") whose e01b22 texture the town
        // TEX_ANIME animates (ring art baked in by the ISO post-step). DrawWater's static-part loop draws
        // every part whose LAYER field (+0xE4) == the pass arg (0x15) with the water texture group
        // resident — so this class flips the injected part's layer to 0x15 (its normal-layer draw showed
        // garbage: water-group texture, wrong pass) and drives its position (+0x10). The v6 attempt hung
        // the mesh as a CHILD NODE of mizu inside e03c08 — the frame loaded and was driven, but the water
        // part's draw never visited it (parts draw their REGISTERED frame, not the node table); levers and
        // failures catalogued in memory/water-surface-and-timeofday.md.
        private const long StaticPartsOff = 0x15F40, PartStride = 0x2A0;   // static CMapParts array
        private const int  StaticPartCount = 0x40;
        private const long PartPos = 0x10, PartLayer = 0xE4;
        private const int  WaterLayer = 0x15;
        // Two half-size ripple decals on the ladder's vertical rails (injected parts "wriplL"/"wriplR" at
        // world x701/x711, z48 — the carved hasigo rail positions). Fixed XZ from mapinfo; CanalTide flips
        // their layer to the water pass and pins Y to the tide surface, like the player ripple.
        private const float PoleLX = 701f, PoleRX = 711f, PoleZ = 48f;
        private static long _poleL, _poleR;
        private static int  _poleNextScan;
        private static bool _loggedPoles;
        // Ring sits at the MIZU TEXTURE height (world Y = _shownLvl, = MizuBaselineY + frame trans) rather than
        // up at the refraction/distortion plane (level + RefractionYOffset). Just a hair above the mizu mesh so
        // it draws on top of the texture without coplanar z-fight — visually flush with the water surface.
        private const float DecalLift = 0.2f;
        private const float DecalParkY = -3000f;
        private const float DecalStillThresh = 0.5f;       // per-tick x+y+z movement below this = "standing still"
        private const int   DecalStillHold = 3;            // ticks of stillness before the calm ripple appears
        // PROBE: ignore the wading gate and show the ripple everywhere in Queens (still-gated), any tide —
        // visual-confirmation mode. Turn OFF once confirmed.
        internal static bool DecalProbe = false;   // true = diagnostic (ring anywhere when still); false = the real water gate
        private static long _decalPart;                    // cached part (mmu); 0 = unknown
        private static int  _decalNextScan;
        private static float _decalLastY = DecalParkY;
        private static float _decalLastPx, _decalLastPy, _decalLastPz;   // last player x/y/z (standing-still detection)
        private static int  _decalStillTicks;              // consecutive ticks below the movement threshold
        private static bool _decalShown, _loggedDecal;
        private static long  _frame;                   // cached mizu__a01 CFrame (mmu); 0 = unknown
        private static float _shownLvl = float.NaN;    // water level currently displayed (lags target while hidden)
        private static float _lastMizu = float.NaN;    // last level written to the mesh (set-once while stable)
        private static int   _rebakeLvl = int.MinValue;
        private static int   _tick, _nextScan, _pend;  // re-scan throttle + frames a change has waited for a fade
        private static bool  _loggedFound, _loggedMiss;

        private static void Log(string m) { if (Diagnostics) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[CanalTide] " + m); }

        /// <summary>Queens fishing water level for the current time of day (LOW = morning 8, MEDIUM =
        /// afternoon + night 31, HIGH = dusk 52). Pushed into the injected _LOAD_FISHING_DATA water arg
        /// at session setup.</summary>
        internal static float QueensWaterLevel() => TargetY(Fishing.GetCurrentTimeOfDay());

        /// <summary>True at LOW tide — the canal floor is exposed and reachable by the ladder. Derived from the
        /// level itself rather than the time period, so it stays correct if the tide chart is re-tuned.</summary>
        internal static bool QueensLowTide() => QueensWaterLevel() <= 10f;

        internal static void Tick()
        {
            if (!Enabled) return;
            DockCamera();   // post-warp: set the dock camera in East Harbor — runs in ANY map, so BEFORE the Queens bail
            if (Memory.ReadInt(EditLoop.MapNo) != QueensMapNo)
            {
                _frame = 0; _shownLvl = float.NaN; _lastMizu = float.NaN; _rebakeLvl = int.MinValue;
                _rippleSlot = -1; _colorSaved = -1; _loggedArm = false;                   // Queens-only state
                _capeStableTicks = 0; _lastClothSig = 0; _loggedCapeGate = false;          // cape early-draw stability gate
                _loggedStaleFlag = false;                                                 // stale-flag clear log
                _dampOrig = float.NaN; _mizuDrawSaved = int.MinValue;                     // ripple sim + diag state
                _poleL = 0; _poleR = 0; _loggedPoles = false;                             // ladder-rail ripples
                _loggedLadderGate = false;                                                // ladder tide-gate log
                _evictArm = 0; _flagTtl = 0; _prevTarget = float.NaN;                     // re-arm the tide-evict; drop the flag so it can't linger into another town
                Memory.WriteInt(CanalEvictFlag, 0);
                _decalPart = 0; _decalShown = false; _loggedDecal = false; _decalStillTicks = 0;
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);                         // disarm the baked redraw
                ClearSprayTable();                                                        // no waterfall mist outside Queens
                return;
            }
            _tick++;

            float target = QueensWaterLevel();
            if (float.IsNaN(_shownLvl)) _shownLvl = target;    // first frame in town — start at the current level

            // TIDE-EVICT — the timing is owned by NATIVE code now (IsoPatcher.PatchCanalEvictFadeHook hooks
            // EdFadeInOut's fully-black store @0x189970). This side only maintains the flag: ARM while the player
            // wades the drained low-tide canal, and at the period boundary (tide turns low→non-low) raise the
            // native evict flag if they were caught. The fade-hook reads it on the exact fully-black frame and
            // does the _MAP_JUMP to the East Harbor dock (+ clears the flag) — frame-perfect, no fade polling.
            if (_shownLvl <= LowTideThreshold && PlayerInCanal()) _evictArm = EvictArmHold;
            else if (_evictArm > 0) _evictArm--;

            // Don't fire while a fishing session is entering/active: _LOAD_FISHING_DATA perturbs the scene's
            // time/water, which can read as a low→non-low tide jump — a FALSE boundary. A player who chose to
            // fish is not being caught by the rising tide.
            if (!float.IsNaN(_prevTarget) && _prevTarget <= LowTideThreshold && target > LowTideThreshold
                && _evictArm > 0 && !CustomFishingSpot.InFishingWindow)
            {
                Memory.WriteInt(CanalEvictFlag, 1);
                _flagTtl = FlagTtl;
                _camActive = true; _camAge = 0; _camHeld = 0;   // set the dock camera once East Harbor loads
                Log($"tide-evict: caught in draining canal ({_prevTarget:0.#}→{target:0.#}) → raised native evict flag");
            }
            _prevTarget = target;
            // Keep the flag CLEAN between evictions. It's a one-shot the native fade-hook consumes on the next
            // fully-black frame — so a stale/garbage 1 (e.g. left in RAM on a DIRECT boot/state-load into Queens,
            // where the non-Queens reset at the top never ran) would be eaten by the next UNRELATED fade — a
            // fishing-entry fade — and false-warp the player to the dock. While no genuine eviction is pending
            // (TTL==0), pin it to 0; only the boundary above raises it, with a TTL that spans the tide fade.
            if (_flagTtl > 0) { if (--_flagTtl == 0) Memory.WriteInt(CanalEvictFlag, 0); }
            else if (BisectFlags.EvictFlagPin && Memory.ReadInt(CanalEvictFlag) != 0)
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
            if (_camActive && Memory.ReadFloat(FadeAlpha) >= FadeSnapAlpha)
            {
                uint cp = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(cp))
                {
                    long cam = Memory.ToMmu(cp);
                    Memory.WriteFloat(cam + CamOffAngle, 0f);
                    Memory.WriteFloat(cam + CamOffAngleSmooth, 0f);
                }
            }

            // (re)locate the mesh CFrame. A fresh find means the town just (re)loaded — safe to snap under the
            // load's own black screen.
            bool freshFrame = false;
            if (!FrameStillMizu(_frame) && _tick >= _nextScan)
            {
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);   // disarm while the frame is unknown/stale
                _frame = FindMizuFrame();
                if (_frame == 0)
                {
                    _nextScan = _tick + 100;   // ~5s back-off (the scan is heavy)
                    if (!_loggedMiss) { Log("mizu__a01 CFrame not found in 0x20300000-0x21E00000"); _loggedMiss = true; }
                }
                else
                {
                    freshFrame = true; _loggedMiss = false; _lastMizu = float.NaN;
                    if (!_loggedFound) { Log($"mizu__a01 CFrame @0x{_frame:X}"); _loggedFound = true; }
                }
            }

            // The tide is DISCRETE: the surface only ever JUMPS between the per-period levels, never slides.
            // Hide the jump inside the time-change fade — snap while the screen is blacked (fade alpha near
            // 128) or on a fresh town load. If a change somehow never gets a fade (alpha never rises, e.g. the
            // period rolled over while the mod was mid-attach), snap anyway once the grace period is up: an
            // instant change is right even when it isn't hidden. This used to RAMP in that case, which is what
            // made the water visibly slide between levels.
            if (Math.Abs(target - _shownLvl) > 0.01f)
            {
                float alpha = Memory.ReadFloat(FadeAlpha);
                bool hidden = freshFrame || alpha >= FadeSnapAlpha;
                if (hidden || ++_pend > FadeGraceTicks) { _shownLvl = target; _pend = 0; }
            }
            else _pend = 0;

            // Waterfall mist only at LOW tide (the falls meet the drained canal then); clear it otherwise.
            if (_shownLvl <= LowTideThreshold) WriteSprayTable(_shownLvl); else ClearSprayTable();

            // Write the shown level to the SURFACE mesh (CFrame set-once while stable, re-applied on a fresh
            // frame) and to the RIPPLE (CEditGround CWater body, pinned every frame — see PinRipple).
            if (_frame != 0 && (freshFrame || Math.Abs(_shownLvl - _lastMizu) > 0.01f))
            {
                Memory.WriteFloat(_frame + FrameMatTransY, _shownLvl - MizuBaselineY);
                Memory.WriteIntFast(_frame + FrameWorldDirty, 0);   // force world-matrix recompute from local
                _lastMizu = _shownLvl;
            }
            // LOW-TIDE EARLY-PLAYER DRAW (see the WADING note above): arm the baked EARLY_STUB with the
            // PLAYER's model root so it MGDraws the player BEFORE the water part's native pass — mizu then
            // blends over the submerged half with its own native pass/state, and the normal EdDrawCharacter
            // redraw is Z-clipped at the waterline into a crisp dry top half. mizu itself is left entirely
            // alone (native layer, native pass — no hide, no redraw). Re-armed every tick; disarmed at
            // medium/high tide and whenever the player pointer is unreadable.
            bool low = _shownLvl <= LowTideThreshold && BisectFlags.CapeEarlyDraw;
            bool armed = false;
            if (low)
            {
                uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(chara))
                {
                    long charaMmu = Memory.ToMmu(chara);
                    uint root = Memory.ReadUInt(charaMmu + CharModelOff) & Memory.PhysAddrMask;
                    if (Memory.IsValidGuest(root))
                    {
                        // The BODY early-draw (root -> MGDraw) is safe: root swaps atomically. The CAPE
                        // early-draw is NOT: the cave walks char+0xC74 -> a 4-entry CCloth pointer array.
                        // During a model swap (fishing enter/quit swaps c01d<->c01d_turi) that chain is
                        // transiently STALE — non-zero garbage the cave's null-guard can't catch, so
                        // Draw__6CCloth feeds the GS a bad packet and the screen hangs. So arm the cape
                        // ONLY when the whole cloth chain is valid AND has been UNCHANGED for a few ticks
                        // (the model has settled); until then leave CapeCharPtr=0 so the cave skips the
                        // cloth loop (its own null-guard) and just draws the body.
                        uint sig = 0; bool clothOk = true;
                        uint clothList = Memory.ReadUInt(charaMmu + ClothListOff) & Memory.PhysAddrMask;
                        if (clothList != 0)
                        {
                            if (Memory.IsValidGuest(clothList))
                            {
                                sig = clothList;
                                long listMmu = Memory.ToMmu(clothList);
                                for (int i = 0; i < ClothMaxPieces; i++)
                                {
                                    uint piece = Memory.ReadUInt(listMmu + i * 4) & Memory.PhysAddrMask;
                                    if (piece != 0 && !Memory.IsValidGuest(piece)) { clothOk = false; break; }
                                    sig = (sig << 3 | sig >> 29) ^ piece;   // order-sensitive fold
                                }
                            }
                            else clothOk = false;
                        }
                        if (clothOk && sig == _lastClothSig && _capeStableTicks < CapeStableTicks) _capeStableTicks++;
                        else if (!clothOk || sig != _lastClothSig) _capeStableTicks = 0;
                        _lastClothSig = sig;
                        // FADE GATE for the fishing model swaps: the session swaps the player model
                        // (c01d <-> c01d_turi) inside the enter/exit scripts, which run UNDER THE BLACK FADE —
                        // and between two mod ticks the cloth chain can go stale-but-non-null the SAME FRAME
                        // the cave draws it, a race no stability counter can close from C# (the intermittent
                        // black-screen hang). So disarm the cape the moment the screen starts darkening
                        // (alpha > gate, far below full black 128): the swap lands many frames later, well
                        // after the disarm, and a dark screen needs no cape anyway. Unlike the earlier hard
                        // InFishingWindow gate, this KEEPS the cape during actual low-tide fishing — the
                        // player wades the canal mid-session and the early draw is exactly what clips them
                        // at the waterline there.
                        if (Memory.ReadFloat(FadeAlpha) > CapeFadeDisarm) _capeStableTicks = 0;
                        bool capeReady = clothOk && _capeStableTicks >= CapeStableTicks;

                        // GROUP + cape char ptr before the FRAME pointer — the pointer is the stub's gate, so
                        // neither must be observable as stale while the pointer is live.
                        Memory.WriteInt(CodeCaves.MizuRedrawTexGroup, PlayerTexGroup);
                        Memory.WriteInt(CodeCaves.Mailbox.CapeCharPtr, capeReady ? (int)chara : 0);
                        Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, (int)root);
                        armed = true;
                        if (!_loggedArm) { Log($"early-player draw armed (model root 0x{root:X}, tex group {PlayerTexGroup})"); _loggedArm = true; }
                        if (!capeReady && !_loggedCapeGate) { Log($"cape early-draw gated (cloth chain unsettled: list 0x{clothList:X}, ok={clothOk}, stable={_capeStableTicks})"); _loggedCapeGate = true; }
                        else if (capeReady) _loggedCapeGate = false;
                    }
                }
            }
            if (!armed) { Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0); Memory.WriteInt(CodeCaves.Mailbox.CapeCharPtr, 0); if (!low) _loggedArm = false; }

            PinRipple(_shownLvl);
            if (WaterDiag)
            {
                // Hide the textured mizu mesh (capture its draw flag first, re-assert 0 each tick) and tint
                // the refraction body light-translucent so the refraction plane shows on its own.
                if (_frame != 0)
                {
                    if (_mizuDrawSaved == int.MinValue) _mizuDrawSaved = Memory.ReadInt(_frame + FrameDrawFlag);
                    Memory.WriteIntFast(_frame + FrameDrawFlag, 0);
                }
                long db = CanalBody();
                if (db != 0)
                {
                    if (_colorSaved < 0) _colorSaved = Memory.ReadInt(db + ColorOff);
                    int want = DiagR | (DiagG << 8) | (DiagB << 16) | (DiagA << 24);
                    if (Memory.ReadInt(db + ColorOff) != want) Memory.WriteInt(db + ColorOff, want);
                }
            }
            else
            {
                if (_mizuDrawSaved != int.MinValue && _frame != 0)   // restore the mizu mesh
                { Memory.WriteIntFast(_frame + FrameDrawFlag, _mizuDrawSaved); _mizuDrawSaved = int.MinValue; }
                ApplyLowTideColor(false);   // quad stays NATIVE colour
            }
            PlayerRipple(_shownLvl);

            RippleDecal(_shownLvl, low);
            PoleRipples(_shownLvl);
            LadderTideGate(low);

            // Re-bake the FISHING water (baked into the injected script at install) once the shown level has
            // settled on a new level — re-writes only the fishing bytecode, skipped mid-session.
            int t = (int)MathF.Round(target);
            if (Math.Abs(_shownLvl - target) < 0.01f && t != _rebakeLvl)
            { CustomFishingSpot.RebuildFishingScript(); _rebakeLvl = t; }
        }

        /// <summary>Pin the canal ripple (CEditGround CWater body for WATER "e03c08") to the shown tide level so
        /// it tracks the mizu surface. Writes pos.y (+0x44) of the active body sitting in the tide range; the two
        /// fountain pools (Y~113 / ~5.6) are outside it and left alone. Cheap enough to run every frame, which is
        /// needed because a town-area transition rebuilds the array back to the mapinfo Y (31).</summary>
        // Refraction plane Y offset from the mizu mesh surface, to break the Z-fight that made the water
        // flash (both were pinned to the exact tide level). Positive = seat the refraction ABOVE the mesh
        // (toward the overhead camera) so it passes the depth test consistently instead of tying with mizu's
        // Z-write. Tune magnitude/sign if it reads detached; ~1 unit is visually negligible.
        private const float RefractionYOffset = 1.0f;
        private static void PinRipple(float level)
        {
            long b = CanalBody();
            if (b == 0) return;
            float want = level + RefractionYOffset;
            float y = Memory.ReadFloat(b + 0x44);
            if (Math.Abs(y - want) > 0.01f) Memory.WriteFloat(b + 0x44, want);
        }

        /// <summary>Set the refraction quad's blend to neutral-RGB half-alpha while <paramref name="active"/>
        /// (low tide) — the composite's blend factor (see the LOW-TIDE COMPOSITE BLEND note above) — and
        /// restore the native mapinfo-authored colour otherwise. Re-asserted every tick because an area
        /// transition rebuilds the CWater array (same reason PinRipple pins every frame).</summary>
        private static void ApplyLowTideColor(bool active)
        {
            long b = CanalBody();
            if (b == 0) return;
            if (_colorSaved < 0) _colorSaved = Memory.ReadInt(b + ColorOff);
            int want = active ? (BlendR | (BlendG << 8) | (BlendB << 16) | (BlendA << 24)) : _colorSaved;
            if (Memory.ReadInt(b + ColorOff) != want) Memory.WriteInt(b + ColorOff, want);
        }

        /// <summary>The CEditGround CWater body for the canal ("e03c08"), or 0 if not resolvable yet.
        ///
        /// Identified ONCE by height — the two fountain pools sit at ~113 / ~5.6, outside the tide range — and
        /// then CACHED BY SLOT. The cache is what makes LOW TIDE work: once pinned to 6 the canal is both
        /// outside that 20..80 window AND almost exactly the lower fountain's height, so a per-frame height
        /// test would stop finding the canal (dropping the ripple pin) and could mistake the fountain for it.
        /// A town-area transition rebuilds the array back to the mapinfo Y (31) and the cache is dropped on
        /// leaving Queens, so identification always runs from a known-good state.</summary>
        private static long CanalBody()
        {
            uint egGuest = Memory.ReadUInt(RippleEditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egGuest)) return 0;
            long arr = Memory.ToMmu(egGuest) + RippleArrOff;
            if (_rippleSlot >= 0)
            {
                long cached = arr + _rippleSlot * RippleStride;
                return Memory.ReadInt(cached + 0x20) != 0 ? cached : 0;    // still an active body
            }
            for (int i = 0; i < RippleSlots; i++)
            {
                long b = arr + i * RippleStride;
                if (Memory.ReadInt(b + 0x20) == 0) continue;               // inactive body
                float y = Memory.ReadFloat(b + 0x44);
                if (y < 20f || y > 80f) continue;                          // a fountain pool, not the canal
                _rippleSlot = i;
                Log($"canal CWater body = slot {i} (mapinfo Y {y:0.#})");
                DumpWaterBodies(arr);
                return b;
            }
            return 0;
        }

        /// <summary>One-shot dump of all water bodies: Y, grid, LOCAL corner bounds, and the CWater frame's
        /// world translation (frame = CWater+0xB0 = body+0x140; matrix translation @+0x200/+0x204/+0x208).
        /// Reveals whether the corners are world-absolute or local-to-frame, and which body covers the canal
        /// where the player actually wades.</summary>
        private static void DumpWaterBodies(long arr)
        {
            if (!Diagnostics) return;
            for (int i = 0; i < RippleSlots; i++)
            {
                long b = arr + i * RippleStride;
                if (Memory.ReadInt(b + 0x20) == 0) { Log($"  water[{i}]: inactive"); continue; }
                long cw = b + WaterOff;
                int gw = Memory.ReadInt(cw + CwW), gh = Memory.ReadInt(cw + CwH);
                float y = Memory.ReadFloat(b + 0x44);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int c = 0; c < 4; c++)
                {
                    float cx = Memory.ReadFloat(cw + CwCorner + c * CwCornerStride);
                    float cz = Memory.ReadFloat(cw + CwCorner + c * CwCornerStride + 8);
                    minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
                    minZ = Math.Min(minZ, cz); maxZ = Math.Max(maxZ, cz);
                }
                float bpx = Memory.ReadFloat(b + 0x40), bpz = Memory.ReadFloat(b + 0x48);   // body position (+0x40/+0x48)
                float spd = Memory.ReadFloat(cw + WaveSpeedOff), dmp = Memory.ReadFloat(cw + WaveDampOff);
                Log($"  water[{i}]: Y{y:0.#} grid {gw}x{gh} cornersLocal X[{minX:0},{maxX:0}] Z[{minZ:0},{maxZ:0}] bodyPos({bpx:0},{bpz:0}) speed {spd:0.00} damp {dmp:0.00}");
            }
        }


        /// <summary>Ripple the canal where the player wades through it — the town half of the healing-spring
        /// look. Town water has NO player-contact path of its own (the dungeon's expanding-ring system is a
        /// separate stack), so we supply the contact: write an amplitude spike into the CWater height buffer at
        /// the player's grid cell, exactly as <c>Shake__6CWater</c> does, and the engine's own StepWater/Hamon
        /// wave step (already running per frame for this body) propagates and draws it. Pure data — no patch.
        ///
        /// Only spikes while the player is INSIDE the water quad and BELOW the surface, and only when actually
        /// moving (a standing player leaves the surface calm, like the springs).</summary>
        private static void PlayerRipple(float level)
        {
            if (!CSharpWaterShake) return;                                 // water distortion is native now (mapinfo)
            // Throttled bail-reason logger: every ~1 s, report the FIRST gate that stops the ripple, so a
            // "no interaction" report tells us exactly which condition is closed.
            bool dbg = Diagnostics && _tick - _rippleLogTick >= 20;
            void Why(string r) { if (dbg) { _rippleLogTick = _tick; Log($"PlayerRipple gate: {r}"); } }

            long body = CanalBody();
            if (body == 0) { Why("no canal body"); return; }
            long cw = body + WaterOff;

            int gw = Memory.ReadInt(cw + CwW), gh = Memory.ReadInt(cw + CwH);
            if (gw <= 2 || gh <= 2 || gw > 512 || gh > 512) { Why($"bad grid {gw}x{gh}"); return; }
            uint bufGuest = Memory.ReadUInt(cw + CwBuf) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(bufGuest)) { Why($"bad buf 0x{bufGuest:X}"); return; }

            // TIGHTEN: boost the wave-sim DAMPING (Hamon's [0x26] @ cw+0x98 — the friction term
            // `-damping*(cur-old)`) so a player spike dies out near the source instead of spreading into
            // patchy far-waves. Additive (works whether the authored damping is 0 or not); captured once so
            // the boost isn't compounded, re-applied each frame in case an area reload resets it.
            if (float.IsNaN(_dampOrig)) _dampOrig = Memory.ReadFloat(cw + WaveDampOff);
            Memory.WriteFloat(cw + WaveDampOff, _dampOrig + RippleDampAdd);

            if (RippleProbe)
            {
                // The grid is centred on the camera's look-at (ref, which tracks the player), covering
                // ref.xz ± the local corner extent — map the player relative to that. Inject ONLY where the
                // player IS and ONLY when MOVING (a still player pumps no energy, so the water calms and a
                // paused game — frozen position — adds nothing: this replaces the frame-counter gate, which
                // an in-game pause defeats). The written displacement is HARD-CLAMPED so it can never build
                // up regardless of tick/pause timing (the real cause of the burst on unpause).
                if (!EditLoop.TryReadPlayerPos(out float ppx, out float ppy, out float ppz)) { Why("no player pos (probe)"); return; }
                float pmoved = Math.Abs(ppx - _lastPx) + Math.Abs(ppz - _lastPz);
                _lastPx = ppx; _lastPz = ppz;

                uint camG = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(camG)) { Why("no camera"); return; }
                long camM = Memory.ToMmu(camG);
                float refX = Memory.ReadFloat(camM + 0x2C0), refZ = Memory.ReadFloat(camM + 0x2C8);

                float exMinX = float.MaxValue, exMaxX = float.MinValue, exMinZ = float.MaxValue, exMaxZ = float.MinValue;
                for (int c = 0; c < 4; c++)
                {
                    float lx = Memory.ReadFloat(cw + CwCorner + c * CwCornerStride);
                    float lz = Memory.ReadFloat(cw + CwCorner + c * CwCornerStride + 8);
                    exMinX = Math.Min(exMinX, lx); exMaxX = Math.Max(exMaxX, lx);
                    exMinZ = Math.Min(exMinZ, lz); exMaxZ = Math.Max(exMaxZ, lz);
                }
                float sX = exMaxX - exMinX, sZ = exMaxZ - exMinZ;
                float uu = (ppx - refX) / sX + 0.5f;                       // player relative to ref, mapped to [0,1]
                float vv = (ppz - refZ) / sZ + 0.5f;
                int cx = Math.Clamp((int)(uu * (gw - 1)), 1, gw - 2);
                int cy = Math.Clamp((int)(vv * (gh - 1)), 1, gh - 2);

                if (pmoved >= RippleMinMove)
                {
                    long pbuf = Memory.ToMmu(bufGuest);
                    long cell = pbuf + (long)(cx * gh + cy) * 4;
                    float pamp = RippleProbeAmp * Math.Min(1f, pmoved / RippleMoveNorm);
                    float next = Math.Clamp(Memory.ReadFloat(cell) - pamp, -RippleMaxDisp, RippleMaxDisp);
                    Memory.WriteFloat(cell, next);
                }
                if (dbg)
                {
                    _rippleLogTick = _tick;
                    Log($"RippleProbe: player({ppx:0},{ppz:0}) moved {pmoved:0.0} cell({cx},{cy}) of {gw}x{gh}");
                }
                return;
            }

            // ⚠ FIXED 2026-08: this used to read guest 0x21EA1D30 (+4/+8) as a plain (x,y,z) vector — that
            // is Addresses.dunPositionX, the DUNGEON player-position global, and even its OWN (x,y,z)
            // ordering is non-adjacent (Y sits at +0x38, Z at +0x34 — not +4/+8). In town it is unrelated
            // data, so "moved"/"py" were noise: the ripple gate almost never opened correctly, which is
            // the reported "no interaction with the water at all". EditLoop.TryReadPlayerPos is the
            // TOWN-correct read (GeoramaProbe-verified CCharacter CFrame chase).
            if (!EditLoop.TryReadPlayerPos(out float px, out float py, out float pz)) { Why("no player pos"); return; }

            float moved = Math.Abs(px - _lastPx) + Math.Abs(pz - _lastPz);
            _lastPx = px; _lastPz = pz;
            if (py > level) { Why($"above water: py {py:0.0} > level {level:0.0}"); return; }
            if (moved < RippleMinMove) { Why($"not moving: {moved:0.00} < {RippleMinMove}"); return; }

            // ⚠ FIXED 2026-08: this used to ADD the water frame's translation on top of the corners — WRONG,
            // and the reason the ripple never fired even after the player-position fix. Decompiled the actual
            // loader (LoadGroundData +0x0CF8, calls CWater::SetVertex): the 4 corners are copied straight from
            // the mapinfo WATER_SURFACE cfg's min/max (edit_info plane +0x20/+0x30) — i.e. they are ALREADY
            // ABSOLUTE WORLD X/Z, not local offsets from the frame. The frame's own position (SetPosition, same
            // load site) is a SEPARATE thing (corner[2] + an attached MapParts object's position) that the
            // renderer uses for its own model-transform bookkeeping — irrelevant to where the quad's world
            // corners are, which is all this needs. Adding it shifted the computed quad far from the player,
            // so the in-quad test always failed. Min/max over all four corners so the result is independent of
            // their winding.
            // The canal refraction is a camera/player-following infinite plane: the 4 corners are LOCAL
            // offsets (±320 / ±70), and the body's own position (+0x40/+0x48) — updated to track the view
            // each frame by DrawWaterSurface — is where that window sits in the world. So the world quad is
            // bodyPos + localCorner (NOT the raw corners, which centre on the origin and never cover the
            // player's actual canal x ~ 1000).
            float bodyPosX = Memory.ReadFloat(body + 0x40), bodyPosZ = Memory.ReadFloat(body + 0x48);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float cxv = bodyPosX + Memory.ReadFloat(cw + CwCorner + i * CwCornerStride);
                float czv = bodyPosZ + Memory.ReadFloat(cw + CwCorner + i * CwCornerStride + 8);
                minX = Math.Min(minX, cxv); maxX = Math.Max(maxX, cxv);
                minZ = Math.Min(minZ, czv); maxZ = Math.Max(maxZ, czv);
            }
            float spanX = maxX - minX, spanZ = maxZ - minZ;
            if (spanX <= 0.01f || spanZ <= 0.01f) { Why($"degenerate quad {spanX:0}x{spanZ:0}"); return; }

            float u = (px - minX) / spanX;
            float v = (pz - minZ) / spanZ;
            if (u < 0f || u > 1f || v < 0f || v > 1f)                      // outside the water quad
            { Why($"outside quad: p({px:0},{pz:0}) vs X[{minX:0},{maxX:0}] Z[{minZ:0},{maxZ:0}]"); return; }

            int cellX = (int)(u * (gw - 1)), cellY = (int)(v * (gh - 1));

            // Shake: buf[x*H + y] += amp. Spike scaled by how fast the player is wading, capped. Spread it
            // over a small cell neighbourhood (radius RippleRadius, linear falloff) so the disturbance is a
            // rounder, bigger wave than a single-cell poke. Each cell clamped to [1, dim-2] like Shake.
            long bufBase = Memory.ToMmu(bufGuest);
            float amp = -RippleAmp * Math.Min(1f, moved / 4f);
            for (int dx = -RippleRadius; dx <= RippleRadius; dx++)
                for (int dy = -RippleRadius; dy <= RippleRadius; dy++)
                {
                    int cx = Math.Clamp(cellX + dx, 1, gw - 2);
                    int cy = Math.Clamp(cellY + dy, 1, gh - 2);
                    float falloff = 1f - (Math.Abs(dx) + Math.Abs(dy)) / (float)(2 * RippleRadius + 1);
                    long cell = bufBase + (long)(cx * gh + cy) * 4;
                    Memory.WriteFloat(cell, Memory.ReadFloat(cell) + amp * falloff);
                }
            if (Diagnostics && _tick - _rippleLogTick >= 20)              // ~1 s throttle
            {
                _rippleLogTick = _tick;
                Log($"PlayerRipple: cell ({cellX},{cellY}) of {gw}x{gh}, moved {moved:0.0}, amp {amp:0.0}");
            }
        }


        /// <summary>Drive the wading ripple part (see the WADING RIPPLE note above): keep its layer on
        /// the WATER pass and, while wading the canal at LOW tide, hold it just above the waterline under
        /// the player; park it otherwise. The texture animates natively (TEX_ANIME) — position is the only
        /// thing driven. Rings while merely STANDING in the water — no movement gate.</summary>
        private static void RippleDecal(float level, bool low)
        {
            long part = DecalPart();
            if (part == 0) return;
            if (Memory.ReadInt(part + PartLayer) != WaterLayer)
                Memory.WriteInt(part + PartLayer, WaterLayer);      // draw with the water pass (+ its tex group)

            if (!EditLoop.TryReadPlayerPos(out float px, out float py, out float pz)) return;

            // STANDING-STILL gate: this is the CALM ripple — the ring animates in place, so while the
            // player walks the expanding texture smears a trail of rings along the path. Only show it
            // when stationary. (Movement-driven surface disturbance is a separate feature.) Instant hide
            // on motion; a short still-debounce before re-showing so a brief pause mid-stride doesn't
            // flash it. Y is in the sum too: a ladder climb is mostly VERTICAL motion (XZ ~fixed), so
            // tracking only X+Z read as "still" and popped the ring while the player was still descending
            // above the waterline — including Y keeps it hidden until they're actually settled in the water.
            float moved = Math.Abs(px - _decalLastPx) + Math.Abs(py - _decalLastPy) + Math.Abs(pz - _decalLastPz);
            _decalLastPx = px; _decalLastPy = py; _decalLastPz = pz;
            if (moved >= DecalStillThresh) _decalStillTicks = 0;
            else if (_decalStillTicks < DecalStillHold) _decalStillTicks++;
            bool still = _decalStillTicks >= DecalStillHold;

            bool here;
            if (DecalProbe)
                here = still;                                        // probe: anywhere in Queens, when still
            else
            {
                // In the canal water = LOW tide + feet at/below the low surface (on the ladder the feet ride the
                // rungs ABOVE it → py > level → no ring) + inside the canal's Z channel. NOTE: the canal water
                // FOLLOWS THE CAMERA IN X, so its X corners are camera-LOCAL (useless for a world test) — the
                // channel runs the length of X. Only Z is world (Z isn't followed), so gate on the Z band.
                bool inWater = low && py <= level && InCanalZ(pz);
                here = still && inWater;
                if (Diagnostics && still && low && _tick - _rippleLogTick >= 40)
                {
                    _rippleLogTick = _tick;
                    Log($"ripple gate: py {py:0.#} level {level:0.#} pz {pz:0.#} inWater={inWater}");
                }
            }

            if (here)
            {
                MoveDecal(part, px, level + DecalLift, pz);
                if (!_decalShown) { Log("ripple part shown (player standing still)"); _decalShown = true; }
            }
            else if (_decalShown)
            {
                MoveDecal(part, 0f, DecalParkY, 0f);                 // park while moving / out of water
                _decalShown = false;
            }
        }

        private static void MoveDecal(long part, float x, float y, float z)
        {
            Memory.WriteFloat(part + PartPos, x);
            Memory.WriteFloat(part + PartPos + 4, y);
            Memory.WriteFloat(part + PartPos + 8, z);
            _decalLastY = y;
        }

        /// <summary>Drive the two ladder-rail ripple decals: found once by their fixed mapinfo XZ (x701/x711
        /// at z48), then each tick flip their layer to the water pass (so their e01b22 texture resolves, like
        /// the player ripple) and pin Y to the tide surface. XZ stays as placed — the poles don't move.</summary>
        private static void PoleRipples(float level)
        {
            if (_poleL == 0 || _poleR == 0)
            {
                if (_tick < _poleNextScan) return;
                uint egGuest = Memory.ReadUInt(RippleEditGroundPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(egGuest)) return;
                long arr = Memory.ToMmu(egGuest) + StaticPartsOff;
                _poleL = _poleR = 0;
                for (int i = 0; i < StaticPartCount; i++)
                {
                    long pt = arr + i * PartStride;
                    if (Memory.ReadInt(pt + 0xE8) < 0) continue;                  // unplaced slot
                    if (Math.Abs(Memory.ReadFloat(pt + PartPos + 8) - PoleZ) > 6f) continue;
                    float x = Memory.ReadFloat(pt + PartPos);
                    if (Math.Abs(x - PoleLX) < 3f) _poleL = pt;
                    else if (Math.Abs(x - PoleRX) < 3f) _poleR = pt;
                }
                if (_poleL == 0 || _poleR == 0) { _poleNextScan = _tick + 100; return; }
                if (!_loggedPoles) { Log($"pole ripples found (L @0x{_poleL:X}, R @0x{_poleR:X})"); _loggedPoles = true; }
            }
            float y = level + DecalLift;
            long[] poles = { _poleL, _poleR };
            foreach (long p in poles)
            {
                if (Memory.ReadInt(p + PartLayer) != WaterLayer) Memory.WriteInt(p + PartLayer, WaterLayer);
                if (Math.Abs(Memory.ReadFloat(p + PartPos + 4) - y) > 0.01f) Memory.WriteFloat(p + PartPos + 4, y);
            }
        }

        // Canal-ladder tide gate: the injected event points all sit at x≈LAD_X (706) — the climb pair (rec
        // types 4/5) plus our co-located type-3 message point (label 402). CheckEventPoint bails on
        // enabled(+0x00)==0, and EdGetEvent matches ONE point, so flipping enabled by tide switches which one
        // the X-press hits: LOW → climb pair on / message off (real climb); otherwise → climb pair off /
        // message on (the "tide too high" line). Re-asserted every tick so a town rebuild can't strand it.
        private const long EvArrPtr = 0x01D19700, EvCountAddr = 0x01D19704;  // live ED_EVENT_POINT array ptr + count
        private const long EvStride = 0x90, EvEnabled = 0x00, EvType = 0x10, EvLabel = 0x1C, EvPos = 0x50;
        private const int  LadderMsgLabel = IsoPatcher.LADDER_MSG_LABEL;      // == 402
        private const float LadderGateX = 706f, LadderGateXTol = 12f;        // LAD_X ± tol — only our cluster
        private static bool _loggedLadderGate;

        private static void LadderTideGate(bool low)
        {
            uint arrGuest = Memory.ReadUInt(EvArrPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(arrGuest)) return;
            long arr = Memory.ToMmu(arrGuest);
            int count = Memory.ReadInt(EvCountAddr);
            if (count <= 0 || count > 256) return;                            // sanity — array not yet built

            int climbs = 0, msgs = 0;
            for (int i = 0; i < count; i++)
            {
                long rec = arr + i * EvStride;
                int type = Memory.ReadInt(rec + EvType);
                if (type != 3 && type != 4 && type != 5) continue;
                if (Math.Abs(Memory.ReadFloat(rec + EvPos) - LadderGateX) > LadderGateXTol) continue;  // our x706 cluster only
                if (type == 3)
                {
                    if (Memory.ReadInt(rec + EvLabel) != LadderMsgLabel) continue;  // not our message point (e.g. a fishing sign)
                    SetEventEnabled(rec, !low); msgs++;                             // message: on at high tide
                }
                else { SetEventEnabled(rec, low); climbs++; }                       // climb pair: on at low tide
            }
            if (!_loggedLadderGate && (climbs > 0 || msgs > 0))
            {
                Log($"ladder tide-gate wired ({climbs} climb pt, {msgs} message pt; low={low})");
                _loggedLadderGate = true;
            }
        }

        private static void SetEventEnabled(long rec, bool on)
        {
            int want = on ? 1 : 0;
            if (Memory.ReadInt(rec + EvEnabled) != want) Memory.WriteInt(rec + EvEnabled, want);
        }

        // Tide-evict: a player caught in the drained canal when the tide rises (morning→afternoon) is warped
        // to the East Harbor dock under the same black fade. Fired by writing the label-403 event id to the
        // engine's start_event_no — EditLoop runs it, the script's _MAP_JUMP does the full load (see
        // CustomFishingSpot.BuildCanalWarpBytecode). One-shot per Queens visit (the load leaves Queens anyway).
        // Direct _MAP_JUMP: the Queens time-change is script EVENT 132 (RunEvent 0x84, GameMode 0xe). Rather
        // than queue a new event via start_event_no (which would run only AFTER 132 ends), we set the map-jump
        // on the CURRENTLY running event — NextMapNo + arrival StartEventNo + the return code EdEventMode reads.
        private const long  CanalEvictFlag = CodeCaves.Mailbox.CanalEvict; // native fade-hook reads this on the fully-black frame
        private const float CanalBankY     = 20f;                        // must be DOWN AT THE FLOOR (≈0), not on the ladder/bank
                                                                          //   (≈70) — the evict should only catch a player standing in the basin
        // World-X run of the canal, from the static (non-camera-followed) waterfall walls in gedit/e03/scene.scn:
        // obj48 span X 187..1111, taki1 187..1509. The Z-band test alone isn't enough — other walkable y≈0 ground
        // elsewhere in Queens shares the canal's Z band, so bound X to the canal too. (The mizu water can't be used:
        // it follows the camera in X.) Tunable if a genuine wade near either end is missed.
        private const float CanalMinX      = 150f;
        private const float CanalMaxX      = 1550f;
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
        private const long  CamOffDist = 0x2D0, CamOffHeight = 0x2D4, CamOffAngle = 0x2D8, CamOffAngleSmooth = 0x2DC;
        private const long  CamOffRefX = 0x2C0, CamOffRefY = 0x2C4, CamOffRefZ = 0x2C8;   // ref (look-at) xyz used by Step
        private const long  CamCurPos = 0x260, CamCurRef = 0x270, CamNextPos = 0x280, CamNextRef = 0x290; // base CCamera ease pair
        private const float DockCamDist = 79.7f, DockCamHeight = 5.0f, DockCamAngle = 0.0f;
        private const float DockRefLoadedX = -1000f;         // ref.x below this ⇒ the dock ref is loaded (dock = -1311)
        private const int   CamHold = 45, CamTimeout = 600;
        private static bool _camActive;
        private static int  _camAge, _camHeld;

        // ── Waterfall mist ───────────────────────────────────────────────────────────────────────────
        // The engine's own EffectWaterSpray (the Matataki waterfall mist, "shibuki"/飛沫 spray texture) is spawned
        // from MainDraw but hardcoded to Matataki (NowEditMap==1). The queensSprayCave (IsoPatcher.PatchQueensSprayHook,
        // hooked at MainDraw 0x17c5a0) reads THIS table every frame and fires an emitter per entry, so we just keep
        // it populated while in Queens. Layout mirrors the cave: word[0]=count, then count × 0x30 entries
        // { pos x,y,z,w @+0x00 ; spread x,y,z,w @+0x10 ; bias bx,by,bz @+0x20 } starting at +0x10.
        //   pos    — waterfall mouth (from gedit/e03/scene.scn); Y = live surface level so the splash tracks the tide.
        //   spread — [x-scatter, size, z-scatter]: particle cloud size (NOT direction/height).
        //   bias   — added to each particle's initial velocity by the spray-bias shim: by<0 lowers the plume (the
        //            vanilla up-velocity is a fixed ~1-1.5, not a spread param); bx/bz aim the mist horizontally
        //            (facing), which spread can't do (its x is symmetric scatter with no net direction).
        // Each waterfall gets EmittersPerFall emitters spread across its X width. World axes: +Z = south, -Z = north
        // (matches the vanilla spray's slight built-in -Z "north" lean); +X = east. Signs/magnitudes are TUNABLE.
        private const long  SprayTableBase = CodeCaves.QueensSprayTable;
        private const float SprayLeanMag = 0.35f;   // horizontal facing strength — kept BELOW the vertical rise so the
                                                    //   mist mostly goes up (was 1.5, which read too horizontal)
        private const float SprayVzDebias = 0.5f;   // EffectWaterSpray bakes a fixed -0.5 vz ("north") into every
                                                    //   particle; add this so the lean below is what's actually seen
        private const float SprayUpBias     = -0.7f;   // obj48: negative → lower plume (÷~3-ish; tune with the surface look)
        private const float SprayUpBiasTaki = -0.5f;   // taki1: taller (≈½ the original height, per request)
        private static readonly float[] Spread = { 5f, 2f, 5f };   // x-scatter, size, z-scatter
        // Each fall's N emitters fan from (Xc,Zc) across ±(SpanX,SpanZ) — obj48s fan along X (their narrow mouth),
        // taki1 fans along Z (its wide western edge). leanZ gets +SprayVzDebias applied on write. Up = per-fall height bias.
        // obj48 Zc = ±27 (the FRONT of the D collision footprint, where the water lands) — NOT the mesh centre ±37,
        // which sits behind the fall's bottom edge.
        private static readonly (float Xc, float Zc, float SpanX, float SpanZ, int N, float LeanX, float LeanZ, float Up)[] Waterfalls =
        {
            (198f,  -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),   // obj48 @X198, north wall (-Z) → face south (+Z), toward centre
            (198f,   27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),   // obj48 @X198, south wall (+Z) → face north (-Z), toward centre
            (601f,  -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),
            (601f,   27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),
            (1100f, -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),
            (1100f,  27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),
            // taki1: western edge (X≈1262 where the fall meets the canal), fanned along its full Z width (-48..52), facing WEST, taller
            (1262f,   2f, 0f, 50f, 10, -SprayLeanMag, 0f, SprayUpBiasTaki),
        };

        private static void WriteSprayTable(float waterY)
        {
            int idx = 0;
            foreach (var w in Waterfalls)
            {
                for (int k = 0; k < w.N && idx < CodeCaves.QueensSprayMaxEmitters; k++, idx++)
                {
                    float t = w.N == 1 ? 0f : k / (float)(w.N - 1) * 2f - 1f;  // -1..+1 across the fall
                    float x = w.Xc + t * w.SpanX;
                    float z = w.Zc + t * w.SpanZ;
                    long e = SprayTableBase + 0x10 + idx * CodeCaves.QueensSprayEntryStride;
                    Memory.WriteFloat(e + 0x00, x);         Memory.WriteFloat(e + 0x04, waterY);
                    Memory.WriteFloat(e + 0x08, z);         Memory.WriteFloat(e + 0x0C, 1f);
                    Memory.WriteFloat(e + 0x10, Spread[0]); Memory.WriteFloat(e + 0x14, Spread[1]);
                    Memory.WriteFloat(e + 0x18, Spread[2]); Memory.WriteFloat(e + 0x1C, 1f);
                    Memory.WriteFloat(e + 0x20, w.LeanX);              Memory.WriteFloat(e + 0x24, w.Up);
                    Memory.WriteFloat(e + 0x28, w.LeanZ + SprayVzDebias); Memory.WriteFloat(e + 0x2C, 0f);
                }
            }
            Memory.WriteInt(SprayTableBase, idx);   // count LAST — the cave reads this each frame; never expose a partial table
        }

        private static void ClearSprayTable() => Memory.WriteInt(SprayTableBase, 0);

        private static void DockCamera()
        {
            if (!_camActive) return;
            if (++_camAge > CamTimeout) { _camActive = false; return; }               // never arrived — give up
            if (Memory.ReadInt(EditLoop.MapNo) != EastHarborMapNo) return;            // still loading / not there yet
            uint p = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long cam = Memory.ToMmu(p);
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

        /// <summary>Is the player down in the canal basin — below the bank (Y &lt; <see cref="CanalBankY"/>) and
        /// inside the canal's Z channel? Uses <see cref="InCanalZ"/> (X is camera-followed and can't be tested
        /// against the stored corners); the Y gate clears the bank (≈70) so the bank or a bridge above doesn't
        /// count.</summary>
        private static bool PlayerInCanal()
        {
            if (!EditLoop.TryReadPlayerPos(out float px, out float py, out float pz)) return false;
            if (py > CanalBankY) return false;                           // on the bank / above the basin
            if (px < CanalMinX || px > CanalMaxX) return false;          // outside the canal's X run (other low ground shares the Z band)
            return InCanalZ(pz);
        }

        /// <summary>Is world-Z <paramref name="pz"/> inside the canal water's Z span? The canal CWater FOLLOWS
        /// THE CAMERA IN X (follow flags 1,0,0), so its stored X corners are camera-LOCAL and meaningless for a
        /// world test — the channel runs the length of X. Z isn't followed, so the Z corners are world.</summary>
        private static bool InCanalZ(float pz)
        {
            long body = CanalBody();
            if (body == 0) return false;
            long cw = body + WaterOff;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float cz = Memory.ReadFloat(cw + CwCorner + i * CwCornerStride + 8);
                minZ = Math.Min(minZ, cz); maxZ = Math.Max(maxZ, cz);
            }
            return pz >= minZ && pz <= maxZ;
        }

        /// <summary>The injected "wripple" part: the static CMapParts slot whose position is the parked
        /// mapinfo y (the one value we control that is unique). Cache re-validated by y being either our
        /// own last write or the park height; an area transition rebuilds the array back to the mapinfo
        /// placement, so a stale cache self-heals through the same scan.</summary>
        private static long DecalPart()
        {
            if (_decalPart != 0)
            {
                float y = Memory.ReadFloat(_decalPart + PartPos + 4);
                if (Math.Abs(y - DecalParkY) < 1f || Math.Abs(y - _decalLastY) < 1f) return _decalPart;
                _decalPart = 0; _decalShown = false;
            }
            if (_tick < _decalNextScan) return 0;
            uint egGuest = Memory.ReadUInt(RippleEditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egGuest)) return 0;
            long arr = Memory.ToMmu(egGuest) + StaticPartsOff;
            for (int i = 0; i < StaticPartCount; i++)
            {
                long pt = arr + i * PartStride;
                if (Memory.ReadInt(pt + 0xE8) < 0) continue;                  // unplaced slot
                if (Math.Abs(Memory.ReadFloat(pt + PartPos + 4) - DecalParkY) > 1f) continue;
                _decalPart = pt; _decalLastY = DecalParkY;
                if (!_loggedDecal)
                {
                    Log($"ripple part = static slot {i} (layer +0xE4={Memory.ReadInt(pt + PartLayer)} -> {WaterLayer}, " +
                        $"act +0xC4={Memory.ReadInt(pt + 0xC4)}, +0xE8={Memory.ReadInt(pt + 0xE8)})");
                    _loggedDecal = true;
                    // one-shot survey: every placed static part's layer/type/vtable + the fn at vtbl+0x94
                    // (the method DrawWater's layer loop invokes) — tells us how the real water part draws.
                    var sb = new StringBuilder("static parts: ");
                    for (int j = 0; j < StaticPartCount; j++)
                    {
                        long q = arr + j * PartStride;
                        if (Memory.ReadInt(q + 0xE8) < 0) continue;
                        uint vt = Memory.ReadUInt(q + 0xA0) & Memory.PhysAddrMask;
                        uint fn94 = 0;
                        if (Memory.IsValidGuest(vt)) fn94 = Memory.ReadUInt(Memory.ToMmu(vt) + 0x94);
                        sb.Append($"[{j}: L={Memory.ReadInt(q + PartLayer)} T={Memory.ReadInt(q + 0x118)} " +
                                  $"vt=0x{vt:X} f94=0x{fn94:X}] ");
                    }
                    Log(sb.ToString());
                }
                return pt;
            }
            _decalNextScan = _tick + 100;   // ~5 s back-off — the part appears once the scene is loaded
            return 0;
        }

        private static bool FrameStillMizu(long f)
        {
            if (f == 0) return false;
            byte[] n = Memory.ReadBytesBatch(f + FrameName, MizuName.Length);
            if (n == null) return false;
            for (int i = 0; i < MizuName.Length; i++)
                if (n[i] != MizuName[i]) return false;
            return true;
        }

        // Broad one-shot scan of main RAM for the mizu__a01 CFrame's name (the scene CFrames aren't in the
        // model/base allocators, so locate them directly and cache the address). Logs the hit so the region
        // can be pinned down; EditInfo.Base is logged for reference.
        private static long FindMizuFrame()
        {
            uint ei = Memory.ReadUInt(0x202A27B0) & Memory.PhysAddrMask;
            Log($"EditInfo.Base=0x{ei:X} — broad-scanning for mizu__a01…");
            const long START = 0x20300000, END = 0x21E00000;
            const int PAGE = 0x40000;                              // 256 KB pages, overlapped by the needle
            for (long p = START; p < END; p += PAGE - MizuName.Length)
            {
                byte[] buf = Memory.ReadBytesBatch(p, (int)Math.Min(PAGE, END - p));
                if (buf == null) continue;
                int idx = IndexOf(buf, MizuName);
                if (idx >= 0)
                {
                    long frame = p + idx - FrameName;
                    Log($"mizu__a01 name @0x{p + idx:X} -> CFrame 0x{frame:X}");
                    return frame;
                }
            }
            Log("mizu__a01 not found in 0x20300000-0x21E00000");
            return 0;
        }

        private static int IndexOf(byte[] hay, byte[] needle, int from = 0)
        {
            for (int i = Math.Max(0, from); i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        private static float TargetY(TimeOfDay tod) => tod switch
        {
            // 2026-08 low-tide-fishing chart: LOW = morning (canal floor walkable/fishable),
            // MEDIUM = afternoon + night (vanilla 31), HIGH = dusk (arch crown underside is Y=60).
            // Low raised 6 -> 8 (more clearance over the floor for the ladder/wading/fish-depth geometry).
            TimeOfDay.Night   => 31f,
            TimeOfDay.Morning => 8f,
            TimeOfDay.Dusk    => 52f,
            _                 => 31f,   // Afternoon (and any fallback): vanilla
        };
    }
}
