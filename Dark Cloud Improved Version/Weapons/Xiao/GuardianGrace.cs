using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Shooter "Guardian Grace" — while Xiao guards with a Guardian weapon, each native heal tick
    /// is amplified and celebrated. Roadmap PR 2; design in game_data/docs/angelshooter-guard-heal-re.md,
    /// reshaped per user direction 2026-09-08.
    ///
    /// MECHANISM — ride the VANILLA cadence, amplify the amount: the dungeon overlay's per-frame loop
    /// (dun 0x1DB8240) increments a heal-tick counter (main BSS 0x2A3684) and every 240 frames grants
    /// +1 HP through AddNowLife IF the live weapon flags carry HEAL (0x800, read from the battle
    /// record's +0xEE). We never touch the counter: while the guard channel is open we WATCH it, and
    /// when it wraps (a native heal just fired) we top the heal up to the ability total — +3 for the
    /// Angel Shooter (4 HP per proc) and +7 for the Angel Gear inheritance (8 HP per proc) — and fire
    /// the visuals at that same moment. Same average rate as the old 1-HP/s floor design, but one
    /// sparkle every 4 s instead of a constant shimmer.
    ///
    /// GATING: everything (bonus AND visuals) requires the live HEAL flag — a weapon whose Heal was
    /// stripped (e.g. the Drain/Heal opposing-pair sanitizer after attaching a Drain item) gets
    /// NOTHING, matching the native tick which gates on the same flag. Ability weapons only
    /// (Angel Shooter 309 / Angel Gear 313); other Heal-sphere weapons keep the plain vanilla tick.
    ///
    /// VISUALS on each proc, per weapon:
    ///  · ANGEL SHOOTER — the healing-spring moment relocated onto Xiao: CHealEffect sparkle
    ///    (@0x21EC4B40, fabricated with Set's own distributions), gentle white flash, heal chime
    ///    (SE 0x1B8 via SeSeq). Vanilla source for all three: HealingWater 0x1AF980 (same sparkle,
    ///    SndSePlay(0x1B8), speed-60 flash).
    ///  · ANGEL GEAR — the character-change moment: the GOLDEN materialize burst (NewChangeFx
    ///    @0x1EB3AD0 — a resident CCharacter playing an effect model; arm motion 6 + raise the
    ///    active flag and the engine steps it, anchors it to the ACTIVE character's pos/rot every
    ///    draw, and self-clears the flag at animation end), a gold-tinted flash, and the change
    ///    jingle (SE 0xF). Vanilla source: the dungeon state-0x122 materialize handler (dun
    ///    0x1DB6940), which arms exactly these fields (+0x2F0=1.0 frame, +0xC60=-1 speed,
    ///    +0xC64=6 motion, +0xC68=0) then SndSePlay(0xF) + a speed-90 white flash.
    /// </summary>
    internal static class GuardianGrace
    {
        internal static bool Enabled = true;

        private const string Tag = "[GuardianGrace] ";

        private const int  XiaoId          = 1;
        private const long HealTickCounter = 0x202A3684;   // dun overlay heal-tick counter (gp-0x616C)
        private const int  HealFlagOffset  = 0xEE;         // WEAPON_HAVE live ability flags (halfword)
        private const int  HealFlagBit     = 0x800;        // HEAL — the same bit the native tick gates on

        // CHealEffect single instance + field offsets (Set/Step 0x1B2900/0x1B2B00, decompiled).
        private const long Fx           = 0x21EC4B40;
        private const int  FxPos        = 0x000;           // base position vec4 (world)
        private const int  FxHeight     = 0x014;           // + i*0x10: per-particle base height
        private const int  FxAngle      = 0x210;           // + i*4
        private const int  FxAngVel     = 0x290;           // + i*4
        private const int  FxRadius     = 0x310;           // + i*4
        private const int  FxAlpha      = 0x390;           // + i*4
        private const int  FxPhase      = 0x410;           // + i*4 (0→π; sin envelope drives alpha)
        private const int  FxPhaseSpd   = 0x490;           // + i*4 (written by Set; Draw-side twinkle)
        private const int  FxActive     = 0x510;           // 1 = playing; Step self-clears at burst end
        private const int  FxParticles  = 32;

        // Gentle pulse on proc — deliberately softer/dimmer than the stock cyan charge flash.
        // The engine flash (dun unitAmbientAnime 0x1DC1050) is a HALF-SINE envelope: each frame
        // color = RGB*sin(Phase)+64 and Phase += π/Speed, so Speed IS the pulse length in frames.
        // 60 matches the sparkle burst's life (CHealEffect phase step 0.05236 ≈ π/60): the glow
        // ramps in to peak mid-burst and eases out as the last sparkles fade. Angel Gear's pulse is
        // gold-tinted to match its golden change burst, same gentle amplitude.
        private const float FlashWhite = 110f, FlashSpeed = 60f;
        private const float FlashGoldR = 150f, FlashGoldG = 118f, FlashGoldB = 40f;
        private const int   FlashCount = 1;

        // Proc chime via the engine's own SE sequencer (see SeSeq). 90 frames (1.5 s) of slot life
        // gives the one-shot room to ring out before the sequencer's auto-stop.
        private const ushort ChimeFrames = 90;

        // NewChangeFx — the golden character-change materialize burst (resident CCharacter playing an
        // effect model, dungeon-resident: init'd by dun GameInit, textures in the floor's block).
        // Arm the four fields, raise the flag; the engine's effect stepper runs Step__CCharacter on
        // it, the draw pass re-anchors it to the ACTIVE character's pos/rot each frame, and the flag
        // self-clears when the motion reaches its end. Flag touchpoints verified: init/arm/step/
        // self-clear/draw only — nothing else reads it (no input gating).
        private const long ChangeFx        = 0x21EB3AD0;
        private const int  ChangeFxFrame   = 0x2F0;        // current motion frame (float) — rewind to 1.0
        private const int  ChangeFxSpeed   = 0xC60;        // motion-speed override (float) — -1 = keyframe rate
        private const int  ChangeFxMotion  = 0xC64;        // motion id — 6 = the change burst
        private const int  ChangeFxMotArg  = 0xC68;        // cleared by the vanilla arm
        private const long ChangeFxActive  = 0x202A3518;   // step+draw gate (gp-0x62D8); engine self-clears

        private const int  FastTickMs = 50, IdleTickMs = 250;

        private static Thread _thread;
        private static readonly Random _rng = new Random();

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "GuardianGrace" };
            _thread.Start();
        }

        private static void Loop()
        {
            int prevCounter = -1;      // -1 = channel closed (re-seed on open; no false wrap)
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    if (Enabled && Player.InDungeonFloor() && Player.CurrentCharacterNum() == XiaoId)
                    {
                        sleep = FastTickMs;
                        int weaponId = Memory.ReadUShort(WeaponHave.BattleWeaponRecord);
                        int bonus = weaponId == Items.angelshooter ? 3
                                  : weaponId == Items.angelgear   ? 7 : 0;
                        bool healFlag = (Memory.ReadUShort(WeaponHave.BattleWeaponRecord + HealFlagOffset) & HealFlagBit) != 0;
                        bool open = bonus > 0 && healFlag
                                 && !Player.CheckDunIsPausedOrMenu()
                                 && Player.Xiao.GetHp() > 0
                                 && GuardWatch.IsGuarding();
                        if (open)
                        {
                            int c = Memory.ReadInt(HealTickCounter);
                            if (prevCounter < 0)
                            {
                                prevCounter = c;   // channel just opened mid-cycle — no retroactive proc
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                                    $"channel open ({(weaponId == Items.angelgear ? "Angel Gear +8" : "Angel Shooter +4")})");
                            }
                            else if (c < prevCounter - 60)     // wrap: the native +1 just fired
                            {
                                ushort hp = Player.Xiao.GetHp(), max = Player.Xiao.GetMaxHp();
                                if (hp < max)
                                {
                                    Player.Xiao.SetHp((ushort)Math.Min(hp + bonus, max));
                                    if (weaponId == Items.angelgear)
                                    {
                                        FabricateChangeBurst();
                                        Player.FlashActiveCharacter(FlashGoldR, FlashGoldG, FlashGoldB,
                                                                    FlashSpeed, FlashCount);
                                        SeSeq.Play(SeSeq.ChangeJingle, ChimeFrames);
                                    }
                                    else
                                    {
                                        FabricateBurst();
                                        Player.FlashActiveCharacter(FlashWhite, FlashWhite, FlashWhite,
                                                                    FlashSpeed, FlashCount);
                                        SeSeq.Play(SeSeq.HealChime, ChimeFrames);
                                    }
                                }
                            }
                            prevCounter = c;

                            if (Memory.ReadInt(Fx + FxActive) == 1)   // follow her while a burst plays
                                WriteFxPos();
                        }
                        else if (prevCounter >= 0)
                        {
                            prevCounter = -1;      // nothing to restore — the native cycle was never touched
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "channel closed");
                        }
                    }
                    else
                    {
                        prevCounter = -1;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
                    sleep = 1000;
                }
                Thread.Sleep(sleep);
            }
        }

        private static void WriteFxPos()
        {
            // The character's CObject position vec4 (+0x10..+0x1C, w=1) → the effect's base pos verbatim.
            for (int k = 0; k < 3; k++)
                Memory.WriteFloat(Fx + FxPos + k * 4,
                                  Memory.ReadFloat(CCharacter.Base + CCharacter.CharPos + k * 4));
            Memory.WriteFloat(Fx + FxPos + 12, 1.0f);
        }

        /// <summary>Replicate Set__11CHealEffect: 32 particles with the engine's own distributions
        /// (constants read from .data), then raise the active flag.</summary>
        private static void FabricateBurst()
        {
            WriteFxPos();
            for (int i = 0; i < FxParticles; i++)
            {
                float r() => (float)_rng.NextDouble();
                Memory.WriteFloat(Fx + FxRadius   + i * 4, 3.0f + r() * 6.0f);                    // 3..9
                Memory.WriteFloat(Fx + FxAngle    + i * 4, r() * 6.2831853f - 3.1415927f);        // -π..π
                Memory.WriteFloat(Fx + FxAngVel   + i * 4, 0.0349066f * (r() * 5.0f));            // ≤ ~10°/f
                Memory.WriteFloat(Fx + FxPhaseSpd + i * 4, 0.4f + 0.6f * r());
                Memory.WriteFloat(Fx + FxAlpha    + i * 4, 0f);
                Memory.WriteFloat(Fx + FxPhase    + i * 4, 0f);                                   // Set's quirk: rand then 0
                Memory.WriteFloat(Fx + FxHeight   + i * 0x10, 2.0f + r() * 10.0f);                // 2..12
            }
            Memory.WriteInt(Fx + FxActive, 1);
        }

        /// <summary>Replicate the dungeon state-0x122 materialize arm (dun 0x1DB6940): rewind
        /// NewChangeFx to frame 1 of motion 6 and raise its gate — the engine does the rest
        /// (steps it, anchors it to the active character, self-clears at motion end). Fields
        /// first, flag last, mirroring the vanilla write order.</summary>
        private static void FabricateChangeBurst()
        {
            Memory.WriteFloat(ChangeFx + ChangeFxFrame, 1.0f);
            Memory.WriteFloat(ChangeFx + ChangeFxSpeed, -1.0f);
            Memory.WriteInt(ChangeFx + ChangeFxMotion, 6);
            Memory.WriteInt(ChangeFx + ChangeFxMotArg, 0);
            Memory.WriteInt(ChangeFxActive, 1);
        }
    }
}
