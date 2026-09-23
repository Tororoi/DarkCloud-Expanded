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
        private const int    PrimeTicks       = 60;    // ≈1.8 s: a charge that never connects gives its prime up
        private const float  LungeBurstReach  = 12f;   // fallback: the blade's reach in front of Toan
        private const float  MarkSanity       = 60f;   // a mark further than this from Toan is not his swing's
        // ⚠ The ISO patch this ability's damage depends on, as the patched instruction reads: `lui $2,0x01FB`
        // (ElfWeaponPatches.PatchChargeHitRadius). Checked once per floor, because without it the radius words are
        // never read and the charge attacks stay their stock 6 and 12 — which looks exactly like the ability
        // silently doing nothing.
        private const long   LungeRadiusInsn  = 0x20241AC0;
        private const uint   LungeRadiusPatched = 0x3C0201FB, LungeRadiusVanillaInsn = 0x3C0240C0;
        private const float  BurstScale       = 6.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Fire;   // the ANIMATION only
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

            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId) { ClearTint(st); RestoreSwing(st); return; }

            if (ExplosionSeeded) MaintainExplosionScale();
            ArmImmunity();
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
        private static void DrainWhp()
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.ToanId));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(Player.ToanId, bag);
            if (Memory.ReadUShort(rec) != Items.bigbang) return;             // not the blade we just spent

            float factor = WhpHits;
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
            if (st.crushing) { GuardBreak.Drive(false); st.crushing = false; }
            st.chargeAction = 0;
        }
    }
}
