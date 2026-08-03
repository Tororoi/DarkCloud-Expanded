using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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
    /// </summary>
    internal static class IsoPatcher
    {
        internal const int    SECTOR      = 2048;
        internal const string OutputName  = "Dark Cloud - Expanded.iso";

        const string HOST_PAK   = "meswin/mes_tex.pak";
        const string SCENE_SCN  = "gedit/s04/scene.scn";
        const string MAPINFO    = "gedit/s04/mapinfo.cfg";
        const int    SIGN_X = 212, SIGN_Y = 9, SIGN_Z = -61, SIGN_RY = 0;

        // Queens (e03): the sign 3 units SOUTH (+Z) of the fishing trigger (250,70,-70), facing NORTH (-Z, so
        // ry 180 — opposite Brownboo's +Z-facing ry 0). e03 has no kanban part natively, so we clone the SAME
        // s04a01 PTS header (self-contained; the e01b24 texture is already registered globally by the boot-cave)
        // and inject the kanban mesh + placement into e03's own scene.scn / mapinfo.cfg.
        const string E03_SCENE   = "gedit/e03/scene.scn";
        const string E03_MAPINFO = "gedit/e03/mapinfo.cfg";
        const string E03_ANCHOR  = "e03g04";   // an existing GROUND block to insert the kanban placement after
        const int    QSIGN_X = 250, QSIGN_Y = 70, QSIGN_Z = -64, QSIGN_RY = 180;   // 6 units south (+Z) of the trigger

        // Low-tide canal fishing (canal-lowtide-fishing-plan.md): the canal-FLOOR sign under the eastern
        // bridge (x≈800), on the authored floor Y=0. DIAGNOSTIC: ry −90. sceVu0RotMatrixY folds the angle
        // (|sin|) so +90 and +270 both face EAST and −X/west seems unreachable by positive ry; but the
        // function branches on the angle SIGN, so test a negative value. If −90 stays east, west truly needs
        // a mesh pre-flip (front +Z→−Z) with all three signs' ry retuned (Brownboo/QSIGN/canal).
        const int    CANAL_SIGN_X = 800, CANAL_SIGN_Y = 0, CANAL_SIGN_Z = 0, CANAL_SIGN_RY = -90;
        // The ladder donor is carved from the user's OWN ISO (Factory scene, node e05a01/hasigo1) at patch
        // time — same principle as the sign (CarveKanban); nothing is extracted into the codebase.
        const string LADDER_SCENE = "gedit/e05/scene.scn";
        const string LADDER_PART = "e05a01", LADDER_NODE = "hasigo1";

        // ELF boot-cave (register fishsign.img's e01b24 into 0x1c75870 at boot)
        const uint GetPackFile = 0x0013F720, EnterIMGFile = 0x00132BA0, LoadFile = 0x0013F360;
        const uint SysTexMgr = 0x01C75870, DETOUR_VA = 0x00180D7C, REJOIN_VA = 0x00180D84;
        const uint CAVE_VA = 0x002A2314, STR_VA = 0x002452B8, DIAG_VA = 0x01F80000;
        const int  CAVE_LEN = 0x6C;
        const string OLD_CRC = "A5C05C78";

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

        // ── little-endian FileStream I/O ──
        static byte[] Rd(FileStream fs, long off, int n) { fs.Seek(off, SeekOrigin.Begin); var b = new byte[n]; int r = 0; while (r < n) { int k = fs.Read(b, r, n - r); if (k == 0) break; r += k; } return b; }
        static void  Wr(FileStream fs, long off, byte[] b) { fs.Seek(off, SeekOrigin.Begin); fs.Write(b, 0, b.Length); }
        static uint  RdU32(FileStream fs, long off) => BitConverter.ToUInt32(Rd(fs, off, 4), 0);
        static void  WrU32(FileStream fs, long off, uint v) => Wr(fs, off, BitConverter.GetBytes(v));
        static uint   U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);
        static void   U32(byte[] b, int o, uint v) => Array.Copy(BitConverter.GetBytes(v), 0, b, o, 4);
        static ushort U16(byte[] b, int o) => BitConverter.ToUInt16(b, o);
        static void   U16(byte[] b, int o, ushort v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        static long  Align(long x, int a = SECTOR) => (x + a - 1) & ~((long)a - 1);

        class Rec { public long RecOff; public uint Ext; public uint Size; }

        static Dictionary<string, Rec> ParseRoot(FileStream fs)
        {
            byte[] pvd = Rd(fs, 16L * SECTOR, SECTOR);
            if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
                throw new IOException("Not a 2048-byte ISO9660 image — is this the right file?");
            uint rootLba = U32(pvd, 158), rootSize = U32(pvd, 166);
            byte[] d = Rd(fs, (long)rootLba * SECTOR, (int)rootSize);
            var recs = new Dictionary<string, Rec>();
            int pos = 0;
            while (pos + 33 <= d.Length)
            {
                int ln = d[pos];
                if (ln == 0) { pos = (pos / SECTOR + 1) * SECTOR; continue; }
                uint ext = U32(d, pos + 2), size = U32(d, pos + 10);
                int nlen = d[pos + 32];
                string name = Encoding.Latin1.GetString(d, pos + 33, nlen).Split(';')[0].ToUpperInvariant();
                recs[name] = new Rec { RecOff = (long)rootLba * SECTOR + pos, Ext = ext, Size = size };
                pos += ln;
            }
            return recs;
        }

        // ── DATA.HED name lookup (80-byte slots, backslash paths) ──
        static int ArchiveFind(byte[] hed, string name)
        {
            string want = name.Replace('/', '\\');
            for (int i = 0; i < hed.Length / 80; i++)
            {
                int end = Array.IndexOf(hed, (byte)0, i * 80, 80); if (end < 0) end = i * 80 + 80;
                string n = Encoding.Latin1.GetString(hed, i * 80, end - i * 80);
                if (n == want) return i;
            }
            throw new IOException($"'{name}' not found in the disc archive — is this a Dark Cloud (USA) ISO?");
        }

        // ── PAK: prepend (name,data) sub-files (name@0, dataOff@0x40, size@0x44, stride@0x48; self-relative) ──
        static byte[] PakBuildEntry(string name, byte[] data)
        {
            int stride = (int)Align(0x50 + data.Length, 0x40);
            var e = new byte[stride];
            byte[] nb = Encoding.Latin1.GetBytes(name); Array.Copy(nb, e, nb.Length);
            U32(e, 0x40, 0x50); U32(e, 0x44, (uint)data.Length); U32(e, 0x48, (uint)stride);
            Array.Copy(data, 0, e, 0x50, data.Length);
            return e;
        }
        static byte[] PakPrepend(byte[] pak, string name, byte[] data)
        {
            byte[] ent = PakBuildEntry(name, data);
            var outb = new byte[ent.Length + pak.Length];
            Array.Copy(ent, outb, ent.Length); Array.Copy(pak, 0, outb, ent.Length, pak.Length);
            return outb;
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
            Redirect(SCENE_SCN, CullBuildings(CullUpperCraterWalls(RemoveRingCornerTris(
                                    BuildInjectedScene(s04scene, kanbanMds, tmplHdr)))));
            Redirect(MAPINFO,   BuildInjectedMapinfo(ReadArchive(MAPINFO), SIGN_X, SIGN_Y, SIGN_Z, SIGN_RY, "s04a01"));

            // Queens (e03): same kanban mesh + globally-registered e01b24 texture; no crater cleanup (that is
            // Brownboo-only). Just add the part + its placement to e03's own scene / mapinfo.
            // Queens' sign stands on walkable ground, so it also gets a `kanban_a` collision (post + board).
            //
            // Low-tide canal fishing (canal-lowtide-fishing-plan.md): ALSO inject
            //   (a) the carved Factory ladder ("hasigo") on the south canal wall (verts world-baked → mapinfo
            //       places it at origin; bakeIdentity:false keeps the baked translation). Optional: only when
            //       the bundled ladder.mds is available (ISO-carve+trim C# port is a follow-on). Its texture
            //       bank e05t06 still needs its own boot-cave registration (see note below) — until then the
            //       ladder renders with wrong textures but correct geometry/placement.
            //   (b) a SECOND kanban placement on the canal floor under the eastern bridge, facing west, so the
            //       low-tide spot has its own sign (reuses the already-injected kanban part + e01b24 texture).
            progress("Carving + injecting the canal ladder …");
            byte[] ladderMds = CarveLadder(ReadArchive(LADDER_SCENE));   // from the user's ISO (Factory e05a01/hasigo1)
            byte[] e03scene = BuildInjectedScene(ReadArchive(E03_SCENE), kanbanMds, tmplHdr, BuildKanbanCollision());
            e03scene = BuildInjectedScene(e03scene, ladderMds, tmplHdr, null, "hasigo", bakeIdentity: false);
            Redirect(E03_SCENE, e03scene);

            byte[] e03map = BuildInjectedMapinfo(ReadArchive(E03_MAPINFO), QSIGN_X, QSIGN_Y, QSIGN_Z, QSIGN_RY, E03_ANCHOR, "kanban_a.mds");
            e03map = BuildInjectedMapinfo(e03map, CANAL_SIGN_X, CANAL_SIGN_Y, CANAL_SIGN_Z, CANAL_SIGN_RY, E03_ANCHOR, "kanban_a.mds");
            e03map = BuildInjectedMapinfo(e03map, 0, 0, 0, 0, E03_ANCHOR, "", "hasigo");   // ladder verts are world-baked
            Redirect(E03_MAPINFO, e03map);

            // 4) fishing labels: append spare labels to each custom fishing town's event.stb so the runtime
            //    installer always has dedicated room and never runs out on the town's tiny native spare pool
            //    (that shortfall was the Queens/Yellow Drops "can't quit" bug — labels 133/134 got no room).
            //    ids 500-509 are placeholders the runtime hijacks + renumbers to 400/133/134 exactly like a
            //    town's own spares; the only runtime change is whitelisting them.
            progress("Adding fishing-script label space …");
            foreach (string stbName in FishingStbs)
                Redirect(stbName, ExtendStb(ReadArchive(stbName)));

            // 5) fishing text: carve the catch bubble (talk mes 2000) + entry/quit menu (event mes 20/21/22)
            //    from the user's OWN Norune mes and append them to each custom fishing town's talk + event mes,
            //    so the engine draws them natively — no runtime ClsMes buffer swap.
            progress("Baking the fishing menu + catch text …");
            ushort[] catchMsg = MesExtract(ReadArchive("gedit/e01/e01talk_1.mes"), 2000);
            byte[] noruneEvent = ReadArchive("gedit/e01/e01_1.mes");
            ushort[] menu20 = MesExtract(noruneEvent, 20), menu21 = MesExtract(noruneEvent, 21), menu22 = MesExtract(noruneEvent, 22);
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
                         AppendMes(ReadArchive($"gedit/{code}/{code}_1.mes"), (20, menu20), (21, menu21), (22, menu22)));
            }

            // 6) ELF boot-cave + CRC
            progress("Patching the boot loader …");
            return ElfPatchAndCrc(fs, recs["SCUS_971.11"]);
        }

        // ── event.stb: append spare labels so the runtime fishing installer never runs out of room ──
        //
        // Vanilla fishing towns host the minigame inside their event.stb (enter woven into label 256, plus
        // dedicated labels 133/134). Custom fishing towns have no such labels, so the mod builds its scripts
        // at runtime by hijacking the town's few spare labels — and Queens/Yellow Drops simply don't have
        // enough, so quit/bait couldn't install. We give every custom fishing town its own spare pool by
        // appending empty labels to its event.stb (baked into the ISO, exactly how vanilla ships fishing in
        // the STB). The label table has zero slack (it ends right at codeBase), so we RELOCATE it: append the
        // new label-code space, then a grown copy of the table (originals unchanged + new entries), and point
        // the header at it. Original labels keep their absolute codeOffsets since the original code never moves.
        static readonly string[] FishingStbs =
            { "gedit/e03/event.stb", "gedit/s13/event.stb", "gedit/s04/event.stb" };   // Queens, Yellow Drops, Brownboo
        // A custom fishing spot installs exactly FOUR scripts. We bake exactly four spare labels — one per
        // script — carrying their FINAL ids and sized (measured Need() + margin) to hold that script in a
        // SINGLE label. The mod's install claims each by id and writes straight into it: no runtime renumber,
        // and nothing spills across two labels (the old "arena run" that retired a second label). The three
        // custom fishing towns have no native 133/134/400/9600, so these ids are collision-free.
        // ⚠ FishSpareIds MUST match CustomFishingSpot.{MenuSubLabelId=9600, FishingLabelId=400} and
        // EventPoints.{FishingExitLabel=133, FishingBaitLabel=134}. Sizes measured 2026-07-24:
        // menu 1780, enter 1994, quit 892, bait 436.
        internal const int FishTermId = 9500;
        static readonly int[] FishSpareIds   = { 9600, 400, 133, 134 };         // menu, enter, quit, bait
        static readonly int[] FishSpareSizes = { 0x800, 0xA00, 0x500, 0x300 };  // one size per id, same order

        static byte[] ExtendStb(byte[] stb)
        {
            uint cbase = U32(stb, 0x08);                               // header: CodeBase @0x08
            uint tbl = U32(stb, 0x0C), cnt = U32(stb, 0x10);           // header: LabelTable @0x0C, LabelCount @0x10
            int origEnd = stb.Length;
            int spares = FishSpareSizes.Length;
            int total = 0; foreach (int s in FishSpareSizes) total += s;
            int newTblOff = origEnd + total;                          // terminator points here; code fills [origEnd, newTblOff)
            var outb = new byte[newTblOff + (int)(cnt + spares + 1) * 8];
            Array.Copy(stb, outb, origEnd);                            // original STB verbatim (appended space stays 0)
            Array.Copy(stb, (int)tbl, outb, newTblOff, (int)cnt * 8);  // original label table, copied unchanged
            int p = newTblOff + (int)cnt * 8;
            int codeOff = origEnd;
            for (int k = 0; k < spares; k++)                           // new spares -> point into the appended code space
            {
                U32(outb, p, (uint)FishSpareIds[k]);                    // FINAL id — the mod claims it by number
                U32(outb, p + 4, (uint)codeOff);                        // codeOffset is ABSOLUTE (runtime uses stb+off)
                // gap[+0] = entry PC as a codeBase-relative offset to the first instruction
                // (codeOff + LabelCodeSkip 0x38 - codeBase); every real label carries this, WriteScript
                // never sets it, so a zero-filled baked label runs from the wrong PC and returns instantly.
                U32(outb, codeOff, (uint)(codeOff + 0x38 - (int)cbase));
                codeOff += FishSpareSizes[k];
                p += 8;
            }
            U32(outb, p, FishTermId);                                  // terminator label: makes the last spare's size computable
            U32(outb, p + 4, (uint)codeOff);                          // == newTblOff (end of the last spare's code)
            U32(outb, 0x0C, (uint)newTblOff);                         // header now points at the relocated table
            U32(outb, 0x10, cnt + (uint)spares + 1);
            return outb;
        }

        // ── town .mes text: bake in the fishing messages the custom towns lack ──────────────────────────
        //
        // Vanilla fishing towns' mes ship the catch bubble (talk mes msg 2000) and the entry/quit menu options
        // (event mes 20/21/22); custom fishing towns do not, so those bubbles/menus rendered blank. Rather than
        // swap the ClsMes buffer pointer at runtime, we carve the messages from the user's OWN Norune mes and
        // inject them into each custom town's mes, so the engine draws them natively.
        //
        // meswin .mes format (verified against every town's mes): u16 count, u16 endOff, count×{u16 id, u16
        // wordOff}, then the text. A message's text starts at byte 2*(count + wordOff + 1) and ends at 0xFF01;
        // files are zero-padded. AppendMes TRIMS that trailing padding before appending so the injected text
        // stays inside the engine's ~48 KB talk-mes buffer even for the largest town (Queens) — see AppendMes.

        /// <summary>The glyph words of message <paramref name="id"/> (from its text start to the 0xFF01
        /// terminator, inclusive). Throws if the id is absent.</summary>
        static ushort[] MesExtract(byte[] mes, int id)
        {
            int cnt = U16(mes, 0);
            for (int i = 0; i < cnt; i++)
            {
                if (U16(mes, 4 + i * 4) != id) continue;
                int tb = 2 * (cnt + U16(mes, 4 + i * 4 + 2) + 1);
                var w = new List<ushort>();
                while (tb + 1 < mes.Length)
                {
                    ushort g = U16(mes, tb); w.Add(g); tb += 2;
                    if (g == 0xFF01) return w.ToArray();
                }
                break;
            }
            throw new IOException($"meswin message {id} not found (unexpected mes layout)");
        }

        /// <summary>Append <paramref name="add"/> messages to a meswin .mes, first TRIMMING the file's trailing
        /// zero padding so the new text lands right after real content. Existing messages are preserved
        /// byte-for-byte (their text never moves); the index is kept id-sorted.
        ///
        /// The trim is why append works for the largest town. The engine loads a town's talk mes into a fixed
        /// ~48 KB buffer (`MesBuffer`). Every town pads its message region with ~31 KB of trailing zeros, so a
        /// plain append placed msg 2000 ~31 KB deep — at ~offset 78 KB for Queens (e03), the largest town at
        /// ~47 KB of real content — far outside the buffer, and `MakeMesTexture` measured garbage and drew a
        /// stretched, textless catch bubble. Trimming the padding drops 2000 to ~offset 47 KB (just past real
        /// content), inside the buffer, WITHOUT shifting any existing message — so nothing else in the town
        /// regresses. (Prepending the text instead moves every message and corrupted unrelated dialogue, so it
        /// is avoided.) Verified: every non-sentinel message decodes byte-identically to a plain append, and
        /// 2000 lands in-buffer for all three custom towns.</summary>
        static byte[] AppendMes(byte[] orig, params (int id, ushort[] words)[] add)
        {
            int cnt = U16(orig, 0), f2 = U16(orig, 2), n = add.Length, newCount = cnt + n;
            int idxEnd = 4 + cnt * 4;                       // byte where the original text blob starts

            // Trim trailing zero padding, keeping a small zero gap so the last real message still terminates
            // (it ends on a 0-word, exactly as it did against the full padding).
            int blobEnd = orig.Length;
            while (blobEnd > idxEnd && orig[blobEnd - 1] == 0) blobEnd--;    // last non-zero byte of the blob
            const int Gap = 16;                                             // zero words kept as a terminator margin
            int raw = (blobEnd - idxEnd) + Gap; raw += raw & 1;             // word-align
            int blobLen = Math.Min(orig.Length - idxEnd, raw);

            var ents = new List<(int id, int off)>(newCount);
            for (int i = 0; i < cnt; i++)                  // existing: +n absorbs the index-growth shift; text unmoved
                ents.Add((U16(orig, 4 + i * 4), U16(orig, 4 + i * 4 + 2) + n));

            var newText = new List<byte>();
            int cum = blobLen;                             // new text laid out after the TRIMMED blob
            foreach (var (id, words) in add)
            {
                int textByte = 4 + newCount * 4 + cum;
                ents.Add((id, textByte / 2 - newCount - 1));
                foreach (ushort g in words) { newText.Add((byte)g); newText.Add((byte)(g >> 8)); }
                cum += words.Length * 2;
            }
            ents.Sort((a, b) => a.id.CompareTo(b.id));     // the engine expects the index id-sorted

            var outb = new byte[4 + newCount * 4 + blobLen + newText.Count];
            U16(outb, 0, (ushort)newCount);
            U16(outb, 2, (ushort)(f2 + n));
            int p = 4;
            foreach (var (id, off) in ents) { U16(outb, p, (ushort)id); U16(outb, p + 2, (ushort)off); p += 4; }
            Array.Copy(orig, idxEnd, outb, p, blobLen); p += blobLen;   // trimmed original blob ...
            newText.CopyTo(outb, p);                                    // ... then the new text
            return outb;
        }

        /// <summary>Inject one message by REPURPOSING the mes's highest-id empty (sentinel) entry instead of
        /// adding a new one — so the message COUNT never grows and no existing message's read position shifts.
        ///
        /// This matters because other systems address talk-mes messages by ABSOLUTE buffer offset — notably the
        /// custom-NPC dialogue writer (Dialogues.cs), which writes e.g. Pickle's text to a hardcoded address. A
        /// count-growing append slides every existing message a few bytes (the format ties a message's read
        /// offset to the message count), so those hardcoded writes land a couple glyphs early and the first
        /// letters get cut off. Repurposing a spare sentinel keeps the count fixed, so every existing message —
        /// and every hardcoded offset into it — stays exactly where it was. The new text is placed after real
        /// content (padding trimmed) so it lands inside the engine's ~48 KB talk-mes buffer.
        ///
        /// The three custom fishing towns each have a high-id empty sentinel (e03 1399, s04 1279, s13 1259).
        /// Verified: repurposing it leaves every other message's read offset unchanged and msg 2000 decodes.</summary>
        static byte[] ReplaceEmptyWithMes(byte[] orig, int id, ushort[] words)
        {
            int cnt = U16(orig, 0), f2 = U16(orig, 2), idxEnd = 4 + cnt * 4;

            // Find the highest-id EMPTY entry (its text begins with a 0-word or the terminator) — the sentinel.
            int si = -1, bestId = -1;
            for (int i = 0; i < cnt; i++)
            {
                int woff = U16(orig, 4 + i * 4 + 2), tb = 2 * (cnt + woff + 1);
                bool empty = tb + 1 >= orig.Length || U16(orig, tb) == 0x0000 || U16(orig, tb) == 0xFF01;
                int mid = U16(orig, 4 + i * 4);
                if (empty && mid > bestId) { bestId = mid; si = i; }
            }
            if (si < 0) throw new IOException($"no empty sentinel entry to repurpose for msg {id}");

            // Trim trailing zero padding (keep a small gap); place the new text right after real content.
            int blobEnd = orig.Length;
            while (blobEnd > idxEnd && orig[blobEnd - 1] == 0) blobEnd--;
            int raw = (blobEnd - idxEnd) + 16; raw += raw & 1;
            int blobLen = Math.Min(orig.Length - idxEnd, raw);

            var ents = new List<(int id, int off)>(cnt);
            for (int i = 0; i < cnt; i++) ents.Add((U16(orig, 4 + i * 4), U16(orig, 4 + i * 4 + 2)));
            ents[si] = (id, (4 + cnt * 4 + blobLen) / 2 - cnt - 1);   // repurpose the sentinel slot in place
            ents.Sort((a, b) => a.id.CompareTo(b.id));

            var newText = new List<byte>();
            foreach (ushort g in words) { newText.Add((byte)g); newText.Add((byte)(g >> 8)); }

            var outb = new byte[4 + cnt * 4 + blobLen + newText.Count];
            U16(outb, 0, (ushort)cnt);                      // COUNT unchanged — the whole point
            U16(outb, 2, (ushort)f2);
            int p = 4;
            foreach (var (mid, off) in ents) { U16(outb, p, (ushort)mid); U16(outb, p + 2, (ushort)off); p += 4; }
            Array.Copy(orig, idxEnd, outb, p, blobLen); p += blobLen;
            newText.CopyTo(outb, p);
            return outb;
        }

        // ── scene.scn: append a `kanban` PTS part cloned from s04a01, + a 26th part-table entry ──
        static readonly int[] SIZE_FIELDS = { 0x4C, 0x50, 0x54, 0x78, 0x90, 0xA8, 0xC0, 0xD8 };
        const int MDSSIZE_FIELD = 0x58;
        // LoadPTS (0x19f6f0) reads the `_a` collision variant's OFFSET at part+0x78 and its SIZE (gate) at
        // part+0x7c; if size>0 it feeds part+offset to LoadCollisionFile -> CreateCollisionMDT.
        const int COLL_OFF_FIELD = 0x78, COLL_SIZE_FIELD = 0x7C;

        /// <summary>The kanban's collision: a single solid PANEL hugging the sign, as an MDS-wrapped COLLISION
        /// MDT. This mirrors how Muska Lacka's native sign is collided (e04m01_a @ the kanban): one flat box
        /// ~13 wide x ~3 thick x 16 tall spanning the whole sign, NOT thin post/board boxes (those were too
        /// flimsy and let the player clip through). Verts are LOCAL — the mapinfo places/rotates them with the
        /// sign, so they line up with the visual. Format reverse-engineered from CreateCollisionMDT
        /// (0x127250) + LoadCollisionFile (0x126f70): MDT needs magic, +0x08 total size, +0x0C vert count,
        /// +0x10 POS offset, +0x28 display-list offset, +0x38 colour block (0 = none); the DL has the triangle
        /// count at +0x14 and 5-int32 records (v0,v1,v2,colour,pad) at +0x18; POS verts are x,y,z,1 at 0x10.</summary>
        static byte[] BuildKanbanCollision()
        {
            var verts = new List<float[]>();
            var tris  = new List<int[]>();
            void Box(float x0, float x1, float y0, float y1, float z0, float z1)
            {
                int b = verts.Count;
                verts.Add(new[]{x0,y0,z0}); verts.Add(new[]{x1,y0,z0}); verts.Add(new[]{x1,y0,z1}); verts.Add(new[]{x0,y0,z1});
                verts.Add(new[]{x0,y1,z0}); verts.Add(new[]{x1,y1,z0}); verts.Add(new[]{x1,y1,z1}); verts.Add(new[]{x0,y1,z1});
                int[][] f = { new[]{0,1,2}, new[]{0,2,3}, new[]{4,6,5}, new[]{4,7,6}, new[]{0,4,5}, new[]{0,5,1},
                              new[]{3,2,6}, new[]{3,6,7}, new[]{0,3,7}, new[]{0,7,4}, new[]{1,5,6}, new[]{1,6,2} };
                foreach (var t in f) tris.Add(new[]{ b+t[0], b+t[1], b+t[2] });   // winding is moot — collision is two-sided
            }
            // One solid panel over the whole sign (kanban local bbox is X[-6,6] Y[0,16] Z[0,2]); ~3 thick in Z
            // and slightly over-wide, matching Muska Lacka's native ~13 x 3 x 16 sign collision.
            Box(-6.5f, 6.5f, 0f, 16f, -1f, 2f);

            int vc = verts.Count, tc = tris.Count;
            int posOff = 0x40, dlOff = posOff + vc * 0x10, mdtLen = dlOff + 0x18 + tc * 0x14;
            var mdt = new byte[mdtLen];
            U32(mdt, 0x00, 0x0054444Du);            // 'MDT\0'
            U32(mdt, 0x08, (uint)mdtLen);           // total size (memcpy in CreateCollisionMDT)
            U32(mdt, 0x0C, (uint)vc);               // POS vertex count (CreateBBox)
            U32(mdt, 0x10, (uint)posOff);           // POS offset
            U32(mdt, 0x28, (uint)dlOff);            // display-list offset
            U32(mdt, 0x38, 0);                      // colour block: none
            for (int i = 0; i < vc; i++)
            {
                int o = posOff + i * 0x10;
                Array.Copy(BitConverter.GetBytes(verts[i][0]), 0, mdt, o + 0, 4);
                Array.Copy(BitConverter.GetBytes(verts[i][1]), 0, mdt, o + 4, 4);
                Array.Copy(BitConverter.GetBytes(verts[i][2]), 0, mdt, o + 8, 4);
                Array.Copy(BitConverter.GetBytes(1.0f),        0, mdt, o + 12, 4);
            }
            U32(mdt, dlOff + 0x14, (uint)tc);       // triangle count
            for (int i = 0; i < tc; i++)
            {
                int o = dlOff + 0x18 + i * 0x14;
                U32(mdt, o + 0, (uint)tris[i][0]); U32(mdt, o + 4, (uint)tris[i][1]); U32(mdt, o + 8, (uint)tris[i][2]);
                // +0x0C colour index, +0x10 pad — left 0
            }

            // MDS wrapper: [0x10 header][0x70 node][MDT @ 0x80] — the node has an identity matrix + parent -1.
            const int nodeOff = 0x10, mdtStart = 0x80;
            var mds = new byte[mdtStart + mdt.Length];
            U32(mds, 0x00, 0x0053444Du); U32(mds, 0x04, 1); U32(mds, 0x08, 1); U32(mds, 0x0C, 0x10);   // MDS,ver,nodeCount,tblOff
            U32(mds, nodeOff + 0x04, 0x70);
            byte[] nn = Encoding.Latin1.GetBytes("kanban_a");
            Array.Copy(nn, 0, mds, nodeOff + 0x08, nn.Length);
            U32(mds, nodeOff + 0x28, mdtStart);            // meshOff (MDS-relative) -> the collision MDT
            U32(mds, nodeOff + 0x2C, 0xFFFFFFFFu);         // parent = -1
            for (int i = 0; i < 4; i++) Array.Copy(BitConverter.GetBytes(1.0f), 0, mds, nodeOff + 0x30 + i * 0x14, 4);  // identity 4x4
            Array.Copy(mdt, 0, mds, mdtStart, mdt.Length);
            return mds;
        }

        /// <summary>Injects a `kanban` part into a scene.scn. <paramref name="templateHeader"/> is a 0x160-byte
        /// PTS part header (carved from s04a01 with <see cref="PartHeader"/>) — self-contained, so the same one
        /// is reused for Brownboo AND Queens.</summary>
        static byte[] BuildInjectedScene(byte[] scene, byte[] kanbanMds, byte[] templateHeader, byte[] collisionMds = null,
                                         string partName = "kanban", bool bakeIdentity = true)
        {
            var scn = new List<byte>(scene);
            int n = (int)U32(scene, 4);

            var kb = (byte[])kanbanMds.Clone();
            const int NODE = 0x10, MAT = NODE + 0x30, TRANS = MAT + 12 * 4;      // node 0 matrix / translation row
            if (bakeIdentity)   // kanban verts are local; force identity+origin so the mapinfo positions it.
            {                   // the ladder MDS already carries world-baked verts (identity), so skip.
                for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                    Array.Copy(BitConverter.GetBytes(r == c ? 1.0f : 0.0f), 0, kb, MAT + (r * 4 + c) * 4, 4);   // identity 3x3
                for (int k = 0; k < 3; k++) Array.Copy(BitConverter.GetBytes(0.0f), 0, kb, TRANS + k * 4, 4);   // origin
            }

            var part = new List<byte>();
            part.AddRange(templateHeader);                                      // the reusable 0x160 PTS header
            byte[] pname = Encoding.Latin1.GetBytes(partName + "_0.mds");
            for (int i = 0; i < 0x10; i++) part[0x08 + i] = i < pname.Length ? pname[i] : (byte)0;
            part.AddRange(kb);
            int collOff = 0, collLen = 0;
            if (collisionMds != null)
            {
                while ((part.Count & 0xF) != 0) part.Add(0);          // 16-align the collision block
                collOff = part.Count; collLen = collisionMds.Length;
                part.AddRange(collisionMds);
            }
            int psize = part.Count;
            byte[] pa = part.ToArray();
            foreach (int o in SIZE_FIELDS) U32(pa, o, (uint)psize);
            U32(pa, MDSSIZE_FIELD, (uint)kb.Length);
            if (collisionMds != null)
            {
                U32(pa, COLL_OFF_FIELD,  (uint)collOff);              // part+0x78: `_a` collision offset (overrides the SIZE_FIELDS write)
                U32(pa, COLL_SIZE_FIELD, (uint)collLen);             // part+0x7c: its size — LoadPTS loads it only when > 0
            }

            int blob = (int)Align(scn.Count, 16);
            while (scn.Count < blob) scn.Add(0);
            scn.AddRange(pa);
            byte[] outp = scn.ToArray();
            int ent = 0x10 + n * 0x30;
            byte[] pn = Encoding.Latin1.GetBytes(partName);
            for (int i = 0; i < 0x10; i++) outp[ent + i] = i < pn.Length ? pn[i] : (byte)0;
            U32(outp, ent + 0x10, (uint)blob); U32(outp, ent + 0x14, (uint)psize);
            U32(outp, 4, (uint)(n + 1));
            return outp;
        }

        // ── scene.scn: enable backface culling on Brownboo's upper crater walls (edit-mode view fix) ──
        // The crater wall is a vertical stack of `s04g01NN__X` mesh nodes. At MDS load the engine's
        // SetFrameAttr reads the node-name suffix after "__" and turns each letter into a render flag:
        // 's' enables backface culling (single-sided), 'n' leaves it off (two-sided). The artist tagged the
        // lower rings (Y 0..300) `__s` but the upper rings (Y 300..1200) `__n`, so the upper walls draw
        // double-sided — their inward-facing back faces show through as stray geometry that hides the town
        // from an overhead edit-mode camera. Flipping the 12 upper nodes' suffix to `__s` makes them
        // attribute-identical to the (correctly culled) lower rings. One byte per node; geometry unchanged.
        static readonly string[] UPPER_WALL_NODES = {
            "s04g0105__n", "s04g0106__n", "s04g0107__n", "s04g0108__n", "s04g0109__n", "s04g0110__n",
            "s04g0111__n", "s04g0112__n", "s04g0113__n", "s04g0114__n", "s04g0115__n", "s04g0116__n",
        };

        static byte[] CullUpperCraterWalls(byte[] scene)
        {
            foreach (string node in UPPER_WALL_NODES)
            {
                byte[] key = Encoding.Latin1.GetBytes(node + "\0");   // the null-terminated node-name field
                int at = Find(scene, key);
                if (at < 0) throw new IOException($"crater-wall node '{node}' not found in scene.scn");
                scene[at + node.Length - 1] = (byte)'s';              // trailing 'n' -> 's' (culling on)
            }
            return scene;
        }

        static int Find(byte[] hay, byte[] needle) => FindFrom(hay, needle, 0);

        static int FindFrom(byte[] hay, byte[] needle, int start)
        {
            for (int i = Math.Max(0, start); i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // ── scene.scn: make Brownboo's houses single-sided so the camera, when it ends up INSIDE a house, sees
        //    straight through it instead of hitting the near walls (the camera already clips in; the problem is
        //    the occlusion). Same SetFrameAttr suffix mechanism as the crater walls — the '__s' suffix turns on
        //    backface culling, so a wall viewed from inside (its exterior face pointing away) is culled and the
        //    whole house becomes see-through from within, while looking identical from outside. h0201/h0202 are
        //    already '__s'; the '__n' houses flip to '__s'; the suffix-less houses get a '__s' written into the
        //    16-byte name field's null padding (verified all-zero, so no bytes shift).
        static byte[] CullBuildings(byte[] scene)
        {
            foreach (string node in new[] { "h0101__n", "h0102__n", "h0103__n" })   // '__n' -> '__s'
            {
                int at = Find(scene, Encoding.Latin1.GetBytes(node + "\0"));
                if (at < 0) throw new IOException($"building node '{node}' not found in scene.scn");
                scene[at + node.Length - 1] = (byte)'s';
            }
            foreach (var (node, expect) in new[] { ("h0104", 1), ("h0301", 3), ("h0302", 3) })  // append '__s'
            {
                byte[] key = Encoding.Latin1.GetBytes(node + "\0");
                byte[] suf = Encoding.Latin1.GetBytes("__s\0");
                int from = 0, hits = 0, at;
                while ((at = FindFrom(scene, key, from)) >= 0)
                {
                    Array.Copy(suf, 0, scene, at + node.Length, suf.Length);   // overwrite '\0' + padding
                    from = at + node.Length; hits++;
                }
                if (hits != expect) throw new IOException($"building node '{node}': found {hits}, expected {expect}");
            }
            return scene;
        }

        static int FindLast(byte[] hay, byte[] needle, int before)
        {
            for (int i = Math.Min(before, hay.Length - needle.Length); i >= 0; i--)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // ── scene.scn: delete stray horizontal triangles that a top-down edit camera sees (edit-mode view fix) ──
        // Two sets, both up-facing horizontal triangles sitting outside the town, so they stay visible from an
        // overhead edit camera even after the vertical walls cull:
        //   • Each square crater ring carries 4 tiny corner-fill triangles at its (±500,±500) corners. A ring's
        //     ONLY up-facing tris are those 4 corners, so we remove every up-facing tri (yMax = +inf).
        //   • The crater floors s04g0117__s and s04g0117__s1 (the pond bottom) are genuinely horizontal surfaces
        //     (Y 0..76 / mostly up-facing) but each ALSO has 2 sunken corner strays down at Y=-100. For them we
        //     remove up-facing tris only BELOW Y=-50, catching just those without touching the real floor.
        //     Together the two nodes hold all 4 crater-floor corners.
        // Each such tri lives in a primType-3 triangle LIST (independent tris), so collapsing its two trailing
        // index-records onto the first yields a zero-area triangle the GS discards — no strip/layout disturbance.
        // Record stride is 3 or 4 ints depending on whether the mesh carries a per-vertex colour block (see the
        // stride computed below); s04g0117__s1 is a 4-int-record "variant" mesh. Must run BEFORE
        // CullUpperCraterWalls (which renames the upper rings' `__n` suffix to `__s`).
        static readonly (string node, double yMax)[] CORNER_TRI_NODES = {
            ("s040101__s", 1e9), ("s04g0102__s", 1e9), ("s04g0103__s", 1e9), ("s04g0104__s", 1e9),
            ("s04g0105__n", 1e9), ("s04g0106__n", 1e9), ("s04g0107__n", 1e9), ("s04g0108__n", 1e9),
            ("s04g0109__n", 1e9), ("s04g0110__n", 1e9), ("s04g0111__n", 1e9), ("s04g0112__n", 1e9),
            ("s04g0113__n", 1e9), ("s04g0114__n", 1e9), ("s04g0115__n", 1e9), ("s04g0116__n", 1e9),
            ("s04g0117__s", -50.0), ("s04g0117__s1", -50.0),
        };

        static byte[] RemoveRingCornerTris(byte[] scene)
        {
            foreach (var (node, yMax) in CORNER_TRI_NODES)
            {
                int mdt = FindMdt(scene, node);
                uint dl = BitConverter.ToUInt32(scene, mdt + 10 * 4);
                int vcount = (int)BitConverter.ToUInt32(scene, mdt + 3 * 4);           // hw[3] = vertex count
                int vbase = mdt + (int)BitConverter.ToUInt32(scene, mdt + 4 * 4);      // hw[4] = vertex-block offset
                uint hw8 = BitConverter.ToUInt32(scene, mdt + 8 * 4);                  // colour block offset, or 0xffffffff
                int rb = (hw8 != 0xffffffff && hw8 > 0 ? 4 : 3) * 4;                   // record size in bytes (4-int if colour)
                int numsub = (int)BitConverter.ToUInt32(scene, (int)(mdt + dl + 8));   // submesh count
                int o = (int)(dl + 0x10);
                for (int sm = 0; sm < numsub; sm++)
                {
                    int prim = BitConverter.ToInt32(scene, mdt + o);
                    int vcnt = BitConverter.ToInt32(scene, mdt + o + 4);
                    o += 0xC;
                    int recbase = mdt + o;                                             // first index-record of this submesh
                    o += vcnt * rb;
                    if (prim != 3) continue;                                           // only the triangle LIST holds them
                    for (int k = 0; k + 2 < vcnt; k += 3)
                    {
                        int i0 = BitConverter.ToInt32(scene, recbase + (k + 0) * rb);
                        int i1 = BitConverter.ToInt32(scene, recbase + (k + 1) * rb);
                        int i2 = BitConverter.ToInt32(scene, recbase + (k + 2) * rb);
                        if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vcount || i1 >= vcount || i2 >= vcount) continue;
                        if (i0 == i1 || i1 == i2 || i0 == i2) continue;
                        double cyc = (F(scene, vbase + i0 * 0x10 + 4) + F(scene, vbase + i1 * 0x10 + 4) + F(scene, vbase + i2 * 0x10 + 4)) / 3.0;
                        if (cyc >= yMax) continue;                                      // only strays below the cutoff
                        if (!UpFacing(scene, vbase, i0, i1, i2)) continue;
                        U32(scene, recbase + (k + 1) * rb, (uint)i0);                  // collapse -> zero-area tri
                        U32(scene, recbase + (k + 2) * rb, (uint)i0);
                    }
                }
            }
            return scene;
        }

        // True if triangle (i0,i1,i2) faces straight up. Verts are LOCAL XYZW floats at vbase + idx*0x10;
        // these nodes have identity rotation, so a local +Y normal is a world +Y normal.
        static bool UpFacing(byte[] s, int vbase, int i0, int i1, int i2)
        {
            float ax = F(s, vbase + i0 * 0x10), ay = F(s, vbase + i0 * 0x10 + 4), az = F(s, vbase + i0 * 0x10 + 8);
            float bx = F(s, vbase + i1 * 0x10), by = F(s, vbase + i1 * 0x10 + 4), bz = F(s, vbase + i1 * 0x10 + 8);
            float cx = F(s, vbase + i2 * 0x10), cy = F(s, vbase + i2 * 0x10 + 4), cz = F(s, vbase + i2 * 0x10 + 8);
            double nx = (by - ay) * (cz - az) - (bz - az) * (cy - ay);
            double ny = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
            double nz = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return len > 0 && ny / len > 0.9;
        }

        static float F(byte[] b, int o) => BitConverter.ToSingle(b, o);

        static int FindMdt(byte[] scene, string node)
        {
            int at = Find(scene, Encoding.Latin1.GetBytes(node + "\0"));               // node name lives at node+8
            if (at < 0) throw new IOException($"ring node '{node}' not found in scene.scn");
            int meshOff = BitConverter.ToInt32(scene, (at - 8) + 0x28);                // meshOff at node+0x28
            int mds = FindLast(scene, Encoding.ASCII.GetBytes("MDS\0"), at);           // owning MDS block base
            foreach (int cand in new[] { meshOff, mds + meshOff })                     // meshOff: absolute or block-relative
                if (cand > 0 && cand < scene.Length - 3 && scene[cand] == 'M' && scene[cand + 1] == 'D' && scene[cand + 2] == 'T')
                    return cand;
            throw new IOException($"MDT for '{node}' not resolved");
        }

        static byte[] BuildInjectedMapinfo(byte[] cfg, int x, int y, int z, int ry, string anchorPart, string atari = "",
                                           string partName = "kanban")
        {
            string t = Encoding.Latin1.GetString(cfg);
            // Slot 5 (after name + level1/2/3 + one blank) is the `_a` (atari/collision) mesh — matches how
            // native GROUND blocks reference e.g. "e03g04_a.mds".
            // Number format MUST match native exactly — "N,\tN,\tN" (comma immediately after each value,
            // THEN a tab). The earlier "N\t,N\t,N" (tab before comma) parsed positions but corrupted the
            // rotation Y for injected entries, leaving the canal sign stuck facing east regardless of ry.
            string blk = "\r\n\tGROUND\t\"" + partName + "\",\t\t//injected part\r\n"
                       + "\t\t\"\",\t\t\t//level1\r\n\t\t\"\",\t\t\t//level2\r\n\t\t\"\",\t\t\t//level3\r\n"
                       + "\t\t\"\",\t\t\t//\r\n\t\t\"" + atari + "\",\t\t\t//atari\r\n\t\t\"\",\t\t\t//\r\n\t\t\"\",\t\t\t//?\r\n"
                       + $"\t\t{x},\t{y},\t{z},\t//position\r\n\t\t0,\t{ry},\t0\t//rotation\r\n";
            var matches = Regex.Matches(t, "\\tGROUND\\t\"" + Regex.Escape(anchorPart) + "\",.*?\\r\\n\\t\\t-?\\d[^\\r\\n]*\\r\\n\\t\\t\\d[^\\r\\n]*,[^\\r\\n]*\\r\\n", RegexOptions.Singleline);
            if (matches.Count == 0) throw new IOException($"no GROUND {anchorPart} block found in mapinfo.cfg");
            int ins = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length;
            return Encoding.Latin1.GetBytes(t.Substring(0, ins) + blk + t.Substring(ins));
        }

        /// <summary>Carve a part's 0x160-byte PTS header out of a scene.scn (used as the kanban template).</summary>
        static byte[] PartHeader(byte[] scene, string partName)
        {
            int n = (int)U32(scene, 4);
            for (int i = 0; i < n; i++)
            {
                int e = 0x10 + i * 0x30;
                if (NameAt(scene, e, 0x10) == partName)
                {
                    int off = (int)U32(scene, e + 0x10);
                    return new ArraySegment<byte>(scene, off, 0x160).ToArray();
                }
            }
            throw new IOException($"template part {partName} not found in scene.scn");
        }

        static string NameAt(byte[] b, int o, int max) { int e = Array.IndexOf(b, (byte)0, o, max); if (e < 0) e = o + max; return Encoding.Latin1.GetString(b, o, e - o); }

        // ── ELF boot-cave patch + new PCSX2 CRC ──
        const int zero = 0, v0 = 2, a0 = 4, a1 = 5, a2 = 6, a3 = 7, t0 = 8, sp = 29;
        static uint Lui(int rt, uint i) => 0x3C000000u | ((uint)rt << 16) | (i & 0xFFFF);
        static uint Ori(int rt, int rs, uint i) => 0x34000000u | ((uint)rs << 21) | ((uint)rt << 16) | (i & 0xFFFF);
        static uint Lw(int rt, int o, int b) => 0x8C000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        static uint Sw(int rt, int o, int b) => 0xAC000000u | ((uint)b << 21) | ((uint)rt << 16) | (uint)(o & 0xFFFF);
        static uint Addiu(int rt, int rs, int i) => 0x24000000u | ((uint)rs << 21) | ((uint)rt << 16) | (uint)(i & 0xFFFF);
        static uint Move(int rd, int rs) => Ori(rd, rs, 0);
        static uint Jal(uint tgt) => 0x0C000000u | ((tgt >> 2) & 0x03FFFFFF);
        static uint J(uint tgt) => 0x08000000u | ((tgt >> 2) & 0x03FFFFFF);

        static byte[] BuildCave()
        {
            uint[] w = {
                Addiu(sp, sp, -0x20), Sw(a0, 0x14, sp), Sw(a1, 0x18, sp),
                Move(a0, a1), Lui(a1, STR_VA >> 16), Ori(a1, a1, STR_VA & 0xFFFF), Addiu(a2, zero, 0),
                Jal(GetPackFile), 0,
                Lui(t0, DIAG_VA >> 16), Sw(v0, (int)(DIAG_VA & 0xFFFF), t0),
                Move(a1, v0), Lui(a0, SysTexMgr >> 16), Ori(a0, a0, SysTexMgr & 0xFFFF),
                Addiu(a2, zero, -1), Addiu(a3, zero, 0), Addiu(t0, zero, 0),
                Jal(EnterIMGFile), 0,
                Lw(a0, 0x14, sp), Lw(a1, 0x18, sp), Addiu(a2, zero, 0),
                Jal(LoadFile), 0,
                Addiu(sp, sp, 0x20), J(REJOIN_VA), 0,
            };
            var b = new byte[w.Length * 4];
            for (int i = 0; i < w.Length; i++) Array.Copy(BitConverter.GetBytes(w[i]), 0, b, i * 4, 4);
            if (b.Length > CAVE_LEN) throw new InvalidOperationException($"cave {b.Length}B > {CAVE_LEN}B");
            return b;
        }

        // Opt-in: bake the native occlusion-camera prototype instead of the C#-driver decouple. When true,
        // ALSO set TownCamera.Enabled=false (the C# driver must not run alongside it). See PatchNativeCameraPostPass.
        internal static bool EnableNativeCameraPrototype = true;

        static uint ElfPatchAndCrc(FileStream fs, Rec elf)
        {
            long elfIso = (long)elf.Ext * SECTOR;
            byte[] eh = Rd(fs, elfIso, 0x34);
            uint phoff = U32(eh, 0x1c); ushort phent = BitConverter.ToUInt16(eh, 0x2a), phnum = BitConverter.ToUInt16(eh, 0x2c);
            long pOff = -1, pVa = -1;
            for (int i = 0; i < phnum; i++)
            {
                byte[] ph = Rd(fs, elfIso + phoff + i * phent, 24);
                uint typ = U32(ph, 0), off = U32(ph, 4), va = U32(ph, 8), fsz = U32(ph, 16);
                if (typ == 1 && fsz > 0 && va <= DETOUR_VA && DETOUR_VA < va + fsz) { pOff = off; pVa = va; break; }
            }
            if (pOff < 0) throw new IOException("No PT_LOAD covers the patch site — wrong ISO/version.");
            long ElfOff(uint va) => elfIso + pOff + (va - pVa);

            byte[] cave = BuildCave();
            if (RdU32(fs, ElfOff(DETOUR_VA)) != Jal(LoadFile) || RdU32(fs, ElfOff(DETOUR_VA + 4)) != 0)
                throw new IOException("Boot-loader patch site is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            byte[] caveWas = Rd(fs, ElfOff(CAVE_VA), cave.Length);
            foreach (byte x in caveWas) if (x != 0) throw new IOException("Boot-cave region not empty — unexpected ISO.");

            Wr(fs, ElfOff(STR_VA), Encoding.ASCII.GetBytes("fishsign.img\0"));
            Wr(fs, ElfOff(CAVE_VA), cave);
            WrU32(fs, ElfOff(DETOUR_VA), J(CAVE_VA));

            PatchFishingLoadFish(fs, ElfOff);
            PatchFishBox(fs, ElfOff);

            // ── Camera: rewritten from scratch (C# prototype era — TownCamera.cs, since deleted; superseded by the
            //    the 4-byte DECOUPLE below; everything else (orbit, collision) lives in C#. Old EdMoveChara-tweak
            //    patches were removed 2026-07 (recoverable via git). Fishing-shot recenter kept.
            if (EnableNativeCameraPrototype)
                PatchNativeCameraPostPass(fs, ElfOff);   // native occlusion-camera prototype (lets vanilla + Step run)
            else
                PatchDecoupleCamera(fs, ElfOff);         // NOP FollowOn → MainCamera stays follow-OFF → C# owns it
            PatchFishingCameraTarget(fs, ElfOff);        // center the fishing shot on the bobber (kept)

            byte[] pelf = Rd(fs, elfIso, (int)elf.Size);
            uint crc = 0;
            for (int i = 0; i < pelf.Length / 4; i++) crc ^= U32(pelf, i * 4);
            return crc;
        }

        // ── FishingLoadFish species-selection rewrite (baked, race-free) ─────────────────────────────
        // Densely rewrites the per-slot species selector [0x1a8a48,0x1a8d44) so the LOADER itself hands
        // back the right fish for every area — including the mod's custom towns (dedicated areas 5/6/7 =
        // Brownboo/Queens/Yellow Drops) — with no runtime re-species and thus no race. Native areas 0-4
        // keep their exact distributions, with two requested vanilla edits folded in: area 2 (Matataki)
        // Gummy->Niler and area 3 (East Harbor) Piccoly->Gobbler. Equal-weight pools are `rand%N -> byte
        // table` lookups, so adding a fish later is one table byte + bumping N (212 bytes of nop headroom
        // remain in-region). Assembled by tools/iso_patch/asm_fishpools.py; the full original and new
        // listings live in game_data/docs/fishing-loadfish-re.md.
        static void PatchFishingLoadFish(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint REGION_VA = 0x001A8A48;   // start of the per-slot species-selection region
            // vanilla anchors spread across the region — reject a non-vanilla / already-patched ISO
            foreach (var (va, word) in new (uint va, uint word)[]
            {
                (0x001A8A48, 0x2413FFFF),   // addiu $s3,$zero,-1   (default species; becomes `jal rand`)
                (0x001A8A8C, 0x100000AD),   // b 0x1a8d44           (old area-dispatch fall-through)
                (0x001A8D40, 0x24130010),   // addiu $s3,$zero,0x10 (last native leaf — Heela)
            })
                if (RdU32(fs, ElfOff(va)) != word)
                    throw new IOException($"FishingLoadFish region 0x{va:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");

            uint[] region =
            {
                0x0C0411BE, 0x2413FFFF, 0x24030005, 0x12C3005C, 0x24030006, 0x12C3006B,
                0x24030007, 0x12C3006C, 0x24030001, 0x12C30011, 0x24030002, 0x12C30023,
                0x24030003, 0x12C30031, 0x24030004, 0x12C3003A, 0x00000000, 0x24030004,
                0x0043001A, 0x3C01001A, 0x34218AB0, 0x00001010, 0x00220821, 0x90330000,
                0x100000A6, 0x00000000, 0x07060201, 0x24030064, 0x0043001A, 0x00000000,
                0x00000000, 0x00001010, 0x24130001, 0x28410023, 0x14200065, 0x00000000,
                0x24130004, 0x28410046, 0x14200061, 0x00000000, 0x24130009, 0x28410050,
                0x1420005D, 0x00000000, 0x2413000A, 0x10000091, 0x00000000, 0x0050001A,
                0x00000000, 0x00000000, 0x00001810, 0x1060004A, 0x00000000, 0x24030003,
                0x0043001A, 0x3C01001A, 0x34218B40, 0x00001010, 0x00220821, 0x90330000,
                0x10000082, 0x00000000, 0x00070402, 0x24030005, 0x0043001A, 0x3C01001A,
                0x34218B68, 0x00001010, 0x00220821, 0x90330000, 0x10000078, 0x00000000,
                0x0C010300, 0x0000000D, 0x0050001A, 0x00000000, 0x00000000, 0x00001810,
                0x1060002F, 0x00000000, 0x24030064, 0x0043001A, 0x00000000, 0x00000000,
                0x00001010, 0x2413000E, 0x28410028, 0x14200030, 0x00000000, 0x2413000F,
                0x28410046, 0x1420002C, 0x00000000, 0x24130010, 0x10000060, 0x00000000,
                0x24030032, 0x0043001A, 0x00000000, 0x00000000, 0x00001810, 0x10600018,
                0x00000000, 0x24030004, 0x0043001A, 0x3C01001A, 0x34218C08, 0x00001010,
                0x00220821, 0x90330000, 0x10000050, 0x00000000, 0x060E0B0B, 0x24130000,
                0x1000004C, 0x00000000, 0x24030004, 0x0043001A, 0x3C01001A, 0x34218C3C,
                0x00001010, 0x00220821, 0x90330000, 0x10000043, 0x00000000, 0x0C0E020A,
                0x0C0411BE, 0x24030005, 0x0043001A, 0x00000000, 0x00001010, 0x24130005,
                0x24030011, 0x0062980A, 0x10000038, 0x00000000, 0x10000036, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000,
            };
            for (int i = 0; i < region.Length; i++)
                WrU32(fs, ElfOff(REGION_VA + (uint)i * 4), region[i]);

            // FishNum per area: default 6; area 0 -> 4 (native override kept); area 4 -> 5. The area-4
            // compare instruction @0x1a8998 doubles as the stored value, so the compare const stays 4 and
            // the `5` is written into that branch's delay slot @0x1a89a0 (a nop in vanilla).
            if (RdU32(fs, ElfOff(0x001A8980)) != 0x24020005)
                throw new IOException("FishNum default site 0x1A8980 is not vanilla.");
            WrU32(fs, ElfOff(0x001A8980), 0x24020006);          // FishNum default 5 -> 6
            if (RdU32(fs, ElfOff(0x001A89A0)) != 0x00000000)
                throw new IOException("FishNum area-4 delay slot 0x1A89A0 is not a nop.");
            WrU32(fs, ElfOff(0x001A89A0), 0x24020005);          // area 4 (Muska Lacka) -> 5 (bne delay slot)
        }

        // ── Fish collision-gather box: symmetrise +Z so thin axis-aligned +Z walls contain fish ─────────
        // Step__5CFish (0x240480) builds the AABB it hands PickUpNearPoly as max=(x+10,y+10,z),
        // min=(x-10,y,z-10) — it reaches 10u toward -Z but 0 toward +Z. So a razor-thin, axis-aligned +Z
        // wall (the Queens south canal wall) is never gathered until the fish is already through it and the
        // fish swim straight past it (north/-Z walls, and all of vanilla's thick/angled terrain, are caught
        // fine). Fix: make max.z = z+10. The build loads its 10.0 with a 2-op lui/mtc1; swapping that for a
        // 1-op $gp load of the rodata 10.0 (@0x2A22F8 = _gp(0x2A97F0)-0x74F8) frees exactly the one slot
        // needed to add +10 to max.z — a same-length in-place rewrite, no code cave. Register use is
        // otherwise identical to vanilla ($f1=10, $f2/$f3/$f4=x/y/z, $f0 scratch; $v0 no longer needed).
        static void PatchFishBox(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint SITE = 0x00240B24;   // start of the box-build in Step__5CFish
            uint[] vanilla =
            {
                0x3C024120, 0x44820800, 0xC7A20040, 0x46020800, 0xE7A05090,
                0xC7A30044, 0x46030800, 0xE7A05094, 0xC7A40048, 0xE7A45098,
                0x46011001, 0xE7A050A0, 0xE7A350A4, 0x46012001, 0xE7A050A8,
            };
            uint[] patched =
            {
                0xC7818B08,   // lwc1  $f1, -0x74f8($gp)   ; f1 = 10.0 (rodata) — was lui $v0,0x4120
                0xC7A20040,   // lwc1  $f2, 0x40($sp)      ; x        (freed slot)
                0x46020800,   // add.s $f0, $f1, $f2       ; x+10
                0xE7A05090,   // swc1  $f0, 0x5090($sp)    ; max.x
                0xC7A30044,   // lwc1  $f3, 0x44($sp)      ; y
                0x46030800,   // add.s $f0, $f1, $f3       ; y+10
                0xE7A05094,   // swc1  $f0, 0x5094($sp)    ; max.y
                0xC7A40048,   // lwc1  $f4, 0x48($sp)      ; z
                0x46040800,   // add.s $f0, $f1, $f4       ; z+10   ← the fix
                0xE7A05098,   // swc1  $f0, 0x5098($sp)    ; max.z = z+10
                0x46011001,   // sub.s $f0, $f2, $f1       ; x-10
                0xE7A050A0,   // swc1  $f0, 0x50a0($sp)    ; min.x
                0xE7A350A4,   // swc1  $f3, 0x50a4($sp)    ; min.y = y
                0x46012001,   // sub.s $f0, $f4, $f1       ; z-10
                0xE7A050A8,   // swc1  $f0, 0x50a8($sp)    ; min.z = z-10
            };
            for (int i = 0; i < vanilla.Length; i++)
                if (RdU32(fs, ElfOff(SITE + (uint)i * 4)) != vanilla[i])
                    throw new IOException($"Fish collision-box site 0x{SITE + (uint)i * 4:X} is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            for (int i = 0; i < patched.Length; i++)
                WrU32(fs, ElfOff(SITE + (uint)i * 4), patched[i]);
        }

        // ── Camera DECOUPLE: turn EdMoveChara's per-frame FollowOn into FollowOff so C# owns MainCamera ──
        // The from-scratch camera (TownCamera.cs, runtime) drives MainCamera by writing its pos/ref. For that to
        // stick, MainCamera must be follow-OFF every frame: with follow-enable (+0x2e0)==0, Step__CCameraFollow
        // ignores the follow FIELDS (dist/angle/height EdMoveChara keeps writing) and just eases current pos/ref
        // (+0x260/+0x270) toward next (+0x280/+0x290) — which C# sets.
        //   ⚠ NOP-ing FollowOn is NOT enough: it only stops RE-enabling follow, it doesn't CLEAR +0x2e0 if it's
        //   already 1 (which it is at town load). So replace EdMoveChara's lone `jal FollowOn` (0x124B00) with
        //   `jal FollowOff` (0x124B10) — same arg (the camera) — so EdMoveChara ACTIVELY clears +0x2e0 every frame
        //   BEFORE Step runs. Deterministic, no async race with our runtime writes. Covers walk AND fishing.
        static void PatchDecoupleCamera(FileStream fs, Func<uint, long> ElfOff)
        {
            const uint VA = 0x0016AC84;          // the lone `jal FollowOn__13CCameraFollow` (0x124B00) in EdMoveChara
            if (RdU32(fs, ElfOff(VA)) != 0x0C0492C0)
                throw new IOException($"Camera-decouple site 0x{VA:X} is not vanilla jal FollowOn — unmodified Dark Cloud (USA) ISO expected.");
            WrU32(fs, ElfOff(VA), 0x0C0492C4);   // jal FollowOff (0x124B10) → clears follow every frame, before Step
        }

        // ── NATIVE camera — ELF port, LEVERAGE vanilla's own controller + surgical edits ─────────────
        // Opt-in (EnableNativeCameraPrototype). We DON'T reimplement the camera: EdMoveChara already has a full
        // controller (decompile line 583) — right-stick rotation (AddAngle), stick-Y height (AddHeight), L1/R1
        // rotation (PadOn 8/4), auto-follow-behind when idle, distance/height clamping. We surgically fix the two
        // things that made it feel bad:
        //   1. Its rotation is COLLISION-GATED by bVar4(=s6)/bVar5(=s4) — "is the right/left side clear of walls"
        //      — so it refuses to rotate INTO a wall (the original "bounce"/stuck feel). Those flags are cleared
        //      only at 0x16B1E8 (`clear s6`, right within 5u) and 0x16B2FC (`clear s4`, left within 5u); NOP both
        //      → flags stay true → FREE rotation everywhere. (Stage 2 adds smooth pull-in to replace the gating.)
        //   2. Stub CheckCameraWidth (single caller 0x16AF98) → its width-slide AND the bVar gating inside that
        //      block are disabled; its `if (result != 0)` block just never fires.
        // Everything else stays vanilla (this REPLACES PatchDecoupleCamera; the old C# TownCamera driver is deleted).
        // Reversible: restore the guarded vanilla words. Addresses RE'd via Ghidra-EE (docs/town-camera-elf-port-plan).
        static void PatchNativeCameraPostPass(FileStream fs, Func<uint, long> ElfOff)
        {
            // CLEAN FREE-CAMERA BASE + STAGE-2 PULL-IN: strip ALL of vanilla's camera collision, then add our OWN
            // smooth pull-in with a space buffer. Sites guarded before any write. (To A/B vanilla's orbit-the-hit-point
            // slide instead, drop the CameraAutoMove stub and NOP its AddAngle 0x169CF8/D14 + AddHeight 0x169D50.)
            const uint STUB_VA  = 0x0014B830;   // CheckCameraWidth entry           (guard addiu sp,-0x100)
            const uint BVAR4_VA = 0x0016B1E8;   // `clear s6` (bVar4=false, right blocked → gates rotation)
            const uint BVAR5_VA = 0x0016B2FC;   // `clear s4` (bVar5=false, left  blocked → gates rotation)
            const uint CAM_VA   = 0x00169B70;   // CameraAutoMove entry (slide/auto-follow/rotate/height-when-close)
            const uint VADJ_VA  = 0x0016B6A8;   // vertical-adjust SetHeight (raise to clear a ceiling within 18u)
            const uint RSTD_VA  = 0x0016B724;   // reset: SetDistance(near)   \ fires when clipped-close or too-high
            const uint RSTH_VA  = 0x0016B738;   // reset: SetHeight(10)       |  → snaps + flips the camera to the
            const uint RSTA_VA  = 0x0016B764;   // reset: AddAngle(flip)      / opposite side of the player
            const uint HFLOOR_SNAP_VA = 0x0016BC54; // floor-snap SetHeight(5/35): clamps height UP to 5 when it's lower,
                                                    // which blocks the ground-relative DROP (camera below the player when
                                                    // he walks uphill). NOP so height can go < 5 / negative; our baseline
                                                    // keeps the eye BASE_H above the ground under it, so it never clips.
            const uint STICKY1_VA = 0x0016B834; // vanilla stick-Y AddHeight (accumulative, height<30 branch) — replaced by
            const uint STICKY2_VA = 0x0016B84C; //   our deadzoned absolute stick offset in the pull-in. NOP both.
            const uint PULLIN_VA = 0x0014B838;
            const uint HOOK_VA  = 0x0016B5DC;   // jal CheckHitVertical → retarget to our pull-in (s5=buf,s8=count live)
            // Town camera clamps distance to [near=70, far=80] AFTER our hook, easing our pull-in back UP to 70 every
            // frame (line 618-621) — so the pull-in "mostly passes through". NOP the near-clamp + the bVar6 SetDistance
            // so our pull-in owns +0x2D0. (Far-clamp 0x16B994 left as a safety; we already clamp target ≤ BASE 80.)
            const uint NCLAMP_VA = 0x0016B9E8;  // AddDistance(+(70-dist)/10) near-clamp → fights pull-in
            const uint SDST_VA   = 0x0016BBCC;  // if(bVar6) SetDistance(70) → hard-resets distance
            // The "too close" reset block (fires when dist < near*0.5 = 35, CONSTANT in the canal) snapped 4 things;
            // we NOPped its SetDistance/SetHeight/AddAngle (RSTD/RSTH/RSTA) but MISSED the SetAngleSoon @0x16B754 —
            // which sets rendered-angle(+0x2DC)=target(+0x2D8), KILLING the angle easing every frame we're pulled in.
            // That snap (size = the easing lag, so bigger the faster you rotate) was the reproducible slide jump. NOP it.
            const uint SANGLE_VA  = 0x0016B754;  // reset-block SetAngleSoon → kills angle easing (the slide jump)
            const uint SANGLE2_VA = 0x0016B7A8;  // sibling SetAngleSoon (MapNo 0x23 only; NOP for consistency)
            // The reset block's LAST effect: a vtable call (**(cam+0x2B8)+8)(cam,-1) = Step(cam,-1), whose param_2<0
            // path does +0x2DC=+0x2D8 — ANOTHER hard angle snap. Fires when dist<35 (constant while pulled in). CSV
            // proved it: rendered angle catches up ~2.4° the frame dist crosses 35. NOP the jalr → block fully inert.
            const uint RSTVT_VA   = 0x0016B7C0;  // reset-block jalr t9 = Step(cam,-1) → snaps +0x2DC (the residual jump)

            void Guard(uint va, uint want, string what) {
                if (RdU32(fs, ElfOff(va)) != want)
                    throw new IOException($"Native-camera site 0x{va:X} ({what}) not vanilla — unmodified Dark Cloud (USA) ISO expected.");
            }
            Guard(STUB_VA,  0x27BDFF00, "CheckCameraWidth");
            Guard(BVAR4_VA, 0x7000B628, "rotation-gate R"); Guard(BVAR5_VA, 0x7000A628, "rotation-gate L");
            Guard(CAM_VA,   0x27BDFFA0, "CameraAutoMove");
            Guard(VADJ_VA,  0x0C0492EC, "vertical SetHeight");
            Guard(RSTD_VA,  0x0C0492DC, "reset SetDistance"); Guard(RSTH_VA, 0x0C0492EC, "reset SetHeight");
            Guard(RSTA_VA,  0x0C0492D4, "reset AddAngle");
            // Camera scratch (stick ease @0x01F10040, E_prev quad @0x01F10050) lives on the MAILBOX DATA page —
            // boot-zeroed heap, no ELF init needed/possible; moved off the code page so per-frame writes stop
            // forcing PCSX2 to re-JIT the camera function (see CodeCaveAddresses cave map).
            Guard(HOOK_VA,  0x0C052820, "pull-in hook (jal CheckHitVertical)");
            Guard(NCLAMP_VA, 0x0C0492E4, "distance near-clamp"); Guard(SDST_VA, 0x0C0492DC, "bVar6 SetDistance");
            Guard(SANGLE_VA, 0x0C0492CC, "reset SetAngleSoon"); Guard(SANGLE2_VA, 0x0C0492CC, "reset SetAngleSoon(map)");
            Guard(RSTVT_VA, 0x0320F809, "reset vtable Step(-1)");

            WrU32(fs, ElfOff(STUB_VA + 0), 0x03E00008);   // CheckCameraWidth → jr ra
            WrU32(fs, ElfOff(STUB_VA + 4), 0x00001021);   //   addu v0,zero,zero (return 0 → width-slide off)
            WrU32(fs, ElfOff(BVAR4_VA), 0x00000000);      // free rotation right (bVar4 stays true)
            WrU32(fs, ElfOff(BVAR5_VA), 0x00000000);      // free rotation left  (bVar5 stays true)
            WrU32(fs, ElfOff(CAM_VA + 0), 0x03E00008);    // CameraAutoMove → jr ra (no collision slide/rotate/height)
            WrU32(fs, ElfOff(CAM_VA + 4), 0x00000000);    //   nop (delay slot)
            WrU32(fs, ElfOff(VADJ_VA), 0x00000000);       // no ceiling height-rise ("angle goes up")
            WrU32(fs, ElfOff(RSTD_VA), 0x00000000);       // no reset distance snap
            WrU32(fs, ElfOff(RSTH_VA), 0x00000000);       // no reset height snap
            WrU32(fs, ElfOff(RSTA_VA), 0x00000000);       // no reset angle flip ("reset to opposite side")
            WrU32(fs, ElfOff(NCLAMP_VA), 0x00000000);     // no near-clamp ease-up (was forcing dist back to 70)
            WrU32(fs, ElfOff(SDST_VA), 0x00000000);       // no bVar6 SetDistance(70) hard-reset
            WrU32(fs, ElfOff(SANGLE_VA), 0x00000000);     // no reset SetAngleSoon → angle easing survives (fixes slide jump)
            WrU32(fs, ElfOff(SANGLE2_VA), 0x00000000);    // no sibling SetAngleSoon (MapNo 0x23)
            WrU32(fs, ElfOff(RSTVT_VA), 0x00000000);      // no reset Step(cam,-1) → +0x2DC angle snap gone (block inert)
            Guard(HFLOOR_SNAP_VA, 0x0C0492EC, "height floor-snap SetHeight");
            Guard(0x0016BC0C, 0x0C0492EC, "height CEILING snap SetHeight(60)");   // vanilla snaps +0x2D4 down to 60 every frame it
            WrU32(fs, ElfOff(0x0016BC0C), 0x00000000);                            //   exceeds 60 — capped EVERY tall-cliff mechanism
            WrU32(fs, ElfOff(HFLOOR_SNAP_VA), 0x00000000); // no floor-snap → height can drop below 5 (camera below player)
            Guard(STICKY1_VA, 0x0C0492F4, "vanilla stick-Y AddHeight #1");
            Guard(STICKY2_VA, 0x0C0492F4, "vanilla stick-Y AddHeight #2");
            WrU32(fs, ElfOff(STICKY1_VA), 0x00000000);    // our deadzoned stick offset replaces the vanilla accumulative one
            WrU32(fs, ElfOff(STICKY2_VA), 0x00000000);

            // ── STAGE 2: our own smooth pull-in (hosted in the reclaimed CheckCameraWidth slack) ──
            // Wraps `jal CheckHitVertical` @0x16B5DC (s5=CCPoly buffer, s8=poly count live in callee-saved regs):
            // calls the real CheckHitVertical (transparent, returns its v0), then reads MainCamera ref(+0x2C0)/
            // angle(+0x2D8)/height(+0x2D4), casts the 3D sightline ray from ref up to (ref.y+height) at BASE=80 in the
            // angle(+0x2D8) direction, CheckHit(s5,s8,rayFrom→rayTo, mode=0). (Look-ahead removed: it didn't flatten the
            // target discontinuity AND broke on the +0x2D8 wrap flip -π..π ↔ 0..2π making the lead ≈±2π garbage.) mode=0 is
            // critical: it hits ALL polys — mode=1 skipped the tall area-dividing walls (attribute bit 0) while
            // buildings (bit clear) still registered, so the camera passed through walls. AND param_6=1 (NEAREST):
            // CheckHit with param_6=0 returns the FIRST poly in buffer order, which on our 80u ray is often a far
            // one (dist≥80 → clamps to base → no pull-in); param_6=1 tracks the nearest hit. On hit, shrinks
            // distance(+0x2D0) to horiz(hit)−MARGIN(8, the space buffer) — floored at MIN 12 (target ≤72<BASE so no
            // max-clamp needed), then eased toward it at 0.15 SYMMETRIC (fast-in disabled — testing; planned: slow the
            // camera rotation when it would clip instead of snapping in, + height-rise when MIN can't be held).
            // MIN must stay UNDER the wall distance or the floor lands the camera PAST close walls (Queens canal walls
            // ~19-34u out). Trade-off: low MIN lets the camera near the player at buildings — real fix = height-rise
            // (go OVER when forced close), TODO. ⚠ EE-ASM GOTCHAS baked in (see mips_asm.py): FP compares use c.OLT.s
            // (.word 0x46..0034) NOT keystone c.lt.s (0x3C — the R5900 doesn't set cc for it); a nop follows every
            // mtc1 and every FP compare (two EE latency hazards). One-frame-stale angle (line 583 after) imperceptible.
            // ===== CAMERA TUNABLES (tools/town_camera_collision.s) — edit these; they inject into the template's constant slots
            //       below on each patch (no asm regen). PutVal = single `lui` (integer / .25 step, low16==0);
            //       PutEase = `lui`+`ori` (any float). Indices auto-located from the source; guards trip loudly on drift.
            // ARCHITECTURE: dist target = BASE_DIST always (no wall-ray pull-in). Height target = REST_H + stick;
            //   ceiling probe DUCKS it (tunnel), ground probe FLOORS it (hard, world-space). ALL height motion is
            //   RATE-LIMITED: falls ≤ H_FALL_RATE/frame in WORLD Y (cameraHeight.bin cave sub @0x27D090 — a falling
            //   player outruns the camera; WARP_BREAK skips the bound across true warps), climb rises ≤ CLIMB_RISE/
            //   frame (anchored to last APPLIED height, not the eased value — the ease decay otherwise eats the rise).
            //   The height sub also owns the CLIFF logic: pinned+occluded → the boom glides toward the player at
            //   GROUND_GLIDE_K·current/frame (progressive ratchet over the lip; floor GLIDE_MIN_DIST), and while
            //   descending (excess > DESCENT_HOLD) the boom may shorten but never extend (kills the lip-crossover
            //   bounce). The SWEPT-SLIDE (persisted origin E_prev @0x01F10050, mailbox data page — off the code page
            //   so PCSX2 doesn't re-JIT per frame) resolves wall contact on the authored-normal side via the weighted
            //   d/h/θ decomposition (SLIDE_BIAS = angle share); |n_t|-scaled friction (head-on undamped →
            //   SLIDE_FRICTION keep at full tangency); θ REACQUISITION (SLIDE_GAIN, stick-gated) slides toward rest;
            //   occlusion-gated GEOMETRIC CLIMB h = REST_H + CLIMB_K·(BASE−d')² (LOS pivot→E_prev, 5th cast, flag
            //   @0x54(sp) — NOT 0x98, the corner-verify spill slot); CORNER VERIFY resolves a second plane (min-norm)
            //   for concave seams. Vanilla height clamps NOP'd BOTH ways (floor snap 0x16BC54 + ceiling snap 0x16BC0C
            //   — the 60-unit ceiling silently capped every tall-cliff mechanism until found). ⚠ EE gotchas:
            //   c.OLT.s/sqrt.s/max.s/min.s are .word-encoded — DERIVE from the formula, fd is bits 10:6 (the fd=31
            //   no-op bug); nop after mtc1/FP-compare; CheckHit args 5-7 = REGISTERS t0/t1/t2 (hitOut/mode/skip) —
            //   set explicitly at every cast, NEVER inherit (stale t2 = the mask-skipping saga). [[native-camera-functions]]
            const float BASE_DIST   = 80f;   // resting orbit distance when nothing blocks
            const float REST_H      = 5f;   // resting eye height above the pivot (flat — no slope-rise/climb anymore)
            const float CEIL_DIST   = 80f;  // how far UP the ceiling probe looks for a tunnel roof to duck under
            const float MIN_CEIL_CLEAR = 4f;// eye stays this far BELOW a detected ceiling (tunnel duck depth)
            const float STICK_DEADZONE = 0.3f; // right-stick Y below this |deflection| (0..1) does nothing
            const float STICK_SCALE    = -25f; // manual height offset at full stick deflection (up = raise)
            const float STICK_EASE     = 0.1f; // per-frame ease of the stick offset (LOW = gentle onset)
            const float HEIGHT_EASE = 0.3f;  // per-frame ease of height toward its target
            const float DIST_EASE   = 0.3f; // per-frame ease of horizontal distance toward its target
            const float SLIDE_MARGIN = 8f;   // swept-slide standoff + proximity-extension reach. KEEP <= MARGIN (else the two setpoints oscillate)
            const float SLIDE_BIAS = 0.03125f; // angle-axis weight² in the slide: 1 = neutral (resists rotation), small = FREE glide (dist/height resolve, rotation flows)
            const float SLIDE_FRICTION = 0.6f; // contact drag at FULL tangency (keep-floor); head-on contact is undamped — keep = 1 − (1−F)·|n_t|
            float SLIDE_FRICTION_INV = 1f - SLIDE_FRICTION;   // injected form (asm folds 1−F to save the 1.0 load)
            const float CLIMB_PEAK = 60f;    // height the climb curve reaches at full pinch (d' = 0); the BELL's peak
            float CLIMB_K = (CLIMB_PEAK - REST_H) / (BASE_DIST * BASE_DIST);   // quadratic climb gain - zero slope at touch
            const float CLIMB_RISE = 2f;     // max climb rise per frame (matches the fall bound — all height motion is rate-limited)
            const float SLIDE_GAIN = 0.03125f; // reacquisition slide: fraction of the tangent-projected restoring pull applied per frame (0 = off; PutVal steps 0.0625/0.125/0.25)
            const float MIN_GROUND_CLEAR = 6f; // eye never gets closer than this to the ground under it (stick-down guard)
            const float H_FALL_RATE    = 2f;    // max WORLD-space height drop per frame (absolute descent bound — a falling player outruns the camera)
            const float WARP_BREAK     = 400f; // world-y discontinuity beyond which the descent bound is skipped (true warps only —
            // the eased desired-height drops ~30% of the offset per frame, so a LONG fall legitimately opens a
            // gap of hundreds of units; 400 misread that as a warp and released the bound mid-fall)
            const float GROUND_GLIDE_K = 0.03125f; // pinned+occluded ground glide: boom fraction pulled toward the player per frame
            const float GLIDE_MIN_DIST = 12f;   // the ground glide never pulls the boom closer than this
            const float DESCENT_HOLD   = 15f;   // height excess above rest that freezes OUTWARD dist recovery (kills the lip-crossover bounce)
            // Assembled template (378 words) from tools/town_camera_collision.s — pull-in + ceiling-duck + stick, one-sided _c, no climb. The KNOBS are the consts above, NOT the hex — they get
            // written into the flagged word slots after this literal (PutVal/PutEase, indices guarded). Regenerate this
            // array via mips_asm.py only if the CODE changes. R5900 quirks: c.OLT.s / sqrt.s are .word-encoded; a nop
            // follows every mtc1 and every FP compare.
            uint[] pullIn = LoadWordsResource("Dark_Cloud_Improved_Version.Resources.isoPatch.townCameraCollision.bin", 0x27BDFF60);   // Resources/isoPatch/townCameraCollision.bin (embedded) —
                                                      // assembled from tools/town_camera_collision.s @0x14B838
            // Inject the tunables above into the template's constant-load slots (indices auto-located from
            // tools/town_camera_collision.s; guards trip loudly if the array drifts). PutVal = single `lui $t0` (float low16 must
            // be 0 — integers / .25 steps); PutEase = `lui $t0` + `ori $t0`.
            static uint[] LoadWordsResource(string res, uint expectedFirstWord)
            {
                using var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(res)
                    ?? throw new IOException($"Embedded EE function missing: {res} (reassemble its .s in tools/ and rebuild)");
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                byte[] b = ms.ToArray();
                if (b.Length == 0 || (b.Length & 3) != 0)
                    throw new IOException($"EE function resource {res} is malformed ({b.Length} bytes).");
                uint[] w = new uint[b.Length / 4];
                Buffer.BlockCopy(b, 0, w, 0, b.Length);
                if (w[0] != expectedFirstWord)
                    throw new IOException($"{res} doesn't start with the expected prologue (got 0x{w[0]:X8}) — stale or mis-assembled.");
                return w;
            }
            static void PutValIn(uint[] arr, int idx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((b & 0xFFFF) != 0)
                    throw new Exception($"Tunable {nm}={f} isn't a single-lui float (low16!=0).");
                if ((arr[idx] & 0xFFFF0000u) != 0x3C080000u)
                    throw new Exception($"Tunable {nm} slot {idx} moved — refresh indices from the .s.");
                arr[idx] = 0x3C080000u | (b >> 16);
            }
            void PutVal(int idx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((b & 0xFFFF) != 0)
                    throw new Exception($"Camera tunable {nm}={f} isn't a single-lui float (low16!=0, got 0x{b:X8}); use an integer or a .25 step.");
                if ((pullIn[idx] & 0xFFFF0000u) != 0x3C080000u)
                    throw new Exception($"Camera tunable {nm} slot {idx} is not a `lui $t0` — slot indices are stale — reassemble tools/town_camera_collision.s and refresh them.");
                pullIn[idx] = 0x3C080000u | (b >> 16);
            }
            void PutEase(int luiIdx, int oriIdx, float f, string nm)
            {
                uint b = BitConverter.SingleToUInt32Bits(f);
                if ((pullIn[luiIdx] & 0xFFFF0000u) != 0x3C080000u || (pullIn[oriIdx] & 0xFFFF0000u) != 0x35080000u)
                    throw new Exception($"Camera tunable {nm} slots ({luiIdx},{oriIdx}) moved — slot indices are stale — reassemble tools/town_camera_collision.s and refresh them.");
                pullIn[luiIdx] = 0x3C080000u | (b >> 16);
                pullIn[oriIdx] = 0x35080000u | (b & 0xFFFF);
            }
            float STICK_DZ2 = STICK_DEADZONE * STICK_DEADZONE;   // deadzone² (compared vs stickY²)
            PutVal(142, BASE_DIST, nameof(BASE_DIST));   // resting dist target
            PutVal(434, BASE_DIST, nameof(BASE_DIST));   // reacquisition rest
            PutVal(451, BASE_DIST, nameof(BASE_DIST));   // climb intrusion reference
            PutVal(145, REST_H, nameof(REST_H));   // height target base
            PutVal(469, REST_H, nameof(REST_H));   // climb-curve base
            PutVal(41, CEIL_DIST, nameof(CEIL_DIST));
            PutVal(153, MIN_CEIL_CLEAR, nameof(MIN_CEIL_CLEAR));   // tunnel-duck clearance
            PutVal(166, MIN_GROUND_CLEAR, nameof(MIN_GROUND_CLEAR));
            PutVal(478, CLIMB_RISE, nameof(CLIMB_RISE));   // climb rise rate cap
            PutVal(269, SLIDE_MARGIN, nameof(SLIDE_MARGIN));   // proximity-extension reach
            PutVal(347, SLIDE_MARGIN, nameof(SLIDE_MARGIN));   // need standoff
            PutVal(560, SLIDE_MARGIN, nameof(SLIDE_MARGIN));   // corner second-resolution standoff
            PutVal(440, SLIDE_GAIN, nameof(SLIDE_GAIN));   // θ reacquisition
            PutVal(116, STICK_SCALE, nameof(STICK_SCALE));
            PutEase(108, 109, STICK_DZ2, nameof(STICK_DZ2));
            PutEase(127, 128, STICK_EASE, nameof(STICK_EASE));
            PutEase(179, 180, HEIGHT_EASE, nameof(HEIGHT_EASE));
            PutEase(195, 196, DIST_EASE, nameof(DIST_EASE));
            PutEase(372, 373, SLIDE_BIAS, nameof(SLIDE_BIAS));
            PutEase(421, 422, SLIDE_FRICTION_INV, nameof(SLIDE_FRICTION_INV));
            PutEase(464, 465, CLIMB_K, nameof(CLIMB_K));
            for (int i = 0; i < pullIn.Length; i++)
                WrU32(fs, ElfOff(PULLIN_VA + (uint)(i * 4)), pullIn[i]);
            Guard(0x0027D090, 0x00000000, "world-height cave (ex-autorotate area, zero words in vanilla)");
            uint[] heightFn = LoadWordsResource("Dark_Cloud_Improved_Version.Resources.isoPatch.cameraHeight.bin", 0x27BDFFE0);
            PutValIn(heightFn, 11, WARP_BREAK, nameof(WARP_BREAK));
            PutValIn(heightFn, 18, H_FALL_RATE, nameof(H_FALL_RATE));
            PutValIn(heightFn, 36, GROUND_GLIDE_K, nameof(GROUND_GLIDE_K));
            PutValIn(heightFn, 41, GLIDE_MIN_DIST, nameof(GLIDE_MIN_DIST));
            PutValIn(heightFn, 49, DESCENT_HOLD, nameof(DESCENT_HOLD));
            for (int i = 0; i < heightFn.Length; i++)
                WrU32(fs, ElfOff(0x0027D090 + (uint)(i * 4)), heightFn[i]);
            WrU32(fs, ElfOff(HOOK_VA), 0x0C052E0E);        // retarget jal CheckHitVertical → our pull-in @0x14B838
        }

        // ── Fishing camera → center on the bobber instead of the player/bobber midpoint ──────────────
        // While the line is cast (chara_fishing states with the hook in water), EdMoveChara aims the
        // follow-camera at the MIDPOINT of the player and the float:
        //     FishLineGetUki(&t);              // t = bobber world pos          @0x16D0B4
        //     sceVu0AddVector(&t,&t,&player);  // t = bobber + player           @0x16D0C8
        //     sceVu0ScaleVector(0.5f,&t,&t);   // t = (bobber + player) * 0.5   @0x16D0E0
        //     SetFollow(t.x,t.y,t.z,cam);      // camera looks at the midpoint  @0x16D0F8
        // NOP-ing the add+scale calls leaves `t` = the raw bobber position, so the shot centers on the
        // float where the action is. Both delay slots are already nop, so this is two jal->nop writes.
        // Runs only during fishing (the enclosing block is gated on the fishing flag), so it needs no
        // town gating and improves every fishing spot, vanilla or custom. (Distance/height are vanilla now —
        // the town-camera tweak patches were removed; the camera is being rewritten from scratch.)
        static void PatchFishingCameraTarget(FileStream fs, Func<uint, long> ElfOff)
        {
            (uint va, uint vanilla, string what)[] sites =
            {
                (0x0016D0C8, 0x0C0485E8, "jal sceVu0AddVector (bobber += player)"),
                (0x0016D0E0, 0x0C0485FA, "jal sceVu0ScaleVector (midpoint *= 0.5)"),
            };
            foreach (var s in sites)
                if (RdU32(fs, ElfOff(s.va)) != s.vanilla)
                    throw new IOException($"Fishing-camera site 0x{s.va:X} ({s.what}) is not vanilla — is this an unmodified Dark Cloud (USA) ISO?");
            foreach (var s in sites)
                WrU32(fs, ElfOff(s.va), 0x00000000);   // nop -> keep the bobber position as the camera target
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

        // ── sign + ladder textures: CARVE from the user's OWN ISO. The sign glyph e01b24 lives in Muska Lacka
        //    (e04/img.pak); the metal-ladder texture e05t06 lives in the Factory (e05/img.pak). Both go into ONE
        //    IM2 bank (fishsign.img) as two entries — a single boot-cave EnterIMGFile(-1) registers every entry
        //    in a bank (that is how the town loads its own multi-texture e03t01.img in one call), so the ladder
        //    material "e05t06" resolves globally exactly like the sign's "e01b24". No boot-cave change needed.
        //    DC_SIGN_ASSETS overrides ONLY the kanban mesh for dev; textures always come from the ISO. ──
        static (byte[] kanban, byte[] img) LoadSignAssets(FileStream fs, byte[] hed, long datIso, long hd2Base)
        {
            byte[] e04img = ReadArchive(fs, hed, datIso, hd2Base, "gedit/e04/img.pak");
            byte[] e05img = ReadArchive(fs, hed, datIso, hd2Base, "gedit/e05/img.pak");
            byte[] bank = Im2BuildMulti(new[] { "e01b24", "e05t06" },
                                        new[] { CarveTim2(e04img, "e01b24"), CarveTim2(e05img, "e05t06") });
            string env = Environment.GetEnvironmentVariable("DC_SIGN_ASSETS");
            byte[] kanban = (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "kanban.mds")))
                ? File.ReadAllBytes(Path.Combine(env, "kanban.mds"))
                : CarveKanban(ReadArchive(fs, hed, datIso, hd2Base, "gedit/e04/scene.scn"));
            return (kanban, bank);
        }

        static byte[] ReadArchive(FileStream fs, byte[] hed, long datIso, long hd2Base, string name)
        {
            long s = hd2Base + (long)ArchiveFind(hed, name) * 32;
            return Rd(fs, datIso + RdU32(fs, s), (int)RdU32(fs, s + 4));
        }


        // Carve a named texture's CLEAN TIM2 (0x10 file header + picture header + image + clut — no adjacent-entry
        // spillover) out of an img.pak's IM2 bank. Returns just the TIM2 block; Im2BuildMulti wraps banks.
        static byte[] CarveTim2(byte[] pak, string texName)
        {
            int p = 0;
            while (p < pak.Length && pak[p] != 0)
            {
                uint dataOff = U32(pak, p + 0x40), size = U32(pak, p + 0x44), stride = U32(pak, p + 0x48);
                int b = p + (int)dataOff;
                if (size >= 8 && pak[b] == 'I' && pak[b + 1] == 'M' && pak[b + 2] == '2' && pak[b + 3] == 0)
                {
                    int count = (int)U32(pak, b + 4);
                    for (int i = 0; i < count; i++)
                    {
                        int e = b + 0x10 + i * 0x30;                              // ENT = 0x30, name@0, offset@+0x20
                        if (NameAt(pak, e, 0x20) != texName) continue;
                        int t = b + (int)U32(pak, e + 0x20);                       // TIM2 block (bank-relative offset)
                        uint clutSz = U32(pak, t + 0x14), imgSz = U32(pak, t + 0x18);
                        ushort hdrSz = BitConverter.ToUInt16(pak, t + 0x1C);
                        int clean = 0x10 + hdrSz + (int)imgSz + (int)clutSz;
                        var tim2 = new byte[clean]; Array.Copy(pak, t, tim2, 0, clean);
                        return tim2;
                    }
                }
                p += (int)stride;
            }
            throw new IOException($"Could not find texture {texName} in img.pak.");
        }

        // Wrap N clean TIM2 blocks into one IM2 bank (header 0x10, per-entry 0x30 = name@0 + bank-relative
        // offset@+0x20; TIM2 blocks 16-aligned after the entry table). Matches the native bank layout that
        // EnterIMGFile(-1) registers wholesale, so every entry's name resolves for meshes that reference it.
        static byte[] Im2BuildMulti(string[] names, byte[][] tim2s)
        {
            int count = names.Length;
            int dataStart = (0x10 + count * 0x30 + 0xF) & ~0xF;
            var offs = new int[count]; int cur = dataStart;
            for (int i = 0; i < count; i++) { offs[i] = cur; cur += (tim2s[i].Length + 0xF) & ~0xF; }
            var outb = new byte[cur];
            outb[0] = (byte)'I'; outb[1] = (byte)'M'; outb[2] = (byte)'2'; outb[3] = 0;
            U32(outb, 4, (uint)count);
            for (int i = 0; i < count; i++)
            {
                int e = 0x10 + i * 0x30;
                byte[] nb = Encoding.Latin1.GetBytes(names[i]);
                Array.Copy(nb, 0, outb, e, Math.Min(nb.Length, 0x1F));
                U32(outb, e + 0x20, (uint)offs[i]);
                Array.Copy(tim2s[i], 0, outb, offs[i], tim2s[i].Length);
            }
            return outb;
        }

        // Carve the kanban mesh: find its node in e04/scene.scn, its containing MDS block + MDT, emit a
        // standalone 1-node MDS (parent -1, block-relative meshOff 0x80). Matches mds_surgery.build.
        static byte[] CarveKanban(byte[] scene)
        {
            int ki = IndexOf(scene, Encoding.ASCII.GetBytes("kanban\0"), 0);
            if (ki < 0) throw new IOException("Could not find the fishing-sign mesh (kanban) in the ISO.");
            int mds = LastIndexOf(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, ki - 8);
            int tbl = (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8);
            int knOff = -1;
            for (int i = 0; i < count; i++) { int no = mds + tbl + i * 0x70; if (NameAt(scene, no + 8, 0x20) == "kanban") { knOff = no; break; } }
            if (knOff < 0) throw new IOException("kanban node index not found.");
            int mdt = mds + (int)U32(scene, knOff + 0x28);                         // meshOff is block-relative
            int mdtTotal = (int)U32(scene, mdt + 8);                              // MDT self-delimiting
            var outb = new byte[0x10 + 0x70 + mdtTotal];
            outb[0] = (byte)'M'; outb[1] = (byte)'D'; outb[2] = (byte)'S'; outb[3] = 0;
            U32(outb, 4, U32(scene, mds + 4)); U32(outb, 8, 1); U32(outb, 0xC, 0x10);   // version, count 1, tbl 0x10
            Array.Copy(scene, knOff, outb, 0x10, 0x70);                            // the node
            U32(outb, 0x10 + 0x28, 0x80);                                          // meshOff = 0x80 (block-relative)
            U32(outb, 0x10 + 0x2C, 0xFFFFFFFF);                                    // parent = -1 (detached root)
            Array.Copy(scene, mdt, outb, 0x10 + 0x70, mdtTotal);
            return outb;
        }

        // ── canal ladder: carve the Factory metal ladder (e05a01/hasigo1) from the user's ISO and reshape it
        //    for the Queens canal wall. Faithful C# port of tools/carve_ladder.py (the reference the viewer
        //    renders): de-yaw ~9.5° so the rails run parallel to X, clip the bottom off at the mid-rung gap
        //    (y=22) with edge interpolation so the rails stay watertight, snap the cut ring to the floor and
        //    shift so the donor's ground mount lands on the walkway (y=70), compact, then translate to the
        //    world placement (centred x=700, feet on the walkway). Emitted as a kanban-style 1-node MDS with
        //    world-baked verts (mapinfo GROUND "hasigo" places it at the origin). ──
        const float LAD_CUT_Y = 22f, LAD_SNAP_Y = 20f, LAD_SHIFT = 20f, LAD_X = 706f, LAD_FEET_Z = 52f;

        sealed class Mdt
        {
            public uint[] hw; public int[] preamble; public bool hasCol;
            public List<float[]> pos, uv, norm, col;                      // col null when absent
            public List<(int prim, int mat, List<int[]> recs)> subs;
            public List<byte[]> mats;
        }

        static List<float[]> ReadVecs(byte[] s, int b, int n)
        {
            var v = new List<float[]>(n);
            for (int i = 0; i < n; i++)
                v.Add(new[] { BitConverter.ToSingle(s, b + i * 16), BitConverter.ToSingle(s, b + i * 16 + 4),
                              BitConverter.ToSingle(s, b + i * 16 + 8), BitConverter.ToSingle(s, b + i * 16 + 12) });
            return v;
        }

        static Mdt MdtParse(byte[] s, int fo)
        {
            var m = new Mdt { hw = new uint[16] };
            for (int i = 0; i < 16; i++) m.hw[i] = U32(s, fo + i * 4);
            int total = (int)m.hw[2], nPos = (int)m.hw[3], POS = (int)m.hw[4], UV = (int)m.hw[6];
            uint COL = m.hw[8]; int DL = (int)m.hw[10], NORM = (int)m.hw[12], MAT = (int)m.hw[14];
            m.hasCol = COL > 0 && COL < 0x80000000; int stride = m.hasCol ? 4 : 3;
            m.preamble = new int[4]; for (int i = 0; i < 4; i++) m.preamble[i] = (int)U32(s, fo + DL + i * 4);
            int numsub = m.preamble[2], o = DL + 0x10;
            m.subs = new();
            for (int si = 0; si < numsub; si++)
            {
                int prim = (int)U32(s, fo + o), vcnt = (int)U32(s, fo + o + 4), midx = (int)U32(s, fo + o + 8); o += 0xC;
                var recs = new List<int[]>(vcnt);
                for (int r = 0; r < vcnt; r++)
                {
                    var rec = new int[stride];
                    for (int k = 0; k < stride; k++) rec[k] = (int)U32(s, fo + o + (r * stride + k) * 4);
                    recs.Add(rec);
                }
                o += vcnt * stride * 4;
                m.subs.Add((prim, midx, recs));
            }
            int nUV = 0, nNorm = 0, nCol = 0;
            foreach (var sub in m.subs) foreach (var r in sub.recs)
            { nUV = Math.Max(nUV, r[1] + 1); nNorm = Math.Max(nNorm, r[2] + 1); if (m.hasCol) nCol = Math.Max(nCol, r[3] + 1); }
            m.pos = ReadVecs(s, fo + POS, nPos);
            m.uv = ReadVecs(s, fo + UV, nUV);
            m.norm = NORM > 0 ? ReadVecs(s, fo + NORM, nNorm) : new();
            m.col = m.hasCol ? ReadVecs(s, fo + (int)COL, nCol) : null;
            int nmat = (total - MAT) / 0x60;
            m.mats = new();
            for (int i = 0; i < nmat; i++) { var mb = new byte[0x60]; Array.Copy(s, fo + MAT + i * 0x60, mb, 0, 0x60); m.mats.Add(mb); }
            return m;
        }

        static float[] Lerp(float[] a, float[] b, float t)
        { var o = new float[4]; for (int i = 0; i < 4; i++) o[i] = a[i] + (b[i] - a[i]) * t; return o; }

        static IEnumerable<int[][]> TrisOf(int prim, List<int[]> recs)
        {
            if (prim == 3) for (int i = 0; i + 2 < recs.Count; i += 3) yield return new[] { recs[i], recs[i + 1], recs[i + 2] };
            else if (prim == 4) for (int i = 0; i + 2 < recs.Count; i++)
                yield return (i & 1) == 1 ? new[] { recs[i], recs[i + 2], recs[i + 1] } : new[] { recs[i], recs[i + 1], recs[i + 2] };
        }

        static void CarveMesh(Mdt m)
        {
            // 1) de-yaw: measure dz/dx of the rail-plane verts (y<85, z<-40), rotate pos + norm by -that about Y
            double mx = 0, mz = 0; int cnt = 0;
            foreach (var v in m.pos) if (v[1] < 85 && v[2] < -40) { mx += v[0]; mz += v[2]; cnt++; }
            mx /= cnt; mz /= cnt;
            double num = 0, den = 0;
            foreach (var v in m.pos) if (v[1] < 85 && v[2] < -40) { num += (v[0] - mx) * (v[2] - mz); den += (v[0] - mx) * (v[0] - mx); }
            double th = Math.Atan2(num, den); float c = (float)Math.Cos(th), s = (float)Math.Sin(th);
            void RotY(List<float[]> vs) { foreach (var v in vs) { float x = v[0], z = v[2]; v[0] = x * c + z * s; v[2] = -x * s + z * c; } }
            RotY(m.pos); if (m.norm.Count > 0) RotY(m.norm);

            // 2) clip everything below LAD_CUT_Y, interpolating a new vert on each crossing edge
            int firstNew = m.pos.Count, stride = m.hasCol ? 4 : 3;
            var cache = new Dictionary<string, int[]>();
            int[] CutVert(int[] rA, int[] rB)
            {
                bool aFirst = string.CompareOrdinal(string.Join(",", rA), string.Join(",", rB)) <= 0;
                int[] a = aFirst ? rA : rB, b = aFirst ? rB : rA;
                string key = string.Join(",", a) + "|" + string.Join(",", b);
                if (cache.TryGetValue(key, out var got)) return got;
                float[] pa = m.pos[a[0]], pb = m.pos[b[0]];
                float t = (LAD_CUT_Y - pa[1]) / (pb[1] - pa[1]);
                m.pos.Add(Lerp(pa, pb, t)); m.uv.Add(Lerp(m.uv[a[1]], m.uv[b[1]], t));
                var rec = new int[stride]; rec[0] = m.pos.Count - 1; rec[1] = m.uv.Count - 1;
                if (m.norm.Count > 0) { m.norm.Add(Lerp(m.norm[a[2]], m.norm[b[2]], t)); rec[2] = m.norm.Count - 1; } else rec[2] = 0;
                if (m.hasCol) { m.col.Add(Lerp(m.col[a[3]], m.col[b[3]], t)); rec[3] = m.col.Count - 1; }
                cache[key] = rec; return rec;
            }
            var newSubs = new List<(int, int, List<int[]>)>();
            foreach (var (prim, midx, recs) in m.subs)
            {
                var outRecs = new List<int[]>();
                foreach (var tri in TrisOf(prim, recs))
                {
                    var poly = new List<int[]>();
                    for (int i = 0; i < 3; i++)
                    {
                        int[] A = tri[i], B = tri[(i + 1) % 3];
                        bool inA = m.pos[A[0]][1] >= LAD_CUT_Y, inB = m.pos[B[0]][1] >= LAD_CUT_Y;
                        if (inA) poly.Add(A);
                        if (inA != inB) poly.Add(CutVert(A, B));
                    }
                    // clone each emitted record: strip sources share a record across triangles, and the
                    // per-slot in-place compaction below must see every list position as a distinct object
                    for (int k = 1; k + 1 < poly.Count; k++)
                    { outRecs.Add((int[])poly[0].Clone()); outRecs.Add((int[])poly[k].Clone()); outRecs.Add((int[])poly[k + 1].Clone()); }
                }
                if (outRecs.Count > 0) newSubs.Add((3, midx, outRecs));
            }
            m.subs = newSubs.ConvertAll(x => (x.Item1, x.Item2, x.Item3));

            // 3) snap the cut ring to the floor + shift so the ground mount lands on the walkway
            for (int i = 0; i < m.pos.Count; i++)
                m.pos[i][1] = (i >= firstNew ? LAD_SNAP_Y : m.pos[i][1]) - LAD_SHIFT;

            // 4) compact: drop the now-unreferenced (clipped-away) verts from every stream
            CompactStream(m, 0, m.pos); CompactStream(m, 1, m.uv);
            if (m.norm.Count > 0) CompactStream(m, 2, m.norm);
            if (m.hasCol) CompactStream(m, 3, m.col);
        }

        static void CompactStream(Mdt m, int slot, List<float[]> stream)
        {
            var used = new SortedSet<int>();
            foreach (var sub in m.subs) foreach (var r in sub.recs) used.Add(r[slot]);
            var remap = new Dictionary<int, int>(); var ns = new List<float[]>();
            foreach (int o in used) { remap[o] = ns.Count; ns.Add(stream[o]); }
            stream.Clear(); stream.AddRange(ns);
            foreach (var sub in m.subs) foreach (var r in sub.recs) r[slot] = remap[r[slot]];
        }

        static void WorldPlace(Mdt m)
        {
            float minx = float.MaxValue, maxx = float.MinValue, feet = float.MinValue;
            foreach (var v in m.pos) { minx = Math.Min(minx, v[0]); maxx = Math.Max(maxx, v[0]); if (v[1] > 69) feet = Math.Max(feet, v[2]); }
            float dx = LAD_X - (minx + maxx) / 2, dz = LAD_FEET_Z - feet;
            foreach (var v in m.pos) { v[0] += dx; v[2] += dz; }
        }

        static byte[] MdtBuild(Mdt m)
        {
            int stride = m.hasCol ? 4 : 3;
            var dl = new List<byte>();
            void PutI(List<byte> b, int v) => b.AddRange(BitConverter.GetBytes(v));
            PutI(dl, m.preamble[0]); PutI(dl, m.preamble[1]); PutI(dl, m.subs.Count); PutI(dl, m.preamble[3]);
            foreach (var (prim, midx, recs) in m.subs)
            { PutI(dl, prim); PutI(dl, recs.Count); PutI(dl, midx); foreach (var r in recs) for (int k = 0; k < stride; k++) PutI(dl, r[k]); }
            byte[] VecBytes(List<float[]> vs)
            { var b = new byte[vs.Count * 16]; for (int i = 0; i < vs.Count; i++) for (int k = 0; k < 4; k++) Array.Copy(BitConverter.GetBytes(vs[i][k]), 0, b, i * 16 + k * 4, 4); return b; }
            byte[] matBytes = new byte[m.mats.Count * 0x60];
            for (int i = 0; i < m.mats.Count; i++) Array.Copy(m.mats[i], 0, matBytes, i * 0x60, 0x60);

            var outb = new List<byte>(new byte[0x40]);
            int Emit(byte[] blk) { while ((outb.Count & 0xF) != 0) outb.Add(0); int off = outb.Count; outb.AddRange(blk); return off; }
            int posOff = Emit(VecBytes(m.pos)), dlOff = Emit(dl.ToArray()), uvOff = Emit(VecBytes(m.uv));
            int normOff = m.norm.Count > 0 ? Emit(VecBytes(m.norm)) : 0;
            int colOff = m.hasCol ? Emit(VecBytes(m.col)) : 0;
            int matOff = Emit(matBytes);
            while ((outb.Count & 0xF) != 0) outb.Add(0);

            byte[] o = outb.ToArray();
            var hw = (uint[])m.hw.Clone();
            hw[2] = (uint)o.Length; hw[3] = (uint)m.pos.Count; hw[4] = (uint)posOff; hw[6] = (uint)uvOff;
            hw[8] = m.hasCol ? (uint)colOff : m.hw[8]; hw[9] = (uint)dl.Count; hw[10] = (uint)dlOff;
            hw[12] = m.norm.Count > 0 ? (uint)normOff : 0; hw[14] = (uint)matOff;
            for (int i = 0; i < 16; i++) U32(o, i * 4, hw[i]);
            return o;
        }

        static byte[] CarveLadder(byte[] scene)
        {
            // Scope to the e05a01 PART (the node name also appears in a name table before the geometry, so a
            // bare string search grabs the wrong one): part-table entry -> its MDS -> node-table scan.
            int nParts = (int)U32(scene, 4), poff = -1;
            for (int i = 0; i < nParts; i++) { int e = 0x10 + i * 0x30; if (NameAt(scene, e, 0x10) == LADDER_PART) { poff = (int)U32(scene, e + 0x10); break; } }
            if (poff < 0) throw new IOException($"Ladder part {LADDER_PART} not found in the ISO.");
            int mds = IndexOf(scene, new byte[] { (byte)'M', (byte)'D', (byte)'S', 0 }, poff);
            if (mds < 0) throw new IOException("Ladder part MDS not found.");
            int tbl = mds + (int)U32(scene, mds + 0xC), count = (int)U32(scene, mds + 8), no = -1;
            for (int i = 0; i < count; i++) { int c = tbl + i * 0x70; if (NameAt(scene, c + 8, 0x20) == LADDER_NODE) { no = c; break; } }
            if (no < 0) throw new IOException($"{LADDER_NODE} node index not found.");
            int meshOff = (int)U32(scene, no + 0x28);
            int mdt = (scene[mds + meshOff] == 'M') ? mds + meshOff : meshOff;   // meshOff is block-relative
            if (!(scene[mdt] == 'M' && scene[mdt + 1] == 'D' && scene[mdt + 2] == 'T')) throw new IOException("ladder MDT not resolved.");

            var m = MdtParse(scene, mdt);
            CarveMesh(m); WorldPlace(m);
            byte[] mdtBytes = MdtBuild(m);

            // wrap in a 1-node MDS (identity 4x4 — mapinfo places the world-baked verts at the origin)
            var outb = new byte[0x10 + 0x70 + mdtBytes.Length];
            outb[0] = (byte)'M'; outb[1] = (byte)'D'; outb[2] = (byte)'S'; outb[3] = 0;
            U32(outb, 4, 1); U32(outb, 8, 1); U32(outb, 0xC, 0x10);
            const int nOff = 0x10;
            U32(outb, nOff + 4, 0x70);
            byte[] nn = Encoding.Latin1.GetBytes("hasigo"); Array.Copy(nn, 0, outb, nOff + 8, nn.Length);
            U32(outb, nOff + 0x28, 0x80); U32(outb, nOff + 0x2C, 0xFFFFFFFF);
            for (int i = 0; i < 4; i++) Array.Copy(BitConverter.GetBytes(1.0f), 0, outb, nOff + 0x30 + i * 0x14, 4);
            Array.Copy(mdtBytes, 0, outb, nOff + 0x70, mdtBytes.Length);
            return outb;
        }

        static int IndexOf(byte[] hay, byte[] needle, int start)
        {
            for (int i = start; i <= hay.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
        static int LastIndexOf(byte[] hay, byte[] needle, int before)
        {
            for (int i = Math.Min(before, hay.Length - needle.Length); i >= 0; i--)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
    }
}
