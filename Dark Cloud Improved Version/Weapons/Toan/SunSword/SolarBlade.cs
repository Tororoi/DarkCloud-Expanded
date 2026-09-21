using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Sun Sword's blade, tinted toward white while Solar Flash charges (SunSword.SolarFlashEffect). The sword is a
    /// frame of Toan's own tree (EquipWeaponFrame), so it draws inside Draw__10CCharacter under HIS ambient — a whole-body
    /// tint would whiten Toan too. A mesh reaches the screen through the virtual DrawVu1 slots of its CVisualMDT, so the
    /// blade's visual (the model's private object: rebuilt with every weapon swap, shared with nothing) is given a private
    /// copy of its class vtable whose DrawVu1 slots point at ElfCave.SolarBladeTint — two entries into the Divine Beast cat's
    /// mask-tint cave body, which adds Mailbox.CatCapeTint to the ambient for that one draw. A weapon mesh is a rigid
    /// CVisualVu1 (vtable 0x2A11C0), which is why the mask cave's own entries (the skinned class's DrawVu1) cannot serve it.
    /// Toan's sword and Xiao's cat are never live at once, so the cave body and its tint word are the blade's while the Sun
    /// Sword is in hand. Nothing of the engine's is written: the vtable copy sits in CodeCaves.SolarBladeVtable and the only
    /// pointer to it is in the weapon's visual.
    /// </summary>
    internal static class SolarBlade
    {
        private const string ModelCode = "c01w10";      // the Sun Sword's weapon model (ToanWeapons: id 267 → commenu/weapon/c01w10.chr)
        private const float  WhiteMax  = 200f;          // the ambient add at full charge, per channel on the 0-255 scale

        private static long  _visual;                   // the blade mesh's visual (MMU), while armed
        private static uint  _stockVtable = CVisualMDT.RigidVtable;   // the class vtable the visual held before ours
        private static int   _weaponId = -1;
        private static bool  _armed, _warned;
        private static float _last = -1f;

        /// <summary>The blade's whiteness, 0 (its own colour) to 1 (full). Arms the private vtable on first use and again
        /// whenever the weapon model was rebuilt under it.</summary>
        internal static void Set(float k)
        {
            k = Math.Clamp(k, 0f, 1f);
            if (!Arm()) return;
            float v = k * WhiteMax;
            if (Math.Abs(v - _last) < 0.5f) return;
            Memory.WriteVec3(CodeCaves.Mailbox.CatCapeTint, v, v, v);
            _last = v;
        }

        /// <summary>Back to the blade's own colour, and its visual back on the engine's vtable.</summary>
        internal static void Clear()
        {
            if (_armed)
            {
                Memory.WriteVec3(CodeCaves.Mailbox.CatCapeTint, 0f, 0f, 0f);
                if (Memory.ReadGuestPtr(_visual + CVisualMDT.VisVtable) == CodeCaves.SolarBladeVtableGuest)
                    Memory.WriteUInt(_visual + CVisualMDT.VisVtable, _stockVtable);
                _armed = false;
            }
            _last = -1f;
        }

        private static bool Arm()
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (_armed && wid == _weaponId && Memory.ReadGuestPtr(_visual + CVisualMDT.VisVtable) == CodeCaves.SolarBladeVtableGuest)
                return true;                                                     // still our visual, still our vtable
            _armed = false; _last = -1f;

            uint nameWord = Weapons.ResolveBladeFrame(ModelCode);                // the blade mesh frame "w10"
            if (nameWord == 0) return false;                                     // model not loaded yet
            long nameAddr = Weapons.LocateModelFrame(nameWord, null);
            if (nameAddr == 0) return false;
            long node = nameAddr - CFrameVu1.Name;                               // the template name sits at node + 0x118
            uint vis = Memory.ReadGuestPtr(node + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(vis)) { WarnOnce("the blade frame has no visual"); return false; }
            long visM = Memory.ToMmu(vis);
            uint vt = Memory.ReadGuestPtr(visM + CVisualMDT.VisVtable);
            if (vt != CodeCaves.SolarBladeVtableGuest)
            {
                // The entries that enter the tint cave for this visual's class: a weapon mesh is a rigid CVisualVu1 (ElfCave.SolarBladeTint);
                // the skinned CVisualMDTVu1 would take the mask cave's own entries.
                uint entry = vt == CVisualMDT.RigidVtable ? CodeCaves.ElfCave.SolarBladeTint
                           : vt == CVisualMDT.Vu1Vtable   ? CodeCaves.ElfCave.CatMaskTint : 0;
                if (entry == 0) { WarnOnce($"the blade visual's vtable is 0x{vt:X}, neither CVisualVu1 nor CVisualMDTVu1 — no blade tint"); return false; }
                byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(vt), CVisualMDT.Vu1VtableBytes);
                if (tbl == null) return false;
                BitConverter.GetBytes(entry).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);           // the uint* overload
                BitConverter.GetBytes(entry + 0x0Cu).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);  // the packet overload
                Memory.WriteBytesBatch(CodeCaves.SolarBladeVtable, tbl);
                Memory.WriteUInt(visM + CVisualMDT.VisVtable, CodeCaves.SolarBladeVtableGuest);
                _stockVtable = vt;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blade visual 0x{vis:X} (class vtable 0x{vt:X}) draws through its own vtable (0x{CodeCaves.SolarBladeVtableGuest:X}) → cave 0x{entry:X}");
            }
            _visual = visM; _weaponId = wid; _armed = true;
            return true;
        }

        private static void WarnOnce(string what)
        {
            if (_warned) return;
            _warned = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SunSword] " + what);
        }
    }
}
