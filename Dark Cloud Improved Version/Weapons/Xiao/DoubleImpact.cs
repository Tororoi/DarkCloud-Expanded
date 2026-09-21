using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Double Impact — every shot is two pellets. The pellet the game fires is joined by a TWIN the tick it appears: a
    /// second real pellet in the player's shot pool (step__5CSHOT flies it, collides it and plants its own damage entry
    /// with the weapon's ability flags, so each pellet rolls the weapon's steal / poison / critical / drain on its own),
    /// beside the first — the pair <see cref="PairWidth"/> units apart, side by side across the flight line, the same
    /// velocity and life. Every pellet is drawn as the Steel Slingshot's single stone (Mailbox.PelletSpriteId, the ISO's
    /// pellet-sprite hook), so the pair reads as two stones. Each pellet carries <see cref="DamageFactor"/> of the shot's
    /// attack: the game's own damage word is scaled down and the twin gets the same figure. Both pellets ricochet as the
    /// Hardshooter's do (<see cref="Hardshooter.Drive"/>, driven from here), each at a different target. Super Steve carrying
    /// a Double Impact SynthSphere has it too (<see cref="SuperSteve.SuperSteveEffect"/> drives it).
    /// </summary>
    internal static class DoubleImpact
    {
        private const string Tag = "[DoubleImpact] ";
        private const float DamageFactor = 0.75f;   // each pellet's attack, of the shot's
        private const float PairWidth    = 2f;      // the pair this far apart, side by side across the flight line
        private static bool _spriteSet;

        private static readonly bool[] _live = new bool[PlayerShotPool.SlotCount];   // the slots seen live last tick
        private static readonly bool[] _twin = new bool[PlayerShotPool.SlotCount];   // the slots that are twins (not twinned again)

        /// <summary>Drive every tick while Double Impact is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!_spriteSet || Memory.ReadInt(CodeCaves.Mailbox.PelletSpriteId) != Items.steelslingshot)
            { Memory.WriteInt(CodeCaves.Mailbox.PelletSpriteId, Items.steelslingshot); _spriteSet = true; }   // every pellet a single stone
            Hardshooter.Drive(true);                                             // its ricochets, first: a ricochet is not twinned
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_live[i] && !_twin[i] && !Hardshooter.IsBounced(i)) Twin(pool, i);
                if (!live) _twin[i] = false;
                _live[i] = live;
            }
        }

        private static void Twin(long pool, int slot)
        {
            long dmgA = PlayerShotPool.DamageAddr(pool, slot);
            int damage = Math.Max(1, (int)Math.Round(Memory.ReadInt(dmgA) * DamageFactor));
            Memory.WriteInt(dmgA, damage);
            int twin = -1;
            for (int j = 0; j < PlayerShotPool.SlotCount; j++)
                if (j != slot && Memory.ReadInt(PlayerShotPool.FlagAddr(pool, j)) == 0) { twin = j; break; }
            if (twin < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no free pellet slot: a single pellet this shot"); return; }
            long pa = PlayerShotPool.PosAddr(pool, slot), va = PlayerShotPool.VelAddr(pool, slot);
            float x = Memory.ReadFloat(pa), h = Memory.ReadFloat(pa + 4), y = Memory.ReadFloat(pa + 8);
            float vx = Memory.ReadFloat(va), vh = Memory.ReadFloat(va + 4), vy = Memory.ReadFloat(va + 8);
            // side by side: the pair straddles the flight line, half the width each way across it (in the ground plane)
            float gl = (float)Math.Sqrt(vx * vx + vy * vy);
            float sx = gl > 1e-3f ? vy / gl : 1f, sy = gl > 1e-3f ? -vx / gl : 0f;   // the flight line's right-hand side
            float half = PairWidth / 2f;
            Memory.WriteFloat(pa, x - sx * half); Memory.WriteFloat(pa + 8, y - sy * half);
            long tp = PlayerShotPool.PosAddr(pool, twin), tv = PlayerShotPool.VelAddr(pool, twin);
            Memory.WriteFloat(tp, x + sx * half); Memory.WriteFloat(tp + 4, h); Memory.WriteFloat(tp + 8, y + sy * half);
            Memory.WriteFloat(tv, vx); Memory.WriteFloat(tv + 4, vh); Memory.WriteFloat(tv + 8, vy);
            Memory.WriteInt  (PlayerShotPool.NoCollideAddr(pool, twin), 0);
            Memory.WriteInt  (PlayerShotPool.LifetimeAddr(pool, twin), Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot)));
            Memory.WriteInt  (PlayerShotPool.DamageAddr(pool, twin), damage);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, twin), 1f);
            Memory.WriteInt  (PlayerShotPool.FlagAddr(pool, twin), 1);      // live LAST
            _twin[twin] = true; _live[twin] = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shot: pellet slot {slot} and its twin slot {twin}, {damage} attack each");
        }

        /// <summary>The weapon or the floor went: the sprite back to the weapon's own, the ricochets' departures handed to the
        /// game, the latches start over.</summary>
        internal static void Stop()
        {
            if (_spriteSet) { Memory.WriteInt(CodeCaves.Mailbox.PelletSpriteId, 0); _spriteSet = false; }
            Hardshooter.Stop();
            Array.Clear(_live, 0, _live.Length); Array.Clear(_twin, 0, _twin.Length);
        }

        // ── Double Impact ──────────────────────────────────────────────────────────────────
        /// <summary>Xiao's Double Impact thread: hands every tick to <see cref="DoubleImpact.Drive"/> while the weapon is
        /// equipped, and stands it down once when it goes.</summary>
        public static void DoubleImpactEffect()
        {
            while (Player.InDungeonFloor() && Player.Weapon.GetCurrentWeaponId() == Items.doubleimpact)
            {
                DoubleImpact.Drive(!Player.CheckDunIsPaused() && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest());
                Thread.Sleep(16);
            }
            DoubleImpact.Stop();
        }

    }
}
