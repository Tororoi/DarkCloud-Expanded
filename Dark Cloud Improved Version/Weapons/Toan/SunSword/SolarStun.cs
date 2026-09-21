using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The blinded enemies. Each enemy the flash reached takes the hit's own stagger — CheckDmg runs its label-110 reaction,
    /// which requests its damage clip — and then, for the rest of <see cref="StunSeconds"/>, its AI is HELD: the slot's
    /// script PC (<see cref="CRunScript.Pc"/>) is parked on <see cref="CodeCaves.SolarYieldBlock"/>, a run of YIELD ops, and
    /// re-parked every tick, so CMonstorUnit::Step resumes a script that only waits — no _SET_MOTION, no _SET_MOVE — while the
    /// engine keeps stepping the enemy's motion as usual. Its scripted move speed is zeroed once (nothing rewrites it while
    /// the script waits) so it stops where it stands, and its facing is held on the spot Toan flashed from. Once the stagger
    /// clip has played out, its guard is requested exactly as its own script would (<see cref="Request"/>): the raise, then
    /// the hold loop, which loops on its own; a species with no guard is put in its idle. When the timer runs out the
    /// running word is cleared and Step starts label 100 afresh next frame. A death during the hold is left entirely to the
    /// engine: the moment the slot's script is label 120 the hold lets go.
    /// </summary>
    internal static class SolarStun
    {
        internal const double StunSeconds     = 5.0;    // blinded for this long after the flash
        private  const double ParkDelay       = 0.10;   // the hit reaction gets these frames to start (its first op requests the damage clip)
        private  const double StaggerMin      = 0.35;   // the guard is never requested sooner than this after the flash…
        private  const double StaggerCap      = 1.50;   // …and never later, even if the damage clip is not seen ending
        private  const double RequestRetry    = 0.25;   // a clip not seen playing is requested again after this
        internal const int    YieldOps        = 18;     // YIELDs in the block, then push 0 + RET (the PC is re-parked every tick, well within)
        private  const int    LabelAi = 100, LabelHit = 110, LabelDeath = 120;

        private sealed class Blinded
        {
            public int slot; public float fx, fy;               // where the flash came from (horizontal)
            public DateTime start, lastRequest;
            public uint fdDeath;                                // the death label's funcdata (guest): the hold lets go when the script is there
            public int phase;                                   // 0 stagger, 1 guard raise / idle, 2 guard loop
            public int reposes;
            public EnemyGuardMotions.Guard g;
            public bool haveG, parked, speedZeroed;
        }
        private static readonly List<Blinded> _list = new List<Blinded>();
        private static bool _blockWritten;

        /// <summary>True while any enemy is blinded.</summary>
        internal static bool Active => _list.Count > 0;

        /// <summary>Every live enemy within <paramref name="radius"/> of the flash point is blinded from now.</summary>
        internal static void Begin(float px, float py, float radius)
        {
            WriteYieldBlock();
            int n = 0, withGuard = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                if (Math.Sqrt((ex - px) * (ex - px) + (ey - py) * (ey - py)) > radius) continue;
                _list.RemoveAll(b => b.slot == s);
                var b = new Blinded { slot = s, fx = px, fy = py, start = GameClock.Now, fdDeath = LabelFuncData(s, LabelDeath) };
                ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
                if (EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.TableIndex.HasValue
                    && EnemyGuardMotions.TryGet(def.TableIndex.Value, out b.g)) { b.haveG = true; if (b.g.HasGuard) withGuard++; }
                _list.Add(b); n++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] flash: {n} enemies blinded ({withGuard} with a guard clip)");
        }

        internal static void Tick()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var b = _list[i];
                if (!Enemies.IsLive(b.slot)) { _list.RemoveAt(i); continue; }          // dead or gone: its script is the engine's again
                double el = (GameClock.Now - b.start).TotalSeconds;
                if (el >= StunSeconds) { End(b); _list.RemoveAt(i); continue; }
                if (el < ParkDelay) continue;                                          // the hit reaction starts first

                uint fd = Memory.ReadGuestPtr(CRunScript.SlotAddr(b.slot, CRunScript.Label));
                if (b.fdDeath != 0 && fd == b.fdDeath) { _list.RemoveAt(i); continue; }   // dying: hands off
                Park(b);
                Face(b);
                DriveMotion(b, el);
            }
        }

        /// <summary>Everything released at once (floor change, weapon put away).</summary>
        internal static void Clear()
        {
            foreach (var b in _list) if (Enemies.IsLive(b.slot)) End(b);
            _list.Clear();
        }

        /// <summary>Let the AI go: label 100 starts afresh next frame.</summary>
        private static void End(Blinded b)
        {
            if (b.parked)
            {
                Memory.WriteInt(CRunScript.SlotAddr(b.slot, CRunScript.Pc), 0);
                Memory.WriteInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(b.slot), 0);
            }
            if (b.reposes > 0)
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] slot {b.slot}: blinding over (clip re-requested {b.reposes}×)");
        }

        /// <summary>The script waits: PC on the YIELD block, running word set so Step resumes it; move speed zeroed once.</summary>
        private static void Park(Blinded b)
        {
            Memory.WriteUInt(CRunScript.SlotAddr(b.slot, CRunScript.Pc), CodeCaves.SolarYieldBlockGuest);
            Memory.WriteInt(EnemyAddresses.MainMonstorUnit.ScriptRunningAddr(b.slot), 1);
            b.parked = true;
            if (!b.speedZeroed) { Memory.WriteFloat(MoveControl.SpeedAddr(b.slot), 0f); b.speedZeroed = true; }
        }

        /// <summary>Stagger until the damage clip has played (or the cap), then the guard raise → hold loop, or the idle.</summary>
        private static void DriveMotion(Blinded b, double el)
        {
            if (!b.haveG) return;
            long model = ModelScaleOffsets.ModelBase + (long)b.slot * ModelScaleOffsets.ModelStride;
            int   playing = Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId);
            float frame   = Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame);
            var g = b.g;
            DateTime now = GameClock.Now;
            switch (b.phase)
            {
                case 0:
                {
                    if (el < StaggerMin) return;
                    bool staggerDone = el >= StaggerCap || g.Damage < 0 || playing != g.Damage || frame >= g.DamageEnd - 0.5f;
                    if (!staggerDone) return;
                    Request(b, g.HasGuard ? g.Enter : g.Idle); b.lastRequest = now; b.phase = 1;
                    return;
                }
                case 1:
                {
                    int want = g.HasGuard ? g.Enter : g.Idle;
                    if (playing == want)
                    {
                        if (g.HasGuard && g.Loop != g.Enter && frame >= g.EnterEnd - 0.5f) { Request(b, g.Loop); b.lastRequest = now; b.phase = 2; }
                        return;
                    }
                    if (g.HasGuard && playing == g.Loop) { b.phase = 2; return; }
                    Retry(b, want, now);
                    return;
                }
                default:
                    if (playing != g.Loop) Retry(b, g.Loop, now);
                    return;
            }
        }

        private static void Retry(Blinded b, int motion, DateTime now)
        {
            if ((now - b.lastRequest).TotalSeconds < RequestRetry) return;
            b.lastRequest = now; b.reposes++;
            Request(b, motion);
        }

        /// <summary>Request a motion exactly as the enemy's own _SET_MOTION (ELF 0x1E1710) does — the render object's id, flags
        /// and KEY speed for the body and every extra part, plus the request words on the slot — so the engine's motion player
        /// plays the clip from its start with its usual key-change blend.</summary>
        private static void Request(Blinded b, int motion)
        {
            long slot  = EnemyAddresses.FloorSlots.SlotAddr(b.slot, 0);
            long model = ModelScaleOffsets.ModelBase + (long)b.slot * ModelScaleOffsets.ModelStride;
            Memory.WriteInt(slot + EnemySlotOffsets.MotionRequestAux, -1);
            Memory.WriteFloat(slot + EnemySlotOffsets.AiSpeedParam, -1f);
            float speed = -1f;
            uint table = Memory.ReadGuestPtr(model + ModelScaleOffsets.MotionTablePtr);
            if (Memory.IsValidGuest(table))
                speed = Memory.ReadFloat(Memory.ToMmu(table) + (long)motion * ModelScaleOffsets.MotionTableStride + ModelScaleOffsets.MotionTableSpeed);
            if (Memory.ReadInt(slot + EnemySlotOffsets.GooeyState) > 0) speed *= 0.5f;
            int parts = Memory.ReadShort(slot + EnemySlotOffsets.PartCount);
            for (int i = 0; i <= Math.Min(parts, 8); i++)
            {
                long m = model + (long)i * ModelScaleOffsets.PartStride;
                Memory.WriteInt(m + ModelScaleOffsets.PlayingMotionId, motion);
                Memory.WriteInt(m + ModelScaleOffsets.PlayingMotionFlags, 0);
                Memory.WriteFloat(m + ModelScaleOffsets.PlayingMotionSpeed, speed);
            }
            Memory.WriteUShort(slot + EnemySlotOffsets.AiStatePacked, (ushort)motion);
            Memory.WriteUShort(slot + EnemySlotOffsets.MotionRequestFlags, 0);
            Memory.WriteFloat(slot + EnemySlotOffsets.AiSpeedParam, speed);
        }

        /// <summary>Hold the enemy's facing on the flash point.</summary>
        private static void Face(Blinded b)
        {
            float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(b.slot, EnemySlotOffsets.LocationX));
            float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(b.slot, EnemySlotOffsets.LocationY));
            float dx = b.fx - ex, dy = b.fy - ey;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1f) return;
            Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(b.slot, EnemySlotOffsets.FacingX), dx / len);
            Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(b.slot, EnemySlotOffsets.FacingZ), dy / len);
        }

        /// <summary>A label's funcdata address (guest) in the STB this slot runs, or 0.</summary>
        private static uint LabelFuncData(int slot, int label)
        {
            uint stb = Memory.ReadGuestPtr(CRunScript.StbPtrAddr(slot));
            if (!Memory.IsValidGuest(stb)) return 0;
            long s = Memory.ToMmu(stb);
            if (Memory.ReadInt(s) != StbVm.Magic) return 0;
            int tbl = Memory.ReadInt(s + StbVm.LabelTableOff), cnt = Memory.ReadInt(s + StbVm.LabelCount);
            for (int i = 0; i < cnt && i < 64; i++)
                if (Memory.ReadInt(s + tbl + i * 8) == label) return stb + (uint)Memory.ReadInt(s + tbl + i * 8 + 4);
            return 0;
        }

        /// <summary>The wait block: YieldOps × YIELD, then `push 0; RET` (never reached while a PC is re-parked every tick; if it
        /// ever is, the label just ends and Step restarts the AI).</summary>
        private static void WriteYieldBlock()
        {
            if (_blockWritten && Memory.ReadInt(CodeCaves.SolarYieldBlock) == StbVm.OpYield) return;
            var ops = new byte[(YieldOps + 2) * StbVm.InstrSize];
            for (int i = 0; i < YieldOps; i++) BitConverter.GetBytes(StbVm.OpYield).CopyTo(ops, i * StbVm.InstrSize);
            int o = YieldOps * StbVm.InstrSize;
            BitConverter.GetBytes(StbVm.OpPush3).CopyTo(ops, o); BitConverter.GetBytes(StbVm.TypeInt).CopyTo(ops, o + StbVm.OperandA);
            BitConverter.GetBytes(StbVm.OpRet).CopyTo(ops, o + StbVm.InstrSize);
            if (ops.Length > CodeCaves.SolarYieldBlockBytes) throw new InvalidOperationException("the YIELD block outgrew its slot");
            Memory.WriteBytesBatch(CodeCaves.SolarYieldBlock, ops);
            _blockWritten = true;
        }
    }
}
