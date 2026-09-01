using System;
using System.Text;
using static Dark_Cloud_Improved_Version.CanalTide;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The canal's CWater bodies and ripple decals: resolve the canal body, pin its refraction plane to the
    /// tide, keep/tint its quad colour, and drive the wading ripple + ladder-rail ripple parts (layer flip to
    /// the water pass, position pin). The refraction diagnostic (WaterDiag) lives here too.
    /// </summary>
    internal static class CanalRipples
    {
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

        // Corners are ABSOLUTE WORLD x/z (per the LoadGroundData/SetVertex decompile) — no frame translation
        // needed to interpret them, so CWater's own frame (+0xB0) plays no part in this math.
        private const long CwCorner = 0x20, CwCornerStride = 0x10;
        // Water DISTORTION is fully NATIVE (e03 mapinfo: the canal body is world-anchored and its
        // WATER_SHAKE sources agitate it — IsoPatcher.WorldAnchorCanalWater). The old C# height-buffer
        // poking experiments (PlayerRipple/RippleProbe) were deleted 2026-08 — recoverable via git.
        private const long  WaveSpeedOff = 0x94, WaveDampOff = 0x98;   // Hamon params in CWater (cw = body+0x90)

        private static int _rippleSlot = -1;                    // cached canal CWater body index (see CanalBody)
        private static int  _rippleLogTick;                     // throttle for the ripple-gate diagnostic
        private static int  _colorSaved = -1;          // canal body's OWN colour bytes, captured before we touch them
        private static int  _tick;                     // this class's own tick counter (scan back-offs / log throttles)

        // Canal RIPPLE lever. The animated water is the mapinfo WATER "e03c08" body drawn by
        // DrawWaterSurface__11CEditGround. The CEditGround is *(gp-0x6f18) = *(0x202A28D8) — NOT edit_info; its
        // CWater array is at base+0x15040 (4 bodies, stride 0x3B0), pos.y @ +0x44, active @ +0x20 (Y-follow flag
        // off, so a write to +0x44 holds). The canal body sits at Y31; the two fountain pools (Y113 / Y5.6) are
        // left alone. Re-pinned every frame because a Queens area transition rebuilds the array back to Y31.
        private const long RippleEditGroundPtr = EditGround.EditGroundPtr;
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
        private static long _decalPart;                    // cached part (mmu); 0 = unknown
        private static int  _decalNextScan;
        private static float _decalLastY = DecalParkY;
        private static float _decalLastPx, _decalLastPy, _decalLastPz;   // last player x/y/z (standing-still detection)
        private static int  _decalStillTicks;              // consecutive ticks below the movement threshold
        private static bool _decalShown, _loggedDecal;

        /// <summary>Per-tick: pin the refraction plane to the tide, keep the quad colour native (or the
        /// refraction diagnostic), and drive the wading + ladder-rail ripple decals.</summary>
        internal static void Tick(float level, bool low, long mizuFrame)
        {
            _tick++;
            PinRipple(level);
            if (WaterDiag)
            {
                // Hide the textured mizu mesh (capture its draw flag first, re-assert 0 each tick) and tint
                // the refraction body light-translucent so the refraction plane shows on its own.
                if (mizuFrame != 0)
                {
                    if (_mizuDrawSaved == int.MinValue) _mizuDrawSaved = Memory.ReadInt(mizuFrame + FrameDrawFlag);
                    Memory.WriteIntFast(mizuFrame + FrameDrawFlag, 0);
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
                if (_mizuDrawSaved != int.MinValue && mizuFrame != 0)   // restore the mizu mesh
                { Memory.WriteIntFast(mizuFrame + FrameDrawFlag, _mizuDrawSaved); _mizuDrawSaved = int.MinValue; }
                ApplyLowTideColor(false);   // quad stays NATIVE colour
            }
            RippleDecal(level, low);
            PoleRipples(level);
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

            // In the canal water = LOW tide + feet at/below the low surface (on the ladder the feet ride the
            // rungs ABOVE it → py > level → no ring) + inside the canal's Z channel. NOTE: the canal water
            // FOLLOWS THE CAMERA IN X, so its X corners are camera-LOCAL (useless for a world test) — the
            // channel runs the length of X. Only Z is world (Z isn't followed), so gate on the Z band.
            bool inWater = low && py <= level && InCanalZ(pz);
            bool here = still && inWater;
            if (Diagnostics && still && low && _tick - _rippleLogTick >= 40)
            {
                _rippleLogTick = _tick;
                Log($"ripple gate: py {py:0.#} level {level:0.#} pz {pz:0.#} inWater={inWater}");
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

        internal static void Reset()
        {
            _rippleSlot = -1; _colorSaved = -1;
            _mizuDrawSaved = int.MinValue;                                            // mizu draw-flag diag state
            _poleL = 0; _poleR = 0; _loggedPoles = false;                             // ladder-rail ripples
            _decalPart = 0; _decalShown = false; _loggedDecal = false; _decalStillTicks = 0;
        }
    }
}
