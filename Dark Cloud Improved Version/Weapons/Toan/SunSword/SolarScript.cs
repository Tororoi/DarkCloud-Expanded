using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Solar Flash's blinding, driven by the enemies' OWN behaviour scripts instead of by mod ticks.
    ///
    /// Polling at 30 ms fought the engine: between ticks it runs about two frames, so a hit landing mid-hold restarted the
    /// enemy's reaction script and moved it before the next write could stop it (the sliding), and every motion had to be
    /// re-requested from outside. Letting the script do it means the transitions, the blending and the movement cancel all
    /// come from the engine for free.
    ///
    /// HOW. While the flash blinds the floor, <see cref="GlobalInt.SolarBlindIndex"/> is non-zero — the one channel a
    /// running script can read (<c>_GET_GLOBAL_INT</c>). Each species' AI label (100) gets its head replaced by:
    ///
    ///     _SET_MOVE_CANSEL();  _SET_MOTION(guard, 1, 0);  YIELD;  JMP self
    ///
    /// so the enemy cancels its movement, holds its own guard clip and waits, frame after frame, natively. The original
    /// bytes are saved and written back when the blinding ends, exactly as BossScriptPatcher does for a dying boss.
    ///
    /// ⚠ A script is per SPECIES, not per slot: every enemy sharing a patched script guards, not only those in range. That
    /// is the trade for native behaviour, and it matches what the flash means — the whole room is blinded.
    ///
    /// ⚠ There is nowhere to grow. Labels are packed back to back and the files end with a handful of zero bytes, so a
    /// flag-checked preamble cannot be added ahead of the real code without displacing opcodes that have no home. Hence
    /// replace-and-restore rather than a permanent patch made once at equip time.
    /// </summary>
    internal static class SolarScript
    {
        private const int LabelAi = 100;
        private const int SnapshotBytes = StbVm.InstrSize * 9;   // what HoldSeq writes, and what is saved to put back

        private sealed class Patched
        {
            public uint Stb;           // the script's base (guest), one per species
            public long CodeAt;        // where label 100's code starts
            public int  EntryPc;       // …as a CodeBase-relative offset: the jump target
            public int  Guard, Return; // that species' hold and lowering clips
            public byte[] Original;    // what was there before
        }
        private static readonly List<Patched> _patched = new List<Patched>();

        private static bool _woken;

        internal static bool Active => _patched.Count > 0;

        /// <summary>Raise the flag and put the guard sequence into every species on the floor. Idempotent.</summary>
        internal static void Begin()
        {
            if (Active) return;
            _woken = false;
            Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 1);
            var seen = new HashSet<uint>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(s));
                if (!Memory.IsValidGuest(stb) || !seen.Add(stb)) continue;      // one patch per species, not per slot
                if (!GuardOf(s, out int guard, out int back)) continue;           // no guard clip: leave it to its own AI
                long b = Memory.ToMmu(stb);
                if (Memory.ReadInt(b) != StbVm.Magic) continue;
                long code = LabelCode(b, out int entryPc);
                if (code == 0) continue;
                byte[] orig = Memory.ReadBytesBatch(code, SnapshotBytes);
                if (orig == null) continue;
                Memory.WriteByteArray(code, HoldSeq(guard, entryPc));
                _patched.Add(new Patched { Stb = stb, CodeAt = code, EntryPc = entryPc, Guard = guard, Return = back, Original = orig });
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] blinding: {_patched.Count} species scripts hold their guard natively (flag {GlobalInt.SolarBlindIndex})");
        }

        /// <summary>The last stretch before the AI resumes: each script swaps its held guard for that species' own guard-
        /// LOWERING clip, so the pose breaks visibly a moment before the enemy acts. Species without one simply hold.</summary>
        internal static void Wake()
        {
            if (_woken || !Active) return;
            _woken = true;
            int n = 0;
            foreach (var p in _patched)
                if (p.Return >= 0) { Memory.WriteByteArray(p.CodeAt, HoldSeq(p.Return, p.EntryPc)); n++; }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blinding ending: {n} scripts lowering their guard");
        }

        /// <summary>Clear the flag and give every script its own AI back.</summary>
        internal static void End()
        {
            if (!Active) { Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 0); return; }
            foreach (var p in _patched) Memory.WriteByteArray(p.CodeAt, p.Original);
            Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blinding over: {_patched.Count} scripts restored");
            _patched.Clear();
            _woken = false;
        }

        /// <summary>The blinded AI, in the enemy's own bytecode:
        ///
        ///     _SET_MOVE_CANSEL();  _SET_MOTION(clip);        ← once
        ///   loop: _SET_MOVE_CANSEL();  YIELD;  JMP loop      ← every frame after
        ///
        /// The motion is issued ONCE and then only waited on: _SET_MOTION rewrites the render object's frame cursor, so
        /// re-issuing it every frame would restart the clip and freeze the pose on its first frame. Movement is cancelled
        /// again each frame instead, which is what actually stops the sliding — a hit landing mid-hold runs the enemy's
        /// reaction script (label 110, untouched) and that sets movement, and here the AI itself takes it straight back.
        /// One argument to _SET_MOTION selects the handler's KEY-rate form, so the clip plays at its own native speed.</summary>
        private static byte[] HoldSeq(int motion, int entryPc)
        {
            const int Cancel = 0, Motion = 2, Loop = 5;                         // record indices, for the jump target
            var recs = new (uint op, uint a, uint v)[]
            {
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel),   // 0
                ((uint)StbVm.OpExt,   1, 0),                                               // 1
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMotion),       // 2
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)motion),                  // 3
                ((uint)StbVm.OpExt,   2, 0),                                               // 4  argc counts the command id
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel),   // 5  ← loop
                ((uint)StbVm.OpExt,   1, 0),                                               // 6
                ((uint)StbVm.OpYield, 0, 0),                                               // 7
                ((uint)StbVm.OpJmp,   (uint)(entryPc + Loop * StbVm.InstrSize), 0),        // 8  target is CodeBase-relative
            };
            _ = Cancel; _ = Motion;
            var blk = new byte[recs.Length * StbVm.InstrSize];
            for (int i = 0; i < recs.Length; i++)
            {
                BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * StbVm.InstrSize);
                BitConverter.GetBytes(recs[i].a).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandA);
                BitConverter.GetBytes(recs[i].v).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandB);
            }
            return blk;
        }

        /// <summary>Label 100's code address: its entry PC, which the label table's funcdata holds, past the code base.</summary>
        private static long LabelCode(long stb, out int entryPc)
        {
            entryPc = 0;
            int cb = Memory.ReadInt(stb + StbVm.CodeSectionOff);
            int tbl = Memory.ReadInt(stb + StbVm.LabelTableOff), cnt = Memory.ReadInt(stb + StbVm.LabelCount);
            for (int i = 0; i < cnt && i < 64; i++)
                if (Memory.ReadInt(stb + tbl + i * 8) == LabelAi)
                {
                    entryPc = Memory.ReadInt(stb + Memory.ReadInt(stb + tbl + i * 8 + 4));   // the label's funcdata: its entry PC
                    return stb + cb + entryPc;
                }
            return 0;
        }

        /// <summary>That slot's species guard-hold clip, or false if the model has none.</summary>
        private static bool GuardOf(int slot, out int motion, out int back)
        {
            motion = -1; back = -1;
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return false;
            if (!EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g) || !g.HasGuard) return false;
            motion = g.Loop;
            back = g.HasReturn ? g.Return : -1;
            return true;
        }
    }
}
