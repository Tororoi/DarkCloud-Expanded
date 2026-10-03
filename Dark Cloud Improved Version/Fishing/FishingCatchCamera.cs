using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The fishing camera put back after a catch. EdMoveChara's chara_fishing states 0xB (the fish held up) and 0xC (put
    /// away) close the camera in every frame — SetAngle(the player's facing), SetDistance 40 then 30 — and state 0xC returns
    /// to 0 (fishing) without restoring anything; fishing re-asserts only the height (the patched SetHeight site), and a
    /// session never sets distance or angle itself (it keeps whatever it began with), so they stayed at the close-up until
    /// the session ended. This keeps the camera's distance and target yaw while fishing and writes them back the moment the
    /// catch sequence ends (the smoothed yaw eases to the target on its own).
    /// </summary>
    internal static class FishingCatchCamera
    {
        private const string Tag = "[FishingCatchCamera] ";
        private const int StateShowFish = 0xB, StatePutAway = 0xC, StateLanded = 8;   // chara_fishing: 8 lasts one frame, then 0xB

        private static bool _have, _inCatch;
        private static float _dist, _angle;

        /// <summary>Every town tick.</summary>
        internal static void Tick()
        {
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeFishing) { _have = false; _inCatch = false; return; }
            uint p = Memory.ReadGuestPtr(FollowCamera.Ptr);
            if (!Memory.IsValidGuest(p)) return;
            long cam = Memory.ToMmu(p);
            int state = Memory.ReadInt(FishingAddresses.FishCatchConfirm);                  // chara_fishing
            bool catching = state == StateLanded || state == StateShowFish || state == StatePutAway;
            if (catching) { _inCatch = true; return; }
            if (_inCatch)
            {
                _inCatch = false;
                if (_have)
                {
                    Memory.WriteFloat(cam + FollowCamera.Dist, _dist);
                    Memory.WriteFloat(cam + FollowCamera.Angle, _angle);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"catch over: camera back to distance {_dist:F1}, yaw {_angle:F2}");
                }
                return;
            }
            _dist = Memory.ReadFloat(cam + FollowCamera.Dist);
            _angle = Memory.ReadFloat(cam + FollowCamera.Angle);
            _have = _dist > 0f && !float.IsNaN(_dist) && !float.IsNaN(_angle);
        }
    }
}
