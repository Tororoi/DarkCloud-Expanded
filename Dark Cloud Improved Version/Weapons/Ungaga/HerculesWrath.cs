using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Hercules' Wrath — the ultimate, the guard charge's SECOND level (docs/hercules-wrath.md). Level 1 is the Mirage's
    /// decoy (Mirage.cs: the guard pose held Mirage.GuardChargeMs); the guard held <see cref="ChargeSeconds"/> more charges level 2:
    /// the room dims as Big Bang's does (SceneLighting, <see cref="PrimeDim"/>) and the spear goes gold (<see cref="Gold"/>,
    /// <see cref="Curve"/>) through the Sun Sword's blade lever (<see cref="SetGold"/>), the Mirage clone's spear with it. Released
    /// early, it all falls away; full, it is PRIMED for as long as a Mirage decoy stands. The strike — Ungaga's next swing reaching
    /// <see cref="StrikeFrame"/>, or the FIRST pellet Super Steve fires with its sphere (<see cref="Fire"/>, <see cref="Track"/>) —
    /// plays the sparkle (e508_ex on the dead dun/effect/zibaku_r, in the second main-character instance: motion 0 at the clone's
    /// spear tip or where the pellet died, motion 1 on the ground there, <see cref="Play"/>), plunges the room to black from
    /// <see cref="DarkFrom"/> to <see cref="BlastFrame"/>, lands Big Bang's blast there (<see cref="Blast"/>) under the Sword of
    /// Zeus's flash, and fades the sparkle and the gold out together from <see cref="FadeFrom"/> to <see cref="FadeTo"/>.</summary>
    internal static class HerculesWrath
    {
        private const string Tag = "[HerculesWrath] ";
        private const int    TickMs        = 16;
        private const double ChargeSeconds = 5.0;
        private const float  PrimeDim      = 0.35f;                    // Big Bang's prime darkness
        private static readonly float[] Gold = { 150f, 130f, 50f };    // the spear's charged tint (ambient add)
        private const double GoldCurve     = 4.0;                      // the exponential ramp's sharpness
        private const uint   BladeFrame    = 0x5F393077;               // 'w','0','9','_' — Hercules' mesh frame, w09__m
        private const string ModelCode     = "c10w09";
        private const float  BlastWhp      = 20f;                      // Big Bang's blast cost
        private const float  BlastReach    = 2f;                       // Big Bang's falloff rings (10/25/40/50 at 4×/3×/2×/1× attack) ×2
        // e508_ex's KEYs: 0 = 2–16 @0.3, 1 = 21–80 @0.7, 2 = 81–96 (the fade).
        private const float  M0Start = 2f, M0End = 16f, M1Start = 21f, M1End = 80f;
        private const float  FadeFrom = 60f, FadeTo = 68f;             // motion 1 and the spear's gold fade out across these frames; the sparkle ends at FadeTo
        // The strike leaves on this frame of Ungaga's swing (his motion cursor, PlayerAction.AnimFrameCursor): his first combo
        // swing's hit window is 674–677 (UngagaKey_Play), the next swing starts at 686.
        private const float  StrikeFrame = 677f, StrikeWindowEnd = 686f;
        private static bool  _swingLatched;
        private const uint   TipWord = 0x6C6F6364;                     // "dcol" — the spear's tip bone is dcol0
        private const byte   TipDigit = (byte)'0';
        private const float  M1Rate  = 0.2f;                           // motion 1's play rate (absolute; its own KEY is 0.7)
        private const float  DarkFrom = 21f, BlastFrame = 36f;
        private const float  Lead = 0.5f;                              // frames before a clip's end to move on (the cursor parks short of it)
        private const string EffectName = "zibaku_r";
        private const double MissSeconds = 3.0;                        // a primed pellet still out this long strikes where it is
        private static readonly GuardHold _guard = new();              // the guard pose timed (its Since: when the pose began)

        private enum Phase { Idle, Charging, Primed, Flying, Playing }
        private static Phase    _phase = Phase.Idle;
        private static DateTime _chargeStart;
        private static float    _px, _ph, _py;                        // the mirage's spot
        private static float    _hx, _hy;                             // motion 1's ground position: the spear tip's as motion 0 ended (height: the mirage's)
        private static BorrowedEffect _fx;
        private static int      _sub = -1, _key = -1;
        private static bool     _blasted;
        private static int      _lastAction;
        private static bool     _xiao;                                // Super Steve's sphere is the one wielding it (latched per charge)
        private static float    _sx, _sh, _sy;                        // Xiao's strike point: where her primed pellet died
        private static float    _fh;                                  // the floor's height under it: her motion 1 and blast height
        private static int      _slot = -1;                           // her primed pellet while it flies
        private static DateTime _firedAt;
        private static int      _contactSeen = PelletContacts.Fresh;
        private static readonly PelletWatch _pellets = new PelletWatch();   // the pellets of hers seen, for the primed one

        /// <summary>Hercules' Wrath in the active character's hands: Ungaga's, or Super Steve's with its sphere attached.</summary>
        internal static bool Wielded() => UngagaWeapon.WieldsOrSphere(Items.herculeswrath);

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the sparkle, whenever Hercules' Wrath is
        /// wielded (<see cref="Wielded"/>). Every phase radius zeroed: the effect is the visual only (the blast is the hit).</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (!Wielded()) return null;
            _fx ??= BorrowedShots.VisualOnly(5, EffectName, muzzleMotion: 2, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                             dir: BorrowedShots.EffectDir, instance: ShotEffectPack.CharaMainEffectCrash);
            return _fx;
        }

        public static void UltimateEffect()
        {
            _xiao = Player.CurrentCharacterNum() == Player.XiaoId;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"ultimate: keep guarding {ChargeSeconds:F0} s past the Mirage to prime; the next " + (_xiao ? "pellet" : "swing") + " brings it down");
            try
            {
                while (Wielded() && Player.InDungeonFloor())
                {
                    if (!Player.CheckDunIsPausedOrMenu()) { Step(); SceneLighting.Tick(); BlastFalloff.ExpireShells(); }   // the flash's ease is stepped here (nothing of the wielder's else does); the strike's unspent entries withdrawn
                    Thread.Sleep(TickMs);
                }
            }
            finally { Cancel("sword away"); SceneLighting.Restore(); }
        }

        private static void Step()
        {
            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool swinging = action >= PlayerAction.ActionComboFirst && action <= PlayerAction.ActionComboLast;
            if (!swinging) _swingLatched = false;
            float cursor = Memory.ReadFloat(PlayerAction.AnimFrameCursor);
            bool swingStart = swinging && !_swingLatched && cursor >= StrikeFrame && cursor < StrikeWindowEnd;   // the swing reaches frame 677
            if (swingStart) _swingLatched = true;
            _lastAction = action;
            switch (_phase)
            {
                case Phase.Idle:
                {
                    double held = _guard.Held();
                    if (held * 1000.0 >= Mirage.GuardChargeMs) { _phase = Phase.Charging; _chargeStart = _guard.Since.AddMilliseconds(Mirage.GuardChargeMs); SceneLighting.BeginDim(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "level 2 charging (the Mirage is level 1)"); }
                    break;
                }
                case Phase.Charging:
                {
                    if (_guard.Held() <= 0) { Cancel("guard released before level 2"); break; }
                    float f = (float)Math.Min(1.0, (GameClock.Now - _chargeStart).TotalSeconds / ChargeSeconds);
                    SceneLighting.DimTo(PrimeDim * f);
                    SetGold(Curve(f));
                    if (f >= 1f) { _phase = Phase.Primed; _pellets.NewPellet(); Player.FlashChargeComplete(); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "primed"); }   // pellets already out are not the strike
                    break;
                }
                case Phase.Primed:
                {
                    if (!Mirage.DecoyUp) { Cancel("the mirage faded before the strike"); break; }
                    float k = Mirage.DecoyOutroAlpha;                                // 1 while it stands; its dissolve at the end
                    SceneLighting.DimTo(PrimeDim * k);
                    SetGold(k);
                    if (_xiao) { int slot = _pellets.NewPellet(); if (slot >= 0 && k > 0f) Fire(slot); }
                    else if (swingStart && k > 0f) Unleash();
                    break;
                }
                case Phase.Flying:
                    SceneLighting.DimTo(PrimeDim);
                    SetGold(1f);
                    Track();
                    break;
                case Phase.Playing:
                    Play();
                    break;
            }
        }

        private static float Curve(float f) => (float)((Math.Exp(GoldCurve * f) - 1.0) / (Math.Exp(GoldCurve) - 1.0));

        /// <summary>The spear's gold at <paramref name="k"/> (0..1) through the blade lever — which tints the clone's spear as well:
        /// the clone's rigid weapon visuals are the real spear's own (shared), private vtable and all.</summary>
        private static void SetGold(float k) => WielderTint.Set(k, ModelCode, BladeFrame, Gold, _xiao);   // the spear's frame, or the whole slingshot
        private static void ClearGold() => BladeTint.Clear();

        /// <summary>Where motion 0 plays: Xiao's — where her primed pellet died; Ungaga's — the clone's spear tip, or the mirage's
        /// spot when the clone's weapon cannot be read.</summary>
        private static (float x, float h, float y) SpearTip()
        {
            if (_xiao) return (_sx, _sh, _sy);
            return CharacterClone.WeaponBoneWorld(TipWord, TipDigit, out float x, out float h, out float y) ? (x, h, y) : (_px, _ph, _py);
        }

        /// <summary>Xiao's primed pellet has left: followed until it dies.</summary>
        private static void Fire(int slot)
        {
            long pa = PlayerShotPool.PosAddr((uint)Memory.ReadInt(PlayerShotPool.BasePtr), slot);
            _sx = Memory.ReadFloat(pa); _sh = Memory.ReadFloat(pa + 4); _sy = Memory.ReadFloat(pa + 8);
            _slot = slot; _firedAt = GameClock.Now; _phase = Phase.Flying;
            PelletContacts.Sync(ref _contactSeen);                                 // only contacts from here on are this pellet's
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"primed pellet: slot {slot}");
        }

        /// <summary>The primed pellet in flight: the strike where it dies — the engine's contact point on an enemy or a wall, else
        /// where it was last seen (the end of its range); one still out after <see cref="MissSeconds"/> strikes where it is.</summary>
        private static void Track()
        {
            if (PelletContacts.Poll(ref _contactSeen, out var c) && c.Slot == _slot)
            {
                _sx = c.X; _sh = c.H; _sy = c.Y;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet met " + (c.Enemy ? "an enemy" : "a wall") + $" at ({c.X:F0},{c.H:F0},{c.Y:F0})");
                Strike(); return;
            }
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            bool live = Memory.IsValidGuest(pool) && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, _slot)) != 0;
            if (live)
            {
                long pa = PlayerShotPool.PosAddr(pool, _slot);
                _sx = Memory.ReadFloat(pa); _sh = Memory.ReadFloat(pa + 4); _sy = Memory.ReadFloat(pa + 8);
                if ((GameClock.Now - _firedAt).TotalSeconds < MissSeconds) return;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"pellet ended at ({_sx:F0},{_sh:F0},{_sy:F0})");
            Strike();
        }
        private static void Strike()
        {
            _slot = -1;
            MiragePos();
            if (DungeonFloor.HeightAt(_sx, _sy, _sh, out float fh)) { _fh = fh; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"floor under the pellet at height {fh:F1} (pellet {_sh:F1}, mirage {_ph:F1})"); }
            else { _fh = _ph; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no floor found under the pellet — the mirage's height"); }
            Unleash();
        }

        /// <summary>Motion 1's (and the blast's) height: the floor under Xiao's strike point, or Ungaga's mirage's height.</summary>
        private static float GroundH => _xiao ? _fh : _ph;

        /// <summary>The mirage's spot, while one stands (kept from the last one when it has gone).</summary>
        private static void MiragePos()
        {
            if (Mirage.DecoyUp) (_px, _ph, _py) = Mirage.DecoyPosition;
        }

        /// <summary>The swing: the sparkle on the enemy (motion 0 at its own speed), or — the effect not entered on this floor —
        /// the blast and flash at once.</summary>
        private static void Unleash()
        {
            MiragePos();
            _blasted = false; _key = -1; _sub = -1;
            var (sx, sh, sy) = SpearTip();
            if (_fx != null && BorrowedShots.Entered(_fx) && BorrowedShots.Burst(_fx, sx, sh, sy, 0, 1f))
            {
                _sub = Memory.ReadInt(_fx.Instance + ShotEffectPack.OffLastIdx);
                SetFade(1f);                                                          // a sub-shot a previous sparkle faded, back to full
                Memory.WriteInt(CodeCaves.SecondEffectLive, 1);
                SetClip(0, M0Start, -1f);
                _phase = Phase.Playing;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (_xiao ? $"unleashed where the pellet died ({sx:F0},{sh:F0},{sy:F0})" : $"unleashed on the mirage at ({_px:F0},{_ph:F0},{_py:F0})"));
                return;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "sparkle not entered on this floor — the blast at once");
            Blast();
            _phase = Phase.Idle;
        }

        private static long Obj => _fx.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        /// <summary>The sparkle's sub-shot onto KEY <paramref name="key"/> from <paramref name="frame"/> at <paramref name="rate"/>, the KEY remembered.</summary>
        private static void SetClip(int key, float frame, float rate)
        {
            ShotEffects.SetClip(Obj, key, frame, rate);
            _key = key;
        }

        private static void Play()
        {
            if (_sub < 0 || !Player.CheckDunIsWalkingMode()) return;
            long inst = _fx.Instance, o = Obj;
            bool alive = Memory.ReadUShort(inst + ShotEffectPack.OffActive + _sub * 2) != 0;
            if (!alive)
            {   // the fade ran out (the engine retired it) — or it was lost early: the blast is never skipped
                if (!_blasted) Blast();
                Finish(); return;
            }
            MiragePos();
            if (_key == 0) { var (tx, th, ty) = SpearTip(); Memory.WriteVec3(o + ShotEffectPack.ObjPos, tx, th, ty); }   // motion 0 rides the clone's spear tip
            else Memory.WriteVec3(o + ShotEffectPack.ObjPos, _hx, GroundH, _hy);                                        // motion 1: the strike point across the ground, at GroundH
            float frame = Memory.ReadFloat(o + ShotEffectPack.ObjFrame);
            int status = Memory.ReadInt(o + CharacterMotion.MotionStatusOffset);
            if (_key == 0 && (frame >= M0End - Lead || status == 3))
            {   // motion 1 stays where motion 0 ended across the ground, at the mirage's height
                (_hx, _, _hy) = SpearTip();
                SetClip(1, M1Start, M1Rate);
            }
            else if (_key == 1)
            {
                Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, M1Rate);             // held: nothing resets it mid-clip, but a phase change would
                if (!_blasted)
                {
                    float u = Math.Max(0f, Math.Min(1f, (frame - DarkFrom) / (BlastFrame - DarkFrom)));
                    SceneLighting.DimRamp(PrimeDim, u);                                // the plunge to black, frame 21 → 36
                    if (frame >= BlastFrame) Blast();
                }
                float v = 1f - Math.Max(0f, Math.Min(1f, (frame - FadeFrom) / (FadeTo - FadeFrom)));
                SetGold(v);                                                            // the spear's gold holds full, then fades with the sparkle
                if (frame >= FadeFrom) SetFade(v);
                if (frame >= FadeTo || frame >= M1End - Lead || status == 3)
                {   // faded out: the sub-shot ended here (no motion 2)
                    if (!_blasted) Blast();
                    Memory.WriteUShort(inst + ShotEffectPack.OffActive + _sub * 2, 0);
                    Finish();
                }
            }
        }

        /// <summary>Big Bang's blast on the enemy, its weapon-HP cost, and the Sword of Zeus's flash easing back over two seconds.</summary>
        private static void Blast()
        {
            _blasted = true;
            MiragePos();
            float x = _key >= 1 ? _hx : _xiao ? _sx : _px, h = GroundH, y = _key >= 1 ? _hy : _xiao ? _sy : _py;   // where motion 1 plays (else its strike point: the mirage's spot, or where Xiao's pellet died)
            BlastFalloff.PlantFalloff(x, h, y, reachScale: BlastReach, guardBreak: true); // Big Bang's rings at twice the reach: 20 / 50 / 80 / 100, crushing (through any guard)
            WeaponWhp.Drain((ushort)(_xiao ? Items.supersteve : Items.herculeswrath), BlastWhp, Tag + "ultimate ");
            Mirage.Dispel();                                                         // the strike takes the mirage with it
            SunSword.ZeusFlash.ArmLighting();
            SceneLighting.Flash();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"blast at ({x:F0},{h:F0},{y:F0})");
        }

        /// <summary>The sparkle's brightness 0..1 (SubShotFade: its unlit additive frames' constant colour).</summary>
        private static void SetFade(float k)
        {
            if (_sub < 0 || _fx == null) return;
            SubShotFade.Set(Obj, k);
        }

        private static void Finish()
        {
            SetFade(1f);                                                              // the sub-shot handed back at full
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _sub = -1; _key = -1; _phase = Phase.Idle;
        }

        private static void Cancel(string why)
        {
            if (_phase == Phase.Idle) return;
            if (_phase == Phase.Playing && _sub >= 0 && _fx != null) Memory.WriteUShort(_fx.Instance + ShotEffectPack.OffActive + _sub * 2, 0);
            if (!_blasted || _phase != Phase.Playing) { ClearGold(); SceneLighting.EndDim(); }
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _sub = -1; _key = -1; _slot = -1; _phase = Phase.Idle;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "ultimate off: " + why);
        }
    }
}
