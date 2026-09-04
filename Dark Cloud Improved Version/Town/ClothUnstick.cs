using System;
using static Dark_Cloud_Improved_Version.FishingLabelIds;
using static Dark_Cloud_Improved_Version.FishingLabelAllocator;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Heals EXPLODED town-player cloth by firing the engine's OWN reset — not a manual re-seed.
    ///
    /// Symptom: the FIRST time Queens loads in a session (any route in), the player cloth's first
    /// simulation step blows up — a piece's particles fly to ±1e35..1e38 (fp32 overflow the solver never
    /// recovers from), so that piece renders in deep space and reads as "missing" (Toan's FRONT cape half).
    /// Isolated to the PATCHED ISO's Queens-baked content (vanilla + mod is fine); the exact baked trigger
    /// is still being bisected, but the mechanism is a solver instability, not corruption — cloth definition
    /// and buffers are intact. A character swap "fixes" it only because the reload re-inits the cloth.
    ///
    /// The engine already has the right primitive: <c>Step__6CCloth(piece, -1)</c> → <c>Clear__6CCloth</c>
    /// re-drapes every particle to its REST POSE transformed by the anchor's current world matrix (and zeroes
    /// velocity via the Verlet-pair copy). That is a STABLE reset — unlike snapping particles to the single
    /// anchor point, which leaves them coincident and re-destabilises (why the earlier re-seed had no effect).
    /// STB command <c>_INIT_NPC_CLOTH(-1)</c> (143) runs exactly that on the town player (DAT_01d3d21c =
    /// MainChara), so we detect the blow-up and fire a one-line baked event to invoke it.
    ///
    /// Layout (RE'd from savestates — see [[ccloth-particle-layout]]): CCloth 0x8550 bytes, grid dims
    /// +0x2C × +0x30, CURRENT particle positions @+0x1110 stride 0x10 (x,y,z,_); the 4-slot piece list is at
    /// CCharacter +0xC74. Particle 0 is the skeleton-driven anchor (stays sane).
    /// </summary>
    internal static class ClothUnstick
    {
        private const long ClothListOff   = 0xC74;   // CCharacter +0xC74 → 4-slot CCloth pointer array
        private const int  ClothMaxPieces = 4;
        private const long DimAOff        = 0x2C;    // grid dims: count = [+0x2C] × [+0x30]
        private const long DimBOff        = 0x30;
        private const long CurArrOff      = 0x1110;  // current particle positions, stride 0x10
        private const int  ParticleStride = 0x10;
        private const int  MaxParticles   = 256;
        private const float ExplodedBound = 1e6f;    // world coords are ±2e4; past this is a blow-up

        private const int TickInterval = 8;          // main-loop ticks (~50 ms) between scans → ~0.4 s (catch it fast)
        private const int MaxFires     = 4;          // give up after this many resets (avoid a fire loop if it never heals)
        private const int CooldownTicks = 12;        // wait after a fire (~0.6 s) for the Clear to land before re-checking
        private static int _tick, _fires, _cooldown, _lastMap = -1;

        /// <summary>Set false to observe the RAW (unhealed) cloth — used only while bisecting which Queens
        /// bake triggers the explosion. Leave true for normal play.</summary>
        internal static bool HealEnabled = true;   // set false to observe the RAW (unhealed) cloth while bisecting

        /// <summary>Verbose per-decision logging (which gate blocked, post-fire heal state). Off for normal play.</summary>
        internal static bool Diag = false;

        private static int _postFire;   // ticks to keep logging state after a fire (gating diagnosis)

        private static void Log(string m) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[ClothUnstick] " + m);

        internal static void Tick()
        {
            if (!HealEnabled) return;
            if (Memory.ReadByte(Addresses.mode) != 2) return;   // town only

            int map = Memory.ReadInt(EditLoop.MapNo);
            if (map != _lastMap) { _lastMap = map; _fires = 0; _cooldown = 0; _tick = 0; _postFire = 0; }   // fresh town → fresh budget

            // Post-fire monitor: after we fire, log the cloth + gate state for a few ticks so we can SEE whether
            // the Clear actually landed and what MotionStopFlag was doing when the event ran.
            if (_postFire > 0)
            {
                _postFire--;
                uint ch = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
                bool ex = Memory.IsValidGuest(ch) && AnyPieceExploded(Memory.ToMmu(Memory.ReadUInt(Memory.ToMmu(ch) + ClothListOff) & Memory.PhysAddrMask));
                Log($"post-fire: exploded={ex}  MotionStop={Memory.ReadInt(EditLoop.MotionStopFlag)}  " +
                    $"GameMode={Memory.ReadInt(EditLoop.GameMode)}  StartEvent={Memory.ReadInt(EditLoop.StartEventNo)}");
                if (_postFire == 0 && ex) Log("post-fire: STILL EXPLODED — the Clear did not land (gated or not fired).");
                return;
            }

            if (_cooldown > 0) { _cooldown--; return; }
            if (++_tick < TickInterval) return;
            _tick = 0;
            if (_fires >= MaxFires) return;

            uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) return;
            long charaMmu = Memory.ToMmu(chara);
            uint list = Memory.ReadUInt(charaMmu + ClothListOff) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(list)) return;
            long listMmu = Memory.ToMmu(list);

            if (!AnyPieceExploded(listMmu)) return;

            // Explosion present. Log the gate state so a blocked reset is visible, not silent.
            int gm = Memory.ReadInt(EditLoop.GameMode), sev = Memory.ReadInt(EditLoop.StartEventNo), msf = Memory.ReadInt(EditLoop.MotionStopFlag);
            if (Diag) Log($"EXPLODED cloth found — GameMode={gm} (walk={EditLoop.GameModeWalking}) StartEvent={sev} MotionStop={msf}");

            if (gm != EditLoop.GameModeWalking) return;
            if (sev > 0) return;                 // idle start_event_no is -1 (NOT 0); block only a REAL pending event id
            if (msf != 0) return;

            bool fired = FireClothReset();
            if (!fired) { if (Diag) Log("label 406 (ClothResetLabelId) NOT FOUND in the loaded stb — re-patch with the new label."); return; }
            _fires++;
            _cooldown = CooldownTicks;
            _postFire = 6;
            Log($"exploded cloth detected → fired _INIT_NPC_CLOTH(-1) (reset {_fires}/{MaxFires})");
        }

        private static bool AnyPieceExploded(long listMmu)
        {
            for (int i = 0; i < ClothMaxPieces; i++)
            {
                uint piece = Memory.ReadUInt(listMmu + i * 4) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(piece)) continue;
                long p = Memory.ToMmu(piece);
                int count = Memory.ReadInt(p + DimAOff) * Memory.ReadInt(p + DimBOff);
                if (count <= 1 || count > MaxParticles) continue;
                byte[] cur = Memory.ReadBytesBatch(p + CurArrOff, count * ParticleStride);
                for (int q = 1; q < count; q++)   // skip particle 0 (the anchor)
                {
                    int off = q * ParticleStride;
                    if (!Sane(BitConverter.ToSingle(cur, off))     ||
                        !Sane(BitConverter.ToSingle(cur, off + 4)) ||
                        !Sane(BitConverter.ToSingle(cur, off + 8)))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Write and launch a one-line event into the baked <see cref="ClothResetLabelId"/> spare:
        /// <c>_INIT_NPC_CLOTH(-1)</c>, which runs the engine's cloth Clear on the town player. Returns false
        /// if the town stb / label isn't installed yet.</summary>
        private static bool FireClothReset()
        {
            long stb = TownScript.Base();
            if (stb == 0) return false;
            int labelCount = Memory.ReadInt(stb + TownScript.LabelCount);
            int tbl = Memory.ReadInt(stb + TownScript.LabelTable);
            ScriptLabel lab = FindLabelById(stb, labelCount, tbl, ClothResetLabelId);
            if (lab == null) return false;

            // SIMPLE (non-yielding) event: runs to completion inside the launch, staying in walking mode —
            // so MotionStopFlag holds 0 and ClothStep(char,-1) actually reaches Clear__6CCloth. A yield here
            // would enter event-mode and risk the flag gating the reset out. _INIT_NPC_CLOTH is synchronous
            // (unlike _LOAD_MAIN_CHARA, it needs no event-loop pumping), so no yield is required.
            var w = new StbWriter();
            w.PushInt(StbCommands.InitNpcCloth); w.PushInt(-1); w.Ext(2);   // _INIT_NPC_CLOTH(-1) = Clear the player's cloth
            w.Ret();

            Memory.WriteInt(stb + lab.Entry, ClothResetLabelId);
            WriteScript(stb, lab.Off, lab.Off + lab.Size, w, "cloth reset (_INIT_NPC_CLOTH -1)");
            Memory.WriteInt(EditLoop.StartEventNo, ClothResetLabelId);
            return true;
        }

        private static bool Sane(float v) =>
            !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < ExplodedBound;
    }
}
