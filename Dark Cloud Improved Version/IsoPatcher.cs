using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.SceneBaker;
using static Dark_Cloud_Improved_Version.TextBaker;
using static Dark_Cloud_Improved_Version.AssetCarver;
using static Dark_Cloud_Improved_Version.MdtCarve;
using static Dark_Cloud_Improved_Version.ElfPatches;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Creates a patched COPY of the user's stock Dark Cloud (USA) ISO with the fishing signs baked in, and
    /// publishes the matching pnach to the PCSX2 cheats folder. Cross-platform (macOS / Windows / Linux).
    ///
    /// C# port of tools/iso_patch/{build_sign_iso, sign_scene, ps2iso}.py — the PROVEN Python patcher. The
    /// patch only rearranges data already on the user's disc: absorb the trailing DMMY. padding into DATA.DAT,
    /// then redirect DATA.HD2 index entries at the freed tail (mes_tex.pak = boot texture, s04/scene.scn =
    /// native kanban part, s04/mapinfo.cfg = its placement), plus a tiny ELF boot-cave that registers the
    /// texture. Nothing game-derived is bundled — the sign mesh + texture are carved from the user's OWN ISO.
    ///
    /// Split 2026-09: this class keeps the config bank, the orchestration (Patch / ApplySignPatch) and
    /// the pnach/collision-bake post-steps; the transforms live in IsoBytes, SceneBaker, TextBaker,
    /// AssetCarver, MdtCarve and ElfPatches / ElfCameraPatches / ElfWaterPatches (all `using static`'d here).
    /// </summary>
    internal static class IsoPatcher
    {
        internal const string OutputName  = "Dark Cloud - Expanded.iso";

        internal const string HOST_PAK   = "meswin/mes_tex.pak";
        internal const string SCENE_SCN  = "gedit/s04/scene.scn";
        internal const string MAPINFO    = "gedit/s04/mapinfo.cfg";
        internal const int    SIGN_X = 212, SIGN_Y = 9, SIGN_Z = -61, SIGN_RY = 0;

        // Queens (e03): the sign 3 units SOUTH (+Z) of the fishing trigger (250,70,-70), facing NORTH (-Z, so
        // ry 180 — opposite Brownboo's +Z-facing ry 0). e03 has no kanban part natively, so we clone the SAME
        // s04a01 PTS header (self-contained; the e01b24 texture is already registered globally by the boot-cave)
        // and inject the kanban mesh + placement into e03's own scene.scn / mapinfo.cfg.
        internal const string E03_SCENE   = "gedit/e03/scene.scn";
        internal const string E03_MAPINFO = "gedit/e03/mapinfo.cfg";
        internal const string E03_ANCHOR  = "e03g04";   // an existing GROUND block to insert the kanban placement after
        internal const int    QSIGN_X = 250, QSIGN_Y = 70, QSIGN_Z = -64, QSIGN_RY = 180;   // 6 units south (+Z) of the trigger

        // Low-tide canal fishing (canal-lowtide-fishing-plan.md): the canal-FLOOR sign under the eastern
        // bridge (x≈800), on the authored floor Y=0, facing WEST. CONFIRMED in-game: ry −90. sceVu0RotMatrixY
        // folds the angle (|sin|) so +90 and +270 both face EAST and −X is unreachable by any POSITIVE ry;
        // the function branches on the angle sign, so a NEGATIVE angle (−90) reaches west. (0=south, 180=north
        // work either way since those are Z-facing.)
        internal const int    CANAL_SIGN_X = 800, CANAL_SIGN_Y = 0, CANAL_SIGN_Z = 0, CANAL_SIGN_RY = -90;
        // The ladder donor is carved from the user's OWN ISO (Factory scene, node e05a01/hasigo1) at patch
        // time — same principle as the sign (CarveKanban); nothing is extracted into the codebase.
        internal const string LADDER_SCENE = "gedit/e05/scene.scn";
        internal const string LADDER_PART = "e05a01", LADDER_NODE = "hasigo1";

        // ── NATIVE EVENT-POINT (trigger) BAKING ──────────────────────────────────────────────────────────
        // Triggers are baked as EPARTS_FUNC_DATA entries (0xC0 each) inside a part's PTS blob; at town load
        // EdInitEventPoint (0x183D50) turns each into a live ED_EVENT_POINT — no runtime creation needed.
        // Layout + field map: memory town-event-points.md. Func type: 0x12 -> type-3 SCRIPT, 0x13/0x14 ->
        // type-4/5 ladder BOTTOM/TOP. Time [0,24] -> ConvertTime start==end==7 == always-on.
        internal const int FUNC_STRIDE = 0xC0;
        internal const int FISH_LABEL = 400;        // == CustomFishingSpot.FishingLabelId; north-bank / primary spot
        internal const int FISH_LABEL_CANAL = 401;  // Queens canal-floor spot — its own label + stance (kanbanc sign)
        // Queens canal ladder "tide too high" message: a type-3 script point (label 402) co-located with the
        // climb-down; CanalTide gates the native ladder OR this point by tide. Event-mes id 23 = the line.
        internal const int LADDER_MSG_LABEL = 402;   // == CustomFishingSpot.LadderMsgLabelId
        internal const int LADDER_MSG_ID = 23;                // event-mes id the label-402 script shows
        // Queens canal tide-evict: label 403 = a tiny _MAP_JUMP(East Harbor) script CanalTide fires as an
        // event when the tide rises on a player caught in the drained canal (== CustomFishingSpot.CanalWarpLabelId).
        internal const int CANAL_WARP_LABEL = 403;
        // Dock-spawn event baked into East Harbor (s09): the canal warp's _MAP_JUMP(20, this) makes the engine
        // run it as the arrival event, placing the player at the Shipwreck dock instead of the Queens-side entry.
        internal const int DOCK_SPAWN_LABEL = 404;   // == CustomFishingSpot.DockSpawnEvent
        // The Shipwreck (Sunken Ship, s25) exit spot in East Harbor — captured live from a CameraDiag ref after
        // leaving the ship: world (−1311, ~7, 875.7). (Event 128's (1311,7,875.7) was PART-LOCAL — X mirrored →
        // +1311 was off-map. NOT func_mapj00 (−1088,20,1001) = Rando's shop.) Y=7 = feet; the ref's 21 is the
        // camera look-at ~14 above.
        internal static readonly float[] DOCK_POS = { -1311f, 7f, 875.7f };
        internal const float DOCK_FACING = 0f;                                // ry — tune in-game if he faces wrong here

        // Per-town fishing-trigger position, PART-LOCAL to the sign (the mapinfo placement rotates+translates
        // it to world). Chosen so the native trigger lands exactly where the runtime one did (spot tx,ty,tz).
        internal static readonly float[] BROWNBOO_TRIG = { 0f, 3f, 8f };   // sign(212,9,-61) ry0  -> world (212,12,-53)
        internal static readonly float[] QUEENS_TRIG   = { 0f, 0f, 6f };   // sign(250,70,-64) ry180 -> (250,70,-70); canal placement -> (794,0,0)
        internal static readonly float[] YDROPS_TRIG   = { 0f, 0f, 0f };   // new sign placed AT the spot

        // Yellow Drops (s13): no injected sign yet — inject one at the fishing spot like the other towns.
        internal const string S13_SCENE = "gedit/s13/scene.scn", S13_MAPINFO = "gedit/s13/mapinfo.cfg";
        internal const string S13_ANCHOR = "s1301";                                   // an existing s13 GROUND block
        // Moved 2026-08-30 to the WEST BANK bulge edge (needs the west-bank ground bake; the old spot
        // was (-575,9,-286)). ry 90 = face EAST toward the player walking up (sceVu0RotMatrixY fold).
        internal const int YSIGN_X = -465, YSIGN_Y = 30, YSIGN_Z = 40, YSIGN_RY = 90;  // at the spot (tx,ty,tz), on the y30 plateau

        // Carved ladder climb points, WORLD space (the ladder verts are world-baked so its part sits at origin
        // identity). Derived by running the vanilla Moon-Factory hasigo1 climb points — bottom (9.9,0,-48.4),
        // top (7.6,90,-34.6) — through the SAME de-yaw + placement transform as the mesh (tools/carve_ladder),
        // so the climb-path geometry (stand-off from the rail + lean) matches the Factory exactly. Bottom sits
        // ~6.5u out in front of the ladder's canal edge (z≈47.4); top is on the walkway side.
        internal static readonly float[] LAD_BOTTOM = { LAD_X, 0f, 40.9f };
        internal static readonly float[] LAD_TOP    = { LAD_X, 70f, 54.9f };
        internal const int LAD_RUNGS_BOT = 12, LAD_RUNGS_TOP = 2, LAD_LINK = 0;   // mirror native hasigo1 (+0x74)
        internal static readonly float[] LAD_FACE = { 0f, 0f, 0f };               // rot written to the rec; tune the Y gate in-game

        // ELF boot-cave (register fishsign.img's e01b24 into 0x1c75870 at boot)
        internal const uint GetPackFile = 0x0013F720, EnterIMGFile = 0x00132BA0, LoadFile = 0x0013F360;
        internal const uint SysTexMgr = 0x01C75870, DETOUR_VA = 0x00180D7C, REJOIN_VA = 0x00180D84;
        internal const uint CAVE_VA = 0x002A2314, STR_VA = 0x002452B8, DIAG_VA = 0x01F80000;
        internal const int  CAVE_LEN = 0x6C;
        internal const string OLD_CRC = "A5C05C78";

        // ── PCSX2 cheats folder, per OS ──
        internal static string Pcsx2CheatsDir()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(home, "Library", "Application Support", "PCSX2", "cheats");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2", "cheats");
            string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(xdg)) xdg = Path.Combine(home, ".config");
            return Path.Combine(xdg, "PCSX2", "cheats");
        }

        internal static string Patch(string stockIso, string outDir, Action<string> progress)
        {
            if (string.IsNullOrWhiteSpace(stockIso) || !File.Exists(stockIso))
                throw new FileNotFoundException("Stock ISO not found. Select your Dark Cloud (USA) .iso first.");
            if (string.IsNullOrWhiteSpace(outDir)) outDir = Path.GetDirectoryName(stockIso);
            Directory.CreateDirectory(outDir);
            string outIso = Path.Combine(outDir, OutputName);
            if (Path.GetFullPath(outIso).Equals(Path.GetFullPath(stockIso), StringComparison.OrdinalIgnoreCase))
                throw new IOException("That output folder would overwrite your stock ISO — pick a different folder.");

            progress($"Copying ISO → {OutputName} …");
            File.Copy(stockIso, outIso, overwrite: true);

            uint crc;
            using (var fs = new FileStream(outIso, FileMode.Open, FileAccess.ReadWrite))
                crc = ApplySignPatch(fs, progress);

            // Town camera/structure collision bake — scene data only (the ELF CRC above is unaffected). Runs
            // AFTER the stream above is closed so the baker can reopen the ISO.
            progress("Baking town camera collision …");
            BakeStructureCollision(outIso, progress);

            progress("Publishing pnach to PCSX2 …");
            ReshipPnach(crc);
            return outIso;   // the caller sets the final informative message (avoids overwriting it)
        }

        // ── town camera/structure collision bake (post-step; Python for now, port to C# later) ──────────
        // Invoke the proven baker tools/iso_patch/collision/bake_structure_collision_iso.py against our output ISO. It
        // rebuilds e03's ground `_a` from that ISO's OWN structure meshes + trigger quads (nothing game-derived
        // is bundled — the collision is carved from the user's disc; the perimeter/canal walls are authored
        // constants) and redirects it into the free DATA.DAT tail, composing with the redirects ApplySignPatch
        // already made. Scene data only — the ELF CRC is untouched. Repo root is derived like FishingCollision's
        // game_data path (AppContext.BaseDirectory/../../../..), overridable via DC_REPO; python via DC_PYTHON.
        // TODO: port the bake to pure C# for the standalone distributed build (subprocess needs python3 + repo).
        static void BakeStructureCollision(string outIso, Action<string> progress)
        {
            string repo = Environment.GetEnvironmentVariable("DC_REPO");
            if (string.IsNullOrEmpty(repo))
                repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string script = Path.Combine(repo, "tools", "iso_patch", "collision", "bake_structure_collision_iso.py");
            if (!File.Exists(script))
            {
                progress($"⚠ collision baker not found at {script} — camera collision NOT baked (set DC_REPO).");
                return;
            }
            string py = Environment.GetEnvironmentVariable("DC_PYTHON");
            if (string.IsNullOrEmpty(py)) py = "python3";
            var psi = new ProcessStartInfo
            {
                FileName = py,
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("--iso");
            psi.ArgumentList.Add(outIso);

            string so, se; int code;
            try
            {
                using var p = Process.Start(psi) ?? throw new IOException($"Process.Start returned null for '{py}'.");
                so = p.StandardOutput.ReadToEnd();
                se = p.StandardError.ReadToEnd();
                p.WaitForExit();
                code = p.ExitCode;
            }
            catch (Exception e)
            {
                throw new IOException($"Could not run the collision baker ('{py}'). Is Python installed / on PATH? "
                                      + "Set DC_PYTHON to your python3, or DC_REPO to the repo root.\n" + e.Message);
            }
            if (code != 0)
                throw new IOException($"Collision bake failed (exit {code}).\n{so}\n{se}");
            foreach (string line in so.Split('\n'))
                if (line.Contains("redirected") || line.Contains("camera nodes") || line.Contains("DONE"))
                    progress(line.Trim());
        }

        internal static uint ApplySignPatch(FileStream fs, Action<string> progress)
        {
            var recs = ParseRoot(fs);
            long datIso = (long)recs["DATA.DAT"].Ext * SECTOR;
            long hd2Base = (long)recs["DATA.HD2"].Ext * SECTOR + 16;
            byte[] hed = Rd(fs, (long)recs["DATA.HED"].Ext * SECTOR, (int)recs["DATA.HED"].Size);

            // 1) absorb DMMY. into DATA.DAT -> free tail
            var host = recs["DATA.DAT"]; var dmmy = recs["DMMY."];
            if ((long)host.Ext * SECTOR + host.Size != (long)dmmy.Ext * SECTOR)
                throw new IOException("DATA.DAT and DMMY. are not contiguous — unexpected ISO layout.");
            long freeOff = host.Size;
            long dummySectors = (dmmy.Size + SECTOR - 1) / SECTOR;
            uint newDatSize = (uint)(host.Size + dummySectors * SECTOR);
            Wr(fs, host.RecOff + 10, BitConverter.GetBytes(newDatSize));                                     // LE
            Wr(fs, host.RecOff + 14, new[] { (byte)(newDatSize >> 24), (byte)(newDatSize >> 16), (byte)(newDatSize >> 8), (byte)newDatSize }); // BE
            long freeBytes = newDatSize - freeOff;

            progress("Carving sign assets from your ISO …");
            var (kanbanMds, e01b24Img) = LoadSignAssets(fs, hed, datIso, hd2Base);

            long tail = Align(freeOff);
            byte[] ReadArchive(string name) { long s = hd2Base + (long)ArchiveFind(hed, name) * 32; return Rd(fs, datIso + RdU32(fs, s), (int)RdU32(fs, s + 4)); }
            void Redirect(string name, byte[] data)
            {
                long slot = hd2Base + (long)ArchiveFind(hed, name) * 32;
                if (data.Length > freeOff + freeBytes - tail) throw new IOException("Ran out of tail space (unexpected).");
                Wr(fs, datIso + tail, data);
                uint sec = (uint)(tail >> 11), cnt = (uint)((data.Length + SECTOR - 1) / SECTOR);
                WrU32(fs, slot, (uint)tail); WrU32(fs, slot + 4, (uint)data.Length); WrU32(fs, slot + 8, sec); WrU32(fs, slot + 12, cnt);
                tail = Align(tail + data.Length);
            }

            // 2) texture: prepend e01b24 to mes_tex.pak
            progress("Injecting the fishing-sign texture …");
            Redirect(HOST_PAK, PakPrepend(ReadArchive(HOST_PAK), "fishsign.img", e01b24Img));

            // 3) mesh: inject the kanban as a native georama part + its mapinfo placement, delete the crater
            //    rings' + floors' stray corner triangles, then enable backface culling on the upper crater
            //    walls. Order matters: RemoveRingCornerTris keys off the upper rings' original `__n` names,
            //    and CullUpperCraterWalls renames those to `__s`, so removal must run before the cull rename.
            progress("Injecting the fishing-sign mesh …");
            byte[] s04scene = ReadArchive(SCENE_SCN);
            byte[] tmplHdr  = PartHeader(s04scene, "s04a01");   // the kanban PTS header, reused for e03 too
            // The sign part also carries the BAKED fishing trigger (native type-3 event point at the spot) —
            // replaces the old runtime-installed trigger, so it survives day/night with no self-heal needed.
            Redirect(SCENE_SCN, CullBuildings(CullUpperCraterWalls(RemoveRingCornerTris(
                                    BuildInjectedScene(s04scene, kanbanMds, tmplHdr, funcData: BuildFishingFunc(BROWNBOO_TRIG))))));
            Redirect(MAPINFO,   BuildInjectedMapinfo(ReadArchive(MAPINFO), SIGN_X, SIGN_Y, SIGN_Z, SIGN_RY, "s04a01"));

            // Queens (e03): same kanban mesh + globally-registered e01b24 texture; no crater cleanup (that is
            // Brownboo-only). Just add the part + its placement to e03's own scene / mapinfo.
            // Queens' sign stands on walkable ground, so it also gets a `kanban_a` collision (post + board).
            //
            // Low-tide canal fishing (canal-lowtide-fishing-plan.md): ALSO inject
            //   (a) the carved Factory ladder ("hasigo") on the south canal wall (verts world-baked → mapinfo
            //       places it at origin; bakeIdentity:false keeps the baked translation). CarveLadder ports the
            //       full ISO carve/de-yaw/trim in pure C# (no bundled asset). Its texture e05t06 rides in the
            //       same 2-entry fishsign.img bank as e01b24 (LoadSignAssets), which the boot cave already
            //       registers wholesale — EnterIMGFile(-1) loops every bank entry, so no cave change is needed.
            //       The ladder is reflection/matcap-mapped metal (UVs encode facet direction, not position), so
            //       the donor's texture coordinates and the full 256×256 e05t06 are preserved verbatim; the
            //       ladder renders in e03 exactly as it does in the Moon Factory.
            //   (b) a SECOND kanban placement on the canal floor under the eastern bridge, facing west, so the
            //       low-tide spot has its own sign (reuses the already-injected kanban part + e01b24 texture).
            progress("Carving + injecting the canal ladder …");
            byte[] ladderMds = CarveLadder(ReadArchive(LADDER_SCENE));   // from the user's ISO (Factory e05a01/hasigo1)
            // Queens north-bank kanban carries the label-400 fishing trigger (QUEENS_TRIG local offset).
            byte[] e03scene = BuildInjectedScene(ApplyQueensPartSwaps(ReadArchive(E03_SCENE)), kanbanMds, tmplHdr, BuildKanbanCollision(),
                                                 funcData: BuildFishingFunc(QUEENS_TRIG));
            // The canal-floor sign is its OWN part `kanbanc` (small duplicate of the kanban mesh) carrying a
            // DIFFERENT trigger (label 401 -> its own per-sign script with the canal-floor stance), so triggering
            // it fishes from the canal floor instead of teleporting to the north bank. Same local trigger offset.
            e03scene = BuildInjectedScene(e03scene, kanbanMds, tmplHdr, BuildKanbanCollision("kanbanc_a"), partName: "kanbanc",
                                          funcData: BuildFishingFunc(QUEENS_TRIG, FISH_LABEL_CANAL));
            // The ladder part carries its two baked climb points (type-4 bottom / type-5 top).
            e03scene = BuildInjectedScene(e03scene, ladderMds, tmplHdr, null, "hasigo", bakeIdentity: false,
                                          funcData: BuildLadderFunc());
            // Wading ripple decal (v7): a static part whose LAYER CanalTide flips to 0x15 so DrawWater's
            // static-part loop draws it in the WATER pass (water textures resident + TEX_ANIME animating
            // its e01b22 material, ring-retextured by the bake post-step). Parked at y=-3000 via mapinfo.
            progress("Carving + injecting the wading ripple decal …");
            byte[] e01scn = ReadArchive("gedit/e01/scene.scn");
            e03scene = BuildInjectedScene(e03scene, CarveRippleDecal(e01scn), tmplHdr, null, "wripple");
            // Two HALF-size ripple decals, one on each vertical rail of the ladder (world rails ≈ x701/x711
            // at z48, RE'd from the carved hasigo mesh). Placed at the pole XZ; CanalTide.PoleRipples flips
            // their layer to the water pass and pins Y to the tide (see there).
            byte[] poleDecal = CarveRippleDecal(e01scn, DECAL_HALF / 2f);
            e03scene = BuildInjectedScene(e03scene, poleDecal, tmplHdr, null, "wriplL");
            e03scene = BuildInjectedScene(e03scene, poleDecal, tmplHdr, null, "wriplR");
            Redirect(E03_SCENE, e03scene);

            byte[] e03map = BuildInjectedMapinfo(ReadArchive(E03_MAPINFO), QSIGN_X, QSIGN_Y, QSIGN_Z, QSIGN_RY, E03_ANCHOR, "kanban_a.mds");
            e03map = BuildInjectedMapinfo(e03map, CANAL_SIGN_X, CANAL_SIGN_Y, CANAL_SIGN_Z, CANAL_SIGN_RY, E03_ANCHOR, "kanbanc_a.mds", "kanbanc");
            e03map = BuildInjectedMapinfo(e03map, 0, 0, 0, 0, E03_ANCHOR, "", "hasigo");   // ladder verts are world-baked
            e03map = BuildInjectedMapinfo(e03map, 0, -3000, 0, 0, E03_ANCHOR, "", "wripple");   // parked; CanalTide drives it
            e03map = BuildInjectedMapinfo(e03map, 701, 8, 48, 0, E03_ANCHOR, "", "wriplL");     // west ladder rail
            e03map = BuildInjectedMapinfo(e03map, 711, 8, 48, 0, E03_ANCHOR, "", "wriplR");     // east ladder rail
            e03map = TuneCanalWater(e03map);                                               // camera-follow, square 64x14 grid, p4=1.0
            Redirect(E03_MAPINFO, e03map);

            // Yellow Drops (s13): no native/injected sign, so inject the same kanban sign at its fishing spot,
            // carrying the baked fishing trigger — makes all three custom towns uniform (sign + native trigger).
            progress("Injecting the Yellow Drops sign …");
            Redirect(S13_SCENE, BuildInjectedScene(RaiseYdSuimenn(ReplaceS13Ground(ReadArchive(S13_SCENE))), kanbanMds, tmplHdr, BuildKanbanCollision(),
                                                   funcData: BuildFishingFunc(YDROPS_TRIG)));
            Redirect(S13_MAPINFO, BuildInjectedMapinfo(RaiseYdWater(ReadArchive(S13_MAPINFO)), YSIGN_X, YSIGN_Y, YSIGN_Z, YSIGN_RY, S13_ANCHOR));

            // 4) fishing labels: append spare labels to each custom fishing town's event.stb so the runtime
            //    installer always has dedicated room and never runs out on the town's tiny native spare pool
            //    (that shortfall was the Queens/Yellow Drops "can't quit" bug — labels 133/134 got no room).
            //    ids 500-509 are placeholders the runtime hijacks + renumbers to 400/133/134 exactly like a
            //    town's own spares; the only runtime change is whitelisting them.
            progress("Adding fishing-script label space …");
            foreach (string stbName in FishingStbs)
                Redirect(stbName, ExtendStb(ReadArchive(stbName)));

            // Tide-evict destination: bake the dock-spawn event into East Harbor (s09) so the canal warp's
            // _MAP_JUMP(20, DOCK_SPAWN_LABEL) lands the player at the Shipwreck dock natively (no runtime pin).
            Redirect("gedit/s09/event.stb", BakeStbLabel(ReadArchive("gedit/s09/event.stb"), DOCK_SPAWN_LABEL, BuildDockSpawnCode()));

            // 5) fishing text: carve the catch bubble (talk mes 2000) + entry/quit menu (event mes 20/21/22)
            //    from the user's OWN Norune mes and append them to each custom fishing town's talk + event mes,
            //    so the engine draws them natively — no runtime ClsMes buffer swap.
            progress("Baking the fishing menu + catch text …");
            ushort[] catchMsg = MesExtract(ReadArchive("gedit/e01/e01talk_1.mes"), 2000);
            byte[] noruneEvent = ReadArchive("gedit/e01/e01_1.mes");
            ushort[] menu20 = MesExtract(noruneEvent, 20), menu21 = MesExtract(noruneEvent, 21), menu22 = MesExtract(noruneEvent, 22);
            // Queens canal-ladder "tide too high" line (event-mes id 23). Encoded from ASCII (meswin glyph
            // plane, == the menu text's) + the 0xFF01 terminator MesExtract's blobs carry. Baked into every
            // fishing town (unused outside Queens); label 402's script (CustomFishingSpot) shows it.
            ushort[] ladderMsg = AppendTerminator(WeaponDescriptions.Encode("The tide is too high to climb down."));
            foreach (string code in new[] { "e03", "s13", "s04" })
            {
                // Talk mes: repurpose a spare sentinel entry (no COUNT growth) so no existing message shifts —
                // the custom-NPC dialogue writer (Dialogues.cs) addresses talk-mes messages by absolute buffer
                // offset, and a count-growing append would slide them and cut the first letters off (Pickle).
                Redirect($"gedit/{code}/{code}talk_1.mes",
                         ReplaceEmptyWithMes(ReadArchive($"gedit/{code}/{code}talk_1.mes"), 2000, catchMsg));
                // Event mes: plain append is fine — nothing addresses these three towns' event messages by
                // offset (the menu reads 20/21/22 by id), so the small shift is harmless.
                Redirect($"gedit/{code}/{code}_1.mes",
                         AppendMes(ReadArchive($"gedit/{code}/{code}_1.mes"),
                                   (20, menu20), (21, menu21), (22, menu22), (LADDER_MSG_ID, ladderMsg)));
            }

            // 6) ELF boot-cave + CRC
            progress("Patching the boot loader …");
            return ElfPatchAndCrc(fs, recs["SCUS_971.11"]);
        }

        // ── pnach: copy the mod's own A5C05C78.pnach into the PCSX2 cheats folder as <CRC>.pnach ──
        static void ReshipPnach(uint crc)
        {
            string dir = Pcsx2CheatsDir(); Directory.CreateDirectory(dir);
            string src = Path.Combine(AppContext.BaseDirectory, "Resources", "PNACH", OLD_CRC + ".pnach");
            if (!File.Exists(src)) throw new FileNotFoundException("Bundled pnach not found: " + src);
            string newCrc = crc.ToString("X8");
            string body = Regex.Replace(File.ReadAllText(src), "\\[" + OLD_CRC + "\\]", "[" + newCrc + "]");
            string Norm(string s) => Regex.Replace(s, "\\[[0-9A-Fa-f]{8}\\]", "[]");
            foreach (string old in Directory.GetFiles(dir, "*.pnach"))   // drop OUR stale patched-CRC copies only
            {
                string nm = Path.GetFileNameWithoutExtension(old).ToUpperInvariant();
                if (!Regex.IsMatch(nm, "^[0-9A-F]{8}$") || nm == OLD_CRC || nm == newCrc) continue;
                if (Norm(File.ReadAllText(old)) == Norm(body)) File.Delete(old);
            }
            File.WriteAllText(Path.Combine(dir, newCrc + ".pnach"), body);
        }
    }
}
