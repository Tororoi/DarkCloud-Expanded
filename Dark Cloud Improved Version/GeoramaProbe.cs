using System;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A read-only diagnostic for siting a custom fishing spot in a town. Writes nothing to the game.
    ///
    /// The static research (docs/custom-fishing-spot.md) tells us WHAT a spot needs — a rectangle, a
    /// water height, and collision underneath. It cannot tell us the actual numbers for a given pool,
    /// because those are geometry. This closes that gap: walk the edge of the water, and the probe
    /// reports the coordinates you would put into <see cref="FishingSpot.FishingRect"/> and
    /// <see cref="FishingSpot.WaterLevel"/>.
    ///
    /// Driven from <see cref="Tick"/> in TownCharacter's loop. It dumps each town once on entry
    /// (its Georama parts and their types, its water surfaces with heights and tints, its texture
    /// banks, the live fishing state) and tracks the player's position — walk the edge of a pool and
    /// <see cref="ReportBox"/> hands you a rectangle. <see cref="Mark"/> pins a corner.
    ///
    /// Two things this got wrong the first time, both now fixed, both worth not repeating:
    ///
    /// • **Dump on <c>EditDataDir</c> change, not on <c>MapNo</c> change.** MapNo flips as soon as a
    ///   transition begins, while <c>edit_info</c> still holds the previous town's parsed cfg — so the
    ///   first run dutifully printed Queens' texture banks and parts under the heading "Brownboo".
    ///
    /// • **Read the position from the <c>Chara</c> CFrame, not the loose position globals.** A live
    ///   reading gave the triple as [1407.34, 170, 43.52], with the MIDDLE value tracking the EDITAREA
    ///   base heights — so the mod's positionX/Y/Z naming does not survive contact with the data.
    ///
    /// Validated: Queens' canal water surface reports HEIGHT = 31, exactly as its mapinfo.cfg declares.
    /// </summary>
    internal static class GeoramaProbe
    {
        /// <summary>Master switch. Read-only: this class writes nothing to the game.
        /// The overhead camera lives in <see cref="TownEditMode"/>.</summary>
        internal static bool Enabled = true;

        /// <summary>Log the player's position as they move, and accumulate a bounding box.</summary>
        internal static bool WatchPosition = true;

        private const float MoveEpsilon = 1.5f;   // don't spam a line for standing still

        private static float _minX, _maxX, _minZ, _maxZ, _minY, _maxY;
        private static bool _haveBox;

        private const int PendingTimeoutTicks = 400;   // ~20 s at the town loop's 50 ms

        private static int _lastMap = -1;
        private static int _lastMode = -1;
        private static float _lx = float.NaN, _lz;
        private static string _lastDir = "";
        private static string _pendingDir;
        private static int _pendingTicks;

        /// <summary>
        /// True once <c>edit_info</c> demonstrably holds THIS town's parsed cfg — i.e. at least one texture
        /// bank is registered and its path sits under <paramref name="dir"/>.
        ///
        /// This is the load-order check that both naive triggers failed. The bank paths are absolute
        /// (<c>gedit/s13/img/s1301.img</c>), so matching them against the live <c>EditDataDir</c> proves the
        /// data belongs to the town we think we are in — rather than assuming it because a counter changed.
        /// </summary>
        private static bool TownDataMatches(string dir)
        {
            long info = EditInfo.Base();
            if (info == 0) return false;

            int n = Memory.ReadInt(EditInfo.TextureCount);
            if (n <= 0) return false;

            for (int i = 0; i < n && i < 32; i++)
            {
                string path = ReadCStr(info + EditInfo.TextureBase + i * EditInfo.TextureStride +
                                       EditInfo.TextureName, 0x40);
                if (path.StartsWith(dir, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Driven from the town loop (TownCharacter, ~20 Hz). Dumps each town once on entry and tracks
        /// position. Read-only.
        /// </summary>
        internal static void Tick()
        {
            if (!Enabled) return;

            int map = Memory.ReadInt(EditLoop.MapNo);

            // Deciding WHEN to dump is the whole problem, and two "obvious" triggers are both wrong:
            //   • MapNo — flips the instant a transition starts, long before anything is parsed.
            //   • EditDataDir — is set EARLY too: it is the directory the loader is about to read FROM.
            // Both raced, and the first run happily printed Queens' banks under the heading "Brownboo".
            //
            // So do not trust a signal at all: VERIFY. The texture-bank table holds full paths, so once
            // a bank path begins with the current EditDataDir, edit_info demonstrably holds THIS town's
            // parsed cfg. Wait for that, and the data cannot be stale by construction.
            string dir = ReadCStr(EditLoop.EditDataDir, 64);
            if (dir != _lastDir && dir.Length > 0)
            {
                _lastDir = dir;
                _lastMap = map;
                _haveBox = false;
                _lx = float.NaN;
                _pendingDir = dir;
                _pendingTicks = 0;
                Log($"##### entering {dir}  (MapNo {map} = {MapName(map)}) — waiting for its data #####");
            }

            if (_pendingDir != null)
            {
                _pendingTicks++;
                if (TownDataMatches(_pendingDir))
                {
                    Log($"##### {_pendingDir} loaded (after {_pendingTicks} ticks) #####");
                    _pendingDir = null;
                    DumpTown();
                }
                else if (_pendingTicks > PendingTimeoutTicks)
                {
                    Log($"##### {_pendingDir}: town data never matched after {_pendingTicks} ticks — " +
                        $"dumping anyway, TREAT AS SUSPECT #####");
                    _pendingDir = null;
                    DumpTown();
                }
            }


            // Did Select actually get us into the overhead camera? This is the whole experiment.
            int mode = Memory.ReadInt(EditLoop.GameMode);
            if (mode != _lastMode)
            {
                if (mode == EditLoop.GameModeOverhead)
                    Log($"*** GameMode -> 4 (OVERHEAD CAMERA) on MapNo {map} — it worked ***");
                else if (_lastMode == EditLoop.GameModeOverhead)
                    Log($"GameMode 4 -> {mode} (left the overhead camera)");
                _lastMode = mode;
            }

            if (WatchPosition) SamplePosition();
        }

        private static void SamplePosition()
        {
            if (!ReadPos(out float x, out float y, out float z)) return;
            Accumulate(x, y, z);

            if (!float.IsNaN(_lx) && Math.Abs(x - _lx) <= MoveEpsilon && Math.Abs(z - _lz) <= MoveEpsilon)
                return;
            _lx = x; _lz = z;
            Log($"pos  x={x:0.##}  y={y:0.##} (height)  z={z:0.##}");
        }

        /// <summary>MapNo is the same id TownCharacter calls `currentArea` (it reads 0x202A2518).</summary>
        private static string MapName(int map) => map switch
        {
            0  => "Norune",
            1  => "Matataki",
            2  => "Queens",
            3  => "Muska Lacka",
            14 => "Brownboo",
            19 => "Queens Harbor",
            23 => "Yellow Drops",
            38 => "Dark Heaven Castle",
            42 => "Muska Lacka (outside)",
            _  => "?",
        };

        // ---------------------------------------------------------------- one-shot town dump

        /// <summary>Dump everything the town declared that bears on siting a spot.</summary>
        internal static void DumpTown()
        {
            long info = EditInfo.Base();
            if (info == 0)
            {
                Log("no town loaded (edit_info is null)");
                return;
            }

            Log("===== TOWN DUMP =====");
            DumpTextureBanks(info);
            DumpParts(info);
            DumpWaterSurfaces(info);
            DumpPlacedParts();
            DumpEventPoints();
            DumpScript();
            DumpFishingState();
            Log("===== END =====");
        }

        /// <summary>Which texture banks this town loaded. The one that matters is <c>e01b24</c> — the
        /// fishing sign's texture — which ships only in the four fishing towns.</summary>
        private static void DumpTextureBanks(long info)
        {
            int n = Memory.ReadInt(EditInfo.TextureCount);
            Log($"-- texture banks ({n}) --");
            for (int i = 0; i < n && i < 32; i++)
            {
                long e = info + EditInfo.TextureBase + i * EditInfo.TextureStride;
                string path = ReadCStr(e + EditInfo.TextureName, 0x40);
                int kind = Memory.ReadInt(e + EditInfo.TextureKind);
                Log($"   [{i,2}] kind={kind} {path}");
            }
        }

        /// <summary>The town's Georama part definitions. The `type` column is the whole ballgame:
        /// 2/3/4/5 are water, and a LAKE (4) is a fishing pond.</summary>
        private static void DumpParts(long info)
        {
            Log("-- Georama parts --");
            for (int i = 0; i < EditInfo.MaxParts; i++)
            {
                long p = info + EditInfo.PartBase + i * EditInfo.PartStride;
                string name = ReadCStr(p + EditInfo.PartName, 32);
                if (string.IsNullOrEmpty(name)) continue;
                int type = Memory.ReadInt(p + EditInfo.PartType);
                string water = EditInfo.PartTypes.IsWater(type) ? "  <-- WATER" : "";
                Log($"   [{i,2}] {name,-16} type={type} ({EditInfo.PartTypes.Name(type)}){water}");
            }
        }

        /// <summary>The animated water surfaces. <c>height</c> is the number you want for
        /// <see cref="FishingSpot.WaterLevel"/>; <c>rgb</c> is the tint, which is how a yellow
        /// liquid would be coloured without any new asset.</summary>
        private static void DumpWaterSurfaces(long info)
        {
            int n = Memory.ReadInt(EditInfo.WaterCount);
            Log($"-- water surfaces ({n} of {EditInfo.MaxWaterSurfaces}) --");
            if (n == 0) Log("   (none — this town declares no animated water at all)");

            for (int i = 0; i < n && i < EditInfo.MaxWaterSurfaces; i++)
            {
                long w = info + EditInfo.WaterBase + i * EditInfo.WaterStride;
                string frame = ReadCStr(w + EditInfo.WaterFrameName, 16);
                int gw = Memory.ReadInt(w + EditInfo.WaterGridW);
                int gh = Memory.ReadInt(w + EditInfo.WaterGridH);

                float ax = Memory.ReadFloat(w + EditInfo.WaterCornerA);
                float az = Memory.ReadFloat(w + EditInfo.WaterCornerA + 8);
                float bx = Memory.ReadFloat(w + EditInfo.WaterCornerB);
                float bz = Memory.ReadFloat(w + EditInfo.WaterCornerB + 8);

                float ox = Memory.ReadFloat(w + EditInfo.WaterOffset);
                float oy = Memory.ReadFloat(w + EditInfo.WaterHeight);
                float oz = Memory.ReadFloat(w + EditInfo.WaterOffset + 8);

                int r = Memory.ReadInt(w + EditInfo.WaterColorR);
                int g = Memory.ReadInt(w + EditInfo.WaterColorR + 4);
                int b = Memory.ReadInt(w + EditInfo.WaterColorR + 8);

                Log($"   [{i}] frame='{frame}' grid={gw}x{gh}");
                Log($"        corners ({ax:0.#},{az:0.#}) -> ({bx:0.#},{bz:0.#})   " +
                    $"offset ({ox:0.#}, {oy:0.#}, {oz:0.#})   HEIGHT={oy:0.##}");
                Log($"        rgb=({r},{g},{b})");
            }
        }

        /// <summary>The parts actually placed on the ground, with their world positions. Useful for
        /// finding where a lake part IS, and for spotting free slots.</summary>
        private static void DumpPlacedParts()
        {
            long g = EditGround.Base();
            if (g == 0) { Log("-- placed parts: CEditGround is null --"); return; }

            int used = 0, free = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < EditGround.PlacedCount; i++)
            {
                long s = MapParts.Slot(g, i);
                if (MapParts.IsFree(s)) { free++; continue; }
                used++;

                int type = Memory.ReadInt(s + MapParts.Type);
                int id = Memory.ReadInt(s + MapParts.PartId);
                float x = Memory.ReadFloat(s + MapParts.Position);
                float y = Memory.ReadFloat(s + MapParts.Position + 4);
                float z = Memory.ReadFloat(s + MapParts.Position + 8);

                if (EditInfo.PartTypes.IsWater(type))
                    sb.AppendLine($"        slot {i,3} part {id,2} {EditInfo.PartTypes.Name(type),-8} " +
                                  $"at ({x:0.#}, {y:0.#}, {z:0.#})  <-- WATER");
            }
            Log($"-- placed parts: {used} used, {free} free (of {EditGround.PlacedCount}) --");
            if (sb.Length > 0) Log(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// The town's EVENT POINTS — what you walk up to and press a button on. A real fishing spot is one of
        /// these, so this is the table a custom spot has to join.
        ///
        /// The field layout beyond `type` is not fully mapped, so the raw words are dumped too: run this in
        /// NORUNE (which has a real fishing sign) and diff against Queens / Brownboo / Yellow Drops, and the
        /// fishing point's fields will stand out. That is cheaper and more honest than guessing offsets.
        /// </summary>
        private static void DumpEventPoints()
        {
            long arr = EventPoints.Base();
            int n = Memory.ReadInt(EventPoints.Count);
            if (arr == 0) { Log($"-- event points: array is NULL (count={n}) --"); return; }

            int used = 0;
            var lines = new StringBuilder();
            for (int i = 0; i < n && i < 64; i++)
            {
                long e = EventPoints.Slot(arr, i);
                int type = Memory.ReadInt(e + EventPoints.Type);
                if (type == 0) continue;   // free slot
                used++;

                // Dump the first 0x30 as ints AND floats — the position is in here somewhere, and a float
                // that looks like a world coordinate is far more legible than a hex word.
                var sb = new StringBuilder();
                for (int w = 0; w < 12; w++)
                {
                    int iv = Memory.ReadInt(e + w * 4);
                    float fv = Memory.ReadFloat(e + w * 4);
                    bool looksFloat = Math.Abs(fv) > 0.01f && Math.Abs(fv) < 100000f && iv != 0;
                    sb.Append(looksFloat ? $"{w * 4:X2}:{fv:0.#}f " : $"{w * 4:X2}:{iv} ");
                }
                lines.AppendLine($"        [{i,2}] type={type} item={Memory.ReadInt(e + EventPoints.ItemId)}");
                lines.AppendLine($"             {sb}");
            }
            Log($"-- event points: {used} used of {n} (stride 0x{EventPoints.Stride:X}) --");
            if (lines.Length > 0) Log(lines.ToString().TrimEnd());
        }

        /// <summary>
        /// The town's loaded <c>event.stb</c> and its label table. These are the labels a custom fishing spot
        /// would inject <c>_LOAD_FISHING_DATA</c> / <c>_GOTO_FISHING</c> into — the setup cannot be faked with
        /// writes (it loads a pak, allocates six CFish and gathers collision), so the game must run a script.
        /// </summary>
        private static void DumpScript()
        {
            long stb = TownScript.Base();
            if (stb == 0) { Log("-- event.stb: not loaded --"); return; }

            int codeBase = Memory.ReadInt(stb + TownScript.CodeBase);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            int count = Memory.ReadInt(stb + TownScript.LabelCount);

            if (count <= 0 || count > 4000 || tbl < 0)
            { Log($"-- event.stb @0x{stb:X}: header looks wrong (codeBase={codeBase} tbl={tbl} count={count}) --"); return; }

            Log($"-- event.stb @0x{stb:X}: codeBase=0x{codeBase:X} labelTable=0x{tbl:X} labels={count} --");

            var sb = new StringBuilder();
            for (int i = 0; i < count && i < 64; i++)
            {
                int id = Memory.ReadInt(stb + tbl + i * TownScript.LabelStride);
                int off = Memory.ReadInt(stb + tbl + i * TownScript.LabelStride + 4);
                sb.Append($"{id}@0x{off:X}  ");
                if ((i + 1) % 6 == 0) sb.AppendLine().Append("        ");
            }
            Log("        " + sb.ToString().TrimEnd());
        }

        /// <summary>The live fishing globals, if a spot has been loaded this session.</summary>
        private static void DumpFishingState()
        {
            int polys = Memory.ReadInt(FishingSpot.CPolyNum);
            Log("-- fishing state --");
            Log($"   WaterLevel={Memory.ReadFloat(FishingSpot.WaterLevel):0.##}  " +
                $"GroundLevel={Memory.ReadFloat(FishingSpot.GroundLevel):0.##}  " +
                $"cpoly={polys} (max {FishingSpot.CPolyMax})  " +
                $"FishNum={Memory.ReadInt(FishingSpot.FishNum)}  " +
                $"drawUnderWater={Memory.ReadInt(FishingSpot.DrawUnderWater)}");

            float ax = Memory.ReadFloat(FishingSpot.FishingRect + FishingSpot.BoxCornerA);
            float az = Memory.ReadFloat(FishingSpot.FishingRect + FishingSpot.BoxCornerA + 8);
            float bx = Memory.ReadFloat(FishingSpot.FishingRect + FishingSpot.BoxCornerB);
            float bz = Memory.ReadFloat(FishingSpot.FishingRect + FishingSpot.BoxCornerB + 8);
            Log($"   fishing_rect ({ax:0.#},{az:0.#}) -> ({bx:0.#},{bz:0.#})");
        }



        /// <summary>Where we are and whether the overhead camera has anything to stand on.</summary>
        internal static void Status()
        {
            int map = Memory.ReadInt(EditLoop.MapNo);
            int mode = Memory.ReadInt(EditLoop.GameMode);
            int flag = Memory.ReadInt(EditLoop.EdDebugMoveFlag);

            long g = EditGround.Base();
            int areas = 0;
            if (g != 0)
                for (int i = 0; i < 4; i++)
                    if (Memory.IsValidGuest(Memory.ReadUInt(g + 4 + i * 4) & Memory.PhysAddrMask)) areas++;

            Log($"MapNo={map}  GameMode={mode}  EdDebugMoveFlag={flag}  " +
                $"nativeOverhead={(map < 5 ? "yes" : "NO")}  CEditGround={(g != 0 ? "ok" : "NULL")}  " +
                $"editAreas={areas}/4");

            if (flag > 0 && areas == 0)
                Log("   WARNING: overhead forced on, but this map has NO edit areas — " +
                    "the cursor has no grid to walk. If Select hangs, this is why.");
        }

        // ---------------------------------------------------------------- position box

        /// <summary>Pin the current position — call it at each corner of the water you want to fish.</summary>
        internal static void Mark()
        {
            if (!ReadPos(out float x, out float y, out float z)) return;
            Accumulate(x, y, z);
            Log($"MARK x={x:0.##}  y={y:0.##} (height)  z={z:0.##}");
        }

        /// <summary>The bounding box of everywhere the player has been since the watch started —
        /// i.e. a ready-made fishing rectangle, plus the Y range to pick a water level from.</summary>
        internal static void ReportBox()
        {
            if (!_haveBox) { Log("no positions sampled"); return; }

            Log("===== BOX =====");
            Log($"   fishing_rect cornerA = ({_minX:0.##}, {_minZ:0.##})");
            Log($"   fishing_rect cornerB = ({_maxX:0.##}, {_maxZ:0.##})");
            Log($"   size {(_maxX - _minX):0.#} x {(_maxZ - _minZ):0.#}");
            Log($"   Y ranged {_minY:0.##} .. {_maxY:0.##}   " +
                $"-> WaterLevel is the water's SURFACE, GroundLevel its bed; " +
                $"walking the bank gives you the bank height, not the surface — " +
                $"cross-check against the water-surface HEIGHT from DumpTown().");
        }

        private static void Accumulate(float x, float y, float z)
        {
            if (!_haveBox)
            {
                _minX = _maxX = x; _minZ = _maxZ = z; _minY = _maxY = y;
                _haveBox = true;
                return;
            }
            if (x < _minX) _minX = x; if (x > _maxX) _maxX = x;
            if (z < _minZ) _minZ = z; if (z > _maxZ) _maxZ = z;
            if (y < _minY) _minY = y; if (y > _maxY) _maxY = y;
        }

        /// <summary>
        /// The player's world position, read from the town <c>Chara</c> CCharacter's CFrame at
        /// <c>+0x10</c> — laid out (x, y, z) exactly as <c>CFrame::SetPosition</c> writes it. y is the
        /// height.
        ///
        /// This deliberately does NOT use <c>Player.positionX/Y/Z</c>. Those constants are laid out
        /// so that "positionY" sits at the LOWEST address, and a live reading showed the triple as
        /// [1407.34, 170, 43.52] with the middle value tracking the EDITAREA base heights — i.e. the
        /// naming does not survive contact with the data. The CFrame is unambiguous, so use it.
        /// </summary>
        private static bool ReadPos(out float x, out float y, out float z)
        {
            x = y = z = 0f;
            uint p = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return false;

            long c = Memory.ToMmu(p) + EditLoop.CharaPosition;
            x = Memory.ReadFloat(c);
            y = Memory.ReadFloat(c + 4);
            z = Memory.ReadFloat(c + 8);
            return true;
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [GeoramaProbe] " + s);

        private static string ReadCStr(long addr, int max)
        {
            byte[] b = Memory.ReadBytesBatch(addr, max);
            int n = Array.IndexOf(b, (byte)0);
            if (n < 0) n = b.Length;
            return Encoding.ASCII.GetString(b, 0, n).Trim();
        }
    }
}
