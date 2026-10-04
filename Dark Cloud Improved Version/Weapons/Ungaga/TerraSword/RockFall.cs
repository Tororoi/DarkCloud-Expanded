using System;
using static Dark_Cloud_Improved_Version.TerraSword;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Terra Sword drop's fall, on the engine's frames: a mode-4 fall of the blade-fall cave (<see cref="StartFall"/>:
    /// vy += g, y −= vy, landed at its stop, the slot over a followed point, the stop from a followed float) and the ISO's fall-drive
    /// cave's drive rows (<see cref="FallRow"/>: clamp(a + b·y) written to a word each frame — the drop's scale, its shadow's, the
    /// target's darkness). The nut's hop off the target's head is armed in the cave ahead of the landing (<see cref="AimHop"/>) and
    /// re-aimed as it falls. One of the <see cref="TerraSword"/> classes, which share their members through using static.</summary>
    internal static class RockFall
    {
        internal static float   _hopGround;                         // the floor where the armed hop comes down
        internal static DateTime _hopAimed;                          // when the armed hop's drift and floor were last worked out
        internal static float   _hopSide;                           // which side of the player's line it hops to (±1, picked at the drop)

        /// <summary>The ISO's fall-drive cave is in place (its first word, `lui t0,0x01FB`).</summary>
        internal static bool CaveLive => Memory.ReadUInt(0x20000000L + DebugIfCave.FallDrive) == 0x3C0801FBu;

        /// <summary>One drive row of the fall-drive cave: clamp(a + b·y, lo, hi) to <paramref name="dst"/> (0 = off), <paramref name="count"/> words.</summary>
        internal static void FallRow(int row, uint dst, int count, float a, float b, float lo, float hi)
        {
            var e = new byte[CodeCaves.FallDriveRowStride];
            BitConverter.GetBytes(dst).CopyTo(e, CodeCaves.FallRowDst);
            BitConverter.GetBytes(Math.Max(1, count)).CopyTo(e, CodeCaves.FallRowCount);
            BitConverter.GetBytes(a).CopyTo(e, CodeCaves.FallRowA);
            BitConverter.GetBytes(b).CopyTo(e, CodeCaves.FallRowB);
            BitConverter.GetBytes(lo).CopyTo(e, CodeCaves.FallRowLo);
            BitConverter.GetBytes(hi).CopyTo(e, CodeCaves.FallRowHi);
            Memory.WriteBytesBatch(CodeCaves.FallDrive + CodeCaves.FallDriveRows + row * CodeCaves.FallDriveRowStride, e);
        }

        internal static void FallRowsOff() { for (int r = 0; r < CodeCaves.FallDriveRowCount; r++) Memory.WriteUInt(CodeCaves.FallDrive + CodeCaves.FallDriveRows + r * CodeCaves.FallDriveRowStride, 0); }

        /// <summary>A mode-4 fall: its fields first, the flag last.</summary>
        internal static void StartFall(float y, float vy, float g, float stop, uint follow, float offX, float offZ, uint stopSrc, float stopOff)
        {
            Memory.WriteInt  (CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY, y);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveVy, vy);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveG, g);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveStop, stop);
            Memory.WriteUInt (CodeCaves.VerticalDrive + CodeCaves.VerticalDriveUnit, follow);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffX, offX);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffZ, offZ);
            Memory.WriteUInt (CodeCaves.FallDrive + CodeCaves.FallDriveStopSrc, stopSrc);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveStopOff, stopOff);
            Memory.WriteInt  (CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.DriveFallFollowing);
        }

        /// <summary>The nut's ARMED HOP (the cave's, taken the frame it lands on the head): up BonkUp, drifting BonkSide square to the
        /// player's line to the target (the side picked at the drop), down onto the floor where that comes out (DungeonFloor there,
        /// reckoned under BonkGravity, which Bonk sets as the fall's g once it sees the hop).</summary>
        internal static void AimHop()
        {
            _hopAimed = GameClock.Now;
            long up = EnemyAddresses.CharObjects.PosAddr(_target);
            float x0 = Memory.ReadFloat(up), z0 = Memory.ReadFloat(up + 8), h0 = Memory.ReadFloat(up + 4) + EnemyBody.HeadHeight(_target) + Radius;
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            float lx = x0 - px, ly = z0 - py, ll = (float)Math.Sqrt(lx * lx + ly * ly);
            if (ll < 1e-3f) { lx = 1f; ly = 0f; ll = 1f; }
            float vx = -ly / ll * BonkSide * _hopSide, vy = lx / ll * BonkSide * _hopSide;                // units/s, square to the line
            float ground = _ground + Radius - SinkNow;
            float t1 = (BonkUp + (float)Math.Sqrt(Math.Max(0f, BonkUp * BonkUp + 2f * BonkGravity * (h0 - ground)))) / BonkGravity;
            _hopGround = DungeonFloor.HeightAt(x0 + vx * t1, z0 + vy * t1, h0 + 20f, out float fh) ? fh : _ground;
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopVy, -BonkUp / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopDx, vx / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopDz, vy / 60f);
            Memory.WriteFloat(CodeCaves.FallDrive + CodeCaves.FallDriveHopStop, _hopGround + Radius - SinkNow);
            Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopArmed, 1);
        }
    }
}
