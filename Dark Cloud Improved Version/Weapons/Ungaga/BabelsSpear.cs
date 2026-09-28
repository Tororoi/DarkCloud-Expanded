using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Babel's Spear — "Curse of Babel": a five-second guard charge raises a giant copy of the spear out of the ground
    /// under the locked-on enemy, point up, most of it still buried. The enemy above it is struck and thrown off it, and it
    /// and every enemy within <see cref="ConfusionRadius"/> of the spear are CONFUSED for as long as the spear stands
    /// (<see cref="SpearSeconds"/>): tinted a pale purple, each attacks a target of its own choosing — the player, or another
    /// enemy — and its swings hurt other enemies. Once risen the spear turns slowly on the spot.
    ///
    /// The pieces, all data:
    ///  · the spear is <see cref="BladeProp"/>'s copy of the equipped weapon (the same engine-drawn copy Big Bang hangs over
    ///    its target), baked to point up and placed at the target's feet with the root sunk so only <see cref="Exposed"/>
    ///    units of the tip show (the c10w10 mesh runs −9.6 … +16.0 along its axis, the tip at 16), risen over
    ///    <see cref="EmergeSeconds"/>, then turned <see cref="SpinDegPerSec"/> a second;
    ///  · the strike is one player-hit sphere (CollisionPool.PlayerHitEntry) at the target's body, the weapon's attack, its
    ///    kick words pointing away from the spear;
    ///  · confusion rides the Mirage's per-slot target-pointer table (CodeCaves.PtrTable, read by the cold-hosted
    ///    _GET_POSITION / _GET_DISTANCE): a confused slot's entry points at the LIVE position of a target rolled every
    ///    <see cref="RerollSeconds"/> — the player global, or another enemy's CCharacter position, which then tracks it with no
    ///    upkeep — and the table is owned here while any slot is confused (Mirage's loop stands down, <see cref="OwnsTable"/>);
    ///  · friendly fire: a fresh attack entry a confused enemy plants (pool owner = slot·5 + 200) has its victim mask OR'd
    ///    with 2 (hurts enemies as well as the player), so the swing lands on whoever it was aimed at through CheckDmg;
    ///  · the tint is the unit's ambient add (CCharacter +0xCE0), re-asserted each tick, cleared at the end.</summary>
    internal static class BabelsSpear
    {
        private const string Tag = "[Babel] ";
        private const int    TickMs           = 50;
        private const int    GuardChargeMs    = 5000;   // hold the guard this long to summon
        private const int    GuardLoopMotion  = 9, GuardMoveMotion = 33;   // Ungaga's guard-hold poses (the Mirage's)
        private const float  SpearSeconds     = 20f;
        private const float  EmergeSeconds    = 0.6f;
        private const float  SpinDegPerSec    = 30f;
        private const float  Scale            = 4f;     // the copy's size; the spear is 25.6 long unscaled
        private const float  TipZ             = 16.0f;  // the mesh's tip along its axis (c10w10: −9.6 … 16.0)
        private const float  Exposed          = 26f;    // how much of the tip stands above the ground once risen
        private const float  Buried           = 4f;     // how far below the ground the tip starts
        private const float  ConfusionRadius  = 150f;
        private const float  RerollSeconds    = 2f;
        private const float  StrikeRadius     = 6f;
        private const float  KickStrength     = 2.475f, KickDecay = 0.12f;   // ≈ 25 units, away from the spear
        private const float  NoLockReach      = 60f;    // no lock: the nearest enemy within this, else the spear rises ahead of Ungaga
        private const float  AheadDistance    = 15f;
        // Pale purple, as the unit's ambient add (scene ambient ≈ 128 is neutral): not too bright.
        private const float  TintR = 40f, TintG = 16f, TintB = 56f;

        /// <summary>True while any enemy is confused: the per-slot target table is this ability's (Mirage's loop leaves it).</summary>
        internal static bool OwnsTable { get; private set; }

        private static readonly Random _rng = new();
        private static bool     _up;               // the spear stands
        private static DateTime _summoned, _lastReroll;
        private static float    _sx, _sy, _ground, _yaw;
        private static readonly DateTime[] _confusedUntil = new DateTime[EnemyAddresses.FloorSlots.Count];
        private static readonly int[]      _victim        = new int[EnemyAddresses.FloorSlots.Count];   // −1 = the player, −2 = none
        private static bool _guardLatched; private static DateTime _guardSince;

        public static void CurseOfBabelEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"curse of Babel: hold the guard {GuardChargeMs / 1000} s → the spear rises under the target for {SpearSeconds:F0} s; confusion within {ConfusionRadius:F0}");
            for (int s = 0; s < _victim.Length; s++) _victim[s] = -2;
            try
            {
                while (Player.CurrentCharacterNum() == Player.UngagaId && Player.Weapon.GetCurrentWeaponId() == Items.babelsspear && Player.InDungeonFloor())
                {
                    if (!Player.CheckDunIsPausedOrMenu())
                    {
                        Charge();
                        if (_up) DriveSpear();
                        DriveConfusion();
                    }
                    Thread.Sleep(TickMs);
                }
            }
            finally { End(); }
        }

        // ── the guard charge ──
        private static void Charge()
        {
            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
            int  mid = Memory.ReadInt(CCharacter.Base + CCharacter.MotionId);
            bool inPose = guarding && (mid == GuardLoopMotion || mid == GuardMoveMotion);
            if (!guarding) { _guardLatched = false; _guardSince = default; return; }   // released: a new hold can charge again
            if (!inPose || _guardLatched) return;
            if (_guardSince == default) { _guardSince = GameClock.Now; return; }
            if ((GameClock.Now - _guardSince).TotalMilliseconds < GuardChargeMs) return;
            _guardLatched = true;
            Player.FlashChargeComplete();
            Summon();
        }

        // ── the spear ──
        private static void Summon()
        {
            if (_up) TakeDown();
            int target = Target(out float x, out float y, out float ground);
            _sx = x; _sy = y; _ground = ground; _yaw = 0f;
            if (!BladeProp.Spawn(Scale, 0, pointDown: false, pointUp: true)) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no spear copy (slot or cave busy)"); return; }
            BladeProp.Place(_sx, RootHeight(0f), _sy, _yaw);
            BladeProp.Alpha(1f);
            _up = true; _summoned = GameClock.Now;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the spear rises at ({_sx:F0},{_sy:F0}) ground {_ground:F0}" + (target >= 0 ? $" under enemy slot {target}" : " ahead of Ungaga"));
            if (target >= 0) Strike(target);
            int confused = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                long p = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(p) - _sx, dy = Memory.ReadFloat(p + 8) - _sy;
                if (s != target && dx * dx + dy * dy > ConfusionRadius * ConfusionRadius) continue;
                Confuse(s); confused++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{confused} enem" + (confused == 1 ? "y" : "ies") + " confused");
        }

        /// <summary>The locked-on enemy, else the nearest live one within reach; its position and ground height out. −1 with
        /// a spot ahead of Ungaga when there is none.</summary>
        private static int Target(out float x, out float y, out float ground)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            int slot = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (slot < 0 || slot >= EnemyAddresses.FloorSlots.Count || !Enemies.IsLive(slot))
            {
                slot = -1; float best = NoLockReach * NoLockReach;
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                {
                    if (!Enemies.IsLive(s)) continue;
                    long p = EnemyAddresses.CharObjects.PosAddr(s);
                    float dx = Memory.ReadFloat(p) - px, dy = Memory.ReadFloat(p + 8) - py, d = dx * dx + dy * dy;
                    if (d < best) { best = d; slot = s; }
                }
            }
            if (slot >= 0)
            {
                long p = EnemyAddresses.CharObjects.PosAddr(slot);
                x = Memory.ReadFloat(p); ground = Memory.ReadFloat(p + 4); y = Memory.ReadFloat(p + 8);
                return slot;
            }
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            x = px + (float)Math.Sin(yaw) * AheadDistance; y = py + (float)Math.Cos(yaw) * AheadDistance; ground = ph;
            return -1;
        }

        /// <summary>The copy's root height for an emergence fraction <paramref name="t"/> (0 buried, 1 risen): the tip sits
        /// TipZ·Scale above the root.</summary>
        private static float RootHeight(float t) => _ground - TipZ * Scale + (-Buried + (Exposed + Buried) * t);

        private static void DriveSpear()
        {
            if (!BladeProp.Maintain()) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the copy did not maintain — down"); _up = false; return; }
            double age = (GameClock.Now - _summoned).TotalSeconds;
            if (age >= SpearSeconds) { TakeDown(); return; }
            float t = (float)Math.Min(1.0, age / EmergeSeconds);
            if (t >= 1f) _yaw += (float)(SpinDegPerSec * Math.PI / 180.0 * TickMs / 1000.0);
            BladeProp.Place(_sx, RootHeight(t), _sy, _yaw);
            BladeProp.Alpha(1f);
        }

        private static void TakeDown()
        {
            if (!_up) return;
            BladeProp.Despawn();
            _up = false;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the spear sinks away");
        }

        /// <summary>One player-hit sphere on the target's body at the weapon's attack, thrown away from the spear.</summary>
        private static void Strike(int slot)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int idx = CollisionPool.TakeFreeSlot(pool);
            if (idx < 0) return;
            BigBang.BodyCentre(slot, EnemyAddresses.FloorSlots.SlotAddr(slot, 0), out float cx, out float ch, out float cy, out float cr);
            int attack = Memory.ReadUShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset);
            byte[] e = CollisionPool.PlayerHitEntry(cx, ch, cy, Math.Max(StrikeRadius, cr), Math.Max(1, attack), 0);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x80, _sx); F(0x84, _ground); F(0x88, _sy);                 // the kick comes from the spear
            F(0x90, KickStrength); F(0x94, KickDecay);
            BitConverter.GetBytes(2).CopyTo(e, 0x98);                    // kick type 2: thrown away from it
            CollisionPool.Plant(pool, idx, e);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"struck enemy slot {slot} for {attack} (entry {idx})");
        }

        // ── confusion ──
        private static void Confuse(int slot)
        {
            _confusedUntil[slot] = GameClock.Now.AddSeconds(SpearSeconds);
            Reroll(slot);
        }

        private static bool Confused(int slot) => _confusedUntil[slot] != default && GameClock.Now < _confusedUntil[slot] && Enemies.IsLive(slot);

        /// <summary>A new target for a confused slot: the player, or another live enemy, even odds per candidate.</summary>
        private static void Reroll(int slot)
        {
            var others = new List<int>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++) if (s != slot && Enemies.IsLive(s)) others.Add(s);
            int pick = _rng.Next(others.Count + 1);
            _victim[slot] = pick == others.Count ? -1 : others[pick];
        }

        private static void DriveConfusion()
        {
            bool any = false;
            bool reroll = (GameClock.Now - _lastReroll).TotalSeconds >= RerollSeconds;
            if (reroll) _lastReroll = GameClock.Now;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (_confusedUntil[s] == default) continue;
                if (!Confused(s)) { Release(s); continue; }
                any = true;
                if (reroll || (_victim[s] >= 0 && !Enemies.IsLive(_victim[s]))) Reroll(s);
                if (Mirage.Armed)
                {
                    uint ptr = _victim[s] >= 0 ? (uint)(EnemyAddresses.CharObjects.PosAddr(_victim[s]) - Memory.Pcsx2Base) : StbExternCmd.PlayerPosGuest;
                    if (Memory.ReadUInt(CodeCaves.PtrAddr(s)) != ptr) Memory.WriteUInt(CodeCaves.PtrAddr(s), ptr);
                }
                Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(s) + CCharacter.CharaTint, TintR, TintG, TintB);
            }
            if (any) FriendlyFire();
            OwnsTable = any && Mirage.Armed;
        }

        /// <summary>A confused slot back to normal: its pointer on the player, its tint off.</summary>
        private static void Release(int slot)
        {
            _confusedUntil[slot] = default; _victim[slot] = -2;
            if (Mirage.Armed) Memory.WriteUInt(CodeCaves.PtrAddr(slot), StbExternCmd.PlayerPosGuest);
            Memory.WriteVec3(EnemyAddresses.CharObjects.CharAddr(slot) + CCharacter.CharaTint, 0f, 0f, 0f);
        }

        /// <summary>Every live attack entry a confused enemy planted gets to hurt enemies too.</summary>
        private static void FriendlyFire()
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            byte[] active = Memory.ReadBytesBatch(pool + CollisionPool.ActiveOff, CollisionPool.Entries * 4);
            if (active == null) return;
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (BitConverter.ToInt32(active, i * 4) == 0) continue;
                long e = pool + i * CollisionPool.Stride;
                int owner = Memory.ReadInt(e + CollisionPool.Owner);
                if (owner < 200 || (owner - 200) % 5 != 0) continue;
                int slot = (owner - 200) / 5;
                if (slot >= EnemyAddresses.FloorSlots.Count || !Confused(slot)) continue;
                int mask = Memory.ReadInt(e + CollisionPool.Mask);
                if ((mask & 2) == 0) Memory.WriteInt(e + CollisionPool.Mask, mask | 2);
            }
        }

        private static void End()
        {
            TakeDown();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++) if (_confusedUntil[s] != default) Release(s);
            OwnsTable = false;
            _guardLatched = false; _guardSince = default;
        }
    }
}
