using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// "Harder Enemies" difficulty (Options → Harder Enemies checkbox). Globally speeds up enemy MOVEMENT and
    /// ANIMATION/action speed. Two independent, easily-tuned multipliers:
    ///   • <see cref="MoveSpeedMultiplier"/> — scales every enemy's _SET_MOVE (cmd 32) speed literals in its loaded
    ///     STB, once per species per floor. The STB reloads fresh each floor, so it self-reverts. (Enemies whose
    ///     moves use a variable speed, or that don't use _SET_MOVE, e.g. Cursed Rose, are unreached — the universal
    ///     animation scale partly compensates.)
    ///   • <see cref="AnimSpeedMultiplier"/> — each tick, multiplies each clip's NATURAL PlayingMotionSpeed (typically
    ///     0.3–0.5) while it plays; -1.0 (held/idle) is left alone, and the scaled rate stays under 1 frame/step so no
    ///     hit frame is skipped. Per-slot and universal.
    /// Toggling off stops the animation re-assert immediately (engine reverts to natural on the next motion change);
    /// movement reverts on the next floor load (the STB patch persists until then).
    /// </summary>
    internal static class HarderEnemies
    {
        internal static bool  Enabled             = false;
        internal static float MoveSpeedMultiplier = 2.0f;   // enemy travel speed (×)
        internal static float AnimSpeedMultiplier = 2.0f;   // enemy animation / action speed (×)

        private const int FnSetMove = 32;   // _SET_MOVE STB command

        private static readonly HashSet<int> _stbPatched = new();            // movement: STB native addrs done this floor
        private static readonly Dictionary<int, float> _animWritten = new(); // animation: slot -> rate we last wrote

        internal static void Tick()
        {
            // Clear per-floor state on floor exit (so STBs re-patch next floor). NOT cleared merely on disable — that
            // would let re-enabling double-patch an already-scaled STB.
            if (!Player.InDungeonFloor()) { _stbPatched.Clear(); _animWritten.Clear(); return; }
            if (!Enabled) return;

            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (EnemySlots.GetFloorEnemyId(s) == 0) continue;
                ScaleMovement(s);
                ScaleAnimation(s);
            }
        }

        // ── Movement: scale _SET_MOVE speed literals in the species' loaded STB (once per STB per floor) ──
        private static void ScaleMovement(int slot)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return;
            if (!_stbPatched.Add(stbNative)) return;   // each species' STB patched once / floor

            long stb = Memory.ToMmu(stbNative);
            if (Memory.ReadInt(stb) != StbVm.Magic) return;
            byte[] d = Memory.ReadByteArray(stb, 0xC000);
            if (d == null || d.Length < 0x10) return;
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(StbVm.CodeSectionOff);
            if (code < StbVm.LabelTableOff || code >= d.Length) return;

            for (int x = code; x + StbVm.InstrSize <= d.Length; x += 4)
            {
                if (Word(x) != StbVm.OpExt || Word(x + StbVm.OperandB) != 0) continue;   // op21 ext-call
                int argc = Word(x + StbVm.OperandA);
                if (argc < 2 || argc > 10) continue;
                int fp = x - argc * StbVm.InstrSize;                                     // first pushed arg = funcId
                if (fp < code || Word(fp) != StbVm.OpPush3 || Word(fp + StbVm.OperandA) != StbVm.TypeInt) continue;
                if (Word(fp + StbVm.OperandB) != FnSetMove) continue;

                int spOff = x - StbVm.InstrSize;   // speed = the push immediately before the ext-call
                if (Word(spOff) == StbVm.OpPush3 && Word(spOff + StbVm.OperandA) == StbVm.TypeFloat)
                {
                    int litOff = spOff + StbVm.OperandB;
                    float val = BitConverter.Int32BitsToSingle(Word(litOff));
                    if (val > 0f) Memory.WriteFloat(stb + litOff, val * MoveSpeedMultiplier);
                }
            }
        }

        // ── Animation: scale each clip's natural playback rate (per tick; skip -1.0 held/idle; no compound) ──
        private static void ScaleAnimation(int slot)
        {
            long addr = (long)ModelScaleOffsets.ModelBase
                      + (long)slot * ModelScaleOffsets.ModelStride
                      + ModelScaleOffsets.PlayingMotionSpeed;

            float cur = Memory.ReadFloat(addr);
            if (cur <= 0f) return;                                          // -1.0 = held/idle: leave it
            if (_animWritten.TryGetValue(slot, out float w) && cur == w) return;  // our value: no compound
            float scaled = cur * AnimSpeedMultiplier;
            Memory.WriteFloat(addr, scaled);
            _animWritten[slot] = scaled;
        }
    }
}
