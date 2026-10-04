using System;
using System.Threading;
using static Dark_Cloud_Improved_Version.AngelGear;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Enemy melee on the slingshot: a frame-rate thread (<see cref="HitWatch"/>, started with the loop) scans the
    /// collision pool while the shield is solid for an open, player-hurting MELEE sphere overlapping the copy's volume,
    /// consumes it as a guarded hit does (the engine's own hit-mark at the contact point, the guard clink) and raises
    /// <see cref="_hitFlag"/> for the loop to take a shield hit. A SHOT's impact sphere landing on the copy is swallowed,
    /// never a hit. One of the <see cref="AngelGear"/> classes, which share their members through using static.</summary>
    internal static class MeleeHitWatch
    {
        private const string Tag = "[AngelGear/MeleeHitWatch] ";

        // MELEE HIT ON THE SLINGSHOT: enemy swings are CCollisionData spheres in the
        // NowColData pool, planted by CMonstorUnit::CheckDmg (0x1D9F10) only during an attack's damage
        // window (mask +0x48 bit 1 = hurts the player, owner +0x58 = slot*5+200). The player is hit
        // when CheckHitUser (0x1B5920) finds an open entry (+0x70 == +0x74) whose horizontal distance
        // ≤ its radius and whose vertical band overlaps hers. A guarded hit (BtCheckDamageProc, dun
        // 0x1DBAFD0) then CONSUMES the entry, plants a hit-mark (MyHitPointMark @0x1EC4940, 16 × 0x20:
        // pos vec4, +0x10 life 0x10, +0x14 timer 0, +0x18 active 1 — entry 0 for guards) and plays
        // SndSePlay(0xA2), the guard clink. The slingshot does the same at frame rate against its own
        // volume, then dispels and cools down.
        // Set's 2nd arg → entry +0x38: 0 for MELEE/contact planters (CheckDmg, player swings, bombs, status
        // powders) and 1.0f for PROJECTILE/effect planters (CSHOT_EFFECT impacts, MACHINGUN, FIREBAR, thrown
        // items) — the discriminator that keeps a shot detonating near the slingshot from reading as a swing.
        private const int    ColColIdx = 0x60;
        // RULE: melee is melee — any melee-class player-hurting sphere on the slingshot
        // dispels it. Shots are shots — a pool shot whose BODY reaches the slingshot is caught there and
        // re-fired (ShotReflect), and a shot's impact sphere landing on it is swallowed, never a hit.
        private const long   HitMarkPool = 0x21EC4940;
        private const int    HitMarkLife = 0x10;
        private const ushort GuardClinkSe = 0xA2;
        internal const float PropHitRadius = 6f;                                             // the copy's volume about its root
        private const float  PropHitHeight = 14f, PropHitBelow = 2f;
        private const int    HitWatchMs = 16;

        private static Thread _hitThread;
        internal static volatile bool _hitFlag;                  // set by the hit watch, consumed by the loop

        /// <summary>Start the watch thread once (it lives for the app, idling while no shield stands).</summary>
        internal static void StartWatch()
        {
            if (_hitThread == null || !_hitThread.IsAlive)
            { _hitThread = new Thread(HitWatch) { IsBackground = true, Name = "SlingshotHitWatch" }; _hitThread.Start(); }
        }

        /// <summary>Frame-rate scan of the melee pool while the shield is solid: an open, player-hurting
        /// sphere overlapping the slingshot's volume is a HIT — consume the entry (it has spent itself
        /// on the shield, exactly as a guarded hit does), plant the engine's own hit-mark at the sphere,
        /// play the guard clink, and flag the loop to dispel.</summary>
        private static void HitWatch()
        {
            var hm = new byte[0x20];
            while (true)
            {
                int sleep = HitWatchMs;
                try
                {
                    bool quiet = _dispelling;                       // fading out: still eat the swing that broke it, silently
                    if (!SlingshotProp.Shield || (_alpha < 1f && !quiet)) { Thread.Sleep(50); continue; }
                    long pool = Memory.ReadInt(CollisionPool.Pointer);
                    if (pool <= 0) { Thread.Sleep(50); continue; }
                    pool += 0x20000000;
                    if (!SlingshotProp.RootWorld(out float rx, out float rh, out float ry)) { Thread.Sleep(50); continue; }
                    byte[] flags = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
                    if (flags == null) { Thread.Sleep(50); continue; }
                    for (int i = 0; i < CollisionPool.Entries; i++)
                    {
                        if (BitConverter.ToInt32(flags, i * 4) == 0) continue;
                        byte[] e = Memory.ReadBytesBatch(pool + i * CollisionPool.Stride, CollisionPool.Stride);
                        if (e == null) continue;
                        if ((BitConverter.ToUInt32(e, CollisionPool.Mask) & CollisionPool.HurtsPlayerMask) == 0) continue;
                        if (BitConverter.ToInt32(e, CollisionPool.GateA) != BitConverter.ToInt32(e, CollisionPool.GateB)) continue;
                        float ex = BitConverter.ToSingle(e, 0), eh = BitConverter.ToSingle(e, 4), ey = BitConverter.ToSingle(e, 8);
                        float r  = BitConverter.ToSingle(e, CollisionPool.Radius);
                        float dx = ex - rx, dy = ey - ry;
                        if (dx * dx + dy * dy > (r + PropHitRadius) * (r + PropHitRadius)) continue;
                        if (eh + r < rh - PropHitBelow || eh - r > rh + PropHitHeight) continue;

                        Memory.WriteInt(pool + CollisionPool.ActiveOff + i * 4, 0);            // spent itself on the shield
                        if (BitConverter.ToSingle(e, CollisionPool.EntryClass) != 0f)
                        {
                            // A SHOT's impact sphere landing on the slingshot: swallowed, never a hit.
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"swallowed a shot impact on the slingshot (entry {i}, r={r:F1})");
                            continue;
                        }
                        if (quiet) continue;                                        // no second spark/clink while dispelling
                        Array.Clear(hm, 0, hm.Length);
                        Array.Copy(e, 0, hm, 0, 16);                                // the sphere's centre …
                        float cdx = rx - ex, cdh = rh - eh, cdy = ry - ey, cl = (float)Math.Sqrt(cdx * cdx + cdh * cdh + cdy * cdy);
                        if (cl > 1e-3f)                                             // … moved to its surface toward the prop: the contact point
                        {
                            BitConverter.GetBytes(ex + cdx / cl * r).CopyTo(hm, 0);
                            BitConverter.GetBytes(eh + cdh / cl * r).CopyTo(hm, 4);
                            BitConverter.GetBytes(ey + cdy / cl * r).CopyTo(hm, 8);
                        }
                        BitConverter.GetBytes(HitMarkLife).CopyTo(hm, 0x10);
                        BitConverter.GetBytes(0).CopyTo(hm, 0x14);
                        BitConverter.GetBytes(1).CopyTo(hm, 0x18);                  // active last
                        Memory.WriteBytesBatch(HitMarkPool, hm);
                        SeSeq.Play(GuardClinkSe, 30);
                        int owner = BitConverter.ToInt32(e, CollisionPool.Owner), col = BitConverter.ToInt32(e, ColColIdx);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"melee hit on the slingshot: entry {i} col {col}, r={r:F1}, owner {(owner >= 200 ? "slot " + (owner - 200) / 5 : owner.ToString())}");
                        _hitFlag = true;
                        sleep = 200;                                                // debounce: one hit is enough
                        break;
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "hit watch failed: " + e.Message); sleep = 500; }
                Thread.Sleep(sleep);
            }
        }
    }
}
