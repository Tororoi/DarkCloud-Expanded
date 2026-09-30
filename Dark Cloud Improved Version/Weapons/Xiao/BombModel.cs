using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb's item model (dun/item/main_data/bakudan) in the item-model cash (CashModel), for the copy BombShot hangs
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
    /// caller's (BigBang.PlantFalloff).</summary>
    internal static class BombFx
    {
        internal static bool Spawn(float x, float h, float y, float scale, float ringRadius = -1f)
        {
            uint fx = Memory.ReadGuestPtr(ItemModels.BombEffectPtr);
            if (!Memory.IsValidGuest(fx)) return false;
            long b = 0;
            for (int s = 0; s < ItemModels.BombSlots && b == 0; s++)
            {
                long cand = Memory.ToMmu(fx) + s * ItemModels.BombSlotStride;
                bool free = true;
                for (int i = 0; i < 5; i++) if (Memory.ReadInt(cand + 0xA0 + i * 4) != 0) { free = false; break; }
                if (free) b = cand;
            }
            if (b == 0) return false;
            for (int i = 0; i < 5; i++)
            {
                Memory.WriteVec3 (b + i * 0x10, x, h, y);
                Memory.WriteFloat(b + i * 0x10 + 0xC, 1f);
                Memory.WriteInt  (b + 0x50 + i * 4, 0);
                Memory.WriteInt  (b + 0x64 + i * 4, i * -3);
                Memory.WriteFloat(b + 0x8C + i * 4, 128f);
                Memory.WriteFloat(b + 0x78 + i * 4, 20f);
                Memory.WriteInt  (b + 0xA0 + i * 4, 1);
            }
            Memory.WriteFloat(b + 0xB4, scale);
            Memory.WriteInt  (b + 0x50, 2);
            Memory.WriteInt  (b + 0x54, 1);
            SeSeq.Play(ItemModels.BombSe, 90);
            float ring = ringRadius < 0f ? (scale > 1f ? scale * 30f : 0f) : ringRadius;
            if (ring > 0f)
            {
                uint sw = Memory.ReadGuestPtr(ItemModels.ShockWavePtr);
                if (Memory.IsValidGuest(sw))
                {
                    long w = Memory.ToMmu(sw);
                    Memory.WriteVec3 (w, x, h, y); Memory.WriteFloat(w + 0xC, 1f);
                    Memory.WriteFloat(w + 0x10, ring); Memory.WriteFloat(w + 0x14, ring);
                    Memory.WriteInt  (w + 0x18, 0); Memory.WriteFloat(w + 0x1C, ring * 0.5f);
                    Memory.WriteInt  (w + 0x20, 0); Memory.WriteInt(w + 0x24, 0); Memory.WriteInt(w + 0x28, 1);
                }
            }
            return true;
        }
    }
}
