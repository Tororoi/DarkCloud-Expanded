using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomRubyEffects
    {

        private static Random random = new Random();

        // ── Mobius Ring ────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Mobius Ring effect: Increases damage output the longer you charge an attack
        /// </summary>
        public static void MobiusRingEffect()
        {
            //Declare inputs
            string message;
            int height;
            int width;
            ushort sleep = 1500;
            int chargeGlowTimer = 0x21DC449E;
            ushort chargeTimer = 0;

            //Check these addresses which tells us if Ruby is charging an attack
            if (Player.Ruby.IsChargingAttack())
            {
                //Initialize the damage
                int damage = Player.Weapon.GetCurrentWeaponAttack() + Player.Weapon.GetCurrentWeaponMagic();

                //The energy ball only starts growing once the charge is fully built (first flash); from
                //there its size tracks the Mobius damage multiplier (M = damage / baseDamage). baseDamage
                //is the un-boosted attack+magic, floored at 1 to avoid divide-by-zero.
                int baseDamage = System.Math.Max(1, damage);
                bool fullyCharged = false;
                float ballScale = 1.0f;

                while (Player.Ruby.IsChargingAttack())
                {
                    //Check if the game is paused during the charge
                    if (Player.CheckDunIsPaused())
                    {
                        ReusableFunctions.AwaitUnpause(1);
                    }
                    //If the damage increase reaches the set max value, stop increasing it further
                    if (damage >= ushort.MaxValue)
                    {
                        damage = ushort.MaxValue;
                    }
                    else damage += damage / 2;

                    //Set messages to display onscreen
                    if (damage > 9000)
                    {
                        message = "Total damage is over 9000";
                        height = 1;
                        width = message.Length;
                    }
                    else
                    {
                        message = "Total damage " + damage;
                        height = 1;
                        width = message.Length;
                    }

                    //Keep looping until chargeGlowTimer reaches the value 17008 or the player stops charging
                    while (Memory.ReadUShort(chargeGlowTimer) < 17008 && Player.Ruby.IsChargingAttack())
                    {
                        if (Player.CheckDunIsPaused())
                        {
                            ReusableFunctions.AwaitUnpause(1);
                        }
                        Thread.Sleep(100);
                        continue;
                    }

                    //Save the value of the timer
                    chargeTimer = Memory.ReadUShort(chargeGlowTimer);

                    //Check if the timer hit the value we are looking for. This value makes Ruby flash
                    if (chargeTimer == 17008)
                    {
                        //The flash marks the charge as fully built — from here the ball may grow.
                        fullyCharged = true;

                        //Display current damage
                        Dayuppy.DisplayMessage(message, height, width, sleep + 500);

                        Thread.Sleep(sleep);

                        //Reset Flash
                        Memory.WriteUShort(chargeGlowTimer, 0);
                    }

                    //Once fully charged, grow the energy ball in step with the damage multiplier the Mobius
                    //ramp has reached (scale = 1 + (M-1)*perMultiple, clamped). Re-applied every tick so the
                    //effect stays sized as the charge is held. See Weapons.SetRubyBallScale.
                    if (fullyCharged)
                    {
                        float m = (float)damage / baseDamage;
                        ballScale = 1.0f + (m - 1.0f) * WeaponCollision.RubyBallGrowthPerMultiple;
                        if (ballScale > WeaponCollision.RubyBallMaxScale) ballScale = WeaponCollision.RubyBallMaxScale;
                        if (ballScale < 1.0f) ballScale = 1.0f;
                        Weapons.SetRubyBallScale(ballScale);
                    }

                    Thread.Sleep(100);
                }

                //Charge released. Freeze the final size and re-apply it so the fired orbs (same effect pool
                //as the ball) fly at the grown size, and inflate enemy body radii so the orbs' COLLISION
                //grows to match (equivalent to a bigger damage sphere; see Weapons.MaintainRubyOrbHitbox).
                float finalBallScale = ballScale;
                if (fullyCharged)
                {
                    Weapons.SetRubyBallScale(finalBallScale);
                    Weapons.MaintainRubyOrbHitbox(finalBallScale);
                }

                //Wait for the fired orbs to actually spawn before tracking them. The release animation takes
                //a moment (the held ball is killed, then the shots grab pool slots), so the old approach of
                //using the slot list captured at CHARGE START raced it — when the list was empty the reset
                //below ran instantly and snapped the just-fired orbs back to 1× (and skipped their damage).
                //Poll the live flags instead; time out in case the charge was interrupted without firing.
                List<int> liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                for (int wait = 0; liveOrbs.Count == 0 && wait < 60; wait++)   // up to ~3s (fire lands ~1.5s in)
                {
                    Thread.Sleep(50);
                    if (Player.Ruby.IsChargingAttack()) break;                 // interrupted → recharging already
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //Drive the boosted damage into every live orb until they all expire (slots re-read each tick
                //so late-spawning second orbs are covered too). Keep the enemy hitbox inflation fresh while
                //the orbs fly (covers enemies that spawn mid-flight).
                //Tick period: the engine inits an orb's damage at spawn and never rewrites it, so the only
                //race is spawn→our-next-tick; a point-blank orb can hit within a frame, so the period must
                //stay UNDER one frame (16.7ms @60fps). 10ms ≈ 0.6 frames of worst-case stale damage. PINE
                //writes aren't frame-synced, so exactly matching 16.7ms wouldn't align to anything anyway.
                int hitboxTick = 0;
                while (liveOrbs.Count > 0)
                {
                    //A NEW charge starting is the hand-off signal: its held ball is an active pool slot, so
                    //without this check the loop would never exit (blocking Dungeon from spawning a fresh
                    //MobiusRing), the new ball would inherit this charge's scale, and this loop would write
                    //THIS charge's damage into the new shot — a full size+power carry-over exploit. Break,
                    //reset below, and let the dispatcher start a clean ramp for the new charge. Any old orbs
                    //still flying keep their (already latched) boosted damage but snap to 1× visuals — brief
                    //and acceptable.
                    if (Player.Ruby.IsChargingAttack()) break;

                    foreach (int id in liveOrbs)
                        Memory.WriteInt(RubyOrbs.Orb0.damage + 4 * id, damage);
                    if (fullyCharged && ++hitboxTick % 20 == 0)
                        Weapons.MaintainRubyOrbHitbox(finalBallScale);
                    Thread.Sleep(10);
                    liveOrbs = RubyOrbs.GetRubyActiveOrbs();
                }

                //All orbs expired (or a new charge took over) — snap the effect pool back to its original
                //size so the next charge starts from a clean 1× template, and restore enemy hitboxes.
                Weapons.SetRubyBallScale(1.0f);
                Weapons.RestoreRubyOrbHitbox();
            }
        }

        // ── Secret Armlet ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Enables the Secret Armlet special effect:
        /// <br></br>
        /// <br>Makes all magic circle effects turn into positive ones.</br>
        /// </summary>
        /// <param name="isNewFloor">Determines if we have entered a new floor to know if we should run this code again.</param>
        public static bool SecretArmletEnable()
        {
            bool changed = false;

            //Check if any of the spawned circles have a negative effect and change into positive ones
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0 && Memory.ReadByte(Addresses.circleEffect1) > 4)
                { int effectPositive1 = random.Next(5); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0 && Memory.ReadByte(Addresses.circleEffect2) > 4)
                { int effectPositive2 = random.Next(5); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0 && Memory.ReadByte(Addresses.circleEffect3) > 4)
                { int effectPositive3 = random.Next(5); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect1) > 4)
                { int effectPositive4 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect2) > 4)
                { int effectPositive5 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0 && Memory.ReadByte(Addresses.backfloorcircleEffect3) > 4)
                { int effectPositive6 = random.Next(5); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

        /// <summary>
        /// Disables the Secret Armlet special effect and re-rolls any present magic circle outcome
        /// </summary>
        public static bool SecretArmletDisable()
        {
            bool changed = false;

            //Re-roll the existing circles
            if (Memory.ReadByte(Addresses.circleSpawn1) != 0)
                { int effectPositive1 = random.Next(10); Memory.WriteByte(Addresses.circleEffect1, (byte)effectPositive1); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn2) != 0)
                { int effectPositive2 = random.Next(10); Memory.WriteByte(Addresses.circleEffect2, (byte)effectPositive2); changed = true; }

            if (Memory.ReadByte(Addresses.circleSpawn3) != 0)
                { int effectPositive3 = random.Next(10); Memory.WriteByte(Addresses.circleEffect3, (byte)effectPositive3); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn1) != 0)
                { int effectPositive4 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect1, (byte)effectPositive4); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn2) != 0)
                { int effectPositive5 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect2, (byte)effectPositive5); changed = true; }

            if (Memory.ReadByte(Addresses.backfloorcircleSpawn3) != 0)
                { int effectPositive6 = random.Next(10); Memory.WriteByte(Addresses.backfloorcircleEffect3, (byte)effectPositive6); changed = true; }

            if (changed) return true; else return false;
        }

    }
}


