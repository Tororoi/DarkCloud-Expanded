using System;
using System.IO;
using System.Linq;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TIER-2 fishing sign, step 1: load the game's OWN oasis kanban as a GLOBAL model, exactly the way the
    /// miracle chest (TreasureCursor) loads — a persistent CFrame plus its texture registered into the single
    /// system texture manager 0x1c75870. A later draw hook (step 2, EdEventPointDraw) renders that CFrame in
    /// the three fishing towns. This supersedes the villager-host SignInjector, whose texture failed precisely
    /// because it registered into a transient character context, not 0x1c75870.
    ///
    /// WHY THIS WORKS (see memory iso-patch-pipeline): CreateVUdataFromMDT — the resolver for EVERY mesh,
    /// town scene geometry included — is hardcoded to GetTextureHandle(0x1c75870, name). So a texture entered
    /// into 0x1c75870 with the fixed/persistent flag resolves by name from anywhere, and a CFrame loaded with
    /// LoadMDSFile draws with MGDraw just like the chest.
    ///
    /// THE LOAD (the chest's own two calls, run once via a cave stub reached by the proven cmd-10 dispatch
    /// redirect — CodeCaveFunctions RULE 1):
    ///     EnterIMGFile(0x1c75870, e01b24_bank, -1, 0, 0)          ; 0x132BA0 — persistent (param<0 = EnterFix)
    ///     signCFrame = LoadMDSFile(kanban.mds, 0x1d3a080, 0, 0, 0); 0x1262B0 — stored at SignConfig+8
    ///
    /// DEV-GATED: the two asset buffers are game-derived (carved from the user's own e04 scene.scn/img.pak),
    /// so they are NOT bundled. For runtime development they are read from $DC_SIGN_ASSETS (a folder holding
    /// kanban.mds + e01b24_bank.img); if that env is unset this loader does nothing. The shipped feature will
    /// instead bake the two assets into the ISO's gedit/system and derive them from the disc.
    /// </summary>
    internal static class GlobalSignLoader
    {
        internal static bool Enabled = true;

        // ── engine addresses (guest / ELF vaddr; the stub bakes these in) ──
        private const uint EnterIMGFile = 0x00132BA0;  // CTextureManager::EnterIMGFile(this, buf, i,i,i)
        private const uint LoadMDSFile  = 0x001262B0;  // LoadMDSFile(buf, alloc, i,i,i) -> CFrame
        private const uint GetTexHandle = 0x00131250;  // CTextureManager::GetTextureHandle(this, name, i) -> handle
        private const uint SysTexMgr    = 0x01C75870;  // the one system texture manager
        private const uint MdsAlloc     = 0x01D3A080;  // CDataAlloc2 the chest passes to LoadMDSFile
        private const uint ExclFlagG    = 0x001D3D4A8; // DAT_01d3d4a8 — the "!" draw flag (original cmd-10 act)
        private const uint ExclHandlerG = 0x0018BDD0;  // vanilla _DRAW_EXCLAMATION_MARK (verify-before-redirect)
        private const int  ExclCmdId    = 10;          // its slot in the event VM's flat funcPtr table
        private const long EventTableBasePtr = 0x21D4A434; // *(this)+cmd*4 = the slot the game jalr's
        private const long DrawModelGlobal   = 0x21D19BC0; // DAT_01d19bc0 — the CFrame EdEventPointDraw draws at
                                                           // every type-2 point (EditLoop sets it = TreasureCursor each frame)
        private const long TreasureCursorPtr = 0x202A2908; // the TreasureCursor global itself; override it ONCE and the
                                                           // game's per-frame copy propagates it (no race)

        // ── SignConfig words the stub reads/writes ──
        private static long CfgReady  => CodeCaves.SignConfig + 0x00;  // 1 = perform the load
        private static long CfgLoaded => CodeCaves.SignConfig + 0x04;  // stub sets 1 when done
        private static long CfgCFrame => CodeCaves.SignConfig + 0x08;  // stub stores signCFrame here
        private static long CfgFires  => CodeCaves.SignConfig + 0x0C;  // stub increments (proof it ran)
        private static long CfgHandle => CodeCaves.SignConfig + 0x10;  // stub stores GetTextureHandle("e01b24") — diag

        private static byte[] _mds, _img;
        private static bool _armed, _readySet, _loadSeen, _treasureOverridden;
        private static long _slotMmu;
        private static uint _savedTreasure;
        private static int  _armAttempts, _diagTicks, _drawSlot = -1, _posLogTicks;

        // ── SIGN PLACEMENT (Brownboo) — TUNE HERE then rebuild the mod (no ISO rebuild needed) ──
        // Position is WORLD space (the event point is free-standing, PartIndex = -1). Yaw is radians about Y,
        // applied by EdEventPointDraw as SetRotation(0, yaw, 0). Player-pos is logged each ~2s so you can walk
        // to the exact spot/facing you want and I'll pin these to it.
        internal static float SignX = 212f, SignY = 9f, SignZ = -61f;
        internal static float SignYaw = 0f;   // yaw=pi was north, so yaw=0 faces south

        // ── MANGO = villager SLOT 1, moved back along the boardwalk toward shore. Address by SLOT, not id:
        // +0x1449 is a shared MODEL id (14134 is Kiwi AND Mango), so an id-match hit the wrong villager (slot 0).
        internal static int   MangoSlot = 1;
        internal static float MangoX = 255f, MangoY = 11f, MangoZ = -55f;
        private static bool _mangoDumped;

        /// <summary>Locate the carved sign assets for the runtime prototype: $DC_SIGN_ASSETS first, else walk
        /// up from the assembly dir to a sibling game_data/fishsign (present only on a dev checkout — a shipped
        /// build has neither, so the loader simply stays off). Returns null if not found.</summary>
        private static string ResolveAssetDir()
        {
            var env = Environment.GetEnvironmentVariable("DC_SIGN_ASSETS");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "kanban.mds"))) return env;
            string d = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(d); i++)
            {
                string cand = Path.Combine(d, "game_data", "fishsign");
                if (File.Exists(Path.Combine(cand, "kanban.mds"))) return cand;
                d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar));
            }
            return null;
        }

        // ── MIPS encoders ──
        private const int zero=0, v0=2, a0=4, a1=5, a2=6, a3=7, t0=8, t1=9, sp=29, ra=31;
        private static uint Lui(int rt, uint imm)         => 0x3C000000u | ((uint)rt<<16) | (imm & 0xFFFF);
        private static uint Ori(int rt,int rs,uint imm)   => 0x34000000u | ((uint)rs<<21) | ((uint)rt<<16) | (imm & 0xFFFF);
        private static uint Lw (int rt,int off,int b)     => 0x8C000000u | ((uint)b<<21)  | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Sw (int rt,int off,int b)     => 0xAC000000u | ((uint)b<<21)  | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Addiu(int rt,int rs,int imm)  => 0x24000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(imm & 0xFFFF);
        private static uint Beq(int rs,int rt,int off)    => 0x10000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Bne(int rs,int rt,int off)    => 0x14000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Jr (int rs)                   => ((uint)rs<<21) | 0x08u;
        private const  uint Nop = 0;

        /// <summary>The cmd-10 replacement: a gated ONE-SHOT sign load, then the original "!" flag-set.
        /// 5-arg engine calls, so a 0x20 frame with arg5=0 at sp+0x10; ra saved high at sp+0x1c. Registers are
        /// reloaded after each jal (callees clobber a/v/t/ra). do_flag is index 34 — the branch offsets below
        /// are computed from that.</summary>
        private static byte[] BuildStub()
        {
            uint cHi = CodeCaves.SignConfigGuest >> 16;              // config base hi (0x01FB)
            int rOff = (int)(CodeCaves.SignConfigGuest & 0xFFFF);    // 0x3600
            int lOff = rOff + 0x04, cfOff = rOff + 0x08, fOff = rOff + 0x0C;
            int hOff = rOff + 0x10;
            uint imgHi = CodeCaves.SignImgCaveGuest >> 16, imgLo = CodeCaves.SignImgCaveGuest & 0xFFFF;
            uint nameLo = (CodeCaves.SignImgCaveGuest + 0x10) & 0xFFFF;   // IM2 entry-0 name ("e01b24") = bank+0x10
            uint mdsHi = CodeCaves.SignMdsCaveGuest >> 16, mdsLo = CodeCaves.SignMdsCaveGuest & 0xFFFF;
            uint mgrHi = SysTexMgr >> 16, mgrLo = SysTexMgr & 0xFFFF;
            uint alHi  = MdsAlloc  >> 16, alLo  = MdsAlloc  & 0xFFFF;
            uint exHi  = ExclFlagG >> 16, exLo  = ExclFlagG & 0xFFFF;
            const int DOFLAG = 43;

            var w = new System.Collections.Generic.List<uint>();
            w.Add(Addiu(sp, sp, -0x20));           // 0
            w.Add(Sw(ra, 0x1c, sp));               // 1
            w.Add(Lui(t0, cHi));                   // 2  config base
            w.Add(Lw(t1, fOff, t0));               // 3  fires++
            w.Add(Addiu(t1, t1, 1));               // 4
            w.Add(Sw(t1, fOff, t0));               // 5
            w.Add(Lw(t1, rOff, t0));               // 6  ready?
            w.Add(Beq(t1, zero, DOFLAG-(6+1)));    // 7  !ready -> do_flag
            w.Add(Nop);                            // 8
            w.Add(Lw(t1, lOff, t0));               // 9  loaded?
            w.Add(Bne(t1, zero, DOFLAG-(9+1)));    // 10 already loaded -> do_flag
            w.Add(Nop);                            // 11
            // signCFrame = LoadMDSFile(kanban.mds, 0x1d3a080, 0, 0, 0)   — model FIRST
            w.Add(Lui(a0, mdsHi));                 // 12
            w.Add(Ori(a0, a0, mdsLo));             // 13
            w.Add(Lui(a1, alHi));                  // 14
            w.Add(Ori(a1, a1, alLo));              // 15
            w.Add(Addiu(a2, zero, 0));             // 16
            w.Add(Addiu(a3, zero, 0));             // 17
            w.Add(Sw(zero, 0x10, sp));             // 18 arg5 = 0
            w.Add(CodeCaveFunctions.Jal(LoadMDSFile)); // 19
            w.Add(Nop);                            // 20
            w.Add(Lui(t0, cHi));                   // 21 store cframe
            w.Add(Sw(v0, cfOff, t0));              // 22 cframe = v0
            // EnterIMGFile(0x1c75870, e01b24_bank, -1, 0, 0)   — register texture LAST (after the model load)
            w.Add(Lui(a0, mgrHi));                 // 23
            w.Add(Ori(a0, a0, mgrLo));             // 24
            w.Add(Lui(a1, imgHi));                 // 25
            w.Add(Ori(a1, a1, imgLo));             // 26
            w.Add(Addiu(a2, zero, -1));            // 27 -1 = persistent (EnterFixTexture branch)
            w.Add(Addiu(a3, zero, 0));             // 28
            w.Add(Sw(zero, 0x10, sp));             // 29 arg5 = 0
            w.Add(CodeCaveFunctions.Jal(EnterIMGFile)); // 30
            w.Add(Nop);                            // 31
            // DIAG: handle = GetTextureHandle(0x1c75870, "e01b24", -1)  — is e01b24 actually registered?
            w.Add(Lui(a0, mgrHi));                 // 32
            w.Add(Ori(a0, a0, mgrLo));             // 33
            w.Add(Lui(a1, imgHi));                 // 34 name @ bank+0x10
            w.Add(Ori(a1, a1, nameLo));            // 35
            w.Add(Addiu(a2, zero, -1));            // 36
            w.Add(CodeCaveFunctions.Jal(GetTexHandle)); // 37
            w.Add(Nop);                            // 38
            w.Add(Lui(t0, cHi));                   // 39 reload config base
            w.Add(Sw(v0, hOff, t0));               // 40 handle = v0
            w.Add(Addiu(t1, zero, 1));             // 41
            w.Add(Sw(t1, lOff, t0));               // 42 loaded = 1
            // do_flag (index 43): original _DRAW_EXCLAMATION_MARK behaviour
            w.Add(Lui(t0, exHi));                  // 43
            w.Add(Ori(t0, t0, exLo));              // 44
            w.Add(Addiu(t1, zero, 1));             // 45
            w.Add(Sw(t1, 0, t0));                  // 46 *exclFlag = 1
            w.Add(Lw(ra, 0x1c, sp));               // 47
            w.Add(Addiu(sp, sp, 0x20));            // 48
            w.Add(Jr(ra));                         // 49
            w.Add(Addiu(v0, zero, 1));             // 50 delay slot: v0 = 1
            return CodeCaveFunctions.Assemble(w.ToArray());
        }

        /// <summary>Read the two dev asset buffers (idempotent). Disables the loader if $DC_SIGN_ASSETS is
        /// unset or the files are missing — this is a dev-only experiment until the assets are baked into the ISO.</summary>
        internal static void PrepareAssets()
        {
            if (!Enabled || _mds != null) return;
            string dir = ResolveAssetDir();
            if (dir == null) { Enabled = false; Log("no sign assets (set $DC_SIGN_ASSETS or add game_data/fishsign) — off"); return; }
            try
            {
                _mds = File.ReadAllBytes(Path.Combine(dir, "kanban.mds"));
                _img = File.ReadAllBytes(Path.Combine(dir, "e01b24_bank.img"));
                Log($"assets from {dir}: kanban.mds {_mds.Length}B, e01b24_bank.img {_img.Length}B");
                if (_mds.Length > 0x870 || _img.Length > 0x4790)
                    Log($"WARNING asset too large for its cave: mds={_mds.Length} (<=0x870) img={_img.Length} (<=0x4790)");
            }
            catch (Exception e) { Log($"assets unreadable in {dir}: {e.Message}"); Enabled = false; }
        }

        /// <summary>Arm at the cold window (retry each tick until it takes): write the asset buffers + the load
        /// stub + zeroed config, then repoint cmd-10's dispatch slot at the stub. Idempotent, verify-before-write.</summary>
        internal static void Arm()
        {
            if (!Enabled || _armed || _mds == null) return;
            uint baseGuest = (uint)Memory.ReadInt(EventTableBasePtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(baseGuest)) return;                 // event VM not up yet — retry
            long slotMmu = Memory.ToMmu(baseGuest) + ExclCmdId * 4;
            uint cur = (uint)Memory.ReadInt(slotMmu);
            if (cur == CodeCaves.SignStubCaveGuest) { _armed = true; _slotMmu = slotMmu; return; }
            if (cur != ExclHandlerG)
            {
                if (_armAttempts++ % 40 == 0)
                    Log($"cmd10 slot @0x{slotMmu:X} = 0x{cur:X} (want 0x{ExclHandlerG:X}); waiting/mismatch");
                return;                                                  // not vanilla — retry
            }
            Memory.WriteBytesBatch(CodeCaves.SignMdsCave, _mds);
            Memory.WriteBytesBatch(CodeCaves.SignImgCave, _img);
            Memory.WriteBytesBatch(CodeCaves.SignConfig, new byte[0x20]);
            Memory.WriteBytesBatch(CodeCaves.SignStubCave, BuildStub());
            Memory.WriteUInt(slotMmu, CodeCaves.SignStubCaveGuest);      // RULE 1: the data hand-off
            _slotMmu = slotMmu;
            _armed = (uint)Memory.ReadInt(slotMmu) == CodeCaves.SignStubCaveGuest;
            Log($"armed: cmd10 @0x{slotMmu:X} -> stub 0x{CodeCaves.SignStubCaveGuest:X}; mds {_mds.Length}B img {_img.Length}B");
        }

        /// <summary>Flag the stub ready (once), then watch the config: fires proves the stub ran, cframe proves
        /// the model loaded. The x/y/z are for step 2's draw hook and are unused here.</summary>
        internal static void Tick(float sx, float sy, float sz)
        {
            if (!Enabled || !_armed) return;
            if (!_readySet) { Memory.WriteInt(CfgReady, 1); _readySet = true; Log("ready — next '!' will load the sign"); }

            int loaded = Memory.ReadInt(CfgLoaded);
            if (!_loadSeen && (++_diagTicks % 16 == 0 || loaded != 0))
            {
                int fires  = Memory.ReadInt(CfgFires);
                uint cframe = (uint)Memory.ReadInt(CfgCFrame);
                int handle = Memory.ReadInt(CfgHandle);
                Log($"DIAG: fires={fires} loaded={loaded} signCFrame=0x{cframe:X} e01b24Handle={handle} (>=0 registered, <0 NOT)");
                if (loaded != 0)
                {
                    _loadSeen = true;
                    // Is e01b24's name actually in the manager table, and does the search bound (*mgr) reach it?
                    long mgr = 0x21C75870;
                    int texBound = Memory.ReadInt(mgr);        // SearchTextureName search bound (*mgr)
                    int occupied = 0, firstFree = -1, slot = -1;
                    var names = new System.Collections.Generic.List<string>();
                    for (int i = 1; i < 0xC4; i++)
                    {
                        string nm = (Memory.ReadString(mgr + 0x10F8 + i * 0x50 + 8, 12) ?? "").Split('\0')[0];
                        if (nm.Length > 0) { occupied++; if (names.Count < 20) names.Add(nm); } else if (firstFree < 0) firstFree = i;
                        if (nm == "e01b24") slot = i;
                    }
                    // do boot/system textures survive into town? (gaiji/fuki256 = boot mes_tex; cursor/ibox01 = boot chest)
                    bool has(string n) => names.Contains(n) || Enumerable.Range(1, 0xC3).Any(i =>
                        ((Memory.ReadString(mgr + 0x10F8 + i * 0x50 + 8, 12) ?? "").Split('\0')[0]) == n);
                    Log($"texmgr: bound={texBound} occupied={occupied} firstFree={firstFree} e01b24@{slot} | " +
                        $"gaiji={has("gaiji")} fuki256={has("fuki256")} cursor={has("cursor")} ibox01={has("ibox01")}");
                    Log($"  first names: {string.Join(",", names)}");
                    uint caveGpf = (uint)Memory.ReadInt(0x21F80000);   // the boot cave stored GetPackFile("fishsign.img") here
                    Log($"  cave GetPackFile(fishsign.img)=0x{caveGpf:X} " +
                        $"({(caveGpf == 0 ? "NULL -> grown pak not loaded / redirect miss" : "found -> EnterIMGFile is the issue")})");
                    // Does the game's IN-RAM DATA.HD2 actually have my redirect for mes_tex.pak (archive idx 4483)?
                    uint[] gl = { (uint)Memory.ReadInt(0x20251688), (uint)Memory.ReadInt(0x2025168C),
                                  (uint)Memory.ReadInt(0x20251698), (uint)Memory.ReadInt(0x2025169C) };
                    Log($"  archive globals 688/68c/698/69c = 0x{gl[0]:X}/0x{gl[1]:X}/0x{gl[2]:X}/0x{gl[3]:X}");
                    foreach (uint gp in gl)
                    {
                        uint p = gp & Memory.PhysAddrMask;
                        if (p < 0x100000 || p >= 0x2000000) continue;
                        uint off = (uint)Memory.ReadInt(Memory.ToMmu(p) + 16 + 4483 * 32);
                        if (off == 0x67162800 || off == 0x58E96000)
                            Log($"    HD2 @0x{p:X}: mes_tex[4483].off=0x{off:X} " +
                                $"({(off == 0x67162800 ? "MY REDIRECT (in RAM!)" : "ORIGINAL -> game read an unpatched HD2")})");
                    }
                    // verify the buffer the stub actually hands EnterIMGFile is our bank
                    long img = CodeCaves.SignImgCave;
                    string mag = Memory.ReadString(img, 4) ?? "";
                    int cnt = Memory.ReadInt(img + 4);
                    int itype = (Memory.ReadInt(img + 0x60) >> 24) & 0xFF;   // TIM2(@img+0x40)+0x23 = format
                    string ename = (Memory.ReadString(img + 0x10, 8) ?? "").Split('\0')[0];
                    Log($"imgcave@0x{img:X}: magic='{mag}' count={cnt} entry0='{ename}' itype@+0x63={itype} (want IMG,1,e01b24,5)");
                    if (Memory.IsValidGuest(cframe & Memory.PhysAddrMask))
                    {
                        long cf = Memory.ToMmu(cframe & Memory.PhysAddrMask);
                        int mesh = Memory.ReadInt(cf + 0xBC);            // CFrame model/mesh ptr (sanity)
                        Log($"SIGN LOADED — CFrame 0x{cframe:X}, +0xBC=0x{mesh & 0xFFFFFFFF:X} (step 2 draws this)");
                    }
                    else Log($"loaded=1 but signCFrame 0x{cframe:X} looks invalid — LoadMDSFile may need a pack, not raw MDS");
                }
            }

            LogPlayerPos();
            PinMango();
            if (loaded != 0) DrawViaEventPoint();
        }

        /// <summary>Move Mango (villager SLOT 1) at his SOURCE: the AI (EdMoveVillager) re-applies
        /// VILLAGER_INFO[slot]+0x70 to the model every frame, so writing +0x70 (with +0x54=0 static) is where
        /// the game naturally positions him. Addressed by slot because the +0x1449 id is a shared model id.</summary>
        internal static void PinMango()
        {
            long info = 0x21D329D0 + MangoSlot * 0x90;        // VILLAGER_INFO[slot] — the AI's position source
            if (!_mangoDumped)
            {
                _mangoDumped = true;
                long v = 0x21D25B90 + MangoSlot * 0x14A0;
                Log($"Mango = villager[{MangoSlot}] modelId={Memory.ReadShort(v + 0x1449)}: VILLAGER_INFO+0x70 was " +
                    $"({Memory.ReadFloat(info + 0x70):0.#}, {Memory.ReadFloat(info + 0x74):0.#}, {Memory.ReadFloat(info + 0x78):0.#}) " +
                    $"-> ({MangoX}, {MangoY}, {MangoZ})");
            }
            Memory.WriteInt  (info + 0x54, 0);                // static: SetPosition straight from +0x70
            Memory.WriteFloat(info + 0x70, MangoX);
            Memory.WriteFloat(info + 0x74, MangoY);
            Memory.WriteFloat(info + 0x78, MangoZ);
        }

        /// <summary>DEV draw (data-only, no native draw code): point the shared type-2 draw model at our sign
        /// and drop a type-2 event point at the spot, so the engine's own EdEventPointDraw renders it. This
        /// overrides the miracle-chest model globally, so it is only safe where there are no real chests
        /// (Brownboo) — the shipped all-towns version uses a chest-safe native draw hook instead.</summary>
        private const long EventCountFromBase = -0x9010;  // count source (EditMapInfo+0x1a250), mirrored to Count each frame
        private const int  MaxEventPoints     = 0x100;    // physical array capacity

        private static void DrawViaEventPoint()
        {
            uint cframe = (uint)Memory.ReadInt(CfgCFrame) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(cframe)) return;
            if (!_treasureOverridden)                            // redirect the chest global to our sign, ONCE (no race)
            {
                _savedTreasure = (uint)Memory.ReadInt(TreasureCursorPtr);
                Memory.WriteUInt(TreasureCursorPtr, cframe);
                _treasureOverridden = true;
                Log($"TreasureCursor 0x{_savedTreasure:X} -> sign 0x{cframe:X} (stable draw via DAT_01d19bc0 copy)");
            }

            long arr = EventPoints.Base();
            if (arr == 0) return;

            if (_drawSlot < 0)                                   // place the point once
            {
                int n = Memory.ReadInt(EventPoints.Count);
                if (n <= 0 || n > MaxEventPoints) return;
                long donor = 0; int freeIdx = -1;
                for (int i = 0; i < n; i++)
                {
                    int ty = Memory.ReadInt(EventPoints.Slot(arr, i) + EventPoints.Type);
                    if (ty != EventPoints.TypeFree) { if (donor == 0) donor = EventPoints.Slot(arr, i); }
                    else if (i >= 1 && freeIdx < 0) freeIdx = i;
                }
                if (donor == 0) { Log("no donor event point to clone"); return; }

                bool append = freeIdx < 0;
                int target = append ? n : freeIdx;
                if (target >= MaxEventPoints) { Log("event-point array physically full"); return; }
                long e0 = EventPoints.Slot(arr, target);
                Memory.WriteBytesBatch(e0, Memory.ReadBytesBatch(donor, EventPoints.Stride)); // inherit unmapped fields
                Memory.WriteInt (e0 + EventPoints.Enabled, 1);
                Memory.WriteInt (e0 + EventPoints.MapFlag, 0);
                Memory.WriteInt (e0 + EventPoints.PartIndex, -1);    // free-standing, Position is world space
                Memory.WriteInt (e0 + EventPoints.ObjectPtr, 0);
                Memory.WriteInt (e0 + EventPoints.FramePtr, 0);
                Memory.WriteInt (e0 + EventPoints.ItemOrLabel, 0);   // type-2 item id — harmless for a draw-only point
                Memory.WriteBytesBatch(e0 + EventPoints.Name, new byte[16]);
                Memory.WriteInt (e0 + EventPoints.Type, EventPoints.TypeItem);  // 2 (draws DAT_01d19bc0 here)
                if (append) Memory.WriteInt(arr + EventCountFromBase, n + 1);   // raise the source count
                _drawSlot = target;
                Log($"type-2 sign point at slot {target} ({(append ? "appended" : "reused")}); DAT_01d19bc0 -> 0x{cframe:X}");
            }

            // Refresh position + yaw EVERY tick so tuning the constants takes effect and the point can't drift.
            long e = EventPoints.Slot(arr, _drawSlot);
            Memory.WriteFloat(e + EventPoints.Position + 0, SignX);
            Memory.WriteFloat(e + EventPoints.Position + 4, SignY);
            Memory.WriteFloat(e + EventPoints.Position + 8, SignZ);
            Memory.WriteFloat(e + EventPoints.Rotation + 4, SignYaw);   // ry -> EdEventPointDraw SetRotation(0, yaw, 0)
        }

        /// <summary>Log the player's world position + facing every ~2s, so a target sign spot can be chosen by
        /// simply walking there. CharaPtr -> +0x10 (x,y,z), +0x60 (rot y).</summary>
        private static void LogPlayerPos()
        {
            if (++_posLogTicks % 40 != 0) return;
            uint chara = (uint)Memory.ReadInt(0x202A1F54) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) return;
            long c = Memory.ToMmu(chara);
            float px = Memory.ReadFloat(c + 0x10), py = Memory.ReadFloat(c + 0x14), pz = Memory.ReadFloat(c + 0x18);
            float ry = Memory.ReadFloat(c + 0x64);
            Log($"player pos=({px:0.#}, {py:0.#}, {pz:0.#}) yaw={ry:0.###} | sign=({SignX:0.#},{SignY:0.#},{SignZ:0.#}) yaw={SignYaw:0.###}");
            // villager dump (identify Mango by his position — he's on the small rock the sign wants)
            for (int i = 0; i < 16; i++)
            {
                long v = 0x21D25B90 + i * 0x14A0;
                int id = Memory.ReadShort(v + 0x1449);
                float vx = Memory.ReadFloat(v + 0x10), vy = Memory.ReadFloat(v + 0x14), vz = Memory.ReadFloat(v + 0x18);
                if (id == 0 || (vx == 0f && vz == 0f)) continue;
                Log($"  villager[{i}] id={id} pos=({vx:0.#}, {vy:0.#}, {vz:0.#})");
            }
        }

        internal static void Uninstall()
        {
            if (_armed && _slotMmu != 0)
            {
                Memory.WriteUInt(_slotMmu, ExclHandlerG);               // restore vanilla "!"
                _armed = false; _slotMmu = 0;
            }
            if (_drawSlot >= 0)
            {
                long arr = EventPoints.Base();
                if (arr != 0) Memory.WriteInt(EventPoints.Slot(arr, _drawSlot) + EventPoints.Type, 0); // free our point
                _drawSlot = -1;
            }
            if (_treasureOverridden)
            {
                Memory.WriteUInt(TreasureCursorPtr, _savedTreasure);   // restore the real chest model
                _treasureOverridden = false;
            }
            _readySet = false; _loadSeen = false; _armAttempts = 0; _diagTicks = 0;
            if (Enabled) Memory.WriteBytesBatch(CodeCaves.SignConfig, new byte[0x20]);
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [GlobalSignLoader] " + s);
    }
}
