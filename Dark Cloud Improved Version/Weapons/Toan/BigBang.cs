using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang — Solar Harvest from the Sun Sword line, a whirlwind that IS an explosion, and a guard
    /// charge that hangs a judgement blade over the locked target and drops it (Detonate).</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        private const int    TickMs           = 30;
        // THE WHIRLWIND is the detonation: as the spin begins, the SAME blast the dropped blade makes goes off at Toan's
        // feet (PlantFalloff — the falloff steps, elementless, the kick, every enemy turned to it), and the spin's own hit
        // sphere is taken out of reach so nothing is struck twice. (The lunge and the combo are ordinary swings; the
        // judgement blade below is the other blast.)
        private const float  WhirlNoHit       = -1000f; // the whirl's own hit radius while it is armed: no enemy is inside it
        // ── the whirl's model ────────────────────────────────────────────────────────────
        // Toan's whirlwind visual IS the main-character effect instance — the same instance BorrowedShots borrows
        // for Xiao (hers sits idle holding an unused mgan01, his holds c01_fuusya). Seeding it with
        // `dun/effect/explosion.chr` therefore REPLACES the whirl rather than adding to it, which is the intent, and
        // the engine fires it on the spin by itself. No ISO patch: CustomConfig names any container on the disc.
        //
        // The container is a full pack — explosion.cfg (VERTEX_ANIME, BODY_SIZE 17,7,60, the same as c01_fuusya's),
        // explosion.mds (root frame "null3"), explosion.mot (one motion, KEY 5-30 at speed 0.2) and explosion.img.
        // Only the MUZZLE phase names a motion, matching the shape of the whirl's own config (c01_fuusya is "muzzle
        // motion 0, nothing after"). Naming all four played the explosion three times over — once at the muzzle, once
        // at the impact and once at the expiry burst — which is what "it fires multiple explosions" was.
        private const int    ExplosionTemplate = 5;     // a stock config's shape; the name and motions are replaced
        private const string ExplosionName     = "explosion";
        private const float  ExplosionScale    = 1.0f;  // the size it was authored at
        // Its root frame, as CFrameVu1.Name reads it: "null" then "3". The fuusya path keys on "kiru" and checks the
        // NEXT frame for "fkiri", so it cannot validate this model — hence the separate scale pass below, and
        // Weapons.WhirlScaleStandDown while this is the model in the instance.
        private const uint   ExplosionRootWord = 0x6C6C756E, ExplosionRootTail = 0x00000033;
        private static BorrowedEffect _explosion;
        private static readonly float[] _burstBind = new float[9];
        private static bool _burstBindRead;
        // ── the whirl's blast IS its own hit ─────────────────────────────────────────────
        // Nothing here detects a hit or plants a sphere for it. ToanKey_Play built its two charge-attack hit radii as
        // baked immediates; the ISO patch (ElfWeaponPatches.PatchChargeHitRadius) turned them into the data words
        // CodeCaves.ChargeHitRadius, so widening the whirl's word widens the ENGINE'S OWN sphere — and the engine
        // then does the whole job: who is inside it (real hurtboxes, height included), one plant per frame so a spin
        // sweeps several enemies, the damage, and the knockback. What the ability contributes is all data written
        // while the charge is up: the radius, the boosted Attack, the elementless attribute, and the kick. The
        // lunge's word is held at its vanilla 6.
        private const float  KickStrength     = 3.5f;   // the judgement blade's kick; with KickDecay: distance ≈ force²/(2·decay) ≈ 50 units
        private const float  KickDecay        = 0.12f;  // vanilla melee is 1.2 at 0.2, roughly 3.6 units

        private const float  BlastWhp         = 20f;    // weapon HP a blast costs — the whirlwind's or the dropped blade's — before Endurance scales it (WeaponWhp: the engine's own drain takes it, and breaks the blade at 0)
        // THE BLADE ON A REGULAR CHARGE. Toan's charge meter runs 1.0 → 3.0 (lunge at 1.5, whirlwind at 2.5), and
        // the blade whitens across it exactly as it does for a guard charge — the same tint, driven by the meter
        // instead of by held time. It stands aside while SunSword.FlashArmed: Solar Flash owns the blade then, and
        // two ramps fighting over one mesh would only flicker.
        private const float  ChargeMeterFloor = 1.0f;
        // The whirl's numbers are ARMED from the moment the meter reaches whirlwind range, so they are in place before
        // the spin's first damage frame — a first hit at the blade's plain attack would also make that enemy
        // invincible to the boosted frames that follow.
        private const float  WhirlThreshold   = 2.5f;   // the meter at which the charge becomes a whirlwind
        // ── immunity to explosions ───────────────────────────────────────────────────────
        // The four shot configs that ARE the explosions: Halloween's thrown pumpkin and the three self-destructs
        // (zibaku = 自爆) that Mr. Blare, Bomber Head, Sam and Billy blow themselves up with. Their entries take the
        // reaction straight from the config word, and BtCheckDamageProc subtracts the player's HP inside its
        // reaction branches — 3, and 2-or-4 — so a reaction outside that set is INERT: the entry is consumed and
        // nothing reaches him. No code patch, no pool scanning, four words.
        //
        // ⚠ Global, static ELF data shared by every enemy of those species, so it MUST be put back when the blade
        // goes away — Reset does it. Enemies are unaffected either way: CMonstorUnit::CheckDmg never reads the
        // reaction, so a reflected shot still damages them normally.
        private static readonly int[] ExplosionCfgs = { 3, 16, 17, 18 };   // pump_bom, zibaku_f2, zibaku_r2, zibaku_t2
        private const int    ReactionInert   = 5;      // 1 and 5 are both unhandled; 4 is the light flinch and DAMAGES
        private static readonly int[] _cfgReaction = new int[ExplosionCfgs.Length];
        private static bool  _immune;

        // ⚠ SHARED ELF WORDS, held only while the charge is up: the whirl's hit radius (CodeCaves.ChargeHitRadius) is
        // every charge attack's, and RestoreSwing puts the stock figure back on the spend, a dropped charge, a swap and
        // a floor change. The blast's own kick rides its hit entries (PlantFalloff), not the shared kick words.
        // The explosion is the thrown-gem FIRE burst, spawned at the hit point by plain field writes into the
        // always-resident Maseki pool. It is authored as a small thrown-gem puff, so it is scaled up hard and
        // slowed down to stop the animation snapping at that size. Scale is the sub-slot's own CObject scale, not a
        // radius: it does not change what the blast HITS (that is the falloff's hit entries).
        // ── what an auto-guarded explosion feels like ────────────────────────────────────────
        // The cave (ElfWeaponPatches.PatchAutoGuardMatch) makes the engine forget the hit entirely, which is what
        // keeps Toan's charge alive — but a hit that is silently dropped feels like a bug. So the cave ticks a
        // counter and the mod answers it here with the controller shove the engine itself uses on a hit (its own are
        // motor 1 at 0xE6/22 frames for a knockdown, 0xDC/12 for a lighter one — this sits under both) and the guard
        // clang. No flinch: the only one available without an action change is a tint pulse, and it read as noise.
        private const int    GuardRumble      = 0xC0, GuardRumbleFrames = 10;
        private const ushort GuardSe          = 0xA2;   // the engine's own guard-clang SE
        private static int   _guardSignal = -1;

        // ── the judgement blade ─────────────────────────────────────────────────────────────
        // While Solar Flash is primed AND Toan is locked on, a copy of the sword (BladeProp) hangs point-down over the
        // target at OwnerScale(), fading in over FadeSeconds; the blue glow moves onto it and the target's NAME plate is
        // hidden (CodeCaves.NameHide, the gate the plate's getter ANDs in — the enemy itself is never touched, so a
        // kill during the hover still counts for whatever counts kills). Losing the lock fades it out and the glow
        // shrinks off it and swells back up on Toan. The primed swing then does not flash: the blade FALLS under gravity, and where
        // it lands it detonates — the flash, the blast below, every enemy on the floor turned to face it, and the
        // weapon-HP bill. Swinging with no lock is the ordinary flash.
        // The blade hangs with its tip just above the enemy's HP gauge (HoverHeightFor: the game's own lock-on point,
        // so a species whose script declares a lock-on frame gets a dev-authored body point, the rest the origin).
        // THE HOVER HEIGHT: the blade's tip HoverMargin above the species' AUTHORED height (EnemyDefaults.HeightFromRoot, scaled
        // with a grown miniboss) — nothing is measured at runtime. HoverFallback for a species with no record.
        private const float  HoverMargin      = 6f;
        private const float  HoverFallback    = 20f;
        // The copy hangs point-DOWN from its root (the grip), so the TIP is the blade's length below the placement;
        // the grip goes up by that much so the tip is what clears the target. The length is the weapon's dcol1 reach
        // (the offline table Weapons keeps), at the copy's scale.
        private const float  BladeLengthFallback = 12f;
        private const float  HoverScale       = 2.0f;
        private const double FadeSeconds      = 0.25;
        // THE FALL is gravity: from rest at the hover height, h = h0 − ½·g·t², so a higher hover takes longer to land,
        // t = √(2·h0/g). The constant is a judgement's gravity, not the Earth's (9.81 m/s² at ~5.5 units a metre is 54
        // and made even a short enemy a 0.8 s wait): 500 u/s² lands the tip on a 12 u enemy in ~0.27 s, a 25 u one in
        // ~0.35 s, a 40 u miniboss in ~0.43 s. Placed by its own frame-rate thread (BladeLoop), the way the slingshot prop's orbit thread
        // re-places that copy: the draw re-seeds the root from the slot every frame, so writes at frame rate are
        // smooth where the 30 ms tick was a staircase. Cosmetic by construction — the landing itself is still called
        // from the tick, so nothing that matters rides on this thread's timing.
        private const double Gravity          = 500.0;   // units/s²
        private const int    FallTickMs       = 2;      // the placement cadence: a frame is ~16.7 ms, and the phase between a placement and the frame that
                                                          // samples it is what reads as jitter — the shorter the period, the smaller that phase error
        // As the blade falls the floor's light and fog are driven DOWN (SolarLighting.Dim) on an EXPONENTIAL ramp
        // that peaks at the landing itself: k = (e^(a·u) − 1) / (e^a − 1) over the fall's fraction u, so it barely
        // moves at first and plunges in the last moments, with the flash then landing from the darkest frame.
        // The sharpness is SolarLighting.RampSharpness — higher holds the light longer and drops it later. Driven from
        // the fall thread (the same curve as the fall) and handed to the flash at the landing.
        // …and the flash follows the burst — same tick, but never before it; a head start read as late.
        private const double FlashDelay       = 0.0;
        // THE BLAST FALLS OFF WITH DISTANCE: the damage step an enemy takes is the innermost radius its distance from the
        // blast is within (outermost first here; PlantFalloff keeps the last match). One hit entry per enemy, centred
        // on its own body, carries that step — an entry is consumed by the first enemy it touches, so the sizing is
        // what makes it that enemy's alone.
        private static readonly (float radius, float times)[] Falloff = { (50f, 1f), (40f, 2f), (25f, 3f), (10f, 4f) };

        /// <summary>Who the judgement blade is hanging for. Big Bang's own, or the Sword of Zeus's (SwordOfZeus.Judgement):
        /// the weapon whose primed state hangs it, the glow disc it carries, the profile whose prime dim the fall darkens
        /// from, whether every enemy is turned to watch it fall, and what happens where it lands.</summary>
        internal sealed class JudgementOwner
        {
            internal ushort WeaponId;
            internal string Glow;
            internal SunSword.SolarProfile Profile;
            internal bool   Redirect;
            internal bool   ToTheHilt;                        // the fall ends with the GRIP at the ground — the whole blade in it — rather than the tip
            internal bool   RampWholeFall;                    // the room darkens along the whole fall (Big Bang), not only its last RampFrames
            internal Action<int, float, float, float> Land;   // (target slot, x, h, y)
            // What another owner may bring in place of Toan's sword (all optional; null/0 = the sword's own):
            internal Func<bool>  IsPrimed;                    // whether the charge that hangs the copy is held (SunSword.PrimedFor otherwise)
            internal float       Scale;                       // the copy's scale (OwnerScale() otherwise)
            internal Func<float> Length;                      // the model's reach below its root at 1× — how far above the ground it stops (the blade's dcol1 otherwise)
            internal Func<uint>  SpawnRoot;                   // the model to copy (the equipped weapon otherwise)
            internal bool        Upright;                     // copied as authored rather than turned point-down
            internal float[]     Tint;                        // the copy's ambient add, per channel
            internal int         GlowRow;                     // the glow cave's palette row for the disc (0 = the disc's own)
            internal float       GlowScale;                   // the disc's size (0 = Toan's)
            internal float       GlowLift = float.NaN;        // the disc's height over the copy's root (NaN = half the length below it)
        }
        private static bool  Primed()      => _owner.IsPrimed != null ? _owner.IsPrimed() : SunSword.PrimedFor(_owner.WeaponId);
        private static float OwnerScale()  => _owner.Scale > 0f ? _owner.Scale : HoverScale;
        private static float OwnerLength() => _owner.Length != null ? _owner.Length() : BladeLength();
        private static bool  SpawnCopy()
        {
            uint root = 0;
            if (_owner.SpawnRoot != null && (root = _owner.SpawnRoot()) == 0) return false;   // the owner's model is not to be had yet
            if (!BladeProp.Spawn(OwnerScale(), root, !_owner.Upright)) return false;
            if (_owner.Tint != null) BladeProp.Tint(_owner.Tint[0], _owner.Tint[1], _owner.Tint[2]);
            return true;
        }
        private static readonly JudgementOwner BigBangOwner = new JudgementOwner
        { WeaponId = Items.bigbang, Glow = ToanGlowBakes.BlueName, Profile = SunSword.BigBangFlash, Redirect = true, RampWholeFall = true, Land = LandBigBang };
        private static JudgementOwner _owner = BigBangOwner;
        /// <summary>The hover and the fall, ticked for another sword: the same copy, fade, glow and gravity as Big Bang's.</summary>
        internal static void JudgementTick(JudgementOwner owner) { _owner = owner; JudgementTick(); }
        /// <summary>Everything the judgement blade put up, down (a sword put away, a floor left).</summary>
        internal static void ReleaseJudgement()
        {
            AbandonHover(); Dropping = false; _landed = false; _fallDone = false; _landedAt = default; SolarLighting.EndDim(); GlowOwned = false;
        }

        internal static bool GlowOwned { get; private set; }   // the blue glow is on the blade copy, not on Toan
        internal static bool Dropping  { get; private set; }
        private static bool   _landed;
        private static int    _hoverSlot = -1;                 // the enemy the blade hangs over (−1 = none up)
        private static float  _hoverAlpha;                     // 0..1, the fade
        private static bool   _hoverOut;                       // fading OUT (lock lost) — no re-placement
        private static DateTime _dropStart, _gateLog;
        private static float  _dropX, _dropH, _dropY;          // where the BLAST goes off: the target's root, on the ground
        private static float  _fallHeight;                     // how far above _dropH the grip hung when it was let go
        private static float  _fallStop;                       // where the grip stops above _dropH: a blade length, the tip at the root
        private static int    _followSlot = -1;                // the unit the fall thread places the hover over (shared root); −1 = pinned
        private static float  _spotH;                          // a point hover's ground height (a spot on the floor, not a unit)
        private static int    _heightSlot = -1;                // the target the hover height was measured for
        private static float  _bladeX, _bladeY;                // where the BLADE falls to (the target itself)
        private static volatile bool _fallDone;                // the fall thread has brought it to the ground
        private static float  _paceFrom, _paceTo;              // a fall PACED by Toan's swing: the frame cursor from…to (0 = gravity's own time)
        private static double _fallSeconds;                    // …or a fall over a FIXED time (0 = not this)
        private static float  _fallStart;                      // the grip's world height as the fall began (the engine steps it from here)
        // A POINT HOVER: a blade the owner hangs itself — over a spot on the ground, or over a unit at a fixed height — and
        // lets go on its own cue (the Sword of Zeus's charge attack), outside the lock-on hover's gates below.
        private static bool   _pointHover, _pointDriven, _pointFading;   // driven: its fade-in is set by hand (PointAlpha); fading: on its way out
        private static bool   _pointRides;                     // the point hover rides TOAN through the cave (a spot ahead of him), not the mod thread
        private static float  _pointX, _pointY;                // the spot (when not over a unit)
        internal static DateTime PointLandedAt { get; private set; }   // when a point hover's blade last reached the ground (default = none)
        private static DateTime _landedAt;                     // when the burst went off; the flash waits FlashDelay
        private static Thread _fallThread;
        internal static bool LandingPending => _landedAt != default;
        private static float  _hoverHeight = 20f;              // the grip's height above the target's root, read ONCE per target at rest (HoverHeightFor)
        private static int    _hoverTraceTicks;

        // ⚠ The ISO patch this ability's damage depends on, as the patched instruction reads: `lui $2,0x01FB`
        // (ElfWeaponPatches.PatchChargeHitRadius). Checked once per floor, because without it the radius words are
        // never read and the charge attacks stay their stock 6 and 12 — which looks exactly like the ability
        // silently doing nothing.
        private const long   LungeRadiusInsn  = 0x20241AC0;
        private const uint   LungeRadiusPatched = 0x3C0201FB, LungeRadiusVanillaInsn = 0x3C0240C0;
        private const float  BurstScale       = 10.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Ice;    // the ANIMATION only — the fallback when explosion.chr is not entered
        private const float  BurstMul         = 1.5f;                // the blast's explosion.chr, over the whirl's ExplosionScale
        private sealed class BlastState
        {
            public byte floor = 0xFF;
            public bool bladeLogged;                    // this floor's blade state has been written to the log once
            public bool crushing;                       // the guard break is currently driven on
            public bool tinted;                         // the blade is carrying this ability's charge tint
            public bool swingArmed;                     // the whirl's own hit radius is overridden (out of reach)
            public int  chargeAction;                   // the whirlwind that has already been billed (0 = none)
            public bool patchChecked;                   // the radius patch has been verified this floor
            public int  hp = -1;                        // Toan's HP as of the last tick, for the damage probe
        }

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Big Bang's WHIRLWIND is an explosion. The blade whitens as the meter fills, and the level-2 charge it
        /// becomes is the blast — the very blast the dropped judgement blade makes, at his feet: the falloff steps of
        /// the weapon's attack by distance (<see cref="Falloff"/>), no element so no resistance blunts it, guards crushed,
        /// everything thrown clear and turned to it; its model IS explosion.chr, and the spin's own hit is put out of
        /// reach so nothing is struck twice. The blade pays <see cref="BlastWhp"/> weapon HP per blast (whirlwind or
        /// drop) — the flash alone SunSword.FlashWhp. The lunge and the combo are ordinary swings. Dungeon only.
        ///
        /// While the blade is held, explosions cannot hurt Toan: the four shot configs that ARE the explosions are
        /// given a reaction the player's damage handler does not act on (see ExplosionCfgs).
        ///
        /// Big Bang's GUARD charge is Solar Flash, inherited from the Sun Sword it grows out of and struck at twice
        /// that sword's share (SunSword.BigBangFlash) — so the two charges are different abilities on one blade.
        ///
        /// ⚠ NOTHING here detects a hit. The damage is the engine's own charge-attack sphere, resized through the
        /// data words the ISO patch turned its baked radii into; every write this loop makes is idempotent, so no
        /// part of the ability depends on when a tick lands.
        /// </summary>
        public static void DetonateEffect()
        {
            var st = new BlastState();
            Memory.WriteInt(CodeCaves.NameHide, 0);                // the name-plate gate open, whatever a past run left
            while (Player.Weapon.GetCurrentWeaponId() == Items.bigbang && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try { Tick(st); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] Detonate tick error: " + ex.Message); }
            }
            Reset(st);
        }

        /// <summary>DIAGNOSTIC, once per floor: what the engine has for the sword in his hand — the weapon object, its
        /// model root, the blade visual and the vtable it draws through, the object's opacity and dim, and whether the
        /// blade copy's chara slot was left registered. The equipped blade once failed to draw on entering a floor
        /// (nothing of ours had run yet; it drew again on re-entry); this is what the next occurrence gets compared
        /// against. False until the weapon object exists.</summary>
        private static bool LogBlade()
        {
            uint obj = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
            if (!Memory.IsValidGuest(obj)) return false;
            long o = Memory.ToMmu(obj);
            uint root = Memory.ReadGuestPtr(o + 0xBC);
            if (!Memory.IsValidGuest(root)) return false;
            uint vis = 0, vt = 0;
            for (uint n = root; Memory.IsValidGuest(n) && vis == 0; n = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild))
                vis = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.GeomPtr);
            if (vis != 0) vt = Memory.ReadGuestPtr(Memory.ToMmu(vis) + CVisualMDT.VisVtable);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] blade: obj 0x{obj:X} root 0x{root:X} visual 0x{vis:X} vtable 0x{vt:X}"
                + $" opacity {Memory.ReadFloat(o + CCharacter.NpcOpacity):F0} dim {Memory.ReadFloat(o + CCharacter.DimFactor):F2}"
                + $" | copy slot reg {Memory.ReadInt(DungeonCharaDraw.CharaRegistry + 3 * 4)} active {Memory.ReadInt(DungeonCharaDraw.CharaArray + 3 * DungeonCharaDraw.CharaStride + DungeonCharaDraw.CharaActive)} prop {BladeProp.Active}");
            return true;
        }

        private static void Tick(BlastState st)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) Reset(st); st.floor = floor; st.bladeLogged = false; _yawConv = -1; }
            if (!st.bladeLogged) st.bladeLogged = LogBlade();
            ToanLockOn.HoldReach("[BigBang] ");
            FaceTick();

            ExpireShells();
            ReleaseRedirectWhenDue();

            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId) { ClearTint(st); RestoreSwing(st); return; }

            if (ExplosionSeeded) MaintainExplosionScale();
            ArmImmunity();
            AnswerAutoGuard();
            JudgementTick(BigBangOwner);
            if (!st.patchChecked)
            {
                st.patchChecked = true;
                uint insn = Memory.ReadUInt(LungeRadiusInsn);
                if (insn != LungeRadiusPatched)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[BigBang] ⚠ the charge-radius ISO patch is NOT applied (0x{insn:X8}"
                        + (insn == LungeRadiusVanillaInsn ? ", still vanilla" : "") +
                        ") — the charge attacks keep their stock 6 / 12 reach and the blast will do nothing");
                else
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[BigBang] charge radii are data: lunge {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusLunge):F0}"
                        + $", whirl {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl):F0}");
            }

            ProbeDamage(st);
            int   action = Memory.ReadInt(PlayerAction.ChargeActionState);
            float meter  = Memory.ReadFloat(PlayerAction.ChargeMeter);
            bool  whirl  = action == PlayerAction.ActionWhirlwind;

            // The blade whitens across the regular charge exactly as it does across a guard charge — the same tint,
            // driven by the meter instead of by held time. It stands aside while Solar Flash owns the blade.
            if (action == PlayerAction.ActionWindup && !SunSword.FlashArmed)
            {
                float k = (meter - ChargeMeterFloor) / (PlayerAction.ChargeMeterCap - ChargeMeterFloor);
                SolarBlade.Set(Math.Min(1f, Math.Max(0f, k)), SolarBlade.BigBangModel,
                               SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                st.tinted = true;
            }
            else if (st.tinted && !whirl) ClearTint(st);

            // Armed from the moment the meter reaches whirlwind range, so the spin's own hit is already out of reach
            // before its first damage frame. Nothing below depends on WHEN a tick lands: every write is the same value.
            if (!SunSword.FlashArmed && (whirl || (action == PlayerAction.ActionWindup && meter >= WhirlThreshold)))
                ArmSwing(st);
            else
                RestoreSwing(st);

            // Guards are crushed while the whirl spins: a blocked spin would eat the detonation.
            if (whirl != st.crushing) { GuardBreak.Drive(whirl); st.crushing = whirl; }

            // The blast, once per whirlwind, as the spin begins: the dropped blade's blast at his feet, and its bill.
            if (whirl && action != st.chargeAction)
            {
                st.chargeAction = action;
                float x = Memory.ReadFloat(Addresses.dunPositionX), h = Memory.ReadFloat(Addresses.dunPositionZ), y = Memory.ReadFloat(Addresses.dunPositionY);
                LastBlast = (x, h, y);
                PlantFalloff(x, h, y);
                TurnEnemiesToward(x, y);
                DrainWhp();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] whirlwind blast at ({x:F0},{h:F0},{y:F0}); the spin's own hit at {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl):F0}, flash {SunSword.FlashArmed}");
            }
            else if (!whirl) st.chargeAction = 0;
        }

        /// <summary>The whirl's own hit sphere put out of reach (the blast at his feet is the whirl's damage; a spin that
        /// also struck would hit an enemy twice). The lunge's word stays at its vanilla 6.</summary>
        private static void ArmSwing(BlastState st)
        {
            st.swingArmed = true;
            Weapons.SetChargeHitRadii(CodeCaves.LungeRadiusVanilla, WhirlNoHit);
        }

        /// <summary>The stock 6 / 12 back. The words are global and every weapon's charge reads them.</summary>
        private static void RestoreSwing(BlastState st)
        {
            if (!st.swingArmed) return;
            Weapons.SeedChargeHitRadii();
            st.swingArmed = false;
        }

        /// <summary>The effect this weapon wants in the main-character instance: explosion.chr, in place of Toan's
        /// whirlwind, whenever Big Bang is the blade in his hands. Handed to BorrowedShots.Start as a provider, so
        /// the cave re-enters it whenever the floor loader has refilled the instance — nothing per tick.
        ///
        /// Every phase radius is zeroed: the effect is the VISUAL only, and a phase with a radius plants a damage
        /// entry of its own every frame, which would double up on the spheres this ability plants itself.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.ToanId) return null;
            if (Player.Weapon.GetCurrentWeaponId() != Items.bigbang) return null;
            if (_explosion == null)
            {
                _explosion = BorrowedShots.CustomConfig(ExplosionTemplate, ExplosionName,
                                                        muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1);
                if (_explosion == null) return null;
                for (int phase = 0; phase < 4; phase++) BorrowedShots.SetPhaseRadius(_explosion, phase, 0f);
            }
            return _explosion;
        }

        /// <summary>True while the instance actually holds explosion.chr — which is when the stock whirl scaler must
        /// stand down, since the model it validates ("kiru" + "fkiri") is no longer the one loaded.</summary>
        internal static bool ExplosionSeeded => _explosion != null && BorrowedShots.Entered(_explosion);

        /// <summary>Hold explosion.chr at <see cref="ExplosionScale"/>. Same approach as the stock whirl scaler and
        /// for the same reason — a VERTEX_ANIME mesh is only transformed by its ROOT frame's local matrix, so the
        /// 3x3 at +0x1D0 is scaled and the translation row left anchored — but validated on this model's own root
        /// name. Every pool slot is kept scaled, not just the live one: a spin can activate any of them, and the
        /// idle copies sit at the origin where writing costs nothing.</summary>
        /// <summary>The blast's VISUAL at a point: explosion.chr — the whirlwind's own container, played where it
        /// stands at the whirl's <see cref="ExplosionScale"/> with no damage (the blast's damage is the hit entries).
        /// The ice gem burst stands in until the config is entered on this floor.</summary>
        private static void Burst(float x, float h, float y)
        {
            if (ExplosionSeeded && BorrowedShots.Burst(_explosion, x, h, y, 0, ExplosionScale))
            {
                _burstSlot = Memory.ReadInt(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffLastIdx);   // the sub-shot it took
                MaintainExplosionScale();                                                                  // its size before its first frame
                return;
            }
            GemBurst.Show(BurstElement, x, h, y, BurstScale, damage: 0, speedMult: BurstSpeed);
        }
        private static int _burstSlot = -1;
        /// <summary>The blast's sub-shot is still playing — its model is drawn at <see cref="BurstMul"/> meanwhile.</summary>
        private static bool BurstLive(int slot) =>
            slot == _burstSlot && slot >= 0
            && Memory.ReadUShort(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffActive + slot * 2) != 0;

        private static void MaintainExplosionScale()
        {
            if (!Player.CheckDunIsWalkingMode()) return;          // models are reallocated in menus and on transitions
            for (int slot = 0; slot < ShotEffectPool.EffectSlotCount; slot++)
            {
                uint ptr = Memory.ReadGuestPtr(ShotEffectPool.MainCharaEffectBase
                                               + ShotEffectPool.EffectSlotModelOff + (long)slot * ShotEffectPool.EffectSlotStride);
                if (!Memory.IsValidGuest(ptr)) continue;
                long root = Memory.ToMmu(ptr);
                if (Memory.ReadUInt(root + CFrameVu1.Name) != ExplosionRootWord) continue;
                if (Memory.ReadUInt(root + CFrameVu1.Name + 4) != ExplosionRootTail) continue;
                if (!_burstBindRead)
                {
                    for (int k = 0; k < 9; k++) _burstBind[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                    if (Math.Abs(_burstBind[0]) < 0.05f || Math.Abs(_burstBind[0]) > 4.0f) return;   // already scaled, or a bad read
                    _burstBindRead = true;
                }
                float scale = ExplosionScale * (BurstLive(slot) ? BurstMul : 1f);   // the whirl's size, or the blast's while it plays
                if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - _burstBind[0] * scale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _burstBind[k] * scale);
                Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
            }
        }

        /// <summary>Where config <paramref name="index"/> keeps its reaction. ⚠ ShotEffectPack.CfgTable is an array of
        /// POINTERS to the 0x70-byte records, not the records themselves (BorrowedShots.TableConfig reads it that
        /// way): the record lives at *(CfgTable + index × 4). Indexing the table by the record size instead walks
        /// straight off the end of a 34-entry pointer array and into the species table behind it.</summary>
        private static long ReactionAddr(int index)
        {
            uint cfg = Memory.ReadUInt(ShotEffectPack.CfgTable + (long)index * 4);
            return Memory.IsValidGuest(cfg) ? Memory.ToMmu(cfg) + ShotEffectPack.CfgReaction : 0;
        }

        /// <summary>DIAGNOSTIC. When Toan loses HP, report every sphere in the pool that can hurt him, with the
        /// fields that decide what it does. An explosion that still damages him while its config reads reaction 5
        /// is either not the entry we changed — the reaction is taken from the config at plant time, so a live entry
        /// shows what really arrived — or not an entry at all, in which case nothing here will be holding the damage
        /// and the HP is being taken by code that never touches the collision pool.</summary>
        private static void ProbeDamage(BlastState st)
        {
            int hp = Memory.ReadShort(DngStatusData.Base + 0x12 + Player.ToanId * 2);
            int was = st.hp; st.hp = hp;
            if (was < 0 || hp >= was) return;

            long pool = CollisionPool.Resolve();
            var seen = new List<string>();
            if (pool != 0)
                for (int i = 0; i < CollisionPool.Entries; i++)
                {
                    if (!CollisionPool.IsActive(pool, i)) continue;
                    long e = pool + (long)i * CollisionPool.Stride;
                    int mask = Memory.ReadInt(e + CollisionPool.Mask);
                    if ((mask & (int)CollisionPool.HurtsPlayerMask) == 0) continue;
                    seen.Add($"#{i} dmg {Memory.ReadInt(e + 0x34)} react {Memory.ReadInt(e + 0x4C)}"
                             + $" attr 0x{Memory.ReadInt(e + CollisionPool.Element):X} owner {Memory.ReadInt(e + CollisionPool.Owner)}"
                             + $" class {Memory.ReadFloat(e + CollisionPool.EntryClass):F0} r {Memory.ReadFloat(e + CollisionPool.Radius):F0}");
                }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] probe: HP {was} → {hp} (−{was - hp}); player-hurting spheres live: "
                + (seen.Count == 0 ? "NONE — the damage did not come through the collision pool" : string.Join(" | ", seen)));
        }

        /// <summary>The hovering blade, every tick Toan is out. Primed and locked: up over the target, fading in;
        /// the lock gone: fading out and down; a drop in flight: falling, and landing.</summary>
        private static void JudgementTick()
        {
            // "Locked on" is the lock button's own toggle (PlayerAction.LockOnHeld): the slot word alone is only the
            // nearest CANDIDATE, re-picked every frame the toggle is down, and the cursor word stays raised over it
            // after a release — either one alone hung the blade over whichever enemy was nearest.
            // Liveness by HP alone (HasHp): the hover must never depend on anything it changes itself.
            double dt     = TickMs / 1000.0;
            if (_pointHover) { PointTick(dt); return; }
            bool locked   = PlayerAction.LockHeld(out int lockSlot)
                            && lockSlot < EnemyAddresses.FloorSlots.Count && HasHp(lockSlot);
            bool primed   = Primed();
            // DIAGNOSTIC: the gates, once a second while primed — a hover that never appears is one of these reading
            // something other than what the notes say.
            if (primed && (GameClock.Now - _gateLog).TotalSeconds >= 1.0)
            {
                _gateLog = GameClock.Now;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] hover gates: primed {primed} ({SunSword.LivePhase}), LockOnActive {Memory.ReadInt(PlayerAction.LockOnActive)}, "
                    + $"LockOnTargetSlot {lockSlot}, live {(lockSlot >= 0 && lockSlot < EnemyAddresses.FloorSlots.Count ? Enemies.IsLive(lockSlot).ToString() : "n/a")}"
                    + $", hoverSlot {_hoverSlot}, blade {BladeProp.Active}"
                    + $" | held {Memory.ReadInt(PlayerAction.LockOnHeld)} cursor {Memory.ReadInt(PlayerAction.TargetCursorUp)}"
                    + $" 9d98 {Memory.ReadInt(0x202A3588)} 9d9c {Memory.ReadInt(0x202A358C)}");
            }

            if (Dropping)
            {
                // A paced fall whose swing is gone (interrupted, or the combo left) has nothing to land on: down it comes.
                int act = Memory.ReadInt(PlayerAction.ChargeActionState);
                bool swingLost = _paceTo > 0f && (act < PlayerAction.ActionComboFirst || act > PlayerAction.ActionComboLast);
                if (!BladeProp.Maintain() || !HasHp(_hoverSlot) || swingLost) { AbandonHover(); Dropping = false; SolarLighting.EndDim(); return; }
                // A swing right after the lock drops a blade still fading in: finish the fade on the way down.
                if (_hoverAlpha < 1f) { _hoverAlpha = (float)Math.Min(1.0, _hoverAlpha + dt / FadeSeconds); BladeProp.Alpha(_hoverAlpha); }
                DriveGlow(true);
                if (_fallDone) Land();                                       // FallLoop places it; the tick lands it
                return;
            }

            if (primed && locked && !_hoverOut)
            {
                if (_hoverSlot != lockSlot)
                {
                    if (!BladeProp.Active && !SpawnCopy()) return;
                    _hoverSlot = lockSlot;
                }
                // The name plate off, through the getter's gate (CodeCaves.NameHide): the flag itself is re-raised by
                // the engine every frame the target is on screen, so writing it only made the name flicker.
                Memory.WriteInt(CodeCaves.NameHide, 1);
                if (!BladeProp.Maintain()) { AbandonHover(); return; }
                // PINNED to the target's model root: the engine chains the copy's world through the enemy's every
                // frame, so it rides a moving enemy with no placement writes at all — no tick, no thread, no jitter.
                // Its height above the root is measured ONCE, as the hover begins (the enemy at rest): measuring the
                // spheres every tick had it bobbing with every animation. Heights are measured from that root's own
                // WORLD height — the slot's LocationZ is floor-relative, and taking it for the root hung the blade a
                // body too low and started the fall a body too high.
                // ⚠ Units of one SPECIES share one model tree: the root above is posed for whichever unit the engine
                // drew last, so a pin to it follows the wrong enemy whenever another of its kind is on the floor.
                // The pin is used only when this unit is the root's sole live user; otherwise the blade cave FOLLOWS
                // the unit's own position (CharObjects.PosAddr — per slot, height included: a flyer takes it up) every
                // frame, at the same height over it.
                uint enemyRoot = Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharModel);
                float unitH = UnitHeight(lockSlot);
                if (_heightSlot != lockSlot) { _heightSlot = lockSlot; _hoverHeight = HoverHeightFor(lockSlot, unitH); }
                if (RootShared(enemyRoot, lockSlot))
                {
                    if (BladeProp.PinnedTo != 0) BladeProp.Unpin(PlayerFacing());
                    _followSlot = lockSlot;
                    EngineFollow(lockSlot, _hoverHeight);                          // the cave places it from here on, every frame
                    BladeProp.Orient(PlayerFacing());                            // its flat the way the fallen blade will face
                }
                else
                {
                    _followSlot = -1;
                    if (BladeProp.PinnedTo != enemyRoot) BladeProp.Pin(enemyRoot, _hoverHeight);
                    // Its flat faces the way the fallen blade will (PlayerFacing): the parent's yaw is taken back out.
                    BladeProp.Face(PlayerFacing(), Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharRotY));
                }
                _hoverAlpha = (float)Math.Min(1.0, _hoverAlpha + dt / FadeSeconds);
                BladeProp.Alpha(_hoverAlpha);
                DriveGlow(true);                                                 // the glow crosses to the blade
                if (_hoverTraceTicks < 12)                                       // DIAGNOSTIC: the first ~third of a second of every hover
                {
                    _hoverTraceTicks++;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] hover: " + BladeProp.Where()
                        + $" | height {_hoverHeight:F1}");
                }
                return;
            }

            _hoverTraceTicks = 0;
            if (_hoverSlot >= 0)                                             // up, but no longer wanted: fade out and down
            {
                _hoverOut = true;
                _hoverAlpha = (float)Math.Max(0.0, _hoverAlpha - dt / FadeSeconds);
                if (BladeProp.Maintain()) BladeProp.Alpha(_hoverAlpha);
                DriveGlow(false);                                                // …and the glow shrinks with it
                if (_hoverAlpha <= 0f) AbandonHover();
            }
        }

        /// <summary>The glow is the judgement blade's alone (SolarProfile.BladeGlowOnly: Toan never carries it): it
        /// swells up on the blade over the blade's own fade-in and shrinks with its fade-out, so the two appear and
        /// go as one thing. It hangs at the blade's middle (the copy's root is the grip, and the blade points down
        /// its full length below it). The glow cave draws one sprite, so a fade cut short by a fresh lock starts
        /// again from nothing.</summary>
        private static void DriveGlow(bool onBlade)
        {
            GlowOwned = true;                                                   // Solar Flash's own Show stays out
            SolarGlow.Tick();
            if (!onBlade) { if (SolarGlow.IsUp) SolarGlow.Fade(FadeSeconds); return; }
            uint want = BladeProp.RootGuest;
            if (want == 0 || !(_pointHover || Primed())) return;
            if (SolarGlow.OnAnchor(want)) return;                               // up, where it should be
            if (SolarGlow.IsUp) SolarGlow.Hide();                               // fading off it, or on something else
            SolarGlow.Show(_owner.Glow, want, float.IsNaN(_owner.GlowLift) ? -OwnerLength() * OwnerScale() / 2f : _owner.GlowLift, FadeSeconds, palRow: _owner.GlowRow, scale: _owner.GlowScale);
        }

        /// <summary>Alive enough to hang a blade over: HP above zero.</summary>
        private static bool HasHp(int slot) =>
            slot >= 0 && slot < EnemyAddresses.FloorSlots.Count
            && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) > 0;

        /// <summary>How high above <paramref name="slot"/>'s root to hang the blade's GRIP: the species' authored height
        /// (scaled with the unit) plus <see cref="HoverMargin"/> for the tip, plus the blade's length at the copy's scale.</summary>
        private static float HoverHeightFor(int slot, float rootH)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            float height = EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.HeightFromRoot.HasValue ? def.HeightFromRoot.Value : HoverFallback;
            float scale = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharScale + 4);
            if (!(scale > 0.05f) || scale > 20f) scale = 1f;                                       // a grown miniboss
            float clear = height * scale + HoverMargin;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] hover over slot {slot} (species {eid}): height {height:F1} × scale {scale:F2} + {HoverMargin:F0} — tip {clear:F1} above the root");
            return clear + OwnerLength() * OwnerScale();
        }

        /// <summary>The sword's reach from grip to tip, at 1×: its dcol1 frame's Z from the offline table.</summary>
        private static float BladeLength()
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            return ToanWeapons.TryGetValue(wid, out WeaponData wd) && wd.Dcol1.HasValue
                 ? Math.Abs(wd.Dcol1.Value) : BladeLengthFallback;
        }

        /// <summary>The swing while primed: if the blade is hanging over a target, let it fall — the flash waits for
        /// the landing. False when there is nothing to drop, and Solar Flash fires as it always has. With
        /// <paramref name="paceFrom"/>/<paramref name="paceTo"/> the fall is PACED by Toan's swing instead of by
        /// gravity's clock: the frame cursor running from the one to the other carries the blade down the same
        /// curve, so the tip is in the enemy exactly at <paramref name="paceTo"/> (the Sword of Zeus's first swing).</summary>
        internal static bool BeginDrop(float paceFrom = 0f, float paceTo = 0f)
        {
            if (_hoverSlot < 0 || _hoverOut || !BladeProp.Active || !HasHp(_hoverSlot)) return false;
            _paceFrom = paceFrom; _paceTo = paceTo > paceFrom ? paceTo : 0f; _fallSeconds = 0;
            long a = EnemyAddresses.FloorSlots.SlotAddr(_hoverSlot, 0);
            _bladeX = Memory.ReadFloat(a + EnemySlotOffsets.LocationX);
            _bladeY = Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
            _dropH  = UnitHeight(_hoverSlot);                                // the target's root, wherever it is (a flyer's is up)
            _dropX = _bladeX; _dropY = _bladeY;                              // the blast goes off on the target itself
            // The fall starts from where the blade HANGS — its own world matrix when pinned, the followed height
            // otherwise — so there is no step at the start. It ENDS with the tip at the root (the grip a blade length
            // above it) — the blade in the enemy, not a blade length under the floor — or, for an owner that wants
            // it, the hilt.
            float hang = _followSlot >= 0 ? _dropH + _hoverHeight : BladeProp.WorldHeight();
            _fallStop   = _owner.ToTheHilt ? 0f : OwnerLength() * OwnerScale();   // the grip's height above the root at the end: the tip in the enemy, or the hilt
            _fallHeight = float.IsNaN(hang) ? _hoverHeight : Math.Max(_fallStop + 1f, hang - _dropH);
            _followSlot = -1;
            BladeProp.Unpin(PlayerFacing());                                 // off the enemy and into the world, where it is, to fall
            SolarLighting.BeginDim();                                        // the lights go down with it
            lock (_bladeLock)
            {
                _dropStart = GameClock.Now; Dropping = true; _landed = false; _fallDone = false; _landedAt = default;
                StartEngineFall();
            }
            if (_owner.Redirect) BeginRedirect(_bladeX, _dropH + _fallHeight, _bladeY);
            EnsureBladeThread();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] judgement blade falls on slot {_hoverSlot} for weapon {_owner.WeaponId}");
            return true;
        }

        /// <summary>The FALL at frame rate — the slingshot prop's orbit thread, applied here: the copy's height along a
        /// gravity curve until it reaches the ground, then <see cref="_fallDone"/> for the tick to land on. (The hover
        /// needs no thread: it is pinned to the enemy and the engine carries it.) A pause freezes a fall in the air by
        /// shifting the start time, so it resumes where it was.</summary>
        private static void EnsureBladeThread()
        {
            if (_fallThread == null || !_fallThread.IsAlive)
            { _fallThread = new Thread(BladeLoop) { IsBackground = true, Name = "BigBangBlade" }; _fallThread.Start(); }
        }
        private static void BladeLoop()
        {
            while (true)
            {
                try
                {
                    if (_pointHover && _followSlot < 0 && !_pointRides && !Dropping && BladeProp.Active)   // the point hover over a SPOT: placed from here (a followed unit, or Toan, is the cave's)
                    {
                        BladeProp.Place(_pointX, _spotH + _hoverHeight, _pointY, PlayerFacing());
                        Thread.Sleep(FallTickMs); continue;
                    }
                    if (!Dropping || _fallDone) { Thread.Sleep(20); continue; }
                    // THE ENGINE STEPS THE FALL (the blade-fall cave, once a frame): this thread only watches where it
                    // has got to, for the dim and the landing. No placement writes race the frame any more.
                    float  h = Memory.ReadFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY);
                    int    flag = Memory.ReadInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag);
                    if (flag != CodeCaves.BladeFalling && flag != CodeCaves.BladeLanded)
                    {   // something else took the words mid-fall: the fall re-armed from where it is, and said so
                        lock (_bladeLock) { if (Dropping) Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFalling); }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] engine fall found flag {flag} mid-fall — re-armed");
                    }
                    // DIAGNOSTIC: where the engine has the blade at a few points of the fall, and when it lands
                    double since = (GameClock.Now - _dropStart).TotalSeconds;
                    if (_fallLogged < 4 && since >= _fallLogged * 0.1)
                    { _fallLogged++; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] engine fall at {since * 1000:F0} ms: y {h:F1} vy {Memory.ReadFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy):F2} flag {flag}"); }
                    if (flag == 2 && _fallLogged < 9) { _fallLogged = 9; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] engine fall landed at {since * 1000:F0} ms"); }
                    double span = Math.Max(0.01, _fallStart - (_dropH + _fallStop));
                    double u = Math.Sqrt(Math.Max(0.0, Math.Min(1.0, (_fallStart - h) / span)));   // the fall's fraction in FRAMES (y falls with the square of it)
                    if (_redirecting) Memory.WriteVec3(CodeCaves.JudgementPos, _bladeX, h, _bladeY);   // what every enemy is watching
                    // The dim peaks as it lands: along the whole fall, or only its last RampFrames (a strike's brief plunge).
                    double rampSpan = _owner.RampWholeFall ? 1.0 : Math.Min(1.0, SolarLighting.RampFrames / Math.Max(1.0, _fallFrames));
                    double w = Math.Max(0.0, Math.Min(1.0, (u - (1.0 - rampSpan)) / Math.Max(1e-3, rampSpan)));
                    float  ramp = (float)((Math.Exp(SolarLighting.RampSharpness * w) - 1.0) / (Math.Exp(SolarLighting.RampSharpness) - 1.0));
                    float  from = _owner.Profile.PrimeDim;                        // on from the primed level, not from the floor's own light
                    SolarLighting.Dim(from + (1f - from) * ramp);
                    if (flag == 2) _fallDone = true;
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] fall tick failed: " + e.Message); }
                Thread.Sleep(FallTickMs);
            }
        }

        private static double _fallFrames;                     // how many frames the current fall takes (what the cave's gravity was set for)
        /// <summary>Hand the fall to the engine: from the grip's height now down to the stop in exactly N frames — the
        /// swing's remaining cursor at its clip's step when paced, the fixed time otherwise, gravity's own time else —
        /// as g = 2·span/N² into the blade-fall words, the copy placed once at its x/z, and the flag raised. The
        /// blade-fall cave takes it from here, one step per frame.</summary>
        private static void StartEngineFall()
        {
            float stop = _dropH + _fallStop;
            _fallStart = _dropH + _fallHeight;
            double span = Math.Max(0.01, _fallStart - stop);
            // Paced: the frames left to the hit mark from where the swing's cursor is — if it is inside the clip already
            // (a tick can land a frame or two in); a cursor still outside it is the previous clip's, and the clip is
            // taken as just begun.
            float cursor = Memory.ReadFloat(PlayerAction.AnimFrameCursor);
            if (cursor < _paceFrom || cursor > _paceTo) cursor = _paceFrom;
            _fallFrames = _paceTo > 0f
                ? Math.Max(1.0, (_paceTo - cursor) / SunSword.SwingCursorPerFrame)
                : _fallSeconds > 0 ? Math.Max(1.0, _fallSeconds * 60.0)
                : Math.Max(1.0, Math.Sqrt(2.0 * span / Gravity) * 60.0);
            float g = (float)(2.0 * span / (_fallFrames * _fallFrames));
            BladeProp.Place(_bladeX, _fallStart, _bladeY, PlayerFacing());       // x/z and facing once; the cave carries y
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, _fallStart);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallVy, 0f);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, g);
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallStop, stop);
            Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, 1);
            _fallLogged = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] engine fall: {_fallStart:F1} → {stop:F1} over {_fallFrames:F0} frames (g {g:F4}/frame²; paced {_paceTo > 0f}, timed {_fallSeconds:F2}s)");
        }
        private static int _fallLogged;
        private static void StopEngineFall() => Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFallOff);
        /// <summary>The hover riding a unit through the cave: its position pointer and the height OVER it into the blade
        /// words, the flag at FOLLOWING (written only on a change). The cave adds the unit's own height each frame.</summary>
        private static int _engineFollowSlot = -1; private static float _engineFollowY = float.NaN;
        // ⚠ The hover's tick and the drop (Solar Flash's tick) are different threads: the hover branch once re-wrote the
        // follow mode over a fall that had just been set up, and the blade hung there for good (the fall never landed,
        // Solar Flash waited on it, the lock no longer faded it). The blade words are written under one lock, and a
        // follow is never written once a drop is on.
        private static readonly object _bladeLock = new object();
        private static void EngineFollow(int slot, float overUnit)
        {
            lock (_bladeLock)
            {
                if (Dropping) return;
                if (_engineFollowSlot == slot && Math.Abs(overUnit - _engineFollowY) < 0.05f
                    && Memory.ReadInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag) == CodeCaves.BladeFollowing) return;
                Memory.WriteUInt(CodeCaves.BladeFall + CodeCaves.BladeFallUnit, (uint)(EnemyAddresses.CharObjects.PosAddr(slot) - 0x20000000L));
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, overUnit);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffX, 0f);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffZ, 0f);
                Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFollowing);
                _engineFollowSlot = slot; _engineFollowY = overUnit;
            }
        }
        /// <summary>The hover riding a POSITION WORD (Toan's) through the cave, at an x/z offset from it: the offset and
        /// the height over it into the blade words, the flag at FOLLOWING (written only on a change).</summary>
        private static float _engineOffX = float.NaN, _engineOffZ = float.NaN;
        private static void EngineFollowPoint(uint posGuest, float offX, float offZ, float overUnit)
        {
            lock (_bladeLock)
            {
                if (Dropping) return;
                bool same = _engineFollowSlot == -2 && Math.Abs(overUnit - _engineFollowY) < 0.05f && Math.Abs(offX - _engineOffX) < 0.05f && Math.Abs(offZ - _engineOffZ) < 0.05f;
                if (same && Memory.ReadInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag) == CodeCaves.BladeFollowing) return;
                Memory.WriteUInt(CodeCaves.BladeFall + CodeCaves.BladeFallUnit, posGuest);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallY, overUnit);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffX, offX);
                Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallOffZ, offZ);
                Memory.WriteInt(CodeCaves.BladeFall + CodeCaves.BladeFallFlag, CodeCaves.BladeFollowing);
                _engineFollowSlot = -2; _engineFollowY = overUnit; _engineOffX = offX; _engineOffZ = offZ;
            }
        }

        /// <summary>Solar Flash asks once per tick while it waits: has the blade landed — and has the burst had its
        /// <see cref="FlashDelay"/> head start? Consumed on read.</summary>
        internal static bool TakeDropLanded()
        {
            if (!_landed || (GameClock.Now - _landedAt).TotalSeconds < FlashDelay) return false;
            _landed = false; _landedAt = default; return true;
        }

        /// <summary>The blade has hit the ground: the owner's landing where it fell, and the copy gone — the flash is
        /// Solar Flash's to fire now.</summary>
        private static void Land()
        {
            if (_pointHover)
            {   // the owner fires on its own cue; the blade is simply gone, and the time noted for it
                PointLandedAt = GameClock.Now;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] point hover blade in the ground at ({_bladeX:F0},{_dropH:F0},{_bladeY:F0})");
                PointEnd();
                return;
            }
            LastBlast = (_dropX, _dropH, _dropY);
            if (_redirecting) Memory.WriteVec3(CodeCaves.JudgementPos, _dropX, _dropH, _dropY);   // …and it stays on the blast
            _owner.Land(_hoverSlot, _dropX, _dropH, _dropY);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] judgement blade lands on ({_bladeX:F0},{_dropH:F0},{_bladeY:F0}) for weapon {_owner.WeaponId}");
            AbandonHover();
            _landedAt = GameClock.Now; _landed = true;
            Dropping = false;
        }
        /// <summary>Big Bang's landing: the blast, every enemy turned to look, the weapon-HP bill. The white-out and the
        /// burst on the SAME frame, from here: Solar Flash's own tick runs the rest of the flash (the light hit, the
        /// blinding, the blade and glow) once TakeDropLanded hands it the landing — and that is after the hit entries
        /// and the turns below, well past a frame — so the lighting write goes out now, and Solar Flash is told it is
        /// lit (a second Flash there restores the floor's light and whites it again).</summary>
        private static void LandBigBang(int slot, float x, float h, float y)
        {
            SunSword.BigBangFlash.ArmLighting();
            SolarLighting.Flash();
            Burst(x, h, y);
            PlantFalloff(x, h, y);
            TurnEnemiesToward(x, y);
            DrainWhp();
        }

        /// <summary>Everything the hover put up, back down: the copy, the target's name bar, the glow's home.</summary>
        private static void AbandonHover()
        {
            StopEngineFall(); _engineFollowSlot = -1; _engineFollowY = float.NaN; _engineOffX = _engineOffZ = float.NaN;
            Memory.WriteInt(CodeCaves.NameHide, 0);                                 // the name plate back
            // A glow left hanging on the copy's root after the copy is gone is a sprite drawn every frame at a node in
            // mod memory that nothing maintains; the one run that left it there ended in the game resetting six
            // seconds later. The fade-out waits for the glow to shrink off first; every other way here cuts it.
            if (SolarGlow.AnchoredTo(BladeProp.RootGuest)) SolarGlow.Hide();
            _followSlot = -1;
            BladeProp.Despawn();
            _hoverSlot = -1; _heightSlot = -1; _hoverAlpha = 0f; _hoverOut = false; _pointHover = false; _pointDriven = false; _pointFading = false; _pointRides = false;
            // GlowOwned stays: DriveGlow brings the glow back up on Toan while the charge still stands, then lets go.
        }

        // ── the point hover ─────────────────────────────────────────────────────────────
        /// <summary>Hang the blade for <paramref name="owner"/> over a unit (<paramref name="slot"/> ≥ 0: followed, at
        /// the lock-on hover's own height over it — its top at rest plus the margin — and its name plate hidden) or at a
        /// fixed <paramref name="height"/> over the spot (x, h, y); called again to move the spot. Fades in like the
        /// lock-on hover, with the owner's glow. Nothing while a lock-on hover or a drop is up.</summary>
        internal static void PointHover(JudgementOwner owner, int slot, float x, float h, float y, float height, bool ridesPlayer = false)
        {
            if (Dropping) return;
            if (!_pointHover)
            {
                if (_hoverSlot >= 0 || BladeProp.Active) return;                  // the lock-on hover has the copy
                if (!SpawnCopy()) return;
                _owner = owner; _pointHover = true; _pointDriven = false; _pointFading = false; _hoverAlpha = 0f; _hoverOut = false; PointLandedAt = default;
                _hoverHeight = height; _spotH = h; _heightSlot = -1;
                EnsureBladeThread();
            }
            _followSlot = slot >= 0 && HasHp(slot) ? slot : -1;
            _pointRides = _followSlot < 0 && ridesPlayer;
            if (_followSlot < 0 && ridesPlayer)
            {   // a spot AHEAD OF TOAN: the cave places it from his own position every frame (smooth as he walks), the
                // mod only refreshing the offset ahead of him as he turns
                _pointX = x; _pointY = y; _spotH = h; _hoverHeight = height; _heightSlot = -1; Memory.WriteInt(CodeCaves.NameHide, 0);
                EngineFollowPoint((uint)(Addresses.dunPositionX - 0x20000000L), x - Memory.ReadFloat(Addresses.dunPositionX), y - Memory.ReadFloat(Addresses.dunPositionY), height);
                BladeProp.Orient(PlayerFacing());
            }
            else if (_followSlot < 0) { _pointX = x; _pointY = y; _spotH = h; _hoverHeight = height; _heightSlot = -1; Memory.WriteInt(CodeCaves.NameHide, 0); StopEngineFall(); }
            else
            {
                if (_heightSlot != slot) { _heightSlot = slot; _hoverHeight = HoverHeightFor(slot, UnitHeight(slot)); }   // measured once, at rest
                Memory.WriteInt(CodeCaves.NameHide, 1);                           // the target's name plate off, as under the lock-on hover
                EngineFollow(slot, _hoverHeight);                                 // the cave places it over the unit every frame
                BladeProp.Orient(PlayerFacing());
            }
        }
        /// <summary>The point hover's fade-in set by hand: the blade at <paramref name="alpha"/> (0..1) and its glow the
        /// same size — a charge's progress, rather than the fade's own clock.</summary>
        internal static void PointAlpha(float alpha)
        {
            if (!_pointHover || _pointFading || Dropping) return;
            _pointDriven = true;
            _hoverAlpha = Math.Max(0f, Math.Min(1f, alpha));
        }
        /// <summary>The point hover's blade fading out over the fade, its glow shrinking with it, then gone — a charge
        /// let go or broken before it was spent.</summary>
        internal static void PointFade()
        {
            if (!_pointHover || _pointFading) return;
            if (Dropping) { PointEnd(); return; }
            if (_followSlot >= 0)                                                 // it fades where it hangs, no longer following
            { long p = EnemyAddresses.CharObjects.PosAddr(_followSlot); _pointX = Memory.ReadFloat(p); _pointY = Memory.ReadFloat(p + 8); }
            PointFreeze();
            _pointFading = true; _pointDriven = false; _followSlot = -1;
        }
        /// <summary>A blade riding Toan stops where it is: the spot taken from where the cave has it, the cave let go
        /// (a still spot is placed from here), so it no longer moves with him (the lunge, or a fade).
        /// Returns the spot under it (x, ground h, y) — where a bolt aimed at it lands.</summary>
        internal static (float x, float h, float y) PointFreeze()
        {
            if (_pointHover && _pointRides && BladeProp.Active)
            {
                long s = DungeonCharaDraw.CharaArray + (long)BladeProp.Slot * DungeonCharaDraw.CharaStride + CCharacter.CharPos;
                _pointX = Memory.ReadFloat(s); _pointY = Memory.ReadFloat(s + 8); _spotH = Memory.ReadFloat(s + 4) - _hoverHeight;
                _pointRides = false;
                lock (_bladeLock) { StopEngineFall(); _engineFollowSlot = -1; _engineOffX = _engineOffZ = float.NaN; }
            }
            return (_pointX, _spotH, _pointY);
        }
        /// <summary>Let the point hover's blade go: down from where it hangs to the owner's stop over
        /// <paramref name="seconds"/>, on the fall's curve; the time it reaches the ground is <see cref="PointLandedAt"/>.</summary>
        internal static void PointDrop(double seconds)
        {
            if (!_pointHover || Dropping || !BladeProp.Active) return;
            float dropH = _spotH;
            if (_followSlot >= 0)
            {
                long p = EnemyAddresses.CharObjects.PosAddr(_followSlot);
                _pointX = Memory.ReadFloat(p); _pointY = Memory.ReadFloat(p + 8); dropH = Memory.ReadFloat(p + 4);
            }
            _bladeX = _dropX = _pointX; _bladeY = _dropY = _pointY; _dropH = dropH;
            _fallStop   = _owner.ToTheHilt ? 0f : OwnerLength() * OwnerScale();
            _fallHeight = Math.Max(_fallStop + 1f, _hoverHeight);
            _followSlot = -1; _paceTo = 0f; _fallSeconds = Math.Max(0.05, seconds);
            SolarLighting.BeginDim();                                             // the lights go down with it (a dim already up keeps its capture)
            lock (_bladeLock)
            {
                _dropStart = GameClock.Now; Dropping = true; _landed = false; _fallDone = false; _landedAt = default;
                StartEngineFall();
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] point hover blade falls at ({_bladeX:F0},{_bladeY:F0}) over {_fallSeconds:F2}s for weapon {_owner.WeaponId}");
        }
        /// <summary>The point hover's blade gone at once, wherever it is.</summary>
        internal static void PointEnd()
        {
            if (!_pointHover) return;
            Dropping = false; _fallDone = false;
            AbandonHover();
        }
        private static void PointTick(double dt)
        {
            if (!BladeProp.Maintain()) { PointEnd(); return; }
            if (_pointFading)
            {
                _hoverAlpha = (float)Math.Max(0.0, _hoverAlpha - dt / FadeSeconds);
                BladeProp.Alpha(_hoverAlpha);
                DriveGlow(false);
                if (_hoverAlpha <= 0f) PointEnd();
                return;
            }
            if (_pointDriven) { BladeProp.Alpha(_hoverAlpha); DriveGlow(true); SolarGlow.Drive(_hoverAlpha); if (Dropping && _fallDone) Land(); return; }
            if (_hoverAlpha < 1f) { _hoverAlpha = (float)Math.Min(1.0, _hoverAlpha + dt / FadeSeconds); BladeProp.Alpha(_hoverAlpha); }
            if (_followSlot >= 0 && !HasHp(_followSlot)) { PointEnd(); return; }
            DriveGlow(true);
            if (Dropping && _fallDone) Land();
        }

        /// <summary>The blast that falls with the blade: one hit entry per live enemy in reach, its damage the
        /// <see cref="Falloff"/> step for that enemy's distance, its kick from the blast. The Sword of Zeus's bolt
        /// plants the same blast at its strike point; <paramref name="noKickSlot"/> is the enemy it struck directly,
        /// which takes the hit where it stands (a zero-strength kick: the reaction without the shove), and its steps
        /// are scaled by <paramref name="damageScale"/> (the bolt's blast is half the blade's).</summary>
        internal static void PlantFalloff(float x, float h, float y, int noKickSlot = -1, float damageScale = 1f, float kickScale = 1f)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int attack = Player.Weapon.GetCurrentWeaponAttack();
            // ONE entry per enemy, centred on its own body and sized to it, so that entry can only be consumed by that
            // enemy and each takes exactly one hit; its damage is the falloff step its distance from the blast falls in.
            // (Shells around the blast, one per enemy per step, had every enemy in reach of several entries at once
            // and the hit invincibility does not outlast a landing's own work — enemies took two and three.)
            int planted = 0, inRange = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                float dx = Memory.ReadFloat(a + EnemySlotOffsets.LocationX) - x;
                float dy = Memory.ReadFloat(a + EnemySlotOffsets.LocationY) - y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float times = 0f;
                foreach (var (radius, t) in Falloff) if (dist <= radius) times = t;   // steps ordered outermost first: the last match is the innermost
                if (times <= 0f) continue;
                inRange++;
                if (CollisionPool.FreeCount(pool) <= ShellPoolReserve) break;
                int slot = CollisionPool.TakeFreeSlot(pool);
                if (slot < 0) break;
                BodyCentre(s, a, out float cx, out float ch, out float cy, out float cr);
                byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, cr, Math.Max(1, (int)Math.Round(attack * times * damageScale)), 0);
                void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
                F(0x80, x); F(0x84, h); F(0x88, y);                        // the kick still comes from the blast
                F(0x90, s == noKickSlot ? 0f : KickStrength * kickScale); F(0x94, KickDecay);
                BitConverter.GetBytes(2).CopyTo(e, 0x98);                 // kick type 2: thrown away from the blast
                CollisionPool.Plant(pool, slot, e);
                _shells.Add((slot, ShellLifeTicks));
                planted++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] falloff blast: {planted} sphere(s) on {inRange} enem" + (inRange == 1 ? "y" : "ies") + $" within {Falloff[0].radius:F0}");
        }

        /// <summary>The enemy's largest live body sphere (centre and radius), the surest thing a hit sphere of the same
        /// size at the same place will touch; its root and a plain radius where none is active.</summary>
        internal static void BodyCentre(int slot, long a, out float cx, out float ch, out float cy, out float cr)
        {
            long b = BodyCollision.SlotBase(slot);
            cx = Memory.ReadFloat(a + EnemySlotOffsets.LocationX); cy = Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
            ch = UnitHeight(slot) + BodyRadiusFallback;
            cr = BodyRadiusFallback;
            float best = 0f;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                float r = Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                if (r <= best) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                best = r; cr = r;
                cx = Memory.ReadFloat(c); ch = Memory.ReadFloat(c + 4); cy = Memory.ReadFloat(c + 8);
            }
        }
        private const float BodyRadiusFallback = 10f;

        /// <summary>The unit's own world height: its CCharacter position (per slot — the model root's world matrix is
        /// the SPECIES' tree, posed for whichever unit drew last, and the slot's LocationZ is floor-relative).</summary>
        private static float UnitHeight(int slot) => Memory.ReadFloat(EnemyAddresses.CharObjects.PosAddr(slot) + 4);

        /// <summary>Whether another live unit draws through the same model root as <paramref name="slot"/> — the
        /// species' tree is one object, so a pin to it can only be trusted when this unit is its sole user.</summary>
        private static bool RootShared(uint root, int slot)
        {
            if (!Memory.IsValidGuest(root)) return true;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (s == slot || !Enemies.IsLive(s)) continue;
                if (Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharModel) == root) return true;
            }
            return false;
        }
        private const int ShellLifeTicks = 3;   // ticks an unconsumed entry (a miss) stays before it is withdrawn
        private const int ShellPoolReserve = 16; // free entries the blast always leaves the engine
        private static readonly List<(int slot, int ticks)> _shells = new List<(int, int)>();

        /// <summary>Withdraw the hit entries the engine has not consumed (an enemy that moved off its own).</summary>
        internal static void ExpireShells()
        {
            if (_shells.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = _shells[i];
                if (--ticks > 0) { _shells[i] = (slot, ticks); continue; }
                if (pool != 0) CollisionPool.Deactivate(pool, slot);
                _shells.RemoveAt(i);
            }
        }

        /// <summary>Where the last judgement blade went off — the flash's own point when it is fired for a landing.</summary>
        internal static (float x, float h, float y) LastBlast { get; set; }   // the Sword of Zeus sets it to its strike

        // ── every enemy's eyes on the blade ──────────────────────────────────────────────────
        // Mirage's decoy redirect, borrowed: each enemy's `_GET_POSITION(-2)` ("where is the player") reads through
        // the per-slot pointer table (CodeCaves.PtrTable), so while the blade falls every live slot is pointed at
        // CodeCaves.JudgementPos — the blade, then the blast — and their own AI turns them to it before the flash
        // lands and holds them; the pointers go back to the live player just before the blinding ends, so they
        // come out of it looking at the danger and then find Toan again. Mirage's table writer only runs for
        // Ungaga and Angel Gear's for Xiao, so nothing else writes the table while Toan holds this.
        private const int    RedirectSlots   = 20;    // the entries Mirage and Angel Gear manage too (FloorSlots is 16)
        private const double RedirectRelease = 0.4;   // seconds before the blinding ends that they get the player back
        private const double RedirectOrphan  = 2.0;   // a drop that never flashed: let go this long after it began
        private static bool _redirecting, _redirectBlindSeen; private static DateTime _redirectSince;
        internal static void BeginRedirect(float x, float h, float y)
        {
            if (!Mirage.Armed) return;                                           // the caves are armed at the main menu; without them, nothing to point
            Memory.WriteVec3(CodeCaves.JudgementPos, x, h, y);
            Memory.WriteFloat(CodeCaves.JudgementPos + 12, 1f);
            var ptrs = new byte[RedirectSlots * CodeCaves.PtrStride];
            for (int s = 0; s < RedirectSlots; s++)
                BitConverter.GetBytes(s < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(s) ? CodeCaves.JudgementPosGuest : StbExternCmd.PlayerPosGuest)
                            .CopyTo(ptrs, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs);
            _redirecting = true; _redirectBlindSeen = false; _redirectSince = GameClock.Now;
        }
        internal static void ReleaseRedirectWhenDue()
        {
            if (!_redirecting) return;
            double left = SunSword.BlindSecondsLeft;
            if (left > 0) _redirectBlindSeen = true;
            bool due = _redirectBlindSeen ? left <= RedirectRelease
                     : !Dropping && !LandingPending && (GameClock.Now - _redirectSince).TotalSeconds > RedirectOrphan;
            if (due) ReleaseRedirect();
        }
        internal static void ReleaseRedirect()
        {
            if (!_redirecting) return;
            _redirecting = false;
            var ptrs = new byte[RedirectSlots * CodeCaves.PtrStride];
            for (int s = 0; s < RedirectSlots; s++) BitConverter.GetBytes(StbExternCmd.PlayerPosGuest).CopyTo(ptrs, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs);
        }

        /// <summary>Every living enemy turned to face the point, and HELD there for <see cref="FaceHoldTicks"/>: the
        /// facing vector is the unit its AI steers by and the CCharacter yaw is what it is drawn with, and a single
        /// write of either is undone by the next Step — the AI steering back toward Toan, the flash's own hit
        /// reaction — before the blinding's script hold takes over.</summary>
        internal static void TurnEnemiesToward(float x, float y)
        {
            _faceX = x; _faceY = y; _faceHold = FaceHoldTicks;
            FaceAll();
        }
        private const int FaceHoldTicks = 12;   // ≈0.36 s: through the flash's stagger, into the script hold
        private static int _faceHold; private static float _faceX, _faceY;
        /// <summary>The facing hold's tick — whichever blade's loop is running calls it.</summary>
        internal static void FaceTick() { if (_faceHold > 0) { _faceHold--; FaceAll(); } }
        private static void FaceAll()
        {
            int conv = YawConvention();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                // An enemy the blast cannot move (knockback 0: bosses, rooted plants) is not turned either — with no
                // shove to settle it, the turn fought its own AI and it never held a direction.
                if (Memory.ReadFloat(a + EnemySlotOffsets.KnockbackMult) <= 0f) continue;
                float dx = _faceX - Memory.ReadFloat(a + EnemySlotOffsets.LocationX);
                float dy = _faceY - Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-3f) continue;
                float fx = dx / len, fz = dy / len;
                Memory.WriteFloat(a + EnemySlotOffsets.FacingX, fx);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingY, 0f);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingZ, fz);
                if (conv >= 0) Memory.WriteFloat(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharRotY, YawOf(conv, fx, fz));
            }
        }

        /// <summary>The engine's yaw from a facing vector — which of the four sign/axis conventions the game uses is
        /// READ OFF LIVE ENEMIES the first time it is needed on a floor (each one's own facing against its own yaw),
        /// rather than assumed. −1 while no enemy has told us yet (the yaw is then left to the engine).</summary>
        private static float YawOf(int conv, float fx, float fz) => conv switch
        {
            0 => (float)Math.Atan2(fx, fz), 1 => (float)Math.Atan2(-fx, fz),
            2 => (float)Math.Atan2(fz, fx), _ => (float)Math.Atan2(-fz, fx),
        };
        private static int _yawConv = -1;
        private static int YawConvention()
        {
            if (_yawConv >= 0) return _yawConv;
            var err = new double[4]; int n = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                float fx = Memory.ReadFloat(a + EnemySlotOffsets.FacingX), fz = Memory.ReadFloat(a + EnemySlotOffsets.FacingZ);
                if (fx * fx + fz * fz < 0.5f) continue;
                float yaw = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharRotY);
                if (float.IsNaN(yaw) || Math.Abs(yaw) > 100f) continue;
                for (int c = 0; c < 4; c++)
                {
                    double d = Math.Abs(YawOf(c, fx, fz) - yaw) % (2 * Math.PI);
                    err[c] += Math.Min(d, 2 * Math.PI - d);
                }
                n++;
            }
            if (n == 0) return -1;
            int best = 0; for (int c = 1; c < 4; c++) if (err[c] < err[best]) best = c;
            if (err[best] / n > 0.35) return -1;                                   // none of them fits: leave the yaw alone
            _yawConv = best;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] enemy yaw convention {best} (mean error {err[best] / n:F2} rad over {n})");
            return best;
        }

        /// <summary>Answer a hit the cave swallowed: rumble and the guard clang. The cave only ticks a counter —
        /// everything the player actually feels is here, where it can be tuned without touching MIPS.
        /// The count is compared, never zeroed, so two ticks between polls still read as one answer and nothing is
        /// lost if the mod starts mid-floor.</summary>
        private static void AnswerAutoGuard()
        {
            int n = Memory.ReadInt(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardCount);
            int was = _guardSignal; _guardSignal = n;
            if (was < 0 || n == was) return;

            GamePad.Rumble(GuardRumble, GuardRumbleFrames);
            SeSeq.Play(GuardSe, 90);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] explosion guarded at ({Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardX):F0},"
                + $"{Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardH):F0},"
                + $"{Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardY):F0})");
        }

        /// <summary>The effect file a config names (its first field) — the check that we are looking at the record
        /// we think we are.</summary>
        private static string ConfigName(int index)
        {
            uint cfg = Memory.ReadUInt(ShotEffectPack.CfgTable + (long)index * 4);
            if (!Memory.IsValidGuest(cfg)) return "?";
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(cfg) + ShotEffectPack.CfgName, ShotEffectPack.CfgNameLen);
            int n = Array.IndexOf(b, (byte)0);
            return System.Text.Encoding.ASCII.GetString(b, 0, n < 0 ? b.Length : n);
        }

        /// <summary>The explosions inert (on) or dangerous again (off), for a wielder that inherits it (Super Steve with a Big
        /// Bang sphere).</summary>
        internal static void DriveImmunity(bool on) { if (on) ArmImmunity(); else RestoreImmunity(); }

        /// <summary>Make the explosions inert while the blade is in hand (see ExplosionCfgs).</summary>
        private static void ArmImmunity()
        {
            if (_immune) return;
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                if (a == 0) return;                                  // the table is not up yet — try again next tick
                _cfgReaction[i] = Memory.ReadInt(a);
                Memory.WriteInt(a, ReactionInert);
            }
            Memory.WriteInt(CodeCaves.BombReaction, ReactionInert);   // chest traps, thrown bombs, Halloween's pumpkin
            _immune = true;
            // Reported by NAME and read back, because a reaction that did not take is invisible in play: the hit
            // simply happens, and the config table is reached through a pointer array that is easy to index wrong.
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] cfg {ExplosionCfgs[i]} \"{ConfigName(ExplosionCfgs[i])}\": reaction {_cfgReaction[i]} → {Memory.ReadInt(a)}"
                    + (Memory.ReadInt(a) == ReactionInert ? "" : "  ⚠ DID NOT TAKE"));
            }
        }

        /// <summary>…and dangerous again when it is not. ⚠ Never leave this on: the configs are shared ELF data.</summary>
        private static void RestoreImmunity()
        {
            if (!_immune) return;
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                if (a != 0) Memory.WriteInt(a, _cfgReaction[i]);
            }
            Weapons.SeedBombReaction();
            _immune = false;
        }

        /// <summary>The charge tint off the blade — but only ours. Solar Flash drives the same mesh from its own
        /// thread, so a tint it is holding is left alone.</summary>
        private static void ClearTint(BlastState st)
        {
            if (!st.tinted) return;
            st.tinted = false;
            if (!SunSword.FlashArmed) SolarBlade.Clear();
        }
        private static float PlayerFacing()
        {
            uint root = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (Memory.IsValidGuest(root))
            {
                long m = Memory.ToMmu(root);
                if (Memory.ReadInt(m + CFrameVu1.EulerValid) == 0) return Memory.ReadFloat(m + CFrameVu1.EulerY);
            }
            return Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
        }

        /// <summary>Weapon HP for a blast: <see cref="BlastWhp"/>, taken by the engine's own drain (<see cref="WeaponWhp"/>),
        /// which breaks the blade at 0 the engine's way.</summary>
        private static void DrainWhp() => WeaponWhp.Drain(Items.bigbang, BlastWhp, "[BigBang] detonation ");

        private static void Reset(BlastState st)
        {
            ClearTint(st);
            RestoreSwing(st);          // stats, kick constants and the charge radii
            RestoreImmunity();         // ⚠ shared ELF data: never leave the explosions inert
            ReleaseJudgement();
            ToanLockOn.ReleaseReach(); _faceHold = 0; ReleaseRedirect();
            { long pool = CollisionPool.Resolve(); foreach (var (slot, _) in _shells) if (pool != 0) CollisionPool.Deactivate(pool, slot); _shells.Clear(); }
            if (st.crushing) { GuardBreak.Drive(false); st.crushing = false; }
            st.chargeAction = 0;
        }
    }
}
