using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomUngagaEffects
    {

        private static Random random = new Random();

        // ── Babel's Spear ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Babel Spear effect: Chance on hit to apply stop to all enemies.
        /// </summary>
        public static void BabelSpearEffect()
        {
            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.UngagaId;

            if (hasHit)
            {
                int procChance = random.Next(100);

                //Chance to apply stop (6%)
                if (procChance < 6)
                {
                    for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                        if (Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) == 2)
                            Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.FreezeTimer), 300); //Stop duration (300 = 5 seconds)
                }
            }

            //Reset the damage and source values
            ReusableFunctions.ClearRecentDamageAndDamageSource();
        }

        // ── Cactus "Absorb" ────────────────────────────────────────────────────────────────
        private static readonly HashSet<int> CactusImmuneNameTags = new()
        {
            EnemySpecies.MasterJacket.Id,    // 1
            EnemySpecies.SkeletonSoldier.Id, // 3
            EnemySpecies.Statue.Id,          // 5
            EnemySpecies.PiratesChariot.Id,  // 25
            EnemySpecies.Golem.Id,           // 30
            EnemySpecies.MrBlare.Id,         // 31
            EnemySpecies.Dune.Id,            // 32
            EnemySpecies.Titan.Id,           // 33
            EnemySpecies.Arthur.Id,          // 40
            EnemySpecies.LivingArmor.Id,     // 55
            EnemySpecies.SteelGiant.Id,      // 64
            EnemySpecies.Billy.Id,           // 69
            EnemySpecies.Vulcan.Id,          // 70
            EnemySpecies.Rockanoff.Id,       // 77
            EnemySpecies.Gol.Id,             // 90
            EnemySpecies.Sil.Id,             // 91
            EnemySpecies.StatueDog.Id,       // 303
            EnemySpecies.Gacious.Id,         // 317
            EnemySpecies.SilverGear.Id,      // 318
            EnemySpecies.HornHead.Id,        // 319
        };

        /// <summary>Per-caller Cactus state: last tick's enemy-HP snapshot for fresh-hit detection.</summary>
        internal sealed class CactusState { public int[] PrevHp; }

        /// <summary>
        /// Ability Name: Absorb (Cactus) — per-tick driver.
        /// On hit, restore the wielder's thirst scaled by damage dealt (100 damage = 10.0 thirst units = 1
        /// visible water drop). Rock, metal, and undead types are immune. Wielder-agnostic: the character id
        /// picks whose hits count and the raw thirst/thirstMax float addresses pick whose gauge fills, so
        /// Ungaga's own Cactus and Super Steve's inherited copy both reuse it.
        /// </summary>
        internal static void CactusDrive(bool active, int wielderId, int thirstAddr, int thirstMaxAddr, CactusState st)
        {
            int[] cur = ReusableFunctions.GetEnemiesHp();
            if (st.PrevHp != null && active && ReusableFunctions.GetDamageSourceCharacterID() == wielderId)
            {
                for (int i = 0; i < EnemyAddresses.FloorSlots.Count && i < st.PrevHp.Length && i < cur.Length; i++)
                {
                    if (st.PrevHp[i] <= 0 || cur[i] >= st.PrevHp[i])
                        continue;

                    int enemySpeciesId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    if (CactusImmuneNameTags.Contains(enemySpeciesId))
                        continue;

                    float curThirst = Memory.ReadFloat(thirstAddr);
                    float maxThirst = Memory.ReadFloat(thirstMaxAddr);
                    if (maxThirst > 0 && curThirst >= maxThirst)
                        break;

                    float gain = (st.PrevHp[i] - cur[i]) / 10.0f;
                    float newThirst = (maxThirst > 0)
                        ? Math.Min(curThirst + gain, maxThirst)
                        : curThirst + gain;
                    Memory.WriteFloat(thirstAddr, newThirst);
                    break;
                }
                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
            st.PrevHp = cur;
        }

        /// <summary>Ungaga's own Cactus weapon: loops the shared driver while it's equipped.</summary>
        public static void CactusEffect()
        {
            var st = new CactusState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.cactus && Player.InDungeonFloor())
            {
                CactusDrive(true, Player.UngagaId, Player.Ungaga.thirst, Player.Ungaga.thirstMax, st);
                Thread.Sleep(50);
            }
        }

        // ── Hercules' Wrath ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Hercules Wrath effect: Chance on getting hit to gain Stamina.
        /// </summary>
        public static void HerculesWrathEffect()
        {
            //Check Ungaga's HP
            ushort formerHP = Memory.ReadUShort(Player.Ungaga.hp);

            Thread.Sleep(100);

            //Re-check Ungaga's HP
            ushort currentHP = Memory.ReadUShort(Player.Ungaga.hp);

            if (currentHP < formerHP)
            {
                //Declare the scale for the chance to base on (0 - 100)
                int procChance = random.Next(100);

                //Check for the chance to take effect (30 = 30%)
                if (procChance < 30)
                {
                    //Give the Stamina effect for 30 seconds (1800 = 30 sec)
                    Player.Ungaga.SetStatus("stamina", 1800);
                }
            }
        }

    }
}


