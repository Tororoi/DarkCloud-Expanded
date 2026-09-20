using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Queens (e03) CAMERA + PLAYER collision baked into the scene's ground meshes. Two variants per ground sub-file
    /// (origin-placed, so world == local): PLAYER `_a` = simplified structure meshes + perimeter + both-frame walls + canal
    /// containment + railings + the loading-zone trigger quads, split per ground sub; CAMERA `_c` = structure + perimeter +
    /// both-frame walls only, one-sided (windings fixed), directed simplification, consolidated on the one sub that ships
    /// a `_c`. Each frame's tris are pooled and kd-split into ≤ 100-poly spatially compact nodes (tight bboxes = free
    /// runtime gather culling). Buildings keep their vanilla collision.</summary>
    internal static class QueensCollisionBakes
    {
        private static readonly Regex GroundSub = new Regex("^e03g\\d\\d$");

        private static int? NodeIndex(string prefix, string n) { var m = Regex.Match(n, "^" + prefix + "(\\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : null; }

        /// <summary>grid3* and the obj40/44/6/33/34/43/45/42 structures (obj1/obj9 pipes excluded: replaced by solid drums).</summary>
        private static bool IsCameraStructureNode(string nm)
        {
            if (nm.StartsWith("grid3", StringComparison.Ordinal)) return true;
            int? i = NodeIndex("obj", nm);
            return i != null && new[] { 40, 44, 6, 33, 34, 43, 45, 42 }.Contains(i.Value);
        }

        /// <summary>The loading-zone trigger quads in each ground `_a`: tris whose colour entry carries a non-zero destination tag.
        /// They must survive into the rebuilt `_a` or the town exit stops working.</summary>
        private static Dictionary<string, List<(string node, List<double[][]> tris, List<byte[]> ents)>> TriggerNodes(byte[] scn)
        {
            var dir = SceneScn.DirectoryMap(scn);
            var outp = new Dictionary<string, List<(string, List<double[][]>, List<byte[]>)>>();
            foreach (string g in dir.Keys.Where(n => GroundSub.IsMatch(n)))
            {
                var (off, size) = dir[g]; byte[] sub = scn.AsSpan(off, size).ToArray();
                int vo = CollisionMdsWriter.VariantOff(sub, g);
                if (vo < 0) continue;
                int mds = off + vo; var (nodes, wm) = SceneScn.Accum(scn, mds);
                var found = new List<(string, List<double[][]>, List<byte[]>)>();
                for (int ni = 0; ni < nodes.Count; ni++)
                {
                    var n = nodes[ni];
                    if (n.MeshOff == 0) continue;
                    int fo = SceneScn.ResolveMdt(scn, mds, n.MeshOff);
                    if (fo < 0) continue;
                    int POS = (int)IsoBytes.U32(scn, fo + 0x10), DL = (int)IsoBytes.U32(scn, fo + 0x28), COL = (int)IsoBytes.U32(scn, fo + 0x38);
                    int tc = (int)IsoBytes.U32(scn, fo + DL + 0x14), rb = fo + DL + 0x18;
                    var M = wm(ni);
                    var tris = new List<double[][]>(); var ents = new List<byte[]>();
                    for (int t = 0; t < tc; t++)
                    {
                        int i0 = BitConverter.ToInt32(scn, rb + t * 0x14), i1 = BitConverter.ToInt32(scn, rb + t * 0x14 + 4), i2 = BitConverter.ToInt32(scn, rb + t * 0x14 + 8), ci = BitConverter.ToInt32(scn, rb + t * 0x14 + 12);
                        if (!(COL != 0 && ci >= 0)) continue;
                        int eo = fo + COL + ci * 0x10;
                        if (eo + 0x10 > scn.Length || IsoBytes.U16(scn, eo) == 0) continue;
                        double[] Vv(int i) => SceneScn.Xform(M, IsoBytes.F32(scn, fo + POS + i * 0x10), IsoBytes.F32(scn, fo + POS + i * 0x10 + 4), IsoBytes.F32(scn, fo + POS + i * 0x10 + 8));
                        tris.Add(new[] { Vv(i0), Vv(i1), Vv(i2) }); ents.Add(scn.AsSpan(eo, 0x10).ToArray());
                    }
                    if (tris.Count > 0) found.Add((n.Name, tris, ents));
                }
                if (found.Count > 0) outp[g] = found;
            }
            return outp;
        }

        private sealed class Grouped
        {
            internal List<string> Subs;
            internal Dictionary<string, (List<(string name, List<double[][]> tris)> named, List<(string name, List<double[][]> tris, List<byte[]> ents)> trigs)> Player;
            internal List<(string name, List<double[][]> tris)> Camera;
        }

        private static Grouped GroupedCollision(List<SceneScn.PlacedMesh> placed, byte[] scn, int maxTris = 100)
        {
            var bysub = new Dictionary<string, List<double[][]>>(); var camBysub = new Dictionary<string, List<double[][]>>(); var camOwn = new Dictionary<string, List<double[][]>>();
            var ownNames = new HashSet<string> { "obj40", "obj44" };
            foreach (var pm in placed)
            {
                if (!IsCameraStructureNode(pm.Name)) continue;
                var v = pm.Verts;
                var t = QueensTerrainRules.SimplifyTerrain(pm.Tris.Select(tr => new[] { (double[])v[tr[0]].Clone(), (double[])v[tr[1]].Clone(), (double[])v[tr[2]].Clone() }).ToList());
                if (t.Count == 0) continue;
                if (!bysub.ContainsKey(pm.Sub)) bysub[pm.Sub] = new List<double[][]>();
                bysub[pm.Sub].AddRange(t);
                if (ownNames.Contains(pm.Name)) { if (!camOwn.ContainsKey(pm.Name)) camOwn[pm.Name] = new List<double[][]>(); camOwn[pm.Name].AddRange(t); }
                else { if (!camBysub.ContainsKey(pm.Sub)) camBysub[pm.Sub] = new List<double[][]>(); camBysub[pm.Sub].AddRange(t); }
            }
            var subs = bysub.Keys.OrderBy(s => s, StringComparer.Ordinal).ToList();
            var perim = QueensTerrainRules.PerimeterWallTris(); var bw = QueensTerrainRules.BothWallTris();
            var inv = QueensTerrainRules.InvisibleTris(); var pw = QueensTerrainRules.PlayerWallTris();
            var triggers = TriggerNodes(scn);
            var player = new Dictionary<string, (List<(string, List<double[][]>)>, List<(string, List<double[][]>, List<byte[]>)>)>();
            for (int i = 0; i < subs.Count; i++)
            {
                string sub = subs[i]; var used = new HashSet<string>();
                var pool = new List<double[][]>(bysub[sub]);
                if (i == 0) { pool.AddRange(perim); pool.AddRange(bw); pool.AddRange(inv); pool.AddRange(pw); }
                var named = CollisionMdsWriter.PoolSplit(pool, "pcol", used, maxTris);
                var tl = triggers.TryGetValue(sub, out var have) ? have : new List<(string node, List<double[][]> tris, List<byte[]> ents)>();
                var trigs = tl.Select(x => (CollisionMdsWriter.FitNodeName(x.Item1, used, 15), x.Item2, x.Item3)).ToList();
                player[sub] = (named, trigs);
            }
            var cstruct = QueensTerrainRules.FixCameraWinding(subs.SelectMany(s => camBysub.TryGetValue(s, out var l) ? l : new List<double[][]>()));
            var cbw = QueensTerrainRules.FixCameraWinding(bw); var cperim = QueensTerrainRules.FixCameraWinding(perim);
            var cext = QueensTerrainRules.CameraTris();
            var camPool = QueensTerrainRules.CamMergeSelected(cstruct.Concat(cperim).Concat(cbw).Concat(cext).ToList());
            var cused = new HashSet<string>();
            var camera = CollisionMdsWriter.PoolSplit(camPool, "ccol", cused, maxTris);
            var ownNamed = camOwn.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => ($"c{kv.Key}", QueensTerrainRules.FixCameraWinding(QueensTerrainRules.GateTorchSimplify(kv.Value)))).ToList();
            camera.AddRange(ownNamed.Select(x => (CollisionMdsWriter.FitNodeName(x.Item1, cused, 15), x.Item2)));
            return new Grouped { Subs = subs, Player = player, Camera = camera };
        }

        /// <summary>The waterfall render frames' Z-WRITE back on (the `z` per-frame flag → `x`, a no-op letter) so the player's
        /// body occludes behind them.</summary>
        private static (byte[] scn, int n) EnableWaterfallZwrite(byte[] scn)
        {
            var b = (byte[])scn.Clone(); int n = 0;
            foreach (string pat in new[] { "obj48__a01z", "taki2__a01z" })
            {
                byte[] pb = Encoding.ASCII.GetBytes(pat); int zpos = pb.Length - 1, i = 0;
                while (true)
                {
                    int j = IsoBytes.FindFrom(b, pb, i);
                    if (j < 0) break;
                    if (b[j + zpos] == 0x7a) { b[j + zpos] = 0x78; n++; }
                    i = j + 1;
                }
            }
            return (b, n);
        }

        /// <summary>The scene with every ground sub's `_a` rebuilt, the camera `_c` consolidated on the first ground that ships one,
        /// and the waterfalls' Z-write enabled.</summary>
        internal static byte[] BakeStructures(byte[] scene, byte[] mapinfo, Action<string> log, int maxTris = 100)
        {
            byte[] scn = scene;
            var placed = SceneScn.PlacedMeshes(scene, mapinfo);
            var G = GroupedCollision(placed, scene, maxTris);
            var dirnames = new HashSet<string>(SceneScn.DirectoryMap(scene).Keys);
            int camNodes = 0, camTris = 0, invNodes = 0, invTris = 0;
            foreach (string sub in G.Subs)
            {
                if (!dirnames.Contains(sub)) continue;
                var (named, trigs) = G.Player[sub];
                var all = named.Select(x => (x.name, x.tris, (List<byte[]>)null)).Concat(trigs.Select(x => (x.name, x.tris, x.ents))).ToList();
                (scn, _) = CollisionMdsWriter.ReplaceABlock(scn, sub, CollisionMdsWriter.BuildFlatMds(all));
                invNodes += all.Count; invTris += all.Sum(x => x.Item2.Count);
            }
            var dirMap = SceneScn.DirectoryMap(scn);
            string camHost = G.Subs.FirstOrDefault(s => dirMap.ContainsKey(s) && CollisionMdsWriter.VariantOff(scn.AsSpan(dirMap[s].off, dirMap[s].size).ToArray(), s, "_c") >= 0);
            if (camHost != null && G.Camera.Count > 0)
            {
                (scn, _) = CollisionMdsWriter.ReplaceABlock(scn, camHost, CollisionMdsWriter.BuildFlatMds(G.Camera.Select(x => (x.name, x.tris, (List<byte[]>)null)).ToList()), "_c");
                camNodes = G.Camera.Count; camTris = G.Camera.Sum(x => x.tris.Count);
            }
            (scn, _) = EnableWaterfallZwrite(scn);
            log($"e03: {camNodes} camera nodes ({camTris} tris) + {invNodes} player-only ({invTris} tris), scene {scene.Length:N0} -> {scn.Length:N0} B");
            return scn;
        }
    }
}
