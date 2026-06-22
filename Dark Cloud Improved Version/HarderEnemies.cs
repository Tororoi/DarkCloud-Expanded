using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// "Harder Enemies" difficulty (Options → Harder Enemies checkbox). Globally speeds up enemy MOVEMENT and
    /// ANIMATION/action speed. Two independent, easily-tuned multipliers:
    ///   • <see cref="MoveSpeedMultiplier"/> — scales every enemy's _SET_MOVE (cmd 32) speed literals in its loaded
    ///     STB, once per species per floor (self-reverts on floor reload).
    ///   • <see cref="AnimSpeedMultiplier"/> — per-tick, multiplies each clip's natural PlayingMotionSpeed, EXCEPT
    ///     while the enemy is inside an attack hit window, where it holds the natural rate (a "dwell").
    ///
    /// The dwell is why attacks still connect when sped up. Root cause (see enemy-body-collision notes): the enemy
    /// attack sphere is built by CheckDmg from the weapon bone ONE render-pass stale and tested against the player by
    /// BtCheckDamageProc — so a fast swing leaves the sphere trailing where the weapon WAS and the player point never
    /// lands inside it (size/window-width can't fix it; only the per-frame swing magnitude). Holding natural speed
    /// across the contact frames shrinks that trail to the amount the engine is designed for, so the native hit/i-
    /// frame/effect pipeline works unchanged. Hit windows come from each species' _SET_DMG_COL (cmd 131)
    /// (bone, radius, startFrame, endFrame); the natural rate is captured from the engine during the swing's wind-up.
    /// </summary>
    internal static class HarderEnemies
    {
        internal static bool  Enabled             = false;
        internal static float MoveSpeedMultiplier = 2.0f;   // enemy travel speed (×)
        internal static float AnimSpeedMultiplier = 2.0f;   // enemy animation / action speed (×)
        private const  float DwellMargin          = 1.0f;   // frames of lead-in/out around the hit window for the dwell

        private const int FnSetMove   = 32;    // _SET_MOVE
        private const int FnSetDmgCol = 131;   // _SET_DMG_COL — attack collision (bone, radius, startFrame, endFrame)

        private static readonly HashSet<int> _stbDone = new();                                    // STB natives done this floor
        private static readonly Dictionary<int, List<(float lo, float hi)>> _hitWindows = new();  // eid -> attack hit windows
        private static readonly Dictionary<int, float> _animWritten = new();                      // slot -> last anim rate we wrote
        private static readonly Dictionary<int, float> _animNatural = new();                      // slot -> captured natural rate

        internal static void Tick()
        {
            if (!Player.InDungeonFloor())
            {
                _stbDone.Clear(); _hitWindows.Clear(); _animWritten.Clear(); _animNatural.Clear();
                return;
            }
            // Run when the toggle is on (global move + animation speed-up) OR when minibosses exist (their walk clip
            // still needs the stride-sync slow-down, which composes with the toggle in the single animation writer).
            bool anyMini = MiniBoss.miniBossEnemyNumbers.Count > 0;
            if (!Enabled && !anyMini) return;

            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int eid = EnemySlots.GetFloorEnemyId(s);
                if (eid == 0) continue;
                bool isMini = MiniBoss.miniBossEnemyNumbers.Contains(s);
                if (!Enabled && !isMini) continue;   // toggle off: only minibosses (walk-sync) need touching
                if (Enabled) ProcessStb(s, eid);     // move-speed STB scaling IS the "Faster enemies" feature
                ScaleAnimation(s, eid, isMini);
            }
        }

        // ── Per floor, per species: walk the loaded STB once — scale _SET_MOVE speeds, collect _SET_DMG_COL windows ──
        private static void ProcessStb(int slot, int eid)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return;
            if (!_stbDone.Add(stbNative)) return;

            long stb = Memory.ToMmu(stbNative);
            if (Memory.ReadInt(stb) != StbVm.Magic) return;
            byte[] d = Memory.ReadByteArray(stb, 0xC000);
            if (d == null || d.Length < 0x10) return;
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(StbVm.CodeSectionOff);
            if (code < StbVm.LabelTableOff || code >= d.Length) return;

            var windows = new List<(float, float)>();
            for (int x = code; x + StbVm.InstrSize <= d.Length; x += 4)
            {
                if (Word(x) != StbVm.OpExt || Word(x + StbVm.OperandB) != 0) continue;   // op21 ext-call
                int argc = Word(x + StbVm.OperandA);
                if (argc < 2 || argc > 10) continue;
                int fp = x - argc * StbVm.InstrSize;                                     // first pushed arg = funcId
                if (fp < code || Word(fp) != StbVm.OpPush3 || Word(fp + StbVm.OperandA) != StbVm.TypeInt) continue;
                int fn = Word(fp + StbVm.OperandB);

                if (fn == FnSetMove)
                {
                    int spOff = x - StbVm.InstrSize;   // speed = the push right before the ext-call
                    if (Word(spOff) == StbVm.OpPush3 && Word(spOff + StbVm.OperandA) == StbVm.TypeFloat)
                    {
                        int litOff = spOff + StbVm.OperandB;
                        float val = BitConverter.Int32BitsToSingle(Word(litOff));
                        if (val > 0f) Memory.WriteFloat(stb + litOff, val * MoveSpeedMultiplier);
                    }
                }
                else if (fn == FnSetDmgCol && argc >= 5)
                {
                    // args = (bone, radius_float, startFrame_float, endFrame_float) — floats at fp+3/+4 * InstrSize
                    int si = fp + 3 * StbVm.InstrSize, ei = fp + 4 * StbVm.InstrSize;
                    if (Word(si) == StbVm.OpPush3 && Word(si + StbVm.OperandA) == StbVm.TypeFloat &&
                        Word(ei) == StbVm.OpPush3 && Word(ei + StbVm.OperandA) == StbVm.TypeFloat)
                    {
                        float lo = BitConverter.Int32BitsToSingle(Word(si + StbVm.OperandB));
                        float hi = BitConverter.Int32BitsToSingle(Word(ei + StbVm.OperandB));
                        if (hi > lo) windows.Add((lo, hi));
                    }
                }
            }
            if (windows.Count > 0) _hitWindows[eid] = windows;
        }

        // ── Per tick: drive PlayingMotionSpeed for one slot. This is the SOLE animation-rate writer, composing two
        // multiplicative factors: "Faster enemies" speeds every clip ×AnimSpeedMultiplier (with a hit-window DWELL so
        // attacks still connect), and a miniboss additionally has its WALK clip slowed ×(1/MiniBoss.WalkAnimSyncFactor)
        // to sync its scaled-up strides. e.g. anim ×2 + walk-sync 1.5 ⇒ ×1.33 walk; anim ×1.5 + 1.5 ⇒ ×1.0 (natural).
        // A second independent writer would fight the compound guard, which is why both live here. ──
        private static void ScaleAnimation(int slot, int eid, bool isMini)
        {
            long addr = (long)ModelScaleOffsets.ModelBase
                      + (long)slot * ModelScaleOffsets.ModelStride
                      + ModelScaleOffsets.PlayingMotionSpeed;
            float cur = Memory.ReadFloat(addr);
            if (cur <= 0f) return;                                          // -1.0 = use KEY speed: leave it

            // Dwell only matters while speeding up (Enabled); walk clips have no hit window so it never fights the sync.
            if (Enabled && InHitWindow(slot, eid))
            {
                // hold the clip's natural rate through the contact frames (captured during the wind-up); only ever
                // slow down, never speed up, and skip once already at natural so we don't fight the engine.
                if (_animNatural.TryGetValue(slot, out float nat) && nat > 0f && cur > nat)
                    Memory.WriteFloat(addr, nat);
                return;
            }

            if (_animWritten.TryGetValue(slot, out float w) && cur == w) return;   // our value: no compound

            float mult = Enabled ? AnimSpeedMultiplier : 1f;
            if (isMini && IsWalkMotion(slot) && !MiniBoss.WalkSyncExcluded((ushort)eid))
                mult /= MiniBoss.WalkAnimSyncFactor;   // miniboss walk: slow to sync stride (skip flyers — see exclude list)

            if (mult == 1f) { _animNatural.Remove(slot); _animWritten.Remove(slot); return; }  // nothing to do: natural

            _animNatural[slot] = cur;                                              // capture the natural rate
            float scaled = cur * mult;
            Memory.WriteFloat(addr, scaled);
            _animWritten[slot] = scaled;
        }

        // The chase locomotion ("walk") clip is motion idx 1 for essentially every enemy (idle=0, locomotion=1, in the
        // canonical ~30–45 frame slot) regardless of its .chr label — a literal "歩き" at a high index, or idx 0
        // mislabeled "歩き", are secondary/idle poses, not the chase clip (validated on Captain/Halloween/Bomber Head).
        private const int WalkMotion = 1;
        private static bool IsWalkMotion(int slot) =>
            Memory.ReadInt((long)ModelScaleOffsets.ModelBase
                         + (long)slot * ModelScaleOffsets.ModelStride
                         + ModelScaleOffsets.PlayingMotionId) == WalkMotion;

        private static bool InHitWindow(int slot, int eid)
        {
            if (!_hitWindows.TryGetValue(eid, out var wins)) return false;
            float frame = Memory.ReadFloat((long)ModelScaleOffsets.ModelBase
                                         + (long)slot * ModelScaleOffsets.ModelStride
                                         + ModelScaleOffsets.PlayingMotionFrame);
            foreach (var (lo, hi) in wins)
                if (frame >= lo - DwellMargin && frame <= hi + DwellMargin) return true;
            return false;
        }
    }
}
