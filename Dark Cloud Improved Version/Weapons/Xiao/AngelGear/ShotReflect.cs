using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.AngelGear;
using static Dark_Cloud_Improved_Version.ReflectedHits;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The intercept and the re-fire: every enemy shot closing on Xiao inside the guard radius is claimed (latched
    /// harmless and snapshotted, <see cref="ClaimClosingShots"/>), kept harmless and ended quietly in the pouch or on her body
    /// (<see cref="SustainClaims"/>), and spawned again from the pouch at the nearest living enemy with the engine's own
    /// pure-data Set replica (<see cref="FirePending"/>). Also where the copy should face (<see cref="ChooseBearing"/>) and
    /// where the pouch is (<see cref="GetPouch"/>). One of the <see cref="AngelGear"/> classes, which share their members
    /// through using static.</summary>
    internal static class ShotReflect
    {
        private const string Tag = "[AngelGear/ShotReflect] ";

        private const float ClaimRadius  = 100f;   // claim a closing shot inside this range of Xiao
        private const float CaptureRadius = 7f;    // a faced shot this close to the pouch is caught (+ 2 ticks of travel)
        private const float HomingRange  = 40f;    // faced shot is homed into the pouch from this range
        private const float HomingGain   = 0.35f;  // per-tick blend of its direction toward the pouch
        private const float EngineCatchNear = 20f;      // a claimed shot that died within this of the pouch was caught there
        private const float ReturnBoost  = 1.6f;   // fresh shot speed = absorbed arrival speed × this
        private const int   PendingMax   = 6;
        internal const byte LatchHold    = 0x7F;
        private const int   FreshTimers  = 240;    // wait/life given to a fresh shot (4 s of flight)
        private const float AbsorbNear   = 8f;     // quiet-kill a claimed shot inside this range of her body
        private const float ClearMin     = 4f;     // fresh shots leave at least this far down the aim from the pouch
        private const float PastHer      = 10f;    // ...and, when the aim runs back through her, this far past her body

        // Dull white absorb flash on Xiao (half-sine, 0.25 s — see unitAmbientAnime notes).
        private const float AbsorbWhite = 90f, AbsorbFlashFrames = 15f;

        // Targeting: nearest living enemy (lock-on selection deliberately ignored), aimed EXACTLY where
        // Xiao's own pellets go — the enemy's lock-on frame world position (FloorSlots LockOnPoint,
        // engine-refreshed every draw; setTargetCursor copies it to the aim global 0x1DC4500), or its
        // origin raised 8 when the species set no lock-on frame.

        /// <summary>A claimed enemy shot: its pack slot and sub-shot, and the snapshot a re-fire is built from.</summary>
        internal sealed class Claim
        {
            public int    Slot, Idx;
            public float  Speed;                   // arrival speed (units/frame)
            public float  RetX, RetH, RetY;        // unit return line (back where it came from)
            public int    Damage;
            public ushort Owner, Attr2;
            public byte   SndFlag, Reload;
            public float  LastX, LastH, LastY;         // where it was last seen (to judge an engine-side death)
            public int    LastWait;                    // its flight countdown when last seen: 0 = it ran out, not a contact
        }

        internal static readonly List<Claim> _claimed = new();   // in flight toward Xiao (latched)
        internal static readonly List<Claim> _pending = new();   // absorbed, awaiting re-fire

        // ───────────────────────────── absorb (claim + let it land) ─────────────────────────────

        /// <summary>Every live in-flight shot inside the guard radius, closing on Xiao and with the flight left to
        /// reach the pouch (its countdown × speed against the distance, the pouch standing PropAhead out toward it) is
        /// claimed: latched harmless and SNAPSHOTTED (species slot, speed, damage, owner fields, arrival line), then
        /// left to fly — the engine's contact-kill on her IS the absorb. A shot that would expire short is left alone.</summary>
        internal static void ClaimClosingShots(long pack, float xx, float xh, float xy)
        {
            for (int s = 0; s < ShotEffectPack.PackSlots; s++)
            {
                long inst = pack + s * ShotEffectPack.SlotStride;
                int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
                if (count < 1 || count > ShotEffectPack.SubShots) continue;
                for (int i = 0; i < count; i++)
                {
                    if (IsClaimed(s, i)) continue;
                    if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) continue;
                    if (Memory.ReadUShort(inst + ShotEffectPack.OffPhase + i * 2) != 1) continue;
                    long obj = inst + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride;
                    float dx = xx - Memory.ReadFloat(obj + ShotEffectPack.ObjPos);
                    float dh = xh - Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4);
                    float dy = xy - Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                    if (dx * dx + dh * dh + dy * dy > ClaimRadius * ClaimRadius) continue;
                    long dirA = inst + ShotEffectPack.OffDir + i * 0x10;
                    float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                    if (vx * dx + vh * dh + vy * dy <= 0f) continue;

                    float speed = Math.Max(1f, (float)Math.Sqrt(vx * vx + vh * vh + vy * vy));
                    int wait = Memory.ReadInt(inst + ShotEffectPack.OffWait + i * 4);
                    float need = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy) - PropAhead - CaptureRadius;
                    if (wait * speed < need) continue;                                         // out of range: it dies before the pouch
                    float inv = -1f / speed;
                    Memory.WriteByte(inst + ShotEffectPack.OffLatch + i, LatchHold);
                    _claimed.Add(new Claim
                    {
                        Slot = s, Idx = i, Speed = speed,
                        RetX = vx * inv, RetH = vh * inv, RetY = vy * inv,
                        Damage  = Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4),
                        Owner   = Memory.ReadUShort(inst + ShotEffectPack.OffOwner + i * 2),
                        Attr2   = Memory.ReadUShort(inst + ShotEffectPack.OffAttr2 + i * 2),
                        SndFlag = Memory.ReadByte(inst + ShotEffectPack.OffSndFlag + i),
                        Reload  = Memory.ReadByte(inst + ShotEffectPack.OffReload + i),
                    });
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"claimed shot slot{s}#{i} speed={speed:F2}/frame dmg={Memory.ReadInt(inst + ShotEffectPack.OffDamage + i * 4)}");
                }
            }
        }

        private static bool IsClaimed(int s, int i)
        {
            foreach (var c in _claimed) if (c.Slot == s && c.Idx == i) return true;
            return false;
        }

        /// <summary>Keep every claimed shot harmless and end each one QUIETLY ourselves (active
        /// flag cleared — no impact animation): the shot the copy is FACING is homed gently into the
        /// pouch and caught there; any other vanishes into her body with the dull white flash. Either
        /// way it queues for re-fire. (Two/three ticks of travel pad the catch/kill ranges so a fast
        /// shot can't slip through between 50 ms ticks and detonate on her.)</summary>
        internal static void SustainClaims(long pack, bool shieldUp, float xx, float xh, float xy,
                                           float px, float ph, float py, Claim faced)
        {
            if (!shieldUp)
            {
                // Shield down (broken, cooling, folding, or guard released): every claimed shot goes
                // LIVE again at once (latch → 0; left alone it would stay harmless for its 127-frame
                // countdown) and nothing waits to be fired.
                if (_claimed.Count > 0 || _pending.Count > 0)
                {
                    foreach (var c in _claimed)
                        Memory.WriteByte(pack + c.Slot * ShotEffectPack.SlotStride + ShotEffectPack.OffLatch + c.Idx, 0);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"shield down — released {_claimed.Count} claimed, dropped {_pending.Count} pending");
                    _claimed.Clear(); _pending.Clear();
                }
                return;
            }
            bool armed = true, solid = SlingshotProp.Shield && _alpha >= 1f;
            for (int q = _claimed.Count - 1; q >= 0; q--)
            {
                var c = _claimed[q];
                long inst = pack + c.Slot * ShotEffectPack.SlotStride;
                bool gone = Memory.ReadUShort(inst + ShotEffectPack.OffActive + c.Idx * 2) == 0, caught = false, byEngine = gone;
                if (!gone)
                {
                    long obj = inst + ShotEffectPack.OffObj + c.Idx * ShotEffectPack.ObjStride;
                    float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sh = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 4), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                    c.LastX = sx; c.LastH = sh; c.LastY = sy; c.LastWait = Memory.ReadInt(inst + ShotEffectPack.OffWait + c.Idx * 4);
                    float bx = xx - sx, bh = xh - sh, by = xy - sy;          // shot → her body
                    float qx = px - sx, qh = ph - sh, qy = py - sy;          // shot → the pouch
                    float dq = qx * qx + qh * qh + qy * qy;
                    float catchR = CaptureRadius + c.Speed * 2f, kill = AbsorbNear + c.Speed * 3f;
                    if (solid && dq < catchR * catchR)
                    {
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + c.Idx * 2, 0);   // into the pouch
                        gone = true; caught = true;
                    }
                    else if (bx * bx + bh * bh + by * by < kill * kill
                             || Memory.ReadUShort(inst + ShotEffectPack.OffPhase + c.Idx * 2) > 1)
                    {
                        Memory.WriteUShort(inst + ShotEffectPack.OffActive + c.Idx * 2, 0);   // quiet vanish into her
                        gone = true;
                    }
                    else if (solid && c == faced && dq < HomingRange * HomingRange)
                    {
                        long dirA = inst + ShotEffectPack.OffDir + c.Idx * 0x10;
                        float vx = Memory.ReadFloat(dirA), vh = Memory.ReadFloat(dirA + 4), vy = Memory.ReadFloat(dirA + 8);
                        float vl = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy), ql = (float)Math.Sqrt(dq);
                        if (vl > 1e-3f && ql > 1e-3f)
                        {
                            float nx = vx / vl * (1f - HomingGain) + qx / ql * HomingGain;
                            float nh = vh / vl * (1f - HomingGain) + qh / ql * HomingGain;
                            float ny = vy / vl * (1f - HomingGain) + qy / ql * HomingGain;
                            float nl = (float)Math.Sqrt(nx * nx + nh * nh + ny * ny);
                            if (nl > 1e-3f)
                            {
                                WriteVec(dirA, nx / nl * vl, nh / nl * vl, ny / nl * vl);   // speed kept
                                ShotEffects.FaceAlong(obj, nx, nh, ny);
                            }
                        }
                    }
                }
                if (gone)
                {
                    _claimed.RemoveAt(q);
                    string how = caught ? "caught in the pouch" : "absorbed into her";
                    if (byEngine)
                    {
                        // The engine ended it (it hit "the player" — i.e. the POUCH while redirected — a wall,
                        // or expired): judge by where we last saw it.
                        float qx = px - c.LastX, qh = ph - c.LastH, qy = py - c.LastY;
                        float ex = xx - c.LastX, eh = xh - c.LastH, ey = xy - c.LastY;
                        if (c.LastWait <= 1)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot{c.Slot}#{c.Idx} ran out of flight short of the pouch — not re-fired");
                            continue;
                        }
                        if (qx * qx + qh * qh + qy * qy < EngineCatchNear * EngineCatchNear) { caught = true; how = "caught at the pouch (engine)"; }
                        else if (ex * ex + eh * eh + ey * ey < (AbsorbNear + c.Speed * 3f) * (AbsorbNear + c.Speed * 3f)) how = "ended on her";
                        else
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lost slot{c.Slot}#{c.Idx} (wall/expired) — not re-fired");
                            continue;
                        }
                    }
                    if (armed && _pending.Count < PendingMax)
                    {
                        _pending.Add(c);
                        if (!caught) Player.FlashActiveCharacter(AbsorbWhite, AbsorbWhite, AbsorbWhite, AbsorbFlashFrames, 1);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{how}: slot{c.Slot}#{c.Idx} (pending {_pending.Count})");
                    }
                    continue;
                }
                Memory.WriteByte(inst + ShotEffectPack.OffLatch + c.Idx, LatchHold);   // harmless to the end
            }
        }

        /// <summary>Where the copy should face — a world bearing in her convention (atan2(dx, dy),
        /// forward = (sin, cos)). Fire target while a cycle runs or a caught shot waits with a clear
        /// sky; else the nearest closing shot (also returned, for homing); else the nearest enemy;
        /// else her own facing. False = nothing to face, hold the current bearing.</summary>
        internal static bool ChooseBearing(long pack, float xx, float xh, float xy, float yaw, out float want, out Claim faced)
        {
            want = yaw; faced = null;
            if (_pullTick >= 0 || (_pending.Count > 0 && _claimed.Count == 0))
            {
                if (PickTarget(xx, xh, xy, out float ex, out _, out float ey))
                { want = (float)Math.Atan2(ex - xx, ey - xy); return true; }
                return false;
            }
            float best = float.MaxValue;
            foreach (var c in _claimed)
            {
                long obj = pack + c.Slot * ShotEffectPack.SlotStride + ShotEffectPack.OffObj + c.Idx * ShotEffectPack.ObjStride;
                float sx = Memory.ReadFloat(obj + ShotEffectPack.ObjPos), sy = Memory.ReadFloat(obj + ShotEffectPack.ObjPos + 8);
                float d = (sx - xx) * (sx - xx) + (sy - xy) * (sy - xy);
                if (d < best) { best = d; faced = c; want = (float)Math.Atan2(sx - xx, sy - xy); }
            }
            if (faced != null) return true;
            if (PickTarget(xx, xh, xy, out float nx, out _, out float ny))
            { want = (float)Math.Atan2(nx - xx, ny - xy); return true; }
            return true;                                                // nothing around: straight ahead
        }

        /// <summary>The pouch in world space: the copy's pouch bone once it has been drawn, else the
        /// analytic copy root (her position + the orbit offset).</summary>
        internal static void GetPouch(float xx, float xh, float xy, float yaw, out float px, out float ph, out float py)
        {
            if (SlingshotProp.Shield && SlingshotProp.PouchWorld(out px, out ph, out py)) return;
            float b = yaw + SlingshotProp.Orbit;
            px = xx + (float)Math.Sin(b) * PropAhead; ph = xh + PouchHeight; py = xy + (float)Math.Cos(b) * PropAhead;
        }

        // ─────────────────────────────── re-fire from the pouch ────────────────────────────────

        /// <summary>Spawn a FRESH shot of the absorbed species from the pouch at the targeted enemy —
        /// the same pure-data spawn Set performs for muzzle-less shots (phase 1 direct), reusing the
        /// free sub-shot's own frame objects. Latched harmless (its mask is the enemy-shot one, so the engine never
        /// collides it with monsters); <see cref="ReflectedHits"/> follows it and plants its damage on contact.</summary>
        internal static void FirePending(long pack, float xx, float xh, float xy, float px, float ph, float py)
        {
            if (_pending.Count == 0) return;
            var c = _pending[0];
            long inst = pack + c.Slot * ShotEffectPack.SlotStride;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) { _pending.RemoveAt(0); return; }

            int j = -1;                                             // a free sub-shot to inhabit
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return;                                      // all busy — retry next tick

            long cfg = Memory.ReadInt(inst + ShotEffectPack.OffCfg);
            if (cfg <= 0) { _pending.RemoveAt(0); return; }
            cfg += 0x20000000;
            long obj  = inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride;
            long dirA = inst + ShotEffectPack.OffDir + j * 0x10;

            // Leave from the fork's MUZZLE (the copy's eff30 — where her own pellets spawn), else the pouch.
            float poX = px, poH = ph, poY = py;
            if (SlingshotProp.MuzzleWorld(out float mx, out float mh, out float my)) { poX = mx; poH = mh; poY = my; }

            // Aim: the nearest living enemy (lock-on ignored) —
            // at pellet height above its feet (her vanilla shots fly flat at body height, not into
            // the ground) — else the absorbed arrival line reversed.
            float ax = c.RetX, ah = c.RetH, ay = c.RetY;
            if (PickTarget(poX, poH, poY, out float ex, out float eh, out float ey))
            {
                ax = ex - poX; ah = eh - poH; ay = ey - poY;
                float al = (float)Math.Sqrt(ax * ax + ah * ah + ay * ay);
                if (al < 1f) { ax = c.RetX; ah = c.RetH; ay = c.RetY; }
                else { ax /= al; ah /= al; ay /= al; }
            }
            float v = c.Speed * ReturnBoost;

            // Spawn CLEAR of her contact zone — a fresh shot born inside it dies on her at once (the
            // same contact-kill the absorb uses). The pouch is out in front, so a short lead is
            // normally enough; when the aim runs back through her (target behind), the shot leaves
            // from just past her body instead.
            float tx = xx - poX, th = xh - poH, ty = xy - poY;
            float along = tx * ax + th * ah + ty * ay;
            float perp2 = tx * tx + th * th + ty * ty - along * along;
            float lead = along > 0f && perp2 < (AbsorbNear + 4f) * (AbsorbNear + 4f) ? along + PastHer : ClearMin;
            float spX = poX + ax * lead, spH = poH + ah * lead, spY = poY + ay * lead;

            // ── the Set replica (fields in Set's own order; active LAST) ──
            int flyMot = (short)Memory.ReadUShort(cfg + 0x4E);
            long ftab = Memory.ReadInt(obj + ShotEffectPack.ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + flyMot * 0x10) : 1;
            Memory.WriteUShort(inst + ShotEffectPack.OffPhase + j * 2, 1);          // flying, no muzzle
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos,     spX);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 4, spH);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 8, spY);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 12, 1f);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotId,  flyMot);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotFlag, 4);
            Memory.WriteFloat (obj + ShotEffectPack.ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ShotEffectPack.ObjFrame,  startFrame);
            WriteVec(dirA, ax * v, ah * v, ay * v);
            Memory.WriteInt   (inst + ShotEffectPack.OffWait + j * 4, FreshTimers);
            Memory.WriteInt   (inst + ShotEffectPack.OffDamage + j * 4, c.Damage);
            Memory.WriteInt   (inst + ShotEffectPack.OffUserCol + j * 4, -1);
            Memory.WriteUShort(inst + ShotEffectPack.OffOwner + j * 2, c.Owner);
            Memory.WriteUShort(inst + ShotEffectPack.OffAttr2 + j * 2, c.Attr2);
            Memory.WriteUShort(inst + ShotEffectPack.OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + ShotEffectPack.OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffA110 + j * 4, -1);
            Memory.WriteByte  (inst + ShotEffectPack.OffSndFlag + j, c.SndFlag);
            Memory.WriteByte  (inst + ShotEffectPack.OffReload + j, c.Reload);
            Memory.WriteByte  (inst + ShotEffectPack.OffLatch + j, LatchHold);      // harmless: ReflectedHits plants its damage
            Memory.WriteInt   (inst + ShotEffectPack.OffLastIdx, j);
            ShotEffects.FaceAlong(obj, ax, ah, ay);
            Memory.WriteUShort(inst + ShotEffectPack.OffActive + j * 2, 1);         // live — engine steps it from here
            _flying.Add(new Fired { Slot = c.Slot, Idx = j, Flags = Memory.ReadUInt(cfg + ShotEffectPack.CfgFlags), Radius = Math.Max(0.5f, Memory.ReadFloat(cfg + ShotEffectPack.CfgRadiusFlying)) });

            _pending.RemoveAt(0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"re-fired slot{c.Slot}#{j} at " + (ah == c.RetH && ax == c.RetX ? "return line" : "nearest enemy"));
        }

        /// <summary>Nearest living enemy's AIM POINT: its lock-on frame's world position when the
        /// species set one (what Xiao's own shots fly at), else its origin raised by 8.</summary>
        private static bool PickTarget(float px, float ph, float py, out float ex, out float eh, out float ey)
        {
            ex = eh = ey = 0f;
            float best = float.MaxValue;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float cx = Memory.ReadFloat(p), ch = Memory.ReadFloat(p + 4), cy = Memory.ReadFloat(p + 8);
                if (cx == 0f && ch == 0f && cy == 0f) continue;
                float d = (cx - px) * (cx - px) + (cy - py) * (cy - py);
                if (d >= best) continue;
                best = d;
                ex = cx; eh = ch + EnemySlotOffsets.LockOnFallbackLift; ey = cy;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LockOnFrame)) != 0)
                {
                    long lp = EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LockOnPoint);
                    float lx = Memory.ReadFloat(lp), lh = Memory.ReadFloat(lp + 4), ly = Memory.ReadFloat(lp + 8);
                    if (!(lx == 0f && lh == 0f && ly == 0f) && Math.Abs(lh - ch) < 200f) { ex = lx; eh = lh; ey = ly; }
                }
            }
            return best < float.MaxValue;
        }

        private static void WriteVec(long addr, float a, float b, float c)
        {
            Memory.WriteFloat(addr, a);
            Memory.WriteFloat(addr + 4, b);
            Memory.WriteFloat(addr + 8, c);
        }
    }
}
