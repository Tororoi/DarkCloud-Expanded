using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A borrowed effect's sub-shot faded by the mod: every unlit frame of its model (SetFrameAttr `c`: +0xC4 = 1) drawn
    /// at <c>k</c> × its constant colour (CFrame +0xD0..+0xDC, 128) — for the additive meshes effects are made of, its opacity
    /// (the sub-shot's opacity word does not reach a shot effect's draw, and a material-alpha track would overwrite the
    /// material). Walks the model tree from the sub-shot's root (+0xBC) by its child (+0x138) / sibling (+0x13C) links.</summary>
    internal static class SubShotFade
    {
        private const float UnlitFull = 128f;

        /// <summary>The sub-shot object at <paramref name="obj"/> (ShotEffectPack.OffObj + index × ObjStride) at
        /// <paramref name="k"/> 0..1.</summary>
        internal static void Set(long obj, float k)
        {
            uint root = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
            if (Memory.IsValidGuest(root)) Fade(Memory.ToMmu(root), UnlitFull * k, 0);
        }

        private static void Fade(long node, float v, int depth)
        {
            if (depth > 16) return;
            byte[] q = new byte[16];
            for (int i = 0; i < 4; i++) BitConverter.GetBytes(v).CopyTo(q, i * 4);
            for (long n = node; ; )
            {
                if (Memory.ReadByte(n + CFrameVu1.UnlitFlag) != 0) Memory.WriteBytesBatch(n + CFrameVu1.UnlitColourR, q);
                uint c = Memory.ReadGuestPtr(n + CFrameVu1.RootChild);
                if (Memory.IsValidGuest(c)) Fade(Memory.ToMmu(c), v, depth + 1);
                uint s = Memory.ReadGuestPtr(n + CFrameVu1.RootSibling);
                if (!Memory.IsValidGuest(s) || depth == 0) break;                  // the root has no siblings of its own
                n = Memory.ToMmu(s);
            }
        }
    }
}
