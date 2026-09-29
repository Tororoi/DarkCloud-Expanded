using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Hercules' Wrath — the ultimate, the guard charge's SECOND level. Level 1 is the Mirage's decoy (Mirage.cs, which
    /// Hercules' Wrath inherits: the guard pose held Mirage.GuardChargeMs); keep holding the guard <see cref="ChargeSeconds"/> more
    /// to charge level 2: the room dims
    /// as Big Bang's does (SolarLighting, <see cref="PrimeDim"/>), the enemies, Ungaga and his Mirage clone take the dark's white
    /// (SolarLighting's own, up to 30), and the spear goes gold (<see cref="Gold"/>) on an exponential curve — both through the
    /// blade-tint lever the Sun Sword uses (SolarBlade, its mesh frame `w09__m`): the clone's spear shares the real one's visual.
    /// Released early, it all falls away. Full, it is PRIMED and the dim holds — for as long as a Mirage decoy stands (a new one
    /// cast keeps it primed); when the last one dissolves unused, the gold and the dim fall away with it. The next swing plays the
    /// sparkle ON THE MIRAGE (Mirage.DecoyPosition), and the blast is centred there
    /// (s78's cutscene e508_ex, baked onto dun/effect/zibaku_r and borrowed into the second main-character instance, which
    /// the second-effect caves step and draw): its motion 0 at its own speed AT THE TIP OF THE CLONE'S SPEAR (its `dcol0`, 15.66
    /// along c10w09's blade — the clone's weapon bone, CharacterClone.WeaponBoneWorld), then motion 1 at 0.2 where the tip was as
    /// motion 0 ended across the ground, at the mirage's height — where the blast lands too, faded out by the mod from
    /// frame <see cref="FadeFrom"/> to <see cref="FadeTo"/>, where the mod ends the sub-shot (motion 2 is never played;
    /// the config still declares it as the muzzle motion, so the engine's own retire window, frames 95–96, is never reached
    /// early). Every mesh of the sparkle is an unlit additive frame (`__czapp…`), drawn in its frame's constant colour
    /// (CFrame +0xD0..+0xDC, 128), so that colour scaled to 0 fades it (<see cref="SetFade"/>). The spear's gold holds full
    /// through motion 1 and fades back to the plain spear with the sparkle, frame <see cref="FadeFrom"/> to <see cref="FadeTo"/>; from frame <see cref="DarkFrom"/> to <see cref="BlastFrame"/> the room plunges to black; at frame
    /// <see cref="BlastFrame"/> Big Bang's blast lands on the enemy (its falloff, multiplier and weapon-HP cost) and the light
    /// flashes as the Sword of Zeus's bolt does, easing back to normal over two seconds.</summary>
    internal static class HerculesUltimate
    {
        private const string Tag = "[Hercules] ";
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
        private static DateTime _guardSince;                           // when the guard pose began (default = not guarding)

        private enum Phase { Idle, Charging, Primed, Playing }
        private static Phase    _phase = Phase.Idle;
        private static DateTime _chargeStart;
        private static float    _px, _ph, _py;                        // the mirage's spot
        private static float    _hx, _hy;                             // motion 1's ground position: the spear tip's as motion 0 ended (height: the mirage's)
        private static BorrowedEffect _fx;
        private static int      _sub = -1, _key = -1;
        private static bool     _blasted;
        private static int      _lastAction;

        /// <summary>The effect this weapon wants in the SECOND main-character instance: the sparkle, whenever Hercules' Wrath is in
        /// Ungaga's hands. Every phase radius zeroed: the effect is the visual only (the blast is the hit).</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.UngagaId || Player.Weapon.GetCurrentWeaponId() != Items.herculeswrath) return null;
            if (_fx == null)
            {
                _fx = BorrowedShots.CustomConfig(5, EffectName, muzzleMotion: 2, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                                 dir: BorrowedShots.EffectDir, instance: ShotEffectPack.CharaMainEffectCrash);
                if (_fx == null) return null;
                for (int ph = 0; ph < 4; ph++) BorrowedShots.SetPhaseRadius(_fx, ph, 0f);
            }
            return _fx;
        }

        public static void UltimateEffect()
        {
            Console.WriteLine(Tag + $"ultimate: keep guarding {ChargeSeconds:F0} s past the Mirage to prime; the next swing brings it down");
            try
            {
                while (Player.Weapon.GetCurrentWeaponId() == Items.herculeswrath && Player.InDungeonFloor())
                {
                    if (Player.CurrentCharacterNum() != Player.UngagaId) { Cancel("ally out"); Thread.Sleep(100); continue; }
                    if (!Player.CheckDunIsPausedOrMenu()) { Step(); SolarLighting.Tick(); }   // the flash's ease is stepped here: nothing of Ungaga's else does
                    Thread.Sleep(TickMs);
                }
            }
            finally { Cancel("sword away"); SolarLighting.Restore(); }
        }

        /// <summary>The guard pose held, as the Mirage reads it: R1 down and Ungaga in the guard loop or guard walk. Returns the
        /// seconds it has been held (0 = not guarding).</summary>
        private static double GuardHeld()
        {
            bool r1 = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            if (!r1) { _guardSince = default; return 0; }
            if (mid != Mirage.GuardLoopMotion && mid != Mirage.GuardMoveMotion) return _guardSince == default ? 0 : (GameClock.Now - _guardSince).TotalSeconds;
            if (_guardSince == default) _guardSince = GameClock.Now;
            return (GameClock.Now - _guardSince).TotalSeconds;
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
                    double held = GuardHeld();
                    if (held * 1000.0 >= Mirage.GuardChargeMs) { _phase = Phase.Charging; _chargeStart = _guardSince.AddMilliseconds(Mirage.GuardChargeMs); SolarLighting.BeginDim(); Console.WriteLine(Tag + "level 2 charging (the Mirage is level 1)"); }
                    break;
                }
                case Phase.Charging:
                {
                    if (GuardHeld() <= 0) { Cancel("guard released before level 2"); break; }
                    float f = (float)Math.Min(1.0, (GameClock.Now - _chargeStart).TotalSeconds / ChargeSeconds);
                    SolarLighting.DimTo(PrimeDim * f);
                    SetGold(Curve(f));
                    if (f >= 1f) { _phase = Phase.Primed; Player.FlashChargeComplete(); Console.WriteLine(Tag + "primed"); }
                    break;
                }
                case Phase.Primed:
                {
                    if (!Mirage.DecoyUp) { Cancel("the mirage faded before the strike"); break; }
                    float k = Mirage.DecoyOutroAlpha;                                // 1 while it stands; its dissolve at the end
                    SolarLighting.DimTo(PrimeDim * k);
                    SetGold(k);
                    if (swingStart && k > 0f) Unleash();
                    break;
                }
                case Phase.Playing:
                    Play();
                    break;
            }
        }

        private static float Curve(float f) => (float)((Math.Exp(GoldCurve * f) - 1.0) / (Math.Exp(GoldCurve) - 1.0));

        /// <summary>The spear's gold at <paramref name="k"/> (0..1) through the blade lever — which tints the clone's spear as well:
        /// the clone's rigid weapon visuals are the real spear's own (shared), private vtable and all.</summary>
        private static void SetGold(float k) => SolarBlade.Set(k, ModelCode, BladeFrame, 0, Gold);
        private static void ClearGold() => SolarBlade.Clear();

        /// <summary>The clone's spear tip, or the mirage's spot when the clone's weapon cannot be read.</summary>
        private static (float x, float h, float y) SpearTip()
            => CharacterClone.WeaponBoneWorld(TipWord, TipDigit, out float x, out float h, out float y) ? (x, h, y) : (_px, _ph, _py);

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
                Console.WriteLine(Tag + $"unleashed on the mirage at ({_px:F0},{_ph:F0},{_py:F0})");
                return;
            }
            Console.WriteLine(Tag + "sparkle not entered on this floor — the blast at once");
            Blast();
            _phase = Phase.Idle;
        }

        private static long Obj => _fx.Instance + ShotEffectPack.OffObj + _sub * ShotEffectPack.ObjStride;

        private static void SetClip(int key, float frame, float rate)
        {
            long o = Obj;
            Memory.WriteInt  (o + ShotEffectPack.ObjMotId, key);
            Memory.WriteInt  (o + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat(o + ShotEffectPack.ObjFrame, frame);
            Memory.WriteFloat(o + ShotEffectPack.ObjMotSpd, rate);
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
            else Memory.WriteVec3(o + ShotEffectPack.ObjPos, _hx, _ph, _hy);                                            // motion 1: the tip's last spot across the ground, the mirage's height
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
                    SolarLighting.DimRamp(PrimeDim, u);                                // the plunge to black, frame 21 → 36
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
            float x = _key >= 1 ? _hx : _px, h = _ph, y = _key >= 1 ? _hy : _py;   // where motion 1 plays (the mirage's spot if it never got there)
            BigBang.PlantFalloff(x, h, y, reachScale: BlastReach);                  // Big Bang's rings at twice the reach: 20 / 50 / 80 / 100
            WeaponWhp.Drain(Items.herculeswrath, BlastWhp, Tag + "ultimate ");
            Mirage.Dispel();                                                         // the strike takes the mirage with it
            SunSword.ZeusFlash.ArmLighting();
            SolarLighting.Flash();
            Console.WriteLine(Tag + $"blast at ({x:F0},{h:F0},{y:F0})");
        }

        /// <summary>The sparkle's brightness 0..1: every frame of the sub-shot's model at <paramref name="k"/> × its constant
        /// colour (CFrame +0xD0..+0xDC, 128 on an unlit frame) — for these additive meshes, its opacity. Walks the model tree
        /// from the sub-shot's root (+0xBC) by its child (+0x138) / sibling (+0x13C) links.</summary>
        private const float UnlitFull = 128f;
        private static void SetFade(float k)
        {
            if (_sub < 0 || _fx == null) return;
            uint root = Memory.ReadGuestPtr(Obj + CCharacter.CharModel);
            if (Memory.IsValidGuest(root)) Fade(Memory.ToMmu(root), UnlitFull * k, 0);
        }
        private static void Fade(long node, float v, int depth)
        {
            if (depth > 16) return;
            for (long n = node; ; )
            {
                if (Memory.ReadByte(n + CFrameVu1.UnlitFlag) != 0)        // an unlit frame (SetFrameAttr `c`: +0xC4 = 1)
                    Memory.WriteBytesBatch(n + CFrameVu1.UnlitColourR, Quad(v));
                uint c = Memory.ReadGuestPtr(n + CFrameVu1.RootChild);
                if (Memory.IsValidGuest(c)) Fade(Memory.ToMmu(c), v, depth + 1);
                uint s = Memory.ReadGuestPtr(n + CFrameVu1.RootSibling);
                if (!Memory.IsValidGuest(s) || depth == 0) break;                  // the root has no siblings of its own
                n = Memory.ToMmu(s);
            }
        }
        private static byte[] Quad(float v)
        {
            var b = new byte[16];
            for (int i = 0; i < 4; i++) BitConverter.GetBytes(v).CopyTo(b, i * 4);
            return b;
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
            if (!_blasted || _phase != Phase.Playing) { ClearGold(); SolarLighting.EndDim(); }
            Memory.WriteInt(CodeCaves.SecondEffectLive, 0);
            _sub = -1; _key = -1; _phase = Phase.Idle;
            Console.WriteLine(Tag + "ultimate off: " + why);
        }
    }
}
