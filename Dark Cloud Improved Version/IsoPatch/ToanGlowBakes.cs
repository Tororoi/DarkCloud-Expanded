using System;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Toan's own glow disc, appended to his dungeon pack. Solar Flash lights him white while the charge is held, drawn by
    /// the game's own torch routine through the Divine Beast cat's glow cave (ElfCave.CatGlowDraw) — but that cave draws a
    /// texture BY NAME, and the cat's disc lives in XIAO's pack, so nothing of it is resident when Toan is the active
    /// character. This gives him a disc of his own.
    ///
    /// It is the Gallery of Time's torch glow re-tinted through the same builder the cat's disc uses
    /// (<see cref="CatPackBakes.GlowT8Tim2"/>) with the Angel Gear cat's GOLD ramp (<see cref="CatPackBakes.GlowGold"/>)
    /// resting in its CLUT. ⚠ The name must NOT be the cat's: the palette cave (tools/stubs/cat_glow_palette.s) finds its
    /// target by matching "catglowp", so a disc under any other name is never repainted and keeps its baked white for good
    /// — no cave change and no tenth palette table. One 8-bit 64×64 disc is ~5 KB, so his pack grows by that much.
    /// Idempotent: the disc's presence in the bank is the check.
    /// </summary>
    internal static class ToanGlowBakes
    {
        internal const string HostChr  = @"dun\mainchara\c01d.chr";
        internal const string HostImg  = "c01d01_dun.img";   // his texture bank (Xiao's is c04b01.img)
        internal const string Template = "c01d01";           // an 8-bit TIM2 with a 0x30 picture header in that bank — the headers the disc is built on
        internal const string GlowName = "toanglow";

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            byte[] host = arc.Read(HostChr);
            var pack = ChrPack.Parse(host);
            var imgRec = pack.Find(HostImg) ?? throw new IOException($"{HostChr} lacks {HostImg}");
            var bank = new CatPackBakes.Bank(imgRec.Payload);
            if (!bank.Entries.Any(e => e.name == Template)) throw new IOException($"{HostImg} lacks {Template}, the picture the disc borrows its headers from");

            var fire = ChrPack.Parse(arc.Read(CatPackBakes.GlowSrc)).Find("fire.img")
                       ?? throw new IOException("glow source pack lacks fire.img");
            byte[] light = new CatPackBakes.Bank(fire.Payload).Block("lightling");
            var ramp = CatPackBakes.GlowGold;
            byte[] disc = CatPackBakes.GlowT8Tim2(bank.Block(Template), light, ramp.core, ramp.outer);

            // Already there AND already this colour: nothing to do. Already there in a DIFFERENT colour (the ramp was
            // re-authored): replace it, rather than skipping and leaving the old palette baked in.
            bool present = bank.Entries.Any(e => e.name == GlowName);
            if (present && bank.Block(GlowName).AsSpan().SequenceEqual(disc)) { log($"Toan's glow disc already in {HostChr} — skipped"); return; }

            var items = bank.Entries.Select(e => (e.name, e.name == GlowName ? disc : bank.Block(e.name))).ToList();
            if (!present) items.Add((GlowName, disc));
            imgRec.ReplacePayload(CatPackBakes.Bank.Build(bank.Magic, items));
            byte[] outp = pack.Rebuild();
            arc.Redirect(HostChr, outp);
            log($"Toan's glow disc `{GlowName}` ({disc.Length:N0} B) {(present ? "recoloured in" : "added to")} {HostChr}: {items.Count} textures, pack {host.Length:N0} -> {outp.Length:N0} B");
        }
    }
}
