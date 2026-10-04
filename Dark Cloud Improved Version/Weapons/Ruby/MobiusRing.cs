using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Mobius Ring — the longer the charge, the greater the damage (<see cref="MobiusRingEffect"/>, the weapon's thread): while
    /// Ruby charges, every flash of the charge glow (the glow timer reaching 17008, reset by the mod) compounds the shot's damage
    /// ×1.5 (capped at 65535) and shows the running total; from the first flash the held ball grows with the damage multiplier
    /// (<see cref="RubyBallGrowthPerMultiple"/>, capped at <see cref="RubyBallMaxScale"/>), the fired orbs fly at that size with the
    /// ramped damage written into them every 10 ms until they expire, and the enemy bodies are inflated so the orbs' collision grows
    /// to match. The growth itself is below. The ball + fired orbs are CSHOT_EFFECT slots of the MainCharaEffectBase
    /// pool (decomp-confirmed: RubyOrbs' addresses ARE the pool's per-slot fields; the objects are full CCharacters). Three
    /// coordinated levers, all engine-fed, each behind its own switch:
    ///  1. Mot_List SCALE keyframes (ratio-multiply) — the billboard sprite layers, whose size the motion animates per frame
    ///     (works; OFF, redundant with 2).
    ///  2. CObject scale (+0x90/94/98) on template + slots — Draw__10CCharacter pushes it into the root frame every draw,
    ///     scaling the whole hierarchy (the ball's core geometry, which the sprite tracks don't cover). THE visual lever.
    ///  3. BT_SHOT_EFFECT per-phase collision radii (BT+0x28) — Step builds the shot's damage sphere from these. ⚠ CRASHES
    ///     PCSX2; OFF. The shipping collision path is <see cref="MaintainOrbHitbox"/>: enemy-body inflation, mathematically
    ///     equivalent (hit ⇔ dist &lt; orbR + bodyR), with none of the BT side effects (wall checks stay native).
    /// All slots share one Mot_List/BT, so the fired orbs inherit everything. Layout consts + RE notes in ShotEffectPool
    /// (WeaponAddresses.cs). Driven from <see cref="MobiusRingEffect"/>, which holds the growth formula; restored via
    /// factor 1.0.</summary>
    internal static class MobiusRing
    {
        // ── the charge-ball growth — MOD POLICY, not a vanilla fact ──────────────────────────────
        // The ball's size tracks the Mobius damage multiplier M = currentDamage/baseDamage:
        //   scale = 1 + (M-1)*RubyBallGrowthPerMultiple, clamped to RubyBallMaxScale.
        // Both are eyeball-calibration (tune live) — which is exactly why they do NOT belong in an addresses
        // file. The vanilla facts they drive live in ShotEffectPool. MobiusRingSphere reads the growth for Xiao's pellet.
        internal const float RubyBallMaxScale          = 5.0f;   // hard cap on ball/orb size (M can reach 1000s)
        internal const float RubyBallGrowthPerMultiple = 1.0f;   // ball-scale gained per +1.0 of damage multiplier

        // ── WHICH lever to use (mod implementation choices / crash triage, not game facts) ──
        private const bool ScaleSprites   = false;   // 1. Mot_List chain-1 SCALE keyframe patch — redundant with the core scale
        private const bool ScaleCore      = true;    // 2. CObject scale on the template + slots — THE visual lever
        private const bool ScaleCollision = false;   // 3. BT body radii — ⚠ CRASHES the game. Keep off (the flag keeps the dead end documented)

        // ── sprite-keyframe lever state (Mot_List keyframe patch; see SetBallScale) ──
        static float _ballApplied = 1.0f;  // factor currently baked into the scale keyframes
        static long  _chainHead;           // MMU addr of the Mot_List head the factor was applied to
        static float _origKey0;            // original (1×) first scale-key value — reload canary
        // ── BT-radii lever state ──
        static long  _btAddr;              // MMU addr of the BT_SHOT_EFFECT the radii were captured from
        static float _btFactor = 1.0f;     // factor currently applied to the BT collision radii
        static readonly float[] _btOrig = new float[ShotEffectPool.BtShotRadiiCount]; // original per-phase radii

        // The exact chain-1 track-type set MotionProc (0x147D20) dispatches on. Anything else in a walked
        // record means we're not looking at a real Mot_List — stop before touching memory.
        static bool IsKnownTrackType(int t)
            => t == 0 || t == 1 || t == 2 || t == 0xC || (t >= 0x1E && t <= 0x21) || t == 0x28 || t == 0x29 || t == 0x32 || t == 0x33;

        /// <summary>Grow Ruby's charge ball / shot orbs (visual + collision) to <paramref name="factor"/>×
        /// (1.0 = original). Idempotent per factor; detects a reloaded effect (character/floor change) and
        /// rebases. Only writes during active field play.</summary>
        public static void SetBallScale(float factor)
        {
            if (!Player.CheckDunIsWalkingMode()) return;

            // 1. Sprite layers: ratio-patch the chain-1 SCALE keyframe values. REDUNDANT with the core
            // object scale (billboards inherit the root scale — live-confirmed 2026-07-06: both together
            // double-scale the sprite layers), so this lever is normally OFF; kept for experiments.
            // CHAIN-1 ONLY: chain-2 (MotionProc2) records are vertex-skinning data with a different layout
            // (+0x10 = blend weights) — patching them corrupts vertex animation (the Read Abort crashes).
            if (ScaleSprites)
            {
                var tracks = new System.Collections.Generic.List<(long Keys, int Count)>();
                long head = 0;
                for (int bank = 0; bank < 8; bank++)
                {
                    // Trust a bank only if its motion-id range fields look sane (GetMotionParam's own check).
                    long chr = ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectTemplateOff;
                    int first = Memory.ReadInt(chr + 0x3E0 + bank * 4);
                    int end   = Memory.ReadInt(chr + 0x400 + bank * 4);
                    int mpN   = Memory.ReadInt(chr + 0xC20 + bank * 4);
                    if (!WeaponModelFrames.IsRamPtr(mpN) || first < 0 || end <= first || end > 100) continue;
                    long mp = Memory.ToMmu(mpN);

                    int headN = Memory.ReadInt(mp + ShotEffectPool.MotionParamChain1);
                    if (!WeaponModelFrames.IsRamPtr(headN)) continue;
                    long rec = Memory.ToMmu(headN);
                    if (head == 0) head = rec;                                 // identity for the rebase check
                    for (int i = 0; i < 64 && rec != 0; i++)
                    {
                        int frame = Memory.ReadInt(rec + ShotEffectPool.MotRecFrameIdx);
                        int type  = Memory.ReadInt(rec + ShotEffectPool.MotRecType);
                        int cnt   = Memory.ReadInt(rec + ShotEffectPool.MotRecKeyCount);
                        int keysN = Memory.ReadInt(rec + ShotEffectPool.MotRecKeysPtr);
                        int nextN = Memory.ReadInt(rec + ShotEffectPool.MotRecNext);
                        if ((uint)frame >= 32 || cnt <= 0 || cnt > 300 || !WeaponModelFrames.IsRamPtr(keysN) || !IsKnownTrackType(type)) break;
                        if (type == ShotEffectPool.MotTypeScale)
                        {
                            // Keys must read as keyframes: small non-decreasing integer times at +0 of each 0x20.
                            long keys = Memory.ToMmu(keysN);
                            int t0 = Memory.ReadInt(keys);
                            int t1 = cnt > 1 ? Memory.ReadInt(keys + ShotEffectPool.MotKeyStride) : t0;
                            if (t0 >= 0 && t0 <= 1000 && t1 >= t0 && t1 <= 1000)
                                tracks.Add((keys, cnt));
                        }
                        rec = nextN == 0 ? 0 : Memory.ToMmu(nextN);
                    }
                }
                if (tracks.Count > 0)
                {
                    // Rebase on reload: a new chain address, or the canary key reading at its 1× value again
                    // (same address, freshly rebuilt data), means the keyframes are back at original scale.
                    float v0 = Memory.ReadFloat(tracks[0].Keys + ShotEffectPool.MotKeyValueOff);
                    if (head != _chainHead) { _chainHead = head; _ballApplied = 1f; _origKey0 = v0; }
                    else if (Math.Abs(v0 - _origKey0 * _ballApplied) > Math.Abs(_origKey0) * 0.05f + 0.001f)
                        { _ballApplied = 1f; _origKey0 = v0; }

                    float ratio = factor / _ballApplied;
                    if (Math.Abs(ratio - 1f) >= 0.005f)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[MobiusRing] applying sprite keyframe scale {factor:F2} ({tracks.Count} tracks)");
                        foreach (var (keys, cnt) in tracks)
                        {
                            byte[] blk = Memory.ReadBytesBatch(keys, cnt * ShotEffectPool.MotKeyStride);
                            if (blk == null) continue;
                            for (int k = 0; k < cnt; k++)
                                for (int c = 0; c < 3; c++)                    // value vec xyz at key+0x10
                                {
                                    int off = k * ShotEffectPool.MotKeyStride + ShotEffectPool.MotKeyValueOff + c * 4;
                                    BitConverter.GetBytes(BitConverter.ToSingle(blk, off) * ratio).CopyTo(blk, off);
                                }
                            Memory.WriteByteArray(keys, blk);
                        }
                        _ballApplied = factor;
                    }
                }
            }

            // 2. Core geometry: object scale on the template + every slot. The engine (Draw__10CCharacter)
            // re-applies it to the root frame each draw, so one write per factor change suffices.
            long tmpl = ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectTemplateOff;
            if (ScaleCore &&
                Math.Abs(Memory.ReadFloat(tmpl + ShotEffectPool.EffectObjectScale) - factor) > 0.005f)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[MobiusRing] applying core object scale {factor:F2}");
                for (int o = -1; o < ShotEffectPool.EffectSlotCount; o++)
                {
                    long obj = o < 0 ? tmpl
                        : ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectSlotObjectsOff + (long)o * ShotEffectPool.EffectSlotStride;
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale,     factor);
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale + 4, factor);
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale + 8, factor);
                }
            }

            // 3. Collision via BT radii — ⚠ CRASHES PCSX2 (live-bisected 2026-07-06: this lever alone,
            // one float write BT+0x2C 5.0→6.25, still Read Abort; mechanism unresolved — the live BT is a
            // runtime-built registry entry whose static content differs). Kept for reference, toggle OFF.
            // The shipping collision path is MaintainOrbHitbox (enemy-body inflation — mathematically
            // equivalent: hit ⇔ dist < orbR + bodyR) driven from MobiusRingEffect.
            int btN = Memory.ReadInt(ShotEffectPool.MainCharaEffectBase + ShotEffectPool.BtShotPtrOff);
            if (ScaleCollision && WeaponModelFrames.IsRamPtr(btN))
            {
                long bt = Memory.ToMmu(btN);
                if (bt != _btAddr)                                             // new BT (element/char change) → capture originals
                {
                    _btAddr = bt; _btFactor = 1f;
                    for (int i = 0; i < ShotEffectPool.BtShotRadiiCount; i++)
                        _btOrig[i] = Memory.ReadFloat(bt + ShotEffectPool.BtShotRadiiOff + i * 4);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[MobiusRing] BT 0x{bt:X} radii captured [{_btOrig[0]:F2}, {_btOrig[1]:F2}, {_btOrig[2]:F2}, {_btOrig[3]:F2}]");
                }
                if (Math.Abs(factor - _btFactor) > 0.005f)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[MobiusRing] applying collision scale {factor:F2} (phases 1..3)");
                    for (int i = 1; i < ShotEffectPool.BtShotRadiiCount; i++) // skip phase 0 (held ball)
                        if (_btOrig[i] > 0f && _btOrig[i] < 100f)              // sane radii only; 0 = phase without a hit
                            Memory.WriteFloat(bt + ShotEffectPool.BtShotRadiiOff + i * 4, _btOrig[i] * factor);
                    _btFactor = factor;
                }
            }
        }

        // ── orb collision growth via enemy-body inflation (the SAFE collision path) ──
        // hit ⇔ dist(orb, part) < orbRadius + bodyRadius, so adding (factor−1)×orbRadius to every enemy
        // body radius is exactly equivalent to growing the orb's damage sphere — with none of the BT
        // side effects (wall checks stay native). Own snapshot, ungated: every live enemy, while the orbs fly.
        private static readonly EnemyHitboxInflation _orbHitbox = new EnemyHitboxInflation();

        /// <summary>Inflate every active enemy's body radii to make Ruby's scaled orbs (<paramref name="factor"/>×)
        /// connect as if their damage sphere had grown. Call while the orbs are in flight; restore when they die.</summary>
        public static void MaintainOrbHitbox(float factor)
        {
            float bonus = (factor - 1f) * ShotEffectPool.RubyOrbBaseRadius;
            if (bonus <= 0.01f || !Player.CheckDunIsWalkingMode()) return;
            _orbHitbox.Maintain(bonus);
        }

        /// <summary>Restore every body radius inflated by <see cref="MaintainOrbHitbox"/> to its stock value.</summary>
        public static void RestoreOrbHitbox() => _orbHitbox.Restore();

        // ── the weapon's thread ─────────────────────────────────────────────────────────────────
        /// <summary>Mobius Ring's thread, one charge per launch (the class summary): the damage ramp per glow flash, the ball's growth,
        /// the fired orbs' damage and size until they expire, then everything back to 1×.</summary>
        public static void MobiusRingEffect()
        {
            //Declare inputs
            string message;
            int height;
            int width;
            ushort sleep = 1500;
            int chargeGlowTimer = 0x21DC449E;
            ushort chargeTimer = 0;

            //Check these addresses which tells us if Ruby is charging an attack
            if (Player.IsChargingAttack())
            {
                //Initialize the damage
                int damage = Player.Weapon.GetCurrentWeaponAttack() + Player.Weapon.GetCurrentWeaponMagic();

                //The energy ball only starts growing once the charge is fully built (first flash); from
                //there its size tracks the Mobius damage multiplier (M = damage / baseDamage). baseDamage
                //is the un-boosted attack+magic, floored at 1 to avoid divide-by-zero.
                int baseDamage = System.Math.Max(1, damage);
                bool fullyCharged = false;
                float ballScale = 1.0f;

                while (Player.IsChargingAttack())
                {
                    //Check if the game is paused during the charge
                    if (Player.CheckDunIsPaused())
                    {
                        ReusableFunctions.AwaitUnpause(1);
                    }
                    //If the damage increase reaches the set max value, stop increasing it further
                    if (damage >= ushort.MaxValue)
                    {
                        damage = ushort.MaxValue;
                    }
                    else damage += damage / 2;

                    //Set messages to display onscreen
                    if (damage > 9000)
                    {
                        message = "Total damage is over 9000";
                        height = 1;
                        width = message.Length;
                    }
                    else
                    {
                        message = "Total damage " + damage;
                        height = 1;
                        width = message.Length;
                    }

                    //Keep looping until chargeGlowTimer reaches the value 17008 or the player stops charging
                    while (Memory.ReadUShort(chargeGlowTimer) < 17008 && Player.IsChargingAttack())
                    {
                        if (Player.CheckDunIsPaused())
                        {
                            ReusableFunctions.AwaitUnpause(1);
                        }
                        Thread.Sleep(100);
                        continue;
                    }

                    //Save the value of the timer
                    chargeTimer = Memory.ReadUShort(chargeGlowTimer);

                    //Check if the timer hit the value we are looking for. This value makes Ruby flash
                    if (chargeTimer == 17008)
                    {
                        //The flash marks the charge as fully built — from here the ball may grow.
                        fullyCharged = true;

                        //Display current damage
                        DungeonMessages.DisplayMessage(message, height, width, sleep + 500);

                        Thread.Sleep(sleep);

                        //Reset Flash
                        Memory.WriteUShort(chargeGlowTimer, 0);
                    }

                    //Once fully charged, grow the energy ball in step with the damage multiplier the Mobius
                    //ramp has reached (scale = 1 + (M-1)*perMultiple, clamped). Re-applied every tick so the
                    //effect stays sized as the charge is held. See SetBallScale.
                    if (fullyCharged)
                    {
                        float m = (float)damage / baseDamage;
                        ballScale = 1.0f + (m - 1.0f) * RubyBallGrowthPerMultiple;
                        if (ballScale > RubyBallMaxScale) ballScale = RubyBallMaxScale;
                        if (ballScale < 1.0f) ballScale = 1.0f;
                        SetBallScale(ballScale);
                    }

                    Thread.Sleep(100);
                }

                //Charge released. Freeze the final size and re-apply it so the fired orbs (same effect pool
                //as the ball) fly at the grown size, and inflate enemy body radii so the orbs' COLLISION
                //grows to match (equivalent to a bigger damage sphere; see MaintainOrbHitbox).
                float finalBallScale = ballScale;
                if (fullyCharged)
                {
                    SetBallScale(finalBallScale);
                    MaintainOrbHitbox(finalBallScale);
                }

                //Wait for the fired orbs to actually spawn before tracking them. The release animation takes
                //a moment (the held ball is killed, then the shots grab pool slots), so the old approach of
                //using the slot list captured at CHARGE START raced it — when the list was empty the reset
                //below ran instantly and snapped the just-fired orbs back to 1× (and skipped their damage).
                //Poll the live flags instead; time out in case the charge was interrupted without firing.
                List<int> liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                for (int wait = 0; liveOrbs.Count == 0 && wait < 60; wait++)   // up to ~3s (fire lands ~1.5s in)
                {
                    Thread.Sleep(50);
                    if (Player.IsChargingAttack()) break;                 // interrupted → recharging already
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //Drive the boosted damage into every live orb until they all expire (slots re-read each tick
                //so late-spawning second orbs are covered too). Keep the enemy hitbox inflation fresh while
                //the orbs fly (covers enemies that spawn mid-flight).
                //Tick period: the engine inits an orb's damage at spawn and never rewrites it, so the only
                //race is spawn→our-next-tick; a point-blank orb can hit within a frame, so the period must
                //stay UNDER one frame (16.7ms @60fps). 10ms ≈ 0.6 frames of worst-case stale damage. PINE
                //writes aren't frame-synced, so exactly matching 16.7ms wouldn't align to anything anyway.
                int hitboxTick = 0;
                while (liveOrbs.Count > 0)
                {
                    //A NEW charge starting is the hand-off signal: its held ball is an active pool slot, so
                    //without this check the loop would never exit (blocking Dungeon from spawning a fresh
                    //MobiusRing), the new ball would inherit this charge's scale, and this loop would write
                    //THIS charge's damage into the new shot — a full size+power carry-over exploit. Break,
                    //reset below, and let the dispatcher start a clean ramp for the new charge. Any old orbs
                    //still flying keep their (already latched) boosted damage but snap to 1× visuals — brief
                    //and acceptable.
                    if (Player.IsChargingAttack()) break;

                    foreach (int id in liveOrbs)
                        Memory.WriteInt(RubyOrbs.Orb0.damage + 4 * id, damage);
                    if (fullyCharged && ++hitboxTick % 20 == 0)
                        MaintainOrbHitbox(finalBallScale);
                    Thread.Sleep(10);
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //All orbs expired (or a new charge took over) — snap the effect pool back to its original
                //size so the next charge starts from a clean 1× template, and restore enemy hitboxes.
                SetBallScale(1.0f);
                RestoreOrbHitbox();
            }
        }
    }
}
