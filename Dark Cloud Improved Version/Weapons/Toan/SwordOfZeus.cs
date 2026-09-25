using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sword of Zeus — Solar Harvest and Solar Flash from the Sun Sword line, with lightning: the whirlwind
    /// IS the cat-zap bolt (`gedit/s99/chara/lightning.chr`, the scene actor the Divine Beast Cave cutscene strikes
    /// the cat with), and a primed flash swung while locked on brings that bolt down on the locked target.
    /// ⚠ The LOOK only, so far: the bolt does no damage of its own here (the whirl's own hit is untouched, the flash
    /// is the Sun Sword's), and nothing is stunned or chained — that is the next iteration.</summary>
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
        // lightning.mds's root is `null2`. ⚠ Only its FIRST FIVE bytes are the name at runtime: the word after "null"
        // read `2 ??` on the live copy (whatever followed the NUL in the frame's name field), where explosion.chr's
        // `null3` happened to be NUL-padded — an 8-byte compare never matched and the whirl stayed 1×.
        private const uint   RootWord = 0x6C6C756E;                          // "null"
        private const byte   RootDigit = (byte)'2';
        private static bool IsBoltRoot(long root) =>
            Memory.ReadUInt(root + CFrameVu1.Name) == RootWord && Memory.ReadByte(root + CFrameVu1.Name + 4) == RootDigit;
        private static BorrowedEffect _lightning;

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

        /// <summary>The bolt on the locked target: a sub-shot of the instance played once where the enemy's body is,
        /// no damage. False when the bolt is not entered on this floor or every sub-shot is busy.</summary>
        internal static bool Strike(int slot)
        {
            if (!LightningSeeded || slot < 0 || slot >= EnemyAddresses.FloorSlots.Count || !Enemies.IsLive(slot)) return false;
            long pos = EnemyAddresses.CharObjects.PosAddr(slot);                    // the unit's own position: its ground point
            float x = Memory.ReadFloat(pos), h = Memory.ReadFloat(pos + 4), y = Memory.ReadFloat(pos + 8);
            if (!BorrowedShots.Burst(_lightning, x, h, y, 0, 1f)) return false;   // 1×: the root hold sizes every bolt alike
            MaintainScale();                                                        // …before its first frame
            SeSeq.Play(StrikeSe, 90);
            // Every enemy's eyes on the bolt, the way Big Bang's landing has them: the blast point for the flash's own
            // light hit, the facing hold, and the pointer-table redirect through the blinding.
            BigBang.LastBlast = (x, h, y);
            BigBang.TurnEnemiesToward(x, y);
            // ⚠ BISECT (temporary): the redirect is not armed on the strike. Every reset so far has landed on the blind's END,
            // and the redirect's release (0.4 s before it) is the one Zeus-only thing that runs there; Big Bang's copy of it
            // has never actually been exercised. Restore the BeginRedirect call once cleared.
            // BigBang.BeginRedirect(x, h, y);
            BeginConvulsion(slot);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning on slot {slot} at ({x:F0},{h:F0},{y:F0})");
            return true;
        }

        // ── the electrocution ────────────────────────────────────────────────────────────
        // The struck enemy does not brace behind its guard like the rest of the floor: it CONVULSES. Its own script is
        // pointed at a program in the mod's reserved AI-stub slot (SolarScript.Convulse) that cancels movement, issues
        // its damage clip once, and then — one step per engine frame, through the engine's own _SET_MOTION_FRM — walks
        // the clip's playing frame up and back down ConvulseFrames consecutive frames from the MIDDLE of the clip
        // (1 2 3 4 5 4 3 2 1 2 …) until it is let go. Nothing is written to the unit per tick and no label needs room for it. The release re-enters the
        // label at its top — the guard — for the wind-down, like everyone else.
        // WHEN it is pointed matters: the flash's own hit lands after the strike and runs the hit-reaction label in the
        // unit's script slot, and when that finishes the AI label is entered from its top — the guard — which threw an
        // early pointing away (the unit flinched on its damage clip, then braced like the rest). So the program is
        // pointed once the unit is on its hold clip, and pointed again whenever its PC is found outside the program.
        // ⚠ Touching the motion REQUEST word (+0xEC), with or without the forced commit (+0xF4), reset the game at the
        // next guard-lowering wave, three runs out of three; and writing the playing frame from the mod while the
        // script yielded showed nothing (the motion player put its own frame back each step). Both are gone.
        private const double ConvulseSeconds = 4.0;    // through the stun, ending as the floor starts lowering its guard
        private const int    ConvulseFrames  = 5;      // consecutive frames the walk covers, centred in the clip: 8 steps up and back
        private const double ConvulseLatest  = 1.0;    // seconds after the strike by which it is pointed even without seeing the hold clip
        private static int _convSlot = -1, _convClip = -1, _convHold = -1; private static float _convA, _convB; private static DateTime _convUntil, _convFrom;
        private static bool _convPointed;

        private static void BeginConvulsion(int slot)
        {
            EndConvulsion();                                                    // one at a time: a new strike takes over
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return;
            if (!EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g) || g.Damage < 0) return;   // no damage clip authored: the guard hold stands
            float lo = g.DamageStart, hi = Math.Max(g.DamageStart, g.DamageEnd);
            _convA = Math.Max(lo, (float)Math.Round((lo + hi) / 2) - (ConvulseFrames - 1) / 2);
            _convB = Math.Min(hi, _convA + ConvulseFrames - 1); _convClip = g.Damage;
            _convHold = g.HasGuard ? g.Loop : g.Idle;                            // what the flash's hold puts it on
            _convSlot = slot; _convPointed = false; _convFrom = GameClock.Now; _convUntil = _convFrom.AddSeconds(ConvulseSeconds);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] slot {slot} to convulse on clip {g.Damage} over frames {_convA:F0}..{_convB:F0}");
        }
        /// <summary>Per tick: point the unit at its program once the flash's hold has it on its hold clip (or after
        /// ConvulseLatest regardless), point it again if its script has been taken elsewhere since, and let go when the
        /// time is up or the enemy is dead.</summary>
        private static void ConvulseTick()
        {
            if (_convSlot < 0) return;
            bool alive = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_convSlot, EnemySlotOffsets.Hp)) > 0 && Enemies.IsLive(_convSlot);
            if (!alive || GameClock.Now >= _convUntil) { EndConvulsion(); return; }
            if (!SolarScript.Active) { if (_convPointed) EndConvulsion(); return; }   // the hold comes up a tick after the strike; if it is gone, let go
            if (_convPointed)
            {
                if (SolarScript.Convulsing(_convSlot)) return;
                _convPointed = false;                                           // a hit reaction took its script: wait for the guard, then again
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] slot {_convSlot} left its program — pointing again once it holds");
            }
            long unit = EnemyAddresses.MainMonstorUnit.Base + (long)_convSlot * ModelScaleOffsets.ModelStride;
            bool onHold = Memory.ReadInt(unit + ModelScaleOffsets.PlayingMotionIdFromUnit) == _convHold;
            if (!onHold && GameClock.Now < _convFrom.AddSeconds(ConvulseLatest)) return;
            if (SolarScript.Convulse(_convSlot, _convClip, _convA, _convB))
            { _convPointed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] slot {_convSlot} convulsing" + (onHold ? "" : " (hold clip not seen)")); }
            else { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] slot {_convSlot}: not held by the flash — it braces"); _convSlot = -1; }
        }
        private static void EndConvulsion()
        {
            if (_convSlot < 0) return;
            if (_convPointed) SolarScript.Unconvulse(_convSlot);       // back to the guard for the wind-down
            _convSlot = -1;
        }

        /// <summary>Per tick while the Sword of Zeus is out in a dungeon: the lock-on reach and speed, and the bolt's size.</summary>
        public static void LightningEffect()
        {
            while (Player.Weapon.GetCurrentWeaponId() == Items.swordofzeus && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try
                {
                    ToanLockOn.HoldReach("[Zeus] ");                                  // Big Bang's reach, inherited
                    bool moving = !Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest();
                    ToanLockOn.DriveSpeed(moving, "[Zeus] ");
                    ToanLockOn.DriveStride(moving, "[Zeus] ");
                    if (LightningSeeded) MaintainScale();
                    ConvulseTick();
                    BigBang.FaceTick();
                    BigBang.ReleaseRedirectWhenDue();
                }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] tick error: " + ex.Message); }
            }
            EndConvulsion(); BigBang.ReleaseRedirect();
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
