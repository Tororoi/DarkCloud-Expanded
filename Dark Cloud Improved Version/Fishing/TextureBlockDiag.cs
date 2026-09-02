using System;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Diagnostic dump of the CTextureManager block table (72 blocks, 0x3C stride) for the Brownboo
    /// stilts-texture investigation (memory: fishing-stilts-texture-block). Two dumps per visit — a
    /// BASELINE at spot install (pre-fishing scene state) and one at fishing-session start (after
    /// _LOAD_FISHING_DATA ran) — so the diff shows exactly which block descriptors the fishing load
    /// stole and which blocks are free. Brownboo-only, once per phase, ~80 log lines each.
    /// </summary>
    internal static class TextureBlockDiag
    {
        internal static bool Enabled = false;   // stilts investigation CLOSED (PatchStiltsHeal shipped) — re-arm for future block-table forensics

        private const long Manager    = 0x21C75870;      // CTextureManager instance
        private const long BlocksOff  = 0x18;            // first block struct
        private const int  BlockSize  = 0x3C;            // per-block stride (loaded flag @ +0x28)
        private const int  BlockCount = 0x48;            // blocks 0x00..0x47
        private const long FishTexB   = 0x202A2B50;      // fishing texture block index global

        private static string _dumpedTag;

        /// <summary>Dump every block's raw descriptor words once per (visit, tag).</summary>
        internal static void Dump(string tag)
        {
            if (!Enabled || _dumpedTag == tag) return;
            _dumpedTag = tag;

            int fishTexb = Memory.ReadInt(FishTexB);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[TexBlocks] ===== {tag} ===== fish_texb=0x{fishTexb:X2} (mgr 0x{Manager:X})");
            for (int n = 0; n < BlockCount; n++)
            {
                long b = Manager + BlocksOff + n * BlockSize;
                var sb = new StringBuilder();
                for (int o = 0; o < BlockSize; o += 4)
                    sb.Append(Memory.ReadUInt(b + o).ToString("X8")).Append(o + 4 < BlockSize ? " " : "");
                bool allZero = true;                             // dump every non-empty block, loaded or not
                for (int o = 0; o < BlockSize && allZero; o += 4) allZero = Memory.ReadUInt(b + o) == 0;
                if (allZero) continue;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[TexBlocks] blk 0x{n:X2} @0x{b:X}: {sb}");
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[TexBlocks] ===== {tag} end =====");
        }

        internal static void Reset() => _dumpedTag = null;   // re-arm on town change
    }
}
