using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Angel Shooter "Guardian Grace" — while Xiao guards with an ability weapon whose HEAL flag is live, the native
    /// heal tick runs once a second and she pulses gently in time with it.
    ///
    /// MECHANISM — the dungeon overlay's per-frame loop (dun 0x1DB8240) increments a counter (main BSS 0x2A3684) and,
    /// when it reaches the threshold in the overlay's `slti` (<see cref="DunPatches.HealCadenceAddr"/>: 180 frames with
    /// the mod's cadence patch, 240 vanilla), grants +1 HP through AddNowLife IF the live weapon flags carry HEAL
    /// (0x800) and resets it. While the guard channel is open the counter is FLOORED at threshold − 60: whenever it
    /// dips below (right after a proc) it is set there, so the native tick fires every 60 frames — the heal itself
    /// stays the game's own +1, and the Angel Gear's party heal (<see cref="CustomXiaoEffects.DriveAngelGear"/>) rides
    /// the same wraps, so it speeds up with it. On release nothing is restored: the counter just climbs on from
    /// wherever it is.
    ///
    /// PRESENTATION: the weapon's moment plays ONCE as the guard starts — the Angel Shooter's healing-spring sparkle
    /// (CHealEffect @0x21EC4B40, fabricated with Set's own distributions), a gentle white flash and the heal chime
    /// (vanilla source HealingWater 0x1AF980); the Angel Gear's golden character-change burst (NewChangeFx @0x1EB3AD0,
    /// armed as the dungeon state-0x122 handler does, dun 0x1DB6940), a warm gold-white flash and the change jingle.
    /// Then, for the whole hold, Xiao's own ambient tint (CCharacter +0xCE0, the ADD Draw__10CCharacter applies over
    /// the scene ambient; nothing else writes the active character's) swells between <see cref="ShooterLow"/> and
    /// <see cref="ShooterHigh"/> (the Gear's pair in gold-white) in one smooth cycle per period, peaking as the native heal procs: the
    /// phase is the counter's own position between the floor and the threshold. The flash is the engine's global
    /// ambient pulse (unitAmbientAnime, dun 0x1DC1050: colour × sin + 64 over Speed frames), a separate term, so the
    /// two add. The tint is cleared when the channel closes.
    ///
    /// GATING: ability weapons only (Angel Shooter 309 / Angel Gear 313) with the live HEAL flag — a weapon whose Heal
    /// was stripped (the Drain/Heal opposing-pair sanitizer after attaching a Drain item) gets nothing, matching the
    /// native tick which gates on the same flag; other Heal-sphere weapons keep the plain vanilla tick.
    /// </summary>
    internal static class GuardianGrace
    {
        internal static bool Enabled = true;

        private const string Tag = "[GuardianGrace] ";

        private const int  XiaoId          = 1;
        private const int  HealFlagOffset  = 0xEE;         // WEAPON_HAVE live ability flags (halfword)
        private const int  HealFlagBit     = 0x800;        // HEAL — the same bit the native tick gates on

        // The native tick while guarding: fires this many frames after each proc (the counter is floored at threshold − this).
        private const int  PulsePeriodFrames = 60;
        private const int  VanillaThreshold  = 240;   // when the overlay word is not the expected `slti`

        // The hold's pulse: the ambient ADD swells high → low → high over each period, one smooth cycle that peaks on the
        // proc and is continuous across it. White for the Angel Shooter, gold-white for the Angel Gear. Tune in game.
        private static readonly float[] ShooterLow  = { 12f, 12f, 12f }, ShooterHigh = { 48f, 48f, 48f };
        private static readonly float[] GearLow     = { 16f, 14f,  8f }, GearHigh    = { 60f, 52f, 32f };

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

        // The guard-start flash — gentle amplitude, one 60-frame half-sine synced to the sparkle's life.
        // Angel Shooter flashes white; Angel Gear a warm gold-white.
        private const float WhiteR = 110f, WhiteG = 110f, WhiteB = 110f;
        private const float WarmR  = 130f, WarmG  = 114f, WarmB  = 75f;
        private const float FlashSpeed = 60f;
        private const int   FlashCount = 1;

        // Slot life for the one-shot chimes through the SE sequencer (see SeSeq): 90 frames (1.5 s)
        // gives them room to ring out before the sequencer's auto-stop.
        private const ushort ChimeFrames = 90;

        private const int  FastTickMs = 16, IdleTickMs = 250;

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
            bool open = false;
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool now = false; bool gear = false;
                    if (Enabled && Player.InDungeonFloor() && Player.CurrentCharacterNum() == XiaoId)
                    {
                        sleep = FastTickMs;
                        int weaponId  = Memory.ReadUShort(WeaponHave.BattleWeaponRecord);
                        gear          = weaponId == Items.angelgear;
                        bool ability  = gear || weaponId == Items.angelshooter;
                        bool healFlag = (Memory.ReadUShort(WeaponHave.BattleWeaponRecord + HealFlagOffset) & HealFlagBit) != 0;
                        now = ability && healFlag
                           && !Player.CheckDunIsPausedOrMenu()
                           && Player.Xiao.GetHp() > 0
                           && GuardWatch.IsGuarding();
                    }
                    if (now)
                    {
                        if (!open)
                        {
                            open = true;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "channel open (" + (gear ? "Angel Gear" : "Angel Shooter") + ": native heal every second)");
                            if (gear)
                            {
                                FabricateChangeBurst();
                                Player.FlashActiveCharacter(WarmR, WarmG, WarmB, FlashSpeed, FlashCount);
                                SeSeq.Play(SeSeq.ChangeJingle, ChimeFrames);
                            }
                            else
                            {
                                FabricateBurst();
                                Player.FlashActiveCharacter(WhiteR, WhiteG, WhiteB, FlashSpeed, FlashCount);
                                SeSeq.Play(SeSeq.HealChime, ChimeFrames);
                            }
                        }
                        int threshold = HealThreshold(), floor = threshold - PulsePeriodFrames;
                        int c = Memory.ReadInt(HealAbility.TickCounter);
                        if (c < floor) { Memory.WriteInt(HealAbility.TickCounter, floor); c = floor; }   // the native tick fires PulsePeriodFrames from now at the latest
                        float phase = Math.Clamp((c - floor) / (float)(threshold - 1 - floor), 0f, 1f);
                        float ease  = 0.5f + 0.5f * (float)Math.Cos(2 * Math.PI * phase);              // peak at the proc, low mid-period, back to peak on the next: no cut at the wrap
                        float[] lo = gear ? GearLow : ShooterLow, hi = gear ? GearHigh : ShooterHigh;
                        Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint,
                                         lo[0] + (hi[0] - lo[0]) * ease, lo[1] + (hi[1] - lo[1]) * ease, lo[2] + (hi[2] - lo[2]) * ease);
                        if (Memory.ReadInt(Fx + FxActive) == 1)   // follow her while the spring burst plays
                            WriteFxPos();
                    }
                    else if (open)
                    {
                        open = false;
                        Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, 0f, 0f, 0f);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "channel closed");
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

        /// <summary>The heal tick's threshold, read from the overlay's own `slti` so the floor is right on a patched
        /// (180) or vanilla (240) dun.bin.</summary>
        private static int HealThreshold()
        {
            uint w = Memory.ReadUInt(DunPatches.HealCadenceAddrMmu);
            return (w >> 16) == (DunPatches.HealCadenceOrig >> 16) ? (int)(w & 0xFFFF) : VanillaThreshold;
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
