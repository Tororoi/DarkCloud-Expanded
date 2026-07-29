using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Let Brownboo's follow-camera pass THROUGH the buildings (paired with IsoPatcher.CullBuildings, which makes
    /// the houses see-through from inside).
    ///
    /// The town camera and the player use DIFFERENT per-part collision frames: the player reads
    /// <c>part+0xD0</c> (GetCollisionFrame__10CMapObject 0x1574f0), the camera reads the frame the
    /// camera gather (PickUpCameraPoly__11CEditGround 0x1a5210) walks. That gather scans TWO separate
    /// per-part arrays in the CEditGround object:
    ///   • map-parts  — base +0x30,     stride 0x2A0, 0x80 slots; active @+0xE8,    camera frame @+0xDC.
    ///   • static-parts — base +0x0,     stride 0x2A0, 0x40 slots; active @+0x16028, camera frame @+0x1601C.
    /// (A third pass gathers the ground grid, which keeps the camera above the floor — left untouched.)
    ///
    /// Brownboo's houses come from scene.scn and load as STATIC-parts, so clearing only the map-parts frame
    /// (the original fix) left the camera colliding with them. We now zero BOTH camera frames. Zeroing a
    /// camera frame drops that part from the CAMERA gather only — the player still walks into it (uses +0xD0),
    /// and the ground grid still holds the camera above the floor.
    ///
    /// Pure per-frame data write, gated to Brownboo (MapNo 14). The frame pointers are set at part load, so a
    /// fresh town build re-populates them — running every tick re-clears them.
    /// </summary>
    internal static class CameraPassThrough
    {
        // Disabled while testing the dungeon-style dynamic pull-in (IsoPatcher.PatchTownCamera). Brownboo
        // and Queens are exactly where the pull-in must be judged, so this runtime pass-through would mask it.
        // Flip back to true if the baked camera patch needs to be rolled back.
        internal static bool Enabled = false;

        // Custom fishing towns whose follow-camera should pass through buildings (Brownboo 14, Queens 2). The
        // map-parts/static-parts gather is generic per-town, so the same clear applies to any of them.
        private static readonly int[] Towns = { 14, 2 };
        private const long EditGroundPtr = 0x21D1968C;   // holds the CEditGround* (DAT_01d1968c / uVar10 in EdMoveChara)

        // map-parts array (georama-placed tiles)
        private const int  MapPartsBase  = 0x30;
        private const int  MapPartStride = 0x2A0;
        private const int  MapPartCount  = 0x80;
        private const int  MapActive     = 0xE8;         // part live when >= 0
        private const int  MapCameraFrame = 0xDC;        // camera-only collision frame (player uses +0xD0)

        // static-parts array (fixed scene.scn geometry — crater walls, boardwalk, HOUSES)
        private const int  StaticPartsBase = 0x0;
        private const int  StaticPartStride = 0x2A0;
        private const int  StaticPartCount  = 0x40;
        private const int  StaticActive     = 0x16028;   // part live when >= 0
        private const int  StaticCameraFrame = 0x1601C;  // camera-only collision frame

        internal static void Apply(int mapNo)
        {
            if (!Enabled || Array.IndexOf(Towns, mapNo) < 0) return;

            uint p = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long eg = Memory.ToMmu(p);

            for (int i = 0; i < MapPartCount; i++)
            {
                long part = eg + MapPartsBase + (long)i * MapPartStride;
                if (Memory.ReadInt(part + MapActive) < 0) continue;         // inactive slot
                if (Memory.ReadInt(part + MapCameraFrame) != 0)
                    Memory.WriteInt(part + MapCameraFrame, 0);              // drop from the camera collision gather
            }

            for (int i = 0; i < StaticPartCount; i++)
            {
                long part = eg + StaticPartsBase + (long)i * StaticPartStride;
                if (Memory.ReadInt(part + StaticActive) < 0) continue;      // inactive slot
                if (Memory.ReadInt(part + StaticCameraFrame) != 0)
                    Memory.WriteInt(part + StaticCameraFrame, 0);           // drop from the camera collision gather
            }
        }
    }
}
