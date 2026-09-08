using System;
using static Dark_Cloud_Improved_Version.CanalTide;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Low-tide WADING look: arm the ISO-baked EARLY_STUB (ElfWaterPatches.PatchWaterRedraw) with the player's
    /// model root — and, once the cloth chain is stable, the cape — so the body is drawn before the water
    /// pass and mizu blends over the submerged half. Disarmed at medium/high tide and under the fade.
    /// </summary>
    internal static class CanalWading
    {
        private static void Log(string m) => CanalTide.Log(m, nameof(CanalWading));

        // ── WADING (current design): at LOW TIDE, the PLAYER is drawn EARLY, mizu stays 100% vanilla ─────
        // GS transparency only reveals what is ALREADY in the framebuffer, so for the submerged body to sit
        // "under" the water it must be in the framebuffer before the water part's own native pass draws.
        // At low tide this class arms the mailbox word the ISO-baked EARLY_STUB reads (IsoPatcher.
        // PatchWaterRedraw: the retargeted `jal DrawWater(ground, 0x15)` at 0x17BB6C): MGDraw(player model
        // root) runs just before the water pass, mizu then draws over the submerged half with its native
        // pass/state, and the normal EdDrawCharacter redraw later is Z-clipped at the waterline — leaving a
        // crisp dry top half over the water-blended lower half.
        //
        // Rejected variants (details in memory/water-surface-and-timeofday.md): permanent/low-tide Z-off,
        // whole-quad colour tint, hide-mizu+MGDraw-post-player (frame+0xB0 gates the visual draw itself),
        // park-part-layer+MGDraw-post-player (drew opaque over the body — authored-opaque and/or missing
        // the native pass's blend state).
        private const long  CharModelOff = 0xBC;              // CCharacter +0xBC -> model root CFrame
        private const long  ClothListOff = 0xC74;             // CCharacter +0xC74 -> cloth-piece list (cape early-draw)
        private const int   ClothMaxPieces = 4;               // Draw__CCharacter walks 4 cloth slots
        private const int   CapeStableTicks = 4;              // cloth chain must be valid+unchanged this many ticks before the cape is drawn early
        private const float CapeFadeDisarm  = 16f;            // fade alpha (0..128) past which the cape disarms — the fishing model swaps run under the black
        // The town PLAYER's texture group is HARDCODED 8 in EdDrawCharacter (0x172980: `li a2,8` →
        // ReloadTexture → TextureAnime(player, 8)). The +0x148C per-character group field only exists on
        // the VILLAGER array records (stride 0x14A0 off EdDrawCharacter's a3) — on the player object it
        // reads 0, which is what garbled the early draw when we trusted it.
        private const int   PlayerTexGroup = 8;

        private static bool _loggedArm;                // one log line per low-tide arming
        private static uint _lastClothSig;             // signature of the player cloth chain last tick (stability gate for the cape early-draw)

        // Suppress the early-draw across a PLAYER-MODEL SWAP (the in-place ally switch). The cave MGDraws
        // the model root (chara+0xBC) every frame; an in-place _LOAD_MAIN_CHARA frees/rebuilds that root
        // (Initialize__10CMainChara then LoadPackData2), so drawing the stale/half-built root mid-load feeds
        // the GS a bad packet and hangs. This ONLY bites at Queens LOW TIDE — the one time the early-draw is
        // armed — the observed "swap froze when morning came in Queens" freeze (medium/high-tide swaps, draw
        // disarmed, never froze). The fishing swap doesn't need this: it reloads into the SAME slot so the
        // root address stays valid — which is why the old code's "the body root swaps atomically" held for
        // fishing but NOT for the ally swap, where Initialize invalidates the root.
        //
        // EVENT-DRIVEN, not a flat timer: the swap runs as a REAL (yielding) STB event, so GameMode leaves
        // "walking" for event-mode while the character loads and returns to walking once it's fully built.
        // Suppress exactly for that span (+ a couple settle ticks) so the wading look returns the instant the
        // swap finishes (a flat 3 s window was very visible at the canal floor). A short floor catches the
        // event actually starting (and bails if a fast event finished before we saw it leave walking); a hard
        // cap guarantees we never strand the draw off if GameMode never transitions.
        private const int SwapStartTimeoutMs = 400;    // phase-1: no event-mode seen within this → assume already done, re-arm
        private const int SwapWatchMaxMs     = 5000;   // hard safety cap on total suppression
        private const int SwapSettleTicks    = 2;      // extra ticks after GameMode returns to walking, before re-arming
        private static int      _swapPhase;            // 0 = idle, 1 = fired/awaiting event start, 2 = event running
        private static DateTime _swapFiredAt;
        private static int      _swapSettle;

        /// <summary>Called by AllySwapPrototype the moment it fires an in-place ally swap: hold the canal
        /// early-draw off the model root until the swap event completes.</summary>
        internal static void SuppressForSwap() { _swapPhase = 1; _swapFiredAt = DateTime.UtcNow; _swapSettle = 0; }

        private static bool SwapInProgress()
        {
            if (_swapPhase == 0) return false;
            var elapsed = DateTime.UtcNow - _swapFiredAt;
            if (elapsed.TotalMilliseconds > SwapWatchMaxMs) { _swapPhase = 0; return false; }   // hard cap
            bool walking = Memory.ReadInt(EditLoop.GameMode) == EditLoop.GameModeWalking;
            if (_swapPhase == 1)
            {
                if (!walking) { _swapPhase = 2; _swapSettle = 0; return true; }                 // event started (left walking)
                if (elapsed.TotalMilliseconds > SwapStartTimeoutMs) { _swapPhase = 0; return false; }  // never left walking → assume done
                return true;                                                                    // brief floor until we see it start
            }
            if (!walking) { _swapSettle = 0; return true; }                                     // phase 2: still loading
            if (++_swapSettle < SwapSettleTicks) return true;                                    // settle a couple ticks after it lands
            _swapPhase = 0; return false;                                                        // done → re-arm next tick
        }
        private static int  _capeStableTicks;          // consecutive ticks the cloth chain has been valid+unchanged
        private static bool _loggedCapeGate;           // one log line when the cape is gated off mid-swap

        /// <summary>LOW-TIDE EARLY-PLAYER DRAW (see the WADING note above): arm the baked EARLY_STUB with the
        /// PLAYER's model root so it MGDraws the player BEFORE the water part's native pass — mizu then
        /// blends over the submerged half with its own native pass/state, and the normal EdDrawCharacter
        /// redraw is Z-clipped at the waterline into a crisp dry top half. mizu itself is left entirely
        /// alone (native layer, native pass — no hide, no redraw). Re-armed every tick; disarmed at
        /// medium/high tide and whenever the player pointer is unreadable.</summary>
        internal static void Arm(bool low)
        {
            if (SwapInProgress())
            {
                // A player-model swap is in flight — keep the cave OFF the (rebuilding) model root, or it
                // draws a stale/half-built root and hangs. Force-disarm both mailboxes; make the cape
                // re-settle after. Arm re-arms with the fresh root once the swap event completes.
                Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0);
                Memory.WriteInt(CodeCaves.Mailbox.CapeCharPtr, 0);
                _capeStableTicks = 0;
                return;
            }
            bool armed = false;
            if (low)
            {
                uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
                if (Memory.IsValidGuest(chara))
                {
                    long charaMmu = Memory.ToMmu(chara);
                    uint root = Memory.ReadUInt(charaMmu + CharModelOff) & Memory.PhysAddrMask;
                    if (Memory.IsValidGuest(root))
                    {
                        // The BODY early-draw (root -> MGDraw) is safe: root swaps atomically. The CAPE
                        // early-draw is NOT: the cave walks char+0xC74 -> a 4-entry CCloth pointer array.
                        // During a model swap (fishing enter/quit swaps c01d<->c01d_turi) that chain is
                        // transiently STALE — non-zero garbage the cave's null-guard can't catch, so
                        // Draw__6CCloth feeds the GS a bad packet and the screen hangs. So arm the cape
                        // ONLY when the whole cloth chain is valid AND has been UNCHANGED for a few ticks
                        // (the model has settled); until then leave CapeCharPtr=0 so the cave skips the
                        // cloth loop (its own null-guard) and just draws the body.
                        uint sig = 0; bool clothOk = true;
                        uint clothList = Memory.ReadUInt(charaMmu + ClothListOff) & Memory.PhysAddrMask;
                        if (clothList != 0)
                        {
                            if (Memory.IsValidGuest(clothList))
                            {
                                sig = clothList;
                                long listMmu = Memory.ToMmu(clothList);
                                for (int i = 0; i < ClothMaxPieces; i++)
                                {
                                    uint piece = Memory.ReadUInt(listMmu + i * 4) & Memory.PhysAddrMask;
                                    if (piece != 0 && !Memory.IsValidGuest(piece)) { clothOk = false; break; }
                                    sig = (sig << 3 | sig >> 29) ^ piece;   // order-sensitive fold
                                }
                            }
                            else clothOk = false;
                        }
                        if (clothOk && sig == _lastClothSig && _capeStableTicks < CapeStableTicks) _capeStableTicks++;
                        else if (!clothOk || sig != _lastClothSig) _capeStableTicks = 0;
                        _lastClothSig = sig;
                        // FADE GATE for the fishing model swaps: the session swaps the player model
                        // (c01d <-> c01d_turi) inside the enter/exit scripts, which run UNDER THE BLACK FADE —
                        // and between two mod ticks the cloth chain can go stale-but-non-null the SAME FRAME
                        // the cave draws it, a race no stability counter can close from C# (the intermittent
                        // black-screen hang). So disarm the cape the moment the screen starts darkening
                        // (alpha > gate, far below full black 128): the swap lands many frames later, well
                        // after the disarm, and a dark screen needs no cape anyway. Unlike the earlier hard
                        // InFishingWindow gate, this KEEPS the cape during actual low-tide fishing — the
                        // player wades the canal mid-session and the early draw is exactly what clips them
                        // at the waterline there.
                        if (Memory.ReadFloat(EditLoop.FadeBoxAlpha) > CapeFadeDisarm) _capeStableTicks = 0;
                        bool capeReady = clothOk && _capeStableTicks >= CapeStableTicks;

                        // GROUP + cape char ptr before the FRAME pointer — the pointer is the stub's gate, so
                        // neither must be observable as stale while the pointer is live.
                        Memory.WriteInt(CodeCaves.MizuRedrawTexGroup, PlayerTexGroup);
                        Memory.WriteInt(CodeCaves.Mailbox.CapeCharPtr, capeReady ? (int)chara : 0);
                        Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, (int)root);
                        armed = true;
                        if (!_loggedArm) { Log($"early-player draw armed (model root 0x{root:X}, tex group {PlayerTexGroup})"); _loggedArm = true; }
                        if (!capeReady && !_loggedCapeGate) { Log($"cape early-draw gated (cloth chain unsettled: list 0x{clothList:X}, ok={clothOk}, stable={_capeStableTicks})"); _loggedCapeGate = true; }
                        else if (capeReady) _loggedCapeGate = false;
                    }
                }
            }
            if (!armed) { Memory.WriteInt(CodeCaves.MizuRedrawFramePtr, 0); Memory.WriteInt(CodeCaves.Mailbox.CapeCharPtr, 0); if (!low) _loggedArm = false; }
        }

        internal static void Reset()
        {
            _capeStableTicks = 0; _lastClothSig = 0; _loggedCapeGate = false;   // cape early-draw stability gate
            _loggedArm = false;
        }
    }
}
