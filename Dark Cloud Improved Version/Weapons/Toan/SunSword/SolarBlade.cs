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
    /// pointer to it is in the weapon's visual. At full charge <see cref="PaintPeak"/> adds a second, sudden step: the blade's
    /// OWN palette is exposed — every entry multiplied, the brightest washing to white — so the metal turns rather than only
    /// the light on it, and its shading survives the change.
    /// </summary>
    internal static class SolarBlade
    {
        private const string ModelCode   = "c01w10";    // the Sun Sword's weapon model (ToanWeapons: id 267 → commenu/weapon/c01w10.chr)
        private const string TextureName = "c01w10";    // its dungeon texture (dun/item/main_wep/c01w10.img: one 256×256 T8 TIM2 of that name)
        private const float  WhiteMax    = 200f;        // the ambient add at full charge, per channel on the 0-255 scale
        // The PEAK step: the blade's 256-entry T8 palette is EXPOSED, not repainted — every entry is multiplied by
        // PeakGain, and an entry bright enough after that gain washes toward white by up to PeakWash. Multiplying keeps the
        // ratios between entries, so the blade's shading survives and only its brightest gold blows out; selecting "the gold
        // ones" and pushing each the same distance instead drove dark golds as far as bright ones, which flattened the
        // shading into one cream with hard edges at the selection boundary.
        private const float  PeakGain    = 1.60f;       // exposure at the peak (1 = untouched)
        private const float  PeakWash    = 0.50f;       // how far a fully blown-out entry goes to white
        private const float  WashFrom    = 150f;        // exposed luminance where the wash starts
        private const int    ClutEntries = 256, ClutBytes = ClutEntries * 4;

        private static long  _visual;                   // the blade mesh's visual (MMU), while armed
        private static byte[] _palOrig;                 // the texture's own CLUT, while the peak palette is painted over it
        private static long  _palAt;                    // …and where it lives (MMU)
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

        /// <summary>The peak step: the blade's gold palette entries jump most of the way to white (<paramref name="on"/>), or
        /// the texture's own palette comes back. The manager holds its own copy of the CLUT and the draw re-uploads it, so this
        /// is a plain data write — the same live-recolour path the cat's cape uses.</summary>
        internal static void PaintPeak(bool on)
        {
            if (on == (_palOrig != null)) return;
            if (!on)
            {
                if (_palOrig != null && _palAt != 0) Memory.WriteByteArray(_palAt, _palOrig);
                _palOrig = null; _palAt = 0;
                return;
            }
            long entry = FindTexEntry(TextureName);
            if (entry == 0) { WarnOnce($"texture \"{TextureName}\" is not in the manager — the blade keeps its own palette"); return; }
            uint clut = Memory.ReadGuestPtr(entry + TextureManager.EntryClut);
            if (!Memory.IsValidGuest(clut)) { WarnOnce("the blade texture has no palette — it keeps its own colours"); return; }
            long at = Memory.ToMmu(clut);
            byte[] pal = Memory.ReadBytesBatch(at, ClutBytes);
            if (pal == null) return;
            var peak = (byte[])pal.Clone();
            for (int i = 0; i < ClutEntries; i++)
            {
                float r = pal[i * 4], g = pal[i * 4 + 1], b = pal[i * 4 + 2];
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                r *= PeakGain; g *= PeakGain; b *= PeakGain;
                float t = Math.Clamp((lum * PeakGain - WashFrom) / (255f - WashFrom), 0f, 1f) * PeakWash;
                r += (255f - r) * t; g += (255f - g) * t; b += (255f - b) * t;
                peak[i * 4]     = (byte)Math.Min(255f, MathF.Round(r));
                peak[i * 4 + 1] = (byte)Math.Min(255f, MathF.Round(g));
                peak[i * 4 + 2] = (byte)Math.Min(255f, MathF.Round(b));
            }
            Memory.WriteByteArray(at, peak);
            _palOrig = pal; _palAt = at;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blade palette exposed ×{PeakGain:0.00}, highlights washed {PeakWash * 100:F0}% to white");
        }

        /// <summary>The texture manager's entry of that name, or 0.</summary>
        private static long FindTexEntry(string name)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base));
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                byte[] nb = Memory.ReadBytesBatch(e + TextureManager.EntryName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(nb, 0, len) == name) return e;
            }
            return 0;
        }

        /// <summary>Back to the blade's own colour, and its visual back on the engine's vtable.</summary>
        internal static void Clear()
        {
            PaintPeak(false);
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
