using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The glow Toan carries while a charged sword is primed. It is drawn by the game's own wall-torch routine through the
    /// Divine Beast cat's glow cave (ElfCave.CatGlowDraw) — the same path the Matador's charged pellet borrows — and the
    /// mailbox words are written in that cave's order: everything set while it is off, then armed last. The disc is Toan's
    /// own, baked into his dungeon pack, and <see cref="Show"/> names it: <see cref="ToanGlowBakes.GlowName"/>'s gold for
    /// Sun Sword, <see cref="ToanGlowBakes.BlueName"/>'s blue for Big Bang. The cat's is in Xiao's pack and is not
    /// resident for him. Both anchors are his model root, so the glow needs no named bone — <see cref="Lift"/> raises it
    /// from his feet to his chest, and <see cref="Scale"/> sizes it to him rather than to the cat.
    /// </summary>
    internal static class SolarGlow
    {
        // Binding one of Toan's OWN shipped textures here drew NOTHING at all, which cleared the baked disc of suspicion:
        // the texture was never the problem, the DRAW was — an unposed anchor (see Show) rather than a missing upload.
        // The COLOUR is the disc: Toan's discs carry their ramp baked into the CLUT and the palette cave repaints only the
        // cat's, so Sun Sword's gold and Big Bang's blue are two textures rather than two palette rows. Held from Show to
        // Hide, because Reserve and Release must move and restore the same manager entry.
        private static string _disc = ToanGlowBakes.GlowName;
        // ⚠ SCALE IS NOT A MODEL SCALE. The sprite is 45 × 22.5 units at 1.0 (cat_glow_draw.s), so 1.0 is already
        // wider than Toan is tall: a wall torch uses 1.0, the cat 0.5 × its look scale, the Matador's pellet 0.4.
        // 1.15 with the torches' 15-unit pull put a sprite bigger than a torch right in front of the camera and
        // washed the screen white. Toan wants a little more than the cat, drawn where he stands.
        private const float  Scale = 1.0f;    // the flame sprite is 45 × 22.5 units at 1.0 — about Toan's height
        private const int    Flags = 2;       // the flickering flame sprite ALONE. 1 adds the steady glow pair (18 × 9 at 1.0), which
                                              // draws as a second, much smaller glow beside the first — the cat and the Matador both
                                              // use 2 for exactly that reason; 3 (both) is what put a tiny duplicate in the corner.
        private const float  Pull  = 5f;      // toward the camera, as the cat and the Matador use (the torches' 15 is to clear their wall)
        private const float  Lift  = 0f;      // AT the anchor bone. It is a spine bone, around chest height, so +8 put the
                                              // glow on his head and +14 above it; centred on his mid-height is 0.
        internal const double GrowSeconds = 0.25;  // it swells from nothing rather than snapping on — and the charge starts
                                                   // it this early, so it is at full size the instant the charge is ready
        private const double FadeSeconds = 0.50;   // …and shrinks away again when a charge is spent unused
        private static double _grow = GrowSeconds, _fade = FadeSeconds;   // this showing's own timings (a hand-off runs faster)
        private static bool  _on, _fading, _driven;   // driven: its size is set by hand (Drive) rather than swelling on its own clock
        private static float _k;                      // the size it was last written at, 0..1 — what a fade shrinks from
        private static DateTime _shownAt, _fadeAt;
        private static int   _palRow;                 // the palette row this showing asked the cave for (0 = none); handed back to None on Hide
        private static float _scaleMax = Scale;       // this showing's full size
        private const int    PalRowNone = 6;

        /// <summary>Up, once. Re-arming every tick would re-bind the texture each frame.</summary>
        /// <param name="anchor">A GUEST node to hang the disc on instead of Toan (BladeProp.RootGuest for the judgement
        /// blade); 0 = Toan's own spine. A glow already up is re-hung when the anchor changes — one data write.</param>
        /// <param name="lift">Height above the anchor node, world units (negative = below); Toan's spine wants 0.</param>
        /// <param name="growSeconds">How long it takes to swell to full; <see cref="GrowSeconds"/> unless a hand-off is pacing it.</param>
        /// <param name="palRow">The glow cave's ONE-based palette row to paint the disc with (0 = leave it: Toan's discs carry their
        /// own palette; the cat's disc, the one resident for Xiao, takes a row — 8 is the Angel Gear cat's gold).</param>
        /// <param name="scale">The disc's full size (0 = <see cref="Scale"/>, Toan's; the Matador's pellet wants 0.4).</param>
        internal static void Show(string disc = ToanGlowBakes.GlowName, uint anchor = 0, float lift = Lift, double growSeconds = GrowSeconds, int palRow = 0, float scale = 0f)
        {
            if (scale > 0f) _scaleMax = scale; else if (!_on) _scaleMax = Scale;
            if (_on)
            {
                uint want = anchor != 0 ? anchor : Anchor();
                if (want != 0 && want != _anchor)
                {
                    Memory.WriteUInt(CodeCaves.Mailbox.CatGlowNodeA, want);
                    Memory.WriteUInt(CodeCaves.Mailbox.CatGlowNodeB, want);
                    Memory.WriteFloat(CodeCaves.Mailbox.CatGlowLift, lift);
                    _anchor = want;
                }
                KeepAlive(); return;
            }
            _disc = disc;
            uint root = anchor != 0 ? anchor : Anchor();
            _anchor = root;
            if (root == 0) return;                                       // no posed bone yet: try again next tick
            Reserve();                                                   // …and give the disc a home that is actually uploaded, BEFORE the cave binds it
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 0);
            if (palRow > 0) Memory.WriteInt(CodeCaves.Mailbox.CatGlowPalRow, palRow);
            _palRow = palRow;
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, 0f);       // …from nothing; Tick swells it over GrowSeconds
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowFlags, Flags);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowPull, Pull);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowLift, lift);
            Memory.WriteUInt (CodeCaves.Mailbox.CatGlowNodeA, root);
            Memory.WriteUInt (CodeCaves.Mailbox.CatGlowNodeB, root);
            byte[] nm = new byte[16]; System.Text.Encoding.ASCII.GetBytes(_disc).CopyTo(nm, 0);
            Memory.WriteBytesBatch(CodeCaves.Mailbox.CatGlowName, nm);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowReady, 0);        // bind the disc
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 1);           // armed last
            _on = true; _fading = false; _driven = false; _k = 0f; _shownAt = GameClock.Now; _grow = Math.Max(0.01, growSeconds);
            // Where the anchor really sits, so any residual offset is one measurement rather than another guess: the cave
            // places the sprite at the node's posed world position (world matrix translation row), and his feet are the
            // player's own height.
            float anchorH = Memory.ReadFloat(Memory.ToMmu(root) + CFrameVu1.WorldMatrix + 0x30 + 4);
            float feetH   = Memory.ReadFloat(Addresses.dunPositionZ);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] glow up at bone 0x{root:X} (disc `{_disc}`, scale {Scale:0.00}, lift {Lift:0}); bone sits {anchorH - feetH:0.#} above his feet");
        }

        /// <summary>One line describing where the disc actually LIVES: its manager entry, the block it belongs to, its TEX0
        /// page, and that block's uploaded window. A sprite drawn from a page outside its block's base..top samples whatever
        /// else is there — which is what a garbled square instead of a soft disc means. Diagnostic only.</summary>
        /// <summary>Per tick while it is up: swell in over <see cref="GrowSeconds"/>, or shrink away over
        /// <see cref="FadeSeconds"/> once <see cref="Fade"/> has been called, taking it down at the end.</summary>
        internal static void Tick()
        {
            if (!_on) return;
            float k;
            if (_fading)
            {
                double t = (GameClock.Now - _fadeAt).TotalSeconds / _fade;
                if (t >= 1.0) { Hide(); return; }
                k = _fadeFrom * (float)(1.0 - t);                       // from wherever it was, not from full
            }
            else if (_driven) return;                                    // Drive holds its size
            else k = (float)Math.Min(1.0, (GameClock.Now - _shownAt).TotalSeconds / _grow);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, _scaleMax * k);
            _k = k;
        }
        /// <summary>Its size set by hand, 0..1 — a glow growing with something else's own progress (the charge
        /// blade's fade-in) rather than on a clock. Holds until a fade or hide.</summary>
        internal static void Drive(float k)
        {
            if (!_on || _fading) return;
            _driven = true;
            k = Math.Max(0f, Math.Min(1f, k));
            if (Math.Abs(k - _k) < 0.01f) return;
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, _scaleMax * k);
            _k = k;
        }
        private static float _fadeFrom = 1f;

        internal static bool IsUp => _on;
        /// <summary>Up at this node, on its way down or not — what must be cut before that node goes away.</summary>
        internal static bool AnchoredTo(uint anchor) => _on && anchor != 0 && _anchor == anchor;
        /// <summary>Up (and not on its way down) at this node — 0 meaning Toan's own spine.</summary>
        internal static bool OnAnchor(uint anchor) => _on && !_fading && _anchor == (anchor != 0 ? anchor : Anchor());

        /// <summary>Start shrinking it away (a charge going unused), rather than cutting it.</summary>
        /// <param name="seconds">How long the shrink takes; <see cref="FadeSeconds"/> unless a hand-off is pacing it.</param>
        internal static void Fade(double seconds = FadeSeconds)
        {
            if (!_on || _fading) return;
            _fading = true; _driven = false; _fadeAt = GameClock.Now; _fade = Math.Max(0.01, seconds); _fadeFrom = _k;
        }

        /// <summary>Down, and the disc back where it loaded.</summary>
        internal static void Hide()
        {
            if (_palRow != 0) { Memory.WriteInt(CodeCaves.Mailbox.CatGlowPalRow, PalRowNone); _palRow = 0; }   // the cave repaints the disc for whoever uses it next
            if (!_on) return;
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowOn, 0);
            Release();
            _on = false; _fading = false;
        }

        /// <summary>Keep the disc in a block that RELOADS, at an address nobody overwrites.
        ///
        /// The probe settled it: clearing an unused chara group's loaded flag left it zero, so NOTHING services chara
        /// texture groups — a slot cannot carry the disc, invisible character or not. The cat never relied on that either:
        /// its textures sit in Xiao's own block, which the scene draw reloads every frame, and the group retag governs which
        /// group the slot BINDS, not what gets uploaded.
        ///
        /// So the disc stays tagged to Toan's block, which reloads every frame, and only its pixels move — to the private
        /// window above every other block, where nothing else uploads (its old home at 0x2000 sat inside block 3's window,
        /// and block 3 re-uploads every frame, which is what kept shredding it). One wrinkle: ReloadTexture skips an entry
        /// whose address is above its block's top unless the block is marked unloaded, so that flag is cleared every tick —
        /// the block then re-sends all its entries, ours included.</summary>
        private static void Reserve()
        {
            if (_block >= 0) return;
            long e = FindEntry(_disc);
            if (e == 0) return;
            uint highest = 0;
            for (int b2 = 0; b2 < 0x48; b2++)
                highest = Math.Max(highest, Memory.ReadUInt(BlockAddr(b2) + TextureManager.BlkTop));
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            uint window = (cursor - WindowBlocks) & ~0x1Fu;
            if (window < highest)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[SunSword] no VRAM above the blocks (cursor 0x{cursor:X}, highest top 0x{highest:X}) — no glow this time");
                return;
            }
            _tex0Saved = Tex0(e); _blockSaved = Memory.ReadUShort(e);          // the block tag stays exactly as it is
            ulong keep = _tex0Saved & ~(ulong)TextureManager.Tex0AddrMask & ~((ulong)TextureManager.Tex0AddrMask << TextureManager.Tex0CbpShift);
            ulong moved = keep | window | ((ulong)(window + 0x10) << TextureManager.Tex0CbpShift);
            Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)moved);
            Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(moved >> 32));
            Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, window);   // a real allocation: the cursor bumps DOWN
            _cursorSaved = cursor; _cursorTaken = window; _block = _blockSaved;
            Memory.WriteUInt(BlockAddr(_block) + TextureManager.BlkLoaded, 0);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[SunSword] disc stays in block {_block} (reloaded every frame) but moves to its own window 0x{window:X}..0x{window + WindowBlocks:X}, clear of every block");
        }

        /// <summary>Every tick, mark the disc's block unloaded so the uploader re-sends ALL its entries — ours included, which
        /// it would otherwise skip for sitting above the block's top.</summary>
        private static void KeepAlive()
        {
            if (_block < 0) return;
            Memory.WriteUInt(BlockAddr(_block) + TextureManager.BlkLoaded, 0);
        }

        /// <summary>The disc back where it loaded, and its block re-sent so it lands there again.</summary>
        private static void Release()
        {
            if (_block < 0) return;
            long e = FindEntry(_disc);
            if (e != 0)
            {
                Memory.WriteUInt(e + TextureManager.EntryTex0, (uint)_tex0Saved);
                Memory.WriteUInt(e + TextureManager.EntryTex0 + 4, (uint)(_tex0Saved >> 32));
                Memory.WriteUShort(e, _blockSaved);
            }
            uint cur = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            if (cur == _cursorTaken) Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _cursorSaved);
            Memory.WriteUInt(BlockAddr(_block) + TextureManager.BlkLoaded, 0);
            _block = -1; _cursorTaken = 0;
        }
        // ── The disc's own VRAM window ────────────────────────────────────────────────────────────────────
        // The disc loads at the TOP of Toan's texture block, and measuring found TWO other loaded blocks whose
        // windows cover those same pages (block 3 0x1A40..0x2CA0 and block 42 0x1A40..0x2160 over the disc's
        // 0x2000..0x2014). Whichever uploads last owns them, so the sprite sampled another texture and drew a
        // striped rectangle — the registers, the size and the placement were all correct. The cat hit exactly this
        // and solved it by moving its textures above every block's top; this does the same for the one disc.
        private const uint   WindowBlocks = 0x20;          // one PSMT8 page: 16 blocks of pixels + 4 of CLUT, rounded up
        private static int    _block = -1;                 // the texture group the disc has been moved into
        private static uint   _cursorSaved, _cursorTaken;
        private static ulong  _tex0Saved;                  // the entry's TEX0 before the move
        private static ushort _blockSaved;

        private static long BlockAddr(int b) => TextureManager.Base + TextureManager.Blocks + (long)b * TextureManager.BlockStride;

        /// <summary>A posed bone on Toan, near his spine.
        ///
        /// ⚠ NOT the model root: a root carries an unposed transform while the engine poses its children, so a glow hung
        /// there sits at the world origin — which is the stray sprite, and why lowering the lift made it vanish instead of
        /// sliding down his chest. The live weapon is parented to the wielder's hand bone (CharacterClone reads it the same
        /// way rather than trusting a bone INDEX, since every character's skeleton differs), so that pointer is a posed bone
        /// for free; walking up its parents to just below the root lands on the spine.</summary>
        private static uint _anchor;
        private static uint Anchor()
        {
            uint modelRoot = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (!Memory.IsValidGuest(modelRoot)) return 0;
            int nw = Memory.ReadInt(WeaponModel.NowWeaponPtr);
            if (!Memory.IsValidGuest((uint)nw)) return modelRoot;
            uint wRoot = Memory.ReadGuestPtr(Memory.ToMmu(nw) + WeaponModel.WeaponModelRootOffset);
            if (!Memory.IsValidGuest(wRoot)) return modelRoot;
            uint node = Memory.ReadGuestPtr(Memory.ToMmu(wRoot) + CFrameVu1.Parent);     // his hand
            if (!Memory.IsValidGuest(node)) return modelRoot;
            for (int i = 0; i < 4; i++)                                                   // …up the arm toward the spine
            {
                uint par = Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.Parent);
                if (!Memory.IsValidGuest(par) || par == modelRoot) break;
                node = par;
            }
            return node;
        }

        private static ulong Tex0(long entry) =>
            (ulong)Memory.ReadUInt(entry + TextureManager.EntryTex0) | ((ulong)Memory.ReadUInt(entry + TextureManager.EntryTex0 + 4) << 32);

        /// <summary>The manager entry of that name, or 0.</summary>
        private static long FindEntry(string name)
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base));
            for (int i = 0; i < count; i++)
            {
                long e = TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride;
                byte[] nb = Memory.ReadBytesBatch(e + TextureManager.EntryName, 32);
                if (nb == null) continue;
                int len = 0; while (len < nb.Length && nb[len] != 0) len++;
                if (System.Text.Encoding.ASCII.GetString(nb, 0, len) == name) return e;
            }
            return 0;
        }
    }
}
