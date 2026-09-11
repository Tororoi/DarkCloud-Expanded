using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Divine Beast Title — the CHARGED shot launches Xiao's cat form (roadmap PR 8).
    ///
    /// Hold the shot past <see cref="ChargeSeconds"/> (the game's own charge flash announces it) and the pellet
    /// that leaves the slingshot grows into an animated cat: the copy is pinned to the LIVE pellet — its head rides
    /// the pellet's point every tick — and scales up from nothing over <see cref="GrowSeconds"/> while the pellet's
    /// own sprite shrinks away, so the pellet itself appears to become the cat. The pellet keeps flying and
    /// colliding exactly as the game runs it; the cat only follows, in its fall pose, and fades when the pellet
    /// ends. (The landing → run → pounce chain that follows is parked while the flight is tuned — user 2026-09-10.)
    ///
    /// Where the cat comes from: the ISO bake (tools/iso_patch/build_cat_pack.py) grafts the s86 cat rig INTO
    /// Xiao's dungeon character pack c04b.chr as 37 extra `cat_` nodes that sit UNPARENTED in her frame array (the
    /// engine never draws, skins or DMAs them — a parented-but-hidden cat froze the menus, whose draw buffers are
    /// smaller than the dungeon's) plus a second motion channel (KEY_START 64: stand, ready, run, take-off, leap, land)
    /// that only ever plays on the copy. So whenever Xiao is in a dungeon the cat's mesh, textures and clips are
    /// already resident in her tree, and summoning it is the same recipe as the Angel Gear slingshot copy and the
    /// Mirage clone: deep-copy the subtree into the clone caves, give the copy its own skin buffers and motion
    /// channel, host it in a dungeon chara slot, and let the engine step and draw it natively. Her own model is
    /// never touched. (The weapon pack was tried first and rejected: the weapon menu rebuilds every carried weapon
    /// into a 944 KB arena and a bigger weapon pack overflowed it.)
    /// </summary>
    internal static class DivineBeastCat
    {
        internal static bool Enabled = true;
        internal static bool Active { get; private set; }

        private const string Tag = "[DivineBeastCat] ";
        private const int  XiaoId = 1;
        private const int  Slot   = 1;                 // DungeonCharaDraw host (0 = Mirage clone, 3 = Angel Gear slingshot)
        private const int  CharCopySize = 0xD60, MotionStructSize = 0xC0;
        private const int  TickMs = 16, IdleMs = 100;

        // The cat's motion channel on HER (build_cat_pack.py: MOTION 1 in c04b.chr's base.cfg, KEY_START 64; track
        // bone ids are relative to the cat root, which is the copy's node 0).
        private const int  CatChannel = 1;
        private const int  MaxTreeNodes = 160;         // her array: 79 body + 37 cat (+ headroom for the scan)
        private const int  KeyBase = 64, KeyCount = 6;
        private const int  KeyStand = 64, KeyReady = 65, KeyRun = 66, KeyTakeOff = 67, KeyLeap = 68, KeyLand = 69;
        private const string CatRootName = "catroot";
        private const float  HideScale   = 0.001f;     // must match build_cat_pack.py HIDE_SCALE
        private const int    ChanKeyStart = 0x3E0;     // CCharacter: channel[i] first key id (CommandKEY_START)
        private const int    ChanKeyEnd   = 0x400;     // CCharacter: channel[i] key id end (CommandMOTION_END)
        private const int    MotListHead  = 0x04;      // MOTION_TYPE +0x04 → the .mot track list (shared, read-only)
        private const int    ShadowModel  = 0xC0;      // CCharacter: shadow rig root (CommandSHADOW_MODEL); 0 = no shadow
        private const int    ShadowSlotBase = 0xC40;   // CCharacter: shadow channel[i] ptr (ShadowStep__10CCharacter)
        // CNPCharacter tail (Initialize__12CNPCharacter 0x1569E0 / ClearSeq 0x156350 / SetFootSound / SetEvent)
        private const int    FootTable = 0xD60, FootStride = 0x14, FootSlots = 6;      // frame < 0 = free
        private const int    EventTable = 0xDE8, EventStride = 0x14, EventSlots = 32;  // frame < 0 = free
        private const int    SeqEnable = 0x11B0;       // ClearSeq sets 1 (+0x11B4/8 = 0, 8 × 0x50 entries @+0x11C0 = 0)
        private const int    SeqWord1490 = 0x1490;     // Initialize sets -1

        // Charge + launch.
        private const double ChargeSeconds = 1.0;      // hold this long → the shot is the cat (1.5 → 1.0, user 2026-09-10)
        private const double GrowSeconds   = 0.1;      // the pellet grows into the cat over this long after it is fired
        private const int    GrowFrames    = 6;        // the same, in frames, for the native follower (60 fps)
        private const float  Gravity       = 0.05f;    // units/frame² — the pounce arc
        private const float  FallGravity   = 0.08f;    // units/frame² — the fall off the pellet's line at full size (cave)
        // The land clip is s86 c04cat motion 7, frames 215..227 (KEY 69 in build_cat_pack.py keeps the absolute frames):
        // the paws first touch the ground at 219 — that is where the forward momentum stops; at 227 the run begins.
        private const float  LandStopFrame = 219f, LandEndFrame = 227f;
        private const float  LandClipStart = 215f, LandClipSpeed = 0.36f;   // KEY 69 in build_cat_pack.py (frames/frame)
        // The clip lowers the cat itself (hips 5.5 → 4.6 over 215..219), so it must start this many frames BEFORE the
        // physical touchdown for the paws to meet the floor at 219; the cave predicts the touchdown from the fall.
        private const float  LandLeadFrames = (LandStopFrame - LandClipStart) / LandClipSpeed;
        private const float  CatScale      = 1.0f;
        // Ground game.
        private const float  RunSpeed      = 1.3f;     // units/frame
        private const float  PounceRange   = 24f;      // start the pounce within this of the target
        private const float  PounceFrames  = 40f;      // leap flight time (frames)
        private const float  HitRadius     = 9f;       // planted hit sphere
        private const double LandSeconds   = 0.45, TakeOffSeconds = 0.4, RunTimeoutSeconds = 6.0, StraightRunSeconds = 1.5;
        private const int    FadeTicks     = 18;
        private const float  DamageMult    = 1.5f;     // × the weapon's attack (a charged pellet's worth)
        private const int    PlantedLifeTicks = 2;

        // Hit-entry plumbing (CCollisionData pool, as GuardianReflector.PlantReflectedHit).
        private const long NowColDataPtr = 0x202A35E0;
        private const int  ColEntries = 96, ColStride = 0xA0, ColActiveOff = 0x3C00;
        private const long BattleWeaponAttack = WeaponHave.BattleWeaponRecord + 0x04;
        private const long BattleWeaponStats  = WeaponHave.BattleWeaponRecord + 0x1C;
        private const long BattleWeaponFlags  = WeaponHave.BattleWeaponRecord + 0xEE;

        private enum Phase { Resident, Flying, Falling, Landing, Running, TakeOff, Leaping, LandEnd, Fading }   // Resident = built, hidden, waiting

        private static Thread _thread;
        private static readonly bool[] _seenPellet = new bool[PlayerShotPool.SlotCount];
        private static bool   _holding, _flashed;
        private static DateTime _holdStart;
        private static double _holdSeconds;

        // The copy.
        private static uint  _liveRoot, _copyRoot;
        private static int   _nodeCount, _catIndex;
        private static int   _key = -1;
        private static float _alpha = 1f;
        // Flight state (world: x, h = height, y).
        private static Phase _phase;
        private static DateTime _phaseStart;
        private static float _x, _h, _y, _yaw, _vx, _vh, _vy, _floor, _dirX, _dirY;
        private static float _scale = 1f;                    // growth 0 → 1 over GrowSeconds (× CatScale)
        private static long  _pool;                          // the shot pool the cat is pinned to
        private static int   _pelletSlot = -1;               // its pellet's slot while that pellet lives, else −1
        private static bool  _native;                        // the ISO carries the pellet catcher/follower cave
        private static bool  _nativeWarned;
        private static bool  _caveOwns;                      // cave armed (waiting) or following: slot 1's pos/scale/opacity are its
        private static int   _disarmTicks;                   // after a shot-less release: ticks until the waiting cave is disarmed
        private static DateTime _spawnFailedAt = DateTime.MinValue;
        private static float _flightFrame0;                  // copy's motion frame at the bind (fall-pose check in the log)
        private static int   _target = -1;
        private static float _px, _ph, _py;                 // the flight POINT (where the pellet would be) — the head rides it
        private static float _headX, _headH, _headZ;        // head rest offset in cat space (FindHead)
        private const string HeadNodeName = "cat_kao";
        private const float  HeadFallbackHeight = 6f;
        private static bool  _hitDone;
        private static int   _fade;
        private static readonly List<(int idx, int ticks)> _planted = new();

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "DivineBeastCat" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                int sleep = IdleMs;
                try
                {
                    bool inDun = Player.InDungeonFloor();
                    if (inDun) { HeapWatch(); sleep = WatchMs; }
                    bool armed = Enabled && inDun && Player.CurrentCharacterNum() == XiaoId
                              && Memory.ReadUShort(WeaponHave.BattleWeaponRecord) == Items.divinebeasttitle
                              && !Player.CheckDunIsPausedOrMenu();
                    if (!armed)
                    {
                        if (Active) Despawn();
                        _holding = false; _holdSeconds = 0;
                        Array.Clear(_seenPellet, 0, _seenPellet.Length);
                    }
                    else
                    {
                        sleep = TickMs;
                        WatchHerCatChannel();
                        if (!Active) SpawnResident();                          // built once, hidden, ready for the next charge
                        TrackCharge();
                        if (_native) PollCave(); else WatchPellets();
                        if (Active) Step();
                    }
                    RetirePlanted();
                }
                catch (Exception e)
                {
                    Console.WriteLine(Tag + "tick failed: " + e.Message);
                    try { if (Active) Despawn(); } catch { }
                }
                Thread.Sleep(sleep);
            }
        }

        // ─────────────────────────────────────── character heap watch ──────────────────────────────────────
        // The dungeon's character/weapon/effect data share ONE CDataAlloc2 pool of 210000 × 16 B vanilla (240000
        // with DunPatches' heap raise; LoadChara2: chara @0x1F06660, weapons @0x1F06670, effects @0x1F06680; the
        // chara cap @+12 is the pool size, the others are whatever the earlier ones left). An
        // overflow is a SILENT spin (Alloc__14CDataAlloc2<1>Fi: printf + while(true)) — i.e. a freeze. Xiao only
        // ever loads through the party switch, so the watch runs on every floor for every character and logs the
        // counters and the background reads whenever they change: the last line before a freeze is the verdict.
        private const long HeapChara = 0x21F06660, HeapWeapon = 0x21F06670, HeapEffect = 0x21F06680;
        private const int  WatchMs = 50;
        // The 27 MB global buffer every pool is carved from (GlobalDataBuffer @0x2AB080, cap 0x19C98F units; its
        // bump counter sits at the array's end). Raising the character heap (DunPatches) must leave room here.
        private const long GlobalPoolUsed = 0x21C74980;
        private const int  GlobalPoolCap  = 0x19C98F;
        // Every CDataAlloc2 GameInit (dun 0x1DAC1C0) carves from it, in carve order — used @+8, cap @+12 (units).
        private static readonly (long addr, string name)[] Pools =
        {
            (0x21F06640, "common"), (0x202AB020, "motion"), (0x21F06660, "chara"), (0x21F06690, "shotfx"),
            (0x202AB030, "texture"), (0x21F06870, "p870"), (0x21F066A0, "p6a0"), (0x21F066B0, "p6b0"),
            (0x21F066C0, "p6c0"), (0x21F06840, "p840"), (0x21F066D0, "monstor"), (0x21F06650, "map"),
        };
        private const long BgReadInfo = 0x21CBB0C0;          // bg_read_info[32], stride 0x9C: +0 active, +0xC name, +0x8C dest, +0x90 size
        private static string _heapLast = "", _bgLast = "";
        private static void HeapWatch()
        {
            int c = Memory.ReadInt(HeapChara + 8), w = Memory.ReadInt(HeapWeapon + 8), e = Memory.ReadInt(HeapEffect + 8);
            int cCap = Memory.ReadInt(HeapChara + 12), wCap = Memory.ReadInt(HeapWeapon + 12), eCap = Memory.ReadInt(HeapEffect + 12);
            int poolUsed = Memory.ReadInt(GlobalPoolUsed);
            string heap = $"chara {c * 16L:N0}/{cCap * 16L:N0}, weapons {w * 16L:N0}/{wCap * 16L:N0}, effects {e * 16L:N0}/{eCap * 16L:N0} — total {(c + w + e) * 16L:N0} of {cCap * 16L:N0} B (free {(cCap - c - w - e) * 16L:N0}); global pool {poolUsed * 16L:N0} of {GlobalPoolCap * 16L:N0} B (free {(GlobalPoolCap - poolUsed) * 16L:N0})";
            if (heap != _heapLast)
            {
                _heapLast = heap;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"heap (char {Player.CurrentCharacterNum()}): " + heap);
                var pools = new System.Text.StringBuilder();
                foreach (var (addr, name) in Pools)
                    pools.Append($" {name} {Memory.ReadInt(addr + 8) * 16L:N0}/{Memory.ReadInt(addr + 12) * 16L:N0}");
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "pools (used/cap B):" + pools);
            }
            var bg = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                long ent = BgReadInfo + i * 0x9C;
                if (Memory.ReadInt(ent) == 0) continue;
                byte[] nb = Memory.ReadBytesBatch(ent + 0xC, 48);
                int len = 0; while (nb != null && len < nb.Length && nb[len] != 0) len++;
                string nm = nb == null ? "?" : System.Text.Encoding.ASCII.GetString(nb, 0, len);
                bg.Append($" [{i}] {nm} → 0x{Memory.ReadInt(ent + 0x8C):X} ({Memory.ReadInt(ent + 0x90):N0} B)");
            }
            string bgs = bg.ToString();
            if (bgs != _bgLast)
            {
                _bgLast = bgs;
                if (bgs.Length > 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "bg reads:" + bgs);
            }
        }

        // ───────────────────────────────────────── charge + launch ─────────────────────────────────────────

        /// <summary>Hold time on the draw/nocked states → the shot is charged past <see cref="ChargeSeconds"/>;
        /// the game's own charge-complete pulse marks it. Frozen on release so the fired pellet reads it.</summary>
        private static void TrackCharge()
        {
            int state = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = state == PlayerAction.XiaoShotDraw || state == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!_holding) { _holding = true; _holdStart = DateTime.UtcNow; _flashed = false; }
                _holdSeconds = (DateTime.UtcNow - _holdStart).TotalSeconds;
                if (_holdSeconds >= ChargeSeconds && !_flashed)
                {
                    Player.FlashChargeComplete(); _flashed = true;
                    if (_native && Active) ArmCave();                          // the cave catches the release's pellet on its birth frame
                }
            }
            else
            {
                if (_holding && _caveOwns && _phase != Phase.Flying) _disarmTicks = 30;  // released: ~0.5 s for the shoot motion to spawn the pellet, else the charge was cancelled
                _holding = false;
            }
        }

        /// <summary>A pellet that appears while the shot was charged becomes the cat: read where it was born
        /// (the muzzle) and which way it flies, expire it, and launch the cat there.</summary>
        private static void WatchPellets()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seenPellet[i])
                {
                    _seenPellet[i] = true;
                    if (_holdSeconds >= ChargeSeconds && Active)
                    {
                        _holdSeconds = 0;                                        // one cat per charge
                        Bind(pool, i);                                           // (thread fallback: unpatched ISO)
                    }
                }
                else if (!live) _seenPellet[i] = false;
            }
        }

        /// <summary>Build the copy once per floor/character and keep it RESIDENT but hidden (opacity 0, scale 0, the
        /// fall pose looping) so a charged shot has nothing left to build — the cave shows it on the pellet's birth
        /// frame (user 2026-09-10: the growth must start the very frame the pellet is created). A failed spawn is
        /// retried after a pause rather than every tick.</summary>
        private static void SpawnResident()
        {
            if (_spawnFailedAt != DateTime.MinValue && (DateTime.UtcNow - _spawnFailedAt).TotalSeconds < 5) return;
            _scale = 0f; _alpha = 0f; _target = -1; _pelletSlot = -1; _caveOwns = false; _disarmTicks = 0;
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            _x = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos);
            _h = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
            _y = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            if (!Spawn()) { _spawnFailedAt = DateTime.UtcNow; return; }
            _spawnFailedAt = DateTime.MinValue;
            _native = (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew;
            if (!_native && !_nativeWarned) { _nativeWarned = true; Console.WriteLine(Tag + "pellet-catcher cave not in this ISO (re-patch) — using the thread follower"); }
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }
            _phase = Phase.Resident; _phaseStart = DateTime.UtcNow; _hitDone = false; _fade = 0;
            SetKey(KeyLeap);
            Maintain();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "cat resident (hidden) — " + (_native ? "native catcher" : "thread follower"));
        }

        /// <summary>At the charge threshold: give the cave the growth reciprocal and the head rest offset (cat space ×
        /// scale), zero its counters, hide the copy (scale 0 — the cave owns position/scale/opacity from here) and set
        /// state 3: the next NEW pellet binds on its birth frame. Arming while a previous cat still rides its pellet
        /// clears it (the new charged shot always starts clean — user 2026-09-10).</summary>
        private static void ArmCave()
        {
            Memory.WriteInt  (CodeCaves.Mailbox.CatPelletSlot, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatState, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGrowInv, 1f / GrowFrames);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadX, CatScale * _headX);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadH, CatScale * _headH);
            Memory.WriteFloat(CodeCaves.Mailbox.CatHeadZ, CatScale * _headZ);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGrowFrames, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGrowN, GrowFrames);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGravity, FallGravity);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloorH, Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4));
            Memory.WriteInt  (CodeCaves.Mailbox.CatTargetPtr, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandStopFrame, LandStopFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandEndFrame, LandEndFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandLead, LandLeadFrames);
            long sl = SlotAddr();
            Memory.WriteFloat(sl + CCharacter.CharScale, 0f); Memory.WriteFloat(sl + CCharacter.CharScale + 4, 0f); Memory.WriteFloat(sl + CCharacter.CharScale + 8, 0f);
            Memory.WriteFloat(sl + CCharacter.NpcOpacity, 0f);
            _scale = 0f; _alpha = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = true; _disarmTicks = 0;
            _phase = Phase.Resident; _phaseStart = DateTime.UtcNow;
            Memory.WriteInt  (CodeCaves.Mailbox.CatState, 3);                   // waiting — armed
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charge complete — the cave binds the next pellet on its birth frame");
        }

        private static void DisarmCave()
        {
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }
            _caveOwns = false; _disarmTicks = 0;
        }

        /// <summary>Follow the cave's state: 1 = it bound a pellet (note it, face along the pellet, log), 2 = that
        /// pellet ended (hold the last placed spot, fade, then hide again). A shot-less release disarms it.</summary>
        private static void PollCave()
        {
            if (!Active) return;
            int state = Memory.ReadInt(CodeCaves.Mailbox.CatState);
            if (_disarmTicks > 0 && --_disarmTicks == 0 && state == 3) { DisarmCave(); Hide(); Console.WriteLine(Tag + "charge released without a shot — cat stays hidden"); return; }
            switch (state)
            {
                case 1:
                    if (_phase != Phase.Flying)
                    {
                        long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                        int slot = Memory.ReadInt(CodeCaves.Mailbox.CatPelletSlot) - 1;
                        if (!Memory.IsValidGuest(pool) || slot < 0) break;
                        _pool = pool; _pelletSlot = slot; _alpha = 1f; _fade = 0; _caveOwns = true; _disarmTicks = 0;
                        long va = PlayerShotPool.VelAddr(pool, slot);
                        FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
                        _target = LockedTarget();
                        _floor = _target >= 0 ? Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(_target) + 4)
                                              : Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
                        _phase = Phase.Flying; _phaseStart = DateTime.UtcNow; _hitDone = false;
                        _flightFrame0 = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame);
                        Memory.WriteFloat(CodeCaves.Mailbox.CatFloorH, _floor);
                        Memory.WriteInt  (CodeCaves.Mailbox.CatTargetPtr, _target >= 0 ? (int)(EnemyAddresses.CharObjects.PosAddr(_target) & Memory.PhysAddrMask) : 0);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"cat bound to pellet slot {slot} on its birth frame" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + $" (motion frame {_flightFrame0:F1})");
                    }
                    break;
                case 4:                                                          // broke away at full size: falling
                    if (_phase != Phase.Falling)
                    {
                        _phase = Phase.Falling; _phaseStart = DateTime.UtcNow;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"full size after {Memory.ReadInt(CodeCaves.Mailbox.CatGrowFrames)} frames — off the pellet's line, v=({Memory.ReadFloat(CodeCaves.Mailbox.CatVx):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVh):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVz):F2})/frame, run speed {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F2}");
                    }
                    break;
                case 5:                                                          // land clip started (ahead of touchdown); the cave stops momentum at paw contact and runs at clip end
                    if (_phase != Phase.Landing)
                    {
                        _phase = Phase.Landing; _phaseStart = DateTime.UtcNow;
                        long lp = SlotAddr() + CCharacter.CharPos;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"land clip started at ({Memory.ReadFloat(lp):F1},{Memory.ReadFloat(lp + 4):F1},{Memory.ReadFloat(lp + 8):F1}), {Memory.ReadFloat(lp + 4) - Memory.ReadFloat(CodeCaves.Mailbox.CatFloorH):F2} above the floor, motion frame {Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame):F1}");
                    }
                    break;
                case 6:                                                          // running (cave moves it); face along its direction, decide the end
                {
                    if (_phase != Phase.Running)
                    {
                        _phase = Phase.Running; _phaseStart = DateTime.UtcNow;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"land clip done (frame {Memory.ReadFloat(CodeCaves.Mailbox.CatPrevFrame):F1}) — running at {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F2}/frame");
                    }
                    float dx = Memory.ReadFloat(CodeCaves.Mailbox.CatDirX), dz = Memory.ReadFloat(CodeCaves.Mailbox.CatDirZ);
                    if (dx * dx + dz * dz > 1e-6f) { _dirX = dx; _dirY = dz; _yaw = (float)Math.Atan2(dx, dz); }
                    long rp = SlotAddr() + CCharacter.CharPos;
                    _x = Memory.ReadFloat(rp); _h = Memory.ReadFloat(rp + 4); _y = Memory.ReadFloat(rp + 8);
                    double rt = (DateTime.UtcNow - _phaseStart).TotalSeconds;
                    float dist = float.MaxValue;
                    if (_target >= 0 && !IsLiveEnemy(_target)) { _target = -1; Memory.WriteInt(CodeCaves.Mailbox.CatTargetPtr, 0); }
                    if (_target >= 0)
                    {
                        long tp = EnemyAddresses.CharObjects.PosAddr(_target);
                        float ex = Memory.ReadFloat(tp) - _x, ey = Memory.ReadFloat(tp + 8) - _y; dist = (float)Math.Sqrt(ex * ex + ey * ey);
                    }
                    bool reached = _target >= 0 ? dist <= PounceRange : rt >= StraightRunSeconds;
                    if (reached || rt >= RunTimeoutSeconds)                     // (the pounce chain is still parked: fade here)
                    {
                        Memory.WriteInt(CodeCaves.Mailbox.CatState, 0);
                        _scale = 1f; _caveOwns = false;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (reached ? "run reached its mark" : "run timed out") + " — fading");
                        Enter(Phase.Fading, KeyStand);
                    }
                    break;
                }
                case 2:
                {
                    long sp = SlotAddr() + CCharacter.CharPos;                       // hold the last placed spot through the fade
                    _x = Memory.ReadFloat(sp); _h = Memory.ReadFloat(sp + 4); _y = Memory.ReadFloat(sp + 8);
                    int frames = Memory.ReadInt(CodeCaves.Mailbox.CatGrowFrames);
                    float mf = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame);
                    _scale = 1f; _pelletSlot = -1;
                    Memory.WriteInt(CodeCaves.Mailbox.CatState, 0);
                    _caveOwns = false;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet ended after {frames} frames; motion frame {_flightFrame0:F1} → {mf:F1}");
                    Enter(Phase.Fading, KeyLeap);
                    break;
                }
            }
        }

        /// <summary>Thread fallback (unpatched ISO): pin the resident copy to the new charged pellet from here on.</summary>
        private static void Bind(long pool, int slot)
        {
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            _px = Memory.ReadFloat(pa); _ph = Memory.ReadFloat(pa + 4); _py = Memory.ReadFloat(pa + 8);
            FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
            _target = LockedTarget();
            _floor = _target >= 0 ? Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(_target) + 4)
                                  : Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
            _pool = pool; _pelletSlot = slot; _scale = 0f; _alpha = 1f; _fade = 0;
            PlaceRootUnderHead();
            _phase = Phase.Flying; _phaseStart = DateTime.UtcNow; _hitDone = false;
            Maintain();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"cat pinned to pellet slot {slot} from ({_px:F1},{_ph:F1},{_py:F1})" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + " [thread follower]");
        }

        /// <summary>Back to resident: invisible, scale 0, fall pose looping, ready for the next charge.</summary>
        private static void Hide()
        {
            _alpha = 0f; _scale = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = false;
            _phase = Phase.Resident; _phaseStart = DateTime.UtcNow;
            SetKey(KeyLeap);
            Maintain();
        }

        /// <summary>Yaw the cat along a horizontal direction; a pellet going straight up or down keeps her facing.</summary>
        private static void FaceAlong(float vx, float vy)
        {
            float hl = (float)Math.Sqrt(vx * vx + vy * vy);
            if (hl < 1e-3f)
            {
                float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
                vx = (float)Math.Sin(yaw); vy = (float)Math.Cos(yaw); hl = 1f;
            }
            _dirX = vx / hl; _dirY = vy / hl;
            _yaw = (float)Math.Atan2(_dirX, _dirY);
        }

        /// <summary>The locked-on enemy slot, if it is still alive.</summary>
        private static int LockedTarget()
        {
            int s = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (s < 0 || s >= EnemyAddresses.FloorSlots.Count) return -1;
            return IsLiveEnemy(s) ? s : -1;
        }

        private static bool IsLiveEnemy(int s)
        {
            int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
            if (id == 0 || id == 0xFFFF) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }

        // ───────────────────────────────────────────── flight ──────────────────────────────────────────────

        private static void Step()
        {
            double t = (DateTime.UtcNow - _phaseStart).TotalSeconds;
            if (_target >= 0 && !IsLiveEnemy(_target)) _target = -1;
            float tx = 0, th = 0, ty = 0;
            if (_target >= 0)
            {
                long p = EnemyAddresses.CharObjects.PosAddr(_target);
                tx = Memory.ReadFloat(p); th = Memory.ReadFloat(p + 4); ty = Memory.ReadFloat(p + 8);
            }
            switch (_phase)
            {
                case Phase.Resident:
                    break;                                                       // hidden; the cave (or Bind) wakes it
                case Phase.Flying:
                {
                    if (_native) break;                                          // the cave owns position/scale; PollCave handles the end
                    // Thread follower (unpatched ISO): the head sits on the pellet's point, the body grows in behind it.
                    if (Memory.ReadInt(PlayerShotPool.FlagAddr(_pool, _pelletSlot)) == 0)
                    {
                        _pelletSlot = -1;                                        // the pellet ended (hit, wall or lifetime)
                        Enter(Phase.Fading, KeyLeap);                            // landing / run / pounce parked while the flight is tuned
                        break;
                    }
                    long pa = PlayerShotPool.PosAddr(_pool, _pelletSlot), va = PlayerShotPool.VelAddr(_pool, _pelletSlot);
                    _px = Memory.ReadFloat(pa); _ph = Memory.ReadFloat(pa + 4); _py = Memory.ReadFloat(pa + 8);
                    FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
                    _scale = (float)Math.Min(1.0, t / GrowSeconds);
                    Memory.WriteFloat(PlayerShotPool.ScaleAddr(_pool, _pelletSlot), 1f - _scale);   // sprite only; the hitbox is untouched
                    PlaceRootUnderHead();
                    break;
                }
                case Phase.Falling:
                    break;                                                       // the cave flies it; PollCave sees the landing
                case Phase.Landing:
                    if (_native) break;                                          // the cave runs the clip and hands over to the run at its end
                    if (t >= LandSeconds) Enter(Phase.Running, KeyRun);
                    break;
                case Phase.Running:
                {
                    if (_native) break;                                          // the cave runs it; PollCave ends the run
                    float dx = _dirX, dy = _dirY, dist = float.MaxValue;
                    if (_target >= 0)
                    {
                        dx = tx - _x; dy = ty - _y; dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist > 1e-3f) { dx /= dist; dy /= dist; _dirX = dx; _dirY = dy; }
                        _floor = th;
                    }
                    _yaw = (float)Math.Atan2(dx, dy);
                    _x += dx * RunSpeed; _y += dy * RunSpeed; _h = _floor;
                    bool go = _target >= 0 ? dist <= PounceRange : t >= StraightRunSeconds;
                    if (go) Enter(Phase.TakeOff, KeyTakeOff);
                    else if (t >= RunTimeoutSeconds) Enter(Phase.Fading, KeyStand);
                    break;
                }
                case Phase.TakeOff:
                    if (_target >= 0) { float dx = tx - _x, dy = ty - _y; if (dx * dx + dy * dy > 1e-3f) _yaw = (float)Math.Atan2(dx, dy); }
                    if (t >= TakeOffSeconds)
                    {
                        // Leap: reach the target (or PounceRange straight ahead) in PounceFrames with a small arc.
                        float ex = _target >= 0 ? tx : _x + _dirX * PounceRange, ey = _target >= 0 ? ty : _y + _dirY * PounceRange;
                        float dx = ex - _x, dy = ey - _y;
                        _vx = dx / PounceFrames; _vy = dy / PounceFrames; _vh = Gravity * PounceFrames * 0.5f;
                        _dirX = dx; _dirY = dy;
                        float l = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (l > 1e-3f) { _dirX /= l; _dirY /= l; _yaw = (float)Math.Atan2(_dirX, _dirY); }
                        Enter(Phase.Leaping, KeyLeap);
                    }
                    break;
                case Phase.Leaping:
                {
                    _x += _vx; _y += _vy; _h += _vh; _vh -= Gravity;
                    bool near = _target >= 0 && (tx - _x) * (tx - _x) + (ty - _y) * (ty - _y) <= HitRadius * HitRadius * 0.5f;
                    if (!_hitDone && (near || (_vh < 0 && _h <= _floor + 1f))) { _hitDone = true; PlantHit(_x, _h + 3f, _y, HitRadius); }
                    if (_vh < 0 && _h <= _floor) { _h = _floor; Enter(Phase.LandEnd, KeyLand); }
                    break;
                }
                case Phase.LandEnd:
                    if (t >= LandSeconds) Enter(Phase.Fading, KeyStand);
                    break;
                case Phase.Fading:
                    _fade++;
                    _alpha = Math.Max(0f, 1f - _fade / (float)FadeTicks);
                    if (_fade >= FadeTicks) { Hide(); return; }
                    break;
            }
            Maintain();
        }

        /// <summary>Root = flight point − the head's rest offset (at the current growth scale) turned by the yaw
        /// (model +Z → world (sin yaw, cos yaw)), so the head stays on the pellet while the body grows behind it.</summary>
        private static void PlaceRootUnderHead()
        {
            float cy = (float)Math.Cos(_yaw), sy = (float)Math.Sin(_yaw), k = _scale * CatScale;
            _x = _px - k * (_headX * cy + _headZ * sy);
            _y = _py - k * (-_headX * sy + _headZ * cy);
            _h = _ph - k * _headH;
        }

        private static void Enter(Phase p, int key)
        {
            _phase = p; _phaseStart = DateTime.UtcNow;
            SetKey(key);
        }

        // ──────────────────────────────────────────── the copy ─────────────────────────────────────────────

        /// <summary>Deep-copy the cat subtree out of XIAO's live frame tree into the NodePool (the bake appends
        /// the 37 `cat_` nodes after her 79 body nodes, so they are one contiguous run ending the array), make the
        /// copied cat root a free-standing root, un-hide it, give it its own skin buffers and motion channel, and
        /// host it in a dungeon chara slot.</summary>
        private static bool Spawn()
        {
            if (Active) return true;
            uint playerRoot = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(playerRoot)) { Console.WriteLine(Tag + "no player model"); return false; }
            _liveRoot = playerRoot;

            // Her frames are ONE contiguous 0x270 array from the model root (.mds node order): walk it while the
            // names stay printable and the parent pointers stay inside the array. The cat root is the first
            // `catroot`; everything after it is the cat.
            var names = new List<string>();
            int catIdx = -1;
            for (int i = 0; i < MaxTreeNodes; i++)
            {
                uint n = playerRoot + (uint)(i * CFrameVu1.NodeStride);
                if (!Memory.IsValidGuest(n)) break;
                string nm = ReadName(n);
                uint par = (uint)Memory.ReadInt(Memory.ToMmu(n) + CFrameVu1.Parent) & Memory.PhysAddrMask;
                // Every body node's parent is inside the array; the cat root is UNPARENTED (the bake gives it parent
                // -1 so she never draws or skins it) and its children parent back into the cat run.
                bool parOk = i == 0 || nm == CatRootName || (par >= playerRoot && par < n && (par - playerRoot) % CFrameVu1.NodeStride == 0);
                if (nm.Length == 0 || !parOk) break;
                bool printable = true;
                foreach (char ch in nm) if (ch < 0x20 || ch > 0x7E) { printable = false; break; }
                if (!printable) break;
                names.Add(nm);
                if (catIdx < 0 && nm == CatRootName) catIdx = i;
            }
            string chanInfo = "";
            for (int c = 0; c < CCharacter.MotionSlots; c++)
            {
                uint cp = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.MotionSlotBase + c * 4) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(cp)) continue;
                chanInfo += $" ch{c}=0x{cp:X} keys {Memory.ReadInt(CCharacter.Base + ChanKeyStart + c * 4)}..{Memory.ReadInt(CCharacter.Base + ChanKeyEnd + c * 4)}";
            }
            Console.WriteLine(Tag + $"live player tree @0x{playerRoot:X}: {names.Count} node(s); channels:{chanInfo}");
            if (catIdx < 0)
            {
                Console.WriteLine(Tag + "nodes: " + string.Join(",", names));
                Console.WriteLine(Tag + "no '" + CatRootName + "' in her tree — the loaded c04b.chr has no cat (ISO not patched, or PCSX2 still on the old image)");
                return false;
            }
            _catIndex  = catIdx;
            _nodeCount = names.Count - catIdx;
            uint min = playerRoot + (uint)(catIdx * CFrameVu1.NodeStride);
            uint max = min + (uint)((_nodeCount - 1) * CFrameVu1.NodeStride);
            int blockSize = _nodeCount * CFrameVu1.NodeStride;
            if (_nodeCount > CodeCaves.MaxNodes) { Console.WriteLine(Tag + $"{_nodeCount} cat nodes exceed the NodePool"); return false; }
            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            _skinNodes.Clear();
            uint poolG = (uint)CodeCaves.NodePoolGuest;
            for (int o = 0; o < blockSize; o += CFrameVu1.NodeStride)
            {
                bool isRoot = o == 0;
                Rebase(block, o + CFrameVu1.Parent,      min, max, poolG, isRoot);   // cat root's parent (her root) → none
                Rebase(block, o + CFrameVu1.RootChild,   min, max, poolG, false);
                Rebase(block, o + CFrameVu1.RootSibling, min, max, poolG, isRoot);   // ...and its sibling chain → none
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + CFrameVu1.WorldCacheB);
                Array.Clear(block, o + CFrameVu1.WorldMatrix, 0x40);
            }
            // Un-hide: the bake scaled the cat root's bind 3x3 by HideScale so SHE never shows it.
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int b = CFrameVu1.LocalMatrix + r * 0x10 + c * 4;
                    BitConverter.GetBytes(BitConverter.ToSingle(block, b) / HideScale).CopyTo(block, b);
                }
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransX);           // root sits at the object origin
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransY);
            BitConverter.GetBytes(0f).CopyTo(block, CFrameVu1.LocalTransZ);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX + 4);
            BitConverter.GetBytes(1f).CopyTo(block, CFrameVu1.TrsScaleX + 8);
            BitConverter.GetBytes(0).CopyTo(block, CFrameVu1.DirtyTrs);
            Memory.WriteBytesBatch(CodeCaves.NodePool, block);
            _copyRoot = poolG;
            FindHead(block);

            if (!CopyMeshes()) return false;
            if (!RegisterSlot(min, blockSize)) return false;
            Active = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cat copy up: {_nodeCount} nodes (her n{_catIndex}..n{_catIndex + _nodeCount - 1}) → 0x{_copyRoot:X}, slot {Slot}");
            return true;
        }

        /// <summary>The head's rest position in the cat's own space (row-vector chain of local matrices from
        /// `cat_kao` up to the root), so the flight can keep the HEAD on the pellet's line (user 2026-09-10).</summary>
        private static void FindHead(byte[] block)
        {
            _headX = 0f; _headH = HeadFallbackHeight; _headZ = 0f;
            int head = -1;
            for (int i = 0; i < _nodeCount; i++)
            {
                int o = i * CFrameVu1.NodeStride + CFrameVu1.Name, len = 0;
                while (len < 0x20 && block[o + len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(block, o, len) == HeadNodeName) { head = i; break; }
            }
            if (head < 0) return;
            uint poolG = (uint)CodeCaves.NodePoolGuest;
            float[] p = { 0f, 0f, 0f, 1f };
            int n = head;
            for (int guard = 0; guard < 64 && n > 0; guard++)
            {
                int b = n * CFrameVu1.NodeStride + CFrameVu1.LocalMatrix;
                float[] q = new float[4];
                for (int c = 0; c < 4; c++)
                    q[c] = p[0] * BitConverter.ToSingle(block, b + c * 4) + p[1] * BitConverter.ToSingle(block, b + 0x10 + c * 4)
                         + p[2] * BitConverter.ToSingle(block, b + 0x20 + c * 4) + p[3] * BitConverter.ToSingle(block, b + 0x30 + c * 4);
                p = q;
                uint par = (uint)BitConverter.ToInt32(block, n * CFrameVu1.NodeStride + CFrameVu1.Parent) & Memory.PhysAddrMask;
                n = par >= poolG ? (int)((par - poolG) / CFrameVu1.NodeStride) : 0;
            }
            _headX = p[0]; _headH = p[1]; _headZ = p[2];
            Console.WriteLine(Tag + $"head ({HeadNodeName}, n{head}) rests at ({_headX:F2},{_headH:F2},{_headZ:F2}) in cat space");
        }

        private static string ReadName(uint node)
        {
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(node) + CFrameVu1.Name, 0x20);
            if (b == null) return "";
            int len = 0; while (len < b.Length && b[len] != 0) len++;
            return System.Text.Encoding.ASCII.GetString(b, 0, len);
        }

        /// <summary>The cat's software-skinned meshes get their own copies in the MeshCave (CopyMeshNodes'
        /// recipe). HER copy of the cat skin sits collapsed under the hidden root, and MotionProc2 would skin a
        /// shared buffer for whichever character stepped last — the copy must own it, or the spawn is refused.</summary>
        private static bool CopyMeshes()
        {
            long cave = CodeCaves.MeshCave, caveGuest = CodeCaves.MeshCaveGuest;
            long caveEnd = CodeCaves.MeshCave + CodeCaves.MeshCaveSize - 0x1000;    // top 0x1000 = the reflector's track cave
            int copied = 0;
            for (int i = 0; i < _nodeCount; i++)
            {
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisMDT) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(mdt) || (uint)Memory.ReadInt(Memory.ToMmu(mdt)) != CVisualMDT.MdtMagic) continue;
                uint vu   = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU) & Memory.PhysAddrMask;
                int vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU + 4) * 16;
                int mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + CVisualMDT.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;
                int visSz = CVisualMDT.VisualSize;
                int need = A16(visSz) + A16(vuSz) + A16(mdtSz);
                if (cave + need > caveEnd) { Console.WriteLine(Tag + "cat meshes do not fit the MeshCave"); return false; }
                long cVis = cave;              uint cVisG = (uint)caveGuest;
                long cVU  = cave + A16(visSz); uint cVUG  = (uint)(caveGuest + A16(visSz));
                long cMDT = cVU + A16(vuSz);   uint cMDTG = (uint)(caveGuest + A16(visSz) + A16(vuSz));
                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu),  vuSz);
                byte[] mdtB = Memory.ReadBytesBatch(Memory.ToMmu(mdt), mdtSz);
                if (visB == null || vuB == null || mdtB == null) continue;
                RebaseRange(visB, vu, vuSz, cVUG); RebaseRange(visB, mdt, mdtSz, cMDTG);
                foreach (byte[] b in new[] { vuB, mdtB }) { RebaseRange(b, vu, vuSz, cVUG); RebaseRange(b, mdt, mdtSz, cMDTG); }
                // The engine writes the skinned draw packet into buffer[DBuffID] (+0x28 / +0x2C) every frame while
                // the GIF is still reading the other. A single-buffered copy tears (flicker); give the copy both.
                long cVU2 = cave + need; uint cVU2G = (uint)(caveGuest + need);
                bool twoBuffers = cVU2 + A16(vuSz) <= caveEnd;
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(twoBuffers ? cVU2G : cVUG).CopyTo(visB, 0x2c);
                Memory.WriteBytesBatch(cVU, vuB);
                if (twoBuffers) Memory.WriteBytesBatch(cVU2, vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);
                if (twoBuffers) need += A16(vuSz);
                cave += need; caveGuest += need; copied++;
                _skinNodes.Add((i, cMDT, mdtSz, cVU, twoBuffers ? cVU2 : 0L, vuSz));
                Console.WriteLine(Tag + $"mesh n{i} ({ReadName((uint)(CodeCaves.NodePoolGuest + i * CFrameVu1.NodeStride))}): vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} copied");
            }
            _caveFree = cave;
            if (copied == 0) { Console.WriteLine(Tag + "no software-skinned cat mesh found — refusing to share her collapsed copy of the skin"); return false; }
            return true;
        }

        private static readonly List<(int node, long mdt, int mdtSz, long vu, long vu2, int vuSz)> _skinNodes = new();
        private static long _caveFree;

        /// <summary>The skinner's SOURCE vertices. AnimeDataInit (0x1493A0) runs once per character — from
        /// CommandMOTION only while CCharacter+0x2CC is still null, i.e. for MOTION 0 — and builds, for every mesh
        /// in THAT channel's skin list, a bind-transformed copy of its vertices into FRAME_INF[mesh] (+4 count,
        /// +8 → copy). Her cat skin is a MOTION 1 mesh, so its entry never got one: MotionProc2 would read its
        /// source vertices from address 0 (the first crash's run of low-address load faults). Build it here in
        /// the MeshCave exactly as the initializer does: def[i] = bindMatrix(node) · vertex[i], with the bind
        /// matrix taken from the table row the caller built from the copied node.</summary>
        private static bool BuildSkinSources(byte[] fib)
        {
            long cave = A16L(_caveFree), caveEnd = CodeCaves.MeshCave + CodeCaves.MeshCaveSize - 0x1000;
            foreach (var (node, mdt, _, _, _, _) in _skinNodes)
            {
                int count = Memory.ReadInt(mdt + CVisualMDT.MdtVertCount);
                int vOff  = Memory.ReadInt(mdt + CVisualMDT.MdtVertOffset);
                if (count <= 0 || count > 3000 || vOff <= 0) { Console.WriteLine(Tag + $"skin n{node}: odd MDT header (count {count}, verts @+0x{vOff:X})"); return false; }
                int bytes = count * 16;
                if (cave + bytes > caveEnd) { Console.WriteLine(Tag + "skin source vertices do not fit the MeshCave"); return false; }
                byte[] src = Memory.ReadBytesBatch(mdt + vOff, bytes);
                if (src == null) return false;
                int e = node * MotionType.FrameInfEntry;
                float[] m = new float[16];
                for (int k = 0; k < 16; k++) m[k] = BitConverter.ToSingle(fib, e + 0x10 + k * 4);   // the entry's bind matrix
                byte[] dst = new byte[bytes];
                for (int v = 0; v < count; v++)
                {
                    float x = BitConverter.ToSingle(src, v * 16), y = BitConverter.ToSingle(src, v * 16 + 4), z = BitConverter.ToSingle(src, v * 16 + 8), w = BitConverter.ToSingle(src, v * 16 + 12);
                    for (int c = 0; c < 4; c++)
                        BitConverter.GetBytes(x * m[c] + y * m[4 + c] + z * m[8 + c] + w * m[12 + c]).CopyTo(dst, v * 16 + c * 4);
                }
                Memory.WriteBytesBatch(cave, dst);
                BitConverter.GetBytes(count).CopyTo(fib, e + 4);
                BitConverter.GetBytes((uint)(cave - 0x20000000)).CopyTo(fib, e + 8);
                Console.WriteLine(Tag + $"skin n{node}: {count} source vertices built at 0x{cave - 0x20000000:X} from bind [{m[0]:F2} {m[5]:F2} {m[10]:F2} | {m[12]:F2},{m[13]:F2},{m[14]:F2}]");
                cave += A16(bytes);
            }
            _caveFree = cave;
            return true;
        }

        private static long A16L(long n) => (n + 15) & ~15L;

        // ── Her MOTION 1 channel: watchdog + repair ─────────────────────────────────────────────────────────
        // 2026-09-10: after some spawn/despawn cycles her channel-1 pointer (+0xC24) read as invalid and every later
        // shot failed ("she has no MOTION 1 channel"). No engine writer of that word runs mid-floor (DeleteExtendMotion
        // is town-only, Initialize/CommandMOTION only on loads, operator= only for town NPCs) and the mod never writes
        // her table — so the watchdog logs the exact tick it changes, with the raw words, and the spawn repairs it
        // when the inline channel struct (character + 0x420 + 0x80·1, CommandMOTION's placement) is still intact.
        private const int  ChanInlineBase = 0x420, ChanInlineStride = 0x80;
        private static bool _herChanValid = true;
        private static DateTime _lastDespawn = DateTime.MinValue;

        private static void WatchHerCatChannel()
        {
            uint raw = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.MotionSlotBase + CatChannel * 4);
            bool valid = Memory.IsValidGuest(raw & Memory.PhysAddrMask);
            if (valid == _herChanValid) return;
            _herChanValid = valid;
            if (valid) { Console.WriteLine(Tag + $"her MOTION 1 pointer is back (0x{raw:X8})"); return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < CCharacter.MotionSlots; i++)
                sb.Append($" ch{i}=0x{(uint)Memory.ReadInt(CCharacter.Base + CCharacter.MotionSlotBase + i * 4):X8}/{Memory.ReadInt(CCharacter.Base + ChanKeyStart + i * 4)}..{Memory.ReadInt(CCharacter.Base + ChanKeyEnd + i * 4)}");
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"her MOTION 1 pointer LOST (raw 0x{raw:X8}) — phase {(Active ? _phase.ToString() : "idle")}, {(DateTime.UtcNow - _lastDespawn).TotalSeconds:F2} s after the last despawn; table:{sb}");
        }

        /// <summary>Put her channel-1 pointer and key range back when the inline MOTION struct still holds its data
        /// (its KEY table and bind rows are valid pointers). Returns true and refreshes <paramref name="buf"/> on success.</summary>
        private static bool RepairHerCatChannel(ref byte[] buf)
        {
            long inline = CCharacter.Base + ChanInlineBase + CatChannel * ChanInlineStride;
            uint keyTable = (uint)Memory.ReadInt(inline + MotionType.MotionInfoPtr) & Memory.PhysAddrMask;
            uint boneRows = (uint)Memory.ReadInt(inline + MotionType.BoneMtxPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(keyTable) || !Memory.IsValidGuest(boneRows))
            {
                Console.WriteLine(Tag + $"her MOTION 1 struct @0x{inline & Memory.PhysAddrMask:X} is empty too (KEY 0x{keyTable:X}, rows 0x{boneRows:X}) — cannot repair");
                return false;
            }
            Memory.WriteUInt(CCharacter.Base + CCharacter.MotionSlotBase + CatChannel * 4, (uint)(inline & Memory.PhysAddrMask));
            Memory.WriteInt (CCharacter.Base + ChanKeyStart + CatChannel * 4, KeyBase);
            Memory.WriteInt (CCharacter.Base + ChanKeyEnd   + CatChannel * 4, KeyBase + KeyCount);
            Console.WriteLine(Tag + $"her MOTION 1 pointer repaired → 0x{inline & Memory.PhysAddrMask:X} (keys {KeyBase}..{KeyBase + KeyCount})");
            buf = Memory.ReadBytesBatch(CCharacter.Base, CharCopySize) ?? buf;
            return true;
        }

        /// <summary>Host slot from XIAO's own CCharacter (draw/texture config, as the Mirage clone does), aimed at
        /// the copy, with her cat channel (MOTION 1) cloned as the copy's ONLY channel — own FrameInf/BoneMtx (any
        /// bone pointers into the live cat block re-based to the copy), the KEY table and track list shared
        /// read-only — and the copy's key range set to the cat keys so <see cref="SetKey"/> with 64..69 selects it.
        /// The cat channel's tracks are indexed relative to the cat root, which IS the copy's node 0.</summary>
        private static bool RegisterSlot(uint min, int blockSize)
        {
            byte[] buf = Memory.ReadBytesBatch(CCharacter.Base, CharCopySize);
            if (buf == null) return false;
            uint chan = (uint)BitConverter.ToInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chan) && RepairHerCatChannel(ref buf))
                chan = (uint)BitConverter.ToInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chan)) { Console.WriteLine(Tag + $"she has no MOTION 1 channel (raw 0x{BitConverter.ToUInt32(buf, CCharacter.MotionSlotBase + CatChannel * 4):X8}) — the loaded c04b.chr has no cat"); return false; }
            byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(chan), MotionStructSize);
            if (mstr == null) return false;
            int fiSize = (_nodeCount + 1) * MotionType.FrameInfEntry;
            int bmSize = (_nodeCount + 1) * MotionType.BoneMtxEntry;
            if (fiSize > CodeCaves.FrameInfCaveSize || bmSize > CodeCaves.BoneMtxCaveSize) { Console.WriteLine(Tag + "bone buffers exceed the caves"); return false; }
            uint poolG = (uint)CodeCaves.NodePoolGuest;
            // FRAME_INF is indexed by NODE (AnimeDataInit 0x1493A0: entry i = {parent index, skin vertex count, →
            // bind-vertex copy, bind matrix @+0x10, two scratch matrices @+0x50/+0x90}). The initializer fills the
            // rows only for nodes reachable from her model root — and the cat is UNPARENTED on purpose — so her
            // rows for the cat are uninitialized heap (they printed as floats). Build the copy's table from the
            // copied nodes themselves: parent index made cat-relative (root → itself) and the bind matrix = the
            // node's local matrix (never posed on her; the root's is the un-hidden one written above).
            {
                byte[] fib = new byte[fiSize];
                for (int i = 0; i < _nodeCount; i++)
                {
                    long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                    uint par = (uint)Memory.ReadInt(node + CFrameVu1.Parent) & Memory.PhysAddrMask;
                    int rel = (par >= poolG && par < poolG + (uint)blockSize) ? (int)((par - poolG) / CFrameVu1.NodeStride) : 0;
                    int e = i * MotionType.FrameInfEntry;
                    BitConverter.GetBytes(rel).CopyTo(fib, e);
                    byte[] local = Memory.ReadBytesBatch(node + CFrameVu1.LocalMatrix, 0x40);
                    if (local == null) return false;
                    local.CopyTo(fib, e + 0x10);
                }
                if (!BuildSkinSources(fib)) return false;
                Memory.WriteBytesBatch(CodeCaves.FrameInfCave, fib);
                BitConverter.GetBytes((uint)CodeCaves.FrameInfCaveGuest).CopyTo(mstr, MotionType.FrameInfPtr);
            }
            // Per-bone BIND matrices (channel +0): CreateAnimeDataEX (0x149090) makes this buffer a straight copy of
            // the channel's .bbp file, and the skinner chains it into the inverse-bind side (posed world × inv(bind
            // world) × vertex). Her channel 1's buffer IS cat.bbp — 37 rows in cat order — so take rows 0..36 as they
            // are. (The .mds local matrices are NOT the same thing: initializing from them stretched every blended
            // joint — neck, shoulders, tail tip.)
            uint bm = (uint)BitConverter.ToInt32(mstr, MotionType.BoneMtxPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(bm)) { Console.WriteLine(Tag + "her cat channel has no bind rows"); return false; }
            {
                byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                if (bmb == null) return false;
                Memory.WriteBytesBatch(CodeCaves.BoneMtxCave, bmb);
                BitConverter.GetBytes((uint)(CodeCaves.BoneMtxCave & Memory.PhysAddrMask)).CopyTo(mstr, MotionType.BoneMtxPtr);
            }
            uint keyTable = (uint)BitConverter.ToInt32(mstr, MotionType.MotionInfoPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(keyTable)) { Console.WriteLine(Tag + "cat KEY table unreadable"); return false; }
            RebaseRange(mstr, min, blockSize, poolG);
            Memory.WriteBytesBatch(CodeCaves.MotionCave, mstr);

            BitConverter.GetBytes((uint)CodeCaves.MotionCaveGuest).CopyTo(buf, CCharacter.MotionSlotBase);
            for (int s = 1; s < CCharacter.MotionSlots; s++) BitConverter.GetBytes(0).CopyTo(buf, CCharacter.MotionSlotBase + s * 4);
            BitConverter.GetBytes(KeyBase).CopyTo(buf, ChanKeyStart);
            BitConverter.GetBytes(KeyBase + KeyCount).CopyTo(buf, ChanKeyEnd);
            for (int s = 1; s < CCharacter.MotionSlots; s++)
            {
                BitConverter.GetBytes(0).CopyTo(buf, ChanKeyStart + s * 4);
                BitConverter.GetBytes(0).CopyTo(buf, ChanKeyEnd + s * 4);
            }
            BitConverter.GetBytes(keyTable).CopyTo(buf, CCharacter.MotionList);
            // No shadow: her CCharacter carries her shadow rig (+0xC0) and shadow channels (+0xC40+i*4), which
            // ShadowStep would step with OUR motion id against HER shadow KEY table (37 entries, ids 64..69 fall
            // off its end) and pose HER shadow tree. The slingshot copy has none either.
            BitConverter.GetBytes(0).CopyTo(buf, ShadowModel);
            for (int i = 0; i < CCharacter.MotionSlots; i++) BitConverter.GetBytes(0).CopyTo(buf, ShadowSlotBase + i * 4);
            BitConverter.GetBytes((uint)CodeCaves.ClothStubGuest).CopyTo(buf, CCharacter.ClothList);
            BitConverter.GetBytes(_copyRoot).CopyTo(buf, CCharacter.CharModel);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 4);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharaTint + 8);
            BitConverter.GetBytes(1.0f).CopyTo(buf, CCharacter.DimFactor);
            BitConverter.GetBytes(128f).CopyTo(buf, CCharacter.NpcOpacity);
            for (int o = CCharacter.LightFrom; o < CCharacter.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            BitConverter.GetBytes(_x).CopyTo(buf, CCharacter.CharPos);
            BitConverter.GetBytes(_h).CopyTo(buf, CCharacter.CharPos + 4);
            BitConverter.GetBytes(_y).CopyTo(buf, CCharacter.CharPos + 8);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharRot);
            BitConverter.GetBytes(_yaw).CopyTo(buf, CCharacter.CharRotY);
            BitConverter.GetBytes(0f).CopyTo(buf, CCharacter.CharRot + 8);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale + 4);
            BitConverter.GetBytes(CatScale * _scale).CopyTo(buf, CCharacter.CharScale + 8);
            BitConverter.GetBytes(KeyLeap).CopyTo(buf, CCharacter.MotionId);
            BitConverter.GetBytes(CharacterMotion.MotionSpeedUseKey).CopyTo(buf, CharacterMotion.MotionSpeedOffset);
            BitConverter.GetBytes((uint)BitConverter.ToInt32(buf, CCharacter.MotionFlags) | (uint)CCharacter.MotionRestart)
                .CopyTo(buf, CCharacter.MotionFlags);

            Memory.WriteBytesBatch(CodeCaves.ClothStub, new byte[16]);
            long slot = SlotAddr();
            // The CNPCharacter tail past the copied 0xD60 block (foot-sound + event tables, the NPC sequence
            // state PlaySeq runs every step, the fade/ramp words) is whatever this slot last held. Initialize it
            // the way Initialize__12CNPCharacter / ClearSeq do: tables free (-1.0f frames), sequences cleared.
            var tail = new byte[DungeonCharaDraw.CharaStride - CharCopySize];
            for (int i = 0; i < FootSlots; i++)  BitConverter.GetBytes(-1f).CopyTo(tail, FootTable  + i * FootStride  - CharCopySize);
            for (int i = 0; i < EventSlots; i++) BitConverter.GetBytes(-1f).CopyTo(tail, EventTable + i * EventStride - CharCopySize);
            BitConverter.GetBytes(1).CopyTo(tail, SeqEnable - CharCopySize);
            BitConverter.GetBytes(-1).CopyTo(tail, DungeonCharaDraw.CharaRampB - CharCopySize);
            BitConverter.GetBytes(-1).CopyTo(tail, SeqWord1490 - CharCopySize);
            Memory.WriteBytesBatch(slot, buf);
            Memory.WriteBytesBatch(slot + CharCopySize, tail);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt(slot + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 0);
            Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 1);
            RetagCatTextures(HerTextureBlock, SlotTextureGroup);
            uint boneHead = (uint)BitConverter.ToInt32(mstr, MotListHead) & Memory.PhysAddrMask, skinHead = (uint)BitConverter.ToInt32(mstr, MotionType.MotionSkinList) & Memory.PhysAddrMask;
            string heads = $"bone list 0x{boneHead:X}" + (Memory.IsValidGuest(boneHead) ? $" (w0 {Memory.ReadInt(Memory.ToMmu(boneHead))}, type {Memory.ReadInt(Memory.ToMmu(boneHead) + 8)}, keys {Memory.ReadInt(Memory.ToMmu(boneHead) + 0xC)})" : "")
                         + $", skin list 0x{skinHead:X}" + (Memory.IsValidGuest(skinHead) ? $" (mesh {Memory.ReadInt(Memory.ToMmu(skinHead))}, bone {Memory.ReadInt(Memory.ToMmu(skinHead) + 4)}, type {Memory.ReadInt(Memory.ToMmu(skinHead) + 8)}, keys {Memory.ReadInt(Memory.ToMmu(skinHead) + 0xC)})" : "");
            Console.WriteLine(Tag + $"slot {Slot}: cat channel cloned (keys {KeyBase}..{KeyBase + KeyCount - 1}, KEY table 0x{keyTable:X}), FrameInf 0x{fiSize:X}; {heads}");
            return true;
        }

        private static void SetKey(int key)
        {
            if (!Active) return;
            _key = key;
            long s = SlotAddr();
            Memory.WriteInt  (s + CCharacter.MotionId, key);
            Memory.WriteInt  (s + CCharacter.MotionFlags, Memory.ReadInt(s + CCharacter.MotionFlags) | CCharacter.MotionRestart);
            Memory.WriteFloat(s + CharacterMotion.MotionSpeedOffset, CharacterMotion.MotionSpeedUseKey);
        }

        /// <summary>Re-assert the copy every tick: pose, opacity, model, key and the draw/step registration
        /// (the engine resets some of these between our ticks).</summary>
        private static void Maintain()
        {
            if (((uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask) != _liveRoot)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "her model changed — cat despawned");
                Despawn();
                return;
            }
            long s = SlotAddr();
            if (!_caveOwns)                                                      // armed/following: position, scale and opacity are the cave's
            {
                Memory.WriteFloat(s + CCharacter.CharPos,     _x);
                Memory.WriteFloat(s + CCharacter.CharPos + 4, _h);
                Memory.WriteFloat(s + CCharacter.CharPos + 8, _y);
                Memory.WriteFloat(s + CCharacter.CharScale,     CatScale * _scale);
                Memory.WriteFloat(s + CCharacter.CharScale + 4, CatScale * _scale);
                Memory.WriteFloat(s + CCharacter.CharScale + 8, CatScale * _scale);
                Memory.WriteFloat(s + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, _alpha)));
            }
            Memory.WriteFloat(s + CCharacter.CharRot,     0f);
            Memory.WriteFloat(s + CCharacter.CharRotY,    _yaw);
            Memory.WriteFloat(s + CCharacter.CharRot + 8, 0f);
            Memory.WriteUInt (s + CCharacter.CharModel, _copyRoot);
            if (!_caveOwns) Memory.WriteInt(s + CCharacter.MotionId, _key);      // the cave sets leap/land/run keys on its own frames
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 0);
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 1);
        }

        internal static void Despawn()
        {
            if (!Active) return;
            DisarmCave();
            if (_pelletSlot >= 0)                                                // cat gone while its pellet still flies: give the sprite back
            {
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(_pool, _pelletSlot)) != 0) Memory.WriteFloat(PlayerShotPool.ScaleAddr(_pool, _pelletSlot), 1f);
                _pelletSlot = -1;
            }
            long s = SlotAddr();
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 0);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 0);
            Memory.WriteFloat(s + CCharacter.NpcOpacity, 0f);
            Memory.WriteUInt (s + CCharacter.CharModel, 0);
            RetagCatTextures(SlotTextureGroup, HerTextureBlock);
            Active = false; _key = -1; _target = -1;
            if (!SlingshotProp.Active) Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 2);   // after Active=false: Mirage's loop owns it again
            _lastDespawn = DateTime.UtcNow;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "cat copy down");
        }

        private static long SlotAddr() => DungeonCharaDraw.CharaArray + (long)Slot * DungeonCharaDraw.CharaStride;

        // ─────────────────────────────────────────── textures ──────────────────────────────────────────────
        // The dungeon draw loop re-uploads texture group 0x20+slot to VRAM right before it draws chara slot i
        // (ReloadTexture 0x133070 uploads every CTexture entry tagged with that block). The cat's textures sit in
        // HER block, whose VRAM pages are gone by then (the upload window is paged and shared), so the copy sampled
        // whatever was there — a shimmering cat. Re-tag the five cat entries to the slot's group while the copy is
        // up (CTextureManager @0x1C75870: entry count @+0, entries @+0x10F8, 0x50 apart, block tag = first short,
        // name @+8) and hand them back on despawn.
        private const long TextureManager = 0x21C75870;
        private const int  TexEntries = 0x10F8, TexStride = 0x50, TexMaxEntries = 0xC4, TexName = 8;
        private const short HerTextureBlock = 0x11;
        private const short SlotTextureGroup = (short)(DungeonCharaDraw.CharaTexBase + Slot);
        private static readonly string[] CatTextureNames = { "c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05" };
        // A block descriptor (CTextureBlock, 0x3C bytes at manager+0x18+block*0x3C): +0x20 VRAM base, +0x24 VRAM top,
        // +0x28 loaded flag, +0x30 dirty watermark. ReloadTexture re-uploads an entry only if its VRAM address is at or
        // below the watermark (capped by the block's top) or the loaded flag is 0 — so a block whose base/top are 0
        // uploads nothing. The re-tagged entries keep the VRAM addresses they were given in HER window, so slot 1's
        // block takes over that tail of her window (and hers shrinks to just before it) while the copy is up.
        private const int  TexBlocks = 0x18, TexBlockStride = 0x3C, BlkBase = 0x20, BlkTop = 0x24, BlkLoaded = 0x28, BlkDirty = 0x30;
        private static uint _herTopSaved;
        private static void RetagCatTextures(short from, short to)
        {
            int count = Math.Min(TexMaxEntries, Memory.ReadInt(TextureManager));
            int done = 0; uint minTbp = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager + TexEntries + (long)i * TexStride;
                if (Memory.ReadShort(e) != from) continue;
                byte[] nb = Memory.ReadBytesBatch(e + TexName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                string nm = System.Text.Encoding.ASCII.GetString(nb, 0, len);
                if (Array.IndexOf(CatTextureNames, nm) < 0) continue;
                Memory.WriteUShort(e, (ushort)to);
                minTbp = Math.Min(minTbp, Memory.ReadUInt(e + 0x28) & 0x3FFF);
                done++;
            }
            long her = TextureManager + TexBlocks + (long)HerTextureBlock * TexBlockStride;
            long grp = TextureManager + TexBlocks + (long)SlotTextureGroup * TexBlockStride;
            if (to == SlotTextureGroup && done > 0 && minTbp != uint.MaxValue)
            {
                // The slot loop's group reload is written into the main frame packet, but the slot's draw goes
                // into the chara packet the GS consumes EARLIER in the frame (GS dump 2026-09-10: the cat's binds
                // precede its own upload, and an enemy block had overwritten the pages in between). So the cat's
                // textures live where nothing else uploads: the top of the manager's VRAM range, above every
                // block's top. Entries, the copy's packet buffers and MDT get the new addresses; hers are untouched.
                _herTopSaved = Memory.ReadUInt(her + BlkTop);
                uint size = _herTopSaved - minTbp;
                uint limit = Memory.ReadUInt(TextureManager + 0x14);
                uint newBase = (limit - size) & ~0x1Fu;
                uint highest = 0;
                for (int b = 0; b < 0x48; b++) highest = Math.Max(highest, Memory.ReadUInt(TextureManager + TexBlocks + (long)b * TexBlockStride + BlkTop));
                if (highest > newBase) Console.WriteLine(Tag + $"WARNING: a texture block tops at 0x{highest:X}, inside the cat's window 0x{newBase:X}..0x{newBase + size:X}");
                int patched = RelocateCatTextures(minTbp, newBase);
                Memory.WriteUInt(grp + BlkBase, newBase);
                Memory.WriteUInt(grp + BlkTop, newBase + size);
                Memory.WriteUInt(grp + BlkLoaded, 0);
                Memory.WriteUInt(grp + BlkDirty, 0);
                Memory.WriteUInt(her + BlkTop, minTbp);
                Console.WriteLine(Tag + $"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X} and moved 0x{minTbp:X}..0x{_herTopSaved:X} → 0x{newBase:X}..0x{newBase + size:X} ({patched} register words patched in the copy); her block now tops at 0x{minTbp:X}");
            }
            else if (to == HerTextureBlock)
            {
                if (_texMoved.Count > 0) RelocateCatTextures(0, 0);          // back to their original addresses
                if (_herTopSaved != 0) Memory.WriteUInt(her + BlkTop, _herTopSaved);
                Memory.WriteUInt(grp + BlkBase, 0);
                Memory.WriteUInt(grp + BlkTop, 0);
                Memory.WriteUInt(grp + BlkLoaded, 1);
                Memory.WriteUInt(her + BlkLoaded, 0);                          // she re-uploads her whole window next frame
                Console.WriteLine(Tag + $"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X}; her block restored to top 0x{_herTopSaved:X}");
            }
            else Console.WriteLine(Tag + $"textures: {done} cat entries re-tagged block 0x{from:X} → 0x{to:X}");
        }

        // (entry address, original tex0) for every cat texture moved this spawn — restored on despawn.
        private static readonly List<(long entry, ulong tex0)> _texMoved = new();

        /// <summary>Move the cat entries' TEX0 (TBP0 bits 0..13, CBP bits 37..50) by newBase − oldBase, and patch
        /// the identical register words wherever they sit in the copy's own packet buffers and MDT copy (the
        /// packet is rebuilt from the MDT every frame). With oldBase == 0 the saved originals are put back.</summary>
        private static int RelocateCatTextures(uint oldBase, uint newBase)
        {
            var moves = new List<(ulong oldT, ulong newT)>();
            if (oldBase == 0 && newBase == 0)
            {
                foreach (var (entry, tex0) in _texMoved)
                {
                    ulong cur = (ulong)Memory.ReadUInt(entry + 0x28) | ((ulong)Memory.ReadUInt(entry + 0x2C) << 32);
                    moves.Add((cur, tex0));
                    Memory.WriteUInt(entry + 0x28, (uint)tex0); Memory.WriteUInt(entry + 0x2C, (uint)(tex0 >> 32));
                }
                _texMoved.Clear();
            }
            else
            {
                int count = Math.Min(TexMaxEntries, Memory.ReadInt(TextureManager));
                for (int i = 0; i < count; i++)
                {
                    long e = TextureManager + TexEntries + (long)i * TexStride;
                    if (Memory.ReadShort(e) != SlotTextureGroup) continue;
                    ulong t = (ulong)Memory.ReadUInt(e + 0x28) | ((ulong)Memory.ReadUInt(e + 0x2C) << 32);
                    uint tbp = (uint)(t & 0x3FFF), cbp = (uint)((t >> 37) & 0x3FFF);
                    ulong n = (t & ~0x3FFFUL & ~(0x3FFFUL << 37)) | (ulong)(newBase + (tbp - oldBase)) | ((ulong)(newBase + (cbp - oldBase)) << 37);
                    _texMoved.Add((e, t));
                    moves.Add((t, n));
                    Memory.WriteUInt(e + 0x28, (uint)n); Memory.WriteUInt(e + 0x2C, (uint)(n >> 32));
                }
            }
            int patched = 0;
            var blocks = new List<(long addr, int size)>();
            foreach (var (node, mdt, mdtSz, vu, vu2, vuSz) in _skinNodes)
                blocks.AddRange(new[] { (mdt, mdtSz), (vu, vuSz), (vu2, vuSz) });
            // Rigid cat meshes (the bell) were not copied — their packet is shared with her hidden cat, which is
            // never drawn, so patching it in place is harmless (and undone with the entries on despawn).
            for (int i = 0; i < _nodeCount; i++)
            {
                if (_skinNodes.Exists(sn => sn.node == i)) continue;
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                uint vu = (uint)Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU) & Memory.PhysAddrMask;
                int vuSz = Memory.ReadInt(Memory.ToMmu(vis) + CVisualMDT.VisVU + 4) * 16;
                if (Memory.IsValidGuest(vu) && vuSz > 0 && vuSz < 0x40000) blocks.Add((Memory.ToMmu(vu), vuSz));
                uint vuB = (uint)Memory.ReadInt(Memory.ToMmu(vis) + 0x2C) & Memory.PhysAddrMask;
                if (vuB != vu && Memory.IsValidGuest(vuB) && vuSz > 0) blocks.Add((Memory.ToMmu(vuB), vuSz));
            }
            foreach (var (addr, size) in blocks)
                {
                    if (addr == 0) continue;
                    byte[] b = Memory.ReadBytesBatch(addr, size);
                    if (b == null) continue;
                    bool dirty = false;
                    for (int o = 0; o + 8 <= b.Length; o += 4)                 // GIF A+D data is 16-aligned, but be safe
                    {
                        ulong w = BitConverter.ToUInt64(b, o);
                        foreach (var (oldT, newT) in moves)
                            if (w == oldT) { BitConverter.GetBytes(newT).CopyTo(b, o); dirty = true; patched++; o += 4; break; }
                    }
                    if (dirty) Memory.WriteBytesBatch(addr, b);
                }
            return patched;
        }


        // ─────────────────────────────────────────── the hit ───────────────────────────────────────────────

        /// <summary>One pellet-style CollisionData entry at the pounce (GuardianReflector.PlantReflectedHit's
        /// recipe): base = the weapon's attack × <see cref="DamageMult"/>, the weapon's selected element as a pure
        /// bit (or none), her anti-category bytes and ability flags — CheckDmg does the rest.</summary>
        private static void PlantHit(float x, float h, float y, float radius)
        {
            long pool = Memory.ReadInt(NowColDataPtr);
            if (pool <= 0) return;
            pool += 0x20000000;
            int slot = -1;
            for (int i = ColEntries - 1; i >= 0; i--)
                if (Memory.ReadInt(pool + ColActiveOff + i * 4) == 0) { slot = i; break; }
            if (slot < 0) { Console.WriteLine(Tag + "no free collision entry — pounce lost"); return; }
            float attack = Memory.ReadShort(BattleWeaponAttack);
            int baseDmg = Math.Max(1, (int)Math.Round(attack * DamageMult));
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;      // one pure element bit or none
            var e = new byte[ColStride];
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            void I(int o, int v)   => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x00, x); F(0x04, h); F(0x08, y); F(0x0C, 1f);
            F(0x1C, 1f); F(0x20, 1f);
            I(0x34, baseDmg); I(0x38, 0); F(0x3C, radius);
            I(0x44, 1); I(0x48, 2); I(0x4C, 2); I(0x50, (int)attr); I(0x54, 0);
            I(0x58, 1); I(0x5C, -1); I(0x60, 0);
            I(0x64, (int)(BattleWeaponStats - 0x20000000)); I(0x68, -1); I(0x6C, Memory.ReadShort(BattleWeaponFlags));
            I(0x70, 0); I(0x74, 0); F(0x8C, 1f); I(0x98, 0);
            Memory.WriteBytesBatch(pool + slot * ColStride, e);
            Memory.WriteInt(pool + ColActiveOff + slot * 4, 1);
            lock (_planted) _planted.Add((slot, PlantedLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"pounce hit at ({x:F1},{h:F1},{y:F1}) r={radius:F0}: base {baseDmg} (atk {attack:F0} × {DamageMult}), attr 0x{attr:X} → entry {slot}");
        }

        private static void RetirePlanted()
        {
            lock (_planted)
            {
                if (_planted.Count == 0) return;
                long pool = Memory.ReadInt(NowColDataPtr);
                for (int i = _planted.Count - 1; i >= 0; i--)
                {
                    var (idx, ticks) = _planted[i];
                    if (--ticks > 0) { _planted[i] = (idx, ticks); continue; }
                    if (pool > 0) Memory.WriteInt(pool + 0x20000000 + ColActiveOff + idx * 4, 0);
                    _planted.RemoveAt(i);
                }
            }
        }

        // ───────────────────────────────────────────── utils ───────────────────────────────────────────────

        private static int A16(int n) => (n + 15) & ~15;

        private static void Rebase(byte[] block, int off, uint min, uint max, uint caveG, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & Memory.PhysAddrMask;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max) neu = caveG + (old - min);
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        /// <summary>Pointer re-basing for copied blocks — shared, segment-checked (see Memory.RebaseRange: the
        /// old mask-first version re-pointed five vertex floats of the MDT copy into the cave).</summary>
        private static void RebaseRange(byte[] b, uint src, int size, uint dst) => Memory.RebaseRange(b, src, size, dst);
    }
}
