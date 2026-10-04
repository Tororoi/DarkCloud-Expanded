using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ungaga's "Mirage" (weapon 354; Super Steve inherits it through a Mirage / Hercules' Wrath sphere): the guard pose held
    /// <see cref="GuardChargeMs"/> flashes the player and plants a stationary DECOY at their spot — a clone of the wielder in
    /// the guard pose (<see cref="CharacterClone"/>) under a heat shimmer (<see cref="HeatHaze"/>). While it stands (12 s, 18 s
    /// from Hercules' Wrath; a re-cast hands off to a new one) every enemy is pointed at it through the per-slot target table
    /// (<see cref="TargetRedirectCaves"/>, held via <see cref="AggroTable"/>) PER ENEMY: one you HIT drops the illusion and
    /// re-targets you until the next decoy. The loop runs on every floor and tears the decoy down on expiry, weapon or party
    /// swap and floor exit. docs/mirage.md.
    /// </summary>
    internal static class Mirage
    {
        private const string Tag = "[Mirage] ";
        private const double MirageSeconds = 12.0, HerculesSeconds = 18.0;   // the decoy's life: the Mirage's, and Hercules' Wrath's longer one
        private static double DecoySeconds = MirageSeconds;                  // latched at each cast (PlaceDecoyAt) from the weapon or sphere that cast it

        /// <summary>Hercules' Wrath (Ungaga's, or Super Steve's sphere of it) is the one casting.</summary>
        private static bool HerculesCasts() => UngagaWeapon.WieldsOrSphere(Items.herculeswrath);

        /// <summary>For Hercules' Wrath's ultimate: a decoy is up (a hand-off counts), where it stands, and how far its clone has
        /// dissolved at the END of its life (1 until the last fade, then down to 0 — the fade-in and a hand-off do not count).</summary>
        internal static bool  DecoyUp => _decoyActive;
        internal static bool  InHandoff => _handoff;
        internal static (float x, float h, float y) DecoyPosition => (_dx, _dz, _dy);
        internal static float DecoyOutroAlpha
        {
            get
            {
                if (!_decoyActive) return 0f;
                if (_handoff) return 1f;
                double outT = (_decoyDeadline - GameClock.Now).TotalSeconds - HazeRampSeconds;
                return (float)Math.Clamp(outT / FadeSeconds, 0.0, 1.0);
            }
        }
        private const int    FastTickMs   = 25;    // table maintenance cadence while armed + in a dungeon
        private const int    IdleTickMs   = 150;
        // The guard-hold pose is GuardWatch's (GuardLoopMotion 9 / GuardMoveMotion 33, read by GuardWatch.HoldPose): Ungaga AND Xiao
        // both use them — see docs/character-motion-table.md — so the same trigger and hold-pose work for either wielder.
        internal const int   GuardChargeMs   = 250; // hold the guard pose this long before the flash + decoy fire (level 1; Hercules' Wrath's ultimate is level 2)

        // ── What MIRAGE chooses (as opposed to what the game dictates) ───────────────────────────────
        // Engine struct layouts live in CCharacter/CFrameVu1/CCloth/...; cave addresses AND their capacities
        // live in CodeCaves. These are the mod's own decisions.
        private const int MaxSlots = 20;      // enemy slots the mod actively manages (FloorSlots is 16)

        /// <summary>Pose the clone. During a re-cast hand-off the instance is still the OUTGOING clone, so it
        /// stays parked at the old pose while it dissolves — _dx/_dy already point at the NEW decoy (which the
        /// shimmer is ramping up on). Choosing the pose is the CALLER's job; CharacterClone just renders it.</summary>
        private static void PoseClone(bool spawn = false)
        {
            float cx   = _handoff ? _oldDx  : _dx;
            float cz   = _handoff ? _oldDz  : _dz;
            float cy   = _handoff ? _oldDy  : _dy;
            float cyaw = _handoff ? _oldYaw : _decoyYaw;
            CharacterClone.HoldMotion = GuardWatch.GuardLoopMotion;   // hold the guard pose regardless of what the player does
            if (spawn) CharacterClone.Spawn(cx, cz, cy, cyaw, CloneAlpha());
            else       CharacterClone.Maintain(cx, cz, cy, cyaw, CloneAlpha());
        }

        /// <summary>The weapons that grant the Mirage: the Mirage itself and Hercules' Wrath, the spear built up from it.</summary>
        private static readonly int[] MirageLine = { Items.mirage, Items.herculeswrath };

        /// <summary>Is the Mirage ability currently wielded? UNGAGA holding a weapon of <see cref="MirageLine"/>, or XIAO holding
        /// Super Steve with one of their SynthSpheres attached — the standard Super Steve inheritance (the sphere's SOURCE weapon
        /// id selects the effect). Both wielders work unchanged because Ungaga and Xiao share the guard motions this triggers on
        /// (9 / 33), and Xiao fits the clone's mesh cave. Mirage is NOT driven from SuperSteve's SphereInheritanceEffect hub — it
        /// owns a thread and a state machine (guard charge → decoy → clone → haze), so it gates itself here rather than being
        /// pulsed per-tick like the stateless abilities.</summary>
        private static bool MirageArmed() => UngagaWeapon.WieldsOrSphere(MirageLine);

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
            uint root = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (ch != _seenChar || root != _seenRoot)
            {
                _seenChar = ch; _seenRoot = root; _seenSince = DateTime.UtcNow;
                return false;
            }
            if (!Memory.IsValidGuest(root)) return false;
            return DateTime.UtcNow - _seenSince >= TimeSpan.FromMilliseconds(CharSettleMs);
        }

        /// <summary>Hercules' Wrath's ultimate lands on the mirage: the decoy dispelled at once (under the strike's flash) — clone,
        /// shimmer and every lure gone; the enemies turn back to the player.</summary>
        internal static void Dispel()
        {
            if (!_decoyActive && !CharacterClone.IsActive) return;
            EndDecoy();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "dispelled by the strike");
        }

        /// <summary>Tear the decoy + clone + shimmer down. Used on expiry, weapon swap, floor exit, party swap.</summary>
        private static void EndDecoy()
        {
            _decoyActive = false; _handoff = false; _aggroHoldUntil = default; _decoyChar = -1;
            AggroTable.Release(AggroTable.Holder.MirageDecoy);
            Array.Clear(_fooled, 0, _fooled.Length);
            CharacterClone.Despawn();
            HeatHaze.Hide();
        }

        private static bool     _decoyActive;
        private static DateTime _decoyDeadline;
        private static float    _dx, _dz, _dy;
        private static readonly bool[] _fooled          = new bool[MaxSlots];
        private static readonly bool[] _brokenThisDecoy = new bool[MaxSlots];
        private static int[] _prevHp;

        internal static void Start() => new Thread(Loop) { IsBackground = true }.Start();

        /// <summary>Forwarder for Core/MainMenuThread.cs only: the engine redirect is <see cref="TargetRedirectCaves"/>'s.</summary>
        internal static void ArmColdPatch() => TargetRedirectCaves.ArmColdPatch();


        // ── Clone heat-haze: the game's fire-raster distortion, drawn at the clone by ElfCave.MirageHazeDraw ──
        // HeatHaze names the clone's root CFrame in the mailbox and ramps the strength; the cave draws one raster
        // there every frame, in the map's own raster pass. (The mechanisms tried before it: docs/mirage.md.)
        //
        // Clone materialize / dematerialize. The clone fades IN over FadeSeconds, holds at full, then fades
        // OUT over the last FadeSeconds before the decoy expires. Derived from the DEADLINE, which is on GameClock,
        // so the envelope holds through a pause, and a re-cast that re-plants the decoy restarts the fade in
        // naturally. The heat-haze is deliberately NOT gated by this — it runs the clone's full lifetime.
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
                double ht = (GameClock.Now - _handoffStart).TotalSeconds;
                return (float)Math.Clamp(1.0 - ht / HandoffFade, 0.0, 1.0);
            }
            double remaining = (_decoyDeadline - GameClock.Now).TotalSeconds;
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
        private const float  HazeBodyY = -15f;  // the shimmer's anchor sits this far DOWN the clone (the raster is built to rise above a flame)
        private static float _decoyYaw;               // clone's heading, latched at cast and PINNED onto the clone each tick
        private static float _decoyFwdX, _decoyFwdY;  // forward vector derived from _decoyYaw

        private static float HazeGain01()
        {
            // No hand-off case needed: a re-cast resets the deadline, so this reads elapsed≈0 and ramps up from
            // zero AT THE NEW DECOY — i.e. the shimmer vanishes from the old spot the instant we cast.
            double remaining = (_decoyDeadline - GameClock.Now).TotalSeconds;
            double elapsed   = DecoySeconds - remaining;
            double g = 1.0;
            if (elapsed   < HazeRampSeconds) g = elapsed / HazeRampSeconds;                 // ramp in  (leads the clone)
            if (remaining < HazeRampSeconds) g = Math.Min(g, remaining / HazeRampSeconds);  // ramp out (outlives the clone)
            return (float)Math.Clamp(g, 0.0, 1.0);
        }

        /// <summary>Drive the shimmer at the clone: pushed back along its facing and down onto its body.</summary>
        private static void ShowDecoyHaze()
            => HeatHaze.Show(CharacterClone.RootGuest, HazeBodyY, HazeGain01());   // pinned to the clone's root by the haze cave


        private static void Loop()
        {
            bool guardLatched = false;
            DateTime guardPoseSince = default;
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool inDun = Player.InDungeonFloor();
                    if (!TargetRedirectCaves.Armed && !inDun) TargetRedirectCaves.ArmColdPatch();   // the engine redirect, retried out of a dungeon until it arms
                    if (inDun) { if (MirageLineReach.Wielded()) MirageLineReach.Hold(); else MirageLineReach.Release(); }   // the line's lock-on reach (needs no decoy patch)

                    if (TargetRedirectCaves.Armed && inDun)
                    {

                        // Keep un-fooled slots on the live player (the patch reads the table for EVERY
                        // enemy, always) and fooled slots on the decoy — one batched write per fast tick.
                        // A live clone cannot survive a party swap (its source model gets unloaded).
                        if ((_decoyActive || CharacterClone.IsActive) && Player.CurrentCharacterNum() != _decoyChar)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"party swapped away from char {_decoyChar} — tearing down the decoy " +
                                              "(the clone is bound to its source model; its geometry would dangle)");
                            EndDecoy();
                        }

                        bool mirageArmed = MirageArmed();
                        bool paused = Player.CheckDunIsPausedOrMenu();   // "PAUSE" screen OR the in-dungeon item menu — freeze the decoy for both
                        CharacterClone.Held = paused;                    // the clone's slot is drawn but not stepped while held

                        if (mirageArmed && !paused)
                        {
                            // Plant the decoy ONCE per guard-hold: the held-guard pose oscillates between motion 9
                            // (loop) and 33 (move) under R1, so we can't edge-trigger on a single motion. Latch on
                            // the first hold-pose motion while guarding and only clear the latch when R1 is released
                            // (guard exited) — so re-entering guard plants a fresh decoy, but 9<->33 doesn't.
                            var (guarding, inGuardPose) = GuardWatch.HoldPose();
                            if (!guarding) { guardLatched = false; guardPoseSince = default; }   // released guard → re-arm
                            if (inGuardPose && !guardLatched && CharacterSettled())
                            {
                                // Hold the guard pose for GuardChargeMs, THEN flash the player (Mobius-charge
                                // style) and plant the decoy at that same moment. One flash+decoy per guard-hold.
                                if (guardPoseSince == default) guardPoseSince = GameClock.Now;
                                else if (GameClock.Now - guardPoseSince >= TimeSpan.FromMilliseconds(GuardChargeMs))
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
                            // Held: keep the clone drawn; its slot is marked skip-step (CharacterClone.Held), which
                            // freezes body and cloth for both hold types — the item menu (the engine already freezes
                            // the clone there) and the PAUSE screen (where the chara loop otherwise keeps stepping
                            // it). The decoy timer, a hand-off and its aggro lag stand still on their own: GameClock.
                            PoseClone();
                            ShowDecoyHaze();
                        }
                        else if (!mirageArmed && _decoyActive)
                        {
                            EndDecoy();   // weapon swapped away from Mirage
                        }

                        if (_decoyActive) WriteTable();   // the decoy holds the table while it is up (AggroTable): fooled slots on the decoy, the rest on the player
                        // PNACH gate flag: 1 = decoy up (NOP the chara-loop scene+step gates so the clone draws; a hold freezes the
                        // clone's own slot instead, so the PNACH's 3 = "up but paused" state is never written); 2 = in a dungeon
                        // without a decoy → RESTORE the vanilla gates (they don't auto-revert). 0 (town) is set below so the
                        // shared town overlay at those addresses is never touched.
                        // Guardian Reflector's slingshot prop, Divine Beast Title's cat and Big Bang's judgement blade
                        // share this gate flag (and the chara slots / caves): while any copy is up, IT drives the flag —
                        // stand down. (Mirage and Xiao's weapons can never be wielded simultaneously.) A competing 2 here
                        // made the slot loop run only on the frames the other writer won — the cat flickered.
                        if (!SlingshotProp.Active && !DivineBeastTitle.Active && !BladeProp.Active)
                            Memory.WriteInt(Mailbox.MirageSceneGate, (_decoyActive && CharacterClone.IsActive) ? 1 : 2);
                        sleep = FastTickMs;
                    }
                    else
                    {
                        guardLatched = false;
                        if (_decoyActive || CharacterClone.IsActive) EndDecoy();   // left the floor
                        if (!SlingshotProp.Active && !DivineBeastTitle.Active && !BladeProp.Active)
                            Memory.WriteInt(Mailbox.MirageSceneGate, 0);   // town: leave the gates to the overlay reload
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                Thread.Sleep(sleep);
            }
        }

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
            AggroTable.Claim(AggroTable.Holder.MirageDecoy);                           // the table is the decoy's while it stands
            _decoyActive = true;
            _decoyChar = Player.CurrentCharacterNum();   // the clone is bound to THIS character's model
            DecoySeconds = HerculesCasts() ? HerculesSeconds : MirageSeconds;
            _decoyDeadline = GameClock.Now.AddSeconds(DecoySeconds);   // timer + haze ramp start HERE
            if (spawnClone)
            {
                CharacterClone.Despawn();   // clear any stale slot from a previous decoy
                PoseClone(spawn: true);
                if (CharacterClone.IsActive) Memory.WriteInt(Mailbox.MirageSceneGate, 1);   // arm the PNACH scene-gate NOP (clone draws)
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"decoy planted at ({_dx:0.#},{_dy:0.#}); enemies redirected");
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
                if (Enemies.IsLive(s)) _fooled[s] = true;
        }

        /// <summary>Re-cast with a clone already up. The new decoy is created RIGHT NOW and normally (timer,
        /// redirect, and its haze ramping up at the new spot). We just hold the existing clone instance at its
        /// OLD pose and dissolve it over HandoffFade — during which the incoming clone is still fully
        /// transparent, so one instance covers both. It is respawned at the new pose the moment it hits 0.</summary>
        private static void BeginHandoff()
        {
            _oldDx = _dx; _oldDz = _dz; _oldDy = _dy; _oldYaw = _decoyYaw;   // hold the outgoing clone in place
            _handoff = true;
            _handoffStart = GameClock.Now;
            _aggroHoldUntil = _handoffStart.AddSeconds(AggroHoldSeconds);    // aggro lags on the old spot past the swap
            var o = ReadDecoyOrigin();
            PlaceDecoyAt(o.dx, o.dz, o.dy, o.yaw, spawnClone: false, refreshAggro: false);   // new decoy live now; aggro state preserved
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"re-cast: new decoy live; outgoing clone dissolving over {HandoffFade:0.###}s, aggro held on the old spot for {AggroHoldSeconds:0.###}s");
        }

        private static void CompleteHandoff()
        {
            _handoff = false;   // NOTE: aggro does NOT move here — it stays on the old spot until _aggroHoldUntil
            CharacterClone.Despawn();   // swap the clone INSTANCE; the haze is the decoy's, so it keeps ramping undisturbed
            PoseClone(spawn: true);
            if (CharacterClone.IsActive) Memory.WriteInt(Mailbox.MirageSceneGate, 1);
        }

        private static void UpdateDecoyState()
        {
            if (_handoff && (GameClock.Now - _handoffStart).TotalSeconds >= HandoffFade)
                CompleteHandoff();   // outgoing clone hit alpha 0 → respawn it at the new decoy and fade it in
            if (_aggroHoldUntil != default && GameClock.Now >= _aggroHoldUntil)
            {
                _aggroHoldUntil = default;   // new clone is fully materialized → enemies finally notice the switch
                RefreshAggro();              // incl. the ones that had broken the old illusion: they only fall for the NEW clone
                WriteDecoyPos();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "hand-off: new clone fully faded in — enemies re-target it");
            }
            if (GameClock.Now > _decoyDeadline)
            {
                EndDecoy();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "decoy faded; enemies re-target the player");
                return;
            }
            PoseClone();
            ShowDecoyHaze();
            int[] hp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MaxSlots; s++)
            {
                if (!Enemies.IsLive(s)) { _fooled[s] = false; continue; }
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
            AggroTable.Write(AggroTable.Holder.MirageDecoy, buf);
        }

        /// <summary>Write the stationary decoy position (x,z,y,w) that fooled slots' pointers reference.</summary>
        private static void WriteDecoyPos()
        {
            // Enemies chase THIS position. Through a re-cast hand-off it stays on the OLD decoy spot until the
            // new clone has fully materialized (AggroHoldSeconds) — deliberately outliving the clone swap, so
            // enemies commit to the body they were fighting and only notice the switch once the new one is
            // actually there, instead of psychically peeling off the instant we cast.
            bool hold = GameClock.Now < _aggroHoldUntil;
            var b = new byte[16];
            BitConverter.GetBytes(hold ? _oldDx : _dx).CopyTo(b, 0);
            BitConverter.GetBytes(hold ? _oldDz : _dz).CopyTo(b, 4);
            BitConverter.GetBytes(hold ? _oldDy : _dy).CopyTo(b, 8);
            BitConverter.GetBytes(1.0f).CopyTo(b, 12);
            Memory.WriteBytesBatch(CodeCaves.DecoyPos, b);
        }
    }
}
