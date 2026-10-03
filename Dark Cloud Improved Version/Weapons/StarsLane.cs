using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The RESIDENT stars instance (docs/confuse-ability.md): a CSHOT_EFFECT of the mod's own in main BSS `frame_info_cam`
    /// (CodeCaves.StarsInstance — nothing references it), stepped and drawn every dungeon frame by the second-effect caves'
    /// continuation (ElfWeaponPatches.StarsTail) behind CodeCaves.StarsGate, and entered with the spinning stars
    /// (ConfusionStars.Effect, 8 sub-shots) once per floor — so every confused enemy can wear them on any floor, whatever the
    /// weapons, items and character effects in use.
    ///
    /// Entering goes through the ISO's loader cave (ElfCave.BorrowedShotsEnter), which serves ONE request block, BorrowedShots':
    /// on each new floor, before that block's own effect is requested again, the block is saved, the stars' request written
    /// (the stars' config, the container, the stars instance, its own fresh region carved from the monster pool), the cave
    /// answers, and the block is put back as it was; the stars' config is copied to CodeCaves.StarsCfg and the instance pointed at
    /// it (the block's config changes under it), and the gate is opened with the region's base and mark (the step/draw caves
    /// check the region is still the floor's: its "BSHT" signature and the monster pool at or past the mark). Leaving the floor
    /// closes the gate.
    /// </summary>
    internal static class StarsLane
    {
        private const string Tag = "[StarsLane] ";
        internal const int  SubShots = 8;
        private const int   Reserve = 4096;              // units: the stars take ~2,450 at 6 sub-shots
        private const double RequestTimeout = 3.0;

        private static bool _live, _requesting, _failedThisFloor;
        private static int _floor = -1;
        private static byte[] _snapshot;
        private static DateTime _since;
        private static readonly object _lock = new();

        /// <summary>The instance the stars run in, once entered on this floor (0 = not yet).</summary>
        internal static long Instance => _live ? CodeCaves.StarsInstance : 0;
        /// <summary>The config the stars run from (what BorrowedShots.BurstIn wants).</summary>
        internal static byte[] Cfg { get; private set; }

        /// <summary>The floor was left: the gate closed (nothing more is stepped or drawn from the old region), the stars down.</summary>
        internal static void Leave()
        {
            lock (_lock)
            {
                if (_requesting) Abort("left the floor");
                if (_live || Memory.ReadInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive) != 0) Memory.WriteInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive, 0);
                _live = false; _floor = -1; _failedThisFloor = false;
            }
            ConfusionStars.StopAll();
        }

        /// <summary>BorrowedShots' loop, every pass while on a floor: true while the stars' request holds the block (the loop then
        /// touches nothing). <paramref name="floor"/> identifies the floor; a new one closes the gate and enters again.</summary>
        internal static bool Tick(int floor)
        {
            lock (_lock)
            {
                try
                {
                    if (floor != _floor)
                    {
                        if (_requesting) Abort("the floor changed");
                        Memory.WriteInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive, 0);
                        _live = false; _failedThisFloor = false; _floor = floor;
                        ConfusionStars.StopAll();
                    }
                    if (_requesting) return Progress();
                    if (_live || _failedThisFloor) return false;
                    var fx = ConfusionStars.Effect();
                    if (fx == null) return false;
                    Start(fx);
                    return _requesting;
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); return _requesting; }
            }
        }

        private static void Start(BorrowedEffect fx)
        {
            _snapshot = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock, CodeCaves.BorrowedShotBlockSize);
            if (_snapshot == null) return;
            long b = CodeCaves.BorrowedShotBlock;
            Cfg = BorrowedShots.PreparedCfg(fx);
            Memory.WriteInt(b, 0);                                                                  // quiet while the block changes
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotCfg, Cfg);
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotPath, BorrowedShots.PathBytes(fx.Path));
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotAlloc, new byte[0x10]);               // no region: the cave carves a fresh one
            Memory.WriteInt(b + CodeCaves.BorrowedShotCarveMark, 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotReserve, Reserve);
            Memory.WriteInt(b + CodeCaves.BorrowedShotInstance, (int)CodeCaves.StarsInstanceGuest);
            Memory.WriteInt(b + CodeCaves.BorrowedShotMainFlag, 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotSubShots, SubShots);
            Memory.WriteInt(b + CodeCaves.BorrowedShotState, 0);
            Memory.WriteInt(b, (int)CodeCaves.BorrowedShotMagic);                                  // last: the cave enters it on its next frame
            _requesting = true; _since = GameClock.Now;
        }

        private static bool Progress()
        {
            int state = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState);
            if (state == 1)
            {
                byte[] cfg = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg, ShotEffectPack.CfgSize);
                uint allocBase = Memory.ReadUInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc);
                int used = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc + 8);
                int mark = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCarveMark);
                RestoreBlock();                                                                     // the block names its own instance again first
                Memory.WriteBytesBatch(CodeCaves.StarsCfg, cfg);
                Memory.WriteUInt(CodeCaves.StarsInstance + ShotEffectPack.OffCfg, (uint)(CodeCaves.StarsCfg - 0x20000000L));   // its own config copy
                Memory.WriteUInt(CodeCaves.StarsGate + CodeCaves.StarsGateBase, allocBase);
                Memory.WriteInt (CodeCaves.StarsGate + CodeCaves.StarsGateMark, mark);
                Memory.WriteInt (CodeCaves.StarsGate + CodeCaves.StarsGateLive, 1);                // last: stepped and drawn from the next frame
                _requesting = false; _live = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"stars entered in the resident instance ({SubShots} sub-shots, region {used:N0} of {Reserve:N0} units)");
                return false;
            }
            if (state < 0 || (GameClock.Now - _since).TotalSeconds > RequestTimeout)
            {
                Abort(state < 0 ? "the cave could not enter the stars (no room in the monster pool?) — no stars on this floor" : $"no answer in {RequestTimeout:F0} s — no stars on this floor");
                _failedThisFloor = true;
                return false;
            }
            return true;
        }

        private static void Abort(string why)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + why);
            RestoreBlock();
            _requesting = false;
        }

        /// <summary>The block as it was before the stars borrowed it, its magic word last.</summary>
        private static void RestoreBlock()
        {
            if (_snapshot == null) return;
            long b = CodeCaves.BorrowedShotBlock;
            Memory.WriteInt(b, 0);
            Memory.WriteBytesBatch(b + 4, _snapshot.AsSpan(4).ToArray());
            Memory.WriteInt(b, BitConverter.ToInt32(_snapshot, 0));
            _snapshot = null;
        }
    }
}
