using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Port the Dark Cloud 2 "Priscleen" fish into DC1's one empty fish slot (species 8), at runtime.
    ///
    /// CAUGHT-FISH PATH (RE'd — see the fishing-fish-slot-limit / dc2-archive-and-priscleen memos): when a
    /// fish is reeled, EdMoveChara does StartReadBG (clears the BG slots) + LoadFileBG(name_419[species],
    /// read_buffer); then, after a fixed ~120-frame delay, it builds the model from *(GetReadBGFile(0)+0x8c)
    /// — the buffer the BG read landed in. DC1's species 8 is cut: name_419[8] = "chara/f09a.chr", which
    /// does NOT exist, so LoadFileBG queues nothing and the model is null (invisible).
    ///
    /// Injection (all RAM, Brownboo-scoped):
    ///   1. Install: REDIRECT name_419[8] -> "chara/f01a.chr" (a real file) so the BG load succeeds and a
    ///      real BG slot is populated.
    ///   2. Tick: while a species-8 fish is being reeled, find EVERY finished BG slot that loaded the f01a
    ///      stand-in and OVERWRITE its buffer with our converted DC2 Priscleen pack (Resources/Fish/f19a.chr).
    ///      The build (GetPackFile is name-based) then parses OUR pack and shows Priscleen.
    ///
    /// The earlier version matched the slot by a HARD-CODED size (168656) and only the first match — if that
    /// size was wrong, nothing swapped and the f01a stand-in ("Bobo") was built. This version keys on the
    /// caught SPECIES (== 8) plus the slot's FILENAME and scans ALL 32 BG slots, so it is robust to which
    /// BG slot / which fish slot is involved. The build waits ~120 frames after the read completes, so the
    /// overwrite (done once the slot's done-flags are set) is never in a race with the disc read.
    /// </summary>
    internal static class PriscleenFish
    {
        internal static bool Enabled = true;

        /// <summary>TEST AID: stamp every Brownboo fish's CFish species (+0) to 8 so ANY catch shows
        /// Priscleen (species 8 does not spawn naturally yet). Set false for normal play.</summary>
        internal static bool ForceAllSpecies8 = true;

        private const int Species = 8;

        // ---- RE'd addresses (ELF vaddr + 0x20000000, the mod's guest-address convention) ----
        private const long Name419Entry8 = 0x20296590;      // name_419[8] -> the species-8 model-path pointer
        private const uint StandInPathPtr = 0x0029FC38;     // -> "chara/f01a.chr" (name_419[0]; a real file)
        private const string StandInTag  = "f01a";          // identifies the redirected caught-model BG load

        private const long BgReadInfo = 0x21CBB0C0;         // bg_read_info: 32 slots, stride 0x9C
        private const int  BgSlots    = 32;
        private const int  BgStride   = 0x9C;
        private const int  BgActive   = 0x00;               // slot in use (LoadFileBG sets 1)
        private const int  BgDone1    = 0x04, BgDone2 = 0x08;  // both non-zero once the read has completed
        private const int  BgName     = 0x0C;               // char[] the requested filename was copied into
        private const int  BgSize     = 0x90;               // file size
        private const int  BgBuffer   = 0x8C;               // -> the destination buffer the model was read into

        private const long BattleFishPtr = 0x202A2B60;      // BattleFish: the CFish* currently being reeled
        private const long FishArrayPtr  = 0x202A2B58;      // Fish: the CFish[] base pointer
        private const int  FishStride    = 0x2410;
        private const int  CFishSpecies  = 0x00;            // CFish+0 = species id

        private static byte[] _model;
        private static uint _origPath;
        private static bool _redirected;
        private static readonly HashSet<int> _injected = new HashSet<int>();  // BG slots done THIS catch

        private static string ModelPath =>
            Path.Combine(AppContext.BaseDirectory, "Resources", "Fish", "f19a.chr");

        /// <summary>Brownboo spot install: load the Priscleen pack and redirect species 8's model path to a
        /// real stand-in so its BG load succeeds (only matters once a species-8 fish is caught).</summary>
        internal static void Install()
        {
            if (!Enabled) return;
            if (_model == null)
            {
                try { _model = File.ReadAllBytes(ModelPath); }
                catch (Exception e) { Log($"could not read {ModelPath}: {e.Message}"); return; }
            }
            _origPath = Memory.ReadUInt(Name419Entry8);
            Memory.WriteUInt(Name419Entry8, StandInPathPtr);
            _redirected = true;
            _injected.Clear();
            Log($"name_419[8] redirected to stand-in f01a (was 0x{_origPath:X8}); Priscleen model {_model.Length}B ready");
        }

        /// <summary>Undo the path redirect on town change.</summary>
        internal static void Uninstall()
        {
            if (_redirected) { Memory.WriteUInt(Name419Entry8, _origPath); _redirected = false; }
            _injected.Clear();
        }

        /// <summary>TEST AID: stamp every loaded Brownboo fish as species 8 (call once at fishing start).</summary>
        internal static void ForceSpecies8OnFish()
        {
            if (!Enabled || !ForceAllSpecies8) return;
            uint p = Memory.ReadUInt(FishArrayPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(p)) return;
            long arr = Memory.ToMmu(p);
            int n = Memory.ReadInt(FishingSpot.FishNum);
            if (n <= 0 || n > 6) return;
            for (int i = 0; i < n; i++) Memory.WriteInt(arr + (long)i * FishStride + CFishSpecies, Species);
            Log($"TEST: stamped {n} Brownboo fish as species 8 (any catch -> Priscleen)");
        }

        /// <summary>Per-frame: while a species-8 fish is being reeled, overwrite the caught-model buffer of
        /// every finished f01a-stand-in BG slot with the Priscleen model, so the build shows Priscleen.</summary>
        internal static void Tick()
        {
            if (!Enabled || !_redirected || _model == null) return;

            // Gate on the fish being REELED (BattleFish) actually being species 8. BattleFish holds the CFish
            // address and is cleared to 0 by the build, so this window is exactly the catch. When it closes,
            // re-arm for the next catch.
            uint bf = Memory.ReadUInt(BattleFishPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(bf) || Memory.ReadInt(Memory.ToMmu(bf) + CFishSpecies) != Species)
            {
                if (_injected.Count > 0) _injected.Clear();
                return;
            }

            // Scan ALL BG slots (not just slot 0 — robust to whichever slot / fish is used). Inject into each
            // finished slot whose filename is our f01a stand-in, once per catch.
            for (int s = 0; s < BgSlots; s++)
            {
                if (_injected.Contains(s)) continue;
                long slot = BgReadInfo + (long)s * BgStride;
                if (Memory.ReadInt(slot + BgActive) == 0) continue;
                if (Memory.ReadInt(slot + BgDone1) == 0 || Memory.ReadInt(slot + BgDone2) == 0) continue; // still reading
                string fn = Memory.ReadString(slot + BgName, 16);
                if (fn == null || fn.IndexOf(StandInTag, StringComparison.Ordinal) < 0) continue;
                uint bp = Memory.ReadUInt(slot + BgBuffer) & Memory.PhysAddrMask;
                if (!Memory.IsValidGuest(bp)) continue;
                Memory.WriteBytesBatch(Memory.ToMmu(bp), _model);
                Memory.WriteInt(slot + BgSize, _model.Length);
                _injected.Add(s);
                Log($"injected Priscleen ({_model.Length}B) into BG slot {s} buffer 0x{bp:X8} (file '{fn.TrimEnd('\0')}')");
            }
        }

        private static void Log(string s) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + " [PriscleenFish] " + s);
    }
}
