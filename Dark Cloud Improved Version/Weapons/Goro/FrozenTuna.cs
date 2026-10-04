using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Frozen Tuna — "Cold Storage". WHP lost builds a healing pool (<see cref="FrozenTunaHealPerWhp"/> HP per WHP); when the
    /// wielder takes damage the pool drains into his HP at <see cref="FrozenTunaHealPerSec"/> HP/s (time-based, so any tick rate
    /// works), pausing at max HP and resetting on a weapon repair. On hit, a <see cref="FrozenTunaProcPercent"/>% chance to stop
    /// every non-ice enemy on the floor (<see cref="FrozenTunaEnemyFreeze"/> frames) at the price of freezing the wielder too
    /// (<see cref="FrozenTunaSelfFreeze"/>); the ice species (Blizzard, Sam, Ice Gemron) are immune. The driver
    /// (<see cref="FrozenTunaDrive"/>) takes whose HP and status it works on (<see cref="FrozenTunaWielder"/>) and its own state
    /// (<see cref="FrozenTunaState"/>), so Goro's own weapon and Super Steve's inherited copy never share a pool.</summary>
    internal static class FrozenTuna
    {
        private static Random random = new Random();

        private static readonly HashSet<int> FrozenTunaIceEnemies = new()
        {
            EnemySpecies.Blizzard.Id,   // 65
            EnemySpecies.Sam.Id,        // 85
            EnemySpecies.GemronIce.Id,  // 312
        };

        /// <summary>Who is holding the Frozen Tuna (or its sphere): character id + the raw HP/status
        /// addresses the driver heals/freezes. Mirrors the CurseAddrs pattern in Weapons/Toan/Shared/Curse.cs.</summary>
        internal sealed class FrozenTunaWielder
        {
            public readonly int CharId; public readonly int Hp, MaxHp, Status, StatusTimer;
            public FrozenTunaWielder(int charId, int hp, int maxHp, int status, int statusTimer)
            { CharId = charId; Hp = hp; MaxHp = maxHp; Status = status; StatusTimer = statusTimer; }
        }

        /// <summary>Per-wielder Frozen Tuna state (healing pool, snapshots, self-freeze countdown) so Goro's
        /// own weapon and Super Steve's inherited copy never share (or fight over) a pool.</summary>
        internal sealed class FrozenTunaState
        {
            public float StoredHealing;     // HP banked from WHP losses
            public float HealFraction;      // sub-integer carry for the 2 HP/s drain
            public bool  HealActive;
            public float  PrevWhp = -1f;    // last tick's WHP (-1 = unseeded)
            public int    PrevHp  = -1;     // last tick's wielder HP (-1 = unseeded)
            public int[]  PrevEnemyHp;      // last tick's enemy-HP snapshot
            public DateTime LastTick = DateTime.MinValue;
            public int SelfFreezeStartTick = -1;   // ingameTimer tick the self-freeze began (-1 = none)
        }

        private const float FrozenTunaHealPerWhp   = 2f;    // pool gained per 1 WHP lost
        private const float FrozenTunaHealPerSec   = 2f;    // pool drain rate (1 HP / 0.5s)
        private const int   FrozenTunaProcPercent  = 5;     // on-hit stop-proc chance
        private const ushort FrozenTunaEnemyFreeze = 300;   // enemy FreezeTimer written by the proc
        private const ushort FrozenTunaSelfFreeze  = 180;   // wielder freeze duration (ticks @60fps)

        /// <summary>Cold Storage's per-tick driver (the class summary). Called with <paramref name="active"/>=false the state
        /// resets, so a sphere swap starts clean; the self-freeze countdown runs even then, so an in-flight freeze always clears.</summary>
        internal static void FrozenTunaDrive(bool active, FrozenTunaWielder w, int weaponSlot, FrozenTunaState st)
        {
            // Self-freeze countdown runs even while "inactive" so an in-flight freeze always clears.
            if (st.SelfFreezeStartTick >= 0 &&
                Memory.ReadInt(Addresses.ingameTimer) - st.SelfFreezeStartTick >= FrozenTunaSelfFreeze)
            {
                if (Memory.ReadUShort(w.Status) == 4)   // still frozen (and nothing else) → clear it
                {
                    Memory.WriteUShort(w.Status, 0);
                    Memory.WriteUShort(w.StatusTimer, 0);
                }
                st.SelfFreezeStartTick = -1;
            }

            if (!active)
            {
                st.StoredHealing = 0f; st.HealFraction = 0f; st.HealActive = false;
                st.PrevWhp = -1f; st.PrevHp = -1; st.PrevEnemyHp = null;
                return;
            }

            float whp    = WeaponRecord.ReadWhp(w.CharId, weaponSlot);
            ushort hp    = Memory.ReadUShort(w.Hp);
            ushort maxHp = Memory.ReadUShort(w.MaxHp);
            int[] enemyHp = EnemyQueries.GetEnemiesHp();
            DateTime now = DateTime.UtcNow;
            double elapsed = st.LastTick == DateTime.MinValue ? 0 : (now - st.LastTick).TotalSeconds;
            st.LastTick = now;

            if (st.PrevWhp >= 0f)
            {
                // WHP lost → bank into the healing pool; WHP repaired → reset the pool.
                if (whp < st.PrevWhp) st.StoredHealing += (st.PrevWhp - whp) * FrozenTunaHealPerWhp;
                else if (whp > st.PrevWhp) { st.StoredHealing = 0f; st.HealFraction = 0f; st.HealActive = false; }
            }

            // Wielder took damage → start draining the pool (if it has anything banked).
            if (st.PrevHp >= 0 && hp < st.PrevHp && st.StoredHealing > 0f)
                st.HealActive = true;

            // Drain the pool at FrozenTunaHealPerSec while below max HP.
            if (st.HealActive && st.StoredHealing > 0f && hp > 0 && hp < maxHp)
            {
                float drain = Math.Min((float)(FrozenTunaHealPerSec * elapsed), st.StoredHealing);
                st.StoredHealing -= drain;
                st.HealFraction  += drain;
                int intHeal = (int)st.HealFraction;
                if (intHeal > 0)
                {
                    Memory.WriteUShort(w.Hp, (ushort)Math.Min(hp + intHeal, maxHp));
                    st.HealFraction -= intHeal;
                }
            }
            if (st.StoredHealing <= 0f) st.HealActive = false;

            // On-hit stop proc: freeze every active non-ice enemy — and the wielder pays the price too.
            if (st.PrevEnemyHp != null && EnemyQueries.GetDamageSourceCharacterID() == w.CharId)
            {
                bool hitDetected = false;
                for (int i = 0; i < st.PrevEnemyHp.Length && i < enemyHp.Length; i++)
                {
                    if (st.PrevEnemyHp[i] > 0 && enemyHp[i] < st.PrevEnemyHp[i]) { hitDetected = true; break; }
                }

                if (hitDetected)
                {
                    if (random.Next(100) < FrozenTunaProcPercent)
                    {
                        for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                        {
                            if (Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) == 2 &&
                                !FrozenTunaIceEnemies.Contains(Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId))))
                            {
                                Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FreezeTimer), FrozenTunaEnemyFreeze);
                            }
                        }
                        Memory.WriteUShort(w.Status, 4);   // freeze the wielder (status bit 4 = freeze)
                        Memory.WriteUShort(w.StatusTimer, FrozenTunaSelfFreeze);
                        st.SelfFreezeStartTick = Memory.ReadInt(Addresses.ingameTimer);
                    }
                    EnemyQueries.ClearRecentDamageAndDamageSource();
                }
            }

            st.PrevWhp = whp;
            st.PrevHp = hp;
            st.PrevEnemyHp = enemyHp;
        }

        /// <summary>Goro's own Frozen Tuna: the driver every 50 ms while it is equipped on a floor; the pool reset when it goes.</summary>
        public static void ColdStorageEffect()
        {
            var wielder = new FrozenTunaWielder(Player.GoroId, Player.Goro.hp, Player.Goro.maxHP,
                                                Player.Goro.status, Player.Goro.statusTimer);
            var st = new FrozenTunaState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.frozentuna && Player.InDungeonFloor())
            {
                FrozenTunaDrive(true, wielder, Player.Goro.GetWeaponSlot(), st);
                Thread.Sleep(50);
            }
            FrozenTunaDrive(false, wielder, 0, st);   // unequipped/left floor: reset the pool
        }
    }
}
