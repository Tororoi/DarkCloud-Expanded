using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The equipped weapon's MODEL, frame by frame: its CFrame tree walked by pointer from *(*NowWeapon+0xBC) — no RAM scan —
    /// for a frame by its four-byte name (<see cref="Locate"/>); the mesh frame a weapon code resolves to (<see cref="ResolveBladeFrame"/>;
    /// the trees are not uniformly shaped); and that frame's local 3×3 scaled by a factor (<see cref="ScaleByName"/> /
    /// <see cref="ScaleBlade"/>: a snapshot MULTIPLIED, so the frame's own rotation and base scale survive and a ramp eases smoothly;
    /// 1.0 restores). <see cref="DumpTree"/> logs the whole tree — how a new weapon's frame names are found. Also the two reads of the
    /// equipped weapon RECORD the Xiao-sphere abilities key their element on (<see cref="EquippedRecord"/>,
    /// <see cref="SelectedElementBits"/>). Used by HeavensCloud, KitchenKnife, Claymore, SolarBlade, WhirlwindScale, MobiusRing,
    /// DivineBeastTitle and the spheres.</summary>
    internal static class WeaponModelFrames
    {
        /// <summary>The equipped weapon record (WEAPON_HAVE) of the active character, or 0 if there isn't one.</summary>
        internal static long EquippedRecord()
        {
            int ch = Player.CurrentCharacterNum();
            if (ch < 0) return 0;
            int slot = Memory.ReadByte(DngStatusData.Base +
                                       DngStatusData.EquipSlotArrayOffset + ch);
            return (uint)slot > 9 ? 0 : DngStatusData.WeaponRecord(ch, slot);
        }

        /// <summary>The element a weapon's hits carry, as the BIT the engine uses on the collision data
        /// (1=Fire, 2=Ice, 4=Thunder, 8=Wind, 0x10=Holy), or 0 for no element.
        ///
        /// The game keeps the SELECTED element index on the record (+0x16), maintained by SetWeaponElementStatus
        /// as "whichever of the five element levels is highest". The catch: that index is 0 (Fire) for a weapon
        /// with NO elements at all, because the scan starts at 0 and only moves on a strict &gt;. So the LEVEL has
        /// to be checked — a zero level means no element, not Fire.</summary>
        internal static int SelectedElementBits(long rec)
        {
            if (rec == 0) return 0;
            int idx = Memory.ReadByte(rec + WeaponHave.SelectedElementOffset);
            if ((uint)idx >= WeaponHave.ElementCount) return 0;
            int level = Memory.ReadByte(rec + WeaponHave.ElementLevelsOffset + idx);
            return level > 0 ? 1 << idx : 0;
        }

        /// <summary>
        /// Resolve the frame that carries a weapon's MESH. Weapon models are NOT uniformly shaped, and the
        /// naming is not uniform either — this was established by dumping both trees:
        ///
        ///   Heaven's Cloud ("c01w14")  root '14_1'  ->  child 'w14'  ->  4x 'dcol'      (mesh on the CHILD)
        ///   Kitchen Knife  ("c01w08")  root 'c01w'  ->  'null' + 3x 'dcol'              (mesh on the ROOT; no 'w08')
        ///   Xiao slingshot ("c04w..")  root 'c04w'                                      (mesh on the ROOT)
        ///
        /// <c>Find</c> matches the first FOUR bytes of the name, so we probe two candidates derived from the
        /// model code and take whichever the model actually has:
        ///   1. the "wNN" suffix (code minus the 3-char character prefix, NUL-padded) — HC's child mesh frame;
        ///   2. the 4-char prefix ("c01w" / "c04w") — the root, for models with no separate mesh child.
        ///
        /// The winner is cached per weapon id. It MUST NOT be re-probed per tick: a failed probe resets the
        /// snapshot in <see cref="ScaleByName"/>, which would then re-snapshot an ALREADY-SCALED
        /// matrix and compound the scale every tick.
        /// </summary>
        internal static uint ResolveBladeFrame(string weaponCode)
        {
            if (string.IsNullOrEmpty(weaponCode) || weaponCode.Length < 4) return 0;

            if (weaponCode.Length > 4)                                   // "c01w14" -> 'w','1','4',NUL
            {
                string s = weaponCode.Substring(3);
                uint suffix = 0;
                for (int i = 0; i < 4 && i < s.Length; i++) suffix |= (uint)s[i] << (i * 8);
                if (suffix != 0 && Locate(suffix, null) != 0) return suffix;
            }

            uint prefix = (uint)(weaponCode[0] | (weaponCode[1] << 8) | (weaponCode[2] << 16) | (weaponCode[3] << 24));
            return Locate(prefix, null) != 0 ? prefix : 0;
        }

        static int  _bladeWeaponId = -1;
        static uint _bladeName;

        /// <summary>Scale an equipped weapon's mesh to <paramref name="factor"/>× — the visible model AND its dcol
        /// hit points grow together. Resolves the mesh frame with <see cref="ResolveBladeFrame"/> and scales it via
        /// <see cref="ScaleByName"/>, which snapshots the frame's local 3x3 and MULTIPLIES it, so the
        /// model's own rotation and base scale survive and a gradual factor eases smoothly.
        ///
        /// Returns false until the frame is located (model not loaded). If it won't resolve,
        /// <see cref="DumpTree"/> shows what the model actually calls its frames.</summary>
        public static bool ScaleBlade(string weaponCode, float factor)
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (wid != _bladeWeaponId || _bladeName == 0)
            {
                _bladeName = ResolveBladeFrame(weaponCode);
                _bladeWeaponId = wid;
                if (_bladeName == 0) return false;      // model not loaded yet — probe again next tick
            }
            return ScaleByName(_bladeName, factor);
        }

        // Walk the equipped weapon model's CFrame tree (POINTER, no scan) for a frame whose name (word@+0x118)
        // == nameWord and, if fifthByte is given, whose 5th name byte matches (to disambiguate dcol0..dcol3).
        // The model root = *(*NowWeapon+0xBC) is a CFrame (name@+0x118, child@+0x138, next@+0x13c — confirmed
        // via CopyFrameVu1/SearchFrame); DFS from it exactly as the game's SearchFrame does. Returns the frame's
        // NAME address (base+0x118, so the +0xB8/0xE8/0xF0 offset conventions hold), or 0 if not resolvable.
        internal static long Locate(uint nameWord, byte? fifthByte)
        {
            int nw = Memory.ReadInt(WeaponModel.NowWeaponPtr);
            if (!IsRamPtr(nw)) return 0;
            int modelRoot = Memory.ReadInt(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            if (!IsRamPtr(modelRoot)) return 0;
            return Find(Memory.ToMmu(modelRoot), nameWord, fifthByte, 0);
        }

        // Recursive DFS over the CFrame tree (child @+0x138, sibling @+0x13c, name @+0x118). Returns the matching
        // frame's NAME address, or 0. Depth-capped so a stray/looping pointer can't recurse forever.
        static long Find(long node, uint nameWord, byte? fifthByte, int depth)
        {
            if (depth > 32) return 0;
            long name = node + CFrameVu1.Name;
            if ((uint)Memory.ReadInt(name) == nameWord &&
                (!fifthByte.HasValue || (byte)Memory.ReadByte(name + 4) == fifthByte.Value))
                return name;
            for (int c = Memory.ReadInt(node + CFrameVu1.RootChild); IsRamPtr(c); )
            {
                long cm = Memory.ToMmu(c);
                long hit = Find(cm, nameWord, fifthByte, depth + 1);
                if (hit != 0) return hit;
                c = Memory.ReadInt(cm + CFrameVu1.RootSibling);
            }
            return 0;
        }

        /// <summary>Re-arm on floor entry so the freshly reloaded weapon model is re-located and re-snapshotted.</summary>
        public static void OnFloorEntered()
        {
            _sfFrameNameAddr = 0; _sfHasSnapshot = false; _sfWeaponId = -1; _sfLastFactor = 1f;   // model moved: re-locate + re-snapshot
            _bladeName = 0; _bladeWeaponId = -1;
        }

        static bool _weaponTreeDumped;

        /// <summary>
        /// DIAGNOSTIC: log the equipped weapon model's whole CFrame tree — each frame's name and its current
        /// scale (local 3x3 m00). This is how you discover a weapon's mesh-frame name, and it is worth having:
        /// the trees are NOT uniformly shaped. Heaven's Cloud hangs its mesh on a 'w14' CHILD under a '14_1'
        /// root; the Kitchen Knife has no 'w08' child at all and carries the mesh on its 'c01w' ROOT. A name
        /// that works for one weapon silently finds nothing on another — dump, do not assume. Dumps once per run.
        /// </summary>
        public static bool DumpTree()
        {
            if (_weaponTreeDumped) return true;
            _weaponTreeDumped = true;

            int nw = Memory.ReadInt(WeaponModel.NowWeaponPtr);
            if (!IsRamPtr(nw))
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[WeaponModelFrames] NowWeapon not resolvable (nw=0x{nw:X})");
                return false;
            }
            int modelRoot = Memory.ReadInt(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            if (!IsRamPtr(modelRoot))
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[WeaponModelFrames] modelRoot not resolvable (nw=0x{nw:X} root=0x{modelRoot:X})");
                return false;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[WeaponModelFrames] === equipped weapon frame tree (nw=0x{nw:X} root=0x{modelRoot:X}) ===");
            DumpRec(Memory.ToMmu(modelRoot), 0);
            return true;
        }

        static void DumpRec(long node, int depth)
        {
            if (depth > 32) return;
            long name = node + CFrameVu1.Name;
            uint nword = (uint)Memory.ReadInt(name);
            string s = "";
            for (int b = 0; b < 4; b++) { char c = (char)((nword >> (b * 8)) & 0xFF); s += (c >= 32 && c < 127) ? c : '.'; }
            Console.WriteLine($"{new string(' ', depth * 2)}[{depth}] '{s}' (0x{nword:X8}) m00={Memory.ReadFloat(name + WeaponModel.Vu1LocalMatrixDiag0):F2}");
            for (int c = Memory.ReadInt(node + CFrameVu1.RootChild); IsRamPtr(c); )
            {
                long cm = Memory.ToMmu(c);
                DumpRec(cm, depth + 1);
                c = Memory.ReadInt(cm + CFrameVu1.RootSibling);
            }
        }

        // Uniform-scale a named model frame by MULTIPLYING its local 3x3 by `factor`. The frame's ORIGINAL matrix is
        // snapshotted the first time it is located and every write is original×factor, so the write is stable across
        // ticks, a gradual factor eases smoothly, the frame's own rotation survives (the slingshot's frames carry one,
        // so SETTING the diagonal would only be right for an identity bind), and factor 1.0 restores. Re-snapshots when
        // the model reloads.
        static long _sfFrameNameAddr;
        static uint _sfNameWord;
        static int  _sfWeaponId = -1;   // two Toan swords share the "c01w" key — re-locate + re-snapshot on a swap
        static readonly float[] _sfOrig = new float[9];
        static bool _sfHasSnapshot;
        static float _sfLastFactor = 1f;   // what the frame currently holds relative to the snapshot (1 = nothing of ours)
        static bool SfIdentity(float f) => Math.Abs(f - 1f) < 1e-4f;

        public static bool ScaleByName(uint nameWord, float factor)
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (_sfFrameNameAddr == 0 || _sfNameWord != nameWord || _sfWeaponId != wid ||
                (uint)Memory.ReadInt(_sfFrameNameAddr) != nameWord)
            {
                _sfFrameNameAddr = Locate(nameWord, null);
                _sfNameWord = nameWord;
                _sfWeaponId = wid;
                _sfHasSnapshot = false;         // never carry a snapshot across models
                if (_sfFrameNameAddr == 0) return false;
            }
            long m = _sfFrameNameAddr + WeaponModel.Vu1LocalMatrixDiag0;   // 3x3 base: row stride 0x10, col stride 4
            var cur = new float[9];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    cur[r * 3 + c] = Memory.ReadFloat(m + r * 0x10 + c * 4);
            // At rest nothing is written: the snapshot just follows whatever the frame holds. A weapon swap rebuilds the model
            // at the same addresses, and this key (name word + weapon id) can survive it while the frame's bind rotation does
            // not — forcing the previous model's snapshot back in left the new slingshot turned a quarter in her hand.
            bool idle = SfIdentity(factor) && SfIdentity(_sfLastFactor);
            if (idle || !_sfHasSnapshot) { Array.Copy(cur, _sfOrig, 9); _sfHasSnapshot = true; }
            if (idle) return true;
            if (!SfIdentity(_sfLastFactor))                                       // mid-scale: the frame must still hold our last write,
            {                                                                     // else the model was rebuilt under us — re-snapshot
                bool ours = true;
                for (int k = 0; k < 9 && ours; k++) ours = Math.Abs(cur[k] - _sfOrig[k] * _sfLastFactor) <= 0.001f;
                if (!ours) Array.Copy(cur, _sfOrig, 9);
            }
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    long addr = m + r * 0x10 + c * 4;
                    float want = _sfOrig[r * 3 + c] * factor;
                    if (Math.Abs(cur[r * 3 + c] - want) > 0.001f) Memory.WriteFloat(addr, want);
                }
            _sfLastFactor = factor;
            return true;
        }

        /// <summary>A guest pointer that lands in EE RAM above the first 0x80000 (where the model objects live).</summary>
        internal static bool IsRamPtr(int p) => (uint)p >= 0x80000 && (uint)p < Memory.EeRamSize;
    }
}
