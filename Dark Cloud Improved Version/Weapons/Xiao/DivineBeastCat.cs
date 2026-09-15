using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private const int  KeyBase = 64, KeyCount = 9;
        private const int  KeyStand = 64, KeyReady = 65, KeyRun = 66, KeyTakeOff = 67, KeyLeap = 68, KeyLand = 69, KeyWalk = 70, KeyFloat = 71, KeySit = 72;   // walk = s86 KEY 2 at 1.0; float = the town ladder jump's vertical leap (e04c04cat #5); sit = s86 KEY 1
        private const float  MoveFrac      = 0.20f;    // ground speed after the landing, as a fraction of the pellet's speed (user 2026-09-11: 20%, it was losing enemies)
        // A full-charge pellet flies 5.0 u/frame, a lighter one 3.5, so a fraction made the walk jump between 0.56 and 0.80
        // ("suddenly very fast", 2026-09-11). The tuned feel was 16% of 3.5: pin it as an absolute speed instead.
        private const float  MoveSpeedAbs  = 0.20f * 3.5f;
        // Walk clip rate from the ground speed, the TOWN's mapping for this very rig (EdMoveChara 0x16A160: rate =
        // 0.8·(0.2 + stick) capped at 0.85, ground = 1.6·stick → rate = 0.16 + 0.5·ground). Planted feet would need
        // 5× that (the clip's real stride is 0.196 u/clip-frame) and looked far too fast; this is the tuned look.
        // Calibrated by eye against the town (user 2026-09-11): the walk reaches its cap at WalkCapSpeed units/frame —
        // 20% of the 3.5 u/frame pellet — rather than at the 1.36 u/frame the town formula literally implies (the two
        // contexts' units-per-frame do not read the same on screen). Slope = (cap − base) / that speed.
        private const float  RateBase = 0.16f, RateMax = 0.85f, WalkCapSpeed = 0.20f * 3.5f;   // cap and ground speed both at 20% (user 2026-09-11)
        private const float  RatePerSpeed = (RateMax - RateBase) / WalkCapSpeed;   // ≈ 0.99 per unit of ground speed
        private const double LifetimeSeconds = 20.0;   // from the bind: the cat stays until it lands a hit or this passes (user 2026-09-11)
        private const float  ProbeUp       = 8f;       // floor probe reach above the cat's root (catches a tread it is flying into)
        private const float  ProbeDown     = 40f;      // … and below (a drop off a ledge still finds the floor)
        // Extra casts ahead of and behind the root along its direction; the cat stands on the HIGHEST of the three, so a
        // ten-unit body on stairs rides on its uphill end (front paws ≈ z 2, hind paws ≈ z −2 in cat space).
        private const float  ProbeFront    = 3f, ProbeBack = 2f;
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
        private const double ChargeSeconds = 0.5;      // hold this long → the shot is the cat (user 2026-09-11)
        private const double GrowSeconds   = 0.1;      // the pellet grows into the cat over this long after it is fired
        private const int    GrowFrames    = 6;        // the same, in frames, for the native follower (60 fps)
        private const float  Gravity       = 0.05f;    // units/frame² — the pounce arc
        private const float  FallGravity   = 0.08f;    // units/frame² — the fall off the pellet's line at full size (cave)
        // The land clip is s86 c04cat motion 7, frames 215..227 (KEY 69 in build_cat_pack.py keeps the absolute frames):
        // the paws first touch the ground at 219 — that is where the forward momentum stops; at 227 the run begins.
        private const float  LandStopFrame = 219f, LandEndFrame = 227f;   // the paws touch at 219 (the land clip's lead-in aligns it with the touchdown)
        // The WINGED cat (user 2026-09-13, "make it feel like the wings make a difference"): pounces from 50 units, and keeps
        // re-aiming at the target past the apex until it has fallen halfway from the apex to the floor (the cave's height
        // rule, Mailbox.CatTrackHalf). Dormant chest-mimics keep the 30-unit range.
        private const float  PounceRangeWinged = 50f;
        private const float  LandClipStart = 215f, LandClipSpeed = 0.36f;   // KEY 69 in build_cat_pack.py (frames/frame)
        // The clip lowers the cat itself (hips 5.5 → 4.6 over 215..219), so it must start this many frames BEFORE the
        // physical touchdown for the paws to meet the floor at 219; the cave predicts the touchdown from the fall.
        private const float  LandLeadFrames = (LandStopFrame - LandClipStart) / LandClipSpeed;
        private const float  CatScale      = 1.0f;                     // the rig's own size (the 2× try on 2026-09-12 was reverted); the cave grows the cat to this via Mailbox.CatScaleMul
        // Ground game.
        private const float  RunSpeed      = 1.3f;     // units/frame
        private const float  PounceRange   = 30f;      // start the pounce within this of the target (user 2026-09-11; the leap re-sizes itself at launch)
        private const float  MaxTargetDistance = 300f; // PickTarget: only enemies within the vanilla render distance of Xiao
        private const float  KickStrength  = 2.0f, KickDecay = 0.3f;   // the hit's kickback, sized like Toan's heavier combo hits (1.2..3.0 / 0.2..0.4, type 2)
        private const int    CatKickType   = 2;                        // the hit's kick type (+0x98): melee-style reaction; also the value a hurt sphere's spare[1] must hold to admit the cat at spare[0] % (ELF PatchCatSpherePercent; disc-baked on Minotaur Joe's face)
        private const float  PounceFrames  = 32f;      // leap flight time (frames) to the enemy — most enemies (user 2026-09-11)
        private const float  PounceFramesTall = 40f;   // … for the tall/large/flying set (EnemySpecies.VerticalLeapTargets) and minibosses: a higher, longer arc
        private const float  HitRadius     = 4f;       // planted hit sphere at the struck enemy
        private const float  TouchRadius   = 3f;       // the cat's own touch radius in the cave's body-sphere test
        // Pounce clips (KEY 65 ready 95..105 @0.4, 67 take-off 190..204 @0.5, 68 leap 205..214 @0.5, 69 land 215..227 @0.36):
        // in place through the ready and the first take-off frames, forward momentum ramps over 194..198 and holds
        // through the leap and into the landing until the paws touch at 219 (user 2026-09-11). The clips carry the height.
        private const float  ReadyStartFrame = 95f, ReadyEndFrame = 105f;   // every pounce is the ready crouch + float-up vertical leap (user 2026-09-11); the take-off path is gone
        // The float-up clip is the town float's first ten frames (e04c04cat #5, source 160..169) at 285..294, play-once.
        // The cat keeps turning to the target through the whole clip and the jump is locked in the moment source
        // frame 169 arrives (user 2026-09-11). A play-once clip holds just short of its last frame (Step stops the
        // rate once frame + rate reaches the end), so the launch test is end − 1, the same margin the other clips use.
        private const float  FloatStartFrame = 285f, FloatSourceStart = 160f, FloatFeetOffSource = 169f;
        private const float  FloatLaunchFrame = FloatStartFrame + (FloatFeetOffSource - FloatSourceStart) - 1f;   // 293
        private const float  FloatRate       = 0.75f;   // the float-up's play rate (its KEY rate is 0.6; user 2026-09-11: a touch faster)
        private const float  FallBlendSteps  = 16f;     // the float-up → fall fade, in steps (the engine's default is 10; user 2026-09-11: 40)
        private const float  BlendDefault    = 0.1f;    // the engine's own per-step blend increment (MOTION_END seeds it)
        private const double LandSeconds   = 0.45, TakeOffSeconds = 0.4, RunTimeoutSeconds = 6.0, StraightRunSeconds = 1.5;
        private const int    FadeTicks     = 30;       // ≈ 0.5 s at the 16 ms tick (user 2026-09-11)
        private const int    GlowFadeTicks = FadeTicks; // the glow SHRINKS over the same ≈ 0.5 s the cat fades (user 2026-09-12; a longer linger was tried and dropped)
        private static int   _glowFade = -1;           // ticks into the glow's shrink (−1 = full size and following the cat; ≥ GlowFadeTicks = done: OFF until the next bind)
        private const float  DamageMult    = 1.5f;     // × the weapon's attack (a charged pellet's worth)
        private const int    PlantedLifeTicks = 4;   // ~4 frames for the enemy's CheckDmg to find the entry

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
        private static DateTime _boundAt = DateTime.MinValue, _armedSince = DateTime.MinValue;
        private static bool _gaitLogged, _blockedLogged;
        private static int   _target = -1;
        private static float _px, _ph, _py;                 // the flight POINT (where the pellet would be) — the head rides it
        private static float _headX, _headH, _headZ;        // head rest offset in cat space (FindHead)
        private const string HeadNodeName = "cat_kao";
        private const string GlowNodeA = "cat_kosibone", GlowNodeB = "cat_sebone2";   // hips + upper spine: the glow sits at their midpoint (the middle of the torso)
        private const float  GlowScale = 0.5f;         // the torch routine's scale: the flame sprite is 45 × 22.5 units at 1.0 (a 90-unit haze, half of it z-culled by the floor — the 2026-09-12 screenshot); 0.5 ≈ 22.5 × 11 around the torso (user 2026-09-12)
        private const int    GlowFlags = 2;            // 1 = the steady glow pair (18 × 9 at 1.0), 2 = the flickering flame sprite (45 × 22.5 at 1.0), 3 = both (two sizes → two glows)
        // The cat's own light: Draw__10CCharacter (0x137xxx) adds this float3 (CCharacter +0xCE0) to the scene ambient it lights
        // the model with (0..255 scale; the dungeon's own key lights are ~100-120). Brightness with a slight cyan lean
        // (user 2026-09-12); scaled by the fade so the cat dims as it goes.
        // Per-weapon look (user 2026-09-13): the Divine Beast Title keeps its blue glow, cyan tint and NO wings; the Angel
        // Shooter's cat wears the wings with a WHITE glow and a neutral ("dark grey") add; the Angel Gear's the wings with a
        // GOLD glow and a gold-white add. The glow discs are baked textures (build_cat_pack.GLOW_VARIANTS) the glow cave binds by
        // the name written to Mailbox.CatGlowName; the wings are two mesh nodes the copy hides by zeroing their geometry pointer.
        private sealed class WeaponLook { public string Glow; public float[] Tint; public bool Wings; public bool Cape; public float Range = PounceRange; public bool Track; }
        // Super Steve inherits the cat from its attached SynthSphere (user 2026-09-13): a Divine Beast Title sphere = the Title's
        // cat exactly; an Angel Shooter / Angel Gear sphere = the BLUE cat (the Title's look, no wings) wearing a solid-yellow
        // cloth cape from its collar, with the winged cat's range and tracking. Keyed under a private id so the sphere swap
        // rebuilds the copy like a weapon change.
        private const int SuperSteveAngelKey = -2;
        private static readonly Dictionary<int, WeaponLook> Looks = new Dictionary<int, WeaponLook>
        {
            { Items.divinebeasttitle, new WeaponLook { Glow = "catglow",  Tint = new[] { 12f, 24f, 48f }, Wings = false } },   // user 2026-09-12
            { Items.angelshooter,     new WeaponLook { Glow = "catgloww", Tint = new[] { 20f, 20f, 20f }, Wings = true, Range = PounceRangeWinged, Track = true } },
            { Items.angelgear,        new WeaponLook { Glow = "catglowg", Tint = new[] { 27f, 26f, 20f }, Wings = true, Range = PounceRangeWinged, Track = true } },
            { SuperSteveAngelKey,     new WeaponLook { Glow = "catglow",  Tint = new[] { 12f, 24f, 48f }, Wings = false, Cape = true, Range = PounceRangeWinged, Track = true } },
        };
        private static WeaponLook _look = Looks[Items.divinebeasttitle];
        /// <summary>The look key for the equipped weapon: its own id, or for Super Steve the one its attached sphere grants
        /// (−1 = none: the cat stays down).</summary>
        private static int LookKeyFor(int weapon)
        {
            if (weapon != Items.supersteve) return Looks.ContainsKey(weapon) ? weapon : -1;
            int sphere = SuperSteveAbilities.AttachedSphere(WeaponHave.BattleWeaponRecord);
            if (sphere == Items.divinebeasttitle) return Items.divinebeasttitle;
            if (sphere == Items.angelshooter || sphere == Items.angelgear) return SuperSteveAngelKey;
            return -1;
        }
        private static int _weapon = -1;                                    // the weapon the resident copy was built for
        private static readonly string[] WingMeshNodes = { "cat_rwingm", "cat_lwingm" };   // build_cat_pack / wing_bake.MESH_NAMES
        private const string MaskNodeName = "cat_mask";                                    // wing_bake.MASK_NODE — the Super Steve
        private static int _maskMeshIdx = -1;                                              // cat's domino mask, rigid to cat_kao
        private const string CapeNodeName = "cat_cape", CapeAnchorName = "cat_sebone2";     // wing_bake.CAPE_NODE / cat_wings.CAPE_ANCHOR
        /// <summary>The cape's body-collision capsules, in the order the .clo lists them — IN STEP WITH cat_wings.CAPE_BOUNDS.
        /// Every BOUND in the record names the cape node (the only name that resolves in HER tree at load); at spawn each cloned
        /// CBound is re-pointed at the cat copy's own bone, so the capsules ride the cat's spine and the cape drapes over it.</summary>
        private static readonly string[] CapeBoundBones = { "cat_sebone2", "cat_sebone1", "cat_kosibone", "cat_kao" };
        private static readonly List<int> _wingMeshIdx = new List<int>();
        private const float  GlowLift  = 0f;        // units added to the glow's height (negative lowers it; user 2026-09-12: the centre sat just above the cat)
        private const float  GlowPull  = 5.0f;         // how far toward the camera the sprite is pulled (user 2026-09-12)         // how far toward the camera the sprite is pulled (the torches use 15 to clear their wall; the cat only needs to clear its own body)
        private const float  HeadFallbackHeight = 6f;
        private static bool  _hitDone;
        private static int   _fade;
        private static readonly List<(int idx, int ticks, bool native)> _planted = new();   // native = planted by the cave (the cat's hit)
        private static bool _hitFade;                        // the hit landed: the flight follows through while the cat fades out
        private static int  _aimLoggedFor = -1;              // last target the aim choice was logged for

        // ── the PAUSE screen (user 2026-09-13: the cat used to vanish; it should wait) ──
        // The mod's clocks are wall-clock; the copy is a chara-slot character the engine keeps stepping on the pause screen
        // (Player.CheckDunIsPausedOrMenu's note). So on entry the copy's motion is STOPPED (flags bit 0: rate 0, blend
        // increment 0 — it holds its frame) and the clock offset grows by the pause; nothing else ticks. The cave hooks the
        // shot-pool step, which the pause screen does not run.
        private static DateTime _pausedAt = DateTime.MinValue;
        private static TimeSpan _pauseOffset = TimeSpan.Zero;
        private static float _pausedBlend = -1f;                              // the channel's blend increment before the stop (the stop zeroes it for good)
        private static DateTime Now => DateTime.UtcNow - _pauseOffset;       // the cat's clock: stands still through the PAUSE screen
        private const int MotionStop = 0x1;                                   // CCharacter.MotionFlags bit 0 (CharacterAddresses: stop)
        private static void FreezeForPause()
        {
            if (_pausedAt != DateTime.MinValue) return;
            _pausedAt = DateTime.UtcNow;
            _pausedBlend = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateSpeed);   // read BEFORE the stop: Step writes 0 there while stopped
            long f = SlotAddr() + CCharacter.MotionFlags;
            Memory.WriteInt(f, Memory.ReadInt(f) | MotionStop);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"paused — cat held (blend increment {_pausedBlend:F3})");
        }
        private static void Resume()
        {
            if (_pausedAt == DateTime.MinValue) return;
            _pauseOffset += DateTime.UtcNow - _pausedAt; _pausedAt = DateTime.MinValue;
            if (Active)
            {
                long f = SlotAddr() + CCharacter.MotionFlags; Memory.WriteInt(f, Memory.ReadInt(f) & ~MotionStop);
                // The stop left the blend increment at 0 and nothing re-seeds it (MOTION_END does so at load only): the next
                // key cross-fade would never finish — the cat slid along frozen in its last pose (user 2026-09-13, "the walking
                // motion isn't animating"). Put back what it was, or the engine's default.
                Memory.WriteFloat(CodeCaves.MotionCave + MotionType.StateSpeed, _pausedBlend > 0f ? _pausedBlend : BlendDefault);
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"resumed — clocks held for {_pauseOffset.TotalSeconds:F1} s in all");
        }

        /// <summary>The glow disc for this weapon: written where the glow cave reads it (CatGlowName), and the cave is told
        /// to bind again (CatGlowReady = 0).</summary>
        private static void WriteGlowName()
        {
            byte[] nm = new byte[16]; Encoding.ASCII.GetBytes(_look.Glow).CopyTo(nm, 0);
            Memory.WriteBytesBatch(CodeCaves.Mailbox.CatGlowName, nm);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowReady, 0);
        }

        /// <summary>Meshes this look does not wear — the wings on a wingless weapon, the mask on anything but Super Steve. They
        /// are all SKINNED frames, so nulling their geometry alone is unsafe (MotionProc2 writes every mesh's .wgt run through
        /// its visual each frame). So the copy's channel gets a PRIVATE clone of the skin list (the same 0x18-byte nodes the
        /// .mot list uses: {mesh, bone, type 20, count, keys, next}) with those meshes' runs left out — the keys still point at
        /// her data, only the chain is ours — and THEN their geometry pointers are cleared so nothing draws them. Her own list
        /// is untouched. One pass for all of them, so the clone is built once.</summary>
        private const int SkinNodeSize = 0x18;
        private static void HideMeshes(List<int> hide, string what)
        {
            if (hide.Count == 0) return;
            long chan = CodeCaves.MotionCave;                                              // the copy's channel struct
            uint head = (uint)Memory.ReadInt(chan + MotionType.MotionSkinList) & Memory.PhysAddrMask;
            var nodes = new List<byte[]>();
            for (uint p = head; Memory.IsValidGuest(p) && nodes.Count < 256;)
            {
                byte[] n = Memory.ReadBytesBatch(Memory.ToMmu(p), SkinNodeSize);
                if (n == null) break;
                nodes.Add(n);
                p = (uint)BitConverter.ToInt32(n, 0x14) & Memory.PhysAddrMask;
            }
            if (nodes.Count == 0) { Console.WriteLine(Tag + $"{what}-off: the copy's skin list is unreadable — they stay visible"); return; }
            var keep = new List<byte[]>();
            foreach (byte[] n in nodes) if (!hide.Contains(BitConverter.ToInt32(n, 0))) keep.Add(n);
            if (keep.Count == nodes.Count) { Console.WriteLine(Tag + $"{what}-off: no such runs in the skin list — they stay visible"); return; }
            long cave = TakeCave(keep.Count * SkinNodeSize, out uint caveG);
            if (cave == 0) { Console.WriteLine(Tag + $"{what}-off: no cave room for the skin list clone — they stay visible"); return; }
            for (int i = 0; i < keep.Count; i++)
            {
                BitConverter.GetBytes(i + 1 < keep.Count ? caveG + (uint)((i + 1) * SkinNodeSize) : 0u).CopyTo(keep[i], 0x14);
                Memory.WriteBytesBatch(cave + i * SkinNodeSize, keep[i]);
            }
            Memory.WriteUInt(chan + MotionType.MotionSkinList, caveG);
            foreach (int i in hide) Memory.WriteUInt(CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride + CFrameVu1.GeomPtr, 0);
            Console.WriteLine(Tag + $"{what} hidden: skin list {nodes.Count} → {keep.Count} runs (private clone at 0x{caveG:X}), {hide.Count} geometry pointers cleared");
        }

        // ── the Super Steve cape: the engine's cloth (CCloth 0x8550), built by HER pack load from the baked cat_cape node + catcape.clo
        // (the .clo's FRAME finds the node in her tree; its bind 3×3 is HIDE_SCALE'd so on her the cloth is a point) and CLONED onto
        // the cat copy here, CharacterClone.CopyCloth's recipe: the whole object, two private draw packets (the engine rebuilds the
        // packet from the particles every draw), the anchor re-pointed at the copy's cat_sebone2 (the rest lattice was authored in
        // that bone's space), no body capsules for now, the Verlet "previous" seeded from "current" so the first step is quiet, and
        // the copy's +0xC74 pointing at a one-entry list. Her own list entry is zeroed so she neither steps nor draws it. The
        // dungeon chara loop steps every slot's cloth while MirageSceneGateFlag == 1 (the Mirage pnach's ClothStep swap), which
        // the cat already sets; Draw__10CCharacter draws the list. ──
        private static long _capeObj;
        private static uint _capeTemplate;                                    // her CCloth for cat_cape, taken out of her draw list by TakeHerCape
        private static int _capeSweepTick;

        /// <summary>The cape's cloth record lives in HER pack, so the engine builds it for XIAO and hangs it off her own cloth
        /// list — anchored to the hidden cat_cape node at her origin, where it draws as a sheet at her feet whether or not the cat
        /// is out. Take it out of her list the moment it appears (every reload rebuilds it) and keep the object as the template
        /// the cat's copy is cloned from.</summary>
        private static void TakeHerCape()
        {
            uint herList = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.ClothList) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(herList)) return;
            for (int i = 0; i < CCloth.ClothMaxPieces; i++)
            {
                uint obj = (uint)Memory.ReadInt(Memory.ToMmu(herList) + i * 4) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(obj)) continue;
                uint frame = (uint)Memory.ReadInt(Memory.ToMmu(obj) + CCloth.ClothAttach) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(frame) || ReadName(frame) != CapeNodeName) continue;
                Memory.WriteInt(Memory.ToMmu(herList) + i * 4, 0);
                if (_capeTemplate != obj) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cape: took her own copy out of her cloth list (entry {i}, 0x{obj:X}) — it is the clone template");
                _capeTemplate = obj;
                return;
            }
        }
        private static int  _capeWatch;
        private static void SpawnCape()
        {
            _capeObj = 0;
            TakeHerCape();                                                        // a reload rebuilds her copy: re-cache it first
            uint herList = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.ClothList) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(herList)) { Console.WriteLine(Tag + "cape: she has no cloth list — is the ISO patched with the cape?"); return; }
            uint template = 0; int entry = -1;
            for (int i = 0; i < CCloth.ClothMaxPieces; i++)
            {
                uint obj = (uint)Memory.ReadInt(Memory.ToMmu(herList) + i * 4) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(obj)) continue;
                uint frame = (uint)Memory.ReadInt(Memory.ToMmu(obj) + CCloth.ClothAttach) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(frame) && ReadName(frame) == CapeNodeName) { template = obj; entry = i; break; }
            }
            if (template == 0 && Memory.IsValidGuest(_capeTemplate))              // cleared from her list by an earlier spawn: still hers, still intact
            {
                uint frame = (uint)Memory.ReadInt(Memory.ToMmu(_capeTemplate) + CCloth.ClothAttach) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(frame) && ReadName(frame) == CapeNodeName) template = _capeTemplate;
            }
            if (template == 0) { Console.WriteLine(Tag + "cape: no cloth anchored to " + CapeNodeName + " in her list — is the ISO patched with the cape?"); return; }
            {
                int i = entry; uint obj = template;
                byte[] o = Memory.ReadBytesBatch(Memory.ToMmu(obj), CCloth.ClothObjSize);
                if (o == null) { Console.WriteLine(Tag + "cape: template read failed"); return; }
                int wide = BitConverter.ToInt32(o, 0x2C), hang = BitConverter.ToInt32(o, 0x30);   // outer (across the back) × inner (down the cape; index 0 pinned)
                uint b0 = (uint)BitConverter.ToInt32(o, CCloth.ClothBuf0) & Memory.PhysAddrMask, b1 = (uint)BitConverter.ToInt32(o, CCloth.ClothBuf0 + 4) & Memory.PhysAddrMask;
                // How big a draw packet this cloth builds. Take it from the cloth's OWN figure (+0x1C, what CreateVUData returned
                // at init, in 16-byte units) and never from a guess: the packet is rebuilt into these buffers from scratch every
                // draw, so one byte short is an overrun straight through the rest of the cave. A 12 × 16 lattice needs 17,760 B and
                // a hardcoded 0x2000 fallback handed it 8,192 — the engine wrote 9.5 KB past the end and the game jumped into
                // garbage (user 2026-09-14). The pointer gap is only a cross-check; the packet figure wins.
                int packet = BitConverter.ToInt32(o, CCloth.ClothPacketUnits) * 16;
                int gap = (int)(b1 - b0);
                int bufSize = Math.Max(packet, gap > 0 && gap < 0x20000 ? gap : 0);
                bufSize = (bufSize + 0x3F) & ~0x3F;
                if (packet <= 0 || bufSize > 0x20000)
                {
                    Console.WriteLine(Tag + $"cape: refusing to clone — packet {packet} B, pointer gap {gap} B, neither is a sane buffer size");
                    return;
                }
                long cObj = TakeCave(CCloth.ClothObjSize, out uint cObjG);
                long cB0 = TakeCave(bufSize, out uint cB0G), cB1 = TakeCave(bufSize, out uint cB1G), cList = TakeCave(16, out uint cListG);
                if (cObj == 0 || cB0 == 0 || cB1 == 0 || cList == 0) { Console.WriteLine(Tag + "cape: no cave room — no cape"); return; }
                int anchor = NodeIndexOf(CapeAnchorName);
                if (anchor < 0) { Console.WriteLine(Tag + "cape: no " + CapeAnchorName + " in the copy — no cape"); return; }
                BitConverter.GetBytes(cB0G).CopyTo(o, CCloth.ClothActive);
                BitConverter.GetBytes(cB0G).CopyTo(o, CCloth.ClothBuf0);
                BitConverter.GetBytes(cB1G).CopyTo(o, CCloth.ClothBuf0 + 4);
                BitConverter.GetBytes((uint)(CodeCaves.NodePoolGuest + anchor * CFrameVu1.NodeStride)).CopyTo(o, CCloth.ClothAttach);
                uint bounds = CloneBounds((uint)BitConverter.ToInt32(o, CCloth.ClothBounds) & Memory.PhysAddrMask, out int nb);
                BitConverter.GetBytes(bounds).CopyTo(o, CCloth.ClothBounds);                 // the cat's own body capsules
                BitConverter.GetBytes(0).CopyTo(o, 0x50);                                    // wind: the step refreshes it from the character
                Array.Copy(o, 0x1110, o, 0x2110, 0x1000);                                    // previous = current: a quiet first step
                Memory.WriteBytesBatch(cObj, o);
                Memory.WriteBytesBatch(cB0, Memory.ReadBytesBatch(Memory.ToMmu(b0), bufSize) ?? new byte[bufSize]);
                Memory.WriteBytesBatch(cB1, Memory.ReadBytesBatch(Memory.ToMmu(b0), bufSize) ?? new byte[bufSize]);
                byte[] list = new byte[16]; BitConverter.GetBytes(cObjG).CopyTo(list, 0);
                Memory.WriteBytesBatch(cList, list);
                Memory.WriteUInt(SlotAddr() + CCharacter.ClothList, cListG);
                if (i >= 0) Memory.WriteInt(Memory.ToMmu(herList) + i * 4, 0);                 // she neither steps nor draws the template
                _capeObj = cObj; _capeTemplate = obj; _capeWatch = 0; _capeWide = wide; _capeHang = hang;
                // The wind's taper watches ONE particle — the middle of the hem, the point that swings furthest from the shape the
                // cape is meant to hold. Its slot is (column × 0x100 + row × 0x10), and BOTH indices come from the cloth itself, so
                // re-tessellating the cape in the bake cannot leave the runtime watching some point up its middle.
                _capeHemParticle = (_capeWide / 2) * 0x100 + (_capeHang - 1) * 0x10;
                _capeRest = Memory.ReadBytesBatch(cObj + CCloth.ClothRest, _capeWide * 0x100);   // read ONCE: the wind rides on it
                if (_capeRest != null)                                                   // its own length, for the lift's geometry
                {
                    int mid = (_capeWide / 2) * 0x100;
                    _capeSpan = Math.Abs(BitConverter.ToSingle(_capeRest, mid) - BitConverter.ToSingle(_capeRest, mid + (_capeHang - 1) * 0x10));
                }
                TintCape();
                string V(int off) => $"({BitConverter.ToSingle(o, off):F2},{BitConverter.ToSingle(o, off + 4):F2},{BitConverter.ToSingle(o, off + 8):F2})";
                Console.WriteLine(Tag + $"cape physics: K {V(CCloth.ClothK)} gravity {V(CCloth.ClothGravity)} follow {V(CCloth.ClothFollow)} wind {BitConverter.ToSingle(o, CCloth.ClothWindScale):F2} normal {BitConverter.ToSingle(o, CCloth.ClothNormal):F2} floor {BitConverter.ToSingle(o, 0x4C):F1} (flag {BitConverter.ToInt32(o, 0x48)})");
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cape: {wide} wide × {hang} down cloth cloned from 0x{obj:X} → 0x{cObjG:X} (buffers 0x{bufSize:X} ×2 for a {packet} B packet, her gap was 0x{gap:X}), anchored to the copy's {CapeAnchorName} (n{anchor}), {nb} body capsule(s) on {string.Join("/", CapeBoundBones.Take(nb))}{(i >= 0 ? $"; her entry {i} cleared" : "")}");
            }
        }

        /// <summary>The index of a bone in the cat copy's node pool, or −1.</summary>
        private static int NodeIndexOf(string name)
        {
            for (int k = 0; k < _nodeCount; k++) if (ReadName((uint)(CodeCaves.NodePoolGuest + k * CFrameVu1.NodeStride)) == name) return k;
            return -1;
        }

        /// <summary>Clone the template cape's CBound chain into the cave, re-pointing capsule <i>i</i> at the cat copy's
        /// <see cref="CapeBoundBones"/>[i] (in the .clo's own order) so the cloth collides with the cat instead of her. Returns the
        /// guest pointer to the head of the new chain (0 = none), and how many capsules it holds.</summary>
        private static uint CloneBounds(uint bnd, out int count)
        {
            uint head = 0; long prev = 0; count = 0;
            while (Memory.IsValidGuest(bnd) && count < CapeBoundBones.Length)
            {
                byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(bnd), CBound.BoundSize);
                if (b == null) break;
                uint next = (uint)BitConverter.ToInt32(b, CBound.BoundNext) & Memory.PhysAddrMask;
                int bone = NodeIndexOf(CapeBoundBones[count]);
                if (bone < 0) { Console.WriteLine(Tag + "cape: no " + CapeBoundBones[count] + " in the copy — capsule skipped"); break; }
                long cB = TakeCave(CBound.BoundSize, out uint cBG);
                if (cB == 0) { Console.WriteLine(Tag + "cape: no cave room for the body capsules"); break; }
                BitConverter.GetBytes((uint)(CodeCaves.NodePoolGuest + bone * CFrameVu1.NodeStride)).CopyTo(b, CBound.BoundFrameA);
                BitConverter.GetBytes(0).CopyTo(b, CBound.BoundFrameB);                       // A alone carries both endpoints
                BitConverter.GetBytes(0).CopyTo(b, CBound.BoundNext);                         // the chain is re-linked below
                Memory.WriteBytesBatch(cB, b);
                if (prev != 0) Memory.WriteUInt(prev + CBound.BoundNext, cBG); else head = cBG;
                prev = cB; count++; bnd = next;
            }
            return head;
        }

        /// <summary>Diagnostics while the cape is up: is it being stepped (particle 0 leaves its rest-local spot for the world) and
        /// drawn (the active packet pointer flips between the two buffers)? Logged every ~1 s.</summary>
        private static void WatchCape()
        {
            if (_capeObj == 0 || ++_capeWatch % 60 != 0) return;
            byte[] o = Memory.ReadBytesBatch(_capeObj, 0x1120 + 0x700);
            if (o == null) return;
            string P(int off) => $"({BitConverter.ToSingle(o, off):F2},{BitConverter.ToSingle(o, off + 4):F2},{BitConverter.ToSingle(o, off + 8):F2})";
            uint active = (uint)BitConverter.ToInt32(o, CCloth.ClothActive), anchor = (uint)BitConverter.ToInt32(o, CCloth.ClothAttach);
            long s = SlotAddr();
            // the lattice: slot = a*16 + b — a = width (0..4 across the collar), b = hang (0 = the collar edge, 6 = the hem)
            Console.WriteLine(Tag + $"cape watch: cur a0b0 {P(0x1110)} a0b1 {P(0x1120)} a0b6 {P(0x1170)} a4b0 {P(0x1510)} a4b6 {P(0x1570)} | rest p0 {P(0x110)} p1 {P(0x120)} p16 {P(0x210)} | anchor-centroid {P(0xF0)} local {P(0x100)} active 0x{active:X}");
            uint bnd = (uint)BitConverter.ToInt32(o, CCloth.ClothBounds) & Memory.PhysAddrMask;
            var caps = new List<string>();
            while (Memory.IsValidGuest(bnd) && caps.Count < 4)
            {
                byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(bnd), CBound.BoundSize);
                if (b == null) break;
                caps.Add($"{ReadName((uint)BitConverter.ToInt32(b, CBound.BoundFrameA) & Memory.PhysAddrMask)} c({BitConverter.ToSingle(b, CBound.BoundCentre):F1},{BitConverter.ToSingle(b, CBound.BoundCentre + 4):F1},{BitConverter.ToSingle(b, CBound.BoundCentre + 8):F1}) r({BitConverter.ToSingle(b, CBound.BoundRadii):F1},{BitConverter.ToSingle(b, CBound.BoundRadii + 4):F1},{BitConverter.ToSingle(b, CBound.BoundRadii + 8):F1})");
                bnd = (uint)BitConverter.ToInt32(b, CBound.BoundNext) & Memory.PhysAddrMask;
            }
            Console.WriteLine(Tag + "cape watch: capsules " + (caps.Count == 0 ? "none" : string.Join(" | ", caps)));
            // how far each particle is from the rest shape the engine is pulling it to (+0x7550 = LW(anchor) × rest, refreshed every step)
            byte[] tg = Memory.ReadBytesBatch(_capeObj + CCloth.ClothTarget, 0x80);
            if (tg != null)
            {
                float Sag(int b)
                {
                    float dx = BitConverter.ToSingle(tg, b * 16) - BitConverter.ToSingle(o, 0x1110 + b * 16);
                    float dy = BitConverter.ToSingle(tg, b * 16 + 4) - BitConverter.ToSingle(o, 0x1114 + b * 16);
                    float dz = BitConverter.ToSingle(tg, b * 16 + 8) - BitConverter.ToSingle(o, 0x1118 + b * 16);
                    return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                Console.WriteLine(Tag + $"cape watch: column 0 off the rest shape by b1 {Sag(1):F2} b3 {Sag(3):F2} b6 {Sag(6):F2} | b6 target ({BitConverter.ToSingle(tg, 0x60):F1},{BitConverter.ToSingle(tg, 0x64):F1},{BitConverter.ToSingle(tg, 0x68):F1})");
            }
            byte[] lw = Memory.ReadBytesBatch(Memory.ToMmu(anchor) + CFrameVu1.WorldMatrix, 0x40);
            string rows = lw == null ? "?" : string.Join(" | ", new[] { 0, 1, 2, 3 }.Select(r => $"({BitConverter.ToSingle(lw, r * 16):F2},{BitConverter.ToSingle(lw, r * 16 + 4):F2},{BitConverter.ToSingle(lw, r * 16 + 8):F2},{BitConverter.ToSingle(lw, r * 16 + 12):F2})"));
            Console.WriteLine(Tag + $"cape watch: anchor LW rows {rows} | cat at ({Memory.ReadFloat(s + CCharacter.CharPos):F1},{Memory.ReadFloat(s + CCharacter.CharPos + 4):F1},{Memory.ReadFloat(s + CCharacter.CharPos + 8):F1}) yaw {Memory.ReadFloat(s + CCharacter.CharRotY):F2} scale {Memory.ReadFloat(s + CCharacter.CharScale):F2} opacity {Memory.ReadFloat(s + CCharacter.NpcOpacity):F0}");
        }

        /// <summary>The wind, as a SHAPE rather than a force.
        ///
        /// It was a force for a long time — GRAVITY aimed from the cat's face, since the engine's own wind is undirected noise —
        /// and every version of it bunched the cape. The reason is structural: the collar edge is pinned, so the sheet cannot move
        /// downwind bodily, and its displacement from the rest shape therefore grows from nothing at the collar to everything at
        /// the hem. The spring pulls each particle toward its OWN place in that shape, so it pulls hardest exactly where the wind
        /// has carried the cloth furthest. The tail is hauled back while the middle is still being pushed out, the sheet goes into
        /// compression along its length, and a sheet in compression buckles. No amount of tuning removes that: the wind and the
        /// spring are pulling against each other by construction, and the crumple lives between them (user 2026-09-14, a cape
        /// crushed into a vertical spike).
        ///
        /// So move the target instead of pushing the cloth. Each tick the REST SHAPE is rewritten as the blown shape — every row
        /// carried back along the cape and lifted off the back, eased in along the hang so the collar stays put and the hem moves
        /// most, with the travelling ripple on top. The spring now has nothing to fight: it pulls the cloth toward a shape that is
        /// already streaming, which is the shape we want it in, and what is left for the simulation is exactly what it is good at
        /// — lag, collision against the body, and the swing when the cat moves. GRAVITY stays zero.
        ///
        /// Everything is in the ANCHOR's frame, so it follows the cat's spine without any facing maths: −x runs down the cape away
        /// from the collar, −y lifts off the back.</summary>
        // The sheet cannot LENGTHEN — its rest distances were fixed from the baked lattice when the cloth loaded, and the distance
        // constraints enforce them. A row that rises therefore has to draw IN toward the collar by as much as the geometry demands,
        // or the target sits further away than the cloth can reach and the lift spends itself pulling against those constraints.
        // That trim is not a constant: a row d down the cape, raised by l, spans √(d² − l²), so it is computed per row from the
        // cape's own measured length. Raise the lift as far as you like and the shape stays reachable.
        private const float CapeWindLift = 2.6f;         // how far the hem flies off the back — this is the wind's real strength
        private const float CapeWindEase = 1.6f;         // the profile along the hang: > 1 keeps the shoulders down and flies the tail
        // A torn sheet is measured BY THE SHEET, not by where the cat is. Distance from the rest shape does not distinguish the
        // two: a cat riding its pellet covers 5 units a frame, so the cloth legitimately trails far behind it — testing that put
        // the cape into a reseat 16 times a second for the whole flight, which is its own kind of stretching (user 2026-09-14).
        // The cape's own collar-to-hem span is about 5 units and the distance constraints hold it near that however fast the cat
        // moves; only a sheet that has been left behind by a teleport, with its pinned edge snapped away from the rest, spans the
        // room. So: compare the cloth against itself.
        private const float CapeTearSpan = 20f;          // collar corner to mid-hem. Its real span is ~5 units and a hard flight
                                                         // stretches it to maybe 10 while the 4 constraint passes catch up;
                                                         // the tears in the log were 40–54, so this sits clear of both
        private static int _capeTearLog;
        private static int  _capeHemParticle;                                // the hem's middle, from the cloth's own dimensions
        private static byte[] _capeRest;                                     // the rest shape as baked — the wind's baseline
        private static float _capeSpan;                                      // collar to hem along the cape, measured from that shape
        // The gust that runs down it. A single force on the whole sheet cannot ripple — it moves every particle at once, and what
        // that produces is the sheet rocking from one side to the other, so the wave has to be written per particle.
        //
        // It goes into the REST SHAPE (+0x110), not the velocity array, and that distinction matters more than it looks. The rest
        // is the only per-particle field the engine reads and never writes during play, so a wave written there cannot race it:
        // the cloth simply chases a shape that is already rippling, and the spring's own lag smooths the result. Writing the
        // VELOCITY instead means reading 3 KB the engine owns, computing, and writing it back several engine steps later — every
        // tick clobbers the integrator with stale values, and on the frame the cat binds to its pellet the engine ZEROES those
        // velocities for the teleport and we hand the pre-teleport ones straight back. That is a cloth explosion, and it is what
        // stretched the cape across the screen when the cat was fired (user 2026-09-14). It also halves the traffic: the baseline
        // is read once at spawn, so the tick only writes.
        private const float CapeRippleAmp = 0.65f;       // units of swell at the crest, once the swell is at full strength
        private const float CapeRippleReach = 0.5f;      // how far down the cape it gets there: the swell ramps from nothing at the
                                                        // pinned collar to full at this fraction, and holds full over the rest. It
                                                        // has to ramp at all for two reasons — a pinned sheet flutters least at its
                                                        // pinned end, and a swell larger than a row's own distance from the collar
                                                        // would make the draw-in geometry below collapse that row onto it
        // Wavelength matters for BUNCHING as much as for looks: neighbouring rows differ in velocity by roughly the amplitude
        // times 2π/wavelength, and where that difference points them at each other the sheet is in compression and buckles — the
        // pile-up at the hem (user 2026-09-14). A longer wave flattens that gradient; the rest spring (CAPE_PHYSICS K) is the
        // other half of the answer, since each particle is pulled toward its OWN place in the shape and that restores spacing.
        private const float CapeRippleRows = 10.0f;      // rows per wavelength — over one wavelength the cape shows a single swell
        private const float CapeRippleSeconds = 0.5f;    // one crest, collar to hem
        private static double _ripplePhase;
        private static int _capeWide, _capeHang;      // the cloth's own dimensions: across the back × down the cape
        private static void BreezeCape()
        {
            if (_capeObj == 0 || _capeRest == null) return;
            if (_capeWide <= 0 || _capeWide > 16 || _capeHang <= 1 || _capeHang > 16) return;   // the engine's grid is 16 × 16
            byte[] cur = Memory.ReadBytesBatch(_capeObj + CCloth.ClothCur + _capeHemParticle, 12);
            byte[] pin = Memory.ReadBytesBatch(_capeObj + CCloth.ClothCur, 12);       // the pinned corner: the sheet's own anchor end
            if (cur != null && pin != null)
            {
                float sx = BitConverter.ToSingle(cur, 0) - BitConverter.ToSingle(pin, 0);
                float sy = BitConverter.ToSingle(cur, 4) - BitConverter.ToSingle(pin, 4);
                float sz = BitConverter.ToSingle(cur, 8) - BitConverter.ToSingle(pin, 8);
                float span = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (span > CapeTearSpan || float.IsNaN(span))
                {
                    if (--_capeTearLog <= 0) { _capeTearLog = 60; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cape: torn — {span:F0} units from collar to hem; reseating the cloth"); }
                    ReseedCape();
                    return;
                }
            }
            StiffenCape();
            _ripplePhase += 2 * Math.PI * (TickMs / 1000.0) / CapeRippleSeconds;
            byte[] rest = (byte[])_capeRest.Clone();
            for (int b = 1; b < _capeHang; b++)                                       // b = 0 is the pinned collar edge: leave it
            {
                float t = (float)b / (_capeHang - 1);
                float f = (float)Math.Pow(t, CapeWindEase);                            // nothing at the collar, everything at the hem
                float swell = CapeRippleAmp * Math.Min(1f, t / CapeRippleReach);
                float lift = CapeWindLift * f + swell * (float)Math.Sin(_ripplePhase - b * 2 * Math.PI / CapeRippleRows);
                float d = _capeSpan * t;                                               // how far down the cape this row sits
                float trim = d - (float)Math.Sqrt(Math.Max(0f, d * d - lift * lift));  // …and how much it must draw in to rise that far
                for (int a = 0; a < _capeWide; a++)
                {
                    int o = a * 0x100 + b * 0x10;
                    BitConverter.GetBytes(BitConverter.ToSingle(_capeRest, o) + trim).CopyTo(rest, o);          // +x = back toward the collar
                    BitConverter.GetBytes(BitConverter.ToSingle(_capeRest, o + 4) - lift).CopyTo(rest, o + 4);  // −y = up off the back
                }
            }
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothRest, rest);
        }

        /// <summary>Stiff across the cape, soft along it — so the wind can lift it and run a ripple down its length while the
        /// flare keeps its width instead of collapsing into a rectangle.
        ///
        /// The spring the engine pulls each particle home with is a VECTOR (CCloth +0xE0/+0xE4/+0xE8), applied component-wise to
        /// the correction, not a single number — so it can be stiff on one axis and slack on another. The catch is that those are
        /// WORLD axes, and the cat turns, so a constant would put the stiff axis across the cape only while the cat happened to
        /// face one way. Instead it is rebuilt every tick from the cat's own facing: the stiff value goes on the axis that is
        /// currently sideways-on to the cat and the slack one on its fore-and-aft axis, mixed by the squares of the facing so the
        /// pair rotates smoothly through the diagonals (an axis-aligned diagonal is all the engine can express — the off-diagonal
        /// terms of the true rotated tensor have nowhere to go, and at 45° the two values simply meet in the middle).
        ///
        /// The baked .clo carries <see cref="CapeSpringAlong"/> as its seed, which is what the cloth uses for the frame or two
        /// before the first tick lands.</summary>
        private const float CapeSpringSide = 0.42f;      // across the cape: holds the width, and with it the authored flare
        private const float CapeSpringAlong = 0.12f;     // along it: low, so the wind can lift the sheet and carry a wave down it
        private const float CapeSpringUp = 0.16f;        // vertical: between the two — it fights the lift, but also the sagging
        /// <summary>The cat's facing as a GUARANTEED unit vector. It is (0, 0) until something aims the cat, and with no target
        /// in range nothing ever does — the cave only writes a direction when it has somewhere to go. Everything here that leans
        /// on the facing must survive that: <see cref="StiffenCape"/> mixes the stiff and slack spring values by the squares of
        /// these, which only sums to the intended pair when they are normalised. Feed it (0, 0) and BOTH horizontal axes of the
        /// spring come out zero — the cape loses its restoring force entirely and wanders off, which is what "goes crazy when
        /// there's no target" was (user 2026-09-14).</summary>
        private static void Facing(out float dx, out float dz)
        {
            float l = (float)Math.Sqrt(_dirX * _dirX + _dirY * _dirY);
            if (l > 1e-3f) { dx = _dirX / l; dz = _dirY / l; } else { dx = 0f; dz = 1f; }
        }

        private static void StiffenCape()
        {
            Facing(out float fdx, out float fdz);
            float fx = fdx * fdx, fz = fdz * fdz;                                      // normalised, so fx + fz = 1 always
            byte[] k = new byte[12];
            BitConverter.GetBytes(CapeSpringAlong * fx + CapeSpringSide * fz).CopyTo(k, 0);
            BitConverter.GetBytes(CapeSpringUp).CopyTo(k, 4);
            BitConverter.GetBytes(CapeSpringAlong * fz + CapeSpringSide * fx).CopyTo(k, 8);
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothK, k);
        }

        /// <summary>The cape's own colour. The cat is lit by an ambient ADD on its CCharacter (its weapon's Tint), but a cloth is
        /// drawn on its own — CCloth::Draw builds a bare frame and hands it to MGDraw — so none of that reaches the cape, and in a
        /// dark dungeon it rendered a dull maroon beside a glowing cat (user 2026-09-14).
        ///
        /// Draw__10CCharacter saves the global ambient, adds the CHARACTER's tint (+0xCE0, what <see cref="Looks"/> sets per
        /// weapon), draws its meshes AND THEN its cloth list inside that same window, and only then restores. So a cloth is lit by
        /// whatever ambient is standing when it draws — the character's colour, never one of its own. That is why writing the
        /// cape's material colour rows did nothing, twice: the colour does not come from the material at all.
        ///
        /// ElfCave.CatCapeTint wraps that cloth-draw call. For the one cloth named here it adds this delta to the ambient for that
        /// draw alone and puts the ambient straight back, so the cape carries a red of its own while the cat keeps its blue and
        /// every other cloth in the game is untouched. The delta is ON TOP of the cat's tint, which is already in the ambient by
        /// then — so it is written as (what the cape should have) − (what the cat has), and the cat's colour does not leak in.</summary>
        // Ambient is FLAT — it lifts every part of the cape by the same amount, so the more of it there is, the less the scene's
        // own lighting (the part that varies with each particle's normal, and therefore the only part that SHOWS the ripples)
        // counts for. Enough to be clearly red in a dark dungeon, not so much that it washes the shading out.
        private static readonly float[] CapeTint = { 80f, 20f, 10f };                  // the cape's own ambient, same scale as Look.Tint —
                                                                                       // tuned against the texture in the viewer's cape panel
                                                                                       // (the cat's blue is 12/24/48)
        private static void TintCape()
        {
            if (_capeObj == 0) { Memory.WriteUInt(CodeCaves.Mailbox.CatCapeCloth, 0); return; }
            WriteCapeTint(1f);
            Memory.WriteUInt(CodeCaves.Mailbox.CatCapeCloth, (uint)(_capeObj - 0x20000000));
            Console.WriteLine(Tag + $"cape tint: ambient ({CapeTint[0]:F0},{CapeTint[1]:F0},{CapeTint[2]:F0}) for its draw alone, as a delta off the cat's ({_look.Tint[0]:F0},{_look.Tint[1]:F0},{_look.Tint[2]:F0})");
        }

        /// <summary>The cape's ambient delta, faded with the cat so the two never drift apart mid-fade.</summary>
        private static void WriteCapeTint(float lit)
        {
            for (int i = 0; i < 3; i++)
                Memory.WriteFloat(CodeCaves.Mailbox.CatCapeTint + i * 4, (CapeTint[i] - _look.Tint[i]) * lit);
        }

        /// <summary>Put the whole cloth exactly on its rest shape at the cat's new place. Called when the copy teleports — the
        /// bind to the pellet's birth frame — where the anchor jumps the length of the room in one step.
        ///
        /// The engine has its own guard for that (Step teleports the sheet bodily when the anchor's centroid moves more than 10
        /// units) but it did NOT save the cape when the cat was fired: the pinned collar edge is re-pinned to the anchor on every
        /// constraint pass, so the instant it snaps to the new position while the rest of the sheet is still at the old one, the
        /// cape is stretched the width of the screen and the constraints tear it apart from there (user 2026-09-14). Rather than
        /// work out why the heuristic misses, place every particle ourselves: current AND previous = LW(anchor) × rest, velocities
        /// zeroed, and the mark the teleport test compares against moved to match — the same state Clear__6CCloth builds, which is
        /// what the engine itself does when a cloth is reset.</summary>
        private static void ReseedCape()
        {
            if (_capeObj == 0 || _capeRest == null) return;
            uint anchor = (uint)Memory.ReadInt(_capeObj + CCloth.ClothAttach) & Memory.PhysAddrMask;
            byte[] lw = Memory.IsValidGuest(anchor) ? Memory.ReadBytesBatch(Memory.ToMmu(anchor) + CFrameVu1.WorldMatrix, 0x40) : null;
            if (lw == null) { Console.WriteLine(Tag + "cape: cannot reseed — the anchor's matrix did not read"); return; }
            float[] m = new float[16];
            for (int i = 0; i < 16; i++) m[i] = BitConverter.ToSingle(lw, i * 4);
            // row-vector convention, as everywhere in this engine: world = x·row0 + y·row1 + z·row2 + row3
            void Place(byte[] dst, int o, float x, float y, float z)
            {
                BitConverter.GetBytes(x * m[0] + y * m[4] + z * m[8] + m[12]).CopyTo(dst, o);
                BitConverter.GetBytes(x * m[1] + y * m[5] + z * m[9] + m[13]).CopyTo(dst, o + 4);
                BitConverter.GetBytes(x * m[2] + y * m[6] + z * m[10] + m[14]).CopyTo(dst, o + 8);
                BitConverter.GetBytes(1f).CopyTo(dst, o + 12);
            }
            byte[] cur = new byte[_capeWide * 0x100];
            for (int a = 0; a < _capeWide; a++)
                for (int b = 0; b < _capeHang; b++)
                {
                    int o = a * 0x100 + b * 0x10;
                    Place(cur, o, BitConverter.ToSingle(_capeRest, o), BitConverter.ToSingle(_capeRest, o + 4), BitConverter.ToSingle(_capeRest, o + 8));
                }
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothCur, cur);
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothPrev, cur);                  // no history: nothing to whip back toward
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothVel, new byte[_capeWide * 0x100]);
            byte[] c = Memory.ReadBytesBatch(_capeObj + CCloth.ClothAnchorLocal, 12);  // and the mark the teleport test compares to
            if (c != null)
            {
                byte[] w = new byte[12];
                float lx = BitConverter.ToSingle(c, 0), ly = BitConverter.ToSingle(c, 4), lz = BitConverter.ToSingle(c, 8);
                BitConverter.GetBytes(lx * m[0] + ly * m[4] + lz * m[8] + m[12]).CopyTo(w, 0);
                BitConverter.GetBytes(lx * m[1] + ly * m[5] + lz * m[9] + m[13]).CopyTo(w, 4);
                BitConverter.GetBytes(lx * m[2] + ly * m[6] + lz * m[10] + m[14]).CopyTo(w, 8);
                Memory.WriteBytesBatch(_capeObj + CCloth.ClothAnchorWorld, w);
            }
        }

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
                    int weapon = inDun ? LookKeyFor(Memory.ReadUShort(WeaponHave.BattleWeaponRecord)) : -1;   // Super Steve: by its sphere
                    bool paused = inDun && Player.CheckDunIsPaused();                       // the PAUSE screen: the world stops, the cat waits
                    bool menu = inDun && !paused && Player.CheckDunIsPausedOrMenu();        // the item menu: it can rebuild the texture manager under the copy — stand down
                    // her own copy of the cape hangs off her cloth list from the moment the model loads — whatever the weapon is
                    if (inDun && Player.CurrentCharacterNum() == XiaoId && ++_capeSweepTick >= 4) { _capeSweepTick = 0; TakeHerCape(); }
                    bool armed = Enabled && inDun && Player.CurrentCharacterNum() == XiaoId
                              && (weapon >= 0 || weapon == SuperSteveAngelKey)
                              && !menu
                              && Memory.ReadInt(DungeonScriptEvent.BtEventMode) == 0;   // a script event deletes her MOTION 1 and rebuilds textures: stand down
                    if (armed && Active && weapon != _weapon)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"weapon {_weapon} → {weapon}: rebuilding the cat with its look");
                        Despawn();
                    }
                    if (!armed)
                    {
                        if (Active) Despawn();
                        Resume();
                        _armedSince = DateTime.MinValue;
                        _holding = false; _holdSeconds = 0;
                        Array.Clear(_seenPellet, 0, _seenPellet.Length);
                    }
                    else if (paused)
                    {
                        sleep = TickMs;
                        if (Active) { FreezeForPause(); Maintain(); }                       // hold the copy's frame; keep re-asserting it
                    }
                    else
                    {
                        sleep = TickMs;
                        Resume();
                        WatchHerCatChannel();
                        if (Active && ++_texCheckTick >= 30) { _texCheckTick = 0; CheckTexturesStillOurs(); }
                        if (_armedSince == DateTime.MinValue) _armedSince = Now;
                        if (!Active && (Now - _armedSince).TotalSeconds >= 1.0) SpawnResident();   // built once, hidden — after the switch/menu has settled (textures still register for a moment)
                        TrackCharge();
                        if (_native) PollCave(); else WatchPellets();
                        if (Active) { Step(); BreezeCape(); WatchCape(); }
                    }
                    if (!paused) RetirePlanted();
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
        // The dungeon's character/weapon/effect data share ONE CDataAlloc2 pool of 210000 × 16 B vanilla (260000 × 16 =
        // 4.16 MB with DunPatches' heap raise; LoadChara2: chara @0x1F06660, weapons @0x1F06670, effects @0x1F06680; the
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
                if (!_holding) { _holding = true; _holdStart = Now; _flashed = false; }
                _holdSeconds = (Now - _holdStart).TotalSeconds;
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
            if (_spawnFailedAt != DateTime.MinValue && (Now - _spawnFailedAt).TotalSeconds < 5) return;
            _scale = 0f; _alpha = 0f; _target = -1; _pelletSlot = -1; _caveOwns = false; _disarmTicks = 0;
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            _x = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos);
            _h = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
            _y = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            _weapon = LookKeyFor(Memory.ReadUShort(WeaponHave.BattleWeaponRecord));   // the look key (Super Steve: by its sphere)
            _look = Looks.TryGetValue(_weapon, out var lk) ? lk : Looks[Items.divinebeasttitle];
            if (!Spawn()) { _spawnFailedAt = Now; return; }
            _spawnFailedAt = DateTime.MinValue;
            WriteGlowName();
            var hide = new List<int>();
            if (!_look.Wings) hide.AddRange(_wingMeshIdx);
            if (!_look.Cape && _maskMeshIdx >= 0) hide.Add(_maskMeshIdx);
            HideMeshes(hide, !_look.Wings && !_look.Cape ? "wings and mask" : !_look.Wings ? "wings" : "mask");
            if (_look.Cape) SpawnCape();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"look for weapon {_weapon}: glow {_look.Glow}, wings {(_look.Wings ? "on" : "off")} ({_wingMeshIdx.Count} wing meshes in the copy), mask {(_look.Cape ? "on" : "off")} (n{_maskMeshIdx})");
            _native = (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew;
            if (!_native && !_nativeWarned) { _nativeWarned = true; Console.WriteLine(Tag + "pellet-catcher cave not in this ISO (re-patch) — using the thread follower"); }
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }
            _phase = Phase.Resident; _phaseStart = Now; _hitDone = false; _fade = 0;
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
            Memory.WriteFloat(CodeCaves.Mailbox.CatTrackHalf, _look.Track ? 1f : 0f);   // the winged cat re-aims until halfway down from the apex
            Memory.WriteFloat(CodeCaves.Mailbox.CatLandLead, LandLeadFrames);
            Memory.WriteInt  (CodeCaves.Mailbox.CatMoveKey, KeyWalk);                // a brisk walk reads better than the run (user 2026-09-10)
            Memory.WriteFloat(CodeCaves.Mailbox.CatMoveFrac, MoveFrac);
            Memory.WriteFloat(CodeCaves.Mailbox.CatMoveAbs, MoveSpeedAbs);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatLaunch, FloatLaunchFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatStart, FloatStartFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFloatRate, CharacterMotion.MotionSpeedUseKey);   // KEY rate — FloatRate is baked into the KEY entry at spawn (see RegisterSlot)
            Memory.WriteFloat(CodeCaves.Mailbox.CatFallBlend, 1f / FallBlendSteps);
            Memory.WriteFloat(CodeCaves.Mailbox.CatFallBlendFrames, FallBlendSteps);   // the cave switches float → fall this many frames before the land clip starts
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, GlowScale);
            Memory.WriteFloat(CodeCaves.Mailbox.CatScaleMul, CatScale);          // the size the cave grows the cat to on the pellet
            _glowFade = -1;
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowFlags, GlowFlags);
            WriteGlowName();                                                      // (clears CatGlowReady: the copy's texture entries are remade per spawn — rebind)
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowPull, GlowPull);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowLift, GlowLift);
            Memory.WriteFloat(CodeCaves.Mailbox.CatBlendDefault, BlendDefault);
            Memory.WriteFloat(CodeCaves.MotionCave + MotionType.StateSpeed, BlendDefault);   // the copy's channel: a hit mid-fall can leave the slow fade armed
            Memory.WriteInt  (CodeCaves.Mailbox.CatSitKey, KeySit);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeUp, ProbeUp);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeDown, ProbeDown);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeFront, ProbeFront);
            Memory.WriteFloat(CodeCaves.Mailbox.CatProbeBack, ProbeBack);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRateBase, RateBase);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRatePerSpeed, RatePerSpeed);
            Memory.WriteFloat(CodeCaves.Mailbox.CatRateMax, RateMax);
            Memory.WriteInt  (CodeCaves.Mailbox.CatIdleKey, KeyStand);
            Memory.WriteInt  (CodeCaves.Mailbox.CatBlocked, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceRange, RangeFor(_target));
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceFrames, PounceFrames);   // per target: ApplyFlightTime
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitEntry, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitLatch, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitDamage, 0);                 // 0 = not stamped yet: the cave tests no touch until WriteHitStamps at bind (a stale 1 here, tested from the hidden copy's last-drawn head next to the enemy it had just hit, dealt a 1-damage hit on the first frame — user 2026-09-12)
            Memory.WriteInt  (CodeCaves.Mailbox.CatHitAttr, 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatKickStrength, KickStrength);
            Memory.WriteFloat(CodeCaves.Mailbox.CatKickDecay, KickDecay);
            _hitFade = false;
            Memory.WriteFloat(CodeCaves.Mailbox.CatReadyEnd, ReadyEndFrame);
            Memory.WriteInt  (CodeCaves.Mailbox.CatPounceFly, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatFloatKey, KeyFloat);
            Memory.WriteFloat(CodeCaves.Mailbox.CatReadyStart, ReadyStartFrame);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceMaxDist, RangeFor(_target) * 2f);
            long sl = SlotAddr();
            Memory.WriteFloat(sl + CCharacter.CharScale, 0f); Memory.WriteFloat(sl + CCharacter.CharScale + 4, 0f); Memory.WriteFloat(sl + CCharacter.CharScale + 8, 0f);
            Memory.WriteFloat(sl + CCharacter.NpcOpacity, 0f);
            _scale = 0f; _alpha = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = true; _disarmTicks = 0;
            _phase = Phase.Resident; _phaseStart = Now;
            Memory.WriteInt  (CodeCaves.Mailbox.CatState, 3);                   // waiting — armed
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charge complete — the cave binds the next pellet on its birth frame");
        }

        private static void DisarmCave()
        {
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }   // CatGlowOn is PollCave's (the glow may linger past the cat)
            _caveOwns = false; _disarmTicks = 0;
        }

        /// <summary>Follow the cave's state: 1 = it bound a pellet (note it, face along the pellet, log), 2 = that
        /// pellet ended (hold the last placed spot, fade, then hide again). A shot-less release disarms it.</summary>
        private static void PollCave()
        {
            if (!Active) return;
            int state = Memory.ReadInt(CodeCaves.Mailbox.CatState);
            if (_disarmTicks > 0 && --_disarmTicks == 0 && state == 3) { DisarmCave(); Hide(); Console.WriteLine(Tag + "charge released without a shot — cat stays hidden"); return; }
            if (_target >= 0 && state >= 4) WriteTargetAim();                                                  // the aim point follows the target's body every tick
            // A lock-on made after the cat picked its target wins (user 2026-09-12: it kept a far target): checked every ~0.5 s
            // while walking or crouched. A closed mimic keeps the cat crouched (the cave loops the ready clip on CatHoldReady).
            if (_target >= 0 && (state == 6 || state == 10) && ++_retargetTick >= 30) { _retargetTick = 0; RetargetToLockOn(state); }
            // Hold the crouch while the target cannot be hit: a chest-mimic still shut, or any enemy inside its invincibility
            // frames (`_STATUS_SET_MUTEKI`: 9 after a hit, 100 when a mimic wakes, 1000 dying) — CheckDmg skips every hit then.
            if (state == 1 || state == 4 || state == 5 || state == 8 || state == 11) CrushGuardsNearCat();
            bool hold = _target >= 0 && state >= 4 && (IsUnopenedMimic(_target) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.HitStunTimer)) > 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatHoldReady, hold ? 1 : 0);
            if (hold != _holdLogged)
            {
                _holdLogged = hold;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (hold ? $"target slot {_target} cannot be hit yet (shut mimic / invincibility frames) — crouching until it can" : "target hittable — leaping"));
                // Released: a chest-mimic's init label ran when it woke (its guard windows — the disc bake makes the wake a
                // guard — are registered only now); crush them for this flight so the leap lands through the guard.
                if (!hold && _target >= 0) CrushGuard(_target, again: true);
            }
            // The glow follows the cat's visibility. While the cat fades out (hit or 20 s expiry: opacity over FadeTicks) the glow
            // SHRINKS on its own, longer clock (GlowFadeTicks) — so it lingers a beat where the cat vanished (user 2026-09-12;
            // dimming it through the torch tint global did not take, so size is the fade). Its clock starts with the fade
            // and keeps running past Hide(); a new bind resets it.
            // Once the shrink has run out the glow stays OFF until the next bind resets _glowFade: PollCave runs before Step in
            // the tick, so snapping back to "follow the cat" here showed one full-size frame before Step hid the cat (user 2026-09-12).
            bool fading = _hitFade || _phase == Phase.Fading;
            if (fading && _glowFade < 0) _glowFade = 0;
            if (_glowFade >= 0 && _glowFade < GlowFadeTicks) _glowFade++;
            bool shrinking = _glowFade >= 0 && _glowFade < GlowFadeTicks, done = _glowFade >= GlowFadeTicks;
            bool glow = !done && ((state != 0 && state != 3) || shrinking);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, glow ? 1 : 0);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, GlowScale * (shrinking ? 1f - _glowFade / (float)GlowFadeTicks : done ? 0f : 1f));
            int ent = Memory.ReadInt(CodeCaves.Mailbox.CatHitEntry);
            if (ent != 0)                                                        // the cave planted a damage entry at a contact (the pellet's own recipe)
            {
                Memory.WriteInt(CodeCaves.Mailbox.CatHitEntry, 0);
                lock (_planted) _planted.Add((ent - 1, PlantedLifeTicks, true));
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cat contact — damage entry {ent - 1} planted natively (base {Memory.ReadInt(CodeCaves.Mailbox.CatHitDamage)}, attr 0x{Memory.ReadInt(CodeCaves.Mailbox.CatHitAttr):X})");
            }
            if (state >= 4 && state <= 11 && state != 9)                        // every cave-owned state: face the cave's live direction (it re-aims in the ready crouch, the float wind-up and the take-off)
            {
                float ddx = Memory.ReadFloat(CodeCaves.Mailbox.CatDirX), ddz = Memory.ReadFloat(CodeCaves.Mailbox.CatDirZ);
                if (ddx * ddx + ddz * ddz > 1e-6f) { _dirX = ddx; _dirY = ddz; _yaw = (float)Math.Atan2(ddx, ddz); }
            }
            switch (state)
            {
                case 1:
                    if (_phase != Phase.Flying)
                    {
                        long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
                        int slot = Memory.ReadInt(CodeCaves.Mailbox.CatPelletSlot) - 1;
                        if (!Memory.IsValidGuest(pool) || slot < 0) break;
                        _pool = pool; _pelletSlot = slot; _alpha = 1f; _fade = 0; _glowFade = -1; _caveOwns = true; _disarmTicks = 0;
                        ReseedCape();
                        long va = PlayerShotPool.VelAddr(pool, slot);
                        FaceAlong(Memory.ReadFloat(va), Memory.ReadFloat(va + 8));
                        _target = PickTarget();                                   // locked-on first, else the nearest to Xiao (user 2026-09-11)
                        _floor = _target >= 0 ? Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(_target) + 4)
                                              : Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
                        _pelletDamage = Memory.ReadInt(PlayerShotPool.DamageAddr(pool, slot));
                        WriteHitStamps();                                         // the entry the cave will plant: pellet + attack, the weapon's element
                        CrushGuard(_target);                                      // Guard Crush: the target's guard windows are dropped for this flight
                        ApplyFlightTime();
                        _phase = Phase.Flying; _phaseStart = Now; _hitDone = false; _boundAt = Now; _gaitLogged = false; _blockedLogged = false; _pounceLogged = false; _pounceKind = 0; _sitLogged = false; _retargetTick = 0;
                        _flightFrame0 = Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame);
                        Memory.WriteFloat(CodeCaves.Mailbox.CatFloorH, _floor);
                        WriteTargetAim();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"cat bound to pellet slot {slot} on its birth frame" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + $" (motion frame {_flightFrame0:F1})");
                    }
                    break;
                case 4:                                                          // falling: off the pellet's line, or a flying pounce's arc
                    if (_phase != Phase.Falling)
                    {
                        _phase = Phase.Falling; _phaseStart = Now;
                        if (Memory.ReadInt(CodeCaves.Mailbox.CatPounceFly) != 0)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"vertical leap at flying enemy slot {_target}, v=({Memory.ReadFloat(CodeCaves.Mailbox.CatVx):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVh):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVz):F2})/frame (decided at distance {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgDist):F1})");
                            break;
                        }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"full size after {Memory.ReadInt(CodeCaves.Mailbox.CatGrowFrames)} frames — off the pellet's line, v=({Memory.ReadFloat(CodeCaves.Mailbox.CatVx):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVh):F2},{Memory.ReadFloat(CodeCaves.Mailbox.CatVz):F2})/frame, run speed {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F2}");
                    }
                    break;
                case 5:                                                          // land clip started (ahead of touchdown); the cave stops momentum at paw contact and runs at clip end
                    if (_phase != Phase.Landing)
                    {
                        _phase = Phase.Landing; _phaseStart = Now;
                        long lp = SlotAddr() + CCharacter.CharPos;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"land clip started at ({Memory.ReadFloat(lp):F1},{Memory.ReadFloat(lp + 4):F1},{Memory.ReadFloat(lp + 8):F1}), {Memory.ReadFloat(lp + 4) - Memory.ReadFloat(CodeCaves.Mailbox.CatFloorH):F2} above the floor, motion frame {Memory.ReadFloat(CodeCaves.MotionCave + MotionType.StateFrame):F1}");
                    }
                    break;
                case 6:                                                          // running (cave moves it); face along its direction, decide the end
                {
                    if (_phase != Phase.Running)
                    {
                        _phase = Phase.Running; _phaseStart = Now; _pounceLogged = false; _pounceKind = 0;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"land clip done (frame {Memory.ReadFloat(CodeCaves.Mailbox.CatPrevFrame):F1}) — moving off, floor {Memory.ReadFloat(CodeCaves.Mailbox.CatFloorH):F2}");
                    }
                    long rp = SlotAddr() + CCharacter.CharPos;
                    _x = Memory.ReadFloat(rp); _h = Memory.ReadFloat(rp + 4); _y = Memory.ReadFloat(rp + 8);
                    double rt = (Now - _phaseStart).TotalSeconds;
                    float dist = float.MaxValue;
                    if (_target >= 0 && !IsLiveEnemy(_target))
                    {
                        int was = _target;
                        _target = PickTarget();                                   // the next nearest, if any
                        WriteTargetAim();
                        CrushGuard(_target); ApplyFlightTime();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"target slot {was} is gone — now {(_target >= 0 ? $"slot {_target}" : "none (walking straight)")}");
                    }
                    if (_target >= 0)
                    {
                        long tp = EnemyAddresses.CharObjects.PosAddr(_target);
                        float ex = Memory.ReadFloat(tp) - _x, ey = Memory.ReadFloat(tp + 8) - _y; dist = (float)Math.Sqrt(ex * ex + ey * ey);
                    }
                    if (_target < 0)
                    {
                        if (!_sitLogged) { _sitLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no enemy within {MaxTargetDistance:F0} — sitting"); }
                        if (++_retargetTick >= 30)                                 // look again every ~0.5 s
                        {
                            _retargetTick = 0;
                            _target = PickTarget();
                            if (_target >= 0)
                            {
                                WriteTargetAim();
                                CrushGuard(_target); ApplyFlightTime();
                                _sitLogged = false; _gaitLogged = false;
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"enemy slot {_target} came within range — up and after it");
                            }
                        }
                    }
                    else if (!_gaitLogged && Memory.ReadInt(CodeCaves.Mailbox.CatBlocked) == 0)
                    {
                        _gaitLogged = true;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"walking at {Memory.ReadFloat(CodeCaves.Mailbox.CatRunSpeed):F3}/frame, clip rate {Memory.ReadFloat(SlotAddr() + CharacterMotion.MotionSpeedOffset):F2} (town mapping)");
                    }
                    bool blocked = Memory.ReadInt(CodeCaves.Mailbox.CatBlocked) != 0;
                    if (blocked && !_blockedLogged) { _blockedLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "a wall stops the cat — waiting"); }
                    if (!blocked) _blockedLogged = false;
                    if ((Now - _boundAt).TotalSeconds >= LifetimeSeconds)
                    {
                        Memory.WriteInt(CodeCaves.Mailbox.CatState, 0);
                        _scale = 1f; _caveOwns = false;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "20 s lifetime over — shrinking away");
                        FadeKeepingPose();                                       // sitting, walking or blocked: shrink + fade as it is (Phase.Fading; the glow follows)
                    }
                    break;
                }
                case 11:                                                         // float-up wind-up: in place, turning, until the feet-off frame launches the leap
                    if (_pounceKind != 3) { _pounceKind = 3; _phase = Phase.TakeOff; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"float wind-up at enemy slot {_target} — jump at frame {FloatLaunchFrame:F0}"); }
                    break;
                case 10:                                                         // ready: in place before the jump
                    if (!_pounceLogged) { _pounceLogged = true; _phase = Phase.TakeOff; _phaseStart = Now; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"readying a pounce at enemy slot {_target} (cave compared distance {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgDist):F1} vs range {Memory.ReadFloat(CodeCaves.Mailbox.CatDbgRange):F1})"); }
                    break;
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
            _pool = pool; _pelletSlot = slot; _scale = 0f; _alpha = 1f; _fade = 0; _glowFade = -1;
            PlaceRootUnderHead();
            _phase = Phase.Flying; _phaseStart = Now; _hitDone = false;
            Maintain();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"cat pinned to pellet slot {slot} from ({_px:F1},{_ph:F1},{_py:F1})" + (_target >= 0 ? $", locked enemy slot {_target}" : "") + " [thread follower]");
        }

        /// <summary>Back to resident: invisible, scale 0, fall pose looping, ready for the next charge.</summary>
        private static void Hide()
        {
            _alpha = 0f; _scale = 0f; _pelletSlot = -1; _fade = 0; _caveOwns = false; _hitFade = false;
            RestoreGuardsNow();
            _phase = Phase.Resident; _phaseStart = Now;
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

        /// <summary>The vertical leap is for the species in EnemySpecies.VerticalLeapTargets (flyers, hoverers, tall and
        /// large bodies; enhanced variants share the id) and for any miniboss spawn (MiniBoss's slot list, or a model
        /// scale ≥ 1.25 in the scale table). For those the cave's height threshold is set far below zero so the leap is
        /// always vertical; for everything else far above, so a ground enemy on a ledge is never mistaken for airborne.</summary>
        /// <summary>The leap's flight time to the target (the cave reads it at launch): the tall/large/flying set in
        /// <see cref="EnemySpecies.VerticalLeapTargets"/> and minibosses get the higher 40-frame arc, everything else
        /// the quick 27-frame one (user 2026-09-11).</summary>
        private static void ApplyFlightTime()
        {
            float frames = PounceFrames; string why = "";
            if (_target >= 0)
            {
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.EnemySpeciesId));
                float scale = Memory.ReadFloat(ModelScaleOffsets.ModelBase + (long)_target * ModelScaleOffsets.ModelStride + ModelScaleOffsets.ScaleX);
                if (EnemySpecies.VerticalLeapTargets.TryGetValue(species, out string name)) { frames = PounceFramesTall; why = name; }
                else if (MiniBoss.miniBossEnemyNumbers.Contains(_target) || scale >= 1.25f) { frames = PounceFramesTall; why = $"miniboss (model scale {scale:F2})"; }
            }
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceFrames, frames);
            if (why.Length > 0) Console.WriteLine(Tag + $"target slot {_target}: {frames:F0}-frame leap ({why})");
        }

        /// <summary>The point the cave walks to and jumps at: the centre of the target's BIGGEST active body sphere (the
        /// Dragon's torso, a bat's body, a Titan's chest) rather than its root at the feet (user 2026-09-12). Written
        /// into the mailbox every tick; CatTargetPtr points at that vector. No target → pointer 0 (the cat sits).</summary>
        /// <summary>The pounce range for this target: the look's (50 for the winged cat) — but a dormant chest-mimic is
        /// always approached to 30 (user 2026-09-13).</summary>
        private static float RangeFor(int target)
            => target >= 0 && _look.Range > PounceRange && IsUnopenedMimic(target) ? PounceRange : _look.Range;

        private static void WriteTargetAim()
        {
            if (_target < 0) { Memory.WriteInt(CodeCaves.Mailbox.CatTargetPtr, 0); return; }
            float range = RangeFor(_target);                                     // per target, every tick (a mimic may open mid-approach)
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceRange, range);
            Memory.WriteFloat(CodeCaves.Mailbox.CatPounceMaxDist, range * 2f);
            long root = EnemyAddresses.CharObjects.PosAddr(_target);
            float x = Memory.ReadFloat(root), h = Memory.ReadFloat(root + 4), y = Memory.ReadFloat(root + 8);
            if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_target, EnemySlotOffsets.RenderStatus)) != 2)
            {
                // Dormant (a chest-mimic that has not opened): its script has not declared any spheres, so the sphere table
                // is whatever the slot's previous occupant left — aiming at that sent the cat wandering off (user 2026-09-12).
                // The root is the chest's spot (SetMimicEvent places the box at the enemy's spawn position).
                Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos, x); Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos + 4, h); Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos + 8, y);
                Memory.WriteInt(CodeCaves.Mailbox.CatTargetPtr, (int)(CodeCaves.Mailbox.CatAimPos - 0x20000000));
                if (_target != _aimLoggedFor) { _aimLoggedFor = _target; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"aim at enemy slot {_target}: dormant (chest) — its root at ({x:F1},{h:F1},{y:F1})"); }
                return;
            }
            long tbl = EnemyAddresses.MainMonstorUnit.Base + (long)_target * 0x510;
            byte[] active = Memory.ReadBytesBatch(tbl + 0x55450, 16 * 4), radii = Memory.ReadBytesBatch(tbl + 0x55390, 16 * 4), centres = Memory.ReadBytesBatch(tbl + 0x55250, 16 * 0x10);
            byte[] pct = Memory.ReadBytesBatch(tbl + 0x555D0, 16 * 0x18);           // per sphere: damage % by attacker character (_SET_BODY_COL_PARA 10+char); Xiao = +4
            byte[] spare = Memory.ReadBytesBatch(tbl + 0x55490, 16 * 0x14);         // per sphere: the spare 5-int table — [1] = kick type admitted for Xiao at [0] % (disc-baked on Joe's face; ELF PatchCatSpherePercent)
            if (active != null && radii != null && centres != null)
            {
                // The biggest hurt sphere that can actually damage (Xiao % > 0 — Master Utan's neck/face/hands are 0 for her,
                // only the toes count); among equals, the one furthest FORWARD along the enemy's facing (Statue Dog's front
                // sphere, the Black Knight Mount's fore-body), then the root as a last resort (user 2026-09-12).
                float yaw = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(_target) + CCharacter.CharRotY);
                float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
                float best = -1f, bestFwd = float.MinValue; bool anyDamaging = false;
                for (int pass = 0; pass < 2 && best < 0; pass++)                     // pass 0: damaging spheres only; pass 1: any
                {
                    for (int j = 0; j < 16; j++)
                    {
                        if (BitConverter.ToInt32(active, j * 4) == 0) continue;
                        int p = pct == null ? 100 : BitConverter.ToInt32(pct, j * 0x18 + 4);
                        if (spare != null && BitConverter.ToInt32(spare, j * 0x14 + 4) == CatKickType) p = BitConverter.ToInt32(spare, j * 0x14);   // the cat's own % on this sphere
                        if (pass == 0 && p <= 0) continue;
                        float r = BitConverter.ToSingle(radii, j * 4);
                        float cx = BitConverter.ToSingle(centres, j * 0x10), ch = BitConverter.ToSingle(centres, j * 0x10 + 4), cy = BitConverter.ToSingle(centres, j * 0x10 + 8);
                        float fwd = (cx - Memory.ReadFloat(root)) * fx + (cy - Memory.ReadFloat(root + 8)) * fz;
                        if (r > best + 0.01f || (Math.Abs(r - best) <= 0.01f && fwd > bestFwd)) { best = r; bestFwd = fwd; x = cx; h = ch; y = cy; anyDamaging = pass == 0; }
                    }
                }
                if (_target != _aimLoggedFor) { _aimLoggedFor = _target; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"aim at enemy slot {_target}: sphere r={best:F1} at ({x:F1},{h:F1},{y:F1}){(anyDamaging ? "" : " — NO sphere can take Xiao's damage; aiming at the biggest anyway")}"); }
            }
            Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos,     x);
            Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos + 4, h);
            Memory.WriteFloat(CodeCaves.Mailbox.CatAimPos + 8, y);
            Memory.WriteInt  (CodeCaves.Mailbox.CatTargetPtr, (int)(CodeCaves.Mailbox.CatAimPos - 0x20000000));
        }

        // A chest-mimic waits as a DORMANT slot; while the cat's target is one, the cat crouches and waits (user 2026-09-12).
        private static readonly HashSet<ushort> KingMimics = new() { (ushort)EnemySpecies.KingMimicDBC.Id, (ushort)EnemySpecies.KingMimicSMT.Id, (ushort)EnemySpecies.KingMimicMS.Id, (ushort)EnemySpecies.KingMimicWOF.Id, (ushort)EnemySpecies.KingMimicSW.Id, (ushort)EnemySpecies.KingMimicGoT.Id, (ushort)EnemySpecies.KingMimicDS.Id };
        private static readonly HashSet<ushort> Mimics     = new() { (ushort)EnemySpecies.MimicDBC.Id, (ushort)EnemySpecies.MimicSMT.Id, (ushort)EnemySpecies.MimicMS.Id, (ushort)EnemySpecies.MimicWOF.Id, (ushort)EnemySpecies.MimicSW.Id, (ushort)EnemySpecies.MimicGoT.Id, (ushort)EnemySpecies.MimicDS.Id };
        private static bool _holdLogged;

        /// <summary>A native chest-mimic is a DORMANT enemy slot (RenderStatus 1: its view gate is 0, so it is never promoted
        /// to 2 and never drawn — the treasure box drawn at its position is the disguise, see ChestAddresses). Opening the
        /// box sets the gate and the slot goes to 2: its script declares the hurt spheres right then, so the cat leaps at
        /// that moment and not after the "appear" clip (user 2026-09-12).</summary>
        private static bool IsUnopenedMimic(int slot)
        {
            ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!KingMimics.Contains(species) && !Mimics.Contains(species)) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) != 2;   // dormant: the chest
        }

        /// <summary>A lock-on onto a different live enemy retargets the cat. Crouched (state 10) it is sent back to walking
        /// (state 6, the crouch's play-once cleared so the walk loops) so the cave closes the distance before leaping again.</summary>
        private static void RetargetToLockOn(int state)
        {
            int locked = LockedTarget();
            if (locked < 0 || locked == _target || !IsLiveEnemy(locked)) return;
            int was = _target; _target = locked;
            WriteTargetAim(); CrushGuard(_target); ApplyFlightTime();
            _gaitLogged = false; _pounceLogged = false; _sitLogged = false;
            if (state == 10)
            {
                long flags = SlotAddr() + CCharacter.MotionFlags;
                Memory.WriteInt(flags, Memory.ReadInt(flags) & ~2);
                Memory.WriteInt(CodeCaves.Mailbox.CatState, 6);
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lock-on moved: target slot {was} → {locked}{(state == 10 ? " (leaving the crouch, walking)" : "")}");
        }

        /// <summary>Locked-on enemy first; otherwise the live enemy nearest to Xiao (user 2026-09-11); −1 when none.</summary>
        private static int PickTarget()
        {
            int locked = LockedTarget();
            if (locked >= 0) return locked;
            float px = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos), py = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            int best = -1; float bestD = float.MaxValue;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!IsLiveEnemy(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - px, dy = Memory.ReadFloat(p + 8) - py, d = dx * dx + dy * dy;
                if (d < bestD && d <= MaxTargetDistance * MaxTargetDistance) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>The damage entry the cave plants at a contact carries the pellet's damage plus the weapon's attack
        /// (the "attack doubled" rule) and the weapon's element — written once per flight, at bind.</summary>
        private static void WriteHitStamps()
        {
            int attack = Memory.ReadShort(BattleWeaponAttack);
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;      // one pure element bit or none
            Memory.WriteInt(CodeCaves.Mailbox.CatHitDamage, Math.Max(1, _pelletDamage + attack));
            Memory.WriteInt(CodeCaves.Mailbox.CatHitAttr, (int)attr);
        }

        /// <summary>Guard Crush for the cat: the target's guard-frame windows are zeroed for the flight (a guarding enemy
        /// would otherwise take the hit on its guard); restored by <see cref="RetirePlanted"/> after the cat's lifetime
        /// or at once by <see cref="RestoreGuardsNow"/>.</summary>
        private static void CrushGuard(int enemy, bool again = false)
        {
            if (enemy < 0 || enemy >= EnemyAddresses.FloorSlots.Count) return;
            lock (_planted)
            {
                int have = _guardRestore.FindIndex(g => g.slot == enemy);
                if (have >= 0 && !again) return;                                     // already crushed this flight
                if (have >= 0)
                {   // again: windows registered AFTER the first crush (a chest-mimic's init label runs when it wakes) — zero
                    // them too; the snapshot keeps the first non-zero flag per window so the restore puts everything back
                    var (slot, ticks, snap) = _guardRestore[have];
                    bool more = false;
                    for (int w = 0; w < snap.Length; w++)
                    {
                        long a = EnemyAddresses.GuardWindows.FlagAddr(enemy, w);
                        ushort cur = Memory.ReadUShort(a);
                        if (cur != 0) { Memory.WriteUShort(a, 0); if (snap[w] == 0) snap[w] = cur; more = true; }
                    }
                    if (more) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"guard windows of enemy slot {enemy} zeroed again (registered since the first crush)");
                    return;
                }
            }
            var snap0 = new ushort[EnemyAddresses.GuardWindows.WindowCount];
            bool any = false;
            for (int w = 0; w < snap0.Length; w++)
            {
                long a = EnemyAddresses.GuardWindows.FlagAddr(enemy, w);
                snap0[w] = Memory.ReadUShort(a);
                if (snap0[w] != 0) { Memory.WriteUShort(a, 0); any = true; }
            }
            if (!any) return;
            lock (_planted) _guardRestore.Add((enemy, (int)(LifetimeSeconds * 60) + 120, snap0));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"guard windows of enemy slot {enemy} zeroed for this flight (Guard Crush)");
        }

        /// <summary>Guard Crush, every tick the cat can deal damage. <see cref="CrushGuard"/> only covers the chosen target and
        /// only at the moment it is called, but an enemy re-registers its guard windows whenever its script runs
        /// <c>_SET_GUARD_FRAME</c> again, and the cat's contact test hits whatever body sphere it touches — which need not be the
        /// target. CheckDmg (0x1D9F10) offers the attacker no "unguardable" flag: the window IS the guard, so it has to be down at
        /// the instant of contact. So: read every slot's window flags in one block, and crush the ones that are live and near
        /// enough for the cat to reach (the target always counts). Everything crushed is restored by RetirePlanted.</summary>
        private const float GuardCrushRadius = 30f;      // the cat's reach while it is airborne
        private const int   GuardSweepTicks  = 45;       // how long a swept crush holds after the last sweep (~1.5 s)
        private static void CrushGuardsNearCat()
        {
            int n = EnemyAddresses.FloorSlots.Count, wc = EnemyAddresses.GuardWindows.WindowCount;
            byte[] blk = Memory.ReadBytesBatch(EnemyAddresses.GuardWindows.FlagAddr(0, 0), n * EnemyAddresses.GuardWindows.Stride);
            if (blk == null) return;
            long sl = SlotAddr();
            float cx = Memory.ReadFloat(sl + CCharacter.CharPos), cy = Memory.ReadFloat(sl + CCharacter.CharPos + 8);
            for (int slot = 0; slot < n; slot++)
            {
                var cur = new ushort[wc];
                bool any = false;
                for (int w = 0; w < wc; w++)
                {
                    cur[w] = BitConverter.ToUInt16(blk, slot * EnemyAddresses.GuardWindows.Stride + w * 2);
                    if (cur[w] != 0) any = true;
                }
                if (!any) continue;                                                  // nothing registered: nothing to crush
                if (slot != _target)
                {
                    long p = EnemyAddresses.CharObjects.PosAddr(slot);
                    float dx = Memory.ReadFloat(p) - cx, dy = Memory.ReadFloat(p + 8) - cy;
                    if (dx * dx + dy * dy > GuardCrushRadius * GuardCrushRadius) continue;
                }
                for (int w = 0; w < wc; w++) if (cur[w] != 0) Memory.WriteUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w), 0);
                lock (_planted)
                {
                    int i = _guardRestore.FindIndex(g => g.slot == slot);
                    if (i < 0)
                    {
                        _guardRestore.Add((slot, GuardSweepTicks, cur));
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"guard windows of enemy slot {slot} zeroed (in the cat's reach)");
                    }
                    else
                    {
                        var (s2, ticks, snap) = _guardRestore[i];
                        for (int w = 0; w < wc && w < snap.Length; w++) if (snap[w] == 0 && cur[w] != 0) snap[w] = cur[w];
                        _guardRestore[i] = (s2, Math.Max(ticks, GuardSweepTicks), snap);
                    }
                }
            }
        }

        private static void RestoreGuardsNow()
        {
            lock (_planted)
            {
                foreach (var (slot, _, flags) in _guardRestore)
                    for (int w = 0; w < flags.Length; w++) if (flags[w] != 0) Memory.WriteUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w), flags[w]);
                _guardRestore.Clear();
            }
        }

        /// <summary>Fade out from the current pose without restarting the clip (the cave left the key as it stood).</summary>
        private static void FadeKeepingPose()
        {
            _key = Memory.ReadInt(SlotAddr() + CCharacter.MotionId);
            _phase = Phase.Fading; _phaseStart = Now; _fade = 0;
        }
        private static int _pelletDamage;
        private static bool _pounceLogged, _sitLogged;
        private static int _retargetTick;
        private static int _pounceKind;               // 1 ground, 2 flying (log only)
        private static readonly List<(int slot, int ticks, ushort[] flags)> _guardRestore = new();

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
            if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) < 1) return false;   // not on the floor yet / gone
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }

        // ───────────────────────────────────────────── flight ──────────────────────────────────────────────

        private static void Step()
        {
            double t = (Now - _phaseStart).TotalSeconds;
            if (_hitFade)                                                        // after a landed hit: keep flying/landing under the cave, fade out meanwhile
            {
                _fade++; _alpha = Math.Max(0f, 1f - _fade / (float)FadeTicks);
                if (_fade >= FadeTicks) { DisarmCave(); Hide(); return; }      // the cave stops driving the (now invisible) cat
            }
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
                    if (t >= LandSeconds) Enter(Phase.Running, KeyWalk);
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
                    if (_native) break;                                          // the cave runs the take-off, the leap and the landing; PollCave only mirrors
                                                                                 // them (2026-09-11: this timer used to SetKey(KeyLeap) with the restart bit
                                                                                 // under the cave — cutting the take-off short and, via Leaping, the leap)
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
                    if (_native) break;                                          // cave-owned (see TakeOff)
                    _x += _vx; _y += _vy; _h += _vh; _vh -= Gravity;
                    bool near = _target >= 0 && (tx - _x) * (tx - _x) + (ty - _y) * (ty - _y) <= HitRadius * HitRadius * 0.5f;
                    if (!_hitDone && (near || (_vh < 0 && _h <= _floor + 1f))) { _hitDone = true; PlantHit(_x, _h + 3f, _y, HitRadius, Math.Max(1, _pelletDamage + Memory.ReadShort(BattleWeaponAttack)), _x, _h, _y); }
                    if (_vh < 0 && _h <= _floor) { _h = _floor; Enter(Phase.LandEnd, KeyLand); }
                    break;
                }
                case Phase.LandEnd:
                    if (_native) break;                                          // thread follower only
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
            _phase = p; _phaseStart = Now;
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
            if (FindTexEntry(CatTextureNames[0]) == 0 && RecreateCatEntries() < CatTextureNames.Length)
            {
                if (!_texDeferLogged) { _texDeferLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "her cat textures are not in the manager and none are remembered — spawn deferred (retrying)"); }
                return false;
            }
            _texDeferLogged = false;
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
                if (nm.Length == 0 || !parOk || nm == CapeNodeName) break;      // the cape's cloth FRAME node follows the cat run: hers, not the copy's
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
            _wingMeshIdx.Clear();
            foreach (string nm in WingMeshNodes) { int wi = FindNode(block, nm); if (wi >= 0) _wingMeshIdx.Add(wi); }
            _maskMeshIdx = FindNode(block, MaskNodeName);

            if (!CopyMeshes()) return false;
            if (!RegisterSlot(min, blockSize)) return false;
            Active = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cat copy up: {_nodeCount} nodes (her n{_catIndex}..n{_catIndex + _nodeCount - 1}) → 0x{_copyRoot:X}, slot {Slot}");
            return true;
        }

        /// <summary>The head's rest position in the cat's own space (row-vector chain of local matrices from
        /// `cat_kao` up to the root), so the flight can keep the HEAD on the pellet's line (user 2026-09-10).</summary>
        private static int FindNode(byte[] block, string name)
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                int o = i * CFrameVu1.NodeStride + CFrameVu1.Name, len = 0;
                while (len < 0x20 && block[o + len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(block, o, len) == name) return i;
            }
            return -1;
        }

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
            Memory.WriteInt(CodeCaves.Mailbox.CatHeadNode, head >= 0 ? (int)(CodeCaves.NodePoolGuest + head * CFrameVu1.NodeStride) : 0);   // the cave's contact point = this frame's posed position
            int ga = FindNode(block, GlowNodeA), gb = FindNode(block, GlowNodeB);                                                              // the glow's anchor frames
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowNodeA, ga >= 0 ? (int)(CodeCaves.NodePoolGuest + ga * CFrameVu1.NodeStride) : 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowNodeB, gb >= 0 ? (int)(CodeCaves.NodePoolGuest + gb * CFrameVu1.NodeStride) : 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowReady, 0);
            if (ga < 0 || gb < 0) Console.WriteLine(Tag + $"glow anchors: {GlowNodeA} n{ga}, {GlowNodeB} n{gb} — falling back to the root");
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
        // Two arenas (2026-09-13, the wings): every mesh's visual + first VU buffer + MDT goes in the MeshCave (below the Angel
        // Gear prop's region); the SECOND VU buffers and the skin sources go wherever they fit — the MeshCave's remainder,
        // else the overflow cave borrowed from CharacterClone's cloth caves (idle while Xiao is the active character).
        private static long _ovFree;
        private static long TakeCave(int bytes, out uint guest)
        {
            long need = A16L(bytes);
            if (_caveFree + need <= CodeCaves.CatMeshCaveEnd) { long a = _caveFree; _caveFree += need; guest = (uint)(a - 0x20000000); return a; }
            if (_ovFree + need <= CodeCaves.CatOverflowCave + CodeCaves.CatOverflowCaveSize) { long a = _ovFree; _ovFree += need; guest = (uint)(a - 0x20000000); return a; }
            guest = 0; return 0;
        }
        private static bool CopyMeshes()
        {
            long cave = CodeCaves.MeshCave, caveGuest = CodeCaves.MeshCaveGuest;
            long caveEnd = CodeCaves.CatMeshCaveEnd;                                // above it: the Angel Gear prop's meshes, then its track cave
            _ovFree = CodeCaves.CatOverflowCave;
            int copied = 0;
            var second = new List<(long vis, byte[] vuB, int vuSz, int idx)>();
            for (int i = 0; i < _nodeCount; i++)
            {
                long node = CodeCaves.NodePool + (long)i * CFrameVu1.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + CFrameVu1.GeomPtr) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(vis)) continue;
                if (!_look.Wings && _wingMeshIdx.Contains(i)) continue;              // a wingless look: the wings are never copied (HideMeshes unlinks their runs and nulls their geometry)
                if (!_look.Cape && i == _maskMeshIdx) continue;                      // and the mask belongs to Super Steve alone
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
                // the GIF is still reading the other. A single-buffered copy tears (flicker); give the copy both — the
                // second one is placed after every mesh's primary data has a home (second pass below).
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);
                Memory.WriteBytesBatch(cVU, vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + CFrameVu1.GeomPtr, cVisG);
                cave += need; caveGuest += need; copied++;
                _skinNodes.Add((i, cMDT, mdtSz, cVU, 0L, vuSz));
                second.Add((cVis, vuB, vuSz, _skinNodes.Count - 1));
                Console.WriteLine(Tag + $"mesh n{i} ({ReadName((uint)(CodeCaves.NodePoolGuest + i * CFrameVu1.NodeStride))}): vis 0x{visSz:X} + vu 0x{vuSz:X} + mdt 0x{mdtSz:X} copied");
            }
            _caveFree = cave;
            if (copied == 0) { Console.WriteLine(Tag + "no software-skinned cat mesh found — refusing to share her collapsed copy of the skin"); return false; }
            foreach (var (vis, vuB, vuSz, idx) in second)                          // second VU buffers: MeshCave remainder, else the overflow cave
            {
                long cVU2 = TakeCave(vuSz, out uint cVU2G);
                if (cVU2 == 0) { Console.WriteLine(Tag + $"mesh n{_skinNodes[idx].node}: no room for a second VU buffer — single-buffered (may flicker)"); continue; }
                Memory.WriteBytesBatch(cVU2, vuB);
                Memory.WriteUInt(vis + 0x2c, cVU2G);
                var e = _skinNodes[idx]; _skinNodes[idx] = (e.node, e.mdt, e.mdtSz, e.vu, cVU2, e.vuSz);
            }
            Console.WriteLine(Tag + $"mesh caves: main {_caveFree - CodeCaves.MeshCave:N0} of {CodeCaves.CatMeshCaveEnd - CodeCaves.MeshCave:N0} B, overflow {_ovFree - CodeCaves.CatOverflowCave:N0} of {CodeCaves.CatOverflowCaveSize:N0} B");
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
            foreach (var (node, mdt, _, _, _, _) in _skinNodes)
            {
                int count = Memory.ReadInt(mdt + CVisualMDT.MdtVertCount);
                int vOff  = Memory.ReadInt(mdt + CVisualMDT.MdtVertOffset);
                if (count <= 0 || count > 3000 || vOff <= 0) { Console.WriteLine(Tag + $"skin n{node}: odd MDT header (count {count}, verts @+0x{vOff:X})"); return false; }
                int bytes = count * 16;
                long cave = TakeCave(bytes, out uint caveG);
                if (cave == 0) { Console.WriteLine(Tag + "skin source vertices do not fit the mesh caves"); return false; }
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
                BitConverter.GetBytes(caveG).CopyTo(fib, e + 8);
                Console.WriteLine(Tag + $"skin n{node}: {count} source vertices built at 0x{caveG:X} from bind [{m[0]:F2} {m[5]:F2} {m[10]:F2} | {m[12]:F2},{m[13]:F2},{m[14]:F2}]");
            }
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

        private static int _texCheckTick;
        /// <summary>A dungeon script event or a menu can rebuild the texture manager under the resident copy: the cat's
        /// entries come back at their vanilla addresses (or vanish) while the copy's packets still name the relocated
        /// ones — garbled fur until a rebuild. Notice it and tear the copy down; it re-spawns clean after the 1 s gate.</summary>
        private static void CheckTexturesStillOurs()
        {
            long e = FindTexEntry(CatTextureNames[0]);
            uint tbp = e == 0 ? 0u : (Memory.ReadUInt(e + 0x28) & 0x3FFF);
            if (e != 0 && tbp >= StuckFloor && Memory.ReadShort(e) == SlotTextureGroup) return;   // still relocated and ours
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (e == 0 ? "texture manager rebuilt (cat entries gone)" : $"texture manager rebuilt (cat entry back at 0x{tbp:X}, block 0x{Memory.ReadShort(e):X})") + " — rebuilding the copy");
            _texMoved.Clear();                                                   // nothing of ours is in there to restore
            Despawn();
        }

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
                $"her MOTION 1 pointer LOST (raw 0x{raw:X8}) — phase {(Active ? _phase.ToString() : "idle")}, {(Now - _lastDespawn).TotalSeconds:F2} s after the last despawn, event mode {Memory.ReadInt(DungeonScriptEvent.BtEventMode)}; table:{sb}");
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
            if (fiSize > CodeCaves.CatFrameInfCaveSize || bmSize > CodeCaves.CatBoneMtxCaveSize) { Console.WriteLine(Tag + "bone buffers exceed the caves"); return false; }
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
            // The float-up's play rate goes into its KEY entry, not the speed override: Step's play-once stop test
            // (0x138530) looks ahead by the KEY rate while the advance uses the override, so an override faster than
            // the KEY rate overshoots the last frame and the clip wraps (user 2026-09-11: the float kept looping).
            // The table is her hidden cat channel's, played by nobody but this copy.
            Memory.WriteFloat(Memory.ToMmu(keyTable) + (KeyFloat - KeyBase) * CCharacter.MotionEntryStride + 8, FloatRate);
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
            Memory.WriteInt  (s + CCharacter.MotionFlags, (Memory.ReadInt(s + CCharacter.MotionFlags) & ~CCharacter.MotionPlayOnce) | CCharacter.MotionRestart);   // a fresh key loops again (the hit sets play-once)
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
            else if (_hitFade) Memory.WriteFloat(s + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, _alpha)));   // the cave keeps the pose; only the opacity is ours (a plain fade, no shrink — user 2026-09-12)
            float lit = Math.Max(0f, Math.Min(1f, _alpha));
            Memory.WriteFloat(s + CCharacter.CharaTint,     _look.Tint[0] * lit);   // ambient ADD, per weapon (Looks)
            Memory.WriteFloat(s + CCharacter.CharaTint + 4, _look.Tint[1] * lit);
            Memory.WriteFloat(s + CCharacter.CharaTint + 8, _look.Tint[2] * lit);
            if (_capeObj != 0) WriteCapeTint(lit);                                  // the cape rides the same fade, one step further red
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
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowOn, 0); _glowFade = -1;    // PollCave stops with Active — switch the glow off here
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
            Memory.WriteInt  (s + CCharacter.ClothList, 0); _capeObj = 0; _capeRest = null;        // the cape list lives in OUR cave: never leave it on a slot we hand back
            Memory.WriteUInt (CodeCaves.Mailbox.CatCapeCloth, 0);                // …and the recolour cave stops matching a dead pointer
            RetagCatTextures(SlotTextureGroup, HerTextureBlock);
            Active = false; _key = -1; _target = -1; _weapon = -1;
            if (!SlingshotProp.Active) Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 2);   // after Active=false: Mirage's loop owns it again
            _lastDespawn = Now;
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
        private static readonly string[] CatTextureNames = { "c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05", "catglow", "catwing", "catgloww", "catglowg", "catcape" };   // catglow = the blue torch-glow disc (build_cat_pack.GLOW_NAME); catwing = the wings' flat white; catgloww/g = the white / gold discs
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
                if (to == SlotTextureGroup && (Memory.ReadUInt(e + 0x28) & 0x3FFF) < StuckFloor)
                {
                    byte[] snap = Memory.ReadBytesBatch(e, TexStride);           // the whole entry at rest: block, name, image pointers, TEX0
                    if (snap != null) _texSnapshot[nm] = snap;                   // a script event's slot clean-up wipes it; RecreateCatEntries puts it back
                }
                Memory.WriteUShort(e, (ushort)to);
                if (to == SlotTextureGroup)
                {
                    // An entry still sitting in the relocation window is one an earlier despawn failed to put back
                    // (the manager's entry list had shifted under an address-keyed restore — 2026-09-11, textures
                    // garbled until a party switch rebuilt the manager). Put its remembered original back first.
                    ulong t = (ulong)Memory.ReadUInt(e + 0x28) | ((ulong)Memory.ReadUInt(e + 0x2C) << 32);
                    uint tbp = (uint)(t & 0x3FFF);
                    if (tbp >= StuckFloor)
                    {
                        if (_texOriginal.TryGetValue(nm, out ulong orig))
                        {
                            Memory.WriteUInt(e + 0x28, (uint)orig); Memory.WriteUInt(e + 0x2C, (uint)(orig >> 32));
                            Console.WriteLine(Tag + $"texture {nm} was left at 0x{tbp:X} by an earlier despawn — restored to 0x{orig & 0x3FFF:X}");
                            t = orig; tbp = (uint)(t & 0x3FFF);
                        }
                        else Console.WriteLine(Tag + $"WARNING: texture {nm} sits at 0x{tbp:X} with no remembered original — its relocation will be wrong this spawn");
                    }
                    else _texOriginal[nm] = t;
                    minTbp = Math.Min(minTbp, tbp);
                }
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

        // (texture NAME, original tex0) for every cat texture moved this spawn — restored on despawn by looking the
        // entry up by name again (entry addresses shift when the manager registers or drops textures meanwhile).
        private static readonly List<(string name, ulong tex0)> _texMoved = new();
        // name → TEX0 at rest, refreshed at every sane spawn; repairs an entry a failed restore left relocated.
        private static readonly Dictionary<string, ulong> _texOriginal = new();
        private static readonly Dictionary<string, byte[]> _texSnapshot = new();   // name → the manager entry (0x50 B) as it sits in her block
        private static bool _texDeferLogged;

        /// <summary>A script event's clean-up (EdEventAllClear 0x197810 → DeleteTextureBlock, which zeroes every entry of
        /// a block id) wipes the cat entries while they are tagged to the copy's slot group (2026-09-11: after the chasm
        /// jump every spawn found 0 entries and the copy was rebuilt every half second). Put the remembered entries back
        /// into free manager rows (first empty name from row 1, as SearchTexture 0x131320 allocates) — the image data they
        /// point at is her pack's own IMG bank, still loaded — so the normal re-tag/relocate can run. Returns how many of
        /// the cat's entries the manager now holds.</summary>
        private static int RecreateCatEntries()
        {
            int present = 0, made = 0;
            foreach (string nm in CatTextureNames)
            {
                if (FindTexEntry(nm) != 0) { present++; continue; }
                if (!_texSnapshot.TryGetValue(nm, out byte[] snap)) continue;
                int idx = -1;
                for (int i = 1; i < TexMaxEntries; i++)
                    if (Memory.ReadByte(TextureManager + TexEntries + (long)i * TexStride + TexName) == 0) { idx = i; break; }
                if (idx < 0) { Console.WriteLine(Tag + "texture manager full — cannot recreate " + nm); break; }
                Memory.WriteBytesBatch(TextureManager + TexEntries + (long)idx * TexStride, snap);
                if (idx + 1 > Memory.ReadInt(TextureManager)) Memory.WriteInt(TextureManager, idx + 1);
                present++; made++;
            }
            if (made > 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"cat textures recreated in the manager ({made} put back, {present} of {CatTextureNames.Length} present) after a script event wiped them");
            return present;
        }
        private const uint StuckFloor = 0x3000;          // no vanilla block reaches this high (max seen 0x3920 is the manager's own top area)

        /// <summary>The manager entry for a cat texture, found by name (0 if absent).</summary>
        private static long FindTexEntry(string name)
        {
            int count = Math.Min(TexMaxEntries, Memory.ReadInt(TextureManager));
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager + TexEntries + (long)i * TexStride;
                byte[] nb = Memory.ReadBytesBatch(e + TexName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(nb, 0, len) == name) return e;
            }
            return 0;
        }

        /// <summary>Move the cat entries' TEX0 (TBP0 bits 0..13, CBP bits 37..50) by newBase − oldBase, and patch
        /// the identical register words wherever they sit in the copy's own packet buffers and MDT copy (the
        /// packet is rebuilt from the MDT every frame). With oldBase == 0 the saved originals are put back.</summary>
        private static int RelocateCatTextures(uint oldBase, uint newBase)
        {
            var moves = new List<(ulong oldT, ulong newT)>();
            if (oldBase == 0 && newBase == 0)
            {
                foreach (var (name, tex0) in _texMoved)
                {
                    long entry = FindTexEntry(name);
                    if (entry == 0) { Console.WriteLine(Tag + $"WARNING: texture {name} is gone from the manager — nothing to restore"); continue; }
                    ulong cur = (ulong)Memory.ReadUInt(entry + 0x28) | ((ulong)Memory.ReadUInt(entry + 0x2C) << 32);
                    moves.Add((cur, tex0));
                    Memory.WriteUInt(entry + 0x28, (uint)tex0); Memory.WriteUInt(entry + 0x2C, (uint)(tex0 >> 32));
                }
                _texMoved.Clear();
            }
            else
            {
                foreach (string name in CatTextureNames)                     // the five cat textures only, by name
                {
                    long e = FindTexEntry(name);
                    if (e == 0) continue;
                    ulong t = (ulong)Memory.ReadUInt(e + 0x28) | ((ulong)Memory.ReadUInt(e + 0x2C) << 32);
                    uint tbp = (uint)(t & 0x3FFF), cbp = (uint)((t >> 37) & 0x3FFF);
                    ulong n = (t & ~0x3FFFUL & ~(0x3FFFUL << 37)) | (ulong)(newBase + (tbp - oldBase)) | ((ulong)(newBase + (cbp - oldBase)) << 37);
                    _texMoved.Add((name, t));
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
        /// <param name="ox">…the kick's origin (the cat): CheckDmg pushes the enemy along enemy − origin with strength/decay
        /// from the entry when its type word (+0x98) is 2 — the same words Toan's sword hits carry, so the enemy's own
        /// hit reaction (flinch + shove) runs exactly as for a melee hit. A pellet's entry has type 0: no reaction.</param>
        private static void PlantHit(float x, float h, float y, float radius, int baseDmg, float ox, float oh, float oy)
        {
            long pool = Memory.ReadInt(NowColDataPtr);
            if (pool <= 0) return;
            pool += 0x20000000;
            int slot = -1;
            for (int i = ColEntries - 1; i >= 0; i--)
                if (Memory.ReadInt(pool + ColActiveOff + i * 4) == 0) { slot = i; break; }
            if (slot < 0) { Console.WriteLine(Tag + "no free collision entry — pounce lost"); return; }
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
            I(0x70, 0); I(0x74, 0);
            F(0x80, ox); F(0x84, oh); F(0x88, oy); F(0x8C, 1f);                   // kick origin (a point)
            F(0x90, KickStrength); F(0x94, KickDecay); I(0x98, CatKickType);       // kick strength, decay, type 2 = melee-style reaction
            Memory.WriteBytesBatch(pool + slot * ColStride, e);
            Memory.WriteInt(pool + ColActiveOff + slot * 4, 1);
            lock (_planted) _planted.Add((slot, PlantedLifeTicks, false));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                $"hit entry at ({x:F1},{h:F1},{y:F1}) r={radius:F0}: base {baseDmg}, attr 0x{attr:X} → entry {slot}");
        }

        private static void RetirePlanted()
        {
            lock (_planted)
            {
                for (int i = _guardRestore.Count - 1; i >= 0; i--)
                {
                    var (slot, ticks, flags) = _guardRestore[i];
                    if (--ticks > 0) { _guardRestore[i] = (slot, ticks, flags); continue; }
                    for (int w = 0; w < flags.Length; w++) if (flags[w] != 0) Memory.WriteUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w), flags[w]);
                    _guardRestore.RemoveAt(i);
                }
                if (_planted.Count == 0) return;
                long pool = Memory.ReadInt(NowColDataPtr);
                for (int i = _planted.Count - 1; i >= 0; i--)
                {
                    var (idx, ticks, native) = _planted[i];
                    if (native && pool > 0 && Memory.ReadInt(pool + 0x20000000 + ColActiveOff + idx * 4) == 0)
                    {
                        // The entry is gone. Accepted = some enemy's CheckDmg overwrote the −1 the cave stamped into every
                        // slot's "last hit sphere" word (+0x55750) at the plant — it only does so past its guard and
                        // invincibility gates, right before applying the damage. Gone without that = the engine dropped it
                        // (a mimic wakes with 100 invincibility frames — user 2026-09-12): not spent, the latch is freed.
                        int hitSlot = -1;
                        for (int s2 = 0; s2 < 16 && hitSlot < 0; s2++)
                            if (Memory.ReadInt(EnemyAddresses.MainMonstorUnit.Base + (long)s2 * 0x510 + 0x55750) != -1) hitSlot = s2;
                        if (hitSlot >= 0)
                        {
                            // Accepted: damage, hitspark, kick and (via the patched flinch rule) the stagger are all the engine's.
                            // Only now is the cat spent (user 2026-09-11).
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"hit landed on enemy slot {hitSlot} (entry {idx}) — fading out");
                            _hitFade = true; _fade = 0; _alpha = 1f;
                        }
                        else
                        {
                            Memory.WriteInt(CodeCaves.Mailbox.CatHitLatch, 0);
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"contact was not accepted (entry {idx} gone, no enemy took it — invincible or guarding) — no hit, still flying");
                        }
                        _planted.RemoveAt(i); continue;
                    }
                    if (--ticks > 0) { _planted[i] = (idx, ticks, native); continue; }
                    if (pool > 0) Memory.WriteInt(pool + 0x20000000 + ColActiveOff + idx * 4, 0);
                    if (native)
                    {   // never consumed: the enemy was invulnerable or the sphere's hurt window was closed — not spent; the cave may contact again
                        Memory.WriteInt(CodeCaves.Mailbox.CatHitLatch, 0);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"contact did not connect within {PlantedLifeTicks} ticks (entry {idx}) — no hit, still flying");
                    }
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
