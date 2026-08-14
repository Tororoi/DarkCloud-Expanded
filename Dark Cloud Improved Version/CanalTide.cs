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
        private static bool _loggedArm;                // one log line per low-tide arming
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
        private const float DecalLift = 0.6f;              // above the surface — coplanar with the mesh = z-fight
        private const float DecalParkY = -3000f;
        private const float DecalStillThresh = 0.5f;       // per-tick x+z movement below this = "standing still"
        private const int   DecalStillHold = 3;            // ticks of stillness before the calm ripple appears
        // PROBE: ignore the wading gate and show the ripple everywhere in Queens (still-gated), any tide —
        // visual-confirmation mode. Turn OFF once confirmed.
        internal static bool DecalProbe = true;
        private static long _decalPart;                    // cached part (mmu); 0 = unknown
        private static int  _decalNextScan;
        private static float _decalLastY = DecalParkY;
        private static float _decalLastPx, _decalLastPz;   // last player x/z (standing-still detection)
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
            if (Memory.ReadInt(EditLoop.MapNo) != QueensMapNo)
            {
                _frame = 0; _shownLvl = float.NaN; _lastMizu = float.NaN; _rebakeLvl = int.MinValue;
                _rippleSlot = -1; _colorSaved = -1; _loggedArm = false;                   // Queens-only state
                _decalPart = 0; _decalShown = false; _loggedDecal = false; _decalStillTicks = 0;
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
            // LOW-TIDE EARLY-PLAYER DRAW (see the WADING note above): arm the baked EARLY_STUB with the
            // PLAYER's model root so it MGDraws the player BEFORE the water part's native pass — mizu then
            // blends over the submerged half with its own native pass/state, and the normal EdDrawCharacter
            // redraw is Z-clipped at the waterline into a crisp dry top half. mizu itself is left entirely
            // alone (native layer, native pass — no hide, no redraw). Re-armed every tick; disarmed at
            // medium/high tide and whenever the player pointer is unreadable.
            bool low = _shownLvl <= LowTideThreshold;
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
                        // GROUP before FRAME pointer — the pointer is the stub's gate, so the group must
                        // never be observable as stale while the pointer is live.
                        Memory.WriteInt(CodeCaves.MizuRedrawTexGroup, PlayerTexGroup);
                        Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, (int)root);
                        armed = true;
                        if (!_loggedArm) { Log($"early-player draw armed (model root 0x{root:X}, tex group {PlayerTexGroup})"); _loggedArm = true; }
                    }
                }
            }
            if (!armed) { Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0); if (!low) _loggedArm = false; }

            PinRipple(_shownLvl);
            ApplyLowTideColor(false);   // quad stays NATIVE colour — the underwater blend now comes from
                                        // mizu's own native pass over the early-drawn player, not the quad
            PlayerRipple(_shownLvl);

            RippleDecal(_shownLvl, low);

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
                return b;
            }
            return 0;
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
            // flash it.
            float moved = Math.Abs(px - _decalLastPx) + Math.Abs(pz - _decalLastPz);
            _decalLastPx = px; _decalLastPz = pz;
            if (moved >= DecalStillThresh) _decalStillTicks = 0;
            else if (_decalStillTicks < DecalStillHold) _decalStillTicks++;
            bool still = _decalStillTicks >= DecalStillHold;

            bool here;
            if (DecalProbe)
                here = still;                                        // probe: anywhere in Queens, when still
            else
            {
                bool inWater = false;
                if (low && py <= level)
                {
                    long body = CanalBody();
                    if (body != 0)
                    {
                        long cw = body + WaterOff;
                        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                        for (int i = 0; i < 4; i++)
                        {
                            float cxv = Memory.ReadFloat(cw + CwCorner + i * CwCornerStride);
                            float czv = Memory.ReadFloat(cw + CwCorner + i * CwCornerStride + 8);
                            minX = Math.Min(minX, cxv); maxX = Math.Max(maxX, cxv);
                            minZ = Math.Min(minZ, czv); maxZ = Math.Max(maxZ, czv);
                        }
                        inWater = px >= minX && px <= maxX && pz >= minZ && pz <= maxZ;
                    }
                }
                here = still && inWater;
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
