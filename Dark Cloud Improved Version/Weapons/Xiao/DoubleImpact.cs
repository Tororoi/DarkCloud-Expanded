using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Double Impact — every shot is two pellets. The pellet the game fires is joined by a TWIN the tick it appears: a
    /// second real pellet in the player's shot pool (step__5CSHOT flies it, collides it and plants its own damage entry
    /// with the weapon's ability flags, so each pellet rolls the weapon's steal / poison / critical / drain on its own),
    /// <see cref="TrailUnits"/> behind on the flight line with the same velocity and life. The sprite already shows two
    /// pellets, so the twin is drawn at <see cref="TwinScale"/> (invisible). Each pellet carries <see cref="DamageFactor"/>
    /// of the shot's attack: the game's own damage word is scaled down and the twin gets the same figure. Super Steve carrying
    /// a Double Impact SynthSphere has it too (<see cref="CustomXiaoEffects.SuperSteveEffect"/> drives it).
    /// </summary>
    internal static class DoubleImpact
    {
        private const string Tag = "[DoubleImpact] ";
        private const float DamageFactor = 0.75f;   // each pellet's attack, of the shot's
        private const float TrailUnits   = 5f;      // the twin this far behind the pellet, along its flight
        private const float TwinScale    = 0.001f;  // the twin's sprite: not seen (the pellet's own sprite shows two)

        private static readonly bool[] _live = new bool[PlayerShotPool.SlotCount];   // the slots seen live last tick
        private static readonly bool[] _twin = new bool[PlayerShotPool.SlotCount];   // the slots that are twins (not twinned again)

        /// <summary>Drive every tick while Double Impact is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_live[i] && !_twin[i]) Twin(pool, i);
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
            float vl = (float)Math.Sqrt(vx * vx + vh * vh + vy * vy);
            if (vl > 1e-3f) { x -= vx / vl * TrailUnits; h -= vh / vl * TrailUnits; y -= vy / vl * TrailUnits; }
            long tp = PlayerShotPool.PosAddr(pool, twin), tv = PlayerShotPool.VelAddr(pool, twin);
            Memory.WriteFloat(tp, x); Memory.WriteFloat(tp + 4, h); Memory.WriteFloat(tp + 8, y);
            Memory.WriteFloat(tv, vx); Memory.WriteFloat(tv + 4, vh); Memory.WriteFloat(tv + 8, vy);
            Memory.WriteInt  (PlayerShotPool.NoCollideAddr(pool, twin), 0);
            Memory.WriteInt  (PlayerShotPool.LifetimeAddr(pool, twin), Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, slot)));
            Memory.WriteInt  (PlayerShotPool.DamageAddr(pool, twin), damage);
            Memory.WriteFloat(PlayerShotPool.ScaleAddr(pool, twin), TwinScale);
            Memory.WriteInt  (PlayerShotPool.FlagAddr(pool, twin), 1);      // live LAST
            _twin[twin] = true; _live[twin] = true;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"shot: pellet slot {slot} and its twin slot {twin}, {damage} attack each");
        }

        /// <summary>The weapon or the floor went: nothing is held; the latches start over.</summary>
        internal static void Stop() { Array.Clear(_live, 0, _live.Length); Array.Clear(_twin, 0, _twin.Length); }
    }
}
