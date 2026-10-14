using System;
using System.Text;
using static Dark_Cloud_Improved_Version.DivineBeastTitle;
using static Dark_Cloud_Improved_Version.CatSlot;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The cat's textures: the cat pack's entries, loaded into slot 1's texture group (block 0x21) in a VRAM span of their own
    /// (ElfCave.CatLoad), and the check that they are still there. One of the <see cref="DivineBeastTitle"/> classes, which share their
    /// members through using static.</summary>
    internal static class CatTextures
    {
        internal const short SlotTextureGroup = (short)(DungeonCharaDraw.CharaTexBase + Slot);
        /// <summary>The first of the cat pack's textures (CatPackBakes: c04cat01..05, catwing, catcape) — the one the check looks up.</summary>
        private const string CatTextureProbe = "c04cat01";
        internal static int _texCheckTick;

        /// <summary>A dungeon script event or a menu can rebuild the texture manager under the cat, and a floor load or a party switch
        /// forgets the load altogether. Notice either and stand the cat down; it loads again once play resumes.</summary>
        internal static void CheckTexturesStillOurs()
        {
            if (!StillLoaded()) { Log("the cat's load is gone (a floor load or a party switch) — standing down"); Despawn(); return; }
            long e = FindTexEntry(CatTextureProbe);
            uint w = Memory.ReadUInt(CodeCaves.CatLoad + CodeCaves.CatLoadVram);
            uint tbp = e == 0 ? 0u : (Memory.ReadUInt(e + TextureManager.EntryTex0) & TextureManager.Tex0AddrMask);
            if (e != 0 && Memory.ReadShort(e) == SlotTextureGroup && w != 0 && tbp >= w) return;
            Log((e == 0 ? "texture manager rebuilt (cat entries gone)" : $"texture manager rebuilt (cat entry at 0x{tbp:X}, block 0x{Memory.ReadShort(e):X}, its span 0x{w:X})") + " — reloading the cat");
            Despawn();
        }

        /// <summary>The manager entry for a texture, found by name (0 if absent): the whole entry table in one read.</summary>
        internal static long FindTexEntry(string name)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base));
            if (count <= 0) return 0;
            long table = TextureManager.Base + TextureManager.Entries;
            byte[] all = Memory.ReadBytesBatch(table, count * TextureManager.EntryStride);
            if (all == null) return 0;
            byte[] want = Encoding.ASCII.GetBytes(name);
            for (int i = 0; i < count; i++)
            {
                int o = i * TextureManager.EntryStride + TextureManager.EntryName;
                int k = 0;
                while (k < want.Length && all[o + k] == want[k]) k++;
                if (k == want.Length && all[o + k] == 0) return table + (long)i * TextureManager.EntryStride;
            }
            return 0;
        }
    }
}
