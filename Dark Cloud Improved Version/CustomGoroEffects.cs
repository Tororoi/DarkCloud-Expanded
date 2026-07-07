using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Ability Name: Cold Storage (Frozen Tuna)
        /// Frozen Tuna: WHP lost builds a healing pool. When Goro takes damage the pool
        /// drains at 1 HP per 0.5 seconds. Healing pauses if HP reaches max; the pool is
        /// preserved until the next hit. Pool resets on weapon repair or weapon switch.
        /// On hit, 5% chance to stop all non-ice enemies and freeze Goro for 3 seconds.
        /// Ice enemies (Blizzard, Sam, Ice Gemron) are immune to the stop proc.
        /// </summary>
        public static void FrozenTunaEffect()
        {
            float storedHealing = 0f; // HP banked from WHP losses
            float healFraction  = 0f; // sub-integer carry for 1 HP/500ms drain
            bool  healActive    = false;

            while (Player.Weapon.GetCurrentWeaponId() == Items.frozentuna && Player.InDungeonFloor())
            {
                int[] formerHp      = ReusableFunctions.GetEnemiesHp();
                float whpBefore     = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());
                ushort goroHpBefore = Player.Goro.GetHp();

                Thread.Sleep(50);

                int[] currentHp  = ReusableFunctions.GetEnemiesHp();
                float whpAfter   = ReusableFunctions.GetCurrentEquippedWhp(Player.GoroId, Player.Goro.GetWeaponSlot());
                ushort goroHp    = Player.Goro.GetHp();
                ushort goroMaxHp = Player.Goro.GetMaxHp();

                // WHP lost → bank into healing pool (2 HP per 1 WHP lost)
                if (whpAfter < whpBefore)
                    storedHealing += (whpBefore - whpAfter) * 2f;

                // WHP repaired → reset everything
                if (whpAfter > whpBefore)
                {
                    storedHealing = 0f;
                    healFraction  = 0f;
                    healActive    = false;
                }

                // Goro took damage → activate pool drain if pool has anything
                if (goroHp < goroHpBefore && storedHealing > 0f)
                    healActive = true;

                // Drain pool at 1 HP per 500ms (0.1 HP per 50ms tick) while below max
                if (healActive && storedHealing > 0f && goroHp < goroMaxHp)
                {
                    float drain    = Math.Min(0.1f, storedHealing);
                    storedHealing -= drain;
                    healFraction  += drain;

                    int intHeal = (int)healFraction;
                    if (intHeal > 0)
                    {
                        Player.Goro.SetHp((ushort)Math.Min(goroHp + intHeal, goroMaxHp));
                        healFraction -= intHeal;
                    }
                }

                if (storedHealing <= 0f)
                    healActive = false;

                // On-hit 5% stop proc: all non-ice enemies stopped; Goro frozen too
                if (ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId)
                {
                    bool hitDetected = false;
                    for (int i = 0; i < 15; i++)
                    {
                        if (formerHp[i] > 0 && currentHp[i] < formerHp[i])
                        {
                            hitDetected = true;
                            break;
                        }
                    }

                    if (hitDetected && random.Next(100) < 5)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            if (Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) == 2 &&
                                !FrozenTunaIceEnemies.Contains(Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId))))
                            {
                                Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.FreezeTimer), 300);
                            }
                        }
                        Player.Goro.SetStatus("freeze", 180); // 3 seconds at 60fps
                        int freezeStartTick = Memory.ReadInt(Addresses.ingameTimer);
                        Task.Run(() =>
                        {
                            while (Memory.ReadInt(Addresses.ingameTimer) - freezeStartTick < 180)
                                Thread.Sleep(50);
                            if (Player.Goro.GetStatus() == 4)
                            {
                                Memory.WriteUShort(Player.Goro.status, 0);
                                Memory.WriteUShort(Player.Goro.statusTimer, 0);
                            }
                        });
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
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
        /// <summary>
        /// Triggers Tall Hammer effect: Reduces enemies size on hit
        /// </summary>
        public static void TallHammerEffect()
        {
            //Offset between the enemy's dimension addresses
            int scaleOffset = MiniBoss.scaleOffset;

            //Save every enemy's HP on the current floor
            int[] formerEnemyHpList = ReusableFunctions.GetEnemiesHp();

            Thread.Sleep(250);

            //Re-save every enemy's HP on the current floor
            int[] currentEnemyHpList = ReusableFunctions.GetEnemiesHp();

            int hit = ReusableFunctions.GetRecentDamageDealtByPlayer();

            bool hasHit = hit > -1 && ReusableFunctions.GetDamageSourceCharacterID() == Player.GoroId;

            if (hasHit)
            {
                //Store the damaged enemies ID onto a list
                List<int> enemyIds = ReusableFunctions.GetEnemiesHitIds(formerEnemyHpList, currentEnemyHpList);

                //Run through the enemies hit
                foreach (int id in enemyIds)
                {
                    //Declare the enemy dimensions based on the enemy that got hit
                    float enemyZeroWidth = Memory.ReadFloat(0x21E18530 + (scaleOffset * id));
                    float enemyZeroHeight = Memory.ReadFloat(0x21E18534 + (scaleOffset * id));
                    float enemyZeroDepth = Memory.ReadFloat(0x21E18538 + (scaleOffset * id));

                    //Set an initial acceleration value
                    float i = 0.15f;

                    //Set a counter for how many times to change the enemy's dimensions (this acts as a duration variable)
                    int counter = 0;

                    //Instructions will run for 1000 times (arbitrary number) and only while the enemy's dimensions are between 30% - 100% of their original size
                    while (counter < 1000 && ((enemyZeroWidth >= 0.3f && enemyZeroWidth <= 1f) || (enemyZeroHeight >= 0.3f && enemyZeroHeight <= 1f) || (enemyZeroDepth >= 0.3f && enemyZeroDepth <= 1f)))
                    {
                        //Change each of the enemy axis dimensions (X,Y and Z) based on the offset from the original Enemy 0 address
                        Memory.WriteFloat(MiniBoss.enemyZeroWidth + (scaleOffset * id), enemyZeroWidth - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroHeight + (scaleOffset * id), enemyZeroHeight - (i * 0.0001f));
                        Memory.WriteFloat(MiniBoss.enemyZeroDepth + (scaleOffset * id), enemyZeroDepth - (i * 0.0001f));
                        i++;
                        counter++;
                    }
                }

                ReusableFunctions.ClearRecentDamageAndDamageSource();
            }
        }

    }
}


