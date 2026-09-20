using System;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The town camera/structure collision bake, a post-step on the patched ISO: Queens (e03) gets its ground `_a` and
    /// camera `_c` rebuilt from the scene's own structure meshes + the authored geometry, the canal west-end visual cap, and the
    /// ripple texture; Brownboo (s04) gets its camera variant rebuilt (vanilla hull + obj56 with the iwa01 CSG hull) and the
    /// fishing rock nodes appended to its player collision. Scene data only; each scene is redirected into the DATA.DAT tail.</summary>
    internal static class TownCollisionBakes
    {
        internal static void Run(IsoArchive arc, Action<string> log)
        {
            // e03 — Queens
            byte[] scene0 = arc.Read("gedit/e03/scene.scn"), mapinfo0 = arc.Read("gedit/e03/mapinfo.cfg");
            byte[] baked = QueensCollisionBakes.BakeStructures(scene0, mapinfo0, log);
            (baked, _) = CanalVisualCap.AddCanalCap(baked, log);       // AFTER the collision bake: the cap must never enter the collision
            arc.Redirect("gedit/e03/img.pak", CanalRipple.RetextureRippleBank(arc.Read("gedit/e03/img.pak"), log));
            arc.Redirect("gedit/e03/scene.scn", baked);
            // s04 — Brownboo
            byte[] s04 = arc.Read("gedit/s04/scene.scn");
            var named = BrownbooCollisionBakes.BakedNamed(s04);
            var (s04New, _) = CollisionMdsWriter.ReplaceABlock(s04, "s04g01", CollisionMdsWriter.BuildFlatMds(named.Select(x => (x.name, x.tris, (System.Collections.Generic.List<byte[]>)null)).ToList()), "_v");
            log($"s04: s04g01_v rebuilt: {named.Count} nodes, {named.Sum(x => x.tris.Count)} tris, scene {s04.Length:N0} -> {s04New.Length:N0} B");
            (s04New, _) = BrownbooCollisionBakes.BakeRocks(s04New);
            arc.Redirect("gedit/s04/scene.scn", s04New);
        }
    }
}
