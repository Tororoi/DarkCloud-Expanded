using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public class CustomGoroEffects
    {

        private static Random random = new Random();

        // ── Frozen Tuna "Cold Storage" ─────────────────────────────────────────────────────
        private static readonly HashSet<int> FrozenTunaIceEnemies = new()
        {
            EnemySpecies.Blizzard.Id,   // 65
            EnemySpecies.Sam.Id,        // 85
            EnemySpecies.GemronIce.Id,  // 312
        };

        /// <summary>Who is holding the Frozen Tuna (or its sphere): character id + the raw HP/status
        /// addresses the driver heals/freezes. Mirrors the CurseAddrs pattern in CustomToanEffects.</summary>
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

        /// <summary>
        /// Ability Name: Cold Storage (Frozen Tuna) — per-tick driver.
        /// WHP lost builds a healing pool (<see cref="FrozenTunaHealPerWhp"/> HP per WHP). When the wielder
        /// takes damage the pool drains at <see cref="FrozenTunaHealPerSec"/> HP/s (time-based, so any tick
        /// rate works); healing pauses at max HP, and the pool resets on weapon repair. On hit, a
        /// <see cref="FrozenTunaProcPercent"/>% chance to stop all non-ice enemies — at the price of freezing
        /// the wielder too. Ice enemies (Blizzard, Sam, Ice Gemron) are immune. Called with
        /// <paramref name="active"/>=false the state resets, so a sphere swap starts clean.
        /// </summary>
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

            float whp    = ReusableFunctions.GetCurrentEquippedWhp(w.CharId, weaponSlot);
            ushort hp    = Memory.ReadUShort(w.Hp);
            ushort maxHp = Memory.ReadUShort(w.MaxHp);
            int[] enemyHp = ReusableFunctions.GetEnemiesHp();
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
            if (st.PrevEnemyHp != null && ReusableFunctions.GetDamageSourceCharacterID() == w.CharId)
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
                    ReusableFunctions.ClearRecentDamageAndDamageSource();
                }
            }

            st.PrevWhp = whp;
            st.PrevHp = hp;
            st.PrevEnemyHp = enemyHp;
        }

        /// <summary>Goro's own Frozen Tuna weapon: loops the shared driver while it's equipped.</summary>
        public static void FrozenTunaEffect()
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

        // ── Inferno ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Inferno effect: Increase attack power depending on health and thirst
        /// </summary>
        public static void InfernoEffect()
        {
            float goroMaxHP = Player.Goro.GetMaxHp();
            float goroCurrentHP = Player.Goro.GetHp();

            float hpPercentage = 100 - (goroCurrentHP / goroMaxHP * 100);

            float goroMaxThirst = Player.Goro.GetMaxThirst();
            float goroCurrentThirst = Player.Goro.GetThirst();

            float thirstPercentage = 100 - (goroCurrentThirst / goroMaxThirst * 100);

            ushort currentBaseAttack = Player.Weapon.GetCurrentWeaponAttack();

            ushort attachmentsAttack = 0;

            for (int i = 0; i < 4; i++)
            {
                attachmentsAttack += Memory.ReadUShort(0x21EA75C0 + (i * 0x20));
            }

            ushort currentTotalAttack = (ushort)(currentBaseAttack + attachmentsAttack);

            if (currentTotalAttack > 350)
            {
                currentTotalAttack = 350;
            }

            ushort hpAttackBoost = (ushort)((currentTotalAttack / 100) * hpPercentage);

            ushort thirstAttackBoost = (ushort)((currentTotalAttack / 100) * (thirstPercentage / 2));

            Memory.WriteUShort(0x21EA7594, (ushort)(currentTotalAttack + hpAttackBoost + thirstAttackBoost));
        }

        // ── Tall Hammer ────────────────────────────────────────────────────────────────────
        private const float TallHammerShrinkStep = 0.1f;   // scale lost per hit (matches the old net -0.1)
        private const float TallHammerMinScale   = 0.3f;   // don't shrink past 30% of original

        /// <summary>Per-tick enemy-HP snapshot for detecting fresh hits across driver calls.</summary>
        internal sealed class TallHammerState { public int[] PrevHp; }

        /// <summary>Tall Hammer: shrinks enemies the wielder hits. Compares the enemy-HP snapshot to last
        /// tick's (<paramref name="st"/>) and, when the damage came from <paramref name="wielderId"/>, shrinks
        /// each freshly-hit enemy by one step (clamped to <see cref="TallHammerMinScale"/>). Character-agnostic
        /// apart from the wielder id, so Goro's own weapon and Super Steve both reuse it.</summary>
        internal static void TallHammerDrive(bool active, int wielderId, TallHammerState st)
        {
            int[] cur = ReusableFunctions.GetEnemiesHp();
            if (st.PrevHp != null && active && ReusableFunctions.GetDamageSourceCharacterID() == wielderId)
            {
                foreach (int id in ReusableFunctions.GetEnemiesHitIds(st.PrevHp, cur))
                    ShrinkEnemy(id);
            }
            st.PrevHp = cur;
        }

        /// <summary>Goro's own Tall Hammer weapon: loops the shared driver while it's equipped.</summary>
        public static void TallHammerEffect()
        {
            var st = new TallHammerState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.tallhammer && Player.InDungeonFloor())
            {
                TallHammerDrive(true, Player.GoroId, st);
                Thread.Sleep(50);
            }
        }

        /// <summary>Shrink one enemy's X/Y/Z scale by <see cref="TallHammerShrinkStep"/>, but only while at
        /// least one axis is still within [<see cref="TallHammerMinScale"/>, 1] of its original size.</summary>
        private static void ShrinkEnemy(int id)
        {
            int off = MiniBoss.scaleOffset * id;
            float w = Memory.ReadFloat(MiniBoss.enemyZeroWidth  + off);
            float h = Memory.ReadFloat(MiniBoss.enemyZeroHeight + off);
            float d = Memory.ReadFloat(MiniBoss.enemyZeroDepth  + off);
            if ((w >= TallHammerMinScale && w <= 1f) ||
                (h >= TallHammerMinScale && h <= 1f) ||
                (d >= TallHammerMinScale && d <= 1f))
            {
                Memory.WriteFloat(MiniBoss.enemyZeroWidth  + off, w - TallHammerShrinkStep);
                Memory.WriteFloat(MiniBoss.enemyZeroHeight + off, h - TallHammerShrinkStep);
                Memory.WriteFloat(MiniBoss.enemyZeroDepth  + off, d - TallHammerShrinkStep);
            }
        }

    }
}


