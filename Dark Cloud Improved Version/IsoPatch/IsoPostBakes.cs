using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The post-steps of the patch flow: the bakes that run on the finished ISO through <see cref="IsoArchive"/>, in the
    /// ONE order <see cref="Steps"/> gives — the order the DATA.DAT tail is written in, so the patched ISO's bytes depend on it.
    /// IsoPatcher.Patch runs them all; the dev command line (`postbake &lt;step&gt; &lt;iso&gt;`, used to compare a step's output
    /// against a reference on a scratch ISO) runs one by name — the same code either way.</summary>
    internal static class IsoPostBakes
    {
        internal sealed class Step
        {
            internal string Name, Progress;
            internal Action<IsoArchive, Action<string>> Run;
        }

        /// <summary>Where TownScenePartBakes writes the fishing collision bins: beside the app by default; the CLI points it at
        /// DC_FISHING_OUT (unset = the bins are skipped).</summary>
        internal static string FishingCollisionDir = Path.Combine(AppContext.BaseDirectory, "Resources", "FishingCollision");

        /// <summary>In ISO order. Each step opens its own archive: the archive's free tail is re-derived from the DATA.HD2 slots
        /// (the highest slot end, sector-aligned), which after any redirect is exactly where the previous archive left it, so one
        /// archive across several steps and one per step write the same bytes.</summary>
        internal static readonly Step[] Steps =
        {
            // The scene parts rebuilt from the disc's own geometry, and the fishing collision bins — scene data only (the ELF CRC
            // is unaffected). First, so the collision bake reads the scenes as this leaves them.
            new() { Name = "town-scene-parts", Progress = "Baking the Queens statue collision, the Yellow Drops bank and the fishing walls …",
                    Run = (arc, log) => TownScenePartBakes.Run(arc, log, FishingCollisionDir) },
            // Town camera/structure collision: Queens' ground `_a` and camera `_c` rebuilt from the scene's own structure meshes +
            // the authored walls, the canal cap and ripple texture; Brownboo's camera variant and fishing rocks.
            new() { Name = "town-collision",   Progress = "Baking town camera collision …",                 Run = TownCollisionBakes.Run },
            // Each swapped-in ally's full town motion set by transplanting clips into the safe base model + rewriting its KEY
            // table (docs/town-swap-animation-map.md).
            new() { Name = "town-models",      Progress = "Assembling town-ally animation sets …",           Run = TownModelBakes.Run },
            // The Divine Beast Title cat rig (mesh, textures, leap clips) INTO Xiao's dungeon pack (dun\mainchara\c04b.chr) as hidden
            // extra nodes + a second motion channel — NOT the weapon pack: the weapon menu rebuilds every carried weapon into a
            // 944 KB arena and a bigger weapon pack overflowed it. Reads every model from the user's OWN ISO; idempotent; also
            // reverts the earlier weapon-pack bake if an ISO carries it.
            new() { Name = "cat-pack",         Progress = "Baking the Divine Beast Title cat into Xiao's dungeon model …", Run = CatPackBakes.Run },
            new() { Name = "toan-glow",        Progress = "Baking Toan's glow discs …",                     Run = ToanGlowBakes.Run },      // Solar Flash's white glow disc, into Toan's pack
            new() { Name = "pellet-sheet",     Progress = "Baking the pellet sheet's blank cell …",         Run = PelletSheetBakes.Run },   // the transparent cell (the bottom row is the mod's)
            // Hurt-sphere fixes baked into the monster scripts (dun\monstor\*.stb): Blizzard takes Titan's four spheres, Sam and
            // Billy take Mr. Blare's two, and Minotaur Joe's face admits the Divine Beast cat's kick at 100 %
            // (ElfCatPatches.PatchCatSpherePercent reads the armed spare table). Idempotent (appended marker).
            new() { Name = "monster-scripts",  Progress = "Baking monster script fixes (hurt spheres, mimic wake guard) …", Run = MonsterScriptBakes.Run },
            // Effect containers the shot-effect pack can load under the dead dun\effect names (BorrowedShots): each a copy with the
            // cfg record the pack's loader asks for by name appended; the sources untouched.
            new() { Name = "borrowed-shots",   Progress = "Baking borrowed shot effects …",                 Run = BorrowedShotBakes.Run },
            // The mod's enemy species: the Bomb Gemron's model, script and name under a repurposed orphan DATA.HED entry (its species
            // record is ElfSpeciesPatches'). Idempotent (the script's appended marker; the name compared).
            new() { Name = "bomb-gemron",      Progress = "Baking the Bomb Gemron …",                         Run = BombGemronBake.Run },
            // The Crystal Gemron's model, script and name under the orphan entries e148a (renamed e168a). Idempotent (the script's moved
            // cry frame; the name compared).
            new() { Name = "crystal-gemron",   Progress = "Baking the Crystal Gemron …",                      Run = CrystalGemronBake.Run },
            // Every monster sound program of the dungeon sound sets in one file, from which tools/stubs/monster_bank.s builds each
            // randomized floor's monster bank (MonsterSoundBake).
            new() { Name = "monster-sounds",   Progress = "Gathering the monster sounds …",                   Run = MonsterSoundBake.Run },
            new() { Name = "confuse-ability",  Progress = "Baking the Confuse ability's name and icon …",   Run = ConfuseAbilityBakes.Run }, // the name (system banks) and icon (charaface)
            // Spelling fixes to the English text, in place in every English bank — last, so it edits the banks as the steps above
            // leave them (the system banks' appended Confuse name, the relocated item notices).
            new() { Name = "text-fixes",       Progress = "Fixing the game's spelling …",                   Run = MesTextFixes.Run },
            // The element picker's None cell (a grey synth sphere) in the quick-change menu's own icon sheet.
            new() { Name = "element-menu-icon", Progress = "Baking the element picker's None cell …",        Run = ElementMenuIconBake.Run },
        };

        internal static Step Find(string name) => Steps.FirstOrDefault(s => s.Name == name);

        internal static void Run(string step, string iso, Action<string> log)
        {
            using var arc = new IsoArchive(iso, log);
            Find(step).Run(arc, log);
        }

        /// <summary>`postbake &lt;step&gt; &lt;iso&gt;`: one step on the given ISO, in place. Dev use only.</summary>
        internal static int RunCli(string[] args)
        {
            if (args.Length >= 2 && args[1] == "mathcheck")
            {   // dev: the numeric helpers against CPython's answers
                foreach (var p in new[] { new[] { 0.1, 0.2, 0.3 }, new[] { 1e-3, 7.25, -3.5 }, new[] { 12.345, -0.001, 98.7 } })
                    Console.WriteLine($"dist {ExactMath.Repr(ExactMath.Dist(p, new[] { 0.0, 0.0, 0.0 }))} hypot2 {ExactMath.Repr(ExactMath.Hypot(p[0], p[2]))} pow {ExactMath.Repr(p[1] > 0 ? Math.Pow(p[1], 0.5) : 0.0)} {ExactMath.Repr(Math.Pow(0.37, 1.5))}");
                return 0;
            }
            if (args.Length < 3 || Find(args[1]) == null)
            {
                Console.WriteLine("usage: postbake <" + string.Join("|", Steps.Select(s => s.Name)) + "> <iso>");
                return 2;
            }
            FishingCollisionDir = Environment.GetEnvironmentVariable("DC_FISHING_OUT");   // dev: the bins go where the env var says, or nowhere
            Run(args[1], args[2], Console.WriteLine);
            return 0;
        }
    }
}
