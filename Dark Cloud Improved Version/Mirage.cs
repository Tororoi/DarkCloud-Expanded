using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ungaga's "Mirage" (weapon 354): releasing a charge plants a stationary DECOY at the player's spot.
    /// While it lives (10s, refreshed by a new charge), enemies path toward the decoy instead of the
    /// player — PER ENEMY, so an enemy you HIT drops the illusion and re-targets you.
    ///
    /// Hard-won implementation (6 crashes): two independent PCSX2 limits. (1) Patching HOT, actively-
    /// executing EE code crashes the recompiler → the _GET_POSITION patch is applied ONLY at the COLD
    /// window (in-game entry, before any enemy has called it — same window the ABS patches use).
    /// (2) Executing native code from a PINE-written CAVE crashes when a fresh path is first hit → no
    /// cave stub; the redirect is a 5-word IN-PLACE rewrite (runs in _GET_POSITION's own compiled block)
    /// that reads the player pointer from a per-slot DATA table. The rewrite is UNCONDITIONAL, so a fast
    /// loop keeps the table = the live player position for un-fooled slots; fooled slots hold the decoy.
    /// </summary>
    internal static class Mirage
    {
        private const double DecoySeconds = 12.0;
        private const int    FastTickMs   = 25;    // table maintenance cadence while armed + in a dungeon
        private const int    IdleTickMs   = 150;
        // Guard-hold motions. Ungaga AND Xiao both use 9 (guard loop) / 33 (guard move) — see
        // docs/character-motion-table.md — so the same trigger and hold-pose work for either wielder.
        private const int    GuardLoopMotion = 9;   // guard-hold loop (spawn here, not on guard-enter)
        private const int    GuardMoveMotion = 33;  // guard-while-moving; the hold pose oscillates 9<->33 under R1
        private const int    GuardChargeMs   = 250; // hold the guard pose this long before the flash + decoy fire

        // ── What MIRAGE chooses (as opposed to what the game dictates) ───────────────────────────────
        // Engine struct layouts live in CCharacter/CFrameVu1/CCloth/...; cave addresses AND their capacities
        // live in CodeCaves. These are the mod's own decisions.
        private const int MaxSlots = 20;      // enemy slots the mod actively manages (FloorSlots is 16)

        /// <summary>Copy ONLY the draw-relevant CCharacter fields (through the light block @0xD60). The chara
        /// slot's own gate fields begin further in, so copying the player's FULL object clobbers them and
        /// corrupts the instance (it teleported the player off-map). 0xD60 is the safe cut.</summary>

        /// <summary>The clone's WEAPON draws in its own chara slot rather than being grafted into the body tree
        /// (which would share the body's texture pass → wrong texture). Slot 3, because the per-chara texgroup
        /// formula is patched to (i*4 + 0x11): chara[0] = 0x11 (body), chara[3] = 0x1D (weapon). Drawn but NOT
        /// stepped (DungeonCharaDraw.StepSkipTable).</summary>


        /// <summary>Pose the clone. During a re-cast hand-off the instance is still the OUTGOING clone, so it
        /// stays parked at the old pose while it dissolves — _dx/_dy already point at the NEW decoy (which the
        /// shimmer is ramping up on). Choosing the pose is the CALLER's job; CharacterClone just renders it.</summary>
        private static void PoseClone(bool spawn = false)
        {
            float cx   = _handoff ? _oldDx  : _dx;
            float cz   = _handoff ? _oldDz  : _dz;
            float cy   = _handoff ? _oldDy  : _dy;
            float cyaw = _handoff ? _oldYaw : _decoyYaw;
            CharacterClone.HoldMotion = GuardLoopMotion;   // hold the guard pose regardless of what the player does
            if (spawn) CharacterClone.Spawn(cx, cz, cy, cyaw, CloneAlpha());
            else       CharacterClone.Maintain(cx, cz, cy, cyaw, CloneAlpha());
        }


        /// <summary>Is the Mirage ability currently wielded? Two ways in:
        ///   • UNGAGA holding <b>Mirage</b> or <b>Hercules' Wrath</b> (the spear line that grants it), or
        ///   • XIAO holding <b>Super Steve</b> with a Mirage / Hercules' Wrath SynthSphere attached — the
        ///     standard Super Steve inheritance (the sphere's SOURCE weapon id selects the effect).
        ///
        /// Both wielders work unchanged because Ungaga and Xiao share the guard motions this triggers on
        /// (9 / 33), and Xiao fits the clone's mesh cave. Mirage is NOT driven from CustomXiaoEffects'
        /// SuperSteveEffect hub — it owns a thread and a state machine (guard charge → decoy → clone → haze),
        /// so it gates itself here rather than being pulsed per-tick like the stateless abilities.</summary>
        private static bool MirageArmed()
        {
            int ch = Player.CurrentCharacterNum();

            if (ch == Player.UngagaId)
            {
                int w = Player.Weapon.GetCurrentWeaponId();
                return w == Items.mirage || w == Items.herculeswrath;
            }

            if (ch == Player.XiaoId)
            {
                int equipSlot = Memory.ReadByte(UserStatus.Base +
                                                UserStatus.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) return false;
                long rec = UserStatus.WeaponRecord(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) return false;
                int sphere = SuperSteveAbilities.AttachedSphere(rec);
                return sphere == Items.mirage || sphere == Items.herculeswrath;
            }

            return false;
        }


        // ── Character-swap safety ───────────────────────────────────────────────────────────────────
        // A clone is a deep copy of ONE character's frame tree, and it SHARES that model's geometry pointers
        // (CFrameVu1.GeomPtr is deliberately shared, never duplicated). Swap the party member and the source
        // model is unloaded/reused — those pointers dangle, and the engine happily walks the garbage. If the
        // walk finds a cycle, its draw loops forever: a hard freeze, not a crash.
        //
        // This only became reachable when Xiao (Super Steve + sphere) joined Ungaga as a valid wielder: before,
        // switching away made MirageArmed() false and the decoy tore itself down as a side effect.
        private static int _decoyChar = -1;   // the character the live decoy/clone was built from

        // The swap is also LAGGED: CurrentCharacterNum() flips to the new id SEVERAL FRAMES before the model at
        // +0xBC actually swaps. Casting inside that window would deep-copy the previous character's (or
        // transitional garbage) tree. So require a settled (character, modelRoot) pair before allowing a cast.
        private const int CharSettleMs = 400;
        private static int      _seenChar = -1;
        private static uint     _seenRoot;
        private static DateTime _seenSince;

        private static bool CharacterSettled()
        {
            int  ch   = Player.CurrentCharacterNum();
            uint root = (uint)Memory.ReadInt(CCharacter.Base + CCharacter.CharModel) & Memory.PhysAddrMask;
            if (ch != _seenChar || root != _seenRoot)
            {
                _seenChar = ch; _seenRoot = root; _seenSince = DateTime.UtcNow;
                return false;
            }
            if (!Memory.IsValidGuest(root)) return false;
            return DateTime.UtcNow - _seenSince >= TimeSpan.FromMilliseconds(CharSettleMs);
        }

        /// <summary>Tear the decoy + clone + shimmer down. Used on expiry, weapon swap, floor exit, party swap.</summary>
        private static void EndDecoy()
        {
            _decoyActive = false; _handoff = false; _aggroHoldUntil = default; _decoyChar = -1;
            Array.Clear(_fooled, 0, _fooled.Length);
            CharacterClone.Despawn();
            HeatHaze.Hide();
        }

        private static bool _armed;                 // both caves hosted + dispatch repointed this session (cold)

        private static bool     _decoyActive;
        private static DateTime _decoyDeadline;
        private static float    _dx, _dz, _dy;
        private static readonly bool[] _fooled          = new bool[MaxSlots];
        private static readonly bool[] _brokenThisDecoy = new bool[MaxSlots];
        private static int[] _prevHp;

        internal static void Start() => new Thread(Loop) { IsBackground = true }.Start();

        /// <summary>Arm both engine redirects at the COLD window (from ApplyNewChanges, retried from the loop).
        /// _GET_POSITION and _GET_DISTANCE are hosted in COLD-PINE CAVES reached via the STB external-command
        /// dispatch table — a pure DATA path (see docs/cave-code-execution.md), so there's no in-place code
        /// surgery and nothing to defer. Fill the per-slot pointer table first so the first enemy read is valid.</summary>
        internal static void ArmColdPatch()
        {
            if (_armed) return;
            FillWholeTablePlayer();                 // every slot → live-player pointer (valid before any enemy read)
            bool pos  = ArmDecoyCave("_GET_POSITION", StbExternCmd.GetPositionFn, CodeCaves.PosCave,  CodeCaves.PosCaveGuest,
                                     StbExternCmd.GetPositionSlot,  StbExternCmd.PosPlayerLdOff,  StbExternCmd.PosCopyJalOff);
            bool dist = ArmDecoyCave("_GET_DISTANCE", StbExternCmd.GetDistanceFn, CodeCaves.DistCave, CodeCaves.DistCaveGuest,
                                     StbExternCmd.GetDistanceSlot, StbExternCmd.DistPlayerLdOff, StbExternCmd.DistCopyJalOff);
            _armed = pos && dist;                   // retry from the loop if either couldn't arm (e.g. not vanilla yet)
        }


        // ── Clone heat-haze by HIJACKING an existing torch's fire-raster (pure data; no cave, no crash) ──
        // The ONLY framebuffer distortion in the game is CFireOmni::DrawRaster (0x162310, via
        // blendTextuerTest + MGGetFBuffTex); it's driven by DrawRaster__11CDungeonMap (0x1C4610), which
        // iterates the 20×20 fire-tile array at dngMap+0x9C50 (0x10/entry: +0=fireIdx, +4=rot,
        // +8=dist(≤240 draws), +C=enabled) and, per enabled tile, draws the raster emitters of the fire
        // struct at dngMap+fireIdx*0x1D0 (raster count @+0x4A2, emitter[0] local pos @+0x4B0/4B4/4B8) at
        // world (localX*10 + col*160, localY*10, localZ*10 + row*160).
        //
        // The earlier "make a NEW fire tile" version broke floor collision (marking a floor tile as a
        // fire made the engine treat it as fire-tile geometry) — the tile-array write, NOT the struct
        // write, was the culprit (the ForceRaster probe wrote +0x4A2 on a real torch struct with NO
        // collision effect). So instead we reuse an EXISTING enabled torch tile's struct: set its raster
        // count=1 and point emitter[0] at the CLONE (using that tile's col/row as the anchor, so any tile
        // works no matter how far). The torch keeps its flame (flame emitters live at +0x490, untouched,
        // and the raster is now positioned at the clone, not overlapping the torch). Only struct writes —
        // the collision-safe ones. Prefer a torch whose fireIdx no OTHER enabled tile shares (else every
        // sharer would draw a second raster at its own offset). Restored on despawn.
        // Clone materialize / dematerialize. The clone fades IN over FadeSeconds, holds at full, then fades
        // OUT over the last FadeSeconds before the decoy expires. Derived from the DEADLINE rather than a
        // wall-clock start, so it inherits the pause semantics for free (while paused the deadline is pushed
        // forward, so the envelope freezes with it) and a re-cast that re-plants the decoy restarts the fade
        // in naturally. The heat-haze is deliberately NOT gated by this — it runs the clone's full lifetime.
        // Sequencing is mirrored: on cast the HAZE leads and the clone resolves into it; on expiry the CLONE
        // dissolves FIRST and the haze tails off after it, so the shimmer is the last thing to go.
        //   0 .. 0.5s          haze 0→full, clone invisible
        //   0.5 .. 1.0s        clone fades in, haze full
        //   ... body ...       both full
        //   T-1.0 .. T-0.5s    clone fades OUT, haze still full
        //   T-0.5 .. T         clone gone, haze ramps full→0
        private const double FadeSeconds = 0.5;

        // RE-CAST HAND-OFF (overlapped). The new decoy is created IMMEDIATELY and normally — timer, enemy
        // redirect, and its haze all start at the cast, unaffected. The OUTGOING clone simply dissolves in
        // place over HandoffFade while the NEW haze ramps up at the new spot.
        //
        // Only ONE clone instance is needed, because the two never overlap VISUALLY: the outgoing clone is
        // visible only during 0..HandoffFade, and the incoming clone is still fully transparent then (it does
        // not begin to materialize until the haze ramp completes at HazeRampSeconds). So the single instance
        // stays parked at the OLD pose while it fades out, and is respawned at the NEW pose exactly when its
        // alpha reaches 0 — which is the same instant the incoming fade-in starts from 0. Seamless, and no
        // second mesh/node/cloth copy (which would be a whole extra ~200 KB cave).
        //
        // The haze needs NO special case: it tracks _dx/_dy (now the new decoy) and its gain envelope is
        // driven by the new deadline, so it "instantly disappears" from the old spot and ramps up at the new.
        private const double HandoffFade = 0.25;   // outgoing clone's dissolve == the incoming haze's ramp-up
        // AGGRO LAG: enemies keep attacking the OLD decoy spot until the NEW clone has fully materialized —
        // i.e. past the clone swap, all the way through the incoming fade-in. Without this they'd re-target the
        // instant we cast, which reads as psychic; with it they stay committed to the body they were fighting
        // and only notice the switch once the new one is actually there.
        private const double AggroHoldSeconds = HazeRampSeconds + FadeSeconds;
        private static bool     _handoff;
        private static DateTime _handoffStart;
        private static DateTime _aggroHoldUntil;                // while now < this, DecoyPos stays on the OLD spot
        private static float _oldDx, _oldDz, _oldDy, _oldYaw;   // the OUTGOING clone's pose, held while it fades

        private static float CloneAlpha()
        {
            if (_handoff)   // outgoing clone dissolving in place; the incoming one is still invisible
            {
                double ht = (DateTime.UtcNow - _handoffStart).TotalSeconds;
                return (float)Math.Clamp(1.0 - ht / HandoffFade, 0.0, 1.0);
            }
            double remaining = (_decoyDeadline - DateTime.UtcNow).TotalSeconds;
            double elapsed   = DecoySeconds - remaining;
            double a = 1.0;
            double inT  = elapsed   - HazeRampSeconds;   // materialize only AFTER the haze has ramped in
            double outT = remaining - HazeRampSeconds;   // dematerialize BEFORE the haze ramps out (haze outlives it)
            if (inT  < FadeSeconds) a = inT / FadeSeconds;
            if (outT < FadeSeconds) a = Math.Min(a, outT / FadeSeconds);
            return (float)Math.Clamp(a, 0.0, 1.0);
        }

        // ── The decoy's shimmer: HeatHaze does the rendering; Mirage only decides WHERE and HOW STRONG ──
        // Envelope: 0 → full over HazeRampSeconds on cast (leading the clone in), full through the decoy's life,
        // then back to 0 as the clone dissolves — so the shimmer is the first thing to appear and the last to go.
        private const double HazeRampSeconds = 0.25;
        private const float  HazeBack  = 8f;    // pull the shimmer BACK along the clone's facing (the raster renders forward)
        private const float  HazeBodyY = -15f;  // and DOWN onto the body (the raster is built to rise above a flame)
        private static float _decoyYaw;               // clone's heading, latched at cast and PINNED onto the clone each tick
        private static float _decoyFwdX, _decoyFwdY;  // forward vector derived from _decoyYaw

        private static float HazeGain01()
        {
            // No hand-off case needed: a re-cast resets the deadline, so this reads elapsed≈0 and ramps up from
            // zero AT THE NEW DECOY — i.e. the shimmer vanishes from the old spot the instant we cast.
            double remaining = (_decoyDeadline - DateTime.UtcNow).TotalSeconds;
            double elapsed   = DecoySeconds - remaining;
            double g = 1.0;
            if (elapsed   < HazeRampSeconds) g = elapsed / HazeRampSeconds;                 // ramp in  (leads the clone)
            if (remaining < HazeRampSeconds) g = Math.Min(g, remaining / HazeRampSeconds);  // ramp out (outlives the clone)
            return (float)Math.Clamp(g, 0.0, 1.0);
        }

        /// <summary>Drive the shimmer at the clone: pushed back along its facing and down onto its body.</summary>
        private static void ShowDecoyHaze()
            => HeatHaze.Show(_dx - HazeBack * _decoyFwdX, _dz + HazeBodyY, _dy - HazeBack * _decoyFwdY, HazeGain01());


        // ── The decoy's cave payload ─────────────────────────────────────────────────────────────────
        // The generic hosting mechanism (copy → detour → repoint dispatch) lives in CodeCaveFunctions; what
        // is Mirage-specific is only the HELPER below — the code we splice into the copy.
        //
        // _GET_POSITION / _GET_DISTANCE both read the PLAYER global (0x1EA1D30) directly. We copy each into a
        // cave and replace that hardcoded load with `j helper / nop`; the helper resolves the CURRENT enemy's
        // slot and sets a1 = *(PtrTable + slot*4) — the per-slot target (fooled → decoy, else → the live
        // player global itself, so un-fooled enemies read bit-identical vanilla and the mod isn't in the loop).
        // It then jumps back into the copy at the sceVu0CopyVector jal. Explicit-coord queries are untouched.
        private const int FnCopySize = 0xF0;    // both functions fit in this
        private const int HelperOff  = 0x100;   // helper sits after the copied body

        private static uint[] DecoyHelper(uint caveGuest, int jalOff) => new[]
        {
            0x8F889CE0u,                                     // lw   t0, -0x6320(gp)   ; NowMonstorUnit
            0x8D080090u,                                     // lw   t0, 0x90(t0)      ; current enemy slot
            0x00084080u,                                     // sll  t0, t0, 2         ; slot*4
            // Materialize PtrTable's FULL 32-bit address. `lui` alone only sets the high half — it silently
            // truncates any base whose low 16 bits are non-zero. That is not theoretical: PtrTable used to be
            // 0x01F30000 (low half zero, so lui sufficed); when the cave band was relaid it moved to
            // 0x01F19000 and `lui 0x01F1` produced 0x01F10000 — the PNACH MAILBOX. Every enemy then read its
            // target pointer out of flag bytes, so they all walked toward the origin, with no cast needed.
            // `ori` is zero-extended (unlike addiu), so this is correct for any low half, including >= 0x8000.
            0x3C050000u | (CodeCaves.PtrTableGuest >> 16),      // lui  a1, HI(PtrTable)
            0x34A50000u | (CodeCaves.PtrTableGuest & 0xFFFFu),  // ori  a1, a1, LO(PtrTable)
            0x00A82821u,                                     // addu a1, a1, t0        ; &PtrTable[slot]
            0x8CA50000u,                                     // lw   a1, 0(a1)         ; a1 = per-slot target pointer
            CodeCaveFunctions.J(caveGuest + (uint)jalOff),   // j    cave+jalOff       ; back into the copy
            CodeCaveFunctions.Nop,                           // (j delay)
        };

        /// <summary>Arm one of the two decoy caves. Returns true once armed (idempotent — safe to retry).</summary>
        private static bool ArmDecoyCave(string name, long vanillaFn, long cave, uint caveGuest, long dispatch, int detourOff, int jalOff)
            => CodeCaveFunctions.ArmDispatchCave(
                name, vanillaFn, FnCopySize, cave, caveGuest, dispatch,
                pristine: new[] { (0, StbExternCmd.VanillaPrologue),            // addiu sp,-0x50
                                  (detourOff, StbExternCmd.VanillaPlayerLd) },  // lui v0,0x1ea (the player-addr load)
                detours:  new[] { (detourOff, new[] { CodeCaveFunctions.J(caveGuest + HelperOff),   // was lui v0,0x1ea
                                                      CodeCaveFunctions.Nop }) },                   // was addiu a1,v0,0x1d30
                helperOff: HelperOff, helper: DecoyHelper(caveGuest, jalOff));


        private static void Loop()
        {
            bool guardLatched = false;
            DateTime guardPoseSince = default;
            DateTime lastTick = DateTime.UtcNow;
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    // REAL elapsed wall time since the previous iteration. A tick can take substantially
                    // longer than FastTickMs (MaintainClone batch-reads the whole fire-tile grid and does a
                    // pile of PINE writes), so freezing the decoy timer by pushing the deadline a hardcoded
                    // FastTickMs under-compensates and the timer keeps draining while paused. Push by this.
                    DateTime nowTick = DateTime.UtcNow;
                    TimeSpan dt = nowTick - lastTick;
                    lastTick = nowTick;

                    bool inDun = Player.InDungeonFloor();
                    if (!_armed && !inDun) ArmColdPatch();

                    if (_armed && inDun)
                    {

                        // Keep un-fooled slots on the live player (the patch reads the table for EVERY
                        // enemy, always) and fooled slots on the decoy — one batched write per fast tick.
                        // A live clone cannot survive a party swap (its source model gets unloaded).
                        if ((_decoyActive || CharacterClone.IsActive) && Player.CurrentCharacterNum() != _decoyChar)
                        {
                            Console.WriteLine($"[Mirage] party swapped away from char {_decoyChar} — tearing down the decoy " +
                                              "(the clone is bound to its source model; its geometry would dangle)");
                            EndDecoy();
                        }

                        bool mirageArmed = MirageArmed();
                        bool paused = Player.CheckDunIsPausedOrMenu();   // "PAUSE" screen OR the in-dungeon item menu — freeze the decoy for both

                        if (mirageArmed && !paused)
                        {
                            // Plant the decoy ONCE per guard-hold: the held-guard pose oscillates between motion 9
                            // (loop) and 33 (move) under R1, so we can't edge-trigger on a single motion. Latch on
                            // the first hold-pose motion while guarding and only clear the latch when R1 is released
                            // (guard exited) — so re-entering guard plants a fresh decoy, but 9<->33 doesn't.
                            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
                            int  mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
                            bool inGuardPose = guarding && (mid == GuardLoopMotion || mid == GuardMoveMotion);
                            if (!guarding) { guardLatched = false; guardPoseSince = default; }   // released guard → re-arm
                            if (inGuardPose && !guardLatched && CharacterSettled())
                            {
                                // Hold the guard pose for GuardChargeMs, THEN flash the player (Mobius-charge
                                // style) and plant the decoy at that same moment. One flash+decoy per guard-hold.
                                if (guardPoseSince == default) guardPoseSince = DateTime.UtcNow;
                                else if (DateTime.UtcNow - guardPoseSince >= TimeSpan.FromMilliseconds(GuardChargeMs))
                                {
                                    Player.FlashChargeComplete();
                                    if (_handoff) { }                           // a hand-off is already running — ignore
                                    else if (_decoyActive) BeginHandoff();      // clone up → new decoy now, dissolve the old one
                                    else PlaceDecoy();                          // nothing up → plant immediately
                                    guardLatched = true;
                                }
                            }
                            if (_decoyActive) UpdateDecoyState();
                        }
                        else if (_decoyActive && paused)
                        {
                            // Freeze while paused: hold the deadline (timer stops) and keep the clone drawn — the
                            // flag-3 gate below freezes its step + cloth. Covers BOTH pause types: the item menu
                            // (engine already freezes the clone there) and the PAUSE screen (where the chara loop
                            // otherwise keeps stepping the clone).
                            _decoyDeadline += dt;   // hold the timer: push by the REAL tick delta, not a fixed FastTickMs
                            if (_handoff) _handoffStart += dt;                      // freeze a hand-off mid-dissolve
                            if (_aggroHoldUntil != default) _aggroHoldUntil += dt;   // ...and its aggro lag
                            PoseClone();
                            ShowDecoyHaze();
                        }
                        else if (!mirageArmed && _decoyActive)
                        {
                            EndDecoy();   // weapon swapped away from Mirage
                        }

                        WriteTable();   // fills the per-slot table both _GET_POSITION and _GET_DISTANCE now read
                        // PNACH gate flag: 1 = clone drawn → NOP the chara-loop gates; 2 = in a dungeon w/o a decoy
                        // → RESTORE the vanilla gates (they don't auto-revert). 0 (town) is set below so the shared
                        // town overlay at those addresses is never touched.
                        // 1 = decoy up & running (NOP scene+step gates); 3 = decoy up but PAUSED (NOP scene only →
                        // clone still drawn but frozen); 2 = dungeon, no decoy (restore vanilla).
                        Memory.WriteInt(CodeCaves.MirageSceneGateFlag, (_decoyActive && CharacterClone.IsActive) ? (paused ? 3 : 1) : 2);
                        sleep = FastTickMs;
                    }
                    else
                    {
                        guardLatched = false;
                        if (_decoyActive || CharacterClone.IsActive) EndDecoy();   // left the floor
                        Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 0);   // town: leave the gates to the overlay reload
                    }
                }
                catch (Exception e) { Console.WriteLine("[Mirage] tick failed: " + e.Message); }
                Thread.Sleep(sleep);
            }
        }

        /// <summary>True while the game is paused in a way that should freeze the decoy — either the "PAUSE"
        /// screen (CheckDunIsPaused) or the in-dungeon item menu (mode 3 / dungeonMode 2, same test the other
        /// effects use for menuOpen). The two pause types freeze different things natively (the menu freezes the
        /// clone but not our timer; the PAUSE screen freezes our timer but not the clone), so we unify them.</summary>
        // ── decoy state (all DATA) ───────────────────────────────────────────────────────────
        /// <summary>Read the player's current spot + facing as a decoy origin. The facing is the YAW at CObject
        /// +0x64 (GetRotation__7CObject stores EULER ANGLES at +0x60/+0x64/+0x68 — NOT a direction vector; for an
        /// upright character the X/Z angles are ~0, which is why reading +0x60/+0x68 as a vector gave (0,0)).</summary>
        private static (float dx, float dz, float dy, float yaw) ReadDecoyOrigin()
            => (Memory.ReadFloat(Addresses.dunPositionX),
                Memory.ReadFloat(Addresses.dunPositionZ),
                Memory.ReadFloat(Addresses.dunPositionY),
                Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY));

        private static void PlaceDecoy()
        {
            var o = ReadDecoyOrigin();
            PlaceDecoyAt(o.dx, o.dz, o.dy, o.yaw);
        }

        /// <summary>Create the decoy: this is the moment its TIMER, enemy REDIRECT and haze all begin. On a
        /// re-cast this still runs immediately and normally (spawnClone:false) — the only difference is that we
        /// hold off respawning the clone instance until the OUTGOING one has finished dissolving in place.</summary>
        private static void PlaceDecoyAt(float dx, float dz, float dy, float yaw, bool spawnClone = true, bool refreshAggro = true)
        {
            _dx = dx; _dz = dz; _dy = dy;
            _decoyYaw  = yaw;                                   // PINNED onto the clone each tick (see MaintainClone)
            _decoyFwdX = (float)System.Math.Sin(yaw);           // haze is pushed BACK along this (the raster renders forward)
            _decoyFwdY = (float)System.Math.Cos(yaw);
            WriteDecoyPos();   // the stationary decoy position fooled slots' pointers reference
            // refreshAggro:false on a re-cast hand-off. Re-fooling everyone here would instantly re-deceive the
            // enemies that had BROKEN the illusion (by hitting them) and — since aggro is held on the old spot —
            // send them at the OUTGOING clone. Instead we preserve _brokenThisDecoy so they keep chasing the
            // player through the hand-off, and fold them back in when the new clone finishes materializing.
            if (refreshAggro) RefreshAggro();
            _decoyActive = true;
            _decoyChar = Player.CurrentCharacterNum();   // the clone is bound to THIS character's model
            _decoyDeadline = DateTime.UtcNow.AddSeconds(DecoySeconds);   // timer + haze ramp start HERE
            if (spawnClone)
            {
                CharacterClone.Despawn();   // clear any stale slot from a previous decoy
                PoseClone(spawn: true);
                if (CharacterClone.IsActive) Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 1);   // arm the PNACH scene-gate NOP (clone draws)
            }
            Console.WriteLine($"[Mirage] decoy planted at ({_dx:0.#},{_dy:0.#}); enemies redirected");
        }

        /// <summary>(Re-)deceive every live enemy: clear the "broke the illusion" set and fool them all, so they
        /// path to the decoy. Called at a normal cast, and — on a re-cast — deferred until the new clone has
        /// fully materialized, so enemies that had wised up don't get re-fooled onto the OUTGOING clone.</summary>
        private static void RefreshAggro()
        {
            Array.Clear(_fooled, 0, _fooled.Length);
            Array.Clear(_brokenThisDecoy, 0, _brokenThisDecoy.Length);
            _prevHp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MaxSlots; s++)
                if (IsLiveEnemy(s)) _fooled[s] = true;
        }

        /// <summary>Re-cast with a clone already up. The new decoy is created RIGHT NOW and normally (timer,
        /// redirect, and its haze ramping up at the new spot). We just hold the existing clone instance at its
        /// OLD pose and dissolve it over HandoffFade — during which the incoming clone is still fully
        /// transparent, so one instance covers both. It is respawned at the new pose the moment it hits 0.</summary>
        private static void BeginHandoff()
        {
            _oldDx = _dx; _oldDz = _dz; _oldDy = _dy; _oldYaw = _decoyYaw;   // hold the outgoing clone in place
            _handoff = true;
            _handoffStart = DateTime.UtcNow;
            _aggroHoldUntil = _handoffStart.AddSeconds(AggroHoldSeconds);    // aggro lags on the old spot past the swap
            var o = ReadDecoyOrigin();
            PlaceDecoyAt(o.dx, o.dz, o.dy, o.yaw, spawnClone: false, refreshAggro: false);   // new decoy live now; aggro state preserved
            Console.WriteLine($"[Mirage] re-cast: new decoy live; outgoing clone dissolving over {HandoffFade:0.###}s, aggro held on the old spot for {AggroHoldSeconds:0.###}s");
        }

        private static void CompleteHandoff()
        {
            _handoff = false;   // NOTE: aggro does NOT move here — it stays on the old spot until _aggroHoldUntil
            CharacterClone.Despawn();   // swap the clone INSTANCE; the haze is the decoy's, so it keeps ramping undisturbed
            PoseClone(spawn: true);
            if (CharacterClone.IsActive) Memory.WriteInt(CodeCaves.MirageSceneGateFlag, 1);
        }

        private static void UpdateDecoyState()
        {
            if (_handoff && (DateTime.UtcNow - _handoffStart).TotalSeconds >= HandoffFade)
                CompleteHandoff();   // outgoing clone hit alpha 0 → respawn it at the new decoy and fade it in
            if (_aggroHoldUntil != default && DateTime.UtcNow >= _aggroHoldUntil)
            {
                _aggroHoldUntil = default;   // new clone is fully materialized → enemies finally notice the switch
                RefreshAggro();              // incl. the ones that had broken the old illusion: they only fall for the NEW clone
                WriteDecoyPos();
                Console.WriteLine("[Mirage] hand-off: new clone fully faded in — enemies re-target it");
            }
            if (DateTime.UtcNow > _decoyDeadline)
            {
                EndDecoy();
                Console.WriteLine("[Mirage] decoy faded; enemies re-target the player");
                return;
            }
            PoseClone();
            ShowDecoyHaze();
            int[] hp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MaxSlots; s++)
            {
                if (!IsLiveEnemy(s)) { _fooled[s] = false; continue; }
                if (_prevHp != null && s < _prevHp.Length && hp[s] < _prevHp[s])
                { _fooled[s] = false; _brokenThisDecoy[s] = true; continue; }
                if (!_fooled[s] && !_brokenThisDecoy[s]) _fooled[s] = true;
            }
            _prevHp = hp;
        }

        /// <summary>One batched write of the managed slots' POINTERS: fooled → DecoyPos, else → the live player
        /// global. Un-fooled entries are the live-player address itself, so those enemies read the engine-live
        /// player (vanilla) — the mod only flips a pointer when a slot's fooled state changes.</summary>
        private static void WriteTable()
        {
            var buf = new byte[MaxSlots * CodeCaves.PtrStride];
            for (int s = 0; s < MaxSlots; s++)
                BitConverter.GetBytes(_fooled[s] ? CodeCaves.DecoyPosGuest : StbExternCmd.PlayerPosGuest)
                    .CopyTo(buf, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, buf);
        }

        /// <summary>Point every slot at the live player global (vanilla) — done at cold-arm before any enemy reads,
        /// so out-of-range slots and the pre-first-tick window are valid without the mod having run.</summary>
        private static void FillWholeTablePlayer()
        {
            var buf = new byte[CodeCaves.TableSlots * CodeCaves.PtrStride];
            for (int s = 0; s < CodeCaves.TableSlots; s++)
                BitConverter.GetBytes(StbExternCmd.PlayerPosGuest).CopyTo(buf, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, buf);
        }

        /// <summary>Write the stationary decoy position (x,z,y,w) that fooled slots' pointers reference.</summary>
        private static void WriteDecoyPos()
        {
            // Enemies chase THIS position. Through a re-cast hand-off it stays on the OLD decoy spot until the
            // new clone has fully materialized (AggroHoldSeconds) — deliberately outliving the clone swap, so
            // enemies commit to the body they were fighting and only notice the switch once the new one is
            // actually there, instead of psychically peeling off the instant we cast.
            bool hold = DateTime.UtcNow < _aggroHoldUntil;
            var b = new byte[16];
            BitConverter.GetBytes(hold ? _oldDx : _dx).CopyTo(b, 0);
            BitConverter.GetBytes(hold ? _oldDz : _dz).CopyTo(b, 4);
            BitConverter.GetBytes(hold ? _oldDy : _dy).CopyTo(b, 8);
            BitConverter.GetBytes(1.0f).CopyTo(b, 12);
            Memory.WriteBytesBatch(CodeCaves.DecoyPos, b);
        }

        private static bool IsLiveEnemy(int s)
        {
            int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
            if (id == 0 || id == 0xFFFF) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }
    }
}
