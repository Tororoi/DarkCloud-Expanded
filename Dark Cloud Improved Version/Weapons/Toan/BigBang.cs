using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang — every enemy Toan hits triggers an explosion.</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        private const float BigBangScale          = 1.5f;   // explosion size (item Bomb = 1.0; >1 adds the shockwave ring)

        private const float BigBangRadiusPerScale = 20f;    // AoE radius = scale × this (matches the native SetBombEffect damage radius)

        private const float BigBangDamageFraction = 0.5f;   // splash HP damage = this × Toan's weapon attack

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Every enemy Toan HITS detonates like a Bomber Head: the item-Bomb explosion (five staggered
        /// fireball puffs + ground shockwave) erupts at the struck enemy and splashes damage to its
        /// neighbours.
        ///
        /// The visual is fabricated by replicating the engine's own SetBomb data writes into a free slot of
        /// the always-resident item-Bomb effect pool (see <see cref="BombEffect"/> — pure
        /// memory writes, no engine call). The AoE damage is a direct HP write with Toan credited.
        /// All direct hits are collected BEFORE any splash is applied (and the splash lands in the next
        /// tick's baseline), so only real weapon hits detonate — the explosion's own splash never
        /// chain-reacts the room.
        /// </summary>
        public static void DetonateEffect()
        {
            int n = EnemyAddresses.FloorSlots.Count;
            int[] prev = null;
            byte floor = 0xFF;
            var hits = new List<int>();

            while (Player.Weapon.GetCurrentWeaponId() == Items.bigbang && Player.InDungeonFloor())
            {
                Thread.Sleep(50);
                byte f = Memory.ReadByte(Addresses.checkFloor);
                if (f != floor) { floor = f; prev = null; }

                int[] cur = ReusableFunctions.GetEnemiesHp();
                if (prev != null && !Player.CheckDunIsPaused())
                {
                    // Collect the tick's direct hits first — DetonateAt mutates cur (its splash), and
                    // pre-collecting keeps that splash from registering as fresh hits of its own.
                    hits.Clear();
                    for (int h = 0; h < n; h++)
                    {
                        if (prev[h] <= 0 || cur[h] >= prev[h]) continue;   // fresh HP drop this tick
                        if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.KillerCharId)) != Player.ToanId)
                            continue;
                        hits.Add(h);
                    }
                    foreach (int h in hits)
                        DetonateAt(h, cur);
                }
                prev = cur;
            }
        }

        /// <summary>Detonate at struck enemy <paramref name="h"/>'s position: fabricate the item-Bomb
        /// explosion + shockwave there (native SetBomb/SetBombEffect writes, see
        /// <see cref="BombEffect"/>), then splash <see cref="BigBangDamageFraction"/> ×
        /// Toan's attack to OTHER live enemies within the blast radius, credited to Toan.</summary>
        private static void DetonateAt(int h, int[] cur)
        {
            long ePos = EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.LocationX);
            float x = Memory.ReadFloat(ePos), ht = Memory.ReadFloat(ePos + 4), z = Memory.ReadFloat(ePos + 8);

            // The explosion visual: replicate SetBomb into a free pool slot (all five active flags clear).
            long poolNative = Memory.ReadGuestPtr(BombEffect.PoolPtr);
            if (Memory.IsValidGuest(poolNative))
            {
                long pool = Memory.ToMmu(poolNative);
                for (int s = 0; s < BombEffect.SlotCount; s++)
                {
                    long slot = pool + s * BombEffect.SlotStride;
                    bool busy = false;
                    for (int i = 0; i < BombEffect.SubPuffs && !busy; i++)
                        busy = Memory.ReadInt(slot + BombEffect.ActiveOffset + i * 4) != 0;
                    if (busy) continue;

                    for (int i = 0; i < BombEffect.SubPuffs; i++)
                    {
                        long pos = slot + BombEffect.PosOffset + i * 0x10;
                        Memory.WriteFloat(pos, x); Memory.WriteFloat(pos + 4, ht);
                        Memory.WriteFloat(pos + 8, z); Memory.WriteFloat(pos + 12, 1f);
                        Memory.WriteInt(slot + BombEffect.StateOffset + i * 4, 0);
                        Memory.WriteInt(slot + BombEffect.TimerOffset + i * 4, i * -3);
                        Memory.WriteFloat(slot + BombEffect.SizeAOffset + i * 4, 20f);
                        Memory.WriteFloat(slot + BombEffect.SizeBOffset + i * 4, 128f);
                        Memory.WriteInt(slot + BombEffect.ActiveOffset + i * 4, 1);
                    }
                    Memory.WriteFloat(slot + BombEffect.ScaleOffset, BigBangScale);
                    Memory.WriteInt(slot + BombEffect.StateOffset, 2);
                    Memory.WriteInt(slot + BombEffect.StateOffset + 4, 1);
                    break;
                }

                // The ground shockwave ring (the native path adds it when scale > 1).
                long swNative = Memory.ReadGuestPtr(BombEffect.ShockWavePtr);
                if (BigBangScale > 1f && Memory.IsValidGuest(swNative))
                {
                    long sw = Memory.ToMmu(swNative);
                    Memory.WriteFloat(sw, x); Memory.WriteFloat(sw + 4, ht);
                    Memory.WriteFloat(sw + 8, z); Memory.WriteFloat(sw + 12, 1f);
                    Memory.WriteFloat(sw + 0x0C, 1f);
                    Memory.WriteFloat(sw + 0x10, BigBangScale * 30f);
                    Memory.WriteFloat(sw + 0x14, BigBangScale * 30f);
                    Memory.WriteInt(sw + 0x18, 0);
                    Memory.WriteFloat(sw + 0x1C, BigBangScale * 15f);
                    Memory.WriteInt(sw + 0x20, 0);
                    Memory.WriteInt(sw + 0x24, 0);
                    Memory.WriteInt(sw + 0x28, 1);
                }
            }

            // The AoE damage: splash a share of Toan's attack to every live enemy in the blast radius,
            // Toan-credited — a splash kill is a fresh Toan kill next tick, so explosions chain.
            int dmg = (int)(Player.Weapon.GetCurrentWeaponAttack() * BigBangDamageFraction);
            float radius = BigBangScale * BigBangRadiusPerScale;
            float r2 = radius * radius;
            for (int t = 0; t < cur.Length && dmg > 0; t++)
            {
                if (t == h || cur[t] <= 0) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(t, EnemySlotOffsets.RenderStatus)) < 1) continue;
                float dx = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(t, EnemySlotOffsets.LocationX)) - x;
                float dz = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(t, EnemySlotOffsets.LocationY)) - z;
                if (dx * dx + dz * dz > r2) continue;
                int nhp = Math.Max(0, cur[t] - dmg);
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(t, EnemySlotOffsets.KillerCharId), Player.ToanId);
                Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(t, EnemySlotOffsets.Hp), nhp);
                cur[t] = nhp;
            }
        }
    }
}
