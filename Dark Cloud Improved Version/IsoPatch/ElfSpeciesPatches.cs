using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.MipsAsm;
using static Dark_Cloud_Improved_Version.ElfCaveWriter;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The species table's extension rows. MonstorTable (EnemySpeciesTable.TableBase, 167 × 0x9C) has no slack — BtEnemyLayout
    /// starts 12 bytes after it — so the mod's species are rows in the dead SmoothRest body (SmoothRestCave.SpeciesRows), and the
    /// table's one reader, CMonstorUnit::SetupBaseModel, forms <c>&amp;MonstorTable[model_no]</c> through the species-lookup stub
    /// (tools/stubs/species_lookup.s at SmoothRestCave.SpeciesLookup): the lui/addiu at 0x1DFEE0 become <c>jal stub; nop</c>, the
    /// addu after them stays. Layouts and scripts carry the index, so a mod species is a floor entry like any other.
    /// Each row starts as the vanilla record of the species it is modelled on, with its EnemyData fields written over it.</summary>
    internal static class ElfSpeciesPatches
    {
        private const uint HookSite = 0x001DFEE0;                                       // lui v0, %hi(MonstorTable); addiu v0, v0, %lo(MonstorTable)
        private static readonly uint[] HookVanilla = { 0x3C020028, 0x2442FB00 };
        private const int ModelName = 0x000, ScriptName = 0x040, NameBytes = 16;

        /// <summary>The Bomb Gemron (EnemySpecies.BombGemron): Holy Gemron's record under the mod's model and script stems, both shot
        /// slots re-pointed — <c>ringo_ex</c> (4) for its shot, <c>zibaku_f2</c> (16) for the death and self-destruct blasts (the
        /// outlaws' explosion), every element resistance 0.</summary>
        private static readonly (EnemyDefaults species, int template, string stem, ushort shot0, ushort shot1)[] Rows =
        {
            (EnemySpecies.BombGemron, EnemySpecies.GemronHoly.TableIndex.Value, ModSpeciesBakes.BombGemronStem, 4, 16),
        };

        internal static void PatchSpeciesExtension(FileStream fs, Func<uint, long> ElfOff)
        {
            byte[] stub = Embedded("speciesLookup.bin");
            if (stub.Length == 0 || (stub.Length & 3) != 0 || U32(stub, 0) != 0x28C200A7)   // first insn = slti v0, a2, 167
                throw new IOException($"speciesLookup.bin malformed ({stub.Length} B) or stale — reassemble its .s.");
            WriteBytes(fs, ElfOff, SmoothRestCave.SpeciesLookup, stub, SmoothRestCave.SpeciesRows, "speciesLookup.bin overruns into the species rows");
            ReplaceWords(fs, ElfOff, HookSite, HookVanilla, new[] { Jal(SmoothRestCave.SpeciesLookup), 0u }, "SetupBaseModel's MonstorTable address");

            int slots = (int)(SmoothRestCave.End - SmoothRestCave.SpeciesRows) / EnemySpeciesTable.Stride;
            if (Rows.Length > slots) throw new IOException($"{Rows.Length} extension rows, {slots} fit in the SmoothRest cave");
            var rows = new byte[slots * EnemySpeciesTable.Stride];
            for (int i = 0; i < Rows.Length; i++)
            {
                var (species, template, stem, shot0, shot1) = Rows[i];
                if (species.TableIndex != EnemySpeciesTable.VanillaCount + i) throw new IOException($"{species.Name}: TableIndex {species.TableIndex} is not extension row {i}");
                byte[] row = Rd(fs, ElfOff((uint)EnemySpeciesTable.RecordAddress(template)), EnemySpeciesTable.Stride);
                Fill(row, species, stem, shot0, shot1);
                Array.Copy(row, 0, rows, i * EnemySpeciesTable.Stride, row.Length);
            }
            WriteBytes(fs, ElfOff, SmoothRestCave.SpeciesRows, rows, SmoothRestCave.End, "the species rows overrun the SmoothRest cave");
        }

        /// <summary>The template record under the species' own names and EnemyData fields.</summary>
        private static void Fill(byte[] row, EnemyDefaults e, string stem, ushort shot0, ushort shot1)
        {
            Array.Clear(row, 0, 0x50);                                                  // model_name[4][16] + script_name[16]
            Name(row, ModelName, stem); Name(row, ScriptName, stem);
            U32(row, EnemySpeciesTable.MaxHp, (uint)e.MaxHp.Value);
            U16(row, EnemySpeciesTable.Category, (ushort)e.Category.Value);
            U16(row, EnemySpeciesTable.FireRes, e.FireRes.Value); U16(row, EnemySpeciesTable.IceRes, e.IceRes.Value);
            U16(row, EnemySpeciesTable.ThunderRes, e.ThunderRes.Value); U16(row, EnemySpeciesTable.WindRes, e.WindRes.Value);
            U16(row, EnemySpeciesTable.HolyRes, e.HolyRes.Value);
            WrF(row, EnemySpeciesTable.EntityScale, e.EntityScale.Value);
            U16(row, EnemySpeciesTable.DamageReduction, e.DamageReduction.Value); U16(row, EnemySpeciesTable.WeaponDefense, e.WeaponDefense.Value);
            U16(row, EnemySpeciesTable.PrimaryBstIndex, shot0); U16(row, EnemySpeciesTable.SecondaryBstIndex, shot1);
            U32(row, EnemySpeciesTable.Abs, (uint)e.Abs.Value); U32(row, EnemySpeciesTable.MinGoldDrop, (uint)e.MinGoldDrop.Value);
            U32(row, EnemySpeciesTable.DropChance, (uint)e.DropChance.Value);
            U16(row, EnemySpeciesTable.EnemySpeciesId, e.Id);
            U16(row, EnemySpeciesTable.StealItemId, e.StealItemId ?? 0xFFFF);
            U16(row, EnemySpeciesTable.ItemDamageRes, e.ItemDamageRes.Value); U16(row, EnemySpeciesTable.ItemStatusRes, e.ItemStatusRes.Value);
            U16(row, EnemySpeciesTable.RareDropItemId, e.RareDropItemId ?? 0xFFFF);
            WrF(row, EnemySpeciesTable.KnockbackMult, e.KnockbackMult.Value);
        }

        private static void Name(byte[] row, int at, string s)
        {
            byte[] b = System.Text.Encoding.ASCII.GetBytes(s);
            if (b.Length >= NameBytes) throw new IOException($"species name '{s}' does not fit a 16-byte field");
            Array.Copy(b, 0, row, at, b.Length);
        }
    }
}
