using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Her pellets kept out of sight while the sphere is on: every pellet is replaced (a bomb) or retired (the drop's, the
    /// flash's) the tick it appears, a frame or two after the engine drew it, so Mailbox.PelletSpriteId is held at the pellet sheet's
    /// transparent cell (<see cref="Hide"/>; BigBangShot re-asserts it over another writer) and the sprite id in force before comes
    /// back when the sphere goes (<see cref="Unhide"/>). One of the <see cref="BigBangShot"/> classes, which share their members
    /// through using static.</summary>
    internal static class PelletHide
    {
        internal static bool _hiding;                                  // pellets drawn from the blank cell while the sphere is on: no pellet of hers is ever meant to be seen
        private static int   _spriteBefore;                            // Mailbox.PelletSpriteId as the hide found it, put back after

        /// <summary>Every pellet drawn from the sheet's transparent cell, until <see cref="Unhide"/>.</summary>
        internal static void Hide()
        {
            _spriteBefore = Memory.ReadInt(Mailbox.PelletSpriteId);
            if (_spriteBefore == PelletSheetBakes.BlankCell) _spriteBefore = 0;
            Memory.WriteInt(Mailbox.PelletSpriteId, PelletSheetBakes.BlankCell);
            _hiding = true;
        }
        internal static void Unhide()
        {
            if (!_hiding) return;
            if (Memory.ReadInt(Mailbox.PelletSpriteId) == PelletSheetBakes.BlankCell)   // still ours (nothing else wrote it meanwhile)
                Memory.WriteInt(Mailbox.PelletSpriteId, _spriteBefore);
            _hiding = false;
        }
    }
}
