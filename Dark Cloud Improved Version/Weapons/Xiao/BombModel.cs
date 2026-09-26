using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb's item model (dun/item/main_data/bakudan), loaded into the game's own item-model cash so that a
    /// copy of it can be hung and thrown (BombShot). The game loads an active item's model from the item menu — the two
    /// files into the menu's read buffer, then SetCashModel, which allocates, uploads the texture block and builds the
    /// frames — and that is exactly what is done here, from the mod: the files read off the ISO and written into that
    /// buffer (free while no menu is open), SetCashModel called through the call-request cave. The cash is emptied on a
    /// floor change, so the root is checked before every use and reloaded when it is gone.</summary>
    internal static class BombModel
    {
        private const string Tag = "[BombModel] ";
        private static uint _root;                                 // the cash root last loaded (0 = none)
        private static int  _cash = -1;
        private static DateTime _lastTry;

        /// <summary>The bomb model's root frame (guest), loading it if the cash no longer holds it; 0 when it cannot be had.</summary>
        internal static uint Root()
        {
            uint self = Memory.ReadGuestPtr(ItemModels.ItemModelPtr);
            if (!Memory.IsValidGuest(self)) return 0;
            long m = Memory.ToMmu(self);
            if (_cash >= 0 && Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + _cash * 4) == _root
                && Memory.ReadInt(m + ItemModels.CashItemOffset + _cash * 4) == ItemModels.BombItemId && Memory.IsValidGuest(_root)) return _root;
            // Already in the cash (an active-item Bomb, or a load of ours the bookkeeping lost)?
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == ItemModels.BombItemId) { _root = r; _cash = i; return r; }
            }
            if ((DateTime.UtcNow - _lastTry).TotalSeconds < 2) return 0;              // a failed load is not retried every tick
            _lastTry = DateTime.UtcNow;
            return Load(self, m);
        }

        private static uint Load(uint self, long m)
        {
            byte[] mds = GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.mds");
            byte[] img = GameDataFiles.TryReadEntry(@"dun\item\main_data\bakudan.img");
            if (mds == null || img == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "bakudan.mds/.img not readable from the ISO"); return 0; }
            uint buf = Memory.ReadGuestPtr(ItemModels.MenuBufferPtr);
            if (!Memory.IsValidGuest(buf) || mds.Length > ItemModels.MenuBufferImgOffset) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no menu read buffer (0x{buf:X}) or the model is too big ({mds.Length} B)"); return 0; }
            if (Player.CheckDunIsPausedOrMenu()) return 0;                                 // the buffer is the menu's while one is open
            Memory.WriteBytesBatch(Memory.ToMmu(buf), mds);
            Memory.WriteBytesBatch(Memory.ToMmu(buf) + ItemModels.MenuBufferImgOffset, img);
            if (!NativeCall.Invoke(ItemModels.SetCashModel, out _, self, (uint)ItemModels.BombItemId, buf, buf + (uint)ItemModels.MenuBufferImgOffset, (uint)img.Length))
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "SetCashModel was not called — is the call-request cave in this ISO?"); return 0; }
            for (int i = 0; i < ItemModels.CashCount; i++)
            {
                uint r = Memory.ReadGuestPtr(m + ItemModels.CashRootOffset + i * 4);
                if (Memory.IsValidGuest(r) && Memory.ReadInt(m + ItemModels.CashItemOffset + i * 4) == ItemModels.BombItemId)
                {
                    _root = r; _cash = i;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bomb model loaded into cash {i}: root 0x{r:X} (mds {mds.Length} B, img {img.Length} B)");
                    return r;
                }
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "SetCashModel ran but no cash entry holds the bomb (the cash is full?)");
            return 0;
        }

        /// <summary>Forget the load (a floor change empties the cash).</summary>
        internal static void Forget() { _root = 0; _cash = -1; }
    }

    /// <summary>The item bomb's blast VISUAL, by data — what SetBomb writes into a free CItemBombEffect slot (five sprites
    /// that spread and fade, sized by the slot's scale), the bomb's own sound, and the blast ring for a blast bigger than
    /// 1×. Nothing of the bomb's collision: the damage is the caller's (BigBang.PlantFalloff).</summary>
    internal static class BombFx
    {
        internal static bool Spawn(float x, float h, float y, float scale)
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
            if (scale > 1f)
            {
                uint sw = Memory.ReadGuestPtr(ItemModels.ShockWavePtr);
                if (Memory.IsValidGuest(sw))
                {
                    long w = Memory.ToMmu(sw);
                    Memory.WriteVec3 (w, x, h, y); Memory.WriteFloat(w + 0xC, 1f);
                    Memory.WriteFloat(w + 0x10, scale * 30f); Memory.WriteFloat(w + 0x14, scale * 30f);
                    Memory.WriteInt  (w + 0x18, 0); Memory.WriteFloat(w + 0x1C, scale * 15f);
                    Memory.WriteInt  (w + 0x20, 0); Memory.WriteInt(w + 0x24, 0); Memory.WriteInt(w + 0x28, 1);
                }
            }
            return true;
        }
    }
}
