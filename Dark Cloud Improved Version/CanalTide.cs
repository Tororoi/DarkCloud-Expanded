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

        // ── WADING: at LOW TIDE, mizu is REORDERED to draw AFTER the player (hide + native redraw) ───────
        // The natural underwater look = the mesh's own semi-transparent vanilla texture alpha-blending
        // over the submerged body, exactly as it blends over the canal floor. GS transparency only reveals
        // what is ALREADY in the framebuffer, and vanilla draws mizu in the scene pass BEFORE the player —
        // so the body could never sit "under" it (and mizu's Z-write clipped the submerged half outright).
        // The reorder: at low tide this class (1) HIDES mizu from the scene pass via the frame draw-flag
        // (+0xB0 = 0, the same gate CheckEventPoint/VillagerPlacement use), and (2) arms two mailbox words
        // that the ISO-baked MIZU_STUB (IsoPatcher.PatchWaterRedraw, hosted in the compacted GameMode gate)
        // reads at the post-character hook: it rebinds the part's texture group (ReloadTexture) and calls
        // the engine's own MGDraw__FP6CFrame on the frame pointer we provide — drawing mizu after the
        // player, before the capture. Earlier approaches (permanent Z-off: broke waterfall occlusion;
        // low-tide Z-off: body visible but untinted; whole-quad colour tint: tinted rectangle) are
        // superseded — this also restores correct waterfall occlusion at ALL tides (their submerged parts
        // now sit under the late-drawn water texture at low tide).
        //
        // ⚠ If +0xB0=0 turns out to also suppress our direct MGDraw call (untested whether the flag gate
        // sits above or inside the visual draw), the fallback is C#-ONLY: hide the whole part by flipping
        // its layer id (+0xE4) to an unused value and write the PART's LOD frame (+0xB0 on the PART) into
        // the mailbox instead — the baked stub draws whatever pointer it is given.
        private const long FrameDrawFlag = 0xB0;              // CFrame draw gate: 0 = not drawn by the scene pass
        private const float LowTideThreshold = 10f;           // matches QueensLowTide's own threshold

        // Part-table offsets for discovering the canal part's texture group (from GetPartsObject__11CEditGround
        // + Draw__11CEditGround: 128 parts @ ground+0x30, stride 0x2A0; valid if +0xE8 >= 0; id @ +0xF0 matches
        // CWater body+0x34; layer/texture-group @ +0xE4 — the scene pass binds ReloadTexture(layer) per pass).
        private const long PartsArrOff = 0x30, PartStride = 0x2A0;
        private const int  PartCount = 128;
        private const long PartValid = 0xE8, PartId = 0xF0, PartLayer = 0xE4;
        private const long BodyPartId = 0x34;                 // CWater body -> its part id

        // ── LOW-TIDE TINT: the CWater plane draws capturedTexture × vertexColor; Queens' canal colour is
        // authored near-clear (LoadGroundData: SetColor(body, cfg.R,G,B, 0x80) straight from mapinfo —
        // springs look blue purely because their per-room colour is authored blue). With the capture now
        // containing the wading body, overriding the canal body's colour bytes to a blue-green tints the
        // submerged half exactly like a spring. Same low-tide gating as the Z-write toggle; native colour
        // captured once and restored at medium/high so those stay 100% vanilla. Absolute bytes =
        // body+0x120..0x123 (SetColor writes THIS-relative +0x90..93 and THIS is body+0x90 — do not
        // confuse with the CWater struct's OWN base at body+0x90).
        private const long ColorOff = 0x120;
        private const byte TintR = 40, TintG = 140, TintB = 170, TintA = 0x80;

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
        private const float RippleAmp = 0.6f;                   // per-spike amplitude (cfg WATER_SHAKE uses ~0.5)
        private const float RippleMinMove = 0.35f;              // only ripple when actually moving through the water

        private static int _rippleSlot = -1;                    // cached canal CWater body index (see CanalBody)
        private static float _lastPx, _lastPz;
        private static int  _meshDrawFlagSaved = -1;   // mizu__a01's OWN +0xB0 value, captured before we touch it
        private static bool _meshHiddenNow;            // current hidden state, so we only write on a CHANGE
        private static int  _partGroup = -1;           // canal part's texture group (+0xE4), cached per session
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
            if (Memory.ReadInt(EditLoop.MapNo) != QueensMapNo)
            {
                _frame = 0; _shownLvl = float.NaN; _lastMizu = float.NaN; _rebakeLvl = int.MinValue;
                _rippleSlot = -1; _meshDrawFlagSaved = -1; _meshHiddenNow = false;
                _partGroup = -1; _colorSaved = -1;                                        // Queens-only state
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);                         // disarm the baked redraw
                return;
            }
            _tick++;

            float target = QueensWaterLevel();
            if (float.IsNaN(_shownLvl)) _shownLvl = target;    // first frame in town — start at the current level

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
                    _meshDrawFlagSaved = -1; _meshHiddenNow = false;   // re-capture the fresh frame's own flag
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

            // Write the shown level to the SURFACE mesh (CFrame set-once while stable, re-applied on a fresh
            // frame) and to the RIPPLE (CEditGround CWater body, pinned every frame — see PinRipple).
            if (_frame != 0 && (freshFrame || Math.Abs(_shownLvl - _lastMizu) > 0.01f))
            {
                Memory.WriteFloat(_frame + FrameMatTransY, _shownLvl - MizuBaselineY);
                Memory.WriteIntFast(_frame + FrameWorldDirty, 0);   // force world-matrix recompute from local
                _lastMizu = _shownLvl;
            }
            // LOW-TIDE REORDER (see the WADING note above): hide mizu from the scene pass and arm the baked
            // MIZU_STUB to redraw it after the player. Capture the frame's own flag ONCE so "restore" means
            // "whatever it natively was". Gated on the DISPLAYED level (_shownLvl, already fade-hidden), so
            // both flips happen at the exact moment the surface height snaps — behind the fade.
            if (_frame != 0)
            {
                if (_meshDrawFlagSaved < 0) _meshDrawFlagSaved = Memory.ReadInt(_frame + FrameDrawFlag);
                bool wantHidden = _shownLvl <= LowTideThreshold;
                if (wantHidden != _meshHiddenNow)
                {
                    Memory.WriteInt(_frame + FrameDrawFlag, wantHidden ? 0 : _meshDrawFlagSaved);
                    _meshHiddenNow = wantHidden;
                    Log($"mizu__a01 {(wantHidden ? "hidden from scene pass (low tide — redrawn post-player by MIZU_STUB)" : "restored to scene pass (vanilla)")}");
                }
                // Mailbox arming, re-asserted every tick while hidden (cheap, and survives anything that
                // zeroes BSS). GROUP is written before the FRAME pointer — the pointer is the stub's gate,
                // so the group must never be observable as stale/zero while the pointer is live.
                if (wantHidden)
                {
                    int grp = CanalPartGroup();
                    Memory.WriteInt(CodeCaves.MizuRedrawTexGroup, grp);
                    Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, (int)(_frame & Memory.PhysAddrMask));
                }
                else Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);
            }

            PinRipple(_shownLvl);
            ApplyLowTideColor(false);   // tint REJECTED in testing: colour multiplies the whole refraction
                                        // quad — a canal-wide tinted rectangle, not an underwater body. Kept
                                        // calling with false so any previously-applied tint self-restores;
                                        // the natural look comes from the mizu-after-player reorder instead.
            PlayerRipple(_shownLvl);

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
        private static void PinRipple(float level)
        {
            long b = CanalBody();
            if (b == 0) return;
            float y = Memory.ReadFloat(b + 0x44);
            if (Math.Abs(y - level) > 0.01f) Memory.WriteFloat(b + 0x44, level);
        }

        /// <summary>Tint the canal body's colour bytes blue-green while <paramref name="active"/> (low tide),
        /// restore its native mapinfo-authored colour otherwise. The colour multiplies the captured refraction
        /// texture, so with the capture containing the wading body this tints the submerged half spring-style —
        /// see the LOW-TIDE TINT note above. Re-asserted every tick because an area transition rebuilds the
        /// CWater array (same reason PinRipple pins every frame).</summary>
        private static void ApplyLowTideColor(bool active)
        {
            long b = CanalBody();
            if (b == 0) return;
            if (_colorSaved < 0) _colorSaved = Memory.ReadInt(b + ColorOff);
            int want = active ? (TintR | (TintG << 8) | (TintB << 16) | (TintA << 24)) : _colorSaved;
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
                return b;
            }
            return 0;
        }

        /// <summary>The canal part's texture group (its layer id, +0xE4) — what the baked MIZU_STUB must
        /// ReloadTexture before redrawing mizu, since the scene pass binds ReloadTexture(layer) per layer
        /// pass and the character draws will have paged other groups in by hook time. Found by replicating
        /// GetPartsObject: scan the 128 parts for the id the canal CWater body names (+0x34). Cached per
        /// session; falls back to group 1 (the first scene pass) if the scan misses.</summary>
        private static int CanalPartGroup()
        {
            if (_partGroup >= 0) return _partGroup;
            long body = CanalBody();
            if (body == 0) return 1;
            uint egGuest = Memory.ReadUInt(RippleEditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egGuest)) return 1;
            long parts = Memory.ToMmu(egGuest) + PartsArrOff;
            int id = Memory.ReadInt(body + BodyPartId);
            for (int i = 0; i < PartCount; i++)
            {
                long p = parts + i * PartStride;
                if (Memory.ReadInt(p + PartValid) < 0 || Memory.ReadInt(p + PartId) != id) continue;
                _partGroup = Memory.ReadInt(p + PartLayer);
                Log($"canal part = slot {i} (id {id}), texture group {_partGroup}");
                return _partGroup;
            }
            Log($"canal part id {id} not found in parts table — defaulting texture group 1");
            _partGroup = 1;
            return _partGroup;
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
            long body = CanalBody();
            if (body == 0) return;
            long cw = body + WaterOff;

            int gw = Memory.ReadInt(cw + CwW), gh = Memory.ReadInt(cw + CwH);
            if (gw <= 2 || gh <= 2 || gw > 512 || gh > 512) return;        // not a sane grid — bail
            uint bufGuest = Memory.ReadUInt(cw + CwBuf) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(bufGuest)) return;

            // ⚠ FIXED 2026-08: this used to read guest 0x21EA1D30 (+4/+8) as a plain (x,y,z) vector — that
            // is Addresses.dunPositionX, the DUNGEON player-position global, and even its OWN (x,y,z)
            // ordering is non-adjacent (Y sits at +0x38, Z at +0x34 — not +4/+8). In town it is unrelated
            // data, so "moved"/"py" were noise: the ripple gate almost never opened correctly, which is
            // the reported "no interaction with the water at all". EditLoop.TryReadPlayerPos is the
            // TOWN-correct read (GeoramaProbe-verified CCharacter CFrame chase).
            if (!EditLoop.TryReadPlayerPos(out float px, out float py, out float pz)) return;

            float moved = Math.Abs(px - _lastPx) + Math.Abs(pz - _lastPz);
            _lastPx = px; _lastPz = pz;
            if (py > level) return;                                        // above the waterline — not wading
            if (moved < RippleMinMove) return;                             // standing still: leave it calm

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
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float cxv = Memory.ReadFloat(cw + CwCorner + i * CwCornerStride);
                float czv = Memory.ReadFloat(cw + CwCorner + i * CwCornerStride + 8);
                minX = Math.Min(minX, cxv); maxX = Math.Max(maxX, cxv);
                minZ = Math.Min(minZ, czv); maxZ = Math.Max(maxZ, czv);
            }
            float spanX = maxX - minX, spanZ = maxZ - minZ;
            if (spanX <= 0.01f || spanZ <= 0.01f) return;

            float u = (px - minX) / spanX;
            float v = (pz - minZ) / spanZ;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;              // outside the water quad

            int cellX = (int)(u * (gw - 1)), cellY = (int)(v * (gh - 1));
            cellX = Math.Clamp(cellX, 1, gw - 2);                          // Shake's own clamps
            cellY = Math.Clamp(cellY, 1, gh - 2);

            // Shake: buf[x*H + y] += amp. Spike scaled by how fast the player is wading, capped.
            long cell = Memory.ToMmu(bufGuest) + (long)(cellX * gh + cellY) * 4;
            float amp = -RippleAmp * Math.Min(1f, moved / 4f);
            Memory.WriteFloat(cell, Memory.ReadFloat(cell) + amp);
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

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i <= hay.Length - needle.Length; i++)
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
