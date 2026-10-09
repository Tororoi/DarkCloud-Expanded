using System;
using System.Collections.Generic;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The species table's extension rows: the records of the mod's enemy species (EnemySpecies.BombGemron …), index 167
    /// on. MonstorTable (EnemySpeciesTable.TableBase, 167 × 0x9C) has no slack and its page holds executed code the mod could
    /// never write beside, so the rows live at CodeCaves.SpeciesRows in the mod's data page, baked into the ISO as the front page of
    /// the ELF's cave segment (ElfSpeciesPatches; <see cref="Ensure"/> rewrites them in play only if they are not there), and the
    /// table's one reader, SetupBaseModel, reaches them through the species-lookup stub. Each row is the vanilla record of the
    /// species it is modelled on with its EnemyData fields written over it.</summary>
    internal static class SpeciesRows
    {
        private const string Tag = "[SpeciesRows] ";
        private const int ModelName = 0x000, ScriptName = 0x040, NameBytes = 16;

        /// <summary>The Bomb Gemron: Holy Gemron's record under the mod's model and script stems, every element resistance 0, its two
        /// shots the item-bomb shots of ElfSpeciesPatches.PatchBombConfigs — config 8 the thrown bomb, config 27 the death and
        /// self-destruct blast. The Crystal Gemron: Holy Gemron's record under its stems, Holy Gemron's own shots (null keeps the
        /// template's).</summary>
        internal static readonly (EnemyDefaults species, int template, string stem, ushort? shot0, ushort? shot1)[] Rows =
        {
            (EnemySpecies.BombGemron, EnemySpecies.GemronHoly.TableIndex.Value, ModSpeciesBakes.BombGemronStem, 8, 27),
            (EnemySpecies.CrystalGemron, EnemySpecies.GemronHoly.TableIndex.Value, CrystalGemronBake.Stem, null, null),
        };

        /// <summary>Every reserved row's bytes, the unused rows zero; <paramref name="template"/> reads a vanilla record by index.</summary>
        internal static byte[] Build(Func<int, byte[]> template)
        {
            if (Rows.Length > CodeCaves.SpeciesRowCount) throw new IOException($"{Rows.Length} extension rows, {CodeCaves.SpeciesRowCount} reserved at CodeCaves.SpeciesRows");
            var rows = new byte[CodeCaves.SpeciesRowCount * EnemySpeciesTable.Stride];
            for (int i = 0; i < Rows.Length; i++)
            {
                var (species, tpl, stem, shot0, shot1) = Rows[i];
                if (species.TableIndex != EnemySpeciesTable.VanillaCount + i) throw new IOException($"{species.Name}: TableIndex {species.TableIndex} is not extension row {i}");
                byte[] row = template(tpl);
                Fill(row, species, stem, shot0, shot1);
                Array.Copy(row, 0, rows, i * EnemySpeciesTable.Stride, row.Length);
            }
            return rows;
        }

        /// <summary>The rows in place in the running game, written when they are not (the patched ISO has them there since boot).</summary>
        internal static void Ensure()
        {
            byte[] want = Build(ti => Memory.ReadBytesBatch(EnemySpeciesTable.RecordAddress(ti), EnemySpeciesTable.Stride));
            byte[] have = Memory.ReadBytesBatch(CodeCaves.SpeciesRows, want.Length);
            if (have != null && have.AsSpan().SequenceEqual(want)) return;
            Memory.WriteBytesBatch(CodeCaves.SpeciesRows, want);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{Rows.Length} species row(s) written at 0x{CodeCaves.SpeciesRows:X}");
        }

        /// <summary>The template record under the species' own names and EnemyData fields.</summary>
        private static void Fill(byte[] row, EnemyDefaults e, string stem, ushort? shot0, ushort? shot1)
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
            if (shot0 != null) U16(row, EnemySpeciesTable.PrimaryBstIndex, shot0.Value);
            if (shot1 != null) U16(row, EnemySpeciesTable.SecondaryBstIndex, shot1.Value);
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
