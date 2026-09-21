using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.CatCape;
using static Dark_Cloud_Improved_Version.CatFlight;
using static Dark_Cloud_Improved_Version.CatCopy;
using static Dark_Cloud_Improved_Version.CatTextures;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Divine Beast Title — the CHARGED shot launches Xiao's cat form.
    ///
    /// Hold the shot past <see cref="ChargeSeconds"/> and the pellet that leaves the slingshot becomes an animated
    /// cat: the copy is pinned to the live pellet, its head riding the pellet's point, and grows from nothing over
    /// <see cref="GrowSeconds"/> while the pellet's sprite shrinks away. The pellet keeps flying and colliding as
    /// the game runs it. At the breakaway the cat falls under its own gravity, lands, walks to its target, crouches
    /// and leaps to strike — or sits when there is nothing to chase — and fades on a hit or at the end of its
    /// lifetime. The flight is driven frame-by-frame by a native cave (tools/stubs/cat_pellet_follow.s); this class
    /// arms it, picks targets, plants the damage and owns the look.
    ///
    /// Where the cat comes from: the ISO bake (tools/iso_patch/build_cat_pack.py) grafts the s86 cat rig into Xiao's
    /// dungeon pack c04b.chr as 37 `cat_` nodes that sit UNPARENTED in her frame array — the engine never draws,
    /// skins or DMAs them — plus a second motion channel (KEY_START 64: stand, ready, run, take-off, leap, land,
    /// walk, float, sit) that only ever plays on the copy. So whenever Xiao is in a dungeon the cat's mesh, textures
    /// and clips are already resident in her tree. Summoning it follows the Angel Gear / Mirage recipe: deep-copy the
    /// subtree into the mod's caves, give the copy its own skin buffers and motion channel, host it in a dungeon
    /// chara slot, and let the engine step and draw it natively. Her own model is never touched.
    /// </summary>
    internal static class DivineBeastTitle
    {
        internal static bool Enabled = true;
        internal static bool Active { get; set; }

        private const string Tag = "[DivineBeastTitle] ";
        private const int  XiaoId = 1;
        internal const int  Slot   = 1;                 // DungeonCharaDraw host (0 = Mirage clone, 3 = Angel Gear slingshot)
        internal const int  CharCopySize = 0xD60, MotionStructSize = 0xC0;
        internal const int  TickMs = 16;
        /// <summary>How long after arming the build waits for the texture manager to settle. The cat's VRAM window is claimed
        /// by writing a block's base and top DIRECTLY rather than through the allocator, so claiming it before the manager has
        /// finished handing out addresses lets later textures land on top of it.</summary>
        private const double SettleSeconds = 1.0;

        // The cat's motion channel on HER (build_cat_pack.py: MOTION 1 in c04b.chr's base.cfg, KEY_START 64; track
        // bone ids are relative to the cat root, which is the copy's node 0).
        internal const int  CatChannel = 1;
        internal const int  MaxTreeNodes = 160;         // her array: 79 body + 37 cat (+ headroom for the scan)
        internal const int  KeyBase = 64, KeyCount = 9;
        internal const int  KeyStand = 64, KeyReady = 65, KeyRun = 66, KeyTakeOff = 67, KeyLeap = 68, KeyLand = 69, KeyWalk = 70, KeyFloat = 71, KeySit = 72;   // walk = s86 KEY 2 at 1.0; float = the town ladder jump's vertical leap (e04c04cat #5); sit = s86 KEY 1
        internal const float  MoveFrac      = 0.20f;    // ground speed after the landing, as a fraction of the pellet's speed; below this it loses enemies
        // A full-charge pellet flies 5.0 u/frame, a lighter one 3.5, so a fraction made the walk jump between 0.56 and
        // 0.80. Pinned as an absolute speed instead: 16% of 3.5.
        internal static float MoveSpeedAbs => 0.20f * 3.5f * Stride;
        // Walk clip rate from the ground speed, the TOWN's mapping for this very rig (EdMoveChara 0x16A160: rate =
        // 0.8·(0.2 + stick) capped at 0.85, ground = 1.6·stick → rate = 0.16 + 0.5·ground). Planted feet would need
        // 5× that (the clip's real stride is 0.196 u/clip-frame) and looked far too fast; this is the tuned look.
        // Calibrated by eye against the town: the walk reaches its cap at WalkCapSpeed units/frame —
        // 20% of the 3.5 u/frame pellet — rather than at the 1.36 u/frame the town formula literally implies (the two
        // contexts' units-per-frame do not read the same on screen). Slope = (cap − base) / that speed.
        internal const float  RateBase = 0.16f, RateMax = 0.85f, WalkCapSpeed = 0.20f * 3.5f;   // cap and ground speed both at 20%
        internal static float RatePerSpeed => (RateMax - RateBase) / (WalkCapSpeed * Stride);   // ≈ 0.99 per unit of ground speed at the tuned size
        internal const double LifetimeSeconds = 20.0;   // from the bind: the cat stays until it lands a hit or this passes
        internal const float  ProbeUp       = 8f;       // floor probe reach above the cat's root (catches a tread it is flying into)
        internal const float  ProbeDown     = 40f;      // … and below (a drop off a ledge still finds the floor)
        // Extra casts ahead of and behind the root along its direction; the cat stands on the HIGHEST of the three, so a
        // ten-unit body on stairs rides on its uphill end (front paws ≈ z 2, hind paws ≈ z −2 in cat space).
        internal const float  ProbeFront    = 3f, ProbeBack = 2f;
        internal const string CatRootName = "catroot";
        internal const float  HideScale   = 0.001f;     // must match build_cat_pack.py HIDE_SCALE
        internal const int    ChanKeyStart = 0x3E0;     // CCharacter: channel[i] first key id (CommandKEY_START)
        internal const int    ChanKeyEnd   = 0x400;     // CCharacter: channel[i] key id end (CommandMOTION_END)
        internal const int    MotListHead  = 0x04;      // MOTION_TYPE +0x04 → the .mot track list (shared, read-only)
        internal const int    ShadowModel  = 0xC0;      // CCharacter: shadow rig root (CommandSHADOW_MODEL); 0 = no shadow
        internal const int    ShadowSlotBase = 0xC40;   // CCharacter: shadow channel[i] ptr (ShadowStep__10CCharacter)
        // CNPCharacter tail (Initialize__12CNPCharacter 0x1569E0 / ClearSeq 0x156350 / SetFootSound / SetEvent)
        internal const int    FootTable = 0xD60, FootStride = 0x14, FootSlots = 6;      // frame < 0 = free
        internal const int    EventTable = 0xDE8, EventStride = 0x14, EventSlots = 32;  // frame < 0 = free
        internal const int    SeqEnable = 0x11B0;       // ClearSeq sets 1 (+0x11B4/8 = 0, 8 × 0x50 entries @+0x11C0 = 0)
        internal const int    SeqWord1490 = 0x1490;     // Initialize sets -1

        // Charge + launch.
        private const double ChargeSeconds = 0.5;      // hold this long → the shot is the cat
        internal const double GrowSeconds   = 0.1;      // the pellet grows into the cat over this long after it is fired
        internal const int    GrowFrames    = 6;        // the same, in frames, for the native follower (60 fps)
        internal const float  Gravity       = 0.05f;    // units/frame² — the pounce arc
        internal const float  FallGravity   = 0.08f;    // units/frame² — the fall off the pellet's line at full size (cave)
        // The land clip is s86 c04cat motion 7, frames 215..227 (KEY 69 in build_cat_pack.py keeps the absolute frames):
        // the paws first touch the ground at 219 — that is where the forward momentum stops; at 227 the run begins.
        internal const float  LandStopFrame = 219f, LandEndFrame = 227f;   // the paws touch at 219 (the land clip's lead-in aligns it with the touchdown)
        // The WINGED cat pounces from 50 units, and keeps
        // re-aiming at the target past the apex until it has fallen halfway from the apex to the floor (the cave's height
        // rule, Mailbox.CatTrackHalf). Dormant chest-mimics keep the 30-unit range.
        private const float  PounceRangeWinged = 50f;
        private const float  LandClipStart = 215f, LandClipSpeed = 0.36f;   // KEY 69 in build_cat_pack.py (frames/frame)
        // The clip lowers the cat itself (hips 5.5 → 4.6 over 215..219), so it must start this many frames BEFORE the
        // physical touchdown for the paws to meet the floor at 219; the cave predicts the touchdown from the fall.
        internal const float  LandLeadFrames = (LandStopFrame - LandClipStart) / LandClipSpeed;
        internal static float CatScale => _look.Scale;                  // the cat's size for this look; the cave grows the cat to this via Mailbox.CatScaleMul
        /// <summary>The ground speeds and glow are stated for the 1.0 cat and scale with its size, so the walk clip keeps
        /// the same rate whatever the look's size.</summary>
        private static float Stride => CatScale;
        // Ground game.
        internal static float RunSpeed => 1.3f * Stride;   // units/frame
        internal const float  PounceRange   = 30f;      // start the pounce within this of the target; the leap re-sizes itself at launch
        internal const float  MaxTargetDistance = 300f; // PickTarget: only enemies within the vanilla render distance of Xiao
        internal const float  KickStrength  = 2.0f, KickDecay = 0.3f;   // the hit's kickback, sized like Toan's heavier combo hits (1.2..3.0 / 0.2..0.4, type 2)
        internal const int    CatKickType   = 2;                        // the hit's kick type (+0x98): melee-style reaction; also the value a hurt sphere's spare[1] must hold to admit the cat at spare[0] % (ELF PatchCatSpherePercent; disc-baked on Minotaur Joe's face)
        internal const float  PounceFrames  = 32f;      // leap flight time (frames) to the enemy — most enemies
        internal const float  PounceFramesTall = 40f;   // … for the tall/large/flying set (EnemySpecies.VerticalLeapTargets) and minibosses: a higher, longer arc
        internal const float  HitRadius     = 4f;       // planted hit sphere at the struck enemy
        private const float  TouchRadius   = 3f;       // the cat's own touch radius in the cave's body-sphere test
        // Pounce clips (KEY 65 ready 95..105 @0.4, 67 take-off 190..204 @0.5, 68 leap 205..214 @0.5, 69 land 215..227 @0.36):
        // in place through the ready and the first take-off frames, forward momentum ramps over 194..198 and holds
        // through the leap and into the landing until the paws touch at 219. The clips carry the height.
        internal const float  ReadyStartFrame = 95f, ReadyEndFrame = 105f;   // every pounce is the ready crouch + float-up vertical leap; the take-off path is gone
        // The float-up clip is the town float's first ten frames (e04c04cat #5, source 160..169) at 285..294, play-once.
        // The cat keeps turning to the target through the whole clip and the jump is locked in the moment source
        // frame 169 arrives. A play-once clip holds just short of its last frame (Step stops the
        // rate once frame + rate reaches the end), so the launch test is end − 1, the same margin the other clips use.
        internal const float  FloatStartFrame = 285f, FloatSourceStart = 160f, FloatFeetOffSource = 169f;
        internal const float  FloatLaunchFrame = FloatStartFrame + (FloatFeetOffSource - FloatSourceStart) - 1f;   // 293
        internal const float  FloatRate       = 0.75f;   // the float-up's play rate (its KEY rate is 0.6)
        internal const float  FallBlendSteps  = 16f;     // the float-up → fall fade, in steps (the engine's default is 10)
        internal const float  BlendDefault    = 0.1f;    // the engine's own per-step blend increment (MOTION_END seeds it)
        internal const double LandSeconds   = 0.45, TakeOffSeconds = 0.4, RunTimeoutSeconds = 6.0, StraightRunSeconds = 1.5;
        internal const int    FadeTicks     = 30;       // ≈ 0.5 s at the 16 ms tick
        internal const int    GlowFadeTicks = FadeTicks; // the glow SHRINKS over the same ≈ 0.5 s the cat fades
        internal static int   _glowFade = -1;           // ticks into the glow's shrink (−1 = full size and following the cat; ≥ GlowFadeTicks = done: OFF until the next bind)
        private const float  DamageMult    = 1.5f;     // × the weapon's attack (a charged pellet's worth)
        internal const int    PlantedLifeTicks = 4;   // ~4 frames for the enemy's CheckDmg to find the entry

        // Hit-entry plumbing (CCollisionData pool, as AngelGear.PlantReflectedHit).
        internal const long BattleWeaponAttack = WeaponHave.BattleWeaponRecord + 0x04;

        internal enum Phase { Resident, Flying, Falling, Landing, Running, TakeOff, Leaping, LandEnd, Fading }   // Resident = built, hidden, waiting

        private static readonly bool[] _seenPellet = new bool[PlayerShotPool.SlotCount];
        private static bool   _holding, _flashed;
        private static DateTime _holdStart;
        private static double _holdSeconds;

        // The copy.
        internal static uint  _liveRoot, _copyRoot;
        internal static int   _nodeCount, _catIndex;
        internal static int   _key = -1;
        internal static float _alpha = 1f;
        // Flight state (world: x, h = height, y).
        internal static Phase _phase;
        internal static DateTime _phaseStart;
        internal static float _x, _h, _y, _yaw, _vx, _vh, _vy, _floor, _dirX, _dirY;
        internal static float _scale = 1f;                    // growth 0 → 1 over GrowSeconds (× CatScale)
        internal static long  _pool;                          // the shot pool the cat is pinned to
        internal static int   _pelletSlot = -1;               // its pellet's slot while that pellet lives, else −1
        internal static bool  _native;                        // the ISO carries the pellet catcher/follower cave
        private static bool  _nativeWarned;
        private const  double ArmPendingSeconds = 1.0;       // how long a released charge keeps trying to arm while the copy rebuilds
        private static DateTime _armPendingUntil = DateTime.MinValue;
        internal static bool  _caveOwns;                      // cave armed (waiting) or following: slot 1's pos/scale/opacity are its
        internal static int   _disarmTicks;                   // after a shot-less release: ticks until the waiting cave is disarmed
        private static DateTime _spawnFailedAt = DateTime.MinValue;
        internal static float _flightFrame0;                  // copy's motion frame at the bind (fall-pose check in the log)
        internal static DateTime _boundAt = DateTime.MinValue, _armedSince = DateTime.MinValue;
        internal static bool _gaitLogged, _blockedLogged;
        internal static int   _target = -1;
        internal static float _px, _ph, _py;                 // the flight POINT (where the pellet would be) — the head rides it
        internal static float _headX, _headH, _headZ;        // head rest offset in cat space (FindHead)
        internal const string HeadNodeName = "cat_kao";

        // ── how the cat LOOKS: the glow's geometry, then the per-weapon and per-element looks ──────────────
        internal const string GlowNodeA = "cat_kosibone", GlowNodeB = "cat_sebone2";   // hips + upper spine: the glow sits at their midpoint (the middle of the torso)
        internal static float GlowScale => 0.5f * Stride;   // the torch routine's scale: the flame sprite is 45 × 22.5 units at 1.0 (a 90-unit haze, half of it z-culled by the floor); 0.5 ≈ 22.5 × 11 around the torso
        internal const int    GlowFlags = 2;            // 1 = the steady glow pair (18 × 9 at 1.0), 2 = the flickering flame sprite (45 × 22.5 at 1.0), 3 = both (two sizes → two glows)
        internal const float  GlowLift  = 0f;           // units added to the glow's height (negative lowers it)
        internal const float  GlowPull  = 5.0f;         // how far toward the camera the sprite is pulled (the torches use 15 to clear their wall; the cat only needs to clear its own body)
        private const string CapeTexture = "catcape";  // the cape AND the mask draw from it (wing_bake.MASK_TEX)
        /// <summary>The element glow: ONE 8-bit disc carrying all six colours, repainted by ElfCave.CatGlowPalette the way
        /// the cape is — 5,184 B for every colour, against 16,448 B for a single 32-bit disc. The colours live in
        /// build_cat_pack.GLOW_ELEMENTS, not here: tune them there and re-bake.</summary>
        private const string ElementGlowDisc = "catglowp";
        // Per weapon: the Divine Beast Title keeps its blue glow, cyan tint and NO wings; the Angel Shooter wears the wings
        // with a WHITE glow and a neutral add; the Angel Gear the wings with a GOLD glow and a gold-white add. Every look
        // draws the SAME 8-bit disc, differing only in the palette row (PalRow → Mailbox.CatGlowPalRow; the rows themselves
        // are build_cat_pack.GLOW_LOOKS 6-8). Wings are two mesh nodes the copy hides by zeroing their geometry.
        internal sealed class WeaponLook { public int PalRow; public float[] Tint; public bool Wings; public bool Cape; public float Range = PounceRange; public bool Track; public float Scale = 1.0f; }
        private const int SuperSteveShooterKey = -2, SuperSteveGearKey = -3;   // Super Steve's look per sphere, keyed privately so a sphere swap rebuilds the copy
        // Super Steve: the BLUE cat of the Divine Beast Title, with a red cape. The mask's red cannot come from here — a
        // mesh has its tint ADDED to its lit colour, so this blue lands on the mask too and turns red to pink. The mask is
        // meant to be lit like the CAPE instead, which needs a per-node tint (see _capeTint).
        private static WeaponLook SuperSteveLook(float scale) => new WeaponLook { PalRow = 0, Tint = new[] { 12f, 24f, 48f }, Wings = false, Cape = true, Range = PounceRangeWinged, Track = true, Scale = scale };
        private static readonly Dictionary<int, WeaponLook> Looks = new Dictionary<int, WeaponLook>
        {
            { Items.divinebeasttitle, new WeaponLook { PalRow = 7, Tint = new[] { 12f, 24f, 48f }, Wings = false } },
            { Items.angelshooter,     new WeaponLook { PalRow = 8, Tint = new[] { 20f, 20f, 20f }, Wings = true, Range = PounceRangeWinged, Track = true, Scale = 1.1f } },
            { Items.angelgear,        new WeaponLook { PalRow = 9, Tint = new[] { 27f, 26f, 20f }, Wings = true, Range = PounceRangeWinged, Track = true, Scale = 1.2f } },
            { SuperSteveShooterKey,   SuperSteveLook(1.1f) },
            { SuperSteveGearKey,      SuperSteveLook(1.2f) },
        };
        internal static WeaponLook _look = Looks[Items.divinebeasttitle];
        /// <summary>The look key for the equipped weapon: its own id, or −1 for none — the cat stays down. Super Steve
        /// inherits the cat from its attached SynthSphere: a Divine Beast Title sphere gives the Title's cat exactly, while an
        /// Angel Shooter or Angel Gear sphere gives the BLUE cat (the Title's look, no wings) wearing a solid-yellow cloth
        /// cape, with the winged cat's range and tracking, at that sphere's size — keyed per sphere so a sphere swap rebuilds
        /// the copy like a weapon change.</summary>
        private static int LookKeyFor(int weapon)
        {
            if (weapon != Items.supersteve) return Looks.ContainsKey(weapon) ? weapon : -1;
            int sphere = SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord);
            if (sphere == Items.divinebeasttitle) return Items.divinebeasttitle;
            if (sphere == Items.angelshooter) return SuperSteveShooterKey;
            if (sphere == Items.angelgear)    return SuperSteveGearKey;
            return -1;
        }

        /// <summary>Per element: the cape/mask texture colour and the ambient the CAPE draws under. The CAT's own ambient is
        /// the same for every element — <see cref="ElementAmbient"/>.</summary>
        private sealed class ElementLook { public string Name; public byte[] Rgb; public float[] Tint; }
        /// <summary>The authored per-element appearance. ⚠ Each element's look is split across two files: the cape/mask
        /// colour and ambient here, its GLOW colour in build_cat_pack.GLOW_ELEMENTS (row per element) — tuning an element
        /// means touching both, and the glow half needs a re-bake and an ISO re-patch.</summary>
        private static readonly ElementLook[] ElementLooks =
        {
            // Every element binds the SAME disc (ElementGlowDisc) and differs only in the palette the cave paints into
            // it — so the glow colour is tuned in build_cat_pack.GLOW_ELEMENTS, not by naming a different texture here.
            new ElementLook { Name = "Fire",    Rgb = new byte[] { 128,  15,   0 }, Tint = new[] { 80f, 20f, 10f } },
            new ElementLook { Name = "Ice",     Rgb = new byte[] {   9,  45, 104 }, Tint = new[] { 12f, 40f, 48f } },
            new ElementLook { Name = "Thunder", Rgb = new byte[] { 180,  148,  0 }, Tint = new[] { 34f, 31f,  6f } },
            new ElementLook { Name = "Wind",    Rgb = new byte[] {   30, 100, 15 }, Tint = new[] {  8f, 46f, 37f } },
            new ElementLook { Name = "Holy",    Rgb = new byte[] {  193, 79, 160 }, Tint = new[] { 32f, 11f, 66f } },
            new ElementLook { Name = "None",    Rgb = new byte[] {   0,   0,   0 }, Tint = new[] {  0f,  0f,  0f } },
        };
        /// <summary>The CAT's ambient while the element look is on — the Angel Shooter's own 20/20/20, the same for every
        /// element — darker read too dull. The cape and mask keep their authored per-element ambients
        /// (<see cref="ElementLook.Tint"/>); only the cat is unified.</summary>
        private static readonly float[] ElementAmbient = { 20f, 20f, 20f };
        private const int  NoElement = 5;                // elementHUD: 00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy, 05 None
        private static int _weapon = -1;                                    // the weapon the resident copy was built for
        internal static readonly string[] WingMeshNodes = { "cat_rwingm", "cat_lwingm" };   // build_cat_pack / wing_bake.MESH_NAMES
        internal const string MaskNodeName = "cat_mask";                                    // wing_bake.MASK_NODE — the Super Steve
        internal static int _maskMeshIdx = -1;                                              // cat's domino mask, rigid to cat_kao
        internal static long _maskVisual;                                                   // …and its own copied CVisualMDT
        internal const string CapeNodeName = "cat_cape", CapeAnchorName = "cat_sebone2";     // wing_bake.CAPE_NODE / cat_wings.CAPE_ANCHOR
        /// <summary>The cape's body-collision capsules, in the order the .clo lists them — IN STEP WITH cat_wings.CAPE_BOUNDS.
        /// Every BOUND in the record names the cape node (the only name that resolves in HER tree at load); at spawn each cloned
        /// CBound is re-pointed at the cat copy's own bone, so the capsules ride the cat's spine and the cape drapes over it.</summary>
        internal static readonly string[] CapeBoundBones = { "cat_sebone2", "cat_sebone1", "cat_kosibone", "cat_kao" };
        internal static readonly List<int> _wingMeshIdx = new List<int>();
        internal const float  HeadFallbackHeight = 6f;
        internal static bool  _hitDone;
        internal static int   _fade;
        internal static readonly List<(int idx, int ticks, bool native)> _planted = new();   // native = planted by the cave (the cat's hit)
        internal static bool _hitFade;                        // the hit landed: the flight follows through while the cat fades out
        internal static int  _aimLoggedFor = -1;              // last target the aim choice was logged for

        // ──────────────────────────────────────── the mod's thread ─────────────────────────────────────────

        /// <summary>Whether the weapon in her hand wears a cat look: the Title, the Angel Shooter, the Angel Gear, or Super Steve
        /// carrying one of their spheres. The one gate for the thread and its launcher.</summary>
        internal static bool Wields() => Looks.ContainsKey(LookKeyFor(Memory.ReadUShort(WeaponHave.BattleWeaponRecord)));   // Super Steve's looks have NEGATIVE keys

        /// <summary>Xiao's Divine Beast Title thread: hands every tick to <see cref="Drive"/> while she wields a weapon with a look, Super
        /// Steve's spheres included, and stands the cat down once when it goes. The cat has exactly ONE driver: a spawn is hundreds of
        /// writes and a machine-side copy, and a second thread stepping in mid-way (Super Steve's loop, when the weapon changed under a
        /// look thread) corrupted the copy and once took the game down — so Super Steve does not pulse this one. The tick holds the cat
        /// itself through the PAUSE screen and the menus, so nothing is gated here.</summary>
        public static void DivineBeastTitleEffect()
        {
            while (Player.InDungeonFloor() && Wields())
            {
                Drive(true);
                Thread.Sleep(TickMs);
            }
            Stop();
        }

        private static readonly object _gate = new object();   // Drive and Stop never overlap, whoever calls

        /// <summary>The mod's tick. Decides each pass whether the cat may exist at all — Xiao, in a dungeon floor, a weapon with a look,
        /// no script event — and then which of four states applies: stood down, held for the PAUSE screen, held through a menu, or
        /// playing. A weapon change or a character switch tears the copy down; a menu does not. <paramref name="active"/> false stands
        /// everything down.</summary>
        internal static void Drive(bool active)
        {
            if (!active) { Stop(); return; }
            lock (_gate)
            try
            {
                bool inDun = Player.InDungeonFloor();
                int weapon = inDun ? LookKeyFor(Memory.ReadUShort(WeaponHave.BattleWeaponRecord)) : -1;   // Super Steve: by its sphere
                bool paused = inDun && Player.CheckDunIsPaused();                       // the PAUSE screen: the world stops, the cat waits
                bool menu = inDun && !paused && Player.CheckDunIsPausedOrMenu();        // the item menu: it can rebuild the texture manager under the copy — stand down
                bool armed = Enabled && inDun && Player.CurrentCharacterNum() == XiaoId
                          && Looks.ContainsKey(weapon)
                          && Memory.ReadInt(DungeonScriptEvent.BtEventMode) == 0;   // a script event deletes her MOTION 1 and rebuilds textures: stand down
                if (armed && Active && weapon != _weapon)
                {
                    Log($"weapon {_weapon} → {weapon}: rebuilding the cat with its look");
                    Despawn();
                }
                if (!armed) Stop();
                else if (paused)
                {
                    if (Active) { FreezeForPause(); Maintain(); }                       // hold the copy's frame; keep re-asserting it
                }
                else if (menu)
                {
                    // The menu does NOT wipe the cat's texture entries, so the cat is held here exactly as the PAUSE
                    // screen holds it. Only a weapon change or a character switch takes it away — both fall through to
                    // the !armed branch above. `menu` covers the menu ROOT, not just the weapon pane, which is what the
                    // cat has to survive.
                    if (Active)
                    {
                        FreezeForPause();
                        Maintain();
                        CheckTexturesStillOurs();                                       // safety net: a menu that DOES rebuild the manager still tears down
                        if (Active && _look.Cape) WatchElementLook();                   // recolour cape, mask and glow WHILE the element is being changed
                    }
                }
                else
                {
                    Resume();
                    WatchHerCatChannel();
                                if (Active && ++_texCheckTick >= 30)
                    {
                        _texCheckTick = 0;
                        CheckTexturesStillOurs();
                        CheckGuards();                                             // anything overwriting the copy's caves is named, once
                        if (Active && _look.Cape) WatchElementLook(force: true);   // re-assert the colour if the entries were remade
                    }
                    if (_armedSince == DateTime.MinValue) _armedSince = GameClock.Now;
                    // Built once, hidden, after the switch or menu has settled — her cat textures are still registering for
                    // a moment. Not the real guard: Spawn refuses and retries while they are absent from the manager, so
                    // arriving early costs a retry rather than a broken cat.
                    if (!Active && (GameClock.Now - _armedSince).TotalSeconds >= SettleSeconds) SpawnResident();
                    TrackCharge();
                    if (_native) PollCave(); else WatchPellets();
                    if (Active) { Step(); BreezeCape(); WatchCape(); if (_look.Cape) WatchElementLook(); }
                }
                if (!paused) RetirePlanted();
            }
            catch (Exception e)
            {
                Log("tick failed: " + e.Message);
                try { if (Active) Despawn(); } catch { }
            }
        }

        /// <summary>The weapon or the floor went: the cat down, the pause hold released, the charge forgotten.</summary>
        internal static void Stop()
        {
            lock (_gate)
            {
                if (Active) Despawn();
                Resume();
                _armedSince = DateTime.MinValue;
                _holding = false; _holdSeconds = 0;
                Array.Clear(_seenPellet, 0, _seenPellet.Length);
            }
        }

        // ── the PAUSE screen ───────────────────────────────────────────────────────────────────────────────
        internal static bool _held;   // the slot is drawn but not stepped: motion, cape cloth and shadow all stand still
        /// <summary>Hold the copy while the PAUSE screen or a menu is up. The engine keeps stepping a chara-slot character
        /// there, so the slot is marked skip-step in the loop's own table (<see cref="DungeonCharaDraw.StepSkipTable"/>):
        /// still drawn, nothing inside it touched. <see cref="Maintain"/> keeps the mark while held. The cave hooks the
        /// shot-pool step, which a hold does not run; the clock is <see cref="GameClock"/>'s to hold.</summary>
        private static void FreezeForPause()
        {
            if (_held) return;
            _held = true;
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 1);
            Log("paused — cat held");
        }

        /// <summary>Come back from a hold: let the loop step the slot again.</summary>
        private static void Resume()
        {
            if (!_held) return;
            _held = false;
            if (Active) Memory.WriteInt(DungeonCharaDraw.StepSkipTable + (long)Slot * 4, 0);
            Log($"resumed — clocks held for {GameClock.HeldTotal.TotalSeconds:F1} s in all");
        }

        // ── the cape, mask and GLOW follow the equipped weapon's ELEMENT ───────────────────────────────────
        // The colours are authored in ElementLooks, beside the per-weapon looks; what follows is the plumbing
        // that reads the element and the live slots it fills.
        private const byte CapeAlpha = 0x80;             // PS2 convention: 0x80 = fully opaque, as build_cat_pack bakes it
        /// <summary>Xiao's weapon-slot 0 element byte, + 0xF8 per bag slot (Player.Xiao.WeaponSlot0.elementHUD). It lives in
        /// the status block, well clear of the dungeon pools, so it needs no DungeonPools resolution.</summary>
        private static readonly long XiaoElementHud = Player.Xiao.WeaponSlot0.elementHUD;
        private const int WeaponSlotStride = 0xF8;

        /// <summary>The cat's OWN ambient as drawn — <see cref="WeaponLook.Tint"/> for every other look, the element's for
        /// the cape one. Never write through _look.Tint: those arrays are shared by the Looks table.</summary>
        private static readonly float[] _catTint = new float[3];
        private static readonly float[] _capeTint = new float[3];   // the cape's, from ElementLooks[e].Tint
        private static int _element = -1;                // the look last applied (-1 = none yet)

        /// <summary>The element Xiao's EQUIPPED weapon is set to, or None when it cannot be read.</summary>
        private static int ElementNow()
        {
            int slot = Memory.ReadByte(DngStatusData.Base + DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
            if ((uint)slot > 9) return NoElement;
            int e = Memory.ReadByte(XiaoElementHud + (long)WeaponSlotStride * slot);
            return (uint)e < ElementLooks.Length ? e : NoElement;
        }

        /// <summary>Follow the equipped weapon's element: repaint the cape/mask texture and swap the ambient. The tint is
        /// picked up by the next <see cref="WriteCapeTint"/> the fade already runs every tick, so it keeps fading in step.</summary>
        private static void WatchElementLook(bool force = false)
        {
            int e = ElementNow();
            if (e == _element && !force) return;
            var look = ElementLooks[e];
            Array.Copy(look.Tint, _capeTint, 3);                   // the cape and mask keep their authored per-element ambient
            Array.Copy(ElementAmbient, _catTint, 3);               // the CAT alone is the same under every element
            // The TEXTURE colour is the cave's when this ISO has one (ElfCave.CatPalette, called from the copy-queue cave
            // every dungeon frame): it reads the element itself, so the cape is right even with the app closed — and the
            // mod stops re-walking the texture manager by name, which cost up to 195 round trips per re-assert. The TINT
            // stays here: it rides the fade the mod already drives.
            bool painted = _native || PaintCapePalette(look.Rgb);
            if (e != _element)
            {
                WriteGlowName();                                  // every element binds the SAME disc now; clearing CatGlowReady re-binds it
                Log(
                                  $"element {look.Name}: cape/mask ({look.Rgb[0]},{look.Rgb[1]},{look.Rgb[2]}) under ambient "
                                  + $"({look.Tint[0]:F0},{look.Tint[1]:F0},{look.Tint[2]:F0})"
                                  + (painted ? "" : " — texture not in the manager yet"));
            }
            if (painted) _element = e;                                   // only latch once the colour actually landed
        }

        /// <summary>Point the glow cave at this look: the palette ROW it should paint (CatGlowPalRow), the disc to bind —
        /// always the same one — and CatGlowReady = 0 so it binds again, since the copy's texture entries are remade per
        /// spawn.</summary>
        internal static void WriteGlowName()
        {
            // EVERY look draws the same 8-bit disc and differs only in the palette row the cave paints into it. Row 0
            // means "derive it from the equipped element", which is what the cape look wants.
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowPalRow, _look.Cape ? 0 : _look.PalRow);
            byte[] nm = new byte[16]; Encoding.ASCII.GetBytes(ElementGlowDisc).CopyTo(nm, 0);
            Memory.WriteBytesBatch(CodeCaves.Mailbox.CatGlowName, nm);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowReady, 0);
        }

        /// <summary>Paint the flat texture's palette. build_cat_pack.flat_tim2 bakes the cape as 32×32 pixels that are ALL
        /// palette index 0 followed by 256 identical entries, so the whole cape is ONE palette entry: repainting the manager's
        /// own copy recolours it live, because the dungeon draw loop re-uploads the cat's texture group before drawing the
        /// slot — the same live-recolour path the palette caves use. Writing all 256 entries also makes it immune to CLUT
        /// ordering. The mask SHARES catcape (wing_bake.MASK_TEX), so it follows with no work of its own. Cheap to re-assert:
        /// one 4-byte read, and the 1 KB write only when the colour is not already there (a script event can wipe the
        /// entries, and they come back from the pack).</summary>
        private static bool PaintCapePalette(byte[] rgb)
        {
            long entry = FindTexEntry(CapeTexture);
            if (entry == 0) return false;
            uint clut = Memory.ReadGuestPtr(entry + TextureManager.EntryClut);
            if (!Memory.IsValidGuest(clut)) return false;
            long at = Memory.ToMmu(clut);
            byte[] cur = Memory.ReadBytesBatch(at, 4);
            if (cur != null && cur[0] == rgb[0] && cur[1] == rgb[1] && cur[2] == rgb[2] && cur[3] == CapeAlpha) return true;
            var pal = new byte[256 * 4];
            for (int i = 0; i < 256; i++)
            {
                pal[i * 4] = rgb[0]; pal[i * 4 + 1] = rgb[1]; pal[i * 4 + 2] = rgb[2]; pal[i * 4 + 3] = CapeAlpha;
            }
            Memory.WriteByteArray(at, pal);
            return true;
        }

        /// <summary>Give the cape a colour of its own. A cloth is drawn inside the CHARACTER's ambient window
        /// (Draw__10CCharacter adds the tint at +0xCE0, draws meshes then the cloth list, then restores), so it is lit by the
        /// cat's colour and not by anything of its own — the material rows do nothing. ElfCave.CatCapeTint wraps that one
        /// cloth's draw and adds <see cref="_capeTint"/> as a delta on top of the cat's, so it is written as
        /// (cape − cat); every other cloth in the game is untouched.</summary>
        internal static void TintCape()
        {
            if (_capeObj == 0) { Memory.WriteUInt(CodeCaves.Mailbox.CatCapeCloth, 0); return; }
            Array.Copy(ElementLooks[ElementNow()].Tint, _capeTint, 3);   // seed before the first write: SpawnCape calls this
            WriteCapeTint(1f);                                           // before WatchElementLook has run
            Memory.WriteUInt(CodeCaves.Mailbox.CatCapeCloth, Memory.ToGuest(_capeObj));
            Log($"cape tint: ambient ({_capeTint[0]:F0},{_capeTint[1]:F0},{_capeTint[2]:F0}) for its draw alone, as a delta off the cat's ({_look.Tint[0]:F0},{_look.Tint[1]:F0},{_look.Tint[2]:F0})");
        }

        /// <summary>The cape's ambient delta (cape − cat), faded with the cat so the two never drift apart mid-fade.</summary>
        private static void WriteCapeTint(float lit)
        {
            for (int i = 0; i < 3; i++)
                Memory.WriteFloat(CodeCaves.Mailbox.CatCapeTint + i * 4, (_capeTint[i] - _catTint[i]) * lit);
        }

        // ───────────────────────────────────────── charge + launch ─────────────────────────────────────────

        /// <summary>Hold time on the draw/nocked states → the shot is charged past <see cref="ChargeSeconds"/>;
        /// the game's own charge-complete pulse marks it. Frozen on release so the fired pellet reads it.</summary>
        private static void TrackCharge()
        {
            int state = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = state == PlayerAction.XiaoShotDraw || state == PlayerAction.XiaoShotHold;
            ChargedShotWhp.Tick();
            if (holding)
            {
                if (!_holding) { _holding = true; _holdStart = GameClock.Now; _flashed = false; }
                _holdSeconds = (GameClock.Now - _holdStart).TotalSeconds;
                if (_holdSeconds >= ChargeSeconds && !_flashed) { Player.FlashChargeComplete(); _flashed = true; }
                ChargedShotWhp.Arm(_holdSeconds >= ChargeSeconds ? ChargedShotWhp.ChargedFactor : 1f);   // the cat shot's weapon HP
                ChargeTint.Ramp(_flashed ? 0 : ChargeSeconds - _holdSeconds);                            // toward the flash, then nothing
            }
            else
            {
                ChargeTint.Clear();
                if (_holding)
                {
                    // Arm on RELEASE, not when the charge completes: arming HIDES the cat that is already out (scale 0,
                    // opacity 0) and resets the cave's state, and there is only ONE copy, so a cat in flight lasts until
                    // the next shot is actually fired. The shoot motion takes ~0.5 s to spawn the pellet against a 16 ms
                    // tick, so the cave is waiting long before its birth frame. Nothing is torn down: the copy persists
                    // either way — only its visibility moves.
                    if (_holdSeconds >= ChargeSeconds) _armPendingUntil = GameClock.Now.AddSeconds(ArmPendingSeconds);
                    else if (_caveOwns && _phase != Phase.Flying) _disarmTicks = 30;   // a cancelled charge, nothing coming
                }
                _holding = false;
                if (_armPendingUntil != DateTime.MinValue)
                {
                    // Retried until the copy is up: a charge released while it is still rebuilding — just after a menu
                    // close — would otherwise be dropped silently, the flash firing with no cat behind it.
                    if (_native && Active)
                    {
                        ArmCave();
                        _disarmTicks = 30;                        // AFTER ArmCave, which zeroes it: no pellet within ~0.5 s = the shot never happened
                        _armPendingUntil = DateTime.MinValue;
                    }
                    else if (GameClock.Now >= _armPendingUntil) _armPendingUntil = DateTime.MinValue;
                }
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
        /// frame: the growth must start the very frame the pellet is created. A failed spawn is
        /// retried after a pause rather than every tick.</summary>
        private static void SpawnResident()
        {
            if (_spawnFailedAt != DateTime.MinValue && (GameClock.Now - _spawnFailedAt).TotalSeconds < 5) return;
            _scale = 0f; _alpha = 0f; _target = -1; _pelletSlot = -1; _caveOwns = false; _disarmTicks = 0;
            _yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            _x = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos);
            _h = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 4);
            _y = Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + 8);
            _weapon = LookKeyFor(Memory.ReadUShort(WeaponHave.BattleWeaponRecord));   // the look key (Super Steve: by its sphere)
            _look = Looks.TryGetValue(_weapon, out var lk) ? lk : Looks[Items.divinebeasttitle];
            Array.Copy(_look.Tint, _catTint, 3);                   // the weapon's own ambient; the element may override it below
            if (!Spawn()) { _spawnFailedAt = GameClock.Now; return; }
            _spawnFailedAt = DateTime.MinValue;
            WriteGlowName();
            var hide = new List<int>();
            if (!_look.Wings) hide.AddRange(_wingMeshIdx);
            if (!_look.Cape && _maskMeshIdx >= 0) hide.Add(_maskMeshIdx);
            HideMeshes(hide, !_look.Wings && !_look.Cape ? "wings and mask" : !_look.Wings ? "wings" : "mask");
            GoLive();                                                                      // the engine may step the copy only now: nothing of hers is reachable from it
            if (_look.Cape) { SpawnCape(); MaskTint(); WatchElementLook(force: true); }   // colour before the first frame draws
            Log($"look for weapon {_weapon}: glow row {(_look.Cape ? "element" : _look.PalRow.ToString())}, wings {(_look.Wings ? "on" : "off")} ({_wingMeshIdx.Count} wing meshes in the copy), mask {(_look.Cape ? "on" : "off")} (n{_maskMeshIdx})");
            _native = (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew;
            if (!_native && !_nativeWarned) { _nativeWarned = true; Log("pellet-catcher cave not in this ISO (re-patch) — using the thread follower"); }
            if (_native) { Memory.WriteInt(CodeCaves.Mailbox.CatPelletSlot, 0); Memory.WriteInt(CodeCaves.Mailbox.CatState, 0); }
            _phase = Phase.Resident; _phaseStart = GameClock.Now; _hitDone = false; _fade = 0;
            SetKey(KeyLeap);
            Maintain();
            Log("cat resident (hidden) — " + (_native ? "native catcher" : "thread follower"));
        }

        /// <summary>Play a clip on the copy: motion id, play-once cleared and restart set so a fresh key loops from its start
        /// (the hit sets play-once instead), and the rate returned to the KEY's own.</summary>
        internal static void SetKey(int key)
        {
            if (!Active) return;
            _key = key;
            long s = SlotAddr();
            Memory.WriteInt  (s + CCharacter.MotionId, key);
            SetMotionFlags(set: CCharacter.MotionRestart, clear: CCharacter.MotionPlayOnce);   // a fresh key loops again (the hit sets play-once)
            Memory.WriteFloat(s + CharacterMotion.MotionSpeedOffset, CharacterMotion.MotionSpeedUseKey);
        }

        private const  int HitElemEvery = 6;          // ticks between element-drift checks (~100 ms at TickMs 16)
        private static int _hitElemTick;
        /// <summary>Re-assert everything the engine or another system could take back: the copy's model pointer, position,
        /// scale, opacity, facing and motion key (all except those the cave owns mid-flight), and the cat's own light —
        /// Draw__10CCharacter adds CharaTint (+0xCE0) to the scene ambient on a 0..255 scale where the dungeon's key lights
        /// are ~100-120, scaled here by the fade so the cat dims as it goes. Despawns if her model changed underneath us.
        /// Runs every tick in all three active states, so anything that must hold regardless of state belongs here.</summary>
        internal static void Maintain()
        {
            if ((Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel)) != _liveRoot)
            {
                Log("her model changed — cat despawned");
                Despawn();
                return;
            }
            // The hit stamp is written at BIND, so a cat already in flight kept the element it was FIRED with: an ice cat
            // switched to thunder still hit for ice. CONFIRMED in the log — the contact stamped attr 0x2 four seconds after
            // switching to thunder, so the DAMAGE was wrong, not merely the hit visual. Re-stamp whenever
            // the live element stops matching what is stamped. This belongs HERE and not in WatchElementLook, which only runs
            // for the cape look, while the weapon's element drives damage for EVERY look. ⚠ Only while a stamp is LIVE:
            // CatHitDamage is deliberately 0 until bind, and a non-zero value on a hidden copy once let it deal a 1-damage
            // hit on its first frame. Rate-limited: an element change mid-flight is not frame-critical, and the
            // full check is six PINE reads that would otherwise run every 16 ms for the whole flight.
            if (++_hitElemTick >= HitElemEvery)
            {
                _hitElemTick = 0;
                if (Memory.ReadInt(CodeCaves.Mailbox.CatHitDamage) != 0)
                {
                    uint live = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
                    uint want = (live != 0 && (live & (live - 1)) == 0) ? live : 0u;      // one pure element bit or none
                    if (Memory.ReadInt(CodeCaves.Mailbox.CatHitAttr) != (int)want) WriteHitStamps();
                }
            }
            long s = SlotAddr();
            if (!_caveOwns)                                                      // armed/following: position, scale and opacity are the cave's
            {
                Memory.WriteVec3(s + CCharacter.CharPos, _x, _h, _y);
                Memory.WriteVec3(s + CCharacter.CharScale, CatScale * _scale, CatScale * _scale, CatScale * _scale);
                Memory.WriteFloat(s + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, _alpha)));
            }
            else if (_hitFade) Memory.WriteFloat(s + CCharacter.NpcOpacity, 128f * Math.Max(0f, Math.Min(1f, _alpha)));   // the cave keeps the pose; only the opacity is ours (a plain fade, no shrink)
            float lit = Math.Max(0f, Math.Min(1f, _alpha));
            Memory.WriteVec3(s + CCharacter.CharaTint, _catTint[0] * lit, _catTint[1] * lit, _catTint[2] * lit);   // ambient ADD, per weapon (Looks) and per element
            if (_capeObj != 0) WriteCapeTint(lit);                                  // the cape rides the same fade, one step further red
            Memory.WriteVec3(s + CCharacter.CharRot, 0f, _yaw, 0f);   // Euler x, yaw, z
            Memory.WriteUInt (s + CCharacter.CharModel, _copyRoot);
            if (!_caveOwns) Memory.WriteInt(s + CCharacter.MotionId, _key);      // the cave sets leap/land/run keys on its own frames
            Memory.WriteInt  (s + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt  (s + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt  (DungeonCharaDraw.CharaRegistry + (long)Slot * 4, 1);
            Memory.WriteInt  (DungeonCharaDraw.StepSkipTable + (long)Slot * 4, _held ? 1 : 0);   // a hold keeps the slot unstepped
            Memory.WriteInt  (CodeCaves.MirageSceneGateFlag, 1);
        }

        /// <summary>Take the cat off screen: unregister the draw slot, zero its opacity and model pointer, detach the cape
        /// cloth list (it lives in the mod's cave and must never be left on a slot handed back), disarm the cave and return
        /// the textures to her block. Frees nothing — the copy, its meshes and its caves stay allocated for the next spawn.</summary>
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
            _lastDespawn = GameClock.Now;
            Log("cat copy down");
        }

        // ─────────────────────────────────────────── the hit ───────────────────────────────────────────────

        /// <summary>One pellet-style CollisionData entry at the pounce (AngelGear.PlantReflectedHit's
        /// recipe): base = the weapon's attack × <see cref="DamageMult"/>, the weapon's selected element as a pure
        /// bit (or none), her anti-category bytes and ability flags — CheckDmg does the rest.</summary>
        /// <param name="ox">…the kick's origin (the cat): CheckDmg pushes the enemy along enemy − origin with strength/decay
        /// from the entry when its type word (+0x98) is 2 — the same words Toan's sword hits carry, so the enemy's own
        /// hit reaction (flinch + shove) runs exactly as for a melee hit. A pellet's entry has type 0: no reaction.</param>
        internal static void PlantHit(float x, float h, float y, float radius, int baseDmg, float ox, float oh, float oy)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int slot = CollisionPool.TakeFreeSlot(pool);
            if (slot < 0) { Log("no free collision entry — pounce lost"); return; }
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;      // one pure element bit or none
            byte[] e = CollisionPool.PlayerHitEntry(x, h, y, radius, baseDmg, attr);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x80, ox); F(0x84, oh); F(0x88, oy);                                  // kick origin (a point)
            F(0x90, KickStrength); F(0x94, KickDecay);                              // kick strength, decay
            BitConverter.GetBytes(CatKickType).CopyTo(e, 0x98);                     // type 2 = melee-style reaction
            CollisionPool.Plant(pool, slot, e);
            lock (_planted) _planted.Add((slot, PlantedLifeTicks, false));
            Log(
                $"hit entry at ({x:F1},{h:F1},{y:F1}) r={radius:F0}: base {baseDmg}, attr 0x{attr:X} → entry {slot}");
        }

        /// <summary>Per-tick housekeeping for the hit. Restores crushed guard windows once their countdown expires, and
        /// decides the fate of each planted damage entry: an entry that vanished AND left some slot's "last hit sphere" word
        /// (+0x55750) no longer −1 was ACCEPTED — CheckDmg only overwrites that sentinel past its guard and invincibility
        /// gates, right before applying damage — so the cat is spent and fades. An entry that vanished without it was merely
        /// dropped by the engine, and the latch is freed so the cat may touch again.</summary>
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
                long pool = CollisionPool.Resolve();
                for (int i = _planted.Count - 1; i >= 0; i--)
                {
                    var (idx, ticks, native) = _planted[i];
                    if (native && pool != 0 && !CollisionPool.IsActive(pool, idx))
                    {
                        // The entry is gone. Accepted = some enemy's CheckDmg overwrote the −1 the cave stamped into every
                        // slot's "last hit sphere" word (+0x55750) at the plant — it only does so past its guard and
                        // invincibility gates, right before applying the damage. Gone without that = the engine dropped it
                        // (a mimic wakes with 100 invincibility frames): not spent, the latch is freed.
                        int hitSlot = -1;
                        for (int s2 = 0; s2 < 16 && hitSlot < 0; s2++)
                            if (Memory.ReadInt(BodyCollision.SlotBase(s2) + BodyCollision.LastHitSphere) != -1) hitSlot = s2;
                        if (hitSlot >= 0)
                        {
                            // Accepted: damage, hitspark, kick and (via the patched flinch rule) the stagger are all the engine's.
                            // Only now is the cat spent.
                            Log($"hit landed on enemy slot {hitSlot} (entry {idx}) — fading out");
                            _hitFade = true; _fade = 0; _alpha = 1f;
                        }
                        else
                        {
                            Memory.WriteInt(CodeCaves.Mailbox.CatHitLatch, 0);
                            Log($"contact was not accepted (entry {idx} gone, no enemy took it — invincible or guarding) — no hit, still flying");
                        }
                        _planted.RemoveAt(i); continue;
                    }
                    if (--ticks > 0) { _planted[i] = (idx, ticks, native); continue; }
                    if (pool != 0) CollisionPool.Deactivate(pool, idx);
                    if (native)
                    {   // never consumed: the enemy was invulnerable or the sphere's hurt window was closed — not spent; the cave may contact again
                        Memory.WriteInt(CodeCaves.Mailbox.CatHitLatch, 0);
                        Log($"contact did not connect within {PlantedLifeTicks} ticks (entry {idx}) — no hit, still flying");
                    }
                    _planted.RemoveAt(i);
                }
            }
        }

        // ───────────────────────────────────────────── utils ───────────────────────────────────────────────

        internal static long SlotAddr() => DungeonCharaDraw.CharaArray + (long)Slot * DungeonCharaDraw.CharaStride;
        internal static void Log(string message) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + message);

        /// <summary>Read-modify-write the copy's MotionFlags: <paramref name="clear"/> bits off, then <paramref name="set"/> bits on.</summary>
        internal static void SetMotionFlags(int set = 0, int clear = 0)
        {
            long f = SlotAddr() + CCharacter.MotionFlags;
            Memory.WriteInt(f, (Memory.ReadInt(f) & ~clear) | set);
        }
    }
}
