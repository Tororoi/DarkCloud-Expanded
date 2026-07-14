using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Dark_Cloud_Improved_Version
{
    public class Weapons
    {

        // ── Ruby ball scaling: WHICH lever to use. MOD implementation choices / crash triage, not game facts.
        // 1. Mot_List chain-1 SCALE keyframe patch — redundant with Core.
        // 2. CObject scale on the template + slots — THE visual lever.
        // 3. BT body radii — ⚠ CRASHES the game. Keep off. (Kept as a flag so the dead end stays documented.)
        private const bool RubyBallScaleSprites   = false;
        private const bool RubyBallScaleCore      = true;
        private const bool RubyBallScaleCollision = false;

        //Default Weapons ID
        public const int daggerid = Items.dagger;
        public const int woodenid = Items.woodenslingshot;
        public const int malletid = Items.mallet;
        public const int goldringid = Items.goldring;
        public const int stickid = Items.fightingstick;
        public const int machinegunid = Items.machinegun;

        //Base database table Dagger addresses
        public const int synth1 = 0x2027A717;       //Synth slot 1 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int synth2 = 0x2027A718;       //Synth slot 2 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int synth3 = 0x2027A719;       //Synth slot 3 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int synth4 = 0x2027A71A;       //Synth slot 4 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int synth5 = 0x2027A71B;       //Synth slot 5 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int synth6 = 0x2027A71C;       //Synth slot 6 (0 = None, 1 = Regular gray slot, 2 = Synth blue slot); (ALSO RUNTIME)
        public const int ownership = 0x2027A716;    //0 = Toan, 1 = Xiao, 2 = Goro, 3 = Ruby, 4 = Ungaga, 5 = Osmond;
        public const int whp = 0x2027A70C;          //Base weapon health points;
        public const int abs = 0x2027A73C;          //Base weapon absorption points; (ALSO RUNTIME)
        public const int absadd = 0x2027A73E;       //How much abs to be added per weapon level; (ALSO RUNTIME)
        public const int attack = 0x2027A70E;       //Base weapon Attack stat;
        public const int maxattack = 0x2027A750;    //Base weapon Max Attack stat; (ALSO RUNTIME)
        public const int endurance = 0x2027A710;    //Base weapon Endurance stat;
        public const int speed = 0x2027A712;        //Base weapon Speed stat;
        public const int magic = 0x2027A714;        //Base weapon Magic stat;
        public const int maxmagic = 0x2027A752;     //Base weapon Max Magic stat; (ALSO RUNTIME)
        public const int fire = 0x2027A71E;         //Base weapon Fire stat;
        public const int ice = 0x2027A720;          //Base weapon Ice stat;
        public const int thunder = 0x2027A722;      //Base weapon Thunder stat;
        public const int wind = 0x2027A724;         //Base weapon Wind stat;
        public const int holy = 0x2027A726;         //Base weapon Holy stat;
        public const int dinoslayer = 0x2027A728;   //Base weapon Dino Slayer stat;
        public const int undead = 0x2027A72A;       //Base weapon Undead Buster stat;
        public const int sea = 0x2027A72C;          //Base weapon Sea Killer stat;
        public const int stone = 0x2027A72E;        //Base weapon Stone Breaker stat;
        public const int plant = 0x2027A730;        //Base weapon Plant Buster stat;
        public const int beast = 0x2027A732;        //Base weapon Beast Buster stat;
        public const int sky = 0x2027A734;          //Base weapon Sky Hunter stat;
        public const int metal = 0x2027A736;        //Base weapon Metal Breaker stat;
        public const int mimic = 0x2027A738;        //Base weapon Mimic Breaker stat;
        public const int mage = 0x2027A73A;         //Base weapon Mage Slayer stat;
        public const int effect = 0x2027A744;       //Base weapon special effects (Set 1); (ALSO RUNTIME) - 2=Big bucks, 4=poor, 8=quench, 16=thirst, 32=poison, 64=stop, 128=steal
        public const int effect2 = 0x2027A745;      //Base weapon special effects (Set 2); (ALSO RUNTIME) - 1=fragile, 2=durable, 4=drain, 8=heal, 16=critical, 32=absup
        public const int buildup = 0x2027A748;      //Base weapon build-up branches;

        //Offset between each weapon
        public const int weaponoffset = 0x4C;

        //Character offsets
        public const int xiaooffset = 0xC78;    //Xiao
        public const int gorooffset = 0x10EC;   //Goro
        public const int rubyoffset = 0x15F8;   //Ruby
        public const int ungagaoffset = 0x1AB8; //Ungaga
        public const int osmondoffset = 0x1F78; //Osmond

        //Lamb sword buff
        public const int lambTransformThreshold = 0x202A1818;
        public const int lambStatsThreshold = 0x202A188C;

        public static Thread weaponsMenuListener = new Thread(new ThreadStart(WeaponListenForSynthSphere));

        static Random rnd = new Random();

        /// <summary>
        /// Adds a listener to the customize weapon menu to check for custom synthspheres and apply its effects if used
        /// </summary>
        public static void WeaponListenForSynthSphere()
        {
            int attack;
            int endurance;
            int speed;
            int magic;
            int weaponLevel;
            int diffLevel;
            int diffLevelBeforeChange;
            int hasChangedBySynth;

            while (Player.CheckIsWeaponCustomizeMenu())
            {
                int character = Memory.ReadByte(Addresses.weaponMenuCurrentCharacterHover);
                int weapon = Memory.ReadByte(Addresses.weaponMenuCurrentWeaponHover);

                switch (character)
                {
                    case 0:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Toan.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Toan.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Toan.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Toan.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Toan.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Toan.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Toan.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Toan.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Toan.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Toan.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Toan.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Toan.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;

                    case 1:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Xiao.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Xiao.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Xiao.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Xiao.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Xiao.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Xiao.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Xiao.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Xiao.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Xiao.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Xiao.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Xiao.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Xiao.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;

                    case 2:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Goro.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Goro.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Goro.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Goro.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Goro.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Goro.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Goro.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Goro.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Goro.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Goro.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Goro.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Goro.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;

                    case 3:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ruby.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Ruby.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Ruby.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Ruby.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ruby.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ruby.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ruby.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ruby.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ruby.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ruby.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ruby.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ruby.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;

                    case 4:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Ungaga.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Ungaga.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Ungaga.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Ungaga.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Ungaga.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Ungaga.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Ungaga.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;

                    case 5:
                        switch (weapon)
                        {
                            case 0:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot0.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot0.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot0.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot0.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot0.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot0.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot0.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot0.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot0.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot0.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot0.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot0.hasChangedBySynth, 0);
                                }
                                break;

                            case 1:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot1.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot1.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot1.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot1.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot1.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot1.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot1.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot1.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot1.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot1.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot1.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot1.hasChangedBySynth, 0);
                                }
                                break;

                            case 2:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot2.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot2.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot2.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot2.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot2.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot2.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot2.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot2.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot2.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot2.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot2.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot2.hasChangedBySynth, 0);
                                }
                                break;

                            case 3:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot3.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot3.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot3.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot3.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot3.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot3.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot3.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot3.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot3.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot3.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot3.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot3.hasChangedBySynth, 0);
                                }
                                break;

                            case 4:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot4.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot4.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot4.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot4.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot4.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot4.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot4.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot4.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot4.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot4.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot4.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot4.hasChangedBySynth, 0);
                                }
                                break;

                            case 5:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot5.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot5.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot5.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot5.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot5.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot5.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot5.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot5.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot5.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot5.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot5.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot5.hasChangedBySynth, 0);
                                }
                                break;

                            case 6:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot6.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot6.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot6.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot6.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot6.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot6.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot6.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot6.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot6.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot6.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot6.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot6.hasChangedBySynth, 0);
                                }
                                break;

                            case 7:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot7.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot7.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot7.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot7.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot7.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot7.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot7.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot7.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot7.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot7.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot7.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot7.hasChangedBySynth, 0);
                                }
                                break;

                            case 8:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot8.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot8.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot8.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot8.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot8.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot8.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot8.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot8.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot8.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot8.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot8.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot8.hasChangedBySynth, 0);
                                }
                                break;

                            case 9:

                                //Store the current weapon base stats
                                attack = Memory.ReadUShort(Player.Osmond.WeaponSlot9.attack);
                                endurance = Memory.ReadUShort(Player.Osmond.WeaponSlot9.endurance);
                                speed = Memory.ReadUShort(Player.Osmond.WeaponSlot9.speed);
                                magic = Memory.ReadUShort(Player.Osmond.WeaponSlot9.magic);
                                hasChangedBySynth = Memory.ReadUShort(Player.Osmond.WeaponSlot9.hasChangedBySynth);

                                //Store the current weapon level and calculate the difference to +5
                                weaponLevel = Memory.ReadByte(Player.Osmond.WeaponSlot9.level);
                                diffLevel = 5 - weaponLevel;

                                //Has the empty synthshpere in socket?
                                if (Memory.ReadUShort(Player.Osmond.WeaponSlot9.slot1_itemId) == Items.synthsphere &&
                                    Memory.ReadUShort(Player.Osmond.WeaponSlot9.slot1_synthesisedItemId) == 0)
                                {
                                    //Weapon level is below +5 and has not yet been changed by an empty synthphere?
                                    if (diffLevel > 0 && hasChangedBySynth == 0)
                                    {
                                        //Set the weapon to +5 with the increase in main stats
                                        Memory.WriteByte(Player.Osmond.WeaponSlot9.level, 5);
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.attack, (ushort)(attack + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.endurance, (ushort)(endurance + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.speed, (ushort)(speed + diffLevel));
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.magic, (ushort)(magic + diffLevel));

                                        //Save former stat value
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.weaponFormerStatsValue, (ushort)diffLevel);

                                        //Set changed flag
                                        Memory.WriteUShort(Player.Osmond.WeaponSlot9.hasChangedBySynth, 1);
                                    }

                                }
                                else if (diffLevel == 0 && hasChangedBySynth == 1)
                                {
                                    //Fetch the previous level before the change
                                    diffLevelBeforeChange = Memory.ReadUShort(Player.Osmond.WeaponSlot9.weaponFormerStatsValue);

                                    //Revert the weapons changes back to normal
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.level, (ushort)(5 - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.attack, (ushort)(attack - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.endurance, (ushort)(endurance - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.speed, (ushort)(speed - diffLevelBeforeChange));
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.magic, (ushort)(magic - diffLevelBeforeChange));

                                    //Reset flags
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.weaponFormerStatsValue, 0);
                                    Memory.WriteUShort(Player.Osmond.WeaponSlot9.hasChangedBySynth, 0);
                                }
                                break;
                        }
                        break;
                }

                Thread.Sleep(64);
            }
        }

        /// <summary>
        /// Applies all the weapon changes to their base values (This runs once when starting the mod)
        /// </summary>
        public static void WeaponsBalanceChanges()
        {
            if (Memory.ReadUShort(endurance + (weaponoffset * (Items.baselard - daggerid))) != 30) //check if changes have already applied
            {

                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Applying the new weapon changes...");


                /****************************************
                 *               TOAN                   *
                 ****************************************/

                //Baselard
                Memory.WriteUShort(endurance + (weaponoffset * (Items.baselard - daggerid)), 30);   //Endurance set to 30

                //Antique Sword
                Memory.WriteUShort(speed + (weaponoffset * (Items.antiquesword - daggerid)), 70);   //Speed set to 70
                Memory.WriteUShort(fire + (weaponoffset * (Items.antiquesword - daggerid)), 15);    //Fire set to 15

                //Kitchen Knife
                Memory.WriteUShort((whp + (weaponoffset * (Items.kitchenknife - daggerid))), 50);          //Whp set to 50
                Memory.WriteUShort((attack + (weaponoffset * (Items.kitchenknife - daggerid))), 25);       //Attack set to 25
                Memory.WriteUShort((endurance + (weaponoffset * (Items.kitchenknife - daggerid))), 30);    //Endurance set to 30
                Memory.WriteUShort((ice + (weaponoffset * (Items.kitchenknife - daggerid))), 0);           //Ice set to 0
                Memory.WriteUShort((thunder + (weaponoffset * (Items.kitchenknife - daggerid))), 8);       //Thunder set to 0
                Memory.WriteUShort((sea + (weaponoffset * (Items.kitchenknife - daggerid))), 90);          //Sea Killer set to 90
                Memory.WriteUShort((buildup + 5 + (weaponoffset * (Items.kitchenknife - daggerid))), 0);   //Set build-up branches to none (The 5 was just to offset to the correct address since I wasn't finding a way to write 8 bytes)

                //Tsukikage
                Memory.WriteUShort((endurance + (weaponoffset * (Items.tsukikage - daggerid))), 33);    //Endurance set to 33
                Memory.WriteUShort((speed + (weaponoffset * (Items.tsukikage - daggerid))), 80);        //Speed set to 80

                //Macho Sword
                Memory.WriteByte((effect2 + (weaponoffset * (Items.machosword - daggerid))), 32);  //Adds ABS up effect

                //Evilcise
                Memory.WriteByte((effect + (weaponoffset * (Items.evilcise - daggerid))), 4);    //Poor

                //Aga's Sword
                Memory.WriteUShort((buildup   + (weaponoffset * (Items.agassword - daggerid))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((maxattack + (weaponoffset * (Items.agassword - daggerid))), 190); //Max attack set to 190

                //Heaven's Cloud
                Memory.WriteUShort((synth3 + (weaponoffset * (Items.heavenscloud - daggerid))), 1);    //Adds a 3rd regular attachment slot
                Memory.WriteUShort((buildup + (weaponoffset * (Items.heavenscloud - daggerid))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((maxattack + (weaponoffset * (Items.heavenscloud - daggerid))), 180); //Max attack set to 180
                Memory.WriteUShort((maxmagic  + (weaponoffset * (Items.heavenscloud - daggerid))), 180); //Max magic set to 180

                //Lamb's Sword
                Memory.WriteUShort((synth3 + (weaponoffset * (Items.lambsswordnormal - daggerid))), 1);    //Adds a 3rd regular attachment slot
                Memory.WriteDouble(lambTransformThreshold, 0.5);    //Change the percent limit for when the sword should transform
                Memory.WriteFloat(lambStatsThreshold, (float)0.5); //Change the percent limit for when the sword stats should upgrade

                //Brave Ark
                Memory.WriteUShort((synth3 + (weaponoffset * (Items.braveark - daggerid))), 1);    //Adds a 3rd regular attachment slot

                //Big Bang
                Memory.WriteUShort((speed + (weaponoffset * (Items.bigbang - daggerid))), 70);    //Speed set to 70

                //Small Sword
                Memory.WriteUShort((whp + (weaponoffset * (Items.smallsword - daggerid))), 35);      //Whp set to 35
                Memory.WriteUShort((magic + (weaponoffset * (Items.smallsword - daggerid))), 17);    //Magic set to 17
                Memory.WriteUShort((sea + (weaponoffset * (Items.smallsword - daggerid))), 0);       //Sea Killer set to 0
                Memory.WriteUShort((metal + (weaponoffset * (Items.smallsword - daggerid))), 10);    //Metal Breaker set to 10

                //Sand Breaker
                Memory.WriteUShort((whp + (weaponoffset * (Items.sandbreaker - daggerid))), 45);          //Whp set to 45
                Memory.WriteUShort((endurance + (weaponoffset * (Items.sandbreaker - daggerid))), 25);    //Endurance set to 25
                Memory.WriteUShort((synth3 + (weaponoffset * (Items.sandbreaker - daggerid))), 1);        //Adds a 3rd regular attachment slot

                //Drain Seeker
                Memory.WriteUShort((whp + (weaponoffset * (Items.drainseeker - daggerid))), 60); //Whp set to 60

                //Chopper
                Memory.WriteUShort((speed + (weaponoffset * (Items.chopper - daggerid))), 60); //Speed set to 60

                //Choora
                Memory.WriteUInt((buildup + (weaponoffset * (Items.choora - daggerid))), 2147483648); //Build-up to Maneater only
                Memory.WriteUShort((whp + (weaponoffset * (Items.choora - daggerid))), 57);      //Whp set to 57
                Memory.WriteUShort((attack + (weaponoffset * (Items.choora - daggerid))), 45);   //Attack set to 45
                Memory.WriteUShort((speed + (weaponoffset * (Items.choora - daggerid))), 70);    //Speed set to 70
                Memory.WriteUShort((ice + (weaponoffset * (Items.choora - daggerid))), 10);      //Ice set to 10
                Memory.WriteUShort((thunder + (weaponoffset * (Items.choora - daggerid))), 15);  //Thunder set to 15
                Memory.WriteUShort((undead + (weaponoffset * (Items.choora - daggerid))), 15);   //Undead Buster set to 15
                Memory.WriteUShort((beast + (weaponoffset * (Items.choora - daggerid))), 15);    //Beaster Buster set to 15
                Memory.WriteUShort((metal + (weaponoffset * (Items.choora - daggerid))), 15);    //Metal Breaker set to 15
                Memory.WriteUShort((synth3 + (weaponoffset * (Items.choora - daggerid))), 1);    //Adds a 3rd regular attachment slot

                //Claymore
                Memory.WriteUShort((undead + (weaponoffset * (Items.claymore - daggerid))), 10);   //Undead Buster set to 10
                Memory.WriteUShort((beast + (weaponoffset * (Items.claymore - daggerid))), 10);    //Beaster Buster set to 10
                Memory.WriteUShort((mage + (weaponoffset * (Items.claymore - daggerid))), 10);     //Mage Slayer set to 10

                //Maneater
                Memory.WriteUShort((endurance + (weaponoffset * (Items.maneater - daggerid))), 44);    //Endurance set to 44
                Memory.WriteUShort((speed + (weaponoffset * (Items.maneater - daggerid))), 70);        //Speed set to 70
                Memory.WriteUShort((magic + (weaponoffset * (Items.maneater - daggerid))), 45);        //Magic set to 45
                Memory.WriteUShort((ice + (weaponoffset * (Items.maneater - daggerid))), 15);          //Ice set to 15
                Memory.WriteUShort((thunder + (weaponoffset * (Items.maneater - daggerid))), 15);      //Thunder set to 15
                Memory.WriteUShort((holy + (weaponoffset * (Items.maneater - daggerid))), 15);         //Holy set to 15
                Memory.WriteUShort((undead + (weaponoffset * (Items.maneater - daggerid))), 15);       //Undead Buster set to 15
                Memory.WriteUShort((beast + (weaponoffset * (Items.maneater - daggerid))), 15);        //Beast Buster set to 15
                Memory.WriteUShort((metal + (weaponoffset * (Items.maneater - daggerid))), 15);        //Metal Breaker set to 15
                Memory.WriteUShort((mimic + (weaponoffset * (Items.maneater - daggerid))), 10);        //Mimic Breaker set to 10

                //Bone Rapier
                Memory.WriteUShort((whp + (weaponoffset * (Items.bonerapier - daggerid))), 38);      //Whp set to 38
                Memory.WriteUShort((magic + (weaponoffset * (Items.bonerapier - daggerid))), 26);    //Magic set to 26

                //Sax
                Memory.WriteUShort((speed + (weaponoffset * (Items.sax - daggerid))), 60);    //Speed set to 60
                Memory.WriteUShort((fire + (weaponoffset * (Items.sax - daggerid))), 6);      //Fire set to 6
                Memory.WriteUShort((sky + (weaponoffset * (Items.sax - daggerid))), 10);      //Sky Hunter set to 10

                //7 Branch Sword
                Memory.WriteUShort((whp + (weaponoffset * (Items.sevenbranchsword - daggerid))), 47);          //Whp set to 47
                Memory.WriteUShort((endurance + (weaponoffset * (Items.sevenbranchsword - daggerid))), 47);    //Endurance set to 47
                Memory.WriteUShort((magic + (weaponoffset * (Items.sevenbranchsword - daggerid))), 37);        //Magic set to 37
                Memory.WriteUShort((dinoslayer + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);    //Dino Slayer set to 7
                Memory.WriteUShort((undead + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);        //Undead Buster set to 7
                Memory.WriteUShort((sea + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);           //Sea Killer set to 7
                Memory.WriteUShort((stone + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);         //Stone Breaker set to 7
                Memory.WriteUShort((plant + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);         //Plant Buster set to 7
                Memory.WriteUShort((beast + (weaponoffset * (Items.sevenbranchsword - daggerid))), 8);         //Beast Buster set to 8
                Memory.WriteUShort((sky + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);           //Sky Killer set to 7
                Memory.WriteUShort((metal + (weaponoffset * (Items.sevenbranchsword - daggerid))), 10);        //Metal Breaker set to 10
                Memory.WriteUShort((mimic + (weaponoffset * (Items.sevenbranchsword - daggerid))), 7);         //Mimic Breaker set to 7
                Memory.WriteUShort((mage + (weaponoffset * (Items.sevenbranchsword - daggerid))), 8);          //Mage Slayer set to 8

                //Cross Hinder
                Memory.WriteUShort((endurance + (weaponoffset * (Items.crosshinder - daggerid))), 50);    //Endurance set to 50
                Memory.WriteUShort((speed + (weaponoffset * (Items.crosshinder - daggerid))), 70);        //Speed set to 70
                Memory.WriteUShort((magic + (weaponoffset * (Items.crosshinder - daggerid))), 32);        //Magic set to 32

                //Chronicle 2
                Memory.WriteUShort(maxattack + (weaponoffset * (Items.chronicletwo - daggerid)), 999); //Max Attack set to 999




                /****************************************
                 *               XIAO                   *
                 ****************************************/

                //Wooden Slingshot
                Memory.WriteUShort((attack + (xiaooffset + (weaponoffset * (Items.woodenslingshot - woodenid)))), 6); //Attack set to 6
                Memory.WriteUShort((magic + (xiaooffset + (weaponoffset * (Items.woodenslingshot - woodenid)))), 2);  //Magic set to 2
                Memory.WriteUShort((fire + (xiaooffset + (weaponoffset * (Items.woodenslingshot - woodenid)))), 4);   //Fire set to 4

                //Bandit Slingshot
                // Memory.WriteUInt((buildup + (xiaooffset + (weaponoffset * (Items.banditslingshot - woodenid)))), 128); //Sets build-up to Double Impact only

                //Bone Slingshot
                Memory.WriteUShort((attack + (xiaooffset + (weaponoffset * (Items.boneslingshot - woodenid)))), 11);    //Attack set to 11
                Memory.WriteUShort((endurance + (xiaooffset + (weaponoffset * (Items.boneslingshot - woodenid)))), 30); //Endurance set to 30

                //Hardshooter
                Memory.WriteUShort((speed + (xiaooffset + (weaponoffset * (Items.hardshooter - woodenid)))), 60); //Speed set to 60

                //Matador
                Memory.WriteByte((effect2 + (xiaooffset + (weaponoffset * (Items.matador - woodenid)))), 16); //Adds Critical effect




                /****************************************
                 *               Goro                   *
                 ****************************************/

                //Turtle Shell
                Memory.WriteUShort((magic + (gorooffset + (weaponoffset * (Items.turtleshell - malletid)))), 10); //Magic set to 10

                //Big Bucks Hammer
                Memory.WriteUInt((buildup + (gorooffset + (weaponoffset * (Items.bigbuckshammer - malletid)))), 8); //Sets build-up branch to Magical Hammer only

                //Frozen Tuna
                Memory.WriteUShort((whp       + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 65);  //Whp set to 65
                Memory.WriteUShort((buildup   + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 0);   //No buildup paths (final form)
                Memory.WriteUShort((maxattack + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 100); //Max attack set to 100
                Memory.WriteUShort((maxmagic  + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 678); //Max MP set to 678
                Memory.WriteByte  ((effect    + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 64);  //Stop
                Memory.WriteUShort((synth4    + (gorooffset + (weaponoffset * (Items.frozentuna - malletid)))), 1);   //Adds a 3rd regular attachment slot

                //Gaia Hammer
                Memory.WriteUShort((endurance + (gorooffset + (weaponoffset * (Items.gaiahammer - malletid)))), 25); //Endurance set to 25

                //Trial Hammer
                Memory.WriteUShort((attack + (gorooffset + (weaponoffset * (Items.trialhammer - malletid)))), 30);    //Attack set to 30
                Memory.WriteUShort((endurance + (gorooffset + (weaponoffset * (Items.trialhammer - malletid)))), 25); //Endurance set to 25




                /****************************************
                 *               Ruby                   *
                 ****************************************/

                //Gold Ring
                Memory.WriteUShort((attack + (rubyoffset + (weaponoffset * (Items.goldring - goldringid)))), 15);  //Attack set to 15
                Memory.WriteUShort((magic + (rubyoffset + (weaponoffset * (Items.goldring - goldringid)))), 30);   //Magic set to 30

                //Bandit's Ring
                Memory.WriteUShort((attack + (rubyoffset + (weaponoffset * (Items.banditsring - goldringid)))), 30);      //Attack set to 30
                Memory.WriteUShort((maxattack + (rubyoffset + (weaponoffset * (Items.banditsring - goldringid)))), 50);   //Max Attack set to 50
                Memory.WriteUShort((magic + (rubyoffset + (weaponoffset * (Items.banditsring - goldringid)))), 20);       //Magic set to 20
                // Memory.WriteInt((buildup + (rubyoffset + (weaponoffset * (Items.banditsring - goldringid)))), 8200);      //Sets build-up branches to both Crystal Ring and Thorn Armlet

                //Platinum Ring
                Memory.WriteUShort((attack + (rubyoffset + (weaponoffset * (Items.platinumring - goldringid)))), 23); //Attack set to 23

                //Pocklekul
                Memory.WriteUShort((attack + (rubyoffset + (weaponoffset * (Items.pocklekul - goldringid)))), 28);      //Attack set to 28
                Memory.WriteUShort((magic + (rubyoffset + (weaponoffset * (Items.pocklekul - goldringid)))), 28);       //Magic set to 28
                Memory.WriteUShort((holy + (rubyoffset + (weaponoffset * (Items.pocklekul - goldringid)))), 0);         //Holy set to 0
                Memory.WriteUShort((buildup + (rubyoffset + (weaponoffset * (Items.pocklekul - goldringid)))), 8256);   //Sets build-up branches to both Fairy Ring and Thorn Armlet

                //Thorn Armlet
                Memory.WriteUShort((maxattack + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 90);   //Max Attack set to 90
                Memory.WriteUShort((maxmagic + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 72);  //Max Magic set to 72
                Memory.WriteUShort((stone + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 20);     //Stone Breaker set to 20
                Memory.WriteUShort((beast + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 20);     //Beast Buster set to 20
                Memory.WriteUShort((buildup + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 128);  //Sets build-up branches to Destruction Ring

                //Athenas Armlet
                Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.athenasarmlet - daggerid)))), 32);     //Adds ABS up effect




                /****************************************
                 *               Ungaga                 *
                 ****************************************/

                for (int ungagaweaponid = 348; ungagaweaponid <= 360; ungagaweaponid++)
                {
                    if (ungagaweaponid != 357)
                    {
                        int CurrWeaponAttack = Memory.ReadUShort((attack + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))));          //Reads the current weapon Attack value
                        int CurrWeaponMaxAttack = Memory.ReadUShort((maxattack + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))));    //Reads the current weapon Max Attack value
                        int CurrWeaponEndurance = Memory.ReadUShort((endurance + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))));    //Reads the current weapon Endurance value

                        Memory.WriteUShort((attack + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))), (ushort)(CurrWeaponAttack + 10));       //Adds +10 Attack to the current weapon being looped through
                        Memory.WriteUShort((maxattack + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))), (ushort)(CurrWeaponMaxAttack + 10)); //Adds +10 Max Attack to the current weapon being looped through
                        Memory.WriteUShort((endurance + (ungagaoffset + (weaponoffset * (ungagaweaponid - stickid)))), (ushort)(CurrWeaponEndurance + 15)); //Adds +15 Endurance to the current weapon being looped through
                    }
                }

                //Babel Spear
                Memory.WriteUShort((synth4 + (ungagaoffset + (weaponoffset * (Items.babelsspear - stickid)))), 1); //Adds a 4th regular attackment slot




                /****************************************
                 *               Osmond                 *
                 ****************************************/

                for (int osmondweaponid = Items.machinegun; osmondweaponid <= Items.swallow; osmondweaponid++)
                {
                    int CurrWeaponAttack = Memory.ReadUShort((attack + (osmondoffset + (weaponoffset * (osmondweaponid - machinegunid)))));         //Reads the current weapon Attack value
                    int CurrWeaponMaxAttack = Memory.ReadUShort((maxattack + (osmondoffset + (weaponoffset * (osmondweaponid - machinegunid)))));   //Reads the current weapon Max Attack value

                    Memory.WriteUShort((attack + (osmondoffset + (weaponoffset * (osmondweaponid - machinegunid)))), (ushort)(CurrWeaponAttack + 15));      //Adds +15 Attack to the current weapon being looped through
                    Memory.WriteUShort((maxattack + (osmondoffset + (weaponoffset * (osmondweaponid - machinegunid)))), (ushort)(CurrWeaponMaxAttack + 15)); //Adds +15 Max Attack to the current weapon being looped through
                }

                //Jackal
                Memory.WriteUShort((buildup + (osmondoffset + (weaponoffset * (Items.jackal - machinegunid)))), 4096);    //Build-up to Swallow only

                //Snail
                Memory.WriteUShort((buildup + (osmondoffset + (weaponoffset * (Items.snail - machinegunid)))), 256);     //Build-up to Hexa Blaster only

                //Blessing Gun
                Memory.WriteUShort((maxattack + (osmondoffset + (weaponoffset * (Items.blessinggun - machinegunid)))), 87);   //Blessing Gun max attack set to 87
                Memory.WriteUShort((maxmagic  + (osmondoffset + (weaponoffset * (Items.blessinggun - machinegunid)))), 80);   //Blessing Gun max magic set to 80

                //Skunk
                Memory.WriteUShort((maxattack + (osmondoffset + (weaponoffset * (Items.skunk - machinegunid)))), 143);        //Skunk max attack set to 143
                Memory.WriteUShort((maxmagic  + (osmondoffset + (weaponoffset * (Items.skunk - machinegunid)))), 105);         //Skunk max magic set to 105
                Memory.WriteUShort((buildup + (osmondoffset + (weaponoffset * (Items.skunk - machinegunid)))), 0);       //No buildup paths (final form)


                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Finished applying new weapon changes!");

            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "New weapon changes have already been applied!");
        }

        /// <summary>
        /// Process to roll the new weapon special attributes on weapons that now may have them
        /// </summary>
        public static void RerollWeaponSpecialAttributes()
        {
            while (true)
            {
                if (MainMenuThread.userMode == true)
                {
                    if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                    {
                        Thread.Sleep(100);

                        if (Memory.ReadByte(Addresses.mode) == 0 || Memory.ReadByte(Addresses.mode) == 1)
                        {
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Not ingame anymore! Exited from WeaponRerollEffectsThread!");
                            break;
                        }
                    }
                }

                //Base weapon special effects (Set 1); (ALSO RUNTIME) - 2=Big bucks, 4=poor, 8=quench, 16=thirst, 32=poison, 64=stop, 128=steal
                //Base weapon special effects (Set 2); (ALSO RUNTIME) - 1=fragile, 2=durable, 4=drain, 8=heal, 16=critical, 32=absup

                /*********************
                 *   Heavens Cloud   *
                 *********************/

                int attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets attribute
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which attribute it gets
                    {
                        Memory.WriteByte((effect + (weaponoffset * (Items.heavenscloud - daggerid))), 32);
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.heavenscloud - daggerid))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.heavenscloud - daggerid))), 16);
                        Memory.WriteByte((effect + (weaponoffset * (Items.heavenscloud - daggerid))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.heavenscloud - daggerid))), 0);
                    Memory.WriteByte((effect2 + (weaponoffset * (Items.heavenscloud - daggerid))), 0);
                }


                /**********************
                 *     Dark Cloud     *
                 **********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((effect + (weaponoffset * (Items.darkcloud - daggerid))), 32);
                    }
                    else
                    {
                        Memory.WriteByte((effect + (weaponoffset * (Items.darkcloud - daggerid))), 64);
                    }
                }
                else
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.darkcloud - daggerid))), 0);
                }

                /*********************
                 *      Big Bang     *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.bigbang - daggerid))), 16);
                        Memory.WriteByte((effect + (weaponoffset * (Items.bigbang - daggerid))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((effect + (weaponoffset * (Items.bigbang - daggerid))), 64);
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.bigbang - daggerid))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.bigbang - daggerid))), 0);
                    Memory.WriteByte((effect2 + (weaponoffset * (Items.bigbang - daggerid))), 0);
                }

                /************************
                 *   Atlamillia Sword   *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50) //first roll if weapon gets effect
                {
                    attributeRoll = rnd.Next(100);

                    if (attributeRoll < 50) //roll for which effect it gets
                    {
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.atlamilliasword - daggerid))), 8);
                        Memory.WriteByte((effect + (weaponoffset * (Items.atlamilliasword - daggerid))), 0);
                    }
                    else
                    {
                        Memory.WriteByte((effect + (weaponoffset * (Items.atlamilliasword - daggerid))), 64);
                        Memory.WriteByte((effect2 + (weaponoffset * (Items.atlamilliasword - daggerid))), 0);
                    }
                }
                else
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.atlamilliasword - daggerid))), 0);
                    Memory.WriteByte((effect2 + (weaponoffset * (Items.atlamilliasword - daggerid))), 0);
                }

                /*********************
                 *       Dusack      *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.dusack - daggerid))), 128);
                }
                else
                {
                    Memory.WriteByte((effect + (weaponoffset * (Items.dusack - daggerid))), 0);
                }

                /************************
                 *    Bone Slingshot    *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect2 + (xiaooffset + (weaponoffset * (Items.boneslingshot - woodenid)))), 1);
                }
                else
                {
                    Memory.WriteByte((effect2 + (xiaooffset + (weaponoffset * (Items.boneslingshot - woodenid)))), 0);
                }

                /*********************
                 *    Hardshooter    *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect2 + (xiaooffset + (weaponoffset * (Items.hardshooter - woodenid)))), 1);
                }
                else
                {
                    Memory.WriteByte((effect2 + (xiaooffset + (weaponoffset * (Items.hardshooter - woodenid)))), 0);
                }

                /**********************
                 *    Goddess Ring    *
                 **********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.goddessring - goldringid)))), 8);
                }
                else
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.goddessring - goldringid)))), 0);
                }

                /************************
                 *   Destruction Ring   *
                 ************************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.destructionring - goldringid)))), 16);
                }
                else
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.destructionring - goldringid)))), 0);
                }

                /*********************
                 *    Satans Ring    *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.satansring - goldringid)))), 4);
                }
                else
                {
                    Memory.WriteByte((effect2 + (rubyoffset + (weaponoffset * (Items.satansring - goldringid)))), 0);
                }

                /*********************
                 *   Thorn Armlet   *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 32); //Poison
                }
                else
                {
                    Memory.WriteByte((effect + (rubyoffset + (weaponoffset * (Items.thornarmlet - goldringid)))), 0);
                }

                /*********************
                 *      De Sanga     *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 30)
                {
                    Memory.WriteByte((effect2 + (ungagaoffset + (weaponoffset * (Items.desanga - stickid)))), 4);
                }
                else
                {
                    Memory.WriteByte((effect2 + (ungagaoffset + (weaponoffset * (Items.desanga - stickid)))), 0);
                }

                /*********************
                 *       Skunk       *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect + (osmondoffset + (weaponoffset * (Items.skunk - machinegunid)))), 32);
                }
                else
                {
                    Memory.WriteByte((effect + (osmondoffset + (weaponoffset * (Items.skunk - machinegunid)))), 0);
                }

                /*********************
                 *      Swallow      *
                 *********************/

                attributeRoll = rnd.Next(100);

                if (attributeRoll < 50)
                {
                    Memory.WriteByte((effect + (osmondoffset + (weaponoffset * (Items.swallow - machinegunid)))), 128);
                }
                else
                {
                    Memory.WriteByte((effect + (osmondoffset + (weaponoffset * (Items.swallow - machinegunid)))), 0);
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>TEST utility (no live callers — wire it up temporarily when a test save needs a
        /// weapon): give <paramref name="character"/> (Player.ToanId..OsmondId) a fresh copy of
        /// <paramref name="weaponId"/> at WeaponList base stats if they don't already own one. Writes the
        /// first empty weapon-inventory slot directly (id/stats/elements/antis/specials; level 0, xp 0,
        /// no attachments) — the same record layout MiniBoss's weapon boost edits. Call only once a save
        /// is loaded in-game (e.g. from MainMenuThread's "Entered ingame" block, where a commented-out
        /// example call lives). Returns true if the weapon was added.</summary>
        public static bool GiveWeaponIfMissing(int character, int weaponId)
        {
            int entry = WeaponList.EntryAddr(weaponId);
            if (entry < 0 || character < Player.ToanId || character > Player.OsmondId) return false;

            const int slotStride = 0xF8;   // per weapon-slot record
            const int charStride = 0xAA8;  // per character block (11 × 0xF8)
            long charBase = Addresses.firstBagWeapon + (long)character * charStride;

            // Already owned? Otherwise claim the first empty slot (id outside the weapon range).
            int free = -1;
            for (int s = 0; s < 10; s++)
            {
                int id = Memory.ReadUShort(charBase + s * slotStride);
                if (id == weaponId) return false;
                if (free < 0 && (id < Items.brokendagger || id > Items.swallow)) free = s;
            }
            if (free < 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"GiveWeaponIfMissing: no free weapon slot for character {character}.");
                return false;
            }

            long slot = charBase + free * slotStride;
            for (int i = 0; i < slotStride; i += 4) Memory.WriteInt(slot + i, 0);   // clean record (no attachments, level 0, xp 0)

            ushort whp = Memory.ReadUShort(entry + WeaponList.Whp);
            Memory.WriteUShort(slot + 0x00, (ushort)weaponId);
            Memory.WriteUShort(slot + 0x04, Memory.ReadUShort(entry + WeaponList.Attack));
            Memory.WriteUShort(slot + 0x06, Memory.ReadUShort(entry + WeaponList.Endurance));
            Memory.WriteUShort(slot + 0x08, Memory.ReadUShort(entry + WeaponList.Speed));
            Memory.WriteUShort(slot + 0x0A, Memory.ReadUShort(entry + WeaponList.Magic));
            Memory.WriteUShort(slot + 0x0C, whp);   // whpMax
            Memory.WriteUShort(slot + 0x10, whp);   // whp

            // Elements: table shorts → slot bytes (+0x17..0x1B); HUD element (+0x16) = strongest, 5 = none.
            int hud = 5, best = 0;
            for (int e = 0; e < 5; e++)
            {
                int v = Memory.ReadUShort(entry + WeaponList.Fire + e * 2);
                Memory.WriteByte(slot + 0x17 + e, (byte)Math.Min(v, 255));
                if (v > best) { best = v; hud = e; }
            }
            Memory.WriteByte(slot + 0x16, (byte)hud);

            // Anti-/slayer stats: table shorts (+0x1C..0x2E) → slot bytes (+0x1C..0x25), same order.
            for (int a = 0; a < 10; a++)
            {
                int v = Memory.ReadUShort(entry + WeaponList.DinoSlayer + a * 2);
                Memory.WriteByte(slot + 0x1C + a, (byte)Math.Min(v, 255));
            }

            Memory.WriteByte(slot + 0xEE, Memory.ReadByte(entry + WeaponList.Effect1));  // special1
            Memory.WriteByte(slot + 0xEF, Memory.ReadByte(entry + WeaponList.Effect2));  // special2

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"GiveWeaponIfMissing: added weapon {weaponId} to character {character} slot {free}.");
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════════════════════
        //  Heaven's Cloud reach + per-weapon whirlwind visual scale (runtime, data-side; no EE-code patch).
        //  Three features — see docs/weapon-reach.md / WeaponAddresses.WeaponModel:
        //   1. Scale Heaven's Cloud's blade mesh "w14" — grows the visible blade AND its dcol hit collision.
        //   2. Scale the whirlwind effect model (c01_fuusya) for EVERY Toan weapon, sized to its reach.
        //   3. Distance-gated enemy-hitbox inflation so the scaled HC's close/far hits stay consistent.
        // ════════════════════════════════════════════════════════════════════════════════════════
        const int HeavensCloudReachId = 271;

        // Whirl visual scale = D / WhirlwindVisualRadius, where D = the weapon's dcol1 reach (dcol1 Z × mesh
        // scale) and WhirlwindVisualRadius is the fuusya disc's default radius (its cyl1__cappz X/Z scale). It's
        // the single eyeball-calibration knob — the base cylinder's unit radius isn't readable from the
        // VIF1-packed verts, so nudge it until the visual edge meets the hit.
        const float WhirlwindVisualRadius = 8.0f; // Default 7.467f
        static float WhirlVisualScale;                          // 0 until located
        static float _weaponDcol1Z;                             // equipped weapon's dcol1 Z (0 = unknown)
        static int   _whirlWeaponId = -1;                       // weapon id WhirlVisualScale was computed for
        static int   _whirlDcolBackoff;
        static int   _whirlLocateBackoff;
        static long[] _whirlRoots = System.Array.Empty<long>(); // MMU bases of the fuusya "kiru" roots we scale
        static readonly float[] _whirlBind3x3 = new float[9];   // root's bind local-matrix 3x3 (target = bind × scale)
        static bool _whirlBindRead;

        // ── Ruby Mobius-Ring charge-ball scale (Mot_List keyframe patch; see SetRubyBallScale) ──
        static float _rubyBallApplied = 1.0f;  // factor currently baked into the scale keyframes
        static long  _rubyChainHead;           // MMU addr of the Mot_List head the factor was applied to
        static float _rubyOrigKey0;            // original (1×) first scale-key value — reload canary
        static long  _rubyBtAddr;              // MMU addr of the BT_SHOT_EFFECT the radii were captured from
        static float _rubyBtFactor = 1.0f;     // factor currently applied to the BT collision radii
        static readonly float[] _rubyBtOrig = new float[ShotEffectPool.BtShotRadiiCount]; // original per-phase radii

        // The exact chain-1 track-type set MotionProc (0x147D20) dispatches on. Anything else in a walked
        // record means we're not looking at a real Mot_List — stop before touching memory.
        static bool IsKnownTrackType(int t)
            => t == 0 || t == 1 || t == 2 || t == 0xC || (t >= 0x1E && t <= 0x21) || t == 0x28 || t == 0x29 || t == 0x32 || t == 0x33;

        static bool _reachStarted;

        /// <summary>Starts the Heaven's Cloud reach extender background thread (idempotent).</summary>
        public static void StartHeavensCloudReach()
        {
            if (_reachStarted) return;
            _reachStarted = true;
            new Thread(ReachLoop) { IsBackground = true }.Start();
        }

        // ── HC reach control (data-side; driven by CustomToanEffects.HeavensCloudEffect's charge ramp) ──
        // Everything is keyed to a blade `factor` (1.0 = original). Three writes:
        //   • blade mesh "w14" scaled by factor → the visible blade AND its dcol hit point grow together;
        //   • whirlwind effect (c01_fuusya) scaled to match: WhirlVisualScale = stockZ*factor / visualRadius;
        //   • enemy body radii inflated (distance-gated) to reach = stockZ*factor.
        // HeavensCloudEffect grows factor during the charge windup and applies whirl+hitbox on the whirlwind
        // release, then ResetHeavensCloudReach snaps everything back. (docs/weapon-reach.md top section.)
        const float HcStockDcol1Z = 9.2053f;       // HC commenu dcol1 Z
        static float _hcHitboxDelta;               // persistent delta added to enemy body radii (0 = none)
        static readonly float[,] _hcHitboxOrig = new float[16, BodyCollision.MaxBodyParts];

        const string HcModelCode = "c01w14";   // Heaven's Cloud; its mesh frame is the 'w14' child (see ResolveBladeFrame)

        /// <summary>The equipped weapon record (WEAPON_HAVE) of the active character, or 0 if there isn't one.</summary>
        internal static long EquippedRecord()
        {
            int ch = Player.CurrentCharacterNum();
            if (ch < 0) return 0;
            int slot = Memory.ReadByte(DngStatusData.Base +
                                       DngStatusData.EquipSlotArrayOffset + ch);
            return (uint)slot > 9 ? 0 : DngStatusData.WeaponRecord(ch, slot);
        }

        /// <summary>The element a weapon's hits carry, as the BIT the engine uses on the collision data
        /// (1=Fire, 2=Ice, 4=Thunder, 8=Wind, 0x10=Holy), or 0 for no element.
        ///
        /// The game keeps the SELECTED element index on the record (+0x16), maintained by SetWeaponElementStatus
        /// as "whichever of the five element levels is highest". The catch: that index is 0 (Fire) for a weapon
        /// with NO elements at all, because the scan starts at 0 and only moves on a strict &gt;. So the LEVEL has
        /// to be checked — a zero level means no element, not Fire.</summary>
        internal static int SelectedElementBits(long rec)
        {
            if (rec == 0) return 0;
            int idx = Memory.ReadByte(rec + WeaponHave.SelectedElementOffset);
            if ((uint)idx >= WeaponHave.ElementCount) return 0;
            int level = Memory.ReadByte(rec + WeaponHave.ElementLevelsOffset + idx);
            return level > 0 ? 1 << idx : 0;
        }

        /// <summary>Scale Heaven's Cloud's blade (visible blade + dcol hit points) to <paramref name="factor"/>×.
        /// Uses the same snapshot-and-MULTIPLY write as Super Steve's slingshot. The old route found the right
        /// frame ('w14') but wrote it badly: only the DIAGONAL, SET to `factor` absolutely — which (a) assumed the
        /// frame's base scale was exactly 1.0, (b) discarded the off-diagonal rotation terms, and (c) used a 0.01
        /// dead-band, ten times coarser than this path's 0.001, so a smooth charge ramp came out visibly stepped.
        /// Returns false until the frame is located (model not loaded yet).</summary>
        public static bool ScaleHeavensCloudBlade(float factor) => ScaleWeaponBlade(HcModelCode, factor);

        /// <summary>
        /// Resolve the frame that carries a weapon's MESH. Weapon models are NOT uniformly shaped, and the
        /// naming is not uniform either — this was established by dumping both trees:
        ///
        ///   Heaven's Cloud ("c01w14")  root '14_1'  ->  child 'w14'  ->  4x 'dcol'      (mesh on the CHILD)
        ///   Kitchen Knife  ("c01w08")  root 'c01w'  ->  'null' + 3x 'dcol'              (mesh on the ROOT; no 'w08')
        ///   Xiao slingshot ("c04w..")  root 'c04w'                                      (mesh on the ROOT)
        ///
        /// <c>FindFrame</c> matches the first FOUR bytes of the name, so we probe two candidates derived from the
        /// model code and take whichever the model actually has:
        ///   1. the "wNN" suffix (code minus the 3-char character prefix, NUL-padded) — HC's child mesh frame;
        ///   2. the 4-char prefix ("c01w" / "c04w") — the root, for models with no separate mesh child.
        ///
        /// The winner is cached per weapon id. It MUST NOT be re-probed per tick: a failed probe resets the
        /// snapshot in <see cref="ScaleWeaponFrameByName"/>, which would then re-snapshot an ALREADY-SCALED
        /// matrix and compound the scale every tick.
        /// </summary>
        static uint ResolveBladeFrame(string weaponCode)
        {
            if (string.IsNullOrEmpty(weaponCode) || weaponCode.Length < 4) return 0;

            if (weaponCode.Length > 4)                                   // "c01w14" -> 'w','1','4',NUL
            {
                string s = weaponCode.Substring(3);
                uint suffix = 0;
                for (int i = 0; i < 4 && i < s.Length; i++) suffix |= (uint)s[i] << (i * 8);
                if (suffix != 0 && LocateModelFrame(suffix, null) != 0) return suffix;
            }

            uint prefix = (uint)(weaponCode[0] | (weaponCode[1] << 8) | (weaponCode[2] << 16) | (weaponCode[3] << 24));
            return LocateModelFrame(prefix, null) != 0 ? prefix : 0;
        }

        static int  _bladeWeaponId = -1;
        static uint _bladeName;

        /// <summary>Scale an equipped weapon's mesh to <paramref name="factor"/>× — the visible model AND its dcol
        /// hit points grow together. Resolves the mesh frame with <see cref="ResolveBladeFrame"/> and scales it via
        /// <see cref="ScaleWeaponFrameByName"/>, which snapshots the frame's local 3x3 and MULTIPLIES it, so the
        /// model's own rotation and base scale survive and a gradual factor eases smoothly.
        ///
        /// Returns false until the frame is located (model not loaded). If it won't resolve,
        /// <see cref="DumpWeaponFrameTree"/> shows what the model actually calls its frames.</summary>
        public static bool ScaleWeaponBlade(string weaponCode, float factor)
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (wid != _bladeWeaponId || _bladeName == 0)
            {
                _bladeName = ResolveBladeFrame(weaponCode);
                _bladeWeaponId = wid;
                if (_bladeName == 0) return false;      // model not loaded yet — probe again next tick
            }
            return ScaleWeaponFrameByName(_bladeName, factor);
        }

        /// <summary>Scale the whirlwind visual to match a blade <paramref name="factor"/> and re-apply it to the fuusya roots.</summary>
        public static void ScaleHeavensCloudWhirl(float factor)
        {
            WhirlVisualScale = HcStockDcol1Z * factor / WhirlwindVisualRadius;
            MaintainWhirlScale();
        }

        /// <summary>Inflate enemy body hitboxes (distance-gated) to match a blade <paramref name="factor"/> (reach = stockZ×factor).</summary>
        public static void ScaleHeavensCloudHitbox(float factor) => MaintainEnemyHitbox(HcStockDcol1Z * factor);

        /// <summary>Snap Heaven's Cloud back to original: blade 1×, enemy hitboxes restored, whirl at base scale.</summary>
        public static void ResetHeavensCloudReach()
        {
            ScaleWeaponBlade(HcModelCode, 1.0f);
            RestoreEnemyHitbox();
            ScaleHeavensCloudWhirl(1.0f);
            _whirlWeaponId = -1;   // force ReachTick to recompute the whirl scale for whatever is equipped next
        }

        /// <summary>True while the player is winding up a WHIRLWIND charge (action 0xE at whirlwind charge
        /// level 2 — i.e. the meter has passed the whirlwind threshold and it's unlocked). The lunge-level
        /// windup (level 0/1) is excluded, so the blade only grows once the whirlwind charge specifically begins.</summary>
        public static bool IsChargingWhirlwind()
            => Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWindup
            && Memory.ReadInt(PlayerAction.ChargeLevel) == PlayerAction.ChargeLevelWhirl;

        /// <summary>True while the whirlwind attack is actually executing (action 0x18 with the charge-active
        /// flag still set). The flag clears on the final whirlwind frame, so this goes false immediately when the
        /// attack finishes — even though the action state lingers at 0x18 for a frame.</summary>
        public static bool IsWhirlwindActive()
            => Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWhirlwind
            && Memory.ReadInt(PlayerAction.ChargeActiveFlag) == 1;

        // Scale the equipped weapon's visible mesh frame (name@0 == nameWord) by factor: write factor to its
        // CFrameVu1 local-3x3 diagonal (+0xB8/+0xCC/+0xE0; bind is identity). The engine does not re-pose this
        // template node, so this holds. Returns false until the frame is located. Caches/re-validates by name.

        // Distance-gated enemy hitbox: for each active enemy, if its horizontal distance to the player is within
        // the range gate, ADD a reach bonus to its body radii so close swings connect; otherwise restore them to
        // their cached stock value so far hits never land past the blade. The whirlwind charge gets a bigger
        // bonus + a slightly wider gate (see below). The enemy slot carries both its own world position
        // (LocationX/Y) and the player's (TargetX/Y mirrors player dunPosition each frame), so the distance is
        // read entirely from the slot. Caches each part's stock value; forgets it when the slot goes inactive
        // (next occupant is recaptured from its own stock).
        static void MaintainEnemyHitbox(float reach)
        {
            // The whirlwind charge sweeps a wide arc, so it gets the full `reach` bonus and a slightly wider
            // range gate; every other attack (combo, lunge) gets reach − 5.0 with the plain gate.
            bool whirl = Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWhirlwind;
            float bonus  = whirl ? reach : reach;       // added to each enemy's stock body radius
            float gateSq = whirl ? reach * reach + 5.0f : reach * reach + 2.8f; // squared distance to player for the bonus to apply
            for (int s = 0; s < 16; s++)
            {
                long slot = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                int status = Memory.ReadInt(slot + EnemySlotOffsets.RenderStatus);
                if (status <= 0)                                       // empty slot → forget cached stock
                {
                    for (int p = 0; p < BodyCollision.MaxBodyParts; p++) _hcHitboxOrig[s, p] = 0f;
                    continue;
                }
                float dx = Memory.ReadFloat(slot + EnemySlotOffsets.LocationX) - Memory.ReadFloat(slot + EnemySlotOffsets.TargetX);
                float dy = Memory.ReadFloat(slot + EnemySlotOffsets.LocationY) - Memory.ReadFloat(slot + EnemySlotOffsets.TargetY);
                bool inRange = dx * dx + dy * dy <= gateSq;
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    long addr = BodyCollision.RadiusAddr(s, p);
                    float r = Memory.ReadFloat(addr);
                    float orig = _hcHitboxOrig[s, p];
                    if (orig <= 0.01f)                                 // capture stock the first time we see it
                    {
                        if (r <= 0.01f || r >= 1000f) continue;       // no real hitbox on this part
                        orig = _hcHitboxOrig[s, p] = r;
                    }
                    float want = inRange ? orig + bonus : orig;        // inflate while close, else stock
                    if (Math.Abs(r - want) > 0.05f) Memory.WriteFloat(addr, want);
                }
            }
            _hcHitboxDelta = reach;
        }

        // Restore every inflated enemy body radius to its cached stock base (called when HC is not equipped).
        // The mesh scale is a per-loaded-model template (reloaded per weapon), so it needs no restore.
        static void RestoreEnemyHitbox()
        {
            if (_hcHitboxDelta == 0f) return;
            for (int s = 0; s < 16; s++)
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                    if (_hcHitboxOrig[s, p] > 0.01f)
                    {
                        Memory.WriteFloat(BodyCollision.RadiusAddr(s, p), _hcHitboxOrig[s, p]);
                        _hcHitboxOrig[s, p] = 0f;
                    }
            _hcHitboxDelta = 0f;
        }

        // Walk the equipped weapon model's CFrame tree (POINTER, no scan) for a frame whose name (word@+0x118)
        // == nameWord and, if fifthByte is given, whose 5th name byte matches (to disambiguate dcol0..dcol3).
        // The model root = *(*NowWeapon+0xBC) is a CFrame (name@+0x118, child@+0x138, next@+0x13c — confirmed
        // via CopyFrameVu1/SearchFrame); DFS from it exactly as the game's SearchFrame does. Returns the frame's
        // NAME address (base+0x118, so the +0xB8/0xE8/0xF0 offset conventions hold), or 0 if not resolvable.
        static long LocateModelFrame(uint nameWord, byte? fifthByte)
        {
            int nw = Memory.ReadInt(WeaponModel.NowWeaponPtr);
            if (!IsRamPtr(nw)) return 0;
            int modelRoot = Memory.ReadInt(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            if (!IsRamPtr(modelRoot)) return 0;
            return FindFrame(Memory.ToMmu(modelRoot), nameWord, fifthByte, 0);
        }

        // Recursive DFS over the CFrame tree (child @+0x138, sibling @+0x13c, name @+0x118). Returns the matching
        // frame's NAME address, or 0. Depth-capped so a stray/looping pointer can't recurse forever.
        static long FindFrame(long node, uint nameWord, byte? fifthByte, int depth)
        {
            if (depth > 32) return 0;
            long name = node + CFrameVu1.Name;
            if ((uint)Memory.ReadInt(name) == nameWord &&
                (!fifthByte.HasValue || (byte)Memory.ReadByte(name + 4) == fifthByte.Value))
                return name;
            for (int c = Memory.ReadInt(node + CFrameVu1.RootChild); IsRamPtr(c); )
            {
                long cm = Memory.ToMmu(c);
                long hit = FindFrame(cm, nameWord, fifthByte, depth + 1);
                if (hit != 0) return hit;
                c = Memory.ReadInt(cm + CFrameVu1.RootSibling);
            }
            return 0;
        }

        /// <summary>Re-arm on floor entry so the freshly reloaded weapon model / fuusya pool is re-located.</summary>
        public static void OnReachFloorEntered()
        {
            _whirlRoots = System.Array.Empty<long>(); _whirlLocateBackoff = 0;
            _sfFrameNameAddr = 0; _sfHasSnapshot = false; _sfWeaponId = -1;   // model moved: re-locate + re-snapshot
            _bladeName = 0; _bladeWeaponId = -1;
        }

        static bool _weaponTreeDumped;

        /// <summary>
        /// DIAGNOSTIC: log the equipped weapon model's whole CFrame tree — each frame's name and its current
        /// scale (local 3x3 m00). This is how you discover a weapon's mesh-frame name, and it is worth having:
        /// the trees are NOT uniformly shaped. Heaven's Cloud hangs its mesh on a 'w14' CHILD under a '14_1'
        /// root; the Kitchen Knife has no 'w08' child at all and carries the mesh on its 'c01w' ROOT. A name
        /// that works for one weapon silently finds nothing on another — dump, do not assume.
        ///
        /// (This replaces a ScaleAllWeaponMeshes that scaled every frame beginning with 'w'. It was dead code
        /// and its premise was wrong twice over: its docstring claimed it served the slingshot, but the
        /// slingshot's frame begins with 'c', so it could never have matched it.)
        /// </summary>
        public static bool DumpWeaponFrameTree()
        {
            if (_weaponTreeDumped) return true;
            _weaponTreeDumped = true;

            int nw = Memory.ReadInt(WeaponModel.NowWeaponPtr);
            if (!IsRamPtr(nw))
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[weapon model] NowWeapon not resolvable (nw=0x{nw:X})");
                return false;
            }
            int modelRoot = Memory.ReadInt(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            if (!IsRamPtr(modelRoot))
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[weapon model] modelRoot not resolvable (nw=0x{nw:X} root=0x{modelRoot:X})");
                return false;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"=== equipped weapon frame tree (nw=0x{nw:X} root=0x{modelRoot:X}) ===");
            DumpFrameRec(Memory.ToMmu(modelRoot), 0);
            return true;
        }

        static void DumpFrameRec(long node, int depth)
        {
            if (depth > 32) return;
            long name = node + CFrameVu1.Name;
            uint nword = (uint)Memory.ReadInt(name);
            string s = "";
            for (int b = 0; b < 4; b++) { char c = (char)((nword >> (b * 8)) & 0xFF); s += (c >= 32 && c < 127) ? c : '.'; }
            Console.WriteLine($"{new string(' ', depth * 2)}[{depth}] '{s}' (0x{nword:X8}) m00={Memory.ReadFloat(name + WeaponModel.Vu1LocalMatrixDiag0):F2}");
            for (int c = Memory.ReadInt(node + CFrameVu1.RootChild); IsRamPtr(c); )
            {
                long cm = Memory.ToMmu(c);
                DumpFrameRec(cm, depth + 1);
                c = Memory.ReadInt(cm + CFrameVu1.RootSibling);
            }
        }

        // Uniform-scale a named model frame by MULTIPLYING its local 3x3 by `factor` (snapshotting the
        // which SETS the diagonal — that only works on an identity/bind matrix; the slingshot's frames carry
        // rotation, so we scale by multiply to preserve orientation). Snapshots the frame's ORIGINAL matrix
        // the first time it's located and always writes original×factor, so it's stable across ticks and a
        // gradual factor eases smoothly; pass factor 1.0 to restore. Re-snapshots when the model reloads.
        static long _sfFrameNameAddr;
        static uint _sfNameWord;
        static int  _sfWeaponId = -1;   // two Toan swords share the "c01w" key — re-locate + re-snapshot on a swap
        static readonly float[] _sfOrig = new float[9];
        static bool _sfHasSnapshot;

        public static bool ScaleWeaponFrameByName(uint nameWord, float factor)
        {
            int wid = Player.Weapon.GetCurrentWeaponId();
            if (_sfFrameNameAddr == 0 || _sfNameWord != nameWord || _sfWeaponId != wid ||
                (uint)Memory.ReadInt(_sfFrameNameAddr) != nameWord)
            {
                _sfFrameNameAddr = LocateModelFrame(nameWord, null);
                _sfNameWord = nameWord;
                _sfWeaponId = wid;
                _sfHasSnapshot = false;         // never carry a snapshot across models
                if (_sfFrameNameAddr == 0) return false;
            }
            long m = _sfFrameNameAddr + WeaponModel.Vu1LocalMatrixDiag0;   // 3x3 base: row stride 0x10, col stride 4
            if (!_sfHasSnapshot)
            {
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        _sfOrig[r * 3 + c] = Memory.ReadFloat(m + r * 0x10 + c * 4);
                _sfHasSnapshot = true;
            }
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    long addr = m + r * 0x10 + c * 4;
                    float want = _sfOrig[r * 3 + c] * factor;
                    if (Math.Abs(Memory.ReadFloat(addr) - want) > 0.001f) Memory.WriteFloat(addr, want);
                }
            return true;
        }

        static void ReachLoop()
        {
            while (true)
            {
                try { ReachTick(); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "HC reach error: " + ex.Message); }
                Thread.Sleep(150);
            }
        }

        static void ReachTick()
        {
            // Whirlwind VISUAL scale applies to ALL of Toan's weapons (the fuusya effect is character-, not
            // weapon-bound), sized to each weapon's own dcol1 reach — EXCEPT Heaven's Cloud, whose blade/whirl/
            // hitbox are driven per-charge by CustomToanEffects.HeavensCloudEffect (which owns the shared whirl state
            // while HC is equipped, so this loop must not also write it). Recompute on weapon swap.
            if (Player.CurrentCharacterNum() != Player.ToanId) return;
            int wid = GetEquippedWeaponId();
            if (wid == HeavensCloudReachId) return;   // HC → HeavensCloudEffect

            if (wid != _whirlWeaponId)
            {
                _whirlWeaponId = wid; _weaponDcol1Z = 0f; WhirlVisualScale = 0f; _whirlDcolBackoff = 0;
                // The fuusya pool relocates when a new weapon model loads — drop the cached root(s) and clear the
                // locate backoff so the first walking tick re-finds and re-scales it with the NEW scale.
                _whirlRoots = System.Array.Empty<long>(); _whirlLocateBackoff = 0;
                // Static table first (offline-extracted dcol1; abs guards mirrored models). Falls back to a live
                // tree-walk only for ids not in the table or with no dcol1 frame (e.g. id 277).
                if (ToanWeapons.TryGetValue(wid, out WeaponData wd) && wd.Dcol1.HasValue)
                {
                    _weaponDcol1Z = Math.Abs(wd.Dcol1.Value);
                    WhirlVisualScale = _weaponDcol1Z / WhirlwindVisualRadius;
                }
            }
            if (_weaponDcol1Z == 0f) { if (_whirlDcolBackoff <= 0) LocateWeaponDcol1(); else _whirlDcolBackoff--; }
            if (WhirlVisualScale > 0f) MaintainWhirlScale();
        }

        // Locate / maintain the whirlwind effect (c01_fuusya). Only the root "kiru" matrix transforms the
        // VERTEX_ANIME mesh, so we scale the root's local-matrix 3x3. The effect is a pool of concurrent
        // instances (LocateWhirlRoots derefs all of them via pointer); we keep every pool root scaled so
        // whichever a cast activates is already correct. Each cast re-poses its root once; the maintain re-applies.
        static void MaintainWhirlScale()
        {
            // Only touch the effect during active in-field gameplay. In the weapon menu / floor transitions
            // the game reallocates models, and writing the fuusya structure then can corrupt the reload and
            // crash the emulator (observed on weapon switch). Resume when walking again.
            if (!Player.CheckDunIsWalkingMode()) return;

            if (_whirlRoots.Length > 0)
            {
                // Drop the cache if the first root's name vanished (model freed/relocated); else re-apply.
                if (Memory.ReadUInt(_whirlRoots[0] + CFrameVu1.Name) != ShotEffectPool.KiruNameWord)
                    { _whirlRoots = System.Array.Empty<long>(); return; }
                // Keep EVERY pool instance scaled (don't narrow to one): a cast right after a swap can activate a
                // different instance than the previously-live one, so narrowing would leave that first cast at the
                // wrong scale. Re-applying to all idle copies is harmless (they're at world 0,0,0 until used).
                float target0 = _whirlBind3x3[0] * WhirlVisualScale;
                foreach (long root in _whirlRoots)
                    if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - target0) > 0.01f) WriteWhirlScaled(root);
            }
            else if (_whirlLocateBackoff <= 0) LocateWhirlRoots();
            else _whirlLocateBackoff--;
        }

        // Scale a fuusya root's local-matrix 3x3 (base+0x1d0) by bind*WhirlVisualScale (the only matrix that
        // transforms the morph mesh), leaving the translation row (+0x200) anchored. Force a recompute.
        static void WriteWhirlScaled(long root)
        {
            for (int i = 0; i < 9; i++)
                Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[i], _whirlBind3x3[i] * WhirlVisualScale);
            Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
        }

        // True if `root` is a fuusya "kiru" instance (next frame +0x270 is "fkiri"). Caches its bind 3x3 once.
        static bool ValidateWhirlRoot(long root)
        {
            if (Memory.ReadUInt(root + CFrameVu1.Name) != ShotEffectPool.KiruNameWord) return false;
            if (Memory.ReadUInt(root + ShotEffectPool.FuusyaFrameStride + CFrameVu1.Name) != ShotEffectPool.FkiriNameWord) return false;
            if (!_whirlBindRead)
            {
                for (int k = 0; k < 9; k++) _whirlBind3x3[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                if (Math.Abs(_whirlBind3x3[0]) < 0.05f || Math.Abs(_whirlBind3x3[0]) > 4.0f) return false; // already scaled / bad read
                _whirlBindRead = true;
            }
            return true;
        }

        // Equipped weapon id from the inventory equip slot (updates IMMEDIATELY on a weapon swap, unlike the
        // battle-block id GetCurrentWeaponId/0x21EA7590 which only refreshes once Toan is walking again — so
        // keying the reach scheme off this removes the post-swap lag). Toan's weapon list, so callers gate on
        // ToanId. Returns -1 when no valid slot.
        static int GetEquippedWeaponId()
        {
            int slot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
            if ((uint)slot > 9) return -1;
            return Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id + slot * WeaponHave.InventoryWeaponSlotStride);
        }

        // Fallback for weapons not in ToanWeapons (or with no static dcol1): read the EQUIPPED weapon's dcol1 Z
        // live by tree-walking its model to the "dcol1" frame (LocateModelFrame — POINTER, no scan), and size
        // the whirl from it. The dcol1 frame's local translation is (0,0,Z) at name+0xE8/+0xEC/+0xF0; Z = reach.
        static void LocateWeaponDcol1()
        {
            long name = LocateModelFrame(WeaponModel.DcolNameWord, WeaponModel.Dcol1Digit);
            if (name == 0) { _whirlDcolBackoff = 8; return; }        // model / dcol1 not ready yet
            float x = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalX);
            float y = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalX + 4);
            float z = Memory.ReadFloat(name + WeaponModel.DcolNameToLocalZ);
            if (Math.Abs(x) > 0.5f || Math.Abs(y) > 0.5f || z < 0.1f || z > 40f) { _whirlDcolBackoff = 8; return; } // sane (0,0,Z)
            _weaponDcol1Z = z;
            WhirlVisualScale = z / WhirlwindVisualRadius;
        }

        static bool IsRamPtr(int p) => (uint)p >= 0x80000 && (uint)p < Memory.EeRamSize;

        // Locate the fuusya effect root(s) via a direct pointer (no RAM scan): the main-character effect object
        // is a fixed global (MainCharaEffectBase) with EffectSlotCount pool slots; each slot's model object
        // holds its root CFrame ("kiru") pointer at a fixed offset, so we deref straight to the roots and scale
        // every valid one. Backs off ~1s if the effect isn't loaded yet. Caches the bind 3x3.
        static void LocateWhirlRoots()
        {
            var roots = new System.Collections.Generic.List<long>();
            for (int s = 0; s < ShotEffectPool.EffectSlotCount; s++)
            {
                int p = Memory.ReadInt(ShotEffectPool.MainCharaEffectBase + (long)s * ShotEffectPool.EffectSlotStride + ShotEffectPool.EffectSlotModelOff);
                if (!IsRamPtr(p)) continue;
                long root = Memory.ToMmu(p);
                if (ValidateWhirlRoot(root)) roots.Add(root);
            }
            if (roots.Count == 0) { _whirlLocateBackoff = 8; return; }   // effect not loaded yet → retry later
            _whirlRoots = roots.ToArray();
            foreach (long root in _whirlRoots) WriteWhirlScaled(root);
        }

        // ════════════════════════════════════════════════════════════════════════════════════════
        //  Ruby Mobius-Ring charge-ball growth. The ball + fired orbs are CSHOT_EFFECT slots of the
        //  MainCharaEffectBase pool (decomp-confirmed: RubyOrbs' addresses ARE the pool's per-slot
        //  fields; the objects are full CCharacters). Three coordinated levers, all engine-fed:
        //   1. Mot_List SCALE keyframes (ratio-multiply) — the billboard sprite layers, whose size the
        //      motion animates per frame (CONFIRMED working in-game).
        //   2. CObject scale (+0x90/94/98) on template + slots — Draw__10CCharacter pushes it into the
        //      root frame every draw, scaling the whole hierarchy (the ball's core geometry, which the
        //      sprite tracks don't cover — visibly element-dependent otherwise).
        //   3. BT_SHOT_EFFECT per-phase collision radii (BT+0x28) — Step builds the shot's damage
        //      sphere from these, so the hit grows with the visual.
        //  All slots share one Mot_List/BT, so the fired orbs inherit everything. Layout consts + RE
        //  notes in WeaponAddresses.cs. Driven from CustomRubyEffects.MobiusRingEffect; restored via factor 1.0.
        // ════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Grow Ruby's charge ball / shot orbs (visual + collision) to <paramref name="factor"/>×
        /// (1.0 = original). Idempotent per factor; detects a reloaded effect (character/floor change) and
        /// rebases. Only writes during active field play.</summary>
        public static void SetRubyBallScale(float factor)
        {
            if (!Player.CheckDunIsWalkingMode()) return;

            // 1. Sprite layers: ratio-patch the chain-1 SCALE keyframe values. REDUNDANT with the core
            // object scale (billboards inherit the root scale — live-confirmed 2026-07-06: both together
            // double-scale the sprite layers), so this lever is normally OFF; kept for experiments.
            // CHAIN-1 ONLY: chain-2 (MotionProc2) records are vertex-skinning data with a different layout
            // (+0x10 = blend weights) — patching them corrupts vertex animation (the Read Abort crashes).
            if (RubyBallScaleSprites)
            {
                var tracks = new System.Collections.Generic.List<(long Keys, int Count)>();
                long head = 0;
                for (int bank = 0; bank < 8; bank++)
                {
                    // Trust a bank only if its motion-id range fields look sane (GetMotionParam's own check).
                    long chr = ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectTemplateOff;
                    int first = Memory.ReadInt(chr + 0x3E0 + bank * 4);
                    int end   = Memory.ReadInt(chr + 0x400 + bank * 4);
                    int mpN   = Memory.ReadInt(chr + 0xC20 + bank * 4);
                    if (!IsRamPtr(mpN) || first < 0 || end <= first || end > 100) continue;
                    long mp = Memory.ToMmu(mpN);

                    int headN = Memory.ReadInt(mp + ShotEffectPool.MotionParamChain1);
                    if (!IsRamPtr(headN)) continue;
                    long rec = Memory.ToMmu(headN);
                    if (head == 0) head = rec;                                 // identity for the rebase check
                    for (int i = 0; i < 64 && rec != 0; i++)
                    {
                        int frame = Memory.ReadInt(rec + ShotEffectPool.MotRecFrameIdx);
                        int type  = Memory.ReadInt(rec + ShotEffectPool.MotRecType);
                        int cnt   = Memory.ReadInt(rec + ShotEffectPool.MotRecKeyCount);
                        int keysN = Memory.ReadInt(rec + ShotEffectPool.MotRecKeysPtr);
                        int nextN = Memory.ReadInt(rec + ShotEffectPool.MotRecNext);
                        if ((uint)frame >= 32 || cnt <= 0 || cnt > 300 || !IsRamPtr(keysN) || !IsKnownTrackType(type)) break;
                        if (type == ShotEffectPool.MotTypeScale)
                        {
                            // Keys must read as keyframes: small non-decreasing integer times at +0 of each 0x20.
                            long keys = Memory.ToMmu(keysN);
                            int t0 = Memory.ReadInt(keys);
                            int t1 = cnt > 1 ? Memory.ReadInt(keys + ShotEffectPool.MotKeyStride) : t0;
                            if (t0 >= 0 && t0 <= 1000 && t1 >= t0 && t1 <= 1000)
                                tracks.Add((keys, cnt));
                        }
                        rec = nextN == 0 ? 0 : Memory.ToMmu(nextN);
                    }
                }
                if (tracks.Count > 0)
                {
                    // Rebase on reload: a new chain address, or the canary key reading at its 1× value again
                    // (same address, freshly rebuilt data), means the keyframes are back at original scale.
                    float v0 = Memory.ReadFloat(tracks[0].Keys + ShotEffectPool.MotKeyValueOff);
                    if (head != _rubyChainHead) { _rubyChainHead = head; _rubyBallApplied = 1f; _rubyOrigKey0 = v0; }
                    else if (Math.Abs(v0 - _rubyOrigKey0 * _rubyBallApplied) > Math.Abs(_rubyOrigKey0) * 0.05f + 0.001f)
                        { _rubyBallApplied = 1f; _rubyOrigKey0 = v0; }

                    float ratio = factor / _rubyBallApplied;
                    if (Math.Abs(ratio - 1f) >= 0.005f)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"RubyBall: applying sprite keyframe scale {factor:F2} ({tracks.Count} tracks)");
                        foreach (var (keys, cnt) in tracks)
                        {
                            byte[] blk = Memory.ReadBytesBatch(keys, cnt * ShotEffectPool.MotKeyStride);
                            if (blk == null) continue;
                            for (int k = 0; k < cnt; k++)
                                for (int c = 0; c < 3; c++)                    // value vec xyz at key+0x10
                                {
                                    int off = k * ShotEffectPool.MotKeyStride + ShotEffectPool.MotKeyValueOff + c * 4;
                                    BitConverter.GetBytes(BitConverter.ToSingle(blk, off) * ratio).CopyTo(blk, off);
                                }
                            Memory.WriteByteArray(keys, blk);
                        }
                        _rubyBallApplied = factor;
                    }
                }
            }

            // 2. Core geometry: object scale on the template + every slot. The engine (Draw__10CCharacter)
            // re-applies it to the root frame each draw, so one write per factor change suffices.
            long tmpl = ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectTemplateOff;
            if (RubyBallScaleCore &&
                Math.Abs(Memory.ReadFloat(tmpl + ShotEffectPool.EffectObjectScale) - factor) > 0.005f)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"RubyBall: applying core object scale {factor:F2}");
                for (int o = -1; o < ShotEffectPool.EffectSlotCount; o++)
                {
                    long obj = o < 0 ? tmpl
                        : ShotEffectPool.MainCharaEffectBase + ShotEffectPool.EffectSlotObjectsOff + (long)o * ShotEffectPool.EffectSlotStride;
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale,     factor);
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale + 4, factor);
                    Memory.WriteFloat(obj + ShotEffectPool.EffectObjectScale + 8, factor);
                }
            }

            // 3. Collision via BT radii — ⚠ CRASHES PCSX2 (live-bisected 2026-07-06: this lever alone,
            // one float write BT+0x2C 5.0→6.25, still Read Abort; mechanism unresolved — the live BT is a
            // runtime-built registry entry whose static content differs). Kept for reference, toggle OFF.
            // The shipping collision path is MaintainRubyOrbHitbox (enemy-body inflation — mathematically
            // equivalent: hit ⇔ dist < orbR + bodyR) driven from CustomRubyEffects.MobiusRingEffect.
            int btN = Memory.ReadInt(ShotEffectPool.MainCharaEffectBase + ShotEffectPool.BtShotPtrOff);
            if (RubyBallScaleCollision && IsRamPtr(btN))
            {
                long bt = Memory.ToMmu(btN);
                if (bt != _rubyBtAddr)                                         // new BT (element/char change) → capture originals
                {
                    _rubyBtAddr = bt; _rubyBtFactor = 1f;
                    for (int i = 0; i < ShotEffectPool.BtShotRadiiCount; i++)
                        _rubyBtOrig[i] = Memory.ReadFloat(bt + ShotEffectPool.BtShotRadiiOff + i * 4);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"RubyBall: BT 0x{bt:X} radii captured [{_rubyBtOrig[0]:F2}, {_rubyBtOrig[1]:F2}, {_rubyBtOrig[2]:F2}, {_rubyBtOrig[3]:F2}]");
                }
                if (Math.Abs(factor - _rubyBtFactor) > 0.005f)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"RubyBall: applying collision scale {factor:F2} (phases 1..3)");
                    for (int i = 1; i < ShotEffectPool.BtShotRadiiCount; i++) // skip phase 0 (held ball)
                        if (_rubyBtOrig[i] > 0f && _rubyBtOrig[i] < 100f)      // sane radii only; 0 = phase without a hit
                            Memory.WriteFloat(bt + ShotEffectPool.BtShotRadiiOff + i * 4, _rubyBtOrig[i] * factor);
                    _rubyBtFactor = factor;
                }
            }
        }

        // ── Ruby orb collision growth via enemy-body inflation (the SAFE collision path) ──
        // hit ⇔ dist(orb, part) < orbRadius + bodyRadius, so adding (factor−1)×orbRadius to every enemy
        // body radius is exactly equivalent to growing the orb's damage sphere — with none of the BT
        // side effects (wall checks stay native). Same machinery as the HC reach hitbox (own snapshot).
        static readonly float[,] _rubyOrbHitboxOrig = new float[16, BodyCollision.MaxBodyParts];
        static bool _rubyOrbHitboxActive;

        /// <summary>Inflate every active enemy's body radii to make Ruby's scaled orbs (<paramref name="factor"/>×)
        /// connect as if their damage sphere had grown. Call while the orbs are in flight; restore when they die.</summary>
        public static void MaintainRubyOrbHitbox(float factor)
        {
            float bonus = (factor - 1f) * ShotEffectPool.RubyOrbBaseRadius;
            if (bonus <= 0.01f || !Player.CheckDunIsWalkingMode()) return;
            _rubyOrbHitboxActive = true;
            for (int s = 0; s < 16; s++)
            {
                long slot = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slot + EnemySlotOffsets.RenderStatus) <= 0)  // empty slot → forget cached stock
                {
                    for (int p = 0; p < BodyCollision.MaxBodyParts; p++) _rubyOrbHitboxOrig[s, p] = 0f;
                    continue;
                }
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    long addr = BodyCollision.RadiusAddr(s, p);
                    float r = Memory.ReadFloat(addr);
                    float orig = _rubyOrbHitboxOrig[s, p];
                    if (orig <= 0.01f)                                          // capture stock the first time
                    {
                        if (r <= 0.01f || r >= 1000f) continue;                 // no real hitbox on this part
                        orig = _rubyOrbHitboxOrig[s, p] = r;
                    }
                    if (Math.Abs(r - (orig + bonus)) > 0.05f) Memory.WriteFloat(addr, orig + bonus);
                }
            }
        }

        /// <summary>Restore every body radius inflated by <see cref="MaintainRubyOrbHitbox"/> to its stock value.</summary>
        public static void RestoreRubyOrbHitbox()
        {
            if (!_rubyOrbHitboxActive) return;
            for (int s = 0; s < 16; s++)
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                    if (_rubyOrbHitboxOrig[s, p] > 0.01f)
                    {
                        Memory.WriteFloat(BodyCollision.RadiusAddr(s, p), _rubyOrbHitboxOrig[s, p]);
                        _rubyOrbHitboxOrig[s, p] = 0f;
                    }
            _rubyOrbHitboxActive = false;
        }

    }
}
