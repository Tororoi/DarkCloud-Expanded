using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sun Sword — every enemy killed has a chance to drop a Sun attachment (Big Bang inherits), and a guard-charged
    /// blade whose next swing blinds the room (Solar Flash; helpers in SunSword/).</summary>
    internal static class SunSword
    {
        private static readonly Random random = new Random();

        // ── Sun Sword "Solar Flash" ────────────────────────────────────────────────────────
        private const int    TickMs               = 30;
        private const double ChargeSeconds        = 1.5;    // guard held this long primes the blade
        internal const float FlashRadius          = 300f;   // the hit and the blinding reach this far from Toan
        private const float  FlashDamageFraction  = 0.25f;  // the hit's base damage, as a fraction of the weapon's attack
        private const float  KickStrength = 2.0f, KickDecay = 0.3f;   // the hit's shove, sized like Toan's heavier combo hits
        private const int    KickTypeMelee        = 2;      // +0x98: the melee-style reaction (flinch + shove)
        private const int    HitLifeTicks         = 3;      // the planted spheres are withdrawn after this many ticks
        private const float  PerEnemyRadius       = 25f;    // each enemy's own sphere: centred on it, so overlap is certain
        internal const float FlashPulseSpeed      = 90f;    // Toan's own white pulse at the flash (the change effect's rate)
        private  const double PrimedSeconds       = 10.0;   // a charge left unused this long dissipates
        private  const double DissipateSeconds    = 0.5;    // …fading the tint and shrinking the glow away
        internal const float  FlashWhp            = 5f;     // weapon HP the flash-bang costs (Sun Sword, and Big Bang not locked on), before Endurance
        internal const double BlindSeconds        = 5.0;    // how long a flash holds the floor (a profile may take its own: the Sword of Zeus's bolts)

        private static DateTime _blindUntil;
        /// <summary>How much of the flash's hold is left; 0 when none is running.</summary>
        internal static double BlindSecondsLeft => _blindUntil == default ? 0 : Math.Max(0, (_blindUntil - GameClock.Now).TotalSeconds);
        private const float  PrimedTint           = 45f;    // the slight white Toan keeps while the charge is held, per channel (the tint is an ambient ADD)
        private const ushort FlashSe              = 0;      // sound effect at the flash (SeSeq id; 0 = none)
        internal const float SwingCursorPerFrame = 0.3f;   // the combo clips' KEY step: what the frame cursor moves per engine frame
        private const float  Combo1Start = 820f;           // where the first combo clip begins: the judgement blade's fall is paced from here
        private const float  Combo1Hit = 826f, Combo2Hit = 835f, Combo3Hit = 843f, Combo4Hit = 852f, Combo5Hit = 867f;   // frame cursor at which each combo swing comes forward (docs/character-motion-table.md clips 37: 820-830, 38: 830-838, 39: 838-847, 40: 847-857, 41: 856-884)

        /// <summary>What differs between the swords that carry Solar Flash. The ability itself — the charge, the
        /// blinding, the script hold — is identical; each weapon brings its own damage share, glow disc and blade
        /// frames. Big Bang inherits the ability at twice the Sun Sword's share, as the later sword in the line.</summary>
        internal sealed class SolarProfile
        {
            internal readonly ushort WeaponId;
            internal readonly float  DamageFraction;
            internal readonly string Glow, Model, Tag;   // Glow null = this sword carries no glow disc
            internal readonly uint   Frame, Unlit;
            internal readonly float  Fog;              // how much of the fog wash the flash does (SolarLighting.FogAmount)
            internal readonly float[] Light, FogRgb;   // the colours the light and the fog are driven to
            internal readonly float  PrimeDim;         // how far the scene darkens while the charge builds and holds (0 = not at all)
            internal readonly bool   BladeGlowOnly;    // the glow belongs to the judgement blade alone — never on Toan
            internal readonly double EaseSeconds;      // how long the wash takes to recede (0 = as long as the blinding)
            internal readonly float  RestDim;          // the dim the flash recedes ONTO and holds through the blinding (0 = none)
            internal readonly double RestRelease;      // over how many of the blinding's last seconds that dim lifts
            internal readonly bool   HoldsPrimedTint;  // Toan keeps a slight white while the charge is held (the cyan build-up and the flash pulse are every sword's)
            internal readonly float[] BladeWhite;      // the blade's RGB add at full charge (SolarBlade.White unless a sword asks for its own)
            internal readonly double BlindSeconds;     // how long this sword's flash holds the floor
            internal readonly float  FlashWhp;         // weapon HP the flash itself costs, before Endurance (0 = the sword pays another way: Zeus's bolts)
            internal SolarProfile(ushort id, float dmg, string glow, string model, uint frame, uint unlit, string tag,
                                  float fog = 1f, float[] light = null, float[] fogRgb = null, float primeDim = 0f, bool bladeGlowOnly = false,
                                  double easeSeconds = 0, float restDim = 0f, double restRelease = 1.0, bool holdsPrimedTint = true,
                                  float[] bladeWhite = null, double blindSeconds = SunSword.BlindSeconds, float flashWhp = 0f)
            {
                WeaponId = id; DamageFraction = dmg; Glow = glow; Model = model; Frame = frame; Unlit = unlit; Tag = tag; Fog = fog;
                Light = light ?? SolarLighting.SunLight; FogRgb = fogRgb ?? SolarLighting.SunFog; PrimeDim = primeDim; BladeGlowOnly = bladeGlowOnly;
                BlindSeconds = blindSeconds; FlashWhp = flashWhp;
                EaseSeconds = easeSeconds > 0 ? easeSeconds : blindSeconds; RestDim = restDim; RestRelease = restRelease;
                HoldsPrimedTint = holdsPrimedTint; BladeWhite = bladeWhite ?? SolarBlade.White;
            }
            /// <summary>Hand the lighting this sword's wash: how much fog, what colour the light and fog go, how long
            /// the white takes to recede, and what it recedes onto for the rest of the blinding.</summary>
            internal void ArmLighting()
            {
                SolarLighting.FogAmount = Fog; SolarLighting.FlashColour = Light; SolarLighting.FogColour = FogRgb;
                SolarLighting.EaseSeconds = EaseSeconds; SolarLighting.RestDim = RestDim;
                SolarLighting.RestSeconds = BlindSeconds; SolarLighting.RestRelease = RestRelease;   // this sword's own blinding
            }
        }

        internal static readonly SolarProfile SunSwordFlash = new SolarProfile(
            Items.sunsword, 0.25f, ToanGlowBakes.GlowName, SolarBlade.SunSwordModel, 0, 0, "SunSword",
            primeDim: 0.35f,                                                         // the room darkens as the blade brightens, as it does for Big Bang (the enemies' white with it)
            flashWhp: FlashWhp);
        internal static readonly SolarProfile BigBangFlash = new SolarProfile(
            Items.bigbang, 0.50f, ToanGlowBakes.BlueName, SolarBlade.BigBangModel,
            SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame, "BigBang", fog: 0.8f,
            light: new[] { 236f, 226f, 255f }, fogRgb: new[] { 242f, 236f, 255f },   // a cool white, toward pale violet
            primeDim: 0.35f,                                                         // the room darkens as the blade brightens; the drop takes it the rest of the way
            bladeGlowOnly: true,                                                     // the glow appears with the judgement blade and goes with it
            flashWhp: FlashWhp);                                                     // the flash alone (not locked on); a dropped blade's blast pays its own
        /// <summary>The Sword of Zeus: Big Bang's charge look (dim, cool light) with NO glow disc, a softer blade white
        /// and no white on Toan while primed (he keeps the cyan build-up and the flash pulse). Its flash does NO damage of
        /// its own: the bolts and their blasts do (SwordOfZeus.Strike). Locked on, the primed combo brings a bolt down
        /// on the target with EVERY swing; not locked on, one swing brings a bolt down on each of the nearest enemies.</summary>
        internal static readonly SolarProfile ZeusFlash = new SolarProfile(
            Items.swordofzeus, 0f, null, "c01w39", 0, 0, "Zeus", fog: 0.8f,
            light: new[] { 228f, 240f, 255f }, fogRgb: new[] { 238f, 246f, 255f },   // an electric white, toward blue
            primeDim: 0.5f,                                                          // darker than Big Bang's 0.35 (k is darkness: 1 = full dim)
            easeSeconds: 2.0,                                                        // a strike's flash, gone in two seconds — back to the floor's own light, not onto a rest dim
            holdsPrimedTint: false);                                                 // its flash costs nothing — each bolt does (StrikeWhp)

        /// <summary>True while a Solar Flash charge is building, held or going off — Big Bang's own charge-attack tint
        /// stands aside for it rather than fighting it for the blade.</summary>
        internal static bool FlashArmed { get; private set; }

        private enum Phase { Idle, Charging, Primed, Windup, Dropping, Dissipating, Chain }
        private static SolarState   _live;        // the running flash's state, for the questions below
        private static SolarProfile _liveProfile;
        /// <summary>Is <paramref name="weaponId"/>'s Solar Flash currently PRIMED (charge held, swing not yet made)? Big
        /// Bang hangs its judgement blade over the lock-on target during exactly this.</summary>
        /// <summary>The running flash's phase and weapon, for diagnostics.</summary>
        internal static string LivePhase => _live == null ? "none" : $"{_live.phase}/{_liveProfile?.WeaponId}";
        internal static bool PrimedFor(ushort weaponId) =>
            _live != null && _liveProfile != null && _liveProfile.WeaponId == weaponId
            && (_live.phase == Phase.Primed || _live.phase == Phase.Windup || _live.phase == Phase.Dropping
                || (_live.phase == Phase.Chain && !_live.chainDropped));   // a chain whose blade has fallen hangs no second one
        private sealed class SolarState
        {
            public Phase phase;
            public DateTime holdStart, primedAt, dissipateAt;
            public byte floor = 0xFF;
            public int errors;                                  // tick exceptions logged so far (the first few carry a stack)
            public int  chainAction;                            // the combo swing the chain last struck on …
            public bool chainFired;                             // … and whether this swing's bolt has gone
            public bool chainDropped;                           // the judgement blade was let go on the last swing: its landing is the bolt

            public readonly List<(int slot, int ticks)> planted = new List<(int, int)>();
        }

        /// <summary>
        /// Ability Name: Solar Flash (Sun Sword)
        /// Hold guard and the blade whitens over <see cref="ChargeSeconds"/>; at full it is PRIMED (the charge-complete
        /// pulse marks it) and stays so, guard or not. The next attack carries the charge: as the swing comes forward the
        /// blade returns to its own colour and the dungeon flashes blinding white, easing back over a second
        /// (<see cref="SolarLighting"/>). Every enemy within <see cref="FlashRadius"/> takes a light hit — a quarter of the
        /// weapon's attack through the normal formula, with the sword's element and a melee stagger — and is then blinded
        /// for <see cref="BlindSeconds"/>: its OWN script holds its guard and cancels its movement, frame by frame
        /// (<see cref="SolarScript"/>). The blade tint is <see cref="SolarBlade"/>. Dungeon only; a sidekick out or a floor
        /// change drops the charge.
        /// </summary>
        public static void SolarFlashEffect(SolarProfile p)
        {
            var st = new SolarState();
            _live = st; _liveProfile = p;
            while (Player.Weapon.GetCurrentWeaponId() == p.WeaponId && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try { SolarTick(st, p); }
                catch (Exception ex)
                {
                    if (st.errors++ < 3) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[{p.Tag}] Solar Flash tick error: " + ex.Message + "\n" + ex.StackTrace);
                }
            }
            SolarReset(st);
        }

        private static void SolarTick(SolarState st, SolarProfile p)
        {
            FlashArmed = st.phase != Phase.Idle;
            SolarLighting.ToanTintOwned = FlashArmed && p.HoldsPrimedTint;   // his tint is the charge's while it is held (the dark's white stays off him)
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) SolarReset(st); st.floor = floor; }

            SolarLighting.Tick();
            SolarBlind();
            ExpireHits(st);
            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId)
            {
                if (st.phase != Phase.Idle) { SolarBlade.Clear(); ChargeTint.Clear(); SolarLighting.EndDim(); st.phase = Phase.Idle; }
                return;
            }

            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            switch (st.phase)
            {
                case Phase.Idle:
                    // One flash at a time: no re-charging until the one on the floor has run its course.
                    if (_blindUntil != default) break;
                    if (GuardWatch.IsGuarding()) { st.phase = Phase.Charging; st.holdStart = GameClock.Now; }
                    break;

                case Phase.Charging:
                {
                    // One flash at a time, and that includes a charge already in flight when the last one went off.
                    if (_blindUntil != default) { st.phase = Phase.Idle; SolarBlade.Set(0f, p.Model, p.Frame, p.Unlit, p.BladeWhite); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.EndDim(); break; }
                    // Guard released before it primed: the glow goes with it rather than lingering.
                    if (!GuardWatch.IsGuarding()) { st.phase = Phase.Idle; SolarBlade.Set(0f, p.Model, p.Frame, p.Unlit, p.BladeWhite); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.EndDim(); break; }
                    double held = (GameClock.Now - st.holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / ChargeSeconds), p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    // The room darkens as the blade brightens, to PrimeDim at the moment it primes.
                    if (p.PrimeDim > 0f) { SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim * (float)Math.Min(1.0, held / ChargeSeconds)); }
                    ChargeTint.Ramp(ChargeSeconds - held);
                    // The glow LEADS the charge: started a grow-time early, it reaches full size exactly as it primes.
                    if (p.Glow != null && !p.BladeGlowOnly && held >= ChargeSeconds - SolarGlow.GrowSeconds) { SolarGlow.Show(p.Glow); SolarGlow.Tick(); }
                    if (held >= ChargeSeconds)
                    {
                        st.phase = Phase.Primed; st.primedAt = GameClock.Now;
                        ChargeTint.Clear();                                  // the cyan build-up ends; the white hold below takes over
                        if (p.Glow != null && !p.BladeGlowOnly) SolarGlow.Show(p.Glow);   // …and Toan takes a glow of his own
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[{p.Tag}] Solar Flash primed");
                    }
                    break;
                }

                case Phase.Primed:
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);                                   // re-asserted each tick: a rebuilt model gets it back
                    if (p.PrimeDim > 0f) { SolarLighting.BeginDim(); SolarLighting.DimTo(p.PrimeDim); }   // held (one write; a floor change re-captures)
                    if (p.Glow != null && !p.BladeGlowOnly) SolarGlow.Show(p.Glow);   // re-asserted each tick: Show's KeepAlive is what keeps the disc uploaded
                    SolarGlow.Tick();
                    HoldPrimedTint(p, 1f);
                    if (IsAttack(action)) { st.phase = Phase.Windup; break; }
                    if ((GameClock.Now - st.primedAt).TotalSeconds >= PrimedSeconds)
                    {
                        st.phase = Phase.Dissipating; st.dissipateAt = GameClock.Now;
                        SolarGlow.Fade();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[{p.Tag}] charge went unused — dissipating");
                    }
                    break;

                case Phase.Dissipating:
                {
                    // The charge lapses: the white bleeds out of Toan as the glow shrinks away.
                    double t = (GameClock.Now - st.dissipateAt).TotalSeconds / DissipateSeconds;
                    SolarGlow.Tick();
                    if (t >= 1.0) { SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Hide(); SolarLighting.EndDim(); st.phase = Phase.Idle; }
                    else { SolarBlade.Set((float)(1.0 - t), p.Model, p.Frame, p.Unlit, p.BladeWhite); HoldPrimedTint(p, (float)(1.0 - t)); SolarLighting.DimTo(p.PrimeDim * (float)(1.0 - t)); }
                    break;
                }

                case Phase.Windup:
                {
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    SolarGlow.Tick();
                    HoldPrimedTint(p, 1f);
                    if (!IsAttack(action)) { st.phase = Phase.Primed; break; }     // the swing was cancelled: still primed
                    // ⚠ Paced from the CLIP'S START, not from the cursor read now: the action word turns to the swing a
                    // frame before the cursor is reset to the clip, so a read here can still be the idle clip's, a
                    // hundred frames off — that was a two-second fall, and a bolt after the combo was over.
                    if (p.WeaponId == Items.swordofzeus && action == PlayerAction.ActionComboFirst && PlayerAction.LockHeld(out _)
                        && BigBang.BeginDrop(Combo1Start, ComboHitFrame(action)))
                    { st.phase = Phase.Chain; st.chainAction = action; st.chainFired = true; st.chainDropped = true; break; }
                    bool forward = action == PlayerAction.ActionWhirlwind || action == PlayerAction.ActionLunge
                                || Memory.ReadFloat(PlayerAction.AnimFrameCursor) >= ComboHitFrame(action);
                    if (!forward)
                    {   // the Sword of Zeus: the room plunges to black along the swing, peaking on the bolt
                        if (p.WeaponId == Items.swordofzeus && action >= PlayerAction.ActionComboFirst && action <= PlayerAction.ActionComboLast) SwingDim(p, action);
                        break;
                    }
                    // Big Bang, locked on: the swing does not flash — it lets the judgement blade fall, and the flash
                    // goes off when it lands (BigBang.BeginDrop → Dropping). Not locked on: the flash, as ever.
                    if (p.WeaponId == Items.bigbang && BigBang.BeginDrop()) { st.phase = Phase.Dropping; break; }
                    // The Sword of Zeus. Locked on with the judgement blade hanging: the blade is let go as the FIRST
                    // swing begins (above, before the swing comes forward), paced by the swing so it lands at the
                    // swing's hit frame — the landing is the bolt, and the combo CHAINS from there (Chain). Locked on
                    // without a blade: the bolt at the hit frame, and the chain. Not locked on: one bolt on each of
                    // the nearest enemies in reach, and the charge is spent.
                    if (p.WeaponId == Items.swordofzeus)
                    {
                        if (PlayerAction.LockHeld(out int target) && SwordOfZeus.Strike(target))
                        { Flash(st, p); st.phase = Phase.Chain; st.chainAction = action; st.chainFired = true; st.chainDropped = false; break; }
                        SwordOfZeus.StrikeNearest();
                    }
                    Flash(st, p);
                    st.phase = Phase.Idle;
                    break;
                }

                case Phase.Chain:
                {
                    // The primed combo, locked on: each swing darkens the room through its wind-up and brings the bolt
                    // and the flash down at its hit frame. The combo ending — or the lock let go — spends the charge.
                    // The engine's own combo state is the action word: hits 1-5 are ActionComboFirst..Last in turn, and
                    // it chains straight from one to the next, so the combo is over the tick the word is outside that
                    // range — or the tick it is a hit EARLIER than the one that last struck, or that same hit with its
                    // clip back before the hit frame: a new combo (a first swing again after stopping), not this one
                    // carrying on. (The word stays on a hit's value for the rest of that swing, so "the same hit" alone
                    // is not an ending.)
                    // The judgement blade let go on the first swing (Windup) is in the air here until its landing —
                    // the tip in the enemy at the swing's hit frame — which is the first bolt (the owner's landing),
                    // the flash following from here. The combo cannot end while it is in the air.
                    bool falling = BigBang.Dropping || BigBang.LandingPending;
                    if (st.chainDropped && BigBang.TakeDropLanded()) { Flash(st, p); break; }
                    bool swinging = action >= PlayerAction.ActionComboFirst && action <= PlayerAction.ActionComboLast;
                    bool restarted = swinging && (action < st.chainAction
                                     || (action == st.chainAction && st.chainFired && Memory.ReadFloat(PlayerAction.AnimFrameCursor) < ComboHitFrame(action) - 1f));
                    if (!falling && (!swinging || restarted || !PlayerAction.LockHeld(out _)))
                    {
                        // A wind-up's dim with no bolt behind it would otherwise stay: EndDim lifts it (and leaves a
                        // flash that is still easing or resting to run its own course).
                        SolarBlade.Clear(); ChargeTint.Clear(); SolarLighting.EndDim(); st.phase = Phase.Idle;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[{p.Tag}] combo over (action 0x{action:X} after hit {st.chainAction - PlayerAction.ActionComboFirst + 1}) — the charge is spent");
                        break;
                    }
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);      // still primed through the combo
                    if (!swinging) break;
                    if (action != st.chainAction) { st.chainAction = action; st.chainFired = false; }
                    if (st.chainFired) break;
                    if (Memory.ReadFloat(PlayerAction.AnimFrameCursor) < ComboHitFrame(action))
                    {   // the wind-up: the room plunges to black along the swing, peaking on the bolt (taking the last
                        // flash's ease over; the combo ending with no bolt behind it lifts the dim, above)
                        SwingDim(p, action);
                        break;
                    }
                    st.chainFired = true;
                    if (PlayerAction.LockHeld(out int target) && SwordOfZeus.Strike(target)) Flash(st, p);
                    break;
                }

                case Phase.Dropping:
                {
                    SolarBlade.Set(1f, p.Model, p.Frame, p.Unlit, p.BladeWhite);
                    HoldPrimedTint(p, 1f);
                    SolarGlow.Tick();
                    // The blade has landed (or the drop was abandoned — a lost lock mid-fall): the flash fires either way,
                    // so a spent charge never sits waiting on a visual. A landing has already fired the white-out on
                    // the burst's own frame; the abandon has not.
                    if (BigBang.TakeDropLanded())                              { Flash(st, p, lit: true); st.phase = Phase.Idle; }
                    else if (!BigBang.Dropping && !BigBang.LandingPending)     { Flash(st, p);            st.phase = Phase.Idle; }
                    break;
                }
            }
        }

        /// <summary>The slight white Toan carries while the charge is held: the same ambient-add field the charge ramp uses,
        /// re-asserted each tick so a status tint or a character swap cannot leave it stuck on. Not for a sword whose
        /// profile leaves Toan plain while primed (the Sword of Zeus).</summary>
        private static void HoldPrimedTint(SolarProfile p, float k)
        {
            if (p.HoldsPrimedTint) Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, PrimedTint * k, PrimedTint * k, PrimedTint * k);
        }

        /// <summary>The blinding's clock. The behaviour itself is the enemies' own scripts (SolarScript); this only decides
        /// when they lower their guard and when they get their AI back.</summary>
        private static void SolarBlind()
        {
            if (_blindUntil == default) return;
            double left = (_blindUntil - GameClock.Now).TotalSeconds;
            if (left <= 0) { SolarScript.End(); GuardBreak.Drive(false); _blindUntil = default; }
            else
            {
                // Blinded enemies hold a guard and it really does block, so the flash breaks it for as long as it lasts:
                // a hit lands, their own hit reaction staggers them, and returning from it drops them back into the guard.
                GuardBreak.Drive(true);
                SolarScript.Wake(left);               // each species winds down on its own clip's clock
            }
        }

        private static bool IsAttack(int action) =>
            (action >= PlayerAction.ActionComboFirst && action <= PlayerAction.ActionComboLast)
            || action == PlayerAction.ActionLunge || action == PlayerAction.ActionWhirlwind;

        /// <summary>The darkening before a bolt: the sword's prime dim until the last SolarLighting.RampFrames of the
        /// swing, then to black along them, peaking at the hit frame (on the swing's frame cursor).</summary>
        private static void SwingDim(SolarProfile p, int action)
        {
            float window = SolarLighting.RampFrames * SwingCursorPerFrame, hit = ComboHitFrame(action);
            SolarLighting.DimRamp(p.PrimeDim, (Memory.ReadFloat(PlayerAction.AnimFrameCursor) - (hit - window)) / window);
        }
        private static float ComboHitFrame(int action) => action switch
        {
            PlayerAction.ActionComboFirst     => Combo1Hit,
            PlayerAction.ActionComboFirst + 1 => Combo2Hit,
            PlayerAction.ActionComboFirst + 2 => Combo3Hit,
            PlayerAction.ActionComboFirst + 3 => Combo4Hit,
            _                                 => Combo5Hit,
        };

        /// <summary>The flash itself: blade back to normal, the light to white, Toan's pulse, the hit (for a sword whose
        /// flash carries one), the blinding. <paramref name="lit"/>: the white-out has already been fired (Big Bang's
        /// landing does it with the burst).</summary>
        private static void Flash(SolarState st, SolarProfile p, bool lit = false)
        {
            // The light hit comes from the flash's own point: Toan, or the blast — the judgement blade's landing — when
            // that is what struck, so the kick throws everyone from it and the hit turns them to it, not to him.
            float px, ph, py;
            if (lit) (px, ph, py) = BigBang.LastBlast;
            else { px = Memory.ReadFloat(Addresses.dunPositionX); ph = Memory.ReadFloat(Addresses.dunPositionZ); py = Memory.ReadFloat(Addresses.dunPositionY); }
            SolarBlade.Clear();                                          // tint off, and the blade's own palette back
            ChargeTint.Clear();                                          // …and the white Toan was holding
            SolarGlow.Hide();
            if (!lit) { p.ArmLighting(); SolarLighting.Flash(); }
            Player.FlashActiveCharacter(p.Light[0], p.Light[1], p.Light[2], FlashPulseSpeed, 1);
            if (FlashSe != 0) SeSeq.Play(FlashSe, 90);
            if (p.DamageFraction > 0f) PlantFlashHit(st, px, ph, py, p);
            if (!lit && p.FlashWhp > 0f) WeaponWhp.Drain(p.WeaponId, p.FlashWhp, "[" + p.Tag + "] flash ");   // a blast's landing paid for itself
            Blind(p.BlindSeconds);
        }
        /// <summary>The blinding: every enemy's own script holding its guard for <paramref name="seconds"/> from now — a
        /// flash inside a blinding sends whoever had begun lowering their guard back to it and restarts the clock.</summary>
        private static void Blind(double seconds)
        {
            SolarScript.Rehold();          // a flash inside a blinding: whoever had begun lowering their guard raises it again
            SolarScript.Begin();           // the enemies' OWN scripts hold the guard from here
            _blindUntil = GameClock.Now.AddSeconds(seconds);        // …for the full time from THIS flash
        }
        /// <summary>A strike's flash outside the Solar Flash phases — the Sword of Zeus's charge attack: the white-out
        /// onto the sword's rest dim (none for Zeus: its ease goes back to normal light), Toan's pulse, and the blinding, exactly as a primed strike has them. (Its
        /// profile carries no light hit, so none is planted.)</summary>
        internal static void StrikeFlash(SolarProfile p)
        {
            p.ArmLighting(); SolarLighting.Flash();
            Player.FlashActiveCharacter(p.Light[0], p.Light[1], p.Light[2], FlashPulseSpeed, 1);
            if (FlashSe != 0) SeSeq.Play(FlashSe, 90);
            Blind(p.BlindSeconds);
        }

        /// <summary>A player-attack sphere ON EACH ENEMY in range (CollisionPool: the same entries CheckDmg tests his sword
        /// swings against): base = attack × <see cref="FlashDamageFraction"/>, the sword's selected element as a pure bit, and
        /// a melee kick originating at Toan so each one is shoved outward from him.
        ///
        /// ⚠ NOT one big sphere. An entry is CONSUMED by the first victim the engine matches it against, so a single
        /// 300-unit sphere damaged exactly one enemy and left the rest untouched — which looked like "one per species"
        /// because a species tends to be clustered. One small sphere centred on each enemy hits all of them, and the pool
        /// holds 96 entries against at most 16 enemies.</summary>
        private static void PlantFlashHit(SolarState st, float x, float h, float y, SolarProfile p)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            float attack = Memory.ReadShort(WeaponHave.BattleWeaponRecord + 0x04);
            int baseDmg = Math.Max(1, (int)Math.Round(attack * p.DamageFraction));
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;
            int hit = 0, missed = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                float eh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ));
                if (Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) > FlashRadius) continue;
                int slot = CollisionPool.TakeFreeSlot(pool);
                if (slot < 0) { missed++; continue; }
                byte[] e = CollisionPool.PlayerHitEntry(ex, eh, ey, PerEnemyRadius, baseDmg, attr);
                void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
                F(0x80, x); F(0x84, h); F(0x88, y);                    // kick origin stays Toan: everyone is shoved AWAY from him
                F(0x90, KickStrength); F(0x94, KickDecay);
                BitConverter.GetBytes(KickTypeMelee).CopyTo(e, 0x98);
                CollisionPool.Plant(pool, slot, e);
                st.planted.Add((slot, HitLifeTicks));
                hit++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[{p.Tag}] flash at ({x:F0},{h:F0},{y:F0}) r={FlashRadius:F0}: {hit} enemies struck for base {baseDmg}, attr 0x{attr:X}"
                + (missed > 0 ? $" ({missed} missed — pool full)" : ""));
        }

        /// <summary>The engine withdraws its own swing spheres when the swing ends; ours is withdrawn here.</summary>
        private static void ExpireHits(SolarState st)
        {
            if (st.planted.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = st.planted.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = st.planted[i];
                if (--ticks > 0) { st.planted[i] = (slot, ticks); continue; }
                if (pool != 0) CollisionPool.Deactivate(pool, slot);
                st.planted.RemoveAt(i);
            }
        }

        private static void SolarReset(SolarState st)
        {
            SolarBlade.Clear();
            SolarGlow.Hide();
            ChargeTint.Clear();
            SolarLighting.Restore();
            SolarScript.End(); GuardBreak.Drive(false); _blindUntil = default; FlashArmed = false;
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in st.planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            st.planted.Clear();
            st.phase = Phase.Idle;
        }

        // ── Sun Sword "Solar Harvest" ──────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Solar Harvest (Sun Sword, Big Bang)
        /// While the Sun Sword — or its evolution, Big Bang — is wielded, each enemy on the floor
        /// has a 1% chance (rolled once per slot per floor) to drop a Sun attachment instead of
        /// its regular drop when killed. (Big Bang shares this effect for lineage/visual-design
        /// reasons; it will additionally get its own unique effect later.)
        /// Implemented by pre-staging the engine's guaranteed-drop field (<see
        /// cref="EnemySlotOffsets.ForceItemDrop"/> — the same mechanism dungeon keys and miniboss
        /// loot use, consumed by the death-drop block in CMonstorUnit::Step, ELF 0x1DF4C0) while
        /// the sword is in hand, and un-staging when it isn't (unequip, character switch), so
        /// there is no race against the killing blow. Slots already carrying a forced drop
        /// (dungeon key, miniboss loot, mimic key) are never touched. Engine caveat: the drop
        /// path runs items through a small de-dupe set, so a second Sun proc on the same floor
        /// may be swallowed.
        /// </summary>
        public static void SolarHarvestEffect()
        {
            var st = new SunHarvestState(EnemyAddresses.FloorSlots.Count);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (equippedId != Items.sunsword && equippedId != Items.bigbang)
                    break;

                // Kills while a sidekick is out aren't Sun Sword kills — winners keep their win
                // (state is preserved) and are re-staged when Toan takes over again.
                SunHarvestDrive(Player.CurrentCharacterNum() == Player.ToanId, st);
                Thread.Sleep(250);
            }
            SunHarvestDrive(false, st);   // unequipped / left the floor: revert anything still staged
        }

        // Solar Harvest core — shared with Super Steve (an attached Sun Sword / Big Bang sphere).
        // Per-caller state (no shared statics) so the Toan thread and the Xiao thread never fight
        // over the staged slots. `active` = the Sun wielder is currently the acting character.
        internal sealed class SunHarvestState
        {
            public byte LastFloor = 0xFF;
            public readonly bool[] Rolled, Winner, Staged;
            public readonly ushort[] OriginalDrop;
            public SunHarvestState(int n)
            { Rolled = new bool[n]; Winner = new bool[n]; Staged = new bool[n]; OriginalDrop = new ushort[n]; }
        }

        internal static void SunHarvestDrive(bool active, SunHarvestState st)
        {
            const int procPercent = 1;
            int slotCount = EnemyAddresses.FloorSlots.Count;

            byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
            if (currentFloor != st.LastFloor)
            {
                // New floor: the slot array was reinitialized, so forget everything WITHOUT
                // restoring (writing stale values into fresh slots would corrupt them).
                st.LastFloor = currentFloor;
                Array.Clear(st.Rolled, 0, slotCount);
                Array.Clear(st.Winner, 0, slotCount);
                Array.Clear(st.Staged, 0, slotCount);
            }

            if (!active) { SunUnstageAll(st); return; }

            for (int i = 0; i < slotCount; i++)
            {
                if (Enemies.GetFloorEnemyId(i) == 0) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) <= 0) continue;

                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                ushort dropVal = Memory.ReadUShort(dropAddr);

                if (!st.Rolled[i])
                {
                    st.Rolled[i] = true;
                    // Slots that already carry a forced drop (key/miniboss/mimic) are off-limits
                    if (dropVal != 0 && dropVal != 65535) continue;
                    st.Winner[i] = random.Next(100) < procPercent;
                    if (st.Winner[i]) st.OriginalDrop[i] = dropVal;
                }
                if (!st.Winner[i] || st.Staged[i]) continue;

                // Re-check occupancy — a key could have been assigned here after our roll
                if (dropVal != 0 && dropVal != 65535 && dropVal != Items.sun)
                { st.Winner[i] = false; continue; }
                Memory.WriteUShort(dropAddr, (ushort)Items.sun);
                st.Staged[i] = true;
            }
        }

        private static void SunUnstageAll(SunHarvestState st)
        {
            for (int i = 0; i < st.Staged.Length; i++)
            {
                if (!st.Staged[i]) continue;
                st.Staged[i] = false;
                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                if (Memory.ReadUShort(dropAddr) == Items.sun)   // still ours → restore
                    Memory.WriteUShort(dropAddr, st.OriginalDrop[i]);
            }
        }
    }
}
