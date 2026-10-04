using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>"Tornado Charge Buff", the Halberd line's charge (docs/hercules-wrath.md): Ungaga's charge effect c10a_ex (the shot
    /// UngagaKey_Play fires every 30 frames of a held charge from the main-character effect instance) travels faster and is drawn
    /// larger, hit spheres to match, at a form that grows down the build path (<see cref="TierOf"/>): the Halberd starts it, the
    /// Scorpion, the Mirage (and the Cactus beside it) and Hercules' Wrath (and the Terra Sword and Babel's Spear beside it) each
    /// carry a stronger one — Hercules' Wrath's at 2.5× the speed and 1.7× the size. Ungaga's own charge animation is untouched.
    /// Applied as data the engine reads every frame (<see cref="Apply"/>) and put back when the spear goes (<see cref="Restore"/>);
    /// Super Steve with a sphere of the line carries the form on a charged shot instead (<see cref="DriveSphere"/>).</summary>
    internal static class Halberd
    {
        private const float AnimFactor   = 1f;     // the effect's animation speed (1 = its own)

        /// <summary>A form of the ability: the effect's travel speed (over its fixed 45-frame flight, its reach too) and its size and
        /// hit radius.</summary>
        internal readonly record struct Tier(string Name, float Travel, float Scale);
        private static readonly Tier HalberdTier  = new("Halberd", 1.4f, 1.1f);
        private static readonly Tier Scorpion = new("Scorpion", 1.7f, 1.3f);
        private static readonly Tier Mirage   = new("Mirage", 2.0f, 1.5f);
        private static readonly Tier Hercules = new("Hercules' Wrath", 2.5f, 1.7f);

        /// <summary>The form a weapon carries, or null: the Halberd line, each branch at its parent's form.</summary>
        internal static Tier? TierOf(int weaponId) => weaponId switch
        {
            Items.halberd => HalberdTier,
            Items.scorpion => Scorpion,
            Items.mirage or Items.cactus => Mirage,
            Items.herculeswrath or Items.terrasword or Items.babelsspear => Hercules,
            _ => null,
        };
        private static Tier _tier;                  // the form applied (valid while _origCfg != 0)
        private const int   TickMs      = 250;
        private const int   Motions     = 3;      // c10a_ex's KEYs: rise 5–25, active 30–50, vanish 55–70
        private const string Tag = "[Halberd] ";
        private const int    CfgSpeed = 0x18;     // BT_SHOT_EFFECT: the phase speeds (float ×4), the direction's length when a phase starts

        private static uint _origCfg;                                   // the engine's config pointer we replaced (0 = not applied)
        private static readonly Dictionary<long, float[]> _steps = new(); // Mot_List (MMU) → its original steps

        public static void TornadoChargeBuffEffect()
        {
            for (Tier? t = TierOf(Player.Weapon.GetCurrentWeaponId()); t != null && Player.InDungeonFloor(); t = TierOf(Player.Weapon.GetCurrentWeaponId()))
            {
                if (_origCfg != 0 && _tier != t.Value) Restore();                                        // another form of the line: from scratch
                if (Player.CurrentCharacterNum() == Player.UngagaId && Player.CheckDunIsWalkingMode()) Apply(t.Value);
                Thread.Sleep(TickMs);
            }
            Restore();
        }

        private static long Inst => ShotEffectPack.CharaMainEffect;

        /// <summary>The form onto the live instance, once per effect load — everything is data the ENGINE reads every frame, so
        /// nothing is re-asserted per frame and nothing races the engine:
        ///  · speed: the KEY step of each of the effect's motions in its Mot_List (sub-shot CCharacter +0x344 → entries of 0x10:
        ///    start, end, step), which Step__CCharacter reads as the play rate whenever the rate override is −1 — and
        ///    Step__12CSHOT_EFFECT sets it to −1 on every phase change;
        ///  · hit area and travel: the instance's config pointer (+0) aimed at a copy of the config (CodeCaves.HerculesCfg) with every
        ///    phase's radius (+0x28..+0x34) × the form's scale and speed (+0x18..+0x24) × its travel factor — Step reads the radius
        ///    from it each frame it plants, and Set__12CSHOT_EFFECT / Step normalise the direction and scale it by the phase's speed
        ///    when a shot starts or changes phase (c10a_ex: speeds 0.2, 0.9, 0.2, 0.2; radii 8, 8, 6, 6; its flight is SetWait's
        ///    45 frames, so it also goes that many times as far);
        ///  · size: each sub-shot's CObject scale (+0x90), which the draw folds into the model each frame and Set__12CSHOT_EFFECT
        ///    never rewrites.
        /// A floor load or a menu rebuilds the instance with the engine's own config pointer: the next tick applies it again.</summary>
        private static void Apply(Tier tier)
        {
            if (Memory.ReadUInt(ShotEffectPack.MainEffectLivePtr) != (uint)(Inst - 0x20000000L)) return;   // not the live instance
            uint cfg = Memory.ReadUInt(Inst + ShotEffectPack.OffCfg);
            if (cfg == CodeCaves.HerculesCfgGuest) return;                                                 // applied, and still ours
            if (!Memory.IsValidGuest(cfg)) return;
            int count = Memory.ReadInt(Inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return;
            // A fresh entry (a floor load, a menu): its lists are new, its scale words 1.
            _steps.Clear();
            byte[] c = Memory.ReadBytesBatch(Memory.ToMmu((int)cfg), ShotEffectPack.CfgSize);
            if (c == null) return;
            for (int ph = 0; ph < 4; ph++)
            {
                int r = ShotEffectPack.CfgRadiusMuzzle + ph * 4, v = CfgSpeed + ph * 4;
                BitConverter.GetBytes(BitConverter.ToSingle(c, r) * tier.Scale).CopyTo(c, r);
                BitConverter.GetBytes(BitConverter.ToSingle(c, v) * tier.Travel).CopyTo(c, v);
            }
            Memory.WriteBytesBatch(CodeCaves.HerculesCfg, c);
            for (int j = 0; j < count; j++)
            {
                long obj = Inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride;
                Memory.WriteVec3(obj + CCharacter.CharScale, tier.Scale, tier.Scale, tier.Scale);
                uint list = Memory.ReadUInt(obj + ShotEffectPack.ObjFrameTb);
                if (!Memory.IsValidGuest(list)) continue;
                long l = Memory.ToMmu((int)list);
                if (_steps.ContainsKey(l)) continue;                                                         // shared by several sub-shots
                var orig = new float[Motions];
                for (int m = 0; m < Motions; m++)
                {
                    long a = l + m * CCharacter.MotionEntryStride + CCharacter.MotionEntryStep;
                    orig[m] = Memory.ReadFloat(a);
                    Memory.WriteFloat(a, orig[m] * AnimFactor);
                }
                _steps[l] = orig;
            }
            _origCfg = cfg; _tier = tier;
            Memory.WriteUInt(Inst + ShotEffectPack.OffCfg, CodeCaves.HerculesCfgGuest);                    // last: the radii from here on
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"applied: {count} sub-shots scaled, {_steps.Count} motion list(s) at ×{AnimFactor:F1}, config 0x{cfg:X} → radii ×{tier.Scale:F1}, speeds ×{tier.Travel:F1} ({tier.Name}'s form)");
        }

        // ── Super Steve with a sphere of the line: a CHARGED shot carries the form ──
        private const double SphereChargeSeconds = 0.5;   // hold the draw this long to charge
        private const float  SphereDamage        = 1.5f;  // the charged pellet's attack
        private static readonly bool[] _ssSeen = new bool[PlayerShotPool.SlotCount];   // pellets already live before this shot
        private static bool     _ssHolding, _ssCharged, _ssArmed;
        private static DateTime _ssHoldStart;

        /// <summary>Every dispatch tick while Super Steve is out: with a sphere from the Halberd line, holding the shot
        /// <see cref="SphereChargeSeconds"/> charges it (the game's charge-complete flash; the shot's weapon HP billed as a charged
        /// one), and the pellet a charged release fires is drawn the form's size larger, flies its travel factor faster and hits
        /// for <see cref="SphereDamage"/>× — the sprite only: the pellet's hit sphere is the engine's fixed one. An ordinary shot is
        /// untouched.</summary>
        internal static void DriveSphere(int sphere, bool active)
        {
            Tier? tier = active ? TierOf(sphere) : null;
            int state = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = tier != null && (state == PlayerAction.XiaoShotDraw || state == PlayerAction.XiaoShotHold);
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            if (holding)
            {
                if (!_ssHolding)
                {   // a new draw: whatever is already flying is not this shot
                    _ssHolding = true; _ssCharged = false; _ssHoldStart = GameClock.Now;
                    for (int i = 0; i < PlayerShotPool.SlotCount; i++) _ssSeen[i] = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                }
                double held = (GameClock.Now - _ssHoldStart).TotalSeconds;
                if (!_ssCharged)
                {
                    ChargeTint.Ramp(SphereChargeSeconds - held);
                    if (held >= SphereChargeSeconds) { _ssCharged = true; Player.FlashChargeComplete(); ChargeTint.Clear(); }
                }
                ChargedShotWhp.Arm(_ssCharged ? ChargedShotWhp.ChargedFactor : 1f);
                return;
            }
            if (_ssHolding)
            {   // released: a charged draw arms its pellet
                _ssHolding = false; _ssArmed = _ssCharged; _ssCharged = false;
                if (!_ssArmed) ChargeTint.Clear();
            }
            if (!_ssArmed || tier == null) { _ssArmed = false; return; }
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (!live) { _ssSeen[i] = false; continue; }
                if (_ssSeen[i]) continue;
                Tier t = tier.Value;
                Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, i), t.Scale);
                long v = PlayerShotPool.VelAddr(pool, i);
                for (int c = 0; c < 3; c++) Memory.WriteFloat(v + c * 4, Memory.ReadFloat(v + c * 4) * t.Travel);
                long d = PlayerShotPool.DamageAddr(pool, i);
                int dmg = Memory.ReadInt(d);
                if (dmg > 0) Memory.WriteInt(d, (int)Math.Round(dmg * SphereDamage));
                _ssSeen[i] = true; _ssArmed = false;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged pellet (slot {i}, {t.Name}'s form): ×{t.Scale:F1} size, ×{t.Travel:F1} speed, damage {dmg} → {(int)Math.Round(dmg * SphereDamage)}");
                break;
            }
        }

        /// <summary>The spear gone: the engine's own config, steps and sizes back — only where they still hold ours.</summary>
        private static void Restore()
        {
            if (_origCfg == 0) return;
            if (Memory.ReadUInt(Inst + ShotEffectPack.OffCfg) == CodeCaves.HerculesCfgGuest)
            {
                Memory.WriteUInt(Inst + ShotEffectPack.OffCfg, _origCfg);
                int count = Memory.ReadInt(Inst + ShotEffectPack.OffCount);
                for (int j = 0; j < count && j < ShotEffectPack.SubShots; j++)
                    Memory.WriteVec3(Inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride + CCharacter.CharScale, 1f, 1f, 1f);
                foreach (var (l, orig) in _steps)
                    for (int m = 0; m < Motions; m++)
                    {
                        long a = l + m * CCharacter.MotionEntryStride + CCharacter.MotionEntryStep;
                        if (Math.Abs(Memory.ReadFloat(a) - orig[m] * AnimFactor) < 1e-4f) Memory.WriteFloat(a, orig[m]);
                    }
            }
            _steps.Clear(); _origCfg = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "restored");
        }
    }
}
