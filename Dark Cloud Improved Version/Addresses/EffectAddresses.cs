// Effect address bank: the dungeon's resident pure-data effect pools — the fire / heat-haze raster emitters
// (FireRaster), the item-Bomb explosion pool and shockwave (BombEffect) and the thrown-gem elemental burst
// CSHOT_EFFECT[5] (MasekiEffect). Spawning any of them is field writes the engine's own step/draw loops pick up.
namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The dungeon FIRE / HEAT-HAZE system — the only framebuffer distortion in the game, and the vehicle
    /// for the Mirage clone's shimmer (ElfCave.MirageHazeDraw draws one raster at the clone).
    ///
    /// DrawRaster__11CDungeonMap (0x1C4610) walks a 20x20 per-tile fire array, but ONLY tiles within +/-4 of
    /// the CAMERA and with dist &lt;= 240 (both camera-relative — DrawMap__11CDungeonMap @0x1C286C RECOMPUTES
    /// that dist every frame, so forcing it from the mod is futile; the PNACH relaxes the 240 gate instead).
    /// Each enabled tile draws the raster emitters of its fire STRUCT at dngMap + fireIdx*Stride, placing
    /// emitter i at world ((localX + col*16)*10, localY*10, (localZ + row*16)*10) AFTER rotating (localX,localZ)
    /// by theta = (4 - tileRot) * 90deg.
    ///
    /// DrawFire__11CDungeonMap walks the SAME emitter list and draws each as a flame+light, so an injected
    /// emitter must zero its <see cref="EmitFlags"/> byte (bit0 = light, bit1 = flame) to be haze-only.
    /// </summary>
    internal static class FireRaster
    {
        // Per-tile fire array (relative to the CDungeonMap instance, i.e. NowDngMapPtr).
        internal const int  TileArray   = 0x9C50;   // 20x20 entries, 0x10 each
        internal const int  TileStride  = 0x10;
        internal const int  TileCount   = 400;
        internal const int  TileFireIdx = 0x00;     // int; -1 = no fire
        internal const int  TileRot     = 0x04;     // int; emitter offsets are rotated by (4-rot)*90deg
        internal const int  TileDist    = 0x08;     // float camera distance; <=240 to draw (game recomputes each frame)
        internal const int  TileEnabled = 0x0C;     // int; ==1 to draw

        // Fire struct: dngMap + fireIdx * Stride.
        internal const int  Stride      = 0x1D0;
        internal const int  EmitCount   = 0x4A2;    // short — RASTER emitter count (lh v1,0x4A2(v1) @0x1C48B8)
        internal const int  EmitPos     = 0x4B0;    // emitter[i] local pos (x,y,z floats), stride 0x10
        internal const int  EmitPosStride = 0x10;
        internal const int  EmitFlags   = 0x510;    // emitter[i] flag BYTE (stride 1): bit0 = light/glow, bit1 = flame.
                                                    // Zero it => heat-haze ONLY (DrawRaster never reads this byte).
        internal const int  MaxEmitSlot = 5;        // EmitPos[6] would land on EmitFlags[0] — never append past 5

        /// <summary>DAT_002a1da8 (RODATA): blendTextuerTest's multiplicative displacement gain. The distortion is
        /// (size/10000) * gain * wave * AMP, so 0 = no distortion and it scales linearly to full (~1.3 vanilla).
        /// Being plain DATA, the mod can RAMP it per tick — patching the AMP code constant every frame would be
        /// the recompiler-crashing hot-code surgery we avoid everywhere.</summary>
        internal const long DistortionGain = 0x202A1DA8;
    }

    /// <summary>
    /// The item-Bomb explosion effect (the visual thrown Bomb items make — resident on EVERY floor) plus
    /// the ground shockwave ring. RE'd from <c>SetBombEffect</c> @0x1D5940 / <c>SetBomb__15CItemBombEffect</c>
    /// @0x1D60A0 / <c>CheckBomb</c> @0x1D6190: spawning is PURE DATA WRITES into a 3-slot pool, so the mod
    /// can fabricate an explosion at any position (the native path's extra steps — SndSePlay(0x6C) and the
    /// CCollisionData damage — are function calls, replaced mod-side). A slot is FREE when all five
    /// <see cref="ActiveOffset"/> flags are 0 (CheckBomb's test). Each explosion = five staggered sub-puffs.
    /// Native SetBomb writes, per sub-puff i (0..4): pos float4 @ +i*0x10 (all five = the blast point),
    /// state 0 @ +0x50+i*4, timer i*−3 @ +0x64+i*4, 20.0f @ +0x78+i*4, 128.0f @ +0x8C+i*4, active 1
    /// @ +0xA0+i*4; then scale @ +0xB4, state[0]=2, state[1]=1. The native caller also fires the shockwave
    /// when scale &gt; 1: pos float4 @ +0, 1.0f @ +0xC, scale×30 @ +0x10/+0x14, 0 @ +0x18, scale×15 @ +0x1C,
    /// 0 @ +0x20/+0x24, 1 @ +0x28.
    /// </summary>
    internal static class BombEffect
    {
        internal const long PoolPtr      = 0x202A35E4; // NowBombEffect (gp-0x620C) → 3 slots × 0xC0
        internal const long ShockWavePtr = 0x202A35E8; // NowShockWave (gp-0x6208) → single struct
        internal const int  SlotCount    = 3;
        internal const int  SlotStride   = 0xC0;
        internal const int  SubPuffs     = 5;
        internal const int  PosOffset    = 0x00;  // + i*0x10, float4 per sub-puff
        internal const int  StateOffset  = 0x50;  // + i*4
        internal const int  TimerOffset  = 0x64;  // + i*4 (int, i*−3 = staggered starts)
        internal const int  SizeAOffset  = 0x78;  // + i*4 (float, 20.0)
        internal const int  SizeBOffset  = 0x8C;  // + i*4 (float, 128.0)
        internal const int  ActiveOffset = 0xA0;  // + i*4 (int; any nonzero = slot busy)
        internal const int  ScaleOffset  = 0xB4;  // float — whole-explosion scale
    }

    /// <summary>
    /// The THROWN-GEM elemental burst (Fire/Ice/Thunder/Wind/Holy Gem, items 161-165) — ELF global
    /// <c>MasekiEffect</c> (魔石 = "magic stone"), a <c>CSHOT_EFFECT[5]</c>: one slot per element.
    ///
    /// Like the item-Bomb pool this is a PURE-DATA effect, and for the same three reasons:
    ///   • RESIDENT — dungeon load unconditionally does LoadFile("dun/effect/maseki_ex.chr") and Entry2/ReEntry's
    ///     all five slots into a dedicated 65KB arena (MasekiModelBuffer). NOT gated on owning/throwing a gem.
    ///     All five descriptors name the SAME .chr — they differ only in element bits and MOTION INDEX
    ///     (<see cref="MotionIdxOffset"/>). One model, five animations.
    ///   • STEPPED + DRAWN unconditionally every frame by the dun overlay's per-frame loops (step dun 0x1DB8550,
    ///     draw dun 0x1DAE590) — the loop ADJACENT to the CBomb pool <see cref="BombEffect"/> already drives,
    ///     under the same draw gate. If Big Bang renders, this renders.
    ///   • SPAWNING IS FIELD WRITES. Vanilla spawns from Step__14CMainItemModel (main 0x1D4E20) via
    ///     Set__12CSHOT_EFFECT (0x1ADD60) + SetDmg/SetLifeTime/SetWait — every one of which is a plain store.
    ///     Nothing is allocated, loaded or registered at spawn time, so the mod replicates the stores itself.
    ///
    /// Each element slot holds <see cref="SubSlots"/> = 3 INITIALISED sub-slots, each a real CCharacter
    /// (position/rotation/SCALE/motion), so bursts get an independent size — that is the "big for the main
    /// pellet, normal for shrapnel" knob. Sub-slots 3..7 physically exist but Entry2 never gave them model or
    /// motion data: activating them is garbage or a crash. Three per element is the hard cap.
    ///
    /// Bursts are fire-and-forget: Step clears the active flag when the lifetime expires.
    /// See also the memory note "maseki-gem-effect-pool". Do NOT confuse with CWeaponElement (CWeaponElFx) —
    /// that is the ON-HIT elemental spark an elemental WEAPON makes, spawned from CheckDmg.
    /// </summary>
    internal static class MasekiEffect
    {
        internal const long SlotBase     = 0x21E5B380; // MasekiEffect[0]; + element*SlotStride
        internal const int  SlotStride   = 0xA160;     // sizeof(CSHOT_EFFECT)
        internal const long DescBase     = 0x21DC2230; // MyEntryEffect_Maseki00..04; + element*DescStride
        internal const int  DescStride   = 0x70;
        internal const int  SubSlots     = 3;          // INITIALISED sub-slots per element (Entry2's count)

        // Element index — the slot AND the descriptor's motion index select the animation.
        internal const int  Fire = 0, Ice = 1, Thunder = 2, Wind = 3, Holy = 4;

        // ── the sub-slot CCharacter: SubCharBase + i*SubCharStride, relative to the element slot ──
        internal const int  SubCharBase   = 0x11C0;
        internal const int  SubCharStride = 0x11B0;    // sizeof(CCharacter)

        // ── per-sub-slot arrays, relative to the element slot ──
        internal const int  VelOffset       = 0x9F40;  // + i*VelStride (float4; gems are stationary → 0,0,0,1)
        internal const int  VelStride       = 0x10;    // one float4 per sub-slot
        // The burst's little STATE MACHINE. All three MUST be re-seeded on every spawn: a sub-slot is reused, and
        // if the phase is left at the value the PREVIOUS burst died in, Step never reaches its expire branch — the
        // active flag is never cleared, the slot LEAKS, and once all three leak nothing renders ever again.
        internal const int  PhaseCountOff   = 0x9FC0;  // + i*2  (u16) — reset to 0
        internal const int  PhaseTimerOff   = 0x9FD0;  // + i*4  (int, seeded from descriptor +0x38)
        internal const int  PhaseOffset     = 0x9FF0;  // + i*2  (u16) — reset to 0
        internal const int  ActiveOffset    = 0xA000;  // + i*2  (u16) — THE GATE. Write LAST. Step clears it on expiry.
        internal const int  DamageOffset    = 0xA010;  // + i*4  (int) — 0 = visual-only burst
        internal const int  WepStatusOffset = 0xA030;  // + i*4
        internal const int  UserIdOffset    = 0xA050;  // + i*2  (u16)
        internal const int  UserId2Offset   = 0xA060;  // + i*2  (u16)
        internal const int  Unk070Offset    = 0xA070;  // + i*4
        internal const int  LoopOffset      = 0xA0B0;  // + i*4  (int, -1 = no loop)
        internal const int  RandomRateOff   = 0xA0D0;  // + i*4  (float; >=0 makes Draw jitter the position — a shake knob)
        internal const int  LifeTimeOffset  = 0xA0F0;  // + i*4  (int; vanilla 10)
        internal const int  EnemyAttrOffset = 0xA110;  // + i*4  (int, -1 = normal)
        internal const int  NoSoundOffset   = 0xA130;  // + i    (byte)
        internal const int  WaitOffset      = 0xA138;  // + i    (byte; vanilla 5 = re-hit wait)

        /// <summary>+ i (byte) — the collision RE-HIT COOLDOWN, and the switch that makes a burst visual-only.
        /// Step creates the burst's damage sphere only when this is &lt; 1 (then re-arms it to
        /// <see cref="WaitOffset"/>), so seeding it ABOVE the burst's lifetime means the sphere is NEVER created.
        /// That matters for more than the damage: a burst that collides DEALS a hit, which advances the engine's
        /// hitCnt — and anything driving effects off that ring then spawns another burst, and cascades.
        /// Zeroing <see cref="DamageOffset"/> alone is NOT enough (the damage formula floors at 1 HP).</summary>
        internal const int  HitCooldownOff  = 0xA140;
        internal const byte NoCollide       = 0x7F;   // >> any burst lifetime → the cooldown never reaches 0
        internal const int  Unk148Offset    = 0xA148;  // int
        internal const int  LastEnteredOff  = 0xA150;  // int — index of the most recently entered sub-slot

        // ── descriptor (BT_SHOT_EFFECT) fields we read ──
        internal const int  PhaseSeedOffset = 0x38;    // int  — seeds PhaseTimerOff
        internal const int  MotionIdxOffset = 0x4C;    // s16  — the element's animation in maseki_ex.chr
        /// <summary>float — the RADIUS of the damage sphere Step spawns for a burst (vanilla 10.0). Widening it is
        /// how a burst damages an AREA, and it makes the engine resolve that damage itself: guards, elements,
        /// death, drops all behave, unlike a direct HP write which produces an unkillable walking corpse.
        /// It lives on the SHARED descriptor, so a change is also seen by real thrown gems of that element —
        /// snapshot and restore it (GemBurst does).</summary>
        internal const int  ColRadiusOffset = 0x28;
        /// <summary>int — the ELEMENT BITS a burst's damage carries (1=Fire, 2=Ice, 4=Thunder, 8=Wind, 0x10=Holy;
        /// 0 = none). Step passes it into CCollisionData::Set, and CheckDmg reads it back off the collision to pick
        /// the element. Like the radius it is on the SHARED descriptor — snapshot and restore it (GemBurst does),
        /// or real thrown gems of that element change too.</summary>
        internal const int  ElementBitsOffset = 0x40;

        internal static long Slot(int element)   => SlotBase + (long)element * SlotStride;
        internal static long Desc(int element)   => DescBase + (long)element * DescStride;
        internal static long SubChar(long slot, int i) => slot + SubCharBase + (long)i * SubCharStride;

        /// <summary>The pool is live only once the dungeon has entered the models: slot[0] holds the pointer to
        /// its own descriptor. If it doesn't, Draw early-outs and writing fields would do nothing (or worse).
        /// NOTE the pointer in RAM is a GUEST address (0x01DC2230) — it must be mapped before comparing with our
        /// MMU-space <see cref="Desc"/> (0x21DC2230), or this never matches and every burst silently no-ops.</summary>
        internal static bool Loaded(int element)
        {
            int p = Memory.ReadInt(Slot(element));
            return Memory.IsValidGuest(p) && Memory.ToMmu(p) == Desc(element);
        }
    }
}
