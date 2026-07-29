using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMP diagnostic for the town-camera collision work. Logs the town follow-camera's INTERNAL distance
    /// (+0x2d0 — the value Step__13CCameraFollow actually uses to place the camera) and angle (+0x2d8).
    ///
    /// The question this answers: when the camera is behind a building, does the collision pull-in fire AT
    /// ALL? If distance stays pinned at the far value (~80) even when the camera is clearly behind a wall,
    /// then the collision is never DETECTED (bVar6 stays true → the rest-pin forces distance back to far
    /// every frame) — which would explain why every response-side patch (SetPos, SetDistance, rotate-free)
    /// changes nothing. If instead distance drops but snaps back, the problem is downstream.
    ///
    /// Logs both the static follow-camera object (0x21D34840) and the live pointer EdMoveChara uses
    /// (*(0x21D19678) = iVar8) so we can tell which one is real. Runtime only; delete when done.
    /// </summary>
    internal static class CameraDiag
    {
        internal static bool Enabled = true;
        // FORCE TEST: write dist=40 every tick. If the logged dist (read at the next tick, after several game
        // frames) reads back ~40, SetDistance sticks and nothing forces it -> the collision just never calls it.
        // If it reads back ~80, the game is hard-forcing far every frame (the rest-pin) -> that's the culprit.
        internal static bool ForceDist = false;   // off: we're testing the smooth-rest patch now, just observe dist
        private const float ForceValue = 40f;

        private const long CamPtrVar = 0x21D19678;   // DAT_01d19678 = iVar8, the follow-camera EdMoveChara drives
        private const long NowCamPtr = 0x202A3498;   // NowCamera pointer (the active camera)
        private const int  OffHeight = 0x2D4, OffDist = 0x2D0, OffAngle = 0x2D8;

        private static float _lastDist = float.NaN, _lastAng = float.NaN;
        private static int   _tick;

        internal static void Tick()
        {
            if (!Enabled) return;
            _tick++;

            uint p = Memory.ReadUInt(CamPtrVar) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long cam = Memory.ToMmu(p);

            float dist = Memory.ReadFloat(cam + OffDist);
            float ang  = Memory.ReadFloat(cam + OffAngle);
            float h    = Memory.ReadFloat(cam + OffHeight);

            // ref = the follow look-at (near the player); camera world pos derived exactly as Step does:
            //   pos = ref + dist*sin(angle) [x], ref.y + height [y], ref.z + dist*cos(angle) [z]
            float rx = Memory.ReadFloat(cam + 0x2C0);
            float ry = Memory.ReadFloat(cam + 0x2C4);
            float rz = Memory.ReadFloat(cam + 0x2C8);
            float cx = (float)(rx + dist * Math.Sin(ang));
            float cy = ry + h;
            float cz = (float)(rz + dist * Math.Cos(ang));
            int mapNo = Memory.ReadInt(0x202A2518);

            // Trigger on distance OR angle movement (so we can see if it rotates at all), plus a slow heartbeat.
            if (float.IsNaN(_lastDist) || Math.Abs(dist - _lastDist) > 0.3f
                || Math.Abs(ang - _lastAng) > 0.02f || _tick % 90 == 0)
            {
                Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[CameraDiag] map={mapNo} ref=({rx:0.#},{ry:0.#},{rz:0.#}) campos=({cx:0.#},{cy:0.#},{cz:0.#}) dist={dist:0.0} angle={ang:0.00} height={h:0.0}");
                _lastDist = dist; _lastAng = ang;
            }

            // Force the distance small; next tick's read shows whether the game snapped it back to far.
            if (ForceDist) Memory.WriteFloat(cam + OffDist, ForceValue);

            if (_tick % 120 == 0) DumpParts();
        }

        // Count how many map/static parts carry a PLAYER collision frame (+0xd0/+0x16010) vs a CAMERA
        // collision frame (+0xdc/+0x1601c). PickUpCameraPoly gathers ONLY the camera frames, so if parts have
        // player collision but no camera frame, the camera literally cannot detect the wall -> never pulls in.
        private const long EditGroundPtr = 0x21D1968C;
        private static void DumpParts()
        {
            uint egp = Memory.ReadUInt(EditGroundPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(egp)) return;
            long eg = Memory.ToMmu(egp);

            int mAct = 0, mCam = 0, mPly = 0;
            for (int i = 0; i < 0x80; i++)
            {
                long part = eg + 0x30 + (long)i * 0x2A0;
                if (Memory.ReadInt(part + 0xE8) < 0) continue;          // inactive
                mAct++;
                if (Memory.ReadInt(part + 0xDC) != 0) mCam++;           // camera collision frame
                if (Memory.ReadInt(part + 0xD0) != 0) mPly++;           // player collision frame
            }
            int sAct = 0, sCam = 0, sPly = 0;
            for (int i = 0; i < 0x40; i++)
            {
                long part = eg + (long)i * 0x2A0;
                if (Memory.ReadInt(part + 0x16028) < 0) continue;       // inactive
                sAct++;
                if (Memory.ReadInt(part + 0x1601C) != 0) sCam++;        // camera collision frame
                if (Memory.ReadInt(part + 0x16010) != 0) sPly++;        // player collision frame (guessed offset)
            }
            Console.WriteLine($"{ReusableFunctions.GetDateTimeForLog()}[CameraDiag] MAP parts active={mAct} cam(+0xDC)={mCam} player(+0xD0)={mPly}  |  STATIC active={sAct} cam(+0x1601C)={sCam} player(+0x16010)={sPly}");
        }
    }
}
