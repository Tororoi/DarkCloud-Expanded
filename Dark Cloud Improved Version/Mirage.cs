using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ungaga's "Mirage" (weapon 354): releasing a charge plants a stationary DECOY at the player's spot.
    /// While it lives (10s, refreshed by a new charge), enemies path toward the decoy instead of the
    /// player — PER ENEMY, so an enemy you HIT drops the illusion and re-targets you.
    ///
    /// Hard-won implementation (6 crashes): two independent PCSX2 limits. (1) Patching HOT, actively-
    /// executing EE code crashes the recompiler → the _GET_POSITION patch is applied ONLY at the COLD
    /// window (in-game entry, before any enemy has called it — same window the ABS patches use).
    /// (2) Executing native code from a PINE-written CAVE crashes when a fresh path is first hit → no
    /// cave stub; the redirect is a 5-word IN-PLACE rewrite (runs in _GET_POSITION's own compiled block)
    /// that reads the player pointer from a per-slot DATA table. The rewrite is UNCONDITIONAL, so a fast
    /// loop keeps the table = the live player position for un-fooled slots; fooled slots hold the decoy.
    /// </summary>
    internal static class Mirage
    {
        private const double DecoySeconds = 10.0;
        private const int    FastTickMs   = 25;    // table maintenance cadence while armed + in a dungeon
        private const int    IdleTickMs   = 150;
        private const int    GuardLoopMotion = 9;   // Ungaga's guard-hold loop (spawn here, not on guard-enter)
        private const int    GuardMoveMotion = 33;  // guard-while-moving; the hold pose oscillates 9<->33 under R1
        private const int    GuardChargeMs   = 500; // hold the guard pose this long before the flash + decoy fire

        private static bool _armed;                 // both caves hosted + dispatch repointed this session (cold)

        private static bool     _decoyActive;
        private static DateTime _decoyDeadline;
        private static float    _dx, _dz, _dy;
        private static readonly bool[] _fooled          = new bool[MirageDecoy.MaxSlots];
        private static readonly bool[] _brokenThisDecoy = new bool[MirageDecoy.MaxSlots];
        private static int[] _prevHp;

        internal static void Start() => new Thread(Loop) { IsBackground = true }.Start();

        /// <summary>Arm both engine redirects at the COLD window (from ApplyNewChanges, retried from the loop).
        /// _GET_POSITION and _GET_DISTANCE are hosted in COLD-PINE CAVES reached via the STB external-command
        /// dispatch table — a pure DATA path (see docs/cave-code-execution.md), so there's no in-place code
        /// surgery and nothing to defer. Fill the per-slot pointer table first so the first enemy read is valid.</summary>
        internal static void ArmColdPatch()
        {
            if (_armed) return;
            FillWholeTablePlayer();                 // every slot → live-player pointer (valid before any enemy read)
            bool pos  = ArmFuncCave(0x201E1DF0, 0x21F34200, 0x01F34200, MirageDecoy.PosDispatch, 0x84, 0x8C, "_GET_POSITION");
            bool dist = ArmFuncCave(0x201E1D00, 0x21F34000, 0x01F34000, MirageDecoy.DistDispatch, 0x5C, 0x64, "_GET_DISTANCE");
            if (HeatHaze) ArmHazeCave();            // only when the PNACH jal patch (0x1DAEBCC) is present (HeatHaze on)
            _armed = pos && dist;                   // retry from the loop if either couldn't arm (e.g. not vanilla yet)
        }

        // Heat-haze on the clone: the dungeon's fire-source heat-haze pass (DrawRaster__11CDungeonMap, 0x1C4610)
        // is called from Draw__11CSeireiKing @0x1DAEBCC — the same dungeon draw that renders the clone. The PNACH
        // redirects that jal (in-dungeon, all flag states) to this COLD-written cave stub, which calls the ORIGINAL
        // pass (normal fires) then, gated by HazeFlag, RasterStep + DrawRaster__9CFireOmni (0x162310) on a fake
        // CFireOmni the mod positions at the decoy → a heat shimmer on the clone. Reached via jal (like ClothStep);
        // the cold-written cave compiles fresh on first draw.
        private const long HazeStub   = 0x21F34400;   // cave stub (guest 0x01F34400)
        private const uint HazeStubGuest = 0x01F34400;
        private const long HazeFireObj = 0x21F34500;  // fake CFireOmni (guest 0x01F34500): +0x04 phase, +0x20/24/28 pos
        private const long HazeFlag   = 0x21F34540;    // mod-controlled: 1 = draw the clone haze, 0 = skip
        private const long HazeTexResult = 0x21F34544; // stub stores the GetTexture("blender") result here (0 = not loaded)
        internal static void ArmHazeCave()
        {
            // BISECT BUILD: skips the actual DrawRaster__9CFireOmni call (the suspected crash — blendTextuerTest's
            // framebuffer distortion), keeps flag-check + GetTexture guard + RasterStep, and STORES the GetTexture
            // result to HazeTexResult so we learn (a) whether the crash is in DrawRaster and (b) whether "blender"
            // is even loaded. Compare-and-rewrite (not a version byte) so ANY edit auto-applies on a town re-arm.
            uint[] code = {
                // MINIMAL PASS-THROUGH: do NOTHING, just return. Isolates whether the crash is the jal→cave
                // mechanism on the flag=1 (clone-draw) path vs. calling DrawRaster after the clone draw. Skips the
                // fire pass (no fire haze) but that's fine for the test. jal set ra=0x1DAEBD4; jr ra returns there.
                0x03E00008, // 00 jr ra
                0x00000000, // 04 nop
            };
            byte[] cb = new byte[code.Length * 4];
            for (int i = 0; i < code.Length; i++) BitConverter.GetBytes(code[i]).CopyTo(cb, i * 4);
            byte[] cur = Memory.ReadBytesBatch(HazeStub, cb.Length);
            if (cur != null && cur.AsSpan().SequenceEqual(cb)) return;   // already current → don't touch (cold-only anyway)
            Memory.WriteBytesBatch(HazeStub, cb);
            Memory.WriteBytesBatch(HazeFireObj, new byte[0x40]);   // zero the fake CFireOmni
            Memory.WriteInt(HazeFlag, 0);
            Memory.WriteInt(HazeTexResult, 0);
            Console.WriteLine($"[Mirage/haze] cave stub armed @0x{HazeStubGuest:X} (MINIMAL: jr ra only — no fire pass, no haze; tests the jal→cave mechanism on the clone-draw path)");
        }

        private static bool _hazeStubLogged;
        /// <summary>Position the fake CFireOmni at the decoy and enable the haze draw. Called while the clone is up.</summary>
        private static void MaintainHaze()
        {
            if (!HeatHaze) return;
            if (!_hazeStubLogged)   // BEFORE enabling: report which stub is actually live (disambiguates restart state)
            {
                _hazeStubLogged = true;
                uint s5c = (uint)Memory.ReadInt(HazeStub + 0x5C);   // bisect stub: 0x00000000; full stub: 0x0C0588C4
                Console.WriteLine($"[Mirage/haze] ACTIVE stub +0x5C=0x{s5c:X8} ({(s5c == 0 ? "BISECT (DrawRaster skipped)" : s5c == 0x0C0588C4 ? "FULL (old — re-arm from TOWN!)" : "?")})");
            }
            Memory.WriteFloat(HazeFireObj + 0x20, _dx);
            Memory.WriteFloat(HazeFireObj + 0x24, _dz);   // DrawRaster raises this by +3
            Memory.WriteFloat(HazeFireObj + 0x28, _dy);
            if (EnableHazeDraw) Memory.WriteInt(HazeFlag, 1);   // DIAGNOSTIC OFF: if it STILL crashes with the haze
                                                               // branch never taken, the crash isn't the haze draw
                                                               // at all — it's the stub's presence + the clone draw.
        }
        private const bool EnableHazeDraw = false;
        private static void HazeOff() { if (HeatHaze) Memory.WriteInt(HazeFlag, 0); }

        // ── Clone heat-haze by HIJACKING an existing torch's fire-raster (pure data; no cave, no crash) ──
        // The ONLY framebuffer distortion in the game is CFireOmni::DrawRaster (0x162310, via
        // blendTextuerTest + MGGetFBuffTex); it's driven by DrawRaster__11CDungeonMap (0x1C4610), which
        // iterates the 20×20 fire-tile array at dngMap+0x9C50 (0x10/entry: +0=fireIdx, +4=rot,
        // +8=dist(≤240 draws), +C=enabled) and, per enabled tile, draws the raster emitters of the fire
        // struct at dngMap+fireIdx*0x1D0 (raster count @+0x4A2, emitter[0] local pos @+0x4B0/4B4/4B8) at
        // world (localX*10 + col*160, localY*10, localZ*10 + row*160).
        //
        // The earlier "make a NEW fire tile" version broke floor collision (marking a floor tile as a
        // fire made the engine treat it as fire-tile geometry) — the tile-array write, NOT the struct
        // write, was the culprit (the ForceRaster probe wrote +0x4A2 on a real torch struct with NO
        // collision effect). So instead we reuse an EXISTING enabled torch tile's struct: set its raster
        // count=1 and point emitter[0] at the CLONE (using that tile's col/row as the anchor, so any tile
        // works no matter how far). The torch keeps its flame (flame emitters live at +0x490, untouched,
        // and the raster is now positioned at the clone, not overlapping the torch). Only struct writes —
        // the collision-safe ones. Prefer a torch whose fireIdx no OTHER enabled tile shares (else every
        // sharer would draw a second raster at its own offset). Restored on despawn.
        private const bool  CloneFireHaze = true;
        private const float HazeBack      = 30f;   // world units to pull the shimmer BACK along the clone's facing (raster sits forward)
        private static float _decoyFwdX, _decoyFwdY;  // clone's normalized forward vector (X/Y plane), captured at cast
        private const float HazeBodyY     = -17f;  // world height above the clone's feet to center the shimmer (the raster is
                                                   // designed to rise above a fire, so it renders high; pull it down onto the body)
        private static int  _hazeFireIdx  = -1;    // reused torch's fire-struct index (-2 = checked/none, -1 = unchecked)
        private static int  _hazeTileIdx  = -1;    // reused torch's TILE index (for the dist-gate override)
        private static int  _hazeCol, _hazeRow;    // that torch tile's col/row (emitter anchor for re-assert)
        private static int  _hazeRot;              // that torch tile's rotation index (+0x9c54) — DrawRaster rotates the emitter by it
        private static byte[] _hazeSaved;          // saved struct raster region (+0x4A0..+0x4C0)

        private static void SetupCloneHaze(uint dngMap)
        {
            if (!CloneFireHaze || _hazeFireIdx != -1) return;        // -1 only = not yet checked this decoy
            if (dngMap == 0 || dngMap >= 0x02000000) return;
            // Anchor near the cast spot first (player is there at cast). Maintain re-anchors to the live
            // player as they move. If the dungeon has NO fire at all, UpdateAnchor returns false → skip.
            if (!UpdateAnchor(dngMap, _dx, _dy)) { _hazeFireIdx = -2; return; }
            Console.WriteLine($"[Mirage/haze] anchored to torch fireIdx {_hazeFireIdx} @tile ({_hazeCol},{_hazeRow}); dynamic re-anchor ON");
        }

        // Point the raster emitter at the clone and force the anchor tile's dist so the ≤240 gate passes.
        private static void WriteHazeRaster(uint dngMap)
        {
            long b  = Memory.ToMmu(dngMap);
            long fs = b + (long)_hazeFireIdx * 0x1D0;
            // DrawRaster places the emitter at worldX=(localX+col*16)*10, worldZ=(localZ+row*16)*10 AFTER
            // rotating (localX,localZ) by θ=(4-rot)*90° (the tile's own orientation). Pre-apply the inverse
            // rotation so it lands exactly on the clone regardless of which torch (and its rot) we borrowed.
            float tx = _dx - HazeBack * _decoyFwdX, ty = _dy - HazeBack * _decoyFwdY;   // pull back along the clone's facing
            double dxc = (tx - _hazeCol * 160) / 10.0, dzc = (ty - _hazeRow * 160) / 10.0;
            double th = (4 - _hazeRot) * (System.Math.PI / 2.0), c = System.Math.Cos(th), s = System.Math.Sin(th);
            Memory.WriteBytesBatch(fs + 0x4A2, new byte[] { 1, 0 });          // raster emitter count = 1
            Memory.WriteFloat(fs + 0x4B0, (float)(dxc * c - dzc * s));        // emitter[0] local X (inverse-rotated) → worldX = _dx
            Memory.WriteFloat(fs + 0x4B4, (_dz + HazeBodyY) / 10f);           // emitter[0] local Y → worldY = _dz+HazeBodyY
            Memory.WriteFloat(fs + 0x4B8, (float)(dxc * s + dzc * c));        // emitter[0] local Z (inverse-rotated) → worldZ = _dy
            // (dist gate is relaxed via PNACH during a decoy — no point forcing +0x08 here; DrawMap rewrites it each frame.)
        }

        // Restore the CURRENT anchor's struct to what it was before we hijacked it.
        private static void RestoreAnchor(uint dngMap)
        {
            if (_hazeFireIdx < 0 || dngMap == 0 || dngMap >= 0x02000000) return;
            if (_hazeSaved != null) Memory.WriteBytesBatch(Memory.ToMmu(dngMap) + (long)_hazeFireIdx * 0x1D0 + 0x4A0, _hazeSaved);
        }

        // Choose the enabled fire tile nearest (refX,refY) as the raster anchor; hand the raster off to a
        // nearer tile when the reference (player/camera) moves out of the current anchor's draw window, so
        // the shimmer keeps rendering wherever the camera roams. Returns false only if the dungeon has NO
        // fire (→ no distortion textures resident → skip entirely, no crash). Writes the emitter each call.
        private static bool UpdateAnchor(uint dngMap, float refX, float refY)
        {
            long b = Memory.ToMmu(dngMap);
            // Keep the current anchor while it's still close enough to the reference to stay in the camera's
            // ±4-tile window (≈480 u = 3 tiles) — avoids per-frame thrashing between equidistant torches.
            if (_hazeFireIdx >= 0 && _hazeTileIdx >= 0)
            {
                double kx = _hazeCol * 160.0 - refX, ky = _hazeRow * 160.0 - refY;
                if (kx * kx + ky * ky <= 480.0 * 480.0) { WriteHazeRaster(dngMap); return true; }
            }
            byte[] tiles = Memory.ReadBytesBatch(b + 0x9C50, 400 * 0x10);   // one batch read of the whole fire-tile grid
            if (tiles == null) return _hazeFireIdx >= 0;
            int bestT = -1, bestFi = -1; double bestD2 = double.MaxValue;
            for (int t = 0; t < 400; t++)
            {
                int off = t * 0x10;
                if (BitConverter.ToInt32(tiles, off + 0x0C) != 1) continue;
                int fi = BitConverter.ToInt32(tiles, off + 0x00);
                if (fi < 1 || fi > 200) continue;
                double wx = (t % 20) * 160.0, wz = (t / 20) * 160.0;
                double d2 = (wx - refX) * (wx - refX) + (wz - refY) * (wz - refY);
                if (d2 < bestD2) { bestT = t; bestFi = fi; bestD2 = d2; }
            }
            if (bestT < 0) return _hazeFireIdx >= 0;     // no enabled fire found this scan (keep any current anchor)
            if (bestT != _hazeTileIdx || bestFi != _hazeFireIdx)
            {
                RestoreAnchor(dngMap);                   // hand the raster back to the old torch before taking a new one
                _hazeTileIdx = bestT; _hazeFireIdx = bestFi; _hazeCol = bestT % 20; _hazeRow = bestT / 20;
                _hazeRot = BitConverter.ToInt32(tiles, bestT * 0x10 + 0x04);   // tile rotation → inverse-rotated in WriteHazeRaster
                _hazeSaved = Memory.ReadBytesBatch(b + (long)bestFi * 0x1D0 + 0x4A0, 0x20);
            }
            WriteHazeRaster(dngMap);
            return true;
        }

        private static void MaintainCloneHaze(uint dngMap)
        {
            if (!CloneFireHaze || _hazeFireIdx < 0 || dngMap == 0 || dngMap >= 0x02000000) return;
            float px = Memory.ReadFloat(Addresses.dunPositionX);   // live player = camera proxy; anchor follows it
            float py = Memory.ReadFloat(Addresses.dunPositionY);
            UpdateAnchor(dngMap, px, py);
        }

        private static void TeardownCloneHaze(uint dngMap)
        {
            RestoreAnchor(dngMap);
            _hazeFireIdx = -1; _hazeTileIdx = -1; _hazeSaved = null;
        }

        /// <summary>Host a CLEAN copy of a vanilla STB command function in a cold-PINE cave and repoint its
        /// dispatch-table slot at the copy — no in-place surgery (see docs/cave-code-execution.md). Byte-copies
        /// the self-contained function (absolute jal, PC-relative branches), detours only its player branch —
        /// `addiu a0,sp,0x40` (dest) stays; the hardcoded `0x1EA1D30` load at <paramref name="detourOff"/> becomes
        /// `j helper / nop` — and adds a helper (cave+0x100) that sets a1 = *(PtrTable + slot*4) (the per-slot
        /// target: fooled→decoy, else→live player), then jumps back to the sceVu0CopyVector jal at
        /// <paramref name="jalOff"/>. Explicit-coord queries (param!=1) are untouched. Returns true if armed (or
        /// already armed). Aborts if the function isn't pristine vanilla (stale in-place patch → restart game).</summary>
        private static bool ArmFuncCave(long vanillaFn, long cave, uint caveGuest, long dispatch, int detourOff, int jalOff, string name)
        {
            static uint J(uint target) => 0x08000000u | ((target >> 2) & 0x03FFFFFF);   // absolute jump encoding
            uint vanillaGuest = (uint)(vanillaFn - 0x20000000);
            uint slot = (uint)Memory.ReadInt(dispatch);
            if (slot == caveGuest) return true;                                  // already armed
            if (slot != vanillaGuest) { Console.WriteLine($"[Mirage/cave] {name} dispatch = 0x{slot:X} (expected 0x{vanillaGuest:X}) — abort"); return false; }
            if ((uint)Memory.ReadInt(vanillaFn) != 0x27BDFFB0 ||                  // vanilla prologue (addiu sp,-0x50)
                (uint)Memory.ReadInt(vanillaFn + detourOff) != 0x3C0201EA)       // vanilla player-addr load (lui v0,0x1ea)
            { Console.WriteLine($"[Mirage/cave] {name} not pristine vanilla (stale in-place patch? restart the game) — abort"); return false; }

            byte[] fn = Memory.ReadBytesBatch(vanillaFn, 0xF0);
            if (fn == null) return false;
            BitConverter.GetBytes(J(caveGuest + 0x100)).CopyTo(fn, detourOff);   // j helper — was lui v0,0x1ea
            BitConverter.GetBytes(0u).CopyTo(fn, detourOff + 4);                 // nop — was addiu a1,v0,0x1d30 (j delay)

            uint[] helper = {
                0x8F889CE0,                   // lw   t0, -0x6320(gp)   ; NowMonstorUnit
                0x8D080090,                   // lw   t0, 0x90(t0)      ; current enemy slot
                0x00084080,                   // sll  t0, t0, 2         ; slot*4
                0x3C0501F3,                   // lui  a1, 0x01F3
                0x00A82821,                   // addu a1, a1, t0        ; PtrTable + slot*4
                0x8CA50000,                   // lw   a1, 0(a1)         ; a1 = per-slot target pointer
                J(caveGuest + (uint)jalOff),  // j    cave+jalOff       ; back into the sceVu0CopyVector jal
                0x00000000                    // nop (j delay)
            };
            byte[] hb = new byte[helper.Length * 4];
            for (int i = 0; i < helper.Length; i++) BitConverter.GetBytes(helper[i]).CopyTo(hb, i * 4);

            Memory.WriteBytesBatch(cave, fn);
            Memory.WriteBytesBatch(cave + 0x100, hb);
            Memory.WriteUInt(dispatch, caveGuest);
            Console.WriteLine($"[Mirage/cave] {name} → clean cave @0x{caveGuest:X} (per-slot pointer). readback 0x{(uint)Memory.ReadInt(dispatch):X}");
            return true;
        }

        /// <summary>Patch the dungeon draw's chara-loop scene gate so the clone draws while enemies keep
        /// moving. At MMU 0x21DAE8A4 the gate is `beq v0,zero,skip` (word 0x10400037, skipping the 6-chara
        /// loop when iGpffff9e18==0); NOP it (0x00000000) → the loop always runs, iGpffff9e18 stays 0 so
        /// motionDrive keeps stepping enemies. Verifies the exact word before writing. The dun overlay
        /// reloads per floor, so call this at floor load (like EnemyModelInjector.Install). EXPERIMENTAL:
        /// PINE code writes can crash PCSX2 (per EnemyModelInjector) — testing whether a single beq→nop is OK.</summary>
        internal const long SceneGateAddr = 0x21DAE8A4;
        internal const uint SceneGateWord = 0x10400037;   // beq v0,zero,0x1dae984
        // The scene-gate NOP is done by the PNACH (PCSX2 applies code patches safely; PINE crashes). The
        // PNACH conditional writes 0 to 0x1DAE8A4 only while this flag == 1, so it never touches the town
        // overlay that shares the address. Mod sets it 1 while a decoy is up (in a dungeon), 0 otherwise.
        internal const long SceneGateFlag = 0x21F10038;   // guest 0x01F10038 (mod PNACH mailbox; free slot)
        internal static void ApplySceneDrawPatch()
        {
            uint cur = (uint)Memory.ReadInt(SceneGateAddr);
            if (cur == 0x00000000) { Console.WriteLine("[Mirage] scene-draw gate already NOP'd"); return; }
            if (cur != SceneGateWord) { Console.WriteLine($"[Mirage] scene-draw gate word 0x{cur:X8} != 0x{SceneGateWord:X8} — NOT patching"); return; }
            Memory.WriteInt(SceneGateAddr, 0x00000000);
            uint back = (uint)Memory.ReadInt(SceneGateAddr);
            Console.WriteLine($"[Mirage] scene-draw gate patched 0x{SceneGateWord:X8}→0x{back:X8} (chara loop always runs)");
        }

        private static void Loop()
        {
            bool guardLatched = false;
            DateTime guardPoseSince = default;
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool inDun = Player.InDungeonFloor();
                    if (!_armed && !inDun) ArmColdPatch();

                    if (inDun) ProbeCharacter();   // once per party member: node/mesh/cloth footprint for cave sizing
                    if (FireProbe && inDun && DateTime.UtcNow >= _fireProbeNext)   // map the dungeon fire-tile data (idea 1)
                    { _fireProbeNext = DateTime.UtcNow.AddSeconds(2); DumpFireData(); }

                    if (_armed && inDun)
                    {

                        // Keep un-fooled slots on the live player (the patch reads the table for EVERY
                        // enemy, always) and fooled slots on the decoy — one batched write per fast tick.
                        bool ungagaMirage = Player.Weapon.GetCurrentWeaponId() == Items.mirage &&
                                            Player.CurrentCharacterNum() == Player.UngagaId;
                        bool paused = IsPaused();   // "PAUSE" screen OR the in-dungeon item menu — freeze the decoy for both

                        if (ungagaMirage && !paused)
                        {
                            // Plant the decoy ONCE per guard-hold: the held-guard pose oscillates between motion 9
                            // (loop) and 33 (move) under R1, so we can't edge-trigger on a single motion. Latch on
                            // the first hold-pose motion while guarding and only clear the latch when R1 is released
                            // (guard exited) — so re-entering guard plants a fresh decoy, but 9<->33 doesn't.
                            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
                            int  mid = Memory.ReadInt(MirageClone.PlayerChar + MirageClone.MotionId);
                            bool inGuardPose = guarding && (mid == GuardLoopMotion || mid == GuardMoveMotion);
                            if (!guarding) { guardLatched = false; guardPoseSince = default; }   // released guard → re-arm
                            if (inGuardPose && !guardLatched)
                            {
                                // Hold the guard pose for GuardChargeMs, THEN flash the player (Mobius-charge
                                // style) and plant the decoy at that same moment. One flash+decoy per guard-hold.
                                if (guardPoseSince == default) guardPoseSince = DateTime.UtcNow;
                                else if (DateTime.UtcNow - guardPoseSince >= TimeSpan.FromMilliseconds(GuardChargeMs))
                                {
                                    Player.FlashActiveCharacter(0f, 122f, 208f, 15f, 1);
                                    PlaceDecoy();
                                    guardLatched = true;
                                }
                            }
                            if (_decoyActive) UpdateDecoyState();
                        }
                        else if (_decoyActive && paused)
                        {
                            // Freeze while paused: hold the deadline (timer stops) and keep the clone drawn — the
                            // flag-3 gate below freezes its step + cloth. Covers BOTH pause types: the item menu
                            // (engine already freezes the clone there) and the PAUSE screen (where the chara loop
                            // otherwise keeps stepping the clone).
                            _decoyDeadline = _decoyDeadline.AddMilliseconds(FastTickMs);
                            MaintainClone();
                        }
                        else if (!ungagaMirage && _decoyActive)
                        {
                            _decoyActive = false; Array.Clear(_fooled, 0, _fooled.Length);
                            DespawnClone();
                        }

                        WriteTable();   // fills the per-slot table both _GET_POSITION and _GET_DISTANCE now read
                        // PNACH gate flag: 1 = clone drawn → NOP the chara-loop gates; 2 = in a dungeon w/o a decoy
                        // → RESTORE the vanilla gates (they don't auto-revert). 0 (town) is set below so the shared
                        // town overlay at those addresses is never touched.
                        // 1 = decoy up & running (NOP scene+step gates); 3 = decoy up but PAUSED (NOP scene only →
                        // clone still drawn but frozen); 2 = dungeon, no decoy (restore vanilla).
                        Memory.WriteInt(SceneGateFlag, (_decoyActive && _cloneSlot >= 0) ? (paused ? 3 : 1) : 2);
                        sleep = FastTickMs;
                    }
                    else
                    {
                        guardLatched = false;
                        if (_decoyActive || _cloneSlot >= 0) { _decoyActive = false; DespawnClone(); }
                        Memory.WriteInt(SceneGateFlag, 0);   // town: leave the gates to the overlay reload
                    }
                }
                catch (Exception e) { Console.WriteLine("[Mirage] tick failed: " + e.Message); }
                Thread.Sleep(sleep);
            }
        }

        /// <summary>True while the game is paused in a way that should freeze the decoy — either the "PAUSE"
        /// screen (CheckDunIsPaused) or the in-dungeon item menu (mode 3 / dungeonMode 2, same test the other
        /// effects use for menuOpen). The two pause types freeze different things natively (the menu freezes the
        /// clone but not our timer; the PAUSE screen freezes our timer but not the clone), so we unify them.</summary>
        private static bool IsPaused()
        {
            if (Player.CheckDunIsPaused()) return true;
            return Memory.ReadByte(Addresses.mode) == 3 && Memory.ReadByte(Addresses.dungeonMode) == 2;
        }

        // ── decoy state (all DATA) ───────────────────────────────────────────────────────────
        private static void PlaceDecoy()
        {
            _dx = Memory.ReadFloat(Addresses.dunPositionX);
            _dz = Memory.ReadFloat(Addresses.dunPositionZ);
            _dy = Memory.ReadFloat(Addresses.dunPositionY);
            // Capture the player's facing (CCharacter +0x60/+0x68 = forward vector, X/Y plane) at cast so the
            // heat-haze can be pushed BACK along it (the raster is built to rise over a fire ahead → too far forward).
            float fwx = Memory.ReadFloat(MirageClone.PlayerChar + 0x60), fwy = Memory.ReadFloat(MirageClone.PlayerChar + 0x68);
            double fmag = System.Math.Sqrt((double)fwx * fwx + (double)fwy * fwy);
            _decoyFwdX = fmag > 0.0001 ? (float)(fwx / fmag) : 0f;
            _decoyFwdY = fmag > 0.0001 ? (float)(fwy / fmag) : 0f;
            WriteDecoyPos();   // the stationary decoy position fooled slots' pointers reference
            Array.Clear(_fooled, 0, _fooled.Length);
            Array.Clear(_brokenThisDecoy, 0, _brokenThisDecoy.Length);
            _prevHp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MirageDecoy.MaxSlots; s++)
                if (IsLiveEnemy(s)) _fooled[s] = true;
            _decoyActive = true;
            _decoyDeadline = DateTime.UtcNow.AddSeconds(DecoySeconds);
            DespawnClone();   // clear any stale slot from a previous decoy
            SpawnClone();
            if (_cloneSlot >= 0) Memory.WriteInt(SceneGateFlag, 1);   // arm the PNACH scene-gate NOP (clone draws)
            Console.WriteLine($"[Mirage] decoy planted at ({_dx:0.#},{_dy:0.#}); enemies redirected");
        }

        private static void UpdateDecoyState()
        {
            if (DateTime.UtcNow > _decoyDeadline)
            {
                _decoyActive = false; Array.Clear(_fooled, 0, _fooled.Length);
                DespawnClone();
                Console.WriteLine("[Mirage] decoy faded; enemies re-target the player");
                return;
            }
            MaintainClone();
            int[] hp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MirageDecoy.MaxSlots; s++)
            {
                if (!IsLiveEnemy(s)) { _fooled[s] = false; continue; }
                if (_prevHp != null && s < _prevHp.Length && hp[s] < _prevHp[s])
                { _fooled[s] = false; _brokenThisDecoy[s] = true; continue; }
                if (!_fooled[s] && !_brokenThisDecoy[s]) _fooled[s] = true;
            }
            _prevHp = hp;
        }

        /// <summary>One batched write of the managed slots' POINTERS: fooled → DecoyPos, else → the live player
        /// global. Un-fooled entries are the live-player address itself, so those enemies read the engine-live
        /// player (vanilla) — the mod only flips a pointer when a slot's fooled state changes.</summary>
        private static void WriteTable()
        {
            var buf = new byte[MirageDecoy.MaxSlots * MirageDecoy.PtrStride];
            for (int s = 0; s < MirageDecoy.MaxSlots; s++)
                BitConverter.GetBytes(_fooled[s] ? MirageDecoy.DecoyPosGuest : MirageDecoy.PlayerPosGuest)
                    .CopyTo(buf, s * MirageDecoy.PtrStride);
            Memory.WriteBytesBatch(MirageDecoy.PtrTable, buf);
        }

        /// <summary>Point every slot at the live player global (vanilla) — done at cold-arm before any enemy reads,
        /// so out-of-range slots and the pre-first-tick window are valid without the mod having run.</summary>
        private static void FillWholeTablePlayer()
        {
            var buf = new byte[MirageDecoy.TableSlots * MirageDecoy.PtrStride];
            for (int s = 0; s < MirageDecoy.TableSlots; s++)
                BitConverter.GetBytes(MirageDecoy.PlayerPosGuest).CopyTo(buf, s * MirageDecoy.PtrStride);
            Memory.WriteBytesBatch(MirageDecoy.PtrTable, buf);
        }

        /// <summary>Write the stationary decoy position (x,z,y,w) that fooled slots' pointers reference.</summary>
        private static void WriteDecoyPos()
        {
            var b = new byte[16];
            BitConverter.GetBytes(_dx).CopyTo(b, 0);
            BitConverter.GetBytes(_dz).CopyTo(b, 4);
            BitConverter.GetBytes(_dy).CopyTo(b, 8);
            BitConverter.GetBytes(1.0f).CopyTo(b, 12);
            Memory.WriteBytesBatch(MirageDecoy.DecoyPos, b);
        }

        private static bool IsLiveEnemy(int s)
        {
            int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
            if (id == 0 || id == 0xFFFF) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }

        // ── clone visual: a copy of the player CCharacter placed in a free NPC draw slot (the game draws
        //    it for us; see MirageClone). No injected code — pure data. ──────────────────────────────────
        private const bool  CloneEnabled = true;         // MotionParts host: single character-draw slot with its own texture group
        private const bool  GhostSilhouette = false;     // translucent character clone (false = normal appearance, rely on the heat-haze)
        private const bool  EngineDrivenAnim = true;     // let the engine's chara-step pose the clone tree natively (no polling)
        private const bool  IndependentMesh = true;      // copy the software-skinned body meshes → fully independent clone
        private const bool  WeaponEnabled   = true;      // graft the equipped weapon onto the clone's hand bone
        private const bool  ClothEnabled    = true;      // copy the player's cloth onto the clone
        private const bool  ClothPhysics    = true;      // anchor cloth to the clone skeleton for real sim (needs step-injection)
        private const bool  HeatHaze        = false;     // DISABLED: fire heat-haze via a cave-stub jal at 0x1DAEBCC
                                                         // crashes on the clone-draw frame — the scene-gate toggle
                                                         // re-compiles Draw__11CSeireiKing and the direct jal→cave
                                                         // chokes the recompiler (only dispatch-jalr into a cave is
                                                         // safe, and DrawRaster has no fn-ptr table). Kept for a
                                                         // possible future revisit (force-load textures + a dispatch
                                                         // reach). While false: no cave write, PNACH jal reverted.
        private const float GhostOpacity = 90f;          // of 128; lower = more see-through
        private const float GhostDim     = 1.0f;         // full lighting so any resident character texture shows
        // Ambient ADD tint (+0xCE0 RGB) folded into the clone's lighting each draw; scene ambient ~128 = neutral,
        // so positive brightens and B>R,G gives the blue tint. Tune to taste.
        private const float GhostTintR = 20f;
        private const float GhostTintG = 30f;
        private const float GhostTintB = 65f;
        private const bool  FireProbe = false;  // DEBUG: dump the dungeon fire-tile data (idea 1: move a torch haze to the clone)
        private const bool  ForceRaster = false; // (test done: forcing on a torch broke its flame, but NO crash → textures loaded)
        private static DateTime _fireProbeNext;
        private static int _cloneSlot = -1;   // NPC type slot we borrowed, or -1
        private static int      _probeCid = -1;      // (cid,root) currently being observed for stability
        private static uint     _probeRoot;
        private static DateTime _probeSince;         // when the current (cid,root) pair was first seen
        private static string   _probeLastSig = "";  // last logged footprint signature (dedupe)
        private static uint _srcModelRoot;    // the model root the clone copies/renders (player or test enemy)
        private static uint _cloneRootGuest;  // guest addr of the deep-copied clone root (node pool slot 0)
        private static int  _cloneNodeCount;  // nodes in the last deep copy
        private static uint _weaponRootGuest; // copied weapon tree root (guest); 0 = no weapon grafted
        private static uint _wpnObjPtr;       // the live weapon object (iGpffff9d00) — source of the grip transform
        private static int  _meshesCopied;    // # software-skinned meshes given independent copies this spawn
        private static readonly System.Collections.Generic.List<(long player, long clone)> _chanMap = new();  // motion-channel frame-sync map

        /// <summary>The source model root for the clone: the player's, or (TEST) a live enemy's — to check
        /// whether the NPC slot can render ANY geometry at the decoy.</summary>
        private static uint CloneSourceModelRoot()
        {
            return (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.CharModel);
        }

        private static void SpawnClone()
        {
            if (!CloneEnabled) return;

            // Independent deep copy of the player's whole frame tree.
            _weaponRootGuest = 0; _wpnObjPtr = 0;
            if (!BuildCloneTree()) { _cloneSlot = -1; return; }
            if (IndependentMesh) CopyMeshNodes();   // give the clone its OWN software-skinned body meshes
            if (WeaponEnabled) GraftWeapon();       // graft the equipped weapon onto the clone's hand bone

            // Host = chara slot 0, drawn by the DUNGEON scene draw (Draw__11CSeireiKing) as a single
            // Draw__12CNPCharacter with its own texture group (0x20). Fill the player's draw fields, aim the
            // model at the clone tree, freeze, set position/opacity, mark active, and register it for draw.
            _cloneSlot = 0;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;

            byte[] buf = Memory.ReadBytesBatch(MirageClone.PlayerChar, MirageClone.CharCopySize);
            if (buf == null) { _cloneSlot = -1; return; }
            // Cloth: snapshot the player's cloth pieces into clone-owned copies and hang them off the CLONE's own
            // +0xC74. The clone's Draw renders them (ghost tint — NO player flash bleed). In PHYSICS mode the
            // anchor/collision are re-based to the clone skeleton (in CopyCloth) and the clone is ClothStep'd by
            // the PNACH chara-loop patch (jal ShadowStep→ClothStep while the decoy flag == 1), so they simulate.
            uint clothListGuest = ClothEnabled ? CopyCloth() : (uint)MirageClone.ClothStubGuest;
            BitConverter.GetBytes(clothListGuest).CopyTo(buf, MirageClone.ClothList);
            BitConverter.GetBytes(GhostSilhouette ? GhostDim : 1.0f).CopyTo(buf, MirageClone.DimFactor);
            // PIN the clone to the guard-loop motion instead of whatever the player was mid-doing at spawn. The
            // engine's chara-step (unlocked via the PNACH step-gate NOP) reads +0xC68 every frame → SetMotionEX,
            // so a stale/transition capture would stick. Force motion 9 and set the clean-restart flag (+0xC64
            // bit2) so the first step starts it at frame 0 with no blend. MaintainClone re-asserts +0xC68 = 9.
            // NOTE: leave +0xC60 (motion speed) at the copied player value — native speed. A near-zero override
            // made MotionProc loop-to-freeze covering a motion span by 0.001-steps.
            BitConverter.GetBytes(GuardLoopMotion).CopyTo(buf, MirageClone.MotionId);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, MirageClone.MotionFlags) | (uint)MirageClone.MotionRestart)
                .CopyTo(buf, MirageClone.MotionFlags);
            BitConverter.GetBytes(GhostSilhouette ? GhostOpacity : 128f).CopyTo(buf, MirageClone.NpcOpacity);  // +0xcec (Draw needs >0)
            BitConverter.GetBytes(GhostSilhouette ? GhostTintR : 0f).CopyTo(buf, MirageClone.CharaTint);      // +0xce0 ambient ADD (brighter + blue)
            BitConverter.GetBytes(GhostSilhouette ? GhostTintG : 0f).CopyTo(buf, MirageClone.CharaTint + 4);
            BitConverter.GetBytes(GhostSilhouette ? GhostTintB : 0f).CopyTo(buf, MirageClone.CharaTint + 8);
            for (int o = MirageClone.LightFrom; o < MirageClone.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            // Keep the copied tex-anime object (+0xdc) intact — it selects the face/skin textures; zeroing it
            // left the face dark and arms mis-textured. (Was zeroed for the old shared-texture NPC host.)
            BitConverter.GetBytes(_dx).CopyTo(buf, MirageClone.CharPos);       // +0x10 X
            BitConverter.GetBytes(_dz).CopyTo(buf, MirageClone.CharPos + 4);   // +0x14 Z/height
            BitConverter.GetBytes(_dy).CopyTo(buf, MirageClone.CharPos + 8);   // +0x18 Y
            BitConverter.GetBytes(_cloneRootGuest).CopyTo(buf, MirageClone.CharModel);

            // Give the clone INDEPENDENT motion channels so the engine-step advances its OWN frame counter
            // (not the player's shared one → the 2× fix). Copy each active channel struct into the cave and
            // repoint buf's +0xC20[i]. Internal ptrs (shared keyframe data) copy as-is.
            // When the clone skins its own meshes, it also needs its OWN FRAME_INF bone-matrix buffer (copy
            // once; shared across channels). Otherwise the clone's MotionProc2 clobbers the player's matrices.
            bool ownSkin = IndependentMesh && _meshesCopied > 0;
            uint fiOld = 0, fiNew = 0, bmOld = 0, bmNew = 0;
            int  fiSize = (_cloneNodeCount + 1) * MirageClone.FrameInfEntry;
            int  bmSize = (_cloneNodeCount + 1) * MirageClone.BoneMtxEntry;
            _chanMap.Clear();
            if (EngineDrivenAnim)
            {
                for (int s = 0; s < MirageClone.MotionSlots; s++)
                {
                    int po = MirageClone.MotionSlotBase + s * 4;
                    uint sp = (uint)BitConverter.ToInt32(buf, po) & 0x1FFFFFFF;
                    if (sp == 0 || sp >= 0x02000000) continue;
                    byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(sp), MirageClone.MotionStructSize);
                    if (mstr == null) continue;

                    // Give the clone its own FRAME_INF (skinning-matrix buffer), copied once and reused.
                    if (ownSkin)
                    {
                        uint fi = (uint)BitConverter.ToInt32(mstr, MirageClone.FrameInfPtr) & 0x1FFFFFFF;
                        if (fi != 0 && fi < 0x02000000)
                        {
                            if (fiNew == 0 || fi == fiOld)
                            {
                                if (fiNew == 0)
                                {
                                    byte[] fib = Memory.ReadBytesBatch(Memory.ToMmu(fi), fiSize);
                                    if (fib != null) { Memory.WriteBytesBatch(MirageClone.FrameInfCave, fib); fiOld = fi; fiNew = (uint)MirageClone.FrameInfCaveGuest; }
                                }
                                if (fiNew != 0) BitConverter.GetBytes(fiNew).CopyTo(mstr, MirageClone.FrameInfPtr);
                            }
                        }

                        // Give the clone its own per-bone ANIMATION-matrix buffer (channel+0x00), copied once.
                        uint bm = (uint)BitConverter.ToInt32(mstr, MirageClone.BoneMtxPtr) & 0x1FFFFFFF;
                        if (bm != 0 && bm < 0x02000000)
                        {
                            if (bmNew == 0 || bm == bmOld)
                            {
                                if (bmNew == 0)
                                {
                                    byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                                    if (bmb != null) { Memory.WriteBytesBatch(MirageClone.BoneMtxCave, bmb); bmOld = bm; bmNew = (uint)(MirageClone.BoneMtxCave & 0x1FFFFFFF | 0x01000000); }
                                }
                                if (bmNew != 0) BitConverter.GetBytes(bmNew).CopyTo(mstr, MirageClone.BoneMtxPtr);
                            }
                        }
                    }
                    // The SKINNING list (+0x08) drives MotionProc2, which software-skins the mesh vertex buffer.
                    // If the clone has its OWN mesh copies (IndependentMesh), let it skin them → fully independent
                    // body. Otherwise zero the list so it never touches the SHARED mesh (body mirrors the player).
                    // Only let the clone software-skin if it got its OWN mesh copies; else zero the skin list so
                    // it never touches the SHARED mesh (safe fallback = body mirrors, player uncorrupted).
                    if (!IndependentMesh || _meshesCopied == 0) BitConverter.GetBytes(0).CopyTo(mstr, MirageClone.MotionSkinList);
                    long cloneChan = MirageClone.MotionCave + (long)s * MirageClone.MotionStructSize;
                    Memory.WriteBytesBatch(cloneChan, mstr);
                    BitConverter.GetBytes((uint)(MirageClone.MotionCaveGuest + s * MirageClone.MotionStructSize)).CopyTo(buf, po);
                    _chanMap.Add((Memory.ToMmu(sp), cloneChan));   // player channel → clone channel (for frame sync)
                }
            }

            Memory.WriteBytesBatch(MirageClone.ClothStub, new byte[16]);
            Memory.WriteBytesBatch(slot, buf);   // fill the CCharacter fields (0..0xD60)
            Memory.WriteInt(slot + MirageClone.CharaActive, 1);    // active (Draw__12CNPCharacter gate; beyond 0xD60)
            // Engine-driven animation: enable the chara motion step (Step__12CNPCharacter → Step__10CCharacter →
            // SetMotionEX) and zero the opacity ramp so it doesn't fight our GhostOpacity.
            Memory.WriteInt(slot + MirageClone.CharaMotionA, EngineDrivenAnim ? 1 : 0);
            Memory.WriteInt(slot + MirageClone.CharaMotionB, 0);
            Memory.WriteInt(slot + MirageClone.CharaRampA, 0);     // opacity-ramp amount = 0 (no fade drift)
            Memory.WriteInt(slot + MirageClone.CharaRampB, 0);
            Memory.WriteInt(MirageClone.CharaRegistry + (long)_cloneSlot * 4, 1);   // register → dungeon draw draws it
            Memory.WriteInt(MirageClone.StepSkipTable + (long)_cloneSlot * 4, 0);   // un-skip → dungeon step loop steps it (clear the 1 a prior DespawnClone left)
            Console.WriteLine($"[Mirage] clone in chara slot {_cloneSlot} (texgroup 0x{MirageClone.CharaTexBase + _cloneSlot:X}) @({_dx:0.#},{_dz:0.#},{_dy:0.#}) root=0x{_cloneRootGuest:X}");

            if (WeaponEnabled) RegisterWeaponChara();   // clone weapon in its own slot 3 / texgroup 0x1d pass
        }

        /// <summary>Deep-copy the source model's CFrame tree into the cave pool as a CONTIGUOUS 0x270-stride
        /// array preserving the player's memory order. This is REQUIRED for engine posing: MotionProc
        /// (0x147d20) addresses bones as <c>root + boneIndex*0x270</c> — raw array indexing, NOT pointer walks.
        /// So the copy must keep the same stride (0x270) and order; only the link pointers (parent/child/
        /// sibling) are re-based by a single offset (clonePool − playerBase). Geometry (+0x260) stays SHARED.
        /// The result both renders (draw follows the re-based child/sibling) and poses (SetMotionEX indexes the
        /// contiguous array) correctly. Returns false if the tree isn't resolvable or overflows the pool.</summary>
        /// <summary>Graft the equipped weapon onto the clone's hand. The weapon (WeaponObjGlobal → obj, +0xBC =
        /// model root) is a small CFrame tree parented to the player's hand bone but drawn SEPARATELY (not part of
        /// the player's +0xBC tree). We deep-copy that tree into WeaponCave (rebasing its internal links exactly
        /// like BuildCloneTree) and splice the copy's root into the CLONE's hand-bone child list, so the clone's
        /// own MGDraw walks into it and draws the weapon at the clone's hand — no separate draw call needed.
        /// Mesh visuals (node+0x260) are left SHARED: if the weapon is VU1 (rigid) they're per-instance already;
        /// if the weapon flickers it's software-skinned (MDT) and needs the CopyMeshNodes double-buffer copy.</summary>
        private static void GraftWeapon()
        {
            uint wpnObj = (uint)Memory.ReadInt(MirageClone.WeaponObjGlobal) & 0x1FFFFFFF;
            if (wpnObj == 0 || wpnObj >= 0x02000000) { Console.WriteLine("[Mirage/wpn] no weapon object"); return; }
            uint wRoot = (uint)Memory.ReadInt(Memory.ToMmu(wpnObj) + 0xBC) & 0x1FFFFFFF;   // weapon model root
            if (wRoot == 0 || wRoot >= 0x02000000) { Console.WriteLine("[Mirage/wpn] no weapon model"); return; }

            // DFS child/sibling for the tree's contiguous 0x270-stride address span (same layout as the body).
            uint min = wRoot, max = wRoot;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(wRoot);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (n == 0 || n >= 0x02000000 || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > 64) { Console.WriteLine("[Mirage/wpn] weapon tree > 64 nodes — bailing"); return; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }
            if ((max - min) % MirageClone.NodeStride != 0)
            { Console.WriteLine($"[Mirage/wpn] span 0x{max - min:X} not 0x270-aligned — bailing"); return; }
            int nodeCount = (int)((max - min) / MirageClone.NodeStride) + 1;
            int blockSize = nodeCount * MirageClone.NodeStride;
            if (blockSize > MirageClone.WeaponCaveSize)
            { Console.WriteLine($"[Mirage/wpn] weapon tree 0x{blockSize:X} > cave 0x{MirageClone.WeaponCaveSize:X}"); return; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return;

            uint rootOff = wRoot - min;
            uint wCaveG  = (uint)MirageClone.WeaponCaveGuest;
            for (int o = 0; o < blockSize; o += MirageClone.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                WpnRebase(block, o + MirageClone.Parent,      min, max, wCaveG, isRoot);   // root parent → set below
                WpnRebase(block, o + MirageClone.RootChild,   min, max, wCaveG, false);
                WpnRebase(block, o + MirageClone.RootSibling, min, max, wCaveG, isRoot);   // root sibling → set below
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheB);
            }
            Memory.WriteBytesBatch(MirageClone.WeaponCave, block);

            // Parent the copied root to the clone's hand bone (for positioning ONLY — MGDraw seeds a drawn
            // root's world matrix from its parent's cached world, exactly like the player's weapon object). We do
            // NOT insert it into the hand's child list: the weapon draws in its OWN chara slot / texgroup-0x1d
            // pass (RegisterWeaponChara), so the body's 0x11 pass never draws it with the wrong texture.
            uint wRootG = (uint)(wCaveG + rootOff);
            uint handG  = (uint)(MirageClone.NodePoolGuest + MirageClone.WeaponHandBone * MirageClone.NodeStride);
            Memory.WriteUInt(Memory.ToMmu(wRootG) + MirageClone.Parent,      handG);
            Memory.WriteUInt(Memory.ToMmu(wRootG) + MirageClone.RootSibling, 0);   // standalone root
            _weaponRootGuest = wRootG;
            _wpnObjPtr = wpnObj;
            Console.WriteLine($"[Mirage/wpn] weapon tree copied ({nodeCount} nodes) → root 0x{wRootG:X}, parent=clone hand {MirageClone.WeaponHandBone}");
        }

        /// <summary>Register the copied weapon tree as its OWN chara slot (WeaponCharaSlot). Cloned from the body
        /// slot for a valid CCharacter (vtable/light/cloth-stub), then re-aimed: model → weapon tree (parent =
        /// clone hand), transform ← the real weapon object (pos 0,0,0 + grip rot/scale), motion channels zeroed
        /// (rigid). Drawn (registry) but NOT stepped (step-skip table + CharaMotionA=0). The patched group
        /// formula gives this slot texgroup 0x1d, so the weapon samples its own atlas — correct texture.</summary>
        private static void RegisterWeaponChara()
        {
            if (_weaponRootGuest == 0 || _wpnObjPtr == 0 || _cloneSlot < 0) return;
            long src = MirageClone.CharaArray + (long)_cloneSlot        * MirageClone.CharaStride;   // body slot (valid)
            long dst = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
            byte[] cslot = Memory.ReadBytesBatch(src, MirageClone.CharaStride);
            if (cslot == null) return;
            byte[] wtf = Memory.ReadBytesBatch(Memory.ToMmu(_wpnObjPtr) + MirageClone.CharPos, 0x90);   // +0x10..+0xA0 TRS
            if (wtf != null) wtf.CopyTo(cslot, MirageClone.CharPos);
            BitConverter.GetBytes(_weaponRootGuest).CopyTo(cslot, MirageClone.CharModel);               // +0xbc = weapon tree
            for (int s = 0; s < MirageClone.MotionSlots; s++)                                            // rigid: no motion
                BitConverter.GetBytes(0).CopyTo(cslot, MirageClone.MotionSlotBase + s * 4);
            Memory.WriteBytesBatch(dst, cslot);
            Memory.WriteInt  (dst + MirageClone.CharaActive, 1);                                         // +0x146c draw gate
            Memory.WriteFloat(dst + MirageClone.NpcOpacity, GhostSilhouette ? GhostOpacity : 128f);      // +0xcec > 0
            Memory.WriteInt  (dst + MirageClone.CharaMotionA, 0);                                        // never step
            Memory.WriteInt  (dst + MirageClone.CharaMotionB, 0);
            Memory.WriteInt  (MirageClone.StepSkipTable  + (long)MirageClone.WeaponCharaSlot * 4, 1);    // step loop skips it
            Memory.WriteInt  (MirageClone.CharaRegistry  + (long)MirageClone.WeaponCharaSlot * 4, 1);    // draw loop draws it
            Console.WriteLine($"[Mirage/wpn] weapon chara slot {MirageClone.WeaponCharaSlot} (texgroup 0x1d) model=0x{_weaponRootGuest:X}");
        }

        /// <summary>Re-base a weapon-tree link into WeaponCave (mirror of Rebase but for the weapon cave); zero
        /// external refs and, when forceZero, the root's parent/sibling (set explicitly by the graft splice).</summary>
        private static void WpnRebase(byte[] block, int off, uint min, uint max, uint caveGuest, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & 0x1FFFFFFF;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max) neu = (uint)(caveGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        private static bool BuildCloneTree()
        {
            _srcModelRoot = CloneSourceModelRoot();
            uint rootPhys = _srcModelRoot & 0x1FFFFFFF;
            if (_srcModelRoot == 0 || rootPhys == 0 || rootPhys >= 0x02000000) return false;

            // DFS over child/sibling to find the tree's address SPAN (the bones form a contiguous 0x270 array
            // rooted at the model root, so min == rootPhys and the block is [min, max+0x270)).
            uint min = rootPhys, max = rootPhys;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(rootPhys);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (n == 0 || n >= 0x02000000 || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > MirageClone.MaxNodes)
                { Console.WriteLine($"[Mirage] clone tree > {MirageClone.MaxNodes} nodes — aborting"); return false; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }

            int nodeCount = (int)((max - min) / MirageClone.NodeStride) + 1;
            int blockSize = nodeCount * MirageClone.NodeStride;
            if ((max - min) % MirageClone.NodeStride != 0)
            { Console.WriteLine($"[Mirage] tree span 0x{max - min:X} not 0x270-aligned — bailing"); return false; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            // Re-base every link pointer by (clonePool − playerBase); zero external refs and the root's
            // parent/sibling; invalidate world caches. Same-offset translation keeps array indexing valid.
            uint rootOff = rootPhys - min;
            for (int o = 0; o < blockSize; o += MirageClone.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                Rebase(block, o + MirageClone.Parent,      min, max, isRoot);   // root's parent → 0
                Rebase(block, o + MirageClone.RootChild,   min, max, false);
                Rebase(block, o + MirageClone.RootSibling, min, max, isRoot);   // root's sibling → 0
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheB);
            }
            Memory.WriteBytesBatch(MirageClone.NodePool, block);

            _cloneNodeCount = nodeCount;
            _cloneRootGuest = (uint)(MirageClone.NodePoolGuest + rootOff);   // clone root = pool + root offset
            Console.WriteLine($"[Mirage] contiguous {nodeCount}-node clone tree (0x270 stride) → root 0x{_cloneRootGuest:X}");
            return true;
        }

        /// <summary>Re-base a node link pointer: if it targets a node inside the copied block [min, max+0x270),
        /// rewrite it to the clone pool at the same offset; otherwise (external ref, or forceZero for the
        /// root's parent/sibling) zero it.</summary>
        private static void Rebase(byte[] block, int off, uint min, uint max, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & 0x1FFFFFFF;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max)
                neu = (uint)(MirageClone.NodePoolGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        /// <summary>Give the clone its own copy of every SOFTWARE-SKINNED mesh (CVisualMDTVu1). Those are the
        /// only bones whose vertex buffer MotionProc2 deforms in place; copying them (visual + VU data + MDT)
        /// and repointing node+0x260 lets the clone skin its OWN buffers → a body independent of the player.
        /// The other bones skin on VU1 at draw time and are already per-instance. Cross-references between the
        /// three copied blocks are rebased by scanning for words in the old address ranges (safe: model data
        /// pointers are low 0x00xxxxxx addresses, distinct from vertex floats).</summary>
        private static void CopyMeshNodes()
        {
            long cave = MirageClone.MeshCave, caveGuest = MirageClone.MeshCaveGuest;
            long caveEnd = MirageClone.MeshCave + MirageClone.MeshCaveSize;
            int copied = 0;
            for (int i = 0; i < _cloneNodeCount; i++)
            {
                long node = MirageClone.NodePool + (long)i * MirageClone.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + MirageClone.GeomPtr) & 0x1FFFFFFF;
                if (vis == 0 || vis >= 0x02000000) continue;
                uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisMDT) & 0x1FFFFFFF;
                uint magic = (mdt != 0 && mdt < 0x02000000) ? (uint)Memory.ReadInt(Memory.ToMmu(mdt)) : 0xDEAD;
                if (mdt == 0 || mdt >= 0x02000000) continue;
                if (magic != MirageClone.MdtMagic) continue;   // not software-skinned

                uint vu    = (uint)Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisVU) & 0x1FFFFFFF;
                int  vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisVU + 4) * 16;   // field is in QWORDS
                int  mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + MirageClone.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;

                int visSz = MirageClone.VisualSize;
                int need  = Align16(visSz) + Align16(vuSz) + Align16(mdtSz);
                if (cave + need > caveEnd) { Console.WriteLine($"[Mirage/mesh] cave full at n{i}"); break; }

                long cVis = cave;              uint cVisG = (uint)caveGuest;
                long cVU  = cave + Align16(visSz); uint cVUG = (uint)(caveGuest + Align16(visSz));
                long cMDT = cVU + Align16(vuSz);   uint cMDTG = (uint)(caveGuest + Align16(visSz) + Align16(vuSz));

                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu),  vuSz);
                byte[] mdtB = Memory.ReadBytesBatch(Memory.ToMmu(mdt), mdtSz);
                if (visB == null || vuB == null || mdtB == null) continue;

                // The visual ALWAYS repoints to the clone copies (+0x18 → clone VU, +0x20 → clone MDT).
                RebaseRange(visB, vu, vuSz, cVUG);
                RebaseRange(visB, mdt, mdtSz, cMDTG);
                foreach (byte[] b in new[] { vuB, mdtB })
                {
                    RebaseRange(b, vu,  vuSz,  cVUG);
                    RebaseRange(b, mdt, mdtSz, cMDTG);
                }

                // Force BOTH double-buffer slots (+0x28/+0x2c) and the current ptr (+0x18) to the clone's single
                // VU copy. n66 is truly double-buffered (two distinct buffers, DBuffID-toggled); copying only one
                // left +0x2c aimed at the PLAYER's buffer, so on DBuffID=1 frames the clone wrote the player's
                // buffer → flicker. Single-buffering the clone (like n64/n65) can't corrupt the player.
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);

                Memory.WriteBytesBatch(cVU,  vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + MirageClone.GeomPtr, cVisG);   // node draws the clone's own mesh
                cave += need; caveGuest += need; copied++;
                Console.WriteLine($"[Mirage/mesh] n{i}: vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} = 0x{need:X}");
            }
            _meshesCopied = copied;
            // Instrument actual cave usage (character-dependent — needed to right-size MeshCaveSize for BOTH
            // Ungaga and Xiao/Super Steve once we have real numbers; the reservation stays generous until then).
            long used = cave - MirageClone.MeshCave;
            Console.WriteLine($"[Mirage/mesh] {copied} mesh(es), used 0x{used:X} of 0x{MirageClone.MeshCaveSize:X} MeshCave");
        }

        private static int Align16(int n) => (n + 15) & ~15;

        /// <summary>Rewrite every 4-byte word in <paramref name="b"/> whose value points into [oldBase,
        /// oldBase+size) to the corresponding offset in the clone copy at newBaseGuest.</summary>
        private static void RebaseRange(byte[] b, uint oldBase, int size, uint newBaseGuest)
        {
            for (int o = 0; o + 4 <= b.Length; o += 4)
            {
                uint w = (uint)BitConverter.ToInt32(b, o);
                uint phys = w & 0x1FFFFFFF;
                if (phys >= oldBase && phys < oldBase + (uint)size)
                    BitConverter.GetBytes(newBaseGuest + (phys - oldBase)).CopyTo(b, o);
            }
        }

        /// <summary>Read-only footprint probe for the CURRENTLY-ACTIVE party member: DFS the model tree (child
        /// +0x138 / sibling +0x13C) for node count + span, sum the software-skinned (CVisualMDTVu1) mesh bytes
        /// the clone would have to copy (→ MeshCave sizing), and read the cloth list (piece count + grids →
        /// cloth-cave sizing). Latches per character ID only on a STABLE read (a settled model has many bones),
        /// so the transitional garbage that made the old one-shot DumpCloth unreliable can't lock in bad data.
        /// Cycle party members in a dungeon to capture all six — needed to make Mirage usable for any character.</summary>
        /// <summary>DEBUG: map the dungeon's fire-tile data for the "move a torch's heat-haze to the clone" idea.
        /// DrawRaster__11CDungeonMap reads a 20×20 per-tile fire array at dngMap+0x9C50 (0x10/entry: +0=fireIndex
        /// (-1=none), +4=rotation, +8=distance(float, <=240 to draw), +C=enabled(==1)); an enabled tile draws the
        /// haze of the fire structure at dngMap+fireIndex*0x1D0. Logs live fire tiles + the decoy's tile so we can
        /// copy a torch's record onto the clone's tile. Stand near a torch.</summary>
        private static void DumpFireData()
        {
            uint dngMap = (uint)Memory.ReadInt(0x202A34B8) & 0x1FFFFFFF;   // NowDngMap ptr
            if (dngMap == 0 || dngMap >= 0x02000000) { Console.WriteLine("[Mirage/fire] no dngMap"); return; }
            long b = Memory.ToMmu(dngMap);
            int found = 0;
            for (int t = 0; t < 400 && found < 8; t++)
            {
                long e = b + 0x9C50 + (long)t * 0x10;
                int idx = Memory.ReadInt(e + 0x00);
                int en  = Memory.ReadInt(e + 0x0C);
                if (en != 1 || idx == -1 || idx < 0 || idx > 200) continue;
                float dist = Memory.ReadFloat(e + 0x08);
                int rot = Memory.ReadInt(e + 0x04);
                int sub = Memory.ReadUShort(b + (long)idx * 0x1D0 + 0x4A2);
                Console.WriteLine($"[Mirage/fire] FIRE tile({t % 20},{t / 20}) fireIdx={idx} rot={rot} dist={dist:0.#} | struct@0x{dngMap + (uint)idx * 0x1D0:X} sub={sub} | rec:{BitConverter.ToString(Memory.ReadBytesBatch(e, 0x10) ?? new byte[0]).Replace("-", " ")}");
                if (found == 0)   // dump the fire struct's emitter region to find the real sub-emitter count/data
                {
                    long fs = b + (long)idx * 0x1D0;
                    byte[] d = Memory.ReadBytesBatch(fs + 0x480, 0x60);
                    if (d != null) Console.WriteLine($"[Mirage/fire]   struct+0x480: {BitConverter.ToString(d).Replace("-", " ")}");
                    if (ForceRaster)   // TEST: force one raster (heat-haze) emitter on THIS fire → does the torch shimmer, or crash?
                    {
                        Memory.WriteBytesBatch(fs + 0x4A2, new byte[] { 1, 0 });   // count = 1 (emitter[0].pos @+0x4B0 already 0 → draws at tile)
                        Console.WriteLine($"[Mirage/fire]   FORCED raster count=1 on fireIdx={idx} @0x{dngMap + (uint)idx * 0x1D0:X} — watch that torch for heat-haze (or crash = 'alpha01' not loaded)");
                    }
                }
                found++;
            }
            if (_decoyActive)
            {
                int dc = (int)((_dx + 80) / 160), dr = (int)((_dy + 80) / 160);
                Console.WriteLine($"[Mirage/fire] decoy tile ({dc},{dr}) idx={dc + dr * 20} pos=({_dx:0.#},{_dy:0.#})");
            }
            if (found == 0) Console.WriteLine($"[Mirage/fire] no fire tiles (dngMap@0x{dngMap:X}) — stand near a torch/flame");
        }

        private static void ProbeCharacter()
        {
            int cid = Player.CurrentCharacterNum();
            uint root = (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.CharModel) & 0x1FFFFFFF;
            if (cid < 0 || cid >= 6 || root == 0 || root >= 0x02000000) { _probeCid = -1; return; }
            // Character switching is LAGGED: CurrentCharacterNum() flips to the new id several frames before the
            // model at +0xBC actually swaps, so a first-valid read latches the PREVIOUS character's model under the
            // new id (that's why Toan & Xiao read identically). Require the (id,root) pair to hold STABLE for ≥1s
            // before reading, and log by CHANGED signature (no permanent latch) so a stale read self-corrects when
            // you settle on the character again.
            if (cid != _probeCid || root != _probeRoot) { _probeCid = cid; _probeRoot = root; _probeSince = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _probeSince).TotalMilliseconds < 1000) return;   // not settled yet

            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(root);
            int meshes = 0; long meshBytes = 0; uint nodeMin = root, nodeMax = root;
            bool overflow = false;
            while (work.Count > 0)
            {
                uint nn = work.Pop();
                if (nn == 0 || nn >= 0x02000000 || !seen.Add(nn)) continue;
                if (seen.Count > 512) { overflow = true; break; }
                if (nn < nodeMin) nodeMin = nn; if (nn > nodeMax) nodeMax = nn;
                uint vis = (uint)Memory.ReadInt(Memory.ToMmu(nn) + MirageClone.GeomPtr) & 0x1FFFFFFF;
                if (vis != 0 && vis < 0x02000000)
                {
                    uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisMDT) & 0x1FFFFFFF;
                    if (mdt != 0 && mdt < 0x02000000 && (uint)Memory.ReadInt(Memory.ToMmu(mdt)) == MirageClone.MdtMagic)
                    {
                        int vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisVU + 4) * 16;
                        int mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + MirageClone.MdtSizeField);
                        if (vuSz > 0 && vuSz <= 0x40000 && mdtSz > 0 && mdtSz <= 0x40000)
                        { meshes++; meshBytes += Align16(MirageClone.VisualSize) + Align16(vuSz) + Align16(mdtSz); }
                    }
                }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(nn) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }
            int nodes = seen.Count;
            if (overflow || nodes < 20) return;   // transitional / not settled — retry next tick, don't latch
            int span = (int)((nodeMax - nodeMin) / 0x270) + 1;

            // cloth pieces + grids + buffer bytes (character-dependent; may still be loading — best-effort)
            uint clist = (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.ClothList) & 0x1FFFFFFF;
            int cpieces = 0; long cbufBytes = 0; var grids = new System.Text.StringBuilder();
            if (clist != 0 && clist < 0x02000000)
                for (int i = 0; i < 4; i++)
                {
                    uint co = (uint)Memory.ReadInt(Memory.ToMmu(clist) + i * 4) & 0x1FFFFFFF;
                    if (co == 0 || co >= 0x02000000) continue;
                    int r = Memory.ReadInt(Memory.ToMmu(co) + 0x2C), cc = Memory.ReadInt(Memory.ToMmu(co) + 0x30);
                    uint b0 = (uint)Memory.ReadInt(Memory.ToMmu(co) + 0x24) & 0x1FFFFFFF;
                    uint b1 = (uint)Memory.ReadInt(Memory.ToMmu(co) + 0x28) & 0x1FFFFFFF;
                    int bs = (int)(b1 - b0);
                    cpieces++; cbufBytes += (bs > 0 && bs <= 0x4000) ? bs : 0;
                    grids.Append($" {r}x{cc}");
                }

            // Dedupe on the full footprint signature so we log once per settled state and re-log if it later
            // changes (e.g. cloth finishes loading) — but never spam the same reading.
            string sig = $"{cid}:{root:X}:{nodes}:{meshBytes:X}:{cpieces}:{cbufBytes:X}";
            if (sig == _probeLastSig) return;
            _probeLastSig = sig;
            string[] names = { "Toan", "Xiao", "Goro", "Ruby", "Ungaga", "Osmond" };
            Console.WriteLine($"[Mirage/probe] {names[cid]} (id{cid}): nodes={nodes} span={span} root=0x{root:X} | {meshes} MDT mesh(es)=0x{meshBytes:X} (MeshCave 0x{MirageClone.MeshCaveSize:X}) | cloth {cpieces}pc={cpieces}×0x{MirageClone.ClothObjSize:X}obj +0x{cbufBytes:X}buf grids:{grids}");
        }

        /// <summary>Snapshot the player's cloth pieces into clone-owned copies (frozen drape) and return the guest
        /// address of a cloth-ptr list to hang off the clone's +0xC74. Each CCloth is a self-contained 0x8550 object
        /// with no internal cross-refs; the only fix-ups are its draw-packet fields (+0x18 active, +0x24/+0x28
        /// double-buffer) → a single clone-owned buffer (frozen verts never change, so single-buffering is safe).
        /// Draw__10CCharacter renders the list automatically; nothing steps it in the dungeon, so it stays frozen at
        /// the world-space pose captured now (= where the player stands as the decoy plants). Returns the zero stub
        /// on any read failure so the clone simply gets no cloth rather than a bad pointer.</summary>
        private static uint CopyCloth()
        {
            uint listGuest = (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.ClothList) & 0x1FFFFFFF;
            if (listGuest == 0 || listGuest >= 0x02000000) return (uint)MirageClone.ClothStubGuest;

            // Character-adaptive: walk the WHOLE +0xC74 list (up to ClothMaxPieces) rather than a hardcoded count,
            // so Xiao/Super Steve's cloth works too. Objects are PACKED DENSELY by success count (not source index)
            // so gaps in the list don't waste object slots, and both the object and buffer caves are capacity-guarded
            // (skip-and-log on overflow rather than corrupt). Full 0x8550 copy = physics-ready (Step touches +0x7550+).
            uint[] cloneList = new uint[MirageClone.ClothMaxPieces];   // guest ptrs; 0 = empty (Draw skips)
            int copied = 0, bufOff = 0, anchorOff = 0, boundOff = 0;
            int bufCap = (int)MirageClone.ClothAnchorCave - (int)MirageClone.ClothBufCave;   // room before anchor cave
            uint modelRoot = (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.CharModel) & 0x1FFFFFFF;
            var boundDedupe = new System.Collections.Generic.Dictionary<uint, uint>();   // player bound-head → clone head

            for (int i = 0; i < MirageClone.ClothMaxPieces; i++)
            {
                uint srcObj = (uint)Memory.ReadInt(Memory.ToMmu(listGuest) + i * 4) & 0x1FFFFFFF;
                if (srcObj == 0 || srcObj >= 0x02000000) continue;   // empty list slot

                if (copied >= MirageClone.ClothObjSlots)
                { Console.WriteLine($"[Mirage/cloth] piece {i}: object cave full ({MirageClone.ClothObjSlots} slots) — skip"); continue; }

                byte[] obj = Memory.ReadBytesBatch(Memory.ToMmu(srcObj), MirageClone.ClothObjSize);
                if (obj == null) { Console.WriteLine($"[Mirage/cloth] piece {i}: obj read (0x{MirageClone.ClothObjSize:X}) failed @0x{srcObj:X} — skip"); continue; }

                int rows = BitConverter.ToInt32(obj, 0x2C), cols = BitConverter.ToInt32(obj, 0x30);
                // The two draw buffers (+0x24/+0x28) point at external packets; their gap is the allocated size.
                uint sBuf0 = (uint)BitConverter.ToInt32(obj, MirageClone.ClothBuf0)     & 0x1FFFFFFF;
                uint sBuf1 = (uint)BitConverter.ToInt32(obj, MirageClone.ClothBuf0 + 4) & 0x1FFFFFFF;
                int bufSize = (int)(sBuf1 - sBuf0);
                if (bufSize <= 0 || bufSize > 0x4000)
                { Console.WriteLine($"[Mirage/cloth] piece {i}: odd buf span 0x{sBuf0:X}..0x{sBuf1:X} → fallback 0x2000"); bufSize = 0x2000; }
                bufSize = (bufSize + 0x3F) & ~0x3F;                        // qword-align
                if (bufOff + bufSize > bufCap) { Console.WriteLine($"[Mirage/cloth] piece {i}: buffer cave full — skip"); continue; }

                uint cloneBufGuest = MirageClone.ClothBufGuest + (uint)bufOff;   // single clone-owned buffer
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, MirageClone.ClothActive);       // +0x18 active
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, MirageClone.ClothBuf0);         // +0x24 DBuffID0
                BitConverter.GetBytes(cloneBufGuest).CopyTo(obj, MirageClone.ClothBuf0 + 4);     // +0x28 DBuffID1
                // Leave +0x3c (attach frame) pointing at the player's bone: it's never read while unstepped, and a
                // valid frame is safer than a null if some path ever did step it.

                long cloneObj      = MirageClone.ClothObjCave  + (long)copied * MirageClone.ClothObjSize;   // DENSE
                uint cloneObjGuest = MirageClone.ClothObjGuest + (uint)(copied * MirageClone.ClothObjSize);
                Memory.WriteBytesBatch(cloneObj, obj);
                cloneList[copied] = cloneObjGuest;
                bufOff += bufSize;
                Console.WriteLine($"[Mirage/cloth] piece {i}: grid {rows}x{cols} → obj 0x{cloneObjGuest:X}, buf 0x{bufSize:X} @0x{cloneBufGuest:X}");

                // PHYSICS: re-anchor +0x3c to a clone-space frame so the stepped sim tracks the CLONE skeleton.
                if (ClothPhysics && _cloneNodeCount > 0)
                {
                    uint pAttach = (uint)BitConverter.ToInt32(obj, MirageClone.ClothAttach) & 0x1FFFFFFF;
                    uint cAttach = ResolveCloneAttach(pAttach, modelRoot, ref anchorOff);
                    if (cAttach != 0)
                    { Memory.WriteUInt(cloneObj + MirageClone.ClothAttach, cAttach);
                      Console.WriteLine($"[Mirage/cloth] piece {i}: attach 0x{pAttach:X} → clone 0x{cAttach:X}"); }
                    else
                        Console.WriteLine($"[Mirage/cloth] piece {i}: attach resolve FAILED (0x{pAttach:X}) — cloth would follow player");

                    // Re-anchor the body-collision capsules so the cloth collides with the CLONE, not the player.
                    uint pBounds = (uint)BitConverter.ToInt32(obj, 0x44) & 0x1FFFFFFF;   // CCloth+0x44 = CBound list
                    uint cBounds = ResolveCloneBounds(pBounds, modelRoot, ref anchorOff, ref boundOff, boundDedupe);
                    if (cBounds != 0) Memory.WriteUInt(cloneObj + 0x44, cBounds);
                }
                copied++;
            }

            if (copied == 0) return (uint)MirageClone.ClothStubGuest;

            byte[] listBytes = new byte[MirageClone.ClothMaxPieces * 4];
            for (int i = 0; i < cloneList.Length; i++) BitConverter.GetBytes(cloneList[i]).CopyTo(listBytes, i * 4);
            Memory.WriteBytesBatch(MirageClone.ClothListCave, listBytes);
            Console.WriteLine($"[Mirage/cloth] {copied} piece(s) → clone; {copied}×0x{MirageClone.ClothObjSize:X} obj + 0x{bufOff:X} buf bytes");
            return MirageClone.ClothListGuest;
        }

        /// <summary>Resolve a player cloth attach CFrame to a CLONE-space frame whose world matrix follows the
        /// CLONE skeleton. GetLWMatrix (0x1281b0) recomputes world matrices on-demand by walking +0x110 parents,
        /// so the sim just needs +0x3c to point at a frame that chains up to the clone tree. The clone tree is a
        /// contiguous same-offset copy (clone = NodePool + (player − modelRoot)); attach frames INSIDE the tree map
        /// directly, and frames allocated PAST it are copied into the anchor cave and re-parented to the clone's
        /// copy of their first in-tree ancestor (child/sibling zeroed so GetLWMatrix never walks back into player
        /// frames; world cache zeroed to force recompute). Returns 0 (caller keeps player attach) on any failure.</summary>
        private static uint ResolveCloneAttach(uint playerAttach, uint modelRoot, ref int anchorOff)
        {
            if (playerAttach == 0 || playerAttach >= 0x02000000 || modelRoot == 0) return 0;
            uint treeLo = modelRoot, treeHi = modelRoot + (uint)_cloneNodeCount * 0x270;
            uint CloneOf(uint p) => (uint)MirageClone.NodePoolGuest + (p - modelRoot);   // in-tree same-offset map

            // Walk out-of-tree ancestors (nearest-first) up to the first in-tree frame.
            var outChain = new System.Collections.Generic.List<uint>();
            uint n = playerAttach;
            while (n != 0 && n < 0x02000000 && !(n >= treeLo && n < treeHi))
            {
                outChain.Add(n);
                if (outChain.Count > 32) return 0;   // runaway guard
                n = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.Parent) & 0x1FFFFFFF;
            }
            if (outChain.Count == 0) return CloneOf(playerAttach);   // attach itself is in-tree
            uint inTreeAncestor = n;
            if (inTreeAncestor == 0) return 0;   // chain never reached the tree — can't anchor

            // Copy each out-of-tree frame to the anchor cave; build an old→new(guest) map.
            var map = new System.Collections.Generic.Dictionary<uint, uint>();
            foreach (uint f in outChain)
            {
                if (MirageClone.ClothAnchorCave + anchorOff + 0x270 > MirageClone.ClothAnchorEnd)
                { Console.WriteLine("[Mirage/cloth] anchor cave full"); return 0; }
                byte[] fb = Memory.ReadBytesBatch(Memory.ToMmu(f), 0x270);
                if (fb == null) return 0;
                Memory.WriteBytesBatch(MirageClone.ClothAnchorCave + anchorOff, fb);
                map[f] = MirageClone.ClothAnchorGuest + (uint)anchorOff;
                anchorOff += 0x270;
            }
            // Fix each copy: parent → clone-space, zero child/sibling + world cache.
            foreach (uint f in outChain)
            {
                long copyMmu = MirageClone.ClothAnchorCave + (long)(map[f] - MirageClone.ClothAnchorGuest);
                uint parent  = (uint)Memory.ReadInt(Memory.ToMmu(f) + MirageClone.Parent) & 0x1FFFFFFF;
                uint newParent = map.TryGetValue(parent, out uint mp) ? mp : CloneOf(parent);
                Memory.WriteUInt(copyMmu + MirageClone.Parent,      newParent);
                Memory.WriteInt (copyMmu + MirageClone.RootChild,   0);
                Memory.WriteInt (copyMmu + MirageClone.RootSibling, 0);
                Memory.WriteInt (copyMmu + MirageClone.WorldCacheA, 0);
                Memory.WriteInt (copyMmu + MirageClone.WorldCacheB, 0);
            }
            return map[playerAttach];
        }

        /// <summary>Copy a cloth's CBound collision list (+0x44) into the clone bound cave and re-anchor each
        /// capsule's endpoint bones (+0xe4/+0xe8) to the clone skeleton, so the cloth collides against the CLONE's
        /// body (legs/hips) instead of the player's. Deduped by list head (a character's cloth pieces share one
        /// body-collision list). Returns the clone list head, or 0 (caller keeps the player list) on failure.</summary>
        private static uint ResolveCloneBounds(uint playerHead, uint modelRoot, ref int anchorOff, ref int boundOff,
                                               System.Collections.Generic.Dictionary<uint, uint> dedupe)
        {
            if (playerHead == 0 || playerHead >= 0x02000000) return 0;
            if (dedupe.TryGetValue(playerHead, out uint cached)) return cached;

            var list = new System.Collections.Generic.List<uint>();
            for (uint b = playerHead; b != 0 && b < 0x02000000 && list.Count < 48;
                 b = (uint)Memory.ReadInt(Memory.ToMmu(b) + MirageClone.BoundNext) & 0x1FFFFFFF)
                list.Add(b);

            var map = new System.Collections.Generic.Dictionary<uint, uint>();
            foreach (uint pb in list)
            {
                if (MirageClone.ClothBoundCave + boundOff + MirageClone.BoundSize > MirageClone.ClothBoundEnd)
                { Console.WriteLine("[Mirage/cloth] bound cave full"); break; }
                byte[] bb = Memory.ReadBytesBatch(Memory.ToMmu(pb), MirageClone.BoundSize);
                if (bb == null) break;
                Memory.WriteBytesBatch(MirageClone.ClothBoundCave + boundOff, bb);
                map[pb] = MirageClone.ClothBoundGuest + (uint)boundOff;
                boundOff += MirageClone.BoundSize;
            }
            if (map.Count == 0) return 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (!map.TryGetValue(list[i], out uint copy)) continue;
                long copyMmu = MirageClone.ClothBoundCave + (long)(copy - MirageClone.ClothBoundGuest);
                uint next = (i + 1 < list.Count && map.TryGetValue(list[i + 1], out uint nc)) ? nc : 0;
                Memory.WriteUInt(copyMmu + MirageClone.BoundNext, next);
                foreach (int fo in new[] { MirageClone.BoundFrameA, MirageClone.BoundFrameB })
                {
                    uint pf = (uint)Memory.ReadInt(Memory.ToMmu(list[i]) + fo) & 0x1FFFFFFF;
                    if (pf == 0) continue;
                    uint cf = ResolveCloneAttach(pf, modelRoot, ref anchorOff);
                    if (cf != 0) Memory.WriteUInt(copyMmu + fo, cf);
                }
            }
            uint head = map[list[0]];
            dedupe[playerHead] = head;
            Console.WriteLine($"[Mirage/cloth] bounds: {map.Count} CBound → clone list 0x{head:X}");
            return head;
        }

        /// <summary>Re-assert the ghost's ambient-ADD tint (+0xCE0 RGB) on a chara slot — brighter + blue.</summary>
        private static void WriteGhostTint(long charaSlot)
        {
            Memory.WriteFloat(charaSlot + MirageClone.CharaTint,     GhostSilhouette ? GhostTintR : 0f);
            Memory.WriteFloat(charaSlot + MirageClone.CharaTint + 4, GhostSilhouette ? GhostTintG : 0f);
            Memory.WriteFloat(charaSlot + MirageClone.CharaTint + 8, GhostSilhouette ? GhostTintB : 0f);
        }

        private static void MaintainClone()
        {
            if (_cloneSlot < 0) return;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;
            // Hold at the decoy, keep the model + active/opacity set, and keep it registered for draw
            // (the game may reset the registry / opacity-ramp between our ticks).
            Memory.WriteFloat(slot + MirageClone.CharPos,     _dx);
            Memory.WriteFloat(slot + MirageClone.CharPos + 4, _dz);
            Memory.WriteFloat(slot + MirageClone.CharPos + 8, _dy);
            Memory.WriteUInt (slot + MirageClone.CharModel,   _cloneRootGuest);

            Memory.WriteInt  (slot + MirageClone.CharaActive, 1);
            Memory.WriteFloat(slot + MirageClone.NpcOpacity,  GhostSilhouette ? GhostOpacity : 128f);
            WriteGhostTint(slot);
            Memory.WriteInt  (MirageClone.CharaRegistry + (long)_cloneSlot * 4, 1);   // draw loop draws it
            Memory.WriteInt  (MirageClone.StepSkipTable + (long)_cloneSlot * 4, 0);   // step loop STEPS it (DespawnClone set this to 1; must clear on re-cast or physics only works once)
            uint dngMap = (uint)Memory.ReadInt(0x202A34B8) & 0x1FFFFFFF;   // NowDngMap
            SetupCloneHaze(dngMap);     // one-shot: claim a spare fire slot + mark the clone's tile (heat-haze)
            MaintainCloneHaze(dngMap);  // re-assert the tile marker each tick

            // Engine-driven animation: the engine's own chara-step (unlocked via the PNACH step-gate NOP) poses
            // the clone tree at 60fps — no polling. Re-assert the step flags (game may reset between our ticks)
            // and HOLD motion 9 (guard loop) so the clone stays in a clean guard pose regardless of what the
            // player is doing — the step reads +0xC68 each frame, so pinning it here keeps the motion stable.
            if (EngineDrivenAnim)
            {
                Memory.WriteInt(slot + MirageClone.CharaMotionA, 1);
                Memory.WriteInt(slot + MirageClone.CharaRampA,   0);
                Memory.WriteInt(slot + MirageClone.MotionId,     GuardLoopMotion);
            }
            // (Legacy per-tick TRS poll disabled: it fought the 60fps step and read as jitter.)

            // Re-assert the weapon chara slot (drawn, never stepped) — the game may reset registry/opacity.
            if (_weaponRootGuest != 0)
            {
                long wslot = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
                Memory.WriteUInt (wslot + MirageClone.CharModel,   _weaponRootGuest);
                Memory.WriteInt  (wslot + MirageClone.CharaActive, 1);
                Memory.WriteFloat(wslot + MirageClone.NpcOpacity,  GhostSilhouette ? GhostOpacity : 128f);
                WriteGhostTint(wslot);
                Memory.WriteInt  (wslot + MirageClone.CharaMotionA, 0);
                Memory.WriteInt  (MirageClone.StepSkipTable + (long)MirageClone.WeaponCharaSlot * 4, 1);
                Memory.WriteInt  (MirageClone.CharaRegistry + (long)MirageClone.WeaponCharaSlot * 4, 1);
            }

            // NOTE: iGpffff9e18 (0x202A3608) is the "scene active" flag — setting it draws the chara loop BUT
            // freezes enemies (motionDrive steps enemies only when it's 0). So we DON'T set it here; the clone
            // is drawn instead via an in-place patch of Draw__11CSeireiKing's chara-loop gate (SceneDrawPatch),
            // which lets the loop run while the flag stays 0 so enemies keep moving.
        }

        private static void DespawnClone()
        {
            // (SceneGateFlag is driven by the Loop now: 2 in-dungeon → PNACH restores vanilla gates, 0 in town.)
            TeardownCloneHaze((uint)Memory.ReadInt(0x202A34B8) & 0x1FFFFFFF);   // restore the clone tile + free our fire slot
            if (_cloneSlot < 0) return;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;
            Memory.WriteInt (MirageClone.CharaRegistry + (long)_cloneSlot * 4, 0);   // unregister (draw)
            Memory.WriteInt (MirageClone.StepSkipTable + (long)_cloneSlot * 4, 1);   // step loop skips it (belt+braces)
            Memory.WriteInt (slot + MirageClone.CharaActive,  0);                    // inactive
            Memory.WriteInt (slot + MirageClone.CharaMotionA, 0);                    // don't step (leftover clone)
            Memory.WriteUInt(slot + MirageClone.CharModel, 0);                       // clear model

            if (_weaponRootGuest != 0)   // tear down the weapon chara slot too
            {
                long wslot = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
                Memory.WriteInt (MirageClone.CharaRegistry + (long)MirageClone.WeaponCharaSlot * 4, 0);
                Memory.WriteInt (MirageClone.StepSkipTable + (long)MirageClone.WeaponCharaSlot * 4, 0);
                Memory.WriteInt (wslot + MirageClone.CharaActive, 0);
                Memory.WriteUInt(wslot + MirageClone.CharModel, 0);
                _weaponRootGuest = 0;
            }
            _cloneSlot = -1;
        }

        private const int TexMgrMax = 0xC4;   // 196 CTexture slots (Initialize__15CTextureManager inits 0xC4) — HARD cap

    }
}
