using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Helpers shared by everything that fabricates CSHOT_EFFECT sub-shots (the Guardian Reflector's re-fires,
    /// the borrowed shots).</summary>
    internal static class ShotEffects
    {
        /// <summary>Point a sub-shot's effect model along its direction of travel (Z = travel, X = up × Z).</summary>
        internal static void FaceAlong(long obj, float dx, float dh, float dy)
        {
            float len = (float)Math.Sqrt(dx * dx + dh * dh + dy * dy);
            if (len < 1e-4f) return;
            uint frame = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
            if (!Memory.IsValidGuest(frame)) return;
            float zx = dx / len, zh = dh / len, zy = dy / len;          // Z = direction of travel
            float xx, xh, xy;                                            // X = up × Z (up = +height)
            if (Math.Abs(zh) > 0.99f) { xx = 1f; xh = 0f; xy = 0f; }     // near-vertical: any horizontal X
            else
            {
                xx = zy; xh = 0f; xy = -zx;                              // (0,1,0) × (zx,zh,zy)
                float xl = (float)Math.Sqrt(xx * xx + xy * xy);
                xx /= xl; xy /= xl;
            }
            float yx = zh * xy - zy * xh, yh = zy * xx - zx * xy, yy = zx * xh - zh * xx;   // Y = Z × X
            long f = Memory.ToMmu(frame);
            var m = new byte[0x30];
            BitConverter.GetBytes(xx).CopyTo(m, 0x00); BitConverter.GetBytes(xh).CopyTo(m, 0x04); BitConverter.GetBytes(xy).CopyTo(m, 0x08);
            BitConverter.GetBytes(yx).CopyTo(m, 0x10); BitConverter.GetBytes(yh).CopyTo(m, 0x14); BitConverter.GetBytes(yy).CopyTo(m, 0x18);
            BitConverter.GetBytes(zx).CopyTo(m, 0x20); BitConverter.GetBytes(zh).CopyTo(m, 0x24); BitConverter.GetBytes(zy).CopyTo(m, 0x28);
            Memory.WriteBytesBatch(f + CFrameVu1.LocalMatrix, m);
            Memory.WriteInt(f + CFrameVu1.WorldCacheA, 0);
        }
    }
}
