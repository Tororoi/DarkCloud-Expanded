using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Crystal Gemron (EnemySpecies.CrystalGemron) in play. The species is the disc's: its record (SpeciesRows), model (the
    /// breaking crystal balls, its eyes a node of their own), script and name (CrystalGemronBake). In play this only gives the eyes'
    /// visual its private vtable, so the eyes draw tinted white (<see cref="ArmEyes"/>).</summary>
    internal static class CrystalGemron
    {
        private const string Tag = "[CrystalGemron] ";
        private static uint _eyeVisual;                                                     // the eyes' visual last armed (guest), 0 = none

        /// <summary>Once a dungeon tick; <paramref name="active"/> false while a load is on (the floor's units and pools are rebuilt).</summary>
        internal static void Tick(bool active)
        {
            if (!active) return;
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++)
                if (IsCrystalGemron(unit)) { ArmEyes(unit); return; }                     // the species' one eyes visual
        }

        /// <summary>A new floor: its visual is another object.</summary>
        internal static void Reset() => _eyeVisual = 0;

        /// <summary>The eyes' visual — the eyes frame's, one object every Crystal Gemron on the floor draws — given a private copy of its
        /// class vtable (CodeCaves.EyeTintVtable) whose two DrawVu1 slots enter tools/stubs/eye_tint.s (DeadChainCave.EyeTint), the stock
        /// targets in CodeCaves.EyeTintStock: the cave adds the tint to the ambient for that one draw. Data writes only, the vtable pointer
        /// last; a new floor's visual is armed afresh.</summary>
        private static void ArmEyes(int unit)
        {
            if (_eyeVisual != 0 && Memory.ReadGuestPtr(Memory.ToMmu(_eyeVisual) + CVisualMDT.VisVtable) == CodeCaves.EyeTintVtableGuest) return;
            long chara = Model(unit) - CCharacter.CharScale;
            uint root = Memory.ReadGuestPtr(chara + CCharacter.CharModel);
            uint node = Memory.IsValidGuest(root) ? BombCarrier.NodeNamed(root, CrystalGemronBake.EyeNode) : 0;
            if (node == 0) return;
            uint vis = Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(vis)) return;
            long visM = Memory.ToMmu(vis);
            uint vt = Memory.ReadGuestPtr(visM + CVisualMDT.VisVtable);
            if (vt != CodeCaves.EyeTintVtableGuest)
            {
                if (vt != CVisualMDT.Vu1Vtable && vt != CVisualMDT.RigidVtable)
                {
                    if (_eyeVisual != vis) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the eyes' visual 0x{vis:X} has vtable 0x{vt:X}, neither CVisualMDTVu1 nor CVisualVu1 — no eye tint");
                    _eyeVisual = vis; return;
                }
                byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(vt), CVisualMDT.Vu1VtableBytes);
                if (tbl == null) return;
                Memory.WriteBytesBatch(CodeCaves.EyeTintStock, tbl.AsSpan(CVisualMDT.Vu1VtableDrawSlot, 8).ToArray());   // slot 6, slot 7
                BitConverter.GetBytes(DeadChainCave.EyeTint).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);
                BitConverter.GetBytes(DeadChainCave.EyeTint + 0x0Cu).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);
                Memory.WriteBytesBatch(CodeCaves.EyeTintVtable, tbl);
                Memory.WriteUInt(visM + CVisualMDT.VisVtable, CodeCaves.EyeTintVtableGuest);   // last: the next draw takes it
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"eyes' visual 0x{vis:X} (class vtable 0x{vt:X}) draws through eye_tint (0x{DeadChainCave.EyeTint:X})");
            }
            _eyeVisual = vis;
        }

        private static bool IsCrystalGemron(int unit)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(unit, 0);
            return Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) == EnemySpecies.CrystalGemron.Id;
        }

        private static long Model(int unit) => ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride;
    }
}
