using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb's item model (dun/item/main_data/bakudan) in the item-model cash (CashModel), for the copy BigBangShot hangs
    /// and throws.</summary>
    internal static class BombModel
    {
        internal const int WeaponPassBlock = CashModel.WeaponPassBlock, MainEffectBlock = CashModel.MainEffectBlock;
        private static readonly CashModel M = new CashModel("[BombModel] ", ItemModels.BombItemId, "bomb",
            () => (GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.mds"), GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.img")));
        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        internal static void KeepTextures(int block = WeaponPassBlock) => M.KeepTextures(block);
        internal static void Tick() => M.Tick();
        internal static void ReleaseTextures() => M.ReleaseTextures();
    }

    /// <summary>The item bomb's blast VISUAL, by data — what SetBomb writes into a free CItemBombEffect slot (five sprites
    /// that spread and fade, sized by the slot's scale), the bomb's own sound, and the blast ring (CShockWave): the game
    /// spawns it for a blast bigger than 1× at 30 × scale across and 15 × scale high; <paramref name="ringRadius"/> sets
    /// its reach instead (0 = no ring, negative = the game's rule). Nothing of the bomb's collision: the damage is the
    /// caller's (BlastFalloff.PlantFalloff).</summary>
}
