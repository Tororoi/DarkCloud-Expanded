using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang — Solar Harvest from the Sun Sword line, and a guard-charged blade whose next landed hit
    /// detonates (Detonate).</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        private const int    TickMs           = 30;
        private const float  BlastRadius      = 50f;    // the LEVEL-1 blast, and the combo finisher's
        // LEVEL 2 — the whirlwind. Charged past the lunge, the spin is the detonation: a bigger blast, centred on
        // Toan because the whirl is (the lunge's is centred on what it hit), and thrown proportionally harder.
        // Distance ≈ force²/(2·decay), so clearing the radius takes a force that squares past 2·decay·radius.
        private const float  WhirlRadius      = 60f;
        private const float  WhirlKick        = 4.0f;   // force²/(2·decay) ≈ 67 units: still clears the radius
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
        private const float  DamageFraction   = 3.0f;   // the blast's base damage, as a multiple of the weapon's attack
        // ── the blast IS the charge attack's own hit ─────────────────────────────────────
        // Nothing here detects a hit or plants a sphere. ToanKey_Play built its two charge-attack hit radii as baked
        // immediates; the ISO patch (ElfWeaponPatches.PatchChargeHitRadius) turned them into the data words
        // CodeCaves.ChargeHitRadius, so widening those words widens the ENGINE'S OWN sphere — and the engine then
        // does the whole job: who is inside it (real hurtboxes, height included), one plant per frame so a spin
        // sweeps several enemies, the damage, and the knockback. What the ability contributes is all data written
        // while the charge is up: the radius, the boosted Attack, the elementless attribute, and the kick.
        //
        // This replaced a mod-tick design that watched for the engine's hit mark and planted its own spheres. It was
        // wrong repeatedly and in both directions — the lunge THROWS Toan at his target, so its hit landed long
        // after the action id moved on and the detonation never fired; the whirlwind damages every frame it spins,
        // so the same watch fired over and over.
        private const float  KickStrength     = 3.5f;   // with KickDecay: distance ≈ force²/(2·decay) ≈ 50 units
        private const float  KickDecay        = 0.12f;  // vanilla melee is 1.2 at 0.2, roughly 3.6 units

        private const float  WhpHits          = 5f;
        // THE BLADE ON A REGULAR CHARGE. Toan's charge meter runs 1.0 → 3.0 (lunge at 1.5, whirlwind at 2.5), and
        // the blade whitens across it exactly as it does for a guard charge — the same tint, driven by the meter
        // instead of by held time. It stands aside while SunSword.FlashArmed: Solar Flash owns the blade then, and
        // two ramps fighting over one mesh would only flicker.
        private const float  ChargeMeterFloor = 1.0f;
        private const float  LungeThreshold   = 1.5f;   // the meter at which the charge becomes a lunge
        // ONE DETONATION PER CHARGE ATTACK, and the window outlives the action. A hit is seen through the engine's
        // mark, which this loop polls every TickMs — a couple of frames — so the lunge's action id has often moved on
        // by the time its hit is noticed, and keying the detonation on the id being current missed it every time.
        // The window opens when a charge attack starts and stays open a few ticks past its end; it is spent by the
        // first hit inside it. The whirlwind needs the other half of that: it plants its damage EVERY frame it spins,
        // so without the spend flag one spin detonated over and over.
        private const float  LungeAttackMult  = 1.5f;   // the engine's own multiplier on a lunge and on the whirlwind
        // THE COMBO FINISHER detonates too: the fourth swing primes it, the fifth carries it. Its hit radius is the
        // ELF constant combos 3-5 share (Weapons.SetComboHitRadius) — held only while that fifth swing is out, so the
        // two hits before it keep their own reach — and the engine puts ×1.8 on a fifth hit, so the Attack boost is
        // the blast fraction over THAT.
        private const int    ComboFinisher    = PlayerAction.ActionComboLast;       // action 0x28 — the fifth swing
        private const int    ComboPrimer      = PlayerAction.ActionComboLast - 1;   // 0x27 — the one before it
        private const float  ComboAttackMult  = 1.8f;
        // …and the finisher is written the same ×2.0 Attack boost the lunge gets, which over the engine's own ×1.8
        // lands at 3.6 × the weapon's attack — the reward for actually landing five swings in sequence.
        // (DamageFraction × this ÷ 1.8 = the 2.0 written, so this is 1.2.)
        private const float  ComboDamageBonus = 1.2f;
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

        private const int    ElementNoneIndex = 5;      // GetWeaponElementAttr clamps 0..5; element_tbl[5] = 0
        private const int    ElementIndexOff  = 0x16;   // byte on the weapon record: which element the blade swings
        // ⚠ SHARED ELF CONSTANTS, held only while the charge is up. The strength word is every ToanKey_Play melee
        // kick's (1.2, read from TEN places in the ELF — Goro's smash shockwave among them); the decay word (0.3) is
        // the one the LUNGE and WHIRLWIND sites read, shared with combo hits 3 and 5, which cannot run while a charge
        // attack is executing. RestoreSwing puts both back on the spend, a dropped charge, a swap and a floor change.
        private const long   SwingKickStrength = 0x202A1AF8, SwingKickDecay = 0x202A1A80;
        // The explosion is the thrown-gem FIRE burst, spawned at the hit point by plain field writes into the
        // always-resident Maseki pool. It is authored as a small thrown-gem puff, so it is scaled up hard and
        // slowed down to stop the animation snapping at that size. Scale is the sub-slot's own CObject scale, not a
        // radius: it does not change what the blast HITS (that is BlastRadius).
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
        // target at HoverScale, fading in over FadeSeconds; the blue glow moves onto it and the target's NAME plate is
        // hidden (CodeCaves.NameHide, the gate the plate's getter ANDs in — the enemy itself is never touched, so a
        // kill during the hover still counts for whatever counts kills). Losing the lock fades it out and the glow
        // shrinks off it and swells back up on Toan. The primed swing then does not flash: the blade FALLS over DropSeconds, and where
        // it lands it detonates — the flash, the blast below, every enemy on the floor turned to face it, and the
        // weapon-HP bill. Swinging with no lock is the ordinary flash.
        // The blade hangs HoverMargin above the target's own top — read live from the engine's body-collision
        // spheres (BodyCollision: world centre + radius, rebuilt every frame from the bone's SCALED position, so a
        // grown miniboss's top rises with it and no per-species table is needed). HoverFallback when none is up.
        private const float  HoverMargin      = 6f;
        private const float  HoverFallback    = 20f;
        // The copy hangs point-DOWN from its root (the grip), so the TIP is the blade's length below the placement;
        // the grip goes up by that much so the tip is what clears the target. The length is the weapon's dcol1 reach
        // (the offline table Weapons keeps), at the copy's scale.
        private const float  BladeLengthFallback = 12f;
        private const float  HoverScale       = 2.0f;
        private const double FadeSeconds      = 0.25;
        // THE FALL is gravity: from rest at the hover height, accelerating to land at DropSeconds — h = h0 − ½·g·t²
        // with g chosen to arrive on time. Placed by its own frame-rate thread (FallLoop), the way the slingshot
        // prop's orbit thread re-places that copy: the draw re-seeds the root from the slot every frame, so writes
        // at frame rate are smooth where the 30 ms tick was a staircase. Cosmetic by construction — the landing
        // itself is still called from the tick, so nothing that matters rides on this thread's timing.
        private const double DropSeconds      = 0.35;
        private const int    FallTickMs       = 4;
        // As the blade falls the floor's light and fog are driven DOWN (SolarLighting.Dim) on an EXPONENTIAL ramp
        // that peaks at the landing itself: k = (e^(a·u) − 1) / (e^a − 1) over the fall's fraction u, so it barely
        // moves at first and plunges in the last moments, with the flash then landing from the darkest frame.
        // DimSharpness is a — higher holds the light longer and drops it later. Driven from the fall thread (the
        // same curve as the fall) and handed to the flash at the landing.
        private const double DimSharpness     = 4.0;
        // …and the flash follows the burst — same tick, but never before it; a head start read as late.
        private const double FlashDelay       = 0.0;
        private const float  DropWhpFactor    = 10f;    // 1.5 × 10 = 15 weapon HP before Endurance scales it — the engine's own formula
        // THE BLAST FALLS OFF WITH DISTANCE: the damage step an enemy takes is the innermost radius its distance from the
        // blast is within (outermost first here; PlantFalloff keeps the last match). One hit entry per enemy, centred
        // on its own body, carries that step — an entry is consumed by the first enemy it touches, so the sizing is
        // what makes it that enemy's alone.
        private static readonly (float radius, float times)[] Falloff = { (50f, 1f), (40f, 2f), (25f, 3f), (10f, 4f) };

        internal static bool GlowOwned { get; private set; }   // the blue glow is on the blade copy, not on Toan
        internal static bool Dropping  { get; private set; }
        private static bool   _landed;
        private static int    _hoverSlot = -1;                 // the enemy the blade hangs over (−1 = none up)
        private static float  _hoverAlpha;                     // 0..1, the fade
        private static bool   _hoverOut;                       // fading OUT (lock lost) — no re-placement
        private static DateTime _dropStart, _gateLog;
        private static float  _dropX, _dropH, _dropY;          // where the BLAST goes off: the target's root, on the ground
        private static float  _fallHeight;                     // how far above _dropH the blade hung when it was let go
        private static float  _bladeX, _bladeY;                // where the BLADE falls to (the target itself)
        private static volatile bool _fallDone;                // the fall thread has brought it to the ground
        private static DateTime _landedAt;                     // when the burst went off; the flash waits FlashDelay
        private static Thread _fallThread;
        internal static bool LandingPending => _landedAt != default;
        private static float  _hoverHeight = HoverFallback;    // how far above its root the current target's top is, plus the margin
        private static int    _hoverTraceTicks;

        private const int    PrimeTicks       = 60;    // ≈1.8 s: a charge that never connects gives its prime up
        private const float  LungeBurstReach  = 12f;   // fallback: the blade's reach in front of Toan
        private const float  MarkSanity       = 60f;   // a mark further than this from Toan is not his swing's
        // ⚠ The ISO patch this ability's damage depends on, as the patched instruction reads: `lui $2,0x01FB`
        // (ElfWeaponPatches.PatchChargeHitRadius). Checked once per floor, because without it the radius words are
        // never read and the charge attacks stay their stock 6 and 12 — which looks exactly like the ability
        // silently doing nothing.
        private const long   LungeRadiusInsn  = 0x20241AC0;
        private const uint   LungeRadiusPatched = 0x3C0201FB, LungeRadiusVanillaInsn = 0x3C0240C0;
        private const float  BurstScale       = 10.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Ice;    // the ANIMATION only
        private sealed class BlastState
        {
            public byte floor = 0xFF;
            public bool crushing;                       // the guard break is currently driven on
            public bool tinted;                         // the blade is carrying this ability's charge tint
            public bool swingArmed;                     // the charge attack's stats and radius are overridden
            public ushort weaponAttack;                 // …the blade's real Attack, and
            public byte swingElement;                   // …its real element index, and
            public float swingKickS, swingKickD;        // …the melee kick constants it is holding
            public float comboRadius;                   // …and the combo swings' shared hit radius
            public ushort armedValue;                   // the boosted Attack that was written, to recognise our own
            public int  chargeAction;                   // the charge attack that has already erupted (0 = none)
            public bool primed;                         // an attack is up and its detonation is unspent
            public bool wasPriming;                     // …the priming state last tick, so a prime is set on its EDGE
            public int  primeTicks;                     // …how long a prime lives without a hit
            public int  spendTicks;                     // …and how long the swing that may SPEND it stays open
            public bool patchChecked;                   // the radius patch has been verified this floor
            public int  hp = -1;                        // Toan's HP as of the last tick, for the damage probe
        }

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Big Bang's CHARGE ATTACK detonates. The blade whitens as the meter fills, and the charge attack it becomes
        /// is the explosion: its hit reaches <see cref="BlastRadius"/> for the level-1 lunge and
        /// <see cref="WhirlRadius"/> for the level-2 whirlwind, deals <see cref="DamageFraction"/> × the weapon's
        /// attack through the normal formula, carries NO element so no resistance blunts it, crushes guards, and
        /// throws what it hits clear. The lunge erupts in a fire burst at the blade's reach; the whirlwind has no
        /// burst spawned for it because its own model IS explosion.chr. The blade pays <see cref="WhpHits"/> swings'
        /// worth of weapon HP per charge attack. Dungeon only.
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

        private static void Tick(BlastState st)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) Reset(st); st.floor = floor; }

            // POLLED EVERY TICK, ahead of every early return. The mark counter is a running total of hits on
            // monsters — anyone's, from anything — so a poll that only ran while a charge was primed compared against
            // a baseline from the LAST one, and read everything stamped in between as a hit that had just landed.
            // Solar Flash is the clearest case: its own hits stamp marks on every enemy it strikes, and the next
            // lunge then burst instantly on the nearest of them.
            bool hit = HitLanded(out float hx, out float hh, out float hy);
            ExpireShells();

            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId) { ClearTint(st); RestoreSwing(st); return; }

            if (ExplosionSeeded) MaintainExplosionScale();
            ArmImmunity();
            AnswerAutoGuard();
            JudgementTick();
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
            bool  swinging = action == PlayerAction.ActionLunge || whirl;

            // The blade whitens across the regular charge exactly as it does across a guard charge — the same tint,
            // driven by the meter instead of by held time. It stands aside while Solar Flash owns the blade.
            if (action == PlayerAction.ActionWindup && !SunSword.FlashArmed)
            {
                float k = (meter - ChargeMeterFloor) / (PlayerAction.ChargeMeterCap - ChargeMeterFloor);
                SolarBlade.Set(Math.Min(1f, Math.Max(0f, k)), SolarBlade.BigBangModel,
                               SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                st.tinted = true;
            }
            else if (st.tinted && !swinging) ClearTint(st);

            // Armed from the moment the meter reaches lunge range, so the numbers are in place before the hit window
            // opens inside the action. Nothing below depends on WHEN a tick lands: every write is the same value.
            bool finisher = action == ComboFinisher;
            if (!SunSword.FlashArmed && (swinging || finisher
                                         || (action == PlayerAction.ActionWindup && meter >= LungeThreshold)))
            {
                ArmSwing(st, whirl ? WhirlKick : KickStrength,
                         finisher ? ComboAttackMult : LungeAttackMult, finisher);
            }
            else
                RestoreSwing(st);

            // Guards are crushed while the charge swings: a blocked charge attack would eat the detonation.
            bool crush = swinging || finisher;
            if (crush != st.crushing) { GuardBreak.Drive(crush); st.crushing = crush; }

            // PRIMED BY THE CHARGE, SPENT BY THE HIT. The charge itself is what arms the detonation — a wind-up
            // held past lunge strength, or the swing it becomes — and it stays armed until a hit lands. Priming on a
            // state the player HOLDS, rather than catching the swing, is what makes this insensitive to when a tick
            // falls; the lunge in particular throws Toan at his target, so its hit can land long after the action id
            // has moved on, and anything keyed to the action being current missed it every time.
            // …and the combo's fourth swing primes the fifth the same way a wind-up primes the lunge.
            //
            // ⚠ On the EDGE of that state, not while it holds. The whirlwind damages every frame it spins, so a prime
            // re-armed whenever the state was true was spent and re-armed over and over, and the spin ended with the
            // blade still primed — the next ordinary hit then set off a detonation that belonged to nothing.
            bool priming = swinging || finisher || action == ComboPrimer
                        || (action == PlayerAction.ActionWindup && meter >= LungeThreshold);
            if (priming && !st.wasPriming) { st.primed = true; st.primeTicks = PrimeTicks; }
            st.wasPriming = priming;
            if (st.primed && !priming && --st.primeTicks <= 0)
                st.primed = false;                                   // it came to nothing

            // WHICH swing may spend it: the charge attacks and the combo's fifth, and for a moment after — the lunge
            // throws Toan at his target, so its hit lands well past the action. The fourth combo swing PRIMES but may
            // not spend, or its own hit would take the detonation meant for the fifth.
            if (swinging || finisher) st.spendTicks = PrimeTicks;
            else if (st.spendTicks > 0) st.spendTicks--;

            // The blade's weapon-HP bill, once per charge attack.
            if ((swinging || finisher) && action != st.chargeAction)
            {
                st.chargeAction = action; DrainWhp();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] charge attack 0x{action:X}: hit radii {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusLunge):F0}"
                    + $"/{Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl):F0}"
                    + $", attack {st.weaponAttack}→{Player.Weapon.GetCurrentWeaponAttack()}, armed {st.swingArmed}, flash {SunSword.FlashArmed}");
            }
            else if (!swinging && !finisher) st.chargeAction = 0;

            // The BURST goes where the charge connected. ⚠ Cosmetic by construction: the DAMAGE is the engine's own
            // charge sphere, which lands whether or not this cue is seen, so a burst that arrives late — or not at
            // all — costs nothing but the picture. The whirlwind needs none: its own model IS explosion.chr.
            if (st.primed && st.spendTicks > 0 && hit)
            {
                st.primed = false;
                if (whirl || st.chargeAction == PlayerAction.ActionWhirlwind) return;
                GemBurst.Show(BurstElement, hx, hh, hy, BurstScale, damage: 0, speedMult: BurstSpeed);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] lunge burst at the hit mark ({hx:F0},{hh:F0},{hy:F0})");
            }
        }

        private static int _lastMark = -1, _lastLife = -1;
        /// <summary>Where the engine last stamped a hit on a monster, for the lunge's burst and nothing else. A hit
        /// that DAMAGES stamps the struck part's position at the counter's index and advances it; a BLOCKED one
        /// re-stamps the current index and only resets that mark's life, which otherwise counts down from 16. A mark
        /// further than <see cref="MarkSanity"/> from Toan is not his.</summary>
        private static bool HitLanded(out float x, out float h, out float y)
        {
            x = h = y = 0f;
            int n = PlayerAction.HitPointMarkCount;
            int now = Memory.ReadInt(PlayerAction.HitSparkCounter);
            int cur = ((now % n) + n) % n;
            int life = Memory.ReadInt(PlayerAction.HitPointMark + (long)cur * PlayerAction.HitPointMarkStride
                                      + PlayerAction.HitPointMarkLife);
            int was = _lastMark, wasLife = _lastLife;
            _lastMark = now; _lastLife = life;
            if (was < 0) return false;

            int idx;
            if (now != was)          idx = ((now - 1) % n + n) % n;
            else if (life > wasLife) idx = cur;
            else return false;
            long e = PlayerAction.HitPointMark + (long)idx * PlayerAction.HitPointMarkStride;
            x = Memory.ReadFloat(e); h = Memory.ReadFloat(e + 4); y = Memory.ReadFloat(e + 8);
            float dx = x - Memory.ReadFloat(Addresses.dunPositionX);
            float dy = y - Memory.ReadFloat(Addresses.dunPositionY);
            float dh = h - Memory.ReadFloat(Addresses.dunPositionZ);
            return dx * dx + dy * dy + dh * dh <= MarkSanity * MarkSanity;
        }


        private static void ArmSwing(BlastState st, float kick, float moveMult, bool combo)
        {
            ushort atk = Player.Weapon.GetCurrentWeaponAttack();
            if (!st.swingArmed)
            {
                if (atk == 0) return;
                st.weaponAttack = atk;
                st.swingElement = Memory.ReadByte(WeaponHave.BattleWeaponRecord + ElementIndexOff);
                st.swingKickS   = Memory.ReadFloat(SwingKickStrength);
                st.swingKickD   = Memory.ReadFloat(SwingKickDecay);
                st.comboRadius  = Weapons.ComboHitRadius;
                st.swingArmed   = true;
            }
            else if (atk != 0 && atk != st.armedValue)
            {
                st.weaponAttack = atk;                                   // the record was rebuilt: this is the real one
            }
            st.armedValue = Boosted(st.weaponAttack, moveMult, combo ? ComboDamageBonus : 1f);
            Player.Weapon.SetCurrentWeaponAttack(st.armedValue);
            Memory.WriteByte(WeaponHave.BattleWeaponRecord + ElementIndexOff, ElementNoneIndex);
            Memory.WriteFloat(SwingKickStrength, kick);   // the whirl throws harder than the lunge
            Memory.WriteFloat(SwingKickDecay, KickDecay);
            if (combo) Weapons.SetComboHitRadius(BlastRadius);     // the fifth swing's own sphere becomes the blast
            else       Weapons.SetChargeHitRadii(BlastRadius, WhirlRadius);
        }

        /// <summary>The Attack that makes a lunge land <see cref="DamageFraction"/> × the blade's own, once the
        /// engine's ×<see cref="LungeAttackMult"/> is on top of it.</summary>
        /// <summary>The Attack that makes a move landing <paramref name="moveMult"/> × its weapon's attack hit for
        /// <see cref="DamageFraction"/> × <paramref name="bonus"/> × it instead.</summary>
        private static ushort Boosted(ushort attack, float moveMult, float bonus) =>
            (ushort)Math.Min(ushort.MaxValue, Math.Max(1, (int)Math.Round(attack * DamageFraction * bonus / moveMult)));

        /// <summary>The blade's own numbers back, and the shared kick constants with them. ⚠ The weapon fields are
        /// restored only while Big Bang is still in hand and the record still reads what we left — an equip change or
        /// a menu has already rebuilt it, and writing a stale Attack over a rebuilt record would hand another weapon
        /// Big Bang's damage. The ELF kick constants are global and are ALWAYS put back.</summary>
        private static void RestoreSwing(BlastState st)
        {
            if (!st.swingArmed) return;
            if (Player.Weapon.GetCurrentWeaponId() == Items.bigbang
                && Player.Weapon.GetCurrentWeaponAttack() == st.armedValue)
            {
                Player.Weapon.SetCurrentWeaponAttack(st.weaponAttack);
                Memory.WriteByte(WeaponHave.BattleWeaponRecord + ElementIndexOff, st.swingElement);
            }
            Memory.WriteFloat(SwingKickStrength, st.swingKickS);
            Memory.WriteFloat(SwingKickDecay, st.swingKickD);
            Weapons.SeedChargeHitRadii();                          // …and the stock 6 / 12 back
            Weapons.ComboHitRadius = st.comboRadius;               // …and the combo swings' own reach
            st.weaponAttack = 0; st.armedValue = 0; st.swingArmed = false;
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
                if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - _burstBind[0] * ExplosionScale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _burstBind[k] * ExplosionScale);
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
            bool locked   = PlayerAction.LockHeld(out int lockSlot)
                            && lockSlot < EnemyAddresses.FloorSlots.Count && HasHp(lockSlot);
            bool primed   = SunSword.PrimedFor(Items.bigbang);
            double dt     = TickMs / 1000.0;
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
                if (!BladeProp.Maintain() || !HasHp(_hoverSlot)) { AbandonHover(); Dropping = false; SolarLighting.EndDim(); return; }
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
                    if (!BladeProp.Active && !BladeProp.Spawn(HoverScale)) return;
                    _hoverSlot = lockSlot;
                }
                // The name plate off, through the getter's gate (CodeCaves.NameHide): the flag itself is re-raised by
                // the engine every frame the target is on screen, so writing it only made the name flicker.
                Memory.WriteInt(CodeCaves.NameHide, 1);
                if (!BladeProp.Maintain()) { AbandonHover(); return; }
                long a = EnemyAddresses.FloorSlots.SlotAddr(lockSlot, 0);
                // PINNED to the target's model root: the engine chains the copy's world through the enemy's every
                // frame, so it rides a moving enemy with no placement writes at all — no tick, no thread, no jitter.
                // Heights are measured from that root's own WORLD height — the slot's LocationZ is floor-relative,
                // and taking it for the root hung the blade a body too low and started the fall a body too high.
                uint enemyRoot = Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharModel);
                float rootH = RootWorldHeight(enemyRoot, a);
                _hoverHeight = HoverHeightFor(lockSlot, rootH);
                if (BladeProp.PinnedTo != enemyRoot) BladeProp.Pin(enemyRoot, _hoverHeight);
                // Its flat faces the way the fallen blade will (PlayerFacing): the parent's yaw is taken back out.
                BladeProp.Face(PlayerFacing(), Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharRotY));
                _hoverAlpha = (float)Math.Min(1.0, _hoverAlpha + dt / FadeSeconds);
                BladeProp.Alpha(_hoverAlpha);
                DriveGlow(true);                                                 // the glow crosses to the blade
                if (_hoverTraceTicks < 12)                                       // DIAGNOSTIC: the first ~third of a second of every hover
                {
                    _hoverTraceTicks++;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] hover: " + BladeProp.Where()
                        + $" | height {_hoverHeight:F1} {SphereTops(lockSlot, rootH)}");
                }
                return;
            }

            _hoverTraceTicks = 0;
            if (_hoverSlot >= 0)                                             // up, but no longer wanted: fade out and down
            {
                _hoverOut = true;
                _hoverAlpha = (float)Math.Max(0.0, _hoverAlpha - dt / FadeSeconds);
                if (BladeProp.Maintain()) BladeProp.Alpha(_hoverAlpha);
                DriveGlow(false);                                                // …and the glow shrinks off it first
                if (_hoverAlpha <= 0f && !SolarGlow.OnAnchor(BladeProp.RootGuest)) AbandonHover();
                return;
            }
            if (GlowOwned) DriveGlow(false);                                     // the hand-off back to Toan, to its end
        }

        /// <summary>The glow's HAND-OFF between Toan and the blade: it shrinks away where it is, then swells up from
        /// nothing where it is wanted — never a jump, and paced to the blade's own fade (<see cref="FadeSeconds"/>):
        /// the shrink takes <see cref="HandoffShrink"/> of it and the swell the rest, so the glow is at full size on
        /// the blade the moment the blade is fully in, and back to full on Toan the moment the blade is gone. (The
        /// glow cave draws ONE sprite, so it cannot be in two places at once; this is the crossing.) On the blade it
        /// hangs at the blade's middle (the copy's root is the grip, and the blade points down its full length below
        /// it). <see cref="GlowOwned"/> is true from the first step until it is back up on Toan, so Solar Flash's own
        /// Show stays out of the way meanwhile.</summary>
        private static void DriveGlow(bool onBlade)
        {
            uint want = onBlade ? BladeProp.RootGuest : 0u;
            if (onBlade && want == 0) return;
            GlowOwned = true;
            SolarGlow.Tick();
            if (SolarGlow.IsUp)
            {
                if (SolarGlow.OnAnchor(want)) { if (!onBlade) GlowOwned = false; return; }   // where it should be
                SolarGlow.Fade(FadeSeconds * HandoffShrink);                                 // shrink off the old place; Tick takes it down
                return;
            }
            if (!SunSword.PrimedFor(Items.bigbang)) { GlowOwned = false; return; }           // nothing to carry any more
            SolarGlow.Show(ToanGlowBakes.BlueName, want, onBlade ? -BladeLength() * HoverScale / 2f : 0f,
                           FadeSeconds * (1.0 - HandoffShrink));
        }
        private const double HandoffShrink = 0.3;   // the share of the blade's fade the glow spends leaving its old place

        /// <summary>Alive enough to hang a blade over: HP above zero.</summary>
        private static bool HasHp(int slot) =>
            slot >= 0 && slot < EnemyAddresses.FloorSlots.Count
            && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) > 0;

        /// <summary>How high above <paramref name="slot"/>'s root to hang the blade: the top of its highest live body
        /// sphere (centre height + radius, from the arrays CheckDmg rebuilds each frame) plus <see cref="HoverMargin"/>,
        /// relative to <paramref name="rootH"/>. <see cref="HoverFallback"/> if it has no sphere up.</summary>
        private static float HoverHeightFor(int slot, float rootH)
        {
            long b = BodyCollision.SlotBase(slot);
            float top = float.MinValue;
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                float h = Memory.ReadFloat(b + BodyCollision.CentreArray + part * BodyCollision.CentreStride + 4)
                        + Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride);
                if (h > top) top = h;
            }
            float clear = top == float.MinValue ? HoverFallback : Math.Max(HoverMargin, top - rootH + HoverMargin);
            return clear + BladeLength() * HoverScale;
        }

        /// <summary>The sword's reach from grip to tip, at 1×: its dcol1 frame's Z from the offline table.</summary>
        private static float BladeLength()
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            return ToanWeapons.TryGetValue(wid, out WeaponData wd) && wd.Dcol1.HasValue
                 ? Math.Abs(wd.Dcol1.Value) : BladeLengthFallback;
        }

        /// <summary>DIAGNOSTIC: every active body sphere of <paramref name="slot"/> as (h+r), beside its root height.</summary>
        private static string SphereTops(int slot, float rootH)
        {
            long b = BodyCollision.SlotBase(slot);
            var parts = new List<string>();
            for (int part = 0; part < BodyCollision.MaxBodyParts; part++)
            {
                if (Memory.ReadInt(b + BodyCollision.ActiveArray + part * BodyCollision.BodyPartStride) == 0) continue;
                long c = b + BodyCollision.CentreArray + part * BodyCollision.CentreStride;
                parts.Add($"[{part}] c=({Memory.ReadFloat(c):F0},{Memory.ReadFloat(c + 4):F0},{Memory.ReadFloat(c + 8):F0}) r={Memory.ReadFloat(b + BodyCollision.RadiusArray + part * BodyCollision.BodyPartStride):F1}");
            }
            return $"root h {rootH:F0}; spheres: " + (parts.Count == 0 ? "none active" : string.Join(" ", parts));
        }

        /// <summary>The swing while primed: if the blade is hanging over a target, let it fall — the flash waits for
        /// the landing. False when there is nothing to drop, and Solar Flash fires as it always has.</summary>
        internal static bool BeginDrop()
        {
            if (_hoverSlot < 0 || _hoverOut || !BladeProp.Active || !HasHp(_hoverSlot)) return false;
            long a = EnemyAddresses.FloorSlots.SlotAddr(_hoverSlot, 0);
            _bladeX = Memory.ReadFloat(a + EnemySlotOffsets.LocationX);
            _bladeY = Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
            _dropH  = RootWorldHeight(Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(_hoverSlot) + CCharacter.CharModel), a);
            _dropX = _bladeX; _dropY = _bladeY;                              // the blast goes off on the target itself
            // The fall starts from where the blade HANGS, read off its own world matrix — not from the height it was
            // pinned with, which the target's pose may have moved on from — so there is no step at the start.
            float hang = BladeProp.WorldHeight();
            _fallHeight = float.IsNaN(hang) ? _hoverHeight : Math.Max(1f, hang - _dropH);
            BladeProp.Unpin(PlayerFacing());                                 // off the enemy and into the world, where it is, to fall
            SolarLighting.BeginDim();                                        // the lights go down with it
            _dropStart = GameClock.Now; Dropping = true; _landed = false; _fallDone = false; _landedAt = default;
            if (_fallThread == null || !_fallThread.IsAlive)
            { _fallThread = new Thread(BladeLoop) { IsBackground = true, Name = "BigBangBlade" }; _fallThread.Start(); }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] judgement blade falls on slot {_hoverSlot}");
            return true;
        }

        /// <summary>The FALL at frame rate — the slingshot prop's orbit thread, applied here: the copy's height along a
        /// gravity curve until it reaches the ground, then <see cref="_fallDone"/> for the tick to land on. (The hover
        /// needs no thread: it is pinned to the enemy and the engine carries it.) A pause freezes a fall in the air by
        /// shifting the start time, so it resumes where it was.</summary>
        private static void BladeLoop()
        {
            while (true)
            {
                try
                {
                    if (!Dropping || _fallDone) { Thread.Sleep(20); continue; }
                    if (Player.CheckDunIsPausedOrMenu()) { _dropStart = _dropStart.AddMilliseconds(FallTickMs); Thread.Sleep(FallTickMs); continue; }
                    double t = (GameClock.Now - _dropStart).TotalSeconds;
                    double g = 2.0 * _fallHeight / (DropSeconds * DropSeconds);
                    float  h = _dropH + (float)Math.Max(0.0, _fallHeight - 0.5 * g * t * t);
                    BladeProp.Place(_bladeX, h, _bladeY, PlayerFacing());
                    double u = Math.Min(1.0, t / DropSeconds);
                    SolarLighting.Dim((float)((Math.Exp(DimSharpness * u) - 1.0) / (Math.Exp(DimSharpness) - 1.0)));
                    if (t >= DropSeconds) _fallDone = true;
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] fall tick failed: " + e.Message); }
                Thread.Sleep(FallTickMs);
            }
        }

        /// <summary>Solar Flash asks once per tick while it waits: has the blade landed — and has the burst had its
        /// <see cref="FlashDelay"/> head start? Consumed on read.</summary>
        internal static bool TakeDropLanded()
        {
            if (!_landed || (GameClock.Now - _landedAt).TotalSeconds < FlashDelay) return false;
            _landed = false; _landedAt = default; return true;
        }

        /// <summary>The blade has hit the ground: the blast, every enemy turned to look, the weapon-HP bill, and the
        /// copy gone — the flash is Solar Flash's to fire now.</summary>
        private static void Land()
        {
            // The white-out and the burst on the SAME frame, from here. Solar Flash's own tick runs the rest of the
            // flash (the light hit, the blinding, the blade and glow) once TakeDropLanded hands it the landing — and
            // that is after the hit entries and the turns below, well past a frame — so the lighting write goes out now,
            // and Solar Flash is told it is lit (a second Flash there restores the floor's light and whites it again).
            SunSword.BigBangFlash.ArmLighting();
            SolarLighting.Flash();
            GemBurst.Show(BurstElement, _dropX, _dropH, _dropY, BurstScale, damage: 0, speedMult: BurstSpeed);
            PlantFalloff(_dropX, _dropH, _dropY);
            TurnEnemiesToward(_dropX, _dropY);
            DrainWhp(DropWhpFactor);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] judgement blade lands on ({_bladeX:F0},{_dropH:F0},{_bladeY:F0}); blast at ({_dropX:F0},{_dropY:F0})");
            AbandonHover();
            _landedAt = GameClock.Now; _landed = true;
            Dropping = false;
        }

        /// <summary>Everything the hover put up, back down: the copy, the target's name bar, the glow's home.</summary>
        private static void AbandonHover()
        {
            Memory.WriteInt(CodeCaves.NameHide, 0);                                 // the name plate back
            // A glow left hanging on the copy's root after the copy is gone is a sprite drawn every frame at a node in
            // mod memory that nothing maintains; the one run that left it there ended in the game resetting six
            // seconds later. The fade-out waits for the glow to shrink off first; every other way here cuts it.
            if (SolarGlow.OnAnchor(BladeProp.RootGuest)) SolarGlow.Hide();
            BladeProp.Despawn();
            _hoverSlot = -1; _hoverAlpha = 0f; _hoverOut = false;
            // GlowOwned stays: DriveGlow brings the glow back up on Toan while the charge still stands, then lets go.
        }

        /// <summary>The blast that falls with the blade: one hit entry per live enemy in reach, its damage the
        /// <see cref="Falloff"/> step for that enemy's distance, its kick from the blast.</summary>
        private static void PlantFalloff(float x, float h, float y)
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
                byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, cr, Math.Max(1, (int)Math.Round(attack * times)), 0);
                void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
                F(0x80, x); F(0x84, h); F(0x88, y);                        // the kick still comes from the blast
                F(0x90, KickStrength); F(0x94, KickDecay);
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
        private static void BodyCentre(int slot, long a, out float cx, out float ch, out float cy, out float cr)
        {
            long b = BodyCollision.SlotBase(slot);
            cx = Memory.ReadFloat(a + EnemySlotOffsets.LocationX); cy = Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
            ch = RootWorldHeight(Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharModel), a) + BodyRadiusFallback;
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

        /// <summary>The enemy's model root in the world: its height is the world matrix's, the same number the body
        /// spheres and a pinned copy are measured in. (The slot's LocationZ is floor-relative and is not it.) The
        /// slot's LocationZ stands in only when the root cannot be read.</summary>
        private static float RootWorldHeight(uint rootGuest, long slotAddr)
        {
            if (Memory.IsValidGuest(rootGuest))
                return Memory.ReadFloat(Memory.ToMmu(rootGuest) + CFrameVu1.WorldMatrix + 0x34);
            return Memory.ReadFloat(slotAddr + EnemySlotOffsets.LocationZ);
        }
        private const int ShellLifeTicks = 3;   // ticks an unconsumed entry (a miss) stays before it is withdrawn
        private const int ShellPoolReserve = 16; // free entries the blast always leaves the engine
        private static readonly List<(int slot, int ticks)> _shells = new List<(int, int)>();

        /// <summary>Withdraw the hit entries the engine has not consumed (an enemy that moved off its own).</summary>
        private static void ExpireShells()
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

        /// <summary>Every living enemy turned to face the point — its facing vector is the unit it steers by.</summary>
        private static void TurnEnemiesToward(float x, float y)
        {
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long a = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                float dx = x - Memory.ReadFloat(a + EnemySlotOffsets.LocationX);
                float dy = y - Memory.ReadFloat(a + EnemySlotOffsets.LocationY);
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-3f) continue;
                Memory.WriteFloat(a + EnemySlotOffsets.FacingX, dx / len);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingY, 0f);
                Memory.WriteFloat(a + EnemySlotOffsets.FacingZ, dy / len);
            }
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

        /// <summary>The blade pays for the detonation in weapon HP: <see cref="WhpHits"/> ordinary swings' worth, by
        /// the engine's own formula — `(1.5 − 0.01 × Endurance) × factor`, halved by Durable and doubled by Fragile
        /// (BattleSubWeaponDmg 0x1B5D90). Endurance comes from the BATTLE record, where attachments have already been
        /// folded in; WHP itself lives on the INVENTORY record (+0x10 of the equipped bag slot), which is the copy the
        /// engine drains and the menu shows.
        ///
        /// ⚠ Floored at 1, never 0. Everything that happens at zero WHP — the auto-consumed Repair Powder, the
        /// warnings, the weapon breaking back to its base form — lives inside that native function, which a write here
        /// does not call. Leaving 1 keeps the blade whole and lets the next ordinary hit take it to zero through the
        /// engine's own path, with all of that intact. A weapon at Endurance 150 pays nothing, exactly as its swings
        /// cost nothing.</summary>
        private static void DrainWhp(float swings = WhpHits)
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.ToanId));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(Player.ToanId, bag);
            if (Memory.ReadUShort(rec) != Items.bigbang) return;             // not the blade we just spent

            float factor = swings;
            int flags = Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset);
            if ((flags & WeaponHave.DurableFlag) != 0) factor *= 0.5f;
            if ((flags & WeaponHave.FragileFlag) != 0) factor *= 2f;
            int endurance = Memory.ReadShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffEnduranceOffset);
            float drain = (1.5f - 0.01f * endurance) * factor;
            if (drain <= 0f) return;

            long whpAddr = rec + WeaponHave.InventoryWeaponWhpOffset;
            float whp = Memory.ReadFloat(whpAddr);
            float left = Math.Max(1f, whp - drain);
            Memory.WriteFloat(whpAddr, left);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] detonation cost {whp - left:F1} WHP ({whp:F0} → {left:F0})");
        }

        private static void Reset(BlastState st)
        {
            ClearTint(st);
            RestoreSwing(st);          // stats, kick constants and the charge radii
            RestoreImmunity();         // ⚠ shared ELF data: never leave the explosions inert
            AbandonHover(); Dropping = false; _landed = false; _fallDone = false; _landedAt = default; SolarLighting.EndDim();
            { long pool = CollisionPool.Resolve(); foreach (var (slot, _) in _shells) if (pool != 0) CollisionPool.Deactivate(pool, slot); _shells.Clear(); }
            if (st.crushing) { GuardBreak.Drive(false); st.crushing = false; }
            st.chargeAction = 0;
        }
    }
}
