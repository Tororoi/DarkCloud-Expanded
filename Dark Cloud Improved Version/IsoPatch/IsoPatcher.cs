using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Dark_Cloud_Improved_Version.IsoBytes;
using static Dark_Cloud_Improved_Version.SceneBaker;
using static Dark_Cloud_Improved_Version.TownSceneBakes;
using static Dark_Cloud_Improved_Version.StbLabelBaker;
using static Dark_Cloud_Improved_Version.MesTextBaker;
using static Dark_Cloud_Improved_Version.IsoAssetCarver;
using static Dark_Cloud_Improved_Version.CanalLadderCarve;
using static Dark_Cloud_Improved_Version.SignPlacements;
using static Dark_Cloud_Improved_Version.ElfPatches;
using static Dark_Cloud_Improved_Version.FishingLabelIds;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Creates a patched COPY of the user's stock Dark Cloud (USA) ISO with the fishing signs baked in, and
    /// publishes the matching pnach to the PCSX2 cheats folder. Cross-platform (macOS / Windows / Linux).
    ///
    /// The patch only rearranges data already on the user's disc: absorb the trailing DMMY. padding into DATA.DAT,
    /// then redirect DATA.HD2 index entries at the freed tail (mes_tex.pak = boot texture, s04/scene.scn =
    /// native kanban part, s04/mapinfo.cfg = its placement), plus a tiny ELF boot-cave that registers the
    /// texture. Nothing game-derived is bundled — the sign mesh + texture are carved from the user's OWN ISO.
    /// (The shared ISO primitives also live on in tools/iso_patch/ps2iso.py.)
    ///
    /// This class is the orchestration: <see cref="Patch"/> (copy, <see cref="ApplySignPatch"/>, then the
    /// <see cref="IsoPostBakes.Steps"/> in their one order, then the pnach). The placements are SignPlacements, the boot
    /// cave's addresses BootCave; the transforms live in IsoBytes, SceneBaker, TownSceneBakes, StbLabelBaker, MesTextBaker,
    /// IsoAssetCarver, CanalLadderCarve and ElfPatches (all `using static`'d here).
    /// </summary>
    internal static class IsoPatcher
    {
        internal const string OutputName  = "Dark Cloud - Expanded.iso";
        internal const string StockElfCrc = "A5C05C78";

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

        internal static string Patch(string stockIso, string outDir, Action<string> progress, bool publishPnach = true)
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

            // The post-steps, in the one order IsoPostBakes gives (the ISO's tail is written in that order). Scene data only — the
            // ELF CRC above is unaffected. Each runs AFTER the stream above is closed, on its own archive over the ISO.
            foreach (var step in IsoPostBakes.Steps)
            {
                progress(step.Progress);
                using var arc = new IsoArchive(outIso, progress);
                step.Run(arc, progress);
            }

            if (publishPnach) { progress("Publishing pnach to PCSX2 …"); ReshipPnach(crc); }
            else progress("pnach not published (--no-pnach)");
            return outIso;   // the caller sets the final informative message (avoids overwriting it)
        }

        internal static uint ApplySignPatch(FileStream fs, Action<string> progress)
        {
            var recs = ParseRoot(fs);
            long datIso = (long)recs["DATA.DAT"].Ext * SectorBytes;
            long hd2Base = (long)recs["DATA.HD2"].Ext * SectorBytes + 16;
            byte[] hed = Rd(fs, (long)recs["DATA.HED"].Ext * SectorBytes, (int)recs["DATA.HED"].Size);

            // 1) absorb DMMY. into DATA.DAT -> free tail
            var host = recs["DATA.DAT"]; var dmmy = recs["DMMY."];
            if ((long)host.Ext * SectorBytes + host.Size != (long)dmmy.Ext * SectorBytes)
                throw new IOException("DATA.DAT and DMMY. are not contiguous — unexpected ISO layout.");
            long dummySectors = (dmmy.Size + SectorBytes - 1) / SectorBytes;
            uint newDatSize = (uint)(host.Size + dummySectors * SectorBytes);
            Wr(fs, host.RecOff + 10, BitConverter.GetBytes(newDatSize));                                     // LE
            Wr(fs, host.RecOff + 14, new[] { (byte)(newDatSize >> 24), (byte)(newDatSize >> 16), (byte)(newDatSize >> 8), (byte)newDatSize }); // BE

            progress("Carving sign assets from your ISO …");
            var (kanbanMds, e01b24Img) = LoadSignAssets(fs, hed, datIso, hd2Base);

            // The archive over the stream just grown: its free tail is the sector past the highest DATA.HD2 slot end, which on the
            // stock disc is the sector DATA.DAT itself ended on (slot end 0x67162480, size 0x67162800 — both align to 0x67162800), so
            // every redirect below lands where the absorbed DMMY. space begins. The stream stays this method's.
            var arc = new IsoArchive(fs);

            // 2) texture: prepend e01b24 to mes_tex.pak
            progress("Injecting the fishing-sign texture …");
            arc.Redirect(BootCave.TexturePak, PakPrepend(arc.Read(BootCave.TexturePak), "fishsign.img", e01b24Img));

            // 3) mesh: inject the kanban as a native georama part + its mapinfo placement, delete the crater
            //    rings' + floors' stray corner triangles, then enable backface culling on the upper crater
            //    walls. Order matters: RemoveRingCornerTris keys off the upper rings' original `__n` names,
            //    and CullUpperCraterWalls renames those to `__s`, so removal must run before the cull rename.
            progress("Injecting the fishing-sign mesh …");
            byte[] s04scene = arc.Read(BrownbooScene);
            byte[] tmplHdr  = PartHeader(s04scene, "s04a01");   // the kanban PTS header, reused for e03 too
            // The sign part also carries the BAKED fishing trigger (native type-3 event point at the spot) —
            // replaces the old runtime-installed trigger, so it survives day/night with no self-heal needed.
            arc.Redirect(BrownbooScene, CullBuildings(CullUpperCraterWalls(RemoveRingCornerTris(
                                        BuildInjectedScene(s04scene, kanbanMds, tmplHdr, funcData: BuildFishingFunc(BrownbooTriggerOffset))))));
            arc.Redirect(BrownbooMapinfo, BuildInjectedMapinfo(arc.Read(BrownbooMapinfo), BrownbooSignX, BrownbooSignY, BrownbooSignZ, BrownbooSignRotY, "s04a01"));

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
            byte[] ladderMds = CarveLadder(arc.Read(MoonFactoryScene));   // from the user's ISO (Factory e05a01/hasigo1)
            // Queens north-bank kanban carries the label-400 fishing trigger (QueensTriggerOffset local offset).
            byte[] e03scene = BuildInjectedScene(arc.Read(QueensScene), kanbanMds, tmplHdr, BuildKanbanCollision(),
                                                 funcData: BuildFishingFunc(QueensTriggerOffset));
            // The canal-floor sign is its OWN part `kanbanc` (small duplicate of the kanban mesh) carrying a
            // DIFFERENT trigger (label 401 -> its own per-sign script with the canal-floor stance), so triggering
            // it fishes from the canal floor instead of teleporting to the north bank. Same local trigger offset.
            e03scene = BuildInjectedScene(e03scene, kanbanMds, tmplHdr, BuildKanbanCollision("kanbanc_a"), partName: "kanbanc",
                                          funcData: BuildFishingFunc(QueensTriggerOffset, CanalFishingLabelId));
            // The ladder part carries its two baked climb points (type-4 bottom / type-5 top).
            e03scene = BuildInjectedScene(e03scene, ladderMds, tmplHdr, null, "hasigo", bakeIdentity: false,
                                          funcData: BuildLadderFunc());
            // Wading ripple decal (v7): a static part whose LAYER CanalTide flips to 0x15 so DrawWater's
            // static-part loop draws it in the WATER pass (water textures resident + TEX_ANIME animating
            // its e01b22 material, ring-retextured by the bake post-step). Parked at y=-3000 via mapinfo.
            progress("Carving + injecting the wading ripple decal …");
            byte[] e01scn = arc.Read("gedit/e01/scene.scn");
            e03scene = BuildInjectedScene(e03scene, CarveRippleDecal(e01scn), tmplHdr, null, "wripple");
            // Two HALF-size ripple decals, one on each vertical rail of the ladder (world rails ≈ x701/x711
            // at z48, RE'd from the carved hasigo mesh). Placed at the pole XZ; CanalWaterEffects.PoleRipples flips
            // their layer to the water pass and pins Y to the tide (see there).
            byte[] poleDecal = CarveRippleDecal(e01scn, RippleDecalHalfExtent / 2f);
            e03scene = BuildInjectedScene(e03scene, poleDecal, tmplHdr, null, "wriplL");
            e03scene = BuildInjectedScene(e03scene, poleDecal, tmplHdr, null, "wriplR");
            arc.Redirect(QueensScene, e03scene);

            byte[] e03map = BuildInjectedMapinfo(arc.Read(QueensMapinfo), QueensSignX, QueensSignY, QueensSignZ, QueensSignRotY, QueensAnchorPart, "kanban_a.mds");
            e03map = BuildInjectedMapinfo(e03map, CanalSignX, CanalSignY, CanalSignZ, CanalSignRotY, QueensAnchorPart, "kanbanc_a.mds", "kanbanc");
            e03map = BuildInjectedMapinfo(e03map, 0, 0, 0, 0, QueensAnchorPart, "", "hasigo");   // ladder verts are world-baked
            e03map = BuildInjectedMapinfo(e03map, 0, -3000, 0, 0, QueensAnchorPart, "", "wripple");   // parked; CanalTide drives it
            e03map = BuildInjectedMapinfo(e03map, 701, 8, 48, 0, QueensAnchorPart, "", "wriplL");     // west ladder rail
            e03map = BuildInjectedMapinfo(e03map, 711, 8, 48, 0, QueensAnchorPart, "", "wriplR");     // east ladder rail
            e03map = TuneCanalWater(e03map);                                               // camera-follow, square 64x14 grid, p4=1.0
            arc.Redirect(QueensMapinfo, e03map);

            // Yellow Drops (s13): no native/injected sign, so inject the same kanban sign at its fishing spot,
            // carrying the baked fishing trigger — makes all three custom towns uniform (sign + native trigger).
            progress("Injecting the Yellow Drops sign …");
            arc.Redirect(YellowDropsScene, BuildInjectedScene(RaiseYellowDropsSurfaceMesh(arc.Read(YellowDropsScene)), kanbanMds, tmplHdr, BuildKanbanCollision(),
                                                       funcData: BuildFishingFunc(YellowDropsTriggerOffset)));
            arc.Redirect(YellowDropsMapinfo, BuildInjectedMapinfo(RaiseYellowDropsWaterPlane(arc.Read(YellowDropsMapinfo)), YellowDropsSignX, YellowDropsSignY, YellowDropsSignZ, YellowDropsSignRotY, YellowDropsAnchorPart));

            // 4) spare-label space: grow each walkable town's event.stb label table (SpareLabelsFor gives the
            //    ids+sizes per town). Every town gets the ally-swap label 405 (so the in-place town swap works
            //    everywhere, including Norune/Spirit Tree which have NO native spare); the three custom fishing
            //    towns also get the fishing pool in the SAME table-grow (menu/enter/quit/bait/…). The old fishing
            //    shortfall — labels 133/134 getting no room — is covered by the fishing entries here.
            progress("Adding town ally-swap + fishing label space …");
            const string s09Stb = "gedit/s09/event.stb";
            foreach (string stbName in AllySwapTownStbPaths)
            {
                if (stbName == s09Stb) continue;   // s09 chained with its dock-spawn bake below (one redirect per file)
                var (ids, sizes) = SpareLabelsFor(stbName);
                arc.Redirect(stbName, ExtendStb(arc.Read(stbName), ids, sizes));
            }

            // Tide-evict destination: bake the dock-spawn event into East Harbor (s09) so the canal warp's
            // _MAP_JUMP(20, DockSpawnEvent) lands the player at the Shipwreck dock natively (no runtime pin).
            // s09 also needs the ally-swap 405 label — chain both grows into ONE redirect (two separate redirects
            // of the same file would clobber each other, keeping only the last).
            {
                var (s09ids, s09sizes) = SpareLabelsFor(s09Stb);
                byte[] s09 = ExtendStb(arc.Read(s09Stb), s09ids, s09sizes);
                arc.Redirect(s09Stb, BakeStbLabel(s09, DockSpawnEvent, BuildDockSpawnCode()));
            }

            // 5) fishing text: carve the catch bubble (talk mes 2000) + entry/quit menu (event mes 20/21/22)
            //    from the user's OWN Norune mes and append them to each custom fishing town's talk + event mes,
            //    so the engine draws them natively — no runtime ClsMes buffer swap.
            progress("Baking the fishing menu + catch text …");
            ushort[] catchMsg = MesExtract(arc.Read("gedit/e01/e01talk_1.mes"), 2000);
            byte[] noruneEvent = arc.Read("gedit/e01/e01_1.mes");
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
                arc.Redirect($"gedit/{code}/{code}talk_1.mes",
                             ReplaceEmptyWithMes(arc.Read($"gedit/{code}/{code}talk_1.mes"), 2000, catchMsg));
                // Event mes: plain append is fine — nothing addresses these three towns' event messages by
                // offset (the menu reads 20/21/22 by id), so the small shift is harmless.
                arc.Redirect($"gedit/{code}/{code}_1.mes",
                             AppendMes(arc.Read($"gedit/{code}/{code}_1.mes"),
                                       (20, menu20), (21, menu21), (22, menu22), (LadderMsgId, ladderMsg)));
            }

            // 5.2) the dungeon's "acquired" notices (meswin/system_ae.bin, ids 10/20/30) moved into 80-word blocks at the bank's
            //      end, so the Bandit Slingshot can append its line to the one an item's arrival will show (BanditSlingshot).
            progress("Baking room into the item notices …");
            {
                var moves = new (int, int)[BanditSlingshot.NoticeIds.Length];
                for (int i = 0; i < moves.Length; i++) moves[i] = (BanditSlingshot.NoticeIds[i], BanditSlingshot.NoticeReserveWords);
                arc.Redirect(BanditSlingshot.NoticeFile, RelocateMes(arc.Read(BanditSlingshot.NoticeFile), moves));
            }

            // 5.5) Ungaga run animation: speed up the ally-swap model's run to match his battle run. The swap
            //      loads e323_2c10a.chr — the only Ungaga model with BOTH cloth and a real run (c10p had cloth
            //      but its run KEY reused the walk frames; the NPC c10a had a real run but no cloth). That event
            //      model's run KEY plays at 0.30, visibly slower/less energetic than his battle model c10b's run
            //      (0.55). In-place ASCII edit of the run KEY's speed in the model's info.cfg — same 4 bytes
            //      ("0.30"→"0.55"), no relocation, DATA.DAT-only so the ELF CRC is untouched. See AllySwapPrototype.
            progress("Tuning Ungaga's run speed …");
            {
                long uslot = hd2Base + (long)ArchiveFind(hed, "gedit/e04/chara/e323_2c10a.chr") * 32;
                long uAt = datIso + RdU32(fs, uslot) + 0xAB4E4;   // the "0.30" in `KEY\t60,\t80,\t0.30,\t//走り` (run)
                byte[] cur = Rd(fs, uAt, 4);
                if (cur[0] != '0' || cur[1] != '.' || cur[2] != '3' || cur[3] != '0')
                    throw new IOException("Ungaga run-speed site is not vanilla (expected \"0.30\") — unmodified Dark Cloud (USA) ISO expected.");
                Wr(fs, uAt, new byte[] { (byte)'0', (byte)'.', (byte)'5', (byte)'5' });
            }

            // 5b) dungeon overlay words (dun.bin — a flat image; CRC-neutral, it is not the ELF)
            if (!recs.TryGetValue("DUN.BIN", out var dunRec)) throw new IOException("DUN.BIN not found in the ISO root — unexpected ISO layout.");
            DunPatches.Apply(fs, dunRec, progress);

            // 6) ELF boot-cave + CRC
            progress("Patching the boot loader …");
            return ElfPatchAndCrc(fs, recs["SCUS_971.11"]);
        }

        // ── pnach: copy the mod's own A5C05C78.pnach into the PCSX2 cheats folder as <CRC>.pnach ──
        static void ReshipPnach(uint crc)
        {
            string dir = Pcsx2CheatsDir(); Directory.CreateDirectory(dir);
            string src = Path.Combine(AppContext.BaseDirectory, "Resources", "PNACH", StockElfCrc + ".pnach");
            if (!File.Exists(src)) throw new FileNotFoundException("Bundled pnach not found: " + src);
            string newCrc = crc.ToString("X8");
            string body = Regex.Replace(File.ReadAllText(src), "\\[" + StockElfCrc + "\\]", "[" + newCrc + "]");
            string Norm(string s) => Regex.Replace(s, "\\[[0-9A-Fa-f]{8}\\]", "[]");
            foreach (string old in Directory.GetFiles(dir, "*.pnach"))   // drop OUR stale patched-CRC copies only
            {
                string nm = Path.GetFileNameWithoutExtension(old).ToUpperInvariant();
                if (!Regex.IsMatch(nm, "^[0-9A-F]{8}$") || nm == StockElfCrc || nm == newCrc) continue;
                if (Norm(File.ReadAllText(old)) == Norm(body)) File.Delete(old);
            }
            File.WriteAllText(Path.Combine(dir, newCrc + ".pnach"), body);
        }
    }
}
