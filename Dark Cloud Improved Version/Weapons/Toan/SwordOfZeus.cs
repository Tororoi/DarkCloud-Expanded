using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sword of Zeus — Solar Harvest and Solar Flash from the Sun Sword line, with lightning: the whirlwind
    /// IS the cat-zap bolt (`gedit/s99/chara/lightning.chr`, the scene actor the Divine Beast Cave cutscene strikes
    /// the cat with), and the primed flash brings that bolt down on enemies — on the locked target with every swing
    /// of the combo while locked on, or on each of the nearest <see cref="MaxStrikes"/> within <see cref="StrikeReach"/>
    /// when not. A bolt's damage is Big Bang's falloff blast at its foot at half its steps (the bolt itself touches nothing); the flash
    /// carries no hit of its own (SunSword.ZeusFlash) and stuns the floor the way the Sun Sword's does. A full CHARGE
    /// attack is a bolt as well (<see cref="ChargeTick"/>): the room darkens as the meter fills, the level-2 release
    /// plays the LUNGE, and the bolt comes down ahead of Toan as its clip ends its dash.</summary>
    internal static class SwordOfZeus
    {
        private const int    TickMs = 30;
        // ── the bolt ────────────────────────────────────────────────────────────────────
        // Toan's whirlwind visual IS the main-character effect instance, so seeding it with lightning.chr REPLACES the
        // whirl with the bolt and the engine fires it on the spin by itself (the same borrowing Big Bang does with
        // explosion.chr). The container is the cutscene's scene actor, loaded by path from its own directory — the
        // loader takes any container whose cfg record carries its name. Its motion list is two KEYs over the same
        // frames 10-30: KEY 0 at speed 0 (a held pose, 止め) and KEY 1 at 0.2 — the strike. Only the MUZZLE phase names
        // a motion, the whirl's own shape (c01_fuusya is "muzzle motion 0, nothing after").
        private const int    LightningTemplate = 5;                  // a stock config's shape; the name and motions are replaced
        private const string LightningName     = "lightning", LightningDir = "gedit/s99/chara/";
        private const short  StrikeMotion      = 1;
        // THE CUTSCENE'S OWN STRIKE (dun/script/d01/event.stb label 90, the cat zapped into an atla): the actor is
        // loaded hidden at _SET_NPC_SCALE(5, 5) holding motion 0, then at the moment — _PLAY_SE(390), _NPC_DRAW(1, 5),
        // _SET_NPC_MOTION(5, 1) played through, _SET_NPC_POS(5, x, 0, y) at the cat's GROUND point, _NPC_DRAW(0, 5).
        // Its cards rise from the origin (nodes at +4.5 and +10 up), so it stands ON the ground under the target,
        // not centred on the body. ONE scale path for every bolt, strike or whirl: the root frame's local 3×3, held
        // the way the stock whirl and Big Bang's explosion.chr are (a VERTEX_ANIME mesh is only transformed by its
        // root's local matrix). ⚠ Writing the CCharacter scale (+0x90) on the engine-fired whirl object reset the
        // game mid-spin; and telling the strike's object from the whirl's by that field failed too — the engine
        // re-fires the whirl into whichever sub-shot is free, scale and all.
        private const float  LightningScale    = 10.0f;   // on the root hold; the cutscene's ×5 on the actor scale read about half this
        private const ushort StrikeSe          = 390;   // the cutscene's thunderclap
        private const float  StrikeReach       = 300f;  // not locked on: enemies this far from Toan are in reach (the flash's radius, about the draw distance)
        private const int    MaxStrikes        = 6;     // …and this many of the nearest take a bolt each
        private const float  BlastScale        = 0.5f;  // the bolt's blast, against Big Bang's falloff steps (½× … 2× attack)
        private const float  StrikeWhp         = 5f;    // weapon HP a bolt costs, before the weapon's Endurance scales it down (WeaponWhp)
        // lightning.mds's root is `null2`. ⚠ Only its FIRST FIVE bytes are the name at runtime: the word after "null"
        // read `2 ??` on the live copy (whatever followed the NUL in the frame's name field), where explosion.chr's
        // `null3` happened to be NUL-padded — an 8-byte compare never matched and the whirl stayed 1×.
        private const uint   RootWord = 0x6C6C756E;                          // "null"
        private const byte   RootDigit = (byte)'2';
        private static bool IsBoltRoot(long root) =>
            Memory.ReadUInt(root + CFrameVu1.Name) == RootWord && Memory.ReadByte(root + CFrameVu1.Name + 4) == RootDigit;
        private static BorrowedEffect _lightning;

        /// <summary>The judgement blade, for this sword: hung over the locked target while primed (BigBang.JudgementTick),
        /// with the red ring for its glow, the fall darkening from this sword's dim, no enemy turned to watch it. Let go
        /// as the primed combo's FIRST swing begins, paced by the swing so it is in the ground to the HILT at the
        /// swing's hit frame; there it is gone at once and the bolt comes down on the target.</summary>
        internal static readonly BigBang.JudgementOwner Judgement = new BigBang.JudgementOwner
        {
            WeaponId = Items.swordofzeus, Glow = ToanGlowBakes.ZeusName, Profile = SunSword.ZeusFlash, Redirect = false, ToTheHilt = true,
            Land = (slot, x, h, y) => { if (!StrikeAt(x, h, y, slot)) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] the blade landed but no bolt was free"); },
        };

        /// <summary>What BorrowedShots seeds the instance with while Toan carries the Sword of Zeus: the bolt.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.ToanId) return null;
            if (Player.Weapon.GetCurrentWeaponId() != Items.swordofzeus) return null;
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

        /// <summary>The bolt on one enemy: the strike played at its ground point, its blast, its cost. False when the
        /// bolt is not entered on this floor or every sub-shot is busy.</summary>
        internal static bool Strike(int slot)
        {
            if (slot < 0 || slot >= EnemyAddresses.FloorSlots.Count || !Enemies.IsLive(slot)) return false;
            long pos = EnemyAddresses.CharObjects.PosAddr(slot);                    // the unit's own position: its ground point
            if (!StrikeAt(Memory.ReadFloat(pos), Memory.ReadFloat(pos + 4), Memory.ReadFloat(pos + 8), slot)) return false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning on slot {slot}");
            return true;
        }

        /// <summary>The bolt at a point: a sub-shot of the instance played once there (no damage of its own), Big
        /// Bang's falloff blast at the point, and the bolt's weapon-HP cost. <paramref name="noKickSlot"/> is the enemy
        /// under it, which takes the blast where it stands — no shove; nobody is turned to face the bolt, they are
        /// stunned facing wherever they were, and the struck enemy braces behind its guard like the rest of the floor.</summary>
        private static bool StrikeAt(float x, float h, float y, int noKickSlot)
        {
            if (!LightningSeeded) return false;
            if (!BorrowedShots.Burst(_lightning, x, h, y, 0, 1f)) return false;   // 1×: the root hold sizes every bolt alike
            MaintainScale();                                                        // …before its first frame
            SeSeq.Play(StrikeSe, 90);
            BigBang.LastBlast = (x, h, y);
            BigBang.PlantFalloff(x, h, y, noKickSlot: noKickSlot, damageScale: BlastScale);
            WeaponWhp.Drain(Items.swordofzeus, StrikeWhp, "[Zeus] bolt ");
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning at ({x:F0},{h:F0},{y:F0})");
            return true;
        }

        // ── the charge attack ───────────────────────────────────────────────────────────
        // Charge level 2 is DISABLED while Toan charges with this sword: the whirlwind-unlock word ToanKey_Play reads
        // is zeroed for the length of the wind-up (and put back the moment it is over — it is save data), so the game
        // never reaches its own level 2 and every release is the LUNGE. The sword keeps its own level 2 instead: the
        // room darkens with the meter from the moment he starts charging, the way the guard charge darkens it, to the
        // Zeus profile's prime dim at whirlwind range, where the stock charge-complete flash fires on him and the
        // release is flagged as a bolt. The lunge runs through five action states (PlayerAction.InLunge); the tick the
        // END one (ActionLungeEnd: clip 17 from frame 196) comes up, the bolt comes down ChargeBoltAhead units ahead
        // of Toan with the strike's blast, and the flash goes off — the same white and
        // pulse as every strike, but easing back to the floor's own light, with no blinding behind it. A charge let go
        // early plays whatever it earned and the dim simply lifts. Stands aside while Solar Flash owns the blade.
        private const float  ChargeBoltAhead   = 30f;   // units ahead of Toan the bolt lands
        private const float  ChargeDimFrom     = 1.0f;  // the meter as the charge starts (ToanKey_On resets it to 1.0) …
        private const float  ChargeDimTo       = 2.5f;  // … and at whirlwind range: the dim is full here
        private static bool  _chargeDimming, _chargeFull, _chargeBoltDue, _chargeBoltFired, _unlockZeroed;
        private static int   _unlockWas;

        private static void ChargeTick()
        {
            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            if (SunSword.FlashArmed) { ChargeStandDown(); return; }
            if (action == PlayerAction.ActionWindup)
            {
                float meter = Memory.ReadFloat(PlayerAction.ChargeMeter);
                float k = Math.Max(0f, Math.Min(1f, (meter - ChargeDimFrom) / (ChargeDimTo - ChargeDimFrom)));
                if (!_chargeDimming) { _chargeDimming = true; _chargeFull = false; _chargeBoltDue = false; ZeroUnlock(); }   // no level 2 for the game this wind-up
                if (!SolarLighting.Active) { SolarLighting.BeginDim(); SolarLighting.DimTo(SunSword.ZeusFlash.PrimeDim * k); }   // a flash still easing keeps its course
                if (meter >= ChargeDimTo && !_chargeFull)
                {   // the sword's own level 2: the stock charge-complete flash on him, and the release flagged as a bolt
                    _chargeFull = true;
                    Player.FlashChargeComplete();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] charge level 2 — the release is a bolt");
                }
                return;
            }
            if (_unlockZeroed) RestoreUnlock();                                 // the release has been read: the word goes back at once
            if (!_chargeDimming) return;
            if (PlayerAction.InLunge(action))
            {
                if (!_chargeFull) { ChargeStandDown(); return; }                // a level-1 lunge: nothing more to it
                if (!_chargeBoltDue) { _chargeBoltDue = true; _chargeBoltFired = false; }
                if (_chargeBoltFired || action != PlayerAction.ActionLungeEnd) return;
                _chargeBoltFired = true;
                float cursor = Memory.ReadFloat(PlayerAction.AnimFrameCursor);
                float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
                float x = Memory.ReadFloat(Addresses.dunPositionX) + ChargeBoltAhead * (float)Math.Sin(yaw);
                float y = Memory.ReadFloat(Addresses.dunPositionY) + ChargeBoltAhead * (float)Math.Cos(yaw);
                float h = Memory.ReadFloat(Addresses.dunPositionZ);
                if (!StrikeAt(x, h, y, -1)) { ChargeStandDown(); return; }
                var p = SunSword.ZeusFlash;
                p.ArmLighting(rest: false);                                      // the white, then the floor's own light — no blinding
                SolarLighting.Flash();
                Player.FlashActiveCharacter(p.Light[0], p.Light[1], p.Light[2], SunSword.FlashPulseSpeed, 1);
                _chargeDimming = false; _chargeFull = false; _chargeBoltDue = false;   // the flash took the dim over
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] charge bolt ahead of Toan (yaw {yaw:F2}, cursor {cursor:F1})");
                return;
            }
            ChargeStandDown();                                                   // the charge went another way, or is over
        }
        private static void ChargeStandDown()
        {
            if (_unlockZeroed) RestoreUnlock();
            if (_chargeDimming) { SolarLighting.EndDim(); _chargeDimming = false; }
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
        /// <see cref="StrikeReach"/> of Toan, nearest first, as far as the instance has sub-shots free. How many struck.</summary>
        internal static int StrikeNearest()
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
                if (!Strike(s)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] no free bolt for slot {s} ({d:F0} away)"); break; }
                struck++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] {struck} of {near.Count} enem" + (near.Count == 1 ? "y" : "ies") + $" within {StrikeReach:F0} struck");
            return struck;
        }

        /// <summary>Per tick while the Sword of Zeus is out in a dungeon: the lock-on reach and speed, and the bolt's size.</summary>
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
                    if (f != floor) { if (floor != 0xFF) BigBang.ReleaseJudgement(); floor = f; }
                    ToanLockOn.HoldReach("[Zeus] ");                                  // Big Bang's reach, inherited
                    bool moving = !Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest();
                    ToanLockOn.DriveSpeed(moving, "[Zeus] ");
                    ToanLockOn.DriveStride(moving, "[Zeus] ");
                    if (LightningSeeded) MaintainScale();
                    if (!Player.CheckDunIsPausedOrMenu()) { ChargeTick(); if (Player.CurrentCharacterNum() == Player.ToanId) BigBang.JudgementTick(Judgement); }
                    BigBang.ExpireShells();                                            // the bolt's blast entries, once spent
                    BigBang.ReleaseRedirectWhenDue();
                }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] tick error: " + ex.Message); }
            }
            ChargeStandDown(); BigBang.ReleaseJudgement(); BigBang.ReleaseRedirect();
            ToanLockOn.ReleaseReach(); ToanLockOn.ReleaseSpeed("[Zeus] "); ToanLockOn.DriveStride(false, "[Zeus] ");
        }

        /// <summary>Hold every instance sub-shot that carries lightning.chr at <see cref="LightningScale"/> on its root
        /// frame's local 3×3 (translation left anchored). The authored bind is read once, from the first root seen at
        /// its authored size.</summary>
        private static void MaintainScale()
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
                {   // DIAGNOSTIC: what this sub-shot's model is called, for the one log line below
                    byte[] nm = Memory.ReadBytesBatch(root + CFrameVu1.Name, 8);
                    seen.Add($"#{slot}:{(nm == null ? "?" : System.Text.Encoding.ASCII.GetString(nm).TrimEnd('\0'))}");
                    continue;
                }
                matched = true;
                float m00 = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]);
                if (!_bindRead)
                {   // the authored bind is the identity; anything else is a root a previous hold (this session's or an earlier
                    // mod run's, PCSX2 running on) left scaled — not the bind
                    if (Math.Abs(m00 - 1f) > 0.05f) continue;
                    for (int k = 0; k < 9; k++) _bind[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                    _bindRead = true;
                }
                state.Add($"#{slot}: active {Memory.ReadUShort(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffActive + slot * 2)} charScale {Memory.ReadFloat(obj + CCharacter.CharScale):F2} m00 {m00:F2} root 0x{ptr:X}");
                if (Math.Abs(m00 - _bind[0] * LightningScale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _bind[k] * LightningScale);
                Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
                if (!_scaleLogged) { _scaleLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] bolt sub-shot #{slot} held ×{LightningScale:F0} on its root (null2 at 0x{ptr:X})"); }
            }
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
        private static bool _whirlLogged;
        private static bool _scaleLogged, _rootsLogged, _bindRead;
        private static readonly float[] _bind = new float[9];

    }
}
