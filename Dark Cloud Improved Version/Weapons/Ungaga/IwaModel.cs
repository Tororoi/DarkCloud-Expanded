using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The boulder Master Utan throws in his cutscene (gedit\s96\chara\iwa.chr) in the item-model cash (CashModel), for
    /// the Terra Sword's falling rock (TerraSword). Both files are built from the pack once a session, off the ISO:
    ///  · the MODEL is iwa.mds whole (5,968 B): one rigid mesh `iwa`, its root at the rock's centre, ±20.4 on every axis;
    ///  · its TEXTURE is d02b10 of iwa.img — 256×256 8-bit (66.6 KB, more than a cash entry's 40,016-byte allocator takes with the
    ///    model), so the bank handed to the cash holds a stand-in resampled to <see cref="TexSize"/>² (the Bomb's own size), CLUT
    ///    kept, row-major under the IMG magic (the source bank is IM2: PSMT8 block order, un-swizzled); once loaded, the entry is
    ///    pointed at the full 256² picture kept outside the cash (<see cref="CashModel.FullTexture"/>).</summary>
    internal static class IwaModel
    {
        private const string Tag = "[IwaModel] ";
        internal const int  CashKey = 30001;                         // the cash entry's label: no item has this id (the palm is 30000)
        private const int   TexSize = 128;
        private const string Pack = @"gedit\s96\chara\iwa.chr", Model = "iwa.mds", Bank = "iwa.img", Texture = "d02b10";
        /// <summary>The rock's radius about its root (the mesh's extent, every axis).</summary>
        internal const float Radius = 20.4f;

        private static readonly CashModel M = new CashModel(Tag, CashKey, "rock", Files) { FullTexture = Full };
        private static byte[] _mds, _img;
        private static bool _built, _failed;

        internal static uint Root() => M.Root();
        internal static void Forget() => M.Forget();
        /// <summary>The rock's shadow: a flat disc of the rock's radius (<see cref="ShadowDisc"/>) loaded as a SHADOW model (CVisualShadow,
        /// in the rock's own cash entry), drawn straight down under it. 0 when it cannot be had.</summary>
        internal static uint ShadowRoot() { var (mds, _) = Files(); return mds == null ? 0 : M.ShadowRoot(_disc ??= ShadowDisc(mds), ShadowNeedUnits); }
        private static byte[] _disc;
        private const int DiscSegments = 16;
        // Room asked of the allocator: ~96 B (6 units) of shadow VU data per triangle (both faces), and 2 KB for the frame, the
        // visual and the batches' headers. The rock's 128² texture and its model leave ~445 units in its entry.
        private const int ShadowNeedUnits = DiscSegments * 2 * 6 + 0x80;

        /// <summary>A one-node MDS of a flat disc, radius <see cref="Radius"/>, in the rock's own frame (iwa.mds's header and node,
        /// its MDT rebuilt): a centre and DiscSegments rim points at y 0, a triangle fan wound both ways (one face survives whichever
        /// way the shadow program culls), one UV and one up normal, the rock's materials kept.</summary>
        private static byte[] ShadowDisc(byte[] mds)
        {
            const int NodeEnd = 0x80;                                                   // MDS header 0x10 + one 0x70 node; the MDT follows
            var m = MdtCarve.MdtParse(mds, NodeEnd);
            m.pos = new List<float[]> { new[] { 0f, 0f, 0f, 1f } };
            for (int i = 0; i < DiscSegments; i++)
            {
                double a = 2 * Math.PI * i / DiscSegments;
                m.pos.Add(new[] { (float)(Math.Sin(a) * Radius), 0f, (float)(Math.Cos(a) * Radius), 1f });
            }
            m.uv = new List<float[]> { new[] { 0f, 0f, 0f, 0f } };
            m.norm = new List<float[]> { new[] { 0f, 1f, 0f, 0f } };
            m.hasCol = false; m.col = null;
            int stride = 3;
            var recs = new List<int[]>();
            void Tri(int a, int b, int c) { foreach (int v in new[] { a, b, c }) { var r = new int[stride]; r[0] = v; recs.Add(r); } }
            for (int i = 0; i < DiscSegments; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % DiscSegments;
                Tri(0, a, b); Tri(0, b, a);
            }
            m.subs = new List<(int prim, int mat, List<int[]> recs)> { (3, 0, recs) };
            byte[] mdt = MdtCarve.MdtBuild(m);
            IsoBytes.U32(mdt, 0x14, (uint)m.uv.Count);                                  // the count words MdtBuild keeps verbatim
            IsoBytes.U32(mdt, 0x2C, (uint)m.norm.Count);
            var outb = new byte[NodeEnd + mdt.Length];
            Array.Copy(mds, outb, NodeEnd);
            Array.Copy(mdt, 0, outb, NodeEnd, mdt.Length);
            return outb;
        }
        internal static void KeepTextures() => M.KeepTextures(CashModel.WeaponPassBlock);
        internal static void ReleaseTextures() => M.ReleaseTextures();

        private static byte[] _fullPx, _fullClut;
        private static int _fullW, _fullH;

        /// <summary>The full d02b10, row-major, and its CLUT — the picture the stand-in's entry is pointed at.</summary>
        private static (string, int, int, byte[], byte[]) Full()
        {
            Files();
            return (Texture, _fullW, _fullH, _fullPx, _fullClut);
        }

        private static (byte[] mds, byte[] img) Files()
        {
            if (!_built && !_failed)
            {
                try
                {
                    byte[] chr = GameDataFiles.TryReadEntry(Pack) ?? throw new IOException(Pack + " not readable");
                    var pack = ChrPack.Parse(chr);
                    _mds = (pack.Find(Model) ?? throw new IOException(Pack + " lacks " + Model)).Payload;
                    var bank = new CatPackBakes.Bank((pack.Find(Bank) ?? throw new IOException(Pack + " lacks " + Bank)).Payload);
                    bool swizzled = bank.Magic[2] == (byte)'2';
                    (_fullW, _fullH, _fullPx, _fullClut) = CashModel.ReadTim8(bank.Block(Texture), swizzled);
                    _img = CatPackBakes.Bank.Build(new[] { (byte)'I', (byte)'M', (byte)'G', (byte)0 },
                                                   new System.Collections.Generic.List<(string, byte[])> { (Texture, CashModel.ResampleTim8(bank.Block(Texture), swizzled, TexSize)) });
                    _built = true;
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"built from Master Utan's boulder: model {_mds.Length} B, texture {_img.Length} B");
                }
                catch (Exception e) { _failed = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "could not be built: " + e.Message); }
            }
            return _built ? (_mds, _img) : (null, null);
        }
    }
}
