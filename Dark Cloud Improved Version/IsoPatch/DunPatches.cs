using System;
using System.IO;
using static Dark_Cloud_Improved_Version.IsoBytes;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Word patches to <c>dun.bin</c> — the dungeon overlay (disc root <c>DUN.BIN</c>, loaded at
    /// <see cref="LoadBase"/> every dungeon). Why the ISO and not runtime writes or the pnach: overlay code is
    /// HOT every frame (a PINE write to it crashes the recompiler — the ABS floor-load lesson) and reloads with
    /// every dungeon, and a pnach per-frame write must be gated because the town overlay shares these addresses.
    /// A baked word changes only this file, needs no gating and no app, and any data a patched load points at
    /// lives in the ELF cave segment with its default baked in (runtime-tunable through PINE).
    /// File offset of a word = record start + (RAM address − LoadBase): the overlay is a flat image.
    /// </summary>
    internal static class DunPatches
    {
        internal const uint LoadBase = 0x01DABD00;

        private sealed record Word(uint Addr, uint Orig, uint New, string What);

        private static readonly Word[] Words =
        {
            // Passive HEAL ability (weapon flag 0x800) cadence: heal tick compares its frame counter with
            // `slti v0,v0,0xF0` (240 f = 4 s, dun 0x1DB8234); 0xB4 = 180 f = 3 s for every HEAL weapon
            // (user 2026-09-09). Guardian Grace watches the counter wrap, so its sparkles/chime follow.
            new(0x01DB8234, 0x284200F0, 0x284200B4, "heal-ability cadence 4 s → 3 s"),
            // Xiao's attack-gauge refill multiplier (motionDrive: `lui v0,0x3fc0; mtc1 v0,f0`, v0 dead after)
            // → `lui v0,HI; lwc1 f0,LO(v0)` of the baked cave word ElfCave.ShieldGaugeRate (1.5 = vanilla).
            new(0x01DB8090, 0x3C023FC0, 0x3C020000u | (CodeCaves.ElfCave.ShieldGaugeRate >> 16),     "gauge refill multiplier → cave word (lui)"),
            new(0x01DB8094, 0x44820000, 0xC4400000u | (CodeCaves.ElfCave.ShieldGaugeRate & 0xFFFF),  "gauge refill multiplier → cave word (lwc1 f0)"),
        };

        /// <summary>The patched form of the first gauge word — what the runtime checks to know the patch is live.</summary>
        internal static uint GaugePatchedWord0 => Words[1].New;
        internal const long GaugePatchAddrMmu = 0x201DB8090;

        internal static void Apply(FileStream fs, Rec dun, Action<string> progress)
        {
            long baseOff = (long)dun.Ext * SectorBytes;
            int applied = 0;
            foreach (var w in Words)
            {
                uint rel = w.Addr - LoadBase;
                if (rel + 4 > dun.Size) throw new IOException($"dun.bin patch 0x{w.Addr:X} lies past the file ({w.What}).");
                long off = baseOff + rel;
                uint cur = RdU32(fs, off);
                if (cur == w.New) continue;                                   // already ours (re-patching a patched ISO)
                if (cur != w.Orig)
                    throw new IOException($"dun.bin @0x{w.Addr:X} is not vanilla ({cur:X8}, expected {w.Orig:X8}) — {w.What}. Unmodified Dark Cloud (USA) ISO expected.");
                WrU32(fs, off, w.New);
                applied++;
            }
            progress($"Patched dun.bin ({applied} word(s): heal cadence 3 s, gauge multiplier → cave word) …");
        }
    }
}
