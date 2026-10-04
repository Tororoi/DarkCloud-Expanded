namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// One playable character's vitals in the status block — HP, max HP, defense, thirst, status ailment and the
    /// equipped bag slot — addressed by character id through the arrays in <see cref="PlayerAddresses"/>.
    /// <see cref="Player"/> holds one instance per character (<c>Player.Toan</c> .. <c>Player.Osmond</c>), reached
    /// as <c>Player.Xiao.GetHp()</c>.
    ///
    /// The address fields keep lower-case names (<c>Player.Xiao.status</c>, <c>Player.Goro.hp</c>, …) because
    /// weapon effects hand them around as raw addresses (Curse, Frozen Tuna, the cactus thirst drive, Scorpion
    /// Venom), including files owned by other jobs.
    /// </summary>
    internal sealed class Character
    {
        /// <summary>0 = Toan .. 5 = Osmond (<see cref="Player.ToanId"/> ..).</summary>
        internal readonly int Id;

        internal readonly int hp;                 // ushort
        internal readonly int maxHP;              // ushort
        internal readonly int defense;            // int
        internal readonly int thirst;             // float
        internal readonly int thirstMax;          // float
        internal readonly int status;             // ushort bitfield: 02 Near Death, 04 Freeze, 08 Stamina, 16 Poison, 32 Curse, 64 Goo
        internal readonly int statusTimer;        // ushort: frames left on the status
        internal readonly int currentWeaponSlot;  // byte: equipped bag slot 0-9

        internal Character(int id)
        {
            Id                = id;
            hp                = PlayerAddresses.HpArray          + id * PlayerAddresses.HpStride;
            maxHP             = PlayerAddresses.MaxHpArray       + id * PlayerAddresses.HpStride;
            defense           = PlayerAddresses.DefenseArray     + id * PlayerAddresses.WordStride;
            thirst            = PlayerAddresses.ThirstArray      + id * PlayerAddresses.WordStride;
            thirstMax         = PlayerAddresses.ThirstMaxArray   + id * PlayerAddresses.WordStride;
            status            = PlayerAddresses.StatusArray      + id * PlayerAddresses.WordStride;
            statusTimer       = PlayerAddresses.StatusTimerArray + id * PlayerAddresses.WordStride;
            currentWeaponSlot = (int)DngStatusData.EquippedSlotAddr(id);
        }

        public ushort GetHp() => Memory.ReadUShort(hp);

        public void SetHp(ushort newhp) => Memory.WriteUShort(hp, newhp);

        public ushort GetMaxHp() => Memory.ReadUShort(maxHP);

        public int GetDefense() => Memory.ReadInt(defense);

        public void SetDefense(int newdef) => Memory.WriteInt(defense, newdef);

        public float GetThirst() => Memory.ReadFloat(thirst);

        public float GetMaxThirst() => Memory.ReadFloat(thirstMax);

        /// <summary>The bag slot (0-9) this character has equipped.</summary>
        public byte GetWeaponSlot() => Memory.ReadByte(currentWeaponSlot);
    }
}
