using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The whirlwind visual (c01_fuusya, the main-character effect) sized to the equipped weapon's reach, for every Toan
    /// weapon: VisualScale = reach / <see cref="VisualRadius"/>, reach = the weapon's dcol1 Z (the static ToanWeapons table, else a
    /// live tree-walk of its model) × its blade factor (the Claymore is swung scaled up), re-applied to every "kiru" root of the
    /// effect pool while Toan is walking a floor — each cast re-poses its root, so the scale is maintained, not set once. A
    /// background thread does it (<see cref="Start"/>, 150 ms). Heaven's Cloud drives the same scale itself through its charge
    /// (<see cref="SizeTo"/> from HeavensCloud.ScaleWhirl), so the tick stands aside while it is equipped; it also stands down
    /// while Big Bang / Sword of Zeus have replaced the fuusya model in the instance (their roots do not validate as "kiru").</summary>
    internal static class WhirlwindScale
    {
        // Whirl visual scale = D / VisualRadius, where D = the weapon's dcol1 reach (dcol1 Z × mesh scale) and VisualRadius is
        // the fuusya disc's default radius (its cyl1__cappz X/Z scale). It's the single eyeball-calibration knob — the base
        // cylinder's unit radius isn't readable from the VIF1-packed verts, so nudge it until the visual edge meets the hit.
        internal const float VisualRadius = 8.0f; // Default 7.467f
        static float VisualScale;                                // 0 until located
        static float _weaponDcol1Z;                              // equipped weapon's dcol1 Z (0 = unknown)
        static int   _weaponId = -1;                             // weapon id VisualScale was computed for
        static int   _dcolBackoff;
        static int   _locateBackoff;
        static long[] _roots = Array.Empty<long>();              // MMU bases of the fuusya "kiru" roots we scale
        static readonly float[] _bind3x3 = new float[9];         // root's bind local-matrix 3x3 (target = bind × scale)
        static bool _bindRead;
        static bool _started;

        /// <summary>Starts the background thread (idempotent).</summary>
        public static void Start()
        {
            if (_started) return;
            _started = true;
            new Thread(Loop) { IsBackground = true }.Start();
        }

        static void Loop()
        {
            while (true)
            {
                try { Tick(); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WhirlwindScale] " + ex.Message); }
                Thread.Sleep(150);
            }
        }

        static void Tick()
        {
            // Whirlwind VISUAL scale applies to ALL of Toan's weapons (the fuusya effect is character-, not
            // weapon-bound), sized to each weapon's own dcol1 reach — EXCEPT Heaven's Cloud, whose blade/whirl/
            // hitbox are driven per-charge by HeavensCloud.TyphoonEffect (which owns the shared whirl state
            // while HC is equipped, so this loop must not also write it). Recompute on weapon swap.
            if (Player.CurrentCharacterNum() != Player.ToanId) return;
            int wid = EquippedWeaponId();
            if (wid == Items.heavenscloud) return;   // HC → TyphoonEffect
            // Big Bang REPLACES the whirl model with explosion.chr (BigBang.WantedShot). This path validates a root
            // by the fuusya names and would only drop its cache every tick against that model, so it stands down and
            // BigBang.MaintainExplosionScale holds the scale instead.
            if (BigBang.ExplosionSeeded || SwordOfZeus.LightningSeeded) { _roots = Array.Empty<long>(); return; }

            if (wid != _weaponId)
            {
                _weaponId = wid; _weaponDcol1Z = 0f; VisualScale = 0f; _dcolBackoff = 0;
                // The fuusya pool relocates when a new weapon model loads — drop the cached root(s) and clear the
                // locate backoff so the first walking tick re-finds and re-scales it with the NEW scale.
                _roots = Array.Empty<long>(); _locateBackoff = 0;
                // Static table first (offline-extracted dcol1; abs guards mirrored models). Falls back to a live
                // tree-walk only for ids not in the table or with no dcol1 frame (e.g. id 277).
                if (ToanWeapons.TryGetValue(wid, out WeaponData wd) && wd.Dcol1.HasValue)
                {
                    _weaponDcol1Z = Math.Abs(wd.Dcol1.Value) * BladeFactor(wid);
                    VisualScale = _weaponDcol1Z / VisualRadius;
                }
            }
            if (_weaponDcol1Z == 0f) { if (_dcolBackoff <= 0) LocateWeaponDcol1(); else _dcolBackoff--; }
            if (VisualScale > 0f) Maintain();
        }

        /// <summary>The whirl sized to a blade reach of <paramref name="reach"/> units and re-applied to the fuusya roots now
        /// (Heaven's Cloud, per charge tick).</summary>
        internal static void SizeTo(float reach)
        {
            VisualScale = reach / VisualRadius;
            Maintain();
        }

        /// <summary>Forget which weapon the scale was computed for, so the next tick recomputes it for whatever is equipped
        /// (Heaven's Cloud, when its charge-driven scale lets go).</summary>
        internal static void ForgetWeapon() => _weaponId = -1;

        /// <summary>Re-arm on floor entry so the freshly reloaded fuusya pool is re-located.</summary>
        public static void OnFloorEntered()
        {
            _roots = Array.Empty<long>(); _locateBackoff = 0;
        }

        // Locate / maintain the whirlwind effect (c01_fuusya). Only the root "kiru" matrix transforms the
        // VERTEX_ANIME mesh, so we scale the root's local-matrix 3x3. The effect is a pool of concurrent
        // instances (LocateRoots derefs all of them via pointer); we keep every pool root scaled so
        // whichever a cast activates is already correct. Each cast re-poses its root once; the maintain re-applies.
        static void Maintain()
        {
            // Only touch the effect during active in-field gameplay. In the weapon menu / floor transitions
            // the game reallocates models, and writing the fuusya structure then can corrupt the reload and
            // crash the emulator (observed on weapon switch). Resume when walking again.
            if (!Player.CheckDunIsWalkingMode()) return;

            if (_roots.Length > 0)
            {
                // Drop the cache if the first root's name vanished (model freed/relocated); else re-apply.
                if (Memory.ReadUInt(_roots[0] + CFrameVu1.Name) != ShotEffectPool.KiruNameWord)
                    { _roots = Array.Empty<long>(); return; }
                // Keep EVERY pool instance scaled (don't narrow to one): a cast right after a swap can activate a
                // different instance than the previously-live one, so narrowing would leave that first cast at the
                // wrong scale. Re-applying to all idle copies is harmless (they're at world 0,0,0 until used).
                float target0 = _bind3x3[0] * VisualScale;
                foreach (long root in _roots)
                    if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - target0) > 0.01f) WriteScaled(root);
            }
            else if (_locateBackoff <= 0) LocateRoots();
            else _locateBackoff--;
        }

        // Scale a fuusya root's local-matrix 3x3 (base+0x1d0) by bind*VisualScale (the only matrix that
        // transforms the morph mesh), leaving the translation row (+0x200) anchored. Force a recompute.
        static void WriteScaled(long root)
        {
            for (int i = 0; i < 9; i++)
                Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[i], _bind3x3[i] * VisualScale);
            Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
        }

        // True if `root` is a fuusya "kiru" instance (next frame +0x270 is "fkiri"). Caches its bind 3x3 once.
        static bool ValidateRoot(long root)
        {
            if (Memory.ReadUInt(root + CFrameVu1.Name) != ShotEffectPool.KiruNameWord) return false;
            if (Memory.ReadUInt(root + ShotEffectPool.FuusyaFrameStride + CFrameVu1.Name) != ShotEffectPool.FkiriNameWord) return false;
            if (!_bindRead)
            {
                for (int k = 0; k < 9; k++) _bind3x3[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                if (Math.Abs(_bind3x3[0]) < 0.05f || Math.Abs(_bind3x3[0]) > 4.0f) return false; // already scaled / bad read
                _bindRead = true;
            }
            return true;
        }

        /// <summary>Equipped weapon id from the inventory equip slot (updates IMMEDIATELY on a weapon swap, unlike the
        /// battle-block id GetCurrentWeaponId/0x21EA7590 which only refreshes once Toan is walking again — so keying the
        /// reach scheme off this removes the post-swap lag). Toan's weapon list, so callers gate on ToanId. Returns -1
        /// when no valid slot.</summary>
        internal static int EquippedWeaponId()
        {
            int slot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
            if ((uint)slot > 9) return -1;
            return Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id + slot * WeaponHave.InventoryWeaponSlotStride);
        }

        // Fallback for weapons not in ToanWeapons (or with no static dcol1): read the EQUIPPED weapon's dcol1 Z
        // live by tree-walking its model to the "dcol1" frame (WeaponModelFrames.Locate — POINTER, no scan), and size
        // the whirl from it. The dcol1 frame's local translation is (0,0,Z) at name+0xE8/+0xEC/+0xF0; Z = reach.
        static void LocateWeaponDcol1()
        {
            long name = WeaponModelFrames.Locate(WeaponModel.DcolNameWord, WeaponModel.Dcol1Digit);
            if (name == 0) { _dcolBackoff = 8; return; }        // model / dcol1 not ready yet
            float x = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalX);
            float y = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalX + 4);
            float z = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalZ);
            if (Math.Abs(x) > 0.5f || Math.Abs(y) > 0.5f || z < 0.1f || z > 40f) { _dcolBackoff = 8; return; } // sane (0,0,Z)
            _weaponDcol1Z = z * BladeFactor(EquippedWeaponId());
            VisualScale = _weaponDcol1Z / VisualRadius;
        }

        /// <summary>How much longer than its model a weapon's blade is in Toan's hand (the Claymore is scaled up), so the whirl
        /// visual is sized to the blade as swung.</summary>
        static float BladeFactor(int wid) => wid == Items.claymore ? Claymore.BladeScale : 1f;

        // Locate the fuusya effect root(s) via a direct pointer (no RAM scan): the main-character effect object
        // is a fixed global (MainCharaEffectBase) with EffectSlotCount pool slots; each slot's model object
        // holds its root CFrame ("kiru") pointer at a fixed offset, so we deref straight to the roots and scale
        // every valid one. Backs off ~1s if the effect isn't loaded yet. Caches the bind 3x3.
        static void LocateRoots()
        {
            var roots = new System.Collections.Generic.List<long>();
            for (int s = 0; s < ShotEffectPool.EffectSlotCount; s++)
            {
                int p = Memory.ReadInt(ShotEffectPool.MainCharaEffectBase + (long)s * ShotEffectPool.EffectSlotStride + ShotEffectPool.EffectSlotModelOff);
                if (!WeaponModelFrames.IsRamPtr(p)) continue;
                long root = Memory.ToMmu(p);
                if (ValidateRoot(root)) roots.Add(root);
            }
            if (roots.Count == 0) { _locateBackoff = 8; return; }   // effect not loaded yet → retry later
            _roots = roots.ToArray();
            foreach (long root in _roots) WriteScaled(root);
        }
    }
}
