using System;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Render the fishing SIGN in towns that have none (Brownboo, Yellow Drops, Queens), at runtime, no ISO.
    ///
    /// THE PROBLEM (see docs/custom-fishing-spot.md §8c-sign): the fishing signboard geometry exists only inside
    /// per-town scene.scn archives (the oasis kanban) and no .chr carries it, so it cannot be reached by a
    /// pointer trick. And a town object's model loads SYNCHRONOUSLY (EdLoadVillager → CDRead → build, one call),
    /// so there is no buffer-overwrite window like the fish BG-read (which is how Priscleen is injected).
    ///
    /// THE SOLUTION (the user's code-cave insight): bundle the game's OWN sign as a self-contained fishsign.chr
    /// (oasis kanban + its e01b24 texture — the texture travels, so it renders in towns that lack the bank), and
    /// let the engine's own build run on OUR bytes via a cave stub. Because we redirect the build's SOURCE, not
    /// race a copy, the synchronous load is fine.
    ///
    /// THE MECHANISM: the villager build path is EdInitVilager → EdLoadVillager_build(buf, name, npc, alloc)
    /// (0x1860C0), which GetPackFile's the pack and calls the model-load virtual. A cave stub — reached via the
    /// STB external-command dispatch table (the ONLY proven cave entry, see docs/cave-code-execution.md) — does
    /// the two native calls the mod cannot make itself:
    ///     Initialize(npc)                                              ; 0x1569E0
    ///     EdLoadVillager_build(SignChrCave, "fishsign", npc, 0x1D1B360); 0x1860C0
    /// We host it by repointing _DRAW_EXCLAMATION_MARK's dispatch slot (cmd 10, funcPtr @ 0x20269CA8) at the
    /// stub: the injected fishing script draws "!" every frame near the spot, so the stub fires there — it
    /// builds the sign ONCE (ready+built gate in SignConfig) then does the original flag-set so "!" still works.
    /// npc = a spare EdVillager slot; the mod pins the sign's world position each tick.
    ///
    /// STATUS: first pass — compiles; the MIPS stub, region placement and positioning all need LIVE TESTING.
    /// </summary>
    internal static class SignInjector
    {
        internal static bool Enabled = true;

        // ── RE'd addresses (guest / ELF vaddr; the stub encodes these directly) ──
        private const uint Initialize      = 0x001569E0;   // Initialize__12CNPCharacter(this)
        private const uint BuildOverload   = 0x001860C0;   // EdLoadVillager(buf, name, npc, alloc) — builds the model
        private const uint VillagerAllocG  = 0x001D1B360;  // EdVillagerBuffer (the CDataAlloc2 arena)
        private const long EdVillagerBase  = 0x21D25B90;   // MMU: CNPCharacter[0]
        private const int  VillagerStride  = 0x14A0;
        private const int  VillagerSlots   = 10;
        private const long PeopleCount     = 0x202A27E0;   // people_list — active villager count
        // VILLAGER_INFO drives the villager AI (EdMoveVillager 0x186EF0 loops all 10 slots, stride 0x90).
        // Per slot: +0x40 active gate (!=0 → processed & drawn), +0x48 parts-object idx (<0 → static path),
        // +0x54 mode (0 → SetPosition straight from +0x70, no walk), +0x70/74/78 target pos, +0x80/84/88 rot.
        // So a slot with {+0x40=1, +0x48=-1, +0x54=0, +0x70=trigger} is a STATIONARY villager the AI itself
        // pins at the trigger every frame — no fight, no flicker. A slot whose +0x40 was 0 (beyond the town's
        // active villagers) has no name/talk data, so it shows no talk-name board either.
        private const long VillagerInfoBase   = 0x21D329D0; // VILLAGER_INFO[0] (MMU)
        private const int  VillagerInfoStride = 0x90;
        private const int  GroundSnapFlag     = 0x147C;     // EdVillager[slot]+0x147C — AI ground-snap gate
        private const int  TalkIdField        = 0x1444;     // CNPCharacter+0x1444 — talk-event id; <0 = non-talkable
        private const long NpcColFlags        = 0x21D3D244; // DAT_01d3d244[slot] (stride 4) — body-collision enable
        private const uint ExclFlagG       = 0x001D3D4A8;  // DAT_01d3d4a8 — the "!" draw flag (original behaviour)
        private const uint ExclHandlerG    = 0x0018BDD0;   // the vanilla _DRAW_EXCLAMATION_MARK handler
        private const int  ExclCmdId       = 10;           // its command id in the EVENT VM's flat dispatch table
        // The event VM dispatches EXT via a FLAT funcPtr array (table[cmd_id]), NOT a {funcPtr,id} table. The
        // array base is a runtime pointer at CRunScript+4 (event CRunScript = 0x1D4A430). So cmd 10's slot is
        // *(0x21D4A434) + 10*4 — a pure data write the game's own jalr follows (cave-code-execution RULE 1).
        private const long EventTableBasePtr = 0x21D4A434;

        // ── config words the stub reads (SignConfig) ──
        private static long CfgReady => CodeCaves.SignConfig + 0x00;
        private static long CfgBuilt => CodeCaves.SignConfig + 0x04;
        private static long CfgNpc   => CodeCaves.SignConfig + 0x08;
        private static long CfgName  => CodeCaves.SignConfig + 0x10;   // "fishsign\0"
        private const uint  CfgReadyG = CodeCaves.SignConfigGuest + 0x00;
        private const uint  CfgBuiltG = CodeCaves.SignConfigGuest + 0x04;
        private const uint  CfgNpcG   = CodeCaves.SignConfigGuest + 0x08;
        private const uint  CfgNameG  = CodeCaves.SignConfigGuest + 0x10;

        private static byte[] _chr;
        private static bool _armed;
        private static int  _slot = -1;
        private static long _npcMmu;
        private static long _infoMmu;
        private static int  _diagTicks;
        private static bool _buildSeen;
        private static long _slotMmu;      // the flat-table slot we redirected (for restore)
        private static int  _armAttempts;

        private static string ChrPath =>
            Path.Combine(AppContext.BaseDirectory, "Resources", "Sign", "fishsign.chr");

        // ── MIPS encoders (register numbers) ──
        private const int zero=0, v0=2, a0=4, a1=5, a2=6, a3=7, t0=8, t1=9, sp=29, ra=31;
        private static uint Lui(int rt, uint imm)          => 0x3C000000u | ((uint)rt<<16) | (imm & 0xFFFF);
        private static uint Ori(int rt, int rs, uint imm)  => 0x34000000u | ((uint)rs<<21) | ((uint)rt<<16) | (imm & 0xFFFF);
        private static uint Lw (int rt, int off, int b)    => 0x8C000000u | ((uint)b<<21)  | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Sw (int rt, int off, int b)    => 0xAC000000u | ((uint)b<<21)  | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Addiu(int rt,int rs,int imm)   => 0x24000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(imm & 0xFFFF);
        private static uint Beq(int rs,int rt,int off)     => 0x10000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Bne(int rs,int rt,int off)     => 0x14000000u | ((uint)rs<<21) | ((uint)rt<<16) | (uint)(off & 0xFFFF);
        private static uint Jr (int rs)                    => ((uint)rs<<21) | 0x08u;
        private const  uint Nop = 0;

        /// <summary>The cave stub — a fresh _DRAW_EXCLAMATION_MARK replacement: gated one-shot sign build, then
        /// the original flag-set. Registers reloaded after each jal (callees clobber t/a/v/ra).</summary>
        private static byte[] BuildStub()
        {
            // config base halves (config = 0x01FB3600): ready@+0, built@+4, npc@+8, name@+0x10 via t0=lui hi.
            uint cHi = CodeCaves.SignConfigGuest >> 16;       // 0x01FB
            int  rOff = (int)(CodeCaves.SignConfigGuest & 0xFFFF);        // 0x3600
            int  bOff = rOff + 0x04, nOff = rOff + 0x08, nameOff = rOff + 0x10;
            uint chrHi = CodeCaves.SignChrCaveGuest >> 16, chrLo = CodeCaves.SignChrCaveGuest & 0xFFFF; // 0x01FA / 0xE400
            uint alHi = VillagerAllocG >> 16, alLo = VillagerAllocG & 0xFFFF;   // 0x001D / 0xB360
            uint exHi = ExclFlagG >> 16,     exLo = ExclFlagG & 0xFFFF;         // 0x001D / 0xD4A8

            int fOff = rOff + 0x0C;              // fires counter @ config+0x0C
            // Instruction layout (indices matter for the branch offsets to do_flag = index 28).
            var w = new System.Collections.Generic.List<uint>();
            w.Add(Addiu(sp, sp, -16));           // 0
            w.Add(Sw(ra, 0, sp));                // 1
            w.Add(Lui(t0, cHi));                 // 2  t0 = config hi
            w.Add(Lw(t1, fOff, t0));             // 3  fires++
            w.Add(Addiu(t1, t1, 1));             // 4
            w.Add(Sw(t1, fOff, t0));             // 5  (persistent proof the stub ran)
            w.Add(Lw(t1, rOff, t0));             // 6  t1 = ready
            w.Add(Beq(t1, zero, 28-(7+1)));      // 7  if !ready -> do_flag (offset 20)
            w.Add(Nop);                          // 8
            w.Add(Lw(t1, bOff, t0));             // 9  t1 = built
            w.Add(Bne(t1, zero, 28-(10+1)));     // 10 if built -> do_flag (offset 17)
            w.Add(Nop);                          // 11
            w.Add(Lw(a0, nOff, t0));             // 12 a0 = npc
            w.Add(CodeCaveFunctions.Jal(Initialize)); // 13 Initialize(npc)
            w.Add(Nop);                          // 14
            w.Add(Lui(a0, chrHi));               // 15 buf = SignChrCave
            w.Add(Ori(a0, a0, chrLo));           // 16
            w.Add(Lui(a1, cHi));                 // 17 name = config+0x10
            w.Add(Ori(a1, a1, (uint)nameOff));   // 18
            w.Add(Lui(t0, cHi));                 // 19 reload config base
            w.Add(Lw(a2, nOff, t0));             // 20 a2 = npc
            w.Add(Lui(a3, alHi));                // 21 alloc
            w.Add(Ori(a3, a3, alLo));            // 22
            w.Add(CodeCaveFunctions.Jal(BuildOverload)); // 23 build
            w.Add(Nop);                          // 24
            w.Add(Lui(t0, cHi));                 // 25 reload config
            w.Add(Ori(t1, zero, 1));             // 26 t1 = 1
            w.Add(Sw(t1, bOff, t0));             // 27 built = 1
            // do_flag (index 28):
            w.Add(Lui(t0, exHi));                // 28 DAT flag
            w.Add(Ori(t0, t0, exLo));            // 29
            w.Add(Ori(t1, zero, 1));             // 30 t1 = 1
            w.Add(Sw(t1, 0, t0));                // 31 *DAT = 1
            w.Add(Lw(ra, 0, sp));                // 32 restore ra
            w.Add(Addiu(sp, sp, 16));            // 33
            w.Add(Jr(ra));                       // 34
            w.Add(Ori(v0, zero, 1));             // 35 v0 = 1 (delay slot)
            return CodeCaveFunctions.Assemble(w.ToArray());
        }

        /// <summary>Load the asset + write config name (idempotent). Call at install.</summary>
        internal static void PrepareAssets()
        {
            if (_chr != null) return;
            try { _chr = File.ReadAllBytes(ChrPath); }
            catch (Exception e) { Log($"could not read {ChrPath}: {e.Message}"); Enabled = false; }
        }

        /// <summary>Arm at the cold window (retry every tick until it takes): write the asset + stub + name,
        /// then repoint the dispatch slot. Idempotent.</summary>
        internal static void Arm()
        {
            if (!Enabled || _armed || _chr == null) return;
            uint baseGuest = (uint)Memory.ReadInt(EventTableBasePtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(baseGuest)) return;                     // event VM not set up yet — retry
            long slotMmu = Memory.ToMmu(baseGuest) + ExclCmdId * 4;
            uint cur = (uint)Memory.ReadInt(slotMmu);
            if (cur == CodeCaves.SignStubCaveGuest) { _armed = true; _slotMmu = slotMmu; return; }  // already armed
            if (cur != ExclHandlerG)
            {
                if (_armAttempts++ % 40 == 0)
                    Log($"cmd10 slot @0x{slotMmu:X} = 0x{cur:X} (want 0x{ExclHandlerG:X}); tableBase=0x{baseGuest:X} — waiting/mismatch");
                return;                                                      // not the vanilla handler — retry (verify-before-write)
            }
            Memory.WriteBytesBatch(CodeCaves.SignChrCave, _chr);            // the sign pack
            Memory.WriteBytesBatch(CodeCaves.SignConfig, new byte[0x20]);  // zero config
            Memory.WriteBytesBatch(CfgName, System.Text.Encoding.ASCII.GetBytes("fishsign\0"));
            Memory.WriteBytesBatch(CodeCaves.SignStubCave, BuildStub());   // the cave stub (code)
            Memory.WriteUInt(slotMmu, CodeCaves.SignStubCaveGuest);        // RULE 1: the data hand-off
            _slotMmu = slotMmu;
            _armed = (uint)Memory.ReadInt(slotMmu) == CodeCaves.SignStubCaveGuest;
            Log($"armed: cmd10 slot @0x{slotMmu:X} -> stub 0x{CodeCaves.SignStubCaveGuest:X} (tableBase 0x{baseGuest:X}), asset {_chr.Length}B");
        }

        /// <summary>Choose a spare villager slot + flag the stub ready. Once built, pin the sign at the water.</summary>
        internal static void Tick(float sx, float sy, float sz)
        {
            if (!Enabled || !_armed) return;
            if (_slot < 0)
            {
                int count = Memory.ReadInt(PeopleCount);
                _slot = PickFreeSlot();                                         // inactive slot (no name/talk), else last
                _npcMmu  = EdVillagerBase   + (long)_slot * VillagerStride;
                _infoMmu = VillagerInfoBase + (long)_slot * VillagerInfoStride;
                bool free = Memory.ReadInt(_infoMmu + 0x40) == 0;
                Memory.WriteInt(CfgNpc, (int)(_npcMmu & Memory.PhysAddrMask));  // guest npc ptr for the stub
                Memory.WriteInt(CfgReady, 1);                                   // let the next "!" build it
                Log($"slot {_slot} (villagers={count}, {(free ? "free" : "hijack")}) npc=0x{_npcMmu & Memory.PhysAddrMask:X}; ready");
            }

            // DIAGNOSTICS: fires counter (persistent — proves the stub was CALLED), built, model ptr, name.
            // Log ~once/second until the build is seen, so it captures the moment you're at the "!".
            int built = Memory.ReadInt(CfgBuilt);
            if (!_buildSeen && (++_diagTicks % 16 == 0 || built != 0))
            {
                int fires = Memory.ReadInt(CodeCaves.SignConfig + 0x0C);        // stub call count (persistent)
                int model = Memory.ReadInt(_npcMmu + 0xBC);                     // npc+0xBC = model/CFrame ptr
                string nm = (Memory.ReadString(_npcMmu + 0x1448, 16) ?? "").Split('\0')[0];  // name the build writes
                Log($"DIAG: fires={fires} built={built} model@0xBC=0x{model & 0xFFFFFFFF:X} name='{nm}'");
                if (built != 0) { _buildSeen = true; Log("BUILD RAN — sign model constructed into slot " + _slot); }
            }

            if (built != 0) PinSign(sx, sy, sz);
        }

        /// <summary>Find a villager slot the town's AI is NOT using (VILLAGER_INFO+0x40 == 0). Such a slot has no
        /// name/talk data, so it renders no talk-name board and won't fight a real villager. Falls back to the
        /// last slot if every slot is active (then we hijack it, made stationary + we clear its talk data).</summary>
        private static int PickFreeSlot()
        {
            for (int i = VillagerSlots - 1; i >= 0; i--)   // search from the back — least likely to be re-spawned
            {
                long info = VillagerInfoBase + (long)i * VillagerInfoStride;
                if (Memory.ReadInt(info + 0x40) == 0) return i;
            }
            return VillagerSlots - 1;
        }

        /// <summary>Pin the sign by feeding the villager AI a STATIONARY villager at the trigger. EdMoveVillager
        /// positions active slots from VILLAGER_INFO+0x70 when +0x54==0 (static, no walk), so writing the trigger
        /// there makes the AI itself hold the sign in place — no flicker. Also neutralize talk so no name board.</summary>
        private static void PinSign(float sx, float sy, float sz)
        {
            if (_infoMmu == 0 || _npcMmu == 0) return;
            // 1) Configure the slot as an active, stationary, non-walking villager pinned at the trigger.
            Memory.WriteInt (_infoMmu + 0x40, 1);     // active → processed & drawn
            Memory.WriteInt (_infoMmu + 0x48, -1);    // no parts object → static branch
            Memory.WriteInt (_infoMmu + 0x54, 0);     // static: SetPosition straight from +0x70 (no walk logic)
            Memory.WriteFloat(_infoMmu + 0x70, sx);   // the position the AI applies every frame
            Memory.WriteFloat(_infoMmu + 0x74, sy);
            Memory.WriteFloat(_infoMmu + 0x78, sz);
            Memory.WriteInt (_npcMmu + GroundSnapFlag, 0);  // skip the AI's terrain ground-snap → exact position

            // Strip the villager behaviors so it reads as a static prop, not an NPC:
            Memory.WriteInt (_npcMmu + TalkIdField, -1);        // non-talkable → no name board, no talk, no greet-facing
            Memory.WriteInt (NpcColFlags + (long)_slot * 4, 0); // body-collision off → player can stand at the spot

            // 2) First-frame placement (before the AI's next pass) via the CObject + model CFrames directly.
            Memory.WriteFloat(_npcMmu + 0x10, sx);
            Memory.WriteFloat(_npcMmu + 0x14, sy);
            Memory.WriteFloat(_npcMmu + 0x18, sz);
            foreach (int mo in new[] { 0xBC, 0xC0 })
            {
                uint cf = (uint)Memory.ReadInt(_npcMmu + mo) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(cf)) continue;
                long cframe = Memory.ToMmu(cf);
                Memory.WriteFloat(cframe + 0x220, sx);
                Memory.WriteFloat(cframe + 0x224, sy);
                Memory.WriteFloat(cframe + 0x228, sz);
                Memory.WriteInt(cframe + 0x24C, 1);
                Memory.WriteInt(cframe + 0x240, 0);
            }
        }

        internal static void Uninstall()
        {
            if (_armed && _slotMmu != 0)
            {
                Memory.WriteUInt(_slotMmu, ExclHandlerG);   // restore vanilla _DRAW_EXCLAMATION_MARK
                _armed = false; _slotMmu = 0;
            }
            _slot = -1; _npcMmu = 0; _infoMmu = 0; _armAttempts = 0; _buildSeen = false; _diagTicks = 0;
            if (Enabled) Memory.WriteBytesBatch(CodeCaves.SignConfig, new byte[0x20]);
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [SignInjector] " + s);
    }
}
