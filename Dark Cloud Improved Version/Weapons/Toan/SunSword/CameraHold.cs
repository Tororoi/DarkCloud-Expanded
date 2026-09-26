using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The camera HELD at its world height while Toan lunges. The follow camera's height field is its height above its
    /// follow point, and the follow point rises and falls with him, so the camera would rise and fall too; while the
    /// pin flag is set the camera-pin cave (CodeCaves.DebugInfoCave.CameraPin, run at the end of the dungeon camera
    /// pass every frame) re-derives that field from the pinned world height (CodeCaves.CameraPin) and the follow
    /// point, so the camera stays put in Y. Distance and angle stay the engine's. Nothing per tick.
    /// </summary>
    internal static class CameraHold
    {
        private static bool _pinned;

        /// <summary>Hold the camera at its world height now (its follow point's height plus its height field).</summary>
        internal static void Pin()
        {
            uint p = Memory.ReadGuestPtr(DungeonCamera.NowCamera);
            if (!Memory.IsValidGuest(p)) return;
            long cam = Memory.ToMmu(p);
            float ry = Memory.ReadFloat(cam + FollowCamera.RefY), h = Memory.ReadFloat(cam + FollowCamera.Height);
            Memory.WriteFloat(CodeCaves.CameraPin + 4, ry + h);
            Memory.WriteInt(CodeCaves.CameraPin + CodeCaves.CameraPinFlag, 1);
            _pinned = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[CameraHold] camera height pinned at {ry + h:F1} (follow point {ry:F1} + {h:F1})");
        }
        /// <summary>Let the camera follow again (the engine eases its height from where the pin left it).</summary>
        internal static void Unpin()
        {
            if (!_pinned) return;
            Memory.WriteInt(CodeCaves.CameraPin + CodeCaves.CameraPinFlag, 0);
            _pinned = false;
        }
    }
}
