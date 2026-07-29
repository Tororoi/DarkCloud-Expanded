using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Make the town camera collide against the PLAYER collision surfaces (the visible walls) instead of
    /// the default CAMERA collision polys. Ray-casting player→camera against the town mesh showed the camera
    /// parks a consistent ~40 units IN FRONT of the visible walls — it's hitting the game's camera-collision
    /// geometry, which is pushed out from the real walls. The player collision is at the visible wall (that's
    /// where you stop walking), so we point the camera-collision frame (+0xDC / +0x1601C) at the player
    /// collision frame (+0xD0 / +0x16010) for every active part — REPLACE, not just fill, so the ~26 parts
    /// that already have (short) camera frames get corrected too.
    ///
    /// Downside: player collision includes INVISIBLE boundaries (walk-blockers with no visible wall), so the
    /// camera will also hug those until we filter them. DumpPositions logs every aliased part's world
    /// position so we can identify the invisible ones (positions with no building) and exclude them next.
    /// </summary>
    internal static class CameraWallCollision
    {
        // DISABLED: the collision bake now writes a proper custom CAMERA collision into the ground `_c` variant
        // (terrain + canal + perimeter, no triggers — bake_player_camera_collision), so the camera reads the
        // NATIVE `_c` frame directly and no player-frame aliasing is needed. Buildings keep their vanilla `_a`
        // (player) and `_c` (camera). Re-enable only to revert to the old shared-`_a` alias scheme.
        internal static bool Enabled = false;
        internal static bool DumpPositions = true;   // one-shot per town: log part positions to find invisible walls

        // EXPERIMENT: gather ONLY the custom split-terrain collision baked into the ground parts — skip
        // buildings entirely, and (inverting the normal exclusion) alias ONLY the origin base-ground static
        // parts. Pairs with tools/bake_player_camera_collision.py. Set false for the normal all-parts alias.
        internal static bool TerrainOnly = true;

        private const long EditGroundPtr = 0x21D1968C;

        // map-parts (georama tiles / buildings)
        private const int MapBase = 0x30, MapStride = 0x2A0, MapCount = 0x80;
        private const int MapActive = 0xE8, MapCamFrame = 0xDC, MapPlayerFrame = 0xD0;
        private const int MapPosX = 0x10;   // part world pos x,y,z at +0x10/0x14/0x18 (PickUpCameraPoly SetPosition args)
        // static-parts (scene.scn buildings / crater walls)
        private const int StBase = 0x0, StStride = 0x2A0, StCount = 0x40;
        private const int StActive = 0x16028, StCamFrame = 0x1601C, StPlayerFrame = 0x16010;
        private const int StPosX = 0x15F50;

        private static bool _dumped;

        internal static void Tick()
        {
            if (!Enabled) return;
            uint egp = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egp)) { _dumped = false; return; }
            long eg = Memory.ToMmu(egp);

            var dump = DumpPositions && !_dumped ? new List<string>() : null;

            // MAP parts (buildings). In TerrainOnly experiment mode we skip them entirely so the camera
            // never gathers building collision.
            if (!TerrainOnly)
            {
                for (int i = 0; i < MapCount; i++)
                {
                    long part = eg + MapBase + (long)i * MapStride;
                    if (Memory.ReadInt(part + MapActive) < 0) continue;
                    int ply = Memory.ReadInt(part + MapPlayerFrame);
                    if (ply == 0) continue;
                    Memory.WriteInt(part + MapCamFrame, ply);   // camera now sees the player collision surface
                    dump?.Add($"MAP[{i}] ({Memory.ReadFloat(part + MapPosX):0.#},{Memory.ReadFloat(part + MapPosX + 4):0.#},{Memory.ReadFloat(part + MapPosX + 8):0.#})");
                }
            }

            for (int i = 0; i < StCount; i++)
            {
                long part = eg + StBase + (long)i * StStride;
                if (Memory.ReadInt(part + StActive) < 0) continue;
                int ply = Memory.ReadInt(part + StPlayerFrame);
                if (ply == 0) continue;
                float sx = Memory.ReadFloat(part + StPosX), sy = Memory.ReadFloat(part + StPosX + 4), sz = Memory.ReadFloat(part + StPosX + 8);
                bool origin = (sx == 0f && sy == 0f && sz == 0f);
                // TerrainOnly: gather ONLY the origin base-ground parts (our custom split terrain lives there).
                // Normal mode: gather everything EXCEPT the origin base-ground (its terrain has invisible canal walls).
                if (TerrainOnly ? !origin : origin) continue;
                // Single shared `_a`: camera and player both gather the whole ground collision. The 5-unit
                // canal walls clear the camera by height, so no camera/player split (camcol) is needed.
                Memory.WriteInt(part + StCamFrame, ply);
                dump?.Add($"STATIC[{i}] ({sx:0.#},{sy:0.#},{sz:0.#})");
            }

            if (dump != null && dump.Count > 0)
            {
                Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[CameraWall] aliased {dump.Count} parts to player collision:");
                foreach (var s in dump) Console.WriteLine("    " + s);
                _dumped = true;
            }
        }

    }
}
