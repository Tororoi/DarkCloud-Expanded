using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The customize-menu listener behind the EMPTY SynthSphere: socketed in a weapon's first attachment slot with nothing
    /// synthesised into it, it raises the hovered weapon to +5, the levels gained added to Attack, Endurance, Speed and Magic; taken
    /// out again, the weapon goes back to the level and stats it had. The two bookkeeping words that make the second half possible
    /// live in the record itself (<see cref="WeaponRecord.HasChangedBySynth"/>, <see cref="WeaponRecord.WeaponFormerStatsValue"/>).
    ///
    /// <see cref="Listener"/> is the thread: Dungeon.InsideDungeonThread and TownLoop start it when the weapon
    /// customize menu opens (a stopped one is replaced with a fresh thread first); <see cref="Listen"/> polls the hovered
    /// character and weapon every 64 ms until the menu closes.</summary>
    internal static class WeaponSynthSphereLevel
    {
        internal static Thread Listener = new Thread(new ThreadStart(Listen));

        private const int TargetLevel = 5;   // the "+5" an empty sphere raises a weapon to

        /// <summary>Thread body: while the weapon customize menu is open, <see cref="Apply"/> on the hovered weapon, 64 ms apart.</summary>
        public static void Listen()
        {
            while (Player.CheckIsWeaponCustomizeMenu())
            {
                int character = Memory.ReadByte(Addresses.weaponMenuCurrentCharacterHover);   // 0-5
                int weapon = Memory.ReadByte(Addresses.weaponMenuCurrentWeaponHover);         // 0-9
                if (character <= Player.OsmondId && weapon <= 9) Apply(character, weapon);
                Thread.Sleep(64);
            }
        }

        /// <summary>One weapon (<paramref name="character"/>'s bag slot <paramref name="slot"/>). An empty SynthSphere in attachment
        /// slot 1 with the weapon under +5 and not yet raised: set to +5, the difference added to the four main stats, the difference
        /// saved and the raised flag set. No empty sphere there, the weapon at +5 and the raised flag set: the saved difference taken
        /// back off the level and the four stats, both words cleared.</summary>
        private static void Apply(int character, int slot)
        {
            long At(int field) => WeaponRecord.Address(character, slot, field);

            //Store the current weapon base stats
            int attack = Memory.ReadUShort(At(WeaponRecord.Attack));
            int endurance = Memory.ReadUShort(At(WeaponRecord.Endurance));
            int speed = Memory.ReadUShort(At(WeaponRecord.Speed));
            int magic = Memory.ReadUShort(At(WeaponRecord.Magic));
            int hasChangedBySynth = Memory.ReadUShort(At(WeaponRecord.HasChangedBySynth));

            //Store the current weapon level and calculate the difference to +5
            int weaponLevel = Memory.ReadByte(At(WeaponRecord.Level));
            int diffLevel = TargetLevel - weaponLevel;

            //Has the empty synthsphere in socket?
            if (Memory.ReadUShort(At(WeaponRecord.Slot1ItemId)) == Items.synthsphere &&
                Memory.ReadUShort(At(WeaponRecord.Slot1SynthesisedItemId)) == 0)
            {
                //Weapon level is below +5 and has not yet been changed by an empty synthsphere?
                if (diffLevel > 0 && hasChangedBySynth == 0)
                {
                    //Set the weapon to +5 with the increase in main stats
                    Memory.WriteByte(At(WeaponRecord.Level), TargetLevel);
                    Memory.WriteUShort(At(WeaponRecord.Attack), (ushort)(attack + diffLevel));
                    Memory.WriteUShort(At(WeaponRecord.Endurance), (ushort)(endurance + diffLevel));
                    Memory.WriteUShort(At(WeaponRecord.Speed), (ushort)(speed + diffLevel));
                    Memory.WriteUShort(At(WeaponRecord.Magic), (ushort)(magic + diffLevel));

                    //Save former stat value
                    Memory.WriteUShort(At(WeaponRecord.WeaponFormerStatsValue), (ushort)diffLevel);

                    //Set changed flag
                    Memory.WriteUShort(At(WeaponRecord.HasChangedBySynth), 1);
                }
            }
            else if (diffLevel == 0 && hasChangedBySynth == 1)
            {
                //Fetch the previous level before the change
                int diffLevelBeforeChange = Memory.ReadUShort(At(WeaponRecord.WeaponFormerStatsValue));

                //Revert the weapons changes back to normal
                Memory.WriteUShort(At(WeaponRecord.Level), (ushort)(TargetLevel - diffLevelBeforeChange));
                Memory.WriteUShort(At(WeaponRecord.Attack), (ushort)(attack - diffLevelBeforeChange));
                Memory.WriteUShort(At(WeaponRecord.Endurance), (ushort)(endurance - diffLevelBeforeChange));
                Memory.WriteUShort(At(WeaponRecord.Speed), (ushort)(speed - diffLevelBeforeChange));
                Memory.WriteUShort(At(WeaponRecord.Magic), (ushort)(magic - diffLevelBeforeChange));

                //Reset flags
                Memory.WriteUShort(At(WeaponRecord.WeaponFormerStatsValue), 0);
                Memory.WriteUShort(At(WeaponRecord.HasChangedBySynth), 0);
            }
        }
    }
}
