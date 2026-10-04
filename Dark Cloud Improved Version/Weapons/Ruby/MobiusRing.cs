using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Mobius Ring — the charge ball's growth. The ball + fired orbs are CSHOT_EFFECT slots of the MainCharaEffectBase
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
    /// (WeaponAddresses.cs). Driven from CustomRubyEffects.MobiusRingEffect, which holds the growth formula; restored via
    /// factor 1.0.</summary>
    internal static class MobiusRing
    {
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
            // equivalent: hit ⇔ dist < orbR + bodyR) driven from CustomRubyEffects.MobiusRingEffect.
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
    }
}
