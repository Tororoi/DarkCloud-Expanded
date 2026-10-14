using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    // ── The curse machinery Evilcise (Jealous Soul) and Maneater (Blood Price) share — also driven by Super Steve for Xiao ──
    /// <summary>The per-character status/HP addresses a curse effect writes. The status BITS are character-independent (see
    /// ToanState); only these addresses differ between Toan and, e.g., Xiao. A curse needs no timer: CUserStatus::ailment_frames
    /// counts down only while Freeze or Stamina is set.</summary>
    internal readonly struct CurseAddrs
    {
        public readonly int Status, Hp;
        public CurseAddrs(int status, int hp) { Status = status; Hp = hp; }
    }

    /// <summary>Per-wielder curse state (no shared statics), so the Toan thread and the Super
    /// Steve thread never trample each other's tracking.</summary>
    internal sealed class CurseState
    {
        public bool Resolved;       // Evilcise: penalized · Maneater: cleansed (no penalty)
        public bool WasNearDeath;
        public byte LastFloor = 0xFF;
        public bool Applied;        // curse currently owned by this driver (for a clean strip)
        public DateTime LastDrain = DateTime.MinValue;
    }
}
