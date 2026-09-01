using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Relocate + enlarge the town camera-collision gather arena (the "400-poly buffer").
    ///
    /// EdMoveChara's per-frame gather does <c>used=0; Alloc(WorkBuffer, 2000)</c> — a 32 KB arena
    /// (@0x01C16F00, cap 2048×0x10) that <c>PickUpCameraPoly</c> fills with NO bounds check. Brownboo's
    /// gather measured 425–674 CCPolys (Mailbox.CamGatherCount): at 674 the gather writes ~21 KB PAST the
    /// arena end into neighbouring engine globals — every frame — and the polys stored out there are
    /// corruptible by their real owners before the collision casts read them (the rock clipping, the
    /// impossible dist=98.4 &gt; BASE readings). A bounds check would only swap corruption for silent
    /// truncation (dropping REAL walls/floors); the correct fix is a bigger arena so the COMPLETE gather
    /// fits — CheckHit then iterates every real poly, contiguous and unclobbered.
    ///
    /// New home: the Mirage clone's MeshCave slice (0x01F56400, 0x58000 B) in the proven-clean heap tail
    /// (CodeCaveScanner, 68 sessions). It is DUNGEON-only (software-skinned clone meshes) and the town
    /// camera is TOWN-only, so they time-share safely; the arena is per-frame scratch, re-filled by every
    /// town camera frame, and Mirage rebuilds its meshes from scratch on every summon. 0x5800 units =
    /// 4505 polys — the full Brownboo gather with ~6× headroom.
    ///
    /// Pure data writes through the engine's own pointer (struct @*0x202A2388: {+0 data, +8 used, +C cap});
    /// asserted while in TOWN mode (2), restored to vanilla when leaving it. Fully revertible.
    /// </summary>
    internal static class TownCameraPolyBuffer
    {
        internal static bool Enabled = true;
        internal static bool Diagnostics = false;   // log the redirect/restore transitions

        private const long WorkBufferPtr = FollowCamera.WorkBufferPtr;   // -> struct {dataPtr, ?, used, cap}
        private const uint NewBaseGuest  = 0x01F56400;   // Mirage MeshCave (dungeon-only) — see class doc
        private const int  NewCapUnits   = 0x5800;       // 0x58000 B / 0x10 = 4505 CCPolys' worth

        private static uint _origBase;
        private static int  _origCap;
        private static bool _redirected;

        internal static void Tick()
        {
            if (!Enabled) return;
            uint structRaw = Memory.ReadUInt(WorkBufferPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(structRaw)) return;
            long s = Memory.ToMmu(structRaw);
            uint data = Memory.ReadUInt(s) & Memory.PhysAddrMask;

            bool town = Memory.ReadByte(Addresses.mode) == 2;
            if (town)
            {
                if (data == NewBaseGuest) return;               // already ours
                if (!Memory.IsValidGuest(data)) return;
                _origBase = data;                                // vanilla arena (0x01C16F00 observed)
                _origCap  = Memory.ReadInt(s + 0xC);
                Memory.WriteUInt(s, NewBaseGuest);
                Memory.WriteInt(s + 0xC, NewCapUnits);
                _redirected = true;
                if (Diagnostics)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[CameraPolyBuffer] gather arena -> 0x{NewBaseGuest:X8} cap {NewCapUnits} units " +
                        $"({NewCapUnits / 5} polys; was 0x{_origBase:X8} cap {_origCap})");
            }
            else if (_redirected && data == NewBaseGuest && _origBase != 0)
            {
                Memory.WriteUInt(s, _origBase);                  // hand MeshCave back before any dungeon Mirage
                Memory.WriteInt(s + 0xC, _origCap);
                _redirected = false;
                if (Diagnostics)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[CameraPolyBuffer] gather arena restored -> 0x{_origBase:X8} cap {_origCap}");
            }
        }
    }
}
