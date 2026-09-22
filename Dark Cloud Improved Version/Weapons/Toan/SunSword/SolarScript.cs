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
        private const int LabelAi = 100, LabelHit = 110;
        private const int StaggerFrames = 20;          // the flash's own hit reaction, before the guard comes up (~1/3 s)
        private const int StaggerFramesMin = 4;        // …shortened to fit a cramped label rather than skipped entirely
        private const int RoomMargin = 0x60;           // bytes left untouched at the end of a label's span — see BelongsToUs
        private const int TypeBoss = 2;
        private const int SnapshotBytes = StbVm.InstrSize * 9;   // what HoldSeq writes, and what is saved to put back

        private sealed class Patched
        {
            public uint Stb;           // the script's base (guest), one per species
            public long CodeAt;        // where label 100's code starts
            public int  EntryPc;       // …as a CodeBase-relative offset: the jump target
            public int  Guard, Return; // that species' hold and lowering clips
            public byte[] Original;    // what was there before …
            public byte[] Written;     // … and exactly what we put there, so a restore can tell the script is still ours
            public long HitAt;         // …and the hit-reaction label, when there was room to take it over
            public byte[] HitOriginal, HitWritten;
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
            var seenPatched = new HashSet<uint>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(s));
                if (!Memory.IsValidGuest(stb) || !seen.Add(stb)) continue;      // one patch per species, not per slot
                int slotOf = s;
                if (!Ordinary(s)) continue;                                      // mimics, king mimics and bosses are left alone
                if (!HoldClipOf(s, out int guard, out int back)) continue;        // no motions at all: leave it to its own AI
                long b = Memory.ToMmu(stb);
                if (Memory.ReadInt(b) != StbVm.Magic) continue;
                long code = LabelCode(b, LabelAi, out int entryPc, out int aiRoom);
                if (code == 0 || !BelongsToUs(code, SnapshotBytes, aiRoom)) continue;
                byte[] orig = Memory.ReadBytesBatch(code, SnapshotBytes);
                if (orig == null) continue;
                byte[] hold = HoldSeq(guard, entryPc);
                Memory.WriteByteArray(code, hold);
                var rec = new Patched { Stb = stb, CodeAt = code, EntryPc = entryPc, Guard = guard, Return = back, Original = orig, Written = hold };

                // The hit reaction as well. CheckDmg runs label 110 DIRECTLY when the flash's own hit lands, which overrides
                // the AI label for its whole duration and is free to turn the enemy toward the player — the tracking that
                // survives the flash. Taking it over makes the stagger part of the stunned sequence: cancel movement, play
                // that species' damage clip, wait, and return, at which point the AI label's guard takes hold.
                int dmg = DamageOf(slotOf, out _);
                if (dmg >= 0)
                {
                    long hit = LabelCode(b, LabelHit, out _, out int hitRoom);
                    // Labels are packed back to back, so a replacement may only use the span before the next one starts.
                    // Rather than skip a cramped script, shorten the wait to what fits — a brief stagger still reads better
                    // than the vanilla reaction turning the enemy back toward the player.
                    int frames = Math.Min(StaggerFrames, ((hitRoom - RoomMargin) / StbVm.InstrSize) - 7);
                    if (hit != 0 && frames >= StaggerFramesMin && BelongsToUs(hit, (frames + 7) * StbVm.InstrSize, hitRoom))
                    {
                        byte[] seq = StaggerSeq(dmg, frames);
                        byte[] ho = Memory.ReadBytesBatch(hit, seq.Length);
                        if (ho != null) { Memory.WriteByteArray(hit, seq); rec.HitAt = hit; rec.HitOriginal = ho; rec.HitWritten = seq; }
                    }
                    else if (hit != 0)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[SunSword] script 0x{stb:X}: hit reaction has only {hitRoom} B — left alone, its stagger stays vanilla");
                }
                _patched.Add(rec);
                seenPatched.Add(stb);
            }
            int restarted = RestartScripts(seenPatched);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] blinding: {_patched.Count} species scripts held natively (guard where there is one, idle otherwise), {restarted} enemies re-entered at once");
        }

        /// <summary>Is the code at <paramref name="at"/> still exactly what we wrote there? The only safe basis for undoing
        /// a patch: if a script was reloaded, freed or replaced in the meantime, the address now belongs to something else and
        /// writing a saved snapshot into it corrupts whatever moved in.</summary>
        private static bool StillOurs(long at, byte[] written)
        {
            if (written == null) return false;
            byte[] now = Memory.ReadBytesBatch(at, written.Length);
            if (now == null) return false;
            for (int i = 0; i < written.Length; i++) if (now[i] != written[i]) return false;
            return true;
        }

        /// <summary>Make every affected enemy re-enter its AI label from the TOP, this frame.
        ///
        /// Overwriting the label does not disturb a script that is already running: its saved program counter points into
        /// the old code, so the enemy finishes its current pass — still chasing and turning — before control reaches the new
        /// sequence. That is the lag after the flash, and it is also unsafe, since a counter sitting inside the replaced
        /// bytes would resume mid-sequence. Clearing the slot's script-running flag makes Step call the label afresh next
        /// frame instead of resuming, so the guard takes hold immediately.</summary>
        private static int RestartScripts(HashSet<uint> stbs)
        {
            int n = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(s));
                if (!stbs.Contains(stb)) continue;
                Memory.WriteInt(CRunScript.SlotAddr(s, CRunScript.Pc), 0);                       // nothing to resume …
                Memory.WriteInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(s), 0);         // … so Step runs the label fresh
                n++;
            }
            return n;
        }

        /// <summary>The last stretch before the AI resumes: each script swaps its held guard for that species' own guard-
        /// LOWERING clip, so the pose breaks visibly a moment before the enemy acts. Species without one simply hold.</summary>
        internal static void Wake()
        {
            if (_woken || !Active) return;
            _woken = true;
            int n = 0;
            foreach (var p in _patched)
                if (p.Return >= 0 && StillOurs(p.CodeAt, p.Written))
                {
                    byte[] seq = HoldSeq(p.Return, p.EntryPc);
                    Memory.WriteByteArray(p.CodeAt, seq); p.Written = seq; n++;
                }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blinding ending: {n} scripts lowering their guard");
        }

        /// <summary>Clear the flag and give every script its own AI back.</summary>
        internal static void End()
        {
            if (!Active) { Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 0); return; }
            var stbs = new HashSet<uint>();
            int restored = 0, stale = 0;
            foreach (var p in _patched)
            {
                // ⚠ Only put the original back if the script is STILL the one we patched. Five seconds is long enough for a
                // script to be reloaded or freed — a mimic changes state when it is opened, a floor can change, a species can
                // leave — and by then the address belongs to something else. Writing a stale snapshot into it is what ended a
                // run at the memory-card screen. If the bytes are no longer ours, the script has already been replaced with
                // its own content and there is nothing to undo.
                bool ours = StillOurs(p.CodeAt, p.Written);
                if (ours)
                {
                    Memory.WriteByteArray(p.CodeAt, p.Original);
                    if (p.HitOriginal != null && StillOurs(p.HitAt, p.HitWritten)) Memory.WriteByteArray(p.HitAt, p.HitOriginal);
                    stbs.Add(p.Stb); restored++;
                }
                else stale++;
            }
            if (stale > 0)
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SunSword] {stale} script(s) were reloaded or freed while blinded — left alone, they already carry their own code");
            RestartScripts(stbs);                                        // …and back into their REAL AI from the top, not mid-sequence
            Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] blinding over: {restored} scripts restored");
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

        /// <summary>Everything except BOSSES, which BossScriptPatcher already rewrites — two writers on one script would
        /// collide. Mimics are NOT excluded: the memory-card crash came from restoring a snapshot into a script that had been
        /// reloaded underneath us, which <see cref="StillOurs"/> now prevents for every species, so there is no reason to
        /// single them out. A mimic still posing as a chest will play its guard clip like anything else.</summary>
        private static bool Ordinary(int slot)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return false;
            return Memory.ReadUShort(EnemySpeciesTable.RecordAddress(def.TableIndex.Value) + EnemySpeciesTable.MonsterType) != TypeBoss;
        }

        /// <summary>Is it safe to write `need` bytes here?
        ///
        /// The span to the next label's entry is NOT all ours: scripts keep their subroutine bodies outside the label
        /// regions, so callable code can sit inside that gap, and overwriting it corrupts functions other labels jump to.
        /// A margin is left untouched at the end of the span, and the bytes about to be replaced are checked to decode as
        /// plausible vmcode first — a script that does not look like one is skipped rather than clobbered.</summary>
        private static bool BelongsToUs(long at, int need, int room)
        {
            if (room < need + RoomMargin) return false;
            for (int o = 0; o < need; o += StbVm.InstrSize)
            {
                int op = Memory.ReadInt(at + o);
                if (op < 1 || op > 30) return false;                 // the VM dispatches 1..30; anything else is not code
            }
            return true;
        }

        /// <summary>The flash's own hit reaction: stop, play the species' damage clip, wait out its stagger, then return —
        /// whereupon the AI label (already replaced) brings the guard up. Plain YIELDs rather than a timer, because the
        /// script is the clock here: one per engine frame.</summary>
        private static byte[] StaggerSeq(int motion, int frames)
        {
            var recs = new List<(uint op, uint a, uint v)>
            {
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel),
                ((uint)StbVm.OpExt,   1, 0),
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMotion),
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)motion),
                ((uint)StbVm.OpExt,   2, 0),
            };
            for (int i = 0; i < frames; i++) recs.Add(((uint)StbVm.OpYield, 0, 0));
            recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, 0));
            recs.Add(((uint)StbVm.OpRet, 0, 0));
            var blk = new byte[recs.Count * StbVm.InstrSize];
            for (int i = 0; i < recs.Count; i++)
            {
                BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * StbVm.InstrSize);
                BitConverter.GetBytes(recs[i].a).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandA);
                BitConverter.GetBytes(recs[i].v).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandB);
            }
            return blk;
        }

        /// <summary>That slot's species damage clip, or -1.</summary>
        private static int DamageOf(int slot, out int unused)
        {
            unused = 0;
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return -1;
            return EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g) ? g.Damage : -1;
        }

        /// <summary>Label 100's code address: its entry PC, which the label table's funcdata holds, past the code base.</summary>
        /// <summary>A label's code address, its CodeBase-relative entry offset, and how many bytes sit before the next
        /// label's code begins — labels are packed back to back, so that span is all the room a replacement may use.</summary>
        private static long LabelCode(long stb, int label, out int entryPc, out int room)
        {
            entryPc = 0; room = 0;
            int cb = Memory.ReadInt(stb + StbVm.CodeSectionOff);
            int tbl = Memory.ReadInt(stb + StbVm.LabelTableOff), cnt = Memory.ReadInt(stb + StbVm.LabelCount);
            if (cnt <= 0 || cnt > 64) return 0;
            var starts = new List<int>();
            int mine = -1;
            for (int i = 0; i < cnt; i++)
            {
                int pc = Memory.ReadInt(stb + Memory.ReadInt(stb + tbl + i * 8 + 4));
                starts.Add(pc);
                if (Memory.ReadInt(stb + tbl + i * 8) == label) mine = pc;
            }
            if (mine < 0) return 0;
            entryPc = mine;
            int next = int.MaxValue;
            foreach (int pc in starts) if (pc > mine && pc < next) next = pc;
            room = next == int.MaxValue ? 0x400 : next - mine;      // the last label: assume a page, it is never the tight one
            return stb + cb + mine;
        }

        /// <summary>What this species holds while blinded: its guard, or its IDLE where it has no guard.
        ///
        /// Only about 111 of 158 species have a guard clip — flyers such as bats do not — and skipping those left them
        /// carrying on through the flash untouched, which is exactly what "bats are unaffected" looked like. Holding the
        /// idle instead stuns everything: a grounded enemy braces, a flyer hovers in place, and neither acts. The guard
        /// LOWERING clip is only meaningful for the ones that actually raised a guard.</summary>
        private static bool HoldClipOf(int slot, out int motion, out int back)
        {
            motion = -1; back = -1;
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return false;
            if (!EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g)) return false;
            motion = g.HasGuard ? g.Loop : g.Idle;
            back = g.HasGuard && g.HasReturn ? g.Return : -1;
            return true;
        }
    }
}
