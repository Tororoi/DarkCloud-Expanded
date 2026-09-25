using System;
using System.Collections.Generic;
using System.Linq;

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
    /// running script can read (<c>_GET_GLOBAL_INT</c>). Each held species gets a block of the mod's own stub memory
    /// (<see cref="CodeCaves.SolarStubBase"/>) holding three small programs in its bytecode:
    ///
    ///     hold:    _SET_MOVE_CANSEL();  _SET_MOTION(guard);            loop: _SET_MOVE_CANSEL();  YIELD;  JMP loop
    ///     lower:   _SET_MOVE_CANSEL();  _SET_MOTION(return, -1, once); loop: _SET_MOVE_CANSEL();  YIELD;  JMP loop
    ///     stagger: _SET_MOVE_CANSEL();  _SET_MOTION(damage);  YIELD × n;  RET
    ///
    /// and the FIRST instruction of its AI label (100) becomes a jump to the hold, the first of its hit-reaction label
    /// (110) a jump to the stagger. Winding down changes the AI label's jump target to the lowering program; ending puts
    /// the two original instructions back. Every enemy of the species then guards natively, and one enemy of a species
    /// can be pointed somewhere else by its own script PC (the Sword of Zeus's electrocution, <see cref="Convulse"/>).
    ///
    /// ⚠ WHY the programs live in stub memory and the labels carry ONE instruction each: a script's saved PC sits inside
    /// the bytes it yielded from. Writing a whole sequence over a label while enemies are parked in it — as the hold used
    /// to be — let the engine step one of them into half-written bytecode whenever it ran during the write (a byte-wise
    /// write took most of a frame), and an `EXT` with a garbage command id is a jump to nowhere: the memory-card screen.
    /// The stub programs are never written while anyone is inside them, and a label's single jump instruction changes
    /// in one 64-bit store (opcode and target together), the other word being one a jump does not read.
    ///
    /// ⚠ A script is per SPECIES, not per slot: every enemy sharing a patched script guards, not only those in range. That
    /// is the trade for native behaviour, and it matches what the flash means — the whole room is blinded.
    /// </summary>
    internal static class SolarScript
    {
        private const int LabelAi = 100, LabelHit = 110;
        private const double IdleBeat = 0.4;           // the pause every enemy gets between lowering its guard and acting
        private const int StaggerFrames = 20;          // the flash's own hit reaction, before the guard comes up (~1/3 s)
        private const int TypeBoss = 2;
        // Where the three programs sit in a species' block (see CodeCaves.SolarStubBlock, 0x300): hold 9 cells, lowering
        // 11 cells, stagger 7 + StaggerFrames cells.
        private const int HoldAt = 0x000, LowerAt = 0x070, StaggerAt = 0x100;
        private const int HoldLoop = 5, LowerLoop = 7;  // the cell each loop jumps back to

        private sealed class Patched
        {
            public uint Stb;           // the script's base (guest), one per species
            public long Block;         // its stub block (MMU)
            public long CodeAt;        // label 100's first instruction …
            public byte[] Original;    // … what was there before …
            public byte[] Written;     // … and exactly what we put there, so a restore can tell the script is still ours
            public long HitAt;         // …and label 110's, when the species has a damage clip (0 = untouched)
            public byte[] HitOriginal, HitWritten;
            public int  Guard, Return; // that species' hold and lowering clips
            public double ReturnSeconds;  // how long THIS species' guard-lowering clip runs …
            public bool   Woken;          // … and whether its wind-down has been started
        }
        private static readonly List<Patched> _patched = new List<Patched>();

        internal static bool Active => _patched.Count > 0;

        /// <summary>Raise the flag and point every species on the floor at its guard hold. Idempotent.</summary>
        internal static void Begin()
        {
            if (Active) return;
            Memory.WriteInt(GlobalInt.Addr(GlobalInt.SolarBlindIndex), 1);
            var seen = new HashSet<uint>();
            var seenPatched = new HashSet<uint>();
            int noRoom = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(s));
                if (!Memory.IsValidGuest(stb) || !seen.Add(stb)) continue;      // one patch per species, not per slot
                if (!Ordinary(s)) continue;                                      // bosses are left alone
                if (!HoldClipOf(s, out int guard, out int back, out var clips)) continue;   // no motions at all: leave it to its own AI
                long b = Memory.ToMmu(stb);
                if (Memory.ReadInt(b) != StbVm.Magic) continue;
                long code = LabelCode(b, LabelAi, out _, out _);
                if (code == 0 || !LooksLikeCode(code)) continue;
                if (_patched.Count >= CodeCaves.SolarStubBlocks) { noRoom++; continue; }
                byte[] orig = Memory.ReadBytesBatch(code, StbVm.InstrSize);
                if (orig == null) continue;

                long block = CodeCaves.SolarStubBase + (long)_patched.Count * CodeCaves.SolarStubBlock;
                uint blockGuest = (uint)(block - 0x20000000L);
                uint origin = stb + (uint)Memory.ReadInt(b + StbVm.CodeSectionOff);   // jump operands are relative to this
                int dmg = DamageOf(s);

                // The programs first — nobody is in the block yet — then the jumps that lead into them.
                Memory.WriteBytesBatch(block + HoldAt, LoopSeq(guard, blockGuest + HoldAt, origin, once: false));
                if (back >= 0) Memory.WriteBytesBatch(block + LowerAt, LoopSeq(back, blockGuest + LowerAt, origin, once: true));
                if (dmg >= 0)  Memory.WriteBytesBatch(block + StaggerAt, StaggerSeq(dmg, StaggerFrames));

                byte[] jmp = JmpInstr(blockGuest + HoldAt - origin);
                PutInstr(code, jmp);
                var rec = new Patched { Stb = stb, Block = block, CodeAt = code, Original = orig, Written = jmp, Guard = guard, Return = back,
                                        ReturnSeconds = back >= 0 ? ClipSeconds(s, back, clips) : 0 };

                // The hit reaction as well. CheckDmg runs label 110 DIRECTLY when the flash's own hit lands, which overrides
                // the AI label for its whole duration and is free to turn the enemy toward the player — the tracking that
                // survives the flash. Taking it over makes the stagger part of the stunned sequence: cancel movement, play
                // that species' damage clip, wait, and return, at which point the AI label's guard takes hold.
                if (dmg >= 0)
                {
                    long hit = LabelCode(b, LabelHit, out _, out _);
                    byte[] ho = hit != 0 && LooksLikeCode(hit) ? Memory.ReadBytesBatch(hit, StbVm.InstrSize) : null;
                    if (ho != null)
                    {
                        byte[] hj = JmpInstr(blockGuest + StaggerAt - origin);
                        PutInstr(hit, hj);
                        rec.HitAt = hit; rec.HitOriginal = ho; rec.HitWritten = hj;
                    }
                }
                _patched.Add(rec);
                seenPatched.Add(stb);
            }
            int restarted = RestartScripts(seenPatched);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] blinding: {_patched.Count} species scripts held natively (guard where there is one, idle otherwise), {restarted} enemies re-entered at once"
                + (noRoom > 0 ? $"; {noRoom} species beyond the {CodeCaves.SolarStubBlocks} stub blocks left to their own AI" : ""));
        }

        /// <summary>ELECTROCUTE one enemy. A program of its own is written into the mod's reserved AI-stub slot
        /// (<see cref="CodeCaves.AiStubZeus"/>): cancel movement, issue the species' DAMAGE clip once, then every frame set
        /// the clip's playing frame — through the engine's own _SET_MOTION_FRM — to the next step of an up-and-back walk
        /// over consecutive frames (lo, lo+1 … hi … lo+1, lo, …), yielding between steps, forever. The unit's own script PC
        /// is pointed at it (the same per-slot write RestartScripts relies on), so this one unit convulses while its
        /// species braces; the hold's guard is what it goes back to. Jump operands are relative to the unit's own script
        /// (stbBase + CodeBase), so the program is built per strike. False when the blinding is not up, the species is not
        /// one it holds, or the program would not fit the slot.</summary>
        internal static bool Convulse(int slot, int damageClip, float frameLo, float frameHi)
        {
            if (!Active || !Enemies.IsLive(slot) || damageClip < 0) return false;
            uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(slot));
            if (!_patched.Exists(p => p.Stb == stb && !p.Woken)) return false;
            long b = Memory.ToMmu(stb);
            uint origin = stb + (uint)Memory.ReadInt(b + StbVm.CodeSectionOff);                // op16 operands are relative to this
            uint stubGuest = (uint)(CodeCaves.AiStubZeus - 0x20000000L);
            var recs = new List<(uint op, uint a, uint v)>
            {
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel), ((uint)StbVm.OpExt, 1, 0),
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMotion), ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)damageClip), ((uint)StbVm.OpExt, 2, 0),
                ((uint)StbVm.OpYield, 0, 0),                                                    // the clip is up next frame
            };
            int loopAt = recs.Count;
            int n = Math.Max(2, (int)Math.Round(frameHi - frameLo) + 1);
            var sweep = new List<float>();
            for (int k = 0; k < n; k++) sweep.Add(frameLo + k);                                 // up …
            for (int k = n - 2; k >= 1; k--) sweep.Add(frameLo + k);                            // … and back down, ends not repeated
            foreach (float f in sweep)
            {
                recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMotionFrm));
                recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeFloat, BitConverter.SingleToUInt32Bits(f)));
                recs.Add(((uint)StbVm.OpExt, 2, 0));
                recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel));
                recs.Add(((uint)StbVm.OpExt, 1, 0));
                recs.Add(((uint)StbVm.OpYield, 0, 0));
            }
            recs.Add(((uint)StbVm.OpJmp, stubGuest + (uint)(loopAt * StbVm.InstrSize) - origin, 0));
            if (recs.Count * StbVm.InstrSize > CodeCaves.AiStubStride)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] electrocution program of {recs.Count} cells does not fit the stub slot — not run");
                return false;
            }
            byte[] blk = Pack(recs);
            Memory.WriteBytesBatch(CodeCaves.AiStubZeus, blk);
            _convulseEnd = stubGuest + (uint)blk.Length;
            Memory.WriteInt(CRunScript.SlotAddr(slot, CRunScript.Pc), (int)stubGuest);           // resume HERE …
            Memory.WriteInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(slot), 1);           // … rather than calling the label afresh
            return true;
        }
        private static uint _convulseEnd;
        /// <summary>Is the unit's script still inside the electrocution program? A hit reaction (CheckDmg runs label 110
        /// in the unit's own script slot) or a restart takes the PC elsewhere, after which the AI label is entered from
        /// its top — the guard — and the program has to be pointed at again.</summary>
        internal static bool Convulsing(int slot)
        {
            uint stubGuest = (uint)(CodeCaves.AiStubZeus - 0x20000000L);
            uint pc = (uint)Memory.ReadInt(CRunScript.SlotAddr(slot, CRunScript.Pc));
            return pc >= stubGuest && pc < _convulseEnd && Memory.ReadInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(slot)) != 0;
        }
        /// <summary>…and back to the label's top — the guard hold — from where the wind-down takes it like everyone else.</summary>
        internal static void Unconvulse(int slot)
        {
            if (slot < 0 || !Enemies.IsLive(slot)) return;
            Memory.WriteInt(CRunScript.SlotAddr(slot, CRunScript.Pc), 0);
            Memory.WriteInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(slot), 0);
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
        /// Changing where the label leads does not disturb a script that is already running: its saved program counter
        /// points into the program it yielded from, so the enemy would finish its current pass — still chasing and
        /// turning, or still holding the guard when it should be lowering it. Clearing the slot's script-running flag makes
        /// Step call the label afresh next frame instead of resuming, so the new program takes hold immediately.</summary>
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

        /// <summary>Wind each species down on ITS OWN clock.
        ///
        /// One shared window could not fit them all: the guard-lowering clips run from 0.21 s to 1.67 s, so a single figure
        /// either cut the long ones off mid-animation or left the short ones standing idle for most of a second. Each
        /// species is started exactly its own clip-length plus <see cref="IdleBeat"/> before the end, so every enemy gets
        /// the same brief pause between lowering its guard and acting, whatever its animation costs. The label's jump is
        /// retargeted with one word — the program it led to is left as it is for whoever is still inside it.</summary>
        internal static void Wake(double secondsLeft)
        {
            if (!Active) return;
            var stbs = new HashSet<uint>();
            int n = 0;
            foreach (var p in _patched)
            {
                if (p.Woken || p.Return < 0) continue;                       // no lowering clip: it simply holds to the end
                if (secondsLeft > p.ReturnSeconds + IdleBeat) continue;      // not yet its turn
                p.Woken = true;
                if (!StillOurs(p.CodeAt, p.Written)) continue;               // reloaded underneath us: leave it alone
                uint origin = p.Stb + (uint)Memory.ReadInt(Memory.ToMmu(p.Stb) + StbVm.CodeSectionOff);
                uint target = (uint)(p.Block - 0x20000000L) + LowerAt - origin;
                Memory.WriteUInt(p.CodeAt + StbVm.OperandA, target);
                BitConverter.GetBytes(target).CopyTo(p.Written, StbVm.OperandA);
                stbs.Add(p.Stb); n++;
            }
            if (n == 0) return;
            // As with every retarget: re-enter the label, or the saved PC stays in the hold's loop and the new clip is never issued.
            int restarted = RestartScripts(stbs);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] {n} script(s) lowering their guard ({secondsLeft:0.00}s left), {restarted} enemies re-entered to play it");
        }

        /// <summary>How long a clip runs, from the model's OWN motion table — its frame range at its own KEY rate — falling
        /// back to the decoded table when the live read looks implausible.</summary>
        private static double ClipSeconds(int slot, int motion, EnemyGuardMotions.Guard g)
        {
            long model = ModelScaleOffsets.ModelBase + (long)slot * ModelScaleOffsets.ModelStride;
            uint table = Memory.ReadGuestPtr(model + ModelScaleOffsets.MotionTablePtr);
            if (Memory.IsValidGuest(table))
            {
                long e = Memory.ToMmu(table) + (long)motion * ModelScaleOffsets.MotionTableStride;
                int start = Memory.ReadInt(e + ModelScaleOffsets.MotionTableStart);
                int end   = Memory.ReadInt(e + ModelScaleOffsets.MotionTableEnd);
                float step = Memory.ReadFloat(e + ModelScaleOffsets.MotionTableSpeed);
                if (end > start && step > 0.01f && step < 10f) return (end - start) / (step * 60.0);
            }
            return Math.Max(1, g.ReturnEnd - g.ReturnStart) / (Math.Max(0.05f, g.Speed) * 60.0);
        }

        /// <summary>Clear the flag and give every script its own AI back: the two original instructions go back into the
        /// labels, then every enemy of those species re-enters its label from the top — out of whichever stub program it
        /// was parked in, which stays intact until the next blinding rewrites it.</summary>
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
                    PutInstr(p.CodeAt, p.Original);
                    if (p.HitOriginal != null && StillOurs(p.HitAt, p.HitWritten)) PutInstr(p.HitAt, p.HitOriginal);
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
        }

        // ── the bytecode ─────────────────────────────────────────────────────────────────

        /// <summary>One instruction, replaced whole. A jump reads only its opcode and first operand, so those two words go
        /// in one 64-bit store and the third — which the jump ignores — after it; putting an original back goes the other
        /// way round, the ignored word first, so that at no point does a jump exist with someone else's target.</summary>
        private static void PutInstr(long at, byte[] instr)
        {
            bool jump = BitConverter.ToUInt32(instr, 0) == StbVm.OpJmp;
            if (jump) { Memory.WriteULong(at, BitConverter.ToUInt64(instr, 0)); Memory.WriteInt(at + StbVm.OperandB, BitConverter.ToInt32(instr, StbVm.OperandB)); }
            else      { Memory.WriteInt(at + StbVm.OperandB, BitConverter.ToInt32(instr, StbVm.OperandB)); Memory.WriteULong(at, BitConverter.ToUInt64(instr, 0)); }
        }
        private static byte[] JmpInstr(uint operandA) => Pack(new List<(uint, uint, uint)> { ((uint)StbVm.OpJmp, operandA, 0u) });

        private static byte[] Pack(List<(uint op, uint a, uint v)> recs)
        {
            var blk = new byte[recs.Count * StbVm.InstrSize];
            for (int i = 0; i < recs.Count; i++)
            {
                BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * StbVm.InstrSize);
                BitConverter.GetBytes(recs[i].a).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandA);
                BitConverter.GetBytes(recs[i].v).CopyTo(blk, i * StbVm.InstrSize + StbVm.OperandB);
            }
            return blk;
        }

        /// <summary>The blinded AI, in the enemy's own bytecode:
        ///
        ///     _SET_MOVE_CANSEL();  _SET_MOTION(clip);        ← once
        ///   loop: _SET_MOVE_CANSEL();  YIELD;  JMP loop      ← every frame after
        ///
        /// The cancel is repeated every frame because something else keeps setting movement: a hit runs the species' own
        /// reaction script (label 110) and that sets movement, and here the AI itself takes it straight back. One argument
        /// to _SET_MOTION selects the handler's KEY-rate form, so the clip plays at its own native speed. A HOLD loops
        /// (flags 0, the 2-argument form); the guard-LOWERING clip must play once and stop, so it goes through the
        /// 3-argument form with flags 2 and the -1.0 speed sentinel, which means "the clip's own rate". Issued with flags 0
        /// it looped over and over until the restore snapped it away. <paramref name="at"/> is where the program sits
        /// (guest), <paramref name="origin"/> what the script's jump operands are relative to.</summary>
        private static byte[] LoopSeq(int motion, uint at, uint origin, bool once)
        {
            var recs = new List<(uint op, uint a, uint v)>
            {
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel),   // 0
                ((uint)StbVm.OpExt,   1, 0),                                               // 1
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMotion),       // 2
                ((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)motion),                  // 3
            };
            if (once)
            {
                recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeFloat, StbVm.MotionSpeedKeyBits));
                recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeInt,   (uint)StbVm.MotionFlagsOnce));
            }
            recs.Add(((uint)StbVm.OpExt, (uint)(once ? 4 : 2), 0));                        // argc counts the command id
            int loop = recs.Count;                                                         // HoldLoop / LowerLoop
            recs.Add(((uint)StbVm.OpPush3, (uint)StbVm.TypeInt, (uint)StbVm.FnSetMoveCancel));
            recs.Add(((uint)StbVm.OpExt,   1, 0));
            recs.Add(((uint)StbVm.OpYield, 0, 0));
            recs.Add(((uint)StbVm.OpJmp,   at + (uint)(loop * StbVm.InstrSize) - origin, 0));
            System.Diagnostics.Debug.Assert(loop == (once ? LowerLoop : HoldLoop));
            return Pack(recs);
        }

        /// <summary>The flash's own hit reaction: stop, play the species' damage clip, wait out its stagger, then return —
        /// whereupon the AI label brings the guard up. Plain YIELDs rather than a timer, because the script is the clock
        /// here: one per engine frame.</summary>
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
            return Pack(recs);
        }

        // ── the species ──────────────────────────────────────────────────────────────────

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

        /// <summary>Does the instruction here decode as one? The VM dispatches opcodes 1..30; a label whose head is
        /// anything else is not code we understand, and is skipped rather than jumped from.</summary>
        private static bool LooksLikeCode(long at)
        {
            int op = Memory.ReadInt(at);
            return op >= 1 && op <= 30;
        }

        /// <summary>That slot's species damage clip, or -1.</summary>
        private static int DamageOf(int slot)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return -1;
            return EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g) ? g.Damage : -1;
        }

        /// <summary>A label's code address, its CodeBase-relative entry offset, and how many bytes sit before the next
        /// label's code begins.</summary>
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

        private static bool HoldClipOf(int slot, out int motion, out int back, out EnemyGuardMotions.Guard g)
        {
            motion = -1; back = -1; g = default;
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return false;
            if (!EnemyGuardMotions.TryGet(def.TableIndex.Value, out g)) return false;
            motion = g.HasGuard ? g.Loop : g.Idle;
            back = g.HasGuard && g.HasReturn ? g.Return : -1;
            return true;
        }
    }
}
