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
        // not centred on the body. Two scale paths, one per owner: the STRIKE's sub-shot is the mod's, so it takes
        // the size the cutscene's way (the actor's own CCharacter scale, +0x90 — Burst's scale argument); the WHIRL's
        // sub-shot is the engine's, whose spin step owns that field, so it is held on its root frame's local 3×3 the
        // way the stock whirl and Big Bang's explosion.chr are. ⚠ Writing +0x90 on the engine-fired whirl object
        // reset the game mid-spin.
        private const float  LightningScale    = 3.0f;   // the cutscene draws it at 5; 3 sits better in a dungeon
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
            if (!BorrowedShots.Burst(_lightning, x, h, y, 0, LightningScale)) return false;
            _strikeSlot = Memory.ReadInt(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffLastIdx);   // its sub-shot: the root hold leaves it be
            SeSeq.Play(StrikeSe, 90);
            BeginConvulsion(slot);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] lightning on slot {slot} at ({x:F0},{h:F0},{y:F0})");
            return true;
        }

        // ── the electrocution ────────────────────────────────────────────────────────────
        // The struck enemy does not brace behind its guard like the rest of the floor: it CONVULSES. Nothing is forced
        // on it — the flash's own stagger (SolarScript's per-species label: _SET_MOTION(damage) for a few frames, then
        // the guard) already puts every unit of its species on its damage clip; once THIS unit's render object is
        // playing that clip, its playing frame is written up and back down a short run inside the clip's window
        // every tick, so the clip never reaches its end, the queued guard clip never commits, and the body twitches
        // instead of flinching. The release simply stops writing: the clip runs out and the engine commits the guard
        // clip by itself, exactly as it does for everyone else. Per-tick writes, deliberately: the jitter IS the look
        // (research §7a). ⚠ A FORCED commit (+0xF4) here reset the game at the next guard-lowering wave, twice.
        private const double ConvulseSeconds = 4.0;    // through the stun, ending as the floor starts lowering its guard
        private const float  ConvulseInto    = 2f;     // frames into the damage clip the run starts
        private const int    ConvulseFrames  = 3;      // consecutive frames the twitch runs over: 1 2 3 2 1 2 3 2 …
        private static int _convSlot = -1, _convClip = -1, _convStep; private static float _convA, _convMax; private static DateTime _convUntil;
        private static bool _convSeen;

        private static void BeginConvulsion(int slot)
        {
            _convSlot = -1;                                                     // one at a time: a new strike takes over
            ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(eid, out var def) || !def.TableIndex.HasValue) return;
            if (!EnemyGuardMotions.TryGet(def.TableIndex.Value, out var g) || g.Damage < 0) return;   // no damage clip authored: the guard hold stands
            float lo = g.DamageStart, hi = Math.Max(g.DamageStart, g.DamageEnd);
            _convA = Math.Min(hi, lo + ConvulseInto); _convMax = hi; _convClip = g.Damage;
            _convSlot = slot; _convStep = 0; _convSeen = false; _convUntil = GameClock.Now.AddSeconds(ConvulseSeconds);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] slot {slot} convulsing on clip {g.Damage} over frames {_convA:F0}..{Math.Min(_convMax, _convA + ConvulseFrames - 1):F0} once it plays");
        }
        /// <summary>Per tick: nothing until the unit is on its damage clip; then the frame walked up and back down the
        /// run one frame at a time, never skipping one; done when the time is up or the enemy is dead.</summary>
        private static void ConvulseTick()
        {
            if (_convSlot < 0) return;
            bool alive = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(_convSlot, EnemySlotOffsets.Hp)) > 0 && Enemies.IsLive(_convSlot);
            if (!alive || GameClock.Now >= _convUntil) { _convSlot = -1; return; }
            long unit = EnemyAddresses.MainMonstorUnit.Base + (long)_convSlot * ModelScaleOffsets.ModelStride;
            if (Memory.ReadInt(unit + ModelScaleOffsets.PlayingMotionIdFromUnit) != _convClip)
            {
                if (_convSeen) _convSlot = -1;                                  // it left the clip on its own (a kill, a knockback): done
                return;                                                         // …or the stagger has not reached it yet: wait
            }
            _convSeen = true;
            int period = 2 * (ConvulseFrames - 1);                              // 0 1 2 1 for three frames
            int k = _convStep++ % period; if (k >= ConvulseFrames) k = period - k;
            Memory.WriteFloat(unit + ModelScaleOffsets.PlayingMotionFrameFromUnit, Math.Min(_convMax, _convA + k));
        }
        private static void EndConvulsion() => _convSlot = -1;

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
                }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] tick error: " + ex.Message); }
            }
            EndConvulsion();
            ToanLockOn.ReleaseReach(); ToanLockOn.ReleaseSpeed("[Zeus] "); ToanLockOn.DriveStride(false, "[Zeus] ");
        }

        /// <summary>Hold the engine-fired sub-shots that carry lightning.chr at <see cref="LightningScale"/> on their
        /// root frame's local 3×3 (translation left anchored — a VERTEX_ANIME mesh is only transformed by its root's
        /// local matrix); the strike's own sub-shot, scaled through its CCharacter, is left alone.</summary>
        private static void MaintainScale()
        {
            if (!Player.CheckDunIsWalkingMode()) return;          // models are reallocated in menus and on transitions
            if (_strikeSlot >= 0 && Memory.ReadUShort(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffActive + _strikeSlot * 2) == 0)
                _strikeSlot = -1;                                  // the strike has played out: its index is anyone's again
            var seen = new System.Collections.Generic.List<string>(); bool matched = false;
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
                // The strike's sub-shot carries its size on its own CCharacter scale (Burst's argument); anything the
                // engine fired is at 1. Told apart by that, not by index — the index is only known after Burst returns,
                // and a tick in between put the root hold on top of the strike's own scale (25×).
                if (Math.Abs(Memory.ReadFloat(obj + CCharacter.CharScale) - 1f) > 0.01f) continue;
                if (!_bindRead)
                {
                    for (int k = 0; k < 9; k++) _bind[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                    if (Math.Abs(_bind[0]) < 0.05f || Math.Abs(_bind[0]) > 4.0f) return;   // already scaled, or a bad read
                    _bindRead = true;
                }
                if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - _bind[0] * LightningScale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _bind[k] * LightningScale);
                Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
                if (!_scaleLogged) { _scaleLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Zeus] whirl sub-shot #{slot} held ×{LightningScale:F0} on its root (null2 at 0x{ptr:X})"); }
            }
            if (!matched && seen.Count > 0 && !_rootsLogged)
            {   // DIAGNOSTIC, once: the instance holds models but none is the bolt — the whirl's object is not being reached
                _rootsLogged = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Zeus] no lightning root among the sub-shots: " + string.Join(" ", seen));
            }
        }
        private static bool _scaleLogged, _rootsLogged, _bindRead;
        private static readonly float[] _bind = new float[9];
        private static int _strikeSlot = -1;

    }
}
