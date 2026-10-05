using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The dungeon quick-change menu as the weapon's element picker: D-pad UP opens the SELECT ring with the five element stones
    /// and a grey synth sphere for None in place of the party (ElfElementMenuPatches, tools/stubs/element_menu.s). The native side
    /// draws the cells, sets the live weapon's element on X and leaves the confirmed cell in <see cref="CodeCaves.ElementMenuPick"/>;
    /// this applies the rest: the weapon record's HUD element byte, the HUD tint and Ruby's armlet texture refresh.
    /// </summary>
    internal static class ElementMenu
    {
        private const string Tag = "[ElementMenu] ";
        internal const int None = 5;
        private static readonly string[] Names = { "Fire", "Ice", "Thunder", "Wind", "Holy", "None" };

        /// <summary>The HUD's element tint block (0x21E59450, 16 B) per element.</summary>
        private const long HudTint = 0x21E59450;
        private static readonly byte[][] HudTints =
        {
            new byte[] { 63, 15, 0, 63, 6, 1, 0, 63, 63, 15, 0, 31, 6, 1, 0, 31 },     // Fire
            new byte[] { 25, 50, 63, 63, 2, 5, 6, 63, 25, 50, 63, 31, 2, 5, 6, 31 },   // Ice
            new byte[] { 63, 63, 25, 63, 6, 6, 2, 63, 63, 63, 25, 31, 6, 6, 2, 31 },   // Thunder
            new byte[] { 34, 63, 47, 63, 3, 6, 4, 63, 34, 63, 47, 31, 3, 6, 4, 31 },   // Wind
            new byte[] { 56, 37, 43, 63, 5, 3, 4, 63, 56, 37, 43, 31, 5, 3, 4, 31 },   // Holy
            new byte[] { 64, 64, 64, 63, 6, 6, 6, 63, 64, 64, 64, 31, 6, 6, 6, 31 },   // None
        };

        /// <summary>Once a dungeon tick: a confirmed pick is applied and cleared.</summary>
        internal static void Tick()
        {
            int pick = Memory.ReadInt(CodeCaves.ElementMenuPick);
            if (pick <= 0) return;
            Memory.WriteInt(CodeCaves.ElementMenuPick, 0);
            if (pick - 1 > None) return;
            Apply(pick - 1);
        }

        /// <summary>The element (0..4, <see cref="None"/>) on the current character's equipped weapon: its record's HUD byte, the live
        /// battle copy, the HUD tint, and Ruby's armlet texture (the Mailbox flag her weapon thread watches).</summary>
        internal static void Apply(int element)
        {
            int character = Player.CurrentCharacterNum();
            int owner = character >= Player.ToanId && character <= Player.UngagaId ? character : Player.OsmondId;
            byte slot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr + character);
            Memory.WriteByte(WeaponRecord.Address(owner, slot, WeaponRecord.ElementHud), (byte)element);
            Memory.WriteByte(WeaponHave.BattleWeaponRecord + WeaponRecord.ElementHud, (byte)element);
            if (character == Player.RubyId) Memory.WriteByte(Mailbox.Element, 1);
            Memory.WriteByteArray(HudTint, HudTints[element]);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"element set to {Names[element]} (character {character}, slot {slot})");
        }
    }
}
