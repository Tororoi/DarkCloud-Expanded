using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// SCREEN-SPACE HEAT SHIMMER AT AN ARBITRARY WORLD POSITION — pure data, no cave, no code injection.
    ///
    /// The game has exactly ONE framebuffer distortion: CFireOmni::DrawRaster (blendTextuerTest + MGGetFBuffTex),
    /// the heat-haze over a fire. It is not a positionable effect — it is driven by DrawRaster__11CDungeonMap,
    /// which walks the dungeon's fire-tile grid. (The CEffect system is additive sprites only; there is no other
    /// refraction primitive in the game, water included.)
    ///
    /// So we HIJACK a torch: append an emitter to an existing lit tile's fire struct, aim it wherever we like,
    /// and zero its flame/light bits so only the shimmer draws. The torch keeps its own flame. Everything is
    /// restored on <see cref="Hide"/>.
    ///
    /// Hard-won details, all of which are engine facts rather than caller concerns:
    ///  • DrawRaster only walks tiles within ±4 of the CAMERA, and only those with dist &lt;= 240 — and DrawMap
    ///    RECOMPUTES that dist every frame, so it cannot be forced from the mod. We therefore RE-ANCHOR to
    ///    whichever lit tile is nearest the player (a camera proxy) as they move, and the PNACH relaxes the 240
    ///    gate while the effect is up. Anchor to a distant torch and the shimmer silently vanishes in hallways.
    ///  • The emitter offset is ROTATED by the tile's own orientation ((4-rot)*90°), so we pre-apply the inverse
    ///    — otherwise the shimmer drifts off-target by an amount that grows with the anchor's distance.
    ///  • DrawFire walks the SAME emitter list and would draw our emitter as a flame + light; the per-emitter
    ///    flag byte (bit0 = light, bit1 = flame) must be zeroed. DrawRaster never reads it.
    ///  • Strength is a pure multiplicative gain in RODATA (FireRaster.DistortionGain), so it can be ramped
    ///    per-tick as DATA. Patching the amplitude CODE constant every frame would be exactly the
    ///    recompiler-crashing hot-code surgery we avoid everywhere.
    ///
    /// Requires a dungeon with at least one lit fire — that is also what guarantees the distortion textures are
    /// resident. In a fireless dungeon <see cref="Show"/> simply returns false and draws nothing.
    /// </summary>
    internal static class HeatHaze
    {
        private const int   Cols      = DungeonAddresses.MapPartsGrid.Columns;     // 20
        private const float TileWorld = DungeonAddresses.MapPartsGrid.CellWorld;   // 160 world units per tile
        private const int   SaveOff   = 0x4A0;   // saved window: count(+0x4A2), emitter pos array(+0x4B0..), flags(+0x510..)
        private const int   SaveLen   = 0x80;
        private const double KeepAnchorDist = 480.0;   // 3 tiles — hysteresis, so we don't thrash between equidistant torches

        private static int    _fireIdx = -1;   // hijacked torch's fire-struct index (-2 = no fire here, -1 = not yet anchored)
        private static int    _tileIdx = -1;
        private static int    _col, _row, _rot;
        private static int    _emitIdx;        // emitter slot we own (appended AFTER the torch's own, so it keeps its flames)
        private static byte[] _saved;          // the torch's original bytes, restored on Hide()
        private static float  _gainOrig;       // vanilla distortion gain (~1.3), captured once

        internal static bool IsShowing => _fireIdx >= 0;

        /// <summary>Render the shimmer at a world position with strength <paramref name="gain01"/> (0..1).
        /// Call EVERY TICK while it should be visible — it re-anchors and re-asserts as the camera moves.
        /// Returns false if the dungeon has no fire to hijack (nothing is drawn; this is not an error).</summary>
        internal static bool Show(float worldX, float worldY, float worldZ, float gain01)
        {
            uint dngMap = (uint)Memory.ReadInt(DungeonAddresses.Map.NowDngMapPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(dngMap)) return false;
            if (_fireIdx == -2) return false;               // already established there's no fire in this dungeon

            // Capture the vanilla gain ONCE, before we ever ramp it — never latch a value we ourselves wrote.
            if (_gainOrig <= 0f)
            {
                float g = Memory.ReadFloat(FireRaster.DistortionGain);
                _gainOrig = g > 0f && g < 100f ? g : 1.3f;   // sane-range guard; 1.3 is the shipped value
            }

            // Anchor to the lit tile nearest the PLAYER — a stand-in for the camera, which is what DrawRaster's
            // ±4-tile window is actually centred on.
            float px = Memory.ReadFloat(Addresses.dunPositionX);
            float py = Memory.ReadFloat(Addresses.dunPositionY);
            if (!UpdateAnchor(dngMap, px, py, worldX, worldY, worldZ))
            {
                _fireIdx = -2;   // no fire anywhere in this dungeon → give up quietly
                return false;
            }
            Memory.WriteFloat(FireRaster.DistortionGain, Math.Clamp(gain01, 0f, 1f) * _gainOrig);
            return true;
        }

        /// <summary>Hand the torch back and restore the vanilla distortion gain. Safe to call when not showing.</summary>
        internal static void Hide()
        {
            uint dngMap = (uint)Memory.ReadInt(DungeonAddresses.Map.NowDngMapPtr) & Memory.PhysAddrMask;
            RestoreAnchor(dngMap);
            if (_gainOrig > 0f) Memory.WriteFloat(FireRaster.DistortionGain, _gainOrig);
            _fireIdx = -1; _tileIdx = -1; _saved = null;
        }

        /// <summary>Pick the lit tile nearest (refX,refY) as our anchor, handing the emitter off to a nearer one
        /// as the camera roams. False only if the dungeon contains no lit fire at all.</summary>
        private static bool UpdateAnchor(uint dngMap, float refX, float refY, float wx, float wy, float wz)
        {
            long b = Memory.ToMmu(dngMap);

            // Keep the current anchor while it's still comfortably inside the camera window.
            if (_fireIdx >= 0 && _tileIdx >= 0)
            {
                double kx = _col * (double)TileWorld - refX, ky = _row * (double)TileWorld - refY;
                if (kx * kx + ky * ky <= KeepAnchorDist * KeepAnchorDist) { WriteEmitter(dngMap, wx, wy, wz); return true; }
            }

            byte[] tiles = Memory.ReadBytesBatch(b + FireRaster.TileArray, FireRaster.TileCount * FireRaster.TileStride);
            if (tiles == null) return _fireIdx >= 0;

            int bestT = -1, bestFi = -1; double bestD2 = double.MaxValue;
            for (int t = 0; t < FireRaster.TileCount; t++)
            {
                int off = t * FireRaster.TileStride;
                if (BitConverter.ToInt32(tiles, off + FireRaster.TileEnabled) != 1) continue;
                int fi = BitConverter.ToInt32(tiles, off + FireRaster.TileFireIdx);
                if (fi < 1 || fi > 200) continue;
                double tx = (t % Cols) * (double)TileWorld, tz = (t / Cols) * (double)TileWorld;
                double d2 = (tx - refX) * (tx - refX) + (tz - refY) * (tz - refY);
                if (d2 < bestD2) { bestT = t; bestFi = fi; bestD2 = d2; }
            }
            if (bestT < 0) return _fireIdx >= 0;   // nothing lit found this scan

            if (bestT != _tileIdx || bestFi != _fireIdx)
            {
                RestoreAnchor(dngMap);   // give the previous torch its bytes back before taking a new one
                _tileIdx = bestT; _fireIdx = bestFi; _col = bestT % Cols; _row = bestT / Cols;
                _rot = BitConverter.ToInt32(tiles, bestT * FireRaster.TileStride + FireRaster.TileRot);
                _saved = Memory.ReadBytesBatch(b + (long)bestFi * FireRaster.Stride + SaveOff, SaveLen);
                // APPEND after the torch's own emitters rather than overwriting slot 0, so a torch that really
                // does have fire emitters keeps its flames. Capped: emitter pos[i] lives at +0x4B0 + i*0x10, so
                // pos[6] would land on the flag array at +0x510.
                int own = _saved != null ? BitConverter.ToInt16(_saved, FireRaster.EmitCount - SaveOff) : 0;
                _emitIdx = (own >= 1 && own <= FireRaster.MaxEmitSlot) ? own : 0;
            }
            WriteEmitter(dngMap, wx, wy, wz);
            return true;
        }

        /// <summary>Aim our emitter at a world point. DrawRaster places emitter i at
        /// world((localX + col*16)*10, localY*10, (localZ + row*16)*10) AFTER rotating (localX,localZ) by
        /// θ = (4-rot)*90°, so we invert that rotation to land exactly on target from any anchor.</summary>
        private static void WriteEmitter(uint dngMap, float wx, float wy, float wz)
        {
            long fs = Memory.ToMmu(dngMap) + (long)_fireIdx * FireRaster.Stride;
            double dxc = (wx - _col * (double)TileWorld) / 10.0;
            double dzc = (wz - _row * (double)TileWorld) / 10.0;
            double th = (4 - _rot) * (Math.PI / 2.0), c = Math.Cos(th), s = Math.Sin(th);
            int i = _emitIdx;

            Memory.WriteBytesBatch(fs + FireRaster.EmitCount, new byte[] { (byte)(i + 1), 0 });   // count = our slot + 1
            Memory.WriteFloat(fs + FireRaster.EmitPos     + i * FireRaster.EmitPosStride, (float)(dxc * c - dzc * s));
            Memory.WriteFloat(fs + FireRaster.EmitPos + 4 + i * FireRaster.EmitPosStride, wy / 10f);
            Memory.WriteFloat(fs + FireRaster.EmitPos + 8 + i * FireRaster.EmitPosStride, (float)(dxc * s + dzc * c));
            // Zero the flame/light bitmask or DrawFire renders a flame + glow under our shimmer.
            Memory.WriteBytesBatch(fs + FireRaster.EmitFlags + i, new byte[] { 0 });
        }

        private static void RestoreAnchor(uint dngMap)
        {
            if (_fireIdx < 0 || _saved == null || !Memory.IsValidGuest(dngMap)) return;
            Memory.WriteBytesBatch(Memory.ToMmu(dngMap) + (long)_fireIdx * FireRaster.Stride + SaveOff, _saved);
        }
    }
}
