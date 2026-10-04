using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sword of Zeus — Solar Harvest and Solar Flash (SunSword.ZeusFlash) with lightning: the whirlwind IS the
    /// bolt (`gedit/s99/chara/lightning.chr`), and the primed flash brings it down on the locked target with every swing
    /// of the combo, or on each of the nearest <see cref="MaxStrikes"/> within <see cref="StrikeReach"/> when not locked
    /// on. A bolt touches nothing itself: its damage is Big Bang's falloff blast at its foot at <see cref="BlastScale"/>.
    /// A full charge attack is a bolt as well (<see cref="ChargeTick"/>). Xiao's Zeus sphere fires the same bolt (ZeusShot).
    /// (docs/sword-of-zeus.md)</summary>
    internal static class SwordOfZeus
    {
        private const int    TickMs = 30;
        // ── the bolt ────────────────────────────────────────────────────────────────────
        // Toan's whirlwind visual is a sub-shot of the main-character effect instance, so seeding it with lightning.chr
        // REPLACES the whirl with the bolt and the engine fires it on the spin by itself (as Big Bang does with
        // explosion.chr). lightning.chr is the Divine Beast Cave cutscene's scene actor, loaded by path from its own
        // directory. Its motion list is two KEYs over frames 10-30: KEY 0 at speed 0 (a held pose, 止め) and KEY 1 at
        // 0.2 — the strike. Only the MUZZLE phase names a motion, as the whirl's own shape does.
        private const int    LightningTemplate = 5;                  // a stock config's shape; the name and motions are replaced
        private const string LightningName     = "lightning", LightningDir = "gedit/s99/chara/";
        private const short  StrikeMotion      = 1;
        // The actor's cards rise from its origin (nodes at +4.5 and +10 up): it stands ON the ground under the target,
        // so a strike is played at the enemy's ground point. ONE scale path for every bolt, strike or whirl: the root
        // frame's local 3×3 (a VERTEX_ANIME mesh is only transformed by its root's local matrix), as the stock whirl and
        // Big Bang's explosion.chr are held. ⚠ The CCharacter scale (+0x90) is never written on these sub-shots: the
        // engine re-fires the whirl into whichever one is free, scale and all, and a +0x90 write there crashes the game.
        private const float  LightningScale    = 10.0f;   // on the root hold
        private const ushort StrikeSe          = 390;   // the cutscene's thunderclap
        private const float  StrikeReach       = 300f;  // not locked on: enemies this far from Toan are in reach (the flash's radius, about the draw distance)
        private const int    MaxStrikes        = 6;     // …and this many of the nearest take a bolt each
        private const float  BlastScale        = 0.5f;  // the bolt's blast, against Big Bang's falloff steps (½× … 2× attack)
        private const float  StrikeWhp         = 10f;   // weapon HP a bolt costs, before the weapon's Endurance scales it down (WeaponWhp: the engine's own drain takes it)
        // lightning.mds's root is `null2`. ⚠ Only its FIRST FIVE bytes are the name at runtime: the frame's name field is
        // not NUL-padded past the NUL, so the root is matched as the word "null" + the byte '2', never as 8 bytes.
        private const uint   RootWord = 0x6C6C756E;                          // "null"
        private const byte   RootDigit = (byte)'2';
        private static bool IsBoltRoot(long root) =>
            Memory.ReadUInt(root + CFrameVu1.Name) == RootWord && Memory.ReadByte(root + CFrameVu1.Name + 4) == RootDigit;
        private static BorrowedEffect _lightning;

        /// <summary>The judgement blade, for this sword: hung over the locked target while primed (JudgementBlade.JudgementTick),
        /// with the red ring for its glow, the fall darkening from this sword's dim, no enemy turned to watch it. Let go
        /// as the primed combo's FIRST swing begins, paced by the swing so it is in the ground to the HILT at the
        /// swing's hit frame; there it is gone at once and the bolt comes down on the target.</summary>
        internal static readonly JudgementBlade.JudgementOwner Judgement = new JudgementBlade.JudgementOwner
        {
            WeaponId = Items.swordofzeus, Glow = ToanGlowBakes.ZeusName, Profile = SunSword.ZeusFlash, Redirect = false, ToTheHilt = true,
            Land = (slot, x, h, y) => { if (!StrikeAt(x, h, y, slot)) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] the blade landed but no bolt was free"); },
        };

        /// <summary>What BorrowedShots seeds the instance with while Toan carries the Sword of Zeus: the bolt.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.ToanId) return null;
            if (Player.Weapon.GetCurrentWeaponId() != Items.swordofzeus) return null;
            return Lightning();
        }
        /// <summary>The bolt's config (built once): lightning.chr under a stock config's shape, the strike its muzzle motion, no
        /// radius on any phase. Xiao's Zeus sphere (ZeusShot) borrows the same bolt.</summary>
        internal static BorrowedEffect Lightning()
        {
            if (_lightning == null)
            {
                _lightning = BorrowedShots.CustomConfig(LightningTemplate, LightningName,
                                                        muzzleMotion: StrikeMotion, flyMotion: -1, impactMotion: -1, expireMotion: -1,
                                                        dir: LightningDir);
                if (_lightning == null) return null;
                for (int phase = 0; phase < 4; phase++) BorrowedShots.SetPhaseRadius(_lightning, phase, 0f);   // the bolt itself hits nothing
            }
            return _lightning;
        }

        /// <summary>True while the instance actually holds lightning.chr — when the stock whirl scaler must stand down
        /// (the model it validates, "kiru" + "fkiri", is not the one loaded).</summary>
        internal static bool LightningSeeded => _lightning != null && BorrowedShots.Entered(_lightning);

        /// <summary>The bolt on one enemy: the strike played at its ground point, its blast, its cost (billed to
        /// <paramref name="weapon"/>, the one in the active character's hand). False when the bolt is not entered on this floor
        /// or every sub-shot is busy.</summary>
        internal static bool Strike(int slot, bool bill = true, ushort weapon = (ushort)Items.swordofzeus)
        {
            if (slot < 0 || slot >= EnemyAddresses.FloorSlots.Count || !Enemies.IsLive(slot)) return false;
            long pos = EnemyAddresses.CharObjects.PosAddr(slot);                    // the unit's own position: its ground point
            if (!StrikeAt(Memory.ReadFloat(pos), Memory.ReadFloat(pos + 4), Memory.ReadFloat(pos + 8), slot, bill, weapon)) return false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning on slot {slot}");
            return true;
        }

        /// <summary>The bolt at a point: a sub-shot of the instance played once there (no damage of its own), Big
        /// Bang's falloff blast at the point, and the bolt's weapon-HP cost. <paramref name="noKickSlot"/> is the enemy
        /// under it, which takes the blast where it stands — no shove; nobody is turned to face the bolt, they are
        /// stunned facing wherever they were, and the struck enemy braces behind its guard like the rest of the floor — the blast crushes that guard.</summary>
        internal static bool StrikeAt(float x, float h, float y, int noKickSlot, bool bill = true, ushort weapon = (ushort)Items.swordofzeus)
        {
            if (!LightningSeeded) return false;
            if (!BorrowedShots.Burst(_lightning, x, h, y, 0, 1f)) return false;   // 1×: the root hold sizes every bolt alike
            MaintainScale();                                                        // …before its first frame
            SeSeq.Play(StrikeSe, 90);
            GamePad.Knockdown();                                                    // the strike felt in the hands
            BlastFalloff.LastBlast = (x, h, y);
            BlastFalloff.PlantFalloff(x, h, y, noKickSlot: noKickSlot, damageScale: BlastScale, guardBreak: true);   // the bolt crushes any guard (the ISO's guard gate)
            if (bill) WeaponWhp.Drain(weapon, StrikeWhp, "[Zeus] bolt ");   // taken by the engine's own drain, as a landed hit's is (a volley bills once, StrikeNearest)
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning at ({x:F0},{h:F0},{y:F0})");
            return true;
        }

        // ── the charge attack (ChargeTick) ──────────────────────────────────────────────
        private const float  ChargeBoltAhead   = 40f;   // units ahead of Toan the bolt lands
        private const float  LungeHigher       = 0.5f;  // the level-2 lunge's extra gravity (CodeCaves.LungeGravityExtra): 1.5× the height in the same frames
        private const float  ChargeHoverHeight = 45f;   // the blade's grip this far above the spot ahead of Toan (over a locked target: the lock-on hover's own height)
        private const int    ChargeLungeFrames = 38;    // the lunge, dash state to end state — the same every time
        private const int    ChargeFallFrames  = 19;    // the fall, hover to hilt-in-the-ground: the drop comes ChargeLungeFrames − this after the dash begins (two frames' margin)
        private const double ChargeFallSeconds = ChargeFallFrames / 60.0, ChargeDropAfterDash = (ChargeLungeFrames - ChargeFallFrames) / 60.0;
        private const float  ChargeDimFrom     = 1.0f;  // the meter as the charge starts (ToanKey_On resets it to 1.0) …
        private const float  ChargeLevel1      = 1.5f;  // … and at level 1 (the game's lunge threshold): half the dim is on, and the blade appears
        private const double ChargeLevel2Seconds = 3.0; // held this long past level 1 is the sword's level 2: the dim full, the blade solid, the release a bolt
        private static bool  _chargeDimming, _chargeFull, _chargeBoltDue, _chargeBoltFired, _unlockZeroed, _chargeDropped;
        private static float _chargeDimFloor;                // the dim a blinding already had on when the charge began: never lifted while charging
        private static DateTime _chargeDropAt, _chargeDashAt, _chargeLevel1At;
        private static int   _unlockWas, _chargeTarget = -1;
        private static float _chargeX, _chargeH, _chargeY;   // where the bolt will land (the hover's spot, frozen at the drop)

        /// <summary>The charge attack, per tick. While Toan winds up, PlayerAction.WhirlwindUnlock is zeroed (put back
        /// the tick the release is read — it is save data), so every release is the LUNGE; the room dims from
        /// <see cref="ChargeDimFrom"/> to half the Zeus prime dim at <see cref="ChargeLevel1"/> and to full over
        /// <see cref="ChargeLevel2Seconds"/> held past it, the blade whitening with it. At that hold the stock
        /// charge-complete flash fires, CodeCaves.LungeGravityExtra = <see cref="LungeHigher"/>, and the judgement blade
        /// hangs <see cref="ChargeHoverHeight"/> over the landing spot (<see cref="ChargeAim"/>), fading in with the hold.
        /// In the lunge the blade is let go <see cref="ChargeDropAfterDash"/> after the dash so its hilt is in the ground
        /// as the END state (PlayerAction.ActionLungeEnd: clip 17 from frame 196) comes up; that tick the bolt comes down
        /// where it fell, with a strike's flash (SunSword.StrikeFlash), the camera pinned through the lunge (CameraHold).
        /// A charge let go short of level 2 plays what it earned and the dim lifts. Stands aside while SunSword.FlashArmed.</summary>
        private static void ChargeTick()
        {
            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            if (SunSword.FlashArmed) { ChargeStandDown(); return; }
            if (action == PlayerAction.ActionWindup)
            {
                float meter = Memory.ReadFloat(PlayerAction.ChargeMeter);
                if (!_chargeDimming)
                {
                    _chargeDimming = true; _chargeFull = false; _chargeBoltDue = false; _chargeLevel1At = default; ZeroUnlock();   // no level 2 for the game this wind-up
                    _chargeDimFloor = SceneLighting.Active ? SceneLighting.LastDim : 0f;   // a blinding's dim stays on under the charge rather than lifting and snapping back
                }
                if (meter >= ChargeLevel1 && _chargeLevel1At == default) _chargeLevel1At = GameClock.Now;   // level 1: the sword's own clock starts
                // The charge's progress: half of it is the meter climbing to level 1, the other half the ChargeLevel2Seconds held past it.
                float held = _chargeLevel1At == default ? 0f : (float)Math.Min(1.0, (GameClock.Now - _chargeLevel1At).TotalSeconds / ChargeLevel2Seconds);
                float k = _chargeLevel1At == default
                    ? 0.5f * Math.Max(0f, Math.Min(1f, (meter - ChargeDimFrom) / (ChargeLevel1 - ChargeDimFrom)))
                    : 0.5f + 0.5f * held;
                BladeTint.Set(k, SunSword.ZeusFlash.Model, 0, 0, SunSword.ZeusFlash.BladeWhite);   // the blade whitens with the meter, as under a guard charge
                SceneLighting.BeginDim();                                        // takes an easing flash over (its capture kept)
                SceneLighting.DimTo(Math.Max(_chargeDimFloor, SunSword.ZeusFlash.PrimeDim * k));
                if (held >= 1f && !_chargeFull)
                {   // the sword's own level 2, ChargeLevel2Seconds past level 1: the stock charge-complete flash on him, and the release flagged as a bolt
                    _chargeFull = true; _chargeDropped = false;
                    Memory.WriteFloat(CodeCaves.LungeGravityExtra, LungeHigher);   // the release's lunge jumps higher
                    Player.FlashChargeComplete();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] charge level 2 — the release is a bolt");
                }
                // From level 1 the blade hangs over where the bolt will land, moving with him, fading in over the hold
                // to full at level 2 — its glow growing at the same rate.
                if (_chargeLevel1At != default)
                {
                    ChargeAim();
                    JudgementBlade.PointAlpha(held);
                }
                return;
            }
            if (_unlockZeroed) RestoreUnlock();                                 // the release has been read: the word goes back at once
            if (!_chargeDimming) return;
            if (PlayerAction.InLunge(action))
            {
                if (_chargeTarget < 0 && !_chargeDropped)
                {   // the blade ahead of him stops riding him as he lunges: the bolt lands under where it hangs
                    var spot = JudgementBlade.PointFreeze(); _chargeX = spot.x; _chargeH = spot.h; _chargeY = spot.y;
                }
                if (!_chargeFull) { ChargeStandDown(); return; }                // a level-1 lunge: nothing more to it
                if (!_chargeBoltDue) { _chargeBoltDue = true; _chargeBoltFired = false; _chargeDashAt = default; CameraHold.Pin(); }   // the camera held at its height through the lunge
                if (_chargeDashAt == default && action != PlayerAction.ActionLunge) _chargeDashAt = GameClock.Now;   // the dash has begun: the clock starts
                bool dropDue = _chargeDashAt != default && (GameClock.Now - _chargeDashAt).TotalSeconds >= ChargeDropAfterDash;
                if (!_chargeDropped && (dropDue || action == PlayerAction.ActionLungeEnd))
                {   // timed to land as the end state comes up: the blade is let go where it hangs, and that is where the bolt lands
                    _chargeDropped = true; _chargeDropAt = GameClock.Now;
                    if (_chargeTarget >= 0 && Enemies.IsLive(_chargeTarget))
                    { long tp = EnemyAddresses.CharObjects.PosAddr(_chargeTarget); _chargeX = Memory.ReadFloat(tp); _chargeH = Memory.ReadFloat(tp + 4); _chargeY = Memory.ReadFloat(tp + 8); }
                    JudgementBlade.PointDrop(ChargeFallSeconds);
                }
                if (_chargeBoltFired) return;
                // The room plunges to black from the dash to the bolt: the falling blade drives it (BigBang's fall
                // thread, the same ramp), or — no blade — the clock does over the same ChargeFallSeconds.
                if (_chargeDropped && !JudgementBlade.Dropping && action != PlayerAction.ActionLungeEnd)
                    SceneLighting.DimRamp(SunSword.ZeusFlash.PrimeDim, (float)((GameClock.Now - _chargeDropAt).TotalSeconds / ChargeFallSeconds));
                if (action != PlayerAction.ActionLungeEnd) return;
                _chargeBoltFired = true;
                float cursor = Memory.ReadFloat(PlayerAction.AnimFrameCursor);
                if (!_chargeDropped) ChargeAim();                                // no dash seen: aim now
                float x = _chargeX, h = _chargeH, y = _chargeY;
                double gap = JudgementBlade.PointLandedAt == default ? double.NaN : (GameClock.Now - JudgementBlade.PointLandedAt).TotalSeconds;
                JudgementBlade.PointEnd();                                              // still in the air: gone now
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + (double.IsNaN(gap) ? "[Zeus] charge bolt with the blade still falling — ChargeFallSeconds is too long" : $"[Zeus] charge bolt {gap * 1000:F0} ms after the blade reached the ground"));
                if (!StrikeAt(x, h, y, _chargeTarget)) { ChargeStandDown(); return; }
                SunSword.StrikeFlash(SunSword.ZeusFlash);                        // the white easing back to normal light, his pulse, and the 5 s blinding — a strike's flash
                CameraHold.Unpin();                                              // the camera follows again
                _chargeDimming = false; _chargeFull = false; _chargeBoltDue = false;   // the flash took the dim over
                BladeTint.Clear(); _chargeTarget = -1;                          // …and the charge's white is off the blade
                Memory.WriteFloat(CodeCaves.LungeGravityExtra, 0f);              // the next lunge is vanilla
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] charge bolt at ({x:F0},{h:F0},{y:F0}) (cursor {cursor:F1})");
                return;
            }
            ChargeStandDown();                                                   // the charge went another way, or is over
        }
        /// <summary>Where the charge's bolt will land, and the blade hung over it: the locked target, followed; else
        /// the spot ChargeBoltAhead units ahead of Toan, moving with him.</summary>
        private static void ChargeAim()
        {
            if (PlayerAction.LockHeld(out int slot) && slot >= 0 && slot < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(slot))
            {
                _chargeTarget = slot;
                long p = EnemyAddresses.CharObjects.PosAddr(slot);
                _chargeX = Memory.ReadFloat(p); _chargeH = Memory.ReadFloat(p + 4); _chargeY = Memory.ReadFloat(p + 8);
            }
            else
            {
                _chargeTarget = -1;
                float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
                _chargeX = Memory.ReadFloat(Addresses.dunPositionX) + ChargeBoltAhead * (float)Math.Sin(yaw);
                _chargeY = Memory.ReadFloat(Addresses.dunPositionY) + ChargeBoltAhead * (float)Math.Cos(yaw);
                _chargeH = Memory.ReadFloat(Addresses.dunPositionZ);
            }
            JudgementBlade.PointHover(Judgement, _chargeTarget, _chargeX, _chargeH, _chargeY, ChargeHoverHeight, ridesPlayer: true);   // ahead of him: the cave carries it as he walks
        }
        private static void ChargeStandDown()
        {
            Memory.WriteFloat(CodeCaves.LungeGravityExtra, 0f);
            CameraHold.Unpin();                                                  // broken or spent: the camera follows again
            JudgementBlade.PointFade(); _chargeDropped = false; _chargeTarget = -1;     // a blade not yet let go fades out; one in the air is gone
            if (!SunSword.FlashArmed) BladeTint.Clear();                        // the charge's white off the blade (a primed flash keeps its own)
            if (_unlockZeroed) RestoreUnlock();
            if (_chargeDimming) { SceneLighting.EndDim(); _chargeDimming = false; }
            _chargeFull = false; _chargeBoltDue = false;
        }
        private static void ZeroUnlock()
        {
            _unlockWas = Memory.ReadInt(PlayerAction.WhirlwindUnlock);
            if (_unlockWas == 0) return;                                         // no whirlwind to take away: the release is a lunge anyway
            Memory.WriteInt(PlayerAction.WhirlwindUnlock, 0); _unlockZeroed = true;
        }
        private static void RestoreUnlock()
        {
            Memory.WriteInt(PlayerAction.WhirlwindUnlock, _unlockWas);
            _unlockZeroed = false;
        }

        /// <summary>Not locked on: a bolt on each of the nearest <see cref="MaxStrikes"/> live enemies within
        /// <see cref="StrikeReach"/> of the active character, nearest first, as far as the instance has sub-shots free; the
        /// volley billed once to <paramref name="weapon"/>. How many struck.</summary>
        internal static int StrikeNearest(ushort weapon = (ushort)Items.swordofzeus)
        {
            if (!LightningSeeded) return 0;
            float tx = Memory.ReadFloat(Addresses.dunPositionX), ty = Memory.ReadFloat(Addresses.dunPositionY);
            var near = new System.Collections.Generic.List<(float d, int slot)>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s) || Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) <= 0) continue;
                long pos = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(pos) - tx, dy = Memory.ReadFloat(pos + 8) - ty;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d <= StrikeReach) near.Add((d, s));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            int struck = 0;
            foreach (var (d, s) in near)
            {
                if (struck >= MaxStrikes) break;
                if (!Strike(s, bill: false)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] no free bolt for slot {s} ({d:F0} away)"); break; }
                struck++;
            }
            if (struck > 0) WeaponWhp.Drain(weapon, StrikeWhp, "[Zeus] volley ");   // the volley is ONE hit to the weapon, however many bolts
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] {struck} of {near.Count} enem" + (near.Count == 1 ? "y" : "ies") + $" within {StrikeReach:F0} struck");
            return struck;
        }

        /// <summary>Per tick while the Sword of Zeus is out in a dungeon: the lock-on reach, the charge, and the bolt's size.</summary>
        public static void LightningEffect()
        {
            byte floor = 0xFF;
            Memory.WriteInt(CodeCaves.NameHide, 0);                                       // the name-plate gate open, whatever a past run left
            while (Player.Weapon.GetCurrentWeaponId() == Items.swordofzeus && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try
                {
                    byte f = Memory.ReadByte(Addresses.checkFloor);
                    if (f != floor) { if (floor != 0xFF) { JudgementBlade.ReleaseJudgement(); CameraHold.Unpin(); } floor = f; }
                    ToanLockOn.HoldReach("[Zeus] ");                                  // Big Bang's reach, inherited
                    if (LightningSeeded) MaintainScale();
                    if (!Player.CheckDunIsPausedOrMenu()) { ChargeTick(); if (Player.CurrentCharacterNum() == Player.ToanId) JudgementBlade.JudgementTick(Judgement); }
                    BlastFalloff.ExpireShells();                                            // the bolt's blast entries, once spent
                    BladeRedirect.ReleaseRedirectWhenDue();
                }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] tick error: " + ex.Message); }
            }
            ChargeStandDown(); CameraHold.Unpin(); JudgementBlade.ReleaseJudgement(); BladeRedirect.ReleaseRedirect();
            ToanLockOn.ReleaseReach();
        }

        /// <summary>Hold every instance sub-shot that carries lightning.chr at <see cref="LightningScale"/> on its root
        /// frame's local 3×3 (translation left anchored). The authored bind is read once, from the first root seen at
        /// its authored size.</summary>
        internal static void MaintainScale()
        {
            if (!Player.CheckDunIsWalkingMode()) return;          // models are reallocated in menus and on transitions
            var seen = new System.Collections.Generic.List<string>(); bool matched = false;
            var state = new System.Collections.Generic.List<string>();
            for (int slot = 0; slot < ShotEffectPool.EffectSlotCount; slot++)
            {
                long obj = ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectSlotObjectsOff + (long)slot * ShotEffectPool.EffectSlotStride;
                uint ptr = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                if (!Memory.IsValidGuest(ptr)) continue;
                long root = Memory.ToMmu(ptr);
                if (!IsBoltRoot(root))
                {
                    if (DebugDiagnostics.Enabled)
                    {   // DIAGNOSTIC: what this sub-shot's model is called, for the one log line below
                        byte[] nm = Memory.ReadBytesBatch(root + CFrameVu1.Name, 8);
                        seen.Add($"#{slot}:{(nm == null ? "?" : System.Text.Encoding.ASCII.GetString(nm).TrimEnd('\0'))}");
                    }
                    continue;
                }
                matched = true;
                float m00 = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]);
                if (!_bindRead)
                {   // the authored bind is the identity; a root already scaled is a previous hold's, not the bind
                    if (Math.Abs(m00 - 1f) > 0.05f) continue;
                    for (int k = 0; k < 9; k++) _bind[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                    _bindRead = true;
                }
                if (DebugDiagnostics.Enabled) state.Add($"#{slot}: active {Memory.ReadUShort(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffActive + slot * 2)} charScale {Memory.ReadFloat(obj + CCharacter.CharScale):F2} m00 {m00:F2} root 0x{ptr:X}");
                if (Math.Abs(m00 - _bind[0] * LightningScale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _bind[k] * LightningScale);
                Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
                if (!_scaleLogged) { _scaleLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] bolt sub-shot #{slot} held ×{LightningScale:F0} on its root (null2 at 0x{ptr:X})"); }
            }
            if (DebugDiagnostics.Enabled)
            {
                // DIAGNOSTIC, once per whirl: every bolt sub-shot's state as the spin starts
                bool whirl = Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWhirlwind;
                if (whirl && !_whirlLogged && state.Count > 0) { _whirlLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] whirl sub-shots: " + string.Join(" | ", state)); }
                if (!whirl) _whirlLogged = false;
                if (!matched && seen.Count > 0 && !_rootsLogged)
                {   // DIAGNOSTIC, once: the instance holds models but none is the bolt — the whirl's object is not being reached
                    _rootsLogged = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] no lightning root among the sub-shots: " + string.Join(" ", seen));
                }
            }
        }
        private static bool _whirlLogged;
        private static bool _scaleLogged, _rootsLogged, _bindRead;
        private static readonly float[] _bind = new float[9];

    }
}
