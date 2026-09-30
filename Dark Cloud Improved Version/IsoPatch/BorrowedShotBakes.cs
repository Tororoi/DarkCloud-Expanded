using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Effect containers made loadable through the dungeon's shot-effect pack under the `dun\effect` names no
    /// species uses. The pack's loader (Entry__12CSHOT_EFFECT) takes ONE name from a BT_SHOT_EFFECT config and uses it
    /// twice: `dun/effect/&lt;name&gt;.chr` is the archive file, `&lt;name&gt;.cfg` the record it then asks that container
    /// for — a container whose cfg record is named differently loads nothing. So each source container is copied onto
    /// the dead name with one record appended, `&lt;name&gt;.cfg` = the source's cfg payload, nothing else changed. The mod
    /// fires these with BorrowedShots.CustomConfig. A monster-side container (a boss companion under `dun\monstor`, whose
    /// cfg is `info.cfg`) is exposed IN PLACE — its own entry redirected to the copy with the record appended, which its
    /// boss keeps loading unchanged — and the mod names it with the `dun/monstor/` dir.</summary>
    internal static class BorrowedShotBakes
    {
        // name → (source container, the record to expose as <name>.cfg, the entry to redirect: null = dun\effect\<name>.chr,
        //         swapRG: the copy's palettes with red and green exchanged — pink → cyan, purple → blue — the source untouched,
        //         alpha: every palette entry's alpha scaled by this (1 = as authored; the sub-shot's opacity word does not reach a shot effect))
        private static readonly (string name, string source, string cfg, string target, bool swapRG, float alpha)[] Borrowed =
        {
            ("_b_boll",      @"dun\effect\_b_boll.chr",        "b_boll.cfg",   null, false, 1f),
            ("_f_boll",      @"dun\effect\_f_boll.chr",        "f_boll.cfg",   null, false, 1f),
            ("_f_boll_2",    @"dun\effect\_f_boll_2.chr",      "f_boll_2.cfg", null, false, 1f),
            ("_f_boll_3",    @"dun\effect\_f_boll_3.chr",      "f_boll_3.cfg", null, false, 1f),
            ("_i_boll",      @"dun\effect\_i_boll.chr",        "i_boll.cfg",   null, false, 1f),
            ("es_zone",      @"dun\effect\es_zone.chr",        "info.cfg",     null, false, 1f),
            // Babel's Spear's confusion area: the Dark Genie's small beam recoloured cyan/blue at 0.3 of its alpha, on the dead
            // zibaku_f name — the genie's own c17_beem_s entry keeps its pink.
            ("zibaku_f",     @"dun\monstor\c17_beem_s.chr",   "info.cfg",     null, true, 0.3f),
            // Hercules' Wrath's ultimate: a cutscene sparkle (s78's e508_ex) on the dead zibaku_r name.
            ("zibaku_r",     @"gedit\s78\chara\e508_ex.chr",  "e508_ex.cfg",  null, false, 1f),
            // The Cactus spike's rise: a cutscene poof of smoke (e03's e228ex) on the dead zibaku_t name.
            ("zibaku_t",     @"gedit\e03\chara\e228ex.chr",   "e228ex.cfg",   null, false, 1f),
        };

        /// <summary>Every palette in the container's `.img` banks recoloured (8-bit TIM2: the 256 × RGBA CLUT after the picture
        /// data): red and green exchanged when asked, every alpha scaled. The banks are rebuilt in place, so the image data and
        /// its swizzle are untouched.</summary>
        private static byte[] RecolourPalettes(byte[] container, bool swapRG, float alpha)
        {
            var pack = ChrPack.Parse(container);
            foreach (var rec in pack.Records)
            {
                if (!rec.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) continue;
                var bank = new CatPackBakes.Bank(rec.Payload);
                var items = new List<(string name, byte[] blob)>();
                foreach (var (tname, _) in bank.Entries)
                {
                    byte[] blk = (byte[])bank.Block(tname).Clone();
                    if (blk.Length > 0x40 && blk[0] == (byte)'T' && blk[1] == (byte)'I' && blk[2] == (byte)'M' && blk[3] == (byte)'2')
                    {
                        const int pic = 0x10;
                        int clutSz = BitConverter.ToInt32(blk, pic + 4), imgSz = BitConverter.ToInt32(blk, pic + 8);
                        int hdrSz = BitConverter.ToUInt16(blk, pic + 0x0C), colors = BitConverter.ToUInt16(blk, pic + 0x0E);
                        int clut = pic + hdrSz + imgSz;
                        if (colors == 256 && clutSz >= 1024 && clut + 1024 <= blk.Length)
                            for (int k = 0; k < 256; k++)
                            {
                                int o = clut + k * 4;
                                if (swapRG) (blk[o], blk[o + 1]) = (blk[o + 1], blk[o]);
                                blk[o + 3] = (byte)Math.Round(blk[o + 3] * alpha);
                            }
                    }
                    items.Add((tname, blk));
                }
                rec.ReplacePayload(CatPackBakes.Bank.Build(bank.Magic, items));
            }
            return pack.Rebuild();
        }

        /// <summary>The container with `&lt;name&gt;.cfg` appended (the payload of <paramref name="cfgRecord"/>), or null
        /// when it is there already.</summary>
        internal static byte[] Expose(byte[] container, string name, string cfgRecord, bool swapRG = false, float alpha = 1f)
        {
            if (swapRG || alpha != 1f) container = RecolourPalettes(container, swapRG, alpha);
            var pack = ChrPack.Parse(container);
            if (pack.Find(name + ".cfg") != null) return null;
            var src = pack.Find(cfgRecord) ?? throw new IOException($"{name}: no record {cfgRecord} in the source container");
            byte[] payload = src.Payload;
            pack.Records.Add(ChrRecord.Create(name + ".cfg", payload));
            byte[] outp = pack.Rebuild();
            var check = ChrPack.Parse(outp);
            if (check.Records.Count != pack.Records.Count || !check.Require(name + ".cfg").Payload.AsSpan().SequenceEqual(payload))
                throw new IOException($"{name}: the rebuilt container does not read back");
            return outp;
        }

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            int done = 0;
            foreach (var (name, source, cfg, explicitTarget, swapRG, alpha) in Borrowed)
            {
                string target = explicitTarget ?? $@"dun\effect\{name}.chr";
                byte[] fresh = Expose(arc.Read(source), name, cfg, swapRG, alpha);
                if (fresh == null) { log($"{target}: already exposes {name}.cfg — skipped"); continue; }
                arc.Redirect(target, fresh);
                done++;
            }
            log($"borrowed shot effects: {done} redirected");
        }
    }
}
