using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The town scene parts rebuilt from the disc's OWN geometry, as a post-step on the patched ISO: the fishing
    /// collision bins (DCFC, appended to the fishing cpoly at session start — Queens' canal bridges + pipes + the authored
    /// containment walls; Yellow Drops' west-bank chain) written beside the app, then Queens' snake-statue collision swap and
    /// the Yellow Drops west bank replaced in their scenes. The bins are built BEFORE the s1301 swap: the bank chain is read
    /// from the vanilla stations. Nothing game-derived is stored anywhere.</summary>
    internal static class TownScenePartBakes
    {
        private const string QueensScene = "gedit/e03/scene.scn", QueensMapinfo = "gedit/e03/mapinfo.cfg";
        private const string YellowDropsScene = "gedit/s13/scene.scn", YellowDropsMapinfo = "gedit/s13/mapinfo.cfg";

        /// <summary>'DCFC', u32 version 1, u32 mapNo, u32 triCount, then 9 floats per triangle (the mod computes the plane
        /// normal itself).</summary>
        internal static byte[] Dcfc(int mapNo, List<double[][]> tris)
        {
            var o = new List<byte>(Encoding.ASCII.GetBytes("DCFC"));
            o.AddRange(BitConverter.GetBytes(1u)); o.AddRange(BitConverter.GetBytes((uint)mapNo)); o.AddRange(BitConverter.GetBytes((uint)tris.Count));
            foreach (var t in tris) foreach (var p in t) for (int k = 0; k < 3; k++) o.AddRange(BitConverter.GetBytes((float)p[k]));
            return o.ToArray();
        }

        private static int? ObjN(string name) { var m = Regex.Match(name, "^obj(\\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : null; }

        private static List<double[][]> MeshTris(List<SceneScn.PlacedMesh> placed, HashSet<int> objNums)
        {
            var outp = new List<double[][]>();
            foreach (var pm in placed)
            {
                int? n = ObjN(pm.Name);
                if (n == null || !objNums.Contains(n.Value)) continue;
                foreach (var t in pm.Tris) outp.Add(new[] { (double[])pm.Verts[t[0]].Clone(), (double[])pm.Verts[t[1]].Clone(), (double[])pm.Verts[t[2]].Clone() });
            }
            return outp;
        }

        /// <summary>Queens fishing collision: the canal's bridge (obj40/44) and pipe (obj9) tris from the scene plus the authored
        /// containment walls. (Fish leaking the +Z wall was the engine's gather box, fixed in IsoPatcher.PatchFishBox.)</summary>
        internal static List<double[][]> QueensFishingTris(List<SceneScn.PlacedMesh> placed, Action<string> log)
        {
            var bridges = MeshTris(placed, new HashSet<int> { 40, 44 });
            var pipes = MeshTris(placed, new HashSet<int> { 9 });
            var contain = CollisionMdt.TrisFrom(SceneCollisionData.QueensContainTris);
            log($"    bridges {bridges.Count} + pipes {pipes.Count} + contain {contain.Count}");
            return bridges.Concat(pipes).Concat(contain).ToList();
        }

        internal static void Run(IsoArchive arc, Action<string> log, string fishingOut)
        {
            byte[] e03 = arc.Read(QueensScene), e03map = arc.Read(QueensMapinfo);
            byte[] s13 = arc.Read(YellowDropsScene), s13map = arc.Read(YellowDropsMapinfo);
            if (fishingOut != null)
            {
                Directory.CreateDirectory(fishingOut);
                log("fishing collision bins:");
                var q = QueensFishingTris(SceneScn.PlacedMeshes(e03, e03map), log);
                File.WriteAllBytes(Path.Combine(fishingOut, "queens_2.bin"), Dcfc(2, q));
                log($"  queens_2.bin: {q.Count} tris");
                var y = YellowDropsBankBakes.WestbankFishWalls();
                File.WriteAllBytes(Path.Combine(fishingOut, "yellowdrops_23.bin"), Dcfc(23, y));
                log($"  yellowdrops_23.bin: {y.Count} tris");
            }
            var (h06, orig, nodes) = SnakeStatueBakes.RebuildH06(e03, log);
            log($"e03h06: sub {orig:N0} -> {h06.Length:N0} B ({nodes} player-collision nodes)");
            arc.Redirect(QueensScene, SceneScn.ReplaceSub(e03, "e03h06", h06));
            byte[] s1301 = YellowDropsBankBakes.RebuildS1301(s13, s13map, log);
            arc.Redirect(YellowDropsScene, SceneScn.ReplaceSub(s13, "s1301", s1301));
        }
    }
}
