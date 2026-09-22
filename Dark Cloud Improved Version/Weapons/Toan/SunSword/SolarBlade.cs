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
    /// pointer to it is in the weapon's visual. (An earlier version also exposed the blade's own palette at full charge; the
    /// glow reads the charge clearly enough on its own, so that step is gone.)
    /// </summary>
    internal static class SolarBlade
    {
        // The weapon whose blade is being tinted, as a model code (ToanWeapons.Code): the Sun Sword's c01w10 for Solar
        // Flash, Big Bang's c01w18 for Detonate. Set by the caller before the first Set of a charge; Arm re-resolves the
        // blade frame whenever it changes, so switching weapons cannot leave the previous blade's visual armed.
        internal const string SunSwordModel = "c01w10", BigBangModel = "c01w18";
        /// <summary>Big Bang's lit blade mesh, named explicitly because its model does NOT follow the usual
        /// root "NN_1_1" → mesh "wNN" shape that <see cref="Weapons.ResolveBladeFrame"/> derives. Its tree is
        /// root `18_1_1` → `w18a` (lit; the parent of all four dcol frames) → `w18b__c`. Neither the derived
        /// "w18" nor the "c01w" fallback matches anything there, so deriving silently found no frame and the
        /// tint never wrote. ⚠ `w18b__c` is deliberately NOT tinted: its `__c` suffix is SetFrameAttr's
        /// UNLIT-constant-colour flag (light matrix zeroed, colour floats 128), so an ambient ADD cannot reach
        /// it — <see cref="BigBangGlowFrame"/> takes the second lever below instead.</summary>
        internal const uint   BigBangBladeFrame = 0x61383177;   // 'w','1','8','a' — the HANDLE (lit; parents the dcol frames)
        /// <summary>Big Bang's BLADE, `w18b__c`. Its `__c` suffix means SetFrameAttr drew it unlit at a constant
        /// colour with the light matrix zeroed, so the ambient cave cannot touch it; it is brightened by writing
        /// that constant colour directly (<see cref="CFrameVu1.UnlitColourR"/>), snapshotted on first use and
        /// handed back on <see cref="Clear"/>.</summary>
        internal const uint   BigBangGlowFrame  = 0x62383177;   // 'w','1','8','b'
        private static string ModelCode  = SunSwordModel;
        private static uint   FrameWord;                        // 0 = derive from ModelCode (the Sun Sword's root matches "c01w")
        private static uint   UnlitWord;                        // 0 = no unlit mesh on this weapon
        private const float  WhiteMax    = 200f;        // the ambient add at full charge, per channel on the 0-255 scale
        private const float  UnlitMax    = 255f;        // what the unlit mesh's constant colour reaches at full charge
        private static long   _unlitNode;
        private static int    _unlitWeaponId = -1;
        private static bool   _unlitArmed;
        private static readonly float[] _unlitOrig = new float[3];
        // The PEAK step: the blade's 256-entry T8 palette is EXPOSED, not repainted — every entry is multiplied by

        private static long  _visual;                   // the blade mesh's visual (MMU), while armed
        private static uint  _stockVtable = CVisualMDT.RigidVtable;   // the class vtable the visual held before ours
        private static int   _weaponId = -1;
        private static bool  _armed, _warned;
        private static float _last = -1f;

        /// <summary>The blade's whiteness, 0 (its own colour) to 1 (full). Arms the private vtable on first use and again
        /// whenever the weapon model was rebuilt under it.</summary>
        internal static void Set(float k, string modelCode = SunSwordModel, uint frameWord = 0, uint unlitWord = 0)
        {
            k = Math.Clamp(k, 0f, 1f);
            if (modelCode != ModelCode || frameWord != FrameWord || unlitWord != UnlitWord)
            {
                Clear();                                                      // a different blade: drop the old visual's vtable first
                ModelCode = modelCode; FrameWord = frameWord; UnlitWord = unlitWord; _warned = false;
            }
            Unlit(k);                                                         // its own frame and its own lever — independent of the cave
            if (!Arm()) return;
            float v = k * WhiteMax;
            if (Math.Abs(v - _last) < 0.5f) return;
            Memory.WriteVec3(CodeCaves.Mailbox.CatCapeTint, v, v, v);
            _last = v;
        }

        /// <summary>Back to the blade's own colour, and its visual back on the engine's vtable.</summary>
        internal static void Clear()
        {
            if (_unlitArmed)
            {
                for (int i = 0; i < 3; i++) Memory.WriteFloat(_unlitNode + CFrameVu1.UnlitColourR + i * 4, _unlitOrig[i]);
                _unlitArmed = false; _unlitWeaponId = -1;
            }
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

            // An explicitly named frame wins; otherwise derive it from the model code, which only works for a model
            // whose mesh frame is its root or a plain "wNN" child. A failure dumps the tree rather than going quiet:
            // the frame names are NOT guessable per weapon, which is exactly how the Big Bang tint failed silently.
            uint nameWord = FrameWord != 0 ? FrameWord : Weapons.ResolveBladeFrame(ModelCode);
            if (nameWord == 0)
            { WarnOnce($"no blade frame derived for '{ModelCode}' — dumping the model's frame tree:"); Weapons.DumpWeaponFrameTree(); return false; }
            long nameAddr = Weapons.LocateModelFrame(nameWord, null);
            if (nameAddr == 0)
            { WarnOnce($"blade frame 0x{nameWord:X8} of '{ModelCode}' not in the model — dumping the model's frame tree:"); Weapons.DumpWeaponFrameTree(); return false; }
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
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BladeTint] {ModelCode} blade visual 0x{vis:X} (class vtable 0x{vt:X}) draws through its own vtable (0x{CodeCaves.SolarBladeVtableGuest:X}) → cave 0x{entry:X}");
            }
            _visual = visM; _weaponId = wid; _armed = true;
            return true;
        }

        /// <summary>The unlit mesh's whiteness: its constant colour ramped from whatever it was baked with toward
        /// <see cref="UnlitMax"/>. Snapshots the original the first time it locates the frame, and re-locates when
        /// the weapon model is rebuilt under it, so the restore always hands back that model's own colour.</summary>
        private static void Unlit(float k)
        {
            if (UnlitWord == 0) return;
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (!_unlitArmed || wid != _unlitWeaponId)
            {
                long nameAddr = Weapons.LocateModelFrame(UnlitWord, null);
                if (nameAddr == 0) { WarnOnce($"unlit mesh 0x{UnlitWord:X8} of '{ModelCode}' not in the model — the blade keeps its own colour"); return; }
                _unlitNode = nameAddr - CFrameVu1.Name;
                for (int i = 0; i < 3; i++) _unlitOrig[i] = Memory.ReadFloat(_unlitNode + CFrameVu1.UnlitColourR + i * 4);
                _unlitArmed = true; _unlitWeaponId = wid;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BladeTint] {ModelCode} unlit mesh 0x{UnlitWord:X8} at 0x{_unlitNode:X}: constant colour ({_unlitOrig[0]:F0},{_unlitOrig[1]:F0},{_unlitOrig[2]:F0})");
            }
            for (int i = 0; i < 3; i++)
                Memory.WriteFloat(_unlitNode + CFrameVu1.UnlitColourR + i * 4, _unlitOrig[i] + (UnlitMax - _unlitOrig[i]) * k);
        }

        private static void WarnOnce(string what)
        {
            if (_warned) return;
            _warned = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BladeTint] " + what);
        }
    }
}
