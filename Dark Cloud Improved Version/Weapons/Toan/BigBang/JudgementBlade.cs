using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The judgement blade (docs/big-bang.md): a copy of a model (<see cref="BladeProp"/>) hung point-down over
    /// the locked-on target for whichever owner holds it (<see cref="JudgementOwner"/> — Big Bang, the Sword of Zeus, the
    /// Big Bang shot), fading in with its glow, then let go to fall under the blade-fall cave's gravity and land where the
    /// owner's callback fires. Two hovers share the one copy: the lock-on hover (JudgementTick / BeginDrop) and the point
    /// hover an owner places itself (PointHover … PointEnd). The fall is stepped by the engine, watched here at frame rate
    /// for the dim and the landing; ReleaseJudgement takes everything down.</summary>
    internal static class JudgementBlade
    {
        // ── the judgement blade ─────────────────────────────────────────────────────────────
        // While the owner is primed AND Toan is locked on, the copy (BladeProp) hangs point-down over the target at
        // OwnerScale(), fading in over FadeSeconds; the owner's glow moves onto it and the target's NAME plate is hidden
        // (CodeCaves.NameHide, the gate the plate's getter ANDs in — the enemy itself is never touched, so a kill during
        // the hover still counts for whatever counts kills). Losing the lock fades it out and the glow shrinks off it and
        // swells back up on Toan. BeginDrop lets it FALL; where it lands the owner's Land fires (Big Bang: the flash, the
        // blast, every enemy on the floor turned to face it, the weapon-HP bill).
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
        // t = √(2·h0/g). 500 u/s² lands the tip on a 12 u enemy in ~0.27 s, a 25 u one in ~0.35 s, a 40 u miniboss in
        // ~0.43 s. The engine steps the fall once a frame (the blade-fall cave, StartEngineFall); BladeLoop watches it
        // for the dim and the landing, and the landing itself is called from the tick.
        private const double Gravity          = 500.0;   // units/s²
        private const int    FallTickMs       = 2;      // BladeLoop's cadence; for a point hover placed over a spot from it (a frame is ~16.7 ms) the phase between
                                                          // a placement and the frame that samples it is what reads as jitter — the shorter the period, the smaller it is
        // As the blade falls the floor's light and fog are driven DOWN (SceneLighting.Dim) on an EXPONENTIAL ramp
        // that peaks at the landing itself: k = (e^(a·u) − 1) / (e^a − 1) over the fall's fraction u, so it barely
        // moves at first and plunges in the last moments, with the flash then landing from the darkest frame.
        // The sharpness is SceneLighting.RampSharpness — higher holds the light longer and drops it later. Driven from
        // the fall thread (the same curve as the fall) and handed to the flash at the landing.
        private const double FlashDelay       = 0.0;     // seconds the flash waits after the burst: the same tick, never before it

        /// <summary>Who the judgement blade is hanging for — Big Bang's BigBangOwner, the Sword of Zeus's SwordOfZeus.Judgement,
        /// the Big Bang shot's Owner: the weapon whose primed state hangs it, the glow disc it carries, the profile whose
        /// prime dim the fall darkens from, whether every enemy is turned to watch it fall, and what happens where it lands.</summary>
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
            internal Func<float> Alpha;                       // a ceiling on the copy's fade-in, 0..1 — a copy hung while its charge still builds fades in with it (FadeSeconds alone otherwise)
            internal float       Scale;                       // the copy's scale (OwnerScale() otherwise)
            internal Func<float> Length;                      // the model's reach below its root at 1× — how far above the ground it stops (the blade's dcol1 otherwise)
            internal Func<uint>  SpawnRoot;                   // the model to copy (the equipped weapon otherwise)
            internal bool        Upright;                     // copied as authored rather than turned point-down
            internal float[]     Tint;                        // the copy's ambient add, per channel
            internal int         GlowRow;                     // the glow cave's palette row for the disc (0 = the disc's own)
            internal float       GlowScale;                   // the disc's size (0 = Toan's)
            internal float       GlowLift = float.NaN;        // the disc's height over the copy's root (NaN = half the length below it)
            internal float       Margin;                      // how far the copy's lowest point hangs above the species' authored height (0 = HoverMargin)
        }
        private static float OwnerMargin() => _owner.Margin > 0f ? _owner.Margin : HoverMargin;
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
        private static JudgementOwner _owner = BigBang.BigBangOwner;
        /// <summary>The hover and the fall, ticked for <paramref name="owner"/> from its own loop, every tick.</summary>
        internal static void JudgementTick(JudgementOwner owner) { _owner = owner; JudgementTick(); }
        /// <summary>Everything the judgement blade put up, down (a sword put away, a floor left).</summary>
        internal static void ReleaseJudgement()
        {
            AbandonHover(); Dropping = false; _landed = false; _fallDone = false; _landedAt = default; SceneLighting.EndDim(); GlowOwned = false;
        }

        internal static bool GlowOwned { get; private set; }   // the blue glow is on the blade copy, not on Toan
        internal static bool Dropping  { get; private set; }
        private static bool   _landed;
        private static int    _hoverSlot = -1;                 // the enemy the blade hangs over (−1 = none up)
        private static float  _hoverAlpha;                     // 0..1, the fade
        /// <summary>The hanging copy's fade, 0..1 (1 = fully in) — for an owner that paints it as it appears.</summary>
        internal static float HoverAlpha => _hoverAlpha;
        private static bool   _hoverOut;                       // fading OUT (lock lost) — no re-placement
        private static DateTime _lockLostAt;                   // when the lock last left the hovered enemy (default = on it)
        private const double  LockGrace = 0.35;                // seconds a hover rides out a lock that blinks or wanders before it fades
        /// <summary>A copy hangs over a live enemy, ready to drop (what BeginDrop needs).</summary>
        internal static bool HoverReady => _hoverSlot >= 0 && !_hoverOut && BladeProp.Active && EnemyBody.HasHp(_hoverSlot);
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

        /// <summary>The hovering blade, every tick Toan is out. Primed and locked: up over the target, fading in;
        /// the lock gone: fading out and down; a drop in flight: falling, and landing.</summary>
        private static void JudgementTick()
        {
            // "Locked on" is the lock button's own toggle (PlayerAction.LockOnHeld): the slot word alone is only the
            // nearest CANDIDATE, re-picked every frame the toggle is down, and the cursor word stays raised over it
            // after a release.
            // Liveness by HP alone (HasHp): the hover must never depend on anything it changes itself.
            double dt     = BigBang.TickMs / 1000.0;
            if (_pointHover) { PointTick(dt); return; }
            bool locked   = PlayerAction.LockHeld(out int lockSlot)
                            && lockSlot < EnemyAddresses.FloorSlots.Count && EnemyBody.HasHp(lockSlot);
            bool primed   = Primed();
            // A lock that flickers off, or wanders to another enemy, for less than LockGrace keeps the hover on its enemy: the
            // lock-on words blink as the target is re-acquired (and there is no re-placement while fading).
            if (_hoverSlot >= 0 && !_hoverOut && primed && EnemyBody.HasHp(_hoverSlot) && (!locked || lockSlot != _hoverSlot))
            {
                if (_lockLostAt == default) _lockLostAt = GameClock.Now;
                if ((GameClock.Now - _lockLostAt).TotalSeconds < LockGrace) { locked = true; lockSlot = _hoverSlot; }
            }
            else _lockLostAt = default;
            // DIAGNOSTIC: the gates, once a second while primed — a hover that never appears is one of these reading
            // something other than what the notes say.
            if (DebugDiagnostics.Enabled && primed && (GameClock.Now - _gateLog).TotalSeconds >= 1.0)
            {
                _gateLog = GameClock.Now;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[JudgementBlade] hover gates: primed {primed} ({SunSword.LivePhase}), LockOnActive {Memory.ReadInt(PlayerAction.LockOnActive)}, "
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
                if (!BladeProp.Maintain() || !EnemyBody.HasHp(_hoverSlot) || swingLost) { AbandonHover(); Dropping = false; SceneLighting.EndDim(); return; }
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
                // the engine every frame the target is on screen, so the gate is written, not the flag.
                Memory.WriteInt(CodeCaves.NameHide, 1);
                if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[JudgementBlade] hover: the copy did not maintain — down"); AbandonHover(); return; }
                // PINNED to the target's model root: the engine chains the copy's world through the enemy's every
                // frame, so it rides a moving enemy with no placement writes at all — no tick, no thread, no jitter.
                // Its height above the root is measured ONCE, as the hover begins (the enemy at rest), from the unit's
                // own WORLD height (the slot's LocationZ is floor-relative).
                // ⚠ Units of one SPECIES share one model tree: the root above is posed for whichever unit the engine
                // drew last, so a pin to it follows the wrong enemy whenever another of its kind is on the floor.
                // The pin is used only when this unit is the root's sole live user; otherwise the blade cave FOLLOWS
                // the unit's own position (CharObjects.PosAddr — per slot, height included: a flyer takes it up) every
                // frame, at the same height over it.
                uint enemyRoot = Memory.ReadGuestPtr(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharModel);
                float unitH = EnemyBody.UnitHeight(lockSlot);
                if (_heightSlot != lockSlot) { _heightSlot = lockSlot; _hoverHeight = HoverHeightFor(lockSlot, unitH); }
                if (EnemyBody.RootShared(enemyRoot, lockSlot))
                {
                    if (BladeProp.PinnedTo != 0) BladeProp.Unpin(EnemyFacing.PlayerFacing());
                    _followSlot = lockSlot;
                    EngineFollow(lockSlot, _hoverHeight);                          // the cave places it from here on, every frame
                    BladeProp.Orient(EnemyFacing.PlayerFacing());                // its flat the way the fallen blade will face
                }
                else
                {
                    _followSlot = -1;
                    if (BladeProp.PinnedTo != enemyRoot) BladeProp.Pin(enemyRoot, _hoverHeight);
                    // Its flat faces the way the fallen blade will (PlayerFacing): the parent's yaw is taken back out.
                    BladeProp.Face(EnemyFacing.PlayerFacing(), Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(lockSlot) + CCharacter.CharRotY));
                }
                _hoverAlpha = (float)Math.Min(_owner.Alpha?.Invoke() ?? 1f, _hoverAlpha + dt / FadeSeconds);
                BladeProp.Alpha(_hoverAlpha);
                DriveGlow(true);                                                 // the glow crosses to the blade
                if (_owner.Alpha != null) SolarGlow.Drive(_hoverAlpha);          // …and grows with the copy
                if (DebugDiagnostics.Enabled && _hoverTraceTicks < 12)           // DIAGNOSTIC: the first ~third of a second of every hover
                {
                    _hoverTraceTicks++;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[JudgementBlade] hover: " + BladeProp.Where()
                        + $" | height {_hoverHeight:F1}");
                }
                return;
            }

            _hoverTraceTicks = 0;
            if (_hoverSlot >= 0)                                             // up, but no longer wanted: fade out and down
            {
                if (!_hoverOut) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] hover out: primed {primed}, locked {locked} (slot {lockSlot}, held {Memory.ReadInt(PlayerAction.LockOnHeld)}, hp {EnemyBody.HasHp(_hoverSlot)}) — fading");
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
            if (SolarGlow.OnAnchor(want) && (_owner.GlowRow == 0 || SolarGlow.PalRow == _owner.GlowRow)) return;   // up, where it should be, in its colour
            if (SolarGlow.IsUp) SolarGlow.Hide();                               // fading off it, or on something else
            SolarGlow.Show(_owner.Glow, want, float.IsNaN(_owner.GlowLift) ? -OwnerLength() * OwnerScale() / 2f : _owner.GlowLift, FadeSeconds, palRow: _owner.GlowRow, scale: _owner.GlowScale);
        }

        /// <summary>How high above <paramref name="slot"/>'s root to hang the blade's GRIP: the species' authored height
        /// (scaled with the unit) plus <see cref="HoverMargin"/> for the tip, plus the blade's length at the copy's scale.</summary>
        private static float HoverHeightFor(int slot, float rootH)
        {
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            float height = EnemySpecies.Defaults.TryGetValue(eid, out var def) && def.HeightFromRoot.HasValue ? def.HeightFromRoot.Value : HoverFallback;
            float scale = Memory.ReadFloat(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharScale + 4);
            if (!(scale > 0.05f) || scale > 20f) scale = 1f;                                       // a grown miniboss
            float clear = height * scale + OwnerMargin();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] hover over slot {slot} (species {eid}): height {height:F1} × scale {scale:F2} + {OwnerMargin():F0} — tip {clear:F1} above the root");
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
            if (_hoverSlot < 0 || _hoverOut || !BladeProp.Active || !EnemyBody.HasHp(_hoverSlot)) return false;
            _paceFrom = paceFrom; _paceTo = paceTo > paceFrom ? paceTo : 0f; _fallSeconds = 0;
            // The target's own CCharacter position (per slot, what the engine moves and the follow cave reads): the floor-slot
            // record's location fields read 0 for some enemies.
            long up = EnemyAddresses.CharObjects.PosAddr(_hoverSlot);
            _bladeX = Memory.ReadFloat(up);
            _bladeY = Memory.ReadFloat(up + 8);
            _dropH  = EnemyBody.UnitHeight(_hoverSlot);                      // the target's root, wherever it is (a flyer's is up)
            _dropX = _bladeX; _dropY = _bladeY;                              // the blast goes off on the target itself
            // The fall starts from where the blade HANGS — its own world matrix when pinned, the followed height
            // otherwise — so there is no step at the start. It ENDS with the tip at the root (the grip a blade length
            // above it) — the blade in the enemy, not a blade length under the floor — or, for an owner that wants
            // it, the hilt.
            float hang = _followSlot >= 0 ? _dropH + _hoverHeight : BladeProp.WorldHeight();
            _fallStop   = _owner.ToTheHilt ? 0f : OwnerLength() * OwnerScale();   // the grip's height above the root at the end: the tip in the enemy, or the hilt
            _fallHeight = float.IsNaN(hang) ? _hoverHeight : Math.Max(_fallStop + 1f, hang - _dropH);
            _followSlot = -1;
            BladeProp.Unpin(EnemyFacing.PlayerFacing());                     // off the enemy and into the world, where it is, to fall
            SceneLighting.BeginDim();                                        // the lights go down with it
            lock (_bladeLock)
            {
                _dropStart = GameClock.Now; Dropping = true; _landed = false; _fallDone = false; _landedAt = default;
                StartEngineFall();
            }
            if (_owner.Redirect) BladeRedirect.BeginRedirect(_bladeX, _dropH + _fallHeight, _bladeY);
            EnsureBladeThread();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] judgement blade falls on slot {_hoverSlot} for weapon {_owner.WeaponId}");
            return true;
        }

        /// <summary>The blade thread (BladeLoop): watches an engine-stepped fall at <see cref="FallTickMs"/> — the dim along
        /// it, then <see cref="_fallDone"/> for the tick to land on — and places a point hover hung over a spot. (The
        /// lock-on hover needs no thread: pinned or followed, the engine carries it.)</summary>
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
                        BladeProp.Place(_pointX, _spotH + _hoverHeight, _pointY, EnemyFacing.PlayerFacing());
                        Thread.Sleep(FallTickMs); continue;
                    }
                    if (!Dropping || _fallDone) { Thread.Sleep(20); continue; }
                    // THE ENGINE STEPS THE FALL (the blade-fall cave, once a frame): this thread only watches where it
                    // has got to, for the dim and the landing; no placement is written from here.
                    float  h = Memory.ReadFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY);
                    int    flag = Memory.ReadInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag);
                    if (flag != CodeCaves.DriveFalling && flag != CodeCaves.DriveLanded)
                    {   // something else took the words mid-fall: the fall re-armed from where it is, and said so
                        lock (_bladeLock) { if (Dropping) Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.DriveFalling); }
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] engine fall found flag {flag} mid-fall — re-armed");
                    }
                    // DIAGNOSTIC: where the engine has the blade at a few points of the fall, and when it lands
                    double since = (GameClock.Now - _dropStart).TotalSeconds;
                    if (DebugDiagnostics.Enabled && _fallLogged < 4 && since >= _fallLogged * 0.1)
                    { _fallLogged++; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] engine fall at {since * 1000:F0} ms: y {h:F1} vy {Memory.ReadFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveVy):F2} flag {flag}"); }
                    if (DebugDiagnostics.Enabled && flag == 2 && _fallLogged < 9) { _fallLogged = 9; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] engine fall landed at {since * 1000:F0} ms"); }
                    double span = Math.Max(0.01, _fallStart - (_dropH + _fallStop));
                    double u = Math.Sqrt(Math.Max(0.0, Math.Min(1.0, (_fallStart - h) / span)));   // the fall's fraction in FRAMES (y falls with the square of it)
                    if (BladeRedirect._redirecting) Memory.WriteVec3(CodeCaves.JudgementPos, _bladeX, h, _bladeY);   // what every enemy is watching
                    // The dim peaks as it lands: along the whole fall, or only its last RampFrames (a strike's brief plunge).
                    double rampSpan = _owner.RampWholeFall ? 1.0 : Math.Min(1.0, SceneLighting.RampFrames / Math.Max(1.0, _fallFrames));
                    double w = Math.Max(0.0, Math.Min(1.0, (u - (1.0 - rampSpan)) / Math.Max(1e-3, rampSpan)));
                    float  ramp = (float)((Math.Exp(SceneLighting.RampSharpness * w) - 1.0) / (Math.Exp(SceneLighting.RampSharpness) - 1.0));
                    float  from = _owner.Profile.PrimeDim;                        // on from the primed level, not from the floor's own light
                    SceneLighting.Dim(from + (1f - from) * ramp);
                    if (flag == 2) _fallDone = true;
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[JudgementBlade] fall tick failed: " + e.Message); }
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
            BladeProp.Place(_bladeX, _fallStart, _bladeY, EnemyFacing.PlayerFacing());   // x/z and facing once; the cave carries y
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY, _fallStart);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveVy, 0f);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveG, g);
            Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveStop, stop);
            Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, 1);
            _fallLogged = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] engine fall: {_fallStart:F1} → {stop:F1} over {_fallFrames:F0} frames (g {g:F4}/frame²; paced {_paceTo > 0f}, timed {_fallSeconds:F2}s)");
        }
        private static int _fallLogged;
        private static void StopEngineFall() => Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.VerticalDriveOff);
        /// <summary>The hover riding a unit through the cave: its position pointer and the height OVER it into the blade
        /// words, the flag at FOLLOWING (written only on a change). The cave adds the unit's own height each frame.</summary>
        private static int _engineFollowSlot = -1; private static float _engineFollowY = float.NaN;
        // ⚠ The hover's tick and the drop (Solar Flash's tick) are different threads: the blade words are written under
        // one lock, and a follow is never written once a drop is on (a follow written over a fresh fall leaves the blade
        // hanging for good).
        private static readonly object _bladeLock = new object();
        private static void EngineFollow(int slot, float overUnit)
        {
            lock (_bladeLock)
            {
                if (Dropping) return;
                if (_engineFollowSlot == slot && Math.Abs(overUnit - _engineFollowY) < 0.05f
                    && Memory.ReadInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag) == CodeCaves.DriveFollowing) return;
                Memory.WriteUInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveUnit, (uint)(EnemyAddresses.CharObjects.PosAddr(slot) - 0x20000000L));
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY, overUnit);
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffX, 0f);
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffZ, 0f);
                Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.DriveFollowing);
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
                if (same && Memory.ReadInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag) == CodeCaves.DriveFollowing) return;
                Memory.WriteUInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveUnit, posGuest);
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveY, overUnit);
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffX, offX);
                Memory.WriteFloat(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveOffZ, offZ);
                Memory.WriteInt(CodeCaves.VerticalDrive + CodeCaves.VerticalDriveFlag, CodeCaves.DriveFollowing);
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
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] point hover blade in the ground at ({_bladeX:F0},{_dropH:F0},{_bladeY:F0})");
                PointEnd();
                return;
            }
            BlastFalloff.LastBlast = (_dropX, _dropH, _dropY);
            if (BladeRedirect._redirecting) Memory.WriteVec3(CodeCaves.JudgementPos, _dropX, _dropH, _dropY);   // …and it stays on the blast
            _owner.Land(_hoverSlot, _dropX, _dropH, _dropY);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] judgement blade lands on ({_bladeX:F0},{_dropH:F0},{_bladeY:F0}) for weapon {_owner.WeaponId}");
            AbandonHover();
            _landedAt = GameClock.Now; _landed = true;
            Dropping = false;
        }

        /// <summary>Everything the hover put up, back down: the copy, the target's name bar, the glow's home.</summary>
        private static void AbandonHover()
        {
            StopEngineFall(); _engineFollowSlot = -1; _engineFollowY = float.NaN; _engineOffX = _engineOffZ = float.NaN;
            Memory.WriteInt(CodeCaves.NameHide, 0);                                 // the name plate back
            // ⚠ A glow left hanging on the copy's root after the copy is gone is a sprite drawn every frame at a node in
            // mod memory that nothing maintains (a reset). The fade-out waits for the glow to shrink off first; every
            // other way here cuts it.
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
            _followSlot = slot >= 0 && EnemyBody.HasHp(slot) ? slot : -1;
            _pointRides = _followSlot < 0 && ridesPlayer;
            if (_followSlot < 0 && ridesPlayer)
            {   // a spot AHEAD OF TOAN: the cave places it from his own position every frame (smooth as he walks), the
                // mod only refreshing the offset ahead of him as he turns
                _pointX = x; _pointY = y; _spotH = h; _hoverHeight = height; _heightSlot = -1; Memory.WriteInt(CodeCaves.NameHide, 0);
                EngineFollowPoint((uint)(Addresses.dunPositionX - 0x20000000L), x - Memory.ReadFloat(Addresses.dunPositionX), y - Memory.ReadFloat(Addresses.dunPositionY), height);
                BladeProp.Orient(EnemyFacing.PlayerFacing());
            }
            else if (_followSlot < 0) { _pointX = x; _pointY = y; _spotH = h; _hoverHeight = height; _heightSlot = -1; Memory.WriteInt(CodeCaves.NameHide, 0); StopEngineFall(); }
            else
            {
                if (_heightSlot != slot) { _heightSlot = slot; _hoverHeight = HoverHeightFor(slot, EnemyBody.UnitHeight(slot)); }   // measured once, at rest
                Memory.WriteInt(CodeCaves.NameHide, 1);                           // the target's name plate off, as under the lock-on hover
                EngineFollow(slot, _hoverHeight);                                 // the cave places it over the unit every frame
                BladeProp.Orient(EnemyFacing.PlayerFacing());
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
            SceneLighting.BeginDim();                                             // the lights go down with it (a dim already up keeps its capture)
            lock (_bladeLock)
            {
                _dropStart = GameClock.Now; Dropping = true; _landed = false; _fallDone = false; _landedAt = default;
                StartEngineFall();
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[JudgementBlade] point hover blade falls at ({_bladeX:F0},{_bladeY:F0}) over {_fallSeconds:F2}s for weapon {_owner.WeaponId}");
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
            if (_followSlot >= 0 && !EnemyBody.HasHp(_followSlot)) { PointEnd(); return; }
            DriveGlow(true);
            if (Dropping && _fallDone) Land();
        }
    }
}
