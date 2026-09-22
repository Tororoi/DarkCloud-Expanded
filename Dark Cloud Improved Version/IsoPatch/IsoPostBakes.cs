using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The patch flow's post-steps that run on the finished ISO through <see cref="IsoArchive"/>, by name — so
    /// the patcher and the dev command line (`postbake &lt;step&gt; &lt;iso&gt;`, used to compare a step's output against
    /// a reference on a scratch ISO) run exactly the same code.</summary>
    internal static class IsoPostBakes
    {
        internal static readonly Dictionary<string, Action<IsoArchive, Action<string>>> Steps = new()
        {
            ["monster-scripts"] = MonsterScriptBakes.Run,
            ["borrowed-shots"]  = BorrowedShotBakes.Run,
            ["town-models"]     = TownModelBakes.Run,
            ["town-collision"]  = TownCollisionBakes.Run,
            ["cat-pack"]        = CatPackBakes.Run,
            ["toan-glow"]       = ToanGlowBakes.Run,
            ["town-scene-parts"] = (arc, log) => TownScenePartBakes.Run(arc, log, Environment.GetEnvironmentVariable("DC_FISHING_OUT"))   // dev: the bins go where the env var says,
        };

        internal static void Run(string step, string iso, Action<string> log)
        {
            using var arc = new IsoArchive(iso, log);
            Steps[step](arc, log);
        }

        /// <summary>`postbake &lt;step&gt; &lt;iso&gt;`: one step on the given ISO, in place. Dev use only.</summary>
        internal static int RunCli(string[] args)
        {
            if (args.Length >= 2 && args[1] == "mathcheck")
            {   // dev: the numeric helpers against CPython's answers
                foreach (var p in new[] { new[] { 0.1, 0.2, 0.3 }, new[] { 1e-3, 7.25, -3.5 }, new[] { 12.345, -0.001, 98.7 } })
                    Console.WriteLine($"dist {PyMath.Repr(PyMath.Dist(p, new[] { 0.0, 0.0, 0.0 }))} hypot2 {PyMath.Repr(PyMath.Hypot(p[0], p[2]))} pow {PyMath.Repr(p[1] > 0 ? Math.Pow(p[1], 0.5) : 0.0)} {PyMath.Repr(Math.Pow(0.37, 1.5))}");
                return 0;
            }
            if (args.Length < 3 || !Steps.ContainsKey(args[1]))
            {
                Console.WriteLine("usage: postbake <" + string.Join("|", Steps.Keys) + "> <iso>");
                return 2;
            }
            Run(args[1], args[2], Console.WriteLine);
            return 0;
        }
    }
}
