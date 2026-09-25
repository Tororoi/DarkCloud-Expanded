using System;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Toan's own glow discs, appended to his dungeon pack. A primed charge lights him while it is held, drawn by
    /// the game's own torch routine through the Divine Beast cat's glow cave (ElfCave.CatGlowDraw) — but that cave draws a
    /// texture BY NAME, and the cat's disc lives in XIAO's pack, so nothing of it is resident when Toan is the active
    /// character. This gives him discs of his own: <see cref="GlowName"/> in the Angel Gear cat's GOLD for Sun Sword's
    /// Solar Flash, <see cref="BlueName"/> in the Divine Beast Title cat's BLUE for Big Bang, and <see cref="ZeusName"/>,
    /// a black-cored RED RING, for the Sword of Zeus's judgement blade.
    ///
    /// Each is the Gallery of Time's torch glow re-tinted through the same builder the cat's disc uses
    /// (<see cref="CatPackBakes.GlowT8Tim2"/>) with that ramp (<see cref="CatPackBakes.GlowGold"/>,
    /// <see cref="CatPackBakes.GlowBlue"/>) resting in its CLUT. ⚠ The names must NOT be the cat's: the palette cave
    /// (tools/stubs/cat_glow_palette.s) finds its target by matching "catglowp", so a disc under any other name is never
    /// repainted and keeps its baked colour for good — which is why a second colour is a second DISC here rather than a
    /// palette row, and why neither needs a cave change. One 8-bit 64×64 disc is ~5 KB, so his pack grows by that per disc.
    /// Idempotent: a disc already in the bank in exactly its colour is left alone.
    /// </summary>
    internal static class ToanGlowBakes
    {
        internal const string HostChr  = @"dun\mainchara\c01d.chr";
        internal const string HostImg  = "c01d01_dun.img";   // his texture bank (Xiao's is c04b01.img)
        internal const string Template = "c01d01";           // an 8-bit TIM2 with a 0x30 picture header in that bank — the headers the disc is built on
        internal const string GlowName = "toanglow";    // Sun Sword — the Angel Gear cat's gold
        internal const string BlueName = "toanglowb";   // Big Bang — the Divine Beast Title cat's blue
        internal const string ZeusName = "toanglowz";   // Sword of Zeus — a black core out to a deep red edge (CatPackBakes.GlowZeus)

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
            var ramps = new[]
            {
                (name: GlowName, ramp: CatPackBakes.GlowGold),
                (name: BlueName, ramp: CatPackBakes.GlowBlue),
                (name: ZeusName, ramp: CatPackBakes.GlowZeus),
            };
            var discs = ramps.Select(d => (d.name, bytes: CatPackBakes.GlowT8Tim2(bank.Block(Template), light, d.ramp.core, d.ramp.outer))).ToArray();
            bool Present(string name) => bank.Entries.Any(e => e.name == name);
            byte[] Fresh(string name) => discs.FirstOrDefault(d => d.name == name).bytes;

            // Every disc already there AND already its colour: nothing to do. One there in a DIFFERENT colour (its ramp
            // was re-authored): replace it, rather than skipping and leaving the old palette baked in.
            if (discs.All(d => Present(d.name) && bank.Block(d.name).AsSpan().SequenceEqual(d.bytes)))
            { log($"Toan's glow discs already in {HostChr} — skipped"); return; }

            var items = bank.Entries.Select(e => (e.name, Fresh(e.name) ?? bank.Block(e.name))).ToList();
            foreach (var d in discs) if (!Present(d.name)) items.Add((d.name, d.bytes));
            imgRec.ReplacePayload(CatPackBakes.Bank.Build(bank.Magic, items));
            byte[] outp = pack.Rebuild();
            arc.Redirect(HostChr, outp);
            string what = string.Join(", ", discs.Select(d => $"`{d.name}` {(Present(d.name) ? "recoloured" : "added")}"));
            log($"Toan's glow discs in {HostChr}: {what} ({discs[0].bytes.Length:N0} B each); {items.Count} textures, pack {host.Length:N0} -> {outp.Length:N0} B");
        }
    }
}
