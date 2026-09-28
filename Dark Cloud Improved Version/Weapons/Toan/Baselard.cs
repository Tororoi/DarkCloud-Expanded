using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Baselard — "Heavy Hand": every hit from the sword throws its enemy far. Each melee hit, the lunge and
    /// the whirlwind kick at a third of the distance Big Bang's blast does (MeleeKick, the engine's own knockback words),
    /// for as long as the Baselard is in Toan's hand on a dungeon floor. Super Steve carrying its SynthSphere throws with
    /// every PELLET the same way (<see cref="DriveSphere"/>): a pellet's hit carries no kick of its own, so the guard-bypass
    /// cave stamps this one on every entry planted with her pellets' damage (Mailbox.PelletKickDamage, as Dragon's Y's shot),
    /// out of the hit itself — the vanilla guard test kept. A Claymore sphere drives the same pellet throw.</summary>
    internal static class Baselard
    {
        // Big Bang's blast kicks at 3.5 fading 0.12 a frame: ≈ 3.5² / (2·0.12) ≈ 51 units. A third of that distance at
        // the same fade is a strength of 3.5 / √3 (the distance goes with the square of it).
        internal const float KickStrength = 2.02f;    // ≈ 17 units (the Claymore, and both spheres, throw with the same)
        internal const float KickDecay    = 0.12f;     // the blast's own fade: the throw reads the same, only shorter
        private const int   TickMs       = 100;

        public static void HeavyHandEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Baselard] heavy hand: every hit kicks at {KickStrength:F2} fading {KickDecay:F2} (≈ {KickStrength * KickStrength / (2 * KickDecay):F0} units)");
            while (Player.Weapon.GetCurrentWeaponId() == Items.baselard && Player.InDungeonFloor())
            {
                if (Player.CurrentCharacterNum() == Player.ToanId) MeleeKick.Set(KickStrength, KickDecay);   // re-asserted: a word found vanilla is written again
                else MeleeKick.Restore();                                                                    // an ally swinging keeps the game's own kick
                Thread.Sleep(TickMs);
            }
            MeleeKick.Restore();                                                        // swapped, or off the floor: the game's own kick back
        }

        private static int _stampedDamage = int.MinValue;   // the pellet damage the kick is stamped on (MinValue = nothing stamped)
        /// <summary>Every tick while Super Steve carries the Baselard sphere: the kick stamped on her pellets' damage (the live
        /// pellets' damage word — one value per weapon and level); off when the sphere goes.</summary>
        internal static void DriveSphere(bool active)
        {
            if (!active)
            {
                if (_stampedDamage != int.MinValue) { Memory.WriteInt(CodeCaves.Mailbox.PelletKickDamage, 0); _stampedDamage = int.MinValue; }
                return;
            }
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                if (Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) == 0) continue;
                int damage = Memory.ReadInt(PlayerShotPool.DamageAddr(pool, i));
                if (damage <= 0 || damage == _stampedDamage) return;                     // a marked pellet (another ability's) or the one already stamped
                Memory.WriteFloat(CodeCaves.Mailbox.PelletKickStrength, KickStrength);
                Memory.WriteFloat(CodeCaves.Mailbox.PelletKickDecay, KickDecay);
                Memory.WriteInt  (CodeCaves.Mailbox.PelletKickDamage, damage);
                _stampedDamage = damage;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Baselard] heavy hand on her pellets: kick {KickStrength:F2} fading {KickDecay:F2} stamped on damage {damage}");
                return;
            }
        }
    }
}
