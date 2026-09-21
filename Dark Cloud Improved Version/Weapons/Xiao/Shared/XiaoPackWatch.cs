using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>What Xiao's dungeon pack carries beyond her own model, watched whenever she is the character and whatever weapon she
    /// holds, so it is no weapon's thread: the cat pack's cape cloth — built for HER on every model load and hung on her cloth list,
    /// where it would draw at her feet; taken off the list the moment it appears and kept as the cat's clone template
    /// (<see cref="CatCape.TakeHerCape"/>) — and the character heap the pack fills, whose overflow is a silent freeze, so its counters
    /// are logged as they change. Started once with the game.</summary>
    internal static class XiaoPackWatch
    {
        private const string Tag = "[XiaoPack] ";
        private const int XiaoId = 1;
        private const int WatchMs = 50, IdleMs = 100;
        private static Thread _thread;
        private static int _capeSweepTick;

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "XiaoPackWatch" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                int sleep = IdleMs;
                try
                {
                    if (Player.InDungeonFloor())
                    {
                        sleep = WatchMs;
                        HeapWatch();
                        if (Player.CurrentCharacterNum() == XiaoId && ++_capeSweepTick >= 4) { _capeSweepTick = 0; CatCape.TakeHerCape(); }
                    }
                }
                catch (Exception e) { Log("tick failed: " + e.Message); sleep = 1000; }
                Thread.Sleep(sleep);
            }
        }

        private static string _heapLast = "", _bgLast = "";
        /// <summary>Free bytes below which the effects pool is reported as TIGHT. An effect that cannot allocate does not
        /// warn — it simply never appears, which is what "Pirate's Chariot and Alexander fired nothing" looks like.</summary>
        private const long EffectsTightBytes = 32 * 1024;
        private static int _effectPeak, _effectCapLast, _weaponCapLast;
        private static DateTime _effectTightAt = DateTime.MinValue;
        /// <summary>Watch the character heap. Chara, weapons and effects share ONE CDataAlloc2 pool (210000 × 16 B vanilla,
        /// 265000 × 16 = 4.24 MB with DunPatches' raise) and an overflow is a SILENT spin — Alloc__14CDataAlloc2&lt;1&gt;Fi is
        /// printf + while(true), i.e. a freeze — so the counters and the background reads are logged whenever they change and
        /// the last line before a freeze is the verdict. Also tracks the effects pool's high-water mark, since enemy
        /// projectiles allocate from whatever the chara data leaves.</summary>
        internal static void HeapWatch()
        {
            int c = Memory.ReadInt(DataPools.Chara + DataPools.Used), w = Memory.ReadInt(DataPools.Weapon + DataPools.Used), e = Memory.ReadInt(DataPools.Effect + DataPools.Used);
            int cCap = Memory.ReadInt(DataPools.Chara + DataPools.Cap), wCap = Memory.ReadInt(DataPools.Weapon + DataPools.Cap), eCap = Memory.ReadInt(DataPools.Effect + DataPools.Cap);
            int poolUsed = Memory.ReadInt(DataPools.GlobalUsed);
            // ── effects-pool high-water mark ──────────────────────────────────────────────────────────────────────
            // The effects pool is only what the character heap has left after chara and weapons, so with the cat resident
            // it is a FRACTION of what Toan gets — measured 70,144/175,904 B as Xiao against 190,272/1,907,968 as Toan.
            // Enemy projectiles are effects, and the summary line below only prints when the whole string CHANGES, so a spike
            // that empties the pool and drains again leaves no trace at all; the peak does.
            if (eCap != _effectCapLast || wCap != _weaponCapLast)
            {
                _effectCapLast = eCap; _weaponCapLast = wCap; _effectPeak = 0;   // the caps move with chara: start a fresh peak
                Log(
                    $"effects pool now caps at {eCap * 16L:N0} B (weapons {wCap * 16L:N0} B) — char {Player.CurrentCharacterNum()}, cat {(DivineBeastTitle.Active ? "resident" : "down")}");
            }
            if (e > _effectPeak)
            {
                _effectPeak = e;
                long freeB = (eCap - e) * 16L;
                if (e * 2 >= eCap)                                                // only once it is worth knowing about
                    Log(
                        $"effects pool peak {e * 16L:N0} of {eCap * 16L:N0} B (free {freeB:N0})");
            }
            // Separately from the peak, and rate-limited: the pool being tight RIGHT NOW is the thing that makes an enemy
            // projectile silently not appear, and it can happen on a floor whose peak never exceeds an earlier floor's.
            if ((eCap - e) * 16L < EffectsTightBytes && (GameClock.Now - _effectTightAt).TotalSeconds >= 5)
            {
                _effectTightAt = GameClock.Now;
                Log(
                    $"effects pool TIGHT: {e * 16L:N0} of {eCap * 16L:N0} B used, only {(eCap - e) * 16L:N0} free — "
                    + "an effect that cannot allocate never appears, and enemy projectiles are effects");
            }
            string heap = $"chara {c * 16L:N0}/{cCap * 16L:N0}, weapons {w * 16L:N0}/{wCap * 16L:N0}, effects {e * 16L:N0}/{eCap * 16L:N0} — total {(c + w + e) * 16L:N0} of {cCap * 16L:N0} B (free {(cCap - c - w - e) * 16L:N0}); global pool {poolUsed * 16L:N0} of {DataPools.GlobalCap * 16L:N0} B (free {(DataPools.GlobalCap - poolUsed) * 16L:N0})";
            if (heap != _heapLast)
            {
                _heapLast = heap;
                Log($"heap (char {Player.CurrentCharacterNum()}): " + heap);
                uint cb = Memory.ReadUInt(DataPools.Chara), wb = Memory.ReadUInt(DataPools.Weapon), eb = Memory.ReadUInt(DataPools.Effect);   // the pools' base words
                Log($"pool bases: chara 0x{cb:X} used-end 0x{cb + (uint)c * 16:X} cap-end 0x{cb + (uint)cCap * 16:X} | weapons 0x{wb:X} used-end 0x{wb + (uint)w * 16:X} | effects 0x{eb:X} used-end 0x{eb + (uint)e * 16:X}");
                var pools = new System.Text.StringBuilder();
                foreach (var (addr, name) in DataPools.InCarveOrder)
                    pools.Append($" {name} {Memory.ReadInt(addr + DataPools.Used) * 16L:N0}/{Memory.ReadInt(addr + DataPools.Cap) * 16L:N0}");
                pools.Append($" cash {Memory.ReadInt(DataPools.Cash + DataPools.Used) * 16L:N0}/{Memory.ReadInt(DataPools.Cash + DataPools.Cap) * 16L:N0}");   // the floor script's work allocator (P840 on a first floor, the monster pool after)
                Log("pools (used/cap B):" + pools);
            }
            var bg = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                long ent = BgRead.Table + i * BgRead.Stride;
                if (Memory.ReadInt(ent) == 0) continue;
                byte[] nb = Memory.ReadBytesBatch(ent + BgRead.Name, 48);
                int len = 0; while (nb != null && len < nb.Length && nb[len] != 0) len++;
                string nm = nb == null ? "?" : System.Text.Encoding.ASCII.GetString(nb, 0, len);
                bg.Append($" [{i}] {nm} → 0x{Memory.ReadInt(ent + BgRead.Dest):X} ({Memory.ReadInt(ent + BgRead.Size):N0} B)");
            }
            string bgs = bg.ToString();
            if (bgs != _bgLast)
            {
                _bgLast = bgs;
                if (bgs.Length > 0) Log("bg reads:" + bgs);
            }
        }

        private static void Log(string message) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + message);
    }
}
