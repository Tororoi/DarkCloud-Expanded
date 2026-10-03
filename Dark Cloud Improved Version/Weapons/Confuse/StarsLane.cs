using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The RESIDENT stars instance (docs/confuse-ability.md, Phase 3): a CSHOT_EFFECT of the mod's own, carved from the monster pool
    /// once per floor, constructed by the stars step cave (CodeCaves.StarsConstruct), entered with the spinning stars
    /// (ConfusionStars.Effect, 8 sub-shots) through the ISO's loader cave (ElfCave.BorrowedShotsEnter) by borrowing BorrowedShots'
    /// request block, and stepped and drawn every dungeon frame by the second-effect caves' continuation
    /// (ElfConfusePatches.StarsTail) behind CodeCaves.StarsGate. Leaving the floor closes the gate.
    /// </summary>
    internal static class StarsLane
    {
        private const string Tag = "[StarsLane] ";
        internal const int  SubShots = 8;
        private const int   Reserve = 4096;              // units: the stars take ~2,450 at 6 sub-shots
        private const double SlowAnswerSeconds = 10.0; // logged once if the cave takes longer than this (it is never interrupted)
        private const double RetrySeconds = 3.0;
        private const int    MaxTries = 3;            // a floor's attempts (each after a −1, "no room") before it goes without stars
        private const int    InstanceUnits = ShotEffectPack.SlotStride / 16;   // the CSHOT_EFFECT in the pool's 16-byte units
        private const int    CCharacterVtable = 0xA0;   // __ct__10CCharacter writes its vtable here

        private static bool _live, _requesting, _slowLogged;
        private static int _tries;
        private static DateTime _retryAt;
        private static int _floor = -1;
        private static long _instance;                // this floor's carved instance (0 = none yet)
        private static bool _constructed;             // the step cave has run its constructor
        private static byte[] _region;                // the stars' region (the block's allocator, 16 B) and its mark, once carved: re-entries reuse it
        private static int _regionMark;
        private static byte[] _written = new byte[0x10];   // the allocator words the pending request was written with
        private static long _texEntry;                // the stars' texture entry in the texture manager (0 = not found)
        private const double QuietSeconds = 0.5;      // the dungeon plain and unheld this long before a request goes in
        private const long DriveStepHold = 0x202A3564, FrameCapture = 0x202A3568;   // set at a menu's opening (the quick character select: BtMiniChrSelect_Loop sled 0)
        private static DateTime _quietSince;
        private const int  TexBlock = 0x10;           // the texture block the cave enters every instance into (the main effect's)
        private static readonly byte[] TexName = System.Text.Encoding.ASCII.GetBytes("e114ex\0");
        private static byte[] _snapshot;
        private static DateTime _since;
        private static readonly object _lock = new();

        /// <summary>The instance the stars run in, once entered on this floor (0 = not yet).</summary>
        internal static long Instance => _live ? _instance : 0;
        /// <summary>The config the stars run from (what BorrowedShots.BurstIn wants).</summary>
        internal static byte[] Cfg { get; private set; }

        /// <summary>The floor was left: the gate closed (nothing more is stepped or drawn from the old region), the stars down.</summary>
        internal static void Leave()
        {
            lock (_lock)
            {
                if (_requesting) Abort("left the floor");
                if (_live || Memory.ReadInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive) != 0) Memory.WriteInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive, 0);
                Memory.WriteUInt(CodeCaves.StarsConstruct, 0);                                      // a pending construct withdrawn: its memory is the next floor's
                _live = false; _floor = -1; _tries = 0; _retryAt = default; _instance = 0; _constructed = false;
                _region = null; _texEntry = 0;
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
                        Memory.WriteUInt(CodeCaves.StarsConstruct, 0);
                        _live = false; _tries = 0; _retryAt = default; _floor = floor; _instance = 0; _constructed = false;
                        _region = null; _texEntry = 0;
                        ConfusionStars.StopAll();
                    }
                    if (_requesting) return Progress();
                    if (_live && TexturesLost())
                    {   // a main-effect entry cleared texture block 0x10 — the stars' textures with it: entered again (same instance, same region)
                        Memory.WriteInt(CodeCaves.StarsGate + CodeCaves.StarsGateLive, 0);
                        _live = false; _tries = 0; _retryAt = default; _texEntry = 0;
                        ConfusionStars.StopAll();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the stars' textures were cleared by a main-effect entry — entering them again");
                    }
                    if (_live || _tries >= MaxTries || GameClock.Now < _retryAt) return false;
                    // Only after the dungeon has played plainly for QuietSeconds: the cave reads into the loader's read buffer, which
                    // the menus load into too (docs/confuse-ability.md).
                    if (!Quiet()) { _quietSince = default; return false; }
                    if (_quietSince == default) _quietSince = GameClock.Now;
                    if ((GameClock.Now - _quietSince).TotalSeconds < QuietSeconds) return false;
                    // Never while another request is in flight (the cave reads the block across frames).
                    if (Memory.ReadUInt(CodeCaves.BorrowedShotBlock) == CodeCaves.BorrowedShotMagic && Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState) == 0) return false;
                    if (!Constructed()) return false;                                                   // carved between requests: the cave is the pool's other mid-floor allocator
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
            // The floor's first entry names no region (the cave carves one); a re-entry names the stars' own, which the cave reuses
            // while its signature and mark hold.
            _written = _region ?? new byte[0x10];
            Memory.WriteBytesBatch(b + CodeCaves.BorrowedShotAlloc, _written);
            Memory.WriteInt(b + CodeCaves.BorrowedShotCarveMark, _region != null ? _regionMark : 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotReserve, Reserve);
            Memory.WriteInt(b + CodeCaves.BorrowedShotInstance, (int)(_instance - 0x20000000L));
            Memory.WriteInt(b + CodeCaves.BorrowedShotMainFlag, 0);
            Memory.WriteInt(b + CodeCaves.BorrowedShotSubShots, SubShots);
            Memory.WriteInt(b + CodeCaves.BorrowedShotState, 0);
            if (!Quiet()) { RestoreBlock(); _quietSince = default; return; }                      // a menu began opening meanwhile
            Memory.WriteInt(b, (int)CodeCaves.BorrowedShotMagic);                                  // last: the cave enters it on its next frame
            _requesting = true; _since = GameClock.Now; _tries++; _slowLogged = false;
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
                _region = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc, 0x10);
                _regionMark = mark;
                RestoreBlock();                                                                     // the block names its own instance again first
                Memory.WriteBytesBatch(CodeCaves.StarsCfg, cfg);
                Memory.WriteUInt(_instance + ShotEffectPack.OffCfg, (uint)(CodeCaves.StarsCfg - 0x20000000L));   // its own config copy
                Memory.WriteUInt(CodeCaves.StarsGate + CodeCaves.StarsGateInstance, (uint)(_instance - 0x20000000L));
                Memory.WriteUInt(CodeCaves.StarsGate + CodeCaves.StarsGateBase, allocBase);
                Memory.WriteInt (CodeCaves.StarsGate + CodeCaves.StarsGateMark, mark);
                Memory.WriteInt (CodeCaves.StarsGate + CodeCaves.StarsGateLive, 1);                // last: stepped and drawn from the next frame
                _requesting = false; _live = true;
                _texEntry = FindTexEntry();
                if (_texEntry == 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the stars' texture entry was not found — a later main-effect entry will not be noticed");
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"stars entered in the resident instance ({SubShots} sub-shots, region {used:N0} of {Reserve:N0} units)");
                return false;
            }
            if (state < 0)
            {   // the cave answered "no room": it has let go of the block, so it is safe to put back
                bool last = _tries >= MaxTries;
                Abort("the cave could not enter the stars (no room in the monster pool?)" + (last ? $" — {MaxTries} tries, no stars on this floor" : $" — trying again in {RetrySeconds:F0} s (try {_tries} of {MaxTries})"));
                _retryAt = GameClock.Now.AddSeconds(RetrySeconds);
                return false;
            }
            // No answer yet and a menu is opening: withdrawn while the cave has not begun on it (docs/confuse-ability.md), asked
            // again once the dungeon is quiet.
            if (!Quiet() && !CaveStarted())
            {
                Abort("a menu opened before the cave answered — withdrawn, asked again later");
                _tries--; _quietSince = default;
                return false;
            }
            // Otherwise keep waiting — never interrupted (only a floor change abandons it).
            if (!_slowLogged && (GameClock.Now - _since).TotalSeconds > SlowAnswerSeconds)
            { _slowLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the cave has not answered in {SlowAnswerSeconds:F0} s — still waiting"); }
            return true;
        }

        /// <summary>Whether the cave has begun on the request: it carves (the allocator's base filled in) or reuses the region (its
        /// used count zeroed) before anything else — from then on the block must not change until it answers.</summary>
        private static bool CaveStarted()
        {
            byte[] a = Memory.ReadBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc, 0x10);
            return a == null || !a.AsSpan().SequenceEqual(_written);
        }

        /// <summary>The dungeon walking, unpaused, no menu open or opening (no held step, no frame capture).</summary>
        private static bool Quiet()
            => Player.CheckDunIsWalkingMode() && !Player.CheckDunIsPausedOrMenu()
               && Memory.ReadInt(DriveStepHold) == 0 && Memory.ReadInt(FrameCapture) == 0;

        /// <summary>The stars' texture entry (named e114ex, in block 0x10), or 0.</summary>
        private static long FindTexEntry()
        {
            for (int i = 1; i < TextureManager.MaxEntries; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                if (Memory.ReadUShort(e + TextureManager.EntryBlock) == TexBlock && NameIs(e)) return e;
            }
            return 0;
        }

        private static bool NameIs(long e)
        {
            byte[] n = Memory.ReadBytesBatch(e + TextureManager.EntryName, TexName.Length);
            return n != null && n.AsSpan().SequenceEqual(TexName);
        }

        /// <summary>The stars' texture entry gone (DeleteTextureBlock(0x10) clears it when the cave enters the main effect).</summary>
        private static bool TexturesLost()
            => _texEntry != 0 && (Memory.ReadUShort(_texEntry + TextureManager.EntryBlock) != TexBlock || !NameIs(_texEntry));

        /// <summary>This floor's instance carved and constructed: carved and posted to the stars step cave on the first call, then
        /// true once the cave has cleared the word (its vtables in place — checked).</summary>
        private static bool Constructed()
        {
            if (_constructed) return true;
            if (_instance == 0)
            {
                if (!CarveInstance()) { _tries = MaxTries; return false; }
                Memory.WriteUInt(CodeCaves.StarsConstruct, (uint)(_instance - 0x20000000L));
                return false;
            }
            if (Memory.ReadUInt(CodeCaves.StarsConstruct) != 0) return false;                      // not stepped yet
            uint vt = Memory.ReadUInt(_instance + ShotEffectPack.OffObj + CCharacterVtable);       // the last-built sub-object's
            if (vt == 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the constructor left no vtable — no stars on this floor");
                _tries = MaxTries; return false;
            }
            _constructed = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"instance constructed (vtable 0x{vt:X})");
            return true;
        }

        /// <summary>This floor's instance, carved from the top of the monster pool (zeroed; the pool's used counter bumped past
        /// it), only between requests (docs/confuse-ability.md). False (no stars on this floor) when the instance and the stars'
        /// region would not both fit.</summary>
        private static bool CarveInstance()
        {
            long pool = DataPools.Monstor;
            int used = Memory.ReadInt(pool + DataPools.Used), cap = Memory.ReadInt(pool + DataPools.Cap);
            uint poolBase = Memory.ReadUInt(pool);
            if (poolBase == 0 || used < 0 || used + InstanceUnits + Reserve + 1 > cap)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no room for the stars in the monster pool ({used:N0} of {cap:N0} units used) — no stars on this floor");
                return false;
            }
            long inst = 0x20000000L + poolBase + (long)used * 16;
            Memory.WriteBytesBatch(inst, new byte[ShotEffectPack.SlotStride]);
            Memory.WriteInt(pool + DataPools.Used, used + InstanceUnits);
            _instance = inst;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"instance carved at 0x{inst - 0x20000000L:X} (monster pool {used + InstanceUnits:N0} of {cap:N0} units)");
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
