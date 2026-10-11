using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>One model node's visual drawn through a cave: the node's visual (one object every unit of the species on the floor draws)
    /// gets a private copy of its class vtable whose two DrawVu1 slots (6, the uint* overload; 7, the packet one) enter the cave at
    /// +0 and +slot7, the stock targets saved for the cave to call. Data writes only, the vtable pointer last; a new floor's visual is armed
    /// afresh (<see cref="Reset"/>). Used by the Bomb Gemron's fuse tint (bomb_tint.s), the Crystal Gemron's eye tint and the Atla
    /// Gemron's Atlamillia tint (eye_tint.s).</summary>
    internal sealed class NodeDrawHook
    {
        private const string Tag = "[NodeDrawHook] ";
        private readonly string _what, _cave;
        private readonly uint _entry, _slot7, _vtableGuest;
        private readonly long _stock, _vtable;
        private uint _visual;                                                               // the visual last armed (guest), 0 = none

        /// <param name="what">The node, for the log ("the Bomb Gemron's big bomb").</param>
        /// <param name="cave">The cave's name, for the log.</param>
        /// <param name="entry">The cave (guest): slot 6 enters at +0, slot 7 at +0xC.</param>
        /// <param name="stock">Where the two stock targets go (8 B), for the cave to call.</param>
        /// <param name="vtable">The private vtable (CVisualMDT.Vu1VtableBytes).</param>
        /// <param name="vtableGuest">The private vtable's guest address.</param>
        /// <param name="slot7">Where slot 7 enters the cave, from <paramref name="entry"/>.</param>
        internal NodeDrawHook(string what, string cave, uint entry, long stock, long vtable, uint vtableGuest, uint slot7 = 0x0C)
        {
            _what = what; _cave = cave; _entry = entry; _stock = stock; _vtable = vtable; _vtableGuest = vtableGuest; _slot7 = slot7;
        }

        /// <summary>A new floor: its visual is another object.</summary>
        internal void Reset() => _visual = 0;

        /// <summary>Arms the node named <paramref name="node"/> of <paramref name="unit"/>'s model (once per visual).</summary>
        internal void Arm(int unit, string node)
        {
            if (_visual != 0 && Memory.ReadGuestPtr(Memory.ToMmu(_visual) + CVisualMDT.VisVtable) == _vtableGuest) return;
            long chara = ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride - CCharacter.CharScale;
            uint root = Memory.ReadGuestPtr(chara + CCharacter.CharModel);
            uint frame = Memory.IsValidGuest(root) ? BombCarrier.NodeNamed(root, node) : 0;
            if (frame == 0) return;
            uint vis = Memory.ReadGuestPtr(Memory.ToMmu(frame) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(vis)) return;
            long visM = Memory.ToMmu(vis);
            uint vt = Memory.ReadGuestPtr(visM + CVisualMDT.VisVtable);
            if (vt != _vtableGuest)
            {
                if (vt != CVisualMDT.Vu1Vtable && vt != CVisualMDT.RigidVtable)
                {
                    if (_visual != vis) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_what}'s visual 0x{vis:X} has vtable 0x{vt:X}, neither CVisualMDTVu1 nor CVisualVu1 — not drawn through {_cave}");
                    _visual = vis; return;
                }
                byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(vt), CVisualMDT.Vu1VtableBytes);
                if (tbl == null) return;
                Memory.WriteBytesBatch(_stock, tbl.AsSpan(CVisualMDT.Vu1VtableDrawSlot, 8).ToArray());   // slot 6, slot 7
                BitConverter.GetBytes(_entry).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);
                BitConverter.GetBytes(_entry + _slot7).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);
                Memory.WriteBytesBatch(_vtable, tbl);
                Memory.WriteUInt(visM + CVisualMDT.VisVtable, _vtableGuest);                   // last: the next draw takes it
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_what}'s visual 0x{vis:X} (class vtable 0x{vt:X}) draws through {_cave} (0x{_entry:X})");
            }
            _visual = vis;
        }
    }
}
